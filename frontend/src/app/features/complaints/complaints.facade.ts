import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { ComplaintsApiService } from '../../core/api/complaints-api.service';
import { ComplaintDetail, ComplaintListItem, LogComplaintRequest } from '../../core/models';

/**
 * Signal store for the complaints registry: log → acknowledge → validate
 * (justified spawns an NC via the backend saga) → investigate → outcome →
 * resolve → close, with closure blocked while the linked NC is open (CMP-020).
 */
@Injectable({ providedIn: 'root' })
export class ComplaintsFacade {
  private readonly api = inject(ComplaintsApiService);

  private readonly _list = signal<ComplaintListItem[]>([]);
  private readonly _selected = signal<ComplaintDetail | null>(null);
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

  async log(request: LogComplaintRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.log(request))).id);
  }

  async acknowledge(id: string): Promise<void> {
    await this.mutate(id, () => this.api.acknowledge(id));
  }

  async validate(id: string, justified: boolean, reason: string): Promise<void> {
    await this.mutate(id, () => this.api.validate(id, { justified, reason }));
  }

  async startInvestigation(id: string): Promise<void> {
    await this.mutate(id, () => this.api.startInvestigation(id));
  }

  async logOutcome(id: string, outcome: string): Promise<void> {
    await this.mutate(id, () => this.api.logOutcome(id, { outcome }));
  }

  async resolve(id: string, resolution: string): Promise<void> {
    await this.mutate(id, () => this.api.resolve(id, { resolution }));
  }

  async close(id: string): Promise<void> {
    await this.mutate(id, () => this.api.close(id));
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
