# NT.QAMS Frontend (Angular 18)

The web client for NT.QAMS. Standalone-component Angular 18, signals-based state,
lazy-loaded feature routes, trilingual (EN / AR-RTL / FR) per the design system.

## This slice (frontend v1)

The architectural foundation plus a working vertical slice against the live API:

- **App shell** — navy sidebar + header, language switcher, sign-out.
- **Auth** — JWT login with the **MFA step** (the form reveals the authenticator
  code field when the API responds `mfaRequired`), token persistence, an HTTP
  interceptor that attaches the bearer token and bounces to `/login` on 401,
  and a route guard.
- **i18n / RTL** — `I18nService` dictionary; selecting Arabic flips
  `document.dir` to `rtl` and translates labels (verified in-browser).
- **Dashboard** — live KPI cards (open NCs, high-RPN items, unread notifications).
- **NC & CAPA** — list + raise + submit, wired to `/api/nonconformances`.
- **Notifications** — the in-app feed with mark-as-read.

All feature routes are lazy-loaded; `ng build` (strict templates) passes and the
production bundle is ~84 kB initial transfer.

## Run

```bash
npm install
npm start            # dev server on :4200 (proxy /api to the backend, or set apiBaseUrl)
npm run build        # production build -> dist/nt-qams-frontend
```

`src/environments/environment.ts` sets `apiBaseUrl` (default `/api`, i.e. same
origin behind the reverse proxy). For local dev against a backend on :5000,
point it at `http://localhost:5000/api` or add an Angular dev-server proxy.

## Deploy

Build, then serve `dist/nt-qams-frontend/browser` as static files behind the same
TLS reverse proxy that fronts the API (so `/api` is same-origin). On IIS this is
the `C:\inetpub\wwwroot\qams-ui` pattern from the legacy deployment notes.

## Remaining (frontend)

The other ~25 modules follow the same shape as the NC feature (list + drawer +
form against their controllers): Documents, Audits, Equipment, Competency, Risk,
Change, Suppliers, Analytical Quality, Records, admin screens, plus the SignalR
live-refresh wiring, the full translation dictionary, and the charts/print packs.
