import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Client for the Part 11 export endpoints. Downloads run through HttpClient
 * (so the bearer token applies) and are handed to the browser as a blob.
 */
@Injectable({ providedIn: 'root' })
export class ExportsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/exports`;

  async download(path: string, fallbackName: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.get(`${this.base}/${path}`, { responseType: 'blob', observe: 'response' }));
    const name = /filename="?([^";]+)"?/
      .exec(response.headers.get('content-disposition') ?? '')?.[1] ?? fallbackName;
    const url = URL.createObjectURL(response.body ?? new Blob());
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    link.click();
    URL.revokeObjectURL(url);
  }

  ncRegisterXlsx(): Promise<void> { return this.download('nonconformances.xlsx', 'nc-register.xlsx'); }
  auditTrailXlsx(): Promise<void> { return this.download('audit-trail.xlsx', 'audit-trail.xlsx'); }
  signaturesXlsx(): Promise<void> { return this.download('signatures.xlsx', 'signature-manifest.xlsx'); }
  reviewPackPdf(reviewId: string): Promise<void> {
    return this.download(`review-pack/${reviewId}.pdf`, 'review-pack.pdf');
  }
}
