import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreatedResource, PtPlanDetail, PtPlanListItem } from '../models';

/** Typed client for the annual PT/EQA plan API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class PtPlansApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/pt-plans`;

  list(): Observable<PtPlanListItem[]> { return this.http.get<PtPlanListItem[]>(this.base); }

  getById(id: string): Observable<PtPlanDetail> { return this.http.get<PtPlanDetail>(`${this.base}/${id}`); }

  create(year: number): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, { year });
  }

  addItem(id: string, body: {
    scheme: string; analyte: string; provider: string | null; plannedCycles: number; notes: string | null;
  }): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/items`, body);
  }

  removeItem(id: string, itemId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/items/${itemId}`);
  }

  approve(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/approve`, {}); }

  recordFulfilment(id: string, itemId: string, enrollmentId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/fulfilments`, { itemId, enrollmentId });
  }

  close(id: string, closureSummary: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/close`, { closureSummary });
  }
}
