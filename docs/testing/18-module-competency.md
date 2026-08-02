# 18 — Module Test Package: Personnel Training, Competency Assessment, Test Authorizations, Authorization Matrix

**Module code:** `COMP`
**System under test:** NT.QMS v1.51.2, repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master`. Inspection date 2026-08-01.
**Binding conventions:** `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md` (read in full before this file). Where this file and the conventions disagree, the conventions win unless §0 below records a proven correction.

**This file is FRONT MATTER ONLY.** It contains no `## 5. Detailed test cases` section by design (conventions §7, *Split convention*). The detailed cases are authored separately into `18-module-competency-cases-<letter>.md`. The table below is a **reservation**, not a delivery: a reserved range with no matching case file is a coverage hole.

---

## ID reservation table

| Kind | Reserved range | Count | Intended batch | Slice of scope |
|---|---|---|---|---|
| `TC-COMP-UNIT-` | 001–030 | 30 | A | `CompetencyRecord` + `TrainingAssignment` + `AssessmentResult` pure-domain guards |
| `TC-COMP-STATE-` | 001–024 | 24 | A | Both state machines, every legal and illegal edge (§3) |
| `TC-COMP-BVA-` | 001–020 | 20 | A | PassMark 79/80/81, score 0/−1/100/101, validity 0/1/60/61, expiry day-before/day-of/day-after |
| `TC-COMP-EP-` | 001–010 | 10 | A | Equivalence partitions on score, validity months, scope string, status filter |
| `TC-COMP-DT-` | 001–012 | 12 | B | Decision tables of §4 (competency authorization; grant evidence gate; reinstate gate) |
| `TC-COMP-MCDC-` | 001–004 | 4 | B | `ExpireIfDue` compound guards (both aggregates), `Revoke` terminal-status guard |
| `TC-COMP-PATH-` | 001–004 | 4 | B | Assign→score→authorize→expire→requalify; grant→suspend→reinstate→revoke |
| `TC-COMP-DF-` | 001–004 | 4 | B | `ValidityMonths` → `ExpiresAt` → `TestAuthorization.ExpiresOn` inheritance chain |
| `TC-COMP-LOOP-` | 001–002 | 2 | B | Repeated failed assessments (append-only attempts, status stays `PendingTraining`) |
| `TC-COMP-API-` | 001–030 | 30 | C | All 11 logical endpoints × happy/negative, exact status + `code` extension |
| `TC-COMP-SEC-` | 001–014 | 14 | C | `[RequirePermission]` matrix, ungated GETs, ungated `complete`, `RequireInternalActor` tier |
| `TC-COMP-RLS-` | 001–008 | 8 | C | Cross-tenant isolation on the four tables + the elevated sweep path |
| `TC-COMP-INT-` | 001–016 | 16 | D | Sweep → outbox → `CompetencyLapseAuthorizationPolicy` saga; notification dispatch; audit ledger |
| `TC-COMP-ESC-` | 001–004 | 4 | D | `COMP_EXPIRED` notification rule dispatch and its absent siblings |
| `TC-COMP-OBS-` | 001–003 | 3 | D | Sweep log line, `compliance-sweep` job gauge, trace span |
| `TC-COMP-DR-` | 001–002 | 2 | D | Migration round-trip on `PersonnelAuthorizationMatrix` + hardening successors |
| `TC-COMP-E2E-` | 001–004 | 4 | D | Playwright: assign→score→authorize→grant→matrix chip |
| `TC-COMP-A11Y-` | 001–002 | 2 | D | Matrix table semantics, score/reason form labelling |
| `TC-COMP-PERF-` | 001–002 | 2 | D | Unpaginated `GET /api/test-authorizations` under a wide matrix |
| `TC-COMP-RTL-` | 001–002 | 2 | D | `ar` locale rendering of the matrix (`inset-inline-start` sticky column) |
| **Detailed-case reservation total** | | **195** | | |
| `TC-COMP-UAT-` | 001–010 | 10 | — | Delivered in **§6 of this file** |
| `TC-COMP-EXPL-` | 001–006 | 6 | — | Delivered in **§7 of this file** |
| `GAP-COMP-` | 001–026 | 26 | — | Delivered in **§8 of this file** |

**Completeness statement.**
**Complete in this file:** every public method and every guard clause of `CompetencyRecord`, `AssessmentResult`, `TrainingAssignment` and `TestAuthorization`; the full `Competency` application slice (2 command/query files + 1 saga); both controllers plus `TrainingAssignmentsController` (which lives in the same file as `CompetenciesController`); the `PersonnelAuthorizationMatrix` migration and the five later migrations that touch these four tables; the competency and test-authorization passes of `ScheduledSweepService`; the EF configurations; the permission-catalogue and seeded-role entries; the notification policy; and all seven Angular files under `frontend/src/app/features/competency/`. Live-database facts (RLS flags, columns, constraints, indexes) were **measured** with read-only `psql` against dev DB `ntqams` on 2026-08-01 and are cited as such.

**Not covered / deferred:** electronic signature on competency authorization (no `Sign` code path exists — GAP-COMP-007); training-content, curricula, or e-learning delivery (no such entity); competency *matrices per method/analyte* beyond the free-text `Subject` string; effectiveness-of-training evaluation; authorization *export* (no export endpoint for this module); and any pass-mark configurability (compile-time constant). Each is a Gap in §8.

**Result discipline.** No case in, or reserved by, this file was executed. Every `Result` in every case file consuming these ids must read `Not Run · —`.

**Risk IDs.** `docs/validation/02-Functional-Risk-Assessment.md` carries **no** competency-specific risk identifiers; competency sits inside the area row *"Governance (change/risk/policy/supplier/reviews) — URS-047,048,049,050,051,052 — Low–Medium"* (`docs/validation/02-Functional-Risk-Assessment.md:66`). Per conventions §5 I therefore **mint** the following and say so:

| Risk ID | Statement |
|---|---|
| RSK-COMP-001 | A person performs, reviews or releases a test they are not demonstrably competent to perform (ISO 17025 §6.2.6). |
| RSK-COMP-002 | A competency is authorized on a failing or absent assessment. |
| RSK-COMP-003 | A person assesses or authorizes their own competency (segregation of duties, Part 11 §11.10(g)). |
| RSK-COMP-004 | The requalification (expiry) date is computed wrongly, so an authorization outlives the competence it rests on. |
| RSK-COMP-005 | A competency lapses or is revoked but the dependent test authorizations remain in force. |
| RSK-COMP-006 | The authorization record is not attributable to a responsible signatory (Part 11 §11.50/§11.70/§11.200). |
| RSK-COMP-007 | Competency or authorization data leaks across tenants. |
| RSK-COMP-008 | An actor without the granted capability creates, edits, approves or voids a competency or authorization. |
| RSK-COMP-009 | A change to a competency or authorization is absent from, or unattributable in, the tamper-evident audit trail. |
| RSK-COMP-010 | The compliance sweep does not run, runs twice, or is non-idempotent, so expiries are missed or duplicated. |
| RSK-COMP-011 | A duplicate or contradictory authorization exists for the same person, test and scope. |
| RSK-COMP-012 | The authorization matrix displayed to a reviewer does not match the enforced state. |

---

## 0. Correction to ground truth

*Omitted — no factual error was found in `00-GROUND-TRUTH-AND-CONVENTIONS.md` within this module's scope.*

Two entries were specifically re-checked because they touch this module and both were **confirmed**, not corrected:

- §2 *Segregation of duties*: "`CompetencyRecord`: `PassMark = 80` (`src/NT.QAMS.Domain/Competency/CompetencyRecord.cs:33`); requires an assessor who is not the trainee" — verified verbatim at `src/NT.QAMS.Domain/Competency/CompetencyRecord.cs:33` and `:89-92`.
- §2 *Segregation of duties*: "SoD violations surface as **`AUTHZ-*` → HTTP 403** … Domain rule breaches surface as **HTTP 422**". Verified at `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:63-68` / `:75-80`. This is an accurate statement of the *handler*; that the `TestAuthorization` aggregate also uses `AUTHZ-` as its ordinary business-error prefix is a **code-side** problem, recorded as GAP-COMP-001/002/003, not a conventions error.

---

## 1. Implementation inventory

### 1.1 Aggregates, entities and enums

| Element | Kind | Interfaces / base | Location |
|---|---|---|---|
| `CompetencyRecord` | Aggregate root | `AggregateRoot, ITenantScoped` | `src/NT.QAMS.Domain/Competency/CompetencyRecord.cs:31` |
| `AssessmentResult` | Owned child entity (append-only) | `Entity` | `src/NT.QAMS.Domain/Competency/CompetencyRecord.cs:8` |
| `TrainingAssignment` | Aggregate root | `AggregateRoot, ITenantScoped` | `src/NT.QAMS.Domain/Competency/CompetencyRecord.cs:149` |
| `TestAuthorization` | Aggregate root | `AggregateRoot, ITenantScoped` | `src/NT.QAMS.Domain/Competency/TestAuthorization.cs:19` |
| `CompetencyStatus` | Enum — `PendingTraining, Evaluated, Authorized, Revoked` | — | `src/NT.QAMS.Domain/Competency/CompetencyRecord.cs:6` |
| `AuthorizationScope` | Enum — `Perform, ReviewAndRelease, Train` | — | `src/NT.QAMS.Domain/Competency/TestAuthorization.cs:7` |
| `TestAuthorizationStatus` | Enum — `Active, Suspended, Revoked, Expired` | — | `src/NT.QAMS.Domain/Competency/TestAuthorization.cs:9` |

**There is no `Expired` member on `CompetencyStatus`.** Expiry of a competency is modelled as a *return to `PendingTraining`* (`CompetencyRecord.cs:125`), not as a distinct terminal state. Any test or UI that expects `CompetencyStatus.Expired` is testing something that does not exist (see GAP-COMP-015).

### 1.2 `CompetencyRecord` — properties and the score field type

| Property | CLR type | Setter | Column (measured) |
|---|---|---|---|
| `TenantId` | `Guid` | public set | `tenant_id uuid NOT NULL` |
| `TraineeId` | `Guid` | private set | `trainee_id uuid NOT NULL` |
| `Subject` | `string` | private set | `subject character varying(300) NOT NULL` |
| `DocumentId` | `Guid?` | private set | `document_id uuid NULL` |
| `Status` | `CompetencyStatus` | private set | `status character varying(20) NOT NULL` (string conversion, `ResourceConfigurations.cs:119`) |
| `ValidityMonths` | `int` | private set | `validity_months integer NOT NULL` |
| `ExpiresAt` | `DateOnly?` | private set | `expires_at date NULL` |
| `AuthorizedBy` | `Guid?` | private set | `authorized_by uuid NULL` |
| `RevocationReason` | `string?` | private set | `revocation_reason text NULL` (widened from `varchar(1000)` by `Hardening1_TypesAndNames`) |
| `Assessments` | `IReadOnlyList<AssessmentResult>` | read-only projection | child table `qams.assessment_result` |

`AssessmentResult` (`src/NT.QAMS.Domain/Competency/CompetencyRecord.cs:8-22`): `Score` **`int`** (`:19`), `AssessorId` `Guid` (`:20`), `AssessedAtUtc` `DateTimeOffset` (`:21`). Measured column: `qams.assessment_result.score` is **`integer`** (read-only `psql` against `ntqams`, 2026-08-01).

> **SCORE FIELD TYPE — BINDING FOR THE CASE AUTHORS.**
> The score is an **`int`** in the domain (`CompetencyRecord.cs:19`), an **`int`** on the request DTO (`ScoreAssessmentRequest(int Score)`, `src/NT.QAMS.Contracts/Resources/ResourceContracts.cs:64`) and an **`integer`** column in PostgreSQL. **`79.99` is not representable anywhere in this path.** A fractional score never reaches `CompetencyRecord.ScoreAssessment`, so it can never be compared against `PassMark`. Do **not** author a BVA case asserting "79.99 is rejected by the pass gate" — the pass gate never sees it. The correct boundary set is the integer triple **79 / 80 / 81**. A separate *input-binding* case may send `{"score": 79.99}` to `POST /api/competencies/{id}/assessments`, but its expected result is a **JSON model-binding failure at the ASP.NET layer, before MediatR** — the exact status code and body shape were **not** read in this pass and must be labelled `[RNV]` until measured against the running API.

### 1.3 Invariants and guard clauses — `CompetencyRecord`

| # | Rule | Enforced in | Code | Exception type → HTTP |
|---|---|---|---|---|
| C1 | Subject required, non-blank; stored trimmed | `Assign` `CompetencyRecord.cs:57-60`, trim `:71` | `COMP-001` | `DomainException` → **422** |
| C2 | `ValidityMonths >= 1` | `Assign` `:62-65` | `COMP-002` | `DomainException` → **422** |
| C3 | Initial status is `PendingTraining` | `Assign` `:72` | — | — |
| C4 | Scoring is only legal in `PendingTraining` or `Evaluated` | `ScoreAssessment` `:79-82` | `COMP-010` | `InvalidStateTransitionException` → **409** |
| C5 | `0 <= score <= 100` | `ScoreAssessment` `:84-87` | `COMP-011` | `DomainException` → **422** |
| C6 | Assessor ≠ trainee (SoD rule 4) | `ScoreAssessment` `:89-92` | `SOD-COMP-001` | `DomainException` → **422** |
| C7 | Attempts are append-only — every attempt is retained, pass or fail | `ScoreAssessment` `:94` | — | — |
| C8 | Status after scoring = `Evaluated` iff `score >= 80`, else `PendingTraining` | `ScoreAssessment` `:95` | — | — |
| C9 | Only an `Evaluated` competency may be authorized | `Authorize` `:100-104` | `COMP-012` | `InvalidStateTransitionException` → **409** |
| C10 | Authorizer ≠ trainee | `Authorize` `:106-109` | `SOD-COMP-001` | `DomainException` → **422** |
| C11 | `ExpiresAt = asOf.AddMonths(ValidityMonths)` | `Authorize` `:113` | — | — |
| C12 | Authorization raises `CompetencyAuthorized` | `Authorize` `:114` | — | — |
| C13 | Expiry fires only when `Status == Authorized && ExpiresAt != null && ExpiresAt <= asOf` | `ExpireIfDue` `:120-123` | — (silent no-op) | — |
| C14 | Expiry returns to `PendingTraining` and **clears `AuthorizedBy`**; `ExpiresAt` is left as-is | `ExpireIfDue` `:125-126` | — | — |
| C15 | Only an `Authorized` competency may be revoked | `Revoke` `:132-135` | `COMP-013` | `InvalidStateTransitionException` → **409** |
| C16 | Revocation reason required, non-blank; stored trimmed | `Revoke` `:137-140`, `:143` | `COMP-014` | `DomainException` → **422** |
| C17 | Revocation is terminal — no method returns a `Revoked` record to any other state | whole aggregate | — | — |

**Not enforced (verified absent, read the whole file):** no electronic signature; no check that the assessor is themselves competent/authorized; no check that the authorizer differs from the *creator* of the record (`AggregateRoot.EnsureSignerIsNotPreparer` exists at `src/NT.QAMS.SharedKernel/Primitives/AggregateRoot.cs:35-42` and is **not called** from `CompetencyRecord`); no maximum on `ValidityMonths` in the domain; no minimum number of assessments; no re-assessment cool-off.

### 1.4 Invariants and guard clauses — `TrainingAssignment`

| # | Rule | Enforced in | Code | → HTTP |
|---|---|---|---|---|
| T1 | Subject required, non-blank; stored trimmed | `Create` `CompetencyRecord.cs:166-169`, `:175` | `TRN-001` | **422** |
| T2 | Completion is once-only | `Complete` `:182-185` | `TRN-002` | **409** (`InvalidStateTransitionException`) |
| T3 | `CompletedAtUtc` stamped from the injected clock | `Complete` `:188`, handler `CompetencySlice.cs:128` | — | — |

**Not enforced:** no due-date validation (a past `DueDate` is accepted); no subject length rule in the domain **and no FluentValidation validator for `AssignTrainingCommand`** (the only bound is the `varchar(300)` column) — GAP-COMP-016; no link from a completed training to any `CompetencyRecord` — GAP-COMP-021.

### 1.5 Invariants and guard clauses — `TestAuthorization`

