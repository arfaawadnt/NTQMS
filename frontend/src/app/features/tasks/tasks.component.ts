import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { TasksFacade } from './tasks.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { TENANT_ROLES } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { UserSelectComponent } from '../../shared/ui/user-select.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { LoadMoreComponent } from '../../shared/ui/load-more.component';

/** Group-level validator: a task must name a user or a role (mirrors TASK-002). */
function assigneeRequired(group: AbstractControl): ValidationErrors | null {
  const user = (group.get('assigneeUserId')?.value as string ?? '').trim();
  const role = (group.get('assigneeRole')?.value as string ?? '').trim();
  return user || role ? null : { assigneeRequired: true };
}

/**
 * "My Tasks" queue (direct + role assignments, overdue flagged server-side)
 * with role-gated task creation, plus the SLA definitions that drive the
 * backend escalation sweep (QM/TenantAdmin only).
 */
@Component({
  selector: 'qams-tasks',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, StatusPillComponent, UserSelectComponent, LoadMoreComponent],
  template: `
    <qams-page-header [title]="i18n.t('task.title')" [subtitle]="i18n.t('task.subtitle')">
      @if (perms.canAssignTraining()) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('task.new') }}</button>
      }
    </qams-page-header>

    <qams-drawer [open]="showForm()" [title]="i18n.t('task.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('task.subject') }}</label>
        <input formControlName="subject" />
        <label>{{ i18n.t('task.subjectRef') }}</label>
        <input formControlName="subjectRef" [placeholder]="i18n.t('common.optional')" />
        <label>{{ i18n.t('task.assigneeUser') }}</label>
        <qams-user-select formControlName="assigneeUserId" />
        <label>{{ i18n.t('task.assigneeRole') }}</label>
        <select formControlName="assigneeRole">
          <option value="">—</option>
          @for (r of roles; track r) { <option [value]="r">{{ r }}</option> }
        </select>
        <div class="hint">{{ i18n.t('task.assigneeHint') }}</div>
        <label>{{ i18n.t('train.dueDate') }}</label>
        <input type="date" formControlName="dueDate" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('task.create') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.error() && !showForm()) { <div class="error">{{ facade.error() }}</div> }
    @if (facade.loading() && facade.tasks().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.tasks().length === 0) {
      <p class="muted">{{ i18n.t('task.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('task.subject') }}</th><th>{{ i18n.t('task.subjectRef') }}</th>
            <th>{{ i18n.t('task.assignedTo') }}</th><th>{{ i18n.t('train.dueDate') }}</th>
            <th>{{ i18n.t('nc.status') }}</th><th></th>
          </tr></thead>
          <tbody>
            @for (t of facade.tasks(); track t.id) {
              <tr [class.overdue]="t.overdue && t.status === 'Pending'">
                <td>{{ t.subject }}</td>
                <td class="code">{{ t.subjectRef ?? '—' }}</td>
                <td>{{ t.assigneeRole ?? i18n.t('task.direct') }}</td>
                <td>
                  {{ t.dueDate | date:'mediumDate' }}
                  @if (t.overdue && t.status === 'Pending') { <span class="over-tag">{{ i18n.t('task.overdue') }}</span> }
                </td>
                <td><qams-status-pill [status]="t.status === 'Pending' ? 'InProgress' : t.status" /></td>
                <td>
                  @if (t.status === 'Pending') {
                    <button class="link" type="button" (click)="facade.completeTask(t.id)">{{ i18n.t('task.complete') }}</button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <qams-load-more [shown]="facade.tasks().length" [total]="facade.total()" [hasMore]="facade.hasMore()"
                      [loading]="facade.loading()" (more)="facade.loadMore()" />
    }

    @if (perms.canApprove()) {
      <section class="card sla">
        <h3>{{ i18n.t('sla.title') }}</h3>
        <p class="muted small">{{ i18n.t('sla.subtitle') }}</p>
        @if (facade.sla().length > 0) {
          <table>
            <thead><tr><th>{{ i18n.t('sla.module') }}</th><th>{{ i18n.t('sla.severity') }}</th><th>{{ i18n.t('sla.target') }}</th></tr></thead>
            <tbody>
              @for (s of facade.sla(); track s.id) {
                <tr><td>{{ s.module }}</td><td class="code">{{ s.severity }}</td><td>{{ s.targetHours }}h</td></tr>
              }
            </tbody>
          </table>
        }
        <form [formGroup]="slaForm" (ngSubmit)="upsertSla()">
          <div class="trio">
            <div><label>{{ i18n.t('sla.module') }}</label><input formControlName="module" [placeholder]="i18n.t('sla.moduleHint')" /></div>
            <div><label>{{ i18n.t('sla.severity') }}</label><input formControlName="severity" [placeholder]="i18n.t('sla.severityHint')" /></div>
            <div><label>{{ i18n.t('sla.target') }}</label><input type="number" min="1" formControlName="targetHours" /></div>
          </div>
          <button type="submit" [disabled]="slaForm.invalid || facade.loading()">{{ i18n.t('sla.upsert') }}</button>
        </form>
      </section>
    }
  `,
  styles: [`
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .overdue td { color: var(--nt-red); }
    .over-tag {
      margin-inline-start: 8px; font-size: 10.5px; font-weight: 700; color: var(--nt-red);
      background: rgba(220, 53, 69, .12); border-radius: 999px; padding: 2px 8px;
    }
    .sla { margin-top: 1rem; }
    .small { font-size: 11.5px; margin: -6px 0 10px; }
    .trio { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 1rem; }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    button, select { width: auto; }
  `],
})
export class TasksComponent implements OnInit {
  readonly facade = inject(TasksFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  readonly roles = TENANT_ROLES;
  readonly showForm = signal(false);

  readonly form = this.fb.nonNullable.group({
    subject: ['', [Validators.required, Validators.maxLength(500)]],
    subjectRef: [''],
    assigneeUserId: [''],
    assigneeRole: [''],
    dueDate: ['', [Validators.required]],
  }, { validators: [assigneeRequired] });

  readonly slaForm = this.fb.nonNullable.group({
    module: ['', [Validators.required, Validators.maxLength(100)]],
    severity: ['', [Validators.required, Validators.maxLength(50)]],
    targetHours: [72, [Validators.required, Validators.min(1)]],
  });

  ngOnInit(): void {
    void this.facade.loadTasks();
    if (this.perms.canApprove()) { void this.facade.loadSla(); }
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const created = await this.facade.createTask({
      subject: raw.subject,
      subjectRef: raw.subjectRef.trim() || null,
      assigneeUserId: raw.assigneeUserId.trim() || null,
      assigneeRole: raw.assigneeRole.trim() || null,
      dueDate: raw.dueDate,
    });
    if (created) { this.cancel(); }
  }

  async upsertSla(): Promise<void> {
    if (this.slaForm.invalid) { return; }
    if (await this.facade.upsertSla(this.slaForm.getRawValue())) {
      this.slaForm.reset({ targetHours: 72 });
    }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset();
  }
}
