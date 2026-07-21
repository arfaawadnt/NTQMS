import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { NcDetail, NcListItem, NotificationFeedItem } from './models';

/** Typed client for the tenant-facing API surface used by this frontend slice. */
@Injectable({ providedIn: 'root' })
export class QamsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  listNcs(status?: string): Observable<NcListItem[]> {
    const query = status ? `?status=${encodeURIComponent(status)}` : '';
    return this.http.get<NcListItem[]>(`${this.base}/nonconformances${query}`);
  }

  getNc(id: string): Observable<NcDetail> {
    return this.http.get<NcDetail>(`${this.base}/nonconformances/${id}`);
  }

  raiseNc(body: {
    title: string; description: string; severity: number; likelihood: number; sourceType: string;
  }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/nonconformances`, body);
  }

  submitNc(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/nonconformances/${id}/submit`, {});
  }

  myNotifications(unreadOnly = false): Observable<NotificationFeedItem[]> {
    return this.http.get<NotificationFeedItem[]>(`${this.base}/notifications/mine?unreadOnly=${unreadOnly}`);
  }

  markNotificationRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/notifications/${id}/read`, {});
  }
}
