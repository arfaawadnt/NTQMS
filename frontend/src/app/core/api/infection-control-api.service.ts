import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatedResource, DeviceExposureListItem, HaiCaseDetail, HaiCaseListItem, HaiRates,
  RecordDeviceExposureRequest, RemoveDeviceRequest, ReportHaiCaseRequest, ReviewHaiCaseRequest,
} from '../models';

/** Typed client for the Infection Prevention & Control API (HQMS M09). */
@Injectable({ providedIn: 'root' })
export class InfectionControlApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/infection-control`;

  listCases(type?: string, status?: string): Observable<HaiCaseListItem[]> {
    let params = new HttpParams();
    if (type) { params = params.set('type', type); }
    if (status) { params = params.set('status', status); }
    return this.http.get<HaiCaseListItem[]>(`${this.base}/cases`, { params });
  }

  getCase(id: string): Observable<HaiCaseDetail> { return this.http.get<HaiCaseDetail>(`${this.base}/cases/${id}`); }
  rates(windowDays = 30): Observable<HaiRates> {
    return this.http.get<HaiRates>(`${this.base}/rates`, { params: new HttpParams().set('windowDays', windowDays) });
  }
  reportCase(body: ReportHaiCaseRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/cases`, body); }
  reviewCase(id: string, body: ReviewHaiCaseRequest): Observable<void> { return this.http.post<void>(`${this.base}/cases/${id}/review`, body); }
  closeCase(id: string): Observable<void> { return this.http.post<void>(`${this.base}/cases/${id}/close`, {}); }
  rejectCase(id: string, body: { reason: string }): Observable<void> { return this.http.post<void>(`${this.base}/cases/${id}/reject`, body); }

  listDevices(deviceType?: string, status?: string): Observable<DeviceExposureListItem[]> {
    let params = new HttpParams();
    if (deviceType) { params = params.set('deviceType', deviceType); }
    if (status) { params = params.set('status', status); }
    return this.http.get<DeviceExposureListItem[]>(`${this.base}/devices`, { params });
  }

  recordDevice(body: RecordDeviceExposureRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/devices`, body); }
  removeDevice(id: string, body: RemoveDeviceRequest): Observable<void> { return this.http.post<void>(`${this.base}/devices/${id}/remove`, body); }
}
