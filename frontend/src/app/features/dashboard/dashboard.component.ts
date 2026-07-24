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
        <a class="kpi blue" routerLink="/nonconformances">
          <div class="n">{{ facade.openCount() }}</div>
          <div class="l">{{ i18n.t('dash.openNc') }}</div>
        </a>
        <a class="kpi red" routerLink="/nonconformances">
          <div class="n">{{ facade.highRpnCount() }}</div>
          <div class="l">{{ i18n.t('dash.highRpn') }}</div>
        </a>
        <a class="kpi teal" routerLink="/notifications">
          <div class="n">{{ unread() }}</div>
          <div class="l">{{ i18n.t('dash.unread') }}</div>
        </a>
      </div>
    }
  `,
  styles: [`
    /* KPI tiles per the design system: solid semantic fills, tabular numerals. */
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 12px; }
    .kpi {
      border-radius: var(--nt-radius-card); padding: 14px 16px; color: #fff;
      box-shadow: var(--nt-shadow-xs); text-decoration: none; display: block;
      transition: filter .12s;
    }
    .kpi:hover { filter: brightness(1.06); }
    .kpi.blue { background: var(--nt-blue); }
    .kpi.red { background: var(--nt-red); }
    .kpi.teal { background: var(--nt-teal); }
    .n { font-size: 26px; font-weight: 800; line-height: 1; font-variant-numeric: tabular-nums; }
    .l { font-size: 12px; font-weight: 600; margin-top: 7px; opacity: .96; }
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
