import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { EquipmentApiService } from '../../core/api/equipment-api.service';
import { FilesApiService } from '../../core/api/files-api.service';
import {
  ActionSafetyNoticeRequest, EndDowntimeRequest, EquipmentDetail, EquipmentListItem, LogMaintenanceRequest,
  LogSafetyNoticeRequest, RecordIntermediateCheckRequest, RegisterEquipmentRequest, StartDowntimeRequest,
} from '../../core/models';

/** Signal-based facade for Equipment & Calibration, including certificate upload. */
@Injectable({ providedIn: 'root' })
export class EquipmentFacade {
  private readonly api = inject(EquipmentApiService);
  private readonly files = inject(FilesApiService);

  private readonly _list = signal<EquipmentListItem[]>([]);
  private readonly _total = signal(0);
  private readonly _hasMore = signal(false);
  private readonly _selected = signal<EquipmentDetail | null>(null);
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

  downloadUrl(fileId: string): string { return this.files.downloadUrl(fileId); }

  /** Authenticated certificate download (a bare anchor would 401). */
  async downloadCertificate(fileId: string, fallbackName: string): Promise<void> {
    await this.files.download(fileId, fallbackName);
  }

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

  async register(request: RegisterEquipmentRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.register(request))).id);
  }

  /** Logs a calibration, uploading the certificate first when one is provided. */
  async logCalibration(id: string, performedAt: string, provider: string, result: string, certificate: File | null): Promise<void> {
    await this.run(async () => {
      const certificateFileId = certificate ? (await firstValueFrom(this.files.upload(certificate))).id : null;
      await firstValueFrom(this.api.logCalibration(id, { performedAt, provider, result, certificateFileId }));
      this._selected.set(await firstValueFrom(this.api.getById(id)));
    });
  }

  /** Logs maintenance, uploading the certificate first when one is provided. */
  async logMaintenance(id: string, performedAt: string, workDescription: string, certificate: File | null): Promise<void> {
    await this.run(async () => {
      const certificateFileId = certificate ? (await firstValueFrom(this.files.upload(certificate))).id : null;
      await firstValueFrom(this.api.logMaintenance(id, { performedAt, workDescription, certificateFileId }));
      this._selected.set(await firstValueFrom(this.api.getById(id)));
    });
  }

  async recordCheck(id: string, request: RecordIntermediateCheckRequest): Promise<void> {
    await this.mutate(id, () => this.api.recordCheck(id, request));
  }

  async retire(id: string): Promise<void> { await this.mutate(id, () => this.api.retire(id)); }

  // ── Downtime & safety notices (HQMS M14) ────────────────────────────────────
  async startDowntime(id: string, r: StartDowntimeRequest): Promise<void> { await this.mutate(id, () => this.api.startDowntime(id, r)); }
  async endDowntime(id: string, downtimeId: string, r: EndDowntimeRequest): Promise<void> { await this.mutate(id, () => this.api.endDowntime(id, downtimeId, r)); }
  async logSafetyNotice(id: string, r: LogSafetyNoticeRequest): Promise<void> { await this.mutate(id, () => this.api.logSafetyNotice(id, r)); }
  async actionSafetyNotice(id: string, noticeId: string, r: ActionSafetyNoticeRequest): Promise<void> { await this.mutate(id, () => this.api.actionSafetyNotice(id, noticeId, r)); }
  async closeSafetyNotice(id: string, noticeId: string): Promise<void> { await this.mutate(id, () => this.api.closeSafetyNotice(id, noticeId)); }

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
