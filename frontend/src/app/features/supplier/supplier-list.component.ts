import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterOutlet } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SupplierFacade } from './supplier.facade';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import { SuppliersApiService } from '../../core/api/suppliers-api.service';
import { SupplierListItem } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AllocationPickerComponent } from '../../shared/ui/allocation-picker.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { ExportColumn, ExportMenuComponent } from '../../shared/ui/export-menu.component';
import { LovSelectComponent } from '../../shared/ui/lov-select.component';
import { LoadMoreComponent } from '../../shared/ui/load-more.component';

/** Approved-supplier register: live statistics, filterable list + a register form. */
@Component({
    selector: 'qams-supplier-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, AllocationPickerComponent, ListStatsComponent, LovSelectComponent, LoadMoreComponent, ExportMenuComponent],
    template: `
    <qams-page-header [title]="i18n.t('sup.title')">
      <qams-export-menu [title]="i18n.t('sup.title')" [stats]="stats()" [columns]="exportColumns"
                        [rows]="filtered()" [fetchAll]="exportAll" [filtersSummary]="filtersSummary()" />
      <button (click)="showForm.set(!showForm())">{{ i18n.t('sup.new') }}</button>
    </qams-page-header>

    <qams-list-stats [stats]="stats()" ratioFromFirst />

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

    <qams-drawer [open]="showForm()" [title]="i18n.t('sup.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="register()">
        <div class="grid">
          <div class="col-2"><label>{{ i18n.t('sup.name') }}</label><input formControlName="name" /></div>
          <div>
            <label>{{ i18n.t('sup.type') }}</label>
            <qams-lov-select formControlName="supplierType" category="SUPPLIER_TYPE" [placeholder]="i18n.t('sup.type')" />
          </div>
        </div>
        <qams-allocation-picker [branchCtrl]="form.controls.branchId" [departmentCtrl]="form.controls.departmentId" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('sup.register') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (filtered().length === 0) {
      <p class="muted">{{ i18n.t('sup.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('sup.ref') }}</th><th>{{ i18n.t('sup.name') }}</th>
            <th>{{ i18n.t('sup.type') }}</th><th>{{ i18n.t('nc.status') }}</th>
            <th>{{ i18n.t('alloc.branch') }}</th>
          </tr></thead>
          <tbody>
            @for (s of filtered(); track s.id) {
              <tr class="clickable" (click)="open(s.id)">
                <td class="code">{{ s.supplierRef }}</td><td>{{ s.name }}</td>
                <td>{{ s.supplierType }}</td>
                <td><qams-status-pill [status]="s.status" /></td>
                <td class="code">{{ org.branchName(s.branchId) || '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <qams-load-more [shown]="facade.list().length" [total]="facade.total()" [hasMore]="facade.hasMore()"
                      [loading]="facade.loading()" (more)="facade.loadMore()" />
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('sup.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .form { margin-bottom: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
  `]
})
export class SupplierListComponent implements OnInit {
  readonly facade = inject(SupplierFacade);
  readonly i18n = inject(I18nService);
  readonly org = inject(OrgDataService);
  private readonly api = inject(SuppliersApiService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = ['PendingEvaluation', 'Approved', 'Suspended'];
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
    return this.facade.list().filter((s) =>
      (!branch || s.branchId === branch)
      && (!q || `${s.supplierRef} ${s.name} ${s.supplierType} ${s.status}`.toLowerCase().includes(q)));
  });

  /** Export columns — the printed grid mirrors the on-screen table. */
  readonly exportColumns: ExportColumn<SupplierListItem>[] = [
    { header: this.i18n.t('sup.ref'), cell: (s) => s.supplierRef },
    { header: this.i18n.t('sup.name'), cell: (s) => s.name },
    { header: this.i18n.t('sup.type'), cell: (s) => s.supplierType },
    { header: this.i18n.t('nc.status'), cell: (s) => s.status },
    { header: this.i18n.t('alloc.branch'), cell: (s) => this.org.branchName(s.branchId) || '—' },
  ];

  /** The filter line printed on the document, mirroring the filter bar. */
  readonly filtersSummary = computed(() => {
    const parts: string[] = [];
    if (this.statusFilter()) { parts.push(this.statusFilter()); }
    if (this.branchFilter()) { parts.push(this.org.branchName(this.branchFilter())); }
    if (this.search().trim()) { parts.push(`"${this.search().trim()}"`); }
    return parts.length ? parts.join(' · ') : this.i18n.t('exp.allRecords');
  });

  /**
   * Pulls every page of the current server-side filter so the document holds
   * the whole filtered register, then applies the same client-side narrowing
   * the screen applies.
   */
  readonly exportAll = async (): Promise<readonly SupplierListItem[]> => {
    const all: SupplierListItem[] = [];
    for (let page = 1; page <= 25; page++) {
      const batch = await firstValueFrom(this.api.list(this.statusFilter() || undefined, page, 200));
      all.push(...batch.items);
      if (!batch.hasMore) { break; }
    }
    const q = this.search().trim().toLowerCase();
    const branch = this.branchFilter();
    return all.filter((s) =>
      (!branch || s.branchId === branch)
      && (!q || `${s.supplierRef} ${s.name} ${s.supplierType} ${s.status}`.toLowerCase().includes(q)));
  };

  /** Live statistics computed from the real register. */
  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('stat.pendingApproval'), value: all.filter((s) => s.status === 'PendingEvaluation').length, tone: 'gold' },
      { label: this.i18n.t('stat.approved'), value: all.filter((s) => s.status === 'Approved').length, tone: 'green' },
      { label: this.i18n.t('stat.suspended'), value: all.filter((s) => s.status === 'Suspended').length, tone: 'red' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    supplierType: ['Reagents', [Validators.required]],
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

  async register(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.register({
      ...raw,
      branchId: raw.branchId || null,
      departmentId: raw.departmentId || null,
    });
    if (id) { this.cancel(); void this.router.navigate(['/suppliers', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ supplierType: 'Reagents' });
  }

  open(id: string): void { void this.router.navigate(['/suppliers', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/suppliers']); }
}
