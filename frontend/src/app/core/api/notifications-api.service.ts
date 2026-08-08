import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatedResource, DEFAULT_PAGE_SIZE, DispatchMonitorItem, MailSettings, NotificationFeedItem,
  NotificationRule, Paged, UpdateMailSettingsRequest, UpsertNotificationRuleRequest,
} from '../models';

/**
 * Typed client for notifications: the signed-in user's feed, plus the
 * QM/TenantAdmin administration surface (rules and the dispatch monitor).
 */
@Injectable({ providedIn: 'root' })
export class NotificationsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/notifications`;

  mine(unreadOnly = false, page = 1, pageSize = DEFAULT_PAGE_SIZE): Observable<Paged<NotificationFeedItem>> {
    const params = new HttpParams().set('unreadOnly', unreadOnly).set('page', page).set('pageSize', pageSize);
    return this.http.get<Paged<NotificationFeedItem>>(`${this.base}/mine`, { params });
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

  mailSettings(): Observable<MailSettings> {
    return this.http.get<MailSettings>(`${this.base}/mail-settings`);
  }

  updateMailSettings(body: UpdateMailSettingsRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/mail-settings`, body);
  }
}
