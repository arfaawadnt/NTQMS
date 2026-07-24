import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AnalyticalApiService } from '../../core/api/analytical-api.service';
import { EnrollPtRequest, PtEnrollment, RecordPtResultRequest } from '../../core/models';

/**
 * Signal store for proficiency testing: enrollments per scheme/analyte/cycle
 * and result entry. The z-score and performance grade (Satisfactory /
 * Questionable / Unsatisfactory) are computed server-side; an unsatisfactory
 * grade raises a nonconformance automatically via the PT→NC saga.
 */
@Injectable({ providedIn: 'root' })
export class PtFacade {
  private readonly api = inject(AnalyticalApiService);

  private readonly _list = signal<PtEnrollment[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(performance?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.ptEnrollments(performance))));
  }

  async enroll(request: EnrollPtRequest, performanceFilter?: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.enrollPt(request));
      this._list.set(await firstValueFrom(this.api.ptEnrollments(performanceFilter)));
    });
  }

  async recordResult(id: string, request: RecordPtResultRequest, performanceFilter?: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.recordPtResult(id, request));
      this._list.set(await firstValueFrom(this.api.ptEnrollments(performanceFilter)));
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
