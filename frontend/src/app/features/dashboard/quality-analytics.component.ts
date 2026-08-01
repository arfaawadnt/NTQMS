import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, FormControl, FormRecord, ReactiveFormsModule, Validators } from '@angular/forms';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import { PermissionsService } from '../../core/permissions.service';
import { AnalyticsRow, CategoryCount } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { GaugeComponent } from '../../shared/ui/gauge.component';
import { DonutChartComponent } from '../../shared/ui/donut-chart.component';
import { BarChartComponent } from '../../shared/ui/bar-chart.component';
import { RiskMatrixComponent } from '../../shared/ui/risk-matrix.component';
import { QualityAnalyticsFacade } from './quality-analytics.facade';

/** The two framings of the same computation. */
type View = 'statistics' | 'review';

/**
 * Quality Analytics: the Quality Statistics dashboard and the ISO/IEC 17025
 * §8.9.2 management-review pack, over one fetch so the two framings cannot
 * disagree about a number.
 *
 * Three behaviours are deliberate and load-bearing:
 *
 * - A section the user may not view is absent from the payload entirely, so this
 *   template renders what it was given rather than hiding what it received.
 * - A null percentage renders an em dash. "No population" is not "zero percent",
 *   and a quality system that conflates them reports failures that never happened.
 * - When a branch/department filter is in force, the sections that carry no
 *   organisational attribution say so, rather than appearing to be narrowed.
 */
