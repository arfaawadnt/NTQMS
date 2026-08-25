import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { SurveysApiService } from '../../core/api/surveys-api.service';
import {
  AddSurveyQuestionRequest, CreateSurveyRequest, SubmitSurveyResponseRequest, SurveyDetail, SurveyListItem,
  SurveyResults,
} from '../../core/models';

/**
 * Signal-based facade for Patient Satisfaction Surveys (HQMS M11). Holds the register, the
 * loaded survey with its questions, and its scored results, refreshing after each write.
 */
@Injectable({ providedIn: 'root' })
export class SurveysFacade {
  private readonly api = inject(SurveysApiService);

  private readonly _list = signal<SurveyListItem[]>([]);
  private readonly _selected = signal<SurveyDetail | null>(null);
  private readonly _results = signal<SurveyResults | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly results = this._results.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly openCount = computed(() => this._list().filter((s) => s.status === 'Open').length);

  async loadList(status?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list(status))));
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => {
      this._selected.set(await firstValueFrom(this.api.getById(id)));
      this._results.set(await firstValueFrom(this.api.results(id)));
    });
  }

  async create(request: CreateSurveyRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.create(request))).id);
  }

  async addQuestion(id: string, r: AddSurveyQuestionRequest): Promise<void> { await this.refresh(id, () => this.api.addQuestion(id, r)); }
  async open(id: string): Promise<void> { await this.refresh(id, () => this.api.open(id)); }
  async close(id: string): Promise<void> { await this.refresh(id, () => this.api.close(id)); }
  async submitResponse(id: string, r: SubmitSurveyResponseRequest): Promise<void> { await this.refresh(id, () => this.api.submitResponse(id, r)); }

  private async refresh<T>(id: string, call: () => Observable<T>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._selected.set(await firstValueFrom(this.api.getById(id)));
      this._results.set(await firstValueFrom(this.api.results(id)));
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
