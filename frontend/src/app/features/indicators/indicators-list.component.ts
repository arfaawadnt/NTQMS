import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { IndicatorsFacade } from './indicators.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import {
  INDICATOR_DIRECTIONS, INDICATOR_FREQUENCIES, INDICATOR_STATUSES, IndicatorDirection, IndicatorFrequency,
} from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { LoadMoreComponent } from '../../shared/ui/load-more.component';

/**
 * Quality Indicators register (HQMS M06): the indicator library with live status,
 * a define form capturing the data dictionary, and the record workspace in a drawer.
 */
@Component({
    selector: 'qams-indicators-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent, LoadMoreComponent],
    template: `
    <qams-page-header [title]="i18n.t('qi.title')">
      @if (perms.can('indicators.create')) {
        <button (click)="showForm.set(true)">{{ i18n.t('qi.new') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" ratioFromFirst />

    <div class="filterbar card">
      <input class="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="i18n.t('common.search')" />
      <select [value]="statusFilter()" (change)="onStatusFilter($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('qi.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ i18n.t('qi.status.' + s) }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="i18n.t('qi.new')" (closed)="showForm.set(false)">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <div class="grid">
          <div>
            <label>{{ i18n.t('qi.code') }}</label>
            <input formControlName="code" />
          </div>
          <div class="col-2">
            <label>{{ i18n.t('qi.name') }}</label>
            <input formControlName="name" />
          </div>
        </div>
        <label>{{ i18n.t('qi.description') }}</label>
        <textarea rows="2" formControlName="description"></textarea>
        <div class="grid">
          <div class="col-2">
            <label>{{ i18n.t('qi.numerator') }}</label>
            <input formControlName="numerator" />
          </div>
          <div class="col-2">
            <label>{{ i18n.t('qi.denominator') }}</label>
            <input formControlName="denominator" />
          </div>
          <div>
            <label>{{ i18n.t('qi.unit') }}</label>
            <input formControlName="unit" />
          </div>
          <div>
            <label>{{ i18n.t('qi.rateFactor') }}</label>
            <input type="number" formControlName="rateFactor" />
          </div>
          <div>
            <label>{{ i18n.t('qi.frequency') }}</label>
            <select formControlName="frequency">@for (f of frequencies; track f) { <option [value]="f">{{ i18n.t('qi.freq.' + f) }}</option> }</select>
          </div>
          <div>
            <label>{{ i18n.t('qi.direction') }}</label>
            <select formControlName="direction">@for (d of directions; track d) { <option [value]="d">{{ i18n.t('qi.dir.' + d) }}</option> }</select>
          </div>
        </div>
        <label>{{ i18n.t('qi.inclusions') }}</label>
        <textarea rows="2" formControlName="inclusions"></textarea>
        <label>{{ i18n.t('qi.exclusions') }}</label>
        <textarea rows="2" formControlName="exclusions"></textarea>
        <label>{{ i18n.t('qi.dataSource') }}</label>
        <input formControlName="dataSource" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('qi.create') }}</button>
          <button type="button" class="secondary" (click)="showForm.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (filtered().length === 0) {
      <p class="muted">{{ i18n.t('qi.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead>
            <tr>
              <th>{{ i18n.t('qi.code') }}</th><th>{{ i18n.t('qi.name') }}</th>
              <th>{{ i18n.t('qi.latest') }}</th><th>{{ i18n.t('qi.latestStatus') }}</th>
              <th>{{ i18n.t('qi.target') }}</th><th>{{ i18n.t('qi.frequency') }}</th>
              <th>{{ i18n.t('qi.status') }}</th>
            </tr>
          </thead>
          <tbody>
            @for (i of filtered(); track i.id) {
              <tr class="clickable" (click)="open(i.id)">
                <td class="code">{{ i.code }}</td>
                <td>{{ i.name }}</td>
                <td>
                  @if (i.latestValue !== null) { {{ i.latestValue | number:'1.0-2' }} {{ i.unit }}
                    <span class="muted">({{ i.latestPeriod | date:'MMM y' }})</span>
                  } @else { <span class="muted">—</span> }
                </td>
                <td>@if (i.latestStatus) { <qams-status-pill [status]="i.latestStatus" /> } @else { — }</td>
                <td>{{ i.target !== null ? (i.target | number:'1.0-2') + ' ' + i.unit : '—' }}</td>
                <td>{{ i18n.t('qi.freq.' + i.frequency) }}</td>
                <td><qams-status-pill [status]="i.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <qams-load-more [shown]="facade.list().length" [total]="facade.total()" [hasMore]="facade.hasMore()"
                      [loading]="facade.loading()" (more)="facade.loadMore()" />
    }

    <qams-drawer [open]="detailOpen()" [title]="i18n.t('qi.title')" width="960px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    select, button { width: auto; }
  `]
})
export class IndicatorsListComponent implements OnInit {
  readonly facade = inject(IndicatorsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly frequencies = INDICATOR_FREQUENCIES;
  readonly directions = INDICATOR_DIRECTIONS;
  readonly statuses = INDICATOR_STATUSES;
  readonly showForm = signal(false);
  readonly detailOpen = signal(false);
  readonly statusFilter = signal('');
  readonly search = signal('');

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.facade.list().filter((i) =>
      !q || `${i.code} ${i.name} ${i.indicatorRef}`.toLowerCase().includes(q));
  });

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('qi.stat.active'), value: all.filter((i) => i.status === 'Active').length, tone: 'blue' },
      { label: this.i18n.t('qi.stat.breached'), value: all.filter((i) => i.latestStatus === 'Breached').length, tone: 'red' },
      { label: this.i18n.t('qi.stat.warning'), value: all.filter((i) => i.latestStatus === 'Warning').length, tone: 'orange' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(300)]],
    description: ['', [Validators.maxLength(2000)]],
    numerator: ['', [Validators.required, Validators.maxLength(1000)]],
    denominator: ['', [Validators.required, Validators.maxLength(1000)]],
    unit: ['%', [Validators.required, Validators.maxLength(50)]],
    rateFactor: [100, [Validators.required, Validators.min(0.0001)]],
    frequency: ['Monthly' as IndicatorFrequency, [Validators.required]],
    direction: ['HigherIsBetter' as IndicatorDirection, [Validators.required]],
    inclusions: ['', [Validators.maxLength(2000)]],
    exclusions: ['', [Validators.maxLength(2000)]],
    dataSource: ['', [Validators.maxLength(1000)]],
  });

  ngOnInit(): void {
    void this.facade.loadList();
  }

  onStatusFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.statusFilter() || undefined);
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.define({
      code: raw.code,
      name: raw.name,
      description: raw.description || null,
      numerator: raw.numerator,
      denominator: raw.denominator,
      unit: raw.unit,
      rateFactor: raw.rateFactor,
      frequency: raw.frequency,
      direction: raw.direction,
      inclusions: raw.inclusions || null,
      exclusions: raw.exclusions || null,
      dataSource: raw.dataSource || null,
    });
    if (id) {
      this.showForm.set(false);
      this.form.reset({ unit: '%', rateFactor: 100, frequency: 'Monthly', direction: 'HigherIsBetter' });
      void this.router.navigate(['/indicators', id]);
    }
  }

  open(id: string): void {
    void this.router.navigate(['/indicators', id]);
  }

  closeDetail(): void { void this.router.navigate(['/indicators']); }
}
