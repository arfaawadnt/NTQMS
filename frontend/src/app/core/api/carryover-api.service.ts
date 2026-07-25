import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CarryoverDetail, CarryoverListItem, CreateCarryoverStudyRequest, CreatedResource,
} from '../models';

/** Typed client for the carryover-study API (CLSI EP10-style). */
@Injectable({ providedIn: 'root' })
export class CarryoverApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/carryover-studies`;

  list(state?: string): Observable<CarryoverListItem[]> {
    return this.http.get<CarryoverListItem[]>(
      state ? `${this.base}?state=${encodeURIComponent(state)}` : this.base);
  }

  getById(id: string): Observable<CarryoverDetail> {
    return this.http.get<CarryoverDetail>(`${this.base}/${id}`);
  }

  create(body: CreateCarryoverStudyRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  addReading(id: string, kind: string, sequence: number, value: number): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/readings`, { kind, sequence, value });
  }

  removeReading(id: string, readingId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/readings/${readingId}`);
  }

  calculate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/calculate`, {}); }

  signOff(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/sign-off`, {}); }
}
