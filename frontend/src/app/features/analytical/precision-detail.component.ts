import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PrecisionFacade } from './precision.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { BulkImportResult } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';
import { CsvColumn, CsvImportComponent } from '../../shared/ui/csv-import.component';

/**
 * Imprecision workspace (CLSI EP05): run-grouped replicate entry, the derived
 * variance components (repeatability / between-run / within-lab) shown as SD +
 * CV cards with claim verdicts, a stacked variance-component bar, and the
 * per-run means table. Statistics come from the backend ANOVA.
 */
@Component({
    selector: 'qams-precision-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, CsvImportComponent],
    template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.studyRef + ' — ' + s.analyte" [subtitle]="s.level">
        <a routerLink="/precision-studies" class="ghost-link">← {{ i18n.t('prc.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.state" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.state" /></div>
        <div><span class="muted">{{ i18n.t('prc.runs') }}</span> {{ s.runs.length }}</div>
        <div><span class="muted">{{ i18n.t('prc.replicates') }}</span> {{ s.measurements.length }}</div>
        @if (s.grandMean !== null) { <div><span class="muted">{{ i18n.t('prc.grandMean') }}</span> <b>{{ s.grandMean | number:'1.0-4' }} {{ s.unit }}</b></div> }
        @if (s.signedOffAtUtc) { <div><span class="muted">{{ i18n.t('val.signedOff') }}</span> {{ s.signedOffAtUtc | date:'medium' }}</div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      @if (s.withinLabSd !== null) {
        <div class="components">
          <div class="comp card">
            <span class="muted">{{ i18n.t('prc.repeatability') }}</span>
            <b>{{ s.repeatabilityCvPct | number:'1.1-2' }}%</b>
            <span class="sub">SD {{ s.repeatabilitySd | number:'1.0-4' }} {{ s.unit }}</span>
            @if (s.meetsRepeatabilityClaim !== null) {
              <span class="claim" [class.ok]="s.meetsRepeatabilityClaim" [class.no]="!s.meetsRepeatabilityClaim">
                {{ i18n.t('prc.claim') }} {{ s.claimedRepeatabilityCvPct }}% · {{ s.meetsRepeatabilityClaim ? i18n.t('equip.pass') : i18n.t('equip.fail') }}
              </span>
            }
          </div>
          <div class="comp card">
            <span class="muted">{{ i18n.t('prc.betweenRun') }}</span>
            <b>{{ s.betweenRunCvPct | number:'1.1-2' }}%</b>
            <span class="sub">SD {{ s.betweenRunSd | number:'1.0-4' }} {{ s.unit }}</span>
          </div>
          <div class="comp card highlight">
            <span class="muted">{{ i18n.t('prc.withinLab') }}</span>
            <b>{{ s.withinLabCvPct | number:'1.1-2' }}%</b>
            <span class="sub">SD {{ s.withinLabSd | number:'1.0-4' }} {{ s.unit }}</span>
            @if (s.meetsWithinLabClaim !== null) {
              <span class="claim" [class.ok]="s.meetsWithinLabClaim" [class.no]="!s.meetsWithinLabClaim">
                {{ i18n.t('prc.claim') }} {{ s.claimedWithinLabCvPct }}% · {{ s.meetsWithinLabClaim ? i18n.t('equip.pass') : i18n.t('equip.fail') }}
              </span>
            }
          </div>
        </div>

        <section class="card">
          <h3>{{ i18n.t('prc.varianceBreakdown') }}</h3>
          <div class="stackbar">
            <div class="seg rep" [style.width.%]="repShare()" [attr.title]="i18n.t('prc.repeatability')"></div>
            <div class="seg btw" [style.width.%]="btwShare()" [attr.title]="i18n.t('prc.betweenRun')"></div>
          </div>
          <div class="legend">
            <span class="k rep"></span>{{ i18n.t('prc.repeatability') }} ({{ repShare() | number:'1.0-0' }}%)
            <span class="k btw"></span>{{ i18n.t('prc.betweenRun') }} ({{ btwShare() | number:'1.0-0' }}%)
          </div>
          <p class="muted small">{{ i18n.t('prc.varianceNote') }}</p>
        </section>
      }

      @if (s.runs.length > 0) {
        <section class="card">
          <h3>{{ i18n.t('prc.runMeans') }}</h3>
          <table>
            <thead><tr><th>{{ i18n.t('prc.run') }}</th><th>n</th><th>{{ i18n.t('lin.mean') }}</th></tr></thead>
            <tbody>
              @for (r of s.runs; track r.runLabel) {
                <tr><td><b>{{ r.runLabel }}</b></td><td>{{ r.replicateCount }}</td><td>{{ r.mean | number:'1.0-4' }} {{ s.unit }}</td></tr>
              }
            </tbody>
          </table>
        </section>
      }

      <section class="card">
        <h3>{{ i18n.t('prc.replicateData') }} ({{ s.measurements.length }})</h3>
        @if (s.state !== 'SignedOff') {
          <form [formGroup]="entryForm" (ngSubmit)="add(s.id)">
            <div class="pair">
              <div><label>{{ i18n.t('prc.run') }}</label><input formControlName="runLabel" [placeholder]="i18n.t('prc.runHint')" /></div>
              <div><label>{{ i18n.t('lin.measured') }} ({{ s.unit }})</label><input type="number" step="any" formControlName="value" /></div>
            </div>
            <div class="hint">{{ i18n.t('prc.entryHint') }}</div>
            <button type="submit" [disabled]="entryForm.invalid">{{ i18n.t('prc.addReplicate') }}</button>
          </form>

          <button type="button" class="link toggle" (click)="showImport.set(!showImport())">
            {{ showImport() ? i18n.t('csv.hide') : i18n.t('csv.show') }}
          </button>
          @if (showImport()) {
            <qams-csv-import [columns]="importColumns" [result]="importResult()" [busy]="facade.loading()" (import)="importRows(s.id, $event)" />
          }
        }
        @if (s.measurements.length > 0) {
          <table class="mtable">
            <thead><tr><th>{{ i18n.t('prc.run') }}</th><th>{{ i18n.t('lin.measured') }}</th><th></th></tr></thead>
            <tbody>
              @for (m of s.measurements; track m.id) {
                <tr>
                  <td>{{ m.runLabel }}</td>
                  <td>{{ m.value | number:'1.0-4' }}</td>
                  <td>
                    @if (s.state !== 'SignedOff') {
                      <button class="link danger-link" type="button" (click)="facade.removeMeasurement(s.id, m.id)">✕</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>

      <section class="card">
        <h3>{{ i18n.t('val.workflow') }}</h3>
        <div class="actions">
          @if (s.state !== 'SignedOff') {
            <button (click)="facade.calculate(s.id)" [disabled]="runCount() < 2">{{ i18n.t('prc.calculate') }}</button>
            @if (runCount() < 2) { <span class="muted">{{ i18n.t('prc.minRuns') }}</span> }
          }
          @if (s.state === 'Calculated' && perms.can('analytical-quality.sign')) {
            <button (click)="facade.signOff(s.id)">{{ i18n.t('mc.signOff') }}</button>
          }
          @if (s.state === 'SignedOff') { <p class="muted">{{ i18n.t('mc.signedOffNote') }}</p> }
        </div>
      </section>

      <qams-audit-trail [subject]="s.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .components { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem; margin-bottom: 1rem; }
    .comp { display: flex; flex-direction: column; gap: 2px; padding: 14px 16px; }
    .comp b { font-size: 1.4rem; color: var(--nt-navy-deep); }
    .comp.highlight { border-inline-start: 4px solid var(--nt-blue); }
    .comp .sub { font-size: .72rem; color: var(--nt-grey-m); }
    .comp .claim { font-size: .72rem; margin-top: 4px; font-weight: 600; }
    .comp .claim.ok { color: var(--nt-green); } .comp .claim.no { color: var(--nt-red); }
    .stackbar { display: flex; height: 22px; border-radius: 5px; overflow: hidden; background: var(--nt-filter-grey); }
    .seg.rep { background: var(--nt-teal); } .seg.btw { background: var(--nt-blue); }
    .legend { font-size: 11px; color: var(--nt-grey-m); display: flex; gap: 14px; align-items: center; margin-top: 6px; }
    .k { display: inline-block; width: 12px; height: 12px; border-radius: 2px; margin-inline-end: 4px; vertical-align: middle; }
    .k.rep { background: var(--nt-teal); } .k.btw { background: var(--nt-blue); }
    .small { font-size: .75rem; margin-top: 6px; }
    section { margin-bottom: 1rem; }
    .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .mtable { margin-top: 1rem; }
    .danger-link { color: var(--nt-red); }
    .actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    .actions button { width: auto; }
    form { border-bottom: 1px solid var(--nt-border); padding-bottom: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 800px) { .components, .pair { grid-template-columns: 1fr; } }
  `]
})
export class PrecisionDetailComponent implements OnInit {
  readonly facade = inject(PrecisionFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound study id. */
  readonly id = input.required<string>();

  readonly flowSteps = ['DataEntry', 'Calculated', 'SignedOff'] as const;
  readonly item = this.facade.selected;

  readonly entryForm = this.fb.nonNullable.group({
    runLabel: ['', [Validators.required, Validators.maxLength(60)]],
    value: [null as number | null, [Validators.required]],
  });

  readonly runCount = computed(() =>
    new Set((this.item()?.measurements ?? []).map((m) => m.runLabel)).size);

  /** CSV importer state: run label, value. */
  readonly showImport = signal(false);
  readonly importResult = signal<BulkImportResult | null>(null);
  readonly importColumns: CsvColumn[] = [
    { label: this.i18n.t('prc.run'), numeric: false },
    { label: this.i18n.t('lin.measured'), numeric: true },
  ];

  async importRows(id: string, rows: string[][]): Promise<void> {
    const parsed = rows.map((r) => ({ runLabel: (r[0] ?? '').trim(), value: Number(r[1]) }));
    this.importResult.set(await this.facade.importMeasurements(id, parsed));
  }

  /** Variance shares (SD² proportions) for the stacked bar. */
  readonly repShare = computed(() => this.share((this.item()?.repeatabilitySd ?? 0)));
  readonly btwShare = computed(() => this.share((this.item()?.betweenRunSd ?? 0)));

  private share(sd: number): number {
    const s = this.item();
    const rep = (s?.repeatabilitySd ?? 0) ** 2;
    const btw = (s?.betweenRunSd ?? 0) ** 2;
    const total = rep + btw;
    if (total === 0) { return sd === (s?.repeatabilitySd ?? 0) ? 100 : 0; }
    return Math.round((sd * sd / total) * 100);
  }

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  async add(id: string): Promise<void> {
    if (this.entryForm.invalid) { return; }
    const raw = this.entryForm.getRawValue();
    await this.facade.addMeasurement(id, raw.runLabel.trim(), raw.value!);
    // Keep the run label so replicates of the same run enter quickly.
    this.entryForm.patchValue({ value: null });
  }
}
