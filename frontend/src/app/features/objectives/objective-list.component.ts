import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { ObjectivesFacade } from './objectives.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { OrgDataService } from '../../core/org-data.service';
import { OBJECTIVE_DIRECTIONS, QualityObjectiveListItem } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AllocationPickerComponent } from '../../shared/ui/allocation-picker.component';
import { UserSelectComponent } from '../../shared/ui/user-select.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { ExportColumn, ExportMenuComponent } from '../../shared/ui/export-menu.component';

/** Quality objectives register: measurable targets with live on-target verdicts (§6.2 / §8.2). */
@Component({
    selector: 'qams-objective-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, AllocationPickerComponent, UserSelectComponent, ListStatsComponent, ExportMenuComponent],
    template: `
    <qams-page-header [title]="i18n.t('obj.title')" [subtitle]="i18n.t('obj.subtitle')">
      <qams-export-menu [title]="i18n.t('obj.title')" [stats]="stats()" [columns]="exportColumns"
                        [rows]="filtered()" [filtersSummary]="filtersSummary()" />
      @if (perms.can('objectives.create')) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('obj.new') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" ratioFromFirst />

    <div class="filterbar card">
      <input class="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="i18n.t('common.search')" />
      <select [value]="statusFilter()" (change)="onFilter($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="i18n.t('obj.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="define()">
        <label>{{ i18n.t('obj.objTitle') }}</label>
        <input formControlName="title" [placeholder]="i18n.t('obj.titleHint')" />
        <label>{{ i18n.t('obj.metric') }}</label>
        <input formControlName="metric" [placeholder]="i18n.t('obj.metricHint')" />
        <label>{{ i18n.t('mu.unit') }}</label>
        <input formControlName="unit" [placeholder]="i18n.t('obj.unitHint')" />
        <label>{{ i18n.t('obj.target') }}</label>
        <input type="number" step="any" formControlName="targetValue" />
        <label>{{ i18n.t('obj.direction') }}</label>
        <select formControlName="direction">
          @for (d of directions; track d) { <option [value]="d">{{ i18n.t('obj.dir' + d) }}</option> }
        </select>
        <label>{{ i18n.t('mrv.owner') }}</label>
        <qams-user-select formControlName="ownerId" />
        <label>{{ i18n.t('obj.periodStart') }}</label>
        <input type="date" formControlName="periodStart" />
        <label>{{ i18n.t('obj.periodEnd') }}</label>
        <input type="date" formControlName="periodEnd" />
        <label>{{ i18n.t('nc.description') }}</label>
        <input formControlName="description" [placeholder]="i18n.t('common.optional')" />
        <qams-allocation-picker [branchCtrl]="form.controls.branchId" [departmentCtrl]="form.controls.departmentId" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('qc.create') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (filtered().length === 0) {
      <p class="muted">{{ i18n.t('obj.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('obj.objTitle') }}</th><th>{{ i18n.t('obj.metric') }}</th>
            <th>{{ i18n.t('obj.target') }}</th><th>{{ i18n.t('obj.current') }}</th>
            <th>{{ i18n.t('mrv.owner') }}</th><th>{{ i18n.t('atr.period') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (o of filtered(); track o.id) {
              <tr class="clickable" (click)="open(o.id)">
                <td class="code">{{ o.objectiveRef }}</td>
                <td>{{ o.title }}</td>
                <td class="muted">{{ o.metric }}</td>
                <td>{{ i18n.t('obj.dir' + o.direction) }} {{ o.targetValue | number:'1.0-2' }} {{ o.unit }}</td>
                <td>
                  @if (o.currentValue !== null) {
                    <b [class.good]="o.onTarget === true" [class.bad]="o.onTarget === false">
                      {{ o.currentValue | number:'1.0-2' }} {{ o.unit }}
                    </b>
                  } @else { — }
                </td>
                <td>{{ org.userName(o.ownerId) || '—' }}</td>
                <td class="muted">{{ o.periodStart | date:'MMM y' }} – {{ o.periodEnd | date:'MMM y' }}</td>
                <td><qams-status-pill [status]="o.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('obj.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
    .good { color: var(--nt-green); }
    .bad { color: var(--nt-red); }
  `]
})
export class ObjectiveListComponent implements OnInit {
  readonly facade = inject(ObjectivesFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  readonly org = inject(OrgDataService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = ['Active', 'Achieved', 'Missed', 'Cancelled'];
  readonly directions = OBJECTIVE_DIRECTIONS;
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly statusFilter = signal('');
  readonly search = signal('');

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.facade.list().filter((o) =>
      !q || `${o.objectiveRef} ${o.title} ${o.metric} ${o.status}`.toLowerCase().includes(q));
  });

  /** Export columns — the printed grid mirrors the on-screen table. */
  readonly exportColumns: ExportColumn<QualityObjectiveListItem>[] = [
    { header: this.i18n.t('mu.ref'), cell: (o) => o.objectiveRef },
    { header: this.i18n.t('obj.objTitle'), cell: (o) => o.title },
    { header: this.i18n.t('obj.metric'), cell: (o) => o.metric },
    { header: this.i18n.t('obj.target'), cell: (o) => `${this.i18n.t('obj.dir' + o.direction)} ${o.targetValue} ${o.unit}`.trim() },
    { header: this.i18n.t('obj.current'), cell: (o) => o.currentValue !== null ? `${o.currentValue} ${o.unit}`.trim() : '—' },
    { header: this.i18n.t('mrv.owner'), cell: (o) => this.org.userName(o.ownerId) || '—' },
    { header: this.i18n.t('atr.period'), cell: (o) => `${o.periodStart} – ${o.periodEnd}` },
    { header: this.i18n.t('nc.status'), cell: (o) => o.status },
  ];

  /** The filter line printed on the document, mirroring the filter bar. */
  readonly filtersSummary = computed(() => {
    const parts: string[] = [];
    if (this.statusFilter()) { parts.push(this.statusFilter()); }
    if (this.search().trim()) { parts.push(`"${this.search().trim()}"`); }
    return parts.length ? parts.join(' · ') : this.i18n.t('exp.allRecords');
  });

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('obj.onTarget'), value: all.filter((o) => o.status === 'Active' && o.onTarget === true).length, tone: 'green' },
      { label: this.i18n.t('obj.offTarget'), value: all.filter((o) => o.status === 'Active' && o.onTarget === false).length, tone: 'red' },
      { label: this.i18n.t('obj.achieved'), value: all.filter((o) => o.status === 'Achieved').length, tone: 'teal' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    description: [''],
    metric: ['', [Validators.required, Validators.maxLength(300)]],
    unit: ['', [Validators.maxLength(30)]],
    targetValue: [90, [Validators.required]],
    direction: ['AtLeast' as (typeof OBJECTIVE_DIRECTIONS)[number], [Validators.required]],
    ownerId: ['', [Validators.required]],
    periodStart: ['', [Validators.required]],
    periodEnd: ['', [Validators.required]],
    branchId: [''],
    departmentId: [''],
  });

  ngOnInit(): void {
    void this.facade.loadList();
    void this.org.ensureDirectory();
  }

  onFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.statusFilter() || undefined);
  }

  async define(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.define({
      ...raw,
      description: raw.description.trim() || null,
      branchId: raw.branchId || null,
      departmentId: raw.departmentId || null,
    });
    if (id) { this.cancel(); void this.router.navigate(['/quality-objectives', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ targetValue: 90, direction: 'AtLeast' });
  }

  open(id: string): void { void this.router.navigate(['/quality-objectives', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/quality-objectives']); }
}
