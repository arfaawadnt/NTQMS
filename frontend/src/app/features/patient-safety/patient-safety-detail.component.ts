import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PatientSafetyFacade } from './patient-safety.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Patient-safety event workspace (HQMS M08): the reported fall or pressure injury with
 * its harm and origin, then the review → close ceremony. Hospital-acquired pressure
 * injuries are flagged because they drive the HAPI rate. Actions are hidden for users
 * who lack the privilege (the server still enforces every call).
 */
@Component({
    selector: 'qams-patient-safety-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
    template: `
    @if (event(); as e) {
      <qams-page-header [title]="e.eventRef + ' — ' + i18n.t('psf.ty.' + e.type)">
        <a routerLink="/patient-safety" class="ghost-link">← {{ i18n.t('psf.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="e.status" />

      @if (e.type === 'PressureInjury' && e.origin === 'HospitalAcquired') {
        <div class="banner hapi">{{ i18n.t('psf.hapiBanner') }}</div>
      }

      <div class="grid">
        <section class="card">
          <div class="meta">
            <div><span class="muted">{{ i18n.t('psf.status') }}</span><qams-status-pill [status]="e.status" /></div>
            <div><span class="muted">{{ i18n.t('psf.type') }}</span> {{ i18n.t('psf.ty.' + e.type) }}</div>
            <div><span class="muted">{{ i18n.t('psf.patientRef') }}</span> {{ e.patientRef }}</div>
            @if (e.unit) { <div><span class="muted">{{ i18n.t('psf.unit') }}</span> {{ e.unit }}</div> }
            <div><span class="muted">{{ i18n.t('psf.occurredAt') }}</span> {{ e.occurredAtUtc | date:'medium' }}</div>
            <div><span class="muted">{{ i18n.t('psf.harm') }}</span> <b [class.danger-text]="e.harmLevel === 'Severe' || e.harmLevel === 'Death'">{{ i18n.t('psf.harm.' + e.harmLevel) }}</b></div>
            @if (e.type === 'PressureInjury') {
              <div><span class="muted">{{ i18n.t('psf.stage') }}</span> {{ i18n.t('psf.stg.' + e.stage) }}</div>
              <div><span class="muted">{{ i18n.t('psf.origin') }}</span> {{ i18n.t('psf.or.' + e.origin) }}</div>
            }
          </div>
          @if (e.description) { <p>{{ e.description }}</p> }
          @if (e.reviewNotes) {
            <h3>{{ i18n.t('psf.reviewNotes') }}</h3>
            <p>{{ e.reviewNotes }}</p>
            <p class="muted">{{ i18n.t('psf.reviewedAt') }}: {{ e.reviewedAtUtc | date:'medium' }}</p>
          }
        </section>

        <section class="card actions">
          <h3>{{ i18n.t('psf.workflow') }}</h3>
          @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

          @switch (e.status) {
            @case ('Reported') {
              @if (perms.can('patient-safety.edit')) {
                <form [formGroup]="reviewForm" (ngSubmit)="review(e.id)">
                  <label>{{ i18n.t('psf.reviewNotes') }}</label>
                  <textarea rows="3" formControlName="notes"></textarea>
                  <button type="submit" [disabled]="reviewForm.invalid || facade.loading()">{{ i18n.t('psf.recordReview') }}</button>
                </form>
              } @else { <p class="muted">{{ i18n.t('psf.awaitReview') }}</p> }
            }
            @case ('Reviewed') {
              @if (perms.can('patient-safety.void')) {
                <p class="muted">{{ i18n.t('psf.closeHint') }}</p>
                <button (click)="facade.close(e.id)" [disabled]="facade.loading()">{{ i18n.t('psf.close') }}</button>
              } @else { <p class="muted">{{ i18n.t('psf.awaitClose') }}</p> }
            }
            @default { <p class="muted">{{ i18n.t('psf.terminal') }}</p> }
          }
        </section>
      </div>

      <qams-audit-trail [subject]="e.id" />
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
    .banner { padding: .4rem .8rem; border-radius: 6px; font-weight: 600; margin-bottom: .75rem; }
    .banner.hapi { background: var(--nt-ink-crit); color: #fff; }
    .danger-text { color: var(--nt-ink-crit); }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    h3 { margin-top: 1rem; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class PatientSafetyDetailComponent implements OnInit {
  readonly facade = inject(PatientSafetyFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound event id (provided via withComponentInputBinding). */
  readonly id = input.required<string>();

  readonly flowSteps = ['Reported', 'Reviewed', 'Closed'] as const;
  readonly event = this.facade.selected;

  readonly reviewForm = this.fb.nonNullable.group({
    notes: ['', [Validators.required, Validators.maxLength(4000)]],
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
  }

  async review(id: string): Promise<void> {
    if (this.reviewForm.invalid) { return; }
    await this.facade.review(id, this.reviewForm.getRawValue());
    if (this.facade.error() === '') { this.reviewForm.reset({ notes: '' }); }
  }
}
