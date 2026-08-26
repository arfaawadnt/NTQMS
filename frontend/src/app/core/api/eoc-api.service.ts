import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddFindingRequest, CreatedResource, DrillDetail, DrillListItem, EocSummary, EvaluateDrillRequest,
  ExecuteDrillRequest, ResolveFindingRequest, RoundDetail, RoundListItem, ScheduleDrillRequest,
  ScheduleRoundRequest,
} from '../models';

/** Typed client for the Environment of Care API (HQMS M15). */
@Injectable({ providedIn: 'root' })
export class EocApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/eoc`;

  listRounds(type?: string, status?: string): Observable<RoundListItem[]> {
    const params = new URLSearchParams();
    if (type) { params.set('type', type); }
    if (status) { params.set('status', status); }
    return this.http.get<RoundListItem[]>(`${this.base}/rounds?${params.toString()}`);
  }

  getRound(id: string): Observable<RoundDetail> { return this.http.get<RoundDetail>(`${this.base}/rounds/${id}`); }
  summary(): Observable<EocSummary> { return this.http.get<EocSummary>(`${this.base}/summary`); }
  scheduleRound(body: ScheduleRoundRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/rounds`, body); }
  startRound(id: string): Observable<void> { return this.http.post<void>(`${this.base}/rounds/${id}/start`, {}); }
  addFinding(id: string, body: AddFindingRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/rounds/${id}/findings`, body); }
  resolveFinding(id: string, findingId: string, body: ResolveFindingRequest): Observable<void> { return this.http.post<void>(`${this.base}/rounds/${id}/findings/${findingId}/resolve`, body); }
  completeRound(id: string): Observable<void> { return this.http.post<void>(`${this.base}/rounds/${id}/complete`, {}); }

  listDrills(type?: string, status?: string): Observable<DrillListItem[]> {
    const params = new URLSearchParams();
    if (type) { params.set('type', type); }
    if (status) { params.set('status', status); }
    return this.http.get<DrillListItem[]>(`${this.base}/drills?${params.toString()}`);
  }

  getDrill(id: string): Observable<DrillDetail> { return this.http.get<DrillDetail>(`${this.base}/drills/${id}`); }
  scheduleDrill(body: ScheduleDrillRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/drills`, body); }
  executeDrill(id: string, body: ExecuteDrillRequest): Observable<void> { return this.http.post<void>(`${this.base}/drills/${id}/execute`, body); }
  evaluateDrill(id: string, body: EvaluateDrillRequest): Observable<void> { return this.http.post<void>(`${this.base}/drills/${id}/evaluate`, body); }
}
