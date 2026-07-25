import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddUncertaintyComponentRequest, CreateUncertaintyBudgetRequest, CreatedResource,
  UncertaintyBudgetDetail, UncertaintyBudgetListItem,
} from '../models';

/** Typed client for the Measurement Uncertainty API (one method per endpoint). */
@Injectable({ providedIn: 'root' })
export class UncertaintyApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/uncertainty-budgets`;

  list(status?: string): Observable<UncertaintyBudgetListItem[]> {
    let params = new HttpParams();
    if (status) { params = params.set('status', status); }
    return this.http.get<UncertaintyBudgetListItem[]>(this.base, { params });
  }

  getById(id: string): Observable<UncertaintyBudgetDetail> {
    return this.http.get<UncertaintyBudgetDetail>(`${this.base}/${id}`);
  }

  create(body: CreateUncertaintyBudgetRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  addComponent(id: string, body: AddUncertaintyComponentRequest): Observable<{ componentId: string }> {
    return this.http.post<{ componentId: string }>(`${this.base}/${id}/components`, body);
  }

  removeComponent(id: string, componentId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/components/${componentId}`);
  }

  calculate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/calculate`, {}); }

  approve(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/approve`, {}); }
}
