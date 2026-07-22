import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AssignCompetencyRequest, AssignTrainingRequest, CompetencyDetail, CompetencyListItem,
  CreatedResource, RevokeCompetencyRequest, ScoreAssessmentRequest, TrainingAssignment,
} from '../models';

/**
 * Typed client for the Competency & Training API. Covers both the
 * `/api/competencies` aggregate and the `/api/training-assignments` queue
 * (one method per backend endpoint).
 */
@Injectable({ providedIn: 'root' })
export class CompetencyApiService {
  private readonly http = inject(HttpClient);
  private readonly competencies = `${environment.apiBaseUrl}/competencies`;
  private readonly training = `${environment.apiBaseUrl}/training-assignments`;

  listCompetencies(traineeId?: string, status?: string): Observable<CompetencyListItem[]> {
    let params = new HttpParams();
    if (traineeId) { params = params.set('traineeId', traineeId); }
    if (status) { params = params.set('status', status); }
    return this.http.get<CompetencyListItem[]>(this.competencies, { params });
  }

  getCompetency(id: string): Observable<CompetencyDetail> {
    return this.http.get<CompetencyDetail>(`${this.competencies}/${id}`);
  }

  assignCompetency(body: AssignCompetencyRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.competencies, body);
  }

  scoreAssessment(id: string, body: ScoreAssessmentRequest): Observable<void> {
    return this.http.post<void>(`${this.competencies}/${id}/assessments`, body);
  }

  authorizeCompetency(id: string): Observable<void> {
    return this.http.post<void>(`${this.competencies}/${id}/authorize`, {});
  }

  revokeCompetency(id: string, body: RevokeCompetencyRequest): Observable<void> {
    return this.http.post<void>(`${this.competencies}/${id}/revoke`, body);
  }

  listTraining(traineeId?: string, includeCompleted = false): Observable<TrainingAssignment[]> {
    let params = new HttpParams().set('includeCompleted', includeCompleted);
    if (traineeId) { params = params.set('traineeId', traineeId); }
    return this.http.get<TrainingAssignment[]>(this.training, { params });
  }

  assignTraining(body: AssignTrainingRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.training, body);
  }

  completeTraining(id: string): Observable<void> {
    return this.http.post<void>(`${this.training}/${id}/complete`, {});
  }
}
