import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { DocumentsApiService } from '../../core/api/documents-api.service';
import { FilesApiService } from '../../core/api/files-api.service';
import {
  CreateDocumentRequest, DocumentCompliance, DocumentDetail, DocumentListItem, DraftNewVersionRequest,
  PublishDocumentRequest, ReadAndUnderstand, RejectVersionRequest, SetReadAndUnderstandRequest, VersionBump,
} from '../../core/models';

/**
 * Signal-based facade for Document Control. Owns list/detail state, loading and
 * error, and orchestrates the upload-then-persist flows (create, new version).
 */
@Injectable({ providedIn: 'root' })
export class DocumentsFacade {
  private readonly api = inject(DocumentsApiService);
  private readonly files = inject(FilesApiService);

  private readonly _list = signal<DocumentListItem[]>([]);
  private readonly _total = signal(0);
  private readonly _hasMore = signal(false);
  private readonly _selected = signal<DocumentDetail | null>(null);
  private readonly _readAndUnderstand = signal<ReadAndUnderstand | null>(null);
  private readonly _compliance = signal<DocumentCompliance | null>(null);
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
  /** The document's read-and-understand distribution (mandatory flag + audience). */
  readonly readAndUnderstand = this._readAndUnderstand.asReadonly();
  /** Read-and-understand compliance for the loaded document (who is expected to read, and who has). */
  readonly compliance = this._compliance.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  downloadUrl(fileId: string): string { return this.files.downloadUrl(fileId); }

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
      const page = await firstValueFrom(this.api.list(this.lastStatus, undefined, next));
      this._page.set(next);
      this._list.update((items) => [...items, ...page.items]);
      this._total.set(page.total);
      this._hasMore.set(page.hasMore);
    });
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  /** Loads the read-and-understand distribution config for the document. */
  async loadReadAndUnderstand(id: string): Promise<void> {
    const ru = await this.run(() => firstValueFrom(this.api.readAndUnderstand(id)));
    if (ru) { this._readAndUnderstand.set(ru); }
  }

  /** Loads read-and-understand compliance (audience vs acknowledgements) for the document. */
  async loadCompliance(id: string): Promise<void> {
    const c = await this.run(() => firstValueFrom(this.api.compliance(id)));
    if (c) { this._compliance.set(c); }
  }

  /** Saves the distribution config, then refreshes both the config and the compliance view. */
  async setReadAndUnderstand(id: string, r: SetReadAndUnderstandRequest): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.setReadAndUnderstand(id, r));
      this._readAndUnderstand.set(await firstValueFrom(this.api.readAndUnderstand(id)));
      this._compliance.set(await firstValueFrom(this.api.compliance(id)));
    });
  }

  /** Uploads the file, then creates the document at v1.0; returns the new id or null. */
  async create(file: File, meta: Omit<CreateDocumentRequest, 'fileId'>): Promise<string | null> {
    return this.run(async () => {
      const uploaded = await firstValueFrom(this.files.upload(file));
      const created = await firstValueFrom(this.api.create({ ...meta, fileId: uploaded.id }));
      return created.id;
    });
  }

  /** Uploads a replacement file, then drafts a new version (major/minor). */
  async draftNewVersion(id: string, file: File, changeSummary: string, bump: VersionBump): Promise<void> {
    await this.run(async () => {
      const uploaded = await firstValueFrom(this.files.upload(file));
      await firstValueFrom(this.api.draftNewVersion(id, { fileId: uploaded.id, changeSummary, bump }));
      this._selected.set(await firstValueFrom(this.api.getById(id)));
    });
  }

  async submit(id: string): Promise<void> { await this.mutate(id, () => this.api.submit(id)); }
  async recommend(id: string): Promise<void> { await this.mutate(id, () => this.api.recommend(id)); }
  async reject(id: string, r: RejectVersionRequest): Promise<void> { await this.mutate(id, () => this.api.reject(id, r)); }
  async publish(id: string, r: PublishDocumentRequest): Promise<void> { await this.mutate(id, () => this.api.publish(id, r)); }
  async retire(id: string): Promise<void> { await this.mutate(id, () => this.api.retire(id)); }

  private async mutate(id: string, call: () => Observable<void>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._selected.set(await firstValueFrom(this.api.getById(id)));
    });
  }

  async confirmReview(id: string): Promise<void> {
    await this.mutate(id, () => this.api.confirmReview(id));
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
