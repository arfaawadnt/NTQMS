# 15 — Module RISK: Risk Register, Change Control, Impartiality, Org Context, Quality Policy, Objectives, Complaints, Feedback, Management Review

**Module code:** `RISK`
**System under test:** NT.QMS **v1.51.2**, repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master`. Inspection date 2026-08-01.
**Binding conventions:** `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md` — read in full before this file. Its `[corrected 2026-08-01]` entries supersede anything older.

**This file is FRONT MATTER ONLY.** Per the split convention (`00-GROUND-TRUTH-AND-CONVENTIONS.md:192-197`) it carries the implementation inventory, divergences, state machines, decision tables, UAT scenarios, exploratory charters and the gap register. **It contains no `## 5. Detailed test cases` section by design** — detailed cases are authored into `15-module-risk-governance-cases-<A|B|C|D>.md` by separate passes. The table below is a *reservation*; a reserved range with no matching case file is a coverage hole, not a delivered case.

---

## ID reservation table

Ten aggregates are in scope, so the reservation is deliberately generous. Case authors consume from the low end of each range upward and record the actual consumed sub-range in their own file header.

| Kind | Reserved range | Reserved count | Intended content |
|---|---|---|---|
| `TC-RISK-API-nnn` | 001–090 | 90 | HTTP contract: status codes, `problem+json` bodies, exact domain codes, envelopes |
| `TC-RISK-STATE-nnn` | 001–070 | 70 | Every legal and every illegal transition of the 10 state machines in §3 |
| `TC-RISK-BVA-nnn` | 001–035 | 35 | 1–5 score boundaries, RPN 1/12/13/25, satisfaction 1–5, version ≥ 1, period ordering, field lengths |
| `TC-RISK-EP-nnn` | 001–015 | 15 | Equivalence partitions on enum parsing (`Direction`, `Type`, `Channel`, `Outcome`, `RiskLevel`), status filters |
| `TC-RISK-DT-nnn` | 001–018 | 18 | Decision tables in §4: change closure, PIR, risk closure, objective closure, complaint closure, feedback escalation |
| `TC-RISK-SEC-nnn` | 001–035 | 35 | Permission gates, the ungated surface, SoD (`SOD-QP-001`, `SOD-COI-001`), confidentiality masking, auditor deny-matrix |
| `TC-RISK-RLS-nnn` | 001–018 | 18 | Tenant isolation on the 12 tables + 3 owned child tables; composite-FK cross-tenant impossibility |
| `TC-RISK-INT-nnn` | 001–024 | 24 | Cross-aggregate: complaint→NC saga, feedback→complaint escalation, context-issue→risk link, change→risk link, KPI rollup, review pack |
| `TC-RISK-UNIT-nnn` | 001–050 | 50 | Pure domain guards reachable only below the API (validator-shadowed codes — see §2 D-12) |
| `TC-RISK-MCDC-nnn` | 001–010 | 10 | `Complaint.Close` (2 conditions), `QualityObjective.OnTarget` (direction × comparison), `FeedbackEntry.Escalate` (type × status) |
| `TC-RISK-PATH-nnn` | 001–008 | 8 | Full-lifecycle paths per aggregate |
| `TC-RISK-DF-nnn` | 001–008 | 8 | Data flow: `ResidualRpn` → `HighResidualRisk` → notification → KPI; `LinkedNcId` → `CMP-020` |
| `TC-RISK-E2E-nnn` | 001–010 | 10 | Playwright-level journeys over the 9 SPA routes |
| `TC-RISK-A11Y-nnn` | 001–006 | 6 | axe scans on the governance screens |
| `TC-RISK-OBS-nnn` | 001–006 | 6 | Outbox → `audit.audit_trail` chaining, `field_change` attribution, trace propagation |
| `TC-RISK-COMP-nnn` | 001–012 | 12 | Angular component specs (list/detail/forms) |
| `TC-RISK-UAT-nnn` | 001–010 | 10 | **Consumed in this file, §6** — the Gherkin scenarios |
| `TC-RISK-EXPL-nnn` | 001–006 | 6 | **Consumed in this file, §7** — exploratory charters (not detailed cases) |
| `GAP-RISK-nnn` | 001–024 | 24 | **Consumed in this file, §8** |

**Suggested batch split for the case files** (disjoint scope, disjoint ids — authors must honour this or ids will collide):

| Batch file | Scope | Suggested id slice |
|---|---|---|
| `…-cases-A.md` | `RiskItem` + `MitigationAction` + `ChangeRequest` + `ManagementReview` + `ReviewDecision` | API 001–030 · STATE 001–024 · BVA 001–014 · DT 001–008 · SEC 001–012 · RLS 001–006 · UNIT 001–018 · INT 001–008 |
| `…-cases-B.md` | `ConflictDeclaration` + `InterestedParty` + `ContextIssue` | API 031–050 · STATE 025–040 · BVA 015–020 · DT 009–011 · SEC 013–022 · RLS 007–012 · UNIT 019–030 · INT 009–014 |
| `…-cases-C.md` | `QualityPolicy` + `QualityObjective` + `ObjectiveProgressUpdate` | API 051–068 · STATE 041–054 · BVA 021–030 · DT 012–014 · SEC 023–028 · RLS 013–015 · UNIT 031–040 · INT 015–018 |
| `…-cases-D.md` | `Complaint` + `FeedbackEntry` + the complaint→NC saga | API 069–090 · STATE 055–070 · BVA 031–035 · DT 015–018 · SEC 029–035 · RLS 016–018 · UNIT 041–050 · INT 019–024 · MCDC 001–010 |

**Completeness statement.**
*Complete in this file:* all 10 aggregates in scope opened and read line by line; the exhaustive domain-error-code list (§1.3) is closed for this module — every `throw` site in the ten domain files and the five application slices was enumerated; all 59 logical endpoints cross-checked against `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt`; every `[RequirePermission]` gate and every **ungated** route recorded; persistence, RLS, CHECK constraints and composite keys traced to migration SQL; ten state machines and the full 5×5 RPN grid derived from source; 24 gaps raised with acceptance criteria.
*Deferred / not written as executable cases here:* electronic signature on governance approvals (does not exist — GAP-RISK-002), risk owner / risk review cadence (does not exist — GAP-RISK-019), overdue-mitigation sweeps (does not exist — GAP-RISK-014), complaint acknowledgement SLA (does not exist — GAP-RISK-018). Each is a Gap, not a test.
*Nothing in this package was executed.* Every `Result` in the companion case files must read `Not Run`.

---

## 0. Correction to ground truth

> Recorded rather than silently reconciled, per honesty rule 3 (`00-GROUND-TRUTH-AND-CONVENTIONS.md:177`). Only one factual error was found, and it is inside this module's authorization surface, so it is load-bearing for the §1.5 permission table.

**`00-GROUND-TRUTH-AND-CONVENTIONS.md:61` states "**30 permission modules** in 7 groups". Measured from source, the build has **31 modules in 8 groups**, yielding **170** permission keys.**

Proof — `src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:133-188`, the `Modules` initializer, counted by its own group comments:

| Group constant | Source lines | Modules | Names |
|---|---|---|---|
| `GroupQuality` | 136–142 | 7 | `nc`, `complaints`, `feedback`, `audits`, `objectives`, `changes`, `reviews` |
| `GroupDocuments` | 145–147 | 3 | `documents`, `quality-policy`, `records` |
| `GroupRisk` | 150–156 | 5 | `risks`, `compliance`, `conflicts`, `org-context`, `access-reviews` |
| `GroupResources` | 159–162 | 4 | `equipment`, `reference-standards`, `monitoring-points`, `suppliers` |
| `GroupPeople` | 165–168 | 4 | `competencies`, `training`, `test-authorizations`, `users` |
| `GroupAnalytical` | 171–175 | 2 | `analytical-quality`, `proficiency-testing` |
| `GroupOperations` | 178–180 | 3 | `tasks`, `notifications`, `reports` |
| `GroupAdministration` | 183–185 | 3 | `organization`, `tenant-settings`, `roles` |
| **Total** | | **31** | **8 groups** |

The eight group constants are declared at `PermissionCatalog.cs:66-75` (`GroupQuality`, `GroupDocuments`, `GroupRisk`, `GroupResources`, `GroupPeople`, `GroupAnalytical`, `GroupOperations`, `GroupAdministration`). The ground-truth sentence *lists all eight* parenthetically while asserting "7 groups" — the prose and its own enumeration disagree.

Key count from the action bundles at `PermissionCatalog.cs:106-127` (`FullRecordLifecycle` = 6, `SignedRecordLifecycle` = 7, `ReadOnlyModule` = 2, `ConfigurationModule` = 2, plus five explicit per-module arrays): 46 + 20 + 28 + 24 + 22 + 14 + 8 + 8 = **170**, materialised as `AllKeys` at `PermissionCatalog.cs:191-193`. This agrees with `docs/validation/06-Revalidation-Delta-v1.38-v1.50.md:154` ("31 modules … = 170 keys"), which is the corrected figure. **Use 31 / 8 / 170.**

**Confirmed correct in the ground truth (no correction needed), for this module's scope:**
- `RiskItem.Rpn = likelihood * impact` — `src/NT.QAMS.Domain/RiskGovernance/RiskItem.cs:76`.
- `ResidualRpn = likelihood * impact` — `RiskItem.cs:110`.
- **RPN > 12 raises `HighResidualRisk`** — `RiskItem.cs:33` (`HighResidualThreshold = 12`) and `RiskItem.cs:112-115` (strict `>`).
- A defaulted RPN of 9 is explicitly banned — `RiskItem.cs:143-149` throws `RSK-002` unless both scores are explicitly 1–5; the XML doc at `RiskItem.cs:26-29` says so in words.
- `QualityPolicy.Approve` guarded by `SOD-QP-001` — `src/NT.QAMS.Domain/Improvement/QualityPolicy.cs:78`.
- `AggregateRoot.EnsureSignerIsNotPreparer` is a **no-op when the preparer is unknown** — `src/NT.QAMS.SharedKernel/Primitives/AggregateRoot.cs:36-42`. (Consequence for this module: GAP-RISK-009.)
- Change-request route is `/api/changes`, not `/api/change-requests` — `src/NT.QAMS.WebApi/Controllers/GovernanceControllers.cs:67`.
- Endpoint gating is `[RequirePermission(module, action)]` with **144 call sites** — verified by count across `src/NT.QAMS.WebApi/Controllers/`; exactly **2** `[Authorize(Roles=…)]` occurrences remain in `src/NT.QAMS.WebApi/` (one attribute plus its `Roles.cs` companion usage), guarding the platform surface only.

---

## 1. Implementation inventory

Every claim below carries `file:line`. Anything not carrying one was not read and must be labelled `[RNV]` by case authors.

### 1.1 Aggregates in scope

| # | Aggregate | File | Interfaces | Owned children | Tenant-scoped | Branch/Dept scoped |
|---|---|---|---|---|---|---|
| 1 | `RiskItem` | `src/NT.QAMS.Domain/RiskGovernance/RiskItem.cs:31` | `AggregateRoot, ITenantScoped, IAllocatable` | `MitigationAction` (`RiskItem.cs:8`) | Yes | **Yes** |
| 2 | `ChangeRequest` | `src/NT.QAMS.Domain/RiskGovernance/ChangeAndReview.cs:13` | `AggregateRoot, ITenantScoped, IAllocatable` | — | Yes | **Yes** |
| 3 | `ManagementReview` | `src/NT.QAMS.Domain/RiskGovernance/ChangeAndReview.cs:158` | `AggregateRoot, ITenantScoped, IAllocatable` | `ReviewDecision` (`ChangeAndReview.cs:138`) | Yes | **Yes** |
| 4 | `ConflictDeclaration` | `src/NT.QAMS.Domain/RiskGovernance/ConflictDeclaration.cs:20` | `AggregateRoot, ITenantScoped` | — | Yes | No |
| 5 | `QualityPolicy` | `src/NT.QAMS.Domain/Improvement/QualityPolicy.cs:17` | `AggregateRoot, ITenantScoped` | — | Yes | No |
| 6 | `QualityObjective` | `src/NT.QAMS.Domain/Improvement/QualityObjective.cs:37` | `AggregateRoot, ITenantScoped, IAllocatable` | `ObjectiveProgressUpdate` (`QualityObjective.cs:12`) | Yes | **Yes** |
| 7 | `Complaint` | `src/NT.QAMS.Domain/Improvement/Complaint.cs:24` | `AggregateRoot, ITenantScoped, IAllocatable` | — | Yes | **Yes** |
| 8 | `FeedbackEntry` | `src/NT.QAMS.Domain/Improvement/FeedbackEntry.cs:17` | `AggregateRoot, ITenantScoped, IAllocatable` | — | Yes | **Yes** |
| 9 | `InterestedParty` | `src/NT.QAMS.Domain/Organization/OrganizationContext.cs:15` | `AggregateRoot, ITenantScoped` | — | Yes | No |
| 10 | `ContextIssue` | `src/NT.QAMS.Domain/Organization/OrganizationContext.cs:107` | `AggregateRoot, ITenantScoped` | — | Yes | No |

**Scope-filter consequence.** `AppDbContext.OnModelCreating` chooses the query filter by interface: `IAllocatable` → `ApplyTenantAndScopeFilter` (tenant **plus** branch/department), everything else → `ApplyTenantFilter` (tenant only) — `src/NT.QAMS.Infrastructure/Persistence/AppDbContext.cs:168-181`. The composed scope predicate is at `AppDbContext.cs:203-210`: a restricted user sees rows in their allowed branches **plus** rows with `BranchId == null`, likewise departments; unrestricted actors short-circuit to true. Writes are additionally guarded by `OrgScopeGuardInterceptor` → `SCOPE-001` (branch) / `SCOPE-002` (department) — `src/NT.QAMS.Infrastructure/Persistence/Interceptors/OrgScopeGuardInterceptor.cs:53-65`.
**Therefore:** conflicts, quality policy, interested parties and context issues are **tenant-wide and ignore branch/department scoping** — a branch-restricted Analyst sees every conflict declaration in the tenant. That is by design (they are not `IAllocatable`) and must be asserted positively, not treated as a leak.

### 1.2 Aggregate invariants (read, not inferred)

**`RiskItem`** — `RiskItem.cs`
- `HighResidualThreshold = 12` (`:33`); `Rpn = likelihood * impact` computed in the factory (`:76`); `ResidualRpn = likelihood * impact` (`:110`).
- `Assess` requires a non-blank title (`:62-65`) and both scores 1–5 (`:67`, validator `:143-149`); `Category` defaults to `"Operational"` when blank (`:73`).
- `AddMitigationAction` forces `Status = Mitigating` as a side effect (`:91`) — adding an action is the *only* way to leave `Identified`.
- `RecordResidualAssessment` does **not** change `Status` (`:103-116`); it raises `HighResidualRisk` only when `ResidualRpn > 12` (`:112-115`).
- `Close` requires `ResidualRpn is not null` → `RSK-005` (`:121-124`) and every action `Completed` → `RSK-006` (`:126-129`), then raises `RiskClosed` (`:132`).
- `RequireOpen()` blocks all mutation once `Closed` → `RSK-007`, an `InvalidStateTransitionException` (`:135-141`). It guards `AddMitigationAction`, `CompleteMitigationAction`, `RecordResidualAssessment` and `Close` itself.
- **No `OwnerId`, no `NextReviewDate`, no re-open** on the aggregate (whole-file read).

**`ChangeRequest`** — `ChangeAndReview.cs`
- `Propose` requires title (`:46-49`, `CHG-001`) and impact analysis (`:51-54`, `CHG-002`); `ProposedBy` is stamped from `ICurrentUser` in the handler (`src/NT.QAMS.Application/RiskGovernance/RiskGovernanceSlice.cs:173-174`).
- **Load-bearing invariant:** `Approve` throws `CHG-012` when `RiskItemId is null` (`:75-78`) — risk-based thinking, ISO 9001 §6.1. It does **not** check that the linked risk is open, nor that the approver differs from the proposer (whole method read, `:72-84`).
- `Reject` requires a reason → `CHG-014` (`:89-93`); `Rejected` is terminal (no method accepts it as `expected`).
- `Close` accepts `implementationNotes` and tolerates null at the domain level (`:101` uses `?.Trim() ?? string.Empty`) — the API validator forbids empty (`RiskGovernanceSlice.cs:202-208`).
- `RecordPostImplementationReview` requires state `Closed` → `CHG-020` (`:113`), non-blank notes → `CHG-021` (`:115-118`), sets `Status = Reviewed` and raises `ChangePostImplementationReviewed` (`:119-124`). `Reviewed` is terminal.
- `Require(expected, code, action)` throws `InvalidStateTransitionException` → HTTP 409 (`:127-133`).

**`ManagementReview`** — `ChangeAndReview.cs`
- `Schedule` requires a title → `MRV-001` (`:185-188`); `Participants` tolerates null → `string.Empty` (`:195`), but the API validator requires non-empty ≤ 2000 (`RiskGovernanceSlice.cs:329-336`).
- `AddDecision` requires state `Scheduled` and non-blank description → `MRV-002` (`:200-211`).
- `Close` requires non-blank minutes → `MRV-003` (`:216-219`), stamps `ClosedBy`, raises `ReviewClosed` carrying `_decisions.Count` (`:224`).
- `RequireScheduled` → `MRV-004` `InvalidStateTransitionException`, message "closed minutes are immutable" (`:227-234`). **No SoD** between the chair who closes and the scheduler.

**`ConflictDeclaration`** — `ConflictDeclaration.cs`
- `Declare` requires description **and** related party → `COI-001` (`:47-50`).
- `Assess` requires state `Declared` → `COI-010` (`:65-68`); **SoD: `assessorId == DeclarantId` → `SOD-COI-001`** (`:70-73`); mitigation required → `COI-011` (`:75-78`). Raises `HighImpartialityRiskDeclared` **only** for `ConflictRiskLevel.High` (`:85-88`).
- The SoD check compares against `DeclarantId`, **not** `CreatedByUserId` — unlike `EnsureSignerIsNotPreparer` it is *not* a no-op on legacy rows.
- `Close` requires state `Assessed` → `COI-012` (`:91-96`) and a closure note → `COI-013` (`:98-101`). Declarations are never deleted (no delete path exists; no DELETE route in the surface).

**`QualityPolicy`** — `QualityPolicy.cs`
- `Draft` requires a statement → `QP-001` (`:36-39`) and `version >= 1` → `QP-002` (`:41-44`).
- `ReviseDraft` only in `Draft` → `QP-012` (`:58-61`); re-checks `QP-001` (`:63-66`).
- `Approve` calls `EnsureSignerIsNotPreparer(approverId, "SOD-QP-001")` **first** (`:78`), *then* checks `Status == Draft` → `QP-010` (`:80-84`). Ordering matters: a self-approval attempt on an already-Active policy surfaces `SOD-QP-001` (422), not `QP-010` (409).
- `Supersede` only from `Active` → `QP-011` (`:94-99`).
- **One-in-force is enforced in the handler, not the aggregate:** `QualityPolicyWorkflowHandlers.Handle(ApproveQualityPolicyCommand)` loads every other `Active` policy and calls `Supersede()` on each before approving — `src/NT.QAMS.Application/Improvement/QualityPolicySlice.cs:74-83`. There is no DB partial-unique index backing it.
- **Version is derived, not supplied:** `DraftQualityPolicyHandler` reads the tenant's max `Version` and adds 1 — `QualityPolicySlice.cs:41-49`. Therefore `QP-002` is unreachable through the API.

**`QualityObjective`** — `QualityObjective.cs`
- `Define` requires title **and** metric → `OBJ-001` (`:73-76`) and `periodEnd > periodStart` → `OBJ-002` (`:78-81`, strict `<=` rejection).
- `CurrentValue` = value of the update with the greatest `MeasuredOn` (`:100-101`) — ties are resolved by whatever order `OrderByDescending` yields, i.e. unspecified.
- `OnTarget` is null before any measurement; otherwise `AtLeast → current >= target`, `AtMost → current <= target` (`:104-106`).
- `RecordProgress` requires `Active` → `OBJ-010` (`:110-113`); updates are append-only (no edit/delete method exists).
- `CloseAsAchieved` requires `OnTarget == true` → `OBJ-011` (`:125-129`) — an objective cannot be declared achieved against the evidence. `CloseAsMissed` / `Cancel` route through `CloseWithRequiredNote` (`:134-142`).
- `Close(status, note)` requires a note → `OBJ-012` (`:146-149`); `RequireActive` → `OBJ-013` `InvalidStateTransitionException` (`:155-161`).
- Handler maps the outcome string case-insensitively; anything but `achieved|missed|cancelled` → `OBJ-014` — `src/NT.QAMS.Application/Improvement/QualityObjectiveSlice.cs:94-100`.

**`Complaint`** — `Complaint.cs`
- `Log` requires complainant name → `CMP-001` (`:59-62`) and subject + description → `CMP-002` (`:64-67`); raises `ComplaintLogged` (`:82`).
- `Acknowledge` from `Logged` → `CMP-010` (`:88`), stamps `AcknowledgedAtUtc`, raises `ComplaintAcknowledged` (`:91`).
- `RecordValidationVerdict(justified, reason)` from `Acknowledged` → `CMP-011` (`:101`); reason required → `CMP-003` (`:102-105`). **Justified → `Validated` + `ComplaintValidated` event; unjustified → `Invalid`, terminal** (`:108-116`).
- `StartInvestigation` from `Validated` → `CMP-012` (`:121`); `LogOutcome` from `Investigating` → `CMP-013` (`:127`), outcome required → `CMP-004` (`:129-132`); `Resolve` from `OutcomeLogged` → `CMP-014` (`:139`), resolution required → `CMP-005` (`:141-144`), raises `ComplaintResolved` (`:147`).
- `Close(linkedNcClosed)` from `Resolved` → `CMP-015` (`:157`); **`CMP-020` when `LinkedNcId is not null && !linkedNcClosed`** (`:158-161`). The gate value is computed transactionally by the handler — `src/NT.QAMS.Application/Improvement/Commands/ComplaintCommands.cs:146-148`.
- `LinkNc` is idempotent by `??=` (`:170`) — the first NC wins; a second saga run cannot rebind.
- `Confidential` drives masking at the **query** boundary, not the aggregate (see §1.5).

**`FeedbackEntry`** — `FeedbackEntry.cs`
- `Log` requires subject + details → `FBK-001` (`:53-56`), source + channel → `FBK-002` (`:58-61`), and `satisfactionScore is < 1 or > 5` → `FBK-003` (`:63-66`). **`null` passes** — the pattern is false for null, so the score stays optional.
- `Review` only from `Logged` → `FBK-010` (`:85-88`); notes required → `FBK-011` (`:90-93`).
- `Close` only from `Reviewed` → `FBK-012` (`:101-104`); action summary required → `FBK-013` (`:106-109`).
- `Escalate(complaintId)`: **type must be `Dissatisfaction`** → `FBK-014` (`:118-121`, a `DomainException` → 422); status must not be `Closed` or `Escalated` → `FBK-015` (`:123-126`, an `InvalidStateTransitionException` → 409). **`Logged` and `Reviewed` may both escalate.** Terminal here.
- Ordering note: the type check runs **before** the status check, so escalating a *closed compliment* yields `FBK-014`, not `FBK-015`.

**`InterestedParty`** — `OrganizationContext.cs`
- `Register` requires name + category → `IP-001` (`:40-43`) and needs/expectations → `IP-002` (`:45-48`).
- `Revise` requires `Active` → `IP-010` `InvalidStateTransitionException` (`:67-70`) and re-checks name/category/needs → `IP-001` (`:72-76`). **Revision is in place** — history lives only in the field-change ledger.
- `Archive` is not idempotent: archiving twice → `IP-011` (`:85-92`). There is no un-archive.

**`ContextIssue`** — `OrganizationContext.cs`
- `Register` requires category + description → `CTX-001` (`:132-135`) and impact → `CTX-002` (`:137-140`).
- `Revise` requires `Active` (`RequireActive` → `CTX-010`, `:187-193`) and re-checks all three → `CTX-001` (`:157-161`).
- `LinkRisk` requires `Active` (`:169-173`); it **overwrites** any prior link and performs no de-duplication. Risk existence is checked by the handler → `RSK-404` (`src/NT.QAMS.Application/Organization/OrgContextSlice.cs:148-152`).
- `Close` requires `Active` and a resolution → `CTX-003` (`:175-185`).

