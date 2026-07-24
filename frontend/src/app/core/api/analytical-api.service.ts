import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ConfigureStudyRequest, CreateQcProfileRequest, CreatedResource, EnrollPtRequest,
  EnterReplicateRequest, PtEnrollment, QcProfile, QcRun, QcTroubleshootRequest,
  RecordPtResultRequest, RecordQcRunRequest, ValidationStudyDetail, ValidationStudyListItem,
} from '../models';

/**
 * Typed client for the Analytical Quality APIs: QC profiles/runs (Westgard),
 * method-validation studies, and proficiency-testing enrollments
 * (one method per backend endpoint).
 */
@Injectable({ providedIn: 'root' })
export class AnalyticalApiService {
  private readonly http = inject(HttpClient);
  private readonly qc = `${environment.apiBaseUrl}/qc`;
  private readonly studies = `${environment.apiBaseUrl}/validation-studies`;
  private readonly pt = `${environment.apiBaseUrl}/proficiency-tests`;

  // ── QC ─────────────────────────────────────────────────────────────────────

  qcProfiles(): Observable<QcProfile[]> {
    return this.http.get<QcProfile[]>(`${this.qc}/profiles`);
  }

  createQcProfile(body: CreateQcProfileRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(`${this.qc}/profiles`, body);
  }

  qcRuns(profileId: string, take = 60): Observable<QcRun[]> {
    return this.http.get<QcRun[]>(`${this.qc}/profiles/${profileId}/runs`, {
      params: new HttpParams().set('take', take),
    });
  }

  recordQcRun(profileId: string, body: RecordQcRunRequest): Observable<{ runId: string }> {
    return this.http.post<{ runId: string }>(`${this.qc}/profiles/${profileId}/runs`, body);
  }

  troubleshootRun(runId: string, body: QcTroubleshootRequest): Observable<void> {
    return this.http.post<void>(`${this.qc}/runs/${runId}/troubleshoot`, body);
  }

  // ── Method validation ──────────────────────────────────────────────────────

  studiesList(state?: string): Observable<ValidationStudyListItem[]> {
    let params = new HttpParams();
    if (state) { params = params.set('state', state); }
    return this.http.get<ValidationStudyListItem[]>(this.studies, { params });
  }

  studyById(id: string): Observable<ValidationStudyDetail> {
    return this.http.get<ValidationStudyDetail>(`${this.studies}/${id}`);
  }

  configureStudy(body: ConfigureStudyRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.studies, body);
  }

  enterReplicate(id: string, body: EnterReplicateRequest): Observable<void> {
    return this.http.post<void>(`${this.studies}/${id}/replicates`, body);
  }

  calculateStudy(id: string): Observable<void> {
    return this.http.post<void>(`${this.studies}/${id}/calculate`, {});
  }

  signOffStudy(id: string): Observable<void> {
    return this.http.post<void>(`${this.studies}/${id}/sign-off`, {});
  }

  // ── Proficiency testing ────────────────────────────────────────────────────

  ptEnrollments(performance?: string): Observable<PtEnrollment[]> {
    let params = new HttpParams();
    if (performance) { params = params.set('performance', performance); }
    return this.http.get<PtEnrollment[]>(this.pt, { params });
  }

  enrollPt(body: EnrollPtRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.pt, body);
  }

  recordPtResult(id: string, body: RecordPtResultRequest): Observable<void> {
    return this.http.post<void>(`${this.pt}/${id}/result`, body);
  }
}
