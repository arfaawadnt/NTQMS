import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatedResource, DEFAULT_PAGE_SIZE, EquipmentDetail, EquipmentListItem, LogCalibrationRequest,
  LogMaintenanceRequest, Paged, RecordIntermediateCheckRequest, RegisterEquipmentRequest,
} from '../models';

/** Typed client for the Equipment & Calibration API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class EquipmentApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/equipment`;

  list(status?: string, page = 1, pageSize = DEFAULT_PAGE_SIZE): Observable<Paged<EquipmentListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) { params = params.set('status', status); }
    return this.http.get<Paged<EquipmentListItem>>(this.base, { params });
  }

  getById(id: string): Observable<EquipmentDetail> {
    return this.http.get<EquipmentDetail>(`${this.base}/${id}`);
  }

  register(body: RegisterEquipmentRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  logCalibration(id: string, body: LogCalibrationRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/calibrations`, body);
  }

  logMaintenance(id: string, body: LogMaintenanceRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/maintenance`, body);
  }

  recordCheck(id: string, body: RecordIntermediateCheckRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/checks`, body);
  }

  retire(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/retire`, {}); }
}
