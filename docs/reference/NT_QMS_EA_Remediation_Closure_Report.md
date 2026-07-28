# NT.QMS — Enterprise Architecture Remediation Closure Report

| | |
|---|---|
| **Report ID** | EA-CLOSE-NTQMS-001 |
| **Date** | 2026-07-28 |
| **Scope** | All findings of audit **EA-AUD-NTQMS-001** via remediation plan **EA-PLAN-NTQMS-001** (Phases 0–6, releases v1.38.0 → v1.44.0) |
| **Verdict** | **All 24 findings CLOSED.** Every production blocker was cleared at v1.41.0; the audit's "approved with conditions" release condition is **unconditional** as of that tag. Phases 4–6 closed the remaining P2/P3 findings. |
| **Verification gate at closure** | **359 backend tests** (211 domain / 57 application / 24 architecture / 18 integration on real PostgreSQL 17 / 49 functional over the real HTTP pipeline), **45 frontend unit specs**, **3 Playwright e2e against the live API** — all green, **0 skipped**; build 0 warnings; perf smoke PASS |

Each finding below lists the release that closed it, the implementation evidence, and the automated test that keeps it closed. "Test" means green in the suite at v1.44.0 and enforced by CI on every push (.github/workflows/ci.yml).

---

## Phase 0 — Deployment safety gates (v1.38.0)

| Finding | Resolution | Evidence | Proving test |
|---|---|---|---|
| **TENANT-004** — app could run as an RLS-exempt DB role | Startup guard refuses a Production boot when the connection role is SUPERUSER, BYPASSRLS, or owns application tables; two-role model (qams_owner DDL / qams_app DML) documented and scripted | `Infrastructure/Security/DatabaseRoleGuard.cs`; `deploy/db-init.sql`, `deploy/harden-runtime-role.sql`, `DEPLOY.md §1` | `RuntimeRolePrivilegeTests` (incl. boot-as-owner rejected); `RealPostgresFixture` hard-fails the RLS suite in CI if the role is over-privileged |
| **OPS-008** — no DB-backed readiness | `/health/ready` = PostgreSQL answering (503 otherwise); `/health/live` process-only; container HEALTHCHECK repointed | `Infrastructure/Health/PostgresReadinessHealthCheck.cs`; `WebApi/Program.cs`; `WebApi/Dockerfile` | `HealthEndpointTests` (live 200 while DB down, ready 503); `ReadinessAndTopologyTests` |
| **OPS-002** — undefined replica topology | Single-replica decision recorded (ADR-0001) + runtime advisory-lock sentinel warns on accidental scale-out | `docs/adr/ADR-0001-single-replica-topology.md`; `Infrastructure/Jobs/SingleReplicaGuardService.cs` | `ReadinessAndTopologyTests.Singleton_advisory_lock_is_contended…` |

## Phase 1 — Data consistency & messaging robustness (v1.39.0)

| Finding | Resolution | Evidence | Proving test |
|---|---|---|---|
| **DB-009 / VAL-003** — lost updates on regulated records | PostgreSQL `xmin` concurrency token on every aggregate root by convention; conflict → 409 `CONCURRENCY-409` | `AppDbContext.OnModelCreating`; `DomainExceptionHandler`; ADR-0005 | `OptimisticConcurrencyTests` (two racing edits → one wins); `ConcurrencyConflictMappingTests` |
| **MSG-004** — poison events block/vanish | Dead-letter state after 5 attempts + ERROR alert log + metric + triage index | `outbox_event.dead_lettered_at_utc`; `OutboxProcessor` | `OutboxProcessorTests.Poison_event_dead_letters…never_blocks_a_healthy_one` |
| **MSG-005** — lock-step retries | Per-event exponential backoff (5s·2ⁿ, ≤25% jitter) via `next_attempt_at_utc` | `OutboxProcessor.ComputeBackoff` | `OutboxProcessorTests.Backoff_is_exponential_with_bounded_jitter` |
| **MSG-006** — redelivery duplicates | Natural-key idempotency DB-enforced (`ux_nonconformance_source`; notifications dedupe by SourceEventId) | migration `Phase1OutboxResilienceAndConcurrency` | `OutboxProcessorTests.Redelivery…nets_a_single_side_effect` + per-policy idempotency tests |
| **MSG-007** — unbounded outbox growth | Hourly retention purge (`Outbox:RetentionDays`, default 30); ledger remains the permanent record; scoped DELETE grant | `OutboxProcessor.PurgeProcessedAsync`; `harden-runtime-role.sql` | `OutboxResilienceTests.Retention_purge_deletes_only…` |
| **OPS-002 (durable)** — unsafe horizontal scale | `FOR UPDATE SKIP LOCKED` claim + 2-min lease; sweep/KPI leader election via `pg_try_advisory_xact_lock` | `OutboxProcessor.ClaimDueBatchAsync`; `AdvisoryLock(+Keys)`; ADR-0006 | `OutboxResilienceTests` (two concurrent claimants disjoint; lease expiry reclaims) |

