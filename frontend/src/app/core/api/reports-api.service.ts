import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  DashboardKpis, KpiHistoryPoint, NcParetoBucket, QualityAnalytics,
  QualityHealthProfile, QualityHealthWeight, SlaCompliance,
} from '../models';

/** Typed client for the Reporting read side (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class ReportsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/reports`;

  kpis(): Observable<DashboardKpis> {
    return this.http.get<DashboardKpis>(`${this.base}/kpis`);
  }

  kpiHistory(days = 90): Observable<KpiHistoryPoint[]> {
    return this.http.get<KpiHistoryPoint[]>(`${this.base}/kpi-history`, {
      params: new HttpParams().set('days', days),
    });
  }

  ncPareto(): Observable<NcParetoBucket[]> {
    return this.http.get<NcParetoBucket[]>(`${this.base}/nc-pareto`);
  }

  slaCompliance(): Observable<SlaCompliance> {
    return this.http.get<SlaCompliance>(`${this.base}/sla-compliance`);
  }

  /** Every analytics section the caller may see, optionally narrowed to a branch/department. */
  qualityAnalytics(branchId?: string, departmentId?: string): Observable<QualityAnalytics> {
    let params = new HttpParams();
    if (branchId) { params = params.set('branchId', branchId); }
    if (departmentId) { params = params.set('departmentId', departmentId); }
    return this.http.get<QualityAnalytics>(`${this.base}/quality-analytics`, { params });
  }

  qualityHealthProfile(): Observable<QualityHealthProfile> {
    return this.http.get<QualityHealthProfile>(`${this.base}/quality-health-profile`);
  }

  updateQualityHealthProfile(weights: QualityHealthWeight[], reason: string): Observable<void> {
    return this.http.put<void>(`${this.base}/quality-health-profile`, { weights, reason });
  }
}
