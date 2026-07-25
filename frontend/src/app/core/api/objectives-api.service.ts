import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatedResource, DefineQualityObjectiveRequest, QualityObjectiveDetail, QualityObjectiveListItem,
} from '../models';

/** Typed client for the quality objectives API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class ObjectivesApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/quality-objectives`;

  list(status?: string): Observable<QualityObjectiveListItem[]> {
    return this.http.get<QualityObjectiveListItem[]>(
      status ? `${this.base}?status=${encodeURIComponent(status)}` : this.base);
  }

  getById(id: string): Observable<QualityObjectiveDetail> {
    return this.http.get<QualityObjectiveDetail>(`${this.base}/${id}`);
  }

  define(body: DefineQualityObjectiveRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  recordProgress(id: string, measuredOn: string, value: number, comment: string | null): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/progress`, { measuredOn, value, comment });
  }

  close(id: string, outcome: string, note: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/close`, { outcome, note });
  }
}
