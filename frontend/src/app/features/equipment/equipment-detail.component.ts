import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { EquipmentFacade } from './equipment.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { OrgDataService } from '../../core/org-data.service';
import { ReferenceStandardsApiService } from '../../core/api/reference-standards-api.service';
import {
  DOWNTIME_CATEGORIES, DowntimeCategory, ReferenceStandardListItem, SAFETY_NOTICE_SEVERITIES,
  SAFETY_NOTICE_TYPES, SafetyNoticeSeverity, SafetyNoticeType,
} from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';
import { LovSelectComponent } from '../../shared/ui/lov-select.component';

/**
 * Equipment workspace: calibration status + logs (with certificate upload +
 * download), maintenance log, and retire. Logging a calibration returns the item
 * to service; the out-of-service/lockout status comes from the backend sweep.
 */
@Component({
    selector: 'qams-equipment-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, LovSelectComponent],
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
        <div><span class="muted">{{ i18n.t('equip.availability') }}</span> <b [class.danger-text]="e.availabilityPercent30d < 90">{{ e.availabilityPercent30d }}%</b></div>
        @if (e.status !== 'Retired' && perms.can('equipment.void')) {
          <button class="secondary" (click)="facade.retire(e.id)">{{ i18n.t('equip.retire') }}</button>
        }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <!-- The three logs are tabs: each is a growing history, and stacking them
           made the page scroll past one log to reach another. -->
      <div class="tabs" role="tablist">
        <button role="tab" [attr.aria-selected]="tab() === 'calibration'"
                [class.on]="tab() === 'calibration'" (click)="tab.set('calibration')">
          {{ i18n.t('equip.calibrations') }} <span class="cnt">{{ e.calibrations.length }}</span>
        </button>
        <button role="tab" [attr.aria-selected]="tab() === 'maintenance'"
                [class.on]="tab() === 'maintenance'" (click)="tab.set('maintenance')">
          {{ i18n.t('equip.maintenance') }} <span class="cnt">{{ e.maintenance.length }}</span>
        </button>
        <button role="tab" [attr.aria-selected]="tab() === 'checks'"
                [class.on]="tab() === 'checks'" (click)="tab.set('checks')">
          {{ i18n.t('equip.checks') }} <span class="cnt">{{ e.intermediateChecks.length }}</span>
        </button>
        <button role="tab" [attr.aria-selected]="tab() === 'downtime'"
                [class.on]="tab() === 'downtime'" (click)="tab.set('downtime')">
          {{ i18n.t('equip.downtime') }} <span class="cnt">{{ e.downtime.length }}</span>
        </button>
        <button role="tab" [attr.aria-selected]="tab() === 'safety'"
                [class.on]="tab() === 'safety'" (click)="tab.set('safety')">
          {{ i18n.t('equip.safetyNotices') }} <span class="cnt">{{ e.safetyNotices.length }}</span>
        </button>
      </div>

      @if (tab() === 'calibration') {
        <section class="card">
          <h3>{{ i18n.t('equip.calibrations') }}</h3>
          @if (e.calibrations.length === 0) { <p class="muted">—</p> }
          @else {
            <table>
              <thead><tr>
                <th>{{ i18n.t('equip.performedAt') }}</th><th>{{ i18n.t('equip.provider') }}</th>
                <th>{{ i18n.t('equip.result') }}</th><th>{{ i18n.t('equip.certificate') }}</th>
              </tr></thead>
              <tbody>
                @for (c of e.calibrations; track c.id) {
                  <tr>
                    <td>{{ c.performedAt | date:'mediumDate' }}</td>
                    <td>{{ c.provider || '—' }}</td>
                    <td><b>{{ c.result }}</b></td>
                    <td>
                      @if (c.certificateFileId) {
                        <button type="button" class="link" (click)="facade.downloadCertificate(c.certificateFileId!, e.code + '-calibration')">{{ i18n.t('doc.download') }}</button>
                      } @else { <span class="muted">—</span> }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
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
      }

      @if (tab() === 'maintenance') {
        <section class="card">
          <h3>{{ i18n.t('equip.maintenance') }}</h3>
          @if (e.maintenance.length === 0) { <p class="muted">—</p> }
          @else {
            <table>
              <thead><tr>
                <th>{{ i18n.t('equip.performedAt') }}</th><th>{{ i18n.t('equip.work') }}</th>
                <th>{{ i18n.t('equip.certificate') }}</th>
              </tr></thead>
              <tbody>
                @for (m of e.maintenance; track m.id) {
                  <tr>
                    <td>{{ m.performedAt | date:'mediumDate' }}</td>
                    <td>{{ m.workDescription }}</td>
                    <td>
                      @if (m.certificateFileId) {
                        <button type="button" class="link" (click)="facade.downloadCertificate(m.certificateFileId!, e.code + '-maintenance')">{{ i18n.t('doc.download') }}</button>
                      } @else { <span class="muted">—</span> }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
          @if (e.status !== 'Retired') {
            <form [formGroup]="maintForm" (ngSubmit)="logMaintenance(e.id)">
              <label>{{ i18n.t('equip.performedAt') }}</label><input type="date" formControlName="performedAt" />
              <label>{{ i18n.t('equip.work') }}</label><input formControlName="workDescription" />
              <label>{{ i18n.t('equip.maintCertificate') }}</label><input type="file" (change)="onMaintCert($event)" />
              <button type="submit" [disabled]="maintForm.invalid">{{ i18n.t('equip.logMaint') }}</button>
            </form>
          }
        </section>
      }

      @if (tab() === 'checks') {
      <section class="card checks">
        <h3>{{ i18n.t('equip.checks') }} <span class="muted">(ISO 17025 §6.4.10)</span></h3>
        @if (e.intermediateChecks.length === 0) { <p class="muted">{{ i18n.t('equip.noChecks') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('equip.performedAt') }}</th><th>{{ i18n.t('equip.checkType') }}</th>
              <th>{{ i18n.t('val.verdict') }}</th><th>{{ i18n.t('equip.standardUsed') }}</th>
              <th>{{ i18n.t('equip.performedBy') }}</th><th>{{ i18n.t('equip.remarks') }}</th>
            </tr></thead>
            <tbody>
              @for (c of e.intermediateChecks; track c.id) {
                <tr>
                  <td>{{ c.performedOn | date:'mediumDate' }}</td>
                  <td>{{ c.checkType }}</td>
                  <td>
                    @if (c.passed) { <qams-status-pill status="Pass" /> }
                    @else { <qams-status-pill status="Failed" /> }
                  </td>
                  <td>{{ standardName(c.referenceStandardId) }}</td>
                  <td>{{ org.userName(c.performedById) || '—' }}</td>
                  <td class="muted">{{ c.remarks ?? '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
        @if (e.status !== 'Retired') {
          <form [formGroup]="checkForm" (ngSubmit)="recordCheck(e.id)">
            <div class="check-grid">
              <div><label>{{ i18n.t('equip.performedAt') }}</label><input type="date" formControlName="performedOn" /></div>
              <div>
                <label>{{ i18n.t('equip.checkType') }}</label>
                <qams-lov-select formControlName="checkType" category="INTERMEDIATE_CHECK_TYPE" [placeholder]="i18n.t('equip.checkTypeHint')" />
              </div>
              <div>
                <label>{{ i18n.t('equip.standardUsed') }}</label>
                <select formControlName="referenceStandardId">
                  <option [value]="''">{{ i18n.t('common.optional') }}</option>
                  @for (s of activeStandards(); track s.id) {
                    <option [value]="s.id">{{ s.standardRef }} — {{ s.name }}</option>
                  }
                </select>
              </div>
              <div>
                <label>{{ i18n.t('val.verdict') }}</label>
                <select formControlName="passed">
                  <option [value]="'true'">{{ i18n.t('equip.pass') }}</option>
                  <option [value]="'false'">{{ i18n.t('equip.fail') }}</option>
                </select>
              </div>
              <div class="wide"><label>{{ i18n.t('equip.remarks') }}</label><input formControlName="remarks" /></div>
            </div>
            <div class="hint">{{ i18n.t('equip.failNote') }}</div>
            <button type="submit" [disabled]="checkForm.invalid">{{ i18n.t('equip.recordCheck') }}</button>
          </form>
        }
      </section>
      }

      @if (tab() === 'downtime') {
        <section class="card">
          <h3>{{ i18n.t('equip.downtime') }}</h3>
          @if (e.downtime.length === 0) { <p class="muted">{{ i18n.t('equip.noDowntime') }}</p> }
          @else {
            <table>
              <thead><tr>
                <th>{{ i18n.t('equip.started') }}</th><th>{{ i18n.t('equip.ended') }}</th><th>{{ i18n.t('equip.category') }}</th>
                <th>{{ i18n.t('equip.reason') }}</th><th>{{ i18n.t('equip.hours') }}</th><th></th>
              </tr></thead>
              <tbody>
                @for (d of e.downtime; track d.id) {
                  <tr [class.open]="d.isOpen">
                    <td>{{ d.startedAtUtc | date:'short' }}</td>
                    <td>{{ d.endedAtUtc ? (d.endedAtUtc | date:'short') : i18n.t('equip.ongoing') }}</td>
                    <td>{{ i18n.t('equip.dc.' + d.category) }}</td>
                    <td>{{ d.reason }}</td>
                    <td>{{ d.durationHours }}</td>
                    <td>@if (d.isOpen && e.status !== 'Retired') { <button type="button" class="link" (click)="endDowntime(e.id, d.id)">{{ i18n.t('equip.endDowntime') }}</button> }</td>
                  </tr>
                }
              </tbody>
            </table>
          }
          @if (e.status !== 'Retired') {
            <form [formGroup]="downtimeForm" (ngSubmit)="startDowntime(e.id)">
              <label>{{ i18n.t('equip.started') }}</label><input type="datetime-local" formControlName="startedAt" />
              <label>{{ i18n.t('equip.category') }}</label>
              <select formControlName="category">@for (c of downtimeCategories; track c) { <option [value]="c">{{ i18n.t('equip.dc.' + c) }}</option> }</select>
              <label>{{ i18n.t('equip.reason') }}</label><input formControlName="reason" />
              <button type="submit" [disabled]="downtimeForm.invalid">{{ i18n.t('equip.startDowntime') }}</button>
            </form>
          }
        </section>
      }

      @if (tab() === 'safety') {
        <section class="card">
          <h3>{{ i18n.t('equip.safetyNotices') }}</h3>
          @if (e.safetyNotices.length === 0) { <p class="muted">{{ i18n.t('equip.noNotices') }}</p> }
          @else {
            <table>
              <thead><tr>
                <th>{{ i18n.t('equip.noticeType') }}</th><th>{{ i18n.t('equip.reference') }}</th><th>{{ i18n.t('equip.issuer') }}</th>
                <th>{{ i18n.t('equip.severity') }}</th><th>{{ i18n.t('equip.actionBy') }}</th><th>{{ i18n.t('nc.status') }}</th><th></th>
              </tr></thead>
              <tbody>
                @for (n of e.safetyNotices; track n.id) {
                  <tr>
                    <td>{{ i18n.t('equip.nt.' + n.type) }}</td>
                    <td class="code">{{ n.reference }}</td>
                    <td>{{ n.issuer }}</td>
                    <td><span class="sev" [class]="'sev ' + n.severity.toLowerCase()">{{ i18n.t('equip.sev.' + n.severity) }}</span></td>
                    <td [class.danger-text]="n.isOverdue">{{ n.requiredActionBy ? (n.requiredActionBy | date:'mediumDate') : '—' }}@if (n.isOverdue) { <b> · {{ i18n.t('equip.overdue') }}</b> }</td>
                    <td><qams-status-pill [status]="n.status" /></td>
                    <td>
                      @if (e.status !== 'Retired') {
                        @if (n.status === 'Open') { <button type="button" class="link" (click)="startAction(n.id)">{{ i18n.t('equip.action') }}</button> }
                        @else if (n.status === 'Actioned') { <button type="button" class="link" (click)="facade.closeSafetyNotice(e.id, n.id)">{{ i18n.t('equip.closeNotice') }}</button> }
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
          @if (actioningNotice()) {
            <form [formGroup]="actionForm" (ngSubmit)="actionNotice(e.id)">
              <label>{{ i18n.t('equip.actionNote') }}</label><input formControlName="note" />
              <label>{{ i18n.t('equip.actionedOn') }}</label><input type="date" formControlName="on" />
              <button type="submit" [disabled]="actionForm.invalid">{{ i18n.t('equip.recordAction') }}</button>
              <button type="button" class="secondary" (click)="actioningNotice.set(null)">{{ i18n.t('common.cancel') }}</button>
            </form>
          }
          @if (e.status !== 'Retired') {
            <form [formGroup]="noticeForm" (ngSubmit)="logNotice(e.id)">
              <h4>{{ i18n.t('equip.logNotice') }}</h4>
              <label>{{ i18n.t('equip.noticeType') }}</label>
              <select formControlName="type">@for (t of noticeTypes; track t) { <option [value]="t">{{ i18n.t('equip.nt.' + t) }}</option> }</select>
              <label>{{ i18n.t('equip.reference') }}</label><input formControlName="reference" />
              <label>{{ i18n.t('equip.issuer') }}</label><input formControlName="issuer" />
              <label>{{ i18n.t('equip.severity') }}</label>
              <select formControlName="severity">@for (s of severities; track s) { <option [value]="s">{{ i18n.t('equip.sev.' + s) }}</option> }</select>
              <label>{{ i18n.t('equip.receivedOn') }}</label><input type="date" formControlName="receivedOn" />
              <label>{{ i18n.t('equip.actionBy') }}</label><input type="date" formControlName="requiredActionBy" />
              <button type="submit" [disabled]="noticeForm.invalid">{{ i18n.t('equip.logNoticeBtn') }}</button>
            </form>
          }
        </section>
      }

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
    .checks { margin-top: 0; }
    .tabs { display: flex; gap: 4px; margin-bottom: 1rem; border-bottom: 1px solid var(--nt-border); }
    .tabs button { width: auto; background: none; color: var(--nt-grey-d); border: none;
                   border-bottom: 3px solid transparent; border-radius: 0; padding: 9px 16px;
                   font-weight: 700; font-size: 13px; }
    .tabs button.on { color: var(--nt-blue); border-bottom-color: var(--nt-blue); }
    .tabs .cnt { display: inline-block; min-width: 18px; padding: 0 5px; margin-inline-start: 4px;
                 border-radius: 999px; background: var(--nt-filter-grey); color: var(--nt-slate);
                 font-size: 11px; font-variant-numeric: tabular-nums; }
    .check-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 1rem; }
    .check-grid .wide { grid-column: 1 / -1; }
    @media (max-width: 900px) { .check-grid { grid-template-columns: 1fr 1fr; } }
    .row-item { padding: .5rem 0; border-bottom: 1px solid var(--nt-border); display: flex; justify-content: space-between; gap: 1rem; }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    .danger-text { color: var(--nt-ink-crit); }
    button.link { background: none; border: none; color: var(--nt-blue); cursor: pointer; padding: 0; text-decoration: underline; width: auto; }
    tr.open td { background: color-mix(in srgb, var(--nt-ink-warn) 8%, transparent); }
    .sev { padding: 1px 7px; border-radius: 999px; font-size: 11px; font-weight: 700; }
    .sev.low { background: color-mix(in srgb, var(--nt-slate) 18%, transparent); color: var(--nt-slate); }
    .sev.medium { background: color-mix(in srgb, var(--nt-ink-warn) 22%, transparent); color: #3a2d00; }
    .sev.high { background: var(--nt-ink-crit); color: #fff; }
    h4 { margin: .3rem 0; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class EquipmentDetailComponent implements OnInit {
  readonly facade = inject(EquipmentFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  readonly org = inject(OrgDataService);
  private readonly standardsApi = inject(ReferenceStandardsApiService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound equipment id. */
  readonly id = input.required<string>();

  /** Canonical workflow path for the stepper (off-path states render as terminal). */
  readonly flowSteps = ['NeedsCalibration', 'Active', 'Retired'] as const;

  readonly item = this.facade.selected;

  /** Active history tab; calibration first, matching the page's purpose. */
  readonly tab = signal<'calibration' | 'maintenance' | 'checks' | 'downtime' | 'safety'>('calibration');

  readonly certificate = signal<File | null>(null);
  readonly maintCertificate = signal<File | null>(null);

  readonly downtimeCategories = DOWNTIME_CATEGORIES;
  readonly noticeTypes = SAFETY_NOTICE_TYPES;
  readonly severities = SAFETY_NOTICE_SEVERITIES;
  /** The safety notice currently being actioned (its inline form open), or null. */
  readonly actioningNotice = signal<string | null>(null);

  readonly downtimeForm = this.fb.nonNullable.group({
    startedAt: ['', [Validators.required]],
    category: ['Breakdown' as DowntimeCategory, [Validators.required]],
    reason: ['', [Validators.maxLength(1000)]],
  });
  readonly noticeForm = this.fb.nonNullable.group({
    type: ['Recall' as SafetyNoticeType, [Validators.required]],
    reference: ['', [Validators.required, Validators.maxLength(100)]],
    issuer: ['', [Validators.maxLength(200)]],
    severity: ['Medium' as SafetyNoticeSeverity, [Validators.required]],
    receivedOn: ['', [Validators.required]],
    requiredActionBy: [''],
  });
  readonly actionForm = this.fb.nonNullable.group({
    note: ['', [Validators.required, Validators.maxLength(2000)]],
    on: ['', [Validators.required]],
  });

  readonly calForm = this.fb.nonNullable.group({
    performedAt: ['', [Validators.required]],
    provider: ['', [Validators.maxLength(200)]],
    result: ['', [Validators.required, Validators.maxLength(500)]],
  });
  readonly maintForm = this.fb.nonNullable.group({
    performedAt: ['', [Validators.required]],
    workDescription: ['', [Validators.required, Validators.maxLength(2000)]],
  });
  readonly checkForm = this.fb.nonNullable.group({
    performedOn: ['', [Validators.required]],
    checkType: ['', [Validators.required, Validators.maxLength(200)]],
    referenceStandardId: [''],
    passed: ['true'],
    remarks: ['', [Validators.maxLength(2000)]],
  });

  /** Full register for name resolution; the dropdown offers only active entries. */
  private readonly standards = signal<ReferenceStandardListItem[]>([]);
  readonly activeStandards = computed(() => this.standards().filter((s) => s.status === 'Active'));

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
    void this.org.ensureDirectory();
    void firstValueFrom(this.standardsApi.list())
      .then((standards) => this.standards.set(standards))
      .catch(() => this.standards.set([]));
  }

  /** Ref label for a standard id ('—' when none was used). */
  standardName(id: string | null): string {
    if (!id) { return '—'; }
    return this.standards().find((s) => s.id === id)?.standardRef ?? id.slice(0, 8);
  }

  async recordCheck(id: string): Promise<void> {
    if (this.checkForm.invalid) { return; }
    const raw = this.checkForm.getRawValue();
    await this.facade.recordCheck(id, {
      performedOn: raw.performedOn,
      checkType: raw.checkType,
      passed: raw.passed === 'true',
      referenceStandardId: raw.referenceStandardId || null,
      remarks: raw.remarks.trim() || null,
    });
    this.checkForm.reset({ passed: 'true', referenceStandardId: '' });
  }

  onCert(event: Event): void { this.certificate.set((event.target as HTMLInputElement).files?.[0] ?? null); }

  onMaintCert(event: Event): void { this.maintCertificate.set((event.target as HTMLInputElement).files?.[0] ?? null); }

  async logCalibration(id: string): Promise<void> {
    if (this.calForm.invalid) { return; }
    const { performedAt, provider, result } = this.calForm.getRawValue();
    await this.facade.logCalibration(id, performedAt, provider, result, this.certificate());
    this.certificate.set(null);
    this.calForm.reset();
  }

  async logMaintenance(id: string): Promise<void> {
    if (this.maintForm.invalid) { return; }
    const { performedAt, workDescription } = this.maintForm.getRawValue();
    await this.facade.logMaintenance(id, performedAt, workDescription, this.maintCertificate());
    this.maintCertificate.set(null);
    this.maintForm.reset();
  }

  // ── Downtime & safety notices (HQMS M14) ────────────────────────────────────
  async startDowntime(id: string): Promise<void> {
    if (this.downtimeForm.invalid) { return; }
    const raw = this.downtimeForm.getRawValue();
    await this.facade.startDowntime(id, {
      startedAtUtc: new Date(raw.startedAt).toISOString(), category: raw.category, reason: raw.reason,
    });
    if (this.facade.error() === '') { this.downtimeForm.reset({ category: 'Breakdown', startedAt: '', reason: '' }); }
  }

  async endDowntime(id: string, downtimeId: string): Promise<void> {
    await this.facade.endDowntime(id, downtimeId, { endedAtUtc: new Date().toISOString() });
  }

  startAction(noticeId: string): void {
    this.actionForm.reset({ note: '', on: '' });
    this.actioningNotice.set(noticeId);
  }

  async actionNotice(id: string): Promise<void> {
    const noticeId = this.actioningNotice();
    if (!noticeId || this.actionForm.invalid) { return; }
    const raw = this.actionForm.getRawValue();
    await this.facade.actionSafetyNotice(id, noticeId, { note: raw.note, on: raw.on });
    if (this.facade.error() === '') { this.actioningNotice.set(null); }
  }

  async logNotice(id: string): Promise<void> {
    if (this.noticeForm.invalid) { return; }
    const raw = this.noticeForm.getRawValue();
    await this.facade.logSafetyNotice(id, {
      type: raw.type, reference: raw.reference, issuer: raw.issuer, severity: raw.severity,
      receivedOn: raw.receivedOn, requiredActionBy: raw.requiredActionBy || null,
    });
    if (this.facade.error() === '') { this.noticeForm.reset({ type: 'Recall', severity: 'Medium', reference: '', issuer: '', receivedOn: '', requiredActionBy: '' }); }
  }
}
