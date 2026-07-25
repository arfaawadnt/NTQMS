import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { EquipmentApiService } from '../../core/api/equipment-api.service';
import { FilesApiService } from '../../core/api/files-api.service';
import {
  EquipmentDetail, EquipmentListItem, LogMaintenanceRequest,
  RecordIntermediateCheckRequest, RegisterEquipmentRequest,
} from '../../core/models';

/** Signal-based facade for Equipment & Calibration, including certificate upload. */
@Injectable({ providedIn: 'root' })
export class EquipmentFacade {
  private readonly api = inject(EquipmentApiService);
  private readonly files = inject(FilesApiService);

  private readonly _list = signal<EquipmentListItem[]>([]);
  private readonly _selected = signal<EquipmentDetail | null>(null);
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

  async logMaintenance(id: string, request: LogMaintenanceRequest): Promise<void> {
    await this.mutate(id, () => this.api.logMaintenance(id, request));
  }

  async recordCheck(id: string, request: RecordIntermediateCheckRequest): Promise<void> {
    await this.mutate(id, () => this.api.recordCheck(id, request));
  }

  async retire(id: string): Promise<void> { await this.mutate(id, () => this.api.retire(id)); }

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
