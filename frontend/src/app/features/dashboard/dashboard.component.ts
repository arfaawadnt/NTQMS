import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { QamsApiService } from '../../core/qams-api.service';
import { I18nService } from '../../core/i18n.service';

@Component({
  selector: 'qams-dashboard',
  standalone: true,
  imports: [RouterLink],
  template: `
    <h1>{{ i18n.t('dash.title') }}</h1>
    @if (loading()) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else {
      <div class="kpis">
        <a class="card kpi" routerLink="/nonconformances">
          <div class="value">{{ openNc() }}</div>
          <div class="label">{{ i18n.t('dash.openNc') }}</div>
        </a>
        <a class="card kpi" routerLink="/nonconformances">
          <div class="value danger">{{ highRpn() }}</div>
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
  readonly i18n = inject(I18nService);
  private readonly api = inject(QamsApiService);

  readonly loading = signal(true);
  readonly openNc = signal(0);
  readonly highRpn = signal(0);
  readonly unread = signal(0);

  ngOnInit(): void {
    forkJoin({
      ncs: this.api.listNcs(),
      unread: this.api.myNotifications(true),
    }).subscribe({
      next: ({ ncs, unread }) => {
        this.openNc.set(ncs.filter((n) => n.status !== 'Closed' && n.status !== 'Rejected').length);
        this.highRpn.set(ncs.filter((n) => n.rpn > 12).length);
        this.unread.set(unread.length);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
