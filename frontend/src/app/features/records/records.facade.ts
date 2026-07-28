import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { ArchivesApiService } from '../../core/api/archives-api.service';
import { FilesApiService } from '../../core/api/files-api.service';
import { ArchiveListItem, Paged, RetentionClass } from '../../core/models';

/**
 * Signal store for Records & Retention: the archive register and its
 * archive → retrieve → return → dispose lifecycle. Snapshot files upload
 * first, then link by id. Disposal is refused server-side for permanent
 * retention or before the retention expiry (ARC-013/014).
 */
@Injectable({ providedIn: 'root' })
export class RecordsFacade {
  private readonly api = inject(ArchivesApiService);
  private readonly files = inject(FilesApiService);

  private readonly _list = signal<ArchiveListItem[]>([]);
  private readonly _total = signal(0);
  private readonly _hasMore = signal(false);
  private readonly _loading = signal(false);
  private readonly _error = signal('');
  /** 1-based page of the last fetched slice (R-3 load-more pager). */
  private readonly _page = signal(1);
  /** Active state filter, reused to refresh the list after every mutation. */
  private stateFilter?: string;

  readonly list = this._list.asReadonly();
  /** Total matching records on the server (pagination envelope, API-004). */
  readonly total = this._total.asReadonly();
  /** True when more pages exist beyond the loaded slice. */
  readonly hasMore = this._hasMore.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(state?: string): Promise<void> {
    this.stateFilter = state;
    await this.run(async () => this.applyFirstPage(await firstValueFrom(this.api.list(state))));
  }

  /** Appends the next page under the current filter (R-3); no-op while loading or exhausted. */
  async loadMore(): Promise<void> {
    if (this._loading() || !this._hasMore()) { return; }
    await this.run(async () => {
      const next = this._page() + 1;
      const page = await firstValueFrom(this.api.list(this.stateFilter, next));
      this._page.set(next);
      this._list.update((items) => [...items, ...page.items]);
      this._total.set(page.total);
      this._hasMore.set(page.hasMore);
    });
  }

  /**
   * Archives a record snapshot. A content snapshot is mandatory (F-14): the file
   * uploads first, then links by id — an archive with no immutable copy is refused.
   */
  async archive(sourceModule: string, sourceRef: string, retentionClass: RetentionClass, snapshot: File | null): Promise<boolean> {
    if (!snapshot) {
      this._error.set('A content snapshot file is required to archive a record.');
      return false;
    }

    return await this.run(async () => {
      const snapshotFileId = (await firstValueFrom(this.files.upload(snapshot))).id;
      await firstValueFrom(this.api.archive({ sourceModule, sourceRef, snapshotFileId, retentionClass }));
      this.applyFirstPage(await firstValueFrom(this.api.list(this.stateFilter)));
      return true;
    }) ?? false;
  }

  async retrieve(id: string): Promise<void> { await this.mutate(() => this.api.retrieve(id)); }

  async return(id: string): Promise<void> { await this.mutate(() => this.api.return(id)); }

  async dispose(id: string): Promise<void> { await this.mutate(() => this.api.dispose(id)); }

  async placeLegalHold(id: string, reason: string): Promise<void> {
    await this.mutate(() => this.api.placeLegalHold(id, reason));
  }

  async releaseLegalHold(id: string): Promise<void> { await this.mutate(() => this.api.releaseLegalHold(id)); }

  private async mutate(call: () => Observable<void>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this.applyFirstPage(await firstValueFrom(this.api.list(this.stateFilter)));
    });
  }

  /** Unwraps a first-page envelope into the list signals, resetting the pager. */
  private applyFirstPage(page: Paged<ArchiveListItem>): void {
    this._page.set(1);
    this._list.set(page.items);
    this._total.set(page.total);
    this._hasMore.set(page.hasMore);
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
