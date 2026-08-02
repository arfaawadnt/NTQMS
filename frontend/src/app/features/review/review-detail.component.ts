import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ReviewFacade } from './review.facade';
import { ExportsApiService } from '../../core/api/exports-api.service';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { UserSelectComponent } from '../../shared/ui/user-select.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Management review workspace. Decisions accumulate while the review is
 * Scheduled; closing records the minutes and freezes the record (MRV-004,
 * ISO 9001 9.3). All write actions are restricted to Quality Managers, matching
 * the backend authorization.
 */
@Component({
    selector: 'qams-review-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, UserSelectComponent],
    template: `
    @if (item(); as r) {
      <qams-page-header [title]="r.reviewRef + ' — ' + r.title" [subtitle]="(r.reviewDate | date:'fullDate') ?? ''">
        @if (perms.can('reviews.export')) {
          <button class="secondary" (click)="exports.reviewPackPdf(r.id)">{{ i18n.t('exp.reviewPack') }}</button>
        }
        <a routerLink="/management-reviews" class="ghost-link">← {{ i18n.t('mrv.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="r.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="r.status" /></div>
        @if (r.closedBy) { <div><span class="muted">{{ i18n.t('mrv.closedBy') }}</span> {{ r.closedBy }}</div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      @if (r.meetingLink || r.agenda) {
        <section class="card">
          <h3>{{ i18n.t('mrv.meeting') }}</h3>
          @if (r.meetingLink) {
            <p><a [href]="r.meetingLink" target="_blank" rel="noopener noreferrer" class="mlink">{{ r.meetingLink }}</a></p>
          }
          @if (r.agenda) {
            <h4 class="agh">{{ i18n.t('mrv.agenda') }}</h4>
            <p class="pre">{{ r.agenda }}</p>
          }
        </section>
      }

      @if (r.participants) {
        <section class="card"><h3>{{ i18n.t('mrv.participants') }}</h3><p class="pre">{{ r.participants }}</p></section>
      }

      <section class="card">
        <h3>{{ i18n.t('mrv.decisions') }}</h3>
        @if (r.decisions.length === 0) { <p class="muted">—</p> }
        @for (d of r.decisions; track d.id) {
          <div class="row-item">
            <div>{{ d.description }}</div>
            <span class="muted">{{ i18n.t('mrv.owner') }}: {{ d.ownerId }} · {{ i18n.t('mrv.due') }}: {{ d.dueDate | date:'mediumDate' }}</span>
          </div>
        }
        @if (open() && perms.can('reviews.edit')) {
          <form [formGroup]="decisionForm" (ngSubmit)="addDecision(r.id)">
            <label>{{ i18n.t('mrv.decisionDesc') }}</label><input formControlName="description" />
            <label>{{ i18n.t('mrv.owner') }}</label><qams-user-select formControlName="ownerId" />
            <label>{{ i18n.t('mrv.due') }}</label><input type="date" formControlName="dueDate" />
            <button type="submit" [disabled]="decisionForm.invalid">{{ i18n.t('mrv.addDecision') }}</button>
          </form>
        }
      </section>

      @if (r.minutes) {
        <section class="card"><h3>{{ i18n.t('mrv.minutes') }}</h3><p class="pre">{{ r.minutes }}</p></section>
      } @else if (open() && perms.can('reviews.void')) {
        <section class="card">
          <h3>{{ i18n.t('mrv.closeOut') }}</h3>
          <form [formGroup]="closeForm" (ngSubmit)="close(r.id)">
            <label>{{ i18n.t('mrv.minutes') }}</label>
            <textarea formControlName="minutes" rows="4" [placeholder]="i18n.t('mrv.minutesHint')"></textarea>
            <button type="submit" [disabled]="closeForm.invalid">{{ i18n.t('mrv.close') }}</button>
          </form>
        </section>
      }

      @if (!open() && !perms.can('reviews.edit')) { <p class="muted">{{ i18n.t('mrv.closedNote') }}</p> }
    
      <qams-audit-trail [subject]="r.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .mlink { color: var(--nt-navy-deep); font-weight: 600; overflow-wrap: anywhere; }
    .agh { font-size: 12px; text-transform: uppercase; letter-spacing: .03em; color: var(--nt-grey-m); margin: 10px 0 4px; }
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .pre { white-space: pre-wrap; margin: 0; }
    .row-item { padding: .5rem 0; border-bottom: 1px solid var(--nt-border); display: flex; justify-content: space-between; gap: 1rem; }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form textarea { width: 100%; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
  `]
})
export class ReviewDetailComponent implements OnInit {
  readonly facade = inject(ReviewFacade);
  readonly exports = inject(ExportsApiService);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound review id. */
  readonly id = input.required<string>();

  /** Canonical workflow path for the stepper (off-path states render as terminal). */
  readonly flowSteps = ['Scheduled', 'Closed'] as const;

  readonly item = this.facade.selected;

  /** A review is open (editable) while it is Scheduled. */
  readonly open = computed(() => this.item()?.status === 'Scheduled');

  readonly decisionForm = this.fb.nonNullable.group({
    description: ['', [Validators.required, Validators.maxLength(1000)]],
    ownerId: ['', [Validators.required]],
    dueDate: ['', [Validators.required]],
  });
  readonly closeForm = this.fb.nonNullable.group({
    minutes: ['', [Validators.required, Validators.maxLength(8000)]],
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  async addDecision(id: string): Promise<void> {
    if (this.decisionForm.invalid) { return; }
    await this.facade.addDecision(id, this.decisionForm.getRawValue());
    this.decisionForm.reset();
  }

  async close(id: string): Promise<void> {
    if (this.closeForm.invalid) { return; }
    await this.facade.close(id, this.closeForm.getRawValue().minutes);
    this.closeForm.reset();
  }
}
