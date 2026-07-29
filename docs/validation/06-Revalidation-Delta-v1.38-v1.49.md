# CSV Re-Validation Delta — v1.38.0 → v1.49.0

| Field | Value |
| ----- | ----- |
| Document ID | REVAL-NTQMS-001 (rev 2 — extended to v1.49.0) |
| System | NT.QMS |
| Baseline validated version | 1.0 (VMP/URS/FRA/QP/RTM/VSR — docs 00–05) |
| Scope of this delta | Changes across releases **v1.38.0 → v1.49.0** (EA-remediation Phases 0–6 + Road-to-100 backlog/Phases 7–9 + v1.49.0 supply-chain assurance & Angular 22 upgrade) |
| Parent | VMP-NTQMS-001; URS-NTQMS-001; RTM-NTQMS-001; QP-NTQMS-001; VSR-NTQMS-001 |
| Status | **DRAFT for QA execution.** Engineering-prepared traceability + qualification stubs; **QA owns, executes, witnesses, and signs.** |

> **How to use this document.** This is a *delta* re-validation package: it adds new
> requirements (URS-056+), new installation checks (IQ-16+), new operational cases (OQ, new
> areas), and performance cases (PQ) covering only what changed since the validated 1.0
> baseline — plus a VSR addendum. Every "Actual / P-F / Executed by / Date" cell is a
> **template for formal execution**; the named automated test is the *evidence engine* and its
> green CI/local run may be attached as executed evidence, with a witnessed manual
> confirmation recorded per the baseline QP convention. Nothing here is "done" until QA
> executes and signs.

**Signature block (per executed protocol section):**

| Activity | Name | Signature | Date |
| -------- | ---- | --------- | ---- |
| Prepared by (Engineering) | | | |
| Executed by | | | |
| Reviewed by (QA) | | | |
| Approved by (System Owner) | | | |

**Change-control provenance.** Each release below is a tagged, green CI build (Build & Test
with real PostgreSQL 17 · Container non-root + Trivy scan · Frontend incl. SCA gates). Engineering
record: `IMPLEMENTATION_LOG.md`; decisions: `docs/adr/ADR-0001…0009`; audits:
`docs/reference/NT_QMS_EA_Remediation_Closure_Report.md`, `…_Compliance_Audit_v1.48.html`,
`…_Enterprise_Application_Compliance_Audit.html` (EAC-NTQMS-001, covers v1.49.0).

---

## Part A — Requirements Traceability Matrix (RTM) delta

New/strengthened requirements introduced by the change program. File paths are repo-relative.
Verification legend as in RTM-NTQMS-001 (**AUTO** automated test, **OQ/PQ** scripted case,
**IQ** install check, **INSP** inspection).

### A.1 Deployment safety & data-layer integrity (Phase 0, 5 — v1.38, v1.43)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-056 | The application shall refuse to start in Production when its DB connection role is over-privileged (SUPERUSER / BYPASSRLS / table owner). | `Infrastructure/Security/DatabaseRoleGuard.cs`; `Program.cs` startup gate; `deploy/db-init.sql` (owner/app split) | AUTO `IntegrationTests/RuntimeRolePrivilegeTests`; IQ-17; OQ-DEP-01 | Template |
| URS-057 | The system shall expose liveness (`/health/live`) and DB-backed readiness (`/health/ready`, 503 when PostgreSQL is down) probes. | `Infrastructure/Health/PostgresReadinessHealthCheck.cs`; `Program.cs` health mapping | AUTO `WebApi.FunctionalTests/HealthEndpointTests`, `IntegrationTests/ReadinessAndTopologyTests`; IQ-16; OQ-DEP-02 | Template |
| URS-058 | Supported deployment topology (single-replica) shall be enforced/observable; recurring jobs shall not double-process under concurrency. | `Jobs/SingleReplicaGuardService.cs`; `Persistence/AdvisoryLock(+Keys).cs`; ADR-0001 | AUTO `IntegrationTests/ReadinessAndTopologyTests`; INSP ADR-0001; OQ-DEP-03 | Template |
| URS-059 | Regulated tables shall reject out-of-domain values at the database (enum domains, 1–5 scores, non-negative quantities, completion-after-creation). | migration `Phase5CheckConstraints` | AUTO `IntegrationTests/CheckConstraintTests`; IQ-20 | Template |
| URS-060 | Present-but-invalid critical configuration shall fail startup (never silently default). | `Infrastructure/Configuration/ConfigGuard.cs` | AUTO `WebApi.FunctionalTests/ConfigGuardTests`; OQ-DEP-04 | Template |
| URS-061 | The runtime container shall run as a non-root user with a least-privilege filesystem. | `WebApi/Dockerfile` (`USER $APP_UID`); `deploy/compose.production.yml` | IQ-19 (CI-verified: `.github/workflows/ci.yml` `container` job) | Template |
| URS-062 | Transient DB faults shall be retried with a bounded command timeout. | `Infrastructure/DependencyInjection.cs` (`EnableRetryOnFailure`, `CommandTimeout`); execution-strategy-safe `AdvisoryLock` | INSP; OQ-DEP-05 (regression: full suite green under the retrying strategy) | Template |

