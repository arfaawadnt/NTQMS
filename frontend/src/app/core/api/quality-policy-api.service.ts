import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreatedResource, QualityPolicy } from '../models';

/** Typed client for the controlled quality-policy API (ISO 9001 §5.2 / 17025 §8.2). */
@Injectable({ providedIn: 'root' })
export class QualityPolicyApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/quality-policy`;

  /** The policy in force, or null when none has been approved yet (204). */
  active(): Observable<QualityPolicy | null> {
    return this.http.get<QualityPolicy | null>(`${this.base}/active`);
  }

  history(): Observable<QualityPolicy[]> {
    return this.http.get<QualityPolicy[]>(this.base);
  }

  draft(statement: string): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, { statement });
  }

  revise(id: string, statement: string): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}`, { statement });
  }

  approve(id: string, effectiveDate: string, credentials: { password: string; pin: string }): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/approve`, { effectiveDate, ...credentials });
  }
}
