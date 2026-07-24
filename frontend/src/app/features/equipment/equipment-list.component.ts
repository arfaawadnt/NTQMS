import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { EquipmentFacade } from './equipment.facade';
import { I18nService } from '../../core/i18n.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/** Equipment register: status-filterable list + a register form. */
@Component({
  selector: 'qams-equipment-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent],
  template: `
    <qams-page-header [title]="i18n.t('equip.title')">
      <select [value]="statusFilter()" (change)="onFilter($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
      <button (click)="showForm.set(!showForm())">{{ i18n.t('equip.new') }}</button>
    </qams-page-header>

    <qams-drawer [open]="showForm()" [title]="i18n.t('equip.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="register()">
        <div class="grid">
          <div class="col-2"><label>{{ i18n.t('equip.name') }}</label><input formControlName="name" /></div>
          <div><label>{{ i18n.t('equip.serial') }}</label><input formControlName="serialNumber" /></div>
          <div><label>{{ i18n.t('equip.location') }}</label><input formControlName="location" /></div>
          <div><label>{{ i18n.t('equip.interval') }}</label><input type="number" min="1" formControlName="calibrationIntervalDays" /></div>
          <div><label>{{ i18n.t('equip.grace') }}</label><input type="number" min="0" formControlName="gracePeriodDays" /></div>
        </div>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('equip.register') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('equip.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('equip.code') }}</th><th>{{ i18n.t('equip.name') }}</th><th>{{ i18n.t('equip.serial') }}</th>
            <th>{{ i18n.t('nc.status') }}</th><th>{{ i18n.t('equip.nextDue') }}</th>
          </tr></thead>
          <tbody>
            @for (e of facade.list(); track e.id) {
              <tr class="clickable" (click)="open(e.id)">
                <td>{{ e.code }}</td><td>{{ e.name }}</td><td>{{ e.serialNumber }}</td>
                <td><qams-status-pill [status]="e.status" /></td>
                <td>{{ e.nextCalibrationDue ? (e.nextCalibrationDue | date:'mediumDate') : '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('equip.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
  styles: [`
    .form { margin-bottom: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
  `],
})
export class EquipmentListComponent implements OnInit {
  readonly facade = inject(EquipmentFacade);
  readonly i18n = inject(I18nService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = ['NeedsCalibration', 'Active', 'OutOfService', 'Retired'];
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly statusFilter = signal('');

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    serialNumber: ['', [Validators.required, Validators.maxLength(100)]],
    location: [''],
    calibrationIntervalDays: [180, [Validators.required, Validators.min(1)]],
    gracePeriodDays: [14, [Validators.required, Validators.min(0)]],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.statusFilter() || undefined);
  }

  async register(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.register({ ...raw, location: raw.location || null });
    if (id) { this.cancel(); void this.router.navigate(['/equipment', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ calibrationIntervalDays: 180, gracePeriodDays: 14 });
  }

  open(id: string): void { void this.router.navigate(['/equipment', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/equipment']); }
}
