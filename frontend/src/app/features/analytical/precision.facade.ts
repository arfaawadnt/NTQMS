import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { PrecisionApiService } from '../../core/api/precision-api.service';
import {
  AddPrecisionMeasurementRequest, BulkImportResult, CreatePrecisionSeriesRequest, CreatePrecisionStudyRequest,
  PrecisionDetail, PrecisionListItem,
} from '../../core/models';

/**
 * Signal-based facade for imprecision studies (CLSI EP05 establishment /
 * EP15 verification). Every statistic — variance components, CIs, the EP15
 * verification limits — is computed by the backend; this store only moves data.
 */
@Injectable({ providedIn: 'root' })
export class PrecisionFacade {
  private readonly api = inject(PrecisionApiService);

  private readonly _list = signal<PrecisionListItem[]>([]);
  private readonly _selected = signal<PrecisionDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(state?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list(state))));
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  async create(request: CreatePrecisionStudyRequest): Promise<string | null> {
    const id = await this.run(async () => (await firstValueFrom(this.api.create(request))).id);
    if (id) { await this.loadList(); }
    return id;
  }

  /** Creates one linked study per level; returns the first level's id to open. */
  async createSeries(request: CreatePrecisionSeriesRequest): Promise<string | null> {
    const id = await this.run(async () => (await firstValueFrom(this.api.createSeries(request))).studyIds[0] ?? null);
    if (id) { await this.loadList(); }
    return id;
  }

  async addMeasurement(id: string, request: AddPrecisionMeasurementRequest): Promise<void> {
    await this.mutate(id, () => this.api.addMeasurement(id, request));
  }

  async removeMeasurement(id: string, measurementId: string): Promise<void> {
    await this.mutate(id, () => this.api.removeMeasurement(id, measurementId));
  }

  async importMeasurements(id: string, rows: AddPrecisionMeasurementRequest[]): Promise<BulkImportResult | null> {
    const result = await this.run(async () => {
      const r = await firstValueFrom(this.api.importMeasurements(id, rows));
      this._selected.set(await firstValueFrom(this.api.getById(id)));
      return r;
    });
    return result;
  }

  async calculate(id: string): Promise<void> { await this.mutate(id, () => this.api.calculate(id)); }

  async signOff(id: string, credentials: { password: string; pin: string }): Promise<void> { await this.mutate(id, () => this.api.signOff(id, credentials)); }

  private async mutate(id: string, call: () => Observable<void>): Promise<void> {
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
