import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddSurveyQuestionRequest, CreateSurveyRequest, CreatedResource, SubmitSurveyResponseRequest,
  SurveyDetail, SurveyListItem, SurveyResults,
} from '../models';

/** Typed client for the Patient Satisfaction Surveys API (HQMS M11). */
@Injectable({ providedIn: 'root' })
export class SurveysApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/surveys`;

  list(status?: string): Observable<SurveyListItem[]> {
    let params = new HttpParams();
    if (status) { params = params.set('status', status); }
    return this.http.get<SurveyListItem[]>(this.base, { params });
  }

  getById(id: string): Observable<SurveyDetail> { return this.http.get<SurveyDetail>(`${this.base}/${id}`); }
  results(id: string): Observable<SurveyResults> { return this.http.get<SurveyResults>(`${this.base}/${id}/results`); }
  create(body: CreateSurveyRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(this.base, body); }
  addQuestion(id: string, body: AddSurveyQuestionRequest): Observable<{ questionId: string }> { return this.http.post<{ questionId: string }>(`${this.base}/${id}/questions`, body); }
  open(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/open`, {}); }
  close(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/close`, {}); }
  submitResponse(id: string, body: SubmitSurveyResponseRequest): Observable<{ responseId: string }> { return this.http.post<{ responseId: string }>(`${this.base}/${id}/responses`, body); }
}
