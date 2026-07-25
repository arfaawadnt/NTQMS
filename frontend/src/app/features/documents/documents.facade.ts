import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { DocumentsApiService } from '../../core/api/documents-api.service';
import { FilesApiService } from '../../core/api/files-api.service';
import {
  CreateDocumentRequest, DocumentDetail, DocumentListItem, DraftNewVersionRequest,
  PublishDocumentRequest, RejectVersionRequest, VersionBump,
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
  private readonly _selected = signal<DocumentDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  downloadUrl(fileId: string): string { return this.files.downloadUrl(fileId); }

  async loadList(status?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list(status))));
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
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
