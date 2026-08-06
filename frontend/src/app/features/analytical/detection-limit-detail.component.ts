import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { EsignCredentials, EsignDialogComponent } from '../../shared/ui/esign-dialog.component';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { DetectionLimitFacade } from './detection-limit.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { DETECTION_SAMPLE_KINDS, DetectionSampleKind } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Detection-capability workspace (CLSI EP17): blank + low-level replicate
 * entry, the derived LoB / LoD / LoQ result cards, and a precision profile
 * (CV% vs concentration) with the CV goal and LoD/LoQ markers so the
 * functional-sensitivity decision is visible. Statistics come from the backend.
 */
@Component({
    selector: 'qams-detection-limit-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, EsignDialogComponent],
    template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.studyRef + ' — ' + s.analyte" [subtitle]="s.method">
        <a routerLink="/detection-limits" class="ghost-link">← {{ i18n.t('dl.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.state" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.state" /></div>
        <div><span class="muted">{{ i18n.t('dl.cvTarget') }}</span> ≤ {{ s.loqCvTargetPct | number:'1.0-2' }}%</div>
        <div><span class="muted">{{ i18n.t('dl.blanks') }}</span> {{ blankCount() }}</div>
        <div><span class="muted">{{ i18n.t('dl.lowLevels') }}</span> {{ lowCount() }}</div>
        @if (s.signedOffAtUtc) { <div><span class="muted">{{ i18n.t('val.signedOff') }}</span> {{ s.signedOffAtUtc | date:'medium' }}</div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      @if (s.lod !== null) {
        <div class="results">
          <div class="rescard card"><span class="muted">LoB</span><b>{{ s.lob | number:'1.0-4' }} {{ s.unit }}</b><span class="sub">mean + 1.645·SD</span></div>
          <div class="rescard card"><span class="muted">LoD</span><b>{{ s.lod | number:'1.0-4' }} {{ s.unit }}</b><span class="sub">LoB + 1.645·SD_low</span></div>
          <div class="rescard card" [class.bad]="s.loq === null">
            <span class="muted">LoQ</span>
            @if (s.loq !== null) { <b>{{ s.loq | number:'1.0-4' }} {{ s.unit }}</b><span class="sub">{{ i18n.t('dl.functional') }} ≤{{ s.loqCvTargetPct }}% CV</span> }
            @else { <b class="warn">{{ i18n.t('dl.notEstablished') }}</b><span class="sub">{{ i18n.t('dl.loqMissingHint') }}</span> }
          </div>
        </div>

        @if (s.lowLevels.length > 0) {
          <div class="grid">
            <section class="card">
              <h3>{{ i18n.t('dl.precisionProfile') }}</h3>
              <svg [attr.viewBox]="'0 0 ' + W + ' ' + H" class="plot">
                <!-- CV goal line -->
                <line [attr.x1]="PAD" [attr.y1]="cvY(s.loqCvTargetPct)" [attr.x2]="W - 8" [attr.y2]="cvY(s.loqCvTargetPct)" class="goal" />
                <text [attr.x]="W - 10" [attr.y]="cvY(s.loqCvTargetPct) - 4" class="goallabel">{{ s.loqCvTargetPct }}% CV</text>
                <!-- LoD marker -->
                @if (inXRange(s.lod!)) {
                  <line [attr.x1]="cx(s.lod!)" y1="8" [attr.x2]="cx(s.lod!)" [attr.y2]="H - PAD" class="lod" />
                  <text [attr.x]="cx(s.lod!)" y="16" class="marklabel">LoD</text>
                }
                <line [attr.x1]="PAD" [attr.y1]="H - PAD" [attr.x2]="W - 8" [attr.y2]="H - PAD" class="axis" />
                <line [attr.x1]="PAD" [attr.y1]="8" [attr.x2]="PAD" [attr.y2]="H - PAD" class="axis" />
                @for (l of s.lowLevels; track l.assignedValue) {
                  <circle [attr.cx]="cx(l.assignedValue)" [attr.cy]="cvY(l.cvPct)" r="4"
                          [class]="l.qualifiesForLoq ? 'lvl pass' : 'lvl fail'" />
                }
                <text [attr.x]="W / 2" [attr.y]="H - 4" class="axlabel">{{ i18n.t('dl.concentration') }} ({{ s.unit }})</text>
                <text [attr.x]="-(H / 2)" y="12" transform="rotate(-90)" class="axlabel">CV (%)</text>
              </svg>
              <div class="legend">
                <span class="dotk pass"></span>{{ i18n.t('dl.qualifies') }}
                <span class="dotk fail"></span>{{ i18n.t('dl.belowGoal') }}
                <span class="k goal"></span>{{ i18n.t('dl.cvGoal') }}
              </div>
            </section>

            <section class="card">
              <h3>{{ i18n.t('dl.lowLevelTable') }}</h3>
              <table>
                <thead><tr>
                  <th>{{ i18n.t('dl.concentration') }}</th><th>n</th><th>{{ i18n.t('lin.mean') }}</th>
                  <th>SD</th><th>CV%</th><th>{{ i18n.t('dl.loqCandidate') }}</th>
                </tr></thead>
                <tbody>
                  @for (l of s.lowLevels; track l.assignedValue) {
                    <tr>
                      <td><b>{{ l.assignedValue | number:'1.0-3' }}</b></td>
                      <td>{{ l.replicateCount }}</td>
                      <td>{{ l.mean | number:'1.0-4' }}</td>
                      <td class="muted">{{ l.sd | number:'1.0-4' }}</td>
                      <td [class.bad]="l.cvPct > s.loqCvTargetPct">{{ l.cvPct | number:'1.0-2' }}%</td>
                      <td>
                        @if (l.qualifiesForLoq) { <qams-status-pill status="Pass" /> }
                        @else { <qams-status-pill status="Failed" /> }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </section>
          </div>
        }
      }

      <section class="card">
        <h3>{{ i18n.t('dl.dataEntry') }}</h3>
        @if (s.state !== 'SignedOff') {
          <form [formGroup]="entryForm" (ngSubmit)="add(s.id)">
            <div class="quad">
              <div>
                <label>{{ i18n.t('dl.sampleKind') }}</label>
                <select formControlName="kind">
                  <option value="Blank">{{ i18n.t('dl.blank') }}</option>
                  <option value="LowLevel">{{ i18n.t('dl.lowLevel') }}</option>
                </select>
              </div>
              @if (entryForm.controls.kind.value === 'LowLevel') {
                <div><label>{{ i18n.t('dl.assignedConc') }} ({{ s.unit }})</label><input type="number" step="any" formControlName="assignedValue" /></div>
              }
              <div><label>{{ i18n.t('lin.measured') }} ({{ s.unit }})</label><input type="number" step="any" formControlName="measuredValue" /></div>
            </div>
            <div class="hint">{{ i18n.t('dl.entryHint') }}</div>
            <button type="submit" [disabled]="entryForm.invalid">{{ i18n.t('dl.addMeasurement') }}</button>
          </form>
        }
        @if (s.measurements.length > 0) {
          <table class="mtable">
            <thead><tr>
              <th>{{ i18n.t('dl.sampleKind') }}</th><th>{{ i18n.t('dl.assignedConc') }}</th><th>{{ i18n.t('lin.measured') }}</th><th></th>
            </tr></thead>
            <tbody>
              @for (m of s.measurements; track m.id) {
                <tr>
                  <td>{{ m.kind === 'Blank' ? i18n.t('dl.blank') : i18n.t('dl.lowLevel') }}</td>
                  <td class="muted">{{ m.assignedValue !== null ? (m.assignedValue | number:'1.0-3') : '—' }}</td>
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
      </section>

      <section class="card">
        <h3>{{ i18n.t('val.workflow') }}</h3>
        <div class="actions">
          @if (s.state !== 'SignedOff') {
            <button (click)="facade.calculate(s.id)" [disabled]="blankCount() < 10 || lowCount() < 10">{{ i18n.t('dl.calculate') }}</button>
            @if (blankCount() < 10 || lowCount() < 10) { <span class="muted">{{ i18n.t('dl.minReplicates') }}</span> }
          }
          @if (s.state === 'Calculated' && perms.can('analytical-quality.sign')) {
            <button (click)="esignOpen.set(true)">{{ i18n.t('mc.signOff') }}</button>
            <qams-esign-dialog [open]="esignOpen()" [meaning]="i18n.t('esign.aqMeaning')" [busy]="facade.loading()" [error]="facade.error()" (confirm)="doSignOff(s.id, $event)" (cancel)="esignOpen.set(false)" />
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
    .results { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem; margin-bottom: 1rem; }
    .rescard { display: flex; flex-direction: column; gap: 2px; padding: 14px 16px; }
    .rescard b { font-size: 1.3rem; color: var(--nt-navy-deep); }
    .rescard.bad { border-inline-start: 4px solid var(--nt-red); }
    .rescard .sub { font-size: .7rem; color: var(--nt-grey-m); }
    .warn { color: var(--nt-red); font-size: 1rem; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; margin-bottom: 1rem; align-items: start; }
    .plot { width: 100%; height: auto; }
    .axis { stroke: var(--nt-border); stroke-width: 1; }
    .goal { stroke: var(--nt-orange, #ef6c00); stroke-width: 1.4; stroke-dasharray: 5 3; }
    .goallabel { font-size: 9px; fill: var(--nt-orange, #ef6c00); text-anchor: end; }
    .lod { stroke: var(--nt-blue); stroke-width: 1.2; stroke-dasharray: 3 3; }
    .marklabel { font-size: 9px; fill: var(--nt-blue); text-anchor: middle; }
    .lvl.pass { fill: var(--nt-teal); }
    .lvl.fail { fill: var(--nt-red); }
    .axlabel { font-size: 10px; fill: var(--nt-grey-m); text-anchor: middle; }
    .legend { font-size: 11px; color: var(--nt-grey-m); display: flex; gap: 14px; align-items: center; margin-top: 4px; flex-wrap: wrap; }
    .k { display: inline-block; width: 14px; height: 3px; margin-inline-end: 4px; vertical-align: middle; }
    .k.goal { background: var(--nt-orange, #ef6c00); }
    .dotk { display: inline-block; width: 9px; height: 9px; border-radius: 50%; margin-inline-end: 4px; }
    .dotk.pass { background: var(--nt-teal); } .dotk.fail { background: var(--nt-red); }
    section { margin-bottom: 1rem; }
    .quad { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem; }
    .mtable { margin-top: 1rem; }
    .bad { color: var(--nt-red); }
    .danger-link { color: var(--nt-red); }
    .actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    .actions button { width: auto; }
    form { border-bottom: 1px solid var(--nt-border); padding-bottom: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 900px) { .results, .grid, .quad { grid-template-columns: 1fr; } }
  `]
})
export class DetectionLimitDetailComponent implements OnInit {
  readonly facade = inject(DetectionLimitFacade);
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
  readonly kinds = DETECTION_SAMPLE_KINDS;
  readonly item = this.facade.selected;

  // Plot geometry.
  readonly W = 380;
  readonly H = 300;
  readonly PAD = 34;

  readonly entryForm = this.fb.nonNullable.group({
    kind: ['LowLevel' as DetectionSampleKind, [Validators.required]],
    assignedValue: [null as number | null],
    measuredValue: [null as number | null, [Validators.required]],
  });

  readonly blankCount = computed(() =>
    (this.item()?.measurements ?? []).filter((m) => m.kind === 'Blank').length);
  readonly lowCount = computed(() =>
    (this.item()?.measurements ?? []).filter((m) => m.kind === 'LowLevel').length);

  private readonly concentrations = computed(() => this.item()?.lowLevels.map((l) => l.assignedValue) ?? []);
  private readonly maxCv = computed(() => {
    const s = this.item();
    if (!s) { return 1; }
    const cvs = [...s.lowLevels.map((l) => l.cvPct), s.loqCvTargetPct];
    return Math.max(1, ...cvs);
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  inXRange(v: number): boolean {
    const xs = this.concentrations();
    return xs.length > 0 && v >= Math.min(...xs) && v <= Math.max(...xs);
  }

  cx(v: number): number {
    const xs = this.concentrations();
    const lo = xs.length ? Math.min(...xs) : 0;
    const hi = xs.length ? Math.max(...xs) : 1;
    return this.map(v, lo, hi === lo ? lo + 1 : hi, this.PAD, this.W - 8);
  }
  cvY(v: number): number { return this.map(v, 0, this.maxCv() * 1.1, this.H - this.PAD, 8); }

  private map(v: number, lo: number, hi: number, plo: number, phi: number): number {
    if (hi === lo) { return (plo + phi) / 2; }
    return plo + ((v - lo) / (hi - lo)) * (phi - plo);
  }

  async add(id: string): Promise<void> {
    const raw = this.entryForm.getRawValue();
    if (raw.measuredValue === null) { return; }
    const assigned = raw.kind === 'LowLevel' ? raw.assignedValue : null;
    if (raw.kind === 'LowLevel' && assigned === null) { return; }
    await this.facade.addMeasurement(id, raw.kind, assigned, raw.measuredValue);
    // Keep kind + level so replicates enter quickly; clear the reading.
    this.entryForm.patchValue({ measuredValue: null });
  }
}
