import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AnswerChecklistItemRequest, AuditDetail, AuditListItem, CreatedResource,
  DEFAULT_PAGE_SIZE, Paged, RaiseFindingRequest, ScheduleAuditRequest,
} from '../models';

/** Typed client for the Audit Management API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class AuditsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/audits`;

  list(status?: string, page = 1, pageSize = DEFAULT_PAGE_SIZE): Observable<Paged<AuditListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) { params = params.set('status', status); }
    return this.http.get<Paged<AuditListItem>>(this.base, { params });
  }

  getById(id: string): Observable<AuditDetail> {
    return this.http.get<AuditDetail>(`${this.base}/${id}`);
  }

  schedule(body: ScheduleAuditRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  start(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/start`, {}); }

  answer(id: string, itemId: string, body: AnswerChecklistItemRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/checklist/${itemId}/answer`, body);
  }

  raiseFinding(id: string, body: RaiseFindingRequest): Observable<{ findingId: string }> {
    return this.http.post<{ findingId: string }>(`${this.base}/${id}/findings`, body);
  }

  signOff(id: string, body: { password: string; pin: string }): Observable<void> { return this.http.post<void>(`${this.base}/${id}/sign-off`, body); }
}
