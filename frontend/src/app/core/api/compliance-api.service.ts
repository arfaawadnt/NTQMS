import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuditTrailEntry } from '../models';

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
}
