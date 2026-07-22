import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ConfirmEffectivenessRequest, CreatedResource, NcDetail, NcListItem,
  PlanCapaActionRequest, RaiseNcRequest, RecordRcaRequest, RejectNcRequest,
  TriageNcRequest, VerifyNcRequest,
} from '../models';

/**
 * Typed client for the Nonconformances / CAPA API. One method per backend
 * endpoint (12 total); no client-side business logic — the aggregate's state
 * machine lives on the server.
 */
@Injectable({ providedIn: 'root' })
export class NcApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/nonconformances`;

  list(status?: string, search?: string): Observable<NcListItem[]> {
    const params = new URLSearchParams();
    if (status) { params.set('status', status); }
    if (search) { params.set('search', search); }
    const query = params.toString();
    return this.http.get<NcListItem[]>(query ? `${this.base}?${query}` : this.base);
  }

  getById(id: string): Observable<NcDetail> {
    return this.http.get<NcDetail>(`${this.base}/${id}`);
  }

  raise(body: RaiseNcRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  submit(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/submit`, {});
  }

  triage(id: string, body: TriageNcRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/triage`, body);
  }

  reject(id: string, body: RejectNcRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/reject`, body);
  }

  recordRca(id: string, body: RecordRcaRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/rca`, body);
  }

  planAction(id: string, body: PlanCapaActionRequest): Observable<{ actionId: string }> {
    return this.http.post<{ actionId: string }>(`${this.base}/${id}/actions`, body);
  }

  completeAction(id: string, actionId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/actions/${actionId}/complete`, {});
  }

  submitForVerification(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/submit-verification`, {});
  }

  verify(id: string, body: VerifyNcRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/verify`, body);
  }

  confirmEffectiveness(id: string, body: ConfirmEffectivenessRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/confirm-effectiveness`, body);
  }
}
