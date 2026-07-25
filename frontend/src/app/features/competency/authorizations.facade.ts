import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { AuthorizationsApiService } from '../../core/api/authorizations-api.service';
import {
  GrantTestAuthorizationRequest, TestAuthorizationDetail, TestAuthorizationListItem,
} from '../../core/models';

/** Signal-based facade for the personnel authorization matrix (ISO 17025 §6.2.6). */
@Injectable({ providedIn: 'root' })
export class AuthorizationsFacade {
  private readonly api = inject(AuthorizationsApiService);

  private readonly _list = signal<TestAuthorizationListItem[]>([]);
  private readonly _selected = signal<TestAuthorizationDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(userId?: string, status?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list(userId, status))));
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  async grant(request: GrantTestAuthorizationRequest): Promise<string | null> {
    const id = await this.run(async () => (await firstValueFrom(this.api.grant(request))).id);
    if (id) { await this.loadList(); }
    return id;
  }

  async suspend(id: string, reason: string): Promise<void> {
    await this.mutate(id, () => this.api.suspend(id, reason));
  }

  async reinstate(id: string): Promise<void> { await this.mutate(id, () => this.api.reinstate(id)); }

  async revoke(id: string, reason: string): Promise<void> {
    await this.mutate(id, () => this.api.revoke(id, reason));
  }

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
