import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { CompetencyApiService } from '../../core/api/competency-api.service';
import {
  AssignCompetencyRequest, AssignTrainingRequest, CompetencyDetail, CompetencyListItem,
  TrainingAssignment,
} from '../../core/models';

/**
 * Signal-based facade for Competency & Training. Exposes the competency matrix,
 * the selected competency (with its assessment history), and the training queue,
 * plus the write operations that drive the authorize/revoke and training lifecycles.
 */
@Injectable({ providedIn: 'root' })
export class CompetencyFacade {
  private readonly api = inject(CompetencyApiService);

  private readonly _list = signal<CompetencyListItem[]>([]);
  private readonly _selected = signal<CompetencyDetail | null>(null);
  private readonly _training = signal<TrainingAssignment[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly training = this._training.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(status?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.listCompetencies(undefined, status))));
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getCompetency(id))));
  }

  async assignCompetency(request: AssignCompetencyRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.assignCompetency(request))).id);
  }

  async scoreAssessment(id: string, score: number): Promise<void> {
    await this.mutate(id, () => this.api.scoreAssessment(id, { score }));
  }

  async authorize(id: string): Promise<void> {
    await this.mutate(id, () => this.api.authorizeCompetency(id));
  }

  async revoke(id: string, reason: string): Promise<void> {
    await this.mutate(id, () => this.api.revokeCompetency(id, { reason }));
  }

  async loadTraining(includeCompleted: boolean): Promise<void> {
    await this.run(async () => this._training.set(await firstValueFrom(this.api.listTraining(undefined, includeCompleted))));
  }

  async assignTraining(request: AssignTrainingRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.assignTraining(request))).id);
  }

  /** Marks a training assignment complete and refreshes the queue keeping the current filter. */
  async completeTraining(id: string, includeCompleted: boolean): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.completeTraining(id));
      this._training.set(await firstValueFrom(this.api.listTraining(undefined, includeCompleted)));
    });
  }

  private async mutate(id: string, call: () => Observable<void>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._selected.set(await firstValueFrom(this.api.getCompetency(id)));
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
