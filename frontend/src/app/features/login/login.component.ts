import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../core/auth.service';
import { I18nService, Lang } from '../../core/i18n.service';

/**
 * Split-panel sign-in per the QAMS Design System: a signature-gradient brand
 * panel (logo, tagline, compliance chips) beside a 15px-radius form card.
 * The laboratory comes exclusively from its own URL (/t/{lab}) — the form
 * carries no tenant field; without a lab address the page signs in the
 * platform administrator.
 */
@Component({
  selector: 'qams-login',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="wrap">
      <div class="panel">
        <!-- brand side -->
        <aside class="brand">
          <div class="logocard"><img src="assets/nt-qms-logo.svg" alt="NT.QMS" /></div>
          <h1>{{ i18n.t('app.subtitle') }}</h1>
          <p class="tagline">{{ i18n.t('login.tagline') }}</p>
          <div class="chips">
            <span class="chip">ISO/IEC 17025</span>
            <span class="chip">ISO 15189</span>
            <span class="chip">ISO 9001</span>
            <span class="chip">21 CFR Part 11</span>
            <span class="chip">GMP</span>
          </div>
          <div class="brandfoot">National Technology · NT.QMS</div>
        </aside>

        <!-- form side -->
        <section class="form">
          <div class="formhead">
            <h2>{{ i18n.t('login.title') }}</h2>
            <div class="langswitch" role="group" aria-label="Language">
              @for (l of langs; track l.code) {
                <button type="button" [class.active]="i18n.lang() === l.code" (click)="i18n.setLang(l.code)">{{ l.label }}</button>
              }
            </div>
          </div>

          @if (tenantSlug(); as slug) {
            <div class="tenantline">
              <span class="tenantchip" [title]="i18n.t('login.tenantChipHint')">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="M3 21h18 M5 21V7l8-4v18 M19 21V11l-6-4 M9 9v.01 M9 12v.01 M9 15v.01 M9 18v.01" />
                </svg>
                {{ slug }}
              </span>
              <span class="muted">{{ i18n.t('login.notYourLab') }}</span>
            </div>
            <button type="button" class="link platformswitch" (click)="switchToPlatform()">
              {{ i18n.t('login.platformSwitch') }} →
            </button>
          } @else {
            <div class="platformline">{{ i18n.t('login.platformMode') }}</div>
            <div class="muted hint">{{ i18n.t('login.labUrlHint') }}</div>
          }

          <form (ngSubmit)="submit()">
            <label for="email">{{ i18n.t('login.email') }}</label>
            <input id="email" name="email" type="email" [(ngModel)]="email" autocomplete="username" required />

            <label for="password">{{ i18n.t('login.password') }}</label>
            <input id="password" name="password" type="password" [(ngModel)]="password" autocomplete="current-password" required />

            @if (passwordExpired()) {
              <div class="error">{{ i18n.t('login.expired') }}</div>
              <label for="newPassword">{{ i18n.t('login.newPassword') }}</label>
              <input id="newPassword" name="newPassword" type="password" [(ngModel)]="newPassword" autocomplete="new-password" />
              <div class="muted hint">{{ i18n.t('login.newPasswordHint') }}</div>
            }

            @if (mfaRequired()) {
              <label for="mfa">{{ i18n.t('login.mfa') }}</label>
              <input id="mfa" name="mfa" inputmode="numeric" [(ngModel)]="mfaCode" autocomplete="one-time-code" />
              <div class="muted hint">{{ i18n.t('login.mfaPrompt') }}</div>
            }

            <button type="submit" class="signin" [disabled]="busy()">
              {{ busy() ? i18n.t('common.loading') : i18n.t('login.submit') }}
            </button>

            @if (error()) { <div class="error">{{ error() }}</div> }
          </form>
        </section>
      </div>
    </div>
  `,
  styles: [`
    .wrap { min-height: 100vh; display: grid; place-items: center; padding: 1.5rem;
            background: linear-gradient(135deg, #10263e 0%, #16314f 55%, #0d3a4a 100%); }
    .panel {
      width: 880px; max-width: 100%; display: grid; grid-template-columns: 1.05fr 1fr;
      background: var(--nt-surface); border-radius: var(--nt-radius-login);
      box-shadow: var(--nt-shadow-pop); overflow: hidden;
    }

    /* brand side */
    .brand {
      background: var(--nt-header-grad); color: #fff; padding: 44px 36px;
      display: flex; flex-direction: column; align-items: flex-start;
    }
    .logocard { background: #fff; border-radius: 10px; padding: 12px 18px; }
    .logocard img { height: 54px; display: block; }
    /* Explicit white: the global heading palette must not bleed into the gradient panel. */
    .brand h1 { font-size: 20px; font-weight: 800; margin: 26px 0 0; letter-spacing: .01em; color: #fff; text-shadow: 0 1px 2px rgba(0,0,0,.18); }
    .tagline { font-size: 13px; color: #fff; opacity: .95; margin: 10px 0 0; line-height: 1.6; }
    .chips { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 22px; }
    .chip {
      font-size: 11px; font-weight: 700; letter-spacing: .03em;
      border: 1px solid rgba(255,255,255,.45); border-radius: 999px; padding: 4px 12px;
      background: rgba(255,255,255,.08);
    }
    .brandfoot { margin-top: auto; padding-top: 32px; font-size: 11.5px; opacity: .75; font-weight: 600; }

    /* form side */
    .form { padding: 36px 34px 30px; }
    .formhead { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 14px; }
    h2 { font-size: 17px; font-weight: 800; color: var(--nt-navy-deep); margin: 0; }
    .langswitch {
      display: inline-flex; background: var(--nt-filter-grey); border-radius: 999px; padding: 3px;
    }
    .langswitch button {
      width: auto; background: transparent; color: var(--nt-slate);
      font-size: 11.5px; font-weight: 700; padding: 5px 12px; border-radius: 999px;
    }
    .langswitch button.active { background: #fff; color: var(--nt-blue); box-shadow: var(--nt-shadow-xs); }

    .tenantline { display: flex; align-items: center; gap: 10px; margin-bottom: 16px; flex-wrap: wrap; }
    .tenantchip {
      display: inline-flex; align-items: center; gap: 7px;
      /* Darker blue than --nt-blue: the token fails WCAG AA (4.14:1) on the
         soft-blue chip; #00639e clears 4.5:1 while keeping the brand hue. */
      background: var(--nt-brand-soft); color: #00639e;
      font-size: 12.5px; font-weight: 700; border-radius: 999px; padding: 5px 14px;
    }
    .tenantchip svg { width: 14px; height: 14px; }
    .tenantline .muted { font-size: 11.5px; }
    /* Slightly darker than --nt-grey-m (#797979 = 4.35:1) so muted hints clear WCAG AA on white. */
    .muted { color: #6e6e6e; }
    /* Darker than --nt-teal (2.64:1 on white); #007c74 clears 4.5:1 for this bold label. */
    .platformline { font-size: 13px; font-weight: 700; color: #007c74; margin-bottom: 4px; }
    .hint { margin: 2px 0 14px; }
    .platformswitch { width: auto; background: transparent; color: var(--nt-blue); font-size: 12px; font-weight: 700; padding: 0 0 12px; }
    .platformswitch:hover { text-decoration: underline; }

    label { margin-top: 12px; }
    .signin {
      margin-top: 22px; width: 100%; border-radius: 10px;
      padding: 12px 16px; font-size: 14px; font-weight: 700;
    }

    @media (max-width: 760px) {
      .panel { grid-template-columns: 1fr; }
      .brand { padding: 28px 28px 24px; }
      .brandfoot { padding-top: 20px; }
    }
  `],
})
export class LoginComponent {
  readonly i18n = inject(I18nService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly langs: { code: Lang; label: string }[] = [
    { code: 'en', label: 'EN' },
    { code: 'ar', label: 'ع' },
    { code: 'fr', label: 'FR' },
  ];

  /** The lab pinned by its /t/{slug} address — null means platform sign-in. */
  readonly tenantSlug = computed(() => this.auth.tenantSlug());

  email = '';
  password = '';
  mfaCode = '';
  newPassword = '';

  readonly busy = signal(false);
  readonly error = signal('');
  readonly mfaRequired = signal(false);
  readonly passwordExpired = signal(false);

  /** Clear the pinned lab so the form signs in the platform administrator. */
  switchToPlatform(): void {
    this.auth.setTenantSlug(null);
    this.error.set('');
    this.mfaRequired.set(false);
  }

  submit(): void {
    this.error.set('');
    this.busy.set(true);
    const tenant = this.tenantSlug();

    if (this.passwordExpired()) {
      this.auth.changePassword(tenant, this.email.trim(), this.password, this.newPassword).subscribe({
        next: () => {
          this.busy.set(false);
          this.passwordExpired.set(false);
          this.password = this.newPassword;
          this.newPassword = '';
          this.submit(); // Sign in with the freshly rotated password.
        },
        error: (err: HttpErrorResponse) => {
          this.busy.set(false);
          this.error.set(err.error?.title ?? 'Password change failed.');
        },
      });
      return;
    }

    const mfa = this.mfaRequired() ? (this.mfaCode.trim() || null) : null;

    this.auth.login(tenant, this.email.trim(), this.password, mfa).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.mfaRequired) {
          this.mfaRequired.set(true);
          return;
        }
        // Privileged user of an MFA-enforcing tenant: must enrol before full access.
        if (res.mfaEnrollmentRequired) {
          void this.router.navigate(['/security/mfa-setup']);
          return;
        }
        // Platform administrators land on the control plane; lab users on the dashboard.
        void this.router.navigate([res.role === 'PlatformAdmin' ? '/platform/tenants' : '/dashboard']);
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        if (err.error?.code === 'AUTH-101') {
          this.passwordExpired.set(true);
          return;
        }
        this.error.set(err.error?.title ?? 'Sign-in failed.');
      },
    });
  }
}
