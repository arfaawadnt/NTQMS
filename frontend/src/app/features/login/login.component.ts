import { Component, DestroyRef, effect, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../core/auth.service';
import { I18nService, Lang } from '../../core/i18n.service';
import { WorkspaceApiService } from '../../core/api/workspace-api.service';

/** Persisted key for the sign-in surface's light/dark preference. */
const THEME_KEY = 'qams.login.theme';

/**
 * Sign-in page, built to the supplied design: a full-width white header band
 * (logo card, brand lockup, workspace pill, language switch, theme toggle) over
 * a soft-blue field holding the value panel and the credentials card, closed by
 * a white footer band.
 *
 * The laboratory comes exclusively from its own URL (/t/{lab}) — the form
 * carries no tenant field; without a lab address the page signs in the platform
 * administrator.
 *
 * Accessibility note (this surface is an axe gate in CI): the design's teal
 * (#00B2A9) is ~2.4:1 on the white band, far under WCAG AA, so the eyebrow uses
 * a darkened tone of the same hue. Every other colour is the design's own.
 */
@Component({
    selector: 'qams-login',
    imports: [FormsModule],
    template: `
    <div class="page" [class.dark]="dark()">
      <!-- ---------------------------------------------------- header band -->
      <header class="band top">
        <div class="inner">
          <div class="brandrow">
            <span class="logocard"><img src="assets/nt-qms-logo.svg" alt="NT.QMS" /></span>
            <span class="brandlines">
              <span class="eyebrow">{{ i18n.t('app.subtitle') }}</span>
              <span class="platformtag">{{ i18n.t('login.platformTag') }}</span>
              <!-- Platform sign-in reads as a third brand line: it stays left with
                   the lockup and never shifts the right-hand control group, which
                   sits exactly where the design puts it. -->
              @if (tenantSlug()) {
                <button type="button" class="link platformswitch" (click)="switchToPlatform()">
                  {{ i18n.t('login.platformSwitch') }} &rarr;
                </button>
              }
            </span>
          </div>

          <div class="controlrow">
            @if (tenantSlug()) {
              <span class="wspill">
                <span class="avatar" aria-hidden="true">{{ workspaceInitials() }}</span>
                <span class="wsname">{{ workspaceName() }}</span>
              </span>
            } @else {
              <span class="wspill admin">
                <span class="avatar admin" aria-hidden="true">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                       stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <path d="M12 3l7 3v6c0 4.2-2.9 7.7-7 9-4.1-1.3-7-4.8-7-9V6l7-3z" />
                  </svg>
                </span>
                <span class="wsname">{{ i18n.t('login.adminPortal') }}</span>
              </span>
            }

            <div class="langswitch" role="group" [attr.aria-label]="i18n.t('common.language')">
              @for (l of langs; track l.code) {
                <button type="button" [class.active]="i18n.lang() === l.code"
                        [attr.aria-pressed]="i18n.lang() === l.code"
                        (click)="i18n.setLang(l.code)">{{ l.label }}</button>
              }
            </div>

            <button type="button" class="themetoggle" [attr.aria-label]="i18n.t('login.themeToggle')"
                    [attr.aria-pressed]="dark()" (click)="toggleTheme()">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                   stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M21 12.8A9 9 0 1 1 11.2 3 7 7 0 0 0 21 12.8z" />
              </svg>
            </button>
          </div>
        </div>
      </header>

      <!-- ------------------------------------------------------------ body -->
      <main class="body">
        <div class="cols"><div class="colgrid">
          <section class="hero">
            <h1>{{ i18n.t(tenantSlug() ? 'login.heroTitle' : 'login.adminHeroTitle') }}</h1>
            <p class="herobody">{{ i18n.t(tenantSlug() ? 'login.heroBody' : 'login.adminHeroBody') }}</p>
            <ul class="features">
              @for (f of features(); track f) {
                <li>{{ i18n.t(f) }}</li>
              }
            </ul>
          </section>

          <section class="card">
            <h2>{{ i18n.t('login.title') }}</h2>
            <p class="cardhint">{{ i18n.t(tenantSlug() ? 'login.cardHint' : 'login.adminCardHint') }}</p>
            @if (!tenantSlug()) {
              <p class="cardnote">{{ i18n.t('login.labUrlHint') }}</p>
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
                <div class="hint">{{ i18n.t('login.newPasswordHint') }}</div>
              }

              @if (mfaRequired()) {
                <label for="mfa">{{ i18n.t('login.mfa') }}</label>
                <input id="mfa" name="mfa" inputmode="numeric" [(ngModel)]="mfaCode" autocomplete="one-time-code" />
                <div class="hint">{{ i18n.t('login.mfaPrompt') }}</div>
              }

              <button type="submit" class="signin" [disabled]="busy()">
                {{ busy() ? i18n.t('common.loading') : i18n.t('login.submit') }}
              </button>

              @if (error()) { <div class="error" role="alert">{{ error() }}</div> }
            </form>

            <p class="authnote">{{ i18n.t('login.authorizedOnly') }}</p>
          </section>
        </div></div>
      </main>

      <!-- ---------------------------------------------------- footer band -->
      <footer class="band bottom">
        <div class="footinner">
          <span>&copy; {{ year }} National Technology &middot; NT.QMS</span>
          <span aria-hidden="true">&middot;</span>
          <span>ISO/IEC 17025</span>
        </div>
      </footer>
    </div>
  `,
    changeDetection: ChangeDetectionStrategy.Eager,
    styles: [`
    /* Design palette. --slate is the design's own muted tone (#3B4658); --accent
       is its teal darkened from #00B2A9 for WCAG AA on white (see class docs). */
    .page {
      --field:  #E3F2FD;
      --band:   #FFFFFF;
      --line:   #C1C1C6;
      --ink:    #1E3A5F;
      --ink2:   #1B365D;
      --slate:  #3B4658;
      --accent: #00706A;
      --pill:   #93C9EE;
      --chipbg: #FFFFFF;
      --inputbg:#FFFFFF;
      --link:   #0064A6;

      min-height: 100vh; background: var(--field);
      display: flex; flex-direction: column;
    }
    /* Dark variant for the header toggle. Derived from the app's navy tokens;
       every pair below was contrast-checked for AA. */
    .page.dark {
      --field:  #0F1B2D;
      --band:   #16273F;
      --line:   #2A3B55;
      --ink:    #E7EDF5;
      --ink2:   #D7E2F0;
      --slate:  #A8B6C8;
      --accent: #35D0C6;
      --pill:   #2F4A6B;
      --chipbg: transparent;
      --inputbg:#0F1B2D;
      --link:   #7FC0F5;
    }

    .band { background: var(--band); }
    .band.top { border-bottom: 1px solid var(--line); padding: 28px 0 29px; }
    .band.bottom { border-top: 1px solid var(--line); padding: 14px 0; margin-top: auto; }

    /* The header is ONE row: brand group left, controls group right, both centred
       on the logo card. It wraps to a second (left-aligned) row only when the
       viewport is too narrow to hold both — which is exactly what the mockup
       shows at 1280px. */
    .inner {
      width: 100%; padding: 0 56px;
      display: flex; align-items: center; justify-content: space-between;
      gap: 24px; flex-wrap: wrap;
    }
    /* The two-column block is a fixed 946px wide and centred at every viewport
       (design: 160px gutters at 1280, 327px at 1600). */
    .cols { width: 100%; max-width: 946px; margin: 0 auto; }
    .footinner { width: 100%; max-width: 1265px; margin: 0 auto; padding: 0 56px; }

    /* ------------------------------------------------------- brand lockup */
    .brandrow { display: flex; align-items: center; gap: 28px; }
    .logocard {
      flex: none; background: var(--band); border-radius: 14px; padding: 12px 22px;
      box-shadow: 0 1px 2px rgba(59, 70, 88, .06); display: inline-flex;
    }
    .page.dark .logocard { background: #FFFFFF; }
    .logocard img { height: 88px; display: block; }
    .brandlines { display: flex; flex-direction: column; gap: 5px; min-width: 0; }
    .eyebrow {
      font-size: 12px; font-weight: 700; letter-spacing: 1.92px;
      text-transform: uppercase; color: var(--accent);
    }
    .platformtag { font-size: 22px; font-weight: 800; color: var(--ink); line-height: 1.25; }

    /* ------------------------------------------------------- control row */
    .controlrow { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .wspill {
      display: inline-flex; align-items: center; gap: 10px;
      background: var(--chipbg); border: 1px solid var(--line); border-radius: 999px;
      padding: 6px 16px 6px 6px;
      /* The design's pill is wider than its content (216px). */
      min-width: 216px; box-sizing: border-box;
    }
    .avatar {
      flex: none; width: 34px; height: 34px; border-radius: 999px;
      display: grid; place-items: center;
      background: #1E3A5F; color: #FFFFFF; font-size: 12px; font-weight: 800;
    }
    .page.dark .avatar { background: #2E5A8C; }
    .wsname {
      font-size: 13px; font-weight: 700; color: var(--ink);
      overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    }

    /* The pill/switch/toggle are optically centred on the 48px workspace pill,
       matching the design's y offsets (pill 170, switch 175, toggle 176). */
    .langswitch {
      flex: none; display: inline-flex; background: #EDEFF3; border-radius: 999px;
      padding: 3px;
    }
    .page.dark .langswitch { background: #0F1B2D; }
    .langswitch button {
      width: auto; background: transparent; color: var(--slate);
      font-size: 12px; font-weight: 600; padding: 8px 12px; border-radius: 999px; line-height: 1.25;
    }
    .langswitch button.active {
      background: var(--band); color: var(--ink); font-weight: 700;
      box-shadow: 0 1px 2px rgba(59, 70, 88, .15);
    }

    .themetoggle {
      flex: none; width: 36px; height: 36px; padding: 0; border-radius: 999px;
      background: var(--chipbg); border: 1px solid var(--line); color: var(--slate);
      display: grid; place-items: center;
    }
    .themetoggle svg { width: 15px; height: 15px; }

    /* --------------------------------------------------------------- body */
    .body { flex: 1; display: flex; align-items: center; padding: 48px 0; }
    .colgrid { display: grid; grid-template-columns: minmax(0, 1fr) 502px; gap: 66px; align-items: center; }

    .hero h1 {
      margin: 0; font-size: 32px; line-height: 1.25; font-weight: 800;
      color: var(--ink); letter-spacing: -.01em; max-width: 20ch;
    }
    .herobody {
      margin: 20px 0 0; font-size: 14px; font-weight: 500; line-height: 1.5;
      color: var(--slate); max-width: 40ch;
    }
    .features { list-style: none; margin: 22px 0 0; padding: 0; display: flex; flex-wrap: wrap; gap: 7px; }
    .features li {
      background: var(--chipbg); border: 1px solid var(--pill); border-radius: 999px;
      padding: 5px 12px; font-size: 12px; font-weight: 600; color: var(--ink2); line-height: 1.25;
    }

    /* --------------------------------------------------------------- card */
    .card {
      background: var(--band); border: 1px solid var(--line); border-radius: 10px;
      box-shadow: 0 4px 12px rgba(59, 70, 88, .12);
      padding: 36px 40px;
    }
    /* Explicit line-heights: the inherited 1.4+ rhythm inflates each row and the
       card drifts ~13px taller than the design. */
    h2 { margin: 0; font-size: 21px; font-weight: 800; line-height: 1.28; color: var(--ink); }
    .cardhint { margin: 4px 0 0; font-size: 13px; font-weight: 500; line-height: 1.23; color: var(--slate); }
    .cardnote { margin: 8px 0 0; font-size: 11.5px; line-height: 1.45; color: var(--slate); }
    /* Colour comes from the theme's --link so the dark variant cannot be beaten
       by the global button.link rule. */
    .platformswitch {
      align-self: flex-start; width: auto; background: transparent; color: var(--link);
      font-size: 12px; font-weight: 700; padding: 0; margin-top: 8px; text-align: start;
    }
    .platformswitch:hover { text-decoration: underline; }

    /* The design uses a uniform 18px gap above every field label and 6px between
       label and control, so the spacing lives entirely on the label. */
    form { margin-top: 0; }
    label { margin: 18px 0 6px; font-size: 14px; font-weight: 600; line-height: 1.2; color: var(--ink); }
    input {
      border-radius: 10px; border: 1px solid var(--line); background: var(--inputbg);
      color: var(--ink); font-size: 14px; padding: 12px 14px;
    }
    .signin {
      margin-top: 22px; width: 100%; min-height: 46px; border-radius: 15px;
      padding: 14px 16px; font-size: 15px; font-weight: 700;
    }
    .hint { margin: 2px 0 0; font-size: 11.5px; color: var(--slate); }
    .authnote {
      margin: 18px 0 0; font-size: 11px; font-weight: 500; line-height: 1.2;
      color: var(--slate); text-align: center;
    }

    /* ------------------------------------------------------- footer band */
    .footinner {
      display: flex; flex-wrap: wrap; justify-content: center; gap: 8px;
      font-size: 12px; font-weight: 700; color: var(--slate);
    }

    @media (max-width: 1180px) {
      .colgrid { gap: 40px; }
    }
    @media (max-width: 940px) {
      .inner, .footinner { padding: 0 28px; }
      .cols { max-width: 520px; padding: 0 28px; }
      .colgrid { grid-template-columns: minmax(0, 1fr); }
      .logocard img { height: 64px; }
      .platformtag { font-size: 18px; }
      .hero h1 { font-size: 26px; }
      .card { padding: 26px 22px 22px; }
    }
  `]
})
export class LoginComponent {
  readonly i18n = inject(I18nService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly workspaces = inject(WorkspaceApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly langs: { code: Lang; label: string }[] = [
    { code: 'en', label: 'EN' },
    { code: 'ar', label: 'العربية' },
    { code: 'fr', label: 'FR' },
  ];

  private static readonly LabFeatures = [
    'login.featureDocs', 'login.featureCapa', 'login.featureAudits', 'login.featureRisk',
  ];

  private static readonly AdminFeatures = [
    'login.adminFeatureTenants', 'login.adminFeatureSecurity', 'login.adminFeatureAudit',
  ];

  /** Capability pills on the value panel, as i18n keys — per sign-in variant. */
  readonly features = computed(() =>
    this.tenantSlug() ? LoginComponent.LabFeatures : LoginComponent.AdminFeatures);

  readonly year = new Date().getFullYear();

  /** Sign-in surface theme; remembered so the choice survives a reload. */
  readonly dark = signal(localStorage.getItem(THEME_KEY) === 'dark');

  /** The lab pinned by its /t/{slug} address — null means platform sign-in. */
  readonly tenantSlug = computed(() => this.auth.tenantSlug());

  /**
   * The laboratory's real name, resolved from its slug before sign-in. Null while
   * the lookup is in flight or when the slug matches no active laboratory — the
   * slug-derived label below then stands in, so the pill is never empty.
   */
  private readonly resolvedName = signal<string | null>(null);

  /** What the workspace pill shows: the real laboratory name when known. */
  readonly workspaceName = computed(() => this.resolvedName() ?? this.slugLabel());

  readonly workspaceInitials = computed(() => {
    const words = this.workspaceName().split(/\s+/).filter((w) => w.length > 0);
    if (words.length === 0) { return '?'; }
    const initials = words.slice(0, 2).map((w) => w.charAt(0).toUpperCase()).join('');
    return initials.length > 1 ? initials : words[0].slice(0, 2).toUpperCase();
  });

  /** Fallback label derived from the slug: "arfa-lab" -> "Arfa Lab". */
  private readonly slugLabel = computed(() => {
    const slug = this.tenantSlug();
    if (!slug) { return ''; }
    return slug
      .split(/[-_\s]+/)
      .filter((part) => part.length > 0)
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join(' ');
  });

  email = '';
  password = '';
  mfaCode = '';
  newPassword = '';

  readonly busy = signal(false);
  readonly error = signal('');
  readonly mfaRequired = signal(false);
  readonly passwordExpired = signal(false);

  constructor() {
    // Resolve the laboratory name for whichever lab the address pins, and again
    // if the visitor switches to the platform portal and back.
    effect(() => {
      const slug = this.tenantSlug();
      this.resolvedName.set(null);
      if (!slug) { return; }
      this.workspaces.get(slug).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (workspace) => this.resolvedName.set(workspace.name),
        // 404 = unknown/inactive lab: keep the slug-derived label, say nothing.
        error: () => this.resolvedName.set(null),
      });
    });
  }

  toggleTheme(): void {
    const next = !this.dark();
    this.dark.set(next);
    localStorage.setItem(THEME_KEY, next ? 'dark' : 'light');
  }

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