### 1.3 Domain error codes — EXHAUSTIVE for this module

Every `throw` site in the ten domain files and the five application slices, enumerated. `DomainException` → **HTTP 422**; `InvalidStateTransitionException` → **HTTP 409**; any code ending `-404` → **HTTP 404**; `AUTH-*` → **401**; `AUTHZ-*` → **403** (`src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:45-80`).

| Code | Exception type → HTTP | Meaning | Thrown at |
|---|---|---|---|
| `RSK-001` | Domain → 422 | Risk title is required | `RiskItem.cs:64` |
| `RSK-002` | Domain → 422 | Likelihood and impact must each be explicitly assessed 1–5 | `RiskItem.cs:147` (from `Assess` `:67` and `RecordResidualAssessment` `:106`) |
| `RSK-003` | Domain → 422 | Mitigation description is required | `RiskItem.cs:86` |
| `RSK-004` | Domain → 422 | Mitigation action not found | `RiskItem.cs:99` |
| `RSK-005` | Domain → 422 | A residual assessment is required before closing a risk | `RiskItem.cs:123` |
| `RSK-006` | Domain → 422 | All mitigation actions must be completed before closure | `RiskItem.cs:128` |
| `RSK-007` | **Transition → 409** | A closed risk is immutable | `RiskItem.cs:139` |
| `RSK-404` | Domain → 404 | Risk not found | `RiskGovernanceSlice.cs:67`, `:140`, `:231`; `OrgContextSlice.cs:151` |
| `CHG-001` | Domain → 422 | Change title is required | `ChangeAndReview.cs:48` |
| `CHG-002` | Domain → 422 | An impact analysis is required to propose a change | `ChangeAndReview.cs:53` |
| `CHG-010` | **Transition → 409** | Cannot link a risk assessment to a change in state X | `ChangeAndReview.cs:68` |
| `CHG-011` | **Transition → 409** | Cannot approve a change in state X | `ChangeAndReview.cs:74` |
| `CHG-012` | Domain → 422 | A change cannot be approved without a linked risk assessment | `ChangeAndReview.cs:77` |
| `CHG-013` | **Transition → 409** | Cannot reject a change in state X | `ChangeAndReview.cs:88` |
| `CHG-014` | Domain → 422 | A rejection reason is required | `ChangeAndReview.cs:91` |
| `CHG-015` | **Transition → 409** | Cannot close a change in state X | `ChangeAndReview.cs:100` |
| `CHG-020` | **Transition → 409** | Cannot review a change in state X | `ChangeAndReview.cs:113` |
| `CHG-021` | Domain → 422 | Post-implementation review notes are required | `ChangeAndReview.cs:116` |
| `CHG-404` | Domain → 404 | Change request not found | `RiskGovernanceSlice.cs:222`, `:310` |
| `MRV-001` | Domain → 422 | Review title is required | `ChangeAndReview.cs:187` |
| `MRV-002` | Domain → 422 | Decision description is required | `ChangeAndReview.cs:204` |
| `MRV-003` | Domain → 422 | Minutes are required to close a management review | `ChangeAndReview.cs:218` |
| `MRV-004` | **Transition → 409** | Cannot add decisions to / close a review in state X | `ChangeAndReview.cs:232` |
| `MRV-404` | Domain → 404 | Management review not found | `RiskGovernanceSlice.cs:375`, `:389`, `:419` |
| `COI-001` | Domain → 422 | The conflict description and the related party are required | `ConflictDeclaration.cs:49` |
| `COI-010` | **Transition → 409** | Only a declared conflict can be assessed | `ConflictDeclaration.cs:67` |
| `COI-011` | Domain → 422 | A mitigation (or justification that none is needed) is required | `ConflictDeclaration.cs:77` |
| `COI-012` | **Transition → 409** | Only an assessed conflict can be closed | `ConflictDeclaration.cs:95` |
| `COI-013` | Domain → 422 | A closure note is required | `ConflictDeclaration.cs:100` |
| `SOD-COI-001` | Domain → 422 | Declarants cannot assess their own conflict | `ConflictDeclaration.cs:72` |
| `COI-404` | Domain → 404 | Conflict declaration not found | `ConflictSlice.cs:88`, `:126` |
| `QP-001` | Domain → 422 | A policy statement is required | `QualityPolicy.cs:38`, `:65` |
| `QP-002` | Domain → 422 | The policy version must be a positive number | `QualityPolicy.cs:43` |
| `QP-010` | **Transition → 409** | Only a draft policy can be approved | `QualityPolicy.cs:83` |
| `QP-011` | **Transition → 409** | Only an active policy can be superseded | `QualityPolicy.cs:98` |
| `QP-012` | **Transition → 409** | Only a draft policy can be edited | `QualityPolicy.cs:60` |
| `SOD-QP-001` | Domain → 422 | The preparer of a record cannot sign it off | raised by `AggregateRoot.cs:40`, code passed at `QualityPolicy.cs:78` |
| `QP-404` | Domain → 404 | Quality policy not found | `QualityPolicySlice.cs:89` |
| `OBJ-001` | Domain → 422 | A title and a measurable metric are required | `QualityObjective.cs:75` |
| `OBJ-002` | Domain → 422 | The objective period end must fall after its start | `QualityObjective.cs:80` |
| `OBJ-010` | **Transition → 409** | Progress can only be recorded on an active objective | `QualityObjective.cs:112` |
| `OBJ-011` | Domain → 422 | The latest measurement does not meet the target | `QualityObjective.cs:127` |
| `OBJ-012` | Domain → 422 | A closure note is required | `QualityObjective.cs:148` |
| `OBJ-013` | **Transition → 409** | The objective is already X and immutable | `QualityObjective.cs:159` |
| `OBJ-014` | Domain → 422 | The outcome must be Achieved, Missed or Cancelled | `QualityObjectiveSlice.cs:99` |
| `OBJ-404` | Domain → 404 | Quality objective not found | `QualityObjectiveSlice.cs:108` |
| `CMP-001` | Domain → 422 | The complainant name is required | `Complaint.cs:61` |
| `CMP-002` | Domain → 422 | A complaint subject and description are required | `Complaint.cs:66` |
| `CMP-003` | Domain → 422 | A validation verdict reason is required | `Complaint.cs:104` |
| `CMP-004` | Domain → 422 | An investigation outcome is required | `Complaint.cs:130` |
| `CMP-005` | Domain → 422 | A resolution is required | `Complaint.cs:142` |
| `CMP-010` | **Transition → 409** | Cannot acknowledge a complaint in state X | `Complaint.cs:88` |
| `CMP-011` | **Transition → 409** | Cannot validate a complaint in state X | `Complaint.cs:101` |
| `CMP-012` | **Transition → 409** | Cannot start investigating a complaint in state X | `Complaint.cs:121` |
| `CMP-013` | **Transition → 409** | Cannot log an outcome for a complaint in state X | `Complaint.cs:127` |
| `CMP-014` | **Transition → 409** | Cannot resolve a complaint in state X | `Complaint.cs:139` |
| `CMP-015` | **Transition → 409** | Cannot close a complaint in state X | `Complaint.cs:157` |
| `CMP-020` | Domain → 422 | The linked nonconformance must be closed before the complaint | `Complaint.cs:160` |
| `CMP-404` | Domain → 404 | Complaint not found | `ComplaintCommands.cs:156` |
| `FBK-001` | Domain → 422 | A subject and details are required | `FeedbackEntry.cs:55` |
| `FBK-002` | Domain → 422 | The feedback source and channel are required | `FeedbackEntry.cs:60` |
| `FBK-003` | Domain → 422 | The satisfaction score is on a 1–5 scale | `FeedbackEntry.cs:65` |
| `FBK-010` | **Transition → 409** | Only logged feedback can be reviewed | `FeedbackEntry.cs:87` |
| `FBK-011` | Domain → 422 | Review notes are required | `FeedbackEntry.cs:92` |
| `FBK-012` | **Transition → 409** | Only reviewed feedback can be closed | `FeedbackEntry.cs:103` |
| `FBK-013` | Domain → 422 | A summary of the action taken is required | `FeedbackEntry.cs:108` |
| `FBK-014` | Domain → 422 | Only dissatisfaction can be escalated to a complaint | `FeedbackEntry.cs:120` |
| `FBK-015` | **Transition → 409** | Closed/Escalated feedback cannot be escalated | `FeedbackEntry.cs:125` |
| `FBK-404` | Domain → 404 | Feedback entry not found | `FeedbackSlice.cs:126` |
| `IP-001` | Domain → 422 | The party name, category (and on revise, needs) are required | `OrganizationContext.cs:42`, `:74` |
| `IP-002` | Domain → 422 | The needs and expectations are required | `OrganizationContext.cs:47` |
| `IP-010` | **Transition → 409** | An archived entry is frozen — register a new one instead | `OrganizationContext.cs:69` |
| `IP-011` | **Transition → 409** | The entry is already archived | `OrganizationContext.cs:89` |
| `IP-404` | Domain → 404 | Interested party not found | `OrgContextSlice.cs:69` |
| `CTX-001` | Domain → 422 | The issue category and description (and on revise, impact) are required | `OrganizationContext.cs:134`, `:160` |
| `CTX-002` | Domain → 422 | The assessed impact on the QMS is required | `OrganizationContext.cs:139` |
| `CTX-003` | Domain → 422 | A resolution is required to close a context issue | `OrganizationContext.cs:180` |
| `CTX-010` | **Transition → 409** | The issue is closed and frozen | `OrganizationContext.cs:191` |
| `CTX-404` | Domain → 404 | Context issue not found | `OrgContextSlice.cs:168` |

**Cross-cutting codes reachable from every write in this module:**

| Code | HTTP | Source |
|---|---|---|
| `TENANT-000` | 422 | `RiskGovernanceSlice.cs:14`; also `ConflictSlice.cs:56`, `QualityPolicySlice.cs:39`, `QualityObjectiveSlice.cs:40`, `FeedbackSlice.cs:39`, `ComplaintCommands.cs:38`, `OrgContextSlice.cs:44`, `:129` |
| `AUTH-003` | **401** | `RiskGovernanceSlice.cs:17` and the same six slices — "An authenticated user is required." Note the `AUTH-` prefix routes it to 401, not 422 (`DomainExceptionHandler.cs:54-59`) |
| `SCOPE-001` / `SCOPE-002` | 422 | `OrgScopeGuardInterceptor.cs:56`, `:63` — only on the six `IAllocatable` aggregates |
| `AUTHZ-403` | **403** | `ProblemAuthorizationResultHandler.ForbiddenCode`, written by `RequirePermissionAttribute.OnAuthorizationAsync` — `src/NT.QAMS.WebApi/Authorization/RequirePermissionAttribute.cs:56-60` |
| `CONCURRENCY-409` | **409** | `DomainExceptionHandler.cs:21`, `:28-33` — `xmin` token, no `row_version` column |

**Codes NOT present anywhere in this module** (do not invent them): no `SOD-CHG-*`, no `SOD-MRV-*`, no `SOD-RSK-*`, no `SIG-*` (no e-signature on any governance action), no `CHANGE-REASON-REQUIRED` (no DELETE endpoint exists here).

### 1.4 Domain events and their consumers

| Event | Declared | Carries `TenantId` | Consumer |
|---|---|---|---|
| `HighResidualRisk(RiskId, RiskRef, Title, ResidualRpn, TenantId)` | `RiskItem.cs:152-153` | Yes | `NotificationEventPolicies.Handle` → key `RISK_HIGH_RESIDUAL` — `src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:101-107`; default rule seeded at `:150-151` |
| `RiskClosed(RiskId, RiskRef, ResidualRpn, TenantId)` | `RiskItem.cs:155` | Yes | **None** — outbox + audit ledger only |
| `ChangeApproved(ChangeId, ChangeRef, Title, ApprovedBy, TenantId)` | `ChangeAndReview.cs:237-238` | Yes | **None** |
| `ChangePostImplementationReviewed(ChangeId, ChangeRef, Effective, ReviewedBy, TenantId)` | `ChangeAndReview.cs:240-241` | Yes | **None** |
| `ReviewClosed(ReviewId, ReviewRef, ClosedBy, DecisionCount, TenantId)` | `ChangeAndReview.cs:243-244` | Yes | **None** |
| `HighImpartialityRiskDeclared(ConflictId, ConflictRef, DeclarantId, RelatedParty, Mitigation, TenantId)` | `ConflictDeclaration.cs:109-111` | Yes | `NotificationEventPolicies.Handle` → key `COI_HIGH` — `NotificationPolicies.cs:82-87`. **No default rule is seeded** for `COI_HIGH` (the seed array at `:138-156` omits it) |
| `QualityPolicyApproved(PolicyId, PolicyRef, Version, ApprovedBy, TenantId)` | `QualityPolicy.cs:105-106` | Yes | **None** |
| `ComplaintLogged(ComplaintId, ComplaintRef, Subject, Channel)` | `Complaint.cs:182-183` | **No** | **None** |
| `ComplaintAcknowledged(ComplaintId, ComplaintRef)` | `Complaint.cs:185` | **No** | **None** |
| `ComplaintValidated(ComplaintId, ComplaintRef, Subject, Description, LoggedBy, TenantId)` | `Complaint.cs:188-190` | Yes | **`ComplaintToNcPolicy`** — `src/NT.QAMS.Application/Improvement/ComplaintToNcPolicy.cs:21-58` |
| `ComplaintResolved(ComplaintId, ComplaintRef)` | `Complaint.cs:192` | **No** | **None** |
| `ComplaintClosed(ComplaintId, ComplaintRef)` | `Complaint.cs:194` | **No** | **None** |
| `QualityObjective`, `FeedbackEntry`, `InterestedParty`, `ContextIssue` | — | — | **Raise no domain events at all** (whole-file reads) |

**Every raised event still reaches the tamper-evident trail** regardless of whether a handler exists: `OutboxProcessor` publishes the notification and then chains the row into `audit.audit_trail` in the same `SaveChanges` — `src/NT.QAMS.Infrastructure/Persistence/Outbox/OutboxProcessor.cs:122-129`. The processor elevates (`ICurrentTenantSetter.Elevate()`, `:102`) because it chains rows for many tenants in one batch.

**`ComplaintToNcPolicy` mechanics** (`ComplaintToNcPolicy.cs`):
- Sets the tenant from the event before touching the DB (`:26`) — it runs in a background scope.
- Idempotency key is `SourceRef = "CMP:{ComplaintRef}"` (`:28-30`); a second run finds the existing NC and only heals the back-link (`:34-39`).
- Creates the NC with **hard-coded `severity: 3, likelihood: 3`** and `NcSourceType.Complaint`, title `"Justified complaint {ref}: {subject}"` (`:41-50`), then `Submit()`s it (`:52`).
- Back-links via `complaint.LinkNc(nc.Id)` (`:55`) — which is `??=`, so idempotent.

### 1.5 Endpoints and permission gates

**59 logical endpoints** across 9 controllers (**118 routes** including the `/api/v{version}/…` mirror). Verified line-for-line against `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` (lines 36–37, 40–41, 48–49, 65–66, 77–78, 88–89, 99–102, 111–112, 268–273, 278–284, 287–289, 312–315, 334–336, 360–364, 384–388, 397–401, 630–631, 633).

Every controller class carries `[ApiController]` + `[Authorize]`. A blank **Gate** cell means **`[Authorize]` only — no permission is required beyond being authenticated.**

**`RisksController`** — `src/NT.QAMS.WebApi/Controllers/GovernanceControllers.cs:12-64`, route `api/risks`

| Method | Route | Gate | Command/Query | Line |
|---|---|---|---|---|
| GET | `/api/risks?status&page&pageSize` | *(none)* | `GetRisksQuery` → `PagedResponse<RiskListItemDto>` | `:17-22` |
| GET | `/api/risks/{id}` | *(none)* | `GetRiskByIdQuery` | `:24-26` |
| POST | `/api/risks` → 201 | *(none)* | `AssessRiskCommand` | `:28-35` |
| POST | `/api/risks/{id}/actions` → 200 `{actionId}` | *(none)* | `AddMitigationCommand` | `:37-40` |
| POST | `/api/risks/{id}/actions/{actionId}/complete` → 204 | *(none)* | `CompleteMitigationCommand` | `:42-47` |
| POST | `/api/risks/{id}/residual` → 204 | **`risks.approve`** | `RecordResidualCommand` | `:49-55` |
| POST | `/api/risks/{id}/close` → 204 | **`risks.void`** | `CloseRiskCommand` | `:57-63` |

**`ChangeRequestsController`** — `GovernanceControllers.cs:66-128`, route `api/changes`

| Method | Route | Gate | Command/Query | Line |
|---|---|---|---|---|
| GET | `/api/changes?status&page&pageSize` | *(none)* | `GetChangesQuery` → paged | `:71-76` |
| GET | `/api/changes/{id}` | *(none)* | `GetChangeByIdQuery` | `:78-80` |
| POST | `/api/changes` → 201 | *(none)* | `ProposeChangeCommand` | `:82-88` |
| POST | `/api/changes/{id}/risk` → 204 | *(none)* | `LinkRiskCommand` | `:90-95` |
| POST | `/api/changes/{id}/approve` → 204 | **`changes.approve`** | `ApproveChangeCommand` | `:97-103` |
| POST | `/api/changes/{id}/reject` → 204 | **`changes.void`** | `RejectChangeCommand` | `:105-111` |
| POST | `/api/changes/{id}/close` → 204 | *(none)* | `CloseChangeCommand` | `:113-118` |
| POST | `/api/changes/{id}/review` → 204 | **`changes.approve`** | `ReviewChangeCommand` | `:120-127` |

**`ManagementReviewsController`** — `GovernanceControllers.cs:130-168`, route `api/management-reviews`

| Method | Route | Gate | Command/Query | Line |
|---|---|---|---|---|
| GET | `/api/management-reviews?page&pageSize` | *(none)* | `GetReviewsQuery` → paged | `:135-139` |
| GET | `/api/management-reviews/{id}` | *(none)* | `GetReviewByIdQuery` | `:141-143` |
| POST | `/api/management-reviews` → 201 | **`reviews.create`** | `ScheduleReviewCommand` | `:145-153` |
| POST | `/api/management-reviews/{id}/decisions` → 200 `{decisionId}` | **`reviews.edit`** | `AddDecisionCommand` | `:155-159` |
| POST | `/api/management-reviews/{id}/close` → 204 | **`reviews.void`** | `CloseReviewCommand` | `:161-167` |

**`ConflictsController`** — `src/NT.QAMS.WebApi/Controllers/ConflictsController.cs`, route `api/conflicts`

| Method | Route | Gate | Command/Query | Line |
|---|---|---|---|---|
| GET | `/api/conflicts?status` | *(none)* | `GetConflictsQuery` → **unpaged list** | `:17-19` |
| GET | `/api/conflicts/{id}` | *(none)* | `GetConflictByIdQuery` | `:21-23` |
| POST | `/api/conflicts` → 201 | *(none)* | `DeclareConflictCommand` | `:25-31` |
| POST | `/api/conflicts/{id}/assess` → 204 | **`conflicts.approve`** | `AssessConflictCommand` | `:33-39` |
| POST | `/api/conflicts/{id}/close` → 204 | **`conflicts.void`** | `CloseConflictCommand` | `:41-47` |

**`OrgContextController`** — `src/NT.QAMS.WebApi/Controllers/OrgContextController.cs`, route `api/org-context`

| Method | Route | Gate | Command/Query | Line |
|---|---|---|---|---|
| GET | `/api/org-context/interested-parties` | *(none)* | `GetInterestedPartiesQuery` → **unpaged** | `:19-21` |
| POST | `/api/org-context/interested-parties` → **200** `{id}` | **`org-context.create`** | `RegisterInterestedPartyCommand` | `:23-31` |
| PUT | `/api/org-context/interested-parties/{id}` → 204 | **`org-context.edit`** | `ReviseInterestedPartyCommand` | `:33-41` |
| POST | `/api/org-context/interested-parties/{id}/archive` → 204 | **`org-context.void`** | `ArchiveInterestedPartyCommand` | `:43-49` |
| GET | `/api/org-context/issues` | *(none)* | `GetContextIssuesQuery` → **unpaged** | `:53-55` |
| POST | `/api/org-context/issues` → **200** `{id}` | **`org-context.create`** | `RegisterContextIssueCommand` | `:57-64` |
| PUT | `/api/org-context/issues/{id}` → 204 | **`org-context.edit`** | `ReviseContextIssueCommand` | `:66-73` |
| POST | `/api/org-context/issues/{id}/link-risk` → 204 | **`org-context.edit`** | `LinkContextIssueRiskCommand` | `:75-81` |
| POST | `/api/org-context/issues/{id}/close` → 204 | **`org-context.void`** | `CloseContextIssueCommand` | `:83-89` |

> Note the create verbs here return **200 with a body**, not `201 Created` — unlike risks/changes/reviews/objectives/complaints/feedback which use `CreatedAtAction`. Assert 200, not 201.

**`QualityPolicyController`** — `src/NT.QAMS.WebApi/Controllers/QualityPolicyController.cs`, route `api/quality-policy`

| Method | Route | Gate | Command/Query | Line |
|---|---|---|---|---|
| GET | `/api/quality-policy/active` → **200 or 204** | *(none — deliberately readable by any authenticated user)* | `GetActiveQualityPolicyQuery` | `:23-28` |
| GET | `/api/quality-policy` | **`quality-policy.view`** | `GetQualityPoliciesQuery` → unpaged | `:31-34` |
| POST | `/api/quality-policy` → **200** `{id}` | **`quality-policy.create`** | `DraftQualityPolicyCommand` | `:36-39` |
| PUT | `/api/quality-policy/{id}` → 204 | **`quality-policy.edit`** | `ReviseQualityPolicyCommand` | `:41-47` |
| POST | `/api/quality-policy/{id}/approve` → 204 | **`quality-policy.approve`** | `ApproveQualityPolicyCommand` | `:49-55` |

> `GET /active` returning **204 No Content** when no policy has been approved is the only 204-on-read in this module (`:27`).

**`QualityObjectivesController`** — `src/NT.QAMS.WebApi/Controllers/QualityObjectivesController.cs`, route `api/quality-objectives`

| Method | Route | Gate | Command/Query | Line |
|---|---|---|---|---|
| GET | `/api/quality-objectives?status` | *(none)* | `GetQualityObjectivesQuery` → unpaged | `:17-19` |
| GET | `/api/quality-objectives/{id}` | *(none)* | `GetQualityObjectiveByIdQuery` | `:21-23` |
| POST | `/api/quality-objectives` → 201 | **`objectives.create`** | `DefineQualityObjectiveCommand` | `:25-34` |
| POST | `/api/quality-objectives/{id}/progress` → 200 `{updateId}` | *(none)* | `RecordObjectiveProgressCommand` | `:36-42` |
| POST | `/api/quality-objectives/{id}/close` → 204 | **`objectives.void`** | `CloseObjectiveCommand` | `:44-50` |

**`ComplaintsController`** — `src/NT.QAMS.WebApi/Controllers/ComplaintsController.cs`, route `api/complaints`

| Method | Route | Gate | Command/Query | Line |
|---|---|---|---|---|
| GET | `/api/complaints?status` | *(none)* | `GetComplaintsQuery(status, CanViewConfidential)` | `:21-23` |
| GET | `/api/complaints/{id}` | *(none)* | `GetComplaintByIdQuery(id, CanViewConfidential)` | `:25-27` |
| POST | `/api/complaints` → 201 | *(none)* | `LogComplaintCommand` | `:29-38` |
| POST | `/api/complaints/{id}/acknowledge` → 204 | **`complaints.edit`** | `AcknowledgeComplaintCommand` | `:40-46` |
| POST | `/api/complaints/{id}/validate` → 204 | **`complaints.approve`** | `ValidateComplaintCommand` | `:48-54` |
| POST | `/api/complaints/{id}/start-investigation` → 204 | **`complaints.edit`** | `StartComplaintInvestigationCommand` | `:56-62` |
| POST | `/api/complaints/{id}/outcome` → 204 | **`complaints.edit`** | `LogComplaintOutcomeCommand` | `:64-70` |
| POST | `/api/complaints/{id}/resolve` → 204 | **`complaints.edit`** | `ResolveComplaintCommand` | `:72-78` |
| POST | `/api/complaints/{id}/close` → 204 | **`complaints.void`** | `CloseComplaintCommand` | `:80-86` |

