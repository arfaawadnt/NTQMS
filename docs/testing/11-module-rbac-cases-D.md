# RBAC — Detailed Test Cases, Batch D

This batch authors **43 detailed cases** over two reserved id blocks — `TC-RBAC-SEC-001` … `TC-RBAC-SEC-033` and `TC-RBAC-MCDC-001` … `TC-RBAC-MCDC-010` — covering segregation of duties end to end against the real implemented codes (`SOD-AQ-001` via `AggregateRoot.EnsureSignerIsNotPreparer` including its documented no-op when the preparer is unknown, `SOD-CAPA-001`/`SOD-CAPA-002`, `SOD-QP-001`, `SOD-COMP-001` competency self-assessment and self-authorization, `SOD-SUP-001` supplier self-approval, and the *absent* guard on audit sign-off by the auditor who raised the findings); MC/DC over the composite authorization condition that governs a Part-11 document publish (tenant match ∧ permission held ∧ ownership ∧ record state ∧ signature valid); data-scoped access through `qams.user_branch_access` and `qams.user_department_access` (`SCOPE-001` … `SCOPE-004`, the 404-vs-422 asymmetry, null-attribution visibility, and RLS isolation of the two scope tables); direct-API access that bypasses a permission-hidden SPA control; and direct Angular route access to a page no route guard protects. **Deliberately left to sibling batches:** `Role`/`PermissionCatalog` domain units and the role lifecycle state machine (batch A); per-`[RequirePermission]` call-site allow/deny matrices and privilege-matrix decision tables (batches B and C); per-request privilege resolution, immediate grant/revoke and `AuthorizationBehavior` policy dispatch as isolated integration cases (front-matter reservation for `TC-RBAC-INT-*`); the `ROLE-006` lockout-guard boundary set (`TC-RBAC-BVA-*`); `PermissionsService`, guard unit specs, Playwright journeys and privilege-screen accessibility (batch F). Requirement traces use `URS-031`, `URS-039`, `URS-049` (the three SoD requirements the FRA names), `URS-020`/`URS-025`/`URS-026` (signing ceremony and document lifecycle), and `URS-095`…`URS-099` from `docs/validation/06-Revalidation-Delta-v1.38-v1.50.md` Part A.9. **The FRA (`docs/validation/02-Functional-Risk-Assessment.md`) is area-level and carries no per-risk identifiers**, so per conventions §5 this batch mints `RSK-RBAC-001` … `RSK-RBAC-010` and declares them here: `RSK-RBAC-001` a preparer signs off their own regulated record · `RSK-RBAC-002` an SoD rule is silently absent because the preparer id is null · `RSK-RBAC-003` guard evaluation order lets one refusal mask another, hiding the SoD breach from the operator · `RSK-RBAC-004` a privileged act reachable by direct API call after the SPA hid its control · `RSK-RBAC-005` out-of-scope branch/department data is read or written · `RSK-RBAC-006` cross-tenant leakage of role or scope rows · `RSK-RBAC-007` a forbidden SPA page renders and discloses structure · `RSK-RBAC-008` a signature is minted for a publish that then fails · `RSK-RBAC-009` a regulated sign-off carries no SoD guard at all · `RSK-RBAC-010` one true condition compensates for another that is false in the composite authorization decision. Every `Result` is `Not Run` — this package is authored, not executed (conventions §6.5).

**Shared fixture set referenced by these cases** (create once, per case Preconditions): tenant `demo-lab`; second tenant `rival-lab` for cross-tenant probes. Users — `admin@demo-lab.local` / `Demo-Admin-Pass-2!` (tier `TenantAdmin`, seeded role *Tenant Administrator*, all 170 keys); `qm-a@demo-lab.local` / `Qm-Alpha-Pass-3!` PIN `481920` (tier `QualityManager`, seeded role *Quality Manager*, 164 keys); `qm-b@demo-lab.local` / `Qm-Bravo-Pass-4!` PIN `573104` (same role, the independent second pair of eyes); `analyst-n@demo-lab.local` / `An-North-Pass-5!` (tier `Analyst`, seeded role *Analyst*, 65 keys, working scope = branch `North`); `trainee@demo-lab.local` / `Tr-Pass-6!` (tier `Analyst`). Org units — branches `North` (`@BR_NORTH`) and `South` (`@BR_SOUTH`), departments `Chemistry` (`@DEP_CHEM`) and `Microbiology` (`@DEP_MICRO`), created with `POST /api/branches` / `POST /api/departments` (`organization.create`, `PlatformControllers.cs:22,45`). Signature PINs are set by their owner with `POST /api/auth/signature-pin` (`AuthController.cs:134-138`).

