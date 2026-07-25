import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  BulkImportResult, CreatePrecisionStudyRequest, CreatedResource, PrecisionDetail, PrecisionListItem,
} from '../models';

/** Typed client for the imprecision-study API (CLSI EP05). */
@Injectable({ providedIn: 'root' })
export class PrecisionApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/precision-studies`;

  list(state?: string): Observable<PrecisionListItem[]> {
    return this.http.get<PrecisionListItem[]>(
      state ? `${this.base}?state=${encodeURIComponent(state)}` : this.base);
  }

  getById(id: string): Observable<PrecisionDetail> {
    return this.http.get<PrecisionDetail>(`${this.base}/${id}`);
  }

  create(body: CreatePrecisionStudyRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  addMeasurement(id: string, runLabel: string, value: number): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/measurements`, { runLabel, value });
  }

  importMeasurements(id: string, rows: { runLabel: string; value: number }[]): Observable<BulkImportResult> {
    return this.http.post<BulkImportResult>(`${this.base}/${id}/measurements/import`, { rows });
  }

  removeMeasurement(id: string, measurementId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/measurements/${measurementId}`);
  }

  calculate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/calculate`, {}); }

  signOff(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/sign-off`, {}); }
}
