import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ReportsApiService } from '../../core/api/reports-api.service';
import { QualityAnalytics, QualityHealthWeight } from '../../core/models';

/**
 * Drives the Quality Analytics page: one fetch serves both the Quality
 * Statistics and the ISO/IEC 17025 §8.9.2 management-review framing, so the two
 * views can never disagree about a figure.
 *
 * The facade holds the branch/department selection so a reload re-applies the
 * current filter, and exposes the weighting separately because it is fetched
 * once and only re-read after an edit.
 */
@Injectable({ providedIn: 'root' })
export class QualityAnalyticsFacade {
  private readonly api = inject(ReportsApiService);

  private readonly _analytics = signal<QualityAnalytics | null>(null);
  private readonly _weights = signal<QualityHealthWeight[]>([]);
  private readonly _loading = signal(false);
  private readonly _saving = signal(false);
  private readonly _error = signal('');
  private readonly _branchId = signal('');
  private readonly _departmentId = signal('');

  readonly analytics = this._analytics.asReadonly();
  readonly weights = this._weights.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly saving = this._saving.asReadonly();
  readonly error = this._error.asReadonly();
  readonly branchId = this._branchId.asReadonly();
  readonly departmentId = this._departmentId.asReadonly();

  /** True once a narrowing filter is in force, for the "filtered" notice. */
  readonly filtered = computed(() => this._branchId() !== '' || this._departmentId() !== '');

  async load(): Promise<void> {
    await this.run(async () => {
      const analytics = await firstValueFrom(
        this.api.qualityAnalytics(this._branchId() || undefined, this._departmentId() || undefined));
      this._analytics.set(analytics);
    });
  }

  /** Applies a new scope and refetches; the server does the narrowing, not the browser. */
  async applyFilter(branchId: string, departmentId: string): Promise<void> {
    this._branchId.set(branchId);
    this._departmentId.set(departmentId);
    await this.load();
  }

  async clearFilter(): Promise<void> {
    await this.applyFilter('', '');
  }

  async loadWeights(): Promise<void> {
    await this.run(async () => {
      const profile = await firstValueFrom(this.api.qualityHealthProfile());
      this._weights.set(profile.weights);
    });
  }

  /**
   * Saves the weighting and refetches the analytics, because changing the
   * weighting changes the composite score the page is currently displaying.
   */
  async saveWeights(weights: QualityHealthWeight[], reason: string): Promise<boolean> {
    this._saving.set(true);
    try {
      const result = await this.run(async () => {
        await firstValueFrom(this.api.updateQualityHealthProfile(weights, reason));
        await this.loadWeights();
        await this.load();
        return true;
      });
      return result === true;
    } finally {
      this._saving.set(false);
    }
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
