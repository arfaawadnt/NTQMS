# NT.QAMS — Complete Structural Breakdown (Scale & Scope Map)

| Field | Value |
|---|---|
| Companion to | AS-BUILT Review [`00_REVIEW_MANIFEST.md`](00_REVIEW_MANIFEST.md) |
| Commit | `d74d4bf` (`master`) |
| Generated | 2026-08-02, from fresh read-only enumeration of source (no execution) |
| Purpose | Exhaustive structural inventory — every layer, module, entity, class, command, query, and the total scale |

> All figures are counted from source at the reviewed commit. Where a count differs from `CLAUDE.md` it is flagged. Nothing was executed; nothing was modified.

---

## 0. Scale at a glance (the headline numbers)

| Dimension | Count |
|---|---|
| **Solution projects** | 12 (6 `src` + 5 test in `.sln`, + `LoadTests` outside) |
| **Backend source files** (`.cs`, excl. bin/obj) | **401** (SharedKernel 11 · Domain 56 · Contracts 22 · Application 89 · Infrastructure 165 · WebApi 58) + 100 test |
| **Frontend source files** (`.ts`, excl. specs) | **204** (221 incl. 17 specs) |
| **C# classes** | ~749 |
| **C# records** (DTOs + commands + queries + events + VOs) | ~730 |
| **C# interfaces** | 31 (≈19 application ports + others) |
| **C# enums** | 72 |
| **Aggregate roots** | **57** |
| **Domain entities / owned children** | 29 (+4 plain owned, +6 persistence/read types) |
| **Value objects** | 11 |
| **Domain events** | **82** |
| **Domain services** | 1 (`WestgardEvaluator`) |
| **Database tables** | **99** across 4 schemas (qams 92 · audit 4 · saas 2 · read 1) |
| **EF migrations** | 59 (CLAUDE.md) / 58 non-designer on disk |
| **EF entity configurations** | 67 |
| **CQRS commands** | **217** |
| **CQRS queries** | **105** |
| **MediatR handlers** | ~322 (in 238 handler classes) |
| **FluentValidation validators** | 90 |
| **MediatR pipeline behaviors** | 5 |
| **Sagas / event policies** | 11 (+1 dispatcher engine) |
| **Application ports (interfaces)** | ~19 |
| **Wire DTOs (Contracts)** | 298 records |
| **HTTP controllers** | **54** (in 42 files) |
| **HTTP routes** | **333** (666 counting the `api/v1/…` mirror) |
| **Custom middleware** | 6 (+2 handlers, +3 helpers) |
| **Permission catalogue keys** | 170 |
| **Frontend components** | 107 (84 feature + 18 shared-ui + shell/core) |
| **Frontend signal facades** | 35 |
| **Frontend API client services** | 44 |
| **Frontend guards / interceptors** | 2 / 2 |
| **Frontend lazy routes** | 84 (48 tenant feature routes) |
| **i18n dictionary keys** | ~1,571 (EN/AR/FR) |
| **Automated tests** | 395 backend (static) + 87 frontend unit + 6 Playwright e2e |

---

## 1. Architecture & Layers

**Pattern:** Clean Architecture **modular monolith** + **CQRS over MediatR** + **DDD** (rich aggregates) + **multi-tenant PostgreSQL FORCE RLS** + **transactional outbox** for domain events. One deployable ASP.NET Core app; bounded contexts are enforced module *folders*, not separate services. Dependencies point inward; enforced as CI merge gates by `NT.QAMS.Architecture.Tests`.

