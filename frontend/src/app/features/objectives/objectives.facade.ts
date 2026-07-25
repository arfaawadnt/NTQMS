import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { ObjectivesApiService } from '../../core/api/objectives-api.service';
import {
  DefineQualityObjectiveRequest, QualityObjectiveDetail, QualityObjectiveListItem,
} from '../../core/models';

/** Signal-based facade for quality objectives & targets (ISO 9001 §6.2). */
@Injectable({ providedIn: 'root' })
export class ObjectivesFacade {
  private readonly api = inject(ObjectivesApiService);

  private readonly _list = signal<QualityObjectiveListItem[]>([]);
  private readonly _selected = signal<QualityObjectiveDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(status?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list(status))));
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  async define(request: DefineQualityObjectiveRequest): Promise<string | null> {
    const id = await this.run(async () => (await firstValueFrom(this.api.define(request))).id);
    if (id) { await this.loadList(); }
    return id;
  }

  async recordProgress(id: string, measuredOn: string, value: number, comment: string | null): Promise<void> {
    await this.mutate(id, () => this.api.recordProgress(id, measuredOn, value, comment));
  }

  async close(id: string, outcome: string, note: string): Promise<void> {
    await this.mutate(id, () => this.api.close(id, outcome, note));
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
