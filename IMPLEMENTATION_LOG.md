# NT.QAMS Implementation Log

Roadmap reference: `NT_QAMS_Application_Architecture.md` §11.

## Phase 0 — Foundation (2026-07-22) ✅ (one caveat)

**Delivered:**
- Solution skeleton: 6 source + 3 test projects, dependency rules per architecture.
- SharedKernel: Entity/AggregateRoot (Guid v7, domain-event collection), ValueObject,
  DomainEvent, IAuditable, DomainException (coded), ITenantScoped, LocalizedText, IClock.
- Domain/Tenancy walking slice: Tenant aggregate (Provisioning→Active→Suspended⇄→Terminated
  guarded state machine), TenantSlug VO, TenantSettings VO, 4 domain events.
- Application: CQRS markers (ICommand/IQuery), ValidationBehavior + LoggingBehavior pipeline,
  ICurrentTenant/ICurrentUser/IAppDbContext ports, ProvisionTenant + GetTenants slice.
- Infrastructure: AppDbContext (saas/qams schemas, snake_case, convention-applied tenant
  query filters), AuditStampInterceptor, TenantStampInterceptor (throws on unresolved tenant),
  OutboxInterceptor (same-transaction event drain), OutboxProcessor (BackgroundService,
  retry w/ MaxAttempts, DomainEventNotification<T> wrapper), CurrentTenant scoped holder.
- WebApi: thin TenantsController, RFC 7807 DomainExceptionHandler (coded errors), health,
  OpenAPI (dev), empty connection string in appsettings.json (no secrets committed).
- EF migration `InitialFoundation`: saas.tenant (unique identifier), qams.outbox_event
  (partial pending index), REAL model snapshot.
- Tests: 21 green — 14 domain (state machine + slug), 5 architecture (layer rules as merge
  gate), 2 application (provision persists + audit-stamps + outbox row; duplicate slug rejected).
- docker-compose (postgres:17), GitHub Actions CI (build + test), README, .gitignore.

**Environment notes:** .NET 9.0.316 SDK installed user-locally at
`%LOCALAPPDATA%\Microsoft\dotnet` (prepend to PATH + set DOTNET_ROOT). MediatR pinned 12.4.*
(v13 changes license & next() signature).

**Caveat / open item:** no Docker or local PostgreSQL on this machine — the live-database
smoke test (apply migration, POST /api/tenants, verify outbox dispatch) is pending a
PostgreSQL instance. Everything database-independent is verified.

**Deferred to Phase 1 (by design):**
- Authorization pipeline behavior + privilege gates (needs Identity & Access).
- RLS policies (land with the first tenant-scoped business table; raw-SQL migration pattern
  per `NT_QAMS_Database_Architecture.md` §1.1).
- Tenant-resolution middleware from JWT claim (needs JWT auth).
- TenantsController must remain trusted-network-only until Phase 1 gates land.

## Deployment package (2026-07-22) ✅

`deploy/NT.QAMS-webapi-phase0-win-x64.zip` (47.5 MB): self-contained win-x64 publish
(no runtime needed on target), idempotent `migrations.sql`, `db-init.sql` bootstrap,
`DEPLOY.md` (service install, env-var config, verification steps, Phase 0 security
notice: no auth yet → trusted network/loopback only). Published exe smoke-tested:
starts clean in Production env, `/health` → 200. `Database:MigrateOnStartup` flag
added for first-boot schema apply. Dockerfile added for container hosts.
EF package drift fixed (direct Relational reference pins 9.0.18 consistently).

## Increment v0.2 (2026-07-22) ✅ — Auth + NC/CAPA

- **Authentication:** JWT (HS256, config-validated secret, fail-fast), login command
  (tenant users by slug+email, platform admins tenant-less), deny-by-default
  FallbackPolicy (only /api/auth/login and /health anonymous), role gates on
  controllers (seed matrix; fine-grained privilege catalog still pending),
  platform-admin bootstrap from env config, tenant resolution middleware
  (JWT claim only), HttpCurrentUser feeding audit stamps.
- **Identity:** UserAccount aggregate (deliberately not ITenantScoped — platform
  admins are tenant-less; handlers filter (TenantId, Email) explicitly),
  PBKDF2 hashing via Identity's PasswordHasher. Provisioning now creates the
  tenant admin atomically with the tenant.
- **Improvement module:** Nonconformance aggregate — canonical 9-state machine,
  CapaAction + RcaRecord children, RPN, guarded transitions, SOD-CAPA-001
  (raiser ≠ closer) enforced in-aggregate; 9 workflow commands + 2 queries +
  race-free NC refs via ref_counter upsert-returning (IReferenceNumberGenerator).
- **Migration `IdentityAndImprovement`:** user_account, nonconformance, capa_action,
  rca_record, ref_counter + **RLS policy on nonconformance** (dormant until the
  runtime role is split from owner — documented).
- **Tests: 29 green** (22 domain incl. full NC path, loop-backs, SoD; 5 architecture;
  2 application incl. atomic tenant+admin+outbox).
