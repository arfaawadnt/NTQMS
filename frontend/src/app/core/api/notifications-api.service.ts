import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatedResource, DispatchMonitorItem, NotificationFeedItem, NotificationRule,
  Paged, UpsertNotificationRuleRequest,
} from '../models';

/**
 * Typed client for notifications: the signed-in user's feed, plus the
 * QM/TenantAdmin administration surface (rules and the dispatch monitor).
 */
@Injectable({ providedIn: 'root' })
export class NotificationsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/notifications`;

  mine(unreadOnly = false): Observable<Paged<NotificationFeedItem>> {
    return this.http.get<Paged<NotificationFeedItem>>(`${this.base}/mine?unreadOnly=${unreadOnly}`);
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/read`, {});
  }

  rules(): Observable<NotificationRule[]> {
    return this.http.get<NotificationRule[]>(`${this.base}/rules`);
  }

  upsertRule(body: UpsertNotificationRuleRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(`${this.base}/rules`, body);
  }

  monitor(status?: string): Observable<Paged<DispatchMonitorItem>> {
    let params = new HttpParams();
    if (status) { params = params.set('status', status); }
    return this.http.get<Paged<DispatchMonitorItem>>(`${this.base}/monitor`, { params });
  }
}
