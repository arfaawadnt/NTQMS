import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { MonitoringFacade } from './monitoring.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AllocationPickerComponent } from '../../shared/ui/allocation-picker.component';
import { LovSelectComponent } from '../../shared/ui/lov-select.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/** Environmental monitoring register: points, latest readings, excursion counts (§6.3). */
@Component({
    selector: 'qams-monitoring-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, AllocationPickerComponent, LovSelectComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('env.title')" [subtitle]="i18n.t('env.subtitle')">
      @if (perms.canAssignTraining()) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('env.new') }}</button>
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

    <qams-drawer [open]="showForm()" [title]="i18n.t('env.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="register()">
        <label>{{ i18n.t('std.name') }}</label>
        <input formControlName="name" [placeholder]="i18n.t('env.nameHint')" />
        <label>{{ i18n.t('equip.location') }}</label>
        <input formControlName="location" />
        <label>{{ i18n.t('env.parameter') }}</label>
        <qams-lov-select formControlName="parameter" category="ENV_PARAMETER" [placeholder]="i18n.t('env.parameterHint')" />
        <label>{{ i18n.t('mu.unit') }}</label>
        <input formControlName="unit" [placeholder]="i18n.t('env.unitHint')" />
        <label>{{ i18n.t('env.lowLimit') }}</label>
        <input type="number" step="any" formControlName="lowLimit" [placeholder]="i18n.t('common.optional')" />
        <label>{{ i18n.t('env.highLimit') }}</label>
        <input type="number" step="any" formControlName="highLimit" [placeholder]="i18n.t('common.optional')" />
        <div class="hint">{{ i18n.t('env.limitsHint') }}</div>
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
      <p class="muted">{{ i18n.t('env.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('std.name') }}</th><th>{{ i18n.t('env.parameter') }}</th>
            <th>{{ i18n.t('env.window') }}</th><th>{{ i18n.t('env.lastReading') }}</th>
            <th>{{ i18n.t('env.excursions') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (p of filtered(); track p.id) {
              <tr class="clickable" (click)="open(p.id)">
                <td class="code">{{ p.pointRef }}</td>
                <td>{{ p.name }}</td>
                <td>{{ p.parameter }} ({{ p.unit }})</td>
                <td>{{ window(p.lowLimit, p.highLimit, p.unit) }}</td>
                <td>
                  @if (p.lastValue !== null) {
                    <b [class.bad]="p.lastInLimit === false" [class.good]="p.lastInLimit === true">
                      {{ p.lastValue | number:'1.0-2' }} {{ p.unit }}
                    </b>
                    <span class="muted"> · {{ p.lastRecordedAtUtc | date:'short' }}</span>
                  } @else { — }
                </td>
                <td>
                  @if (p.excursionCount > 0) { <span class="count bad">{{ p.excursionCount }}</span> }
                  @else { <span class="muted">0</span> }
                </td>
                <td><qams-status-pill [status]="p.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('env.title')" width="920px" (closed)="closeDetail()">
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
    .count.bad { font-weight: 700; }
  `]
})
export class MonitoringListComponent implements OnInit {
  readonly facade = inject(MonitoringFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = ['Active', 'Suspended', 'Retired'];
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly statusFilter = signal('');
  readonly search = signal('');

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.facade.list().filter((p) =>
      !q || `${p.pointRef} ${p.name} ${p.location ?? ''} ${p.parameter} ${p.status}`.toLowerCase().includes(q));
  });

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('std.active'), value: all.filter((p) => p.status === 'Active').length, tone: 'green' },
      { label: this.i18n.t('env.outOfLimit'), value: all.filter((p) => p.lastInLimit === false).length, tone: 'red' },
      { label: this.i18n.t('env.excursions'), value: all.reduce((sum, p) => sum + p.excursionCount, 0), tone: 'orange' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    location: ['', [Validators.maxLength(200)]],
    parameter: ['', [Validators.required, Validators.maxLength(100)]],
    unit: ['', [Validators.required, Validators.maxLength(30)]],
    lowLimit: [null as number | null],
    highLimit: [null as number | null],
    branchId: [''],
    departmentId: [''],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  window(low: number | null, high: number | null, unit: string): string {
    if (low !== null && high !== null) { return `${low}–${high} ${unit}`; }
    return low !== null ? `≥ ${low} ${unit}` : high !== null ? `≤ ${high} ${unit}` : '—';
  }

  onFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.statusFilter() || undefined);
  }

  async register(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.register({
      ...raw,
      location: raw.location.trim() || null,
      branchId: raw.branchId || null,
      departmentId: raw.departmentId || null,
    });
    if (id) { this.cancel(); void this.router.navigate(['/monitoring', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset();
  }

  open(id: string): void { void this.router.navigate(['/monitoring', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/monitoring']); }
}
