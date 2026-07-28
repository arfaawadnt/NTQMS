import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

/**
 * Phase-7 (ADR-0009) guarantees on the client: the access token lives in
 * memory only — never web storage — and a silent refresh single-flights.
 */
describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/auth`;

  const authBody = {
    accessToken: 'token-1', role: 'TenantAdmin', displayName: 'Admin',
    tenantId: 't1', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(), mfaRequired: false,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule], providers: [AuthService] });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    localStorage.clear();
  });

  afterEach(() => http.verify());

  it('keeps the access token in memory and never in web storage', () => {
    auth.login('demo', 'a@b.test', 'pw', null).subscribe();
    http.expectOne(`${base}/login`).flush(authBody);

    expect(auth.token).toBe('token-1');
    expect(auth.isAuthenticated()).toBeTrue();
    // The token must not have leaked into any web-storage bucket.
    expect(JSON.stringify(localStorage)).not.toContain('token-1');
    expect(JSON.stringify(sessionStorage)).not.toContain('token-1');
  });

  it('refresh swaps the cookie for a fresh in-memory token', () => {
    auth.refresh().subscribe();
    http.expectOne(`${base}/refresh`).flush({ ...authBody, accessToken: 'token-2' });

    expect(auth.token).toBe('token-2');
    expect(auth.isAuthenticated()).toBeTrue();
  });

  it('single-flights concurrent refreshes into one HTTP call', () => {
    auth.refresh().subscribe();
    auth.refresh().subscribe();
    auth.refresh().subscribe();

    // Exactly one in-flight request serves all three callers.
    const req = http.expectOne(`${base}/refresh`);
    req.flush({ ...authBody, accessToken: 'token-shared' });
    expect(auth.token).toBe('token-shared');
  });

  it('a failed refresh clears the session and resolves to null', (done) => {
    auth.refresh().subscribe((token) => {
      expect(token).toBeNull();
      expect(auth.isAuthenticated()).toBeFalse();
      done();
    });
    http.expectOne(`${base}/refresh`).flush(
      { title: 'expired' }, { status: 401, statusText: 'Unauthorized' });
  });

  it('logout revokes server-side and clears the session', () => {
    auth.login('demo', 'a@b.test', 'pw', null).subscribe();
    http.expectOne(`${base}/login`).flush(authBody);

    auth.logout();
    http.expectOne(`${base}/logout`).flush(null);
    expect(auth.isAuthenticated()).toBeFalse();
    expect(auth.token).toBeNull();
  });
});
