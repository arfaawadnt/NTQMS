import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { InfectionControlFacade } from './infection-control.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import {
  DEVICE_TYPES, DeviceType, HAI_STATUSES, HAI_TYPES, HaiType,
} from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/**
 * Infection Prevention & Control register (HQMS M09): device-associated infection rates per
 * 1,000 device-days at the top; the HAI-case register with a report drawer and drawer-detail;
 * and the device-exposure register (the rate denominator) with a record drawer and inline removal.
 */
@Component({
    selector: 'qams-infection-control-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('ipc.title')">
      @if (perms.can('infection-control.create')) {
        <button (click)="caseForm.reset(caseDefaults); showCase.set(true)">{{ i18n.t('ipc.reportCase') }}</button>
        <button class="secondary" (click)="deviceForm.reset(deviceDefaults); showDevice.set(true)">{{ i18n.t('ipc.recordDevice') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <div class="filterbar card">
      <select [value]="typeFilter()" (change)="onFilter('type', $event)" aria-label="Type filter">
        <option value="">{{ i18n.t('ipc.allTypes') }}</option>
        @for (t of types; track t) { <option [value]="t">{{ i18n.t('ipc.ty.' + t) }}</option> }
      </select>
      <select [value]="statusFilter()" (change)="onFilter('status', $event)" aria-label="Status filter">
        <option value="">{{ i18n.t('ipc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ i18n.t('ipc.st.' + s) }}</option> }
      </select>
    </div>

    <!-- HAI-case report drawer -->
    <qams-drawer [open]="showCase()" [title]="i18n.t('ipc.reportCase')" (closed)="showCase.set(false)">
      <form class="drawer-form" [formGroup]="caseForm" (ngSubmit)="createCase()">
        <div class="grid">
          <div><label>{{ i18n.t('ipc.type') }}</label><select formControlName="type">@for (t of types; track t) { <option [value]="t">{{ i18n.t('ipc.ty.' + t) }}</option> }</select></div>
          <div><label>{{ i18n.t('ipc.patientRef') }}</label><input formControlName="patientRef" /></div>
          <div><label>{{ i18n.t('ipc.unit') }}</label><input formControlName="unit" /></div>
          <div><label>{{ i18n.t('ipc.onsetDate') }}</label><input type="datetime-local" formControlName="onsetDate" /></div>
          <div><label>{{ i18n.t('ipc.organism') }}</label><input formControlName="organism" /></div>
        </div>
        <label>{{ i18n.t('ipc.description') }}</label>
        <textarea rows="2" formControlName="description"></textarea>
        <div class="row">
          <button type="submit" [disabled]="caseForm.invalid || facade.loading()">{{ i18n.t('ipc.submit') }}</button>
          <button type="button" class="secondary" (click)="showCase.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    <!-- Device-exposure record drawer -->
    <qams-drawer [open]="showDevice()" [title]="i18n.t('ipc.recordDevice')" (closed)="showDevice.set(false)">
      <form class="drawer-form" [formGroup]="deviceForm" (ngSubmit)="createDevice()">
        <div class="grid">
          <div><label>{{ i18n.t('ipc.deviceType') }}</label><select formControlName="deviceType">@for (d of deviceTypes; track d) { <option [value]="d">{{ i18n.t('ipc.dev.' + d) }}</option> }</select></div>
          <div><label>{{ i18n.t('ipc.patientRef') }}</label><input formControlName="patientRef" /></div>
          <div><label>{{ i18n.t('ipc.unit') }}</label><input formControlName="unit" /></div>
          <div><label>{{ i18n.t('ipc.insertedAt') }}</label><input type="datetime-local" formControlName="insertedAt" /></div>
        </div>
        <div class="row">
          <button type="submit" [disabled]="deviceForm.invalid || facade.loading()">{{ i18n.t('ipc.submit') }}</button>
          <button type="button" class="secondary" (click)="showDevice.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    <!-- HAI cases -->
    <h3>{{ i18n.t('ipc.casesHeading') }}</h3>
    @if (facade.loading() && facade.cases().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.cases().length === 0) {
      <p class="muted">{{ i18n.t('ipc.noCases') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('ipc.ref') }}</th><th>{{ i18n.t('ipc.type') }}</th><th>{{ i18n.t('ipc.unit') }}</th>
            <th>{{ i18n.t('ipc.onsetDate') }}</th><th>{{ i18n.t('ipc.organism') }}</th><th>{{ i18n.t('ipc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (c of facade.cases(); track c.id) {
              <tr class="clickable" (click)="open(c.id)">
                <td class="code">{{ c.caseRef }}</td>
                <td>{{ i18n.t('ipc.ty.' + c.type) }}</td>
                <td>{{ c.unit }}</td>
                <td>{{ c.onsetDateUtc | date:'short' }}</td>
                <td>{{ c.organism ?? '—' }}</td>
                <td><qams-status-pill [status]="c.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Device exposures (the denominator) -->
    <h3>{{ i18n.t('ipc.devicesHeading') }}</h3>
    @if (facade.devices().length === 0) {
      <p class="muted">{{ i18n.t('ipc.noDevices') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('ipc.deviceType') }}</th><th>{{ i18n.t('ipc.patientRef') }}</th><th>{{ i18n.t('ipc.unit') }}</th>
            <th>{{ i18n.t('ipc.insertedAt') }}</th><th>{{ i18n.t('ipc.removedAt') }}</th><th>{{ i18n.t('ipc.status') }}</th><th></th>
          </tr></thead>
          <tbody>
            @for (d of facade.devices(); track d.id) {
              <tr>
                <td>{{ i18n.t('ipc.dev.' + d.deviceType) }}</td>
                <td>{{ d.patientRef }}</td>
                <td>{{ d.unit }}</td>
                <td>{{ d.insertedAtUtc | date:'short' }}</td>
                <td>{{ d.removedAtUtc ? (d.removedAtUtc | date:'short') : '—' }}</td>
                <td><qams-status-pill [status]="d.status" /></td>
                <td>@if (d.status === 'InPlace') { <button class="link" (click)="removeDevice(d.id)">{{ i18n.t('ipc.remove') }}</button> }</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <qams-drawer [open]="detailOpen()" [title]="i18n.t('ipc.title')" width="820px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: .5rem 1rem; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    h3 { margin: 1.4rem 0 .6rem; }
    select, button { width: auto; }
    button.link { background: none; border: none; color: var(--nt-blue); cursor: pointer; padding: 0; text-decoration: underline; }
  `]
})
export class InfectionControlListComponent implements OnInit {
  readonly facade = inject(InfectionControlFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly types = HAI_TYPES;
  readonly statuses = HAI_STATUSES;
  readonly deviceTypes = DEVICE_TYPES;

  readonly showCase = signal(false);
  readonly showDevice = signal(false);
  readonly detailOpen = signal(false);
  readonly typeFilter = signal('');
  readonly statusFilter = signal('');

  readonly stats = computed<ListStat[]>(() => {
    const r = this.facade.rates();
    return [
      { label: this.i18n.t('ipc.stat.clabsi'), value: r?.clabsi.ratePer1000 ?? '—', tone: 'red' },
      { label: this.i18n.t('ipc.stat.cauti'), value: r?.cauti.ratePer1000 ?? '—', tone: 'orange' },
      { label: this.i18n.t('ipc.stat.vap'), value: r?.vap.ratePer1000 ?? '—', tone: 'red' },
      { label: this.i18n.t('ipc.stat.ssi'), value: r?.ssiCount ?? 0, tone: 'gold' },
      { label: this.i18n.t('ipc.stat.devicesInPlace'), value: this.facade.devicesInPlace(), tone: 'teal' },
      { label: this.i18n.t('ipc.stat.patientDays'), value: r?.patientDays ?? 0, tone: 'slate' },
    ];
  });

  readonly caseDefaults = { type: 'Clabsi' as HaiType, patientRef: '', unit: '', onsetDate: '', organism: '', description: '' };
  readonly caseForm = this.fb.nonNullable.group({
    type: ['Clabsi' as HaiType, [Validators.required]],
    patientRef: ['', [Validators.required, Validators.maxLength(100)]],
    unit: ['', [Validators.maxLength(100)]],
    onsetDate: ['', [Validators.required]],
    organism: ['', [Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(4000)]],
  });

  readonly deviceDefaults = { deviceType: 'CentralLine' as DeviceType, patientRef: '', unit: '', insertedAt: '' };
  readonly deviceForm = this.fb.nonNullable.group({
    deviceType: ['CentralLine' as DeviceType, [Validators.required]],
    patientRef: ['', [Validators.required, Validators.maxLength(100)]],
    unit: ['', [Validators.maxLength(100)]],
    insertedAt: ['', [Validators.required]],
  });

  ngOnInit(): void {
    void this.facade.loadAll();
  }

  onFilter(which: 'type' | 'status', event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    if (which === 'type') { this.typeFilter.set(val); } else { this.statusFilter.set(val); }
    void this.facade.loadAll(this.typeFilter() || undefined, this.statusFilter() || undefined);
  }

  async createCase(): Promise<void> {
    if (this.caseForm.invalid) { return; }
    const raw = this.caseForm.getRawValue();
    const id = await this.facade.reportCase({
      type: raw.type, patientRef: raw.patientRef, unit: raw.unit,
      onsetDateUtc: new Date(raw.onsetDate).toISOString(),
      organism: raw.organism || null, description: raw.description, departmentId: null,
    });
    if (id) {
      this.showCase.set(false);
      void this.facade.loadAll(this.typeFilter() || undefined, this.statusFilter() || undefined);
      void this.router.navigate(['/infection-control', id]);
    }
  }

  async createDevice(): Promise<void> {
    if (this.deviceForm.invalid) { return; }
    const raw = this.deviceForm.getRawValue();
    const id = await this.facade.recordDevice({
      deviceType: raw.deviceType, patientRef: raw.patientRef, unit: raw.unit,
      insertedAtUtc: new Date(raw.insertedAt).toISOString(), departmentId: null,
    });
    if (id) { this.showDevice.set(false); }
  }

  async removeDevice(id: string): Promise<void> {
    await this.facade.removeDevice(id, { removedAtUtc: new Date().toISOString() });
  }

  open(id: string): void { void this.router.navigate(['/infection-control', id]); }
  closeDetail(): void { void this.router.navigate(['/infection-control']); }
}
