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
      <div class="card panel">
        <div class="head">
          <h1>{{ i18n.t('app.title') }}</h1>
          <select [value]="i18n.lang()" (change)="onLang($event)" aria-label="Language">
            <option value="en">EN</option>
            <option value="ar">AR</option>
            <option value="fr">FR</option>
          </select>
        </div>
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

          <button type="submit" [disabled]="busy()" style="margin-top:1rem;width:100%">
            {{ busy() ? i18n.t('common.loading') : i18n.t('login.submit') }}
          </button>

          @if (error()) { <div class="error">{{ error() }}</div> }
        </form>
      </div>
    </div>
  `,
  styles: [`
    .wrap { min-height: 100vh; display: grid; place-items: center; padding: 1rem;
            background: linear-gradient(135deg, var(--nt-navy), #0a1826); }
    .panel { width: 380px; max-width: 100%; }
    .head { display: flex; align-items: center; justify-content: space-between; }
    .head select { width: auto; }
    .hint { font-size: .8rem; margin-top: .2rem; }
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
        void this.router.navigate(['/dashboard']);
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(err.error?.title ?? 'Sign-in failed.');
      },
    });
  }
}
