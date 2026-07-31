import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FeedbackFacade } from './feedback.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { OrgDataService } from '../../core/org-data.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Feedback workspace: the entry, its review and closure narratives, and — for
 * dissatisfaction — escalation into the formal complaint workflow (which
 * links the records and ends this one).
 */
@Component({
    selector: 'qams-feedback-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
    template: `
    @if (item(); as f) {
      <qams-page-header [title]="f.feedbackRef + ' — ' + f.subject" [subtitle]="i18n.t('fbk.type' + f.type) + ' · ' + f.source + ' · ' + f.channel">
        <a routerLink="/feedback" class="ghost-link">← {{ i18n.t('fbk.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="f.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="f.status" /></div>
        <div><span class="muted">{{ i18n.t('fbk.receivedOn') }}</span> {{ f.receivedOn | date:'mediumDate' }}</div>
        <div><span class="muted">{{ i18n.t('fbk.score') }}</span> {{ f.satisfactionScore !== null ? f.satisfactionScore + '/5' : '—' }}</div>
        <div><span class="muted">{{ i18n.t('fbk.loggedBy') }}</span> {{ org.userName(f.loggedBy) || '—' }}</div>
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <section class="card">
        <h3>{{ i18n.t('nc.description') }}</h3>
        <p>{{ f.details }}</p>
      </section>

      @if (f.reviewNotes) {
        <section class="card"><h3>{{ i18n.t('fbk.reviewNotes') }}</h3><p>{{ f.reviewNotes }}</p></section>
      }
      @if (f.actionSummary) {
        <section class="card"><h3>{{ i18n.t('fbk.action') }}</h3><p>{{ f.actionSummary }}</p></section>
      }
      @if (f.complaintId) {
        <section class="card">
          <p class="warn">{{ i18n.t('fbk.escalatedNote') }}
            <a [routerLink]="['/complaints', f.complaintId]">{{ i18n.t('fbk.openComplaint') }}</a>
          </p>
        </section>
      }

      @if (perms.canAny('feedback.edit', 'feedback.void')) {
        <section class="card">
          <h3>{{ i18n.t('val.workflow') }}</h3>
          @if (f.status === 'Logged') {
            <form [formGroup]="reviewForm" (ngSubmit)="review(f.id)">
              <label>{{ i18n.t('fbk.reviewNotes') }}</label>
              <input formControlName="reviewNotes" />
              <button type="submit" [disabled]="reviewForm.invalid">{{ i18n.t('fbk.review') }}</button>
            </form>
          }
          @if (f.status === 'Reviewed') {
            <form [formGroup]="closeForm" (ngSubmit)="close(f.id)">
              <label>{{ i18n.t('fbk.action') }}</label>
              <input formControlName="actionSummary" [placeholder]="i18n.t('fbk.actionHint')" />
              <button type="submit" [disabled]="closeForm.invalid">{{ i18n.t('fbk.close') }}</button>
            </form>
          }
          @if (f.type === 'Dissatisfaction' && (f.status === 'Logged' || f.status === 'Reviewed')) {
            <form [formGroup]="escalateForm" (ngSubmit)="escalate(f.id)">
              <div class="pair">
                <div><label>{{ i18n.t('cmpl.complainant') }}</label><input formControlName="complainantName" /></div>
                <div><label>{{ i18n.t('cmpl.contact') }}</label><input formControlName="complainantContact" [placeholder]="i18n.t('common.optional')" /></div>
              </div>
              <div class="hint">{{ i18n.t('fbk.escalateHint') }}</div>
              <button type="submit" class="secondary" [disabled]="escalateForm.invalid">{{ i18n.t('fbk.escalate') }}</button>
            </form>
          }
        </section>
      }

      <qams-audit-trail [subject]="f.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    section { margin-bottom: 1rem; }
    .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .warn { color: var(--nt-red); }
    form { margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
  `]
})
export class FeedbackDetailComponent implements OnInit {
  readonly facade = inject(FeedbackFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  readonly org = inject(OrgDataService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  /** Route-bound feedback id. */
  readonly id = input.required<string>();

  /** Canonical path (Escalated renders off-path — the complaint takes over). */
  readonly flowSteps = ['Logged', 'Reviewed', 'Closed'] as const;

  readonly item = this.facade.selected;

  readonly reviewForm = this.fb.nonNullable.group({
    reviewNotes: ['', [Validators.required, Validators.maxLength(2000)]],
  });
  readonly closeForm = this.fb.nonNullable.group({
    actionSummary: ['', [Validators.required, Validators.maxLength(2000)]],
  });
  readonly escalateForm = this.fb.nonNullable.group({
    complainantName: ['', [Validators.required, Validators.maxLength(200)]],
    complainantContact: [''],
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
    void this.org.ensureDirectory();
  }

  async review(id: string): Promise<void> {
    if (this.reviewForm.invalid) { return; }
    await this.facade.review(id, this.reviewForm.getRawValue().reviewNotes);
    this.reviewForm.reset();
  }

  async close(id: string): Promise<void> {
    if (this.closeForm.invalid) { return; }
    await this.facade.close(id, this.closeForm.getRawValue().actionSummary);
  }

  async escalate(id: string): Promise<void> {
    if (this.escalateForm.invalid) { return; }
    const raw = this.escalateForm.getRawValue();
    const complaintId = await this.facade.escalate(id, raw.complainantName, raw.complainantContact.trim() || null);
    if (complaintId) { void this.router.navigate(['/complaints', complaintId]); }
  }
}
