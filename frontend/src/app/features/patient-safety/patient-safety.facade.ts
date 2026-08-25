import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { PatientSafetyApiService } from '../../core/api/patient-safety-api.service';
import {
  ReportFallRequest, ReportPressureInjuryRequest, ReviewSafetyEventRequest, SafetyEventDetail,
  SafetyEventListItem, SafetyRates,
} from '../../core/models';

/**
 * Signal-based facade for Patient Safety (HQMS M08). Holds the events register, the live
 * rates (per 1,000 patient-days), and the loaded event; refreshes after each write.
 */
@Injectable({ providedIn: 'root' })
export class PatientSafetyFacade {
  private readonly api = inject(PatientSafetyApiService);

  private readonly _list = signal<SafetyEventListItem[]>([]);
  private readonly _rates = signal<SafetyRates | null>(null);
  private readonly _selected = signal<SafetyEventDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly rates = this._rates.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly openCount = computed(() => this._list().filter((e) => e.status !== 'Closed').length);

  async loadList(type?: string, status?: string): Promise<void> {
    await this.run(async () => {
      this._list.set(await firstValueFrom(this.api.list(type, status)));
      this._rates.set(await firstValueFrom(this.api.rates(30)));
    });
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  async reportFall(r: ReportFallRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.reportFall(r))).id);
  }

  async reportPressureInjury(r: ReportPressureInjuryRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.reportPressureInjury(r))).id);
  }

  async review(id: string, r: ReviewSafetyEventRequest): Promise<void> { await this.mutate(id, () => this.api.review(id, r)); }
  async close(id: string): Promise<void> { await this.mutate(id, () => this.api.close(id)); }

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
