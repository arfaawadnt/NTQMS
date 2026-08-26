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
import { EsignCredentials, EsignDialogComponent } from '../../shared/ui/esign-dialog.component';

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
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, LovSelectComponent, EsignDialogComponent],
    template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.supplierRef + ' — ' + s.name" [subtitle]="s.supplierType">
        <a routerLink="/suppliers" class="ghost-link">← {{ i18n.t('sup.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.status" /></div>
        @if (s.approvedBy) { <div><span class="muted">{{ i18n.t('sup.approvedBy') }}</span> {{ s.approvedBy }}</div> }
        @if (perms.can('suppliers.sign') && s.status === 'PendingEvaluation') {
          <button (click)="esignOpen.set(true)">{{ i18n.t('sup.approve') }}</button>
          <qams-esign-dialog [open]="esignOpen()" [meaning]="i18n.t('esign.signMeaning')" [busy]="facade.loading()" [error]="facade.error()" (confirm)="doApprove(s.id, $event)" (cancel)="esignOpen.set(false)" />
        }
      </div>
      @if (s.isOutsourcedClinicalService) {
        <div class="banner outsourced">{{ i18n.t('sup.outsourcedBanner') }}@if (s.serviceScope) { <span> — {{ s.serviceScope }}</span> }</div>
      }
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
          @if (s.status === 'Approved' && perms.can('suppliers.void')) {
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

      <div class="grid2">
        <!-- Contract / SLA register (HQMS M16) -->
        <section class="card">
          <h3>{{ i18n.t('sup.contracts') }}</h3>
          @if (s.contracts.length === 0) { <p class="muted">{{ i18n.t('sup.noContracts') }}</p> }
          @for (c of s.contracts; track c.id) {
            <div class="row-item">
              <div>
                <b>{{ c.title }}</b> <span class="muted code">· {{ c.contractRef }}</span><br />
                <span class="muted">{{ c.startDate | date:'mediumDate' }} → {{ c.endDate | date:'mediumDate' }}</span>
                @if (c.isExpired) { <span class="tag expired">{{ i18n.t('sup.expired') }}</span> }
                @if (c.slaSummary) { <div class="muted small">{{ c.slaSummary }}</div> }
              </div>
              <div>
                <qams-status-pill [status]="c.status" />
                @if (c.status === 'Active' && s.status !== 'Suspended') { <button type="button" class="link" (click)="startTerminate(c.id)">{{ i18n.t('sup.terminate') }}</button> }
              </div>
            </div>
          }
          @if (terminatingContract()) {
            <form [formGroup]="terminateForm" (ngSubmit)="terminate(s.id)">
              <label>{{ i18n.t('sup.terminationReason') }}</label><input formControlName="reason" />
              <button type="submit" class="danger" [disabled]="terminateForm.invalid">{{ i18n.t('sup.terminate') }}</button>
              <button type="button" class="secondary" (click)="terminatingContract.set(null)">{{ i18n.t('nc.cancel') }}</button>
            </form>
          }
          <form [formGroup]="contractForm" (ngSubmit)="addContract(s.id)">
            <label>{{ i18n.t('sup.contractTitle') }}</label><input formControlName="title" />
            <div class="period">
              <div><label>{{ i18n.t('sup.startDate') }}</label><input type="date" formControlName="startDate" /></div>
              <div><label>{{ i18n.t('sup.endDate') }}</label><input type="date" formControlName="endDate" /></div>
            </div>
            <label>{{ i18n.t('sup.slaSummary') }}</label><input formControlName="slaSummary" />
            <button type="submit" [disabled]="contractForm.invalid">{{ i18n.t('sup.addContract') }}</button>
          </form>
        </section>

        <!-- Corrective-action requests (HQMS M16) -->
        <section class="card">
          <h3>{{ i18n.t('sup.cars') }}</h3>
          @if (s.cars.length === 0) { <p class="muted">{{ i18n.t('sup.noCars') }}</p> }
          @for (car of s.cars; track car.id) {
            <div class="row-item">
              <div>
                {{ car.description }}<br />
                <span class="muted">{{ i18n.t('sup.raisedOn') }} {{ car.raisedOn | date:'mediumDate' }}</span>
                @if (car.dueDate) { <span class="muted" [class.danger-text]="car.isOverdue"> · {{ i18n.t('sup.due') }} {{ car.dueDate | date:'mediumDate' }}@if (car.isOverdue) { <b> · {{ i18n.t('sup.overdue') }}</b> }</span> }
                @if (car.responseNote) { <div class="muted small">↳ {{ car.responseNote }}</div> }
                @if (car.status === 'Closed') { <div class="small">{{ car.effective ? i18n.t('sup.effective') : i18n.t('sup.notEffective') }}</div> }
              </div>
              <div>
                <qams-status-pill [status]="car.status" />
                @if (s.status !== 'Suspended') {
                  @if (car.status === 'Open') { <button type="button" class="link" (click)="startResponse(car.id)">{{ i18n.t('sup.respond') }}</button> }
                  @else if (car.status === 'ResponseReceived' && perms.can('suppliers.approve')) { <button type="button" class="link" (click)="startClose(car.id)">{{ i18n.t('sup.closeCar') }}</button> }
                }
              </div>
            </div>
          }
          @if (respondingCar()) {
            <form [formGroup]="responseForm" (ngSubmit)="recordResponse(s.id)">
              <label>{{ i18n.t('sup.responseNote') }}</label><input formControlName="note" />
              <label>{{ i18n.t('sup.responseOn') }}</label><input type="date" formControlName="on" />
              <button type="submit" [disabled]="responseForm.invalid">{{ i18n.t('sup.recordResponse') }}</button>
              <button type="button" class="secondary" (click)="respondingCar.set(null)">{{ i18n.t('nc.cancel') }}</button>
            </form>
          }
          @if (closingCar()) {
            <form [formGroup]="closeForm" (ngSubmit)="closeCar(s.id)">
              <label class="chk"><input type="checkbox" formControlName="effective" /> {{ i18n.t('sup.wasEffective') }}</label>
              <label>{{ i18n.t('sup.closureNote') }}</label><input formControlName="closureNote" />
              <button type="submit" [disabled]="closeForm.invalid">{{ i18n.t('sup.closeCar') }}</button>
              <button type="button" class="secondary" (click)="closingCar.set(null)">{{ i18n.t('nc.cancel') }}</button>
            </form>
          }
          <form [formGroup]="carForm" (ngSubmit)="raiseCar(s.id)">
            <label>{{ i18n.t('sup.carDescription') }}</label><input formControlName="description" />
            <label>{{ i18n.t('sup.due') }}</label><input type="date" formControlName="dueDate" />
            <button type="submit" [disabled]="carForm.invalid">{{ i18n.t('sup.raiseCar') }}</button>
          </form>
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

        @if (perms.can('suppliers.approve')) {
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
    button.link { background: none; border: none; color: var(--nt-blue); cursor: pointer; padding: 0; text-decoration: underline; width: auto; }
    .banner { padding: .5rem .8rem; border-radius: 6px; margin-bottom: 1rem; font-weight: 600; }
    .banner.outsourced { background: color-mix(in srgb, var(--nt-ink-info) 14%, transparent); color: var(--nt-ink-info); }
    .tag.expired { margin-inline-start: 6px; padding: 1px 6px; border-radius: 999px; font-size: 10.5px; font-weight: 700; background: var(--nt-ink-crit); color: #fff; }
    .small { font-size: .72rem; } .danger-text { color: var(--nt-ink-crit); }
    .chk { display: flex; align-items: center; gap: .4rem; } .chk input { width: auto; }
    @media (max-width: 800px) { .grid2 { grid-template-columns: 1fr; } }
  `]
})
export class SupplierDetailComponent implements OnInit {
  readonly facade = inject(SupplierFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound supplier id. */
  readonly id = input.required<string>();

  /** Whether the Part 11 e-signature dialog is open for the approval. */
  readonly esignOpen = signal(false);

  /** Approves through the ceremony dialog; closes on success, stays open (showing the error) on failure. */
  async doApprove(id: string, credentials: EsignCredentials): Promise<void> {
    await this.facade.approve(id, credentials);
    if (this.facade.error() === '') { this.esignOpen.set(false); }
  }

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

  // ── Contract / SLA register & CARs (HQMS M16) ───────────────────────────────
  readonly terminatingContract = signal<string | null>(null);
  readonly respondingCar = signal<string | null>(null);
  readonly closingCar = signal<string | null>(null);

  readonly contractForm = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    startDate: ['', [Validators.required]],
    endDate: ['', [Validators.required]],
    slaSummary: ['', [Validators.maxLength(4000)]],
  });
  readonly terminateForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(1000)]],
  });
  readonly carForm = this.fb.nonNullable.group({
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    dueDate: [''],
  });
  readonly responseForm = this.fb.nonNullable.group({
    note: ['', [Validators.required, Validators.maxLength(4000)]],
    on: ['', [Validators.required]],
  });
  readonly closeForm = this.fb.nonNullable.group({
    effective: [true],
    closureNote: ['', [Validators.required, Validators.maxLength(4000)]],
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

  // ── Contract / SLA register (HQMS M16) ──────────────────────────────────────
  startTerminate(contractId: string): void { this.terminateForm.reset({ reason: '' }); this.terminatingContract.set(contractId); }

  async addContract(id: string): Promise<void> {
    if (this.contractForm.invalid) { return; }
    const raw = this.contractForm.getRawValue();
    await this.facade.addContract(id, {
      title: raw.title, startDate: raw.startDate, endDate: raw.endDate, slaSummary: raw.slaSummary || null,
    });
    if (this.facade.error() === '') { this.contractForm.reset(); }
  }

  async terminate(id: string): Promise<void> {
    const contractId = this.terminatingContract();
    if (!contractId || this.terminateForm.invalid) { return; }
    await this.facade.terminateContract(id, contractId, this.terminateForm.getRawValue());
    if (this.facade.error() === '') { this.terminatingContract.set(null); }
  }

  // ── Corrective-action requests (HQMS M16) ───────────────────────────────────
  startResponse(carId: string): void { this.responseForm.reset({ note: '', on: '' }); this.respondingCar.set(carId); }
  startClose(carId: string): void { this.closeForm.reset({ effective: true, closureNote: '' }); this.closingCar.set(carId); }

  async raiseCar(id: string): Promise<void> {
    if (this.carForm.invalid) { return; }
    const raw = this.carForm.getRawValue();
    await this.facade.raiseCar(id, {
      description: raw.description, raisedOn: new Date().toISOString().slice(0, 10), dueDate: raw.dueDate || null,
    });
    if (this.facade.error() === '') { this.carForm.reset(); }
  }

  async recordResponse(id: string): Promise<void> {
    const carId = this.respondingCar();
    if (!carId || this.responseForm.invalid) { return; }
    await this.facade.recordCarResponse(id, carId, this.responseForm.getRawValue());
    if (this.facade.error() === '') { this.respondingCar.set(null); }
  }

  async closeCar(id: string): Promise<void> {
    const carId = this.closingCar();
    if (!carId || this.closeForm.invalid) { return; }
    await this.facade.closeCar(id, carId, this.closeForm.getRawValue());
    if (this.facade.error() === '') { this.closingCar.set(null); }
  }

  private buildCriterion(): FormGroup<CriterionForm> {
    return this.fb.nonNullable.group({
      criterion: ['', [Validators.required, Validators.maxLength(200)]],
      weight: [1, [Validators.required, Validators.min(0)]],
      score: [80, [Validators.required, Validators.min(0), Validators.max(100)]],
    }) as FormGroup<CriterionForm>;
  }
}
