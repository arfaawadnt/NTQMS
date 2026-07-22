import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NcFacade } from '../nc/nc.facade';
import { NotificationsApiService } from '../../core/api/notifications-api.service';
import { I18nService } from '../../core/i18n.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';

/** Quality dashboard: live KPI cards derived from the NC register and the notification feed. */
@Component({
  selector: 'qams-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, PageHeaderComponent],
  template: `
    <qams-page-header [title]="i18n.t('dash.title')" />
    @if (loading()) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else {
      <div class="kpis">
        <a class="card kpi" routerLink="/nonconformances">
          <div class="value">{{ facade.openCount() }}</div>
          <div class="label">{{ i18n.t('dash.openNc') }}</div>
        </a>
        <a class="card kpi" routerLink="/nonconformances">
          <div class="value danger">{{ facade.highRpnCount() }}</div>
          <div class="label">{{ i18n.t('dash.highRpn') }}</div>
        </a>
        <a class="card kpi" routerLink="/notifications">
          <div class="value">{{ unread() }}</div>
          <div class="label">{{ i18n.t('dash.unread') }}</div>
        </a>
      </div>
    }
  `,
  styles: [`
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; }
    .kpi { text-decoration: none; color: inherit; display: block; }
    .kpi:hover { border-color: var(--nt-blue); }
    .value { font-size: 2.4rem; font-weight: 700; color: var(--nt-navy); }
    .value.danger { color: var(--nt-danger); }
    .label { color: var(--nt-muted); margin-top: .25rem; }
  `],
})
export class DashboardComponent implements OnInit {
  readonly facade = inject(NcFacade);
  readonly i18n = inject(I18nService);
  private readonly notifications = inject(NotificationsApiService);

  readonly loading = signal(true);
  readonly unread = signal(0);

  async ngOnInit(): Promise<void> {
    try {
      await this.facade.loadList();
      this.unread.set((await firstValueFrom(this.notifications.mine(true))).length);
    } finally {
      this.loading.set(false);
    }
  }
}
