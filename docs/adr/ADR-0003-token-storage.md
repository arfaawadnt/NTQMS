# ADR-0003 — Access-token storage in the SPA (risk acceptance)

- **Status:** Accepted (2026-07-28) — risk-acceptance, with a revisit trigger
- **Finding:** SEC-017 (Enterprise Architecture remediation plan, Phase 3)
- **Related:** SEC-011 (CSP), SEC-013 (rate limiting)

## Context

The Angular SPA keeps the JWT access token in browser web storage
(`localStorage`) so a session survives a page reload. The alternative is an
in-memory access token plus an httpOnly SameSite refresh cookie — stronger
against XSS token theft, but it requires a refresh-token issuance/rotation/
revocation flow the backend does not yet have, plus CSRF defenses for the
cookie path.

## Decision

**Keep web-storage tokens for now, with compensating controls, and accept the
residual risk:**

1. **Strict CSP everywhere** (SEC-011): the SPA host serves
   `script-src 'self'` (no inline/eval — Angular AOT needs none), which is the
   actual defense against the XSS class that steals storage tokens; the API
   itself serves `default-src 'none'`.
2. **Short access-token lifetime:** default halved to **60 minutes**
   (`Jwt:ExpiryMinutes`); a stolen token has a bounded window.
3. **Server-side session revocation already exists** (`ActiveSessionMiddleware`,
   audit finding F-06): sign-out and administrative revocation kill a token
   before expiry, independent of storage.
4. **Credential-surface rate limiting** (SEC-013) and account lockout bound
   what a stolen-but-expired token can be parlayed into.

## Residual risk (accepted)

A successful XSS in the SPA could exfiltrate a token valid for ≤60 minutes and
scoped to the victim's role/tenant, until the session is revoked. Accepted by
engineering for the current release train because the CSP forecloses the main
injection class and the flow change is disproportionate to Phase-3 scope.

## Revisit trigger

Implement the in-memory + httpOnly-refresh-cookie flow (and retire this
acceptance) when ANY of: a customer/regulatory requirement demands it; an XSS
finding reaches production; or the Phase-4+ API polish window opens with
capacity for the refresh-token flow (issuance, rotation, revocation list,
CSRF protection).
