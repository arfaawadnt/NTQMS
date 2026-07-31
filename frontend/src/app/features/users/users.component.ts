import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { UsersFacade } from './users.facade';
import { I18nService } from '../../core/i18n.service';
import { TextPromptService } from '../../core/text-prompt.service';
import { RolesApiService } from '../../core/api/roles-api.service';
import { ReferenceApiService } from '../../core/api/reference-api.service';
import { Branch, Department, RoleSummary, TenantRole, UserAccount } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/**
 * Tenant user administration: list staff, onboard a new user onto a
 * configurable role, move users between roles, confine a user's working scope
 * to specific branches/departments (a hard data filter, enforced server-side),
 * set their interface language, activate/deactivate, and reset passwords.
 */
@Component({
    selector: 'qams-users',
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
            <select formControlName="roleId">
              @for (r of assignableRoles(); track r.id) { <option [value]="r.id">{{ r.name }}</option> }
            </select>
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

    <qams-drawer [open]="scopeFor() !== null" [title]="i18n.t('users.scopeTitle')" (closed)="scopeFor.set(null)">
      @if (scopeFor(); as user) {
        <div class="drawer-form">
          <p class="muted">{{ i18n.t('users.scopeHint') }}</p>

          <h4>{{ i18n.t('ref.branches') }}</h4>
          @for (b of branches(); track b.id) {
            <label class="check">
              <input type="checkbox" [checked]="scopeBranchIds().has(b.id)"
                     (change)="toggleScope('branch', b.id, $event)" /> {{ b.name }}
            </label>
          }

          <h4>{{ i18n.t('ref.departments') }}</h4>
          @for (d of departments(); track d.id) {
            <label class="check">
              <input type="checkbox" [checked]="scopeDepartmentIds().has(d.id)"
                     (change)="toggleScope('department', d.id, $event)" /> {{ d.name }}
            </label>
          }

          <h4>{{ i18n.t('users.language') }}</h4>
          <select [value]="scopeLanguage()" (change)="onLanguageChange($event)" [attr.aria-label]="i18n.t('users.language')">
            <option value="">{{ i18n.t('roles.langInherit') }}</option>
            <option value="en">English</option>
            <option value="ar">العربية</option>
            <option value="fr">Français</option>
          </select>

          <div class="row">
            <button (click)="saveScope(user)" [disabled]="facade.loading()">{{ i18n.t('roles.save') }}</button>
            <button class="secondary" (click)="scopeFor.set(null)">{{ i18n.t('nc.cancel') }}</button>
          </div>
        </div>
      }
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
              <th>{{ i18n.t('users.role') }}</th><th>{{ i18n.t('users.scope') }}</th>
              <th>MFA</th><th>{{ i18n.t('nc.status') }}</th><th></th>
            </tr>
          </thead>
          <tbody>
            @for (u of facade.users(); track u.id) {
              <tr>
                <td>{{ u.displayName }}</td>
                <td>{{ u.email }}</td>
                <td>
                  <select (change)="onRoleChange(u, $event)" [attr.aria-label]="i18n.t('users.role')">
                    @if (u.roleId === null) { <option value="" selected>—</option> }
                    @for (r of assignableRoles(); track r.id) {
                      <option [value]="r.id" [selected]="r.id === u.roleId">{{ r.name }}</option>
                    }
                  </select>
                </td>
                <td>
                  @if (u.branchIds.length === 0 && u.departmentIds.length === 0) {
                    <span class="muted">{{ i18n.t('users.scopeAll') }}</span>
                  } @else {
                    {{ scopeSummary(u) }}
                  }
                </td>
                <td><qams-status-pill [status]="u.mfaEnabled ? 'Active' : 'Pending'" /></td>
                <td><qams-status-pill [status]="u.isActive ? 'Active' : 'Suspended'" /></td>
                <td class="row-actions">
                  <button class="ghost" (click)="openScope(u)">{{ i18n.t('users.scope') }}</button>
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
    .check { display: block; margin: .15rem 0; }
    .check input { width: auto; margin-inline-end: .4rem; }
    h4 { margin: .9rem 0 .3rem; }
    button, select { width: auto; }
    td select { min-width: 150px; }
  `]
})
export class UsersComponent implements OnInit {
  readonly facade = inject(UsersFacade);
  readonly i18n = inject(I18nService);
  private readonly fb = inject(FormBuilder);
  private readonly prompts = inject(TextPromptService);
  private readonly rolesApi = inject(RolesApiService);
  private readonly referenceApi = inject(ReferenceApiService);

  readonly showForm = signal(false);
  readonly roles = signal<RoleSummary[]>([]);
  readonly branches = signal<Branch[]>([]);
  readonly departments = signal<Department[]>([]);

  readonly assignableRoles = computed(() => this.roles().filter((r) => r.isActive));

  /** The user whose working scope is being edited, plus the draft selections. */
  readonly scopeFor = signal<UserAccount | null>(null);
  readonly scopeBranchIds = signal<Set<string>>(new Set());
  readonly scopeDepartmentIds = signal<Set<string>>(new Set());
  readonly scopeLanguage = signal('');

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    displayName: ['', [Validators.required, Validators.maxLength(150)]],
    roleId: ['', [Validators.required]],
    initialPassword: ['', [Validators.required, Validators.minLength(12)]],
  });

  ngOnInit(): void {
    void this.facade.load();
    void this.loadLookups();
  }

  private async loadLookups(): Promise<void> {
    try {
      const [roles, branches, departments] = await Promise.all([
        firstValueFrom(this.rolesApi.list()),
        firstValueFrom(this.referenceApi.branches()),
        firstValueFrom(this.referenceApi.departments()),
      ]);
      this.roles.set(roles);
      this.branches.set(branches.filter((b) => b.isActive));
      this.departments.set(departments);
    } catch {
      // Lookups are best-effort; the table still renders and errors surface on save.
    }
  }

  async register(): Promise<void> {
    if (this.form.invalid) { return; }
    const value = this.form.getRawValue();
    const registered = await this.facade.register({
      email: value.email,
      displayName: value.displayName,
      // The account tier backing structural checks; derived from the seeded
      // role where recognisable, Analyst for custom roles. Privileges come
      // from the assigned role either way.
      role: this.tierFor(value.roleId),
      initialPassword: value.initialPassword,
      roleId: value.roleId,
    });
    if (registered) {
      this.cancel();
    }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ roleId: '' });
  }

  onRoleChange(user: UserAccount, event: Event): void {
    const roleId = (event.target as HTMLSelectElement).value;
    if (roleId && roleId !== user.roleId) {
      void this.facade.assignRole(user.id, { roleId });
    }
  }

  openScope(user: UserAccount): void {
    this.scopeBranchIds.set(new Set(user.branchIds));
    this.scopeDepartmentIds.set(new Set(user.departmentIds));
    this.scopeLanguage.set(user.preferredLanguage ?? '');
    this.scopeFor.set(user);
  }

  toggleScope(kind: 'branch' | 'department', id: string, event: Event): void {
    const source = kind === 'branch' ? this.scopeBranchIds : this.scopeDepartmentIds;
    const next = new Set(source());
    if ((event.target as HTMLInputElement).checked) {
      next.add(id);
    } else {
      next.delete(id);
    }
    source.set(next);
  }

  onLanguageChange(event: Event): void {
    this.scopeLanguage.set((event.target as HTMLSelectElement).value);
  }

  async saveScope(user: UserAccount): Promise<void> {
    await this.facade.setScope(user.id, {
      branchIds: [...this.scopeBranchIds()],
      departmentIds: [...this.scopeDepartmentIds()],
    });
    const language = this.scopeLanguage() || null;
    if (language !== user.preferredLanguage) {
      await this.facade.setLanguage(user.id, { language });
    }
    this.scopeFor.set(null);
  }

  scopeSummary(user: UserAccount): string {
    const branchNames = this.branches().filter((b) => user.branchIds.includes(b.id)).map((b) => b.name);
    const departmentNames = this.departments().filter((d) => user.departmentIds.includes(d.id)).map((d) => d.name);
    return [...branchNames, ...departmentNames].join(', ') || this.i18n.t('users.scopeRestricted');
  }

  /** Collects the admin-set new password in the accessible masked prompt (R-4). */
  async resetPassword(user: UserAccount): Promise<void> {
    const password = await this.prompts.request({
      titleKey: 'users.resetPassword',
      labelKey: 'users.resetPrompt',
      inputType: 'password',
    });
    if (password) {
      void this.facade.resetPassword(user.id, { newPassword: password });
    }
  }

  private tierFor(roleId: string): TenantRole {
    const name = this.roles().find((r) => r.id === roleId)?.name;
    switch (name) {
      case 'Tenant Administrator': return 'TenantAdmin';
      case 'Quality Manager': return 'QualityManager';
      case 'Department Head': return 'DepartmentHead';
      case 'External Auditor': return 'ExternalAuditor';
      default: return 'Analyst';
    }
  }
}
