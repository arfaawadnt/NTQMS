import { Component, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../core/auth.service';
import { I18nService, Lang } from '../../core/i18n.service';

/**
 * Sign-in page. Two panels on a soft-blue field: a brand/value panel (logo,
 * platform line, headline, capability list) beside the credentials card.
 * The laboratory comes exclusively from its own URL (/t/{lab}) — the form
 * carries no tenant field; without a lab address the page signs in the
 * platform administrator.
 *
 * Accessibility notes (the sign-in surface is an axe gate in CI): two design
 * colours are deliberately darkened here because the supplied values fail WCAG
 * AA on this background — see the comments on `.eyebrow` and `.muted`.
 */
@Component({
    selector: 'qams-login',
    imports: [FormsModule],
    template: `
    <div class="page">
      <div class="shell">
        <!-- brand / value panel -->
        <section class="hero">
          <img class="logo" src="assets/nt-qms-logo.svg" alt="NT.QMS" />
          <p class="eyebrow">{{ i18n.t('app.subtitle') }}</p>
          <p class="platformtag">{{ i18n.t('login.platformTag') }}</p>

          <h1>{{ i18n.t('login.heroTitle') }}</h1>
          <p class="herobody">{{ i18n.t('login.heroBody') }}</p>

          <ul class="features">
            @for (f of features; track f) {
              <li>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"
                     stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="M20 6 9 17l-5-5" />
                </svg>
                {{ i18n.t(f) }}
              </li>
            }
          </ul>
        </section>

        <!-- credentials card -->
        <section class="card">
          <div class="cardtop">
            @if (tenantSlug(); as slug) {
              <div class="workspace">
                <span class="avatar" aria-hidden="true">{{ tenantInitials() }}</span>
                <span class="wsmeta">
                  <span class="wsname">{{ tenantName() }}</span>
                  <span class="muted">{{ i18n.t('login.workspaceHint') }}</span>
                </span>
              </div>
            } @else {
              <div class="workspace">
                <span class="avatar platform" aria-hidden="true">NT</span>
                <span class="wsmeta">
                  <span class="wsname">{{ i18n.t('login.platformMode') }}</span>
                  <span class="muted">{{ i18n.t('login.labUrlHint') }}</span>
                </span>
              </div>
            }

            <div class="langswitch" role="group" [attr.aria-label]="i18n.t('common.language')">
              @for (l of langs; track l.code) {
                <button type="button" [class.active]="i18n.lang() === l.code"
                        [attr.aria-pressed]="i18n.lang() === l.code"
                        (click)="i18n.setLang(l.code)">{{ l.label }}</button>
              }
            </div>
          </div>

          <h2>{{ i18n.t('login.title') }}</h2>
          <p class="cardhint">{{ i18n.t('login.cardHint') }}</p>

          @if (tenantSlug()) {
            <button type="button" class="link platformswitch" (click)="switchToPlatform()">
              {{ i18n.t('login.platformSwitch') }} &rarr;
            </button>
          }

          <form (ngSubmit)="submit()">
            <label for="email">{{ i18n.t('login.email') }}</label>
            <input id="email" name="email" type="email" [(ngModel)]="email"
                   [placeholder]="i18n.t('login.emailPlaceholder')"
                   autocomplete="username" required />

            <label for="password">{{ i18n.t('login.password') }}</label>
            <input id="password" name="password" type="password" [(ngModel)]="password"
                   placeholder="••••••••" autocomplete="current-password" required />

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

            @if (error()) { <div class="error" role="alert">{{ error() }}</div> }
          </form>

          <p class="authnote">{{ i18n.t('login.authorizedOnly') }}</p>
        </section>
      </div>

      <footer class="pagefoot">
        <span>&copy; {{ year }} National Technology &middot; NT.QMS</span>
        <span aria-hidden="true">&middot;</span>
        <span>ISO/IEC 17025</span>
      </footer>
    </div>
  `,
    changeDetection: ChangeDetectionStrategy.Eager,
    styles: [`
    /* The design's field colour; --nt-brand-soft (#E0F2FE) is its sibling. */
    .page {
      min-height: 100vh; background: #E3F2FD;
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      padding: 40px 24px 20px; gap: 22px;
    }
    .shell {
      width: 100%; max-width: 1180px;
      display: grid; grid-template-columns: 1fr 420px; gap: 64px; align-items: center;
    }

    /* ---------------------------------------------------------- hero panel */
    .hero { min-width: 0; }
    .logo { height: 62px; display: block; }
    /* --nt-teal (#00B2A9) is 2.2:1 on this field — well under AA for 12px text.
       #00706A keeps the accent hue and clears 4.5:1. */
    .eyebrow {
      margin: 22px 0 0; font-size: 12px; font-weight: 700; letter-spacing: .16em;
      text-transform: uppercase; color: #00706A;
    }
    .platformtag { margin: 4px 0 0; font-size: 13px; font-weight: 600; color: var(--nt-navy-deep); }
    .hero h1 {
      margin: 20px 0 0; font-size: 32px; line-height: 1.25; font-weight: 800;
      color: var(--nt-navy); letter-spacing: -.01em; max-width: 22ch;
    }
    .herobody { margin: 14px 0 0; font-size: 14.5px; line-height: 1.65; color: #4A5768; max-width: 54ch; }
    .features {
      list-style: none; margin: 26px 0 0; padding: 0;
      display: flex; flex-wrap: wrap; gap: 10px 26px;
    }
    .features li {
      display: inline-flex; align-items: center; gap: 8px;
      font-size: 12.5px; font-weight: 700; color: var(--nt-navy-deep);
    }
    .features svg { width: 15px; height: 15px; color: #00706A; flex: none; }

    /* --------------------------------------------------------------- card */
    .card {
      background: var(--nt-surface); border: 1px solid var(--nt-border);
      border-radius: 10px; box-shadow: 0 4px 12px rgba(59, 70, 88, .12);
      padding: 32px 36px 26px;
    }
    .cardtop {
      display: flex; align-items: flex-start; justify-content: space-between;
      gap: 12px; padding-bottom: 18px; border-bottom: 1px solid var(--nt-border);
    }
    .workspace { display: flex; align-items: center; gap: 10px; min-width: 0; }
    .avatar {
      flex: none; width: 34px; height: 34px; border-radius: 999px;
      display: grid; place-items: center;
      background: var(--nt-brand-soft); color: #00639e;
      font-size: 12px; font-weight: 800; letter-spacing: .02em;
    }
    .avatar.platform { background: #E4EEF7; color: var(--nt-navy-deep); }
    .wsmeta { display: flex; flex-direction: column; min-width: 0; }
    .wsname {
      font-size: 13.5px; font-weight: 700; color: var(--nt-navy);
      overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    }
    /* --nt-muted (#797979) is ~4.0:1 on white at 11px; #5A6472 clears AA. */
    .muted { color: #5A6472; font-size: 11.5px; }

    .langswitch { flex: none; display: inline-flex; background: var(--nt-filter-grey); border-radius: 999px; padding: 3px; }
    .langswitch button {
      width: auto; background: transparent; color: #4A5768;
      font-size: 11.5px; font-weight: 700; padding: 5px 11px; border-radius: 999px;
    }
    .langswitch button.active { background: var(--nt-surface); color: var(--nt-blue); box-shadow: var(--nt-shadow-xs); }

    h2 { margin: 20px 0 0; font-size: 21px; font-weight: 800; color: var(--nt-navy); }
    .cardhint { margin: 4px 0 0; font-size: 13px; color: #4A5768; }
    .platformswitch {
      width: auto; background: transparent; color: var(--nt-blue);
      font-size: 12px; font-weight: 700; padding: 10px 0 0;
    }
    .platformswitch:hover { text-decoration: underline; }

    form { margin-top: 18px; }
    label { margin-top: 14px; font-size: 14px; font-weight: 600; color: var(--nt-navy); }
    input { border-radius: var(--nt-radius-input); font-size: 14px; padding: 12px 14px; }
    .signin {
      margin-top: 22px; width: 100%; border-radius: var(--nt-radius-login);
      padding: 14px 16px; font-size: 15px; font-weight: 700;
    }
    .hint { margin: 2px 0 0; }
    .authnote {
      margin: 20px 0 0; padding-top: 16px; border-top: 1px solid var(--nt-border);
      font-size: 11.5px; line-height: 1.5; color: #5A6472; text-align: center;
    }

    /* --------------------------------------------------------- page footer */
    .pagefoot {
      display: flex; flex-wrap: wrap; justify-content: center; gap: 8px;
      font-size: 11.5px; font-weight: 600; color: #4A5768;
    }

    @media (max-width: 940px) {
      .shell { grid-template-columns: 1fr; gap: 30px; max-width: 480px; }
      .hero { text-align: center; }
      .hero h1 { font-size: 26px; margin-inline: auto; }
      .herobody { margin-inline: auto; }
      .logo { margin-inline: auto; }
      .features { justify-content: center; }
      .card { padding: 26px 22px 22px; }
    }
  `]
})
export class LoginComponent {
  readonly i18n = inject(I18nService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly langs: { code: Lang; label: string }[] = [
    { code: 'en', label: 'EN' },
    { code: 'ar', label: 'العربية' },
    { code: 'fr', label: 'FR' },
  ];

  /** Capability list shown on the hero panel, as i18n keys. */
  readonly features = [
    'login.featureDocs',
    'login.featureCapa',
    'login.featureAudits',
    'login.featureRisk',
  ] as const;

  readonly year = new Date().getFullYear();

  /** The lab pinned by its /t/{slug} address — null means platform sign-in. */
  readonly tenantSlug = computed(() => this.auth.tenantSlug());

  /** Slug rendered as a lab name: "arfa-lab" -> "Arfa Lab". */
  readonly tenantName = computed(() => {
    const slug = this.tenantSlug();
    if (!slug) { return ''; }
    return slug
      .split(/[-_\s]+/)
      .filter((part) => part.length > 0)
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join(' ');
  });

  /** Up to two initials for the workspace avatar: "Arfa Lab" -> "AL". */
  readonly tenantInitials = computed(() => {
    const words = this.tenantName().split(' ').filter((w) => w.length > 0);
    if (words.length === 0) { return '?'; }
    const initials = words.slice(0, 2).map((w) => w.charAt(0).toUpperCase()).join('');
    return initials.length > 1 ? initials : words[0].slice(0, 2).toUpperCase();
  });

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
