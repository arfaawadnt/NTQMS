import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { FileUploaded } from '../models';

/** Uploads/downloads files against the content-addressed object store (POST/GET /api/files). */
@Injectable({ providedIn: 'root' })
export class FilesApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/files`;

  /** Uploads a file (multipart field name must be "file" to match the controller). */
  upload(file: File): Observable<FileUploaded> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<FileUploaded>(this.base, form);
  }

  /** Absolute URL for downloading a stored file by reference id. */
  downloadUrl(fileId: string): string {
    return `${this.base}/${fileId}`;
  }
}