**Confidentiality masking is decided by the legacy tier claim, not the permission catalogue:**
`private bool CanViewConfidential => User.IsInRole("QualityManager") || User.IsInRole("TenantAdmin");` — `ComplaintsController.cs:18-19`. The role claim is still minted from the `UserRole` enum at `src/NT.QAMS.Infrastructure/Security/SecurityAdapters.cs:88` (`new(ClaimTypes.Role, user.Role.ToString())`) and bound as the role claim type at `src/NT.QAMS.WebApi/Program.cs:118`. Masking replaces the name with `•••` and nulls the contact — `ComplaintCommands.cs:182` (list) and `:202-206` (detail). **There is no `complaints.view-confidential` permission key** (see GAP-RISK-005).

**`FeedbackController`** — `src/NT.QAMS.WebApi/Controllers/FeedbackController.cs`, route `api/feedback`

| Method | Route | Gate | Command/Query | Line |
|---|---|---|---|---|
| GET | `/api/feedback?status&type` | *(none)* | `GetFeedbackQuery` → unpaged | `:17-20` |
| GET | `/api/feedback/{id}` | *(none)* | `GetFeedbackByIdQuery` | `:22-24` |
| POST | `/api/feedback` → 201 | *(none)* | `LogFeedbackCommand` | `:26-33` |
| POST | `/api/feedback/{id}/review` → 204 | **`feedback.edit`** | `ReviewFeedbackCommand` | `:35-41` |
| POST | `/api/feedback/{id}/close` → 204 | **`feedback.void`** | `CloseFeedbackCommand` | `:43-49` |
| POST | `/api/feedback/{id}/escalate` → 200 `{complaintId}` | **`feedback.edit`** | `EscalateFeedbackCommand` | `:51-58` |

**Adjacent endpoint owned elsewhere but exercising this module:** `GET /api/exports/review-pack/{reviewId}.pdf`, gated **`reviews.export`** — `src/NT.QAMS.WebApi/Controllers/ExportsController.cs:125-127`. It composes `GetReviewByIdQuery` + `GetDashboardKpisQuery` + `GetNcParetoQuery` into a QuestPDF pack including the live **High Residual Risks** KPI (`:132-146`).

**Gate coverage summary for this module:**

| | Count |
|---|---|
| Logical endpoints | **59** |
| Carrying `[RequirePermission]` | **26** |
| `[Authorize]` only — no permission required | **33** |
| Of those, **write** (POST/PUT) endpoints with no permission gate | **10** — `POST /api/risks`, `/api/risks/{id}/actions`, `/api/risks/{id}/actions/{actionId}/complete`, `POST /api/changes`, `/api/changes/{id}/risk`, `/api/changes/{id}/close`, `POST /api/conflicts`, `POST /api/complaints`, `POST /api/feedback`, `POST /api/quality-objectives/{id}/progress` |
| Of those, **read** endpoints with no permission gate | **17** of the 18 GETs (all but `GET /api/quality-policy`) |

Those 33 are not unauthenticated — `[Authorize]` still applies, and every command additionally carries `[RequireInternalActor]` (§1.6), so the read-only `ExternalAuditor` cannot write. But the catalogued `*.view` / `*.create` / `*.edit` keys for this module have **no HTTP enforcement point**. See GAP-RISK-003 and GAP-RISK-004.

**Permission keys defined for this module and where they are consumed:**

| Key | Consumed at | Key | Consumed at |
|---|---|---|---|
| `risks.view` | **nowhere** | `objectives.view` | **nowhere** |
| `risks.create` | **nowhere** | `objectives.create` | `QualityObjectivesController.cs:26` |
| `risks.edit` | **nowhere** | `objectives.edit` | **nowhere** |
| `risks.approve` | `GovernanceControllers.cs:50` | `objectives.approve` | **nowhere** |
| `risks.void` | `GovernanceControllers.cs:58` | `objectives.void` | `QualityObjectivesController.cs:45` |
| `risks.export` | **nowhere** | `objectives.export` | **nowhere** |
| `changes.view/create/edit/sign/export` | **nowhere** | `complaints.view/create/export` | **nowhere** |
| `changes.approve` | `GovernanceControllers.cs:98`, `:122` | `complaints.edit` | `ComplaintsController.cs:41`, `:57`, `:65`, `:73` |
| `changes.void` | `GovernanceControllers.cs:106` | `complaints.approve` | `ComplaintsController.cs:49` |
| `reviews.view/edit… ` see next | | `complaints.void` | `ComplaintsController.cs:81` |
| `reviews.create` | `GovernanceControllers.cs:146` | `feedback.view/create/approve/export` | **nowhere** |
| `reviews.edit` | `GovernanceControllers.cs:156` | `feedback.edit` | `FeedbackController.cs:36`, `:52` |
| `reviews.void` | `GovernanceControllers.cs:162` | `feedback.void` | `FeedbackController.cs:44` |
| `reviews.export` | `ExportsController.cs:126` | `conflicts.view/create/edit/export` | **nowhere** |
| `reviews.view / .approve / .sign` | **nowhere** | `conflicts.approve` | `ConflictsController.cs:34` |
| `quality-policy.view` | `QualityPolicyController.cs:32` | `conflicts.void` | `ConflictsController.cs:42` |
| `quality-policy.create` | `QualityPolicyController.cs:37` | `org-context.view` | **nowhere** |
| `quality-policy.edit` | `QualityPolicyController.cs:42` | `org-context.create` | `OrgContextController.cs:24`, `:58` |
| `quality-policy.approve` | `QualityPolicyController.cs:50` | `org-context.edit` | `OrgContextController.cs:34`, `:67`, `:76` |
| `quality-policy.void / .sign / .export` | **nowhere** | `org-context.void` | `OrgContextController.cs:44`, `:84` |

**`.sign` keys exist for `changes`, `reviews` and `quality-policy`** (they are `SignedRecordLifecycle` modules — `PermissionCatalog.cs:141`, `:142`, `:146`) **but no endpoint in the build consumes them**; a repository-wide search for `PermissionAction.Sign` in `src/NT.QAMS.WebApi/Controllers/` returns only analytical-quality, audits and documents. See GAP-RISK-002.

**Seeded system-role grants for this module** — `src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs`:

| Module | TenantAdministrator | QualityManager | DepartmentHead | Analyst | ExternalAuditor |
|---|---|---|---|---|---|
| `risks` | all 6 | all 6 | View, Create, Edit, Export (`:132`) | View, Create, Edit, Export (`:165`) | View, Export |
| `changes` | all 7 | all 7 | View, Create, Edit, Export (`:127`) | View, Create, Edit, Export (`:160`) | View, Export |
| `reviews` | all 7 | all 7 | View, Export (`:128`) | View, Export (`:161`) | **View only** (`:190`) |
| `conflicts` | all 6 | all 6 | View, Create, Edit, Export (`:133`) | View, Create, Edit, Export (`:166`) | View, Export |
| `org-context` | all 5 | all 5 | View, Create, Edit, **Void**, Export (`:134`) | View, Export (`:167`) | View, Export |
| `quality-policy` | all 7 | all 7 | **none** | **none** | **none** (`:188`) |
| `objectives` | all 6 | all 6 | View, Create, Edit, Export (`:126`) | View, **Edit**, Export (`:159`) | View, Export |
| `complaints` | all 6 | all 6 | View, Create, Edit, Export (`:124`) | View, Create, Export (`:157`) | View, Export |
| `feedback` | all 6 | all 6 | View, Create, Edit, **Void**, Export (`:125`) | View, Create, Export (`:158`) | View, Export |

TenantAdministrator receives `PermissionCatalog.AllKeys` (`:99`). QualityManager receives everything except `users`, `tenant-settings`, `roles` (view only) and `organization.manage` (`:107-117`) — so QM holds every key in this module. ExternalAuditor is read-only and is explicitly denied `quality-policy` and `access-reviews` entirely (`:188`), and limited to `reviews.view` (`:190`).
**Documented parity concession relevant here:** the XML doc at `SystemRoleCatalog.cs:88-92` records that "a Department Head may now archive an interested party because closing context issues and archiving parties share `org-context.void`" — a deliberate widening, not a defect.

### 1.6 Command policies and validators

Every command in the module carries exactly one `CommandPolicyAttribute`, and in all cases it is **`[RequireInternalActor]`** — i.e. every authenticated role except `ExternalAuditor` (`src/NT.QAMS.Application/Abstractions/CommandAuthorization.cs:14-19`). **No command in this module uses `[RequirePermissionPolicy]`.** The merge gate `tests/NT.QAMS.Architecture.Tests/CommandPolicyTests.cs:26-38` fails CI if any command lacks exactly one policy attribute.

Declaration sites: `RiskGovernanceSlice.cs:22, 53, 56, 58, 60, 153, 183, 185, 187, 198, 209, 323, 354, 357`; `ConflictSlice.cs:13, 16, 18`; `QualityPolicySlice.cs:13, 15, 17`; `QualityObjectiveSlice.cs:13, 54, 57`; `FeedbackSlice.cs:13, 54, 56, 58`; `ComplaintCommands.cs:13, 57, 59, 61, 63, 74, 85`; `OrgContextSlice.cs:13, 17, 21, 88, 91, 94, 96`.

FluentValidation rules (a failure is **400** with an `errors` dictionary, *not* 422 — `DomainExceptionHandler.cs:34-44`):

| Command | Rules | File:line |
|---|---|---|
| `AssessRiskCommand` | `Title` NotEmpty ≤300; `Likelihood` 1–5; `Impact` 1–5 | `RiskGovernanceSlice.cs:27-35` |
| `AddMitigationCommand` | **no validator** | — |
| `CompleteMitigationCommand` | **no validator** | — |
| `RecordResidualCommand` | **no validator** | — |
| `CloseRiskCommand` | **no validator** | — |
| `LinkRiskCommand` | **no validator** | — |
| `ProposeChangeCommand` | `Title` NotEmpty ≤300; `ImpactAnalysis` NotEmpty ≤4000 | `RiskGovernanceSlice.cs:157-164` |
| `RejectChangeCommand` | `Reason` NotEmpty ≤1000 | `RiskGovernanceSlice.cs:191-197` |
| `CloseChangeCommand` | `ImplementationNotes` NotEmpty ≤4000 | `RiskGovernanceSlice.cs:202-208` |
| `ReviewChangeCommand` | `Notes` NotEmpty ≤4000 | `RiskGovernanceSlice.cs:212-216` |
| `ScheduleReviewCommand` | `Title` NotEmpty ≤300; `Participants` NotEmpty ≤2000 | `RiskGovernanceSlice.cs:329-336` |
| `AddDecisionCommand` | **no validator** | — |
| `CloseReviewCommand` | `Minutes` NotEmpty ≤20000 | `RiskGovernanceSlice.cs:361-367` |
| `DeclareConflictCommand` | `DeclarantId` NotEmpty; `Description` NotEmpty ≤2000; `RelatedParty` NotEmpty ≤300 | `ConflictSlice.cs:30-38` |
| `AssessConflictCommand` | `RiskLevel` NotEmpty; `Mitigation` NotEmpty ≤2000 | `ConflictSlice.cs:40-47` |
| `CloseConflictCommand` | `ClosureNote` NotEmpty ≤2000 | `ConflictSlice.cs:22-28` |
| `DraftQualityPolicyCommand` | `Statement` NotEmpty ≤8000 | `QualityPolicySlice.cs:20-24` |
| `ReviseQualityPolicyCommand` | `Statement` NotEmpty ≤8000 | `QualityPolicySlice.cs:26-30` |
| `ApproveQualityPolicyCommand` | **no validator** | — |
| `DefineQualityObjectiveCommand` | `Title` NotEmpty ≤300; `Description` ≤2000; `Metric` NotEmpty ≤300; `Unit` ≤30; `Direction` NotEmpty; `OwnerId` NotEmpty | `QualityObjectiveSlice.cs:20-31` |
| `RecordObjectiveProgressCommand` | `Comment` ≤1000 | `QualityObjectiveSlice.cs:69-75` |
| `CloseObjectiveCommand` | `Note` NotEmpty ≤2000 | `QualityObjectiveSlice.cs:61-67` |
| `LogComplaintCommand` | `ComplainantName` NotEmpty ≤300; `ComplainantContact` ≤300; `Subject` NotEmpty ≤300; `Description` NotEmpty ≤4000 | `ComplaintCommands.cs:19-28` |
| `ValidateComplaintCommand` | `Reason` NotEmpty ≤2000 | `ComplaintCommands.cs:88-94` |
| `LogComplaintOutcomeCommand` | `Outcome` NotEmpty ≤4000 | `ComplaintCommands.cs:67-73` |
| `ResolveComplaintCommand` | `Resolution` NotEmpty ≤4000 | `ComplaintCommands.cs:78-84` |
| `Acknowledge/StartInvestigation/CloseComplaintCommand` | **no validator** | — |
| `LogFeedbackCommand` | `Source` NotEmpty ≤100; `Channel` NotEmpty ≤100; `Type` NotEmpty; `Subject` NotEmpty ≤300; `Details` NotEmpty ≤4000; `SatisfactionScore` 1–5 **when not null** | `FeedbackSlice.cs:19-30` |
| `ReviewFeedbackCommand` | `ReviewNotes` NotEmpty ≤2000 | `FeedbackSlice.cs:61-67` |
| `CloseFeedbackCommand` | `ActionSummary` NotEmpty ≤2000 | `FeedbackSlice.cs:69-75` |
| `EscalateFeedbackCommand` | **no validator** | — |
| `RegisterInterestedPartyCommand` | `Name` NotEmpty ≤200; `Category` NotEmpty ≤100; `NeedsAndExpectations` NotEmpty ≤4000; `RelevantRequirements` ≤4000 | `OrgContextSlice.cs:24-33` |
| `ReviseInterestedPartyCommand` | **no validator** | — |
| `RegisterContextIssueCommand` | `Type` NotEmpty; `Category` NotEmpty ≤100; `Description` NotEmpty ≤4000; `Impact` NotEmpty ≤4000 | `OrgContextSlice.cs:108-117` |
| `ReviseContextIssueCommand` | **no validator** | — |
| `CloseContextIssueCommand` | `Resolution` NotEmpty ≤4000 | `OrgContextSlice.cs:100-106` |

**Unguarded enum parsing.** Five call sites use `Enum.Parse<T>(string, ignoreCase: true)` with no `TryParse` fallback, so a bad enum string throws `ArgumentException` — **not** a domain code, so `DomainExceptionHandler` returns `null` and the request falls through to the generic 500 path: `ComplaintsController.cs:33` (`ComplaintChannel`), `ConflictSlice.cs:75` (`ConflictRiskLevel`), `:82` (`ConflictOutcome`), `QualityObjectiveSlice.cs:44` (`ObjectiveDirection`), `FeedbackSlice.cs:44` (`FeedbackType`), `OrgContextSlice.cs:132`, `:142` (`ContextIssueType`). By contrast the **query** handlers use `Enum.TryParse` and silently ignore an unparsable filter (`ConflictSlice.cs:102`, `QualityObjectiveSlice.cs:124`, `FeedbackSlice.cs:141`, `:147`, `ComplaintCommands.cs:172`), while `GetRisksQuery` / `GetChangesQuery` compare `Status.ToString() == q.Status`, i.e. **case-sensitively** (`RiskGovernanceSlice.cs:120`, `:291`). See GAP-RISK-021 note and the EP reservation.

### 1.7 Persistence

**Twelve tables + three owned child tables**, all in schema `qams`.

| Table | Entity | PK | Config | Created by |
|---|---|---|---|---|
| `risk_item` | `RiskItem` | `(tenant_id, id)` | `GovernanceConfigurations.cs:8-35` | pre-baseline |
| `mitigation_action` | owned `MitigationAction` | `(tenant_id, id)` | `GovernanceConfigurations.cs:21-31` | pre-baseline |
| `change_request` | `ChangeRequest` | `(tenant_id, id)` | `GovernanceConfigurations.cs:37-49` | pre-baseline; **+4 columns** by `ChangePostImplementationReview` |
| `management_review` | `ManagementReview` | `(tenant_id, id)` | `GovernanceConfigurations.cs:51-76` | pre-baseline |
| `review_decision` | owned `ReviewDecision` | `(tenant_id, id)` | `GovernanceConfigurations.cs:62-72` | pre-baseline |
| `conflict_declaration` | `ConflictDeclaration` | `(tenant_id, id)` | `GovernanceConfigurations.cs:123-138` | `20260725081714_ImpartialityAndOrgContext.cs:14-40` |
| `interested_party` | `InterestedParty` | `(tenant_id, id)` | `PlatformConfigurations.cs:112-125` | `…ImpartialityAndOrgContext.cs:67-89` |
| `context_issue` | `ContextIssue` | `(tenant_id, id)` | `PlatformConfigurations.cs:127-140` | `…ImpartialityAndOrgContext.cs:42-65` |
| `quality_policy` | `QualityPolicy` | `(tenant_id, id)` | `IdentityAndImprovementConfigurations.cs:191-204` | `20260726203026_QualityPolicy.cs:14-37` |
| `quality_objective` | `QualityObjective` | `(tenant_id, id)` | `IdentityAndImprovementConfigurations.cs:144-175` | `20260725080545_ObjectivesAndFeedback.cs:46-75` |
| `objective_progress` | owned `ObjectiveProgressUpdate` | `(tenant_id, id)` | `IdentityAndImprovementConfigurations.cs:161-171` | `…ObjectivesAndFeedback.cs:77-99` |
| `feedback_entry` | `FeedbackEntry` | `(tenant_id, id)` | `IdentityAndImprovementConfigurations.cs:206-222` | `…ObjectivesAndFeedback.cs:14-44` |
| `complaint` | `Complaint` | `(tenant_id, id)` | `IdentityAndImprovementConfigurations.cs:113-130` | pre-baseline |

**Migrations named in scope, verbatim effects:**

1. **`20260725080545_ObjectivesAndFeedback`** — creates `feedback_entry` (`:14-44`), `quality_objective` (`:46-75`), `objective_progress` (`:77-99`, FK `objective_id → quality_objective.id` CASCADE at `:92-98`). Unique `(tenant_id, feedback_ref)` (`:101-106`), index `(tenant_id, status)` (`:108-112`), index `objective_id` (`:114-118`), unique `(tenant_id, objective_ref)` (`:120-125`), index `(tenant_id, status)` (`:127-131`). RLS added in the same migration but in its **early, weaker form** — `ENABLE` only, `USING` only, no `FORCE`, no `WITH CHECK`, no bypass clause (`:132-140`).
2. **`20260725081714_ImpartialityAndOrgContext`** — creates `conflict_declaration`, `context_issue`, `interested_party` with the same early RLS shape (`:117-128`).
3. **`20260726203026_QualityPolicy`** — creates `quality_policy` with `policy_ref varchar(30)`, `version integer`, `statement varchar(8000)`, `status varchar(20)`, `effective_date date`, `approved_by_id uuid`, `approved_at_utc timestamptz`, plus `created_by_user_id` (`:30`, the SoD column). Three indexes: unique `(tenant_id, policy_ref)`, `(tenant_id, status)`, **unique `(tenant_id, version)`** (`:39-57`). **This migration alone writes the modern RLS shape inline** — `ENABLE` + `FORCE` + `USING`/`WITH CHECK` with the `app.bypass_rls` clause (`:62-75`), with a comment explaining F-01 parity.
4. **`20260726211332_ChangePostImplementationReview`** — adds four nullable columns to `change_request`: `change_effective boolean` (`:14-19`), `post_implementation_review_notes text` (`:21-26`), `post_implementation_reviewed_at_utc timestamptz` (`:28-33`), `post_implementation_reviewed_by uuid` (`:35-40`). No index, no constraint, no RLS change (the table already had it).

**RLS as it stands at v1.51.2.** The weak policies from migrations 1 and 2 were rewritten by `20260726081443_ActivateForcedTenantRls`, which iterates `pg_policies WHERE policyname='tenant_isolation'` and, for each, applies `ENABLE` + `FORCE` + the canonical `USING`/`WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant',true),'')::uuid OR current_setting('app.bypass_rls',true)='on')` — `20260726081443_ActivateForcedTenantRls.cs:23-47`. `quality_policy` (created later) carries that shape from birth. The three owned child tables were brought in by `Hardening4_ChildTenancy`: `mitigation_action` (`:582-589`), `objective_progress` (`:591-598`), `review_decision` (`:645+`).

**Tenant-composite FKs on the owned children** (`20260731201114_Hardening4_ChildTenancy.cs`):
- `fk_mitigation_action_risk_item_tenant FOREIGN KEY (risk_id, tenant_id) REFERENCES qams.risk_item (id, tenant_id) ON DELETE CASCADE` (`:400-402`), backed by `ux_risk_item_id_tenant` (`:344`).
- `fk_objective_progress_quality_objective_tenant FOREIGN KEY (objective_id, tenant_id) REFERENCES qams.quality_objective (id, tenant_id) ON DELETE CASCADE` (`:403-405`), backed by `ux_quality_objective_id_tenant` (`:342`).
- `fk_review_decision_management_review_tenant FOREIGN KEY (review_id, tenant_id) REFERENCES qams.management_review (id, tenant_id) ON DELETE CASCADE` (`:421-423`), backed by `ux_management_review_id_tenant` (`:335`).
Backfills at `:295-310`. The shadow `TenantId` is stamped by `TenantStampInterceptor` per the EF config comments (`GovernanceConfigurations.cs:25-28`, `:65-69`; `IdentityAndImprovementConfigurations.cs:164-168`).

**CHECK constraints touching this module:**

| Constraint | Definition | Source |
|---|---|---|
| `ck_risk_item_likelihood_range` | `likelihood BETWEEN 1 AND 5` | `20260728073229_Phase5CheckConstraints.cs:35` |
| `ck_risk_item_impact_range` | `impact BETWEEN 1 AND 5` | `…:36` |
| `ck_risk_item_rpn_range` | `rpn BETWEEN 1 AND 25` | `…:37` |
| `ck_risk_item_residual_ranges` | each residual col `IS NULL OR` in range; `residual_rpn IS NULL OR BETWEEN 1 AND 25` | `…:38-41` |
| `ck_risk_item_status_domain` | `status IN ('Identified','Mitigating','Closed')` | `20260731191212_Hardening3_CheckDomains.cs:131` |
| `ck_change_request_status_domain` | `status IN ('Proposed','Approved','Rejected','Closed','Reviewed')` | `…:51` |
| `ck_management_review_status_domain` | `status IN ('Scheduled','Closed')` | `…:95` |
| `ck_conflict_declaration_status_domain` | `status IN ('Declared','Assessed','Closed')` | `…:63` |
| `ck_conflict_declaration_risk_level_domain` | `risk_level IN ('Low','Medium','High')` | `…:61` |
| `ck_conflict_declaration_outcome_domain` | `outcome IN ('Accepted','Mitigated','Withdrawn')` | `…:59` |
| `ck_context_issue_status_domain` | `status IN ('Active','Closed')` | `…:65` |
| `ck_context_issue_type_domain` | `type IN ('Internal','External')` | `…:67` |
| `ck_interested_party_status_domain` | `status IN ('Active','Archived')` | `…:87` |
| `ck_quality_policy_status_domain` | `status IN ('Draft','Active','Superseded')` | `…:119` |
| `ck_quality_objective_status_domain` | `status IN ('Active','Achieved','Missed','Cancelled')` | `…:117` |
| `ck_quality_objective_direction_domain` | `direction IN ('AtLeast','AtMost')` | `…:115` |
| `ck_complaint_status_domain` | `status IN ('Logged','Acknowledged','Validated','Investigating','OutcomeLogged','Resolved','Closed','Invalid')` | `…:57` |
| `ck_complaint_channel_domain` | `channel IN ('Phone','Email','Portal','InPerson','Letter')` | `…:55` |
| `ck_feedback_entry_status_domain` | `status IN ('Logged','Reviewed','Closed','Escalated')` | `…:81` |
| `ck_feedback_entry_type_domain` | `type IN ('Compliment','Suggestion','Dissatisfaction')` | `…:83` |

All `Hardening3` constraints were added `NOT VALID` then immediately `VALIDATE`d. **There is no CHECK on `feedback_entry.satisfaction_score`** — the 1–5 rule lives only in the domain (`FeedbackEntry.cs:63-66`) and the validator; and **no CHECK on `quality_objective` period ordering**.

