import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
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

  /**
   * Downloads through HttpClient so the bearer token applies — a plain anchor
   * to the [Authorize] endpoint gets a 401 — then hands the blob to the
   * browser, taking the filename from Content-Disposition (same pattern as the
   * register exports).
   */
  async download(fileId: string, fallbackName: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.get(`${this.base}/${fileId}`, { responseType: 'blob', observe: 'response' }));
    const name = /filename="?([^";]+)"?/.exec(response.headers.get('content-disposition') ?? '')?.[1] ?? fallbackName;
    const url = URL.createObjectURL(response.body ?? new Blob());
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    link.click();
    URL.revokeObjectURL(url);
  }
}
