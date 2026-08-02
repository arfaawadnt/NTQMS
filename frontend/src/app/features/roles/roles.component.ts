import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { RolesApiService } from '../../core/api/roles-api.service';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { TextPromptService } from '../../core/text-prompt.service';
import { PermissionCatalog, PermissionModule, RoleDetail, RoleSummary } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ExportColumn, ExportMenuComponent } from '../../shared/ui/export-menu.component';
import { ListStat } from '../../shared/ui/list-stats.component';

/**
 * Roles & privileges administration: the tenant composes named roles over the
 * permission catalogue (module × action matrix) and the whole system follows.
 * Every grant change requires a reason, which lands in the audit trail —
 * privilege configuration is itself a regulated record (21 CFR Part 11 §11.10).
 */
@Component({
    selector: 'qams-roles',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, StatusPillComponent, ExportMenuComponent],
    template: `
    <qams-page-header [title]="i18n.t('roles.title')" [subtitle]="i18n.t('roles.subtitle')">
      <qams-export-menu [title]="i18n.t('roles.title')" [stats]="stats()" [columns]="exportColumns" [rows]="roles()" />
      @if (perms.can('roles.manage')) {
        <button (click)="openCreate()">{{ i18n.t('roles.new') }}</button>
      }
    </qams-page-header>

    @if (error()) { <div class="error">{{ error() }}</div> }

    @if (loading() && roles().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead>
            <tr>
              <th>{{ i18n.t('roles.name') }}</th>
              <th>{{ i18n.t('roles.kind') }}</th>
              <th>{{ i18n.t('nc.status') }}</th>
              <th>{{ i18n.t('roles.permissions') }}</th>
              <th>{{ i18n.t('roles.members') }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (r of roles(); track r.id) {
              <tr>
                <td>
                  <div class="role-name">{{ r.name }}</div>
                  @if (r.description) { <div class="muted small">{{ r.description }}</div> }
                </td>
                <td>{{ r.isSystem ? i18n.t('roles.system') : i18n.t('roles.custom') }}</td>
                <td><qams-status-pill [status]="r.isActive ? 'Active' : 'Suspended'" /></td>
                <td>{{ r.permissionCount }}</td>
                <td>{{ r.memberCount }}</td>
                <td class="row-actions">
                  <button class="ghost" (click)="openEditor(r)">
                    {{ perms.can('roles.manage') ? i18n.t('perm.action.edit') : i18n.t('roles.inspect') }}
                  </button>
                  @if (perms.can('roles.manage') && !r.isSystem) {
                    @if (r.isActive) {
                      <button class="ghost" (click)="setActive(r, false)">{{ i18n.t('users.deactivate') }}</button>
                    } @else {
                      <button class="ghost" (click)="setActive(r, true)">{{ i18n.t('users.reactivate') }}</button>
                    }
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <qams-drawer [open]="editorOpen()" [title]="editorTitle()" (closed)="closeEditor()" width="860px">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="save()">
        <div class="grid">
          <div>
            <label>{{ i18n.t('roles.name') }}</label>
            <input formControlName="name" [attr.readonly]="editing()?.isSystem ? '' : null" />
            @if (editing()?.isSystem) { <div class="hint">{{ i18n.t('roles.systemNameHint') }}</div> }
          </div>
          <div>
            <label>{{ i18n.t('roles.description') }}</label>
            <input formControlName="description" />
          </div>
          <div>
            <label>{{ i18n.t('roles.defaultLanguage') }}</label>
            <select formControlName="defaultLanguage">
              <option value="">{{ i18n.t('roles.langInherit') }}</option>
              <option value="en">English</option>
              <option value="ar">العربية</option>
              <option value="fr">Français</option>
            </select>
          </div>
        </div>

        <h3 class="matrix-heading">{{ i18n.t('roles.matrix') }}</h3>
        <p class="muted small">{{ i18n.t('roles.matrixHint') }}</p>

        @for (group of groupedModules(); track group.key) {
          <div class="group">
            <h4>{{ i18n.t('perm.group.' + group.key) }}</h4>
            <div class="matrix-scroll">
              <table class="matrix">
                <thead>
                  <tr>
                    <th class="mod-col">{{ i18n.t('roles.module') }}</th>
                    @for (a of actions(); track a) { <th>{{ i18n.t('perm.action.' + a) }}</th> }
                  </tr>
                </thead>
                <tbody>
                  @for (m of group.modules; track m.key) {
                    <tr>
                      <td class="mod-col">{{ i18n.t(m.nameKey) }}</td>
                      @for (a of actions(); track a) {
                        <td class="cell">
                          @if (m.actions.includes(a)) {
                            <input type="checkbox"
                                   [checked]="isGranted(m.key, a)"
                                   [disabled]="!perms.can('roles.manage')"
                                   (change)="toggle(m.key, a, $event)"
                                   [attr.aria-label]="i18n.t(m.nameKey) + ' — ' + i18n.t('perm.action.' + a)" />
                          }
                        </td>
                      }
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        @if (perms.can('roles.manage')) {
          <div class="row">
            <button type="submit" [disabled]="form.invalid || saving()">{{ i18n.t('roles.save') }}</button>
            <button type="button" class="secondary" (click)="closeEditor()">{{ i18n.t('nc.cancel') }}</button>
          </div>
        }
        @if (editorError()) { <div class="error">{{ editorError() }}</div> }
      </form>
    </qams-drawer>
  `,
    styles: [`
    .role-name { font-weight: 600; }
    .small { font-size: .85em; }
    .row-actions { display: flex; gap: .4rem; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: .5rem 1rem; }
    .matrix-heading { margin: 1.2rem 0 .2rem; }
    .group { margin-top: .8rem; }
    .group h4 { margin: .4rem 0; }
    .matrix-scroll { overflow-x: auto; }
    table.matrix { width: 100%; border-collapse: collapse; }
    table.matrix th, table.matrix td { padding: .25rem .45rem; text-align: center; }
    table.matrix .mod-col { text-align: start; min-width: 12rem; }
    .cell input { width: auto; }
    button, select { width: auto; }
  `]
})
export class RolesComponent implements OnInit {
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly api = inject(RolesApiService);
  private readonly fb = inject(FormBuilder);
  private readonly prompts = inject(TextPromptService);

