import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreatedResource, UserAccessReview } from '../models';

/** Typed client for periodic user-access reviews (Part 11 §11.10(d) / Annex 11 §12). */
@Injectable({ providedIn: 'root' })
export class AccessReviewsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/access-reviews`;

  list(): Observable<UserAccessReview[]> {
    return this.http.get<UserAccessReview[]>(this.base);
  }

  open(): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, {});
  }

  complete(id: string, changesRequired: boolean, conclusion: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/complete`, { changesRequired, conclusion });
  }
}
