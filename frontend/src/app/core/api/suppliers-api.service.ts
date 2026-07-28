import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddCertificateRequest, CreatedResource, DEFAULT_PAGE_SIZE, Paged, RecordEvaluationRequest,
  RegisterSupplierRequest, SupplierDetail, SupplierEvaluation, SupplierListItem, SuspendSupplierRequest,
} from '../models';

/** Typed client for the Supplier Quality API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class SuppliersApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/suppliers`;

  list(status?: string, page = 1, pageSize = DEFAULT_PAGE_SIZE): Observable<Paged<SupplierListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) { params = params.set('status', status); }
    return this.http.get<Paged<SupplierListItem>>(this.base, { params });
  }

  getById(id: string): Observable<SupplierDetail> {
    return this.http.get<SupplierDetail>(`${this.base}/${id}`);
  }

  register(body: RegisterSupplierRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  addCertificate(id: string, body: AddCertificateRequest): Observable<{ certificateId: string }> {
    return this.http.post<{ certificateId: string }>(`${this.base}/${id}/certificates`, body);
  }

  approve(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/approve`, {}); }

  suspend(id: string, body: SuspendSupplierRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/suspend`, body);
  }

  evaluations(id: string): Observable<SupplierEvaluation[]> {
    return this.http.get<SupplierEvaluation[]>(`${this.base}/${id}/evaluations`);
  }

  recordEvaluation(id: string, body: RecordEvaluationRequest): Observable<{ evaluationId: string }> {
    return this.http.post<{ evaluationId: string }>(`${this.base}/${id}/evaluations`, body);
  }
}