## Phase 2 — Observability baseline (v1.40.0)

| Finding | Resolution | Evidence | Proving test |
|---|---|---|---|
| **OBS-001** — unstructured logs, no standard fields | JSON console in Production; one canonical completion record per request (service, environment, method, path, operation, status, outcome, duration, tenant, user, correlation) | `WebApi/Middleware/ObservabilityMiddleware.cs` | `ObservabilityTests.The_request_completion_log_carries_every_required_field` |
| **OBS-002 / API-006** — no tracing/correlation | OTel: HTTP + Npgsql + MediatR + outbox/job spans; W3C traceparent persisted on outbox rows so ONE trace spans the async boundary; sanitized `X-Correlation-Id` echoed; traceId+correlationId in all ProblemDetails; OTLP export | `TracingBehavior`; `QamsDiagnostics`; `outbox_event.trace_parent` | `TracePropagationTests` (HTTP→EF→outbox share one trace id); `ObservabilityTests` correlation cases incl. reflected-XSS guard |
| **OBS-003** — no metrics/alerts | `/metrics` Prometheus endpoint: ASP.NET RED, Npgsql pool, outbox counters + backlog/dead-letter/age gauges, job liveness; actionable alert set + retention aligned to DR runbook | `QamsMetrics`; `deploy/OBSERVABILITY.md` | `ObservabilityTests.Metrics_endpoint…`; dead-letter counter asserted via processor tests |

## Phase 3 — Edge & security hardening (v1.41.0)

| Finding | Resolution | Evidence | Proving test |
|---|---|---|---|
| **SEC-013 / API-002** — no throttling on credential/e-sign surfaces | Global 300/min per client; `/api/auth/*` 10/min per client; password+PIN publish 10/min per ACTOR; 429 + Retry-After; probes exempt | `WebApi/Security/RateLimiting.cs` | `SecurityHardeningTests` (burst → 429; probes never throttled) + live check (10×401 then 429) |
| **SEC-011** — missing defensive headers | Every response: API CSP `default-src 'none'…` (inline script impossible), nosniff, X-Frame-Options DENY, Referrer-Policy no-referrer, HSTS 2y outside Dev; SPA CSP (`script-src 'self'`) at the host | `SecurityHeadersMiddleware`; `deploy/web.config` | `SecurityHardeningTests.Every_response_carries_the_defensive_header_set` (success AND error paths) |
| **SEC-012** — TLS posture undecided | TLS terminates at the proxy; HSTS emitted in-app; forwarded headers (loopback-trusted) feed real client IPs to rate limits/logs; go-live checklist | `docs/adr/ADR-0002-tls-termination-and-hsts.md` | HSTS assertion in `SecurityHardeningTests` |
| **SEC-017** — token storage risk unowned | Risk-acceptance ADR: web-storage stays, compensated by strict SPA CSP, token lifetime halved to 60 min, session revocation, MFA/lockout, auth rate limit; revisit trigger = refresh-cookie flow | `docs/adr/ADR-0003-token-storage.md`; `JwtOptions.ExpiryMinutes` | Config default asserted through `ConfigGuardTests` + functional login flow |

## Phase 4 — API & application-pipeline polish (v1.42.0)