- **Package `deploy/NT.QAMS-webapi-v0.2-win-x64.zip` (48 MB):** publish smoke-tested —
  /health 200, anonymous /api/* → 401. Includes DEPLOY.md (full curl verification
  suite) and ANTIGRAVITY_DEPLOY_PROMPT.md (operations runbook prompt).
- Known deferred: MFA/TOTP, privilege matrix + authorization behavior, e-signature
  PINs, refresh tokens/sessions, capa child tables lack tenant_id column
  (composite-FK hardening), children not RLS-covered until then.

## Increment v0.3 (2026-07-22) ✅ — Document Control + file storage

- **ControlledDocument aggregate:** canonical lifecycle (Draft→Review→Approved→
  Published→Obsolete), major/minor versioning with atomic supersession (publishing
  vN obsoletes vN−1 + emits DocumentVersionObsoleted), one-in-flight-version
  invariant, rejection loop with reason, retire; SoD — author ≠ reviewer
  (SOD-DOC-001) and author ≠ approver (SOD-DOC-002); reviewer/approver identity +
  timestamps recorded per version (full PIN e-signature ceremony still Phase 1).
- **File storage:** IFileStorage port + LocalFileStorage adapter (content-addressed
  {root}/{tenant}/{sha256}, hash-while-streaming to temp + atomic move, natural
  dedupe); FileReference aggregate (tenant-scoped, immutable) — SHA-256 is the
  Part 11 integrity anchor. Upload (50 MB limit, multipart) + download endpoints.
  S3/MinIO adapter is drop-in later. Config: FileStorage__RootPath.
- 7 commands + 2 queries + DocumentsController (role-gated recommend/publish/retire)
  + FilesController.
- **Migration `DocumentControl`:** controlled_document, document_version,
  file_reference + RLS policies on both tenant-scoped tables.
- **Tests: 37 green** (30 domain — incl. 8 document tests: SoD both roles,
  supersession, one-in-flight, major/minor bump, retire; 5 architecture; 2 app).
- **Package `deploy/NT.QAMS-webapi-v0.3-win-x64.zip`** smoke-tested (health 200;
  anonymous /api/documents and /api/files → 401). DEPLOY.md + Antigravity runbook
  updated (file-pipeline verification step h added; FileStorage__RootPath config).
- Note: PS 5.1 Get-Content/Set-Content round-trips corrupt UTF-8 — deploy docs
  rewritten ASCII-safe; use the Write tool/UTF-8-aware editors for these files.

## Increment v0.4 (2026-07-22) ✅ — Audit Management + finding→NC saga

- **Audit aggregate:** Scheduled→InProgress→SignedOff; ISO-clause checklist
  (answer with Conform/Ofi/NonConform + evidence), findings (Ofi/MinorNc/MajorNc);
  sign-off gates: all items answered (AUD-017) AND every NC-graded finding
  acknowledged with its NC (AUD-018); signed-off audits immutable (AUD-020).
- **First cross-module saga** (`FindingToNcPolicy`): FindingRaised (carries
  tenant + actor for background scopes) → policy sets tenant context, raises a
  submitted NC (source=Audit, SourceRef="auditRef#findingId" as idempotency key,
  MajorNc→severity 4 / MinorNc→2) → AcknowledgeFindingNc opens the sign-off gate.
  At-least-once safe: redelivery test proves no duplicate NC.
- `DomainEventNotification<T>` moved Infrastructure→Application.Abstractions so
  policies subscribe without violating layer rules. NC gained SourceRef.
- **Migration `AuditManagement`** (audit, audit_checklist_item, audit_finding +
  RLS on audit). **Tests: 46 green** (36 domain, 5 architecture, 5 application —
  saga end-to-end, idempotency, OFI-ignore on real DbContext + interceptors).
- **Package `deploy/NT.QAMS-webapi-v0.4-win-x64.zip`** smoke-tested (health 200,
  anonymous /api/audits → 401); deploy docs + Antigravity runbook updated.

## Increment v0.5 (2026-07-22) ✅ — Equipment & Calibration + Competency & Training

- **EquipmentItem aggregate:** registered as NeedsCalibration (first calibration
  activates) → Active → NeedsCalibration (due) → OutOfService (grace exhausted)
  → Active (recalibration); Retired terminal. Calibration records with certificate
  FileReference; maintenance log; serial + code unique per tenant (code from
  ref counter "EQP"). Events: CalibrationDue, EquipmentLockedOut,
  EquipmentReturnedToService, EquipmentRetired (all carry TenantId).
- **CompetencyRecord aggregate:** 80% pass gate (FR-GOV-02), SoD SOD-COMP-001
  (trainee ≠ assessor/authorizer), append-only assessment attempts, authorization
  sets validity expiry, expiry → PendingTraining (requalify), revocation terminal.
  TrainingAssignment root (manual assign/complete; DocumentPublished policy later).
- **ScheduledSweepService** (hourly hosted service): cross-tenant IgnoreQueryFilters
  read, but transitions ONLY via guarded aggregate methods (sweep proposes,
  aggregate decides). Covers calibration due, grace lockout, competency expiry.
  Idempotent; events flow through outbox with per-row tenants.
- Migration `ResourcesModules` (6 tables + RLS on 3 roots). InternalsVisibleTo
  added Infrastructure→Application.UnitTests for sweep testing.
- **Tests: 57 green** (46 domain incl. 11 new; 5 architecture; 6 application —
  sweep multi-run end-to-end: due→lockout across runs, cross-tenant, idempotent,
  outbox tenant stamping verified).
- **Package `deploy/NT.QAMS-webapi-v0.5-win-x64.zip`** smoke-tested (health 200;
  anonymous /api/equipment and /api/competencies → 401). Docs + runbook updated.

## Increment v0.6 (2026-07-22) ✅ — Risk & Governance + Supplier Quality

- **RiskItem:** explicit 1-5 L/I (no defaults — RSK-002), RPN, mitigation actions,
  residual assessment required before close (RSK-005) + all actions completed
  (RSK-006); residual RPN > 12 → HighResidualRisk event; closed = immutable.
- **ChangeRequest:** Proposed→Approved→Closed (+Rejected); the invariant:
  **approval requires a linked risk assessment** (CHG-012); closed immutable.
- **ManagementReview:** decisions while Scheduled; Close requires minutes;
  closed minutes immutable (MRV-004); ReviewClosed event.
- **Supplier:** PendingEvaluation→Approved⇄Suspended; SoD SOD-SUP-001
  (registrant ≠ approver); certificate registry; sweep proposal
  SuspendIfCertificateExpired (idempotent) — sweep now returns 4-tuple.
- **SupplierEvaluation:** separate accreting root; weighted total = score of
  record; criteria evidence as JSON; input validation (SUP-020..023).
- 4 controllers (risks, changes, management-reviews, suppliers incl. evaluations),
  refs RSK/CHG/MRV/SUP. Migration `GovernanceAndSuppliers` (8 tables + RLS on 5
  roots). **Tests: 66 green** (55 domain incl. 9 new; 5 architecture; 6 app).
- **Package `deploy/NT.QAMS-webapi-v0.6-win-x64.zip`** smoke-tested (health 200;
  anonymous /api/risks and /api/suppliers → 401). Docs + runbook updated.

## Increment v0.7 (2026-07-22) ✅ — Organization & Reference Data + Notifications engine

- **Organization:** Branch, Department (real FK → branch, RESTRICT), TestCatalogItem,
  LovEntry with the LocalizedText shared-kernel VO persisted as name_en/ar/fr
  columns per the DB architecture. Deactivate-never-delete. Unique per-tenant
  natural keys. Upsert semantics for LOVs.
- **Notifications:** NotificationRule (event key → roles → templates) +
  NotificationDispatch (the in-app feed IS the table; per-recipient email status).
  NotificationDispatcher: rule match → role-resolved recipients → {placeholder}
  template rendering → feed rows persisted FIRST, then best-effort email (a dead
  SMTP server never loses the record). Idempotent by SourceEventId.
- **Event wiring:** one policy class subscribes to 7 events (NcRaised,
  DocumentPublished, CalibrationDue, EquipmentLockedOut, CompetencyExpired,
  HighResidualRisk, SupplierSuspended); TenantProvisioned now seeds 7 default
  rules per tenant (provisioning saga's first extension).
- **Email:** IEmailSender port; SmtpEmailSender (env-configured Smtp__*) or
  LoggingEmailSender fallback when unconfigured — DI decides at startup.
- 5 controllers (branches, departments, test-catalog, lovs, notifications
  incl. /mine feed + /monitor). Migration `OrgAndNotifications` (6 tables + RLS).
- **Tests: 70 green** (4 new dispatcher tests: rule/role/template end-to-end,
  SourceEventId idempotency, email-failure resilience, unmatched-key no-op).
- **Package `deploy/NT.QAMS-webapi-v0.7-win-x64.zip`** smoke-tested (health 200;
  anonymous /api/lovs and /api/notifications/mine → 401).
- Deferred to a later increment: SLA definitions + escalation timers + work-task
  queue (the ladder engine), SignalR push channel for the feed.

## Increment v0.8 (2026-07-22) ✅ — Analytical Quality (Validation + QC/Westgard + PT)

- **WestgardEvaluator** (pure domain service): 1-3s/2-2s/R-4s/10-x reject rules,
  1-2s warning; z-score based; exhaustively tested (9 rule tests incl. same-side
  vs opposite-side, mean-crossing, zero-SD guard).
- **QcProfile** (target mean/SD, effective-dated) + **QcRun** (verdict computed
  once at entry via evaluator over a 12-run window; stored as fact; out-of-control
  raises QcOutOfControl; troubleshooting note gate).
- **ValidationStudy:** ProtocolConfigured→DataEntered→StatsCalculated→SignedOff
  (locked); immutable replicates (reopen voids results); real CV%/mean-bias
  computation judged vs TEa; sign-off event.
- **PtEnrollment:** z = (submitted-assigned)/SD; |z|≤2 satisfactory / 2<|z|<3
  questionable / ≥3 unsatisfactory; **PtUnsatisfactory→NC saga** (source=PT,
  SourceRef "PT:{ref}", idempotent) — 4th auto-NC source alongside audit findings.
- 3 controllers (qc, validation-studies, proficiency-tests). Migration
  `AnalyticalQuality` (5 tables + RLS on 4 roots; qc_run indexed for LJ window).
- **Tests: 93 green** (76 domain incl. 21 new; 5 architecture; 12 app incl. PT→NC
  saga + idempotency).
- **Package `deploy/NT.QAMS-webapi-v0.8-win-x64.zip`** smoke-tested (health 200;
  anonymous /api/qc, /api/validation-studies, /api/proficiency-tests → 401).

## Increment v0.9 (2026-07-22) ✅ — Records & Retention + SLA/Escalation + Tasks

- **ArchiveEntry:** retention classes (5yr/10yr/Permanent) with derived expiry;
  archive-once guard; retrieve/return; disposal authorized only after expiry;
  permanent never disposable; RecordDisposed event.
- **SlaDefinition** (module+severity → target hours, upsert). **WorkTask**
  (assignee user OR role; My-Tasks query = mine + my-role; overdue derived).
- **EscalationTimer:** armed on CapaActionPlanned (deadline = action due date),
  cancelled on CapaActionCompleted; ladder L1(+24h→owner)/L2(+48h→QM)/L3(+72h→QM)
  advanced by the sweep tick; each step raises EscalationTriggered →
  EscalationToTaskPolicy creates a WorkTask (idempotent by subjectRef#level) +
  NotificationEventPolicies sends the SLA_ESCALATED alert (default rule seeded).
- Added CapaActionCompleted event to NC aggregate. Sweep now returns/handles a
  4th concern (escalation) alongside calibration/lockout/competency/supplier.
- 3 controllers (archives, sla-definitions, tasks). Migration `RecordsAndSla`
  (4 tables + RLS). **Tests: 105 green** (86 domain incl. 13 new; 5 architecture;
  14 app incl. arm/cancel + sweep-tick escalation end-to-end).
- **Package `deploy/NT.QAMS-webapi-v0.9-win-x64.zip`** smoke-tested (health 200;
  anonymous /api/archives, /api/tasks/mine → 401).

**All 14 functional bounded contexts now built.** Remaining: compliance-hardening
(Compliance Ledger tamper-evident tables, MFA/TOTP, privilege matrix, e-signature
PINs), SignalR push, Angular frontend.

## Increment v1.0 (2026-07-22) ✅ — Compliance & security hardening (21 CFR Part 11)

- **Account lockout (FR-AUTH-02):** UserAccount tracks failed attempts; 5 → 30-min
  lock, UserLockedOut event; login records success/reset.
- **MFA/TOTP (FR-AUTH-01):** hand-rolled RFC 6238 TotpService (HMAC-SHA1, ±1 step
  skew, Base32, otpauth URI). Enroll → confirm → enforced at login (MfaRequired
  response step). Per-user secret; MfaEnabled flips on confirm.
- **Hash-chained audit trail:** AuditTrailAppender chains every processed outbox
  event per tenant (SHA-256 of prev-hash‖seq‖event); ComplianceLedgerStore
  verifies the chain and pinpoints the first break. Wired into OutboxProcessor.
- **Security-event log:** every login/lockout/MFA outcome written to audit.security_event.
- **E-signatures:** per-user 4-digit PIN (PBKDF2-hashed), ESignatureService verifies
  + mints immutable SignatureRecord (signer, meaning, subjectRef, content hash).
  Document-publish wired as the signing ceremony (content hash = published file's
  SHA-256); pattern documented for the other signing points.
- **DB-level tamper protection:** audit.* ledgers get RLS + append-only guard
  triggers (reject UPDATE/DELETE). Migration `ComplianceAndAuth` (3 audit tables +
  user columns + triggers).
- ComplianceController (audit-trail, signatures, security-events, chain-verification;
  QM/Admin/Auditor only). AuthController gains mfa/enroll, mfa/confirm, signature-pin.
- **Tests: 114 green** (90 domain incl. lockout/MFA state; 5 architecture; 19 app
  incl. hash-chain verify + tamper detection, e-signature PIN, TOTP contract).
- **Package `deploy/NT.QAMS-webapi-v1.0-win-x64.zip`** smoke-tested (health 200;
  anonymous /api/compliance → 401).

**Backend feature-complete for the specified scope.** Remaining: fine-grained
privilege matrix (role gates in place today), e-sign ceremonies on remaining
signing points, SignalR push, Angular frontend.

## v1.0 release consolidation (2026-07-22) ✅

- Release-config build clean (0 warnings) + all 114 tests green as the release checkpoint.
- Solution-level `README.md` (architecture, module map, build/run/deploy, increment history).
- `scripts/verify-e2e.ps1` — automates the DEPLOY.md happy path against a live
  instance (login → provision → NC → file upload → signed document publish →
  audit trail → hash-chain verification), pass/fail per step, exit code.
- **Git repository initialized**; two commits; build/publish artifacts excluded
  (`.gitignore` covers deploy/publish-win-x64 + zips + data/); `.gitattributes`
  normalizes line endings. 184 source files tracked. Local only — not pushed.

## Frontend v1 (2026-07-22) ✅ — Angular 18 foundation + auth/shell/NC slice

- Node 20.18.1 installed user-locally (`%LOCALAPPDATA%\nodejs-portable`). Angular
  18 standalone app scaffolded by hand (package.json, angular.json, tsconfig,
  application builder). `npm install` (852 pkgs) + `ng build` (strict templates)
  both green; ~84 kB initial transfer, per-feature lazy chunks.
- **Foundation:** app.config (router + HttpClient + interceptor), AppComponent
  syncs document dir/lang, AuthService (signals, session persistence, login/MFA/
  PIN flows), authInterceptor (bearer + 401→login), authGuard, I18nService
  (EN/AR/FR dictionary + RTL), QamsApiService typed client.
- **Slice:** login (with the MFA-required step revealing the code field), shell
  (navy sidebar, language switcher, sign-out), dashboard (live KPI cards),
  NC list (raise + submit + list), notifications feed (mark-read).
- **Verified in a real browser** (ng serve :4210 + browser pane): login form
  renders with all fields; selecting Arabic flips document.dir→rtl, lang→ar, and
  translates labels (تسجيل الدخول / معرّف المختبر).
- `frontend/README.md` documents run/deploy + the remaining ~25 module screens
  (same list+form shape as the NC feature). Frontend node_modules/dist gitignored.

## Backend Phase 1 items still open (non-blocking)

Per architecture §11: JWT + refresh + MFA(TOTP), UserAccount/Role/Privilege aggregates,
privilege catalog + authorization behavior, session registry, provisioning saga
(TenantProvisioned → seed admin + canonical roles), security_event capture,
cross-tenant denial functional test suite (merge gate from day one).

## EA Remediation Phase 0 (v1.38.0, 2026-07-28) ✅ — deployment safety gates

- **TENANT-004 — least-privilege role guard.** `DatabaseRoleGuard` (Infrastructure/
  Security) inspects the connection role at startup: SUPERUSER, BYPASSRLS, or
  ownership of application tables (qams/audit/read/saas/ref, `pg_has_role`-aware).
  Production **refuses to boot** on any violation (message carries the
  harden-runtime-role.sql remediation); other environments log warnings.
  `db-init.sql` rewritten to the two-role model (qams_owner DDL / qams_app DML);
  DEPLOY.md documents migrate-as-owner + harden + connect-as-app. CI gate: with
  `QMS_ITEST_POSTGRES` set the RLS suite hard-fails instead of skipping when the
  DB is unreachable or the role is over-privileged (RealPostgresFixture +
  RuntimeRolePrivilegeTests — includes the boot-as-owner-rejected test).
- **OPS-008 — DB-backed readiness.** `PostgresReadinessHealthCheck`
  (Infrastructure/Health, 5s-capped probe): `/health/ready` = PostgreSQL
  answering (503 otherwise); `/health/live` + legacy `/health` = process-only.
  Dockerfile HEALTHCHECK and verify-e2e.ps1 repointed at readiness. Functional
  tests prove live=200/ready=503 with the DB down; integration tests prove
  Healthy/Unhealthy against real PG.
- **OPS-002 — single-replica topology (ADR-0001).** Supported topology is one
  API instance per database until Phase 1 (outbox SKIP LOCKED + leader election).
  `SingleReplicaGuardService` holds session advisory lock 0x4E54514D5301 for the
  process lifetime; a second instance logs a prominent scale-out warning and
  re-probes. docs/adr/ADR-0001-single-replica-topology.md + DEPLOY.md note.
- Suite: **279 tests green, 0 skipped** (211 domain + 35 application + 5 arch +
  10 integration + 18 functional); build 0 warnings. Verified live: role-guard
  warning fires in dev (qams_app owns tables), lock acquired, all 3 health
  endpoints 200.

## EA Remediation Phase 1 (v1.39.0, 2026-07-28) ✅ — data consistency & messaging robustness

- **DB-009/VAL-003 — optimistic concurrency.** PostgreSQL `xmin` mapped as the
  concurrency token on EVERY aggregate root by convention in AppDbContext
  (Npgsql-only; zero schema change — scaffolded AddColumn ops removed by hand
  per Npgsql docs since xmin is a system column). DbUpdateConcurrencyException
  → HTTP 409 + stable code `CONCURRENCY-409` in DomainExceptionHandler.
  Proven: two racing edits on one committed row → exactly one wins (integration)
  + handler mapping test.
- **MSG-004/005 — dead-letter + backoff.** outbox_event gains
  `next_attempt_at_utc` (exponential backoff 5s·2^n with ≤25% jitter),
  `dead_lettered_at_utc` (set at MaxAttempts=5, ERROR alert log names the row
  for triage, filtered index for the triage query). Per-row try/catch + due
  filter ⇒ a poison event never head-of-line-blocks healthy ones (proven with
  an unresolvable-type poison row racing a real PtUnsatisfactory event).
- **MSG-006 — redelivery idempotency.** Policies already dedupe by natural key;
  now DB-ENFORCED: unique partial index `ux_nonconformance_source`
  (tenant_id, source_ref WHERE NOT NULL). Crash-before-mark redelivery proven
  to net exactly one NC through the real MediatR pipeline.
- **MSG-007 — retention purge.** Hourly `ExecuteDelete` of processed rows older
  than `Outbox:RetentionDays` (default 30, validated at startup); the ledger
  keeps the history. harden-runtime-role.sql grants qams_app DELETE on
  qams.outbox_event ONLY (outbox is transport, not record). Purge proven to
  spare recent-processed and unprocessed rows.
- **OPS-002 (durable) — scale-out safety.** Outbox claim = CTE with
  `FOR UPDATE SKIP LOCKED` + 2-min lease (`claimed_until_utc`): two concurrent
  claimants proven disjoint; leases proven to expire and release. Sweeps
  (ScheduledSweepService, KpiSnapshotService) wrap in `pg_try_advisory_xact_lock`
  leader election (AdvisoryLock + AdvisoryLockKeys registry; sentinel key moved
  there too). InMemory unit-test path unaffected (locks are Npgsql-only).
- Migration `Phase1OutboxResilienceAndConcurrency` applied. Suite: **290 tests
  green, 0 skipped** (was 279); build 0 warnings. Live: /health/ready 200,
  login 200, xmin visible in EF SQL, singleton lock acquired.

## EA Remediation Phase 2 (v1.40.0, 2026-07-28) ✅ — observability baseline

- **OBS-001 — structured logging.** Production: JSON console (built-in
  formatter, UTC, scopes). ObservabilityMiddleware (first in pipeline) opens a
  scope (Service/Environment/CorrelationId/TraceId) and emits ONE canonical
  completion record per request with Service, Environment, Method, Path,
  Operation, Status, Outcome (success/client-error/server-error), DurationMs,
  TenantId, UserId, CorrelationId — shape locked by a structured-state test.
- **OBS-002/API-006 — tracing + correlation.** OpenTelemetry: ASP.NET Core
  server spans (health/metrics filtered), Npgsql command spans, MediatR
  request spans (TracingBehavior, BCL ActivitySource — Application stays
  vendor-free), outbox delivery + job spans (QamsDiagnostics). The async
  boundary keeps the trace: OutboxInterceptor persists the writing W3C
  traceparent (`outbox_event.trace_parent`, migration Phase2OutboxTraceParent)
  and the processor parents the delivery span on it — acceptance test proves
  HTTP→EF→Outbox share one trace id. X-Correlation-Id accepted (sanitized: the
  reflected-XSS case is tested), echoed on every response; traceId +
  correlationId stamped into ALL ProblemDetails (handler + framework).
  OTLP export for traces/metrics/logs when `Otlp:Endpoint` is set.
- **OBS-003 — metrics + alerts.** /metrics (Prometheus, anonymous) publishes
  ASP.NET RED, Npgsql pool, and NT.QAMS instruments: outbox
  processed/failed/dead_lettered counters + backlog / dead_letters /
  oldest_pending_age_seconds gauges (30s stats poll in the processor) +
  job.last_success_timestamp_seconds{job} liveness for compliance-sweep and
  kpi-snapshot. deploy/OBSERVABILITY.md defines the actionable alert set
  (error-rate, p95, dead-letter>0, backlog age, sweep/snapshot liveness) and
  aligns log retention with the DR runbook (≥35 days; ledger remains the
  7-year compliance record).
- Packages (WebApi only): OpenTelemetry 1.17 (hosting/aspnetcore/otlp),
  Npgsql.OpenTelemetry 9.0.4, Prometheus exporter 1.17.0-beta.1 (endpoint).
- Suite: **297 tests green, 0 skipped** (was 290); build 0 warnings. Live:
  correlation echo ✓, 401 problem carries traceId+correlationId ✓, /metrics
  serves RED + qams gauges ✓, completion logs carry all fields ✓.

## EA Remediation Phase 3 (v1.41.0, 2026-07-28) ✅ — edge & security hardening

- **SEC-013/API-002 — rate limiting.** Built-in AddRateLimiter: global
  per-client fixed window (default 300/min) + strict policies on the
  credential surface (`[EnableRateLimiting("auth")]` on AuthController,
  default 10/min per client) and the password+PIN e-signature ceremony
  (documents publish — per ACTOR, default 10/min, so a PIN can't be
  brute-forced inside a valid session). 429 + Retry-After; health/metrics
  endpoints DisableRateLimiting (throttled probes = broken monitoring).
  Typed RateLimitSettings (RateLimit:*), resolved via DI at options-build time
  so tests swap the singleton cleanly. Proven: burst → first N pass, rest 429
  (functional + live: 10×401 then 429, Retry-After 60); probes never throttled.
- **SEC-011 — security headers.** SecurityHeadersMiddleware on EVERY response:
  API CSP `default-src 'none'; frame-ancestors 'none'; base-uri 'none';
  form-action 'none'` (no script-src grant ⇒ inline script blocked by
  definition), nosniff, X-Frame-Options DENY, Referrer-Policy no-referrer,
  HSTS 2y+subdomains outside Development. SPA host headers added to
  deploy/web.config (script-src 'self' — Angular AOT, no inline/eval) +
  /metrics added to the proxy rule. Header set asserted on success AND error
  responses.
- **SEC-012 — TLS/HSTS decision (ADR-0002).** TLS terminates at the reverse
  proxy (certificates/redirects/protocol policy); the app emits HSTS itself so
  the commitment can't be dropped in proxy config; UseForwardedHeaders
  (loopback-trusted) first in the pipeline so the real client IP feeds the
  rate-limit partitions and logs. No in-app UseHttpsRedirection (loopback
  proxy model). Go-live checklist in the ADR.
- **SEC-017 — token storage (ADR-0003, risk acceptance).** Web-storage tokens
  stay for this train, compensated by: strict SPA CSP (the actual anti-XSS
  control), access-token default lifetime **halved 120→60 min**
  (Jwt:ExpiryMinutes), existing server-side session revocation (F-06), MFA +
  lockout + the new auth rate limit. Residual risk + revisit trigger (refresh
  cookie flow) captured in the ADR.
- Suite: **301 tests green, 0 skipped** (was 297); build 0 warnings. Live:
  header set on every response ✓, 12-burst → 10×401 + 2×429 + Retry-After ✓,
  probes exempt ✓.
- **Phases 0–3 complete → every production blocker from the EA audit is
  cleared; the release condition is now unconditional.**

## EA Remediation Phase 4 (v1.42.0, 2026-07-28) ✅ — API & application-pipeline polish

- **API-003 — one error contract.** ProblemResponse is the single writer:
  RFC 7807 + application/problem+json + stable `code` + traceId/correlationId
  on EVERY path — ActiveSession/ChangeReason/MfaGate middlewares dropped their
  anonymous-object shapes; DomainExceptionHandler (incl. the CONCURRENCY-409)
  routes through it. Contract locked by ProblemContractTests.
- **API-005 — file hardening.** FileContentPolicy: evidence extension
  allow-list + magic-byte sniffing (renamed executables fail), text formats
  refuse binary, and the STORED content type is the canonical sniffed one —
  the client's claim is never trusted. 422 FILE-415 on refusal; downloads
  remain Content-Disposition: attachment. Proven over the real pipeline.
- **API-001 — versioning (ADR-0004).** Asp.Versioning.Mvc 8.1: one central
  VersionedRouteConvention adds api/v1/... beside every literal api/... route
  (41 controllers untouched, implicitly v1.0); legacy paths serve the default
  version; supported versions reported; unsupported versions refused.
  Contract-evolution policy in ADR-0004.
- **CQRS-003 — deny-by-default command authorization.** Every one of the 211
  commands carries exactly one policy marker: [RequireInternalActor] (write
  default — the read-only ExternalAuditor can no longer invoke ungated write
  commands), [RequireAuthenticatedActor] (self-service MFA/PIN),
  [AllowUnauthenticated] (login/password rotation), [RequireRole] (tenant
  provisioning → PlatformAdmin; e-sign publish → QM/TenantAdmin).
  AuthorizationBehavior fails closed (AUTHZ-000/001/002); ICurrentUser now
  carries the token role; an architecture test makes a missing policy a CI
  failure.
- **CQRS-004 — Idempotency-Key replay protection.** Opt-in per request: first
  execution's response stored per (actor, key, command type) in
  qams.idempotency_record (24h retention, purged in the outbox cycle, DELETE
  grant scoped); a retry replays the stored response — the double-submit nets
  exactly one NC (proven end-to-end).
- **API-004 — pagination envelope.** All 14 silently-capped list queries
  (13× Take(500) + the notifications feed's Take(200)) now return
  PagedResponse{items,total,page,pageSize,hasMore} with clamped page size
  (max 200) and stable ordering; controllers accept page/pageSize. The
  Part 11 NC-register export walks EVERY page (no truncation). Frontend:
  Paged<T> model, 13 api-service methods, 12 facades unwrap + expose
  total/hasMore signals (pager UI is a UX follow-up — the contract and
  plumbing are done). Boundary/navigation/clamp functional tests.
- Migration Phase4IdempotencyRecords. Suite: **330 backend green, 0 skipped**
  (was 301) + 37 frontend unit + 3 Playwright e2e (live against the running
  API). Live: envelope on legacy + api/v1 routes with true totals ✓.

## EA Remediation Phase 5 (v1.43.0, 2026-07-28) - DB integrity, configuration & container [DONE]

- DB-005 - CHECK constraints (migration Phase5CheckConstraints): nonconformance
  severity/likelihood 1-5 + rpn 1-25 + status IN NcStatus domain; risk_item
  scores 1-5 / rpn 1-25 incl. residuals; equipment interval > 0, grace >= 0;
  supplier_evaluation weighted_total >= 0; date-ordering on work_task /
  training_assignment / audit (completion never precedes creation). Proven:
  a domain-valid NC cannot be corrupted by direct SQL (23514, savepoint-proof
  per probe); reversible Down().
- CFG-001/002 - fail-fast config: ConfigGuard (ReadInt/Bool/Decimal) throws on
  PRESENT-but-invalid values, names the key; swapped into PasswordPolicy,
  Security:RequireMfa, Westgard, Outbox retention, Jwt:ExpiryMinutes,
  RateLimit:* (silent TryParse defaults eliminated). Jwt:Secret + connection
  string already failed fast. ConfigGuardTests lock the contract.
- OPS-009 - Npgsql EnableRetryOnFailure(5, 10s) + CommandTimeout(30) on the DI
  context; AdvisoryLock wraps its user-initiated transaction in the execution
  strategy (retry granularity = whole locked unit). SMTP resilience deferred
  by design: LoggingEmailSender/SmtpEmailSender is best-effort after in-app
  delivery persists (delivery monitor is the record).
- DEPLOY-002/003 - Dockerfile runs as the aspnet image's unprivileged app user
  (USER $APP_UID) with a chowned /app/data/files volume mount + canonical
  FileStorage__RootPath; deploy/compose.production.yml = reference manifest
  (replicas 1 per ADR-0001, CPU/memory limits, read_only rootfs + tmpfs,
  loopback-only publish behind the TLS proxy, secrets from env, readiness
  healthcheck). NOTE (honest gap): no Docker on this dev machine - the image
  was NOT built/run here; manifest verified by review only, first CI/host
  build must confirm.
- Suite: 337 tests green, 0 skipped (was 330); build 0 warnings.

## EA Remediation Phase 6 (v1.44.0, 2026-07-28) - Test coverage & governance [DONE]

- TEST-001/002/003 - remaining gaps closed: migration UP/DOWN round-trip smoke
  (last migration reverts + reapplies against real PG); mid-chain audit-tamper
  detection (3-entry chain, trigger-disabled insider edit of sequence 2 ->
  VerifyChain reports broken AT 2; timestamps whole-second because PG stores
  microseconds and production only ever hashes DB-read values);
  scripts/perf-smoke.ps1 baseline tripwire - live numbers on this dev box:
  /health/ready p95 20.6ms, login p95 69.6ms (hash-bound), paged NC list p95
  6.3ms (threshold 800ms). ProblemDetails/concurrency/outbox/dedup tests
  already existed from Phases 1-4.
- ARCH-004 - ModuleBoundaryTests: 18 domain modules, each proven to reference
  NO other module's types (NetArchTest, per-module theory) - the modular
  monolith boundary is now a merge gate. Zero violations found.
- ARCH-005 - ApiSurfaceSnapshotTests: 620-line route+method baseline
  (ApiSurface.approved.txt) from the OpenAPI document; unreviewed surface
  drift fails CI; intentional changes update the snapshot in the same commit
  (ADR-0004 policy).
- ARCH-006 - ADR set complete: ADR-0005 xmin concurrency, ADR-0006 outbox
  reliability model, ADR-0007 same-origin/no-CORS, ADR-0008 EF DbSet
  persistence port (0001-0004 shipped in earlier phases).
- SEC-003 - confirmed: analytical reading/study deletes are gated by
  [RequireInternalActor] + X-Change-Reason + field-change ledger + signed-
  record immutability (pre-lock deletion by internal lab roles is the intended
  workflow). Hardened the HTTP layer: AUTHZ-* codes now map to 403 (not 422),
  and ProblemAuthorizationResultHandler gives role-gate 403s and credential
  401s the same problem+json body as every other error (they were BARE status
  codes - an API-003 gap found by the new test). AuditorDenyMatrixTests: the
  ExternalAuditor reads registers but every flagged write (raise NC, configure
  screening, create document) is 403 + AUTHZ-*.
- UI-008/014 - authorization-matrix valueChanges now takeUntilDestroyed (only
  unmanaged subscription in the SPA); Part-11 change-reason window.prompt
  replaced by an accessible dialog (role=dialog, aria-modal, labelled, focus
  to textarea, Escape/cancel, confirm disabled when blank, EN/AR/FR keys)
  driven by ChangeReasonService; async interceptor attaches the header or
  silently aborts on cancel; records legal-hold uses the same dialog.
  +8 frontend specs (45 total) incl. 7 dialog a11y specs.
- Gates: backend 359 green 0 skipped (was 337) - 211 domain / 57 app / 24 arch
  / 18 integration / 49 functional; frontend build + 45 unit specs; 3 e2e
  against the live API; perf smoke PASS. Build 0 warnings.

## Post-remediation backlog #1 (v1.45.0, 2026-07-28) [DONE]

- R-1 (container verification): ci.yml gains a `container` job - builds the
  hardened image on every push, asserts the runtime uid is non-root and the
  evidence volume is writable by the app user. Proven by the Actions run for
  this push (no Docker on dev machines).
- R-3 (pager UI): shared accessible qams-load-more footer (showing X of Y,
  aria-live, load-more button) on all 13 paged lists; services take
  page/pageSize; facades track pages, append on loadMore, reset on
  filter/reload, guard concurrent loads; notifications feed paged in-component.
  i18n common.showingOf/loadMore (+confirm/cancel) EN/AR/FR.
- R-4 (reset-password prompt): generic accessible text-prompt dialog
  (titleKey/labelKey/inputType incl. password masking; same a11y contract as
  the change-reason dialog) hosted in shell; users.component drops
  window.prompt.
- Gates: backend 359 green 0 skipped; frontend build + 58 specs (+13); 3 e2e
  vs live API. Closure report residual register updated (R-3/R-4 closed,
  R-1 CI-enforced).

## Road-to-100 Phase 7 (v1.46.0, 2026-07-28) — session-security completion (retires R-2/ADR-0003) [DONE]

- ADR-0009 (supersedes ADR-0003): access token → SPA MEMORY only, default
  lifetime 15 min; session continuity via a rotating, httpOnly Secure
  SameSite=Strict refresh cookie (qams_rt, Path=/api/auth).
- Backend: RefreshSession aggregate + qams.refresh_session (migration
  Phase7RefreshSessions; only SHA-256 of the secret stored). Commands:
  RefreshTokenCommand (rotate + reuse-detection → revoke whole family +
  REFRESH_REUSE_DETECTED event), LogoutCommand (revoke family). LoginHandler
  starts a family on full sign-in (not on the MFA/enrollment interstitial).
  AuthController: /refresh + /logout, hardened cookie set server-side, token
  never in the body's cookie. Refresh rate-limit policy (60/min per client).
  Retention purge extended to dead refresh sessions; DELETE grant added.
- Frontend: auth.service holds the token in memory only (web storage removed);
  single-flight refresh(); APP_INITIALIZER hydrate() → reload keeps the
  session; auth interceptor does one silent refresh + retry on 401, login only
  if that fails; logout revokes server-side.
- Tests: +5 functional (RefreshSessionTests: cookie flags, rotation, reuse→
  family revocation, logout, no-cookie 401), +9 frontend (auth.service 5 incl.
  "never in web storage" + single-flight; auth.interceptor 4). Hardened two
  pre-existing dialog specs (fixture.destroy in afterEach) to kill a
  random-order focus flake. API-surface snapshot re-approved (+/refresh,
  +/logout on legacy and v1).
- Gates: backend 365 green 0 skipped; frontend build + 67 specs; 3 e2e vs live
  API; build 0 warnings. Live: cookie flags httponly+secure+samesite=strict+
  path=/api/auth; rotation issues a new cookie; replaying a rotated token → 401
  and the whole family revoked. (Live refresh over plain-HTTP localhost needs a
  manual non-secure cookie because the Secure flag blocks the jar — production
  is HTTPS; functional tests are the authoritative proof.)

## Road-to-100 Phase 8 (v1.47.0, 2026-07-28) - evidence at scale [PARTIAL - see residuals]

- Load harness (tests/NT.QAMS.LoadTests, BCL-only concurrent generator, kept
  out of the solution). Ran live vs the running API, 50 users x 30s, read mix:
  p95 86-105ms, p99 95-179ms, ~750-800 rps/scenario, 0.00% errors -> PASS
  (docs/reference/NT_QMS_Load_Test_Report.md). Finding: the global rate limit
  (300/min per IP) is an ABUSE ceiling, not a concurrency ceiling - measured
  capacity needs it raised to a load-test profile; production must size it to
  expected peak legitimate concurrency (labs behind shared NAT).
- Failure drills (scripts/failure-drills.ps1) run live: Drill 2 poison outbox
  event -> dead-lettered at MaxAttempts within a couple of poll cycles,
  end-to-end against the running processor (injected pre-aged, auto-cleaned).
  Drill 1 (stop PG -> readiness 503 -> recover) gracefully SKIPPED without an
  elevated shell; that behaviour stays proven by ReadinessAndTopologyTests +
  HealthEndpointTests.
- Observability stack (deploy/observability/): compose (otel-collector +
  Prometheus + Grafana), OTLP->Prometheus collector config, alert.rules.yml
  (the OBSERVABILITY.md set as PromQL: dead-letter, backlog age, sweep/snapshot
  liveness, 5xx rate, p95 latency), provisioned Grafana datasource + RED/outbox/
  job-liveness/pool dashboard. Metric names verified live against /metrics.
  NOT run here (no Docker) -> new residual R-7 (host bring-up + drill-fires-alert
  confirmation).
- Backend suite unchanged: 364 green, 0 skipped. Residual R-5 (perf on a
  prod-like host, incl. 24h soak) partially addressed by the committed harness +
  dev-box baseline; the authoritative staging run remains external.

## Road-to-100 Phase 9 (v1.48.0, 2026-07-28) - assurance depth [DONE]

- Role x endpoint deny matrix (RoleEndpointMatrixTests): all 6 roles driven
  against the distinct role-gates; two invariants proven for every cell - never
  401/5xx (authenticated callers), and every 403 is problem+json with an AUTH*
  code (no bare status, no silent leakage). Plus explicit deny assertions
  (auditor/analyst/dept-head off the admin + platform surface).
- Contract coverage (ContractCoverageTests): 13 list endpoints return the
  API-004 envelope on BOTH legacy and api/v1 routes; 5 by-id reads of a missing
  resource return problem+json 404 with a stable *-404 code + traceId.
- Frontend a11y: @axe-core/playwright scans of the platform + tenant login
  pages (zero serious/critical), wired into the always-on CI frontend job.
  The scan found and we FIXED real violations: a critical missing label
  association (4 inputs) + 3 serious color-contrast failures on the login
  screen (local color overrides; shared tokens untouched). e2e: load-more
  pagination journey added; auditor journey documented-skipped (no seeded
  auditor login). Fixed a pre-existing i18n spec order-flake (localStorage
  leak) via afterEach cleanup.
- Gates: backend 370 green 0 skipped (211 domain / 57 app / 24 arch / 18
  integration / 58 functional); frontend build + 67 unit specs; 6 e2e vs live
  API (incl. 2 axe scans + pagination journey). Build 0 warnings.
- Engineering ceiling reached (~98%). Remaining to 100% is the EXTERNAL track
  only: penetration test (staging, after the v1.46 auth model), CSV
  re-validation (R-6), and the staging telemetry/soak confirmation (R-5/R-7).

## Role Privilege module (2026-07-31) - configurable roles, privileges, working scope [DONE - awaiting review]

Replaces role-name authorization with tenant-configurable privileges end to end.

- Domain: `PermissionCatalog` (31 modules x 8-action closed set = 170 keys,
  code-defined so a grant always maps to a real code path; unknown keys rejected
  ROLE-005) + `Role` aggregate (tenant-scoped, system-role rename/deactivate
  protection, permission changes evented with grants/revocations + reason ->
  hash-chained audit trail). `UserAccount` extended: `RoleId`, `PreferredLanguage`,
  owned `user_branch_access`/`user_department_access` scope (empty = unrestricted,
  widening evented explicitly).
- Enforcement (three layers, deny-by-default):
  1. HTTP: `[RequirePermission(module, action)]` filter replaced ALL 127
     tenant `[Authorize(Roles=...)]` gates (mapped endpoint-by-endpoint; the
     platform-admin gate on /api/tenants stays tier-based by design). 403s keep
     the AUTHZ-403 problem+json contract.
  2. Command pipeline: `[RequirePermissionPolicy(module, action)]` policy in
     AuthorizationBehavior (unknown key -> loud AUTHZ-008); document publish +
     the role/user admin commands converted; RequireInternalActor retained as
     tier defense-in-depth beneath the permission gates.
  3. Data: composed EF global filter (tenant AND working scope) on all 12
     IAllocatable aggregates - a branch-restricted user cannot LOAD another
     branch's records (edits/approvals die as 404) - plus OrgScopeGuardInterceptor
     refusing out-of-scope creates in-transaction (SCOPE-001/002).
- Privileges resolve from the DB on every authenticated request (ActiveSession
  middleware) - revocation bites on the next request, not at token expiry.
  Deliberately uncached; the resolver is 2 indexed reads.
- Seeding: 5 system roles reproduce the fixed tiers (explicit per-module table
  derived from the old gate matrix; parity pins in tests). Tenant provisioning
  seeds them + puts the first admin on Tenant Administrator; startup backfill
  (idempotent) upgrades existing tenants/users. Tier-based register/change-role
  APIs keep working (default onto the equivalent seeded role).
- Lockout guard ROLE-006: no edit/deactivation/reassignment may leave the tenant
  without an active user holding roles.manage.
- API: /api/roles (catalog, list, detail, create, update, permissions,
  de/reactivate) + /api/users assigned-role|scope|language + /api/auth
  me/privileges|me/language. API surface snapshot re-approved (626 -> 652).
- Migration `RolePrivilegeModule`: role (FORCE RLS + tenant_isolation policy,
  verified relforcerowsecurity=t), role_permission, user_branch_access,
  user_department_access (owned-table precedent), user_account.role_id/
  preferred_language. Applied to dev.
- SPA: PermissionsService rewritten over permission keys fetched from
  me/privileges (deny until loaded); ALL 62 consumer components converted from
  the 5 coarse role signals to module.action checks (110 call sites, mapped per
  action semantics); Roles & Privileges screen (grouped matrix editor, reason
  prompt on grant changes, i18n en/ar/fr incl. 31 module names); Users screen
  gained role assignment, branch/department scope drawer, per-user language;
  language switcher persists the signed-in user's choice server-side.
- Tests: 417 backend green (was 412) incl. new RolePrivilegeFlowTests proving
  over HTTP: seeded roles at provisioning, grant flip 403->allowed on the NEXT
  request, ROLE-005/ROLE-006 refusals, and the scope filter hiding branch B
  from a branch-A user on reads AND refusing writes (SCOPE-001) while the
  admin still sees everything. 76 frontend specs green (PermissionsService
  spec rewritten). Browser-verified on demo-lab: 5 roles listed with member
  counts, QM matrix shows exactly 164/170 grants, users table shows backfilled
  roles, console clean.
- Honest deltas (8-action granularity vs old per-endpoint nuance), all widen
  only within a module a role already worked in: DeptHead may archive an
  interested party (shares org-context.void with close-issue); QM gains branch/
  dept/test deactivation ONLY IF granted organization.manage (seeded QM does
  NOT have it). Document acknowledgement coverage listing follows documents.view
  (was QmDeptAdmin-visible). UserRole enum retained solely as the structural
  platform/tenant tier + legacy JWT claim - no tenant authorization decision
  reads it anymore.
- NOT done yet (needs owner direction): formal OQ execution/RTM rows for the
  module; e2e Playwright scenario; per-branch RLS at the DB layer (app-layer
  filter + guard only); removing the now-unused Roles.cs group constants
  (kept while functional tests reference tier logins).

## Role Privilege module OQ execution + defect RP-D1 (2026-07-31) [EXECUTED - awaiting witness signature]

- OQ executed live against dev (doc 12, OQ-EXEC-NTQMS-002): 10 cases / 30 checks
  on a dedicated tenant (oq-roles-103114) incl. a 25-cell seeded-role deny
  matrix. 29/30 first-pass; URS-095..099 registered with trace.
- DEFECT RP-D1 (found by OQ-RP-09): UserAccount events (UserRoleAssigned,
  UserScopeChanged, and pre-existing UserLockedOut) landed in the audit ledger
  with tenant_id = empty -> invisible to the tenant's own compliance view,
  because the outbox drain stamped tenant only from ITenantScoped and
  UserAccount deliberately isn't. Fix: IOptionallyTenantScoped (SharedKernel)
  on UserAccount + outbox fallback. Pinned by UserEventTenantStampTests (2);
  OQ-RP-09 re-executed to Pass (events at ledger sequences 14/15 with the
  correct tenant). Historical rows keep the empty tenant (append-only ledger);
  QA to disposition. 419 backend tests green post-fix.
- OBS-1: GovernanceTests' migration round-trip on the shared dev DB drops/
  recreates the newest migration's tables (roles vanished mid-session; SPA
  403s). Startup backfill self-healed on next boot (90 roles / 18 tenants,
  0 unassigned) - by design. Ops note: restart the API after running the
  integration suite on dev; custom dev-only roles/scopes are lost by that test.

## Database schema hardening — CSV doc set updated (2026-08-01) [ENGINEERING-VERIFIED, OQ NOT EXECUTED]

- REVAL-NTQMS-001 (doc 06) -> rev 5, scope extended to v1.51.2. New Part A section A.10
  registers URS-100..107 for the 6-migration hardening programme, with the two defects the
  programme's own live verification found (SH-D1 sign-in broken by security_event RLS;
  SH-D2 tenant provisioning broken by fk_outbox_event_tenant vs EF insert ordering), both
  fixed and re-proven. Also: IQ-26..30 (migrations applied, tenant fence intact, guard
  triggers survived, identifier limit, deployment script current), OQ-DB-01..08, the
  schema-hardening test suites added to the evidence-engine table, a VSR-addendum row, and
  two Part F checklist items.
- RTM-NTQMS-001 (doc 04) -> v1.2. Three baseline traces revised where the hardening changed
  them: URS-008 (tenant fence now 90 FORCE-RLS tables incl. 30 owned children, cross-tenant
  children structurally impossible, 2 documented permanent exceptions guarded by an
  architecture test), URS-011 (field-change tenant attribution corrected; entity_id identity
  preserved after composite keys), URS-012 (audit ledger re-keyed tenant-first, hash columns
  format-constrained, chain re-verified intact).
- HONEST STATUS: OQ-DB-01..08 are Template. No witnessed session was run for this programme.
  The catalog introspection, 442 automated tests (12 written for it) and the live checks are
  supporting evidence only. Two permanent acceptances (user_account/outbox_event RLS) and two
  record dispositions (historical unattributed ledger rows) are recorded and need reflecting
  in the site deviation register.

## OQ-DB-01..08 executed (2026-08-01) [EXECUTED on dev, UNSIGNED]

- Witnessed session ran the 8 schema-hardening OQ cases: 23 checks, 23 passed, 0 failed,
  0 deviations. Transcribed with verbatim actuals in
  docs/validation/13-OQ-Execution-Record-SchemaHardening-v1.51.2.md (OQ-EXEC-NTQMS-003).
- OQ-DB-02 proves isolation at BOTH layers separately, which a single probe would have
  conflated: the read fence (A cannot see B's parent -> INSERT 0 0) and the structural fence
  (elevated mismatched-tenant insert -> 23503 from the composite FK). Three cases carry a
  control step so a constraint refusing everything could not pass as one that discriminates.
- Observations: the composite FK's real name is fk_capa_action_nonconformance_tenant_id_nc_id
  (EF regenerated it in Phase 5, superseding the Phase-4 name); the longest database identifier
  is exactly 62 chars - the convention holds with zero margin.
- Doc 06 A.10 status moved Template -> Executed (dev), unsigned; Part F checklist item ticked
  with the qualified-environment re-execution still required. CSR rev 3.1: DOC-001 progress now
  36 OQ cases across docs 09-13, all unsigned.
- Does NOT close DOC-001 (dev workstation, unsigned), SEC-001 or OPS-001.

## RISK-03 — Part 11 e-signature ceremony extended to every signed-record gate (2026-08-06..07) [CLOSED on dev, UNSIGNED validation]

Closes RISK-03 / SEC-01 / NB-03-02 from the AS-BUILT review (Doc 12): before this work the
21 CFR Part 11 signing ceremony (`IESignatureService.SignAsync`, password + PIN, §11.200(a)(1))
was wired only to document-publish, so ~19 other regulated sign-off gates wrote signer fields but
minted **no `signature_record`** (§11.50/§11.70 unsatisfied). The ceremony is now minted on **every**
signed-record transition. No domain/schema change — the aggregates already carried their SoD + state
invariants; each handler pre-validates state/SoD **before** minting, so a refused sign-off leaves no
signature (append-only ledger). A reusable `SignatureContentHash` helper binds the signature to
non-file records; a reusable `qams-esign-dialog` + self-fetching `qams-signature-manifest` carry the
ceremony and the §11.50 manifest across the UI.

- **Gates converted (all mint + surface a manifest):** NC verify + NC close; the 14 analytical-quality
  sign-offs (linearity, detection-limit, outlier, method-comparison, reference-interval,
  instrument-comparability, precision, sigma, interference, carryover, lot-comparison, validation-study,
  uncertainty-budget approve, **and PtPlan approve**); internal-audit sign-off; quality-policy approve;
  change-control approve; management-review close; the 4 borderline SoD gates (supplier approve,
  conflict assess, competency authorize, test-authorization grant); and the 2 periodic-review
  completions (audit-trail review, user-access review).
- **New catalogue keys:** `proficiency-testing.sign`, `suppliers.sign`, `conflicts.sign`,
  `compliance.sign` (each auto-granted to Tenant Administrator via AllKeys + Quality Manager via
  predicate default; External Auditor correctly excluded — read-only). `access-reviews.sign`,
  `analytical-quality.sign`, `nc.sign`, `audits.sign` already existed.
- **Manifest display:** `GET /api/<subject>/{id}/signatures` on the 13 AQ studies + PtPlan (reusing
  `GetSignaturesForSubjectQuery`); `qams-signature-manifest` made self-fetching (`subjectUrl` input) so
  no per-study facade/API signal was needed and the NC page's `[signatures]` binding stays
  backward-compatible. `document-detail` migrated off its raw inline password/PIN form onto the shared
  dialog, so every gate uses one ceremony UI.
- **Tests:** `SignatureContentHashTests` (5), `VerifyNcSigningTests` (4), `AnalyticalSignOffSigningTests`
  (4), `QualityPolicySigningTests` (3), `AccessReviewSigningTests` (2) — all drive the **real**
  `ESignatureService`. Backend **478** (Domain 242 / App 90 / Arch 33 / Integ 31 +1 skip / Func 82),
  frontend **95** Karma, all green; `ApiSurface.approved.txt` reconciled (+28 GET signatures routes total).
- **Live PostgreSQL proofs:** wrong PIN → 422 SIG-001 (fence before mint); missing credential body → 400;
  preparer signing own record → SOD-* (fenced before mint); new-key-not-yet-granted → 403 AUTHZ-403;
  management-review close → 204 positive mint (`subjectRef = MRV:{id}` persisted).
- **UPGRADE GAP (act before release):** existing tenants' seeded roles need the 4 new `.sign` keys
  granted, plus 12 endpoints tightened `approve/void/create → sign` re-granted — enumerated in
  `docs/validation/06-…` **§A.19** (admin-grant or a deliberate data migration; not written, by design).
- **Docs:** delta doc 06 Part A §A.13–A.19 + URS-123..128 (Template); OQ doc 14 (NC-verify, unsigned);
  verification-log rows; ground-truth URS ceiling → 128. AS-BUILT review Doc 12/15 + standalone HTML and
  the Consolidated Compliance Status Report addendum all mark RISK-03 CLOSED.
- **Commits (dev):** `ddd1551` (pilot), `713ed40` + `b980921` (AQ), `0211fb6` + `eb4c596` (non-AQ),
  `3917dee` (PtPlan gate), `ed556e7` (review-close), `a16c77d` + `6bfccb3` (borderline), `eb7330d` +
  `58130f6` (AQ manifest), `1ce8a31` (delta doc), `9b6c63c` + `3d43849` (close-out: PtPlan manifest,
  periodic-review signing, document-detail migration), `bdcafac` (reports marked closed).
- **HONEST STATUS:** RISK-03 is a **code** closure — engineering-complete and verified on `dev`. The
  witnessed positive-mint OQ for the SoD-guarded gates (needs a second PIN'd operator so signer ≠
  preparer) and the execution + signature of URS-123..128 / OQ doc 14 remain **QA's**, folded under
  DOC-001. Does NOT close DOC-001, SEC-001 or OPS-001; the release verdict stays Pre-production.

## Deploy remediation — role/DDL correctness + Windows-service hosting (2026-08-07)

Post-review fixes to the deployment path (no application behaviour, API surface, or schema change):
- **Deploy docs — DDL role (`6eabfbb`):** the two ANTIGRAVITY prompts told operators to apply
  `migrations.sql` as `qams_app`, which either fails (runtime role has no DDL) or trips the
  TENANT-004 start-up guard (Production refuses to boot if `qams_app` owns the tables). Both now
  run DDL as `qams_owner` + add the previously-missing `harden-runtime-role.sql` grant step, and
  `DEPLOY.md`'s package manifest now lists that script (Step 1 already required it).
- **`harden-runtime-role.sql` — stale `ref` schema (`ce8ad04`):** the script granted on a `ref`
  schema no migration creates (schemas are `audit, qams, read, saas`); with `\set ON_ERROR_STOP on`
  the first `GRANT USAGE … ref` aborted the whole script on fresh install and re-run, so the
  least-privilege runtime role was never provisioned. Removed `ref` from all six references.
  `DatabaseRoleGuard` still lists `ref` harmlessly (it filters `pg_tables` by schema → 0 rows).
- **`Program.cs` — Windows-service SCM integration (this commit):** added
  `builder.Host.UseWindowsService()` (+ `Microsoft.Extensions.Hosting.WindowsServices` package).
  The deploy scripts register the exe via `sc.exe create` + `Start-Service`, but source had no SCM
  integration, so a recompiled build would hang `Start-Pending` and fail the A5 start gate
  (error 1053). The call is a no-op for console/dev and Linux-container hosting.
- **Docs:** IQ-31 added to delta doc 06 Part B (Template — unexecuted); Deployment Spec 10.2
  Windows-service row cites the mechanism.
- **VERIFIED:** `dotnet build src/NT.QAMS.WebApi -c Debug` → succeeded, **0 warnings** (package
  restored; `TreatWarningsAsErrors` holds); API restarted in console mode → `GET /health/ready`
  → **200** (proves the `UseWindowsService()` no-op path). **NOT run:** the full test suite, and the
  actual Windows-service install (`sc.exe`-reaches-Running) — that is IQ-31, a deploy-side check
  owned by QA. No API route changed, so `ApiSurface.approved.txt` is untouched.
- **`migrations.sql` — FORCE-RLS bypass syntax (this commit):** two migrations
  (`Hardening4_ChildTenancy`, `QualityHealthProfile`) opened their RLS-bypass with
  `SELECT set_config('app.bypass_rls','on',true);`. That is valid at top level (so `ef database
  update` / `MigrateOnStartup` — the dev/CI path — always worked), but EF wraps each migration in a
  `DO $EF$ … $EF$` plpgsql block for the `--idempotent` script, where a bare `SELECT` aborts with
  **42601** ("query has no destination for result data"). A from-scratch `psql -f deploy/migrations.sql`
  therefore failed — the path `Deploy-FullStack.ps1` and `DEPLOY.md` §1 use. Changed all four
  occurrences (Up + Down ×2) to `SET LOCAL app.bypass_rls = 'on';`, which is valid in **both** apply
  paths; `PERFORM` (proposed elsewhere) was rejected — it fixes the script but breaks the top-level
  path (invalid SQL outside plpgsql). Regenerated `deploy/migrations.sql`. Updated the documented
  convention that had prescribed the broken form (CLAUDE.md §5; `ntqms-database` skill Trap 1) and
  IQ-30 (now requires a from-scratch `psql -f` apply, not only `MigrateOnStartup`).
- **VERIFIED (live PostgreSQL 17, parse/behaviour):** inside `DO $EF$ BEGIN … END $EF$` — old
  `SELECT set_config` → **ERROR 42601**; `SET LOCAL` → **OK**. At top level — `PERFORM` → **syntax
  error**; `SET LOCAL` (in txn) → **OK**; old `SELECT set_config` → OK (why dev never saw it).
  `dotnet ef migrations script --idempotent` regenerated clean; the two bypass lines now read
  `SET LOCAL app.bypass_rls = 'on';`. **NOT run:** a full end-to-end from-empty-DB apply (no
  superuser/clean DB on this host) — that is IQ-30, owned by QA; and the full test suite.

## Product enhancement program (2026-08-08) [DONE on dev — pushed `b8259ee`/`cada683`, UNSIGNED validation]

Five product-backlog features (URS-129…134), each with domain/application/functional tests and a
validation-record set. Delivered in one commit (`b8259ee`) plus a deployment release note
(`cada683`). **No new permission keys** — every feature reuses an existing key, so there is **no
tenant authorization-upgrade action** (contrast RISK-03). Full backend suite **515** (Domain 254 /
App 102 / Arch 33 / Integration 33 +1 skip / Functional 93) + **95** frontend Karma, green on real
PostgreSQL; both new migrations round-trip; `ApiSurface.approved.txt` updated for every new route;
`deploy/migrations.sql` regenerated.

- **URS-129 — NC re-open (`nc.sign`, no new key):** `Nonconformance.Reopen(reason, actor)` — guarded
  `Closed → ActionPlan` (NC-023 wrong state, NC-024 missing reason), new `ReopenReason` (text) column,
  `NcReopened` event. NC is deliberately **not** in the `frozen_immutability` trigger set, so the
  transition is a legitimate audited state change. `ReopenNcCommand(NcId, Reason, Password, Pin)`
  `[RequirePermissionPolicy(nc, Sign)]` + handler copies the verify/close ceremony — pre-validate
  `Closed`, `IESignatureService.SignAsync` (subject `NC:{id}`, reason folded into the meaning and the
  content hash), then `Reopen`. `POST /api/nonconformances/{id}/reopen`; migration `AddNcReopenReason`.
  Frontend: shared `EsignDialogComponent` gains an optional mandatory-reason field (backward-compatible),
  Closed-state re-open action gated `nc.sign`. Tests: domain +3, `ReopenNcSigningTests` +3.
- **URS-130 — Quality Analytics report PDF & Excel (`reports.export`, already in catalogue):** new
  `IExportService.ToQualityAnalyticsReportPdf/Xlsx` (`ExportService.QualityAnalytics.cs`, partial) over a
  `QualityAnalyticsReportPack` wrapping `QualityAnalyticsDto` — branded QuestPDF with a health-score
  gauge, per-category weighted progress bars, Pareto bars and a 5×5 risk heat-matrix; ClosedXML summary
  sheet + one sheet per sub-system. `GET /api/exports/quality-analytics.pdf|.xlsx` re-query
  `GetQualityAnalyticsQuery` under the caller's scope, logged `RECORD_EXPORTED`. Frontend Export PDF/Excel
  buttons on the dashboard. Tests: `ExportServiceTests` +4 (full + empty computation render), functional
  +3. Live: both endpoints 200; sample 67 KB PDF / 17.5 KB XLSX generated from demo-lab.
- **URS-131 — User Manual PDF (authenticated, no key):** `IExportService.ToManualPdf`
  (`ExportService.Manual.cs`) — cover coverage-by-section chart, a linked table of contents with page
  numbers (`Section`/`SectionLink`/`BeginPageNumberOfSection`), and per-topic cards with a numbered step
  progress bar mirroring `HelpBodyComponent`. Content lives only in the SPA (the help catalogue), so the
  caller posts it localized (`ManualExportRequest`); `POST /api/exports/manual.pdf` auth-only,
  `EXPORT-003` size ceiling. Frontend "Export PDF Manual" button assembling `HELP_TOPICS` + i18n for the
  active language. Tests: `ExportServiceTests` +1, `ManualExportTests` +3. Live: POST 200, 78 KB PDF.
- **URS-132 — My Tasks unified action centre (authenticated, no key):** root cause of "not working" was
  `WorkTask` being a standalone table fed by only three inserts. New `GetMyActionsQuery`/`Handler`
  (`Sla/MyActionsSlice.cs`) — a **live read model** unioning composable per-source providers via
  `IAppDbContext` (no writes, no migration): manual tasks (inline-completable, completed history kept per
  URS-115), NCs assigned, CAPA actions owned, NC verify/close the user may sign (gated `nc.sign`, never
  their own NC), risk mitigation actions owned, objectives owned, review participation. `GET
  /api/tasks/my-actions`; the tasks page becomes a grouped feed with deep links; action/category types are
  stable codes localized client-side. **Gotcha fixed:** EF InMemory (the functional-test host) cannot
  translate `SelectMany` over an owned collection — the CAPA/risk providers use an owner-filter-then-flatten
  projection so the query runs on both InMemory and PostgreSQL (a handler-level InMemory test locks this in).
  Tests: `MyActionsHandlerTests` +1 (6 sources), `MyActionsTests` +2.
- **URS-133/134 — Mail Management + HTML e-mail (`notifications.manage`, no new key):** new tenant-scoped
  `TenantMailSettings` aggregate (`Domain/Notifications`) — sender identity (from name/address, reply-to),
  enable switch, brand accent, footer; invariants MAIL-001…004; `MailSettingsChanged` event. Migration
  `AddTenantMailSettings` — new table with **mandatory FORCE RLS + `tenant_isolation` policy**, a
  `tenant_id` unique index (one row per tenant), and a `#RRGGBB` CHECK; round-tripped; RLS verified from
  `pg_class`/`pg_policies`. **No SMTP transport credential is stored** — the relay stays in server config
  (SEC-001 posture). `IEmailSender.SendAsync` now takes a rich `EmailMessage`; `HtmlEmailTemplate`
  (`Infrastructure/Email`) renders a self-contained, brand-accented, HTML-escaped e-mail with a plain-text
  alternate; `SmtpEmailSender` sets the From/Reply-To from the message; `NotificationDispatcher` resolves
  the tenant's mail identity per dispatch and gates e-mail on `Enabled`. `GET`/`PUT
  /api/notifications/mail-settings` gated `notifications.manage`; frontend `/mail-management` page (nav +
  help topic). Tests: `TenantMailSettingsTests` +9, `MailSettingsRlsTests` +2 (real-PG isolation),
  `MailSettingsEndpointTests` +3, `HtmlEmailTemplateTests` +3. Live: settings saved (`PUT 204`) and persisted.
- **VERIFIED:** full solution suite on real PostgreSQL — Domain 254 / App 102 / Arch 33 / Integration 31 +1
  skip → 33 / Functional 93 = **515** green; frontend production build clean + **95** Karma; migrations
  `AddNcReopenReason` and `AddTenantMailSettings` applied and round-tripped; each feature exercised live in
  the running app (browser). **NOT run:** the Playwright e2e suite (unchanged). **Left for QA** (documented
  in the OQ records): the positive NC-reopen live walk (needs a Closed NC + a PIN-holding non-raiser), a
  multi-source My-Tasks live walk, and a witnessed live SMTP send on a configured relay.
- **Docs:** delta doc 06 URS-129…134; OQ execution records `docs/validation/15…19-OQ-Execution-Record-*.md`;
  FRA hazard #7 (NC re-open); `verification-log.md` rows; `deploy/RELEASE-NOTE-PRODUCT-ENHANCEMENTS.md`.
  Validation records ship **Template/unsigned** — QA owns execution and signature (folds under DOC-001).
  Release posture unchanged — **Pre-production**; DOC-001 + SEC-001 remain the open blockers.

## 2026-08-25 → 2026-08-28 — HQMS hospital-module train + conformance audit + Group A remediation

- **Feature train** (`feature/hqms-hospital-modules`, now = `origin/dev`): 12 new bounded contexts (M02, M04–M13, M15, M17, M24) + 4 completions of live aggregates (M01 R&U, M14, M16, M18) + the M03 incident→CAPA convergence — 15 commits `8b80680…d5cf5a4`, 19 additive migrations, +352 API operations (0 removed/changed), 16 SPA pages, trilingual i18n throughout.
- **Conformance verification (2026-08-27/28):** the Architecture Conformance Verification Pack executed end-to-end on throwaway databases (production untouched). Gates 1–6 green with evidence: from-zero DB by both apply paths (parity 144=144, FORCE-RLS 134, 0 unprotected), OpenAPI diff purely additive, drift check empty, duplication improved (4.39%→3.77%), backend 845 + Karma green on real PostgreSQL, live cross-tenant probe 404 problem+json. Architecture suite extended **33 → 190 tests** + 3 shrink-only decision snapshots (`UngatedActions`, `EventsCarryingTenantId`, `CommandsWithoutValidators`). Gate 7 (ten adversarial slice reviews): **2 Blockers + 23 Majors** recorded — report `E:\QMS\NT_QAMS_HQMS_Conformance_Verification_Report_2026-08-28.md`, annexes A–C, and the Phase-0–3 register `E:\QMS\NT_QAMS_HQMS_Audit_Register_2026-08-28.md`.
- **Group A remediation (approved 2026-08-28, one atomic commit per finding, each with a test red before the fix):**
  `45ebd7b` M-01 SoD pre-checks before the ceremony signature (Ratify/Approve) · `f6d0a20` M-15 evidence links verified in-tenant + UI record picker · `889d2cb` M-03 one canonical windowed accrual (`SharedKernel.WindowedDays`) + future-date guard on ADT ingest · `c313e6e` M-09 command policies mirror endpoints; M14/M16 endpoints gated (arch suite 173→190) · `78c0c25` M-21 emergency-pathway rollback fails fast on live data (proven by executed Down/Up on a seeded throwaway DB) · `bba06e8` M-23 server-side register filters + anonymous-report tracking UI (Karma 133) · `2d25019` N-02 HttpParams across all 14 new API services · `92b38a2` N-01 create affordances permission-gated on 12 registers + denial explanations · `62bd116` N-03 real `--nt-surface-alt` token, tint-based tier pills, HQMS status-pill tones · law docs + this entry = M-13.
- **Still open (do not report as closed):** B-01 anonymity vs audit stamping (decision), B-02 validation/CSV delta (widens DOC-001), M-02 org-scope decision, M-04 cross-module-read ADR, M-05/M-06/M-07/M-08/M-10/M-11/M-12/M-14/M-16/M-17/M-18/M-19/M-20/M-22 + minors per the register. Release posture unchanged — **Pre-production**; DOC-001 + SEC-001 remain the open blockers. Baseline tag `verify/baseline-20260827` stays until the Blockers and M-01/M-07-class items clear a re-run of the pack.

## 2026-08-28 — Audit remediation, approved batch 2 (B-01 → M-07 → Group B → B-02)

One atomic commit per finding; each carries a test that failed before the fix.

- **B-01 (Blocker, closed `04a92ef`)** — the anonymity promise is now enforced at persistence:
  `IIdentitySuppressed` marker (SharedKernel), `Incident.IdentitySuppressed => IsAnonymous`;
  `AuditStampInterceptor` stamps CREATION as `anonymous`/null-id and `FieldChangeInterceptor`
  suppresses the actor on the "Created" row only — later transitions stay fully attributed.
  Deliberate consequence recorded on the marker: preparer-based SoD cannot apply to an anonymous
  creation. Tests: `AnonymousSuppressionTests` (3; two red pre-fix). App 129 / Domain 437 / Arch 190.
- **M-07 (Major, closed)** — the 65 new HQMS permission keys are now deliberate per-role decisions
  in `SystemRoleCatalog`, not fall-through: Department Head and Analyst gained explicit clinical
  grant rows (occurrence intake, HAI/mortality recording, EOC rounds, survey response entry,
  point-of-care `credentialing.view`); the **External Auditor is excluded** from `patient-safety`,
  `infection-control`, `mortality-review`, `credentialing` and `integration` (incl. the ADT census)
  by an explicit deny arm; the QM catch-all is annotated as deliberate. Release note
  `deploy/RELEASE-NOTE-HQMS-ROLE-GRANTS.md` (grant matrix for existing tenants + re-used-key
  disclosure: `risks.*`→FMEA, `audits.*`→audit programs/tracers, `training.*`→hospital training).
  Tests red-first: `SystemRoleCatalogTests.The_hqms_clinical_grants_are_explicit_per_role_decisions`,
  `AuditorDenyMatrixTests.The_auditor_is_excluded_from_clinical_registries_and_the_census`,
  3 HQMS rows in `RoleEndpointMatrixTests`, seeded-grant assertions in `RolePrivilegeFlowTests`
  (1 unit + 3 functional failures pre-fix; all green post-fix).
  **NOT RUN this cycle:** the 4 real-PostgreSQL functional tests (credential access blocked in the
  session) — risk bounded statically: `role_permission.permission_key` is varchar(60), longest new
  key is 26 chars, and Tenant Administrator's AllKeys grant already persisted all 65 keys on real
  PG in the 2026-08-28 green run; the new grants are same-shape subsets.
- **M-11 (Major, closed)** — malformed enum input is a 400, not a 500 (and never a smuggled write):
  new `RequestEnum.Parse<T>` (case-insensitive, defined-values-only) replaces `Enum.Parse` at all
  36 boundary sites in the 13 HQMS controllers; `DomainExceptionHandler` maps `ArgumentException`
  → 400 problem+json `REQ-001` fleet-wide (legacy endpoints' unknown-name path improves 500→400;
  disclosed in the release note). Legacy `Enum.Parse` sites and the 11 application-layer sites
  stay as recorded out-of-scope observations, now backstopped by the handler arm. Tests red-first:
  `MalformedEnumRequestTests` (unknown name = 500 pre-fix; undefined numeric = 201 pre-fix — both
  400 `REQ-001` after) + `RequestEnumTests` (4 contract facts). Functional 98/98, Arch 190/190.
- **Verification environment note (2026-08-29):** session-scoped credential access was blocked, so a
  dedicated throwaway PostgreSQL 17.2 instance was initialised at `E:\pg-verify` (port 55432, trust
  auth, non-superuser owner `vowner`, database `qams_verify`, full 80-migration chain applied —
  144-table parity with Gate 1). Production (`ntqams`, port 5432) untouched. The 4 real-PG
  functional tests skipped during M-07 were re-run here: **4/4 green** (provisioning now exercises
  the enlarged seeded grants against real constraints) — the M-07 caveat above is closed.
  `E:\pg-verify` is disposable evidence infrastructure: stop the server and delete after the batch.
- **M-05 (Major, closed)** — the three HQMS frozen-record types are now database-immutable:
  migration `20260828213425_HqmsFrozenRecordImmutability` registers `incident` (frozen at
  `Closed`) and `meeting` (frozen at `MinutesApproved`) with `qams.reject_frozen_mutation`, and
  adds `qams.reject_any_mutation()` for `survey_response`/`survey_answer` (create-only aggregates,
  immutable from capture). Meeting DECISIONS deliberately stay live (post-approval action-item
  tracking; own table). Domain: `Incident.LinkCorrectiveAction` now refuses Closed (INC-032) —
  the closure signature binds the content hash. Executed proof on the throwaway: Up applied,
  Down removed all 4 triggers + function, re-Up restored (counts 4→0→4). Tests red-first:
  `IncidentTests.A_closed_incident_cannot_gain_a_corrective_action_link` (guard) and
  `SignedRecordImmutabilityTests.Hqms_frozen_records_reject_raw_update_and_delete` (7 tamper
  probes, savepoint-isolated, all 23514) — both failed pre-fix. Domain 438 · App 130 · Arch 190 ·
  Integration 26+9 skips.
- **M-14 (Major, closed)** — the five HQMS foreign keys that shipped with EF's silent 62-char
  mid-word truncation ("…tenant_id_trai") are pinned to readable names via `HasConstraintName`
  (map additions in CLAUDE.md §5: ind_meas, eq_safety_notice, prac_priv, doc_aud_dept,
  ts_attendance); migration `20260828214735_PinTruncatedForeignKeyNames` renames in place
  (`RENAME CONSTRAINT`, metadata-only — no FK revalidation), `Down()` restores the truncated
  names. Executed proof on the throwaway: Up→5 pinned, Down→5 truncated restored, re-Up→5 pinned.
  Guard test red-first: `RelationalNameTruncationTests` (model-level, snake_case-aware,
  prefix-detection; 3 pre-baseline truncations frozen in a shrink-only allowlist) named exactly
  the five pre-fix. App 131 · Integration 26+9 · Arch 190.
- **N-04 (Minor, closed)** — the 14 HQMS unique indexes that shipped with EF's default `ix_`
  prefix are pinned to the house `ux_` convention via `HasDatabaseName`; migration
  `PinUniqueIndexNames` (EF `RenameIndex` → `ALTER INDEX … RENAME`, metadata-only), `Down()`
  restores. Guard fact `Every_unique_index_uses_the_ux_prefix` added to
  `RelationalNameTruncationTests` (48 pre-baseline `ix_` uniques frozen in a shrink-only
  allowlist) — red pre-fix naming exactly the 14. Executed proof: Up→14 `ux_`, Down→`ix_`
  restored, re-Up. App 132 · Integration 26+9 · Arch 190 · Domain 438.
- **N-05 (Minor, closed)** — schema hardening regressions in the HQMS train: (a) all 24 free-text
  `varchar(1000–4000)` columns are now `text`, with the bound in the command validator per
  hardening 1.2 — 23 already had `MaximumLength` rules; the one gap (`CloseDecisionCommand.Note`)
  gained `CloseDecisionValidator` (2000) and its `CommandsWithoutValidators` snapshot row was
  removed (shrink-only). (b) 8 numeric CHECK constraints added: FMEA severity/occurrence/detection
  1–10 and RPN 1–1000, planned-audit quarter 1–4, audit-program year 2000–2100,
  `rate_factor > 0`, `standard_element.weight >= 1` (NOT VALID + VALIDATE idiom). Migration
  `20260828220752_HqmsColumnHardening`; executed proof: Up → 0 over-bounded varchars + 8 checks,
  Down → 24 varchars restored + 0 checks, re-Up. Tests red-first:
  `No_free_text_column_is_a_bounded_varchar_of_1000_or_more` (model scan) and
  `Postgres_rejects_out_of_range_hqms_numerics` (8 savepoint-isolated corruption probes, all
  23514) both failed pre-fix. App 133 · Integration 27+9 · Arch 190.
- **M-08 (Major, closed)** — the 8 bare cross-aggregate reference columns in the HQMS train are
  now tenant-composite foreign keys (Hardening4 idiom, `NOT VALID`→`VALIDATE`, RESTRICT):
  `meeting→committee`, `survey_response→satisfaction_survey`, `survey_response→department`,
  `survey_answer→survey_question`, `evidence_link→standard_set`, `evidence_link→standard_element`,
  `planned_audit→audit`, `integration_message→integration_endpoint`. Declared in SQL (migration
  `20260828221537_HqmsCrossAggregateForeignKeys`) because two targets are EF owned children that
  `HasOne` cannot address. Executed proof: Up→8 FKs, Down→0, re-Up→8. Probe
  `CrossAggregateReferenceTests` red-first (all 8 dangling references persisted pre-fix; all
  23503 after — the survey probes are dangling INSERTs since M-05 freezes those rows against
  UPDATE). Two M-05-era test seeds gained real parents (the new FKs correctly rejected their
  dangling committee/survey ids — EF batching also cannot order SQL-declared FK inserts, so
  probe seeds save parents first; production flows always persist the parent in an earlier
  command). Functional 102/102 (incl. real-PG four) · Integration 28+9 · App 133 · Arch 190.
- **M-16 (Major, code half closed; ceremony decision deferred to Group C)** — committee governance
  integrity: `Meeting.RecordAttendance` rejects an empty attendee (MTG-024); `RecordAttendanceHandler`
  admits only committee members (CMT-017 — quorum can no longer be met by arbitrary Guids);
  Schedule/Hold/ApproveMinutes all refuse a disbanded committee (CMT-016). Unique backstops
  `ux_meeting_attendance_tenant_meeting_user` and `ux_committee_member_tenant_committee_user`
  (migration `CommitteeIntegrityIndexes`; EF also drops the two now-redundant FK-prefix indexes,
  restored by `Down()`). Whether ApproveMinutes becomes a Part-11 signed gate remains the deferred
  M-16 product decision. Tests red-first: 1 domain + 3 application (handler) + 1 integration
  (duplicate-row 23505 probes) — all failed pre-fix. Executed index proof: Up→2 ux, Down→0 ux +
  2 ix restored, re-Up. Domain 439 · App 136 · Integration 29+9 · Arch 190 · Functional 102.
- **M-17 (Major, closed)** — the indicator loop: `QualityIndicator.NormalizePeriod` maps every
  recorded period to its frequency's canonical start day before the IND-016 duplicate check (a
  Monthly indicator could carry two governed numbers, two SPC points and two breach tasks in one
  month); `UpdateIndicatorDefinitionValidator` added (update path was unbounded; snapshot row
  removed, shrink-only); SPC R2 window now admits the series opening (`end == 1` — a
  Beyond,Beyond start flags); the SPA seeds `targetForm` from the loaded record (saving untouched
  fields used to null the thresholds and silently kill breach grading) and Monthly indicators get
  a month picker (server normalizes regardless). Release-note disclosure for the correct-ward
  rejection of misaligned dates. Tests red-first: 2 domain normalization facts + 1 SPC fact + the
  arch snapshot shrink — all red pre-fix. Domain 442 · App 136 · Arch 190 · Karma 133 · prod build clean.
- **M-18 (Major, closed)** — HAI/complication correction path + honest rates: `HaiCase.Reject` /
  `ComplicationCase.Reject` (guarded — reason required, Closed/Rejected refuse; HAI-013/014,
  CMP-013/014; events raised), endpoints `POST cases/{id}/reject` + `complications/{id}/reject`
  (`<module>.void` at both tiers), migration `HaiComplicationRejectTransition` (3+3 reject columns
  + both status CHECK domains gain `Rejected`; Down restores — loudly failing on Rejected rows,
  deliberate). Counting convention decided and documented in the rates query: every non-rejected
  case counts from report; rejection is the correction. All three rate families
  (patient-safety/HAI/mortality) now return **null** for zero-denominator windows (DTOs additive-
  nullable, disclosed) and the SPA renders "—"; reject forms on the HAI detail and complication
  drawer (void-gated, trilingual keys). ApiSurface snapshot +4 additive routes (approved).
  Tests red-first: nullable-rate fact (was 0m) + CHECK-domain probe (23514 pre-migration) both
  red; HaiRatesTests' fabricated-zero assertion updated to the approved nullable contract; +2
  domain reject facts ×2 aggregates + rejected-exclusion rate fact. Executed Down/Up: 6 cols/2
  domains → 0/0 → 6/2. Domain 445 · App 138 · Integration 30+9 · Arch 192 · Functional 102 ·
  Karma 133 · prod build clean.
- **M-19 (Major, closed)** — credentialing integrity: PSV independence is enforced
  (`LicenceCredential.AddedByUserId` persisted — migration `LicenceAddedByForPsvSod` — and
  `VerifyLicence` throws `SOD-CRD-001` on self-verification); a verified licence cannot be
  re-verified in place (CRD-014); `RequireEvidence(asOf)` demands CURRENT evidence (unexpired
  verified licence + active grant) at Credential/Reappoint; a lapsed grant no longer blocks
  renewal (`RequestPrivilege(name, asOf)`); the point-of-care check includes the appointment
  window — a lapsed appointment answers false with "Appointment lapsed on …" detail (clinically
  significant flip, release-noted). Handlers supply actor/clock; harness updated to the new
  signatures. Tests red-first: lapsed-appointment and silent-re-verify facts failed pre-fix
  (runnable red); SOD/renewal/stale-evidence facts ride the signature change. Executed migration
  Down/Up. Domain 450 · App 138 · Arch 192 · Integration 30+9 · Functional 102.
- **M-20 (Major, closed)** — training delivery integrity: sessions can be scheduled and held only
  for an **Active** course (CRS-013 — Draft is still editable, Retired is history);
  `TrainingSession.PassMarkAtHold` freezes the pass threshold at Hold (migration
  `SessionPassMarkSnapshot`) and every recording is judged by that snapshot — the recording
  handler no longer reads the course, so "Passed" is reproducible on the session record alone
  (with the Active-only guard, live-mark drift is also structurally closed: `UpdateDetails` is
  Draft-only). The compliance dashboard now computes its stated basis — CURRENCY: per course,
  passes stay current for `ValidityMonths` from the session date (latest pass wins; null never
  lapses; pattern `CompetencyRecord.ExpiresAt`); DTO gains `CurrentTrainees/LapsedTrainees`
  (additive) and the SPA training page shows a "Lapsed trainings" stat. Tests red-first: the
  Draft-schedule fact and the snapshot fact both failed pre-fix; a stale-pass-lapses fact pins
  the currency math. Executed migration Down/Up. Domain 450 · App 141 · Integration 30+9 ·
  Functional 102 · Arch 192 · Karma 133.
- **M-12 code half (Major, code closed; retention/PHI ADR deferred to Group C)** — the ADT inbox
  is store-first: the `Received` row is persisted in its own save before processing (a crash can
  no longer lose the record and `Received` is reachable); the event type is a raw string parsed
  INSIDE the inbox so a malformed type becomes a recorded Failed message (previously bounced 400
  with no trace); non-domain failures are captured on the record (durably Failed + endpoint
  health + error in the result — not swallowed); concurrent duplicate deliveries resolve
  idempotently via the new `IDatabaseErrorClassifier` port (Npgsql 23505 impl in Infrastructure —
  race branch itself not directly driven, the dedup constraint is probe-verified);
  `RawPayload` bounded (100k); a patient-mismatched encounter refresh is refused (STAY-023);
  endpoint config split onto `integration.manage` (ingest keeps `create`) at both tiers + SPA
  gates. Tests red-first: the malformed-type functional fact (was 400/no row) and the
  patient-mismatch app fact (was silent merge) both failed pre-fix. Domain 450 · App 142 ·
  Integration 30+9 · Functional 103 · Arch 192 · Karma 133 · prod build clean.
- **M-10 (Major, closed)** — reads project and page: the three register-scale clinical endpoints
  (patient-safety events, HAI cases, device exposures) return the house paging envelope
  (`ToPagedAsync`, clamped `PageRequest`; SPA facades gained load-more per the incidents idiom,
  spec expectations updated to the paged contract); whole-aggregate loads replaced by server-side
  projections/grouping in: meetings list, practitioner register, expiring-credentials sweep
  (server-side `SelectMany`+filter), integration endpoint/reconciliation dashboards (grouped
  counts per (endpoint,status)), EOC rounds list (the EF-ignored `OpenFindingCount` no longer
  forces client evaluation) and EOC summary (seven database aggregates), survey list/results
  (per-question Sum/Count grouped in the database; domain/department means derived exactly from
  the sums), training sessions list + compliance loader (column projections; course titles fetched
  only for courses that appear), and the R&U dashboard's per-document acknowledgement N+1 folded
  into one query. Tests red-first: the 3-row paging-envelope functional theory failed pre-fix
  (bare arrays); the projection refactors are contract-preserving and ride the full suites.
  Functional 106 · App 142 · Arch 192 · Integration 30+9 · Karma 133 · prod build clean.
- **B-02 (Blocker, package authored as DRAFT — closes the documentation GAP; QA execution still
  folds under DOC-001)** — the HQMS validation package now exists: **REVAL-NTQMS-002**
  (`docs/validation/20-Revalidation-Delta-HQMS-Hospital-Modules.md`) carries the URS/RTM delta
  (URS-135…149 across all hospital modules and the two cross-cutting tiers, every row citing its
  design elements, evidence-engine suites and OQ case), the IQ delta (IQ-32…35 incl. the
  role-grant/reused-key review), the OQ evidence-engine table, a PQ addition (PQ-HQMS-01
  month-boundary rate recomputation), the VSR addendum (920/0 basis, open Group-C decisions named
  — the RawPayload retention/PHI ADR flagged as pre-PHI-go-live) and the QA execution checklist.
  OQ execution-record TEMPLATES: doc 21 (clinical: OQ-HQMS-01…07, 15, 17) and doc 22
  (governance/ops: OQ-HQMS-08…14, 16) — every Actual/P-F cell blank by design, DBA-witnessed
  tamper probes included. FRA (doc 02) gained nine HQMS area-risk rows (four HIGH). One
  verification-log row records the 2026-08-29 basis run (`abcc881`, 920/0 + Karma 133). All
  documents describe the POST-remediation behavior, per the approved sequencing.

## 2026-08-29 — Group C decisions (owner-approved): M-04, M-16-ceremony, M-12-ADR, M-02

Four deferred decisions approved (recommended options). Executed ascending blast radius.

- **M-04 (cross-module reads — ADR + enforcement)** — ADR-0010 accepts cross-module reads on the
  QUERY side and forbids new cross-module WRITES on the command side, documenting the as-built
  truth: shared cross-cutting modules (ComplianceLedger signing/audit, Files, Authorization,
  IdentityAccess) are free; six command handlers hold verified read-guards (LinkEvidence,
  ContextIssue, Login, ChangePassword, SetUserScope, GrantTestAuthorization); and the incident→CAPA
  convergence (`RaiseCapaFromIncidentHandler`) is the single sanctioned cross-module write (creates
  a Nonconformance from an incident in one transaction — corrected from the earlier "travels by
  event" claim after reading the handler). Enforced by `CommandHandlerScopeTests` (Mono.Cecil via
  NetArchTest; scans async state-machine bodies + generic type args; shrink-only approved map) —
  proven red with an emptied map. Arch 192→193.
- **M-16 ceremony half (Group C, closed)** — committee minutes approval is a Part 11 signed gate:
  the `committees` module moves to `SignedRecordLifecycle` (gains `.sign`); `ApproveMinutesCommand`
  gains Password/Pin with a validator and `[RequirePermissionPolicy(Committees, Sign)]`;
  `ApproveMinutesHandler` pre-validates (Held + minutes present + active committee) BEFORE
  `IESignatureService.SignAsync` (append-only ledger — no signature on a failed precondition),
  binding the hash to meeting ref + committee + minutes text; controller endpoint + `ApproveMinutesRequest`
  take {password,pin} (same route). Release-noted as a contract + grant change. Tests red-first:
  `Approving_minutes_is_a_signing_ceremony_that_mints_exactly_one_signature` and
  `…mints_no_signature` when minutes absent. App 142→144, Arch 193, Functional 102 (+4 real-PG skips
  — throwaway instance retired at batch close).
- **M-12 retention/PHI ADR (Group C, closed)** — ADR-0011 decides retention + masking. Masking at
  capture: `Hl7Redaction.MaskPatientIdentifiers` masks the PID direct-identifier fields
  (MRN/name/DOB/address/phone/SSN) while preserving message structure; `IntegrationMessage.Receive`
  applies it so no un-masked payload is ever stored. Retention purge:
  `IntegrationPayloadRetentionService` (leader-elected, 6-hourly) tombstones the payload of settled
  messages older than `Integration:PayloadRetentionDays` (default 90, clamp 1–3650) to «purged»,
  keeping the row as the health record; `PurgeOlderThanAsync` is the testable core (ExecuteUpdate).
  Tests: `Hl7RedactionTests` (5, domain — mask PHI, preserve structure, malformed-safe) and
  `IntegrationPayloadRetentionTests` (real-PG SkippableFact — settled+old purged, recent + Received
  kept). Domain 455 · App 144 · Integration 31+9 · Functional 106 · Arch 193.
- **M-02 org-scope (Group C, closed)** — the five patient-level clinical registers (Incident,
  PatientSafetyEvent, HaiCase, MortalityReview, ComplicationCase) now implement `IAllocatable`, so
  the composed tenant+working-scope query filter and the `OrgScopeGuardInterceptor` apply to them
  automatically (three `DepartmentId` setters widened; no schema change — the org columns were
  already mapped). Branch-restricted users now see only their branch's + unattributed clinical
  records and cannot write out of scope; unrestricted actors/jobs are unaffected (null-object
  privileges). Governance/config registers (committees, credentialing, standards, indicators,
  integration) stay tenant-wide by design. Red-first: `ClinicalWorkingScopeTests` (functional) —
  proven red by removing `IAllocatable` from Incident (branch B's incident leaked), green restored.
  Domain 455 · App 144 · Arch 193 · Integration 31+9 · Functional 107.

## 2026-08-29 — Remaining open findings (post Group C)

- **M-06 (ledger half, closed; notification-recipient wiring remains a product decision)** — the
  regulated decision facts of the hospital modules now raise domain events, which the
  OutboxInterceptor drains to the hash-chained audit ledger for every aggregate: `MortalityClassified`
  + `MortalityReviewClosed`, `DrillEvaluated`, `RoundCompleted`, `PrivilegeGranted` +
  `PractitionerCredentialed` + `PractitionerSuspended`, `SafetyEventClosed`. Standing enforcement:
  `DomainEventsAreRaisedTests` (Mono.Cecil) fails the build if any declared `DomainEvent` is never
  instantiated — the register's earlier dead-declaration observations are clean under it. Tests:
  `HqmsDecisionEventTests` (5, red-first — each event asserted at its decision point). WHO is
  *notified* of these facts (recipients/rules) stays the deferred product decision; the ledger
  gap — the compliance-material half — is closed. Domain 460 · App 144 · Arch 202 · Functional 103 (+4 real-PG skips).
- **M-22 (guard half, closed; EOC→CAPA hand-off remains a product decision)** — an FMEA failure
  mode could flip to `Actioned` via `RecordResidual` with no `RecommendedAction` on record — a false
  prospective-risk claim. The residual-scoring transition now requires a recorded action first
  (FME-020). Red-first: `FmeaStudyTests.Cannot_record_residual_before_a_recommended_action`. The
  drill/finding→CAPA hand-off (new `NcSourceType` + CHECK-widening migration) stays the deferred
  product decision. Domain 461.
- **N-13 (closed)** — temporal-integrity guards: a new equipment downtime period cannot overlap a
  prior one (EQP-035 — overlap double-counted in the availability sum; contiguous starts allowed);
  a supplier CAR response cannot predate the CAR being raised (SUP-CAR-010); a safety notice cannot
  be actioned before it was received (EQP-SN-010). Red-first: 3 domain tests. Domain 464.
- **N-07 (closed)** — the indicator-breach analysis-task policy deduped only against Pending tasks,
  so a redelivered `IndicatorBreached` re-opened an analysis that had already been completed; and
  the SubjectRef date key was culture-dependent. Dedup now counts ANY task for the breach period
  (one breach → one task, ever), and the key formats the period with `InvariantCulture`. Red-first:
  `A_completed_analysis_task_is_not_reopened_by_a_redelivered_breach`. App 145.
- **N-11 (round-trip test added)** — the outbox event payload shape (Web camelCase) is now pinned by
  `OutboxPayloadShapeTests`: a domain event round-trips losslessly through the exact serializer
  options the OutboxInterceptor/OutboxProcessor use, and the serialized JSON is asserted camelCase
  so a PascalCase regression that would orphan historical ledger rows fails the build. App 147.
- **N-06 (closed)** — SoD code convention: a sweep of the HQMS SoD gates found the mortality
  second-review guard throwing a module code (`MRT-014`) while its own message referenced a phantom
  `SoD-MRT-001` that nothing threw. It now throws `SOD-MRT-001` (the `SOD-*` convention the incident,
  CAPA, credentialing and analytical-quality gates already follow), and the message/docstring match
  the real code. Test updated. (The other HQMS SoD gates — incident close SOD-INC-001, credentialing
  SOD-CRD-001, CAPA SOD-CAPA-00x — already comply.) Domain 464.
