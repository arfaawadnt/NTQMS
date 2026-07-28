import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { SigmaFacade } from './sigma.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/** Six-Sigma assessment register: analytical method capability and QC-design guidance. */
@Component({
    selector: 'qams-sigma-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DecimalPipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('sig.title')" [subtitle]="i18n.t('sig.subtitle')">
      @if (perms.canAssignTraining()) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('sig.new') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <div class="filterbar card">
      <input class="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="i18n.t('common.search')" />
      <select [value]="stateFilter()" (change)="onFilter($event)" aria-label="State filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of states; track s) { <option [value]="s">{{ s }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="i18n.t('sig.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('qc.analyte') }}</label>
        <input formControlName="analyte" />
        <label>{{ i18n.t('mu.unit') }}</label>
        <input formControlName="unit" [placeholder]="i18n.t('mu.unitHint')" />
        <label>{{ i18n.t('sig.tea') }}</label>
        <input type="number" step="any" formControlName="allowableTotalErrorPct" />
        <div class="hint">{{ i18n.t('sig.teaHint') }}</div>
        <label>{{ i18n.t('sig.bias') }}</label>
        <input type="number" step="any" formControlName="biasPct" />
        <label>{{ i18n.t('sig.cv') }}</label>
        <input type="number" step="any" formControlName="cvPct" />
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
      <p class="muted">{{ i18n.t('sig.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('qc.analyte') }}</th>
            <th>TEa%</th><th>{{ i18n.t('sig.biasShort') }}</th><th>CV%</th>
            <th>{{ i18n.t('sig.sigma') }}</th><th>{{ i18n.t('sig.grade') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (s of filtered(); track s.id) {
              <tr class="clickable" (click)="open(s.id)">
                <td class="code">{{ s.assessmentRef }}</td>
                <td>{{ s.analyte }}</td>
                <td>{{ s.allowableTotalErrorPct | number:'1.0-2' }}</td>
                <td>{{ s.biasPct | number:'1.0-2' }}</td>
                <td>{{ s.cvPct | number:'1.0-2' }}</td>
                <td><b [class]="'sigma ' + s.grade.toLowerCase()">{{ s.sigmaValue | number:'1.1-2' }}σ</b></td>
                <td>{{ i18n.t('sig.grade' + s.grade) }}</td>
                <td><qams-status-pill [status]="s.state" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('sig.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
    .sigma.worldclass, .sigma.excellent { color: var(--nt-green); }
    .sigma.good { color: var(--nt-teal); }
    .sigma.marginal { color: var(--nt-orange, #ef6c00); }
    .sigma.unacceptable { color: var(--nt-red); }
  `]
})
export class SigmaListComponent implements OnInit {
  readonly facade = inject(SigmaFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly states = ['Draft', 'SignedOff'];
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly stateFilter = signal('');
  readonly search = signal('');

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.facade.list().filter((s) =>
      !q || `${s.assessmentRef} ${s.analyte} ${s.grade} ${s.state}`.toLowerCase().includes(q));
  });

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('sig.worldClass'), value: all.filter((s) => s.sigmaValue >= 6).length, tone: 'green' },
      { label: this.i18n.t('sig.acceptable'), value: all.filter((s) => s.sigmaValue >= 3).length, tone: 'teal' },
      { label: this.i18n.t('sig.belowThree'), value: all.filter((s) => s.sigmaValue < 3).length, tone: 'red' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    analyte: ['', [Validators.required, Validators.maxLength(200)]],
    unit: ['', [Validators.maxLength(50)]],
    allowableTotalErrorPct: [null as number | null, [Validators.required, Validators.min(0.0001)]],
    biasPct: [0, [Validators.required]],
    cvPct: [null as number | null, [Validators.required, Validators.min(0.0001)]],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.stateFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.stateFilter() || undefined);
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.create({
      ...raw,
      allowableTotalErrorPct: raw.allowableTotalErrorPct!,
      cvPct: raw.cvPct!,
    });
    if (id) { this.cancel(); void this.router.navigate(['/sigma-metrics', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ biasPct: 0 });
  }

  open(id: string): void { void this.router.navigate(['/sigma-metrics', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/sigma-metrics']); }
}
