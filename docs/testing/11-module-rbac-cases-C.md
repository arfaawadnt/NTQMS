# RBAC — Detailed Test Cases, Batch C

This batch consumes `TC-RBAC-API-060` … `TC-RBAC-API-101` (42 cases) and covers exactly one slice of module RBAC: HTTP permission enforcement for the **Analytical** group (`analytical-quality`, `proficiency-testing`), the **Operations** group (`tasks`, `notifications`, `reports`) and the **Administration** group (`organization`, `tenant-settings`, `roles`); the eight `RolesController` role/permission-management routes and their `ROLE-005` / `ROLE-006` refusals; the immediacy of a grant and a revoke on the affected user's *next* request against the *same unexpired access token* (URS-096); the interaction between a tier change and `ActiveSessionMiddleware`'s `AUTH-007` check versus a configurable-role change that raises no such challenge; what "permission cache invalidation" actually means in this build (there is no server-side cache and no permission claim in the JWT — `SecurityAdapters.cs:84-96`, `PrivilegeResolution.cs:52-60`) plus the one place a stale snapshot does exist, the SPA's `PermissionsService` signal; and the single surviving `[Authorize(Roles = Roles.PlatformAdmin)]` platform gate on `TenantsController.cs:12`. Deliberately left to sibling batches: the `PermissionCatalog` / `Role` aggregate domain units and the role lifecycle state machine (batch A); the Quality, Documents, Risk, Resources and People endpoint gates and the class-level ∧ method-level composition on `ComplianceController` / `AccessReviewsController` (batches B and C's DT block); per-request resolution internals, RLS on `qams.role*`, and branch/department working scope (batch D); the ten `SOD-*` duty-pair codes and the `ROLE-006` boundary matrix (batch E); the frontend components, route guards, e2e journeys and accessibility (batch F). Risk IDs are minted `RSK-RBAC-9xx` — `docs/validation/02-Functional-Risk-Assessment.md` carries no RBAC-privilege entries at this granularity, so these are new and are declared as such. Shared fixture for the whole batch (created once, torn down per case): tenant `demo-lab`; `admin@demo-lab.local` on seeded role **Tenant Administrator** (tier `TenantAdmin`); `qm@demo-lab.local` on **Quality Manager** (tier `QualityManager`); `dh@demo-lab.local` on **Department Head** (tier `DepartmentHead`); `analyst@demo-lab.local` on **Analyst** (tier `Analyst`); `auditor@demo-lab.local` on **External Auditor** (tier `ExternalAuditor`); every fixture password `Fixture-Pass-2026!`; platform account `platform-admin@localhost`. Tier and seeded role are always kept in step, because `SeededRoleDefault.AssignAsync` follows the tier (`UserManagement.cs:122`) and a mismatch would fire `AUTH-007` before any permission gate is reached.

---

#### TC-RBAC-API-060 — `analytical-quality.manage` admits the Quality Manager to QC profile creation  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-901 |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — the "holder of the gated key" partition of DT-1 row 3 |
| **Priority / Severity / Automation** | High · High · Yes (functional, `WebApi.FunctionalTests`) |
| **Role / Permission / Tenant** | Quality Manager · `analytical-quality.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qm@demo-lab.local` is active and holds the seeded **Quality Manager** role, which grants every key except `users.*`, `tenant-settings.*`, `roles.manage` and `organization.manage` (`SystemRoleCatalog.cs:107-117`), so `analytical-quality.manage` is present. Gate site `AnalyticalQualityControllers.cs:21`. |
| **Test Data** | `POST /api/qc/profiles` body `{"analyte":"Glucose","instrument":"Cobas-c501","controlLot":"L2-2026-08","targetMean":5.40,"targetSd":0.18}` |
| **Steps** | 1. `POST /api/auth/login` as `qm@demo-lab.local` / `Fixture-Pass-2026!`; keep the access JWT. 2. `POST /api/qc/profiles` with the body above and `Authorization: Bearer <jwt>`. 3. Read status and body. 4. `SELECT permission_key FROM qams.role_permission rp JOIN qams.role r ON r.id=rp.role_id AND r.tenant_id=rp.tenant_id WHERE r.normalized_name='QUALITY MANAGER' AND rp.permission_key='analytical-quality.manage';`. |
| **Expected UI** | The QC configuration screen saves the profile and lists it; no permission banner is shown. |
| **Expected API** | `200 OK`, `application/json`, body `{"id":"<uuid>"}` — the action returns `Ok(new { id = … })`, not `201` (`AnalyticalQualityControllers.cs:22-25`). |
| **Expected DB** | One new row in `qams.qc_profile` with `tenant_id` = demo-lab's id; step 4 returns exactly one row (`analytical-quality.manage`). |
| **Expected Audit** | One `qams.outbox_event` row whose `event_type` is the assembly-qualified domain-event name written by `OutboxInterceptor.cs:67`, forwarded verbatim to `audit.audit_trail.event_type` by `OutboxProcessor.cs:126-127`. Assert the exact type string against the QC profile aggregate's creation event before execution — the event name was not read in this pass. |
| **Expected Notification** | n/a — no `NotificationPolicies` rule is defined for QC profile creation. |
| **Cleanup** | `DELETE FROM qams.qc_profile WHERE analyte='Glucose' AND control_lot='L2-2026-08';` with `SELECT set_config('app.bypass_rls','on',false);` first. |
| **Evidence** | HTTP response capture · SQL result of step 4 · `qams.qc_profile` row |
| **Result / Defect** | Not Run · — |
| **Notes** | Positive half of the pair with TC-RBAC-API-061. `Has()` short-circuits true for platform admins (`PrivilegeResolution.cs:39`), so this case must not be run as `platform-admin@localhost`. |

---

