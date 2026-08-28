import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddPlannedAuditRequest, AuditProgramDetail, AuditProgramListItem, CompletePlannedAuditRequest,
  CreateAuditProgramRequest, CreatedResource, LinkScheduledAuditRequest,
} from '../models';

/**
 * Typed client for the annual audit programme API (HQMS M05). One method per backend
 * endpoint; coverage is computed server-side.
 */
@Injectable({ providedIn: 'root' })
export class AuditProgramsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/audit-programs`;

  list(status?: string): Observable<AuditProgramListItem[]> {
    let params = new HttpParams();
    if (status) { params = params.set('status', status); }
    return this.http.get<AuditProgramListItem[]>(this.base, { params });
  }

  getById(id: string): Observable<AuditProgramDetail> { return this.http.get<AuditProgramDetail>(`${this.base}/${id}`); }
  create(body: CreateAuditProgramRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(this.base, body); }
  addPlanned(id: string, body: AddPlannedAuditRequest): Observable<{ plannedId: string }> { return this.http.post<{ plannedId: string }>(`${this.base}/${id}/plan`, body); }
  activate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/activate`, {}); }
  schedule(id: string, plannedId: string, body: LinkScheduledAuditRequest): Observable<void> { return this.http.post<void>(`${this.base}/${id}/plan/${plannedId}/schedule`, body); }
  complete(id: string, plannedId: string, body: CompletePlannedAuditRequest): Observable<void> { return this.http.post<void>(`${this.base}/${id}/plan/${plannedId}/complete`, body); }
  close(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/close`, {}); }
}
