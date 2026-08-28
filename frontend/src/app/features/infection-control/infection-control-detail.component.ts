import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { InfectionControlFacade } from './infection-control.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * HAI-case workspace (HQMS M09): the reported infection with its type, unit, onset and organism,
 * then the infection-control review → close ceremony. Actions are hidden for users who lack the
 * privilege (the server still enforces every call).
 */
@Component({
    selector: 'qams-infection-control-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
    template: `
    @if (hai(); as c) {
      <qams-page-header [title]="c.caseRef + ' — ' + i18n.t('ipc.ty.' + c.type)">
        <a routerLink="/infection-control" class="ghost-link">← {{ i18n.t('ipc.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="c.status" />

      <div class="grid">
        <section class="card">
          <div class="meta">
            <div><span class="muted">{{ i18n.t('ipc.status') }}</span><qams-status-pill [status]="c.status" /></div>
            <div><span class="muted">{{ i18n.t('ipc.type') }}</span> {{ i18n.t('ipc.ty.' + c.type) }}</div>
            <div><span class="muted">{{ i18n.t('ipc.patientRef') }}</span> {{ c.patientRef }}</div>
            @if (c.unit) { <div><span class="muted">{{ i18n.t('ipc.unit') }}</span> {{ c.unit }}</div> }
            <div><span class="muted">{{ i18n.t('ipc.onsetDate') }}</span> {{ c.onsetDateUtc | date:'medium' }}</div>
            @if (c.organism) { <div><span class="muted">{{ i18n.t('ipc.organism') }}</span> {{ c.organism }}</div> }
          </div>
          @if (c.description) { <p>{{ c.description }}</p> }
          @if (c.reviewNotes) {
            <h3>{{ i18n.t('ipc.reviewNotes') }}</h3>
            <p>{{ c.reviewNotes }}</p>
            <p class="muted">{{ i18n.t('ipc.reviewedAt') }}: {{ c.reviewedAtUtc | date:'medium' }}</p>
          }
        </section>

        <section class="card actions">
          <h3>{{ i18n.t('ipc.workflow') }}</h3>
          @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

          @switch (c.status) {
            @case ('Reported') {
              @if (perms.can('infection-control.edit')) {
                <form [formGroup]="reviewForm" (ngSubmit)="review(c.id)">
                  <label>{{ i18n.t('ipc.reviewNotes') }}</label>
                  <textarea rows="3" formControlName="notes"></textarea>
                  <button type="submit" [disabled]="reviewForm.invalid || facade.loading()">{{ i18n.t('ipc.recordReview') }}</button>
                </form>
              } @else { <p class="muted">{{ i18n.t('ipc.awaitReview') }}</p> }
            }
            @case ('Reviewed') {
              @if (perms.can('infection-control.void')) {
                <p class="muted">{{ i18n.t('ipc.closeHint') }}</p>
                <button (click)="facade.close(c.id)" [disabled]="facade.loading()">{{ i18n.t('ipc.close') }}</button>
              } @else { <p class="muted">{{ i18n.t('ipc.awaitClose') }}</p> }
            }
            @default { <p class="muted">{{ i18n.t('ipc.terminal') }}</p> }
          }

          @if ((c.status === 'Reported' || c.status === 'Reviewed') && perms.can('infection-control.void')) {
            <form class="reject" [formGroup]="rejectForm" (ngSubmit)="reject(c.id)">
              <p class="muted">{{ i18n.t('ipc.rejectHint') }}</p>
              <label>{{ i18n.t('ipc.rejectReason') }}</label>
              <textarea rows="2" formControlName="reason"></textarea>
              <button type="submit" class="danger" [disabled]="rejectForm.invalid || facade.loading()">{{ i18n.t('ipc.reject') }}</button>
            </form>
          }
        </section>
      </div>

      <qams-audit-trail [subject]="c.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .grid { display: grid; grid-template-columns: 2fr 1fr; gap: 1rem; align-items: start; }
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .actions form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    .actions button { margin-top: .5rem; width: auto; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    h3 { margin-top: 1rem; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class InfectionControlDetailComponent implements OnInit {
  readonly facade = inject(InfectionControlFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound case id (provided via withComponentInputBinding). */
  readonly id = input.required<string>();

  readonly flowSteps = ['Reported', 'Reviewed', 'Closed'] as const;
  readonly hai = this.facade.selected;

  readonly reviewForm = this.fb.nonNullable.group({
    notes: ['', [Validators.required, Validators.maxLength(4000)]],
  });

  readonly rejectForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
  }

  async review(id: string): Promise<void> {
    if (this.reviewForm.invalid) { return; }
    await this.facade.review(id, this.reviewForm.getRawValue());
    if (this.facade.error() === '') { this.reviewForm.reset({ notes: '' }); }
  }

  async reject(id: string): Promise<void> {
    if (this.rejectForm.invalid) { return; }
    await this.facade.reject(id, this.rejectForm.getRawValue().reason);
    if (this.facade.error() === '') { this.rejectForm.reset({ reason: '' }); }
  }
}