**No DB-level immutability trigger applies to any table in this module.** `qams.reject_frozen_mutation()` is attached only to the twelve analytical study roots plus `uncertainty_budget` — `20260726084134_SignedRecordImmutability.cs:16-28`, applied at `:56-64`. "A closed risk is immutable", "a reviewed change is fully terminal", "closed minutes are immutable" and "an active or superseded policy is immutable" are **application-layer statements only**. See GAP-RISK-006.

**Concurrency:** the `xmin` system column is the concurrency token across aggregate roots; there is no `row_version` column. A lost-update attempt surfaces as `409` `CONCURRENCY-409` (`DomainExceptionHandler.cs:21`, `:28-33`).

**Reference prefixes** (from `IReferenceNumberGenerator.NextAsync(tenantId, prefix, ct)`): `RSK` (`RiskGovernanceSlice.cs:43`), `CHG` (`:172`), `MRV` (`:344`), `COI` (`ConflictSlice.cs:57`), `QP` (`QualityPolicySlice.cs:48`), `QO` (`QualityObjectiveSlice.cs:41`), `FB` (`FeedbackSlice.cs:42`), `CMP` (`ComplaintCommands.cs:42` and `FeedbackSlice.cs:107`), `IP` (`OrgContextSlice.cs:45`), `CTX` (`OrgContextSlice.cs:130`), `NC` (saga, `ComplaintToNcPolicy.cs:41`). All `*_ref` columns are `varchar(30)` and unique per tenant.

### 1.8 Read models and pagination

| Query | Shape | Paged? | Ordering | Source |
|---|---|---|---|---|
| `GetRisksQuery` | `PagedResponse<RiskListItemDto>` | **Yes** (`PageRequest.Normalized`) | `Rpn` desc | `RiskGovernanceSlice.cs:108-130` |
| `GetRiskByIdQuery` | `RiskDetailDto` + `MitigationActionDto[]` | n/a | — | `:132-149` |
| `GetChangesQuery` | `PagedResponse<ChangeListItemDto>` | **Yes** | `CreatedAtUtc` desc | `:279-300` |
| `GetChangeByIdQuery` | `ChangeDetailDto` incl. all four PIR fields | n/a | — | `:302-319` |
| `GetReviewsQuery` | `PagedResponse<ReviewListItemDto>` incl. `Decisions.Count` | **Yes** | `ReviewDate` desc | `:395-408` |
| `GetReviewByIdQuery` | `ReviewDetailDto` + `ReviewDecisionDto[]` | n/a | — | `:410-427` |
| `GetConflictsQuery` | `IReadOnlyList<ConflictListItemDto>` | **No** | `DeclaredOn` desc | `ConflictSlice.cs:93-115` |
| `GetConflictByIdQuery` | `ConflictDetailDto` | n/a | — | `:117-133` |
| `GetQualityPoliciesQuery` | `IReadOnlyList<QualityPolicyDto>` | **No** | `Version` desc | `QualityPolicySlice.cs:94-106` |
| `GetActiveQualityPolicyQuery` | `QualityPolicyDto?` | n/a | first `Active` | `:109-121` |
| `GetQualityObjectivesQuery` | `IReadOnlyList<QualityObjectiveListItemDto>` | **No** | `PeriodStart` desc | `QualityObjectiveSlice.cs:113-141` |
| `GetQualityObjectiveByIdQuery` | detail + progress desc by `MeasuredOn` | n/a | — | `:143-164` |
| `GetComplaintsQuery` | `IReadOnlyList<ComplaintListItemDto>` | **No** | `LoggedAtUtc` desc | `ComplaintCommands.cs:161-186` |
| `GetComplaintByIdQuery` | `ComplaintDetailDto` | n/a | — | `:188-211` |
| `GetFeedbackQuery` | `IReadOnlyList<FeedbackListItemDto>` | **No** | `ReceivedOn` desc | `FeedbackSlice.cs:131-160` |
| `GetFeedbackByIdQuery` | `FeedbackDetailDto` | n/a | — | `:162-178` |
| `GetInterestedPartiesQuery` | `IReadOnlyList<InterestedPartyDto>` | **No** | `Status` then `Name` | `OrgContextSlice.cs:72-84` |
| `GetContextIssuesQuery` | `IReadOnlyList<ContextIssueDto>` | **No** | `Status` then `IssueRef` | `OrgContextSlice.cs:171-183` |

**Only 3 of the 9 list endpoints return the API-004 pagination envelope.** The other six return a bare array with no total and no cap (GAP-RISK-017).

**`GetQualityObjectivesQuery` materialises before projecting** — `CurrentValue`/`OnTarget` are domain-computed and `Ignore`d in EF (`IdentityAndImprovementConfigurations.cs:158-159`), so the handler calls `ToListAsync` on the whole filtered set then maps in memory (`QualityObjectiveSlice.cs:129-139`). With no page cap this is an unbounded materialisation.

### 1.9 KPI and reporting integration

`KpiSnapshotService` (a `BackgroundService`) computes, per tenant, with `IgnoreQueryFilters()`:
- `HighResidualRisks = COUNT(risk_item WHERE tenant_id = @t AND status <> 'Closed' AND residual_rpn IS NOT NULL AND residual_rpn > RiskItem.HighResidualThreshold)` — `src/NT.QAMS.Infrastructure/Jobs/KpiSnapshotService.cs:120-122`. Note it excludes closed risks and uses the same strict `>` 12.
- `OpenComplaints = COUNT(complaint WHERE status NOT IN ('Closed','Invalid'))` — `:112-114`.
Both surface in the dashboard, the management-review pack (`ExportsController.cs:139-146`) and the KPI XLSX.

### 1.10 Frontend surface

Nine SPA routes, all lazy standalone components — `frontend/src/app/app.routes.ts`:

| Path | Component | Line |
|---|---|---|
| `quality-policy` | `QualityPolicyComponent` (`features/governance/quality-policy.component`) | `:72-73` |
| `quality-objectives` | (`features/…`) | `:76` |
| `feedback` / `feedback/:id` | `FeedbackListComponent` / `FeedbackDetailComponent` | `:86-91` |
| `complaints` / `complaints/:id` | `ComplaintListComponent` / `ComplaintDetailComponent` | `:96-101` |
| `risks` | | `:170` |
| `conflicts` | | `:180` |
| `org-context` | `OrgContextComponent` (`features/organization/org-context.component`) | `:190-191` |
| `changes` | | `:194` |
| `management-reviews` | | `:204` |

### 1.11 Existing automated coverage (baseline — do not duplicate blindly)

| Test file | Covers |
|---|---|
| `tests/NT.QAMS.Domain.UnitTests/Governance/GovernanceAndSupplierTests.cs` | risk / change / management-review domain guards |
| `tests/NT.QAMS.Domain.UnitTests/RiskGovernance/ConflictDeclarationTests.cs` | conflict lifecycle + `SOD-COI-001` |
| `tests/NT.QAMS.Domain.UnitTests/Improvement/ComplaintTests.cs` | complaint state machine |
| `tests/NT.QAMS.Domain.UnitTests/Improvement/FeedbackEntryTests.cs` | feedback lifecycle + escalation |
| `tests/NT.QAMS.Domain.UnitTests/Improvement/QualityObjectiveTests.cs` | objective closure honesty |
| `tests/NT.QAMS.Domain.UnitTests/Improvement/QualityPolicyTests.cs` | policy versioning + `SOD-QP-001` |
| `tests/NT.QAMS.Domain.UnitTests/Organization/OrganizationContextTests.cs` | interested party + context issue |
| `tests/NT.QAMS.Architecture.Tests/CommandPolicyTests.cs` | every command carries one policy attribute |

Case authors should mark overlapping cases `Automation Candidate: Yes (already automated — extend, do not re-implement)` and cite the file.

---

## 2. Divergences from the commissioning brief

Each row is a place where the brief (or the general regulatory expectation the brief encodes) assumes behaviour the code does not implement, or implements differently. Nothing here is reconciled silently; every row has a gap id in §8.

| # | What the brief assumes | What the code actually does | Evidence `file:line` | Gap |
|---|---|---|---|---|
| D-01 | Change approval is a segregated act — the proposer cannot approve their own change | `ChangeRequest.Approve(actorId, at)` compares nothing. It checks state and `RiskItemId is null` only. `ProposedBy` is never read during approval | `src/NT.QAMS.Domain/RiskGovernance/ChangeAndReview.cs:72-84`; proposer stamped at `RiskGovernanceSlice.cs:173-174` | GAP-RISK-001 |
| D-02 | Governance approvals (change, management review, quality policy) are electronically signed under Part 11 | No `ESignatureService` call anywhere in the module. `changes`, `reviews` and `quality-policy` are `SignedRecordLifecycle` modules, so `changes.sign` / `reviews.sign` / `quality-policy.sign` **exist as keys** but no endpoint consumes them | `PermissionCatalog.cs:141`, `:142`, `:146` define them; zero `PermissionAction.Sign` hits in the nine controllers | GAP-RISK-002 |
| D-03 | Reading the risk register / complaint register / change log requires the corresponding `*.view` privilege | 17 of the 18 GET endpoints carry `[Authorize]` only. `risks.view`, `changes.view`, `reviews.view`, `conflicts.view`, `org-context.view`, `objectives.view`, `complaints.view`, `feedback.view` have **no enforcement point** | `GovernanceControllers.cs:17`, `:24`, `:71`, `:78`, `:135`, `:141`; `ConflictsController.cs:17`, `:21`; `OrgContextController.cs:19`, `:53`; `QualityObjectivesController.cs:17`, `:21`; `ComplaintsController.cs:21`, `:25`; `FeedbackController.cs:17`, `:22` | GAP-RISK-003 |
| D-04 | Raising a risk, proposing a change, logging a complaint and logging feedback each require the module `create` privilege | Those POSTs carry no `[RequirePermission]`; any internal actor may create. Ten write endpoints in total are ungated | `GovernanceControllers.cs:28`, `:37`, `:42`, `:82`, `:90`, `:113`; `ConflictsController.cs:25`; `ComplaintsController.cs:29`; `FeedbackController.cs:26`; `QualityObjectivesController.cs:36` | GAP-RISK-004 |
| D-05 | Confidential complainant identity is released on a privilege | It is released on the **legacy tier claim**: `User.IsInRole("QualityManager") \|\| User.IsInRole("TenantAdmin")`. A tenant-defined role granted every `complaints.*` key still sees `•••` | `ComplaintsController.cs:18-19`; masking at `ComplaintCommands.cs:182`, `:202-206`; claim minted at `SecurityAdapters.cs:88` | GAP-RISK-005 |
| D-06 | Terminal governance records are immutable at the database, as signed analytical records are | The `qams.reject_frozen_mutation()` trigger is attached only to the 12 analytical study roots + `uncertainty_budget`. No table in this module has it. A direct `UPDATE qams.change_request SET status='Proposed'` on a Reviewed row succeeds | `20260726084134_SignedRecordImmutability.cs:16-28`, applied `:56-64` | GAP-RISK-006 |
| D-07 | A high residual risk notifies once and is cleared when mitigated below threshold | `RecordResidualAssessment` raises `HighResidualRisk` **every time** it is called with RPN > 12; there is no dedup and no counterpart event when the residual drops to ≤ 12 | `RiskItem.cs:103-116`; dispatch at `NotificationPolicies.cs:101-107` | GAP-RISK-007 |
| D-08 | Approving a change, closing a management review and approving the quality policy notify the relevant audience | `ChangeApproved`, `ChangePostImplementationReviewed`, `ReviewClosed`, `QualityPolicyApproved`, `RiskClosed`, `ComplaintLogged/Acknowledged/Resolved/Closed` have **no `INotificationHandler`**. They reach the outbox and the audit chain but produce no notification | `NotificationPolicies.cs:24-33` lists the ten handled event types — none of these are among them; seed array `:138-156` | GAP-RISK-008 |
| D-09 | Quality-policy approval always enforces signer ≠ preparer (URS-049) | `EnsureSignerIsNotPreparer` is a **no-op when `CreatedByUserId` is null** — a policy drafted before the column was captured, or by a background/system path, can be self-approved | `AggregateRoot.cs:36-42`; invoked `QualityPolicy.cs:78` | GAP-RISK-009 |
| D-10 | Exactly one quality policy is in force, and versions supersede in order | The handler supersedes **every** other `Active` policy regardless of version, so approving an *older* draft retires a *newer* active version. The one-in-force rule has no DB backing (`ux (tenant_id, version)` exists; there is no partial unique index on `status='Active'`) | `QualityPolicySlice.cs:74-83`; indexes at `20260726203026_QualityPolicy.cs:39-57` | GAP-RISK-010, GAP-RISK-011 |
| D-11 | A change may only be approved against a **live** risk assessment | `LinkRiskAssessment` accepts any risk id that exists in the tenant/scope; a `Closed` risk satisfies `CHG-012`. `LinkRiskHandler` checks existence only | `ChangeAndReview.cs:66-70`; `RiskGovernanceSlice.cs:225-237` | GAP-RISK-012 |
| D-12 | Domain "required field" rules are the enforcement point | For most of them the FluentValidation rule fires first, so the API returns **400 with an `errors` dictionary** and the domain code never surfaces. Codes unreachable via HTTP: `RSK-001`, `CHG-001`, `CHG-002`, `CHG-014`, `CHG-021`, `MRV-001`, `MRV-003`, `COI-001`, `COI-011`, `COI-013`, `QP-001`, `QP-002`, `OBJ-001` (title/metric), `OBJ-012`, `CMP-001`, `CMP-002`, `CMP-003`, `CMP-004`, `CMP-005`, `FBK-001`, `FBK-002`, `FBK-003`, `FBK-011`, `FBK-013`, `IP-001`, `IP-002`, `CTX-001`, `CTX-002`, `CTX-003`. They remain reachable and must be tested at **domain unit level** | validator table §1.6; `DomainExceptionHandler.cs:34-44` | GAP-RISK-021 |
| D-13 | `RSK-003` (mitigation description) behaves like the other required-field rules | It does **not** — `AddMitigationCommand` has no validator, so an empty description reaches the aggregate and returns **422 `RSK-003`**. Same asymmetry for `RSK-002` on the residual path (`RecordResidualCommand` has no validator → 422, while `AssessRiskCommand` has one → 400) | no validator for `AddMitigationCommand`/`RecordResidualCommand` in `RiskGovernanceSlice.cs`; `RiskItem.cs:86`, `:147` | — (recorded as an EP/BVA authoring instruction) |
| D-14 | Every regulated mutation captures a Part-11 reason for change | `ChangeReasonMiddleware` demands `X-Change-Reason` on **DELETE only**, and this module has **zero DELETE endpoints** (26 DELETEs exist system-wide; none on these nine routes). No governance mutation captures an explicit reason — only the automatic field-level diff | approved surface has no `DELETE /api/{risks,changes,...}`; ground truth `00-GROUND-TRUTH-AND-CONVENTIONS.md:82` | GAP-RISK-020 |
| D-15 | A risk register entry has an owner and a scheduled review date | `RiskItem` has neither. Only `MitigationAction` carries `OwnerId` + `DueDate`; the risk itself has no accountable person and no review cadence | whole-file read `RiskItem.cs:44-58` | GAP-RISK-019 |
| D-16 | Overdue mitigation actions and overdue management-review decisions are chased | `ScheduledSweepService` proposes calibration-due, grace-lockout, competency-expiry and supplier-suspension transitions only. It never reads `mitigation_action` or `review_decision` | ground truth `:108`; no reference to these tables in the sweep | GAP-RISK-014 |
| D-17 | Complaints are acknowledged within a defined period (ISO 17025 §7.9) | `AcknowledgedAtUtc` is recorded but never compared to anything. No SLA, no timer, no escalation on an unacknowledged complaint | `Complaint.cs:86-92`; `AcknowledgedAtUtc` read only into the DTO at `ComplaintCommands.cs:208` | GAP-RISK-018 |
| D-18 | Organizational context (ISO 9001 §4.1/§4.2) is a stated user requirement | **No URS covers it.** `URS-051` names conflict-of-interest, objectives, equipment, standards, competency and monitoring points — not interested parties or context issues. Grep of the URS for "interested part" / "context of the organi" returns nothing | `docs/validation/01-User-Requirements-Specification.md:109` | GAP-RISK-023 |
| D-19 | Escalating feedback carries the reporter's confidentiality choice and the original channel | `EscalateFeedbackCommand` hard-codes `ComplaintChannel.Portal` and `confidential: false`, and synthesises the subject as `"Escalated feedback {ref}: {subject}"` | `FeedbackSlice.cs:108-113` | GAP-RISK-022 |
| D-20 | The complaint→NC saga assigns severity from the complaint | It hard-codes `severity: 3, likelihood: 3` (so RPN 9) for every justified complaint, irrespective of content | `ComplaintToNcPolicy.cs:41-50` | GAP-RISK-024 |
| D-21 | Register lists are paginated consistently (API-004 envelope) | Only `risks`, `changes` and `management-reviews` return `PagedResponse`. Conflicts, org-context (both), quality-policy, objectives, complaints and feedback return bare unbounded arrays | §1.8 table; e.g. `ConflictSlice.cs:107-113` vs `RiskGovernanceSlice.cs:124-128` | GAP-RISK-017 |
| D-22 | Creating a register entry returns `201 Created` with a `Location` header | Org-context and quality-policy creates return **200** with a bare `{id}` body; the other six use `CreatedAtAction` → 201 | `OrgContextController.cs:25-31`, `:59-64`; `QualityPolicyController.cs:38-39` vs `GovernanceControllers.cs:34` | — (recorded as an API-case authoring instruction) |
| D-23 | An invalid enum value in a request body is a client error | Seven `Enum.Parse<T>` call sites throw `ArgumentException`, which `DomainExceptionHandler` does not match, so the response is the generic **500** path rather than 400/422 | `ComplaintsController.cs:33`; `ConflictSlice.cs:75`, `:82`; `QualityObjectiveSlice.cs:44`; `FeedbackSlice.cs:44`; `OrgContextSlice.cs:132`, `:142` | GAP-RISK-021 (sub-item b) |
| D-24 | A closed/rejected change can be reopened or superseded | `Rejected` and `Reviewed` are absolute terminals — no method accepts either as its `expected` state, and there is no reopen path | `ChangeAndReview.cs:66-133` (all five `Require` call sites read) | GAP-RISK-016 |
| D-25 | Objective progress is a clean time series | Updates are append-only with no uniqueness on `MeasuredOn`, no ordering constraint against `PeriodStart`/`PeriodEnd`, no edit/void, and `CurrentValue` breaks ties non-deterministically | `QualityObjective.cs:100-101`, `:108-120` | GAP-RISK-015 |
| D-26 | Risk assessment and residual assessment are equally privileged | The **initial** assessment (`POST /api/risks`) is ungated while the **residual** assessment requires `risks.approve` and closure requires `risks.void`. A user with no risk privileges at all can create risks and add mitigations | `GovernanceControllers.cs:28`, `:37` vs `:50`, `:58` | GAP-RISK-004 (sub-item) |

---

## 3. State-transition matrices

Ten machines. In every matrix the **rows are current states** and the **columns are the operations exposed by the aggregate**. A cell reads either the target state (legal) or the **exact code + HTTP status** the illegal attempt produces. `—` means the operation does not exist for that aggregate. Case authors must cover every non-empty cell; the STATE reservation (70 ids) is sized for that.

Persisted status strings are the enum names verbatim, pinned by the `ck_*_status_domain` CHECK constraints in §1.7.

### 3.1 `RiskItem` — `Identified | Mitigating | Closed`

Enum: `RiskItem.cs:6`. Persisted values pinned by `ck_risk_item_status_domain`.

| From ↓ / Op → | `AddMitigationAction` | `CompleteMitigationAction` | `RecordResidualAssessment` | `Close` |
|---|---|---|---|---|
| **Identified** | → **Mitigating** (`:91`) | `RSK-404` 422 if id unknown (`:99`); no state change | stays **Identified**, sets residual, may raise `HighResidualRisk` (`:103-116`) | → **Closed** *iff* `ResidualRpn != null` (`:121`) — with no actions the `RSK-006` clause is vacuously satisfied |
| **Mitigating** | → **Mitigating** (idempotent re-set at `:91`) | stays **Mitigating**, marks `Completed = true` (`:100`) | stays **Mitigating** | → **Closed** *iff* residual recorded **and** every action `Completed`; else `RSK-005` 422 or `RSK-006` 422 |
| **Closed** | **`RSK-007` 409** (`:139`) | **`RSK-007` 409** | **`RSK-007` 409** | **`RSK-007` 409** |

Notes for case authors:
- There is **no transition back** from `Mitigating` to `Identified`, and none out of `Closed`.
- `Identified → Closed` **is legal** and is the shortest lifecycle: assess → record residual → close.
- `RSK-005` fires before `RSK-006`: `Close` checks `ResidualRpn is null` at `:121` *before* the incomplete-actions scan at `:126`. A risk with both defects returns `RSK-005`.
- `RecordResidualAssessment` re-validates the 1–5 scale → `RSK-002` 422 (`:106` → `:147`). This path has **no** FluentValidation shadow, so `RSK-002` is genuinely reachable over HTTP here (unlike on `Assess`, which returns 400).

### 3.2 `ChangeRequest` — `Proposed | Approved | Rejected | Closed | Reviewed`

Enum: `ChangeAndReview.cs:6`. Guard helper `Require(expected, code, action)` at `:127-133` — every failure is an `InvalidStateTransitionException` → **409**.

| From ↓ / Op → | `LinkRiskAssessment` | `Approve` | `Reject` | `Close` | `RecordPostImplementationReview` |
|---|---|---|---|---|---|
| **Proposed** | → **Proposed** (sets/overwrites `RiskItemId`, `:66-70`) | → **Approved** *iff* `RiskItemId != null`; else **`CHG-012` 422** (`:75-78`) | → **Rejected** *iff* reason non-blank; else `CHG-014` 422 | **`CHG-015` 409** | **`CHG-020` 409** |
| **Approved** | **`CHG-010` 409** | **`CHG-011` 409** | **`CHG-013` 409** | → **Closed** (`:98-103`) | **`CHG-020` 409** |
| **Rejected** | **`CHG-010` 409** | **`CHG-011` 409** | **`CHG-013` 409** | **`CHG-015` 409** | **`CHG-020` 409** |
| **Closed** | **`CHG-010` 409** | **`CHG-011` 409** | **`CHG-013` 409** | **`CHG-015` 409** | → **Reviewed** *iff* notes non-blank; else `CHG-021` 422 (`:111-125`) |
| **Reviewed** | **`CHG-010` 409** | **`CHG-011` 409** | **`CHG-013` 409** | **`CHG-015` 409** | **`CHG-020` 409** |

Notes:
- `Rejected` and `Reviewed` are **absolute terminals** (D-24 / GAP-RISK-016).
- `Approve` sets `ApprovedBy` + `ApprovedAtUtc` from `ICurrentUser` + `IClock` and raises `ChangeApproved` (`:80-83`).
- `RecordPostImplementationReview` writes all four PIR columns and raises `ChangePostImplementationReviewed(…, effective, …)` — the boolean is recorded either way; **an ineffective change still reaches `Reviewed`** (`:111-125`). There is no "reopen because ineffective" path.
- Ordering inside `Approve`: the state guard (`CHG-011`, 409) runs **before** the missing-risk guard (`CHG-012`, 422). Approving an already-Approved change with no risk yields `CHG-011`.
- Ordering inside `RecordPostImplementationReview`: state guard `CHG-020` (409) before `CHG-021` (422).

### 3.3 `ManagementReview` — `Scheduled | Closed`

Enum: `ChangeAndReview.cs:136`.

| From ↓ / Op → | `AddDecision` | `Close` |
|---|---|---|
| **Scheduled** | → **Scheduled**, appends a `ReviewDecision`; blank description → `MRV-002` 422 (`:200-211`) | → **Closed** *iff* minutes non-blank; else `MRV-003` 422; raises `ReviewClosed(…, DecisionCount)` (`:213-225`) |
| **Closed** | **`MRV-004` 409** — "closed minutes are immutable" (`:232`) | **`MRV-004` 409** |

Notes: no reopen; decisions are append-only with no complete/void operation of their own — a `ReviewDecision` has `Description`, `OwnerId`, `DueDate` and nothing else (`ChangeAndReview.cs:138-152`). `ReviewClosed.DecisionCount` is captured at closure time.

### 3.4 `ConflictDeclaration` — `Declared | Assessed | Closed`

Enum: `ConflictDeclaration.cs:8`. Risk level `Low|Medium|High` (`:6`); outcome `Accepted|Mitigated|Withdrawn` (`:10`).

