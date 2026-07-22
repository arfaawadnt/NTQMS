import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NcFacade } from './nc.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { CAPA_ACTION_TYPES, CapaActionType, RCA_METHODS, RcaMethod } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/**
 * Full nonconformance workspace: header + details, and the context-appropriate
 * workflow action for the current state (submit, triage/reject, RCA, CAPA
 * actions, submit-for-verification, verify, effectiveness). Approver-only
 * actions are hidden for non-approvers (server still enforces).
 */
@Component({
  selector: 'qams-nc-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, NgTemplateOutlet, RouterLink, PageHeaderComponent, StatusPillComponent],
  template: `
    @if (nc(); as n) {
      <qams-page-header [title]="n.ncRef + ' — ' + n.title">
        <a routerLink="/nonconformances" class="ghost-link">← {{ i18n.t('nc.backToList') }}</a>
      </qams-page-header>

      <div class="grid">
        <section class="card">
          <div class="meta">
            <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="n.status" /></div>
            <div><span class="muted">{{ i18n.t('nc.severity') }}</span> {{ n.severity }}</div>
            <div><span class="muted">{{ i18n.t('nc.likelihood') }}</span> {{ n.likelihood }}</div>
            <div><span class="muted">RPN</span> <b [class.danger-text]="n.rpn > 12">{{ n.rpn }}</b></div>
            <div><span class="muted">{{ i18n.t('nc.source') }}</span> {{ n.sourceType }}</div>
          </div>
          <p>{{ n.description }}</p>
          @if (n.rejectionReason) { <p class="error">{{ i18n.t('nc.rejected') }}: {{ n.rejectionReason }}</p> }

          <h3>{{ i18n.t('nc.rcaRecords') }}</h3>
          @if (n.rcaRecords.length === 0) { <p class="muted">—</p> }
          @for (r of n.rcaRecords; track r.id) { <div class="row-item"><b>{{ r.method }}</b> — {{ r.analysis }}</div> }

          <h3>{{ i18n.t('nc.capaActions') }}</h3>
          @if (n.capaActions.length === 0) { <p class="muted">—</p> }
          @for (a of n.capaActions; track a.id) {
            <div class="row-item">
              <div><b>{{ a.type }}</b> — {{ a.details }} <span class="muted">({{ i18n.t('nc.due') }} {{ a.dueDate | date:'mediumDate' }})</span></div>
              <div>
                <qams-status-pill [status]="a.status" />
                @if (a.status !== 'Completed' && canWork()) {
                  <button class="ghost" (click)="facade.completeAction(n.id, a.id)">{{ i18n.t('nc.complete') }}</button>
                }
              </div>
            </div>
          }
        </section>

        <section class="card actions">
          <h3>{{ i18n.t('nc.workflow') }}</h3>
          @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

          @switch (n.status) {
            @case ('Draft') {
              <button (click)="facade.submit(n.id)">{{ i18n.t('nc.submitForTriage') }}</button>
            }
            @case ('Raised') {
              @if (perms.canApprove()) {
                <form [formGroup]="triageForm" (ngSubmit)="facade.triage(n.id, triageForm.getRawValue())">
                  <label>{{ i18n.t('nc.assignee') }}</label>
                  <input formControlName="assigneeId" [placeholder]="i18n.t('nc.userIdHint')" />
                  <button type="submit" [disabled]="triageForm.invalid">{{ i18n.t('nc.triage') }}</button>
                </form>
                <form [formGroup]="rejectForm" (ngSubmit)="facade.reject(n.id, rejectForm.getRawValue())">
                  <label>{{ i18n.t('nc.rejectReason') }}</label>
                  <input formControlName="reason" />
                  <button type="submit" class="secondary" [disabled]="rejectForm.invalid">{{ i18n.t('nc.reject') }}</button>
                </form>
              } @else { <p class="muted">{{ i18n.t('nc.awaitTriage') }}</p> }
            }
            @case ('Assigned') { <ng-container *ngTemplateOutlet="rca" /> }
            @case ('Rca') {
              <ng-container *ngTemplateOutlet="rca" />
              <ng-container *ngTemplateOutlet="plan" />
            }
            @case ('ActionPlan') {
              <ng-container *ngTemplateOutlet="plan" />
              <button (click)="facade.submitForVerification(n.id)" [disabled]="!allActionsComplete()">
                {{ i18n.t('nc.submitVerification') }}
              </button>
              @if (!allActionsComplete()) { <p class="muted">{{ i18n.t('nc.completeAllFirst') }}</p> }
            }
            @case ('PendingVerification') {
              @if (perms.canApprove()) {
                <button (click)="facade.verify(n.id, { passed: true })">{{ i18n.t('nc.verifyPass') }}</button>
                <button class="secondary" (click)="facade.verify(n.id, { passed: false })">{{ i18n.t('nc.verifyFail') }}</button>
              } @else { <p class="muted">{{ i18n.t('nc.awaitVerify') }}</p> }
            }
            @case ('EffectivenessCheck') {
              @if (perms.canApprove()) {
                <button (click)="facade.confirmEffectiveness(n.id, { effective: true })">{{ i18n.t('nc.effectiveClose') }}</button>
                <button class="secondary" (click)="facade.confirmEffectiveness(n.id, { effective: false })">{{ i18n.t('nc.notEffective') }}</button>
              } @else { <p class="muted">{{ i18n.t('nc.awaitEffectiveness') }}</p> }
            }
            @default { <p class="muted">{{ i18n.t('nc.terminal') }}</p> }
          }

          <ng-template #rca>
            <form [formGroup]="rcaForm" (ngSubmit)="facade.recordRca(n.id, rcaForm.getRawValue())">
              <label>{{ i18n.t('nc.rcaMethod') }}</label>
              <select formControlName="method">@for (m of rcaMethods; track m) { <option [value]="m">{{ m }}</option> }</select>
              <label>{{ i18n.t('nc.rcaAnalysis') }}</label>
              <textarea rows="2" formControlName="analysis"></textarea>
              <button type="submit" [disabled]="rcaForm.invalid">{{ i18n.t('nc.recordRca') }}</button>
            </form>
          </ng-template>

          <ng-template #plan>
            <form [formGroup]="actionForm" (ngSubmit)="planAction()">
              <label>{{ i18n.t('nc.actionType') }}</label>
              <select formControlName="type">@for (t of actionTypes; track t) { <option [value]="t">{{ t }}</option> }</select>
              <label>{{ i18n.t('nc.actionDetails') }}</label>
              <input formControlName="details" />
              <label>{{ i18n.t('nc.owner') }}</label>
              <input formControlName="ownerId" [placeholder]="i18n.t('nc.userIdHint')" />
              <label>{{ i18n.t('nc.due') }}</label>
              <input type="date" formControlName="dueDate" />
              <button type="submit" [disabled]="actionForm.invalid">{{ i18n.t('nc.addAction') }}</button>
            </form>
          </ng-template>
        </section>
      </div>
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
  styles: [`
    .grid { display: grid; grid-template-columns: 2fr 1fr; gap: 1rem; align-items: start; }
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .row-item { padding: .5rem 0; border-bottom: 1px solid var(--nt-border); display: flex; justify-content: space-between; gap: 1rem; }
    .actions form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    .actions button { margin-top: .5rem; margin-inline-end: .5rem; width: auto; }
    .danger-text { color: var(--nt-danger); }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    h3 { margin-top: 1rem; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `],
})
export class NcDetailComponent implements OnInit {
  readonly facade = inject(NcFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound nonconformance id (provided via withComponentInputBinding). */
  readonly id = input.required<string>();

  readonly nc = this.facade.selected;
  readonly rcaMethods = RCA_METHODS;
  readonly actionTypes = CAPA_ACTION_TYPES;

  /** True when the loaded NC has at least one CAPA action and all are complete. */
  readonly allActionsComplete = computed(() => {
    const n = this.nc();
    return !!n && n.capaActions.length > 0 && n.capaActions.every((a) => a.status === 'Completed');
  });

  /** True when the current user can record RCA / work CAPA (any authenticated user). */
  readonly canWork = computed(() => true);

  readonly triageForm = this.fb.nonNullable.group({ assigneeId: ['', [Validators.required]] });
  readonly rejectForm = this.fb.nonNullable.group({ reason: ['', [Validators.required, Validators.maxLength(1000)]] });
  readonly rcaForm = this.fb.nonNullable.group({
    method: ['FiveWhys' as RcaMethod, [Validators.required]],
    analysis: ['', [Validators.required, Validators.maxLength(8000)]],
  });
  readonly actionForm = this.fb.nonNullable.group({
    type: ['Corrective' as CapaActionType, [Validators.required]],
    details: ['', [Validators.required, Validators.maxLength(2000)]],
    ownerId: ['', [Validators.required]],
    dueDate: ['', [Validators.required]],
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
  }

  async planAction(): Promise<void> {
    if (this.actionForm.invalid) { return; }
    await this.facade.planAction(this.id(), this.actionForm.getRawValue());
    this.actionForm.reset({ type: 'Corrective', details: '', ownerId: '', dueDate: '' });
  }
}