### A.2 Data consistency & messaging robustness (Phase 1 — v1.39)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-063 | Concurrent edits to one regulated record shall not silently lose an update; the loser shall receive HTTP 409 (`CONCURRENCY-409`). | `AppDbContext` xmin concurrency token (all aggregate roots); `DomainExceptionHandler`; ADR-0005 | AUTO `IntegrationTests/OptimisticConcurrencyTests`, `WebApi.FunctionalTests/ConcurrencyConflictMappingTests`; OQ-MSG-01 | Template |
| URS-064 | A permanently-failing integration event shall dead-letter after N attempts without head-of-line-blocking healthy events; retries shall back off. | `Persistence/Outbox/OutboxProcessor.cs`; `outbox_event.dead_lettered_at_utc/next_attempt_at_utc`; migration `Phase1OutboxResilienceAndConcurrency`; ADR-0006 | AUTO `Application.UnitTests/Outbox/OutboxProcessorTests`; OQ-MSG-02 | Template |
| URS-065 | Redelivery of an integration event shall net exactly one side-effect (idempotent consumers). | natural-key unique index `ux_nonconformance_source`; policy dedup | AUTO `OutboxProcessorTests` (redelivery), per-policy idempotency tests; OQ-MSG-03 | Template |
| URS-066 | Concurrent outbox processors shall publish each event exactly once (claim + lease). | `OutboxProcessor.ClaimDueBatchAsync` (`FOR UPDATE SKIP LOCKED` + lease) | AUTO `IntegrationTests/OutboxResilienceTests`; OQ-MSG-04 | Template |
| URS-067 | Processed outbox rows shall be purged after a retention window; the audit ledger remains the record. | `OutboxProcessor.PurgeProcessedAsync`; `harden-runtime-role.sql` scoped DELETE | AUTO `OutboxResilienceTests` (retention purge); OQ-MSG-05 | Template |

### A.3 Observability (Phase 2 — v1.40)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-068 | Each request shall emit one structured completion record with standard fields (service, environment, tenant, user, operation, status, outcome, duration, correlation). | `WebApi/Middleware/ObservabilityMiddleware.cs` | AUTO `WebApi.FunctionalTests/ObservabilityTests` (log-shape); OQ-OBS-01 | Template |
| URS-069 | A single request shall produce a correlated trace spanning HTTP→MediatR→EF→Outbox, with a client-facing correlation id and problem `traceId`. | `Behaviors/TracingBehavior.cs`; `Infrastructure/Observability/QamsDiagnostics.cs`; `outbox_event.trace_parent` (migration `Phase2OutboxTraceParent`) | AUTO `Application.UnitTests/Outbox/TracePropagationTests`, `ObservabilityTests` (correlation); OQ-OBS-02 | Template |
| URS-070 | The system shall publish metrics (RED, DB pool, outbox backlog/dead-letter, job liveness) and a documented, actionable alert set. | `Observability/QamsMetrics.cs`; `/metrics`; `deploy/OBSERVABILITY.md`; `deploy/observability/alert.rules.yml` | AUTO `ObservabilityTests` (/metrics); OQ-OBS-03; PQ-OBS-01 (drill fires alert — staging) | Template |

