import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AnalyticalApiService } from '../../core/api/analytical-api.service';
import { CreateQcProfileRequest, QcProfile, QcRun } from '../../core/models';

/**
 * Signal store for statistical QC: control profiles and their run history
 * (Westgard verdicts computed server-side). Runs arrive newest-first from the
 * API; `chartRuns` re-orders them oldest-first for Levey-Jennings plotting.
 */
@Injectable({ providedIn: 'root' })
export class QcFacade {
  private readonly api = inject(AnalyticalApiService);

  private readonly _profiles = signal<QcProfile[]>([]);
  private readonly _selected = signal<QcProfile | null>(null);
  private readonly _runs = signal<QcRun[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly profiles = this._profiles.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly runs = this._runs.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  /** Runs in chronological order for the Levey-Jennings chart. */
  readonly chartRuns = computed(() => [...this._runs()].reverse());

  async loadProfiles(): Promise<void> {
    await this.run(async () => this._profiles.set(await firstValueFrom(this.api.qcProfiles())));
  }

  /** Selects a profile (from the loaded list) and fetches its run history. */
  async openProfile(id: string): Promise<void> {
    await this.run(async () => {
      if (this._profiles().length === 0) {
        this._profiles.set(await firstValueFrom(this.api.qcProfiles()));
      }
      this._selected.set(this._profiles().find((p) => p.id === id) ?? null);
      this._runs.set(await firstValueFrom(this.api.qcRuns(id)));
    });
  }

  async createProfile(request: CreateQcProfileRequest): Promise<string | null> {
    return this.run(async () => {
      const id = (await firstValueFrom(this.api.createQcProfile(request))).id;
      this._profiles.set(await firstValueFrom(this.api.qcProfiles()));
      return id;
    });
  }

  async recordRun(profileId: string, value: number, operator: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.recordQcRun(profileId, { value, operator }));
      this._runs.set(await firstValueFrom(this.api.qcRuns(profileId)));
    });
  }

  async troubleshoot(profileId: string, runId: string, note: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.troubleshootRun(runId, { note }));
      this._runs.set(await firstValueFrom(this.api.qcRuns(profileId)));
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