| # | Rule | Enforced in | Code | → HTTP |
|---|---|---|---|---|
| A1 | Grantor ≠ grantee | `Grant` `TestAuthorization.cs:41-44` | `SOD-AUTHZ-001` | **422** (prefix is `SOD-`, not `AUTHZ-`) |
| A2 | `expiresOn > grantedOn` (strict) | `Grant` `:46-49` | `AUTHZ-001` | **403** — see GAP-COMP-001/003 |
| A3 | Initial status `Active` | `Grant` `:60` | — | — |
| A4 | Only `Active` may be suspended | `Suspend` `:66-69` | `AUTHZ-010` | **409** |
| A5 | Suspension reason required, trimmed | `Suspend` `:71-74`, `:77` | `AUTHZ-011` | **403** — GAP-COMP-001 |
| A6 | Saga-shaped suspend never throws off the Active path | `SuspendIfActive` `:81-87` | — | — |
| A7 | Only `Suspended` may be reinstated | `Reinstate` `:91-94` | `AUTHZ-012` | **409** |
| A8 | Reinstatement refused once `ExpiresOn <= asOf` | `Reinstate` `:96-99` | `AUTHZ-013` | **403** — GAP-COMP-001 |
| A9 | Reinstatement clears `SuspensionReason` | `Reinstate` `:102` | — | — |
| A10 | `Revoked` / `Expired` cannot be revoked | `Revoke` `:107-110` | `AUTHZ-014` | **409** |
| A11 | Revocation reason required, trimmed | `Revoke` `:112-115`, `:118` | `AUTHZ-015` | **403** — GAP-COMP-001 |
| A12 | Revocation raises `TestAuthorizationRevoked` | `Revoke` `:119` | — | — |
| A13 | Expiry latches from `Active` **or** `Suspended` only, and only when `ExpiresOn <= asOf` | `ExpireIfDue` `:125-128` | — (silent no-op) | — |
| A14 | Expiry raises `TestAuthorizationExpired` | `ExpireIfDue` `:131` | — | — |

### 1.6 Application-layer rules (`GrantTestAuthorizationHandler`) — the evidence gate

Read in full at `src/NT.QAMS.Application/Competency/AuthorizationSlice.cs:28-73`. Executed **in this order**:

| Step | Rule | Line | Code | → HTTP |
|---|---|---|---|---|
| 1 | An authenticated actor is required | `:33-34` | `AUTH-003` | **401** (`DomainExceptionHandler.cs:54-59`) |
| 2 | `Enum.Parse<AuthorizationScope>(c.Scope, ignoreCase: true)` | `:35` | **none** — throws `ArgumentException` | **500** (unhandled; `DomainExceptionHandler.cs:81` returns `null` → `false`) — GAP-COMP-004 |
| 3 | Catalog test must exist | `:37-38` | `ORG-404` | **404** (suffix rule, `DomainExceptionHandler.cs:69-74`) |
| 4 | Catalog test must be active | `:39-42` | `AUTHZ-002` | **403** — GAP-COMP-001, and **collides** with `AuthorizationBehavior`'s own `AUTHZ-002` (GAP-COMP-003) |
| 5 | Evidencing competency must exist | `:46-47` | `COMP-404` | **404** |
| 6 | Competency must belong to the same person | `:48-51` | `AUTHZ-003` | **403** — GAP-COMP-001 |
| 7 | Competency must be `Authorized` **and** carry a non-null `ExpiresAt` | `:53-56` | `AUTHZ-004` | **403** — GAP-COMP-001 |
| 8 | No equivalent `Active` **or** `Suspended` authorization for the same (user, test, scope) | `:58-64` | `AUTHZ-005` | **403** — GAP-COMP-001; read-then-write with no unique index (GAP-COMP-011) |
| 9 | `GrantedOn = today (UTC)`, `ExpiresOn = competency.ExpiresAt.Value` — **expiry is inherited, never client-supplied** | `:66-68` | — | — |

Note step 7: the competency's *currency* is **not** re-evaluated against today's date at grant time — an `Authorized` competency whose `ExpiresAt` is already in the past would still satisfy the gate. In practice the sweep expires it first, but the guard itself does not check `ExpiresAt >= today`. This is why `TestAuthorization.Grant`'s own `AUTHZ-001` (`expiresOn > grantedOn`) is the only date defence, and it produces a **403**, which reads as "you are not allowed" rather than "the evidence has lapsed".

### 1.7 Complete domain error-code inventory for module COMP (exhaustive)

Every code that can originate from this module's domain, slice or loader. Codes are grouped by originating file; the HTTP mapping is derived from `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:26-82` (arm order matters and is reproduced faithfully).

| Code | Meaning | Raised at | Exception type | HTTP |
|---|---|---|---|---|
| `COMP-001` | Competency subject required | `CompetencyRecord.cs:59` | `DomainException` | 422 |
| `COMP-002` | Validity must be ≥ 1 month | `CompetencyRecord.cs:64` | `DomainException` | 422 |
| `COMP-010` | Cannot score in the current state | `CompetencyRecord.cs:81` | `InvalidStateTransitionException` | 409 |
| `COMP-011` | Score must be 0–100 | `CompetencyRecord.cs:86` | `DomainException` | 422 |
| `COMP-012` | Only an `Evaluated` competency can be authorized | `CompetencyRecord.cs:103` | `InvalidStateTransitionException` | 409 |
| `COMP-013` | Only an `Authorized` competency can be revoked | `CompetencyRecord.cs:134` | `InvalidStateTransitionException` | 409 |
| `COMP-014` | Revocation reason required | `CompetencyRecord.cs:139` | `DomainException` | 422 |
| `COMP-404` | Competency record not found | `CompetencySlice.cs:61`, `CompetencySlice.cs:177`, `AuthorizationSlice.cs:47` | `DomainException` | 404 |
| `SOD-COMP-001` | Trainee cannot assess **or** authorize their own competency | `CompetencyRecord.cs:91` and `:108` (same code, two guards) | `DomainException` | 422 |
| `TRN-001` | Training subject required | `CompetencyRecord.cs:168` | `DomainException` | 422 |
| `TRN-002` | Training already completed | `CompetencyRecord.cs:184` | `InvalidStateTransitionException` | 409 |
| `TRN-404` | Training assignment not found | `CompetencySlice.cs:127` | `DomainException` | 404 |
| `SOD-AUTHZ-001` | Users cannot grant their own test authorizations | `TestAuthorization.cs:43` | `DomainException` | 422 |
| `AUTHZ-001` | Authorization expiry must fall after the grant date | `TestAuthorization.cs:48` | `DomainException` | **403** |
| `AUTHZ-002` | Target catalog test is inactive | `AuthorizationSlice.cs:41` | `DomainException` | **403** |
| `AUTHZ-003` | Evidencing competency belongs to a different person | `AuthorizationSlice.cs:50` | `DomainException` | **403** |
| `AUTHZ-004` | Only a current, `Authorized` competency can evidence a grant | `AuthorizationSlice.cs:55` | `DomainException` | **403** |
| `AUTHZ-005` | Equivalent authorization already in force | `AuthorizationSlice.cs:63` | `DomainException` | **403** |
| `AUTHZ-010` | Only an active authorization can be suspended | `TestAuthorization.cs:68` | `InvalidStateTransitionException` | 409 |
| `AUTHZ-011` | Suspension reason required | `TestAuthorization.cs:73` | `DomainException` | **403** |
| `AUTHZ-012` | Only a suspended authorization can be reinstated | `TestAuthorization.cs:93` | `InvalidStateTransitionException` | 409 |
| `AUTHZ-013` | Authorization has lapsed — grant a new one | `TestAuthorization.cs:98` | `DomainException` | **403** |
| `AUTHZ-014` | A `Revoked`/`Expired` authorization cannot be revoked | `TestAuthorization.cs:109` | `InvalidStateTransitionException` | 409 |
| `AUTHZ-015` | Revocation reason required | `TestAuthorization.cs:114` | `DomainException` | **403** |
| `AUTHZ-404` | Test authorization not found | `AuthorizationSlice.cs:128`, `AuthorizationSlice.cs:175` | `DomainException` | **403 — NOT 404** (the `AUTHZ-` arm at `DomainExceptionHandler.cs:63` precedes the `-404` arm at `:69`) — GAP-COMP-002 |
| `AUTH-003` | An authenticated user is required | `CompetencySlice.cs:64`, `AuthorizationSlice.cs:34`, `AuthorizationSlice.cs:120` | `DomainException` | 401 |
| `ORG-404` | Catalog test not found | `AuthorizationSlice.cs:38` | `DomainException` | 404 |

**Codes reachable on this module's endpoints but owned elsewhere** (cite the owning file when a case asserts them):

| Code | Origin | HTTP | When it fires on a COMP endpoint |
|---|---|---|---|
| `AUTHZ-000` | `src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs:52` | 403 | A command with no `CommandPolicyAttribute` — cannot occur here; all 8 COMP commands are annotated, and `CommandPolicyTests` is a CI gate |
| `AUTHZ-001` | `AuthorizationBehavior.cs:60` | 403 | Unauthenticated actor at the command tier — **same string as the domain's `AUTHZ-001`** (GAP-COMP-003) |
| `AUTHZ-002` | `AuthorizationBehavior.cs:83` | 403 | Role not permitted (i.e. `ExternalAuditor` on any COMP write) — **same string as the domain's `AUTHZ-002`** (GAP-COMP-003) |
| `AUTHZ-008` | `AuthorizationBehavior.cs:68` | 403 | Declared permission key unknown to the catalogue — not reachable here (no COMP command uses `RequirePermissionPolicy`) |
| `AUTHZ-403` | `src/NT.QAMS.WebApi/Middleware/ProblemAuthorizationResultHandler.cs:16`, emitted by `RequirePermissionAttribute.cs:55-59` | 403 | The endpoint-level permission gate denies |
| `CONCURRENCY-409` | `DomainExceptionHandler.cs:21`, `:28-33` | 409 | `xmin` conflict on any COMP write |
| *(validation)* | `DomainExceptionHandler.cs:34-44` | 400 | FluentValidation failure — body has `errors`, **no `code` extension** |

### 1.8 Domain events and their consumers

| Event | Raised at | Payload | Consumers |
|---|---|---|---|
| `CompetencyAuthorized(CompetencyId, TraineeId, Subject, ExpiresAt, TenantId)` | `CompetencyRecord.cs:114` (declared `:192`) | — | **None.** No `INotificationHandler` subscribes (verified by repo-wide grep). It reaches the outbox and the audit ledger only. |
| `CompetencyExpired(CompetencyId, TraineeId, Subject, TenantId)` | `CompetencyRecord.cs:127` (declared `:195`) | — | `CompetencyLapseAuthorizationPolicy` (`CompetencyLapseAuthorizationPolicy.cs:24-41`) **and** `NotificationEventPolicies` (`src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:97-99`) |
| `CompetencyRevoked(CompetencyId, TraineeId, Subject, RevokedBy, Reason, TenantId)` | `CompetencyRecord.cs:144` (declared `:198`) | — | `CompetencyLapseAuthorizationPolicy` (`:43-64`). **No notification handler.** |
| `TestAuthorizationRevoked(AuthorizationId, UserId, TestCatalogItemId, ActorId, Reason, TenantId)` | `TestAuthorization.cs:119` (declared `:138`) | — | **None.** |
| `TestAuthorizationExpired(AuthorizationId, UserId, TestCatalogItemId, TenantId)` | `TestAuthorization.cs:131` (declared `:135`) | — | **None.** |

**`ScoreAssessment` raises no event at all** (`CompetencyRecord.cs:77-96`). An assessment therefore never appears in the tamper-evident hash chain `audit.audit_trail_entry` (which is appended only from processed outbox events, `src/NT.QAMS.Infrastructure/Compliance/ComplianceLedgerServices.cs:29-66`). It appears only as `audit.field_change` rows. See GAP-COMP-019.

### 1.9 The lapse saga

`CompetencyLapseAuthorizationPolicy` (`src/NT.QAMS.Application/Competency/CompetencyLapseAuthorizationPolicy.cs:17-71`):

- On `CompetencyExpired`: sets the tenant from the event (`:27`), loads dependants where `CompetencyRecordId == e.CompetencyId && Status == Active` (`:29-32`), calls `SuspendIfActive` with the literal reason `"Competency '{Subject}' expired — requalification required."` (`:33-34`), saves (`:35`), logs a count (`:39`, message template `:66-67`).
- On `CompetencyRevoked`: loads dependants in `Active` **or** `Suspended` (`:48-52`), calls `Revoke(e.RevokedBy, $"Competency '{Subject}' revoked: {Reason}")` (`:55`) — note the **revoking actor of the competency is propagated as the actor on every dependent authorization**, saves (`:58`), logs (`:62`, template `:69-70`).
- Idempotence: expiry path uses the non-throwing `SuspendIfActive`; revocation path uses the throwing `Revoke` but pre-filters to `Active`/`Suspended`, which are exactly the states `Revoke` accepts (`TestAuthorization.cs:107-110`).

### 1.10 The scheduled sweep

`src/NT.QAMS.Infrastructure/Jobs/ScheduledSweepService.cs`:

| Fact | Line |
|---|---|
| `BackgroundService`, interval **1 hour** | `:24`, `:29` |
| 15-second startup delay | `:34` |
| Cross-tenant elevation before the first query (`ICurrentTenantSetter.Elevate()`) | `:64` |
| Leader election via `AdvisoryLock.TryRunExclusiveAsync(db, AdvisoryLockKeys.ComplianceSweep, …)` | `:70-76` |
| Job-liveness gauge `RecordJobSuccess("compliance-sweep", …)` — only when the lock was won | `:77-81` |
| `today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)` | `:88` |
| Competency pass: `IgnoreQueryFilters()`, `Status == Authorized && ExpiresAt != null && ExpiresAt <= today`, then `ExpireIfDue(today)` | `:104-109` |
| Authorization pass: `IgnoreQueryFilters()`, `(Status == Active \|\| Status == Suspended) && ExpiresOn <= today`, then `ExpireIfDue(today)` | `:111-117` |
| Single `SaveChangesAsync` for the whole sweep — all seven passes commit atomically | `:150` |
| Returned `Expired` count is `expiryCandidates.Count`, i.e. **candidates, not effected transitions** | `:151` — GAP-COMP-020 |
| Log template names only competency expiries, not authorization expiries | `:154-156` |

**Ordering consequence (important for the case authors).** Because the competency pass and the authorization pass run against the same snapshot and commit in one `SaveChanges`, a competency and the authorizations it evidences lapse **together**: the authorizations go straight to `Expired`, and the saga (which runs later, from the outbox) finds no `Active` dependants and suspends nothing. Any integration case asserting "expiry → Suspended" must first establish an authorization whose `ExpiresOn` is *later* than the competency's `ExpiresAt`, which the grant path does not produce (`AuthorizationSlice.cs:68` copies the competency's date verbatim). State this explicitly in the case's Preconditions.

### 1.11 Endpoints

**15 logical endpoints** (6 on `CompetenciesController`, 3 on `TrainingAssignmentsController`, 6 on `TestAuthorizationsController`), each dual-exposed as `/api/…` and `/api/v{version}/…` per `Asp.Versioning` → **30 routes**, all present and counted in `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` (lines 38-39, 125-128, 146-147, 233-236, 274-277, 416-422, 467-470, 609-615). Any change to this surface must update that snapshot in the same commit or the merge gate fails.