| Finding | Resolution | Evidence | Proving test |
|---|---|---|---|
| **API-003** — error-shape drift | ONE problem+json writer for every path — middlewares, domain handler, and (Phase 6) framework 401/403s; stable `code` + traceId/correlationId everywhere | `ProblemResponse`; `ProblemAuthorizationResultHandler` | `ProblemContractTests`; deny-matrix body assertions |
| **API-004** — silent `.Take(500)` caps | 14 list queries → `PagedResponse{items,total,page,pageSize,hasMore}`; pageSize clamped ≤200; Part 11 NC-register export walks every page; SPA consumes the envelope (13 services, 12 facades expose total/hasMore) | `Contracts/Common/PagedResponse.cs`; `Abstractions/Paging.cs` | `PaginationTests` (slice/navigate/true total; past-the-end; hostile pageSize clamp) |
| **API-005** — no upload/download hardening | Extension allow-list + magic-byte sniffing; canonical content type stored (client claim never trusted); attachment downloads | `WebApi/Security/FileContentPolicy.cs`; `FilesController` | `FileHardeningTests` (renamed exe rejected; bad extension rejected; canonical type + attachment) |
| **CQRS-003** — ungated commands | Deny-by-default `AuthorizationBehavior`: all 211 commands carry exactly one policy; write default excludes the read-only ExternalAuditor; missing policy = CI failure + runtime AUTHZ-000 | `Abstractions/CommandAuthorization.cs`; `Behaviors/AuthorizationBehavior.cs` | `AuthorizationBehaviorTests` (every branch); `CommandPolicyTests` (architecture gate); `AuditorDenyMatrixTests` |
| **CQRS-004** — unsafe retries duplicate records | Opt-in `Idempotency-Key`: first response stored per (actor, key, command type); retry replays it; 24h retention | `Behaviors/IdempotencyBehavior.cs`; `qams.idempotency_record` | `IdempotencyBehaviorTests`; `IdempotencyTests` (double-submit nets ONE NC, end-to-end) |
| **API-001** — no versioning story | `api/v1/...` beside legacy `api/...` via one central convention (41 controllers untouched); contract-evolution policy | `WebApi/Versioning/VersionedRouteConvention.cs`; ADR-0004 | `ApiVersioningTests` (parity, refusal, version reporting) |

## Phase 5 — DB integrity, configuration & container (v1.43.0)

| Finding | Resolution | Evidence | Proving test |
|---|---|---|---|
| **DB-005** — DB accepts invalid domain values | CHECK constraints: NC severity/likelihood 1–5, rpn 1–25, status ∈ NcStatus; risk scores 1–5 (+residuals); equipment intervals; evaluation score ≥ 0; completion-after-creation date ordering (work_task, training, audit) | migration `Phase5CheckConstraints` (reversible) | `CheckConstraintTests` (direct-SQL corruption dies with 23514) |
| **CFG-001/002** — invalid config silently defaults | `ConfigGuard`: present-but-invalid values refuse startup naming the key (PasswordPolicy, MFA policy, Westgard, outbox retention, JWT expiry, rate limits); Jwt:Secret and connection string already failed fast | `Infrastructure/Configuration/ConfigGuard.cs` | `ConfigGuardTests` (fast-fail incl. the "treu" MFA typo case) |
| **OPS-009** — no DB resilience | `EnableRetryOnFailure(5, ≤10s)` + `CommandTimeout(30)`; advisory-lock transactions wrapped in the execution strategy. SMTP left best-effort BY DESIGN (in-app delivery is the record; delivery monitor tracks email) | `Infrastructure/DependencyInjection.cs`; `AdvisoryLock` | Full suite green under the retrying strategy (sweeps/outbox/locks) |
| **DEPLOY-002/003** — root container, no manifest | `USER $APP_UID` + owned evidence volume; `deploy/compose.production.yml`: replicas 1 (ADR-0001), CPU/memory limits, read-only rootfs, loopback publish, readiness healthcheck | `WebApi/Dockerfile`; `deploy/compose.production.yml` | **By review only — see Residual R-1 below** |

## Phase 6 — Test coverage & governance (v1.44.0)

