import { Injectable, signal } from '@angular/core';

/** Default dialog title — callers may supply their own key (e.g. legal hold). */
const DEFAULT_TITLE_KEY = 'changeReason.title';

/**
 * Collects a 21 CFR Part 11 reason-for-change through the accessible modal
 * hosted once in the shell (ChangeReasonDialogComponent), replacing the old
 * window.prompt. Callers await request(); the dialog resolves the promise
 * with the trimmed reason, or null when the operator cancels.
 */
@Injectable({ providedIn: 'root' })
export class ChangeReasonService {
  private resolver: ((reason: string | null) => void) | null = null;

  private readonly openState = signal(false);
  private readonly titleKeyState = signal(DEFAULT_TITLE_KEY);

  /** Whether the dialog is currently shown (bound by the dialog component). */
  readonly open = this.openState.asReadonly();

  /** i18n key of the visible dialog title. */
  readonly titleKey = this.titleKeyState.asReadonly();

  /**
   * Opens the dialog and resolves with the trimmed reason, or null on cancel.
   * A dialog already in flight is cancelled first so its caller never hangs.
   */
  request(titleKey?: string): Promise<string | null> {
    this.settle(null);
    this.titleKeyState.set(titleKey ?? DEFAULT_TITLE_KEY);
    this.openState.set(true);
    return new Promise<string | null>((resolve) => { this.resolver = resolve; });
  }

  /** Confirms with the operator's reason; blank input is treated as cancel. */
  confirm(reason: string): void {
    const trimmed = reason.trim();
    this.settle(trimmed === '' ? null : trimmed);
    this.openState.set(false);
  }

  /** Dismisses the dialog without a reason (Escape / Cancel / scrim). */
  cancel(): void {
    this.settle(null);
    this.openState.set(false);
  }

  private settle(reason: string | null): void {
    this.resolver?.(reason);
    this.resolver = null;
  }
}
