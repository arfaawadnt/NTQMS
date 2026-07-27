import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AnswerChecklistItemRequest, AuditDetail, AuditListItem, CreatedResource,
  Paged, RaiseFindingRequest, ScheduleAuditRequest,
} from '../models';

/** Typed client for the Audit Management API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class AuditsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/audits`;

  list(status?: string): Observable<Paged<AuditListItem>> {
    return this.http.get<Paged<AuditListItem>>(status ? `${this.base}?status=${encodeURIComponent(status)}` : this.base);
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

  signOff(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/sign-off`, {}); }
}
