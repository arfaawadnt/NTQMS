import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ComplaintsFacade } from './complaints.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Complaint workspace mirroring the backend state machine: acknowledge →
 * validate (justified spawns the linked NC via the saga; unjustified
 * terminates as Invalid) → investigate → outcome → resolve → close.
 * Closing is refused server-side while the linked NC is open (CMP-020).
 * Confidential reporter identity arrives pre-masked for roles without the
 * view-confidential privilege.
 */
@Component({
  selector: 'qams-complaint-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
  template: `
    @if (item(); as c) {
      <qams-page-header [title]="c.complaintRef + ' — ' + c.subject" [subtitle]="c.channel + ' · ' + ((c.loggedAtUtc | date:'medium') ?? '')">
        <a routerLink="/complaints" class="ghost-link">← {{ i18n.t('cmpl.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="c.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="c.status" /></div>
        <div>
          <span class="muted">{{ i18n.t('cmpl.complainant') }}</span>
          {{ c.complainantName }} @if (c.confidential) { <span title="Confidential">🔒</span> }
          @if (c.complainantContact) { <span class="muted"> · {{ c.complainantContact }}</span> }
        </div>
        @if (c.linkedNcId) {
          <div><span class="muted">{{ i18n.t('cmpl.linkedNc') }}</span> <a [routerLink]="['/nonconformances', c.linkedNcId]">{{ i18n.t('cmpl.openNc') }}</a></div>
        }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <section class="card">
        <h3>{{ i18n.t('nc.description') }}</h3>
        <p class="pre">{{ c.description }}</p>
      </section>

      @if (c.validationVerdict) {
        <section class="card"><h3>{{ i18n.t('cmpl.verdict') }}</h3><p class="pre">{{ c.validationVerdict }}</p></section>
      }
      @if (c.investigationOutcome) {
        <section class="card"><h3>{{ i18n.t('cmpl.outcome') }}</h3><p class="pre">{{ c.investigationOutcome }}</p></section>
      }
      @if (c.resolution) {
        <section class="card"><h3>{{ i18n.t('cmpl.resolution') }}</h3><p class="pre">{{ c.resolution }}</p></section>
      }

      <section class="card">
        <h3>{{ i18n.t('cmpl.workflow') }}</h3>
        @switch (c.status) {
          @case ('Logged') {
            @if (perms.canAssignTraining()) {
              <button (click)="facade.acknowledge(c.id)">{{ i18n.t('cmpl.acknowledge') }}</button>
            } @else { <p class="muted">{{ i18n.t('cmpl.handlerOnly') }}</p> }
          }
          @case ('Acknowledged') {
            @if (perms.canApprove()) {
              <form [formGroup]="validateForm" (ngSubmit)="validate(c.id, true)">
                <label>{{ i18n.t('cmpl.verdictReason') }}</label>
                <textarea formControlName="reason" rows="2"></textarea>
                <div class="pair">
                  <button type="submit" [disabled]="validateForm.invalid">{{ i18n.t('cmpl.justified') }}</button>
                  <button type="button" class="danger" [disabled]="validateForm.invalid" (click)="validate(c.id, false)">{{ i18n.t('cmpl.unjustified') }}</button>
                </div>
                <div class="hint">{{ i18n.t('cmpl.justifiedHint') }}</div>
              </form>
            } @else { <p class="muted">{{ i18n.t('comp.approverOnly') }}</p> }
          }
          @case ('Validated') {
            @if (perms.canAssignTraining()) {
              <button (click)="facade.startInvestigation(c.id)">{{ i18n.t('cmpl.startInvestigation') }}</button>
            } @else { <p class="muted">{{ i18n.t('cmpl.handlerOnly') }}</p> }
          }
          @case ('Investigating') {
            @if (perms.canAssignTraining()) {
              <form [formGroup]="outcomeForm" (ngSubmit)="logOutcome(c.id)">
                <label>{{ i18n.t('cmpl.outcome') }}</label>
                <textarea formControlName="outcome" rows="3"></textarea>
                <button type="submit" [disabled]="outcomeForm.invalid">{{ i18n.t('cmpl.logOutcome') }}</button>
              </form>
            } @else { <p class="muted">{{ i18n.t('cmpl.handlerOnly') }}</p> }
          }
          @case ('OutcomeLogged') {
            @if (perms.canAssignTraining()) {
              <form [formGroup]="resolveForm" (ngSubmit)="resolve(c.id)">
                <label>{{ i18n.t('cmpl.resolution') }}</label>
                <textarea formControlName="resolution" rows="3"></textarea>
                <button type="submit" [disabled]="resolveForm.invalid">{{ i18n.t('cmpl.resolve') }}</button>
              </form>
            } @else { <p class="muted">{{ i18n.t('cmpl.handlerOnly') }}</p> }
          }
          @case ('Resolved') {
            @if (perms.canApprove()) {
              <button (click)="facade.close(c.id)">{{ i18n.t('cmpl.close') }}</button>
              @if (c.linkedNcId) { <p class="muted small">{{ i18n.t('cmpl.closeGateHint') }}</p> }
            } @else { <p class="muted">{{ i18n.t('comp.approverOnly') }}</p> }
          }
          @case ('Closed') { <p class="muted">{{ i18n.t('cmpl.closedNote') }}</p> }
          @case ('Invalid') { <p class="muted">{{ i18n.t('cmpl.invalidNote') }}</p> }
        }
      </section>

      <qams-audit-trail [subject]="c.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
  styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: inline; font-size: .78rem; }
    .pre { white-space: pre-wrap; margin: 0; }
    .pair { display: flex; gap: .6rem; margin-top: .5rem; }
    form { margin-top: .25rem; }
    form textarea { width: 100%; }
    form button, section button { width: auto; margin-top: .5rem; }
    .small { font-size: .78rem; margin-top: .4rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
  `],
})
export class ComplaintDetailComponent implements OnInit {
  readonly facade = inject(ComplaintsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound complaint id. */
  readonly id = input.required<string>();

  /** Canonical workflow path for the stepper (Invalid renders as terminal off-path). */
  readonly flowSteps = ['Logged', 'Acknowledged', 'Validated', 'Investigating', 'OutcomeLogged', 'Resolved', 'Closed'] as const;

  readonly item = this.facade.selected;

  readonly validateForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(2000)]],
  });
  readonly outcomeForm = this.fb.nonNullable.group({
    outcome: ['', [Validators.required, Validators.maxLength(4000)]],
  });
  readonly resolveForm = this.fb.nonNullable.group({
    resolution: ['', [Validators.required, Validators.maxLength(4000)]],
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  async validate(id: string, justified: boolean): Promise<void> {
    if (this.validateForm.invalid) { return; }
    await this.facade.validate(id, justified, this.validateForm.getRawValue().reason);
    this.validateForm.reset();
  }

  async logOutcome(id: string): Promise<void> {
    if (this.outcomeForm.invalid) { return; }
    await this.facade.logOutcome(id, this.outcomeForm.getRawValue().outcome);
    this.outcomeForm.reset();
  }

  async resolve(id: string): Promise<void> {
    if (this.resolveForm.invalid) { return; }
    await this.facade.resolve(id, this.resolveForm.getRawValue().resolution);
    this.resolveForm.reset();
  }
}
