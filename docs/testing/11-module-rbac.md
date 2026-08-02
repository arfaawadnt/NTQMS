# 11 — Roles, Permission Catalog, Privilege Matrix, Data-Scoped Access, Segregation of Duties

**Module code:** `RBAC` · **System under test:** NT.QMS **v1.51.2** (repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master`) · **Inspection date:** 2026-08-01

**Binding conventions:** `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md`. That file governs the 28-field case format (§4), the canonical detailed-case block (§8), the evidence labels `[IV]` / `[RNV]` / `[ID]` / `[GD]`, the `TC-<MODULE>-<KIND>-<NNN>` id convention (§5), and the honesty rules (§6). Where this file records a fact that contradicts the conventions file, §0 below gives the file:line proof and this file wins for the RBAC module only.

**This file is FRONT MATTER ONLY.** Per conventions §7 (split convention) it contains no `## 5. Detailed test cases` section. Detailed cases are authored into `11-module-rbac-cases-<letter>.md` by separate passes against the id blocks reserved below.

---

## ID reservation table

Ranges are reserved generously; a case file consumes ids from its block in order and never renumbers. A reserved range with no matching case file is a **coverage hole**, not a delivered case (conventions §7).

| Batch file | Slice of scope | Reserved id range | Reserved count |
|---|---|---|---|
| `11-module-rbac-cases-A.md` | `PermissionCatalog` + `Role` aggregate domain units: key format, catalogue closure, `ROLE-001`…`ROLE-005`, event emission, system-role protections, idempotent activate/deactivate | `TC-RBAC-UNIT-001` … `TC-RBAC-UNIT-060` | 60 |
| `11-module-rbac-cases-A.md` | Role lifecycle state machine (Active/Inactive × System/Tenant-defined) | `TC-RBAC-STATE-001` … `TC-RBAC-STATE-020` | 20 |
| `11-module-rbac-cases-B.md` | HTTP gate allow/deny per `[RequirePermission]` call site — administration, people, documents, operations groups | `TC-RBAC-API-001` … `TC-RBAC-API-070` | 70 |
| `11-module-rbac-cases-C.md` | HTTP gate allow/deny per `[RequirePermission]` call site — quality, risk, resources, analytical groups; class-level ∧ method-level composition | `TC-RBAC-API-071` … `TC-RBAC-API-140` | 70 |
| `11-module-rbac-cases-C.md` | Decision-table derived cases over the privilege matrix (DT-1…DT-6 in §4) | `TC-RBAC-DT-001` … `TC-RBAC-DT-040` | 40 |
| `11-module-rbac-cases-D.md` | Per-request privilege resolution, immediate grant/revoke, inactive-role blackout, platform-admin bypass, `AuthorizationBehavior` policy dispatch | `TC-RBAC-INT-001` … `TC-RBAC-INT-035` | 35 |
| `11-module-rbac-cases-D.md` | Tenant isolation on `qams.role`, `qams.role_permission`, `qams.user_branch_access`, `qams.user_department_access` | `TC-RBAC-RLS-001` … `TC-RBAC-RLS-020` | 20 |
| `11-module-rbac-cases-D.md` | Branch/department working-scope data flow (query filter ∧ `OrgScopeGuardInterceptor`) | `TC-RBAC-DF-001` … `TC-RBAC-DF-015` | 15 |
| `11-module-rbac-cases-E.md` | Segregation of duties — all 10 implemented `SOD-*` codes, `EnsureSignerIsNotPreparer`, `AUTHZ-*` mapping | `TC-RBAC-SEC-001` … `TC-RBAC-SEC-045` | 45 |
| `11-module-rbac-cases-E.md` | `ManageRolesLockoutGuard` (`ROLE-006`) boundary and bypass paths | `TC-RBAC-BVA-001` … `TC-RBAC-BVA-012` | 12 |
| `11-module-rbac-cases-F.md` | Frontend: `PermissionsService`, `platformOnlyGuard` / `tenantOnlyGuard`, privilege-driven affordances | `TC-RBAC-COMP-001` … `TC-RBAC-COMP-025` | 25 |
| `11-module-rbac-cases-F.md` | End-to-end privilege journeys (Playwright) | `TC-RBAC-E2E-001` … `TC-RBAC-E2E-015` | 15 |
| `11-module-rbac-cases-F.md` | Accessibility of the privilege-matrix screen | `TC-RBAC-A11Y-001` … `TC-RBAC-A11Y-006` | 6 |
| **this file, §6** | UAT scenarios (Gherkin) | `TC-RBAC-UAT-001` … `TC-RBAC-UAT-008` | 8 (**consumed here**) |
| **this file, §7** | Exploratory charters | `TC-RBAC-EXPL-001` … `TC-RBAC-EXPL-006` | 6 (**consumed here**) |

**Total reserved:** 428 ids across 13 KIND blocks; 14 consumed in this file, 414 reserved for batches A–F.

### Completeness statement

**Complete in this file:** the correction to ground truth (§0); the implementation inventory (§1) covering every aggregate, invariant, domain error code, domain event, endpoint gate, permission key, table and state in scope, each with `file:line`; the brief-vs-code divergence table (§2); the role lifecycle state-transition matrices (§3); the **complete 31-module / 170-key privilege inventory**, the endpoint→permission→tier→SoD matrix, the brief-code translation table and the SoD rule table (§4); 8 UAT scenarios (§6); 6 exploratory charters (§7); 17 gaps in the 9-field format (§8).

**Deferred to the case batches:** every detailed test case. **Not attempted in this pass:** exhaustive per-cell enumeration of the 170 keys × 5 seeded roles = 850 grant assertions (the seeded sets are given as rules + counts in §4.3, and the parity pins already live in `tests/NT.QAMS.Application.UnitTests/Authorization/SystemRoleCatalogTests.cs`); the 144 endpoint gates are listed by permission key and aggregated by route family in §4.2 rather than one row per call site — batches B and C consume the per-call-site detail. **Not executed:** nothing in this package is executed; every `Result` in the case batches is `Not Run` (conventions §6.5).

---

## 0. Correction to ground truth

One factual error found in `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md`.

| Conventions file says | Measured as-built | Proof |
|---|---|---|
| §2, *Authorization / privileges*, line 61: “**30 permission modules** in 7 groups” | **31 permission modules in 8 groups**, yielding **170 permission keys** | `src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:133-186` — the `Modules` collection literal has 31 `new(...)` entries; the group constants at `PermissionCatalog.cs:68-75` are eight (`quality`, `documents`, `risk`, `resources`, `people`, `analytical`, `operations`, `administration`). The conventions file's own inline list at line 61 enumerates **31** module keys and **8** parenthesised group names, so the “30 … in 7” figure contradicts the list printed beside it. |

Corroboration outside the code: `docs/validation/06-Revalidation-Delta-v1.38-v1.50.md:154` (URS-095) states “*31 modules × 8 actions = 170 keys*” — the validation record and the source agree; the conventions file is the outlier.

**Confirmations (no correction needed), recorded so the case authors do not re-measure:**

- “**144 call sites**” of `[RequirePermission]` (conventions §2, line 65) is **correct**. A naive `grep -c "\[RequirePermission("` over `src/NT.QAMS.WebApi/` returns 145; the 145th is the XML-doc example inside `src/NT.QAMS.WebApi/Authorization/RequirePermissionAttribute.cs:11`, not a call site. Counted per controller, the 37 controllers carrying the attribute sum to exactly **144**.
- “**exactly one** `[Authorize(Roles=…)]` remains, guarding the platform (non-tenant) surface” is **correct**: `src/NT.QAMS.WebApi/Controllers/TenantsController.cs:12` — `[Microsoft.AspNetCore.Authorization.Authorize(Roles = Roles.PlatformAdmin)]`. It is the only usage of any `NT.QAMS.WebApi.Authorization.Roles` constant anywhere in `src/`.
- “`PermissionAction` values *seen in use*: View, Create, Edit, Approve, Void, Sign, Export, Manage” — this is the **complete closed enum**, all eight members, `PermissionCatalog.cs:9-34`. There are no unused or additional actions.

---

## 1. Implementation inventory

### 1.1 Aggregates and owned entities

| Type | File:line | Kind | Notes |
|---|---|---|---|
| `Role` | `src/NT.QAMS.Domain/Authorization/Role.cs:34` | `AggregateRoot`, `ITenantScoped` | Tenant-defined named privilege set. Properties: `TenantId`, `Name`, `NormalizedName`, `Description?`, `IsSystem`, `DefaultLanguage?`, `IsActive`, `Permissions` (owned collection), computed `PermissionKeys` (`Role.cs:44-72`). |
| `RolePermission` | `src/NT.QAMS.Domain/Authorization/Role.cs:7` | Owned by `Role` | Single property `PermissionKey`; `internal` constructor — only the aggregate may mint one (`Role.cs:11`). |
| `UserAccount` | `src/NT.QAMS.Domain/IdentityAccess/UserAccount.cs:27` | `AggregateRoot`, `IOptionallyTenantScoped` | Carries `RoleId?` (`:68`), `PreferredLanguage?` (`:74`), and the owned scope collections `BranchAccess` (`:81`) / `DepartmentAccess` (`:84`). Deliberately **outside RLS** (accepted deviation B9, documented at `UserAccount.cs:20-26`). |
| `UserBranchAccess` | `src/NT.QAMS.Domain/IdentityAccess/UserAccount.cs:262` | Owned by `UserAccount` | Single property `BranchId`, `internal` ctor. |
| `UserDepartmentAccess` | `src/NT.QAMS.Domain/IdentityAccess/UserAccount.cs:272` | Owned by `UserAccount` | Single property `DepartmentId`, `internal` ctor. |
| `PermissionModule` | `src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:45` | Immutable record (not persisted) | `(Key, Group, NameKey, Actions)`. Catalogue metadata only — it is **code**, never a table. |

### 1.2 Invariants

| # | Invariant | Enforced at | Code |
|---|---|---|---|
| INV-1 | A role name is required and is ≤ 80 characters, trimmed. | `Role.Create` / `Role.Rename` — `Role.cs:86-94`, `:119-123` | `ROLE-001`, `ROLE-002` |
| INV-2 | **A grant must map to a real capability.** Unknown keys are rejected on save, never stored. | `Role.ReplacePermissions` — `Role.cs:196-201`, via `PermissionCatalog.IsKnown` (`PermissionCatalog.cs:198`) | `ROLE-005` |
| INV-3 | A system role cannot be renamed. | `Role.Rename` — `Role.cs:114-117` | `ROLE-003` |
| INV-4 | A system role cannot be deactivated. | `Role.Deactivate` — `Role.cs:158-161` | `ROLE-004` |
| INV-5 | Grants are normalised before storage: trimmed, lower-cased, de-duplicated, blanks dropped. | `Role.ReplacePermissions` — `Role.cs:190-194` | — (silent normalisation) |
| INV-6 | A no-op permission change raises no event. | `Role.SetPermissions` — `Role.cs:142-145` | — |
| INV-7 | Role name is unique per tenant, case-insensitively (checked in the handler **and** by a unique index). | `CreateRoleHandler` `RolesSlice.cs:86-90`; `UpdateRoleHandler` `RolesSlice.cs:122-126`; index `AuthorizationConfigurations.cs:20` | `ROLE-007` |
| INV-8 | **Cross-aggregate:** after any change, at least one *active* user must still hold an *active* role granting `roles.manage`. | `ManageRolesLockoutGuard.EnsureSurvivesAsync` — `src/NT.QAMS.Application/Authorization/RolesSlice.cs:28-52` | `ROLE-006` |
| INV-9 | An inactive role cannot be assigned to a user. | `AssignUserRoleHandler` — `UserManagement.cs:212-216`; also `UserManagement.cs:68-72` at registration | `ROLE-008` |
| INV-10 | An inactive role **grants nothing** at resolution time — deactivation stops existing holders, not merely new assignments. | `PrivilegeResolver.ResolveAsync` — `src/NT.QAMS.Infrastructure/Authorization/PrivilegeResolution.cs:88-98` (`r.IsActive` in the predicate) | — (silent zero-grant) |
| INV-11 | Every command must declare exactly one `CommandPolicyAttribute`; an unannotated command is denied. | `AuthorizationBehavior` — `src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs:49-56`; CI gate `tests/NT.QAMS.Architecture.Tests/CommandPolicyTests.cs:26-38` | `AUTHZ-000` |
| INV-12 | A `[RequirePermissionPolicy]` naming a key the catalogue does not know **fails loudly on every call** rather than denying quietly. | `AuthorizationBehavior.cs:63-70` | `AUTHZ-008` |
| INV-13 | Unresolved privileges deny: `RequestPrivileges` starts with no permissions and `IsResolved=false`. | `src/NT.QAMS.Infrastructure/Authorization/PrivilegeResolution.cs:12-39` | — (fail-closed default) |
| INV-14 | A user's working scope must point at org units that exist in the tenant. | `SetUserScopeHandler` — `UserManagement.cs:262-281` | `SCOPE-003`, `SCOPE-004` |
| INV-15 | A branch/department-restricted actor cannot **create or re-point** an `IAllocatable` row outside their scope. | `OrgScopeGuardInterceptor.Guard` — `src/NT.QAMS.Infrastructure/Persistence/Interceptors/OrgScopeGuardInterceptor.cs:39-67` | `SCOPE-001`, `SCOPE-002` |
| INV-16 | A branch/department-restricted actor cannot **read** out-of-scope `IAllocatable` rows; null-attributed rows stay visible. | Composed EF global query filter `ApplyTenantAndScopeFilter` — `src/NT.QAMS.Infrastructure/Persistence/AppDbContext.cs:200-211` | — (invisible, not refused) |
| INV-17 | A tenant user cannot be made a platform administrator. | `UserAccount.ChangeRole` — `UserAccount.cs:125-133` | `USER-005` |
| INV-18 | A role id is required when assigning. | `UserAccount.AssignRole` — `UserAccount.cs:166-174` | `USER-010` |

### 1.3 Domain error codes — exhaustive for this module

Measured by exhaustive grep over `src/` for the literal code strings. **`ROLE-*` (9 distinct), `SCOPE-*` (4), `AUTHZ-*` (11), `SOD-*` (10)** plus the two HTTP-layer constants. Nothing else in scope emits a code.

#### 1.3.1 `ROLE-*`

| Code | Meaning | Raised at | HTTP |
|---|---|---|---|
| `ROLE-001` | A role name is required. | `Role.cs:88` (create), `Role.cs:122` (rename) | 422 |
| `ROLE-002` | A role name may not exceed 80 characters. | `Role.cs:93` | 422 |
| `ROLE-003` | A system role cannot be renamed. | `Role.cs:116` | 422 |
| `ROLE-004` | A system role cannot be deactivated. | `Role.cs:160` | 422 |
| `ROLE-005` | Unknown permission key(s) — a privilege must map to a real capability. | `Role.cs:199` | 422 |
| `ROLE-006` | This change would leave no active user able to manage roles and privileges. | `RolesSlice.cs:48` | 422 |
| `ROLE-007` | A role named '…' already exists. | `RolesSlice.cs:89` (create), `RolesSlice.cs:125` (update) | 422 |
| `ROLE-008` | An inactive role cannot be assigned. | `UserManagement.cs:214` (assign), `UserManagement.cs:71` (register) | 422 |
| `ROLE-009` | The seeded role '…' is not available in this workspace; assign a role explicitly. | `SeededRoleDefault.cs:25` | 422 |
| `ROLE-404` | Role not found. | `RolesSlice.cs:60`, `RolesSlice.cs:276`, `UserManagement.cs:68`, `UserManagement.cs:211` | **404** (suffix rule, `DomainExceptionHandler.cs:69-74`) |

#### 1.3.2 `SCOPE-*`

| Code | Meaning | Raised at | HTTP |
|---|---|---|---|
| `SCOPE-001` | You are not permitted to work in the selected branch. | `OrgScopeGuardInterceptor.cs:56` | 422 |
| `SCOPE-002` | You are not permitted to work in the selected department. | `OrgScopeGuardInterceptor.cs:63` | 422 |
| `SCOPE-003` | One or more selected branches do not exist. | `UserManagement.cs:268` | 422 |
| `SCOPE-004` | One or more selected departments do not exist. | `UserManagement.cs:278` | 422 |

#### 1.3.3 `AUTHZ-*` — two disjoint families sharing a prefix

**Family 1 — authorization refusals (the RBAC family).** `DomainExceptionHandler.cs:63-68` maps *any* `AUTHZ-`-prefixed `DomainException` to **403 Forbidden** with `application/problem+json`.

| Code | Meaning | Raised at |
|---|---|---|
| `AUTHZ-000` | Command declares no authorization policy — denied (fail closed). | `AuthorizationBehavior.cs:52` |
| `AUTHZ-001` | An authenticated actor is required for this action. | `AuthorizationBehavior.cs:60`; also `UserManagement.cs:324` |
| `AUTHZ-002` | Role '…' is not permitted to execute this action. | `AuthorizationBehavior.cs:83` |
| `AUTHZ-008` | Command requires unknown permission '…' (catalogue drift). | `AuthorizationBehavior.cs:68` |
| `AUTHZ-403` | HTTP-layer refusal — “You do not have permission to perform this action.” | Constant at `ProblemAuthorizationResultHandler.cs:16`; written by `RequirePermissionAttribute.OnAuthorizationAsync` (`RequirePermissionAttribute.cs:54-60`) **and** by the framework result handler (`ProblemAuthorizationResultHandler.cs:27-32`) — deliberately the same code from both paths. |

**Family 2 — the Test-Authorization aggregate (`test-authorizations` module), which reuses the same prefix for business rules.** These are *not* RBAC refusals but they land on 403 because of the prefix rule. Recorded here so a case author does not mis-attribute them.

| Code | Meaning | Raised at |
|---|---|---|
| `AUTHZ-001` | The authorization expiry must fall after the grant date. | `src/NT.QAMS.Domain/Competency/TestAuthorization.cs:48` |
| `AUTHZ-002` / `AUTHZ-003` / `AUTHZ-004` / `AUTHZ-005` | Slice-level guards on granting a test authorization. | `src/NT.QAMS.Application/Competency/AuthorizationSlice.cs:41,50,55,63` |
| `AUTHZ-010` … `AUTHZ-015` | Test-authorization state guards (suspend/reinstate/revoke). | `TestAuthorization.cs:68,73,93,98,109,114` |
| `AUTHZ-404` | Test authorization not found. | `AuthorizationSlice.cs:128,175` — 404 by the suffix rule, which **wins** over the prefix rule only because the prefix arm is evaluated first; see GAP-RBAC-011. |

> **Collision, verified:** `AUTHZ-001` is used with two different meanings — “an authenticated actor is required” (`AuthorizationBehavior.cs:60`) and “the authorization expiry must fall after the grant date” (`TestAuthorization.cs:48`). Both surface as 403. → **GAP-RBAC-010**.

**Authentication codes are deliberately separate:** `DomainExceptionHandler.cs:54-59` matches the exact prefix `AUTH-` (with hyphen) for 401, and the comment at `:51-53` states the `AUTHZ-*` codes must not masquerade as 401s. `ProblemAuthorizationResultHandler.ChallengedCode = "AUTH-401"` (`:19`).

#### 1.3.4 `SOD-*` — every implemented segregation-of-duties code

| Code | Rule | Raised at | Trigger |
|---|---|---|---|
| `SOD-AQ-001` | The preparer of an analytical record cannot sign it off. | 14 sites: `CarryoverStudy.cs:148`, `DetectionLimitStudy.cs:210`, `InstrumentComparabilityStudy.cs:195`, `InterferenceStudy.cs:169`, `LinearityStudy.cs:187`, `LotComparisonStudy.cs:148`, `MethodComparisonStudy.cs:190`, `OutlierScreening.cs:170`, `PrecisionStudy.cs:200`, `PtPlan.cs:108`, `ReferenceIntervalStudy.cs:153`, `SigmaAssessment.cs:98`, `UncertaintyBudget.cs:166`, `ValidationStudy.cs:145` — all via `AggregateRoot.EnsureSignerIsNotPreparer` | `signerId == CreatedByUserId` |
| `SOD-QP-001` | The approver of the quality policy cannot be its author. | `src/NT.QAMS.Domain/Improvement/QualityPolicy.cs:78` (via `EnsureSignerIsNotPreparer`) | `approverId == CreatedByUserId` |
| `SOD-CAPA-001` | The raiser cannot **close** their own nonconformance. | `src/NT.QAMS.Domain/Improvement/Nonconformance.cs:268` | `actorId == RaisedBy` on `ConfirmEffectiveness(effective: true, …)` |
| `SOD-CAPA-002` | The raiser cannot **verify** their own nonconformance. | `src/NT.QAMS.Domain/Improvement/Nonconformance.cs:245` | `actorId == RaisedBy` on `Verify(…)` |
| `SOD-DOC-001` | The author cannot **review** their own document. | `src/NT.QAMS.Domain/DocumentControl/ControlledDocument.cs:122` | `actorId == version.AuthorId` on `Recommend` |
| `SOD-DOC-002` | The author cannot **approve/publish** their own document. | `src/NT.QAMS.Domain/DocumentControl/ControlledDocument.cs:156` (aggregate) **and** `src/NT.QAMS.Application/DocumentControl/Commands/DocumentCommands.cs:145` (pre-check before the e-signature ceremony, so a failing publish never burns a signature attempt) | `actor == version.AuthorId` on `Publish` |
| `SOD-COMP-001` | A trainee cannot assess **or** authorize their own competency. | `src/NT.QAMS.Domain/Competency/CompetencyRecord.cs:91` (assess), `:108` (authorize) | `assessorId == TraineeId` / `actorId == TraineeId` |
| `SOD-AUTHZ-001` | Users cannot grant their own test authorizations. | `src/NT.QAMS.Domain/Competency/TestAuthorization.cs:43` | `grantedBy == userId` |
| `SOD-COI-001` | Declarants cannot assess their own conflict of interest. | `src/NT.QAMS.Domain/RiskGovernance/ConflictDeclaration.cs:72` | `assessorId == DeclarantId` |
| `SOD-SUP-001` | The registrant cannot approve their own supplier. | `src/NT.QAMS.Domain/SupplierQuality/Supplier.cs:91` | `actorId == RegisteredBy` |

All ten map to **HTTP 422** with the domain code (the default `DomainException` arm, `DomainExceptionHandler.cs:75-80`) — they are **not** `AUTHZ-*` and are **not** 403.

**The shared helper:**
```csharp
protected void EnsureSignerIsNotPreparer(Guid signerId, string code)
{
    if (CreatedByUserId is { } preparer && preparer == signerId)
    { throw new DomainException(code, "Segregation of duties: the preparer of a record cannot sign it off."); }
}
```
`src/NT.QAMS.SharedKernel/Primitives/AggregateRoot.cs:36-42`. **It is a no-op when `CreatedByUserId` is null** — the accepted residual F-05b. → **GAP-RBAC-009**.

### 1.4 Domain events

| Event | Payload | Raised at |
|---|---|---|
| `RoleCreated` | `(RoleId, Name, IsSystem)` | `Role.cs:214`; raised in `Role.Create` (`Role.cs:107`) |
| `RoleRenamed` | `(RoleId, Name)` | `Role.cs:217`; raised in `Role.Rename` (`Role.cs:128`) |
| `RolePermissionsChanged` | `(RoleId, Name, Granted[], Revoked[], Reason)` | `Role.cs:220-225`; raised in `Role.SetPermissions` (`Role.cs:149`) — **carries the operator's reason**, the Part-11 §11.10(e) hook for privilege change |
| `RoleDeactivated` | `(RoleId, Name)` | `Role.cs:228`; raised at `Role.cs:169` |
| `RoleReactivated` | `(RoleId, Name)` | `Role.cs:231`; raised at `Role.cs:181` |
| `UserRoleAssigned` | `(UserId, RoleId)` | `src/NT.QAMS.Domain/IdentityAccess/UserAccount.cs:282`; raised at `UserAccount.cs:173` |
| `UserScopeChanged` | `(UserId, BranchIds[], DepartmentIds[], IsUnrestricted)` | `UserAccount.cs:290-294`; raised at `UserAccount.cs:196`. `IsUnrestricted` records the widest case explicitly rather than leaving it inferred from an empty list. |

`RoleCreated`, `RoleDeactivated` and `RoleReactivated` carry **no reason field**; only `RolePermissionsChanged` does. → **GAP-RBAC-014**.

Per URS-099 (`docs/validation/06-Revalidation-Delta-v1.38-v1.50.md:158`) these flow domain-event → outbox → hash-chained `audit.audit_trail`; the v1.51.1 defect RP-D1 (user-account access-control events losing their tenant stamp) is recorded as closed there, pinned by `tests/NT.QAMS.Application.UnitTests/Authorization/UserEventTenantStampTests.cs`.

### 1.5 Endpoints

**`RolesController`** — `src/NT.QAMS.WebApi/Controllers/RolesController.cs`, `[Route("api/roles")]` + `[Authorize]` at class level (`:18-19`). Every route is dual-exposed as `/api/roles/...` and `/api/v{version}/roles/...` (conventions §1, `Asp.Versioning.Mvc`).

| Verb | Route | Gate | Sends | Line |
|---|---|---|---|---|
| GET | `/api/roles/catalog` | `roles.view` | `GetPermissionCatalogQuery` | `:23-26` |
| GET | `/api/roles` | `roles.view` | `GetRolesQuery` | `:28-31` |
| GET | `/api/roles/{id:guid}` | `roles.view` | `GetRoleQuery` | `:33-36` |
| POST | `/api/roles` | `roles.manage` | `CreateRoleCommand` → `200 { id }` | `:38-45` |
| PUT | `/api/roles/{id:guid}` | `roles.manage` | `UpdateRoleCommand` → `204` | `:47-53` |
| PUT | `/api/roles/{id:guid}/permissions` | `roles.manage` | `SetRolePermissionsCommand` → `204` | `:55-61` |
| POST | `/api/roles/{id:guid}/deactivate` | `roles.manage` | `SetRoleActiveCommand(Active:false)` → `204` | `:63-69` |
| POST | `/api/roles/{id:guid}/reactivate` | `roles.manage` | `SetRoleActiveCommand(Active:true)` → `204` | `:71-77` |

**There is no DELETE.** A role is deactivated, never removed. → **GAP-RBAC-015**.

**Adjacent RBAC-relevant endpoints outside `RolesController`:**

| Verb | Route | Gate | File:line |
|---|---|---|---|
| GET | `/api/auth/me/privileges` | `[Authorize]` only — **no permission gate** | `src/NT.QAMS.WebApi/Controllers/AuthController.cs:147-150` |
| PUT | `/api/auth/me/language` | `[Authorize]` only; command policy `[RequireAuthenticatedActor]` | `AuthController.cs:153-159`; `UserManagement.cs:310` |
| GET | `/api/users` | `users.view` | `UsersController.cs:23` |
| POST | `/api/users` | `users.manage` | `UsersController.cs:28` |
| POST | `/api/users/{id}/role` | `users.manage` | `UsersController.cs:34` — **tier** change (`ChangeUserRoleCommand`) |
| PUT | `/api/users/{id}/assigned-role` | `users.manage` | `UsersController.cs:43` — **configurable role** change (`AssignUserRoleCommand`) |
| PUT | `/api/users/{id}/scope` | `users.manage` | `UsersController.cs:52` — `SetUserScopeCommand` |
| PUT | `/api/users/{id}/language` | `users.manage` | `UsersController.cs:61` |
| POST | `/api/users/{id}/deactivate` \| `/reactivate` \| `/reset-password` | `users.manage` | `UsersController.cs:69,77,85` |
| GET | `/api/tenants` (and the rest of `TenantsController`) | `[Authorize(Roles = Roles.PlatformAdmin)]` — the **one** surviving tier gate | `TenantsController.cs:12` |

**Aggregate endpoint-gate count: 144 `[RequirePermission]` call sites across 37 controllers.** Three are **class-level** (they gate every action on the controller):

| Controller | Class-level gate | Line |
|---|---|---|
| `AccessReviewsController` | `access-reviews.view` | `:20` |
| `ComplianceController` | `compliance.view` | `:20` |
| `TenantSettingsController` | `tenant-settings.manage` | `:18` |

`RequirePermissionAttribute` is `AttributeUsage(… AllowMultiple = true)` (`RequirePermissionAttribute.cs:26`) and each instance is an independent `IAsyncAuthorizationFilter`, so a class-level gate and a method-level gate **compose as AND**. Concretely: `POST /api/compliance/audit-trail-reviews` requires **both** `compliance.view` **and** `compliance.create` (`ComplianceController.cs:20` ∧ `:46`). → recorded as DT-5 in §4 and **GAP-RBAC-011**.

### 1.6 Permission keys

The complete inventory is §4.1 (31 modules, 170 keys). Summary:

- Keys are `{module}.{action}`, lower-case, built by `PermissionCatalog.Key` (`PermissionCatalog.cs:194-195`), persisted verbatim, `varchar(60)` (`AuthorizationConfigurations.cs:30`).
- `PermissionCatalog.AllKeys` is an `Ordinal` `HashSet` computed once from `Modules` (`PermissionCatalog.cs:189-191`) — **170 entries**.
- `PermissionCatalog.ManageRoles` is the pinned literal `roles.manage` (`PermissionCatalog.cs:205`), held apart because it is the one permission that can lock every administrator out.
- **78 of the 170 keys appear at an HTTP `[RequirePermission]` gate. 92 do not.** The unreached 92 are listed in §4.1 (marked `*`). This is not automatically a defect — list/read endpoints are frequently gated only by `[Authorize]`, and command-layer `[RequirePermissionPolicy]` covers a further 3 distinct keys — but it means most `*.view` and `*.export` grants are configuration with no enforcement point. → **GAP-RBAC-003**.
- **Command-layer gates:** 12 `[RequirePermissionPolicy]` call sites over **3 distinct keys** — `roles.manage` (`RolesSlice.cs:66,102,141,176`), `users.manage` (`UserManagement.cs:32,92,94,96,200,240,288`), `documents.sign` (`DocumentCommands.cs:65-67`, on `PublishDocumentCommand`).
- Frontend affordances reference **68 distinct keys** across 126 `can()`/`canAny()` call sites in `frontend/src/app`. Every one of the 68 is a real catalogue key (checked against `AllKeys`).

### 1.7 Persistence

| Table | Columns of interest | PK | RLS | Source |
|---|---|---|---|---|
| `qams.role` | `id`, `tenant_id`, `name` vc(80), `normalized_name` vc(80), `description` vc(500), `is_system` bool, `default_language` vc(10), `is_active` bool, `xmin` (concurrency token), audit stamps | **`(tenant_id, id)`** composite, tenant-first — set by `Hardening5_CompositeKeys` (`20260731210953_Hardening5_CompositeKeys.cs:831-834`), superseding the single-column `pk_role` created at `20260730112800_RolePrivilegeModule.cs:51` | `ENABLE` + **`FORCE`** + policy `tenant_isolation` — `20260730112800_RolePrivilegeModule.cs:132-144` | `AuthorizationConfigurations.cs:11-20` |
| `qams.role_permission` | `permission_key` vc(60), `role_id`, **`tenant_id`** (shadow property, stamped by `TenantStampInterceptor`) | **`(tenant_id, role_id, permission_key)`** — `Hardening5_CompositeKeys.cs:825-828`; EF `HasKey("TenantId","role_id","PermissionKey")` at `AuthorizationConfigurations.cs:31` | `ENABLE` + `FORCE` + `tenant_isolation`, added by `Hardening4_ChildTenancy.cs:654-661` | `AuthorizationConfigurations.cs:22-32` |
| `qams.user_branch_access` | `user_id`, `branch_id`, **`tenant_id`** (added + backfilled from `user_account` by `Hardening4_ChildTenancy.cs:317-318`) | `(user_id, branch_id)` — `RolePrivilegeModule.cs:64` | `ENABLE` + `FORCE` + `tenant_isolation` — `Hardening4_ChildTenancy.cs:681-689` | owned by `UserAccount` |
| `qams.user_department_access` | `user_id`, `department_id`, **`tenant_id`** (`Hardening4_ChildTenancy.cs:319-320`) | `(user_id, department_id)` — `RolePrivilegeModule.cs:84` | `ENABLE` + `FORCE` + `tenant_isolation` — `Hardening4_ChildTenancy.cs:690-698` | owned by `UserAccount` |
| `qams.user_account` (columns added) | `role_id` uuid null (+ index `ix_user_account_role_id`), `preferred_language` vc(10) null | unchanged single-column (nullable tenant, deviation B9) | **none** — accepted deviation B9 | `RolePrivilegeModule.cs:14-27,114-118` |

**Referential integrity:** `role_permission → role` was converted from a single-column FK to the **tenant-composite** `fk_role_permission_role_tenant FOREIGN KEY (role_id, tenant_id) REFERENCES qams.role (id, tenant_id) ON DELETE CASCADE` (`Hardening4_ChildTenancy.cs:424-426`, supported by `ux_role_id_tenant` at `:345`), then renamed to `fk_role_permission_role_tenant_id_role_id` over the composite PK (`Hardening5_CompositeKeys.cs:1675-1681`). A grant belonging to another tenant's role is therefore structurally impossible.
`user_branch_access` / `user_department_access` cascade from `user_account` (`RolePrivilegeModule.cs:65-71, 85-91`).

**Unique index:** `ix_role_tenant_id_normalized_name` UNIQUE `(tenant_id, normalized_name)` — `RolePrivilegeModule.cs:120-125`, declared at `AuthorizationConfigurations.cs:20`. This is what makes INV-7 a database guarantee, not just a handler check.

**Stale comment:** `RolePrivilegeModule.cs:127-131` says the three child tables “carry no `tenant_id`” and are protected only through their parent. `Hardening4_ChildTenancy` subsequently gave all three a `tenant_id` and full FORCE RLS. The comment is now false. → **GAP-RBAC-016**.

### 1.8 States

| State machine | States | Transitions | Source |
|---|---|---|---|
| Role activation | `Active` \| `Inactive` | `Create → Active`; `Active --Deactivate--> Inactive` (blocked for system roles, `ROLE-004`); `Inactive --Reactivate--> Active`; both idempotent (early return at `Role.cs:163-166`, `:175-178`) | `Role.cs:156-182` |
| Role provenance | `System` \| `Tenant-defined` | **Set at creation, never changes.** `Role.CreateSystem` (`:79-80`) vs `Role.Create` (`:75-76`). No API mints a system role — `CreateSystem` is “not reachable from the API” per `Role.cs:78`. | `Role.cs:58, 75-80` |
| Privilege resolution (per request) | `Unresolved` → `PlatformAdmin` \| `Resolved(role)` \| `Resolved(no role)` | Set once by `ActiveSessionMiddleware`: platform tier → `SetPlatformAdmin()`; otherwise `PrivilegeResolver.ResolveAsync` → `Set(...)`; if the resolver returns null the request stays `Unresolved` (deny-all) | `src/NT.QAMS.WebApi/Middleware/RequestIdentity.cs:113-121`; `PrivilegeResolution.cs:12-49` |
| User scope | `Unrestricted` \| `Branch-restricted` \| `Department-restricted` \| `Both` | `SetScope(branches, departments)`; **empty list = unrestricted** (`IUserPrivileges` doc `:14-18`; `HasBranchRestriction` at `PrivilegeResolution.cs:33`) | `UserAccount.cs:186-203` |

### 1.9 Command authorization policies

`CommandPolicyAttribute` is abstract; five concrete policies exist (`src/NT.QAMS.Application/Abstractions/CommandAuthorization.cs`):

| Policy | Semantics | Declared at | Count in `src/NT.QAMS.Application/` |
|---|---|---|---|
| `[AllowUnauthenticated]` | No actor required; the handler does its own credential checks. | `:53` | 4 — `LoginCommand` (`Login.cs:17`), `ChangePasswordCommand` (`Login.cs:160`), `RefreshTokenCommand` (`RefreshSessions.cs:79`), `LogoutCommand` (`RefreshSessions.cs:151`) |
| `[RequireAuthenticatedActor]` | Any authenticated role **including** `ExternalAuditor`. | `:26` | 4 — `EnrollMfaCommand`, `ConfirmMfaCommand`, `SetPinCommand` (`MfaAndPin.cs:12,34,60`), `SetMyLanguageCommand` (`UserManagement.cs:310`) |
| `[RequireInternalActor]` | Any authenticated tier **except** `ExternalAuditor`. The default for writes. | `:19` | **193** call sites |
| `[RequireRole(params UserRole[])]` | Listed tiers only. | `:29-32` | **1** — `[RequireRole(UserRole.PlatformAdmin)]` on `ProvisionTenantCommand` (`ProvisionTenant.cs:17`) |
| `[RequirePermissionPolicy(module, action)]` | Actors whose configured role grants the key. | `:43-47` | **12** — see §1.6 |

Dispatch is `AuthorizationBehavior.Handle` (`AuthorizationBehavior.cs:39-88`), **commands only** — `IsCommand` gates the whole behaviour (`:34-37, 44-47`), so **queries are not authorized at the application layer at all**; read authorization is entirely the controller's job (stated at `AuthorizationBehavior.cs:22-24`).

### 1.10 Frontend surface

| Artefact | Behaviour | File:line |
|---|---|---|
| `PermissionsService.can(key)` | `isPlatformAdmin() \|\| granted().has(key)`; **false until the privileges fetch lands** — “a button appearing a beat late is a cosmetic cost, a button appearing wrongly is a broken promise”. | `frontend/src/app/core/permissions.service.ts:67-70`, doc `:16-17` |
| `PermissionsService.canAny(...keys)` | `keys.some(can)`. | `:72-75` |
| Privilege fetch | `GET {apiBaseUrl}/auth/me/privileges` in an `effect`, re-run on session change; **skipped entirely for platform admins** (`:50-52`); errors swallowed to `null` (`:56`). Also drives the i18n language (`:59-62`). | `:43-65` |
| `isPlatformAdmin` | Derived from the **session tier** `auth.role() === 'PlatformAdmin'`, not from the fetched privileges, so route guards resolve synchronously at bootstrap. | `:36-41` |
| `platformOnlyGuard` / `tenantOnlyGuard` | The **only** two route guards besides `authGuard`. They partition platform vs tenant shells; **no route carries a permission guard**. | `frontend/src/app/core/role.guard.ts:11-20`; `frontend/src/app/app.routes.ts:19,24,30,36` |
| `/roles` route | `loadComponent` → `RolesComponent`, under the tenant shell's `tenantOnlyGuard` only. Any tenant user can navigate to it; the screen then 403s on its API calls. | `frontend/src/app/app.routes.ts:390-392` |

The service's own doc comment states the contract explicitly: “*authoritative enforcement stays on the server — this is affordance, never a security boundary*” (`permissions.service.ts:13-14`). Case authors must not treat a hidden button as an access control.

### 1.11 Existing automated coverage (baseline — do not duplicate)

| Test | What it pins | File |
|---|---|---|
| `RoleTests` (11 facts) | Key format/uniqueness, catalogue closure, `ROLE-001/002/003/004/005`, event emission, no-op silence, `ManageRoles` literal, “every module declares `view`”. | `tests/NT.QAMS.Domain.UnitTests/Authorization/RoleTests.cs` |
| `RoleHandlersTests` (5 facts) | `ROLE-007` case-insensitive duplicate; `ROLE-006` on revoke, on move, on deactivate; the allowed case once a second holder exists. | `tests/NT.QAMS.Application.UnitTests/Authorization/RoleHandlersTests.cs` |
| `SystemRoleCatalogTests` (6 facts) | Seeding creates 5 roles and is idempotent; every seeded grant is a known key; only Tenant Administrator manages roles+users; the auditor holds **no** write privilege; parity pins for gates stricter than their module default; every tier maps to a seeded role name. | `tests/NT.QAMS.Application.UnitTests/Authorization/SystemRoleCatalogTests.cs` |
| `AuthorizationBehaviorTests` (9 facts) | `AUTHZ-000` deny-by-default; auditor blocked from writes; internal actors pass; anonymous refused; self-service admits the auditor; `RequireRole` exactness; open commands; permission policy admits only granting roles; **platform admin passes every permission policy**. | `tests/NT.QAMS.Application.UnitTests/Behaviors/AuthorizationBehaviorTests.cs` |
| `CommandPolicyTests` (1 fact, CI merge gate) | Every `ICommand`/`ICommand<T>` in the Application assembly carries **exactly one** `CommandPolicyAttribute`. | `tests/NT.QAMS.Architecture.Tests/CommandPolicyTests.cs:26-38` |
| `RoleEndpointMatrixTests` (2 facts) | 5 tenant roles × 5 representative endpoints: never 401, never 500, status ∈ {200,204,400,403,404}; every 403 is `application/problem+json` with a `code` starting `AUTH`. Plus explicit deny lists for `/api/users`, `/api/access-reviews`, `/api/tenants`. | `tests/NT.QAMS.WebApi.FunctionalTests/RoleEndpointMatrixTests.cs` |
| `RolePrivilegeFlowTests` (5 facts) | Provisioning seeds 5 roles + catalogue renders; a custom role's grants flip real endpoints 403↔allowed; unknown key rejected; lockout guard over HTTP; working scope as a hard filter on reads **and** writes. | `tests/NT.QAMS.WebApi.FunctionalTests/RolePrivilegeFlowTests.cs` |
| `UserEventTenantStampTests` (2 facts) | RP-D1 pins — access-control events carry the owning tenant; platform-admin events stay platform-level. | `tests/NT.QAMS.Application.UnitTests/Authorization/UserEventTenantStampTests.cs` |
| `RuntimeRolePrivilegeTests` (3 facts) | **Database** role least-privilege, not application RBAC — named similarly; do not confuse. | `tests/NT.QAMS.IntegrationTests/RuntimeRolePrivilegeTests.cs` |

`RoleEndpointMatrixTests` still labels its cells with the retired tier-gate names (`"TenantAdminOnly"`, `"QmOrAdmin"`, `"QmAdminAuditor"`, `"QmDeptAdmin-read?"` — `:41-45`) although those gates were converted to `[RequirePermission]` at v1.51.0. The assertions remain correct because the seeded roles reproduce the tiers, but the labels now describe a mechanism that no longer exists. → **GAP-RBAC-006**.

---

## 2. Divergences from the commissioning brief

| # | What the brief assumes | What the code does | Proof (file:line) | Gap id |
|---|---|---|---|---|
| D-1 | Privilege codes `USER.CREATE`, `DOC.APPROVE`, `NCR.TRIAGE`, `EQUIP.CALIB_SCHED`, `ROLE.MANAGE`, `LAB.CONFIG`, `DOC.REVIEW`, `DOC.PUBLISH`, `DOC.OBSOLETE`, `NCR.CREATE`, `NCR.INVESTIGATE`, `NCR.ACTION_PLAN`, `NCR.VERIFY`, `NCR.CLOSE`, `AUDIT.PLAN`. | **None of these strings exists.** Keys are lower-case `{module}.{action}` from a closed 170-key catalogue; an unknown key is refused with `ROLE-005`. | `PermissionCatalog.cs:194-195` (format), `:189-191` (closure), `Role.cs:196-201` (refusal) | GAP-RBAC-001 |
| D-2 | A single SoD violation code `SOD_VIOLATION`. | **Ten** distinct codes, each naming the specific duty pair, all → HTTP 422. | §1.3.4; `DomainExceptionHandler.cs:75-80` | GAP-RBAC-002 |
| D-3 | Fixed roles grant fixed capabilities (“a Quality Manager may approve documents”). | Authorization is **tenant-configured**. `UserRole` survives only as the platform/tenant structural tier; the seeded roles reproduce the old tiers on day one and the laboratory may re-privilege every one of them (except that a system role cannot be renamed or deactivated). | `UserAccount.cs:10-17`; `SystemRoleCatalog.cs:8-20`; conventions §2 line 44 | GAP-RBAC-004 |
| D-4 | Endpoints are gated by role name. | Gated by capability. Exactly **one** role gate survives, on the platform control plane. | `TenantsController.cs:12`; `RequirePermissionAttribute.cs:12-17` | GAP-RBAC-006 |
| D-5 | (URS-095) “`[RequirePermission]` on **127** endpoint gates”. | **144** call sites measured at v1.51.2. | `docs/validation/06-Revalidation-Delta-v1.38-v1.50.md:154` vs measured count across 37 controllers | GAP-RBAC-005 |
| D-6 | (Conventions §2 line 61) “30 permission modules in 7 groups”. | **31 modules in 8 groups, 170 keys** — agreeing with URS-095. | `PermissionCatalog.cs:68-75, 133-186`; `06-Revalidation-Delta…:154` | §0 (corrected in place) |
| D-7 | A role can be deleted. | Roles are **deactivated**, never deleted; there is no DELETE route and no domain method. | `RolesController.cs` (no `[HttpDelete]`); `Role.cs:156-182` | GAP-RBAC-015 |
| D-8 | An SoD control prevents one role from holding conflicting privileges (“toxic combination”). | **No such control exists.** Every implemented SoD rule compares *actor identity to record identity* at the moment of the act. A single role may hold `documents.create` **and** `documents.sign`; nothing refuses it. The system relies on the same person not being both author and approver of the *same record*. | All 10 sites in §1.3.4 compare a `Guid` actor to a `Guid` on the record; `Role.SetPermissions` (`Role.cs:136-150`) applies no combination rule | GAP-RBAC-008 |
| D-9 | Privilege changes require a recorded reason. | Only `SetRolePermissionsCommand` requires one (`ROLE-*` validator `RolesSlice.cs:150`, event field `Role.cs:225`). Creating a role with grants, deactivating a role, assigning a user to a role, and changing a working scope require **no** reason. | `RolesSlice.cs:71-80` (create validator: no `Reason`), `:177` (`SetRoleActiveCommand` has no reason), `UserManagement.cs:200,240` | GAP-RBAC-014 |
| D-10 | The UI enforces access. | The SPA is affordance-only and says so; no route carries a permission guard. | `permissions.service.ts:13-14`; `app.routes.ts` — only `authGuard`, `platformOnlyGuard`, `tenantOnlyGuard` | GAP-RBAC-007 |

---

## 3. State-transition matrices

### 3.1 Role activation × provenance

Cells give the outcome; `→` shows the resulting state.

| Current state | `Create` | `Rename` | `SetPermissions` | `Deactivate` | `Reactivate` |
|---|---|---|---|---|---|
| *(none)* → tenant-defined | → **Active / Tenant-defined**; `RoleCreated(IsSystem=false)` (`Role.cs:107`) | n/a | n/a | n/a | n/a |
| *(none)* → system (seeding only) | → **Active / System**; `RoleCreated(IsSystem=true)` (`Role.cs:79-80`) — not reachable over HTTP | n/a | n/a | n/a | n/a |
| **Active / Tenant-defined** | `ROLE-007` if the normalized name collides | → Active; `RoleRenamed` (`:128`) | → Active; `RolePermissionsChanged` **unless** the set is unchanged (`:142-145`); `ROLE-006` first if it drops the last `roles.manage` (`RolesSlice.cs:160-166`) | → **Inactive**; `RoleDeactivated` (`:169`); `ROLE-006` first if it holds `roles.manage` (`RolesSlice.cs:193-196`) | no-op, no event (`:175-178`) |
| **Active / System** | — | **`ROLE-003`** (`:116`); `UpdateRoleHandler` silently skips the rename for system roles and still applies the language (`RolesSlice.cs:128-133`) | → Active; same as tenant-defined — **system roles may be re-privileged** (`Role.cs:23-26`) | **`ROLE-004`** (`:160`) | no-op |
| **Inactive / Tenant-defined** | — | allowed (no state guard on `Rename`) | allowed — but the role grants nothing while inactive (`PrivilegeResolution.cs:91`) | no-op, no event (`:163-166`) | → **Active**; `RoleReactivated` (`:181`) |
| **Inactive / System** | — | `ROLE-003` | allowed | `ROLE-004` (thrown before the idempotence check, `Role.cs:158-166`) | → Active; `RoleReactivated` |

> Note the ordering at `Role.cs:158-166`: `Deactivate` checks `IsSystem` **before** the `!IsActive` early return, so deactivating an already-inactive system role raises `ROLE-004` rather than returning silently. That asymmetry is testable and intentional-looking but undocumented.

### 3.2 User ↔ role assignment

| Current | `AssignUserRoleCommand(roleId)` | `ChangeUserRoleCommand(tier)` | `SetUserActiveCommand(false)` |
|---|---|---|---|
| No role (`RoleId = null`) | → role assigned; `UserRoleAssigned` (`UserAccount.cs:173`). `ROLE-404` if unknown, `ROLE-008` if inactive (`UserManagement.cs:211-216`) | → tier set (`USER-005` if `PlatformAdmin` on a tenant user) **and** the seeded role for that tier assigned; `ROLE-009` if the tenant lost that seeded role (`SeededRoleDefault.cs:25`) | → `IsActive=false`; privileges become irrelevant (`ActiveSessionMiddleware` returns `401 AUTH-006`, `RequestIdentity.cs:98-102`) |
| Role granting `roles.manage`, and the only such active holder | → **`ROLE-006`** if the target role does not grant it (`UserManagement.cs:218-227`) | → **succeeds — the lockout guard is not called on this path** (`UserManagement.cs:115-128`) | → **succeeds — the lockout guard is not called on this path** (`UserManagement.cs:135-143`) |
| Role granting `roles.manage`, other active holders exist | → allowed | → allowed | → allowed |

The two unguarded cells are **GAP-RBAC-012** (tier change) and **GAP-RBAC-013** (deactivation). Both defeat INV-8 / URS-098 through a `users.manage` route rather than a `roles.manage` route.

### 3.3 Per-request privilege resolution

| Request condition | Resulting `IUserPrivileges` state | Effect on `[RequirePermission]` | Source |
|---|---|---|---|
| Anonymous | `IsResolved=false`, `Permissions={}` | Filter returns early **without denying** — an anonymous caller on a permission-gated action means the action is `[AllowAnonymous]`, and gating would be a programming error | `RequirePermissionAttribute.cs:43-46` |
| Authenticated, tier `PlatformAdmin` | `IsPlatformAdmin=true` | `Has(key)` returns **true for every key** (`PrivilegeResolution.cs:39`) — the platform admin bypasses the entire catalogue | `RequestIdentity.cs:116` |
| Authenticated, tenant user with an **active** role | `Resolved(roleId, roleName, keys, branches, departments, language)` | Per-key check | `PrivilegeResolution.cs:86-106` |
| Authenticated, tenant user whose role is **inactive** | `Resolved(roleId, roleName=null, keys={}, …)` — the role row is filtered out but `RoleId` is still echoed | Denies everything; `roleName` is null in problem responses | `PrivilegeResolution.cs:90-106` |
| Authenticated, tenant user with **no** role | `Resolved(null, null, {}, …)` | Denies everything | `PrivilegeResolution.cs:108-114` |
| Authenticated, user row missing | resolver returns `null`; setter never called → `IsResolved=false` | Denies everything (fail-closed default) | `PrivilegeResolution.cs:77-80`; `RequestIdentity.cs:119-121` |
| Account deactivated between requests | Middleware short-circuits before resolution | `401 AUTH-006` | `RequestIdentity.cs:98-102` |
| JWT role claim ≠ DB tier | Middleware short-circuits | `401 AUTH-007` | `RequestIdentity.cs:105-109` |

**Immediacy property (URS-096):** resolution is a database read on **every** authenticated request and is *deliberately uncached* — the rationale is written into the code (`PrivilegeResolution.cs:54-60`): a missed cache invalidation would leave a user holding a revoked privilege, “the one outcome an access control must not have”. A grant or revoke therefore takes effect on the very next request, on the same unexpired token.

### 3.4 Working-scope state

| Scope state | Reads of `IAllocatable` aggregates | Writes (create / re-point) |
|---|---|---|
| Unrestricted (both lists empty) | Whole tenant | Unrestricted; interceptor short-circuits (`OrgScopeGuardInterceptor.cs:41-44`) |
| Branch-restricted | Rows whose `branch_id ∈ allowed` **or** `branch_id IS NULL` (`AppDbContext.cs:205-207`) | `SCOPE-001` if `BranchId` is outside the list (`OrgScopeGuardInterceptor.cs:53-58`) |
| Department-restricted | Rows whose `department_id ∈ allowed` **or** `IS NULL` (`AppDbContext.cs:208-210`) | `SCOPE-002` (`:60-65`) |
| Both | Conjunction of the two | First failing dimension wins (branch checked first) |
| Platform admin | `HasBranchRestriction`/`HasDepartmentRestriction` are hard-false for platform admins (`PrivilegeResolution.cs:33-35`) → whole tenant | Unrestricted |

The 12 `IAllocatable` aggregates: `Audit`, `EquipmentItem`, `ReferenceStandard`, `MonitoringPoint`, `Complaint`, `FeedbackEntry`, `Nonconformance`, `QualityObjective`, `ChangeRequest`, `ManagementReview`, `RiskItem`, `Supplier` (declared at `Audit.cs:53`, `EquipmentItem.cs:77`, `ReferenceStandard.cs:18`, `MonitoringPoint.cs:43`, `Complaint.cs:24`, `FeedbackEntry.cs:17`, `Nonconformance.cs:84`, `QualityObjective.cs:37`, `ChangeAndReview.cs:13`, `ChangeAndReview.cs:158`, `RiskItem.cs:31`, `Supplier.cs:29`). EF permits one filter per entity, so tenant isolation and scope are composed into a single expression chosen by type (`AppDbContext.cs:171-181`).

> **Asymmetry worth a case:** an out-of-scope **read** returns *nothing* (the row is invisible, so the API answers `404`), while an out-of-scope **write** returns `422 SCOPE-001/002`. Two different observable behaviours for the same underlying restriction.

---

## 4. Decision tables

### 4.1 THE COMPLETE PRIVILEGE INVENTORY — 31 modules × their actions = 170 keys

Generated from `src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:133-186`. Bundle names refer to the action arrays at `PermissionCatalog.cs:110-126`. **A key marked `*` has no `[RequirePermission]` gate anywhere in the controllers** (92 of 170).

| # | Module key | Group | i18n name key | Action set | Keys | The keys |
|---|---|---|---|---|---|---|
| 1 | `nc` | quality | `perm.mod.nc` | SignedRecordLifecycle | 7 | `nc.view`* `nc.create`* `nc.edit`* `nc.approve` `nc.void` `nc.sign`* `nc.export`* |
| 2 | `complaints` | quality | `perm.mod.complaints` | FullRecordLifecycle | 6 | `complaints.view`* `complaints.create`* `complaints.edit` `complaints.approve` `complaints.void` `complaints.export`* |
| 3 | `feedback` | quality | `perm.mod.feedback` | FullRecordLifecycle | 6 | `feedback.view`* `feedback.create`* `feedback.edit` `feedback.approve`* `feedback.void` `feedback.export`* |
| 4 | `audits` | quality | `perm.mod.audits` | SignedRecordLifecycle | 7 | `audits.view`* `audits.create` `audits.edit`* `audits.approve`* `audits.void`* `audits.sign` `audits.export`* |
| 5 | `objectives` | quality | `perm.mod.objectives` | FullRecordLifecycle | 6 | `objectives.view`* `objectives.create` `objectives.edit`* `objectives.approve`* `objectives.void` `objectives.export`* |
| 6 | `changes` | quality | `perm.mod.changes` | SignedRecordLifecycle | 7 | `changes.view`* `changes.create`* `changes.edit`* `changes.approve` `changes.void` `changes.sign`* `changes.export`* |
| 7 | `reviews` | quality | `perm.mod.reviews` | SignedRecordLifecycle | 7 | `reviews.view`* `reviews.create` `reviews.edit` `reviews.approve`* `reviews.void` `reviews.sign`* `reviews.export` |
| 8 | `documents` | documents | `perm.mod.documents` | SignedRecordLifecycle | 7 | `documents.view` `documents.create`* `documents.edit` `documents.approve` `documents.void` `documents.sign` `documents.export`* |
| 9 | `quality-policy` | documents | `perm.mod.qualityPolicy` | SignedRecordLifecycle | 7 | `quality-policy.view` `quality-policy.create` `quality-policy.edit` `quality-policy.approve` `quality-policy.void`* `quality-policy.sign`* `quality-policy.export`* |
| 10 | `records` | documents | `perm.mod.records` | FullRecordLifecycle | 6 | `records.view`* `records.create`* `records.edit`* `records.approve`* `records.void` `records.export`* |
| 11 | `risks` | risk | `perm.mod.risks` | FullRecordLifecycle | 6 | `risks.view`* `risks.create`* `risks.edit`* `risks.approve` `risks.void` `risks.export`* |
| 12 | `compliance` | risk | `perm.mod.compliance` | *(explicit)* | 4 | `compliance.view` `compliance.create` `compliance.approve` `compliance.export` |
| 13 | `conflicts` | risk | `perm.mod.conflicts` | FullRecordLifecycle | 6 | `conflicts.view`* `conflicts.create`* `conflicts.edit`* `conflicts.approve` `conflicts.void` `conflicts.export`* |
| 14 | `org-context` | risk | `perm.mod.orgContext` | *(explicit — no `approve`)* | 5 | `org-context.view`* `org-context.create` `org-context.edit` `org-context.void` `org-context.export`* |
| 15 | `access-reviews` | risk | `perm.mod.accessReviews` | SignedRecordLifecycle | 7 | `access-reviews.view` `access-reviews.create`* `access-reviews.edit`* `access-reviews.approve`* `access-reviews.void`* `access-reviews.sign`* `access-reviews.export`* |
| 16 | `equipment` | resources | `perm.mod.equipment` | FullRecordLifecycle | 6 | `equipment.view`* `equipment.create`* `equipment.edit`* `equipment.approve`* `equipment.void` `equipment.export`* |
| 17 | `reference-standards` | resources | `perm.mod.referenceStandards` | FullRecordLifecycle | 6 | `reference-standards.view`* `reference-standards.create` `reference-standards.edit` `reference-standards.approve` `reference-standards.void` `reference-standards.export`* |
| 18 | `monitoring-points` | resources | `perm.mod.monitoringPoints` | FullRecordLifecycle | 6 | `monitoring-points.view`* `monitoring-points.create` `monitoring-points.edit` `monitoring-points.approve`* `monitoring-points.void` `monitoring-points.export`* |
| 19 | `suppliers` | resources | `perm.mod.suppliers` | FullRecordLifecycle | 6 | `suppliers.view`* `suppliers.create`* `suppliers.edit`* `suppliers.approve` `suppliers.void` `suppliers.export`* |
| 20 | `competencies` | people | `perm.mod.competencies` | SignedRecordLifecycle | 7 | `competencies.view`* `competencies.create` `competencies.edit` `competencies.approve` `competencies.void` `competencies.sign`* `competencies.export`* |
| 21 | `training` | people | `perm.mod.training` | FullRecordLifecycle | 6 | `training.view`* `training.create` `training.edit`* `training.approve`* `training.void`* `training.export`* |
| 22 | `test-authorizations` | people | `perm.mod.testAuthorizations` | SignedRecordLifecycle | 7 | `test-authorizations.view`* `test-authorizations.create` `test-authorizations.edit` `test-authorizations.approve` `test-authorizations.void` `test-authorizations.sign`* `test-authorizations.export`* |
| 23 | `users` | people | `perm.mod.users` | ConfigurationModule | 2 | `users.view` `users.manage` |
| 24 | `analytical-quality` | analytical | `perm.mod.analyticalQuality` | *(explicit — the only 8-action module)* | 8 | `analytical-quality.view`* `analytical-quality.create` `analytical-quality.edit` `analytical-quality.approve` `analytical-quality.void`* `analytical-quality.sign` `analytical-quality.export`* `analytical-quality.manage` |
| 25 | `proficiency-testing` | analytical | `perm.mod.proficiencyTesting` | FullRecordLifecycle | 6 | `proficiency-testing.view`* `proficiency-testing.create` `proficiency-testing.edit` `proficiency-testing.approve` `proficiency-testing.void` `proficiency-testing.export`* |
| 26 | `tasks` | operations | `perm.mod.tasks` | *(explicit)* | 4 | `tasks.view`* `tasks.create` `tasks.edit`* `tasks.manage` |
| 27 | `notifications` | operations | `perm.mod.notifications` | *(explicit)* | 2 | `notifications.view`* `notifications.manage` |
| 28 | `reports` | operations | `perm.mod.reports` | ReadOnlyModule | 2 | `reports.view`* `reports.export`* |
| 29 | `organization` | administration | `perm.mod.organization` | *(explicit)* | 4 | `organization.view`* `organization.create` `organization.edit` `organization.manage` |
| 30 | `tenant-settings` | administration | `perm.mod.tenantSettings` | ConfigurationModule | 2 | `tenant-settings.view`* `tenant-settings.manage` |
| 31 | `roles` | administration | `perm.mod.roles` | ConfigurationModule | 2 | `roles.view` `roles.manage` |

**Totals:** 31 modules · 8 groups · **170 keys** · 78 reachable at an HTTP gate · **92 with no HTTP gate** (`*`).

Action-set distribution: `SignedRecordLifecycle` (7 actions) on 10 modules; `FullRecordLifecycle` (6) on 12; `ConfigurationModule` (2) on 3; `ReadOnlyModule` (2) on 1; explicit sets on 5 (`compliance` 4, `org-context` 5, `analytical-quality` 8, `tasks` 4, `notifications` 2, `organization` 4 — note `notifications` and `organization` are written inline rather than reusing a bundle).

Every module declares `view` — pinned by `RoleTests.Every_module_declares_view_so_the_matrix_always_has_a_read_column`.

### 4.2 Role and Permission Matrix — endpoint → required key → tier that may hold it → SoD restriction

“Tier that may hold it (by seed)” is the set of **seeded** roles granted the key on day one (`SystemRoleCatalog.Definitions()`, `SystemRoleCatalog.cs:95-194`). A tenant may re-privilege every one of them, so this column is a **default**, never a guarantee. Legend: **TA** Tenant Administrator · **QM** Quality Manager · **DH** Department Head · **AN** Analyst · **EA** External Auditor · **PA** platform admin (bypasses all checks).

| Endpoint (verb + route) | Required permission key | Seeded holders | SoD restriction on the same act | Gate site |
|---|---|---|---|---|
| GET `/api/roles/catalog`, GET `/api/roles`, GET `/api/roles/{id}` | `roles.view` | TA, QM | n/a — read | `RolesController.cs:24,29,34` |
| POST `/api/roles`, PUT `/api/roles/{id}`, PUT `/api/roles/{id}/permissions`, POST `/api/roles/{id}/deactivate`, POST `/api/roles/{id}/reactivate` | `roles.manage` | **TA only** | `ROLE-006` — cannot orphan the last holder (permissions/deactivate paths only) | `RolesController.cs:39,48,56,64,72`; command policy `RolesSlice.cs:66,102,141,176` |
| GET `/api/users` | `users.view` | **TA only** | n/a | `UsersController.cs:23` |
| POST `/api/users`, POST `/api/users/{id}/role`, PUT `/api/users/{id}/assigned-role`, PUT `/api/users/{id}/scope`, PUT `/api/users/{id}/language`, POST `/api/users/{id}/deactivate` \| `/reactivate` \| `/reset-password` | `users.manage` | **TA only** | `ROLE-006` on `assigned-role` **only** — the tier and deactivate paths are unguarded (GAP-RBAC-012/013) | `UsersController.cs:28,34,43,52,61,69,77,85`; command policies `UserManagement.cs:32,92,94,96,200,240,288` |
| GET `/api/tenant-settings/mfa-policy`, PUT `/api/tenant-settings/mfa-policy` | `tenant-settings.manage` (**class-level, so the GET needs `manage` too**) | **TA only** | n/a | `TenantSettingsController.cs:18` |
| GET `/api/access-reviews`, POST `/api/access-reviews`, POST `/api/access-reviews/{id}/complete` | `access-reviews.view` (**class-level — writes need no further key**) | TA, QM | n/a | `AccessReviewsController.cs:20` |
| GET `/api/compliance/*` (audit-trail, field-changes, signatures, security-events, audit-trail-reviews, chain-verification) | `compliance.view` (class-level) | TA, QM, EA | n/a | `ComplianceController.cs:20` |
| POST `/api/compliance/audit-trail-reviews` | `compliance.view` **∧** `compliance.create` | TA, QM | n/a | `ComplianceController.cs:20` ∧ `:46` |
| POST `/api/compliance/audit-trail-reviews/{id}/complete` | `compliance.view` **∧** `compliance.approve` | TA, QM | n/a | `ComplianceController.cs:20` ∧ `:52` |
| GET `/api/exports/audit-trail.xlsx`, GET `/api/exports/signatures.xlsx` | `compliance.export` | TA, QM, EA | n/a | `ExportsController.cs:61,106` |
| GET `/api/exports/review-pack/{reviewId}.pdf` | `reviews.export` | TA, QM, DH, AN | n/a | `ExportsController.cs:126` |
| POST `/api/documents/{id}/recommend`, POST `/api/documents/{id}/reject` | `documents.approve` | TA, QM, DH | **`SOD-DOC-001`** on recommend — author ≠ reviewer (`ControlledDocument.cs:122`) | `DocumentsController.cs:99,107` |
| POST `/api/documents/{id}/publish` | `documents.sign` (HTTP) **∧** `documents.sign` (command policy) | TA, QM | **`SOD-DOC-002`** — author ≠ approver, checked twice (`DocumentCommands.cs:145`, `ControlledDocument.cs:156`); plus the password+PIN e-signature ceremony | `DocumentsController.cs:115`; `DocumentCommands.cs:65-67` |
| POST `/api/documents/{id}/confirm-review` | `documents.sign` | TA, QM | n/a | `DocumentsController.cs:37` |
| GET `/api/documents/{id}/acknowledgements` | `documents.view` | TA, QM, DH, AN, EA | n/a | `DocumentsController.cs:61` |
| POST `/api/documents/{id}/controlled-copies`, POST `/api/documents/controlled-copies/{copyId}/close` | `documents.edit` | TA, QM, DH, AN | n/a | `DocumentsController.cs:71,76` |
| POST `/api/documents/{id}/retire` | `documents.void` | TA, QM | n/a | `DocumentsController.cs:135` |
| GET `/api/quality-policy` | `quality-policy.view` | TA, QM | n/a | `QualityPolicyController.cs:32` |
| POST `/api/quality-policy` \| PUT `/api/quality-policy/{id}` | `quality-policy.create` \| `.edit` | TA, QM | n/a | `QualityPolicyController.cs:37,42` |
| POST `/api/quality-policy/{id}/approve` | `quality-policy.approve` | TA, QM | **`SOD-QP-001`** — approver ≠ author (`QualityPolicy.cs:78`) | `QualityPolicyController.cs:50` |
| POST `/api/nonconformances/{id}/triage`, `/verify`, `/confirm-effectiveness` | `nc.approve` | TA, QM | **`SOD-CAPA-002`** on verify, **`SOD-CAPA-001`** on confirm-effectiveness(true) — actor ≠ `RaisedBy` (`Nonconformance.cs:245,268`) | `NonconformancesController.cs:53,100,108` |
| POST `/api/nonconformances/{id}/reject` | `nc.void` | TA, QM | n/a | `NonconformancesController.cs:61` |
| POST `/api/audits` \| `/api/audits/{id}/sign-off` | `audits.create` \| `audits.sign` | TA, QM | sign-off: `EnsureSignerIsNotPreparer` is **not** applied to `Audit` — see GAP-RBAC-017 | `AuditsController.cs:30,70` |
| POST `/api/competencies`, `/{id}/assessments`, `/{id}/authorize`, `/{id}/revoke` | `competencies.create` \| `.edit` \| `.approve` \| `.void` | TA, QM (+ DH holds create/edit) | **`SOD-COMP-001`** on assess and authorize — actor ≠ `TraineeId` (`CompetencyRecord.cs:91,108`); pass mark 80 | `CompetenciesController.cs:28,37,45,53` |
| POST `/api/training-assignments` | `training.create` | TA, QM, DH | n/a | `CompetenciesController.cs:74` |
| POST `/api/test-authorizations` | `test-authorizations.create` | TA, QM | **`SOD-AUTHZ-001`** — grantor ≠ grantee (`TestAuthorization.cs:43`) | `TestAuthorizationsController.cs:27` |
| POST `/api/test-authorizations/{id}/suspend` \| `/reinstate` \| `/revoke` | `.edit` \| `.approve` \| `.void` | TA, QM | n/a | `TestAuthorizationsController.cs:36,44,52` |
| POST `/api/conflicts/{id}/assess` | `conflicts.approve` | TA, QM | **`SOD-COI-001`** — assessor ≠ declarant (`ConflictDeclaration.cs:72`) | `ConflictsController.cs:34` |
| POST `/api/conflicts/{id}/close` | `conflicts.void` | TA, QM | n/a | `ConflictsController.cs:42` |
| POST `/api/suppliers/{id}/approve`, POST `/api/suppliers/{id}/evaluations` | `suppliers.approve` | TA, QM | **`SOD-SUP-001`** on approve — approver ≠ registrant (`Supplier.cs:91`) | `GovernanceControllers.cs:200,220` |
| POST `/api/suppliers/{id}/suspend` | `suppliers.void` | TA, QM | n/a | `GovernanceControllers.cs:208` |
| POST `/api/{12 analytical study families}` (`validation-studies`, `carryover-studies`, `detection-limit-studies`, `instrument-comparabilities`, `interference-studies`, `linearity-studies`, `lot-comparisons`, `method-comparisons`, `outlier-screenings`, `precision-studies`, `reference-interval-studies`, `sigma-assessments`) | `analytical-quality.create` (13 sites incl. `uncertainty-budgets`) | TA, QM (+ DH/AN read-only) | n/a on create | 13 sites; see §1.5 |
| POST `/api/{…}/{id}/sign-off` (12 families) | `analytical-quality.sign` (12 sites) | TA, QM | **`SOD-AQ-001`** at all 14 guarded sign-off/approval points — signer ≠ `CreatedByUserId` | 12 sites |
| POST `/api/uncertainty-budgets/{id}/approve` | `analytical-quality.approve` | TA, QM | **`SOD-AQ-001`** (`UncertaintyBudget.cs:166`) | `UncertaintyController.cs:58` |
| POST `/api/uncertainty-budgets/{id}/components`, DELETE `…/components/{componentId}`, POST `…/calculate`; PUT `/api/sigma-assessments/{id}` | `analytical-quality.edit` | TA, QM | n/a | `UncertaintyController.cs:36,42,50`; `SigmaAssessmentsController.cs:35` |
| POST `/api/qc/profiles`, PUT `/api/qc/profiles/{id}/targets` | `analytical-quality.manage` | TA, QM | n/a — `QC-012` reason and `QC-013` forward-only apply instead | `AnalyticalQualityControllers.cs:21,28` |
| POST `/api/pt-plans` \| `/{id}/items` \| DELETE `/{id}/items/{itemId}` \| `/{id}/approve` \| `/{id}/close` | `proficiency-testing.create` \| `.edit` \| `.edit` \| `.approve` \| `.void` | TA, QM | `SOD-AQ-001` on the PT-plan approval (`PtPlan.cs:108`) | `PtPlansController.cs:26,34,43,51,66` |
| POST `/api/risks/{id}/residual` \| `/close` | `risks.approve` \| `risks.void` | TA, QM | n/a | `GovernanceControllers.cs:50,58` |
| POST `/api/changes/{id}/approve` \| `/review` \| `/reject` | `changes.approve` \| `changes.approve` \| `changes.void` | TA, QM | n/a | `GovernanceControllers.cs:98,122,106` |
| POST `/api/management-reviews` \| `/{id}/decisions` \| `/{id}/close` | `reviews.create` \| `.edit` \| `.void` | TA, QM | n/a | `GovernanceControllers.cs:146,156,162` |
| POST `/api/complaints/{id}/acknowledge` \| `/start-investigation` \| `/outcome` \| `/resolve` | `complaints.edit` | TA, QM, DH | n/a | `ComplaintsController.cs:41,57,65,73` |
| POST `/api/complaints/{id}/validate` \| `/close` | `complaints.approve` \| `.void` | TA, QM | n/a | `ComplaintsController.cs:49,81` |
| POST `/api/feedback/{id}/review` \| `/escalate` \| `/close` | `feedback.edit` \| `.edit` \| `.void` | TA, QM, DH (DH also holds `.void`) | n/a | `FeedbackController.cs:36,52,44` |
| POST `/api/quality-objectives` \| `/{id}/close` | `objectives.create` \| `.void` | TA, QM (+ DH create) | n/a | `QualityObjectivesController.cs:26,45` |
| POST `/api/org-context/interested-parties` \| PUT `/{id}` \| `/{id}/archive`; POST `/api/org-context/issues` \| PUT `/{id}` \| `/{id}/link-risk` \| `/{id}/close` | `org-context.create` \| `.edit` \| `.void` (×2 each) | TA, QM, DH | n/a — the DH holding `org-context.void` is the documented granularity compromise (`SystemRoleCatalog.cs:86-92`) | `OrgContextController.cs:24,34,44,58,67,76,84` |
| POST `/api/equipment/{id}/retire` | `equipment.void` | TA, QM | n/a | `EquipmentController.cs:65` |
| POST `/api/reference-standards` \| `/{id}/quarantine` \| `/{id}/reactivate` \| `/{id}/retire` | `.create` \| `.edit` \| `.approve` \| `.void` | TA, QM | n/a | `ReferenceStandardsController.cs:26,38,46,54` |
| POST `/api/monitoring-points` \| `/{id}/limits` \| `/{id}/suspend` \| `/{id}/resume` \| `/{id}/retire` | `.create` \| `.edit` ×3 \| `.void` | TA, QM | n/a | `MonitoringPointsController.cs:26,36,48,56,64` |
| POST `/api/archives/{id}/dispose` \| `/legal-hold` \| DELETE `/legal-hold` | `records.void` | TA, QM | n/a — the DELETE additionally needs `X-Change-Reason` (`400 CHANGE-REASON-REQUIRED`); place-legal-hold sends its Part-11 reason in the POST body | `OperationsControllers.cs:47,55,63` |
| POST `/api/sla-definitions` \| POST `/api/tasks` | `tasks.manage` \| `tasks.create` | TA, QM (+ DH/AN task create/edit) | n/a | `OperationsControllers.cs:81,102` |
| GET/POST `/api/notifications/rules`, GET `/api/notifications/monitor` | `notifications.manage` | TA, QM | n/a | `PlatformControllers.cs:111,116,123` |
| POST `/api/branches` \| POST `/api/departments` \| POST `/api/test-catalog` | `organization.create` | TA, QM | n/a | `PlatformControllers.cs:22,45,69` |
| POST `/api/lovs` | `organization.edit` | TA, QM | n/a | `PlatformControllers.cs:85` |
| POST `/api/branches/{id}/deactivate` \| POST `/api/departments/{id}/deactivate` | `organization.manage` | **TA only** | n/a | `PlatformControllers.cs:27,51` |
| `/api/tenants/*` (whole controller) | **tier gate** `[Authorize(Roles = PlatformAdmin)]`; command policy `[RequireRole(UserRole.PlatformAdmin)]` on `ProvisionTenantCommand` | PA only | n/a | `TenantsController.cs:12`; `ProvisionTenant.cs:17` |
| GET `/api/auth/me/privileges`, PUT `/api/auth/me/language` | none beyond `[Authorize]` | every authenticated actor incl. EA | n/a | `AuthController.cs:147,153` |

### 4.3 Seeded role privilege totals

Computed from `SystemRoleCatalog.Definitions()` (`SystemRoleCatalog.cs:95-194`).

| Seeded role | Constant | Grant rule | Modules touched | **Keys granted (of 170)** |
|---|---|---|---|---|
| Tenant Administrator | `SystemRoleCatalog.TenantAdministrator` (`:25`) | `PermissionCatalog.AllKeys` verbatim (`:100`) | 31 | **170** |
| Quality Manager | `:26` | Everything **except**: `users` and `tenant-settings` entirely; `roles` limited to `view`; `organization` minus `manage` (`:107-117`) | 29 | **164** |
| Department Head | `:27` | Explicit per-module table, derived endpoint-by-endpoint from the retired tier gates (`:123-147`) | 24 | **90** |
| Analyst | `:28` | Explicit per-module table — no approval, signing or configuration rights (`:153-177`) | 24 | **65** |
| External Auditor | `:29` | `view`+`export` only, and **only** on non-administration modules; `quality-policy` and `access-reviews` excluded outright; `reviews`/`tasks`/`notifications` reduced to `view` (`:184-193`) | 25 | **47** |

Verification of the External Auditor's read-only property is already pinned by `SystemRoleCatalogTests.The_external_auditor_holds_no_write_privileges_at_all`.

Seeding is **additive and idempotent** — a role the tenant already has, by normalized name, is left exactly as configured; re-running must never quietly restore a privilege an administrator removed (`SystemRoleCatalog.cs:16-20, 58-79`). Call sites: `ProvisionTenant.cs:66` (in the provisioning transaction, with the first admin placed on Tenant Administrator at `:69-70`) and the startup backfill `src/NT.QAMS.WebApi/Startup/StartupSeeding.cs:139`.

### 4.4 Translation table — brief-invented codes → real keys

Every code the commissioning brief uses is fictional in this build. This is the authoritative mapping; **case authors must use the right-hand column and must not write a case naming a left-hand code.**

| Brief code | Real key(s) in this build | Endpoint(s) it actually gates | Verdict |
|---|---|---|---|
| `USER.CREATE` | `users.manage` | POST `/api/users` (`UsersController.cs:28`) — the catalogue has **no** `users.create`; the `users` module is a `ConfigurationModule` with only `view` + `manage` (`PermissionCatalog.cs:168`) | Mapped, but **coarser**: one key covers create, role change, scope, language, deactivate, reactivate and password reset |
| `DOC.REVIEW` | `documents.approve` | POST `/api/documents/{id}/recommend` (`DocumentsController.cs:99`) | Mapped — note the *review* step is gated by `approve`, not by a distinct review key |
| `DOC.APPROVE` | `documents.approve` (recommend/reject) **and** `documents.sign` (publish) | `DocumentsController.cs:99,107` / `:115` | Mapped to **two** keys — the brief's single code conflates recommendation and the Part-11 signing ceremony |
| `DOC.PUBLISH` | `documents.sign` | POST `/api/documents/{id}/publish` (`DocumentsController.cs:115`; command policy `DocumentCommands.cs:65-67`) | Mapped |
| `DOC.OBSOLETE` | `documents.void` | POST `/api/documents/{id}/retire` (`DocumentsController.cs:135`) | Mapped — the code says *retire*, not *obsolete*; **no PDF “OBSOLETE - UNCONTROLLED” watermark exists** (conventions §2 line 118) |
| `NCR.CREATE` | `nc.create` | **no equivalent endpoint gate** — `nc.create` exists in the catalogue but no `[RequirePermission]` cites it; NC creation is gated by `[RequireInternalActor]` at the command layer only | Key exists, **gate does not** → GAP-RBAC-003 |
| `NCR.TRIAGE` | `nc.approve` | POST `/api/nonconformances/{id}/triage` (`NonconformancesController.cs:53`) | Mapped — triage shares `nc.approve` with verify and confirm-effectiveness |
| `NCR.INVESTIGATE` | **no equivalent** | — | **No equivalent.** No investigate endpoint carries a `[RequirePermission]`; there is no `nc.investigate` action in the closed 8-action enum |
| `NCR.ACTION_PLAN` | **no equivalent** | — | **No equivalent.** Same reason |
| `NCR.VERIFY` | `nc.approve` | POST `/api/nonconformances/{id}/verify` (`NonconformancesController.cs:100`) | Mapped, plus the SoD rule `SOD-CAPA-002` |
| `NCR.CLOSE` | `nc.approve` | POST `/api/nonconformances/{id}/confirm-effectiveness` (`NonconformancesController.cs:108`) | Mapped; note **`nc.void`** gates *reject* (`:61`), not close, plus `SOD-CAPA-001` |
| `AUDIT.PLAN` | `audits.create` | POST `/api/audits` (`AuditsController.cs:30`) | Mapped — “plan” is create; there is no separate planning key |
| `EQUIP.CALIB_SCHED` | **no equivalent** | — | **No equivalent.** The `equipment` module's only gated key is `equipment.void` (`EquipmentController.cs:65`); calibration scheduling is proposed by `ScheduledSweepService`, a `BackgroundService`, not by a privileged endpoint |
| `ROLE.MANAGE` | `roles.manage` | All five `RolesController` write routes; `PermissionCatalog.ManageRoles` is the pinned literal (`PermissionCatalog.cs:205`) | Mapped — the closest 1:1 in the whole table |
| `LAB.CONFIG` | Split across **`tenant-settings.manage`**, **`organization.manage`**, **`organization.create`**, **`organization.edit`**, **`analytical-quality.manage`**, **`tasks.manage`**, **`notifications.manage`** | `TenantSettingsController.cs:18`; `PlatformControllers.cs:22,27,45,51,69,85,111,116,123`; `AnalyticalQualityControllers.cs:21,28`; `OperationsControllers.cs:81` | **No single equivalent** — “lab configuration” fragments across 7 keys in 4 modules |
| `SOD_VIOLATION` | Ten distinct codes — see §4.5 | — | **No equivalent single code**; also the wrong HTTP class (the brief implies 403; the real codes are 422) |

### 4.5 Segregation-of-duties rule table (real codes)

| Rule id | Duty pair | Real code | HTTP | Comparison performed | Where | Bypass condition |
|---|---|---|---|---|---|---|
| SoD-1 | Preparer of an analytical record vs its sign-off | `SOD-AQ-001` | 422 | `signerId == CreatedByUserId` | `AggregateRoot.cs:36-42`, 14 call sites | **`CreatedByUserId is null` → no-op** (F-05b) |
| SoD-2 | Author of the quality policy vs its approver | `SOD-QP-001` | 422 | `approverId == CreatedByUserId` | `QualityPolicy.cs:78` | same null bypass |
| SoD-3 | Raiser of a nonconformance vs its verifier | `SOD-CAPA-002` | 422 | `actorId == RaisedBy` | `Nonconformance.cs:245` | none — `RaisedBy` is non-nullable |
| SoD-4 | Raiser of a nonconformance vs its closer | `SOD-CAPA-001` | 422 | `actorId == RaisedBy`, only when `effective == true` | `Nonconformance.cs:268` | **not evaluated when `effective == false`** — the early return at `:260-264` sends the record back to `ActionPlan` first |
| SoD-5 | Author of a document version vs its reviewer | `SOD-DOC-001` | 422 | `actorId == version.AuthorId` | `ControlledDocument.cs:122` | none |
| SoD-6 | Author of a document version vs its approver/publisher | `SOD-DOC-002` | 422 | `actor == approving.AuthorId` | `DocumentCommands.cs:145` (pre-check) **and** `ControlledDocument.cs:156` | none — deliberately checked before the e-signature so a doomed publish never consumes a signing attempt |
| SoD-7 | Trainee vs assessor of their competency | `SOD-COMP-001` | 422 | `assessorId == TraineeId` | `CompetencyRecord.cs:91` | none |
| SoD-8 | Trainee vs authorizer of their competency | `SOD-COMP-001` (same code, different act) | 422 | `actorId == TraineeId` | `CompetencyRecord.cs:108` | none |
| SoD-9 | Grantee vs grantor of a test authorization | `SOD-AUTHZ-001` | 422 | `grantedBy == userId` | `TestAuthorization.cs:43` | none |
| SoD-10 | Declarant vs assessor of a conflict of interest | `SOD-COI-001` | 422 | `assessorId == DeclarantId` | `ConflictDeclaration.cs:72` | none |
| SoD-11 | Registrant vs approver of a supplier | `SOD-SUP-001` | 422 | `actorId == RegisteredBy` | `Supplier.cs:91` | none |
| SoD-12 | Last-administrator protection (an **organisational** SoD control, not an actor-pair rule) | `ROLE-006` | 422 | “some active user still holds an active role granting `roles.manage`” | `RolesSlice.cs:28-52` | **bypassed via `ChangeUserRoleCommand` and `SetUserActiveCommand`** — GAP-RBAC-012/013 |

**Not implemented (do not write execution cases):** any toxic-combination / privilege-conflict rule at *role composition* time; any SoD rule on `Audit` sign-off (`AuditsController.cs:70` reaches a sign-off with no `EnsureSignerIsNotPreparer` call found in `src/NT.QAMS.Domain/AuditManagement/Audit.cs`); any four-eyes rule on role or user administration itself.

### 4.6 Decision tables for the case authors

**DT-1 — HTTP gate outcome** (`RequirePermissionAttribute.OnAuthorizationAsync`, `:38-60`)

| # | Authenticated? | Platform admin? | Role active? | Role grants key? | Outcome |
|---|---|---|---|---|---|
| 1 | No | — | — | — | Filter passes through; the framework's `[Authorize]` produces `401` + `AUTH-401` problem+json (`ProblemAuthorizationResultHandler.cs:35-48`) |
| 2 | Yes | Yes | n/a | n/a | **Allow** — `Has()` short-circuits true (`PrivilegeResolution.cs:39`) |
| 3 | Yes | No | Yes | Yes | **Allow** |
| 4 | Yes | No | Yes | No | **403** `AUTHZ-403`, `application/problem+json` |
| 5 | Yes | No | No | (grants irrelevant) | **403** `AUTHZ-403` — the inactive role resolves to zero keys |
| 6 | Yes | No | no role assigned | — | **403** `AUTHZ-403` |
| 7 | Yes, account deactivated between requests | — | — | — | **401** `AUTH-006` before the filter runs (`RequestIdentity.cs:98-102`) |
| 8 | Yes, token tier ≠ DB tier | — | — | — | **401** `AUTH-007` (`RequestIdentity.cs:105-109`) |

**DT-2 — command-layer policy dispatch** (`AuthorizationBehavior.cs:49-86`)

| # | Request kind | Policy attribute | Actor | Outcome |
|---|---|---|---|---|
| 1 | Query (`IQuery`) | any / none | any | **Pass, unconditionally** — the behaviour returns at `:44-47` |
| 2 | Command | none | any | `AUTHZ-000` → 403 |
| 3 | Command | `AllowUnauthenticated` | any | Pass |
| 4 | Command | any other | unauthenticated **or** no tier | `AUTHZ-001` → 403 |
| 5 | Command | `RequirePermissionPolicy` with a key absent from `AllKeys` | authenticated | `AUTHZ-008` → 403 (**before** the grant check) |
| 6 | Command | `RequireAuthenticatedActor` | any authenticated | Pass (auditor included) |
| 7 | Command | `RequireInternalActor` | `ExternalAuditor` | `AUTHZ-002` → 403 |
| 8 | Command | `RequireInternalActor` | any other tier | Pass |
| 9 | Command | `RequireRole(PlatformAdmin)` | non-platform tier | `AUTHZ-002` → 403 |
| 10 | Command | `RequirePermissionPolicy(k)` | role grants `k` (or platform admin) | Pass |
| 11 | Command | `RequirePermissionPolicy(k)` | role does not grant `k` | `AUTHZ-002` → 403 |

**DT-3 — `ROLE-006` lockout guard** (`RolesSlice.cs:28-52`)

| # | Operation | Role currently grants `roles.manage`? | Another **active** role granting it, held by another **active** user? | Guard invoked? | Outcome |
|---|---|---|---|---|---|
| 1 | `PUT /roles/{id}/permissions` dropping the key | Yes | Yes | Yes | Allowed |
| 2 | `PUT /roles/{id}/permissions` dropping the key | Yes | No | Yes | **`ROLE-006`** |
| 3 | `PUT /roles/{id}/permissions` keeping the key | Yes | — | **No** (`losesManage` false, `:160-166`) | Allowed |
| 4 | `POST /roles/{id}/deactivate` | Yes | Yes | Yes | Allowed |
| 5 | `POST /roles/{id}/deactivate` | Yes | No | Yes | **`ROLE-006`** |
| 6 | `POST /roles/{id}/deactivate` | No | — | **No** (`:193`) | Allowed |
| 7 | `PUT /users/{id}/assigned-role` moving the last holder to a non-granting role | Yes | No | Yes (`UserManagement.cs:218-227`) | **`ROLE-006`** |
| 8 | `PUT /users/{id}/assigned-role` to a role that also grants it | — | — | **No** (`!role.Grants(...)` is false) | Allowed |
| 9 | `POST /users/{id}/role` (tier) moving the last holder to `Analyst` | Yes | No | **No — guard absent** | **Allowed → tenant locked out** (GAP-RBAC-012) |
| 10 | `POST /users/{id}/deactivate` on the last holder | Yes | No | **No — guard absent** | **Allowed → tenant locked out** (GAP-RBAC-013) |

**DT-4 — working scope** (`AppDbContext.cs:200-211` ∧ `OrgScopeGuardInterceptor.cs:39-67`)

| # | Branch restriction | Record `BranchId` | Read | Write (add/modify) |
|---|---|---|---|---|
| 1 | none (empty list) | any | visible | allowed |
| 2 | `{B1}` | `B1` | visible | allowed |
| 3 | `{B1}` | `B2` | **invisible** → the API answers `404` | **`SCOPE-001`** → 422 |
| 4 | `{B1}` | `null` | **visible** (unattributed evidence is not hidden) | allowed (`CanAccessBranch(null)` is true, `PrivilegeResolution.cs:42`) |
| 5 | platform admin, any list | any | visible | allowed (`HasBranchRestriction` hard-false, `:33`) |

Rows 1–5 apply identically to departments with `SCOPE-002` (`OrgScopeGuardInterceptor.cs:60-65`). When both dimensions fail, **branch wins** — it is checked first.

**DT-5 — attribute composition**

| # | Class-level `[RequirePermission]` | Method-level `[RequirePermission]` | Required | Example |
|---|---|---|---|---|
| 1 | absent | present | the method key | most of the 141 method-level sites |
| 2 | present | absent | the class key | `GET /api/access-reviews`, `POST /api/access-reviews` (`AccessReviewsController.cs:20`) |
| 3 | present | present | **both** (AND) | `POST /api/compliance/audit-trail-reviews` needs `compliance.view` ∧ `compliance.create` |
| 4 | present | present, same key | the key once (idempotent) | none observed |

**DT-6 — HTTP status by code family** (`DomainExceptionHandler.cs:26-82`, evaluated top-down)

| # | Condition | Status | Notes |
|---|---|---|---|
| 1 | `DbUpdateConcurrencyException` | 409 `CONCURRENCY-409` | `xmin` token |
| 2 | `ValidationException` | 400 with an `errors` map — **no `code`** | FluentValidation |
| 3 | `InvalidStateTransitionException` | 409 with the transition code | |
| 4 | code starts `AUTH-` | 401 | exact prefix, hyphen included |
| 5 | code starts `AUTHZ-` | **403** | catches `AUTHZ-000/001/002/008` **and** the `test-authorizations` business codes |
| 6 | code ends `-404` | 404 | `ROLE-404`, `AUTHZ-404`, `USER-404` |
| 7 | any other `DomainException` | 422 | all ten `SOD-*`, all `ROLE-00x`, all `SCOPE-00x` |

> Arm 5 precedes arm 6, so **`AUTHZ-404` resolves to 403, not 404** — the prefix match fires first. Verify this against the running build before asserting a 404 on any `AUTHZ-404` path.

---

## 6. UAT scenarios (Gherkin)

Business-readable, written for a laboratory quality manager to sign. Each names its evidence label per conventions §4.

### TC-RBAC-UAT-001 — A laboratory composes its own role  `[IV]`
```gherkin
Given I am signed in as the Tenant Administrator of the "demo-lab" workspace
  And the privilege matrix shows 31 modules grouped into 8 navigation groups
When I create a role named "Deputy Quality Manager"
  And I grant it exactly: documents.view, documents.approve, nc.view, nc.approve
  And I save the role
Then the role appears in the roles list marked as tenant-defined, not a system role
  And its privilege count reads 4
  And the audit trail records a role-created entry naming "Deputy Quality Manager"
```

### TC-RBAC-UAT-002 — A privilege takes effect on the next click, not the next sign-in  `[IV]`
```gherkin
Given an analyst is signed in and holds no "approve" privilege on nonconformances
  And the analyst has an open nonconformance awaiting triage
When the analyst attempts to triage it
Then the system refuses the action with a clear "you do not have permission" message
When the Tenant Administrator grants nc.approve to the analyst's role, giving the reason
  "Cover for the Quality Manager during annual leave"
  And the analyst retries the triage without signing out and back in
Then the triage is accepted
```

### TC-RBAC-UAT-003 — Withdrawing a privilege is immediate  `[IV]`
```gherkin
Given the analyst from TC-RBAC-UAT-002 currently holds nc.approve
When the Tenant Administrator removes nc.approve from that role with the reason
  "Cover period ended"
  And the analyst attempts another triage on their existing session
Then the triage is refused
  And the audit trail shows the revocation with its reason, the role name, and the operator
```

### TC-RBAC-UAT-004 — The laboratory cannot lock itself out of its own privilege screen  `[IV]`
```gherkin
Given "Tenant Administrator" is the only active role granting "Roles & Privileges - Manage"
  And exactly one active user holds that role
When that user tries to remove the manage-privileges grant from the role
Then the system refuses and explains that no active user would be able to manage privileges
  And it advises granting "Roles & Privileges - Manage" to another active user's role first
  And the role's privileges are unchanged
```

### TC-RBAC-UAT-005 — A grant that means nothing cannot be saved  `[IV]`
```gherkin
Given I am editing a tenant-defined role
When I attempt to save a privilege key that this build does not recognise,
  for example the code "DOC.APPROVE" carried over from the commissioning brief
Then the save is refused, naming the unrecognised key
  And the message explains that a privilege must map to a real capability
  And the role's stored privileges are unchanged
```

### TC-RBAC-UAT-006 — A branch-restricted analyst sees only their own branch  `[IV]`
```gherkin
Given the laboratory operates two branches, "North" and "South"
  And an analyst's working scope is set to "North" only
  And there is one nonconformance raised in North, one in South, and one with no branch recorded
When the analyst opens the nonconformance register
Then the North record and the unattributed record are listed
  And the South record is not listed at all
When the analyst tries to raise a new nonconformance against the South branch
Then the system refuses, stating they are not permitted to work in the selected branch
```

### TC-RBAC-UAT-007 — The author of a document cannot approve it  `[IV]`
```gherkin
Given a Quality Manager has authored a new version of SOP "QM-001"
  And that same Quality Manager holds every document privilege including sign
When they submit the version for review and then attempt to recommend it themselves
Then the system refuses on segregation of duties: the author cannot review their own document
When a second approver recommends it and the author then attempts to publish it
Then the system refuses on segregation of duties before any electronic signature is requested
  And no failed-signature event is recorded against the author's account
```

### TC-RBAC-UAT-008 — An external auditor can read the quality record and change nothing  `[IV]`
```gherkin
Given an external auditor account exists on the seeded "External Auditor" role
When the auditor signs in
Then they can open the audit trail, the signature log and the quality registers
  And they can export the audit trail and the signature manifest
  And every create, edit, approve, void and sign action is unavailable to them
  And the users, roles, tenant-settings and organisation screens return "no permission"
```

---

## 7. Exploratory charters

Time-boxed, session-based. Each charter states its mission, the areas it touches, and what would make it a finding rather than an observation.

### TC-RBAC-EXPL-001 — Hunt for permission keys with no enforcement point
**Explore** the 92 catalogue keys marked `*` in §4.1 (`PermissionCatalog.cs:133-186` vs the 144 `[RequirePermission]` sites and the 12 `[RequirePermissionPolicy]` sites)
**With** a role granted **only** the unreached key and nothing else, driven against every route in the corresponding module
**To discover** whether the key changes any observable behaviour anywhere — HTTP gate, command policy, query projection, or SPA affordance.
**Time-box:** 120 min. **Finding if:** a key changes nothing at all anywhere (dead configuration that reads to an auditor as an active privilege). **Feeds:** GAP-RBAC-003.

### TC-RBAC-EXPL-002 — Attack the last-administrator invariant from every direction
**Explore** every route reachable with `users.manage` or `roles.manage` (`UsersController.cs`, `RolesController.cs`)
**With** a tenant whose only `roles.manage` holder is the caller themselves, and sequences that combine tier change, configurable-role change, deactivation, role deactivation and permission edits — including interleaved and concurrent attempts
**To discover** every path that reaches zero active `roles.manage` holders.
**Time-box:** 90 min. **Finding if:** any sequence completes without `ROLE-006`. Two are already predicted (GAP-RBAC-012, GAP-RBAC-013) — the charter's value is finding a third, and probing the race between two concurrent `PUT /roles/{id}/permissions` calls, which `EnsureSurvivesAsync` reads outside any lock.

### TC-RBAC-EXPL-003 — Probe the boundary between the tier and the configurable role
**Explore** `POST /api/users/{id}/role` (tier) against `PUT /api/users/{id}/assigned-role` (configurable role), plus `ActiveSessionMiddleware`'s `AUTH-007` token-role check (`RequestIdentity.cs:105-109`)
**With** users placed on bespoke roles whose grants diverge sharply from their tier, then moved by tier
**To discover** whether a tier change silently discards a bespoke privilege set (`SeededRoleDefault.AssignAsync` overwrites `RoleId` unconditionally, `UserManagement.cs:122`), and whether the resulting mismatch surfaces to the operator or only to the affected user as an `AUTH-007` sign-out.
**Time-box:** 60 min. **Finding if:** a bespoke role assignment is lost with no warning and no distinguishable audit entry.

### TC-RBAC-EXPL-004 — Test the working scope as a security boundary rather than a filter
**Explore** the composed query filter (`AppDbContext.cs:200-211`) and `OrgScopeGuardInterceptor` across all 12 `IAllocatable` aggregates
**With** a branch-restricted actor using: direct-id GETs for out-of-scope records, exports, report endpoints, nested/child routes, `IgnoreQueryFilters` paths, and records whose branch is changed *to* out-of-scope in the same request
**To discover** any read path that returns an out-of-scope row, and whether the 404-vs-422 asymmetry (DT-4) leaks the existence of out-of-scope records through timing or message differences.
**Time-box:** 120 min. **Finding if:** an out-of-scope row is readable through any route, or an export bypasses the filter.

### TC-RBAC-EXPL-005 — Stress the SoD rules where identity is unknown or indirect
**Explore** all ten `SOD-*` sites (§4.5), concentrating on `EnsureSignerIsNotPreparer`'s null bypass (`AggregateRoot.cs:38`)
**With** records created by system paths (seeding, `ScheduledSweepService`, `OutboxProcessor`, migration backfill) so `CreatedByUserId` is null, and with the `SOD-CAPA-001` path entered via `ConfirmEffectiveness(effective: false)` first
**To discover** how many regulated sign-offs are reachable by their own preparer because the preparer is unrecorded, and whether the not-effective route can be used to launder a self-close.
**Time-box:** 90 min. **Finding if:** a self-sign-off completes on a record a real user created. **Feeds:** GAP-RBAC-009.

### TC-RBAC-EXPL-006 — Compare what the SPA offers with what the server allows
**Explore** the 68 distinct permission keys referenced across the 126 `can()`/`canAny()` call sites in `frontend/src/app`, against the 78 keys that actually gate an endpoint
**With** each of the five seeded roles plus two bespoke roles, walking every screen in the tenant shell
**To discover** (a) affordances shown for actions the server will refuse, (b) actions the server permits that the SPA hides, and (c) screens reachable by direct URL that render fully before failing — `/roles` is the known case, since no route carries a permission guard (`app.routes.ts`).
**Time-box:** 120 min. **Finding if:** a destructive control is offered to a role that cannot use it, or a granted capability is unreachable through the UI. **Feeds:** GAP-RBAC-007.

---

## 8. Gap Register (this module)

Seventeen gaps. Severity uses the package scale: **Critical** (blocks release / regulatory finding), **High** (must be resolved before validation sign-off), **Medium** (resolve in the next release), **Low** (documentation or hygiene).

---

**GAP-RBAC-001 — The commissioning brief's privilege codes do not exist**
- **Source reference:** Commissioning brief privilege codes; `src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:189-195`; conventions §2 line 64.
- **Description:** The brief specifies `USER.CREATE`, `DOC.APPROVE`, `NCR.TRIAGE`, `EQUIP.CALIB_SCHED`, `ROLE.MANAGE`, `LAB.CONFIG`, `DOC.REVIEW`, `DOC.PUBLISH`, `DOC.OBSOLETE`, `NCR.CREATE`, `NCR.INVESTIGATE`, `NCR.ACTION_PLAN`, `NCR.VERIFY`, `NCR.CLOSE`, `AUDIT.PLAN`. None is a valid key. The build uses lower-case `{module}.{action}` from a closed 170-key catalogue and refuses anything else with `ROLE-005`.
- **Impact:** Any requirement, procedure or training material citing a brief code refers to a privilege that cannot be granted. A validation reviewer tracing the URS to the configuration screen finds no match.
- **Testing limitation:** No case may be written against a brief code. Every RBAC case must cite the real key. The translation table in §4.4 is the only sanctioned bridge; four brief codes have **no equivalent at all** (`NCR.INVESTIGATE`, `NCR.ACTION_PLAN`, `EQUIP.CALIB_SCHED`, and `LAB.CONFIG` as a single code).
- **Recommended clarification:** Reissue the brief's privilege section against the catalogue, or formally adopt §4.4 as the controlled mapping and reference it from the URS.
- **Suggested acceptance criteria:** Every privilege named in an approved requirement is a member of `PermissionCatalog.AllKeys`, verifiable by a test that reads the requirement register and asserts membership.
- **Severity:** High. **Responsible role:** Product Owner + Quality Manager (document control).

---

**GAP-RBAC-002 — `SOD_VIOLATION` is not a real code; ten distinct codes exist, on a different HTTP class**
- **Source reference:** Commissioning brief; §1.3.4 and §4.5 of this file; `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:75-80`.
- **Description:** The brief expects one code. The build raises `SOD-AQ-001`, `SOD-QP-001`, `SOD-CAPA-001`, `SOD-CAPA-002`, `SOD-DOC-001`, `SOD-DOC-002`, `SOD-COMP-001`, `SOD-AUTHZ-001`, `SOD-COI-001`, `SOD-SUP-001` — each naming its duty pair — and all return **HTTP 422**, not 403.
- **Impact:** An interface or SOP written to detect `SOD_VIOLATION` or a 403 will silently miss every real SoD refusal.
- **Testing limitation:** No case may assert `SOD_VIOLATION` or a 403 on an SoD path. Cases must assert 422 plus the exact code from §4.5.
- **Recommended clarification:** Confirm 422 is the intended class for a duty-pair refusal (it is a business-rule failure, not an access-control refusal) and record the ten codes in the URS.
- **Suggested acceptance criteria:** Each of the ten codes has at least one requirement tracing to it, and a functional case asserting `422` + the exact code string.
- **Severity:** High. **Responsible role:** Quality Manager + Lead Developer.

---

**GAP-RBAC-003 — 92 of 170 permission keys have no enforcement point**
- **Source reference:** §4.1 of this file (keys marked `*`); `PermissionCatalog.cs:133-186` vs the 144 `[RequirePermission]` sites and 12 `[RequirePermissionPolicy]` sites.
- **Description:** 78 keys gate an HTTP endpoint; 3 more gate a command; the remaining **92** appear nowhere. They include nearly every `*.view` (only `documents.view`, `quality-policy.view`, `users.view`, `roles.view`, `compliance.view`, `access-reviews.view` are gated) and nearly every `*.export` (only `compliance.export` and `reviews.export`). An administrator can grant `nc.export` or `equipment.approve` and nothing changes.
- **Impact:** The privilege screen displays 170 switches of which 92 are inert. A user-access review that certifies "this role may export nonconformances" is certifying a configuration the system does not honour — a §11.10(d) recertification defect. Conversely, revoking such a key gives a false sense of restriction.
- **Testing limitation:** No negative access-control case can be written for those 92 keys — there is no observable behaviour to assert. Cases for them are `[GD]` on this gap.
- **Recommended clarification:** For each of the 92, decide: gate the corresponding endpoint, or remove the action from that module's action set. State explicitly whether read/list endpoints are intended to be gated only by `[Authorize]`.
- **Suggested acceptance criteria:** An architecture test asserts that every member of `PermissionCatalog.AllKeys` appears in at least one `[RequirePermission]` or `[RequirePermissionPolicy]` declaration, and fails CI otherwise (the mirror of `CommandPolicyTests`).
- **Severity:** **Critical** — it is a compliance-visible discrepancy between the configured and enforced privilege sets. **Responsible role:** Solution Architect + Quality Manager.

---

**GAP-RBAC-004 — `tenant-settings.view` is unreachable; the read endpoint demands `manage`**
- **Source reference:** `src/NT.QAMS.WebApi/Controllers/TenantSettingsController.cs:18`; `PermissionCatalog.cs:184`.
- **Description:** The `tenant-settings` module is a `ConfigurationModule` offering `view` and `manage`. The controller carries a **class-level** `[RequirePermission(TenantSettings, Manage)]`, so `GET /api/tenant-settings/mfa-policy` requires `manage`. A role granted only `tenant-settings.view` can do nothing at all.
- **Impact:** Read-only oversight of the tenant's MFA policy — exactly what an auditor or a deputy would need — is impossible without granting full change rights. It forces over-privileging.
- **Testing limitation:** A positive case for `tenant-settings.view` cannot be written; the only observable behaviour is 403 for a view-only holder. `[GD]` on this gap.
- **Recommended clarification:** Either move the class-level gate to the write action and gate the GET on `view`, or remove `view` from the module's action set.
- **Suggested acceptance criteria:** A role holding only `tenant-settings.view` receives `200` on `GET /api/tenant-settings/mfa-policy` and `403 AUTHZ-403` on `PUT`.
- **Severity:** Medium. **Responsible role:** Lead Developer.

---

**GAP-RBAC-005 — URS-095 understates the endpoint-gate count (127 vs 144)**
- **Source reference:** `docs/validation/06-Revalidation-Delta-v1.38-v1.50.md:154`; measured count across 37 controllers in `src/NT.QAMS.WebApi/Controllers/`.
- **Description:** URS-095's implementation column states "`[RequirePermission]` on 127 endpoint gates". The v1.51.2 build has **144**. The requirement was written at v1.51.0 and not updated when gates were added.
- **Impact:** The traceability matrix cites a number the build does not match, which an auditor will treat as evidence that the RTM is not maintained.
- **Testing limitation:** None for behaviour; it affects only the traceability assertion. A case that asserts "144 gates" would fail against the URS and pass against the code.
- **Recommended clarification:** Update URS-095, or replace the absolute count with a reference to a generated inventory so it cannot drift again.
- **Suggested acceptance criteria:** The URS figure equals the count produced by a repeatable command, and a CI check fails when they diverge.
- **Severity:** Medium. **Responsible role:** Validation Lead.

---

**GAP-RBAC-006 — Nine unused role constants and stale gate labels survive the v1.51.0 conversion**
- **Source reference:** `src/NT.QAMS.WebApi/Authorization/Roles.cs:14-31`; `tests/NT.QAMS.WebApi.FunctionalTests/RoleEndpointMatrixTests.cs:38-45`; `CLAUDE.md` §2 rules 2 and 3.
- **Description:** `Roles.cs` declares ten constants; only `Roles.PlatformAdmin` is referenced anywhere in `src/` (`TenantsController.cs:12`). `TenantAdmin`, `QualityManager`, `DepartmentHead`, `Analyst`, `ExternalAuditor`, `TenantAdminOnly`, `QmOrAdmin`, `QmAdminAuditor` and `QmDeptAdmin` are dead. `RoleEndpointMatrixTests` still labels its cells with the retired gate names and its `Endpoint.Gate` field carries a literal `"QmDeptAdmin-read?"` — a question mark left in an assertion label.
- **Impact:** A reader of `Roles.cs` reasonably concludes role-based gating is still in use. The test file documents a mechanism that no longer exists, weakening it as validation evidence. Contradicts the project's own no-dead-code rule.
- **Testing limitation:** None functional; the assertions remain correct because the seeded roles reproduce the tiers. It is an evidence-quality problem.
- **Recommended clarification:** Delete the nine unused constants and relabel the test's `Gate` values with the real permission keys.
- **Suggested acceptance criteria:** `Roles.cs` contains only constants that are referenced; every `Endpoint.Gate` label in `RoleEndpointMatrixTests` is a valid `PermissionCatalog` key.
- **Severity:** Low. **Responsible role:** Lead Developer.

---

**GAP-RBAC-007 — No route in the SPA carries a permission guard**
- **Source reference:** `frontend/src/app/app.routes.ts` (only `authGuard`, `platformOnlyGuard`, `tenantOnlyGuard` at `:19,24,30,36`); `frontend/src/app/core/role.guard.ts:11-20`; `/roles` at `app.routes.ts:390-392`.
- **Description:** Route protection distinguishes only *platform* from *tenant*. Any authenticated tenant user can navigate to `/roles`, `/users`, `/access-reviews` and every other administrative screen; the component loads and its API calls then return 403. `PermissionsService` supplies `can()` but no guard consumes it.
- **Impact:** Poor experience (a fully rendered admin screen that does nothing) and a weak signal to a user-access reviewer, who may infer from the navigable surface that a user has access they do not. Not a security defect — the server enforces independently, as `permissions.service.ts:13-14` states.
- **Testing limitation:** Frontend authorization cases must assert *server refusal*, never route blocking. Any case asserting "the analyst cannot reach /roles" would fail.
- **Recommended clarification:** Decide whether a `requirePermissionGuard(key)` should exist. If the affordance-only model is deliberate, state it in the URS so the absence is a design decision on record rather than an omission.
- **Suggested acceptance criteria:** Either every administrative route declares a permission guard that redirects, or a requirement records that route-level authorization is intentionally server-side only.
- **Severity:** Medium. **Responsible role:** Frontend Lead + Product Owner.

---

**GAP-RBAC-008 — No toxic-combination control at role-composition time**
- **Source reference:** `src/NT.QAMS.Domain/Authorization/Role.cs:136-150` and `:188-205`; all ten SoD sites in §4.5.
- **Description:** Every implemented SoD rule compares *actor identity to record identity* at the moment of the act. Nothing prevents a single role from holding both sides of a duty pair — e.g. `documents.create` together with `documents.sign`, or `nc.create` with `nc.approve`. The seeded Tenant Administrator holds all 170 keys, so it holds every conflicting pair by construction.
- **Impact:** ISO 17025 §6.2 and Part 11 §11.10(g) expect the *organisation* to segregate incompatible duties, not merely to stop one person acting twice on one record. A laboratory can compose a role that a regulator would reject, and the system will not warn. In a small laboratory with one active user, the record-level rules are the *only* control and they are defeated whenever a second person is available.
- **Testing limitation:** No case can assert that a conflicting grant is refused, because none is. Any such case is `[GD]` on this gap.
- **Recommended clarification:** Define the incompatible key pairs (at minimum create↔approve and create↔sign per module) and decide whether holding both should be refused, warned, or merely reported in the access review.
- **Suggested acceptance criteria:** Saving a role holding a defined incompatible pair either raises a new domain code or is recorded as an exception requiring justification, and the access-review export lists every role holding one.
- **Severity:** High. **Responsible role:** Quality Manager + Solution Architect.

---

**GAP-RBAC-009 — `EnsureSignerIsNotPreparer` is a no-op when the preparer is unknown**
- **Source reference:** `src/NT.QAMS.SharedKernel/Primitives/AggregateRoot.cs:36-42`; conventions §2 line 87 (accepted residual F-05b).
- **Description:** The guard is `if (CreatedByUserId is { } preparer && preparer == signerId)`. When `CreatedByUserId` is null the check passes silently. It backs `SOD-AQ-001` at 14 analytical sign-off/approval sites and `SOD-QP-001` on the quality policy — 15 of the regulated signing gates.
- **Impact:** Any record whose creator is unrecorded — legacy rows, system-created rows, migration backfills, records created by a background service — can be signed off by whoever created it, with no refusal and nothing distinguishable in the audit trail.
- **Testing limitation:** The negative case ("a preparer with a null `CreatedByUserId` is refused") **cannot pass** and must not be written as an executable case. The correct case is the positive one: a null-preparer record *is* signable, labelled `[ID]`, with a note that this is accepted residual F-05b.
- **Recommended clarification:** Confirm the acceptance is still current at v1.51.2 and quantify it: how many rows in the 15 affected tables have a null `CreatedByUserId` today?
- **Suggested acceptance criteria:** Either `CreatedByUserId` is non-null on every new regulated record (enforced by a CHECK constraint or an interceptor), or the residual risk is re-signed with a current row count.
- **Severity:** High. **Responsible role:** Quality Manager (risk acceptance) + Lead Developer.

---

**GAP-RBAC-010 — `AUTHZ-001` carries two unrelated meanings**
- **Source reference:** `src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs:60`; `src/NT.QAMS.Domain/Competency/TestAuthorization.cs:48`.
- **Description:** `AUTHZ-001` means "an authenticated actor is required for this action" in the authorization behaviour, and "the authorization expiry must fall after the grant date" in the `TestAuthorization` aggregate. Both surface as 403 because of the prefix rule at `DomainExceptionHandler.cs:63-68`. More broadly, the `test-authorizations` module reuses the `AUTHZ-` prefix for ten business-rule codes that are not authorization refusals.
- **Impact:** A client cannot distinguish "you are not signed in" from "your date range is invalid" by code. A date-validation error is reported as 403 Forbidden, which is the wrong semantic class and will mislead any consumer, log analysis or alert rule keyed on 403 rates.
- **Testing limitation:** A case asserting `AUTHZ-001` must state which of the two it means and pin the exact endpoint; the code alone is not a unique assertion.
- **Recommended clarification:** Rename the `TestAuthorization` family to a non-colliding prefix (e.g. `TAUTH-*`) so the `AUTHZ-` prefix means "authorization refusal" exclusively.
- **Suggested acceptance criteria:** Every `AUTHZ-`-prefixed code is an authorization refusal returning 403; no two codes in the system share a string with different meanings, asserted by a test over the code registry.
- **Severity:** Medium. **Responsible role:** Lead Developer.

---

**GAP-RBAC-011 — Class-level and method-level permission gates compose as AND, undocumented**
- **Source reference:** `src/NT.QAMS.WebApi/Authorization/RequirePermissionAttribute.cs:26` (`AllowMultiple = true`); `ComplianceController.cs:20` ∧ `:46` ∧ `:52`; `AccessReviewsController.cs:20`; `TenantSettingsController.cs:18`.
- **Description:** Three controllers carry a class-level gate. Because each attribute is an independent authorization filter, a method-level gate is **additional**, not a replacement. `POST /api/compliance/audit-trail-reviews` therefore requires `compliance.view` **and** `compliance.create`. No XML documentation, requirement or test states this composition rule. The converse case is also undocumented: `POST /api/access-reviews` and `POST /api/access-reviews/{id}/complete` are **write** operations gated only by `access-reviews.view`, so `access-reviews.create/edit/approve/void/sign` (five keys) are inert for this controller.
- **Impact:** A privilege configuration built from the module matrix alone will be wrong in both directions — a role granted only `compliance.create` cannot open a review, and a role granted only `access-reviews.view` can complete one, which is a recertification record.
- **Testing limitation:** Every case touching these three controllers must assert the composed requirement, not the method-level key. Getting this wrong produces a case that fails for the wrong reason.
- **Recommended clarification:** Document the AND composition, and decide whether `POST /api/access-reviews/{id}/complete` should require `access-reviews.approve` rather than `view`.
- **Suggested acceptance criteria:** Each of the three controllers has an explicit requirement stating its effective key set per route, and the access-review completion route requires a non-`view` key.
- **Severity:** High (the access-review write-on-view gate is a Part 11 §11.10(d) concern). **Responsible role:** Solution Architect + Quality Manager.

---

**GAP-RBAC-012 — The tier-change route bypasses the last-administrator guard**
- **Source reference:** `src/NT.QAMS.Application/IdentityAccess/Commands/UserManagement.cs:115-128` (`ChangeUserRoleHandler`) vs `:218-227` (`AssignUserRoleHandler`); `RolesSlice.cs:28-52`; URS-098 (`06-Revalidation-Delta-v1.38-v1.50.md:157`).
- **Description:** `ChangeUserRoleHandler` loads the user, changes the tier, then calls `SeededRoleDefault.AssignAsync`, which reassigns `RoleId` to the seeded role for the new tier. It **never calls `ManageRolesLockoutGuard.EnsureSurvivesAsync`**. The sibling `AssignUserRoleHandler` does. `POST /api/users/{id}/role` with `role: "Analyst"` applied to the sole `roles.manage` holder therefore succeeds and leaves the tenant with no privilege administrator.
- **Impact:** Defeats INV-8 and URS-098 — "*No sequence of role edits, deactivations or reassignments shall leave a tenant without an active user able to administer privileges*". Recovery requires a support intervention, exactly the outcome `ROLE-006` exists to prevent. Only a `users.manage` holder can do it, but a Tenant Administrator holds both `users.manage` and `roles.manage` by seed, so self-lockout is one click away.
- **Testing limitation:** A case asserting `ROLE-006` on this route **will fail** against the current build. Author it as a negative-expectation case labelled `[GD]`, or as an `[ID]` case documenting the bypass, and state which was chosen.
- **Recommended clarification:** Confirm whether the omission is deliberate. It reads as an oversight given the sibling handler's explicit guard.
- **Suggested acceptance criteria:** `POST /api/users/{id}/role` on the last active `roles.manage` holder returns `422 ROLE-006` and leaves both tier and `role_id` unchanged.
- **Severity:** **Critical** — a documented invariant and an approved requirement are both defeated. **Responsible role:** Lead Developer + Validation Lead.

---

**GAP-RBAC-013 — Deactivating the last privilege administrator bypasses the same guard**
- **Source reference:** `src/NT.QAMS.Application/IdentityAccess/Commands/UserManagement.cs:135-143` (`SetUserActiveHandler`); guard call sites are only `RolesSlice.cs:165`, `:195` and `UserManagement.cs:226`.
- **Description:** `SetUserActiveHandler` calls `user.Deactivate()` with no lockout check. `ManageRolesLockoutGuard` counts only users with `IsActive == true` (`RolesSlice.cs:39-44`), so deactivating the sole active `roles.manage` holder reduces the survivor count to zero.
- **Impact:** Same outcome as GAP-RBAC-012 by a different route. Also the more likely accident: deactivating a departing administrator is routine housekeeping, whereas a tier change is deliberate.
- **Testing limitation:** As GAP-RBAC-012 — the `ROLE-006` assertion fails today.
- **Recommended clarification:** Same decision as GAP-RBAC-012; the two should be fixed together with a single shared pre-check in the user-administration slice.
- **Suggested acceptance criteria:** `POST /api/users/{id}/deactivate` on the last active `roles.manage` holder returns `422 ROLE-006` and the account stays active.
- **Severity:** **Critical**. **Responsible role:** Lead Developer + Validation Lead.

---

**GAP-RBAC-014 — Reason for change is required for only one of the five privilege-changing operations**
- **Source reference:** `SetRolePermissionsCommand` reason (`RolesSlice.cs:143`, validator `:150`, event field `Role.cs:225`) vs `CreateRoleCommand` (`RolesSlice.cs:67-69`), `SetRoleActiveCommand` (`:177`), `AssignUserRoleCommand` (`UserManagement.cs:200`), `SetUserScopeCommand` (`UserManagement.cs:239-241`); `ChangeReasonMiddleware` at `RequestIdentity.cs` (DELETE only).
- **Description:** Only editing an existing role's grants carries a mandatory reason. Creating a role **with** grants achieves the same privilege outcome with no reason. Deactivating a role (which strips every holder), assigning a user to a different role, and widening a user's working scope to unrestricted all record no reason. `ChangeReasonMiddleware` only demands `X-Change-Reason` on DELETE, and none of these routes is a DELETE.
- **Impact:** 21 CFR Part 11 §11.10(e) expects the reason for a change to a controlled record to be captured. A change to who-may-do-what is such a record (the codebase says so itself at `Role.cs:29-32`). Four of five paths lose it. `UserScopeChanged` even flags `IsUnrestricted` — the widest possible grant — with no justification attached.
- **Testing limitation:** Cases asserting a recorded reason can only be written for `PUT /api/roles/{id}/permissions`. For the other four, an audit-completeness case must assert the **absence** of a reason, labelled `[ID]`.
- **Recommended clarification:** Decide which privilege-changing operations require a reason. At minimum: role deactivation, role assignment, and any scope change that widens access.
- **Suggested acceptance criteria:** Every operation that changes an effective privilege set records an operator-supplied reason on its ledger row, verified by an audit-trail assertion per operation.
- **Severity:** High (Part 11 §11.10(e)). **Responsible role:** Quality Manager + Lead Developer.

---

**GAP-RBAC-015 — Roles cannot be deleted, only deactivated; no requirement states this**
- **Source reference:** `src/NT.QAMS.WebApi/Controllers/RolesController.cs` (no `[HttpDelete]`); `src/NT.QAMS.Domain/Authorization/Role.cs:156-182`.
- **Description:** There is no delete operation at any layer. A mistakenly created role remains in the list forever, deactivated. The design rationale is sound (historical records reference roles by name — `Role.cs:21-26`) but it is stated only for **system** roles; the same restriction silently applies to tenant-defined roles.
- **Impact:** The roles list accumulates permanently. A user-access review must reason about roles that will never be used again. No requirement records the decision, so a reviewer cannot tell whether delete is missing or withheld.
- **Testing limitation:** No delete case can be written. A case asserting "a role can be removed" must not be authored.
- **Recommended clarification:** Record the no-delete decision as a requirement, and decide whether inactive roles should be hidden from the default list view.
- **Suggested acceptance criteria:** A requirement states that roles are deactivated and never deleted, with the retention rationale; the roles list defaults to active roles with an explicit control to reveal inactive ones.
- **Severity:** Low. **Responsible role:** Product Owner.

---

**GAP-RBAC-016 — The `RolePrivilegeModule` migration comment now contradicts the schema**
- **Source reference:** `src/NT.QAMS.Infrastructure/Persistence/Migrations/20260730112800_RolePrivilegeModule.cs:127-131` vs `20260731201114_Hardening4_ChildTenancy.cs:50-60, 311-320, 654-661, 681-698`.
- **Description:** The migration comment states that `role_permission`, `user_branch_access` and `user_department_access` "carry no `tenant_id`" and are protected only through their RLS-protected parent. `Hardening4_ChildTenancy` subsequently added `tenant_id` to all three, backfilled it from the parent, and gave each `ENABLE`/`FORCE` row-level security with a `tenant_isolation` policy. The comment is now false.
- **Impact:** Migrations are validation evidence. A reviewer reading the RLS rationale in the migration history is told three tables are unprotected when they are protected. It also risks a future developer replicating the retired "children need no tenant_id" pattern.
- **Testing limitation:** None functional — the tables *are* protected, so the positive isolation cases (TC-RBAC-RLS-*) are valid. It is an evidence-integrity defect.
- **Recommended clarification:** Superseding migrations cannot be edited retroactively without invalidating the applied history; record the correction in the migration-history documentation and in `CLAUDE.md` §5 instead.
- **Suggested acceptance criteria:** The migration-history document records that `Hardening4_ChildTenancy` supersedes the `RolePrivilegeModule` child-tenancy comment, and `CLAUDE.md` §5 states the current rule (owned children carry `tenant_id` and their own RLS).
- **Severity:** Low. **Responsible role:** Solution Architect.

---

**GAP-RBAC-017 — Audit sign-off carries no segregation-of-duties guard**
- **Source reference:** `src/NT.QAMS.WebApi/Controllers/AuditsController.cs:69-75` (`POST /api/audits/{id}/sign-off`, gated `audits.sign`); `src/NT.QAMS.Domain/AuditManagement/Audit.cs:172-192` (`SignOff`).
- **Description:** `Audit.SignOff(actorId, at)` guards state (`AUD-019` via `RequireInProgress`), unanswered checklist items (`AUD-017`) and unlinked NC-graded findings (`AUD-018`), then sets `SignedOffBy = actorId` — with **no comparison of `actorId` to the preparer**. `EnsureSignerIsNotPreparer` is called from exactly 15 sites (14 analytical aggregates + `QualityPolicy.cs:78`); `Audit.cs` is not one of them, and no `SOD-*` literal appears anywhere in the file. Every other signed record family has a guard — documents, competencies, suppliers, conflicts, test authorizations and nonconformances use bespoke checks. The auditor who created and conducted the audit may sign it off.
- **Impact:** ISO 17025 §8.8.2 and ISO 19011 require internal auditors not to audit their own work; ISO 9001 §9.2.2(c) requires objectivity and impartiality of the audit process. The self-sign-off path is exactly the case those clauses address, and it is the one signed record family with no control.
- **Testing limitation:** A case asserting an SoD refusal on `POST /api/audits/{id}/sign-off` **will fail**. It must be authored as `[GD]` on this gap, or as an `[ID]` case recording that self-sign-off currently succeeds — state which.
- **Recommended clarification:** Confirm whether audit sign-off is intended to be exempt. If a lead auditor legitimately signs off an audit they planned but did not conduct, the rule needs a different comparison field than `CreatedByUserId`.
- **Suggested acceptance criteria:** `POST /api/audits/{id}/sign-off` by the audit's preparer returns `422` with a new `SOD-AUD-001` code, or a requirement records the documented exemption with its justification.
- **Severity:** High. **Responsible role:** Quality Manager + Lead Developer.

---

*End of `11-module-rbac.md`. Detailed cases: `11-module-rbac-cases-A.md` … `-F.md`, against the reserved id blocks in the table at the top of this file.*
