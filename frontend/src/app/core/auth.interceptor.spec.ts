import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

/**
 * Phase-7 (ADR-0009): a 401 on a normal API call triggers exactly one silent
 * refresh and a retry with the fresh token; only a failed refresh bounces the
 * user to login. The auth endpoints themselves never recurse.
 */
describe('authInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;
  let auth: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;
  const api = `${environment.apiBaseUrl}/nonconformances`;
  const refreshUrl = `${environment.apiBaseUrl}/auth/refresh`;

  function setup(token: string | null): void {
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['refresh']);
    Object.defineProperty(auth, 'token', { get: () => token });
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXhr(), withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
      ],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
  }

  afterEach(() => ctrl.verify());

  it('attaches the bearer token', () => {
    setup('tok-A');

    http.get(api).subscribe();
    const req = ctrl.expectOne(api);

    expect(req.request.headers.get('Authorization')).toBe('Bearer tok-A');
    req.flush([]);
  });

  it('on 401 refreshes once and retries with the new token', (done) => {
    setup('stale');
    auth.refresh.and.returnValue(of('fresh'));

    http.get(api).subscribe({
      next: () => {
        expect(auth.refresh).toHaveBeenCalledTimes(1);
        done();
      },
    });

    ctrl.expectOne(api).flush({ title: 'expired' }, { status: 401, statusText: 'Unauthorized' });
    const retried = ctrl.expectOne(api);
    expect(retried.request.headers.get('Authorization')).toBe('Bearer fresh');
    retried.flush([]);
  });

  it('routes to login when the refresh also fails', (done) => {
    setup('stale');
    auth.refresh.and.returnValue(of(null));

    http.get(api).subscribe({
      error: (err: HttpErrorResponse) => {
        expect(err.status).toBe(401);
        expect(router.navigate).toHaveBeenCalledWith(['/login']);
        done();
      },
    });

    ctrl.expectOne(api).flush({ title: 'expired' }, { status: 401, statusText: 'Unauthorized' });
  });

  it('does not attempt to refresh a failed refresh call itself', (done) => {
    setup(null);

    http.post(refreshUrl, {}).subscribe({
      error: () => {
        expect(auth.refresh).not.toHaveBeenCalled();
        done();
      },
    });

    ctrl.expectOne(refreshUrl).flush({ title: 'no' }, { status: 401, statusText: 'Unauthorized' });
  });
});
