import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterOutlet } from '@angular/router';
import { PtPlansFacade } from './pt-plans.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PtPlanListItem } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { ExportColumn, ExportMenuComponent } from '../../shared/ui/export-menu.component';

/** Annual PT/EQA plan register: one plan per year, coverage = fulfilled vs planned cycles (§7.7.2). */
@Component({
    selector: 'qams-pt-plan-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent, ExportMenuComponent],
    template: `
    <qams-page-header [title]="i18n.t('ptp.title')" [subtitle]="i18n.t('ptp.subtitle')">
      <qams-export-menu [title]="i18n.t('ptp.title')" [stats]="stats()" [columns]="exportColumns"
                        [rows]="facade.list()" [filtersSummary]="i18n.t('exp.allRecords')" />
      @if (perms.can('proficiency-testing.create')) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('ptp.new') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <qams-drawer [open]="showForm()" [title]="i18n.t('ptp.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('ptp.year') }}</label>
        <input type="number" min="2000" max="2100" formControlName="year" />
        <div class="hint">{{ i18n.t('ptp.yearHint') }}</div>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('qc.create') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('ptp.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('ptp.year') }}</th><th>{{ i18n.t('ptp.lines') }}</th>
            <th>{{ i18n.t('ptp.coverage') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (p of facade.list(); track p.id) {
              <tr class="clickable" (click)="open(p.id)">
                <td class="code">{{ p.planRef }}</td>
                <td><b>{{ p.year }}</b></td>
                <td>{{ p.itemCount }}</td>
                <td>
                  <div class="coverage">
                    <div class="bar"><div class="fill" [style.width.%]="coveragePct(p.fulfilledCycles, p.plannedCycles)"></div></div>
                    <span>{{ p.fulfilledCycles }}/{{ p.plannedCycles }}</span>
                  </div>
                </td>
                <td><qams-status-pill [status]="p.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('ptp.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button { width: auto; }
    .coverage { display: flex; align-items: center; gap: 8px; }
    .bar { width: 110px; height: 8px; border-radius: 4px; background: var(--nt-filter-grey); overflow: hidden; }
    .fill { height: 100%; background: var(--nt-teal); border-radius: 4px; }
  `]
})
export class PtPlanListComponent implements OnInit {
  readonly facade = inject(PtPlansFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    const planned = all.reduce((s, p) => s + p.plannedCycles, 0);
    const fulfilled = all.reduce((s, p) => s + p.fulfilledCycles, 0);
    return [
      { label: this.i18n.t('ptp.plans'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('ptp.approved'), value: all.filter((p) => p.status === 'Approved').length, tone: 'green' },
      { label: this.i18n.t('ptp.plannedCycles'), value: planned, tone: 'blue' },
      { label: this.i18n.t('ptp.fulfilledCycles'), value: fulfilled, tone: 'teal' },
    ];
  });

  /** Export columns — the printed grid mirrors the on-screen table. */
  readonly exportColumns: ExportColumn<PtPlanListItem>[] = [
    { header: this.i18n.t('mu.ref'), cell: (p) => p.planRef },
    { header: this.i18n.t('ptp.year'), cell: (p) => `${p.year}` },
    { header: this.i18n.t('ptp.lines'), cell: (p) => `${p.itemCount}` },
    { header: this.i18n.t('ptp.coverage'), cell: (p) => `${p.fulfilledCycles}/${p.plannedCycles}` },
    { header: this.i18n.t('nc.status'), cell: (p) => p.status },
  ];

  readonly form = this.fb.nonNullable.group({
    year: [new Date().getFullYear(), [Validators.required, Validators.min(2000), Validators.max(2100)]],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  coveragePct(fulfilled: number, planned: number): number {
    return planned === 0 ? 0 : Math.min(100, Math.round((fulfilled / planned) * 100));
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.create(this.form.getRawValue().year);
    if (id) { this.cancel(); void this.router.navigate(['/pt-plans', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ year: new Date().getFullYear() });
  }

  open(id: string): void { void this.router.navigate(['/pt-plans', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/pt-plans']); }
}
