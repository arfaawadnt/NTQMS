# TENANT — Detailed Test Cases, Batch B

This batch authors **the cross-tenant attack suite** for module `TENANT`: twenty-eight detailed cases in which a fully authenticated, fully privileged user of **Tenant A** deliberately targets a record belonging to **Tenant B**, one or more cases per attack surface — read by id, update, delete, list/search, export, file download, notification retrieval, report/dashboard aggregation, id enumeration, bulk operation, and attempting to make a background job act on B. Every case asserts the **exact** outcome that the code produces on that specific path (404 with a named domain code, 403 `AUTHZ-403`, 403 with the misclassified `AUTHZ-404`, 400 `CHANGE-REASON-REQUIRED`, or HTTP 200 with a provably empty/own-tenant-only result set — determined per path from source, never assumed uniform), and asserts that **no field of Tenant B appears anywhere in the response body, the `problem+json` `detail`/`title`, or the response headers**. It deliberately leaves to sibling batches: the pure-domain `Tenant` aggregate, slug/name boundary and state-transition cases (batch A, `TC-TENANT-UNIT/BVA/EP/STATE/API/DT-001…006`); per-table RLS predicate, fail-closed, `WITH CHECK`, elevation and owned-child isolation cases and the real-PostgreSQL structural sweeps (batch B's sibling slices, `TC-TENANT-RLS-nnn`, `TC-TENANT-INT-nnn`, `TC-TENANT-DT-007…012`, `TC-TENANT-MUT-nnn`); and tenant-resolution middleware, GUC forgery, spoofed header/query/host, platform-admin null tenant, MC/DC over the composed query filter, and SPA-level isolation (batch C, `TC-TENANT-SEC-021…030` as currently reserved — see **GAP-TENANT-901**, `TC-TENANT-COMP/E2E/MCDC/PATH/DF/OBS/DR/PERF/A11Y-nnn`).

**Shared fixture referenced by every case below.** Two tenants provisioned via `POST /api/tenants` as the platform administrator, then seeded through the ordinary tenant APIs. All identifiers are fixed so the cases are re-runnable and the traceability matrix can key on them.

| Symbol | Value | Source of truth |
|---|---|---|
| Tenant A | slug `alpha-lab`, `saas.tenant.id = 01990000-0000-7000-8000-0000000000aa` | `saas.tenant`, `TenantConfiguration.cs:11-20` |
| Tenant B | slug `beta-lab`, `saas.tenant.id = 01990000-0000-7000-8000-0000000000bb` | same |
| Attacker | `qm@alpha-lab.local` / `Alpha-QM-Pass-1!`, `qams.user_account.id = 01990001-0000-7000-8000-0000000000a1`, tenant A, role tier `QualityManager`, assigned a tenant role holding the full permission set under test | `UserManagement.cs:48-90` |
| B's NC | `qams.nonconformance.id = 01990002-0000-7000-8000-0000000000b1`, `nc_ref = 'NC-2026-0007'`, `status = 'Raised'` | `NcWorkflowCommands.cs:43-51` |
| B's test authorization | `qams.test_authorization.id = 01990003-0000-7000-8000-0000000000b2` | `AuthorizationSlice.cs:126-128` |
| B's equipment | `qams.equipment_item.id = 01990004-0000-7000-8000-0000000000b3` | `EquipmentSlice.cs:85` |
| B's method-comparison study / pair | `qams.method_comparison_study.id = 01990005-0000-7000-8000-0000000000b4`, `qams.measurement_pair.id = 01990006-0000-7000-8000-0000000000b5` | `MethodComparisonSlice.cs:91-93`; `MethodComparisonStudy.cs:115-122` |
| B's archive entry | `qams.archive_entry.id = 01990007-0000-7000-8000-0000000000b6`, `is_on_legal_hold = true` | `Records` slice `ArchiveLoader.LoadAsync:79-81` |
| B's user | `qams.user_account.id = 01990008-0000-7000-8000-0000000000b7`, `analyst@beta-lab.local` | `UserManagement.cs:105-113` |
| B's file | `qams.file_reference.id = 01990009-0000-7000-8000-0000000000b8`, `sha256 = 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855'`, `storage_key = '01990000000070008000000000000bb/e3b0…b855'` | `FilesController.cs:59-72`; `LocalFileStorage.cs:57` |
| B's notification dispatch | `qams.notification_dispatch.id = 0199000a-0000-7000-8000-0000000000b9`, `recipient_user_id = 01990008-…-0000000000b7` | `NotificationSlice.cs:103-105` |
| B's management review | `qams.management_review.id = 0199000b-0000-7000-8000-0000000000ba` | `RiskGovernanceSlice.cs:412-419` |
| B's branch | `qams.branch.id = 0199000c-0000-7000-8000-0000000000bb` | `PlatformControllers.cs:22` |
| B's document | `qams.controlled_document.id = 0199000d-0000-7000-8000-0000000000bc`, title contains `BETA-SOP-QC-001` | `DocumentsController.cs:24-33` |
| Never-existing id | `01990fff-0000-7000-8000-0000000000ff` (no row in any table, either tenant) | control value |
| Risk ids | `RSK-TENANT-001` cross-tenant read · `RSK-TENANT-002` cross-tenant write/mutation · `RSK-TENANT-003` inference/enumeration disclosure · `RSK-TENANT-004` aggregate/export leakage · `RSK-TENANT-005` background-job cross-tenant action | **Minted here.** `docs/validation/02-Functional-Risk-Assessment.md:51` carries the area row *Tenant isolation / URS-008 / S=High P=Med D=Low / **HIGH*** but assigns no `RSK-` identifier, so these five are new per conventions §5 |

**Standing assertion carried by every case in this batch (asserted, never assumed):** the response body, every `problem+json` extension (`title`, `detail`, `code`, `traceId`, `correlationId`), and every response header contain **no** Tenant B `tenant_id`, primary key, reference string (`nc_ref`, `study_ref`, `archive_ref`, `review_ref`), email address, file name, SHA-256, table name, column name, constraint name or SQL fragment. Where a case has a shorter Expected-API cell, this standing assertion still applies and is listed in that case's Evidence row as *body-diff against the B-field deny-list*.

---

#### TC-TENANT-SEC-001 — Cross-tenant read by id: Tenant A requests Tenant B's nonconformance  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-001 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — partition "id exists, but in a foreign tenant" against the partitions "id exists in own tenant" and "id exists nowhere" |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional, `WebApi.FunctionalTests`) |
| **Role / Permission / Tenant** | QualityManager of A · n/a — `GET /api/nonconformances/{id}` carries no `[RequirePermission]` (`NonconformancesController.cs:30-32`), only class-level `[Authorize]` (`:20`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`, runtime role `qams_app` |
| **Preconditions** | Both tenants provisioned. B's NC row exists: `SELECT count(*) FROM qams.nonconformance WHERE id='01990002-0000-7000-8000-0000000000b1'` returns 1 when run with `SELECT set_config('app.bypass_rls','on',false);` first. A holds a valid access JWT whose `tenant_id` claim is `01990000-0000-7000-8000-0000000000aa`. |
| **Test Data** | Target id `01990002-0000-7000-8000-0000000000b1` (B's NC, `nc_ref='NC-2026-0007'`) |
| **Steps** | 1. Sign in as `qm@alpha-lab.local` / `Alpha-QM-Pass-1!` at `POST /api/auth/login` and capture the access token. 2. `curl.exe -i -H "Authorization: Bearer <A-token>" http://localhost:5080/api/nonconformances/01990002-0000-7000-8000-0000000000b1`. 3. Record status, `Content-Type`, and the full body. 4. Repeat identically against `/api/v1/nonconformances/01990002-0000-7000-8000-0000000000b1` (the versioned mirror). 5. Grep the two bodies for the literals `NC-2026-0007`, `0000000000bb`, `beta`, `nonconformance`. |
| **Expected UI** | n/a — API-level case driven with `curl.exe`; the SPA equivalent is covered by batch C `TC-TENANT-E2E-nnn`. |
| **Expected API** | `404 Not Found`, `Content-Type: application/problem+json`, body `{"title":"Nonconformance not found.","status":404,"code":"NC-404","traceId":"…","correlationId":"…"}`. The code is `NC-404` from `src/NT.QAMS.Application/Improvement/Queries/NcQueries.cs:59`, mapped to 404 by the `-404`-suffix arm at `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:69-74`, written by `ProblemResponse.WriteAsync` (`ProblemResponse.cs:22-35`). Step 4 returns a byte-identical body except `traceId`/`correlationId`. Steps 5: zero matches for `NC-2026-0007`, `0000000000bb`, `beta`. |
| **Expected DB** | No change. `SELECT xmin, status FROM qams.nonconformance WHERE id='01990002-…b1'` (under `set_config('app.bypass_rls','on',false)`) is unchanged from the pre-step reading — the read never reached the row: `SingleOrDefaultAsync` returned null because the EF global filter (`AppDbContext.cs:190`) narrowed to `tenant_id = 01990000-…aa` and PostgreSQL RLS `USING` narrowed identically. |
| **Expected Audit** | No `audit.audit_trail` row (reads are not ledgered) and no `audit.security_event` row — `ISecurityEventLog.WriteAsync` is not called on a read-by-id path (`NcQueries.cs:50-71` injects only `IAppDbContext`). Verify by comparing `SELECT max(occurred_at_utc) FROM audit.security_event` before and after. |
| **Expected Notification** | n/a — no notification policy subscribes to a query (`NotificationPolicies.cs:40-130` handles domain events only). |
| **Cleanup** | None — the case writes nothing. Revoke the A session with `POST /api/auth/logout`. |
| **Evidence** | HTTP response capture (both routes) · `psql` before/after row state · body-diff against the B-field deny-list |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the reference case for the batch: 404 here is produced by the **handler's null-check**, not by an authorization gate, because the two isolation layers made the row invisible rather than forbidden. Do not generalise the code — `NC-404` is module-local; see TC-TENANT-SEC-002 and -003 for two paths that answer differently. |

#### TC-TENANT-SEC-002 — Cross-tenant read by id returns **403**, not 404, because the code prefix wins over the suffix  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-001, RSK-TENANT-003 |
| **Level / Type / Technique** | API · Security (negative) · Multiple-Condition coverage over the `DomainExceptionHandler` switch — the pair (`Code.StartsWith("AUTHZ-")` = true, `Code.EndsWith("-404")` = true) is a reachable combination whose arm order decides the status |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A · n/a — `GET /api/test-authorizations/{id}` carries no `[RequirePermission]` (`TestAuthorizationsController.cs:22-24`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | B's test authorization exists: `qams.test_authorization.id = 01990003-0000-7000-8000-0000000000b2`, `tenant_id = 01990000-…bb`. A also owns at least one test authorization, so the endpoint is demonstrably reachable and functional for A. |
| **Test Data** | Target id `01990003-0000-7000-8000-0000000000b2`; control id = A's own authorization id; control id `01990fff-0000-7000-8000-0000000000ff` |
| **Steps** | 1. As A, `GET /api/test-authorizations/<A-own-id>` — expect `200` (proves the route works for A). 2. `GET /api/test-authorizations/01990003-0000-7000-8000-0000000000b2`. 3. `GET /api/test-authorizations/01990fff-0000-7000-8000-0000000000ff`. 4. Compare the status codes and the `code` extension of steps 2 and 3. 5. Compare step 2's status against TC-TENANT-SEC-001's status for the structurally identical attack. |
| **Expected UI** | n/a — API-level case; the SPA renders a generic "You do not have permission" for 403, which is itself the misleading symptom this case pins. |
| **Expected API** | Step 1 `200`. Steps 2 and 3 both `403 Forbidden`, `application/problem+json`, `{"title":"Test authorization not found.","status":403,"code":"AUTHZ-404",…}`. Reason, read from source: the handler throws `DomainException("AUTHZ-404", …)` at `src/NT.QAMS.Application/Competency/AuthorizationSlice.cs:175`, and `DomainExceptionHandler.cs:63` (`StartsWith("AUTHZ-")` → **403**) is evaluated **before** `:69` (`EndsWith("-404")` → 404), so the not-found is reported as a permission refusal. Step 5: the same class of attack yields `404 NC-404` on one module and `403 AUTHZ-404` on another. |
| **Expected DB** | No change to `qams.test_authorization`; row invisible to A at both layers. |
| **Expected Audit** | None — read path, no `ISecurityEventLog` call in `AuthorizationSlice.cs:168-187`. |
| **Expected Notification** | n/a — read path. |
| **Cleanup** | None. |
| **Evidence** | Three HTTP captures · the `DomainExceptionHandler.cs:63,69` arm-order excerpt · body-diff against the B-field deny-list |
| **Result / Defect** | Not Run · — |
| **Notes** | **This case pins a defect, deliberately.** `AUTHZ-404` is a not-found condition wearing an authorization prefix; the status is wrong and the isolation surface is inconsistent across modules. Raised as **GAP-TENANT-902**. Do not "fix" the expectation to 404 during execution — record the observed 403 and the defect id. Confidentiality is not breached (both a foreign row and a non-existent row give the same answer), so the severity is Medium, not Critical. |

#### TC-TENANT-SEC-003 — Cross-tenant read by id on a second module confirms the 404 partition is not module-specific  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-001 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — a second member of the "foreign-tenant id" partition, chosen from a different aggregate and a different application slice |
| **Priority / Severity / Automation** | High · Critical · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A · n/a — `GET /api/equipment/{id}` (`EquipmentController.cs:23`) has no `[RequirePermission]` · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qams.equipment_item.id = 01990004-0000-7000-8000-0000000000b3` exists with `tenant_id = 01990000-…bb`. |
| **Test Data** | Target id `01990004-0000-7000-8000-0000000000b3` |
| **Steps** | 1. As A, `GET /api/equipment/01990004-0000-7000-8000-0000000000b3`. 2. Record status, `code`, body length in bytes. 3. `GET /api/equipment/01990fff-0000-7000-8000-0000000000ff`. 4. Assert the two bodies are identical apart from `traceId` and `correlationId`. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Both requests `404 Not Found`, `application/problem+json`, `{"title":"Equipment not found.","status":404,"code":"EQP-404",…}` from `src/NT.QAMS.Application/Equipment/EquipmentSlice.cs:85`. Byte-identical bodies apart from the two id extensions. |
| **Expected DB** | `qams.equipment_item` row for B unchanged (compare `xmin`, `status`, `next_calibration_due` before and after under `bypass_rls`). |
| **Expected Audit** | No new `audit.security_event` or `audit.audit_trail` row. |
| **Expected Notification** | n/a — read path. |
| **Cleanup** | None. |
| **Evidence** | Two HTTP captures · byte-diff of the two bodies · `psql` row state |
| **Result / Defect** | Not Run · — |
| **Notes** | Together with TC-TENANT-SEC-001 this establishes that `404 <MODULE>-404` is the *majority* outcome for cross-tenant read-by-id; TC-TENANT-SEC-002 is the documented exception. The traceability matrix must not record a single "expected 404" for the whole surface. |

#### TC-TENANT-SEC-004 — Cross-tenant update: Tenant A triages Tenant B's nonconformance  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-002 |
| **Level / Type / Technique** | API · Security (negative) · State Transition — the attacker attempts the legal `Raised → Assigned` edge (`Nonconformance.cs:157-163`) on a record in another tenant's state machine |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A **holding `nonconformances.approve`** (so the gate at `NonconformancesController.cs:53` passes and the tenant boundary is what is on trial) · `nonconformances.approve` · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | B's NC is `status='Raised'` (the only state from which `Triage` is legal — `Nonconformance.cs:159`). A's role grants `nonconformances.approve`; confirm with `SELECT p.permission_key FROM qams.role_permission p JOIN qams.role r ON r.id=p.role_id WHERE r.tenant_id='01990000-…aa'` returning `nonconformances.approve`. |
| **Test Data** | `POST /api/nonconformances/01990002-0000-7000-8000-0000000000b1/triage` body `{"assigneeId":"01990001-0000-7000-8000-0000000000a1"}` |
| **Steps** | 1. As A, send the POST above. 2. Record status and body. 3. `SELECT status, assigned_to, xmin FROM qams.nonconformance WHERE id='01990002-…b1'` under `SELECT set_config('app.bypass_rls','on',false);`. 4. `SELECT count(*) FROM audit.field_change WHERE entity_id='01990002-0000-7000-8000-0000000000b1'`. 5. Repeat step 1 with the assignee set to B's own user `01990008-…b7` to confirm the outcome does not change. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | `404 Not Found`, `application/problem+json`, `code = "NC-404"`, title `"Nonconformance not found."` — thrown by the shared loader `NcLoader.LoadAsync` (`src/NT.QAMS.Application/Improvement/Commands/NcWorkflowCommands.cs:97-106`) because the tenant-filtered `SingleOrDefaultAsync` returned null. Step 5 identical. |
| **Expected DB** | `qams.nonconformance` for B: `status` still `'Raised'`, `assigned_to` still `NULL`, `xmin` **unchanged** (no write was attempted, so this is not even an RLS `WITH CHECK` refusal — the row was never loaded). Row count of `qams.nonconformance` unchanged in both tenants. |
| **Expected Audit** | Step 4 returns the pre-existing count, unchanged — `FieldChangeInterceptor` only stamps rows on an actual `SaveChanges` mutation, and none occurred. No `audit.audit_trail` entry, no `qams.outbox_event` row. |
| **Expected Notification** | **No** `qams.notification_dispatch` row for the `NcTriaged` key in either tenant — the domain event was never raised because `Triage()` was never called. Assert `SELECT count(*) FROM qams.notification_dispatch` is unchanged in A and, under bypass, in B. |
| **Cleanup** | None — nothing was written. |
| **Evidence** | HTTP capture · `psql` row + `xmin` before/after · `audit.field_change` count · `notification_dispatch` count |
| **Result / Defect** | Not Run · — |
| **Notes** | The distinguishing assertion is `xmin` unchanged: it separates "the write was refused by the database" from "the write was never attempted". On this path it is the latter, because the load is filtered first. A future refactor that loads with `IgnoreQueryFilters()` would still be caught by RLS but would change `xmin` semantics and the status code, so this assertion is the regression detector. |

#### TC-TENANT-SEC-005 — The permission gate fires before the tenant boundary: an unprivileged A user attacking B gets 403, not 404  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, RBAC · URS-005, URS-008 · RSK-TENANT-002, RSK-TENANT-003 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — two independent conditions (caller holds `nonconformances.approve`? / target belongs to caller's tenant?) over four rule columns, of which this case executes rule (No, No) and TC-TENANT-SEC-004 executes rule (Yes, No) |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst of A, on a tenant role **without** `nonconformances.approve` · required key `nonconformances.approve` (`NonconformancesController.cs:53`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `analyst@alpha-lab.local` exists on a role whose `qams.role_permission` rows do **not** include `nonconformances.approve`. B's NC is `status='Raised'`. |
| **Test Data** | `POST /api/nonconformances/01990002-0000-7000-8000-0000000000b1/triage` body `{"assigneeId":"01990001-0000-7000-8000-0000000000a1"}` as the A analyst |
| **Steps** | 1. Sign in as `analyst@alpha-lab.local`. 2. Send the POST above against **B's** NC id. 3. Record status and `code`. 4. Send the same POST against **A's own** NC id. 5. Compare steps 3 and 4 — both must be the same refusal, so the attacker learns nothing about whether the id exists in a foreign tenant. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Steps 2 and 4 both `403 Forbidden`, `application/problem+json`, `{"title":"You do not have permission to perform this action.","status":403,"code":"AUTHZ-403",…}` written by `RequirePermissionAttribute.OnAuthorizationAsync` (`src/NT.QAMS.WebApi/Authorization/RequirePermissionAttribute.cs:52-62`) using the same constant as the framework path (`ProblemAuthorizationResultHandler.ForbiddenCode`, `ProblemAuthorizationResultHandler.cs:16`). **The handler is never reached**, so `NC-404` never appears. |
| **Expected DB** | No change anywhere. `qams.nonconformance` untouched in both tenants. |
| **Expected Audit** | No `audit.field_change`, no `audit.audit_trail`. Whether an `audit.security_event` of an authorization refusal is written is **not asserted here** — `RequirePermissionAttribute.cs:52-62` calls only `ProblemResponse.WriteAsync` and injects no `ISecurityEventLog`, so the expectation is **no row**; capture `SELECT count(*) FROM audit.security_event` before/after to confirm. |
| **Expected Notification** | n/a — the request never reaches a command handler. |
| **Cleanup** | None. |
| **Evidence** | Two HTTP captures · `RequirePermissionAttribute.cs:52-62` excerpt · `audit.security_event` count |
| **Result / Defect** | Not Run · — |
| **Notes** | Ordering matters for the whole batch: `TenantResolutionMiddleware` (`Program.cs:266`) → `[RequirePermission]` authorization filter → handler → EF filter/RLS. A privileged attacker therefore reaches the tenant boundary and sees 404; an unprivileged one is stopped earlier and sees 403. Both are safe; the *pair* is the assertion. |

#### TC-TENANT-SEC-006 — Cross-tenant update of a user account, the one table outside RLS (accepted deviation B9)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, RBAC · URS-005, URS-008 · RSK-TENANT-002 |
| **Level / Type / Technique** | API · Security (negative) · Statement/Branch coverage of `TenantUserLoader.LoadAsync` — the false branch of `u.Id == id && u.TenantId == tenantId` |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin of A holding `users.manage` · `users.manage` (`UsersController.cs:43`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qams.user_account` has **no RLS**: verified `SELECT relrowsecurity, relforcerowsecurity FROM pg_class WHERE relname='user_account'` returns `f, f` (accepted deviation B9, front matter §4.4). B's user `01990008-…b7` exists with `tenant_id = 01990000-…bb`. A's admin holds `users.manage`. A role owned by tenant A exists with `qams.role.id = 0199000e-0000-7000-8000-0000000000a2`. |
| **Test Data** | `PUT /api/users/01990008-0000-7000-8000-0000000000b7/assigned-role` body `{"roleId":"0199000e-0000-7000-8000-0000000000a2"}` |
| **Steps** | 1. As A's tenant admin, send the PUT above. 2. Record status and `code`. 3. `SELECT role_id, tenant_id, is_active, xmin FROM qams.user_account WHERE id='01990008-…b7'` (no bypass needed — the table has no RLS). 4. Also attempt `POST /api/users/01990008-…b7/deactivate` and `POST /api/users/01990008-…b7/reset-password` with `{"newPassword":"Alpha-Reset-Pass-9!"}`. 5. `GET /api/users` as A and assert B's user is absent from the list. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Steps 1 and 4 all `404 Not Found`, `application/problem+json`, `{"title":"User not found.","status":404,"code":"USER-404",…}` from `src/NT.QAMS.Application/IdentityAccess/Commands/UserManagement.cs:112`, reached because line `:111` requires `u.TenantId == tenantId` and line `:110` throws `TENANT-000` if no tenant is resolved. Step 5: `200` with an array containing only tenant-A users (`GetUsersHandler`, `UserManagement.cs:161-163`). |
| **Expected DB** | `qams.user_account` row `01990008-…b7`: `role_id`, `is_active`, `password_hash` and `xmin` all **unchanged**. Since the table is outside RLS, the *only* thing that stopped this was the explicit `TenantId ==` predicate in application code — that is precisely the B9 compensating control, and this case is its executable proof. |
| **Expected Audit** | No `audit.field_change` row with `entity_type='UserAccount'` and `entity_id='01990008-0000-7000-8000-0000000000b7'`. |
| **Expected Notification** | n/a — no notification policy handles user administration events on this path. |
| **Cleanup** | None — no write occurred. If the reset-password step ever succeeds, that is a Critical defect and B's password hash must be restored from the pre-test capture. |
| **Evidence** | Four HTTP captures · `pg_class` RLS reading for `user_account` · row + `xmin` before/after · `GET /api/users` payload |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the highest-value case in the batch: on every other table two independent layers fence the attack; here there is exactly **one**, written in C#. `tests/NT.QAMS.Architecture.Tests/UserAccountTenantBoundTests.cs:49-68` guards the source shape at build time, but only this case proves the runtime behaviour end to end. Failure here reopens accepted deviation B9 under its revisit trigger 3. |

#### TC-TENANT-SEC-007 — Cross-tenant delete of an owned child row  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-101 · RSK-TENANT-002 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning over the owned-child partition — the child (`qams.measurement_pair`) is reached only through its parent aggregate, so the parent load is the boundary |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A · n/a — `DELETE /api/method-comparisons/{id}/pairs/{pairId}` carries no `[RequirePermission]` (`MethodComparisonsController.cs:46-51`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | B's study `01990005-…b4` is in an editable state (`RequireEditable()`, `MethodComparisonStudy.cs:117`) and owns pair `01990006-…b5`. `qams.measurement_pair` is one of the 30 `Hardening4_ChildTenancy` children: `relrowsecurity=t, relforcerowsecurity=t`, policy `tenant_isolation`, `tenant_id NOT NULL`. |
| **Test Data** | `DELETE /api/method-comparisons/01990005-0000-7000-8000-0000000000b4/pairs/01990006-0000-7000-8000-0000000000b5` with header `X-Change-Reason: cross-tenant isolation test TC-TENANT-SEC-007` |
| **Steps** | 1. As A, send the DELETE above **with** the `X-Change-Reason` header. 2. Record status and `code`. 3. `SELECT count(*) FROM qams.measurement_pair WHERE id='01990006-…b5'` under `SELECT set_config('app.bypass_rls','on',false);`. 4. `SELECT pair_count, xmin FROM qams.method_comparison_study WHERE id='01990005-…b4'` under bypass. 5. Repeat the DELETE with the parent id set to **A's own** study and the pair id still B's. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Step 1 `404 Not Found`, `application/problem+json`, `code = "MC-404"`, title `"Method-comparison study not found."` — the **parent** loader `MethodComparisonWorkflowHandlers.LoadAsync` (`src/NT.QAMS.Application/AnalyticalQuality/MethodComparisonSlice.cs:91-93`) fails first, so the child id is never evaluated. Step 5 `404` with `code = "MC-404"` and title `"Measurement pair not found."` from `src/NT.QAMS.Domain/AnalyticalQuality/MethodComparisonStudy.cs:119` — the same code string carries two different messages; assert the **message** to distinguish which guard fired. |
| **Expected DB** | Step 3 returns 1 — B's pair still exists. Step 4: `pair_count` and `xmin` on B's study unchanged. Step 5 causes no row change in either tenant. |
| **Expected Audit** | No `audit.field_change` row with `action='Delete'` and `entity_id='01990006-0000-7000-8000-0000000000b5'`. Note the scoped change reason **is** captured by `ChangeReasonMiddleware` (`RequestIdentity.cs:158`) before the failure, but with no mutation there is nothing for `FieldChangeInterceptor` to stamp it onto. |
| **Expected Notification** | n/a — no notification policy handles measurement-pair removal. |
| **Cleanup** | None. |
| **Evidence** | Two HTTP captures · `psql` child row count and parent `pair_count`/`xmin` · the two distinct `MC-404` messages side by side |
| **Result / Defect** | Not Run · — |
| **Notes** | The interesting property is that the child is unreachable **twice over**: the parent load is tenant-filtered, and even a direct child write would be refused by the tenant-composite FK (`Hardening4_ChildTenancy.cs:353-435`, SqlState `23503`) and by the child's own `tenant_isolation` policy. This case exercises only the first; the other two belong to sibling batch-B slices `TC-TENANT-RLS-nnn` / `TC-TENANT-INT-nnn`. |

#### TC-TENANT-SEC-008 — A cross-tenant DELETE without `X-Change-Reason` is refused before the tenant boundary is consulted  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, LEDGER · URS-008 · RSK-TENANT-003 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table on middleware ordering — conditions (header present? / privileged? / same tenant?) resolved by pipeline position, `Program.cs:269` before `:270` before `:272` |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A · n/a — refusal precedes authorization · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Same fixture as TC-TENANT-SEC-007. `ChangeReasonMiddleware` is registered at `src/NT.QAMS.WebApi/Program.cs:269`, i.e. **before** `UseAuthorization()` (`:270`) and `MapControllers()` (`:272`). |
| **Test Data** | Three DELETEs, all **without** `X-Change-Reason`: (a) B's study + B's pair; (b) A's own study + A's own pair; (c) a wholly non-existent study `01990fff-…ff` + pair `01990fff-…ff` |
| **Steps** | 1. As A, send DELETE (a). 2. Send DELETE (b). 3. Send DELETE (c). 4. Compare the three statuses, `code` values and bodies. 5. Repeat (a) with the header set to a whitespace-only value `"   "`. |
| **Expected UI** | The SPA never produces this request: `changeReasonInterceptor` collects a reason through the accessible dialog before any DELETE leaves the browser. The case is executed with `curl.exe` to bypass the SPA. |
| **Expected API** | All of (a), (b), (c) and step 5 return `400 Bad Request`, `application/problem+json`, `{"title":"A reason is required for this change.","status":400,"code":"CHANGE-REASON-REQUIRED",…}` from `src/NT.QAMS.WebApi/Middleware/RequestIdentity.cs:149-156` (whitespace is caught by `string.IsNullOrWhiteSpace` at `:149`). The four bodies are identical apart from `traceId`/`correlationId`. **No `MC-404`, no `AUTHZ-403`.** |
| **Expected DB** | No change in either tenant. |
| **Expected Audit** | No `audit.field_change` row; the request is short-circuited at `:155` and `next(context)` is never invoked. |
| **Expected Notification** | n/a — request terminated in middleware. |
| **Cleanup** | None. |
| **Evidence** | Four HTTP captures · pipeline-order excerpt `Program.cs:266-272` · body-identity diff |
| **Result / Defect** | Not Run · — |
| **Notes** | Security value: the reason-for-change gate is an unintended but useful anti-enumeration layer on the 26 DELETE routes — a foreign id, an own id and a non-existent id are indistinguishable while the header is absent. That is a property to preserve, so it is written as an assertion rather than an observation. |

#### TC-TENANT-SEC-009 — Cross-tenant delete of a legal hold on another tenant's archived record  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, REC · URS-008 · RSK-TENANT-002 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — the "foreign-tenant id" partition applied to a **retention-controlled** record, where a successful attack would have regulatory consequences |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A holding `records.void` · `records.void` (`OperationsControllers.cs:63`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qams.archive_entry.id = 01990007-0000-7000-8000-0000000000b6`, `tenant_id = 01990000-…bb`, `is_on_legal_hold = true`, `legal_hold_reason` populated. A's role grants `records.void`. |
| **Test Data** | `DELETE /api/archives/01990007-0000-7000-8000-0000000000b6/legal-hold` with header `X-Change-Reason: cross-tenant isolation test TC-TENANT-SEC-009` |
| **Steps** | 1. As A, send the DELETE above with the header. 2. Record status and `code`. 3. `SELECT is_on_legal_hold, legal_hold_placed_by, legal_hold_reason, state, xmin FROM qams.archive_entry WHERE id='01990007-…b6'` under bypass. 4. Also send `POST /api/archives/01990007-…b6/dispose`. 5. Confirm the archive still appears in B's own `GET /api/archives` when queried with a B session. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Steps 1 and 4 both `404 Not Found`, `application/problem+json`, `{"title":"Archive entry not found.","status":404,"code":"ARC-404",…}` from `ArchiveLoader.LoadAsync` (`src/NT.QAMS.Application/Records/…:79-81`). |
| **Expected DB** | `is_on_legal_hold` still `true`; `legal_hold_placed_by`, `legal_hold_reason`, `state` and `xmin` unchanged. Row count of `qams.archive_entry` unchanged in both tenants. |
| **Expected Audit** | No `audit.field_change` row for `entity_id='01990007-0000-7000-8000-0000000000b6'`; no `audit.audit_trail` entry; B's hash chain verifies unchanged — run `GET /api/compliance/chain-verification` as a **B** user and assert `ok=true` with `verifiedEntries` equal to the pre-test value. |
| **Expected Notification** | n/a — no notification policy handles legal-hold release. |
| **Cleanup** | None. |
| **Evidence** | Two HTTP captures · `psql` legal-hold columns + `xmin` · B's chain-verification result before and after |
| **Result / Defect** | Not Run · — |
| **Notes** | Chosen because releasing another laboratory's legal hold is the highest-consequence delete in the product: it would permit disposal of records under regulatory hold. The chain-verification assertion is what turns "no row changed" into "B's Part-11 evidence is provably intact". |

#### TC-TENANT-SEC-010 — Cross-tenant list/search: Tenant A searches for Tenant B's exact NC reference  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, NCR · URS-008 · RSK-TENANT-001, RSK-TENANT-003 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — search term partitions: exact foreign reference / foreign substring / own reference / term matching nothing |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A · n/a — `GET /api/nonconformances` has no `[RequirePermission]` (`NonconformancesController.cs:23-28`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | B owns exactly 3 nonconformances, one of which is `nc_ref='NC-2026-0007'` with `title='Beta pipette calibration drift'`. A owns exactly 2 nonconformances, neither matching those strings. Verified under bypass: `SELECT tenant_id, count(*) FROM qams.nonconformance GROUP BY tenant_id` → A=2, B=3. |
| **Test Data** | Four requests: `?search=NC-2026-0007`, `?search=Beta%20pipette`, `?search=<A's own nc_ref>`, `?search=zzz-no-such-term`; each also with `&page=1&pageSize=200` |
| **Steps** | 1. As A, `GET /api/nonconformances?search=NC-2026-0007&page=1&pageSize=200`. 2. `GET /api/nonconformances?search=Beta%20pipette&page=1&pageSize=200`. 3. `GET /api/nonconformances?search=<A-own-ref>&page=1&pageSize=200`. 4. `GET /api/nonconformances?search=zzz-no-such-term`. 5. `GET /api/nonconformances` unfiltered. 6. Compare the `total` values of steps 1, 2 and 4. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Steps 1, 2 and 4 all `200 OK` with the pagination envelope `{"items":[],"total":0,"page":1,"pageSize":200}` and `hasMore` computed false (`PagedResponse<T>`, `src/NT.QAMS.Contracts/Common/…:8-11`) — indistinguishable from a term that matches nothing anywhere. Step 3 `200` with `total=1`. Step 5 `200` with `total=2` (A's own count only), never 5. The filter `n.Title.Contains(term) \|\| n.NcRef.Contains(term)` (`NcQueries.cs:29`) is applied on top of the tenant-filtered `db.Nonconformances`. |
| **Expected DB** | No writes. Optionally capture the generated SQL via the Npgsql OpenTelemetry instrumentation and assert the emitted `WHERE` contains `tenant_id = @__ef_filter…`; the RLS predicate is applied by PostgreSQL and will not appear in the statement text. |
| **Expected Audit** | No rows — list queries are not ledgered and write no `audit.security_event` (contrast the export cases, which do). |
| **Expected Notification** | n/a — read path. |
| **Cleanup** | None. |
| **Evidence** | Five HTTP captures · `psql` per-tenant counts · captured SQL statement text |
| **Result / Defect** | Not Run · — |
| **Notes** | The critical assertion is `total: 0`, not merely `items: []`. `total` is computed by `ToPagedAsync` over the same filtered query, so a leak would surface as a non-zero `total` with an empty page — the classic count-side disclosure. Assert both. |

#### TC-TENANT-SEC-011 — Cross-tenant list narrowed by a foreign-tenant foreign key  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, COMP · URS-008 · RSK-TENANT-001, RSK-TENANT-003 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — a caller-supplied **foreign key** (not a primary key) as the probe, exercising `GetTestAuthorizationsHandler`'s optional `userId` filter |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A · n/a — `GET /api/test-authorizations` has no `[RequirePermission]` (`TestAuthorizationsController.cs:17-20`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | B's user `01990008-…b7` holds at least 2 rows in `qams.test_authorization` with `tenant_id = 01990000-…bb`. A owns at least 1 test authorization for its own user. |
| **Test Data** | `?userId=01990008-0000-7000-8000-0000000000b7` (B's user), `?userId=01990001-0000-7000-8000-0000000000a1` (A's own user), `?userId=01990fff-0000-7000-8000-0000000000ff&status=Active` |
| **Steps** | 1. As A, `GET /api/test-authorizations?userId=01990008-0000-7000-8000-0000000000b7`. 2. `GET /api/test-authorizations?userId=01990001-0000-7000-8000-0000000000a1`. 3. `GET /api/test-authorizations?userId=01990fff-0000-7000-8000-0000000000ff&status=Active`. 4. Compare the bodies of steps 1 and 3. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Steps 1 and 3 both `200 OK` with body `[]` — byte-identical. Step 2 `200` with a non-empty array of A's rows only. The handler applies `a.UserId == userId` (`AuthorizationSlice.cs:145`) on top of the tenant-filtered `db.TestAuthorizations`, and the inner `Join` to `db.TestCatalogItems` (`:155-157`) is itself tenant-filtered, so a foreign catalog item cannot be surfaced through the join either. |
| **Expected DB** | No writes. |
| **Expected Audit** | None. |
| **Expected Notification** | n/a — read path. |
| **Cleanup** | None. |
| **Evidence** | Three HTTP captures · byte-diff of steps 1 and 3 · `psql` per-tenant row counts for `qams.test_authorization` |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the join-leak case: a filtered outer query joined to a second table can leak if the join target is unfiltered. Here both sides are `ITenantScoped`, so the composed filter applies twice. Any future report that joins a tenant-scoped table to an unfiltered lookup must add a sibling case. |

#### TC-TENANT-SEC-012 — Cross-tenant search across the whole pagination envelope, at the page-size boundary  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, DOC · URS-008 · RSK-TENANT-001 |
| **Level / Type / Technique** | API · Security (negative) · Boundary Value Analysis on the pagination envelope — `pageSize` at 1, 50 (default), 200 (`PageRequest.MaxPageSize`), 201 (clamped) and `page` beyond the last page |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A · n/a — `GET /api/documents` has no `[RequirePermission]` (`DocumentsController.cs:24-29`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | B owns 7 controlled documents including `01990fff`-unrelated id `0199000d-…bc` whose title contains `BETA-SOP-QC-001`. A owns exactly 4 controlled documents, none matching `BETA-SOP`. `PageRequest.DefaultPageSize = 50`, `MaxPageSize = 200` (`src/NT.QAMS.Application/Abstractions/Paging.cs:13-14`), clamp at `:20`. |
| **Test Data** | `?search=BETA-SOP-QC-001` with `pageSize` ∈ {1, 50, 200, 201} and `page` ∈ {1, 2, 99} |
| **Steps** | 1. As A, `GET /api/documents?search=BETA-SOP-QC-001&page=1&pageSize=1`. 2. Same with `pageSize=50`. 3. Same with `pageSize=200`. 4. Same with `pageSize=201`. 5. `GET /api/documents?search=BETA-SOP-QC-001&page=99&pageSize=200`. 6. `GET /api/documents?page=1&pageSize=200` unfiltered. 7. Record `total`, `pageSize` and `hasMore` for each. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Steps 1–5 all `200 OK`, `items: []`, `total: 0`, `hasMore: false`. Step 4 echoes `pageSize: 200`, not 201 — `Math.Clamp(pageSize, 1, MaxPageSize)` at `Paging.cs:20`. Step 6 `200` with `total: 4` (A's documents only), never 11. `hasMore` is `(long)Page * PageSize < Total` (`PagedResponse` `:10`), so with `total=0` it is false at every page. |
| **Expected DB** | No writes. |
| **Expected Audit** | None. |
| **Expected Notification** | n/a — read path. |
| **Cleanup** | None. |
| **Evidence** | Six HTTP captures · the `total`/`pageSize`/`hasMore` table · `psql` per-tenant document counts |
| **Result / Defect** | Not Run · — |
| **Notes** | Paging is the classic place a filter is applied to the page but not to the count. Asserting `total: 0` at four page sizes and a beyond-the-end page closes that hole. The `pageSize=201 → 200` clamp is asserted because a future removal of the clamp would let an attacker pull a whole tenant in one request if the filter ever regressed. |

#### TC-TENANT-SEC-013 — Cross-tenant export: the XLSX register walks every page and still contains only Tenant A  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, RPT · URS-008 · RSK-TENANT-004 |
| **Level / Type / Technique** | API · Security (negative) · Use Case — the Part 11 §11.10(b) "complete copy" flow, whose completeness loop is the exact place a tenant filter could be lost |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A · n/a — `GET /api/exports/nonconformances.xlsx` carries no `[RequirePermission]` (`ExportsController.cs:30-31`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Enough rows to force the loop to iterate: A owns 250 nonconformances, B owns 300. Verified under bypass: `SELECT tenant_id, count(*) FROM qams.nonconformance GROUP BY tenant_id` → A=250, B=300. The loop pages at `PageRequest.MaxPageSize = 200` (`ExportsController.cs:40`), so A's export requires 2 iterations. |
| **Test Data** | `GET /api/exports/nonconformances.xlsx` as A |
| **Steps** | 1. As A, `curl.exe -i -H "Authorization: Bearer <A-token>" -o nc-register.xlsx http://localhost:5080/api/exports/nonconformances.xlsx`. 2. Assert `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` and a `Content-Disposition` filename of the form `nc-register-<yyyyMMdd-HHmm>.xlsx`. 3. Open the workbook and count the data rows on the `Nonconformances` sheet. 4. Grep the extracted `xl/sharedStrings.xml` for `NC-2026-0007`, `Beta`, `0000000000bb`. 5. Read the provenance header cell and assert the tenant name is **Alpha** (`PackAsync`, `ExportsController.cs:160-166`). 6. `SELECT tenant_id, event_type, detail, actor FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 1` under bypass. |
| **Expected UI** | The SPA download completes; the browser saves the file. No cross-tenant indicator appears because none exists. |
| **Expected API** | `200 OK`, XLSX media type, `Content-Disposition` attachment with the timestamped name from `ExportsController.cs:57`. Row count on the `Nonconformances` sheet is **exactly 250** — A's total — never 550. Columns are the seven headers at `ExportsController.cs:47`. |
| **Expected DB** | One new row in `audit.security_event`: `event_type='RECORD_EXPORTED'`, `tenant_id='01990000-0000-7000-8000-0000000000aa'`, `detail='nonconformances.xlsx'`, `actor` = A's display name — written by `LogExportAsync` (`ExportsController.cs:168-169`) with `tenant.TenantId`, not a caller value. No writes to `qams.nonconformance`. |
| **Expected Audit** | Step 6 returns exactly that row, with A's `tenant_id`. Assert `tenant_id IS NOT NULL` — a null-tenant export event would be a platform-scoped record and readable across the estate under `bypass_rls`. |
| **Expected Notification** | n/a — no notification policy handles `RECORD_EXPORTED`. |
| **Cleanup** | Delete the downloaded workbook. The `audit.security_event` row is append-only and is retained by design — do not attempt to remove it. |
| **Evidence** | HTTP headers capture · the workbook itself · sheet row count · `sharedStrings.xml` grep result · the `audit.security_event` row |
| **Result / Defect** | Not Run · — |
| **Notes** | The completeness loop at `ExportsController.cs:38-43` calls `sender.Send(GetNcsQuery…)` per page, so each page re-enters the filtered handler; a regression that switched the loop to a direct `db.Nonconformances` read with `IgnoreQueryFilters()` would leak all 550 rows and would still return 200. Row count is therefore the load-bearing assertion, not the absence of a string. |

#### TC-TENANT-SEC-014 — Cross-tenant export of the compliance audit trail, including the legacy null-tenant rows  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, LEDGER · URS-008, URS-100 · RSK-TENANT-004 |
| **Level / Type / Technique** | API · Security (negative) · Data Flow — follow `tenant_id` from the JWT claim → `ICurrentTenant` → GUC → the `audit.*` `USING` predicate → the three worksheets of the export |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A holding `compliance.export` · `compliance.export` (`ExportsController.cs:61`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Both tenants have ledger history. At least one **null-tenant** `audit.field_change` row exists (produced by a failed pre-auth login) — of the 19 296 legacy null-tenant rows retained by design. The four `audit.*` tables carry predicate **shape R**: `USING` is strict, `WITH CHECK` additionally admits `tenant_id IS NULL` (`RelaxAuditRlsWriteCheck.cs:36-40`), so null-tenant rows are writable by anyone and readable only under `bypass_rls='on'`. |
| **Test Data** | `GET /api/exports/audit-trail.xlsx?take=1000` as A |
| **Steps** | 1. Under bypass, record `SELECT tenant_id, count(*) FROM audit.audit_trail GROUP BY tenant_id` and the same for `audit.field_change` including the `tenant_id IS NULL` bucket. 2. As A, download `GET /api/exports/audit-trail.xlsx?take=1000`. 3. Count rows on each of the three sheets: `Integrity Attestation`, `Event Trail`, `Field-Level Changes`. 4. Read the `Chain integrity` and `Entries verified` cells. 5. Grep `xl/sharedStrings.xml` for B's `tenant_id`, B's `nc_ref`, `analyst@beta-lab.local`. 6. Confirm no row on `Field-Level Changes` originates from a null-tenant `audit.field_change` row (cross-check the exported `Occurred (UTC)` values against the null-tenant bucket from step 1). |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | `200 OK`, XLSX media type, filename `audit-trail-<yyyyMMdd-HHmm>.xlsx`. `Event Trail` row count equals A's `audit.audit_trail` count (capped at `take=1000`), never the estate total. `Integrity Attestation` shows `Entries verified` equal to A's chain length, computed by `VerifyChainQuery(tenant.TenantId)` — the tenant id comes from `ICurrentTenant`, not from the query string (`ExportsController.cs:69-71`). |
| **Expected DB** | One new `audit.security_event` row: `event_type='RECORD_EXPORTED'`, `detail='audit-trail.xlsx'`, `tenant_id = 01990000-…aa`. |
| **Expected Audit** | Step 5 yields zero matches. Step 6 yields zero null-tenant rows in the export — because `USING` was left strict by `RelaxAuditRlsWriteCheck.cs` (comment at `:18`), a null-tenant field-change row is invisible to a tenant session even though any tenant may write one. |
| **Expected Notification** | n/a. |
| **Cleanup** | Delete the downloaded workbook; the security event is append-only. |
| **Evidence** | `psql` per-tenant and null-tenant ledger counts · the workbook · three sheet row counts · attestation cells · `sharedStrings.xml` grep |
| **Result / Defect** | Not Run · — |
| **Notes** | The null-tenant assertion is the subtle half. Shape R's asymmetry (write-open, read-closed) is deliberate; a future "fix" that relaxed `USING` to match `WITH CHECK` would silently publish every tenant's pre-auth login failures into every tenant's compliance export, and this is the case that would catch it. |

#### TC-TENANT-SEC-015 — Cross-tenant export of a PDF review pack keyed on Tenant B's management review  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, RPT · URS-008 · RSK-TENANT-004 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — a foreign-tenant id supplied as a **route parameter to an export**, the one export that takes a caller-chosen record id |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A holding `reviews.export` · `reviews.export` (`ExportsController.cs:126`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | B's management review `01990000`-family id `0199000b-…ba` exists with decisions attached. A's role grants `reviews.export` (otherwise the case degenerates into TC-TENANT-SEC-005). |
| **Test Data** | `GET /api/exports/review-pack/0199000b-0000-7000-8000-0000000000ba.pdf` |
| **Steps** | 1. As A, send the GET above. 2. Record status, `Content-Type`, `code` and body. 3. Assert no PDF bytes were returned (`Content-Length` corresponds to the problem body, and the first bytes are not `%PDF`). 4. `SELECT count(*) FROM audit.security_event WHERE event_type='RECORD_EXPORTED' AND detail LIKE 'review-pack/%'` before and after. 5. Repeat with A's own review id to prove the route works and returns a PDF. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Step 1 `404 Not Found`, `application/problem+json`, `{"title":"Management review not found.","status":404,"code":"MRV-404",…}` from `src/NT.QAMS.Application/RiskGovernance/RiskGovernanceSlice.cs:419`, thrown by `GetReviewByIdHandler` **before** the KPI and Pareto queries run (`ExportsController.cs:129-131`). Step 5 `200 application/pdf` with `Content-Disposition` filename `review-pack-<A-review-ref>-<yyyyMMdd>.pdf`. |
| **Expected DB** | No `RECORD_EXPORTED` row for the failed attempt — `LogExportAsync` at `ExportsController.cs:155` is reached only after the pack is built, so step 4's count is unchanged by step 1 and increases by exactly 1 after step 5. |
| **Expected Audit** | As above. Note and record the consequence: **a failed cross-tenant export attempt leaves no security-event trace.** That is an observation about detection coverage, not a functional failure of isolation; it is raised as part of **GAP-TENANT-904**'s discussion, not as a defect of this case. |
| **Expected Notification** | n/a. |
| **Cleanup** | Delete the PDF produced by step 5. |
| **Evidence** | Two HTTP captures · first-bytes check · `audit.security_event` counts before/after each step |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the only export that takes a record id, and it composes three queries (`GetReviewByIdQuery`, `GetDashboardKpisQuery`, `GetNcParetoQuery`). Because the review load fails first, the two aggregate queries never run — assert that too, by confirming the response arrives without the latency of a full KPI computation, or by span inspection if OpenTelemetry capture is available. |

#### TC-TENANT-SEC-016 — Cross-tenant file download returns a bare framework 404 with no stable `code`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, DOC · URS-008 · RSK-TENANT-001, RSK-TENANT-003 |
| **Level / Type / Technique** | API · Security (negative) · Error Guessing — the one in-scope handler that returns `NotFound()` from the controller rather than throwing a domain exception, so the error contract differs from every other case in this batch |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A · n/a — `GET /api/files/{id}` carries only class-level `[Authorize]` (`FilesController.cs:16,59`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`, `FileStorage:RootPath` as configured for Development |
| **Preconditions** | B's `qams.file_reference` row `01990009-…b8` exists with `storage_key = '01990000000070008000000000000bb/e3b0…b855'`, and the physical object exists under `{root}/01990000000070008000000000000bb/e3b0…b855`. `qams.file_reference` is RLS-protected: measured `relrowsecurity=t, relforcerowsecurity=t`. |
| **Test Data** | `GET /api/files/01990009-0000-7000-8000-0000000000b8`; control `GET /api/files/01990fff-0000-7000-8000-0000000000ff` |
| **Steps** | 1. As A, `curl.exe -i -H "Authorization: Bearer <A-token>" http://localhost:5080/api/files/01990009-0000-7000-8000-0000000000b8`. 2. Record status, `Content-Type` and the complete body. 3. Repeat with the never-existing id. 4. Diff the two bodies. 5. Assert the body has **no** `code` extension and no `fileName`, `sha256`, `storageKey`, `contentType` or `sizeBytes` field. 6. Confirm the physical object under `{root}/01990000000070008000000000000bb/` was **not opened** — its file-system last-access time is unchanged, or no read appears in the storage log. |
| **Expected UI** | The SPA shows its generic download-failed state; no file name from B is rendered. |
| **Expected API** | Steps 1 and 3 both `404 Not Found`, `Content-Type: application/problem+json`. Body is the **framework** client-error mapping of `NotFound()` (`FilesController.cs:65`), enriched by `CustomizeProblemDetails` (`Program.cs:174-182`) with `traceId` and `correlationId` — so it carries `title`, `status`, `type`, `traceId`, `correlationId` and **no `code`**. Capture the exact `title` and `type` strings at execution; they are ASP.NET Core defaults and must be recorded, not assumed. The two bodies are identical apart from the two id extensions. |
| **Expected DB** | No writes. `db.Files.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id)` (`FilesController.cs:62`) returned null under the tenant filter plus RLS, so `storage.OpenReadAsync` at `:68` was never reached. |
| **Expected Audit** | No `audit.security_event` row — file downloads are not logged as exports. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Two HTTP captures with full headers · byte-diff · file-system access-time check on B's stored object |
| **Result / Defect** | Not Run · — |
| **Notes** | Isolation holds, but the **error contract does not**: this 404 has no machine-readable `code`, breaking the API-003 promise that "every failure path emits the same shape with a stable code" (`ProblemResponse.cs:6-13`). Recorded as **GAP-TENANT-904**. Assert the current shape here — do not write the desired shape as expected. |

#### TC-TENANT-SEC-017 — The storage layer offers no key-addressed route, and content-addressing does not cross tenants  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, DOC · URS-008 · RSK-TENANT-001 |
| **Level / Type / Technique** | API + Component · Security (negative) · Path coverage of `LocalFileStorage.SaveAsync`/`OpenReadAsync` — the dedupe branch (`File.Exists(finalPath)`) is per-tenant-directory, so the two tenants take disjoint paths for identical content |
| **Priority / Severity / Automation** | High · Critical · Partial — the HTTP probes are automatable; the file-system inspection is manual |
| **Role / Permission / Tenant** | QualityManager of A · n/a — no permission gate on the files surface · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` + local file storage under `FileStorage:RootPath` |
| **Preconditions** | B has uploaded a file whose content hashes to `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`, stored at `{root}/01990000000070008000000000000bb/e3b0…b855`. The approved API surface `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` contains **no** route that accepts a storage key. |
| **Test Data** | Byte-identical content to B's file, prepared locally as `identical.txt`; probe URLs `/api/files/01990000000070008000000000000bb/e3b0…b855` and `/api/files/e3b0…b855` |
| **Steps** | 1. As A, `GET /api/files/01990000000070008000000000000bb/e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`. 2. `GET /api/files/e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`. 3. `POST /api/files` uploading `identical.txt` as A; capture the returned `id` and `sha256`. 4. `SELECT id, tenant_id, storage_key FROM qams.file_reference WHERE sha256='e3b0…b855'` under bypass. 5. Inspect the storage root and list the per-tenant directories. 6. `GET /api/files/<A's new id>` as A, then as a **B** session. |
| **Expected UI** | n/a — API-level case plus a file-system inspection. |
| **Expected API** | Steps 1 and 2 `404 Not Found` — no route template matches: the only file route is `[HttpGet("{id:guid}")]` (`FilesController.cs:59`), and neither probe is a GUID. Step 3 `201 Created` with `Location` pointing at `/api/files/<new-guid>` and `sha256 = e3b0…b855`. Step 6: `200` for A, `404` for B. |
| **Expected DB** | Step 4 returns **two** rows with the same `sha256` — one per tenant, with distinct `id` values, distinct `tenant_id` values, and `storage_key` values `01990000000070008000000000000aa/e3b0…b855` and `01990000000070008000000000000bb/e3b0…b855`. No row is shared. |
| **Expected Audit** | No `audit.security_event` for uploads or downloads on this path. |
| **Expected Notification** | n/a. |
| **Cleanup** | `DELETE` is not offered for files; leave A's uploaded row in place and record its id in the execution record so the fixture stays reproducible. |
| **Evidence** | Two 404 captures · the 201 upload response · the two-row `psql` result · a directory listing of the storage root showing two tenant directories |
| **Result / Defect** | Not Run · — |
| **Notes** | Content-addressed storage is a classic cross-tenant leak: a single global `{sha}` namespace would let any tenant fetch any other's object by hash. `LocalFileStorage.cs:28,47,57` scopes the directory by `tenantId.ToString("N")` **before** hashing into it, so identical content is stored twice by design. That duplication is a deliberate isolation cost and must not be "optimised away"; this case is the guard on that decision. |

#### TC-TENANT-SEC-018 — Cross-tenant notification retrieval: Tenant A's feed contains only its own dispatches  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, NOTIF · URS-008 · RSK-TENANT-001 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning over dispatch ownership — own-tenant/own-recipient, own-tenant/other-recipient, other-tenant |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A · n/a — `GET /api/notifications/mine` has no `[RequirePermission]` (`PlatformControllers.cs:97-101`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Three dispatch rows exist: (i) `recipient_user_id = 01990001-…a1`, tenant A — the attacker's own; (ii) another tenant-A recipient; (iii) `0199000a-…b9` with `recipient_user_id = 01990008-…b7`, tenant B. `qams.notification_dispatch` is RLS-protected: measured `relrowsecurity=t, relforcerowsecurity=t`. |
| **Test Data** | `GET /api/notifications/mine?unreadOnly=false&page=1&pageSize=200`, then `?unreadOnly=true` |
| **Steps** | 1. As A, `GET /api/notifications/mine?unreadOnly=false&page=1&pageSize=200`. 2. Record `total` and the `id` list. 3. `GET /api/notifications/mine?unreadOnly=true`. 4. Assert `0199000a-0000-7000-8000-0000000000b9` appears in neither result. 5. Under bypass, `SELECT count(*) FROM qams.notification_dispatch` and compare with the sum of A's and B's per-tenant counts to confirm the fixture. |
| **Expected UI** | The notification bell shows A's unread count only. |
| **Expected API** | `200 OK` with the pagination envelope. `items` contains **only** dispatches whose `recipient_user_id = 01990001-0000-7000-8000-0000000000a1` — a **double** bound: the handler's explicit `d.RecipientUserId == userId` (`src/NT.QAMS.Application/Notifications/NotificationSlice.cs:77`) where `userId` comes from `ICurrentUser.UserId` (`:74`, else `AUTH-003`), **and** the tenant filter plus RLS on `qams.notification_dispatch`. Dispatch (ii) — same tenant, different recipient — is also absent, proving the recipient bound is real and not merely the tenant filter. |
| **Expected DB** | No writes. |
| **Expected Audit** | None. |
| **Expected Notification** | This case *is* the notification assertion: no B dispatch is delivered into A's feed. |
| **Cleanup** | None. |
| **Evidence** | Two HTTP captures · id lists · `psql` per-tenant dispatch counts |
| **Result / Defect** | Not Run · — |
| **Notes** | Including dispatch (ii) in the fixture is what makes this a real test: with only own-recipient and foreign-tenant rows, a handler that dropped the recipient predicate would still pass. |

#### TC-TENANT-SEC-019 — Cross-tenant mutation of a notification: marking Tenant B's dispatch as read  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, NOTIF · URS-008 · RSK-TENANT-002 |
| **Level / Type / Technique** | API · Security (negative) · Branch coverage — the false branch of the composite predicate `d.Id == c.DispatchId && d.RecipientUserId == userId` |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A · n/a — `POST /api/notifications/{id}/read` has no `[RequirePermission]` (`PlatformControllers.cs:103-108`); the command carries `[RequireInternalActor]` (`NotificationSlice.cs:94`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | B's dispatch `0199000a-…b9` has `read_by_recipient = false`. A tenant-A dispatch addressed to a **different** A user also has `read_by_recipient = false`. |
| **Test Data** | `POST /api/notifications/0199000a-0000-7000-8000-0000000000b9/read`; second probe against the other A user's dispatch id |
| **Steps** | 1. As A, `POST /api/notifications/0199000a-0000-7000-8000-0000000000b9/read`. 2. Record status and `code`. 3. `SELECT read_by_recipient, xmin FROM qams.notification_dispatch WHERE id='0199000a-…b9'` under bypass. 4. Repeat step 1 against the other tenant-A user's dispatch id. 5. Compare the two responses. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Steps 1 and 4 both `404 Not Found`, `application/problem+json`, `{"title":"Notification not found.","status":404,"code":"NTF-404",…}` from `src/NT.QAMS.Application/Notifications/NotificationSlice.cs:105`. Identical bodies apart from the id extensions, so the attacker cannot separate "another tenant's" from "another user's". |
| **Expected DB** | B's dispatch: `read_by_recipient` still `false`, `xmin` unchanged. The other A user's dispatch likewise unchanged. |
| **Expected Audit** | No `audit.field_change` row for `entity_id='0199000a-0000-7000-8000-0000000000b9'`. |
| **Expected Notification** | The dispatch remains in B's unread feed — verify with a B session: `GET /api/notifications/mine?unreadOnly=true` still returns it. |
| **Cleanup** | None. |
| **Evidence** | Two HTTP captures · `psql` `read_by_recipient` + `xmin` · B's unread feed after the attack |
| **Result / Defect** | Not Run · — |
| **Notes** | `[RequireInternalActor]` on the command (`NotificationSlice.cs:94`) is a tier gate, not a tenant gate — it would not have stopped this. The stop is the recipient predicate plus RLS. Do not credit the attribute in the execution record. |

#### TC-TENANT-SEC-020 — Cross-tenant delivery monitor: the administrative notification view is tenant-fenced  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, NOTIF · URS-008 · RSK-TENANT-001, RSK-TENANT-004 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — the *administrative* view, which unlike `/mine` has no recipient predicate and is fenced by the tenant layers alone |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin of A holding `notifications.manage` · `notifications.manage` (`PlatformControllers.cs:123`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | A owns 12 dispatch rows across users; B owns 9, including at least one with `email_status='Failed'` and a populated `error` column containing B's recipient address `analyst@beta-lab.local`. |
| **Test Data** | `GET /api/notifications/monitor?page=1&pageSize=200`, then `?status=Failed` |
| **Steps** | 1. As A's tenant admin, `GET /api/notifications/monitor?page=1&pageSize=200`. 2. Record `total` and every `recipientEmail` in the payload. 3. `GET /api/notifications/monitor?status=Failed&page=1&pageSize=200`. 4. Grep both payloads for `beta-lab`. 5. Under bypass, `SELECT tenant_id, count(*) FROM qams.notification_dispatch GROUP BY tenant_id` to confirm the fixture. |
| **Expected UI** | The notification-monitor screen shows 12 rows for A. |
| **Expected API** | Step 1 `200 OK`, `total: 12`, never 21. Step 3 `200 OK` containing only A's failed dispatches; B's failed row and its `error` text are absent. Step 4 returns zero matches for `beta-lab`. `GetDispatchMonitorHandler` (`NotificationSlice.cs:116-…`) has **no** recipient predicate, so the only fence is the tenant query filter plus the `tenant_isolation` policy on `qams.notification_dispatch`. |
| **Expected DB** | No writes. |
| **Expected Audit** | None. |
| **Expected Notification** | n/a — read path. |
| **Cleanup** | None. |
| **Evidence** | Two HTTP captures · `total` values · grep result for `beta-lab` · `psql` per-tenant counts |
| **Result / Defect** | Not Run · — |
| **Notes** | This view leaks recipient **email addresses** and SMTP **error strings** if the fence fails, which is a personal-data exposure on top of the tenancy breach. It is the strongest reason the monitor is permission-gated as well as tenant-fenced; the permission gate is exercised separately by TC-TENANT-SEC-005's rule pattern. |

#### TC-TENANT-SEC-021 — Cross-tenant dashboard aggregation: every KPI counts Tenant A's rows only  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, RPT · URS-008 · RSK-TENANT-004 |
| **Level / Type / Technique** | API · Security (negative) · Use Case — the dashboard load, with a fixture designed so that any missing filter produces an arithmetically detectable number |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A holding `reports.view` · `reports.view` (`ReportsController.cs:25`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture with distinct, non-overlapping counts so a leak is unambiguous. Tenant A: 7 open NCs, 3 overdue CAPA actions, 2 open complaints, 1 audit in progress, 4 equipment out of service, 5 high-residual risks, 6 overdue tasks, 2 unsatisfactory PT enrolments. Tenant B: 11, 8, 9, 3, 10, 12, 13, 4 respectively. Every A/B pair differs, and no A value equals any A+B sum. |
| **Test Data** | `GET /api/reports/kpis` |
| **Steps** | 1. As A, `GET /api/reports/kpis`. 2. Record all twelve KPI values and the `totals` object. 3. Compare each against the A column of the fixture. 4. Confirm no value equals the corresponding A+B sum (7+11=18, 3+8=11, 2+9=11, 1+3=4, 4+10=14, 5+12=17, 6+13=19, 2+4=6). 5. `GET /api/reports/nc-pareto` and assert the bucket counts sum to A's `totals.nonconformances`, not to A+B. |
| **Expected UI** | The dashboard tiles render A's numbers. |
| **Expected API** | `200 OK`. `openNcs=7`, `overdueCapaActions=3`, `openComplaints=2`, `auditsInProgress=1`, `equipmentOutOfService=4`, `highResidualRisks=5`, `overdueTasks=6`, `ptUnsatisfactory=2`, plus `totals` matching A's row counts (`GetDashboardKpisHandler`, `src/NT.QAMS.Application/Reporting/ReportingQueries.cs:33-75`). Every count is issued against `db.<Set>` with no `IgnoreQueryFilters()`, so both isolation layers apply. Step 5: the Pareto buckets (`GetNcParetoHandler`, `:112-…`) sum to A's NC total. |
| **Expected DB** | No writes. |
| **Expected Audit** | None — dashboard reads are not ledgered. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | HTTP capture · the fixture-vs-observed table for all twelve KPIs · Pareto sum check |
| **Result / Defect** | Not Run · — |
| **Notes** | Aggregates are the quietest leak in a multi-tenant system: a count discloses another tenant's volume without disclosing a single row, and no error is raised. The fixture is built so that *every* KPI is independently falsifiable — do not simplify it to equal counts, which would make a leak invisible. |

#### TC-TENANT-SEC-022 — Cross-tenant KPI history: the snapshot table is written under elevation but read under the tenant fence  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, RPT · URS-008, URS-100 · RSK-TENANT-004, RSK-TENANT-005 |
| **Level / Type / Technique** | API + Integration · Security (negative) · Data Flow — `read.kpi_snapshot` rows are written by an elevated background job for all tenants, then read by a tenant-scoped request; the case follows one datum across that boundary |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional + integration) |
| **Role / Permission / Tenant** | QualityManager of A holding `reports.view` · `reports.view` (`ReportsController.cs:30`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `KpiSnapshotService` has run at least once and written rows for **both** tenants: verified under bypass, `SELECT tenant_id, count(*) FROM read.kpi_snapshot GROUP BY tenant_id` returns two rows. `read.kpi_snapshot` is RLS-protected (measured `relrowsecurity=t, relforcerowsecurity=t`; predicate shape S). The job filters to `Status == Active` tenants (`KpiSnapshotService.cs:90`) and runs under `Elevate()` (`:63`) with `.IgnoreQueryFilters()` on its reads (`:97,105`). |
| **Test Data** | `GET /api/reports/kpi-history?days=90`, then `?days=366`, then `?days=400` |
| **Steps** | 1. Under bypass, record A's and B's snapshot row counts and the distinct `date` values per tenant. 2. As A, `GET /api/reports/kpi-history?days=90`. 3. Count the returned points and compare with A's snapshot rows within the 90-day window. 4. `GET /api/reports/kpi-history?days=366` and `?days=400`; assert both return the same window (clamped by `Math.Clamp(query.Days, 1, 366)`, `ReportingQueries.cs:98`). 5. Assert no returned point's KPI values match B's snapshot values for a date on which A has no row. |
| **Expected UI** | The KPI trend chart plots A's series only. |
| **Expected API** | `200 OK` with an array of `KpiHistoryPointDto`. Point count equals **A's** snapshot rows within the window, never A+B. The handler reads `db.KpiSnapshots` with **no** `IgnoreQueryFilters()` (`ReportingQueries.cs:100-108`), so the request-scoped tenant filter applies and the `tenant_isolation` policy fences the rest. `?days=400` behaves identically to `?days=366`. |
| **Expected DB** | No writes. |
| **Expected Audit** | None. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | `psql` per-tenant snapshot inventory · three HTTP captures · point-count reconciliation |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the read half of the elevation story: the writer legitimately crosses tenants, the reader must not. Per front matter §3.4, elevation without `IgnoreQueryFilters()` returns nothing and elevation with it returns everything; the request path has neither, which is the correct third state. A regression that added `.IgnoreQueryFilters()` here would publish every tenant's KPI history and would still return 200 — hence the point-count assertion rather than a string grep. |

#### TC-TENANT-SEC-023 — Cross-tenant report narrowed by another tenant's branch id  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, RPT, ORG · URS-008 · RSK-TENANT-001, RSK-TENANT-003 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — conditions (branch id supplied? / branch belongs to caller's tenant? / section is branch-attributed?) against the three documented handler rules at `QualityAnalyticsQuery.cs:29-41` |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A holding `reports.view` plus the per-section view keys · `reports.view` (`ReportsController.cs:50`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | B owns branch `0199000c-…bb` with 11 nonconformances attributed to it. A owns at least one branch with attributed records, so the endpoint is demonstrably functional. A's role grants `reports.view` plus `nonconformances.view`, `complaints.view`, `audits.view`, `equipment.view`, `suppliers.view`, `risks.view`, `documents.view`, `competencies.view`, `proficiency-testing.view`, so no section is hidden for permission reasons and the tenant boundary is the only variable. |
| **Test Data** | `?branchId=0199000c-0000-7000-8000-0000000000bb`; controls `?branchId=<A's own branch>` and no filter |
| **Steps** | 1. As A, `GET /api/reports/quality-analytics?branchId=0199000c-0000-7000-8000-0000000000bb`. 2. Record `scope` and every section's counts. 3. `GET /api/reports/quality-analytics?branchId=<A-own-branch>`. 4. `GET /api/reports/quality-analytics` unfiltered. 5. Compare the `ncCapa` section across the three. |
| **Expected UI** | The analytics page renders with zeroed branch-attributed sections and the "some sections are not organisationally attributed" note. |
| **Expected API** | Step 1 `200 OK`. `scope.branchId` echoes `0199000c-0000-7000-8000-0000000000bb`, `scope.filterApplied = true`, `scope.unscopedSections = ["documentControl","competency","proficiencyTesting"]` (`QualityAnalyticsQuery.cs:47-48,102`), `scope.hiddenSections = []`. `ncCapa.total = 0` and `ncCapa.open = 0` — the filter `n.BranchId == b` (`:144`) is applied on top of the tenant-filtered `db.Nonconformances`, so B's 11 records are unreachable. The branch-unattributed sections (`documentControl`, `competency`, `proficiencyTesting`) return **A's own** unnarrowed figures, which is correct and is not a leak. Step 4's `ncCapa.total` equals A's NC count. |
| **Expected DB** | No writes. |
| **Expected Audit** | None. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Three HTTP captures · the `scope` object · the `ncCapa` comparison table |
| **Result / Defect** | Not Run · — |
| **Notes** | Two distinct properties are asserted. (a) No B data appears. (b) **The echo of the supplied branch id discloses nothing** — the response is identical for a foreign branch id and for a random GUID, so the endpoint is not a branch-existence oracle. Verify (b) explicitly by repeating step 1 with `01990fff-0000-7000-8000-0000000000ff` and byte-diffing the two bodies apart from `scope.branchId` and the freshness stamp. |

#### TC-TENANT-SEC-024 — Id enumeration: a foreign-tenant id and a never-existing id are indistinguishable  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-003 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — the whole case is the assertion that two nominally different partitions ("exists elsewhere", "exists nowhere") collapse into one observable class |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A holding every permission used below · various, all held · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`, executed **after** any credential-burst probes so the 10/min auth rate-limit partition is not poisoned |
| **Preconditions** | All fixture rows in place. Note the guid-route constraint: `[HttpGet("{id:guid}")]` means a non-GUID segment fails routing before any handler runs. |
| **Test Data** | Five endpoint pairs, each run with B's real id and with `01990fff-0000-7000-8000-0000000000ff`: `/api/nonconformances/{id}`, `/api/equipment/{id}`, `/api/archives/{id}/dispose` (POST), `/api/files/{id}`, `/api/notifications/{id}/read` (POST) |
| **Steps** | 1. For each of the five endpoints, issue the request with B's id and capture status, `code`, full body and `Content-Length`. 2. Repeat with `01990fff-…ff`. 3. Byte-diff each pair, ignoring `traceId` and `correlationId`. 4. Repeat each pair 20 times and record the wall-clock latency distribution. 5. Compare the median and 95th-percentile latencies of the two members of each pair. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Within each pair, the two responses are **identical**: same status, same `code` (`NC-404`, `EQP-404`, `ARC-404`, none for `/api/files`, `NTF-404`), same title, same `Content-Length`. Across pairs the codes differ by module — that is expected and discloses only which module was addressed, which the URL already discloses. |
| **Expected DB** | No writes on any of the ten requests. |
| **Expected Audit** | No `audit.security_event` rows — record this explicitly: **an enumeration sweep against another tenant leaves no detection trace anywhere in the system.** That is a monitoring gap, not an isolation failure; it is folded into **GAP-TENANT-904**. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Ten HTTP captures · five byte-diffs · the latency distribution table (200 samples) |
| **Result / Defect** | Not Run · — |
| **Notes** | Latency is measured, not asserted with a hard threshold: on the foreign-id path the query runs and returns no row; on the never-existing path the identical query runs and returns no row — the work is the same, so a systematic gap would indicate an unexpected extra lookup. Record the distributions and escalate only if the medians separate by more than the observed run-to-run variance; do not fail the case on a single noisy sample. |

#### TC-TENANT-SEC-025 — Id enumeration through a tenant-valued parameter: chain verification cannot be re-pointed at Tenant B  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, LEDGER · URS-008 · RSK-TENANT-001, RSK-TENANT-003 |
| **Level / Type / Technique** | API · Security (negative) · Error Guessing — target the one query in the codebase whose handler signature takes a `tenantId` argument (`VerifyChainQuery(Guid)`), and attempt to supply it from the request |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A holding `compliance.view` · `compliance.view` (class-level, `ComplianceController.cs:20`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Both tenants have non-empty `audit.audit_trail` chains of **different** lengths — A: 40 entries, B: 90 entries — so a successful re-point would be immediately visible in `verifiedEntries`. |
| **Test Data** | Five variants: no parameter; `?tenantId=01990000-0000-7000-8000-0000000000bb`; `?tenantid=…bb`; `?TenantId=…bb`; header `X-Tenant-Id: 01990000-0000-7000-8000-0000000000bb` |
| **Steps** | 1. As A, `GET /api/compliance/chain-verification`. 2. Record `ok`, `verifiedEntries`, `brokenAtSequence`. 3. Repeat with each of the four override attempts. 4. Assert all five responses are identical. 5. As a **B** user, `GET /api/compliance/chain-verification` and record `verifiedEntries` to confirm the fixture asymmetry. |
| **Expected UI** | The compliance screen shows A's chain status. |
| **Expected API** | All five requests `200 OK` with `verifiedEntries = 40` (A's chain), `ok = true`, `brokenAtSequence = null`. **Never 90.** The tenant comes from `User.FindFirstValue("tenant_id")` (`ComplianceController.cs:63`), parsed at `:64`, and is the only value passed to `VerifyChainQuery` at `:69`; no query-string or header binding exists on this action. Step 5 returns `verifiedEntries = 90`. |
| **Expected DB** | No writes. `VerifyChainAsync` (`ComplianceLedgerServices.cs:209-214`) reads `audit.audit_trail` filtered by the passed `tenantId`, which is A's. |
| **Expected Audit** | None — chain verification is a read. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Six HTTP captures · the identical-body diff across the five A variants · the B baseline |
| **Result / Defect** | Not Run · — |
| **Notes** | The interesting structural fact is that `VerifyChainAsync` genuinely accepts an arbitrary tenant id — the safety lives entirely in the single call site's choice of argument. There is **no** architecture test asserting that no other caller ever passes a request-derived value; if one is added, this case must be duplicated for it. Also assert the negative form: `BadRequest("Chain verification runs within a tenant context.")` at `:66` is reachable only for a principal with no parsable `tenant_id`, i.e. a platform administrator — capture that response separately in batch C. |

#### TC-TENANT-SEC-026 — Cross-tenant bulk operation: a 50-row import aimed at Tenant B's study writes nothing  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, MV · URS-008, URS-101 · RSK-TENANT-002 |
| **Level / Type / Technique** | API · Security (negative) · Loop coverage — the import loop at `MethodComparisonSlice.cs:117-129` must execute **zero** iterations, because the parent load fails before the loop is entered |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager of A · n/a — `POST /api/method-comparisons/{id}/pairs/import` has no `[RequirePermission]` (`MethodComparisonsController.cs:42-44`); the command carries `[RequireInternalActor]` (`MethodComparisonSlice.cs:96`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | B's study `01990005-…b4` is editable and currently has `pair_count = 8`. Under bypass, `SELECT count(*) FROM qams.measurement_pair WHERE tenant_id='01990000-…bb'` returns 8. |
| **Test Data** | `POST /api/method-comparisons/01990005-0000-7000-8000-0000000000b4/pairs/import` with `{"rows":[…50 rows…]}` where rows 1–48 are valid (`referenceValue` 1.00–48.00, `testValue` 1.05–48.05, `sampleId` `"SEC026-001"`…`"SEC026-048"`) and rows 49–50 are invalid (`referenceValue: 0`, which trips `MC-003` at `MethodComparisonStudy.cs:103-106`) |
| **Steps** | 1. Record `pair_count` on B's study and the row count of `qams.measurement_pair` per tenant, under bypass. 2. As A, send the import above against B's study id. 3. Record status, `code` and body. 4. Re-read the counts from step 1. 5. Send the **same 50-row payload** against A's own study id and record `imported` and `rejected`. 6. Confirm none of the 48 rows written in step 5 carry `tenant_id = 01990000-…bb`. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Step 2 `404 Not Found`, `application/problem+json`, `{"title":"Method-comparison study not found.","status":404,"code":"MC-404",…}` from `src/NT.QAMS.Application/AnalyticalQuality/MethodComparisonSlice.cs:113` — thrown **before** the loop, so **no** `BulkImportResultDto` is returned and no per-row `rejected` array leaks partial information. Step 5 `200 OK` with `{"imported":48,"rejected":[{"row":49,"reason":"Measured values must be positive."},{"row":50,"reason":"Measured values must be positive."}]}`. |
| **Expected DB** | Step 4 identical to step 1: B's `pair_count` still 8, B's `qams.measurement_pair` count still 8, and `SELECT count(*) FROM qams.measurement_pair WHERE sample_id LIKE 'SEC026-%'` returns 0 across all tenants. After step 5: 48 new rows, all with `tenant_id = 01990000-…aa` (stamped from the parent by `TenantStampInterceptor.StampOwnedChildren`, `TenantStampInterceptor.cs:68-111`), 0 with B's tenant id. |
| **Expected Audit** | Step 2 produces no `audit.field_change` rows. Step 5 produces field-change rows for A only. |
| **Expected Notification** | n/a — no notification policy handles measurement-pair import. |
| **Cleanup** | Remove the 48 rows written by step 5: `DELETE /api/method-comparisons/<A-study>/pairs/{pairId}` per pair with `X-Change-Reason: TC-TENANT-SEC-026 cleanup`, then re-assert A's study `pair_count` equals its pre-test value. |
| **Evidence** | Two HTTP captures · `psql` counts before/after both steps · the `sample_id LIKE 'SEC026-%'` sweep across all tenants |
| **Result / Defect** | Not Run · — |
| **Notes** | Bulk endpoints are the highest-leverage write target because one request carries N mutations. Two properties are asserted: the loop never runs (zero rows), and the **partial-commit** behaviour that makes this endpoint dangerous is real and confined to A (step 5's 48/2 split). Without step 5 the case could pass against a completely broken endpoint. |

#### TC-TENANT-SEC-027 — Tenant A persists a Tenant B identifier as an attribution: `branch_id` and `assigned_to` are unvalidated and un-constrained  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT, NCR · **no URS requirement covers this** — traced to source; see **GAP-TENANT-903** · RSK-TENANT-002, RSK-TENANT-003 |
| **Level / Type / Technique** | API + Integration · Security (negative) · Error Guessing — probe the *inbound* direction that the isolation design does not cover: not reading B's data, but storing B's identifiers inside A |
| **Priority / Severity / Automation** | High · Medium · Yes (functional + `psql` assertion) |
| **Role / Permission / Tenant** | QualityManager of A holding `nonconformances.approve` · `nonconformances.approve` (`NonconformancesController.cs:53`) · `alpha-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Measured 2026-08-01: `SELECT count(*) FROM pg_constraint WHERE contype='f' AND conrelid='qams.nonconformance'::regclass` returns **0** — the table has no foreign keys at all, so neither `branch_id`, `department_id` nor `assigned_to` is referentially constrained. `RaiseNcHandler` assigns `nc.BranchId = command.BranchId` and `nc.DepartmentId = command.DepartmentId` with no validation (`src/NT.QAMS.Application/Improvement/Commands/NcWorkflowCommands.cs:48-49`); `TriageNcHandler` passes `AssigneeId` straight through (`:118-121`) and `Nonconformance.Triage` stores it unchecked (`src/NT.QAMS.Domain/Improvement/Nonconformance.cs:157-163`). A owns an NC in `status='Raised'`. |
| **Test Data** | (a) `POST /api/nonconformances` body `{"title":"SEC027 probe","description":"cross-tenant attribution probe","severity":3,"likelihood":3,"sourceType":"Internal","branchId":"0199000c-0000-7000-8000-0000000000bb","departmentId":null,"eventType":"Nonconformity"}` — B's branch id. (b) `POST /api/nonconformances/<A-own-nc-id>/triage` body `{"assigneeId":"01990008-0000-7000-8000-0000000000b7"}` — B's user id. |
| **Steps** | 1. As A, send request (a). 2. Record the status and the returned id. 3. `SELECT tenant_id, branch_id FROM qams.nonconformance WHERE id='<new-id>'`. 4. Send request (b). 5. `SELECT tenant_id, assigned_to, status FROM qams.nonconformance WHERE id='<A-own-nc-id>'`. 6. As A, `GET /api/nonconformances/<new-id>` and confirm the response echoes `branchId = 0199000c-…bb`. 7. `GET /api/reports/quality-analytics?branchId=0199000c-0000-7000-8000-0000000000bb` as A and record whether the probe NC is now counted. 8. Confirm **no** notification reached B: `SELECT count(*) FROM qams.notification_dispatch WHERE recipient_user_id='01990008-…b7' AND created_at_utc > <t0>` under bypass. |
| **Expected UI** | The NC form would not offer B's branch in its picker; the probe is issued with `curl.exe`. |
| **Expected API** | Step 1 `201 Created` — **accepted**, with `Location: /api/nonconformances/<new-id>`. Step 4 `204 No Content` — **accepted**. Step 6 `200` echoing `branchId = "0199000c-0000-7000-8000-0000000000bb"`. Record the observed behaviour; do not record a refusal that the code does not produce. |
| **Expected DB** | Step 3: the new row has `tenant_id = 01990000-…aa` (stamped from context by `TenantStampInterceptor.cs:39-55`) and `branch_id = 0199000c-…bb` — a **Tenant B identifier stored inside Tenant A's row**. Step 5: `assigned_to = 01990008-…b7`, `status = 'Assigned'`. No row of B changed; `qams.branch` and `qams.user_account` are untouched. |
| **Expected Audit** | A `audit.field_change` row set for A's NC recording the triage, with `new_value` containing B's user GUID — the foreign identifier is now embedded in A's Part-11 audit trail and cannot be removed (append-only). Assert this explicitly; it is the durable consequence. |
| **Expected Notification** | Step 8 returns **0**. `NotificationDispatcher.DispatchAsync` resolves recipients with `u.TenantId == tenantId && u.IsActive && roles.Contains(...)` (`src/NT.QAMS.Application/Notifications/NotificationDispatcher.cs:57-58`) where `tenantId` is A's, so B's user is never selected however the NC is assigned. **Confidentiality holds; referential integrity does not.** |
| **Cleanup** | The probe NC cannot be deleted (no delete route for nonconformances). Reject it: `POST /api/nonconformances/<new-id>/reject` with `{"reason":"TC-TENANT-SEC-027 fixture cleanup"}` and record its id permanently in the execution record. A's triaged NC cannot be un-triaged; use a disposable NC for step 4. |
| **Evidence** | Two HTTP captures · `psql` row contents showing the foreign GUIDs · the `pg_constraint` count of 0 · the `audit.field_change` row · the zero-dispatch count |
| **Result / Defect** | Not Run · — |
| **Notes** | **`[ID]` — implementation-derived: no URS requirement states that a cross-tenant identifier must be rejected on input.** This is the direction the whole isolation design does not defend: RLS, the EF filter and the tenant-composite FKs all prevent A from *reaching* B, and prevent a child from landing under a foreign parent, but `nonconformance` has no FKs at all, so a foreign branch, department or user GUID is accepted, persisted, echoed back and written into the immutable audit trail. Raised as **GAP-TENANT-903** with acceptance criteria. Severity is Medium rather than High because no B data is disclosed and no B row is modified. |

#### TC-TENANT-SEC-028 — Tenant A cannot make a background job act on Tenant B  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-005 |
| **Level / Type / Technique** | API + Integration · Security (negative) · Use Case combined with Data Flow — trace every request-reachable route into the two elevated `BackgroundService` components and prove the tenant of each unit of work is derived from the data, never from the request |
| **Priority / Severity / Automation** | Critical · Critical · Partial — the route-absence and dispatch assertions automate; observing a sweep round requires a timed integration run |
| **Role / Permission / Tenant** | QualityManager of A, and separately the platform administrator · various · `alpha-lab`, then no tenant |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; single API instance so leader election is not a confound |
| **Preconditions** | `ScheduledSweepService` runs on a 1-hour interval with a 15-second startup delay, elevates at `src/NT.QAMS.Infrastructure/Jobs/ScheduledSweepService.cs:64`, is leader-elected via `AdvisoryLockKeys.ComplianceSweep` (`:70-71`), and issues eight `.IgnoreQueryFilters()` reads (`:91,98,105,112,120,129,136,145`). `OutboxProcessor` elevates at `OutboxProcessor.cs:102`. `NotificationDispatcher.DispatchAsync` calls `tenantSetter.Set(tenantId)` at `NotificationDispatcher.cs:33` with the tenant taken from the domain event or resolved from the owning row (`NotificationPolicies.cs:51,63,71-117,130`). B owns one equipment item with `status='Active'` and `next_calibration_due` = yesterday, so the next sweep round **will** transition it. |
| **Test Data** | Probe routes: `POST /api/jobs/sweep`, `POST /api/admin/sweep`, `POST /api/compliance/sweep`, `POST /api/outbox/drain`, `POST /api/reports/kpi-snapshot`; plus a `grep -c "sweep\|outbox\|snapshot" tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` |
| **Steps** | 1. As A, issue all five probe requests and record the statuses. 2. Search `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` for any route naming a sweep, snapshot, outbox or job trigger. 3. Repeat step 1 as the **platform administrator** to confirm the absence is structural, not permission-based. 4. Record B's equipment row state, then wait for one sweep round (or invoke `RunSweepAsync` directly in an integration harness). 5. After the round, read B's equipment row and A's equipment rows. 6. Read every `qams.notification_dispatch` row created by the round and record its `tenant_id` and `recipient_user_id`. 7. Assert no dispatch created by the round has A's `tenant_id` and a B-derived subject, or B's `tenant_id` and an A recipient. |
| **Expected UI** | n/a — there is no operator screen that triggers a job; that absence is part of the assertion. |
| **Expected API** | All five probes in steps 1 and 3 return `404 Not Found` (no route matches). Step 2 returns **zero** matching routes in the approved surface. The only request-reachable elevated path in the entire codebase is `POST /api/tenants` (front matter §4.3 site 1), which is doubly gated by `[Authorize(Roles = Roles.PlatformAdmin)]` (`TenantsController.cs:12`) and `[RequireRole(UserRole.PlatformAdmin)]` (`ProvisionTenant.cs:17`) and is out of this batch's scope. |
| **Expected DB** | Step 5: B's equipment item transitions from `Active` to `NeedsCalibration` (`ScheduledSweepService.cs:95`) — the job acts on B, correctly, **on its own schedule and by its own data-driven selection**, and A had no influence over whether, when or how. A's equipment rows transition only according to A's own due dates. No row acquires the other tenant's `tenant_id`. |
| **Expected Audit** | The sweep's `audit.field_change` rows for B carry `tenant_id = 01990000-…bb` and for A carry `01990000-…aa`. No row mixes them. `audit.audit_trail` chains for both tenants verify: `GET /api/compliance/chain-verification` returns `ok = true` for A and for B after the round. |
| **Expected Notification** | Step 6/7: every dispatch created by the round has a `recipient_user_id` whose `qams.user_account.tenant_id` equals the dispatch's own `tenant_id`, enforced by `NotificationDispatcher.cs:57-58`. Zero cross-tenant dispatches. |
| **Cleanup** | Restore B's equipment `status` and `next_calibration_due` to their pre-test values via the equipment API under a B session, with an `X-Change-Reason`. Leave the audit rows — append-only. |
| **Evidence** | Ten HTTP captures (five probes × two principals) · the `ApiSurface.approved.txt` search result · B's equipment row before/after · the round's `field_change` and `notification_dispatch` rows with their tenant ids · both chain verifications |
| **Result / Defect** | Not Run · — |
| **Notes** | The honest formulation matters: a background job **does** act on Tenant B — that is its job. What must be false is that *Tenant A can cause it to*. The assertion is therefore (i) no request-reachable trigger exists on any principal, and (ii) the tenant of each unit of work is derived from the row or the domain event, never from a request. Two scope limits, stated: `ScheduledSweepService` applies **no** tenant-status filter (`ScheduledSweepService.cs:86-152`, contrast `KpiSnapshotService.cs:90`), so it would also sweep a suspended tenant — that is **GAP-TENANT-011** and is not testable here because no suspension path exists (**GAP-TENANT-002**). And per **GAP-TENANT-014** no automated control caps the eight `Elevate()` call sites, so this case proves the *current* set is unreachable and cannot prove a ninth will not appear. |

---

## Batch coverage note

**Covered.** Twenty-eight detailed cases, `TC-TENANT-SEC-001` … `TC-TENANT-SEC-028`, all `Not Run · —`, all in the canonical §8 block with the full 28-field set, each naming its technique. Every one of the eleven attack surfaces named in the slice is covered, with the outcome determined per path from source rather than assumed uniform:

| Attack surface | Cases | Outcome asserted, read from source |
|---|---|---|
| Read by id | 001, 002, 003 | `404 NC-404` (`NcQueries.cs:59`) · **`403 AUTHZ-404`** (`AuthorizationSlice.cs:175` + `DomainExceptionHandler.cs:63` before `:69`) · `404 EQP-404` (`EquipmentSlice.cs:85`) |
| Update | 004, 005, 006 | `404 NC-404` (`NcWorkflowCommands.cs:106`) · `403 AUTHZ-403` when the permission gate fires first (`RequirePermissionAttribute.cs:52-62`) · `404 USER-404` on the non-RLS `user_account` (`UserManagement.cs:112`) |
| Delete | 007, 008, 009 | `404 MC-404` on the parent load (`MethodComparisonSlice.cs:93`) · `400 CHANGE-REASON-REQUIRED` ahead of everything (`RequestIdentity.cs:149-156`) · `404 ARC-404` (`ArchiveLoader:79-81`) |
| List / search | 010, 011, 012 | `200` with `total: 0` and `items: []`, asserted at four page sizes and beyond the last page |
| Export | 013, 014, 015 | `200` with an **exact own-tenant row count** in the workbook + a `RECORD_EXPORTED` security event carrying A's `tenant_id` · null-tenant `audit.field_change` rows absent because shape R leaves `USING` strict · `404 MRV-404` on a foreign review id |
| File download | 016, 017 | `404` **without a `code` extension** (framework client-error mapping of `NotFound()`) · no key-addressed route exists and content-addressing is per-tenant-directory |
| Notification retrieval | 018, 019, 020 | `200` bounded twice (recipient + tenant) · `404 NTF-404` · the administrative monitor fenced by the tenant layers alone |
| Report / dashboard aggregation | 021, 022, 023 | twelve KPIs each independently falsifiable · `read.kpi_snapshot` written elevated, read fenced · a foreign `branchId` yields zeros and is not an existence oracle |
| Id enumeration | 024, 025 | foreign id and never-existing id byte-identical across five endpoints · `chain-verification` takes its tenant from the JWT claim only |
| Bulk operation | 026 | zero loop iterations, zero rows, no `BulkImportResultDto`; with a control run proving the 48/2 partial-commit path is real and confined to A |
| Background job acting on B | 028 | no request-reachable trigger on any principal; each unit of work's tenant derived from the row or the domain event |
| *(additional)* Inbound cross-tenant identifiers | 027 | `201`/`204` — **accepted and persisted**; a real finding, written `[ID]` against the code, not as a passing isolation case |

**Not covered in this batch, and why.**

1. **`TC-TENANT-SEC-005`'s platform-administrator column.** Whether a platform administrator (who holds `SetPlatformAdmin()` and no tenant, `RequestIdentity.cs:114-117`) satisfies `IUserPrivileges.Has("…")` was not read in this pass; the front matter marks the same cell `[RNV]` at §4.5 footnote †. No case in this batch asserts a platform-admin status code against a tenant endpoint. Batch C owns it.
2. **The typed HTTP shape of an RLS `WITH CHECK` refusal (SqlState `42501`) and a tenant-composite FK refusal (`23503`).** Every case here is stopped by a tenant-filtered read *before* a write is attempted, so no case in this slice reaches the database refusal. Reaching it requires bypassing the application filter, which is `TC-TENANT-RLS-nnn` / `TC-TENANT-INT-nnn` territory. Per **GAP-TENANT-006** those refusals currently surface as untyped HTTP 500.
3. **Suspended- and terminated-tenant attack variants** (does a valid token for a suspended tenant still reach B's fence in the same way?). Unreachable: there is no suspension path at all (**GAP-TENANT-002**), and the condition can only be forced by an out-of-band `UPDATE saas.tenant`, which is not a supported operation. Any such case would be `[GD]` on **GAP-TENANT-001** and **-002**.
4. **Production-grade least privilege.** Every isolation claim in this batch is conditional on the runtime role being neither owner nor `BYPASSRLS`. In dev, `qams_app` is the owner. Per **GAP-TENANT-008** the hardening script cannot execute, so **a green run of this batch in dev proves the application-layer fence, not the database fence.** Cases 001–028 should be re-executed on a role-split installation before any of them is cited as qualification evidence, and the execution record must state which environment produced each result.
5. **The remaining 40 `[HttpGet("{id:guid}")]` read-by-id routes, the other 25 DELETE routes and the remaining exports.** Three read-by-id modules, three delete routes and three exports were sampled as equivalence-partition representatives. The residual risk is a module whose handler forgot the pattern; that is a *structural* question and is better answered by an architecture test than by 40 more cases — see **GAP-TENANT-906** below.

**New gaps found while authoring this batch.** Numbered `GAP-TENANT-9xx` so they cannot collide with the front matter's `001…014` sequence.

**GAP-TENANT-901 — The `TC-TENANT-SEC` ID reservation is over-subscribed.** The front-matter reservation table (`docs/testing/12-module-tenancy-rls.md:25,29`) reserves `TC-TENANT-SEC-001…020` for batch B and `021…030` for batch C, but this slice requires twenty-eight cases to cover its eleven mandated attack surfaces at one case per surface plus the outcome variants the code actually produces. This file consumes **001…028**, which overlaps batch C's `021…030`. *Impact:* if batch C is authored against its current reservation, eight ids collide and the traceability matrix silently double-counts. *Acceptance criteria:* (1) the front-matter table is amended to read batch B `TC-TENANT-SEC-001…028` and batch C `TC-TENANT-SEC-031…040`; (2) the batch-C authoring prompt is updated before that file is written; (3) a matrix build step fails on any duplicate test-case id across `docs/testing/*.md`. *Severity:* **High** (traceability integrity). *Owner:* Validation Lead.

**GAP-TENANT-902 — `AUTHZ-404` is reported as HTTP 403, because the prefix arm precedes the suffix arm.** `src/NT.QAMS.Application/Competency/AuthorizationSlice.cs:128,175` throw `DomainException("AUTHZ-404", "Test authorization not found.")`. `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:63-68` matches `Code.StartsWith("AUTHZ-")` → **403** and is evaluated before `:69-74`'s `Code.EndsWith("-404")` → 404. A not-found is therefore reported as a permission refusal on the `test-authorizations` surface, and only there. *Impact:* the isolation error surface is inconsistent across modules (404 on nonconformances, equipment, archives, users, notifications; 403 here); the SPA cannot distinguish "you lack permission" from "no such record"; and any authorization-matrix test that counts 403s will mis-attribute this one. *Acceptance criteria:* (1) the code is renamed to a non-`AUTHZ-` not-found code (e.g. `TAUTH-404`), or the handler's arm order is changed so `-404` is evaluated first with a documented rationale; (2) a unit test over `DomainExceptionHandler` asserts the status for each of the four code shapes including the `AUTHZ-…-404` overlap; (3) an architecture test asserts that no code both starts with `AUTHZ-` and ends with `-404`. *Severity:* **Medium**. *Owner:* Backend Lead. *Pinned by:* `TC-TENANT-SEC-002`.

**GAP-TENANT-903 — A foreign tenant's identifiers are accepted, persisted and written into the audit trail.** `qams.nonconformance` carries **zero** foreign-key constraints (measured: `SELECT count(*) FROM pg_constraint WHERE contype='f' AND conrelid='qams.nonconformance'::regclass` = 0), and no handler validates the identifiers it stores: `RaiseNcHandler` assigns `nc.BranchId`/`nc.DepartmentId` verbatim (`NcWorkflowCommands.cs:48-49`), `TriageNcHandler` passes `AssigneeId` through (`:118-121`), and `Nonconformance.Triage` stores it unchecked (`Nonconformance.cs:157-163`). A Tenant A user can therefore file a nonconformance attributed to Tenant B's branch and assign it to Tenant B's user; the row is stamped with A's `tenant_id`, so no data crosses, but a foreign GUID is durably embedded in A's records and in A's append-only `audit.field_change` trail. *Impact:* referential integrity and data-quality failure inside a regulated record; branch/department analytics silently mis-attribute; a foreign user id in an immutable audit trail cannot be corrected, only superseded. Contrast the owned-child path, where `Hardening4_ChildTenancy`'s tenant-composite FKs make exactly this class of drift structurally impossible — the protection exists for children and is absent for aggregate-level attributions. *Acceptance criteria:* (1) each of `branch_id`, `department_id` and `assigned_to` is either given a tenant-composite FK to its parent (`FOREIGN KEY (branch_id, tenant_id) REFERENCES qams.branch(id, tenant_id)`) or validated in the command handler with a documented domain code at HTTP 422; (2) an integration test proves a foreign branch id and a foreign assignee id are both refused; (3) an audit of the other aggregate roots identifies every un-constrained cross-aggregate GUID column and each is closed the same way; (4) existing rows are surveyed for foreign attributions before the constraint is added. *Severity:* **Medium** (no confidentiality breach; integrity and data-quality defect). *Owner:* Backend Lead + Database Owner. *Pinned by:* `TC-TENANT-SEC-027`.

**GAP-TENANT-904 — Cross-tenant probing is invisible: no security event, and one 404 has no `code`.** Two related observations. (a) None of the twenty-eight attacks in this batch writes an `audit.security_event` row: read-by-id, list, update, delete, bulk, enumeration and failed export attempts all fail silently from a detection standpoint, so an estate-wide enumeration sweep leaves no trace in the ledger that `GET /api/compliance/security-events` exposes. Successful exports *are* logged (`ExportsController.cs:168-169`), failed ones are not (`:155` is reached only after the pack is built). (b) `FilesController.cs:65` returns `NotFound()`, whose framework client-error mapping produces a `problem+json` body with `title`, `status`, `type`, `traceId` and `correlationId` but **no `code`** — breaking the API-003 contract that "every failure path emits the same shape with a stable code" (`ProblemResponse.cs:6-13`), which every other case in this batch relies on. *Impact:* (a) a cross-tenant reconnaissance campaign is undetectable by the product's own compliance surface; (b) a client cannot branch on the file-download failure the way it branches on every other failure. *Acceptance criteria:* (1) a decision is recorded on whether cross-tenant refusals should emit a `security_event` (e.g. `CROSS_TENANT_REFUSED`) and, if so, the emission is rate-limited so an enumeration sweep cannot itself become a write-amplification denial of service; (2) `FilesController.Download` returns a typed domain code (e.g. `FILE-404`, already in use at `DocumentCommands.cs:45`) so the body carries `code`; (3) a functional test asserts that every 4xx emitted by the API carries a `code` extension. *Severity:* **Medium**. *Owner:* Security Architect + Backend Lead. *Pinned by:* `TC-TENANT-SEC-015`, `-016`, `-024`.

**GAP-TENANT-905 — `qams.idempotency_record` is outside RLS and appears in no isolation register.** Measured 2026-08-01: `idempotency_record` has `relrowsecurity=f, relforcerowsecurity=f` and **no `tenant_id` column** (columns: `id, actor_id, idempotency_key, request_type, response_json, created_at_utc`, `IdempotencyRecordConfiguration.cs:12-25`). It stores whole serialized command responses (`response_json`). It is correctly absent from the front matter's 90-table RLS inventory and from the two accepted B9 exceptions, because both of those sets are defined over tables that *carry* `tenant_id` — so there is no contradiction, but there is also no register in which this table's isolation posture is stated. Analysis of the current design: the replay anchor is the unique index on `(actor_id, idempotency_key, request_type)` (`:19-22`) and `actor_id` is a globally unique `user_account.id`, so two tenants cannot collide on a key and no cross-tenant replay is reachable today. *Impact:* documentation and review coverage, not a demonstrated leak. A future change that keyed replay on something less than a globally unique actor — a tenant-local sequence, an anonymous session id, or a platform-admin actor operating across tenants — would create a cross-tenant response-replay channel with no database fence behind it. *Acceptance criteria:* (1) the table is added to the isolation register with its posture and rationale stated ("no `tenant_id`; isolation derives from the global uniqueness of `actor_id`"); (2) an architecture or integration test asserts that every table in schemas `qams`, `read` and `audit` is in exactly one of three sets — RLS-protected, accepted-deviation, or no-tenant-data-by-design — so a new table cannot be added to none of them; (3) a comment at `IdempotencyRecord.cs:12` records the actor-uniqueness dependency. *Severity:* **Low**. *Owner:* Database Owner + Validation Lead.

**GAP-TENANT-906 — No structural control asserts the read-by-id tenant-boundary pattern across all modules.** This batch sampled three of the roughly forty `[HttpGet("{id:guid}")]` routes, three of the twenty-six DELETE routes and three of the four exports as equivalence-partition representatives. Each sampled handler happens to load through a tenant-filtered `DbSet` and throw a `-404` domain code, but nothing enforces that pattern: a new handler could call `.IgnoreQueryFilters()`, or accept a caller-supplied tenant id (as `VerifyChainAsync` already does, safely, at `ComplianceLedgerServices.cs:209`), and no test would fail. The parallel and weaker risk — an unbounded `user_account` query — *does* have a build-time guard (`UserAccountTenantBoundTests`). *Impact:* coverage of "every read-by-id is tenant-fenced" is a sampling claim, not a proof, and it decays with every new module. *Acceptance criteria:* (1) an architecture test enumerates every `IQueryHandler`/`ICommandHandler` in `src/NT.QAMS.Application` and fails on any use of `IgnoreQueryFilters()` outside an explicit allow-list carrying a one-line justification per entry (the same shape as the `Elevate()` allow-list proposed in **GAP-TENANT-014**); (2) a second test fails on any handler that accepts a `Guid tenantId` parameter unless allow-listed; (3) both allow-lists are regenerated into this module's front matter so the document and the build cannot disagree. *Severity:* **Medium**. *Owner:* Security Architect + Backend Lead.

*Nothing in this file was executed. Every `Result / Defect` reads `Not Run · —`. No compliance control is marked compliant anywhere in this batch.*