| # | Method + route | Controller line | Endpoint permission | Command policy | Success |
|---|---|---|---|---|---|
| 1 | `GET /api/competencies?traineeId&status&page&pageSize` | `CompetenciesController.cs:16-21` | **none — `[Authorize]` only** (`:13`) | n/a — query | `200` `PagedResponse<CompetencyListItemDto>` |
| 2 | `GET /api/competencies/{id:guid}` | `:23-25` | **none** | n/a | `200` `CompetencyDetailDto` |
| 3 | `POST /api/competencies` | `:27-34` | `competencies.create` (`:28`) | `RequireInternalActor` (`CompetencySlice.cs:13`) | `201` + `Location` to #2, body `{ id }` |
| 4 | `POST /api/competencies/{id}/assessments` | `:36-42` | `competencies.edit` (`:37`) | `RequireInternalActor` (`CompetencySlice.cs:39`) | `204` |
| 5 | `POST /api/competencies/{id}/authorize` | `:44-50` | `competencies.approve` (`:45`) | `RequireInternalActor` (`CompetencySlice.cs:41`) | `204` |
| 6 | `POST /api/competencies/{id}/revoke` | `:52-58` | `competencies.void` (`:53`) | `RequireInternalActor` (`CompetencySlice.cs:43`) | `204` |
| 7 | `GET /api/training-assignments?traineeId&includeCompleted&page&pageSize` | `:66-71` | **none** | n/a | `200` `PagedResponse<TrainingAssignmentDto>` |
| 8 | `POST /api/training-assignments` | `:73-80` | `training.create` (`:74`) | `RequireInternalActor` (`CompetencySlice.cs:102`) | **`200`** `{ id }` — note: `Ok`, not `Created` (`:79`) |
| 9 | `POST /api/training-assignments/{id}/complete` | `:82-87` | **NONE — no `[RequirePermission]`** | `RequireInternalActor` (`CompetencySlice.cs:118`) | `204` — GAP-COMP-006 |
| 10 | `GET /api/test-authorizations?userId&status` | `TestAuthorizationsController.cs:17-20` | **none** | n/a | `200` **bare `IReadOnlyList`, unpaginated** — GAP-COMP-013 |
| 11 | `GET /api/test-authorizations/{id:guid}` | `:22-24` | **none** | n/a | `200` `TestAuthorizationDetailDto` |
| 12 | `POST /api/test-authorizations` | `:26-33` | `test-authorizations.create` (`:27`) | `RequireInternalActor` (`AuthorizationSlice.cs:13`) | `201` + `Location` to #11 |
| 13 | `POST /api/test-authorizations/{id}/suspend` | `:35-41` | `test-authorizations.edit` (`:36`) | `RequireInternalActor` (`AuthorizationSlice.cs:75`) | `204` |
| 14 | `POST /api/test-authorizations/{id}/reinstate` | `:43-49` | `test-authorizations.approve` (`:44`) | `RequireInternalActor` (`AuthorizationSlice.cs:77`) | `204` |
| 15 | `POST /api/test-authorizations/{id}/revoke` | `:51-57` | `test-authorizations.void` (`:52`) | `RequireInternalActor` (`AuthorizationSlice.cs:79`) | `204` |

**No DELETE exists anywhere in this module.** Consequently `ChangeReasonMiddleware` — which refuses only `DELETE` without `X-Change-Reason` (conventions §2) — **never applies to a competency or authorization mutation**. The Part-11 reason for change is carried instead inside the command bodies (`RevokeCompetencyRequest.Reason`, `SuspendTestAuthorizationRequest.Reason`, `RevokeTestAuthorizationRequest.Reason`); scoring and authorizing carry **no reason at all**. See GAP-COMP-018.

### 1.12 Validators (FluentValidation)

| Command | Validator | Rules | File:line |
|---|---|---|---|
| `AssignCompetencyCommand` | `AssignCompetencyValidator` | `TraineeId` NotEmpty; `Subject` NotEmpty + MaxLength **300**; `ValidityMonths` InclusiveBetween **1..60** | `CompetencySlice.cs:17-25` |
| `ScoreAssessmentCommand` | **none** | — bounds are domain-only (`COMP-011`) | `CompetencySlice.cs:40` |
| `AuthorizeCompetencyCommand` | **none** | — | `CompetencySlice.cs:42` |
| `RevokeCompetencyCommand` | `RevokeCompetencyValidator` | `Reason` NotEmpty + MaxLength **1000** | `CompetencySlice.cs:47-53` |
| `AssignTrainingCommand` | **none** | — no subject-length bound at any layer above `varchar(300)` | `CompetencySlice.cs:102-104` |
| `CompleteTrainingCommand` | **none** | — | `CompetencySlice.cs:119` |
| `GrantTestAuthorizationCommand` | `GrantTestAuthorizationValidator` | `UserId`/`TestCatalogItemId`/`CompetencyRecordId` NotEmpty; `Scope` **NotEmpty only — no enum-membership rule** | `AuthorizationSlice.cs:17-26` |
| `SuspendTestAuthorizationCommand` | `SuspendTestAuthorizationValidator` | `Reason` NotEmpty + MaxLength 1000 | `AuthorizationSlice.cs:82-88` |
| `ReinstateTestAuthorizationCommand` | **none** | — | `AuthorizationSlice.cs:78` |
| `RevokeTestAuthorizationCommand` | `RevokeTestAuthorizationValidator` | `Reason` NotEmpty + MaxLength 1000 | `AuthorizationSlice.cs:90-96` |

`ValidityMonths = 61` therefore fails at **400 (validation)**, never at `COMP-002` — the domain would accept it. Author the BVA pair accordingly: `0` → 400 (validator) and, if the domain is exercised directly in a unit test, `0` → `COMP-002`.

### 1.13 Queries and their read-model behaviour

| Query | Handler | Behaviour worth a case |
|---|---|---|
| `GetCompetenciesQuery(TraineeId?, Status?, Page, PageSize)` | `CompetencySlice.cs:140-164` | Status filter is a **raw string comparison** `x.Status.ToString() == q.Status` (`:154`) with **no enum validation** — an unknown value silently yields zero rows. Ordered by `Subject` (`:159`). Paged via `PageRequest.Normalized` (`:162`), clamp `1..200`, default 50 (`src/NT.QAMS.Application/Abstractions/Paging.cs:13-20`). `HasMore = Page*PageSize < Total` (`src/NT.QAMS.Contracts/Common/…:10`). The SQL-translatability of `.ToString()` over a `HasConversion<string>` enum was **not** verified in this pass — `[RNV]` on that specific claim; charter `TC-COMP-EXPL-003` targets it. |
| `GetCompetencyByIdQuery` | `CompetencySlice.cs:168-186` | `Include(Assessments)`, `COMP-404` on miss (`:177`), assessments returned **newest-first** (`:182`). |
| `GetTrainingQueueQuery(TraineeId?, IncludeCompleted, Page, PageSize)` | `CompetencySlice.cs:193-217` | Default excludes completed (`:205-208`); ordered by `DueDate` (`:212`). |
| `GetTestAuthorizationsQuery(UserId?, Status?)` | `AuthorizationSlice.cs:136-164` | **Unpaginated** `IReadOnlyList` (`:134`). Status parsed with `Enum.TryParse(ignoreCase)` and **silently ignored when unparsable** (`:148-152`). **INNER `Join`** to `TestCatalogItems` (`:155-157`) — an authorization whose catalog row is absent is silently dropped from the register. Ordered by `TestCode` (`:158`). |
| `GetTestAuthorizationByIdQuery` | `AuthorizationSlice.cs:168-187` | `AUTHZ-404` on miss (`:175`). Missing catalog test degrades to the literal `"?"` for code and name (`:183`) — **inconsistent with the list query's inner join** (GAP-COMP-014). |

### 1.14 Permission keys

Module keys (`src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:96-98`): `competencies`, `training`, `test-authorizations` — all in group `people` (`:72`, `:165-167`).

| Module | Action bundle | Keys (built by `PermissionCatalog.Key`, `:194`) |
|---|---|---|
| `competencies` | `SignedRecordLifecycle` (`:116-121`, applied `:165`) | `competencies.view`, `.create`, `.edit`, `.approve`, `.void`, **`.sign`**, `.export` |
| `training` | `FullRecordLifecycle` (`:110-114`, applied `:166`) | `training.view`, `.create`, `.edit`, `.approve`, `.void`, `.export` |
| `test-authorizations` | `SignedRecordLifecycle` (applied `:167`) | `test-authorizations.view`, `.create`, `.edit`, `.approve`, `.void`, **`.sign`**, `.export` |

**Keys defined but enforced by nothing in this build** (verified by grepping every `RequirePermission`/`RequirePermissionPolicy` call site): `competencies.view`, `competencies.sign`, `competencies.export`, `training.view`, `training.edit`, `training.approve`, `training.void`, `training.export`, `test-authorizations.view`, `test-authorizations.sign`, `test-authorizations.export`. Granting or revoking any of them changes nothing at the API. See GAP-COMP-005 and GAP-COMP-007.

**Seeded-role grants** (`src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs`):

| Seeded role | `competencies` | `training` | `test-authorizations` | Line |
|---|---|---|---|---|
| Tenant Administrator | all 7 | all 6 | all 7 | `:100` (`PermissionCatalog.AllKeys`) |
| Quality Manager | all 7 | all 6 | all 7 | `:107-117` (predicate excludes only `users`, `tenant-settings`, `roles`, `organization.manage`) |
| Department Head | view, create, edit, export | view, create, edit, export | view, create, edit, export | `:140-142` |
| Analyst | view, export | view, edit, export | view, export | `:170-172` |
| External Auditor | view, export | view, export | view, export | `:184-193` (`ReadActions` on every non-administration module) |

Consequence to test: **no seeded role except Tenant Administrator and Quality Manager holds `competencies.approve` or `competencies.void`** — a Department Head can create and score a competency but cannot authorize or revoke it, and cannot reinstate or revoke an authorization.

### 1.15 Persistence

Four tables, all in schema `qams`, all `ITenantScoped`. Values below were **measured** with read-only `psql` against dev DB `ntqams` on 2026-08-01, and cross-read against the EF configuration at `src/NT.QAMS.Infrastructure/Persistence/Configurations/ResourceConfigurations.cs:92-152`.

| Table | PK (measured) | RLS (measured `relrowsecurity`/`relforcerowsecurity`/policy) | Created by |
|---|---|---|---|
| `qams.competency_record` | `PRIMARY KEY (tenant_id, id)` (`ResourceConfigurations.cs:116`) | `t` / `t` / `tenant_isolation` | `20260721221903_ResourcesModules.cs:218-219` (policy), forced by `20260726081443_ActivateForcedTenantRls.cs:29-46` |
| `qams.assessment_result` | `PRIMARY KEY (tenant_id, id)` (`ResourceConfigurations.cs:132`) | `t` / `t` / `tenant_isolation` | RLS added retro-actively by `20260731201114_Hardening4_ChildTenancy.cs:438-446` |
| `qams.training_assignment` | `PRIMARY KEY (tenant_id, id)` (`ResourceConfigurations.cs:144`) | `t` / `t` / `tenant_isolation` | `20260721221903_ResourcesModules.cs:220-222` |
| `qams.test_authorization` | `PRIMARY KEY (tenant_id, id)` (`ResourceConfigurations.cs:97`) | `t` / `t` / `tenant_isolation` | `20260725070822_PersonnelAuthorizationMatrix.cs:14-69` |

**Migration `PersonnelAuthorizationMatrix` (`20260725070822`) as authored** — read it before writing a migration-history case, because it is *not* the shape the table has today:

- Created `qams.test_authorization` with a **single-column** `PRIMARY KEY (id)` (`:38`), `scope`/`status` as `varchar(20)`, `suspension_reason`/`revocation_reason` as `varchar(1000)` (`:24-30`), and audit columns (`:31-34`).
- Four indexes (`:41-63`): `ix_…_competency_record_id` (**not** tenant-prefixed), and three tenant-prefixed on `status`, `test_catalog_item_id`, `user_id`.
- RLS: `ENABLE ROW LEVEL SECURITY` + a `USING`-only policy **without** `FORCE` and **without** the `app.bypass_rls` clause (`:65-69`). One day later `ActivateForcedTenantRls` (`20260726081443`) rewrote it — it iterates `pg_policies WHERE policyname='tenant_isolation'`, so this table *was* picked up (unlike `audit.security_event`, conventions §2).
- `Down()` drops the table (`:75-77`) — **no RLS teardown needed** because the table goes with it.

**Later migrations touching these tables:**

| Migration | Effect |
|---|---|
| `20260726081443_ActivateForcedTenantRls` | Adds `FORCE`, rewrites the policy to the canonical `NULLIF(current_setting('app.current_tenant',true),'')::uuid OR app.bypass_rls='on'` shape with a `WITH CHECK` half (`:29-46`) |
| `20260726192118_CreatedByUserIdForSoD` | Adds `created_by_user_id uuid NULL` to these tables (measured present on all four roots) |
| `20260731180344_Hardening1_TypesAndNames` | `test_authorization.suspension_reason` and `.revocation_reason` `varchar(1000) → text` (`:55-75`); `Down()` reverses (`:677-697`) |
| `20260731191212_Hardening3_CheckDomains` | `ck_competency_record_status_domain` (`:53-54`); `ck_test_authorization_scope_domain` and `ck_test_authorization_status_domain` (`:139-142`) — all `NOT VALID` then `VALIDATE` |
| `20260731201114_Hardening4_ChildTenancy` | Shadow `tenant_id` on `assessment_result`, tenant-composite FK, and its own RLS block (`:438-446`) |
| `20260731210953_Hardening5_CompositeKeys` | Tenant-first composite PKs on all four; rebuilds the `assessment_result` FK as `(tenant_id, competency_id) → competency_record(tenant_id, id) ON DELETE CASCADE` (`:1425-1431`) |

**Measured constraints (2026-08-01):**

| Table | Constraint | Definition |
|---|---|---|
| `competency_record` | `pk_competency_record` | `PRIMARY KEY (tenant_id, id)` |
| `competency_record` | `ux_competency_record_id_tenant` | `UNIQUE (id, tenant_id)` — legal under the partition rule because it contains `tenant_id` |
| `competency_record` | `ck_competency_record_status_domain` | `status IN ('PendingTraining','Evaluated','Authorized','Revoked')` |
| `assessment_result` | `pk_assessment_result` | `PRIMARY KEY (tenant_id, id)` |
| `assessment_result` | `fk_assessment_result_competency_record_tenant_id_competency_id` | `FOREIGN KEY (tenant_id, competency_id) REFERENCES qams.competency_record(tenant_id, id) ON DELETE CASCADE` |
| `training_assignment` | `pk_training_assignment` | `PRIMARY KEY (tenant_id, id)` |
| `training_assignment` | `ck_training_completion_order` | `completed_at_utc IS NULL OR completed_at_utc >= created_at_utc` |
| `test_authorization` | `pk_test_authorization` | `PRIMARY KEY (tenant_id, id)` |
| `test_authorization` | `ck_test_authorization_scope_domain` | `scope IN ('Perform','ReviewAndRelease','Train')` |
| `test_authorization` | `ck_test_authorization_status_domain` | `status IN ('Active','Suspended','Revoked','Expired')` |

**Measured absences — every one of these is a real, provable gap:**

- **No `CHECK` on `assessment_result.score`.** The `0..100` rule (`COMP-011`) lives only in the domain. A direct `INSERT … score = 999` succeeds. → GAP-COMP-010.
- **No `CHECK` on `competency_record.validity_months >= 1`** and none on `expires_at` vs `created_at_utc`.
- **No `CHECK` on `test_authorization.expires_on > granted_on`.** `AUTHZ-001` is domain-only. → GAP-COMP-023.
- **No foreign keys at all on `test_authorization`** — `user_id`, `test_catalog_item_id` and `competency_record_id` are unconstrained `uuid` columns. The "evidence" link is an application-level convention. → GAP-COMP-012.
- **No unique index on `(tenant_id, user_id, test_catalog_item_id, scope)`** for in-force rows. `AUTHZ-005` is a read-then-write check with no database backstop. → GAP-COMP-011.
- `ix_test_authorization_competency_record_id` is **not** tenant-prefixed (measured), unlike its three siblings — the saga's `Where(a => a.CompetencyRecordId == …)` relies on RLS plus the EF filter for isolation, not on the index.

**Audit-trail coverage.** `FieldChangeInterceptor` (`src/NT.QAMS.Infrastructure/Persistence/Interceptors/FieldChangeInterceptor.cs:22-118`) excludes only the ledgers and plumbing (`:27-31`); all four competency entity types therefore produce `audit.field_change` rows on `Created`/`Modified`/`Deleted` (`:66-77`), one row **per changed property** (`:86-100`), stamped with `ActorId`, `Actor`, the scoped `Reason` and the clock time (`:103-117`). Expect `EntityType` values `CompetencyRecord`, `AssessmentResult`, `TrainingAssignment`, `TestAuthorization` exactly (the interceptor uses `entry.Entity.GetType().Name`, `:107`).

**Signed-record immutability.** `qams.reject_frozen_mutation()` covers the 12 analytical study roots plus `uncertainty_budget` (conventions §2). **None of the four competency tables is protected by it** — an `Authorized` or `Revoked` competency, and a `Revoked` authorization, remain physically mutable by any writer holding the tenant GUC. → GAP-COMP-024.

### 1.16 States

| Aggregate | States (column domain enforced by `CHECK`) |
|---|---|
| `CompetencyRecord.Status` | `PendingTraining` · `Evaluated` · `Authorized` · `Revoked` |
| `TestAuthorization.Status` | `Active` · `Suspended` · `Revoked` · `Expired` |
| `TrainingAssignment` | not an enum — boolean `Completed` (`false` → `true`, one-way) |

### 1.17 Frontend

