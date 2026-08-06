import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { EsignCredentials, EsignDialogComponent } from '../../shared/ui/esign-dialog.component';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LotComparisonFacade } from './lot-comparison.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Lot-to-lot workspace: paired current/new lot readings. The backend derives
 * mean bias% = (meanNew − meanCurrent) / meanCurrent × 100 and the accept/reject
 * verdict against the allowable limit.
 */
@Component({
    selector: 'qams-lot-comparison-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, EsignDialogComponent],
    template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.studyRef + ' — ' + s.analyte" [subtitle]="s.currentLot + ' → ' + s.newLot">
        <a routerLink="/lot-comparisons" class="ghost-link">← {{ i18n.t('lot.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.state" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.state" /></div>
        <div><span class="muted">{{ i18n.t('lot.pairs') }}</span> {{ s.pairs.length }}</div>
        <div><span class="muted">{{ i18n.t('lot.allowable') }}</span> {{ s.allowableBiasPct }}%</div>
        @if (s.meanBiasPct !== null) {
          <div><span class="muted">{{ i18n.t('lot.bias') }}</span> <b>{{ s.meanBiasPct | number:'1.1-2' }}%</b></div>
          <div><span class="muted">{{ i18n.t('val.verdict') }}</span>
            @if (s.passes) { <qams-status-pill status="Pass" /> } @else { <qams-status-pill status="Failed" /> }
          </div>
        }
        @if (s.signedOffAtUtc) { <div><span class="muted">{{ i18n.t('val.signedOff') }}</span> {{ s.signedOffAtUtc | date:'medium' }}</div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      @if (s.meanBiasPct !== null) {
        <div class="components">
          <div class="comp card"><span class="muted">{{ i18n.t('lot.meanCurrent') }}</span><b>{{ s.meanCurrent | number:'1.0-3' }}</b><span class="sub">{{ s.currentLot }} · {{ s.unit }}</span></div>
          <div class="comp card"><span class="muted">{{ i18n.t('lot.meanNew') }}</span><b>{{ s.meanNew | number:'1.0-3' }}</b><span class="sub">{{ s.newLot }} · {{ s.unit }}</span></div>
          <div class="comp card highlight"><span class="muted">{{ i18n.t('lot.bias') }}</span><b>{{ s.meanBiasPct | number:'1.1-2' }}%</b><span class="sub">{{ i18n.t('lot.allowable') }} {{ s.allowableBiasPct }}%</span></div>
        </div>
      }

      <section class="card">
        <h3>{{ i18n.t('lot.pairs') }} ({{ s.pairs.length }})</h3>
        @if (s.state !== 'SignedOff') {
          <form [formGroup]="entryForm" (ngSubmit)="add(s.id)">
            <div class="triple">
              <div><label>{{ i18n.t('lot.currentValue') }}</label><input type="number" step="any" formControlName="currentLotValue" /></div>
              <div><label>{{ i18n.t('lot.newValue') }}</label><input type="number" step="any" formControlName="newLotValue" /></div>
              <div><label>{{ i18n.t('lot.sampleId') }}</label><input formControlName="sampleId" [placeholder]="i18n.t('common.optional')" /></div>
            </div>
            <button type="submit" [disabled]="entryForm.invalid">{{ i18n.t('lot.addPair') }}</button>
          </form>
        }
        @if (s.pairs.length > 0) {
          <table class="mtable">
            <thead><tr><th>{{ i18n.t('lot.sampleId') }}</th><th>{{ s.currentLot }}</th><th>{{ s.newLot }}</th><th></th></tr></thead>
            <tbody>
              @for (p of s.pairs; track p.id) {
                <tr>
                  <td class="muted">{{ p.sampleId || '—' }}</td>
                  <td>{{ p.currentLotValue | number:'1.0-4' }}</td>
                  <td>{{ p.newLotValue | number:'1.0-4' }}</td>
                  <td>
                    @if (s.state !== 'SignedOff') {
                      <button class="link danger-link" type="button" (click)="facade.removePair(s.id, p.id)">✕</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>

      <section class="card">
        <h3>{{ i18n.t('val.workflow') }}</h3>
        <div class="actions">
          @if (s.state !== 'SignedOff') {
            <button (click)="facade.calculate(s.id)" [disabled]="s.pairs.length < 3">{{ i18n.t('lot.calculate') }}</button>
            @if (s.pairs.length < 3) { <span class="muted">{{ i18n.t('lot.minPairs') }}</span> }
          }
          @if (s.state === 'Calculated' && perms.can('analytical-quality.sign')) {
            <button (click)="esignOpen.set(true)">{{ i18n.t('mc.signOff') }}</button>
            <qams-esign-dialog [open]="esignOpen()" [meaning]="i18n.t('esign.aqMeaning')" [busy]="facade.loading()" [error]="facade.error()" (confirm)="doSignOff(s.id, $event)" (cancel)="esignOpen.set(false)" />
          }
          @if (s.state === 'SignedOff') { <p class="muted">{{ i18n.t('mc.signedOffNote') }}</p> }
        </div>
      </section>

      <qams-audit-trail [subject]="s.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .components { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem; margin-bottom: 1rem; }
    .comp { display: flex; flex-direction: column; gap: 2px; padding: 14px 16px; }
    .comp b { font-size: 1.3rem; color: var(--nt-navy-deep); }
    .comp.highlight { border-inline-start: 4px solid var(--nt-blue); }
    .comp .sub { font-size: .72rem; color: var(--nt-grey-m); }
    section { margin-bottom: 1rem; }
    .triple { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 1rem; }
    .mtable { margin-top: 1rem; }
    .danger-link { color: var(--nt-red); }
    .actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    .actions button { width: auto; }
    form { border-bottom: 1px solid var(--nt-border); padding-bottom: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 800px) { .components, .triple { grid-template-columns: 1fr; } }
  `]
})
export class LotComparisonDetailComponent implements OnInit {
  readonly facade = inject(LotComparisonFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  readonly id = input.required<string>();

  /** Whether the Part 11 e-signature dialog is open for the sign-off. */
  readonly esignOpen = signal(false);

  /** Signs off through the ceremony dialog; closes on success, stays open (showing the error) on failure. */
  async doSignOff(id: string, credentials: EsignCredentials): Promise<void> {
    await this.facade.signOff(id, credentials);
    if (this.facade.error() === '') { this.esignOpen.set(false); }
  }

  readonly flowSteps = ['DataEntry', 'Calculated', 'SignedOff'] as const;
  readonly item = this.facade.selected;

  readonly entryForm = this.fb.nonNullable.group({
    currentLotValue: [null as number | null, [Validators.required]],
    newLotValue: [null as number | null, [Validators.required]],
    sampleId: ['', [Validators.maxLength(80)]],
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  async add(id: string): Promise<void> {
    if (this.entryForm.invalid) { return; }
    const raw = this.entryForm.getRawValue();
    await this.facade.addPair(id, raw.currentLotValue!, raw.newLotValue!, raw.sampleId.trim() || null);
    this.entryForm.reset({ currentLotValue: null, newLotValue: null, sampleId: '' });
  }
}
