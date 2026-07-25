import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateMethodComparisonRequest, CreatedResource, MethodComparisonDetail, MethodComparisonListItem,
} from '../models';

/** Typed client for the method-comparison API (CLSI EP09). */
@Injectable({ providedIn: 'root' })
export class MethodComparisonApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/method-comparisons`;

  list(state?: string): Observable<MethodComparisonListItem[]> {
    return this.http.get<MethodComparisonListItem[]>(
      state ? `${this.base}?state=${encodeURIComponent(state)}` : this.base);
  }

  getById(id: string): Observable<MethodComparisonDetail> {
    return this.http.get<MethodComparisonDetail>(`${this.base}/${id}`);
  }

  create(body: CreateMethodComparisonRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  addPair(id: string, referenceValue: number, testValue: number, sampleId: string | null): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/pairs`, { referenceValue, testValue, sampleId });
  }

  removePair(id: string, pairId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/pairs/${pairId}`);
  }

  calculate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/calculate`, {}); }

  signOff(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/sign-off`, {}); }
}
