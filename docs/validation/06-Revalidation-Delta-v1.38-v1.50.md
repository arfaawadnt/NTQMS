# CSV Re-Validation Delta — v1.38.0 → v1.51.2

| Field | Value |
| ----- | ----- |
| Document ID | REVAL-NTQMS-001 (rev 5 — extended to v1.51.2; filename retained for reference stability) |
| System | NT.QMS |
| Baseline validated version | 1.0 (VMP/URS/FRA/QP/RTM/VSR — docs 00–05) |
| Scope of this delta | Changes across releases **v1.38.0 → v1.51.1** (EA-remediation Phases 0–6 + Road-to-100 backlog/Phases 7–9 + v1.49 supply-chain assurance & Angular 22 upgrade + v1.50 sign-in surface & statistic presentation + **v1.51 Role Privilege module**, v1.51.1 RP-D1 audit-attribution fix, and the **v1.51.2 database schema-hardening programme** (6 migrations)) |
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
`docs/reference/NT_QMS_EA_Remediation_Closure_Report.md`, `…_Compliance_Audit_v1.50.0.html`,
`…_Enterprise_Application_Compliance_Audit.html` (EAC-NTQMS-001 rev 4, covers v1.51.2).

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

### A.8 Sign-in surface & statistic presentation (v1.50)

> **Change assessment (v1.50.0).** Presentation-layer rework plus **one new unauthenticated
> endpoint**. No change to the validated domain model, no migration, and no change to any
> existing API contract (the reporting DTO gained a member additively; `ApiSurface.approved.txt`
> grew by the new route and its versioned twin only). The regulated significance is (a) a new
> anonymous read surface, which is a security control question, and (b) the numbers displayed on
> every register and the dashboard now assert a *proportion*, so a wrong denominator would be a
> misstatement of quality data — hence URS-091's explicit prohibition on inferred denominators.

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-089 | A laboratory shall be identified on its own sign-in page by its registered **name**, resolved from the address slug before authentication; the identifier shall not be presented as the name. | `Application/Tenancy/Queries/GetWorkspace.cs`; `AuthController.Workspace`; `frontend/core/api/workspace-api.service.ts`; `login.component.ts` | AUTO `WebApi.FunctionalTests/WorkspaceLookupTests`; OQ-UI-03 | Template |
| URS-090 | The pre-authentication workspace lookup shall disclose the laboratory name and nothing further, and shall not reveal whether a slug is unknown, malformed or belongs to a non-active tenant. | `GetWorkspaceQuery` (Active-only projection of `Name`); uniform `404 problem+json`; strict auth rate-limit partition | AUTO `WorkspaceLookupTests` (payload-shape assertion: exactly one JSON property; 3 indistinguishable-miss cases); OQ-SEC-16 | Template |
| URS-091 | Where a statistic is presented as a proportion, the denominator shall be a real counted population; the system shall **not** infer a denominator, and shall present a plain count where no population exists. | `shared/ui/list-stats.component.ts` (`of`, opt-in `ratioFromFirst`, refusal guards); `Reporting/ReportingQueries.cs` (`DashboardKpiTotalsDto`, 11 real counts) | AUTO `list-stats.component.spec.ts` (7 cases incl. refusal), `WebApi.FunctionalTests/DashboardKpiTotalsTests` (3 cases); OQ-UI-04 | Template |
| URS-092 | No displayed proportion shall exceed its own population. | `DashboardKpiTotalsDto` pairing (each KPI to the population it is drawn from); component guard `value <= whole` | AUTO `DashboardKpiTotalsTests.No_kpi_ever_exceeds_its_own_population`; `list-stats` refusal case | Template |
| URS-093 | The sign-in surface and statistic tiles shall meet WCAG AA contrast; brand tones that fail as text or marks shall be replaced by compliant steps of the same hue rather than used as-is. | `styles.css` `--nt-ink-*` tokens; documented deviations in `login.component.ts` and `list-stats.component.ts` | AUTO axe scan `e2e/a11y.spec.ts` (0 serious/critical); INSP measured ratios (value 11.5:1, label/caption 9.5:1, meter fills ≥3:1) | Template |
| URS-094 | Platform (administrator) sign-in shall be visually and textually distinct from a laboratory workspace sign-in. | `login.component.ts` platform variant (shield mark, `login.adminPortal`, admin-specific copy) | OQ-UI-05 | Template |

