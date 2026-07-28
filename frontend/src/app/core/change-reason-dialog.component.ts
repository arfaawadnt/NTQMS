import {
  ChangeDetectionStrategy, Component, ElementRef, HostListener, effect, inject, signal, viewChild,
} from '@angular/core';
import { ChangeReasonService } from './change-reason.service';
import { I18nService } from './i18n.service';

/**
 * Accessible modal that captures the 21 CFR Part 11 reason-for-change,
 * replacing window.prompt (EA finding UI-014). Hosted once in the shell and
 * driven entirely by ChangeReasonService signals: role="dialog" +
 * aria-modal, labelled by its visible title, focus moves to the textarea on
 * open and back to the invoking control on close, Escape or Cancel dismisses,
 * and Confirm stays disabled until a non-blank reason is entered.
 */
@Component({
  selector: 'qams-change-reason-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (svc.open()) {
      <div class="scrim" (click)="svc.cancel()" aria-hidden="true"></div>
      <div class="modal" role="dialog" aria-modal="true" aria-labelledby="change-reason-title">
        <h3 id="change-reason-title">{{ i18n.t(svc.titleKey()) }}</h3>
        <p class="explain">{{ i18n.t('changeReason.explain') }}</p>
        <label for="change-reason-input">{{ i18n.t('changeReason.reason') }}</label>
        <textarea
          #reasonBox
          id="change-reason-input"
          rows="3"
          [value]="reason()"
          (input)="reason.set($any($event.target).value)"
          [placeholder]="i18n.t('changeReason.placeholder')"></textarea>
        <div class="row">
          <button type="button" [disabled]="reason().trim() === ''" (click)="svc.confirm(reason())">
            {{ i18n.t('changeReason.confirm') }}
          </button>
          <button type="button" class="secondary" (click)="svc.cancel()">
            {{ i18n.t('changeReason.cancel') }}
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
    h3 { margin: 0 0 6px; font-size: 15px; font-weight: 700; color: var(--nt-slate); }
    .explain { margin: 0 0 14px; font-size: 12.5px; color: var(--nt-grey-m); }
    label { display: block; font-size: 12.5px; font-weight: 600; color: var(--nt-slate); margin-bottom: 4px; }
    textarea { width: 100%; resize: vertical; box-sizing: border-box; }
    .row { display: flex; gap: .6rem; margin-top: 14px; }
    button { width: auto; }
  `],
})
export class ChangeReasonDialogComponent {
  readonly svc = inject(ChangeReasonService);
  readonly i18n = inject(I18nService);

  /** The operator's draft reason; reset every time the dialog opens. */
  readonly reason = signal('');

  private readonly reasonBox = viewChild<ElementRef<HTMLTextAreaElement>>('reasonBox');

  /** The control that had focus before the dialog opened, to restore on close. */
  private previouslyFocused: HTMLElement | null = null;

  constructor() {
    effect(() => {
      const box = this.reasonBox();
      if (this.svc.open() && box) {
        const active = document.activeElement;
        this.previouslyFocused = active instanceof HTMLElement ? active : null;
        this.reason.set('');
        box.nativeElement.focus();
      } else if (!this.svc.open() && this.previouslyFocused) {
        this.previouslyFocused.focus();
        this.previouslyFocused = null;
      }
    }, { allowSignalWrites: true });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.svc.open()) { this.svc.cancel(); }
  }
}