```
┌─────────────────────────────────────────────────────────────────────┐
│  frontend/  Angular 22 SPA  (standalone components · signal facades)  │
│    components → facades → core/api services ──HTTP /api──┐            │
└──────────────────────────────────────────────────────────┼──────────┘
                                                            ▼
┌───────────────────────────────────────────────────────────────────────┐
│  NT.QAMS.WebApi        host · 54 controllers · 6 middleware · authz     │  ← depends on Application, Contracts, Infrastructure (DI only)
├───────────────────────────────────────────────────────────────────────┤
│  NT.QAMS.Infrastructure  EF Core · interceptors · outbox · security ·   │  ← implements Application ports
│                          jobs · compliance ledger · storage · email     │
├───────────────────────────────────────────────────────────────────────┤
│  NT.QAMS.Application    CQRS handlers · validators · 5 behaviors ·       │  ← depends on Domain, Contracts
│                         sagas · ~19 ports (interfaces)                   │
├───────────────────────────────────────────────────────────────────────┤
│  NT.QAMS.Domain (57 aggregates)     NT.QAMS.Contracts (298 DTOs)         │  ← Domain depends only on SharedKernel
├───────────────────────────────────────────────────────────────────────┤
│  NT.QAMS.SharedKernel   TenantId · UserRef · LocalizedText · AggregateRoot · Entity · ValueObject · IClock · IDomainEvent │
└───────────────────────────────────────────────────────────────────────┘
                                                            ▼
                             PostgreSQL 17 (99 tables · FORCE RLS) + local file store
```

**Request pipeline (both layers):** Angular interceptors (auth bearer, change-reason) → WebApi middleware (ForwardedHeaders → Observability → SecurityHeaders → ExceptionHandler → AuthN → RateLimiter → TenantResolution → ActiveSession → MfaGate → ChangeReason → AuthZ) → controller `[RequirePermission]` → `ISender.Send` → MediatR pipeline (**Tracing → Logging → Authorization → Idempotency → Validation**) → handler → domain aggregate → `SaveChanges` (6 interceptors) → outbox → sagas + hash-chained audit trail.

**19 bounded-context modules** (Domain folders): AnalyticalQuality · AuditManagement · Authorization · Competency · ComplianceLedger · DocumentControl · Equipment · Facility · Files · IdentityAccess · Improvement · Notifications · Organization · Records · Reporting · RiskGovernance · Sla · SupplierQuality · Tenancy.

---

## 2. Domain Models & Entities (57 aggregates · 99 tables)

Tree of aggregate roots → owned entities, per module. Every tenant-scoped table has a composite `(tenant_id, id)` PK and FORCE RLS (2 accepted exceptions: `user_account`, `outbox_event`).

