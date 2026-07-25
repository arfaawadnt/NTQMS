import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { SupplierFacade } from './supplier.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { LovSelectComponent } from '../../shared/ui/lov-select.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/** Typed shape of one evaluation-criterion row in the form array. */
interface CriterionForm {
  criterion: FormControl<string>;
  weight: FormControl<number>;
  score: FormControl<number>;
}

/**
 * Supplier workspace: certificates (optional file upload/download), the
 * approve/suspend lifecycle, and weighted performance evaluations captured via
 * a dynamic criteria FormArray (scores 0-100, weights normalised server-side).
 * Approval is QM-gated and subject to segregation of duties — the registrant
 * cannot approve their own supplier (enforced by the backend, surfaced here).
 */
@Component({
  selector: 'qams-supplier-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, LovSelectComponent],
  template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.supplierRef + ' — ' + s.name" [subtitle]="s.supplierType">
        <a routerLink="/suppliers" class="ghost-link">← {{ i18n.t('sup.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.status" /></div>
        @if (s.approvedBy) { <div><span class="muted">{{ i18n.t('sup.approvedBy') }}</span> {{ s.approvedBy }}</div> }
        @if (perms.canApprove() && s.status === 'PendingEvaluation') {
          <button (click)="facade.approve(s.id)">{{ i18n.t('sup.approve') }}</button>
        }
      </div>
      @if (s.suspensionReason) { <div class="error">{{ i18n.t('sup.suspended') }}: {{ s.suspensionReason }}</div> }
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <div class="grid2">
        <section class="card">
          <h3>{{ i18n.t('sup.certificates') }}</h3>
          @if (s.certificates.length === 0) { <p class="muted">—</p> }
          @for (c of s.certificates; track c.id) {
            <div class="row-item">
              <div>
                {{ c.certificateType }}
                <span class="muted"> · {{ i18n.t('sup.expires') }} {{ c.expiresAt | date:'mediumDate' }}</span>
              </div>
              @if (c.fileId) { <a [href]="facade.downloadUrl(c.fileId)" target="_blank" rel="noopener">{{ i18n.t('doc.download') }}</a> }
            </div>
          }
          <form [formGroup]="certForm" (ngSubmit)="addCertificate(s.id)">
            <label>{{ i18n.t('sup.certType') }}</label><qams-lov-select formControlName="certificateType" category="CERTIFICATE_TYPE" [placeholder]="i18n.t('sup.certTypeHint')" />
            <label>{{ i18n.t('sup.expires') }}</label><input type="date" formControlName="expiresAt" />
            <label>{{ i18n.t('sup.certFile') }}</label><input type="file" (change)="onFile($event)" />
            <button type="submit" [disabled]="certForm.invalid">{{ i18n.t('sup.addCert') }}</button>
          </form>
        </section>

        <section class="card">
          <h3>{{ i18n.t('sup.lifecycle') }}</h3>
          @if (s.status === 'Approved' && perms.canApprove()) {
            <form [formGroup]="suspendForm" (ngSubmit)="suspend(s.id)">
              <label>{{ i18n.t('sup.suspendReason') }}</label>
              <input formControlName="reason" />
              <button type="submit" class="danger" [disabled]="suspendForm.invalid">{{ i18n.t('sup.suspend') }}</button>
            </form>
          } @else if (s.status === 'PendingEvaluation') {
            <p class="muted">{{ i18n.t('sup.pendingNote') }}</p>
          } @else if (s.status === 'Suspended') {
            <p class="muted">{{ i18n.t('sup.suspendedNote') }}</p>
          } @else {
            <p class="muted">{{ i18n.t('comp.approverOnly') }}</p>
          }
        </section>
      </div>

      <section class="card">
        <h3>{{ i18n.t('sup.evaluations') }}</h3>
        @if (facade.evaluations().length === 0) { <p class="muted">{{ i18n.t('sup.noEvaluations') }}</p> }
        @for (e of facade.evaluations(); track e.id) {
          <div class="row-item">
            <div>{{ e.periodStart | date:'mediumDate' }} → {{ e.periodEnd | date:'mediumDate' }}</div>
            <b [class.low]="e.weightedTotal < 70">{{ e.weightedTotal | number:'1.2-2' }}%</b>
          </div>
        }

        @if (perms.canApprove()) {
          <form [formGroup]="evalForm" (ngSubmit)="recordEvaluation(s.id)">
            <div class="period">
              <div><label>{{ i18n.t('sup.periodStart') }}</label><input type="date" formControlName="periodStart" /></div>
              <div><label>{{ i18n.t('sup.periodEnd') }}</label><input type="date" formControlName="periodEnd" /></div>
            </div>

            <label>{{ i18n.t('sup.criteria') }}</label>
            <div formArrayName="criteria">
              @for (row of criteria.controls; track row; let i = $index) {
                <div class="crit-row" [formGroupName]="i">
                  <input formControlName="criterion" [placeholder]="i18n.t('sup.criterionHint')" />
                  <input type="number" min="0" step="0.1" formControlName="weight" [attr.aria-label]="i18n.t('sup.weight')" />
                  <input type="number" min="0" max="100" formControlName="score" [attr.aria-label]="i18n.t('sup.score')" />
                  <button type="button" class="link" (click)="removeCriterion(i)" [disabled]="criteria.length === 1">✕</button>
                </div>
              }
            </div>
            <div class="crit-legend muted">{{ i18n.t('sup.criteriaLegend') }}</div>
            <div class="row">
              <button type="button" class="secondary" (click)="addCriterion()">{{ i18n.t('sup.addCriterion') }}</button>
              <button type="submit" [disabled]="evalForm.invalid">{{ i18n.t('sup.recordEvaluation') }}</button>
            </div>
          </form>
        }
      </section>
    
      <qams-audit-trail [subject]="s.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
  styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .meta button { margin-inline-start: auto; }
    .grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; align-items: start; margin-bottom: 1rem; }
    .row-item { padding: .5rem 0; border-bottom: 1px solid var(--nt-border); display: flex; justify-content: space-between; gap: 1rem; align-items: center; }
    .low { color: var(--nt-red); }
    .period { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .crit-row { display: grid; grid-template-columns: 1fr 90px 90px 36px; gap: 8px; margin-bottom: 6px; align-items: center; }
    .crit-legend { font-size: 11px; margin: 2px 0 8px; }
    .row { display: flex; gap: .6rem; margin-top: .5rem; }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 800px) { .grid2 { grid-template-columns: 1fr; } }
  `],
})
export class SupplierDetailComponent implements OnInit {
  readonly facade = inject(SupplierFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound supplier id. */
  readonly id = input.required<string>();

  /** Canonical workflow path for the stepper (off-path states render as terminal). */
  readonly flowSteps = ['PendingEvaluation', 'Approved'] as const;

  readonly item = this.facade.selected;
  readonly certFile = signal<File | null>(null);

  readonly certForm = this.fb.nonNullable.group({
    certificateType: ['', [Validators.required, Validators.maxLength(200)]],
    expiresAt: ['', [Validators.required]],
  });
  readonly suspendForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(500)]],
  });
  readonly evalForm = this.fb.nonNullable.group({
    periodStart: ['', [Validators.required]],
    periodEnd: ['', [Validators.required]],
    criteria: this.fb.array<FormGroup<CriterionForm>>([this.buildCriterion()]),
  });

  get criteria(): FormArray<FormGroup<CriterionForm>> {
    return this.evalForm.controls.criteria;
  }

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  onFile(event: Event): void { this.certFile.set((event.target as HTMLInputElement).files?.[0] ?? null); }

  addCriterion(): void { this.criteria.push(this.buildCriterion()); }

  removeCriterion(index: number): void {
    if (this.criteria.length > 1) { this.criteria.removeAt(index); }
  }

  async addCertificate(id: string): Promise<void> {
    if (this.certForm.invalid) { return; }
    const { certificateType, expiresAt } = this.certForm.getRawValue();
    await this.facade.addCertificate(id, certificateType, expiresAt, this.certFile());
    this.certFile.set(null);
    this.certForm.reset();
  }

  async suspend(id: string): Promise<void> {
    if (this.suspendForm.invalid) { return; }
    await this.facade.suspend(id, this.suspendForm.getRawValue().reason);
    this.suspendForm.reset();
  }

  async recordEvaluation(id: string): Promise<void> {
    if (this.evalForm.invalid) { return; }
    const raw = this.evalForm.getRawValue();
    await this.facade.recordEvaluation(id, raw);
    this.evalForm.reset();
    this.criteria.clear();
    this.addCriterion();
  }

  private buildCriterion(): FormGroup<CriterionForm> {
    return this.fb.nonNullable.group({
      criterion: ['', [Validators.required, Validators.maxLength(200)]],
      weight: [1, [Validators.required, Validators.min(0)]],
      score: [80, [Validators.required, Validators.min(0), Validators.max(100)]],
    }) as FormGroup<CriterionForm>;
  }
}
