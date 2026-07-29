import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Workspace } from '../models';

/**
 * Typed client for the pre-authentication workspace lookup: turns the slug in a
 * laboratory's own sign-in address (/t/{slug}) into its display NAME, so the
 * login page names the lab rather than echoing its identifier.
 */
@Injectable({ providedIn: 'root' })
export class WorkspaceApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/auth/workspace`;

  /** 404 when the slug is unknown, malformed, or the tenant is not active. */
  get(slug: string): Observable<Workspace> {
    return this.http.get<Workspace>(`${this.base}/${encodeURIComponent(slug)}`);
  }
}
