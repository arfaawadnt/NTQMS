import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { RiskFacade } from './risk.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { HIGH_RESIDUAL_RPN_THRESHOLD } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { UserSelectComponent } from '../../shared/ui/user-select.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Risk workspace: inherent vs residual scoring, mitigation actions, and the
 * close gate. Residual assessment and closure are restricted to Quality Managers
 * (mirroring the backend); closure is offered only once a residual score exists
 * and every mitigation action is complete — the same rule the domain enforces.
 */
@Component({
  selector: 'qams-risk-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, UserSelectComponent],
  template: `
    @if (item(); as r) {
      <qams-page-header [title]="r.riskRef + ' — ' + r.title" [subtitle]="r.category">
        <a routerLink="/risks" class="ghost-link">← {{ i18n.t('risk.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="r.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="r.status" /></div>
        <div><span class="muted">{{ i18n.t('risk.inherent') }}</span> {{ r.likelihood }} × {{ r.impact }} = <b class="rpn" [class.high]="r.rpn > threshold">{{ r.rpn }}</b></div>
        <div>
          <span class="muted">{{ i18n.t('risk.residual') }}</span>
          @if (r.residualRpn !== null) {
            {{ r.residualLikelihood }} × {{ r.residualImpact }} = <b class="rpn" [class.high]="r.residualRpn > threshold">{{ r.residualRpn }}</b>
          } @else { <span class="muted">{{ i18n.t('risk.notAssessed') }}</span> }
        </div>
        @if (r.residualRpn !== null && r.residualRpn > threshold) {
          <div class="warn-tag">{{ i18n.t('risk.highResidual') }}</div>
        }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <div class="grid">
        <section class="card">
          <h3>{{ i18n.t('risk.actions') }}</h3>
          @if (r.actions.length === 0) { <p class="muted">—</p> }
          @for (a of r.actions; track a.id) {
            <div class="row-item">
              <div>
                <div [class.done]="a.completed">{{ a.description }}</div>
                <span class="muted">{{ i18n.t('risk.due') }}: {{ a.dueDate | date:'mediumDate' }} · {{ a.ownerId }}</span>
              </div>
              @if (a.completed) {
                <span class="ok-tag">✓</span>
              } @else if (open()) {
                <button class="link" (click)="facade.completeMitigation(r.id, a.id)">{{ i18n.t('risk.complete') }}</button>
              }
            </div>
          }
          @if (open()) {
            <form [formGroup]="mitForm" (ngSubmit)="addMitigation(r.id)">
              <label>{{ i18n.t('risk.mitDescription') }}</label><input formControlName="description" />
              <label>{{ i18n.t('risk.owner') }}</label><qams-user-select formControlName="ownerId" />
              <label>{{ i18n.t('risk.due') }}</label><input type="date" formControlName="dueDate" />
              <button type="submit" [disabled]="mitForm.invalid">{{ i18n.t('risk.addAction') }}</button>
            </form>
          }
        </section>

        <section class="card">
          <h3>{{ i18n.t('risk.governance') }}</h3>
          @if (!open()) {
            <p class="muted">{{ i18n.t('risk.closedNote') }}</p>
          } @else if (perms.canApprove()) {
            <form [formGroup]="residualForm" (ngSubmit)="recordResidual(r.id)">
              <label>{{ i18n.t('risk.residualScore') }}</label>
              <div class="pair">
                <input type="number" min="1" max="5" formControlName="likelihood" [attr.aria-label]="i18n.t('risk.likelihood')" />
                <span>×</span>
                <input type="number" min="1" max="5" formControlName="impact" [attr.aria-label]="i18n.t('risk.impact')" />
              </div>
              <button type="submit" [disabled]="residualForm.invalid">{{ i18n.t('risk.recordResidual') }}</button>
            </form>
            <div class="close-row">
              <button (click)="facade.close(r.id)" [disabled]="!canClose()">{{ i18n.t('risk.close') }}</button>
              @if (!canClose()) { <p class="muted small">{{ i18n.t('risk.closeHint') }}</p> }
            </div>
          } @else {
            <p class="muted">{{ i18n.t('comp.approverOnly') }}</p>
          }
        </section>
      </div>
    
      <qams-audit-trail [subject]="r.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
  styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .rpn.high { color: var(--nt-red, #b42318); }
    .warn-tag { background: #fbeaea; color: var(--nt-red, #b42318); padding: .25rem .6rem; border-radius: 4px; font-size: .8rem; font-weight: 600; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; align-items: start; }
    .row-item { padding: .5rem 0; border-bottom: 1px solid var(--nt-border); display: flex; justify-content: space-between; gap: 1rem; align-items: center; }
    .done { text-decoration: line-through; color: var(--nt-muted, #6b7280); }
    .ok-tag { color: var(--nt-green, #1a7f37); font-weight: 700; }
    .pair { display: flex; align-items: center; gap: .5rem; }
    .pair input { width: 5rem; }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .close-row { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    .close-row button { width: auto; }
    .small { font-size: .78rem; margin-top: .35rem; }
    .link { width: auto; background: none; color: var(--nt-blue); padding: 0; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `],
})
export class RiskDetailComponent implements OnInit {
  readonly facade = inject(RiskFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound risk id. */
  readonly id = input.required<string>();

  /** Canonical workflow path for the stepper (off-path states render as terminal). */
  readonly flowSteps = ['Identified', 'Mitigating', 'Closed'] as const;

  readonly item = this.facade.selected;
  readonly threshold = HIGH_RESIDUAL_RPN_THRESHOLD;

  /** A risk is open (editable) until it is closed. */
  readonly open = computed(() => this.item()?.status !== 'Closed');

  /** Closure requires a residual assessment and all mitigation actions complete (domain rule RSK-005/006). */
  readonly canClose = computed(() => {
    const r = this.item();
    return !!r && r.residualRpn !== null && r.actions.every((a) => a.completed);
  });

  readonly mitForm = this.fb.nonNullable.group({
    description: ['', [Validators.required, Validators.maxLength(500)]],
    ownerId: ['', [Validators.required]],
    dueDate: ['', [Validators.required]],
  });
  readonly residualForm = this.fb.nonNullable.group({
    likelihood: [3, [Validators.required, Validators.min(1), Validators.max(5)]],
    impact: [3, [Validators.required, Validators.min(1), Validators.max(5)]],
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  async addMitigation(id: string): Promise<void> {
    if (this.mitForm.invalid) { return; }
    await this.facade.addMitigation(id, this.mitForm.getRawValue());
    this.mitForm.reset();
  }

  async recordResidual(id: string): Promise<void> {
    if (this.residualForm.invalid) { return; }
    const { likelihood, impact } = this.residualForm.getRawValue();
    await this.facade.recordResidual(id, likelihood, impact);
  }
}
