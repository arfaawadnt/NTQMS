import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { StandardsFacade } from './standards.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Reference standard workspace: the documented traceability chain (issuer,
 * certificate, certified value ± uncertainty), the certificate validity window,
 * and the lifecycle actions — quarantine/reactivate and terminal retire.
 * Expiry is latched by the backend sweep, never by the client.
 */
@Component({
    selector: 'qams-standards-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
    template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.standardRef + ' — ' + s.name" [subtitle]="i18n.t('std.type' + s.type)">
        <a routerLink="/reference-standards" class="ghost-link">← {{ i18n.t('std.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.status" /></div>
        <div><span class="muted">{{ i18n.t('std.receivedOn') }}</span> {{ s.receivedOn | date:'mediumDate' }}</div>
        <div><span class="muted">{{ i18n.t('std.expiresOn') }}</span> {{ s.expiresOn ? (s.expiresOn | date:'mediumDate') : '—' }}</div>
        @if (s.status !== 'Retired' && perms.can('reference-standards.void')) {
          <button class="secondary" (click)="facade.retire(s.id)">{{ i18n.t('std.retire') }}</button>
        }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <section class="card">
        <h3>{{ i18n.t('std.traceability') }}</h3>
        <dl class="pairs">
          <dt>{{ i18n.t('std.traceableTo') }}</dt><dd><b>{{ s.traceableTo }}</b></dd>
          <dt>{{ i18n.t('std.manufacturer') }}</dt><dd>{{ s.manufacturer ?? '—' }}</dd>
          <dt>{{ i18n.t('std.lot') }}</dt><dd>{{ s.lotNumber ?? '—' }}</dd>
          <dt>{{ i18n.t('std.certificateNo') }}</dt><dd>{{ s.certificateNumber ?? '—' }}</dd>
          <dt>{{ i18n.t('std.certifiedValue') }}</dt><dd>{{ s.certifiedValue ?? '—' }}</dd>
          <dt>{{ i18n.t('std.uncertainty') }}</dt><dd>{{ s.uncertaintyStatement ?? '—' }}</dd>
        </dl>
      </section>

      <section class="card">
        <h3>{{ i18n.t('val.workflow') }}</h3>
        @if (s.status === 'Quarantined') {
          <p class="warn">{{ i18n.t('std.quarantinedNote') }} <b>{{ s.quarantineReason }}</b></p>
          @if (perms.can('reference-standards.approve')) {
            <button (click)="facade.reactivate(s.id)">{{ i18n.t('std.reactivate') }}</button>
          }
        } @else if (s.status === 'Active' && perms.can('reference-standards.edit')) {
          <form [formGroup]="quarantineForm" (ngSubmit)="quarantine(s.id)">
            <label>{{ i18n.t('std.quarantineReason') }}</label>
            <input formControlName="reason" [placeholder]="i18n.t('std.quarantineReasonHint')" />
            <button type="submit" class="secondary" [disabled]="quarantineForm.invalid">{{ i18n.t('std.quarantine') }}</button>
          </form>
        } @else if (s.status === 'Expired') {
          <p class="warn">{{ i18n.t('std.expiredNote') }}</p>
        } @else if (s.status === 'Retired') {
          <p class="muted">{{ i18n.t('std.retiredNote') }}</p>
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
    .meta button { width: auto; margin-inline-start: auto; }
    section { margin-bottom: 1rem; }
    .pairs { display: grid; grid-template-columns: max-content 1fr; gap: .4rem 1.25rem; margin: 0; }
    .pairs dt { color: var(--nt-muted); }
    .pairs dd { margin: 0; }
    .warn { color: var(--nt-red); }
    form button, section > button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
  `]
})
export class StandardsDetailComponent implements OnInit {
  readonly facade = inject(StandardsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound standard id. */
  readonly id = input.required<string>();

  /** Canonical lifecycle for the stepper (Quarantined/Expired render off-path). */
  readonly flowSteps = ['Active', 'Retired'] as const;

  readonly item = this.facade.selected;

  readonly quarantineForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  async quarantine(id: string): Promise<void> {
    if (this.quarantineForm.invalid) { return; }
    await this.facade.quarantine(id, this.quarantineForm.getRawValue().reason);
    this.quarantineForm.reset();
  }
}
