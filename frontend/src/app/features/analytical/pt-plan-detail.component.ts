import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { PtPlansFacade } from './pt-plans.facade';
import { AnalyticalApiService } from '../../core/api/analytical-api.service';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PtEnrollment } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * PT plan workspace: the committed scheme/analyte lines (editable in Draft,
 * frozen on approval), fulfilment recording against resulted enrollments, and
 * QM closure with the coverage summary. Unfulfilled lines stay visible as gaps.
 */
@Component({
    selector: 'qams-pt-plan-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
    template: `
    @if (item(); as p) {
      <qams-page-header [title]="p.planRef + ' — ' + p.year" [subtitle]="i18n.t('ptp.subtitle')">
        <a routerLink="/pt-plans" class="ghost-link">← {{ i18n.t('ptp.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="p.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="p.status" /></div>
        @if (p.approvedAtUtc) { <div><span class="muted">{{ i18n.t('val.signedOff') }}</span> {{ p.approvedAtUtc | date:'medium' }}</div> }
        @if (p.status === 'Draft' && perms.canApprove()) {
          <button (click)="facade.approve(p.id)" [disabled]="p.items.length === 0">{{ i18n.t('ptp.approve') }}</button>
        }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <section class="card">
        <h3>{{ i18n.t('ptp.lines') }} ({{ p.items.length }})</h3>
        @if (p.items.length === 0) { <p class="muted">{{ i18n.t('ptp.noLines') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('pt.scheme') }}</th><th>{{ i18n.t('qc.analyte') }}</th><th>{{ i18n.t('ptp.provider') }}</th>
              <th>{{ i18n.t('ptp.coverage') }}</th><th>{{ i18n.t('ptp.lastCycle') }}</th><th></th>
            </tr></thead>
            <tbody>
              @for (item of p.items; track item.id) {
                <tr>
                  <td>{{ item.scheme }}</td>
                  <td>{{ item.analyte }}</td>
                  <td class="muted">{{ item.provider ?? '—' }}</td>
                  <td>
                    <b [class.good]="item.fulfilledCycles >= item.plannedCycles"
                       [class.gap]="p.status === 'Closed' && item.fulfilledCycles < item.plannedCycles">
                      {{ item.fulfilledCycles }}/{{ item.plannedCycles }}
                    </b>
                  </td>
                  <td class="code">{{ item.lastEnrollmentRef ?? '—' }}</td>
                  <td>
                    @if (p.status === 'Draft') {
                      <button class="link danger-link" type="button" (click)="facade.removeItem(p.id, item.id)">✕</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
        @if (p.status === 'Draft' && perms.canAssignTraining()) {
          <form [formGroup]="itemForm" (ngSubmit)="addItem(p.id)">
            <div class="grid5">
              <div><label>{{ i18n.t('pt.scheme') }}</label><input formControlName="scheme" /></div>
              <div><label>{{ i18n.t('qc.analyte') }}</label><input formControlName="analyte" /></div>
              <div><label>{{ i18n.t('ptp.provider') }}</label><input formControlName="provider" /></div>
              <div><label>{{ i18n.t('ptp.cycles') }}</label><input type="number" min="1" max="52" formControlName="plannedCycles" /></div>
              <div><label>{{ i18n.t('ptp.notes') }}</label><input formControlName="notes" /></div>
            </div>
            <button type="submit" [disabled]="itemForm.invalid">{{ i18n.t('ptp.addLine') }}</button>
          </form>
        }
      </section>

      @if (p.status === 'Approved') {
        <section class="card">
          <h3>{{ i18n.t('ptp.recordFulfilment') }}</h3>
          <form [formGroup]="fulfilForm" (ngSubmit)="fulfil(p.id)">
            <div class="pair">
              <div>
                <label>{{ i18n.t('ptp.line') }}</label>
                <select formControlName="itemId">
                  <option value="">—</option>
                  @for (item of p.items; track item.id) {
                    <option [value]="item.id">{{ item.scheme }} / {{ item.analyte }} ({{ item.fulfilledCycles }}/{{ item.plannedCycles }})</option>
                  }
                </select>
              </div>
              <div>
                <label>{{ i18n.t('ptp.enrollment') }}</label>
                <select formControlName="enrollmentId">
                  <option value="">—</option>
                  @for (e of resulted(); track e.id) {
                    <option [value]="e.id">{{ e.ptRef }} — {{ e.scheme }}/{{ e.analyte }} ({{ e.performance }})</option>
                  }
                </select>
              </div>
            </div>
            <div class="hint">{{ i18n.t('ptp.fulfilHint') }}</div>
            <button type="submit" [disabled]="fulfilForm.invalid">{{ i18n.t('ptp.count') }}</button>
          </form>
        </section>

        @if (perms.canApprove()) {
          <section class="card">
            <h3>{{ i18n.t('ptp.close') }}</h3>
            <form [formGroup]="closeForm" (ngSubmit)="close(p.id)">
              <label>{{ i18n.t('ptp.closureSummary') }}</label>
              <input formControlName="closureSummary" [placeholder]="i18n.t('ptp.closureHint')" />
              <button type="submit" class="secondary" [disabled]="closeForm.invalid">{{ i18n.t('ptp.closeBtn') }}</button>
            </form>
          </section>
        }
      }
      @if (p.status === 'Closed' && p.closureSummary) {
        <section class="card">
          <h3>{{ i18n.t('ptp.closureSummary') }}</h3>
          <p>{{ p.closureSummary }}</p>
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
    .meta button { width: auto; margin-inline-start: auto; }
    section { margin-bottom: 1rem; }
    .grid5 { display: grid; grid-template-columns: 2fr 1.5fr 1.5fr .8fr 2fr; gap: 1rem; }
    .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .good { color: var(--nt-green); }
    .gap { color: var(--nt-red); }
    .danger-link { color: var(--nt-red); }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 900px) { .grid5, .pair { grid-template-columns: 1fr 1fr; } }
  `]
})
export class PtPlanDetailComponent implements OnInit {
  readonly facade = inject(PtPlansFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly analyticalApi = inject(AnalyticalApiService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound plan id. */
  readonly id = input.required<string>();

  /** Canonical workflow path for the stepper. */
  readonly flowSteps = ['Draft', 'Approved', 'Closed'] as const;

  readonly item = this.facade.selected;

  /** Enrollments with a recorded result — the only ones that count as fulfilment. */
  private readonly enrollments = signal<PtEnrollment[]>([]);
  readonly resulted = computed(() => this.enrollments().filter((e) => e.performance !== 'Pending'));

  readonly itemForm = this.fb.nonNullable.group({
    scheme: ['', [Validators.required, Validators.maxLength(200)]],
    analyte: ['', [Validators.required, Validators.maxLength(200)]],
    provider: ['', [Validators.maxLength(200)]],
    plannedCycles: [2, [Validators.required, Validators.min(1), Validators.max(52)]],
    notes: ['', [Validators.maxLength(1000)]],
  });
  readonly fulfilForm = this.fb.nonNullable.group({
    itemId: ['', [Validators.required]],
    enrollmentId: ['', [Validators.required]],
  });
  readonly closeForm = this.fb.nonNullable.group({
    closureSummary: ['', [Validators.required, Validators.maxLength(4000)]],
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
    void firstValueFrom(this.analyticalApi.ptEnrollments())
      .then((enrollments) => this.enrollments.set(enrollments))
      .catch(() => this.enrollments.set([]));
  }

  async addItem(id: string): Promise<void> {
    if (this.itemForm.invalid) { return; }
    const raw = this.itemForm.getRawValue();
    await this.facade.addItem(id, {
      ...raw,
      provider: raw.provider.trim() || null,
      notes: raw.notes.trim() || null,
    });
    this.itemForm.reset({ plannedCycles: 2 });
  }

  async fulfil(id: string): Promise<void> {
    if (this.fulfilForm.invalid) { return; }
    const raw = this.fulfilForm.getRawValue();
    await this.facade.recordFulfilment(id, raw.itemId, raw.enrollmentId);
    this.fulfilForm.reset();
  }

  async close(id: string): Promise<void> {
    if (this.closeForm.invalid) { return; }
    await this.facade.close(id, this.closeForm.getRawValue().closureSummary);
  }
}
