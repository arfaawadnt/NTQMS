import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { EsignCredentials, EsignDialogComponent } from '../../shared/ui/esign-dialog.component';
import { SignatureManifestComponent } from '../../shared/ui/signature-manifest.component';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { InstrumentComparabilityFacade } from './instrument-comparability.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Instrument-comparability workspace: readings keyed by (instrument, sample).
 * The backend compares every non-reference instrument to the reference on the
 * samples they share, deriving a mean percentage bias and a comparable verdict
 * against the allowable limit.
 */
@Component({
    selector: 'qams-instrument-comparability-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, EsignDialogComponent, SignatureManifestComponent],
    template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.studyRef + ' — ' + s.analyte" [subtitle]="i18n.t('icp.reference') + ': ' + s.referenceInstrument">
        <a routerLink="/instrument-comparabilities" class="ghost-link">← {{ i18n.t('icp.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.state" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.state" /></div>
        <div><span class="muted">{{ i18n.t('icp.readings') }}</span> {{ s.readings.length }}</div>
        <div><span class="muted">{{ i18n.t('icp.allowable') }}</span> {{ s.allowableBiasPct }}%</div>
        @if (s.nonComparableCount !== null) { <div><span class="muted">{{ i18n.t('icp.nonComparable') }}</span> <b>{{ s.nonComparableCount }}</b></div> }
        @if (s.signedOffAtUtc) { <div><span class="muted">{{ i18n.t('val.signedOff') }}</span> {{ s.signedOffAtUtc | date:'medium' }}</div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      @if (s.results.length > 0) {
        <section class="card">
          <h3>{{ i18n.t('icp.results') }}</h3>
          <table>
            <thead><tr>
              <th>{{ i18n.t('icp.instrument') }}</th><th>{{ i18n.t('icp.pairedSamples') }}</th>
              <th>{{ i18n.t('icp.bias') }}</th><th>{{ i18n.t('val.verdict') }}</th>
            </tr></thead>
            <tbody>
              @for (r of s.results; track r.instrument) {
                <tr>
                  <td><b>{{ r.instrument }}</b></td>
                  <td>{{ r.pairedSamples }}</td>
                  <td [class.neg]="r.meanBiasPct < 0">{{ r.meanBiasPct | number:'1.1-2' }}%</td>
                  <td>
                    @if (r.comparable) { <qams-status-pill status="Pass" />&nbsp;{{ i18n.t('icp.comparable') }} }
                    @else { <qams-status-pill status="Failed" />&nbsp;{{ i18n.t('icp.notComparable') }} }
                  </td>
                </tr>
              }
            </tbody>
          </table>
          <p class="muted small">{{ s.referenceInstrument }} — {{ i18n.t('icp.reference') }}</p>
        </section>
      }

      <section class="card">
        <h3>{{ i18n.t('icp.readings') }} ({{ s.readings.length }})</h3>
        @if (s.state !== 'SignedOff') {
          <form [formGroup]="entryForm" (ngSubmit)="add(s.id)">
            <div class="triple">
              <div><label>{{ i18n.t('icp.instrument') }}</label><input formControlName="instrument" [placeholder]="s.referenceInstrument" /></div>
              <div><label>{{ i18n.t('icp.sampleId') }}</label><input formControlName="sampleId" /></div>
              <div><label>{{ i18n.t('out.value') }} ({{ s.unit }})</label><input type="number" step="any" formControlName="value" /></div>
            </div>
            <div class="hint">{{ i18n.t('icp.readingHint') }}</div>
            <button type="submit" [disabled]="entryForm.invalid">{{ i18n.t('icp.addReading') }}</button>
          </form>
        }
        @if (s.readings.length > 0) {
          <table class="mtable">
            <thead><tr><th>{{ i18n.t('icp.instrument') }}</th><th>{{ i18n.t('icp.sampleId') }}</th><th>{{ i18n.t('out.value') }}</th><th></th></tr></thead>
            <tbody>
              @for (r of s.readings; track r.id) {
                <tr>
                  <td><span class="tag" [class.ref]="r.instrument === s.referenceInstrument">{{ r.instrument }}</span></td>
                  <td class="muted">{{ r.sampleId }}</td>
                  <td>{{ r.value | number:'1.0-4' }}</td>
                  <td>
                    @if (s.state !== 'SignedOff') {
                      <button class="link danger-link" type="button" (click)="facade.removeReading(s.id, r.id)">✕</button>
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
            <button (click)="facade.calculate(s.id)" [disabled]="!canCalculate()">{{ i18n.t('icp.calculate') }}</button>
            @if (!canCalculate()) { <span class="muted">{{ i18n.t('icp.minReadings') }}</span> }
          }
          @if (s.state === 'Calculated' && perms.can('analytical-quality.sign')) {
            <button (click)="esignOpen.set(true)">{{ i18n.t('mc.signOff') }}</button>
            <qams-esign-dialog [open]="esignOpen()" [meaning]="i18n.t('esign.aqMeaning')" [busy]="facade.loading()" [error]="facade.error()" (confirm)="doSignOff(s.id, $event)" (cancel)="esignOpen.set(false)" />
          }
          @if (s.state === 'SignedOff') { <p class="muted">{{ i18n.t('mc.signedOffNote') }}</p> }
        </div>
      </section>

      <qams-signature-manifest [subjectUrl]="'/api/instrument-comparabilities/' + s.id + '/signatures'" />

      <qams-audit-trail [subject]="s.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    section { margin-bottom: 1rem; }
    .small { font-size: .75rem; margin-top: 6px; }
    .triple { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 1rem; }
    .mtable { margin-top: 1rem; }
    .neg { color: var(--nt-red); }
    .tag { font-size: .72rem; padding: 2px 8px; border-radius: 10px; background: var(--nt-filter-grey); }
    .tag.ref { background: color-mix(in srgb, var(--nt-teal) 20%, transparent); color: var(--nt-navy-deep); }
    .danger-link { color: var(--nt-red); }
    .actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    .actions button { width: auto; }
    form { border-bottom: 1px solid var(--nt-border); padding-bottom: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 800px) { .triple { grid-template-columns: 1fr; } }
  `]
})
export class InstrumentComparabilityDetailComponent implements OnInit {
  readonly facade = inject(InstrumentComparabilityFacade);
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
    instrument: ['', [Validators.required, Validators.maxLength(120)]],
    sampleId: ['', [Validators.required, Validators.maxLength(80)]],
    value: [null as number | null, [Validators.required]],
  });

  /** Comparability needs the reference plus at least one other instrument. */
  readonly canCalculate = computed(() => {
    const s = this.item();
    if (!s) { return false; }
    const instruments = new Set(s.readings.map((r) => r.instrument));
    return instruments.has(s.referenceInstrument) && instruments.size >= 2;
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  async add(id: string): Promise<void> {
    if (this.entryForm.invalid) { return; }
    const raw = this.entryForm.getRawValue();
    await this.facade.addReading(id, raw.instrument.trim(), raw.sampleId.trim(), raw.value!);
    // Keep the instrument so its sample series enters quickly.
    this.entryForm.patchValue({ sampleId: '', value: null });
  }
}
