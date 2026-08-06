import {
  ChangeDetectionStrategy, Component, ElementRef, HostListener, computed, effect, inject, input,
  output, signal, viewChild,
} from '@angular/core';
import { I18nService } from '../../core/i18n.service';

/** The two identification components an operator supplies for a 21 CFR Part 11 signing. */
export interface EsignCredentials {
  password: string;
  pin: string;
}

/**
 * Reusable 21 CFR Part 11 electronic-signature ceremony dialog (§11.200(a)(1) —
 * a signing demands BOTH identification components: the account password and the
 * signature PIN). Declarative and record-agnostic: the parent opens it for a
 * specific signing, shows the meaning the signer is attesting to, and receives the
 * captured credentials via (confirm). The parent never stores the credentials — it
 * forwards them straight to the signing request, and the server verifies them.
 *
 * Affordance only: the button that opens this is a convenience; the server rejects
 * a wrong password/PIN (SIG-002/SIG-001) and throttles repeats like a login.
 * Accessible modal (role="dialog" + aria-modal, Escape/Cancel dismiss, focus moves
 * in on open and back on close), mirroring TextPromptDialogComponent.
 */
@Component({
  selector: 'qams-esign-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div class="scrim" (click)="onCancel()" aria-hidden="true"></div>
      <div class="modal" role="dialog" aria-modal="true" aria-labelledby="esign-title">
        <h3 id="esign-title">{{ i18n.t('esign.title') }}</h3>
        <p class="meaning">{{ meaning() }}</p>

        <label for="esign-password">{{ i18n.t('esign.password') }}</label>
        <input
          #firstBox
          id="esign-password"
          type="password"
          autocomplete="current-password"
          [value]="password()"
          (input)="password.set($any($event.target).value)" />

        <label for="esign-pin">{{ i18n.t('esign.pin') }}</label>
        <input
          id="esign-pin"
          type="password"
          inputmode="numeric"
          autocomplete="off"
          [value]="pin()"
          [placeholder]="i18n.t('esign.pinHint')"
          (input)="pin.set($any($event.target).value)" />

        @if (error()) { <p class="error">{{ error() }}</p> }

        <div class="row">
          <button type="button" [disabled]="!canConfirm() || busy()" (click)="onConfirm()">
            {{ busy() ? i18n.t('esign.signing') : i18n.t('esign.sign') }}
          </button>
          <button type="button" class="secondary" [disabled]="busy()" (click)="onCancel()">
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
    h3 { margin: 0 0 8px; font-size: 15px; font-weight: 700; color: var(--nt-slate); }
    .meaning { margin: 0 0 14px; font-size: 13px; color: var(--nt-slate); }
    label { display: block; font-size: 12.5px; font-weight: 600; color: var(--nt-slate); margin-bottom: 4px; }
    input { width: 100%; box-sizing: border-box; margin-bottom: 10px; }
    .row { display: flex; gap: .6rem; margin-top: 6px; }
    button { width: auto; }
  `],
})
export class EsignDialogComponent {
  readonly i18n = inject(I18nService);

  /** Whether the dialog is shown; the parent owns this state. */
  readonly open = input.required<boolean>();
  /** Human-readable statement of what signing means here (becomes the signature's recorded meaning server-side). */
  readonly meaning = input('');
  /** True while the signing request is in flight; disables the controls. */
  readonly busy = input(false);
  /** Server error to surface (e.g. wrong PIN); cleared by the parent on reopen. */
  readonly error = input('');

  /** Emits the captured credentials when the operator confirms. */
  readonly confirm = output<EsignCredentials>();
  /** Emits when the operator dismisses without signing. */
  readonly cancel = output<void>();

  readonly password = signal('');
  readonly pin = signal('');

  /** Both identification components must be present before signing is offered (§11.200(a)(1)). */
  readonly canConfirm = computed(() => this.password().trim() !== '' && this.pin().trim() !== '');

  private readonly firstBox = viewChild<ElementRef<HTMLInputElement>>('firstBox');
  private previouslyFocused: HTMLElement | null = null;

  constructor() {
    effect(() => {
      const box = this.firstBox();
      if (this.open() && box) {
        const active = document.activeElement;
        this.previouslyFocused = active instanceof HTMLElement ? active : null;
        this.password.set('');
        this.pin.set('');
        box.nativeElement.focus();
      } else if (!this.open() && this.previouslyFocused) {
        this.previouslyFocused.focus();
        this.previouslyFocused = null;
      }
    });
  }

  onConfirm(): void {
    if (this.canConfirm() && !this.busy()) {
      this.confirm.emit({ password: this.password(), pin: this.pin() });
    }
  }

  onCancel(): void {
    if (!this.busy()) { this.cancel.emit(); }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open() && !this.busy()) { this.cancel.emit(); }
  }
}
