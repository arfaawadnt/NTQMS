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
