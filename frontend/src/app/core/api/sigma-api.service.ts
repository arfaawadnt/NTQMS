import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateSigmaAssessmentRequest, CreatedResource, SigmaAssessmentDetail, SigmaAssessmentListItem,
} from '../models';

/** Typed client for the Six-Sigma assessment API. */
@Injectable({ providedIn: 'root' })
export class SigmaApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/sigma-assessments`;

  list(state?: string): Observable<SigmaAssessmentListItem[]> {
    return this.http.get<SigmaAssessmentListItem[]>(
      state ? `${this.base}?state=${encodeURIComponent(state)}` : this.base);
  }

  getById(id: string): Observable<SigmaAssessmentDetail> {
    return this.http.get<SigmaAssessmentDetail>(`${this.base}/${id}`);
  }

  create(body: CreateSigmaAssessmentRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  updateInputs(id: string, allowableTotalErrorPct: number, biasPct: number, cvPct: number): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}`, { allowableTotalErrorPct, biasPct, cvPct });
  }

  signOff(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/sign-off`, {}); }
}
