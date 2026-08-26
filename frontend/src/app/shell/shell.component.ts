import { Component, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { I18nService, Lang } from '../core/i18n.service';
import { PermissionsService } from '../core/permissions.service';
import { NAV_ICONS } from '../core/nav-icons';
import { ChangeReasonDialogComponent } from '../core/change-reason-dialog.component';
import { TextPromptDialogComponent } from '../core/text-prompt-dialog.component';
import { MyAccountDrawerComponent } from './my-account-drawer.component';
import { PageHelpComponent } from '../shared/ui/page-help.component';

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

// Sidebar/help/manual icon geometry lives in one shared module (core/nav-icons).
const ICONS = NAV_ICONS;

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
    imports: [RouterOutlet, RouterLink, RouterLinkActive, PageHelpComponent, ChangeReasonDialogComponent, TextPromptDialogComponent, MyAccountDrawerComponent],
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
              <button type="button" [class.active]="i18n.lang() === l.code" (click)="switchLang(l.code)">{{ l.label }}</button>
            }
          </div>
          <button class="who" type="button" (click)="accountOpen.set(true)"
                  [attr.aria-label]="i18n.t('acct.title')" aria-haspopup="dialog">
            <span class="avatar">{{ initials() }}</span>
            <span class="nm">{{ auth.displayName() }}</span>
            <span class="rl">{{ auth.role() }}</span>
          </button>
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

    <!-- Global per-page help popup, opened from the page-header ? icon. -->
    <qams-page-help />

    <!-- Part 11 reason-for-change modal (UI-014), overlaying every screen. -->
    <qams-change-reason-dialog />

    <!-- Accessible text/password prompt modal (R-4), overlaying every screen. -->
    <qams-text-prompt-dialog />
    <qams-my-account-drawer [(open)]="accountOpen" />
  `,
    changeDetection: ChangeDetectionStrategy.Eager,
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
    /* The user identity is a button (opens My account) — strip the global
       button chrome so it keeps reading as part of the header. */
    .who { display: flex; align-items: center; gap: 8px; background: none; border: none;
           padding: 4px 8px; border-radius: var(--nt-radius-btn); cursor: pointer; color: inherit; }
    .who:hover { background: rgba(255, 255, 255, 0.12); }
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
  `]
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

  /** My-account drawer visibility (header user button). */
  readonly accountOpen = signal(false);
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
          { path: '/quality-analytics', label: 'nav.qualityAnalytics', icon: 'analytics',
            visible: () => this.perms.can('reports.view') },
          { path: '/tasks', label: 'nav.tasks', icon: 'tasks' },
          { path: '/notifications', label: 'nav.notifications', icon: 'bell' },
          { path: '/manual', label: 'nav.manual', icon: 'manual' },
        ],
      },
      {
        key: 'clinical', label: 'nav.groupClinical',
        items: [
          { path: '/patient-safety', label: 'nav.patientSafety', icon: 'patientSafety' },
          { path: '/infection-control', label: 'nav.infectionControl', icon: 'infectionControl' },
          { path: '/mortality-review', label: 'nav.mortalityReview', icon: 'mortalityReview' },
          { path: '/credentialing', label: 'nav.credentialing', icon: 'credentialing' },
          { path: '/eoc', label: 'nav.eoc', icon: 'eoc' },
        ],
      },
      {
        key: 'improvement', label: 'nav.groupImprovement',
        items: [
          { path: '/incidents', label: 'nav.incidents', icon: 'incidents' },
          { path: '/nonconformances', label: 'nav.nc', icon: 'nc' },
          { path: '/complaints', label: 'nav.complaints', icon: 'complaints' },
          { path: '/surveys', label: 'nav.surveys', icon: 'surveys' },
          { path: '/feedback', label: 'nav.feedback', icon: 'feedback' },
          { path: '/audits', label: 'nav.audits', icon: 'audits' },
          { path: '/audit-programs', label: 'nav.auditPrograms', icon: 'auditPrograms' },
          { path: '/quality-objectives', label: 'nav.objectives', icon: 'objectives' },
          { path: '/indicators', label: 'nav.indicators', icon: 'indicators' },
          { path: '/changes', label: 'nav.changes', icon: 'changes' },
          { path: '/management-reviews', label: 'nav.reviews', icon: 'reviews' },
          { path: '/committees', label: 'nav.committees', icon: 'committees' },
        ],
      },
      {
        key: 'docs', label: 'nav.groupDocs',
        items: [
          { path: '/quality-policy', label: 'nav.qualityPolicy', icon: 'qualityPolicy' },
          { path: '/documents', label: 'nav.documents', icon: 'documents' },
          { path: '/records', label: 'nav.records', icon: 'records' },
        ],
      },
      {
        key: 'risk', label: 'nav.groupRisk',
        items: [
          { path: '/risks', label: 'nav.risks', icon: 'risks' },
          { path: '/fmea', label: 'nav.fmea', icon: 'fmea' },
          { path: '/standards', label: 'nav.accreditation', icon: 'accreditation' },
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
          { path: '/training-catalogue', label: 'nav.trainingCatalogue', icon: 'trainingCatalogue' },
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
          { path: '/settings/security', label: 'nav.security', icon: 'security' },
          { path: '/integration', label: 'nav.integration', icon: 'integration', visible: () => this.perms.can('integration.view') },
          { path: '/reference-data', label: 'nav.reference', icon: 'reference' },
          { path: '/notification-rules', label: 'nav.notificationRules', icon: 'rules', visible: () => this.perms.can('notifications.manage') },
          { path: '/mail-management', label: 'nav.mailManagement', icon: 'mail', visible: () => this.perms.can('notifications.manage') },
          { path: '/compliance', label: 'nav.compliance', icon: 'compliance', visible: () => this.perms.can('compliance.view') },
          { path: '/users', label: 'nav.users', icon: 'users', visible: () => this.perms.can('users.view') },
          { path: '/roles', label: 'nav.roles', icon: 'accessReview', visible: () => this.perms.can('roles.view') },
          { path: '/access-reviews', label: 'nav.accessReviews', icon: 'accessReview', visible: () => this.perms.can('access-reviews.view') },
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

  /** Applies the language locally and saves it as the user's own preference. */
  switchLang(lang: Lang): void {
    this.i18n.setLang(lang);
    this.perms.saveMyLanguage(lang);
  }
}
