import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ClassifyMortalityRequest, CommitteeDiscussedRequest, ComplicationDetail, ComplicationListItem,
  CreatedResource, MortalityDetail, MortalityListItem, MortalityRates, ReportComplicationRequest,
  ReportMortalityRequest, ReviewComplicationRequest, SecondReviewRequest,
} from '../models';

/** Typed client for the Mortality, Morbidity & Peer Review API (HQMS M10). */
@Injectable({ providedIn: 'root' })
export class MortalityReviewApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/mortality-review`;

  listReviews(classification?: string, status?: string): Observable<MortalityListItem[]> {
    let params = new HttpParams();
    if (classification) { params = params.set('classification', classification); }
    if (status) { params = params.set('status', status); }
    return this.http.get<MortalityListItem[]>(`${this.base}/reviews`, { params });
  }

  getReview(id: string): Observable<MortalityDetail> { return this.http.get<MortalityDetail>(`${this.base}/reviews/${id}`); }
  rates(windowDays = 30): Observable<MortalityRates> {
    return this.http.get<MortalityRates>(`${this.base}/rates`, { params: new HttpParams().set('windowDays', windowDays) });
  }
  reportReview(body: ReportMortalityRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/reviews`, body); }
  classify(id: string, body: ClassifyMortalityRequest): Observable<void> { return this.http.post<void>(`${this.base}/reviews/${id}/classify`, body); }
  secondReview(id: string, body: SecondReviewRequest): Observable<void> { return this.http.post<void>(`${this.base}/reviews/${id}/second-review`, body); }
  committeeDiscussed(id: string, body: CommitteeDiscussedRequest): Observable<void> { return this.http.post<void>(`${this.base}/reviews/${id}/committee-discussed`, body); }
  closeReview(id: string): Observable<void> { return this.http.post<void>(`${this.base}/reviews/${id}/close`, {}); }

  listComplications(type?: string, status?: string): Observable<ComplicationListItem[]> {
    let params = new HttpParams();
    if (type) { params = params.set('type', type); }
    if (status) { params = params.set('status', status); }
    return this.http.get<ComplicationListItem[]>(`${this.base}/complications`, { params });
  }

  getComplication(id: string): Observable<ComplicationDetail> { return this.http.get<ComplicationDetail>(`${this.base}/complications/${id}`); }
  reportComplication(body: ReportComplicationRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/complications`, body); }
  reviewComplication(id: string, body: ReviewComplicationRequest): Observable<void> { return this.http.post<void>(`${this.base}/complications/${id}/review`, body); }
  closeComplication(id: string): Observable<void> { return this.http.post<void>(`${this.base}/complications/${id}/close`, {}); }
  rejectComplication(id: string, body: { reason: string }): Observable<void> { return this.http.post<void>(`${this.base}/complications/${id}/reject`, body); }
}
