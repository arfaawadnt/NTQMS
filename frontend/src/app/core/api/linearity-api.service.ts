import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateLinearityStudyRequest, CreatedResource, LinearityDetail, LinearityListItem,
} from '../models';

/** Typed client for the linearity / AMR API (CLSI EP06). */
@Injectable({ providedIn: 'root' })
export class LinearityApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/linearity-studies`;

  list(state?: string): Observable<LinearityListItem[]> {
    return this.http.get<LinearityListItem[]>(
      state ? `${this.base}?state=${encodeURIComponent(state)}` : this.base);
  }

  getById(id: string): Observable<LinearityDetail> {
    return this.http.get<LinearityDetail>(`${this.base}/${id}`);
  }

  create(body: CreateLinearityStudyRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  addMeasurement(id: string, assignedValue: number, measuredValue: number): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/measurements`, { assignedValue, measuredValue });
  }

  removeMeasurement(id: string, measurementId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/measurements/${measurementId}`);
  }

  calculate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/calculate`, {}); }

  signOff(id: string, body: { password: string; pin: string }): Observable<void> { return this.http.post<void>(`${this.base}/${id}/sign-off`, body); }
}
