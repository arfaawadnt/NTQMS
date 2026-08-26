import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { InfectionControlApiService } from '../../core/api/infection-control-api.service';
import {
  DeviceExposureListItem, HaiCaseDetail, HaiCaseListItem, HaiRates,
  RecordDeviceExposureRequest, RemoveDeviceRequest, ReportHaiCaseRequest, ReviewHaiCaseRequest,
} from '../../core/models';

/**
 * Signal-based facade for Infection Prevention & Control (HQMS M09). Holds the HAI-case
 * register, the device-exposure register and the live device-associated rates; refreshes
 * after each write.
 */
@Injectable({ providedIn: 'root' })
export class InfectionControlFacade {
  private readonly api = inject(InfectionControlApiService);

  private readonly _cases = signal<HaiCaseListItem[]>([]);
  private readonly _devices = signal<DeviceExposureListItem[]>([]);
  private readonly _rates = signal<HaiRates | null>(null);
  private readonly _selected = signal<HaiCaseDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly cases = this._cases.asReadonly();
  readonly devices = this._devices.asReadonly();
  readonly rates = this._rates.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly devicesInPlace = computed(() => this._devices().filter((d) => d.status === 'InPlace').length);

  private lastType?: string;
  private lastStatus?: string;

  async loadAll(type?: string, status?: string): Promise<void> {
    this.lastType = type;
    this.lastStatus = status;
    await this.run(async () => {
      this._cases.set(await firstValueFrom(this.api.listCases(type, status)));
      this._devices.set(await firstValueFrom(this.api.listDevices()));
      this._rates.set(await firstValueFrom(this.api.rates(30)));
    });
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getCase(id))));
  }

  async reportCase(r: ReportHaiCaseRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.reportCase(r))).id);
  }

  async recordDevice(r: RecordDeviceExposureRequest): Promise<string | null> {
    const id = await this.run(async () => (await firstValueFrom(this.api.recordDevice(r))).id);
    if (id) { await this.loadAll(this.lastType, this.lastStatus); }
    return id;
  }

  async removeDevice(id: string, r: RemoveDeviceRequest): Promise<void> {
    await this.run(async () => { await firstValueFrom(this.api.removeDevice(id, r)); });
    if (this._error() === '') { await this.loadAll(this.lastType, this.lastStatus); }
  }

  async review(id: string, r: ReviewHaiCaseRequest): Promise<void> { await this.mutate(id, () => this.api.reviewCase(id, r)); }
  async close(id: string): Promise<void> { await this.mutate(id, () => this.api.closeCase(id)); }

  private async mutate<T>(id: string, call: () => Observable<T>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._selected.set(await firstValueFrom(this.api.getCase(id)));
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
