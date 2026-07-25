import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { OutlierApiService } from '../../core/api/outlier-api.service';
import { CreateOutlierScreeningRequest, OutlierScreeningDetail, OutlierScreeningListItem } from '../../core/models';

/** Signal-based facade for outlier screenings (Tukey fences + modified z-score). */
@Injectable({ providedIn: 'root' })
export class OutlierFacade {
  private readonly api = inject(OutlierApiService);

  private readonly _list = signal<OutlierScreeningListItem[]>([]);
  private readonly _selected = signal<OutlierScreeningDetail | null>(null);
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

  async create(request: CreateOutlierScreeningRequest): Promise<string | null> {
    const id = await this.run(async () => (await firstValueFrom(this.api.create(request))).id);
    if (id) { await this.loadList(); }
    return id;
  }

  async addPoint(id: string, value: number, label: string | null): Promise<void> {
    await this.mutate(id, () => this.api.addPoint(id, value, label));
  }

  async removePoint(id: string, pointId: string): Promise<void> {
    await this.mutate(id, () => this.api.removePoint(id, pointId));
  }

  async calculate(id: string): Promise<void> { await this.mutate(id, () => this.api.calculate(id)); }

  async signOff(id: string): Promise<void> { await this.mutate(id, () => this.api.signOff(id)); }

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
