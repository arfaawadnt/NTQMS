import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddDecisionRequest, CloseReviewRequest, CreatedResource, Paged, ReviewDetail, ReviewListItem,
  ScheduleReviewRequest,
} from '../models';

/** Typed client for the Management Review API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class ReviewApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/management-reviews`;

  list(): Observable<Paged<ReviewListItem>> {
    return this.http.get<Paged<ReviewListItem>>(this.base);
  }

  getById(id: string): Observable<ReviewDetail> {
    return this.http.get<ReviewDetail>(`${this.base}/${id}`);
  }

  schedule(body: ScheduleReviewRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  addDecision(id: string, body: AddDecisionRequest): Observable<{ decisionId: string }> {
    return this.http.post<{ decisionId: string }>(`${this.base}/${id}/decisions`, body);
  }

  close(id: string, body: CloseReviewRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/close`, body);
  }
}
