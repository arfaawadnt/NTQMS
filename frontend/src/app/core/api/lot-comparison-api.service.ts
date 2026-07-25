import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateLotComparisonRequest, CreatedResource, LotComparisonDetail, LotComparisonListItem,
} from '../models';

/** Typed client for the reagent/control lot-to-lot comparison API. */
@Injectable({ providedIn: 'root' })
export class LotComparisonApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/lot-comparisons`;

  list(state?: string): Observable<LotComparisonListItem[]> {
    return this.http.get<LotComparisonListItem[]>(
      state ? `${this.base}?state=${encodeURIComponent(state)}` : this.base);
  }

  getById(id: string): Observable<LotComparisonDetail> {
    return this.http.get<LotComparisonDetail>(`${this.base}/${id}`);
  }

  create(body: CreateLotComparisonRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  addPair(id: string, currentLotValue: number, newLotValue: number, sampleId: string | null): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/pairs`, { currentLotValue, newLotValue, sampleId });
  }

  removePair(id: string, pairId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/pairs/${pairId}`);
  }

  calculate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/calculate`, {}); }

  signOff(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/sign-off`, {}); }
}
