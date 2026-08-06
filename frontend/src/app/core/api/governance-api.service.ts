import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ConflictDetail, ConflictListItem, ContextIssue, CreatedResource, InterestedParty } from '../models';

/** Typed client for the impartiality/COI register and org-context APIs. */
@Injectable({ providedIn: 'root' })
export class GovernanceApiService {
  private readonly http = inject(HttpClient);
  private readonly conflicts = `${environment.apiBaseUrl}/conflicts`;
  private readonly context = `${environment.apiBaseUrl}/org-context`;

  // ── Conflicts of interest ──────────────────────────────────────────────────

  listConflicts(status?: string): Observable<ConflictListItem[]> {
    return this.http.get<ConflictListItem[]>(
      status ? `${this.conflicts}?status=${encodeURIComponent(status)}` : this.conflicts);
  }

  getConflict(id: string): Observable<ConflictDetail> {
    return this.http.get<ConflictDetail>(`${this.conflicts}/${id}`);
  }

  declareConflict(body: {
    declarantId: string; description: string; relatedParty: string; declaredOn: string;
  }): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.conflicts, body);
  }

  assessConflict(id: string, riskLevel: string, mitigation: string, credentials: { password: string; pin: string }): Observable<void> {
    return this.http.post<void>(`${this.conflicts}/${id}/assess`, { riskLevel, mitigation, ...credentials });
  }

  closeConflict(id: string, outcome: string, closureNote: string): Observable<void> {
    return this.http.post<void>(`${this.conflicts}/${id}/close`, { outcome, closureNote });
  }

  // ── Interested parties + context issues ────────────────────────────────────

  parties(): Observable<InterestedParty[]> {
    return this.http.get<InterestedParty[]>(`${this.context}/interested-parties`);
  }

  registerParty(body: Omit<InterestedParty, 'id' | 'partyRef' | 'status'>): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(`${this.context}/interested-parties`, body);
  }

  reviseParty(id: string, body: Omit<InterestedParty, 'id' | 'partyRef' | 'status'>): Observable<void> {
    return this.http.put<void>(`${this.context}/interested-parties/${id}`, body);
  }

  archiveParty(id: string): Observable<void> {
    return this.http.post<void>(`${this.context}/interested-parties/${id}/archive`, {});
  }

  issues(): Observable<ContextIssue[]> {
    return this.http.get<ContextIssue[]>(`${this.context}/issues`);
  }

  registerIssue(body: { type: string; category: string; description: string; impact: string }): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(`${this.context}/issues`, body);
  }

  reviseIssue(id: string, body: { type: string; category: string; description: string; impact: string }): Observable<void> {
    return this.http.put<void>(`${this.context}/issues/${id}`, body);
  }

  linkIssueRisk(id: string, riskId: string): Observable<void> {
    return this.http.post<void>(`${this.context}/issues/${id}/link-risk`, { riskId });
  }

  closeIssue(id: string, resolution: string): Observable<void> {
    return this.http.post<void>(`${this.context}/issues/${id}/close`, { resolution });
  }
}
