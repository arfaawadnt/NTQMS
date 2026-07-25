import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse } from './models';

interface StoredSession {
  token: string;
  role: string;
  displayName: string;
  tenantId: string | null;
  expiresAtUtc: string;
}

const STORAGE_KEY = 'qams.session';

/**
 * Holds the JWT session (signals for reactive UI) and drives the login /
 * MFA / PIN flows against the backend. The token is the only client-side
 * auth state; role/tenant come from the login response, authorization is
 * re-checked server-side on every call.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly base = `${environment.apiBaseUrl}/auth`;
  private readonly session = signal<StoredSession | null>(this.restore());

  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly displayName = computed(() => this.session()?.displayName ?? '');
  readonly role = computed(() => this.session()?.role ?? '');
  readonly tenantId = computed(() => this.session()?.tenantId ?? null);

  constructor(private readonly http: HttpClient) {}

  get token(): string | null {
    return this.session()?.token ?? null;
  }

  login(tenantIdentifier: string | null, email: string, password: string, mfaCode: string | null):
    Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.base}/login`, { tenantIdentifier, email, password, mfaCode })
      .pipe(tap((res) => {
        if (!res.mfaRequired && res.accessToken) {
          this.persist(res);
        }
      }));
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

  /** Self-service rotation — anonymous endpoint; works while the password is expired. */
  changePassword(tenantIdentifier: string | null, email: string, currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.base}/change-password`, {
      tenantIdentifier, email, currentPassword, newPassword,
    });
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
  }

  private persist(res: AuthResponse): void {
    const stored: StoredSession = {
      token: res.accessToken,
      role: res.role,
      displayName: res.displayName,
      tenantId: res.tenantId,
      expiresAtUtc: res.expiresAtUtc,
    };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(stored));
    this.session.set(stored);
  }

  private restore(): StoredSession | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }
    try {
      const parsed = JSON.parse(raw) as StoredSession;
      if (new Date(parsed.expiresAtUtc).getTime() <= Date.now()) {
        localStorage.removeItem(STORAGE_KEY);
        return null;
      }
      return parsed;
    } catch {
      return null;
    }
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
