import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateDetectionLimitStudyRequest, CreatedResource, DetectionLimitDetail, DetectionLimitListItem,
} from '../models';

/** Typed client for the LoB/LoD/LoQ API (CLSI EP17). */
@Injectable({ providedIn: 'root' })
export class DetectionLimitApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/detection-limit-studies`;

  list(state?: string): Observable<DetectionLimitListItem[]> {
    return this.http.get<DetectionLimitListItem[]>(
      state ? `${this.base}?state=${encodeURIComponent(state)}` : this.base);
  }

  getById(id: string): Observable<DetectionLimitDetail> {
    return this.http.get<DetectionLimitDetail>(`${this.base}/${id}`);
  }

  create(body: CreateDetectionLimitStudyRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  addMeasurement(id: string, kind: string, assignedValue: number | null, measuredValue: number): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/measurements`, { kind, assignedValue, measuredValue });
  }

  removeMeasurement(id: string, measurementId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/measurements/${measurementId}`);
  }

  calculate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/calculate`, {}); }

  signOff(id: string, body: { password: string; pin: string }): Observable<void> { return this.http.post<void>(`${this.base}/${id}/sign-off`, body); }
}
