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

Ten verified increments, each building on the last (full detail in
[`IMPLEMENTATION_LOG.md`](IMPLEMENTATION_LOG.md)):

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

## Remaining

Fine-grained ~70-privilege matrix (role gates suffice today) · e-signature
ceremonies on the signing points beyond document-publish (pattern established) ·
SignalR real-time push · the **Angular 18 frontend** (largest remaining workstream).
