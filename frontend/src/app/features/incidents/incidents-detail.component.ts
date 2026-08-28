import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { IncidentsFacade } from './incidents.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { CONTRIBUTING_FACTOR_CATEGORIES, ContributingFactorCategory, INCIDENT_CATEGORIES, IncidentCategory } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { UserSelectComponent } from '../../shared/ui/user-select.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';
import { EsignCredentials, EsignDialogComponent } from '../../shared/ui/esign-dialog.component';
import { SignatureManifestComponent } from '../../shared/ui/signature-manifest.component';

/**
 * Full incident workspace (HQMS M02): header + details, the reconstructed timeline
 * and contributing factors, and the context-appropriate workflow action for the
 * current state. Closing and declaring a sentinel event are Part 11 signing
 * ceremonies. Approver/signer actions are hidden for users who lack the privilege
 * (the server still enforces every call — this is affordance, not a boundary).
 */
@Component({
    selector: 'qams-incidents-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, UserSelectComponent, EsignDialogComponent, SignatureManifestComponent],
    template: `
    @if (incident(); as n) {
      <qams-page-header [title]="n.incidentRef + ' — ' + n.title">
        <a routerLink="/incidents" class="ghost-link">← {{ i18n.t('inc.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="n.status" />

      <div class="banners">
        @if (n.isSentinel) { <div class="banner sentinel">{{ i18n.t('inc.sentinelBanner') }}</div> }
        @if (n.isAnonymous) { <div class="banner anon">{{ i18n.t('inc.anonBanner') }}</div> }
      </div>

      <div class="grid">
        <section class="card">
          <div class="meta">
            <div><span class="muted">{{ i18n.t('inc.status') }}</span><qams-status-pill [status]="n.status" /></div>
            <div><span class="muted">{{ i18n.t('inc.category') }}</span> {{ i18n.t('inc.cat.' + n.category) }}</div>
            <div><span class="muted">{{ i18n.t('inc.harmGrade') }}</span> <b [class.danger-text]="n.harmGrade === 'Severe' || n.harmGrade === 'Death'">{{ i18n.t('inc.harm.' + n.harmGrade) }}</b></div>
            <div><span class="muted">{{ i18n.t('inc.channel') }}</span> {{ i18n.t('inc.ch.' + n.channel) }}</div>
            <div><span class="muted">{{ i18n.t('inc.occurredAt') }}</span> {{ n.occurredAtUtc | date:'medium' }}</div>
            @if (n.location) { <div><span class="muted">{{ i18n.t('inc.location') }}</span> {{ n.location }}</div> }
          </div>
          <p>{{ n.description }}</p>
          @if (n.rejectionReason) { <p class="error">{{ i18n.t('inc.rejected') }}: {{ n.rejectionReason }}</p> }
          @if (n.closureSummary) { <p><b>{{ i18n.t('inc.closure') }}:</b> {{ n.closureSummary }}</p> }

          <h3>{{ i18n.t('inc.contributingFactors') }}</h3>
          @if (n.contributingFactors.length === 0) { <p class="muted">—</p> }
          @for (f of n.contributingFactors; track f.id) {
            <div class="row-item"><b>{{ i18n.t('inc.factor.' + f.category) }}</b> — {{ f.description }}</div>
          }

          <h3>{{ i18n.t('inc.timeline') }}</h3>
          @if (n.timeline.length === 0) { <p class="muted">—</p> }
          @for (t of n.timeline; track t.id) {
            <div class="row-item"><span class="muted">{{ t.occurredAtUtc | date:'short' }}</span> — {{ t.note }}</div>
          }

          @if (n.investigationSummary) {
            <h3>{{ i18n.t('inc.investigationSummary') }}</h3>
            <p>{{ n.investigationSummary }}</p>
          }
        </section>

        <section class="card actions">
          <h3>{{ i18n.t('inc.workflow') }}</h3>
          @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

          @switch (n.status) {
            @case ('Reported') {
              @if (perms.can('incidents.approve')) {
                <form [formGroup]="triageForm" (ngSubmit)="facade.triage(n.id, triageForm.getRawValue())">
                  <label>{{ i18n.t('inc.assignee') }}</label>
                  <qams-user-select formControlName="assigneeId" />
                  <label>{{ i18n.t('inc.category') }}</label>
                  <select formControlName="category">@for (c of categories; track c) { <option [value]="c">{{ i18n.t('inc.cat.' + c) }}</option> }</select>
                  <button type="submit" [disabled]="triageForm.invalid">{{ i18n.t('inc.triage') }}</button>
                </form>
              }
              @if (perms.can('incidents.void')) {
                <form [formGroup]="rejectForm" (ngSubmit)="facade.reject(n.id, rejectForm.getRawValue())">
                  <label>{{ i18n.t('inc.rejectReason') }}</label>
                  <input formControlName="reason" />
                  <button type="submit" class="secondary" [disabled]="rejectForm.invalid">{{ i18n.t('inc.reject') }}</button>
                </form>
              }
              @if (!perms.canAny('incidents.approve', 'incidents.void')) {
                <p class="muted">{{ i18n.t('inc.awaitTriage') }}</p>
              }
            }
            @case ('Triaged') {
              @if (perms.can('incidents.approve')) {
                <form [formGroup]="investigatorForm" (ngSubmit)="facade.startInvestigation(n.id, investigatorForm.getRawValue())">
                  <label>{{ i18n.t('inc.investigator') }}</label>
                  <qams-user-select formControlName="investigatorId" />
                  <button type="submit" [disabled]="investigatorForm.invalid">{{ i18n.t('inc.startInvestigation') }}</button>
                </form>
              } @else { <p class="muted">{{ i18n.t('inc.awaitInvestigation') }}</p> }
            }
            @case ('UnderInvestigation') {
              @if (perms.can('incidents.edit')) {
                <form [formGroup]="factorForm" (ngSubmit)="addFactor(n.id)">
                  <label>{{ i18n.t('inc.factorCategory') }}</label>
                  <select formControlName="category">@for (c of factorCategories; track c) { <option [value]="c">{{ i18n.t('inc.factor.' + c) }}</option> }</select>
                  <label>{{ i18n.t('inc.factorDescription') }}</label>
                  <input formControlName="description" />
                  <button type="submit" [disabled]="factorForm.invalid">{{ i18n.t('inc.addFactor') }}</button>
                </form>
                <form [formGroup]="timelineForm" (ngSubmit)="addTimeline(n.id)">
                  <label>{{ i18n.t('inc.timelineWhen') }}</label>
                  <input type="datetime-local" formControlName="occurredAt" />
                  <label>{{ i18n.t('inc.timelineNote') }}</label>
                  <input formControlName="note" />
                  <button type="submit" [disabled]="timelineForm.invalid">{{ i18n.t('inc.addTimeline') }}</button>
                </form>
                <form [formGroup]="summaryForm" (ngSubmit)="facade.recordInvestigationSummary(n.id, summaryForm.getRawValue())">
                  <label>{{ i18n.t('inc.investigationSummary') }}</label>
                  <textarea rows="3" formControlName="summary"></textarea>
                  <button type="submit" [disabled]="summaryForm.invalid">{{ i18n.t('inc.recordSummary') }}</button>
                </form>
              }
              @if (perms.can('incidents.approve')) {
                <button (click)="facade.submitForReview(n.id)" [disabled]="!n.investigationSummary">{{ i18n.t('inc.submitReview') }}</button>
                @if (!n.investigationSummary) { <p class="muted">{{ i18n.t('inc.summaryFirst') }}</p> }
              }
              @if (!perms.canAny('incidents.edit', 'incidents.approve')) {
                <p class="muted">{{ i18n.t('inc.awaitInvestigation') }}</p>
              }
            }
            @case ('PendingReview') {
              @if (perms.can('incidents.sign')) {
                <p class="muted">{{ i18n.t('inc.closeSignHint') }}</p>
                <form [formGroup]="closeForm" (ngSubmit)="openClose()">
                  <label>{{ i18n.t('inc.closure') }}</label>
                  <textarea rows="2" formControlName="closureSummary"></textarea>
                  <button type="submit" [disabled]="closeForm.invalid">{{ i18n.t('inc.close') }}</button>
                </form>
              } @else { <p class="muted">{{ i18n.t('inc.awaitClosure') }}</p> }
            }
            @default { <p class="muted">{{ i18n.t('inc.terminal') }}</p> }
          }

          <!-- Corrective-action convergence: "one loop, many sources" (HQMS M03). -->
          <div class="capa-action">
            @if (n.correctiveActionNcId) {
              <p class="muted">{{ i18n.t('inc.capaRaised') }}</p>
              <a class="ghost-link" [routerLink]="['/nonconformances', n.correctiveActionNcId]">{{ i18n.t('inc.viewCapa') }} →</a>
            } @else if (n.status !== 'Rejected' && perms.can('nc.create')) {
              <p class="muted">{{ i18n.t('inc.capaHint') }}</p>
              <button (click)="facade.raiseCapa(n.id)">{{ i18n.t('inc.raiseCapa') }}</button>
            }
          </div>

          <!-- Sentinel declaration is available in any active state to a signer. -->
          @if (canDeclareSentinel(n.status) && !n.isSentinel && perms.can('incidents.sign')) {
            <div class="sentinel-action">
              <p class="muted">{{ i18n.t('inc.sentinelHint') }}</p>
              <button class="danger" (click)="sentinelOpen.set(true)">{{ i18n.t('inc.declareSentinel') }}</button>
            </div>
          }
        </section>
      </div>

      <qams-signature-manifest [signatures]="facade.signatures()" />

      <qams-audit-trail [subject]="n.id" />

      <qams-esign-dialog
        [open]="closeOpen()"
        [meaning]="closeMeaning()"
        [busy]="facade.loading()"
        [error]="facade.error()"
        (confirm)="signClose(n.id, $event)"
        (cancel)="closeOpen.set(false)" />

      <qams-esign-dialog
        [open]="sentinelOpen()"
        [meaning]="sentinelMeaning()"
        [busy]="facade.loading()"
        [error]="facade.error()"
        (confirm)="signSentinel(n.id, $event)"
        (cancel)="sentinelOpen.set(false)" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .grid { display: grid; grid-template-columns: 2fr 1fr; gap: 1rem; align-items: start; }
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .row-item { padding: .5rem 0; border-bottom: 1px solid var(--nt-border); }
    .actions form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    .actions button { margin-top: .5rem; margin-inline-end: .5rem; width: auto; }
    .capa-action, .sentinel-action { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    .banners { display: flex; gap: .6rem; flex-wrap: wrap; margin-bottom: .75rem; }
    .banner { padding: .4rem .8rem; border-radius: 6px; font-weight: 600; }
    .banner.sentinel { background: var(--nt-ink-crit); color: #fff; }
    .banner.anon { background: var(--nt-ink-neutral); color: #fff; }
    .danger-text { color: var(--nt-ink-crit); }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    h3 { margin-top: 1rem; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class IncidentsDetailComponent implements OnInit {
  readonly facade = inject(IncidentsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound incident id (provided via withComponentInputBinding). */
  readonly id = input.required<string>();

  /** Canonical workflow path for the stepper (off-path states render as terminal). */
  readonly flowSteps = ['Reported', 'Triaged', 'UnderInvestigation', 'PendingReview', 'Closed'] as const;

  readonly incident = this.facade.selected;
  readonly categories = INCIDENT_CATEGORIES;
  readonly factorCategories = CONTRIBUTING_FACTOR_CATEGORIES;

  readonly triageForm = this.fb.nonNullable.group({
    assigneeId: ['', [Validators.required]],
    category: ['Fall' as IncidentCategory, [Validators.required]],
  });
  readonly rejectForm = this.fb.nonNullable.group({ reason: ['', [Validators.required, Validators.maxLength(1000)]] });
  readonly investigatorForm = this.fb.nonNullable.group({ investigatorId: ['', [Validators.required]] });
  readonly factorForm = this.fb.nonNullable.group({
    category: ['Process' as ContributingFactorCategory, [Validators.required]],
    description: ['', [Validators.required, Validators.maxLength(2000)]],
  });
  readonly timelineForm = this.fb.nonNullable.group({
    occurredAt: ['', [Validators.required]],
    note: ['', [Validators.required, Validators.maxLength(2000)]],
  });
  readonly summaryForm = this.fb.nonNullable.group({
    summary: ['', [Validators.required, Validators.maxLength(8000)]],
  });
  readonly closeForm = this.fb.nonNullable.group({
    closureSummary: ['', [Validators.required, Validators.maxLength(8000)]],
  });

  /** Part 11 signing dialog state for closing the incident. */
  readonly closeOpen = signal(false);
  readonly closeMeaning = computed(() =>
    this.i18n.t('inc.signClose').replace('{ref}', this.incident()?.incidentRef ?? ''));

  /** Part 11 signing dialog state for declaring a sentinel event. */
  readonly sentinelOpen = signal(false);
  readonly sentinelMeaning = computed(() =>
    this.i18n.t('inc.signSentinel').replace('{ref}', this.incident()?.incidentRef ?? ''));

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
  }

  /** Sentinel may be declared while the incident is still active. */
  canDeclareSentinel(status: string): boolean {
    return status !== 'Closed' && status !== 'Rejected';
  }

  async addFactor(id: string): Promise<void> {
    if (this.factorForm.invalid) { return; }
    await this.facade.addContributingFactor(id, this.factorForm.getRawValue());
    if (this.facade.error() === '') { this.factorForm.reset({ category: 'Process', description: '' }); }
  }

  async addTimeline(id: string): Promise<void> {
    if (this.timelineForm.invalid) { return; }
    const raw = this.timelineForm.getRawValue();
    await this.facade.addTimelineEntry(id, { occurredAtUtc: new Date(raw.occurredAt).toISOString(), note: raw.note });
    if (this.facade.error() === '') { this.timelineForm.reset({ occurredAt: '', note: '' }); }
  }

  /** Opens the signing dialog for closure (the summary is captured in the form). */
  openClose(): void {
    if (this.closeForm.invalid) { return; }
    this.closeOpen.set(true);
  }

  async signClose(id: string, credentials: EsignCredentials): Promise<void> {
    await this.facade.close(id, {
      closureSummary: this.closeForm.getRawValue().closureSummary,
      password: credentials.password,
      pin: credentials.pin,
    });
    if (this.facade.error() === '') { this.closeOpen.set(false); }
  }

  async signSentinel(id: string, credentials: EsignCredentials): Promise<void> {
    await this.facade.declareSentinel(id, { password: credentials.password, pin: credentials.pin });
    if (this.facade.error() === '') { this.sentinelOpen.set(false); }
  }
}
