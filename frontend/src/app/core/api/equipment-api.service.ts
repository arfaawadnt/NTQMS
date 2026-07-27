import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatedResource, EquipmentDetail, EquipmentListItem, LogCalibrationRequest,
  LogMaintenanceRequest, Paged, RecordIntermediateCheckRequest, RegisterEquipmentRequest,
} from '../models';

/** Typed client for the Equipment & Calibration API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class EquipmentApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/equipment`;

  list(status?: string): Observable<Paged<EquipmentListItem>> {
    return this.http.get<Paged<EquipmentListItem>>(status ? `${this.base}?status=${encodeURIComponent(status)}` : this.base);
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
