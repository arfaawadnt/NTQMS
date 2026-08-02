import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { ReferenceIntervalFacade } from './reference-interval.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { ReferenceIntervalListItem } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { ExportColumn, ExportMenuComponent } from '../../shared/ui/export-menu.component';

/** Reference-interval verification register (CLSI EP28): transference of claimed intervals. */
@Component({
    selector: 'qams-reference-interval-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DecimalPipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent, ExportMenuComponent],
    template: `
    <qams-page-header [title]="i18n.t('ri.title')" [subtitle]="i18n.t('ri.subtitle')">
      <qams-export-menu [title]="i18n.t('ri.title')" [stats]="stats()" [columns]="exportColumns"
                        [rows]="filtered()" [filtersSummary]="filtersSummary()" />
      @if (perms.can('analytical-quality.create')) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('ri.new') }}</button>
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

    <qams-drawer [open]="showForm()" [title]="i18n.t('ri.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('qc.analyte') }}</label>
        <input formControlName="analyte" />
        <label>{{ i18n.t('mu.unit') }}</label>
        <input formControlName="unit" [placeholder]="i18n.t('mu.unitHint')" />
        <label>{{ i18n.t('ri.population') }}</label>
        <input formControlName="population" [placeholder]="i18n.t('ri.populationHint')" />
        <label>{{ i18n.t('ri.source') }}</label>
        <input formControlName="source" [placeholder]="i18n.t('ri.sourceHint')" />
        <div class="pair">
          <div><label>{{ i18n.t('ri.lower') }}</label><input type="number" step="any" formControlName="claimedLower" /></div>
          <div><label>{{ i18n.t('ri.upper') }}</label><input type="number" step="any" formControlName="claimedUpper" /></div>
        </div>
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
      <p class="muted">{{ i18n.t('ri.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('qc.analyte') }}</th><th>{{ i18n.t('ri.population') }}</th>
            <th>{{ i18n.t('ri.interval') }}</th><th>{{ i18n.t('ri.outside') }}</th><th>{{ i18n.t('val.verdict') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (s of filtered(); track s.id) {
              <tr class="clickable" (click)="open(s.id)">
                <td class="code">{{ s.studyRef }}</td>
                <td>{{ s.analyte }}</td>
                <td class="muted">{{ s.population }}</td>
                <td>{{ s.claimedLower | number:'1.0-3' }} – {{ s.claimedUpper | number:'1.0-3' }}</td>
                <td>{{ s.outsideCount !== null ? s.outsideCount + ' / ' + (s.allowedOutside ?? 0) : '—' }}</td>
                <td>
                  @if (s.verdict === 'Verified') { <qams-status-pill status="Verified" /> }
                  @else if (s.verdict === 'Rejected') { <qams-status-pill status="Rejected" /> }
                  @else { — }
                </td>
                <td><qams-status-pill [status]="s.state" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('ri.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
  `]
})
export class ReferenceIntervalListComponent implements OnInit {
  readonly facade = inject(ReferenceIntervalFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly states = ['DataEntry', 'Calculated', 'SignedOff'];
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly stateFilter = signal('');
  readonly search = signal('');

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.facade.list().filter((s) =>
      !q || `${s.studyRef} ${s.analyte} ${s.population} ${s.state} ${s.verdict ?? ''}`.toLowerCase().includes(q));
  });

  /** Export columns — the printed grid mirrors the on-screen table. */
  readonly exportColumns: ExportColumn<ReferenceIntervalListItem>[] = [
    { header: this.i18n.t('mu.ref'), cell: (s) => s.studyRef },
    { header: this.i18n.t('qc.analyte'), cell: (s) => s.analyte },
    { header: this.i18n.t('ri.population'), cell: (s) => s.population },
    { header: this.i18n.t('ri.interval'), cell: (s) => `${s.claimedLower} – ${s.claimedUpper}` },
    { header: this.i18n.t('ri.outside'), cell: (s) => s.outsideCount !== null ? `${s.outsideCount} / ${s.allowedOutside ?? 0}` : '—' },
    { header: this.i18n.t('val.verdict'), cell: (s) => s.verdict ?? '—' },
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
      { label: this.i18n.t('ri.verified'), value: all.filter((s) => s.verdict === 'Verified').length, tone: 'green' },
      { label: this.i18n.t('ri.rejected'), value: all.filter((s) => s.verdict === 'Rejected').length, tone: 'red' },
      { label: this.i18n.t('mc.signedOff'), value: all.filter((s) => s.state === 'SignedOff').length, tone: 'teal' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    analyte: ['', [Validators.required, Validators.maxLength(200)]],
    unit: ['', [Validators.maxLength(50)]],
    population: ['', [Validators.required, Validators.maxLength(150)]],
    source: ['', [Validators.required, Validators.maxLength(300)]],
    claimedLower: [null as number | null, [Validators.required]],
    claimedUpper: [null as number | null, [Validators.required]],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.stateFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.stateFilter() || undefined);
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.create({
      ...raw,
      claimedLower: raw.claimedLower!,
      claimedUpper: raw.claimedUpper!,
    });
    if (id) { this.cancel(); void this.router.navigate(['/reference-intervals', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset();
  }

  open(id: string): void { void this.router.navigate(['/reference-intervals', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/reference-intervals']); }
}
