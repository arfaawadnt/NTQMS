import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { MethodComparisonApiService } from '../../core/api/method-comparison-api.service';
import {
  BulkImportResult, CreateMethodComparisonRequest, MethodComparisonDetail, MethodComparisonListItem,
} from '../../core/models';

/** Signal-based facade for method-comparison studies (CLSI EP09). */
@Injectable({ providedIn: 'root' })
export class MethodComparisonFacade {
  private readonly api = inject(MethodComparisonApiService);

  private readonly _list = signal<MethodComparisonListItem[]>([]);
  private readonly _selected = signal<MethodComparisonDetail | null>(null);
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

  async create(request: CreateMethodComparisonRequest): Promise<string | null> {
    const id = await this.run(async () => (await firstValueFrom(this.api.create(request))).id);
    if (id) { await this.loadList(); }
    return id;
  }

  async addPair(id: string, referenceValue: number, testValue: number, sampleId: string | null): Promise<void> {
    await this.mutate(id, () => this.api.addPair(id, referenceValue, testValue, sampleId));
  }

  async removePair(id: string, pairId: string): Promise<void> {
    await this.mutate(id, () => this.api.removePair(id, pairId));
  }

  async importPairs(id: string, rows: { referenceValue: number; testValue: number; sampleId: string | null }[]): Promise<BulkImportResult | null> {
    const result = await this.run(async () => {
      const r = await firstValueFrom(this.api.importPairs(id, rows));
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
