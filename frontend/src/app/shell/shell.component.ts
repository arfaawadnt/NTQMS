import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { I18nService, Lang } from '../core/i18n.service';
import { PermissionsService } from '../core/permissions.service';

/** One sidebar destination: route, i18n label key, and its descriptive icon. */
interface NavItem {
  path: string;
  label: string;
  icon: string;
  visible?: () => boolean;
}

/** A collapsible sidebar group. */
interface NavGroup {
  key: string;
  label: string;
  items: NavItem[];
}

/**
 * Feather-style single-path icons (compound subpaths in one `d`), stroked with
 * currentColor so they inherit the item's active/hover color.
 */
const ICONS: Record<string, string> = {
  dashboard: 'M3 3h7v7H3z M14 3h7v7h-7z M14 14h7v7h-7z M3 14h7v7H3z',
  tasks: 'M9 11l3 3L22 4 M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11',
  bell: 'M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9 M13.73 21a2 2 0 0 1-3.46 0',
  nc: 'M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z M12 9v4 M12 17h.01',
  complaints: 'M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z',
  feedback: 'M14 9V5a3 3 0 0 0-3-3l-4 9v11h11.28a2 2 0 0 0 2-1.7l1.38-9a2 2 0 0 0-2-2.3z M7 22H4a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2h3',
  audits: 'M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2 M9 2h6a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1H9a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1z',
  objectives: 'M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z M4 22v-7',
  changes: 'M23 4v6h-6 M1 20v-6h6 M3.51 9a9 9 0 0 1 14.85-3.36L23 10 M1 14l4.64 4.36A9 9 0 0 0 20.49 15',
  reviews: 'M3 3h18v12H3z M8 21l4-4 4 4',
  documents: 'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M16 13H8 M16 17H8',
  records: 'M21 8v13H3V8 M1 3h22v5H1z M10 12h4',
  risks: 'M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z',
  coi: 'M12 3v18 M8 21h8 M5 7l7-4 7 4 M3 13a3 3 0 0 0 6 0L6 7l-3 6z M15 13a3 3 0 0 0 6 0l-3-6-3 6',
  context: 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z M2 12h20 M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z',
  equipment: 'M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z',
  standards: 'M12 15a7 7 0 1 0 0-14 7 7 0 0 0 0 14z M8.21 13.89L7 23l5-3 5 3-1.21-9.12',
  environment: 'M14 14.76V3.5a2.5 2.5 0 0 0-5 0v11.26a4.5 4.5 0 1 0 5 0z',
  suppliers: 'M1 3h15v13H1z M16 8h4l3 3v5h-7V8z M5.5 21a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z M18.5 21a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z',
  competencies: 'M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2 M8.5 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8z M17 11l2 2 4-4',
  authorizations: 'M21 2l-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0l3 3L22 7l-3-3m-3.5 3.5L19 4',
  training: 'M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z',
  qc: 'M22 12h-4l-3 9L9 3l-3 9H2',
  validation: 'M22 11.08V12a10 10 0 1 1-5.93-9.14 M22 4L12 14.01l-3-3',
  sigma: 'M18 4H6l6 8-6 8h12',
  refinterval: 'M4 6h16 M4 12h16 M4 18h16 M8 3v18 M16 3v18',
  detection: 'M2 20h20 M4 20V10 M9 20V6 M14 20v-3 M19 20V4 M4 10a5 5 0 0 1 10 0',
  linearity: 'M3 21L21 3 M6 18v.01 M9 15v.01 M12 12v.01 M15 9v.01 M18 6v.01',
  precision: 'M12 2v4 M12 18v4 M2 12h4 M18 12h4 M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M12 11v.01',
  methodcomp: 'M3 3v18h18 M7 15l4-5 3 3 5-7',
  outlier: 'M4 20h16 M7 16v.01 M11 15v.01 M9 17v.01 M13 16v.01 M18 5v.01 M8 15a4 4 0 1 0 0-1',
  carryover: 'M6 3v6a3 3 0 0 0 6 0V3 M6 21v-6a3 3 0 0 1 6 0v6 M18 8l3 3-3 3 M15 11h6',
  lotcompare: 'M4 4h7v16H4z M13 8h7v12h-7z M7 8h1 M7 12h1 M16 12h1',
  interference: 'M3 12h4l2-8 4 16 2-8h6',
  instrumentcompare: 'M4 5h6v14H4z M14 5h6v14h-6z M10 12h4',
  uncertainty: 'M19 5L5 19 M6.5 9a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z M17.5 20a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z',
  ptplan: 'M3 4h18v18H3z M16 2v4 M8 2v4 M3 10h18',
  pt: 'M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20z M22 12h-4 M6 12H2 M12 6V2 M12 22v-4',
  reference: 'M12 8c4.97 0 9-1.34 9-3s-4.03-3-9-3-9 1.34-9 3 4.03 3 9 3z M21 12c0 1.66-4.03 3-9 3s-9-1.34-9-3 M3 5v14c0 1.66 4.03 3 9 3s9-1.34 9-3V5',
  rules: 'M4 21v-7 M4 10V3 M12 21v-9 M12 8V3 M20 21v-5 M20 12V3 M1 14h6 M9 8h6 M17 16h6',
  compliance: 'M5 11h14v10H5z M7 11V7a5 5 0 0 1 10 0v4',
  users: 'M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2 M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8z M23 21v-2a4 4 0 0 0-3-3.87 M16 3.13a4 4 0 0 1 0 7.75',
  tenants: 'M12 2L2 7l10 5 10-5-10-5z M2 17l10 5 10-5 M2 12l10 5 10-5',
};

