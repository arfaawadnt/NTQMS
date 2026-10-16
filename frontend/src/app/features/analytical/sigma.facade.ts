import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { SigmaApiService } from '../../core/api/sigma-api.service';
import { PrecisionApiService } from '../../core/api/precision-api.service';
import {
  CreateSigmaAssessmentRequest, PrecisionListItem, SigmaAssessmentDetail, SigmaAssessmentListItem,
} from '../../core/models';

/**
 * Signal-based facade for Six-Sigma assessments. The CV input can come from a
 * signed-off precision study's WITHIN-LAB CV (the repeatability CV would
 * overstate σ); the backend records the study as the source.
 */
@Injectable({ providedIn: 'root' })
export class SigmaFacade {
  private readonly api = inject(SigmaApiService);
  private readonly precisionApi = inject(PrecisionApiService);

  private readonly _list = signal<SigmaAssessmentListItem[]>([]);
  private readonly _selected = signal<SigmaAssessmentDetail | null>(null);
  private readonly _precisionCandidates = signal<PrecisionListItem[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  /** Signed-off precision studies of the assessment's analyte. */
  readonly precisionCandidates = this._precisionCandidates.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadPrecisionCandidates(analyte: string): Promise<void> {
    await this.run(async () =>
      this._precisionCandidates.set(await firstValueFrom(this.precisionApi.list('SignedOff', analyte))));
  }

  async usePrecisionStudy(id: string, precisionStudyId: string): Promise<void> {
    await this.mutate(id, () => this.api.usePrecisionStudy(id, precisionStudyId));
  }

  async loadList(state?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list(state))));
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  async create(request: CreateSigmaAssessmentRequest): Promise<string | null> {
    const id = await this.run(async () => (await firstValueFrom(this.api.create(request))).id);
    if (id) { await this.loadList(); }
    return id;
  }

  async updateInputs(id: string, tEa: number, bias: number, cv: number): Promise<void> {
    await this.mutate(id, () => this.api.updateInputs(id, tEa, bias, cv));
  }

  async signOff(id: string, credentials: { password: string; pin: string }): Promise<void> { await this.mutate(id, () => this.api.signOff(id, credentials)); }

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
