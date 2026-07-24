import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EquipmentFacade } from './equipment.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Equipment workspace: calibration status + logs (with certificate upload +
 * download), maintenance log, and retire. Logging a calibration returns the item
 * to service; the out-of-service/lockout status comes from the backend sweep.
 */
@Component({
  selector: 'qams-equipment-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
  template: `
    @if (item(); as e) {
      <qams-page-header [title]="e.code + ' — ' + e.name" [subtitle]="e.serialNumber">
        <a routerLink="/equipment" class="ghost-link">← {{ i18n.t('equip.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="e.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="e.status" /></div>
        <div><span class="muted">{{ i18n.t('equip.lastCal') }}</span> {{ e.lastCalibrationAt ? (e.lastCalibrationAt | date:'mediumDate') : '—' }}</div>
        <div><span class="muted">{{ i18n.t('equip.nextDue') }}</span> {{ e.nextCalibrationDue ? (e.nextCalibrationDue | date:'mediumDate') : '—' }}</div>
        <div><span class="muted">{{ i18n.t('equip.location') }}</span> {{ e.location ?? '—' }}</div>
        @if (e.status !== 'Retired' && perms.canApprove()) {
          <button class="secondary" (click)="facade.retire(e.id)">{{ i18n.t('equip.retire') }}</button>
        }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <div class="grid">
        <section class="card">
          <h3>{{ i18n.t('equip.calibrations') }}</h3>
          @if (e.calibrations.length === 0) { <p class="muted">—</p> }
          @for (c of e.calibrations; track c.id) {
            <div class="row-item">
              <div>{{ c.performedAt | date:'mediumDate' }} — {{ c.provider }} — <b>{{ c.result }}</b></div>
              @if (c.certificateFileId) { <a [href]="facade.downloadUrl(c.certificateFileId)" target="_blank" rel="noopener">{{ i18n.t('doc.download') }}</a> }
            </div>
          }
          @if (e.status !== 'Retired') {
            <form [formGroup]="calForm" (ngSubmit)="logCalibration(e.id)">
              <label>{{ i18n.t('equip.performedAt') }}</label><input type="date" formControlName="performedAt" />
              <label>{{ i18n.t('equip.provider') }}</label><input formControlName="provider" />
              <label>{{ i18n.t('equip.result') }}</label><input formControlName="result" [placeholder]="i18n.t('equip.resultHint')" />
              <label>{{ i18n.t('equip.certificate') }}</label><input type="file" (change)="onCert($event)" />
              <button type="submit" [disabled]="calForm.invalid">{{ i18n.t('equip.logCal') }}</button>
            </form>
          }
        </section>

        <section class="card">
          <h3>{{ i18n.t('equip.maintenance') }}</h3>
          @if (e.maintenance.length === 0) { <p class="muted">—</p> }
          @for (m of e.maintenance; track m.id) {
            <div class="row-item">{{ m.performedAt | date:'mediumDate' }} — {{ m.workDescription }}</div>
          }
          @if (e.status !== 'Retired') {
            <form [formGroup]="maintForm" (ngSubmit)="logMaintenance(e.id)">
              <label>{{ i18n.t('equip.performedAt') }}</label><input type="date" formControlName="performedAt" />
              <label>{{ i18n.t('equip.work') }}</label><input formControlName="workDescription" />
              <button type="submit" [disabled]="maintForm.invalid">{{ i18n.t('equip.logMaint') }}</button>
            </form>
          }
        </section>
      </div>
    
      <qams-audit-trail [subject]="e.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
  styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .meta button { width: auto; margin-inline-start: auto; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; align-items: start; }
    .row-item { padding: .5rem 0; border-bottom: 1px solid var(--nt-border); display: flex; justify-content: space-between; gap: 1rem; }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `],
})
export class EquipmentDetailComponent implements OnInit {
  readonly facade = inject(EquipmentFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound equipment id. */
  readonly id = input.required<string>();

  /** Canonical workflow path for the stepper (off-path states render as terminal). */
  readonly flowSteps = ['NeedsCalibration', 'Active', 'Retired'] as const;

  readonly item = this.facade.selected;
  readonly certificate = signal<File | null>(null);

  readonly calForm = this.fb.nonNullable.group({
    performedAt: ['', [Validators.required]],
    provider: ['', [Validators.maxLength(200)]],
    result: ['', [Validators.required, Validators.maxLength(500)]],
  });
  readonly maintForm = this.fb.nonNullable.group({
    performedAt: ['', [Validators.required]],
    workDescription: ['', [Validators.required, Validators.maxLength(2000)]],
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  onCert(event: Event): void { this.certificate.set((event.target as HTMLInputElement).files?.[0] ?? null); }

  async logCalibration(id: string): Promise<void> {
    if (this.calForm.invalid) { return; }
    const { performedAt, provider, result } = this.calForm.getRawValue();
    await this.facade.logCalibration(id, performedAt, provider, result, this.certificate());
    this.certificate.set(null);
    this.calForm.reset();
  }

  async logMaintenance(id: string): Promise<void> {
    if (this.maintForm.invalid) { return; }
    await this.facade.logMaintenance(id, this.maintForm.getRawValue());
    this.maintForm.reset();
  }
}
