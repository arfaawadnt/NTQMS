import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { SigmaFacade } from './sigma.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Sigma-metric workspace: TEa / bias / CV inputs, the derived σ shown on a
 * graded gauge (0–6+ with the Westgard performance zones), the grade, and the
 * sigma-based QC-design recommendation. Inputs are editable while Draft; the
 * sigma and grade come from the backend.
 */
@Component({
    selector: 'qams-sigma-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
    template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.assessmentRef + ' — ' + s.analyte" [subtitle]="i18n.t('sig.subtitle')">
        <a routerLink="/sigma-metrics" class="ghost-link">← {{ i18n.t('sig.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.state" />

      <div class="hero card">
        <div class="gauge">
          <div class="value" [class]="'grade-' + s.grade.toLowerCase()">{{ s.sigmaValue | number:'1.1-2' }}<span>σ</span></div>
          <div class="grade">{{ i18n.t('sig.grade' + s.grade) }}</div>
          <svg viewBox="0 0 300 40" class="bar">
            <!-- zone bands 0..6+ -->
            <rect x="0" y="14" width="150" height="12" class="z-unacceptable" />
            <rect x="150" y="14" width="50" height="12" class="z-marginal" />
            <rect x="200" y="14" width="50" height="12" class="z-good" />
            <rect x="250" y="14" width="50" height="12" class="z-excellent" />
            @for (t of [1,2,3,4,5,6]; track t) {
              <line [attr.x1]="t*50" y1="12" [attr.x2]="t*50" y2="28" class="tick" />
              <text [attr.x]="t*50" y="38" class="tlabel">{{ t }}</text>
            }
            <polygon [attr.points]="markerPoints()" class="marker" />
          </svg>
        </div>
        <div class="inputs">
          <div><span class="muted">{{ i18n.t('sig.tea') }}</span> <b>{{ s.allowableTotalErrorPct | number:'1.0-2' }}%</b></div>
          <div><span class="muted">{{ i18n.t('sig.bias') }}</span> <b>{{ s.biasPct | number:'1.0-2' }}%</b></div>
          <div><span class="muted">{{ i18n.t('sig.cv') }}</span> <b>{{ s.cvPct | number:'1.0-2' }}%</b></div>
          <div class="formula muted">σ = (TEa − |bias|) / CV</div>
        </div>
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <div class="qc card" [class.bad]="s.sigmaValue < 3">
        <h3>{{ i18n.t('sig.qcRecommendation') }}</h3>
        <p>{{ s.qcRecommendation }}</p>
      </div>

      @if (s.state === 'Draft' && perms.can('analytical-quality.edit')) {
        <section class="card">
          <h3>{{ i18n.t('sig.editInputs') }}</h3>
          <form [formGroup]="form" (ngSubmit)="save(s.id)">
            <div class="trio">
              <div><label>{{ i18n.t('sig.tea') }}</label><input type="number" step="any" formControlName="allowableTotalErrorPct" /></div>
              <div><label>{{ i18n.t('sig.bias') }}</label><input type="number" step="any" formControlName="biasPct" /></div>
              <div><label>{{ i18n.t('sig.cv') }}</label><input type="number" step="any" formControlName="cvPct" /></div>
            </div>
            <button type="submit" class="secondary" [disabled]="form.invalid">{{ i18n.t('sig.recalculate') }}</button>
          </form>
        </section>
      }

      <section class="card">
        <h3>{{ i18n.t('val.workflow') }}</h3>
        <div class="actions">
          @if (s.state === 'Draft' && perms.can('analytical-quality.sign')) {
            <button (click)="facade.signOff(s.id)">{{ i18n.t('mc.signOff') }}</button>
          }
          @if (s.state === 'SignedOff') {
            <p class="muted">{{ i18n.t('mc.signedOffNote') }}
              @if (s.signedOffAtUtc) { · {{ s.signedOffAtUtc | date:'medium' }} }
            </p>
          }
        </div>
      </section>

      <qams-audit-trail [subject]="s.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .hero { display: flex; flex-wrap: wrap; gap: 2rem; align-items: center; margin-bottom: 1rem; }
    .gauge { flex: 1; min-width: 260px; }
    .value { font-size: 2.6rem; font-weight: 800; line-height: 1; }
    .value span { font-size: 1.2rem; margin-inline-start: 4px; }
    .grade-worldclass, .grade-excellent { color: var(--nt-green); }
    .grade-good { color: var(--nt-teal); }
    .grade-marginal { color: var(--nt-orange, #ef6c00); }
    .grade-unacceptable { color: var(--nt-red); }
    .grade { font-size: .9rem; font-weight: 600; color: var(--nt-slate); margin: 2px 0 10px; }
    .bar { width: 100%; max-width: 340px; height: auto; }
    .z-unacceptable { fill: rgba(198,40,40,.25); }
    .z-marginal { fill: rgba(239,108,0,.25); }
    .z-good { fill: rgba(0,178,169,.25); }
    .z-excellent { fill: rgba(46,125,50,.25); }
    .tick { stroke: var(--nt-border); stroke-width: 1; }
    .tlabel { font-size: 8px; fill: var(--nt-grey-m); text-anchor: middle; }
    .marker { fill: var(--nt-navy-deep); }
    .inputs { display: flex; flex-direction: column; gap: 6px; min-width: 180px; }
    .inputs .muted { font-size: .72rem; display: block; }
    .formula { margin-top: 6px; font-family: var(--nt-mono); font-size: .8rem; }
    .qc { margin-bottom: 1rem; border-inline-start: 4px solid var(--nt-teal); }
    .qc.bad { border-inline-start-color: var(--nt-red); }
    .qc h3 { margin-top: 0; }
    section { margin-bottom: 1rem; }
    .trio { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem; }
    form button { width: auto; margin-top: .5rem; }
    .actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    .actions button { width: auto; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 700px) { .trio { grid-template-columns: 1fr; } }
  `]
})
export class SigmaDetailComponent implements OnInit {
  readonly facade = inject(SigmaFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound assessment id. */
  readonly id = input.required<string>();

  readonly flowSteps = ['Draft', 'SignedOff'] as const;
  readonly item = this.facade.selected;

  readonly form = this.fb.nonNullable.group({
    allowableTotalErrorPct: [null as number | null, [Validators.required, Validators.min(0.0001)]],
    biasPct: [0, [Validators.required]],
    cvPct: [null as number | null, [Validators.required, Validators.min(0.0001)]],
  });

  /** Gauge pointer: σ clamped to the 0–6 scale (each σ = 50px on the 300-wide bar). */
  readonly markerPoints = computed(() => {
    const s = this.item();
    const sigma = Math.min(6, Math.max(0, s?.sigmaValue ?? 0));
    const x = sigma * 50;
    return `${x - 5},8 ${x + 5},8 ${x},14`;
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id()).then(() => {
      const s = this.item();
      if (s) {
        this.form.setValue({
          allowableTotalErrorPct: s.allowableTotalErrorPct,
          biasPct: s.biasPct,
          cvPct: s.cvPct,
        });
      }
    });
  }

  async save(id: string): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    await this.facade.updateInputs(id, raw.allowableTotalErrorPct!, raw.biasPct, raw.cvPct!);
  }
}