### A.4 Edge & session security (Phase 3, 7 — v1.41, v1.46)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-071 | Credential and e-signature endpoints shall be rate-limited; bursts shall return 429. | `WebApi/Security/RateLimiting.cs`; `[EnableRateLimiting]` on auth + publish | AUTO `WebApi.FunctionalTests/SecurityHardeningTests`; OQ-SEC-11 | Template |
| URS-072 | Every response shall carry the defensive header set (locked CSP, nosniff, frame-deny, referrer, HSTS outside Dev). | `WebApi/Middleware/SecurityHeadersMiddleware.cs`; `deploy/web.config` (SPA) | AUTO `SecurityHardeningTests` (headers on success + error); OQ-SEC-12; IQ-18 (HSTS at TLS host) | Template |
| URS-073 | TLS shall terminate at the proxy with HSTS; the app shall honour forwarded client IP/scheme. | `Program.cs` `UseForwardedHeaders`; ADR-0002 | IQ-18; INSP ADR-0002 | Template |
| URS-074 | The SPA access token shall be memory-only with a short lifetime; sessions shall use a rotating httpOnly SameSite=Strict refresh cookie with reuse-detection family revocation. | `Domain/IdentityAccess/RefreshSession.cs`; `Application/IdentityAccess/Commands/RefreshSessions.cs`; `AuthController` refresh/logout; migration `Phase7RefreshSessions`; ADR-0009 (supersedes ADR-0003) | AUTO `WebApi.FunctionalTests/RefreshSessionTests` (rotate/reuse/family-revoke); OQ-SEC-13/14; adversarial `scripts/security-probe-deep.ps1` [I] | Template |

### A.5 API & application-pipeline (Phase 4, 6, 9 — v1.42, v1.44, v1.48)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-075 | Every error response shall be `application/problem+json` with a stable machine-readable code (incl. framework 401/403). | `WebApi/Middleware/ProblemResponse.cs`; `ProblemAuthorizationResultHandler.cs`; `DomainExceptionHandler` | AUTO `WebApi.FunctionalTests/ProblemContractTests`; OQ-API-01 | Template |
| URS-076 | List endpoints shall return a bounded pagination envelope (items/total/page/pageSize/hasMore) with a clamped page size — no silent result caps. | `Contracts/Common/PagedResponse.cs`; `Application/Abstractions/Paging.cs`; SPA `Paged<T>` + facades | AUTO `WebApi.FunctionalTests/PaginationTests`, `ContractCoverageTests`; OQ-API-02 | Template |
| URS-077 | File uploads shall be allow-listed and content-sniffed (magic-byte); downloads shall force attachment; the stored type shall be canonical, not client-claimed. | `WebApi/Security/FileContentPolicy.cs`; `FilesController` | AUTO `WebApi.FunctionalTests/FileHardeningTests`; OQ-API-03 | Template |
| URS-078 | Every command shall carry an authorization policy; unannotated/unauthorized commands shall be denied (deny-by-default). The read-only ExternalAuditor shall not mutate. | `Abstractions/CommandAuthorization.cs`; `Behaviors/AuthorizationBehavior.cs`; `ICurrentUser.Role` | AUTO `Application.UnitTests/Behaviors/AuthorizationBehaviorTests`, `Architecture.Tests/CommandPolicyTests`, `WebApi.FunctionalTests/AuditorDenyMatrixTests`, `RoleEndpointMatrixTests`; OQ-API-04 | Template |
| URS-079 | Unsafe commands shall be retry-safe via an Idempotency-Key (replayed response nets one side-effect). | `Behaviors/IdempotencyBehavior.cs`; `Persistence/Idempotency/*`; migration `Phase4IdempotencyRecords` | AUTO `Application.UnitTests/Behaviors/IdempotencyBehaviorTests`, `WebApi.FunctionalTests/IdempotencyTests`; OQ-API-05 | Template |
| URS-080 | The API shall be versioned (`api/v1/…` beside legacy `api/…`) with a documented evolution policy; surface changes shall be gated. | `WebApi/Versioning/VersionedRouteConvention.cs`; ADR-0004; `ApiSurface.approved.txt` snapshot | AUTO `WebApi.FunctionalTests/ApiVersioningTests`, `ApiSurfaceSnapshotTests`; OQ-API-06 | Template |
| URS-081 | The modular-monolith boundary shall be enforced (no cross-module domain type references). | `Architecture.Tests/ModuleBoundaryTests` | AUTO (merge gate) | Template |

