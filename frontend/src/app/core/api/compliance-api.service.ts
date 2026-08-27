import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuditTrailEntry, AuditTrailReview, ChainVerification, FieldChange, SecurityEvent, SignatureRecord } from '../models';

/** Typed client for the read-only Compliance Ledger API (QM/TenantAdmin/ExternalAuditor). */
@Injectable({ providedIn: 'root' })
export class ComplianceApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/compliance`;

  /**
   * Ledger-wide search: audit-trail entries whose payload or event type *contains*
   * `subject`. Backs the compliance search box — for one record's own timeline use
   * {@link recordAuditTrail}, which matches exactly and never returns other records' logs.
   */
  auditTrail(subject?: string, take = 200): Observable<AuditTrailEntry[]> {
    let params = new HttpParams().set('take', take);
    if (subject) { params = params.set('subject', subject); }
    return this.http.get<AuditTrailEntry[]>(`${this.base}/audit-trail`, { params });
  }

  /** Audit-trail entries a single record produced (matched on its aggregate id — no cross-record leakage). */
  recordAuditTrail(subjectId: string, take = 200): Observable<AuditTrailEntry[]> {
    return this.http.get<AuditTrailEntry[]>(`${this.base}/audit-trail/record/${subjectId}`, {
      params: new HttpParams().set('take', take),
    });
  }

  /** Field-level change rows, optionally filtered to one record's id. */
  fieldChanges(entityId?: string, take = 200): Observable<FieldChange[]> {
    let params = new HttpParams().set('take', take);
    if (entityId) { params = params.set('entityId', entityId); }
    return this.http.get<FieldChange[]>(`${this.base}/field-changes`, { params });
  }

  signatures(take = 200): Observable<SignatureRecord[]> {
    return this.http.get<SignatureRecord[]>(`${this.base}/signatures`, {
      params: new HttpParams().set('take', take),
    });
  }

  securityEvents(take = 200): Observable<SecurityEvent[]> {
    return this.http.get<SecurityEvent[]>(`${this.base}/security-events`, {
      params: new HttpParams().set('take', take),
    });
  }

  /** Recomputes the tenant's audit-trail hash chain and reports the first break, if any. */
  auditTrailReviews(): Observable<AuditTrailReview[]> {
    return this.http.get<AuditTrailReview[]>(`${this.base}/audit-trail-reviews`);
  }

  openAuditTrailReview(periodStart: string, periodEnd: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/audit-trail-reviews`, { periodStart, periodEnd });
  }

  completeAuditTrailReview(
    id: string, anomaliesFound: boolean, conclusion: string,
    credentials: { password: string; pin: string }): Observable<void> {
    return this.http.post<void>(
      `${this.base}/audit-trail-reviews/${id}/complete`, { anomaliesFound, conclusion, ...credentials });
  }

  verifyChain(): Observable<ChainVerification> {
    return this.http.get<ChainVerification>(`${this.base}/chain-verification`);
  }
}