#### TC-RBAC-SEC-001 — Validation-study sign-off refused when the signer is the preparer (`SOD-AQ-001`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-039 · RSK-RBAC-001 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — DT row *(signer == CreatedByUserId, state = StatsCalculated)* |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `analytical-quality.sign` (`AnalyticalQualityControllers.cs:88`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qm-a` created the study, so `AuditStampInterceptor` stamped `created_by_user_id = @QM_A` (`AuditStampInterceptor.cs:49`); study is at `state='StatsCalculated'` (`ValidationStudy.cs:141`). |
| **Test Data** | `POST /api/validation-studies` body `{"analyte":"Glucose","protocol":"CLSI EP15-A3","totalAllowableError":10.0}` as `qm-a`; two replicates `{"level":"L1","measured":5.10,"reference":5.00}` and `{"level":"L1","measured":5.14,"reference":5.00}`; then `POST /api/validation-studies/@STUDY/calculate`. |
| **Steps** | 1. Sign in as `qm-a@demo-lab.local` / `Qm-Alpha-Pass-3!`. 2. Create the study, enter both replicates, call `/calculate`. 3. `POST /api/validation-studies/@STUDY/sign-off` with no body, still as `qm-a`. 4. Read status and the `code` extension. 5. `SELECT state, signed_off_by, created_by_user_id FROM qams.validation_study WHERE id='@STUDY';`. |
| **Expected UI** | The study page keeps the *Sign off* control enabled (`analytical-quality.sign` is granted) and surfaces the returned problem title "Segregation of duties: the preparer of a record cannot sign it off." as an inline error; the state pill stays *StatsCalculated*. |
| **Expected API** | `422 Unprocessable Content`, `application/problem+json`, `code = "SOD-AQ-001"`, title `Segregation of duties: the preparer of a record cannot sign it off.` (`AggregateRoot.cs:36-42`; 422 arm at `DomainExceptionHandler.cs:75-80`). |
| **Expected DB** | `qams.validation_study`: `state` still `'StatsCalculated'`, `signed_off_by IS NULL`, `signed_off_at_utc IS NULL`, `created_by_user_id = @QM_A` unchanged. |
| **Expected Audit** | No `ValidationStudySignedOff` row appears in `audit.audit_trail` for `@STUDY` (the event is raised only after the guard passes, `ValidationStudy.cs:154`); the ledger `sequence` maximum for `demo-lab` is unchanged. |
| **Expected Notification** | n/a — no notification policy fires on a refused sign-off. |
| **Cleanup** | `DELETE FROM qams.validation_replicate WHERE study_id='@STUDY'; DELETE FROM qams.validation_study WHERE id='@STUDY';` run with `SELECT set_config('app.bypass_rls','on',false);` first. |
| **Evidence** | HTTP response capture (status + `code`) · SQL result set · `audit.audit_trail` max-sequence before/after |
| **Notes** | `EnsureSignerIsNotPreparer` is called **before** the `MV-015` state guard (`ValidationStudy.cs:145-149`) — see TC-RBAC-SEC-004 for the ordering case. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-002 — Validation-study sign-off accepted for a signer who is not the preparer  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-039 · RSK-RBAC-001 |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — valid partition *signer ≠ preparer* |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-b`) · `analytical-quality.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Study created by `qm-a` exactly as in TC-RBAC-SEC-001, `state='StatsCalculated'`, `created_by_user_id=@QM_A`. `qm-b` holds the seeded *Quality Manager* role (164 keys, includes `analytical-quality.sign`). |
| **Test Data** | Same study `@STUDY`; signer `qm-b@demo-lab.local` / `Qm-Bravo-Pass-4!`. |
| **Steps** | 1. Sign in as `qm-b`. 2. `POST /api/validation-studies/@STUDY/sign-off`. 3. Read status. 4. `SELECT state, signed_off_by, signed_off_at_utc FROM qams.validation_study WHERE id='@STUDY';`. 5. `SELECT event_type, sequence FROM audit.audit_trail WHERE payload LIKE '%@STUDY%' ORDER BY sequence DESC LIMIT 1;` after `SELECT set_config('app.bypass_rls','on',false);`. |
| **Expected UI** | The study page re-renders with state pill *SignedOff*, the *Sign off* control removed, and the signer's display name shown against "Signed off by". |
| **Expected API** | `204 No Content`, empty body (`AnalyticalQualityControllers.cs:89-93`). |
| **Expected DB** | `state='SignedOff'`, `signed_off_by=@QM_B`, `signed_off_at_utc` non-null and within 5 s of the request. A subsequent `UPDATE qams.validation_study SET analyte='x' WHERE id='@STUDY'` is rejected by `qams.reject_frozen_mutation()`. |
| **Expected Audit** | One `audit.audit_trail` row, `event_type='ValidationStudySignedOff'`, payload carrying `StudyId=@STUDY` and `SignedOffBy=@QM_B`, `prev_hash` equal to the previous row's `entry_hash`. |
| **Expected Notification** | n/a — no `NotificationPolicies` rule is registered for `ValidationStudySignedOff`; assert zero new `qams.notification_dispatch` rows for `@STUDY`. |
| **Cleanup** | Signed records are immutable by trigger — leave `@STUDY` in place and record its id in the run log rather than attempting a delete. |
| **Evidence** | HTTP 204 capture · SQL result set · audit-trail row with `prev_hash`/`entry_hash` |
| **Notes** | This is the positive twin of TC-RBAC-SEC-001; running both back to back proves the refusal was caused by identity, not by state or privilege. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-003 — `SOD-AQ-001` is a no-op when the preparer is unknown: a self sign-off completes  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-039 · RSK-RBAC-002 |
| **Level / Type / Technique** | Integration · Security (negative-by-omission) · Condition testing — the `CreatedByUserId is { } preparer` sub-condition evaluated false |
| **Priority / Severity / Automation** | Critical · Critical · Yes (integration, `QMS_ITEST_POSTGRES`) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `analytical-quality.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; SQL executed with `SELECT set_config('app.bypass_rls','on',false);` |
| **Preconditions** | Study `@STUDY2` created by `qm-a` and calculated to `state='StatsCalculated'`; then its provenance erased to simulate a legacy or background-created record: `UPDATE qams.validation_study SET created_by_user_id=NULL WHERE id='@STUDY2';`. This reproduces the accepted residual F-05b documented at `AggregateRoot.cs:33-34`. |
| **Test Data** | `@STUDY2`; signer `qm-a@demo-lab.local` (the original preparer). |
| **Steps** | 1. Create + calculate `@STUDY2` as `qm-a`. 2. Null `created_by_user_id` per Preconditions. 3. `POST /api/validation-studies/@STUDY2/sign-off` as `qm-a`. 4. Read status. 5. `SELECT state, signed_off_by, created_by_user_id FROM qams.validation_study WHERE id='@STUDY2';`. |
| **Expected UI** | The study page shows *SignedOff* and names `qm-a` as signer — the operator sees no indication that a segregation-of-duties control was skipped. |
| **Expected API** | `204 No Content`. **`SOD-AQ-001` is NOT raised** because the guard short-circuits on the null preparer (`AggregateRoot.cs:38`). |
| **Expected DB** | `state='SignedOff'`, `signed_off_by=@QM_A`, `created_by_user_id IS NULL` — the same person prepared and signed the record and nothing in the row records that fact. |
| **Expected Audit** | One `ValidationStudySignedOff` row in `audit.audit_trail` with `SignedOffBy=@QM_A`; **no** compensating security event marks the bypassed control. |
| **Expected Notification** | n/a — no notification is defined for a sign-off. |
| **Cleanup** | Record `@STUDY2` in the run log; the row is now frozen by `qams.reject_frozen_mutation()` and must not be edited. |
| **Evidence** | HTTP 204 capture · SQL result set showing `created_by_user_id IS NULL` alongside `signed_off_by=@QM_A` |
| **Notes** | **Gap-dependent on GAP-RBAC-009.** Acceptance criteria to implement against: `EnsureSignerIsNotPreparer` must, when `CreatedByUserId` is null, either (a) refuse the sign-off with a distinct code (proposal `SOD-AQ-002`, "the preparer of this record is unrecorded — segregation of duties cannot be established") or (b) complete the sign-off **and** write an `audit.security_event` row of type `SOD_UNVERIFIABLE` carrying `subject_ref='VALIDATION_STUDY:@STUDY2'`, so the residual is visible to a reviewer. Silent completion with no trace is the condition this case exists to expose. Do **not** record this case as a Pass on the current build — it documents the defect. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-004 — `SOD-AQ-001` is evaluated before the `MV-015` state guard  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-039 · RSK-RBAC-003 |
| **Level / Type / Technique** | API · Security (negative) · Path testing — the guard-ordering path through `ValidationStudy.SignOff` |
| **Priority / Severity / Automation** | High · Medium · Yes (unit + functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `analytical-quality.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Study `@STUDY3` created by `qm-a` and left at `state='ProtocolConfigured'` — **no** replicates, **no** `/calculate`. Both the SoD condition and the state condition are therefore false. |
| **Test Data** | `POST /api/validation-studies` body `{"analyte":"Sodium","protocol":"CLSI EP15-A3","totalAllowableError":5.0}` as `qm-a`. |
| **Steps** | 1. Create `@STUDY3` as `qm-a`; do not add replicates. 2. `POST /api/validation-studies/@STUDY3/sign-off` as `qm-a`. 3. Record status and `code`. 4. Repeat step 2 as `qm-b` (SoD condition now true, state condition still false). 5. Record status and `code`. |
| **Expected UI** | Both attempts surface a single inline error; the state pill stays *ProtocolConfigured* in both. |
| **Expected API** | Step 3: `422` `code="SOD-AQ-001"` — the SoD check at `ValidationStudy.cs:145` precedes the state check at `:146-149`, so the preparer sees the SoD refusal and never learns the study was also in the wrong state. Step 5: `409 Conflict` `code="MV-015"`, title `Statistics must be calculated before sign-off.` (`InvalidStateTransitionException` arm, `DomainExceptionHandler.cs:45-51`). |
| **Expected DB** | `qams.validation_study` row for `@STUDY3` unchanged after both attempts: `state='ProtocolConfigured'`, `signed_off_by IS NULL`. |
| **Expected Audit** | No `audit.audit_trail` rows for `@STUDY3` beyond the creation event; `sequence` advances by zero across both attempts. |
| **Expected Notification** | n/a — refusals raise no notification. |
| **Cleanup** | `DELETE FROM qams.validation_study WHERE id='@STUDY3';` under `app.bypass_rls='on'`. |
| **Evidence** | Two HTTP response captures showing different codes and different status classes for the same request shape |
| **Notes** | The two status classes matter: 422 (business rule) vs 409 (state transition). A test that asserts only "an error" would not detect a reordering of the two guards, which would change what the preparer is told. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-005 — Uncertainty-budget approval refused when the approver prepared the budget (`SOD-AQ-001`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-039 · RSK-RBAC-001 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — invalid partition *approver == preparer* on a second `EnsureSignerIsNotPreparer` call site |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `analytical-quality.approve` (`UncertaintyController.cs:57-58`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Budget `@BUDGET` created by `qm-a` with one component, then `POST /api/uncertainty-budgets/@BUDGET/calculate` → `status='Calculated'` (`UncertaintyBudget.cs:160`). `created_by_user_id=@QM_A`. |
| **Test Data** | Component `{"source":"Repeatability","relativeStandardUncertainty":0.0120}`; coverage factor as configured at creation. |
| **Steps** | 1. As `qm-a`, create `@BUDGET`, add the component, call `/calculate`. 2. `POST /api/uncertainty-budgets/@BUDGET/approve` as `qm-a`. 3. Read status and `code`. 4. `SELECT status, approved_by, approved_at_utc FROM qams.uncertainty_budget WHERE id='@BUDGET';`. |
| **Expected UI** | Budget page keeps the *Approve* control visible (`analytical-quality.approve` is granted) and shows the SoD message; status pill stays *Calculated*. |
| **Expected API** | `422`, `code="SOD-AQ-001"` (`UncertaintyBudget.cs:166`, called before the `MU-010` state guard at `:167-170`). |
| **Expected DB** | `status='Calculated'`, `approved_by IS NULL`, `approved_at_utc IS NULL`. The row remains mutable — `POST /api/uncertainty-budgets/@BUDGET/components` still succeeds, proving `qams.reject_frozen_mutation()` has not engaged (it triggers only at `status='Approved'`). |
| **Expected Audit** | No `UncertaintyBudgetApproved` entry in `audit.audit_trail`. |
| **Expected Notification** | n/a — no notification policy on budget approval. |
| **Cleanup** | `DELETE FROM qams.uncertainty_component WHERE budget_id='@BUDGET'; DELETE FROM qams.uncertainty_budget WHERE id='@BUDGET';` under bypass. |
| **Evidence** | HTTP response capture · SQL result set · successful post-refusal component add proving the row is still mutable |
| **Notes** | `uncertainty_budget` is the 14th `EnsureSignerIsNotPreparer` site and the only non-*sign-off* one (it is an *approve*), which is why it is covered separately from the 12 study families. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-006 — PT-plan approval: `SOD-AQ-001` precedes both `PTP-010` and the empty-plan guard `PTP-011`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-039 · RSK-RBAC-003 |
| **Level / Type / Technique** | API · Security (negative) · Path testing — three-guard ordering through `PtPlan.Approve` |
| **Priority / Severity / Automation** | High · High · Yes (unit) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `proficiency-testing.approve` (`PtPlansController.cs:50-51`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Plan `@PTPLAN` created by `qm-a` with **zero** items, `status='Draft'`, `created_by_user_id=@QM_A`. Both the SoD condition and the item-count condition are violated. |
| **Test Data** | `POST /api/pt-plans` body `{"year":2026,"title":"Chemistry EQA 2026"}` as `qm-a`. |
| **Steps** | 1. Create `@PTPLAN` as `qm-a`, add no items. 2. `POST /api/pt-plans/@PTPLAN/approve` as `qm-a`. 3. Record status and `code`. 4. Repeat step 2 as `qm-b`. 5. Record status and `code`. 6. `SELECT status, approved_by FROM qams.pt_plan WHERE id='@PTPLAN';`. |
| **Expected UI** | Both attempts leave the plan on *Draft* with an inline error; the *Add scheme line* control remains available. |
| **Expected API** | Step 3: `422` `code="SOD-AQ-001"` (guard at `PtPlan.cs:108`, before the `PTP-010` state check at `:109-112` and the `PTP-011` emptiness check at `:114-117`). Step 5: `422` `code="PTP-011"`, title `An empty plan cannot be approved — add at least one scheme/analyte line.` |
| **Expected DB** | `qams.pt_plan`: `status='Draft'`, `approved_by IS NULL`, `approved_at_utc IS NULL`; `SELECT count(*) FROM qams.pt_plan_item WHERE plan_id='@PTPLAN'` = `0` throughout. |
| **Expected Audit** | No approval entry in `audit.audit_trail` for `@PTPLAN`. |
| **Expected Notification** | n/a — no notification on PT-plan approval. |
| **Cleanup** | `DELETE FROM qams.pt_plan WHERE id='@PTPLAN';` under bypass. |
| **Evidence** | Two HTTP response captures with the two distinct codes · SQL result set |
| **Notes** | The plan is *Draft* in both attempts, so `PTP-010` is never the observed code; the case pins that the SoD guard outranks the emptiness guard, which is what makes the preparer's refusal message identity-based rather than content-based. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-007 — Nonconformance verification refused when the verifier raised it (`SOD-CAPA-002`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-031 · RSK-RBAC-001 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — *(actorId == RaisedBy, status = PendingVerification)* |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `nc.approve` (`NonconformancesController.cs:99-100`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qm-a` raised the NC, so `qams.nonconformance.raised_by = @QM_A` (`RaiseNcHandler`, `NcWorkflowCommands.cs:36-38`); the NC has been driven to `status='PendingVerification'` via submit → triage → RCA → action → complete action → submit-verification. |
| **Test Data** | `POST /api/nonconformances` body `{"title":"Balance drift on AN-02","description":"Daily check outside 0.5 mg","severity":4,"likelihood":3,"sourceType":"InternalAudit"}` as `qm-a` → `rpn = 12`. Verify body `{"passed":true}`. |
| **Steps** | 1. As `qm-a`, raise the NC and drive it to `PendingVerification`. 2. `POST /api/nonconformances/@NC/verify` body `{"passed":true}` as `qm-a`. 3. Read status and `code`. 4. `SELECT status, raised_by, rpn FROM qams.nonconformance WHERE id='@NC';`. |
| **Expected UI** | The NC detail page shows the *Verify* action (both `nc.approve` buttons render at `nc-detail.component.ts:100,106`) and renders the SoD message inline; the workflow stepper stays on *PendingVerification*. |
| **Expected API** | `422`, `application/problem+json`, `code="SOD-CAPA-002"`, title `Segregation of duties: the raiser cannot verify their own nonconformance.` (`Nonconformance.cs:245`). |
| **Expected DB** | `qams.nonconformance`: `status='PendingVerification'` unchanged, `raised_by=@QM_A`, `rpn=12`. |
| **Expected Audit** | No `NcVerified` row in `audit.audit_trail` (raised only after the guard, `Nonconformance.cs:251`). |
| **Expected Notification** | n/a — no notification policy fires on a refused verification; assert zero new `qams.notification_dispatch` rows for `@NC`. |
| **Cleanup** | `DELETE FROM qams.capa_action WHERE nc_id='@NC'; DELETE FROM qams.nonconformance WHERE id='@NC';` under bypass. |
| **Evidence** | HTTP response capture · SQL result set · notification-dispatch count before/after |
| **Notes** | `RaisedBy` is non-nullable, so unlike `SOD-AQ-001` this rule has **no** null bypass (§4.5 SoD-3). |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-008 — Nonconformance verification accepted for an independent verifier  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-031 · RSK-RBAC-001 |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — valid partition *actorId ≠ RaisedBy*, `passed=true` |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-b`) · `nc.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | NC `@NC` from TC-RBAC-SEC-007 still at `status='PendingVerification'`, `raised_by=@QM_A`. |
| **Test Data** | Verify body `{"passed":true}`; actor `qm-b@demo-lab.local` / `Qm-Bravo-Pass-4!`. |
| **Steps** | 1. Sign in as `qm-b`. 2. `POST /api/nonconformances/@NC/verify` body `{"passed":true}`. 3. Read status. 4. `SELECT status FROM qams.nonconformance WHERE id='@NC';`. 5. `SELECT event_type FROM audit.audit_trail WHERE payload LIKE '%@NC%' ORDER BY sequence DESC LIMIT 1;` under bypass. |
| **Expected UI** | Stepper advances to *EffectivenessCheck*; the *Verify* action is replaced by *Confirm effectiveness*. |
| **Expected API** | `204 No Content`. |
| **Expected DB** | `qams.nonconformance.status = 'EffectivenessCheck'` (`Nonconformance.cs:248`). |
| **Expected Audit** | One `audit.audit_trail` row `event_type='NcVerified'` carrying `NcId=@NC` and the `nc_ref`; hash chain contiguous (`prev_hash` = previous `entry_hash`). |
| **Expected Notification** | n/a — no `NotificationPolicies` rule is registered for `NcVerified`; assert zero new dispatch rows. |
| **Cleanup** | Leave `@NC` at *EffectivenessCheck* for TC-RBAC-SEC-011 / SEC-012, which consume it. |
| **Evidence** | HTTP 204 capture · SQL result set · audit-trail row |
| **Notes** | `passed=false` would send the NC back to `ActionPlan` instead (`Nonconformance.cs:248`) — covered by TC-RBAC-SEC-010. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-009 — `NC-021` state guard is evaluated before `SOD-CAPA-002`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-031 · RSK-RBAC-003 |
| **Level / Type / Technique** | API · Security (negative) · Path testing — the reverse ordering to `ValidationStudy.SignOff` |
| **Priority / Severity / Automation** | High · Medium · Yes (unit) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `nc.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | NC `@NC2` raised by `qm-a`, left at `status='Raised'` — never submitted, never triaged. Both the state condition and the SoD condition are violated. |
| **Test Data** | `POST /api/nonconformances` body `{"title":"Reagent lot mismatch","description":"Lot on worksheet ≠ lot in fridge","severity":2,"likelihood":2,"sourceType":"Internal"}` as `qm-a` → `rpn = 4`. |
| **Steps** | 1. Raise `@NC2` as `qm-a`, take no further workflow step. 2. `POST /api/nonconformances/@NC2/verify` body `{"passed":true}` as `qm-a`. 3. Record status and `code`. 4. `SELECT status FROM qams.nonconformance WHERE id='@NC2';`. |
| **Expected UI** | The *Verify* control is not rendered at this stage of the stepper; the case is exercised by direct API call, and a manual call returns the state error. |
| **Expected API** | `409 Conflict`, `code="NC-021"`, title `Cannot verify a nonconformance in state Raised.` — **not** `SOD-CAPA-002`, because `Require(NcStatus.PendingVerification, "NC-021", "verify")` runs first (`Nonconformance.cs:239`) and the SoD comparison sits at `:243`. |
| **Expected DB** | `status='Raised'`, `raised_by=@QM_A`, `rpn=4` unchanged. |
| **Expected Audit** | No new `audit.audit_trail` rows for `@NC2`. |
| **Expected Notification** | n/a — refusals raise no notification. |
| **Cleanup** | `DELETE FROM qams.nonconformance WHERE id='@NC2';` under bypass. |
| **Evidence** | HTTP response capture showing `409 NC-021` where a naive reading would predict `422 SOD-CAPA-002` |
| **Notes** | Contrast with TC-RBAC-SEC-004 and TC-RBAC-SEC-006, where the SoD guard runs **first**. The codebase is not uniform on this ordering; both orderings are pinned so a refactor cannot silently swap them. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-010 — `SOD-CAPA-002` fires even when the verification would fail (`passed=false`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-031 · RSK-RBAC-003 |
| **Level / Type / Technique** | API · Security (negative) · Multiple-condition testing — the SoD condition is independent of the `passed` input |
| **Priority / Severity / Automation** | High · High · Yes (unit) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `nc.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | NC `@NC3` raised by `qm-a` and driven to `status='PendingVerification'`. |
| **Test Data** | Verify body `{"passed":false}`; actor `qm-a` (the raiser). |
| **Steps** | 1. Drive `@NC3` to `PendingVerification` as `qm-a`. 2. `POST /api/nonconformances/@NC3/verify` body `{"passed":false}` as `qm-a`. 3. Record status and `code`. 4. `SELECT status FROM qams.nonconformance WHERE id='@NC3';`. 5. Repeat step 2 as `qm-b` and re-read the status. |
| **Expected UI** | Step 2 shows the SoD error and the stepper stays on *PendingVerification*; step 5 moves the stepper back to *ActionPlan*. |
| **Expected API** | Step 3: `422` `code="SOD-CAPA-002"` — the SoD check at `Nonconformance.cs:243` precedes the `passed` branch at `:248`, so a failing verification by the raiser is still an SoD refusal, not a routine bounce-back. Step 5: `204 No Content`. |
| **Expected DB** | After step 3: `status='PendingVerification'`. After step 5: `status='ActionPlan'`. |
| **Expected Audit** | Step 3 writes no `NcVerified`; step 5 also writes no `NcVerified` (the event is raised only when `passed` is true, `Nonconformance.cs:249-252`) — assert this explicitly, because "no event" here has two different causes. |
| **Expected Notification** | n/a — no notification on either path. |
| **Cleanup** | `DELETE FROM qams.capa_action WHERE nc_id='@NC3'; DELETE FROM qams.nonconformance WHERE id='@NC3';` under bypass. |
| **Evidence** | Two HTTP response captures · two SQL status reads |
| **Notes** | Without this case a tester could conclude the raiser is allowed to "fail" their own verification, which would be a real SoD hole; the code does not permit it, and this pins that. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-011 — Nonconformance closure refused when the closer raised it (`SOD-CAPA-001`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-031 · RSK-RBAC-001 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — *(actorId == RaisedBy, effective = true, status = EffectivenessCheck)* |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `nc.approve` (`NonconformancesController.cs:107-108`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | NC `@NC` from TC-RBAC-SEC-008 at `status='EffectivenessCheck'`, `raised_by=@QM_A`. |
| **Test Data** | `POST /api/nonconformances/@NC/confirm-effectiveness` body `{"effective":true}` as `qm-a`. |
| **Steps** | 1. Sign in as `qm-a`. 2. Send the confirm-effectiveness call with `effective=true`. 3. Record status and `code`. 4. `SELECT status FROM qams.nonconformance WHERE id='@NC';`. |
| **Expected UI** | The *Confirm effectiveness* control renders (`nc.approve` granted) and the SoD message appears inline; stepper stays on *EffectivenessCheck*. |
| **Expected API** | `422`, `code="SOD-CAPA-001"`, title `Segregation of duties: the raiser cannot close their own nonconformance.` (`Nonconformance.cs:268`). |
| **Expected DB** | `status='EffectivenessCheck'`, unchanged. |
| **Expected Audit** | No `NcClosed` row in `audit.audit_trail` for `@NC`. |
| **Expected Notification** | n/a — no notification policy on a refused closure. |
| **Cleanup** | Leave `@NC` at *EffectivenessCheck* for TC-RBAC-SEC-012. |
| **Evidence** | HTTP response capture · SQL status read |
| **Notes** | `SOD-CAPA-001` and `SOD-CAPA-002` are distinct codes on distinct acts of the same aggregate; do not conflate them. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-012 — `SOD-CAPA-001` is not evaluated when `effective=false`: the raiser can bounce their own NC back  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-031 · RSK-RBAC-002 |
| **Level / Type / Technique** | API · Security (exploratory-derived negative) · Path testing — the early-return branch at `Nonconformance.cs:260-264` |
| **Priority / Severity / Automation** | High · Medium · Yes (unit) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `nc.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | NC `@NC` at `status='EffectivenessCheck'`, `raised_by=@QM_A` (carried from TC-RBAC-SEC-011). |
| **Test Data** | `POST /api/nonconformances/@NC/confirm-effectiveness` body `{"effective":false}` as `qm-a` (the raiser). |
| **Steps** | 1. As `qm-a`, send confirm-effectiveness with `effective=false`. 2. Record status. 3. `SELECT status FROM qams.nonconformance WHERE id='@NC';`. 4. Attempt the whole cycle again: complete a new action, submit for verification, and have `qm-b` verify `passed=true`, returning to `EffectivenessCheck`. 5. As `qm-a`, send `{"effective":true}` and record the code. |
| **Expected UI** | Step 1 moves the stepper back to *ActionPlan* with no error shown to `qm-a`; step 5 shows the SoD error. |
| **Expected API** | Step 2: `204 No Content` — **the raiser performs a state-changing act on their own NC without any SoD check**, because the `!effective` early return at `Nonconformance.cs:260-264` precedes the SoD comparison at `:266-269`. Step 5: `422` `code="SOD-CAPA-001"`. |
| **Expected DB** | After step 2: `status='ActionPlan'` — changed by the raiser. After step 5: still `EffectivenessCheck`. |
| **Expected Audit** | Step 2 writes **no** `audit.audit_trail` entry at all — `ConfirmEffectiveness(false, …)` raises no domain event, so the raiser's state change on their own record leaves no ledger trace beyond the generic `audit.field_change` rows written by `FieldChangeInterceptor`. Assert the `field_change` row exists with `property='Status'`, `old_value='EffectivenessCheck'`, `new_value='ActionPlan'`, `actor_id=@QM_A`. |
| **Expected Notification** | n/a — no notification on the bounce-back path. |
| **Cleanup** | `DELETE FROM qams.capa_action WHERE nc_id='@NC'; DELETE FROM qams.nonconformance WHERE id='@NC';` under bypass. |
| **Evidence** | HTTP 204 capture · SQL status reads at each step · `audit.field_change` row |
| **Notes** | This is the "laundering" path named in exploratory charter TC-RBAC-EXPL-005. The code as written is *not* an SoD breach — closure still requires an independent actor — but the raiser can indefinitely reopen their own NC, and only the field-change ledger records it. Flagged as new gap **GAP-RBAC-902** in the coverage note. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-013 — Quality-policy approval refused when the approver drafted it (`SOD-QP-001`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-049 · RSK-RBAC-001 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — *(approverId == CreatedByUserId, status = Draft)* |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `quality-policy.approve` (`QualityPolicyController.cs:49-50`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qm-a` drafted the policy with `POST /api/quality-policy`, so `qams.quality_policy.created_by_user_id = @QM_A` (stamped by `AuditStampInterceptor.cs:49`; the aggregate factory `QualityPolicy.Draft` records no author of its own). `status='Draft'`. |
| **Test Data** | Draft body `{"statement":"The laboratory commits to ISO 17025 conformity and continual improvement."}`; approve body `{"effectiveDate":"2026-09-01"}`. |
| **Steps** | 1. As `qm-a`, `POST /api/quality-policy` and capture `@POLICY`. 2. `POST /api/quality-policy/@POLICY/approve` body `{"effectiveDate":"2026-09-01"}` as `qm-a`. 3. Record status and `code`. 4. `SELECT status, created_by_user_id, approved_by_id, effective_date FROM qams.quality_policy WHERE id='@POLICY';`. |
| **Expected UI** | The quality-policy screen shows the *Approve* control (rendered behind `perms.can('quality-policy.approve')`, `quality-policy.component.ts:47`) and displays the SoD message; the version stays *Draft*. |
| **Expected API** | `422`, `code="SOD-QP-001"`, title `Segregation of duties: the preparer of a record cannot sign it off.` — the shared `EnsureSignerIsNotPreparer` message, not a policy-specific one (`QualityPolicy.cs:78`). |
| **Expected DB** | `status='Draft'`, `approved_by_id IS NULL`, `approved_at_utc IS NULL`, `effective_date IS NULL`. |
| **Expected Audit** | No `QualityPolicyApproved` row in `audit.audit_trail`. |
| **Expected Notification** | n/a — no notification policy is registered for quality-policy approval. |
| **Cleanup** | `DELETE FROM qams.quality_policy WHERE id='@POLICY';` under bypass. |
| **Evidence** | HTTP response capture · SQL result set |
| **Notes** | The refusal message is the generic preparer/signer text, which reads oddly for a policy *approval*; record as a wording observation, not a defect — the code is `SOD-QP-001` and that is what the test asserts. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-014 — `SOD-QP-001` precedes the `QP-010` state guard on an already-active policy  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-049 · RSK-RBAC-003 |
| **Level / Type / Technique** | API · Security (negative) · Path testing — guard ordering in `QualityPolicy.Approve` |
| **Priority / Severity / Automation** | Medium · Medium · Yes (unit) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `quality-policy.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Policy `@POLICY2` drafted by `qm-a` and approved by `qm-b`, so `status='Active'` and `created_by_user_id=@QM_A`. Both the SoD condition and the state condition are now violated for `qm-a`. |
| **Test Data** | `POST /api/quality-policy/@POLICY2/approve` body `{"effectiveDate":"2026-10-01"}`. |
| **Steps** | 1. Draft `@POLICY2` as `qm-a`; approve it as `qm-b`. 2. Re-send the approve call as `qm-a`. 3. Record status and `code`. 4. Re-send the approve call as `qm-b`. 5. Record status and `code`. |
| **Expected UI** | Neither attempt changes the displayed policy; the *Active* badge persists. |
| **Expected API** | Step 3: `422` `code="SOD-QP-001"` — `EnsureSignerIsNotPreparer` at `QualityPolicy.cs:78` runs before the `QP-010` check at `:80-84`. Step 5: `409 Conflict` `code="QP-010"`, title `Only a draft policy can be approved (current: Active).` |
| **Expected DB** | `qams.quality_policy` row for `@POLICY2` unchanged: `status='Active'`, `approved_by_id=@QM_B`. |
| **Expected Audit** | No second `QualityPolicyApproved` entry for `@POLICY2`. |
| **Expected Notification** | n/a — no notification defined. |
| **Cleanup** | `UPDATE qams.quality_policy SET status='Superseded' WHERE id='@POLICY2';` then delete under bypass, so a stale active policy does not leak into other cases. |
| **Evidence** | Two HTTP response captures with distinct codes and status classes |
| **Notes** | The 422-vs-409 split is the observable that distinguishes the two guards; assert both status and code. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-015 — A `SOD-QP-001` refusal must not supersede the prior active policy  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-049 · RSK-RBAC-008 |
| **Level / Type / Technique** | Integration · Data integrity (negative) · Data Flow testing — define `prior.Status` at `QualityPolicySlice.cs:79-82`, use/kill at `policy.Approve` (`:84`) |
| **Priority / Severity / Automation** | High · High · Yes (integration, rollback transaction) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `quality-policy.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; integration project with `QMS_ITEST_POSTGRES` set |
| **Preconditions** | Policy `@POLICY_V1` is `status='Active'` (drafted by `qm-b`, approved by `qm-a`). Policy `@POLICY_V2` is `status='Draft'` and was drafted by `qm-a` (`created_by_user_id=@QM_A`). |
| **Test Data** | `POST /api/quality-policy/@POLICY_V2/approve` body `{"effectiveDate":"2026-11-01"}` sent by `qm-a`. |
| **Steps** | 1. Record `SELECT id, status, version FROM qams.quality_policy WHERE tenant_id=@DEMO ORDER BY version;`. 2. Send the approve call as `qm-a`. 3. Record status and `code`. 4. Re-run the SELECT from step 1 and diff. |
| **Expected UI** | The policy history table is unchanged; v1 still shows *Active*, v2 still shows *Draft*. |
| **Expected API** | `422`, `code="SOD-QP-001"`. |
| **Expected DB** | **`@POLICY_V1.status` is still `'Active'`** — although `ApproveQualityPolicyHandler` calls `prior.Supersede()` (`QualityPolicySlice.cs:79-82`) *before* `policy.Approve(...)` (`:84`), the exception is thrown before `db.SaveChangesAsync` (`:85`), so the in-memory supersede is never persisted. `@POLICY_V2.status='Draft'`. Exactly zero rows in `qams.quality_policy` changed. |
| **Expected Audit** | No `QualityPolicyApproved` and **no** `audit.field_change` row with `entity_type='QualityPolicy'`, `property='Status'`, `old_value='Active'`, `new_value='Superseded'` — assert the absence explicitly, since this row is exactly what a premature save would produce. |
| **Expected Notification** | n/a — no notification defined. |
| **Cleanup** | Roll back the integration transaction; for a manual run, delete `@POLICY_V2` and restore `@POLICY_V1.status='Active'` under bypass. |
| **Evidence** | HTTP response capture · before/after SELECT diff · `audit.field_change` absence query |
| **Notes** | This is the "one-in-force" invariant (URS-049) exercised from the failure side: a refused approval must not leave the laboratory with *no* active quality policy. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-016 — Competency self-assessment refused: a trainee cannot score their own assessment (`SOD-COMP-001`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-051 · RSK-RBAC-001 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — *(assessorId == TraineeId, status = PendingTraining, score in range)* |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (`trainee`) holding `competencies.edit` · `competencies.edit` (`CompetenciesController.cs:36-37`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Competency `@COMP` assigned by `qm-a` with `trainee_id=@TRAINEE`, `status='PendingTraining'`, `validity_months=12`. A tenant-defined role granting exactly `competencies.view` + `competencies.edit` is assigned to `trainee@demo-lab.local` so the HTTP gate passes and the domain rule is the only thing left standing. |
| **Test Data** | Assign body `{"traineeId":"@TRAINEE","subject":"SOP-CHEM-014 Glucose hexokinase","documentId":null,"validityMonths":12}`; score body `{"score":85}`. |
| **Steps** | 1. As `admin`, create the bespoke role and assign it to `trainee` via `PUT /api/users/@TRAINEE/assigned-role`. 2. As `qm-a`, `POST /api/competencies` with the assign body → `@COMP`. 3. Sign in as `trainee@demo-lab.local` / `Tr-Pass-6!`. 4. `POST /api/competencies/@COMP/assessments` body `{"score":85}`. 5. Record status and `code`. 6. `SELECT status FROM qams.competency_record WHERE id='@COMP';` and `SELECT count(*) FROM qams.assessment_result WHERE competency_record_id='@COMP';`. |
| **Expected UI** | The competency detail page renders the score form (the form is not permission-hidden) and shows the SoD message on submit; the status pill stays *PendingTraining*. |
| **Expected API** | `422`, `code="SOD-COMP-001"`, title `Segregation of duties: a trainee cannot assess their own competency.` (`CompetencyRecord.cs:91`). **Not 403** — the HTTP gate passed; the refusal is a domain rule. |
| **Expected DB** | `qams.competency_record.status = 'PendingTraining'`; `count(*) = 0` in `qams.assessment_result` for `@COMP` — the append-only child list is untouched (`CompetencyRecord.cs:94`). |
| **Expected Audit** | No `CompetencyAuthorized` entry; assert one `audit.security_event`-free run (the SoD refusal writes no security event — record that as the measured behaviour). |
| **Expected Notification** | n/a — no notification policy on a refused assessment. |
| **Cleanup** | `DELETE FROM qams.competency_record WHERE id='@COMP';` under bypass; revoke the bespoke role from `trainee` and deactivate it. |
| **Evidence** | HTTP response capture · two SQL result sets (status + child count) |
| **Notes** | The gate/rule split is the point of this case: `competencies.edit` is *granted*, so 403 would be the wrong answer. Compare with TC-RBAC-SEC-022, where the same-looking button yields 403 instead. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-017 — `COMP-011` score-range guard is evaluated before `SOD-COMP-001`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-051 · RSK-RBAC-003 |
| **Level / Type / Technique** | API · Security (negative) · BVA + Path testing — score `101` is one above the upper bound `100`, with the SoD condition simultaneously violated |
| **Priority / Severity / Automation** | Medium · Medium · Yes (unit) |
| **Role / Permission / Tenant** | Analyst (`trainee`) holding `competencies.edit` · `competencies.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Competency `@COMP2` assigned to `@TRAINEE`, `status='PendingTraining'`; `trainee` holds the bespoke role from TC-RBAC-SEC-016. |
| **Test Data** | Score bodies, in order: `{"score":101}`, `{"score":-1}`, `{"score":100}`, `{"score":0}` — all sent by the trainee. |
| **Steps** | 1. As `trainee`, `POST /api/competencies/@COMP2/assessments` with `{"score":101}`. 2. Record status and `code`. 3. Repeat with `{"score":-1}`. 4. Repeat with `{"score":100}`. 5. Repeat with `{"score":0}`. 6. `SELECT count(*) FROM qams.assessment_result WHERE competency_record_id='@COMP2';`. |
| **Expected UI** | Steps 1 and 3 show the range error; steps 4 and 5 show the SoD error. The form remains editable throughout. |
| **Expected API** | Steps 2 and 3: `422` `code="COMP-011"`, title `Score must be between 0 and 100.` — the range guard at `CompetencyRecord.cs:83-86` precedes the SoD comparison at `:88-91`. Steps 4 and 5: `422` `code="SOD-COMP-001"` — inside the valid range, the SoD rule is what refuses. |
| **Expected DB** | `count(*) = 0` in `qams.assessment_result` for `@COMP2` after all four calls; `status='PendingTraining'`. |
| **Expected Audit** | No audit-trail entries for `@COMP2` from any of the four calls. |
| **Expected Notification** | n/a — refusals raise no notification. |
| **Cleanup** | `DELETE FROM qams.competency_record WHERE id='@COMP2';` under bypass. |
| **Evidence** | Four HTTP response captures showing the code flipping at the 0 and 100 boundaries · SQL child-row count |
| **Notes** | Boundaries: `-1`/`0` at the lower edge and `100`/`101` at the upper edge of the `score is < 0 or > 100` test. `PassMark = 80` (`CompetencyRecord.cs:33`) is a *status* boundary, not a validity boundary, and is out of this batch's scope. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-018 — Competency authorization refused: a trainee cannot authorize their own competency (`SOD-COMP-001`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-051 · RSK-RBAC-001 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — the second `SOD-COMP-001` site, *(actorId == TraineeId, status = Evaluated)* |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (`trainee`) holding `competencies.approve` · `competencies.approve` (`CompetenciesController.cs:44-45`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Competency `@COMP3` for `@TRAINEE`; `qm-a` has already scored it `{"score":88}` so `status='Evaluated'` (≥ `PassMark = 80`). The bespoke role assigned to `trainee` is widened to include `competencies.approve` so the HTTP gate passes. |
| **Test Data** | `POST /api/competencies/@COMP3/authorize` with no body, sent by `trainee`. |
| **Steps** | 1. As `qm-a`, score `@COMP3` with `{"score":88}`; confirm `status='Evaluated'`. 2. As `admin`, grant `competencies.approve` to the trainee's bespoke role via `PUT /api/roles/@ROLE/permissions` with a `reason`. 3. As `trainee`, `POST /api/competencies/@COMP3/authorize`. 4. Record status and `code`. 5. `SELECT status, authorized_by, expires_at FROM qams.competency_record WHERE id='@COMP3';`. |
| **Expected UI** | The *Authorize* button renders (`perms.can('competencies.approve')`, `competency-detail.component.ts:65`) and the SoD message appears on click; the status pill stays *Evaluated*. |
| **Expected API** | `422`, `code="SOD-COMP-001"`, title `Segregation of duties: a trainee cannot authorize their own competency.` (`CompetencyRecord.cs:108`). |
| **Expected DB** | `status='Evaluated'`, `authorized_by IS NULL`, `expires_at IS NULL`. |
| **Expected Audit** | No `CompetencyAuthorized` row in `audit.audit_trail` for `@COMP3`. However, step 2 **must** produce a `RolePermissionsChanged` entry carrying the operator's reason — assert it, since it is the Part-11 §11.10(e) hook for a privilege change (`Role.cs:220-225`). |
| **Expected Notification** | n/a — no notification on a refused authorization. |
| **Cleanup** | Revoke `competencies.approve` from the bespoke role with a reason; `DELETE FROM qams.competency_record WHERE id='@COMP3';` under bypass. |
| **Evidence** | HTTP response capture · SQL result set · the `RolePermissionsChanged` audit row from step 2 |
| **Notes** | Same code, different act, different comparison operand (`actorId` vs `assessorId`) — both must be covered because a refactor could easily drop one. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-019 — Supplier self-approval refused: the registrant cannot approve their own supplier (`SOD-SUP-001`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-050 · RSK-RBAC-001 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — *(actorId == RegisteredBy, status = PendingEvaluation)* |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `suppliers.approve` (`GovernanceControllers.cs:199-200`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qm-a` registered the supplier via `POST /api/suppliers` (that route carries no `[RequirePermission]`; it is gated only by `[RequireInternalActor]` at the command layer), so `qams.supplier.registered_by = @QM_A` and `status='PendingEvaluation'`. |
| **Test Data** | Register body `{"name":"Nordic Reagents AB","supplierType":"Reagents","branchId":null,"departmentId":null}`. |
| **Steps** | 1. As `qm-a`, `POST /api/suppliers` → `@SUPPLIER`. 2. `POST /api/suppliers/@SUPPLIER/approve` as `qm-a`. 3. Record status and `code`. 4. `SELECT status, registered_by, approved_by FROM qams.supplier WHERE id='@SUPPLIER';`. |
| **Expected UI** | The supplier detail page renders the *Approve* button because both conditions in `perms.can('suppliers.approve') && s.status === 'PendingEvaluation'` hold (`supplier-detail.component.ts:43`); clicking it surfaces the SoD message and the status pill stays *PendingEvaluation*. |
| **Expected API** | `422`, `code="SOD-SUP-001"`, title `Segregation of duties: the registrant cannot approve their own supplier.` (`Supplier.cs:91`). |
| **Expected DB** | `status='PendingEvaluation'`, `registered_by=@QM_A`, `approved_by IS NULL`, `suspension_reason IS NULL`. |
| **Expected Audit** | No `SupplierApproved` row in `audit.audit_trail` for `@SUPPLIER`. |
| **Expected Notification** | n/a — no notification policy on a refused supplier approval. |
| **Cleanup** | `DELETE FROM qams.supplier_certificate WHERE supplier_id='@SUPPLIER'; DELETE FROM qams.supplier WHERE id='@SUPPLIER';` under bypass. |
| **Evidence** | HTTP response capture · SQL result set · screenshot of the rendered *Approve* button proving the UI does not hide it |
| **Notes** | The SPA shows the control to the registrant — the SoD rule is server-side only. That is correct per `permissions.service.ts:13-14` ("affordance, never a security boundary") but is worth capturing as evidence for the auditor. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-020 — `SUP-010` already-approved guard is evaluated before `SOD-SUP-001`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-050 · RSK-RBAC-003 |
| **Level / Type / Technique** | API · Security (negative) · Path testing — guard ordering in `Supplier.Approve` |
| **Priority / Severity / Automation** | Medium · Medium · Yes (unit) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `suppliers.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Supplier `@SUPPLIER2` registered by `qm-a` and already approved by `qm-b`, so `status='Approved'`, `registered_by=@QM_A`, `approved_by=@QM_B`. |
| **Test Data** | `POST /api/suppliers/@SUPPLIER2/approve` re-sent by `qm-a`. |
| **Steps** | 1. Register `@SUPPLIER2` as `qm-a`; approve as `qm-b`. 2. Re-send approve as `qm-a`. 3. Record status and `code`. 4. `SELECT status, approved_by FROM qams.supplier WHERE id='@SUPPLIER2';`. |
| **Expected UI** | The *Approve* button is **not** rendered — the template also requires `s.status === 'PendingEvaluation'` (`supplier-detail.component.ts:43`) — so this case is driven by direct API call. |
| **Expected API** | `409 Conflict`, `code="SUP-010"`, title `Supplier is already approved.` — **not** `SOD-SUP-001`, because the state check at `Supplier.cs:84-87` precedes the SoD comparison at `:89-92`. |
| **Expected DB** | `status='Approved'`, `approved_by=@QM_B` — unchanged; the registrant's re-approval does not overwrite the approver id. |
| **Expected Audit** | No second `SupplierApproved` row for `@SUPPLIER2`. |
| **Expected Notification** | n/a — no notification on a refused re-approval. |
| **Cleanup** | `DELETE FROM qams.supplier WHERE id='@SUPPLIER2';` under bypass. |
| **Evidence** | HTTP response capture showing `409 SUP-010` · SQL result set proving `approved_by` was not overwritten |
| **Notes** | Overwriting `approved_by` would be a Part-11 attribution defect; asserting the column is unchanged is the substantive check here, not just the status code. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-021 — Audit sign-off by the auditor who raised every finding is accepted — no SoD guard exists  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · no URS covers SoD on audit sign-off — trace to `src/NT.QAMS.Domain/AuditManagement/Audit.cs:172-193` · RSK-RBAC-009 |
| **Level / Type / Technique** | API · Security (negative-by-omission) · Error Guessing — probing for a guard the sibling aggregates all have |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional, once the gap is resolved) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`) · `audits.create` then `audits.sign` (`AuditsController.cs:30,70`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Audit `@AUDIT` scheduled by `qm-a` with `lead_auditor_id=@QM_A` and `created_by_user_id=@QM_A`; one checklist item; started so `status='InProgress'`; one `Minor`-graded finding raised by `qm-a` and acknowledged with an NC id; the checklist item answered so no `AUD-017` is possible. |
| **Test Data** | Checklist item `{"question":"Are calibration records retained for 5 years?"}`; verdict `{"verdict":"NonConform","evidence":"Records only to 2024"}`; finding `{"grade":"Minor","description":"Retention shortfall"}`; NC id from `POST /api/nonconformances`. |
| **Steps** | 1. As `qm-a`, schedule `@AUDIT`, add the checklist item, start the audit. 2. Answer the item and raise the finding as `qm-a`. 3. Acknowledge the finding's NC. 4. `POST /api/audits/@AUDIT/sign-off` as `qm-a`. 5. Record status. 6. `SELECT status, lead_auditor_id, signed_off_by, created_by_user_id FROM qams.audit WHERE id='@AUDIT';`. |
| **Expected UI** | The audit page moves to *SignedOff* and names `qm-a` as signer — the same person who planned the audit, answered its checklist, raised its finding and signed it off. |
| **Expected API** | `204 No Content`. **No `SOD-AQ-001` and no `SOD-*` code of any kind is raised** — `Audit.SignOff` (`Audit.cs:172-193`) calls `RequireInProgress`, then checks `AUD-017` unanswered items and `AUD-018` unacknowledged findings, and never calls `EnsureSignerIsNotPreparer` nor compares `actorId` with `LeadAuditorId` or with any finding's raiser. |
| **Expected DB** | `qams.audit`: `status='SignedOff'`, `signed_off_by = lead_auditor_id = created_by_user_id = @QM_A`. |
| **Expected Audit** | One `AuditSignedOff` row in `audit.audit_trail` naming `@QM_A` — with nothing recording that the signer was also the preparer. |
| **Expected Notification** | n/a — no notification policy on audit sign-off. |
| **Cleanup** | Record `@AUDIT` in the run log; delete the NC created in step 3 under bypass. |
| **Evidence** | HTTP 204 capture · SQL result set showing the three identical uuids in one row |
| **Notes** | **Gap-dependent on GAP-RBAC-017.** Acceptance criteria to implement against: `Audit.SignOff(Guid actorId, DateTimeOffset at)` must call `EnsureSignerIsNotPreparer(actorId, "SOD-AUD-001")` (new code) **and** refuse when `actorId == LeadAuditorId`, returning `422` with `code="SOD-AUD-001"` and title "Segregation of duties: the auditor cannot sign off their own audit."; the refusal must precede the `AUD-017`/`AUD-018` content guards so an auditor is told the identity reason first, matching `ValidationStudy.SignOff`. A corresponding URS requirement must be raised, since none exists (`URS-031`/`039`/`049` are the only SoD requirements and none mentions audits). Do **not** record this case as a Pass on the current build. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-022 — A hidden *Approve supplier* button does not protect the endpoint: direct POST by an Analyst  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-004 |
| **Level / Type / Technique** | API · Security (negative) · Use Case testing — the "hidden control" abuse case, driven outside the SPA |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (`analyst-n`), seeded *Analyst* role (65 keys, **no** `suppliers.approve`) · `suppliers.approve` required · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; SPA `localhost:4200` started via `scripts/dev-up.ps1` |
| **Preconditions** | Supplier `@SUPPLIER3` registered by `qm-a`, `status='PendingEvaluation'`. `analyst-n` is signed in to the SPA and to `curl.exe` with a valid access JWT. |
| **Test Data** | Access token for `analyst-n@demo-lab.local` / `An-North-Pass-5!`; target `POST /api/suppliers/@SUPPLIER3/approve`. |
| **Steps** | 1. As `analyst-n`, open `/suppliers/@SUPPLIER3` in the SPA and confirm the *Approve* button is absent (`perms.can('suppliers.approve')` is false, `supplier-detail.component.ts:43`). 2. Read `GET /api/auth/me/privileges` and confirm `suppliers.approve` is not in `permissions`. 3. With `curl.exe`, `POST /api/suppliers/@SUPPLIER3/approve` carrying the same bearer token. 4. Record status, `Content-Type` and `code`. 5. `SELECT status, approved_by FROM qams.supplier WHERE id='@SUPPLIER3';`. 6. Repeat step 3 against the version mirror `POST /api/v1/suppliers/@SUPPLIER3/approve` and confirm the identical outcome. |
| **Expected UI** | No *Approve* control anywhere on the page; no error is displayed, because the SPA never issues the call. |
| **Expected API** | `403 Forbidden`, `application/problem+json`, `code="AUTHZ-403"`, title `You do not have permission to perform this action.` — written by `RequirePermissionAttribute.OnAuthorizationAsync` (`RequirePermissionAttribute.cs:54-60`) before the MediatR pipeline is ever entered. Identical response on the `/api/v1/...` mirror. |
| **Expected DB** | `qams.supplier`: `status='PendingEvaluation'`, `approved_by IS NULL`. |
| **Expected Audit** | No `SupplierApproved` row. Record whether an `audit.security_event` row is written for the refusal — measured behaviour is that the HTTP gate writes none; assert `count(*) = 0` for `event_type` values matching `%FORBIDDEN%` in the window. |
| **Expected Notification** | n/a — no notification on an authorization refusal. |
| **Cleanup** | `DELETE FROM qams.supplier WHERE id='@SUPPLIER3';` under bypass. |
| **Evidence** | Screenshot of the button-free page · `GET /auth/me/privileges` body · two `curl.exe` response captures (`/api` and `/api/v1`) · SQL result set |
| **Notes** | Use `curl.exe`, not PowerShell's `Invoke-WebRequest` — PowerShell 5.1 drops manually supplied headers (conventions §3). The `/api/v1` repetition matters because every route is dual-exposed by `Asp.Versioning.Mvc` and a gate applied to only one surface would be invisible to a single-URL test. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-023 — A permitted actor bypassing the UI still meets the domain rule: direct POST to authorize own competency  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-051 · RSK-RBAC-004 |
| **Level / Type / Technique** | API · Security (negative) · Use Case testing — distinguishing a *gate* refusal (403) from a *rule* refusal (422) on the same bypass technique |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (`trainee`) on the bespoke role holding `competencies.approve` · `competencies.approve` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; SPA `localhost:4200` |
| **Preconditions** | Competency `@COMP4` for `@TRAINEE` at `status='Evaluated'` (scored `92` by `qm-a`). `trainee` holds the bespoke role granting `competencies.view` + `competencies.edit` + `competencies.approve`. |
| **Test Data** | Bearer token for `trainee@demo-lab.local` / `Tr-Pass-6!`; target `POST /api/competencies/@COMP4/authorize`. |
| **Steps** | 1. As `trainee`, open the competency page; confirm the *Authorize* button **is** rendered (`competency-detail.component.ts:65`, `perms.can('competencies.approve')` true and `c.status === 'Evaluated'`). 2. With `curl.exe`, send the POST directly with the same token, bypassing the SPA. 3. Record status and `code`. 4. `SELECT status, authorized_by FROM qams.competency_record WHERE id='@COMP4';`. |
| **Expected UI** | The button renders and is clickable; clicking it produces the same server refusal the direct call produces. |
| **Expected API** | `422`, `code="SOD-COMP-001"` — **not** `403 AUTHZ-403`. The privilege gate passes; the identity rule refuses. |
| **Expected DB** | `status='Evaluated'`, `authorized_by IS NULL`, `expires_at IS NULL`. |
| **Expected Audit** | No `CompetencyAuthorized` row for `@COMP4`. |
| **Expected Notification** | n/a — no notification on a refused authorization. |
| **Cleanup** | `DELETE FROM qams.competency_record WHERE id='@COMP4';` under bypass; return the bespoke role to `competencies.view` + `competencies.edit` with a recorded reason. |
| **Evidence** | `curl.exe` response capture · SQL result set · screenshot of the rendered button |
| **Notes** | Paired deliberately with TC-RBAC-SEC-022: same bypass technique, two different defence layers, two different status codes. A test suite that only asserts "the call fails" cannot tell the two layers apart and would not detect the loss of either one. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-024 — Direct Angular navigation to `/roles` by an Analyst: the page loads, then every call 403s  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-007 |
| **Level / Type / Technique** | E2E (Playwright) · Security (negative) · State Transition testing — route activation state vs privilege state |
| **Priority / Severity / Automation** | High · Medium · Yes (Playwright e2e) |
| **Role / Permission / Tenant** | Analyst (`analyst-n`), seeded *Analyst* role — holds **neither** `roles.view` nor `roles.manage` · `roles.view` required by every `RolesController` GET (`RolesController.cs:24,29,34`) · `demo-lab` |
| **Environment** | SPA `localhost:4200` (started with `scripts/dev-up.ps1`) + API `:5080` Development |
| **Preconditions** | `analyst-n` signed in at `localhost:4200/t/demo-lab`. The `/roles` route is declared at `app.routes.ts:394-396` under the tenant shell with **only** `tenantOnlyGuard` — no permission guard exists on any route (`app.routes.ts`, confirmed by GAP-RBAC-007). |
| **Test Data** | Direct URL `http://localhost:4200/roles`; no query parameters. |
| **Steps** | 1. Sign in as `analyst-n`. 2. Confirm the left navigation shows no *Roles & Privileges* entry. 3. Type `http://localhost:4200/roles` into the address bar and press Enter. 4. Record whether the route activates and the component renders. 5. Capture the network log for `GET /api/roles` and `GET /api/roles/catalog`. 6. Record the rendered page state after the failed calls. |
| **Expected UI** | **The route activates and `RolesComponent` renders** — `tenantOnlyGuard` returns `true` for a non-platform user (`role.guard.ts:17-21`) and nothing else guards the path. The page chrome, headings and empty table shell are visible; the roles list stays empty and the component's error state is shown. |
| **Expected API** | `GET /api/roles` → `403` `application/problem+json` `code="AUTHZ-403"`; `GET /api/roles/catalog` → `403` `AUTHZ-403`. No `200` on any `RolesController` route. |
| **Expected DB** | No rows written or read that the actor is not entitled to; `SELECT count(*) FROM qams.role WHERE tenant_id=@DEMO` is unchanged at 5 (the seeded set) plus any bespoke roles created by earlier cases. |
| **Expected Audit** | No `audit.audit_trail` rows generated by the navigation or by the two refused GETs. |
| **Expected Notification** | n/a — navigation raises no notification. |
| **Cleanup** | Sign out; no data state to restore. |
| **Evidence** | Playwright trace · screenshot of the rendered but empty `/roles` page · HAR entries showing both `403 AUTHZ-403` responses |
| **Notes** | This documents **GAP-RBAC-007** as observed behaviour, not as a defect to be fixed by this case: the server refuses correctly, and the SPA is affordance-only by design (`permissions.service.ts:13-14`). What the case pins is the *observable consequence* — an unprivileged user can enumerate the existence and layout of an administration screen. Assert that no role names, permission keys or user identities appear anywhere in the rendered DOM. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-025 — Branch-restricted read: an out-of-scope nonconformance is invisible, and its direct-id GET answers 404  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-097 · RSK-RBAC-005 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — partition *record.BranchId ∉ AllowedBranchIds* |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (`analyst-n`), working scope = branch `North` · `nc.view` is not gated at the HTTP layer; the list route carries `[Authorize]` only · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `PUT /api/users/@ANALYST_N/scope` body `{"branchIds":["@BR_NORTH"],"departmentIds":[]}` executed by `admin` → exactly one row in `qams.user_branch_access` for `@ANALYST_N`. Three NCs exist, raised by `qm-a`: `@NC_N` with `branch_id=@BR_NORTH`, `@NC_S` with `branch_id=@BR_SOUTH`, `@NC_NULL` with `branch_id IS NULL`. |
| **Test Data** | Raise bodies differ only in `branchId`: `"@BR_NORTH"`, `"@BR_SOUTH"`, `null`; each `{"title":"Scope probe","description":"…","severity":1,"likelihood":1,"sourceType":"Internal"}`. |
| **Steps** | 1. As `admin`, set the scope per Preconditions and verify `SELECT branch_id FROM qams.user_branch_access WHERE user_id='@ANALYST_N';` returns exactly `@BR_NORTH`. 2. Sign in as `analyst-n`. 3. `GET /api/nonconformances?page=1&pageSize=50` and list the returned ids. 4. `GET /api/nonconformances/@NC_S`. 5. Record status and `code`. 6. `GET /api/nonconformances/@NC_N` and `GET /api/nonconformances/@NC_NULL`. |
| **Expected UI** | The NC register lists exactly two rows — the North record and the unattributed record. The South record does not appear and the total count reflects two, not three. |
| **Expected API** | Step 3: `200` with a paged envelope whose `items` contain `@NC_N` and `@NC_NULL` and **not** `@NC_S`. Step 5: `404 Not Found` `code="NC-404"` — the row is filtered out of the query by `ApplyTenantAndScopeFilter` (`AppDbContext.cs:203-211`), so the loader's not-found path is what answers, and the caller cannot distinguish "does not exist" from "not yours". Step 6: `200` for both. |
| **Expected DB** | `SELECT count(*) FROM qams.nonconformance WHERE tenant_id=@DEMO` is `3` when read under `app.bypass_rls='on'` — the row exists; only the application filter hides it. |
| **Expected Audit** | No audit-trail rows from read operations. |
| **Expected Notification** | n/a — reads raise no notification. |
| **Cleanup** | `PUT /api/users/@ANALYST_N/scope` body `{"branchIds":[],"departmentIds":[]}` to restore unrestricted; delete the three NCs under bypass. |
| **Evidence** | Paged list body · `404` response capture · SQL count under bypass proving the row exists |
| **Notes** | The 404 (invisible read) versus 422 (refused write, TC-RBAC-SEC-027) asymmetry is deliberate and is called out in the module front matter §3.4; both halves must be asserted or the boundary is only half tested. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-026 — Branch-restricted read: an unattributed (`branch_id IS NULL`) record stays visible  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-097 · RSK-RBAC-005 |
| **Level / Type / Technique** | API · Functional (positive) · BVA — the null boundary of the `AllowedBranchIds.Contains(...)` term |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (`analyst-n`), working scope = branch `North` · n/a — the list route is `[Authorize]`-only · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Same fixture as TC-RBAC-SEC-025: `@NC_NULL` has `branch_id IS NULL` **and** `department_id IS NULL`; `analyst-n` is branch-restricted to `North`. |
| **Test Data** | `@NC_NULL`; also an equipment item `@EQ_NULL` created with `branchId=null` to prove the rule holds across `IAllocatable` types. |
| **Steps** | 1. As `analyst-n`, `GET /api/nonconformances/@NC_NULL` — record status. 2. `GET /api/equipment/@EQ_NULL` — record status. 3. Widen the scope to both branches (`{"branchIds":["@BR_NORTH","@BR_SOUTH"],"departmentIds":[]}`) and repeat step 1 — the record must still be visible. 4. Clear the scope to `{"branchIds":[],"departmentIds":[]}` and repeat step 1. |
| **Expected UI** | The unattributed record appears in the register under all three scope settings and carries no branch chip. |
| **Expected API** | `200 OK` in every step. The filter term `e.BranchId == null` (`AppDbContext.cs:206`) short-circuits the containment test, and `IUserPrivileges.CanAccessBranch(null)` is likewise true (`PrivilegeResolution.cs:42`). |
| **Expected DB** | `qams.nonconformance.branch_id IS NULL` and `qams.equipment_item.branch_id IS NULL` throughout; nothing is written. |
| **Expected Audit** | No audit-trail rows from reads. |
| **Expected Notification** | n/a — reads raise no notification. |
| **Cleanup** | Delete `@EQ_NULL` under bypass; leave the scope cleared. |
| **Evidence** | Three `200` response captures across the three scope settings · SQL confirming the null branch column |
| **Notes** | URS-097 states this explicitly ("unattributed (tenant-wide) records remain visible"). It is a boundary, not an oversight — a test that treated null as out-of-scope would produce a false defect. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-027 — Branch-restricted write refused with `SCOPE-001` when creating outside the working scope  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-097 · RSK-RBAC-005 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — DT-4 row 3 *(restriction {North}, record branch South, write)* |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (`analyst-n`), working scope = branch `North` · n/a — `POST /api/nonconformances` carries no `[RequirePermission]`; the command policy is `[RequireInternalActor]` (`NcWorkflowCommands.cs:12`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `qams.user_branch_access` holds exactly `(@ANALYST_N, @BR_NORTH, @DEMO)`; `@BR_SOUTH` exists and is active. |
| **Test Data** | `POST /api/nonconformances` body `{"title":"Cross-branch write probe","description":"Attempt to file against South","severity":3,"likelihood":2,"sourceType":"Internal","branchId":"@BR_SOUTH","departmentId":null}` → `rpn = 6` if it were to persist. |
| **Steps** | 1. Confirm the scope row per Preconditions. 2. As `analyst-n`, send the POST. 3. Record status and `code`. 4. `SELECT count(*) FROM qams.nonconformance WHERE title='Cross-branch write probe';` under `app.bypass_rls='on'`. 5. Repeat the POST with `"branchId":"@BR_NORTH"` and record status. |
| **Expected UI** | The NC form's branch picker offers only `North` (the SPA uses `PermissionsService.branchIds` as a hint, `permissions.service.ts:34`); the case is therefore driven by direct API call, and the returned message "You are not permitted to work in the selected branch." is shown inline if the call is made. |
| **Expected API** | Step 3: `422`, `code="SCOPE-001"`, title `You are not permitted to work in the selected branch.` — thrown by `OrgScopeGuardInterceptor.Guard` on the `Added` entry (`OrgScopeGuardInterceptor.cs:53-58`) inside the same `SaveChanges` that would have persisted it. Step 5: `200 OK` with `{"id":"…"}`. |
| **Expected DB** | Step 4: `count(*) = 0` — the interceptor throws during `SavingChanges`, so nothing is committed. After step 5 exactly one row exists, with `branch_id=@BR_NORTH`. |
| **Expected Audit** | Step 3 writes no `NcRaised` entry and no `audit.field_change` rows — the transaction never reaches commit. Step 5 writes one `NcRaised` entry carrying `Severity=3` and `Rpn=6`. |
| **Expected Notification** | n/a — no notification policy on a refused create. |
| **Cleanup** | Delete the NC created in step 5 under bypass; clear the scope. |
| **Evidence** | Two HTTP response captures · SQL count under bypass · audit-trail row from the successful step |
| **Notes** | The write side is a *refusal* (422) while the read side is *invisibility* (404, TC-RBAC-SEC-025). Assert both codes verbatim: a generic "422 error" assertion would not distinguish `SCOPE-001` from `SCOPE-002`. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-028 — Department-restricted write refused with `SCOPE-002`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-097 · RSK-RBAC-005 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — DT-4 department analogue, *(restriction {Chemistry}, record department Microbiology, write)* |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (`analyst-n`), working scope = department `Chemistry`, **no** branch restriction · n/a — `[RequireInternalActor]` only · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `PUT /api/users/@ANALYST_N/scope` body `{"branchIds":[],"departmentIds":["@DEP_CHEM"]}` → exactly one row in `qams.user_department_access`, zero rows in `qams.user_branch_access`. |
| **Test Data** | `POST /api/nonconformances` body `{"title":"Cross-department write probe","description":"Attempt to file against Microbiology","severity":2,"likelihood":2,"sourceType":"Internal","branchId":null,"departmentId":"@DEP_MICRO"}`. |
| **Steps** | 1. Set the scope and verify both scope tables per Preconditions. 2. As `analyst-n`, send the POST. 3. Record status and `code`. 4. `SELECT count(*) FROM qams.nonconformance WHERE title='Cross-department write probe';` under bypass. 5. Repeat with `"departmentId":"@DEP_CHEM"` and record status. 6. Repeat with `"departmentId":null` and record status. |
| **Expected UI** | The department picker offers only `Chemistry`; the direct call surfaces "You are not permitted to work in the selected department." |
| **Expected API** | Step 3: `422` `code="SCOPE-002"` (`OrgScopeGuardInterceptor.cs:60-65`). Step 5: `200 OK`. Step 6: `200 OK` — `CanAccessDepartment(null)` is true (`PrivilegeResolution.cs:45`), so an unattributed write is permitted even under a department restriction. |
| **Expected DB** | Step 4: `count(*) = 0`. After steps 5 and 6, two rows exist: one with `department_id=@DEP_CHEM`, one with `department_id IS NULL`. |
| **Expected Audit** | Two `NcRaised` entries total (from steps 5 and 6); none from step 3. |
| **Expected Notification** | n/a — no notification on a refused or routine create. |
| **Cleanup** | Delete both NCs under bypass; clear the scope with `{"branchIds":[],"departmentIds":[]}`. |
| **Evidence** | Three HTTP response captures · SQL count under bypass |
| **Notes** | Step 6 is the department mirror of TC-RBAC-SEC-026's read-side null boundary — the null case is permissive on **both** sides of the boundary, and that symmetry is what the case establishes. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-029 — With both dimensions out of scope, `SCOPE-001` wins: branch is checked first  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-097 · RSK-RBAC-003 |
| **Level / Type / Technique** | API · Security (negative) · Pairwise — the 2×2 of (branch in/out of scope) × (department in/out of scope) on a single write |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (`analyst-n`), working scope = branch `North` **and** department `Chemistry` · n/a — `[RequireInternalActor]` only · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `PUT /api/users/@ANALYST_N/scope` body `{"branchIds":["@BR_NORTH"],"departmentIds":["@DEP_CHEM"]}` → one row in each scope table. |
| **Test Data** | Four `POST /api/nonconformances` bodies, identical but for the pair: (a) `North`/`Chemistry`; (b) `North`/`Microbiology`; (c) `South`/`Chemistry`; (d) `South`/`Microbiology`. All `{"title":"Pairwise scope probe <n>","severity":1,"likelihood":1,"sourceType":"Internal"}`. |
| **Steps** | 1. Set the two-dimensional scope. 2. Send body (a); record status. 3. Send body (b); record status and `code`. 4. Send body (c); record status and `code`. 5. Send body (d); record status and `code`. 6. `SELECT title, branch_id, department_id FROM qams.nonconformance WHERE title LIKE 'Pairwise scope probe%';` under bypass. |
| **Expected UI** | Only combination (a) is reachable through the SPA's pickers; (b)–(d) are direct API calls. |
| **Expected API** | (a) `200 OK`. (b) `422` `code="SCOPE-002"`. (c) `422` `code="SCOPE-001"`. (d) `422` `code="SCOPE-001"` — **branch wins** because `CanAccessBranch` is evaluated before `CanAccessDepartment` in the interceptor loop (`OrgScopeGuardInterceptor.cs:53-65`), so the caller is never told the department is also wrong. |
| **Expected DB** | Exactly one row matches the title prefix, with `branch_id=@BR_NORTH` and `department_id=@DEP_CHEM`. |
| **Expected Audit** | Exactly one `NcRaised` entry across the four calls. |
| **Expected Notification** | n/a — no notification on these paths. |
| **Cleanup** | Delete the single persisted NC under bypass; clear the scope. |
| **Evidence** | Four HTTP response captures · one SQL result set proving a single row survived |
| **Notes** | Case (d) is the one that pins the precedence. If the two checks were reordered the response code would flip to `SCOPE-002` and every other assertion in this case would still pass — which is why the code string, not just the status, is asserted. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-030 — Re-pointing an in-scope record to an out-of-scope branch is refused on the `Modified` entry  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-097 · RSK-RBAC-005 |
| **Level / Type / Technique** | Integration · Security (negative) · Data Flow testing — `IAllocatable.BranchId` defined at load, redefined in the handler, used by the interceptor at `SavingChanges` |
| **Priority / Severity / Automation** | Critical · Critical · Yes (integration) |
| **Role / Permission / Tenant** | Analyst (`analyst-n`), working scope = branch `North` · n/a — the mutating command carries `[RequireInternalActor]` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; integration project with `QMS_ITEST_POSTGRES` |
| **Preconditions** | Equipment item `@EQ_N` exists with `branch_id=@BR_NORTH` — in scope, therefore loadable. `analyst-n` is branch-restricted to `North`. |
| **Test Data** | An update that changes only the branch allocation: target `branch_id = @BR_SOUTH`. |
| **Steps** | 1. As `analyst-n`, `GET /api/equipment/@EQ_N` → `200` (the row is in scope and loadable). 2. Issue the update that re-points `@EQ_N` to `@BR_SOUTH`. 3. Record status and `code`. 4. `SELECT branch_id FROM qams.equipment_item WHERE id='@EQ_N';` under bypass. 5. Re-issue the update targeting `@BR_NORTH` and record status. |
| **Expected UI** | The equipment edit form's branch picker offers only `North`, so the re-point is exercised by direct API call. |
| **Expected API** | Step 3: `422`, `code="SCOPE-001"` — the interceptor inspects `EntityState.Modified` entries as well as `Added` (`OrgScopeGuardInterceptor.cs:47-50`), which is the only thing standing between a restricted actor and moving a record out of their own reach. Step 5: `204 No Content` (or the route's documented success status). |
| **Expected DB** | Step 4: `branch_id = @BR_NORTH` — unchanged. The re-point never commits. |
| **Expected Audit** | No `audit.field_change` row with `entity_type='EquipmentItem'`, `property='BranchId'`, `new_value=@BR_SOUTH`. Assert the absence explicitly. |
| **Expected Notification** | n/a — no notification on a refused re-allocation. |
| **Cleanup** | Roll back the integration transaction; for a manual run, leave `@EQ_N` on `North`. |
| **Evidence** | Two HTTP response captures · SQL branch read · `audit.field_change` absence query |
| **Notes** | This is the case the interceptor's own doc comment exists for (`OrgScopeGuardInterceptor.cs:11-16`): the query filter blocks loading out-of-scope rows, but it cannot stop a **re-allocation** of an in-scope row, which is a self-inflicted denial of service at best and an evidence-hiding move at worst. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-031 — Empty scope lists mean unrestricted, and clearing a scope restores tenant-wide reach immediately  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-097, URS-096 · RSK-RBAC-005 |
| **Level / Type / Technique** | API · Functional (positive) · State Transition testing — `Branch-restricted → Unrestricted` on the working-scope state machine |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (`analyst-n`) · `users.manage` required of the administrator performing the scope change (`UsersController.cs:51-52`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `analyst-n` is branch-restricted to `North`; NCs `@NC_N` (North) and `@NC_S` (South) both exist. `analyst-n` holds a **valid, unexpired** access JWT issued *before* the scope change — the immediacy property is part of what this case proves. |
| **Test Data** | Scope bodies `{"branchIds":["@BR_NORTH"],"departmentIds":[]}` then `{"branchIds":[],"departmentIds":[]}`. |
| **Steps** | 1. As `analyst-n`, `GET /api/nonconformances` and record that `@NC_S` is absent. 2. As `admin`, `PUT /api/users/@ANALYST_N/scope` body `{"branchIds":[],"departmentIds":[]}` → `204`. 3. **Without re-authenticating**, as `analyst-n` on the same token, `GET /api/nonconformances` again. 4. `GET /api/nonconformances/@NC_S`. 5. `SELECT count(*) FROM qams.user_branch_access WHERE user_id='@ANALYST_N';`. |
| **Expected UI** | The register grows from two rows to three between steps 1 and 3 on a page refresh, with no sign-out required. |
| **Expected API** | Step 1: `@NC_S` absent. Step 2: `204 No Content`. Step 3: `200` with `@NC_S` now present. Step 4: `200 OK` — previously `404`. |
| **Expected DB** | Step 5: `count(*) = 0` — `UserAccount.SetScope` replaces the owned collections wholesale (`UserAccount.cs:186-203`) and an empty list is the explicit widest case, not a no-op. |
| **Expected Audit** | One `audit.audit_trail` row `event_type='UserScopeChanged'` carrying `UserId=@ANALYST_N`, empty `BranchIds`, empty `DepartmentIds` and `IsUnrestricted=true` (`UserAccount.cs:290-294`), stamped with the **tenant id** `@DEMO` (the RP-D1 fix, pinned by `UserEventTenantStampTests`). |
| **Expected Notification** | n/a — no notification policy on a scope change. |
| **Cleanup** | Delete `@NC_N` and `@NC_S` under bypass; the user is left unrestricted, which is the fixture default. |
| **Evidence** | Four HTTP response captures · SQL count of the scope table · the `UserScopeChanged` audit row with a non-empty `tenant_id` |
| **Notes** | The "same unexpired token" requirement is essential: privilege and scope are resolved per request by `ActiveSessionMiddleware` → `PrivilegeResolver` and are deliberately uncached (`PrivilegeResolution.cs:54-60`). If the test re-authenticates between steps it proves nothing about immediacy. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-032 — `SCOPE-003` / `SCOPE-004`: a scope may not point at org units the tenant does not have  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-097 · RSK-RBAC-006 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — partitions *(unknown id)*, *(other tenant's id)*, *(`Guid.Empty`)*, *(valid id)* |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Tenant Administrator (`admin`) · `users.manage` (`UsersController.cs:51-52`) plus command policy `[RequirePermissionPolicy(users, Manage)]` (`UserManagement.cs:288-… /` scope command at `:299-301`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; a second tenant `rival-lab` provisioned with its own branch `@BR_RIVAL` |
| **Preconditions** | `demo-lab` has branches `@BR_NORTH`, `@BR_SOUTH` and departments `@DEP_CHEM`, `@DEP_MICRO`. `rival-lab` has branch `@BR_RIVAL`. `analyst-n` currently unrestricted. |
| **Test Data** | Four bodies: (a) `{"branchIds":["00000000-0000-0000-0000-0000000000ff"],"departmentIds":[]}`; (b) `{"branchIds":["@BR_RIVAL"],"departmentIds":[]}`; (c) `{"branchIds":["00000000-0000-0000-0000-000000000000"],"departmentIds":[]}`; (d) `{"branchIds":[],"departmentIds":["00000000-0000-0000-0000-0000000000ee"]}`. |
| **Steps** | 1. As `admin`, `PUT /api/users/@ANALYST_N/scope` with body (a); record status and `code`. 2. Repeat with body (b); record status and `code`. 3. Repeat with body (c); record status. 4. Repeat with body (d); record status and `code`. 5. `SELECT count(*) FROM qams.user_branch_access WHERE user_id='@ANALYST_N'; SELECT count(*) FROM qams.user_department_access WHERE user_id='@ANALYST_N';`. |
| **Expected UI** | The scope editor only offers the tenant's own org units; these bodies are constructed by direct API call. |
| **Expected API** | (a) `422` `code="SCOPE-003"`, title `One or more selected branches do not exist.` (`UserManagement.cs:268`). (b) `422` `code="SCOPE-003"` — the `db.Branches` count runs under the tenant query filter and RLS, so another tenant's branch is indistinguishable from a non-existent one; **no cross-tenant id is ever accepted**. (c) `204 No Content` — `Guid.Empty` entries are filtered out before the existence check (`UserManagement.cs:261`), so the body reduces to "unrestricted". (d) `422` `code="SCOPE-004"`, title `One or more selected departments do not exist.` (`UserManagement.cs:278`). |
| **Expected DB** | Step 5: both counts `0`. No partial scope is ever written — the checks precede `user.SetScope(...)` at `UserManagement.cs:283`. |
| **Expected Audit** | One `UserScopeChanged` row from case (c) only (the successful call); none from (a), (b) or (d). |
| **Expected Notification** | n/a — no notification on a scope change. |
| **Cleanup** | Leave `analyst-n` unrestricted; no rows to delete. |
| **Evidence** | Four HTTP response captures · two SQL counts · confirmation that (a) and (b) return the identical code, proving no cross-tenant existence oracle |
| **Notes** | Case (b) is the security-relevant one: if `SCOPE-003` were ever replaced by a distinguishable "belongs to another tenant" message, the endpoint would become a cross-tenant enumeration oracle. Assert the two titles are byte-identical. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-SEC-033 — RLS isolation on `qams.user_branch_access` and `qams.user_department_access`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-097, URS-100 · RSK-RBAC-006 |
| **Level / Type / Technique** | Database · Security (positive isolation) · Equivalence Partitioning over the RLS predicate — partitions *(GUC = owning tenant)*, *(GUC = other tenant)*, *(GUC unset)*, *(bypass on)* |
| **Priority / Severity / Automation** | Critical · Critical · Yes (integration, `RuntimeRolePrivilegeTests` sibling) |
| **Role / Permission / Tenant** | n/a — executed as the database role `qams_app`, not through the API · n/a — no HTTP permission is involved · both `demo-lab` (`@DEMO`) and `rival-lab` (`@RIVAL`) |
| **Environment** | `psql` at `C:\Program Files\PostgreSQL\17\bin` against dev DB `ntqams` as `qams_app` / `dev-only-local` |
| **Preconditions** | `@ANALYST_N` (tenant `@DEMO`) has one branch-scope row and one department-scope row; a user in `rival-lab` has one of each. Both tables carry `tenant_id` back-filled from `user_account` (`Hardening4_ChildTenancy.cs:317-320`) and `ENABLE`+`FORCE` RLS with policy `tenant_isolation` (`Hardening4_ChildTenancy.cs:681-698`). |
| **Test Data** | GUC values: `@DEMO`, `@RIVAL`, unset, and `app.bypass_rls='on'`. |
| **Steps** | 1. `SELECT relrowsecurity, relforcerowsecurity FROM pg_class WHERE relname IN ('user_branch_access','user_department_access');`. 2. `SELECT polname FROM pg_policy p JOIN pg_class c ON c.oid=p.polrelid WHERE c.relname IN ('user_branch_access','user_department_access');`. 3. `SELECT set_config('app.current_tenant','@DEMO',false); SELECT count(*) FROM qams.user_branch_access; SELECT count(*) FROM qams.user_department_access;`. 4. Repeat with `'@RIVAL'`. 5. `SELECT set_config('app.current_tenant','',false);` then re-count. 6. `SELECT set_config('app.bypass_rls','on',false);` then re-count. 7. Attempt `INSERT INTO qams.user_branch_access (user_id, branch_id, tenant_id) VALUES ('@ANALYST_N','@BR_NORTH','@RIVAL');` while the GUC is `@DEMO`. |
| **Expected UI** | n/a — this case is executed entirely in `psql`. |
| **Expected API** | n/a — no HTTP request is made. |
| **Expected DB** | Step 1: both rows return `t, t`. Step 2: `tenant_isolation` present on both tables. Step 3: counts include only `@DEMO` rows. Step 4: counts include only `@RIVAL` rows and **exclude** every `@DEMO` row. Step 5: both counts `0` — the policy fails closed when the GUC is empty (`NULLIF(...,'')::uuid` yields NULL and the comparison is never true). Step 6: counts equal the full table. Step 7: `ERROR: new row violates row-level security policy for table "user_branch_access"` — the `WITH CHECK` half refuses a cross-tenant insert. |
| **Expected Audit** | n/a — direct `psql` reads write no application audit rows; the failed INSERT in step 7 is a PostgreSQL error, not an application event. |
| **Expected Notification** | n/a — database-level test. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','off',false); SELECT set_config('app.current_tenant','',false);` at the end of the session so a later case does not inherit an elevated connection. |
| **Evidence** | `psql` transcript covering all seven steps, including the verbatim RLS violation text |
| **Notes** | The two accepted permanent RLS exceptions are `user_account` and `outbox_event` (deviation B9) — the **child** scope tables are not exceptions and must show `force=true`. `audit.security_event` was closed in v1.51.2 and is likewise a positive-isolation target, not a known gap (conventions §2, [corrected 2026-08-01]). |
| **Result / Defect** | Not Run · — |

**MC/DC conditions for `TC-RBAC-MCDC-001` … `TC-RBAC-MCDC-010`.** The decision under test is the composite authorization condition governing a Part-11 document publish, `POST /api/documents/{id}/publish`. It is not a single boolean expression in one file — it is a short-circuiting conjunction spread across four layers, which is precisely why MC/DC is the right technique: each condition must be shown to independently determine the outcome, and each observable is a *different* status code, so masking is detectable.

| Condition | Meaning | Where evaluated | Observable when false |
|---|---|---|---|
| **C1 tenant match** | the document's `tenant_id` equals the JWT `tenant_id` claim resolved by `TenantResolutionMiddleware` | EF global query filter `ApplyTenantFilter` (`AppDbContext.cs:187-192`) + PostgreSQL FORCE RLS | `404` `DOC-404` (`DocumentCommands.cs:81-83`) |
| **C2 permission held** | the actor's active role grants `documents.sign` | HTTP gate `RequirePermissionAttribute` (`DocumentsController.cs:114`) **and** command policy `[RequirePermissionPolicy(Documents, Sign)]` (`DocumentCommands.cs:65-67`) | `403` `AUTHZ-403` (HTTP layer) / `403` `AUTHZ-002` (command layer) |
| **C3 ownership** | the actor is **not** the author of the version being published | `PublishDocumentHandler` pre-check (`DocumentCommands.cs:143-147`) and again in the aggregate (`ControlledDocument.cs:156`) | `422` `SOD-DOC-002` |
| **C4 record state** | the in-flight version is in `VersionState.Approved` | `PublishDocumentHandler` (`DocumentCommands.cs:137-141`) and `RequireInFlight` in the aggregate | `409` `DOC-014` |
| **C5 signature valid** | the supplied account password **and** signature PIN both verify | `ESignatureService.SignAsync` (`ComplianceLedgerServices.cs:104-115`) | `422` `SIG-002` (password) / `422` `SIG-001` (PIN) / `422` `SIG-003` (locked) |

Short-circuit order is **C1 → C2 → C4 → C3 → C5**. `TC-RBAC-MCDC-001` is the all-true baseline; `002`–`007` flip exactly one condition false with all preceding conditions true, giving independent-determination pairs `(001,002)` for C1, `(001,003)` for C2, `(001,004)` for C4, `(001,005)` for C3 and `(001,006)`/`(001,007)` for C5; `008`–`010` are masking cases that pin the evaluation order itself.

#### TC-RBAC-MCDC-001 — All five conditions true: the publish completes and mints exactly one signature  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-026, URS-020, URS-095 · RSK-RBAC-010 |
| **Level / Type / Technique** | API · Functional (positive) · MC/DC — the all-true baseline against which every single-condition flip is compared |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-b`) · `documents.sign` at the HTTP gate **and** the command policy · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Document `@DOC` in `demo-lab`, authored by `qm-a` (`document_version.author_id=@QM_A`), submitted for review, recommended by `qm-b`, so the in-flight version is `state='Approved'`. `qm-b` has set PIN `573104` via `POST /api/auth/signature-pin`. The uploaded file's `sha256` is recorded as `@HASH`. |
| **Test Data** | `POST /api/documents/@DOC/publish` body `{"password":"Qm-Bravo-Pass-4!","pin":"573104"}`. |
| **Steps** | 1. Sign in as `qm-b`. 2. Send the publish call. 3. Record status. 4. `SELECT state, approved_by, approved_at_utc FROM qams.document_version WHERE document_id='@DOC' ORDER BY approved_at_utc DESC NULLS LAST LIMIT 1;`. 5. `SELECT signer_id, meaning, subject_ref, content_hash FROM audit.electronic_signature WHERE subject_ref='DOC:'||replace('@DOC','-','');` under bypass. 6. `SELECT count(*) FROM audit.security_event WHERE event_type='ESIGN_FAILED' AND occurred_at_utc > @T0;`. |
| **Expected UI** | The document page shows status *Published*, the version badge advances, the signing dialog closes, and the previous published version (if any) is shown as *Obsolete*. |
| **Expected API** | `204 No Content` (`DocumentsController.cs:117-121`). |
| **Expected DB** | `qams.document_version`: newest version `state='Published'`, `approved_by=@QM_B`, `approved_at_utc` non-null; `qams.controlled_document.status='Published'`, `next_review_due` = publish date + `review_cycle_months`. |
| **Expected Audit** | Exactly **one** row in `audit.electronic_signature` with `signer_id=@QM_B`, `meaning` beginning `Approved and published`, `subject_ref='DOC:<@DOC without hyphens>'`, `content_hash=@HASH`; one `audit.audit_trail` row `event_type='DocumentPublished'`; **zero** `ESIGN_FAILED` rows in the window. |
| **Expected Notification** | Assert the notification outcome measured on this build rather than assumed: query `SELECT count(*) FROM qams.notification_dispatch WHERE created_at_utc > @T0;` and record the result in the run log. No `NotificationPolicies` rule for `DocumentPublished` was located in this pass — treat a non-zero count as a finding to investigate, not a failure. |
| **Cleanup** | Published documents and signature rows are append-only; record `@DOC` in the run log and do not delete. |
| **Evidence** | HTTP 204 capture · three SQL result sets · the single signature row with its content hash |
| **Notes** | Establish `@T0` immediately before step 2 so the security-event and notification windows are tight. The publish route carries `[EnableRateLimiting(ESignaturePolicy)]` — 10 permits per actor per minute (`RateLimiting.cs:87-92`, default from `RateLimit:ESignaturePermitPerMinute`) — so the MC/DC set must be spaced or split across actors to avoid a spurious `429`. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-MCDC-002 — C1 false (cross-tenant document id) with C2–C5 true → `404 DOC-404`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095, URS-100 · RSK-RBAC-010 |
| **Level / Type / Technique** | API · Security (negative) · MC/DC — C1 independently determines the outcome; pair with TC-RBAC-MCDC-001 |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-b`) of `demo-lab` · `documents.sign` held · target document belongs to `rival-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; both tenants provisioned |
| **Preconditions** | Document `@DOC_RIVAL` exists in `rival-lab`, authored by a `rival-lab` user, in-flight version `state='Approved'` — so C3 (ownership) and C4 (state) would both be true if the row were reachable. `qm-b` holds `documents.sign` in `demo-lab` and a valid PIN. |
| **Test Data** | `POST /api/documents/@DOC_RIVAL/publish` body `{"password":"Qm-Bravo-Pass-4!","pin":"573104"}` sent with `qm-b`'s `demo-lab` token. |
| **Steps** | 1. As a `rival-lab` administrator, create `@DOC_RIVAL` and drive it to *Approved*. 2. Sign in as `qm-b` of `demo-lab`. 3. Send the publish call against `@DOC_RIVAL`. 4. Record status and `code`. 5. Under bypass, `SELECT state FROM qams.document_version WHERE document_id='@DOC_RIVAL' ORDER BY 1;`. 6. `SELECT count(*) FROM audit.electronic_signature WHERE signer_id='@QM_B' AND occurred_at_utc > @T0;` — substitute the column name `signed_at_utc`. |
| **Expected UI** | Unreachable through the SPA — `@DOC_RIVAL` never appears in `qm-b`'s document list. Driven by direct API call. |
| **Expected API** | `404 Not Found`, `application/problem+json`, `code="DOC-404"`, title `Document not found.` — the EF global tenant filter removes the row before `SingleOrDefaultAsync` (`DocumentCommands.cs:81-83`), and PostgreSQL FORCE RLS would refuse it even if the filter were bypassed. **Not 403**: the caller must not be able to distinguish "another tenant's document" from "no such document". |
| **Expected DB** | `@DOC_RIVAL`'s in-flight version remains `state='Approved'`; `qams.controlled_document.status` for `@DOC_RIVAL` unchanged. |
| **Expected Audit** | **Zero** new rows in `audit.electronic_signature` — the handler throws before `ESignatureService.SignAsync` is reached, so no signature is minted for an unreachable record. Zero `ESIGN_FAILED` rows. |
| **Expected Notification** | n/a — no notification on a not-found publish. |
| **Cleanup** | Leave `@DOC_RIVAL` in place in `rival-lab`; no `demo-lab` state changed. |
| **Evidence** | HTTP `404` capture with the `code` extension · SQL state read under bypass · signature-table count |
| **Notes** | Independent determination: relative to TC-RBAC-MCDC-001 only C1 differs, and the outcome flips from `204` to `404`. This case also doubles as the tenant-isolation half of the composite — a `403` here would be a leak, because it would confirm the id exists somewhere. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-MCDC-003 — C2 false (no `documents.sign`) with C1, C3–C5 true → `403 AUTHZ-403`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095 · RSK-RBAC-010 |
| **Level / Type / Technique** | API · Security (negative) · MC/DC — C2 independently determines the outcome |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Department Head (`dh@demo-lab.local`) on the seeded *Department Head* role — 90 keys, holds `documents.approve` but **not** `documents.sign` (front matter §4.2) · `documents.sign` required · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Document `@DOC2` in `demo-lab`, authored by `qm-a`, in-flight version `state='Approved'` (recommended by `qm-b`). The Department Head user `dh@demo-lab.local` / `Dh-Pass-7!` exists with PIN `602517` set, so C3, C4 and C5 would all be true. Confirm `GET /api/auth/me/privileges` for `dh` does **not** list `documents.sign`. |
| **Test Data** | `POST /api/documents/@DOC2/publish` body `{"password":"Dh-Pass-7!","pin":"602517"}`. |
| **Steps** | 1. Sign in as `dh`. 2. Read `GET /api/auth/me/privileges` and record the absence of `documents.sign`. 3. Send the publish call. 4. Record status, `Content-Type` and `code`. 5. `SELECT state FROM qams.document_version WHERE document_id='@DOC2' ORDER BY 1;`. 6. `SELECT count(*) FROM audit.electronic_signature WHERE signer_id='@DH' AND signed_at_utc > @T0;` under bypass. |
| **Expected UI** | The *Publish* control is not rendered — the template guards it with `perms.can('documents.sign')` (`document-detail.component.ts:80,118`) — so the call is issued directly with `curl.exe`. |
| **Expected API** | `403 Forbidden`, `application/problem+json`, `code="AUTHZ-403"`, title `You do not have permission to perform this action.` — the HTTP gate refuses before MediatR runs, so the command-layer `AUTHZ-002` is never reached. |
| **Expected DB** | In-flight version still `state='Approved'`; `qams.controlled_document.status` unchanged. |
| **Expected Audit** | **Zero** rows in `audit.electronic_signature` for `@DH` in the window, and zero `ESIGN_FAILED` — the password and PIN in the body were correct, and the system must never have evaluated them. This is the substantive assertion: a gate that ran *after* the signing ceremony would burn a valid signature attempt. |
| **Expected Notification** | n/a — no notification on an authorization refusal. |
| **Cleanup** | Leave `@DOC2` at *Approved* for TC-RBAC-MCDC-004 and TC-RBAC-MCDC-008. |
| **Evidence** | `curl.exe` `403` capture · `GET /auth/me/privileges` body · two SQL counts proving no signature and no failure event |
| **Notes** | Independent determination: only C2 differs from the baseline and the outcome flips from `204` to `403`. Assert the code is `AUTHZ-403` (the HTTP-layer constant, `ProblemAuthorizationResultHandler.cs:16`) and **not** `AUTHZ-002` — if the code were `AUTHZ-002`, the request had reached the application layer, which would mean the HTTP gate had been removed. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-MCDC-004 — C4 false (version `UnderReview`) with C1–C3, C5 true → `409 DOC-014`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-025, URS-026 · RSK-RBAC-008 |
| **Level / Type / Technique** | API · Security (negative) · MC/DC — C4 independently determines the outcome |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-b`) · `documents.sign` held · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Document `@DOC3` authored by `qm-a`, submitted for review, **not** recommended — the in-flight version is `state='UnderReview'`. `qm-b` holds `documents.sign` and a valid PIN, and is not the author, so C1, C2, C3 and C5 are all true. |
| **Test Data** | `POST /api/documents/@DOC3/publish` body `{"password":"Qm-Bravo-Pass-4!","pin":"573104"}`. |
| **Steps** | 1. As `qm-a`, create `@DOC3` and submit it for review; do not recommend. 2. Sign in as `qm-b`. 3. Send the publish call. 4. Record status and `code`. 5. `SELECT state FROM qams.document_version WHERE document_id='@DOC3';`. 6. `SELECT count(*) FROM audit.electronic_signature WHERE signer_id='@QM_B' AND signed_at_utc > @T0;` and `SELECT count(*) FROM audit.security_event WHERE event_type='ESIGN_FAILED' AND occurred_at_utc > @T0;` under bypass. |
| **Expected UI** | The *Publish* control renders (`documents.sign` is granted) but the workflow stepper shows the version at *Under review*; the returned error is displayed inline. |
| **Expected API** | `409 Conflict`, `application/problem+json`, `code="DOC-014"`, title `Cannot publish a version in state UnderReview.` — the handler's pre-check at `DocumentCommands.cs:137-141` throws an `InvalidStateTransitionException`, which maps to 409 (`DomainExceptionHandler.cs:45-51`). |
| **Expected DB** | `qams.document_version.state='UnderReview'` unchanged; no `approved_by`, no `approved_at_utc`. |
| **Expected Audit** | **Zero** signature rows and **zero** `ESIGN_FAILED` rows in the window — the state pre-check deliberately precedes the signing ceremony so a doomed publish never touches the append-only signature ledger (`DocumentCommands.cs:133-136`, comment in source). |
| **Expected Notification** | n/a — no notification on a refused publish. |
| **Cleanup** | Leave `@DOC3` at *UnderReview*; delete under bypass at the end of the run if desired. |
| **Evidence** | HTTP `409` capture · SQL state read · two zero-counts from the append-only ledgers |
| **Notes** | Independent determination: only C4 differs from the baseline and the outcome flips from `204` to `409`. The zero-signature assertion is the Part-11 substance of the case (RSK-RBAC-008); the status code alone is the weaker half. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-MCDC-005 — C3 false (the author publishes) with C1, C2, C4, C5 true → `422 SOD-DOC-002`, no signature minted  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-025, URS-026 · RSK-RBAC-001, RSK-RBAC-008 |
| **Level / Type / Technique** | API · Security (negative) · MC/DC — C3 independently determines the outcome |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-a`), the **author** of the version · `documents.sign` held (seeded *Quality Manager* holds all 7 `documents.*` keys) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Document `@DOC4` authored by `qm-a` (`document_version.author_id=@QM_A`), recommended by `qm-b` so the in-flight version is `state='Approved'`. `qm-a` holds `documents.sign` and has PIN `481920` set, so C1, C2, C4 and C5 are all true and **only** C3 is false. |
| **Test Data** | `POST /api/documents/@DOC4/publish` body `{"password":"Qm-Alpha-Pass-3!","pin":"481920"}` — correct credentials, deliberately. |
| **Steps** | 1. Establish `@T0`. 2. Sign in as `qm-a`. 3. Send the publish call. 4. Record status and `code`. 5. `SELECT state, approved_by FROM qams.document_version WHERE document_id='@DOC4';`. 6. `SELECT count(*) FROM audit.electronic_signature WHERE signer_id='@QM_A' AND signed_at_utc > @T0;` under bypass. 7. `SELECT count(*) FROM audit.security_event WHERE event_type IN ('ESIGN_FAILED','ESIGN_LOCKED') AND occurred_at_utc > @T0;`. 8. `SELECT failed_attempts, locked_until_utc FROM qams.user_account WHERE email='qm-a@demo-lab.local';`. |
| **Expected UI** | The *Publish* button renders and opens the password+PIN dialog; on submit the dialog shows the SoD message and stays open; the version badge remains *Approved*. |
| **Expected API** | `422`, `application/problem+json`, `code="SOD-DOC-002"`, title `Segregation of duties: the author cannot approve their own document.` — from the handler pre-check at `DocumentCommands.cs:143-147`, **before** `signatures.SignAsync` at `:155-158`. |
| **Expected DB** | `state='Approved'`, `approved_by IS NULL`; `qams.controlled_document.status` unchanged. |
| **Expected Audit** | **Zero** new rows in `audit.electronic_signature`; **zero** `ESIGN_FAILED` and `ESIGN_LOCKED` rows. |
| **Expected Notification** | n/a — no notification on a refused publish. |
| **Cleanup** | Leave `@DOC4` at *Approved*; it is reused by TC-RBAC-MCDC-011-class future cases and by TC-RBAC-MCDC-008 in this batch is not required. |
| **Evidence** | HTTP `422` capture · SQL version read · two append-only-ledger zero-counts · `failed_attempts` read |
| **Notes** | Step 8 is the point of the case beyond the status code: because the SoD check precedes the ceremony, the author's account **must not** have its `failed_attempts` incremented (`ESignatureService.RecordFailureAsync`, `ComplianceLedgerServices.cs:135-142` is never reached), so a doomed publish cannot be used to lock a colleague's account. Assert `failed_attempts` is unchanged and `locked_until_utc IS NULL`. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-MCDC-006 — C5 false via an incorrect PIN, C1–C4 true → `422 SIG-001` and one `ESIGN_FAILED`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-020, URS-026 · RSK-RBAC-010 |
| **Level / Type / Technique** | API · Security (negative) · MC/DC — C5 independently determines the outcome (PIN component) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-b`) · `documents.sign` held · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Document `@DOC5` authored by `qm-a`, recommended by `qm-b`, in-flight version `state='Approved'`. `qm-b` has PIN `573104` set. `qams.user_account.failed_attempts` for `qm-b` is `0` and `locked_until_utc IS NULL`. All of C1–C4 hold. |
| **Test Data** | `POST /api/documents/@DOC5/publish` body `{"password":"Qm-Bravo-Pass-4!","pin":"000000"}` — correct password, wrong PIN. |
| **Steps** | 1. Establish `@T0`; confirm `failed_attempts = 0`. 2. Sign in as `qm-b`. 3. Send the publish call with the wrong PIN. 4. Record status and `code`. 5. `SELECT state, approved_by FROM qams.document_version WHERE document_id='@DOC5';`. 6. `SELECT event_type, subject_ref FROM audit.security_event WHERE occurred_at_utc > @T0 ORDER BY occurred_at_utc;` under bypass. 7. `SELECT failed_attempts FROM qams.user_account WHERE email='qm-b@demo-lab.local';`. 8. `SELECT count(*) FROM audit.electronic_signature WHERE signer_id='@QM_B' AND signed_at_utc > @T0;`. |
| **Expected UI** | The signing dialog stays open with the message "Electronic-signature PIN is not set or is incorrect."; the PIN field is cleared and the version badge stays *Approved*. |
| **Expected API** | `422`, `code="SIG-001"`, title `Electronic-signature PIN is not set or is incorrect.` (`ComplianceLedgerServices.cs:112-116`). |
| **Expected DB** | `state='Approved'`, `approved_by IS NULL` — `doc.Publish(...)` at `DocumentCommands.cs:160` is never reached. `failed_attempts` for `qm-b` = `1` (incremented by `UserAccount.RegisterFailedLogin`). |
| **Expected Audit** | Exactly one `audit.security_event` row, `event_type='ESIGN_FAILED'`, `subject_ref` beginning `bad-pin:DOC:` and containing `@DOC5` without hyphens. **Zero** rows in `audit.electronic_signature`. |
| **Expected Notification** | n/a — no notification policy on a failed signing. |
| **Cleanup** | `UPDATE qams.user_account SET failed_attempts=0, locked_until_utc=NULL WHERE email='qm-b@demo-lab.local';` under bypass. Leave `@DOC5` at *Approved*. |
| **Evidence** | HTTP `422` capture · security-event row with its `subject_ref` · `failed_attempts` before/after · signature-table zero-count |
| **Notes** | Independent determination: only C5 differs from the baseline and the outcome flips from `204` to `422 SIG-001`. Note that `SIG-001` covers *both* "PIN not set" and "PIN incorrect" — a caller cannot tell which, by design (`ComplianceLedgerServices.cs:112`). Because this case increments the lockout counter, run it and TC-RBAC-MCDC-007 **after** the positive cases and reset the counter between them; five failures would produce `SIG-003` and invalidate the next case. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-MCDC-007 — C5 false via an incorrect password, C1–C4 true → `422 SIG-002` and one `ESIGN_FAILED`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-020, URS-026 · RSK-RBAC-010 |
| **Level / Type / Technique** | API · Security (negative) · MC/DC — C5 independently determines the outcome (password component); with TC-RBAC-MCDC-006 this shows the two sub-components of C5 are separately determinative |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-b`) · `documents.sign` held · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Document `@DOC6` authored by `qm-a`, recommended by `qm-b`, in-flight version `state='Approved'`. `qm-b` has PIN `573104`. `failed_attempts = 0` (reset after TC-RBAC-MCDC-006). |
| **Test Data** | `POST /api/documents/@DOC6/publish` body `{"password":"Wrong-Pass-9!","pin":"573104"}` — wrong password, correct PIN. |
| **Steps** | 1. Establish `@T0`; confirm `failed_attempts = 0`. 2. Sign in as `qm-b`. 3. Send the publish call. 4. Record status and `code`. 5. `SELECT state FROM qams.document_version WHERE document_id='@DOC6';`. 6. `SELECT event_type, subject_ref FROM audit.security_event WHERE occurred_at_utc > @T0;` under bypass. 7. `SELECT failed_attempts FROM qams.user_account WHERE email='qm-b@demo-lab.local';`. |
| **Expected UI** | The signing dialog reports "Account password is incorrect."; the password field is cleared and the PIN retained or cleared per the dialog's own behaviour — record which. |
| **Expected API** | `422`, `code="SIG-002"`, title `Account password is incorrect.` (`ComplianceLedgerServices.cs:106-110`). The password is verified **before** the PIN, so a wrong password masks a wrong PIN — see TC-RBAC-MCDC-009 for the masking case. |
| **Expected DB** | `state='Approved'`; `failed_attempts` = `1`. |
| **Expected Audit** | Exactly one `audit.security_event` row, `event_type='ESIGN_FAILED'`, `subject_ref` beginning `bad-password:DOC:`. **Zero** `audit.electronic_signature` rows. |
| **Expected Notification** | n/a — no notification policy on a failed signing. |
| **Cleanup** | `UPDATE qams.user_account SET failed_attempts=0, locked_until_utc=NULL WHERE email='qm-b@demo-lab.local';` under bypass. |
| **Evidence** | HTTP `422` capture · security-event row with the `bad-password:` prefix · `failed_attempts` read |
| **Notes** | The `subject_ref` prefix (`bad-password:` vs `bad-pin:`) is the only place the two failure modes are distinguished in the ledger; assert it verbatim. Both flow through the same 10-per-minute e-signature rate-limit partition. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-MCDC-008 — Masking: C2 false ∧ C3 false → `403 AUTHZ-403`, proving the permission gate short-circuits the SoD rule  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095, URS-026 · RSK-RBAC-010 |
| **Level / Type / Technique** | API · Security (negative) · MC/DC masking analysis — two false conditions, one observable; pins the C2-before-C3 order |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | A bespoke role holding `documents.view` + `documents.create` + `documents.edit` but **not** `documents.sign`, assigned to `author-x@demo-lab.local` who is also the version author · `documents.sign` required · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | User `author-x@demo-lab.local` / `Ax-Pass-8!` exists with PIN `714205`. Document `@DOC7` authored by `author-x` (`author_id=@AUTHOR_X`), recommended by `qm-b`, in-flight version `state='Approved'`. `author-x`'s bespoke role excludes `documents.sign`. Both C2 and C3 are false; C1, C4, C5 are true. |
| **Test Data** | `POST /api/documents/@DOC7/publish` body `{"password":"Ax-Pass-8!","pin":"714205"}`. |
| **Steps** | 1. As `admin`, create the bespoke role with a recorded `reason` and assign it to `author-x`. 2. Confirm via `GET /api/auth/me/privileges` as `author-x` that `documents.sign` is absent. 3. Send the publish call with `curl.exe`. 4. Record status and `code`. 5. `SELECT state, approved_by FROM qams.document_version WHERE document_id='@DOC7';`. 6. Under bypass, count `audit.electronic_signature` and `ESIGN_FAILED` rows since `@T0`. |
| **Expected UI** | No *Publish* control renders (`perms.can('documents.sign')` false); the call is issued directly. |
| **Expected API** | `403 Forbidden`, `code="AUTHZ-403"` — **not** `422 SOD-DOC-002`. C2 is evaluated in the MVC authorization filter, entirely before the MediatR pipeline where C3 lives, so C3 is *masked*. |
| **Expected DB** | `state='Approved'`, `approved_by IS NULL`. |
| **Expected Audit** | Zero signature rows, zero `ESIGN_FAILED` rows, and `qams.user_account.failed_attempts` for `author-x` unchanged at `0`. Step 1 must have produced one `RolePermissionsChanged` audit entry carrying the reason. |
| **Expected Notification** | n/a — no notification on an authorization refusal. |
| **Cleanup** | Deactivate the bespoke role (`POST /api/roles/@ROLE/deactivate`) and reassign `author-x` to the seeded *Analyst* role; leave `@DOC7` at *Approved*. |
| **Evidence** | `curl.exe` `403` capture · privileges body · SQL version read · ledger zero-counts |
| **Notes** | Masking is the reason MC/DC needs this case and not just the six single-flip cases: if the HTTP gate were removed and only the command policy remained, the observable would become `403 AUTHZ-002`; if both were removed, it would become `422 SOD-DOC-002`. Three distinct observables for three distinct configurations — assert the exact code, not just the status class. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-MCDC-009 — Masking: C4 false ∧ C5 false → `409 DOC-014` with no signature attempt at all  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-026, URS-020 · RSK-RBAC-008 |
| **Level / Type / Technique** | API · Security (negative) · MC/DC masking analysis — pins that the state pre-check precedes the signing ceremony |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (`qm-b`) · `documents.sign` held · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Document `@DOC8` authored by `qm-a`, submitted for review, **not** recommended — in-flight version `state='UnderReview'` (C4 false). `qm-b` has a valid PIN. `failed_attempts` for `qm-b` is `0`. |
| **Test Data** | `POST /api/documents/@DOC8/publish` body `{"password":"Definitely-Wrong-1!","pin":"999999"}` — **both** signature components wrong (C5 false). |
| **Steps** | 1. Establish `@T0`; confirm `failed_attempts = 0`. 2. Send the publish call as `qm-b`. 3. Record status and `code`. 4. `SELECT count(*) FROM audit.security_event WHERE event_type='ESIGN_FAILED' AND occurred_at_utc > @T0;` under bypass. 5. `SELECT failed_attempts FROM qams.user_account WHERE email='qm-b@demo-lab.local';`. 6. `SELECT state FROM qams.document_version WHERE document_id='@DOC8';`. |
| **Expected API** | `409 Conflict`, `code="DOC-014"` — **not** `SIG-001` or `SIG-002`. |
| **Expected UI** | The signing dialog closes on the state error rather than reporting a credential problem — record the exact dialog behaviour observed. |
| **Expected DB** | `state='UnderReview'` unchanged; `failed_attempts` still `0`. |
| **Expected Audit** | `count(*) = 0` for `ESIGN_FAILED` — although both credentials were wrong, `ESignatureService.SignAsync` was never invoked, so nothing was verified and nothing was counted against the account. Zero `audit.electronic_signature` rows. |
| **Expected Notification** | n/a — no notification on this path. |
| **Cleanup** | Delete `@DOC8` under bypass at the end of the run, or leave it at *UnderReview*. |
| **Evidence** | HTTP `409` capture · `ESIGN_FAILED` zero-count · `failed_attempts` unchanged |
| **Notes** | This case protects a real abuse vector: if the ceremony ran before the state check, an attacker holding `documents.sign` could burn another user's five permitted signing attempts against documents that could never publish anyway, driving `SIG-003` lockouts. The current ordering (`DocumentCommands.cs:133-158`) makes that impossible, and this case pins it. |
| **Result / Defect** | Not Run · — |

#### TC-RBAC-MCDC-010 — C2 true cannot compensate for C1 false: a platform admin holding every key still gets `404`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | RBAC · URS-095, URS-008 · RSK-RBAC-010 |
| **Level / Type / Technique** | API · Security (negative) · MC/DC compensation analysis — the maximal C2 (`Has()` returns true for every key) against a false C1 |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Platform administrator (`platform-admin@localhost` / `Dev-Only-Platform-Pass-1!`) · `documents.sign` — satisfied unconditionally, `IUserPrivileges.Has` short-circuits `true` for platform admins (`PrivilegeResolution.cs:39`) · **no tenant** — a platform admin carries no `tenant_id` claim |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Document `@DOC` from TC-RBAC-MCDC-001's fixture (or any `demo-lab` document with an in-flight version at `state='Approved'`). The platform admin has never been a member of `demo-lab`. |
| **Test Data** | `POST /api/documents/@DOC9/publish` body `{"password":"Dev-Only-Platform-Pass-1!","pin":"123456"}` with the platform admin's token, where `@DOC9` is a `demo-lab` document at `state='Approved'` authored by `qm-a`. |
| **Steps** | 1. Sign in as `platform-admin@localhost`. 2. Confirm the SPA redirects to `/platform/tenants` (`tenantOnlyGuard`, `role.guard.ts:17-21`) and that `GET /api/auth/me/privileges` is **not** fetched for platform admins (`permissions.service.ts:50-52`). 3. With `curl.exe`, send the publish call against `@DOC9`. 4. Record status and `code`. 5. `SELECT state FROM qams.document_version WHERE document_id='@DOC9';` under bypass. 6. Confirm no signature row exists for the platform admin. |
| **Expected UI** | The platform admin never reaches a document screen — the tenant shell redirects them to the control plane. Driven by direct API call. |
| **Expected API** | `404 Not Found`, `code="DOC-404"`. The HTTP permission gate **passes** (C2 true by short-circuit), the command policy passes, and then the EF tenant filter finds nothing because `ICurrentTenant.TenantId` is null for a platform admin — `TenantResolutionMiddleware` only sets it from a `tenant_id` claim (`RequestIdentity.cs:56-61`). |
| **Expected DB** | `@DOC9`'s in-flight version still `state='Approved'`; `qams.controlled_document` unchanged. |
| **Expected Audit** | Zero `audit.electronic_signature` rows for the platform admin; zero `ESIGN_FAILED` rows. |
| **Expected Notification** | n/a — no notification on a not-found publish. |
| **Cleanup** | Leave `@DOC9` at *Approved*; no state changed. |
| **Evidence** | `curl.exe` `404` capture · SQL version read under bypass · ledger zero-counts |
| **Notes** | This is the compensation case MC/DC exists to force: the strongest possible C2 (a bypass-everything privilege) does not rescue a false C1, because tenancy is enforced at a lower layer than authorization. It also documents a genuine consequence of the design — a platform administrator **cannot** act inside a tenant's regulated records through the tenant API, and any future "impersonate tenant" feature would have to go through `ICurrentTenantSetter.Elevate()` and would need its own case set. |
| **Result / Defect** | Not Run · — |

## Batch coverage note

**Covered.** 43 cases in two blocks. `TC-RBAC-SEC-001` … `TC-RBAC-SEC-033`: all six SoD families named in the slice, each with a negative case, a positive counterpart where one exists, and a guard-ordering case where the aggregate stacks more than one refusal — `SOD-AQ-001` at three of its fourteen call sites (`ValidationStudy.SignOff`, `UncertaintyBudget.Approve`, `PtPlan.Approve`) plus the documented no-op when `CreatedByUserId` is null; `SOD-CAPA-002` including the `passed=false` independence case; `SOD-CAPA-001` including the `effective=false` bypass path; `SOD-QP-001` including the rollback case that proves a refused approval does not leave the laboratory with no active policy; `SOD-COMP-001` at both of its sites (assess and authorize) with the `COMP-011` ordering boundary; `SOD-SUP-001` with the `SUP-010` ordering case; and the **absent** guard on audit sign-off. Direct-API bypass of a permission-hidden SPA control is covered twice, deliberately — once where the server answers `403 AUTHZ-403` (gate) and once where it answers `422 SOD-COMP-001` (domain rule) — because a suite that asserts only "the call fails" cannot tell the two defence layers apart. Direct Angular route access to `/roles` is covered as an E2E case. Data-scoped access is covered across nine cases spanning both scope tables: read invisibility (`404`), null-attribution visibility, `SCOPE-001`, `SCOPE-002`, the branch-before-department precedence, the `Modified`-entry re-point, empty-list-means-unrestricted with the same-token immediacy property, `SCOPE-003`/`SCOPE-004` including the cross-tenant id that must be indistinguishable from a non-existent one, and `psql`-level FORCE-RLS isolation of `qams.user_branch_access` and `qams.user_department_access`. `TC-RBAC-MCDC-001` … `TC-RBAC-MCDC-010`: a full MC/DC set over the five-condition composite governing `POST /api/documents/{id}/publish` — an all-true baseline, one single-flip case per condition (two for C5, since password and PIN are separately determinative), and three masking/compensation cases pinning the `C1 → C2 → C4 → C3 → C5` evaluation order.

**In my slice but not covered, with the reason.** (1) The remaining **eleven** `SOD-AQ-001` call sites (`CarryoverStudy`, `DetectionLimitStudy`, `InstrumentComparabilityStudy`, `InterferenceStudy`, `LinearityStudy`, `LotComparisonStudy`, `MethodComparisonStudy`, `OutlierScreening`, `PrecisionStudy`, `ReferenceIntervalStudy`, `SigmaAssessment`) are not individually cased. All eleven route through the identical `AggregateRoot.EnsureSignerIsNotPreparer(actorId, "SOD-AQ-001")` call with an identical `[RequirePermission(analytical-quality, Sign)]` gate; three representative sites are cased and the remaining eleven should be covered by a parameterised domain unit test asserting that every type implementing the study sign-off contract calls the helper — that is an architecture-test shape, not eleven near-identical API cases, and duplicating them here would inflate the count without adding detection power. (2) `SOD-DOC-001` (author cannot **review** their own document, `ControlledDocument.cs:122`), `SOD-AUTHZ-001` (`TestAuthorization.cs:43`) and `SOD-COI-001` (`ConflictDeclaration.cs:72`) are read and confirmed in source but not cased here — the assignment's slice names six SoD areas and these three are not among them; they belong to the `TC-RBAC-SEC-034`+ range the front matter reserves. (3) `SOD-DOC-002` appears only as MC/DC condition C3, not as a standalone SoD case, for the same scoping reason. (4) The `AUTHZ-*` → 403 mapping table is exercised incidentally (`AUTHZ-403` in three cases) but is not systematically cased — it is batch B/C scope.

**New gaps found in this pass** (numbered `9xx` to avoid colliding with the front matter's `001`–`017` sequence):

**GAP-RBAC-901 — The ID reservation table and this batch's assignment disagree about what batch D owns.** The front matter's reservation table (`11-module-rbac.md`, §*ID reservation table*) assigns `11-module-rbac-cases-D.md` the ranges `TC-RBAC-INT-001…035`, `TC-RBAC-RLS-001…020` and `TC-RBAC-DF-001…015`, and assigns `TC-RBAC-SEC-001…045` to batch **E**. This batch was commissioned against `TC-RBAC-SEC-001…` and `TC-RBAC-MCDC-001…`, and `MCDC` is **not reserved to any batch anywhere in the table**. This file therefore consumes `TC-RBAC-SEC-001`–`033` and `TC-RBAC-MCDC-001`–`010`. *Impact:* if batch E is authored against the front matter's table it will re-issue `TC-RBAC-SEC-001`+ and corrupt the traceability matrix; and the `INT`/`RLS`/`DF` ranges reserved to batch D are now unclaimed, which conventions §7 defines as a coverage hole, not a delivered case. *Recommended action, before any further batch is authored:* amend the front matter's reservation table so batch D reads `TC-RBAC-SEC-001…045` + `TC-RBAC-MCDC-001…020`, re-point the `INT`/`RLS`/`DF` ranges at a named batch (or record them as deferred), and confirm batch E starts at `TC-RBAC-SEC-046`. *Severity:* High (traceability integrity). *Responsible role:* Test package owner.

**GAP-RBAC-902 — The nonconformance raiser can indefinitely reopen their own record with no SoD check and no domain event.** `Nonconformance.ConfirmEffectiveness(effective: false, actorId)` returns at `Nonconformance.cs:260-264` **before** the `SOD-CAPA-001` comparison at `:266-269`, so the person who raised the NC can move it from `EffectivenessCheck` back to `ActionPlan` on their own record, repeatedly. No domain event is raised on that branch, so the only trace is the generic `audit.field_change` row written by `FieldChangeInterceptor`. This is not a closure breach — closing still requires an independent actor (TC-RBAC-SEC-011) — but URS-031 speaks only to the *verifier*, and nothing in the requirement set addresses who may reject an effectiveness check. *Suggested acceptance criteria:* either (a) evaluate the SoD comparison before the `!effective` early return so `SOD-CAPA-001` also guards the reopen, or (b) raise a dedicated domain event (proposal `NcEffectivenessRejected(NcId, NcRef, ActorId, Reason)`) with a mandatory reason, so a reviewer can see how many times a raiser reopened their own record; and add a URS clause covering the reject path. *Severity:* Medium. *Responsible role:* Domain owner + Quality Manager. Cased as `TC-RBAC-SEC-012` under `[IV]`, since the behaviour was read in source; the *requirement* is what is missing.

**GAP-RBAC-903 — Six of the ten implemented SoD rules have no URS requirement.** The FRA's "Separation of duties" area (`docs/validation/02-Functional-Risk-Assessment.md`, §2) cites exactly `URS-031`, `URS-039` and `URS-049`, which cover `SOD-CAPA-002`, `SOD-AQ-001` and `SOD-QP-001`. The other seven codes — `SOD-CAPA-001`, `SOD-DOC-001`, `SOD-DOC-002`, `SOD-COMP-001`, `SOD-AUTHZ-001`, `SOD-COI-001`, `SOD-SUP-001` — are implemented, tested here, and traceable only to source. `URS-050` and `URS-051` mention supplier evaluation and competency management but say nothing about segregation of duties, so the traces used in `TC-RBAC-SEC-016`, `SEC-018`, `SEC-019` and `SEC-020` are the closest available, not exact. *Suggested acceptance criteria:* add URS clauses in the `URS-108`+ range, one per uncovered duty pair, each naming the actor comparison and the expected code and HTTP status, and update RTM-NTQMS-001 so every `SOD-*` code has a requirement row. *Severity:* High (a validation reviewer cannot trace seven implemented controls to a requirement). *Responsible role:* Product Owner + Quality Manager (document control).

**GAP-RBAC-904 — The FRA carries no per-risk identifiers, so every RBAC case mints its own.** `docs/validation/02-Functional-Risk-Assessment.md` is area-level: rows are keyed by area name and URS list, with no `R-nnn`/`RSK-nnn` column. Conventions §5 permits minting `RSK-<MODULE>-<NNN>` and saying so, which this batch does (`RSK-RBAC-001`…`010`, defined in the scope statement). The consequence is that minted risk ids differ from batch to batch unless coordinated, and the traceability matrix will show ten RBAC risks that exist in no controlled document. *Suggested acceptance criteria:* add an id column to the FRA — one identifier per assessed area, at minimum `RSK-AREA-SOD`, `RSK-AREA-RBAC`, `RSK-AREA-TENANT` — and re-point every minted `RSK-RBAC-*` at a real FRA row, or formally adopt the minted set as an FRA addendum. *Severity:* Medium (documentation traceability, not a product defect). *Responsible role:* Validation lead.

**Honesty statement.** Every `[IV]` case above cites a file and line that was opened and read in this pass. The two `[GD]` cases (`TC-RBAC-SEC-003`, `TC-RBAC-SEC-021`) describe behaviour the build genuinely exhibits and must not be recorded as passing; their acceptance criteria are written to be implementable. No endpoint, column, permission key, error code, enum value or state named in this file was inferred — where the front matter and the source disagreed, the source was re-read. Nothing in this package was executed; every `Result` reads `Not Run · —`.
