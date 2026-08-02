# DOC — Detailed Test Cases, Batch A

This batch consumes `TC-DOC-STATE-001` … `TC-DOC-STATE-040` and covers **one slice only: the controlled-document lifecycle state machine** of `ControlledDocument` (`src/NT.QAMS.Domain/DocumentControl/ControlledDocument.cs`) as it is reachable over `/api/documents` — every **valid** transition of `SubmitForReview`, `Recommend`, `RejectVersion`, `Publish`, `DraftNewVersion`, `Retire`, `ConfirmPeriodicReview` and `MarkReviewDueIfReached` as its own case; every **invalid** transition as its own case carrying the real guard code (`DOC-010`…`DOC-020`) and the real HTTP status derived from `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:26-82`; the terminal-state guards (`DOC-015`, `DOC-018`), re-entry attempts (double-recommend, second revision, second retire, second sweep), guard-ordering probes, and transition attempts made by an actor who **does** hold the gating permission key but whose request the *state* forbids. States are named per the composite key **S1…S9** of the front matter (`13-module-document-control.md` §3.1) and the outcomes are those of matrices §3.2 and §3.3. **Deliberately left to sibling batches:** version-number arithmetic and the `Major`/`Minor` bump (`TC-DOC-UNIT-*`), segregation-of-duties refusals `SOD-DOC-001`/`SOD-DOC-002` and the decision tables DT-1/DT-2 (`TC-DOC-UNIT-*` / `TC-DOC-DT-*`), the HTTP surface itself — routing, permission denial `AUTHZ-403`, the `SIG-001/002/003/404` signing ceremony, rate limiting (batch B), the review-cycle sweep→policy→WorkTask→notification integration and `ReviewCycleMonths` boundaries (batch C), acknowledgements and the controlled-copy machine (batches C and D), RLS and files (batch D), and SPA journeys (batch E). Where the aggregate has **no guard at all** — the retire-then-resurrect paths of `GAP-DOC-004` — the case is written `[GD]` with the refusal that must be implemented, never as a passing expectation.

**Fixture `F-DOC-STATE`** — referenced by every Preconditions row below. Tenant `demo-lab`. Four accounts in `qams.user_account`, each bound to a tenant role by `user_account.role_id`: `nadia.analyst@demo-lab.local` (Analyst grants — `documents.view, documents.create, documents.edit, documents.export`, `src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs:161`); `omar.head@demo-lab.local` (Department Head — the Analyst set plus `documents.approve`, `:131`); `layla.qm@demo-lab.local` (Quality Manager — all seven `documents.*` keys, `:102-118`), password `Demo-QM-Pass-3!`, `pin_hash` provisioned from PIN `481902`; `admin@demo-lab.local` / `Demo-Admin-Pass-2!` (Tenant Administrator — all seven, `:97-100`). Every document is authored by **Nadia** so that publish/recommend by Omar or Layla never trips `SOD-DOC-001`/`SOD-DOC-002`. Every file id comes from `POST /api/files` with a 12 KiB `.pdf`, returning `FileUploadedDto.id`. All calls use `curl.exe` with the bearer access token in `Authorization:` (PowerShell 5.1 drops manual `Cookie` headers — conventions §3).

**Risk IDs** are minted for this batch because `docs/validation/02-Functional-Risk-Assessment.md` carries only the area-level row `Document control lifecycle | URS-025,026,027` and mints no per-requirement identifiers (conventions §5): `RSK-DOC-001` an unapproved or superseded version becomes the effective controlled version · `RSK-DOC-002` a published document escapes its periodic-review cadence · `RSK-DOC-003` a retired document silently returns to force · `RSK-DOC-004` a terminal-state guard is bypassed and version history is corrupted · `RSK-DOC-005` an inconsistent error contract misleads the operator or the SPA.

---

## Valid transitions

#### TC-DOC-STATE-001 — S1 → S2: an initial draft is submitted for review  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-001 (minted — FRA has no per-requirement id) |
| **Level / Type / Technique** | API (integration) · Functional (positive) · State Transition — 0-switch, S1→S2 on `SubmitForReview` (`ControlledDocument.cs:110-115`) |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (Nadia) · `n/a — POST /api/documents/{id}/submit carries no [RequirePermission] attribute (DocumentsController.cs:91-96); the only gate is [RequireInternalActor] on SubmitDocumentForReviewCommand (DocumentCommands.cs:58)` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | Document `SOP-STATE-001` created by Nadia via `POST /api/documents`; state **S1** — `controlled_document.status = 'Draft'`, exactly one row in `document_version` with `major=1, minor=0, state='Draft', author_id = Nadia` (seeded by `ControlledDocument.Create`, `:106`). |
| **Test Data** | `documentId` = the guid returned by create; request body: none. |
| **Steps** | 1. `POST /api/documents/{documentId}/submit` as Nadia, no body. 2. Read the status line. 3. `GET /api/documents/{documentId}` and read `versions[0].state`. 4. `SELECT state FROM qams.document_version WHERE document_id = '{documentId}'`. 5. `SELECT event_type FROM qams.outbox_event WHERE tenant_id = '{demoLabTenantId}' ORDER BY occurred_at_utc DESC LIMIT 1`. |
| **Expected UI** | Document detail shows version `1.0` with state `UnderReview`; the stepper still reads `Draft` because `flowSteps = ['Draft','Published','Obsolete']` tracks `DocumentStatus`, not `VersionState` (`document-detail.component.ts:246`). |
| **Expected API** | `204 No Content`, empty body. Subsequent `GET /api/documents/{documentId}` → `200` with `status = "Draft"` and `versions[0].state = "UnderReview"`. |
| **Expected DB** | `qams.document_version.state = 'UnderReview'` for `(major,minor) = (1,0)`; `qams.controlled_document.status` **unchanged** at `'Draft'`; `recommended_by`, `approved_by`, `rejection_reason` all still `NULL`. |
| **Expected Audit** | One `qams.outbox_event` row with `event_type = 'NT.QAMS.Domain.DocumentControl.DocumentSubmittedForReview, NT.QAMS.Domain'` written in the same `SaveChanges` (`OutboxInterceptor.cs:58-73`); after `OutboxProcessor` drains it, one `audit.audit_trail` row with that `event_type`, `sequence` = previous + 1 and `prev_hash` = the previous `entry_hash` (`ComplianceLedgerServices.cs:47-62`). Plus `audit.field_change` rows for the `State` property. |
| **Expected Notification** | n/a — `DocumentSubmittedForReview` has no handler (front matter §1.5) and no `notification_rule` key exists for it. |
| **Cleanup** | `DELETE` is not exposed on this module; leave the fixture document in place and use a fresh `code` per run, or `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.controlled_document WHERE code='SOP-STATE-001';` in the teardown transaction. |
| **Evidence** | HTTP response capture · `GET` detail JSON · SQL result set · outbox + audit_trail rows |
| **Result / Defect** | Not Run · — |
| **Notes** | `SubmitForReview` calls `RequireInFlight(VersionState.Draft, "DOC-010", "submit for review")` (`:112`) — the expected state is `Draft`, and the in-flight lookup is `SingleOrDefault`, so a second in-flight row would throw at read time rather than return the wrong version. |

#### TC-DOC-STATE-002 — S5 → S6: a revision draft is submitted for review while v1.0 stays published  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-027 · RSK-DOC-001 |
| **Level / Type / Technique** | API (integration) · Functional (positive) · State Transition — 1-switch, S4→S5→S6; asserts the published version is untouched by the transition |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (Nadia) · `n/a — /submit is ungated (DocumentsController.cs:91)` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-002` in state **S5**: `controlled_document.status = 'Published'`; `document_version` holds `(1,0) state='Published'` and `(1,1) state='Draft', author_id = Nadia` created by `POST /{id}/versions` with `bump = "Minor"`. |
| **Test Data** | body: none. |
| **Steps** | 1. `POST /api/documents/{documentId}/submit` as Nadia. 2. Read the status line. 3. `SELECT major, minor, state FROM qams.document_version WHERE document_id='{documentId}' ORDER BY major, minor`. 4. `SELECT status, next_review_due, review_due_raised FROM qams.controlled_document WHERE id='{documentId}'`. |
| **Expected UI** | The version table lists `1.1` as `UnderReview` above `1.0` as `Published` (ordering `OrderByDescending(Major).ThenByDescending(Minor)`, `DocumentQueries.cs:62`); the download link on `1.0` remains live. |
| **Expected API** | `204 No Content`. `GET /api/documents/{documentId}` → `200`, `status = "Published"`, `versions[0].version = "1.1"` `state = "UnderReview"`, `versions[1].version = "1.0"` `state = "Published"`. |
| **Expected DB** | `(1,1).state = 'UnderReview'`; `(1,0).state` still `'Published'`; `controlled_document.status` still `'Published'`; `next_review_due` and `review_due_raised` unchanged — `SubmitForReview` touches neither (`:110-115`). |
| **Expected Audit** | One `DocumentSubmittedForReview` outbox row carrying `Version = "1.1"` in the JSON payload; one chained `audit.audit_trail` entry. |
| **Expected Notification** | n/a — no handler and no seeded rule for `DocumentSubmittedForReview`. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-002` with `app.bypass_rls = 'on'`. |
| **Evidence** | HTTP capture · version table SQL · document row SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | `InFlightVersion` excludes `Published` and `Obsolete` (`:81-82`), which is why `RequireInFlight` selects `1.1` and not `1.0`. |

