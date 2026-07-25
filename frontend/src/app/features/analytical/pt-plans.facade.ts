import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { PtPlansApiService } from '../../core/api/pt-plans-api.service';
import { PtPlanDetail, PtPlanListItem } from '../../core/models';

/** Signal-based facade for the annual PT/EQA plan (ISO 17025 §7.7.2). */
@Injectable({ providedIn: 'root' })
export class PtPlansFacade {
  private readonly api = inject(PtPlansApiService);

  private readonly _list = signal<PtPlanListItem[]>([]);
  private readonly _selected = signal<PtPlanDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list())));
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  async create(year: number): Promise<string | null> {
    const id = await this.run(async () => (await firstValueFrom(this.api.create(year))).id);
    if (id) { await this.loadList(); }
    return id;
  }

  async addItem(id: string, body: {
    scheme: string; analyte: string; provider: string | null; plannedCycles: number; notes: string | null;
  }): Promise<void> {
    await this.mutate(id, () => this.api.addItem(id, body));
  }

  async removeItem(id: string, itemId: string): Promise<void> {
    await this.mutate(id, () => this.api.removeItem(id, itemId));
  }

  async approve(id: string): Promise<void> { await this.mutate(id, () => this.api.approve(id)); }

  async recordFulfilment(id: string, itemId: string, enrollmentId: string): Promise<void> {
    await this.mutate(id, () => this.api.recordFulfilment(id, itemId, enrollmentId));
  }

  async close(id: string, closureSummary: string): Promise<void> {
    await this.mutate(id, () => this.api.close(id, closureSummary));
  }

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
