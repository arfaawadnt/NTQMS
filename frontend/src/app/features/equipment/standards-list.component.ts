import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { StandardsFacade } from './standards.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { REFERENCE_STANDARD_TYPES } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AllocationPickerComponent } from '../../shared/ui/allocation-picker.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/** Reference standard / CRM register: statistics, filtration, and registration (§6.5). */
@Component({
    selector: 'qams-standards-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, AllocationPickerComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('std.title')" [subtitle]="i18n.t('std.subtitle')">
      @if (perms.can('reference-standards.create')) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('std.new') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" ratioFromFirst />

    <div class="filterbar card">
      <input class="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="i18n.t('common.search')" />
      <select [value]="statusFilter()" (change)="onFilter($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="i18n.t('std.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="register()">
        <label>{{ i18n.t('std.name') }}</label>
        <input formControlName="name" [placeholder]="i18n.t('std.nameHint')" />
        <label>{{ i18n.t('std.type') }}</label>
        <select formControlName="type">
          @for (t of types; track t) { <option [value]="t">{{ i18n.t('std.type' + t) }}</option> }
        </select>
        <label>{{ i18n.t('std.traceableTo') }}</label>
        <input formControlName="traceableTo" [placeholder]="i18n.t('std.traceableToHint')" />
        <label>{{ i18n.t('std.manufacturer') }}</label>
        <input formControlName="manufacturer" />
        <label>{{ i18n.t('std.lot') }}</label>
        <input formControlName="lotNumber" />
        <label>{{ i18n.t('std.certificateNo') }}</label>
        <input formControlName="certificateNumber" />
        <label>{{ i18n.t('std.certifiedValue') }}</label>
        <input formControlName="certifiedValue" [placeholder]="i18n.t('std.certifiedValueHint')" />
        <label>{{ i18n.t('std.uncertainty') }}</label>
        <input formControlName="uncertaintyStatement" [placeholder]="i18n.t('std.uncertaintyHint')" />
        <label>{{ i18n.t('std.receivedOn') }}</label>
        <input type="date" formControlName="receivedOn" />
        <label>{{ i18n.t('std.expiresOn') }}</label>
        <input type="date" formControlName="expiresOn" />
        <qams-allocation-picker [branchCtrl]="form.controls.branchId" [departmentCtrl]="form.controls.departmentId" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('qc.create') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (filtered().length === 0) {
      <p class="muted">{{ i18n.t('std.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('std.name') }}</th><th>{{ i18n.t('std.type') }}</th>
            <th>{{ i18n.t('std.traceableTo') }}</th><th>{{ i18n.t('std.expiresOn') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (s of filtered(); track s.id) {
              <tr class="clickable" (click)="open(s.id)">
                <td class="code">{{ s.standardRef }}</td>
                <td>{{ s.name }}</td>
                <td>{{ i18n.t('std.type' + s.type) }}</td>
                <td>{{ s.traceableTo }}</td>
                <td>{{ s.expiresOn ? (s.expiresOn | date:'mediumDate') : '—' }}</td>
                <td><qams-status-pill [status]="s.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('std.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
  `]
})
export class StandardsListComponent implements OnInit {
  readonly facade = inject(StandardsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = ['Active', 'Quarantined', 'Expired', 'Retired'];
  readonly types = REFERENCE_STANDARD_TYPES;
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly statusFilter = signal('');
  readonly search = signal('');

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.facade.list().filter((s) =>
      !q || `${s.standardRef} ${s.name} ${s.type} ${s.traceableTo} ${s.status}`.toLowerCase().includes(q));
  });

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('std.active'), value: all.filter((s) => s.status === 'Active').length, tone: 'green' },
      { label: this.i18n.t('std.quarantined'), value: all.filter((s) => s.status === 'Quarantined').length, tone: 'orange' },
      { label: this.i18n.t('std.expired'), value: all.filter((s) => s.status === 'Expired').length, tone: 'red' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(300)]],
    type: ['CertifiedReferenceMaterial' as (typeof REFERENCE_STANDARD_TYPES)[number], [Validators.required]],
    traceableTo: ['', [Validators.required, Validators.maxLength(500)]],
    manufacturer: [''],
    lotNumber: [''],
    certificateNumber: [''],
    certifiedValue: [''],
    uncertaintyStatement: [''],
    receivedOn: ['', [Validators.required]],
    expiresOn: [''],
    branchId: [''],
    departmentId: [''],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.statusFilter() || undefined);
  }

  async register(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.register({
      ...raw,
      manufacturer: raw.manufacturer.trim() || null,
      lotNumber: raw.lotNumber.trim() || null,
      certificateNumber: raw.certificateNumber.trim() || null,
      certifiedValue: raw.certifiedValue.trim() || null,
      uncertaintyStatement: raw.uncertaintyStatement.trim() || null,
      expiresOn: raw.expiresOn || null,
      branchId: raw.branchId || null,
      departmentId: raw.departmentId || null,
    });
    if (id) { this.cancel(); void this.router.navigate(['/reference-standards', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ type: 'CertifiedReferenceMaterial' });
  }

  open(id: string): void { void this.router.navigate(['/reference-standards', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/reference-standards']); }
}
