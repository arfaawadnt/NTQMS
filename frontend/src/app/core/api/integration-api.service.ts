import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { EndpointListItem, IntegrationMessage, PatientCensus, RegisterEndpointRequest } from '../models';

/** Typed client for the Integration & Interoperability API (HQMS M24). */
@Injectable({ providedIn: 'root' })
export class IntegrationApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/integration`;

  endpoints(): Observable<EndpointListItem[]> { return this.http.get<EndpointListItem[]>(`${this.base}/endpoints`); }
  messages(id: string, status?: string): Observable<IntegrationMessage[]> {
    const params = new URLSearchParams();
    if (status) { params.set('status', status); }
    return this.http.get<IntegrationMessage[]>(`${this.base}/endpoints/${id}/messages?${params.toString()}`);
  }
  census(windowDays = 30): Observable<PatientCensus> { return this.http.get<PatientCensus>(`${this.base}/census?windowDays=${windowDays}`); }
  register(body: RegisterEndpointRequest): Observable<{ id: string }> { return this.http.post<{ id: string }>(`${this.base}/endpoints`, body); }
  suspend(id: string): Observable<void> { return this.http.post<void>(`${this.base}/endpoints/${id}/suspend`, {}); }
  resume(id: string): Observable<void> { return this.http.post<void>(`${this.base}/endpoints/${id}/resume`, {}); }
}