| From ↓ / Op → | `Assess(assessorId, riskLevel, mitigation)` | `Close(outcome, note)` |
|---|---|---|
| **Declared** | → **Assessed** *iff* `assessorId != DeclarantId` (else **`SOD-COI-001` 422**) **and** mitigation non-blank (else `COI-011` 422). Raises `HighImpartialityRiskDeclared` **only when `riskLevel == High`** (`:85-88`) | **`COI-012` 409** (`:95`) |
| **Assessed** | **`COI-010` 409** (`:67`) | → **Closed** *iff* note non-blank; else `COI-013` 422 (`:91-106`) |
| **Closed** | **`COI-010` 409** | **`COI-012` 409** |

Guard ordering inside `Assess`: state (`COI-010`, 409) → SoD (`SOD-COI-001`, 422) → mitigation (`COI-011`, 422). A declarant self-assessing an **already-assessed** conflict therefore gets `COI-010`, not `SOD-COI-001`.
Contrast with quality policy: this SoD compares against `DeclarantId`, a **required non-null** field, so it can never be a no-op.

### 3.5 `QualityPolicy` — `Draft | Active | Superseded`

Enum: `QualityPolicy.cs:6`.

| From ↓ / Op → | `ReviseDraft` | `Approve(approverId, at, effectiveDate)` | `Supersede` |
|---|---|---|---|
| **Draft** | → **Draft**, replaces `Statement`; blank → `QP-001` 422 (`:56-69`) | → **Active** *iff* `approverId != CreatedByUserId` (else **`SOD-QP-001` 422**); sets `ApprovedById`, `ApprovedAtUtc`, `EffectiveDate`; raises `QualityPolicyApproved` (`:76-91`) | **`QP-011` 409** (`:98`) |
| **Active** | **`QP-012` 409** (`:60`) | **`SOD-QP-001` 422 if self, otherwise `QP-010` 409** — see ordering note | → **Superseded** (`:94-102`) |
| **Superseded** | **`QP-012` 409** | **`SOD-QP-001` 422 if self, otherwise `QP-010` 409** | **`QP-011` 409** |

**Ordering note (a real trap for case authors).** `Approve` calls `EnsureSignerIsNotPreparer` at `:78` **before** the state check at `:80`. So an author re-approving their own already-Active policy sees **`SOD-QP-001` (422)**, not `QP-010` (409). Every other aggregate in this module checks state first. Assert the exact code.

**Handler-level side effect not visible in the aggregate:** approving policy *P* first calls `Supersede()` on every other `Active` policy in the tenant (`QualityPolicySlice.cs:74-83`), so a single `POST /api/quality-policy/{id}/approve` performs an `Active → Superseded` transition on a *different* aggregate in the same transaction. `Draft` version numbers are assigned as `max(version)+1` per tenant (`:41-49`), and `ux (tenant_id, version)` makes a duplicate impossible (`20260726203026_QualityPolicy.cs:52-57`).

### 3.6 `QualityObjective` — `Active | Achieved | Missed | Cancelled`

Enum: `QualityObjective.cs:9`. Direction `AtLeast | AtMost` (`:7`).

| From ↓ / Op → | `RecordProgress` | `CloseAsAchieved(note)` | `CloseAsMissed(note)` | `Cancel(reason)` |
|---|---|---|---|---|
| **Active** | → **Active**, appends an `ObjectiveProgressUpdate` (`:108-120`) | → **Achieved** *iff* `OnTarget == true`; else **`OBJ-011` 422** (`:122-132`) | → **Missed** *iff* note non-blank; else `OBJ-012` 422 | → **Cancelled** *iff* reason non-blank; else `OBJ-012` 422 |
| **Achieved** | **`OBJ-010` 409** (`:112`) | **`OBJ-013` 409** (`:159`) | **`OBJ-013` 409** | **`OBJ-013` 409** |
| **Missed** | **`OBJ-010` 409** | **`OBJ-013` 409** | **`OBJ-013` 409** | **`OBJ-013` 409** |
| **Cancelled** | **`OBJ-010` 409** | **`OBJ-013` 409** | **`OBJ-013` 409** | **`OBJ-013` 409** |

Notes:
- The three closures are reached from one endpoint, `POST /api/quality-objectives/{id}/close`, dispatched on the lower-cased `Outcome` string; anything else → **`OBJ-014` 422** (`QualityObjectiveSlice.cs:94-100`). The string match is `achieved|missed|cancelled` after `ToLowerInvariant()`, so `"ACHIEVED"` and `"Achieved"` both work.
- `CloseAsAchieved` ordering: `RequireActive` (`OBJ-013`, 409) → `OnTarget != true` (`OBJ-011`, 422) → note check (`OBJ-012`, 422).
- `OnTarget` is `null` when there are no progress updates, and `null != true`, so **closing an objective as Achieved with zero measurements yields `OBJ-011`** — this is the honesty invariant and deserves its own MCDC case.
- `OBJ-012` is unreachable over HTTP because `CloseObjectiveValidator` requires a non-empty `Note` (`QualityObjectiveSlice.cs:61-67`) → 400.

### 3.7 `Complaint` — `Logged | Acknowledged | Validated | Investigating | OutcomeLogged | Resolved | Closed | Invalid`

Enum: `Complaint.cs:6-9`. Eight states — the longest machine in the module. Every guard is `Require(expected, code, action)` at `:173-179` → **409**.

| From ↓ / Op → | `Acknowledge` | `RecordValidationVerdict(justified)` | `StartInvestigation` | `LogOutcome` | `Resolve` | `Close` |
|---|---|---|---|---|---|---|
| **Logged** | → **Acknowledged** (`:86-92`) | `CMP-011` 409 | `CMP-012` 409 | `CMP-013` 409 | `CMP-014` 409 | `CMP-015` 409 |
| **Acknowledged** | `CMP-010` 409 | `justified=true` → **Validated** + `ComplaintValidated`; `justified=false` → **Invalid** (`:99-117`) | `CMP-012` 409 | `CMP-013` 409 | `CMP-014` 409 | `CMP-015` 409 |
| **Validated** | `CMP-010` 409 | `CMP-011` 409 | → **Investigating** (`:119-123`) | `CMP-013` 409 | `CMP-014` 409 | `CMP-015` 409 |
| **Investigating** | `CMP-010` 409 | `CMP-011` 409 | `CMP-012` 409 | → **OutcomeLogged** (`:125-135`) | `CMP-014` 409 | `CMP-015` 409 |
| **OutcomeLogged** | `CMP-010` 409 | `CMP-011` 409 | `CMP-012` 409 | `CMP-013` 409 | → **Resolved** + `ComplaintResolved` (`:137-148`) | `CMP-015` 409 |
| **Resolved** | `CMP-010` 409 | `CMP-011` 409 | `CMP-012` 409 | `CMP-013` 409 | `CMP-014` 409 | → **Closed** *iff* `LinkedNcId is null` **or** the linked NC is `Closed`; else **`CMP-020` 422** (`:155-165`) |
| **Closed** | `CMP-010` 409 | `CMP-011` 409 | `CMP-012` 409 | `CMP-013` 409 | `CMP-014` 409 | `CMP-015` 409 |
| **Invalid** | `CMP-010` 409 | `CMP-011` 409 | `CMP-012` 409 | `CMP-013` 409 | `CMP-014` 409 | `CMP-015` 409 |

`LinkNc(ncId)` is state-agnostic and idempotent (`??=`, `:168-171`) — it is called only by the saga, never by an endpoint.
**`Invalid` is a terminal branch off `Acknowledged`, not a rejection of a validated complaint.** An unjustified verdict still records `ValidationVerdict` (`:107`) but raises **no** event, so no NC is opened.
The `CMP-020` gate value is computed in the handler as `LinkedNcId is null || EXISTS(nc WHERE id = LinkedNcId AND status = Closed)` — `ComplaintCommands.cs:146-148`, same transaction, no projection lag.

### 3.8 `FeedbackEntry` — `Logged | Reviewed | Closed | Escalated`

Enum: `FeedbackEntry.cs:8`. Type `Compliment | Suggestion | Dissatisfaction` (`:6`).

| From ↓ / Op → | `Review(notes)` | `Close(actionSummary)` | `Escalate(complaintId)` |
|---|---|---|---|
| **Logged** | → **Reviewed** *iff* notes non-blank; else `FBK-011` 422 (`:83-97`) | **`FBK-012` 409** (`:103`) | → **Escalated** *iff* `Type == Dissatisfaction`; else **`FBK-014` 422** (`:118-121`) |
| **Reviewed** | **`FBK-010` 409** (`:87`) | → **Closed** *iff* summary non-blank; else `FBK-013` 422 (`:99-113`) | → **Escalated** *iff* `Type == Dissatisfaction`; else **`FBK-014` 422** |
| **Closed** | **`FBK-010` 409** | **`FBK-012` 409** | **`FBK-014` 422 if not Dissatisfaction, otherwise `FBK-015` 409** — see ordering note |
| **Escalated** | **`FBK-010` 409** | **`FBK-012` 409** | **`FBK-014` 422 if not Dissatisfaction, otherwise `FBK-015` 409** |

**Ordering note.** `Escalate` checks **type first** (`:118`), status second (`:123`). Escalating a *closed compliment* returns `FBK-014` (422), not `FBK-015` (409). This is the second ordering trap in the module (the first being `SOD-QP-001`) and warrants an explicit MCDC pair.
**`Escalated` is terminal here but creates a `Complaint` in the same transaction** — `FeedbackWorkflowHandlers.Handle(EscalateFeedbackCommand)` mints a `CMP` reference, logs the complaint with `ComplaintChannel.Portal` and `confidential: false`, copies `TenantId`/`BranchId`/`DepartmentId` from the feedback, then calls `feedback.Escalate(complaint.Id)` — `FeedbackSlice.cs:101-122`. Both rows land in one `SaveChangesAsync` (`:120`); there is no half-escalated state.

### 3.9 `InterestedParty` — `Active | Archived`

Enum: `OrganizationContext.cs:6`.

| From ↓ / Op → | `Revise(...)` | `Archive()` |
|---|---|---|
| **Active** | → **Active**, fields replaced in place; blank name/category/needs → `IP-001` 422 (`:63-83`) | → **Archived** (`:85-93`) |
| **Archived** | **`IP-010` 409** — "register a new one instead" (`:69`) | **`IP-011` 409** — "already archived" (`:89`) |

There is **no un-archive**. Revision history exists only as `audit.field_change` rows (the XML doc at `:12-14` says so explicitly: "entries are updated in place (field-level audit captures every change) and archived, never deleted").

### 3.10 `ContextIssue` — `Active | Closed`

Enum: `OrganizationContext.cs:98`. Type `Internal | External` (`:96`).

| From ↓ / Op → | `Revise(...)` | `LinkRisk(riskId)` | `Close(resolution)` |
|---|---|---|---|
| **Active** | → **Active**, replaces type/category/description/impact; any blank → `CTX-001` 422 (`:154-167`) | → **Active**, sets `LinkedRiskId` (**overwrites** any prior link, no dedup) (`:169-173`); `RSK-404` 404 if the risk is not in this tenant/scope (`OrgContextSlice.cs:148-152`) | → **Closed** *iff* resolution non-blank; else `CTX-003` 422 (`:175-185`) |
| **Closed** | **`CTX-010` 409** — "the issue is closed and frozen" (`:191`) | **`CTX-010` 409** | **`CTX-010` 409** |

### 3.11 The risk RPN 5×5 grid, with the `HighResidualRisk` threshold marked

`Rpn = Likelihood × Impact` (`RiskItem.cs:76`); `ResidualRpn = likelihood × impact` (`:110`).
`HighResidualThreshold = 12` (`:33`); the event fires on **strict greater-than** — `if (ResidualRpn > HighResidualThreshold)` (`:112`).
Both axes are constrained to 1–5 by `RSK-002` (`:143-149`) **and** by `ck_risk_item_likelihood_range` / `ck_risk_item_impact_range`; the product by `ck_risk_item_rpn_range CHECK (rpn BETWEEN 1 AND 25)` (`20260728073229_Phase5CheckConstraints.cs:35-37`).

**★ = residual RPN > 12 → `HighResidualRisk` raised. ◆ = exactly 12, the boundary — NOT raised.**

| L ↓ \\ I → | **1** | **2** | **3** | **4** | **5** |
|---|---|---|---|---|---|
| **1** | 1 | 2 | 3 | 4 | 5 |
| **2** | 2 | 4 | 6 | 8 | 10 |
| **3** | 3 | 6 | 9 | **◆ 12** | **★ 15** |
| **4** | 4 | 8 | **◆ 12** | **★ 16** | **★ 20** |
| **5** | 5 | 10 | **★ 15** | **★ 20** | **★ 25** |

- **6 of the 25 cells** raise the event: (3,5)=15, (5,3)=15, (4,4)=16, (4,5)=20, (5,4)=20, (5,5)=25.
- **2 cells sit exactly on the threshold** and must **not** raise it: (3,4)=12 and (4,3)=12. These are the mandatory BVA pair.
- 17 cells are below the threshold.
- Distinct RPN values reachable: 1, 2, 3, 4, 5, 6, 8, 9, 10, 12, 15, 16, 20, 25 (14 values). **7, 11, 13, 14, 17, 18, 19, 21, 22, 23, 24 are unreachable** — a `residual_rpn` of 13 in the database can only arrive by direct SQL, which the CHECK constraint permits (it only bounds 1–25). Useful as a DB-integrity negative case.
- The **initial** `Rpn` never raises anything: `Assess` computes it but raises no event (`:69-78`). Only the residual assessment can.
- `KpiSnapshotService` counts `status <> 'Closed' AND residual_rpn > 12` (`KpiSnapshotService.cs:120-122`), so a **closed** high-residual risk drops out of the KPI while its `HighResidualRisk` notification stays in the ledger — a legitimate divergence between the event stream and the dashboard, worth one DF case.

The BVA reservation must cover, per axis: `0` (→ `RSK-002` / 400), `1`, `5`, `6` (→ `RSK-002` / 400), and negative values; plus RPN products `1`, `12`, `15`, `25`.

---

## 4. Decision tables

Conditions are read directly off the guard clauses. `–` means "not evaluated on this path" (a preceding guard short-circuits).

### 4.1 DT-1 — Change-request **closure** (`POST /api/changes/{id}/close`)

Handler `CloseChangeHandler` → `ChangeRequest.Close` (`ChangeAndReview.cs:98-103`); validator `CloseChangeValidator` (`RiskGovernanceSlice.cs:202-208`).

| Rule | Caller authenticated & internal | `ImplementationNotes` non-empty ≤4000 | Change exists in tenant/scope | `Status == Approved` | **Outcome** |
|---|---|---|---|---|---|
| R1 | N | – | – | – | **401** `AUTH-003` (unauthenticated) / **403** for `ExternalAuditor` via `[RequireInternalActor]` |
| R2 | Y | **N** | – | – | **400** validation `errors: { ImplementationNotes: [...] }` |
| R3 | Y | Y | **N** | – | **404** `CHG-404` |
| R4 | Y | Y | Y | **N** (Proposed/Rejected/Closed/Reviewed) | **409** `CHG-015`, message "Cannot close a change in state {Status}." |
| R5 | Y | Y | Y | Y | **204**; `status='Closed'`, `implementation_notes` set; **no domain event raised** |

Note there is **no permission condition** — `POST /api/changes/{id}/close` carries no `[RequirePermission]` (`GovernanceControllers.cs:113`). Contrast DT-2.

### 4.2 DT-2 — Change-request **post-implementation review** (`POST /api/changes/{id}/review`)

Handler `ReviewChangeHandler` → `RecordPostImplementationReview` (`ChangeAndReview.cs:111-125`); gate `changes.approve` (`GovernanceControllers.cs:122`); validator `ReviewChangeValidator` (`RiskGovernanceSlice.cs:212-216`).

| Rule | Authn | Holds `changes.approve` | `Notes` non-empty ≤4000 | Exists | `Status == Closed` | **Outcome** |
|---|---|---|---|---|---|---|
| R1 | N | – | – | – | – | **401** |
| R2 | Y | **N** | – | – | – | **403** `AUTHZ-403`, `application/problem+json` (`RequirePermissionAttribute.cs:56-60`) |
| R3 | Y | Y | **N** | – | – | **400** validation |
| R4 | Y | Y | Y | **N** | – | **404** `CHG-404` |
| R5 | Y | Y | Y | Y | **N** | **409** `CHG-020` |
| R6 | Y | Y | Y | Y | Y, `effective = true` | **204**; `status='Reviewed'`, `change_effective=true`, reviewer + timestamp written; raises `ChangePostImplementationReviewed(effective: true)` → outbox row → `audit.audit_trail` row. **No notification** (no handler) |
| R7 | Y | Y | Y | Y | Y, `effective = false` | **204**; identical to R6 but `change_effective=false`. **The change still becomes `Reviewed` and terminal** — there is no corrective path (GAP-RISK-016) |

R6 vs R7 is the single most important pair in this module: the PIR verdict is recorded but has **no consequence**.

### 4.3 DT-3 — Risk closure (`POST /api/risks/{id}/close`)

Gate `risks.void` (`GovernanceControllers.cs:58`); `RiskItem.Close` (`RiskItem.cs:118-133`). No request body, no validator.

| Rule | Authn | Holds `risks.void` | Exists | `Status != Closed` | `ResidualRpn != null` | All actions `Completed` | **Outcome** |
|---|---|---|---|---|---|---|---|
| R1 | N | – | – | – | – | – | **401** |
| R2 | Y | **N** | – | – | – | – | **403** `AUTHZ-403` |
| R3 | Y | Y | **N** | – | – | – | **404** `RSK-404` |
| R4 | Y | Y | Y | **N** | – | – | **409** `RSK-007` |
| R5 | Y | Y | Y | Y | **N** | – | **422** `RSK-005` |
| R6 | Y | Y | Y | Y | Y | **N** | **422** `RSK-006` |
| R7 | Y | Y | Y | Y | Y | Y (incl. the zero-action case) | **204**; `status='Closed'`; raises `RiskClosed(…, ResidualRpn)`; **no notification** (no handler); the risk leaves the `HighResidualRisks` KPI count |

R5 precedes R6 by source order (`:121` before `:126`) — assert `RSK-005` when both fail.

### 4.4 DT-4 — Objective closure (`POST /api/quality-objectives/{id}/close`)

Gate `objectives.void` (`QualityObjectivesController.cs:45`); dispatcher `QualityObjectiveWorkflowHandlers` (`QualityObjectiveSlice.cs:91-103`).

| Rule | `Outcome` string (case-insensitive) | `Note` non-empty ≤2000 | `Status == Active` | `OnTarget` | **Outcome** |
|---|---|---|---|---|---|
| R1 | any | **N** | – | – | **400** validation (`Note`) |
| R2 | not in {achieved, missed, cancelled} | Y | – | – | **422** `OBJ-014` |
| R3 | `achieved` | Y | **N** | – | **409** `OBJ-013` |
| R4 | `achieved` | Y | Y | **`null`** (no measurements) | **422** `OBJ-011` |
| R5 | `achieved` | Y | Y | **`false`** | **422** `OBJ-011` |
| R6 | `achieved` | Y | Y | **`true`** | **204**; `status='Achieved'`, `closure_note` set |
| R7 | `missed` | Y | **N** | – | **409** `OBJ-013` |
| R8 | `missed` | Y | Y | any (**not evaluated**) | **204**; `status='Missed'` — an on-target objective **may** be closed as Missed; only *Achieved* is evidence-checked |
| R9 | `cancelled` | Y | **N** | – | **409** `OBJ-013` |
| R10 | `cancelled` | Y | Y | any (**not evaluated**) | **204**; `status='Cancelled'` |

R8 and R10 are the asymmetry worth a case: closure honesty is enforced in **one direction only**.

### 4.5 DT-5 — `OnTarget` evaluation (feeds DT-4 rows R4–R6)

`QualityObjective.cs:104-106`, computed over `CurrentValue` = the value of the update with the greatest `MeasuredOn` (`:100-101`).

| Rule | Any progress updates | `Direction` | Comparison | `OnTarget` |
|---|---|---|---|---|
| R1 | **No** | any | – | **`null`** |
| R2 | Yes | `AtLeast` | `current >= target` | `true` |
| R3 | Yes | `AtLeast` | `current < target` | `false` |
| R4 | Yes | `AtLeast` | `current == target` | **`true`** (inclusive boundary) |
| R5 | Yes | `AtMost` | `current <= target` | `true` |
| R6 | Yes | `AtMost` | `current > target` | `false` |
| R7 | Yes | `AtMost` | `current == target` | **`true`** (inclusive boundary) |

R4 and R7 are the BVA pair; R1 is the MCDC null case that makes DT-4/R4 possible.

### 4.6 DT-6 — Complaint closure (`POST /api/complaints/{id}/close`)

Gate `complaints.void` (`ComplaintsController.cs:81`); gate value computed at `ComplaintCommands.cs:146-148`; aggregate check at `Complaint.cs:155-165`.

| Rule | Authn | Holds `complaints.void` | Exists | `Status == Resolved` | `LinkedNcId` | Linked NC `Closed` | **Outcome** |
|---|---|---|---|---|---|---|---|
| R1 | Y | **N** | – | – | – | – | **403** `AUTHZ-403` |
| R2 | Y | Y | **N** | – | – | – | **404** `CMP-404` |
| R3 | Y | Y | Y | **N** | – | – | **409** `CMP-015` |
| R4 | Y | Y | Y | Y | **null** | – (not evaluated) | **204**; `status='Closed'`; raises `ComplaintClosed` |
| R5 | Y | Y | Y | Y | set | **N** | **422** `CMP-020` |
| R6 | Y | Y | Y | Y | set | **Y** | **204**; `status='Closed'`; raises `ComplaintClosed` |

R4 vs R5 vs R6 is the MCDC set for the two-condition guard `LinkedNcId is not null && !linkedNcClosed`.

### 4.7 DT-7 — Complaint validation verdict and the NC saga

`Complaint.RecordValidationVerdict` (`:99-117`) + `ComplaintToNcPolicy` (`ComplaintToNcPolicy.cs:23-58`). Gate `complaints.approve`.

| Rule | `Status == Acknowledged` | `Reason` non-empty | `justified` | Existing NC with `SourceRef = "CMP:{ref}"` | **Outcome** |
|---|---|---|---|---|---|
| R1 | **N** | – | – | – | **409** `CMP-011` |
| R2 | Y | **N** | – | – | **400** validation (`Reason` NotEmpty ≤2000) |
| R3 | Y | Y | **false** | – | **204**; `status='Invalid'`, `validation_verdict` set; **no event**, **no NC** |
| R4 | Y | Y | **true** | **none** | **204**; `status='Validated'`; `ComplaintValidated` → outbox → saga creates NC (`severity 3, likelihood 3`, RPN 9, source `Complaint`, `Submit()`ed) and back-links `LinkedNcId` |
| R5 | Y | Y | **true** | **exists** (redelivery) | saga heals only: `complaint.LinkNc(existing.Id)`, no second NC. Net exactly one side effect |

R5 is the idempotency case; the natural key is `SourceRef` (`ComplaintToNcPolicy.cs:28-30`) and `LinkNc` is `??=` (`Complaint.cs:170`).

### 4.8 DT-8 — Feedback escalation (`POST /api/feedback/{id}/escalate`)

Gate `feedback.edit` (`FeedbackController.cs:52`); `FeedbackEntry.Escalate` (`:116-130`); handler `FeedbackSlice.cs:101-122`. **No validator** on `EscalateFeedbackCommand`.

| Rule | Holds `feedback.edit` | Exists | `Type == Dissatisfaction` | `Status` | **Outcome** |
|---|---|---|---|---|---|
| R1 | **N** | – | – | – | **403** `AUTHZ-403` |
| R2 | Y | **N** | – | – | **404** `FBK-404` |
| R3 | Y | Y | **N** (Compliment/Suggestion) | **any, incl. Closed/Escalated** | **422** `FBK-014` — the type check runs first |
| R4 | Y | Y | Y | `Closed` or `Escalated` | **409** `FBK-015` |
| R5 | Y | Y | Y | `Logged` | **200** `{complaintId}`; feedback → `Escalated`, `complaint_id` set; a new `complaint` row (`Logged`, channel `Portal`, `confidential=false`) in the same transaction; raises `ComplaintLogged` |
| R6 | Y | Y | Y | `Reviewed` | identical to R5 — **both** pre-terminal states may escalate |

