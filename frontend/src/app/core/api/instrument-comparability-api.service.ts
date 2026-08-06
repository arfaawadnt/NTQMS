import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateInstrumentComparabilityRequest, CreatedResource,
  InstrumentComparabilityDetail, InstrumentComparabilityListItem,
} from '../models';

/** Typed client for the instrument-to-instrument comparability API. */
@Injectable({ providedIn: 'root' })
export class InstrumentComparabilityApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/instrument-comparabilities`;

  list(state?: string): Observable<InstrumentComparabilityListItem[]> {
    return this.http.get<InstrumentComparabilityListItem[]>(
      state ? `${this.base}?state=${encodeURIComponent(state)}` : this.base);
  }

  getById(id: string): Observable<InstrumentComparabilityDetail> {
    return this.http.get<InstrumentComparabilityDetail>(`${this.base}/${id}`);
  }

  create(body: CreateInstrumentComparabilityRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  addReading(id: string, instrument: string, sampleId: string, value: number): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/readings`, { instrument, sampleId, value });
  }

  removeReading(id: string, readingId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/readings/${readingId}`);
  }

  calculate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/calculate`, {}); }

  signOff(id: string, body: { password: string; pin: string }): Observable<void> { return this.http.post<void>(`${this.base}/${id}/sign-off`, body); }
}
