import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { FeedbackApiService } from '../../core/api/feedback-api.service';
import { FeedbackDetail, FeedbackListItem, LogFeedbackRequest } from '../../core/models';

/** Signal-based facade for general feedback & satisfaction (ISO 17025 §8.6.2). */
@Injectable({ providedIn: 'root' })
export class FeedbackFacade {
  private readonly api = inject(FeedbackApiService);

  private readonly _list = signal<FeedbackListItem[]>([]);
  private readonly _selected = signal<FeedbackDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(status?: string, type?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list(status, type))));
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  async log(request: LogFeedbackRequest): Promise<string | null> {
    const id = await this.run(async () => (await firstValueFrom(this.api.log(request))).id);
    if (id) { await this.loadList(); }
    return id;
  }

  async review(id: string, reviewNotes: string): Promise<void> {
    await this.mutate(id, () => this.api.review(id, reviewNotes));
  }

  async close(id: string, actionSummary: string): Promise<void> {
    await this.mutate(id, () => this.api.close(id, actionSummary));
  }

  async escalate(id: string, complainantName: string, complainantContact: string | null): Promise<string | null> {
    const result = await this.run(async () => {
      const created = await firstValueFrom(this.api.escalate(id, complainantName, complainantContact));
      this._selected.set(await firstValueFrom(this.api.getById(id)));
      return created.complaintId;
    });
    return result;
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
