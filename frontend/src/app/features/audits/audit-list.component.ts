import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { AuditsFacade } from './audits.facade';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import { PermissionsService } from '../../core/permissions.service';
import { AUDIT_TYPES, AuditType, ChecklistItemRequest } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AllocationPickerComponent } from '../../shared/ui/allocation-picker.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { UserSelectComponent } from '../../shared/ui/user-select.component';
import { LoadMoreComponent } from '../../shared/ui/load-more.component';

/** Audit register + a schedule form with a dynamic ISO-clause checklist (FormArray). */
@Component({
  selector: 'qams-audit-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, AllocationPickerComponent, ListStatsComponent, UserSelectComponent, LoadMoreComponent],
  template: `
    <qams-page-header [title]="i18n.t('audit.title')">
      @if (perms.canApprove()) { <button (click)="showForm.set(!showForm())">{{ i18n.t('audit.new') }}</button> }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <div class="filterbar card">
      <input class="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="i18n.t('common.search')" />
      <select [value]="branchFilter()" (change)="branchFilter.set($any($event.target).value)" aria-label="Branch filter">
        <option value="">{{ i18n.t('alloc.allBranches') }}</option>
        @for (b of org.branches(); track b.id) { <option [value]="b.id">{{ b.code }} — {{ b.name }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="i18n.t('audit.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="schedule()">
        <div class="grid">
          <div class="col-2"><label>{{ i18n.t('audit.auditTitle') }}</label><input formControlName="title" /></div>
          <div><label>{{ i18n.t('audit.type') }}</label>
            <select formControlName="type">@for (t of types; track t) { <option [value]="t">{{ t }}</option> }</select></div>
          <div><label>{{ i18n.t('audit.leadAuditor') }}</label>
            <qams-user-select formControlName="leadAuditorId" /></div>
          <div><label>{{ i18n.t('audit.plannedDate') }}</label><input type="date" formControlName="plannedDate" /></div>
        </div>

        <div class="checklist-head">
          <label>{{ i18n.t('audit.checklist') }}</label>
          <button type="button" class="ghost" (click)="addItem()">＋ {{ i18n.t('audit.addItem') }}</button>
        </div>
        <div formArrayName="checklist">
          @for (row of checklist.controls; track $index; let i = $index) {
            <div class="item" [formGroupName]="i">
              <input formControlName="isoClause" [placeholder]="i18n.t('audit.clause')" class="clause" />
              <input formControlName="question" [placeholder]="i18n.t('audit.question')" />
              <button type="button" class="ghost" (click)="removeItem(i)" [disabled]="checklist.length === 1">✕</button>
            </div>
          }
        </div>

        <qams-allocation-picker [branchCtrl]="form.controls.branchId" [departmentCtrl]="form.controls.departmentId" />

        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('audit.schedule') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (filtered().length === 0) {
      <p class="muted">{{ i18n.t('audit.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('audit.ref') }}</th><th>{{ i18n.t('audit.auditTitle') }}</th>
            <th>{{ i18n.t('audit.type') }}</th><th>{{ i18n.t('audit.plannedDate') }}</th><th>{{ i18n.t('nc.status') }}</th>
            <th>{{ i18n.t('alloc.branch') }}</th>
          </tr></thead>
          <tbody>
            @for (a of filtered(); track a.id) {
              <tr class="clickable" (click)="open(a.id)">
                <td>{{ a.auditRef }}</td><td>{{ a.title }}</td><td>{{ a.type }}</td>
                <td>{{ a.plannedDate | date:'mediumDate' }}</td>
                <td><qams-status-pill [status]="a.status" /></td>
                <td class="code">{{ org.branchName(a.branchId) || '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <qams-load-more [shown]="facade.list().length" [total]="facade.total()" [hasMore]="facade.hasMore()"
                      [loading]="facade.loading()" (more)="facade.loadMore()" />
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('audit.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
  styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .form { margin-bottom: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .checklist-head { display: flex; justify-content: space-between; align-items: center; margin-top: 1rem; }
    .item { display: flex; gap: .5rem; margin-bottom: .4rem; }
    .item .clause { max-width: 120px; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button { width: auto; }
  `],
})
export class AuditListComponent implements OnInit {
  readonly facade = inject(AuditsFacade);
  readonly i18n = inject(I18nService);
  readonly org = inject(OrgDataService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly types = AUDIT_TYPES;
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly search = signal('');
  readonly branchFilter = signal('');

  /** Client-side filtration over the loaded register. */
  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const branch = this.branchFilter();
    return this.facade.list().filter((a) =>
      (!branch || a.branchId === branch)
      && (!q || `${a.auditRef} ${a.title} ${a.type} ${a.status}`.toLowerCase().includes(q)));
  });

  /** Live statistics computed from the real register. */
  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('stat.scheduled'), value: all.filter((a) => a.status === 'Scheduled').length, tone: 'blue' },
      { label: this.i18n.t('stat.inProgress'), value: all.filter((a) => a.status === 'InProgress').length, tone: 'gold' },
      { label: this.i18n.t('stat.signedOff'), value: all.filter((a) => a.status === 'SignedOff').length, tone: 'green' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    type: ['Internal' as AuditType, [Validators.required]],
    leadAuditorId: ['', [Validators.required]],
    plannedDate: ['', [Validators.required]],
    checklist: this.fb.array([this.newItem()]),
    branchId: [''],
    departmentId: [''],
  });

  get checklist(): FormArray { return this.form.controls.checklist; }

  ngOnInit(): void {
    void this.facade.loadList();
    void this.org.ensureOrg();
  }

  private newItem(): FormGroup {
    return this.fb.nonNullable.group({
      isoClause: ['', [Validators.maxLength(30)]],
      question: ['', [Validators.required, Validators.maxLength(1000)]],
    });
  }

  addItem(): void { this.checklist.push(this.newItem()); }
  removeItem(index: number): void { if (this.checklist.length > 1) { this.checklist.removeAt(index); } }

  async schedule(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const checklist: ChecklistItemRequest[] = raw.checklist.map((c) => ({ isoClause: c['isoClause'], question: c['question'] }));
    const id = await this.facade.schedule({
      title: raw.title, type: raw.type, leadAuditorId: raw.leadAuditorId,
      plannedDate: raw.plannedDate, checklist,
      branchId: raw.branchId || null,
      departmentId: raw.departmentId || null,
    });
    if (id) { this.cancel(); void this.router.navigate(['/audits', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ type: 'Internal' });
    this.checklist.clear();
    this.checklist.push(this.newItem());
  }

  open(id: string): void { void this.router.navigate(['/audits', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/audits']); }
}