### A.6 Governance, coverage & UX (Phase 6, 9 — v1.44, v1.48)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-082 | Migrations shall be reversible (up/down round-trip) and a mid-chain audit-trail tamper shall be detected at the exact sequence. | `IntegrationTests/GovernanceTests` | AUTO `GovernanceTests`; OQ-AUD-09 | Template |
| URS-083 | The role×endpoint surface shall exhibit no silent authorization leakage across the six roles. | `WebApi.FunctionalTests/RoleEndpointMatrixTests` | AUTO; OQ-SEC-15 | Template |
| URS-084 | Part-11 reason-for-change capture shall be accessible (no `window.prompt`); unmanaged subscriptions removed. | `frontend/core/change-reason-dialog.component.ts` + service/interceptor; `takeUntilDestroyed` fix | Frontend spec (`change-reason-dialog.component.spec.ts`); axe scan `e2e/a11y.spec.ts`; OQ-UI-01 | Template |
| URS-085 | The sign-in surface shall have no serious/critical accessibility violations. | `frontend/e2e/a11y.spec.ts` (@axe-core/playwright); login-component fixes | AUTO (CI, every push); OQ-UI-02 | Template |

### A.7 Supply-chain assurance & framework currency (v1.49)

> **Change assessment (v1.49.0).** Two changes: (1) CI vulnerability-scan gates added
> (build-pipeline only — no application code touched); (2) the SPA framework upgraded
> **Angular 18.2.14 → 22.0.8**, one major at a time via the vendor migration path
> (18→19→20→21→22). The upgrade is **UI-framework only**: no change to the validated
> domain model, database schema (no new migration), or API contracts
> (`ApiSurface.approved.txt` unchanged). Impact is bounded to the presentation layer, so
> the regression evidence is the full frontend gate set plus the unchanged backend suite —
> re-executed green per major and at the final version (production AOT build; 67 unit
> specs; auth + a11y e2e; CI run `1beb3bf` all three jobs green). Toolchain deltas:
> TypeScript 5.5→6.0.3, zone.js 0.15, build/CI Node 24 (npm 11).

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-086 | Every CI build shall gate on software-composition analysis: known High/Critical vulnerabilities in backend NuGet packages (direct + transitive) and in shipped frontend npm packages shall fail the pipeline; any tolerated advisory shall be recorded in a documented exception register with compensating controls and a tracked fix. | `.github/workflows/ci.yml` (".NET SCA", "npm SCA" steps); `.github/npm-audit-allowlist.txt` (exception register — **currently empty**) | AUTO (CI, every push); IQ-24; OQ-SCA-01 | Template |
| URS-087 | Every CI build shall scan the runtime container image for OS/library CVEs and fail on fixable High/Critical findings. | `.github/workflows/ci.yml` ("Install Trivy" + "Trivy image vulnerability scan", `--severity HIGH,CRITICAL --ignore-unfixed`) | AUTO (CI, every push); IQ-24 | Template |
| URS-088 | The shipped SPA framework shall carry no known high/critical advisories; framework currency shall be maintained on a supported release line. | `frontend/package.json` → `@angular/* ^22.0.8`; upgrade evidence: commits `bc5ed96`→`93f8816` (one per major, gates green per step) | AUTO npm SCA (CI); OQ-SCA-02; INSP `npm audit --omit=dev` = 0 advisories | Template |

