import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ReportsApiService } from '../../core/api/reports-api.service';
import { I18nService } from '../../core/i18n.service';
import { DashboardKpis, KpiHistoryPoint, NcParetoBucket, SlaCompliance } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';

/**
 * Quality dashboard fed by the Reporting read side: live KPI tiles with
 * drill-through, the NC Pareto by source, real snapshot-backed trend history
 * (shown honestly as "collecting" until the sweep has accumulated days), and
 * work-task SLA compliance. Every figure displays its freshness stamp — data
 * is computed from real rows or not shown at all.
 */
@Component({
    selector: 'qams-dashboard',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [RouterLink, DatePipe, DecimalPipe, PageHeaderComponent],
    template: `
    <qams-page-header [title]="i18n.t('dash.title')"
        [subtitle]="kpis() ? i18n.t('dash.computedAt') + ' ' + ((kpis()!.computedAtUtc | date:'medium') ?? '') : ''" />

    @if (loading()) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
    @if (kpis(); as k) {
      <div class="kpis">
        <a class="kpi blue" routerLink="/nonconformances"><div class="n">{{ k.openNcs }}</div><div class="l">{{ i18n.t('dash.openNc') }}</div></a>
        <a class="kpi red" routerLink="/nonconformances"><div class="n">{{ k.overdueCapaActions }}</div><div class="l">{{ i18n.t('dash.overdueCapa') }}</div></a>
        <a class="kpi orange" routerLink="/complaints"><div class="n">{{ k.openComplaints }}</div><div class="l">{{ i18n.t('dash.openComplaints') }}</div></a>
        <a class="kpi teal" routerLink="/audits"><div class="n">{{ k.auditsInProgress }}</div><div class="l">{{ i18n.t('dash.auditsInProgress') }}</div></a>
        <a class="kpi red" routerLink="/equipment"><div class="n">{{ k.equipmentOutOfService }}</div><div class="l">{{ i18n.t('dash.equipOos') }}</div></a>
        <a class="kpi gold" routerLink="/equipment"><div class="n">{{ k.equipmentNeedsCalibration }}</div><div class="l">{{ i18n.t('dash.equipDue') }}</div></a>
        <a class="kpi orange" routerLink="/risks"><div class="n">{{ k.highResidualRisks }}</div><div class="l">{{ i18n.t('dash.highRisks') }}</div></a>
        <a class="kpi red" routerLink="/tasks"><div class="n">{{ k.overdueTasks }}</div><div class="l">{{ i18n.t('dash.overdueTasks') }}</div></a>
        <a class="kpi slate" routerLink="/proficiency-tests"><div class="n">{{ k.ptUnsatisfactory }}</div><div class="l">{{ i18n.t('dash.ptUnsat') }}</div></a>
        <a class="kpi gold" routerLink="/training"><div class="n">{{ k.pendingTrainingAssignments }}</div><div class="l">{{ i18n.t('dash.pendingTraining') }}</div></a>
        <a class="kpi slate" routerLink="/suppliers"><div class="n">{{ k.suspendedSuppliers }}</div><div class="l">{{ i18n.t('dash.suspendedSuppliers') }}</div></a>
        <a class="kpi green" routerLink="/documents"><div class="n">{{ k.publishedDocuments }}</div><div class="l">{{ i18n.t('dash.publishedDocs') }}</div></a>
      </div>

      <div class="grid2">
        <section class="card">
          <h3>{{ i18n.t('dash.pareto') }}</h3>
          @if (pareto().length === 0) {
            <p class="muted">{{ i18n.t('dash.noNcs') }}</p>
          } @else {
            <div class="bars">
              @for (b of pareto(); track b.sourceType) {
                <div class="barrow">
                  <span class="blbl">{{ b.sourceType }}</span>
                  <div class="btrack">
                    <div class="bfill" [style.width.%]="barWidth(b)"></div>
                  </div>
                  <span class="bval">{{ b.count }}</span>
                </div>
              }
            </div>
          }
        </section>

        <section class="card">
          <h3>{{ i18n.t('dash.sla') }}</h3>
          @if (sla(); as s) {
            <div class="slagrid">
              <div>
                <div class="bign" [class.good]="s.onTimePercent >= 90" [class.bad]="s.onTimePercent < 70">
                  {{ s.completedTotal === 0 ? '—' : (s.onTimePercent | number:'1.0-1') + '%' }}
                </div>
                <div class="muted">{{ i18n.t('dash.onTime') }}</div>
              </div>
              <div class="slameta">
                <div>{{ i18n.t('dash.completed') }}: <b>{{ s.completedTotal }}</b> ({{ s.completedOnTime }} {{ i18n.t('dash.onTimeShort') }})</div>
                <div>{{ i18n.t('dash.openTasks') }}: <b>{{ s.openTotal }}</b></div>
                <div [class.bad]="s.openOverdue > 0">{{ i18n.t('dash.ofWhichOverdue') }}: <b>{{ s.openOverdue }}</b></div>
              </div>
            </div>
          }
        </section>
      </div>

      <section class="card">
        <h3>{{ i18n.t('dash.trend') }}</h3>
        @if (history().length < 2) {
          <p class="muted">{{ i18n.t('dash.trendCollecting') }}</p>
        } @else {
          <svg [attr.viewBox]="'0 0 720 180'" preserveAspectRatio="none" class="trend" role="img" aria-label="KPI trend">
            <polyline [attr.points]="trendLine('openNcs')" fill="none" stroke="#0077C2" stroke-width="2" />
            <polyline [attr.points]="trendLine('openComplaints')" fill="none" stroke="#F15A29" stroke-width="2" />
            <polyline [attr.points]="trendLine('overdueTasks')" fill="none" stroke="#DC3545" stroke-width="2" />
          </svg>
          <div class="legend">
            <span><i style="background:#0077C2"></i>{{ i18n.t('dash.openNc') }}</span>
            <span><i style="background:#F15A29"></i>{{ i18n.t('dash.openComplaints') }}</span>
            <span><i style="background:#DC3545"></i>{{ i18n.t('dash.overdueTasks') }}</span>
          </div>
        }
      </section>
    }
  `,
    styles: [`
    /* KPI tiles share the statistic-tile language of qams-list-stats: a surface
       card with the tone on a leading rail rather than a saturated block, the
       value in text ink, and proportional figures at display size.
       No meters here on purpose — DashboardKpis carries twelve bare counts with
       no denominator, and an invented ratio would be worse than an honest count. */
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(158px, 1fr)); gap: 12px; margin-bottom: 16px; }
    .kpi {
      background: var(--nt-surface); border: 1px solid var(--nt-border);
      border-inline-start: 3px solid var(--nt-ink-neutral);
      border-radius: var(--nt-radius-input); padding: 13px 16px 14px;
      box-shadow: var(--nt-shadow-xs); text-decoration: none; display: block;
      transition: box-shadow .12s, transform .12s;
    }
    .kpi:hover { box-shadow: var(--nt-shadow-md); transform: translateY(-1px); }
    .kpi.blue   { border-inline-start-color: var(--nt-ink-info); }
    .kpi.red    { border-inline-start-color: var(--nt-ink-crit); }
    .kpi.teal   { border-inline-start-color: var(--nt-ink-teal); }
    .kpi.orange { border-inline-start-color: var(--nt-ink-serious); }
    .kpi.gold   { border-inline-start-color: var(--nt-ink-warn); }
    .kpi.green  { border-inline-start-color: var(--nt-ink-ok); }
    .kpi.slate  { border-inline-start-color: var(--nt-ink-neutral); }
    .n { font-size: 26px; font-weight: 800; line-height: 1.05; color: var(--nt-navy); }
    .l { font-size: 12px; font-weight: 600; margin-top: 7px; color: var(--nt-slate); }
    .grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; align-items: start; margin-bottom: 1rem; }
    .bars { display: flex; flex-direction: column; gap: 8px; }
    .barrow { display: grid; grid-template-columns: 120px 1fr 40px; gap: 10px; align-items: center; font-size: 12px; }
    .btrack { background: var(--nt-filter-grey); border-radius: 4px; height: 18px; }
    .bfill { background: var(--nt-blue); height: 100%; border-radius: 4px; min-width: 2px; }
    .bval { text-align: end; font-weight: 700; font-variant-numeric: tabular-nums; }
    .slagrid { display: flex; gap: 24px; align-items: center; }
    .bign { font-size: 34px; font-weight: 800; font-variant-numeric: tabular-nums; color: var(--nt-slate); }
    .bign.good { color: var(--nt-green); } .bign.bad, .bad { color: var(--nt-red); }
    .slameta { display: flex; flex-direction: column; gap: 4px; font-size: 13px; }
    .trend { width: 100%; height: 180px; background: #fff; border: 1px solid var(--nt-border); border-radius: 4px; }
    .legend { display: flex; gap: 18px; margin-top: 8px; font-size: 11.5px; color: var(--nt-grey-d); }
    .legend i { display: inline-block; width: 10px; height: 10px; border-radius: 2px; margin-inline-end: 5px; }
    @media (max-width: 900px) { .grid2 { grid-template-columns: 1fr; } }
  `]
})
export class DashboardComponent implements OnInit {
  readonly i18n = inject(I18nService);
  private readonly api = inject(ReportsApiService);

