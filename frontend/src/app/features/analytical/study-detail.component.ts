import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { EsignCredentials, EsignDialogComponent } from '../../shared/ui/esign-dialog.component';
import { SignatureManifestComponent } from '../../shared/ui/signature-manifest.component';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ValidationFacade } from './validation.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Method-validation workspace mirroring the backend state machine:
 * enter replicates while configured/entering → calculate bias & CV against the
 * total allowable error → QM sign-off freezes the study.
 */
@Component({
    selector: 'qams-study-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, EsignDialogComponent, SignatureManifestComponent],
    template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.studyRef + ' — ' + s.analyte" [subtitle]="s.protocol + ' · TEa ' + s.totalAllowableError + '%'">
        <a routerLink="/validation-studies" class="ghost-link">← {{ i18n.t('val.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.state" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.state" /></div>
        @if (s.meanBias !== null) { <div><span class="muted">{{ i18n.t('val.bias') }}</span> {{ s.meanBias | number:'1.2-4' }}%</div> }
        @if (s.cv !== null) { <div><span class="muted">CV</span> {{ s.cv | number:'1.2-4' }}%</div> }
        @if (s.passed !== null) {
          <div><span class="muted">{{ i18n.t('val.verdict') }}</span>
            <qams-status-pill [status]="s.passed ? 'Satisfactory' : 'Failed'" />
          </div>
        }
        @if (s.signedOffBy) { <div><span class="muted">{{ i18n.t('val.signedOff') }}</span> {{ s.signedOffAtUtc | date:'medium' }}</div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <section class="card">
        <h3>{{ i18n.t('val.replicates') }} ({{ s.replicates.length }})</h3>
        @if (s.replicates.length === 0) { <p class="muted">—</p> }
        @else {
          <table>
            <thead><tr><th>{{ i18n.t('val.level') }}</th><th>{{ i18n.t('val.measured') }}</th><th>{{ i18n.t('val.reference') }}</th></tr></thead>
            <tbody>
              @for (r of s.replicates; track r.id) {
                <tr><td>{{ r.level }}</td><td>{{ r.measured | number:'1.0-4' }}</td><td>{{ r.reference !== null ? (r.reference | number:'1.0-4') : '—' }}</td></tr>
              }
            </tbody>
          </table>
        }
        @if (editable()) {
          <form [formGroup]="repForm" (ngSubmit)="enterReplicate(s.id)">
            <div class="trio">
              <div><label>{{ i18n.t('val.level') }}</label><input formControlName="level" [placeholder]="i18n.t('val.levelHint')" /></div>
              <div><label>{{ i18n.t('val.measured') }}</label><input type="number" step="any" formControlName="measured" /></div>
              <div><label>{{ i18n.t('val.reference') }}</label><input type="number" step="any" formControlName="reference" [placeholder]="i18n.t('common.optional')" /></div>
            </div>
            <button type="submit" [disabled]="repForm.invalid">{{ i18n.t('val.addReplicate') }}</button>
          </form>
        }
      </section>

      <section class="card">
        <h3>{{ i18n.t('val.workflow') }}</h3>
        <div class="actions">
          @if (editable()) {
            <button (click)="facade.calculate(s.id)" [disabled]="s.replicates.length < 2">{{ i18n.t('val.calculate') }}</button>
            @if (s.replicates.length < 2) { <span class="muted">{{ i18n.t('val.needReplicates') }}</span> }
          }
          @if (s.state === 'StatsCalculated' && perms.can('analytical-quality.sign')) {
            <button (click)="esignOpen.set(true)">{{ i18n.t('val.signOffAction') }}</button>
            <qams-esign-dialog [open]="esignOpen()" [meaning]="i18n.t('esign.aqMeaning')" [busy]="facade.loading()" [error]="facade.error()" (confirm)="doSignOff(s.id, $event)" (cancel)="esignOpen.set(false)" />
          }
          @if (s.state === 'SignedOff') {
            <p class="muted">{{ i18n.t('val.signedOffNote') }}</p>
          }
        </div>
      </section>
    
      <qams-signature-manifest [subjectUrl]="'/api/validation-studies/' + s.id + '/signatures'" />

      <qams-audit-trail [subject]="s.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    section { margin-bottom: 1rem; }
    .trio { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 1rem; }
    .actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    .actions button { width: auto; }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
  `]
})
export class StudyDetailComponent implements OnInit {
  readonly facade = inject(ValidationFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound study id. */
  readonly id = input.required<string>();

  /** Whether the Part 11 e-signature dialog is open for the sign-off. */
  readonly esignOpen = signal(false);

  /** Signs off through the ceremony dialog; closes on success, stays open (showing the error) on failure. */
  async doSignOff(id: string, credentials: EsignCredentials): Promise<void> {
    await this.facade.signOff(id, credentials);
    if (this.facade.error() === '') { this.esignOpen.set(false); }
  }

  /** Canonical workflow path for the stepper (off-path states render as terminal). */
  readonly flowSteps = ['ProtocolConfigured', 'DataEntered', 'StatsCalculated', 'SignedOff'] as const;

  readonly item = this.facade.selected;

  /** Replicates and recalculation are allowed until the study is signed off. */
  readonly editable = computed(() => {
    const state = this.item()?.state;
    return state === 'ProtocolConfigured' || state === 'DataEntered' || state === 'StatsCalculated';
  });

  readonly repForm = this.fb.nonNullable.group({
    level: ['', [Validators.required, Validators.maxLength(100)]],
    measured: [0, [Validators.required]],
    reference: [null as number | null],
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  async enterReplicate(id: string): Promise<void> {
    if (this.repForm.invalid) { return; }
    const raw = this.repForm.getRawValue();
    await this.facade.enterReplicate(id, raw);
    this.repForm.patchValue({ measured: 0, reference: null });
  }
}
