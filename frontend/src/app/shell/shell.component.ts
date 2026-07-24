import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { I18nService, Lang } from '../core/i18n.service';
import { PermissionsService } from '../core/permissions.service';

/**
 * Application chrome per the QAMS Design System: 58px signature-gradient
 * header (navy → blue → teal) with a white logo box, and a white collapsible
 * grouped sidebar (Quality / Resources / Administration) whose active item is
 * soft-blue with a brand inline-start bar.
 */
@Component({
  selector: 'qams-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="app">
      <header class="hdr">
        <div class="logobox">
          <img src="assets/nt-qams-logo.png" alt="NT.QAMS" />
        </div>
        <div class="ctitle">{{ i18n.t('app.subtitle') }}</div>
        <div class="right">
          <div class="langtoggle">
            <select [value]="i18n.lang()" (change)="onLang($event)" aria-label="Language">
              <option value="en">EN</option>
              <option value="ar">AR</option>
              <option value="fr">FR</option>
            </select>
          </div>
          <div class="who">
            <span class="avatar">{{ initials() }}</span>
            <span class="nm">{{ auth.displayName() }}</span>
            <span class="rl">{{ auth.role() }}</span>
          </div>
          <button class="hbtn" (click)="signOut()">{{ i18n.t('nav.signout') }}</button>
        </div>
      </header>

      <div class="body">
        <nav class="nav">
          @if (perms.isPlatformAdmin()) {
            <!-- Control plane: platform administrators manage tenants, not lab records. -->
            <div class="grouplabel">{{ i18n.t('nav.groupPlatform') }}</div>
            <a class="item" routerLink="/platform/tenants" routerLinkActive="active">{{ i18n.t('nav.tenants') }}</a>
          } @else {
            <div class="grouplabel">{{ i18n.t('nav.groupQuality') }}</div>
            <a class="item" routerLink="/dashboard" routerLinkActive="active">{{ i18n.t('nav.dashboard') }}</a>
            <a class="item" routerLink="/tasks" routerLinkActive="active">{{ i18n.t('nav.tasks') }}</a>
            <a class="item" routerLink="/nonconformances" routerLinkActive="active">{{ i18n.t('nav.nc') }}</a>
            <a class="item" routerLink="/audits" routerLinkActive="active">{{ i18n.t('nav.audits') }}</a>
            <a class="item" routerLink="/risks" routerLinkActive="active">{{ i18n.t('nav.risks') }}</a>
            <a class="item" routerLink="/changes" routerLinkActive="active">{{ i18n.t('nav.changes') }}</a>
            <a class="item" routerLink="/management-reviews" routerLinkActive="active">{{ i18n.t('nav.reviews') }}</a>

            <div class="grouplabel">{{ i18n.t('nav.groupResources') }}</div>
            <a class="item" routerLink="/documents" routerLinkActive="active">{{ i18n.t('nav.documents') }}</a>
            <a class="item" routerLink="/equipment" routerLinkActive="active">{{ i18n.t('nav.equipment') }}</a>
            <a class="item" routerLink="/competencies" routerLinkActive="active">{{ i18n.t('nav.competency') }}</a>
            <a class="item" routerLink="/training" routerLinkActive="active">{{ i18n.t('nav.training') }}</a>
            <a class="item" routerLink="/suppliers" routerLinkActive="active">{{ i18n.t('nav.suppliers') }}</a>

            <div class="grouplabel">{{ i18n.t('nav.groupAnalytical') }}</div>
            <a class="item" routerLink="/qc" routerLinkActive="active">{{ i18n.t('nav.qc') }}</a>
            <a class="item" routerLink="/validation-studies" routerLinkActive="active">{{ i18n.t('nav.validation') }}</a>
            <a class="item" routerLink="/proficiency-tests" routerLinkActive="active">{{ i18n.t('nav.pt') }}</a>

            <div class="grouplabel">{{ i18n.t('nav.groupAdmin') }}</div>
            <a class="item" routerLink="/records" routerLinkActive="active">{{ i18n.t('nav.records') }}</a>
            <a class="item" routerLink="/notifications" routerLinkActive="active">{{ i18n.t('nav.notifications') }}</a>
            @if (perms.canViewCompliance()) {
              <a class="item" routerLink="/compliance" routerLinkActive="active">{{ i18n.t('nav.compliance') }}</a>
            }
            @if (perms.canApprove()) {
              <a class="item" routerLink="/notification-rules" routerLinkActive="active">{{ i18n.t('nav.notificationRules') }}</a>
            }
            @if (perms.isTenantAdmin()) {
              <a class="item" routerLink="/users" routerLinkActive="active">{{ i18n.t('nav.users') }}</a>
            }
          }
        </nav>

        <main>
          <div class="wrap">
            <router-outlet />
          </div>
        </main>
      </div>
    </div>
  `,
  styles: [`
    .app { height: 100vh; display: flex; flex-direction: column; overflow: hidden; }

    /* ---------- signature-gradient header ---------- */
    .hdr {
      height: 58px; background: var(--nt-header-grad); display: flex; align-items: center;
      gap: 14px; padding: 0 16px; flex-shrink: 0; color: #fff;
    }
    .logobox { background: #fff; border-radius: 6px; padding: 6px 12px; display: flex; align-items: center; }
    .logobox img { height: 30px; display: block; }
    .ctitle { font-size: 15px; font-weight: 700; letter-spacing: .01em; opacity: .97; }
    .right { margin-inline-start: auto; display: flex; align-items: center; gap: 10px; }
    .langtoggle select {
      width: auto; background: transparent; border: 1px solid rgba(255,255,255,.4);
      border-radius: 999px; padding: 5px 26px 5px 12px; font-weight: 600; font-size: 12.5px; color: #fff;
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='14' height='14' viewBox='0 0 24 24' fill='none' stroke='white' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpath d='M6 9l6 6 6-6'/%3E%3C/svg%3E");
      background-position: right 8px center;
    }
    .langtoggle select:focus { box-shadow: 0 0 0 2px rgba(255,255,255,.25); border-color: #fff; }
    .langtoggle option { color: var(--nt-slate); }
    .who { display: flex; align-items: center; gap: 8px; }
    .avatar {
      width: 32px; height: 32px; border-radius: 50%; background: rgba(255,255,255,.22);
      display: inline-flex; align-items: center; justify-content: center; font-size: 12px; font-weight: 700;
    }
    .nm { font-size: 13px; font-weight: 600; }
    .rl { font-size: 11px; opacity: .85; border: 1px solid rgba(255,255,255,.4); border-radius: 999px; padding: 2px 9px; }
    .hbtn { background: transparent; border: none; color: #fff; padding: 8px 10px; border-radius: 5px; font-size: 13px; font-weight: 600; }
    .hbtn:hover { background: rgba(255,255,255,.15); }

    .body { flex: 1; display: flex; overflow: hidden; }

    /* ---------- white grouped sidebar ---------- */
    .nav {
      width: 248px; background: #fff; border-inline-end: 1px solid var(--nt-border);
      flex-shrink: 0; overflow-y: auto; overflow-x: hidden; padding: 8px 0;
    }
    .grouplabel {
      font-size: 10.5px; font-weight: 700; letter-spacing: .1em; text-transform: uppercase;
      color: var(--nt-grey-m); padding: 13px 18px 5px; white-space: nowrap;
    }
    .item {
      display: flex; align-items: center; gap: 12px; padding: 9px 18px;
      font-size: 13.5px; font-weight: 500; color: var(--nt-slate);
      border-inline-start: 3px solid transparent; text-decoration: none; white-space: nowrap;
    }
    .item:hover { background: var(--nt-bg-grey); }
    .item.active {
      background: var(--nt-brand-soft); color: var(--nt-blue);
      border-inline-start-color: var(--nt-blue); font-weight: 600;
    }

    main { flex: 1; overflow-y: auto; }
    .wrap { padding: 20px 24px 48px; }
  `],
})
export class ShellComponent {
  readonly auth = inject(AuthService);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly router = inject(Router);

  /** Uppercase initials for the header avatar (max two). */
  readonly initials = computed(() => {
    const parts = this.auth.displayName().trim().split(/\s+/);
    return parts.slice(0, 2).map((p) => p[0]?.toUpperCase() ?? '').join('');
  });

  onLang(event: Event): void {
    this.i18n.setLang((event.target as HTMLSelectElement).value as Lang);
  }

  signOut(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
