import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, of } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';
import { I18nService, Lang } from './i18n.service';
import { MyPrivileges } from './models';

/**
 * The signed-in user's effective privileges, fetched from the server after each
 * sign-in (`/auth/me/privileges`) and re-fetched when the session changes. The
 * UI asks `can('documents.approve')` to decide what to OFFER; authoritative
 * enforcement stays on the server — this is affordance, never a security
 * boundary.
 *
 * Until the fetch lands, `can()` answers false: a button appearing a beat late
 * is a cosmetic cost, a button appearing wrongly is a broken promise.
 */
@Injectable({ providedIn: 'root' })
export class PermissionsService {
  private readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly i18n = inject(I18nService);

  private readonly privileges = signal<MyPrivileges | null>(null);

  /** Granted permission keys, as a set for O(1) template checks. */
  private readonly granted = computed(() => new Set(this.privileges()?.permissions ?? []));

  /** The display name of the user's configured role (for the shell header). */
  readonly roleName = computed(() => this.privileges()?.roleName ?? null);

  /** Allowed branches; empty means unrestricted (used by pickers as a hint). */
  readonly branchIds = computed(() => this.privileges()?.branchIds ?? []);

  /**
   * True for platform (cross-tenant) administrators. Comes from the session
   * tier, not the fetched privileges, so route guards work synchronously at
   * bootstrap instead of racing the privileges request.
   */
  readonly isPlatformAdmin = computed(() => this.auth.role() === 'PlatformAdmin');

  /** Whether an e-signature PIN is on file, so signing can be offered honestly. */
  readonly pinConfigured = computed(() => this.privileges()?.pinConfigured ?? false);

  constructor() {
    effect(() => {
      if (!this.auth.isAuthenticated()) {
        this.privileges.set(null);
        return;
      }

      if (this.isPlatformAdmin()) {
        return; // The platform surface is tier-gated; no tenant privileges exist.
      }

      this.http
        .get<MyPrivileges>(`${environment.apiBaseUrl}/auth/me/privileges`)
        .pipe(catchError(() => of(null)))
        .subscribe((p) => {
          this.privileges.set(p);
          const lang = p?.preferredLanguage;
          if (lang === 'en' || lang === 'ar' || lang === 'fr') {
            this.i18n.setLang(lang as Lang);
          }
        });
    });
  }

  /** Re-pulls the privilege snapshot — call after a credential change (e.g. PIN set). */
  refresh(): void {
    if (!this.auth.isAuthenticated() || this.isPlatformAdmin()) { return; }
    this.http
      .get<MyPrivileges>(`${environment.apiBaseUrl}/auth/me/privileges`)
      .pipe(catchError(() => of(null)))
      .subscribe((p) => { if (p) { this.privileges.set(p); } });
  }

  /** True when the user's role grants this permission key (e.g. "nc.approve"). */
  can(key: string): boolean {
    return this.isPlatformAdmin() || this.granted().has(key);
  }

  /** True when any of the keys is granted — for controls shared by sibling actions. */
  canAny(...keys: string[]): boolean {
    return keys.some((k) => this.can(k));
  }

  /**
   * Persists the user's own language choice so it follows them to the next
   * device; the switcher applies it locally regardless.
   */
  saveMyLanguage(language: Lang): void {
    if (!this.auth.isAuthenticated()) {
      return;
    }

    this.http
      .put(`${environment.apiBaseUrl}/auth/me/language`, { language })
      .pipe(catchError(() => of(void 0)))
      .subscribe();
  }
}
