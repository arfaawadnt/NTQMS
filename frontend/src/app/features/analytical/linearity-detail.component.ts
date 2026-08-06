import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { EsignCredentials, EsignDialogComponent } from '../../shared/ui/esign-dialog.component';
import { SignatureManifestComponent } from '../../shared/ui/signature-manifest.component';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LinearityFacade } from './linearity.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Linearity workspace (CLSI EP06): dilution-series data entry, the fitted line
 * with per-level deviation assessment against the allowable criterion, the
 * verified AMR banner, and an SVG plot of level means vs assigned values with
 * the fit and the AMR shading. Statistics come from the backend.
 */
@Component({
    selector: 'qams-linearity-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, EsignDialogComponent, SignatureManifestComponent],
    template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.studyRef + ' — ' + s.analyte" [subtitle]="s.method">
        <a routerLink="/linearity-studies" class="ghost-link">← {{ i18n.t('mc.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.state" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.state" /></div>
        <div><span class="muted">{{ i18n.t('lin.adl') }}</span> ±{{ s.allowableDeviationPct | number:'1.0-2' }}%</div>
        @if (s.slope !== null) {
          <div><span class="muted">{{ i18n.t('lin.fit') }}</span>
            <b>y = {{ s.slope | number:'1.3-4' }}x {{ (s.intercept ?? 0) >= 0 ? '+' : '−' }} {{ absv(s.intercept) | number:'1.3-4' }}</b>
          </div>
          <div><span class="muted">r</span> <b>{{ s.correlationR | number:'1.3-4' }}</b></div>
          <div>
            <span class="muted">{{ i18n.t('lin.verdict') }}</span>
            <b [class.good]="s.isLinear === true" [class.bad]="s.isLinear === false">
              {{ s.isLinear ? i18n.t('lin.linearVerdict') : i18n.t('lin.nonlinearVerdict') }}
            </b>
          </div>
        }
        @if (s.signedOffAtUtc) { <div><span class="muted">{{ i18n.t('val.signedOff') }}</span> {{ s.signedOffAtUtc | date:'medium' }}</div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      @if (s.amrLow !== null) {
        <div class="amr card" [class.restricted]="s.isLinear === false">
          <b>{{ i18n.t('lin.amr') }}:</b> {{ s.amrLow | number:'1.0-3' }} – {{ s.amrHigh | number:'1.0-3' }} {{ s.unit }}
          @if (s.isLinear === false) { <span class="muted"> · {{ i18n.t('lin.restrictedNote') }}</span> }
        </div>
      } @else if (s.isLinear === false) {
        <div class="amr card restricted"><b>{{ i18n.t('lin.amr') }}:</b> {{ i18n.t('lin.noAmr') }}</div>
      }

      @if (s.levels.length > 0) {
        <div class="plots">
          <section class="card">
            <h3>{{ i18n.t('lin.plot') }}</h3>
            <svg [attr.viewBox]="'0 0 ' + W + ' ' + H" class="plot">
              <!-- verified AMR band -->
              @if (s.amrLow !== null) {
                <rect [attr.x]="px(s.amrLow!)" y="8" [attr.width]="px(s.amrHigh!) - px(s.amrLow!)" [attr.height]="H - PAD - 8" class="amrband" />
              }
              <line [attr.x1]="PAD" [attr.y1]="H - PAD" [attr.x2]="W - 8" [attr.y2]="H - PAD" class="axis" />
              <line [attr.x1]="PAD" [attr.y1]="8" [attr.x2]="PAD" [attr.y2]="H - PAD" class="axis" />
              <!-- fitted line across the assigned span -->
              @if (s.slope !== null) {
                <line [attr.x1]="px(minAssigned())" [attr.y1]="py(s.slope! * minAssigned() + s.intercept!)"
                      [attr.x2]="px(maxAssigned())" [attr.y2]="py(s.slope! * maxAssigned() + s.intercept!)" class="fitline" />
              }
              <!-- raw replicates (small) + level means (large, colored by verdict) -->
              @for (m of s.measurements; track m.id) {
                <circle [attr.cx]="px(m.assignedValue)" [attr.cy]="py(m.measuredValue)" r="2.2" class="rep" />
              }
              @for (l of s.levels; track l.assignedValue) {
                <circle [attr.cx]="px(l.assignedValue)" [attr.cy]="py(l.meanMeasured)" r="4"
                        [class]="l.passes ? 'lvl pass' : 'lvl fail'" />
              }
              <text [attr.x]="W / 2" [attr.y]="H - 4" class="axlabel">{{ i18n.t('lin.assigned') }} ({{ s.unit }})</text>
              <text [attr.x]="-(H / 2)" y="12" transform="rotate(-90)" class="axlabel">{{ i18n.t('lin.measured') }} ({{ s.unit }})</text>
            </svg>
            <div class="legend">
              <span class="k fit"></span>{{ i18n.t('lin.fit') }}
              <span class="dotk pass"></span>{{ i18n.t('lin.levelPass') }}
              <span class="dotk fail"></span>{{ i18n.t('lin.levelFail') }}
              <span class="k band"></span>{{ i18n.t('lin.amr') }}
            </div>
          </section>

          <section class="card">
            <h3>{{ i18n.t('lin.levels') }}</h3>
            <table>
              <thead><tr>
                <th>{{ i18n.t('lin.assigned') }}</th><th>n</th><th>{{ i18n.t('lin.mean') }}</th>
                <th>{{ i18n.t('lin.fitted') }}</th><th>{{ i18n.t('lin.deviation') }}</th>
                <th>{{ i18n.t('lin.recovery') }}</th><th>{{ i18n.t('val.verdict') }}</th>
              </tr></thead>
              <tbody>
                @for (l of s.levels; track l.assignedValue) {
                  <tr>
                    <td><b>{{ l.assignedValue | number:'1.0-3' }}</b></td>
                    <td>{{ l.replicateCount }}</td>
                    <td>{{ l.meanMeasured | number:'1.0-4' }}</td>
                    <td class="muted">{{ l.fittedValue | number:'1.0-4' }}</td>
                    <td [class.bad]="!l.passes">{{ l.deviationPct | number:'1.0-3' }}%</td>
                    <td>{{ l.recoveryPct | number:'1.0-2' }}%</td>
                    <td>
                      @if (l.passes) { <qams-status-pill status="Pass" /> }
                      @else { <qams-status-pill status="Failed" /> }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </section>
        </div>
      }

      <section class="card">
        <h3>{{ i18n.t('lin.measurements') }} ({{ s.measurements.length }})</h3>
        @if (s.measurements.length === 0) { <p class="muted">{{ i18n.t('lin.noMeasurements') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('lin.assigned') }}</th><th>{{ i18n.t('lin.measured') }}</th><th></th>
            </tr></thead>
            <tbody>
              @for (m of s.measurements; track m.id) {
                <tr>
                  <td>{{ m.assignedValue | number:'1.0-3' }}</td>
                  <td>{{ m.measuredValue | number:'1.0-4' }}</td>
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
        @if (s.state !== 'SignedOff') {
          <form [formGroup]="measurementForm" (ngSubmit)="add(s.id)">
            <div class="pair">
              <div><label>{{ i18n.t('lin.assigned') }} ({{ s.unit }})</label><input type="number" step="any" formControlName="assignedValue" /></div>
              <div><label>{{ i18n.t('lin.measured') }} ({{ s.unit }})</label><input type="number" step="any" formControlName="measuredValue" /></div>
            </div>
            <div class="hint">{{ i18n.t('lin.entryHint') }}</div>
            <button type="submit" [disabled]="measurementForm.invalid">{{ i18n.t('lin.addMeasurement') }}</button>
          </form>
        }
      </section>

      <section class="card">
        <h3>{{ i18n.t('val.workflow') }}</h3>
        <div class="actions">
          @if (s.state !== 'SignedOff') {
            <button (click)="facade.calculate(s.id)" [disabled]="distinctLevels() < 4">{{ i18n.t('lin.calculate') }}</button>
            @if (distinctLevels() < 4) { <span class="muted">{{ i18n.t('lin.minLevels') }}</span> }
          }
          @if (s.state === 'Calculated' && perms.can('analytical-quality.sign')) {
            <button (click)="esignOpen.set(true)">{{ i18n.t('mc.signOff') }}</button>
            <qams-esign-dialog [open]="esignOpen()" [meaning]="i18n.t('esign.aqMeaning')" [busy]="facade.loading()" [error]="facade.error()" (confirm)="doSignOff(s.id, $event)" (cancel)="esignOpen.set(false)" />
          }
          @if (s.state === 'SignedOff') { <p class="muted">{{ i18n.t('mc.signedOffNote') }}</p> }
        </div>
      </section>

      <qams-signature-manifest [subjectUrl]="'/api/linearity-studies/' + s.id + '/signatures'" />

      <qams-audit-trail [subject]="s.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .good { color: var(--nt-green); }
    .bad { color: var(--nt-red); }
    .amr { margin-bottom: 1rem; padding: 10px 16px; border-inline-start: 4px solid var(--nt-teal); }
    .amr.restricted { border-inline-start-color: var(--nt-red); }
    .plots { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; margin-bottom: 1rem; align-items: start; }
    .plot { width: 100%; height: auto; }
    .axis { stroke: var(--nt-border); stroke-width: 1; }
    .fitline { stroke: var(--nt-blue); stroke-width: 2; }
    .amrband { fill: var(--nt-teal); opacity: .08; }
    .rep { fill: var(--nt-grey-m); opacity: .55; }
    .lvl.pass { fill: var(--nt-teal); }
    .lvl.fail { fill: var(--nt-red); }
    .axlabel { font-size: 10px; fill: var(--nt-grey-m); text-anchor: middle; }
    .legend { font-size: 11px; color: var(--nt-grey-m); display: flex; gap: 14px; align-items: center; margin-top: 4px; flex-wrap: wrap; }
    .k { display: inline-block; width: 14px; height: 3px; margin-inline-end: 4px; vertical-align: middle; }
    .k.fit { background: var(--nt-blue); }
    .k.band { background: var(--nt-teal); opacity: .35; height: 10px; }
    .dotk { display: inline-block; width: 9px; height: 9px; border-radius: 50%; margin-inline-end: 4px; }
    .dotk.pass { background: var(--nt-teal); }
    .dotk.fail { background: var(--nt-red); }
    section { margin-bottom: 1rem; }
    .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .danger-link { color: var(--nt-red); }
    .actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    .actions button { width: auto; }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 900px) { .plots, .pair { grid-template-columns: 1fr; } }
  `]
})
export class LinearityDetailComponent implements OnInit {
  readonly facade = inject(LinearityFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound study id. */
  readonly id = input.required<string>();

  /** Whether the Part 11 e-signature dialog is open for the sign-off. */
  readonly esignOpen = signal(false);

  /** Signs off through the ceremony dialog; closes on success, stays open (showing the error) on failure. */
  async doSignOff(id: string, credentials: EsignCredentials): Promise<void> {
    await this.facade.signOff(id, credentials);
    if (this.facade.error() === '') { this.esignOpen.set(false); }
  }

  readonly flowSteps = ['DataEntry', 'Calculated', 'SignedOff'] as const;
  readonly item = this.facade.selected;

  // Plot geometry.
  readonly W = 380;
  readonly H = 300;
  readonly PAD = 34;

  readonly measurementForm = this.fb.nonNullable.group({
    assignedValue: [null as number | null, [Validators.required, Validators.min(0.0001)]],
    measuredValue: [null as number | null, [Validators.required]],
  });

  readonly distinctLevels = computed(() =>
    new Set((this.item()?.measurements ?? []).map((m) => m.assignedValue)).size);

  readonly minAssigned = computed(() => {
    const values = (this.item()?.measurements ?? []).map((m) => m.assignedValue);
    return values.length ? Math.min(...values) : 0;
  });
  readonly maxAssigned = computed(() => {
    const values = (this.item()?.measurements ?? []).map((m) => m.assignedValue);
    const max = values.length ? Math.max(...values) : 1;
    return max === this.minAssigned() ? max + 1 : max;
  });
  private readonly maxMeasured = computed(() => {
    const values = (this.item()?.measurements ?? []).map((m) => m.measuredValue);
    return values.length ? Math.max(...values) : 1;
  });
  private readonly minMeasured = computed(() => {
    const values = (this.item()?.measurements ?? []).map((m) => m.measuredValue);
    return values.length ? Math.min(...values) : 0;
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  absv(v: number | null): number { return Math.abs(v ?? 0); }

  px(v: number): number { return this.map(v, this.minAssigned(), this.maxAssigned(), this.PAD, this.W - 8); }
  py(v: number): number {
    const lo = Math.min(this.minMeasured(), 0);
    const hi = this.maxMeasured() === lo ? lo + 1 : this.maxMeasured();
    return this.map(v, lo, hi, this.H - this.PAD, 8);
  }

  private map(v: number, lo: number, hi: number, plo: number, phi: number): number {
    if (hi === lo) { return (plo + phi) / 2; }
    return plo + ((v - lo) / (hi - lo)) * (phi - plo);
  }

  async add(id: string): Promise<void> {
    if (this.measurementForm.invalid) { return; }
    const raw = this.measurementForm.getRawValue();
    await this.facade.addMeasurement(id, raw.assignedValue!, raw.measuredValue!);
    // Keep the assigned level so replicates of the same level enter quickly.
    this.measurementForm.patchValue({ measuredValue: null });
  }
}
