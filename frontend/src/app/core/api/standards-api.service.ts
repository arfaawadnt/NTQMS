import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddStandardElementRequest, AssessElementRequest, CreatedResource, DefineStandardSetRequest, EvidenceLink,
  GapItem, LinkEvidenceRequest, ReadinessDashboard, StandardSetDetail, StandardSetListItem,
} from '../models';

/**
 * Typed client for the Accreditation &amp; Standards Compliance API (HQMS M07). One method
 * per backend endpoint; the readiness, gap-analysis and evidence rollups are computed server-side.
 */
@Injectable({ providedIn: 'root' })
export class StandardsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/standards`;

  list(status?: string): Observable<StandardSetListItem[]> {
    let params = new HttpParams();
    if (status) { params = params.set('status', status); }
    return this.http.get<StandardSetListItem[]>(this.base, { params });
  }

  getById(id: string): Observable<StandardSetDetail> { return this.http.get<StandardSetDetail>(`${this.base}/${id}`); }
  readiness(id: string): Observable<ReadinessDashboard> { return this.http.get<ReadinessDashboard>(`${this.base}/${id}/readiness`); }
  gapAnalysis(id: string): Observable<GapItem[]> { return this.http.get<GapItem[]>(`${this.base}/${id}/gap-analysis`); }
  elementEvidence(elementId: string): Observable<EvidenceLink[]> { return this.http.get<EvidenceLink[]>(`${this.base}/elements/${elementId}/evidence`); }

  define(body: DefineStandardSetRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(this.base, body); }
  addElement(id: string, body: AddStandardElementRequest): Observable<{ elementId: string }> { return this.http.post<{ elementId: string }>(`${this.base}/${id}/elements`, body); }
  activate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/activate`, {}); }
  archive(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/archive`, {}); }
  assess(id: string, elementId: string, body: AssessElementRequest): Observable<void> { return this.http.post<void>(`${this.base}/${id}/elements/${elementId}/assess`, body); }
  linkEvidence(id: string, body: LinkEvidenceRequest): Observable<{ evidenceId: string }> { return this.http.post<{ evidenceId: string }>(`${this.base}/${id}/evidence`, body); }
}
