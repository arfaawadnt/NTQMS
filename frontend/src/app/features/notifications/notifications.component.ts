import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { NotificationsApiService } from '../../core/api/notifications-api.service';
import { I18nService } from '../../core/i18n.service';
import { NotificationFeedItem } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { LoadMoreComponent } from '../../shared/ui/load-more.component';
import { ExportColumn, ExportMenuComponent } from '../../shared/ui/export-menu.component';

/** The signed-in user's in-app notification feed with mark-as-read and a load-more pager (R-3). */
@Component({
    selector: 'qams-notifications',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [DatePipe, PageHeaderComponent, LoadMoreComponent],
    template: `
    <qams-page-header [title]="i18n.t('notif.title')" />
    @if (loading() && items().length === 0) {
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
              <button class="ghost" (click)="markRead(n.id)">{{ i18n.t('notif.markRead') }}</button>
            }
          </div>
        }
      </div>
      <qams-load-more [shown]="items().length" [total]="total()" [hasMore]="hasMore()"
                      [loading]="loading()" (more)="loadMore()" />
    }
  `,
    styles: [`
    .feed { display: flex; flex-direction: column; gap: .6rem; }
    .item { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; }
    .item.unread { border-inline-start: 3px solid var(--nt-teal); }
    .subject { font-weight: 600; color: var(--nt-navy); }
    .meta { display: flex; gap: .6rem; align-items: center; margin-top: .4rem; }
  `]
})
export class NotificationsComponent implements OnInit {
  readonly i18n = inject(I18nService);
  private readonly api = inject(NotificationsApiService);

  readonly items = signal<NotificationFeedItem[]>([]);
  readonly total = signal(0);
  readonly hasMore = signal(false);
  readonly loading = signal(true);
  /** 1-based page of the last fetched slice (R-3 load-more pager). */
  private readonly page = signal(1);

  ngOnInit(): void {
    void this.load();
  }

  /** Loads the first page, replacing the feed. */
  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const page = await firstValueFrom(this.api.mine());
      this.page.set(1);
      this.items.set(page.items);
      this.total.set(page.total);
      this.hasMore.set(page.hasMore);
    } finally {
      this.loading.set(false);
    }
  }

  /** Appends the next page of the feed (R-3); no-op while loading or exhausted. */
  async loadMore(): Promise<void> {
    if (this.loading() || !this.hasMore()) { return; }
    this.loading.set(true);
    try {
      const next = this.page() + 1;
      const page = await firstValueFrom(this.api.mine(false, next));
      this.page.set(next);
      this.items.update((items) => [...items, ...page.items]);
      this.total.set(page.total);
      this.hasMore.set(page.hasMore);
    } finally {
      this.loading.set(false);
    }
  }

  async markRead(id: string): Promise<void> {
    await firstValueFrom(this.api.markRead(id));
    await this.load();
  }
}
