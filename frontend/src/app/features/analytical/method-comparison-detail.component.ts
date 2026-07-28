import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MethodComparisonFacade } from './method-comparison.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { BulkImportResult, MethodComparisonDetail } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';
import { CsvColumn, CsvImportComponent } from '../../shared/ui/csv-import.component';

interface Pt { x: number; y: number; }

/**
 * Method-comparison workspace (CLSI EP09): paired-data entry, the derived
 * regression fits (Deming + Passing–Bablok) and Bland–Altman agreement, plotted
 * as a scatter with the identity + fit lines and a difference plot with the
 * mean-bias and 95% limit-of-agreement bands. Statistics come from the backend.
 */
@Component({
    selector: 'qams-method-comparison-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, CsvImportComponent],
    template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.studyRef + ' — ' + s.analyte" [subtitle]="s.testMethod + ' vs ' + s.referenceMethod">
        <a routerLink="/method-comparisons" class="ghost-link">← {{ i18n.t('mc.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.state" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.state" /></div>
        <div><span class="muted">n</span> {{ s.pairCount ?? s.pairs.length }}
          @if (!s.meetsRecommendedPower) { <span class="warn" [title]="i18n.t('mc.powerHint')"> ⚠ &lt; 40</span> }
        </div>
        @if (s.pearsonR !== null) { <div><span class="muted">Pearson r</span> <b>{{ s.pearsonR | number:'1.3-4' }}</b></div> }
        @if (s.signedOffAtUtc) { <div><span class="muted">{{ i18n.t('val.signedOff') }}</span> {{ s.signedOffAtUtc | date:'medium' }}</div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      @if (s.demingSlope !== null) {
        <div class="results card">
          <div class="fit">
            <span class="muted">{{ i18n.t('mc.deming') }}</span>
            <b>y = {{ s.demingSlope | number:'1.3-4' }}x {{ sign(s.demingIntercept) }} {{ absv(s.demingIntercept) | number:'1.3-4' }}</b>
          </div>
          <div class="fit">
            <span class="muted">{{ i18n.t('mc.passingBablok') }}</span>
            <b>y = {{ s.passingBablokSlope | number:'1.3-4' }}x {{ sign(s.passingBablokIntercept) }} {{ absv(s.passingBablokIntercept) | number:'1.3-4' }}</b>
          </div>
          <div class="fit">
            <span class="muted">{{ i18n.t('mc.meanBias') }}</span>
            <b>{{ s.meanBias | number:'1.3-4' }} {{ s.unit }}</b>
            <span class="muted small">95% LoA {{ s.limitOfAgreementLower | number:'1.2-3' }} … {{ s.limitOfAgreementUpper | number:'1.2-3' }}</span>
          </div>
        </div>

        <div class="plots">
          <section class="card">
            <h3>{{ i18n.t('mc.scatter') }}</h3>
            <svg [attr.viewBox]="'0 0 ' + W + ' ' + H" class="plot">
              <line [attr.x1]="PAD" [attr.y1]="H - PAD" [attr.x2]="W - 8" [attr.y2]="H - PAD" class="axis" />
              <line [attr.x1]="PAD" [attr.y1]="8" [attr.x2]="PAD" [attr.y2]="H - PAD" class="axis" />
              <!-- identity y = x -->
              <line [attr.x1]="sx(scatterMin())" [attr.y1]="sy(scatterMin())" [attr.x2]="sx(scatterMax())" [attr.y2]="sy(scatterMax())" class="identity" />
              <!-- Deming fit -->
              <line [attr.x1]="sx(scatterMin())" [attr.y1]="sy(s.demingSlope! * scatterMin() + s.demingIntercept!)"
                    [attr.x2]="sx(scatterMax())" [attr.y2]="sy(s.demingSlope! * scatterMax() + s.demingIntercept!)" class="fitline deming" />
              @for (p of s.pairs; track p.id) {
                <circle [attr.cx]="sx(p.referenceValue)" [attr.cy]="sy(p.testValue)" r="3" class="dot" />
              }
              <text [attr.x]="W / 2" [attr.y]="H - 4" class="axlabel">{{ s.referenceMethod }} (X)</text>
              <text [attr.x]="-(H / 2)" y="12" transform="rotate(-90)" class="axlabel">{{ s.testMethod }} (Y)</text>
            </svg>
            <div class="legend"><span class="k identity"></span>{{ i18n.t('mc.identity') }} <span class="k deming"></span>{{ i18n.t('mc.deming') }}</div>
          </section>

          <section class="card">
            <h3>{{ i18n.t('mc.blandAltman') }}</h3>
            <svg [attr.viewBox]="'0 0 ' + W + ' ' + H" class="plot">
              <line [attr.x1]="PAD" [attr.y1]="H - PAD" [attr.x2]="W - 8" [attr.y2]="H - PAD" class="axis" />
              <line [attr.x1]="PAD" [attr.y1]="8" [attr.x2]="PAD" [attr.y2]="H - PAD" class="axis" />
              <!-- bias + LoA bands -->
              <line [attr.x1]="PAD" [attr.y1]="by(s.meanBias!)" [attr.x2]="W - 8" [attr.y2]="by(s.meanBias!)" class="fitline deming" />
              <line [attr.x1]="PAD" [attr.y1]="by(s.limitOfAgreementUpper!)" [attr.x2]="W - 8" [attr.y2]="by(s.limitOfAgreementUpper!)" class="loa" />
              <line [attr.x1]="PAD" [attr.y1]="by(s.limitOfAgreementLower!)" [attr.x2]="W - 8" [attr.y2]="by(s.limitOfAgreementLower!)" class="loa" />
              @for (p of s.pairs; track p.id) {
                <circle [attr.cx]="bx((p.referenceValue + p.testValue) / 2)" [attr.cy]="by(p.testValue - p.referenceValue)" r="3" class="dot" />
              }
              <text [attr.x]="W / 2" [attr.y]="H - 4" class="axlabel">{{ i18n.t('mc.mean') }}</text>
              <text [attr.x]="-(H / 2)" y="12" transform="rotate(-90)" class="axlabel">{{ i18n.t('mc.difference') }}</text>
            </svg>
            <div class="legend"><span class="k deming"></span>{{ i18n.t('mc.meanBias') }} <span class="k loa"></span>95% LoA</div>
          </section>
        </div>
      }

      <section class="card">
        <h3>{{ i18n.t('mc.pairs') }} ({{ s.pairs.length }})</h3>
        @if (s.pairs.length === 0) { <p class="muted">{{ i18n.t('mc.noPairs') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('mc.sample') }}</th><th>{{ s.referenceMethod }} (X)</th><th>{{ s.testMethod }} (Y)</th>
              <th>{{ i18n.t('mc.difference') }}</th><th></th>
            </tr></thead>
            <tbody>
              @for (p of s.pairs; track p.id) {
                <tr>
                  <td class="muted">{{ p.sampleId ?? '—' }}</td>
                  <td>{{ p.referenceValue | number:'1.0-3' }}</td>
                  <td>{{ p.testValue | number:'1.0-3' }}</td>
                  <td [class.bad]="(p.testValue - p.referenceValue) !== 0">{{ (p.testValue - p.referenceValue) | number:'1.0-3' }}</td>
                  <td>
                    @if (s.state !== 'SignedOff') {
                      <button class="link danger-link" type="button" (click)="facade.removePair(s.id, p.id)">✕</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
        @if (s.state !== 'SignedOff') {
          <form [formGroup]="pairForm" (ngSubmit)="addPair(s.id)">
            <div class="trio">
              <div><label>{{ i18n.t('mc.sample') }}</label><input formControlName="sampleId" [placeholder]="i18n.t('common.optional')" /></div>
              <div><label>{{ s.referenceMethod }} (X)</label><input type="number" step="any" formControlName="referenceValue" /></div>
              <div><label>{{ s.testMethod }} (Y)</label><input type="number" step="any" formControlName="testValue" /></div>
            </div>
            <button type="submit" [disabled]="pairForm.invalid">{{ i18n.t('mc.addPair') }}</button>
          </form>

          <button type="button" class="link toggle" (click)="showImport.set(!showImport())">
            {{ showImport() ? i18n.t('csv.hide') : i18n.t('csv.show') }}
          </button>
          @if (showImport()) {
            <qams-csv-import [columns]="importColumns" [result]="importResult()" [busy]="facade.loading()" (import)="importPairs(s.id, $event)" />
          }
        }
      </section>

      <section class="card">
        <h3>{{ i18n.t('val.workflow') }}</h3>
        <div class="actions">
          @if (s.state !== 'SignedOff') {
            <button (click)="facade.calculate(s.id)" [disabled]="s.pairs.length < 2">{{ i18n.t('mc.calculate') }}</button>
          }
          @if (s.state === 'Calculated' && perms.canApprove()) {
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
    .warn { color: var(--nt-red); font-weight: 600; }
    .results { display: flex; flex-wrap: wrap; gap: 1.75rem; margin-bottom: 1rem; }
    .fit { display: flex; flex-direction: column; gap: 2px; }
    .fit .muted { font-size: .75rem; }
    .small { font-size: .72rem; }
    .plots { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; margin-bottom: 1rem; }
    .plot { width: 100%; height: auto; }
    .axis { stroke: var(--nt-border); stroke-width: 1; }
    .identity { stroke: var(--nt-grey-m); stroke-width: 1.5; stroke-dasharray: 4 3; }
    .fitline.deming { stroke: var(--nt-blue); stroke-width: 2; }
    .loa { stroke: var(--nt-red); stroke-width: 1.4; stroke-dasharray: 5 3; }
    .dot { fill: var(--nt-teal); opacity: .8; }
    .axlabel { font-size: 10px; fill: var(--nt-grey-m); text-anchor: middle; }
    .legend { font-size: 11px; color: var(--nt-grey-m); display: flex; gap: 14px; align-items: center; margin-top: 4px; }
    .k { display: inline-block; width: 14px; height: 3px; margin-inline-end: 4px; vertical-align: middle; }
    .k.identity { background: var(--nt-grey-m); } .k.deming { background: var(--nt-blue); } .k.loa { background: var(--nt-red); }
    section { margin-bottom: 1rem; }
    .trio { display: grid; grid-template-columns: 2fr 1fr 1fr; gap: 1rem; }
    .bad { color: var(--nt-slate); }
    .danger-link { color: var(--nt-red); }
    .actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    .actions button { width: auto; }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 900px) { .plots, .trio { grid-template-columns: 1fr; } }
  `]
})
export class MethodComparisonDetailComponent implements OnInit {
  readonly facade = inject(MethodComparisonFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound study id. */
  readonly id = input.required<string>();

  readonly flowSteps = ['DataEntry', 'Calculated', 'SignedOff'] as const;
  readonly item = this.facade.selected;

  // Plot geometry.
  readonly W = 360;
  readonly H = 300;
  readonly PAD = 34;

  readonly pairForm = this.fb.nonNullable.group({
    sampleId: [''],
    referenceValue: [null as number | null, [Validators.required, Validators.min(0.0001)]],
    testValue: [null as number | null, [Validators.required, Validators.min(0.0001)]],
  });

  /** CSV importer state: X, Y, optional sample id. */
  readonly showImport = signal(false);
  readonly importResult = signal<BulkImportResult | null>(null);
  readonly importColumns: CsvColumn[] = [
    { label: this.i18n.t('mc.referenceMethod'), numeric: true },
    { label: this.i18n.t('mc.testMethod'), numeric: true },
    { label: this.i18n.t('mc.sample'), numeric: false, optional: true },
  ];

  async importPairs(id: string, rows: string[][]): Promise<void> {
    const parsed = rows.map((r) => ({
      referenceValue: Number(r[0]),
      testValue: Number(r[1]),
      sampleId: (r[2] ?? '').trim() || null,
    }));
    this.importResult.set(await this.facade.importPairs(id, parsed));
  }

  // Scatter axis range (shared X/Y so the identity line is a true diagonal).
  readonly scatterMin = computed(() => {
    const s = this.item(); if (!s || s.pairs.length === 0) { return 0; }
    const vals = s.pairs.flatMap((p) => [p.referenceValue, p.testValue]);
    return Math.min(...vals);
  });
  readonly scatterMax = computed(() => {
    const s = this.item(); if (!s || s.pairs.length === 0) { return 1; }
    const vals = s.pairs.flatMap((p) => [p.referenceValue, p.testValue]);
    const max = Math.max(...vals);
    return max === this.scatterMin() ? max + 1 : max;
  });

  // Bland–Altman axis ranges.
  private readonly baMeans = computed(() => (this.item()?.pairs ?? []).map((p) => (p.referenceValue + p.testValue) / 2));
  private readonly baDiffs = computed(() => (this.item()?.pairs ?? []).map((p) => p.testValue - p.referenceValue));

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  sign(v: number | null): string { return (v ?? 0) >= 0 ? '+' : '−'; }
  absv(v: number | null): number { return Math.abs(v ?? 0); }

  /** Scatter: value → pixel (shared span). */
  sx(v: number): number { return this.map(v, this.scatterMin(), this.scatterMax(), this.PAD, this.W - 8); }
  sy(v: number): number { return this.map(v, this.scatterMin(), this.scatterMax(), this.H - this.PAD, 8); }

  /** Bland–Altman: mean → x, difference → y (padded so bias/LoA bands sit inside). */
  bx(v: number): number {
    const means = this.baMeans();
    return this.map(v, Math.min(...means), Math.max(...means), this.PAD, this.W - 8);
  }
  by(v: number): number {
    const s = this.item();
    const diffs = this.baDiffs();
    const candidates = [...diffs, s?.limitOfAgreementLower ?? 0, s?.limitOfAgreementUpper ?? 0];
    const min = Math.min(...candidates);
    const max = Math.max(...candidates);
    const span = max === min ? 1 : (max - min) * 0.1;
    return this.map(v, min - span, max + span, this.H - this.PAD, 8);
  }

  private map(v: number, lo: number, hi: number, plo: number, phi: number): number {
    if (hi === lo) { return (plo + phi) / 2; }
    return plo + ((v - lo) / (hi - lo)) * (phi - plo);
  }

  async addPair(id: string): Promise<void> {
    if (this.pairForm.invalid) { return; }
    const raw = this.pairForm.getRawValue();
    await this.facade.addPair(id, raw.referenceValue!, raw.testValue!, raw.sampleId.trim() || null);
    this.pairForm.reset();
  }
}
