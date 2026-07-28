import {
  ChangeDetectionStrategy, Component, ElementRef, HostListener, effect, inject, signal, viewChild,
} from '@angular/core';
import { TextPromptService } from './text-prompt.service';
import { I18nService } from './i18n.service';

/**
 * Accessible single-input modal replacing window.prompt (backlog R-4), a
 * sibling of ChangeReasonDialogComponent for values that are not a Part 11
 * reason — e.g. an admin-set new password (masked input). Hosted once in the
 * shell and driven entirely by TextPromptService signals: role="dialog" +
 * aria-modal, labelled by its visible title, focus moves to the input on open
 * and back to the invoking control on close, Escape or Cancel dismisses, and
 * Confirm stays disabled until a non-blank value is entered.
 */
@Component({
  selector: 'qams-text-prompt-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (svc.open()) {
      <div class="scrim" (click)="svc.cancel()" aria-hidden="true"></div>
      <div class="modal" role="dialog" aria-modal="true" aria-labelledby="text-prompt-title">
        <h3 id="text-prompt-title">{{ i18n.t(svc.prompt().titleKey) }}</h3>
        <label for="text-prompt-input">{{ i18n.t(svc.prompt().labelKey) }}</label>
        <input
          #valueBox
          id="text-prompt-input"
          [type]="svc.prompt().inputType"
          [value]="value()"
          (input)="value.set($any($event.target).value)"
          [placeholder]="svc.prompt().placeholderKey ? i18n.t(svc.prompt().placeholderKey) : ''" />
        <div class="row">
          <button type="button" [disabled]="value().trim() === ''" (click)="svc.confirm(value())">
            {{ i18n.t('common.confirm') }}
          </button>
          <button type="button" class="secondary" (click)="svc.cancel()">
            {{ i18n.t('common.cancel') }}
          </button>
        </div>
      </div>
    }
  `,
  styles: [`
    .scrim { position: fixed; inset: 0; background: rgba(59, 70, 88, .42); z-index: 300; }
    .modal {
      position: fixed; top: 50%; left: 50%; transform: translate(-50%, -50%);
      width: min(480px, 92vw); background: var(--nt-surface); border-radius: 8px;
      box-shadow: var(--nt-shadow-pop); z-index: 310; padding: 20px 22px;
      border-top: 4px solid var(--nt-teal);
    }
    h3 { margin: 0 0 12px; font-size: 15px; font-weight: 700; color: var(--nt-slate); }
    label { display: block; font-size: 12.5px; font-weight: 600; color: var(--nt-slate); margin-bottom: 4px; }
    input { width: 100%; box-sizing: border-box; }
    .row { display: flex; gap: .6rem; margin-top: 14px; }
    button { width: auto; }
  `],
})
export class TextPromptDialogComponent {
  readonly svc = inject(TextPromptService);
  readonly i18n = inject(I18nService);

  /** The operator's draft value; reset every time the dialog opens. */
  readonly value = signal('');

  private readonly valueBox = viewChild<ElementRef<HTMLInputElement>>('valueBox');

  /** The control that had focus before the dialog opened, to restore on close. */
  private previouslyFocused: HTMLElement | null = null;

  constructor() {
    effect(() => {
      const box = this.valueBox();
      if (this.svc.open() && box) {
        const active = document.activeElement;
        this.previouslyFocused = active instanceof HTMLElement ? active : null;
        this.value.set('');
        box.nativeElement.focus();
      } else if (!this.svc.open() && this.previouslyFocused) {
        this.previouslyFocused.focus();
        this.previouslyFocused = null;
      }
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.svc.open()) { this.svc.cancel(); }
  }
}
