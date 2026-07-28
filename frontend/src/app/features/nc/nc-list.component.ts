import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterOutlet } from '@angular/router';
import { NcFacade } from './nc.facade';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import { ExportsApiService } from '../../core/api/exports-api.service';
import { NC_SOURCE_TYPES, NcSourceType, QUALITY_EVENT_TYPES, QualityEventType } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AllocationPickerComponent } from '../../shared/ui/allocation-picker.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { LoadMoreComponent } from '../../shared/ui/load-more.component';

/**
 * Nonconformance register: live statistics, professional filtration (text
 * search + status + branch/department), and a raise form with organizational
 * allocation.
 */
@Component({
    selector: 'qams-nc-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, AllocationPickerComponent, ListStatsComponent, LoadMoreComponent],
    template: `
    <qams-page-header [title]="i18n.t('nc.title')">
      <button class="secondary" (click)="exports.ncRegisterXlsx()">{{ i18n.t('exp.xlsx') }}</button>
      <button (click)="showForm.set(!showForm())">{{ i18n.t('nc.new') }}</button>
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <div class="filterbar card">
      <input class="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="i18n.t('common.search')" />
      <select [value]="statusFilter()" (change)="onFilter($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
      <select [value]="branchFilter()" (change)="branchFilter.set($any($event.target).value)" aria-label="Branch filter">
        <option value="">{{ i18n.t('alloc.allBranches') }}</option>
        @for (b of org.branches(); track b.id) { <option [value]="b.id">{{ b.code }} — {{ b.name }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="i18n.t('nc.new')" (closed)="showForm.set(false)">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <div class="grid">
          <div class="col-2">
            <label>{{ i18n.t('nc.subject') }}</label>
            <input formControlName="title" />
          </div>
          <div>
            <label>{{ i18n.t('nc.source') }}</label>
            <select formControlName="sourceType">
              @for (s of sources; track s) { <option [value]="s">{{ s }}</option> }
            </select>
          </div>
          <div>
            <label>{{ i18n.t('nc.eventType') }}</label>
            <select formControlName="eventType">
              @for (e of eventTypes; track e) { <option [value]="e">{{ i18n.t('nc.event.' + e) }}</option> }
            </select>
          </div>
          <div>
            <label>{{ i18n.t('nc.severity') }} (1-5)</label>
            <input type="number" min="1" max="5" formControlName="severity" />
          </div>
          <div>
            <label>{{ i18n.t('nc.likelihood') }} (1-5)</label>
            <input type="number" min="1" max="5" formControlName="likelihood" />
          </div>
        </div>
        <label>{{ i18n.t('nc.description') }}</label>
        <textarea rows="3" formControlName="description"></textarea>
        <qams-allocation-picker [branchCtrl]="form.controls.branchId" [departmentCtrl]="form.controls.departmentId" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('nc.create') }}</button>
          <button type="button" class="secondary" (click)="showForm.set(false)">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (filtered().length === 0) {
      <p class="muted">{{ i18n.t('nc.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead>
            <tr>
              <th>{{ i18n.t('nc.ref') }}</th><th>{{ i18n.t('nc.subject') }}</th>
              <th>{{ i18n.t('nc.status') }}</th><th>{{ i18n.t('nc.severity') }}</th>
              <th>{{ i18n.t('nc.rpn') }}</th><th>{{ i18n.t('nc.source') }}</th>
              <th>{{ i18n.t('nc.eventType') }}</th>
              <th>{{ i18n.t('alloc.branch') }}</th>
            </tr>
          </thead>
          <tbody>
            @for (nc of filtered(); track nc.id) {
              <tr class="clickable" (click)="open(nc.id)">
                <td>{{ nc.ncRef }}</td>
                <td>{{ nc.title }}</td>
                <td><qams-status-pill [status]="nc.status" /></td>
                <td>{{ nc.severity }}</td>
                <td [class.danger-text]="nc.rpn > 12">{{ nc.rpn }}</td>
                <td>{{ nc.sourceType }}</td>
                <td>{{ i18n.t('nc.event.' + nc.eventType) }}</td>
                <td class="code">{{ org.branchName(nc.branchId) || '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <qams-load-more [shown]="facade.list().length" [total]="facade.total()" [hasMore]="facade.hasMore()"
                      [loading]="facade.loading()" (more)="facade.loadMore()" />
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('nc.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    .danger-text { color: var(--nt-danger); font-weight: 700; }
    select, button { width: auto; }
  `]
})
export class NcListComponent implements OnInit {
  readonly facade = inject(NcFacade);
  readonly i18n = inject(I18nService);
  readonly org = inject(OrgDataService);
  readonly exports = inject(ExportsApiService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly sources = NC_SOURCE_TYPES;
  readonly eventTypes = QUALITY_EVENT_TYPES;
  readonly statuses = ['Draft', 'Raised', 'Assigned', 'Rca', 'ActionPlan', 'PendingVerification', 'EffectivenessCheck', 'Closed', 'Rejected'];
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly statusFilter = signal('');
  readonly search = signal('');
  readonly branchFilter = signal('');

  /** Client-side filtration over the loaded register (status filters server-side). */
  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const branch = this.branchFilter();
    return this.facade.list().filter((nc) =>
      (!branch || nc.branchId === branch)
      && (!q || `${nc.ncRef} ${nc.title} ${nc.sourceType} ${nc.status}`.toLowerCase().includes(q)));
  });

  /** Live statistics computed from the real register. */
  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('stat.open'), value: all.filter((n) => n.status !== 'Closed' && n.status !== 'Rejected').length, tone: 'blue' },
      { label: this.i18n.t('stat.highRpn'), value: all.filter((n) => n.rpn > 12).length, tone: 'red' },
      { label: this.i18n.t('stat.highSeverity'), value: all.filter((n) => n.severity >= 4).length, tone: 'orange' },
      { label: this.i18n.t('stat.closed'), value: all.filter((n) => n.status === 'Closed').length, tone: 'green' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    description: ['', [Validators.maxLength(4000)]],
    severity: [3, [Validators.required, Validators.min(1), Validators.max(5)]],
    likelihood: [3, [Validators.required, Validators.min(1), Validators.max(5)]],
    sourceType: ['Internal' as NcSourceType, [Validators.required]],
    eventType: ['Nonconformity' as QualityEventType, [Validators.required]],
    branchId: [''],
    departmentId: [''],
  });

  ngOnInit(): void {
    void this.facade.loadList();
    void this.org.ensureOrg();
  }

  onFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.statusFilter() || undefined);
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.raise({
      ...raw,
      branchId: raw.branchId || null,
      departmentId: raw.departmentId || null,
    });
    if (id) {
      this.showForm.set(false);
      this.form.reset({ severity: 3, likelihood: 3, sourceType: 'Internal', eventType: 'Nonconformity' });
      void this.router.navigate(['/nonconformances', id]);
    }
  }

  open(id: string): void {
    void this.router.navigate(['/nonconformances', id]);
  }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/nonconformances']); }
}
