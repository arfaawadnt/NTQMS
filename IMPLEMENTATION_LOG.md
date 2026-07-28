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