---

## Part B — Installation Qualification (IQ) delta

Append to QP-NTQMS-001 Part 1. Templates for execution in the qualified environment.

| Step | Verification | Expected | Actual | P/F | Evidence |
| ---- | ------------ | -------- | ------ | --- | -------- |
| IQ-16 | Health/readiness endpoints | `GET /health/live` → 200; `GET /health/ready` → 200 (DB up), 503 (DB stopped) | | | curl transcripts; `HealthEndpointTests` |
| IQ-17 | Role guard active | App started in Production against an over-privileged role **refuses to boot** with the remediation message | | | startup log; `DatabaseRoleGuard` |
| IQ-18 | TLS + HSTS at host | API over TLS at the proxy; `Strict-Transport-Security` present on responses (ADR-0002) | | | `curl -sI …/health/ready`; proxy config |
| IQ-19 | Non-root container | Deployed image runs as a non-root uid; evidence volume writable | | | CI `container` job log; `docker run --entrypoint id … -u` |
| IQ-20 | CHECK constraints present | The `Phase5CheckConstraints` constraints exist on `nonconformance`, `risk_item`, `equipment_item`, `supplier_evaluation`, `work_task`, `training_assignment`, `audit` | | | `SELECT conname FROM pg_constraint WHERE contype='c' …` |
| IQ-21 | Refresh-session + idempotency schema | `qams.refresh_session` and `qams.idempotency_record` tables present per migrations `Phase7RefreshSessions` + `Phase4IdempotencyRecords` | | | `\dt qams.*`; `dotnet ef migrations list` |
| IQ-22 | Metrics endpoint | `GET /metrics` returns Prometheus text (RED + `qams_outbox_*` + `qams_job_*`) | | | `/metrics` sample |
| IQ-23 | Observability stack (if deployed) | Collector/Prometheus/Grafana up; targets UP; alert rules loaded | | | `deploy/observability/`; Prometheus `/targets`, `/alerts` |
| IQ-24 | CI vulnerability-scan gates active | The deployed build's CI run shows ".NET SCA", "npm SCA", and "Trivy image vulnerability scan" steps executed and green; exception register reviewed (currently empty) | | | GitHub Actions run log; `.github/npm-audit-allowlist.txt` |
| IQ-25 | Frontend framework version | Deployed SPA built from `@angular/* 22.0.8` on the Node 24 / npm 11 toolchain; build artifact matches the tagged release (v1.49.0+) | | | `frontend/package.json` + lock; CI "Setup Node 24" + AOT build log |

---

## Part C — Operational Qualification (OQ) delta

Append to QP-NTQMS-001 Part 2. The named automated suite is the OQ evidence engine; a
witnessed manual confirmation is recorded per baseline convention.

### New OQ evidence-engine suites (add to the QP evidence-engine table)

| Suite / artefact | Coverage | Cited for |
| ---------------- | -------- | --------- |
| `IntegrationTests` (added) | Role guard, readiness/topology, optimistic concurrency, outbox resilience, CHECK constraints, migration round-trip, mid-chain tamper | OQ-DEP/MSG/AUD |
| `WebApi.FunctionalTests` (added) | Health, config fail-fast, security headers + 429, problem+json, pagination, file hardening, versioning + surface snapshot, idempotency, refresh sessions, auditor deny-matrix, role×endpoint matrix, contract coverage, observability | OQ-DEP/SEC/API/OBS |
| `Application.UnitTests` (added) | Authorization behavior, idempotency behavior, outbox processor (dead-letter/backoff/redelivery), trace propagation | OQ-API/MSG/OBS |
| `Architecture.Tests` (added) | Command-policy completeness, module boundary | Design-integrity control |
| Frontend (added) | axe a11y scans, load-more pager, change-reason dialog specs | OQ-UI |
| `scripts/security-probe.ps1`, `security-probe-deep.ps1`, `failure-drills.ps1` | Executed adversarial + operational drills (24/24 checks, live poison→dead-letter) | Supplementary OQ evidence |
| CI SCA/Trivy gates (added, v1.49) | .NET SCA + npm SCA (vs exception register) + Trivy image scan, every push | OQ-SCA |