### A.9 Role Privilege module — configurable roles, permission matrix, working scope (v1.51.0 / v1.51.1)

> Unlike A.1–A.8, this section's OQ cases were **executed** (development environment,
> witnessed session 2026-07-31) and are transcribed with verbatim actuals in
> [`12-OQ-Execution-Record-RolePrivilege-v1.51.md`](12-OQ-Execution-Record-RolePrivilege-v1.51.md)
> (OQ-EXEC-NTQMS-002) — **unsigned until the witness/QA signs**; the record found one
> defect (RP-D1, fixed and re-tested in-session, shipped as v1.51.1) and one
> environmental observation (OBS-1). URS-005's baseline trace (role-name authorization)
> is superseded by this section — see RTM-NTQMS-001 rev 1.1.

| URS | Requirement | Design element(s) | Verification | Status |
| --- | ----------- | ----------------- | ------------ | ------ |
| URS-095 | Authorization shall be governed by tenant-configurable roles composed over a closed, code-defined permission catalogue (module × action); a grant that maps to no code path shall be impossible to store, and seeded system roles shall reproduce the pre-existing fixed tiers so enabling the module changes nobody's reach. | `Domain/Authorization/PermissionCatalog.cs` (31 modules × 8 actions = 170 keys; ROLE-005), `Role.cs` aggregate (FORCE RLS; ROLE-003/004 system-role protections); `Application/Authorization/SystemRoleCatalog.cs` (seeded parity table); `[RequirePermission]` on 127 endpoint gates; `[RequirePermissionPolicy]` command policies | AUTO `Domain.UnitTests/Authorization/RoleTests`+`PermissionCatalogTests`, `Application.UnitTests/Authorization/SystemRoleCatalogTests` (parity pins), `WebApi.FunctionalTests/RolePrivilegeFlowTests`; **OQ-RP-01/02/03/07 executed** | **Executed (dev), unsigned — doc 12** |
| URS-096 | Granting and revoking a privilege shall take effect on the affected user's next request, without waiting for session or token expiry. | Per-request DB resolution: `ActiveSessionMiddleware` + `Infrastructure/Authorization/PrivilegeResolution.cs` (deliberately uncached) | AUTO `RolePrivilegeFlowTests`; **OQ-RP-04 executed** (403 → grant → 201 on the same token → revoke → 403) | **Executed (dev), unsigned — doc 12** |
| URS-097 | A user's allowed branches/departments shall be a hard data boundary: out-of-scope records shall be neither readable nor writable; unattributed (tenant-wide) records remain visible; empty scope means unrestricted. | Composed tenant+scope EF global filter on all 12 `IAllocatable` aggregates (`AppDbContext`); `OrgScopeGuardInterceptor` (SCOPE-001/002); `UserAccount` owned scope tables | AUTO `RolePrivilegeFlowTests` (reads + writes), `Domain.UnitTests/IdentityAccess/UserScopeTests`; **OQ-RP-06 executed** | **Executed (dev), unsigned — doc 12** |
| URS-098 | No sequence of role edits, deactivations or reassignments shall leave a tenant without an active user able to administer privileges. | `Application/Authorization/RolesSlice.cs` `ManageRolesLockoutGuard` (ROLE-006) | AUTO `Application.UnitTests/Authorization/RoleHandlersTests`; **OQ-RP-05 executed** | **Executed (dev), unsigned — doc 12** |
| URS-099 | Every change to who-may-do-what — role grants (with the operator's reason), role assignments, working-scope changes — shall be captured in the tenant's own tamper-evident audit trail. | Domain events → outbox → hash-chained `audit.audit_trail`; **v1.51.1 fix RP-D1**: `SharedKernel/MultiTenancy/IOptionallyTenantScoped.cs` on `UserAccount` + outbox tenant fallback | AUTO `Application.UnitTests/Authorization/UserEventTenantStampTests` (RP-D1 pins); **OQ-RP-09 executed: failed → RP-D1 raised/fixed → re-tested Pass** | **Executed (dev), unsigned — doc 12** |

**Defect RP-D1 (raised by OQ-RP-09, closed in v1.51.1).** User-account access-control
events (`UserRoleAssigned`, `UserScopeChanged`, pre-existing `UserLockedOut`) were
ledgered with an empty tenant id — present in the hash chain, invisible to the tenant's
own compliance view. Root cause, blast radius (SQL-measured), fix and re-test are in
doc 12 §3. **Residual for QA disposition:** rows written before the fix retain the empty
tenant id (the ledger is append-only and is not restated).


### A.10 Database schema hardening — isolation, domains, keys (v1.51.2, migrations `Hardening1`…`Hardening6`)

> **Execution status — read before relying on this section.** The engineering verification is
> unusually strong for a delta of this size (see the evidence column, and
> `SCHEMA-HARDENING-REPORT.md`), but **no witnessed OQ session has been executed for this
> programme**. The cases in Part C A.10 are therefore **Template**. The catalog introspection,
> the 442-test suite and the live application checks attach as *supporting evidence*, not as the
> qualification itself. Two defects were found by that verification and are recorded below,
> because a delta that reports only successes is not evidence.

| URS | Requirement | Design element(s) | Verification | Status |
| --- | ----------- | ----------------- | ------------ | ------ |
| URS-100 | Every table holding tenant data shall be fenced at the database, not only by the application: row-level security enabled **and forced**, with a `tenant_isolation` policy. A table carrying a non-nullable `tenant_id` without that protection shall be impossible to leave in the schema unnoticed. | `Hardening2_RlsGapClosure` (`audit.security_event`, `qams.ref_counter`); `Hardening4_ChildTenancy` (30 owned child tables) | INSP catalog: 90 FORCE-RLS tables / 90 policies / **0 parity violations**; AUTO `OwnedChildTenancyTests.Every_owned_child_table_carries_tenant_id_and_full_rls` (structural sweep — fails if any future table regresses); OQ-DB-01 | Template |
| URS-101 | An owned child record shall be readable only by the tenant that owns its parent, and a child whose tenant differs from its parent's shall be **impossible to persist** — not merely detected. | `Hardening4`: `tenant_id NOT NULL` on 30 children (backfilled from the parent), 28 tenant-composite FKs, 24 parent `UNIQUE (id, tenant_id)`; shadow `TenantId` + `TenantStampInterceptor` | AUTO `OwnedChildTenancyTests` (7 per-family isolation cases + drift rejection with an accepted control); INSP measured `rca_record` 2 rows → owning tenant sees 1, foreign 0, nil tenant 0; OQ-DB-02 | Template |
| URS-102 | A value persisted in a state, status, role or classification column shall be one the application can actually produce; integrity hashes shall be well-formed. | `Hardening3_CheckDomains`: 64 enum domains derived from the C# enums + 2 closed literal sets + 5 hash-format constraints, `NOT VALID` → `VALIDATE` | Pre-flight `scripts/preflight-enum-domains.sql` (67 checks, 0 violations) — the **same generator** produces the scan and the constraints, so they cannot disagree; AUTO `CheckConstraintTests.Phase3_domains_reject_bogus_enum_values_and_malformed_hashes`; INSP 85 CHECKs, 0 left `NOT VALID`; OQ-DB-03 | Template |
| URS-103 | The schema shall be partition-ready: the tenant discriminator shall lead the primary key of every tenant-owned table, since PostgreSQL requires the partition key in every primary key and unique index and cannot convert an existing table into a partitioned one. | `Hardening5_CompositeKeys`: 88 tenant-first composite PKs; `department → branch` made composite; no `UNIQUE (id)` added (it would be illegal on a partitioned table) | INSP 88 composite PKs, **0** single-id PKs remain on a NOT NULL tenant table; the 4 nullable-tenant tables retain single keys by necessity; OQ-DB-04 | Template |
| URS-104 | Free-text fields shall not be bounded only by a column width: where the database limit is removed, the limit shall exist in the command validator, so input remains bounded at the API. | `Hardening1_TypesAndNames` (56 columns → `text`/`jsonb`); 17 FluentValidation rules added/extended | INSP 0 remaining `varchar(≥1000)`; the API-bound audit of all 56 columns is in `SCHEMA-HARDENING-PLAN.md` Appendix A2; OQ-DB-05 | Template |
| URS-105 | Database identifiers shall stay within PostgreSQL's limit, so no index or constraint name is silently truncated. | `Hardening1` §1.4: 3 EF-truncated names renamed and pinned with `HasDatabaseName`; abbreviation map in `CLAUDE.md` §5 | INSP **0** identifiers > 62 characters (re-checked after Phase 5 lengthened generated names); OQ-DB-06 | Template |
| URS-106 | A change to a record shall be attributed to the tenant that owns it, including changes to owned child records, so it appears in that tenant's own compliance view. | `FieldChangeInterceptor.TenantOf` (v1.51.2): `ITenantScoped` → **shadow** `TenantId` → `IOptionallyTenantScoped` → request tenant | AUTO `FieldChangeInterceptorTests.An_owned_childs_change_is_attributed_to_the_owner_tenant_on_an_elevated_write`; INSP live — provisioning wrote 536 `RolePermission` rows, **0 null**, visible to that tenant; OQ-DB-07 | Template |
| URS-107 | Referential integrity to the tenant table shall not depend on the order in which the ORM emits inserts within a transaction. | `Hardening6_DeferrableTenantFks`: the 5 `saas.tenant` FKs become `DEFERRABLE INITIALLY DEFERRED` | INSP 5/5 deferrable; live — tenant provisioning returns 201 (was 500); OQ-DB-08 | Template |

**Two defects found by this programme's own verification, both fixed and re-proven:**

| Ref | Defect | Found by | Closure |
| --- | ------ | -------- | ------- |
| SH-D1 | Applying RLS to `audit.security_event` **broke sign-in** (HTTP 500): `LoginHandler` writes tenant-stamped `LOGIN_*` events before a tenant is resolved, so the new `WITH CHECK` refused them (42501). | Live browser check — the 419-test suite passed, because functional tests run on InMemory where RLS does not exist | Pre-auth handlers scope the request tenant as soon as the workspace slug resolves; failed logins now also appear in their own tenant's view. Pinned by `SecurityEventRlsTests` (4 cases) |
| SH-D2 | `fk_outbox_event_tenant` (added in this programme) **broke tenant provisioning** (HTTP 500, 23503): created in raw SQL, so EF had no model relationship and no reason to order the tenant INSERT before the outbox rows referencing it. | Live check while proving the URS-106 fix — provisioning is not exercised by the InMemory functional tests | `Hardening6` defers the five tenant FKs to COMMIT; the guarantee is unchanged in strength while ordering stops mattering. Provisioning verified 201 |

**Accepted deviation (permanent).** `qams.user_account` and `qams.outbox_event` carry a nullable
tenant and therefore **cannot** hold a `tenant_id`-based policy — applying one would hide every
platform administrator and break authentication, which runs before a tenant exists. Accepted by
the System Owner on 2026-08-01 with compensating controls **verified across all 27 access sites**
(explicit tenant predicate, actor-keyed, or keyed by a tenant-derived id set) and now enforced at
build time by `UserAccountTenantBoundTests`. Full record: `SCHEMA-HARDENING-REPORT.md` §8.

**Dispositioned records.** 32 historical `audit.audit_trail` rows and 21,209 historical
`audit.field_change` rows carry no tenant. Both are **kept as-is** (System Owner, 2026-08-01):
the ledgers are append-only and hash-chained, and a ledger that can be corrected is not a ledger.
Both remain readable under elevation. Forward behaviour is fixed and proven.

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
| IQ-26 | Schema-hardening migrations applied | `__EFMigrationsHistory` contains all 56 migrations, ending `Hardening6_DeferrableTenantFks`; `dotnet ef migrations has-pending-model-changes` reports none | | | migration list; EF output |
| IQ-27 | Tenant fence intact on the installed database | The RLS parity query (`SCHEMA-HARDENING-REPORT.md` §2 / as-built §7) returns **0 rows**; 90 FORCE-RLS tables and 90 `tenant_isolation` policies | | | psql transcript |
| IQ-28 | Guard triggers survived installation/restore | `pg_trigger` shows **17** enabled guards (4 append-only ledgers + 13 `frozen_immutability`), `tgenabled = 'O'` | | | psql transcript |
| IQ-29 | Identifier limit respected | No relation or constraint name exceeds 62 characters | | | psql transcript |
| IQ-30 | Deployment script current | `deploy/migrations.sql` regenerated `--idempotent` covers all 56 migrations; applied as `qams_owner`, followed by `harden-runtime-role.sql`; app connects as `qams_app` | | | script header + migration id list; role grants |

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
| Role Privilege suites (added, v1.51) | `RolePrivilegeFlowTests` (grant flip, lockout guard, scope filter over HTTP), `RoleTests`/`PermissionCatalogTests`, `SystemRoleCatalogTests` (seeded parity pins), `RoleHandlersTests`, `UserScopeTests`, `UserEventTenantStampTests` (RP-D1 pins) | OQ-RP-01..10 |
| Schema-hardening suites (added, v1.51.2) | `OwnedChildTenancyTests` (child isolation, drift rejection, structural RLS sweep), `SecurityEventRlsTests` (ledger isolation, pre-auth write, login-shape pin), `UserAccountTenantBoundTests` (build-time guard on the accepted deviation), `CheckConstraintTests` Phase-3 case, `FieldChangeInterceptorTests` owned-child attribution pin | OQ-DB-01…08 |

### OQ manual/witnessed cases (templates)

> **A.10 (schema hardening, v1.51.2) — NOT executed as protocol.** OQ-DB-01…08 below are
> templates. The behaviour they describe was verified by engineering (catalog introspection,
> 442 automated tests including 12 written for this programme, and live application checks that
> found two defects — SH-D1, SH-D2 — both fixed and re-proven), but **no witnessed session has
> been run**. That evidence attaches as supporting material; it is not the qualification.

> **Execution status (2026-07-29).** A witnessed session executed **18 cases** on the
> **development** environment against v1.49.0, transcribed in
> [`09-OQ-Execution-Record-v1.49.md`](09-OQ-Execution-Record-v1.49.md) (12 cases) and
> [`10-OQ-Execution-Record-Addendum-A-v1.49.md`](10-OQ-Execution-Record-Addendum-A-v1.49.md)
> (6 more + 1 partial). Cumulative: **18 passed, 0 failed, 1 deviation (DEV-01),
> 1 observation closed (OBS-01), and 1 finding — OPS-010 (cold start with PostgreSQL
> unreachable crashed instead of reporting `/health/ready` 503), which was **remediated in
> v1.49.1 and re-tested closed** — see
> [`11-OQ-Execution-Record-Addendum-B-OPS-010-v1.49.1.md`](11-OQ-Execution-Record-Addendum-B-OPS-010-v1.49.1.md)
> (host now starts, live 200 / ready 503, recovers without restart; 6 regression tests; full
> suite 374 passed / 0 skipped). Residual by design: `Database:MigrateOnStartup=true` still
> fails fast, so keep it off where readiness-reports-outage is required.
> Five items remain environment-blocked (live DB service-stop, TLS-at-proxy, deployed
> container, observability stack, PQ load/soak). **The v1.50 cases added in A.8 (OQ-UI-03/04/05,
> OQ-SEC-16) have NOT been executed as protocol** — the underlying behaviour is covered by the
> automated suites and was observed during development, but no witnessed run exists. The cells below remain **Template** until QA
> executes on a **qualified** environment and signs; the dev-session records attach as
> supporting evidence, not as the qualification itself.
>
> **Execution status (2026-07-31, v1.51).** A second witnessed dev-environment session
> executed the Role Privilege module protocol — **10 cases / 30 checks** on a dedicated
> tenant, transcribed in
> [`12-OQ-Execution-Record-RolePrivilege-v1.51.md`](12-OQ-Execution-Record-RolePrivilege-v1.51.md):
> 29/30 first-pass; the single failure was defect **RP-D1** (user-account access-control
> events invisible to the tenant audit trail), **fixed as v1.51.1 and re-tested to Pass
> in-session**; post-fix regression 419 backend tests green. One environmental
> observation (OBS-1: the migration round-trip test empties the newest migration's
> tables on a shared dev DB; the startup backfill self-heals on next boot). The record
> is unsigned until the witness/QA signs.

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
| OQ-UI-03 | Open `/t/{slug}` for a known active laboratory | The pill shows the laboratory's registered NAME (not the slug); initials derive from that name | | |
| OQ-SEC-16 | `GET /api/auth/workspace/{slug}` for (a) an active lab, (b) an unknown slug, (c) a malformed slug, (d) a suspended lab — unauthenticated | (a) 200 with `{"name":…}` and no other property; (b)(c)(d) identical `404 problem+json`, indistinguishable from each other | | |
| OQ-UI-04 | On a register, compare each tile's caption with the list contents; then open a register whose strip has no total | Every "N / M" matches a real count of loaded rows; the strip without a population shows plain counts and NO meter | | |
| OQ-UI-05 | Sign in at `/login` with no laboratory pinned | Platform-administration identity is shown (shield mark, admin wording); laboratory credentials wording is absent | | |
| OQ-DB-01 | On the qualified database run the RLS parity query (as-built §7) and count FORCE-RLS tables and `tenant_isolation` policies | Parity query returns **0 rows**; 90 tables forced; 90 policies. `qams.user_account` and `qams.outbox_event` are the only tenant-carrying exceptions and are the accepted deviation | | |
| OQ-DB-02 | As tenant A, read an owned child register (e.g. CAPA actions / RCA); then attempt, by direct SQL under tenant A's context, to insert a child row referencing tenant B's parent | A sees only its own children and any unattributed rows; the cross-tenant insert is refused (`23503`); the same insert with the parent's own tenant is accepted | | |
| OQ-DB-03 | Attempt by direct SQL to set a status column to a value outside its C# enum, a ledger `action` outside its literal set, and a hash column to malformed hex | All three refused (`23514`) naming `ck_<table>_<column>_domain` / `_sha256`; the append-only trigger still fires on the ledger afterwards | | |
| OQ-DB-04 | Inspect primary keys on the qualified database | Every table with a non-nullable `tenant_id` has a tenant-first composite PK (88); no such table retains a single-column `id` PK; the 4 nullable-tenant tables retain single keys by necessity | | |
| OQ-DB-05 | Submit a request whose free-text field exceeds the former column bound (e.g. a rejection reason > 1000 chars) | Refused at the API with a validation error naming the field — the bound moved to the validator when the column became `text`, so input is still bounded | | |
| OQ-DB-06 | Query `pg_class` and `pg_constraint` for identifier lengths | No relation or constraint name exceeds 62 characters | | |
| OQ-DB-07 | Provision a laboratory, then sign in as its administrator and open the field-change ledger | Privilege-detail rows (`RolePermission`) for that laboratory are present and attributed to it; none carries a null tenant | | |
| OQ-DB-08 | Provision a laboratory (writes tenant, administrator and outbox events in one transaction) | Succeeds (`201`); no `23503` on `fk_outbox_event_tenant` — the tenant FKs are deferred to COMMIT | | |

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
| Role Privilege module (v1.51/.1) | Security/access control | Configurable roles over a closed 170-key catalogue; per-request privilege resolution (immediate revocation); branch/department hard data filter; ROLE-006 lockout guard; privilege changes in the tenant audit trail incl. RP-D1 fix (URS-095..099; OQ-EXEC-NTQMS-002 executed on dev, unsigned) |
| Schema hardening (v1.51.2) | Data integrity / isolation | 6 migrations: RLS closed on the last 2 gap tables and extended to 30 owned child tables (90 FORCE-RLS / 0 parity violations); 71 CHECK domains; 88 tenant-first composite PKs (partition-ready); 28 tenant-composite FKs making cross-tenant children impossible; types/naming corrected. 2 defects found by the programme's own live verification and closed (URS-100..107; IQ-26..30) |

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

- [ ] Environment qualified (baseline IQ + Part B IQ-16..30) on the target/staging host.
- [ ] Sign OQ-EXEC-NTQMS-002 (doc 12) witness/QA lines; disposition defect RP-D1's residual
      (pre-fix ledger rows keep an empty tenant id) and observation OBS-1.
- [ ] Execute and witness the A.10 schema-hardening cases (OQ-DB-01…08) on a **qualified**
      environment. The engineering evidence (introspection, 442 tests, live checks) attaches as
      supporting evidence only — it is not the qualification.
- [ ] Confirm the two permanent acceptances and two record dispositions recorded in A.10 are
      reflected in the site's deviation register.
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
