import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { PrecisionFacade } from './precision.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PrecisionListItem } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { ExportColumn, ExportMenuComponent } from '../../shared/ui/export-menu.component';

/** One concentration level of a new study, with that level's manufacturer claims. */
type LevelForm = FormGroup<{
  level: FormControl<string>;
  claimedRepeatabilityCvPct: FormControl<number | null>;
  claimedWithinLabCvPct: FormControl<number | null>;
  claimedReproducibilityCvPct: FormControl<number | null>;
}>;

/**
 * Imprecision-study register (CLSI EP05 / EP15): within-run / between-run /
 * between-day / within-lab precision. A new study may declare several
 * concentration levels — the backend then creates one linked study per level
 * (a series), each its own signed record, verifying claims at α / L.
 */
@Component({
    selector: 'qams-precision-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DecimalPipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent, ExportMenuComponent],
    template: `
    <qams-page-header [title]="i18n.t('prc.title')" [subtitle]="i18n.t('prc.subtitle')">
      <qams-export-menu [title]="i18n.t('prc.title')" [stats]="stats()" [columns]="exportColumns"
                        [rows]="filtered()" [filtersSummary]="filtersSummary()" />
      @if (perms.can('analytical-quality.create')) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('prc.new') }}</button>
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

    <qams-drawer [open]="showForm()" [title]="i18n.t('prc.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('qc.analyte') }}</label>
        <input formControlName="analyte" />
        <label>{{ i18n.t('mu.unit') }}</label>
        <input formControlName="unit" [placeholder]="i18n.t('mu.unitHint')" />
        <fieldset formArrayName="levels">
          <legend>{{ i18n.t('prc.levels') }}</legend>
          @for (lv of levels.controls; track lv; let i = $index) {
            <!-- One block per level: a full-width name, then the three optional claims, each with a visible label. -->
            <div class="level" [formGroupName]="i">
              <div class="level-head">
                <label [for]="'prc-level-' + i">{{ i18n.t('mu.level') }} {{ i + 1 }}</label>
                @if (levels.length > 1) {
                  <button type="button" class="link" (click)="removeLevel(i)" [attr.aria-label]="i18n.t('prc.removeLevel')">✕</button>
                }
              </div>
              <input [id]="'prc-level-' + i" formControlName="level" [placeholder]="i18n.t('prc.levelHint')" />
              <span class="claims-title">{{ i18n.t('prc.claimsOptional') }}</span>
              <div class="claims">
                <label>{{ i18n.t('prc.repeatabilityCv') }} (%)
                  <input type="number" step="any" min="0" formControlName="claimedRepeatabilityCvPct" placeholder="%" />
                </label>
                <label>{{ i18n.t('prc.withinLabCv') }} (%)
                  <input type="number" step="any" min="0" formControlName="claimedWithinLabCvPct" placeholder="%" />
                </label>
                <label>{{ i18n.t('prc.reproducibilityCv') }} (%)
                  <input type="number" step="any" min="0" formControlName="claimedReproducibilityCvPct" placeholder="%" />
                </label>
              </div>
            </div>
          }
          @if (levels.length < maxLevels) {
            <button type="button" class="link" (click)="addLevel()">+ {{ i18n.t('prc.addLevel') }}</button>
          }
          <p class="helper">{{ levels.length > 1 ? i18n.t('prc.seriesHint') : i18n.t('prc.singleLevelHint') }}</p>
        </fieldset>
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
      <p class="muted">{{ i18n.t('prc.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('qc.analyte') }}</th><th>{{ i18n.t('mu.level') }}</th>
            <th>{{ i18n.t('prc.repeatabilityCv') }}</th><th>{{ i18n.t('prc.withinLabCv') }}</th>
            <th>{{ i18n.t('val.verdict') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (s of filtered(); track s.id) {
              <tr class="clickable" (click)="open(s.id)">
                <td class="code">{{ s.studyRef }}@if (s.seriesRef) { <span class="series">{{ s.seriesRef }}</span> }</td>
                <td>{{ s.analyte }}</td>
                <td class="muted">{{ s.level }}</td>
                <td>{{ s.repeatabilityCvPct !== null ? (s.repeatabilityCvPct | number:'1.1-2') + '%' : '—' }}</td>
                <td>{{ s.withinLabCvPct !== null ? (s.withinLabCvPct | number:'1.1-2') + '%' : '—' }}</td>
                <td>
                  @if (s.meetsWithinLabClaim === true) { <qams-status-pill status="Pass" /> }
                  @else if (s.meetsWithinLabClaim === false) { <qams-status-pill status="Failed" /> }
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
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('prc.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
    fieldset { border: 1px solid var(--nt-border); border-radius: 6px; padding: .5rem .75rem; margin-top: .75rem; }
    .level { border: 1px solid var(--nt-border); border-radius: 6px; padding: .5rem .6rem; margin-bottom: .6rem; }
    .level-head { display: flex; justify-content: space-between; align-items: center; }
    .level-head label { font-weight: 600; margin: 0; }
    .claims-title { display: block; font-size: .72rem; color: var(--nt-ink-neutral); margin: .45rem 0 .2rem; }
    .claims { display: grid; grid-template-columns: repeat(3, 1fr); gap: .5rem; align-items: end; }
    .claims label { font-size: .75rem; margin: 0; line-height: 1.25; }
    .claims input { margin-top: .2rem; }
    @media (max-width: 600px) { .claims { grid-template-columns: 1fr; } }
    .series { display: block; font-size: .7rem; color: var(--nt-ink-neutral); }
  `]
})
export class PrecisionListComponent implements OnInit {
  readonly facade = inject(PrecisionFacade);
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
      !q || `${s.studyRef} ${s.analyte} ${s.level} ${s.state}`.toLowerCase().includes(q));
  });

  /** Export columns — the printed grid mirrors the on-screen table. */
  readonly exportColumns: ExportColumn<PrecisionListItem>[] = [
    { header: this.i18n.t('mu.ref'), cell: (s) => s.studyRef },
    { header: this.i18n.t('qc.analyte'), cell: (s) => s.analyte },
    { header: this.i18n.t('mu.level'), cell: (s) => s.level },
    { header: this.i18n.t('prc.repeatabilityCv'), cell: (s) => s.repeatabilityCvPct !== null ? `${s.repeatabilityCvPct.toFixed(2)}%` : '—' },
    { header: this.i18n.t('prc.withinLabCv'), cell: (s) => s.withinLabCvPct !== null ? `${s.withinLabCvPct.toFixed(2)}%` : '—' },
    { header: this.i18n.t('val.verdict'), cell: (s) => s.meetsWithinLabClaim === true ? 'Pass' : s.meetsWithinLabClaim === false ? 'Failed' : '—' },
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
      { label: this.i18n.t('prc.meetsClaim'), value: all.filter((s) => s.meetsWithinLabClaim === true).length, tone: 'green' },
      { label: this.i18n.t('prc.failsClaim'), value: all.filter((s) => s.meetsWithinLabClaim === false).length, tone: 'red' },
      { label: this.i18n.t('mc.signedOff'), value: all.filter((s) => s.state === 'SignedOff').length, tone: 'teal' },
    ];
  });

  /** Mirrors the backend series bound (PrecisionStudy.MaximumSeriesLevels). */
  readonly maxLevels = 10;

  readonly form = this.fb.nonNullable.group({
    analyte: ['', [Validators.required, Validators.maxLength(200)]],
    unit: ['', [Validators.maxLength(50)]],
    levels: this.fb.array<LevelForm>([this.newLevel()]),
  });

  get levels(): FormArray<LevelForm> { return this.form.controls.levels; }

  addLevel(): void { this.levels.push(this.newLevel()); }

  removeLevel(index: number): void { this.levels.removeAt(index); }

  private newLevel(): LevelForm {
    return this.fb.nonNullable.group({
      level: ['', [Validators.maxLength(100)]],
      claimedRepeatabilityCvPct: this.fb.control<number | null>(null, [Validators.min(0.0001)]),
      claimedWithinLabCvPct: this.fb.control<number | null>(null, [Validators.min(0.0001)]),
      claimedReproducibilityCvPct: this.fb.control<number | null>(null, [Validators.min(0.0001)]),
    });
  }

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.stateFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.stateFilter() || undefined);
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const levels = raw.levels.map((l) => ({ ...l, level: l.level.trim() }));
    // One level is a stand-alone study; several become a linked series.
    const id = levels.length === 1
      ? await this.facade.create({ analyte: raw.analyte, unit: raw.unit, ...levels[0] })
      : await this.facade.createSeries({ analyte: raw.analyte, unit: raw.unit, levels });
    if (id) { this.cancel(); void this.router.navigate(['/precision-studies', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset();
    while (this.levels.length > 1) { this.levels.removeAt(this.levels.length - 1); }
  }

  open(id: string): void { void this.router.navigate(['/precision-studies', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/precision-studies']); }
}
