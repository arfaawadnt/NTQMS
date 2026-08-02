import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { CarryoverFacade } from './carryover.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { CarryoverListItem } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { ExportColumn, ExportMenuComponent } from '../../shared/ui/export-menu.component';

/** Carryover-study register (CLSI EP10-style): high→low sequence, percentage carryover. */
@Component({
    selector: 'qams-carryover-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DecimalPipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent, ExportMenuComponent],
    template: `
    <qams-page-header [title]="i18n.t('car.title')" [subtitle]="i18n.t('car.subtitle')">
      <qams-export-menu [title]="i18n.t('car.title')" [stats]="stats()" [columns]="exportColumns"
                        [rows]="filtered()" [filtersSummary]="filtersSummary()" />
      @if (perms.can('analytical-quality.create')) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('car.new') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" ratioFromFirst />

    <div class="filterbar card">
      <input class="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="i18n.t('common.search')" />
      <select [value]="stateFilter()" (change)="onFilter($event)" aria-label="State filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of states; track s) { <option [value]="s">{{ s }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="i18n.t('car.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('qc.analyte') }}</label>
        <input formControlName="analyte" />
        <label>{{ i18n.t('mu.unit') }}</label>
        <input formControlName="unit" [placeholder]="i18n.t('mu.unitHint')" />
        <label>{{ i18n.t('car.allowable') }}</label>
        <input type="number" step="any" formControlName="allowableCarryoverPct" />
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
      <p class="muted">{{ i18n.t('car.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('qc.analyte') }}</th>
            <th>{{ i18n.t('car.carryover') }}</th><th>{{ i18n.t('val.verdict') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (s of filtered(); track s.id) {
              <tr class="clickable" (click)="open(s.id)">
                <td class="code">{{ s.studyRef }}</td>
                <td>{{ s.analyte }}</td>
                <td>{{ s.carryoverPct !== null ? (s.carryoverPct | number:'1.2-4') + '%' : '—' }}</td>
                <td>
                  @if (s.passes === true) { <qams-status-pill status="Pass" /> }
                  @else if (s.passes === false) { <qams-status-pill status="Failed" /> }
                  @else { — }
                </td>
                <td><qams-status-pill [status]="s.state" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <qams-drawer [open]="detailOpen()" [title]="i18n.t('car.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
  `]
})
export class CarryoverListComponent implements OnInit {
  readonly facade = inject(CarryoverFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly states = ['DataEntry', 'Calculated', 'SignedOff'];
  readonly showForm = signal(false);
  readonly detailOpen = signal(false);
  readonly stateFilter = signal('');
  readonly search = signal('');

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.facade.list().filter((s) =>
      !q || `${s.studyRef} ${s.analyte} ${s.state}`.toLowerCase().includes(q));
  });

  /** Export columns — the printed grid mirrors the on-screen table. */
  readonly exportColumns: ExportColumn<CarryoverListItem>[] = [
    { header: this.i18n.t('mu.ref'), cell: (s) => s.studyRef },
    { header: this.i18n.t('qc.analyte'), cell: (s) => s.analyte },
    { header: this.i18n.t('car.carryover'), cell: (s) => s.carryoverPct !== null ? `${s.carryoverPct.toFixed(4)}%` : '—' },
    { header: this.i18n.t('val.verdict'), cell: (s) => s.passes === true ? 'Pass' : s.passes === false ? 'Failed' : '—' },
    { header: this.i18n.t('nc.status'), cell: (s) => s.state },
  ];

  /** The filter line printed on the document, mirroring the filter bar. */
  readonly filtersSummary = computed(() => {
    const parts: string[] = [];
    if (this.stateFilter()) { parts.push(this.stateFilter()); }
    if (this.search().trim()) { parts.push(`"${this.search().trim()}"`); }
    return parts.length ? parts.join(' · ') : this.i18n.t('exp.allRecords');
  });

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('equip.pass'), value: all.filter((s) => s.passes === true).length, tone: 'green' },
      { label: this.i18n.t('equip.fail'), value: all.filter((s) => s.passes === false).length, tone: 'red' },
      { label: this.i18n.t('mc.signedOff'), value: all.filter((s) => s.state === 'SignedOff').length, tone: 'teal' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    analyte: ['', [Validators.required, Validators.maxLength(200)]],
    unit: ['', [Validators.maxLength(50)]],
    allowableCarryoverPct: [1, [Validators.required, Validators.min(0)]],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.stateFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.stateFilter() || undefined);
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.create(this.form.getRawValue());
    if (id) { this.cancel(); void this.router.navigate(['/carryover-studies', id]); }
  }

  cancel(): void { this.showForm.set(false); this.form.reset({ analyte: '', unit: '', allowableCarryoverPct: 1 }); }

  open(id: string): void { void this.router.navigate(['/carryover-studies', id]); }

  closeDetail(): void { void this.router.navigate(['/carryover-studies']); }
}
