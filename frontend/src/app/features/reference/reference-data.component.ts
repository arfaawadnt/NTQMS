import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ReferenceApiService } from '../../core/api/reference-api.service';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { Branch, Department, LovEntry, TestCatalogItem } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';

type RefTab = 'branches' | 'departments' | 'tests' | 'lovs';

/**
 * Organization & Reference Data: branches, departments (per branch), the test
 * catalog, and trilingual lists of values. Creation is QM/TenantAdmin;
 * deactivation of org units is TenantAdmin-only — both mirroring the backend.
 */
@Component({
    selector: 'qams-reference-data',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [FormsModule, ReactiveFormsModule, PageHeaderComponent, DrawerComponent, StatusPillComponent],
    template: `
    <qams-page-header [title]="i18n.t('ref.title')" [subtitle]="i18n.t('ref.subtitle')">
      @if (perms.canApprove()) {
        <button (click)="openForm()">{{ addLabel() }}</button>
      }
    </qams-page-header>

    <div class="tabs">
      <button class="tab" [class.active]="tab() === 'branches'" (click)="switchTab('branches')">{{ i18n.t('ref.branches') }}</button>
      <button class="tab" [class.active]="tab() === 'departments'" (click)="switchTab('departments')">{{ i18n.t('ref.departments') }}</button>
      <button class="tab" [class.active]="tab() === 'tests'" (click)="switchTab('tests')">{{ i18n.t('ref.tests') }}</button>
      <button class="tab" [class.active]="tab() === 'lovs'" (click)="switchTab('lovs')">{{ i18n.t('ref.lovs') }}</button>
    </div>

    @if (error()) { <div class="error">{{ error() }}</div> }
    @if (loading()) { <p class="muted">{{ i18n.t('common.loading') }}</p> }

    <!-- ── Branches ─────────────────────────────────────────────────────── -->
    @if (tab() === 'branches' && !loading()) {
      <div class="card">
        @if (branches().length === 0) { <p class="muted">{{ i18n.t('ref.empty') }}</p> }
        @else {
          <table>
            <thead><tr><th>{{ i18n.t('ref.code') }}</th><th>{{ i18n.t('ref.name') }}</th><th>{{ i18n.t('ref.city') }}</th><th>{{ i18n.t('nc.status') }}</th><th></th></tr></thead>
            <tbody>
              @for (b of branches(); track b.id) {
                <tr>
                  <td class="code">{{ b.code }}</td><td>{{ b.name }}</td><td>{{ b.city ?? '—' }}</td>
                  <td><qams-status-pill [status]="b.isActive ? 'Active' : 'Obsolete'" /></td>
                  <td>
                    @if (b.isActive && perms.isTenantAdmin()) {
                      <button class="link danger-link" type="button" (click)="deactivateBranch(b.id)">{{ i18n.t('ref.deactivate') }}</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    }

    <!-- ── Departments ──────────────────────────────────────────────────── -->
    @if (tab() === 'departments' && !loading()) {
      <div class="card">
        <div class="filter">
          <select [ngModel]="branchFilter()" (ngModelChange)="onBranchFilter($event)" aria-label="Branch filter">
            <option value="">{{ i18n.t('ref.allBranches') }}</option>
            @for (b of branches(); track b.id) { <option [value]="b.id">{{ b.code }} — {{ b.name }}</option> }
          </select>
        </div>
        @if (departments().length === 0) { <p class="muted">{{ i18n.t('ref.empty') }}</p> }
        @else {
          <table>
            <thead><tr><th>{{ i18n.t('ref.code') }}</th><th>{{ i18n.t('ref.name') }}</th><th>{{ i18n.t('ref.branch') }}</th><th>{{ i18n.t('nc.status') }}</th><th></th></tr></thead>
            <tbody>
              @for (d of departments(); track d.id) {
                <tr>
                  <td class="code">{{ d.code }}</td><td>{{ d.name }}</td>
                  <td>{{ branchName(d.branchId) }}</td>
                  <td><qams-status-pill [status]="d.isActive ? 'Active' : 'Obsolete'" /></td>
                  <td>
                    @if (d.isActive && perms.isTenantAdmin()) {
                      <button class="link danger-link" type="button" (click)="deactivateDepartment(d.id)">{{ i18n.t('ref.deactivate') }}</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    }

    <!-- ── Test catalog ─────────────────────────────────────────────────── -->
    @if (tab() === 'tests' && !loading()) {
      <div class="card">
        @if (tests().length === 0) { <p class="muted">{{ i18n.t('ref.empty') }}</p> }
        @else {
          <table>
            <thead><tr><th>{{ i18n.t('ref.testCode') }}</th><th>{{ i18n.t('ref.testName') }}</th><th>{{ i18n.t('ref.methodology') }}</th><th>{{ i18n.t('ref.tat') }}</th><th>{{ i18n.t('nc.status') }}</th></tr></thead>
            <tbody>
              @for (t of tests(); track t.id) {
                <tr>
                  <td class="code">{{ t.testCode }}</td><td>{{ t.testName }}</td><td>{{ t.methodology }}</td>
                  <td>{{ t.turnaroundHours }}h</td>
                  <td><qams-status-pill [status]="t.isActive ? 'Active' : 'Obsolete'" /></td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    }

    <!-- ── LOVs ─────────────────────────────────────────────────────────── -->
    @if (tab() === 'lovs' && !loading()) {
      <div class="card">
        <div class="filter">
          <input [(ngModel)]="lovCategory" (keyup.enter)="loadLovs()" [placeholder]="i18n.t('ref.categoryHint')" />
          <button class="secondary" type="button" (click)="loadLovs()">{{ i18n.t('cmp.search') }}</button>
        </div>
        @if (lovs().length === 0) { <p class="muted">{{ i18n.t('ref.empty') }}</p> }
        @else {
          <table>
            <thead><tr><th>{{ i18n.t('ref.category') }}</th><th>{{ i18n.t('ref.code') }}</th><th>EN</th><th>AR</th><th>FR</th><th>#</th><th>{{ i18n.t('nc.status') }}</th></tr></thead>
            <tbody>
              @for (l of lovs(); track l.id) {
                <tr>
                  <td class="code">{{ l.category }}</td><td class="code">{{ l.code }}</td>
                  <td>{{ l.nameEn }}</td><td>{{ l.nameAr ?? '—' }}</td><td>{{ l.nameFr ?? '—' }}</td>
                  <td>{{ l.sortOrder }}</td>
                  <td><qams-status-pill [status]="l.isActive ? 'Active' : 'Obsolete'" /></td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    }

    <!-- ── Create drawers (one per tab) ─────────────────────────────────── -->
    <qams-drawer [open]="showForm() && tab() === 'branches'" [title]="i18n.t('ref.newBranch')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="branchForm" (ngSubmit)="createBranch()">
        <label>{{ i18n.t('ref.code') }}</label><input formControlName="code" />
        <label>{{ i18n.t('ref.name') }}</label><input formControlName="name" />
        <label>{{ i18n.t('ref.city') }}</label><input formControlName="city" [placeholder]="i18n.t('common.optional')" />
        <div class="row">
          <button type="submit" [disabled]="branchForm.invalid || loading()">{{ i18n.t('qc.create') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (error()) { <div class="error">{{ error() }}</div> }
      </form>
    </qams-drawer>

    <qams-drawer [open]="showForm() && tab() === 'departments'" [title]="i18n.t('ref.newDepartment')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="deptForm" (ngSubmit)="createDepartment()">
        <label>{{ i18n.t('ref.branch') }}</label>
        <select formControlName="branchId">
          <option value="">—</option>
          @for (b of branches(); track b.id) { <option [value]="b.id">{{ b.code }} — {{ b.name }}</option> }
        </select>
        <label>{{ i18n.t('ref.code') }}</label><input formControlName="code" />
        <label>{{ i18n.t('ref.name') }}</label><input formControlName="name" />
        <div class="row">
          <button type="submit" [disabled]="deptForm.invalid || loading()">{{ i18n.t('qc.create') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (error()) { <div class="error">{{ error() }}</div> }
      </form>
    </qams-drawer>

    <qams-drawer [open]="showForm() && tab() === 'tests'" [title]="i18n.t('ref.newTest')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="testForm" (ngSubmit)="createTest()">
        <label>{{ i18n.t('ref.testCode') }}</label><input formControlName="testCode" />
        <label>{{ i18n.t('ref.testName') }}</label><input formControlName="testName" />
        <label>{{ i18n.t('ref.methodology') }}</label><input formControlName="methodology" />
        <label>{{ i18n.t('ref.tat') }}</label><input type="number" min="1" formControlName="turnaroundHours" />
        <div class="row">
          <button type="submit" [disabled]="testForm.invalid || loading()">{{ i18n.t('qc.create') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (error()) { <div class="error">{{ error() }}</div> }
      </form>
    </qams-drawer>

    <qams-drawer [open]="showForm() && tab() === 'lovs'" [title]="i18n.t('ref.newLov')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="lovForm" (ngSubmit)="upsertLov()">
        <label>{{ i18n.t('ref.category') }}</label><input formControlName="category" [placeholder]="i18n.t('ref.categoryHint')" />
        <label>{{ i18n.t('ref.code') }}</label><input formControlName="code" />
        <label>{{ i18n.t('ref.nameEn') }}</label><input formControlName="nameEn" />
        <label>{{ i18n.t('ref.nameAr') }}</label><input formControlName="nameAr" dir="rtl" [placeholder]="i18n.t('common.optional')" />
        <label>{{ i18n.t('ref.nameFr') }}</label><input formControlName="nameFr" [placeholder]="i18n.t('common.optional')" />
        <label>{{ i18n.t('ref.sortOrder') }}</label><input type="number" formControlName="sortOrder" />
        <div class="row">
          <button type="submit" [disabled]="lovForm.invalid || loading()">{{ i18n.t('ref.save') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (error()) { <div class="error">{{ error() }}</div> }
      </form>
    </qams-drawer>
  `,
    styles: [`
    .tabs { display: flex; gap: 0; margin-bottom: 12px; background: var(--nt-filter-grey); border-radius: 8px; padding: 3px; width: fit-content; }
    .tab { background: transparent; color: var(--nt-slate); font-size: 12.5px; padding: 7px 16px; border-radius: 6px; }
    .tab:hover { background: rgba(255,255,255,.6); }
    .tab.active { background: #fff; color: var(--nt-blue); box-shadow: var(--nt-shadow-xs); font-weight: 700; }
    .filter { display: flex; gap: 8px; margin-bottom: 12px; }
    .filter input { max-width: 320px; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .danger-link { color: var(--nt-red); }
    button, select { width: auto; }
  `]
})
export class ReferenceDataComponent implements OnInit {
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly api = inject(ReferenceApiService);
  private readonly fb = inject(FormBuilder);