```
AnalyticalQuality/ (17 aggregates)
├─ QcProfile ─┬─ (target mean/SD, effective-dated)          [qams.qc_profile]
│             └─ QcRun (stored Westgard verdict)             [qams.qc_run]
├─ PtEnrollment (z-score, performance banding)              [qams.pt_enrollment]
├─ PtPlan ── PtPlanItem                                     [qams.pt_plan / pt_plan_item]
├─ ValidationStudy ── ValidationReplicate                  [qams.validation_study / validation_replicate]
├─ MethodComparisonStudy ── MeasurementPair                (Deming/Passing-Bablok/Bland-Altman)
├─ LinearityStudy ── LinearityMeasurement
├─ DetectionLimitStudy ── DetectionMeasurement             (LoB/LoD/LoQ)
├─ ReferenceIntervalStudy ── ReferenceSample
├─ PrecisionStudy ── PrecisionMeasurement                  (ANOVA)
├─ CarryoverStudy ── CarryoverReading
├─ LotComparisonStudy ── LotSamplePair
├─ InterferenceStudy ── InterferenceMeasurement
├─ InstrumentComparabilityStudy ── InstrumentReading
├─ OutlierScreening ── OutlierDataPoint                    (Tukey + MAD)
├─ SigmaAssessment (σ metric + QC recommendation)
└─ UncertaintyBudget ── UncertaintyComponent               (GUM)
    → 13 study aggregates share state DataEntry→Calculated→SignedOff + SOD-AQ-001

Improvement/ (5 aggregates)
├─ Nonconformance ─┬─ CapaAction ─┬─ RcaRecord             [qams.nonconformance / capa_action / rca_record]
│                   (9-state NC/CAPA machine; SoD raiser≠verifier≠closer)
├─ Complaint (ISO §7.9 lifecycle; auto-NC on justified)    [qams.complaint]
├─ QualityObjective ── ObjectiveProgressUpdate
├─ FeedbackEntry (escalatable → Complaint)
└─ QualityPolicy (versioned Draft→Active→Superseded, SoD)

RiskGovernance/ (4 aggregates)
├─ RiskItem ── MitigationAction (residual RPN alert)
├─ ChangeRequest (propose→approve→close→PIR)
├─ ManagementReview ─┬─ ReviewDecision └─ ReviewParticipant (ISO §8.9)
└─ ConflictDeclaration (impartiality/COI)

Equipment/ (2)  ├─ EquipmentItem ─┬─ CalibrationRecord ├─ MaintenanceRecord └─ IntermediateCheck (auto-lockout)
                └─ ReferenceStandard (CRM traceability)
DocumentControl/ (3) ├─ ControlledDocument ── DocumentVersion (SOP lifecycle + Part-11 publish e-sig)
                     ├─ DocumentAcknowledgement  └─ DocumentControlledCopy
Competency/ (3) ├─ CompetencyRecord ── AssessmentResult  ├─ TrainingAssignment  └─ TestAuthorization (matrix)
SupplierQuality/ (2) ├─ Supplier ── CertificateRecord   └─ SupplierEvaluation (weighted)
AuditManagement/ (1) └─ Audit ─┬─ AuditChecklistItem └─ AuditFinding (finding→NC saga)
Facility/ (1) └─ MonitoringPoint ── EnvironmentalReading (excursion→NC saga)
Records/ (1) └─ ArchiveEntry (retention class · legal hold · disposal gate)
IdentityAccess/ (2) ├─ UserAccount ─┬─ RefreshSession ├─ UserBranchAccess └─ UserDepartmentAccess (MFA/lockout/PIN/scope)
                    └─ UserAccessReview
Authorization/ (1) └─ Role ── RolePermission (dynamic tenant roles over 170-key catalogue)
Organization/ (6) ├─ Branch ├─ Department ├─ TestCatalogItem ├─ LovEntry ├─ InterestedParty └─ ContextIssue
Sla/ (3) ├─ SlaDefinition ├─ WorkTask └─ EscalationTimer
Notifications/ (2) ├─ NotificationRule └─ NotificationDispatch
ComplianceLedger/ (1 AR + 4 append-only ledger types)
   └─ AuditTrailReview ; ledgers: AuditTrailEntry(hash-chained) · SignatureRecord · SecurityEvent · FieldChangeRecord   [audit.*]
Reporting/ (1 AR + 1 read model)  └─ QualityHealthProfile ── QualityHealthWeight ; KpiSnapshot  [read.kpi_snapshot]
Tenancy/ (1)  └─ Tenant + TenantSettings + TenantSlug(VO)   [saas.tenant]  (control plane)
Files/ (1)  └─ FileReference (content-addressed SHA-256)   [qams.file_reference]
```

**Relationship rules:** cross-aggregate FKs are **tenant-composite** `(fk, tenant_id)→(id, tenant_id)` (cross-tenant parentage structurally impossible); ~60 FKs, 56 intra-aggregate cascades only; authorship/file references (`created_by_user_id`, `file_id`, `signer_id`) are **bare Guids with no FK** by design (append-audit). 6 cross-module sagas connect modules (see §4). Full ERD in Document 04.

**Enum domains (69 in Domain):** every status/type enum is stored as a string with a mirroring DB CHECK constraint (87 total). Representative state machines in Document 07.

---

## 3. Classes, Interfaces & DTOs

