import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuditTrailEntry, ChainVerification, FieldChange, SecurityEvent, SignatureRecord } from '../models';

/** Typed client for the read-only Compliance Ledger API (QM/TenantAdmin/ExternalAuditor). */
@Injectable({ providedIn: 'root' })
export class ComplianceApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/compliance`;

  /** Audit-trail entries whose payload or event type contains `subject` (e.g. a record id). */
  auditTrail(subject?: string, take = 200): Observable<AuditTrailEntry[]> {
    let params = new HttpParams().set('take', take);
    if (subject) { params = params.set('subject', subject); }
    return this.http.get<AuditTrailEntry[]>(`${this.base}/audit-trail`, { params });
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
  verifyChain(): Observable<ChainVerification> {
    return this.http.get<ChainVerification>(`${this.base}/chain-verification`);
  }
}
