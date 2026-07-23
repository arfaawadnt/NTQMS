import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { I18nService, Lang } from '../core/i18n.service';
import { PermissionsService } from '../core/permissions.service';

@Component({
  selector: 'qams-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="shell">
      <aside class="qams-sidebar">
        <div class="brand">{{ i18n.t('app.title') }}</div>
        <nav>
          <a routerLink="/dashboard" routerLinkActive="active">{{ i18n.t('nav.dashboard') }}</a>
          <a routerLink="/nonconformances" routerLinkActive="active">{{ i18n.t('nav.nc') }}</a>
          <a routerLink="/documents" routerLinkActive="active">{{ i18n.t('nav.documents') }}</a>
          <a routerLink="/audits" routerLinkActive="active">{{ i18n.t('nav.audits') }}</a>
          <a routerLink="/equipment" routerLinkActive="active">{{ i18n.t('nav.equipment') }}</a>
          <a routerLink="/competencies" routerLinkActive="active">{{ i18n.t('nav.competency') }}</a>
          <a routerLink="/training" routerLinkActive="active">{{ i18n.t('nav.training') }}</a>
          <a routerLink="/risks" routerLinkActive="active">{{ i18n.t('nav.risks') }}</a>
          <a routerLink="/changes" routerLinkActive="active">{{ i18n.t('nav.changes') }}</a>
          <a routerLink="/notifications" routerLinkActive="active">{{ i18n.t('nav.notifications') }}</a>
          @if (perms.isTenantAdmin()) {
            <a routerLink="/users" routerLinkActive="active">{{ i18n.t('nav.users') }}</a>
          }
        </nav>
      </aside>

      <div class="main">
        <header>
          <div class="who">
            <strong>{{ auth.displayName() }}</strong>
            <span class="pill">{{ auth.role() }}</span>
          </div>
          <div class="actions">
            <select [value]="i18n.lang()" (change)="onLang($event)" aria-label="Language">
              <option value="en">EN</option>
              <option value="ar">AR</option>
              <option value="fr">FR</option>
            </select>
            <button class="ghost" (click)="signOut()">{{ i18n.t('nav.signout') }}</button>
          </div>
        </header>
        <main>
          <router-outlet />
        </main>
      </div>
    </div>
  `,
  styles: [`
    .shell { display: flex; min-height: 100vh; }
    .qams-sidebar {
      width: 240px; background: var(--nt-navy); color: #fff;
      padding: 1.25rem 0; border-right: 1px solid var(--nt-border); flex-shrink: 0;
    }
    .brand { font-weight: 700; font-size: 1.15rem; padding: 0 1.25rem 1rem; letter-spacing: .3px; }
    nav { display: flex; flex-direction: column; }
    nav a {
      color: #cdd8e6; text-decoration: none; padding: .7rem 1.25rem; border-inline-start: 3px solid transparent;
    }
    nav a:hover { background: rgba(255,255,255,.06); color: #fff; }
    nav a.active { background: rgba(255,255,255,.1); color: #fff; border-inline-start-color: var(--nt-teal); }
    .main { flex: 1; display: flex; flex-direction: column; min-width: 0; }
    header {
      height: 58px; background: var(--nt-surface); border-bottom: 1px solid var(--nt-border);
      display: flex; align-items: center; justify-content: space-between; padding: 0 1.5rem;
    }
    .who { display: flex; align-items: center; gap: .6rem; }
    .actions { display: flex; align-items: center; gap: .6rem; }
    .actions select { width: auto; }
    main { padding: 1.5rem; overflow: auto; }
  `],
})
export class ShellComponent {
  readonly auth = inject(AuthService);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly router = inject(Router);

  onLang(event: Event): void {
    this.i18n.setLang((event.target as HTMLSelectElement).value as Lang);
  }

  signOut(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
