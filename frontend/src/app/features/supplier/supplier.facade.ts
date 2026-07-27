import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { SuppliersApiService } from '../../core/api/suppliers-api.service';
import { FilesApiService } from '../../core/api/files-api.service';
import {
  RecordEvaluationRequest, RegisterSupplierRequest, SupplierDetail, SupplierEvaluation,
  SupplierListItem,
} from '../../core/models';

/**
 * Signal-based facade for Supplier Quality: the approved-supplier register,
 * the selected supplier (certificates + evaluation history), and the
 * register → certify → approve → evaluate → suspend lifecycle. Certificate
 * files are uploaded first, then linked by id.
 */
@Injectable({ providedIn: 'root' })
export class SupplierFacade {
  private readonly api = inject(SuppliersApiService);
  private readonly files = inject(FilesApiService);

  private readonly _list = signal<SupplierListItem[]>([]);
  private readonly _total = signal(0);
  private readonly _hasMore = signal(false);
  private readonly _selected = signal<SupplierDetail | null>(null);
  private readonly _evaluations = signal<SupplierEvaluation[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  /** Total matching records on the server (pagination envelope, API-004). */
  readonly total = this._total.asReadonly();
  /** True when more pages exist beyond the loaded slice. */
  readonly hasMore = this._hasMore.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly evaluations = this._evaluations.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  downloadUrl(fileId: string): string { return this.files.downloadUrl(fileId); }

  async loadList(status?: string): Promise<void> {
    await this.run(async () => {
      const page = await firstValueFrom(this.api.list(status));
      this._list.set(page.items);
      this._total.set(page.total);
      this._hasMore.set(page.hasMore);
    });
  }

  /** Loads the supplier and its evaluation history together. */
  async loadDetail(id: string): Promise<void> {
    await this.run(async () => {
      this._selected.set(await firstValueFrom(this.api.getById(id)));
      this._evaluations.set(await firstValueFrom(this.api.evaluations(id)));
    });
  }

  async register(request: RegisterSupplierRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.register(request))).id);
  }

  /** Adds a certificate, uploading the file first when one is provided. */
  async addCertificate(id: string, certificateType: string, expiresAt: string, file: File | null): Promise<void> {
    await this.run(async () => {
      const fileId = file ? (await firstValueFrom(this.files.upload(file))).id : null;
      await firstValueFrom(this.api.addCertificate(id, { certificateType, expiresAt, fileId }));
      this._selected.set(await firstValueFrom(this.api.getById(id)));
    });
  }

  async approve(id: string): Promise<void> {
    await this.mutate(id, () => this.api.approve(id));
  }

  async suspend(id: string, reason: string): Promise<void> {
    await this.mutate(id, () => this.api.suspend(id, { reason }));
  }

  async recordEvaluation(id: string, request: RecordEvaluationRequest): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.recordEvaluation(id, request));
      this._evaluations.set(await firstValueFrom(this.api.evaluations(id)));
    });
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
