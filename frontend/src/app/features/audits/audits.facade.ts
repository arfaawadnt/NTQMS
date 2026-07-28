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
  private readonly _total = signal(0);
  private readonly _hasMore = signal(false);
  private readonly _selected = signal<AuditDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');
  /** 1-based page of the last fetched slice (R-3 load-more pager). */
  private readonly _page = signal(1);
  /** Filter of the last loadList, reused verbatim by loadMore. */
  private lastStatus?: string;

  readonly list = this._list.asReadonly();
  /** Total matching records on the server (pagination envelope, API-004). */
  readonly total = this._total.asReadonly();
  /** True when more pages exist beyond the loaded slice. */
  readonly hasMore = this._hasMore.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(status?: string): Promise<void> {
    this.lastStatus = status;
    await this.run(async () => {
      const page = await firstValueFrom(this.api.list(status));
      this._page.set(1);
      this._list.set(page.items);
      this._total.set(page.total);
      this._hasMore.set(page.hasMore);
    });
  }

  /** Appends the next page under the current filter (R-3); no-op while loading or exhausted. */
  async loadMore(): Promise<void> {
    if (this._loading() || !this._hasMore()) { return; }
    await this.run(async () => {
      const next = this._page() + 1;
      const page = await firstValueFrom(this.api.list(this.lastStatus, next));
      this._page.set(next);
      this._list.update((items) => [...items, ...page.items]);
      this._total.set(page.total);
      this._hasMore.set(page.hasMore);
    });
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
