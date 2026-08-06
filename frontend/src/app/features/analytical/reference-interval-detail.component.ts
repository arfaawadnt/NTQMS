import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { EsignCredentials, EsignDialogComponent } from '../../shared/ui/esign-dialog.component';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ReferenceIntervalFacade } from './reference-interval.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Reference-interval verification workspace (CLSI EP28): the claimed interval,
 * verification-sample entry, the outside-count-vs-allowance verdict, and a
 * dot-strip distribution plot showing each sample against the shaded interval
 * band so the outliers are visible. Statistics come from the backend.
 */
@Component({
    selector: 'qams-reference-interval-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, EsignDialogComponent],
    template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.studyRef + ' — ' + s.analyte" [subtitle]="s.population + ' · ' + s.source">
        <a routerLink="/reference-intervals" class="ghost-link">← {{ i18n.t('ri.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.state" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.state" /></div>
        <div><span class="muted">{{ i18n.t('ri.claimed') }}</span> <b>{{ s.claimedLower | number:'1.0-3' }} – {{ s.claimedUpper | number:'1.0-3' }} {{ s.unit }}</b></div>
        <div><span class="muted">{{ i18n.t('ri.samples') }}</span> {{ s.samples.length }}</div>
        @if (s.outsideCount !== null) {
          <div><span class="muted">{{ i18n.t('ri.outside') }}</span>
            <b [class.bad]="s.verdict === 'Rejected'" [class.good]="s.verdict === 'Verified'">{{ s.outsideCount }} / {{ s.allowedOutside }}</b>
          </div>
          <div>
            <span class="muted">{{ i18n.t('val.verdict') }}</span>
            @if (s.verdict === 'Verified') { <qams-status-pill status="Verified" /> }
            @else { <qams-status-pill status="Rejected" /> }
          </div>
        }
        @if (s.signedOffAtUtc) { <div><span class="muted">{{ i18n.t('val.signedOff') }}</span> {{ s.signedOffAtUtc | date:'medium' }}</div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      @if (s.verdict) {
        <div class="banner card" [class.ok]="s.verdict === 'Verified'" [class.rej]="s.verdict === 'Rejected'">
          @if (s.verdict === 'Verified') { {{ i18n.t('ri.verifiedNote') }} }
          @else { {{ i18n.t('ri.rejectedNote') }} }
        </div>
      }

      @if (s.samples.length > 0) {
        <section class="card">
          <h3>{{ i18n.t('ri.distribution') }}</h3>
          <svg [attr.viewBox]="'0 0 ' + W + ' ' + H" class="plot">
            <!-- interval band -->
            <rect [attr.x]="sx(s.claimedLower)" y="20" [attr.width]="Math.max(1, sx(s.claimedUpper) - sx(s.claimedLower))" [attr.height]="H - 50" class="band" />
            <line [attr.x1]="sx(s.claimedLower)" y1="16" [attr.x2]="sx(s.claimedLower)" [attr.y2]="H - 26" class="limit" />
            <line [attr.x1]="sx(s.claimedUpper)" y1="16" [attr.x2]="sx(s.claimedUpper)" [attr.y2]="H - 26" class="limit" />
            <text [attr.x]="sx(s.claimedLower)" y="12" class="limlabel">{{ s.claimedLower | number:'1.0-2' }}</text>
            <text [attr.x]="sx(s.claimedUpper)" y="12" class="limlabel">{{ s.claimedUpper | number:'1.0-2' }}</text>
            <line [attr.x1]="PAD" [attr.y1]="H - 26" [attr.x2]="W - 8" [attr.y2]="H - 26" class="axis" />
            @for (p of jittered(); track p.id) {
              <circle [attr.cx]="sx(p.value)" [attr.cy]="p.cy" r="3.5" [class]="p.outside ? 'dot out' : 'dot in'" />
            }
            <text [attr.x]="W / 2" [attr.y]="H - 6" class="axlabel">{{ i18n.t('lin.measured') }} ({{ s.unit }})</text>
          </svg>
          <div class="legend">
            <span class="dotk in"></span>{{ i18n.t('ri.inside') }}
            <span class="dotk out"></span>{{ i18n.t('ri.outsideDot') }}
            <span class="k band"></span>{{ i18n.t('ri.claimed') }}
          </div>
        </section>
      }

      <section class="card">
        <h3>{{ i18n.t('ri.samples') }} ({{ s.samples.length }})</h3>
        @if (s.samples.length === 0) { <p class="muted">{{ i18n.t('ri.noSamples') }}</p> }
        @else {
          <table>
            <thead><tr><th>{{ i18n.t('ri.subject') }}</th><th>{{ i18n.t('lin.measured') }}</th><th>{{ i18n.t('ri.position') }}</th><th></th></tr></thead>
            <tbody>
              @for (p of s.samples; track p.id) {
                <tr>
                  <td class="muted">{{ p.subjectRef ?? '—' }}</td>
                  <td>{{ p.value | number:'1.0-3' }}</td>
                  <td>
                    @if (p.outside) { <span class="bad">{{ i18n.t('ri.outsideDot') }}</span> }
                    @else { <span class="good">{{ i18n.t('ri.inside') }}</span> }
                  </td>
                  <td>
                    @if (s.state !== 'SignedOff') {
                      <button class="link danger-link" type="button" (click)="facade.removeSample(s.id, p.id)">✕</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
        @if (s.state !== 'SignedOff') {
          <form [formGroup]="sampleForm" (ngSubmit)="add(s.id)">
            <div class="pair">
              <div><label>{{ i18n.t('ri.subject') }}</label><input formControlName="subjectRef" [placeholder]="i18n.t('common.optional')" /></div>
              <div><label>{{ i18n.t('lin.measured') }} ({{ s.unit }})</label><input type="number" step="any" formControlName="value" /></div>
            </div>
            <div class="hint">{{ i18n.t('ri.entryHint') }}</div>
            <button type="submit" [disabled]="sampleForm.invalid">{{ i18n.t('ri.addSample') }}</button>
          </form>
        }
      </section>

      <section class="card">
        <h3>{{ i18n.t('val.workflow') }}</h3>
        <div class="actions">
          @if (s.state !== 'SignedOff') {
            <button (click)="facade.calculate(s.id)" [disabled]="s.samples.length < 20">{{ i18n.t('ri.calculate') }}</button>
            @if (s.samples.length < 20) { <span class="muted">{{ i18n.t('ri.minSamples') }} ({{ s.samples.length }}/20)</span> }
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
    .good { color: var(--nt-green); }
    .bad { color: var(--nt-red); }
    .banner { margin-bottom: 1rem; padding: 10px 16px; font-weight: 600; }
    .banner.ok { border-inline-start: 4px solid var(--nt-green); color: var(--nt-green); }
    .banner.rej { border-inline-start: 4px solid var(--nt-red); color: var(--nt-red); }
    .plot { width: 100%; height: auto; }
    .axis { stroke: var(--nt-border); stroke-width: 1; }
    .band { fill: var(--nt-teal); opacity: .1; }
    .limit { stroke: var(--nt-teal); stroke-width: 1.5; stroke-dasharray: 4 3; }
    .limlabel { font-size: 9px; fill: var(--nt-teal); text-anchor: middle; }
    .dot.in { fill: var(--nt-teal); opacity: .85; }
    .dot.out { fill: var(--nt-red); }
    .axlabel { font-size: 10px; fill: var(--nt-grey-m); text-anchor: middle; }
    .legend { font-size: 11px; color: var(--nt-grey-m); display: flex; gap: 14px; align-items: center; margin-top: 4px; flex-wrap: wrap; }
    .dotk { display: inline-block; width: 9px; height: 9px; border-radius: 50%; margin-inline-end: 4px; }
    .dotk.in { background: var(--nt-teal); } .dotk.out { background: var(--nt-red); }
    .k { display: inline-block; width: 14px; height: 10px; margin-inline-end: 4px; vertical-align: middle; background: var(--nt-teal); opacity: .3; }
    section { margin-bottom: 1rem; }
    .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .danger-link { color: var(--nt-red); }
    .actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    .actions button { width: auto; }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 700px) { .pair { grid-template-columns: 1fr; } }
  `]
})
export class ReferenceIntervalDetailComponent implements OnInit {
  readonly facade = inject(ReferenceIntervalFacade);
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
  readonly Math = Math;

  // Plot geometry.
  readonly W = 700;
  readonly H = 150;
  readonly PAD = 30;

  readonly sampleForm = this.fb.nonNullable.group({
    subjectRef: [''],
    value: [null as number | null, [Validators.required]],
  });

  /** X-axis span padded beyond the interval so out-of-range points stay visible. */
  private readonly range = computed(() => {
    const s = this.item();
    if (!s) { return { lo: 0, hi: 1 }; }
    const values = s.samples.map((p) => p.value);
    const lo = Math.min(s.claimedLower, ...values);
    const hi = Math.max(s.claimedUpper, ...values);
    const pad = (hi - lo) * 0.08 || 1;
    return { lo: lo - pad, hi: hi + pad };
  });

  /** Vertical jitter so overlapping values are legible in the dot strip. */
  readonly jittered = computed(() => {
    const s = this.item();
    if (!s) { return []; }
    const rows = 6;
    return s.samples.map((p, i) => ({
      id: p.id, value: p.value, outside: p.outside,
      cy: 34 + (i % rows) * ((this.H - 70) / rows),
    }));
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  sx(v: number): number {
    const { lo, hi } = this.range();
    if (hi === lo) { return (this.PAD + this.W - 8) / 2; }
    return this.PAD + ((v - lo) / (hi - lo)) * (this.W - 8 - this.PAD);
  }

  async add(id: string): Promise<void> {
    if (this.sampleForm.invalid) { return; }
    const raw = this.sampleForm.getRawValue();
    await this.facade.addSample(id, raw.value!, raw.subjectRef.trim() || null);
    this.sampleForm.reset();
  }
}
