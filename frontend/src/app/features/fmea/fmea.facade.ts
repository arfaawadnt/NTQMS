import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { FmeaApiService } from '../../core/api/fmea-api.service';
import {
  AddFailureModeRequest, CreateFmeaRequest, FmeaDetail, FmeaListItem, RecommendActionRequest, RecordResidualRequest,
} from '../../core/models';

/**
 * Signal-based facade for FMEA / HFMEA (HQMS M04). Holds the register and the loaded
 * worksheet, refreshing the detail after every write so RPNs stay current.
 */
@Injectable({ providedIn: 'root' })
export class FmeaFacade {
  private readonly api = inject(FmeaApiService);

  private readonly _list = signal<FmeaListItem[]>([]);
  private readonly _selected = signal<FmeaDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly activeCount = computed(() => this._list().filter((f) => f.status === 'Active').length);

  async loadList(status?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list(status))));
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  async create(request: CreateFmeaRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.create(request))).id);
  }

  async addFailureMode(id: string, r: AddFailureModeRequest): Promise<void> { await this.refresh(id, () => this.api.addFailureMode(id, r)); }
  async recommend(id: string, modeId: string, r: RecommendActionRequest): Promise<void> { await this.refresh(id, () => this.api.recommend(id, modeId, r)); }
  async residual(id: string, modeId: string, r: RecordResidualRequest): Promise<void> { await this.refresh(id, () => this.api.residual(id, modeId, r)); }
  async activate(id: string): Promise<void> { await this.refresh(id, () => this.api.activate(id)); }
  async close(id: string): Promise<void> { await this.refresh(id, () => this.api.close(id)); }

  private async refresh<T>(id: string, call: () => Observable<T>): Promise<void> {
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