### 3.1 Application ports (interfaces — the Infrastructure contracts, ~19)
`IAppDbContext` (persistence, ~60 DbSets) · `IFileStorage` · `IExportService` · `ICurrentTenant`/`ICurrentTenantSetter` · `ICurrentUser` · `ICurrentChangeReason`/`Setter` · `IUserPrivileges`/`Setter` + `IPrivilegeResolver` · `IIdempotencyStore` + `IIdempotencyKeyAccessor` · `ITotpService` · `ISecurityEventLog` · `IESignatureService` · `IComplianceLedgerStore` · `IPasswordHasher` · `IJwtTokenService` · `IReferenceNumberGenerator` · `IEmailSender` · (`IClock`, `IDomainEvent` from SharedKernel).

### 3.2 Infrastructure implementations (adapters)
| Port | Implementation |
|---|---|
| Persistence | `AppDbContext` + **6 interceptors** (AuditStamp, TenantStamp, TenantConnection[GUC], OrgScopeGuard, Outbox, FieldChange) + **67 EF configurations** |
| Reliability | `OutboxProcessor` (SKIP LOCKED lease, backoff, dead-letter) · `PostgresReferenceNumberGenerator` · `AdvisoryLock` · `EfIdempotencyStore` |
| Security | `IdentityPasswordHasher` (PBKDF2) · `JwtTokenService` (HS256) · `TotpService` (RFC 6238) · `DatabaseRoleGuard` |
| Compliance | `LedgerHash` · `AuditTrailAppender` (hash chain) · `ESignatureService` · `ComplianceLedgerStore` (+VerifyChain) · `SecurityEventLog` |
| Authorization | `PrivilegeResolver` (DB-read per request) · `RequestPrivileges` |
| Adapters | `SmtpEmailSender`/logging fallback · `LocalFileStorage` · `ExportService` (ClosedXML/QuestPDF) |
| Jobs (`BackgroundService`) | `OutboxProcessor` · `KpiSnapshotService` (6h) · `ScheduledSweepService` (1h) · `SingleReplicaGuardService` |
| Ops | `PostgresReadinessHealthCheck` · `QamsDiagnostics`/`QamsMetrics` (OTel) · `ConfigGuard` · `SystemClock` · `CurrentTenant`/`CurrentChangeReason` |

### 3.3 WebApi (54 controllers, 6 middleware)
- **Controllers** — 37 single-controller files + 5 multi-controller files (`PlatformControllers`×5, `GovernanceControllers`×4, `AnalyticalQualityControllers`×3, `OperationsControllers`×3, `CompetenciesController`×2). All `ISender`-based except `FilesController` (streams via `IAppDbContext`+`IFileStorage`).
- **Middleware (6)** — Observability · SecurityHeaders · TenantResolution · ActiveSession · ChangeReason · MfaEnrollmentGate. Plus `DomainExceptionHandler` (→ problem+json), `ProblemAuthorizationResultHandler`, `HttpCurrentUser`, `HeaderIdempotencyKeyAccessor`.
- **Authorization** — `RequirePermissionAttribute` (`[RequirePermission(module, action)]`, 152 uses) + `Roles` constants (platform tier only).
- **Security** — `FileContentPolicy` (allow-list + magic-byte) · `RateLimiting` (global/auth/e-sign/refresh partitions).
- **Startup** — `StartupSeeding` · `DeferredStartupSeeder` · `StartupSeedingState`. **Versioning** — `VersionedRouteConvention` (dual `api/…` + `api/v1/…`).

### 3.4 Contracts — 298 wire DTOs (records) across 13 module folders
AnalyticalQuality 89 · Governance 33 · Improvement 33 · IdentityAccess 27 · Resources 26 · Reporting 24 · Platform 21 · DocumentControl 13 · AuditManagement 8 · Operations 7 · Facility 6 · Tenancy 5 · Compliance 3 · Common 3. All `public sealed record`; requests + response DTOs, never domain entities on the wire.