R3 vs R4 is the ordering MCDC pair.

### 4.9 DT-9 — Conflict assessment (`POST /api/conflicts/{id}/assess`)

Gate `conflicts.approve` (`ConflictsController.cs:34`); `ConflictDeclaration.Assess` (`:63-89`); actor from `ICurrentUser` (`ConflictSlice.cs:72-75`).

| Rule | Holds `conflicts.approve` | Exists | `Status == Declared` | `actor != DeclarantId` | `Mitigation` non-empty | `RiskLevel` | **Outcome** |
|---|---|---|---|---|---|---|---|
| R1 | **N** | – | – | – | – | – | **403** `AUTHZ-403` |
| R2 | Y | **N** | – | – | – | – | **404** `COI-404` |
| R3 | Y | Y | **N** | – | – | – | **409** `COI-010` |
| R4 | Y | Y | Y | **N** (self-assessment) | – | – | **422** `SOD-COI-001` |
| R5 | Y | Y | Y | Y | **N** | – | **400** validation (`Mitigation` NotEmpty ≤2000 — shadows `COI-011`) |
| R6 | Y | Y | Y | Y | Y | `Low` or `Medium` | **204**; `status='Assessed'`, `risk_level`, `mitigation`, `assessed_by` written; **no event** |
| R7 | Y | Y | Y | Y | Y | **`High`** | **204** plus `HighImpartialityRiskDeclared` → notification key `COI_HIGH`. **No default rule is seeded for `COI_HIGH`**, so on a freshly provisioned tenant the dispatch finds no rule (`NotificationPolicies.cs:138-156`) |
| R8 | Y | Y | Y | Y | Y | unparsable string | **500** — `Enum.Parse<ConflictRiskLevel>` throws `ArgumentException`, unmatched by `DomainExceptionHandler` (`ConflictSlice.cs:75`) |

R7 and R8 each need their own case; R8 is a defect case, not a design case.

### 4.10 DT-10 — Quality-policy approval (`POST /api/quality-policy/{id}/approve`)

Gate `quality-policy.approve` (`QualityPolicyController.cs:50`); `QualityPolicy.Approve` (`:76-91`); handler supersede loop (`QualityPolicySlice.cs:67-85`). **No validator** on `ApproveQualityPolicyCommand`.

| Rule | Holds `quality-policy.approve` | Exists | `approver == CreatedByUserId` | `CreatedByUserId` is null | `Status == Draft` | **Outcome** |
|---|---|---|---|---|---|---|
| R1 | **N** | – | – | – | – | **403** `AUTHZ-403` |
| R2 | Y | **N** | – | – | – | **404** `QP-404` |
| R3 | Y | Y | **Y** | N | any | **422** `SOD-QP-001` — *even when the policy is Active/Superseded*, because the SoD check precedes the state check |
| R4 | Y | Y | N | **Y** (legacy/system row) | Draft | **204** — the SoD guard is a **no-op**; a self-approval is not detected (GAP-RISK-009) |
| R5 | Y | Y | N | N | **N** (Active/Superseded) | **409** `QP-010` |
| R6 | Y | Y | N | N | Y | **204**; this policy → `Active` with `approved_by_id`, `approved_at_utc`, `effective_date`; **every other `Active` policy in the tenant → `Superseded` in the same transaction**; raises `QualityPolicyApproved`; **no notification** |
| R7 | as R6, but an **older** draft is approved while a **newer** version is Active | | | | | **204** — the newer version is superseded by the older one; no version-ordering guard exists (GAP-RISK-010) |

R3, R4 and R7 are the three cases a naive reading of URS-049 would miss.

> **§5 is intentionally absent.** Detailed test cases live in `15-module-risk-governance-cases-A|B|C|D.md` per the split convention (`00-GROUND-TRUTH-AND-CONVENTIONS.md:192-197`). Writing them here would collide with those files' reserved id blocks.

---

## 6. UAT scenarios (Gherkin)

Business-readable, written for a Quality Manager to sign. Each carries a `TC-RISK-UAT-nnn` id from the reservation, its evidence label, and the URS it traces to. These are acceptance narratives, not step-level scripts — the executable detail is in the case files.

### TC-RISK-UAT-001 — A change cannot be approved without a risk assessment  [IV]
*Traces:* URS-047 · *Source:* `ChangeAndReview.cs:75-78`

```gherkin
Feature: Risk-based change control (ISO 9001 §6.3 / EU Annex 11 §10)

  Scenario: The system refuses to approve an unassessed change
    Given I am signed in to the "demo-lab" workspace as a Quality Manager
      And a change request "CHG-…" titled "Move the HPLC to Room 204" exists in state Proposed
      And no risk assessment has been linked to it
    When I open the change and select "Approve"
    Then the approval is refused
      And the message tells me a linked risk assessment is required
      And the change is still in state Proposed
      And no approval date or approver is recorded against it

  Scenario: Approval succeeds once the risk is linked
    Given the same change request
      And a risk register entry has been linked to it
    When I select "Approve"
    Then the change moves to state Approved
      And my name and the approval date and time are recorded on the change
      And the approval appears in the tenant's audit trail
```

### TC-RISK-UAT-002 — A change is not finished until its effectiveness is verified  [IV]
*Traces:* URS-047 · *Source:* `ChangeAndReview.cs:111-125`; `GovernanceControllers.cs:120-127`

```gherkin
Feature: Post-implementation review of an implemented change

  Scenario: Recording that an implemented change worked
    Given a change request is in state Closed with implementation notes recorded
    When I record a post-implementation review stating the change was effective
      And I supply the supporting notes
    Then the change moves to state Reviewed
      And the record shows the reviewer, the review date, "effective: yes" and the notes
      And the change can no longer be edited, approved, rejected or closed

  Scenario: Recording that an implemented change did NOT work
    Given a change request is in state Closed
    When I record a post-implementation review stating the change was not effective
    Then the change still moves to state Reviewed and becomes read-only
      And "effective: no" is recorded with my notes
    # NOTE for the reviewer: the system offers no follow-up action for an
    # ineffective change — see GAP-RISK-016. Confirm this is acceptable.
```

### TC-RISK-UAT-003 — A high residual risk reaches the quality team  [IV]
*Traces:* URS-048 · *Source:* `RiskItem.cs:33`, `:112-115`; `NotificationPolicies.cs:101-107`, `:150-151`

```gherkin
Feature: Risk register with residual-risk alerting

  Scenario Outline: Residual risk above the acceptance threshold raises an alert
    Given a risk register entry exists with all mitigation actions completed
    When I record a residual assessment of likelihood <L> and impact <I>
    Then the residual risk priority number shown is <RPN>
      And an alert "<alert>" is raised to the Quality Manager and Tenant Administrator

    Examples:
      | L | I | RPN | alert                     |
      | 3 | 4 | 12  | no alert (at the limit)   |
      | 4 | 3 | 12  | no alert (at the limit)   |
      | 3 | 5 | 15  | High residual risk raised |
      | 5 | 5 | 25  | High residual risk raised |

  Scenario: The dashboard count agrees with the register
    Given two open risks have residual RPN above the acceptance threshold
      And one closed risk also has residual RPN above it
    When I open the quality dashboard
    Then "High residual risks" reads 2
    # Closed risks are excluded from the count by design.
```

### TC-RISK-UAT-004 — A risk cannot be closed on an unfinished mitigation plan  [IV]
*Traces:* URS-048 · *Source:* `RiskItem.cs:118-133`

```gherkin
Feature: Honest risk closure

  Scenario: Closure is refused while a mitigation action is outstanding
    Given a risk with two mitigation actions, one of which is not yet complete
      And a residual assessment has been recorded
    When I select "Close risk"
    Then closure is refused
      And the message tells me all mitigation actions must be completed first
      And the risk remains open

  Scenario: Closure is refused before a residual assessment exists
    Given a risk with all mitigation actions complete
      And no residual assessment recorded
    When I select "Close risk"
    Then closure is refused
      And the message tells me a residual assessment is required first
```

### TC-RISK-UAT-005 — The person who wrote the quality policy cannot approve it  [IV]
*Traces:* URS-049 · *Source:* `QualityPolicy.cs:76-91`; `AggregateRoot.cs:36-42`

```gherkin
Feature: Segregated approval of the controlled quality policy (ISO 9001 §5.2)

  Scenario: Self-approval is refused
    Given I drafted quality policy version 3
    When I attempt to approve it
    Then the approval is refused as a segregation-of-duties breach
      And the policy remains a Draft

  Scenario: Approval by a second person activates it and retires the previous version
    Given quality policy version 2 is currently Active
      And version 3 is a Draft written by a colleague
    When I approve version 3 with an effective date of the first of next month
    Then version 3 becomes the policy in force
      And version 2 becomes Superseded
      And exactly one policy is in force
      And every authenticated user can read the policy currently in force
```

### TC-RISK-UAT-006 — An objective cannot be declared achieved against the evidence  [IV]
*Traces:* URS-051 · *Source:* `QualityObjective.cs:122-132`, `:104-106`

```gherkin
Feature: Measurable quality objectives (ISO 9001 §6.2)

  Scenario: Declaring success without meeting the target is refused
    Given an objective "95% of NCs closed within 30 days" with direction "at least" and target 95
      And the latest recorded measurement is 88
    When I close the objective as Achieved
    Then the closure is refused
      And the message tells me the latest measurement does not meet the target

  Scenario: Declaring success with no measurements at all is refused
    Given an objective with no progress measurements recorded
    When I close the objective as Achieved
    Then the closure is refused for the same reason

  Scenario: The objective may honestly be closed as Missed
    Given the same objective with the latest measurement at 88
    When I close it as Missed with the note "Resourcing shortfall in Q3"
    Then the objective is closed as Missed with my note retained
```

### TC-RISK-UAT-007 — A justified complaint automatically opens a nonconformance, and the complaint cannot close before it  [IV]
*Traces:* URS-033 · *Source:* `Complaint.cs:99-117`, `:155-165`; `ComplaintToNcPolicy.cs:23-58`

```gherkin
Feature: Customer complaints linked to investigation (ISO 17025 §7.9)

  Scenario: A justified complaint spawns a nonconformance
    Given a complaint has been logged and acknowledged
    When I record the validation verdict as justified with my reasoning
    Then the complaint moves to Validated
      And a nonconformance is raised automatically, referencing the complaint
      And the nonconformance is linked back onto the complaint

  Scenario: The complaint cannot be closed while its nonconformance is open
    Given the complaint has been investigated, its outcome logged and resolved
      And the linked nonconformance is still open
    When I close the complaint
    Then closure is refused
      And the message tells me the linked nonconformance must be closed first

  Scenario: An unjustified complaint terminates without a nonconformance
    Given a complaint has been logged and acknowledged
    When I record the validation verdict as not justified with my reasoning
    Then the complaint moves to Invalid
      And no nonconformance is created
      And my reasoning is retained on the record
```

### TC-RISK-UAT-008 — A confidential complainant's identity is withheld  [IV]
*Traces:* URS-033 · *Source:* `ComplaintsController.cs:18-19`; `ComplaintCommands.cs:182`, `:202-206`

```gherkin
Feature: Complainant confidentiality

  Scenario: An analyst cannot see a confidential complainant's identity
    Given a complaint was logged with the confidentiality flag set
    When an Analyst opens the complaint list and the complaint detail
    Then the complainant name reads "•••" and no contact details are shown
      And the rest of the complaint is fully readable

  Scenario: The Quality Manager can see it
    Given the same complaint
    When a Quality Manager opens it
    Then the complainant name and contact details are shown in full
    # NOTE for the reviewer: this is decided by the account's structural tier,
    # not by a configurable privilege — see GAP-RISK-005.
```

### TC-RISK-UAT-009 — Dissatisfaction can be escalated into the formal complaint process  [IV]
*Traces:* URS-033 · *Source:* `FeedbackEntry.cs:116-130`; `FeedbackSlice.cs:101-122`

```gherkin
Feature: Feedback beyond formal complaints (ISO 17025 §8.6.2)

  Scenario: Escalating dissatisfaction opens a linked complaint
    Given a feedback entry of type "Dissatisfaction" in state Logged
    When I escalate it and supply the complainant's name
    Then a new complaint is created carrying the feedback subject and details
      And the feedback moves to Escalated and shows the linked complaint
      And both records were saved together — neither exists without the other

  Scenario: A compliment cannot be escalated
    Given a feedback entry of type "Compliment"
    When I attempt to escalate it
    Then the action is refused because only dissatisfaction can be escalated
```

### TC-RISK-UAT-010 — Impartiality: nobody assesses their own conflict  [IV]
*Traces:* URS-051 · *Source:* `ConflictDeclaration.cs:63-89`

```gherkin
Feature: Impartiality / conflict-of-interest register (ISO 17025 §4.1)

  Scenario: A declarant cannot assess their own declaration
    Given I have declared a conflict of interest concerning a supplier
    When I attempt to assess the impartiality risk of my own declaration
    Then the assessment is refused as a segregation-of-duties breach
      And the declaration remains in state Declared

  Scenario: A high-risk assessment surfaces to the quality team
    Given a colleague's conflict declaration is awaiting assessment
    When I assess it as High risk and record the mitigation
    Then the declaration moves to Assessed
      And an impartiality alert is raised for the quality team
      And the declaration can then be closed with an outcome of
          Accepted, Mitigated or Withdrawn and a closure note
    # NOTE for the reviewer: no default notification rule is seeded for the
    # impartiality alert on a new tenant — see GAP-RISK-008.
```

---

## 7. Exploratory charters

Session-based, 90 minutes each unless stated. Each charter names the areas, the specific oracles to lean on, and what a finding looks like. Charters are **not** detailed cases and consume the `TC-RISK-EXPL-nnn` reservation.

### TC-RISK-EXPL-001 — Probe the ungated write surface  [ID]
**Explore** the ten write endpoints that carry no `[RequirePermission]` (§1.5)
**With** a tenant-defined role holding **zero** keys for `risks`, `changes`, `conflicts`, `complaints`, `feedback` and `objectives`, plus a branch-restricted user
**To discover** what an unprivileged internal actor can actually create, mutate and observe.
*Oracles:* `[RequireInternalActor]` should still block `ExternalAuditor` (`CommandAuthorization.cs:14-19`); `OrgScopeGuardInterceptor` should still emit `SCOPE-001`/`SCOPE-002` on out-of-scope branch/department writes (`OrgScopeGuardInterceptor.cs:53-65`); the composed query filter should still hide out-of-scope rows (`AppDbContext.cs:203-210`).
*A finding is:* any successful create/mutate that the privilege matrix screen implies should be impossible; any out-of-scope row that becomes visible or writable; any 500 instead of 403/422.
*Timebox:* 120 minutes. *Feeds:* GAP-RISK-003, GAP-RISK-004.

### TC-RISK-EXPL-002 — Break the one-in-force quality policy  [ID]
**Explore** `POST /api/quality-policy` + `/{id}/approve` under adversarial sequencing
**With** several drafts created out of order, two concurrent approvals of different drafts, an approval of an *older* draft while a newer one is Active, and a draft whose `created_by_user_id` is NULL (set by direct SQL to simulate a legacy row)
**To discover** whether more than one policy can be Active at once, and whether the SoD guard can be evaded.
*Oracles:* the supersede loop at `QualityPolicySlice.cs:74-83`; `ux_quality_policy_tenant_id_version`; `ck_quality_policy_status_domain`; `xmin` → `CONCURRENCY-409`. Verify with `SELECT version, status FROM qams.quality_policy WHERE tenant_id = … ORDER BY version;` (set the tenant GUC first).
*A finding is:* two rows with `status='Active'`; a newer version superseded by an older one; a successful self-approval.
*Feeds:* GAP-RISK-009, GAP-RISK-010, GAP-RISK-011.

### TC-RISK-EXPL-003 — Stress the complaint→NC saga and the closure gate  [ID]
**Explore** the outbox-driven `ComplaintToNcPolicy` under redelivery, failure and interleaving
**With** repeated delivery of the same `ComplaintValidated` event, a validated complaint whose NC is deleted/reopened by another path, two complaints sharing a reference across tenants, and a `Resolve` racing a `Close`
**To discover** duplicate NCs, orphaned or mis-tenanted links, and whether `CMP-020` can be bypassed.
*Oracles:* natural key `SourceRef = "CMP:{ref}"` (`ComplaintToNcPolicy.cs:28-30`); `LinkNc` `??=` (`Complaint.cs:170`); the transactional gate (`ComplaintCommands.cs:146-148`); outbox dead-letter after `MaxAttempts` (`OutboxProcessor.cs:139-145`); `audit.audit_trail` chaining (`OutboxProcessor.cs:122-129`).
*A finding is:* two NCs for one complaint; a complaint closed while its NC is open; an NC written under the wrong `tenant_id`; a gap in the audit hash chain (check `GET /api/compliance/chain-verification`).

### TC-RISK-EXPL-004 — Terminal-state durability without a database trigger  [ID]
**Explore** whether records the domain calls immutable actually resist mutation
**With** a Closed risk, a Reviewed change, a Closed management review, a Superseded policy, an Archived interested party and a Closed context issue — attacked through the API (every operation, in every order), through a replayed `Idempotency-Key`, and through direct SQL as `qams_app`
**To discover** the real boundary of "immutable" in this module.
*Oracles:* the ten matrices in §3; the **absence** of `qams.reject_frozen_mutation` on these tables (`20260726084134_SignedRecordImmutability.cs:16-28`); the `qams_app` grant set; `ck_*_status_domain`.
*A finding is:* any API path that mutates a terminal record; and — expected, and to be documented rather than filed as a surprise — that raw SQL **does** succeed where it is blocked on analytical roots.
*Feeds:* GAP-RISK-006.

### TC-RISK-EXPL-005 — Tenant and scope isolation across the twelve tables  [ID]
**Explore** cross-tenant and cross-scope reachability for all twelve tables plus the three owned children
**With** two tenants each holding every aggregate type; a user restricted to Branch A; unattributed (null-branch) records; and `psql` sessions with the GUC unset, set to the wrong tenant, and with `app.bypass_rls='on'`
**To discover** any read or write that crosses a boundary, and the exact behaviour of the four aggregates that are **not** `IAllocatable`.
*Oracles:* `relrowsecurity`/`relforcerowsecurity` = `t` for all fifteen tables; the canonical policy text (`ActivateForcedTenantRls.cs:33-45`); the tenant-composite FKs (`Hardening4_ChildTenancy.cs:400-423`); the deliberate design that conflicts, quality policy, interested parties and context issues ignore branch/department scope (§1.1).
*A finding is:* any other-tenant row visible or writable; a child row persisting under a parent of another tenant; a **surprise** at the four non-`IAllocatable` aggregates being tenant-wide — record it as expected behaviour, not a defect.

### TC-RISK-EXPL-006 — The register screens as a Quality Manager would actually use them  [ID]
**Explore** the nine SPA routes (`app.routes.ts:72, 76, 86, 96, 170, 180, 190, 194, 204`) end to end
**With** 500+ rows per register, Arabic (RTL) and French locales, keyboard-only navigation, a slow network, and a session that expires mid-form
**To discover** unpaginated-list behaviour, RTL layout breaks, focus traps, lost form state on silent token refresh, and whether refused actions (403 / 409 / 422) render an intelligible message rather than a raw code.
*Oracles:* six of nine list endpoints return **unbounded arrays** with no envelope (§1.8, GAP-RISK-017); `i18n.service.ts` `en`/`ar`/`fr`; `@axe-core/playwright`; the ADR-0009 silent-refresh path.
*A finding is:* a register that stalls or truncates at volume; any axe violation at serious or critical; a 409/422 surfaced to the user as an unexplained code; unsaved work lost on refresh.
*Timebox:* 120 minutes. *Feeds:* GAP-RISK-017.

---

## 8. Gap Register (this module)

Twenty-four gaps, each in the mandatory 9-field format. **Severity** uses High / Medium / Low with the GAMP 5 sense of the FRA (`docs/validation/02-Functional-Risk-Assessment.md:14-42`): High = data-integrity or Part-11 exposure that is hard to detect at the point of use.

---

**GAP-RISK-001 — Change approval carries no segregation of duties**

| Field | Content |
|---|---|
| **Source reference** | Commissioning brief (change control as a controlled, approved act); ISO 9001 §8.5.6; EU Annex 11 §10; 21 CFR 11 §11.10(g). Code: `src/NT.QAMS.Domain/RiskGovernance/ChangeAndReview.cs:72-84` |
| **Description** | `ChangeRequest.Approve(actorId, at)` checks only the state and that `RiskItemId` is non-null. It never compares `actorId` with `ProposedBy` (stamped at `RiskGovernanceSlice.cs:173-174`) and never calls `EnsureSignerIsNotPreparer`. The proposer of a change can approve it. The same absence applies to `ManagementReview.Close` (`ChangeAndReview.cs:213-225`) and `RiskItem.Close` (`RiskItem.cs:118-133`). |
| **Impact** | A single actor can author and authorise a change to a regulated system — the exact control Part 11 §11.10(g) exists to prevent. Quality policy (`SOD-QP-001`) and conflicts (`SOD-COI-001`) are guarded; change control, the most consequential of the three, is not. |
| **Testing limitation** | A negative "self-approval is refused" case cannot be written — it would fail by design. Until resolved, the only honest case is a `[ID]`-labelled case asserting that self-approval **succeeds**, recording current behaviour. |
| **Recommended clarification** | Confirm with the process owner whether change approval must be segregated, and if so whether the guard compares against `ProposedBy` (business author) or `CreatedByUserId` (record preparer). Decide whether a two-person rule also applies to management-review closure. |
| **Suggested acceptance criteria** | (a) `ChangeRequest.Approve` throws a new `SOD-CHG-001` when `actorId == ProposedBy`, surfacing as **422** with that code; (b) the existing `Proposed → Approved` happy path is unaffected when the approver differs; (c) a domain unit test pins both; (d) a functional test asserts `422 SOD-CHG-001` over HTTP; (e) the code is added to the module's error-code table and the SPA renders a human message for it. |
| **Severity** | **High** |
| **Responsible role** | Quality Manager (process decision) + Development Lead (implementation) |

---

**GAP-RISK-002 — No electronic signature on any governance approval, though `.sign` keys exist**

| Field | Content |
|---|---|
| **Source reference** | 21 CFR 11 subpart C; brief's expectation that approvals are signed. Code: `PermissionCatalog.cs:141`, `:142`, `:146` (the three `SignedRecordLifecycle` modules); zero `PermissionAction.Sign` gates in the nine controllers |
| **Description** | `changes`, `reviews` and `quality-policy` are declared `SignedRecordLifecycle`, so `changes.sign`, `reviews.sign` and `quality-policy.sign` are real, grantable, storable keys. No endpoint in the build consumes any of them, and `ESignatureService` (`src/NT.QAMS.Infrastructure/Compliance/ComplianceLedgerServices.cs:87+`) is never invoked from this module. Approving a change or a quality policy requires no password and no signature PIN. |
| **Impact** | Three permission keys are grantable but unenforceable — a laboratory can configure a role it believes restricts signing when nothing is gated. Governance approvals produce no `SignatureRecord`, so they do not appear in the signature manifest export and cannot evidence a Part-11 signed approval. |
| **Testing limitation** | No `SIG-001` / `SIG-002` / `SIG-003` case can be written for this module. The signature-manifest completeness case must explicitly exclude governance records and say why. |
| **Recommended clarification** | Confirm which governance acts are *signature* events under the site's Part-11 scope — candidates are quality-policy approval, change approval, PIR sign-off and management-review closure. If none are, remove the three `.sign` keys so the catalogue stops advertising an unimplemented capability. |
| **Suggested acceptance criteria** | Either (a) each nominated endpoint requires the module `.sign` key **and** a successful `ESignatureService.SignAsync` (password + PIN) before the transition, writing a `SignatureRecord` with `Meaning` and `SubjectRef`, and failures return `SIG-001` / `SIG-002` / `SIG-003`; **or** (b) the three modules are re-declared with an action bundle that omits `Sign`, `PermissionCatalog.AllKeys` drops from 170 to 167, and `SystemRoleCatalog` parity tests are updated in the same commit. |
| **Severity** | **High** |
| **Responsible role** | Quality Manager / Validation Lead (scope decision) + Development Lead |

---

**GAP-RISK-003 — Seventeen of eighteen read endpoints enforce no `*.view` privilege**

