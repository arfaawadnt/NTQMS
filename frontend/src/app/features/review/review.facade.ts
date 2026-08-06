import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { ReviewApiService } from '../../core/api/review-api.service';
import { AddDecisionRequest, ReviewDetail, ReviewListItem, ScheduleReviewRequest } from '../../core/models';

/**
 * Signal-based facade for Management Reviews. Exposes the review list and the
 * selected review (with its decisions), and drives the schedule → add-decisions
 * → close-with-minutes lifecycle (ISO 9001 9.3).
 */
@Injectable({ providedIn: 'root' })
export class ReviewFacade {
  private readonly api = inject(ReviewApiService);

  private readonly _list = signal<ReviewListItem[]>([]);
  private readonly _total = signal(0);
  private readonly _hasMore = signal(false);
  private readonly _selected = signal<ReviewDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');
  /** 1-based page of the last fetched slice (R-3 load-more pager). */
  private readonly _page = signal(1);

  readonly list = this._list.asReadonly();
  /** Total matching records on the server (pagination envelope, API-004). */
  readonly total = this._total.asReadonly();
  /** True when more pages exist beyond the loaded slice. */
  readonly hasMore = this._hasMore.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(): Promise<void> {
    await this.run(async () => {
      const page = await firstValueFrom(this.api.list());
      this._page.set(1);
      this._list.set(page.items);
      this._total.set(page.total);
      this._hasMore.set(page.hasMore);
    });
  }

  /** Appends the next page (R-3); no-op while loading or exhausted. */
  async loadMore(): Promise<void> {
    if (this._loading() || !this._hasMore()) { return; }
    await this.run(async () => {
      const next = this._page() + 1;
      const page = await firstValueFrom(this.api.list(next));
      this._page.set(next);
      this._list.update((items) => [...items, ...page.items]);
      this._total.set(page.total);
      this._hasMore.set(page.hasMore);
    });
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  async schedule(request: ScheduleReviewRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.schedule(request))).id);
  }

  async addDecision(id: string, request: AddDecisionRequest): Promise<void> {
    await this.mutate(id, () => this.api.addDecision(id, request));
  }

  async close(id: string, minutes: string, credentials: { password: string; pin: string }): Promise<void> {
    await this.mutate(id, () => this.api.close(id, { minutes, ...credentials }));
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