### 3.5 Frontend classes
107 standalone components (all inline template+styles, 103 OnPush) · 35 signal facades (`providedIn:'root'`, `_list/_loading/_error/_selected` state) · **44 `core/api` typed services** (one per resource, over the `Paged<T>` envelope) · 2 guards (`authGuard`, `role.guard`→platform/tenant) · 2 interceptors (auth bearer, change-reason) · 18 shared-ui components (page-header, drawer, load-more, status-pill, list-stats, workflow-stepper, risk-matrix, gauge/donut/bar charts hand-rolled SVG/CSS, audit-trail, csv-import, user/lov/allocation pickers, export-menu, page-help) · `i18n.service` (~1,571 EN/AR/FR keys) · `auth.service` · `permissions.service` · `models.ts` (2,237-line shared type contract).

---

## 4. Functions, Commands & Queries (217 commands · 105 queries · 11 sagas)

### 4.1 MediatR pipeline behaviors (5, outermost→innermost)
1. **TracingBehavior** — one OTel span per request. 2. **LoggingBehavior** — structured request log (names only). 3. **AuthorizationBehavior** — deny-by-default command authz (AUTHZ-000 fail-closed); commands only. 4. **IdempotencyBehavior** — replay stored response per Idempotency-Key. 5. **ValidationBehavior** — runs FluentValidation before the handler.

### 4.2 Command / query catalog by module (responsibility per command)

| Module | Cmd / Qry | Representative commands (responsibility) |
|---|---|---|
| **AnalyticalQuality** | 76 / 31 | Per study family: `Create*`(val) → `Add*`/`Remove*` child → `Calculate*` (server-side stats) → `SignOff*` (SOD-AQ-001); QC: `RecordQcRunCommand` (Westgard eval), `UpdateQcTargetsCommand` (re-target + reason); PT: `RecordPtResultCommand` (z-score → auto-NC); `Import*` (bulk pairs/measurements) |
| **Improvement** | 27 / 10 | NC: `RaiseNc`→`Submit`→`Triage`→`Reject`/`RecordRca`→`PlanCapaAction`→`CompleteCapaAction`→`SubmitForVerification`→`Verify`→`ConfirmEffectiveness`; Complaint: `Log`→`Acknowledge`→`Validate`(→NC)→`Investigate`→`Outcome`→`Resolve`→`Close`; Feedback (`Escalate`→complaint); Objective; QualityPolicy (`Draft`→`Revise`→`Approve`) |
| **IdentityAccess** | 19 / 3 | `LoginCommand`(Anon+MFA), `RefreshToken`/`Logout`(Anon), `Enroll/ConfirmMfa`, `SetPin`, `RegisterUser`, `AssignUserRole`, `SetUserScope`(branch/dept), `ResetUserPassword`, `ChangeMyPassword`, `Open/CompleteAccessReview` |
| **RiskGovernance** | 17 / 8 | Risk (`Assess`→`AddMitigation`→`RecordResidual`→`Close`), Change (`Propose`→`LinkRisk`→`Approve`→`Close`→`Review`/PIR), Review (`Schedule`→`AddDecision`→`Close`), Conflict (`Declare`→`Assess`→`Close`) |
| **Organization** | 12 / 6 | Branch/Department/Test/Lov CRUD; InterestedParty & ContextIssue register (`Register`/`Revise`/`Close`/`LinkRisk`) |
| **DocumentControl** | 11 / 5 | `CreateDocument`→`Submit`→`Recommend`→`RejectVersion`→**`PublishDocument`(Perm Documents.Sign, e-signature)**→`DraftNewVersion`→`Retire`; `ConfirmReview`; controlled copies; `AcknowledgeDocument` |
| **Competency** | 10 / 5 | `AssignCompetency`→`ScoreAssessment`→`Authorize`→`Revoke`; training assign/complete; TestAuthorization `Grant`/`Suspend`/`Reinstate`/`Revoke` |
| **Equipment** | 9 / 4 | `RegisterEquipment`, `LogCalibration`, `LogMaintenance`, `RecordIntermediateCheck`(→NC), `Retire`; reference standard `Register`/`Quarantine`/`Reactivate`/`Retire` |
| **Facility** | 6 / 2 | `RegisterMonitoringPoint`, `SetMonitoringLimits`, `RecordReading`(→excursion→NC), `Suspend`/`Resume`/`Retire` |
| **Records** | 6 / 1 | `ArchiveRecord`, `Retrieve`, `Return`, `Dispose`, `PlaceLegalHold`, `ReleaseLegalHold` |
| **AuditManagement** | 5 / 2 | `ScheduleAudit`→`Start`→`AnswerChecklistItem`→`RaiseFinding`(→NC)→`SignOff` |
| **SupplierQuality** | 5 / 3 | `RegisterSupplier`, `AddCertificate`, `Approve`(SoD), `Suspend`, `RecordEvaluation`(weighted) |
| **Authorization** | 4 / 4 | `CreateRole`, `UpdateRole`, `SetRolePermissions`(reason), `SetRoleActive`; queries: catalogue, roles, my-privileges |
| **Sla** | 3 / 2 | `UpsertSla`, `CreateTask`, `CompleteTask` |
| **Notifications** | 2 / 3 | `UpsertNotificationRule`, `MarkNotificationRead`; feed + dispatch-monitor queries |
| **Tenancy** | 2 / 3 | `ProvisionTenant`(Role PlatformAdmin — seeds roles+LOVs), `SetTenantMfaPolicy`; workspace/tenant queries |
| **ComplianceLedger** | 2 / 7 | `Open`/`CompleteAuditTrailReview`; queries: audit-trail, signatures, security-events, **VerifyChain**, field-changes |
| **Reporting** | 1 / 6 | `UpdateQualityHealthWeights`(Perm Reports.Manage); queries: dashboard KPIs, KPI history, NC Pareto, SLA compliance, **quality-analytics**, health profile |
| **TOTAL** | **217 / 105** | 322 handlers · 90 validators |

