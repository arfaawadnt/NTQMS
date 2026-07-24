import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { QcFacade } from './qc.facade';
import { I18nService } from '../../core/i18n.service';
import { AuthService } from '../../core/auth.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { LeveyJenningsChartComponent } from './levey-jennings-chart.component';

/**
 * QC workspace for one control profile: the Levey-Jennings chart, run entry,
 * and the run log with Westgard verdicts. Out-of-control runs demand a
 * troubleshooting note (the backend refuses notes on in-control runs).
 */
@Component({
  selector: 'qams-qc-profile-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, LeveyJenningsChartComponent],
  template: `
    @if (facade.selected(); as p) {
      <qams-page-header [title]="p.analyte + ' — ' + p.instrument" [subtitle]="i18n.t('qc.lot') + ': ' + p.controlLot + ' · μ=' + p.targetMean + ' σ=' + p.targetSd">
        <a routerLink="/qc" class="ghost-link">← {{ i18n.t('qc.backToList') }}</a>
      </qams-page-header>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <section class="card">
        <h3>{{ i18n.t('qc.chart') }}</h3>
        @if (facade.chartRuns().length === 0) {
          <p class="muted">{{ i18n.t('qc.noRuns') }}</p>
        } @else {
          <qams-levey-jennings [runs]="facade.chartRuns()" [mean]="p.targetMean" [sd]="p.targetSd" />
        }
        <form [formGroup]="runForm" (ngSubmit)="record(p.id)">
          <div class="pair">
            <div><label>{{ i18n.t('qc.value') }}</label><input type="number" step="any" formControlName="value" /></div>
            <div><label>{{ i18n.t('qc.operator') }}</label><input formControlName="operator" /></div>
          </div>
          <button type="submit" [disabled]="runForm.invalid || facade.loading()">{{ i18n.t('qc.record') }}</button>
        </form>
      </section>

      <section class="card">
        <h3>{{ i18n.t('qc.runLog') }}</h3>
        @if (facade.runs().length === 0) { <p class="muted">{{ i18n.t('qc.noRuns') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('qc.measuredAt') }}</th><th>{{ i18n.t('qc.value') }}</th><th>z</th>
              <th>{{ i18n.t('qc.outcome') }}</th><th>{{ i18n.t('qc.rules') }}</th><th>{{ i18n.t('qc.operator') }}</th><th></th>
            </tr></thead>
            <tbody>
              @for (r of facade.runs(); track r.id) {
                <tr>
                  <td>{{ r.measuredAtUtc | date:'short' }}</td>
                  <td>{{ r.value | number:'1.0-4' }}</td>
                  <td [class.bad]="r.outcome === 'OutOfControl'">{{ r.zScore | number:'1.2-2' }}</td>
                  <td><qams-status-pill [status]="qcStatus(r.outcome)" /></td>
                  <td class="code">{{ r.violatedRules || '—' }}</td>
                  <td>{{ r.operator }}</td>
                  <td>
                    @if (r.outcome === 'OutOfControl' && !r.troubleshootingNote) {
                      <button class="link" type="button" (click)="troubleshootId.set(r.id)">{{ i18n.t('qc.troubleshoot') }}</button>
                    } @else if (r.troubleshootingNote) {
                      <span class="muted" [title]="r.troubleshootingNote">✓ {{ i18n.t('qc.resolved') }}</span>
                    }
                  </td>
                </tr>
                @if (troubleshootId() === r.id) {
                  <tr><td colspan="7">
                    <form class="ts" [formGroup]="tsForm" (ngSubmit)="troubleshoot(p.id, r.id)">
                      <input formControlName="note" [placeholder]="i18n.t('qc.tsHint')" />
                      <button type="submit" [disabled]="tsForm.invalid">{{ i18n.t('qc.saveNote') }}</button>
                      <button type="button" class="secondary" (click)="troubleshootId.set('')">{{ i18n.t('nc.cancel') }}</button>
                    </form>
                  </td></tr>
                }
              }
            </tbody>
          </table>
        }
      </section>
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
  styles: [`
    section { margin-bottom: 1rem; }
    .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .bad { color: var(--nt-red); font-weight: 700; }
    .ts { display: flex; gap: 8px; align-items: center; padding: 4px 0; border: none; margin: 0; }
    .ts input { flex: 1; }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
  `],
})
export class QcProfileDetailComponent implements OnInit {
  readonly facade = inject(QcFacade);
  readonly i18n = inject(I18nService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound QC profile id. */
  readonly id = input.required<string>();

  /** Run id whose troubleshooting form is open ('' = none). */
  readonly troubleshootId = signal('');

  readonly runForm = this.fb.nonNullable.group({
    value: [0, [Validators.required]],
    operator: ['', [Validators.required, Validators.maxLength(200)]],
  });
  readonly tsForm = this.fb.nonNullable.group({
    note: ['', [Validators.required, Validators.maxLength(2000)]],
  });

  ngOnInit(): void {
    this.runForm.patchValue({ operator: this.auth.displayName() });
    void this.facade.openProfile(this.id());
  }

  /** Maps the Westgard outcome onto pill tones (warning stays amber by default). */
  qcStatus(outcome: string): string {
    return outcome === 'InControl' ? 'Active' : outcome === 'OutOfControl' ? 'Failed' : outcome;
  }

  async record(profileId: string): Promise<void> {
    if (this.runForm.invalid) { return; }
    const { value, operator } = this.runForm.getRawValue();
    await this.facade.recordRun(profileId, value, operator);
    this.runForm.patchValue({ value: 0 });
  }

  async troubleshoot(profileId: string, runId: string): Promise<void> {
    if (this.tsForm.invalid) { return; }
    await this.facade.troubleshoot(profileId, runId, this.tsForm.getRawValue().note);
    this.tsForm.reset();
    this.troubleshootId.set('');
  }
}
