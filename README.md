# NT.QAMS — Quality Assurance Management System

A multi-tenant SaaS Quality Management System for ISO/IEC 17025 laboratories
(ISO 9001, 21 CFR Part 11). This repository is the **production rebuild**, written
strictly against the architecture package in the project root
(`../NT_QAMS_Domain_Model.md`, `../NT_QAMS_Database_Architecture.md`,
`../NT_QAMS_Application_Architecture.md`) — those documents are the source of
truth; any divergence is a deliberate architecture decision, recorded in
[`IMPLEMENTATION_LOG.md`](IMPLEMENTATION_LOG.md).

**Status:** backend feature-complete for the specified scope — 14 bounded
contexts plus the 21 CFR Part 11 compliance layer. **114 automated tests, all
green.** API-only; the Angular frontend is the remaining workstream.

---

## What it does

| Area | Modules |
|---|---|
| **Improvement** | Nonconformance & CAPA (9-state), Complaints |
| **Documents** | Controlled SOP lifecycle, versioning, e-signed publication |
| **Audits** | Programs, checklists, findings → auto-NC saga |
| **Analytical quality** | Method Validation (CLSI, CV%/bias vs TEa), QC (Westgard multi-rule), Proficiency Testing (z-scores) → auto-NC saga |
| **Resources** | Equipment & Calibration (auto-lockout), Competency & Training (80% gate) |
| **Governance** | Risk register, Change Control (risk-gated approval), Management Review |
| **Suppliers** | Approval lifecycle, certificate expiry auto-suspension, evaluations |
| **Records** | Quality archive with retention classes & authorized disposal |
| **Platform** | Tenancy/provisioning, Identity (JWT + MFA + lockout), Organization & reference data, Notifications engine, SLA/escalation + task queue, Compliance ledgers |

Four cross-module sagas run through the transactional outbox: audit-finding→NC,
PT-unsatisfactory→NC, the daily compliance sweep (calibration due/lockout,
competency expiry, supplier cert expiry), and CAPA overdue→escalation ladder.

---

## Architecture (Clean Architecture + CQRS)

```
NT.QAMS.SharedKernel   → primitives, tenancy marker, LocalizedText, IClock  (references nothing)
NT.QAMS.Domain         → aggregates, state machines, domain events, domain services (WestgardEvaluator, …)
NT.QAMS.Application     → CQRS commands/queries/handlers, pipeline behaviors, ports, policies (sagas)
NT.QAMS.Contracts       → API DTOs & request/response records
NT.QAMS.Infrastructure  → EF Core + interceptors (tenant/audit/outbox), Postgres, JWT, TOTP, S3-style storage, jobs, ledgers
NT.QAMS.WebApi          → thin controllers, middleware, composition root
```

**Non-negotiables, enforced by `NT.QAMS.Architecture.Tests` (a CI merge gate):**
dependencies point inward; Domain references only SharedKernel; Application never
references Infrastructure or ASP.NET; Contracts carry no domain types.

**Multi-tenancy (4 layers):** EF global query filter on every `ITenantScoped`
entity (by convention) · tenant-stamp interceptor (throws on unresolved tenant) ·
PostgreSQL row-level security on every tenant table · composite tenant-aware keys.
The app connects as a low-privilege role; the tenant is resolved from the JWT
claim only (never a header).

**Event pipeline:** aggregates raise domain events → the outbox interceptor writes
them in the same transaction as the state change → the outbox processor dispatches
to in-process policies, appends the hash-chained audit trail, and drives sagas.
Producers never call notifications/ledgers inline — everything reactive hangs off
the stream, so API-, job- and saga-originated changes behave identically.

**Compliance (21 CFR Part 11):** tamper-evident hash-chained audit trail
(append-only, DB guard triggers), electronic-signature ledger with content-hash
linking, security-event log, MFA (TOTP), account lockout, per-user e-signature PINs.

---

## Build & run locally

Prerequisites: .NET 9 SDK, PostgreSQL 17 (or Docker).

```bash
docker compose up -d                              # PostgreSQL 17
dotnet build NT.QAMS.sln
dotnet test  NT.QAMS.sln                           # 114 tests
dotnet run --project src/NT.QAMS.WebApi            # needs Jwt__Secret + connection string
```