`frontend/src/app/features/competency/` — 7 files, 1,018 lines. Routes are children of the shell's `tenantOnlyGuard` branch (`frontend/src/app/app.routes.ts:33-34`); **no per-route permission guard is applied** to any of them.

| Route | Component | app.routes.ts |
|---|---|---|
| `/competencies` (+ `/:id` child, rendered in a 920 px drawer) | `CompetencyListComponent` / `CompetencyDetailComponent` | `:146-154` |
| `/authorizations` (+ `/:id` child, 920 px drawer) | `AuthorizationMatrixComponent` / `AuthorizationDetailComponent` | `:156-164` |
| `/training` | `TrainingQueueComponent` | `:166-168` |

Facades: `CompetencyFacade` (`competency.facade.ts:16-162`, signal-based, `loadMore` pager over the API-004 envelope `:62-72`), `AuthorizationsFacade` (`authorizations.facade.ts:11-75`, **no pager** — mirrors the unpaginated endpoint). Both surface the server's `problem+json` `title` as the error string (`competency.facade.ts:156-161`, `authorizations.facade.ts:69-74`) — **the `code` extension is discarded**, so the UI shows prose, never the domain code.

UI permission gates (client-side only, cosmetic — the server gate is authoritative):

| Control | Client key | Server key on the same action | Match? |
|---|---|---|---|
| "New competency" button | `competencies.create` (`competency-list.component.ts:25`) | `competencies.create` | yes |
| Score form | `competencies.edit` (`competency-detail.component.ts:54`) | `competencies.edit` | yes |
| Authorize button | `competencies.approve` (`competency-detail.component.ts:65-67`) | `competencies.approve` | yes |
| Revoke form | **`competencies.approve`** (same `@if` block, `competency-detail.component.ts:65`, `:69-75`) | **`competencies.void`** (`CompetenciesController.cs:53`) | **NO** — GAP-COMP-025 |
| "New authorization" | `test-authorizations.create` (`authorization-matrix.component.ts:30`) | same | yes |
| Suspend | `test-authorizations.edit` (`authorization-detail.component.ts:50`) | same | yes |
| Reinstate | `test-authorizations.approve` (`authorization-detail.component.ts:59`) | same | yes |
| Revoke | `test-authorizations.void` (`authorization-detail.component.ts:63`) | same | yes |
| "New training" | `training.create` (`training-queue.component.ts:20`) | same | yes |
| "Mark complete" | **no gate at all** (`training-queue.component.ts:59-61`) | **no gate at all** | consistent — and both are wrong (GAP-COMP-006) |

Other frontend facts a case will assert:

- `competency-list.component.ts:94` offers the status filter values `['PendingTraining','Evaluated','Authorized','Revoked','Expired']` — **`Expired` is not a `CompetencyStatus`**; selecting it always returns an empty list. GAP-COMP-015.
- `competency-list.component.ts:101` bounds `subject` at **200** chars; the backend allows **300** (`CompetencySlice.cs:22`) and the column is `varchar(300)`. `training-queue.component.ts:97` repeats the 200-char bound with **no** server-side counterpart at all.
- `competency-detail.component.ts:127` bounds the revoke reason at **500**; the server allows **1000** (`CompetencySlice.cs:51`).
- `competency-detail.component.ts:50` hard-codes the pass threshold `a.score >= 80` in the template — a second, uncoupled copy of `PassMark`. GAP-COMP-017.
- `competency-detail.component.ts:113` renders the stepper as `PendingTraining → Evaluated → Authorized`; `Revoked` is off-path.
- `authorization-detail.component.ts:101` renders the stepper as `Active → Expired`; `Suspended` and `Revoked` are off-path.
- The grant drawer offers **only** the picked person's `Authorized` competencies as evidence — it calls `listCompetencies(userId, 'Authorized')` where the first argument is the API's `traineeId` (`authorization-matrix.component.ts:210-216`, service signature `frontend/src/app/core/api/competency-api.service.ts:22-27`). It offers **no expiry field**, matching the inherited-expiry rule.
- The matrix renders one chip per authorization, coloured by status (`.active` green, `.suspended` orange, `.expired` slate, `.revoked` red, `authorization-matrix.component.ts:129-132`), lettered `P`/`R`/`T` (`:223-225`). Rows/columns are derived **only from authorizations that exist** (`:169-183`) — a person with no authorizations never appears, so the matrix cannot show a *hole*.
- "Expiring soon" stat = `Active && expiresOn <= today+30d` computed **client-side** (`authorization-matrix.component.ts:187-193`). No server-side expiring-soon endpoint exists.
- Both detail components embed `<qams-audit-trail [subject]="…id">`, so the record workspace surfaces the ledger.

### 1.18 Notifications

Only **one** notification exists in this whole module.

| Key | Const | Event | Default rule seeded at provisioning |
|---|---|---|---|
| `COMP_EXPIRED` | `NotificationEventPolicies.CompetencyExpiredKey`, `src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:39` | `CompetencyExpired` → `DispatchAsync(EventId, TenantId, key, { title = Subject })` (`:97-99`) | Recipients `"QualityManager,TenantAdmin"`, email on, subject `"Competency expired: {title}"`, body `"An authorization for {title} expired and requires requalification."` (`:148-149`, `:160`) |

There is **no** notification for `CompetencyAuthorized`, `CompetencyRevoked`, `TestAuthorizationExpired` or `TestAuthorizationRevoked`, and none for an *upcoming* expiry. Every case in this module whose "Expected Notification" is not `COMP_EXPIRED` must read `n/a — no notification is defined for this event (NotificationPolicies.cs:35-45)`. → GAP-COMP-022.

### 1.19 Existing automated coverage (do not duplicate; extend)

| Test | Covers |
|---|---|
| `tests/NT.QAMS.Domain.UnitTests/Resources/EquipmentAndCompetencyTests.cs` (`CompetencyRecordTests`, from `:84`) | 79 → `PendingTraining`, 85 → `Evaluated`, append-only attempts; `SOD-COMP-001` on self-score and self-authorize |
| `tests/NT.QAMS.Domain.UnitTests/Competency/TestAuthorizationTests.cs` | `SOD-AUTHZ-001`, `AUTHZ-001`, suspend/reinstate, `AUTHZ-013`, saga-shaped `SuspendIfActive`, terminal revoke + event |
| `tests/NT.QAMS.Application.UnitTests/Resources/ScheduledSweepTests.cs:66-117` | Competency expiry through the sweep, `CompetencyExpired` on the outbox stamped with the correct tenant |
| `tests/NT.QAMS.Architecture.Tests/CommandPolicyTests.cs` | Every command (including all 8 COMP commands) carries exactly one `CommandPolicyAttribute` |
| `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` | The 22 routes are frozen |

**No existing test covers:** the grant evidence gate (`AUTHZ-002/003/004/005`), the lapse saga end-to-end, any HTTP status code for this module, the permission gates, RLS on these four tables, or any frontend component (there is no `.spec.ts` under `features/competency/`).

---

## 2. Divergences from the commissioning brief

| # | What the brief assumes | What the code actually does | Evidence (file:line) | Gap |
|---|---|---|---|---|
| D1 | Competency authorization is signed electronically (Part 11 §11.100/§11.200) | No signature anywhere in the path. `Authorize` takes only an actor id and a date. `competencies.sign` / `test-authorizations.sign` exist in the catalogue but no endpoint or command consumes them | `CompetencyRecord.cs:98-115`; `PermissionCatalog.cs:116-121`, `:165`, `:167`; `CompetenciesController.cs:44-50` | GAP-COMP-007 |
| D2 | Pass mark is configurable per method / per test | `public const int PassMark = 80` — a compile-time constant, one value for the whole platform, all tenants | `CompetencyRecord.cs:33` | GAP-COMP-008 |
| D3 | Scores may be fractional / percentage with decimals | `int` in the domain, `int` on the DTO, `integer` in PostgreSQL | `CompetencyRecord.cs:19`; `ResourceContracts.cs:64`; measured column type | GAP-COMP-009 |
| D4 | Segregation of duties means preparer ≠ approver | Only `actorId != TraineeId` is checked. `EnsureSignerIsNotPreparer` is available on the base class and **not called** — the person who created the competency record may authorize it | `CompetencyRecord.cs:106-109`; `AggregateRoot.cs:35-42` | GAP-COMP-026 |
| D5 | A `SOD_VIOLATION` code | Does not exist. The real codes are `SOD-COMP-001` (422) and `SOD-AUTHZ-001` (422) | conventions §2; `CompetencyRecord.cs:91`,`:108`; `TestAuthorization.cs:43` | — (already a package-level gap) |
| D6 | Expired competency reaches a distinct `Expired` state | Expiry returns it to `PendingTraining` and nulls `AuthorizedBy`; there is no `Expired` member | `CompetencyRecord.cs:6`, `:125-126` | GAP-COMP-015 (the frontend believes otherwise) |
| D7 | Authorization refusals return 403 and business-rule failures return 422 | True as a rule, but the `TestAuthorization` aggregate uses `AUTHZ-` as its ordinary error prefix, so eight business rules return **403** and `AUTHZ-404` returns **403 instead of 404** | `DomainExceptionHandler.cs:63-74`; `TestAuthorization.cs:48`,`:73`,`:98`,`:114`; `AuthorizationSlice.cs:41`,`:50`,`:55`,`:63`,`:128`,`:175` | GAP-COMP-001, -002 |
| D8 | Error codes are unique across the system ("no magic strings", `CLAUDE.md` §2.2) | `AUTHZ-001` and `AUTHZ-002` each have **two** unrelated meanings — one in the authorization pipeline, one in the test-authorization domain | `AuthorizationBehavior.cs:60`,`:83` vs `TestAuthorization.cs:48`, `AuthorizationSlice.cs:41` | GAP-COMP-003 |
| D9 | Read access to personnel competence is privilege-gated | All five GET actions carry `[Authorize]` only. Any authenticated tenant user — including `ExternalAuditor` — reads every competency, every score, every authorization | `CompetenciesController.cs:16-25`, `:66-71`; `TestAuthorizationsController.cs:17-24` | GAP-COMP-005 |
| D10 | Every write is privilege-gated | `POST /api/training-assignments/{id}/complete` has **no** `[RequirePermission]`; only the `RequireInternalActor` tier applies | `CompetenciesController.cs:82-87`; `CompetencySlice.cs:118` | GAP-COMP-006 |
| D11 | Every list endpoint uses the API-004 pagination envelope | `GET /api/test-authorizations` returns a bare unbounded `IReadOnlyList` | `AuthorizationSlice.cs:133-134`, `:154-162` | GAP-COMP-013 |
| D12 | Training completion feeds competency | `TrainingAssignment.Complete` flips a boolean and stamps a time. It raises no event and touches no `CompetencyRecord`; the two aggregates are unlinked apart from a shared free-text `Subject` | `CompetencyRecord.cs:180-189`; `CompetencySlice.cs:121-131` | GAP-COMP-021 |
| D13 | Every regulated change carries a reason for change | Only revoke/suspend carry one, in the body. Scoring and authorizing carry none, and `ChangeReasonMiddleware` never fires because the module has no DELETE | conventions §2; whole controller pair — no `HttpDelete` | GAP-COMP-018 |
| D14 | Advance warning before a competency lapses | The sweep acts only when `ExpiresAt <= today`. The only "expiring soon" logic is a client-side stat computed in the browser | `ScheduledSweepService.cs:104-109`; `authorization-matrix.component.ts:187-193` | GAP-COMP-022 |
| D15 | Competency records are immutable once authorized | `reject_frozen_mutation()` does not cover any competency table | conventions §2; measured constraint list | GAP-COMP-024 |
| D16 | The authorization matrix can be exported for an audit | No export endpoint, no ClosedXML/QuestPDF path for this module (`competencies.export` / `test-authorizations.export` are unwired) | permission-key audit, §1.14 | GAP-COMP-005 |

---

## 3. State-transition matrices

### 3.1 `CompetencyRecord` state machine

**Trigger legend.** `Assign` = `CompetencyRecord.Assign` (`:54`). `Score(<80)` / `Score(>=80)` = `ScoreAssessment` (`:77`). `Authorize` = `Authorize` (`:98`). `ExpireIfDue(due)` = sweep call with `ExpiresAt <= asOf` (`:118`). `Revoke` = `Revoke` (`:130`). `Self` = actor equals `TraineeId`.

| From \ Trigger | `Score(<80)` | `Score(>=80)` | `Score(Self)` | `Score(0..100 violated)` | `Authorize` | `Authorize(Self)` | `ExpireIfDue(due)` | `ExpireIfDue(not due)` | `Revoke(reason)` | `Revoke(blank)` |
|---|---|---|---|---|---|---|---|---|---|---|
| *(none)* → `Assign` valid | → **PendingTraining** (`:72`) | — | — | — | — | — | — | — | — | — |
| **PendingTraining** | → PendingTraining, attempt appended (`:94-95`) | → **Evaluated** (`:95`) | ✗ `SOD-COMP-001` 422 (`:91`) | ✗ `COMP-011` 422 (`:86`) | ✗ `COMP-012` 409 (`:103`) | ✗ `COMP-012` 409 — the state guard fires **before** the SoD guard (`:100` before `:106`) | no-op (`:120`) | no-op | ✗ `COMP-013` 409 (`:134`) | ✗ `COMP-013` 409 (state guard first) |
| **Evaluated** | → **PendingTraining** (`:95`) — a later failing attempt *demotes* the record | → Evaluated (`:95`) | ✗ `SOD-COMP-001` 422 | ✗ `COMP-011` 422 | → **Authorized**, `AuthorizedBy` set, `ExpiresAt = asOf + ValidityMonths`, raises `CompetencyAuthorized` (`:111-114`) | ✗ `SOD-COMP-001` 422 (`:108`) | no-op | no-op | ✗ `COMP-013` 409 | ✗ `COMP-013` 409 |
| **Authorized** | ✗ `COMP-010` 409 (`:81`) | ✗ `COMP-010` 409 | ✗ `COMP-010` 409 (state guard first) | ✗ `COMP-010` 409 (state guard first) | ✗ `COMP-012` 409 | ✗ `COMP-012` 409 | → **PendingTraining**, `AuthorizedBy = null`, `ExpiresAt` **left populated**, raises `CompetencyExpired` (`:125-127`) | no-op (`:120`) | → **Revoked**, reason trimmed, raises `CompetencyRevoked` (`:142-144`) | ✗ `COMP-014` 422 (`:139`) |
| **Revoked** | ✗ `COMP-010` 409 | ✗ `COMP-010` 409 | ✗ `COMP-010` 409 | ✗ `COMP-010` 409 | ✗ `COMP-012` 409 | ✗ `COMP-012` 409 | no-op (`:120`) | no-op | ✗ `COMP-013` 409 | ✗ `COMP-013` 409 |

**Reachability notes for the case authors.**
1. `Revoked` is **terminal and absorbing** — no public method returns it to any other state.
2. Expiry is **not** terminal: `Authorized → PendingTraining` is the requalification loop. The record keeps its stale `ExpiresAt` until the next `Authorize` overwrites it (`:113`), so a `PendingTraining` record can legitimately display a past expiry date. A UI case should assert that.
3. Guard-ordering is testable and matters: in `ScoreAssessment` the order is **state → range → SoD** (`:79`, `:84`, `:89`); in `Authorize` it is **state → SoD** (`:100`, `:106`). A self-assessment on an `Authorized` record therefore returns `COMP-010`/409, *not* `SOD-COMP-001`/422.
4. The `Evaluated → PendingTraining` demotion on a later failing attempt (row 2, column 1) is an easily-missed edge: a person who passes then re-sits and fails loses their `Evaluated` standing.

### 3.2 `TestAuthorization` state machine

