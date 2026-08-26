import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MortalityReviewFacade } from './mortality-review.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { DEATH_CLASSIFICATIONS, DeathClassification } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Mortality-review workspace (HQMS M10): the death case with its peer-review classification and,
 * for a non-expected death, the mandatory independent second review and committee discussion before
 * closure. The second review must be recorded by a different reviewer (enforced server-side).
 */
@Component({
    selector: 'qams-mortality-review-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
    template: `
    @if (review(); as m) {
      <qams-page-header [title]="m.reviewRef + ' — ' + m.patientRef">
        <a routerLink="/mortality-review" class="ghost-link">← {{ i18n.t('mm.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="steps(m.requiresSecondReview)" [current]="m.status" />

      <div class="grid">
        <section class="card">
          <div class="meta">
            <div><span class="muted">{{ i18n.t('mm.status') }}</span><qams-status-pill [status]="m.status" /></div>
            <div><span class="muted">{{ i18n.t('mm.unit') }}</span> {{ m.unit }}</div>
            <div><span class="muted">{{ i18n.t('mm.deathDate') }}</span> {{ m.deathDateUtc | date:'medium' }}</div>
            @if (m.primaryDiagnosis) { <div><span class="muted">{{ i18n.t('mm.primaryDiagnosis') }}</span> {{ m.primaryDiagnosis }}</div> }
            @if (m.classification) { <div><span class="muted">{{ i18n.t('mm.classification') }}</span> <b [class.danger-text]="m.classification === 'Preventable'">{{ i18n.t('mm.cl.' + m.classification) }}</b></div> }
          </div>

          @if (m.classificationFindings) {
            <h3>{{ i18n.t('mm.classificationFindings') }}</h3>
            <p>{{ m.classificationFindings }}</p>
          }
          @if (m.secondReviewNotes) {
            <h3>{{ i18n.t('mm.secondReview') }}</h3>
            <p>{{ m.secondReviewNotes }}</p>
            <p class="muted">{{ m.secondReviewerConcurs ? i18n.t('mm.concurs') : i18n.t('mm.dissents') }}</p>
          }
          @if (m.committeeLearnings) {
            <h3>{{ i18n.t('mm.committeeLearnings') }}</h3>
            <p>{{ m.committeeLearnings }}</p>
          }
        </section>

        <section class="card actions">
          <h3>{{ i18n.t('mm.workflow') }}</h3>
          @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

          @switch (m.status) {
            @case ('Reported') {
              @if (perms.can('mortality-review.edit')) {
                <form [formGroup]="classifyForm" (ngSubmit)="classify(m.id)">
                  <label>{{ i18n.t('mm.classification') }}</label>
                  <select formControlName="classification">@for (c of classifications; track c) { <option [value]="c">{{ i18n.t('mm.cl.' + c) }}</option> }</select>
                  <label>{{ i18n.t('mm.classificationFindings') }}</label>
                  <textarea rows="3" formControlName="findings"></textarea>
                  <button type="submit" [disabled]="classifyForm.invalid || facade.loading()">{{ i18n.t('mm.classify') }}</button>
                </form>
              } @else { <p class="muted">{{ i18n.t('mm.awaitClassify') }}</p> }
            }
            @case ('Classified') {
              @if (m.requiresSecondReview) {
                @if (perms.can('mortality-review.approve')) {
                  <p class="muted">{{ i18n.t('mm.secondReviewHint') }}</p>
                  <form [formGroup]="secondForm" (ngSubmit)="secondReview(m.id)">
                    <label>{{ i18n.t('mm.secondReviewNotes') }}</label>
                    <textarea rows="3" formControlName="notes"></textarea>
                    <label class="chk"><input type="checkbox" formControlName="concurs" /> {{ i18n.t('mm.concursWithFirst') }}</label>
                    <button type="submit" [disabled]="secondForm.invalid || facade.loading()">{{ i18n.t('mm.recordSecondReview') }}</button>
                  </form>
                } @else { <p class="muted">{{ i18n.t('mm.awaitSecondReview') }}</p> }
              } @else if (perms.can('mortality-review.void')) {
                <p class="muted">{{ i18n.t('mm.expectedCloseHint') }}</p>
                <button (click)="facade.closeReview(m.id)" [disabled]="facade.loading()">{{ i18n.t('mm.close') }}</button>
              }
            }
            @case ('SecondReviewed') {
              @if (perms.can('mortality-review.edit')) {
                <form [formGroup]="committeeForm" (ngSubmit)="committee(m.id)">
                  <label>{{ i18n.t('mm.committeeLearnings') }}</label>
                  <textarea rows="3" formControlName="learnings"></textarea>
                  <button type="submit" [disabled]="committeeForm.invalid || facade.loading()">{{ i18n.t('mm.markCommittee') }}</button>
                </form>
              } @else { <p class="muted">{{ i18n.t('mm.awaitCommittee') }}</p> }
            }
            @case ('CommitteeDiscussed') {
              @if (perms.can('mortality-review.void')) {
                <button (click)="facade.closeReview(m.id)" [disabled]="facade.loading()">{{ i18n.t('mm.close') }}</button>
              } @else { <p class="muted">{{ i18n.t('mm.awaitClose') }}</p> }
            }
            @default { <p class="muted">{{ i18n.t('mm.terminal') }}</p> }
          }
        </section>
      </div>

      <qams-audit-trail [subject]="m.id" />
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
    .chk { display: flex; align-items: center; gap: .4rem; margin: .5rem 0; } .chk input { width: auto; }
    .danger-text { color: var(--nt-ink-crit); }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    h3 { margin-top: 1rem; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class MortalityReviewDetailComponent implements OnInit {
  readonly facade = inject(MortalityReviewFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound review id (provided via withComponentInputBinding). */
  readonly id = input.required<string>();
  readonly review = this.facade.selected;
  readonly classifications = DEATH_CLASSIFICATIONS;

  readonly classifyForm = this.fb.nonNullable.group({
    classification: ['Expected' as DeathClassification, [Validators.required]],
    findings: ['', [Validators.required, Validators.maxLength(4000)]],
  });
  readonly secondForm = this.fb.nonNullable.group({
    notes: ['', [Validators.required, Validators.maxLength(4000)]],
    concurs: [true],
  });
  readonly committeeForm = this.fb.nonNullable.group({
    learnings: ['', [Validators.required, Validators.maxLength(4000)]],
  });

  /** An expected death skips the second-review and committee steps. */
  steps(requiresSecondReview: boolean): readonly string[] {
    return requiresSecondReview
      ? ['Reported', 'Classified', 'SecondReviewed', 'CommitteeDiscussed', 'Closed']
      : ['Reported', 'Classified', 'Closed'];
  }

  ngOnInit(): void {
    void this.facade.loadReview(this.id());
  }

  async classify(id: string): Promise<void> {
    if (this.classifyForm.invalid) { return; }
    await this.facade.classify(id, this.classifyForm.getRawValue());
  }

  async secondReview(id: string): Promise<void> {
    if (this.secondForm.invalid) { return; }
    await this.facade.secondReview(id, this.secondForm.getRawValue());
  }

  async committee(id: string): Promise<void> {
    if (this.committeeForm.invalid) { return; }
    await this.facade.committeeDiscussed(id, this.committeeForm.getRawValue());
  }
}