| Field | Content |
|---|---|
| **Source reference** | URS-095 (authorization governed by the permission catalogue); CLAUDE.md standing rule 9. Code: §1.5 tables — `GovernanceControllers.cs:17`, `:24`, `:71`, `:78`, `:135`, `:141`; `ConflictsController.cs:17`, `:21`; `OrgContextController.cs:19`, `:53`; `QualityObjectivesController.cs:17`, `:21`; `ComplaintsController.cs:21`, `:25`; `FeedbackController.cs:17`, `:22` |
| **Description** | Only `GET /api/quality-policy` carries `[RequirePermission(…, View)]` (`QualityPolicyController.cs:32`). Every other GET in the module is `[Authorize]`-only, so `risks.view`, `changes.view`, `reviews.view`, `conflicts.view`, `org-context.view`, `objectives.view`, `complaints.view` and `feedback.view` have no enforcement point. Any authenticated user in the tenant — including one whose role grants none of these — reads the entire risk register, complaint register and impartiality register (subject only to branch/department scope on the six `IAllocatable` aggregates). |
| **Impact** | The privilege-matrix screen misrepresents the system: an administrator who removes `complaints.view` from a role changes nothing. Impartiality declarations and complainant subject matter are readable by every account in the tenant. |
| **Testing limitation** | Every "role X cannot read register Y" negative case is unwritable. The RBAC matrix for this module can only be authored for the 26 gated endpoints; the other 33 must be documented as ungated, which weakens the URS-095 traceability evidence. |
| **Recommended clarification** | Confirm whether read access to these registers is intended to be universal within a tenant (a defensible design for a quality system) or privilege-controlled. If universal, the eight `*.view` keys should be removed or explicitly documented as advisory. |
| **Suggested acceptance criteria** | (a) Each GET carries `[RequirePermission(<module>, PermissionAction.View)]`; (b) a role without the key receives **403** `AUTHZ-403` in `application/problem+json`; (c) the seeded `SystemRoleCatalog` grants keep every current audience's reach unchanged (parity test); (d) an architecture test asserts every endpoint under these nine controllers carries a permission attribute, so the omission cannot recur; (e) `ApiSurface.approved.txt` is unchanged (no route change). |
| **Severity** | **Medium** |
| **Responsible role** | Security Architect + Development Lead |

---

**GAP-RISK-004 — Ten write endpoints enforce no privilege, including all record creation**

| Field | Content |
|---|---|
| **Source reference** | URS-095; URS-078 (deny-by-default command authorization). Code: `GovernanceControllers.cs:28`, `:37`, `:42`, `:82`, `:90`, `:113`; `ConflictsController.cs:25`; `ComplaintsController.cs:29`; `FeedbackController.cs:26`; `QualityObjectivesController.cs:36` |
| **Description** | `POST /api/risks`, `/api/risks/{id}/actions`, `/api/risks/{id}/actions/{actionId}/complete`, `POST /api/changes`, `/api/changes/{id}/risk`, `/api/changes/{id}/close`, `POST /api/conflicts`, `POST /api/complaints`, `POST /api/feedback` and `POST /api/quality-objectives/{id}/progress` carry no `[RequirePermission]`. The commands carry `[RequireInternalActor]`, which excludes only `ExternalAuditor`. So `risks.create`, `risks.edit`, `changes.create`, `changes.edit`, `conflicts.create`, `complaints.create`, `feedback.create` and `objectives.edit` are unenforced. A sub-case is the asymmetry noted in D-26: a user with **no** risk privileges may raise risks and add mitigations, while recording the residual needs `risks.approve` and closing needs `risks.void`. `POST /api/changes/{id}/close` is the most consequential — it performs a lifecycle transition on a regulated record with no privilege at all. |
| **Impact** | Deny-by-default is defeated at the HTTP layer for record creation across six registers. An Analyst-tier account can declare conflicts of interest in another person's name (`DeclareConflictCommand` takes `DeclarantId` from the request body — `ConflictSlice.cs:14-15`), and can close an approved change. |
| **Testing limitation** | The RBAC negative matrix cannot be authored for these ten routes. Any case asserting "creation requires `<module>.create`" would fail; the honest alternative is an `[ID]` case recording that it does not. |
| **Recommended clarification** | Confirm the intended audience per endpoint against the pre-v1.51.0 fixed-tier gates these routes replaced (the parity table in `SystemRoleCatalog.cs:82-92` is the reference), and separately confirm whether `DeclareConflictCommand.DeclarantId` should be forced to the current user rather than accepted from the body. |
| **Suggested acceptance criteria** | (a) Each of the ten endpoints carries the correct `[RequirePermission]`: creates → `Create`, mitigation add/complete → `Edit`, link-risk → `Edit`, change close → `Edit` or a new decision, progress → `Edit`; (b) an unprivileged role receives **403** `AUTHZ-403`; (c) seeded-role parity is preserved; (d) an architecture test fails CI if any endpoint in these nine controllers lacks a permission attribute; (e) separately, `DeclareConflictCommand` either drops `DeclarantId` in favour of `ICurrentUser`, or declaring on behalf of another person requires `conflicts.create`. |
| **Severity** | **High** |
| **Responsible role** | Security Architect + Development Lead |

---

**GAP-RISK-005 — Complaint confidentiality is decided by the legacy tier claim, not a privilege**

| Field | Content |
|---|---|
| **Source reference** | URS-095; CLAUDE.md standing rule 9 ("do not add new `[Authorize(Roles=…)]` gates to tenant endpoints"). Code: `ComplaintsController.cs:18-19`; masking at `ComplaintCommands.cs:182`, `:202-206`; claim minted at `SecurityAdapters.cs:88` |
| **Description** | `CanViewConfidential => User.IsInRole("QualityManager") \|\| User.IsInRole("TenantAdmin")` reads the `UserRole` structural-tier claim. Since v1.51.0 that enum is explicitly **not** the authorization mechanism. There is no `complaints.view-confidential` key in the catalogue, so a tenant-defined role granted every `complaints.*` key still sees `•••`, and a tier-`QualityManager` account whose role grants **no** complaint keys still sees the identity in full (because the GETs are ungated — GAP-RISK-003 compounds this). |
| **Impact** | Confidential reporter identity — the most privacy-sensitive field in the module — is the one thing a laboratory cannot configure. It is also the last role-string check on a tenant endpoint in this module, so it silently reintroduces the pattern v1.51.0 removed. |
| **Testing limitation** | The masking case must be written against the tier, not the privilege, and labelled `[ID]`. It cannot be traced to URS-095. |
| **Recommended clarification** | Confirm whether confidentiality release should be a grantable privilege. If yes, agree the key name and whether it belongs to `complaints` (a ninth action) or is modelled as `complaints.approve` reuse. |
| **Suggested acceptance criteria** | (a) A catalogued key controls the release — e.g. a new `PermissionAction` on the `complaints` module, added to `PermissionCatalog.Modules` with `AllKeys` and the URS-095 count updated in the same commit; (b) `CanViewConfidential` reads `IUserPrivileges.Has(...)`, not `User.IsInRole`; (c) seeded `QualityManager` and `TenantAdministrator` receive the key so no current audience loses reach; (d) a functional test pins masked vs unmasked for a role with and without the key; (e) no `User.IsInRole` call remains in any tenant controller. |
| **Severity** | **Medium** |
| **Responsible role** | Security Architect + Data Protection Officer |

---

**GAP-RISK-006 — Terminal governance records have no database-level immutability**

| Field | Content |
|---|---|
| **Source reference** | URS-041/042 (signed-record immutability), applied by analogy; the aggregates' own XML docs ("A reviewed change is fully terminal and immutable" `ChangeAndReview.cs:109`; "closed minutes are immutable" `:232`; "an active or superseded policy is immutable" `QualityPolicy.cs:14-15`; "A closed risk is immutable" `RiskItem.cs:139`). Code: `20260726084134_SignedRecordImmutability.cs:16-28`, applied `:56-64` |
| **Description** | `qams.reject_frozen_mutation()` is attached to the twelve analytical study roots plus `uncertainty_budget` only. None of `risk_item`, `change_request`, `management_review`, `quality_policy`, `conflict_declaration`, `interested_party`, `context_issue`, `quality_objective`, `complaint` or `feedback_entry` carries it. Immutability in this module exists only in C# guards. |
| **Impact** | Any path that reaches the database outside the aggregate — a future migration, a data-fix script, a defective handler, or a compromised `qams_app` session — can silently reverse a terminal state. The documented control ("immutable") is stronger than the implemented one, which is itself a validation finding. |
| **Testing limitation** | The DB-layer negative case that exists for analytical records (raw `UPDATE` rejected) cannot be written here; only the API-layer 409 can be asserted. An honest OQ record must state that immutability for governance records is application-enforced. |
| **Recommended clarification** | Confirm which governance terminal states warrant a trigger. Candidates in descending value: `change_request.status IN ('Closed','Reviewed')`, `quality_policy.status IN ('Active','Superseded')`, `management_review.status='Closed'`, `risk_item.status='Closed'`. Note the trigger helper takes a single frozen value, so multi-value states need either two triggers or a helper change. |
| **Suggested acceptance criteria** | (a) A migration attaches `frozen_immutability` BEFORE UPDATE OR DELETE to each nominated table/value pair, following the existing `Frozen` tuple pattern; (b) the transition **into** the frozen state still succeeds; (c) a raw `UPDATE`/`DELETE` on a frozen row raises `check_violation`; (d) an integration test proves both halves; (e) the down migration drops exactly what the up created. |
| **Severity** | **Medium** |
| **Responsible role** | Database Architect + Validation Lead |

---

**GAP-RISK-007 — `HighResidualRisk` re-fires on every re-assessment and never clears**

