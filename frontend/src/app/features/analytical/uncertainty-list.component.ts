import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { UncertaintyFacade } from './uncertainty.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/** MU budget register: statistics, filtration, and a create form (QM/DeptHead-gated). */
@Component({
  selector: 'qams-uncertainty-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DecimalPipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent],
  template: `
    <qams-page-header [title]="i18n.t('mu.title')" [subtitle]="i18n.t('mu.subtitle')">
      @if (perms.canAssignTraining()) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('mu.new') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <div class="filterbar card">
      <input class="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="i18n.t('common.search')" />
      <select [value]="statusFilter()" (change)="onFilter($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="i18n.t('mu.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('qc.analyte') }}</label>
        <input formControlName="analyte" />
        <label>{{ i18n.t('mu.method') }}</label>
        <input formControlName="method" [placeholder]="i18n.t('mu.methodHint')" />
        <label>{{ i18n.t('mu.unit') }}</label>
        <input formControlName="unit" [placeholder]="i18n.t('mu.unitHint')" />
        <label>{{ i18n.t('mu.level') }}</label>
        <input formControlName="level" [placeholder]="i18n.t('mu.levelHint')" />
        <label>{{ i18n.t('mu.k') }}</label>
        <input type="number" min="1" max="4" step="0.1" formControlName="coverageFactor" />
        <div class="hint">{{ i18n.t('mu.kHint') }}</div>
        <label>{{ i18n.t('mu.target') }}</label>
        <input type="number" min="0.01" step="any" formControlName="targetExpandedUncertainty" [placeholder]="i18n.t('common.optional')" />
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
      <p class="muted">{{ i18n.t('mu.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('qc.analyte') }}</th><th>{{ i18n.t('mu.method') }}</th>
            <th>{{ i18n.t('mu.level') }}</th><th>U (%)</th><th>{{ i18n.t('val.verdict') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (b of filtered(); track b.id) {
              <tr class="clickable" (click)="open(b.id)">
                <td class="code">{{ b.budgetRef }}</td>
                <td>{{ b.analyte }}</td>
                <td>{{ b.method }}</td>
                <td>{{ b.level }}</td>
                <td>{{ b.expandedUncertainty !== null ? (b.expandedUncertainty | number:'1.2-4') : '—' }}</td>
                <td>
                  @if (b.meetsTarget === true) { <qams-status-pill status="Satisfactory" /> }
                  @else if (b.meetsTarget === false) { <qams-status-pill status="Failed" /> }
                  @else { — }
                </td>
                <td><qams-status-pill [status]="b.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('mu.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
  styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
  `],
})
export class UncertaintyListComponent implements OnInit {
  readonly facade = inject(UncertaintyFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = ['Draft', 'Calculated', 'Approved'];
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly statusFilter = signal('');
  readonly search = signal('');

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.facade.list().filter((b) =>
      !q || `${b.budgetRef} ${b.analyte} ${b.method} ${b.level} ${b.status}`.toLowerCase().includes(q));
  });

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('stat.approved'), value: all.filter((b) => b.status === 'Approved').length, tone: 'green' },
      { label: this.i18n.t('mu.overTarget'), value: all.filter((b) => b.meetsTarget === false).length, tone: 'red' },
      { label: this.i18n.t('stat.open'), value: all.filter((b) => b.status !== 'Approved').length, tone: 'blue' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    analyte: ['', [Validators.required, Validators.maxLength(200)]],
    method: ['', [Validators.required, Validators.maxLength(300)]],
    unit: ['', [Validators.maxLength(50)]],
    level: ['', [Validators.maxLength(100)]],
    coverageFactor: [2, [Validators.required, Validators.min(1), Validators.max(4)]],
    targetExpandedUncertainty: [null as number | null],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.statusFilter() || undefined);
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.create(this.form.getRawValue());
    if (id) { this.cancel(); void this.router.navigate(['/uncertainty', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ coverageFactor: 2, targetExpandedUncertainty: null });
  }

  open(id: string): void { void this.router.navigate(['/uncertainty', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/uncertainty']); }
}
