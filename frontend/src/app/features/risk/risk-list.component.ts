import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterOutlet } from '@angular/router';
import { RiskFacade } from './risk.facade';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import { HIGH_RESIDUAL_RPN_THRESHOLD } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AllocationPickerComponent } from '../../shared/ui/allocation-picker.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { LovSelectComponent } from '../../shared/ui/lov-select.component';
import { LoadMoreComponent } from '../../shared/ui/load-more.component';

/**
 * Risk register: live statistics, professional filtration (text search +
 * status + branch), and an assess form (1-5 likelihood/impact) with
 * LOV-backed category and organizational allocation.
 */
@Component({
    selector: 'qams-risk-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, AllocationPickerComponent, ListStatsComponent, LovSelectComponent, LoadMoreComponent],
    template: `
    <qams-page-header [title]="i18n.t('risk.title')">
      <button (click)="showForm.set(!showForm())">{{ i18n.t('risk.new') }}</button>
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

    <qams-drawer [open]="showForm()" [title]="i18n.t('risk.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="assess()">
        <div class="grid">
          <div class="col-2"><label>{{ i18n.t('risk.riskTitle') }}</label><input formControlName="title" /></div>
          <div>
            <label>{{ i18n.t('risk.category') }}</label>
            <qams-lov-select formControlName="category" category="RISK_CATEGORY" [placeholder]="i18n.t('risk.category')" />
          </div>
          <div><label>{{ i18n.t('risk.likelihood') }}</label><input type="number" min="1" max="5" formControlName="likelihood" /></div>
          <div><label>{{ i18n.t('risk.impact') }}</label><input type="number" min="1" max="5" formControlName="impact" /></div>
        </div>
        <qams-allocation-picker [branchCtrl]="form.controls.branchId" [departmentCtrl]="form.controls.departmentId" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('risk.assess') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (filtered().length === 0) {
      <p class="muted">{{ i18n.t('risk.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('risk.ref') }}</th><th>{{ i18n.t('risk.riskTitle') }}</th><th>{{ i18n.t('risk.category') }}</th>
            <th>{{ i18n.t('nc.status') }}</th><th>{{ i18n.t('risk.rpn') }}</th><th>{{ i18n.t('risk.residual') }}</th>
            <th>{{ i18n.t('alloc.branch') }}</th>
          </tr></thead>
          <tbody>
            @for (r of filtered(); track r.id) {
              <tr class="clickable" (click)="open(r.id)">
                <td>{{ r.riskRef }}</td><td>{{ r.title }}</td><td>{{ r.category }}</td>
                <td><qams-status-pill [status]="r.status" /></td>
                <td><span class="rpn" [class.high]="r.rpn > threshold">{{ r.rpn }}</span></td>
                <td>
                  @if (r.residualRpn !== null) {
                    <span class="rpn" [class.high]="r.residualRpn > threshold">{{ r.residualRpn }}</span>
                  } @else { — }
                </td>
                <td class="code">{{ org.branchName(r.branchId) || '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <qams-load-more [shown]="facade.list().length" [total]="facade.total()" [hasMore]="facade.hasMore()"
                      [loading]="facade.loading()" (more)="facade.loadMore()" />
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('risk.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .form { margin-bottom: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    .rpn { font-weight: 700; }
    .rpn.high { color: var(--nt-red, #b42318); }
    button, select { width: auto; }
  `]
})
export class RiskListComponent implements OnInit {
  readonly facade = inject(RiskFacade);
  readonly i18n = inject(I18nService);
  readonly org = inject(OrgDataService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = ['Identified', 'Mitigating', 'Closed'];
  readonly threshold = HIGH_RESIDUAL_RPN_THRESHOLD;
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
    return this.facade.list().filter((r) =>
      (!branch || r.branchId === branch)
      && (!q || `${r.riskRef} ${r.title} ${r.category} ${r.status}`.toLowerCase().includes(q)));
  });

  /** Live statistics computed from the real register. */
  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('stat.open'), value: all.filter((r) => r.status !== 'Closed').length, tone: 'blue' },
      { label: this.i18n.t('stat.mitigating'), value: all.filter((r) => r.status === 'Mitigating').length, tone: 'gold' },
      { label: this.i18n.t('stat.highRpn'), value: all.filter((r) => (r.residualRpn !== null ? r.residualRpn > 12 : r.rpn > 12)).length, tone: 'red' },
      { label: this.i18n.t('stat.closed'), value: all.filter((r) => r.status === 'Closed').length, tone: 'green' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    category: ['Operational', [Validators.required]],
    likelihood: [3, [Validators.required, Validators.min(1), Validators.max(5)]],
    impact: [3, [Validators.required, Validators.min(1), Validators.max(5)]],
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

  async assess(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.assess({
      ...raw,
      branchId: raw.branchId || null,
      departmentId: raw.departmentId || null,
    });
    if (id) { this.cancel(); void this.router.navigate(['/risks', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ category: 'Operational', likelihood: 3, impact: 3 });
  }

  open(id: string): void { void this.router.navigate(['/risks', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/risks']); }
}
