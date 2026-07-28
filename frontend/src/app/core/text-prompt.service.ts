import { Injectable, signal } from '@angular/core';

/** What the text-prompt dialog should ask for. All keys are i18n keys. */
export interface TextPromptOptions {
  /** i18n key of the visible dialog title. */
  titleKey: string;
  /** i18n key of the input's label. */
  labelKey: string;
  /** Optional i18n key of the input's placeholder. */
  placeholderKey?: string;
  /** Input rendering: plain text or a masked password field. Defaults to text. */
  inputType?: 'text' | 'password';
}

/** Resolved options with defaults applied, as consumed by the dialog. */
interface ActivePrompt {
  titleKey: string;
  labelKey: string;
  placeholderKey: string;
  inputType: 'text' | 'password';
}

const INACTIVE: ActivePrompt = { titleKey: '', labelKey: '', placeholderKey: '', inputType: 'text' };

/**
 * Collects a single line of text (or an admin-set password) through the
 * accessible modal hosted once in the shell (TextPromptDialogComponent),
 * replacing window.prompt (backlog R-4). Generalizes the ChangeReasonService
 * pattern: callers await request(); the dialog resolves the promise with the
 * entered value, or null when the operator cancels.
 */
@Injectable({ providedIn: 'root' })
export class TextPromptService {
  private resolver: ((value: string | null) => void) | null = null;

  private readonly openState = signal(false);
  private readonly promptState = signal<ActivePrompt>(INACTIVE);

  /** Whether the dialog is currently shown (bound by the dialog component). */
  readonly open = this.openState.asReadonly();

  /** The active prompt's title/label/placeholder keys and input type. */
  readonly prompt = this.promptState.asReadonly();

  /**
   * Opens the dialog and resolves with the entered value, or null on cancel.
   * A dialog already in flight is cancelled first so its caller never hangs.
   */
  request(options: TextPromptOptions): Promise<string | null> {
    this.settle(null);
    this.promptState.set({
      titleKey: options.titleKey,
      labelKey: options.labelKey,
      placeholderKey: options.placeholderKey ?? '',
      inputType: options.inputType ?? 'text',
    });
    this.openState.set(true);
    return new Promise<string | null>((resolve) => { this.resolver = resolve; });
  }

  /** Confirms with the operator's value; blank input is treated as cancel. */
  confirm(value: string): void {
    this.settle(value.trim() === '' ? null : value);
    this.openState.set(false);
  }

  /** Dismisses the dialog without a value (Escape / Cancel / scrim). */
  cancel(): void {
    this.settle(null);
    this.openState.set(false);
  }

  private settle(value: string | null): void {
    this.resolver?.(value);
    this.resolver = null;
  }
}
