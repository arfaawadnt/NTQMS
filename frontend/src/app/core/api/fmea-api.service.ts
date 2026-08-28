import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddFailureModeRequest, CreateFmeaRequest, CreatedResource, FmeaDetail, FmeaListItem,
  RecommendActionRequest, RecordResidualRequest,
} from '../models';

/** Typed client for the FMEA / HFMEA API (HQMS M04). One method per backend endpoint. */
@Injectable({ providedIn: 'root' })
export class FmeaApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/fmea`;

  list(status?: string): Observable<FmeaListItem[]> {
    let params = new HttpParams();
    if (status) { params = params.set('status', status); }
    return this.http.get<FmeaListItem[]>(this.base, { params });
  }

  getById(id: string): Observable<FmeaDetail> { return this.http.get<FmeaDetail>(`${this.base}/${id}`); }
  create(body: CreateFmeaRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(this.base, body); }
  addFailureMode(id: string, body: AddFailureModeRequest): Observable<{ modeId: string }> { return this.http.post<{ modeId: string }>(`${this.base}/${id}/failure-modes`, body); }
  recommend(id: string, modeId: string, body: RecommendActionRequest): Observable<void> { return this.http.post<void>(`${this.base}/${id}/failure-modes/${modeId}/recommend`, body); }
  residual(id: string, modeId: string, body: RecordResidualRequest): Observable<void> { return this.http.post<void>(`${this.base}/${id}/failure-modes/${modeId}/residual`, body); }
  activate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/activate`, {}); }
  close(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/close`, {}); }
}
