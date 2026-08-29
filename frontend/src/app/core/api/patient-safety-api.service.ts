import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatedResource, DEFAULT_PAGE_SIZE, Paged, ReportFallRequest, ReportPressureInjuryRequest,
  ReviewSafetyEventRequest, SafetyEventDetail, SafetyEventListItem, SafetyRates,
} from '../models';

/** Typed client for the Patient Safety API (HQMS M08). */
@Injectable({ providedIn: 'root' })
export class PatientSafetyApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/patient-safety`;

  list(type?: string, status?: string, page = 1, pageSize = DEFAULT_PAGE_SIZE): Observable<Paged<SafetyEventListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (type) { params = params.set('type', type); }
    if (status) { params = params.set('status', status); }
    return this.http.get<Paged<SafetyEventListItem>>(`${this.base}/events`, { params });
  }

  getById(id: string): Observable<SafetyEventDetail> { return this.http.get<SafetyEventDetail>(`${this.base}/events/${id}`); }
  rates(windowDays = 30): Observable<SafetyRates> {
    return this.http.get<SafetyRates>(`${this.base}/rates`, { params: new HttpParams().set('windowDays', windowDays) });
  }
  reportFall(body: ReportFallRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/falls`, body); }
  reportPressureInjury(body: ReportPressureInjuryRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/pressure-injuries`, body); }
  review(id: string, body: ReviewSafetyEventRequest): Observable<void> { return this.http.post<void>(`${this.base}/events/${id}/review`, body); }
  close(id: string): Observable<void> { return this.http.post<void>(`${this.base}/events/${id}/close`, {}); }
}
