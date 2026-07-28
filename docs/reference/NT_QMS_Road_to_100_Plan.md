# NT.QMS — Road to 100% Plan (EA-PLAN-NTQMS-002)

| | |
|---|---|
| **Date** | 2026-07-28 |
| **Baseline** | EA-AUD-NTQMS-002: ≈94% at `v1.45.0` |
| **Goal** | 100% enterprise-architecture compliance |
| **Shape** | Three engineering phases (v1.46 → v1.48) continuing the gated-train discipline, plus one **external track** that engineering cannot substitute for. Honesty first: code alone tops out at ≈97–98% — the last points are bought only by the external track (pen test, production-scale evidence, CSV re-validation). |

## Gap analysis — where the ~6 points live

| Deficit | Category impact | Bought back by |
|---|---|---|
| ADR-0003 token-storage risk acceptance (web-storage JWT) | Security 90 | Phase 7 |
| No production-scale runtime evidence (telemetry, alerts under fire, manifest never run on a host) | Observability 88–92 | Phase 8 |
| No load test; perf evidence = dev-box smoke | Testing 92, Ops | Phase 8 |
| Frontend component-spec ratio thin; endpoint HTTP-contract coverage ~partial; no full role×endpoint matrix | Testing 92, Frontend 94, API 92–93 | Phase 9 |
| No penetration test | Security 90 | External track |
| CSV re-validation (IQ/OQ/PQ deltas, RTM) outstanding | Regulated completeness | External track |

---

## Phase 7 (v1.46) — Session-security completion · retires ADR-0003 (R-2) · ~1 wk

The one remaining *accepted* risk becomes an implemented control.

**Scope**
1. **Refresh-token flow:** opaque, rotating refresh token in an `httpOnly; Secure; SameSite=Strict` cookie **scoped to `/api/auth/refresh`**; server-side store holds only the token **hash** per session (new `saas.refresh_session` table: user, family, hash, expiry, revoked).
2. **Rotation with reuse detection:** every refresh rotates the token; presentation of a superseded token revokes the whole family (classic stolen-token tell) and raises a security event.
3. **Access token:** stays JWT, lifetime drops to **15 min**, held in SPA **memory only** — web storage cleared out entirely; silent refresh on 401/expiry; full logout revokes the family server-side (extends existing F-07 session revocation).
4. **CSRF posture:** SameSite=Strict + custom-header requirement on `/api/auth/refresh` (documented in the ADR); no other endpoint reads cookies.
5. **ADR-0009** supersedes ADR-0003 (the risk acceptance is retired, not deleted — history preserved).

**Acceptance / proving tests:** no token in web storage (e2e assertion); page reload keeps the session via refresh (e2e); rotation + reuse-detection functional tests (replayed old cookie → family revoked → 401); cookie-flag assertions; existing 359-test suite green.
**Score effect:** Security 90 → ~97 · Frontend 94 → ~95. **Overall ≈ 96%.**

## Phase 8 (v1.47) — Evidence at scale · closes R-5 + the runtime gap · ~1 wk eng + infra

Turn "observability exists" into "observability observed working under load".

**Scope**
1. **Staging stands up from `deploy/compose.production.yml` on a real Docker host** — also proves the manifest end-to-end (image itself already CI-proven).
2. **Telemetry pipeline live:** `Otlp__Endpoint` → collector → Prometheus/Grafana (or equivalent); dashboards + the OBSERVABILITY.md alert rules actually configured.
3. **Failure drills:** stop PostgreSQL → readiness flips + alert fires; poison an outbox event → dead-letter alert fires; kill a replica → restart within SLO. Each drill's evidence archived.
4. **Load suite** (k6 or NBomber, committed to `tests/load/`): login storm (rate-limit interplay), mixed list browsing at target concurrency (e.g. 100 concurrent lab users), NC lifecycle write mix, outbox drain under backlog. Targets: p95 < 500 ms API reads, < 1.5 s writes, error rate < 0.1%, zero dead-letters under nominal load.
5. **24 h soak** on staging with dashboards; perf + soak report committed to `docs/reference/`.

**Acceptance:** all drills pass with evidence; load targets met; report committed; `perf-smoke.ps1` numbers recorded from staging.
**Score effect:** Observability cluster → ~97 · Testing +2. **Overall ≈ 97.5%.**

## Phase 9 (v1.48) — Assurance depth · ~1–2 wk

The remaining thin spots in test evidence, mechanically closed.

**Scope**
1. **Full role×endpoint deny matrix:** generated test iterating the 620-route surface snapshot × all six roles, asserting every response is 2xx/404 *or* problem+json 401/403 — no silent role leakage anywhere (extends `AuditorDenyMatrixTests` from samples to the full grid).
2. **Endpoint contract tests to ≥70% of the surface:** typed request/response assertions per route group (status, envelope shape, problem codes), anchored on the ApiSurface snapshot.
3. **Frontend spec expansion:** component specs for all regulated flows (NC lifecycle, document publish ceremony, CAPA, audits sign-off, records legal-hold) to ≥60% of components; **axe-core automated a11y audit in CI** for the main screens.
4. **E2E expansion:** publish-with-password+PIN ceremony, SoD denial path, auditor read-only journey, load-more pagination journey.

**Acceptance:** matrix test green across all roles; contract coverage measured and ≥70%; axe CI job green; new e2e green in CI.
**Score effect:** Testing → ~97 · Frontend → ~98 · API cluster → ~97 · Domain/architecture → ~98. **Overall ≈ 98% — the engineering ceiling.**

## External track (parallel; the last ~2 points)

| Activity | Owner | Gate it closes |
|---|---|---|
| **Penetration test** against staging (after Phase 7 so the new session model is in scope), plus a fix window for findings | External security vendor | Security → 100; the STRIDE score stops being code-inferred |
| **CSV re-validation** (R-6): RTM update for v1.38→v1.48 deltas, OQ/PQ delta execution, VSR addendum | QA / validation team | Regulated completeness — required for a defensible 100 in this domain |
| **30-day production soak review:** first month of real telemetry reviewed against the alert set; tune thresholds | Ops | The "no production telemetry" reservation in audit §F |

## Sequencing & effort

```
Phase 7 (1 wk) ──► Phase 8 (1 wk) ──► Phase 9 (1–2 wk)
                        │                   │
                        └── pen test ◄──────┘   (external, after Phase 7; parallel to 8/9)
CSV re-validation: starts after Phase 7 freezes the auth surface; parallel to 8/9.
```

≈ 3–4 weeks of engineering + the external activities' calendar time. Each phase ships as before: gated increment, acceptance tests in CI, tagged release, IMPLEMENTATION_LOG + audit-document updates. **EA-AUD-NTQMS-003** re-scores after Phase 9 + pen-test fixes; the 100% claim is made there, with the external evidence attached — not before.

## Explicit non-goals

No domain/feature redesign; no move off the modular monolith; no CORS/multi-origin work (ADR-0007 stands); SignalR/real-time remains out of scope.
