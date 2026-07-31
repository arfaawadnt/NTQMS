import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../core/auth.service';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';

/**
 * Security settings: a user sets up their own MFA here, and a TenantAdmin can
 * turn on enforced MFA for the tenant's privileged users (F-04, optional per
 * tenant). The enrollment itself happens on the standalone /security/mfa-setup
 * page.
 */
@Component({
    selector: 'qams-security-settings',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [PageHeaderComponent],
    template: `
    <qams-page-header [title]="i18n.t('sec.title')" [subtitle]="i18n.t('sec.subtitle')" />

    <section class="card">
      <h3>{{ i18n.t('sec.myMfa') }}</h3>
      <p class="muted">{{ i18n.t('sec.myMfaHint') }}</p>
      <button (click)="setUpMfa()">{{ i18n.t('sec.setUpMfa') }}</button>
    </section>

    @if (perms.can('tenant-settings.manage')) {
      <section class="card">
        <h3>{{ i18n.t('sec.tenantPolicy') }}</h3>
        <p class="muted">{{ i18n.t('sec.tenantPolicyHint') }}</p>
        @if (loaded()) {
          <label class="toggle">
            <input type="checkbox" [checked]="required()" (change)="toggle($event)" [disabled]="busy()" />
            <span>{{ i18n.t('sec.requireMfa') }}</span>
          </label>
          @if (saved()) { <span class="ok">✓ {{ i18n.t('sec.saved') }}</span> }
        } @else {
          <p class="muted">{{ error() || i18n.t('common.loading') }}</p>
        }
      </section>
    }
  `,
    styles: [`
    section { margin-bottom: 16px; }
    h3 { margin: 0 0 6px; font-size: 15px; }
    .muted { font-size: 13px; margin-bottom: 12px; }
    button { width: auto; }
    .toggle { display: inline-flex; align-items: center; gap: 10px; font-size: 14px; font-weight: 600; cursor: pointer; }
    .toggle input { width: 18px; height: 18px; }
    .ok { color: var(--nt-green, #1f8a54); font-size: 13px; font-weight: 700; margin-inline-start: 12px; }
  `]
})
export class SecuritySettingsComponent implements OnInit {
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly loaded = signal(false);
  readonly required = signal(false);
  readonly busy = signal(false);
  readonly saved = signal(false);
  readonly error = signal('');

  ngOnInit(): void {
    if (!this.perms.can('tenant-settings.manage')) { return; }
    this.auth.getTenantMfaPolicy().subscribe({
      next: (p) => { this.required.set(p.requireMfaForPrivilegedRoles); this.loaded.set(true); },
      error: (err: HttpErrorResponse) => this.error.set(err.error?.title ?? 'Could not load the policy.'),
    });
  }

  toggle(event: Event): void {
    const value = (event.target as HTMLInputElement).checked;
    this.busy.set(true);
    this.saved.set(false);
    this.auth.setTenantMfaPolicy(value).subscribe({
      next: () => { this.required.set(value); this.busy.set(false); this.saved.set(true); },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(err.error?.title ?? 'Could not save the policy.');
        // Revert the checkbox to the last confirmed state.
        (event.target as HTMLInputElement).checked = this.required();
      },
    });
  }

  setUpMfa(): void { void this.router.navigate(['/security/mfa-setup']); }
}
