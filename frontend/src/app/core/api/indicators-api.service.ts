import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatedResource, DEFAULT_PAGE_SIZE, DefineIndicatorRequest, IndicatorControlChart, IndicatorDetail,
  IndicatorListItem, Paged, RecordMeasurementRequest, SetIndicatorTargetsRequest, UpdateIndicatorDefinitionRequest,
} from '../models';

/**
 * Typed client for the Quality Indicators API (HQMS M06). One method per backend
 * endpoint; the data dictionary, grading and SPC computation live on the server.
 */
@Injectable({ providedIn: 'root' })
export class IndicatorsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/indicators`;

  list(status?: string, search?: string, page = 1, pageSize = DEFAULT_PAGE_SIZE): Observable<Paged<IndicatorListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) { params = params.set('status', status); }
    if (search) { params = params.set('search', search); }
    return this.http.get<Paged<IndicatorListItem>>(this.base, { params });
  }

  getById(id: string): Observable<IndicatorDetail> {
    return this.http.get<IndicatorDetail>(`${this.base}/${id}`);
  }

  controlChart(id: string): Observable<IndicatorControlChart> {
    return this.http.get<IndicatorControlChart>(`${this.base}/${id}/control-chart`);
  }

  define(body: DefineIndicatorRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  update(id: string, body: UpdateIndicatorDefinitionRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}`, body);
  }

  setTargets(id: string, body: SetIndicatorTargetsRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/targets`, body);
  }

  recordMeasurement(id: string, body: RecordMeasurementRequest): Observable<{ measurementId: string }> {
    return this.http.post<{ measurementId: string }>(`${this.base}/${id}/measurements`, body);
  }

  retire(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/retire`, {});
  }
}