| Field | Content |
|---|---|
| **Source reference** | URS-048; brief's dashboard-alert requirement. Code: `RiskItem.cs:103-116`; dispatch `NotificationPolicies.cs:101-107` |
| **Description** | `RecordResidualAssessment` raises `HighResidualRisk` unconditionally whenever the new residual RPN exceeds 12. Re-recording the same assessment three times produces three events, three outbox rows, three audit-chain entries and three notifications. There is no state check ("was it already high?"), no dedup, and no counterpart event when a re-assessment brings the residual to ≤ 12 — so a risk that was alerted and then genuinely mitigated leaves a standing alert with nothing to retract it. |
| **Impact** | Alert fatigue on the exact signal the register exists to surface, plus an audit trail that suggests repeated deteriorations where there was one. Recipients cannot distinguish a new high residual risk from a clerical correction. |
| **Testing limitation** | A "notified exactly once" case is unwritable. The idempotency assertion available elsewhere in the system (e.g. the NC saga's natural key) has no analogue here. |
| **Recommended clarification** | Confirm the intended semantics: fire on *transition* into high, or on every assessment? And is a "risk no longer high" notification wanted? |
| **Suggested acceptance criteria** | (a) The event is raised only when the residual **crosses** the threshold — i.e. previous residual was null or ≤ 12 and the new one is > 12; (b) re-recording an unchanged high residual raises nothing; (c) optionally a `ResidualRiskAcceptable` event fires on the downward crossing, with a seeded notification rule; (d) domain unit tests pin all three transitions plus the two boundary values (12 → no event, 13+ → event); (e) the `HighResidualRisks` KPI is unaffected. |
| **Severity** | **Medium** |
| **Responsible role** | Quality Manager (semantics) + Development Lead |

---

**GAP-RISK-008 — Six governance events raise no notification, and `COI_HIGH` has no seeded rule**

| Field | Content |
|---|---|
| **Source reference** | URS-047, URS-049, URS-050, URS-051. Code: `NotificationPolicies.cs:24-33` (the ten handled event types), seed array `:138-156` |
| **Description** | `ChangeApproved`, `ChangePostImplementationReviewed`, `ReviewClosed`, `QualityPolicyApproved`, `RiskClosed` and the four complaint lifecycle events (`ComplaintLogged`, `ComplaintAcknowledged`, `ComplaintResolved`, `ComplaintClosed`) have **no** `INotificationHandler`. They reach the outbox and are chained into `audit.audit_trail`, but nobody is told. Separately, `HighImpartialityRiskDeclared` **is** handled (key `COI_HIGH`, `:82-87`) but `COI_HIGH` is **absent from the default rule set seeded on tenant provisioning** (`:138-156` seeds eight keys and omits it), so on a new tenant a high impartiality risk dispatches against no rule until an administrator creates one by hand. `QualityObjective`, `FeedbackEntry`, `InterestedParty` and `ContextIssue` raise no events at all. |
| **Impact** | A newly approved quality policy — the document every employee must be aware of (ISO 9001 §5.2 requires it be communicated) — notifies nobody. A high impartiality threat, the one governance alert that *is* wired, is silent on any tenant nobody has hand-configured. |
| **Testing limitation** | "Expected Notification" must read `n/a — no notification is defined for this event` on the great majority of this module's cases, which is weak evidence for the URS-047/049/051 communication expectations. |
| **Recommended clarification** | Agree which governance events warrant a notification and to whom. `QualityPolicyApproved` (all users) and `HighImpartialityRiskDeclared` (QM + TenantAdmin) are the strongest candidates; `ChangeApproved` and `ReviewClosed` are likely. |
| **Suggested acceptance criteria** | (a) `COI_HIGH` is added to the seeded default rules with a subject/body template, and existing tenants are backfilled idempotently; (b) each nominated event gains a handler in `NotificationEventPolicies` with a `…Key` constant and a seeded rule; (c) `SeedDefaultNotificationRulesPolicy` remains idempotent (its `AnyAsync` guard at `:133-136` must not skip the backfill for tenants that already have *some* rules — this needs an explicit per-key upsert); (d) a test asserts that every `…Key` constant has a seeded default. |
| **Severity** | **Medium** |
| **Responsible role** | Quality Manager + Development Lead |

---

**GAP-RISK-009 — Quality-policy SoD is a no-op when the preparer is unknown**

| Field | Content |
|---|---|
| **Source reference** | URS-049 ("enforcing signer ≠ preparer"); FRA §3 item 5. Code: `AggregateRoot.cs:36-42`; invoked `QualityPolicy.cs:78` |
| **Description** | `EnsureSignerIsNotPreparer` compares only when `CreatedByUserId is { } preparer`. A `quality_policy` row whose `created_by_user_id` is NULL — a row created before the column existed (added by `CreatedByUserIdForSoD`, migration `20260726192118`), or by any background/system path — can be approved by its own author with no refusal. This is the accepted residual **F-05b** at the framework level, but URS-049 states the quality-policy rule unconditionally. |
| **Impact** | The single SoD control the URS names for this module is conditional on a nullable column. On legacy or migrated data the control is absent and the absence is invisible — nothing logs that the check was skipped. |
| **Testing limitation** | The URS-049 negative case is only valid for rows with a non-null `created_by_user_id`. A second, `[ID]`-labelled case must record that a NULL-preparer row is self-approvable, and the OQ record must state the precondition. |
| **Recommended clarification** | Confirm whether quality-policy approval should **fail closed** when the preparer is unknown, or whether F-05b's residual acceptance extends here. Determine how many `quality_policy` rows currently have a NULL `created_by_user_id` in production. |
| **Suggested acceptance criteria** | (a) Either `QualityPolicy.Approve` refuses with `SOD-QP-001` (or a distinct code) when `CreatedByUserId is null`, **or** the residual is documented in the validation record with the measured row count; (b) if fail-closed is chosen, a data check confirms zero NULL-preparer drafts before deployment; (c) `DraftQualityPolicyHandler` is confirmed to stamp `CreatedByUserId` on every new draft; (d) a domain unit test covers null-preparer, same-preparer and different-preparer. |
| **Severity** | **Medium** |
| **Responsible role** | Validation Lead + Development Lead |

---

**GAP-RISK-010 — Approving an older draft supersedes a newer active policy**

| Field | Content |
|---|---|
| **Source reference** | URS-049 ("one-in-force"); ISO 9001 §5.2. Code: `QualityPolicySlice.cs:74-83` |
| **Description** | The approval handler loads **every** policy with `Status == Active && Id != policy.Id` and calls `Supersede()` on each, then approves the target. There is no comparison of version numbers. Because drafts are minted as `max(version)+1` per tenant (`:41-49`), several drafts can coexist; approving version 4 while version 6 is Active retires 6 in favour of 4, and the register then shows a Superseded version numerically above the Active one. |
| **Impact** | The controlled quality policy — the document with the strongest "current version" expectation in the QMS — can silently regress to an earlier statement. `GET /api/quality-policy/active` would then serve the older text to every user. |
| **Testing limitation** | A "the newest approved version is always the one in force" case cannot pass; only the weaker "exactly one is Active" can be asserted. |
| **Recommended clarification** | Confirm whether approving an out-of-order draft should be refused outright, or permitted with an explicit warning and audit reason. |
| **Suggested acceptance criteria** | (a) Approval is refused (new code, e.g. `QP-013`, **422**) when any policy with a higher `Version` is already `Active`; (b) the in-order path is unaffected; (c) an application unit test covers approve-newer-over-older (succeeds), approve-older-over-newer (refused) and approve-first-ever (succeeds, nothing to supersede); (d) after any successful approval, exactly one row has `status='Active'` and it holds the greatest approved version. |
| **Severity** | **Medium** |
| **Responsible role** | Quality Manager + Development Lead |

---

**GAP-RISK-011 — "One policy in force" has no database constraint**

| Field | Content |
|---|---|
| **Source reference** | URS-049. Code: `20260726203026_QualityPolicy.cs:39-57` (three indexes: unique `(tenant_id, policy_ref)`, unique `(tenant_id, version)`, plain `(tenant_id, status)`); enforcement only at `QualityPolicySlice.cs:74-83` |
| **Description** | The one-in-force invariant lives entirely in one application handler. There is no partial unique index such as `CREATE UNIQUE INDEX ux_quality_policy_one_active ON qams.quality_policy (tenant_id) WHERE status = 'Active'`. Two concurrent approvals of different drafts each read the "current active" set before the other commits; the `xmin` token protects each *row* that is written but does not prevent two different rows both becoming Active. |
| **Impact** | A concurrent-approval race can leave two Active policies, after which `GET /api/quality-policy/active` returns whichever `FirstOrDefaultAsync` finds (`QualityPolicySlice.cs:114-120`) — non-deterministic, and undetectable without a manual query. |
| **Testing limitation** | The race can only be probed exploratorily (TC-RISK-EXPL-002); there is no deterministic assertion to write until the constraint exists. |
| **Recommended clarification** | Confirm that a partial unique index is acceptable (it is compatible with the tenant-first composite PK strategy because it includes `tenant_id`, per the schema-hardening rule in CLAUDE.md §5). |
| **Suggested acceptance criteria** | (a) A migration adds the partial unique index on `(tenant_id) WHERE status = 'Active'`, with the transaction-local `app.bypass_rls` set as required for FORCE-RLS tables; (b) an integration test running two concurrent approvals ends with exactly one Active row and the loser receiving a deterministic error (`CONCURRENCY-409` or a unique-violation mapped to a domain code, not a raw 500); (c) the existing supersede-then-approve flow still passes; (d) `Down()` drops the index. |
| **Severity** | **Medium** |
| **Responsible role** | Database Architect + Development Lead |

---

**GAP-RISK-012 — A change may be approved against a closed risk assessment**

| Field | Content |
|---|---|
| **Source reference** | URS-047; ISO 9001 §6.1 risk-based thinking. Code: `ChangeAndReview.cs:66-70`, `:75-78`; `RiskGovernanceSlice.cs:225-237` |
| **Description** | `LinkRiskHandler` verifies only that the risk id exists in the caller's tenant and scope (`db.Risks.AnyAsync`). `LinkRiskAssessment` stores it without inspecting the risk's status, and `Approve`'s `CHG-012` guard tests only for null. A `Closed` risk — or one still `Identified` with no residual assessment — fully satisfies the invariant. The link can also be overwritten repeatedly while the change is `Proposed`, with no history of what was linked before beyond the field-change ledger. |
| **Impact** | The module's headline invariant ("a change cannot be approved without a linked risk assessment") is satisfiable by a formality. A change can be approved against a risk that was closed years earlier and never re-assessed for this change. |
| **Testing limitation** | A meaningful positive case ("approval requires a *current* assessment") cannot be written; only the null-check case can. |
| **Recommended clarification** | Confirm what makes a linked assessment adequate: must the risk be non-`Closed`? Must it carry a residual assessment? Must it have been created or re-assessed after the change was proposed? |
| **Suggested acceptance criteria** | (a) `LinkRiskHandler` (or the aggregate, via a passed-in fact) refuses a link to a risk in a state the process owner rejects, with a distinct code and **422**; (b) `Approve` re-validates the linked risk's state at approval time, not only at link time; (c) unit and functional tests cover link-to-closed-risk, approve-after-the-linked-risk-was-closed, and the happy path; (d) an existing change whose linked risk has since closed does not break on read. |
| **Severity** | **Medium** |
| **Responsible role** | Quality Manager + Development Lead |

---

**GAP-RISK-013 — No date validity rules on due dates or objective periods**

| Field | Content |
|---|---|
| **Source reference** | Brief's expectation of actionable due dates. Code: `RiskItem.cs:81-93` (`AddMitigationAction`), `ChangeAndReview.cs:200-211` (`AddDecision`), `QualityObjective.cs:68-97` (`Define`) |
| **Description** | `MitigationAction.DueDate` and `ReviewDecision.DueDate` accept any `DateOnly`, including dates far in the past; neither command has a validator (§1.6). `QualityObjective.Define` enforces only `periodEnd > periodStart` (`OBJ-002`) — a period entirely in the past, or centuries long, is accepted. `ObjectiveProgressUpdate.MeasuredOn` is likewise unbounded and is not required to fall inside the objective's period. `ManagementReview.ReviewDate` has no bound at all. |
| **Impact** | Overdue-by-construction actions and measurements attributed outside the period they purport to measure. Any future overdue report would be seeded with meaningless data. |
| **Testing limitation** | BVA cases around "due date must be in the future" are unwritable; only "any date is accepted" can be recorded, as `[ID]`. |
| **Recommended clarification** | Confirm the intended rules: must a due date be today or later at creation? Must a measurement fall within `[PeriodStart, PeriodEnd]`? Is back-dating permitted with a reason (as QC target changes are)? |
| **Suggested acceptance criteria** | (a) Validators reject a `DueDate` before the current date from `IClock` (never `DateTime.Now`) unless back-dating is explicitly permitted; (b) `RecordProgress` rejects a `MeasuredOn` outside the objective period, or accepts it with an explicit flag; (c) `ScheduleReviewCommand` bounds `ReviewDate` to an agreed window; (d) BVA tests at each boundary (yesterday / today / tomorrow, `PeriodStart - 1` / `PeriodStart` / `PeriodEnd` / `PeriodEnd + 1`). |
| **Severity** | **Low** |
| **Responsible role** | Quality Manager + Development Lead |

---

**GAP-RISK-014 — No overdue sweep for mitigation actions or review decisions**

| Field | Content |
|---|---|
| **Source reference** | URS-047/048/050; brief's expectation that assigned actions are chased. Code: `ScheduledSweepService` covers calibration-due, grace-lockout, competency-expiry and supplier-suspension only (ground truth `00-GROUND-TRUTH-AND-CONVENTIONS.md:108`); no reference to `mitigation_action` or `review_decision` |
| **Description** | `MitigationAction` carries `OwnerId` and `DueDate` (`RiskItem.cs:20-21`) and `ReviewDecision` carries the same (`ChangeAndReview.cs:150-151`), but nothing ever reads them against the clock. There is no escalation timer, no notification, no task creation and no overdue query. `Completed` is set only by an explicit API call. |
| **Impact** | Assigning an owner and a due date creates an expectation the system does not honour. Management-review decisions — the ISO 9001 §9.3 output that most needs follow-through — can lapse with no signal. |
| **Testing limitation** | No escalation or overdue-notification case can be written for this module. The CAPA-side escalation machinery (`EscalationTimer`, `EscalationToTaskPolicy`) is not wired to these tables. |
| **Recommended clarification** | Confirm whether governance actions should join the existing sweep, and whether they should raise notifications, create `WorkTask` rows, or both. Note that reusing the CAPA `EscalationTimer` would bring its fixed 24/48/72-hour ladder with it. |
| **Suggested acceptance criteria** | (a) `ScheduledSweepService` gains an idempotent pass over open `mitigation_action` and `review_decision` rows past their due date; (b) each overdue item produces exactly one notification per escalation step (re-running the sweep is a no-op); (c) the pass runs per tenant under the existing elevation and leader election; (d) an application unit test with a fake clock proves idempotency across three consecutive runs. |
| **Severity** | **Medium** |
| **Responsible role** | Quality Manager + Development Lead |

---

**GAP-RISK-015 — Objective progress series has no uniqueness, ordering or correction path**

| Field | Content |
|---|---|
| **Source reference** | URS-051; data-integrity expectation for a measured series. Code: `QualityObjective.cs:100-101`, `:108-120`; table `objective_progress` (`20260725080545_ObjectivesAndFeedback.cs:77-99`) |
| **Description** | `RecordProgress` appends without checking for an existing update on the same `MeasuredOn`. `CurrentValue` takes the first row after `OrderByDescending(u => u.MeasuredOn)`, so with two updates on the same date the "current" value is whichever the provider returns first — unspecified and potentially unstable between reads. There is no edit, void or correction operation, so a mis-keyed measurement can only be countered by adding another. `objective_progress` has no unique index on `(tenant_id, objective_id, measured_on)`. |
| **Impact** | `OnTarget` — the value the `OBJ-011` closure-honesty guard depends on — can be non-deterministic. An objective could be closable as Achieved on one read and refused on the next, with no data change. |
| **Testing limitation** | The `OnTarget`/DT-5 cases must avoid duplicate dates entirely, so the duplicate scenario can only be written as an `[ID]` case documenting the indeterminacy. |
| **Recommended clarification** | Confirm whether one measurement per date is the rule, and how a mis-entered measurement is corrected — a void with reason (consistent with `QcProfile.UpdateTargets` requiring `QC-012`), or a superseding entry. |
| **Suggested acceptance criteria** | (a) A deterministic tiebreak (e.g. `ThenByDescending(u => u.Id)` on the UUIDv7, which is time-ordered) so `CurrentValue` is stable; **and/or** (b) a unique index on `(tenant_id, objective_id, measured_on)` with a domain code for the duplicate; (c) if corrections are required, a void path capturing a reason and preserving the original row (append-only); (d) tests pin `CurrentValue` and `OnTarget` for same-date pairs. |
| **Severity** | **Medium** |
| **Responsible role** | Quality Manager + Development Lead |

---

**GAP-RISK-016 — `Rejected` and `Reviewed` changes are absolute terminals with no follow-up**

| Field | Content |
|---|---|
| **Source reference** | URS-047; ISO 9001 §8.5.6. Code: `ChangeAndReview.cs:66-133` (all five guard call sites) |
| **Description** | No method on `ChangeRequest` accepts `Rejected` or `Reviewed` as its expected state, and there is no reopen, revise-and-resubmit, or supersede path. A rejected change must be re-proposed as a brand-new record with no link to the original. More importantly, a post-implementation review recording `ChangeEffective = false` still moves the change to `Reviewed` and freezes it — the system records that a change did not work and then offers nothing to do about it. |
| **Impact** | The corrective loop ISO 9001 §8.5.6 implies is absent. An ineffective change produces a terminal record and no obligation. Rejected proposals lose their lineage, weakening change history. |
| **Testing limitation** | No case can assert "an ineffective change triggers follow-up". The DT-2 R7 case can only record that the change becomes terminal. |
| **Recommended clarification** | Confirm the required behaviour for an ineffective PIR: raise a nonconformance automatically (as the complaint saga does), create a task, or require a new linked change request. Separately, confirm whether rejected changes should be re-proposable with a `SupersedesChangeId` link. |
| **Suggested acceptance criteria** | (a) `ChangePostImplementationReviewed` with `Effective = false` triggers a defined follow-up — the strongest option being an automatic NC with `SourceRef = "CHG:{ref}"`, idempotent by that natural key, mirroring `ComplaintToNcPolicy`; (b) the follow-up is visible from the change record; (c) an effective PIR triggers nothing; (d) redelivery of the event produces exactly one follow-up; (e) if a re-propose link is agreed, a nullable `SupersedesChangeId` is added with a tenant-composite FK. |
| **Severity** | **Medium** |
| **Responsible role** | Quality Manager + Development Lead |

---

**GAP-RISK-017 — Six of nine register lists are unpaginated and uncapped**

| Field | Content |
|---|---|
| **Source reference** | URS-079/API-004 pagination envelope (Phase 4, v1.42). Code: §1.8 — `ConflictSlice.cs:107-113`, `QualityPolicySlice.cs:99-105`, `QualityObjectiveSlice.cs:129-139`, `ComplaintCommands.cs:177-184`, `FeedbackSlice.cs:152-158`, `OrgContextSlice.cs:77-83`, `:176-182` |
| **Description** | Only `GetRisksQuery`, `GetChangesQuery` and `GetReviewsQuery` return `PagedResponse<T>` via `ToPagedAsync(PageRequest.Normalized(...))`. Conflicts, quality policies, objectives, complaints, feedback, interested parties and context issues return bare `IReadOnlyList<T>` with no page, no size, no total and no cap. `GetQualityObjectivesHandler` additionally materialises the whole filtered set **with its `Updates` collection** before projecting, because `CurrentValue`/`OnTarget` are domain-computed (`QualityObjectiveSlice.cs:129-139`). |
| **Impact** | Unbounded response growth and unbounded server memory on the complaint and feedback registers, which grow fastest. Inconsistent client contracts across one module: three endpoints return an envelope and six return an array, so the SPA cannot use one pager. The objectives query is O(objectives × updates) in memory. |
| **Testing limitation** | Performance and pagination-contract cases can only be written for three of the nine lists. A volume case on the other six can only record degradation, not assert a bound. |
| **Recommended clarification** | Confirm that the API-004 envelope is intended to be universal (the Phase-4 record implies it is) and agree the default page size for these six. |
| **Suggested acceptance criteria** | (a) Each of the six returns `PagedResponse<T>` with `page`, `pageSize`, `total`, normalised through `PageRequest.Normalized`; (b) `ApiSurface.approved.txt` is updated in the same commit if any signature changes; (c) the objectives handler projects `CurrentValue`/`OnTarget` after paging, not before, so at most one page of `Updates` is materialised; (d) the SPA list components consume the envelope through the shared pager; (e) a perf smoke test asserts a bounded response at 5,000 rows. |
| **Severity** | **Medium** |
| **Responsible role** | Development Lead |

---

**GAP-RISK-018 — Complaint acknowledgement has no timeliness rule**

| Field | Content |
|---|---|
| **Source reference** | ISO 17025 §7.9 (complaints process must be available and responsive); URS-033. Code: `Complaint.cs:86-92`; `AcknowledgedAtUtc` read only into the DTO at `ComplaintCommands.cs:208` |
| **Description** | `AcknowledgedAtUtc` is stamped from `IClock` when the complaint is acknowledged, and is then never used for anything but display. There is no target interval, no `SlaDefinition` binding for the `complaints` module, no timer, no escalation and no overdue view. A complaint can sit in `Logged` indefinitely with no signal. `LoggedAtUtc` is likewise never compared to closure. |
| **Impact** | The lab cannot demonstrate responsiveness to complaints — an accreditation-visible expectation — and cannot detect a complaint that was received and forgotten. |
| **Testing limitation** | No SLA, escalation or ageing case can be written for complaints. The "Expected Notification" field on every complaint case reads `n/a`. |
| **Recommended clarification** | Confirm the acknowledgement target (commonly 1–3 working days) and whether it should be tenant-configurable via `SlaDefinition(Module, Severity, TargetHours)`, which already exists but is unused (see `docs/testing/14-module-nc-capa.md` §0, which records `SlaDefinition` as orphaned configuration). Also confirm whether working days or calendar hours apply. |
| **Suggested acceptance criteria** | (a) An acknowledgement target is configurable per tenant; (b) a complaint unacknowledged past the target is surfaced — at minimum in a dashboard count, ideally as a notification; (c) the sweep pass is idempotent; (d) the elapsed time is derived from `LoggedAtUtc` and the injected `IClock`, never `DateTime.Now`; (e) a test with a fake clock pins on-target, at-target and overdue. |
| **Severity** | **Medium** |
| **Responsible role** | Quality Manager + Development Lead |

---

**GAP-RISK-019 — Risk register entries have no owner and no review cadence**

| Field | Content |
|---|---|
| **Source reference** | URS-048 ("risk items and a risk register"); ISO 9001 §6.1; EU Annex 11 §1 (risk management is ongoing). Code: whole-file read `RiskItem.cs:44-58` |
| **Description** | `RiskItem` exposes `RiskRef, Title, Category, Likelihood, Impact, Rpn, ResidualLikelihood, ResidualImpact, ResidualRpn, Status`, plus tenancy and allocation fields. There is **no** `OwnerId`, no `NextReviewDate`, no `LastReviewedOn` and no review operation. Ownership exists only one level down, on individual `MitigationAction` rows. `Category` is a free-text string defaulting to `"Operational"` (`:73`) with no LOV backing, unlike `FeedbackEntry.Source`/`Channel` and `InterestedParty.Category`, which are documented as LOV-managed. |
| **Impact** | No accountable person for a risk, and no mechanism for the periodic re-assessment that Annex 11 §1 expects. A risk assessed once in 2024 looks identical to one assessed last week. Free-text categories prevent meaningful register analytics. |
| **Testing limitation** | No ownership, review-due or category-consistency case can be written. Risk-register reporting cases are limited to RPN and status. |
| **Recommended clarification** | Confirm whether a risk owner is required, whether a review cadence should be per-risk or per-category, and whether `Category` should move to the LOV catalogue (`DefaultLovCatalog.cs`) as the sibling registers' fields have. |
| **Suggested acceptance criteria** | (a) `RiskItem` gains a required `OwnerId` and an optional `NextReviewDate`, added by a migration that backfills existing rows and keeps the tenant-first composite PK and RLS intact; (b) a `RecordReview` operation updates `LastReviewedOn` and re-arms `NextReviewDate`; (c) risks past their review date are surfaced (dashboard count at minimum); (d) `Category` is validated against the LOV; (e) existing risk cases still pass unchanged. |
| **Severity** | **Medium** |
| **Responsible role** | Quality Manager + Development Lead |

---

**GAP-RISK-020 — No Part-11 reason-for-change is captured on any governance mutation**

| Field | Content |
|---|---|
| **Source reference** | URS-015; 21 CFR 11 §11.10(e); EU Annex 11 §9. Code: `ChangeReasonMiddleware` guards DELETE only (ground truth `00-GROUND-TRUTH-AND-CONVENTIONS.md:82`); the approved surface contains **no** DELETE route under the nine controllers in this module |
| **Description** | The `X-Change-Reason` header is demanded only on DELETE, and this module has zero DELETE endpoints. Every governance mutation — approving a change, superseding a quality policy, revising an interested party in place, closing a risk, cancelling an objective — is captured by `FieldChangeInterceptor` as an automatic before/after diff with `Reason` left null. The only free-text justifications that exist are the domain's own required fields (`RejectionReason`, `ClosureNote`, `ValidationVerdict`, `Minutes`, `PostImplementationReviewNotes`), which are business content, not change reasons, and which do not exist for `RiskItem.Close`, `InterestedParty.Revise` or `ContextIssue.Revise`. |
| **Impact** | `InterestedParty.Revise` and `ContextIssue.Revise` overwrite regulated content in place with no reason recorded — the aggregate's own XML doc points at "field-level audit captures every change" as the compensating control, but that captures *what* changed, not *why*. |
| **Testing limitation** | No `CHANGE-REASON-REQUIRED` case exists for this module, and the "Expected Audit" field on every case must state that `field_change.reason` is null. |
| **Recommended clarification** | Confirm which governance mutations require an explicit reason under the site's Part-11 interpretation. The in-place revisions (`PUT /api/org-context/interested-parties/{id}`, `PUT /api/org-context/issues/{id}`, `PUT /api/quality-policy/{id}`) are the strongest candidates because they destroy prior content. |
| **Suggested acceptance criteria** | (a) Nominated endpoints require `X-Change-Reason` (or a body field, following the place-legal-hold precedent where the reason travels in the POST body); (b) a missing reason returns **400** `CHANGE-REASON-REQUIRED`; (c) the reason is stamped onto every `audit.field_change` row written in the same transaction by `FieldChangeInterceptor`; (d) the SPA collects it through the existing accessible `change-reason-dialog` component, so no `window.prompt` is introduced; (e) a functional test asserts both the refusal and the stamped reason. |
| **Severity** | **Medium** |
| **Responsible role** | Validation Lead + Development Lead |

---

**GAP-RISK-021 — Validator/domain layering makes 29 domain codes unreachable, and 7 enum-parse sites return 500**

| Field | Content |
|---|---|
| **Source reference** | URS-078; API error-contract consistency (Phase 4, problem+json everywhere). Code: validator table §1.6; `DomainExceptionHandler.cs:34-44` (validation → 400) vs `:75-80` (domain → 422); enum parses at `ComplaintsController.cs:33`, `ConflictSlice.cs:75`, `:82`, `QualityObjectiveSlice.cs:44`, `FeedbackSlice.cs:44`, `OrgContextSlice.cs:132`, `:142` |
| **Description** | **(a)** For 29 of the module's codes the FluentValidation rule fires first, so the API returns 400 with an `errors` dictionary and the domain code never reaches the client (full list in §2 D-12). The rules are inconsistently applied: `AddMitigationCommand` and `RecordResidualCommand` have no validator, so `RSK-003` and `RSK-002` **do** surface as 422 on those paths while their siblings return 400. **(b)** Seven `Enum.Parse<T>(…, ignoreCase: true)` call sites throw `ArgumentException`, which `DomainExceptionHandler.TryHandleAsync` does not match, so `problem` is null, the handler returns false, and the request falls through to the generic error path — a **500**, not a 400. **(c)** Status filters are inconsistent: `GetRisksQuery`/`GetChangesQuery` compare `Status.ToString() == q.Status` **case-sensitively** (`RiskGovernanceSlice.cs:120`, `:291`), while the other six query handlers use `Enum.TryParse(…, ignoreCase: true)` and silently ignore an unparsable value. |
| **Impact** | Clients cannot rely on a domain code for a whole class of validation failures, and an obviously-invalid enum string is reported as a server fault rather than a client error — which also means it is logged as an unhandled exception and can trip error-rate alerts. Case authors must know which layer answers, per endpoint. |
| **Testing limitation** | The 29 codes must be tested at domain unit level and explicitly labelled as unreachable over HTTP, weakening API-level negative coverage. Any 500 case is a defect case, not a contract case. |
| **Recommended clarification** | Confirm the intended layering: is a missing required field a 400 (validation) or a 422 (domain rule)? Whichever is chosen must be applied uniformly. Confirm whether list `status` filters should be case-insensitive everywhere. |
| **Suggested acceptance criteria** | (a) Every enum-bearing command parses with `TryParse` and throws a domain code (**422**) on failure, or is bound as a typed enum by the model binder returning **400**; no path returns 500 for a bad enum string; (b) validator coverage is made consistent — either every required field has a validator (all 29 codes become 400-shadowed and are documented as domain-only) or none do; (c) all status filters use `TryParse` with `ignoreCase: true`; (d) a functional test sweeps every enum-bearing endpoint with a garbage value and asserts 400 or 422, never 500. |
| **Severity** | **Medium** |
| **Responsible role** | Development Lead |

---

**GAP-RISK-022 — Feedback escalation discards channel and confidentiality**

| Field | Content |
|---|---|
| **Source reference** | URS-033. Code: `FeedbackSlice.cs:101-122` |
| **Description** | `EscalateFeedbackCommand` creates the complaint with `ComplaintChannel.Portal` hard-coded and `confidential: false` hard-coded (`:109-110`), regardless of how the feedback actually arrived (`FeedbackEntry.Channel` is a free-text LOV value carrying exactly that information) and regardless of any confidentiality expectation. The subject is synthesised as `"Escalated feedback {ref}: {subject}"` (`:111`) and the description is copied verbatim. The command has no validator, so `ComplainantName` is unbounded and unchecked at the API layer — it is caught only by `CMP-001` in the aggregate (**422**). |
| **Impact** | Escalation loses provenance (a phone complaint becomes a portal complaint) and, more seriously, publishes a reporter's identity that may have been given in confidence — the escalated complaint is created non-confidential, so every user who can read complaints sees the name. |
| **Testing limitation** | No case can assert channel fidelity or confidentiality preservation across escalation. The confidentiality-masking suite cannot cover the escalation path. |
| **Recommended clarification** | Confirm how `FeedbackEntry.Channel` (free text) should map onto the fixed `ComplaintChannel` enum, and whether escalation should ask the operator for the confidentiality flag rather than defaulting it. |
| **Suggested acceptance criteria** | (a) `EscalateFeedbackCommand` accepts `Channel` and `Confidential` (defaulting conservatively to confidential = true if the process owner prefers fail-safe) and the SPA prompts for them; (b) an unmappable feedback channel returns a clear 400/422, not a silent `Portal`; (c) a validator bounds `ComplainantName` (≤300) and `ComplainantContact` (≤300), matching `LogComplaintValidator`; (d) tests cover each `ComplaintChannel` value and both confidentiality settings, asserting the created complaint's masking behaviour. |
| **Severity** | **Medium** |
| **Responsible role** | Quality Manager + Data Protection Officer + Development Lead |

---

**GAP-RISK-023 — Organizational context is implemented with no user requirement behind it**

| Field | Content |
|---|---|
| **Source reference** | ISO 9001 §4.1 / §4.2 (implemented per `OrganizationContext.cs:8-14`, `:100-106`). **No URS covers it** — `docs/validation/01-User-Requirements-Specification.md:109` (URS-051) names conflict-of-interest, objectives, equipment, standards, competency and monitoring points, not interested parties or context issues; a search of the URS for "interested part" and "context of the organi" returns nothing |
| **Description** | Two aggregates (`InterestedParty`, `ContextIssue`), three tables, nine endpoints, five permission keys (`org-context.*`), one SPA route and a documented parity concession in `SystemRoleCatalog.cs:88-92` exist with no requirement to trace to. Every test written against them is `[ID]` (implementation-derived) by definition, and they cannot appear in the Requirements Traceability Matrix. |
| **Impact** | A validated system contains regulated functionality outside the validation scope. The RTM will show these endpoints as untraceable, which an auditor will read as either unvalidated function or an incomplete URS. Acceptance criteria for the functionality's *correctness* cannot be derived from any approved document. |
| **Testing limitation** | All nine org-context endpoints, both state machines and every `IP-*` / `CTX-*` code must be labelled `[ID]`. They cannot contribute URS coverage. |
| **Recommended clarification** | Confirm whether organizational context is in scope for the QMS validation. If yes, a URS requirement must be added (post-baseline, so per `00-GROUND-TRUTH-AND-CONVENTIONS.md:169` it belongs in the delta document's Part A with the next free id after URS-107). If no, the functionality should be scoped out explicitly and the reason recorded. |
| **Suggested acceptance criteria** | (a) A URS requirement is added covering the interested-party register and the internal/external issue register, citing ISO 9001 §4.1/§4.2, with a risk classification in the FRA; (b) the RTM links it to `OrganizationContext.cs`, `OrgContextSlice.cs`, `OrgContextController.cs` and the case ids in `15-module-risk-governance-cases-B.md`; (c) the org-context cases are re-labelled from `[ID]` to `[IV]` once the requirement exists; (d) the documented `org-context.void` parity concession is reviewed against the new requirement. |
| **Severity** | **Medium** |
| **Responsible role** | Validation Lead + Quality Manager |

---

**GAP-RISK-024 — The complaint→NC saga hard-codes severity and likelihood**

| Field | Content |
|---|---|
| **Source reference** | URS-033; URS-030 (NC lifecycle). Code: `ComplaintToNcPolicy.cs:41-50` |
| **Description** | Every nonconformance raised from a justified complaint is created with `severity: 3, likelihood: 3` — RPN 9 — irrespective of the complaint's content, channel or subject. The complaint carries no severity field to propagate, and the saga offers no hook to set one. The NC is immediately `Submit()`ed (`:52`), so it enters the CAPA workflow with a fabricated risk score that the NC module's own triage and escalation logic then treats as real. |
| **Impact** | Every complaint-sourced NC has an identical, meaningless risk score, so NC prioritisation, Pareto analysis by RPN and any RPN-driven escalation are distorted. A safety-critical complaint and a packaging grumble arrive with the same score. |
| **Testing limitation** | No case can assert that a serious complaint produces a higher-priority NC. The complaint saga cases can only pin the constant, as `[ID]`. |
| **Recommended clarification** | Confirm whether the complaint should carry a severity assessment (captured at logging or at validation), and whether the saga should propagate it or leave the NC in `Draft` for human triage rather than auto-submitting. |
| **Suggested acceptance criteria** | (a) Either the complaint captures severity/likelihood (with the same 1–5 constraints and CHECK parity as `nonconformance`) and the saga propagates them, **or** the saga creates the NC in `Draft` and requires human triage before submission; (b) the idempotency guarantee is preserved — redelivery still nets exactly one NC keyed on `SourceRef = "CMP:{ref}"`; (c) the existing back-link and the `CMP-020` closure gate are unaffected; (d) an application unit test covers propagation, redelivery and the heal-the-link path. |
| **Severity** | **Medium** |
| **Responsible role** | Quality Manager + Development Lead |

---

**Gap summary by severity:** High — GAP-RISK-001, 002, 004 (3). Medium — GAP-RISK-003, 005, 006, 007, 008, 009, 010, 011, 012, 014, 015, 016, 017, 018, 019, 020, 021, 022, 023, 024 (20). Low — GAP-RISK-013 (1).

**Compliance verdicts for this module** (permitted vocabulary only, per honesty rule 4):

| Control area | Verdict | Basis |
|---|---|---|
| Risk-based change control (ISO 9001 §6.3, Annex 11 §10) | **Partially conforms** | `CHG-012` enforces a linked assessment and the PIR stage exists, but the link is not quality-checked (GAP-RISK-012), approval is unsegregated (GAP-RISK-001) and an ineffective PIR has no consequence (GAP-RISK-016) |
| Segregation of duties on approvals (Part 11 §11.10(g)) | **Partially conforms** | Present and unconditional for conflicts (`SOD-COI-001`); present but null-conditional for the quality policy (GAP-RISK-009); absent for change approval, review closure and risk closure (GAP-RISK-001) |
| Electronic signature on governance approvals (Part 11 subpart C) | **Does not conform** | No signature is required or recorded anywhere in the module (GAP-RISK-002) |
| Signed/terminal-record immutability | **Partially conforms** | Enforced in the aggregates; no database trigger on any table in this module (GAP-RISK-006) |
| Tenant isolation (URS-008/100) | **Conforms** *(subject to execution)* | All 12 tables plus 3 owned children carry `ENABLE` + `FORCE` RLS with the canonical policy; owned children carry tenant-composite FKs. To be confirmed by the RLS cases — no verdict is claimed on unexecuted evidence |
| Reason for change (Part 11 §11.10(e)) | **Partially conforms** | Field-level diffs are captured for every mutation; no explicit reason is captured on any governance write, including in-place revisions that destroy prior content (GAP-RISK-020) |
| Authorization by the permission catalogue (URS-095) | **Partially conforms** | 26 of 59 endpoints are gated; 33 are not, and confidentiality release still reads the legacy tier claim (GAP-RISK-003, 004, 005) |
| Complaints and feedback handling (ISO 17025 §7.9, §8.6.2) | **Partially conforms** | Full 8-state complaint machine, transactional NC linkage and closure gate; no acknowledgement timeliness (GAP-RISK-018), escalation loses provenance and confidentiality (GAP-RISK-022) |
| Quality policy control (ISO 9001 §5.2) | **Partially conforms** | Versioned, SoD-guarded, one-in-force at the handler; no version ordering (GAP-RISK-010), no DB constraint (GAP-RISK-011), no communication notification (GAP-RISK-008) |
| Quality objectives (ISO 9001 §6.2) | **Partially conforms** | Closure honesty (`OBJ-011`) is a genuine strength; the series has no uniqueness or correction path (GAP-RISK-015) and Missed/Cancelled are unchecked (DT-4 R8/R10) |
| Management review (ISO 9001 §9.3) | **Partially conforms** | Scheduling, decisions, minutes, closure and a PDF review pack exist; decisions are never chased (GAP-RISK-014) and closure is unsegregated and unsigned |
| Organizational context (ISO 9001 §4.1/§4.2) | **Cannot be assessed** | Implemented but outside the validated requirement set — no URS to assess against (GAP-RISK-023) |

*End of front matter. Detailed cases: `15-module-risk-governance-cases-A.md` … `-D.md`.*
