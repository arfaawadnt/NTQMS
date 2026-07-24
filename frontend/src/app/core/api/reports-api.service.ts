import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DashboardKpis, KpiHistoryPoint, NcParetoBucket, SlaCompliance } from '../models';

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
}
