import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { ReferenceStandardsApiService } from '../../core/api/reference-standards-api.service';
import {
  ReferenceStandardDetail, ReferenceStandardListItem, RegisterReferenceStandardRequest,
} from '../../core/models';

/** Signal-based facade for the reference standard / CRM register (ISO 17025 §6.5). */
@Injectable({ providedIn: 'root' })
export class StandardsFacade {
  private readonly api = inject(ReferenceStandardsApiService);

  private readonly _list = signal<ReferenceStandardListItem[]>([]);
  private readonly _selected = signal<ReferenceStandardDetail | null>(null);
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

  async register(request: RegisterReferenceStandardRequest): Promise<string | null> {
    const id = await this.run(async () => (await firstValueFrom(this.api.register(request))).id);
    if (id) { await this.loadList(); }
    return id;
  }

  async quarantine(id: string, reason: string): Promise<void> {
    await this.mutate(id, () => this.api.quarantine(id, reason));
  }

  async reactivate(id: string): Promise<void> { await this.mutate(id, () => this.api.reactivate(id)); }

  async retire(id: string): Promise<void> { await this.mutate(id, () => this.api.retire(id)); }

  private async mutate(id: string, call: () => Observable<void>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._selected.set(await firstValueFrom(this.api.getById(id)));
      this._list.set(await firstValueFrom(this.api.list()));
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
