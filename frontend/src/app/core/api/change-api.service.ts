import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ChangeDetail, ChangeListItem, CloseChangeRequest, CreatedResource, LinkRiskRequest,
  ProposeChangeRequest, RejectChangeRequest,
} from '../models';

/** Typed client for the Change Control API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class ChangeApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/changes`;

  list(status?: string): Observable<ChangeListItem[]> {
    let params = new HttpParams();
    if (status) { params = params.set('status', status); }
    return this.http.get<ChangeListItem[]>(this.base, { params });
  }

  getById(id: string): Observable<ChangeDetail> {
    return this.http.get<ChangeDetail>(`${this.base}/${id}`);
  }

  propose(body: ProposeChangeRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  linkRisk(id: string, body: LinkRiskRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/risk`, body);
  }

  approve(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/approve`, {}); }

  reject(id: string, body: RejectChangeRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/reject`, body);
  }

  close(id: string, body: CloseChangeRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/close`, body);
  }
}
