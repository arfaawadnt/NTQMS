import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddPrecisionMeasurementRequest, BulkImportResult, CreatePrecisionSeriesRequest, CreatePrecisionStudyRequest,
  CreatedResource, PrecisionDetail, PrecisionListItem, PrecisionSeriesCreated,
} from '../models';

/** Typed client for the imprecision-study API (CLSI EP05 / EP15). */
@Injectable({ providedIn: 'root' })
export class PrecisionApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/precision-studies`;

  list(state?: string, analyte?: string): Observable<PrecisionListItem[]> {
    let params = new HttpParams();
    if (state) { params = params.set('state', state); }
    if (analyte) { params = params.set('analyte', analyte); }
    return this.http.get<PrecisionListItem[]>(this.base, { params });
  }

  getById(id: string): Observable<PrecisionDetail> {
    return this.http.get<PrecisionDetail>(`${this.base}/${id}`);
  }

  create(body: CreatePrecisionStudyRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  /** One linked study per concentration level. */
  createSeries(body: CreatePrecisionSeriesRequest): Observable<PrecisionSeriesCreated> {
    return this.http.post<PrecisionSeriesCreated>(`${this.base}/series`, body);
  }

  addMeasurement(id: string, body: AddPrecisionMeasurementRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/measurements`, body);
  }

  importMeasurements(id: string, rows: AddPrecisionMeasurementRequest[]): Observable<BulkImportResult> {
    return this.http.post<BulkImportResult>(`${this.base}/${id}/measurements/import`, { rows });
  }

  removeMeasurement(id: string, measurementId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/measurements/${measurementId}`);
  }

  calculate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/calculate`, {}); }

  signOff(id: string, body: { password: string; pin: string }): Observable<void> { return this.http.post<void>(`${this.base}/${id}/sign-off`, body); }
}
