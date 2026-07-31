import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MonitoringFacade } from './monitoring.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { OrgDataService } from '../../core/org-data.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Monitoring point workspace: acceptance window, reading capture, the recent
 * reading history with frozen in/out verdicts, limit re-baselining, and the
 * lifecycle actions. Excursions open an NC on the backend — the form only
 * states that consequence.
 */
@Component({
    selector: 'qams-monitoring-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
    template: `
    @if (item(); as p) {
      <qams-page-header [title]="p.pointRef + ' — ' + p.name" [subtitle]="p.parameter + ' · ' + (p.location ?? '')">
        <a routerLink="/monitoring" class="ghost-link">← {{ i18n.t('env.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="p.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="p.status" /></div>
        <div><span class="muted">{{ i18n.t('env.window') }}</span> <b>{{ window(p.lowLimit, p.highLimit, p.unit) }}</b></div>
        @if (latest(); as last) {
          <div>
            <span class="muted">{{ i18n.t('env.lastReading') }}</span>
            <b [class.bad]="!last.inLimit" [class.good]="last.inLimit">{{ last.value | number:'1.0-2' }} {{ p.unit }}</b>
            <span class="muted"> · {{ last.recordedAtUtc | date:'short' }}</span>
          </div>
        }
        <div class="lifecycle">
          @if (p.status === 'Active' && perms.can('monitoring-points.edit')) {
            <button class="secondary" (click)="facade.suspend(p.id)">{{ i18n.t('env.suspendPoint') }}</button>
          }
          @if (p.status === 'Suspended' && perms.can('monitoring-points.edit')) {
            <button (click)="facade.resume(p.id)">{{ i18n.t('env.resumePoint') }}</button>
          }
          @if (p.status !== 'Retired' && perms.can('monitoring-points.void')) {
            <button class="secondary" (click)="facade.retire(p.id)">{{ i18n.t('std.retire') }}</button>
          }
        </div>
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      @if (p.status === 'Active') {
        <section class="card">
          <h3>{{ i18n.t('env.recordReading') }}</h3>
          <form [formGroup]="readingForm" (ngSubmit)="record(p.id)">
            <div class="pair">
              <div><label>{{ i18n.t('env.value') }} ({{ p.unit }})</label><input type="number" step="any" formControlName="value" /></div>
              <div><label>{{ i18n.t('equip.remarks') }}</label><input formControlName="remark" /></div>
            </div>
            <div class="hint">{{ i18n.t('env.excursionNote') }}</div>
            <button type="submit" [disabled]="readingForm.invalid">{{ i18n.t('env.record') }}</button>
          </form>
        </section>
      }

      <section class="card">
        <h3>{{ i18n.t('env.readings') }} <span class="muted">({{ i18n.t('env.readingWindow') }})</span></h3>
        @if (p.readings.length === 0) { <p class="muted">{{ i18n.t('env.noReadings') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('env.recordedAt') }}</th><th>{{ i18n.t('env.value') }}</th>
              <th>{{ i18n.t('val.verdict') }}</th><th>{{ i18n.t('equip.performedBy') }}</th><th>{{ i18n.t('equip.remarks') }}</th>
            </tr></thead>
            <tbody>
              @for (r of p.readings; track r.id) {
                <tr>
                  <td>{{ r.recordedAtUtc | date:'medium' }}</td>
                  <td [class.bad]="!r.inLimit" [class.good]="r.inLimit"><b>{{ r.value | number:'1.0-2' }} {{ p.unit }}</b></td>
                  <td>
                    @if (r.inLimit) { <qams-status-pill status="Pass" /> }
                    @else { <qams-status-pill status="Failed" /> }
                  </td>
                  <td>{{ org.userName(r.recordedById) || '—' }}</td>
                  <td class="muted">{{ r.remark ?? '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>

      @if (p.status !== 'Retired' && perms.can('monitoring-points.edit')) {
        <section class="card">
          <h3>{{ i18n.t('env.rebaseline') }}</h3>
          <form [formGroup]="limitsForm" (ngSubmit)="setLimits(p.id)">
            <div class="pair">
              <div><label>{{ i18n.t('env.lowLimit') }}</label><input type="number" step="any" formControlName="lowLimit" /></div>
              <div><label>{{ i18n.t('env.highLimit') }}</label><input type="number" step="any" formControlName="highLimit" /></div>
            </div>
            <div class="hint">{{ i18n.t('env.rebaselineHint') }}</div>
            <button type="submit" class="secondary">{{ i18n.t('env.applyLimits') }}</button>
          </form>
        </section>
      }

      <qams-audit-trail [subject]="p.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .lifecycle { display: flex; gap: .5rem; margin-inline-start: auto; }
    .lifecycle button { width: auto; }
    section { margin-bottom: 1rem; }
    .pair { display: grid; grid-template-columns: 1fr 2fr; gap: 1rem; }
    .good { color: var(--nt-green); }
    .bad { color: var(--nt-red); }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 700px) { .pair { grid-template-columns: 1fr; } }
  `]
})
export class MonitoringDetailComponent implements OnInit {
  readonly facade = inject(MonitoringFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  readonly org = inject(OrgDataService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound monitoring point id. */
  readonly id = input.required<string>();

  /** Canonical lifecycle for the stepper (Suspended renders off-path). */
  readonly flowSteps = ['Active', 'Retired'] as const;

  readonly item = this.facade.selected;
  readonly latest = computed(() => this.item()?.readings[0] ?? null);

  readonly readingForm = this.fb.nonNullable.group({
    value: [null as number | null, [Validators.required]],
    remark: ['', [Validators.maxLength(1000)]],
  });
  readonly limitsForm = this.fb.nonNullable.group({
    lowLimit: [null as number | null],
    highLimit: [null as number | null],
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
    void this.org.ensureDirectory();
  }

  window(low: number | null, high: number | null, unit: string): string {
    if (low !== null && high !== null) { return `${low}–${high} ${unit}`; }
    return low !== null ? `≥ ${low} ${unit}` : high !== null ? `≤ ${high} ${unit}` : '—';
  }

  async record(id: string): Promise<void> {
    if (this.readingForm.invalid) { return; }
    const raw = this.readingForm.getRawValue();
    await this.facade.recordReading(id, raw.value!, raw.remark.trim() || null);
    this.readingForm.reset();
  }

  async setLimits(id: string): Promise<void> {
    const raw = this.limitsForm.getRawValue();
    await this.facade.setLimits(id, raw.lowLimit, raw.highLimit);
    this.limitsForm.reset();
  }
}
