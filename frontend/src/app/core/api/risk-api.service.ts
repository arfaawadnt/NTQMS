import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddMitigationRequest, AssessRiskRequest, CreatedResource, Paged, ResidualAssessmentRequest,
  RiskDetail, RiskListItem,
} from '../models';

/** Typed client for the Risk register API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class RiskApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/risks`;

  list(status?: string): Observable<Paged<RiskListItem>> {
    let params = new HttpParams();
    if (status) { params = params.set('status', status); }
    return this.http.get<Paged<RiskListItem>>(this.base, { params });
  }

  getById(id: string): Observable<RiskDetail> {
    return this.http.get<RiskDetail>(`${this.base}/${id}`);
  }

  assess(body: AssessRiskRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  addMitigation(id: string, body: AddMitigationRequest): Observable<{ actionId: string }> {
    return this.http.post<{ actionId: string }>(`${this.base}/${id}/actions`, body);
  }

  completeMitigation(id: string, actionId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/actions/${actionId}/complete`, {});
  }

  recordResidual(id: string, body: ResidualAssessmentRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/residual`, body);
  }

  close(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/close`, {}); }
}
