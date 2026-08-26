import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddCertificateRequest, AddContractRequest, CloseSupplierCarRequest, CreatedResource, DEFAULT_PAGE_SIZE,
  OutsourcedService, Paged, RaiseSupplierCarRequest, RecordCarResponseRequest, RecordEvaluationRequest,
  RegisterSupplierRequest, SupplierDetail, SupplierEvaluation, SupplierListItem, SuspendSupplierRequest,
  TerminateContractRequest,
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

  approve(id: string, body: { password: string; pin: string }): Observable<void> { return this.http.post<void>(`${this.base}/${id}/approve`, body); }

  suspend(id: string, body: SuspendSupplierRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/suspend`, body);
  }

  evaluations(id: string): Observable<SupplierEvaluation[]> {
    return this.http.get<SupplierEvaluation[]>(`${this.base}/${id}/evaluations`);
  }

  recordEvaluation(id: string, body: RecordEvaluationRequest): Observable<{ evaluationId: string }> {
    return this.http.post<{ evaluationId: string }>(`${this.base}/${id}/evaluations`, body);
  }

  // ── Contract / SLA register & CARs (HQMS M16) ───────────────────────────────
  outsourcedServices(): Observable<OutsourcedService[]> { return this.http.get<OutsourcedService[]>(`${this.base}/outsourced-services`); }
  addContract(id: string, body: AddContractRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/${id}/contracts`, body); }
  terminateContract(id: string, contractId: string, body: TerminateContractRequest): Observable<void> { return this.http.post<void>(`${this.base}/${id}/contracts/${contractId}/terminate`, body); }
  raiseCar(id: string, body: RaiseSupplierCarRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/${id}/cars`, body); }
  recordCarResponse(id: string, carId: string, body: RecordCarResponseRequest): Observable<void> { return this.http.post<void>(`${this.base}/${id}/cars/${carId}/response`, body); }
  closeCar(id: string, carId: string, body: CloseSupplierCarRequest): Observable<void> { return this.http.post<void>(`${this.base}/${id}/cars/${carId}/close`, body); }
}
