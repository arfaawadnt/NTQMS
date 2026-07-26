import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ControlledCopy, CreateDocumentRequest, CreatedResource, DocumentAcknowledgement, DocumentDetail, DocumentListItem,
  DraftNewVersionRequest, MyDocumentAcknowledgement, PublishDocumentRequest, RejectVersionRequest, SignatureRecord,
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
  /** Records the completed periodic review and re-arms the cycle. */
  confirmReview(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/confirm-review`, {});
  }

  /** Part 11 §11.50 signature manifest for this document (any viewer of the record). */
  signatures(id: string): Observable<SignatureRecord[]> {
    return this.http.get<SignatureRecord[]>(`${this.base}/${id}/signatures`);
  }

  /** Current user confirms they read & understood the published version. */
  acknowledge(id: string): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/${id}/acknowledge`, {}); }
  myAcknowledgement(id: string): Observable<MyDocumentAcknowledgement> { return this.http.get<MyDocumentAcknowledgement>(`${this.base}/${id}/my-acknowledgement`); }
  acknowledgements(id: string): Observable<DocumentAcknowledgement[]> { return this.http.get<DocumentAcknowledgement[]>(`${this.base}/${id}/acknowledgements`); }

  controlledCopies(id: string): Observable<ControlledCopy[]> { return this.http.get<ControlledCopy[]>(`${this.base}/${id}/controlled-copies`); }
  issueControlledCopy(id: string, holder: string): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/${id}/controlled-copies`, { holder }); }
  closeControlledCopy(copyId: string, outcome: string): Observable<void> { return this.http.post<void>(`${this.base}/controlled-copies/${copyId}/close`, { outcome }); }

  publish(id: string, body: PublishDocumentRequest): Observable<void> { return this.http.post<void>(`${this.base}/${id}/publish`, body); }
  draftNewVersion(id: string, body: DraftNewVersionRequest): Observable<void> { return this.http.post<void>(`${this.base}/${id}/versions`, body); }
  retire(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/retire`, {}); }
}
