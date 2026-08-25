import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatedResource, ReportFallRequest, ReportPressureInjuryRequest, ReviewSafetyEventRequest,
  SafetyEventDetail, SafetyEventListItem, SafetyRates,
} from '../models';

/** Typed client for the Patient Safety API (HQMS M08). */
@Injectable({ providedIn: 'root' })
export class PatientSafetyApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/patient-safety`;

  list(type?: string, status?: string): Observable<SafetyEventListItem[]> {
    const params = new URLSearchParams();
    if (type) { params.set('type', type); }
    if (status) { params.set('status', status); }
    return this.http.get<SafetyEventListItem[]>(`${this.base}/events?${params.toString()}`);
  }

  getById(id: string): Observable<SafetyEventDetail> { return this.http.get<SafetyEventDetail>(`${this.base}/events/${id}`); }
  rates(windowDays = 30): Observable<SafetyRates> { return this.http.get<SafetyRates>(`${this.base}/rates?windowDays=${windowDays}`); }
  reportFall(body: ReportFallRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/falls`, body); }
  reportPressureInjury(body: ReportPressureInjuryRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/pressure-injuries`, body); }
  review(id: string, body: ReviewSafetyEventRequest): Observable<void> { return this.http.post<void>(`${this.base}/events/${id}/review`, body); }
  close(id: string): Observable<void> { return this.http.post<void>(`${this.base}/events/${id}/close`, {}); }
}
