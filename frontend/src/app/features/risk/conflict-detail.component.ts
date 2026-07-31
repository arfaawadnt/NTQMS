import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { GovernanceApiService } from '../../core/api/governance-api.service';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { OrgDataService } from '../../core/org-data.service';
import { CONFLICT_OUTCOMES, CONFLICT_RISK_LEVELS, ConflictDetail } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Conflict workspace: the declaration, the QM impartiality-risk assessment
 * (SoD: never the declarant), and the closure outcome. High-risk assessments
 * notify per the COI_HIGH rule on the backend.
 */
@Component({
    selector: 'qams-conflict-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
    template: `
    @if (item(); as c) {
      <qams-page-header [title]="c.conflictRef + ' — ' + c.relatedParty" [subtitle]="(org.userName(c.declarantId) || '') + ' · ' + (c.declaredOn | date:'mediumDate')">
        <a routerLink="/conflicts" class="ghost-link">← {{ i18n.t('coi.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="c.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="c.status" /></div>
        @if (c.riskLevel) {
          <div><span class="muted">{{ i18n.t('coi.risk') }}</span> <b [class]="'risk ' + c.riskLevel.toLowerCase()">{{ c.riskLevel }}</b></div>
        }
        @if (c.assessedBy) { <div><span class="muted">{{ i18n.t('coi.assessedBy') }}</span> {{ org.userName(c.assessedBy) || '—' }}</div> }
        @if (c.outcome) { <div><span class="muted">{{ i18n.t('val.verdict') }}</span> {{ c.outcome }}</div> }
      </div>
      @if (error()) { <div class="error">{{ error() }}</div> }

      <section class="card"><h3>{{ i18n.t('nc.description') }}</h3><p>{{ c.description }}</p></section>
      @if (c.mitigation) { <section class="card"><h3>{{ i18n.t('coi.mitigation') }}</h3><p>{{ c.mitigation }}</p></section> }
      @if (c.closureNote) { <section class="card"><h3>{{ i18n.t('obj.note') }}</h3><p>{{ c.closureNote }}</p></section> }

      @if (perms.canAny('conflicts.approve', 'conflicts.void')) {
        <section class="card">
          <h3>{{ i18n.t('val.workflow') }}</h3>
          @if (c.status === 'Declared') {
            <form [formGroup]="assessForm" (ngSubmit)="assess(c.id)">
              <div class="pair">
                <div>
                  <label>{{ i18n.t('coi.risk') }}</label>
                  <select formControlName="riskLevel">
                    @for (l of riskLevels; track l) { <option [value]="l">{{ l }}</option> }
                  </select>
                </div>
                <div><label>{{ i18n.t('coi.mitigation') }}</label><input formControlName="mitigation" [placeholder]="i18n.t('coi.mitigationHint')" /></div>
              </div>
              <div class="hint">{{ i18n.t('coi.sodNote') }}</div>
              <button type="submit" [disabled]="assessForm.invalid">{{ i18n.t('coi.assess') }}</button>
            </form>
          }
          @if (c.status === 'Assessed') {
            <form [formGroup]="closeForm" (ngSubmit)="close(c.id)">
              <div class="pair">
                <div>
                  <label>{{ i18n.t('val.verdict') }}</label>
                  <select formControlName="outcome">
                    @for (o of outcomes; track o) { <option [value]="o">{{ o }}</option> }
                  </select>
                </div>
                <div><label>{{ i18n.t('obj.note') }}</label><input formControlName="closureNote" /></div>
              </div>
              <button type="submit" class="secondary" [disabled]="closeForm.invalid">{{ i18n.t('coi.close') }}</button>
            </form>
          }
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
    section { margin-bottom: 1rem; }
    .pair { display: grid; grid-template-columns: 1fr 2fr; gap: 1rem; }
    .risk.low { color: var(--nt-green); }
    .risk.medium { color: var(--nt-orange, #ef6c00); }
    .risk.high { color: var(--nt-red); }
    form { margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 700px) { .pair { grid-template-columns: 1fr; } }
  `]
})
export class ConflictDetailComponent implements OnInit {
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  readonly org = inject(OrgDataService);
  private readonly api = inject(GovernanceApiService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound conflict id. */
  readonly id = input.required<string>();

  /** Canonical workflow path for the stepper. */
  readonly flowSteps = ['Declared', 'Assessed', 'Closed'] as const;

  readonly riskLevels = CONFLICT_RISK_LEVELS;
  readonly outcomes = CONFLICT_OUTCOMES;
  readonly item = signal<ConflictDetail | null>(null);
  readonly error = signal('');

  readonly assessForm = this.fb.nonNullable.group({
    riskLevel: ['Low', [Validators.required]],
    mitigation: ['', [Validators.required, Validators.maxLength(2000)]],
  });
  readonly closeForm = this.fb.nonNullable.group({
    outcome: ['Mitigated', [Validators.required]],
    closureNote: ['', [Validators.required, Validators.maxLength(2000)]],
  });

  ngOnInit(): void {
    void this.load();
    void this.org.ensureDirectory();
  }

  private async load(): Promise<void> {
    try {
      this.item.set(await firstValueFrom(this.api.getConflict(this.id())));
    } catch (err) {
      this.error.set(this.describe(err));
    }
  }

  async assess(id: string): Promise<void> {
    if (this.assessForm.invalid) { return; }
    const raw = this.assessForm.getRawValue();
    await this.call(() => firstValueFrom(this.api.assessConflict(id, raw.riskLevel, raw.mitigation)));
  }

  async close(id: string): Promise<void> {
    if (this.closeForm.invalid) { return; }
    const raw = this.closeForm.getRawValue();
    await this.call(() => firstValueFrom(this.api.closeConflict(id, raw.outcome, raw.closureNote)));
  }

  private async call(action: () => Promise<void>): Promise<void> {
    this.error.set('');
    try {
      await action();
      await this.load();
    } catch (err) {
      this.error.set(this.describe(err));
    }
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return (err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`;
    }
    return 'Unexpected error.';
  }
}
