# RBAC — Detailed Test Cases, Batch A

This batch authors HTTP permission-gate enforcement for the **Quality** module group (`nc`, `complaints`, `feedback`, `audits`, `objectives`, `changes`, `reviews`) and the **Documents** module group (`documents`, `quality-policy`, `records`) — 42 cases consuming `TC-RBAC-API-001` … `TC-RBAC-API-042`. For each of the ten modules it pairs a holder-allowed case against a non-holder-denied case on a representative `[RequirePermission]` call site, asserting the real HTTP status and the exact `application/problem+json` body written by `RequirePermissionAttribute.OnAuthorizationAsync` (`src/NT.QAMS.WebApi/Authorization/RequirePermissionAttribute.cs:54-60`), and it adds twelve **adjacent-but-insufficient** cases in which the actor holds a neighbouring action on the same module (`.view`/`.edit`/`.approve`) but not the action the route demands. It also pins two pipeline-ordering behaviours on the `records` module (`ChangeReasonMiddleware` at `Program.cs:269` runs *before* `UseAuthorization()` at `:270`) and three `[GD]` cases for permission keys in this slice that exist in the catalogue but gate nothing. **Deliberately left to sibling batches:** `PermissionCatalog`/`Role` domain units and the role lifecycle state machine (batch A's original reservation — see GAP-RBAC-902); administration, people, operations, risk, resources and analytical gates (batches B/C); decision-table enumeration over the privilege matrix (batch C); per-request privilege resolution, immediate grant/revoke, tenant isolation on `qams.role*` and branch/department scoping (batch D); every `SOD-*` duty-pair rule and the `ROLE-006` lockout guard (batch E) — this batch asserts *only* the 403/allow decision at the gate, never the domain rule behind it; frontend affordances, e2e journeys and accessibility (batch F).

**Conventions in force.** `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md` §4 (28 mandatory fields), §5 (id convention), §6 (honesty rules), §8 (canonical block). Front matter: `docs/testing/11-module-rbac.md`.

**Risk ids used.** No per-risk ids exist in `docs/validation/02-Functional-Risk-Assessment.md` — it assesses at area level only (row *Access control / RBAC*, URS-005/006, S=High P=Med D=Med, **HIGH**). Per conventions §5 the following are **minted here** and declared as minted:

| Risk id | Statement |
|---|---|
| `RSK-RBAC-001` | An actor without the required capability completes a controlled quality- or document-record transition. |
| `RSK-RBAC-002` | An actor holding the required capability is refused, stalling a regulated workflow. |
| `RSK-RBAC-003` | An adjacent privilege on the same module is treated as sufficient, widening reach beyond the granted action. |
| `RSK-RBAC-004` | A configured privilege has no enforcement point, so a user-access review certifies a control that does not exist. |
| `RSK-RBAC-005` | An authorization refusal is ordered wrongly against another pre-check, or leaks record existence. |

**The refusal body, measured once and asserted by reference below.** `RequirePermissionAttribute` writes through `ProblemResponse.WriteAsync(context, 403, "You do not have permission to perform this action.", ProblemAuthorizationResultHandler.ForbiddenCode)` (`RequirePermissionAttribute.cs:54-60`). `ProblemResponse` (`src/NT.QAMS.WebApi/Middleware/ProblemResponse.cs:21-46`) emits `Content-Type: application/problem+json`, `status = 403`, `title = "You do not have permission to perform this action."`, `code = "AUTHZ-403"` (`ProblemAuthorizationResultHandler.cs:16`), plus extensions `traceId` and — when `ObservabilityMiddleware` set it — `correlationId` (`ProblemResponse.cs:24-29`). Cases below name this shape **PROBLEM-403**; asserting it means asserting all five of those facts, not the status alone.

**Seeded-role grants used as fixtures**, read from `src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs:95-194` — Tenant Administrator = all keys (`:100`); Quality Manager = everything except `users`, `tenant-settings`, `roles` beyond `view`, `organization.manage` (`:107-117`); Department Head (`:123-147`) and Analyst (`:153-177`) are explicit per-module tables; External Auditor = `view`+`export` on non-administration modules, with `quality-policy` and `access-reviews` excluded outright and `reviews` reduced to `view` (`:184-193`).

---

#### TC-RBAC-API-001 — `nc.approve` holder triages a nonconformance  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — the "grants the required key" partition of DT-1 row 3 |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `nc.approve` (gate `NonconformancesController.cs:52-53`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; the `/api/v1/...` mirror asserted identically |
| **Preconditions** | Account `qm@demo-lab.local` active, `user_account.role_id` = the seeded `Quality Manager` role id; that role's `qams.role_permission` contains `nc.approve` (seeded by `SystemRoleCatalog.cs:107-117`, which excludes only `users`/`tenant-settings`/`roles`/`organization.manage`). One `qams.nonconformance` row in `status = 'Raised'` — the only state `Triage` accepts (`Nonconformance.cs:159`, else `NC-011`). |
| **Test Data** | NC id `11111111-1111-4111-8111-000000000001`; body `{"assigneeId":"22222222-2222-4222-8222-000000000002"}` |
| **Steps** | 1. `POST /api/auth/login` as `qm@demo-lab.local` and keep the access JWT. 2. `POST /api/nonconformances/11111111-1111-4111-8111-000000000001/triage` with the body above and `Authorization: Bearer <jwt>`. 3. Record status, `Content-Type` and body. 4. `SELECT status, assigned_to FROM qams.nonconformance WHERE id='11111111-1111-4111-8111-000000000001';` |
| **Expected UI** | The NC detail screen renders the Triage control, because `PermissionsService.can('nc.approve')` is true once `GET /auth/me/privileges` lands (`frontend/src/app/core/permissions.service.ts:67-70`). |
| **Expected API** | `204 No Content`, empty body — `Triage` returns `NoContent()` (`NonconformancesController.cs:57`). No `problem+json`. |
| **Expected DB** | `qams.nonconformance.status` moves `'Raised'` → `'Assigned'`; `assigned_to = '22222222-2222-4222-8222-000000000002'` (`Nonconformance.cs:160-161`). |
| **Expected Audit** | One `audit.audit_trail` row for the `NcTriaged` domain event (`Nonconformance.cs:162`) carrying `tenant_id` = the `demo-lab` tenant, non-null `prev_hash`/`entry_hash`. |
| **Expected Notification** | n/a — no notification rule is asserted by this batch; notification policy is out of the RBAC slice. |
| **Cleanup** | `UPDATE qams.nonconformance SET status='Raised', assigned_to=NULL WHERE id='11111111-1111-4111-8111-000000000001';` executed with `SELECT set_config('app.bypass_rls','on',false);` first. The `audit_trail` row is append-only and is **not** removed. |
| **Evidence** | HTTP response capture (status + headers + body) · SQL result before/after · `audit_trail` row |
| **Result / Defect** | Not Run · — |
| **Notes** | This case asserts the **gate only**. `RaiseNcCommand`/`TriageNcCommand` also pass `[RequireInternalActor]` at the command layer (`AuthorizationBehavior.cs:49-86`); that dispatch is batch D's. |

---

#### TC-RBAC-API-002 — Analyst without `nc.approve` is refused the triage  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-001 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — the "authenticated, role active, key absent" partition (DT-1 row 4) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · required `nc.approve`; held on `nc`: `nc.view`, `nc.create`, `nc.edit`, `nc.export` only (`SystemRoleCatalog.cs:154`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Account `analyst@demo-lab.local` active on the seeded `Analyst` role. Verify the absence directly: `SELECT permission_key FROM qams.role_permission rp JOIN qams.role r ON r.id=rp.role_id AND r.tenant_id=rp.tenant_id WHERE r.normalized_name='ANALYST' AND rp.permission_key LIKE 'nc.%';` returns exactly four rows and **not** `nc.approve`. NC id as in TC-RBAC-API-001, still `status='Raised'`. |
| **Test Data** | NC id `11111111-1111-4111-8111-000000000001`; body `{"assigneeId":"22222222-2222-4222-8222-000000000002"}` |
| **Steps** | 1. Log in as `analyst@demo-lab.local`. 2. `POST /api/nonconformances/11111111-1111-4111-8111-000000000001/triage` with the body above. 3. Capture status, `Content-Type` and the full JSON body. 4. `SELECT status, assigned_to FROM qams.nonconformance WHERE id='11111111-1111-4111-8111-000000000001';` 5. `SELECT count(*) FROM audit.security_event WHERE occurred_at_utc > <t0>;` after `SELECT set_config('app.bypass_rls','on',false);` |
| **Expected UI** | The Triage control is **not rendered** — `can('nc.approve')` is false. This is affordance only and must not be treated as the control (`permissions.service.ts:13-14`). |
| **Expected API** | **PROBLEM-403**: `403`, `Content-Type: application/problem+json`, `title = "You do not have permission to perform this action."`, `code = "AUTHZ-403"`, `traceId` present. |
| **Expected DB** | `status` still `'Raised'`, `assigned_to` still `NULL` — the filter short-circuits with `context.Result = new EmptyResult()` before the action, so no handler and no `SaveChanges` runs (`RequirePermissionAttribute.cs:54`). |
| **Expected Audit** | **No `audit.audit_trail` row and no `audit.security_event` row.** `RequirePermissionAttribute` calls no `ISecurityEventLog`; the refusal exists only in the canonical request log emitted by `ObservabilityMiddleware` (`ObservabilityMiddleware.cs:65-81`, `outcome = "client-error"`). See GAP-RBAC-904. |
| **Expected Notification** | n/a — no notification is defined for an authorization refusal. |
| **Cleanup** | None — the request changed nothing. |
| **Evidence** | HTTP response capture · SQL result showing the unchanged row · `security_event` count delta = 0 · application log line for the request |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert `code` as the exact string `AUTHZ-403`; do not accept a bare `403` with no body — that would be the pre-API-003 framework behaviour the handler exists to replace. |

---

#### TC-RBAC-API-003 — Adjacent privilege: `nc.void` holder cannot triage  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-003 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Pairwise — (module = `nc`) × (held action = `void`) × (required action = `approve`) |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Bespoke role `RBAC-A-NC-VOID` · holds exactly `nc.view`, `nc.void`; required `nc.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Tenant Administrator created the role: `POST /api/roles` (gate `roles.manage`, `RolesController.cs:38-45`) with `{"name":"RBAC-A-NC-VOID","permissions":["nc.view","nc.void"]}`; both keys are members of `PermissionCatalog.AllKeys` (`PermissionCatalog.cs:136` — `nc` uses `SignedRecordLifecycle`), so `ROLE-005` is not raised. Account `nc-void@demo-lab.local` assigned to it via `PUT /api/users/{id}/assigned-role`. NC in `status='Raised'`. |
| **Test Data** | NC id `11111111-1111-4111-8111-000000000001`; body `{"assigneeId":"22222222-2222-4222-8222-000000000002"}` |
| **Steps** | 1. Log in as `nc-void@demo-lab.local`. 2. `GET /api/auth/me/privileges` and confirm the returned set is exactly `["nc.view","nc.void"]`. 3. `POST /api/nonconformances/11111111-1111-4111-8111-000000000001/triage`. 4. Capture status and body. |
| **Expected UI** | Triage hidden; Reject offered — the two controls key off different `can()` calls. |
| **Expected API** | **PROBLEM-403** with `code = "AUTHZ-403"`. Holding a *different* action on the *same* module grants nothing — `Has()` is an exact-string set lookup (`PrivilegeResolution.cs:39`, `Permissions.Contains(permissionKey)`), never a prefix or hierarchy match. |
| **Expected DB** | `qams.nonconformance.status` unchanged at `'Raised'`. |
| **Expected Audit** | No `audit.audit_trail` and no `audit.security_event` row (as TC-RBAC-API-002). |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | `POST /api/roles/{roleId}/deactivate` — the role cannot be deleted (no `[HttpDelete]` on `RolesController`, GAP-RBAC-015). Reassign `nc-void@demo-lab.local` to `Analyst` first; `ROLE-006` cannot fire here because the role never held `roles.manage`. |
| **Evidence** | HTTP response capture · `/auth/me/privileges` capture proving the held set is exactly `["nc.view","nc.void"]` · SQL showing the unchanged row |
| **Result / Defect** | Not Run · — |
| **Notes** | The prefix question is the point: `nc.void` shares the `nc.` prefix with `nc.approve` and is refused, proving the lookup is an exact-string set membership test and not a prefix or hierarchy match. |

---

#### TC-RBAC-API-004 — `nc.void` holder rejects a nonconformance  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — DT-1 row 3 applied to the second `nc` gate |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Bespoke role `RBAC-A-NC-VOID` (`nc.view`, `nc.void`) · `nc.void` (gate `NonconformancesController.cs:60-61`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Role and account from TC-RBAC-API-003. NC `status='Raised'` — the only state `Reject` accepts (`Nonconformance.cs:167`, else `NC-012`). A non-blank reason is mandatory (`NC-013`, `Nonconformance.cs:168-171`). |
| **Test Data** | NC id `11111111-1111-4111-8111-000000000003`; body `{"reason":"Duplicate of NC-2026-0004; consolidated at triage."}` |
| **Steps** | 1. Log in as `nc-void@demo-lab.local`. 2. `POST /api/nonconformances/11111111-1111-4111-8111-000000000003/reject` with the body above. 3. Capture status. 4. `SELECT status, rejection_reason FROM qams.nonconformance WHERE id='11111111-1111-4111-8111-000000000003';` |
| **Expected UI** | Reject control rendered; Triage absent (the mirror of TC-RBAC-API-003). |
| **Expected API** | `204 No Content`, empty body (`NonconformancesController.cs:65`). |
| **Expected DB** | `status` `'Raised'` → `'Rejected'`; `rejection_reason = 'Duplicate of NC-2026-0004; consolidated at triage.'` trimmed (`Nonconformance.cs:173-174`). |
| **Expected Audit** | One `audit.audit_trail` row for `NcRejected` (`Nonconformance.cs:175`), tenant-stamped. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `UPDATE qams.nonconformance SET status='Raised', rejection_reason=NULL WHERE id='11111111-1111-4111-8111-000000000003';` under `app.bypass_rls='on'`. |
| **Evidence** | HTTP response capture · SQL before/after · `audit_trail` row |
| **Result / Defect** | Not Run · — |
| **Notes** | Pairs with TC-RBAC-API-003 to show the two `nc` gates are independent in both directions. |

---

#### TC-RBAC-API-005 — `complaints.approve` holder validates a complaint  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — the complaint validation step of the ISO 17025 §7.9 complaint workflow |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `complaints.approve` (gate `ComplaintsController.cs:48-49`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qm@demo-lab.local` on the seeded `Quality Manager` role, which holds every `complaints.*` key. One `qams.complaint` row in `status='Acknowledged'` (the state `Validate` accepts; the acknowledge step is `CMP-010`-guarded at `Complaint.cs:88`). |
| **Test Data** | Complaint id `33333333-3333-4333-8333-000000000001`; body `{"justified":true,"reason":"Reported turnaround breach confirmed against the LIMS timestamps."}` |
| **Steps** | 1. Log in as `qm@demo-lab.local`. 2. `POST /api/complaints/33333333-3333-4333-8333-000000000001/validate` with the body above. 3. Capture status. 4. `SELECT status FROM qams.complaint WHERE id='33333333-3333-4333-8333-000000000001';` |
| **Expected UI** | Validate control rendered on the complaint detail screen. |
| **Expected API** | `204 No Content` (`ComplaintsController.cs:53`). |
| **Expected DB** | `qams.complaint.status` moves `'Acknowledged'` → `'Validated'` (`ComplaintStatus` enum: `Logged, Acknowledged, Validated, Investigating, OutcomeLogged, Resolved, Closed, Invalid` — `Complaint.cs:6-9`). |
| **Expected Audit** | One `audit.audit_trail` row for the `ComplaintValidated` domain event, tenant-stamped. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `UPDATE qams.complaint SET status='Acknowledged' WHERE id='33333333-3333-4333-8333-000000000001';` under `app.bypass_rls='on'`. |
| **Evidence** | HTTP response capture · SQL before/after · `audit_trail` row |
| **Result / Defect** | Not Run · — |
| **Notes** | `justified:true` is chosen because it is the branch that demands a linked NC downstream; the RBAC assertion is unaffected either way. |

---

#### TC-RBAC-API-006 — Adjacent privilege: Department Head holds `complaints.edit` but not `.approve`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-003 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — DT-1 row 4 with a **seeded** role, so the case also pins the seed parity table |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · holds `complaints.view/create/edit/export` (`SystemRoleCatalog.cs:125`); required `complaints.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `dh@demo-lab.local` active on the seeded `Department Head` role. Confirm by query that `complaints.approve` and `complaints.void` are absent from that role's grants. Complaint in `status='Acknowledged'`. |
| **Test Data** | Complaint id `33333333-3333-4333-8333-000000000001`; body `{"justified":true,"reason":"Attempt by a role holding edit only."}` |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/complaints/33333333-3333-4333-8333-000000000001/validate`. 3. Capture status + body. 4. Immediately `POST /api/complaints/33333333-3333-4333-8333-000000000001/acknowledge` — expect the *different* outcome recorded in TC-RBAC-API-007, proving the two gates on the same controller resolve independently. 5. `SELECT status FROM qams.complaint WHERE id='33333333-3333-4333-8333-000000000001';` |
| **Expected UI** | Validate hidden, Acknowledge offered. |
| **Expected API** | Step 2: **PROBLEM-403**, `code = "AUTHZ-403"`. |
| **Expected DB** | `status` unchanged by step 2 (`'Acknowledged'`). |
| **Expected Audit** | No ledger row and no `audit.security_event` row for step 2. |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | Revert whatever step 4 changed: `UPDATE qams.complaint SET status='Acknowledged' WHERE id='33333333-3333-4333-8333-000000000001';`. |
| **Evidence** | Two HTTP response captures (403 then 204) · SQL after each · `security_event` count delta = 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | Highest-value adjacency in the Quality group: a Department Head genuinely *works* on complaints, so a hierarchy-style implementation would have leaked `approve` here. |

---

#### TC-RBAC-API-007 — Department Head with `complaints.edit` acknowledges a complaint  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — the granted-key partition on the `complaints.edit` gate |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `complaints.edit` (gate `ComplaintsController.cs:40-41`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `dh@demo-lab.local` as above. One `qams.complaint` row in `status='Logged'` — the only state `Acknowledge` accepts (`Complaint.cs:88`, else `CMP-010`). |
| **Test Data** | Complaint id `33333333-3333-4333-8333-000000000002` (status `Logged`); no request body |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/complaints/33333333-3333-4333-8333-000000000002/acknowledge`. 3. Capture status. 4. `SELECT status, acknowledged_at_utc FROM qams.complaint WHERE id='33333333-3333-4333-8333-000000000002';` |
| **Expected UI** | Acknowledge control rendered. |
| **Expected API** | `204 No Content` (`ComplaintsController.cs:45`). |
| **Expected DB** | `status` `'Logged'` → `'Acknowledged'`; `acknowledged_at_utc` set to the `IClock` value (`Complaint.cs:89-90`). |
| **Expected Audit** | One `audit.audit_trail` row for `ComplaintAcknowledged` (`Complaint.cs:91`). |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `UPDATE qams.complaint SET status='Logged', acknowledged_at_utc=NULL WHERE id='33333333-3333-4333-8333-000000000002';`. |
| **Evidence** | HTTP response capture · SQL before/after · `audit_trail` row |
| **Result / Defect** | Not Run · — |
| **Notes** | Three further routes share this same key — `/start-investigation` (`:57`), `/outcome` (`:65`), `/resolve` (`:73`); this case is the representative and the other three are covered by inspection, stated here rather than silently dropped. |

---

#### TC-RBAC-API-008 — Adjacent privilege: Department Head cannot close a complaint (`complaints.void`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-003 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Pairwise — (held = `edit`) × (required = `void`) × (state-destroying transition) |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · required `complaints.void` (gate `ComplaintsController.cs:80-81`); not held · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `dh@demo-lab.local` as above. One `qams.complaint` row in `status='Resolved'` (the state `Close` accepts, `Complaint.cs:157`, else `CMP-015`) with `linked_nc_id IS NULL` so `CMP-020` cannot confound the result. |
| **Test Data** | Complaint id `33333333-3333-4333-8333-000000000003`; no request body |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/complaints/33333333-3333-4333-8333-000000000003/close`. 3. Capture status, `Content-Type` and body. 4. `SELECT status FROM qams.complaint WHERE id='33333333-3333-4333-8333-000000000003';` |
| **Expected UI** | Close control hidden. |
| **Expected API** | **PROBLEM-403**, `code = "AUTHZ-403"` — **not** `422 CMP-015` and **not** `422 CMP-020`, because the authorization filter runs before model binding and before the handler. |
| **Expected DB** | `status` still `'Resolved'`. |
| **Expected Audit** | No ledger row; no `audit.security_event` row. |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | None — nothing changed. |
| **Evidence** | HTTP response capture · SQL showing the unchanged row |
| **Result / Defect** | Not Run · — |
| **Notes** | The precondition deliberately puts the record in a *valid* state for the action so that a 403 cannot be confused with a state-machine refusal. |

---

#### TC-RBAC-API-009 — Department Head with `feedback.edit` reviews a feedback entry  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — granted-key partition on `feedback.edit` |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `feedback.edit` (gate `FeedbackController.cs:35-36`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `dh@demo-lab.local` on the seeded `Department Head` role, which holds `feedback.view/create/edit/void/export` (`SystemRoleCatalog.cs:126`). One `qams.feedback_entry` row in `status='Logged'` (`FeedbackStatus` = `Logged, Reviewed, Closed, Escalated`, `FeedbackEntry.cs:8`). |
| **Test Data** | Feedback id `44444444-4444-4444-8444-000000000001`; body `{"reviewNotes":"Turnaround compliment logged for the management review input pack."}` |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/feedback/44444444-4444-4444-8444-000000000001/review` with the body above. 3. Capture status. 4. `SELECT status FROM qams.feedback_entry WHERE id='44444444-4444-4444-8444-000000000001';` |
| **Expected UI** | Review control rendered. |
| **Expected API** | `204 No Content` (`FeedbackController.cs:40`). |
| **Expected DB** | `qams.feedback_entry.status` moves `'Logged'` → `'Reviewed'`. |
| **Expected Audit** | One `audit.audit_trail` row for the feedback-reviewed domain event, tenant-stamped. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `UPDATE qams.feedback_entry SET status='Logged' WHERE id='44444444-4444-4444-8444-000000000001';`. |
| **Evidence** | HTTP response capture · SQL before/after · `audit_trail` row |
| **Result / Defect** | Not Run · — |
| **Notes** | `POST /api/feedback/{id}/escalate` (`FeedbackController.cs:52`) shares this key; not separately cased. |

---

#### TC-RBAC-API-010 — Analyst without `feedback.edit` is refused the review  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-001 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — key-absent partition, seeded role |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · holds `feedback.view/create/export` only (`SystemRoleCatalog.cs:156`); required `feedback.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `analyst@demo-lab.local` on the seeded `Analyst` role. Confirm `feedback.edit`, `feedback.approve` and `feedback.void` are all absent from that role's grants. Feedback entry in `status='Logged'`. |
| **Test Data** | Feedback id `44444444-4444-4444-8444-000000000001`; body `{"reviewNotes":"Attempt by a create-only role."}` |
| **Steps** | 1. Log in as `analyst@demo-lab.local`. 2. `POST /api/feedback/44444444-4444-4444-8444-000000000001/review`. 3. Capture status + body. 4. `SELECT status FROM qams.feedback_entry WHERE id='44444444-4444-4444-8444-000000000001';` |
| **Expected UI** | Review control hidden. |
| **Expected API** | **PROBLEM-403**, `code = "AUTHZ-403"`. |
| **Expected DB** | `status` unchanged at `'Logged'`. |
| **Expected Audit** | No ledger row; no `audit.security_event` row. |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | None. |
| **Evidence** | HTTP response capture · SQL showing the unchanged row |
| **Result / Defect** | Not Run · — |
| **Notes** | The Analyst *can* create feedback (`feedback.create`) — but `POST /api/feedback` carries **no** `[RequirePermission]` at all (`FeedbackController.cs:26-33`), so that grant is inert; see TC-RBAC-API-040 and GAP-RBAC-003. |

---

#### TC-RBAC-API-011 — Department Head with `feedback.void` closes a feedback entry  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — DT-1 row 3, on the one `void` key the seed grants a Department Head |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `feedback.void` (gate `FeedbackController.cs:43-44`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `dh@demo-lab.local` as above; `feedback.void` **is** in the seeded Department Head set (`SystemRoleCatalog.cs:126`) — the one deliberate exception among that role's Quality-group grants. One `qams.feedback_entry` row in `status='Reviewed'`. |
| **Test Data** | Feedback id `44444444-4444-4444-8444-000000000002`; body `{"actionSummary":"Compliment circulated to the department; no corrective action required."}` |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/feedback/44444444-4444-4444-8444-000000000002/close` with the body above. 3. Capture status. 4. `SELECT status FROM qams.feedback_entry WHERE id='44444444-4444-4444-8444-000000000002';` |
| **Expected UI** | Close control rendered for this role and hidden for the Analyst (TC-RBAC-API-010's role). |
| **Expected API** | `204 No Content` (`FeedbackController.cs:48`). |
| **Expected DB** | `status` moves `'Reviewed'` → `'Closed'`. |
| **Expected Audit** | One `audit.audit_trail` row for the feedback-closed domain event. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `UPDATE qams.feedback_entry SET status='Reviewed' WHERE id='44444444-4444-4444-8444-000000000002';`. |
| **Evidence** | HTTP response capture · SQL before/after · `audit_trail` row |
| **Result / Defect** | Not Run · — |
| **Notes** | Contrast with TC-RBAC-API-008: the same role is refused `complaints.void` but allowed `feedback.void`. Both are seeded facts, not a rule — a laboratory may re-privilege either. |

---

#### TC-RBAC-API-012 — Quality Manager with `audits.create` schedules an internal audit  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — ISO 17025 §8.8 internal-audit programme, planning step |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `audits.create` (gate `AuditsController.cs:29-30`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qm@demo-lab.local` on the seeded `Quality Manager` role (holds all `audits.*`). At least one active user id available for `leadAuditorId`. |
| **Test Data** | Body `{"title":"Internal audit 2026-Q3 — Document Control","type":"Internal","leadAuditorId":"22222222-2222-4222-8222-000000000002","plannedDate":"2026-09-15","checklist":[{"isoClause":"8.3","question":"Are controlled copies registered and recalled on retirement?"}],"branchId":null,"departmentId":null}` |
| **Steps** | 1. Log in as `qm@demo-lab.local`. 2. `POST /api/audits` with the body above. 3. Capture status, `Location` header and the `{ id }` body. 4. `SELECT status FROM qams.audit WHERE id = <returned id>;` |
| **Expected UI** | "Schedule audit" action available on the audits register. |
| **Expected API** | `201 Created` with `Location: /api/audits/{id}` and body `{"id":"<guid>"}` — `CreatedAtAction(nameof(GetById), …)` (`AuditsController.cs:40`). |
| **Expected DB** | One new `qams.audit` row, `status = 'Scheduled'` (`AuditStatus = Scheduled, InProgress, SignedOff` — `Audit.cs:6`), one `qams.audit_checklist_item` child row. |
| **Expected Audit** | One `audit.audit_trail` row for the audit-scheduled domain event, tenant-stamped. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `DELETE FROM qams.audit_checklist_item WHERE audit_id=<id>; DELETE FROM qams.audit WHERE id=<id>;` under `app.bypass_rls='on'`. The `audit_trail` row is append-only and retained. |
| **Evidence** | HTTP response capture incl. `Location` · SQL result · `audit_trail` row |
| **Result / Defect** | Not Run · — |
| **Notes** | `201` here, not `200` — the controller uses `CreatedAtAction`; assert the `Location` header, not only the status. |

---

#### TC-RBAC-API-013 — Adjacent privilege: Department Head holds `audits.edit` but not `audits.create`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-003 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Pairwise — (held = `view`,`edit`,`export`) × (required = `create`) |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · holds `audits.view/edit/export` (`SystemRoleCatalog.cs:127`); required `audits.create` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `dh@demo-lab.local` as above; confirm `audits.create`, `audits.approve`, `audits.void` and `audits.sign` are all absent from the seeded Department Head grants. |
| **Test Data** | Same body as TC-RBAC-API-012, with `"title":"Attempted by an edit-only role"` |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/audits` with that body. 3. Capture status + body. 4. `SELECT count(*) FROM qams.audit WHERE title='Attempted by an edit-only role';` |
| **Expected UI** | "Schedule audit" action hidden; the register itself still lists audits (`GET /api/audits` carries no `[RequirePermission]`, `AuditsController.cs:18-23`). |
| **Expected API** | **PROBLEM-403**, `code = "AUTHZ-403"`. Note there is **no** `400` for the body: the authorization filter runs before model validation, so a malformed body would still yield 403. |
| **Expected DB** | `count(*) = 0` — no `qams.audit` row created. |
| **Expected Audit** | No ledger row; no `audit.security_event` row. |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | None. |
| **Evidence** | HTTP response capture · SQL count = 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | Also worth capturing in the same session: the same actor **can** `GET /api/audits` and `GET /api/audits/{id}`, because neither read is gated — an asymmetry that belongs to GAP-RBAC-003, not to this case's verdict. |

---

#### TC-RBAC-API-014 — Quality Manager with `audits.sign` signs off an audit  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · State Transition — `InProgress` → `SignedOff` through the `audits.sign` gate |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `audits.sign` (gate `AuditsController.cs:69-70`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qm@demo-lab.local` as above. One `qams.audit` row in `status='InProgress'` with every `qams.audit_checklist_item.verdict` answered (not `'Unanswered'`) and no NC-graded finding left unlinked — otherwise the aggregate raises `AUD-017`/`AUD-018` instead of completing (per front matter §8, GAP-RBAC-017). The audit must have been **created by a different user** than `qm@demo-lab.local` is not required — see Notes. |
| **Test Data** | Audit id `55555555-5555-4555-8555-000000000001`; no request body |
| **Steps** | 1. Log in as `qm@demo-lab.local`. 2. `POST /api/audits/55555555-5555-4555-8555-000000000001/sign-off`. 3. Capture status. 4. `SELECT status, signed_off_by, signed_off_at_utc FROM qams.audit WHERE id='55555555-5555-4555-8555-000000000001';` |
| **Expected UI** | Sign-off control rendered. |
| **Expected API** | `204 No Content` (`AuditsController.cs:74`). |
| **Expected DB** | `status` `'InProgress'` → `'SignedOff'`; `signed_off_by` = the QM's `user_account.id`; `signed_off_at_utc` non-null (`Audit.cs:72-74`). |
| **Expected Audit** | One `audit.audit_trail` row for the audit-signed-off domain event. **No `audit.electronic_signature` row** — audit sign-off is not a password+PIN ceremony (contrast document publish, TC-RBAC-API-028). |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `UPDATE qams.audit SET status='InProgress', signed_off_by=NULL, signed_off_at_utc=NULL WHERE id='55555555-5555-4555-8555-000000000001';` under `app.bypass_rls='on'`. |
| **Evidence** | HTTP response capture · SQL before/after · `audit_trail` row · `electronic_signature` count delta = 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | **`audits` is the one signed-record family with no segregation-of-duties guard** — `Audit.SignOff` never calls `EnsureSignerIsNotPreparer` (front matter GAP-RBAC-017). This case therefore passes even if the signer is the preparer. Do not read it as evidence that self-sign-off is *intended*; batch E owns the SoD verdict. |

---

#### TC-RBAC-API-015 — Adjacent privilege: `audits.approve` holder cannot sign off  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-003 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Pairwise — (held = `approve`, an **ungated** key) × (required = `sign`) |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Bespoke role `RBAC-A-AUD-APPROVE` · holds exactly `audits.view`, `audits.approve`; required `audits.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `POST /api/roles` as Tenant Administrator with `{"name":"RBAC-A-AUD-APPROVE","permissions":["audits.view","audits.approve"]}`; both are valid keys (`audits` uses `SignedRecordLifecycle`, `PermissionCatalog.cs:139`). Account `aud-approve@demo-lab.local` assigned to it. Audit in `status='InProgress'` with all checklist items answered. |
| **Test Data** | Audit id `55555555-5555-4555-8555-000000000001`; no body |
| **Steps** | 1. Log in as `aud-approve@demo-lab.local`. 2. `GET /api/auth/me/privileges` — confirm the set is exactly `["audits.view","audits.approve"]`. 3. `POST /api/audits/55555555-5555-4555-8555-000000000001/sign-off`. 4. Capture status + body. 5. `SELECT status, signed_off_by FROM qams.audit WHERE id='55555555-5555-4555-8555-000000000001';` |
| **Expected UI** | Sign-off control hidden. |
| **Expected API** | **PROBLEM-403**, `code = "AUTHZ-403"`. |
| **Expected DB** | `status` still `'InProgress'`; `signed_off_by` still `NULL`. |
| **Expected Audit** | No ledger row; no `audit.security_event` row. |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | Reassign the account to `Analyst`, then `POST /api/roles/{roleId}/deactivate` (roles cannot be deleted — GAP-RBAC-015). |
| **Evidence** | HTTP response capture · `/auth/me/privileges` capture proving the held set · SQL unchanged row |
| **Result / Defect** | Not Run · — |
| **Notes** | `audits.approve` gates **no endpoint anywhere** (marked `*` in front matter §4.1). Step 2's capture is what makes the case meaningful: the role visibly holds a privilege that buys nothing. Feeds GAP-RBAC-003. |

---

#### TC-RBAC-API-016 — Department Head with `objectives.create` defines a quality objective  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — granted-key partition on `objectives.create` |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `objectives.create` (gate `QualityObjectivesController.cs:25-26`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `dh@demo-lab.local` on the seeded `Department Head` role, which holds `objectives.view/create/edit/export` (`SystemRoleCatalog.cs:128`). |
| **Test Data** | Body `{"title":"Reduce Q3 turnaround breaches","description":"Chemistry department","metric":"Breaches per 1000 reports","unit":"count","targetValue":5,"direction":"Down","ownerId":"22222222-2222-4222-8222-000000000002","periodStart":"2026-07-01","periodEnd":"2026-09-30","branchId":null,"departmentId":null}` |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/quality-objectives` with the body above. 3. Capture status, `Location` and `{ id }`. 4. `SELECT status, target_value FROM qams.quality_objective WHERE id=<returned id>;` |
| **Expected UI** | "Define objective" action available. |
| **Expected API** | `201 Created` with `Location: /api/quality-objectives/{id}` and `{"id":"<guid>"}` (`QualityObjectivesController.cs:33`). |
| **Expected DB** | One `qams.quality_objective` row, `status = 'Active'` (`ObjectiveStatus = Active, Achieved, Missed, Cancelled` — `QualityObjective.cs:9`), `target_value = 5`. |
| **Expected Audit** | One `audit.audit_trail` row for the objective-defined domain event. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `DELETE FROM qams.quality_objective WHERE id=<id>;` under `app.bypass_rls='on'`. |
| **Evidence** | HTTP response capture · SQL result · `audit_trail` row |
| **Result / Defect** | Not Run · — |
| **Notes** | `targetValue = 5` is a real numeric assertion, not a placeholder; it is read back from `target_value` to prove the handler ran rather than the gate merely passing. |

---

#### TC-RBAC-API-017 — Analyst holds `objectives.edit` but not `objectives.create`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-003 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Pairwise — (held = `view`,`edit`,`export`) × (required = `create`), seeded role |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · holds `objectives.view/edit/export` (`SystemRoleCatalog.cs:158`); required `objectives.create` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `analyst@demo-lab.local` on the seeded `Analyst` role. Note the asymmetry to assert in the query first: the Analyst holds `objectives.edit` but **not** `objectives.create`, the reverse of most modules. |
| **Test Data** | Same body as TC-RBAC-API-016, with `"title":"Attempted by an edit-only role"` |
| **Steps** | 1. Log in as `analyst@demo-lab.local`. 2. `POST /api/quality-objectives`. 3. Capture status + body. 4. `SELECT count(*) FROM qams.quality_objective WHERE title='Attempted by an edit-only role';` |
| **Expected UI** | "Define objective" action hidden; progress recording still offered (`POST /api/quality-objectives/{id}/progress` carries **no** gate, `QualityObjectivesController.cs:36-42`). |
| **Expected API** | **PROBLEM-403**, `code = "AUTHZ-403"`. |
| **Expected DB** | `count(*) = 0`. |
| **Expected Audit** | No ledger row; no `audit.security_event` row. |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | None. |
| **Evidence** | HTTP response capture · SQL count = 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | The Analyst's `objectives.edit` grant reaches nothing on this controller — the only edit-shaped route, `/progress`, is ungated. Another instance for GAP-RBAC-003. |

---

#### TC-RBAC-API-018 — Adjacent privilege: Department Head cannot close an objective (`objectives.void`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-003 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Pairwise — (held = `create`,`edit`) × (required = `void`) |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · required `objectives.void` (gate `QualityObjectivesController.cs:44-45`); not held · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `dh@demo-lab.local` as above. One `qams.quality_objective` row in `status='Active'`. |
| **Test Data** | Objective id `66666666-6666-4666-8666-000000000001`; body `{"outcome":"Achieved","note":"Breaches fell to 3 per 1000."}` |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/quality-objectives/66666666-6666-4666-8666-000000000001/close` with the body above. 3. Capture status + body. 4. `SELECT status FROM qams.quality_objective WHERE id='66666666-6666-4666-8666-000000000001';` |
| **Expected UI** | Close control hidden. |
| **Expected API** | **PROBLEM-403**, `code = "AUTHZ-403"`. |
| **Expected DB** | `status` still `'Active'`. |
| **Expected Audit** | No ledger row; no `audit.security_event` row. |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | None. |
| **Evidence** | HTTP response capture · SQL unchanged row |
| **Result / Defect** | Not Run · — |
| **Notes** | The same actor may *define* an objective (TC-RBAC-API-016) but not retire one — create and void are separate keys, deliberately. |

---

#### TC-RBAC-API-019 — Quality Manager with `changes.approve` approves a change request  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — ISO 9001 §6.3 controlled-change approval |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `changes.approve` (gate `GovernanceControllers.cs:97-98`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qm@demo-lab.local` as above. One `qams.change_request` row in `status='Proposed'` **with a linked risk item** — the aggregate's load-bearing invariant is that a change cannot be approved without one (`ChangeAndReview.cs:8-10`); link it first via `POST /api/changes/{id}/risk` (ungated, `GovernanceControllers.cs:90`). |
| **Test Data** | Change id `77777777-7777-4777-8777-000000000001`; no request body |
| **Steps** | 1. Log in as `qm@demo-lab.local`. 2. `POST /api/changes/77777777-7777-4777-8777-000000000001/approve`. 3. Capture status. 4. `SELECT status, approved_by FROM qams.change_request WHERE id='77777777-7777-4777-8777-000000000001';` |
| **Expected UI** | Approve control rendered on the change detail screen. |
| **Expected API** | `204 No Content` (`GovernanceControllers.cs:102`). |
| **Expected DB** | `status` `'Proposed'` → `'Approved'` (`ChangeStatus = Proposed, Approved, Rejected, Closed, Reviewed` — `ChangeAndReview.cs:6`); `approved_by` = the QM's `user_account.id` (`ChangeAndReview.cs:31`). |
| **Expected Audit** | One `audit.audit_trail` row for the change-approved domain event. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `UPDATE qams.change_request SET status='Proposed', approved_by=NULL WHERE id='77777777-7777-4777-8777-000000000001';`. |
| **Evidence** | HTTP response capture · SQL before/after · `audit_trail` row |
| **Result / Defect** | Not Run · — |
| **Notes** | Route is **`/api/changes`**, not `/api/change-requests` (`GovernanceControllers.cs:67`; conventions §2 line 113). |

---

#### TC-RBAC-API-020 — Analyst without `changes.approve` is refused the approval  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-001 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — key-absent partition, seeded role |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · holds `changes.view/create/edit/export` (`SystemRoleCatalog.cs:159`); required `changes.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `analyst@demo-lab.local` on the seeded `Analyst` role; confirm `changes.approve`, `changes.void` and `changes.sign` are absent. Change request in `status='Proposed'` with a linked risk item, so a 403 cannot be confused with the missing-risk refusal. |
| **Test Data** | Change id `77777777-7777-4777-8777-000000000001`; no body |
| **Steps** | 1. Log in as `analyst@demo-lab.local`. 2. `POST /api/changes/77777777-7777-4777-8777-000000000001/approve`. 3. Capture status, `Content-Type`, body. 4. `SELECT status, approved_by FROM qams.change_request WHERE id='77777777-7777-4777-8777-000000000001';` |
| **Expected UI** | Approve control hidden; Propose still available (`POST /api/changes` is ungated, `GovernanceControllers.cs:82`). |
| **Expected API** | **PROBLEM-403**, `code = "AUTHZ-403"`. |
| **Expected DB** | `status` still `'Proposed'`; `approved_by` still `NULL`. |
| **Expected Audit** | No ledger row; no `audit.security_event` row. |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | None. |
| **Evidence** | HTTP response capture · SQL unchanged row |
| **Result / Defect** | Not Run · — |
| **Notes** | The post-implementation review route `POST /api/changes/{id}/review` shares `changes.approve` (`GovernanceControllers.cs:121-122`); it is refused for the same actor for the same reason, verified by inspection rather than a separate case. |

---

#### TC-RBAC-API-021 — Adjacent privilege: `changes.approve` holder cannot reject (`changes.void`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-003 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Multiple-Condition Coverage — the two `changes` gates evaluated for one actor: (`approve` held, `void` absent) |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Bespoke role `RBAC-A-CHG-APPROVE` · holds exactly `changes.view`, `changes.approve`; required `changes.void` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `POST /api/roles` with `{"name":"RBAC-A-CHG-APPROVE","permissions":["changes.view","changes.approve"]}`; account `chg-approve@demo-lab.local` assigned. Two change requests in `status='Proposed'`, both with a linked risk item: `…0002` for the allowed leg, `…0003` for the refused leg. |
| **Test Data** | Change ids `77777777-7777-4777-8777-000000000002` and `…0003`; reject body `{"reason":"Superseded by CR-2026-0011."}` |
| **Steps** | 1. Log in as `chg-approve@demo-lab.local`. 2. `POST /api/changes/77777777-7777-4777-8777-000000000002/approve` — record status. 3. `POST /api/changes/77777777-7777-4777-8777-000000000003/reject` with the body above — record status, `Content-Type` and body. 4. `SELECT id, status FROM qams.change_request WHERE id IN ('77777777-7777-4777-8777-000000000002','77777777-7777-4777-8777-000000000003');` |
| **Expected UI** | Approve rendered, Reject hidden, on the same screen for the same actor. |
| **Expected API** | Step 2: `204 No Content`. Step 3: **PROBLEM-403**, `code = "AUTHZ-403"`. |
| **Expected DB** | `…0002.status = 'Approved'`; `…0003.status` still `'Proposed'`. |
| **Expected Audit** | Exactly **one** new `audit.audit_trail` row (for step 2's approval); none for step 3. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `UPDATE qams.change_request SET status='Proposed', approved_by=NULL WHERE id='77777777-7777-4777-8777-000000000002';` then reassign the account and deactivate the role. |
| **Evidence** | Two HTTP response captures · SQL showing both rows · `audit_trail` delta = 1 |
| **Result / Defect** | Not Run · — |
| **Notes** | Both legs in one session on one token — the strongest evidence that the two filters are independent, since nothing between them changed except the route. |

---

#### TC-RBAC-API-022 — Quality Manager with `reviews.create` schedules a management review  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — ISO 17025 §8.9 management review, scheduling step |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `reviews.create` (gate `GovernanceControllers.cs:145-146`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qm@demo-lab.local` on the seeded `Quality Manager` role (all `reviews.*` keys). |
| **Test Data** | Body `{"title":"Management review 2026-H2","reviewDate":"2026-10-06","participants":["Quality Manager","Technical Manager","Laboratory Director"],"branchId":null,"departmentId":null}` |
| **Steps** | 1. Log in as `qm@demo-lab.local`. 2. `POST /api/management-reviews` with the body above. 3. Capture status, `Location`, `{ id }`. 4. `SELECT status FROM qams.management_review WHERE id=<returned id>;` |
| **Expected UI** | "Schedule review" action available. |
| **Expected API** | `201 Created`, `Location: /api/management-reviews/{id}`, body `{"id":"<guid>"}` (`GovernanceControllers.cs:152`). |
| **Expected DB** | One `qams.management_review` row, `status = 'Scheduled'` (`ReviewStatus = Scheduled, Closed` — `ChangeAndReview.cs:136`). |
| **Expected Audit** | One `audit.audit_trail` row for the review-scheduled domain event. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `DELETE FROM qams.management_review WHERE id=<id>;` under `app.bypass_rls='on'`. |
| **Evidence** | HTTP response capture · SQL result · `audit_trail` row |
| **Result / Defect** | Not Run · — |
| **Notes** | The sibling gates `reviews.edit` on `/{id}/decisions` (`:155-156`) and `reviews.void` on `/{id}/close` (`:161-162`) are covered by inspection; only `reviews.create` and `reviews.export` are cased here. |

---

#### TC-RBAC-API-023 — Department Head holds `reviews.view`+`.export` but not `reviews.create`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-003 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — read-only partition of the `reviews` module, seeded role |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · holds `reviews.view`, `reviews.export` only (`SystemRoleCatalog.cs:130`); required `reviews.create` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `dh@demo-lab.local` as above; confirm the role's `reviews.*` grants are exactly two rows. |
| **Test Data** | Same body as TC-RBAC-API-022 with `"title":"Attempted by a read-only reviews role"` |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/management-reviews`. 3. Capture status + body. 4. `SELECT count(*) FROM qams.management_review WHERE title='Attempted by a read-only reviews role';` |
| **Expected UI** | "Schedule review" hidden; the review register and the export button remain available. |
| **Expected API** | **PROBLEM-403**, `code = "AUTHZ-403"`. |
| **Expected DB** | `count(*) = 0`. |
| **Expected Audit** | No ledger row; no `audit.security_event` row. |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | None. |
| **Evidence** | HTTP response capture · SQL count = 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | Pairs directly with TC-RBAC-API-024, where the same actor's `reviews.export` **is** honoured — the two-key read-only partition is not uniformly inert. |

---

#### TC-RBAC-API-024 — Department Head with `reviews.export` downloads the review pack  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — the one `*.export` key in this slice that reaches an HTTP gate |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `reviews.export` (gate `ExportsController.cs:125-126`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; QuestPDF `2026.7.1` renders the pack |
| **Preconditions** | `dh@demo-lab.local` as above. One `qams.management_review` row exists whose `review_ref` the export will name. |
| **Test Data** | Review id `88888888-8888-4888-8888-000000000001` |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `GET /api/exports/review-pack/88888888-8888-4888-8888-000000000001.pdf`. 3. Capture status, `Content-Type`, `Content-Disposition` and byte length. 4. After `SELECT set_config('app.bypass_rls','on',false);` run `SELECT event_type, detail FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 1;` |
| **Expected UI** | Export action offered on the management-review screen. |
| **Expected API** | `200 OK`, `Content-Type: application/pdf`, filename `review-pack-<reviewRef>-<yyyyMMdd>.pdf` (`ExportsController.cs:156-157`), non-empty body. |
| **Expected DB** | No `qams.*` mutation — the export is a read. |
| **Expected Audit** | One `audit.security_event` row, `event_type = 'RECORD_EXPORTED'`, detail `review-pack/<reviewRef>.pdf` (`ExportsController.cs:155`, `:168-169`). |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | None — `audit.security_event` is append-only. Delete the downloaded file from the runner. |
| **Evidence** | HTTP response capture incl. headers · saved PDF · `security_event` row |
| **Result / Defect** | Not Run · — |
| **Notes** | Contrast `GET /api/exports/nonconformances.xlsx` (`ExportsController.cs:30-31`), which carries **no** `[RequirePermission]` at all — so `nc.export` is inert while `reviews.export` is enforced. That inconsistency is GAP-RBAC-003; do not generalise this case's result to other exports. |

---

#### TC-RBAC-API-025 — External Auditor holds `reviews.view` only and is refused the review-pack export  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-003 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — the seed's deliberate carve-out `ManagementReviews => action is View` (`SystemRoleCatalog.cs:190`) |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | External Auditor · holds `reviews.view` only; required `reviews.export` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `auditor@demo-lab.local` active on the seeded `External Auditor` role. Confirm by query that the role's only `reviews.*` grant is `reviews.view` — the seed reduces this module to view because review packs were QM/admin-only under the retired tiers (`SystemRoleCatalog.cs:189-190`). |
| **Test Data** | Review id `88888888-8888-4888-8888-000000000001` |
| **Steps** | 1. Log in as `auditor@demo-lab.local`. 2. `GET /api/management-reviews/88888888-8888-4888-8888-000000000001` — record status (expected allow; the read is ungated). 3. `GET /api/exports/review-pack/88888888-8888-4888-8888-000000000001.pdf` — record status, `Content-Type`, body. 4. Count `audit.security_event` rows with `event_type='RECORD_EXPORTED'` before and after step 3. |
| **Expected UI** | The auditor sees the review record; the export control is hidden. |
| **Expected API** | Step 2: `200 OK`, `application/json`. Step 3: **PROBLEM-403**, `code = "AUTHZ-403"` — and crucially **no PDF bytes**. |
| **Expected DB** | No mutation. |
| **Expected Audit** | `RECORD_EXPORTED` count delta = **0** — `LogExportAsync` is inside the action, which never runs (`ExportsController.cs:155`). |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | None. |
| **Evidence** | Two HTTP response captures · `security_event` count delta = 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | The zero-delta assertion matters for Part 11 §11.10(b): a refused export must not appear in the export log as if it had happened. |

---

#### TC-RBAC-API-026 — Department Head with `documents.approve` recommends a document version  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · State Transition — `UnderReview` → `Approved` through the `documents.approve` gate |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `documents.approve` (gate `DocumentsController.cs:98-99`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `dh@demo-lab.local` on the seeded `Department Head` role, which holds `documents.view/create/edit/approve/export` (`SystemRoleCatalog.cs:131`) and **not** `documents.sign` or `documents.void`. One `qams.controlled_document` with an in-flight `qams.document_version` in `state='UnderReview'` whose `author_id` is **not** the Department Head — otherwise `SOD-DOC-001` (`ControlledDocument.cs:122`) confounds the RBAC result. |
| **Test Data** | Document id `99999999-9999-4999-8999-000000000001`; no request body |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/documents/99999999-9999-4999-8999-000000000001/recommend`. 3. Capture status. 4. `SELECT state, author_id FROM qams.document_version WHERE document_id='99999999-9999-4999-8999-000000000001' ORDER BY created_at_utc DESC LIMIT 1;` |
| **Expected UI** | Recommend control rendered on the document detail screen. |
| **Expected API** | `204 No Content` (`DocumentsController.cs:103`). |
| **Expected DB** | `qams.document_version.state` moves `'UnderReview'` → `'Approved'` (`VersionState = Draft, UnderReview, Approved, Published, Obsolete, Rejected` — `ControlledDocument.cs:8`). |
| **Expected Audit** | One `audit.audit_trail` row for the document-recommended domain event; one or more `audit.field_change` rows for the `state` transition. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `UPDATE qams.document_version SET state='UnderReview' WHERE document_id='99999999-9999-4999-8999-000000000001' AND state='Approved';` under `app.bypass_rls='on'`. |
| **Evidence** | HTTP response capture · SQL before/after · `audit_trail` + `field_change` rows |
| **Result / Defect** | Not Run · — |
| **Notes** | The brief's `DOC.REVIEW` maps here — the *review* step is gated by `documents.approve`, not by a distinct review key (front matter §4.4). Do not name `DOC.REVIEW` in any assertion; it does not exist (GAP-RBAC-001). |

---

#### TC-RBAC-API-027 — Analyst without `documents.approve` is refused the recommendation  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-001 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — key-absent partition, seeded role |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · holds `documents.view/create/edit/export` (`SystemRoleCatalog.cs:161`); required `documents.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `analyst@demo-lab.local` on the seeded `Analyst` role; confirm `documents.approve`, `documents.sign`, `documents.void` are all absent. In-flight version in `state='UnderReview'`, authored by someone else. |
| **Test Data** | Document id `99999999-9999-4999-8999-000000000001`; no body |
| **Steps** | 1. Log in as `analyst@demo-lab.local`. 2. `POST /api/documents/99999999-9999-4999-8999-000000000001/recommend`. 3. Capture status, `Content-Type`, body. 4. `SELECT state FROM qams.document_version WHERE document_id='99999999-9999-4999-8999-000000000001' ORDER BY created_at_utc DESC LIMIT 1;` 5. `POST /api/documents/99999999-9999-4999-8999-000000000001/reject` with `{"reason":"probe"}` — same key, expect the same refusal. |
| **Expected UI** | Recommend and Reject both hidden; Submit (ungated, `DocumentsController.cs:91`) still offered. |
| **Expected API** | Steps 2 and 5: **PROBLEM-403**, `code = "AUTHZ-403"` on both — the two routes share `documents.approve` (`DocumentsController.cs:99` and `:107`). |
| **Expected DB** | `state` still `'UnderReview'`; no `rejection_reason` written. |
| **Expected Audit** | No ledger row and no `audit.field_change` row for either attempt; no `audit.security_event` row. |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | None. |
| **Evidence** | Two HTTP response captures · SQL unchanged row · `field_change` delta = 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | Both the recommend and the reject leg are asserted because the brief's `DOC.APPROVE` conflates them; the real build gates both on the one key. |

---

#### TC-RBAC-API-028 — Quality Manager with `documents.sign` publishes a document  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 (gate) with URS-020…023 (signature) as context · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Path Coverage — HTTP gate → command policy → SoD pre-check → signature ceremony → state change, the full happy path of `PublishDocumentHandler` |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `documents.sign` **twice**: HTTP gate `DocumentsController.cs:114-115` and command policy `[RequirePermissionPolicy(Documents, Sign)]` on `PublishDocumentCommand` (`DocumentCommands.cs:65-67`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; the route is additionally under the `ESignaturePolicy` rate-limit partition (`DocumentsController.cs:118`) — run this case **before** any credential-burst probe |
| **Preconditions** | `qm@demo-lab.local` active on `Quality Manager`, with a signature PIN set (`POST /api/auth/pin`, `SetPinCommand`) and a known password. In-flight `qams.document_version` in `state='Approved'` — the handler rejects any other state with `DOC-014` before minting a signature (`DocumentCommands.cs:134-138`). `author_id` of that version **must not** be the QM, or `SOD-DOC-002` fires at `DocumentCommands.cs:143-147`. |
| **Test Data** | Document id `99999999-9999-4999-8999-000000000002`; body `{"password":"Demo-Admin-Pass-2!","pin":"<the QM's configured PIN>"}` |
| **Steps** | 1. Log in as `qm@demo-lab.local`. 2. `POST /api/documents/99999999-9999-4999-8999-000000000002/publish` with the body above. 3. Capture status. 4. `SELECT status FROM qams.controlled_document WHERE id='99999999-9999-4999-8999-000000000002';` and `SELECT state, approved_by FROM qams.document_version WHERE document_id='99999999-9999-4999-8999-000000000002' ORDER BY created_at_utc DESC LIMIT 1;` 5. `SELECT meaning, subject_ref, content_hash FROM audit.electronic_signature ORDER BY signed_at_utc DESC LIMIT 1;` under `app.bypass_rls='on'`. |
| **Expected UI** | Publish control rendered; the e-signature dialog collects password + PIN. |
| **Expected API** | `204 No Content` (`DocumentsController.cs:122`). |
| **Expected DB** | `qams.controlled_document.status` → `'Published'`; the version's `state` → `'Published'`, `approved_by` = the QM's `user_account.id`. |
| **Expected Audit** | One `audit.electronic_signature` row with `meaning = 'Approved and published <code> v<versionLabel>'`, `subject_ref = 'DOC:<documentId as N>'` and `content_hash` equal to the `sha256` of the approved `qams.file_reference` row (`DocumentCommands.cs:151-160`); plus the `audit.audit_trail` rows for the publish events. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | Restore the document and version states under `app.bypass_rls='on'`. The `audit.electronic_signature` row is append-only and **must not** be deleted; record its id in the execution evidence instead. |
| **Evidence** | HTTP response capture · SQL for both tables · `electronic_signature` row incl. `content_hash` |
| **Result / Defect** | Not Run · — |
| **Notes** | `documents.sign` is enforced at **two** layers with the same key; a passing case proves neither layer alone. Batch D owns the layer-isolation case. |

---

#### TC-RBAC-API-029 — Adjacent privilege: `documents.approve` holder cannot publish, and no signature attempt is burned  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-003, RSK-RBAC-005 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Branch Coverage — the false branch of `privileges.Has("documents.sign")` (`RequirePermissionAttribute.cs:49`) reached by an actor who *does* hold `documents.approve` |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · holds `documents.approve` and not `documents.sign` (`SystemRoleCatalog.cs:131`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; `ESignaturePolicy` rate-limit partition applies to the route |
| **Preconditions** | `dh@demo-lab.local` with a signature PIN set and a valid password (so the refusal cannot be attributed to a missing PIN). In-flight version in `state='Approved'`, authored by a third user. Record `SELECT count(*) FROM audit.electronic_signature;` and `SELECT count(*) FROM audit.security_event WHERE event_type='ESIGN_FAILED';` as `t0` baselines. |
| **Test Data** | Document id `99999999-9999-4999-8999-000000000002`; body `{"password":"<the DH's correct password>","pin":"<the DH's correct PIN>"}` — deliberately **valid** credentials |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/documents/99999999-9999-4999-8999-000000000002/publish` with correct credentials. 3. Capture status, `Content-Type`, body. 4. Re-count `audit.electronic_signature` and `audit.security_event` rows with `event_type='ESIGN_FAILED'`. 5. `SELECT status FROM qams.controlled_document WHERE id='99999999-9999-4999-8999-000000000002';` |
| **Expected UI** | Publish control hidden for this role; the e-signature dialog is never opened. |
| **Expected API** | **PROBLEM-403**, `code = "AUTHZ-403"` — refused by the HTTP filter, before the command policy, before `ESignatureService`. |
| **Expected DB** | `qams.controlled_document.status` unchanged (`'Draft'` or `'Published'` as seeded, not advanced by this request); no `qams.document_version` state change. |
| **Expected Audit** | `audit.electronic_signature` count delta = **0**; `audit.security_event` `ESIGN_FAILED` delta = **0**; no `audit.audit_trail` row. The credentials were correct and were never evaluated. |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | None. |
| **Evidence** | HTTP response capture · both count deltas = 0 · SQL unchanged document row |
| **Result / Defect** | Not Run · — |
| **Notes** | The two zero-deltas are the point: an authorization refusal must not consume one of the five signing attempts that `UserAccount.RegisterFailedLogin` counts toward the 30-minute lockout (conventions §2, e-signature section). A build that evaluated credentials first would show a non-zero `ESIGN_FAILED` delta here. |

---

#### TC-RBAC-API-030 — External Auditor with `documents.view` reads the acknowledgement coverage  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — the read partition; one of only six gated `*.view` keys in the whole catalogue |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | External Auditor · `documents.view` (gate `DocumentsController.cs:60-61`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `auditor@demo-lab.local` on the seeded `External Auditor` role, which grants `view`+`export` on non-administration modules (`SystemRoleCatalog.cs:184-193`) — so `documents.view` is held and every `documents` write key is not. One published `qams.controlled_document` with at least one `qams.document_acknowledgement` row. |
| **Test Data** | Document id `99999999-9999-4999-8999-000000000003` |
| **Steps** | 1. Log in as `auditor@demo-lab.local`. 2. `GET /api/documents/99999999-9999-4999-8999-000000000003/acknowledgements`. 3. Capture status, `Content-Type` and the item count. 4. `SELECT count(*) FROM qams.document_acknowledgement WHERE document_id='99999999-9999-4999-8999-000000000003';` and compare. |
| **Expected UI** | Read-and-understand coverage panel rendered for the auditor. |
| **Expected API** | `200 OK`, `application/json`, item count equal to the SQL count. |
| **Expected DB** | No mutation. |
| **Expected Audit** | No `audit.audit_trail` row — a read raises no domain event. No `audit.security_event` row: unlike `ExportsController`, `DocumentsController` logs no read event. |
| **Expected Notification** | n/a — reads raise none. |
| **Cleanup** | None. |
| **Evidence** | HTTP response capture · SQL count matching the payload length |
| **Result / Defect** | Not Run · — |
| **Notes** | **`documents.view` gates exactly one route on this controller.** `GET /api/documents`, `GET /api/documents/{id}`, `GET /{id}/signatures` and `GET /{id}/controlled-copies` carry no `[RequirePermission]` at all (`DocumentsController.cs:24,31,45,66`), so any authenticated tenant user reads the document register regardless of `documents.view`. New finding — GAP-RBAC-903. |

---

#### TC-RBAC-API-031 — Adjacent privilege: a publish-capable role still cannot retire (`documents.void`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-003 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Multiple-Condition Coverage — (`approve` held ∧ `sign` held ∧ `void` absent) against the `documents.void` gate |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Bespoke role `RBAC-A-DOC-SIGNER` · holds exactly `documents.view`, `documents.approve`, `documents.sign`; required `documents.void` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `POST /api/roles` with `{"name":"RBAC-A-DOC-SIGNER","permissions":["documents.view","documents.approve","documents.sign"]}` — all three are valid keys (`documents` uses `SignedRecordLifecycle`, `PermissionCatalog.cs:145`). Account `doc-signer@demo-lab.local` assigned. One `qams.controlled_document` in `status='Published'`. |
| **Test Data** | Document id `99999999-9999-4999-8999-000000000003`; no request body |
| **Steps** | 1. Log in as `doc-signer@demo-lab.local`. 2. `GET /api/auth/me/privileges` — confirm the three-key set. 3. `POST /api/documents/99999999-9999-4999-8999-000000000003/retire`. 4. Capture status, `Content-Type`, body. 5. `SELECT status FROM qams.controlled_document WHERE id='99999999-9999-4999-8999-000000000003';` |
| **Expected UI** | Retire control hidden even though Publish and Recommend are offered. |
| **Expected API** | **PROBLEM-403**, `code = "AUTHZ-403"`. Note this is the HTTP gate refusing; the command itself is only `[RequireInternalActor]` (`DocumentCommands.cs:71-72`), so the HTTP gate is the sole capability check on retirement. |
| **Expected DB** | `status` still `'Published'` — not `'Obsolete'` (`DocumentStatus = Draft, Published, Obsolete`, `ControlledDocument.cs:6`). |
| **Expected Audit** | No ledger row; no `audit.field_change` row; no `audit.security_event` row. |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | Reassign the account to `Analyst`, then `POST /api/roles/{roleId}/deactivate`. |
| **Evidence** | HTTP response capture · `/auth/me/privileges` capture · SQL unchanged row |
| **Result / Defect** | Not Run · — |
| **Notes** | The brief's `DOC.OBSOLETE` maps to `documents.void` here, but the code says *retire*, and **no "OBSOLETE - UNCONTROLLED" PDF watermark exists anywhere in `src`** (conventions §2 line 118). Do not assert a watermark. |

---

#### TC-RBAC-API-032 — Quality Manager with `quality-policy.view` reads the policy history  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — granted-key partition on a gated `*.view` |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `quality-policy.view` (gate `QualityPolicyController.cs:31-32`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qm@demo-lab.local` on the seeded `Quality Manager` role, which holds all seven `quality-policy.*` keys (the QM predicate excludes only `users`, `tenant-settings`, `roles` beyond view and `organization.manage` — `SystemRoleCatalog.cs:107-117`). At least two `qams.quality_policy` rows: one `status='Active'`, one `status='Superseded'`. |
| **Test Data** | No parameters |
| **Steps** | 1. Log in as `qm@demo-lab.local`. 2. `GET /api/quality-policy`. 3. Capture status, `Content-Type` and the returned item count. 4. `SELECT count(*) FROM qams.quality_policy;` and compare. |
| **Expected UI** | Policy history list rendered with both versions. |
| **Expected API** | `200 OK`, `application/json`, item count equal to the SQL count (drafts, active and superseded all returned — `QualityPolicyController.cs:30`). |
| **Expected DB** | No mutation. |
| **Expected Audit** | No ledger row; no `audit.security_event` row. |
| **Expected Notification** | n/a — reads raise none. |
| **Cleanup** | None. |
| **Evidence** | HTTP response capture · SQL count matching the payload length |
| **Result / Defect** | Not Run · — |
| **Notes** | Pairs with TC-RBAC-API-033, which drives the same route with a role that lacks the key. |

---

#### TC-RBAC-API-033 — External Auditor is denied the policy history but still reads the active policy  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-001, RSK-RBAC-005 (minted) |
| **Level / Type / Technique** | API · Security (negative + positive pair) · Pairwise — (role = External Auditor) × (route gated vs route deliberately ungated) on one controller |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | External Auditor · **no** `quality-policy.*` key at all — the module is excluded outright from the seed (`SystemRoleCatalog.cs:188`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `auditor@demo-lab.local` active on the seeded `External Auditor` role. Confirm: `SELECT count(*) FROM qams.role_permission rp JOIN qams.role r ON r.id=rp.role_id AND r.tenant_id=rp.tenant_id WHERE r.normalized_name='EXTERNAL AUDITOR' AND rp.permission_key LIKE 'quality-policy.%';` returns `0`. One `qams.quality_policy` row in `status='Active'`. |
| **Test Data** | No parameters |
| **Steps** | 1. Log in as `auditor@demo-lab.local`. 2. `GET /api/quality-policy` — capture status, `Content-Type`, body. 3. `GET /api/quality-policy/active` — capture status and body. 4. Record that no `qams.*` row changed. |
| **Expected UI** | Policy history screen returns "no permission"; the current statement remains readable, as the controller's own doc comment intends (`QualityPolicyController.cs:11-16`: "the current statement is readable by any authenticated user (it must be communicated)"). |
| **Expected API** | Step 2: **PROBLEM-403**, `code = "AUTHZ-403"`. Step 3: `200 OK` with the active policy, **or** `204 No Content` if none has been approved (`QualityPolicyController.cs:27`) — with the seeded Active row present, expect `200`. |
| **Expected DB** | No mutation. |
| **Expected Audit** | No ledger row; no `audit.security_event` row for either leg. |
| **Expected Notification** | n/a — reads and refusals raise none. |
| **Cleanup** | None. |
| **Evidence** | Two HTTP response captures · the role-grant count query returning 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | `GET /api/quality-policy/active` being ungated is **documented and deliberate** (`QualityPolicyController.cs:22-23`), not an instance of GAP-RBAC-003. This case records the distinction so a reviewer does not read the `200` as a gate failure. |

---

#### TC-RBAC-API-034 — Quality Manager with `quality-policy.approve` approves a draft policy  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · State Transition — `Draft` → `Active` through the `quality-policy.approve` gate |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `quality-policy.approve` (gate `QualityPolicyController.cs:49-50`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qm@demo-lab.local` as above. One `qams.quality_policy` row in `status='Draft'` whose `created_by_user_id` is **a different user** — `QualityPolicy.Approve` calls `EnsureSignerIsNotPreparer` and raises `SOD-QP-001` otherwise (`QualityPolicy.cs:78`). If `created_by_user_id` were null the guard is a silent no-op (GAP-RBAC-009), so set it explicitly to a second user id to keep this case an RBAC case only. |
| **Test Data** | Policy id `aaaaaaaa-aaaa-4aaa-8aaa-000000000001`; body `{"effectiveDate":"2026-09-01"}` |
| **Steps** | 1. Log in as `qm@demo-lab.local`. 2. `POST /api/quality-policy/aaaaaaaa-aaaa-4aaa-8aaa-000000000001/approve` with the body above. 3. Capture status. 4. `SELECT status, effective_date, approved_by_id FROM qams.quality_policy WHERE id='aaaaaaaa-aaaa-4aaa-8aaa-000000000001';` |
| **Expected UI** | Approve control rendered on the draft policy. |
| **Expected API** | `204 No Content` (`QualityPolicyController.cs:54`). |
| **Expected DB** | `status` `'Draft'` → `'Active'` (`QualityPolicyStatus = Draft, Active, Superseded` — `QualityPolicy.cs:6`); `effective_date = 2026-09-01`; `approved_by_id` = the QM's `user_account.id` (`QualityPolicy.cs:29-31`). |
| **Expected Audit** | One `audit.audit_trail` row for the policy-approved domain event; `audit.field_change` rows for `status` and `effective_date`. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `UPDATE qams.quality_policy SET status='Draft', effective_date=NULL, approved_by_id=NULL WHERE id='aaaaaaaa-aaaa-4aaa-8aaa-000000000001';` under `app.bypass_rls='on'`. |
| **Evidence** | HTTP response capture · SQL before/after · `audit_trail` + `field_change` rows |
| **Result / Defect** | Not Run · — |
| **Notes** | `SOD-QP-001` returns **422** with the domain code, never 403 (`DomainExceptionHandler.cs:75-80`). The preconditions exist to keep the two apart; batch E owns the SoD case. |

---

#### TC-RBAC-API-035 — Adjacent privilege: draft-and-revise role cannot approve the policy  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-003 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — three gates on one controller evaluated for one actor: `view` allow, `create` allow, `approve` deny |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Bespoke role `RBAC-A-QP-DRAFTER` · holds exactly `quality-policy.view`, `quality-policy.create`, `quality-policy.edit`; required `quality-policy.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `POST /api/roles` with `{"name":"RBAC-A-QP-DRAFTER","permissions":["quality-policy.view","quality-policy.create","quality-policy.edit"]}`. Account `qp-drafter@demo-lab.local` assigned. One `qams.quality_policy` row in `status='Draft'`. |
| **Test Data** | Policy id `aaaaaaaa-aaaa-4aaa-8aaa-000000000001`; draft body `{"statement":"The laboratory is committed to impartial, competent and consistent testing."}`; approve body `{"effectiveDate":"2026-09-01"}` |
| **Steps** | 1. Log in as `qp-drafter@demo-lab.local`. 2. `GET /api/quality-policy` — expect allow. 3. `POST /api/quality-policy` with the draft body — expect allow. 4. `PUT /api/quality-policy/aaaaaaaa-aaaa-4aaa-8aaa-000000000001` with the draft body — expect allow. 5. `POST /api/quality-policy/aaaaaaaa-aaaa-4aaa-8aaa-000000000001/approve` — expect refusal; capture status, `Content-Type`, body. 6. `SELECT status FROM qams.quality_policy WHERE id='aaaaaaaa-aaaa-4aaa-8aaa-000000000001';` |
| **Expected UI** | Draft and Revise controls rendered; Approve hidden. |
| **Expected API** | Step 2 `200`; step 3 `200 {"id":"<guid>"}` (`QualityPolicyController.cs:39`); step 4 `204`; step 5 **PROBLEM-403**, `code = "AUTHZ-403"`. |
| **Expected DB** | The row from step 3 exists in `status='Draft'`; the row from step 5 is still `'Draft'`, `approved_by_id` still `NULL`. |
| **Expected Audit** | Ledger rows for steps 3 and 4 only; none for step 5; no `audit.security_event` row for step 5. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `DELETE FROM qams.quality_policy WHERE id=<the id created in step 3>;` under `app.bypass_rls='on'`; reassign the account and deactivate the role. |
| **Evidence** | Four HTTP response captures across one session · SQL after each mutating step |
| **Result / Defect** | Not Run · — |
| **Notes** | Four gates on one controller in one session, on one token: the cleanest available demonstration that each `[RequirePermission]` instance is an independent filter, not a controller-wide setting. |

---

#### TC-RBAC-API-036 — Quality Manager with `records.void` disposes an archived record  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-002 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · BVA — the retention boundary: `retention_expiry` one month **before** the run date, the first value at which `ARC-014` no longer fires |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `records.void` (gate `OperationsControllers.cs:46-47`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qm@demo-lab.local` as above (QM holds all `records.*`). One `qams.archive_entry` row: `state='Archived'`, `retention_class='FiveYears'`, `archived_on='2021-07-01'` so `retention_expiry='2026-07-01'` (`ArchiveEntry.ExpiryFor`, `ArchiveEntry.cs:158-160`) — i.e. expired relative to the 2026-08-01 run date, clearing `ARC-014` (`ArchiveEntry.cs:146-149`); `is_on_legal_hold = false`, clearing `ARC-015` (`:135-139`). Not `Permanent`, clearing `ARC-013` (`:141-144`). |
| **Test Data** | Archive id `bbbbbbbb-bbbb-4bbb-8bbb-000000000001`; no request body |
| **Steps** | 1. Log in as `qm@demo-lab.local`. 2. `POST /api/archives/bbbbbbbb-bbbb-4bbb-8bbb-000000000001/dispose`. 3. Capture status. 4. `SELECT state, disposal_authorized_by FROM qams.archive_entry WHERE id='bbbbbbbb-bbbb-4bbb-8bbb-000000000001';` |
| **Expected UI** | Dispose control rendered on the archive entry. |
| **Expected API** | `204 No Content` (`OperationsControllers.cs:51`). |
| **Expected DB** | `state` `'Archived'` → `'Disposed'` (`ArchiveState = Archived, Retrieved, Disposed` — `ArchiveEntry.cs:8`); `disposal_authorized_by` = the QM's `user_account.id` (`ArchiveEntry.cs:152-153`). |
| **Expected Audit** | One `audit.audit_trail` row for `RecordDisposed` (`ArchiveEntry.cs:154`), tenant-stamped. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `UPDATE qams.archive_entry SET state='Archived', disposal_authorized_by=NULL WHERE id='bbbbbbbb-bbbb-4bbb-8bbb-000000000001';` under `app.bypass_rls='on'`. |
| **Evidence** | HTTP response capture · SQL before/after · `audit_trail` row |
| **Result / Defect** | Not Run · — |
| **Notes** | Dispose is a **POST**, so `ChangeReasonMiddleware` does not demand `X-Change-Reason` (it fires on DELETE only — `RequestIdentity.cs:149`). Contrast TC-RBAC-API-038. |

---

#### TC-RBAC-API-037 — Adjacent privilege: Department Head cannot dispose (`records.void`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-003 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Pairwise — (held = `view`,`create`,`edit`,`export`) × (required = `void`), seeded role |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · holds `records.view/create/edit/export` (`SystemRoleCatalog.cs:132`); required `records.void` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `dh@demo-lab.local` as above; confirm `records.void` and `records.approve` are absent. The same archive row as TC-RBAC-API-036, restored to `state='Archived'` with retention already expired — so a 403 cannot be mistaken for `ARC-014`. |
| **Test Data** | Archive id `bbbbbbbb-bbbb-4bbb-8bbb-000000000001`; no body |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/archives/bbbbbbbb-bbbb-4bbb-8bbb-000000000001/dispose`. 3. Capture status, `Content-Type`, body. 4. `SELECT state FROM qams.archive_entry WHERE id='bbbbbbbb-bbbb-4bbb-8bbb-000000000001';` |
| **Expected UI** | Dispose control hidden; Archive and Retrieve remain available (both ungated — `OperationsControllers.cs:26,31`). |
| **Expected API** | **PROBLEM-403**, `code = "AUTHZ-403"` — **not** `422 ARC-014`, `ARC-013` or `ARC-015`, all of which the preconditions have cleared. |
| **Expected DB** | `state` still `'Archived'`; `disposal_authorized_by` still `NULL`. |
| **Expected Audit** | No `RecordDisposed` ledger row; no `audit.security_event` row. |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | None. |
| **Evidence** | HTTP response capture · SQL unchanged row |
| **Result / Defect** | Not Run · — |
| **Notes** | Destruction of a regulated record is the highest-consequence route in this slice, which is why the severity is Critical despite `records` being a low-traffic module. |

---

#### TC-RBAC-API-038 — DELETE legal-hold without `X-Change-Reason`: the reason middleware refuses before the permission filter  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 with URS-015/037 (reason for change) as context · RSK-RBAC-005 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Path Coverage — the middleware-ordering path `ChangeReasonMiddleware` (`Program.cs:269`) → refusal, never reaching `UseAuthorization()` (`Program.cs:270`) or the MVC filter |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head — **deliberately a non-holder** of `records.void` · required `records.void` (gate `OperationsControllers.cs:62-63`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`. PowerShell 5.1 drops manual headers — issue this request with `curl.exe`, per conventions §3. |
| **Preconditions** | `dh@demo-lab.local` active on the seeded `Department Head` role (no `records.void`). One `qams.archive_entry` row with `is_on_legal_hold = true` and a non-null `legal_hold_reason`, so `ARC-032` (`ArchiveEntry.cs:98-101`) cannot fire either. |
| **Test Data** | Archive id `bbbbbbbb-bbbb-4bbb-8bbb-000000000002`; **no** `X-Change-Reason` header sent |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `curl.exe -i -X DELETE http://localhost:5080/api/archives/bbbbbbbb-bbbb-4bbb-8bbb-000000000002/legal-hold -H "Authorization: Bearer <jwt>"` with no `X-Change-Reason`. 3. Capture status, `Content-Type`, and the exact `code` in the body. 4. `SELECT is_on_legal_hold FROM qams.archive_entry WHERE id='bbbbbbbb-bbbb-4bbb-8bbb-000000000002';` |
| **Expected UI** | The SPA's `changeReasonInterceptor` collects a reason through the accessible dialog, so this state is not reachable through the UI; the case is API-only by design. |
| **Expected API** | `400 Bad Request`, `Content-Type: application/problem+json`, `title = "A reason is required for this change."`, `code = "CHANGE-REASON-REQUIRED"` (`RequestIdentity.cs:149-156`). **Not 403** — even though this actor also lacks `records.void`, `ChangeReasonMiddleware` is registered at `Program.cs:269`, ahead of `UseAuthorization()` at `:270` and far ahead of the MVC authorization filter. |
| **Expected DB** | `is_on_legal_hold` still `true`. |
| **Expected Audit** | No ledger row and no `audit.field_change` row; no `audit.security_event` row. |
| **Expected Notification** | n/a — refusals raise none. |
| **Cleanup** | None. |
| **Evidence** | Raw `curl.exe -i` capture showing headers and body · SQL unchanged row |
| **Result / Defect** | Not Run · — |
| **Notes** | This is an **ordering** assertion, not a permission assertion. It matters because a security reviewer counting 403s would otherwise conclude the gate fired; it did not. TC-RBAC-API-039 supplies the header and gets the 403. |

---

#### TC-RBAC-API-039 — DELETE legal-hold with `X-Change-Reason` by a non-holder: now the permission filter refuses  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-001, RSK-RBAC-005 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — (`X-Change-Reason` present) × (`records.void` absent), the second row of the two-condition table TC-RBAC-API-038 opened |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · required `records.void`; not held · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; use `curl.exe` for the header |
| **Preconditions** | Identical to TC-RBAC-API-038, and the archive row still `is_on_legal_hold = true`. |
| **Test Data** | Archive id `bbbbbbbb-bbbb-4bbb-8bbb-000000000002`; header `X-Change-Reason: Investigation closed 2026-07-28; hold no longer required.` |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `curl.exe -i -X DELETE http://localhost:5080/api/archives/bbbbbbbb-bbbb-4bbb-8bbb-000000000002/legal-hold -H "Authorization: Bearer <jwt>" -H "X-Change-Reason: Investigation closed 2026-07-28; hold no longer required."` 3. Capture status, `Content-Type`, `code`. 4. `SELECT is_on_legal_hold, legal_hold_reason FROM qams.archive_entry WHERE id='bbbbbbbb-bbbb-4bbb-8bbb-000000000002';` |
| **Expected UI** | Release-hold control hidden for this role. |
| **Expected API** | **PROBLEM-403**, `code = "AUTHZ-403"` — the reason passed the middleware (`RequestIdentity.cs:158`) and the permission filter then refused. |
| **Expected DB** | `is_on_legal_hold` still `true`; `legal_hold_reason` unchanged. |
| **Expected Audit** | No `audit.field_change` row: the scoped reason was set on the request context but `FieldChangeInterceptor` never runs, because no `SaveChanges` occurs. No `audit.security_event` row. |
| **Expected Notification** | n/a — authorization refusals raise none. |
| **Cleanup** | None. |
| **Evidence** | Raw `curl.exe -i` capture · SQL unchanged row · `field_change` delta = 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | Together, 038 and 039 pin both rows of the ordering table with the *same actor and the same route*, varying only the header — the cleanest possible isolation of the two refusals. |

---

#### TC-RBAC-API-040 — `nc.create` is granted but gates nothing: an ungranted actor still raises a nonconformance  [GD — GAP-RBAC-003]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · **no URS covers this** — open a requirement per GAP-RBAC-003 · RSK-RBAC-004 (minted) |
| **Level / Type / Technique** | API · Security (negative expectation) · Statement Coverage — the *absent* `[RequirePermission]` statement on `NonconformancesController.Raise` |
| **Priority / Severity / Automation** | Critical · High · Yes, once the gate exists |
| **Role / Permission / Tenant** | Bespoke role `RBAC-A-NC-VIEWONLY` · holds exactly `nc.view`; `nc.create` deliberately **not** granted · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `POST /api/roles` with `{"name":"RBAC-A-NC-VIEWONLY","permissions":["nc.view"]}`; account `nc-view@demo-lab.local` assigned. Measured today: `POST /api/nonconformances` carries no `[RequirePermission]` (`NonconformancesController.cs:34-43`) and `RaiseNcCommand` carries only `[RequireInternalActor]`, so any non-auditor tenant actor may create. |
| **Test Data** | Body `{"title":"Raised by a view-only role","description":"GAP probe","severity":3,"likelihood":3,"sourceType":"Internal","branchId":null,"departmentId":null,"eventType":"Nonconformity"}` — RPN = 3 × 3 = 9 (`Nonconformance.cs:140`) |
| **Steps** | 1. Log in as `nc-view@demo-lab.local`. 2. `GET /api/auth/me/privileges` — confirm the set is exactly `["nc.view"]`. 3. `POST /api/nonconformances` with the body above. 4. Capture status and `{ id }`. 5. `SELECT status, severity, rpn FROM qams.nonconformance WHERE id=<returned id>;` |
| **Expected UI** | The SPA hides the "Raise NC" control if it keys off `can('nc.create')`; the API accepts the call regardless — affordance is not enforcement (`permissions.service.ts:13-14`). |
| **Expected API** | **Today (records the gap, do not treat as a pass):** `201 Created` with `Location: /api/nonconformances/{id}`. **Acceptance criterion for the fix:** `POST /api/nonconformances` declares `[RequirePermission(PermissionCatalog.Nonconformances, PermissionAction.Create)]` and this exact call then returns **PROBLEM-403** with `code = "AUTHZ-403"`, while an actor granted `nc.create` still receives `201`. |
| **Expected DB** | Today: one new `qams.nonconformance` row, `status='Draft'`, `severity=3`, `rpn=9`. After the fix: `count(*) = 0` for this title. |
| **Expected Audit** | Today: one `audit.audit_trail` row for the NC-raised event. After the fix: none. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `DELETE FROM qams.nonconformance WHERE title='Raised by a view-only role';` under `app.bypass_rls='on'`; reassign the account and deactivate the role. |
| **Evidence** | `/auth/me/privileges` capture proving `nc.create` is absent · HTTP response capture · SQL row |
| **Result / Defect** | Not Run · — |
| **Notes** | `[GD]` on **GAP-RBAC-003**. Do **not** author this as a passing negative access-control case — the refusal does not exist. The case's value today is that it makes the inert grant observable for the access-review record. |

---

#### TC-RBAC-API-041 — `documents.create` is granted but gates nothing  [GD — GAP-RBAC-003]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · **no URS covers this** — see GAP-RBAC-003 · RSK-RBAC-004 (minted) |
| **Level / Type / Technique** | API · Security (negative expectation) · Statement Coverage — the absent gate on `DocumentsController.Create` (`DocumentsController.cs:83-89`) |
| **Priority / Severity / Automation** | Critical · High · Yes, once the gate exists |
| **Role / Permission / Tenant** | Bespoke role `RBAC-A-DOC-VIEWONLY` · holds exactly `documents.view`; `documents.create` not granted · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `POST /api/roles` with `{"name":"RBAC-A-DOC-VIEWONLY","permissions":["documents.view"]}`; account `doc-view@demo-lab.local` assigned. One uploaded `qams.file_reference` row to supply `fileId` (`POST /api/files`). Measured today: `POST /api/documents` carries no `[RequirePermission]`; `CreateDocumentCommand` is `[RequireInternalActor]` only. |
| **Test Data** | Body `{"code":"GAP-PROBE-001","title":"Created by a view-only role","category":"SOP","fileId":"<uploaded file id>","changeSummary":"GAP-RBAC-003 probe","reviewCycleMonths":24}` |
| **Steps** | 1. Log in as `doc-view@demo-lab.local`. 2. `GET /api/auth/me/privileges` — confirm the set is exactly `["documents.view"]`. 3. `POST /api/documents` with the body above. 4. Capture status, `Location`, `{ id }`. 5. `SELECT status FROM qams.controlled_document WHERE code='GAP-PROBE-001';` and `SELECT state FROM qams.document_version WHERE document_id=<id>;` |
| **Expected UI** | "New document" control hidden if it keys off `can('documents.create')`; the API accepts regardless. |
| **Expected API** | **Today:** `201 Created` with `Location: /api/documents/{id}`. **Acceptance criterion for the fix:** the action declares `[RequirePermission(PermissionCatalog.Documents, PermissionAction.Create)]` and this call returns **PROBLEM-403** `AUTHZ-403`, while an actor holding `documents.create` still receives `201`. |
| **Expected DB** | Today: one `qams.controlled_document` row `status='Draft'` plus one `qams.document_version` row `state='Draft'`. After the fix: no rows. |
| **Expected Audit** | Today: `audit.audit_trail` row for the document-created event. After the fix: none. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | `DELETE FROM qams.document_version WHERE document_id=<id>; DELETE FROM qams.controlled_document WHERE code='GAP-PROBE-001';` under `app.bypass_rls='on'`; reassign the account and deactivate the role. |
| **Evidence** | `/auth/me/privileges` capture · HTTP response capture · SQL rows |
| **Result / Defect** | Not Run · — |
| **Notes** | `[GD]` on **GAP-RBAC-003**. `documents` is the only module in this slice with *both* a gated `.view` and an ungated `.create` — the inconsistency an access reviewer is most likely to trip over. |

---

#### TC-RBAC-API-042 — Signing keys `nc.sign`, `changes.sign` and `quality-policy.sign` reach no endpoint  [GD — GAP-RBAC-003]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · **no URS covers this** — see GAP-RBAC-003 · RSK-RBAC-004 (minted) |
| **Level / Type / Technique** | API · Security (negative expectation) · Data Flow — a permission key flows from `PermissionCatalog` into `qams.role_permission`, into `RequestPrivileges.Permissions`, and then reaches **no reader** |
| **Priority / Severity / Automation** | High · High · Yes, once the keys are gated or removed |
| **Role / Permission / Tenant** | Bespoke role `RBAC-A-SIGN-ONLY` · holds exactly `nc.sign`, `changes.sign`, `quality-policy.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `POST /api/roles` with `{"name":"RBAC-A-SIGN-ONLY","permissions":["nc.sign","changes.sign","quality-policy.sign"]}` — all three are members of `PermissionCatalog.AllKeys` (`nc` `PermissionCatalog.cs:136`, `changes` `:141`, `quality-policy` `:146`, all `SignedRecordLifecycle`), so `ROLE-005` is **not** raised and all three persist. Account `sign-only@demo-lab.local` assigned. |
| **Test Data** | The three keys above; probe routes: `POST /api/nonconformances/{id}/triage`, `POST /api/changes/{id}/approve`, `POST /api/quality-policy/{id}/approve` |
| **Steps** | 1. Log in as `sign-only@demo-lab.local`. 2. `GET /api/auth/me/privileges` — confirm the returned set is exactly the three signing keys. 3. `SELECT permission_key FROM qams.role_permission rp JOIN qams.role r ON r.id=rp.role_id AND r.tenant_id=rp.tenant_id WHERE r.normalized_name='RBAC-A-SIGN-ONLY';` — confirm three rows persisted. 4. Drive each of the three probe routes and record the status of each. 5. Search the built API surface for a consumer: confirm `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` contains no route gated on any of the three keys. |
| **Expected UI** | The privilege matrix renders and stores all three switches (`PermissionsService`, the `/roles` screen) — an operator sees three grants that do nothing. |
| **Expected API** | Step 2: `200` returning exactly `["nc.sign","changes.sign","quality-policy.sign"]`. Step 4: **PROBLEM-403** `AUTHZ-403` on all three probe routes, because each demands a *different* key (`nc.approve`, `changes.approve`, `quality-policy.approve`). **Acceptance criterion for the fix:** for each of the three keys, either an endpoint declares `[RequirePermission]` on it — in which case a holder gets a non-403 and a non-holder gets `AUTHZ-403` — or the `Sign` action is removed from that module's action set in `PermissionCatalog.Modules`, and the total key count changes accordingly. |
| **Expected DB** | Three `qams.role_permission` rows persist with `permission_key IN ('nc.sign','changes.sign','quality-policy.sign')`, `tenant_id` = the `demo-lab` tenant (the shadow property stamped by `TenantStampInterceptor`, `AuthorizationConfigurations.cs:22-32`). |
| **Expected Audit** | One `audit.audit_trail` row for `RolePermissionsChanged` carrying the operator's reason (`Role.cs:220-225`) when the role was created with these grants — note that `CreateRoleCommand` requires **no** reason (GAP-RBAC-014), so the create path records none. |
| **Expected Notification** | n/a — not asserted by this batch. |
| **Cleanup** | Reassign `sign-only@demo-lab.local` to `Analyst`; `POST /api/roles/{roleId}/deactivate`. |
| **Evidence** | `/auth/me/privileges` capture · the `role_permission` query result · three HTTP 403 captures · the API-surface search result |
| **Result / Defect** | Not Run · — |
| **Notes** | `[GD]` on **GAP-RBAC-003**. This case deliberately asserts a *persisted but unreachable* privilege rather than a refusal, because there is no refusal to assert. It is the Part 11 §11.10(d) evidence item: the access review certifies three signing capabilities the system never consults. |

---

## Batch coverage note

**Covered.** 42 detailed cases, `TC-RBAC-API-001` … `TC-RBAC-API-042`, all `Result / Defect = Not Run · —`, each naming its technique explicitly. Every one of the ten modules in the Quality and Documents groups has at least one holder-allowed and one non-holder-denied case on a representative `[RequirePermission]` call site, asserting the measured refusal body (status `403`, `Content-Type: application/problem+json`, `title = "You do not have permission to perform this action."`, `code = "AUTHZ-403"`, `traceId`) rather than the status alone. Twelve cases are adjacent-but-insufficient (`nc.void`→`approve` 003; `complaints.edit`→`approve` 006; `complaints.edit`→`void` 008; `audits.edit`→`create` 013; `audits.approve`→`sign` 015; `objectives.edit`→`create` 017; `objectives.create`→`void` 018; `changes.approve`→`void` 021; `reviews.view`→`create` 023; `reviews.view`→`export` 025; `documents.approve`→`sign` 029; `documents.sign`→`void` 031; `quality-policy.edit`→`approve` 035; `records.edit`→`void` 037). Two cases (038, 039) pin the `ChangeReasonMiddleware`-before-`UseAuthorization` ordering on the one DELETE route in the slice. Three `[GD]` cases (040, 041, 042) record ungated keys against GAP-RBAC-003 with implementable acceptance criteria and are explicitly **not** written as passing refusals. Case 029 additionally asserts two zero-deltas (`audit.electronic_signature`, `ESIGN_FAILED`) proving an authorization refusal does not consume a signing attempt.

**In my slice and not covered, with the reason.**
1. **Per-call-site exhaustiveness.** The Quality and Documents groups carry more `[RequirePermission]` sites than the 24 routes cased here. Routes covered by inspection and named in a case's Notes rather than given their own case: `complaints` `/start-investigation`, `/outcome`, `/resolve` (all `complaints.edit`, same key as case 007); `feedback` `/escalate` (`feedback.edit`, case 009); `changes` `/review` (`changes.approve`, case 020); `reviews` `/{id}/decisions` (`reviews.edit`) and `/{id}/close` (`reviews.void`); `documents` `/confirm-review` (`documents.sign`), `/controlled-copies` POST and `/controlled-copies/{copyId}/close` (both `documents.edit`); `records` `/{id}/legal-hold` POST (`records.void`, the POST twin of case 039). Each shares a key already exercised; batch C's per-call-site block is the right home for one-row-per-site coverage.
2. **`nc.approve` on `/verify` and `/confirm-effectiveness`.** Both share `nc.approve` with the triaged case 001, but both also carry SoD rules (`SOD-CAPA-002`, `SOD-CAPA-001`). Cased there they would confound a gate assertion with a duty-pair assertion; batch E owns them.
3. **The `documents.edit` / `records.create` positive pair.** Omitted for space in favour of the higher-risk `sign`/`void` adjacencies.
4. **Anonymous and inactive-role rows of DT-1** (rows 1, 5, 6, 7, 8). These are not module-specific and belong to batch D's privilege-resolution block; duplicating them per module would inflate the matrix without adding coverage.
5. **Class-level ∧ method-level composition (DT-5).** No controller in the Quality or Documents groups carries a class-level `[RequirePermission]` — the three that do (`AccessReviewsController`, `ComplianceController`, `TenantSettingsController`) are all outside this slice. Nothing to cover here; batch C owns it.

**New gaps found in this pass** (numbered `GAP-RBAC-9xx` so they cannot collide with the front matter's `001`–`017` sequence):

- **GAP-RBAC-901 — the front matter's catalogue inventory is one release stale: 171 keys, not 170, and `reports` is no longer read-only.** Front matter §0 and §4.1 record 31 modules / **170** keys with `reports` as a `ReadOnlyModule` (`reports.view`, `reports.export`), both marked `*` (ungated). Measured at `src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:182-183`, `Reports` now declares `[View, Export, Manage]` — three actions — which makes the catalogue **171 keys**. `reports.view` gates six routes (`ReportsController.cs:25,30,35,40,50,59`) and `reports.manage` gates one (`ReportsController.cs:68`) plus a command policy (`QualityHealthProfileSlice.cs:51`); migration `20260801131521_QualityHealthProfile.cs:110-139` backfills `reports.manage` onto existing roles. So the "170 keys / 92 ungated" figures, the URS-095 quotation of "31 modules × 8 actions = 170 keys", and the seeded-role totals in front matter §4.3 are all off by at least one. **Severity: Medium** (traceability, not behaviour). **Acceptance criteria:** the front matter's §0, §4.1 and §4.3 counts are regenerated from `PermissionCatalog.AllKeys.Count` on the build under test, and URS-095 either drops the absolute count or is re-derived by a CI check that fails on divergence. **Responsible role:** Validation Lead. *Outside my slice (`reports` is Operations) but it invalidates a count my slice's `[GD]` cases lean on, so it is recorded rather than silently reconciled.*

- **GAP-RBAC-902 — the reserved-id block for batch A in the front matter does not match what this pass was commissioned to write.** `docs/testing/11-module-rbac.md`'s ID reservation table assigns `11-module-rbac-cases-A.md` the ranges `TC-RBAC-UNIT-001`…`060` and `TC-RBAC-STATE-001`…`020`, and assigns `TC-RBAC-API-001`…`070` to `11-module-rbac-cases-B.md`. This pass was instructed to write **this** file against `TC-RBAC-API-001…`, and has done so. Two consequences that must not be left implicit: (a) `TC-RBAC-API-001`…`042` are **consumed here** and batch B must start at `TC-RBAC-API-043` or the traceability matrix will contain duplicate ids; (b) `TC-RBAC-UNIT-001`…`060` and `TC-RBAC-STATE-001`…`020` — the `PermissionCatalog`/`Role` domain units and the role lifecycle state machine — are **not delivered by this file and remain a coverage hole**, exactly the condition front matter §7 warns about ("a reservation with no matching case file is a coverage hole, not a delivered case"). **Severity: High** (traceability integrity). **Acceptance criteria:** the reservation table is amended to record `11-module-rbac-cases-A.md` = `TC-RBAC-API-001…042`, batch B's range is moved to `TC-RBAC-API-043…`, and the 80 UNIT/STATE ids are re-assigned to a named, scheduled batch. **Responsible role:** Test Package Lead.

- **GAP-RBAC-903 — `documents.view` gates one sub-resource while the document register itself is ungated.** On `DocumentsController`, only `GET /api/documents/{id}/acknowledgements` carries `[RequirePermission(Documents, View)]` (`DocumentsController.cs:60-61`). `GET /api/documents` (`:24`), `GET /api/documents/{id}` (`:31`), `GET /api/documents/{id}/signatures` (`:45`) and `GET /api/documents/{id}/controlled-copies` (`:66`) carry no permission gate, so any authenticated tenant user — including one whose role holds **no** `documents.*` key at all — reads the controlled-document register, every version's metadata and the Part 11 signature manifest for a document. Revoking `documents.view` from a role therefore restricts almost nothing while appearing on the privilege screen to restrict document access entirely. **Severity: High** (a user-access review certifying "this role cannot see documents" would be certifying a control that does not exist — Part 11 §11.10(d)). **Acceptance criteria:** either the four read routes declare `[RequirePermission(PermissionCatalog.Documents, PermissionAction.View)]`, after which a role without the key receives `403 AUTHZ-403` on each and a role with it receives `200`; or a requirement records that the document register is intentionally readable by every authenticated tenant member and `documents.view` is documented as gating the acknowledgement report only. **Responsible role:** Solution Architect + Quality Manager.

- **GAP-RBAC-904 — an authorization refusal writes no security-event record.** `RequirePermissionAttribute.OnAuthorizationAsync` (`RequirePermissionAttribute.cs:38-60`) and `ProblemAuthorizationResultHandler.HandleAsync` (`ProblemAuthorizationResultHandler.cs:23-52`) both write only the problem response; neither resolves `ISecurityEventLog`. An exhaustive grep for `ISecurityEventLog` across `src/` finds call sites only in `Login.cs:32,181`, `MfaAndPin.cs:38`, `RefreshSessions.cs:83,154` and `ExportsController.cs:27` — none in the authorization path. A denied privileged action therefore leaves no row in `audit.security_event` and no ledger entry; the only trace is the transient canonical request log (`ObservabilityMiddleware.cs:65-81`), which is not a tamper-evident record and is not retained with the quality record. Failed *logins* are logged (`LOGIN_FAILED`) and failed *signings* are logged (`ESIGN_FAILED`), so the omission is inconsistent with the system's own pattern. **Severity: High** (21 CFR Part 11 §11.10(d) and §11.300(d) expect unauthorised-use attempts to be detected and recorded; ISO 17025 §7.11 expects access-control events to be traceable). **Acceptance criteria:** every `403 AUTHZ-403` written by either path appends one `audit.security_event` row carrying `event_type = 'AUTHZ_DENIED'` (or an equivalent agreed constant), the actor id, the tenant id, the requested permission key and the route, and a functional test asserts the row exists after a refused request and that the count is unchanged after an allowed one. **Responsible role:** Solution Architect + Quality Manager. *Every negative case in this batch currently asserts the delta as **zero**, which is the measured behaviour; those assertions must be inverted when this gap closes.*

*End of `11-module-rbac-cases-A.md`. Ids consumed: `TC-RBAC-API-001` … `TC-RBAC-API-042`.*
