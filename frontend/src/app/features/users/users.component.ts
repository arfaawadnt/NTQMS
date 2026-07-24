import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { UsersFacade } from './users.facade';
import { I18nService } from '../../core/i18n.service';
import { TENANT_ROLES, TenantRole, UserAccount } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/**
 * Tenant user administration (tenant-admin only): list staff, onboard a new
 * user, change role, activate/deactivate, and reset a password. Enables the
 * multi-user setup that segregation-of-duties workflows require.
 */
@Component({
  selector: 'qams-users',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, StatusPillComponent],
  template: `
    <qams-page-header [title]="i18n.t('users.title')">
      <button (click)="showForm.set(!showForm())">{{ i18n.t('users.new') }}</button>
    </qams-page-header>

    <qams-drawer [open]="showForm()" [title]="i18n.t('users.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="register()">
        <div class="grid">
          <div>
            <label>{{ i18n.t('login.email') }}</label>
            <input formControlName="email" type="email" />
          </div>
          <div>
            <label>{{ i18n.t('users.name') }}</label>
            <input formControlName="displayName" />
          </div>
          <div>
            <label>{{ i18n.t('users.role') }}</label>
            <select formControlName="role">@for (r of roles; track r) { <option [value]="r">{{ r }}</option> }</select>
          </div>
          <div>
            <label>{{ i18n.t('users.initialPassword') }}</label>
            <input formControlName="initialPassword" type="text" [placeholder]="i18n.t('users.passwordHint')" />
          </div>
        </div>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('users.create') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.users().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.users().length === 0) {
      <p class="muted">{{ i18n.t('users.empty') }}</p>
    } @else {
      @if (facade.error() && !showForm()) { <div class="error">{{ facade.error() }}</div> }
      <div class="card">
        <table>
          <thead>
            <tr>
              <th>{{ i18n.t('users.name') }}</th><th>{{ i18n.t('login.email') }}</th>
              <th>{{ i18n.t('users.role') }}</th><th>MFA</th><th>{{ i18n.t('nc.status') }}</th><th></th>
            </tr>
          </thead>
          <tbody>
            @for (u of facade.users(); track u.id) {
              <tr>
                <td>{{ u.displayName }}</td>
                <td>{{ u.email }}</td>
                <td>
                  <select [value]="u.role" (change)="onRoleChange(u, $event)" aria-label="Role">
                    @for (r of roles; track r) { <option [value]="r">{{ r }}</option> }
                  </select>
                </td>
                <td><qams-status-pill [status]="u.mfaEnabled ? 'Active' : 'Pending'" /></td>
                <td><qams-status-pill [status]="u.isActive ? 'Active' : 'Suspended'" /></td>
                <td class="row-actions">
                  @if (u.isActive) {
                    <button class="ghost" (click)="facade.deactivate(u.id)">{{ i18n.t('users.deactivate') }}</button>
                  } @else {
                    <button class="ghost" (click)="facade.reactivate(u.id)">{{ i18n.t('users.reactivate') }}</button>
                  }
                  <button class="ghost" (click)="resetPassword(u)">{{ i18n.t('users.resetPassword') }}</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: [`
    .form { margin-bottom: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: .5rem 1rem; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .row-actions { display: flex; gap: .4rem; }
    button, select { width: auto; }
    td select { min-width: 150px; }
  `],
})
export class UsersComponent implements OnInit {
  readonly facade = inject(UsersFacade);
  readonly i18n = inject(I18nService);
  private readonly fb = inject(FormBuilder);

  readonly roles = TENANT_ROLES;
  readonly showForm = signal(false);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    displayName: ['', [Validators.required, Validators.maxLength(150)]],
    role: ['Analyst' as TenantRole, [Validators.required]],
    initialPassword: ['', [Validators.required, Validators.minLength(12)]],
  });

  ngOnInit(): void { void this.facade.load(); }

  async register(): Promise<void> {
    if (this.form.invalid) { return; }
    if (await this.facade.register(this.form.getRawValue())) {
      this.cancel();
    }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ role: 'Analyst' });
  }

  onRoleChange(user: UserAccount, event: Event): void {
    const role = (event.target as HTMLSelectElement).value as TenantRole;
    if (role !== user.role) {
      void this.facade.changeRole(user.id, { role });
    }
  }

  resetPassword(user: UserAccount): void {
    const password = window.prompt(this.i18n.t('users.resetPrompt'));
    if (password) {
      void this.facade.resetPassword(user.id, { newPassword: password });
    }
  }
}
