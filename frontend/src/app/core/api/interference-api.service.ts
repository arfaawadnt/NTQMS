import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateInterferenceStudyRequest, CreatedResource, InterferenceDetail, InterferenceListItem,
} from '../models';

/** Typed client for the interference / analytical-specificity API (CLSI EP07). */
@Injectable({ providedIn: 'root' })
export class InterferenceApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/interference-studies`;

  list(state?: string): Observable<InterferenceListItem[]> {
    return this.http.get<InterferenceListItem[]>(
      state ? `${this.base}?state=${encodeURIComponent(state)}` : this.base);
  }

  getById(id: string): Observable<InterferenceDetail> {
    return this.http.get<InterferenceDetail>(`${this.base}/${id}`);
  }

  create(body: CreateInterferenceStudyRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  addMeasurement(id: string, kind: string, interferent: string | null, value: number): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/measurements`, { kind, interferent, value });
  }

  removeMeasurement(id: string, measurementId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/measurements/${measurementId}`);
  }

  calculate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/calculate`, {}); }

  signOff(id: string, body: { password: string; pin: string }): Observable<void> { return this.http.post<void>(`${this.base}/${id}/sign-off`, body); }
}
