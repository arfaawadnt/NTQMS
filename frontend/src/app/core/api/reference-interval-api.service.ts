import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateReferenceIntervalStudyRequest, CreatedResource, ReferenceIntervalDetail, ReferenceIntervalListItem,
} from '../models';

/** Typed client for the reference-interval verification API (CLSI EP28). */
@Injectable({ providedIn: 'root' })
export class ReferenceIntervalApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/reference-interval-studies`;

  list(state?: string): Observable<ReferenceIntervalListItem[]> {
    return this.http.get<ReferenceIntervalListItem[]>(
      state ? `${this.base}?state=${encodeURIComponent(state)}` : this.base);
  }

  getById(id: string): Observable<ReferenceIntervalDetail> {
    return this.http.get<ReferenceIntervalDetail>(`${this.base}/${id}`);
  }

  create(body: CreateReferenceIntervalStudyRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  addSample(id: string, value: number, subjectRef: string | null): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/samples`, { value, subjectRef });
  }

  removeSample(id: string, sampleId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/samples/${sampleId}`);
  }

  calculate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/calculate`, {}); }

  signOff(id: string, body: { password: string; pin: string }): Observable<void> { return this.http.post<void>(`${this.base}/${id}/sign-off`, body); }
}