  readonly loading = signal(true);
  readonly kpis = signal<DashboardKpis | null>(null);
  readonly pareto = signal<NcParetoBucket[]>([]);
  readonly sla = signal<SlaCompliance | null>(null);
  readonly history = signal<KpiHistoryPoint[]>([]);

  private readonly paretoMax = computed(() => Math.max(...this.pareto().map((b) => b.count), 1));

  async ngOnInit(): Promise<void> {
    try {
      const [kpis, pareto, sla, history] = await Promise.all([
        firstValueFrom(this.api.kpis()),
        firstValueFrom(this.api.ncPareto()),
        firstValueFrom(this.api.slaCompliance()),
        firstValueFrom(this.api.kpiHistory()),
      ]);
      this.kpis.set(kpis);
      this.pareto.set(pareto);
      this.sla.set(sla);
      this.history.set(history);
    } finally {
      this.loading.set(false);
    }
  }

  barWidth(bucket: NcParetoBucket): number {
    return (bucket.count / this.paretoMax()) * 100;
  }

  /** Projects one KPI series onto the 720×180 trend frame (y padded, x spread evenly). */
  trendLine(key: 'openNcs' | 'openComplaints' | 'overdueTasks'): string {
    const points = this.history();
    const max = Math.max(...points.map((p) => Math.max(p.openNcs, p.openComplaints, p.overdueTasks)), 1);
    const step = 704 / Math.max(points.length - 1, 1);
    return points
      .map((p, i) => `${8 + i * step},${170 - (p[key] / max) * 160}`)
      .join(' ');
  }
}
