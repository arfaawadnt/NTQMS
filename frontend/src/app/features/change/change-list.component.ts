import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterOutlet } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ChangeFacade } from './change.facade';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import { ChangeApiService } from '../../core/api/change-api.service';
import { ChangeListItem } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AllocationPickerComponent } from '../../shared/ui/allocation-picker.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { ExportColumn, ExportMenuComponent } from '../../shared/ui/export-menu.component';
import { LoadMoreComponent } from '../../shared/ui/load-more.component';

/** Change Control register: live statistics, filterable list + a propose form. */
@Component({
    selector: 'qams-change-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, AllocationPickerComponent, ListStatsComponent, LoadMoreComponent, ExportMenuComponent],
    template: `
    <qams-page-header [title]="i18n.t('chg.title')">
      <qams-export-menu [title]="i18n.t('chg.title')" [stats]="stats()" [columns]="exportColumns"
                        [rows]="filtered()" [fetchAll]="exportAll" [filtersSummary]="filtersSummary()" />
      <button (click)="showForm.set(!showForm())">{{ i18n.t('chg.new') }}</button>
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

    <qams-drawer [open]="showForm()" [title]="i18n.t('chg.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="propose()">
        <label>{{ i18n.t('chg.changeTitle') }}</label>
        <input formControlName="title" />
        <label>{{ i18n.t('chg.impact') }}</label>
        <textarea formControlName="impactAnalysis" rows="4" [placeholder]="i18n.t('chg.impactHint')"></textarea>
        <qams-allocation-picker [branchCtrl]="form.controls.branchId" [departmentCtrl]="form.controls.departmentId" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('chg.propose') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (filtered().length === 0) {
      <p class="muted">{{ i18n.t('chg.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('chg.ref') }}</th><th>{{ i18n.t('chg.changeTitle') }}</th>
            <th>{{ i18n.t('nc.status') }}</th><th>{{ i18n.t('chg.riskLinked') }}</th>
            <th>{{ i18n.t('alloc.branch') }}</th>
          </tr></thead>
          <tbody>
            @for (c of filtered(); track c.id) {
              <tr class="clickable" (click)="open(c.id)">
                <td>{{ c.changeRef }}</td><td>{{ c.title }}</td>
                <td><qams-status-pill [status]="c.status" /></td>
                <td>{{ c.riskItemId ? i18n.t('common.yes') : i18n.t('common.no') }}</td>
                <td class="code">{{ org.branchName(c.branchId) || '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <qams-load-more [shown]="facade.list().length" [total]="facade.total()" [hasMore]="facade.hasMore()"
                      [loading]="facade.loading()" (more)="facade.loadMore()" />
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('chg.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .form { margin-bottom: 1rem; }
    .form textarea { width: 100%; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
  `]
})
export class ChangeListComponent implements OnInit {
  readonly facade = inject(ChangeFacade);
  readonly i18n = inject(I18nService);
  readonly org = inject(OrgDataService);
  private readonly api = inject(ChangeApiService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = ['Proposed', 'Approved', 'Rejected', 'Closed'];
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
    return this.facade.list().filter((c) =>
      (!branch || c.branchId === branch)
      && (!q || `${c.changeRef} ${c.title} ${c.status}`.toLowerCase().includes(q)));
  });

  /** Export columns — the printed grid mirrors the on-screen table. */
  readonly exportColumns: ExportColumn<ChangeListItem>[] = [
    { header: this.i18n.t('chg.ref'), cell: (c) => c.changeRef },
    { header: this.i18n.t('chg.changeTitle'), cell: (c) => c.title },
    { header: this.i18n.t('nc.status'), cell: (c) => c.status },
    { header: this.i18n.t('chg.riskLinked'), cell: (c) => c.riskItemId ? this.i18n.t('common.yes') : this.i18n.t('common.no') },
    { header: this.i18n.t('alloc.branch'), cell: (c) => this.org.branchName(c.branchId) || '—' },
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
  readonly exportAll = async (): Promise<readonly ChangeListItem[]> => {
    const all: ChangeListItem[] = [];
    for (let page = 1; page <= 25; page++) {
      const batch = await firstValueFrom(this.api.list(this.statusFilter() || undefined, page, 200));
      all.push(...batch.items);
      if (!batch.hasMore) { break; }
    }
    const q = this.search().trim().toLowerCase();
    const branch = this.branchFilter();
    return all.filter((c) =>
      (!branch || c.branchId === branch)
      && (!q || `${c.changeRef} ${c.title} ${c.status}`.toLowerCase().includes(q)));
  };

  /** Live statistics computed from the real register. */
  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('stat.pendingApproval'), value: all.filter((c) => c.status === 'Proposed').length, tone: 'gold' },
      { label: this.i18n.t('stat.approved'), value: all.filter((c) => c.status === 'Approved').length, tone: 'teal' },
      { label: this.i18n.t('stat.rejected'), value: all.filter((c) => c.status === 'Rejected').length, tone: 'red' },
      { label: this.i18n.t('stat.closed'), value: all.filter((c) => c.status === 'Closed').length, tone: 'green' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    impactAnalysis: ['', [Validators.required, Validators.maxLength(4000)]],
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

  async propose(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.propose({
      ...raw,
      branchId: raw.branchId || null,
      departmentId: raw.departmentId || null,
    });
    if (id) { this.cancel(); void this.router.navigate(['/changes', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset();
  }

  open(id: string): void { void this.router.navigate(['/changes', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/changes']); }
}
