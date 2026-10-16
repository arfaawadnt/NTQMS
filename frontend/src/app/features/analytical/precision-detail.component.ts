import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { EsignCredentials, EsignDialogComponent } from '../../shared/ui/esign-dialog.component';
import { SignatureManifestComponent } from '../../shared/ui/signature-manifest.component';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PrecisionFacade } from './precision.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { AddPrecisionMeasurementRequest, BulkImportResult } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';
import { CsvColumn, CsvImportComponent } from '../../shared/ui/csv-import.component';

/** How an observed CV relates to its claim under EP15-A3. */
type ClaimVerdict = 'met' | 'withinLimit' | 'notVerified';

/**
 * Imprecision workspace (CLSI EP05 / EP15): replicate entry nested as
 * day → run, with the measurement conditions (operator, instrument, lots,
 * date) that show which intermediate-precision factors the study spans; the
 * backend-derived variance components (repeatability / between-run /
 * between-day / within-lab) as SD + CV cards with df, 95 % CIs and EP15
 * claim verdicts against the upper verification limit; the other levels of
 * the series; and PT/EQA scheme SDs as between-LABORATORY context — the lab
 * never computes reproducibility from its own data. Statistics are never
 * computed here.
 */
@Component({
    selector: 'qams-precision-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, CsvImportComponent, EsignDialogComponent, SignatureManifestComponent],
    template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.studyRef + ' — ' + s.analyte" [subtitle]="s.level">
        <a routerLink="/precision-studies" class="ghost-link">← {{ i18n.t('prc.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.state" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.state" /></div>
        @if (s.design) { <div><span class="muted">{{ i18n.t('prc.design') }}</span> {{ i18n.t('prc.design.' + s.design) }}</div> }
        @if (s.coverage.sites > 0) { <div><span class="muted">{{ i18n.t('prc.sites') }}</span> {{ s.coverage.sites }}</div> }
        @if (s.coverage.days > 0) { <div><span class="muted">{{ i18n.t('prc.days') }}</span> {{ s.coverage.days }}</div> }
        <div><span class="muted">{{ i18n.t('prc.runs') }}</span> {{ s.runs.length }}</div>
        <div><span class="muted">{{ i18n.t('prc.replicates') }}</span> {{ s.measurements.length }}</div>
        @if (s.grandMean !== null) { <div><span class="muted">{{ i18n.t('prc.grandMean') }}</span> <b>{{ s.grandMean | number:'1.0-4' }} {{ s.unit }}</b></div> }
        @if (s.seriesRef) { <div><span class="muted">{{ i18n.t('prc.series') }}</span> <span class="code">{{ s.seriesRef }}</span> ({{ s.seriesLevelCount }})</div> }
        @if (s.signedOffAtUtc) { <div><span class="muted">{{ i18n.t('val.signedOff') }}</span> {{ s.signedOffAtUtc | date:'medium' }}</div> }
      </div>
      @if (s.seriesMembers.length > 0) {
        <nav class="siblings" [attr.aria-label]="i18n.t('prc.otherLevels')">
          <span class="muted">{{ i18n.t('prc.otherLevels') }}:</span>
          @for (m of s.seriesMembers; track m.id) {
            <a [routerLink]="['/precision-studies', m.id]" (click)="facade.loadDetail(m.id)">{{ m.level || m.studyRef }}
              @if (m.withinLabCvPct !== null) { · {{ m.withinLabCvPct | number:'1.1-2' }}% }</a>
          }
        </nav>
      }
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      @if (s.withinLabSd !== null) {
        <div class="components">
          <div class="comp card">
            <span class="muted">{{ i18n.t('prc.repeatability') }}</span>
            <b>{{ s.repeatabilityCvPct | number:'1.1-2' }}%</b>
            <span class="sub">SD {{ s.repeatabilitySd | number:'1.0-4' }} {{ s.unit }} · df {{ s.repeatabilityDf }}</span>
            @if (s.repeatabilityCvLowerPct !== null) {
              <span class="sub">{{ i18n.t('prc.ci95') }} {{ s.repeatabilityCvLowerPct | number:'1.1-2' }}–{{ s.repeatabilityCvUpperPct | number:'1.1-2' }}%</span>
            }
            @if (s.claimedRepeatabilityCvPct !== null && s.repeatabilityVerificationLimitCvPct !== null) {
              <span class="claim" [class]="verdict(s.repeatabilityCvPct, s.claimedRepeatabilityCvPct, s.meetsRepeatabilityClaim)">
                {{ i18n.t('prc.claim') }} {{ s.claimedRepeatabilityCvPct }}% · UVL {{ s.repeatabilityVerificationLimitCvPct | number:'1.1-2' }}% ·
                {{ i18n.t('prc.verdict.' + verdict(s.repeatabilityCvPct, s.claimedRepeatabilityCvPct, s.meetsRepeatabilityClaim)) }}
              </span>
            }
          </div>
          @if (s.betweenRunSd !== null) {
            <div class="comp card">
              <span class="muted">{{ i18n.t('prc.betweenRun') }}</span>
              <b>{{ s.betweenRunCvPct | number:'1.1-2' }}%</b>
              <span class="sub">SD {{ s.betweenRunSd | number:'1.0-4' }} {{ s.unit }}</span>
            </div>
          }
          @if (s.betweenDaySd !== null) {
            <div class="comp card">
              <span class="muted">{{ i18n.t('prc.betweenDay') }}</span>
              <b>{{ s.betweenDayCvPct | number:'1.1-2' }}%</b>
              <span class="sub">SD {{ s.betweenDaySd | number:'1.0-4' }} {{ s.unit }}</span>
            </div>
          }
          <div class="comp card highlight">
            <span class="muted">{{ i18n.t('prc.withinLab') }}</span>
            <b>{{ s.withinLabCvPct | number:'1.1-2' }}%</b>
            <span class="sub">SD {{ s.withinLabSd | number:'1.0-4' }} {{ s.unit }} · df {{ s.withinLabDf }}</span>
            @if (s.withinLabCvLowerPct !== null) {
              <span class="sub">{{ i18n.t('prc.ci95') }} {{ s.withinLabCvLowerPct | number:'1.1-2' }}–{{ s.withinLabCvUpperPct | number:'1.1-2' }}%</span>
            }
            @if (s.claimedWithinLabCvPct !== null && s.withinLabVerificationLimitCvPct !== null) {
              <span class="claim" [class]="verdict(s.withinLabCvPct, s.claimedWithinLabCvPct, s.meetsWithinLabClaim)">
                {{ i18n.t('prc.claim') }} {{ s.claimedWithinLabCvPct }}% · UVL {{ s.withinLabVerificationLimitCvPct | number:'1.1-2' }}% ·
                {{ i18n.t('prc.verdict.' + verdict(s.withinLabCvPct, s.claimedWithinLabCvPct, s.meetsWithinLabClaim)) }}
              </span>
            }
            @if (s.withinLabConsistentWithReproducibilityClaim !== null) {
              <span class="claim" [class]="s.withinLabConsistentWithReproducibilityClaim ? 'met' : 'notVerified'">
                {{ i18n.t('prc.claimedRepro') }} {{ s.claimedReproducibilityCvPct }}% · UVL {{ s.reproducibilityVerificationLimitCvPct | number:'1.1-2' }}% ·
                {{ s.withinLabConsistentWithReproducibilityClaim ? i18n.t('prc.reproConsistent') : i18n.t('prc.reproInconsistent') }}
              </span>
            }
          </div>
          @if (s.betweenSiteSd !== null) {
            <div class="comp card">
              <span class="muted">{{ i18n.t('prc.betweenSite') }}</span>
              <b>{{ s.betweenSiteCvPct | number:'1.1-2' }}%</b>
              <span class="sub">SD {{ s.betweenSiteSd | number:'1.0-4' }} {{ s.unit }}</span>
            </div>
          }
          @if (s.reproducibilitySd !== null) {
            <div class="comp card highlight repro">
              <span class="muted">{{ i18n.t('prc.reproducibility') }}</span>
              <b>{{ s.reproducibilityCvPct | number:'1.1-2' }}%</b>
              <span class="sub">SD {{ s.reproducibilitySd | number:'1.0-4' }} {{ s.unit }} · df {{ s.reproducibilityDf }}</span>
              @if (s.reproducibilityCvLowerPct !== null) {
                <span class="sub">{{ i18n.t('prc.ci95') }} {{ s.reproducibilityCvLowerPct | number:'1.1-2' }}–{{ s.reproducibilityCvUpperPct | number:'1.1-2' }}%</span>
              }
              @if (s.claimedReproducibilityCvPct !== null && s.reproducibilityVerificationLimitCvPct !== null) {
                <span class="claim" [class]="verdict(s.reproducibilityCvPct, s.claimedReproducibilityCvPct, s.meetsReproducibilityClaim)">
                  {{ i18n.t('prc.claim') }} {{ s.claimedReproducibilityCvPct }}% · UVL {{ s.reproducibilityVerificationLimitCvPct | number:'1.1-2' }}% ·
                  {{ i18n.t('prc.verdict.' + verdict(s.reproducibilityCvPct, s.claimedReproducibilityCvPct, s.meetsReproducibilityClaim)) }}
                </span>
              }
            </div>
          }
        </div>

        <section class="card">
          <h3>{{ i18n.t('prc.varianceBreakdown') }}</h3>
          <div class="stackbar">
            @for (seg of shares(); track seg.key) {
              <div class="seg" [class]="seg.key" [style.width.%]="seg.pct" [attr.title]="i18n.t(seg.label)"></div>
            }
          </div>
          <div class="legend">
            @for (seg of shares(); track seg.key) {
              <span><span class="k" [class]="seg.key"></span>{{ i18n.t(seg.label) }} ({{ seg.pct | number:'1.0-0' }}%)</span>
            }
          </div>
          <p class="muted small">{{ i18n.t('prc.varianceNote') }}</p>
          <p class="muted small">{{ i18n.t('prc.uvlNote') }}</p>
        </section>
      }

      @if (s.measurements.length > 0) {
        <section class="card">
          <h3>{{ i18n.t('prc.coverage') }}</h3>
          <div class="coverage">
            @if (s.coverage.sites > 0) { <div><b>{{ s.coverage.sites }}</b><span>{{ i18n.t('prc.sites') }}</span></div> }
            <div><b>{{ s.coverage.days }}</b><span>{{ i18n.t('prc.days') }}</span></div>
            <div><b>{{ s.coverage.runs }}</b><span>{{ i18n.t('prc.runs') }}</span></div>
            <div><b>{{ s.coverage.operators }}</b><span>{{ i18n.t('prc.operators') }}</span></div>
            <div><b>{{ s.coverage.instruments }}</b><span>{{ i18n.t('prc.instruments') }}</span></div>
            <div><b>{{ s.coverage.reagentLots }}</b><span>{{ i18n.t('prc.reagentLots') }}</span></div>
            <div><b>{{ s.coverage.calibratorLots }}</b><span>{{ i18n.t('prc.calibratorLots') }}</span></div>
          </div>
          @if (s.coverage.firstMeasuredOn) {
            <p class="muted small">{{ s.coverage.firstMeasuredOn | date:'mediumDate' }} – {{ s.coverage.lastMeasuredOn | date:'mediumDate' }}</p>
          }
          <p class="small" [class.warn]="!s.coverage.meetsEp15MinimumDesign">
            {{ s.coverage.meetsEp15MinimumDesign ? i18n.t('prc.designAdequate') : i18n.t('prc.designBelowMinimum') }}
          </p>
          @if (s.coverage.sites > 0) {
            <p class="small" [class.warn]="!s.coverage.meetsMultiSiteMinimumDesign">
              {{ s.coverage.meetsMultiSiteMinimumDesign ? i18n.t('prc.multiSiteAdequate') : i18n.t('prc.multiSiteBelowMinimum') }}
            </p>
          }
        </section>
      }

      @if (s.runs.length > 0) {
        <section class="card">
          <h3>{{ i18n.t('prc.runMeans') }}</h3>
          <table>
            <thead><tr><th>{{ i18n.t('prc.site') }}</th><th>{{ i18n.t('prc.day') }}</th><th>{{ i18n.t('prc.run') }}</th><th>n</th><th>{{ i18n.t('lin.mean') }}</th></tr></thead>
            <tbody>
              @for (r of s.runs; track (r.siteLabel ?? '') + '/' + (r.dayLabel ?? '') + '/' + r.runLabel) {
                <tr><td>{{ r.siteLabel ?? '—' }}</td><td>{{ r.dayLabel ?? '—' }}</td><td><b>{{ r.runLabel }}</b></td><td>{{ r.replicateCount }}</td><td>{{ r.mean | number:'1.0-4' }} {{ s.unit }}</td></tr>
              }
            </tbody>
          </table>
        </section>
      }

      <section class="card">
        <h3>{{ i18n.t('prc.replicateData') }} ({{ s.measurements.length }})</h3>
        @if (s.state !== 'SignedOff') {
          <form [formGroup]="entryForm" (ngSubmit)="add(s.id)">
            <div class="quad">
              <div><label>{{ i18n.t('prc.site') }}</label><input formControlName="siteLabel" [attr.aria-label]="i18n.t('prc.site')" [placeholder]="i18n.t('prc.siteHint')" /></div>
              <div><label>{{ i18n.t('prc.day') }}</label><input formControlName="dayLabel" [attr.aria-label]="i18n.t('prc.day')" [placeholder]="i18n.t('prc.dayHint')" /></div>
              <div><label>{{ i18n.t('prc.run') }}</label><input formControlName="runLabel" [attr.aria-label]="i18n.t('prc.run')" [placeholder]="i18n.t('prc.runHint')" /></div>
              <div><label>{{ i18n.t('lin.measured') }} ({{ s.unit }})</label><input type="number" step="any" formControlName="value" [attr.aria-label]="i18n.t('lin.measured')" /></div>
            </div>
            <button type="button" class="link toggle" (click)="showConditions.set(!showConditions())">
              {{ showConditions() ? i18n.t('prc.hideConditions') : i18n.t('prc.showConditions') }}
            </button>
            @if (showConditions()) {
              <div class="triple">
                <div><label>{{ i18n.t('prc.operator') }}</label><input formControlName="operator" [attr.aria-label]="i18n.t('prc.operator')" /></div>
                <div><label>{{ i18n.t('prc.instrument') }}</label><input formControlName="instrument" [attr.aria-label]="i18n.t('prc.instrument')" /></div>
                <div><label>{{ i18n.t('prc.measuredOn') }}</label><input type="date" formControlName="measuredOn" [attr.aria-label]="i18n.t('prc.measuredOn')" /></div>
                <div><label>{{ i18n.t('prc.reagentLot') }}</label><input formControlName="reagentLot" [attr.aria-label]="i18n.t('prc.reagentLot')" /></div>
                <div><label>{{ i18n.t('prc.calibratorLot') }}</label><input formControlName="calibratorLot" [attr.aria-label]="i18n.t('prc.calibratorLot')" /></div>
              </div>
            }
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
            <thead><tr>
              <th>{{ i18n.t('prc.site') }}</th><th>{{ i18n.t('prc.day') }}</th><th>{{ i18n.t('prc.run') }}</th><th>{{ i18n.t('lin.measured') }}</th>
              <th>{{ i18n.t('prc.operator') }}</th><th>{{ i18n.t('prc.reagentLot') }}</th><th>{{ i18n.t('prc.measuredOn') }}</th><th></th>
            </tr></thead>
            <tbody>
              @for (m of s.measurements; track m.id) {
                <tr>
                  <td>{{ m.siteLabel ?? '—' }}</td>
                  <td>{{ m.dayLabel ?? '—' }}</td>
                  <td>{{ m.runLabel }}</td>
                  <td>{{ m.value | number:'1.0-4' }}</td>
                  <td>{{ m.operator ?? '—' }}</td>
                  <td>{{ m.reagentLot ?? '—' }}</td>
                  <td>{{ m.measuredOn ? (m.measuredOn | date:'mediumDate') : '—' }}</td>
                  <td>
                    @if (s.state !== 'SignedOff') {
                      <button class="link danger-link" type="button" (click)="facade.removeMeasurement(s.id, m.id)" [attr.aria-label]="i18n.t('prc.removeReplicate')">✕</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>

      <section class="card">
        <h3>{{ i18n.t('prc.ptContext') }}</h3>
        <p class="muted small">{{ i18n.t('prc.ptContextNote') }}</p>
        @if (s.claimedReproducibilityCvPct !== null) {
          <p class="small">{{ i18n.t('prc.claimedRepro') }}: <b>{{ s.claimedReproducibilityCvPct }}%</b>
            · {{ i18n.t('prc.withinLabCv') }}: <b>{{ s.withinLabCvPct !== null ? (s.withinLabCvPct | number:'1.1-2') + '%' : '—' }}</b>
            @if (s.reproducibilityCvPct !== null) { · {{ i18n.t('prc.reproducibility') }}: <b>{{ s.reproducibilityCvPct | number:'1.1-2' }}%</b> }
          </p>
        }
        @if (s.ptContext.length === 0) {
          <p class="muted">{{ i18n.t('prc.ptContextEmpty') }}</p>
        } @else {
          <table>
            <thead><tr><th>PT</th><th>{{ i18n.t('prc.scheme') }}</th><th>{{ i18n.t('prc.assigned') }}</th><th>{{ i18n.t('prc.schemeSd') }}</th><th>{{ i18n.t('prc.schemeCv') }}</th><th>z</th></tr></thead>
            <tbody>
              @for (p of s.ptContext; track p.id) {
                <tr>
                  <td class="code">{{ p.ptRef }}</td><td>{{ p.scheme }} {{ p.cycle }}</td>
                  <td>{{ p.assignedValue | number:'1.0-4' }}</td><td>{{ p.standardDeviation | number:'1.0-4' }}</td>
                  <td>{{ p.schemeCvPct !== null ? (p.schemeCvPct | number:'1.1-2') + '%' : '—' }}</td>
                  <td>{{ p.zScore ?? '—' }}</td>
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
            <button (click)="esignOpen.set(true)">{{ i18n.t('mc.signOff') }}</button>
            <qams-esign-dialog [open]="esignOpen()" [meaning]="i18n.t('esign.aqMeaning')" [busy]="facade.loading()" [error]="facade.error()" (confirm)="doSignOff(s.id, $event)" (cancel)="esignOpen.set(false)" />
          }
          @if (s.state === 'SignedOff') { <p class="muted">{{ i18n.t('mc.signedOffNote') }} {{ i18n.t('prc.downstreamNote') }}</p> }
        </div>
      </section>

      <qams-signature-manifest [subjectUrl]="'/api/precision-studies/' + s.id + '/signatures'" />

      <qams-audit-trail [subject]="s.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .siblings { display: flex; flex-wrap: wrap; gap: .75rem; align-items: center; margin-bottom: 1rem; font-size: .85rem; }
    .components { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem; margin-bottom: 1rem; }
    .comp { display: flex; flex-direction: column; gap: 2px; padding: 14px 16px; }
    .comp b { font-size: 1.4rem; color: var(--nt-navy-deep); }
    .comp.highlight { border-inline-start: 4px solid var(--nt-blue); }
    .comp.repro { border-inline-start-color: var(--nt-navy); }
    .comp .sub { font-size: .72rem; color: var(--nt-grey-m); }
    .claim { font-size: .72rem; margin-top: 4px; font-weight: 600; }
    .claim.met { color: var(--nt-ink-ok); } .claim.withinLimit { color: var(--nt-ink-warn); } .claim.notVerified { color: var(--nt-ink-crit); }
    .stackbar { display: flex; height: 22px; border-radius: 5px; overflow: hidden; background: var(--nt-filter-grey); }
    .rep { background: var(--nt-teal); } .run { background: var(--nt-blue); } .day { background: var(--nt-navy); } .site { background: var(--nt-gold); }
    .legend { font-size: 11px; color: var(--nt-grey-m); display: flex; gap: 14px; align-items: center; margin-top: 6px; flex-wrap: wrap; }
    .k { display: inline-block; width: 12px; height: 12px; border-radius: 2px; margin-inline-end: 4px; vertical-align: middle; }
    .coverage { display: grid; grid-template-columns: repeat(auto-fit, minmax(110px, 1fr)); gap: .5rem; }
    .coverage div { display: flex; flex-direction: column; } .coverage span { font-size: .72rem; color: var(--nt-ink-neutral); }
    .warn { color: var(--nt-ink-warn); font-weight: 600; }
    .small { font-size: .75rem; margin-top: 6px; }
    section { margin-bottom: 1rem; }
    .triple { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem; }
    .quad { display: grid; grid-template-columns: repeat(4, 1fr); gap: 1rem; }
    .mtable { margin-top: 1rem; }
    .danger-link { color: var(--nt-red); }
    .actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    .actions button { width: auto; }
    form { border-bottom: 1px solid var(--nt-border); padding-bottom: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 800px) { .triple, .quad { grid-template-columns: 1fr; } }
  `]
})
export class PrecisionDetailComponent implements OnInit {
  readonly facade = inject(PrecisionFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound study id. */
  readonly id = input.required<string>();

  /** Whether the Part 11 e-signature dialog is open for the sign-off. */
  readonly esignOpen = signal(false);
  /** Whether the optional measurement-conditions fields are shown. */
  readonly showConditions = signal(false);

  /** Signs off through the ceremony dialog; closes on success, stays open (showing the error) on failure. */
  async doSignOff(id: string, credentials: EsignCredentials): Promise<void> {
    await this.facade.signOff(id, credentials);
    if (this.facade.error() === '') { this.esignOpen.set(false); }
  }

  readonly flowSteps = ['DataEntry', 'Calculated', 'SignedOff'] as const;
  readonly item = this.facade.selected;

  /** Bounds mirror the backend validator (PrecisionLimits). */
  readonly entryForm = this.fb.nonNullable.group({
    siteLabel: ['', [Validators.maxLength(60)]],
    dayLabel: ['', [Validators.maxLength(60)]],
    runLabel: ['', [Validators.required, Validators.maxLength(60)]],
    value: [null as number | null, [Validators.required]],
    operator: ['', [Validators.maxLength(100)]],
    instrument: ['', [Validators.maxLength(100)]],
    reagentLot: ['', [Validators.maxLength(60)]],
    calibratorLot: ['', [Validators.maxLength(60)]],
    measuredOn: [''],
  });

  /** Distinct runs (nested within site and day) — the client mirror of the backend's minimum check. */
  readonly runCount = computed(() =>
    new Set((this.item()?.measurements ?? []).map((m) => `${m.siteLabel ?? ''}/${m.dayLabel ?? ''}/${m.runLabel}`)).size);

  /** CSV importer state: run, value, then the optional day, conditions and site (appended last so older files still import). */
  readonly showImport = signal(false);
  readonly importResult = signal<BulkImportResult | null>(null);
  readonly importColumns: CsvColumn[] = [
    { label: this.i18n.t('prc.run'), numeric: false },
    { label: this.i18n.t('lin.measured'), numeric: true },
    { label: this.i18n.t('prc.day'), numeric: false, optional: true },
    { label: this.i18n.t('prc.operator'), numeric: false, optional: true },
    { label: this.i18n.t('prc.instrument'), numeric: false, optional: true },
    { label: this.i18n.t('prc.reagentLot'), numeric: false, optional: true },
    { label: this.i18n.t('prc.calibratorLot'), numeric: false, optional: true },
    { label: this.i18n.t('prc.measuredOn'), numeric: false, optional: true },
    { label: this.i18n.t('prc.site'), numeric: false, optional: true },
  ];

  async importRows(id: string, rows: string[][]): Promise<void> {
    const parsed = rows.map((r): AddPrecisionMeasurementRequest => ({
      runLabel: (r[0] ?? '').trim(),
      value: Number(r[1]),
      dayLabel: this.blankToNull(r[2]),
      operator: this.blankToNull(r[3]),
      instrument: this.blankToNull(r[4]),
      reagentLot: this.blankToNull(r[5]),
      calibratorLot: this.blankToNull(r[6]),
      measuredOn: this.blankToNull(r[7]),
      siteLabel: this.blankToNull(r[8]),
    }));
    this.importResult.set(await this.facade.importMeasurements(id, parsed));
  }

  /**
   * Variance shares (SD² proportions) for the stacked bar — only the
   * components the resolved design actually estimates.
   */
  readonly shares = computed(() => {
    const s = this.item();
    const parts = [
      { key: 'rep', label: 'prc.repeatability', v: (s?.repeatabilitySd ?? 0) ** 2 },
      { key: 'run', label: 'prc.betweenRun', v: s?.betweenRunSd === null ? -1 : (s?.betweenRunSd ?? 0) ** 2 },
      { key: 'day', label: 'prc.betweenDay', v: s?.betweenDaySd === null ? -1 : (s?.betweenDaySd ?? 0) ** 2 },
      { key: 'site', label: 'prc.betweenSite', v: s?.betweenSiteSd === null ? -1 : (s?.betweenSiteSd ?? 0) ** 2 },
    ].filter((p) => p.v >= 0);
    const total = parts.reduce((sum, p) => sum + p.v, 0);
    return parts.map((p) => ({ key: p.key, label: p.label, pct: total === 0 ? (p.key === 'rep' ? 100 : 0) : (p.v / total) * 100 }));
  });

  /**
   * EP15-A3: at or under the claim → met; over the claim but within the upper
   * verification limit → the claim still stands; over the UVL → not verified.
   * The backend's boolean is authoritative; the claim comparison only words it.
   */
  verdict(observed: number | null, claim: number | null, meets: boolean | null): ClaimVerdict {
    if (meets === false) { return 'notVerified'; }
    return observed !== null && claim !== null && observed > claim ? 'withinLimit' : 'met';
  }

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  async add(id: string): Promise<void> {
    if (this.entryForm.invalid) { return; }
    const raw = this.entryForm.getRawValue();
    await this.facade.addMeasurement(id, {
      runLabel: raw.runLabel.trim(),
      value: raw.value!,
      siteLabel: this.blankToNull(raw.siteLabel),
      dayLabel: this.blankToNull(raw.dayLabel),
      operator: this.blankToNull(raw.operator),
      instrument: this.blankToNull(raw.instrument),
      reagentLot: this.blankToNull(raw.reagentLot),
      calibratorLot: this.blankToNull(raw.calibratorLot),
      measuredOn: this.blankToNull(raw.measuredOn),
    });
    // Keep the site, day, run and conditions so replicates of the same run enter quickly.
    this.entryForm.patchValue({ value: null });
  }

  private blankToNull(value: string | undefined): string | null {
    const trimmed = (value ?? '').trim();
    return trimmed === '' ? null : trimmed;
  }
}
