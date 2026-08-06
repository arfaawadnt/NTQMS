import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuditsFacade } from './audits.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import {
  CHECKLIST_VERDICTS, ChecklistVerdict, FINDING_GRADES, FindingGrade,
} from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';
import { EsignCredentials, EsignDialogComponent } from '../../shared/ui/esign-dialog.component';

/**
 * Audit workspace: start the audit, run the checklist (answer each item),
 * raise findings (NC-graded findings auto-open a Nonconformance server-side,
 * shown as a linked NC id), and sign off once every item is answered and every
 * NC finding is acknowledged. Sign-off is approver-gated.
 */
@Component({
    selector: 'qams-audit-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, EsignDialogComponent],
    template: `
    @if (audit(); as a) {
      <qams-page-header [title]="a.auditRef + ' — ' + a.title" [subtitle]="a.type">
        <a routerLink="/audits" class="ghost-link">← {{ i18n.t('audit.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="a.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="a.status" /></div>
        <div><span class="muted">{{ i18n.t('audit.plannedDate') }}</span> {{ a.plannedDate | date:'mediumDate' }}</div>
        @if (a.status === 'Scheduled') {
          <button (click)="facade.start(a.id)">{{ i18n.t('audit.start') }}</button>
        }
        @if (a.status === 'InProgress' && perms.can('audits.sign')) {
          <button (click)="esignOpen.set(true)" [disabled]="!canSignOff()">{{ i18n.t('audit.signOff') }}</button>
        }
        <qams-esign-dialog [open]="esignOpen()" [meaning]="i18n.t('esign.signMeaning')" [busy]="facade.loading()" [error]="facade.error()" (confirm)="doSignOff(a.id, $event)" (cancel)="esignOpen.set(false)" />
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      @if (a.status === 'InProgress' && !canSignOff()) { <p class="muted">{{ i18n.t('audit.signOffBlocked') }}</p> }

      <div class="grid">
        <section class="card">
          <h3>{{ i18n.t('audit.checklist') }}</h3>
          <table>
            <thead><tr>
              <th>{{ i18n.t('audit.clause') }}</th><th>{{ i18n.t('audit.question') }}</th>
              <th>{{ i18n.t('audit.verdict') }}</th>
            </tr></thead>
            <tbody>
              @for (item of a.checklist; track item.id) {
                <tr>
                  <td>{{ item.isoClause }}</td>
                  <td>{{ item.question }}</td>
                  <td>
                    @if (a.status === 'InProgress') {
                      <select [value]="item.verdict === 'Unanswered' ? '' : item.verdict"
                              (change)="answer(a.id, item.id, $event)" aria-label="Verdict">
                        <option value="" disabled>{{ i18n.t('audit.choose') }}</option>
                        @for (v of verdicts; track v) { <option [value]="v">{{ v }}</option> }
                      </select>
                    } @else { <qams-status-pill [status]="item.verdict" /> }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </section>

        <section class="card">
          <h3>{{ i18n.t('audit.findings') }}</h3>
          @if (a.findings.length === 0) { <p class="muted">—</p> }
          @for (f of a.findings; track f.id) {
            <div class="finding">
              <div><qams-status-pill [status]="f.grade" /> {{ f.description }}</div>
              @if (f.ncId) { <div class="muted">→ {{ i18n.t('audit.linkedNc') }}: {{ f.ncId }}</div> }
            </div>
          }

          @if (a.status === 'InProgress') {
            <form [formGroup]="findingForm" (ngSubmit)="raiseFinding(a.id)">
              <label>{{ i18n.t('audit.grade') }}</label>
              <select formControlName="grade">@for (g of grades; track g) { <option [value]="g">{{ g }}</option> }</select>
              <label>{{ i18n.t('audit.findingDesc') }}</label>
              <textarea rows="2" formControlName="description"></textarea>
              <button type="submit" [disabled]="findingForm.invalid">{{ i18n.t('audit.raiseFinding') }}</button>
              <p class="muted hint">{{ i18n.t('audit.ncHint') }}</p>
            </form>
          }
        </section>
      </div>
    
      <qams-audit-trail [subject]="a.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .meta button { width: auto; margin-inline-start: auto; }
    .grid { display: grid; grid-template-columns: 3fr 2fr; gap: 1rem; align-items: start; }
    .finding { padding: .5rem 0; border-bottom: 1px solid var(--nt-border); }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .hint { font-size: .8rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class AuditDetailComponent implements OnInit {
  readonly facade = inject(AuditsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound audit id. */
  readonly id = input.required<string>();

  /** Whether the Part 11 e-signature dialog is open for the sign-off. */
  readonly esignOpen = signal(false);

  /** Signs off through the ceremony dialog; closes on success, stays open (showing the error) on failure. */
  async doSignOff(id: string, credentials: EsignCredentials): Promise<void> {
    await this.facade.signOff(id, credentials);
    if (this.facade.error() === '') { this.esignOpen.set(false); }
  }

  /** Canonical workflow path for the stepper (off-path states render as terminal). */
  readonly flowSteps = ['Scheduled', 'InProgress', 'SignedOff'] as const;

  readonly audit = this.facade.selected;
  readonly verdicts = CHECKLIST_VERDICTS;
  readonly grades = FINDING_GRADES;

  /** Sign-off is allowed once every item is answered and every NC-graded finding has its NC. */
  readonly canSignOff = computed(() => {
    const a = this.audit();
    if (!a) { return false; }
    const allAnswered = a.checklist.every((i) => i.verdict !== 'Unanswered');
    const ncFindingsLinked = a.findings.filter((f) => f.grade !== 'Ofi').every((f) => f.ncId !== null);
    return allAnswered && ncFindingsLinked;
  });

  readonly findingForm = this.fb.nonNullable.group({
    grade: ['MinorNc' as FindingGrade, [Validators.required]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  answer(id: string, itemId: string, event: Event): void {
    const verdict = (event.target as HTMLSelectElement).value as ChecklistVerdict;
    if (verdict) {
      void this.facade.answer(id, itemId, { verdict, evidence: null });
    }
  }

  async raiseFinding(id: string): Promise<void> {
    if (this.findingForm.invalid) { return; }
    await this.facade.raiseFinding(id, this.findingForm.getRawValue());
    this.findingForm.reset({ grade: 'MinorNc', description: '' });
  }
}
