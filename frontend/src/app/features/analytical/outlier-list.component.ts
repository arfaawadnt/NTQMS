import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterOutlet } from '@angular/router';
import { OutlierFacade } from './outlier.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/** Outlier-screening register: Tukey fences + MAD-based modified z-score. */
@Component({
  selector: 'qams-outlier-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent],
  template: `
    <qams-page-header [title]="i18n.t('out.title')" [subtitle]="i18n.t('out.subtitle')">
      @if (perms.canAssignTraining()) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('out.new') }}</button>
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

    <qams-drawer [open]="showForm()" [title]="i18n.t('out.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('out.dataset') }}</label>
        <input formControlName="dataset" [placeholder]="i18n.t('out.datasetHint')" />
        <label>{{ i18n.t('mu.unit') }}</label>
        <input formControlName="unit" [placeholder]="i18n.t('mu.unitHint')" />
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
      <p class="muted">{{ i18n.t('out.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('out.dataset') }}</th>
            <th>{{ i18n.t('out.points') }}</th><th>{{ i18n.t('out.outliers') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (s of filtered(); track s.id) {
              <tr class="clickable" (click)="open(s.id)">
                <td class="code">{{ s.screeningRef }}</td>
                <td>{{ s.dataset }}</td>
                <td>{{ s.pointCount ?? '—' }}</td>
                <td>
                  @if (s.outlierCount === null) { — }
                  @else if (s.outlierCount === 0) { <qams-status-pill status="Pass" /> }
                  @else { <qams-status-pill status="Failed" />&nbsp;{{ s.outlierCount }} }
                </td>
                <td><qams-status-pill [status]="s.state" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <qams-drawer [open]="detailOpen()" [title]="i18n.t('out.title')" width="920px" (closed)="closeDetail()">
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
export class OutlierListComponent implements OnInit {
  readonly facade = inject(OutlierFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly states = ['DataEntry', 'Calculated', 'SignedOff'];
  readonly showForm = signal(false);
  readonly detailOpen = signal(false);
  readonly stateFilter = signal('');
  readonly search = signal('');

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.facade.list().filter((s) =>
      !q || `${s.screeningRef} ${s.dataset} ${s.state}`.toLowerCase().includes(q));
  });

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('out.clean'), value: all.filter((s) => s.outlierCount === 0).length, tone: 'green' },
      { label: this.i18n.t('out.outliers'), value: all.filter((s) => (s.outlierCount ?? 0) > 0).length, tone: 'red' },
      { label: this.i18n.t('mc.signedOff'), value: all.filter((s) => s.state === 'SignedOff').length, tone: 'teal' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    dataset: ['', [Validators.required, Validators.maxLength(200)]],
    unit: ['', [Validators.maxLength(50)]],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.stateFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.stateFilter() || undefined);
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.create(this.form.getRawValue());
    if (id) { this.cancel(); void this.router.navigate(['/outlier-screenings', id]); }
  }

  cancel(): void { this.showForm.set(false); this.form.reset(); }

  open(id: string): void { void this.router.navigate(['/outlier-screenings', id]); }

  closeDetail(): void { void this.router.navigate(['/outlier-screenings']); }
}
