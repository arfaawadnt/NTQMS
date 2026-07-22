import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateDocumentRequest, CreatedResource, DocumentDetail, DocumentListItem,
  DraftNewVersionRequest, PublishDocumentRequest, RejectVersionRequest,
} from '../models';

/** Typed client for the Document Control API (one method per backend endpoint). */
@Injectable({ providedIn: 'root' })
export class DocumentsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/documents`;

  list(status?: string, search?: string): Observable<DocumentListItem[]> {
    const params = new URLSearchParams();
    if (status) { params.set('status', status); }
    if (search) { params.set('search', search); }
    const query = params.toString();
    return this.http.get<DocumentListItem[]>(query ? `${this.base}?${query}` : this.base);
  }

  getById(id: string): Observable<DocumentDetail> {
    return this.http.get<DocumentDetail>(`${this.base}/${id}`);
  }

  create(body: CreateDocumentRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  submit(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/submit`, {}); }
  recommend(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/recommend`, {}); }
  reject(id: string, body: RejectVersionRequest): Observable<void> { return this.http.post<void>(`${this.base}/${id}/reject`, body); }
  publish(id: string, body: PublishDocumentRequest): Observable<void> { return this.http.post<void>(`${this.base}/${id}/publish`, body); }
  draftNewVersion(id: string, body: DraftNewVersionRequest): Observable<void> { return this.http.post<void>(`${this.base}/${id}/versions`, body); }
  retire(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/retire`, {}); }
}
