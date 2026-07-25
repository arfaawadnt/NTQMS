import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatedResource, ReferenceStandardDetail, ReferenceStandardListItem, RegisterReferenceStandardRequest,
} from '../models';

/** Typed client for the reference standard / CRM register API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class ReferenceStandardsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/reference-standards`;

  list(status?: string): Observable<ReferenceStandardListItem[]> {
    return this.http.get<ReferenceStandardListItem[]>(
      status ? `${this.base}?status=${encodeURIComponent(status)}` : this.base);
  }

  getById(id: string): Observable<ReferenceStandardDetail> {
    return this.http.get<ReferenceStandardDetail>(`${this.base}/${id}`);
  }

  register(body: RegisterReferenceStandardRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  quarantine(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/quarantine`, { reason });
  }

  reactivate(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/reactivate`, {});
  }

  retire(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/retire`, {});
  }
}