### OQ manual/witnessed cases (templates)

> **Execution status (2026-07-29).** A witnessed session executed 12 of these cases on the
> **development** environment against v1.49.0 — actual results transcribed in
> [`09-OQ-Execution-Record-v1.49.md`](09-OQ-Execution-Record-v1.49.md) (OQ-EXEC-NTQMS-001):
> 12 passed, 0 failed, 1 deviation (DEV-01), 0 defects. That record states its own
> limitations — development environment, limited independence, Part D not executed, and the
> IQ steps needing a deployed host still open. The cells below remain **Template** until QA
> executes on a **qualified** environment and signs; the dev-session record attaches as
> supporting evidence, not as the qualification itself.

| Case | Procedure | Expected | Actual | P/F |
| ---- | --------- | -------- | ------ | --- |
| OQ-DEP-01 | Point config at an over-privileged role in Production; start | Boot refused; message cites `harden-runtime-role.sql` | | |
| OQ-DEP-02 | Stop PostgreSQL; hit `/health/ready`; restart | 503 while down; 200 after recovery (see `scripts/failure-drills.ps1` Drill 1) | | |
| OQ-DEP-04 | Set an invalid value for a guarded config key; start | Startup fails naming the key | | |
| OQ-MSG-01 | Two concurrent edits to one record | Exactly one succeeds; other 409 `CONCURRENCY-409` | | |
| OQ-MSG-02 | Inject a poison outbox event | Dead-letters after MaxAttempts; healthy events unaffected (`failure-drills.ps1` Drill 2) | | |
| OQ-SEC-11 | Burst the login endpoint | 429 + Retry-After after the budget | | |
| OQ-SEC-13 | Sign in; reload the SPA | Session survives via silent refresh; no token in web storage | | |
| OQ-SEC-14 | Replay a rotated refresh token | Rejected; whole family revoked (successor also fails) | | |
| OQ-SEC-15 | Drive each role against the gated surface | Reads 2xx/404; denials 403 problem+json; no leakage | | |
| OQ-API-01 | Trigger validation/auth/not-found errors | All `application/problem+json` with a stable code | | |
| OQ-API-03 | Upload a renamed executable as `.pdf` | Rejected 422 `FILE-415`; valid file stored with canonical type; download is attachment | | |
| OQ-API-05 | Submit the same command twice with one Idempotency-Key | One record; second call replays the first response | | |
| OQ-OBS-02 | Issue one request; inspect logs/trace | Correlated log + trace id; `traceId` echoed in errors | | |
| OQ-UI-01 | Delete a record in the SPA | Accessible reason dialog (role=dialog, focus, Escape); reason sent as `X-Change-Reason` | | |
| OQ-SCA-01 | Run `npm audit --omit=dev` against the shipped `frontend/package-lock.json`; inspect `.github/npm-audit-allowlist.txt` | 0 high/critical advisories; exception register empty (or every entry carries a documented reason, compensating control, and tracked fix) | | |
| OQ-SCA-02 | Smoke the upgraded SPA on the qualified host: sign in, open an NC list (load-more pager), delete with reason dialog, sign out | All regulated-flow UI behaviours unchanged post-Angular-22; no console errors | | |

---

## Part D — Performance Qualification (PQ) delta

| Case | Procedure | Acceptance | Actual | P/F |
| ---- | --------- | ---------- | ------ | --- |
| PQ-PERF-01 | Run `tests/NT.QAMS.LoadTests` against staging (≥100 VUs) from a separate host | Read p95 < 500 ms; error rate < 0.1%; zero dead-letters nominal | | |
| PQ-PERF-02 | 24 h soak on staging with dashboards recording | No memory/connection leak; job liveness maintained; no unhandled errors | | |
| PQ-OBS-01 | Failure drills on staging (`failure-drills.ps1`) | Readiness + dead-letter alerts fire in Prometheus/Grafana | | |

