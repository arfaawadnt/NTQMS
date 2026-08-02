# RBAC — Detailed Test Cases, Batch B

This batch authors the HTTP permission-gate allow/deny cases for the **Risk**, **Resources** and **People** permission groups — the modules `risks`, `compliance`, `conflicts`, `org-context`, `access-reviews` (Risk); `equipment`, `reference-standards`, `monitoring-points`, `suppliers` (Resources); `competencies`, `training`, `test-authorizations`, `users` (People) — one allowed case and one or more denied cases per gated key, plus the three class-level/method-level composition cases those groups contain, plus **ExternalAuditor read-only enforcement against one write command in each of the three groups** (the command-layer `[RequireInternalActor]` defence, reached through the ungated write routes where the HTTP filter is absent), plus the **`CommandPolicyTests` contract** that keeps the command layer deny-by-default. It consumes **TC-RBAC-API-030 … TC-RBAC-API-070** (41 cases) out of batch B's reserved `TC-RBAC-API-001 … 070` block. It deliberately leaves to sibling batches: the `roles`, `users`-adjacent and administration/documents/operations/quality/analytical gates (batches B's lower ids and C), the decision-table-derived privilege-matrix cases `TC-RBAC-DT-*` (C), per-request privilege resolution, immediacy of grant/revoke, inactive-role blackout, platform-admin bypass and RLS on the role tables (D), every `SOD-*` duty-pair case and the `ROLE-006` lockout boundary (E), and all frontend/e2e/a11y cases (F). It records **no** `roles.manage` / `ROLE-00x` case — those are batch A and E.

**Fixture F1 (shared by every case below).** A freshly provisioned tenant `demo-lab` seeded by `SystemRoleCatalog.SeedMissingAsync` (`src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs:52-80`), so all five system roles exist with the grant sets at `SystemRoleCatalog.cs:95-194`. Five accounts, each created by `POST /api/users` from `admin@demo-lab.local` and each placed on the like-named seeded role: `admin@demo-lab.local` (Tenant Administrator), `qm@demo-lab.local` (Quality Manager), `dh@demo-lab.local` (Department Head), `analyst@demo-lab.local` (Analyst), `auditor@demo-lab.local` (External Auditor). Shared password `Demo-Role-Pass-3!` (17 chars, upper+lower+digit+symbol — satisfies `PasswordRules.StrongPassword()`). Every account has an unrestricted working scope (empty branch and department lists). Access tokens are obtained by `POST /api/auth/login` and sent as `Authorization: Bearer <jwt>`.

**Risk IDs.** `docs/validation/02-Functional-Risk-Assessment.md` carries no per-risk identifiers (it indexes risk by *area* against a URS), so per conventions §5 this batch **mints** `RSK-RBAC-010 … RSK-RBAC-015` and says so: `RSK-RBAC-010` a privilege-gated write executes for an actor whose role does not grant the key (over-permission); `RSK-RBAC-011` a gated action is refused for an actor whose role *does* grant the key (under-permission blocks legitimate regulated work); `RSK-RBAC-012` a write route carries no HTTP gate, so the corresponding catalogue key is inert; `RSK-RBAC-013` the read-only External Auditor mutates a regulated record; `RSK-RBAC-014` a command ships with no authorization policy, or with two; `RSK-RBAC-015` a class-level gate composes so that the effective key set differs from the module matrix an administrator configures against.

---