| From \ Trigger | `Grant` | `Suspend(reason)` | `Suspend(blank)` | `SuspendIfActive` | `Reinstate(asOf < ExpiresOn)` | `Reinstate(asOf >= ExpiresOn)` | `Revoke(reason)` | `Revoke(blank)` | `ExpireIfDue(due)` | `ExpireIfDue(not due)` |
|---|---|---|---|---|---|---|---|---|---|---|
| *(none)*, `grantedBy != userId`, `expiresOn > grantedOn` | → **Active** (`:60`) | — | — | — | — | — | — | — | — | — |
| *(none)*, `grantedBy == userId` | ✗ `SOD-AUTHZ-001` **422** (`:43`) | — | — | — | — | — | — | — | — | — |
| *(none)*, `expiresOn <= grantedOn` | ✗ `AUTHZ-001` **403** (`:48`) | — | — | — | — | — | — | — | — | — |
| **Active** | — | → **Suspended**, reason trimmed (`:76-77`) | ✗ `AUTHZ-011` **403** (`:73`) | → Suspended (`:83-86`) | ✗ `AUTHZ-012` 409 (`:93`) | ✗ `AUTHZ-012` 409 (state guard first, `:91` before `:96`) | → **Revoked**, raises `TestAuthorizationRevoked` (`:117-119`) | ✗ `AUTHZ-015` **403** (`:114`) | → **Expired**, raises `TestAuthorizationExpired` (`:130-131`) | no-op (`:125`) |
| **Suspended** | — | ✗ `AUTHZ-010` 409 (`:68`) | ✗ `AUTHZ-010` 409 (state guard first) | no-op (`:83`) | → **Active**, `SuspensionReason = null` (`:101-102`) | ✗ `AUTHZ-013` **403** (`:98`) | → **Revoked** (`:117-119`) | ✗ `AUTHZ-015` **403** | → **Expired** (`:130-131`) — a suspended entry lapses too | no-op |
| **Revoked** | — | ✗ `AUTHZ-010` 409 | ✗ `AUTHZ-010` 409 | no-op (`:83`) | ✗ `AUTHZ-012` 409 | ✗ `AUTHZ-012` 409 | ✗ `AUTHZ-014` 409 (`:109`) | ✗ `AUTHZ-014` 409 (state guard first, `:107` before `:112`) | no-op (`:125`) | no-op |
| **Expired** | — | ✗ `AUTHZ-010` 409 | ✗ `AUTHZ-010` 409 | no-op | ✗ `AUTHZ-012` 409 | ✗ `AUTHZ-012` 409 | ✗ `AUTHZ-014` 409 | ✗ `AUTHZ-014` 409 | no-op (`:125`) | no-op |

**Reachability notes.**
1. `Revoked` and `Expired` are both **absorbing**; there is no path out of either. Requalification always means a *new* grant.
2. `Expired` is reachable from `Suspended` as well as `Active` (`:125`) — a suspended authorization still lapses on its date and can then never be reinstated (`AUTHZ-012` blocks reinstating an `Expired` one).
3. **Trap the case authors must respect:** `AUTHZ-005` treats `Suspended` as "in force" (`AuthorizationSlice.cs:58-64`). So while an authorization sits `Suspended` with a *future* `ExpiresOn`, a replacement grant for the same (user, test, scope) is refused **403 `AUTHZ-005`**, and the only lawful routes are `Reinstate` or `Revoke`-then-grant. Once the `ExpiresOn` has passed, `Reinstate` gives **403 `AUTHZ-013`**, so the only remaining route is `Revoke`-then-grant. Neither the API nor the UI tells the operator this. Written up in **GAP-COMP-011** (see §8), which also covers the concurrency half of that guard.
4. `TestAuthorizationRevoked` carries `ActorId`. When the saga revokes it, that actor is the person who revoked the **competency**, not a person who acted on the authorization (`CompetencyLapseAuthorizationPolicy.cs:55`) — an attribution nuance worth an audit-trail case.

### 3.3 `TrainingAssignment`

| From \ Trigger | `Create(subject)` | `Create(blank)` | `Complete` |
|---|---|---|---|
| *(none)* | → `Completed = false`, `CompletedAtUtc = null` (`:171-177`) | ✗ `TRN-001` 422 (`:168`) | — |
| `Completed = false` | — | — | → `Completed = true`, `CompletedAtUtc = clock.UtcNow` (`:187-188`) |
| `Completed = true` | — | — | ✗ `TRN-002` 409 (`:184`) |

No state depends on `DueDate`; overdue is a **presentation** concept computed in the browser (`training-queue.component.ts:110`).

### 3.4 Cross-aggregate state coupling (the saga)

| Competency transition | Dependent authorization selection | Applied | Resulting authorization state | Source |
|---|---|---|---|---|
| `Authorized → PendingTraining` (expiry) | `CompetencyRecordId == id && Status == Active` | `SuspendIfActive(reason)` | `Suspended` (or unchanged if not `Active`) | `CompetencyLapseAuthorizationPolicy.cs:29-35` |
| `Authorized → Revoked` | `CompetencyRecordId == id && (Status == Active \|\| Status == Suspended)` | `Revoke(e.RevokedBy, reason)` | `Revoked` | `:48-58` |
| `Evaluated → Authorized` | — | — | no effect on existing authorizations; their `ExpiresOn` is **not** extended | `CompetencyRecord.cs:98-115` (no saga subscribes to `CompetencyAuthorized`) |
| Sweep same-run expiry | `(Active \|\| Suspended) && ExpiresOn <= today` | `ExpireIfDue(today)` | `Expired` | `ScheduledSweepService.cs:111-117` |

---

## 4. Decision tables

### 4.1 DT-1 — Competency authorization (`POST /api/competencies/{id}/authorize`)

The brief poses four conditions: *score ≥ PassMark? · assessor ≠ trainee? · signature valid? · record current?* Two of them do not exist in the build; both are represented below as **N/E — not evaluated** with the proof, because a decision table that silently drops a condition is a false record.

**Conditions actually evaluated, in execution order:**

| Id | Condition | Where |
|---|---|---|
| K1 | Caller holds `competencies.approve` | `CompetenciesController.cs:45` → `RequirePermissionAttribute.cs:38-59` |
| K2 | Caller is an authenticated internal actor (not `ExternalAuditor`) | `CompetencySlice.cs:41` → `AuthorizationBehavior.cs:58-85` |
| K3 | Record exists in the caller's tenant | `CompetencySlice.cs:57-61` (+ RLS + EF filter) |
| K4 | `Status == Evaluated` — *this is the only place the pass mark is honoured*, because `Evaluated` is set only when a scored attempt reached ≥ 80 (`CompetencyRecord.cs:95`) | `CompetencyRecord.cs:100-104` |
| K5 | `actorId != TraineeId` | `CompetencyRecord.cs:106-109` |
| **N/E-1** | Signature valid | **Never evaluated.** No `ESignatureService` call, no PIN, no password, no `SignatureRecord`, on any path in `CompetenciesController` / `CompetencySlice` / `CompetencyRecord` |
| **N/E-2** | "Record current" (competency not already past its expiry) | **Never evaluated at authorize time.** `Authorize` reads no clock other than the `asOf` it is handed and writes a fresh `ExpiresAt` (`:113`) |
| **N/E-3** | Assessor's own competence to assess | **Never evaluated.** `AssessorId` is simply `ICurrentUser.UserId` (`CompetencySlice.cs:73`) |

| Rule | K1 | K2 | K3 | K4 (`Evaluated`) | K5 (actor ≠ trainee) | Outcome |
|---|---|---|---|---|---|---|
| R1 | F | – | – | – | – | **403** `problem+json`, `code = AUTHZ-403` (`ProblemAuthorizationResultHandler.cs:16`) — no state change, no ledger row |
| R2 | T | F (external auditor) | – | – | – | **403**, `code = AUTHZ-002` from `AuthorizationBehavior.cs:83` |
| R3 | T | F (unauthenticated) | – | – | – | **403**, `code = AUTHZ-001` from `AuthorizationBehavior.cs:60` — *not* 401, and *not* the domain's `AUTHZ-001` |
| R4 | T | T | F | – | – | **404**, `code = COMP-404` (`CompetencySlice.cs:61`) |
| R5 | T | T | T | F (`PendingTraining`) | – | **409**, `code = COMP-012` |
| R6 | T | T | T | F (`Authorized`) | – | **409**, `code = COMP-012` |
| R7 | T | T | T | F (`Revoked`) | – | **409**, `code = COMP-012` |
| R8 | T | T | T | T | F | **422**, `code = SOD-COMP-001` |
| R9 | T | T | T | T | T | **204**. `status = Authorized`; `authorized_by = actor`; `expires_at = today + validity_months`; `CompetencyAuthorized` on the outbox → audit-ledger entry; `audit.field_change` rows for `Status`, `AuthorizedBy`, `ExpiresAt`, `ModifiedAtUtc`, `ModifiedBy`; **no notification** |

**Impossible combinations** (do not author a case for them): K4 = T with a highest score < 80 — unreachable, because the only writer of `Evaluated` is `CompetencyRecord.cs:95` under `score >= PassMark`. The pass mark can therefore only be tested *through* `ScoreAssessment`, never at the authorize call.

### 4.2 DT-2 — Score boundary (`POST /api/competencies/{id}/assessments`)

Score is `int` (§1.2). Conditions: S1 `Status ∈ {PendingTraining, Evaluated}`; S2 `0 <= score <= 100`; S3 `actor != TraineeId`; S4 `score >= 80`.

| Rule | Input score | S1 | S2 | S3 | S4 | Outcome |
|---|---|---|---|---|---|---|
| R1 | any | F | – | – | – | **409** `COMP-010` |
| R2 | `-1` | T | F | – | – | **422** `COMP-011` |
| R3 | `101` | T | F | – | – | **422** `COMP-011` |
| R4 | `50`, actor = trainee | T | T | F | – | **422** `SOD-COMP-001`, **no attempt appended** (the guard precedes `_assessments.Add`, `:89` before `:94`) |
| R5 | `0` | T | T | T | F | **204**; attempt appended; `Status = PendingTraining` |
| R6 | `79` | T | T | T | F | **204**; `Status = PendingTraining` |
| R7 | `80` | T | T | T | T | **204**; `Status = Evaluated` — **the inclusive boundary** (`score >= PassMark`, `:95`) |
| R8 | `81` | T | T | T | T | **204**; `Status = Evaluated` |
| R9 | `100` | T | T | T | T | **204**; `Status = Evaluated` |
| R10 | `79` on an already-`Evaluated` record | T | T | T | F | **204**; `Status` **demoted back to `PendingTraining`** |
| R11 | `79.99` | – | – | – | – | Never reaches the domain — `int` binding failure at the MVC input formatter. Assert the observed status/body; label `[RNV]` until measured |

### 4.3 DT-3 — Grant a test authorization (`POST /api/test-authorizations`)

Conditions in the handler's own order (`AuthorizationSlice.cs:31-71`): G1 permission `test-authorizations.create`; G2 internal actor; G3 authenticated (`user.UserId` non-null); G4 `Scope` parses to `AuthorizationScope`; G5 catalog test exists; G6 catalog test `IsActive`; G7 competency exists; G8 `competency.TraineeId == UserId`; G9 `competency.Status == Authorized && ExpiresAt != null`; G10 no in-force duplicate (`Active` or `Suspended`) for (user, test, scope); G11 `grantedBy != userId`; G12 `expiresOn > grantedOn`.

| Rule | First failing condition | Outcome |
|---|---|---|
| R1 | G1 | **403** `AUTHZ-403` |
| R2 | G2 | **403** `AUTHZ-002` (behaviour) |
| R3 | G3 | **401** `AUTH-003` (`:34`) |
| R4 | G4 (`"perform "`, `"Performer"`, `""`, `"1"`) | **500** — unhandled `ArgumentException` from `Enum.Parse` (`:35`). `""` is caught earlier by the validator → 400. **GAP-COMP-004** |
| R5 | G5 | **404** `ORG-404` (`:38`) |
| R6 | G6 | **403** `AUTHZ-002` (`:41`) — indistinguishable by code from R2 |
| R7 | G7 | **404** `COMP-404` (`:47`) |
| R8 | G8 | **403** `AUTHZ-003` (`:50`) |
| R9 | G9 (`PendingTraining` / `Evaluated` / `Revoked`, or null `ExpiresAt`) | **403** `AUTHZ-004` (`:55`) |
| R10 | G10 (an `Active` duplicate) | **403** `AUTHZ-005` (`:63`) |
| R11 | G10 (a **`Suspended`** duplicate) | **403** `AUTHZ-005` — the trap of §3.2 note 3 |
| R12 | G11 (self-grant) | **422** `SOD-AUTHZ-001` (`TestAuthorization.cs:43`) |
| R13 | G12 (`competency.ExpiresAt <= today`) | **403** `AUTHZ-001` (`TestAuthorization.cs:48`) |
| R14 | none | **201** + `Location: /api/test-authorizations/{id}`, body `{ id }`. Row: `status='Active'`, `granted_on = today`, `expires_on = competency.expires_at`, `granted_by = actor`. **No domain event is raised on grant** — so no audit-ledger entry, only `audit.field_change` `Created`. |

**Note on R14:** because grant raises no event, the *creation* of an authorization is absent from the tamper-evident hash chain while its *revocation* and *expiry* are present. That asymmetry is GAP-COMP-019.

### 4.4 DT-4 — Reinstate (`POST /api/test-authorizations/{id}/reinstate`)

| Rule | Permission `…approve` | Exists | `Status == Suspended` | `ExpiresOn > today` | Outcome |
|---|---|---|---|---|---|
| R1 | F | – | – | – | **403** `AUTHZ-403` |
| R2 | T | F | – | – | **403** `AUTHZ-404` — *should be 404*, GAP-COMP-002 |
| R3 | T | T | F | – | **409** `AUTHZ-012` |
| R4 | T | T | T | F (`ExpiresOn == today`) | **403** `AUTHZ-013` — the guard is `ExpiresOn <= asOf` (`:96`), so **the expiry date itself is already lapsed** |
| R5 | T | T | T | T | **204**; `Status = Active`; `suspension_reason = NULL` |

### 4.5 DT-5 — Sweep expiry boundary (both aggregates)

`today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)` (`ScheduledSweepService.cs:88`).

| Rule | `ExpiresAt`/`ExpiresOn` vs `today` | Competency (`CompetencyRecord.cs:120`) | Authorization (`TestAuthorization.cs:125`) |
|---|---|---|---|
| R1 | `today + 1` | no-op | no-op |
| R2 | `today` (equal) | **expires** → `PendingTraining` + `CompetencyExpired` | **expires** → `Expired` + `TestAuthorizationExpired` |
| R3 | `today - 1` | expires | expires |
| R4 | `null` (competency only) | no-op (`ExpiresAt is null`) | n/a — column is `NOT NULL` |
| R5 | any, status not `Authorized` | no-op | n/a |
| R6 | any, status ∉ {`Active`,`Suspended`} | n/a | no-op |

Both aggregates therefore expire **on** the stated date, not the day after. Every BVA case must use the triple `today-1 / today / today+1` against the aggregate's own date field, and must pin `IClock` — the sweep uses UTC calendar days, so a case run near midnight local time in a non-UTC zone can straddle a boundary.

---

## 6. UAT scenarios (Gherkin)

Business-readable, one per lifecycle decision a laboratory actually makes. Ids `TC-COMP-UAT-001…010` are consumed **here**.