> Dev-workstation baseline already recorded (`docs/reference/NT_QMS_Load_Test_Report.md`:
> p95 86–105 ms, 0% errors @50 VUs) — informational; the authoritative PQ runs on staging.

---

## Part E — Validation Summary Report (VSR) addendum

Append to VSR-NTQMS-001. The change program v1.38→v1.49 is hardening + assurance evidence
on top of the validated 1.0 baseline; it introduces no change to the validated domain model,
database design, or public API contracts (additive only — ADR-0004). The v1.49.0 Angular
upgrade is presentation-layer only (no migration; API surface snapshot unchanged).

| Program item | Area | Resolution (evidence) |
| ------------ | ---- | --------------------- |
| Deployment safety (Ph 0) | Install/ops | Role guard, readiness probes, single-replica topology (URS-056..058; IQ-16/17; ADR-0001) |
| Data consistency (Ph 1) | Integrity | xmin→409 concurrency; outbox dead-letter/backoff/dedup/SKIP-LOCKED/retention (URS-063..067; ADR-0005/0006) |
| Observability (Ph 2) | Ops/diagnosability | Structured logs, end-to-end tracing, metrics + alerts (URS-068..070) |
| Edge security (Ph 3) | Security | Rate limiting, CSP/headers, TLS/HSTS (URS-071..073; ADR-0002) |
| API polish (Ph 4) | API/CQRS | problem+json, pagination, file hardening, deny-by-default authz, idempotency, versioning (URS-075..081; ADR-0004) |
| DB/config/container (Ph 5) | Integrity/install | CHECK constraints, fail-fast config, DB retry, non-root container (URS-059..062; IQ-19/20) |
| Coverage & governance (Ph 6) | Assurance | Migration round-trip + tamper tests, module-boundary + surface gates, AUTHZ→403 (URS-082/083) |
| Session security (Ph 7) | Security | Rotating refresh cookie + reuse detection; memory-only token (URS-074; ADR-0009 supersedes ADR-0003) |
| Evidence at scale (Ph 8) | Performance/ops | Load baseline + failure drills + observability stack (PQ-PERF/OBS) |
| Assurance depth (Ph 9) | Assurance/UX | Role×endpoint matrix, contract coverage, a11y in CI (URS-083..085) |
| Supply-chain assurance (v1.49) | Security/assurance | CI SCA (.NET + npm w/ exception register) + Trivy image scan; Angular 18.2→22.0.8 — 10 high-severity framework advisories cleared, `npm audit` (prod) = 0, register empty (URS-086..088; IQ-24/25) |

**Independent security validation.** An in-house automated adversarial assessment executed 24
checks with 0 findings (`docs/reference/NT_QMS_Security_Assessment_Report.html`). An
**independent penetration test against staging remains an open external activity**
(`NT_QMS_PenTest_SOW.md`); its report attaches here before the security dimension is claimed
closed.

**Overall (delta).** Subject to QA execution and sign-off of Parts A–D, and completion of the
external activities (independent pen test; staging PQ/soak; this re-validation), NT.QMS at
v1.49.0 is validated for its intended use with the change program as documented hardening
evidence.

---

## Part F — Execution checklist for QA (what "done" requires)

- [ ] Environment qualified (baseline IQ + Part B IQ-16..25) on the target/staging host.
- [ ] Automated evidence engines attached: a green CI run (incl. the SCA + Trivy gates) +
      local `dotnet test` (370 backend, 0 skipped) + frontend (67 specs, Angular 22) +
      e2e (6, incl. a11y) transcripts.
- [ ] Part C OQ manual/witnessed cases executed and signed.
- [ ] Part D PQ executed on staging (load + soak + alert-fires drill).
- [ ] RTM delta statuses moved Template → Verified/Executed with evidence references.
- [ ] Independent pen-test report received and its findings dispositioned.
- [ ] VSR addendum signed; the re-validation is dated and approved by the System Owner.

*Prepared by Engineering as a QA-execution draft. Engineering does not self-certify validation;
QA owns execution, review, and approval.*
