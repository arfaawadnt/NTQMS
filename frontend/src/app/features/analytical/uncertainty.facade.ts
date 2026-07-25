import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { UncertaintyApiService } from '../../core/api/uncertainty-api.service';
import {
  AddUncertaintyComponentRequest, CreateUncertaintyBudgetRequest,
  UncertaintyBudgetDetail, UncertaintyBudgetListItem,
} from '../../core/models';

/**
 * Signal store for measurement-uncertainty budgets: component entry →
 * calculate (u_c, U = k·u_c server-side) → QM approval freeze.
 */
@Injectable({ providedIn: 'root' })
export class UncertaintyFacade {
  private readonly api = inject(UncertaintyApiService);

  private readonly _list = signal<UncertaintyBudgetListItem[]>([]);
  private readonly _selected = signal<UncertaintyBudgetDetail | null>(null);
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

  async create(request: CreateUncertaintyBudgetRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.create(request))).id);
  }

  async addComponent(id: string, request: AddUncertaintyComponentRequest): Promise<void> {
    await this.mutate(id, () => this.api.addComponent(id, request));
  }

  async removeComponent(id: string, componentId: string): Promise<void> {
    await this.mutate(id, () => this.api.removeComponent(id, componentId));
  }

  async calculate(id: string): Promise<void> {
    await this.mutate(id, () => this.api.calculate(id));
  }

  async approve(id: string): Promise<void> {
    await this.mutate(id, () => this.api.approve(id));
  }

  private async mutate(id: string, call: () => Observable<unknown>): Promise<void> {
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
