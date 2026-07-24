import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../core/auth.service';
import { I18nService, Lang } from '../../core/i18n.service';

@Component({
  selector: 'qams-login',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="wrap">
      <div class="panel">
        <div class="brandstrip"></div>
        <div class="inner">
        <div class="head">
          <img class="logo" src="assets/nt-qams-logo.png" alt="NT.QAMS" />
          <select [value]="i18n.lang()" (change)="onLang($event)" aria-label="Language">
            <option value="en">EN</option>
            <option value="ar">AR</option>
            <option value="fr">FR</option>
          </select>
        </div>
        <div class="subtitle">{{ i18n.t('app.subtitle') }}</div>
        <h2>{{ i18n.t('login.title') }}</h2>

        <form (ngSubmit)="submit()">
          <label>{{ i18n.t('login.tenant') }}</label>
          <input name="tenant" [(ngModel)]="tenant" autocomplete="organization" />
          <div class="muted hint">{{ i18n.t('login.tenantHint') }}</div>

          <label>{{ i18n.t('login.email') }}</label>
          <input name="email" type="email" [(ngModel)]="email" autocomplete="username" required />

          <label>{{ i18n.t('login.password') }}</label>
          <input name="password" type="password" [(ngModel)]="password" autocomplete="current-password" required />

          @if (mfaRequired()) {
            <label>{{ i18n.t('login.mfa') }}</label>
            <input name="mfa" inputmode="numeric" [(ngModel)]="mfaCode" autocomplete="one-time-code" />
            <div class="muted hint">{{ i18n.t('login.mfaPrompt') }}</div>
          }

          <button type="submit" class="signin" [disabled]="busy()">
            {{ busy() ? i18n.t('common.loading') : i18n.t('login.submit') }}
          </button>

          @if (error()) { <div class="error">{{ error() }}</div> }
        </form>
        </div>
        <div class="foot">National Technology · NT.QAMS</div>
      </div>
    </div>
  `,
  styles: [`
    .wrap { min-height: 100vh; display: grid; place-items: center; padding: 1rem;
            background: linear-gradient(135deg, #1E3A5F 0%, #16314f 60%, #0f2338 100%); }
    .panel {
      width: 400px; max-width: 100%; background: var(--nt-surface);
      border-radius: 8px; box-shadow: var(--nt-shadow-pop); overflow: hidden;
    }
    .brandstrip { height: 6px; background: var(--nt-header-grad); }
    .inner { padding: 24px 28px 20px; }
    .head { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
    .head select { width: auto; }
    .logo { height: 42px; }
    .subtitle { font-size: 12px; color: var(--nt-grey-d); margin: 8px 0 16px; }
    h2 { font-size: 15px; font-weight: 700; color: var(--nt-slate); margin: 0 0 4px; }
    .hint { margin-top: 3px; }
    .signin {
      margin-top: 20px; width: 100%; border-radius: var(--nt-radius-login);
      padding: 11px 16px; font-size: 14px; font-weight: 700;
    }
    .foot {
      text-align: center; font-size: 12px; font-weight: 700; color: var(--nt-navy-deep);
      border-top: 1px solid var(--nt-filter-grey); padding: 12px;
    }
  `],
})
export class LoginComponent {
  readonly i18n = inject(I18nService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  tenant = '';
  email = '';
  password = '';
  mfaCode = '';

  readonly busy = signal(false);
  readonly error = signal('');
  readonly mfaRequired = signal(false);

  onLang(event: Event): void {
    this.i18n.setLang((event.target as HTMLSelectElement).value as Lang);
  }

  submit(): void {
    this.error.set('');
    this.busy.set(true);
    const tenant = this.tenant.trim() || null;
    const mfa = this.mfaRequired() ? (this.mfaCode.trim() || null) : null;

    this.auth.login(tenant, this.email.trim(), this.password, mfa).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.mfaRequired) {
          this.mfaRequired.set(true);
          return;
        }
        // Platform administrators land on the control plane; lab users on the dashboard.
        void this.router.navigate([res.role === 'PlatformAdmin' ? '/platform/tenants' : '/dashboard']);
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(err.error?.title ?? 'Sign-in failed.');
      },
    });
  }
}
