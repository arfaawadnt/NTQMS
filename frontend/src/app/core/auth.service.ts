import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, shareReplay, tap, map, catchError } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse } from './models';

interface Session {
  token: string;
  role: string;
  displayName: string;
  tenantId: string | null;
  expiresAtUtc: string;
}

const TENANT_SLUG_KEY = 'qams.tenant.slug';

/**
 * Holds the JWT session and drives the login / MFA / PIN flows.
 *
 * ADR-0009: the access token lives in MEMORY ONLY — never web storage, so an
 * XSS cannot exfiltrate a durable credential. Session continuity across a page
 * reload comes from the rotating, httpOnly refresh cookie via a silent refresh
 * at bootstrap; the token itself is short-lived and silently renewed. Only the
 * tenant slug (a UX convenience, not a secret) is persisted locally.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly base = `${environment.apiBaseUrl}/auth`;
  private readonly session = signal<Session | null>(null);

  /** Single-flight guard so concurrent 401s trigger exactly one refresh. */
  private refreshInFlight$: Observable<string | null> | null = null;

  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly displayName = computed(() => this.session()?.displayName ?? '');
  readonly role = computed(() => this.session()?.role ?? '');
  readonly tenantId = computed(() => this.session()?.tenantId ?? null);

  /**
   * The laboratory this browser signs into, taken from the tenant's own URL
   * (/t/{slug}) — never typed at the login form. Survives logout so the next
   * sign-in stays on the same lab. Not a credential.
   */
  private readonly _tenantSlug = signal<string | null>(localStorage.getItem(TENANT_SLUG_KEY));
  readonly tenantSlug = this._tenantSlug.asReadonly();

  setTenantSlug(slug: string | null): void {
    const normalized = slug?.trim().toLowerCase() || null;
    if (normalized) {
      localStorage.setItem(TENANT_SLUG_KEY, normalized);
    } else {
      localStorage.removeItem(TENANT_SLUG_KEY);
    }

    this._tenantSlug.set(normalized);
  }

  constructor(private readonly http: HttpClient) {}

  get token(): string | null {
    return this.session()?.token ?? null;
  }

  login(tenantIdentifier: string | null, email: string, password: string, mfaCode: string | null):
    Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.base}/login`, { tenantIdentifier, email, password, mfaCode },
        { withCredentials: true })
      .pipe(tap((res) => {
        if (!res.mfaRequired && res.accessToken) {
          this.apply(res);
        }
      }));
  }

  /**
   * Silent refresh (ADR-0009): swaps the rotating httpOnly cookie for a fresh
   * access token held in memory. Single-flight — concurrent callers (e.g. a
   * burst of 401s) share one in-flight request. Resolves to the new token, or
   * null when there is no valid session.
   */
  refresh(): Observable<string | null> {
    this.refreshInFlight$ ??= this.http
      .post<AuthResponse>(`${this.base}/refresh`, {}, { withCredentials: true })
      .pipe(
        map((res) => {
          this.apply(res);
          return res.accessToken;
        }),
        catchError(() => {
          this.clear();
          return of(null);
        }),
        tap({ finalize: () => (this.refreshInFlight$ = null) }),
        shareReplay(1),
      );
    return this.refreshInFlight$;
  }

  /** Bootstrap hydration: attempt one silent refresh so a reload keeps the session. */
  hydrate(): Promise<void> {
    return new Promise((resolve) => {
      this.refresh().subscribe({ next: () => resolve(), error: () => resolve() });
    });
  }

  enrollMfa(): Observable<{ secret: string; otpAuthUri: string }> {
    return this.http.post<{ secret: string; otpAuthUri: string }>(`${this.base}/mfa/enroll`, {});
  }

  confirmMfa(code: string): Observable<void> {
    return this.http.post<void>(`${this.base}/mfa/confirm`, { code });
  }

  setPin(pin: string): Observable<void> {
    return this.http.post<void>(`${this.base}/signature-pin`, { pin });
  }

  /** Current tenant's privileged-MFA enforcement policy (TenantAdmin). */
  getTenantMfaPolicy(): Observable<{ requireMfaForPrivilegedRoles: boolean }> {
    return this.http.get<{ requireMfaForPrivilegedRoles: boolean }>(
      `${environment.apiBaseUrl}/tenant-settings/mfa-policy`);
  }

  setTenantMfaPolicy(require: boolean): Observable<void> {
    return this.http.put<void>(`${environment.apiBaseUrl}/tenant-settings/mfa-policy`, { require });
  }

  /** Self-service rotation — anonymous endpoint; works while the password is expired. */
  changePassword(tenantIdentifier: string | null, email: string, currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.base}/change-password`, {
      tenantIdentifier, email, currentPassword, newPassword,
    });
  }

  /** Revokes the refresh family server-side, then clears the in-memory session. */
  logout(): void {
    this.http.post<void>(`${this.base}/logout`, {}, { withCredentials: true })
      .subscribe({ next: () => this.clear(), error: () => this.clear() });
  }

  private apply(res: AuthResponse): void {
    this.session.set({
      token: res.accessToken,
      role: res.role,
      displayName: res.displayName,
      tenantId: res.tenantId,
      expiresAtUtc: res.expiresAtUtc,
    });
  }

  private clear(): void {
    this.session.set(null);
  }

  /** Part 11-friendly idle lockout: sign out after 30 minutes without interaction. */
  private static readonly IDLE_LIMIT_MS = 30 * 60 * 1000;
  private idleTimer: ReturnType<typeof setTimeout> | null = null;

  /** Arms the idle timer and resets it on user activity (call once at app start). */
  startIdleWatch(): void {
    const reset = (): void => {
      if (this.idleTimer) { clearTimeout(this.idleTimer); }
      if (!this.isAuthenticated()) { return; }
      this.idleTimer = setTimeout(() => {
        this.logout();
        location.assign('/login');
      }, AuthService.IDLE_LIMIT_MS);
    };
    for (const evt of ['click', 'keydown', 'visibilitychange']) {
      document.addEventListener(evt, reset, { passive: true });
    }
    reset();
  }
}
