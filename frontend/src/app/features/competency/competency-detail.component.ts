import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CompetencyFacade } from './competency.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Competency workspace: profile, assessment history, and the authorize/revoke
 * lifecycle. Scoring records an assessment (the backend applies the pass gate);
 * authorize is available only once the competency is evaluated, revoke only once
 * it has been authorized — both mirror the backend role gates.
 */
@Component({
    selector: 'qams-competency-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
    template: `
    @if (item(); as c) {
      <qams-page-header [title]="c.subject" [subtitle]="i18n.t('comp.trainee') + ': ' + c.traineeId">
        <a routerLink="/competencies" class="ghost-link">← {{ i18n.t('comp.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="c.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="c.status" /></div>
        <div><span class="muted">{{ i18n.t('comp.validity') }}</span> {{ c.validityMonths }} {{ i18n.t('comp.months') }}</div>
        <div><span class="muted">{{ i18n.t('comp.expires') }}</span> {{ c.expiresAt ? (c.expiresAt | date:'mediumDate') : '—' }}</div>
        @if (c.authorizedBy) { <div><span class="muted">{{ i18n.t('comp.authorizedBy') }}</span> {{ c.authorizedBy }}</div> }
        @if (c.documentId) { <div><span class="muted">{{ i18n.t('comp.documentId') }}</span> <a [routerLink]="['/documents', c.documentId]">{{ i18n.t('comp.openDoc') }}</a></div> }
      </div>

      @if (c.revocationReason) {
        <div class="error">{{ i18n.t('comp.revokedReason') }}: {{ c.revocationReason }}</div>
      }
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <div class="grid">
        <section class="card">
          <h3>{{ i18n.t('comp.assessments') }}</h3>
          @if (c.assessments.length === 0) { <p class="muted">—</p> }
          @for (a of c.assessments; track a.id) {
            <div class="row-item">
              <span [class.pass]="a.score >= 80" [class.fail]="a.score < 80">{{ a.score }}%</span>
              <span class="muted">{{ a.assessedAtUtc | date:'medium' }} · {{ a.assessorId }}</span>
            </div>
          }
          @if (canScore() && perms.can('competencies.edit')) {
            <form [formGroup]="scoreForm" (ngSubmit)="score(c.id)">
              <label>{{ i18n.t('comp.recordScore') }}</label>
              <input type="number" min="0" max="100" formControlName="score" />
              <button type="submit" [disabled]="scoreForm.invalid">{{ i18n.t('comp.submitScore') }}</button>
            </form>
          }
        </section>

        <section class="card">
          <h3>{{ i18n.t('comp.actions') }}</h3>
          @if (perms.can('competencies.approve')) {
            @if (c.status === 'Evaluated') {
              <button (click)="facade.authorize(c.id)">{{ i18n.t('comp.authorize') }}</button>
            }
            @if (c.status === 'Authorized') {
              <form [formGroup]="revokeForm" (ngSubmit)="revoke(c.id)">
                <label>{{ i18n.t('comp.revokeReason') }}</label>
                <input formControlName="reason" />
                <button type="submit" class="secondary" [disabled]="revokeForm.invalid">{{ i18n.t('comp.revoke') }}</button>
              </form>
            }
            @if (c.status !== 'Evaluated' && c.status !== 'Authorized') {
              <p class="muted">{{ i18n.t('comp.noActions') }}</p>
            }
          } @else {
            <p class="muted">{{ i18n.t('comp.approverOnly') }}</p>
          }
        </section>
      </div>
    
      <qams-audit-trail [subject]="c.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; align-items: start; }
    .row-item { padding: .5rem 0; border-bottom: 1px solid var(--nt-border); display: flex; justify-content: space-between; gap: 1rem; }
    .pass { color: var(--nt-green, #1a7f37); font-weight: 700; }
    .fail { color: var(--nt-red, #b42318); font-weight: 700; }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class CompetencyDetailComponent implements OnInit {
  readonly facade = inject(CompetencyFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound competency id. */
  readonly id = input.required<string>();

  /** Canonical workflow path for the stepper (off-path states render as terminal). */
  readonly flowSteps = ['PendingTraining', 'Evaluated', 'Authorized'] as const;

  readonly item = this.facade.selected;

  /** Assessments may only be recorded before the competency is authorized or revoked. */
  readonly canScore = computed(() => {
    const s = this.item()?.status;
    return s === 'PendingTraining' || s === 'Evaluated';
  });

  readonly scoreForm = this.fb.nonNullable.group({
    score: [0, [Validators.required, Validators.min(0), Validators.max(100)]],
  });
  readonly revokeForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(500)]],
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  async score(id: string): Promise<void> {
    if (this.scoreForm.invalid) { return; }
    await this.facade.scoreAssessment(id, this.scoreForm.getRawValue().score);
    this.scoreForm.reset({ score: 0 });
  }

  async revoke(id: string): Promise<void> {
    if (this.revokeForm.invalid) { return; }
    await this.facade.revoke(id, this.revokeForm.getRawValue().reason);
    this.revokeForm.reset();
  }
}
