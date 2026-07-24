import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { PtFacade } from './pt.facade';
import { I18nService } from '../../core/i18n.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/**
 * Proficiency-testing register: enrollments with per-row result entry. The
 * z-score and performance grade come from the backend; an unsatisfactory
 * result auto-raises a nonconformance (PT→NC saga), noted in the banner.
 */
@Component({
  selector: 'qams-pt-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DecimalPipe, PageHeaderComponent, DrawerComponent, StatusPillComponent],
  template: `
    <qams-page-header [title]="i18n.t('pt.title')" [subtitle]="i18n.t('pt.sagaNote')">
      <select [value]="performanceFilter()" (change)="onFilter($event)" aria-label="Performance filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (p of performances; track p) { <option [value]="p">{{ p }}</option> }
      </select>
      <button (click)="showForm.set(!showForm())">{{ i18n.t('pt.new') }}</button>
    </qams-page-header>

    <qams-drawer [open]="showForm()" [title]="i18n.t('pt.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="enrollForm" (ngSubmit)="enroll()">
        <div class="grid">
          <div><label>{{ i18n.t('pt.scheme') }}</label><input formControlName="scheme" [placeholder]="i18n.t('pt.schemeHint')" /></div>
          <div><label>{{ i18n.t('qc.analyte') }}</label><input formControlName="analyte" /></div>
          <div><label>{{ i18n.t('pt.cycle') }}</label><input formControlName="cycle" [placeholder]="i18n.t('pt.cycleHint')" /></div>
        </div>
        <div class="row">
          <button type="submit" [disabled]="enrollForm.invalid || facade.loading()">{{ i18n.t('pt.enroll') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('pt.empty') }}</p>
    } @else {
      @if (facade.error() && !showForm()) { <div class="error">{{ facade.error() }}</div> }
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('pt.ref') }}</th><th>{{ i18n.t('pt.scheme') }}</th><th>{{ i18n.t('qc.analyte') }}</th>
            <th>{{ i18n.t('pt.cycle') }}</th><th>{{ i18n.t('pt.submitted') }}</th><th>z</th>
            <th>{{ i18n.t('pt.performance') }}</th><th></th>
          </tr></thead>
          <tbody>
            @for (e of facade.list(); track e.id) {
              <tr>
                <td class="code">{{ e.ptRef }}</td><td>{{ e.scheme }}</td><td>{{ e.analyte }}</td><td>{{ e.cycle }}</td>
                <td>{{ e.submittedValue !== null ? (e.submittedValue | number:'1.0-4') : '—' }}</td>
                <td [class.bad]="e.performance === 'Unsatisfactory'">{{ e.zScore !== null ? (e.zScore | number:'1.2-2') : '—' }}</td>
                <td><qams-status-pill [status]="e.performance" /></td>
                <td>
                  @if (e.performance === 'Pending') {
                    <button class="link" type="button" (click)="resultId.set(resultId() === e.id ? '' : e.id)">{{ i18n.t('pt.recordResult') }}</button>
                  }
                </td>
              </tr>
              @if (resultId() === e.id) {
                <tr><td colspan="8">
                  <form class="inline" [formGroup]="resultForm" (ngSubmit)="recordResult(e.id)">
                    <input type="number" step="any" formControlName="submitted" [placeholder]="i18n.t('pt.submitted')" />
                    <input type="number" step="any" formControlName="assigned" [placeholder]="i18n.t('pt.assigned')" />
                    <input type="number" step="any" formControlName="standardDeviation" [placeholder]="i18n.t('pt.sd')" />
                    <button type="submit" [disabled]="resultForm.invalid">{{ i18n.t('pt.save') }}</button>
                    <button type="button" class="secondary" (click)="resultId.set('')">{{ i18n.t('nc.cancel') }}</button>
                  </form>
                </td></tr>
              }
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: [`
    .form { margin-bottom: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: .5rem 1rem; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .bad { color: var(--nt-red); font-weight: 700; }
    .inline { display: flex; gap: 8px; align-items: center; padding: 4px 0; }
    .inline input { max-width: 160px; }
    button, select { width: auto; }
  `],
})
export class PtListComponent implements OnInit {
  readonly facade = inject(PtFacade);
  readonly i18n = inject(I18nService);
  private readonly fb = inject(FormBuilder);

  readonly performances = ['Pending', 'Satisfactory', 'Questionable', 'Unsatisfactory'];
  readonly showForm = signal(false);
  readonly performanceFilter = signal('');
  /** Enrollment id whose result form is open ('' = none). */
  readonly resultId = signal('');

  readonly enrollForm = this.fb.nonNullable.group({
    scheme: ['', [Validators.required, Validators.maxLength(200)]],
    analyte: ['', [Validators.required, Validators.maxLength(200)]],
    cycle: ['', [Validators.required, Validators.maxLength(100)]],
  });
  readonly resultForm = this.fb.nonNullable.group({
    submitted: [0, [Validators.required]],
    assigned: [0, [Validators.required]],
    standardDeviation: [1, [Validators.required, Validators.min(0.000001)]],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.performanceFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.performanceFilter() || undefined);
  }

  async enroll(): Promise<void> {
    if (this.enrollForm.invalid) { return; }
    await this.facade.enroll(this.enrollForm.getRawValue(), this.performanceFilter() || undefined);
    this.cancel();
  }

  async recordResult(id: string): Promise<void> {
    if (this.resultForm.invalid) { return; }
    await this.facade.recordResult(id, this.resultForm.getRawValue(), this.performanceFilter() || undefined);
    this.resultForm.reset({ submitted: 0, assigned: 0, standardDeviation: 1 });
    this.resultId.set('');
  }

  cancel(): void {
    this.showForm.set(false);
    this.enrollForm.reset();
  }
}
