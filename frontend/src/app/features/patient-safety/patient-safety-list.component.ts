import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { PatientSafetyFacade } from './patient-safety.facade';
import { I18nService } from '../../core/i18n.service';
import {
  HARM_LEVELS, HarmLevel, INJURY_ORIGINS, InjuryOrigin, PRESSURE_INJURY_STAGES, PressureInjuryStage,
  SAFETY_EVENT_STATUSES, SAFETY_EVENT_TYPES, SafetyEventType,
} from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/**
 * Patient-safety register (HQMS M08): falls and pressure injuries with rates per 1,000
 * patient-days at the top; report drawer for either type; record workspace in a drawer.
 */
@Component({
    selector: 'qams-patient-safety-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('psf.title')">
      <button (click)="openForm('Fall')">{{ i18n.t('psf.reportFall') }}</button>
      <button class="secondary" (click)="openForm('PressureInjury')">{{ i18n.t('psf.reportPi') }}</button>
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <div class="filterbar card">
      <select [value]="typeFilter()" (change)="onFilter('type', $event)" aria-label="Type filter">
        <option value="">{{ i18n.t('psf.allTypes') }}</option>
        @for (t of types; track t) { <option [value]="t">{{ i18n.t('psf.ty.' + t) }}</option> }
      </select>
      <select [value]="statusFilter()" (change)="onFilter('status', $event)" aria-label="Status filter">
        <option value="">{{ i18n.t('psf.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ i18n.t('psf.st.' + s) }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="anchorType() === 'Fall' ? i18n.t('psf.reportFall') : i18n.t('psf.reportPi')" (closed)="showForm.set(false)">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <div class="grid">
          <div><label>{{ i18n.t('psf.patientRef') }}</label><input formControlName="patientRef" /></div>
          <div><label>{{ i18n.t('psf.unit') }}</label><input formControlName="unit" /></div>
          <div><label>{{ i18n.t('psf.occurredAt') }}</label><input type="datetime-local" formControlName="occurredAt" /></div>
          <div><label>{{ i18n.t('psf.harm') }}</label><select formControlName="harm">@for (h of harms; track h) { <option [value]="h">{{ i18n.t('psf.harm.' + h) }}</option> }</select></div>
          @if (anchorType() === 'PressureInjury') {
            <div><label>{{ i18n.t('psf.stage') }}</label><select formControlName="stage">@for (s of stages; track s) { <option [value]="s">{{ i18n.t('psf.stg.' + s) }}</option> }</select></div>
            <div><label>{{ i18n.t('psf.origin') }}</label><select formControlName="origin">@for (o of origins; track o) { <option [value]="o">{{ i18n.t('psf.or.' + o) }}</option> }</select></div>
          }
        </div>
        <label>{{ i18n.t('psf.description') }}</label>
        <textarea rows="2" formControlName="description"></textarea>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('psf.submit') }}</button>
          <button type="button" class="secondary" (click)="showForm.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('psf.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('psf.ref') }}</th><th>{{ i18n.t('psf.type') }}</th><th>{{ i18n.t('psf.unit') }}</th>
            <th>{{ i18n.t('psf.occurredAt') }}</th><th>{{ i18n.t('psf.harm') }}</th><th>{{ i18n.t('psf.origin') }}</th><th>{{ i18n.t('psf.status') }}</th>
          </tr></thead>
          <tbody>
            @for (e of facade.list(); track e.id) {
              <tr class="clickable" (click)="open(e.id)">
                <td class="code">{{ e.eventRef }}</td>
                <td>{{ i18n.t('psf.ty.' + e.type) }}</td>
                <td>{{ e.unit }}</td>
                <td>{{ e.occurredAtUtc | date:'short' }}</td>
                <td [class.danger-text]="e.harmLevel === 'Severe' || e.harmLevel === 'Death'">{{ i18n.t('psf.harm.' + e.harmLevel) }}</td>
                <td>{{ e.type === 'PressureInjury' ? i18n.t('psf.or.' + e.origin) : '—' }}</td>
                <td><qams-status-pill [status]="e.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <qams-drawer [open]="detailOpen()" [title]="i18n.t('psf.title')" width="820px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: .5rem 1rem; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; } .danger-text { color: var(--nt-ink-crit); font-weight: 700; }
    select, button { width: auto; }
  `]
})
export class PatientSafetyListComponent implements OnInit {
  readonly facade = inject(PatientSafetyFacade);
  readonly i18n = inject(I18nService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly types = SAFETY_EVENT_TYPES;
  readonly statuses = SAFETY_EVENT_STATUSES;
  readonly harms = HARM_LEVELS;
  readonly stages = PRESSURE_INJURY_STAGES;
  readonly origins = INJURY_ORIGINS;

  readonly showForm = signal(false);
  readonly anchorType = signal<SafetyEventType>('Fall');
  readonly detailOpen = signal(false);
  readonly typeFilter = signal('');
  readonly statusFilter = signal('');

  readonly stats = computed<ListStat[]>(() => {
    const r = this.facade.rates();
    return [
      { label: this.i18n.t('psf.stat.total'), value: this.facade.list().length, tone: 'slate' },
      { label: this.i18n.t('psf.stat.fallsRate'), value: r?.falls.ratePer1000 ?? 0, tone: 'orange' },
      { label: this.i18n.t('psf.stat.piRate'), value: r?.pressureInjuries.ratePer1000 ?? 0, tone: 'red' },
      { label: this.i18n.t('psf.stat.hapiRate'), value: r?.hapiRatePer1000 ?? 0, tone: 'red' },
      { label: this.i18n.t('psf.stat.patientDays'), value: r?.patientDays ?? 0, tone: 'teal' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    patientRef: ['', [Validators.required, Validators.maxLength(100)]],
    unit: ['', [Validators.maxLength(100)]],
    occurredAt: ['', [Validators.required]],
    harm: ['None' as HarmLevel, [Validators.required]],
    description: ['', [Validators.maxLength(4000)]],
    stage: ['Stage1' as PressureInjuryStage],
    origin: ['HospitalAcquired' as InjuryOrigin],
  });

  ngOnInit(): void {
    void this.facade.loadList();
  }

  openForm(type: SafetyEventType): void {
    this.anchorType.set(type);
    this.showForm.set(true);
  }

  onFilter(which: 'type' | 'status', event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    if (which === 'type') { this.typeFilter.set(val); } else { this.statusFilter.set(val); }
    void this.facade.loadList(this.typeFilter() || undefined, this.statusFilter() || undefined);
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const common = {
      patientRef: raw.patientRef, unit: raw.unit, occurredAtUtc: new Date(raw.occurredAt).toISOString(),
      harm: raw.harm, description: raw.description, departmentId: null,
    };
    const id = this.anchorType() === 'Fall'
      ? await this.facade.reportFall(common)
      : await this.facade.reportPressureInjury({ ...common, stage: raw.stage, origin: raw.origin });
    if (id) {
      this.showForm.set(false);
      this.form.reset({ harm: 'None', stage: 'Stage1', origin: 'HospitalAcquired' });
      void this.facade.loadList(this.typeFilter() || undefined, this.statusFilter() || undefined);
      void this.router.navigate(['/patient-safety', id]);
    }
  }

  open(id: string): void { void this.router.navigate(['/patient-safety', id]); }
  closeDetail(): void { void this.router.navigate(['/patient-safety']); }
}
