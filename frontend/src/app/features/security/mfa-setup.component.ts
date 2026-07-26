import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../core/auth.service';
import { I18nService } from '../../core/i18n.service';

/**
 * Multi-factor authentication enrollment (TOTP / RFC 6238). Reached when a
 * privileged user of an MFA-enforcing tenant signs in, or voluntarily from the
 * security settings. Stands alone (outside the shell) so it works under an
 * enrollment-scoped session that is barred from every other endpoint.
 */
@Component({
  selector: 'qams-mfa-setup',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule],
  template: `
    <div class="wrap">
      <div class="card">
        <div class="head">
          <img src="assets/nt-qms-logo.svg" alt="NT.QMS" />
          <h2>{{ i18n.t('mfa.setupTitle') }}</h2>
        </div>

        @if (done()) {
          <div class="ok">{{ i18n.t('mfa.enabled') }}</div>
          <button class="primary" (click)="goToLogin()">{{ i18n.t('mfa.backToLogin') }}</button>
        } @else {
          <p class="lead">{{ i18n.t('mfa.setupIntro') }}</p>

          @if (secret(); as s) {
            <ol class="steps">
              <li>{{ i18n.t('mfa.step1') }}</li>
              <li>
                {{ i18n.t('mfa.step2') }}
                <code class="secret">{{ s }}</code>
                <button type="button" class="link" (click)="copy(s)">{{ i18n.t('mfa.copy') }}</button>
              </li>
              <li>{{ i18n.t('mfa.step3') }}</li>
            </ol>
            <div class="uri muted">{{ otpUri() }}</div>

            <label>{{ i18n.t('mfa.codeLabel') }}</label>
            <input name="code" inputmode="numeric" autocomplete="one-time-code"
                   [(ngModel)]="code" [placeholder]="i18n.t('mfa.codePlaceholder')" />

            <button class="primary" [disabled]="busy() || code.trim().length < 6" (click)="confirm()">
              {{ busy() ? i18n.t('common.loading') : i18n.t('mfa.confirm') }}
            </button>
          } @else {
            <p class="muted">{{ enrollError() || i18n.t('common.loading') }}</p>
          }

          @if (error()) { <div class="error">{{ error() }}</div> }
          <button type="button" class="link cancel" (click)="goToLogin()">{{ i18n.t('mfa.cancel') }}</button>
        }
      </div>
    </div>
  `,
  styles: [`
    .wrap { min-height: 100vh; display: grid; place-items: center; padding: 1.5rem;
            background: linear-gradient(135deg, #10263e 0%, #16314f 55%, #0d3a4a 100%); }
    .card { width: 460px; max-width: 100%; background: var(--nt-surface);
            border-radius: var(--nt-radius-login); box-shadow: var(--nt-shadow-pop); padding: 30px 30px 24px; }
    .head { display: flex; align-items: center; gap: 14px; margin-bottom: 12px; }
    .head img { height: 40px; }
    h2 { font-size: 17px; font-weight: 800; color: var(--nt-navy-deep); margin: 0; }
    .lead { font-size: 13px; color: var(--nt-ink-2, var(--nt-slate)); margin: 0 0 14px; line-height: 1.5; }
    .steps { margin: 0 0 12px; padding-left: 20px; }
    .steps li { font-size: 13px; margin-bottom: 8px; line-height: 1.5; }
    .secret { display: inline-block; margin: 6px 8px 0 0; font-size: 14px; letter-spacing: 2px;
              background: var(--nt-filter-grey); border: 1px solid var(--nt-border); border-radius: 6px; padding: 5px 10px; }
    .uri { font-family: monospace; font-size: 10.5px; word-break: break-all; margin-bottom: 14px; opacity: .7; }
    label { display: block; margin-top: 8px; font-size: 12px; font-weight: 600; }
    input { width: 100%; }
    .primary { margin-top: 16px; width: 100%; border-radius: 10px; padding: 12px; font-size: 14px; font-weight: 700; }
    .link { background: transparent; color: var(--nt-blue); font-weight: 700; font-size: 12px; width: auto; padding: 2px 4px; }
    .link:hover { text-decoration: underline; }
    .cancel { display: block; margin: 14px auto 0; }
    .ok { background: var(--nt-ok-bg, #e6f4ec); color: var(--nt-green, #1f8a54); border-radius: 8px; padding: 12px 14px;
          font-size: 13px; font-weight: 600; margin-bottom: 16px; }
  `],
})
export class MfaSetupComponent implements OnInit {
  readonly i18n = inject(I18nService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly secret = signal<string | null>(null);
  readonly otpUri = signal('');
  code = '';
  readonly busy = signal(false);
  readonly done = signal(false);
  readonly error = signal('');
  readonly enrollError = signal('');

  ngOnInit(): void {
    this.auth.enrollMfa().subscribe({
      next: (res) => { this.secret.set(res.secret); this.otpUri.set(res.otpAuthUri); },
      error: (err: HttpErrorResponse) =>
        this.enrollError.set(err.error?.title ?? 'Could not start MFA enrollment.'),
    });
  }

  confirm(): void {
    this.error.set('');
    this.busy.set(true);
    this.auth.confirmMfa(this.code.trim()).subscribe({
      next: () => { this.busy.set(false); this.done.set(true); },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(err.error?.title ?? 'The code was not accepted. Try the current code.');
      },
    });
  }

  copy(value: string): void { void navigator.clipboard?.writeText(value); }

  goToLogin(): void {
    // Sign out so the next login mints a full session (or challenges for the code).
    this.auth.logout();
    void this.router.navigate(['/login']);
  }

  // Template two-way binding needs a mutable field; expose via getter/setter-free public field.
}
