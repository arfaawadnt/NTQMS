import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateOutlierScreeningRequest, CreatedResource, OutlierScreeningDetail, OutlierScreeningListItem,
} from '../models';

/** Typed client for the outlier-detection API (Tukey fences + modified z-score). */
@Injectable({ providedIn: 'root' })
export class OutlierApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/outlier-screenings`;

  list(state?: string): Observable<OutlierScreeningListItem[]> {
    return this.http.get<OutlierScreeningListItem[]>(
      state ? `${this.base}?state=${encodeURIComponent(state)}` : this.base);
  }

  getById(id: string): Observable<OutlierScreeningDetail> {
    return this.http.get<OutlierScreeningDetail>(`${this.base}/${id}`);
  }

  create(body: CreateOutlierScreeningRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  addPoint(id: string, value: number, label: string | null): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/points`, { value, label });
  }

  removePoint(id: string, pointId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/points/${pointId}`);
  }

  calculate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/calculate`, {}); }

  signOff(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/sign-off`, {}); }
}
