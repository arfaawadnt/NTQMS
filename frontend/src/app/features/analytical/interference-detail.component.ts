import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { InterferenceFacade } from './interference.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { INTERFERENCE_KINDS } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Interference workspace (CLSI EP07): control replicates set the unbiased
 * baseline; each named interferent's test replicates yield a percentage bias.
 * The backend flags an interferent as significant when |bias| exceeds the
 * allowable limit.
 */
@Component({
  selector: 'qams-interference-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
  template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.studyRef + ' — ' + s.analyte" [subtitle]="i18n.t('inf.allowable') + ': ' + s.allowableBiasPct + '%'">
        <a routerLink="/interference-studies" class="ghost-link">← {{ i18n.t('inf.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.state" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.state" /></div>
        <div><span class="muted">{{ i18n.t('inf.measurements') }}</span> {{ s.measurements.length }}</div>
        @if (s.controlMean !== null) { <div><span class="muted">{{ i18n.t('inf.controlMean') }}</span> <b>{{ s.controlMean | number:'1.0-4' }} {{ s.unit }}</b></div> }
        @if (s.significantCount !== null) { <div><span class="muted">{{ i18n.t('inf.significant') }}</span> <b>{{ s.significantCount }}</b></div> }
        @if (s.signedOffAtUtc) { <div><span class="muted">{{ i18n.t('val.signedOff') }}</span> {{ s.signedOffAtUtc | date:'medium' }}</div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      @if (s.results.length > 0) {
        <section class="card">
          <h3>{{ i18n.t('inf.results') }}</h3>
          <table>
            <thead><tr>
              <th>{{ i18n.t('inf.interferent') }}</th><th>{{ i18n.t('inf.replicates') }}</th>
              <th>{{ i18n.t('inf.meanTest') }}</th><th>{{ i18n.t('inf.bias') }}</th><th>{{ i18n.t('val.verdict') }}</th>
            </tr></thead>
            <tbody>
              @for (r of s.results; track r.interferent) {
                <tr>
                  <td><b>{{ r.interferent }}</b></td>
                  <td>{{ r.replicateCount }}</td>
                  <td>{{ r.meanTest | number:'1.0-3' }} {{ s.unit }}</td>
                  <td [class.neg]="r.biasPct < 0">{{ r.biasPct | number:'1.1-2' }}%</td>
                  <td>
                    @if (r.significantInterference) { <qams-status-pill status="Failed" />&nbsp;{{ i18n.t('inf.significantFlag') }} }
                    @else { <qams-status-pill status="Pass" />&nbsp;{{ i18n.t('inf.notSignificant') }} }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </section>
      }

      <section class="card">
        <h3>{{ i18n.t('inf.measurements') }} ({{ s.measurements.length }})</h3>
        @if (s.state !== 'SignedOff') {
          <form [formGroup]="entryForm" (ngSubmit)="add(s.id)">
            <div class="triple">
              <div><label>{{ i18n.t('inf.kind') }}</label>
                <select formControlName="kind">
                  @for (k of kinds; track k) { <option [value]="k">{{ k === 'Control' ? i18n.t('inf.control') : i18n.t('inf.test') }}</option> }
                </select>
              </div>
              <div><label>{{ i18n.t('inf.interferent') }}</label>
                <input formControlName="interferent" [placeholder]="i18n.t('inf.interferentHint')" [attr.disabled]="entryForm.value.kind === 'Control' ? true : null" />
              </div>
              <div><label>{{ i18n.t('out.value') }} ({{ s.unit }})</label><input type="number" step="any" formControlName="value" /></div>
            </div>
            <button type="submit" [disabled]="entryForm.invalid">{{ i18n.t('inf.addMeasurement') }}</button>
          </form>
        }
        @if (s.measurements.length > 0) {
          <table class="mtable">
            <thead><tr><th>{{ i18n.t('inf.kind') }}</th><th>{{ i18n.t('inf.interferent') }}</th><th>{{ i18n.t('out.value') }}</th><th></th></tr></thead>
            <tbody>
              @for (m of s.measurements; track m.id) {
                <tr>
                  <td><span class="tag" [class.control]="m.isControl">{{ m.isControl ? i18n.t('inf.control') : i18n.t('inf.test') }}</span></td>
                  <td class="muted">{{ m.interferent || '—' }}</td>
                  <td>{{ m.value | number:'1.0-4' }}</td>
                  <td>
                    @if (s.state !== 'SignedOff') {
                      <button class="link danger-link" type="button" (click)="facade.removeMeasurement(s.id, m.id)">✕</button>
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
            <button (click)="facade.calculate(s.id)" [disabled]="!canCalculate()">{{ i18n.t('inf.calculate') }}</button>
            @if (!canCalculate()) { <span class="muted">{{ i18n.t('inf.minControl') }}</span> }
          }
          @if (s.state === 'Calculated' && perms.canApprove()) {
            <button (click)="facade.signOff(s.id)">{{ i18n.t('mc.signOff') }}</button>
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
    section { margin-bottom: 1rem; }
    .triple { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 1rem; }
    .mtable { margin-top: 1rem; }
    .neg { color: var(--nt-red); }
    .tag { font-size: .72rem; padding: 2px 8px; border-radius: 10px; background: color-mix(in srgb, var(--nt-blue) 16%, transparent); color: var(--nt-navy-deep); }
    .tag.control { background: var(--nt-filter-grey); color: var(--nt-grey-m); }
    .danger-link { color: var(--nt-red); }
    .actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    .actions button { width: auto; }
    form { border-bottom: 1px solid var(--nt-border); padding-bottom: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 800px) { .triple { grid-template-columns: 1fr; } }
  `],
})
export class InterferenceDetailComponent implements OnInit {
  readonly facade = inject(InterferenceFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  readonly id = input.required<string>();

  readonly flowSteps = ['DataEntry', 'Calculated', 'SignedOff'] as const;
  readonly kinds = INTERFERENCE_KINDS;
  readonly item = this.facade.selected;

  readonly entryForm = this.fb.nonNullable.group({
    kind: ['Control' as string, [Validators.required]],
    interferent: ['', [Validators.maxLength(120)]],
    value: [null as number | null, [Validators.required]],
  });

  /** EP07 needs at least 3 control replicates and one interferent test set. */
  readonly canCalculate = computed(() => {
    const ms = this.item()?.measurements ?? [];
    return ms.filter((m) => m.isControl).length >= 3 && ms.some((m) => !m.isControl);
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  async add(id: string): Promise<void> {
    if (this.entryForm.invalid) { return; }
    const raw = this.entryForm.getRawValue();
    const interferent = raw.kind === 'Control' ? null : (raw.interferent.trim() || null);
    await this.facade.addMeasurement(id, raw.kind, interferent, raw.value!);
    // Keep kind and interferent so replicates enter quickly.
    this.entryForm.patchValue({ value: null });
  }
}