#### TC-DOC-STATE-003 — S2 → S3: a department head recommends the version under review  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-001 |
| **Level / Type / Technique** | API (integration) · Functional (positive) · State Transition — 0-switch, S2→S3 on `Recommend` (`ControlledDocument.cs:117-129`) |
| **Priority / Severity / Automation** | Critical · Major · Yes (functional) |
| **Role / Permission / Tenant** | Department Head (Omar) · `documents.approve` — `[RequirePermission(PermissionCatalog.Documents, PermissionAction.Approve)]` (`DocumentsController.cs:99`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-003` in state **S2**: `status='Draft'`, `(1,0) state='UnderReview'`, `author_id = Nadia`. Omar's role holds `documents.approve` (`SystemRoleCatalog.cs:131`) and Omar ≠ Nadia, so `SOD-DOC-001` cannot fire. |
| **Test Data** | Actor `omar.head@demo-lab.local`; body: none; server clock `IClock.UtcNow` captured before and after the call. |
| **Steps** | 1. Record `t0 = now()` from PostgreSQL. 2. `POST /api/documents/{documentId}/recommend` as Omar. 3. Read the status line. 4. `SELECT state, recommended_by, recommended_at_utc, approved_by FROM qams.document_version WHERE document_id='{documentId}'`. 5. `SELECT event_type, payload FROM qams.outbox_event WHERE tenant_id='{demoLabTenantId}' ORDER BY occurred_at_utc DESC LIMIT 1`. |
| **Expected UI** | Version `1.0` shows state `Approved` and names Omar as recommender; the Publish control becomes visible for holders of `documents.sign` (`document-detail.component.ts:80`). |
| **Expected API** | `204 No Content`. `GET /api/documents/{documentId}` → `versions[0].state = "Approved"`, `recommendedBy` = Omar's user id, `recommendedAtUtc` non-null and between `t0` and `now()`. |
| **Expected DB** | `document_version.state = 'Approved'`; `recommended_by` = Omar's `user_account.id`; `recommended_at_utc` non-null; `approved_by` and `approved_at_utc` still `NULL`; `controlled_document.status` still `'Draft'`. |
| **Expected Audit** | Outbox row `event_type = 'NT.QAMS.Domain.DocumentControl.DocumentRecommended, NT.QAMS.Domain'`, payload containing `"Version":"1.0"` and `"RecommendedBy":"{omarId}"`; one chained `audit.audit_trail` entry. `audit.field_change` rows for `State`, `RecommendedBy`, `RecommendedAtUtc`. |
| **Expected Notification** | n/a — `DocumentRecommended` has no handler (front matter §1.5). |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-003`. |
| **Evidence** | HTTP capture · version row SQL · outbox payload |
| **Result / Defect** | Not Run · — |
| **Notes** | The SoD branch (`actorId == version.AuthorId` → `SOD-DOC-002`… here `SOD-DOC-001`, `:120-123`) is deliberately **not** exercised here; it belongs to the `TC-DOC-UNIT-*` / DT-1 slice. This case only proves the state edge. |

#### TC-DOC-STATE-004 — S6 → S7: a revision under review is recommended  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-027 · RSK-DOC-001 |
| **Level / Type / Technique** | API (integration) · Functional (positive) · State Transition — 2-switch, S4→S5→S6→S7 |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Department Head (Omar) · `documents.approve` (`DocumentsController.cs:99`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-004` in state **S6**: `status='Published'`, `(1,0) state='Published'`, `(1,1) state='UnderReview'`, `author_id` of `1.1` = Nadia. |
| **Test Data** | Actor Omar; body: none. |
| **Steps** | 1. `POST /api/documents/{documentId}/recommend` as Omar. 2. Read the status line. 3. `SELECT major, minor, state, recommended_by FROM qams.document_version WHERE document_id='{documentId}' ORDER BY major, minor`. |
| **Expected UI** | `1.1` shows `Approved`; `1.0` still shows `Published`; both remain downloadable. |
| **Expected API** | `204 No Content`. Detail JSON: `versions[0] = {version:"1.1", state:"Approved", recommendedBy:"{omarId}"}`, `versions[1] = {version:"1.0", state:"Published"}`. |
| **Expected DB** | `(1,1).state='Approved'`, `(1,1).recommended_by = omarId`; `(1,0).state='Published'` and `(1,0).recommended_by` unchanged; `controlled_document.status='Published'`. |
| **Expected Audit** | One `DocumentRecommended` outbox + `audit.audit_trail` entry with `"Version":"1.1"`. |
| **Expected Notification** | n/a — no handler for `DocumentRecommended`. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-004`. |
| **Evidence** | HTTP capture · version table SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | Proves `Recommend` operates on `InFlightVersion` and never on the currently published row — the `SingleOrDefault` in `:81-82` is the mechanism. |

#### TC-DOC-STATE-005 — S2 → S1: rejecting a version under review returns it to Draft, not to `Rejected`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-001, RSK-DOC-005 |
| **Level / Type / Technique** | API (integration) · Functional (positive) · State Transition — backward edge S2→S1; also Statement coverage of `ControlledDocument.cs:146-148` |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Department Head (Omar) · `documents.approve` — `[RequirePermission(Documents, Approve)]` (`DocumentsController.cs:107`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-005` in state **S2**: `status='Draft'`, `(1,0) state='UnderReview'`, `rejection_reason IS NULL`. |
| **Test Data** | Body `{"reason":"Section 4 contradicts the equipment manual"}` — 44 characters, inside the validator's `MaximumLength(1000)` (`DocumentCommands.cs:76`). |
| **Steps** | 1. `POST /api/documents/{documentId}/reject` as Omar with the body above. 2. Read the status line. 3. `SELECT state, rejection_reason, recommended_by FROM qams.document_version WHERE document_id='{documentId}'`. 4. `GET /api/documents/{documentId}` and read `versions[0].state` and `versions[0].rejectionReason`. |
| **Expected UI** | Version `1.0` returns to `Draft` and the rejection reason is rendered beside the change summary (`document-detail.component.ts:54`); the Submit control is available to Nadia again. |
| **Expected API** | `204 No Content`. Detail JSON: `versions[0].state = "Draft"` — **never** `"Rejected"` — and `versions[0].rejectionReason = "Section 4 contradicts the equipment manual"`. |
| **Expected DB** | `document_version.state = 'Draft'`; `rejection_reason` = the trimmed reason (`:147`); `controlled_document.status` still `'Draft'`. Assert `state <> 'Rejected'` even though `ck_document_version_state_domain` admits that literal. |
| **Expected Audit** | Outbox row `event_type = 'NT.QAMS.Domain.DocumentControl.DocumentVersionRejected, NT.QAMS.Domain'` with `"RejectedBy":"{omarId}"` and `"Reason":"Section 4 contradicts the equipment manual"`; one chained `audit.audit_trail` entry; `audit.field_change` rows for `State` and `RejectionReason`. |
| **Expected Notification** | n/a — `DocumentVersionRejected` has no handler. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-005`. |
| **Evidence** | HTTP capture · version row SQL · outbox payload |
| **Result / Defect** | Not Run · — |
| **Notes** | Direct evidence for `GAP-DOC-002`: `VersionState.Rejected` is declared (`ControlledDocument.cs:8`) and admitted by the CHECK constraint but is never assigned — `RejectVersion` writes `VersionState.Draft` at `:146`. Assert the absence, do not assume the enum. |

#### TC-DOC-STATE-006 — S3 → S1: rejecting an already-recommended version leaves the recommender stamp behind  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-027 · RSK-DOC-001, RSK-DOC-005 |
| **Level / Type / Technique** | API (integration) · Functional (positive, defect-revealing) · State Transition — backward edge S3→S1; Data Flow on `recommended_by` (define at `:126`, no kill on the reject path) |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) · `documents.approve` (`DocumentsController.cs:107`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-006` in state **S3**: `status='Draft'`, `(1,0) state='Approved'`, `recommended_by = omarId`, `recommended_at_utc` non-null, `approved_by IS NULL`. |
| **Test Data** | Body `{"reason":"Approved in error - the calibration interval is wrong"}`. |
| **Steps** | 1. `SELECT recommended_by, recommended_at_utc FROM qams.document_version WHERE document_id='{documentId}'` and record both. 2. `POST /api/documents/{documentId}/reject` as Layla with the body above. 3. Read the status line. 4. Re-run the SELECT of step 1 and add `state, rejection_reason`. |
| **Expected UI** | Version `1.0` shows `Draft` with the rejection reason; the detail screen still renders the recommender from step 1 because `DocumentVersionDto.RecommendedBy` is unchanged (`DocumentQueries.cs:65`). |
| **Expected API** | `204 No Content`. Detail JSON: `versions[0].state = "Draft"`, `rejectionReason` set, and **`recommendedBy` still equal to Omar's id with `recommendedAtUtc` unchanged**. |
| **Expected DB** | `state='Draft'`; `rejection_reason` = the trimmed reason; `recommended_by` and `recommended_at_utc` **byte-identical to step 1** — `RejectVersion` nulls neither (`ControlledDocument.cs:146-147`); `approved_by IS NULL`. |
| **Expected Audit** | One `DocumentVersionRejected` outbox + `audit.audit_trail` entry; `audit.field_change` rows for `State` and `RejectionReason` **only** — no row for `RecommendedBy`, which proves the field was not written. |
| **Expected Notification** | n/a — no handler for `DocumentVersionRejected`. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-006`. |
| **Evidence** | HTTP capture · before/after SQL of `recommended_by` · field_change row list |
| **Result / Defect** | Not Run · — |
| **Notes** | New finding, registered below as **`GAP-DOC-902`**: a version rejected out of `Approved` retains a recommender stamp while sitting in `Draft`, so `GET /api/documents/{id}` advertises a review that has been revoked. Assert the observed behaviour; do not record it as correct. `:136` admits `Approved` as a rejectable state, so this path is fully supported. |

#### TC-DOC-STATE-007 — S7 → S5: rejecting an approved revision returns it to Draft and leaves v1.0 published  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-027 · RSK-DOC-001 |
| **Level / Type / Technique** | API (integration) · Functional (positive) · State Transition — 3-switch, S4→S5→S6→S7→S5; asserts the published version is never collateral |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) · `documents.approve` (`DocumentsController.cs:107`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-007` in state **S7**: `status='Published'`, `(1,0) state='Published'`, `(1,1) state='Approved'`. |
| **Test Data** | Body `{"reason":"Rework required before release"}`. |
| **Steps** | 1. `POST /api/documents/{documentId}/reject` as Layla. 2. Read the status line. 3. `SELECT major, minor, state, rejection_reason FROM qams.document_version WHERE document_id='{documentId}' ORDER BY major, minor`. 4. `SELECT status FROM qams.controlled_document WHERE id='{documentId}'`. |
| **Expected UI** | `1.1` shows `Draft` with the rejection reason; `1.0` still shows `Published`; the effective-version banner still names `1.0`. |
| **Expected API** | `204 No Content`. Detail JSON: `versions[0] = {version:"1.1", state:"Draft", rejectionReason:"Rework required before release"}`, `versions[1] = {version:"1.0", state:"Published"}`, document `status = "Published"`. |
| **Expected DB** | `(1,1).state='Draft'`, `(1,1).rejection_reason='Rework required before release'`; `(1,0).state='Published'` unchanged; `controlled_document.status='Published'`, `next_review_due` and `review_due_raised` unchanged. |
| **Expected Audit** | One `DocumentVersionRejected` outbox + chained `audit.audit_trail` entry with `"Version":"1.1"`. |
| **Expected Notification** | n/a — no handler for `DocumentVersionRejected`. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-007`. |
| **Evidence** | HTTP capture · version table SQL · document row SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | The invariant under test is URS-027's "only the current published version is presented as effective" — rejecting a revision must not disturb it. |

#### TC-DOC-STATE-008 — S3 → S4: first publish arms the review cycle and creates no obsolescence event  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-026 · RSK-DOC-001, RSK-DOC-002 |
| **Level / Type / Technique** | API (integration) · Functional (positive) · State Transition — S3→S4; Branch coverage of the `previous is not null` false branch (`ControlledDocument.cs:159-164`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) · `documents.sign` — `[RequirePermission(Documents, Sign)]` at the endpoint (`DocumentsController.cs:115`) **and** `[RequirePermissionPolicy(Documents, Sign)]` on the command (`DocumentCommands.cs:65-66`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-008` in state **S3**: `status='Draft'`, `(1,0) state='Approved'`, `author_id = Nadia`, `review_cycle_months = 12` (set at create). Layla's `pin_hash` is provisioned and `locked_until_utc IS NULL`. |
| **Test Data** | Body `{"password":"Demo-QM-Pass-3!","pin":"481902"}`; publish date recorded as `d0 = date(now() at time zone 'utc')`. |
| **Steps** | 1. `POST /api/documents/{documentId}/publish` as Layla with the body above. 2. Read the status line. 3. `SELECT state, approved_by, approved_at_utc FROM qams.document_version WHERE document_id='{documentId}'`. 4. `SELECT status, next_review_due, review_due_raised FROM qams.controlled_document WHERE id='{documentId}'`. 5. `GET /api/documents/{documentId}/signatures`. 6. `SELECT event_type FROM qams.outbox_event WHERE tenant_id='{demoLabTenantId}' AND occurred_at_utc >= '{t0}' ORDER BY occurred_at_utc`. |
| **Expected UI** | The stepper advances to `Published`; the signature panel lists Layla with meaning `Approved and published SOP-STATE-008 v1.0`; the Retire control appears for holders of `documents.void`. |
| **Expected API** | `204 No Content`. `GET /api/documents/{documentId}` → `status = "Published"`, `versions[0].state = "Published"`, `approvedBy` = Layla's id, `nextReviewDue` = `d0 + 12 months`. `GET /{id}/signatures` → `200` with exactly one record whose `subjectRef` = `DOC:{documentId:N}` and `contentHash` = the `sha256` of the version's `file_reference` row. |
| **Expected DB** | `document_version.state='Published'`, `approved_by` = Layla's id, `approved_at_utc` non-null; `controlled_document.status='Published'`, `next_review_due = d0 + interval '12 months'` (`ControlledDocument.cs:170`), `review_due_raised = false` (`:171`); exactly one row in `audit.electronic_signature` with `subject_ref = 'DOC:{id:N}'`, `meaning = 'Approved and published SOP-STATE-008 v1.0'`. |
| **Expected Audit** | Outbox contains a `DocumentPublished` row and **no `DocumentVersionObsoleted` row** — there is no predecessor, so `:160` is false. One chained `audit.audit_trail` entry for `DocumentPublished`. `audit.field_change` rows carry no plaintext password or PIN: `FieldChangeInterceptor.cs:33` redacts fragments `password, secret, pin, hash, token`. |
| **Expected Notification** | One `DOC_PUBLISHED` dispatch to recipients `QualityManager,TenantAdmin` (`NotificationPolicies.cs:141-142`, seed loop `:158-163`), subject `Document published: SOP-STATE-008 v1.0`. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-008`; the `audit.electronic_signature` row is append-only and is left in place. |
| **Evidence** | HTTP capture · signature manifest JSON · document + version SQL · outbox event list · notification dispatch row |
| **Result / Defect** | Not Run · — |
| **Notes** | The signing ceremony's own failure modes (`SIG-001/002/003/404`) and the 429 rate-limit partition belong to batch B; this case asserts only the state edge and its post-conditions. `IClock.UtcNow` drives `approved_at_utc` and the `AddMonths` base — never `DateTime.Now`. |

#### TC-DOC-STATE-009 — S7 → S4: publishing a revision obsoletes the predecessor in the same transaction  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-026, URS-027 · RSK-DOC-001 |
| **Level / Type / Technique** | API (integration) · Functional (positive) · State Transition — S7→S4 with a coupled version-level edge `Published → Obsolete`; Branch coverage of the `previous is not null` true branch (`:160-164`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) · `documents.sign` (`DocumentsController.cs:115`; command policy `DocumentCommands.cs:65-66`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-009` in state **S7**: `status='Published'`, `(1,0) state='Published'`, `(1,1) state='Approved'`, `author_id` of `1.1` = Nadia, `review_cycle_months = 24`. |
| **Test Data** | Body `{"password":"Demo-QM-Pass-3!","pin":"481902"}`; `d0 = date(now() at time zone 'utc')`. |
| **Steps** | 1. `POST /api/documents/{documentId}/publish` as Layla. 2. Read the status line. 3. `SELECT major, minor, state, approved_by FROM qams.document_version WHERE document_id='{documentId}' ORDER BY major, minor`. 4. `SELECT next_review_due, review_due_raised FROM qams.controlled_document WHERE id='{documentId}'`. 5. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type FROM audit.audit_trail WHERE tenant_id='{demoLabTenantId}' ORDER BY sequence DESC LIMIT 3;`. |
| **Expected UI** | `1.1` becomes `Published`, `1.0` becomes `Obsolete`; the list screen's `publishedVersion` column changes from `1.0` to `1.1`; **both** versions keep a live download link — there is no obsolescence marking on the link (`document-detail.component.ts:50-57`, `GAP-DOC-014`). |
| **Expected API** | `204 No Content`. Detail JSON: `versions[0] = {version:"1.1", state:"Published"}`, `versions[1] = {version:"1.0", state:"Obsolete"}`; `nextReviewDue = d0 + 24 months`. `GET /api/documents?status=Published` → the list row's `publishedVersion` is `"1.1"` (`DocumentQueries.cs:37-40`). |
| **Expected DB** | Exactly one row with `state='Published'` for this `document_id` (assert `SELECT count(*) … WHERE state='Published'` = 1); `(1,0).state='Obsolete'`; `(1,1).approved_by` = Layla; `controlled_document.next_review_due = d0 + interval '24 months'`, `review_due_raised = false`. |
| **Expected Audit** | Two outbox rows written in the **same** `SaveChanges`: `…DocumentVersionObsoleted, NT.QAMS.Domain` (payload `"Version":"1.0"`, `"FileId"` = v1.0's file id) raised first at `:163`, then `…DocumentPublished, NT.QAMS.Domain` (payload `"Version":"1.1"`) raised at `:172`. Both appear in `audit.audit_trail` with consecutive `sequence` values and a valid `prev_hash → entry_hash` link; `GET /api/compliance/chain-verification` still verifies. |
| **Expected Notification** | One `DOC_PUBLISHED` dispatch for `v1.1`; **no** notification for `DocumentVersionObsoleted` — it has no handler and no seeded rule. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-009`. |
| **Evidence** | HTTP capture · version table SQL with the `count(*)=1` assertion · audit_trail tail · chain-verification response |
| **Result / Defect** | Not Run · — |
| **Notes** | Atomicity is the point: `PublishedVersion` is `SingleOrDefault` (`:79`), so if the predecessor were not flipped in the same call the very next read of the aggregate would throw. Order of the two events in the outbox is deterministic and assertable. |

#### TC-DOC-STATE-010 — S4 → S5: drafting a new version leaves the published version effective and raises no domain event  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-027 · RSK-DOC-001 |
| **Level / Type / Technique** | API (integration) · Functional (positive) · State Transition — S4→S5; negative-space assertion on the event stream |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (Nadia) · `n/a — POST /api/documents/{id}/versions carries no [RequirePermission] attribute (DocumentsController.cs:125); the only gate is [RequireInternalActor] on DraftNewVersionCommand (DocumentCommands.cs:68)` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-010` in state **S4**: `status='Published'`, single version `(1,0) state='Published'`. A second file uploaded via `POST /api/files` yielding `fileId2`, which exists in `qams.file_reference` for this tenant (else `FILE-404`, `DocumentCommands.cs:171`). |
| **Test Data** | Body `{"fileId":"{fileId2}","changeSummary":"Added weekly balance check","bump":"Minor"}`. |
| **Steps** | 1. Record `seqBefore = MAX(sequence)` from `audit.audit_trail` for the tenant. 2. `POST /api/documents/{documentId}/versions` as Nadia with the body above. 3. Read the status line. 4. `SELECT major, minor, state, author_id, file_id, change_summary FROM qams.document_version WHERE document_id='{documentId}' ORDER BY major, minor`. 5. `SELECT count(*) FROM qams.outbox_event WHERE tenant_id='{demoLabTenantId}' AND occurred_at_utc >= '{t0}'`. |
| **Expected UI** | The version table gains a `1.1 / Draft` row above `1.0 / Published`; the stepper still reads `Published`; the Submit control is offered for `1.1`. |
| **Expected API** | `204 No Content`. Detail JSON: two versions, `1.1` `state="Draft"` `authorId` = Nadia `changeSummary="Added weekly balance check"`, `1.0` `state="Published"`; document `status = "Published"`. |
| **Expected DB** | New `qams.document_version` row `(major,minor)=(1,1)`, `state='Draft'`, `file_id={fileId2}`, `author_id` = Nadia, `tenant_id` = the demo-lab tenant (shadow property stamped from the owner, `DocumentControlConfigurations.cs:29-31`); `(1,0)` unchanged; `controlled_document.status='Published'` and `next_review_due` unchanged. |
| **Expected Audit** | **Zero** new `qams.outbox_event` rows and **zero** new `audit.audit_trail` entries — `DraftNewVersion` raises no domain event (`ControlledDocument.cs:207-227`). The only trace is `audit.field_change` rows with `action = 'Created'` for the new `DocumentVersion` (`FieldChangeInterceptor.cs:67-69`). Assert `MAX(sequence) = seqBefore`. |
| **Expected Notification** | n/a — no event is raised, so `NotificationDispatcher` is never reached. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-010`. |
| **Evidence** | HTTP capture · version table SQL · outbox count · `audit.audit_trail` MAX(sequence) before/after · field_change rows |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the executable evidence for `GAP-DOC-006`'s sibling observation: starting a revision of a controlled document leaves **no hash-chained ledger entry**. Record the absence; do not treat it as acceptable. |

#### TC-DOC-STATE-011 — S4 → S8: retiring a published document obsoletes its published version  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-001, RSK-DOC-003 |
| **Level / Type / Technique** | API (integration) · Functional (positive) · State Transition — S4→S8, entry into the terminal document status |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) · `documents.void` — `[RequirePermission(Documents, Void)]` (`DocumentsController.cs:135`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-011` in state **S4**: `status='Published'`, single version `(1,0) state='Published'`, `next_review_due` set. |
| **Test Data** | Body: none. Actor Layla (Quality Manager holds `documents.void`; Department Head does **not** — `SystemRoleCatalog.cs:131`). |
| **Steps** | 1. `POST /api/documents/{documentId}/retire` as Layla. 2. Read the status line. 3. `SELECT status, next_review_due, review_due_raised FROM qams.controlled_document WHERE id='{documentId}'`. 4. `SELECT major, minor, state FROM qams.document_version WHERE document_id='{documentId}'`. 5. `SELECT event_type FROM qams.outbox_event WHERE tenant_id='{demoLabTenantId}' AND occurred_at_utc >= '{t0}' ORDER BY occurred_at_utc`. |
| **Expected UI** | The stepper advances to `Obsolete`; the Publish, Retire and issue-controlled-copy controls disappear; the version table still lists `1.0` with a live download link (no watermark — `GAP-DOC-011`). |
| **Expected API** | `204 No Content`. `GET /api/documents/{documentId}` → `status = "Obsolete"`, `versions[0].state = "Obsolete"`. `GET /api/documents?status=Obsolete` includes the row with `publishedVersion = null` (the `FirstOrDefault` over `state == Published` finds nothing, `DocumentQueries.cs:37-40`). |
| **Expected DB** | `controlled_document.status='Obsolete'`; `document_version (1,0).state='Obsolete'`; `next_review_due` **unchanged** — `Retire` does not clear it (`ControlledDocument.cs:229-245`); `review_due_raised` unchanged. |
| **Expected Audit** | Two outbox rows in order: `…DocumentVersionObsoleted, NT.QAMS.Domain` (`"Version":"1.0"`, raised at `:240`) then `…DocumentRetired, NT.QAMS.Domain` (`"RetiredBy":"{laylaId}"`, raised at `:244`); two consecutive chained `audit.audit_trail` entries. |
| **Expected Notification** | n/a — neither `DocumentVersionObsoleted` nor `DocumentRetired` has a handler or a seeded `notification_rule` key. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-011`. |
| **Evidence** | HTTP capture · document + version SQL · ordered outbox list |
| **Result / Defect** | Not Run · — |
| **Notes** | `next_review_due` surviving retirement is harmless only because the sweep pre-filters on `Status == Published` (`ScheduledSweepService.cs:135-141`) — assert both halves so a future filter change is caught. |

#### TC-DOC-STATE-012 — S1 → S9: retiring a never-published draft leaves the draft version alive  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-003, RSK-DOC-004 |
| **Level / Type / Technique** | API (integration) · Functional (positive, defect-revealing) · State Transition — S1→S9; Branch coverage of `Retire`'s `published is null` branch (`:236-241`) |
| **Priority / Severity / Automation** | Medium · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) · `documents.void` (`DocumentsController.cs:135`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-012` in state **S1**: `status='Draft'`, single version `(1,0) state='Draft'`, nothing ever published, `next_review_due IS NULL`. |
| **Test Data** | Body: none. |
| **Steps** | 1. `POST /api/documents/{documentId}/retire` as Layla. 2. Read the status line. 3. `SELECT status FROM qams.controlled_document WHERE id='{documentId}'`. 4. `SELECT major, minor, state FROM qams.document_version WHERE document_id='{documentId}'`. 5. `SELECT event_type FROM qams.outbox_event WHERE tenant_id='{demoLabTenantId}' AND occurred_at_utc >= '{t0}'`. |
| **Expected UI** | The stepper reads `Obsolete`, yet the version table still shows `1.0` in state `Draft` — a visibly incoherent pair the SPA does not reconcile (`document-detail.component.ts:246` vs `:50-57`). |
| **Expected API** | `204 No Content`. Detail JSON: document `status = "Obsolete"` **and** `versions[0].state = "Draft"` (state **S9**). |
| **Expected DB** | `controlled_document.status='Obsolete'`; `document_version (1,0).state='Draft'` — untouched, because `PublishedVersion` was `null` and `Retire` only ever mutates the published row (`:236-241`). |
| **Expected Audit** | Exactly **one** new outbox row: `…DocumentRetired, NT.QAMS.Domain`. **No** `DocumentVersionObsoleted` row — assert its absence. One chained `audit.audit_trail` entry. |
| **Expected Notification** | n/a — `DocumentRetired` has no handler. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-012`. |
| **Evidence** | HTTP capture · document + version SQL showing the `Obsolete`/`Draft` pair · outbox list showing a single event |
| **Result / Defect** | Not Run · — |
| **Notes** | State **S9** is the precondition for the resurrection defect `GAP-DOC-004`, exercised by `TC-DOC-STATE-037/038/039`. This case only establishes and asserts the state; it does not attempt the resurrection. |

#### TC-DOC-STATE-013 — S7 → S9: retiring a document with an approved revision in flight  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-027 · RSK-DOC-003, RSK-DOC-004 |
| **Level / Type / Technique** | API (integration) · Functional (positive, defect-revealing) · State Transition — S7→S9; Pairwise on (document status × in-flight version state) |
| **Priority / Severity / Automation** | High · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) · `documents.void` (`DocumentsController.cs:135`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-013` in state **S7**: `status='Published'`, `(1,0) state='Published'`, `(1,1) state='Approved'` authored by Nadia and recommended by Omar. |
| **Test Data** | Body: none. |
| **Steps** | 1. `POST /api/documents/{documentId}/retire` as Layla. 2. Read the status line. 3. `SELECT major, minor, state FROM qams.document_version WHERE document_id='{documentId}' ORDER BY major, minor`. 4. `SELECT status FROM qams.controlled_document WHERE id='{documentId}'`. 5. `SELECT event_type, payload FROM qams.outbox_event WHERE tenant_id='{demoLabTenantId}' AND occurred_at_utc >= '{t0}' ORDER BY occurred_at_utc`. |
| **Expected UI** | Stepper reads `Obsolete`; `1.0` shows `Obsolete`; **`1.1` still shows `Approved`** and the Publish control is still rendered for holders of `documents.sign` because the SPA gates it on the permission key, not on document status (`document-detail.component.ts:80`). |
| **Expected API** | `204 No Content`. Detail JSON: document `status = "Obsolete"`, `versions[0] = {version:"1.1", state:"Approved"}`, `versions[1] = {version:"1.0", state:"Obsolete"}`. |
| **Expected DB** | `controlled_document.status='Obsolete'`; `(1,0).state='Obsolete'`; **`(1,1).state='Approved'` unchanged**; `count(*) WHERE state='Published'` = 0. |
| **Expected Audit** | Two outbox rows: `DocumentVersionObsoleted` with `"Version":"1.0"` then `DocumentRetired`. **No** event mentions `1.1` — assert that. |
| **Expected Notification** | n/a — neither event has a handler. |
| **Cleanup** | Retain this document as the fixture for `TC-DOC-STATE-039` in the same run, then delete it in the teardown transaction. |
| **Evidence** | HTTP capture · version table SQL · outbox payloads |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the exact precondition of the resurrection defect: an `Approved` in-flight version surviving inside an `Obsolete` document. `Retire` has no SoD guard and no e-signature (front matter §1.3, "Invariants NOT enforced"), so Layla alone can reach this state. |

#### TC-DOC-STATE-014 — S4 → S4: confirming the periodic review re-arms the cycle and clears the due flag  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · **no URS covers periodic review — `GAP-DOC-001`**; traced to `ControlledDocument.cs:195-205` · RSK-DOC-002 |
| **Level / Type / Technique** | API (integration) · Functional (positive) · State Transition — self-loop on S4 with a state-variable change (`NextReviewDue`, `ReviewDueRaised`) |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) · `documents.sign` — `[RequirePermission(Documents, Sign)]` on `POST /{id}/confirm-review` (`DocumentsController.cs:37`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-014` in state **S4** with `review_cycle_months = 12`, `next_review_due = '2026-07-01'` (in the past) and `review_due_raised = true` (set by a prior sweep). |
| **Test Data** | Body: none. `d0 = date(now() at time zone 'utc')` = `2026-08-01` at the stated inspection date. |
| **Steps** | 1. `SELECT next_review_due, review_due_raised FROM qams.controlled_document WHERE id='{documentId}'` and record. 2. `POST /api/documents/{documentId}/confirm-review` as Layla. 3. Read the status line. 4. Re-run the SELECT. 5. `SELECT event_type, payload FROM qams.outbox_event WHERE tenant_id='{demoLabTenantId}' AND occurred_at_utc >= '{t0}'`. |
| **Expected UI** | The review-due badge clears and the next-review date shown on the detail screen moves to `d0 + 12 months`. |
| **Expected API** | `204 No Content`. `GET /api/documents/{documentId}` → `nextReviewDue = "2027-08-01"` (i.e. `d0.AddMonths(12)`), document `status` still `"Published"`. |
| **Expected DB** | `controlled_document.next_review_due = d0 + interval '12 months'` (`:202`); `review_due_raised = false` (`:203`); no `document_version` row changes — `ConfirmPeriodicReview` touches no version. |
| **Expected Audit** | One outbox row `…DocumentReviewConfirmed, NT.QAMS.Domain` with `"ReviewerId":"{laylaId}"` and `"ReviewedOn":"{d0}"`; one chained `audit.audit_trail` entry; `audit.field_change` rows for `NextReviewDue` and `ReviewDueRaised`. |
| **Expected Notification** | n/a — `DocumentReviewConfirmed` has no handler and no seeded rule. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-014`. |
| **Evidence** | HTTP capture · before/after SQL of the two review columns · outbox payload |
| **Result / Defect** | Not Run · — |
| **Notes** | `[ID]` because no user requirement authorises the periodic-review cycle (`GAP-DOC-001`) — coverage cannot be claimed in the RTM. Note also that `ConfirmPeriodicReview` carries **no SoD guard and no e-signature** despite being gated on `documents.sign` (front matter §1.3). The date arithmetic base is `DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)` from the handler (`DocumentCommands.cs:203`), not the client clock. |

#### TC-DOC-STATE-015 — S6 → S6: confirming the periodic review while a revision is under review  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · none — `GAP-DOC-001`; traced to `ControlledDocument.cs:197-204` · RSK-DOC-002 |
| **Level / Type / Technique** | API (integration) · Functional (positive) · State Transition — self-loop on S6; Equivalence Partitioning over the "status is Published" partition {S4, S5, S6, S7} |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Tenant Administrator (`admin@demo-lab.local`) · `documents.sign` (`DocumentsController.cs:37`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-015` in state **S6**: `status='Published'`, `(1,0) state='Published'`, `(1,1) state='UnderReview'`, `review_cycle_months = 6`, `next_review_due` in the past, `review_due_raised = true`. |
| **Test Data** | Body: none. |
| **Steps** | 1. `POST /api/documents/{documentId}/confirm-review` as `admin@demo-lab.local`. 2. Read the status line. 3. `SELECT next_review_due, review_due_raised FROM qams.controlled_document WHERE id='{documentId}'`. 4. `SELECT major, minor, state FROM qams.document_version WHERE document_id='{documentId}' ORDER BY major, minor`. |
| **Expected UI** | The review-due badge clears; the `1.1 / UnderReview` row is unaffected. |
| **Expected API** | `204 No Content`. Detail JSON: `nextReviewDue = d0 + 6 months`; `versions[0] = {version:"1.1", state:"UnderReview"}`, `versions[1] = {version:"1.0", state:"Published"}`. |
| **Expected DB** | `next_review_due = d0 + interval '6 months'`; `review_due_raised = false`; both `document_version` rows byte-identical to the precondition. |
| **Expected Audit** | One `DocumentReviewConfirmed` outbox row + chained `audit.audit_trail` entry. |
| **Expected Notification** | n/a — no handler for `DocumentReviewConfirmed`. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-015`. |
| **Evidence** | HTTP capture · review-column SQL · version table SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | The only condition in `ConfirmPeriodicReview` is `Status != DocumentStatus.Published` (`:197`); the in-flight version is irrelevant. This case pins that S5, S6 and S7 behave identically to S4 so a future guard addition is caught. |

#### TC-DOC-STATE-016 — Review-due flag raises exactly at `NextReviewDue == today`  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · none — `GAP-DOC-001`; traced to `ControlledDocument.cs:179-189` · RSK-DOC-002 |
| **Level / Type / Technique** | Integration (background service) · Functional (positive) · BVA — the `due <= today` boundary, on-point value `due == today`; also Multiple-Condition coverage of the four-term guard at `:181-182` |
| **Priority / Severity / Automation** | High · Major · Yes (integration) |
| **Role / Permission / Tenant** | System (`ScheduledSweepService`, leader-elected background service) · `n/a — the sweep runs elevated and cross-tenant via ICurrentTenantSetter.Elevate() (conventions §2)` · all tenants; assertions scoped to `demo-lab` |
| **Environment** | API `:5080` Development with the hosted `ScheduledSweepService` running (`BackgroundService`, 1-hour interval, 15 s startup delay) + live PostgreSQL `ntqams` |
| **Preconditions** | `SOP-STATE-016` in state **S4**; `UPDATE qams.controlled_document SET next_review_due = date(now() at time zone 'utc'), review_due_raised = false WHERE id='{documentId}'` executed with `SELECT set_config('app.bypass_rls','on',false)` first. |
| **Test Data** | `next_review_due = today` (on-point). Sweep predicate: `Status == Published && !ReviewDueRaised && NextReviewDue != null && NextReviewDue <= today` (`ScheduledSweepService.cs:135-141`). |
| **Steps** | 1. Restart the API (or wait for the next hourly tick) so the sweep runs; record `t0`. 2. `SELECT review_due_raised, next_review_due FROM qams.controlled_document WHERE id='{documentId}'`. 3. `SELECT event_type, payload FROM qams.outbox_event WHERE tenant_id='{demoLabTenantId}' AND event_type LIKE '%DocumentReviewDue%' AND occurred_at_utc >= '{t0}'`. 4. `SELECT subject, subject_ref, assignee_role, assignee_user_id, due_date, status FROM qams.work_task WHERE subject_ref = 'DOCREV:SOP-STATE-016'`. |
| **Expected UI** | The document list/detail shows the review-due state; no user action is involved in producing it. |
| **Expected API** | No HTTP call triggers this transition. `GET /api/documents/{documentId}` afterwards → `200` with `nextReviewDue` **unchanged** at today's date — `MarkReviewDueIfReached` does not move the date, only the flag. |
| **Expected DB** | `review_due_raised = true` (`:187`); `next_review_due` unchanged; exactly one `qams.work_task` row with `subject = 'Periodic review due: SOP-STATE-016 — {title}'`, `subject_ref = 'DOCREV:SOP-STATE-016'`, `assignee_user_id IS NULL`, `assignee_role = 'QualityManager'`, `due_date = today + 30 days`, `status = 'Pending'` (`DocumentReviewDuePolicy.cs:40-55`). |
| **Expected Audit** | One outbox row `…DocumentReviewDue, NT.QAMS.Domain` with `"DueOn":"{today}"`; one chained `audit.audit_trail` entry once the `OutboxProcessor` drains it. |
| **Expected Notification** | **None.** `DOC_REVIEW_DUE` is declared (`NotificationPolicies.cs:43`) but absent from the seed list (`:138-156`), so `NotificationDispatcher` returns at `:44-47` with `rules.Count == 0` and writes no dispatch row. Assert `SELECT count(*) FROM qams.notification_dispatch WHERE source_event_id = '{eventId}'` = 0. → `GAP-DOC-010`. |
| **Cleanup** | Delete the `work_task` row and the document in the teardown transaction with `app.bypass_rls = 'on'`. |
| **Evidence** | Sweep log line · document row SQL · outbox row · work_task row · notification_dispatch count |
| **Result / Defect** | Not Run · — |
| **Notes** | Sweep-to-task integration in depth belongs to batch C; this case is the **state edge** only — the boundary at which `ReviewDueRaised` flips false→true. Paired off-point is `TC-DOC-STATE-017`. |

#### TC-DOC-STATE-017 — Review-due flag does not raise at `NextReviewDue == today + 1`  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · none — `GAP-DOC-001`; traced to `ControlledDocument.cs:181-185` · RSK-DOC-002 |
| **Level / Type / Technique** | Integration (background service) · Functional (negative) · BVA — off-point `due == today + 1`, the first value that must **not** raise |
| **Priority / Severity / Automation** | High · Major · Yes (integration) |
| **Role / Permission / Tenant** | System (`ScheduledSweepService`) · `n/a — elevated background sweep` · `demo-lab` |
| **Environment** | API `:5080` Development with `ScheduledSweepService` running + live PostgreSQL `ntqams` |
| **Preconditions** | `SOP-STATE-017` in state **S4**; `UPDATE qams.controlled_document SET next_review_due = date(now() at time zone 'utc') + 1, review_due_raised = false WHERE id='{documentId}'`. |
| **Test Data** | `next_review_due = today + 1 day`. |
| **Steps** | 1. Record `t0`. 2. Trigger the sweep (restart the API or wait for the hourly tick). 3. `SELECT review_due_raised FROM qams.controlled_document WHERE id='{documentId}'`. 4. `SELECT count(*) FROM qams.outbox_event WHERE tenant_id='{demoLabTenantId}' AND event_type LIKE '%DocumentReviewDue%' AND occurred_at_utc >= '{t0}'`. 5. `SELECT count(*) FROM qams.work_task WHERE subject_ref='DOCREV:SOP-STATE-017'`. |
| **Expected UI** | No review-due indication appears on the document. |
| **Expected API** | `GET /api/documents/{documentId}` → `200`, `nextReviewDue` = `today + 1`, unchanged. |
| **Expected DB** | `review_due_raised` remains `false`; `next_review_due` unchanged. |
| **Expected Audit** | Zero `DocumentReviewDue` outbox rows for this document; the tenant's `audit.audit_trail` `MAX(sequence)` is unchanged by this document. |
| **Expected Notification** | n/a — no event is raised, and `DOC_REVIEW_DUE` would produce nothing anyway (`GAP-DOC-010`). |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-017`. |
| **Evidence** | Sweep log line · document row SQL · zero-count assertions |
| **Result / Defect** | Not Run · — |
| **Notes** | The document is excluded twice over: the sweep's SQL predicate `NextReviewDue <= today` (`ScheduledSweepService.cs:138`) never selects it, and even if it were passed to the aggregate, `due > today` returns at `:182`. Assert both by also invoking `MarkReviewDueIfReached(today)` in a domain unit test with `NextReviewDue = today.AddDays(1)`. |

#### TC-DOC-STATE-018 — Re-entry: a second sweep on the same cycle raises no second review-due event  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · none — `GAP-DOC-001`; traced to `ControlledDocument.cs:181,187` · RSK-DOC-002 |
| **Level / Type / Technique** | Integration (background service) · Functional (idempotence / re-entry) · State Transition — repeated stimulus in the same state; Condition coverage of the `ReviewDueRaised` term |
| **Priority / Severity / Automation** | High · Major · Yes (integration) |
| **Role / Permission / Tenant** | System (`ScheduledSweepService`) · `n/a — elevated background sweep` · `demo-lab` |
| **Environment** | API `:5080` Development with `ScheduledSweepService` running + live PostgreSQL `ntqams` |
| **Preconditions** | `TC-DOC-STATE-016` has completed against `SOP-STATE-016`: `review_due_raised = true`, `next_review_due = today`, one `Pending` `work_task` with `subject_ref = 'DOCREV:SOP-STATE-016'`. |
| **Test Data** | No data change between the two sweeps. |
| **Steps** | 1. Record `seqBefore = MAX(sequence)` from `audit.audit_trail` for the tenant and `taskCountBefore = count(*)` from `qams.work_task WHERE subject_ref='DOCREV:SOP-STATE-016'`. 2. Trigger a second sweep. 3. `SELECT count(*) FROM qams.outbox_event WHERE event_type LIKE '%DocumentReviewDue%' AND payload LIKE '%SOP-STATE-016%'`. 4. Re-read `MAX(sequence)` and the task count. |
| **Expected UI** | Unchanged; the Quality Manager's task list still shows exactly one periodic-review task for this document. |
| **Expected API** | `GET /api/documents/{documentId}` → `200`, `nextReviewDue` unchanged. |
| **Expected DB** | `review_due_raised` still `true`; `next_review_due` unchanged; `count(*)` of `work_task` with `subject_ref='DOCREV:SOP-STATE-016'` still `1`. |
| **Expected Audit** | Total `DocumentReviewDue` outbox rows for this document remains **1**; `MAX(sequence)` in `audit.audit_trail` unchanged by this document. |
| **Expected Notification** | n/a — no event is raised on the second pass. |
| **Cleanup** | Delete the `work_task` row and `SOP-STATE-016` in the teardown transaction. |
| **Evidence** | Two sweep log lines · outbox count = 1 · work_task count = 1 · audit_trail sequence delta = 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | Two independent idempotence mechanisms are in play and both should be asserted separately: the aggregate's `ReviewDueRaised` short-circuit (`:181`) and the policy's `SubjectRef` + `Pending` check (`DocumentReviewDuePolicy.cs:41-46`). If only one is exercised the other can rot silently. |

---

## Invalid transitions and terminal-state guards

#### TC-DOC-STATE-019 — S2: re-submitting a version already under review is refused `DOC-010`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-001, RSK-DOC-004 |
| **Level / Type / Technique** | API (integration) · Functional (negative, re-entry) · State Transition — illegal self-edge; Branch coverage of `RequireInFlight`'s `version.State != expected` branch (`ControlledDocument.cs:252-256`) |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (Nadia) · `n/a — /submit is ungated (DocumentsController.cs:91)` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-019` in state **S2**: `status='Draft'`, `(1,0) state='UnderReview'`. |
| **Test Data** | Body: none. Second consecutive submit. |
| **Steps** | 1. `POST /api/documents/{documentId}/submit` as Nadia (this is the second submit; the first produced S2). 2. Read the status line, the `Content-Type` and the body. 3. `SELECT state FROM qams.document_version WHERE document_id='{documentId}'`. 4. `SELECT count(*) FROM qams.outbox_event WHERE tenant_id='{demoLabTenantId}' AND occurred_at_utc >= '{t0}'`. |
| **Expected UI** | The SPA surfaces the problem `title` verbatim — `Cannot submit for review a version in state UnderReview.` — and no state changes (`documents.facade.ts:118-123`; the domain `code` is not shown to the user). |
| **Expected API** | `409 Conflict`, `Content-Type: application/problem+json`, body `title = "Cannot submit for review a version in state UnderReview."`, extension `code = "DOC-010"`, plus `traceId`/correlation id. 409 because `RequireInFlight` throws `InvalidStateTransitionException`, matched at `DomainExceptionHandler.cs:45-50` **before** any code-prefix rule. |
| **Expected DB** | `document_version.state` still `'UnderReview'`; `controlled_document.status` still `'Draft'`; no row inserted or updated. |
| **Expected Audit** | **Zero** new `qams.outbox_event` rows and zero new `audit.audit_trail` entries — the exception aborts before `SaveChangesAsync` (`DocumentCommands.cs:94-95`). No `audit.field_change` rows either. |
| **Expected Notification** | n/a — no event, no dispatch. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-019`. |
| **Evidence** | HTTP capture including the `code` extension · version row SQL · zero-count assertions |
| **Result / Defect** | Not Run · — |
| **Notes** | The message is interpolated from the live enum value (`$"Cannot {action} a version in state {version.State}."`, `:255`), so the assertion must match the exact string including the trailing period. |

#### TC-DOC-STATE-020 — S4: submitting when no version is in flight is refused `DOC-010`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-001 |
| **Level / Type / Technique** | API (integration) · Functional (negative) · State Transition — no-source-edge; Branch coverage of `RequireInFlight`'s null branch (`:249-250`) |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (Nadia) · `n/a — /submit is ungated (DocumentsController.cs:91)` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-020` in state **S4**: `status='Published'`, single version `(1,0) state='Published'` — `InFlightVersion` is `null` because `Published` is outside the in-flight set (`:81-82`). |
| **Test Data** | Body: none. |
| **Steps** | 1. `POST /api/documents/{documentId}/submit` as Nadia. 2. Read the status line and body. 3. `SELECT state FROM qams.document_version WHERE document_id='{documentId}'`. |
| **Expected UI** | Error banner reads `No version available to submit for review.`; the version table is unchanged. |
| **Expected API** | `409 Conflict`, `application/problem+json`, `title = "No version available to submit for review."`, `code = "DOC-010"`. Note the **different message, same code** as `TC-DOC-STATE-019` — both branches of `RequireInFlight` reuse the code passed by `SubmitForReview` (`:112`). |
| **Expected DB** | No change: `(1,0).state='Published'`, `controlled_document.status='Published'`. |
| **Expected Audit** | Zero new outbox rows, zero new `audit.audit_trail` entries, zero `audit.field_change` rows. |
| **Expected Notification** | n/a — no event. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-020`. |
| **Evidence** | HTTP capture · version row SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert both the code and the message; a test that checks only `DOC-010` cannot distinguish "wrong state" from "no version at all", and the two branches are separately reachable. |

#### TC-DOC-STATE-021 — S8: submitting on a retired document with no in-flight version is refused `DOC-010`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-003, RSK-DOC-004 |
| **Level / Type / Technique** | API (integration) · Functional (negative) · State Transition — terminal-state guard, S8 |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (Nadia) · `n/a — /submit is ungated (DocumentsController.cs:91)` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-021` in state **S8**: reached by publishing `1.0` then retiring — `status='Obsolete'`, `(1,0) state='Obsolete'`, no in-flight version. |
| **Test Data** | Body: none. |
| **Steps** | 1. `POST /api/documents/{documentId}/submit` as Nadia. 2. Read the status line and body. 3. `SELECT status FROM qams.controlled_document WHERE id='{documentId}'`; `SELECT state FROM qams.document_version WHERE document_id='{documentId}'`. |
| **Expected UI** | Error banner reads `No version available to submit for review.`; the document remains in the `Obsolete` step. |
| **Expected API** | `409 Conflict`, `application/problem+json`, `title = "No version available to submit for review."`, `code = "DOC-010"`. |
| **Expected DB** | `controlled_document.status='Obsolete'` and `(1,0).state='Obsolete'`, both unchanged. |
| **Expected Audit** | Zero new outbox / `audit.audit_trail` / `audit.field_change` rows. |
| **Expected Notification** | n/a — no event. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-021`. |
| **Evidence** | HTTP capture · document + version SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | **Important honesty note:** the refusal here comes from the *absence of an in-flight version*, **not** from a status guard — `SubmitForReview` (`:110-115`) never inspects `Status`. The identical call on state **S9** succeeds; see `TC-DOC-STATE-037` `[GD]`. |

#### TC-DOC-STATE-022 — S1: recommending a version still in Draft is refused `DOC-011`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-001 |
| **Level / Type / Technique** | API (integration) · Functional (negative) · State Transition — out-of-order edge; permission held, state forbids |
| **Priority / Severity / Automation** | Critical · Major · Yes (functional) |
| **Role / Permission / Tenant** | Department Head (Omar) — **holds** the gating key · `documents.approve` (`DocumentsController.cs:99`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-022` in state **S1**: `status='Draft'`, `(1,0) state='Draft'`, never submitted. Omar's `role_permission` set includes `documents.approve`, verified with `SELECT permission_key FROM qams.role_permission rp JOIN qams.role r ON r.id = rp.role_id WHERE r.id = (SELECT role_id FROM qams.user_account WHERE email='omar.head@demo-lab.local')`. |
| **Test Data** | Body: none. |
| **Steps** | 1. Confirm the permission is present with the SQL in Preconditions. 2. `POST /api/documents/{documentId}/recommend` as Omar. 3. Read the status line and body. 4. `SELECT state, recommended_by FROM qams.document_version WHERE document_id='{documentId}'`. |
| **Expected UI** | The Recommend control is rendered (Omar holds `documents.approve`, `document-detail.component.ts:70`) but the call fails and the banner reads `Cannot recommend a version in state Draft.` |
| **Expected API** | `409 Conflict`, `application/problem+json`, `title = "Cannot recommend a version in state Draft."`, `code = "DOC-011"`. **Not** `403 AUTHZ-403` — the permission filter passes (`RequirePermissionAttribute.cs:49-52`) and the refusal is a state refusal, not an authorization refusal. |
| **Expected DB** | `state='Draft'`, `recommended_by IS NULL`, `recommended_at_utc IS NULL`. |
| **Expected Audit** | Zero new outbox / `audit.audit_trail` / `audit.field_change` rows. |
| **Expected Notification** | n/a — no event. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-022`. |
| **Evidence** | Permission SQL · HTTP capture asserting `409` and `DOC-011`, and asserting the status is **not** `403` · version row SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the explicit "role holds the permission but the state forbids it" case for `Recommend`. Distinguishing `409 DOC-011` from `403 AUTHZ-403` is the assertion of record — conflating them would let a broken permission gate pass as a state guard. |

#### TC-DOC-STATE-023 — S3: re-entry — recommending an already-approved version is refused `DOC-011`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-001, RSK-DOC-004 |
| **Level / Type / Technique** | API (integration) · Functional (negative, re-entry) · State Transition — illegal self-edge on S3; double-recommend |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) · `documents.approve` (`DocumentsController.cs:99`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-023` in state **S3**: `status='Draft'`, `(1,0) state='Approved'`, `recommended_by = omarId`, `recommended_at_utc = R0`. |
| **Test Data** | Body: none. Second recommend, by a **different** actor (Layla) to prove the refusal is state-driven and not identity-driven. |
| **Steps** | 1. Record `recommended_by, recommended_at_utc`. 2. `POST /api/documents/{documentId}/recommend` as Layla. 3. Read the status line and body. 4. Re-read `recommended_by, recommended_at_utc, state`. |
| **Expected UI** | Banner reads `Cannot recommend a version in state Approved.`; the recorded recommender on screen is still Omar. |
| **Expected API** | `409 Conflict`, `application/problem+json`, `title = "Cannot recommend a version in state Approved."`, `code = "DOC-011"`. |
| **Expected DB** | `state='Approved'`; `recommended_by` still `omarId`; `recommended_at_utc` still `R0` — **not overwritten by Layla**. |
| **Expected Audit** | Zero new outbox / `audit.audit_trail` / `audit.field_change` rows; in particular no `RecommendedBy` field-change row. |
| **Expected Notification** | n/a — no event. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-023`. |
| **Evidence** | Before/after SQL of `recommended_by`/`recommended_at_utc` · HTTP capture |
| **Result / Defect** | Not Run · — |
| **Notes** | Protects the review attribution: a second reviewer must not be able to silently overwrite the recorded recommender. The guard is the state check at `:252-256`, reached before the SoD check at `:120`. |

#### TC-DOC-STATE-024 — S4: recommending when no version is in flight is refused `DOC-011`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-001 |
| **Level / Type / Technique** | API (integration) · Functional (negative) · State Transition — no-source-edge; permission held, state forbids |
| **Priority / Severity / Automation** | High · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Department Head (Omar) — **holds** the gating key · `documents.approve` (`DocumentsController.cs:99`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-024` in state **S4**: `status='Published'`, single version `(1,0) state='Published'`. |
| **Test Data** | Body: none. |
| **Steps** | 1. `POST /api/documents/{documentId}/recommend` as Omar. 2. Read the status line and body. 3. `SELECT state, recommended_by FROM qams.document_version WHERE document_id='{documentId}'`. |
| **Expected UI** | Banner reads `No version available to recommend.`; the published version keeps its original recommender. |
| **Expected API** | `409 Conflict`, `application/problem+json`, `title = "No version available to recommend."`, `code = "DOC-011"`. |
| **Expected DB** | `(1,0).state='Published'` and its `recommended_by`/`recommended_at_utc` unchanged. |
| **Expected Audit** | Zero new outbox / `audit.audit_trail` / `audit.field_change` rows. |
| **Expected Notification** | n/a — no event. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-024`. |
| **Evidence** | HTTP capture · version row SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | Proves `Recommend` can never reach a `Published` row: `InFlightVersion` excludes it, so the null branch of `RequireInFlight` fires first (`:249-250`). |

#### TC-DOC-STATE-025 — S1: rejecting a version still in Draft is refused `DOC-012`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-001 |
| **Level / Type / Technique** | API (integration) · Functional (negative) · State Transition — illegal edge; Branch coverage of `RejectVersion`'s state check (`:136-139`), which is inline and not `RequireInFlight` |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Department Head (Omar) — **holds** the gating key · `documents.approve` (`DocumentsController.cs:107`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-025` in state **S1**: `status='Draft'`, `(1,0) state='Draft'`, `rejection_reason IS NULL`. |
| **Test Data** | Body `{"reason":"Not ready"}` — non-empty, so the `NotEmpty()` validator (`DocumentCommands.cs:76`) passes and the request reaches the aggregate. |
| **Steps** | 1. `POST /api/documents/{documentId}/reject` as Omar with the body above. 2. Read the status line and body. 3. `SELECT state, rejection_reason FROM qams.document_version WHERE document_id='{documentId}'`. |
| **Expected UI** | Banner reads `Cannot reject a version in state Draft.` |
| **Expected API** | `409 Conflict`, `application/problem+json`, `title = "Cannot reject a version in state Draft."`, `code = "DOC-012"`. |
| **Expected DB** | `state='Draft'` unchanged; **`rejection_reason` still `NULL`** — the state guard at `:136-139` runs before the assignment at `:147`. |
| **Expected Audit** | Zero new outbox / `audit.audit_trail` / `audit.field_change` rows. |
| **Expected Notification** | n/a — no event. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-025`. |
| **Evidence** | HTTP capture · version row SQL showing `rejection_reason IS NULL` |
| **Result / Defect** | Not Run · — |
| **Notes** | Guard order inside `RejectVersion` is: in-flight exists (`:133-134`) → state is `UnderReview`/`Approved` (`:136-139`) → reason non-blank (`:141-144`). A blank reason on a *valid* state yields `422 DOC-013`, but FluentValidation returns `400` first — that pairing belongs to batch B's validation slice. |

#### TC-DOC-STATE-026 — S4: rejecting when nothing awaits review or approval is refused `DOC-012`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-001 |
| **Level / Type / Technique** | API (integration) · Functional (negative) · State Transition — no-source-edge; Branch coverage of `:133-134` |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) · `documents.approve` (`DocumentsController.cs:107`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-026` in state **S4**: `status='Published'`, single version `(1,0) state='Published'`. |
| **Test Data** | Body `{"reason":"Withdrawn by the quality manager"}`. |
| **Steps** | 1. `POST /api/documents/{documentId}/reject` as Layla. 2. Read the status line and body. 3. `SELECT state, rejection_reason FROM qams.document_version WHERE document_id='{documentId}'`. |
| **Expected UI** | Banner reads `No version is awaiting review or approval.`; the published version is untouched. |
| **Expected API** | `409 Conflict`, `application/problem+json`, `title = "No version is awaiting review or approval."`, `code = "DOC-012"`. |
| **Expected DB** | `(1,0).state='Published'`, `rejection_reason IS NULL`; `controlled_document.status='Published'`. |
| **Expected Audit** | Zero new outbox / `audit.audit_trail` / `audit.field_change` rows. |
| **Expected Notification** | n/a — no event. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-026`. |
| **Evidence** | HTTP capture · version row SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | A published version can never be "rejected" back out of circulation — the only route out is `Retire` (`TC-DOC-STATE-011`) or publishing a successor (`TC-DOC-STATE-009`). Assert the code so a future loosening is caught. |

#### TC-DOC-STATE-027 — S1: publishing a Draft version is refused `DOC-014` with no signature minted  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-026 · RSK-DOC-001, RSK-DOC-005 |
| **Level / Type / Technique** | API (integration) · Functional (negative) · Decision Table — DT-2 row 5; permission held, state forbids; asserts guard-before-signature ordering (`DocumentCommands.cs:133-140`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) — **holds** the gating key · `documents.sign` at the endpoint (`DocumentsController.cs:115`) and on the command (`DocumentCommands.cs:65-66`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-027` in state **S1**: `status='Draft'`, `(1,0) state='Draft'`, author Nadia. Layla's `pin_hash` set, `failed_login_attempts = 0`, `locked_until_utc IS NULL`. |
| **Test Data** | Body `{"password":"Demo-QM-Pass-3!","pin":"481902"}` — **correct** credentials, so the only possible refusal is the state guard. |
| **Steps** | 1. Record `sigCountBefore = SELECT count(*) FROM audit.electronic_signature WHERE subject_ref = 'DOC:{documentId:N}'` (after `SELECT set_config('app.bypass_rls','on',false)`). 2. `POST /api/documents/{documentId}/publish` as Layla with the body above. 3. Read the status line and body. 4. `SELECT state, approved_by FROM qams.document_version WHERE document_id='{documentId}'`. 5. Re-run the signature count. 6. `SELECT count(*) FROM audit.security_event WHERE event_type IN ('ESIGN_FAILED','ESIGN_LOCKED') AND detail LIKE '%{documentId:N}%'`. 7. `SELECT failed_login_attempts FROM qams.user_account WHERE email='layla.qm@demo-lab.local'`. |
| **Expected UI** | The publish dialog stays open and shows `Cannot publish a version in state Draft.`; the password and PIN fields are cleared by the form reset. |
| **Expected API** | `409 Conflict`, `application/problem+json`, `title = "Cannot publish a version in state Draft."`, `code = "DOC-014"`. |
| **Expected DB** | `(1,0).state='Draft'`, `approved_by IS NULL`, `approved_at_utc IS NULL`; `controlled_document.status='Draft'`, `next_review_due IS NULL`. |
| **Expected Audit** | `audit.electronic_signature` count **unchanged** at `sigCountBefore` — the state guard at `DocumentCommands.cs:136-140` runs before `signatures.SignAsync` at `:154`. **Zero** `ESIGN_FAILED`/`ESIGN_LOCKED` rows in `audit.security_event` for this subject. `user_account.failed_login_attempts` still `0` — `RegisterFailedLogin` was never called. Zero new outbox / `audit.audit_trail` rows. |
| **Expected Notification** | n/a — no `DocumentPublished` event, so no `DOC_PUBLISHED` dispatch. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-027`. |
| **Evidence** | HTTP capture · signature-count before/after · security_event zero-count · `failed_login_attempts` SQL · version row SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | The load-bearing property is that the append-only signature ledger must never hold a signature for a publish that then failed. Asserting `count(audit.electronic_signature)` unchanged is what proves it; asserting only the HTTP status does not. |

#### TC-DOC-STATE-028 — S2: publishing a version still under review is refused `DOC-014`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-026 · RSK-DOC-001 |
| **Level / Type / Technique** | API (integration) · Functional (negative) · State Transition — skipped-step edge S2→S4; Decision Table DT-2 row 5, second partition value |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) · `documents.sign` (`DocumentsController.cs:115`; `DocumentCommands.cs:65-66`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-028` in state **S2**: `status='Draft'`, `(1,0) state='UnderReview'`, never recommended (`recommended_by IS NULL`). |
| **Test Data** | Body `{"password":"Demo-QM-Pass-3!","pin":"481902"}`. |
| **Steps** | 1. Record `sigCountBefore` for `subject_ref = 'DOC:{documentId:N}'`. 2. `POST /api/documents/{documentId}/publish` as Layla. 3. Read the status line and body. 4. `SELECT state, recommended_by, approved_by FROM qams.document_version WHERE document_id='{documentId}'`. 5. Re-run the signature count. |
| **Expected UI** | Publish dialog shows `Cannot publish a version in state UnderReview.` |
| **Expected API** | `409 Conflict`, `application/problem+json`, `title = "Cannot publish a version in state UnderReview."`, `code = "DOC-014"`. |
| **Expected DB** | `state='UnderReview'`; `recommended_by IS NULL` and `approved_by IS NULL`; `controlled_document.status='Draft'`. |
| **Expected Audit** | `audit.electronic_signature` count unchanged; zero `ESIGN_*` `audit.security_event` rows; zero new outbox / `audit.audit_trail` rows. |
| **Expected Notification** | n/a — no `DocumentPublished`. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-028`. |
| **Evidence** | HTTP capture · signature-count before/after · version row SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | Blocks the "publish without a review step" bypass required by URS-025's ordered lifecycle. The check that fires is the handler's pre-check (`DocumentCommands.cs:136-140`), which shadows the aggregate's identical guard at `ControlledDocument.cs:153` — both must stay in place; a test that removes one should fail. |

#### TC-DOC-STATE-029 — S4: publishing with nothing awaiting approval returns `DOC-014` as **422**, not 409  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-026 · RSK-DOC-005 |
| **Level / Type / Technique** | API (integration) · Functional (negative, contract-defect) · Decision Table — DT-2 row 4; Error Guessing on the code↔status mapping |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) · `documents.sign` (`DocumentsController.cs:115`; `DocumentCommands.cs:65-66`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-029` in state **S4**: `status='Published'`, single version `(1,0) state='Published'` — `doc.InFlightVersion` is `null`. |
| **Test Data** | Body `{"password":"Demo-QM-Pass-3!","pin":"481902"}`. |
| **Steps** | 1. Record `sigCountBefore`. 2. `POST /api/documents/{documentId}/publish` as Layla. 3. Read the **status line**, `Content-Type` and the `code` extension. 4. `SELECT state FROM qams.document_version WHERE document_id='{documentId}'`. 5. Re-run the signature count. 6. Compare the status recorded here with the `409` recorded in `TC-DOC-STATE-028` for the same `code`. |
| **Expected UI** | Publish dialog shows `No version is awaiting approval.` — the SPA renders only the `title`, so the operator cannot tell the two `DOC-014` situations apart (`documents.facade.ts:118-123`). |
| **Expected API** | **`422 Unprocessable Entity`**, `application/problem+json`, `title = "No version is awaiting approval."`, `code = "DOC-014"`. 422 because `DocumentCommands.cs:130-131` throws a plain `DomainException`, which falls through to the catch-all arm at `DomainExceptionHandler.cs:75-80`, whereas `:138-139` throws `InvalidStateTransitionException` → 409. |
| **Expected DB** | `(1,0).state='Published'` unchanged; `controlled_document.status='Published'`, `next_review_due` unchanged. |
| **Expected Audit** | `audit.electronic_signature` count unchanged; zero `ESIGN_*` security events; zero new outbox / `audit.audit_trail` rows. |
| **Expected Notification** | n/a — no `DocumentPublished`. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-029`. |
| **Evidence** | HTTP capture · side-by-side comparison with `TC-DOC-STATE-028`'s capture showing `DOC-014` at two different statuses |
| **Result / Defect** | Not Run · — |
| **Notes** | Executable evidence for **`GAP-DOC-016`**: one domain code, two HTTP statuses. Author the case against the **observed** 422 and record the divergence; do not "correct" it to 409. A client that branches on the status rather than the code will mis-handle one of the two. |

#### TC-DOC-STATE-030 — S1: drafting a new version while one is in flight is refused `DOC-016` (422, not 409)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-027 · RSK-DOC-001, RSK-DOC-005 |
| **Level / Type / Technique** | API (integration) · Functional (negative) · State Transition — one-in-flight invariant; Error Guessing on the code↔exception-type mapping |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (Nadia) · `n/a — POST /{id}/versions is ungated (DocumentsController.cs:125)` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-030` in state **S1**: `status='Draft'`, single version `(1,0) state='Draft'`, nothing ever published. A second uploaded file `fileId2` exists in `qams.file_reference` for this tenant. |
| **Test Data** | Body `{"fileId":"{fileId2}","changeSummary":"Second attempt","bump":"Minor"}`. |
| **Steps** | 1. `POST /api/documents/{documentId}/versions` as Nadia. 2. Read the status line, `Content-Type` and the `code` extension. 3. `SELECT count(*) FROM qams.document_version WHERE document_id='{documentId}'`. |
| **Expected UI** | Banner reads `A version is already in progress; publish or reject it first.`; the version table still shows a single row. |
| **Expected API** | **`422 Unprocessable Entity`**, `application/problem+json`, `title = "A version is already in progress; publish or reject it first."`, `code = "DOC-016"`. 422 — not 409 — because `:216` throws a plain `DomainException`, unlike its neighbours `DOC-015` and `DOC-017` which throw `InvalidStateTransitionException`. |
| **Expected DB** | `count(*)` of `document_version` for this document remains **1**; no row with `fileId2` exists. |
| **Expected Audit** | Zero new outbox / `audit.audit_trail` / `audit.field_change` rows. |
| **Expected Notification** | n/a — no event. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-030`. |
| **Evidence** | HTTP capture asserting `422` and `DOC-016` · version row count |
| **Result / Defect** | Not Run · — |
| **Notes** | Also the executable evidence for new gap **`GAP-DOC-901`**: `DOC-017` ("Only a published document can be revised.", `:219-220`) is **unreachable through any API-driven sequence**. From `Status='Draft'` the seeded v1.0 is always in-flight so `DOC-016` fires first; from `Status='Published'` a published version always exists; from `Status='Obsolete'` `DOC-015` fires first at `:209-212`. Assert `DOC-016` here and record `DOC-017` as dead. |

#### TC-DOC-STATE-031 — S5: re-entry — a second concurrent revision is refused `DOC-016`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-027 · RSK-DOC-001 |
| **Level / Type / Technique** | API (integration) · Functional (negative, re-entry) · State Transition — illegal self-edge on S5 |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (Nadia) · `n/a — POST /{id}/versions is ungated (DocumentsController.cs:125)` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-031` in state **S5**: `status='Published'`, `(1,0) state='Published'`, `(1,1) state='Draft'`. Files `fileId3` uploaded and present in `qams.file_reference`. |
| **Test Data** | Body `{"fileId":"{fileId3}","changeSummary":"Third file","bump":"Major"}` — `bump = "Major"` is used deliberately to prove the refusal precedes any version-number computation at `:222-224`. |
| **Steps** | 1. `POST /api/documents/{documentId}/versions` as Nadia. 2. Read the status line and body. 3. `SELECT major, minor, state, file_id FROM qams.document_version WHERE document_id='{documentId}' ORDER BY major, minor`. |
| **Expected UI** | Banner reads `A version is already in progress; publish or reject it first.`; the version table still shows exactly `1.1` and `1.0`. |
| **Expected API** | `422 Unprocessable Entity`, `application/problem+json`, `title = "A version is already in progress; publish or reject it first."`, `code = "DOC-016"`. |
| **Expected DB** | Exactly two `document_version` rows; **no `(2,0)` row** and no row referencing `fileId3`. |
| **Expected Audit** | Zero new outbox / `audit.audit_trail` / `audit.field_change` rows. |
| **Expected Notification** | n/a — no event. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-031`. |
| **Evidence** | HTTP capture · full version table SQL proving no `(2,0)` row |
| **Result / Defect** | Not Run · — |
| **Notes** | The order in `DraftNewVersion` is: obsolete-status guard (`:209-212`) → in-flight guard (`:214-217`) → published-basis guard (`:219-220`) → numbering (`:222-224`). Asserting the absence of a `(2,0)` row proves execution stopped at the second guard. |

#### TC-DOC-STATE-032 — S8: a retired document refuses a new version with `DOC-015` (terminal guard)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-027 · RSK-DOC-003, RSK-DOC-004 |
| **Level / Type / Technique** | API (integration) · Functional (negative) · State Transition — terminal-state guard on S8 |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (Nadia) · `n/a — POST /{id}/versions is ungated (DocumentsController.cs:125)` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-032` in state **S8**: published then retired — `status='Obsolete'`, single version `(1,0) state='Obsolete'`, no in-flight version. File `fileId4` uploaded and present. |
| **Test Data** | Body `{"fileId":"{fileId4}","changeSummary":"Reinstating the SOP","bump":"Minor"}`. |
| **Steps** | 1. `POST /api/documents/{documentId}/versions` as Nadia. 2. Read the status line, `Content-Type` and the `code` extension. 3. `SELECT count(*) FROM qams.document_version WHERE document_id='{documentId}'`. 4. `SELECT status FROM qams.controlled_document WHERE id='{documentId}'`. |
| **Expected UI** | Banner reads `A retired document cannot receive new versions.`; the "new version" control is not offered on an `Obsolete` document. |
| **Expected API** | `409 Conflict`, `application/problem+json`, `title = "A retired document cannot receive new versions."`, `code = "DOC-015"` — an `InvalidStateTransitionException` (`:211`), hence 409 while its sibling `DOC-016` is 422. |
| **Expected DB** | `count(*)` of `document_version` remains **1**; `controlled_document.status='Obsolete'`. |
| **Expected Audit** | Zero new outbox / `audit.audit_trail` / `audit.field_change` rows. |
| **Expected Notification** | n/a — no event. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-032`. |
| **Evidence** | HTTP capture · version count · document status SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the **only** status-based guard `ControlledDocument` applies on the write side besides `DOC-018` and `DOC-020`; `SubmitForReview`, `Recommend`, `RejectVersion` and `Publish` have none — see `TC-DOC-STATE-037/038/039`. |

#### TC-DOC-STATE-033 — S9: guard ordering — the obsolete-status check precedes the in-flight check (`DOC-015`, not `DOC-016`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-004, RSK-DOC-005 |
| **Level / Type / Technique** | API (integration) · Functional (negative) · Error Guessing on guard order; Path coverage — the S9 path through `DraftNewVersion` where two guards would both fire |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (Nadia) · `n/a — POST /{id}/versions is ungated (DocumentsController.cs:125)` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-033` in state **S9**: reached by publishing `1.0`, drafting `1.1`, then retiring — `status='Obsolete'`, `(1,0) state='Obsolete'`, `(1,1) state='Draft'`. **Both** the status guard and the in-flight guard would fire. |
| **Test Data** | Body `{"fileId":"{fileId5}","changeSummary":"Second revision attempt","bump":"Minor"}`. |
| **Steps** | 1. `POST /api/documents/{documentId}/versions` as Nadia. 2. Read the `code` extension and the `title`. 3. Assert the code is exactly `DOC-015` and **not** `DOC-016`. 4. `SELECT count(*) FROM qams.document_version WHERE document_id='{documentId}'`. |
| **Expected UI** | Banner reads `A retired document cannot receive new versions.` — not the in-flight message. |
| **Expected API** | `409 Conflict`, `application/problem+json`, `title = "A retired document cannot receive new versions."`, `code = "DOC-015"`. Assert `code <> "DOC-016"` and `status <> 422`. |
| **Expected DB** | `count(*)` of `document_version` remains **2**; no new row. |
| **Expected Audit** | Zero new outbox / `audit.audit_trail` / `audit.field_change` rows. |
| **Expected Notification** | n/a — no event. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-033`. |
| **Evidence** | HTTP capture asserting the exact code and the negative assertion on `DOC-016` · version count |
| **Result / Defect** | Not Run · — |
| **Notes** | Order is fixed by `ControlledDocument.cs:209-217`: `Status == Obsolete` at `:209`, then `InFlightVersion is not null` at `:214`. Reversing them would change both the code **and** the HTTP status (409→422), which is why this deserves its own case rather than a note on `TC-DOC-STATE-032`. |

#### TC-DOC-STATE-034 — S8: re-entry — retiring an already-obsolete document is refused `DOC-018`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-003, RSK-DOC-004 |
| **Level / Type / Technique** | API (integration) · Functional (negative, re-entry) · State Transition — terminal-state self-edge; idempotence probe |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) — **holds** the gating key · `documents.void` (`DocumentsController.cs:135`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-034` in state **S8**: `status='Obsolete'`, `(1,0) state='Obsolete'`. |
| **Test Data** | Body: none. Second consecutive retire. |
| **Steps** | 1. Record `seqBefore = MAX(sequence)` from `audit.audit_trail` for the tenant. 2. `POST /api/documents/{documentId}/retire` as Layla. 3. Read the status line and body. 4. `SELECT status FROM qams.controlled_document WHERE id='{documentId}'`. 5. Re-read `MAX(sequence)`. |
| **Expected UI** | The Retire control is not rendered on an `Obsolete` document; driven directly over HTTP the banner reads `Document is already obsolete.` |
| **Expected API** | `409 Conflict`, `application/problem+json`, `title = "Document is already obsolete."`, `code = "DOC-018"`. **Not** `204` — retire is deliberately **not** idempotent. |
| **Expected DB** | `controlled_document.status='Obsolete'` unchanged; `(1,0).state='Obsolete'` unchanged. |
| **Expected Audit** | **No second `DocumentRetired` event** — `MAX(sequence)` unchanged, zero new outbox rows, zero new `audit.field_change` rows. Asserting the absence of a duplicate retire entry is the point: a duplicated ledger entry would corrupt the Part 11 narrative even though the data is identical. |
| **Expected Notification** | n/a — no event. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-034`. |
| **Evidence** | HTTP capture · document status SQL · `audit.audit_trail` sequence delta = 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | `DOC-018` is thrown at `:231-234` before any mutation, so `PublishedVersion` is never re-obsoleted and no spurious `DocumentVersionObsoleted` is raised. Assert both absences. |

#### TC-DOC-STATE-035 — S1: confirming a periodic review on an unpublished document is refused `DOC-020`  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · none — `GAP-DOC-001`; traced to `ControlledDocument.cs:197-200` · RSK-DOC-002 |
| **Level / Type / Technique** | API (integration) · Functional (negative) · State Transition — illegal edge; permission held, state forbids |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager (Layla) — **holds** the gating key · `documents.sign` (`DocumentsController.cs:37`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-035` in state **S1**: `status='Draft'`, `(1,0) state='Draft'`, `next_review_due IS NULL`, `review_due_raised = false`, `review_cycle_months = 24`. |
| **Test Data** | Body: none. |
| **Steps** | 1. `POST /api/documents/{documentId}/confirm-review` as Layla. 2. Read the status line and body. 3. `SELECT next_review_due, review_due_raised, status FROM qams.controlled_document WHERE id='{documentId}'`. |
| **Expected UI** | No confirm-review control is offered on a `Draft` document; driven over HTTP the banner reads `Only a published document undergoes periodic review.` |
| **Expected API** | `409 Conflict`, `application/problem+json`, `title = "Only a published document undergoes periodic review."`, `code = "DOC-020"`. Note this is a `409` despite `documents.sign` being satisfied — the endpoint filter passed and the domain refused. |
| **Expected DB** | `next_review_due` still `NULL`; `review_due_raised` still `false`; `status='Draft'`. |
| **Expected Audit** | Zero new outbox / `audit.audit_trail` / `audit.field_change` rows; in particular no `DocumentReviewConfirmed`. |
| **Expected Notification** | n/a — no event, and `DOC_REVIEW_DUE` is unseeded anyway (`GAP-DOC-010`). |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-035`. |
| **Evidence** | HTTP capture · review-column SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | Prevents a `next_review_due` being stamped on a document that has never been in force. `[ID]` — no URS covers the review cycle (`GAP-DOC-001`), so this asserts implemented behaviour only. |

#### TC-DOC-STATE-036 — S8: confirming a periodic review on a retired document is refused `DOC-020` (terminal guard)  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · none — `GAP-DOC-001`; traced to `ControlledDocument.cs:197-200` · RSK-DOC-002, RSK-DOC-003 |
| **Level / Type / Technique** | API (integration) · Functional (negative) · State Transition — terminal-state guard on S8; Equivalence Partitioning over the "status is not Published" partition {S1, S2, S3, S8, S9} |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Tenant Administrator (`admin@demo-lab.local`) · `documents.sign` (`DocumentsController.cs:37`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | `SOP-STATE-036` in state **S8**: published with `review_cycle_months = 12`, then retired — `status='Obsolete'`, `(1,0) state='Obsolete'`, `next_review_due` still holding the value stamped at publish, `review_due_raised = false`. |
| **Test Data** | Body: none. |
| **Steps** | 1. Record `next_review_due`. 2. `POST /api/documents/{documentId}/confirm-review` as `admin@demo-lab.local`. 3. Read the status line and body. 4. Re-read `next_review_due, review_due_raised, status`. |
| **Expected UI** | No confirm-review control on an `Obsolete` document; over HTTP the banner reads `Only a published document undergoes periodic review.` |
| **Expected API** | `409 Conflict`, `application/problem+json`, `title = "Only a published document undergoes periodic review."`, `code = "DOC-020"`. |
| **Expected DB** | `next_review_due` **identical to step 1** — the retired document's stale due date is not advanced; `review_due_raised = false`; `status='Obsolete'`. |
| **Expected Audit** | Zero new outbox / `audit.audit_trail` / `audit.field_change` rows. |
| **Expected Notification** | n/a — no event. |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-036`. |
| **Evidence** | HTTP capture · before/after `next_review_due` SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | Pair this with `TC-DOC-STATE-011`'s observation that `Retire` leaves `next_review_due` populated: the value is inert only because both the sweep predicate (`ScheduledSweepService.cs:136`) and this guard filter on `Status == Published`. Assert the stale value explicitly so a future filter change surfaces here. |

---

## Guards the aggregate does not implement — `[GD]`, gated on `GAP-DOC-004`

The three cases below describe behaviour the code **does not have**. They are written as the refusal that must exist, with acceptance criteria precise enough to implement against, and each records the currently observed (defective) behaviour in its Notes. **Do not execute them as passing expectations and do not rewrite them to match the current code.**

#### TC-DOC-STATE-037 — S9: submitting a version inside a retired document must be refused  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 ("retire" is the terminal step of the specified lifecycle) · RSK-DOC-003, RSK-DOC-004 · **Gap `GAP-DOC-004`** |
| **Level / Type / Technique** | API (integration) · Functional (negative) · State Transition — missing terminal-state guard on S9 |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) — **blocked until `GAP-DOC-004` is resolved** |
| **Role / Permission / Tenant** | Analyst (Nadia) · `n/a — POST /{id}/submit is ungated (DocumentsController.cs:91)` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | A document in state **S9** as established by `TC-DOC-STATE-012`: `controlled_document.status='Obsolete'` with an in-flight `document_version` in state `'Draft'`. |
| **Test Data** | Body: none. |
| **Steps** | 1. `POST /api/documents/{documentId}/submit` as Nadia. 2. Read the status line and the `code` extension. 3. `SELECT state FROM qams.document_version WHERE document_id='{documentId}' AND state <> 'Obsolete'`. 4. `SELECT status FROM qams.controlled_document WHERE id='{documentId}'`. |
| **Expected UI** | No submit control is offered on an `Obsolete` document, and a direct call surfaces a refusal message naming the retired status. |
| **Expected API** | **Required:** `409 Conflict`, `application/problem+json`, with a status guard code in the `DOC-0xx` series — the natural reuse is `DOC-015`'s sibling semantics, e.g. `code = "DOC-019"`, `title = "A retired document cannot progress its versions."` The exact code must be assigned when the gap is closed and recorded here before execution. |
| **Expected DB** | **Required:** the in-flight version's `state` remains `'Draft'`; `controlled_document.status` remains `'Obsolete'`. |
| **Expected Audit** | **Required:** zero new outbox rows, zero new `audit.audit_trail` entries — in particular no `DocumentSubmittedForReview` for a retired document. |
| **Expected Notification** | n/a — a refused transition emits nothing. |
| **Cleanup** | Teardown transaction deletes the fixture document. |
| **Evidence** | HTTP capture · version + document SQL · outbox zero-count |
| **Result / Defect** | Not Run · — |
| **Notes** | **Currently observed behaviour (read at `ControlledDocument.cs:110-115`): the call SUCCEEDS with `204` and the version moves `Draft → UnderReview` inside an `Obsolete` document.** `SubmitForReview` inspects only `InFlightVersion`, never `Status`. **Acceptance criteria for `GAP-DOC-004`:** (a) `SubmitForReview`, `Recommend`, `RejectVersion` and `Publish` each refuse when `Status == DocumentStatus.Obsolete`, with a documented `DOC-0xx` code mapped to HTTP 409; (b) `Retire` either cancels the in-flight version (moving it to a terminal state) or refuses while one is in flight, so that state S9 becomes unreachable; (c) whichever is chosen, the document's `Status` can never move `Obsolete → Published`; (d) a domain unit test asserts every one of the four refusals; (e) the chosen code appears in the module's error-code inventory and in the API-surface documentation. |

#### TC-DOC-STATE-038 — S9: recommending a version inside a retired document must be refused  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 · RSK-DOC-003, RSK-DOC-004 · **Gap `GAP-DOC-004`** |
| **Level / Type / Technique** | API (integration) · Functional (negative) · State Transition — missing terminal-state guard on S9; permission held, state must forbid |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) — **blocked until `GAP-DOC-004` is resolved** |
| **Role / Permission / Tenant** | Department Head (Omar) — holds the gating key · `documents.approve` (`DocumentsController.cs:99`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | A document in state **S9** with the in-flight version in `'UnderReview'`: publish `1.0`, draft `1.1`, submit `1.1`, then retire. `controlled_document.status='Obsolete'`, `(1,0) state='Obsolete'`, `(1,1) state='UnderReview'`. |
| **Test Data** | Body: none. Actor Omar ≠ author Nadia, so `SOD-DOC-001` cannot mask the result. |
| **Steps** | 1. `POST /api/documents/{documentId}/recommend` as Omar. 2. Read the status line and the `code` extension. 3. `SELECT major, minor, state, recommended_by FROM qams.document_version WHERE document_id='{documentId}' ORDER BY major, minor`. |
| **Expected UI** | No recommend control on an `Obsolete` document; a direct call surfaces the retired-status refusal. |
| **Expected API** | **Required:** `409 Conflict`, `application/problem+json`, with the `DOC-0xx` status-guard code assigned when the gap is closed (see `TC-DOC-STATE-037`). |
| **Expected DB** | **Required:** `(1,1).state` remains `'UnderReview'`; `(1,1).recommended_by` remains `NULL`; `controlled_document.status` remains `'Obsolete'`. |
| **Expected Audit** | **Required:** no `DocumentRecommended` outbox row and no `audit.audit_trail` entry for a retired document. |
| **Expected Notification** | n/a — a refused transition emits nothing. |
| **Cleanup** | Teardown transaction deletes the fixture document. |
| **Evidence** | HTTP capture · version table SQL · outbox zero-count |
| **Result / Defect** | Not Run · — |
| **Notes** | **Currently observed behaviour (read at `ControlledDocument.cs:117-129`): the call SUCCEEDS with `204`, `(1,1)` becomes `Approved` and `recommended_by`/`recommended_at_utc` are stamped inside an `Obsolete` document** — the front matter's §3.2 marks this cell ⚠️. `Recommend` inspects only the in-flight version's state and the SoD pair. Acceptance criteria as for `TC-DOC-STATE-037`. |

#### TC-DOC-STATE-039 — S9: publishing must not resurrect a retired document to `Published`  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-026, URS-027 · RSK-DOC-001, RSK-DOC-003 · **Gap `GAP-DOC-004`** |
| **Level / Type / Technique** | API (integration) · Functional (negative) · State Transition — forbidden edge `Obsolete → Published`; Path coverage of the full retire-then-publish sequence |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) — **blocked until `GAP-DOC-004` is resolved** |
| **Role / Permission / Tenant** | Quality Manager (Layla) — holds the gating key · `documents.sign` (`DocumentsController.cs:115`; `DocumentCommands.cs:65-66`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; fixture `F-DOC-STATE` |
| **Preconditions** | The document left by `TC-DOC-STATE-013` in state **S9** with an `Approved` in-flight version: `controlled_document.status='Obsolete'`, `(1,0) state='Obsolete'`, `(1,1) state='Approved'`, author Nadia. Layla's `pin_hash` set and account unlocked. |
| **Test Data** | Body `{"password":"Demo-QM-Pass-3!","pin":"481902"}` — correct credentials, so only a state guard can refuse. |
| **Steps** | 1. Record `sigCountBefore = SELECT count(*) FROM audit.electronic_signature WHERE subject_ref='DOC:{documentId:N}'`. 2. `POST /api/documents/{documentId}/publish` as Layla. 3. Read the status line and the `code` extension. 4. `SELECT status, next_review_due, review_due_raised FROM qams.controlled_document WHERE id='{documentId}'`. 5. `SELECT major, minor, state FROM qams.document_version WHERE document_id='{documentId}' ORDER BY major, minor`. 6. Re-run the signature count. 7. `SELECT count(*) FROM qams.notification_dispatch WHERE …` for a `DOC_PUBLISHED` dispatch on this document. |
| **Expected UI** | No publish control on an `Obsolete` document; a direct call surfaces the retired-status refusal and the stepper stays on `Obsolete`. |
| **Expected API** | **Required:** `409 Conflict`, `application/problem+json`, with the `DOC-0xx` status-guard code assigned when the gap is closed. |
| **Expected DB** | **Required:** `controlled_document.status` remains `'Obsolete'`; `next_review_due` and `review_due_raised` unchanged; `(1,1).state` remains `'Approved'` with `approved_by IS NULL`; `count(*) WHERE state='Published'` remains `0`. |
| **Expected Audit** | **Required:** `audit.electronic_signature` count unchanged at `sigCountBefore` — no Part 11 signature may be minted for the publication of a retired document; no `DocumentPublished` outbox row; no `audit.audit_trail` entry. |
| **Expected Notification** | **Required:** no `DOC_PUBLISHED` dispatch to `QualityManager,TenantAdmin`. |
| **Cleanup** | Teardown transaction deletes the fixture document; any `audit.electronic_signature` row produced by a defective run is append-only and must be reported, not deleted. |
| **Evidence** | HTTP capture · document + version SQL · signature-count before/after · notification_dispatch count |
| **Result / Defect** | Not Run · — |
| **Notes** | **Currently observed behaviour (read at `ControlledDocument.cs:151-173`): the call SUCCEEDS with `204`. `Publish` sets `Status = DocumentStatus.Published` unconditionally at `:169`, so an `Obsolete` document returns to force; a real Part 11 signature is minted at `DocumentCommands.cs:154`; `next_review_due` is re-armed at `:170`; and a `DOC_PUBLISHED` notification goes out.** This is the highest-severity cell of the front matter's §3.2 matrix and the anchor of exploratory charter `TC-DOC-EXPL-001`. The regulatory exposure is that a withdrawn procedure can be reinstated with a valid signature and no separate approval record. Acceptance criteria as for `TC-DOC-STATE-037`, with the additional criterion: **(f)** a functional test asserts `controlled_document.status` never transitions `Obsolete → Published` for any sequence of the seven lifecycle commands. |

#### TC-DOC-STATE-040 — S8: the sweep leaves a retired document alone  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · none — `GAP-DOC-001`; traced to `ScheduledSweepService.cs:135-141` and `ControlledDocument.cs:181` · RSK-DOC-002, RSK-DOC-003 |
| **Level / Type / Technique** | Integration (background service) · Functional (negative) · Multiple-Condition coverage — the `Status != DocumentStatus.Published` term of the four-term guard at `:181-182`, isolated with the other three terms all satisfying "would raise" |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (integration) |
| **Role / Permission / Tenant** | System (`ScheduledSweepService`) · `n/a — elevated background sweep` · `demo-lab` |
| **Environment** | API `:5080` Development with `ScheduledSweepService` running + live PostgreSQL `ntqams` |
| **Preconditions** | `SOP-STATE-040` in state **S8**; then, with `SELECT set_config('app.bypass_rls','on',false)`, `UPDATE qams.controlled_document SET next_review_due = date(now() at time zone 'utc') - 30, review_due_raised = false WHERE id='{documentId}'` — i.e. overdue and unflagged, so **only** the status term prevents the event. |
| **Test Data** | `status='Obsolete'`, `next_review_due = today - 30`, `review_due_raised = false`, `review_cycle_months = 12`. |
| **Steps** | 1. Record `t0`. 2. Trigger the sweep. 3. `SELECT review_due_raised FROM qams.controlled_document WHERE id='{documentId}'`. 4. `SELECT count(*) FROM qams.outbox_event WHERE event_type LIKE '%DocumentReviewDue%' AND payload LIKE '%SOP-STATE-040%' AND occurred_at_utc >= '{t0}'`. 5. `SELECT count(*) FROM qams.work_task WHERE subject_ref='DOCREV:SOP-STATE-040'`. |
| **Expected UI** | No periodic-review task appears in the Quality Manager's task list for this retired document. |
| **Expected API** | `GET /api/documents/{documentId}` → `200`, `status = "Obsolete"`, `nextReviewDue = today - 30` unchanged. |
| **Expected DB** | `review_due_raised` remains `false`; `next_review_due` unchanged; **zero** rows in `qams.work_task` with `subject_ref='DOCREV:SOP-STATE-040'`. |
| **Expected Audit** | Zero `DocumentReviewDue` outbox rows for this document; the tenant's `audit.audit_trail` `MAX(sequence)` unchanged by it. |
| **Expected Notification** | n/a — no event; and `DOC_REVIEW_DUE` produces nothing regardless (`GAP-DOC-010`). |
| **Cleanup** | Teardown transaction deletes `SOP-STATE-040`. |
| **Evidence** | Sweep log line · document row SQL · zero-count assertions on outbox and work_task |
| **Result / Defect** | Not Run · — |
| **Notes** | Two independent filters both exclude the row — the sweep's SQL `Where(Status == Published …)` (`:136`) and the aggregate's `Status != DocumentStatus.Published` (`:181`). To cover the aggregate term in isolation, add a domain unit test that calls `MarkReviewDueIfReached(today)` directly on a retired document with an overdue `NextReviewDue` and asserts `DomainEvents` is empty. |

---

## Batch coverage note

**Covered (40 cases, `TC-DOC-STATE-001` … `TC-DOC-STATE-040`, all `Result = Not Run`).** Eighteen valid transitions of the composite state machine — `SubmitForReview` (S1→S2, S5→S6), `Recommend` (S2→S3, S6→S7), `RejectVersion` (S2→S1, S3→S1, S7→S5), `Publish` (S3→S4 with no predecessor, S7→S4 with atomic obsolescence of the predecessor), `DraftNewVersion` (S4→S5), `Retire` (S4→S8, S1→S9, S7→S9), `ConfirmPeriodicReview` (self-loops on S4 and S6) and `MarkReviewDueIfReached` (on-point, off-point and re-entry). Eighteen invalid transitions carrying the code and status read at the throw site: `DOC-010` (three distinct reachable situations), `DOC-011` (three), `DOC-012` (two), `DOC-014` (three, including the 409/422 split), `DOC-015` (two, one of them a guard-ordering probe), `DOC-016` (two), `DOC-018` (one) and `DOC-020` (two). Terminal-state guards are covered on S8 for submit, new-version, retire and confirm-review; re-entry is covered for submit, recommend, new-version, retire and the sweep; "holds the permission but the state forbids" is covered explicitly for `documents.approve` (`TC-DOC-STATE-022`, `-024`), `documents.sign` (`-027`, `-029`, `-035`, `-036`) and `documents.void` (`-034`), each asserting the response is a `409`/`422` domain refusal and **not** `403 AUTHZ-403`. Three `[GD]` cases (`-037`, `-038`, `-039`) specify the guards `GAP-DOC-004` requires, with the currently observed defective behaviour recorded in the Notes rather than dressed up as a passing expectation.

**In my slice but not covered, with the reason.** (1) `Retire` from **S2**, **S3** and **S6** — behaviourally identical to the covered S1 and S7 cases (`Retire` branches only on `Status == Obsolete` and on `PublishedVersion is null`, `ControlledDocument.cs:229-245`); they are equivalence-class-covered, and adding them would consume ids without adding a distinct decision. (2) `RejectVersion` from **S6** (`UnderReview` revision) — same class as the covered S7 case; `:136` treats `UnderReview` and `Approved` identically. (3) `Publish` from **S5** — same `409 DOC-014` "Cannot publish a version in state Draft." as the covered S1 case. (4) `ConfirmPeriodicReview` from **S5**, **S7** and **S9** — the guard reads `Status` only, and S4/S6 (positive) plus S1/S8 (negative) already pin both sides. (5) `DOC-013` (blank rejection reason reaching the domain, `:143`) is **not executable through the API**: `RejectDocumentVersionValidator.NotEmpty()` (`DocumentCommands.cs:76`) returns `400` first, so the domain code needs a Domain.UnitTests case — deferred to the `TC-DOC-UNIT-*` block. (6) `DOC-017` is **unreachable by construction** — see the new gap below; it can only be shown negatively, which `TC-DOC-STATE-030` does. (7) `SOD-DOC-001`/`SOD-DOC-002` and DT-1/DT-2 in full, version-number arithmetic, the `SIG-*` ceremony, `AUTHZ-403`/`AUTHZ-002` denials, the 429 e-signature rate-limit partition, `CONCURRENCY-409` on a stale `xmin`, and the acknowledgement/controlled-copy machines are all **out of this slice** and belong to batches B–D as reserved in the front matter's ID table. (8) Everything asserted about the SPA is from `document-detail.component.ts` and `documents.facade.ts` as read in the front matter; the `shared/ui` components those templates compose were **not opened in this pass**, so any UI assertion that depends on `workflow-stepper`'s internals would be `[RNV]` — none of the cases above make such an assertion.

**New gaps found in this pass** (ids in the `GAP-DOC-9xx` range to avoid colliding with the front matter's sequence):

**`GAP-DOC-901` — `DOC-017` is unreachable through the public API.** `DraftNewVersion` throws `DOC-017` "Only a published document can be revised." at `ControlledDocument.cs:219-220` when `PublishedVersion is null`. That condition can never be satisfied at the point it is evaluated: the earlier guards make it dead. With `Status == 'Obsolete'` the method exits at `:209-212` with `DOC-015`; with `Status == 'Draft'` the version seeded by `Create` (`:106`) is always in the in-flight set, so `:214-217` exits with `DOC-016`; and with `Status == 'Published'` a published version always exists, because `Publish` is the only writer of `Status = Published` (`:169`) and it always leaves exactly one `Published` row, while `Retire` — the only path that obsoletes it — simultaneously sets `Status = Obsolete` (`:239-243`). **Impact:** a guard that is believed to protect the revision path contributes nothing, the code is untestable, and a reviewer reading it may assume revision-basis validation exists where the real protection is the in-flight guard. **Severity: Minor** (hygiene / dead defensive code — the same class as `GAP-DOC-002`'s dead `VersionState.Rejected`). **Recommended action:** architect to decide whether to remove `DOC-017` or to reorder the guards so it becomes the primary refusal for "revise a document that has never been published", in which case `TC-DOC-STATE-030`'s expected code changes from `DOC-016`/422 to `DOC-017`/409 and this file must be updated in the same commit. **Responsible role:** Architect; QA Manager for the case update.

**`GAP-DOC-902` — rejecting an `Approved` version leaves a stale recommender stamp.** `RejectVersion` sets `State = Draft` and `RejectionReason` (`ControlledDocument.cs:146-147`) but clears neither `RecommendedBy` nor `RecommendedAtUtc`, and `:136` explicitly admits `Approved` as a rejectable state. A version that was recommended, then rejected, therefore sits in `Draft` while `GET /api/documents/{id}` still reports `recommendedBy` and `recommendedAtUtc` from the revoked review (`DocumentQueries.cs:65`), which the detail screen renders. The mirror-image defect already noted in the front matter — `RejectionReason` never cleared on resubmission — means the two fields drift in opposite directions across a reject/fix/publish cycle. **Impact:** the version record misstates its own review history; a reviewer reading the API or the screen cannot tell a live recommendation from a revoked one, which is a Part 11 §11.10(e) accuracy concern on the audit-relevant view even though the hash-chained ledger itself is intact. **Severity: Moderate.** **Suggested acceptance criteria:** (a) rejecting a version out of `Approved` clears `RecommendedBy` and `RecommendedAtUtc`, or the DTO exposes an explicit "recommendation revoked" marker; (b) re-submitting a previously rejected version clears `RejectionReason`, or the DTO distinguishes "current rejection" from "historical rejection"; (c) a domain unit test asserts the chosen semantics for the sequence submit → recommend → reject → submit → recommend → publish; (d) the audit trail records the revocation as a field change. **Responsible role:** Architect (semantics); Product Owner (what the screen must show); QA Manager (case update). Asserted as observed behaviour by `TC-DOC-STATE-006`.