const SIDEBAR_COLLAPSED_KEY = 'qams.sidebar.collapsed';
const GROUPS_STATE_KEY = 'qams.sidebar.groups';

/**
 * Application chrome per the QAMS Design System: 58px signature-gradient
 * header and a white grouped sidebar. Groups expand/collapse individually
 * (first two open by default, state persisted); the whole sidebar collapses
 * to an icon rail — every page carries a descriptive icon — expanded on first
 * use until the user chooses otherwise.
 */
@Component({
  selector: 'qams-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="app">
      <header class="hdr">
        <button class="hbtn burger" (click)="toggleSidebar()" [attr.aria-label]="i18n.t('nav.toggleSidebar')" [attr.title]="i18n.t('nav.toggleSidebar')">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M3 12h18 M3 6h18 M3 18h18" />
          </svg>
        </button>
        <div class="logobox">
          <img src="assets/nt-qms-logo.svg" alt="NT.QMS" />
        </div>
        @if (!perms.isPlatformAdmin() && auth.tenantSlug(); as slug) {
          <span class="tenantchip">{{ slug }}</span>
        }
        <div class="ctitle">{{ i18n.t('app.subtitle') }}</div>
        <div class="right">
          @if (!perms.isPlatformAdmin()) {
            <a class="hicon" routerLink="/dashboard" [attr.title]="i18n.t('nav.dashboard')" [attr.aria-label]="i18n.t('nav.dashboard')">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z M9 22V12h6v10" />
              </svg>
            </a>
            <a class="hicon" routerLink="/tasks" [attr.title]="i18n.t('nav.tasks')" [attr.aria-label]="i18n.t('nav.tasks')">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M9 11l3 3L22 4 M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
              </svg>
            </a>
            <a class="hicon" routerLink="/notifications" [attr.title]="i18n.t('nav.notifications')" [attr.aria-label]="i18n.t('nav.notifications')">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9 M13.73 21a2 2 0 0 1-3.46 0" />
              </svg>
            </a>
          }
          <div class="langswitch" role="group" aria-label="Language">
            @for (l of langs; track l.code) {
              <button type="button" [class.active]="i18n.lang() === l.code" (click)="i18n.setLang(l.code)">{{ l.label }}</button>
            }
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
        <nav class="nav" [class.rail]="sidebarCollapsed()">
          @for (group of groups(); track group.key) {
            @if (!sidebarCollapsed()) {
              <button class="grouphead" (click)="toggleGroup(group.key)"
                      [attr.aria-expanded]="isOpen(group.key)">
                <span class="grouplabel">{{ i18n.t(group.label) }}</span>
                <svg class="chev" [class.open]="isOpen(group.key)" [class.rtl]="i18n.isRtl()" viewBox="0 0 24 24" fill="none"
                     stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="M9 18l6-6-6-6" />
                </svg>
              </button>
            } @else if (!$first) {
              <div class="railsep" role="separator"></div>
            }
            @if (sidebarCollapsed() || isOpen(group.key)) {
              @for (item of group.items; track item.path) {
                <a class="item" [routerLink]="item.path" routerLinkActive="active"
                   [attr.title]="sidebarCollapsed() ? i18n.t(item.label) : null">
                  <svg class="ic" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"
                       stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <path [attr.d]="icon(item.icon)" />
                  </svg>
                  @if (!sidebarCollapsed()) { <span class="lbl">{{ i18n.t(item.label) }}</span> }
                </a>
              }
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
    .logobox { background: #fff; border-radius: 6px; padding: 5px 12px; display: flex; align-items: center; }
    .logobox img { height: 32px; display: block; }
    .tenantchip {
      font-size: 12px; font-weight: 700; letter-spacing: .02em;
      border: 1px solid rgba(255,255,255,.5); border-radius: 999px; padding: 3px 12px;
      background: rgba(255,255,255,.12); white-space: nowrap;
    }
    .ctitle { font-size: 15px; font-weight: 700; letter-spacing: .01em; opacity: .97; }
    .right { margin-inline-start: auto; display: flex; align-items: center; gap: 8px; }
    .hicon {
      display: inline-flex; align-items: center; justify-content: center;
      width: 34px; height: 34px; border-radius: 8px; color: #fff;
    }
    .hicon:hover { background: rgba(255,255,255,.15); }
    .hicon svg { width: 18px; height: 18px; }
    .langswitch {
      display: inline-flex; background: rgba(255,255,255,.14); border-radius: 999px; padding: 3px;
      border: 1px solid rgba(255,255,255,.28);
    }
    .langswitch button {
      width: auto; background: transparent; color: #fff;
      font-size: 11.5px; font-weight: 700; padding: 4px 11px; border-radius: 999px; border: none;
    }
    .langswitch button.active { background: #fff; color: var(--nt-blue); }
    .who { display: flex; align-items: center; gap: 8px; }
    .avatar {
      width: 32px; height: 32px; border-radius: 50%; background: rgba(255,255,255,.22);
      display: inline-flex; align-items: center; justify-content: center; font-size: 12px; font-weight: 700;
    }
    .nm { font-size: 13px; font-weight: 600; }
    .rl { font-size: 11px; opacity: .85; border: 1px solid rgba(255,255,255,.4); border-radius: 999px; padding: 2px 9px; }
    .hbtn { background: transparent; border: none; color: #fff; padding: 8px 10px; border-radius: 5px; font-size: 13px; font-weight: 600; }
    .hbtn:hover { background: rgba(255,255,255,.15); }
    .burger { display: inline-flex; align-items: center; padding: 8px; }
    .burger svg { width: 20px; height: 20px; }

    .body { flex: 1; display: flex; overflow: hidden; }

    /* ---------- white grouped sidebar ---------- */
    .nav {
      width: 248px; background: #fff; border-inline-end: 1px solid var(--nt-border);
      flex-shrink: 0; overflow-y: auto; overflow-x: hidden; padding: 8px 0 24px;
      transition: width .18s ease;
    }
    .nav.rail { width: 56px; }

    .grouphead {
      width: 100%; display: flex; align-items: center; justify-content: space-between;
      background: transparent; border: none; cursor: pointer;
      padding: 13px 14px 5px 18px; text-align: start;
    }
    .grouphead:hover .grouplabel { color: var(--nt-blue); }
    .grouplabel {
      font-size: 10.5px; font-weight: 700; letter-spacing: .1em; text-transform: uppercase;
      color: var(--nt-grey-m); white-space: nowrap;
    }
    .chev { width: 13px; height: 13px; color: var(--nt-grey-m); flex-shrink: 0; transition: transform .15s ease; }
    /* Closed chevrons point INTO the reading direction; open always points down. */
    .chev.rtl { transform: rotate(180deg); }
    .chev.open { transform: rotate(90deg); }

    .item {
      display: flex; align-items: center; gap: 11px; padding: 8px 18px;
      font-size: 13.5px; font-weight: 500; color: var(--nt-slate);
      border-inline-start: 3px solid transparent; text-decoration: none; white-space: nowrap;
    }
    .item:hover { background: var(--nt-bg-grey); }
    .item.active {
      background: var(--nt-brand-soft); color: var(--nt-blue);
      border-inline-start-color: var(--nt-blue); font-weight: 600;
    }
    .ic { width: 17px; height: 17px; flex-shrink: 0; opacity: .85; }
    .item.active .ic { opacity: 1; }
    .lbl { overflow: hidden; text-overflow: ellipsis; }

    /* Icon rail: centered icons with group separators, labels as tooltips. */
    .nav.rail .item { justify-content: center; padding: 10px 0; gap: 0; }
    .nav.rail .ic { width: 19px; height: 19px; }
    .railsep { height: 1px; background: var(--nt-border); margin: 7px 12px; }

    main { flex: 1; overflow-y: auto; }
    .wrap { padding: 20px 24px 48px; }
  `],
})
export class ShellComponent {
  readonly auth = inject(AuthService);

  constructor() {
    // Part 11-friendly idle lockout for every authenticated session.
    this.auth.startIdleWatch();
    // The group holding the current page always opens, even if stored collapsed.
    const active = this.groups().find((g) => g.items.some((i) => this.router.url.startsWith(i.path)));
    if (active && !this.openGroups().has(active.key)) {
      this.openGroups.update((open) => new Set(open).add(active.key));
    }
  }

  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly router = inject(Router);

  /** Whole-sidebar rail mode: expanded on first use until the user collapses it. */
  readonly sidebarCollapsed = signal(localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === '1');

  /** Open group keys — first two groups open by default until the user decides. */
  readonly openGroups = signal<Set<string>>(this.restoreGroupState());

  /** The professional module map: overview → improvement → knowledge → risk → resources → people → analytical → admin. */
  readonly groups = computed<NavGroup[]>(() => {
    if (this.perms.isPlatformAdmin()) {
      return [{
        key: 'platform', label: 'nav.groupPlatform',
        items: [{ path: '/platform/tenants', label: 'nav.tenants', icon: 'tenants' }],
      }];
    }

    const all: NavGroup[] = [
      {
        key: 'overview', label: 'nav.groupOverview',
        items: [
          { path: '/dashboard', label: 'nav.dashboard', icon: 'dashboard' },
          { path: '/tasks', label: 'nav.tasks', icon: 'tasks' },
          { path: '/notifications', label: 'nav.notifications', icon: 'bell' },
        ],
      },
      {
        key: 'improvement', label: 'nav.groupImprovement',
        items: [
          { path: '/nonconformances', label: 'nav.nc', icon: 'nc' },
          { path: '/complaints', label: 'nav.complaints', icon: 'complaints' },
          { path: '/feedback', label: 'nav.feedback', icon: 'feedback' },
          { path: '/audits', label: 'nav.audits', icon: 'audits' },
          { path: '/quality-objectives', label: 'nav.objectives', icon: 'objectives' },
          { path: '/changes', label: 'nav.changes', icon: 'changes' },
          { path: '/management-reviews', label: 'nav.reviews', icon: 'reviews' },
        ],
      },
      {
        key: 'docs', label: 'nav.groupDocs',
        items: [
          { path: '/documents', label: 'nav.documents', icon: 'documents' },
          { path: '/records', label: 'nav.records', icon: 'records' },
        ],
      },
      {
        key: 'risk', label: 'nav.groupRisk',
        items: [
          { path: '/risks', label: 'nav.risks', icon: 'risks' },
          { path: '/conflicts', label: 'nav.coi', icon: 'coi' },
          { path: '/org-context', label: 'nav.ctx', icon: 'context' },
        ],
      },
      {
        key: 'resources', label: 'nav.groupResources',
        items: [
          { path: '/equipment', label: 'nav.equipment', icon: 'equipment' },
          { path: '/reference-standards', label: 'nav.standards', icon: 'standards' },
          { path: '/monitoring', label: 'nav.env', icon: 'environment' },
          { path: '/suppliers', label: 'nav.suppliers', icon: 'suppliers' },
        ],
      },
      {
        key: 'people', label: 'nav.groupPeople',
        items: [
          { path: '/competencies', label: 'nav.competency', icon: 'competencies' },
          { path: '/authorizations', label: 'nav.authz', icon: 'authorizations' },
          { path: '/training', label: 'nav.training', icon: 'training' },
        ],
      },
      {
        key: 'analytical', label: 'nav.groupAnalytical',
        items: [
          { path: '/qc', label: 'nav.qc', icon: 'qc' },
          { path: '/validation-studies', label: 'nav.validation', icon: 'validation' },
          { path: '/method-comparisons', label: 'nav.mc', icon: 'methodcomp' },
          { path: '/precision-studies', label: 'nav.prc', icon: 'precision' },
          { path: '/linearity-studies', label: 'nav.lin', icon: 'linearity' },
          { path: '/detection-limits', label: 'nav.dl', icon: 'detection' },
          { path: '/reference-intervals', label: 'nav.ri', icon: 'refinterval' },
          { path: '/sigma-metrics', label: 'nav.sig', icon: 'sigma' },
          { path: '/outlier-screenings', label: 'nav.out', icon: 'outlier' },
          { path: '/carryover-studies', label: 'nav.car', icon: 'carryover' },
          { path: '/lot-comparisons', label: 'nav.lot', icon: 'lotcompare' },
          { path: '/interference-studies', label: 'nav.inf', icon: 'interference' },
          { path: '/instrument-comparabilities', label: 'nav.icp', icon: 'instrumentcompare' },
          { path: '/uncertainty', label: 'nav.mu', icon: 'uncertainty' },
          { path: '/pt-plans', label: 'nav.ptp', icon: 'ptplan' },
          { path: '/proficiency-tests', label: 'nav.pt', icon: 'pt' },
        ],
      },
      {
        key: 'admin', label: 'nav.groupAdmin',
        items: [
          { path: '/reference-data', label: 'nav.reference', icon: 'reference' },
          { path: '/notification-rules', label: 'nav.notificationRules', icon: 'rules', visible: () => this.perms.canApprove() },
          { path: '/compliance', label: 'nav.compliance', icon: 'compliance', visible: () => this.perms.canViewCompliance() },
          { path: '/users', label: 'nav.users', icon: 'users', visible: () => this.perms.isTenantAdmin() },
        ],
      },
    ];

    return all
      .map((g) => ({ ...g, items: g.items.filter((i) => i.visible?.() ?? true) }))
      .filter((g) => g.items.length > 0);
  });

  /** Uppercase initials for the header avatar (max two). */
  readonly initials = computed(() => {
    const parts = this.auth.displayName().trim().split(/\s+/);
    return parts.slice(0, 2).map((p) => p[0]?.toUpperCase() ?? '').join('');
  });

  icon(name: string): string { return ICONS[name] ?? ICONS['dashboard']; }

  isOpen(key: string): boolean { return this.openGroups().has(key); }

  toggleGroup(key: string): void {
    this.openGroups.update((open) => {
      const next = new Set(open);
      if (next.has(key)) { next.delete(key); } else { next.add(key); }
      localStorage.setItem(GROUPS_STATE_KEY, JSON.stringify([...next]));
      return next;
    });
  }

  toggleSidebar(): void {
    this.sidebarCollapsed.update((collapsed) => {
      const next = !collapsed;
      localStorage.setItem(SIDEBAR_COLLAPSED_KEY, next ? '1' : '0');
      return next;
    });
  }

  private restoreGroupState(): Set<string> {
    const stored = localStorage.getItem(GROUPS_STATE_KEY);
    if (stored) {
      try {
        return new Set(JSON.parse(stored) as string[]);
      } catch { /* fall through to defaults */ }
    }

    // First visit: only the first two groups open (platform admins get their single group).
    return new Set(['overview', 'improvement', 'platform']);
  }

  /** Language switcher segments (Arabic shown by its own letter, per the prototype). */
  readonly langs: { code: Lang; label: string }[] = [
    { code: 'en', label: 'EN' },
    { code: 'ar', label: 'ع' },
    { code: 'fr', label: 'FR' },
  ];

  signOut(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
