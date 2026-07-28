# ADR-0007 — Same-origin deployment; no CORS by design

- **Status:** Accepted (2026-07-28) — formalizes the standing deployment model
- **Finding:** required ADR per remediation plan §04

## Decision

The SPA and the API are always served from **one origin**: the reverse proxy
(IIS/ARR via deploy/web.config, or the container fronting layer) serves the
Angular bundle and proxies `/api`, `/health`, `/metrics` (and future `/hubs`)
to the loopback Kestrel process. Consequently the API registers **no CORS
policy at all** — the browser's same-origin policy stands untouched, which is
the strongest possible cross-origin posture: there is no allow-list to
misconfigure.

## Implications

- A future native/mobile client or third-party integration talks to the API
  with tokens over non-browser HTTP — still no CORS needed.
- If a genuinely cross-origin BROWSER client ever becomes a requirement, that
  is a new ADR: an explicit, origin-pinned CORS policy plus a re-read of the
  token-storage decision (ADR-0003), never `AllowAnyOrigin`.
- The e2e and dev setups mirror production shape (Angular dev server proxies
  `/api` to :5080), so no dev-only CORS crept in.
