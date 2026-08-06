import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ChangeDetail, ChangeListItem, CloseChangeRequest, CreatedResource, DEFAULT_PAGE_SIZE,
  LinkRiskRequest, Paged, ProposeChangeRequest, RejectChangeRequest, ReviewChangeRequest,
} from '../models';

/** Typed client for the Change Control API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class ChangeApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/changes`;

  list(status?: string, page = 1, pageSize = DEFAULT_PAGE_SIZE): Observable<Paged<ChangeListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) { params = params.set('status', status); }
    return this.http.get<Paged<ChangeListItem>>(this.base, { params });
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

  approve(id: string, body: { password: string; pin: string }): Observable<void> { return this.http.post<void>(`${this.base}/${id}/approve`, body); }

  reject(id: string, body: RejectChangeRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/reject`, body);
  }

  close(id: string, body: CloseChangeRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/close`, body);
  }

  review(id: string, body: ReviewChangeRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/review`, body);
  }
}
