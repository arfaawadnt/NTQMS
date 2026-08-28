import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { MortalityReviewApiService } from '../../core/api/mortality-review-api.service';
import {
  ClassifyMortalityRequest, CommitteeDiscussedRequest, ComplicationListItem, MortalityDetail,
  MortalityListItem, MortalityRates, ReportComplicationRequest, ReportMortalityRequest,
  ReviewComplicationRequest, SecondReviewRequest,
} from '../../core/models';

/**
 * Signal-based facade for Mortality, Morbidity & Peer Review (HQMS M10). Holds the mortality-review
 * register, the complication register, the live rates and the selected review; refreshes after each
 * write.
 */
@Injectable({ providedIn: 'root' })
export class MortalityReviewFacade {
  private readonly api = inject(MortalityReviewApiService);

  private readonly _reviews = signal<MortalityListItem[]>([]);
  private readonly _complications = signal<ComplicationListItem[]>([]);
  private readonly _rates = signal<MortalityRates | null>(null);
  private readonly _selected = signal<MortalityDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly reviews = this._reviews.asReadonly();
  readonly complications = this._complications.asReadonly();
  readonly rates = this._rates.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  private lastClassification?: string;
  private lastStatus?: string;

  readonly openReviews = computed(() => this._reviews().filter((r) => r.status !== 'Closed').length);

  async loadAll(classification?: string, status?: string): Promise<void> {
    this.lastClassification = classification;
    this.lastStatus = status;
    await this.run(async () => {
      this._reviews.set(await firstValueFrom(this.api.listReviews(classification, status)));
      this._complications.set(await firstValueFrom(this.api.listComplications()));
      this._rates.set(await firstValueFrom(this.api.rates(30)));
    });
  }

  async loadReview(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getReview(id))));
  }

  async reportReview(r: ReportMortalityRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.reportReview(r))).id);
  }

  async classify(id: string, r: ClassifyMortalityRequest): Promise<void> { await this.reviewMutate(id, () => this.api.classify(id, r)); }
  async secondReview(id: string, r: SecondReviewRequest): Promise<void> { await this.reviewMutate(id, () => this.api.secondReview(id, r)); }
  async committeeDiscussed(id: string, r: CommitteeDiscussedRequest): Promise<void> { await this.reviewMutate(id, () => this.api.committeeDiscussed(id, r)); }
  async closeReview(id: string): Promise<void> { await this.reviewMutate(id, () => this.api.closeReview(id)); }

  async reportComplication(r: ReportComplicationRequest): Promise<string | null> {
    const id = await this.run(async () => (await firstValueFrom(this.api.reportComplication(r))).id);
    if (id) { await this.reloadComplications(); }
    return id;
  }

  async reviewComplication(id: string, r: ReviewComplicationRequest): Promise<void> {
    await this.run(async () => { await firstValueFrom(this.api.reviewComplication(id, r)); });
    if (this._error() === '') { await this.reloadComplications(); }
  }

  async closeComplication(id: string): Promise<void> {
    await this.run(async () => { await firstValueFrom(this.api.closeComplication(id)); });
    if (this._error() === '') { await this.reloadComplications(); }
  }

  async rejectComplication(id: string, reason: string): Promise<void> {
    await this.run(async () => { await firstValueFrom(this.api.rejectComplication(id, { reason })); });
    if (this._error() === '') { await this.reloadComplications(); }
  }

  private async reloadComplications(): Promise<void> {
    this._complications.set(await firstValueFrom(this.api.listComplications()));
  }

  private async reviewMutate<T>(id: string, call: () => Observable<T>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._selected.set(await firstValueFrom(this.api.getReview(id)));
    });
    if (this._error() === '') {
      this._reviews.set(await firstValueFrom(this.api.listReviews(this.lastClassification, this.lastStatus)));
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