  readonly tab = signal<RefTab>('branches');
  readonly branches = signal<Branch[]>([]);
  readonly departments = signal<Department[]>([]);
  readonly tests = signal<TestCatalogItem[]>([]);
  readonly lovs = signal<LovEntry[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly showForm = signal(false);
  readonly branchFilter = signal('');
  /** Free-text LOV category filter (e.g. NC_SOURCE). */
  lovCategory = '';

  readonly branchForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    city: [''],
  });
  readonly deptForm = this.fb.nonNullable.group({
    branchId: ['', [Validators.required]],
    code: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
  });
  readonly testForm = this.fb.nonNullable.group({
    testCode: ['', [Validators.required, Validators.maxLength(50)]],
    testName: ['', [Validators.required, Validators.maxLength(300)]],
    methodology: ['', [Validators.required, Validators.maxLength(300)]],
    turnaroundHours: [24, [Validators.required, Validators.min(1)]],
  });
  readonly lovForm = this.fb.nonNullable.group({
    category: ['', [Validators.required, Validators.maxLength(100)]],
    code: ['', [Validators.required, Validators.maxLength(100)]],
    nameEn: ['', [Validators.required, Validators.maxLength(300)]],
    nameAr: [''],
    nameFr: [''],
    sortOrder: [0, [Validators.required]],
  });

  ngOnInit(): void { void this.loadBranches(); }

  addLabel(): string {
    switch (this.tab()) {
      case 'branches': return this.i18n.t('ref.newBranch');
      case 'departments': return this.i18n.t('ref.newDepartment');
      case 'tests': return this.i18n.t('ref.newTest');
      case 'lovs': return this.i18n.t('ref.newLov');
    }
  }

  switchTab(tab: RefTab): void {
    this.tab.set(tab);
    this.showForm.set(false);
    if (tab === 'branches' && this.branches().length === 0) { void this.loadBranches(); }
    if (tab === 'departments') { void this.loadDepartments(); }
    if (tab === 'tests' && this.tests().length === 0) { void this.loadTests(); }
    if (tab === 'lovs' && this.lovs().length === 0) { void this.loadLovs(); }
  }

  openForm(): void { this.showForm.set(true); }

  branchName(branchId: string): string {
    const b = this.branches().find((x) => x.id === branchId);
    return b ? `${b.code} — ${b.name}` : branchId;
  }

  onBranchFilter(value: string): void {
    this.branchFilter.set(value);
    void this.loadDepartments();
  }

  async loadBranches(): Promise<void> {
    await this.run(async () => this.branches.set(await firstValueFrom(this.api.branches())));
  }

  async loadDepartments(): Promise<void> {
    await this.run(async () => {
      if (this.branches().length === 0) { this.branches.set(await firstValueFrom(this.api.branches())); }
      this.departments.set(await firstValueFrom(this.api.departments(this.branchFilter() || undefined)));
    });
  }

  async loadTests(): Promise<void> {
    await this.run(async () => this.tests.set(await firstValueFrom(this.api.testCatalog())));
  }

  async loadLovs(): Promise<void> {
    await this.run(async () => this.lovs.set(await firstValueFrom(this.api.lovs(this.lovCategory.trim() || undefined))));
  }

  async createBranch(): Promise<void> {
    if (this.branchForm.invalid) { return; }
    const raw = this.branchForm.getRawValue();
    await this.run(async () => {
      await firstValueFrom(this.api.createBranch({ ...raw, city: raw.city.trim() || null }));
      this.cancel();
      this.branches.set(await firstValueFrom(this.api.branches()));
    });
  }

  async createDepartment(): Promise<void> {
    if (this.deptForm.invalid) { return; }
    await this.run(async () => {
      await firstValueFrom(this.api.createDepartment(this.deptForm.getRawValue()));
      this.cancel();
      this.departments.set(await firstValueFrom(this.api.departments(this.branchFilter() || undefined)));
    });
  }

  async createTest(): Promise<void> {
    if (this.testForm.invalid) { return; }
    await this.run(async () => {
      await firstValueFrom(this.api.createTest(this.testForm.getRawValue()));
      this.cancel();
      this.tests.set(await firstValueFrom(this.api.testCatalog()));
    });
  }

  async upsertLov(): Promise<void> {
    if (this.lovForm.invalid) { return; }
    const raw = this.lovForm.getRawValue();
    await this.run(async () => {
      await firstValueFrom(this.api.upsertLov({
        ...raw,
        nameAr: raw.nameAr.trim() || null,
        nameFr: raw.nameFr.trim() || null,
      }));
      this.cancel();
      this.lovCategory = raw.category;
      await this.loadLovs();
    });
  }

  async deactivateBranch(id: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.deactivateBranch(id));
      this.branches.set(await firstValueFrom(this.api.branches()));
    });
  }

  async deactivateDepartment(id: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.deactivateDepartment(id));
      this.departments.set(await firstValueFrom(this.api.departments(this.branchFilter() || undefined)));
    });
  }

  cancel(): void {
    this.showForm.set(false);
    this.branchForm.reset();
    this.deptForm.reset();
    this.testForm.reset({ turnaroundHours: 24 });
    this.lovForm.reset({ sortOrder: 0 });
  }

  private async run(operation: () => Promise<void>): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      await operation();
    } catch (err) {
      this.error.set(this.describe(err));
    } finally {
      this.loading.set(false);
    }
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return (err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`;
    }
    return 'Unexpected error.';
  }
}
