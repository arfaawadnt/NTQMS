import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { AuditsApiService } from '../../core/api/audits-api.service';
import {
  AnswerChecklistItemRequest, AuditDetail, AuditListItem, RaiseFindingRequest, ScheduleAuditRequest,
} from '../../core/models';

/** Signal-based facade for Audit Management. */
@Injectable({ providedIn: 'root' })
export class AuditsFacade {
  private readonly api = inject(AuditsApiService);

  private readonly _list = signal<AuditListItem[]>([]);
  private readonly _selected = signal<AuditDetail | null>(null);
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

  async schedule(request: ScheduleAuditRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.schedule(request))).id);
  }

  async start(id: string): Promise<void> { await this.mutate(id, () => this.api.start(id)); }
  async answer(id: string, itemId: string, r: AnswerChecklistItemRequest): Promise<void> { await this.mutate(id, () => this.api.answer(id, itemId, r)); }
  async raiseFinding(id: string, r: RaiseFindingRequest): Promise<void> { await this.mutate(id, () => this.api.raiseFinding(id, r)); }
  async signOff(id: string): Promise<void> { await this.mutate(id, () => this.api.signOff(id)); }

  private async mutate<T>(id: string, call: () => Observable<T>): Promise<void> {
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
