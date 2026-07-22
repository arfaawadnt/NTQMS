import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { NotificationFeedItem } from '../models';

/** Typed client for the signed-in user's notification feed. */
@Injectable({ providedIn: 'root' })
export class NotificationsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/notifications`;

  mine(unreadOnly = false): Observable<NotificationFeedItem[]> {
    return this.http.get<NotificationFeedItem[]>(`${this.base}/mine?unreadOnly=${unreadOnly}`);
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/read`, {});
  }
}
