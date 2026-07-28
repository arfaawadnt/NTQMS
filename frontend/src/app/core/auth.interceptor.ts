import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Attaches the in-memory bearer token to API calls and recovers from an
 * expired access token transparently (ADR-0009): on a 401, it performs ONE
 * silent refresh (single-flight in AuthService) and retries the original
 * request with the new token. Only if the refresh also fails does it clear the
 * session and route to login — so a routine 15-minute token expiry never
 * bounces the user out of a working screen.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return next(withBearer(req, auth.token)).pipe(
    catchError((err: HttpErrorResponse) => {
      // Only genuine token expiry on a normal API call is retryable. The auth
      // endpoints themselves (login/refresh/logout) must never recurse.
      if (err.status !== 401 || isAuthEndpoint(req.url)) {
        return throwError(() => err);
      }

      return auth.refresh().pipe(
        switchMap((token) => {
          if (!token) {
            void router.navigate(['/login']);
            return throwError(() => err);
          }
          return next(withBearer(req, token));
        }),
      );
    }),
  );
};

function withBearer(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}

function isAuthEndpoint(url: string): boolean {
  return url.includes('/auth/login')
    || url.includes('/auth/refresh')
    || url.includes('/auth/logout');
}