@Component({
  selector: 'qams-quality-analytics',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe, ReactiveFormsModule, PageHeaderComponent, DrawerComponent,
    GaugeComponent, DonutChartComponent, BarChartComponent, RiskMatrixComponent,
  ],
  template: `
    <qams-page-header
      [title]="i18n.t('qa.title')"
      [subtitle]="facade.analytics()
        ? i18n.t('dash.computedAt') + ' ' + ((facade.analytics()!.computedAtUtc | date:'medium') ?? '')
        : ''">
      @if (perms.can('reports.manage')) {
        <button class="secondary" (click)="openWeights()">{{ i18n.t('qa.tuneWeights') }}</button>
      }
    </qams-page-header>

    <!-- Scope -->
    <div class="filterbar card">
      <select [value]="branch()" (change)="setBranch($any($event.target).value)"
              [attr.aria-label]="i18n.t('qa.branch')">
        <option value="">{{ i18n.t('qa.allBranches') }}</option>
        @for (b of org.branches(); track b.id) { <option [value]="b.id">{{ b.name }}</option> }
      </select>
      <select [value]="department()" (change)="setDepartment($any($event.target).value)"
              [attr.aria-label]="i18n.t('qa.department')">
        <option value="">{{ i18n.t('qa.allDepartments') }}</option>
        @for (d of org.departments(); track d.id) { <option [value]="d.id">{{ d.name }}</option> }
      </select>
      @if (facade.filtered()) {
        <button class="ghost" (click)="clear()">{{ i18n.t('qa.clearFilter') }}</button>
      }
    </div>

    @if (facade.error(); as err) { <p class="error">{{ err }}</p> }
    @if (facade.loading() && !facade.analytics()) { <p class="muted">{{ i18n.t('common.loading') }}</p> }

    @if (facade.analytics(); as a) {
      @if (a.scope.filterApplied && a.scope.unscopedSections.length > 0) {
        <p class="notice">
          {{ i18n.t('qa.unscopedNotice') }}
          <b>{{ sectionNames(a.scope.unscopedSections) }}</b>
        </p>
      }
      @if (a.scope.hiddenSections.length > 0) {
        <p class="notice muted-notice">
          {{ i18n.t('qa.hiddenNotice') }} <b>{{ sectionNames(a.scope.hiddenSections) }}</b>
        </p>
      }

      <!-- Composite score -->
      <section class="card hero">
        <div class="heroGauge">
          <qams-gauge [value]="a.health.score" suffix="" [label]="i18n.t('qa.healthScore')"
                      [caption]="i18n.t('qa.healthScore')" />
          <p class="basis">
            {{ i18n.t('qa.basedOn')
               .replace('{n}', a.health.contributingCategories + '')
               .replace('{total}', a.health.totalCategories + '') }}
          </p>
        </div>
        <div class="heroWeights">
          <h3>{{ i18n.t('qa.weightsDetail') }}</h3>
          <table>
            <thead>
              <tr>
                <th>{{ i18n.t('qa.category') }}</th>
                <th class="num">{{ i18n.t('qa.weight') }}</th>
                <th class="num">{{ i18n.t('qa.achieved') }}</th>
              </tr>
            </thead>
            <tbody>
              @for (c of a.health.components; track c.category) {
                <tr [class.excluded]="!c.contributed">
                  <td>{{ i18n.t('qa.cat.' + lower(c.category)) }}</td>
                  <td class="num">{{ c.weight }}</td>
                  <td class="num">
                    @if (c.contributed) { {{ c.achievedScore }}% }
                    @else { <span class="why">{{ i18n.t('qa.excl.' + (c.excludedReason ?? 'noData')) }}</span> }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <!-- View switch -->
      <div class="tabs" role="tablist">
        <button role="tab" [attr.aria-selected]="view() === 'statistics'"
                [class.on]="view() === 'statistics'" (click)="view.set('statistics')">
          {{ i18n.t('qa.tabStatistics') }}
        </button>
        <button role="tab" [attr.aria-selected]="view() === 'review'"
                [class.on]="view() === 'review'" (click)="view.set('review')">
          {{ i18n.t('qa.tabReview') }}
        </button>
      </div>

      @if (view() === 'statistics') {
        <div class="grid">
          @if (a.documentControl; as d) {
            <section class="card">
              <h3>{{ i18n.t('qa.cat.documentcontrol') }}</h3>
              <div class="split">
                <qams-gauge [value]="d.percentCurrent" [caption]="i18n.t('qa.pctCurrent')" />
                <ul class="facts">
                  <li><span>{{ i18n.t('qa.activeSops') }}</span><b>{{ d.totalActive }}</b></li>
                  <li [class.bad]="d.overdueReviews > 0">
                    <span>{{ i18n.t('qa.overdueReviews') }}</span><b>{{ d.overdueReviews }}</b>
                  </li>
                  <li><span>{{ i18n.t('qa.acksRecorded') }}</span><b>{{ d.acknowledgementsRecorded }}</b></li>
                </ul>
              </div>
              <qams-bar-chart [data]="reviewHorizon(d)" [label]="i18n.t('qa.reviewsDue')"
                              [emptyText]="i18n.t('qa.noData')" />
            </section>
          }

          @if (a.ncCapa; as n) {
            <section class="card">
              <h3>{{ i18n.t('qa.cat.nonconformancecapa') }}</h3>
              <div class="split">
                <qams-gauge [value]="n.capaOnSchedulePercent" [caption]="i18n.t('qa.capaOnSchedule')" />
                <ul class="facts">
                  <li><span>{{ i18n.t('qa.openNcs') }}</span><b>{{ n.openNcs }} / {{ n.totalNcs }}</b></li>
                  <li [class.bad]="n.overdueCapa > 0">
                    <span>{{ i18n.t('qa.overdueCapa') }}</span><b>{{ n.overdueCapa }}</b>
                  </li>
                  <li><span>{{ i18n.t('qa.capaClosed') }}</span><b>{{ n.capaClosedOnTime }} / {{ n.capaClosedTotal }}</b></li>
                </ul>
              </div>
              <qams-bar-chart [data]="n.bySource" [label]="i18n.t('qa.ncBySource')"
                              [emptyText]="i18n.t('qa.noData')" />
            </section>
          }

          @if (a.complaints; as c) {
            <section class="card">
              <h3>{{ i18n.t('qa.cat.complaints') }}</h3>
              <div class="split">
                <qams-gauge [value]="c.percentWithinSla" [caption]="i18n.t('qa.withinSla')" />
                <ul class="facts">
                  <li><span>{{ i18n.t('qa.openComplaints') }}</span><b>{{ c.open }} / {{ c.total }}</b></li>
                  <li>
                    <span>{{ i18n.t('qa.avgResolution') }}</span>
                    <b>{{ c.averageResolutionDays === null ? '—' : c.averageResolutionDays + 'd' }}</b>
                  </li>
                  @if (c.percentWithinSla === null) {
                    <li class="hintrow"><span class="hint">{{ i18n.t('qa.noSlaDefined') }}</span></li>
                  }
                </ul>
              </div>
              <qams-donut-chart [data]="c.byChannel" [label]="i18n.t('qa.byChannel')"
                                [emptyText]="i18n.t('qa.noData')" />
            </section>
          }

          @if (a.audits; as au) {
            <section class="card">
              <h3>{{ i18n.t('qa.cat.internalaudit') }}</h3>
              <div class="split">
                <qams-gauge [value]="au.planCompletionPercent" [caption]="i18n.t('qa.planCompletion')" />
                <ul class="facts">
                  <li><span>{{ i18n.t('qa.auditsDone') }}</span><b>{{ au.completed }} / {{ au.totalPlanned }}</b></li>
                  <li [class.bad]="au.majorFindings > 0">
                    <span>{{ i18n.t('qa.major') }}</span><b>{{ au.majorFindings }}</b>
                  </li>
                  <li><span>{{ i18n.t('qa.minor') }}</span><b>{{ au.minorFindings }}</b></li>
                  <li><span>{{ i18n.t('qa.observation') }}</span><b>{{ au.observations }}</b></li>
                </ul>
              </div>
              <qams-donut-chart [data]="findings(au)" [label]="i18n.t('qa.findingsSplit')"
                                [emptyText]="i18n.t('qa.noFindings')" />
            </section>
          }

          @if (a.equipment; as e) {
            <section class="card">
              <h3>{{ i18n.t('qa.cat.equipment') }}</h3>
              <div class="split">
                <qams-gauge [value]="e.calibrationCompliancePercent" [caption]="i18n.t('qa.calCompliance')" />
                <ul class="facts">
                  <li>
                    <span>{{ i18n.t('qa.availability') }}</span>
                    <b>{{ e.availabilityPercent === null ? '—' : e.availabilityPercent + '%' }}</b>
                  </li>
                  <li [class.bad]="e.overdueCalibration > 0">
                    <span>{{ i18n.t('qa.overdueCal') }}</span><b>{{ e.overdueCalibration }}</b>
                  </li>
                  <li><span>{{ i18n.t('qa.outOfService') }}</span><b>{{ e.outOfService }}</b></li>
                </ul>
              </div>
              <qams-donut-chart [data]="e.byStatus" [label]="i18n.t('qa.equipByStatus')"
                                [emptyText]="i18n.t('qa.noData')" />
            </section>
          }

          @if (a.competency; as k) {
            <section class="card">
              <h3>{{ i18n.t('qa.cat.competency') }}</h3>
              <div class="split">
                <qams-gauge [value]="k.percentCompetent" [caption]="i18n.t('qa.pctCompetent')" />
                <ul class="facts">
                  <li><span>{{ i18n.t('qa.authorized') }}</span><b>{{ k.authorized }} / {{ k.total }}</b></li>
                  <li [class.warn]="k.expiringWithin90 > 0">
                    <span>{{ i18n.t('qa.expiring90') }}</span><b>{{ k.expiringWithin90 }}</b>
                  </li>
                  <li><span>{{ i18n.t('qa.pendingTraining') }}</span><b>{{ k.pendingTraining }}</b></li>
                  <li [class.bad]="k.revoked > 0"><span>{{ i18n.t('qa.revoked') }}</span><b>{{ k.revoked }}</b></li>
                </ul>
              </div>
            </section>
          }

          @if (a.proficiencyTesting; as p) {
            <section class="card">
              <h3>{{ i18n.t('qa.cat.proficiencytesting') }}</h3>
              <div class="split">
                <qams-gauge [value]="p.satisfactionRatePercent" [caption]="i18n.t('qa.satisfactionRate')" />
                <ul class="facts">
                  <li><span>{{ i18n.t('qa.satisfactory') }}</span><b>{{ p.satisfactory }}</b></li>
                  <li [class.warn]="p.questionable > 0">
                    <span>{{ i18n.t('qa.questionable') }}</span><b>{{ p.questionable }}</b>
                  </li>
                  <li [class.bad]="p.unsatisfactory > 0">
                    <span>{{ i18n.t('qa.unsatisfactory') }}</span><b>{{ p.unsatisfactory }}</b>
                  </li>
                  <li><span>{{ i18n.t('qa.ptPending') }}</span><b>{{ p.pending }}</b></li>
                </ul>
              </div>
            </section>
          }

          @if (a.suppliers; as s) {
            <section class="card">
              <h3>{{ i18n.t('qa.cat.supplierquality') }}</h3>
              <div class="split">
                <qams-gauge [value]="s.approvedPercent" [caption]="i18n.t('qa.approvedPct')" />
                <ul class="facts">
                  <li><span>{{ i18n.t('qa.approvedSuppliers') }}</span><b>{{ s.approved }} / {{ s.total }}</b></li>
                  <li>
                    <span>{{ i18n.t('qa.avgEvalScore') }}</span>
                    <b>{{ s.averageEvaluationScore === null ? '—' : s.averageEvaluationScore }}</b>
                  </li>
                  <li [class.bad]="s.suspended > 0">
                    <span>{{ i18n.t('qa.suspended') }}</span><b>{{ s.suspended }}</b>
                  </li>
                </ul>
              </div>
            </section>
          }

          @if (a.risk; as r) {
            <section class="card">
              <h3>{{ i18n.t('qa.cat.risk') }}</h3>
              <div class="split">
                <qams-risk-matrix [data]="r.matrix"
                                  [likelihoodLabel]="i18n.t('qa.likelihood')"
                                  [impactLabel]="i18n.t('qa.impact')" />
                <ul class="facts">
                  <li [class.bad]="r.highOrExtreme > 0">
                    <span>{{ i18n.t('qa.highRisks') }}</span><b>{{ r.highOrExtreme }} / {{ r.total }}</b></li>
                  <li>
                    <span>{{ i18n.t('qa.highMitigated') }}</span>
                    <b>{{ r.highMitigatedPercent === null ? '—' : r.highMitigatedPercent + '%' }}</b>
                  </li>
                  <li [class.bad]="r.overdueTreatments > 0">
                    <span>{{ i18n.t('qa.overdueTreatments') }}</span><b>{{ r.overdueTreatments }}</b>
                  </li>
                </ul>
              </div>
            </section>
          }
        </div>
      } @else {
        <!-- ISO/IEC 17025 §8.9.2 management-review inputs, in clause order -->
        @for (c of clauses(); track c.key) {
          @if (c.present) {
            <section class="card clause">
              <header>
                <span class="num">{{ c.index }}</span>
                <div>
                  <h3>{{ i18n.t('qa.clause.' + c.key) }}</h3>
                  <span class="badge">{{ i18n.t('qa.clauseLabel') }} {{ c.clause }}</span>
                </div>
              </header>
              <p class="narrative">{{ i18n.t('qa.narr.' + c.key) }}</p>
              <div class="kpis">
                @for (k of c.kpis; track k.label) {
                  <div class="kpi">
                    <div class="kv">{{ k.value }}</div>
                    <div class="kl">{{ k.label }}</div>
                  </div>
                }
              </div>
              @if (c.rows.length > 0) {
                <table>
                  <thead>
                    <tr>
                      <th>{{ i18n.t('qa.reference') }}</th>
                      <th>{{ i18n.t('qa.titleCol') }}</th>
                      <th>{{ i18n.t('qa.detail') }}</th>
                      <th>{{ i18n.t('qa.status') }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (r of c.rows; track r.reference + r.title) {
                      <tr>
                        <td class="code">{{ r.reference }}</td>
                        <td>{{ r.title }}</td>
                        <td>{{ r.detail ?? '—' }}</td>
                        <td>{{ r.status }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              } @else {
                <p class="muted">{{ i18n.t('qa.noRecords') }}</p>
              }
            </section>
          }
        }
      }
    }

    <!-- Weighting -->
    <qams-drawer [open]="weightsOpen()" [title]="i18n.t('qa.tuneWeights')" (closed)="weightsOpen.set(false)">
      <!-- [formGroup] must sit on the <form> itself: (ngSubmit) is an output of the
           form directive, and without it the browser performs a native submit and
           reloads the page. The reason control is bound standalone alongside it. -->
      <form class="drawer-form" [formGroup]="weightsForm" (ngSubmit)="saveWeights()">
        <p class="hint">{{ i18n.t('qa.weightsHint') }}</p>
        @for (w of facade.weights(); track w.category) {
          <label>
            {{ i18n.t('qa.cat.' + lower(w.category)) }}
            <input type="number" min="0" max="100" [formControlName]="w.category" />
          </label>
        }
        <label>
          {{ i18n.t('qa.reason') }}
          <textarea [formControl]="reasonCtrl" rows="3"
                    [placeholder]="i18n.t('qa.reasonHint')"></textarea>
        </label>
        <div class="actions">
          <button type="submit" [disabled]="!canSave() || facade.saving()">
            {{ i18n.t('common.save') }}
          </button>
          <button type="button" class="ghost" (click)="weightsOpen.set(false)">
            {{ i18n.t('common.cancel') }}
          </button>
        </div>
      </form>
    </qams-drawer>
  `,
  styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; margin-bottom: 1rem; }
    .filterbar select { width: auto; min-width: 190px; }
    .filterbar button { width: auto; }
    .notice { background: var(--nt-summary-blue); border-inline-start: 3px solid var(--nt-blue);
              padding: 8px 12px; border-radius: var(--nt-radius-card); font-size: 12.5px;
              margin: 0 0 1rem; color: var(--nt-slate); }
    .notice.muted-notice { background: var(--nt-filter-grey); border-inline-start-color: var(--nt-grey); }
    .hero { display: grid; grid-template-columns: 240px 1fr; gap: 24px; align-items: start; margin-bottom: 1rem; }
    .heroGauge { text-align: center; }
    .basis { font-size: 11.5px; color: var(--nt-grey-m); margin: 6px 0 0; }
    .heroWeights table { width: 100%; }
    .heroWeights .num { text-align: end; font-variant-numeric: tabular-nums; }
    .heroWeights tr.excluded td { color: var(--nt-grey-m); }
    .why { font-size: 11px; font-style: italic; }
    .tabs { display: flex; gap: 4px; margin-bottom: 1rem; border-bottom: 1px solid var(--nt-border); }
    .tabs button { width: auto; background: none; color: var(--nt-grey-d); border: none;
                   border-bottom: 3px solid transparent; border-radius: 0; padding: 9px 16px;
                   font-weight: 700; font-size: 13px; }
    .tabs button.on { color: var(--nt-blue); border-bottom-color: var(--nt-blue); }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(340px, 1fr)); gap: 1rem; }
    .split { display: flex; gap: 18px; align-items: center; flex-wrap: wrap; margin-bottom: 12px; }
    .facts { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column;
             gap: 5px; font-size: 12.5px; min-width: 150px; flex: 1; }
    .facts li { display: flex; justify-content: space-between; gap: 12px; }
    .facts b { font-variant-numeric: tabular-nums; color: var(--nt-navy); }
    .facts li.bad b { color: var(--nt-ink-crit); }
    .facts li.warn b { color: var(--nt-ink-warn); }
    .facts li.hintrow { display: block; }
    .hint { font-size: 11.5px; color: var(--nt-grey-m); }
    .clause { margin-bottom: 1rem; }
    .clause header { display: flex; gap: 12px; align-items: center; margin-bottom: 8px; }
    .clause .num { display: grid; place-items: center; width: 30px; height: 30px; flex: none;
                   border-radius: 50%; background: var(--nt-navy); color: #fff;
                   font-weight: 800; font-size: 13px; }
    .clause h3 { margin: 0; border: none; padding: 0; }
    .clause h3::before { display: none; }
    .badge { display: inline-block; font-size: 10.5px; letter-spacing: .04em; text-transform: uppercase;
             color: var(--nt-ink-teal); font-weight: 700; }
    .narrative { font-size: 13px; color: var(--nt-slate); margin: 0 0 12px; }
    .kpis { display: flex; gap: 22px; flex-wrap: wrap; margin-bottom: 12px; }
    .kpi .kv { font-size: 26px; font-weight: 800; font-variant-numeric: tabular-nums; color: var(--nt-navy); }
    .kpi .kl { font-size: 11px; color: var(--nt-grey-m); text-transform: uppercase; letter-spacing: .03em; }
    .actions { display: flex; gap: 10px; }
    .actions button { width: auto; }
    @media (max-width: 900px) { .hero { grid-template-columns: 1fr; } }
  `],
})
export class QualityAnalyticsComponent implements OnInit {
  readonly facade = inject(QualityAnalyticsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  readonly org = inject(OrgDataService);
  private readonly fb = inject(FormBuilder);

  readonly view = signal<View>('statistics');
  readonly weightsOpen = signal(false);
  readonly branch = signal('');
  readonly department = signal('');

  /**
   * A FormRecord, not a typed group: the categories come from the permission
   * catalogue at runtime, so the control set cannot be declared up front without
   * hard-coding a list that could fall behind the catalogue.
   */
  readonly weightsForm = new FormRecord<FormControl<number>>({});
  readonly reasonCtrl = this.fb.nonNullable.control(
    '', [Validators.required, Validators.maxLength(500)]);

  /** Reactive-form validity is not a signal; the template re-reads it on events. */
  canSave(): boolean {
    return this.weightsForm.valid && this.reasonCtrl.valid;
  }

  async ngOnInit(): Promise<void> {
    void this.org.ensureOrg();
    await this.facade.load();
  }

  setBranch(value: string): void {
    this.branch.set(value);
    void this.facade.applyFilter(value, this.department());
  }

  setDepartment(value: string): void {
    this.department.set(value);
    void this.facade.applyFilter(this.branch(), value);
  }

  clear(): void {
    this.branch.set('');
    this.department.set('');
    void this.facade.clearFilter();
  }

  lower(value: string): string { return value.toLowerCase(); }

  /** Translates the server's section keys for the scope notices. */
  sectionNames(keys: readonly string[]): string {
    return keys.map((k) => this.i18n.t('qa.section.' + k.toLowerCase())).join(', ');
  }

  /** Document review-due horizon as bar buckets. */
  reviewHorizon(d: { dueWithin30: number; due31To60: number; due61To90: number }): CategoryCount[] {
    return [
      { label: this.i18n.t('qa.due30'), count: d.dueWithin30 },
      { label: this.i18n.t('qa.due60'), count: d.due31To60 },
      { label: this.i18n.t('qa.due90'), count: d.due61To90 },
    ];
  }

  /** Audit findings as a severity split; an all-zero set renders as "no findings". */
  findings(a: { majorFindings: number; minorFindings: number; observations: number }): CategoryCount[] {
    const split = [
      { label: this.i18n.t('qa.major'), count: a.majorFindings },
      { label: this.i18n.t('qa.minor'), count: a.minorFindings },
      { label: this.i18n.t('qa.observation'), count: a.observations },
    ];
    return split.some((s) => s.count > 0) ? split : [];
  }

  async openWeights(): Promise<void> {
    await this.facade.loadWeights();
    // Controls are built from whatever the catalogue currently defines, so a new
    // category appears here without a code change.
    for (const w of this.facade.weights()) {
      const control = this.weightsForm.get(w.category);
      if (control) {
        control.setValue(w.weight);
      } else {
        this.weightsForm.addControl(
          w.category,
          this.fb.nonNullable.control(
            w.weight, [Validators.required, Validators.min(0), Validators.max(100)]));
      }
    }

    this.weightsOpen.set(true);
  }

  async saveWeights(): Promise<void> {
    if (!this.canSave()) { return; }
    const raw = this.weightsForm.getRawValue();
    const weights = this.facade.weights().map((w) => ({
      category: w.category,
      weight: Number(raw[w.category] ?? w.weight),
    }));

    if (await this.facade.saveWeights(weights, this.reasonCtrl.value)) {
      this.weightsOpen.set(false);
      this.reasonCtrl.reset();
    }
  }

  /**
   * The ten §8.9.2 inputs this system holds evidence for, in clause order. A
   * clause whose section the caller cannot view is marked absent and skipped —
   * the review pack shows what the reader is entitled to see, and says nothing
   * about the rest.
   */
  readonly clauses = computed(() => {
    const a = this.facade.analytics();
    const t = (key: string) => this.i18n.t(key);
    const pct = (value: number | null) => value === null ? '—' : `${value}%`;
    if (!a) { return []; }

    return [
      {
        key: 'policy', index: 1, clause: '8.9.2 a/b', present: true,
        kpis: [
          { label: t('qa.healthScore'), value: a.health.score === null ? '—' : `${a.health.score}` },
          { label: t('qa.contributing'), value: `${a.health.contributingCategories}/${a.health.totalCategories}` },
        ],
        rows: [] as AnalyticsRow[],
      },
      {
        key: 'documents', index: 2, clause: '8.9.2 c', present: a.documentControl !== null,
        kpis: [
          { label: t('qa.pctCurrent'), value: pct(a.documentControl?.percentCurrent ?? null) },
          { label: t('qa.overdueReviews'), value: `${a.documentControl?.overdueReviews ?? 0}` },
          { label: t('qa.acksRecorded'), value: `${a.documentControl?.acknowledgementsRecorded ?? 0}` },
        ],
        rows: a.documentControl?.upcomingReviews ?? [],
      },
      {
        key: 'audits', index: 3, clause: '8.9.2 e', present: a.audits !== null,
        kpis: [
          { label: t('qa.planCompletion'), value: pct(a.audits?.planCompletionPercent ?? null) },
          { label: t('qa.major'), value: `${a.audits?.majorFindings ?? 0}` },
          { label: t('qa.minor'), value: `${a.audits?.minorFindings ?? 0}` },
          { label: t('qa.observation'), value: `${a.audits?.observations ?? 0}` },
        ],
        rows: a.audits?.recent ?? [],
      },
      {
        key: 'capa', index: 4, clause: '8.9.2 f', present: a.ncCapa !== null,
        kpis: [
          { label: t('qa.openNcs'), value: `${a.ncCapa?.openNcs ?? 0}` },
          { label: t('qa.overdueCapa'), value: `${a.ncCapa?.overdueCapa ?? 0}` },
          { label: t('qa.capaOnSchedule'), value: pct(a.ncCapa?.capaOnSchedulePercent ?? null) },
          { label: t('qa.capaOnTime'), value: pct(a.ncCapa?.capaOnTimePercent ?? null) },
        ],
        rows: a.ncCapa?.active ?? [],
      },
      {
        key: 'suppliers', index: 5, clause: '8.9.2 g', present: a.suppliers !== null,
        kpis: [
          { label: t('qa.approvedPct'), value: pct(a.suppliers?.approvedPercent ?? null) },
          {
            label: t('qa.avgEvalScore'),
            value: a.suppliers?.averageEvaluationScore === null || a.suppliers === null
              ? '—' : `${a.suppliers.averageEvaluationScore}`,
          },
          { label: t('qa.suspended'), value: `${a.suppliers?.suspended ?? 0}` },
        ],
        rows: a.suppliers?.recent ?? [],
      },
      {
        key: 'complaints', index: 6, clause: '8.9.2 j', present: a.complaints !== null,
        kpis: [
          { label: t('qa.openComplaints'), value: `${a.complaints?.open ?? 0}` },
          { label: t('qa.withinSla'), value: pct(a.complaints?.percentWithinSla ?? null) },
          {
            label: t('qa.avgResolution'),
            value: a.complaints?.averageResolutionDays == null
              ? '—' : `${a.complaints.averageResolutionDays}d`,
          },
        ],
        rows: a.complaints?.active ?? [],
      },
      {
        key: 'equipment', index: 7, clause: '8.9.2 l', present: a.equipment !== null,
        kpis: [
          { label: t('qa.calCompliance'), value: pct(a.equipment?.calibrationCompliancePercent ?? null) },
          { label: t('qa.availability'), value: pct(a.equipment?.availabilityPercent ?? null) },
          { label: t('qa.overdueCal'), value: `${a.equipment?.overdueCalibration ?? 0}` },
        ],
        rows: a.equipment?.upcomingCalibrations ?? [],
      },
      {
        key: 'risk', index: 8, clause: '8.9.2 m', present: a.risk !== null,
        kpis: [
          { label: t('qa.highRisks'), value: `${a.risk?.highOrExtreme ?? 0}` },
          { label: t('qa.highMitigated'), value: pct(a.risk?.highMitigatedPercent ?? null) },
          { label: t('qa.overdueTreatments'), value: `${a.risk?.overdueTreatments ?? 0}` },
        ],
        rows: a.risk?.top ?? [],
      },
      {
        key: 'pt', index: 9, clause: '8.9.2 n', present: a.proficiencyTesting !== null,
        kpis: [
          { label: t('qa.satisfactionRate'), value: pct(a.proficiencyTesting?.satisfactionRatePercent ?? null) },
          { label: t('qa.questionable'), value: `${a.proficiencyTesting?.questionable ?? 0}` },
          { label: t('qa.unsatisfactory'), value: `${a.proficiencyTesting?.unsatisfactory ?? 0}` },
        ],
        rows: a.proficiencyTesting?.recent ?? [],
      },
      {
        key: 'competency', index: 10, clause: '8.9.2 o', present: a.competency !== null,
        kpis: [
          { label: t('qa.pctCompetent'), value: pct(a.competency?.percentCompetent ?? null) },
          { label: t('qa.expiring90'), value: `${a.competency?.expiringWithin90 ?? 0}` },
          { label: t('qa.revoked'), value: `${a.competency?.revoked ?? 0}` },
        ],
        rows: a.competency?.recent ?? [],
      },
    ];
  });
}
