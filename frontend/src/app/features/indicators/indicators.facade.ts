import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { IndicatorsApiService } from '../../core/api/indicators-api.service';
import {
  DefineIndicatorRequest, IndicatorControlChart, IndicatorDetail, IndicatorListItem,
  RecordMeasurementRequest, SetIndicatorTargetsRequest, UpdateIndicatorDefinitionRequest,
} from '../../core/models';

/**
 * Signal-based facade for the Quality Indicators module (HQMS M06). Holds the register,
 * the loaded indicator with its measurements, and the on-demand control-chart analysis;
 * all API orchestration and refresh-after-write live here so components stay presentational.
 */
@Injectable({ providedIn: 'root' })
export class IndicatorsFacade {
  private readonly api = inject(IndicatorsApiService);

  private readonly _list = signal<IndicatorListItem[]>([]);
  private readonly _total = signal(0);
  private readonly _hasMore = signal(false);
  private readonly _selected = signal<IndicatorDetail | null>(null);
  private readonly _chart = signal<IndicatorControlChart | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');
  private readonly _page = signal(1);
  private lastStatus?: string;
  private lastSearch?: string;

  readonly list = this._list.asReadonly();
  readonly total = this._total.asReadonly();
  readonly hasMore = this._hasMore.asReadonly();
  readonly selected = this._selected.asReadonly();
  /** On-demand SPC analysis for the loaded indicator. */
  readonly chart = this._chart.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  /** Count of indicators whose latest measurement breached the action threshold. */
  readonly breachedCount = computed(() => this._list().filter((i) => i.latestStatus === 'Breached').length);

  async loadList(status?: string, search?: string): Promise<void> {
    this.lastStatus = status;
    this.lastSearch = search;
    await this.run(async () => {
      const page = await firstValueFrom(this.api.list(status, search));
      this._page.set(1);
      this._list.set(page.items);
      this._total.set(page.total);
      this._hasMore.set(page.hasMore);
    });
  }

  async loadMore(): Promise<void> {
    if (this._loading() || !this._hasMore()) { return; }
    await this.run(async () => {
      const next = this._page() + 1;
      const page = await firstValueFrom(this.api.list(this.lastStatus, this.lastSearch, next));
      this._page.set(next);
      this._list.update((items) => [...items, ...page.items]);
      this._total.set(page.total);
      this._hasMore.set(page.hasMore);
    });
  }

  /** Loads a single indicator with its measurements and its control-chart analysis. */
  async loadDetail(id: string): Promise<void> {
    await this.run(async () => {
      this._selected.set(await firstValueFrom(this.api.getById(id)));
      this._chart.set(await firstValueFrom(this.api.controlChart(id)));
    });
  }

  async define(request: DefineIndicatorRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.define(request))).id);
  }

  async update(id: string, r: UpdateIndicatorDefinitionRequest): Promise<void> { await this.mutate(id, () => this.api.update(id, r)); }
  async setTargets(id: string, r: SetIndicatorTargetsRequest): Promise<void> { await this.mutate(id, () => this.api.setTargets(id, r)); }
  async retire(id: string): Promise<void> { await this.mutate(id, () => this.api.retire(id)); }

  /** Records a period measurement, then refreshes both the record and the SPC chart. */
  async recordMeasurement(id: string, r: RecordMeasurementRequest): Promise<void> {
    await this.mutate(id, () => this.api.recordMeasurement(id, r));
    if (this._error() === '') { this._chart.set(await firstValueFrom(this.api.controlChart(id))); }
  }

  private async mutate<T>(id: string, call: () => Observable<T>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._selected.set(await firstValueFrom(this.api.getById(id)));
    });
  }

  private async run<T>(operation: () => Promise<T>): Promise<T | null> {
    this._loading.set(true);
    this._error.set('');
    try {
      return await operation();
    } catch (err) {
      this._error.set(this.describe(err));
      return null;
    } finally {
      this._loading.set(false);
    }
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return (err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`;
    }
    return 'Unexpected error.';
  }
}
