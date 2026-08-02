# 16 - Module Test Specification: Internal Audits, Checklists, Findings and Audit-Trail Review

**Module code:** `AUDIT`
**System under test:** NT.QMS v1.51.2, repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master`. Inspection date 2026-08-01.
**Binding conventions:** `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md` (read in full before this file).

**ID range consumed (this file, never renumber):**

| Kind | Range | Count |
|---|---|---|
| `TC-AUDIT-UNIT-001` .. `TC-AUDIT-UNIT-022` | 22 |
| `TC-AUDIT-BVA-001` .. `TC-AUDIT-BVA-010` | 10 |
| `TC-AUDIT-LOOP-001` .. `TC-AUDIT-LOOP-005` | 5 |
| `TC-AUDIT-EP-001` .. `TC-AUDIT-EP-005` | 5 |
| `TC-AUDIT-DT-001` .. `TC-AUDIT-DT-004` | 4 |
| `TC-AUDIT-STATE-001` .. `TC-AUDIT-STATE-006` | 6 |
| `TC-AUDIT-API-001` .. `TC-AUDIT-API-012` | 12 |
| `TC-AUDIT-SEC-001` .. `TC-AUDIT-SEC-009` | 9 |
| `TC-AUDIT-RLS-001` .. `TC-AUDIT-RLS-004` | 4 |
| `TC-AUDIT-INT-001` .. `TC-AUDIT-INT-007` | 7 |
| `TC-AUDIT-MCDC-001` .. `TC-AUDIT-MCDC-002` | 2 |
| `TC-AUDIT-PATH-001` .. `TC-AUDIT-PATH-002` | 2 |
| `TC-AUDIT-DF-001` .. `TC-AUDIT-DF-002` | 2 |
| `TC-AUDIT-COMP-001` .. `TC-AUDIT-COMP-005` | 5 |
| `TC-AUDIT-E2E-001` .. `TC-AUDIT-E2E-003` | 3 |
| `TC-AUDIT-A11Y-001` | 1 |
| `TC-AUDIT-PERF-001` .. `TC-AUDIT-PERF-002` | 2 |
| `TC-AUDIT-OBS-001` .. `TC-AUDIT-OBS-002` | 2 |
| `TC-AUDIT-DR-001` | 1 |
| `TC-AUDIT-EXPL-001` .. `TC-AUDIT-EXPL-004` | 4 (charters, section 7) |
| `TC-AUDIT-UAT-001` .. `TC-AUDIT-UAT-008` | 8 (Gherkin, section 6) |

**Total detailed test cases: 104.** Gap IDs consumed: `GAP-AUDIT-001` .. `GAP-AUDIT-020`.

**Completeness statement.** Complete for every behaviour that exists in `Audit`, `AuditChecklistItem`,
`AuditFinding`, `AuditTrailReview`, their command/query slices, `AuditsController`, the
`/api/compliance/audit-trail-reviews` slice, the `FindingToNcPolicy` and `AuditTrailAnomalyToNcPolicy`
sagas, the EF/PostgreSQL projection of `qams.audit`, `qams.audit_checklist_item`, `qams.audit_finding`,
`qams.audit_trail_review`, and the Angular `features/audits` + compliance ATR panel.
**Deferred (no executable case authored, Gap raised instead):** audit programme/cycle with a start/end
date range, overlapping-cycle detection, multi-department scoping, an auditor team, an ISO-clause
list-of-values, template-driven dynamic checklist loading, a distinct "notes" field, QM notification on
submission/sign-off, an internal-audit report export, reopening a signed-off audit, database-layer
record locking for `qams.audit`, an electronic signature on audit sign-off, and segregation of duties
on audit sign-off. These behaviours are named in the commissioning brief and **do not exist in this
build** - see section 8.

**No result in this file is anything other than `Not Run`. This package is authored, not executed.**

### Correction to ground truth

None. Every fact in `00-GROUND-TRUTH-AND-CONVENTIONS.md` that this module touches was re-verified and
holds: `RequirePermission` gating (`src/NT.QAMS.WebApi/Controllers/AuditsController.cs:30,70`),
`RequireInternalActor` command policy (`src/NT.QAMS.Application/AuditManagement/Commands/AuditCommands.cs:10,56,58,70,73`),
`xmin` as the only concurrency token (`src/NT.QAMS.Infrastructure/Persistence/AppDbContext.cs:129`),
tenant-first composite PKs (`src/NT.QAMS.Infrastructure/Persistence/Configurations/AuditConfiguration.cs:12`),
FORCE RLS on the owned child tables (`src/NT.QAMS.Infrastructure/Persistence/Migrations/20260731201114_Hardening4_ChildTenancy.cs:447-463`),
and the absence of a URS requirement covering internal-audit execution
(`docs/validation/01-User-Requirements-Specification.md`, URS-001..055 inspected; nearest are URS-018
and URS-034). One clarification worth recording rather than a correction: the ground truth lists the 13
tables carrying `qams.reject_frozen_mutation`; **`qams.audit` is not among them**
(`src/NT.QAMS.Infrastructure/Persistence/Migrations/20260726084134_SignedRecordImmutability.cs:14-29`),
so "record locking" for audits is a domain-only guard. That is captured as `GAP-AUDIT-011`.

---

## 1. Implementation inventory

### 1.1 Aggregates and entities

| Element | Kind | Fields / members actually found | Evidence |
|---|---|---|---|
| `Audit` | Aggregate root, `ITenantScoped`, `IAllocatable` | `TenantId, BranchId?, DepartmentId?, AuditRef, Title, Type, LeadAuditorId, PlannedDate, Status, SignedOffBy?, SignedOffAtUtc?, Checklist[], Findings[]` | `src/NT.QAMS.Domain/AuditManagement/Audit.cs:53-77` |
| `AuditChecklistItem` | Owned entity | `Id, IsoClause, Question, Verdict, Evidence?` | `src/NT.QAMS.Domain/AuditManagement/Audit.cs:14-29` |
| `AuditFinding` | Owned entity | `Id, Grade, Description, NcId?` | `src/NT.QAMS.Domain/AuditManagement/Audit.cs:31-45` |
| `AuditTrailReview` | Aggregate root, `ITenantScoped` | `TenantId, ReviewRef, PeriodStart, PeriodEnd, Status, ReviewedBy?, CompletedAtUtc?, EventsReviewed?, FieldChangesReviewed?, AnomaliesFound?, Conclusion?` | `src/NT.QAMS.Domain/ComplianceLedger/AuditTrailReview.cs:16-31` |

### 1.2 Enumerations (the REAL names - do not paraphrase)

| Enum | Members | Evidence |
|---|---|---|
| `AuditStatus` | `Scheduled, InProgress, SignedOff` | `src/NT.QAMS.Domain/AuditManagement/Audit.cs:6` |
| `AuditType` | `Internal, ExternalHosted` | `src/NT.QAMS.Domain/AuditManagement/Audit.cs:8` |
| `ChecklistVerdict` | `Unanswered, Conform, Ofi, NonConform` | `src/NT.QAMS.Domain/AuditManagement/Audit.cs:10` |
| `FindingGrade` | `Ofi, MinorNc, MajorNc` | `src/NT.QAMS.Domain/AuditManagement/Audit.cs:12` |
| `AuditTrailReviewStatus` | `Open, Completed` | `src/NT.QAMS.Domain/ComplianceLedger/AuditTrailReview.cs:6` |

> The commissioning brief's three result values "Conform / Opportunity for Improvement / Non-Conformance"
> map to `Conform` / `Ofi` / `NonConform`. A fourth member `Unanswered` exists as the initial state and
> is **rejected as an input** (`AUD-012`). Finding grades are a **different**, three-valued enum
> (`Ofi, MinorNc, MajorNc`) - there is no `Critical` grade.

### 1.3 Domain methods, guards and error codes

| Method | Guard order | Code | Exception type | HTTP |
|---|---|---|---|---|
| `Audit.Schedule` | blank/whitespace title | `AUD-001` | `DomainException` | 422 |
| `Audit.AddChecklistItem` | 1. signed-off; 2. blank question | `AUD-020`; `AUD-002` | `InvalidStateTransitionException`; `DomainException` | 409; 422 |
| `Audit.Start` | 1. status != `Scheduled`; 2. zero checklist items | `AUD-010`; `AUD-011` | `InvalidStateTransitionException`; `DomainException` | 409; 422 |
| `Audit.AnswerChecklistItem` | 1. status != `InProgress`; 2. verdict == `Unanswered`; 3. item not found | `AUD-019`; `AUD-012`; `AUD-013` | `InvalidStateTransitionException`; `DomainException`; `DomainException` | 409; 422; 422 |
| `Audit.RaiseFinding` | 1. status != `InProgress`; 2. blank description | `AUD-019`; `AUD-014` | `InvalidStateTransitionException`; `DomainException` | 409; 422 |
| `Audit.AcknowledgeFindingNc` | 1. signed-off; 2. finding not found; 3. grade == `Ofi` | `AUD-020`; `AUD-015`; `AUD-016` | `InvalidStateTransitionException`; `DomainException`; `DomainException` | 409; 422; 422 |
| `Audit.SignOff` | 1. status != `InProgress`; 2. any `Unanswered`; 3. count of non-`Ofi` findings with `NcId is null` > 0 | `AUD-019`; `AUD-017`; `AUD-018` | `InvalidStateTransitionException`; `DomainException`; `DomainException` | 409; 422; 422 |
| `AuditLoader.LoadAsync` | audit id not found in tenant scope | `AUD-404` | `DomainException` | 404 |
| `AuditTrailReview.Open` | `periodEnd < periodStart` | `ATR-001` | `DomainException` | 422 |
| `AuditTrailReview.Complete` | 1. status != `Open`; 2. blank conclusion | `ATR-010`; `ATR-011` | `InvalidStateTransitionException`; `DomainException` | 409; 422 |
| `CompleteAuditTrailReviewHandler` | review id not found | `ATR-404` | `DomainException` | 404 |

Evidence: `src/NT.QAMS.Domain/AuditManagement/Audit.cs:82,103,115,120,131,136,148,162,166,176,181,197,205`;
`src/NT.QAMS.Domain/ComplianceLedger/AuditTrailReview.cs:35,53,58`;
`src/NT.QAMS.Application/AuditManagement/Commands/AuditCommands.cs:88`;
`src/NT.QAMS.Application/ComplianceLedger/AuditTrailReviewSlice.cs:57`.
HTTP mapping: `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:45,54,63,69,75` -
`InvalidStateTransitionException` -> **409**, `*-404` suffix -> **404**, `AUTH-*` -> 401, `AUTHZ-*` -> 403,
any other `DomainException` -> **422**, `FluentValidation.ValidationException` -> **400**,
`DbUpdateConcurrencyException` -> **409 `CONCURRENCY-409`**.

### 1.4 Domain events and cross-module sagas

| Event | Raised by | Consumer | Evidence |
|---|---|---|---|
| `AuditScheduled(AuditId, AuditRef, Title, LeadAuditorId, PlannedDate)` | `Audit.Schedule` | **none** (outbox -> hash-chained ledger only) | `src/NT.QAMS.Domain/AuditManagement/Audit.cs:96,212` |
| `FindingRaised(AuditId, AuditRef, FindingId, Grade, Description, TenantId, RaisedBy)` | `Audit.RaiseFinding` | `FindingToNcPolicy` | `src/NT.QAMS.Domain/AuditManagement/Audit.cs:153,216`; `src/NT.QAMS.Application/AuditManagement/Policies/FindingToNcPolicy.cs:26` |
| `AuditSignedOff(AuditId, AuditRef, SignedOffBy)` | `Audit.SignOff` | **none** | `src/NT.QAMS.Domain/AuditManagement/Audit.cs:192,220` |
| `AuditTrailAnomalyFound(ReviewId, ReviewRef, PeriodStart, PeriodEnd, Conclusion, ReviewedBy, TenantId)` | `AuditTrailReview.Complete` when `anomaliesFound == true` | `AuditTrailAnomalyToNcPolicy` | `src/NT.QAMS.Domain/ComplianceLedger/AuditTrailReview.cs:73,78`; `src/NT.QAMS.Application/ComplianceLedger/AuditTrailReviewSlice.cs:81` |

`FindingToNcPolicy` behaviour actually implemented
(`src/NT.QAMS.Application/AuditManagement/Policies/FindingToNcPolicy.cs:32-82`):
`Ofi` returns immediately (no NC); otherwise the tenant is set from the event, the NC is looked up by
`SourceRef == "{auditRef}#{findingId:N}"` (idempotency), and when absent an NC is raised with
`severity = 4` for `MajorNc` / `2` for `MinorNc`, `likelihood = 3`, `NcSourceType.Audit`,
`raisedBy = evt.RaisedBy`, then `Submit()`; finally `AcknowledgeFindingNc` runs only if `NcId is null`.
`Nonconformance.Rpn = severity * likelihood` -> **12 for MajorNc, 6 for MinorNc**
(`src/NT.QAMS.Domain/Improvement/Nonconformance.cs:140`, per ground truth section 2).

`AuditTrailAnomalyToNcPolicy` raises an NC with `severity: 5, likelihood: 2` (`Rpn = 10`),
`NcSourceType.Internal`, `SourceRef = "ATR:{reviewId}"`
(`src/NT.QAMS.Application/ComplianceLedger/AuditTrailReviewSlice.cs:88-106`).

### 1.5 Endpoints

| Method + route | Controller gate | Command policy | Success status | Evidence |
|---|---|---|---|---|
| `GET /api/audits?status=&page=&pageSize=` | `[Authorize]` only - **no `[RequirePermission]`** | n/a - query | 200 `PagedResponse<AuditListItemDto>` | `AuditsController.cs:14-23` |
| `GET /api/audits/{id}` | `[Authorize]` only | n/a - query | 200 `AuditDetailDto` | `AuditsController.cs:25-27` |
| `POST /api/audits` | `[RequirePermission(audits, Create)]` | `[RequireInternalActor]` | 201 + `Location` header -> `GetById` | `AuditsController.cs:29-41` |
| `POST /api/audits/{id}/start` | **none beyond `[Authorize]`** | `[RequireInternalActor]` | 204 | `AuditsController.cs:43-48` |
| `POST /api/audits/{id}/checklist/{itemId}/answer` | **none beyond `[Authorize]`** | `[RequireInternalActor]` | 204 | `AuditsController.cs:50-59` |
| `POST /api/audits/{id}/findings` | **none beyond `[Authorize]`** | `[RequireInternalActor]` | 200 `{ findingId }` | `AuditsController.cs:61-67` |
| `POST /api/audits/{id}/sign-off` | `[RequirePermission(audits, Sign)]` | `[RequireInternalActor]` | 204 | `AuditsController.cs:69-75` |
| `GET /api/compliance/audit-trail-reviews` | class-level `[RequirePermission(compliance, View)]` | n/a - query | 200 `AuditTrailReviewDto[]` (unpaged) | `ComplianceController.cs:20,41-43` |
| `POST /api/compliance/audit-trail-reviews` | `[RequirePermission(compliance, Create)]` | `[RequireInternalActor]` | 200 `{ id }` | `ComplianceController.cs:45-49` |
| `POST /api/compliance/audit-trail-reviews/{id}/complete` | `[RequirePermission(compliance, Approve)]` | `[RequireInternalActor]` | 204 | `ComplianceController.cs:51-58` |

Every route is dual-exposed under `/api/v{version}/...`; all 14 rows appear in the approved surface
baseline `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt:29-30,42-43,250-254,285-286,137-138,150-151,443-447,478-479`.
**There is no DELETE and no PUT on any audit route**, so `ChangeReasonMiddleware`
(`X-Change-Reason` / `400 CHANGE-REASON-REQUIRED`) never engages for this module.

### 1.6 Permissions

| Key | Where required | Seeded holders (system roles) |
|---|---|---|
| `audits.view` | not enforced on any endpoint (see `GAP-AUDIT-018`) | TenantAdministrator, QualityManager, DepartmentHead, Analyst |
| `audits.create` | `POST /api/audits` | TenantAdministrator, QualityManager |
| `audits.edit` | not enforced on any endpoint | TenantAdministrator, QualityManager, DepartmentHead |
| `audits.approve` | not enforced on any endpoint | TenantAdministrator, QualityManager |
| `audits.void` | not enforced on any endpoint | TenantAdministrator, QualityManager |
| `audits.sign` | `POST /api/audits/{id}/sign-off` | TenantAdministrator, QualityManager |
| `audits.export` | not enforced on any endpoint (no audit export exists) | TenantAdministrator, QualityManager, DepartmentHead, Analyst |
| `compliance.view` / `.create` / `.approve` | ATR read / open / complete | per `SystemRoleCatalog` |

Catalogue: module key `audits` with the `SignedRecordLifecycle` bundle (View/Create/Edit/Approve/Void/Export/Sign),
`src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:81,139`.
Seeded grants: `src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs:98-112` (QM: everything except
`users`/`tenant-settings`), `:127` (DepartmentHead: View/Edit/Export), `:157` (Analyst: View/Export).
Command-layer gate: `RequireInternalActor` => any role except `UserRole.ExternalAuditor`, else
`AUTHZ-002` -> 403 (`src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs:75,83`).
Endpoint-layer denial code is `AUTHZ-403` -> 403 (`src/NT.QAMS.WebApi/Middleware/ProblemAuthorizationResultHandler.cs:16`).

### 1.7 Persistence, tenancy and constraints

| Object | Detail | Evidence |
|---|---|---|
| `qams.audit` | PK `(tenant_id, id)`; `audit_ref varchar(30)`, `title varchar(300)`, `type varchar(20)`, `status varchar(20)`; unique `(tenant_id, audit_ref)`; index `(tenant_id, status)` | `Configurations/AuditConfiguration.cs:11-20` |
| `qams.audit_checklist_item` | owned; shadow `tenant_id`; PK `(tenant_id, id)`; composite FK `(tenant_id, audit_id)`; `iso_clause varchar(30)`; `question`, `evidence` are `text` | `Configurations/AuditConfiguration.cs:22-35`; `Migrations/20260731180344_Hardening1_TypesAndNames.cs:604,614` |
| `qams.audit_finding` | owned; shadow `tenant_id`; PK `(tenant_id, id)`; composite FK `(tenant_id, audit_id)`; `grade varchar(20)`; `description` is `text` | `Configurations/AuditConfiguration.cs:37-48`; `Migrations/20260731180344_Hardening1_TypesAndNames.cs:594` |
| `qams.audit_trail_review` | PK `(tenant_id, id)`; `review_ref varchar(30)`, `status varchar(20)`, `conclusion varchar(4000)`; unique `(tenant_id, review_ref)`; index `(tenant_id, status)` | `Configurations/ComplianceConfigurations.cs:79-91`; `Migrations/20260725075423_PtPlanAndAuditTrailReview.cs:15-38,92-103` |
| RLS | `qams.audit` policy created 2026-07-21 (`Migrations/20260721220535_AuditManagement.cs:119-124`), `qams.audit_trail_review` 2026-07-25 (`Migrations/20260725075423_PtPlanAndAuditTrailReview.cs:126-130`); both rewritten to ENABLE+FORCE with `USING`/`WITH CHECK` + `app.bypass_rls` by `Migrations/20260726081443_ActivateForcedTenantRls.cs:24-47`; children added by `Migrations/20260731201114_Hardening4_ChildTenancy.cs:447-463` | as cited |
| CHECK constraints | `ck_audit_status_domain` IN (`Scheduled,InProgress,SignedOff`); `ck_audit_type_domain` IN (`Internal,ExternalHosted`); `ck_audit_checklist_item_verdict_domain` IN (`Unanswered,Conform,Ofi,NonConform`); `ck_audit_finding_grade_domain` IN (`Ofi,MinorNc,MajorNc`); `ck_audit_trail_review_status_domain` IN (`Open,Completed`); `ck_audit_signoff_order` (`signed_off_at_utc IS NULL OR signed_off_at_utc >= created_at_utc`) | `Migrations/20260731191212_Hardening3_CheckDomains.cs:33-42`; `Migrations/20260728073229_Phase5CheckConstraints.cs:65-67` |
| Concurrency | shadow `xmin` (`xid`) concurrency token on every `AggregateRoot`, incl. `Audit` and `AuditTrailReview`; conflict -> `409 CONCURRENCY-409` | `Persistence/AppDbContext.cs:117-131`; `Middleware/DomainExceptionHandler.cs:21,28-33` |
| Working-scope filter | `Audit` is `IAllocatable`: global query filter adds branch/department scope; write-side `OrgScopeGuardInterceptor` raises `SCOPE-001` / `SCOPE-002` | `Persistence/AppDbContext.cs:200-211`; `Persistence/Interceptors/OrgScopeGuardInterceptor.cs:53-65` |
| Reference numbering | `AUD-{yyyy}-{0000}` and `ATR-{yyyy}-{0000}` from `qams.ref_counter` via one atomic `INSERT .. ON CONFLICT .. RETURNING` | `Persistence/RefCounter.cs:32-43` |
| **NOT present** | `qams.audit` and `qams.audit_trail_review` are **absent** from the `reject_frozen_mutation` table set (only the 12 analytical study roots + `uncertainty_budget`) | `Migrations/20260726084134_SignedRecordImmutability.cs:14-29` |

### 1.8 Validators (FluentValidation)

| Command | Rules actually declared | Evidence |
|---|---|---|
| `ScheduleAuditCommand` | `Title` NotEmpty + MaximumLength(300); `LeadAuditorId` NotEmpty; `Checklist` NotEmpty; each item `Question` non-empty and `<= 1000` chars. **No rule on `IsoClause`, `Type` or `PlannedDate`.** | `Commands/AuditCommands.cs:16-28` |
| `AnswerChecklistItemCommand` | `Evidence` MaximumLength(2000) | `Commands/AuditCommands.cs:63-69` |
| `RaiseFindingCommand` | `Description` NotEmpty + MaximumLength(4000) | `Commands/AuditCommands.cs:76-79` |
| `CompleteAuditTrailReviewCommand` | `Conclusion` NotEmpty + MaximumLength(4000) | `ComplianceLedger/AuditTrailReviewSlice.cs:22-28` |
| `OpenAuditTrailReviewCommand` | **no validator class exists** | searched `src/NT.QAMS.Application/ComplianceLedger/AuditTrailReviewSlice.cs` |

### 1.9 Frontend

| Artefact | Behaviour found | Evidence |
|---|---|---|
| `AuditListComponent` | register table + stats; `search` and `branchFilter` are **client-side over the loaded page only**; schedule drawer with a `FormArray` checklist, min 1 row enforced by `removeItem` guard; `question` `Validators.required + maxLength(1000)`, `isoClause` `maxLength(30)`; "New" button gated on `perms.can('audits.create')` | `frontend/src/app/features/audits/audit-list.component.ts:25,58,136-142,155-180` |
| `AuditDetailComponent` | stepper `Scheduled -> InProgress -> SignedOff`; Start button visible only in `Scheduled`; verdict `<select>` only in `InProgress`; Sign-off button gated on `perms.can('audits.sign')` **and** `canSignOff()`; `canSignOff()` re-implements the AUD-017/AUD-018 gate client-side | `frontend/src/app/features/audits/audit-detail.component.ts:37-45,61-67,133-139` |
| `AuditsFacade` | signal store; `loadMore()` appends the next page; every mutation re-fetches the detail; errors read `problem.title` | `frontend/src/app/features/audits/audits.facade.ts:46-56,71-96` |
| Compliance ATR panel | open period + complete with `anomalies` + `conclusion` | `frontend/src/app/features/compliance/compliance.component.ts:225,253-270`; `frontend/src/app/core/api/compliance-api.service.ts:41-49` |
| Routes | `audits` with child `:id` rendered inside a 920px drawer | `frontend/src/app/app.routes.ts:106-114` |

### 1.10 Existing automated coverage (baseline, not authored here)

`tests/NT.QAMS.Domain.UnitTests/AuditManagement/AuditTests.cs` (6 facts: AUD-011, AUD-017, AUD-018 +
acknowledge/sign-off, OFI does not block, `FindingRaised` payload, AUD-020);
`tests/NT.QAMS.Application.UnitTests/AuditManagement/FindingToNcPolicyTests.cs` (saga + idempotency + OFI no-op);
`tests/NT.QAMS.Domain.UnitTests/ComplianceLedger/AuditTrailReviewTests.cs` (ATR-001, ATR-011, ATR-010, anomaly event).
Everything below that duplicates one of these is marked so in `Notes`.

---

## 2. Divergences from the commissioning brief

| # | Brief demands | Actually implemented | Verdict | Gap |
|---|---|---|---|---|
| 1 | "Audit planning / cycle creation" with a **cycle start and end date** and **overlapping-cycle** rejection | One aggregate `Audit` with a single `PlannedDate` (`DateOnly`). No cycle, no programme, no range, no overlap check anywhere in `src`. | Does not conform | GAP-AUDIT-002 |
| 2 | "Department selection" (multiple departments per cycle) | One nullable `DepartmentId` on the audit, used only for the working-scope filter. | Partially conforms | GAP-AUDIT-003 |
| 3 | "Auditor assignment" (a team) | One `LeadAuditorId : Guid`, never validated against `user_account`. | Partially conforms | GAP-AUDIT-004 |
| 4 | "Date validation, BVA on start/end" | Only `AuditTrailReview` has a two-date rule (`ATR-001`). `Audit.PlannedDate` has no validator at all - a date in 1900 or 2999 is accepted. | Does not conform | GAP-AUDIT-002, GAP-AUDIT-014 |
| 5 | "ISO clause selection" | Free-text `IsoClause` string, `varchar(30)`, no list-of-values, no catalogue table, no command validator. | Does not conform | GAP-AUDIT-005, GAP-AUDIT-020 |
| 6 | "Dynamic checklist loading" | The checklist is supplied verbatim in the `POST /api/audits` body; there is no template library and no clause-driven generator. The Angular `FormArray` is the only "dynamic" element. | Does not conform | GAP-AUDIT-006 |
| 7 | Three result values Conform / OFI / Non-Conformance | `ChecklistVerdict.Conform / Ofi / NonConform`, plus `Unanswered` as the initial value which is rejected as input (`AUD-012`). | Conforms (naming differs) | - |
| 8 | "Notes" per checklist answer | The field is named `Evidence` (`string?`, trimmed, null when whitespace, `<= 2000` at the validator, `text` in the column). There is no separate notes field and no audit-level notes. | Partially conforms | GAP-AUDIT-007 |
| 9 | "Linked-NC creation" | Implemented, but **asynchronously via the outbox**, not in the request. The API returns `200 { findingId }` with `ncId == null`; the NC and the acknowledgement appear only after `OutboxProcessor` runs. | Conforms (timing differs) | GAP-AUDIT-008 (test observability) |
| 10 | "Submission" as a distinct step | There is no submit transition. `SignOff` is the only terminal action, gated on `audits.sign`. | Partially conforms | - |
| 11 | "QM notification" on submission/sign-off | No `INotificationHandler` subscribes to `AuditScheduled` or `AuditSignedOff`; `NotificationEventPolicies` handles 10 event types, none of them from AuditManagement. | Does not conform | GAP-AUDIT-009 |
| 12 | "Report export" | `ExportsController` has `audit-trail.xlsx` (the **compliance ledger**, a different thing) and a management-review PDF. There is **no internal-audit report export**. | Does not conform | GAP-AUDIT-010 |
| 13 | "Record locking" after sign-off | Domain guard only (`AUD-020`, `AUD-019`). `qams.audit` carries no `frozen_immutability` trigger, unlike the 13 tables that do. A raw `UPDATE qams.audit SET title=... WHERE status='SignedOff'` succeeds. | Partially conforms | GAP-AUDIT-011 |
| 14 | "Reopening rules" | No `Reopen`, `Void`, `Cancel` or `Abandon` method exists on `Audit`. `SignedOff` is absorbing. | Does not conform | GAP-AUDIT-012 |
| 15 | SoD: an auditor cannot approve corrective actions for a finding they raised | **No "approve corrective action" operation exists.** `Nonconformance.PlanCapaAction` and `CompleteCapaAction` take no actor and carry no SoD guard. The only SoD checks are `Verify` -> `SOD-CAPA-002` and `ConfirmEffectiveness` -> `SOD-CAPA-001`, both comparing `actorId == RaisedBy`; because `FindingToNcPolicy` sets `RaisedBy = evt.RaisedBy`, the auditor **is** blocked from verifying and closing. | Partially conforms | GAP-AUDIT-013 |
| 16 | SoD on audit sign-off | `SignOff(actorId, at)` never compares `actorId` with `LeadAuditorId`. The lead auditor can sign off their own audit if their role holds `audits.sign`. | Does not conform | GAP-AUDIT-015 |
| 17 | Electronic signature on sign-off | `ESignatureService` is never invoked from the audit slice; no `SignatureRecord` row is written; the permission is called `audits.sign` but the action is a plain state change. | Does not conform | GAP-AUDIT-016 |
| 18 | Periodic audit-trail review | Implemented as `AuditTrailReview` with an immutability rule and a conclusion requirement. No scheduler proposes the next period; `ScheduledSweepService` does not touch it. | Partially conforms | GAP-AUDIT-017 |
| 19 | Requirement traceability | URS-001..107 contain **no requirement for internal-audit planning or execution**. Nearest: URS-018 (formal audit-trail review), URS-034 (link audit findings to NCs). | Cannot be assessed | GAP-AUDIT-001 |

---

## 3. State-transition matrix

### 3.1 `Audit` (`AuditStatus`)

Legend: `OK` = permitted, `code` = rejected with that domain code, `-` = the operation does not exist.

| From \ Operation | `AddChecklistItem` | `Start` | `AnswerChecklistItem` | `RaiseFinding` | `AcknowledgeFindingNc` | `SignOff` | `Reopen` |
|---|---|---|---|---|---|---|---|
| `Scheduled` | OK | OK when >=1 item, else `AUD-011` | `AUD-019` (409) | `AUD-019` (409) | OK (no state guard) | `AUD-019` (409) | - |
| `InProgress` | OK (no state guard beyond not-signed-off) | `AUD-010` (409) | OK | OK | OK | OK when gates pass | - |
| `SignedOff` | `AUD-020` (409) | `AUD-010` (409) | `AUD-019` (409) | `AUD-019` (409) | `AUD-020` (409) | `AUD-019` (409) | - |

Two implementation-derived oddities, both real:
* `AcknowledgeFindingNc` is guarded only by `RequireNotSignedOff`, so it is legal in `Scheduled`. In
  practice a finding cannot exist in `Scheduled` (raising one needs `InProgress`), so the branch is
  unreachable through the API - it can only be reached by the outbox saga after a `SignedOff` audit,
  which is exactly what `AUD-020` blocks. Covered by `TC-AUDIT-PATH-002`.
* `AddChecklistItem` is likewise not gated on `InProgress`, so the checklist can grow mid-audit. The
  API does not expose it (no add-item endpoint after creation), so this is a domain-only capability.
  Covered by `TC-AUDIT-UNIT-021`.

### 3.2 `AuditTrailReview` (`AuditTrailReviewStatus`)

| From \ Operation | `Complete` |
|---|---|
| `Open` | OK when conclusion non-blank, else `ATR-011` (422) |
| `Completed` | `ATR-010` (409) - absorbing state |

### 3.3 Downstream NC state (audit-derived)

`FindingToNcPolicy` calls `Nonconformance.Raise(...)` then `Submit()`, so an audit-derived NC enters the
register in status `Raised`, not `Draft` (`src/NT.QAMS.Application/AuditManagement/Policies/FindingToNcPolicy.cs:60`).

---

## 4. Decision tables

### 4.1 DT-1 - `Audit.SignOff` gate (`src/NT.QAMS.Domain/AuditManagement/Audit.cs:172-193`)

| Rule | C1 `Status == InProgress` | C2 any item `Verdict == Unanswered` | C3 count(non-`Ofi` findings with `NcId == null`) > 0 | Outcome |
|---|---|---|---|---|
| R1 | F | - | - | `AUD-019` -> **409** |
| R2 | T | T | - | `AUD-017` -> **422** |
| R3 | T | F | T | `AUD-018` -> **422**, message contains the count |
| R4 | T | F | F | `Status = SignedOff`, `SignedOffBy = actorId`, `SignedOffAtUtc = clock.UtcNow`, `AuditSignedOff` raised -> **204** |

Evaluation is short-circuit in the order C1, C2, C3 - a masked condition never surfaces. Cases:
`TC-AUDIT-DT-001..004`, condition coverage `TC-AUDIT-MCDC-001`.

### 4.2 DT-2 - `FindingToNcPolicy` dispatch (`FindingToNcPolicy.cs:32-82`)

| Rule | C1 `Grade == Ofi` | C2 NC with `SourceRef` already exists | C3 `finding.NcId == null` | Outcome |
|---|---|---|---|---|
| R1 | T | - | - | return immediately; **no NC**, finding stays `NcId == null` |
| R2 | F | F | T | create NC (`severity 4` Major / `2` Minor, `likelihood 3`), `Submit()`, then `AcknowledgeFindingNc` |
| R3 | F | T | T | reuse existing NC, `AcknowledgeFindingNc` only |
| R4 | F | T | F | full no-op (redelivery after success) |

### 4.3 DT-3 - `AuditTrailReview.Complete` (`AuditTrailReview.cs:49-75`)

| Rule | C1 `Status == Open` | C2 conclusion non-blank | C3 `anomaliesFound` | Outcome |
|---|---|---|---|---|
| R1 | F | - | - | `ATR-010` -> **409** |
| R2 | T | F | - | `ATR-011` -> **422** |
| R3 | T | T | F | Completed; volumes snapshotted; **no** domain event |
| R4 | T | T | T | Completed; `AuditTrailAnomalyFound` raised -> NC (`severity 5`, `likelihood 2`, `SourceRef "ATR:{id}"`) |

### 4.4 DT-4 - authorization for the five audit write endpoints

| Actor (seeded system role) | `POST /audits` (`audits.create`) | `/start` (none) | `/answer` (none) | `/findings` (none) | `/sign-off` (`audits.sign`) |
|---|---|---|---|---|---|
| TenantAdministrator | 201 | 204 | 204 | 200 | 204 |
| QualityManager | 201 | 204 | 204 | 200 | 204 |
| DepartmentHead | **403 `AUTHZ-403`** | 204 | 204 | 200 | **403 `AUTHZ-403`** |
| Analyst | **403 `AUTHZ-403`** | 204 | 204 | 200 | **403 `AUTHZ-403`** |
| ExternalAuditor | 403 `AUTHZ-403` (endpoint) | **403 `AUTHZ-002`** (behavior) | **403 `AUTHZ-002`** | **403 `AUTHZ-002`** | 403 `AUTHZ-403` |
| Unauthenticated | 401 | 401 | 401 | 401 | 401 |

The three unshaded `204/200` cells for DepartmentHead and Analyst are the substance of `GAP-AUDIT-019`:
an Analyst holding only `audits.view` + `audits.export` can start an audit, answer every checklist item
and raise findings - which creates Nonconformances downstream.

---

## 5. Detailed test cases

Format: one field block per case, all 28 fields, in the order fixed by the conventions file.
`Environment` values used throughout:
* **ENV-UNIT** - `dotnet test tests/NT.QAMS.Domain.UnitTests` (no database, in-memory objects).
* **ENV-APP** - `dotnet test tests/NT.QAMS.Application.UnitTests` (EF InMemory provider; no RLS, no `xmin`).
* **ENV-FUNC** - `QamsWebAppFactory` WebApplicationFactory over EF InMemory.
* **ENV-PG** - `RealDatabaseWebAppFactory` / `psql` against PostgreSQL 17 dev DB `ntqams`, env
  `QMS_ITEST_POSTGRES=Host=localhost;Database=ntqams;Username=qams_app;Password=dev-only-local`,
  each case inside a rollback transaction.
* **ENV-SPA** - `ng test --watch=false --browsers=ChromeHeadless` (Jasmine/Karma) or Playwright against
  `http://localhost:4200/t/demo-lab` with the API on `:5080`, started via `scripts/dev-up.ps1`.

---

### 5.1 Domain unit cases (`TC-AUDIT-UNIT-001` .. `TC-AUDIT-UNIT-022`)