```gherkin
Feature: Personnel competence governs who may perform a test (ISO 17025 §6.2.6, ISO 15189 §6.2)

  Background:
    Given I am signed in to laboratory "demo-lab"
    And the seeded role "Quality Manager" is assigned to me

  # TC-COMP-UAT-001  [IV]  CompetencyRecord.cs:54-75, :77-96, :98-115
  Scenario: A technologist becomes competent and is authorized
    Given a competency "SOP-CAL-045 balance calibration" is assigned to technologist Dana with a validity of 12 months
    And the competency is shown as "PendingTraining"
    When an assessor other than Dana records a score of 80
    Then the competency becomes "Evaluated"
    When I authorize the competency today
    Then the competency becomes "Authorized"
    And its requalification date is exactly 12 months after today
    And the record shows me as the authorizing person

  # TC-COMP-UAT-002  [IV]  CompetencyRecord.cs:95
  Scenario: A failing score keeps the person in training
    Given a competency assigned to technologist Dana in state "PendingTraining"
    When an assessor records a score of 79
    Then the competency remains "PendingTraining"
    And the attempt is kept in the assessment history
    And the authorize action is not offered

  # TC-COMP-UAT-003  [IV]  CompetencyRecord.cs:89-92, :106-109
  Scenario: Nobody signs off their own competence
    Given a competency assigned to me
    When I try to record my own assessment score
    Then the system refuses with the segregation-of-duties rule
    And when the competency is later "Evaluated" and I try to authorize it myself
    Then the system refuses with the same segregation-of-duties rule

  # TC-COMP-UAT-004  [IV]  AuthorizationSlice.cs:46-68
  Scenario: A test authorization may only rest on current, authorized competence
    Given technologist Dana holds an "Authorized" competency for "SOP-CAL-045" expiring on 30 June 2027
    And the catalogue test "CHOL-01 Total cholesterol" is active
    When I grant Dana the "Perform" authorization for "CHOL-01" evidenced by that competency
    Then the authorization is created as "Active"
    And its expiry is 30 June 2027, inherited from the competency and not entered by me

  # TC-COMP-UAT-005  [IV]  AuthorizationSlice.cs:53-56
  Scenario: A person still in training cannot be authorized to perform a test
    Given technologist Ravi holds a competency in state "Evaluated" but not yet authorized
    When I try to grant Ravi the "Perform" authorization for "CHOL-01" evidenced by that competency
    Then the system refuses and states that only a current, authorized competency can evidence an authorization
    And no authorization appears for Ravi in the matrix

  # TC-COMP-UAT-006  [IV]  ScheduledSweepService.cs:104-117; CompetencyLapseAuthorizationPolicy.cs:24-41
  Scenario: A lapsed competency takes its authorizations with it
    Given technologist Dana holds an "Authorized" competency whose requalification date is today
    And Dana holds an "Active" authorization evidenced by that competency
    When the daily compliance sweep runs
    Then Dana's competency returns to "PendingTraining" for requalification
    And Dana's authorization is no longer in force
    And the Quality Manager and Tenant Administrator receive the "Competency expired" notification naming the subject

  # TC-COMP-UAT-007  [IV]  CompetencyLapseAuthorizationPolicy.cs:43-64
  Scenario: Revoking a competency revokes everything that rested on it
    Given technologist Dana holds an "Authorized" competency and two authorizations evidenced by it, one "Active" and one "Suspended"
    When I revoke the competency with the reason "Repeated procedural deviations, ref NC-2026-118"
    Then both authorizations become "Revoked"
    And each carries a revocation reason quoting the competency subject and my reason
    And neither can be reinstated

  # TC-COMP-UAT-008  [IV]  TestAuthorization.cs:64-103
  Scenario: A temporary suspension is reversible while the authorization is still current
    Given technologist Dana holds an "Active" authorization expiring in six months
    When I suspend it with the reason "Pending review of two out-of-specification results"
    Then it shows as "Suspended" with that reason visible on the record
    When the review closes and I reinstate it
    Then it returns to "Active" and the suspension reason is cleared

  # TC-COMP-UAT-009  [IV]  TestAuthorization.cs:96-99, :123-132
  Scenario: A suspension that outlives its expiry cannot be revived
    Given technologist Dana holds a "Suspended" authorization whose expiry date is today
    When I try to reinstate it
    Then the system refuses and tells me the authorization has lapsed and a new one must be granted against a current competency
    And after the daily sweep the authorization shows as "Expired"

  # TC-COMP-UAT-010  [IV]  authorization-matrix.component.ts:79-111, :129-132
  Scenario: The authorization matrix is a reviewable record for an auditor
    Given several technologists hold authorizations across three catalogue tests in mixed states
    When I open the personnel authorization matrix
    Then each person is a row and each catalogue test a column
    And each authorization appears as a chip lettered "P", "R" or "T" for its scope
    And the chip colour distinguishes active, suspended, expired and revoked
    And opening a chip shows who granted it, when, on what competency evidence, and until when
```

**Honest caveat on UAT-006.** As §1.10 explains, when the competency and its authorization share the same date — which is what the grant path always produces — the sweep marks the authorization `Expired` in the same transaction and the saga's suspend is a no-op. The Gherkin above deliberately says *"no longer in force"* rather than *"Suspended"*. A case author who wants to observe the **Suspended** outcome must construct an authorization whose `ExpiresOn` is later than the competency's `ExpiresAt`, which requires direct data setup, not the API. Say so in that case's Preconditions.

---

## 7. Exploratory charters

Time-boxed, session-based. Ids `TC-COMP-EXPL-001…006` are consumed **here**.

**TC-COMP-EXPL-001 — Error-code semantics on the authorization surface**
*Explore* every `POST /api/test-authorizations/**` failure path *with* a permission-holding and a permission-lacking actor *to discover* how many distinct business failures are indistinguishable from a genuine permission denial. **Charter focus:** `AUTHZ-001`…`AUTHZ-005`, `AUTHZ-011`, `AUTHZ-013`, `AUTHZ-015`, `AUTHZ-404`, `AUTHZ-403`, and the behaviour's own `AUTHZ-001`/`AUTHZ-002`. **Oracle:** `DomainExceptionHandler.cs:54-80`, `AuthorizationBehavior.cs:52-85`. **Ship:** a code→status→meaning table, and a count of collisions. 90 min. Feeds GAP-COMP-001/002/003.

**TC-COMP-EXPL-002 — Requalification round trip**
*Explore* the full loop competency `Authorized` → sweep expiry → re-score → re-`Authorize` → re-grant, *with* the authorizations from the previous cycle still present, *to discover* whether an operator can ever reach a state from which no lawful next action exists. **Charter focus:** the `AUTHZ-005` duplicate check counting `Suspended` (`AuthorizationSlice.cs:58-64`) against `AUTHZ-012`/`AUTHZ-013` on reinstate. **Ship:** a reachability diagram of the loop and any dead ends, with the exact API sequence that reaches them. 120 min.

**TC-COMP-EXPL-003 — Status filters and their SQL**
*Explore* `GET /api/competencies?status=…` with valid, wrong-case, unknown, empty, whitespace and SQL-ish values, *with* the API's SQL log enabled, *to discover* whether `x.Status.ToString() == q.Status` (`CompetencySlice.cs:154`) translates to server-side SQL over a `HasConversion<string>` enum or falls back to client evaluation, and whether an unknown value is distinguishable from an empty result set. Compare with the enum-parsing sibling at `AuthorizationSlice.cs:148-152`. **Ship:** the emitted SQL, plan shape on a large table, and a recommendation. 60 min. Feeds GAP-COMP-018's neighbour.

**TC-COMP-EXPL-004 — The matrix as an audit artefact**
*Explore* the authorization matrix UI with an auditor's eyes — an `ExternalAuditor` login, `ar` locale, a person with no authorizations, a catalogue test that was deactivated after a grant, an authorization whose catalogue row is unreachable — *to discover* where the displayed matrix diverges from the enforced state. **Charter focus:** the inner `Join` at `AuthorizationSlice.cs:155-157` versus the null-tolerant detail query at `:183`; rows derived only from existing authorizations (`authorization-matrix.component.ts:169-183`); RTL sticky column (`:125`). **Ship:** screenshots of every divergence. 90 min. Feeds GAP-COMP-014.

**TC-COMP-EXPL-005 — Concurrency on the evidence gate**
*Explore* concurrent `POST /api/test-authorizations` for the same (user, test, scope) and concurrent `authorize`/`revoke` on one competency, *with* two sessions and a scripted burst, *to discover* whether the `AUTHZ-005` read-then-write check and the `xmin` token together prevent duplicates. **Oracle:** measured absence of a unique index (§1.15); `CONCURRENCY-409` at `DomainExceptionHandler.cs:21`. **Ship:** the row count after N parallel grants. 60 min. Feeds GAP-COMP-011. **Run this after any rate-limit-sensitive probe** — the auth partition is 10/min (conventions §3).

**TC-COMP-EXPL-006 — What an assessment leaves behind**
*Explore* the evidence trail of one full competency lifecycle *with* `psql` open on `audit.field_change` and `audit.audit_trail_entry` (remember `set_config('app.bypass_rls','on',false)`), *to discover* exactly which acts are hash-chained, which are only field-logged, and which are attributable to the wrong actor. **Charter focus:** `ScoreAssessment` raises no event (`CompetencyRecord.cs:77-96`); grant raises no event; the saga stamps the competency-revoker as the authorization actor (`CompetencyLapseAuthorizationPolicy.cs:55`). **Ship:** a per-act table of `field_change` vs `audit_trail_entry` presence and the recorded actor. 90 min. Feeds GAP-COMP-019.

---

## 8. Gap Register (this module)

Twenty-six gaps, `GAP-COMP-001` … `GAP-COMP-026`. Severity scale: **Critical / High / Medium / Low**. No compliance verdict is given here beyond the permitted four (conventions §6.4).

---

