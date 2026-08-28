import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddContributingFactorRequest, AddTimelineEntryRequest, AnonymousIncidentReceipt, CloseIncidentRequest,
  CreatedResource, DEFAULT_PAGE_SIZE, DeclareSentinelRequest, IncidentDetail, IncidentListItem, IncidentTracking,
  Paged, RecordInvestigationSummaryRequest, RejectIncidentRequest, ReportAnonymousIncidentRequest,
  ReportIncidentRequest, SignatureRecord, StartInvestigationRequest, TriageIncidentRequest,
} from '../models';

/**
 * Typed client for the Incident & Occurrence Reporting API (HQMS M02). One method
 * per backend endpoint; no client-side business logic — the aggregate's state
 * machine and Part 11 signing ceremonies live on the server.
 */
@Injectable({ providedIn: 'root' })
export class IncidentsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/incidents`;

  list(
    status?: string, search?: string, category?: string, sentinelOnly = false,
    page = 1, pageSize = DEFAULT_PAGE_SIZE,
  ): Observable<Paged<IncidentListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) { params = params.set('status', status); }
    if (search) { params = params.set('search', search); }
    if (category) { params = params.set('category', category); }
    if (sentinelOnly) { params = params.set('sentinelOnly', true); }
    return this.http.get<Paged<IncidentListItem>>(this.base, { params });
  }

  getById(id: string): Observable<IncidentDetail> {
    return this.http.get<IncidentDetail>(`${this.base}/${id}`);
  }

  /** Part 11 §11.50 signature manifest for this incident (oldest-first). */
  signatures(id: string): Observable<SignatureRecord[]> {
    return this.http.get<SignatureRecord[]>(`${this.base}/${id}/signatures`);
  }

  /** Anonymous follow-up: status only, matched on the stored reference hash. */
  track(reference: string): Observable<IncidentTracking> {
    return this.http.get<IncidentTracking>(`${this.base}/track`, { params: new HttpParams().set('reference', reference) });
  }

  report(body: ReportIncidentRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  reportAnonymous(body: ReportAnonymousIncidentRequest): Observable<AnonymousIncidentReceipt> {
    return this.http.post<AnonymousIncidentReceipt>(`${this.base}/anonymous`, body);
  }

  triage(id: string, body: TriageIncidentRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/triage`, body);
  }

  reject(id: string, body: RejectIncidentRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/reject`, body);
  }

  startInvestigation(id: string, body: StartInvestigationRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/start-investigation`, body);
  }

  addContributingFactor(id: string, body: AddContributingFactorRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/contributing-factors`, body);
  }

  addTimelineEntry(id: string, body: AddTimelineEntryRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/timeline`, body);
  }

  recordInvestigationSummary(id: string, body: RecordInvestigationSummaryRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/investigation-summary`, body);
  }

  submitForReview(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/submit-review`, {});
  }

  close(id: string, body: CloseIncidentRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/close`, body);
  }

  declareSentinel(id: string, body: DeclareSentinelRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/declare-sentinel`, body);
  }

  /** Raises a Nonconformance/CAPA from the incident and returns its id ("one loop, many sources"). */
  raiseCapa(id: string): Observable<{ ncId: string }> {
    return this.http.post<{ ncId: string }>(`${this.base}/${id}/raise-capa`, {});
  }
}
