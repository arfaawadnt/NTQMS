# ADR-0002 — TLS terminates at the reverse proxy; HSTS is emitted in-app

- **Status:** Accepted (2026-07-28)
- **Finding:** SEC-012 (Enterprise Architecture remediation plan, Phase 3)
- **Related:** SEC-011 (security headers), ADR-0001 (topology)

## Context

The API binds loopback HTTP behind a fronting layer in every supported
deployment (IIS/ARR via `deploy/web.config` on Windows; any TLS-terminating
LB/ingress for containers). The alternatives were in-app TLS
(`UseHttpsRedirection` + Kestrel certificates) or proxy termination.

## Decision

1. **TLS terminates at the reverse proxy.** Certificates, protocol policy
   (TLS 1.2+), and HTTP→HTTPS redirection are the proxy's responsibility.
   In-app `UseHttpsRedirection` is deliberately NOT enabled — behind a
   loopback proxy it would either loop or demand certificate plumbing the
   deployment model doesn't need.
2. **HSTS is emitted by the application** (`SecurityHeadersMiddleware`,
   `max-age=63072000; includeSubDomains`, outside Development) so the
   commitment cannot be forgotten in a proxy config. Browsers ignore HSTS on
   plain HTTP, so the header is harmless in dev and effective exactly when
   the proxy serves HTTPS.
3. **`UseForwardedHeaders` (X-Forwarded-For / X-Forwarded-Proto)** runs first
   in the pipeline with the framework's loopback-only trust defaults, so the
   real client address feeds the SEC-013 rate-limit partitions and the
   request logs, and the scheme reads as https.

## Operational checklist (per environment)

- Proxy redirects HTTP → HTTPS (301) and forwards `X-Forwarded-For`/`-Proto`.
- TLS 1.2+ only; certificates auto-renewed.
- Verify after deploy: `curl -sI https://<host>/health/ready | grep -i strict-transport-security`.

## Consequences

- A deployment that exposes the API without a TLS proxy ships no transport
  encryption — the checklist above is part of go-live sign-off.
- The forwarded-header trust is loopback-only; a proxy on another host must
  be added to `ForwardedHeadersOptions.KnownProxies` explicitly.
