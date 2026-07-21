import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { QamsApiService } from '../../core/qams-api.service';
import { I18nService } from '../../core/i18n.service';
import { NotificationFeedItem } from '../../core/models';

@Component({
  selector: 'qams-notifications',
  standalone: true,
  imports: [DatePipe],
  template: `
    <h1>{{ i18n.t('notif.title') }}</h1>
    @if (loading()) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (items().length === 0) {
      <p class="muted">{{ i18n.t('notif.empty') }}</p>
    } @else {
      <div class="feed">
        @for (n of items(); track n.id) {
          <div class="card item" [class.unread]="!n.read">
            <div class="body">
              <div class="subject">{{ n.subject }}</div>
              <div class="muted">{{ n.body }}</div>
              <div class="meta">
                <span class="pill">{{ n.eventKey }}</span>
                <span class="muted">{{ n.createdAtUtc | date:'short' }}</span>
              </div>
            </div>
            @if (!n.read) {
              <button class="ghost" (click)="markRead(n)">{{ i18n.t('notif.markRead') }}</button>
            }
          </div>
        }
      </div>
    }
  `,
  styles: [`
    .feed { display: flex; flex-direction: column; gap: .6rem; }
    .item { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; }
    .item.unread { border-inline-start: 3px solid var(--nt-teal); }
    .subject { font-weight: 600; color: var(--nt-navy); }
    .meta { display: flex; gap: .6rem; align-items: center; margin-top: .4rem; }
  `],
})
export class NotificationsComponent implements OnInit {
  readonly i18n = inject(I18nService);
  private readonly api = inject(QamsApiService);

  readonly items = signal<NotificationFeedItem[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.myNotifications().subscribe({
      next: (items) => { this.items.set(items); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  markRead(n: NotificationFeedItem): void {
    this.api.markNotificationRead(n.id).subscribe({ next: () => this.load() });
  }
}
