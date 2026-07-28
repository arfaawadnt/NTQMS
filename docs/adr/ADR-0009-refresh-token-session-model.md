# ADR-0009 — Refresh-token session model (supersedes ADR-0003)

- **Status:** Accepted (2026-07-28) — **supersedes ADR-0003** (token-storage risk acceptance)
- **Finding:** Road-to-100 Phase 7 (retires residual risk R-2 of EA-AUD-NTQMS-002)
- **Related:** ADR-0002 (TLS/same-origin), ADR-0007 (same-origin/no-CORS), F-06/F-07 (session revocation)

## Context

ADR-0003 consciously accepted keeping the JWT access token in SPA web storage,
compensated by a strict CSP and a shortened lifetime. Its revisit trigger was
"implement the httpOnly-refresh-cookie flow." Phase 7 implements it, so the
risk acceptance is retired.

## Decision

**Split the session into a short-lived access token held only in memory and a
long-lived, rotating refresh token held only in an httpOnly cookie.**

1. **Access token** — JWT, default **15 minutes** (`Jwt:ExpiryMinutes`), kept
   in a JavaScript variable/signal in `AuthService`, never in `localStorage`
   or `sessionStorage`. Lost on tab close by design.
2. **Refresh token** — opaque `"<sessionId:N>.<base64url secret>"`; the server
   stores only the **SHA-256 of the secret** (`qams.refresh_session`). Carried
   in cookie `qams_rt`: `HttpOnly; Secure; SameSite=Strict; Path=/api/auth`.
   Script cannot read it; it is CSRF-inert (SameSite=Strict) and rides no
   endpoint but the auth ones.
3. **Rotation with reuse detection** — every `POST /api/auth/refresh` rotates:
   the presented link is revoked and a successor is issued in the same
   `FamilyId`. Presenting an already-rotated token is the classic stolen-token
   tell → the **entire family is revoked** and a `REFRESH_REUSE_DETECTED`
   security event is written.
4. **Continuity across reload** — the SPA runs one silent refresh at startup
   (`APP_INITIALIZER` → `AuthService.hydrate`), so a page reload restores the
   session from the cookie without any token in web storage. On a 401 the auth
   interceptor performs one single-flight refresh and retries the original
   request, so a routine expiry is invisible to the user.
5. **Logout / revocation** — `POST /api/auth/logout` revokes the family
   server-side (extends F-07 to the refresh chain); refresh checks
   `user.IsActive`, so a deactivated account cannot refresh. Long-dead sessions
   are purged in the outbox retention cycle.

## Consequences

- A successful XSS can read the in-memory access token only while the page is
  live and only for ≤15 minutes; it **cannot** exfiltrate a durable credential,
  and it cannot read or replay the refresh cookie. This closes the residual
  risk ADR-0003 accepted.
- CSRF: only `/api/auth/refresh` and `/logout` read the cookie; both are
  SameSite=Strict and state-changing via POST only. No other endpoint is
  cookie-authenticated (bearer token only), so the rest of the API stays immune
  by construction.
- Requires the same-origin deployment of ADR-0002/0007 (the cookie is
  first-party); a future cross-origin browser client would revisit this.
- **ADR-0003 is superseded**; its risk acceptance no longer applies.

## Proof

`RefreshSessionTests` (functional): hardened cookie flags; rotation issues a
fresh token; replaying a rotated cookie → 401 + whole family revoked; logout
revokes + clears; no-cookie refresh → 401. `AuthService`/`authInterceptor`
specs: token never in web storage, single-flight refresh, retry-on-401,
login-on-refresh-failure. Reload persistence verified live against the running
API.
