import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { ArchivesApiService } from '../../core/api/archives-api.service';
import { FilesApiService } from '../../core/api/files-api.service';
import { ArchiveListItem, RetentionClass } from '../../core/models';

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
  private readonly _loading = signal(false);
  private readonly _error = signal('');
  /** Active state filter, reused to refresh the list after every mutation. */
  private stateFilter?: string;

  readonly list = this._list.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(state?: string): Promise<void> {
    this.stateFilter = state;
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list(state))));
  }

  /** Archives a record snapshot, uploading the file first when one is provided. */
  async archive(sourceModule: string, sourceRef: string, retentionClass: RetentionClass, snapshot: File | null): Promise<boolean> {
    return await this.run(async () => {
      const snapshotFileId = snapshot ? (await firstValueFrom(this.files.upload(snapshot))).id : null;
      await firstValueFrom(this.api.archive({ sourceModule, sourceRef, snapshotFileId, retentionClass }));
      this._list.set(await firstValueFrom(this.api.list(this.stateFilter)));
      return true;
    }) ?? false;
  }

  async retrieve(id: string): Promise<void> { await this.mutate(() => this.api.retrieve(id)); }

  async return(id: string): Promise<void> { await this.mutate(() => this.api.return(id)); }

  async dispose(id: string): Promise<void> { await this.mutate(() => this.api.dispose(id)); }

  private async mutate(call: () => Observable<void>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._list.set(await firstValueFrom(this.api.list(this.stateFilter)));
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
