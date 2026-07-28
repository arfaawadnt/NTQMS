import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthorizationsFacade } from './authorizations.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { OrgDataService } from '../../core/org-data.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Authorization workspace: the grant (person, test, scope), its competency
 * evidence and inherited expiry, and the lifecycle actions — suspend/reinstate
 * and terminal revoke. Expiry and competency-lapse suspension come from the
 * backend (sweep + saga), never from the client.
 */
@Component({
    selector: 'qams-authorization-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
    template: `
    @if (item(); as a) {
      <qams-page-header
        [title]="org.userName(a.userId) || i18n.t('authz.person')"
        [subtitle]="a.testCode + ' — ' + a.testName + ' · ' + i18n.t('authz.scope' + a.scope)">
        <a routerLink="/authorizations" class="ghost-link">← {{ i18n.t('authz.backToMatrix') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="a.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="a.status" /></div>
        <div><span class="muted">{{ i18n.t('authz.grantedOn') }}</span> {{ a.grantedOn | date:'mediumDate' }}</div>
        <div><span class="muted">{{ i18n.t('authz.grantedBy') }}</span> {{ org.userName(a.grantedBy) || '—' }}</div>
        <div><span class="muted">{{ i18n.t('std.expiresOn') }}</span> {{ a.expiresOn | date:'mediumDate' }}</div>
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <section class="card">
        <h3>{{ i18n.t('authz.evidence') }}</h3>
        <p>{{ i18n.t('authz.evidenceLine') }}: <b>{{ a.competencySubject ?? a.competencyRecordId }}</b></p>
        <p class="muted">{{ i18n.t('authz.expiryNote') }}</p>
      </section>

      <section class="card">
        <h3>{{ i18n.t('val.workflow') }}</h3>
        @if (a.status === 'Active' && perms.canAssignTraining()) {
          <form [formGroup]="reasonForm" (ngSubmit)="suspend(a.id)">
            <label>{{ i18n.t('authz.suspendReason') }}</label>
            <input formControlName="reason" />
            <button type="submit" class="secondary" [disabled]="reasonForm.invalid">{{ i18n.t('authz.suspend') }}</button>
          </form>
        }
        @if (a.status === 'Suspended') {
          <p class="warn">{{ i18n.t('authz.suspendedNote') }} <b>{{ a.suspensionReason }}</b></p>
          @if (perms.canApprove()) {
            <button (click)="facade.reinstate(a.id)">{{ i18n.t('authz.reinstate') }}</button>
          }
        }
        @if ((a.status === 'Active' || a.status === 'Suspended') && perms.canApprove()) {
          <form [formGroup]="revokeForm" (ngSubmit)="revoke(a.id)">
            <label>{{ i18n.t('authz.revokeReason') }}</label>
            <input formControlName="reason" />
            <button type="submit" class="danger" [disabled]="revokeForm.invalid">{{ i18n.t('authz.revoke') }}</button>
          </form>
        }
        @if (a.status === 'Revoked') { <p class="warn">{{ i18n.t('authz.revokedNote') }} <b>{{ a.revocationReason }}</b></p> }
        @if (a.status === 'Expired') { <p class="muted">{{ i18n.t('authz.expiredNote') }}</p> }
      </section>

      <qams-audit-trail [subject]="a.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    section { margin-bottom: 1rem; }
    .warn { color: var(--nt-red); }
    form { margin-top: .75rem; }
    form button, section > button { width: auto; margin-top: .5rem; }
    .danger { background: var(--nt-red); color: #fff; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
  `]
})
export class AuthorizationDetailComponent implements OnInit {
  readonly facade = inject(AuthorizationsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  readonly org = inject(OrgDataService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound authorization id. */
  readonly id = input.required<string>();

  /** Canonical lifecycle for the stepper (Suspended/Revoked render off-path). */
  readonly flowSteps = ['Active', 'Expired'] as const;

  readonly item = this.facade.selected;

  readonly reasonForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(1000)]],
  });
  readonly revokeForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
    void this.org.ensureDirectory();
  }

  async suspend(id: string): Promise<void> {
    if (this.reasonForm.invalid) { return; }
    await this.facade.suspend(id, this.reasonForm.getRawValue().reason);
    this.reasonForm.reset();
  }

  async revoke(id: string): Promise<void> {
    if (this.revokeForm.invalid) { return; }
    await this.facade.revoke(id, this.revokeForm.getRawValue().reason);
    this.revokeForm.reset();
  }
}
