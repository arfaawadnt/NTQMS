import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatedResource, GrantTestAuthorizationRequest, TestAuthorizationDetail, TestAuthorizationListItem,
} from '../models';

/** Typed client for the personnel authorization matrix API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class AuthorizationsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/test-authorizations`;

  list(userId?: string, status?: string): Observable<TestAuthorizationListItem[]> {
    let params = new HttpParams();
    if (userId) { params = params.set('userId', userId); }
    if (status) { params = params.set('status', status); }
    return this.http.get<TestAuthorizationListItem[]>(this.base, { params });
  }

  getById(id: string): Observable<TestAuthorizationDetail> {
    return this.http.get<TestAuthorizationDetail>(`${this.base}/${id}`);
  }

  grant(body: GrantTestAuthorizationRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  suspend(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/suspend`, { reason });
  }

  reinstate(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/reinstate`, {});
  }

  revoke(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/revoke`, { reason });
  }
}
