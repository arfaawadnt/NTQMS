import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ChangeFacade } from './change.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Change Control workspace. Enforces (client-side, matching the domain) the core
 * invariant that a change can only be approved once a risk assessment is linked;
 * approve/reject are QM-gated and available only while Proposed, close only while
 * Approved. Closed and rejected changes are read-only.
 */
@Component({
    selector: 'qams-change-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
    template: `
    @if (item(); as c) {
      <qams-page-header [title]="c.changeRef + ' — ' + c.title" [subtitle]="i18n.t('chg.proposedBy') + ': ' + c.proposedBy">
        <a routerLink="/changes" class="ghost-link">← {{ i18n.t('chg.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="c.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="c.status" /></div>
        <div>
          <span class="muted">{{ i18n.t('chg.linkedRisk') }}</span>
          @if (c.riskItemId) { <a [routerLink]="['/risks', c.riskItemId]">{{ i18n.t('chg.viewRisk') }}</a> }
          @else { <span class="muted">{{ i18n.t('common.no') }}</span> }
        </div>
        @if (c.approvedBy) { <div><span class="muted">{{ i18n.t('chg.approvedBy') }}</span> {{ c.approvedBy }} · {{ c.approvedAtUtc | date:'medium' }}</div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <section class="card">
        <h3>{{ i18n.t('chg.impact') }}</h3>
        <p class="pre">{{ c.impactAnalysis }}</p>
      </section>

      @if (c.rejectionReason) { <div class="error">{{ i18n.t('chg.rejected') }}: {{ c.rejectionReason }}</div> }
      @if (c.implementationNotes) {
        <section class="card"><h3>{{ i18n.t('chg.implNotes') }}</h3><p class="pre">{{ c.implementationNotes }}</p></section>
      }
      @if (c.status === 'Reviewed') {
        <section class="card pir" [class.ok]="c.changeEffective" [class.bad]="c.changeEffective === false">
          <h3>{{ i18n.t('chg.pir') }}</h3>
          <p><b>{{ c.changeEffective ? i18n.t('chg.pirEffectiveYes') : i18n.t('chg.pirEffectiveNo') }}</b>
            <span class="muted"> · {{ c.postImplementationReviewedBy }} · {{ c.postImplementationReviewedAtUtc | date:'medium' }}</span></p>
          <p class="pre">{{ c.postImplementationReviewNotes }}</p>
        </section>
      }

      @if (c.status === 'Proposed') {
        <section class="card">
          <h3>{{ i18n.t('chg.workflow') }}</h3>

          @if (!c.riskItemId) {
            <form [formGroup]="linkForm" (ngSubmit)="linkRisk(c.id)">
              <label>{{ i18n.t('chg.linkRisk') }}</label>
              <select formControlName="riskItemId">
                <option value="">{{ i18n.t('chg.selectRisk') }}</option>
                @for (r of facade.riskOptions(); track r.id) {
                  <option [value]="r.id">{{ r.riskRef }} — {{ r.title }}</option>
                }
              </select>
              <button type="submit" [disabled]="linkForm.invalid">{{ i18n.t('chg.link') }}</button>
            </form>
          }

          @if (perms.canAny('changes.approve', 'changes.void')) {
            <div class="decision">
              <button (click)="facade.approve(c.id)" [disabled]="!c.riskItemId">{{ i18n.t('chg.approve') }}</button>
              @if (!c.riskItemId) { <p class="muted small">{{ i18n.t('chg.approveHint') }}</p> }
            </div>
            <form [formGroup]="rejectForm" (ngSubmit)="reject(c.id)">
              <label>{{ i18n.t('chg.rejectReason') }}</label>
              <input formControlName="reason" />
              <button type="submit" class="secondary" [disabled]="rejectForm.invalid">{{ i18n.t('chg.reject') }}</button>
            </form>
          } @else {
            <p class="muted">{{ i18n.t('chg.approverOnly') }}</p>
          }
        </section>
      } @else if (c.status === 'Approved') {
        <section class="card">
          <h3>{{ i18n.t('chg.closeOut') }}</h3>
          <form [formGroup]="closeForm" (ngSubmit)="close(c.id)">
            <label>{{ i18n.t('chg.implNotes') }}</label>
            <textarea formControlName="implementationNotes" rows="3"></textarea>
            <button type="submit" [disabled]="closeForm.invalid">{{ i18n.t('chg.close') }}</button>
          </form>
        </section>
      } @else if (c.status === 'Closed' && perms.can('changes.approve')) {
        <section class="card">
          <h3>{{ i18n.t('chg.pir') }}</h3>
          <p class="muted small">{{ i18n.t('chg.pirHint') }}</p>
          <form [formGroup]="reviewForm" (ngSubmit)="review(c.id)">
            <label>{{ i18n.t('chg.pirEffective') }}</label>
            <select formControlName="effective">
              <option [ngValue]="true">{{ i18n.t('common.yes') }}</option>
              <option [ngValue]="false">{{ i18n.t('common.no') }}</option>
            </select>
            <label>{{ i18n.t('chg.pirNotes') }}</label>
            <textarea formControlName="notes" rows="3"></textarea>
            <button type="submit" [disabled]="reviewForm.invalid">{{ i18n.t('chg.recordPir') }}</button>
          </form>
        </section>
      }
    
      <qams-audit-trail [subject]="c.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .pre { white-space: pre-wrap; margin: 0; }
    .decision { margin-bottom: .75rem; }
    .decision button { width: auto; }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form textarea, form select { width: 100%; }
    form button { width: auto; margin-top: .5rem; }
    .small { font-size: .78rem; margin-top: .35rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    .pir.ok { border-left: 3px solid var(--nt-green); }
    .pir.bad { border-left: 3px solid var(--nt-red); }
  `]
})
export class ChangeDetailComponent implements OnInit {
  readonly facade = inject(ChangeFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound change id. */
  readonly id = input.required<string>();

  /** Canonical workflow path for the stepper (off-path states render as terminal). */
  readonly flowSteps = ['Proposed', 'Approved', 'Closed', 'Reviewed'] as const;

  readonly item = this.facade.selected;

  readonly linkForm = this.fb.nonNullable.group({
    riskItemId: ['', [Validators.required]],
  });
  readonly rejectForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(500)]],
  });
  readonly closeForm = this.fb.nonNullable.group({
    implementationNotes: ['', [Validators.required, Validators.maxLength(4000)]],
  });
  readonly reviewForm = this.fb.nonNullable.group({
    effective: [true, [Validators.required]],
    notes: ['', [Validators.required, Validators.maxLength(4000)]],
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
    void this.facade.loadRiskOptions();
  }

  async linkRisk(id: string): Promise<void> {
    if (this.linkForm.invalid) { return; }
    await this.facade.linkRisk(id, this.linkForm.getRawValue().riskItemId);
    this.linkForm.reset();
  }

  async reject(id: string): Promise<void> {
    if (this.rejectForm.invalid) { return; }
    await this.facade.reject(id, this.rejectForm.getRawValue().reason);
    this.rejectForm.reset();
  }

  async close(id: string): Promise<void> {
    if (this.closeForm.invalid) { return; }
    await this.facade.close(id, this.closeForm.getRawValue().implementationNotes);
    this.closeForm.reset();
  }

  async review(id: string): Promise<void> {
    if (this.reviewForm.invalid) { return; }
    const { effective, notes } = this.reviewForm.getRawValue();
    await this.facade.review(id, effective, notes);
    this.reviewForm.reset({ effective: true, notes: '' });
  }
}