#### TC-RBAC-API-030 — `risks.approve` admits the Quality Manager to residual re-scoring  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-011 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — DT-1 row 3 (authenticated ∧ not platform admin ∧ role active ∧ role grants the key → Allow) |
| **Priority / Severity / Automation** | High · High · Yes (functional, `WebApi.FunctionalTests`) |
| **Role / Permission / Tenant** | Quality Manager · `risks.approve` (granted: `SystemRoleCatalog.cs:107-117` grants every key outside `users`/`tenant-settings`/`roles`-write/`organization.manage`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` (role `qams_app`) |
| **Preconditions** | Fixture F1. One risk exists, created by `POST /api/risks` as `analyst@demo-lab.local`; capture its `id` as `{riskId}`. Gate under test: `[RequirePermission(PermissionCatalog.Risks, PermissionAction.Approve)]` at `src/NT.QAMS.WebApi/Controllers/GovernanceControllers.cs:50`. |
| **Test Data** | `POST /api/risks/{riskId}/residual` body `{"likelihood":2,"impact":3}` (`ResidualAssessmentRequest(int Likelihood, int Impact)`, `Contracts/Governance/GovernanceContracts.cs:7`); residual RPN = 2 × 3 = 6, below the `HighResidualRisk` threshold of 12. |
| **Steps** | 1. `POST /api/auth/login` as `qm@demo-lab.local` / `Demo-Role-Pass-3!`; keep the access JWT. 2. `GET /api/auth/me/privileges` and assert the returned key list contains `risks.approve`. 3. `POST /api/risks/{riskId}/residual` with the body above and the bearer token. 4. Read the status line. 5. `SELECT residual_likelihood, residual_impact FROM qams.risk_item WHERE tenant_id = '{demoLabTenantId}' AND id = '{riskId}';` under `SELECT set_config('app.current_tenant','{demoLabTenantId}',false);`. |
| **Expected UI** | On `/risks/{riskId}` the *Record residual* control is rendered for this role, because `PermissionsService.can('risks.approve')` is true (`frontend/src/app/core/permissions.service.ts:67-70`). |
| **Expected API** | `204 No Content`, empty body. No `application/problem+json`. |
| **Expected DB** | The `qams.risk_item` row for `{riskId}` now carries the residual likelihood 2 and impact 3; its `xmin` differs from the value read before step 3 (there is no `row_version` column — `xmin` is the concurrency token). |
| **Expected Audit** | One appended row in `audit.audit_trail` for this tenant carrying the residual-assessment event, with `prev_hash` equal to the previous row's `entry_hash` (query with `SELECT set_config('app.bypass_rls','on',false);` first, or the rows are invisible). |
| **Expected Notification** | n/a — no notification rule is seeded for a residual re-score; `notification_rule` is empty in F1. |
| **Cleanup** | `POST /api/risks/{riskId}/close` as `qm@demo-lab.local`; leave the audit ledger untouched (append-only). |
| **Evidence** | HTTP response capture (status + headers) · `/api/auth/me/privileges` body · SQL result of step 5 · `audit_trail` row |
| **Result / Defect** | Not Run · — |
| **Notes** | Pair with TC-RBAC-API-031, which is the same request from a role without the key. Do not assert a `201`: the action returns `NoContent()` (`GovernanceControllers.cs:54`). |

---

#### TC-RBAC-API-031 — `risks.approve` refuses the Analyst with `403 AUTHZ-403`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — DT-1 row 4 (authenticated ∧ role active ∧ role does **not** grant the key → 403) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · `risks.approve` **not** granted — the seeded Analyst holds `risks.view`, `risks.create`, `risks.edit`, `risks.export` only (`SystemRoleCatalog.cs:163`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1, plus the risk `{riskId}` from TC-RBAC-API-030 with **no** residual recorded. |
| **Test Data** | `POST /api/risks/{riskId}/residual` body `{"likelihood":2,"impact":3}`, bearer token for `analyst@demo-lab.local`. |
| **Steps** | 1. Log in as `analyst@demo-lab.local` / `Demo-Role-Pass-3!`. 2. `GET /api/auth/me/privileges`; assert `risks.approve` is **absent** and `risks.edit` is present. 3. `POST /api/risks/{riskId}/residual` with the body above. 4. Read status, `Content-Type` and the JSON `code` field. 5. `SELECT residual_likelihood, residual_impact, xmin FROM qams.risk_item WHERE tenant_id='{demoLabTenantId}' AND id='{riskId}';`. |
| **Expected UI** | The *Record residual* control is absent on `/risks/{riskId}` for this role (`can('risks.approve')` false). The screen itself is reachable — no route carries a permission guard (GAP-RBAC-007). |
| **Expected API** | `403 Forbidden`, `Content-Type: application/problem+json`, body `code` = **`AUTHZ-403`**, `title` = `You do not have permission to perform this action.` (written by `RequirePermissionAttribute.OnAuthorizationAsync`, `src/NT.QAMS.WebApi/Authorization/RequirePermissionAttribute.cs:54-59`, via `ProblemResponse.WriteAsync`). Body also carries `traceId`; `correlationId` is present when `X-Correlation-Id` was sent. |
| **Expected DB** | The `qams.risk_item` row is byte-for-byte unchanged — residual columns still null and `xmin` identical to the pre-call read. The filter short-circuits before MediatR, so no `SaveChanges` occurs. |
| **Expected Audit** | **No** row appended to `audit.audit_trail`, **no** row in `audit.field_change`, and **no** row in `audit.security_event`: no authorization-refusal path calls `ISecurityEventLog` (its only injectors are `Login.cs:32,181`, `MfaAndPin.cs:38`, `RefreshSessions.cs:83,154`, `ComplianceLedgerServices.cs:87` and `ExportsController.cs:27`). See **GAP-RBAC-902**. |
| **Expected Notification** | n/a — no notification is defined for a refused request. |
| **Cleanup** | None — the request has no effect. |
| **Evidence** | HTTP response capture including `Content-Type` and the full problem body · privileges body from step 2 · SQL before/after showing an identical `xmin` |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert the exact string `AUTHZ-403`, not merely "a 403": the framework result handler emits the same code (`ProblemAuthorizationResultHandler.cs:16,27-32`), which is deliberate, so the code alone does not distinguish the two writers — the *presence of an authenticated identity* does. Do **not** expect `SOD_VIOLATION` or any `SOD-*` code here (GAP-RBAC-002). |

---

#### TC-RBAC-API-032 — `risks.void` refuses the Department Head on risk closure  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning — the "holds create/edit but not the state-destroying action" partition of the Risk group |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `risks.void` **not** granted — seeded DH holds `risks.view/create/edit/export` (`SystemRoleCatalog.cs:133`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. An open risk `{riskId2}` created by `dh@demo-lab.local` via `POST /api/risks` (that route carries **no** `[RequirePermission]`, so the creation itself succeeds). Gate under test: `GovernanceControllers.cs:58`. |
| **Test Data** | `POST /api/risks/{riskId2}/close`, empty body, bearer token for `dh@demo-lab.local`. |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/risks` body `{"title":"Batch-B closure probe","category":"Operational","likelihood":3,"impact":3,"branchId":null,"departmentId":null}`; capture `{riskId2}`; expect `201`. 3. `POST /api/risks/{riskId2}/close`. 4. Read status and `code`. 5. `SELECT status FROM qams.risk_item WHERE tenant_id='{demoLabTenantId}' AND id='{riskId2}';`. |
| **Expected UI** | The *Close risk* control is not rendered for this role; the *Add mitigation* control is, because `POST /api/risks/{id}/actions` is ungated (`GovernanceControllers.cs:37`). |
| **Expected API** | Step 2 → `201 Created` with a `Location` header. Step 3 → `403` `application/problem+json`, `code` = `AUTHZ-403`. |
| **Expected DB** | `qams.risk_item.status` for `{riskId2}` is unchanged from the value written at creation; no `Closed` transition is persisted. |
| **Expected Audit** | Exactly one `audit.audit_trail` append for this scenario — the one raised by step 2's creation. Step 3 appends nothing. |
| **Expected Notification** | n/a — closure raises no seeded notification in F1. |
| **Cleanup** | `POST /api/risks/{riskId2}/close` as `qm@demo-lab.local` (holds `risks.void`) to leave the register tidy. |
| **Evidence** | Both HTTP captures · SQL status read · `audit_trail` count delta of exactly 1 |
| **Result / Defect** | Not Run · — |
| **Notes** | The contrast inside one case is the point: the same actor may **create** a risk (no gate at all) but not **close** it (gated `risks.void`). That asymmetry is what GAP-RBAC-003 is about — `risks.create` is an inert catalogue key here. |

---

#### TC-RBAC-API-033 — Class-level `compliance.view` admits the External Auditor to the audit trail  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-011 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — DT-5 row 2 (class-level gate present, method-level absent → the class key is the whole requirement) |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | External Auditor · `compliance.view` (granted: `compliance` is in the `risk` group, not administration, and `View` is in `ReadActions` — `SystemRoleCatalog.cs:31,184-193`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. At least one `audit.audit_trail` row exists for `demo-lab` (guaranteed — provisioning and user creation both append). Gate under test: the class-level `[RequirePermission(PermissionCatalog.Compliance, PermissionAction.View)]` at `src/NT.QAMS.WebApi/Controllers/ComplianceController.cs:20`; the action `AuditTrail` at `:23-26` carries no further gate. |
| **Test Data** | `GET /api/compliance/audit-trail?take=50`, bearer token for `auditor@demo-lab.local`. |
| **Steps** | 1. Log in as `auditor@demo-lab.local`. 2. `GET /api/auth/me/privileges`; assert `compliance.view` and `compliance.export` are present and `compliance.create` and `compliance.approve` are **absent**. 3. `GET /api/compliance/audit-trail?take=50`. 4. `GET /api/compliance/signatures?take=50`. 5. `GET /api/compliance/chain-verification`. |
| **Expected UI** | The Compliance shell renders the audit-trail, signature-log and chain-verification panels for this role; the *Open audit-trail review* button is hidden (`can('compliance.create')` false). |
| **Expected API** | Steps 3, 4 and 5 each return `200 OK` with `application/json`. Step 5's body is the chain-verification result computed for the tenant id taken from the `tenant_id` claim (`ComplianceController.cs:63-69`); it must not be `400`, because the auditor's JWT carries a tenant. |
| **Expected DB** | No write of any kind — all three are `IQuery` reads, and `AuthorizationBehavior` returns at `src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs:44-47` for non-commands. |
| **Expected Audit** | No new `audit.audit_trail` row: reading the ledger is not itself ledgered. |
| **Expected Notification** | n/a — reads raise no notification. |
| **Cleanup** | None. |
| **Evidence** | Three HTTP captures with status and body length · privileges body from step 2 |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the positive half of the auditor's read-only contract; the write half is TC-RBAC-API-035 and TC-RBAC-API-065…067. `GET /api/compliance/security-events` may be added to step 3's set — its store now tenant-filters after the v1.51.2 `Hardening2_RlsGapClosure` fix. |

---

#### TC-RBAC-API-034 — Analyst holds no `compliance.*` key at all, so every compliance read is refused  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning — the "module absent from the role's grant table entirely" partition (as opposed to "module present, action missing") |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · `compliance.view` **not** granted — the seeded Analyst's explicit grant table (`SystemRoleCatalog.cs:153-177`) lists 24 modules and `compliance` is not one of them · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. |
| **Test Data** | Bearer token for `analyst@demo-lab.local`; the five class-gated GET routes `audit-trail`, `field-changes`, `signatures`, `security-events`, `audit-trail-reviews`. |
| **Steps** | 1. Log in as `analyst@demo-lab.local`. 2. `GET /api/auth/me/privileges`; assert no key beginning `compliance.` appears. 3. `GET /api/compliance/audit-trail?take=10`. 4. `GET /api/compliance/field-changes?take=10`. 5. `GET /api/compliance/signatures?take=10`. 6. `GET /api/compliance/security-events?take=10`. 7. `GET /api/compliance/audit-trail-reviews`. 8. Record the status and `code` of each. |
| **Expected UI** | The Compliance area renders no panel content for this role; every server call behind it returns the same refusal. The route is still navigable (GAP-RBAC-007). |
| **Expected API** | All five requests → `403` `application/problem+json`, `code` = `AUTHZ-403`, identical `title`. No `404`, no `401`, no `500`. |
| **Expected DB** | No reads reach the ledger stores; nothing is written. |
| **Expected Audit** | Five refusals, **zero** rows in `audit.security_event` — see GAP-RBAC-902. |
| **Expected Notification** | n/a — refusals raise none. |
| **Cleanup** | None. |
| **Evidence** | Five HTTP captures tabulated route → status → code · privileges body |
| **Result / Defect** | Not Run · — |
| **Notes** | Five routes, one gate: this is the class-level attribute doing all the work. If any single route returns `200`, that route is missing from the controller or has been moved out of `ComplianceController` — investigate before recording a defect. |

---

#### TC-RBAC-API-035 — `compliance.view ∧ compliance.create` — the auditor holds one condition and is still refused  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-015 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · **Multiple-Condition / MC-DC** — two independent authorization filters composed as AND; this case fixes `compliance.view = true` and varies `compliance.create` to false, demonstrating `create`'s independent effect on the outcome |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | External Auditor · needs **both** `compliance.view` (has) **and** `compliance.create` (has not) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. Gates under test: class-level `ComplianceController.cs:20` **∧** method-level `ComplianceController.cs:46`. `RequirePermissionAttribute` is `AttributeUsage(…, AllowMultiple = true)` (`RequirePermissionAttribute.cs:26`) and each instance is an independent `IAsyncAuthorizationFilter`, so the two compose as AND (DT-5 row 3, GAP-RBAC-011). |
| **Test Data** | `POST /api/compliance/audit-trail-reviews` body `{"periodStart":"2026-07-01","periodEnd":"2026-07-31"}` (`OpenAuditTrailReviewRequest(DateOnly PeriodStart, DateOnly PeriodEnd)`, `Contracts/Compliance/ComplianceContracts.cs:5`). |
| **Steps** | 1. Log in as `auditor@demo-lab.local`. 2. `GET /api/compliance/audit-trail-reviews` — this proves the *view* condition is satisfied. 3. `POST /api/compliance/audit-trail-reviews` with the body above. 4. Read status and `code`. 5. `SELECT count(*) FROM qams.audit_trail_review WHERE tenant_id='{demoLabTenantId}';` before and after step 3. |
| **Expected UI** | The reviews list renders (read allowed); the *Open review* control is hidden for this role. |
| **Expected API** | Step 2 → `200 OK`. Step 3 → `403` `application/problem+json`, `code` = `AUTHZ-403`. |
| **Expected DB** | `count(*)` on `qams.audit_trail_review` is identical before and after step 3. |
| **Expected Audit** | No `audit.audit_trail` append; the command never reaches MediatR because the method-level filter short-circuits first. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Both HTTP captures · the two `count(*)` results · a note of which of the two filters produced the refusal (both write the identical body, so the discriminator is step 2 succeeding) |
| **Result / Defect** | Not Run · — |
| **Notes** | The MC-DC partner case — `view = true`, `create = true` → allow — is TC-RBAC-API-036's step 2 with the Quality Manager. The third combination (`view = false`, `create = true`) is TC-RBAC-API-044's pattern applied to `compliance`; it is not authored here because no seeded role produces it, and minting a bespoke role for it belongs to batch C's decision-table set. |

---

#### TC-RBAC-API-036 — `compliance.create` and `compliance.approve` admit the Quality Manager to the full review cycle  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-011 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — the audit-trail-review cycle end to end, exercising both method-level keys behind one class-level key |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `compliance.view` ∧ `compliance.create` (open) then `compliance.view` ∧ `compliance.approve` (complete) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. Gates: `ComplianceController.cs:20` ∧ `:46` for the open, `:20` ∧ `:52` for the complete. |
| **Test Data** | Open body `{"periodStart":"2026-07-01","periodEnd":"2026-07-31"}`; complete body `{"anomaliesFound":false,"conclusion":"July 2026 audit-trail period reviewed; hash chain intact, no anomalies."}` (`CompleteAuditTrailReviewRequest(bool AnomaliesFound, string Conclusion)`, `ComplianceContracts.cs:7`). |
| **Steps** | 1. Log in as `qm@demo-lab.local`. 2. `GET /api/auth/me/privileges`; assert `compliance.view`, `compliance.create`, `compliance.approve`, `compliance.export` all present. 3. `POST /api/compliance/audit-trail-reviews` with the open body; capture `id` as `{reviewId}`. 4. `POST /api/compliance/audit-trail-reviews/{reviewId}/complete` with the complete body. 5. `GET /api/compliance/audit-trail-reviews` and locate `{reviewId}`. 6. `SELECT status, reviewed_by, completed_at_utc FROM qams.audit_trail_review WHERE tenant_id='{demoLabTenantId}' AND id='{reviewId}';`. |
| **Expected UI** | Both the *Open review* and *Complete review* controls render for this role; the completed review shows in the list with its conclusion. |
| **Expected API** | Step 3 → `200 OK` body `{ "id": "<guid>" }` (the action returns `Ok(new { id = … })`, `ComplianceController.cs:49` — **not** `201`). Step 4 → `204 No Content`. Step 5 → `200` with `{reviewId}` present. |
| **Expected DB** | The `qams.audit_trail_review` row exists with a non-null `reviewed_by` equal to the QM's `user_account.id` and a non-null `completed_at_utc`. |
| **Expected Audit** | Two appends to `audit.audit_trail` for this tenant (review opened, review completed), chained: the second row's `prev_hash` equals the first's `entry_hash`. |
| **Expected Notification** | n/a — no seeded notification rule targets audit-trail reviews in F1. |
| **Cleanup** | None — a completed review is Part-11 recertification evidence and must not be deleted. Note the `{reviewId}` in the execution record so later runs can distinguish their own rows. |
| **Evidence** | Three HTTP captures · SQL row · the two chained ledger rows with their hashes |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert `200` on step 3, not `201`: the controller does not use `CreatedAtAction` here. This case is also the MC-DC "both conditions true" partner to TC-RBAC-API-035. |

---

#### TC-RBAC-API-037 — `conflicts.approve` admits the Quality Manager to assess a declaration  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-011 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — DT-1 row 3 |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `conflicts.approve` (granted) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. A declaration `{conflictId}` created by `POST /api/conflicts` (ungated, `ConflictsController.cs:25`) as `dh@demo-lab.local`, with `declarantId` set to the **Department Head's** user id — not the QM's — so the assessment does not trip `SOD-COI-001` (`ConflictDeclaration.cs:72`). Gate under test: `ConflictsController.cs:34`. |
| **Test Data** | Create body `{"declarantId":"{dhUserId}","description":"Spouse employed by candidate supplier","relatedParty":"Acme Reagents Ltd","declaredOn":"2026-07-20"}`; assess body `{"riskLevel":"Medium","mitigation":"Declarant excluded from the Acme supplier evaluation panel."}` (`AssessConflictRequest(string RiskLevel, string Mitigation)`, `GovernanceContracts.cs:82`). |
| **Steps** | 1. Log in as `dh@demo-lab.local` and `POST /api/conflicts` with the create body; capture `{conflictId}`. 2. Log in as `qm@demo-lab.local`. 3. `POST /api/conflicts/{conflictId}/assess` with the assess body. 4. Read the status. 5. `SELECT status, risk_level FROM qams.conflict_declaration WHERE tenant_id='{demoLabTenantId}' AND id='{conflictId}';`. |
| **Expected UI** | The *Assess* control renders for the QM on the conflict detail view. |
| **Expected API** | Step 1 → `201 Created`. Step 3 → `204 No Content` (`ConflictsController.cs:38`). |
| **Expected DB** | `qams.conflict_declaration.risk_level` = `Medium` and the status has advanced past the declared state for `{conflictId}`. |
| **Expected Audit** | One `audit.audit_trail` append for the assessment, chained to the previous entry. |
| **Expected Notification** | n/a — no seeded rule targets conflict assessment. |
| **Cleanup** | `POST /api/conflicts/{conflictId}/close` as `qm@demo-lab.local` with `{"outcome":"Mitigated","closureNote":"Batch-B fixture teardown."}`. |
| **Evidence** | Two HTTP captures · SQL row · ledger row |
| **Result / Defect** | Not Run · — |
| **Notes** | The declarant must not be the QM. If it is, the request fails `422 SOD-COI-001` and the case proves nothing about the permission gate — a classic false negative. The SoD rule itself is batch E's `TC-RBAC-SEC-*`, not this case. |

---

#### TC-RBAC-API-038 — `conflicts.approve` refuses the Department Head  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning — "holds view/create/edit/export, lacks approve/void" partition, the shape shared by DH across `risks`, `conflicts`, `equipment`, `suppliers` |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `conflicts.approve` **not** granted (`SystemRoleCatalog.cs:134` grants View/Create/Edit/Export only) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1 and a declaration `{conflictId2}` in the declared (unassessed) state, whose `declarantId` is the Analyst's user id. |
| **Test Data** | `POST /api/conflicts/{conflictId2}/assess` body `{"riskLevel":"Low","mitigation":"No action required."}`, bearer token for `dh@demo-lab.local`. |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `GET /api/auth/me/privileges`; assert `conflicts.edit` present, `conflicts.approve` absent. 3. `POST /api/conflicts/{conflictId2}/assess` with the body above. 4. Read status, `Content-Type`, `code`. 5. `SELECT status, risk_level FROM qams.conflict_declaration WHERE tenant_id='{demoLabTenantId}' AND id='{conflictId2}';`. |
| **Expected UI** | The *Assess* control is absent for this role; the declaration remains readable. |
| **Expected API** | `403` `application/problem+json`, `code` = `AUTHZ-403`. |
| **Expected DB** | `risk_level` is still `NULL` and the status is unchanged. |
| **Expected Audit** | Nothing appended anywhere; no `audit.security_event` row (GAP-RBAC-902). |
| **Expected Notification** | n/a. |
| **Cleanup** | None — leave `{conflictId2}` unassessed for TC-RBAC-API-039. |
| **Evidence** | HTTP capture · privileges body · SQL row showing `risk_level IS NULL` |
| **Result / Defect** | Not Run · — |
| **Notes** | Note that the *same* DH may declare a conflict (`POST /api/conflicts` is ungated) but not assess one. `conflicts.create` is therefore another inert catalogue key (GAP-RBAC-003). |

---

#### TC-RBAC-API-039 — `conflicts.void` refuses the Analyst on closure  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — DT-1 row 4 applied to the Void action of the Risk group |
| **Priority / Severity / Automation** | Medium · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · `conflicts.void` **not** granted (`SystemRoleCatalog.cs:164` — View/Create/Edit/Export) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1 and `{conflictId2}` from TC-RBAC-API-038. Gate under test: `ConflictsController.cs:42`. |
| **Test Data** | `POST /api/conflicts/{conflictId2}/close` body `{"outcome":"NoConflict","closureNote":"Analyst attempt — should never land."}` (`CloseConflictRequest(string Outcome, string ClosureNote)`, `GovernanceContracts.cs:84`). |
| **Steps** | 1. Log in as `analyst@demo-lab.local`. 2. `POST /api/conflicts/{conflictId2}/close` with the body above. 3. Read status and `code`. 4. `SELECT status, outcome FROM qams.conflict_declaration WHERE tenant_id='{demoLabTenantId}' AND id='{conflictId2}';`. |
| **Expected UI** | The *Close* control is absent for the Analyst. |
| **Expected API** | `403` `application/problem+json`, `code` = `AUTHZ-403`. |
| **Expected DB** | `outcome` is still `NULL`; the closure note string never reaches the database — search `qams.conflict_declaration` for the literal `Analyst attempt` and expect zero rows. |
| **Expected Audit** | No append. |
| **Expected Notification** | n/a. |
| **Cleanup** | `POST /api/conflicts/{conflictId2}/close` as `qm@demo-lab.local` with `{"outcome":"NoConflict","closureNote":"Batch-B fixture teardown."}`. |
| **Evidence** | HTTP capture · the zero-row search for the rejected note text |
| **Result / Defect** | Not Run · — |
| **Notes** | The "search for the rejected payload string" assertion is worth keeping: it proves the body was never persisted, which a status-code-only assertion does not. |

---

#### TC-RBAC-API-040 — `org-context.create` refuses the Analyst on both create routes  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · **Pairwise** — one key (`org-context.create`) × two distinct routes (`interested-parties`, `issues`), one role |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · `org-context.create` **not** granted — the seeded Analyst holds `org-context.view` and `org-context.export` only (`SystemRoleCatalog.cs:165`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. Gates under test: `OrgContextController.cs:24` (parties) and `:58` (issues) — the same key on two routes. |
| **Test Data** | Party body `{"name":"Regional Accreditation Body","category":"Regulator","needsAndExpectations":"Continued ISO 17025 conformity","relevantRequirements":"ISO/IEC 17025:2017","reviewedOn":"2026-07-15"}`; issue body `{"type":"External","category":"Regulatory","description":"Revised accreditation criteria expected Q4","impact":"Medium"}`. |
| **Steps** | 1. Log in as `analyst@demo-lab.local`. 2. `GET /api/org-context/interested-parties` — expect `200` (the GET is ungated, `OrgContextController.cs:19`). 3. `POST /api/org-context/interested-parties` with the party body. 4. `POST /api/org-context/issues` with the issue body. 5. Record both statuses and both `code` values. 6. `SELECT count(*) FROM qams.interested_party WHERE tenant_id='{demoLabTenantId}';` and `SELECT count(*) FROM qams.context_issue WHERE tenant_id='{demoLabTenantId}';` before and after. |
| **Expected UI** | The org-context screen lists parties and issues for this role but offers no *Register* control on either tab. |
| **Expected API** | Step 2 → `200 OK`. Steps 3 and 4 → `403` `application/problem+json`, `code` = `AUTHZ-403` on both. |
| **Expected DB** | Both counts identical before and after; no row containing `Regional Accreditation Body` in `qams.interested_party`, none containing `Revised accreditation criteria` in `qams.context_issue`. |
| **Expected Audit** | No appends. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Three HTTP captures · the four `count(*)` results · both zero-row payload searches |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 2 is load-bearing: it separates "the role cannot reach the module" from "the role cannot perform this action". Without it a `403` on the POST is ambiguous. |

---

#### TC-RBAC-API-041 — `org-context.void` admits the Department Head to close a context issue  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-011 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — the documented granularity exception: DH is the **only** seeded non-QM/TA role granted a `void` key anywhere in the Risk group |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `org-context.void` (granted — `SystemRoleCatalog.cs:135` lists View/Create/Edit/**Void**/Export, with the rationale written at `SystemRoleCatalog.cs:86-92`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. An open context issue `{issueId}` created by `dh@demo-lab.local` (DH holds `org-context.create`). Gate under test: `OrgContextController.cs:84`. |
| **Test Data** | Create body `{"type":"Internal","category":"Resourcing","description":"Second analyst post vacant since June","impact":"High"}`; close body `{"resolution":"Post filled 2026-07-28; capacity restored."}` (`CloseContextIssueRequest(string Resolution)`, `PlatformContracts.cs:60`). |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/org-context/issues` with the create body; capture `{issueId}`. 3. `POST /api/org-context/issues/{issueId}/close` with the close body. 4. Read both statuses. 5. `SELECT status, resolution FROM qams.context_issue WHERE tenant_id='{demoLabTenantId}' AND id='{issueId}';`. 6. `POST /api/org-context/interested-parties/{partyId}/archive` for an existing party — the **same** `org-context.void` key (`OrgContextController.cs:44`). |
| **Expected UI** | Both *Close issue* and *Archive party* controls render for the DH, because both read the one key `org-context.void`. |
| **Expected API** | Step 2 → `200 OK` body `{ "id": … }` (the action returns `Ok(new { id = … })`, `OrgContextController.cs:59-64` — not `201`). Step 3 → `204 No Content`. Step 6 → `204 No Content`. |
| **Expected DB** | `qams.context_issue.resolution` for `{issueId}` equals the submitted string and the status is the closed value; the archived party's row reflects the archived status. |
| **Expected Audit** | Three `audit.audit_trail` appends (issue registered, issue closed, party archived), each chained to its predecessor. |
| **Expected Notification** | n/a. |
| **Cleanup** | None — closed and archived records are the intended end state. |
| **Evidence** | Three HTTP captures · SQL rows for both the issue and the party |
| **Result / Defect** | Not Run · — |
| **Notes** | This case documents a **deliberate** widening: one key covers two semantically different destructive acts, so granting a DH the power to close issues necessarily grants the power to archive interested parties. That coupling is the granularity compromise recorded at `SystemRoleCatalog.cs:86-92` and should be visible in the access-review evidence. |

---

#### TC-RBAC-API-042 — `org-context.edit` refuses the Analyst on revise and on link-risk  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · **Pairwise** — one key (`org-context.edit`) × three routes (`PUT interested-parties/{id}`, `PUT issues/{id}`, `POST issues/{id}/link-risk`) |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · `org-context.edit` **not** granted · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1, an existing party `{partyId}` and an open issue `{issueId2}`, and an existing risk `{riskId}` to link. Gates under test: `OrgContextController.cs:34`, `:67`, `:76`. |
| **Test Data** | Revise-party body `{"name":"Regional Accreditation Body (revised)","category":"Regulator","needsAndExpectations":"…","relevantRequirements":null,"reviewedOn":"2026-07-30"}`; revise-issue body `{"type":"Internal","category":"Resourcing","description":"edited by analyst","impact":"Low"}`; link body `{"riskId":"{riskId}"}`. |
| **Steps** | 1. Log in as `analyst@demo-lab.local`. 2. `PUT /api/org-context/interested-parties/{partyId}`. 3. `PUT /api/org-context/issues/{issueId2}`. 4. `POST /api/org-context/issues/{issueId2}/link-risk`. 5. Record three statuses and three `code` values. 6. `SELECT linked_risk_id FROM qams.context_issue WHERE tenant_id='{demoLabTenantId}' AND id='{issueId2}';`. |
| **Expected UI** | No *Edit* or *Link risk* affordance renders for the Analyst on either tab. |
| **Expected API** | All three → `403` `application/problem+json`, `code` = `AUTHZ-403`. |
| **Expected DB** | `qams.context_issue.linked_risk_id` for `{issueId2}` is still `NULL`; no row in `qams.context_issue` contains the literal `edited by analyst`. |
| **Expected Audit** | No appends. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Three HTTP captures tabulated route → status → code · SQL showing `linked_risk_id IS NULL` |
| **Result / Defect** | Not Run · — |
| **Notes** | `link-risk` is a `POST` that requires `Edit`, not `Create` — a reasonable but non-obvious mapping. Record it in the access-review narrative so a reviewer granting only `org-context.create` does not expect linking to work. |

---

#### TC-RBAC-API-043 — Class-level `access-reviews.view` alone admits a *write*: the QM opens a recertification review  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 (the behaviour is implementation-derived — no requirement states that a recertification write is gated on a read key) · RSK-RBAC-015 (minted) |
| **Level / Type / Technique** | API · Functional (positive, adverse-finding) · Decision Table — DT-5 row 2: class-level gate present, method-level absent, so the **only** requirement for a write is the `view` key |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `access-reviews.view` only is required; the QM also holds `access-reviews.create/edit/approve/void/sign/export`, none of which is consulted · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. Gate under test: the class-level `[RequirePermission(PermissionCatalog.AccessReviews, PermissionAction.View)]` at `src/NT.QAMS.WebApi/Controllers/AccessReviewsController.cs:20`; the `Open` action at `:27-29` and the `Complete` action at `:31-36` carry **no** method-level gate. |
| **Test Data** | `POST /api/access-reviews` with no body; then `POST /api/access-reviews/{reviewId}/complete` body `{"changesRequired":false,"conclusion":"Q3 2026 user-access recertification: all accounts and roles confirmed appropriate."}` (`CompleteAccessReviewRequest(bool ChangesRequired, string Conclusion)`, `Contracts/IdentityAccess/AccessReviewContracts.cs:5`). |
| **Steps** | 1. Log in as `qm@demo-lab.local`. 2. `POST /api/access-reviews`; capture `id` as `{reviewId}`. 3. `POST /api/access-reviews/{reviewId}/complete` with the body above. 4. `SELECT status, reviewed_by, completed_at_utc FROM qams.user_access_review WHERE tenant_id='{demoLabTenantId}' AND id='{reviewId}';`. |
| **Expected UI** | The access-review screen offers both *Open* and *Complete* to this role. |
| **Expected API** | Step 2 → `200 OK` body `{ "id": … }` (`AccessReviewsController.cs:29`). Step 3 → `204 No Content`. |
| **Expected DB** | `qams.user_access_review` holds `{reviewId}` with a non-null `reviewed_by` and `completed_at_utc`, and the completed status. |
| **Expected Audit** | Two `audit.audit_trail` appends, chained. |
| **Expected Notification** | n/a. |
| **Cleanup** | None — a completed access review is §11.10(d) evidence. |
| **Evidence** | Two HTTP captures · SQL row · a written note that `access-reviews.create` / `.approve` were **not** required |
| **Result / Defect** | Not Run · — |
| **Notes** | **Labelled `[ID]`, not `[IV]`** — this is the code's behaviour with no requirement behind it, and it is the concrete manifestation of **GAP-RBAC-011**: five of the module's seven keys (`create`, `edit`, `approve`, `void`, `sign`) are inert on this controller while a `view` grant is sufficient to *complete* a recertification record. Do not "fix" the case by asserting `access-reviews.approve`; assert what the build does and let the gap carry the recommendation. |

---

#### TC-RBAC-API-044 — A role granted only `access-reviews.create` is still refused, because the class gate demands `view`  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-015 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · **MC-DC** — the complement of TC-RBAC-API-043: `view = false`, `create = true` must still deny, proving the class-level condition's independent effect |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Bespoke tenant-defined role `Recert Clerk` holding exactly one key, `access-reviews.create` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. As `admin@demo-lab.local`: `POST /api/roles` `{"name":"Recert Clerk","description":"Batch-B probe role","permissionKeys":["access-reviews.create"]}`, then `PUT /api/users/{clerkUserId}/assigned-role` `{"roleId":"{recertClerkRoleId}"}` for a sixth account `clerk@demo-lab.local`. The key is valid (`access-reviews.create` ∈ `PermissionCatalog.AllKeys`), so `ROLE-005` must **not** fire. |
| **Test Data** | Bearer token for `clerk@demo-lab.local`; `POST /api/access-reviews` with no body. |
| **Steps** | 1. Log in as `clerk@demo-lab.local`. 2. `GET /api/auth/me/privileges`; assert the key list is exactly `["access-reviews.create"]`. 3. `GET /api/access-reviews`. 4. `POST /api/access-reviews`. 5. Record both statuses and `code` values. 6. `SELECT count(*) FROM qams.user_access_review WHERE tenant_id='{demoLabTenantId}';` before and after step 4. |
| **Expected UI** | The access-review screen is navigable (no route guard) and renders empty, then shows the refusal from its list call. |
| **Expected API** | Step 3 → `403` `application/problem+json`, `code` = `AUTHZ-403` (the class gate). Step 4 → `403`, same code — the class-level filter refuses before any method-level consideration. |
| **Expected DB** | `count(*)` on `qams.user_access_review` unchanged. The `qams.role_permission` row for the bespoke role holds exactly one `permission_key` value, `access-reviews.create`. |
| **Expected Audit** | Role creation and role assignment each append to `audit.audit_trail`; the two refusals append nothing. |
| **Expected Notification** | n/a. |
| **Cleanup** | `PUT /api/users/{clerkUserId}/assigned-role` back to the seeded Analyst role, then `POST /api/roles/{recertClerkRoleId}/deactivate` as `admin@demo-lab.local` — roles cannot be deleted (GAP-RBAC-015), only deactivated. |
| **Evidence** | Two HTTP captures · privileges body proving the one-key grant · `SELECT permission_key FROM qams.role_permission WHERE tenant_id=… AND role_id='{recertClerkRoleId}';` |
| **Result / Defect** | Not Run · — |
| **Notes** | Labelled `[ID]` for the same reason as TC-RBAC-API-043. Together the pair is the MC-DC evidence for GAP-RBAC-011's acceptance criterion: an administrator configuring from the module matrix alone gets the answer wrong in **both** directions. |

---

#### TC-RBAC-API-045 — `equipment.void` admits the Quality Manager to retire an instrument  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-011 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — DT-1 row 3, Resources group |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `equipment.void` (granted) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. An equipment item `{equipId}` registered by `POST /api/equipment` (ungated) as `analyst@demo-lab.local`. Gate under test: the **only** `[RequirePermission]` on the whole controller, `src/NT.QAMS.WebApi/Controllers/EquipmentController.cs:65`. |
| **Test Data** | Register body `{"name":"Analytical balance AB-204","serialNumber":"AB204-77321","location":"Weighing room","calibrationIntervalDays":365,"gracePeriodDays":30,"branchId":null,"departmentId":null}`; retire: `POST /api/equipment/{equipId}/retire`, no body. |
| **Steps** | 1. Log in as `analyst@demo-lab.local` and `POST /api/equipment` with the register body; capture `{equipId}`. 2. Log in as `qm@demo-lab.local`. 3. `POST /api/equipment/{equipId}/retire`. 4. Read the status. 5. `SELECT status FROM qams.equipment_item WHERE tenant_id='{demoLabTenantId}' AND id='{equipId}';`. |
| **Expected UI** | The *Retire* control renders for the QM on the equipment detail view. |
| **Expected API** | Step 1 → `201 Created` with `Location`. Step 3 → `204 No Content` (`EquipmentController.cs:69`). |
| **Expected DB** | `qams.equipment_item.status` for `{equipId}` is the retired value; `xmin` has advanced. |
| **Expected Audit** | One `audit.audit_trail` append for the retirement, chained. |
| **Expected Notification** | n/a — retirement raises no seeded rule in F1. Note that `ScheduledSweepService` proposes *calibration-due* transitions on a 1-hour interval; a retired item must not appear in a later proposal, but that is an escalation case, not this one. |
| **Cleanup** | None — retirement is terminal by design. |
| **Evidence** | Two HTTP captures · SQL status read |
| **Result / Defect** | Not Run · — |
| **Notes** | `equipment.void` is the single gated key of a six-key module: `equipment.view/create/edit/approve/export` reach no `[RequirePermission]` anywhere (GAP-RBAC-003). TC-RBAC-API-047 is the case that makes that visible. |

---

#### TC-RBAC-API-046 — `equipment.void` refuses the Department Head  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · **Branch coverage** of `RequirePermissionAttribute.OnAuthorizationAsync` — this case drives the third and last branch (`authenticated ∧ ¬Has(key)` → write problem + `EmptyResult`), `RequirePermissionAttribute.cs:54-59` |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `equipment.void` **not** granted (`SystemRoleCatalog.cs:136` — View/Create/Edit/Export) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1 and an **active** equipment item `{equipId2}` registered by `dh@demo-lab.local`. |
| **Test Data** | `POST /api/equipment/{equipId2}/retire`, no body, bearer token for `dh@demo-lab.local`. |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/equipment/{equipId2}/calibrations` body `{"performedAt":"2026-07-25T09:00:00Z","provider":"NatCal Ltd","result":"Pass","certificateFileId":null}` — expect `204`, proving the role can operate the instrument record. 3. `POST /api/equipment/{equipId2}/retire`. 4. Read status, `Content-Type`, `code`. 5. `SELECT status FROM qams.equipment_item WHERE tenant_id='{demoLabTenantId}' AND id='{equipId2}';`. |
| **Expected UI** | Calibration logging is offered; *Retire* is not. |
| **Expected API** | Step 2 → `204 No Content` (route ungated, `EquipmentController.cs:37`). Step 3 → `403` `application/problem+json`, `code` = `AUTHZ-403`. |
| **Expected DB** | The item's status is unchanged (still active); the calibration from step 2 **is** persisted, so the response body of the retire attempt is the only thing missing. |
| **Expected Audit** | One append for the calibration; none for the refused retirement. |
| **Expected Notification** | n/a. |
| **Cleanup** | `POST /api/equipment/{equipId2}/retire` as `qm@demo-lab.local`. |
| **Evidence** | Both HTTP captures · SQL status read · a note that step 2 and step 3 differ only in the presence of the gate |
| **Result / Defect** | Not Run · — |
| **Notes** | Steps 2 and 3 as a pair are the honest picture of this controller: the destructive act is gated, the routine acts are not. |

---

#### TC-RBAC-API-047 — An ungated Resources write executes for any internal actor, so `equipment.create` is inert  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · **no URS covers this** — trace to `src/NT.QAMS.WebApi/Controllers/EquipmentController.cs:27-35` and `PermissionCatalog.cs:159`; feeds **GAP-RBAC-003** · RSK-RBAC-012 (minted) |
| **Level / Type / Technique** | API · Functional (adverse-finding) · **Error Guessing** — "the privilege screen shows a switch that changes nothing" |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Bespoke tenant-defined role `Resources Reader` holding exactly `equipment.view` — and **not** `equipment.create` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. As `admin@demo-lab.local`, create `Resources Reader` with `permissionKeys: ["equipment.view"]` and assign it to a seventh account `reader@demo-lab.local` whose **tier** is `Analyst` (so the command-layer `[RequireInternalActor]` on `RegisterEquipmentCommand`, `src/NT.QAMS.Application/Equipment/EquipmentSlice.cs:12-13`, admits it). |
| **Test Data** | `POST /api/equipment` body `{"name":"Inert-key probe pipette","serialNumber":"PP-000-B47","location":"Prep bench","calibrationIntervalDays":180,"gracePeriodDays":14,"branchId":null,"departmentId":null}`. |
| **Steps** | 1. Log in as `reader@demo-lab.local`. 2. `GET /api/auth/me/privileges`; assert the key list is exactly `["equipment.view"]`. 3. `POST /api/equipment` with the body above. 4. Read the status. 5. `SELECT id, name FROM qams.equipment_item WHERE tenant_id='{demoLabTenantId}' AND serial_number='PP-000-B47';`. 6. As `admin@demo-lab.local`, `PUT /api/roles/{resourcesReaderRoleId}/permissions` `{"permissionKeys":[],"reason":"Batch-B inert-key probe: revoke everything"}`. 7. Repeat step 3 with a second serial `PP-000-B48`. |
| **Expected UI** | The SPA hides the *Register equipment* control for this role (`can('equipment.create')` false), so this defect is invisible through the UI and reachable only by direct API call. |
| **Expected API** | Step 3 → **`201 Created`** — the write succeeds despite the role holding no create privilege. Step 7 → **`201 Created` again**, with the role holding **zero** keys. |
| **Expected DB** | Two rows exist in `qams.equipment_item` for this tenant with serials `PP-000-B47` and `PP-000-B48`, both created by a role with no create grant. |
| **Expected Audit** | Two `audit.audit_trail` appends for the two registrations — the ledger records the writes as legitimate, because they were. |
| **Expected Notification** | n/a. |
| **Cleanup** | `POST /api/equipment/{id}/retire` on both items as `qm@demo-lab.local`; deactivate the `Resources Reader` role. |
| **Evidence** | Both `201` captures · the privileges body showing zero keys at step 7 · the two SQL rows |
| **Result / Defect** | Not Run · — |
| **Notes** | **`[ID]` and deliberately a passing case.** Writing this as a failing expectation would be dishonest — the build behaves exactly as coded. What it evidences is that `equipment.create` is one of the 92-plus catalogue keys with no enforcement point, so an access review certifying "this role may not register equipment" certifies something the system does not honour. Step 7 (zero keys, still `201`) is the assertion that makes the finding unarguable. |

---

#### TC-RBAC-API-048 — `reference-standards.create` refuses the Analyst  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning — the seeded Analyst's "read-only on this module" partition (`reference-standards.view` + `.export` only, `SystemRoleCatalog.cs:167`) |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · `reference-standards.create` **not** granted · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. Gate under test: `src/NT.QAMS.WebApi/Controllers/ReferenceStandardsController.cs:26`. |
| **Test Data** | `POST /api/reference-standards` body `{"name":"NIST SRM 1640a","type":"CRM","traceableTo":"NIST","manufacturer":"NIST","lotNumber":"1640a-07","certificateNumber":"C-1640a-2026","certifiedValue":"Trace elements, certified","uncertaintyStatement":"k=2","receivedOn":"2026-07-10","expiresOn":"2029-07-10","branchId":null,"departmentId":null}` (`RegisterReferenceStandardRequest`, `Contracts/Resources/ResourceContracts.cs:40-44`). |
| **Steps** | 1. Log in as `analyst@demo-lab.local`. 2. `GET /api/reference-standards` — expect `200` (ungated GET, `ReferenceStandardsController.cs:17`). 3. `POST /api/reference-standards` with the body above. 4. Read status, `Content-Type`, `code`. 5. `SELECT count(*) FROM qams.reference_standard WHERE tenant_id='{demoLabTenantId}' AND certificate_number='C-1640a-2026';`. |
| **Expected UI** | The register lists standards for the Analyst; the *Register standard* control is hidden. |
| **Expected API** | Step 2 → `200 OK`. Step 3 → `403` `application/problem+json`, `code` = `AUTHZ-403`. |
| **Expected DB** | The `count(*)` at step 5 is `0`. |
| **Expected Audit** | No append. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Two HTTP captures · the zero count |
| **Result / Defect** | Not Run · — |
| **Notes** | Unlike `equipment`, this module gates create/edit/approve/void — four of six keys reach a filter. Contrast this case with TC-RBAC-API-047 in the batch summary: two sibling Resources modules with materially different enforcement coverage. |

---

#### TC-RBAC-API-049 — `reference-standards.edit` admits the Department Head to quarantine a standard  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-011 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — DT-1 row 3 |
| **Priority / Severity / Automation** | Medium · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `reference-standards.edit` (granted — `SystemRoleCatalog.cs:137` View/Create/Edit/Export) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1 and an active standard `{stdId}` registered by `dh@demo-lab.local` (DH holds `reference-standards.create`). Gate under test: `ReferenceStandardsController.cs:38`. |
| **Test Data** | `POST /api/reference-standards/{stdId}/quarantine` body `{"reason":"Certificate expiry within 30 days; pending recertification."}` (`QuarantineReferenceStandardRequest(string Reason)`, `ResourceContracts.cs:46`). |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/reference-standards` with a valid body; capture `{stdId}`; expect `201`. 3. `POST /api/reference-standards/{stdId}/quarantine` with the body above. 4. Read the status. 5. `SELECT status FROM qams.reference_standard WHERE tenant_id='{demoLabTenantId}' AND id='{stdId}';`. |
| **Expected UI** | Both *Register* and *Quarantine* controls render for the DH. |
| **Expected API** | Step 2 → `201 Created`. Step 3 → `204 No Content`. |
| **Expected DB** | `qams.reference_standard.status` for `{stdId}` is the quarantined value. |
| **Expected Audit** | Two appends (registered, quarantined), chained. |
| **Expected Notification** | n/a. |
| **Cleanup** | Leave the standard quarantined — TC-RBAC-API-050 needs exactly this state. |
| **Evidence** | Two HTTP captures · SQL status read |
| **Result / Defect** | Not Run · — |
| **Notes** | The reason string is a domain field on the command, not the `X-Change-Reason` header — `ChangeReasonMiddleware` demands that header on **DELETE** only, and this is a POST. |

---

#### TC-RBAC-API-050 — `reference-standards.approve` refuses the same Department Head on reactivation  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · **BVA over the action ordinal** — the DH's grant set stops exactly at `Edit` (`PermissionAction.Edit = 2`, `PermissionCatalog.cs:18`); this case sits one action beyond the boundary, at `Approve = 3` |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `reference-standards.approve` **not** granted · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1 and the **quarantined** `{stdId}` left by TC-RBAC-API-049. Gate under test: `ReferenceStandardsController.cs:46`. |
| **Test Data** | `POST /api/reference-standards/{stdId}/reactivate`, no body, bearer token for `dh@demo-lab.local`; then the same call as `qm@demo-lab.local`. |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/reference-standards/{stdId}/reactivate`. 3. Read status and `code`. 4. `SELECT status FROM qams.reference_standard WHERE tenant_id='{demoLabTenantId}' AND id='{stdId}';` — expect it still quarantined. 5. Log in as `qm@demo-lab.local` and repeat the call. 6. Re-read the status column. |
| **Expected UI** | *Reactivate* is hidden for the DH and shown for the QM. |
| **Expected API** | Step 2 → `403` `application/problem+json`, `code` = `AUTHZ-403`. Step 5 → `204 No Content`. |
| **Expected DB** | After step 4 the status is still quarantined; after step 6 it is active. |
| **Expected Audit** | Exactly one append across the whole case — from step 5 only. |
| **Expected Notification** | n/a. |
| **Cleanup** | `POST /api/reference-standards/{stdId}/retire` as `qm@demo-lab.local`. |
| **Evidence** | Both HTTP captures · the two SQL status reads bracketing the boundary |
| **Result / Defect** | Not Run · — |
| **Notes** | Two roles, one route, one state: the cleanest available demonstration that the gate — not the record state — is what refuses. The record was reactivatable throughout; only the actor changed. |

---

#### TC-RBAC-API-051 — `monitoring-points.create` refuses the Analyst  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning — Analyst read-only partition on this module (`monitoring-points.view` + `.export`, `SystemRoleCatalog.cs:168`) |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · `monitoring-points.create` **not** granted · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. Gate under test: `src/NT.QAMS.WebApi/Controllers/MonitoringPointsController.cs:26`. |
| **Test Data** | `POST /api/monitoring-points` body `{"name":"Cold room CR-2","location":"Cold room","parameter":"Temperature","unit":"degC","lowLimit":2.0,"highLimit":8.0,"branchId":null,"departmentId":null}` (`RegisterMonitoringPointRequest`, `Contracts/Facility/FacilityContracts.cs:5-7`). |
| **Steps** | 1. Log in as `analyst@demo-lab.local`. 2. `GET /api/monitoring-points` — expect `200` (ungated GET, `:17`). 3. `POST /api/monitoring-points` with the body above. 4. Read status and `code`. 5. On an **existing** point `{mpId}`, `POST /api/monitoring-points/{mpId}/readings` body `{"value":5.4,"remark":"Routine"}` — expect `200`, because that route is **ungated** (`:43`). 6. `SELECT count(*) FROM qams.monitoring_point WHERE tenant_id='{demoLabTenantId}' AND name='Cold room CR-2';`. |
| **Expected UI** | The Analyst can record readings but is offered no *Register point* control. |
| **Expected API** | Step 2 → `200`. Step 3 → `403` `application/problem+json`, `code` = `AUTHZ-403`. Step 5 → `200 OK` body `{ "readingId": "<guid>" }`. |
| **Expected DB** | Step 6 returns `0`. A new row **does** exist in `qams.environmental_reading` for `{mpId}` from step 5. |
| **Expected Audit** | One append for the reading; none for the refused registration. |
| **Expected Notification** | If the recorded value 5.4 sits inside the point's limits, no excursion policy fires. Choose a value inside limits deliberately — an excursion would raise a nonconformance through `ExcursionToNcPolicy` and confound the case. |
| **Cleanup** | None. |
| **Evidence** | Three HTTP captures · the zero count · the reading row |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 5 is the honest counterweight: routine data capture is deliberately open to any internal actor, so the absence of a gate there is a design choice, not the GAP-RBAC-003 defect. Say so in the execution record. |

---

#### TC-RBAC-API-052 — `monitoring-points.edit` admits the Department Head across all three edit routes  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-011 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · **Pairwise** — one key (`monitoring-points.edit`) × three routes (`/limits`, `/suspend`, `/resume`), all gated identically at `MonitoringPointsController.cs:36,48,56` |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `monitoring-points.edit` (granted, `SystemRoleCatalog.cs:138`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1 and an **active** monitoring point `{mpId2}` registered by `dh@demo-lab.local`. |
| **Test Data** | Limits body `{"lowLimit":2.0,"highLimit":8.0}` (`SetMonitoringLimitsRequest(decimal? LowLimit, decimal? HighLimit)`, `FacilityContracts.cs:9`); suspend and resume take no body. |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/monitoring-points/{mpId2}/limits` with the limits body. 3. `POST /api/monitoring-points/{mpId2}/suspend`. 4. `POST /api/monitoring-points/{mpId2}/resume`. 5. Record all three statuses. 6. `SELECT status, low_limit, high_limit FROM qams.monitoring_point WHERE tenant_id='{demoLabTenantId}' AND id='{mpId2}';`. |
| **Expected UI** | All three controls render for the DH on the point detail view. |
| **Expected API** | Steps 2, 3 and 4 each → `204 No Content`. |
| **Expected DB** | `low_limit` = 2.0, `high_limit` = 8.0; the status ends **active** again after the resume — the suspend/resume pair is state-neutral. |
| **Expected Audit** | Three `audit.audit_trail` appends in the executed order, each chained to the previous. |
| **Expected Notification** | n/a — limit changes and suspensions raise no seeded rule in F1. |
| **Cleanup** | None — the point is left in its original active state with the stated limits. |
| **Evidence** | Three HTTP captures · SQL row showing both limits and the final status |
| **Result / Defect** | Not Run · — |
| **Notes** | One key governs a configuration change (`limits`) and two state transitions (`suspend`, `resume`). That grouping is worth stating in the access review: granting `monitoring-points.edit` for limit maintenance also grants the power to suspend monitoring of a controlled environment. |

---

#### TC-RBAC-API-053 — `monitoring-points.void` refuses the Department Head on retirement  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · **BVA over the action ordinal** — DH's grants stop at `Edit = 2`; `Void = 4` (`PermissionCatalog.cs:24`) is beyond it, and `Approve = 3` is not gated on this controller, so `Void` is the first denied action reachable here |
| **Priority / Severity / Automation** | Medium · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `monitoring-points.void` **not** granted · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1 and the active point `{mpId2}` left by TC-RBAC-API-052. Gate under test: `MonitoringPointsController.cs:64`. |
| **Test Data** | `POST /api/monitoring-points/{mpId2}/retire`, no body, bearer token for `dh@demo-lab.local`. |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/monitoring-points/{mpId2}/suspend` — expect `204`, re-proving `edit` is held. 3. `POST /api/monitoring-points/{mpId2}/retire`. 4. Read status, `Content-Type`, `code`. 5. `SELECT status FROM qams.monitoring_point WHERE tenant_id='{demoLabTenantId}' AND id='{mpId2}';`. |
| **Expected UI** | *Suspend* renders; *Retire* does not. |
| **Expected API** | Step 2 → `204 No Content`. Step 3 → `403` `application/problem+json`, `code` = `AUTHZ-403`. |
| **Expected DB** | The status is the suspended value from step 2, **not** retired. |
| **Expected Audit** | One append (the suspension); none for the refusal. |
| **Expected Notification** | n/a. |
| **Cleanup** | `POST /api/monitoring-points/{mpId2}/resume` as `dh@demo-lab.local`. |
| **Evidence** | Both HTTP captures · SQL status read |
| **Result / Defect** | Not Run · — |
| **Notes** | Adjacent-action boundary: the same actor, on the same record, in the same request sequence, crosses from allowed to denied by one action ordinal. |

---

#### TC-RBAC-API-054 — `suppliers.approve` refuses the Analyst on both routes it gates  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · **Pairwise** — one key (`suppliers.approve`) × two semantically different routes (`/{id}/approve` and `/{id}/evaluations`), both at `GovernanceControllers.cs:200,220` |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · `suppliers.approve` **not** granted (`SystemRoleCatalog.cs:169` — View/Create/Edit/Export) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1 and a supplier `{supId}` registered by `POST /api/suppliers` (ungated) as `analyst@demo-lab.local`. |
| **Test Data** | Register body `{"name":"Acme Reagents Ltd","supplierType":"Reagents","branchId":null,"departmentId":null}`; evaluation body `{"periodStart":"2026-01-01","periodEnd":"2026-06-30","criteria":[{"criterion":"On-time delivery","weight":0.5,"score":4.0},{"criterion":"Certificate completeness","weight":0.5,"score":5.0}]}` (`RecordEvaluationRequest` / `EvaluationCriterionRequest`, `GovernanceContracts.cs:59-61`). |
| **Steps** | 1. Log in as `analyst@demo-lab.local`. 2. `POST /api/suppliers` with the register body; capture `{supId}`; expect `201`. 3. `POST /api/suppliers/{supId}/approve`. 4. `POST /api/suppliers/{supId}/evaluations` with the evaluation body. 5. Record both statuses and `code` values. 6. `SELECT status FROM qams.supplier WHERE tenant_id='{demoLabTenantId}' AND id='{supId}';` and `SELECT count(*) FROM qams.supplier_evaluation WHERE tenant_id='{demoLabTenantId}' AND supplier_id='{supId}';`. |
| **Expected UI** | The Analyst may register a supplier and add certificates but sees neither *Approve* nor *Record evaluation*. |
| **Expected API** | Step 2 → `201 Created`. Steps 3 and 4 → `403` `application/problem+json`, `code` = `AUTHZ-403` on both. |
| **Expected DB** | The supplier's status is the as-registered value (not approved); `count(*)` on `qams.supplier_evaluation` for `{supId}` is `0`. |
| **Expected Audit** | One append from step 2; none from steps 3 and 4. |
| **Expected Notification** | n/a. |
| **Cleanup** | `POST /api/suppliers/{supId}/suspend` as `qm@demo-lab.local` with `{"reason":"Batch-B fixture teardown."}`. |
| **Evidence** | Three HTTP captures · both SQL results |
| **Result / Defect** | Not Run · — |
| **Notes** | `suppliers.approve` gating *evaluation recording* as well as approval is the notable mapping: a periodic performance evaluation is an assessment, not an approval, yet it needs the approval key. State it in the access review; it is not a defect, but it is not derivable from the module matrix either. |

---

#### TC-RBAC-API-055 — `suppliers.void` admits the Quality Manager to suspend a supplier  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-011 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — DT-1 row 3, Resources group Void action |
| **Priority / Severity / Automation** | Medium · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `suppliers.void` (granted) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1 and an approved supplier `{supId2}` (registered by the Analyst, approved by `qm@demo-lab.local` — the QM is not the registrant, so `SOD-SUP-001` at `Supplier.cs:91` does not fire). Gate under test: `GovernanceControllers.cs:208`. |
| **Test Data** | `POST /api/suppliers/{supId2}/suspend` body `{"reason":"Two consecutive out-of-specification reagent lots."}` (`SuspendSupplierRequest(string Reason)`, `GovernanceContracts.cs:58`). |
| **Steps** | 1. Log in as `qm@demo-lab.local`. 2. `POST /api/suppliers/{supId2}/approve` — expect `204`. 3. `POST /api/suppliers/{supId2}/suspend` with the body above. 4. Read both statuses. 5. `SELECT status FROM qams.supplier WHERE tenant_id='{demoLabTenantId}' AND id='{supId2}';`. |
| **Expected UI** | Both *Approve* and *Suspend* render for the QM. |
| **Expected API** | Steps 2 and 3 each → `204 No Content`. |
| **Expected DB** | `qams.supplier.status` for `{supId2}` is the suspended value. |
| **Expected Audit** | Two appends (approved, suspended), chained. |
| **Expected Notification** | n/a for the manual suspension. Note that `ScheduledSweepService` independently *proposes* supplier suspensions on its 1-hour cycle and is idempotent by construction, so a subsequent proposal on an already-suspended supplier must be a no-op — verify no duplicate suspension append appears within 90 minutes of this case. |
| **Cleanup** | None — a suspended supplier is a valid resting state. |
| **Evidence** | Two HTTP captures · SQL status read · a ledger re-read at T+90 min confirming no sweep-generated duplicate |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 2 must be run by the QM and not by the registrant; otherwise the case fails `422 SOD-SUP-001` and proves nothing about `suppliers.void`. |

---

#### TC-RBAC-API-056 — `competencies.create` admits the Department Head to assign a competency  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-011 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — DT-1 row 3, People group |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `competencies.create` (granted, `SystemRoleCatalog.cs:140`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. Trainee is `analyst@demo-lab.local`, whose `user_account.id` is `{analystUserId}` — deliberately **not** the DH, so `SOD-COMP-001` (`CompetencyRecord.cs:91,108`) has no bearing on the later steps. Gate under test: `src/NT.QAMS.WebApi/Controllers/CompetenciesController.cs:28`. |
| **Test Data** | `POST /api/competencies` body `{"traineeId":"{analystUserId}","subject":"Micropipette calibration verification","documentId":null,"validityMonths":24}` (`AssignCompetencyRequest`, `ResourceContracts.cs:61-62`). |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `GET /api/auth/me/privileges`; assert `competencies.create` and `competencies.edit` present, `competencies.approve` and `competencies.void` absent. 3. `POST /api/competencies` with the body above; capture `{compId}`. 4. Read the status. 5. `SELECT trainee_id, subject FROM qams.competency_record WHERE tenant_id='{demoLabTenantId}' AND id='{compId}';`. |
| **Expected UI** | The *Assign competency* control renders for the DH. |
| **Expected API** | Step 3 → `201 Created` with a `Location` header pointing at `GET /api/competencies/{compId}` (`CompetenciesController.cs:33`). |
| **Expected DB** | The `qams.competency_record` row exists with `trainee_id` = `{analystUserId}` and the given subject. |
| **Expected Audit** | One `audit.audit_trail` append for the assignment, chained. |
| **Expected Notification** | n/a — no seeded rule fires on competency assignment in F1. |
| **Cleanup** | Leave `{compId}` in place — TC-RBAC-API-057 and TC-RBAC-API-058 consume it. |
| **Evidence** | HTTP capture with the `Location` header · privileges body · SQL row |
| **Result / Defect** | Not Run · — |
| **Notes** | `PassMark = 80` (`CompetencyRecord.cs:33`) governs the *assessment outcome*, not the permission gate; do not conflate them. This case asserts only the gate. |

---

#### TC-RBAC-API-057 — `competencies.approve` refuses the Department Head on authorization  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · **BVA over the action ordinal** — DH's competency grants stop at `Edit = 2`; this case is `Approve = 3`, the first denied ordinal |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `competencies.approve` **not** granted · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1 and `{compId}` from TC-RBAC-API-056. Gate under test: `CompetenciesController.cs:45`. |
| **Test Data** | `POST /api/competencies/{compId}/authorize`, no body, bearer token for `dh@demo-lab.local`; then the same as `qm@demo-lab.local`. |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/competencies/{compId}/assessments` body `{"score":85}` — expect `204`, proving `competencies.edit` is held and clearing the pass mark of 80. 3. `POST /api/competencies/{compId}/authorize`. 4. Read status, `Content-Type`, `code`. 5. `SELECT status FROM qams.competency_record WHERE tenant_id='{demoLabTenantId}' AND id='{compId}';`. 6. Log in as `qm@demo-lab.local` and repeat step 3. 7. Re-read the status. |
| **Expected UI** | *Score assessment* renders for the DH; *Authorize* does not. Both render for the QM. |
| **Expected API** | Step 2 → `204 No Content`. Step 3 → `403` `application/problem+json`, `code` = `AUTHZ-403`. Step 6 → `204 No Content`. |
| **Expected DB** | After step 5 the record is assessed but **not** authorized; after step 7 it is authorized. |
| **Expected Audit** | Two appends across the case — the assessment (step 2) and the authorization (step 6). Step 3 appends nothing. |
| **Expected Notification** | n/a in F1; competency-expiry proposals are raised later by `ScheduledSweepService`, outside this case's window. |
| **Cleanup** | `POST /api/competencies/{compId}/revoke` as `qm@demo-lab.local` with `{"reason":"Batch-B fixture teardown."}`. |
| **Evidence** | Three HTTP captures · both SQL status reads · the two ledger rows |
| **Result / Defect** | Not Run · — |
| **Notes** | The DH assessed the trainee (score 85, above the pass mark) and still cannot authorize — the gate, not the score, is what refuses at step 3. Keep the score above 80 so a `422` pass-mark failure cannot be mistaken for the `403`. |

---

#### TC-RBAC-API-058 — `competencies.edit` refuses the Analyst on scoring an assessment  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning — Analyst read-only partition on `competencies` (`view` + `export` only, `SystemRoleCatalog.cs:170`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · `competencies.edit` **not** granted · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1 and a competency record `{compId3}` assigned to `dh@demo-lab.local` by `qm@demo-lab.local` — so the Analyst is neither trainee nor assessor and `SOD-COMP-001` is irrelevant. Gate under test: `CompetenciesController.cs:37`. |
| **Test Data** | `POST /api/competencies/{compId3}/assessments` body `{"score":95}` (`ScoreAssessmentRequest(int Score)`, `ResourceContracts.cs:64`). |
| **Steps** | 1. Log in as `analyst@demo-lab.local`. 2. `GET /api/competencies/{compId3}` — expect `200` (ungated GET, `CompetenciesController.cs:23`). 3. `POST /api/competencies/{compId3}/assessments` with the body above. 4. Read status, `Content-Type`, `code`. 5. `SELECT count(*) FROM qams.competency_record WHERE tenant_id='{demoLabTenantId}' AND id='{compId3}';` and confirm no assessment row was appended to the record's owned assessment collection. |
| **Expected UI** | The Analyst can read the competency record but is offered no *Score* control. |
| **Expected API** | Step 2 → `200 OK`. Step 3 → `403` `application/problem+json`, `code` = `AUTHZ-403`. |
| **Expected DB** | The competency record carries no assessment with score 95; its `xmin` is unchanged from the pre-call read. |
| **Expected Audit** | No append. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Two HTTP captures · the unchanged `xmin` |
| **Result / Defect** | Not Run · — |
| **Notes** | An unqualified analyst scoring their colleague's competence is one of the highest-consequence over-permissions in the People group; hence Critical severity despite the modest module. Assert `AUTHZ-403`, not a `SOD-*` code — segregation of duties is a different control and is batch E's scope. |

---

#### TC-RBAC-API-059 — `training.create` refuses the Analyst, while the ungated completion route admits them  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 and RSK-RBAC-012 (minted) |
| **Level / Type / Technique** | API · Functional (mixed positive/negative) · **Condition coverage** — on one controller, one route evaluates the gate condition and the sibling route has no condition at all (`TrainingAssignmentsController`, `CompetenciesController.cs:73-74` gated vs `:82-87` ungated) |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · `training.create` **not** granted; the seeded Analyst holds `training.view`, `training.edit`, `training.export` (`SystemRoleCatalog.cs:171`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1 and one open training assignment `{trainId}` created by `dh@demo-lab.local` for trainee `{analystUserId}`. |
| **Test Data** | Assign body `{"traineeId":"{analystUserId}","subject":"Revised SOP QM-001 read-and-understand","documentId":null,"dueDate":"2026-08-31"}` (`AssignTrainingRequest`, `ResourceContracts.cs:97-98`). |
| **Steps** | 1. Log in as `analyst@demo-lab.local`. 2. `GET /api/training-assignments?traineeId={analystUserId}` — expect `200` (ungated). 3. `POST /api/training-assignments` with the assign body. 4. Read status and `code`. 5. `POST /api/training-assignments/{trainId}/complete`. 6. Read that status. 7. `SELECT count(*) FROM qams.training_assignment WHERE tenant_id='{demoLabTenantId}' AND subject='Revised SOP QM-001 read-and-understand';`. |
| **Expected UI** | The Analyst's training queue renders with a *Mark complete* action on their own assignment and no *Assign training* control. |
| **Expected API** | Step 2 → `200 OK`. Step 3 → `403` `application/problem+json`, `code` = `AUTHZ-403`. Step 5 → `204 No Content`. |
| **Expected DB** | Step 7 returns `0` — the assignment the Analyst tried to create does not exist. The row for `{trainId}` is marked complete. |
| **Expected Audit** | One append (the completion); none for the refused assignment. |
| **Expected Notification** | n/a in F1. |
| **Cleanup** | None — a completed assignment is the intended end state. |
| **Evidence** | Three HTTP captures · the zero count · the completed row |
| **Result / Defect** | Not Run · — |
| **Notes** | Self-completion of one's own training is intentionally open (`CompleteTrainingCommand` carries only `[RequireInternalActor]`, `src/NT.QAMS.Application/Competency/CompetencySlice.cs:118-119`); record that as designed, not as an instance of GAP-RBAC-003. What *is* worth noting is that the route does not verify the caller is the trainee — a scope question for exploratory charter TC-RBAC-EXPL-004, not for this case. |

---

#### TC-RBAC-API-060 — `test-authorizations.create` admits the Department Head to grant a test authorization  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-011 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — the personnel-authorization grant, ISO 17025 §6.2.6 |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `test-authorizations.create` (granted, `SystemRoleCatalog.cs:142`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1; an authorized competency record `{compId4}` for `{analystUserId}`; a test-catalog item `{testItemId}` created by `POST /api/test-catalog` as `qm@demo-lab.local`. Grantee is the Analyst and grantor is the DH, so `SOD-AUTHZ-001` (`TestAuthorization.cs:43`) does not fire. Gate under test: `src/NT.QAMS.WebApi/Controllers/TestAuthorizationsController.cs:27`. |
| **Test Data** | `POST /api/test-authorizations` body `{"userId":"{analystUserId}","testCatalogItemId":"{testItemId}","competencyRecordId":"{compId4}","scope":"Perform"}` (`GrantTestAuthorizationRequest`, `ResourceContracts.cs:80-81`). |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/test-authorizations` with the body above; capture `{taId}`. 3. Read the status. 4. `SELECT user_id, scope, status FROM qams.test_authorization WHERE tenant_id='{demoLabTenantId}' AND id='{taId}';`. |
| **Expected UI** | The *Grant authorization* control renders for the DH on the authorization matrix. |
| **Expected API** | Step 2 → `201 Created` with a `Location` header (`TestAuthorizationsController.cs:32`). |
| **Expected DB** | The `qams.test_authorization` row exists with `user_id` = `{analystUserId}`, `scope` = `Perform`, and an active status. |
| **Expected Audit** | One `audit.audit_trail` append, chained. |
| **Expected Notification** | n/a in F1. |
| **Cleanup** | Leave `{taId}` active — TC-RBAC-API-061 needs it. |
| **Evidence** | HTTP capture with `Location` · SQL row |
| **Result / Defect** | Not Run · — |
| **Notes** | Beware the `AUTHZ-*` prefix collision here: this aggregate raises `AUTHZ-001` … `AUTHZ-005` and `AUTHZ-010` … `AUTHZ-015` as **business-rule** codes that also surface as 403 (GAP-RBAC-010). A `403 AUTHZ-001` from this route means "the expiry must fall after the grant date" (`TestAuthorization.cs:48`), **not** an authorization refusal. Only `AUTHZ-403` proves a gate refusal. |

---

#### TC-RBAC-API-061 — `test-authorizations.approve` refuses the Department Head on reinstatement  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · **Pairwise** — one role (DH) × two adjacent actions on the same aggregate (`Edit` granted → suspend allowed; `Approve` not granted → reinstate denied), at `TestAuthorizationsController.cs:36` and `:44` |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Department Head · `test-authorizations.edit` granted, `test-authorizations.approve` **not** granted · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1 and the active `{taId}` from TC-RBAC-API-060. |
| **Test Data** | Suspend body `{"reason":"Competency re-verification pending."}` (`SuspendTestAuthorizationRequest(string Reason)`, `ResourceContracts.cs:83`); reinstate takes no body. |
| **Steps** | 1. Log in as `dh@demo-lab.local`. 2. `POST /api/test-authorizations/{taId}/suspend` with the suspend body. 3. `POST /api/test-authorizations/{taId}/reinstate`. 4. Record both statuses and the `code` of the second. 5. `SELECT status FROM qams.test_authorization WHERE tenant_id='{demoLabTenantId}' AND id='{taId}';`. 6. Log in as `qm@demo-lab.local` and repeat step 3. 7. Re-read the status. |
| **Expected UI** | *Suspend* renders for the DH; *Reinstate* does not. Both render for the QM. |
| **Expected API** | Step 2 → `204 No Content`. Step 3 → `403` `application/problem+json`, `code` = **`AUTHZ-403`** — distinguishable from the aggregate's own `AUTHZ-010`…`AUTHZ-015` state guards, which would also be 403 but carry a different code. Step 6 → `204 No Content`. |
| **Expected DB** | After step 5 the status is suspended; after step 7 it is active again. |
| **Expected Audit** | Two appends (suspended, reinstated); the refusal appends nothing. |
| **Expected Notification** | n/a in F1. |
| **Cleanup** | `POST /api/test-authorizations/{taId}/revoke` as `qm@demo-lab.local` with `{"reason":"Batch-B fixture teardown."}`. |
| **Evidence** | Three HTTP captures with the exact `code` of each · both SQL status reads |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the case where the GAP-RBAC-010 code collision bites hardest: the *same aggregate* answers 403 for two unrelated reasons. Assert the code string, never the status alone. |

---

#### TC-RBAC-API-062 — `users.view` refuses the Quality Manager the user roster  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-011 (minted — under-permission: the highest quality role cannot read the roster it is asked to recertify) |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning — the "module excluded wholesale from the role" partition (`SystemRoleCatalog.cs:111` returns false for `PermissionCatalog.Users` and `PermissionCatalog.TenantSettings`) |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `users.view` **not** granted · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. Gate under test: `src/NT.QAMS.WebApi/Controllers/UsersController.cs:23`. |
| **Test Data** | `GET /api/users`, bearer token for `qm@demo-lab.local`; then the same call as `admin@demo-lab.local`. |
| **Steps** | 1. Log in as `qm@demo-lab.local`. 2. `GET /api/auth/me/privileges`; assert **no** key beginning `users.` appears and `roles.view` **does** (`SystemRoleCatalog.cs:112`). 3. `GET /api/users`. 4. Read status, `Content-Type`, `code`. 5. Log in as `admin@demo-lab.local` and repeat step 3. |
| **Expected UI** | The Users screen is navigable for the QM (no route guard, GAP-RBAC-007) and renders an empty list with the refusal surfaced by the error interceptor. |
| **Expected API** | Step 3 → `403` `application/problem+json`, `code` = `AUTHZ-403`. Step 5 → `200 OK` listing at least the seven fixture accounts. |
| **Expected DB** | No writes. `qams.user_account` is outside RLS (accepted deviation B9), so the refusal is an application-layer decision only — confirm the row count is unaffected. |
| **Expected Audit** | No append for either call. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Both HTTP captures · privileges body showing `roles.view` present and `users.view` absent |
| **Result / Defect** | Not Run · — |
| **Notes** | The seeded QM can *read* the privilege configuration (`roles.view`) but cannot *see who holds it* (`users.view` withheld). That combination makes a QM-led access review impossible without over-privileging, and is worth raising with the Quality Manager during UAT even though it is a faithful reproduction of the pre-v1.51.0 tier behaviour. Not a code defect — a configuration-design observation. |

---

#### TC-RBAC-API-063 — `users.manage` refuses the Quality Manager and admits the Tenant Administrator  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (mixed) · **Pairwise** — one key (`users.manage`) × two roles (QM denied, TA allowed) × two of the eight routes it gates (`POST /api/users`, `PUT /api/users/{id}/scope`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (denied) and Tenant Administrator (allowed) · `users.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. Gates under test: `UsersController.cs:28` (register) and `:52` (scope) — both `[RequirePermission(PermissionCatalog.Users, PermissionAction.Manage)]`. |
| **Test Data** | Register body `{"email":"probe-b063@demo-lab.local","displayName":"Batch B Probe","role":"Analyst","initialPassword":"Demo-Role-Pass-3!","roleId":null}` (`RegisterUserRequest`, `Contracts/IdentityAccess/UserContracts.cs:17-18`); scope body `{"branchIds":[],"departmentIds":[]}` (`SetUserScopeRequest`, `Contracts/IdentityAccess/AuthorizationContracts.cs:58-59`). |
| **Steps** | 1. Log in as `qm@demo-lab.local`. 2. `POST /api/users` with the register body. 3. `PUT /api/users/{analystUserId}/scope` with the scope body. 4. Record both statuses and `code` values. 5. `SELECT count(*) FROM qams.user_account WHERE email='probe-b063@demo-lab.local';`. 6. Log in as `admin@demo-lab.local` and repeat steps 2 and 3. 7. Re-run the count. |
| **Expected UI** | The Users screen offers no *Add user* or *Set scope* control to the QM; both render for the TA. |
| **Expected API** | Steps 2 and 3 → `403` `application/problem+json`, `code` = `AUTHZ-403` on both. Step 6's register → `200 OK` body `{ "id": … }` (`UsersController.cs:29-31` returns `Ok`, **not** `201`); step 6's scope → `204 No Content`. |
| **Expected DB** | Count at step 5 is `0`; count at step 7 is `1`. `qams.user_branch_access` and `qams.user_department_access` hold no rows for `{analystUserId}` (empty lists mean unrestricted). |
| **Expected Audit** | Nothing from steps 2–3; from step 6, a `UserRoleAssigned`-derived append and a `UserScopeChanged`-derived append, both carrying the owning tenant id (the v1.51.1 RP-D1 fix — pinned by `tests/NT.QAMS.Application.UnitTests/Authorization/UserEventTenantStampTests.cs`). Assert `tenant_id` is **not** null on both. |
| **Expected Notification** | n/a — account creation raises no seeded notification in F1. |
| **Cleanup** | `POST /api/users/{probeUserId}/deactivate` as `admin@demo-lab.local`; accounts are never deleted. |
| **Evidence** | Four HTTP captures · both counts · the two ledger rows with their non-null `tenant_id` |
| **Result / Defect** | Not Run · — |
| **Notes** | The `tenant_id`-not-null assertion is the RP-D1 regression guard riding along on a permission case; it costs one SQL predicate and catches a defect that was invisible for a release. Do **not** exercise `POST /api/users/{id}/role` or `POST /api/users/{id}/deactivate` on the last `roles.manage` holder here — those are GAP-RBAC-012/013 and belong to batch E. |

---

#### TC-RBAC-API-064 — `users.view` alone grants the roster and nothing else  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 (minted) |
| **Level / Type / Technique** | API · Functional (mixed) · **BVA on a two-action module** — `users` is a `ConfigurationModule` with exactly `View` and `Manage` (`PermissionCatalog.cs:125-126,168`); this case sits precisely on the single boundary the module has |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Bespoke tenant-defined role `User Auditor` holding exactly `users.view` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. As `admin@demo-lab.local`, create the role `User Auditor` with `permissionKeys: ["users.view"]` and assign it to an eighth account `usersro@demo-lab.local` (tier `Analyst`). |
| **Test Data** | `GET /api/users`; then each of the seven `users.manage` routes with minimal valid bodies: `POST /api/users`, `POST /api/users/{analystUserId}/role` `{"role":"Analyst"}`, `PUT /api/users/{analystUserId}/assigned-role` `{"roleId":"{analystRoleId}"}`, `PUT /api/users/{analystUserId}/scope` `{"branchIds":[],"departmentIds":[]}`, `PUT /api/users/{analystUserId}/language` `{"language":"en"}`, `POST /api/users/{analystUserId}/deactivate`, `POST /api/users/{analystUserId}/reset-password` `{"newPassword":"Demo-Role-Pass-4!"}`. |
| **Steps** | 1. Log in as `usersro@demo-lab.local`. 2. `GET /api/auth/me/privileges`; assert the key list is exactly `["users.view"]`. 3. `GET /api/users`. 4. Issue each of the seven manage-gated calls listed in Test Data. 5. Tabulate route → status → `code` for all eight. 6. `SELECT is_active, preferred_language FROM qams.user_account WHERE id='{analystUserId}';`. |
| **Expected UI** | The Users screen renders the roster read-only for this role; no row action is offered. |
| **Expected API** | Step 3 → `200 OK` listing the fixture accounts. All seven calls in step 4 → `403` `application/problem+json`, `code` = `AUTHZ-403`. |
| **Expected DB** | `{analystUserId}` is still active and its `preferred_language` is unchanged; no new `qams.user_account` row exists for the attempted registration. |
| **Expected Audit** | No appends from any of the eight calls — the read is not ledgered and the seven writes never reach a handler. |
| **Expected Notification** | n/a. |
| **Cleanup** | Reassign `usersro@demo-lab.local` to the seeded Analyst role and deactivate the `User Auditor` role. |
| **Evidence** | The eight-row status table · privileges body proving the single-key grant · SQL row showing the untouched account |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the case that proves the `users` module's coarse granularity is at least *consistently* enforced: one key opens all seven write routes and the other opens none. It is also the positive counter-evidence for GAP-RBAC-004 by analogy — unlike `tenant-settings.view`, `users.view` **is** independently useful. |

---

#### TC-RBAC-API-065 — External Auditor is refused a Risk-group write at the command layer with `AUTHZ-002`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · **URS-078** (every command carries a policy; the read-only ExternalAuditor shall not mutate) · RSK-RBAC-013 (minted) |
| **Level / Type / Technique** | API · Security (negative) · **Condition coverage** of `AuthorizationBehavior`'s `RequireInternalActorAttribute => role != UserRole.ExternalAuditor` (`src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs:75`) — this case drives the condition false |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | External Auditor · route is **ungated** at HTTP (`POST /api/risks`, `GovernanceControllers.cs:28`), so the refusal must come from the command policy `[RequireInternalActor]` on `AssessRiskCommand` (`src/NT.QAMS.Application/RiskGovernance/RiskGovernanceSlice.cs:22-23`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. The auditor's tier is `UserRole.ExternalAuditor = 5` (`src/NT.QAMS.Domain/IdentityAccess/UserAccount.cs:10`) and its JWT role claim matches the DB tier, so `ActiveSessionMiddleware` does not short-circuit with `401 AUTH-007`. |
| **Test Data** | `POST /api/risks` body `{"title":"Auditor write probe","category":"Operational","likelihood":4,"impact":4,"branchId":null,"departmentId":null}`. |
| **Steps** | 1. Log in as `auditor@demo-lab.local`. 2. `GET /api/risks` — expect `200`, proving the read half of the auditor contract. 3. `POST /api/risks` with the body above. 4. Read status, `Content-Type` and the exact `code`. 5. `SELECT count(*) FROM qams.risk_item WHERE tenant_id='{demoLabTenantId}' AND title='Auditor write probe';`. |
| **Expected UI** | The risk register renders read-only for the auditor; no *Raise risk* control. |
| **Expected API** | Step 2 → `200 OK`. Step 3 → `403 Forbidden`, `application/problem+json`, `code` = **`AUTHZ-002`** with title `Role 'ExternalAuditor' is not permitted to execute this action.` (`AuthorizationBehavior.cs:83-84`, mapped to 403 by the `AUTHZ-` prefix arm of `DomainExceptionHandler.cs:63-68`). **Not** `AUTHZ-403` — that code would mean an HTTP filter refused, and this route has no filter. |
| **Expected DB** | Step 5 returns `0`; nothing is written, and the outbox holds no event for this attempt. |
| **Expected Audit** | No `audit.audit_trail` append, no `audit.field_change` row, no `audit.security_event` row (GAP-RBAC-902). |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Both HTTP captures with the exact code strings · the zero count · a written note that the refusal came from the command layer, evidenced by the code being `AUTHZ-002` rather than `AUTHZ-403` |
| **Result / Defect** | Not Run · — |
| **Notes** | The `AUTHZ-002` vs `AUTHZ-403` distinction is the whole value of this case: it proves the defence-in-depth layer exists and fires **where the HTTP gate is absent**. Existing coverage (`WebApi.FunctionalTests/AuditorDenyMatrixTests`) probes nonconformances, documents and outlier screenings — the Risk, Resources and People groups are not in it, which is why 065–067 are authored. |

---

#### TC-RBAC-API-066 — External Auditor is refused a Resources-group write at the command layer  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-078 · RSK-RBAC-013 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — DT-2 row 7 (`Command` ∧ `RequireInternalActor` ∧ actor is `ExternalAuditor` → `AUTHZ-002`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | External Auditor · `POST /api/equipment` is ungated at HTTP (`EquipmentController.cs:27`); the refusal comes from `[RequireInternalActor]` on `RegisterEquipmentCommand` (`src/NT.QAMS.Application/Equipment/EquipmentSlice.cs:12-13`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1. The auditor holds `equipment.view` and `equipment.export` and no write key — but note that on this route the grant set is **irrelevant**, since no filter consults it. |
| **Test Data** | `POST /api/equipment` body `{"name":"Auditor write probe balance","serialNumber":"AUD-PROBE-066","location":"Weighing room","calibrationIntervalDays":365,"gracePeriodDays":30,"branchId":null,"departmentId":null}`; and `POST /api/suppliers` body `{"name":"Auditor probe supplier","supplierType":"Reagents","branchId":null,"departmentId":null}` (`RegisterSupplierCommand` also `[RequireInternalActor]`, `src/NT.QAMS.Application/SupplierQuality/SupplierSlice.cs:11-12`). |
| **Steps** | 1. Log in as `auditor@demo-lab.local`. 2. `GET /api/equipment` — expect `200`. 3. `POST /api/equipment` with the equipment body. 4. `POST /api/suppliers` with the supplier body. 5. Record both statuses and `code` values. 6. `SELECT count(*) FROM qams.equipment_item WHERE tenant_id='{demoLabTenantId}' AND serial_number='AUD-PROBE-066';` and `SELECT count(*) FROM qams.supplier WHERE tenant_id='{demoLabTenantId}' AND name='Auditor probe supplier';`. |
| **Expected UI** | The equipment and supplier registers render read-only for the auditor. |
| **Expected API** | Step 2 → `200 OK`. Steps 3 and 4 → `403` `application/problem+json`, `code` = **`AUTHZ-002`** on both. |
| **Expected DB** | Both counts are `0`. |
| **Expected Audit** | No appends; no `audit.security_event` rows. |
| **Expected Notification** | n/a. |
| **Cleanup** | None. |
| **Evidence** | Three HTTP captures · both zero counts |
| **Result / Defect** | Not Run · — |
| **Notes** | Two commands in one case because both are ungated at HTTP and both belong to the Resources group; keeping them together makes the "the command layer is the only defence on these routes" point once rather than twice. Compare directly with TC-RBAC-API-047, where the *same ungated route* admits a zero-privilege **internal** actor — the tier, not the privilege set, is what stops the auditor. |

---

#### TC-RBAC-API-067 — External Auditor is refused a People-group write at the command layer  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-078 · RSK-RBAC-013 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — DT-2 row 7, People group; paired with DT-2 row 6 (a `RequireAuthenticatedActor` command that **does** admit the auditor) for contrast |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | External Auditor · `POST /api/training-assignments/{id}/complete` is ungated at HTTP (`CompetenciesController.cs:82-83`); refusal comes from `[RequireInternalActor]` on `CompleteTrainingCommand` (`src/NT.QAMS.Application/Competency/CompetencySlice.cs:118-119`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture F1 and an open training assignment `{trainId2}` created by `dh@demo-lab.local`. |
| **Test Data** | `POST /api/training-assignments/{trainId2}/complete`, no body; contrast call `PUT /api/auth/me/language` body `{"language":"fr"}` (`SetMyLanguageCommand` carries `[RequireAuthenticatedActor]`, `src/NT.QAMS.Application/IdentityAccess/Commands/UserManagement.cs:310`). |
| **Steps** | 1. Log in as `auditor@demo-lab.local`. 2. `GET /api/training-assignments` — expect `200`. 3. `POST /api/training-assignments/{trainId2}/complete`. 4. Read status and `code`. 5. `PUT /api/auth/me/language` with `{"language":"fr"}`. 6. Read that status. 7. `SELECT completed_at_utc FROM qams.training_assignment WHERE tenant_id='{demoLabTenantId}' AND id='{trainId2}';` and `SELECT preferred_language FROM qams.user_account WHERE email='auditor@demo-lab.local';`. |
| **Expected UI** | The auditor sees the training queue read-only; the language selector in the account menu works. |
| **Expected API** | Step 2 → `200 OK`. Step 3 → `403` `application/problem+json`, `code` = **`AUTHZ-002`**. Step 5 → `204 No Content` — the self-service command admits the auditor, because `RequireAuthenticatedActorAttribute => true` (`AuthorizationBehavior.cs:74`). |
| **Expected DB** | `completed_at_utc` for `{trainId2}` is still `NULL`; `preferred_language` for the auditor account is `fr`. |
| **Expected Audit** | No append for step 3. Step 5's language change is a user-account mutation — assert whatever `audit.field_change` records for it carries a non-null `tenant_id` (RP-D1). |
| **Expected Notification** | n/a. |
| **Cleanup** | `PUT /api/auth/me/language` `{"language":null}` as the auditor, restoring inheritance. |
| **Evidence** | Three HTTP captures · both SQL reads · a note contrasting the two policy attributes |
| **Result / Defect** | Not Run · — |
| **Notes** | The pairing matters: "read-only" does not mean "cannot write anything" — the auditor may still change their own interface language, because `[RequireAuthenticatedActor]` is a deliberately different policy from `[RequireInternalActor]`. State that in the UAT narrative so a reviewer does not read the language write as a defect. |

---

#### TC-RBAC-API-068 — `CommandPolicyTests` passes on the delivered build: every command declares exactly one policy  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-078 · RSK-RBAC-014 (minted) |
| **Level / Type / Technique** | Architecture (static assembly scan, run as a unit test) · Structural · **Statement coverage** of the scan at `tests/NT.QAMS.Architecture.Tests/CommandPolicyTests.cs:28-37` — the `commands` selection, the `unannotated` filter, and both assertions |
| **Priority / Severity / Automation** | Critical · Critical · Yes (already automated — this case documents the existing merge gate, it does not add a new test) |
| **Role / Permission / Tenant** | n/a — a static reflection scan over `NT.QAMS.Application`; no actor, no HTTP request · n/a — no permission is evaluated · n/a — no tenant context exists in a reflection test |
| **Environment** | Local .NET 9 SDK (`$LOCALAPPDATA\Microsoft\dotnet`), `dotnet test tests/NT.QAMS.Architecture.Tests` with the WebApi stopped (it locks its DLLs). No PostgreSQL required. |
| **Preconditions** | The solution builds. `IsCommand` (`CommandPolicyTests.cs:19-23`) matches any concrete, non-interface type implementing `ICommand` or `ICommand<>`. |
| **Test Data** | The `NT.QAMS.Application` assembly as built from `master` at v1.51.2 — no external data. |
| **Steps** | 1. Stop the running WebApi (`scripts\dev-down.ps1 -ApiOnly`). 2. `dotnet test tests/NT.QAMS.Architecture.Tests --filter FullyQualifiedName~CommandPolicyTests`. 3. Read the pass/fail line and the test count. 4. Independently enumerate the command types with `dotnet test` trace output or a scratch reflection script, and record the count. 5. Cross-check that the recorded count is consistent with the five concrete policies inventoried in the module front matter §1.9 (4 + 4 + 193 + 1 + 12 = 214 declarations). |
| **Expected UI** | n/a — no user interface is involved in an architecture test. |
| **Expected API** | n/a — the test issues no HTTP request. |
| **Expected DB** | n/a — the test opens no database connection. |
| **Expected Audit** | n/a — no runtime audit record is produced by a static scan. |
| **Expected Notification** | n/a. |
| **Cleanup** | Restart the WebApi (`scripts\dev-up.ps1`). |
| **Evidence** | `dotnet test` console output showing `Passed!` and the test name `Every_command_declares_exactly_one_authorization_policy` · the recorded command count |
| **Result / Defect** | Not Run · — |
| **Notes** | Do **not** assert the absolute number 214 as a test expectation: the count moves with every feature, and the front matter's figure is a measurement, not a contract. Assert only the invariant — count of unannotated commands is zero. The `commands.Should().NotBeEmpty()` line (`:29`) is the guard that stops the test passing vacuously if the assembly reference breaks; note whether it is exercised. |

---

#### TC-RBAC-API-069 — `CommandPolicyTests` fails when a new command ships with no policy attribute  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-078 · RSK-RBAC-014 (minted) |
| **Level / Type / Technique** | Architecture · Structural (negative / **mutation-style injected defect**) · **Branch coverage** — drives the `Count() != 1` predicate at `CommandPolicyTests.cs:32` down the `0` arm, which the green build never exercises |
| **Priority / Severity / Automation** | Critical · High · Yes — but as a **deliberately reverted** local mutation, never committed |
| **Role / Permission / Tenant** | n/a — static scan · n/a — no permission evaluated · n/a — no tenant context |
| **Environment** | Local .NET 9 SDK; `dotnet test tests/NT.QAMS.Architecture.Tests`; WebApi stopped. Work on a scratch branch or an uncommitted working-tree change. |
| **Preconditions** | TC-RBAC-API-068 recorded green first, so the failure below is attributable to the injected change alone. |
| **Test Data** | Inject into `src/NT.QAMS.Application/RiskGovernance/RiskGovernanceSlice.cs`, immediately after `CloseRiskCommand`, the single line `public sealed record ProbeUnannotatedCommand(Guid RiskId) : ICommand;` — **with no attribute above it**. |
| **Steps** | 1. Apply the injected line. 2. `dotnet build src/NT.QAMS.Application -c Debug`. 3. `dotnet test tests/NT.QAMS.Architecture.Tests --filter FullyQualifiedName~CommandPolicyTests`. 4. Read the failure message and confirm it names `NT.QAMS.Application.RiskGovernance.ProbeUnannotatedCommand` in the `unannotated` list. 5. Additionally start the API and dispatch the probe command through any route wired to it (or assert by unit test) to confirm the runtime arm: `AuthorizationBehavior.cs:51-53` throws `AUTHZ-000`, surfacing as `403` with `code` = `AUTHZ-000`. 6. Revert the injected line and re-run step 3 to green. |
| **Expected UI** | n/a — no user interface. |
| **Expected API** | For step 5 only: `403` `application/problem+json`, `code` = **`AUTHZ-000`**, title `Command 'ProbeUnannotatedCommand' declares no authorization policy — denied.` |
| **Expected DB** | n/a — the probe command is never handled, so nothing is written; no probe artefacts must remain after step 6. |
| **Expected Audit** | n/a — a static test produces no audit record; the runtime probe in step 5 appends nothing because the behaviour throws before `next()`. |
| **Expected Notification** | n/a. |
| **Cleanup** | Revert the working-tree change (`git checkout -- src/NT.QAMS.Application/RiskGovernance/RiskGovernanceSlice.cs`) and confirm `dotnet test` is green again. **The injected line must never be committed.** |
| **Evidence** | Failing `dotnet test` output naming the probe type · the green re-run after reversion · the `AUTHZ-000` problem body if step 5 is executed |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the case that proves the merge gate is alive rather than merely present. Run it once per validation cycle, not per build. Note the two-layer relationship: the architecture test turns the omission into a **CI failure**, and `AuthorizationBehavior` turns it into a **runtime 403** — the test exists so the second never happens in production (`CommandPolicyTests.cs:8-13`). |

---

#### TC-RBAC-API-070 — `CommandPolicyTests` fails when a command declares two policy attributes  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-078 · RSK-RBAC-014 (minted) |
| **Level / Type / Technique** | Architecture · Structural (negative / injected defect) · **BVA on the cardinality predicate** — `Count() != 1` at `CommandPolicyTests.cs:32` has boundaries at 0 (TC-RBAC-API-069) and 2 (this case); 1 is the only accepted value |
| **Priority / Severity / Automation** | High · High · Yes — as a reverted local mutation, never committed |
| **Role / Permission / Tenant** | n/a — static scan · n/a — no permission evaluated · n/a — no tenant context |
| **Environment** | Local .NET 9 SDK; `dotnet test tests/NT.QAMS.Architecture.Tests`; WebApi stopped. |
| **Preconditions** | Green baseline recorded by TC-RBAC-API-068. `CommandPolicyAttribute` must permit multiple application for the injection to compile — if the attribute is declared `AllowMultiple = false`, the compiler rejects the second attribute and **that is the correct outcome**: record it as a compile-time guarantee stronger than the test, and mark this case as satisfied by construction rather than by execution. |
| **Test Data** | Inject a second policy onto an existing command in `src/NT.QAMS.Application/Equipment/EquipmentSlice.cs`: add `[RequireAuthenticatedActor]` immediately above the existing `[RequireInternalActor]` on `RegisterEquipmentCommand` (`EquipmentSlice.cs:12-13`). |
| **Steps** | 1. Apply the second attribute. 2. `dotnet build src/NT.QAMS.Application -c Debug`. 3. If the build **fails** with a duplicate-attribute diagnostic, stop and record the compile-time outcome described in Preconditions. 4. If it builds, `dotnet test tests/NT.QAMS.Architecture.Tests --filter FullyQualifiedName~CommandPolicyTests`. 5. Confirm the failure message names `NT.QAMS.Application.Equipment.RegisterEquipmentCommand`. 6. Independently note which policy `AuthorizationBehavior` would have used: `Attribute.GetCustomAttribute` (`AuthorizationBehavior.cs:31-32`) returns a **single** attribute and throws `AmbiguousMatchException` when more than one is present — record the runtime consequence, do not assume it silently picks one. 7. Revert and re-run to green. |
| **Expected UI** | n/a — no user interface. |
| **Expected API** | n/a — this case does not issue an HTTP request unless step 6 is executed as a runtime probe; if it is, expect a `500` from the ambiguous-match failure, **not** a `403`, and record that as the finding. |
| **Expected DB** | n/a — nothing is written. |
| **Expected Audit** | n/a — static test. |
| **Expected Notification** | n/a. |
| **Cleanup** | `git checkout -- src/NT.QAMS.Application/Equipment/EquipmentSlice.cs` and confirm green. **Never commit the injected attribute.** |
| **Evidence** | Build output or failing test output naming the command · the green re-run · a written note of which of the two outcomes (compile error vs test failure) actually occurred |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 6 is the reason this case is worth running rather than reasoning about: the test's wording is "exactly one", and the *runtime* consequence of two is not a policy choice but an exception. If step 6 confirms a `500`, that is a new observation worth its own gap — a misconfigured command should fail closed with `AUTHZ-000`, not with an unhandled ambiguity. Do not pre-record that as a defect; measure it. |

---

## Batch coverage note

**Covered.** 41 cases, `TC-RBAC-API-030` … `TC-RBAC-API-070`, all `Result = Not Run · —`, all in the canonical 17-row / 28-field block, each naming its technique.

- **Risk group (030–044, 15 cases):** `risks.approve` allow + deny, `risks.void` deny; `compliance.view` class-level allow (External Auditor) and wholesale deny (Analyst, all five class-gated GETs); the `compliance.view ∧ compliance.create` AND-composition as an MC-DC pair (035 deny / 036 allow); `conflicts.approve` allow + deny and `conflicts.void` deny; `org-context.create` deny across two routes, `org-context.void` allow (the documented DH granularity compromise), `org-context.edit` deny across three routes; the two `access-reviews` composition cases (043 write-on-view allowed, 044 create-without-view denied) labelled `[ID]` against GAP-RBAC-011.
- **Resources group (045–055, 11 cases):** `equipment.void` allow + deny plus the inert-`equipment.create` case (047, `[ID]`, GAP-RBAC-003, driven to the zero-key extreme); `reference-standards` create-deny / edit-allow / approve-deny; `monitoring-points` create-deny / edit-allow across three routes / void-deny; `suppliers.approve` deny across the two routes it gates and `suppliers.void` allow.
- **People group (056–064, 9 cases):** `competencies` create-allow / approve-deny / edit-deny; `training.create` deny alongside the ungated self-completion route; `test-authorizations` create-allow / approve-deny with the `AUTHZ-*` code-collision guard; `users.view` deny (QM) and allow-by-bespoke-role, `users.manage` deny (QM) / allow (TA) across two of its eight routes, plus the eight-route `users.view`-only boundary sweep.
- **External Auditor read-only, one write command per group (065–067):** deliberately routed through the **ungated** write endpoints (`POST /api/risks`, `POST /api/equipment` + `POST /api/suppliers`, `POST /api/training-assignments/{id}/complete`) so the refusal provably comes from `[RequireInternalActor]` in `AuthorizationBehavior` and carries **`AUTHZ-002`**, not the HTTP filter's `AUTHZ-403`. 067 pairs the deny with the `[RequireAuthenticatedActor]` self-service write the auditor *may* perform, so "read-only" is stated accurately.
- **`CommandPolicyTests` contract (068–070):** green baseline; the zero-attribute injected defect with its `AUTHZ-000` runtime twin; the two-attribute cardinality boundary with an explicit instruction to measure, not assume, the `Attribute.GetCustomAttribute` ambiguity outcome.

**Not covered in this slice, and why.**

1. **The 92-plus catalogue keys with no enforcement point** are represented by exactly one worked case (047) rather than one per key. There is no observable behaviour to assert for the rest — a per-key case would be 90 identical `[GD]` stubs against GAP-RBAC-003. Exploratory charter `TC-RBAC-EXPL-001` is the right instrument; this batch does not duplicate it.
2. **`tenant-settings.view`** (GAP-RBAC-004) sits in the Administration group, outside this slice.
3. **Platform-admin bypass** on every route in this slice is not authored here — `PrivilegeResolution.cs:39` short-circuits `Has()` to true for the platform tier, and batch D owns per-request resolution including that bypass. Adding it per route would collide.
4. **Branch/department working scope** interacting with these same routes is deliberately excluded: every fixture account is unrestricted. `IAllocatable` scope filtering is batch D's `TC-RBAC-DF-*`, and mixing it in would make a `403` and a `404` indistinguishable in the evidence.
5. **Every `SOD-*` case** is avoided by construction — each positive case above chooses an actor who is not the record's preparer/declarant/trainee/registrant, precisely so a `422 SOD-*` cannot masquerade as a permission result. Batch E owns the duty pairs.
6. **`ROLE-006` and the two lockout bypasses** (GAP-RBAC-012/013) touch `POST /api/users/{id}/role` and `POST /api/users/{id}/deactivate`, which are inside the `users` module but belong to batch E's lockout scope; case 063 explicitly declines to exercise them.
7. **Scope mismatch, recorded for the traceability owner.** The front matter's ID-reservation table assigns batch B the "administration, people, documents, operations" groups, while this authoring pass was commissioned for "Risk, Resources and People". This file follows the commission. Consequence: the **Risk and Resources** groups are now authored in batch B (ids 030–067) rather than batch C, and **documents, operations and administration** gates remain unauthored in the 001–029 range of batch B's block. That range is a live coverage hole until a later pass fills it — it is not a delivered case (conventions §7). Batch C must check this file before consuming `TC-RBAC-API-071+` so the risk/resources gates are not authored twice.

**New gaps found in this pass.** Both are outside the front matter's numbered sequence, as instructed.

**GAP-RBAC-901 — The permission catalogue is 171 keys, not 170; `reports.manage` is missing from every inventory.**
*Source:* `src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:179-183` declares `reports` with `[View, Export, Manage]` — three actions, not the `ReadOnlyModule` pair. The key is real and enforced: `[RequirePermission(PermissionCatalog.Reports, PermissionAction.Manage)]` at `src/NT.QAMS.WebApi/Controllers/ReportsController.cs:68`, `[RequirePermissionPolicy(PermissionCatalog.Reports, PermissionAction.Manage)]` at `src/NT.QAMS.Application/Reporting/QualityHealthProfileSlice.cs:51`, a frontend affordance at `frontend/src/app/features/dashboard/quality-analytics.component.ts:46`, and a data backfill granting it to existing roles in migration `20260801131521_QualityHealthProfile.cs:110-127`. Summing the action arrays across `PermissionCatalog.Modules` gives **171**.
*Contradicted sources:* this module's front matter §4.1 (row 28, `reports` = `ReadOnlyModule`, 2 keys; totals line "170 keys"), its §0 correction ("31 modules … 170 permission keys"), and URS-095 (`docs/validation/06-Revalidation-Delta-v1.38-v1.50.md:154`, "31 modules × 8 actions = 170 keys").
*Impact:* the front matter's §0 corrected the conventions file from 170 to 170-with-31-modules and is itself now one key short; the seeded-role totals in §4.3 (TA 170, QM 164, …) are therefore also short by the keys each role receives from `reports`. An RTM assertion keyed on 170 fails against the build.
*Testing limitation:* no case in this batch depends on the count. A case asserting `AllKeys.Count == 170` would fail; assert 171, or better, assert the count equals the sum over `Modules` so it cannot drift again.
*Suggested acceptance criteria:* URS-095 and the front matter §4.1 both state a figure produced by a repeatable command, and a test asserts `PermissionCatalog.AllKeys.Count == PermissionCatalog.Modules.Sum(m => m.Actions.Count)`.
*Severity:* Medium (documentation/traceability, not behaviour). *Responsible role:* Validation Lead + Solution Architect.

**GAP-RBAC-902 — An authorization refusal is recorded nowhere.**
*Source:* `ISecurityEventLog` is injected at exactly six sites — `src/NT.QAMS.Application/IdentityAccess/Commands/Login.cs:32,181`, `MfaAndPin.cs:38`, `RefreshSessions.cs:83,154`, `src/NT.QAMS.Infrastructure/Compliance/ComplianceLedgerServices.cs:87`, `src/NT.QAMS.WebApi/Controllers/ExportsController.cs:27`. Neither `RequirePermissionAttribute.OnAuthorizationAsync` (`RequirePermissionAttribute.cs:38-60`) nor `AuthorizationBehavior.Handle` (`AuthorizationBehavior.cs:39-88`) writes a security event, an audit-trail entry or a field-change row. A refused request leaves the response log and nothing else.
*Impact:* 21 CFR Part 11 §11.10(d) requires limiting system access to authorized individuals and §11.300(d) requires that unauthorized use attempts be **detected and reported**; ISO 27001 A.8.15/A.8.16 expect the same. Today, a user probing thirty gated endpoints for privileges they lack produces zero rows in `audit.security_event`, so a user-access reviewer or an alert rule has nothing to key on. Contrast with failed **authentication**, which is logged (`LOGIN_FAILED`), and failed **signing**, which is logged and throttled (`ESIGN_FAILED`, `ESIGN_LOCKED`, `ComplianceLedgerServices.cs:100,142`) — the authorization layer is the one access control that is silent.
*Testing limitation:* every negative case in this batch therefore asserts the **absence** of an audit record. Those assertions are correct against the build and must not be rewritten as failures. Should the gap be closed, all of 031, 032, 034, 035, 038, 039, 040, 042, 044, 046, 048, 050, 051, 053, 054, 057, 058, 059, 061, 062, 063, 064, 065, 066 and 067 need their *Expected Audit* row updated in the same change.
*Suggested acceptance criteria:* every `AUTHZ-403` and `AUTHZ-002` refusal writes one `audit.security_event` row carrying the actor id, the tenant id, the attempted permission key and the route, and a repeated-refusal threshold raises a notification to the tenant administrator.
*Severity:* **High** (Part 11 §11.300(d) detection-and-reporting duty). *Responsible role:* Quality Manager + Lead Developer.
