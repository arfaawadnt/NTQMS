import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { IndicatorsFacade } from './indicators.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

interface PlotPoint { x: number; y: number; special: boolean; value: number; period: string; rules: string; }
interface ChartGeom {
  w: number; h: number; padL: number; padB: number;
  meanY: number; uclY: number; lclY: number; u2Y: number; l2Y: number;
  targetY: number | null; actionY: number | null;
  path: string; points: PlotPoint[];
}

/**
 * Quality indicator workspace (HQMS M06): the data dictionary, the target/threshold
 * editor, a statistical-process-control chart that separates real signal from noise,
 * and period measurement entry. Editing is privilege-gated (affordance only).
 */
@Component({
    selector: 'qams-indicators-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, AuditTrailComponent],
    template: `
    @if (indicator(); as n) {
      <qams-page-header [title]="n.code + ' — ' + n.name">
        <a routerLink="/indicators" class="ghost-link">← {{ i18n.t('qi.backToList') }}</a>
      </qams-page-header>

      <div class="meta">
        <div><span class="muted">{{ i18n.t('qi.status') }}</span><qams-status-pill [status]="n.status" /></div>
        <div><span class="muted">{{ i18n.t('qi.frequency') }}</span> {{ i18n.t('qi.freq.' + n.frequency) }}</div>
        <div><span class="muted">{{ i18n.t('qi.direction') }}</span> {{ i18n.t('qi.dir.' + n.direction) }}</div>
        <div><span class="muted">{{ i18n.t('qi.unit') }}</span> {{ n.unit }}</div>
      </div>

      <!-- Control chart -->
      <section class="card">
        <h3>{{ i18n.t('qi.controlChart') }}</h3>
        @if (geom(); as g) {
          <svg [attr.viewBox]="'0 0 ' + g.w + ' ' + g.h" class="spc" role="img" [attr.aria-label]="i18n.t('qi.controlChart')">
            <!-- limit + reference lines -->
            <line [attr.x1]="g.padL" [attr.x2]="g.w - 8" [attr.y1]="g.uclY" [attr.y2]="g.uclY" class="ucl" />
            <line [attr.x1]="g.padL" [attr.x2]="g.w - 8" [attr.y1]="g.lclY" [attr.y2]="g.lclY" class="ucl" />
            <line [attr.x1]="g.padL" [attr.x2]="g.w - 8" [attr.y1]="g.u2Y" [attr.y2]="g.u2Y" class="sigma2" />
            <line [attr.x1]="g.padL" [attr.x2]="g.w - 8" [attr.y1]="g.l2Y" [attr.y2]="g.l2Y" class="sigma2" />
            <line [attr.x1]="g.padL" [attr.x2]="g.w - 8" [attr.y1]="g.meanY" [attr.y2]="g.meanY" class="mean" />
            @if (g.targetY !== null) { <line [attr.x1]="g.padL" [attr.x2]="g.w - 8" [attr.y1]="g.targetY" [attr.y2]="g.targetY" class="target" /> }
            @if (g.actionY !== null) { <line [attr.x1]="g.padL" [attr.x2]="g.w - 8" [attr.y1]="g.actionY" [attr.y2]="g.actionY" class="action" /> }
            <!-- series -->
            <path [attr.d]="g.path" class="series" />
            @for (p of g.points; track p.period) {
              <circle [attr.cx]="p.x" [attr.cy]="p.y" [attr.r]="p.special ? 5 : 3.5"
                      [class.special]="p.special"><title>{{ p.period | date:'MMM y' }}: {{ p.value | number:'1.0-2' }}{{ p.rules ? ' — ' + p.rules : '' }}</title></circle>
            }
            <!-- axis labels -->
            <text [attr.x]="g.padL - 6" [attr.y]="g.uclY + 3" class="axl end">UCL {{ facade.chart()!.ucl | number:'1.0-1' }}</text>
            <text [attr.x]="g.padL - 6" [attr.y]="g.meanY + 3" class="axl end">x̄ {{ facade.chart()!.mean | number:'1.0-1' }}</text>
            <text [attr.x]="g.padL - 6" [attr.y]="g.lclY + 3" class="axl end">LCL {{ facade.chart()!.lcl | number:'1.0-1' }}</text>
          </svg>
          <p class="legend">
            <span class="k mean">— {{ i18n.t('qi.mean') }}</span>
            <span class="k ucl">— {{ i18n.t('qi.controlLimits') }}</span>
            @if (g.targetY !== null) { <span class="k target">— {{ i18n.t('qi.target') }}</span> }
            @if (g.actionY !== null) { <span class="k action">— {{ i18n.t('qi.actionThreshold') }}</span> }
            <span class="k special">● {{ i18n.t('qi.specialCause') }}</span>
          </p>
        } @else {
          <p class="muted">{{ i18n.t('qi.needMoreData') }}</p>
        }
      </section>

      <div class="grid">
        <!-- Data dictionary -->
        <section class="card">
          <h3>{{ i18n.t('qi.dataDictionary') }}</h3>
          @if (n.description) { <p>{{ n.description }}</p> }
          <dl>
            <dt>{{ i18n.t('qi.numerator') }}</dt><dd>{{ n.numerator }}</dd>
            <dt>{{ i18n.t('qi.denominator') }}</dt><dd>{{ n.denominator }}</dd>
            <dt>{{ i18n.t('qi.rateFactor') }}</dt><dd>× {{ n.rateFactor | number:'1.0-2' }}</dd>
            @if (n.inclusions) { <dt>{{ i18n.t('qi.inclusions') }}</dt><dd>{{ n.inclusions }}</dd> }
            @if (n.exclusions) { <dt>{{ i18n.t('qi.exclusions') }}</dt><dd>{{ n.exclusions }}</dd> }
            @if (n.dataSource) { <dt>{{ i18n.t('qi.dataSource') }}</dt><dd>{{ n.dataSource }}</dd> }
          </dl>
        </section>

        <!-- Targets + measurement entry -->
        <section class="card">
          <h3>{{ i18n.t('qi.targets') }}</h3>
          @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
          @if (perms.can('indicators.edit') && n.status === 'Active') {
            <form [formGroup]="targetForm" (ngSubmit)="saveTargets(n.id)">
              <div class="trio">
                <div><label>{{ i18n.t('qi.target') }}</label><input type="number" formControlName="target" /></div>
                <div><label>{{ i18n.t('qi.warningThreshold') }}</label><input type="number" formControlName="warningThreshold" /></div>
                <div><label>{{ i18n.t('qi.actionThreshold') }}</label><input type="number" formControlName="actionThreshold" /></div>
              </div>
              <button type="submit">{{ i18n.t('qi.saveTargets') }}</button>
            </form>
          } @else {
            <dl>
              <dt>{{ i18n.t('qi.target') }}</dt><dd>{{ n.target !== null ? (n.target | number:'1.0-2') : '—' }}</dd>
              <dt>{{ i18n.t('qi.warningThreshold') }}</dt><dd>{{ n.warningThreshold !== null ? (n.warningThreshold | number:'1.0-2') : '—' }}</dd>
              <dt>{{ i18n.t('qi.actionThreshold') }}</dt><dd>{{ n.actionThreshold !== null ? (n.actionThreshold | number:'1.0-2') : '—' }}</dd>
            </dl>
          }

          @if (perms.can('indicators.create') && n.status === 'Active') {
            <h3>{{ i18n.t('qi.recordMeasurement') }}</h3>
            <form [formGroup]="measureForm" (ngSubmit)="record(n.id)">
              <label>{{ i18n.t('qi.period') }}</label>
              @if (n.frequency === 'Monthly') {
                <input type="month" formControlName="period" />
              } @else {
                <input type="date" formControlName="period" />
              }
              <div class="trio">
                <div><label>{{ i18n.t('qi.numeratorValue') }}</label><input type="number" formControlName="numerator" /></div>
                <div><label>{{ i18n.t('qi.denominatorValue') }}</label><input type="number" formControlName="denominator" /></div>
              </div>
              <label>{{ i18n.t('qi.note') }}</label>
              <input formControlName="note" />
              <button type="submit" [disabled]="measureForm.invalid">{{ i18n.t('qi.addMeasurement') }}</button>
            </form>
          }

          @if (perms.can('indicators.void') && n.status === 'Active') {
            <button class="danger retire" (click)="facade.retire(n.id)">{{ i18n.t('qi.retire') }}</button>
          }
        </section>
      </div>

      <!-- Measurement history -->
      <section class="card">
        <h3>{{ i18n.t('qi.measurements') }}</h3>
        @if (n.measurements.length === 0) { <p class="muted">{{ i18n.t('qi.noMeasurements') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('qi.period') }}</th><th>{{ i18n.t('qi.numeratorValue') }}</th>
              <th>{{ i18n.t('qi.denominatorValue') }}</th><th>{{ i18n.t('qi.value') }}</th>
              <th>{{ i18n.t('qi.latestStatus') }}</th><th>{{ i18n.t('qi.note') }}</th>
            </tr></thead>
            <tbody>
              @for (m of measurementsDesc(); track m.id) {
                <tr>
                  <td>{{ m.period | date:'MMM y' }}</td>
                  <td>{{ m.numerator | number:'1.0-2' }}</td>
                  <td>{{ m.denominator | number:'1.0-2' }}</td>
                  <td><b>{{ m.value | number:'1.0-2' }}</b> {{ n.unit }}</td>
                  <td><qams-status-pill [status]="m.status" /></td>
                  <td>{{ m.note || '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>

      <qams-audit-trail [subject]="n.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; align-items: start; }
    dl { display: grid; grid-template-columns: auto 1fr; gap: .3rem .8rem; margin: 0; }
    dt { font-weight: 600; color: var(--nt-ink-neutral); }
    dd { margin: 0; }
    .trio { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: .5rem; }
    .actions form, form { margin-top: .5rem; }
    button { width: auto; margin-top: .5rem; }
    .retire { margin-top: 1rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    h3 { margin-top: 1rem; }
    .spc { width: 100%; height: auto; overflow: visible; }
    .spc .series { fill: none; stroke: var(--nt-ink-info); stroke-width: 1.5; }
    .spc circle { fill: var(--nt-ink-info); }
    .spc circle.special { fill: var(--nt-ink-crit); stroke: var(--nt-ink-crit); }
    .spc .mean { stroke: var(--nt-ink-neutral); stroke-width: 1; }
    .spc .ucl { stroke: var(--nt-ink-crit); stroke-width: 1; stroke-dasharray: 5 3; }
    .spc .sigma2 { stroke: var(--nt-border); stroke-width: 1; stroke-dasharray: 2 3; }
    .spc .target { stroke: var(--nt-ink-ok); stroke-width: 1; stroke-dasharray: 6 3; }
    .spc .action { stroke: var(--nt-ink-warn); stroke-width: 1; stroke-dasharray: 6 3; }
    .spc .axl { font-size: 9px; fill: var(--nt-ink-neutral); }
    .spc .axl.end { text-anchor: end; }
    .legend { display: flex; flex-wrap: wrap; gap: 1rem; font-size: .8rem; margin-top: .5rem; }
    .legend .mean { color: var(--nt-ink-neutral); } .legend .ucl { color: var(--nt-ink-crit); }
    .legend .target { color: var(--nt-ink-ok); } .legend .action { color: var(--nt-ink-warn); }
    .legend .special { color: var(--nt-ink-crit); }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class IndicatorsDetailComponent implements OnInit {
  readonly facade = inject(IndicatorsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  readonly id = input.required<string>();
  readonly indicator = this.facade.selected;

  /** Measurements newest-first for the history table (the chart uses chronological order). */
  readonly measurementsDesc = computed(() =>
    [...(this.indicator()?.measurements ?? [])].sort((a, b) => b.period.localeCompare(a.period)));

  readonly targetForm = this.fb.group({
    target: this.fb.control<number | null>(null),
    warningThreshold: this.fb.control<number | null>(null),
    actionThreshold: this.fb.control<number | null>(null),
  });

  readonly measureForm = this.fb.nonNullable.group({
    period: ['', [Validators.required]],
    numerator: [0, [Validators.required, Validators.min(0)]],
    denominator: [1, [Validators.required, Validators.min(0.0001)]],
    note: ['', [Validators.maxLength(2000)]],
  });

  /** Control-chart geometry mapped from the SPC analysis; null when there are too few points. */
  readonly geom = computed<ChartGeom | null>(() => {
    const c = this.facade.chart();
    if (!c || !c.hasLimits || c.points.length < 2) { return null; }

    const w = 720, h = 260, padL = 56, padR = 8, padT = 12, padB = 24;
    const plotW = w - padL - padR, plotH = h - padT - padB;

    const candidates = [
      c.ucl, c.lcl, c.mean, ...c.points.map((p) => p.value),
      ...(c.target !== null ? [c.target] : []),
      ...(c.actionThreshold !== null ? [c.actionThreshold] : []),
    ];
    let yMin = Math.min(...candidates), yMax = Math.max(...candidates);
    if (yMin === yMax) { yMin -= 1; yMax += 1; }
    const pad = (yMax - yMin) * 0.08;
    yMin -= pad; yMax += pad;

    const yOf = (v: number): number => padT + (plotH * (yMax - v)) / (yMax - yMin);
    const xOf = (i: number): number => padL + (plotW * i) / (c.points.length - 1);

    const points: PlotPoint[] = c.points.map((p, i) => ({
      x: xOf(i), y: yOf(p.value), special: p.specialCause, value: p.value, period: p.period,
      rules: p.rules.join(', '),
    }));
    const path = points.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ');

    return {
      w, h, padL, padB,
      meanY: yOf(c.mean), uclY: yOf(c.ucl), lclY: yOf(c.lcl), u2Y: yOf(c.upper2Sigma), l2Y: yOf(c.lower2Sigma),
      targetY: c.target !== null ? yOf(c.target) : null,
      actionY: c.actionThreshold !== null ? yOf(c.actionThreshold) : null,
      path, points,
    };
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id()).then(() => {
      const n = this.indicator();
      if (n) {
        // M-17: seed the form from the record — saving untouched fields used
        // to null the thresholds and silently kill breach grading.
        this.targetForm.patchValue({
          target: n.target, warningThreshold: n.warningThreshold, actionThreshold: n.actionThreshold,
        });
      }
    });
  }

  async saveTargets(id: string): Promise<void> {
    const v = this.targetForm.getRawValue();
    await this.facade.setTargets(id, {
      target: v.target, warningThreshold: v.warningThreshold, actionThreshold: v.actionThreshold,
    });
  }

  async record(id: string): Promise<void> {
    if (this.measureForm.invalid) { return; }
    const raw = this.measureForm.getRawValue();
    // A month input yields "YYYY-MM"; the API takes the period's first day
    // (the server normalizes by frequency either way — M-17).
    const period = raw.period.length === 7 ? `${raw.period}-01` : raw.period;
    await this.facade.recordMeasurement(id, {
      period, numerator: raw.numerator, denominator: raw.denominator, note: raw.note || null,
    });
    if (this.facade.error() === '') { this.measureForm.reset({ period: '', numerator: 0, denominator: 1, note: '' }); }
  }
}