#### TC-RBAC-API-061 — `analytical-quality.manage` refuses the Analyst on QC target revision  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-901 |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning — the "authenticated, active role, key absent" partition (DT-1 row 4) |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · `analytical-quality.manage` (**not held**) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | The seeded **Analyst** role grants `analytical-quality` only `view` and `export` (`SystemRoleCatalog.cs:174`). An existing QC profile id `P1` created by TC-RBAC-API-060. Gate site `AnalyticalQualityControllers.cs:28`. |
| **Test Data** | `PUT /api/qc/profiles/{P1}/targets` body `{"targetMean":5.55,"targetSd":0.20,"reason":"Lot change 2026-08"}` |
| **Steps** | 1. Login as `analyst@demo-lab.local` / `Fixture-Pass-2026!`. 2. `PUT /api/qc/profiles/{P1}/targets` with the body above. 3. Read status, `Content-Type` and the `code` member of the body. 4. `SELECT target_mean, target_sd FROM qams.qc_profile WHERE id='{P1}';`. |
| **Expected UI** | The "Edit targets" control is not rendered — `can('analytical-quality.manage')` is false (`permissions.service.ts:67-70`); a direct call from the console shows the shared 403 toast. |
| **Expected API** | `403 Forbidden`, `application/problem+json`, `code` = `AUTHZ-403`, `title` = `You do not have permission to perform this action.` (`RequirePermissionAttribute.cs:54-59`, constant at `ProblemAuthorizationResultHandler.cs:16`). |
| **Expected DB** | Step 4 returns `target_mean = 5.40`, `target_sd = 0.18` — unchanged. No row in `qams.qc_target_change` (or the aggregate's target-history table) for this attempt. |
| **Expected Audit** | No `qams.outbox_event` row and no `audit.audit_trail` row — the filter short-circuits before MediatR is reached, so no domain event exists. |
| **Expected Notification** | n/a — a refused request raises no notification. |
| **Cleanup** | None — the request is refused before any write. |
| **Evidence** | HTTP response capture (headers + body) · SQL result of step 4 |
| **Result / Defect** | Not Run · — |
| **Notes** | `QC-012` (reason required) and `QC-013` (forward-only) are domain rules on the same route; they must **not** appear here, because the gate refuses before the handler runs. If a `QC-01x` code is observed instead of `AUTHZ-403`, the filter ordering has regressed. |

---

#### TC-RBAC-API-062 — `analytical-quality.create` admits the Department Head to validation-study configuration  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-901 |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — DT-1 row 3, applied to the create action of the 13 analytical create sites |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `analytical-quality.create` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Seeded **Department Head** grants `analytical-quality` = `view, create, edit, export` (`SystemRoleCatalog.cs:144`). Gate site `AnalyticalQualityControllers.cs:65`. |
| **Test Data** | `POST /api/validation-studies` body `{"analyte":"Sodium","protocol":"CLSI EP15-A3","totalAllowableError":4.0}` |
| **Steps** | 1. Login as `dh@demo-lab.local`. 2. `POST /api/validation-studies` with the body above. 3. Read the status and the `Location` header. 4. `GET /api/validation-studies/{id}` with the same token. 5. `SELECT state, created_by_user_id FROM qams.validation_study WHERE id='{id}';`. |
| **Expected UI** | The study appears in the register with state `Draft`; the "Sign off" control is absent for this role. |
| **Expected API** | Step 2 → `201 Created`, `Location: /api/validation-studies/{id}`, body `{"id":"<uuid>"}` (`CreatedAtAction`, `AnalyticalQualityControllers.cs:70`). Step 4 → `200 OK` (the GET carries no `[RequirePermission]`, `:60-62`). |
| **Expected DB** | One `qams.validation_study` row, `created_by_user_id` = the Department Head's user id, `tenant_id` = demo-lab. |
| **Expected Audit** | One `qams.outbox_event` row, `tenant_id` = demo-lab, forwarded to `audit.audit_trail` by `OutboxProcessor.cs:126-127` with `sequence` = previous + 1 and `prev_hash` equal to the prior row's `entry_hash`. |
| **Expected Notification** | n/a — study configuration defines no notification rule. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.validation_study WHERE id='{id}';` |
| **Evidence** | HTTP capture of steps 2 and 4 · `qams.validation_study` row · `audit.audit_trail` tail |
| **Result / Defect** | Not Run · — |
| **Notes** | `created_by_user_id` must be non-null here; it is the field `EnsureSignerIsNotPreparer` compares against in TC-RBAC-API-063's sibling SoD case (batch E) and the null case is GAP-RBAC-009. |

---

#### TC-RBAC-API-063 — `analytical-quality.sign` refuses the Department Head at study sign-off  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-902 |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — DT-1 row 4 on the highest-consequence analytical action |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `analytical-quality.sign` (**not held**) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Study `S1` from TC-RBAC-API-062 exists, calculated and awaiting sign-off. Department Head holds `create`/`edit` but not `sign` (`SystemRoleCatalog.cs:144`). Gate site `AnalyticalQualityControllers.cs:88`. |
| **Test Data** | `POST /api/validation-studies/{S1}/sign-off` with an empty body |
| **Steps** | 1. Login as `dh@demo-lab.local`. 2. `POST /api/validation-studies/{S1}/sign-off`. 3. Read status, `Content-Type`, `code`. 4. `SELECT state, signed_off_by, signed_off_at_utc FROM qams.validation_study WHERE id='{S1}';`. 5. `SELECT count(*) FROM audit.security_event WHERE event_type='ESIGN_FAILED' AND occurred_at_utc > now() - interval '2 minutes';` after `SELECT set_config('app.bypass_rls','on',false);`. |
| **Expected UI** | No "Sign off" button is rendered for this role; a forced call surfaces the standard 403 problem toast. |
| **Expected API** | `403 Forbidden`, `application/problem+json`, `code` = `AUTHZ-403`. **Not** `422 SOD-AQ-001` — the HTTP gate refuses before the aggregate is loaded. |
| **Expected DB** | Step 4 returns the pre-call `state` unchanged and `signed_off_by IS NULL`. |
| **Expected Audit** | Step 5 returns `0` — a permission refusal is not a signing attempt and must not consume a `RegisterFailedLogin` slot or write `ESIGN_FAILED`. No `audit.audit_trail` row. |
| **Expected Notification** | n/a — no notification on a refused sign-off. |
| **Cleanup** | None — nothing was written. |
| **Evidence** | HTTP capture · SQL results of steps 4 and 5 |
| **Result / Defect** | Not Run · — |
| **Notes** | Guards the ordering property that a 403 costs the user nothing. If `ESIGN_FAILED` appears, the e-signature ceremony is being entered before the permission gate, which would make privilege probing a lockout vector. |

---

#### TC-RBAC-API-064 — `analytical-quality.edit` gates uncertainty-budget component entry across four seeded roles  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-901 |
| **Level / Type / Technique** | API · Functional (matrix) · Pairwise — 4 roles × 1 gated action, each role appearing once against its seeded grant state |
| **Priority / Severity / Automation** | High · High · Yes (functional, table-driven) |
| **Role / Permission / Tenant** | Quality Manager, Department Head, Analyst, External Auditor · `analytical-quality.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Uncertainty budget `U1` exists in an editable state. Seeded grants: QM holds `edit` (`SystemRoleCatalog.cs:113` catch-all), DH holds `edit` (`:144`), Analyst holds only `view`+`export` (`:174`), External Auditor holds only read actions on non-administration modules (`:193`). Gate site `UncertaintyController.cs:36`. |
| **Test Data** | `POST /api/uncertainty-budgets/{U1}/components` body `{"name":"Repeatability","type":"A","relativeStandardUncertainty":0.012,"source":"EP15 replicate set"}` |
| **Steps** | 1. For each of the four accounts in turn: login, `POST` the body above, record status + `code`. 2. After all four, `SELECT count(*) FROM qams.uncertainty_component WHERE budget_id='{U1}' AND name='Repeatability';`. |
| **Expected UI** | The "Add component" control renders for QM and DH only; the Analyst and Auditor screens render the component table read-only. |
| **Expected API** | QM → `200 OK` body `{"componentId":"<uuid>"}` (`UncertaintyController.cs:37-39`). DH → `200 OK` `{"componentId":"<uuid>"}`. Analyst → `403` `AUTHZ-403`. External Auditor → `403` `AUTHZ-403`. |
| **Expected DB** | Step 2 returns `2` — one component per successful call, none from the two refusals. |
| **Expected Audit** | Two `audit.audit_trail` rows (QM, DH), each with `tenant_id` = demo-lab; none for the Analyst or Auditor attempts. |
| **Expected Notification** | n/a — component entry defines no notification rule. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.uncertainty_component WHERE budget_id='{U1}' AND name='Repeatability';` |
| **Evidence** | Four HTTP captures · SQL count · audit-trail tail |
| **Result / Defect** | Not Run · — |
| **Notes** | The Auditor refusal is doubly determined: the HTTP gate refuses on the missing key, and `AuthorizationBehavior` would refuse `AUTHZ-002` on `RequireInternalActor` if the gate were removed. Only the 403 `AUTHZ-403` is observable at HTTP. |

---

#### TC-RBAC-API-065 — `analytical-quality.approve` gate is evaluated before the `SOD-AQ-001` duty check  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-902 |
| **Level / Type / Technique** | API · Functional (ordering) · Multiple-Condition — (key held) × (actor is preparer), asserting which condition surfaces |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst then Quality Manager · `analytical-quality.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Uncertainty budget `U2` with `created_by_user_id` = the **Quality Manager's** user id and `status` awaiting approval. Analyst holds no `approve` key. Gate site `UncertaintyController.cs:58`; duty rule `UncertaintyBudget.cs:166` via `AggregateRoot.cs:36-42`. |
| **Test Data** | `POST /api/uncertainty-budgets/{U2}/approve`, empty body |
| **Steps** | 1. Login as `analyst@demo-lab.local`; `POST /api/uncertainty-budgets/{U2}/approve`; record status + `code`. 2. Login as `qm@demo-lab.local` (the preparer, who **does** hold `approve`); `POST` the same route; record status + `code`. 3. `SELECT status, approved_by FROM qams.uncertainty_budget WHERE id='{U2}';`. |
| **Expected UI** | Analyst: no approve control. QM: the approve control renders, and the request returns a duty-of-segregation error dialog rather than a permission error. |
| **Expected API** | Step 1 → `403` `application/problem+json` `code` = `AUTHZ-403`. Step 2 → `422 Unprocessable Entity` `application/problem+json` `code` = `SOD-AQ-001` (default `DomainException` arm, `DomainExceptionHandler.cs:75-80`). |
| **Expected DB** | Step 3 shows `status` unchanged and `approved_by IS NULL` after both attempts. |
| **Expected Audit** | No `audit.audit_trail` row from either attempt — neither reaches a successful `SaveChangesAsync`. |
| **Expected Notification** | n/a — refusals raise no notification. |
| **Cleanup** | None — no state changed. |
| **Evidence** | Two HTTP captures · SQL result of step 3 |
| **Result / Defect** | Not Run · — |
| **Notes** | The pairing is the point: the same route yields two different classes (403 access-control vs 422 business rule) for two different reasons, which GAP-RBAC-002 says an SOP keyed on a single `SOD_VIOLATION` code would miss entirely. |

---

#### TC-RBAC-API-066 — `X-Change-Reason` (400) is evaluated before the permission gate (403) on a gated DELETE  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-903 |
| **Level / Type / Technique** | API · Functional (ordering) · Multiple-Condition — 2×2 over (header present) × (key held), all four cells asserted |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Analyst and Quality Manager · `analytical-quality.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Uncertainty budget `U1` with component `C1`. Pipeline order is fixed: `ChangeReasonMiddleware` is registered at `Program.cs:269`, **before** `app.UseAuthorization()` at `:270` and before the MVC filter pipeline that runs `RequirePermissionAttribute`. Gate site `UncertaintyController.cs:42`; middleware `RequestIdentity.cs:143-160`. |
| **Test Data** | Route `DELETE /api/uncertainty-budgets/{U1}/components/{C1}`; header value when supplied: `X-Change-Reason: Superseded by the EP15 re-run` |
| **Steps** | 1. QM, header present → record status. 2. QM, header absent → record status + `code`. 3. Analyst, header present → record status + `code`. 4. Analyst, header **absent** → record status + `code`. 5. `SELECT count(*) FROM qams.uncertainty_component WHERE id='{C1}';`. |
| **Expected UI** | The change-reason dialog (`changeReasonInterceptor`) collects the reason before any DELETE leaves the SPA, so cells 2 and 4 are only reachable by a direct API call. |
| **Expected API** | Cell 1 → `204 No Content`. Cell 2 → `400 Bad Request`, `code` = `CHANGE-REASON-REQUIRED` (`RequestIdentity.cs:152-155`). Cell 3 → `403`, `code` = `AUTHZ-403`. **Cell 4 → `400 CHANGE-REASON-REQUIRED`, not `403`** — the middleware fires first. |
| **Expected DB** | Step 5 returns `0` if cell 1 ran first (the component is gone), otherwise `1`; run cell 1 last and assert `1` before it and `0` after. |
| **Expected Audit** | One `audit.field_change` row for cell 1 only, carrying `reason` = `Superseded by the EP15 re-run`, stamped by `FieldChangeInterceptor` in the same transaction. Cells 2–4 write nothing. |
| **Expected Notification** | n/a — component removal defines no notification rule. |
| **Cleanup** | Re-add `C1` via `POST /api/uncertainty-budgets/{U1}/components` as QM after cell 1. |
| **Evidence** | Four HTTP captures · `audit.field_change` row for cell 1 · SQL counts before and after |
| **Result / Defect** | Not Run · — |
| **Notes** | Cell 4 is the finding-bearing cell: an unprivileged caller learns the route exists and is a DELETE before authorization is consulted. Recorded as **GAP-RBAC-907** in the coverage note. |

---

#### TC-RBAC-API-067 — `analytical-quality.void`, `.view` and `.export` are inert: no endpoint observes them  [GD — GAP-RBAC-003]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-904 |
| **Level / Type / Technique** | API · Negative-capability (gap-dependent) · Equivalence Partitioning — the "granted key with no enforcement point" partition |
| **Priority / Severity / Automation** | High · High · No — cannot be automated until GAP-RBAC-003 defines the enforcement point |
| **Role / Permission / Tenant** | Bespoke role `AQ-Void-Only` · `analytical-quality.void` (+ `.view`, `.export` in the variants) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | A tenant-defined role `AQ-Void-Only` holding **exactly** `["analytical-quality.void"]` and nothing else, assigned to a fixture user `aqvoid@demo-lab.local` on tier `Analyst`. Measured: `analytical-quality` has 8 actions (`PermissionCatalog.cs:171-173`) but only `create` (13 sites), `edit` (4 sites), `approve` (1 site), `sign` (12 sites) and `manage` (2 sites) appear at a `[RequirePermission]`. `void`, `view` and `export` appear at none. |
| **Test Data** | Every route on `/api/qc`, `/api/validation-studies`, `/api/uncertainty-budgets`, `/api/sigma-assessments`, `/api/carryover-studies` and the remaining nine study families |
| **Steps** | 1. Create the role and user. 2. Drive every route in the six controller families with the fixture token, recording status per route. 3. Repeat the whole sweep with the same user assigned a role holding **zero** keys. 4. Diff the two status vectors. |
| **Expected UI** | The privilege matrix renders "Analytical Quality — Void" as a grantable switch indistinguishable from the enforced ones (`GetPermissionCatalogHandler`, `RolesSlice.cs:213-219` emits all 8 actions). |
| **Expected API** | **The two status vectors in steps 2 and 3 are identical.** No route's outcome changes when `analytical-quality.void` is granted or withheld. |
| **Expected DB** | `SELECT permission_key FROM qams.role_permission WHERE permission_key IN ('analytical-quality.void','analytical-quality.view','analytical-quality.export')` returns the granted rows — the keys **are** stored (`ROLE-005` accepts them, they are in `AllKeys`), they simply govern nothing. |
| **Expected Audit** | `RolePermissionsChanged` records the grant with its reason, so the audit trail asserts a privilege the system does not enforce — the §11.10(d) recertification defect GAP-RBAC-003 describes. |
| **Expected Notification** | n/a — no notification is defined for a privilege grant. |
| **Cleanup** | `POST /api/roles/{AQ-Void-Only}/deactivate` as `admin@demo-lab.local` (roles are never deleted — GAP-RBAC-015). |
| **Evidence** | Two status vectors · their diff · `qams.role_permission` rows |
| **Result / Defect** | Not Run · — |
| **Notes** | **Acceptance criteria for the gap fix:** either (a) an architecture test asserts every member of `PermissionCatalog.AllKeys` appears in at least one `[RequirePermission]` or `[RequirePermissionPolicy]` declaration and fails CI otherwise, or (b) `Void`, `View` and `Export` are removed from the `analytical-quality` action array at `PermissionCatalog.cs:171-173` so the matrix stops offering them. Once (a) lands, this case is rewritten as a positive gate case. |

---

#### TC-RBAC-API-068 — A zero-key role still reads the analytical registers  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · no URS covers it — trace to `AnalyticalQualityControllers.cs:56-62` · RSK-RBAC-904 |
| **Level / Type / Technique** | API · Functional (implementation-derived) · Equivalence Partitioning — the "authenticated, no keys, ungated read" partition |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Bespoke role `Zero-Keys` · none · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | A tenant-defined role `Zero-Keys` holding `[]`, assigned to `zerokeys@demo-lab.local` on tier `Analyst`. The GET actions at `AnalyticalQualityControllers.cs:56, 60`, `UncertaintyController.cs:17, 21`, `PtPlansController.cs:17, 21` carry class-level `[Authorize]` only — no `[RequirePermission]`. |
| **Test Data** | `GET /api/validation-studies`, `GET /api/uncertainty-budgets`, `GET /api/pt-plans`, `GET /api/qc/profiles` |
| **Steps** | 1. Login as `zerokeys@demo-lab.local`. 2. Issue each of the four GETs. 3. Record status and item counts. 4. `GET /api/auth/me/privileges` and record `permissions`. |
| **Expected UI** | The analytical navigation entries render and the registers populate, even though the user's privilege list is empty. |
| **Expected API** | All four GETs → `200 OK` with the tenant's rows. Step 4 → `200 OK` with `"permissions": []` and `"roleName": "Zero-Keys"`. |
| **Expected DB** | No change. The rows returned are exactly those visible under the tenant filter for an unrestricted working scope. |
| **Expected Audit** | No audit rows — reads are not ledgered. |
| **Expected Notification** | n/a — reads raise no notification. |
| **Cleanup** | `POST /api/roles/{Zero-Keys}/deactivate` as the Tenant Administrator. |
| **Evidence** | Four HTTP captures · the `/auth/me/privileges` body |
| **Result / Defect** | Not Run · — |
| **Notes** | Implementation-derived, not a defect claim: `AuthorizationBehavior` explicitly leaves queries ungated (`AuthorizationBehavior.cs:22-24, 44-47`) and read authorization is the controller's job. The observation is that for the analytical group the controller declines the job. Feeds GAP-RBAC-003. |

---

#### TC-RBAC-API-069 — `proficiency-testing.create` gates PT-plan creation  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-901 |
| **Level / Type / Technique** | API · Functional (positive + negative pair) · Decision Table — DT-1 rows 3 and 4 |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Department Head (holds) vs Analyst (does not) · `proficiency-testing.create` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Seeded DH grants `proficiency-testing` = `view, create, edit, export` (`SystemRoleCatalog.cs:145`); seeded Analyst grants `view, export` only (`:175`). Gate site `PtPlansController.cs:26`. No PT plan exists for year 2027. |
| **Test Data** | `POST /api/pt-plans` body `{"year":2027}` |
| **Steps** | 1. Login as `analyst@demo-lab.local`; `POST /api/pt-plans` `{"year":2027}`; record status + `code`. 2. Login as `dh@demo-lab.local`; `POST /api/pt-plans` `{"year":2027}`; record status + `Location`. 3. `SELECT id, year, status FROM qams.pt_plan WHERE year=2027;`. |
| **Expected UI** | The "New annual PT plan" button is hidden for the Analyst and shown for the Department Head. |
| **Expected API** | Step 1 → `403`, `application/problem+json`, `code` = `AUTHZ-403`. Step 2 → `201 Created`, `Location: /api/pt-plans/{id}`, body `{"id":"<uuid>"}` (`PtPlansController.cs:29-30`). |
| **Expected DB** | Step 3 returns exactly one row, `year = 2027`, `tenant_id` = demo-lab. |
| **Expected Audit** | Exactly one `audit.audit_trail` row for the successful creation; none for the refusal. |
| **Expected Notification** | n/a — PT-plan creation defines no notification rule. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.pt_plan WHERE year=2027;` |
| **Evidence** | Two HTTP captures · SQL result of step 3 |
| **Result / Defect** | Not Run · — |
| **Notes** | The plan created here (`PT1`) is the fixture for TC-RBAC-API-070, 071 and 072; run them in that order before cleanup. |

---

#### TC-RBAC-API-070 — `proficiency-testing.edit` gates both PT-plan item routes, one of which is a DELETE  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-903 |
| **Level / Type / Technique** | API · Functional · Decision Table — one key, two verbs, with the DELETE's header precondition held constant |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Department Head (holds) vs Analyst (does not) · `proficiency-testing.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | PT plan `PT1` (year 2027) from TC-RBAC-API-069 exists in an editable state. Gate sites `PtPlansController.cs:34` (add item) and `:43` (remove item). |
| **Test Data** | Add: `POST /api/pt-plans/{PT1}/items` body `{"scheme":"RIQAS","analyte":"Sodium","provider":"Randox","plannedCycles":12,"notes":"Monthly"}`. Remove: `DELETE /api/pt-plans/{PT1}/items/{itemId}` with `X-Change-Reason: Scheme withdrawn by provider`. |
| **Steps** | 1. DH: `POST` the item; record status and `itemId`. 2. Analyst: `POST` a second item; record status + `code`. 3. Analyst: `DELETE` the DH's item **with** the `X-Change-Reason` header; record status + `code`. 4. DH: `DELETE` the same item with the header; record status. 5. `SELECT count(*) FROM qams.pt_plan_item WHERE plan_id='{PT1}';`. |
| **Expected UI** | Item add/remove controls render for the Department Head only; the Analyst sees the item table read-only. |
| **Expected API** | Step 1 → `200 OK` body `{"itemId":"<uuid>"}` (`PtPlansController.cs:36-40`). Step 2 → `403` `AUTHZ-403`. Step 3 → `403` `AUTHZ-403` (the header is present, so `ChangeReasonMiddleware` passes and the permission filter is the refuser). Step 4 → `204 No Content`. |
| **Expected DB** | Step 5 returns `0`. |
| **Expected Audit** | One `audit.field_change` row for step 4 carrying `reason` = `Scheme withdrawn by provider`; no ledger rows for steps 2 and 3. |
| **Expected Notification** | n/a — PT item maintenance defines no notification rule. |
| **Cleanup** | Covered by step 4 plus TC-RBAC-API-069's cleanup. |
| **Evidence** | Four HTTP captures · `audit.field_change` row · SQL count |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 3 is the counterpart of TC-RBAC-API-066 cell 3: with the header supplied, the DELETE reaches the permission filter and returns 403, which proves the 400-before-403 ordering in that case is caused by the missing header and not by the verb. |

---

#### TC-RBAC-API-071 — `proficiency-testing.approve` gates PT-plan approval and precedes `SOD-AQ-001`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-902 |
| **Level / Type / Technique** | API · Functional (ordering) · Multiple-Condition — (key held) × (actor is the plan's preparer) |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head then Quality Manager · `proficiency-testing.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | PT plan `PT1` created by the **Quality Manager** (so `created_by_user_id` = QM). Seeded DH grants no `approve` on `proficiency-testing` (`SystemRoleCatalog.cs:145`). Gate site `PtPlansController.cs:51`; duty rule `PtPlan.cs:108` via `AggregateRoot.cs:36-42`. |
| **Test Data** | `POST /api/pt-plans/{PT1}/approve`, empty body |
| **Steps** | 1. Recreate `PT1` as `qm@demo-lab.local` so the preparer is the QM. 2. DH: `POST /api/pt-plans/{PT1}/approve`; record status + `code`. 3. QM: `POST` the same route; record status + `code`. 4. `SELECT status, approved_by FROM qams.pt_plan WHERE id='{PT1}';`. |
| **Expected UI** | The Approve control is absent for the DH; for the QM it renders and the request returns a segregation-of-duties dialog. |
| **Expected API** | Step 2 → `403` `AUTHZ-403`. Step 3 → `422` `application/problem+json` `code` = `SOD-AQ-001`. |
| **Expected DB** | Step 4 shows `approved_by IS NULL` and the plan's `status` unchanged. |
| **Expected Audit** | No `audit.audit_trail` row from either attempt. |
| **Expected Notification** | n/a — a refused approval raises no notification. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.pt_plan WHERE year=2027;` |
| **Evidence** | Two HTTP captures · SQL result of step 4 |
| **Result / Defect** | Not Run · — |
| **Notes** | To reach a `204` on this route the tenant needs a second `proficiency-testing.approve` holder who did not create the plan — the organisational consequence of an actor-pair SoD rule, and the reason GAP-RBAC-008 (no toxic-combination control at role-composition time) matters. |

---

#### TC-RBAC-API-072 — `proficiency-testing.void` gates PT-plan closure  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-901 |
| **Level / Type / Technique** | API · Functional (positive + negative pair) · Equivalence Partitioning over the `void` action's holder set |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (holds) vs Department Head (does not) · `proficiency-testing.void` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | PT plan `PT1` exists and is approved. Seeded DH's `proficiency-testing` grant excludes `void` (`SystemRoleCatalog.cs:145`); QM's catch-all includes it (`:113`). Gate site `PtPlansController.cs:66`. |
| **Test Data** | `POST /api/pt-plans/{PT1}/close` body `{"closureSummary":"All 12 cycles submitted; 2 warnings, 0 unacceptable."}` |
| **Steps** | 1. DH: `POST` the close route with the body above; record status + `code`. 2. QM: `POST` the same; record status. 3. `SELECT status, closure_summary FROM qams.pt_plan WHERE id='{PT1}';`. |
| **Expected UI** | The Close control renders for the QM only; the DH sees the plan header without it. |
| **Expected API** | Step 1 → `403` `AUTHZ-403`. Step 2 → `204 No Content` (`PtPlansController.cs:67-71`). |
| **Expected DB** | Step 3 shows the closed state and `closure_summary` equal to the supplied text. |
| **Expected Audit** | One `audit.audit_trail` row for step 2, `tenant_id` = demo-lab, `sequence` = previous + 1. |
| **Expected Notification** | n/a — plan closure defines no notification rule in `NotificationPolicies`. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.pt_plan WHERE year=2027;` |
| **Evidence** | Two HTTP captures · SQL result of step 3 |
| **Result / Defect** | Not Run · — |
| **Notes** | Closure is gated by `void`, not by `approve` — a mapping a configurator reading the module name alone would get wrong. |

---

#### TC-RBAC-API-073 — `proficiency-testing.view` and `.export` are inert  [GD — GAP-RBAC-003]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-904 |
| **Level / Type / Technique** | API · Negative-capability (gap-dependent) · Equivalence Partitioning — ungated-key partition |
| **Priority / Severity / Automation** | Medium · Medium · No — blocked on GAP-RBAC-003 |
| **Role / Permission / Tenant** | Bespoke role `PT-Read-Only` · `proficiency-testing.view`, `.export` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | A tenant-defined role holding exactly `["proficiency-testing.view","proficiency-testing.export"]`, assigned to `ptread@demo-lab.local` on tier `Analyst`. Measured: of the six `proficiency-testing` keys only `create` (`PtPlansController.cs:26`), `edit` (`:34`, `:43`), `approve` (`:51`) and `void` (`:66`) reach a gate; `view` and `export` reach none anywhere in `src/NT.QAMS.WebApi/`. |
| **Test Data** | `GET /api/pt-plans`, `GET /api/pt-plans/{PT1}`, `GET /api/proficiency-tests` |
| **Steps** | 1. Sweep the three GETs with the `PT-Read-Only` token; record statuses. 2. Reassign the user to a zero-key role and repeat. 3. Diff. 4. Search the export surface (`ExportsController.cs`) for any `proficiency-testing.export` gate. |
| **Expected UI** | The privilege matrix offers "Proficiency Testing — View" and "— Export" as switches with no enforcement behind them. |
| **Expected API** | Steps 1 and 2 produce identical `200 OK` vectors. Step 4 finds no gate: `ExportsController.cs` carries three `[RequirePermission]` sites, gating `compliance.export` (`:61`, `:106`) and `reviews.export` (`:126`) only. |
| **Expected DB** | The two keys are stored in `qams.role_permission` — `ROLE-005` accepts them because they are in `AllKeys`. |
| **Expected Audit** | The grant is ledgered by `RolePermissionsChanged` with its reason, asserting a privilege nothing honours. |
| **Expected Notification** | n/a. |
| **Cleanup** | Deactivate `PT-Read-Only`. |
| **Evidence** | Two status vectors · the `ExportsController.cs` gate list |
| **Result / Defect** | Not Run · — |
| **Notes** | **Acceptance criteria:** `GET /api/pt-plans` returns `403 AUTHZ-403` to a role lacking `proficiency-testing.view`, and a PT export route exists gated on `proficiency-testing.export`; or both actions are removed from the module's action set. |

---

#### TC-RBAC-API-074 — `tasks.create` separates the Department Head from the Analyst  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-901 |
| **Level / Type / Technique** | API · Functional (positive + negative pair) · Equivalence Partitioning — adjacent partitions inside one 4-action module |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Department Head (holds `create`) vs Analyst (holds only `view`, `edit`) · `tasks.create` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Seeded DH grants `tasks` = `view, create, edit` (`SystemRoleCatalog.cs:145`); seeded Analyst grants `tasks` = `view, edit` (`:175`) — the single-action difference is the whole point of the case. Gate site `OperationsControllers.cs:102`. |
| **Test Data** | `POST /api/tasks` body `{"subject":"Review PT cycle 08 results","subjectRef":"PT-2027-08","assigneeUserId":"<analyst uuid>","assigneeRole":"Analyst","dueDate":"2027-09-15"}` |
| **Steps** | 1. Analyst: `POST /api/tasks` with the body; record status + `code`. 2. DH: `POST` the same; record status and `id`. 3. `GET /api/tasks/mine` as the Analyst; confirm the created task is listed. 4. `SELECT subject, assignee_user_id FROM qams.work_task WHERE subject_ref='PT-2027-08';`. |
| **Expected UI** | "Create task" is offered on the Department Head's task board and withheld on the Analyst's; both boards list the task once created. |
| **Expected API** | Step 1 → `403` `AUTHZ-403`. Step 2 → `200 OK` body `{"id":"<uuid>"}` (`OperationsControllers.cs:103-106`). Step 3 → `200 OK`, the paged envelope containing the task (route `/api/tasks/mine` carries no `[RequirePermission]`, `:92`). |
| **Expected DB** | Step 4 returns one row with `assignee_user_id` = the Analyst's id. |
| **Expected Audit** | One `audit.audit_trail` row for the created task; none for the Analyst's refused attempt. |
| **Expected Notification** | A task-assignment notification may be dispatched to the assignee by `NotificationPolicies`; assert the exact `event_key` against `qams.notification_rule` before execution — the rule set was not read in this pass. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.work_task WHERE subject_ref='PT-2027-08';` |
| **Evidence** | Three HTTP captures · SQL result of step 4 |
| **Result / Defect** | Not Run · — |
| **Notes** | `tasks.edit` is granted to both roles and gates nothing (`POST /api/tasks/{id}/complete` at `:108` is ungated) — see TC-RBAC-API-076. |

---

#### TC-RBAC-API-075 — `tasks.manage` gates SLA-definition upsert and is withheld from the Department Head  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-901 |
| **Level / Type / Technique** | API · Functional (positive + negative pair) · Decision Table — DT-1 rows 3 and 4 on a configuration action |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (holds) vs Department Head (does not) · `tasks.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | The `tasks` module's action set is `view, create, edit, manage` (`PermissionCatalog.cs:177`); the seeded DH grant stops at `edit` (`SystemRoleCatalog.cs:145`), the QM catch-all includes `manage` (`:113`). Gate site `OperationsControllers.cs:81`. |
| **Test Data** | `POST /api/sla-definitions` body `{"module":"nc","severity":"High","targetHours":24}` |
| **Steps** | 1. DH: `POST /api/sla-definitions` with the body; record status + `code`. 2. QM: `POST` the same; record status and `id`. 3. `GET /api/sla-definitions` as the DH; record status. 4. `SELECT module, severity, target_hours FROM qams.sla_definition WHERE module='nc' AND severity='High';`. |
| **Expected UI** | The SLA configuration screen's save control renders for the QM only; the DH can read the SLA table. |
| **Expected API** | Step 1 → `403` `AUTHZ-403`. Step 2 → `200 OK` body `{"id":"<uuid>"}` (`OperationsControllers.cs:82-84`). Step 3 → `200 OK` — the list route carries no `[RequirePermission]` (`:76`). |
| **Expected DB** | Step 4 returns one row with `target_hours = 24`. |
| **Expected Audit** | One `audit.audit_trail` row for the upsert. |
| **Expected Notification** | n/a — SLA definition changes define no notification rule. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.sla_definition WHERE module='nc' AND severity='High';` |
| **Evidence** | Three HTTP captures · SQL result of step 4 |
| **Result / Defect** | Not Run · — |
| **Notes** | `SLA-001` (module/severity required) and `SLA-002` (target hours positive) are domain rules on this route; a `403` must be observed instead of either for the DH, proving the gate runs first. |

---

#### TC-RBAC-API-076 — `tasks.view` and `tasks.edit` are inert  [GD — GAP-RBAC-003]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-904 |
| **Level / Type / Technique** | API · Negative-capability (gap-dependent) · Equivalence Partitioning — ungated-key partition |
| **Priority / Severity / Automation** | Medium · Medium · No — blocked on GAP-RBAC-003 |
| **Role / Permission / Tenant** | Bespoke role `Task-Edit-Only` · `tasks.view`, `tasks.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Of the four `tasks` keys only `create` (`OperationsControllers.cs:102`) and `manage` (`:81`) reach a gate. `GET /api/tasks/mine` (`:92`) and `POST /api/tasks/{id}/complete` (`:108`) carry `[Authorize]` only. A role holding exactly `["tasks.view","tasks.edit"]` on `taskedit@demo-lab.local`. |
| **Test Data** | An open task `T1` assigned to a different user |
| **Steps** | 1. As `taskedit@demo-lab.local`: `GET /api/tasks/mine`, then `POST /api/tasks/{T1}/complete`. 2. Reassign the user to a zero-key role; repeat both calls. 3. Diff the status pairs. 4. `SELECT status, completed_at_utc FROM qams.work_task WHERE id='{T1}';`. |
| **Expected UI** | "Tasks — Edit" appears in the privilege matrix as a switch with no enforcement point. |
| **Expected API** | The two runs produce identical statuses; both `POST …/complete` calls return the same status (`204` if the task's state permits completion). Granting or withholding `tasks.edit` changes nothing. |
| **Expected DB** | Step 4 shows the task completed by whichever call succeeded first — i.e. a user with **no** task privileges can complete another user's task. |
| **Expected Audit** | The completion is ledgered with the actor id of a user the configuration says holds no task rights. |
| **Expected Notification** | Whatever completion notification the rule set defines fires identically in both runs. |
| **Cleanup** | Reopen or delete `T1`; deactivate `Task-Edit-Only`. |
| **Evidence** | Two status vectors · `qams.work_task` row |
| **Result / Defect** | Not Run · — |
| **Notes** | **Acceptance criteria:** `POST /api/tasks/{id}/complete` requires `tasks.edit`, and `GET /api/tasks/mine` either requires `tasks.view` or a requirement records that a user's own task list is deliberately ungated. |

---

#### TC-RBAC-API-077 — `notifications.manage` gates all three rule/monitor routes while `/notifications/mine` stays open  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-901 |
| **Level / Type / Technique** | API · Functional (matrix) · Decision Table — one key × three routes × two roles, plus the ungated control route |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional, table-driven) |
| **Role / Permission / Tenant** | Quality Manager (holds) vs Department Head (holds only `notifications.view`) · `notifications.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | The `notifications` module has exactly two actions, `view` and `manage` (`PermissionCatalog.cs:178`). Seeded DH grants `notifications` = `view` only (`SystemRoleCatalog.cs:146`); the QM catch-all includes `manage`. Gate sites `PlatformControllers.cs:111` (GET rules), `:116` (POST rules), `:123` (GET monitor). |
| **Test Data** | `POST /api/notifications/rules` body `{"eventKey":"NcRaised","recipientRoles":["QualityManager"],"emailEnabled":true,"subjectTemplate":"NC {{ref}} raised","bodyTemplate":"A nonconformance was raised."}` |
| **Steps** | 1. DH: `GET /api/notifications/rules` → record status + `code`. 2. DH: `POST /api/notifications/rules` with the body → record. 3. DH: `GET /api/notifications/monitor` → record. 4. DH: `GET /api/notifications/mine` → record. 5. QM: repeat steps 1–3. 6. `SELECT event_key, email_enabled FROM qams.notification_rule WHERE event_key='NcRaised';`. |
| **Expected UI** | The Notifications **rules** and **dispatch monitor** screens 403 for the Department Head; the personal inbox renders for both roles. |
| **Expected API** | Steps 1, 2, 3 → `403` `application/problem+json` `code` = `AUTHZ-403`. Step 4 → `200 OK` with the paged envelope — `/api/notifications/mine` carries no gate (`PlatformControllers.cs:97`). Step 5 → `200 OK`, `200 OK` body `{"id":"<uuid>"}`, `200 OK`. |
| **Expected DB** | Step 6 returns exactly one row created by the QM; the DH's attempt created none. |
| **Expected Audit** | One `audit.audit_trail` row for the QM's rule upsert; none for the three DH refusals. |
| **Expected Notification** | n/a — configuring a rule does not itself dispatch one. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.notification_rule WHERE event_key='NcRaised' AND subject_template='NC {{ref}} raised';` |
| **Evidence** | Seven HTTP captures · SQL result of step 6 |
| **Result / Defect** | Not Run · — |
| **Notes** | `notifications.view` is granted to DH, Analyst and External Auditor and gates nothing — the inbox is open to every authenticated actor. Feeds GAP-RBAC-003; not separately re-tested here. |

---

#### TC-RBAC-API-078 — The External Auditor is refused every `notifications.manage` route  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-905 |
| **Level / Type / Technique** | API · Security (negative) · Error Guessing — the read-only tier probed against the one Operations module it partly holds |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | External Auditor · `notifications.manage` (**not held**) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | The seeded External Auditor's rule reduces `tasks` and `notifications` to `view` explicitly (`SystemRoleCatalog.cs:191`) and otherwise grants only `view`/`export` on non-administration modules (`:192`, `ReadActions` at `:31`). Tier is `ExternalAuditor`, so `[RequireInternalActor]` would also refuse at the command layer. |
| **Test Data** | The three gated notifications routes plus `POST /api/tasks` |
| **Steps** | 1. Login as `auditor@demo-lab.local`. 2. `GET /api/notifications/rules`; `POST /api/notifications/rules`; `GET /api/notifications/monitor`; `POST /api/tasks`. 3. Record status, `Content-Type` and `code` for each. 4. `GET /api/auth/me/privileges`; assert `permissions` contains `notifications.view` and does **not** contain `notifications.manage`, `tasks.create` or `tasks.manage`. |
| **Expected UI** | The auditor shell exposes read screens only; no notification-rule or task-creation affordance is rendered. |
| **Expected API** | All four calls → `403 Forbidden`, `application/problem+json`, `code` = `AUTHZ-403`. Step 4 → `200 OK` with the asserted key membership. |
| **Expected DB** | `SELECT count(*) FROM qams.notification_rule WHERE created_at_utc > now() - interval '2 minutes';` returns `0`. |
| **Expected Audit** | No `audit.audit_trail` rows from the four attempts. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Four HTTP captures · the `/auth/me/privileges` body |
| **Result / Defect** | Not Run · — |
| **Notes** | Complements `SystemRoleCatalogTests.The_external_auditor_holds_no_write_privileges_at_all`, which pins the seeded set in-process; this case pins the observable HTTP consequence. Every refusal here must be `AUTHZ-403` from the HTTP filter, never `AUTHZ-002` — a `422`/`AUTHZ-002` would mean the controller gate is missing and only the command behaviour is holding. |

---

#### TC-RBAC-API-079 — `reports.view` gates six ReportsController routes  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-906 |
| **Level / Type / Technique** | API · Functional (matrix) · Decision Table — one key × six routes × (holder, non-holder) |
| **Priority / Severity / Automation** | High · High · Yes (functional, table-driven) |
| **Role / Permission / Tenant** | Analyst (holds `reports.view`) vs bespoke `No-Reports` role · `reports.view` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `ReportsController` carries six `[RequirePermission(PermissionCatalog.Reports, PermissionAction.View)]` sites at `:25`, `:30`, `:35`, `:40`, `:50`, `:59`. Seeded Analyst grants `reports` = `view, export` (`SystemRoleCatalog.cs:177`). A bespoke role `No-Reports` holding `["nc.view"]` only, on `noreports@demo-lab.local` (tier `Analyst`). |
| **Test Data** | `GET /api/reports/kpis`, `/kpi-history?days=90`, `/nc-pareto`, `/sla-compliance`, `/quality-analytics`, `/quality-health-profile` |
| **Steps** | 1. Sweep all six routes as `analyst@demo-lab.local`; record statuses. 2. Sweep all six as `noreports@demo-lab.local`; record status + `code` for each. 3. `SELECT permission_key FROM qams.role_permission rp JOIN qams.role r ON r.id=rp.role_id AND r.tenant_id=rp.tenant_id WHERE r.normalized_name='ANALYST' AND rp.permission_key LIKE 'reports.%' ORDER BY 1;`. |
| **Expected UI** | The Quality Statistics and Quality Health screens render for the Analyst and show the shared 403 state for the `No-Reports` user. |
| **Expected API** | Step 1 → six `200 OK`. Step 2 → six `403 Forbidden`, `application/problem+json`, `code` = `AUTHZ-403` on every one. |
| **Expected DB** | Step 3 returns exactly two rows: `reports.export`, `reports.view`. |
| **Expected Audit** | No audit rows — all six routes are reads. |
| **Expected Notification** | n/a. |
| **Cleanup** | Deactivate `No-Reports`. |
| **Evidence** | Twelve HTTP captures · SQL result of step 3 |
| **Result / Defect** | Not Run · — |
| **Notes** | **This case contradicts the module front matter.** `11-module-rbac.md` §4.1 row 28 records the `reports` module as `ReadOnlyModule` with 2 keys and marks both `reports.view`* and `reports.export`* as having no HTTP gate, and §0 counts 144 `[RequirePermission]` sites across 37 controllers. Measured at v1.51.2: `PermissionCatalog.cs:182-183` gives `reports` three actions (`View, Export, Manage`), and `ReportsController.cs` carries seven gate sites across 38 controllers for a total of **151**. Raised as **GAP-RBAC-903**. |

---

#### TC-RBAC-API-080 — `reports.manage` gates the Quality Health weighting rewrite  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-906 |
| **Level / Type / Technique** | API · Functional (positive + negative) · Equivalence Partitioning — the third `reports` action, unrecorded in the front matter |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (holds) vs Department Head and Analyst (hold `view`+`export` only) · `reports.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `reports.manage` exists (`PermissionCatalog.cs:182-183`) and is gated at `ReportsController.cs:68`. Seeded DH and Analyst grant `reports` = `view, export` only (`SystemRoleCatalog.cs:147`, `:177`); the QM catch-all includes `manage`. No `qams.quality_health_profile` row exists yet for demo-lab (the profile is created on first edit, `QualityHealthProfileSlice.cs:29-41`). |
| **Test Data** | `PUT /api/reports/quality-health-profile` body `{"weights":[{"category":"Nonconformance","weight":30},{"category":"Documents","weight":25},{"category":"Competence","weight":25},{"category":"Equipment","weight":20}],"reason":"Board-approved 2027 weighting"}` — substitute the exact `QualityHealthCategory` member names from `src/NT.QAMS.Domain/Reporting/` before execution. |
| **Steps** | 1. Analyst: `PUT` the body; record status + `code`. 2. DH: `PUT` the body; record status + `code`. 3. QM: `PUT` the body; record status. 4. `SELECT category, weight FROM qams.quality_health_weight ORDER BY category;`. 5. `GET /api/reports/quality-health-profile` as the Analyst; confirm the new weighting is readable. |
| **Expected UI** | The weighting editor is read-only for the Analyst and the Department Head and editable for the Quality Manager. |
| **Expected API** | Steps 1 and 2 → `403` `AUTHZ-403`. Step 3 → `204 No Content` (`ReportsController.cs:69-74`). Step 5 → `200 OK` with the new weights. |
| **Expected DB** | Step 4 returns the four supplied weights; no rows existed before step 3. |
| **Expected Audit** | One `audit.audit_trail` row for the weighting change, carrying the reason `Board-approved 2027 weighting` — `UpdateQualityHealthWeightsValidator` makes `Reason` mandatory (`QualityHealthProfileSlice.cs:62`). |
| **Expected Notification** | n/a — no notification rule covers the weighting. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.quality_health_weight; DELETE FROM qams.quality_health_profile;` |
| **Evidence** | Three HTTP captures · SQL result of step 4 · the read-back in step 5 |
| **Result / Defect** | Not Run · — |
| **Notes** | The read/manage split is deliberate and documented in the catalogue comment at `PermissionCatalog.cs:179-181`. Because the front matter records only two `reports` keys, the total catalogue size is **171**, not 170 — see GAP-RBAC-901. |

---

#### TC-RBAC-API-081 — `reports.manage` is enforced twice: HTTP gate ∧ command policy  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-906 |
| **Level / Type / Technique** | API + Integration · Functional (defence in depth) · Multiple-Condition — (HTTP gate present) × (command policy present), with the HTTP gate suppressed in the second condition |
| **Priority / Severity / Automation** | Medium · High · Yes (functional + application unit test) |
| **Role / Permission / Tenant** | Department Head · `reports.manage` (**not held**) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; second half via `Application.UnitTests` with a stubbed `IUserPrivileges` |
| **Preconditions** | `UpdateQualityHealthWeightsCommand` is an `ICommand` carrying `[RequirePermissionPolicy(PermissionCatalog.Reports, PermissionAction.Manage)]` (`QualityHealthProfileSlice.cs:51`), and the route additionally carries `[RequirePermission(Reports, Manage)]` (`ReportsController.cs:68`). `AuthorizationBehavior` evaluates the policy only for commands (`AuthorizationBehavior.cs:34-37, 44-47`). |
| **Test Data** | The same weighting body as TC-RBAC-API-080 |
| **Steps** | 1. Over HTTP as the DH: `PUT /api/reports/quality-health-profile`; record status + `code`. 2. In `Application.UnitTests`, send `UpdateQualityHealthWeightsCommand` through the MediatR pipeline with `ICurrentUser.Role = DepartmentHead`, `IUserPrivileges.Has("reports.manage") = false`; record the thrown `DomainException`. 3. Repeat step 2 with `IUserPrivileges.IsPlatformAdmin = true`. |
| **Expected UI** | n/a — step 1 is a direct API call; steps 2 and 3 are in-process. |
| **Expected API** | Step 1 → `403 Forbidden`, `code` = `AUTHZ-403` (the HTTP filter refuses first, so the command never dispatches). |
| **Expected DB** | No `qams.quality_health_profile` row is created by any of the three steps. |
| **Expected Audit** | No `audit.audit_trail` row. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | HTTP capture · two xUnit assertion records |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 2 must throw `DomainException` with code `AUTHZ-002` and message `Role 'DepartmentHead' is not permitted to execute this action.` (`AuthorizationBehavior.cs:83-84`), which the HTTP handler would map to 403 via the `AUTHZ-` prefix arm (`DomainExceptionHandler.cs:63-68`). Step 3 must pass — `Has()` short-circuits true for a platform admin (`PrivilegeResolution.cs:39`). |

---

#### TC-RBAC-API-082 — `[RequirePermissionPolicy(reports.view)]` on query types is inert  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · no URS covers it — trace to `AuthorizationBehavior.cs:34-37, 44-47` · RSK-RBAC-907 |
| **Level / Type / Technique** | Integration · Functional (implementation-derived) · Branch coverage — the `!IsCommand` early-return branch of `AuthorizationBehavior.Handle` |
| **Priority / Severity / Automation** | Medium · Medium · Yes (application unit test) |
| **Role / Permission / Tenant** | Any authenticated tenant actor · `reports.view` (**withheld**) · `demo-lab` |
| **Environment** | `Application.UnitTests` with a stubbed `ICurrentUser` and `IUserPrivileges`; cross-checked over HTTP at `:5080` |
| **Preconditions** | `GetQualityAnalyticsQuery` (`QualityAnalyticsQuery.cs:23-25`) and `GetQualityHealthProfileQuery` (`QualityHealthProfileSlice.cs:14-15`) both declare `[RequirePermissionPolicy(Reports, View)]` **and** both implement `IQuery<T>`, not `ICommand`. `IsCommand` is computed once per closed generic (`AuthorizationBehavior.cs:34-37`) and the behaviour returns `next()` immediately when it is false (`:44-47`). |
| **Test Data** | `GetQualityAnalyticsQuery(BranchId: null, DepartmentId: null)` and `GetQualityHealthProfileQuery()` |
| **Steps** | 1. Send each query through the pipeline with `IUserPrivileges.Has("reports.view") = false` and `ICurrentUser.IsAuthenticated = false`. 2. Assert whether the handler executes. 3. Over HTTP, remove nothing and confirm that the only refusal observed on `GET /api/reports/quality-analytics` for a non-holder comes from the controller filter (`code` = `AUTHZ-403`, not `AUTHZ-002`). |
| **Expected UI** | n/a — no UI surface distinguishes the two enforcement layers. |
| **Expected API** | Step 3 → `403 Forbidden`, `code` = `AUTHZ-403` (HTTP filter). A code of `AUTHZ-002` would indicate the behaviour had refused, which the `IsCommand` gate makes impossible. |
| **Expected DB** | Steps 1–2 execute the handlers against the test context and read whatever rows the fixture holds; no writes. |
| **Expected Audit** | None — reads. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Two xUnit assertion records · one HTTP capture |
| **Result / Defect** | Not Run · — |
| **Notes** | **Both handlers run despite the declared policy** — the attribute is documentation, not enforcement, for these two types. This also corrects the front matter's §1.6 tally: there are **15** `[RequirePermissionPolicy]` call sites over **5** distinct keys (`roles.manage` ×4, `users.manage` ×7, `documents.sign` ×1, `reports.view` ×2, `reports.manage` ×1), not 12 over 3. Recorded as **GAP-RBAC-904**. |

---

#### TC-RBAC-API-083 — `reports.export` is the only `reports` key with no enforcement point  [GD — GAP-RBAC-003]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-904 |
| **Level / Type / Technique** | API · Negative-capability (gap-dependent) · Equivalence Partitioning — ungated-key partition inside an otherwise-gated module |
| **Priority / Severity / Automation** | Medium · Medium · No — blocked on GAP-RBAC-003 |
| **Role / Permission / Tenant** | Bespoke role `Reports-Export-Only` · `reports.export` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `reports` has three keys; `reports.view` is gated at six sites and `reports.manage` at one (`ReportsController.cs:25-68`); `reports.export` appears at no `[RequirePermission]` and no `[RequirePermissionPolicy]` anywhere in `src/`. `ExportsController.cs`'s three gates are `compliance.export` (`:61`, `:106`) and `reviews.export` (`:126`). |
| **Test Data** | A role holding exactly `["reports.export"]` on `repexp@demo-lab.local` (tier `Analyst`) |
| **Steps** | 1. Sweep every route on `/api/reports` and `/api/exports` with the fixture token; record statuses. 2. Reassign the user to a zero-key role and repeat. 3. Diff. |
| **Expected UI** | The privilege matrix offers "Reporting — Export" as a switch; the reporting screens offer download controls driven by `can()` on keys the server does not check. |
| **Expected API** | Both sweeps are identical: every `/api/reports` route returns `403 AUTHZ-403` (they need `reports.view`, which neither role holds) and every `/api/exports` route returns `403 AUTHZ-403` on its own key. Granting `reports.export` changes nothing. |
| **Expected DB** | The key is stored in `qams.role_permission` and governs nothing. |
| **Expected Audit** | The grant is ledgered by `RolePermissionsChanged` with its reason. |
| **Expected Notification** | n/a. |
| **Cleanup** | Deactivate `Reports-Export-Only`. |
| **Evidence** | Two status vectors · their diff |
| **Result / Defect** | Not Run · — |
| **Notes** | **Acceptance criteria:** a reporting export route exists and is gated on `reports.export`, or the `Export` action is removed from the `reports` module at `PermissionCatalog.cs:182-183`. Until then, `reports.export` is the one member of the module a user-access review cannot honestly certify. |

---

#### TC-RBAC-API-084 — `organization.create` gates branch, department and test-catalog creation  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-901 |
| **Level / Type / Technique** | API · Functional (matrix) · Decision Table — one key × three routes × (QM holder, DH non-holder) |
| **Priority / Severity / Automation** | High · Medium · Yes (functional, table-driven) |
| **Role / Permission / Tenant** | Quality Manager (holds) vs Department Head (holds no `organization` key at all) · `organization.create` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | The QM rule grants `organization` every action except `Manage` (`SystemRoleCatalog.cs:116`); the seeded DH table contains no `organization` entry at all (`:123-147`). Gate sites `PlatformControllers.cs:22` (branches), `:45` (departments), `:69` (test catalog). |
| **Test Data** | Branch `{"code":"BR-TEST-01","name":"Test Branch","city":"Riyadh"}`; department `{"branchId":"<new branch id>","code":"DP-TEST-01","name":"Test Department"}`; test `{"testCode":"TC-9001","testName":"Fixture Assay","methodology":"HPLC","turnaroundHours":48}` |
| **Steps** | 1. DH: `POST /api/branches`, `POST /api/departments`, `POST /api/test-catalog` with the bodies above; record status + `code` for each. 2. QM: repeat all three; record status and returned ids. 3. `GET /api/branches` as the DH; record status. 4. `SELECT code FROM qams.branch WHERE code='BR-TEST-01'; SELECT code FROM qams.department WHERE code='DP-TEST-01'; SELECT test_code FROM qams.test_catalog WHERE test_code='TC-9001';`. |
| **Expected UI** | The Organisation screen's "Add branch / department / test" controls render for the QM and are withheld from the DH, whose org tree is read-only. |
| **Expected API** | Step 1 → three `403`, `application/problem+json`, `code` = `AUTHZ-403`. Step 2 → three `200 OK`, each body `{"id":"<uuid>"}` (`PlatformControllers.cs:23-24, 46-48, 70-72`). Step 3 → `200 OK` — `GET /api/branches` carries no gate (`:17`). |
| **Expected DB** | Step 4 returns exactly one row per query, all with `tenant_id` = demo-lab. |
| **Expected Audit** | Three `audit.audit_trail` rows from step 2; none from step 1. |
| **Expected Notification** | n/a — org-structure creation defines no notification rule. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.department WHERE code='DP-TEST-01'; DELETE FROM qams.branch WHERE code='BR-TEST-01'; DELETE FROM qams.test_catalog WHERE test_code='TC-9001';` |
| **Evidence** | Seven HTTP captures · SQL results of step 4 |
| **Result / Defect** | Not Run · — |
| **Notes** | The branch created here is the fixture for TC-RBAC-API-085; run that case before this one's cleanup. |

---

#### TC-RBAC-API-085 — `organization.manage` withholds org-unit deactivation from the Quality Manager  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-901 |
| **Level / Type / Technique** | API · Functional (parity pin) · BVA — the single action that separates the QM's `organization` grant from the Tenant Administrator's |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (denied) vs Tenant Administrator (allowed) · `organization.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Branch `BR-TEST-01` from TC-RBAC-API-084 exists and is active. The QM rule is `PermissionCatalog.Organization => action is not PermissionAction.Manage` (`SystemRoleCatalog.cs:116`) — a deliberate parity pin, since deactivating org units was tenant-admin work. Gate sites `PlatformControllers.cs:27` (branch) and `:51` (department). |
| **Test Data** | `POST /api/branches/{BR-TEST-01 id}/deactivate` and `POST /api/departments/{DP-TEST-01 id}/deactivate`, both empty-bodied |
| **Steps** | 1. QM: `POST` the branch deactivate route; record status + `code`. 2. QM: `POST` the department deactivate route; record status + `code`. 3. `admin@demo-lab.local`: `POST` the branch deactivate route; record status. 4. `SELECT code, is_active FROM qams.branch WHERE code='BR-TEST-01';`. |
| **Expected UI** | The "Deactivate" control on an org unit renders only for the Tenant Administrator. |
| **Expected API** | Steps 1 and 2 → `403` `application/problem+json` `code` = `AUTHZ-403`. Step 3 → `204 No Content` (`PlatformControllers.cs:28-32`). |
| **Expected DB** | Step 4 returns `is_active = false` after step 3 and `is_active = true` if queried between steps 2 and 3. |
| **Expected Audit** | One `audit.audit_trail` row for step 3; none for steps 1 and 2. |
| **Expected Notification** | n/a — org-unit deactivation defines no notification rule. |
| **Cleanup** | Per TC-RBAC-API-084's cleanup. |
| **Evidence** | Three HTTP captures · SQL result of step 4 |
| **Result / Defect** | Not Run · — |
| **Notes** | If the QM ever returns `204` here, the parity pin in `SystemRoleCatalogTests` has drifted from the seeded catalogue and the tier-equivalence claim in URS-095 no longer holds. |

---

#### TC-RBAC-API-086 — `organization.edit` gates the list-of-values upsert  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-901 |
| **Level / Type / Technique** | API · Functional (positive + negative pair) · Equivalence Partitioning |
| **Priority / Severity / Automation** | Low · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (holds) vs Analyst (holds no `organization` key) · `organization.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | The `organization` module's actions are `view, create, edit, manage` (`PermissionCatalog.cs:186-187`). The seeded Analyst table contains no `organization` row (`SystemRoleCatalog.cs:153-177`). Gate site `PlatformControllers.cs:85`. |
| **Test Data** | `POST /api/lovs` body `{"category":"SampleMatrix","code":"SERUM-T","nameEn":"Serum (test)","nameAr":"مصل (اختبار)","nameFr":"Sérum (test)","sortOrder":99}` |
| **Steps** | 1. Analyst: `POST /api/lovs` with the body; record status + `code`. 2. QM: `POST` the same; record status and `id`. 3. `GET /api/lovs?category=SampleMatrix` as the Analyst; record status. 4. `SELECT category, code, sort_order FROM qams.lov WHERE code='SERUM-T';`. |
| **Expected UI** | The list-of-values editor's save control renders for the QM only; the picker that consumes the LOV renders for everyone. |
| **Expected API** | Step 1 → `403` `AUTHZ-403`. Step 2 → `200 OK` body `{"id":"<uuid>"}` (`PlatformControllers.cs:86-89`). Step 3 → `200 OK` — the GET carries no gate (`:80`). |
| **Expected DB** | Step 4 returns one row, `sort_order = 99`, `tenant_id` = demo-lab. |
| **Expected Audit** | One `audit.audit_trail` row for step 2. |
| **Expected Notification** | n/a. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.lov WHERE code='SERUM-T';` |
| **Evidence** | Three HTTP captures · SQL result of step 4 |
| **Result / Defect** | Not Run · — |
| **Notes** | The Arabic and French payload members exercise the trilingual LOV columns; keep the request body UTF-8 and send it from a file with `curl.exe --data "@file"` — PowerShell 5.1 mangles non-ASCII inline (conventions §3). |

---

#### TC-RBAC-API-087 — `organization.view` is inert: the org tree reads without a grant  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · no URS covers it — trace to `PlatformControllers.cs:17, 40, 64, 80` · RSK-RBAC-904 |
| **Level / Type / Technique** | API · Functional (implementation-derived) · Equivalence Partitioning — ungated-read partition inside a gated administration module |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Bespoke role `Zero-Keys` · none · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `organization.view` reaches no `[RequirePermission]`; the four organisation GETs (`PlatformControllers.cs:17`, `:40`, `:64`, `:80`) carry class-level `[Authorize]` only. Fixture `zerokeys@demo-lab.local` on role `Zero-Keys` (no keys). |
| **Test Data** | `GET /api/branches`, `GET /api/departments`, `GET /api/test-catalog`, `GET /api/lovs` |
| **Steps** | 1. Login as `zerokeys@demo-lab.local`. 2. Issue the four GETs; record statuses and row counts. 3. `GET /api/auth/me/privileges`; assert `permissions` is `[]`. |
| **Expected UI** | Organisation pickers populate throughout the shell for a user the privilege screen shows as holding nothing. |
| **Expected API** | Four `200 OK` responses carrying the tenant's org structure. Step 3 → `200 OK`, `"permissions": []`. |
| **Expected DB** | No change; the rows returned are exactly the demo-lab rows visible under the tenant query filter. |
| **Expected Audit** | None — reads. |
| **Expected Notification** | n/a. |
| **Cleanup** | Deactivate `Zero-Keys`. |
| **Evidence** | Four HTTP captures · the `/auth/me/privileges` body |
| **Result / Defect** | Not Run · — |
| **Notes** | Arguably intentional — org structure is reference data every screen needs. The point for validation is that it is intentional *nowhere on record*: no requirement states it, and `organization.view` remains a grantable switch. Feeds GAP-RBAC-003. |

---

#### TC-RBAC-API-088 — A `tenant-settings.view`-only role cannot read the MFA policy  [GD — GAP-RBAC-004]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-908 |
| **Level / Type / Technique** | API · Functional (gap-dependent) · Decision Table — DT-5 row 2: a class-level gate covering routes that a method-level gate would have separated |
| **Priority / Severity / Automation** | Medium · Medium · No — the positive half cannot pass until GAP-RBAC-004 is resolved |
| **Role / Permission / Tenant** | Bespoke role `Settings-Reader` · `tenant-settings.view` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `TenantSettingsController` carries a **class-level** `[RequirePermission(PermissionCatalog.TenantSettings, PermissionAction.Manage)]` at `:18`, so both its actions — `GET mfa-policy` (`:23`) and `PUT mfa-policy` (`:29`) — require `manage`. `tenant-settings` is a `ConfigurationModule` offering `view` and `manage` (`PermissionCatalog.cs:188`). A role holding exactly `["tenant-settings.view"]` on `settingsread@demo-lab.local` (tier `QualityManager`). |
| **Test Data** | `GET /api/tenant-settings/mfa-policy`; `PUT /api/tenant-settings/mfa-policy` body `{"require":true}` |
| **Steps** | 1. As `settingsread@demo-lab.local`: `GET /api/tenant-settings/mfa-policy`; record status + `code`. 2. `PUT` the body; record status + `code`. 3. As `admin@demo-lab.local` (holds `tenant-settings.manage`): `GET` then `PUT`; record statuses. 4. `SELECT require_mfa_privileged FROM qams.tenant_settings WHERE tenant_id = <demo-lab id>;`. |
| **Expected UI** | The Security Settings screen 403s wholesale for the view-only holder — there is no read-only rendering of the MFA policy. |
| **Expected API** | **Current build:** step 1 → `403` `AUTHZ-403`; step 2 → `403` `AUTHZ-403`; step 3 → `200 OK` then `204 No Content`. |
| **Expected DB** | Step 4 returns `true` after step 3's `PUT` and is unchanged by steps 1 and 2. |
| **Expected Audit** | One `audit.audit_trail` row for step 3's policy change; none for steps 1 and 2. |
| **Expected Notification** | n/a — an MFA-policy change defines no notification rule. |
| **Cleanup** | `PUT /api/tenant-settings/mfa-policy` `{"require":false}` as `admin@demo-lab.local` — the platform default is `false` (conventions §2). Deactivate `Settings-Reader`. |
| **Evidence** | Four HTTP captures · SQL result of step 4 |
| **Result / Defect** | Not Run · — |
| **Notes** | **Acceptance criteria for GAP-RBAC-004:** move the class-level gate onto the `PUT` and gate the `GET` on `tenant-settings.view`, so step 1 becomes `200 OK` while step 2 stays `403 AUTHZ-403`; or remove `View` from the module's action set so the matrix stops offering an inert switch. Until then the only honest expectation for step 1 is the 403 recorded above, and this case must **not** be rewritten to assert a 200. |

---

#### TC-RBAC-API-089 — `roles.view` admits the Quality Manager to the privilege screen and refuses the Department Head  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-901 |
| **Level / Type / Technique** | API · Functional (matrix) · Decision Table — one key × three read routes × (holder, non-holder) |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (holds `roles.view`) vs Department Head (holds no `roles` key) · `roles.view` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | The QM rule is `PermissionCatalog.RolesPrivileges => action is PermissionAction.View` (`SystemRoleCatalog.cs:112`); the seeded DH table has no `roles` entry. Gate sites `RolesController.cs:24` (catalog), `:29` (list), `:34` (get by id). |
| **Test Data** | Existing role id `R_TA` = the seeded **Tenant Administrator** role of demo-lab |
| **Steps** | 1. QM: `GET /api/roles/catalog`; `GET /api/roles`; `GET /api/roles/{R_TA}`; record statuses. 2. DH: repeat the same three; record status + `code` for each. 3. From step 1's catalog response, count the modules and the actions declared for module key `reports`. 4. From step 1's `GET /api/roles/{R_TA}` response, count `permissionKeys`. |
| **Expected UI** | The Roles & Privileges screen renders for the QM in read-only form (no save control, since `roles.manage` is absent) and shows the 403 state for the DH. Note that the SPA route `/roles` is reachable by both — no route carries a permission guard (GAP-RBAC-007). |
| **Expected API** | Step 1 → three `200 OK`. Step 2 → three `403`, `code` = `AUTHZ-403`. |
| **Expected DB** | No change — all three routes are reads. Cross-check: `SELECT count(*) FROM qams.role_permission rp JOIN qams.role r ON r.id=rp.role_id AND r.tenant_id=rp.tenant_id WHERE r.normalized_name='TENANT ADMINISTRATOR';` equals step 4's count. |
| **Expected Audit** | None — reads. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Six HTTP captures · the catalog payload · SQL count |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 3 is a measurement, not merely an assertion: the catalog is projected straight from `PermissionCatalog.Modules` (`RolesSlice.cs:213-219`), so it should report **31 modules** and **three** actions for `reports` (`view`, `export`, `manage`). Step 4 should report **171** keys for the Tenant Administrator, which holds `AllKeys` verbatim (`SystemRoleCatalog.cs:100`) — not the 170 recorded in URS-095 and in the module front matter. See GAP-RBAC-901. |

---

#### TC-RBAC-API-090 — `roles.manage` refuses the Quality Manager on all five RolesController write routes  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-909 |
| **Level / Type / Technique** | API · Security (negative, matrix) · Decision Table — one key × five write routes, on the role that holds the module's other action |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional, table-driven) |
| **Role / Permission / Tenant** | Quality Manager · `roles.manage` (**not held**) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | The QM holds `roles.view` and nothing else in the module (`SystemRoleCatalog.cs:112`). Gate sites `RolesController.cs:39` (POST), `:48` (PUT), `:56` (PUT permissions), `:64` (deactivate), `:72` (reactivate). A tenant-defined role `R_X` exists and is active. |
| **Test Data** | Create: `{"name":"QM-Escalation-Probe","description":null,"permissionKeys":["roles.manage"],"defaultLanguage":null}`. Update: `{"name":"Renamed","description":null,"defaultLanguage":"en"}`. Permissions: `{"permissionKeys":["roles.manage"],"reason":"probe"}`. |
| **Steps** | 1. QM: `POST /api/roles` with the create body; record status + `code`. 2. QM: `PUT /api/roles/{R_X}` with the update body. 3. QM: `PUT /api/roles/{R_X}/permissions` with the permissions body. 4. QM: `POST /api/roles/{R_X}/deactivate`. 5. QM: `POST /api/roles/{R_X}/reactivate`. 6. `SELECT name, is_active FROM qams.role WHERE id='{R_X}';` and `SELECT count(*) FROM qams.role WHERE normalized_name='QM-ESCALATION-PROBE';`. |
| **Expected UI** | The privilege screen renders in read-only mode for the QM; no create, rename, save-grants or activate control is offered. |
| **Expected API** | All five calls → `403 Forbidden`, `application/problem+json`, `code` = `AUTHZ-403`. |
| **Expected DB** | Step 6: `R_X`'s `name` and `is_active` are unchanged, and the count of `QM-ESCALATION-PROBE` roles is `0`. |
| **Expected Audit** | No `RoleCreated`, `RoleRenamed`, `RolePermissionsChanged`, `RoleDeactivated` or `RoleReactivated` event in `qams.outbox_event` or `audit.audit_trail` from any of the five attempts. |
| **Expected Notification** | n/a. |
| **Cleanup** | None — nothing changed. |
| **Evidence** | Five HTTP captures · SQL results of step 6 |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the privilege-escalation containment case for the batch: the role closest to an administrator without being one must not be able to grant itself `roles.manage`. Both layers refuse independently — the HTTP filter first, and `[RequirePermissionPolicy(RolesPrivileges, Manage)]` on all four commands (`RolesSlice.cs:66, 102, 141, 176`) behind it. |

---

#### TC-RBAC-API-091 — `ROLE-005` refuses a commissioning-brief key on the `roles.manage` path  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-910 |
| **Level / Type / Technique** | API · Functional (negative) · Error Guessing — the exact fictional codes GAP-RBAC-001 says a configurator will try |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Tenant Administrator · `roles.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `Role.ReplacePermissions` lower-cases, trims, de-duplicates and drops blanks, then rejects anything outside `AllKeys` with `ROLE-005` (`Role.cs:188-201`). `PermissionCatalog.IsKnown` is an `Ordinal` set lookup (`PermissionCatalog.cs:193-202`). |
| **Test Data** | Body A: `{"name":"Brief-Codes","description":null,"permissionKeys":["DOC.APPROVE","USER.CREATE"],"defaultLanguage":null}`. Body B: `{"name":"Mixed-Case","description":null,"permissionKeys":["Roles.View","  reports.manage  "],"defaultLanguage":null}`. Body C: `{"name":"Near-Miss","description":null,"permissionKeys":["reports.publish"],"defaultLanguage":null}`. |
| **Steps** | 1. As `admin@demo-lab.local`: `POST /api/roles` with body A; record status, `code`, and the unknown keys named in the message. 2. `POST /api/roles` with body B; record status and the created id. 3. `GET /api/roles/{id from step 2}`; record `permissionKeys`. 4. `POST /api/roles` with body C; record status + `code`. 5. `SELECT count(*) FROM qams.role WHERE normalized_name IN ('BRIEF-CODES','NEAR-MISS');`. |
| **Expected UI** | The privilege matrix cannot produce body A at all — it emits catalogue keys only; the case is reachable through the API. |
| **Expected API** | Step 1 → `422 Unprocessable Entity`, `application/problem+json`, `code` = `ROLE-005`, message naming **both** `doc.approve` and `user.create` (the values are lower-cased at `Role.cs:194` before the membership test, so the message reports the normalised forms). Step 2 → `200 OK` `{"id":"<uuid>"}`. Step 4 → `422` `code` = `ROLE-005` naming `reports.publish`. |
| **Expected DB** | Step 3 returns exactly `["reports.manage","roles.view"]` — trimmed and lower-cased. Step 5 returns `0`. |
| **Expected Audit** | One `RoleCreated` event for step 2 only. Steps 1 and 4 write nothing. |
| **Expected Notification** | n/a. |
| **Cleanup** | `POST /api/roles/{Mixed-Case id}/deactivate`. |
| **Evidence** | Three HTTP captures · the `GET /api/roles/{id}` body · SQL count |
| **Result / Defect** | Not Run · — |
| **Notes** | Body C is the near-miss: `publish` is not a member of the closed eight-value `PermissionAction` enum (`PermissionCatalog.cs:9-34`), so `reports.publish` can never be a key regardless of module. Confirms that `ROLE-005` catches action-name errors as well as module-name errors. |

---

#### TC-RBAC-API-092 — `ROLE-006` refuses dropping the last `roles.manage` grant  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-098 · RSK-RBAC-909 |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — DT-3 row 2, the survivor-count-zero cell |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Tenant Administrator · `roles.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Exactly one active role grants `roles.manage` (the seeded **Tenant Administrator**, role id `R_TA`) and exactly one active user holds it (`admin@demo-lab.local`). Verify first: `SELECT r.id, count(u.id) FROM qams.role r JOIN qams.role_permission rp ON rp.role_id=r.id AND rp.tenant_id=r.tenant_id LEFT JOIN qams.user_account u ON u.role_id=r.id AND u.is_active LEFT JOIN LATERAL (SELECT 1) x ON true WHERE rp.permission_key='roles.manage' AND r.is_active GROUP BY r.id;` returns one row with count 1. Guard at `RolesSlice.cs:160-166` and `:28-52`. |
| **Test Data** | `PUT /api/roles/{R_TA}/permissions` body `{"permissionKeys":["roles.view","users.view"],"reason":"Attempt to drop the last manage grant"}` |
| **Steps** | 1. As `admin@demo-lab.local`: `PUT /api/roles/{R_TA}/permissions` with the body; record status + `code` + `title`. 2. `SELECT count(*) FROM qams.role_permission WHERE role_id='{R_TA}';`. 3. `GET /api/roles/{R_TA}`; confirm `permissionKeys` still contains `roles.manage`. 4. Create a second active user `admin2@demo-lab.local` on the Tenant Administrator role, then repeat step 1 and record the status. |
| **Expected UI** | The save is refused with the guard's own message; the matrix's checkboxes revert to the stored state. |
| **Expected API** | Step 1 → `422 Unprocessable Entity`, `application/problem+json`, `code` = `ROLE-006`, `title` beginning `This change would leave no active user able to manage roles and privileges.` (`RolesSlice.cs:48-50`). Step 4 → `204 No Content` — with a second holder the guard passes. |
| **Expected DB** | Step 2 returns **171** — the Tenant Administrator's grant set is untouched by the refused call. After step 4 it returns `2`. |
| **Expected Audit** | No `RolePermissionsChanged` event from step 1; one from step 4 carrying `reason` = `Attempt to drop the last manage grant`, `revoked` listing 169 keys and `granted` empty. |
| **Expected Notification** | n/a. |
| **Cleanup** | Restore the Tenant Administrator's grants to `PermissionCatalog.AllKeys` via `PUT /api/roles/{R_TA}/permissions` with reason `Restore fixture`, then deactivate `admin2@demo-lab.local`. Take a `pg_dump` of `qams.role_permission` before step 4 — this case mutates the tenant's administrator role. |
| **Evidence** | Two HTTP captures · SQL counts before and after · the restored grant set |
| **Result / Defect** | Not Run · — |
| **Notes** | The `171` in the DB expectation is the corrected catalogue size (GAP-RBAC-901); if the assertion is written as `170` it will fail for the wrong reason. Step 4 deliberately proves the guard is a survivor check, not a blanket prohibition — DT-3 row 1. |

---

#### TC-RBAC-API-093 — A grant takes effect on the very next request, on the same unexpired token  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-096 · RSK-RBAC-911 |
| **Level / Type / Technique** | API · Functional (state transition) · State Transition — `Resolved(no key) → Resolved(key)` with no session event between |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Bespoke role `Ops-Probe` · `tasks.create` (granted mid-session) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | A tenant-defined active role `Ops-Probe` holding `["tasks.view"]`, assigned to `opsprobe@demo-lab.local` on tier `Analyst`. Privilege resolution runs on **every** authenticated request and is deliberately uncached (`PrivilegeResolution.cs:52-60`; middleware call at `RequestIdentity.cs:118-121`). The access JWT lifetime is 15 minutes by default (`SecurityAdapters.cs:57`), so the whole case must complete inside one token. |
| **Test Data** | `POST /api/tasks` body `{"subject":"Immediacy probe","subjectRef":"IMM-001","assigneeUserId":"<opsprobe uuid>","assigneeRole":"Analyst","dueDate":"2027-01-31"}`; grant body `{"permissionKeys":["tasks.view","tasks.create"],"reason":"Cover for the Quality Manager during annual leave"}` |
| **Steps** | 1. Login as `opsprobe@demo-lab.local`; capture the JWT and note `exp`. 2. `POST /api/tasks` with the task body; record status + `code` (expect the refusal). 3. In a **separate** session as `admin@demo-lab.local`: `PUT /api/roles/{Ops-Probe}/permissions` with the grant body; record status. 4. Without re-authenticating, replay step 2 using the **same** JWT from step 1; record status. 5. `GET /api/auth/me/privileges` with the same JWT; record `permissions`. 6. `SELECT subject_ref FROM qams.work_task WHERE subject_ref='IMM-001';`. |
| **Expected UI** | UAT scenario TC-RBAC-UAT-002's business rendering: the analyst retries without signing out and the action is accepted. Note the SPA button may still be hidden — see TC-RBAC-API-098. |
| **Expected API** | Step 2 → `403` `AUTHZ-403`. Step 3 → `204 No Content`. Step 4 → `200 OK` body `{"id":"<uuid>"}`. Step 5 → `200 OK` with `permissions` containing `tasks.create`. |
| **Expected DB** | Step 6 returns exactly one row. `SELECT permission_key FROM qams.role_permission WHERE role_id='{Ops-Probe}' ORDER BY 1;` returns `tasks.create`, `tasks.view`. |
| **Expected Audit** | One `RolePermissionsChanged` event whose payload carries `Granted = ["tasks.create"]`, `Revoked = []` and `Reason = "Cover for the Quality Manager during annual leave"` (`Role.cs:147-149, 220-225`), forwarded to `audit.audit_trail` with `tenant_id` = demo-lab. |
| **Expected Notification** | Any task-assignment rule fires for step 4's task; none for the privilege grant itself. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.work_task WHERE subject_ref='IMM-001';` then deactivate `Ops-Probe`. |
| **Evidence** | JWT `exp` claim · four HTTP captures showing the same `Authorization` header value in steps 2 and 4 · SQL results |
| **Result / Defect** | Not Run · — |
| **Notes** | The evidence that matters is that steps 2 and 4 carry a **byte-identical** bearer token; record both raw requests. This is the direct URS-096 verification at HTTP level, complementing OQ-RP-04. |

---

#### TC-RBAC-API-094 — A revoke takes effect on the very next request, on the same unexpired token  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-096 · RSK-RBAC-911 |
| **Level / Type / Technique** | API · Functional (state transition) · State Transition — `Resolved(key) → Resolved(no key)`, the direction whose failure is a security defect |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Bespoke role `Ops-Probe` · `tasks.create` (revoked mid-session) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Continues from TC-RBAC-API-093: `Ops-Probe` currently holds `["tasks.view","tasks.create"]`; `opsprobe@demo-lab.local` holds a JWT issued before the grant and still unexpired. |
| **Test Data** | `POST /api/tasks` body `{"subject":"Immediacy probe 2","subjectRef":"IMM-002","assigneeUserId":"<opsprobe uuid>","assigneeRole":"Analyst","dueDate":"2027-01-31"}`; revoke body `{"permissionKeys":["tasks.view"],"reason":"Cover period ended"}` |
| **Steps** | 1. With the existing JWT, `POST /api/tasks` (`IMM-002`); record status (expect success). 2. As `admin@demo-lab.local`: `PUT /api/roles/{Ops-Probe}/permissions` with the revoke body; record status. 3. Immediately replay step 1 with the **same** JWT using `subjectRef` `IMM-003`; record status + `code`. 4. `GET /api/auth/me/privileges` with the same JWT; record `permissions`. 5. `SELECT subject_ref FROM qams.work_task WHERE subject_ref IN ('IMM-002','IMM-003') ORDER BY 1;`. |
| **Expected UI** | UAT scenario TC-RBAC-UAT-003: the retried action is refused and the audit trail shows the revocation with its reason, role name and operator. |
| **Expected API** | Step 1 → `200 OK`. Step 2 → `204 No Content`. Step 3 → `403 Forbidden`, `application/problem+json`, `code` = `AUTHZ-403`. Step 4 → `200 OK` with `permissions` = `["tasks.view"]` exactly. |
| **Expected DB** | Step 5 returns exactly one row, `IMM-002`. `SELECT permission_key FROM qams.role_permission WHERE role_id='{Ops-Probe}';` returns the single row `tasks.view`. |
| **Expected Audit** | One `RolePermissionsChanged` event with `Granted = []`, `Revoked = ["tasks.create"]`, `Reason = "Cover period ended"`, and the role name `Ops-Probe` in the payload (`Role.cs:220-225`). |
| **Expected Notification** | n/a — a revoke raises no notification. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.work_task WHERE subject_ref LIKE 'IMM-%';` then deactivate `Ops-Probe`. |
| **Evidence** | Three HTTP captures with the identical bearer token · the `audit.audit_trail` row payload · SQL results |
| **Result / Defect** | Not Run · — |
| **Notes** | If step 3 returns `200`, a privilege cache has been introduced somewhere between `ActiveSessionMiddleware` and `RequirePermissionAttribute`, which the code comment at `PrivilegeResolution.cs:54-60` explicitly forbids: "the failure mode of a missed invalidation is a user retaining a revoked privilege". This is the single highest-value assertion in the batch. |

---

#### TC-RBAC-API-095 — Deactivating a role blacks out its holders mid-session  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-096 · RSK-RBAC-911 |
| **Level / Type / Technique** | API · Functional (state transition) · State Transition — role `Active → Inactive` with a live holder session, per INV-10 |
| **Priority / Severity / Automation** | High · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Bespoke role `Reports-Probe` · `reports.view` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | An active tenant-defined role `Reports-Probe` holding `["reports.view"]`, assigned to `repprobe@demo-lab.local` (tier `Analyst`), with a live unexpired JWT. `PrivilegeResolver` filters the role by `r.IsActive` (`PrivilegeResolution.cs:90-98`), so an inactive role resolves to zero keys while `RoleId` is still echoed (`:108-114`). The role does **not** grant `roles.manage`, so `ROLE-006` will not be consulted (`RolesSlice.cs:193`). |
| **Test Data** | `GET /api/reports/kpis`; `POST /api/roles/{Reports-Probe}/deactivate` |
| **Steps** | 1. As `repprobe@demo-lab.local`: `GET /api/reports/kpis`; record status. 2. As `admin@demo-lab.local`: `POST /api/roles/{Reports-Probe}/deactivate`; record status. 3. Replay step 1 with the same JWT; record status + `code`. 4. `GET /api/auth/me/privileges` with the same JWT; record `roleId`, `roleName`, `permissions`. 5. `SELECT is_active FROM qams.role WHERE id='{Reports-Probe}';`. 6. As `admin@demo-lab.local`: `POST /api/roles/{Reports-Probe}/reactivate`; then replay step 1. |
| **Expected UI** | The reporting screens stop rendering data for the holder without any sign-out prompt — there is no session-level signal, only per-request refusal. |
| **Expected API** | Step 1 → `200 OK`. Step 2 → `204 No Content`. Step 3 → `403` `code` = `AUTHZ-403`. Step 4 → `200 OK` with `roleId` = the `Reports-Probe` id (still echoed), `roleName` = `null`, `permissions` = `[]`. Step 6 → `204` then `200 OK`. |
| **Expected DB** | Step 5 returns `false`; `qams.role_permission` still holds the `reports.view` row — deactivation does not delete grants. |
| **Expected Audit** | One `RoleDeactivated` event and later one `RoleReactivated` event, both carrying only `(RoleId, Name)` and **no reason** — the gap GAP-RBAC-014 records. Assert the absence of a reason field, do not assert its presence. |
| **Expected Notification** | n/a — role deactivation notifies nobody, including the affected holder. |
| **Cleanup** | Step 6 restores the role; then deactivate `Reports-Probe` permanently and leave it (roles are never deleted — GAP-RBAC-015). |
| **Evidence** | Four HTTP captures · the `/auth/me/privileges` body from step 4 · SQL result of step 5 |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 4's `roleName: null` beside a non-null `roleId` is the observable signature of an inactive role and is worth pinning: a client that renders the role name from this payload will show a blank where a role used to be. The 401 `AUTH-006` path is *not* taken here — the user account is still active; only the role is not. |

---

#### TC-RBAC-API-096 — A tier change forces `401 AUTH-007` on the affected user's next request  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-096 · RSK-RBAC-912 |
| **Level / Type / Technique** | API · Functional (state transition) · State Transition — `token tier == DB tier → token tier ≠ DB tier`, crossing the `AUTH-007` guard |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head demoted to Analyst by a `users.manage` holder · n/a — the check precedes every permission gate · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `dh@demo-lab.local` is active on tier `DepartmentHead` and the seeded **Department Head** role, holding a live unexpired JWT whose `ClaimTypes.Role` claim reads `DepartmentHead` (`SecurityAdapters.cs:88`). `ActiveSessionMiddleware` compares the token role to `user_account.role` on every authenticated request and denies on mismatch (`RequestIdentity.cs:104-109`). `ChangeUserRoleHandler` changes the tier **and** reassigns `role_id` to the seeded role for the new tier (`UserManagement.cs:115-128`, `SeededRoleDefault` at `:122`). |
| **Test Data** | `POST /api/users/{dh uuid}/role` body `{"role":"Analyst"}` |
| **Steps** | 1. As `dh@demo-lab.local`: `POST /api/tasks` (a route the DH's tier and role both permit); record status; keep the JWT. 2. As `admin@demo-lab.local`: `POST /api/users/{dh uuid}/role` with `{"role":"Analyst"}`; record status. 3. Replay step 1 with the **same** JWT; record status, `Content-Type` and `code`. 4. `GET /api/auth/me/privileges` with the same JWT; record status + `code`. 5. Re-login as `dh@demo-lab.local`; replay step 1; record status + `code`. 6. `SELECT role, role_id FROM qams.user_account WHERE email='dh@demo-lab.local';` and resolve `role_id` to a role name. |
| **Expected UI** | The SPA receives a 401 on its next call and routes to sign-in with the message "Your permissions have changed. Please sign in again." |
| **Expected API** | Step 1 → `200 OK`. Step 2 → `204 No Content` (or the status `UsersController.cs:34` returns — assert the exact one at execution). Step 3 → `401 Unauthorized`, `application/problem+json`, `code` = `AUTH-007`, `title` = `Your permissions have changed. Please sign in again.` (`RequestIdentity.cs:107`). Step 4 → also `401 AUTH-007` — the middleware short-circuits before the endpoint. Step 5 → `403` `code` = `AUTHZ-403`, because the Analyst role does not grant `tasks.create`. |
| **Expected DB** | Step 6 returns `role = 'Analyst'` and `role_id` resolving to the seeded role named `Analyst`. |
| **Expected Audit** | One `UserRoleAssigned` event carrying `(UserId, RoleId)` (`UserAccount.cs:282`), with a non-empty `tenant_id` (RP-D1 pin). Note there is **no reason field** on this path — GAP-RBAC-014. |
| **Expected Notification** | n/a — a tier change notifies nobody. |
| **Cleanup** | `POST /api/users/{dh uuid}/role` `{"role":"DepartmentHead"}` as `admin@demo-lab.local`, then re-login as the DH and confirm `200` on step 1's call. |
| **Evidence** | Five HTTP captures with the identical bearer token in steps 1, 3 and 4 · SQL result of step 6 |
| **Result / Defect** | Not Run · — |
| **Notes** | Two distinct mechanisms fire in sequence and must not be conflated: the tier mismatch produces a **401** that terminates the session, and only after re-authentication does the new role's missing key produce a **403**. A test that asserts 403 at step 3 will fail. Note also that this route is the GAP-RBAC-012 lockout bypass — do **not** run it against the last `roles.manage` holder. |

---

#### TC-RBAC-API-097 — A configurable-role change does **not** raise `AUTH-007`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-096 · RSK-RBAC-912 |
| **Level / Type / Technique** | API · Functional (negative-of-a-guard) · State Transition — the sibling transition that deliberately does not cross the `AUTH-007` guard |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst moved onto bespoke role `Ops-Probe` by a `users.manage` holder · `users.manage` (the operator's) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `analyst@demo-lab.local` is on tier `Analyst` and the seeded **Analyst** role, with a live unexpired JWT. An active tenant-defined role `Ops-Probe` holding `["tasks.view","tasks.create"]`. `AssignUserRoleHandler` changes `role_id` only and never touches `user_account.role` (`UserManagement.cs:205-232`), so the token's `ClaimTypes.Role` still matches the DB tier. |
| **Test Data** | `PUT /api/users/{analyst uuid}/assigned-role` body `{"roleId":"<Ops-Probe id>"}`; probe `POST /api/tasks` with `subjectRef` `ASSIGN-001` |
| **Steps** | 1. As `analyst@demo-lab.local`: `POST /api/tasks`; record status + `code` (expect the refusal — the seeded Analyst lacks `tasks.create`). 2. As `admin@demo-lab.local`: `PUT /api/users/{analyst uuid}/assigned-role` with the body; record status. 3. Replay step 1 with the **same** JWT; record status. 4. `GET /api/auth/me/privileges` with the same JWT; record `roleName` and `permissions`. 5. `SELECT role, role_id FROM qams.user_account WHERE email='analyst@demo-lab.local';`. |
| **Expected UI** | The analyst is not signed out. Their effective capability changes silently mid-session; the shell header's role name updates only on the next privileges fetch. |
| **Expected API** | Step 1 → `403` `AUTHZ-403`. Step 2 → `204 No Content` (or the status at `UsersController.cs:43` — assert the exact one at execution). Step 3 → `200 OK` — **no `401 AUTH-007`**. Step 4 → `200 OK` with `roleName` = `Ops-Probe` and `permissions` = `["tasks.create","tasks.view"]` (ordinal-ordered, `RolesSlice.cs:300`). |
| **Expected DB** | Step 5 returns `role = 'Analyst'` **unchanged** and `role_id` = the `Ops-Probe` id. This unchanged tier beside a changed role is precisely why `AUTH-007` does not fire. |
| **Expected Audit** | One `UserRoleAssigned` event, `tenant_id` non-empty. No reason is recorded — GAP-RBAC-014. |
| **Expected Notification** | n/a. |
| **Cleanup** | `PUT /api/users/{analyst uuid}/assigned-role` back to the seeded **Analyst** role id; `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.work_task WHERE subject_ref='ASSIGN-001';`; deactivate `Ops-Probe`. |
| **Evidence** | Four HTTP captures with the identical bearer token · the `/auth/me/privileges` body · SQL result of step 5 |
| **Result / Defect** | Not Run · — |
| **Notes** | Deliberate contrast with TC-RBAC-API-096. The two "role change" routes have opposite session consequences: `POST /api/users/{id}/role` ends the session with 401 `AUTH-007`; `PUT /api/users/{id}/assigned-role` does not. Operators and support runbooks need this distinction stated, and no requirement currently states it — see the coverage note. |

---

#### TC-RBAC-API-098 — There is no server-side permission cache and no permission claim in the token  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-096 · RSK-RBAC-911 |
| **Level / Type / Technique** | API · Structural / Functional · Data Flow — the privilege value's definition (DB read) to its use (`Has()`), asserting no intervening store |
| **Priority / Severity / Automation** | High · Critical · Partly — the JWT-claim half is automatable; the "no cache" half is a code-inspection plus timing assertion |
| **Role / Permission / Tenant** | Bespoke role `Ops-Probe` · `tasks.create` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; OTel/Npgsql instrumentation enabled to count queries per request |
| **Preconditions** | The JWT claim set is fixed at `sub`, `email`, `name`, `ClaimTypes.Role`, `scope` and (tenant users only) `tenant_id` (`SecurityAdapters.cs:84-96`) — no permission, role-id or scope claim. `RequestPrivileges` is a **scoped** holder written once per request (`PrivilegeResolution.cs:12-49`); `PrivilegeResolver` reads `db.Users` then `db.Roles` on each call (`:64-115`). The conventions record that Redis, `IDistributedCache` and `IMemoryCache` are all absent from the build. |
| **Test Data** | A decoded JWT; two `POST /api/tasks` calls 200 ms apart straddling a revoke |
| **Steps** | 1. Login as `opsprobe@demo-lab.local`; base64url-decode the JWT payload and enumerate every claim name. 2. Grep the running build's configuration for `IDistributedCache`, `IMemoryCache` and `AddStackExchangeRedisCache` registrations; record the result. 3. With `Ops-Probe` holding `tasks.create`, issue `POST /api/tasks` (`CACHE-001`) and record the elapsed time and the Npgsql span count for the request. 4. Revoke `tasks.create` from another session. 5. Within 200 ms of step 4's `204`, issue `POST /api/tasks` (`CACHE-002`); record status. 6. `SELECT subject_ref FROM qams.work_task WHERE subject_ref LIKE 'CACHE-%';`. |
| **Expected UI** | n/a — this case has no UI surface; the SPA's own snapshot is TC-RBAC-API-099. |
| **Expected API** | Step 1 → the claim set contains exactly `sub`, `email`, `name`, the role claim URI, `scope`, `tenant_id`, plus the standard `nbf`/`exp`/`iss`/`aud` — and **no** claim carrying a permission key. Step 3 → `200 OK`. Step 5 → `403` `AUTHZ-403` despite the 200 ms gap. |
| **Expected DB** | Step 6 returns exactly one row, `CACHE-001`. |
| **Expected Audit** | One `audit.audit_trail` row for `CACHE-001` only. |
| **Expected Notification** | Whatever task-assignment rule applies fires once, for `CACHE-001`. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.work_task WHERE subject_ref LIKE 'CACHE-%';` |
| **Evidence** | The decoded JWT payload · the cache-registration grep output · two OTel traces showing the per-request `db.Users` + `db.Roles` reads · two HTTP captures |
| **Result / Defect** | Not Run · — |
| **Notes** | "Cache invalidation" in this build is a null requirement on the server, and the case exists to prove the null rather than assume it. Step 3's trace should show the `user_account` read from `ActiveSessionMiddleware` (`RequestIdentity.cs:93-96`) **plus** the resolver's own `user_account` and `role` reads — three indexed reads, the cost the design comment at `PrivilegeResolution.cs:57-59` accepts. Record the actual count; do not assert a number that was not measured. |

---

#### TC-RBAC-API-099 — The SPA's privilege snapshot is stale after a mid-session role edit  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · no URS covers it — trace to `frontend/src/app/core/permissions.service.ts:43-65` · RSK-RBAC-913 |
| **Level / Type / Technique** | E2E (browser) · Functional (implementation-derived) · State Transition — SPA signal state across a server-side privilege change with no session event |
| **Priority / Severity / Automation** | Medium · Low (affordance only, not a security boundary) · Yes (Playwright) |
| **Role / Permission / Tenant** | Bespoke role `Ops-Probe` · `tasks.create` · `demo-lab` |
| **Environment** | SPA `localhost:4200/t/demo-lab` (Chromium via Playwright) + API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | `PermissionsService` fetches `GET {apiBaseUrl}/auth/me/privileges` inside an `effect` that re-runs on `auth.isAuthenticated()` changes only (`permissions.service.ts:44-65`); there is no polling, no push channel (SignalR is absent) and no invalidation hook on a role edit. `can()` reads the `granted()` computed set (`:67-70`). `opsprobe@demo-lab.local` is signed in on role `Ops-Probe` holding `["tasks.view"]`. |
| **Test Data** | Grant body `{"permissionKeys":["tasks.view","tasks.create"],"reason":"SPA staleness probe"}` |
| **Steps** | 1. Sign in as `opsprobe@demo-lab.local` and open the tasks board; assert the "Create task" control is **absent**. 2. Out-of-band (API call, not the browser), grant `tasks.create` to `Ops-Probe` as `admin@demo-lab.local`. 3. Without reloading, interact with the tasks board (filter, paginate) and re-assert the control's presence. 4. Reload the page (`F5`) and re-assert. 5. Read the browser's network log for calls to `/auth/me/privileges` between steps 1 and 4. |
| **Expected UI** | Step 1 → control absent. Step 3 → control **still absent** — the signal was not refreshed. Step 4 → control present. |
| **Expected API** | Between steps 1 and 3 the network log shows **no** `GET /auth/me/privileges`; step 4 shows exactly one. Throughout, a direct `POST /api/tasks` from the console would return `200` from step 2 onward — the server is already enforcing the new grant (TC-RBAC-API-093). |
| **Expected DB** | `qams.role_permission` for `Ops-Probe` holds both keys from step 2 onward. |
| **Expected Audit** | One `RolePermissionsChanged` event with `Reason = "SPA staleness probe"`. |
| **Expected Notification** | n/a — no channel notifies the SPA of a privilege change. |
| **Cleanup** | Revoke the grant with reason `Probe complete`; deactivate `Ops-Probe`. |
| **Evidence** | Playwright screenshots at steps 1, 3 and 4 · the HAR network log · the `audit.audit_trail` payload |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the **only** cache in the system's privilege path, and it fails in the safe direction for a grant (a capability appears late) but in the **unsafe-looking** direction for a revoke: after a revoke the control remains rendered until reload and the click returns 403. That is affordance drift, not an access-control defect — the service's own doc says so at `permissions.service.ts:13-14` — but no requirement records the behaviour. Raised as **GAP-RBAC-905**. |

---

#### TC-RBAC-API-100 — The one surviving `[Authorize(Roles = …)]` gate refuses a Tenant Administrator on `/api/tenants`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-914 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — the tier-gated control plane against every tenant tier plus the platform tier |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Tenant Administrator, Quality Manager, External Auditor, then platform admin · n/a — this gate names a **tier**, not a permission key · `demo-lab` / platform |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `TenantsController.cs:12` carries `[Microsoft.AspNetCore.Authorization.Authorize(Roles = Roles.PlatformAdmin)]` — the only remaining role gate in `src/` and the only reference to any `NT.QAMS.WebApi.Authorization.Roles` constant. Its refusal is written by the framework result handler, not by `RequirePermissionAttribute` (`ProblemAuthorizationResultHandler.cs:27-32`). `ProvisionTenantCommand` additionally carries `[RequireRole(UserRole.PlatformAdmin)]` (`ProvisionTenant.cs:17`). |
| **Test Data** | `GET /api/tenants`; `POST /api/tenants` body `{"identifier":"probe-lab","name":"Probe Lab","adminEmail":"probe-admin@probe-lab.local","adminDisplayName":"Probe Admin","adminPassword":"Probe-Admin-Pass-1!"}` |
| **Steps** | 1. As `admin@demo-lab.local` (Tenant Administrator, holds all 171 tenant keys): `GET /api/tenants`; record status, `Content-Type`, `code`. 2. Same account: `POST /api/tenants` with the body; record status + `code`. 3. Repeat step 1 as `qm@demo-lab.local` and as `auditor@demo-lab.local`. 4. As `platform-admin@localhost` / `Dev-Only-Platform-Pass-1!`: `GET /api/tenants`; record status and the number of tenants returned. 5. `SELECT count(*) FROM saas.tenant WHERE identifier='probe-lab';`. |
| **Expected UI** | The platform shell (behind `platformOnlyGuard`) is the only place the tenants screen is reachable; a tenant user navigating there is redirected by the guard before any API call. |
| **Expected API** | Steps 1, 2 and 3 → `403 Forbidden`, `application/problem+json`, `code` = `AUTHZ-403`, `title` = `You do not have permission to perform this action.` — emitted by `ProblemAuthorizationResultHandler.cs:29-31`, **not** by the permission filter. Step 4 → `200 OK` with the tenant list. |
| **Expected DB** | Step 5 returns `0` — no tenant was provisioned by the refused `POST`. |
| **Expected Audit** | No `audit.audit_trail` row from steps 1–3. |
| **Expected Notification** | n/a. |
| **Cleanup** | None — nothing was created. |
| **Evidence** | Five HTTP captures · SQL result of step 5 |
| **Result / Defect** | Not Run · — |
| **Notes** | The refusal code is deliberately identical (`AUTHZ-403`) to the permission-filter refusal so the SPA has one handling path, which means **the code alone cannot tell an operator which mechanism refused**. Distinguish by route: only `/api/tenants/*` is tier-gated. Holding all 171 permission keys does not open this surface — that is the point of the case, and the strongest available evidence that the v1.51.0 conversion left the control plane on the tier gate deliberately. |

---

#### TC-RBAC-API-101 — The platform administrator passes every gate in the three groups, and an anonymous caller reaches none  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-914 |
| **Level / Type / Technique** | API · Functional (matrix + boundary) · Decision Table — DT-1 rows 1 and 2, the two rows that bypass the key check entirely |
| **Priority / Severity / Automation** | High · Critical · Yes (functional, table-driven) |
| **Role / Permission / Tenant** | Platform administrator, then anonymous · n/a — both rows bypass the per-key check · platform / none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `RequestIdentity.cs:114-117` sets `SetPlatformAdmin()` for tier `PlatformAdmin`, after which `Has(key)` returns true for every key (`PrivilegeResolution.cs:39`). For an anonymous caller `RequirePermissionAttribute.OnAuthorizationAsync` returns without denying (`RequirePermissionAttribute.cs:43-46`) and the framework's `[Authorize]` challenge produces the 401 (`ProblemAuthorizationResultHandler.cs:35-48`). The platform admin has **no** `tenant_id` claim (`SecurityAdapters.cs:93-96`), so `app.current_tenant` is unset and RLS is fail-closed for tenant tables. |
| **Test Data** | Gate probes: `GET /api/roles/catalog` (`roles.view`), `GET /api/reports/kpis` (`reports.view`), `GET /api/notifications/rules` (`notifications.manage`), `GET /api/tenant-settings/mfa-policy` (`tenant-settings.manage`) |
| **Steps** | 1. As `platform-admin@localhost`: issue the four probes; record status for each. 2. `GET /api/auth/me/privileges` as the platform admin; record `isPlatformAdmin`, `roleId`, `permissions`. 3. With **no** `Authorization` header: issue the same four probes; record status, `Content-Type` and `code`. 4. With a syntactically valid but expired JWT: repeat one probe; record status + `code`. |
| **Expected UI** | The platform shell exposes the control plane only; the tenant screens these routes back are not part of it. The SPA skips the privileges fetch entirely for platform admins (`permissions.service.ts:50-52`). |
| **Expected API** | Step 1 → the four probes pass the **authorization** gate: none returns `403 AUTHZ-403`. Record the actual statuses — `GET /api/roles/catalog` returns `200` (the catalogue is static and tenant-independent, `RolesSlice.cs:213-222`), while the tenant-scoped probes may return `200` with an empty payload or fail on the nil tenant context; assert the observed status per route and do not pre-declare it. Step 2 → `200 OK` with `"isPlatformAdmin": true`, `"roleId": null`, `"permissions": []` (`RolesSlice.cs:295-303` reads the request holder, which for a platform admin has no `_resolved`). Steps 3 and 4 → `401 Unauthorized`, `application/problem+json`, `code` = `AUTH-401` (`ProblemAuthorizationResultHandler.cs:19, 43-45`) — **never** `403`. |
| **Expected DB** | No writes. |
| **Expected Audit** | No `audit.audit_trail` rows — all probes are reads. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Ten HTTP captures · the platform admin's `/auth/me/privileges` body |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 2's `permissions: []` beside `isPlatformAdmin: true` is a deliberate asymmetry worth pinning: the platform admin passes every gate while reporting no keys, so any client that decides affordances from `permissions` alone would render nothing for them — which is exactly what the SPA does by short-circuiting on the tier instead (`permissions.service.ts:36-41, 68`). Run this case **before** any credential-burst probe; the auth rate-limit partition is 10/min (conventions §3). |

---

## Batch coverage note

**Covered.** 42 cases, `TC-RBAC-API-060` … `TC-RBAC-API-101`, all `Not Run · —`. By slice: **Analytical group** — `analytical-quality` `manage`/`create`/`edit`/`approve`/`sign` at their real gate sites (`AnalyticalQualityControllers.cs:21,28,65,88`, `UncertaintyController.cs:26,36,42,50,58`, `SigmaAssessmentsController.cs:26,35,44`, `CarryoverStudiesController.cs:26,52`), the ordering of the permission gate against `SOD-AQ-001` and against `CHANGE-REASON-REQUIRED`, and the three inert keys; `proficiency-testing` all four gated actions (`PtPlansController.cs:26,34,43,51,66`) plus its two inert keys. **Operations group** — `tasks.create`/`tasks.manage`, `notifications.manage` across all three routes with the External Auditor probe, and the full `reports` module including its previously unrecorded third action. **Administration group** — `organization.create`/`edit`/`manage` with the Quality Manager parity pin, the `tenant-settings` class-level gate, and `roles.view`/`roles.manage`. **Role/permission management** — the five `RolesController` write routes refused to the Quality Manager, `ROLE-005` against the brief's fictional codes plus a near-miss action name, and `ROLE-006` on the last-holder cell of DT-3. **Immediacy** — grant and revoke both verified on a byte-identical unexpired bearer token, plus the role-deactivation blackout. **Session interaction** — the tier route's `401 AUTH-007` contrasted against the configurable-role route's silence. **Cache** — the null-cache proof (no permission claim in the JWT, scoped per-request holder, three indexed reads) and the one real stale snapshot, in the SPA. **Platform gate** — the single `[Authorize(Roles = Roles.PlatformAdmin)]` on `TenantsController.cs:12`, refused to a Tenant Administrator holding every tenant key, plus the platform-admin bypass and the anonymous `AUTH-401` boundary.

**In scope but not covered, and why.** (1) Per-call-site enumeration of the 13 `analytical-quality.create` and 12 `analytical-quality.sign` sites: the twelve study-family controllers are structurally identical (`CarryoverStudiesController.cs:26,52` was read and confirms the shape), so TC-RBAC-API-062/063 stand for the family and a table-driven harness should expand them; one row per site would have consumed the batch without adding a distinct decision. (2) The `ROLE-006` boundary matrix beyond DT-3 row 2 — reserved to batch E's `TC-RBAC-BVA-*` block. (3) Concurrency on `EnsureSurvivesAsync`: the guard reads survivor counts outside any lock (`RolesSlice.cs:31-44`), so two simultaneous `PUT /roles/{id}/permissions` calls could both see a survivor and both remove one. No deterministic HTTP case can pin a race; it belongs to exploratory charter TC-RBAC-EXPL-002 and is left there. (4) The exact `audit.audit_trail.event_type` strings: `OutboxInterceptor.cs:67` builds `"{FullName}, {AssemblyName}"` and `OutboxProcessor.cs:126-127` forwards it verbatim, so the value is derivable, but the per-aggregate event names for QC profiles, PT plans, tasks, SLA definitions, notification rules and LOVs were not read in this pass; each affected case says to assert the exact string before execution rather than asserting an invented one. (5) `QualityHealthCategory` member names in TC-RBAC-API-080's body were not read from `src/NT.QAMS.Domain/Reporting/` and are flagged for substitution. (6) The frontend `can()` call sites behind the Analytical/Operations/Administration screens — batch F.

**Id-block discrepancy (raised, not silently reconciled).** The commissioning instruction for this batch reserves `TC-RBAC-API-060..`, while the module front matter's ID reservation table assigns `TC-RBAC-API-001…070` to batch B and `TC-RBAC-API-071…140` to batch C. Following the instruction, this file consumes **060–101**, which overlaps batch B's reserved 060–070 by eleven ids. No `11-module-rbac-cases-B.md` exists in `docs/testing/` at the time of writing, so nothing is currently corrupted, but the reservation table and the batch instructions disagree and one of them must be amended before batch B is authored. Recorded as **GAP-RBAC-900**.

**New gaps found in this pass** (numbered `GAP-RBAC-9xx` to avoid colliding with the front matter's 001–017):

- **GAP-RBAC-900 — Batch C's instructed id block overlaps batch B's reservation.** Source: this file's scope statement vs `11-module-rbac.md` ID reservation table rows 3 and 4. Impact: a traceability-matrix collision if batch B consumes 060–070. Acceptance criteria: the reservation table and the batch instructions name the same range for every batch, and no id appears in two case files. Severity: Medium. Responsible: Validation Lead.
- **GAP-RBAC-901 — The catalogue holds 171 keys, not 170.** Source: `PermissionCatalog.cs:182-183` gives the `reports` module three actions (`View, Export, Manage`), against `11-module-rbac.md` §4.1 row 28 (2 keys, `ReadOnlyModule`), §0 (170 keys) and URS-095 (`06-Revalidation-Delta-v1.38-v1.50.md:154`, "31 modules × 8 actions = 170 keys"). Summing §4.1's own per-module counts gives 170 only because `reports` is recorded as 2. Impact: the seeded Tenant Administrator holds 171 keys, so any test or requirement asserting 170 fails against the build; the arithmetic in §4.3 (QM 164, etc.) shifts by one. Acceptance criteria: URS-095 and the front-matter inventory read 171, or the figure is replaced by a value generated from `PermissionCatalog.AllKeys.Count` and pinned by a test. Severity: Medium. Responsible: Validation Lead + Solution Architect.
- **GAP-RBAC-902 — Reserved.** Not used; retained so the numbering of the gaps below is stable if 901 is split.
- **GAP-RBAC-903 — The endpoint-gate inventory omits `ReportsController` entirely.** Source: `ReportsController.cs:25,30,35,40,50,59,68` (7 `[RequirePermission]` sites) vs `11-module-rbac.md` §0 ("144 call sites across 37 controllers") and §4.1 (both `reports` keys marked ungated). Measured across `src/NT.QAMS.WebApi/Controllers/`: **151 sites across 38 controllers**; the raw grep over `src/NT.QAMS.WebApi/` returns 152, the 152nd being the XML-doc example at `RequirePermissionAttribute.cs:11`. 151 − 7 = 144 and 38 − 1 = 37, which identifies the omission exactly. Impact: the front matter's "78 keys reachable at an HTTP gate / 92 unreached" becomes **80 / 91**, and GAP-RBAC-003's population changes; a reviewer reconciling the URS's 127 against the front matter's 144 against the measured 151 finds three different numbers. Acceptance criteria: the inventory includes `ReportsController`, the counts are regenerated by a repeatable command, and a CI check fails when the recorded figure and the measured figure diverge. Severity: Medium. Responsible: Validation Lead.
- **GAP-RBAC-904 — Three command-policy sites and two policy keys are missing from the front matter, and two of the three are inert.** Source: `QualityAnalyticsQuery.cs:23`, `QualityHealthProfileSlice.cs:14` and `:51` vs `11-module-rbac.md` §1.6 ("12 `[RequirePermissionPolicy]` call sites over 3 distinct keys"). Measured: **15 sites over 5 keys** (`roles.manage` ×4, `users.manage` ×7, `documents.sign` ×1, `reports.view` ×2, `reports.manage` ×1). Further, the two `reports.view` declarations sit on `IQuery<T>` types, and `AuthorizationBehavior` returns before evaluating any policy when `IsCommand` is false (`AuthorizationBehavior.cs:34-37, 44-47`) — so those two attributes enforce nothing and only `ReportsController.cs:25,50,59` protects the routes. Impact: an attribute that reads as defence-in-depth provides none; if the controller gate were ever removed the queries would be open. `CommandPolicyTests` does not catch this because it asserts only that every `ICommand` carries exactly one policy — it says nothing about policies on queries. Acceptance criteria: an architecture test fails when a `CommandPolicyAttribute` is applied to a type that is not an `ICommand`/`ICommand<T>`; the two query attributes are either removed or the behaviour is extended to evaluate them. Severity: Medium. Responsible: Solution Architect + Lead Developer.
- **GAP-RBAC-905 — No requirement records the SPA privilege snapshot's staleness window.** Source: `permissions.service.ts:43-65` (fetch on session change only; no polling, no push, SignalR absent). Impact: after a revoke, a control the server will refuse remains rendered until the next full page load; after a grant, an authorised control stays hidden. Not a security defect (`permissions.service.ts:13-14` states the affordance-only contract) but it is an undocumented behaviour a user-access reviewer will encounter. Acceptance criteria: either the privileges fetch is re-run on a privilege-change signal or on navigation, or a requirement records the staleness window and the reload remedy. Severity: Low. Responsible: Frontend Lead + Product Owner.
- **GAP-RBAC-906 — Reserved.** Not used.
- **GAP-RBAC-907 — `CHANGE-REASON-REQUIRED` is evaluated before authentication and authorization on DELETE routes.** Source: `Program.cs:263-272` — `ChangeReasonMiddleware` is registered at `:269`, before `app.UseAuthorization()` at `:270` and before the MVC filter pipeline that runs `RequirePermissionAttribute`; the middleware itself (`RequestIdentity.cs:149-156`) inspects only the verb and the header. Impact: an unprivileged — and indeed an anonymous — caller issuing a DELETE without `X-Change-Reason` receives `400 CHANGE-REASON-REQUIRED` rather than `401 AUTH-401` or `403 AUTHZ-403`, distinguishing an existing DELETE route from a non-existent one before any credential is checked. Every DELETE in the build is affected, including `/api/pt-plans/{id}/items/{itemId}`, `/api/uncertainty-budgets/{id}/components/{componentId}` and `/api/archives/{id}/legal-hold`. Small disclosure, easy fix. Acceptance criteria: an anonymous or unprivileged DELETE without the header returns `401`/`403` respectively, and the reason check runs only for callers that pass authorization — i.e. the middleware moves after `UseAuthorization()`, or becomes an MVC filter ordered after the permission filter. Severity: Low–Medium. Responsible: Lead Developer + Security reviewer.
- **GAP-RBAC-908 — The opposite session consequences of the two role-change routes are unrecorded.** Source: `UserManagement.cs:115-128` (`ChangeUserRoleHandler` mutates `user_account.role`, so `RequestIdentity.cs:104-109` fires `401 AUTH-007` on the next request) vs `:205-232` (`AssignUserRoleHandler` mutates only `role_id`, so no challenge occurs). Impact: an administrator changing a user's tier silently ends that user's session, while changing their configurable role does not; no requirement, XML comment or support runbook states the difference, and the affected user sees only "Your permissions have changed. Please sign in again." Acceptance criteria: a requirement records which privilege-administration operations terminate the target's session, and the administration UI warns before the operation that does. Severity: Low. Responsible: Product Owner + Lead Developer.

**Honesty statement.** Every `[IV]` claim in this batch cites a file and line that was opened and read in this pass: `PermissionCatalog.cs`, `Role.cs`, `RolesSlice.cs`, `SystemRoleCatalog.cs`, `AuthorizationBehavior.cs`, `PrivilegeResolution.cs`, `RequestIdentity.cs`, `RequirePermissionAttribute.cs`, `ProblemAuthorizationResultHandler.cs`, `SecurityAdapters.cs`, `Program.cs` (pipeline order only), `UserManagement.cs` (the four handlers cited), `QualityHealthProfileSlice.cs`, `QualityAnalyticsQuery.cs`, `permissions.service.ts`, and the controllers `Roles`, `TenantSettings`, `Tenants`, `AnalyticalQuality`, `PtPlans`, `Uncertainty`, `SigmaAssessments`, `CarryoverStudies`, `Operations`, `Platform`, `Reports`, `Auth` (the two `me/*` actions). Three classes of claim are explicitly **not** verified and are flagged in the case that makes them: the per-aggregate domain-event type strings for the Operations and Analytical modules; the `qams.notification_rule` rule set that decides whether a task assignment dispatches a notification; and the `QualityHealthCategory` enum members. No case asserts a behaviour that was not read, no case records a result other than `Not Run`, and no case names a commissioning-brief privilege code except TC-RBAC-API-091, which exists precisely to prove such a code is refused with `ROLE-005`.