### 4.3 Sagas / process managers (11 event policies + dispatcher)
`FindingToNcPolicy` (audit finding→NC) · `ComplaintToNcPolicy` (justified complaint→NC) · `PtToNcPolicy` (unsatisfactory PT→NC) · `IntermediateCheckToNcPolicy` (failed check→NC) · `ExcursionToNcPolicy` (env excursion→NC) · `CompetencyLapseAuthorizationPolicy` (expiry→suspend authorizations) · `DocumentReviewDuePolicy` (→notification + task) · `EscalationToTaskPolicy` (SLA breach→task) · `NotificationEventPolicies` (routes 11 events→dispatcher) · `SeedDefaultNotificationRulesPolicy` (tenant provision→8 rules) · `NotificationDispatcher` (rule-matching engine). All idempotent (at-least-once outbox).

### 4.4 The 333 HTTP routes
Every command/query is exposed via the 54 controllers as **333 routes** (666 with the `api/v1/…` mirror), the authoritative list living in `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` (a CI merge gate). Full endpoint inventory with auth/status/side-effects is Document 03.

---

## 5. Scale & Metrics (reconciled totals)

| Layer | Files | Key units |
|---|---|---|
| **SharedKernel** | 11 `.cs` | AggregateRoot, Entity, ValueObject, TenantId, UserRef, LocalizedText, IClock, IDomainEvent |
| **Domain** | 56 `.cs` | 57 aggregates · 29 entities · 11 VOs · 69 enums · 82 events · 1 service |
| **Contracts** | 22 `.cs` | 298 DTO records |
| **Application** | 89 `.cs` | 217 commands · 105 queries · ~322 handlers · 90 validators · 5 behaviors · 11 sagas · ~19 ports |
| **Infrastructure** | 165 `.cs` | AppDbContext · 6 interceptors · 67 EF configs · 59 migrations · outbox · 3 jobs · security/compliance/export/email/storage adapters |
| **WebApi** | 58 `.cs` | 54 controllers · 333 routes · 6 middleware · rate limiting · versioning · startup |
| **Tests** | 100 `.cs` | 395 backend tests (5 projects) + LoadTests harness |
| **Frontend** | 204 `.ts` | 107 components · 35 facades · 44 API services · 2 guards · 2 interceptors · 18 shared-ui · ~1,571 i18n keys · 87 unit + 6 e2e specs |
| **Database** | 59 migrations | 99 tables · 4 schemas · 93 FORCE-RLS · 91 composite PKs · 87 CHECKs · ~60 FKs · 148 indexes |

