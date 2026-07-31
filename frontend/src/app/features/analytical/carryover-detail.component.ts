import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CarryoverFacade } from './carryover.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { CARRYOVER_KINDS } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Carryover workspace (CLSI EP10-style): a high sample followed by low
 * replicates ordered by sequence. The backend derives carryover% =
 * (firstLow − steadyLow) / (meanHigh − steadyLow) × 100 and the pass/fail
 * verdict against the allowable limit.
 */
@Component({
    selector: 'qams-carryover-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
    template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.studyRef + ' — ' + s.analyte" [subtitle]="i18n.t('car.allowable') + ': ' + s.allowableCarryoverPct + '%'">
        <a routerLink="/carryover-studies" class="ghost-link">← {{ i18n.t('car.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.state" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.state" /></div>
        <div><span class="muted">{{ i18n.t('car.readings') }}</span> {{ s.readings.length }}</div>
        @if (s.carryoverPct !== null) {
          <div><span class="muted">{{ i18n.t('car.carryover') }}</span> <b>{{ s.carryoverPct | number:'1.2-4' }}%</b></div>
          <div><span class="muted">{{ i18n.t('val.verdict') }}</span>
            @if (s.passes) { <qams-status-pill status="Pass" /> } @else { <qams-status-pill status="Failed" /> }
          </div>
        }
        @if (s.signedOffAtUtc) { <div><span class="muted">{{ i18n.t('val.signedOff') }}</span> {{ s.signedOffAtUtc | date:'medium' }}</div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      @if (s.carryoverPct !== null) {
        <div class="components">
          <div class="comp card"><span class="muted">{{ i18n.t('car.meanHigh') }}</span><b>{{ s.meanHigh | number:'1.0-3' }}</b><span class="sub">{{ s.unit }}</span></div>
          <div class="comp card"><span class="muted">{{ i18n.t('car.firstLow') }}</span><b>{{ s.firstLow | number:'1.0-3' }}</b><span class="sub">{{ s.unit }}</span></div>
          <div class="comp card"><span class="muted">{{ i18n.t('car.steadyLow') }}</span><b>{{ s.steadyLow | number:'1.0-3' }}</b><span class="sub">{{ s.unit }}</span></div>
        </div>
      }

      <section class="card">
        <h3>{{ i18n.t('car.readings') }} ({{ s.readings.length }})</h3>
        @if (s.state !== 'SignedOff') {
          <form [formGroup]="entryForm" (ngSubmit)="add(s.id)">
            <div class="triple">
              <div><label>{{ i18n.t('car.kind') }}</label>
                <select formControlName="kind">
                  @for (k of kinds; track k) { <option [value]="k">{{ k === 'High' ? i18n.t('car.high') : i18n.t('car.low') }}</option> }
                </select>
              </div>
              <div><label>{{ i18n.t('car.sequence') }}</label><input type="number" step="1" formControlName="sequence" /></div>
              <div><label>{{ i18n.t('out.value') }} ({{ s.unit }})</label><input type="number" step="any" formControlName="value" /></div>
            </div>
            <div class="hint">{{ i18n.t('car.seqHint') }}</div>
            <button type="submit" [disabled]="entryForm.invalid">{{ i18n.t('car.addReading') }}</button>
          </form>
        }
        @if (s.readings.length > 0) {
          <table class="mtable">
            <thead><tr><th>{{ i18n.t('car.sequence') }}</th><th>{{ i18n.t('car.kind') }}</th><th>{{ i18n.t('out.value') }}</th><th></th></tr></thead>
            <tbody>
              @for (r of s.readings; track r.id) {
                <tr>
                  <td>{{ r.sequence }}</td>
                  <td><span class="tag" [class.high]="r.kind === 'High'">{{ r.kind === 'High' ? i18n.t('car.high') : i18n.t('car.low') }}</span></td>
                  <td>{{ r.value | number:'1.0-4' }}</td>
                  <td>
                    @if (s.state !== 'SignedOff') {
                      <button class="link danger-link" type="button" (click)="facade.removeReading(s.id, r.id)">✕</button>
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
            <button (click)="facade.calculate(s.id)" [disabled]="!canCalculate()">{{ i18n.t('car.calculate') }}</button>
            @if (!canCalculate()) { <span class="muted">{{ i18n.t('car.minReadings') }}</span> }
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
    .comp b { font-size: 1.3rem; color: var(--nt-navy-deep); }
    .comp .sub { font-size: .72rem; color: var(--nt-grey-m); }
    section { margin-bottom: 1rem; }
    .triple { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 1rem; }
    .mtable { margin-top: 1rem; }
    .tag { font-size: .72rem; padding: 2px 8px; border-radius: 10px; background: var(--nt-filter-grey); }
    .tag.high { background: color-mix(in srgb, var(--nt-blue) 18%, transparent); color: var(--nt-navy-deep); }
    .danger-link { color: var(--nt-red); }
    .actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    .actions button { width: auto; }
    form { border-bottom: 1px solid var(--nt-border); padding-bottom: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 800px) { .components, .triple { grid-template-columns: 1fr; } }
  `]
})
export class CarryoverDetailComponent implements OnInit {
  readonly facade = inject(CarryoverFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  readonly id = input.required<string>();

  readonly flowSteps = ['DataEntry', 'Calculated', 'SignedOff'] as const;
  readonly kinds = CARRYOVER_KINDS;
  readonly item = this.facade.selected;

  readonly entryForm = this.fb.nonNullable.group({
    kind: ['Low' as string, [Validators.required]],
    sequence: [null as number | null, [Validators.required]],
    value: [null as number | null, [Validators.required]],
  });

  /** EP10 needs one high reading and at least three low readings. */
  readonly canCalculate = computed(() => {
    const rs = this.item()?.readings ?? [];
    return rs.some((r) => r.kind === 'High') && rs.filter((r) => r.kind === 'Low').length >= 3;
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  async add(id: string): Promise<void> {
    if (this.entryForm.invalid) { return; }
    const raw = this.entryForm.getRawValue();
    await this.facade.addReading(id, raw.kind, raw.sequence!, raw.value!);
    // Advance the sequence and keep the kind for quick sequential entry.
    this.entryForm.patchValue({ sequence: (raw.sequence ?? 0) + 1, value: null });
  }
}
