import { ApplicationConfig, provideZoneChangeDetection, APP_INITIALIZER } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';
import { changeReasonInterceptor } from './core/change-reason.interceptor';
import { AuthService } from './core/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    // withComponentInputBinding lets routed components bind route params
    // (e.g. :id) directly to signal inputs.
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor, changeReasonInterceptor])),
    // ADR-0009: one silent refresh at startup rehydrates the session from the
    // httpOnly cookie, so a page reload keeps the user signed in without a
    // token in web storage. Always resolves (a failure just means "logged out").
    {
      provide: APP_INITIALIZER,
      multi: true,
      deps: [AuthService],
      useFactory: (auth: AuthService) => () => auth.hydrate(),
    },
  ],
};
