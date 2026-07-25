import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatedResource, MonitoringPointDetail, MonitoringPointListItem, RegisterMonitoringPointRequest,
} from '../models';

/** Typed client for the environmental monitoring API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class MonitoringApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/monitoring-points`;

  list(status?: string): Observable<MonitoringPointListItem[]> {
    return this.http.get<MonitoringPointListItem[]>(
      status ? `${this.base}?status=${encodeURIComponent(status)}` : this.base);
  }

  getById(id: string): Observable<MonitoringPointDetail> {
    return this.http.get<MonitoringPointDetail>(`${this.base}/${id}`);
  }

  register(body: RegisterMonitoringPointRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  setLimits(id: string, lowLimit: number | null, highLimit: number | null): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/limits`, { lowLimit, highLimit });
  }

  recordReading(id: string, value: number, remark: string | null): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/readings`, { value, remark });
  }

  suspend(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/suspend`, {}); }

  resume(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/resume`, {}); }

  retire(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/retire`, {}); }
}