  readonly roles = signal<RoleSummary[]>([]);
  readonly catalog = signal<PermissionCatalog | null>(null);

  readonly exportColumns: ExportColumn<RoleSummary>[] = [
    { header: 'Role Name', cell: (r) => r.name },
    { header: 'Type', cell: (r) => r.isSystem ? 'System' : 'Custom' },
    { header: 'Status', cell: (r) => r.isActive ? 'Active' : 'Suspended' },
    { header: 'Permissions', cell: (r) => `${r.permissionCount}` },
    { header: 'Members', cell: (r) => `${r.memberCount}` },
    { header: 'Description', cell: (r) => r.description ?? '' },
  ];

  readonly stats = computed<readonly ListStat[]>(() => [
    { label: 'Total Roles', value: this.roles().length, tone: 'blue' },
    { label: 'System Roles', value: this.roles().filter(r => r.isSystem).length, tone: 'teal' },
    { label: 'Custom Roles', value: this.roles().filter(r => !r.isSystem).length, tone: 'slate' },
  ]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly editorError = signal('');

  /** Null while creating; the loaded detail while editing. */
  readonly editing = signal<RoleDetail | null>(null);
  readonly editorOpen = signal(false);
  private readonly grantedKeys = signal<Set<string>>(new Set());

  readonly actions = computed(() => this.catalog()?.actions ?? []);

  readonly groupedModules = computed(() => {
    const modules = this.catalog()?.modules ?? [];
    const groups: { key: string; modules: PermissionModule[] }[] = [];
    for (const m of modules) {
      const group = groups.find((g) => g.key === m.group);
      if (group) {
        group.modules.push(m);
      } else {
        groups.push({ key: m.group, modules: [m] });
      }
    }
    return groups;
  });

  readonly editorTitle = computed(() =>
    this.editing() ? this.editing()!.name : this.i18n.t('roles.new'));

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(80)]],
    description: [''],
    defaultLanguage: [''],
  });

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const [catalog, roles] = await Promise.all([
        firstValueFrom(this.api.catalog()),
        firstValueFrom(this.api.list()),
      ]);
      this.catalog.set(catalog);
      this.roles.set(roles);
    } catch (err) {
      this.error.set(this.describe(err));
    } finally {
      this.loading.set(false);
    }
  }

  openCreate(): void {
    this.editing.set(null);
    this.grantedKeys.set(new Set());
    this.form.reset({ name: '', description: '', defaultLanguage: '' });
    this.editorError.set('');
    this.editorOpen.set(true);
  }

  async openEditor(summary: RoleSummary): Promise<void> {
    this.editorError.set('');
    try {
      const detail = await firstValueFrom(this.api.get(summary.id));
      this.editing.set(detail);
      this.grantedKeys.set(new Set(detail.permissionKeys));
      this.form.reset({
        name: detail.name,
        description: detail.description ?? '',
        defaultLanguage: detail.defaultLanguage ?? '',
      });
      this.editorOpen.set(true);
    } catch (err) {
      this.error.set(this.describe(err));
    }
  }

  closeEditor(): void {
    this.editorOpen.set(false);
    this.editing.set(null);
  }

  isGranted(moduleKey: string, action: string): boolean {
    return this.grantedKeys().has(`${moduleKey}.${action}`);
  }

  toggle(moduleKey: string, action: string, event: Event): void {
    const next = new Set(this.grantedKeys());
    const key = `${moduleKey}.${action}`;
    if ((event.target as HTMLInputElement).checked) {
      next.add(key);
    } else {
      next.delete(key);
    }
    this.grantedKeys.set(next);
  }

  async save(): Promise<void> {
    if (this.form.invalid || this.saving()) { return; }
    this.saving.set(true);
    this.editorError.set('');
    const value = this.form.getRawValue();
    const keys = [...this.grantedKeys()].sort();

    try {
      const current = this.editing();
      if (current === null) {
        await firstValueFrom(this.api.create({
          name: value.name,
          description: value.description || null,
          permissionKeys: keys,
          defaultLanguage: value.defaultLanguage || null,
        }));
      } else {
        await firstValueFrom(this.api.update(current.id, {
          name: value.name,
          description: value.description || null,
          defaultLanguage: value.defaultLanguage || null,
        }));

        const before = [...current.permissionKeys].sort();
        if (before.join('\n') !== keys.join('\n')) {
          // A grant change is a regulated change: capture the reason for the trail.
          const reason = await this.prompts.request({
            titleKey: 'roles.reasonTitle',
            labelKey: 'roles.reasonLabel',
          });
          if (!reason) {
            this.saving.set(false);
            return;
          }
          await firstValueFrom(this.api.setPermissions(current.id, { permissionKeys: keys, reason }));
        }
      }

      this.closeEditor();
      await this.load();
    } catch (err) {
      this.editorError.set(this.describe(err));
    } finally {
      this.saving.set(false);
    }
  }

  async setActive(role: RoleSummary, active: boolean): Promise<void> {
    this.error.set('');
    try {
      await firstValueFrom(active ? this.api.reactivate(role.id) : this.api.deactivate(role.id));
      await this.load();
    } catch (err) {
      this.error.set(this.describe(err));
    }
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return (err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`;
    }
    return 'Request failed.';
  }
}
