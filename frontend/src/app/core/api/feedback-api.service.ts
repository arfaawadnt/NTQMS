import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatedResource, FeedbackDetail, FeedbackListItem, LogFeedbackRequest,
} from '../models';

/** Typed client for the general feedback API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class FeedbackApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/feedback`;

  list(status?: string, type?: string): Observable<FeedbackListItem[]> {
    let params = new HttpParams();
    if (status) { params = params.set('status', status); }
    if (type) { params = params.set('type', type); }
    return this.http.get<FeedbackListItem[]>(this.base, { params });
  }

  getById(id: string): Observable<FeedbackDetail> {
    return this.http.get<FeedbackDetail>(`${this.base}/${id}`);
  }

  log(body: LogFeedbackRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  review(id: string, reviewNotes: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/review`, { reviewNotes });
  }

  close(id: string, actionSummary: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/close`, { actionSummary });
  }

  escalate(id: string, complainantName: string, complainantContact: string | null): Observable<{ complaintId: string }> {
    return this.http.post<{ complaintId: string }>(`${this.base}/${id}/escalate`, { complainantName, complainantContact });
  }
}
