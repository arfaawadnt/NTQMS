import { ChangeDetectionStrategy, Component, inject, model, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { I18nService } from '../core/i18n.service';
import { PermissionsService } from '../core/permissions.service';
import { DrawerComponent } from '../shared/ui/drawer.component';

/** Cross-field check: the confirmation must repeat the new secret exactly. */
function confirmMatches(field: string, confirm: string) {
  return (group: AbstractControl): ValidationErrors | null =>
    group.get(field)?.value === group.get(confirm)?.value ? null : { confirmMismatch: true };
}

/**
 * The signed-in user's own credentials: password change and e-signature PIN
 * setup/change. Both flows re-verify the current password — a live session is
 * not a password, and the PIN is one of the two Part 11 signing components, so
 * neither may be replaced on the strength of a session cookie alone.
 *
 * Hosted once in the shell; opened from the header user menu.
 */
@Component({
  selector: 'qams-my-account-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DrawerComponent],
  template: `
    <qams-drawer [open]="open()" [title]="i18n.t('acct.title')" (closed)="close()">
      <div class="acct">
        <div class="idcard">
          <span class="avatar">{{ initials() }}</span>
          <div>
            <div class="nm">{{ auth.displayName() }}</div>
            <div class="rl">{{ perms.roleName() ?? auth.role() }}</div>
          </div>
        </div>

        <!-- Password -->
        <section>
          <h3>{{ i18n.t('acct.password') }}</h3>
          <form class="drawer-form" [formGroup]="passwordForm" (ngSubmit)="changePassword()">
            <label>
              {{ i18n.t('acct.currentPassword') }}
              <input type="password" formControlName="current" autocomplete="current-password" />
            </label>
            <label>
              {{ i18n.t('acct.newPassword') }}
              <input type="password" formControlName="next" autocomplete="new-password" />
            </label>
            <label>
              {{ i18n.t('acct.confirmPassword') }}
              <input type="password" formControlName="confirm" autocomplete="new-password" />
            </label>
            @if (passwordForm.hasError('confirmMismatch') && passwordForm.get('confirm')?.touched) {
              <p class="error">{{ i18n.t('acct.mismatch') }}</p>
            }
            <p class="hint">{{ i18n.t('acct.passwordRules') }}</p>
            @if (passwordError()) { <p class="error">{{ passwordError() }}</p> }
            @if (passwordDone()) { <p class="done">{{ i18n.t('acct.passwordChanged') }}</p> }
            <button type="submit" [disabled]="passwordForm.invalid || busy()">
              {{ i18n.t('acct.changePassword') }}
            </button>
          </form>
        </section>

        <!-- E-signature PIN -->
        <section>
          <h3>{{ i18n.t('acct.pin') }}</h3>
          <p class="pinstate" [class.unset]="!perms.pinConfigured()">
            {{ perms.pinConfigured() ? i18n.t('acct.pinSet') : i18n.t('acct.pinUnset') }}
          </p>
          <form class="drawer-form" [formGroup]="pinForm" (ngSubmit)="changePin()">
            <label>
              {{ i18n.t('acct.currentPassword') }}
              <input type="password" formControlName="password" autocomplete="current-password" />
            </label>
            <label>
              {{ i18n.t('acct.newPin') }}
              <input type="password" formControlName="pin" inputmode="numeric" maxlength="4"
                     [placeholder]="i18n.t('acct.pinHint')" />
            </label>
            <label>
              {{ i18n.t('acct.confirmPin') }}
              <input type="password" formControlName="confirmPin" inputmode="numeric" maxlength="4" />
            </label>
            @if (pinForm.hasError('confirmMismatch') && pinForm.get('confirmPin')?.touched) {
              <p class="error">{{ i18n.t('acct.mismatch') }}</p>
            }
            @if (pinError()) { <p class="error">{{ pinError() }}</p> }
            @if (pinDone()) { <p class="done">{{ i18n.t('acct.pinChanged') }}</p> }
            <button type="submit" [disabled]="pinForm.invalid || busy()">
              {{ perms.pinConfigured() ? i18n.t('acct.changePin') : i18n.t('acct.setPin') }}
            </button>
          </form>
        </section>
      </div>
    </qams-drawer>
  `,
  styles: [`
    .acct { display: flex; flex-direction: column; gap: 20px; }
    .idcard { display: flex; gap: 12px; align-items: center; padding: 12px;
              background: var(--nt-brand-soft); border-radius: var(--nt-radius-card); }
    .avatar { display: grid; place-items: center; width: 40px; height: 40px; flex: none;
              border-radius: 50%; background: var(--nt-navy); color: #fff; font-weight: 800; }
    .nm { font-weight: 700; color: var(--nt-navy); }
    .rl { font-size: 12px; color: var(--nt-grey-d); }
    section h3 { margin-bottom: 10px; }
    .pinstate { font-size: 12.5px; color: var(--nt-ink-ok); font-weight: 600; margin: 0 0 8px; }
    .pinstate.unset { color: var(--nt-ink-warn); }
    .done { color: var(--nt-ink-ok); font-size: 12.5px; font-weight: 600; }
    button { width: auto; }
  `],
})
export class MyAccountDrawerComponent {
  readonly auth = inject(AuthService);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Two-way opened state, owned by the shell. */
  readonly open = model(false);

  readonly busy = signal(false);
  readonly passwordError = signal('');
  readonly passwordDone = signal(false);
  readonly pinError = signal('');
  readonly pinDone = signal(false);

  readonly passwordForm = this.fb.nonNullable.group({
    current: ['', [Validators.required]],
    // Mirrors the server's StrongPassword floor; the server stays authoritative.
    next: ['', [Validators.required, Validators.minLength(12), Validators.maxLength(200)]],
    confirm: ['', [Validators.required]],
  }, { validators: [confirmMatches('next', 'confirm')] });

  readonly pinForm = this.fb.nonNullable.group({
    password: ['', [Validators.required]],
    pin: ['', [Validators.required, Validators.pattern(/^\d{4}$/)]],
    confirmPin: ['', [Validators.required]],
  }, { validators: [confirmMatches('pin', 'confirmPin')] });

  initials(): string {
    return (this.auth.displayName() ?? '')
      .split(/\s+/).map((w) => w[0] ?? '').join('').slice(0, 2).toUpperCase();
  }

  close(): void {
    this.open.set(false);
    this.passwordForm.reset();
    this.pinForm.reset();
    this.passwordError.set(''); this.passwordDone.set(false);
    this.pinError.set(''); this.pinDone.set(false);
  }

  async changePassword(): Promise<void> {
    if (this.passwordForm.invalid) { return; }
    const raw = this.passwordForm.getRawValue();
    this.busy.set(true); this.passwordError.set(''); this.passwordDone.set(false);
    try {
      await firstValueFrom(this.auth.changeMyPassword(raw.current, raw.next));
      this.passwordDone.set(true);
      this.passwordForm.reset();
    } catch (err) {
      this.passwordError.set(this.describe(err));
    } finally {
      this.busy.set(false);
    }
  }

  async changePin(): Promise<void> {
    if (this.pinForm.invalid) { return; }
    const raw = this.pinForm.getRawValue();
    this.busy.set(true); this.pinError.set(''); this.pinDone.set(false);
    try {
      await firstValueFrom(this.auth.setPin(raw.password, raw.pin));
      this.pinDone.set(true);
      this.pinForm.reset();
      // The "PIN configured" state the drawer shows comes from the privilege
      // snapshot — re-pull it so the badge flips without a re-login.
      this.perms.refresh();
    } catch (err) {
      this.pinError.set(this.describe(err));
    } finally {
      this.busy.set(false);
    }
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return (err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`;
    }
    return 'Unexpected error.';
  }
}