**GAP-COMP-001 — `AUTHZ-` prefixed business rules are returned as HTTP 403**
- **Source reference:** `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:63-68`; `src/NT.QAMS.Domain/Competency/TestAuthorization.cs:48`, `:73`, `:98`, `:114`; `src/NT.QAMS.Application/Competency/AuthorizationSlice.cs:41`, `:50`, `:55`, `:63`.
- **Description:** The handler maps every `DomainException` whose code starts with `AUTHZ-` to 403 Forbidden, on the documented assumption that `AUTHZ-*` means an authorization refusal. The `TestAuthorization` aggregate and its slice use `AUTHZ-` as the ordinary error prefix of the *test-authorization module*. Eight business-rule failures — inactive catalogue test, wrong person's competency, competency not authorized, duplicate in force, missing suspension reason, lapsed reinstate, missing revocation reason, expiry-before-grant — are therefore returned as "you do not have permission".
- **Impact:** An operator who supplies a blank revocation reason is told they lack permission. Support and audit teams cannot distinguish a privilege problem from a data problem. Automated clients that treat 403 as a session/role failure (as the SPA's own interceptors do for 401/403 classes) will mis-handle recoverable input errors.
- **Testing limitation:** Every negative case on the test-authorization surface must assert 403 with a `code` extension rather than the 422 the conventions lead an author to expect. Cases asserting "business rule → 422" would fail against correct-as-built behaviour.
- **Recommended clarification:** Confirm with the architecture owner whether `AUTHZ-` is reserved for authorization refusals. If so, renumber the test-authorization domain codes to a non-colliding prefix (e.g. `TAUTH-`), or switch the handler from a prefix test to an explicit code set.
- **Suggested acceptance criteria:** Given a request that violates a test-authorization business rule, when it is submitted by an actor holding the required permission, then the response is `422 Unprocessable Entity` with the module's own `code`; and no `403` is returned unless the endpoint or command policy denied the actor.
- **Severity:** **High**
- **Responsible role:** Solution Architect (code taxonomy) with Quality Manager sign-off.

---

**GAP-COMP-002 — `AUTHZ-404` returns 403, not 404**
- **Source reference:** `DomainExceptionHandler.cs:63` (the `AUTHZ-` arm) precedes `:69` (the `-404` arm); code raised at `AuthorizationSlice.cs:128` and `:175`.
- **Description:** Switch-arm ordering means "Test authorization not found" is classified as a permission denial before the not-found rule is ever consulted. Every sibling module's `*-404` code correctly yields 404 (`COMP-404`, `TRN-404`, `ORG-404`); this one does not.
- **Impact:** `GET /api/test-authorizations/{unknown-guid}` and all four workflow POSTs on an unknown id answer 403. A caller cannot distinguish "does not exist" from "not allowed", which also weakens the resource-enumeration story in the opposite direction from the anti-enumeration design used for workspace lookup.
- **Testing limitation:** A conventional "unknown id → 404" case cannot be written for this endpoint. It must be written as "unknown id → 403 `AUTHZ-404`" and flagged `[ID]`, or deferred as `[GD]` on this gap.
- **Recommended clarification:** Decide whether the fix is arm reordering (move the `-404` suffix arm above the `AUTHZ-` prefix arm) or a code rename under GAP-COMP-001. Reordering alone changes behaviour for any other `AUTHZ-*-404` code.
- **Suggested acceptance criteria:** Given any test-authorization endpoint addressed with an id that does not exist in the caller's tenant, when the request is made by an actor holding the endpoint's permission, then the response is `404 Not Found` with `code = AUTHZ-404`.
- **Severity:** **Medium**
- **Responsible role:** Backend Lead.

---

**GAP-COMP-003 — Error-code collision: `AUTHZ-001` and `AUTHZ-002` each have two meanings**
- **Source reference:** `src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs:60` (`AUTHZ-001` = "an authenticated actor is required") and `:83` (`AUTHZ-002` = "role not permitted"); versus `src/NT.QAMS.Domain/Competency/TestAuthorization.cs:48` (`AUTHZ-001` = "expiry must fall after grant date") and `src/NT.QAMS.Application/Competency/AuthorizationSlice.cs:41` (`AUTHZ-002` = "test is inactive").
- **Description:** Two unrelated subsystems mint identical machine-readable codes, both surfaced at HTTP 403 with only the human-readable `title` distinguishing them. `CLAUDE.md` §2.2 requires structured, non-magic error codes.
- **Impact:** No client, log query, alert rule or traceability matrix can key on `code` alone for these two values. A regression that starts denying every actor (`AUTHZ-002` from the behaviour) is indistinguishable in aggregated logs from a spike of grants against deactivated tests.
- **Testing limitation:** Cases asserting `code = AUTHZ-002` must additionally assert the `title` string or the request shape to be meaningful — an inherently brittle assertion. Two of the DT-3 rules (R2 and R6) cannot be told apart by response code at all.
- **Recommended clarification:** Ask the architecture owner for a codebook: which prefixes belong to cross-cutting pipeline concerns and which to modules, and whether a uniqueness test should be added to `Architecture.Tests`.
- **Suggested acceptance criteria:** An architecture test enumerates every string literal passed as the `code` argument of `DomainException`/`InvalidStateTransitionException` across the solution and asserts the set contains no duplicate that carries different messages.
- **Severity:** **Medium**
- **Responsible role:** Solution Architect.

---

**GAP-COMP-004 — An invalid `scope` string produces HTTP 500**
- **Source reference:** `src/NT.QAMS.Application/Competency/AuthorizationSlice.cs:35` (`Enum.Parse<AuthorizationScope>(c.Scope, ignoreCase: true)`); validator at `:17-26` checks only `NotEmpty`; `DomainExceptionHandler.cs:81` returns `null` for non-domain exceptions, so `UseExceptionHandler` (`src/NT.QAMS.WebApi/Program.cs:256`) falls through to the default 500.
- **Description:** `Enum.Parse` throws `ArgumentException` for any unrecognised value. Nothing catches it. A client sending `{"scope":"Performer"}` receives an unhandled server error.
- **Impact:** A client-supplied value causes a 5xx, which pollutes error budgets and alerting, and returns no actionable `code`. It is also the only known 500 on this module's surface, so it is a natural probe target.
- **Testing limitation:** A negative case must be authored expecting `500`, which is unusual enough that a reviewer will read it as a defect in the test rather than in the code — annotate it explicitly.
- **Recommended clarification:** Confirm whether the fix belongs in the validator (`IsEnumName`), in the handler (`Enum.TryParse` → `DomainException`), or by typing the DTO as the enum so the JSON binder rejects it at 400.
- **Suggested acceptance criteria:** Given `POST /api/test-authorizations` with `scope` set to a value outside `{Perform, ReviewAndRelease, Train}` in any casing, when the request is submitted, then the response is `400` or `422` with a machine-readable code, and no unhandled exception is logged.
- **Severity:** **High**
- **Responsible role:** Backend Lead.

---

**GAP-COMP-005 — Read endpoints are not privilege-gated; `.view` and `.export` keys are unwired**
- **Source reference:** `src/NT.QAMS.WebApi/Controllers/CompetenciesController.cs:16-25` and `:66-71`; `TestAuthorizationsController.cs:17-24` — all carry `[Authorize]` only. Keys defined at `src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:165-167` via the `SignedRecordLifecycle`/`FullRecordLifecycle` bundles (`:110-121`).
- **Description:** `competencies.view`, `training.view`, `test-authorizations.view` and the three `.export` keys are presented to the administrator in the privilege matrix but govern nothing. Any authenticated tenant user reads every colleague's assessment scores, pass/fail history and authorization record.
- **Impact:** Personnel competence data is HR-adjacent and, in some jurisdictions, personal data. The privilege screen misrepresents the enforced state, which is itself an access-review finding (ISO 17025 §7.11.2; Part 11 §11.10(d)). There is also no export capability at all, so `.export` is doubly inert.
- **Testing limitation:** No case can demonstrate that a role *without* `competencies.view` is denied a read, because no such denial exists. Read-authorization cases must be written as `[ID]` documenting the current permissive behaviour, or `[GD]` on this gap.
- **Recommended clarification:** Confirm the intended read audience per module — and specifically whether `ExternalAuditor` should see individual assessment scores, or only the authorization matrix.
- **Suggested acceptance criteria:** Every GET action in `CompetenciesController`, `TrainingAssignmentsController` and `TestAuthorizationsController` carries `[RequirePermission(<module>, PermissionAction.View)]`; a role lacking the key receives `403` with `code = AUTHZ-403`; and either an export endpoint exists behind `.export` or the `.export` key is removed from these modules' action bundles.
- **Severity:** **High**
- **Responsible role:** Security Lead with Quality Manager.

---

**GAP-COMP-006 — `POST /api/training-assignments/{id}/complete` has no permission gate**
- **Source reference:** `src/NT.QAMS.WebApi/Controllers/CompetenciesController.cs:82-87` — no `[RequirePermission]`; command policy is `RequireInternalActor` (`CompetencySlice.cs:118`), which only excludes `ExternalAuditor`. The sibling `POST /api/training-assignments` does carry `training.create` (`:74`). The UI button is likewise ungated (`training-queue.component.ts:59-61`).
- **Description:** Any authenticated internal user can mark **any** training assignment in the tenant complete, including one assigned to someone else.
- **Impact:** Training completion is a compliance record (ISO 17025 §6.2.2/§6.2.3). Its integrity depends on who may assert it. The `training.edit` key exists and is granted to Analyst and Department Head — it simply is not used.
- **Testing limitation:** A "least-privilege role cannot complete another person's training" case cannot pass. It must be authored `[GD]` on this gap.
- **Recommended clarification:** Decide the intended gate: `training.edit` for a supervisor-completes model, or a self-service rule (`trainee_id == current user`) plus `training.edit` for others.
- **Suggested acceptance criteria:** Given an authenticated user without `training.edit`, when they `POST /api/training-assignments/{id}/complete` for an assignment that is not their own, then they receive `403` and `completed` remains `false`.
- **Severity:** **High**
- **Responsible role:** Backend Lead.

---

**GAP-COMP-007 — No electronic signature on competency authorization or test-authorization grant**
- **Source reference:** `src/NT.QAMS.Domain/Competency/CompetencyRecord.cs:98-115` and `src/NT.QAMS.Domain/Competency/TestAuthorization.cs:37-62` — no `ESignatureService`, no `SignatureRecord`, no PIN/password check anywhere in the module. The keys `competencies.sign` and `test-authorizations.sign` exist (`PermissionCatalog.cs:116-121`, `:165`, `:167`) and are consumed by no call site.
- **Description:** Both modules are declared `SignedRecordLifecycle` in the permission catalogue, which grants a `Sign` action, but no code path performs or records a signature. Authorization is a bare `POST` with no re-authentication.
- **Impact:** For a laboratory operating under 21 CFR Part 11, the record that establishes who may perform a regulated test carries no signature manifestation (§11.50), no signature/record linkage (§11.70) and no two-component signing (§11.200). The control **does not conform** for this module as built. (Verdict stated per conventions §6.4.)
- **Testing limitation:** Every signature case in this module is `[GD]`. The e-signature codes `SIG-001`/`SIG-002`/`SIG-003` cannot be exercised on any competency endpoint.
- **Recommended clarification:** Confirm with the regulatory owner whether competency authorization is in the signed-record scope. If yes, specify the signing meaning text and whether the grant of a test authorization also requires one.
- **Suggested acceptance criteria:** Given an `Evaluated` competency, when a holder of `competencies.sign` authorizes it, then password + signature PIN are required, a `SignatureRecord` is written with `Meaning`, `SubjectRef` and `ContentHash`, the failure paths return `SIG-001`/`SIG-002`/`SIG-003`, and failed attempts are logged as `ESIGN_FAILED`.
- **Severity:** **Critical**
- **Responsible role:** Regulatory/CSV Lead with Solution Architect.

---

**GAP-COMP-008 — The pass mark is a compile-time constant, identical for every method and every tenant**
- **Source reference:** `src/NT.QAMS.Domain/Competency/CompetencyRecord.cs:33` — `public const int PassMark = 80;`. No `TenantSettings` entry, no configuration binding, no per-subject override was found.
- **Description:** A single hard-coded threshold governs competence for every subject, method, discipline and laboratory on the platform.
- **Impact:** Laboratories with method-specific acceptance criteria (a 100% pass requirement for a critical-safety procedure, a 70% threshold for a familiarisation module) cannot express them. Changing the threshold is a code change and therefore a revalidation event.
- **Testing limitation:** No configuration-driven pass-mark case can be authored. All BVA is fixed at 79/80/81 and cannot be parameterised.
- **Recommended clarification:** Ask the product owner whether per-subject or per-tenant pass marks are required for release, and whether historical records must retain the threshold in force when they were assessed.
- **Suggested acceptance criteria:** The pass mark is resolved from configuration at the point of assessment, the resolved value is persisted on the assessment row, and an assessment recorded under a previous threshold is not re-evaluated when the threshold changes.
- **Severity:** **Medium**
- **Responsible role:** Product Owner.

---

**GAP-COMP-009 — Assessment scores cannot be fractional**
- **Source reference:** `src/NT.QAMS.Domain/Competency/CompetencyRecord.cs:19` (`public int Score`); `src/NT.QAMS.Contracts/Resources/ResourceContracts.cs:64` (`ScoreAssessmentRequest(int Score)`); measured column `qams.assessment_result.score integer`.
- **Description:** The score is an integer end to end. A percentage such as 79.5 cannot be recorded; a client sending it fails at JSON binding rather than at a domain rule.
- **Impact:** Any assessment instrument that produces a weighted or fractional score must be rounded before entry, and the rounding is invisible in the record — an accuracy-of-record concern (Part 11 §11.10(a)).
- **Testing limitation:** The boundary set is the integer triple 79/80/81. A "79.99 is below the pass mark" case is unwritable as a domain case; only an input-binding case is possible, and its exact response was not measured in this pass (`[RNV]`).
- **Recommended clarification:** Confirm whether integer percentages satisfy the URS, or whether a `decimal(5,2)` is required. Note that changing the type is a migration plus a re-validation.
- **Suggested acceptance criteria:** Either (a) the URS explicitly states integer percentages and the API rejects non-integral scores with `400` and a documented code; or (b) the score is `decimal(5,2)` end to end, with a `CHECK (score >= 0 AND score <= 100)`.
- **Severity:** **Low**
- **Responsible role:** Product Owner with Quality Manager.

---

**GAP-COMP-010 — No database `CHECK` on `assessment_result.score`**
- **Source reference:** Measured constraint list for `qams.assessment_result` (2026-08-01) shows only `pk_assessment_result` and the tenant-composite FK. The `0..100` rule exists only at `CompetencyRecord.cs:84-87` (`COMP-011`).
- **Description:** Unlike the enum-domain `CHECK` constraints added for `status`/`scope` by `Hardening3_CheckDomains`, no range constraint protects the score column.
- **Impact:** Any writer that bypasses the aggregate — a data fix, a migration backfill, a future bulk import — can persist a score of 999 or −5, which the pass-gate comparison would then treat as a pass. The database is not self-defending for the single most decision-relevant number in the module.
- **Testing limitation:** A DB-level negative case (`INSERT … score = 999` must fail) cannot be written today; it must be authored `[GD]`.
- **Recommended clarification:** Confirm whether the schema-hardening programme intended to cover numeric ranges as well as enum domains (85 `CHECK` constraints exist system-wide per conventions §2).
- **Suggested acceptance criteria:** `ALTER TABLE qams.assessment_result ADD CONSTRAINT ck_assessment_result_score_range CHECK (score >= 0 AND score <= 100)` is present and validated; a direct insert outside the range is rejected by PostgreSQL.
- **Severity:** **Medium**
- **Responsible role:** Database Lead.

---

**GAP-COMP-011 — Duplicate-authorization prevention is application-only and racy**
- **Source reference:** `src/NT.QAMS.Application/Competency/AuthorizationSlice.cs:58-64` (`AnyAsync` then `Add`); measured index list for `qams.test_authorization` contains no unique index on `(tenant_id, user_id, test_catalog_item_id, scope)`.
- **Description:** `AUTHZ-005` is a read-then-write check with no database backstop, so two concurrent grants for the same person, test and scope can both pass the check and both commit. Separately, because the check counts `Suspended` as in force, a manually suspended authorization blocks a replacement grant while it remains suspended — the operator's only lawful routes are `Reinstate` (which fails with `AUTHZ-013` once the expiry has passed) or `Revoke` followed by a new grant. Neither the API nor the UI communicates this.
- **Impact:** Two contradictory authorization rows for the same cell of the matrix; the UI renders both chips, and it becomes ambiguous which one governs. The workflow trap can leave an operator convinced the system is broken when the remedy is an undocumented revoke-first step.
- **Testing limitation:** A concurrency case can demonstrate the duplicate but has no oracle for the intended outcome; it must be authored as an exploratory finding (`TC-COMP-EXPL-005`) rather than a pass/fail case. The workflow-trap case is `[ID]` — it documents behaviour with no requirement behind it.
- **Recommended clarification:** Confirm (a) whether a partial unique index on in-force rows is acceptable, and (b) whether `Suspended` should block a replacement grant at all, or whether a new grant should implicitly supersede a suspended one.
- **Suggested acceptance criteria:** A unique index `ux_test_authorization_inforce ON qams.test_authorization (tenant_id, user_id, test_catalog_item_id, scope) WHERE status IN ('Active','Suspended')` exists; N parallel identical grants yield exactly one row and N−1 documented conflict responses; and the `AUTHZ-005` message names the blocking authorization's id and status.
- **Severity:** **Medium**
- **Responsible role:** Database Lead with Backend Lead.

---

**GAP-COMP-012 — `test_authorization` has no foreign keys**
- **Source reference:** Measured `pg_constraint` for `qams.test_authorization` (2026-08-01): one PK and two `CHECK`s, **zero** `contype = 'f'`. Columns `user_id`, `test_catalog_item_id`, `competency_record_id` are plain `uuid NOT NULL`.
- **Description:** The evidencing link to `competency_record`, the subject link to `user_account` and the target link to `test_catalog_item` are all application-level conventions. Compare `assessment_result`, which does carry a tenant-composite CASCADE FK to its owner.
- **Impact:** Nothing structurally prevents an authorization pointing at a competency in another tenant, a deleted catalogue test, or a non-existent user. The tenant-composite FK pattern that the hardening programme adopted precisely to make cross-tenant children impossible is not applied here.
- **Testing limitation:** A referential-integrity case (`INSERT` an authorization with a bogus `competency_record_id` must fail) cannot pass. RLS still isolates reads, so the cross-tenant exposure is bounded — but the guarantee is one layer thinner than the architecture claims.
- **Recommended clarification:** Confirm whether the omission is deliberate (e.g. to allow an authorization to outlive a purged catalogue entry) or an oversight of `PersonnelAuthorizationMatrix`.
- **Suggested acceptance criteria:** `FOREIGN KEY (competency_record_id, tenant_id) REFERENCES qams.competency_record (id, tenant_id)` and `FOREIGN KEY (test_catalog_item_id, tenant_id) REFERENCES qams.test_catalog_item (id, tenant_id)` exist; inserting an authorization against another tenant's competency is rejected by PostgreSQL.
- **Severity:** **Medium**
- **Responsible role:** Database Lead.

---

**GAP-COMP-013 — `GET /api/test-authorizations` is unpaginated**
- **Source reference:** `src/NT.QAMS.Application/Competency/AuthorizationSlice.cs:133-134` returns `IQuery<IReadOnlyList<…>>`; handler `:154-162` ends in `ToListAsync` with no `Skip`/`Take`. Every sibling list query in this module uses `ToPagedAsync` (`CompetencySlice.cs:162`, `:215`).
- **Description:** The endpoint violates the API-004 pagination-envelope decision applied across the rest of the API (`src/NT.QAMS.Application/Abstractions/Paging.cs:6-21`, clamp `1..200`).
- **Impact:** A laboratory with 200 staff × 60 catalogue tests × 3 scopes can hold tens of thousands of rows; the endpoint returns all of them, plus an inner join, on every matrix page load and after **every** mutation (the facade re-fetches the whole list, `authorizations.facade.ts:52`). Memory and latency scale with tenant size and the client has no way to page.
- **Testing limitation:** No pagination case can be written for this endpoint; the performance case (`TC-COMP-PERF-001`) has no documented threshold to assert against.
- **Recommended clarification:** Confirm the intended matrix UX — server-side paging changes the matrix component materially, since it derives its rows and columns from the returned set.
- **Suggested acceptance criteria:** The endpoint returns a `PagedResponse<TestAuthorizationListItemDto>` honouring `page`/`pageSize` with the standard clamp; the matrix component consumes the envelope; and the API-surface snapshot is updated in the same commit.
- **Severity:** **Medium**
- **Responsible role:** Backend Lead with Frontend Lead.

---

**GAP-COMP-014 — List and detail disagree about a missing catalogue test**
- **Source reference:** `AuthorizationSlice.cs:155-157` uses an inner `Join` (row disappears); `:183` uses `test?.TestCode ?? "?"` (row survives, degraded).
- **Description:** If the joined `test_catalog_item` row is absent or invisible, the authorization vanishes from the register but is still retrievable by id.
- **Impact:** The matrix — the artefact an assessor reviews — can silently omit an authorization that is still in force. That is a direct instance of RSK-COMP-012.
- **Testing limitation:** Reproducing it requires making a catalogue row unreachable, which the API offers no route for (tests are deactivated, not deleted). The case is exploratory (`TC-COMP-EXPL-004`) with direct data setup.
- **Recommended clarification:** Confirm the intended behaviour — degrade like the detail query, or exclude deliberately. If excluded deliberately, the reason should be documented, because an ISO 17025 §6.2.6 record that silently hides rows is hard to defend at audit.
- **Suggested acceptance criteria:** Given an authorization whose catalogue test row is unavailable, when the register is listed, then the authorization is present with a clearly degraded test identifier, and list and detail agree.
- **Severity:** **Low**
- **Responsible role:** Backend Lead.

---

**GAP-COMP-015 — The UI offers a competency status "Expired" that does not exist**
- **Source reference:** `frontend/src/app/features/competency/competency-list.component.ts:94` lists `'Expired'`; `src/NT.QAMS.Domain/Competency/CompetencyRecord.cs:6` has no such member; the DB `CHECK` (`Hardening3_CheckDomains.cs:53`) forbids it.
- **Description:** Selecting "Expired" issues `GET /api/competencies?status=Expired`, which the handler translates to `x.Status.ToString() == "Expired"` (`CompetencySlice.cs:154`) — always false. The user sees an empty list indistinguishable from "no expired competencies".
- **Impact:** A quality manager checking "who has lapsed?" gets a confident, wrong answer of *none*. This is the highest-consequence UI defect in the module because the empty result looks like good news.
- **Testing limitation:** The case is straightforward to author but its expected result must record the *defect*, so it must be `[ID]`/`[GD]`, not a passing functional case.
- **Recommended clarification:** Decide how "lapsed" is meant to be surfaced, given that expiry returns a record to `PendingTraining`. Options: derive a virtual status from `ExpiresAt < today && Status == PendingTraining && ExpiresAt IS NOT NULL`, or add a real `Expired` state (schema + `CHECK` + migration).
- **Suggested acceptance criteria:** The status filter offers only values the API can return; and a distinct, correct filter exists for "previously authorized, now requiring requalification" that returns exactly the records whose `expires_at` is in the past and whose status is `PendingTraining`.
- **Severity:** **High**
- **Responsible role:** Frontend Lead with Product Owner.

---

**GAP-COMP-016 — Client and server field bounds disagree; `AssignTrainingCommand` has no validator**
- **Source reference:** Subject: client 200 (`competency-list.component.ts:101`, `training-queue.component.ts:97`) vs server 300 (`CompetencySlice.cs:22`) vs column `varchar(300)`. Revoke reason: client 500 (`competency-detail.component.ts:127`) vs server 1000 (`CompetencySlice.cs:51`). `AssignTrainingCommand` (`CompetencySlice.cs:102-104`) has **no** validator at all.
- **Description:** Three independent bound mismatches, plus one command whose only length bound is the database column — contrary to the standing rule that a free-text bound lives in the command validator when the column is widened (`CLAUDE.md` §5, *Column sizing*).
- **Impact:** Users are blocked at 200/500 characters by the browser for no server-side reason; an API client can exceed the client bound freely. A training subject longer than 300 characters submitted through the API reaches PostgreSQL and fails there — the resulting response was not measured in this pass.
- **Testing limitation:** BVA on these fields yields different answers through the UI and through the API, so every boundary case must state which surface it exercises. The >300-character training-subject case has no predicted response (`[RNV]`).
- **Recommended clarification:** Confirm the single authoritative bound per field and whether `training_assignment.subject` should be widened to `text` with a validator, matching the pattern used for the reason columns.
- **Suggested acceptance criteria:** For each of the four fields, the client validator, the FluentValidation rule and the column bound state the same limit; and `AssignTrainingCommand` has a validator asserting `Subject` NotEmpty with the agreed maximum.
- **Severity:** **Low**
- **Responsible role:** Frontend Lead with Backend Lead.

---

**GAP-COMP-017 — The pass mark is duplicated in the Angular template**
- **Source reference:** `frontend/src/app/features/competency/competency-detail.component.ts:50` — `[class.pass]="a.score >= 80" [class.fail]="a.score < 80"`; authoritative value at `CompetencyRecord.cs:33`.
- **Description:** The threshold that colours every historical attempt green or red is a second, uncoupled literal. Nothing keeps it in step with the domain constant, and the API never sends the threshold.
- **Impact:** If GAP-COMP-008 is ever resolved with a configurable pass mark, this template silently keeps showing the old boundary — assessments will be coloured "pass" that the domain treats as fail, or vice versa. That is a misleading regulated display.
- **Testing limitation:** No case can detect the drift today, because both values are 80. The case must assert the *coupling*, which is only possible once the API exposes the threshold.
- **Recommended clarification:** Confirm that the pass mark should be exposed on `CompetencyDetailDto` so the client stops guessing.
- **Suggested acceptance criteria:** `CompetencyDetailDto` carries the pass mark in force; the template compares against that value; and no numeric pass threshold appears as a literal anywhere under `frontend/src/app/features/competency/`.
- **Severity:** **Low**
- **Responsible role:** Frontend Lead.

---

**GAP-COMP-018 — Scoring and authorizing carry no reason for change**
- **Source reference:** `ScoreAssessmentRequest(int Score)` and the parameterless authorize action (`ResourceContracts.cs:64`; `CompetenciesController.cs:44-50`). `ChangeReasonMiddleware` applies only to `DELETE` (conventions §2) and this module has no DELETE, so `X-Change-Reason` is never demanded; `FieldChangeInterceptor` records `Reason = changeReason.Reason` (`FieldChangeInterceptor.cs:114`), which is null on these paths.
- **Description:** Two of the three decision-bearing acts in the competency lifecycle — recording an assessment result and authorizing a person — are performed with no captured justification. Only revoke and suspend carry one, in the request body.
- **Impact:** The `audit.field_change` rows for a scoring or an authorization have a null `Reason`. For a modification of a regulated record, Part 11 §11.10(e) expects the record to show *why*, and ISO 17025 §8.4 expects the same of quality records.
- **Testing limitation:** An audit-trail case cannot assert a non-null reason for these acts. Cases must assert `reason IS NULL` and be labelled `[ID]`.
- **Recommended clarification:** Confirm which competency acts require a documented reason. Scoring arguably carries its own justification (the score); authorizing arguably does not.
- **Suggested acceptance criteria:** For each act designated as requiring one, the reason is mandatory at the API, is rejected when blank, and appears on every `audit.field_change` row written in the same transaction.
- **Severity:** **Medium**
- **Responsible role:** Regulatory/CSV Lead.

---

**GAP-COMP-019 — Assessments and grants never reach the tamper-evident hash chain**
- **Source reference:** `CompetencyRecord.ScoreAssessment` (`:77-96`) and `TestAuthorization.Grant` (`:37-62`) raise no domain event. The hash chain is appended only from processed outbox events (`src/NT.QAMS.Infrastructure/Compliance/ComplianceLedgerServices.cs:29-66`). Compare `Authorize` (`:114`), `ExpireIfDue` (`:127`), `Revoke` (`:144`), which do raise events.
- **Description:** The two acts that *create* competence evidence and *create* an authorization are absent from `audit.audit_trail_entry`; only their mutation-level `audit.field_change` rows exist. Their reversals (`revoke`, `expire`) **are** chained. The trail is therefore asymmetric: an authorization can be revoked in the hash chain that was never granted in it.
- **Impact:** `GET /api/compliance/chain-verification` proves nothing about how a person came to be competent or authorized. `field_change` is append-only by trigger but is not hash-chained, so it is a weaker artefact.
- **Testing limitation:** A case asserting "the grant appears in the verified chain" cannot pass. Chain-coverage cases must enumerate which acts are chained and be labelled `[ID]`.
- **Recommended clarification:** Confirm which competency acts belong in the hash chain. Adding `AssessmentRecorded` and `TestAuthorizationGranted` events is a domain change with outbox and notification consequences.
- **Suggested acceptance criteria:** Recording an assessment and granting an authorization each produce an `audit.audit_trail_entry` row whose `EventType` names the act, and `chain-verification` remains valid across the added entries.
- **Severity:** **Medium**
- **Responsible role:** Regulatory/CSV Lead with Solution Architect.

---

**GAP-COMP-020 — The sweep reports candidates, not effected transitions, and omits authorization expiries**
- **Source reference:** `src/NT.QAMS.Infrastructure/Jobs/ScheduledSweepService.cs:151` returns `expiryCandidates.Count`; the equipment lockout and supplier passes correctly re-count post-transition (`:102`, `:126`). Log template `:154-156` names four counters, none of which is authorization expiries (computed at `:111-117`).
- **Description:** The competency counter reports the number of rows *selected*, not the number that actually transitioned; and authorization expiries are performed but never counted or logged.
- **Impact:** Operational monitoring of the sweep over-reports competency expiries whenever a candidate declines the proposal, and gives zero visibility into authorization expiries — the transition with the most direct effect on who may work. This weakens the OBS-003 "sweep hasn't run / did nothing" alerting story.
- **Testing limitation:** An observability case cannot assert a correct count. `TC-COMP-OBS-001` must assert the counter's *actual* semantics and be labelled `[ID]`.
- **Recommended clarification:** Confirm the intended counter semantics (candidates vs transitions) across all sweep passes, so the log line means one thing.
- **Suggested acceptance criteria:** Each sweep counter reports transitions actually applied; a fifth counter reports authorization expiries; and the log template names all of them.
- **Severity:** **Low**
- **Responsible role:** Backend Lead.

---

**GAP-COMP-021 — Training and competency are unlinked**
- **Source reference:** `TrainingAssignment` (`CompetencyRecord.cs:149-190`) holds `TraineeId`, `Subject`, `DocumentId`, `DueDate`, `Completed` — no `CompetencyRecordId`, no event on completion, no handler. `CompleteTrainingHandler` (`CompetencySlice.cs:121-131`) flips a flag and saves. The class comment itself says "manual (or future policy-driven)" (`:148`).
- **Description:** Completing training has no effect on any competency record. The only association is a free-text `Subject` string that both entities happen to carry, with no uniqueness, normalisation or matching.
- **Impact:** The training→assessment→authorization chain that ISO 17025 §6.2.2–§6.2.5 describes is not traceable in the data. One cannot answer "did this person complete the training before being assessed?" without matching strings by eye.
- **Testing limitation:** No integration case can assert that completing training advances a competency. The whole training slice is testable only in isolation.
- **Recommended clarification:** Confirm the intended relationship: does completing training move a competency out of `PendingTraining`, or merely evidence eligibility to be assessed?
- **Suggested acceptance criteria:** `TrainingAssignment` carries an optional `CompetencyRecordId`; the competency detail shows the training that preceded each assessment; and a report answers "assessed without completing the prerequisite training".
- **Severity:** **Medium**
- **Responsible role:** Product Owner.

---

**GAP-COMP-022 — Only one notification exists, and there is no advance expiry warning**
- **Source reference:** `src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:28`, `:39`, `:97-99`, seeded rule `:148-149`. No handler for `CompetencyAuthorized`, `CompetencyRevoked`, `TestAuthorizationExpired`, `TestAuthorizationRevoked`. The sweep acts only when `ExpiresAt <= today` (`ScheduledSweepService.cs:104-107`). The only "expiring soon" logic is client-side (`authorization-matrix.component.ts:187-193`).
- **Description:** `COMP_EXPIRED` fires **after** competence has already lapsed, to the Quality Manager and Tenant Administrator. The affected person is not notified. No warning precedes the lapse, and the revocation of a competency or an authorization notifies nobody.
- **Impact:** The first signal that a technologist is no longer authorized arrives on or after the day their authorization stops. A laboratory cannot schedule requalification proactively. Revocation — the most consequential act in the module — is silent.
- **Testing limitation:** Notification cases exist for exactly one event. Every other "Expected Notification" field in this module must read `n/a — no notification is defined (NotificationPolicies.cs:35-45)`. No lead-time case can be authored.
- **Recommended clarification:** Confirm the required notification set and lead times (e.g. 60/30/7 days), and whether the trainee and their department head are recipients.
- **Suggested acceptance criteria:** Configurable lead-time warnings dispatch before `expires_at`; `COMP_REVOKED` and `AUTHZ_REVOKED` rules exist; and the affected person is among the default recipients.
- **Severity:** **Medium**
- **Responsible role:** Product Owner with Quality Manager.

---

**GAP-COMP-023 — No database `CHECK` that `expires_on > granted_on`**
- **Source reference:** Measured `pg_constraint` for `qams.test_authorization` shows only the two enum-domain `CHECK`s. The rule lives at `TestAuthorization.cs:46-49` (`AUTHZ-001`).
- **Description:** The hardening programme added enum-domain constraints to this table but no date-ordering constraint, although the analogous `ck_training_completion_order` was added to `training_assignment`.
- **Impact:** A direct write can create an authorization that expired before it was granted; the sweep would then expire it on its next pass, producing a confusing lifecycle in the audit trail.
- **Testing limitation:** A DB-level negative case cannot pass; it must be `[GD]`.
- **Recommended clarification:** Confirm whether date-ordering constraints are in scope for the hardening programme generally (the pattern exists, so this looks like an omission rather than a decision).
- **Suggested acceptance criteria:** `CHECK (expires_on > granted_on)` exists and is validated; a direct insert violating it is rejected by PostgreSQL.
- **Severity:** **Low**
- **Responsible role:** Database Lead.

---

**GAP-COMP-024 — Competency and authorization records are physically mutable after authorization or revocation**
- **Source reference:** `qams.reject_frozen_mutation()` covers the 12 analytical study roots plus `uncertainty_budget` (conventions §2, `SignedRecordImmutability` migration). None of `competency_record`, `assessment_result`, `training_assignment`, `test_authorization` appears in the measured trigger set for this module.
- **Description:** Nothing at the database layer prevents an `UPDATE` of a `Revoked` competency's `revocation_reason`, or of an `Authorized` record's `expires_at`, or of an `assessment_result.score` after the fact. The aggregate refuses these through the API; the row does not refuse them at all.
- **Impact:** For a Part 11 §11.10(c)/(e) record, "protection of records" and "accurate and complete copies" rest solely on the application. The `field_change` ledger would record the change, so it is detectable — but not prevented, unlike the analytical studies.
- **Testing limitation:** An immutability case (`UPDATE` on a signed/terminal competency must be rejected by the trigger) cannot pass. Author it `[GD]`.
- **Recommended clarification:** Confirm whether personnel competence records were deliberately excluded from the frozen-mutation scope, or whether the scope was drawn around analytical studies only because those were the audit findings at the time.
- **Suggested acceptance criteria:** A BEFORE UPDATE/DELETE trigger rejects mutation of `competency_record` in `Authorized` or `Revoked`, of `assessment_result` unconditionally (attempts are append-only by design, `CompetencyRecord.cs:29`), and of `test_authorization` in `Revoked` or `Expired`; the transition **into** each frozen state remains allowed.
- **Severity:** **High**
- **Responsible role:** Database Lead with Regulatory/CSV Lead.

---

**GAP-COMP-025 — The revoke control is gated on the wrong permission in the UI**
- **Source reference:** `frontend/src/app/features/competency/competency-detail.component.ts:65` opens a single `@if (perms.can('competencies.approve'))` block that contains both the authorize button (`:66-68`) and the revoke form (`:69-75`); the server gates revoke on `competencies.void` (`CompetenciesController.cs:53`).
- **Description:** A user holding `competencies.approve` but not `competencies.void` is shown a revoke form that the server will refuse with `403 AUTHZ-403`. The converse — `void` without `approve` — hides the revoke form from someone entitled to use it.
- **Impact:** Because the seeded Department Head role holds neither key, the visible symptom today is limited to custom tenant roles; but the module is explicitly designed for tenant-defined roles, so custom roles are the expected case. The user-facing failure is a permission error on a control the UI offered.
- **Testing limitation:** The mismatch is only observable with a custom role holding exactly one of the two keys, which requires role setup through the Roles module before the case can run — state that in Preconditions.
- **Recommended clarification:** None needed on intent; confirm only whether the fix is a separate `@if` on `competencies.void` or a consolidation of the two keys for this module.
- **Suggested acceptance criteria:** The revoke form renders if and only if the user holds `competencies.void`; the authorize button renders if and only if the user holds `competencies.approve`; and a role holding exactly one key sees exactly the corresponding control.
- **Severity:** **Low**
- **Responsible role:** Frontend Lead.

---

**GAP-COMP-026 — Segregation of duties does not separate the preparer from the authorizer**
- **Source reference:** `CompetencyRecord.Authorize` (`:106-109`) checks only `actorId != TraineeId`. `AggregateRoot.EnsureSignerIsNotPreparer` (`src/NT.QAMS.SharedKernel/Primitives/AggregateRoot.cs:35-42`) exists, the `created_by_user_id` column is populated on `competency_record` (measured; added by `20260726192118_CreatedByUserIdForSoD`), and the method is **not called** from this aggregate. The same applies to `TestAuthorization.Grant` (`:41-44`), which checks only `grantedBy != userId`.
- **Description:** One person may create a competency record, record the assessment score, and authorize it — provided they are not the trainee. The platform's own preparer-vs-signer mechanism is available, wired to a populated column, and unused here, while it guards 14 analytical sign-offs with `SOD-AQ-001`.
- **Impact:** The four-eyes principle behind Part 11 §11.10(g) and ISO 17025 §6.2.6 is only half-enforced. A single assessor can manufacture a competent operator end to end. This is RSK-COMP-003 largely unmitigated.
- **Testing limitation:** A "creator cannot authorize" case cannot pass. It must be authored `[GD]`. Note that even if added, the guard is a documented no-op when the preparer is unknown (accepted residual F-05b), so legacy records would be unaffected — which the acceptance criteria must acknowledge.
- **Recommended clarification:** Confirm with the quality owner whether creator ≠ authorizer is required, and whether assessor ≠ authorizer is *also* required (a stricter and arguably more meaningful rule, since the assessor is the one making the technical judgement).
- **Suggested acceptance criteria:** `CompetencyRecord.Authorize` calls `EnsureSignerIsNotPreparer(actorId, "SOD-COMP-002")`; where the record has a known creator, that person's authorize attempt returns `422 SOD-COMP-002`; and where the creator is unknown the call is a documented no-op consistent with residual F-05b.
- **Severity:** **High**
- **Responsible role:** Quality Manager with Solution Architect.

---

### Gap summary

| Severity | Count | Ids |
|---|---|---|
| Critical | 1 | 007 |
| High | 7 | 001, 004, 005, 006, 015, 024, 026 |
| Medium | 11 | 002, 003, 008, 010, 011, 012, 013, 018, 019, 021, 022 |
| Low | 7 | 009, 014, 016, 017, 020, 023, 025 |
| **Total** | **26** | |

**Compliance verdicts for this module** (permitted vocabulary only, conventions §6.4):

| Control | Verdict | Basis |
|---|---|---|
| Part 11 §11.50/§11.70/§11.200 — signature on the competency authorization | **Does not conform** | GAP-COMP-007: no signature path exists |
| Part 11 §11.10(g) — authority checks / segregation of duties | **Partially conforms** | Trainee-exclusion enforced (`CompetencyRecord.cs:89-92`, `:106-109`, `TestAuthorization.cs:41-44`); preparer/approver separation absent (GAP-COMP-026); read surface ungated (GAP-COMP-005) |
| Part 11 §11.10(e) — audit trail with reason for change | **Partially conforms** | All mutations produce `audit.field_change` rows (`FieldChangeInterceptor.cs:22-118`); reasons absent on score/authorize (GAP-COMP-018); grant and assessment absent from the hash chain (GAP-COMP-019) |
| Part 11 §11.10(c) — protection of records | **Partially conforms** | RLS forced on all four tables (measured); no frozen-mutation trigger (GAP-COMP-024) |
| ISO 17025 §6.2.6 — authorization of personnel | **Partially conforms** | Scope-level authorization, evidence gate and expiry inheritance are implemented (`AuthorizationSlice.cs:44-68`); no signature, no advance warning, matrix can omit rows (GAP-COMP-007, -014, -022) |
| ISO 17025 §6.2.2–§6.2.5 — training linked to competence | **Cannot be assessed** | The two aggregates are unlinked (GAP-COMP-021); there is no data relationship to assess |