Minimum configuration (environment variables):

```
ConnectionStrings__Postgres = Host=localhost;Port=5432;Database=ntqams;Username=qams_app;Password=...
Jwt__Secret                 = <48+ random chars>   # app refuses to start without it
PlatformAdmin__Email        = admin@yourco.test    # bootstrapped on first run
PlatformAdmin__Password     = <strong>
Database__MigrateOnStartup  = true                 # first boot only
```

Then `scripts/verify-e2e.ps1 -BaseUrl http://localhost:5000 -AdminEmail … -AdminPassword …`
drives the full happy path (login → provision tenant → raise NC → upload file →
publish a signed document → check the audit trail) and reports pass/fail per step.

---

## Deploy to another server

See [`deploy/DEPLOY.md`](deploy/DEPLOY.md). The self-contained Windows x64 package
(`deploy/NT.QAMS-webapi-v1.0-win-x64.zip`) needs no runtime on the target;
[`deploy/ANTIGRAVITY_DEPLOY_PROMPT.md`](deploy/ANTIGRAVITY_DEPLOY_PROMPT.md) is a
copy-paste, verification-gated runbook for AI-assisted deployment. A `Dockerfile`
is included for container hosts.

> ⚠ Bind to loopback / private network behind a TLS reverse proxy. The API is
> hardened (JWT deny-by-default, MFA, lockout) but has not had an external
> penetration test.

---

## Increment history

Current version: **v1.54.0** (full detail in [`IMPLEMENTATION_LOG.md`](IMPLEMENTATION_LOG.md);
per-run test history in [`docs/validation/verification-log.md`](docs/validation/verification-log.md)).

### Stage 1 — Initial vertical-slice build (v0.1 → v1.0, 2026-07-22)
Every increment a complete UI→API→domain→DB→tests slice:

| Ver | Delivered |
|---|---|
| Phase 0 | Solution skeleton, multi-tenancy plumbing, outbox, Tenancy walking slice |
| v0.2 | JWT auth, NC/CAPA module |
| v0.3 | Document Control + content-addressed file storage |
| v0.4 | Audit Management + finding→NC saga |
| v0.5 | Equipment & Calibration + Competency & Training + daily sweep |
| v0.6 | Risk & Governance + Supplier Quality |
| v0.7 | Organization & Reference Data + Notifications engine |
| v0.8 | Analytical Quality (Validation + QC/Westgard + PT) |
| v0.9 | Records & Retention + SLA/Escalation + Task Queue |
| v1.0 | Compliance & security hardening (MFA, lockout, audit ledger, e-signatures) |
| Frontend v1 | Angular SPA foundation — auth/shell + first feature slices |

### Stage 2 — Frontend build-out & regulatory remediation (v1.1 → v1.37)
| Ver | Delivered |
|---|---|
| v1.1 → v1.24 | Full Angular SPA build-out across all modules; **frontend upgraded to Angular 22**; Playwright e2e + axe a11y |
| v1.25 → v1.37 | **CSV / 21 CFR Part 11 audit remediation — all 18 findings CLOSED** (tenant RLS, signed-record immutability, MFA, SoD, reason-for-change, session revocation, e-sig logging, integration tests, backup/DR, exports, password policy, config externalization, secrets, GAMP 5 CSV doc set) |

### Stage 3 — Enterprise-Architecture remediation train (v1.38 → v1.44)
| Ver | Delivered |
|---|---|
| v1.38 | Phase 0 — deployment safety gates (DB-role guard, health live/ready, single-replica ADR-0001) |
| v1.39 | Phase 1 — `xmin` concurrency (409), outbox dead-letter/backoff + `SKIP LOCKED` lease, sweep leader election |
| v1.40 | Phase 2 — observability baseline (JSON logs, OTel HTTP→MediatR→EF→Outbox traces, `/metrics` + alerts) |
| v1.41 | Phase 3 — rate limiting (429), security headers + locked CSP, TLS-at-proxy + HSTS |
| v1.42 | Phase 4 — problem+json everywhere, pagination envelope, file allow-list/sniffing, deny-by-default command authz + CI gate, Idempotency-Key, `api/v1` versioning |
| v1.43 | Phase 5 — CHECK constraints, fail-fast config guard, Npgsql retry/timeout, non-root container |
| v1.44 | Phase 6 — module-boundary + API-surface snapshot merge gates, migration round-trip + audit-tamper tests, perf smoke |

