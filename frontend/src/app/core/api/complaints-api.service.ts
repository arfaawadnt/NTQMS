import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ComplaintDetail, ComplaintListItem, CreatedResource, LogComplaintOutcomeRequest,
  LogComplaintRequest, ResolveComplaintRequest, ValidateComplaintRequest,
} from '../models';

/** Typed client for the Complaints API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class ComplaintsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/complaints`;

  list(status?: string): Observable<ComplaintListItem[]> {
    let params = new HttpParams();
    if (status) { params = params.set('status', status); }
    return this.http.get<ComplaintListItem[]>(this.base, { params });
  }

  getById(id: string): Observable<ComplaintDetail> {
    return this.http.get<ComplaintDetail>(`${this.base}/${id}`);
  }

  log(body: LogComplaintRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  acknowledge(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/acknowledge`, {});
  }

  validate(id: string, body: ValidateComplaintRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/validate`, body);
  }

  startInvestigation(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/start-investigation`, {});
  }

  logOutcome(id: string, body: LogComplaintOutcomeRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/outcome`, body);
  }

  resolve(id: string, body: ResolveComplaintRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/resolve`, body);
  }

  close(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/close`, {});
  }
}
