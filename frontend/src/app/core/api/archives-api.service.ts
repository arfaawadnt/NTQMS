import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ArchiveListItem, ArchiveRecordRequest, CreatedResource, Paged } from '../models';

/** Typed client for the Records & Retention API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class ArchivesApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/archives`;

  list(state?: string): Observable<Paged<ArchiveListItem>> {
    let params = new HttpParams();
    if (state) { params = params.set('state', state); }
    return this.http.get<Paged<ArchiveListItem>>(this.base, { params });
  }

  archive(body: ArchiveRecordRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  retrieve(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/retrieve`, {}); }

  return(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/return`, {}); }

  dispose(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/dispose`, {}); }

  placeLegalHold(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/legal-hold`, { reason });
  }

  releaseLegalHold(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/legal-hold`);
  }
}
