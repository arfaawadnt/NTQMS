import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { EsignCredentials, EsignDialogComponent } from '../../shared/ui/esign-dialog.component';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { UncertaintyFacade } from './uncertainty.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { UNCERTAINTY_COMPONENT_TYPES, UncertaintyComponentType } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * MU budget workspace (GUM): component table with Type A/B rows and their
 * relative standard uncertainties, u_c = √Σu_i² and U = k·u_c computed
 * server-side on Calculate, target verdict, and the QM approval freeze.
 */
@Component({
    selector: 'qams-uncertainty-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, EsignDialogComponent],
    template: `
    @if (item(); as b) {
      <qams-page-header [title]="b.budgetRef + ' — ' + b.analyte" [subtitle]="b.method + ' · ' + b.level">
        <a routerLink="/uncertainty" class="ghost-link">← {{ i18n.t('mu.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="b.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="b.status" /></div>
        <div><span class="muted">u&#8320; (%)</span> {{ b.combinedStandardUncertainty !== null ? (b.combinedStandardUncertainty | number:'1.2-4') : '—' }}</div>
        <div>
          <span class="muted">U = k·u&#8320; (k={{ b.coverageFactor }})</span>
          <b [class.bad]="b.meetsTarget === false" [class.good]="b.meetsTarget === true">
            {{ b.expandedUncertainty !== null ? (b.expandedUncertainty | number:'1.2-4') + ' %' : '—' }}
          </b>
        </div>
        @if (b.targetExpandedUncertainty !== null) {
          <div><span class="muted">{{ i18n.t('mu.target') }}</span> ≤ {{ b.targetExpandedUncertainty | number:'1.2-4' }} %</div>
        }
        @if (b.approvedAtUtc) { <div><span class="muted">{{ i18n.t('val.signedOff') }}</span> {{ b.approvedAtUtc | date:'medium' }}</div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <section class="card">
        <h3>{{ i18n.t('mu.components') }} ({{ b.components.length }})</h3>
        @if (b.components.length === 0) { <p class="muted">{{ i18n.t('mu.noComponents') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('mu.component') }}</th><th>{{ i18n.t('mu.type') }}</th>
              <th>u&#7522; (%)</th><th>{{ i18n.t('mu.source') }}</th><th></th>
            </tr></thead>
            <tbody>
              @for (c of b.components; track c.id) {
                <tr>
                  <td>{{ c.name }}</td>
                  <td>{{ c.type === 'TypeA' ? 'A' : 'B' }}</td>
                  <td>{{ c.relativeStandardUncertainty | number:'1.2-4' }}</td>
                  <td class="muted">{{ c.source ?? '—' }}</td>
                  <td>
                    @if (editable()) {
                      <button class="link danger-link" type="button" (click)="facade.removeComponent(b.id, c.id)">✕</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
        @if (editable() && perms.can('analytical-quality.edit')) {
          <form [formGroup]="componentForm" (ngSubmit)="addComponent(b.id)">
            <div class="quad">
              <div><label>{{ i18n.t('mu.component') }}</label><input formControlName="name" [placeholder]="i18n.t('mu.componentHint')" /></div>
              <div>
                <label>{{ i18n.t('mu.type') }}</label>
                <select formControlName="type">
                  @for (t of types; track t) { <option [value]="t">{{ t === 'TypeA' ? 'A (statistical)' : 'B (other)' }}</option> }
                </select>
              </div>
              <div><label>u&#7522; (%)</label><input type="number" min="0" step="any" formControlName="relativeStandardUncertainty" /></div>
              <div><label>{{ i18n.t('mu.source') }}</label><input formControlName="source" [placeholder]="i18n.t('mu.sourceHint')" /></div>
            </div>
            <button type="submit" [disabled]="componentForm.invalid">{{ i18n.t('mu.addComponent') }}</button>
          </form>
        }
      </section>

      <section class="card">
        <h3>{{ i18n.t('val.workflow') }}</h3>
        <div class="actions">
          @if (editable() && perms.can('analytical-quality.edit')) {
            <button (click)="facade.calculate(b.id)" [disabled]="b.components.length === 0">{{ i18n.t('mu.calculate') }}</button>
            @if (b.components.length === 0) { <span class="muted">{{ i18n.t('mu.noComponents') }}</span> }
          }
          @if (b.status === 'Calculated' && perms.can('analytical-quality.approve')) {
            <button (click)="esignOpen.set(true)">{{ i18n.t('mu.approve') }}</button>
            <qams-esign-dialog [open]="esignOpen()" [meaning]="i18n.t('esign.aqMeaning')" [busy]="facade.loading()" [error]="facade.error()" (confirm)="doApprove(b.id, $event)" (cancel)="esignOpen.set(false)" />
          }
          @if (b.status === 'Approved') {
            <p class="muted">{{ i18n.t('mu.approvedNote') }}</p>
          }
        </div>
      </section>

      <qams-audit-trail [subject]="b.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .good { color: var(--nt-green); }
    .bad { color: var(--nt-red); }
    section { margin-bottom: 1rem; }
    .quad { display: grid; grid-template-columns: 2fr 1fr 1fr 2fr; gap: 1rem; }
    .actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    .actions button { width: auto; }
    .danger-link { color: var(--nt-red); }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 900px) { .quad { grid-template-columns: 1fr 1fr; } }
  `]
})
export class UncertaintyDetailComponent implements OnInit {
  readonly facade = inject(UncertaintyFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound budget id. */
  readonly id = input.required<string>();

  /** Whether the Part 11 e-signature dialog is open for the approval. */
  readonly esignOpen = signal(false);

  /** Approves through the ceremony dialog; closes on success, stays open (showing the error) on failure. */
  async doApprove(id: string, credentials: EsignCredentials): Promise<void> {
    await this.facade.approve(id, credentials);
    if (this.facade.error() === '') { this.esignOpen.set(false); }
  }

  /** Canonical workflow path for the stepper. */
  readonly flowSteps = ['Draft', 'Calculated', 'Approved'] as const;

  readonly types = UNCERTAINTY_COMPONENT_TYPES;
  readonly item = this.facade.selected;

  /** Components and recalculation stay open until approval freezes the budget. */
  readonly editable = computed(() => this.item()?.status !== 'Approved');

  readonly componentForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(300)]],
    type: ['TypeA' as UncertaintyComponentType, [Validators.required]],
    relativeStandardUncertainty: [0, [Validators.required, Validators.min(0)]],
    source: [''],
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  async addComponent(id: string): Promise<void> {
    if (this.componentForm.invalid) { return; }
    const raw = this.componentForm.getRawValue();
    await this.facade.addComponent(id, { ...raw, source: raw.source.trim() || null });
    this.componentForm.reset({ type: 'TypeA', relativeStandardUncertainty: 0 });
  }
}