### Stage 4 — Road-to-100 & validation evidence (v1.45 → v1.50)
| Ver | Delivered |
|---|---|
| v1.45 | Post-remediation backlog #1 |
| v1.46 | Road-to-100 Phase 7 — session-security completion (retires ADR-0003) |
| v1.47 | Road-to-100 Phase 8 — evidence at scale (load harness, perf/security probes) |
| v1.48 | Road-to-100 Phase 9 — assurance depth |
| v1.49 / v1.49.1 | GAMP 5 validation: engineering dry-run, OQ execution records, system-owner release decision |
| v1.50 | Compliance status milestone (EA posture 88%, 0 critical) |

### Stage 5 — Product build-out (v1.51.0 → v1.54.0)
| Ver | Delivered |
|---|---|
| v1.51.0 | **Role Privilege module** — dynamic tenant-defined roles over a **170-key permission catalogue**; branch/department as a hard data filter |
| v1.51.1 | Role Privilege OQ execution + defect RP-D1 fix |
| v1.51.2 | **Schema hardening** — six `Hardening*` migrations (composite PKs, RLS parity, CHECK domains, deferrable tenant FKs); OQ-DB-01..08 executed |
| v1.52.0 | **Quality Analytics** (one computation serving a Quality Statistics dashboard + ISO 17025 §8.9.2 Management-Review pack; 9 sub-systems; tenant-configurable weighted Quality Health Score; URS-108…114) + **usability / self-service set** (My-Tasks role resolution, route↔help parity, management-review agenda/link/participants + dispatch, self-service & admin-issued e-signature PINs, self-service password change, tabbed equipment workspace with maintenance certificates; URS-115…122) |
| v1.53.0 | **RISK-03 — 21 CFR Part 11 e-signature ceremony** extended from document-publish to **every** signed-record gate (NC verify/close, all 14 analytical-quality sign-offs, audit sign-off, quality-policy & change approval, management-review close, the borderline SoD gates, both periodic-review completions); 4 new `.sign` permission keys; self-fetching signature manifest on every gate; URS-123…128 |
| v1.53.1 | Deploy/upgrade-path corrections (apply DDL as `qams_owner`, `SET LOCAL` RLS-bypass so the idempotent script applies from scratch, Windows-service SCM integration, `harden-runtime-role.sql` fixes) — **no application/schema change** |
| **v1.54.0** | **Product enhancement program** (URS-129…134) — **NC re-open** (reason + e-signature, reuses `nc.sign`); **Quality Analytics report** as branded PDF & Excel (`reports.export`); **User Manual PDF** (cover chart, linked TOC, per-topic progress bars); **My Tasks unified action centre** (live read model over 7 pending-action sources); **Mail Management** — per-tenant mail sender identity (FORCE-RLS `tenant_mail_settings`) + branded HTML e-mail template. No new permission keys; two additive migrations |

> **Note:** the newest git tag is **`v1.54.0`** (@ `dea0d2b`). This work lands on `dev`; `master` is
> promoted separately by the dev-team review process. Release posture remains **Pre-production /
> Approved-with-conditions** — the open blockers are **DOC-001** (signed validation on a qualified
> environment) and **SEC-001** (independent penetration test); v1.54.0's validation records
> (URS-129…134) ship as Template/unsigned and fold under DOC-001.

## Remaining / open items

Product-backlog and release-gate items (see
[`docs/as-built-review/`](docs/as-built-review/) for the full as-built assessment):

- **e-signature ceremonies** on signing points beyond document-publish (audit sign-off, NC verify/close, quality-policy/change approve, review close, analytical study sign-offs — the ceremony pattern is established, not yet propagated).
- **SignalR real-time push** (reserved but not built).
- **SEC-001** — independent penetration test (in-house probes only to date).
- **DOC-001** — validation signed on a qualified environment (OQ transcripts 12 & 13 await witness signatures; executed on a dev workstation).
- **OPS-001** — staging observability + load/soak validation.