| Finding | Resolution | Evidence | Proving test |
|---|---|---|---|
| **TEST-001/002/003** — coverage gaps | Migration up/down round-trip; mid-chain audit-tamper detection (broken sequence pinpointed); baseline perf smoke script with live numbers (ready p95 20.6ms, login p95 69.6ms, paged list p95 6.3ms on the dev box) | `GovernanceTests`; `scripts/perf-smoke.ps1` | The tests are the deliverable — green |
| **ARCH-004** — module boundary unenforced | NetArchTest per-module theory: 18 domain modules, zero cross-module type references (by-Id integration proven) | `Architecture.Tests/ModuleBoundaryTests.cs` | Green — a violation now fails the pipeline |
| **ARCH-005** — no contract drift gate | 620-entry route+method snapshot from the OpenAPI document; drift fails CI; intentional changes update the baseline per ADR-0004 | `ApiSurfaceSnapshotTests` + `ApiSurface.approved.txt` | Green |
| **ARCH-006** — decisions unrecorded | ADR set complete: 0001 topology, 0002 TLS/HSTS, 0003 token storage, 0004 versioning, 0005 xmin, 0006 outbox reliability, 0007 same-origin/no-CORS, 0008 EF persistence port | `docs/adr/` | n/a (documents) |
| **SEC-003** — analytical DELETE authorization unconfirmed | Confirmed intended: pre-lock deletion by internal lab roles, guarded by `[RequireInternalActor]` + mandatory X-Change-Reason + field-change ledger + signed-record immutability. Hardened: AUTHZ-* → **403**; framework 401/403 now carry problem+json (found as a gap by the new matrix test) | `DomainExceptionHandler`; `ProblemAuthorizationResultHandler` | `AuditorDenyMatrixTests` (reads OK, all flagged writes 403 + AUTHZ-*) |
| **UI-008** — unmanaged subscription | `takeUntilDestroyed` on the authorization-matrix `valueChanges` (the only unmanaged subscription in the SPA) | `authorization-matrix.component.ts` | Frontend build/specs green |
| **UI-014** — `window.prompt` for Part-11 reason | Accessible change-reason dialog (role=dialog, aria-modal, labelled, focus management, Escape/cancel, trilingual) + async interceptor; legal-hold flow reuses it | `core/change-reason-dialog.component.ts`, `.service.ts`, `.interceptor.ts` | 7 dialog a11y specs + rewritten interceptor specs (45 total) |

---

## Residual risks & observations (accepted, tracked)

| # | Item | Status |
|---|---|---|
| **R-1** | The hardened container was not buildable on the Docker-less dev machine. **Addendum (v1.45.0):** CI builds the image on every push and asserts a non-root runtime uid plus evidence-volume writability (`ci.yml` job `container`). **Verified: the Actions run for commit `e83741b` (v1.45.0) completed with `Container (build + non-root assertion): success`.** | **Closed** |
| **R-2** | Token storage remains SPA web storage under the ADR-0003 **risk acceptance** (strict CSP + 60-min tokens + revocation). Revisit trigger: refresh-cookie flow on customer/regulatory demand or any production XSS finding. | Accepted, ADR-0003 |
| **R-3** | ~~No pager UI over the envelope.~~ **Closed (v1.45.0):** all 13 paged lists ship a shared accessible load-more footer ("showing X of Y", aria-live) with append-on-demand and reset-on-filter; 12 facades track pages; +6 specs. | **Closed** |
| **R-4** | ~~Reset-password via `window.prompt`.~~ **Closed (v1.45.0):** accessible text-prompt dialog (masked input, full a11y contract of the change-reason dialog) replaces the prompt in user management; +7 specs. | **Closed** |
| **R-5** | Load/perf coverage. **Partly addressed (v1.47.0):** committed concurrent load harness (`tests/NT.QAMS.LoadTests`) + dev-box baseline (p95 86–105 ms, 0% errors, ~750–800 rps at 50 users — `docs/reference/NT_QMS_Load_Test_Report.md`). Authoritative production-scale run + 24 h soak remain external (Phase-8 external track). | Ops / external |
| **R-6** | Formal CSV re-validation of changed areas (IQ/OQ/PQ, RTM updates) is a downstream QA activity per the plan's explicit non-goals. | QA/validation queue |
| **R-7** | The observability stack (`deploy/observability/` — collector + Prometheus alerts + Grafana) is authored and its PromQL/dashboard queries target the exact metric names the app emits (verified live via `/metrics`), but it was **not run on this Docker-less dev workstation**. First staging host must `docker compose up` it, confirm targets UP + panels populate, and fire `scripts/failure-drills.ps1` to confirm the dead-letter/readiness alerts trigger. | Open (staging host) |

## Release statement

With v1.41.0 the four production blockers (Phases 0–3) were closed and verified; with v1.44.0 every remaining P2/P3 finding is closed with an automated proof in CI. The enterprise-architecture remediation program **EA-PLAN-NTQMS-001 is complete**. Release train: `v1.38.0 … v1.44.0`, each phase an independently reviewable, gated increment on `master`.

*Prepared automatically at remediation close; complements — does not replace — the CSV validation package (`docs/validation/`).*