**Totals:** ~**501 backend `.cs` files** (401 src + 100 test), ~**221 frontend `.ts` files**, **~749 classes / ~730 records / 31 interfaces / 72 enums**, **322 CQRS handlers**, **333 HTTP endpoints**, **99 database tables**, **488 automated tests** (395 + 87 + 6).

### Count reconciliation notes
- Commands **217** (type declarations) = **219** `ICommandHandler<>` implementations (1:1, +2 minor). An earlier review draft's "146" was a colon-anchored grep that missed handlers implementing the interface after a comma — **217/105 is authoritative.**
- Aggregate roots **57** (precise count this pass) vs the review's earlier "~55" approximation.
- Migrations: **59** per `CLAUDE.md` (v1.52.0); **58** non-designer files on disk — a 1-file discrepancy flagged in Doc 00 (OBS-02), resolved to 59 (the `ReportingKpiSnapshots` filename false-negative).
- Contracts records **298**; total `record` declarations across `src/` **~730** (298 DTOs + 322 commands/queries + 82 events + VOs + options).

---

## 6. One-page system tree

```
NT.QAMS  (multi-tenant SaaS lab QMS · .NET 9 + Angular 22 + PostgreSQL 17)
│
├─ FRONTEND (Angular 22 SPA)
│  ├─ 28 feature areas → 84 components → 35 signal facades → 44 API services
│  ├─ core: auth · permissions · i18n(EN/AR/FR ~1571) · 2 guards · 2 interceptors
│  ├─ shared/ui: 18 components (charts hand-rolled)
│  └─ 84 lazy routes (dual-plane: platform control / tenant)
│
├─ WEBAPI (.NET 9 host)
│  ├─ 54 controllers → 333 routes (api/ + api/v1/)
│  ├─ 6 middleware · deny-by-default authz (170-key catalogue) · rate limiting
│  └─ OpenTelemetry · health/ready · /metrics
│
├─ APPLICATION (CQRS)
│  ├─ 217 commands + 105 queries → 322 handlers
│  ├─ 5 pipeline behaviors · 90 validators · 11 sagas · ~19 ports
│  └─ modules: 19 (AnalyticalQuality is the largest: 76 cmd / 31 qry)
│
├─ DOMAIN (DDD)
│  └─ 57 aggregates · 29 entities · 82 events · 69 enums · WestgardEvaluator
│     (guarded state machines · 10 SoD rules · Part-11 ledger)
│
├─ INFRASTRUCTURE
│  ├─ EF Core: AppDbContext · 6 interceptors · 67 configs · 59 migrations
│  ├─ Reliability: transactional outbox (SKIP LOCKED · dead-letter) · idempotency · xmin concurrency
│  ├─ Security: JWT · TOTP MFA · PBKDF2 · DatabaseRoleGuard
│  ├─ Compliance: hash-chained audit trail · e-signature · field-change ledger
│  ├─ Jobs: outbox · KPI snapshot · compliance sweep · single-replica guard
│  └─ Adapters: SMTP · local file store · ClosedXML/QuestPDF exports
│
└─ DATABASE (PostgreSQL 17)
   └─ 99 tables / 4 schemas (qams·audit·saas·read) · 93 FORCE-RLS · 91 composite PKs · 87 CHECKs
```

---

*Structural map generated from source at commit `d74d4bf`; read-only, nothing executed or modified. For behavioral detail see Documents 01–15; this appendix is the scale-and-scope index.*
