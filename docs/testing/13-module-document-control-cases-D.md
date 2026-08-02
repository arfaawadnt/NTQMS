# DOC — Detailed Test Cases, Batch D

This batch consumes `TC-DOC-INT-001` … `TC-DOC-INT-040` and covers three slices of the Document Control module as built at **v1.51.2**: (1) **read-and-understand acknowledgements** — the receipt pinned to the current published `VersionLabel`, the idempotent repeat that returns the existing receipt id, re-opening by a revision, the `my-acknowledgement` self-status projection, and the Quality-Manager coverage view together with its `documents.view` gate; (2) the **controlled-copy register** — numbered issue pinned to the published version, register listing and ordering, `Close(Returned)` and `Close(Destroyed)`, the `CCP-010` one-shot immutability of a closed copy, the outcome-parsing partitions and the load-bearing guard order between `CCP-003` and `CCP-010`; and (3) **file download/preview and the audit events it does or does not produce**. Deliberately left to sibling batches: the aggregate lifecycle state machine, version numbering and `SOD-DOC-001/002` (batch A); the publish e-signature ceremony, the rate-limit partition and the full `/api/documents` HTTP status matrix (batch B); the periodic-review sweep, `DocumentReviewDue` and `ReviewCycleMonths` boundaries (batch C); dedicated cross-tenant RLS proofs on the five document tables and file-upload allow-list/sniffing cases (the front matter's batch-D reservation — see `GAP-DOC-900` in the coverage note); SPA journeys, accessibility and performance (batch E). All facts below were read at the cited `file:line` or measured read-only against dev database `ntqams`; nothing is inferred. Risk IDs are **minted** as `RSK-DOC-<NNN>` because `docs/validation/02-Functional-Risk-Assessment.md` carries area-level rows only (conventions §5).

---

#### TC-DOC-INT-001 — Acknowledgement receipt is pinned to the current published VersionLabel  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028 · RSK-DOC-001 (minted — FRA has area-level rows only) |
| **Level / Type / Technique** | Integration (API + DB) · Functional (positive) · Use Case — the primary read-and-understand path |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — `POST /{id}/acknowledge` carries no `[RequirePermission]`, only `[RequireInternalActor]` (`src/NT.QAMS.WebApi/Controllers/DocumentsController.cs:50-52`; `src/NT.QAMS.Application/DocumentControl/DocumentAcknowledgementSlice.cs:13`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Document `SOP-CAL-045` exists in `demo-lab` with `controlled_document.status='Published'` and exactly one `document_version` row in `state='Published'` with `version_label='1.0'`. No row in `qams.document_acknowledgement` for (`document_id`, `'1.0'`, actor). |
| **Test Data** | Actor `nadia@demo-lab.local` (Analyst, `user_account.id` = `:actorId`); `:docId` = the `SOP-CAL-045` id |
| **Steps** | 1. `POST /api/documents/{:docId}/acknowledge` with an empty JSON body and a valid bearer token. 2. Read the status and the `id` field of the body. 3. `SELECT id, document_id, document_code, version_label, user_id, acknowledged_at_utc, tenant_id FROM qams.document_acknowledgement WHERE document_id = :docId AND user_id = :actorId;`. 4. Compare the returned `id` with the row's `id`. |
| **Expected UI** | Detail screen's read-and-understand card switches from the prompt to `✓ … (v1.0) — <timestamp>` (`frontend/src/app/features/documents/document-detail.component.ts:128-136`). |
| **Expected API** | `200 application/json`, body `{"id":"<uuid-v7>"}` (`DocumentsController.cs:51-52`). |
| **Expected DB** | Exactly 1 row; `version_label = '1.0'`; `document_code = 'SOP-CAL-045'`; `user_id = :actorId`; `tenant_id` = the demo-lab tenant; `acknowledged_at_utc` = `IClock.UtcNow` at handling (`DocumentAcknowledgementSlice.cs:43-44`). |
| **Expected Audit** | One `audit.field_change` row: `entity_type='DocumentAcknowledgement'`, `action='Created'`, `entity_id` = the receipt id (tenant is dropped from the rendered key, `FieldChangeInterceptor.cs:167-182`), `actor_id = :actorId`, `reason IS NULL`. Within ~5 s (outbox `PollInterval = 2 s`, `Outbox/OutboxProcessor.cs:52`) one `audit.audit_trail` row with `event_type = 'NT.QAMS.Domain.DocumentControl.DocumentAcknowledged, NT.QAMS.Domain'` and `prev_hash` equal to the previous tip's `entry_hash`. |
| **Expected Notification** | n/a — `DocumentAcknowledged` has no handler and no notification rule (front matter §1.5). |
| **Cleanup** | `DELETE FROM qams.document_acknowledgement WHERE document_id = :docId AND user_id = :actorId;` (psql, tenant GUC set). |
| **Evidence** | HTTP response capture · SQL result set · `audit.field_change` + `audit.audit_trail` rows |
| **Result / Defect** | Not Run · — |
| **Notes** | Read `audit.*` in psql only after `SELECT set_config('app.bypass_rls','on',false);` or with the tenant GUC set (conventions §2). |

#### TC-DOC-INT-002 — Repeating the acknowledgement returns the existing receipt id and writes no second row  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028 · RSK-DOC-002 (minted) |
| **Level / Type / Technique** | Integration (API + DB) · Functional (idempotency) · Branch coverage — the `if (existing is { } id) return id;` true branch (`DocumentAcknowledgementSlice.cs:38-41`) |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — ungated endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | TC-DOC-INT-001 has run: one receipt exists for (`:docId`, `'1.0'`, `:actorId`). `SOP-CAL-045` still published at `1.0`. |
| **Test Data** | Same actor and document as TC-DOC-INT-001; record the first receipt id as `:ackId` |
| **Steps** | 1. `SELECT count(*) FROM qams.document_acknowledgement WHERE document_id=:docId AND user_id=:actorId;` → note `n0`. 2. `SELECT count(*) FROM audit.field_change WHERE entity_type='DocumentAcknowledgement' AND entity_id=:ackId::text;` → note `f0`. 3. `POST /api/documents/{:docId}/acknowledge` again. 4. Repeat step 3 a third time. 5. Re-run the counts from steps 1 and 2. |
| **Expected UI** | The card already shows the `✓` state; the acknowledge button is not rendered, so the repeat is only reachable via the API. |
| **Expected API** | Both repeats return `200` with `{"id":"<:ackId>"}` — the **same** uuid as the first call, not a new one. |
| **Expected DB** | Row count unchanged at `n0` (= 1). `acknowledged_at_utc` unchanged from the first call — the handler returns before `SaveChangesAsync` (`DocumentAcknowledgementSlice.cs:38-46`). |
| **Expected Audit** | `audit.field_change` count unchanged at `f0` and **no new** `audit.audit_trail` row: no `SaveChanges` runs on the idempotent path, so neither interceptor fires. |
| **Expected Notification** | n/a — none defined for acknowledgement. |
| **Cleanup** | `DELETE FROM qams.document_acknowledgement WHERE id = :ackId;` |
| **Evidence** | Three HTTP captures showing the identical `id` · before/after count pairs |
| **Result / Defect** | Not Run · — |
| **Notes** | The idempotency is a query-then-return in the handler, not a database upsert; the unique index (TC-DOC-INT-015) is the second line of defence. |

#### TC-DOC-INT-003 — Publishing v1.1 re-opens the acknowledgement and the v1.0 receipt is retained  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028, URS-027 · RSK-DOC-003 (minted) |
| **Level / Type / Technique** | Integration (API + DB) · Functional (positive) · State Transition — published-label change S4 → S5 → S4′ |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (acknowledger) + Quality Manager (publisher) · publisher needs `documents.sign` (`DocumentsController.cs:115`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SOP-CAL-045` published at `1.0`; `:actorId` holds a `1.0` receipt; a `1.1` revision has been drafted, submitted and recommended by a non-author, so the in-flight version is `Approved`. |
| **Test Data** | Publisher `layla@demo-lab.local`; `PublishDocumentRequest { Password: "Demo-Admin-Pass-2!", Pin: "<her PIN>" }`; bump `Minor` |
| **Steps** | 1. `POST /api/documents/{:docId}/publish` as Layla with the password + PIN. 2. `GET /api/documents/{:docId}/my-acknowledgement` as Nadia. 3. `POST /api/documents/{:docId}/acknowledge` as Nadia. 4. `SELECT version_label, acknowledged_at_utc FROM qams.document_acknowledgement WHERE document_id=:docId AND user_id=:actorId ORDER BY acknowledged_at_utc;`. |
| **Expected UI** | After the revision publishes, the read-and-understand card reverts to the prompt `(v1.1)` with the acknowledge button re-enabled (`document-detail.component.ts:132-135`). |
| **Expected API** | Step 1 → `204`. Step 2 → `200 {"publishedVersion":"1.1","acknowledged":false,"acknowledgedAtUtc":null}`. Step 3 → `200 {"id":"<new uuid>"}` — different from the `1.0` receipt id. |
| **Expected DB** | Exactly **2** rows for (`:docId`, `:actorId`): `version_label='1.0'` (original `acknowledged_at_utc` untouched — the entity has no mutator, `DocumentAcknowledgement.cs:21-26`) and `version_label='1.1'`. |
| **Expected Audit** | A second `audit.field_change` `Created` row for `entity_type='DocumentAcknowledgement'`, and a second `audit.audit_trail` entry with `event_type='NT.QAMS.Domain.DocumentControl.DocumentAcknowledged, NT.QAMS.Domain'`. |
| **Expected Notification** | `DOC_PUBLISHED` dispatch for the publish in step 1 to `QualityManager,TenantAdmin` (`Notifications/NotificationPolicies.cs:141-142`). No notification for the acknowledgement itself. |
| **Cleanup** | `DELETE FROM qams.document_acknowledgement WHERE document_id=:docId AND user_id=:actorId;` and roll the document back from a restore point (publishing is not reversible through the API). |
| **Evidence** | Publish + acknowledge captures · two-row SQL result showing both labels |
| **Result / Defect** | Not Run · — |
| **Notes** | Coverage semantics are implicit: nothing computes "who has not acknowledged 1.1". The `1.0` row simply stops matching the `published.VersionLabel` filter (`DocumentAcknowledgementSlice.cs:33-35`). |

#### TC-DOC-INT-004 — Acknowledging a document that has never published is refused with ACK-010  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028 · RSK-DOC-004 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — DT-4 row 4 (front matter §4.4) |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — ungated endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Document `SOP-QC-101` exists with `status='Draft'` and its only `document_version` row in `state='Draft'` — `PublishedVersion` is null (`ControlledDocument.cs:79`). |
| **Test Data** | `:draftDocId` = the `SOP-QC-101` id; actor `nadia@demo-lab.local` |
| **Steps** | 1. `POST /api/documents/{:draftDocId}/acknowledge`. 2. Read the status, `application/problem+json` body and the `code` member. 3. `SELECT count(*) FROM qams.document_acknowledgement WHERE document_id=:draftDocId;`. |
| **Expected UI** | The read-and-understand card is not rendered at all for a non-published document (`document-detail.component.ts:125` guards on `d.status === 'Published'`). |
| **Expected API** | `422 application/problem+json`, `code = "ACK-010"`, title `"Only a published document can be acknowledged."` (`DocumentAcknowledgementSlice.cs:28-29`; 422 mapping at `Middleware/DomainExceptionHandler.cs:75-80`). |
| **Expected DB** | Count remains 0 — the throw precedes any `Add`. |
| **Expected Audit** | No `audit.field_change` row and no `audit.audit_trail` row: the exception aborts before `SaveChangesAsync`. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — no state was written. |
| **Evidence** | problem+json capture · zero-count SQL result |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert the `code` member, not the title string — the title is human text and is what the SPA surfaces verbatim (`documents.facade.ts:118-123`). |

#### TC-DOC-INT-005 — Acknowledging a retired document is refused with ACK-010  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028, URS-025 · RSK-DOC-005 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning — the "no published version" partition reached from the far side of the lifecycle (state S8) |
| **Priority / Severity / Automation** | Medium · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — ungated endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | A document that was published at `1.0` and then retired: `controlled_document.status='Obsolete'` and its former published version now `state='Obsolete'` (`ControlledDocument.cs:236-241`), so `PublishedVersion` is null again. |
| **Test Data** | `:retiredDocId`; actor `nadia@demo-lab.local` |
| **Steps** | 1. `GET /api/documents/{:retiredDocId}` and confirm `status='Obsolete'` with no version in state `Published`. 2. `POST /api/documents/{:retiredDocId}/acknowledge`. 3. Read the status and `code`. |
| **Expected UI** | Card absent (status is not `Published`); the detail screen shows the obsolete notice (`document-detail.component.ts:103`). |
| **Expected API** | `422`, `code = "ACK-010"` — identical to TC-DOC-INT-004 because both partitions collapse to `PublishedVersion is null`. |
| **Expected DB** | No new `qams.document_acknowledgement` row; pre-existing receipts for the formerly published label are **not** deleted or invalidated. |
| **Expected Audit** | No new rows in `audit.field_change` or `audit.audit_trail`. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — no state was written. |
| **Evidence** | Detail response · problem+json capture |
| **Result / Defect** | Not Run · — |
| **Notes** | The retained-receipt observation is deliberate evidence for `GAP-DOC-005`'s sibling question: retirement leaves acknowledgement history in place with no marker. |

#### TC-DOC-INT-006 — Acknowledging an unknown document id returns DOC-404  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028 · RSK-DOC-006 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Error Guessing — non-existent identifier |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — ungated endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | No `qams.controlled_document` row with id `00000000-0000-0000-0000-0000000000ff` in any tenant. |
| **Test Data** | `:missingId = 00000000-0000-0000-0000-0000000000ff` |
| **Steps** | 1. `POST /api/documents/00000000-0000-0000-0000-0000000000ff/acknowledge`. 2. Read the status and `code`. 3. `POST /api/documents/not-a-guid/acknowledge` and read the status. |
| **Expected UI** | n/a — reachable only by direct API call; the SPA never constructs an id it did not receive from the list. |
| **Expected API** | Step 1 → `404 application/problem+json`, `code = "DOC-404"`, title `"Document not found."` (`DocumentAcknowledgementSlice.cs:26`; `-404` suffix → 404 at `DomainExceptionHandler.cs:26-82`). Step 3 → `404` from routing: the route constraint is `{id:guid}` (`DocumentsController.cs:50`), so a non-GUID never matches the action. |
| **Expected DB** | No rows written anywhere. |
| **Expected Audit** | None. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — no state was written. |
| **Evidence** | Two HTTP captures |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 3 distinguishes a routing 404 (no `code` member) from the domain `DOC-404`; assert the presence/absence of the `code` member, not just the status. |

#### TC-DOC-INT-007 — An External Auditor cannot acknowledge (AUTHZ-002)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028 · RSK-DOC-007 (minted) |
| **Level / Type / Technique** | API · Security (negative authorization) · Decision Table — DT-4 row 2 |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | External Auditor (`UserRole.ExternalAuditor`) · n/a — the gate is `[RequireInternalActor]`, not a permission key (`DocumentAcknowledgementSlice.cs:13`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | An account in `demo-lab` with `user_account.role = 5` (`ExternalAuditor`, `UserAccount.cs:10`) and a valid session. `SOP-CAL-045` published at `1.0`. |
| **Test Data** | `auditor@demo-lab.local` |
| **Steps** | 1. Sign in as the auditor and capture the bearer token. 2. `POST /api/documents/{:docId}/acknowledge`. 3. Read the status and `code`. 4. `GET /api/documents/{:docId}/my-acknowledgement` as the same auditor and read the status. |
| **Expected UI** | n/a — the SPA renders the acknowledge button for any authenticated user; the refusal surfaces as the problem title in the error banner. |
| **Expected API** | Step 2 → `403 application/problem+json`, `code = "AUTHZ-002"`, title `"Role 'ExternalAuditor' is not permitted to execute this action."` (`Application/Behaviors/AuthorizationBehavior.cs:75,83-84`). Step 4 → `200` — the query path returns before the policy switch (`AuthorizationBehavior.cs:44-47`), so a read is allowed. |
| **Expected DB** | No `qams.document_acknowledgement` row for the auditor. |
| **Expected Audit** | No `audit.field_change` row; the behaviour throws before the handler. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — no state was written. |
| **Evidence** | 403 problem+json capture · 200 capture from step 4 |
| **Result / Defect** | Not Run · — |
| **Notes** | The asymmetry in step 4 is intended behaviour of the CQRS gate (queries are exempt), and is the honest reason `documents.view` on the coverage endpoint is the module's only read gate. |

#### TC-DOC-INT-008 — my-acknowledgement reports not-acknowledged before any receipt exists  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028 · RSK-DOC-008 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Branch coverage — `ack is not null` false branch (`DocumentAcknowledgementSlice.cs:76-83`) |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — `GET /{id}/my-acknowledgement` carries no `[RequirePermission]` (`DocumentsController.cs:55-57`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SOP-CAL-045` published at `1.0`; no receipt for (`:docId`, `'1.0'`, `:actorId`). |
| **Test Data** | Actor `nadia@demo-lab.local` |
| **Steps** | 1. `DELETE FROM qams.document_acknowledgement WHERE document_id=:docId AND user_id=:actorId;` to establish the precondition. 2. `GET /api/documents/{:docId}/my-acknowledgement`. 3. Compare the body against the three-member `MyDocumentAcknowledgementDto` contract (`Contracts/DocumentControl/DocumentContracts.cs:33-34`). |
| **Expected UI** | Prompt state: `doc.ackPrompt (v1.0)` with the acknowledge button rendered (`document-detail.component.ts:132-135`). |
| **Expected API** | `200 application/json`, exactly `{"publishedVersion":"1.0","acknowledged":false,"acknowledgedAtUtc":null}`. |
| **Expected DB** | Unchanged — the handler is `AsNoTracking` and writes nothing (`DocumentAcknowledgementSlice.cs:66,76`). |
| **Expected Audit** | None — a query produces no `audit.field_change` and no outbox row. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — read-only. |
| **Evidence** | JSON body capture asserted member-by-member |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert all three members explicitly; a regression that drops `publishedVersion` would still satisfy a loose "acknowledged is false" check. |

#### TC-DOC-INT-009 — my-acknowledgement reports the receipt and its timestamp once recorded  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028 · RSK-DOC-008 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Branch coverage — `ack is not null` true branch |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — ungated query · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | TC-DOC-INT-001 has left one receipt for (`:docId`, `'1.0'`, `:actorId`). |
| **Test Data** | Actor `nadia@demo-lab.local`; `:ackAt` = the row's `acknowledged_at_utc` |
| **Steps** | 1. `SELECT acknowledged_at_utc FROM qams.document_acknowledgement WHERE document_id=:docId AND user_id=:actorId;` → `:ackAt`. 2. `GET /api/documents/{:docId}/my-acknowledgement`. 3. Compare `acknowledgedAtUtc` in the body with `:ackAt` to microsecond precision. |
| **Expected UI** | `✓ doc.ackDone (v1.0) — <medium date>` rendered in green (`document-detail.component.ts:131`). |
| **Expected API** | `200`, `{"publishedVersion":"1.0","acknowledged":true,"acknowledgedAtUtc":"<:ackAt in ISO-8601 with offset>"}`. |
| **Expected DB** | Unchanged. |
| **Expected Audit** | None — read-only path. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — read-only. |
| **Evidence** | SQL timestamp · JSON body capture |
| **Result / Defect** | Not Run · — |
| **Notes** | PostgreSQL `timestamptz` stores microseconds; compare truncated to microseconds, not to ticks. |

#### TC-DOC-INT-010 — my-acknowledgement on a never-published document returns 200 with a null label  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028 · RSK-DOC-009 (minted) |
| **Level / Type / Technique** | API · Functional (positive/edge) · Branch coverage — `publishedLabel is null` early return (`DocumentAcknowledgementSlice.cs:70-74`) |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — ungated query · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SOP-QC-101` exists in `status='Draft'` with no published version. |
| **Test Data** | `:draftDocId` |
| **Steps** | 1. `GET /api/documents/{:draftDocId}/my-acknowledgement`. 2. Assert the status is `200`, not `422`. 3. Assert the body members. |
| **Expected UI** | Card absent (status guard at `document-detail.component.ts:125`); the component stores `null` in `myAck` only on a thrown error, not here (`:317-321`). |
| **Expected API** | `200`, exactly `{"publishedVersion":null,"acknowledged":false,"acknowledgedAtUtc":null}`. **Not** `ACK-010` — the query has no published-version guard, unlike the command. |
| **Expected DB** | Unchanged. |
| **Expected Audit** | None. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — read-only. |
| **Evidence** | JSON body capture |
| **Result / Defect** | Not Run · — |
| **Notes** | The command/query asymmetry (422 vs 200) for the same precondition is intentional in code; record it as observed behaviour, not as a defect. |

#### TC-DOC-INT-011 — my-acknowledgement flips back to false when a revision publishes  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028, URS-027 · RSK-DOC-003 (minted) |
| **Level / Type / Technique** | Integration (API + DB) · Functional (positive) · Data Flow — define `version_label` at acknowledge, redefine `published.VersionLabel` at publish, use at the my-acknowledgement read |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst reader + Quality Manager publisher · publisher needs `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | As TC-DOC-INT-003 immediately before the publish: `1.0` published and acknowledged by `:actorId`; `1.1` in-flight and `Approved`. |
| **Test Data** | Publisher `layla@demo-lab.local` with her password + PIN |
| **Steps** | 1. `GET /api/documents/{:docId}/my-acknowledgement` as Nadia → record `acknowledged`. 2. `POST /api/documents/{:docId}/publish` as Layla. 3. `GET /api/documents/{:docId}/my-acknowledgement` as Nadia again. 4. `SELECT version_label FROM qams.document_acknowledgement WHERE document_id=:docId AND user_id=:actorId;`. |
| **Expected UI** | The card returns from the green `✓` state to the amber prompt showing `(v1.1)` on the next detail load. |
| **Expected API** | Step 1 → `{"publishedVersion":"1.0","acknowledged":true,…}`. Step 2 → `204`. Step 3 → `{"publishedVersion":"1.1","acknowledged":false,"acknowledgedAtUtc":null}`. |
| **Expected DB** | Still exactly one receipt row, `version_label='1.0'` — the publish does not touch `qams.document_acknowledgement` (no code path updates it; `ControlledDocument.Publish` at `:151-173` touches only versions and cycle fields). |
| **Expected Audit** | Publish-related `audit.field_change` rows on `ControlledDocument`/`DocumentVersion`; **no** rows on `DocumentAcknowledgement`. |
| **Expected Notification** | `DOC_PUBLISHED` dispatch to `QualityManager,TenantAdmin`. |
| **Cleanup** | Restore the document from a restore point; delete the test receipt. |
| **Evidence** | Two my-acknowledgement captures · publish 204 · one-row SQL result |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the observable half of `URS-028`'s "revising re-opens acknowledgement" — no push, no task and no notification is generated for the re-opening; the user only discovers it by opening the document. |

#### TC-DOC-INT-012 — Coverage view returns every receipt, newest first, with display names  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028 · RSK-DOC-010 (minted) |
| **Level / Type / Technique** | Integration (API + DB) · Functional (positive) · Use Case — the Quality-Manager coverage review |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · **`documents.view`** (`DocumentsController.cs:60-63`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SOP-CAL-045` has three receipts: Nadia@`1.0`, Omar@`1.0`, Nadia@`1.1`, inserted in that chronological order. The Quality Manager system role grants all seven `documents.*` keys (`Application/Authorization/SystemRoleCatalog.cs:102-118`). |
| **Test Data** | Reader `layla@demo-lab.local` |
| **Steps** | 1. `GET /api/documents/{:docId}/acknowledgements`. 2. Assert the array length is 3. 3. Assert element order is Nadia@`1.1`, Omar@`1.0`, Nadia@`1.0` — descending `acknowledgedAtUtc` (`DocumentAcknowledgementSlice.cs:99`). 4. Assert each element carries `userId`, `userDisplay`, `versionLabel`, `acknowledgedAtUtc` and nothing else (`DocumentContracts.cs:36-37`). |
| **Expected UI** | The coverage table renders three rows, `track a.userId + a.versionLabel`, each `v{versionLabel}` (`document-detail.component.ts:142-151`). |
| **Expected API** | `200 application/json`, a bare JSON array (not a paged envelope) of exactly 3 objects in the stated order. |
| **Expected DB** | Unchanged — `AsNoTracking` read (`DocumentAcknowledgementSlice.cs:97`). |
| **Expected Audit** | None — read-only. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | Delete the three seeded receipts. |
| **Evidence** | JSON array capture · SQL ordering check |
| **Result / Defect** | Not Run · — |
| **Notes** | The endpoint returns **all** labels, current and superseded, with no "covers the current version" flag; a reviewer must compare `versionLabel` against the published label by eye. |

#### TC-DOC-INT-013 — Coverage view is refused without documents.view (AUTHZ-403)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028 · RSK-DOC-011 (minted) |
| **Level / Type / Technique** | API · Security (negative authorization) · Decision Table — the module's only permission-gated read |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Custom tenant role holding **zero** `documents.*` keys · required `documents.view` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | A tenant role exists in `qams.role` with no rows in `qams.role_permission` whose key starts `documents.`; a user is assigned it via `user_account.role_id`. `SOP-CAL-045` has at least one receipt. |
| **Test Data** | `nodocs@demo-lab.local` |
| **Steps** | 1. Sign in as `nodocs@demo-lab.local`. 2. `GET /api/documents/{:docId}/acknowledgements`. 3. Read the status, `code` and content type. 4. `GET /api/documents/{:docId}/my-acknowledgement` as the same user and read the status. 5. `GET /api/documents/{:docId}` as the same user and read the status. |
| **Expected UI** | The coverage sub-table is not rendered — the component gates it on `perms.can('documents.view')` (`document-detail.component.ts:138,324`) — and the API is never called. |
| **Expected API** | Step 2 → `403 application/problem+json`, `code = "AUTHZ-403"`, title `"You do not have permission to perform this action."` (`WebApi/Authorization/RequirePermissionAttribute.cs:54-59`). Step 4 → `200` (ungated). Step 5 → `200` (ungated). |
| **Expected DB** | Unchanged. |
| **Expected Audit** | No `audit.field_change` row. The 403 is written by the authorization filter, which does not log a security event for document routes. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — read-only. |
| **Evidence** | 403 problem+json capture · the two contrasting 200s |
| **Result / Defect** | Not Run · — |
| **Notes** | Steps 4 and 5 are the point of the case: the same unprivileged account still reads the document and its own receipt. Cite `GAP-DOC-013` when reporting. |

#### TC-DOC-INT-014 — Coverage view renders "(unknown)" for a receipt whose user row is unresolvable  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028 · RSK-DOC-012 (minted) |
| **Level / Type / Technique** | Integration (DB-seeded) · Functional (negative/robustness) · Branch coverage — `names.TryGetValue(...)` false branch (`DocumentAcknowledgementSlice.cs:110`) |
| **Priority / Severity / Automation** | Low · Moderate · Yes (integration) |
| **Role / Permission / Tenant** | Quality Manager · `documents.view` · `demo-lab` |
| **Environment** | Local PostgreSQL `ntqams` + API `:5080` Development |
| **Preconditions** | A receipt row exists whose `user_id` matches no `qams.user_account.id` — insert it directly, since the command path always uses the authenticated actor. |
| **Test Data** | `INSERT INTO qams.document_acknowledgement (tenant_id, id, document_id, document_code, version_label, user_id, acknowledged_at_utc, created_at_utc) VALUES (:tenantId, gen_random_uuid(), :docId, 'SOP-CAL-045', '1.0', '00000000-0000-0000-0000-0000000000aa', now(), now());` (run with the tenant GUC set so the RLS `WITH CHECK` passes) |
| **Steps** | 1. Execute the insert. 2. `GET /api/documents/{:docId}/acknowledgements` as Layla. 3. Locate the element whose `userId` is `…00aa`. 4. Assert `userDisplay`. |
| **Expected UI** | The coverage table shows a row whose signer column reads `(unknown)`. |
| **Expected API** | `200`; the element is `{"userId":"00000000-0000-0000-0000-0000000000aa","userDisplay":"(unknown)","versionLabel":"1.0","acknowledgedAtUtc":"…"}`. No exception, no 500. |
| **Expected DB** | The seeded row persists; note `qams.document_acknowledgement` has **no** foreign key to `user_account` or `controlled_document` (measured `pg_constraint`: PK only) — `GAP-DOC-017`. |
| **Expected Audit** | The direct SQL insert bypasses EF, so no `audit.field_change` row exists for it — expected, and itself worth recording. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `DELETE FROM qams.document_acknowledgement WHERE user_id='00000000-0000-0000-0000-0000000000aa';` |
| **Evidence** | Insert transcript · JSON element capture |
| **Result / Defect** | Not Run · — |
| **Notes** | The absent FK is what makes this row insertable at all; the case doubles as the detective-query evidence `GAP-DOC-017` asks for. |

#### TC-DOC-INT-015 — The unique index blocks a duplicate receipt at the SQL layer  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028 · RSK-DOC-013 (minted) |
| **Level / Type / Technique** | Database · Data integrity (negative) · Error Guessing — bypass the handler's idempotency and hit the constraint directly |
| **Priority / Severity / Automation** | Medium · Major · Yes (integration, inside a rollback transaction) |
| **Role / Permission / Tenant** | n/a — direct `psql` session · n/a — SQL layer · `demo-lab` tenant GUC set |
| **Environment** | Local PostgreSQL `ntqams` (`qams_app`), transaction rolled back |
| **Preconditions** | One receipt exists for (`:tenantId`, `:docId`, `'1.0'`, `:actorId`). Measured index: `ux_doc_ack_tenant_document_version_user` UNIQUE on (`tenant_id`,`document_id`,`version_label`,`user_id`); PK is `(tenant_id, id)`. |
| **Test Data** | Duplicate tuple (`:tenantId`, new uuid, `:docId`, `'SOP-CAL-045'`, `'1.0'`, `:actorId`) |
| **Steps** | 1. `BEGIN; SELECT set_config('app.current_tenant', :tenantId::text, true);`. 2. `INSERT INTO qams.document_acknowledgement (tenant_id,id,document_id,document_code,version_label,user_id,acknowledged_at_utc,created_at_utc) VALUES (:tenantId, gen_random_uuid(), :docId,'SOP-CAL-045','1.0',:actorId, now(), now());`. 3. Capture the SQLSTATE and constraint name. 4. `ROLLBACK;`. 5. Repeat with `version_label='1.1'` to prove the index discriminates by label. |
| **Expected UI** | n/a — SQL-layer case with no UI surface. |
| **Expected API** | n/a — the API is not exercised; the handler's own idempotency (TC-DOC-INT-002) means the API cannot reach this constraint. |
| **Expected DB** | Step 2 → `ERROR: duplicate key value violates unique constraint "ux_doc_ack_tenant_document_version_user"`, SQLSTATE `23505`. Step 5 → the insert **succeeds** (different `version_label`), proving per-version granularity. |
| **Expected Audit** | None — raw SQL bypasses `FieldChangeInterceptor`. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `ROLLBACK;` after each step — nothing is committed. |
| **Evidence** | psql transcript with SQLSTATE and constraint name |
| **Result / Defect** | Not Run · — |
| **Notes** | No `23505` handler exists in the request pipeline (verified repo-wide by the front matter, `GAP-DOC-018`), so were the API ever to reach this constraint it would surface as an unhandled 500 — one reason to keep the handler-level idempotency test (TC-DOC-INT-002) as well. |

#### TC-DOC-INT-016 — The acknowledgement is chained into the tamper-evident audit trail  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-028 · RSK-DOC-014 (minted) |
| **Level / Type / Technique** | Integration (API + background service + DB) · Compliance (21 CFR Part 11 §11.10(e)) · Data Flow — domain event → `qams.outbox_event` → `audit.audit_trail` |
| **Priority / Severity / Automation** | High · Critical · Yes (integration) |
| **Role / Permission / Tenant** | Analyst · n/a — ungated endpoint · `demo-lab` |
| **Environment** | API `:5080` Development with `OutboxProcessor` running + live PostgreSQL `ntqams` |
| **Preconditions** | `SOP-CAL-045` published at `1.0`; no receipt for the actor; the outbox is drained (`SELECT count(*) FROM qams.outbox_event WHERE processed_at_utc IS NULL;` = 0). |
| **Test Data** | Actor `nadia@demo-lab.local` |
| **Steps** | 1. `SELECT sequence, entry_hash FROM audit.audit_trail WHERE tenant_id=:tenantId ORDER BY sequence DESC LIMIT 1;` → `:seq0`, `:hash0`. 2. `POST /api/documents/{:docId}/acknowledge`. 3. Within 1 s: `SELECT id, event_type, tenant_id, processed_at_utc FROM qams.outbox_event ORDER BY occurred_at_utc DESC LIMIT 1;`. 4. Wait 5 s (poll interval is 2 s, `OutboxProcessor.cs:52`). 5. `SELECT sequence, event_type, prev_hash, entry_hash FROM audit.audit_trail WHERE tenant_id=:tenantId ORDER BY sequence DESC LIMIT 1;`. 6. `GET /api/compliance/chain-verification`. |
| **Expected UI** | The document's audit-trail panel (`<qams-audit-trail [subject]="d.id" />`, `document-detail.component.ts:210`) gains the entry. |
| **Expected API** | Step 2 → `200 {"id":…}`. Step 6 → `200` with a verified/intact chain result for the tenant. |
| **Expected DB** | Step 3 → one row with `event_type = 'NT.QAMS.Domain.DocumentControl.DocumentAcknowledged, NT.QAMS.Domain'` (`Interceptors/OutboxInterceptor.cs:60-63`), `tenant_id = :tenantId`, `processed_at_utc IS NULL`. Step 5 → `sequence = :seq0 + 1`, same `event_type`, `prev_hash = :hash0` (`Compliance/ComplianceLedgerServices.cs:38-62`). |
| **Expected Audit** | Exactly one new `audit.audit_trail` row and one `audit.field_change` `Created` row for `entity_type='DocumentAcknowledgement'`; the `payload` column carries the serialised event with `DocumentId`, `DocumentCode`, `VersionLabel`, `UserId`. |
| **Expected Notification** | n/a — `DocumentAcknowledged` has no notification rule; `qams.notification_dispatch` gains no row. |
| **Cleanup** | Delete the receipt; audit and outbox rows are append-only and are **not** cleaned up. |
| **Evidence** | Outbox row · audit-trail row showing the hash link · chain-verification response |
| **Result / Defect** | Not Run · — |
| **Notes** | Chain hashes are computed over DB-read microsecond-truncated timestamps (conventions §2) — compare hashes, never recompute them in the test from an in-memory `DateTimeOffset`. |

#### TC-DOC-INT-017 — Issuing a controlled copy pins it to the published version as copy number 1  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-020 (minted) |
| **Level / Type / Technique** | Integration (API + DB) · Functional (positive) · Use Case — the primary distribution-register path |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · **`documents.edit`** (`DocumentsController.cs:70-73`) plus `[RequireInternalActor]` (`ControlledCopySlice.cs:13`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SOP-CAL-045` published at `1.0`; `SELECT count(*) FROM qams.document_controlled_copy WHERE document_id=:docId;` = 0. |
| **Test Data** | `POST` body `{"holder":"Balance Room — Bench 3"}` |
| **Steps** | 1. `POST /api/documents/{:docId}/controlled-copies` with the body above. 2. Read the status and `id`. 3. `SELECT id, copy_number, version_label, holder, status, issued_by, issued_at_utc, closed_by, closed_at_utc, tenant_id FROM qams.document_controlled_copy WHERE document_id=:docId;`. 4. `GET /api/documents/{:docId}/controlled-copies` and compare. |
| **Expected UI** | The copy register table gains a row `# 1 · v1.0 · Balance Room — Bench 3 · Issued · <issue date>` with Return/Destroy buttons (`document-detail.component.ts:175-189`). |
| **Expected API** | Step 1 → `200 application/json`, `{"id":"<uuid-v7>"}`. Step 4 → `200` array of one `ControlledCopyDto` (`DocumentContracts.cs:44-46`). |
| **Expected DB** | One row: `copy_number = 1` (`lastCopyNumber ?? 0` then `+ 1`, `ControlledCopySlice.cs:32-39`); `version_label = '1.0'` — the **published** label, not the in-flight one; `holder = 'Balance Room — Bench 3'` (trimmed, `DocumentControlledCopy.cs:60`); `status = 'Issued'`; `issued_by` = the actor; `closed_by IS NULL`; `closed_at_utc IS NULL`. `status` satisfies `ck_document_controlled_copy_status_domain CHECK (status IN ('Issued','Returned','Destroyed'))` (measured). |
| **Expected Audit** | One `audit.field_change` row `entity_type='DocumentControlledCopy'`, `action='Created'`. **No** `audit.audit_trail` row and **no** `qams.outbox_event` row — `Issue` raises no domain event (`DocumentControlledCopy.cs:40-65`); see `GAP-DOC-006` and TC-DOC-INT-033. |
| **Expected Notification** | n/a — no notification rule references controlled copies. |
| **Cleanup** | `DELETE FROM qams.document_controlled_copy WHERE document_id=:docId;` |
| **Evidence** | HTTP captures · SQL row · register listing |
| **Result / Defect** | Not Run · — |
| **Notes** | The em dash in the holder value also exercises non-ASCII round-tripping through `varchar(200)`; keep it in the data set. |

#### TC-DOC-INT-018 — The second issue takes the next copy number (lastCopyNumber + 1 = 2)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-021 (minted) |
| **Level / Type / Technique** | Integration (API + DB) · Functional (positive) · Data Flow — `lastCopyNumber` defined by the max query, used by `Issue` (`ControlledCopySlice.cs:32-39`) |
| **Priority / Severity / Automation** | Medium · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `documents.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | TC-DOC-INT-017 has left exactly one copy, number 1, status `Issued`. |
| **Test Data** | Second holder `"Reception — Binder A"`; third holder `"Store Room"` |
| **Steps** | 1. `POST /api/documents/{:docId}/controlled-copies` with `{"holder":"Reception — Binder A"}`. 2. `POST /api/documents/{:docId}/controlled-copies` with `{"holder":"Store Room"}`. 3. `SELECT copy_number, holder, status FROM qams.document_controlled_copy WHERE document_id=:docId ORDER BY copy_number;`. 4. Close copy 3 as `Destroyed`, then issue a fourth copy and re-read `copy_number`. |
| **Expected UI** | Register shows `#1`, `#2`, `#3` ascending (the query orders by `copy_number`, `ControlledCopySlice.cs:74`). |
| **Expected API** | Both posts → `200` with distinct `id` values. |
| **Expected DB** | Rows `1, 2, 3` with the stated holders, all `status='Issued'`. Step 4 → the new copy is `copy_number = 4`: the max query has no `status` filter, so a destroyed copy still consumes its number and numbers are never reused. |
| **Expected Audit** | One `audit.field_change` `Created` row per issue; no outbox or `audit.audit_trail` rows for the issues. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `DELETE FROM qams.document_controlled_copy WHERE document_id=:docId;` |
| **Evidence** | Three HTTP captures · ordered SQL result |
| **Result / Defect** | Not Run · — |
| **Notes** | Sequential issues only. The **concurrent** case (two simultaneous posts colliding on `ux_doc_copy_tenant_document_number` with no `23505` handler) is `GAP-DOC-018` and is charted as `TC-DOC-EXPL-003`, not written as an executable case here. |

#### TC-DOC-INT-019 — Issuing a copy of a document with no published version is refused with CCP-020  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-022 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — front matter §3.2, `IssueControlledCopy` row, states S1/S2/S3/S8/S9 |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `documents.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SOP-QC-101` in `status='Draft'` with no published version; and a second document retired to `status='Obsolete'`. |
| **Test Data** | `{"holder":"Bench 1"}`; `:draftDocId`, `:retiredDocId` |
| **Steps** | 1. `POST /api/documents/{:draftDocId}/controlled-copies` with the body. 2. Read the status and `code`. 3. `POST /api/documents/{:retiredDocId}/controlled-copies` with the body. 4. Read the status and `code`. 5. `SELECT count(*) FROM qams.document_controlled_copy WHERE document_id IN (:draftDocId, :retiredDocId);`. |
| **Expected UI** | The issue control is only rendered when `d.status === 'Published'` and the user holds `documents.edit` (`document-detail.component.ts:161`), so the SPA cannot reach this state. |
| **Expected API** | Steps 2 and 4 → `422 application/problem+json`, `code = "CCP-020"`, title `"Only a published document can have a controlled copy issued."` (`ControlledCopySlice.cs:29-30`). |
| **Expected DB** | Count = 0 — the throw precedes the copy-number query and the `Add`. |
| **Expected Audit** | No `audit.field_change` row; no outbox row. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — no state was written. |
| **Evidence** | Two problem+json captures · zero-count SQL result |
| **Result / Defect** | Not Run · — |
| **Notes** | Draft and Obsolete are the same equivalence class here — both collapse to `PublishedVersion is null` — but both are exercised because they are reached by opposite lifecycle paths. |

#### TC-DOC-INT-020 — A blank or whitespace-only holder is refused with CCP-001  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-023 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · BVA — the lower boundary of the holder string (`""`, `" "`, `"\t"`) |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `documents.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SOP-CAL-045` published at `1.0`. No FluentValidation validator exists for `IssueControlledCopyCommand` — verified by absence in `ControlledCopySlice.cs`; the domain guard is the only check. |
| **Test Data** | Bodies: `{"holder":""}`, `{"holder":"   "}`, `{"holder":"\t\n"}` |
| **Steps** | 1. `POST /api/documents/{:docId}/controlled-copies` with `{"holder":""}` → record status + `code`. 2. Repeat with `{"holder":"   "}`. 3. Repeat with `{"holder":"\t\n"}`. 4. `POST` with `{"holder":"A"}` (one character, the smallest valid value) → record status. 5. `SELECT copy_number, holder FROM qams.document_controlled_copy WHERE document_id=:docId;`. |
| **Expected UI** | The issue button is `[disabled]="!copyHolder.trim()"` (`document-detail.component.ts:164`), so the SPA blocks all three invalid values client-side; the API cases prove the server also refuses. |
| **Expected API** | Steps 1-3 → `422 application/problem+json`, `code = "CCP-001"`, title `"A copy holder (person, role, or location) is required."` (`DocumentControlledCopy.cs:44-47`). Step 4 → `200 {"id":…}`. |
| **Expected DB** | Exactly one row after step 4, `holder = 'A'`, `copy_number = 1`; no rows from steps 1-3. |
| **Expected Audit** | One `audit.field_change` `Created` row (from step 4 only). |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `DELETE FROM qams.document_controlled_copy WHERE document_id=:docId;` |
| **Evidence** | Four HTTP captures · SQL result |
| **Result / Defect** | Not Run · — |
| **Notes** | Note the copy number: the three refusals do not consume a number, because the max query runs after the published-version check but the domain guard throws before the `Add` and before `SaveChanges`. |

#### TC-DOC-INT-021 — A 200-character holder is accepted and stored trimmed at the column boundary  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-023 (minted) |
| **Level / Type / Technique** | Integration (API + DB) · Functional (boundary) · BVA — upper boundary of `document_controlled_copy.holder varchar(200)` (measured; migration `20260726214512_DocumentControlledCopy.cs:25`) |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `documents.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SOP-CAL-045` published at `1.0`; register empty. |
| **Test Data** | `H200` = the letter `X` repeated exactly 200 times; `H202` = `" " + X×200 + " "` (202 characters, trimming to exactly 200) |
| **Steps** | 1. `POST /api/documents/{:docId}/controlled-copies` with `{"holder":"<H200>"}`. 2. `SELECT length(holder) FROM qams.document_controlled_copy WHERE copy_number=1 AND document_id=:docId;`. 3. `POST` again with `{"holder":"<H202>"}`. 4. `SELECT length(holder) FROM qams.document_controlled_copy WHERE copy_number=2 AND document_id=:docId;`. |
| **Expected UI** | The holder input has no `maxlength` attribute (`document-detail.component.ts:163`), so the SPA does not pre-truncate; the register cell renders the full 200 characters. |
| **Expected API** | Steps 1 and 3 → `200 {"id":…}`. |
| **Expected DB** | Step 2 → `length = 200`. Step 4 → `length = 200` — `Holder = holder.Trim()` in the factory (`DocumentControlledCopy.cs:60`) removes the surrounding spaces before EF sizes the value. |
| **Expected Audit** | Two `audit.field_change` `Created` rows for `DocumentControlledCopy`, each with `new_value` null (a `Created` row records no property values, `FieldChangeInterceptor.cs:68-70`). |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `DELETE FROM qams.document_controlled_copy WHERE document_id=:docId;` |
| **Evidence** | Two HTTP captures · two `length()` results |
| **Result / Defect** | Not Run · — |
| **Notes** | The **201-character** over-boundary is deliberately **not** asserted here: no validator bounds the field, so the failure would be an Npgsql `22001` string-data-right-truncation surfacing as an unhandled 500 — see `GAP-DOC-903` in the coverage note. |

#### TC-DOC-INT-022 — Issuing a copy without documents.edit is refused with AUTHZ-403  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-024 (minted) |
| **Level / Type / Technique** | API · Security (negative authorization) · Decision Table — endpoint gate before the command |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst — seeded with `documents.view, create, edit, export` (`SystemRoleCatalog.cs:161`), so use instead a **custom tenant role** granting `documents.view` only · required `documents.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | A tenant role with `documents.view` and **no** `documents.edit` row in `qams.role_permission`; a user assigned to it. `SOP-CAL-045` published at `1.0`. |
| **Test Data** | `viewonly@demo-lab.local`; body `{"holder":"Bench 9"}` |
| **Steps** | 1. Sign in as `viewonly@demo-lab.local`. 2. `POST /api/documents/{:docId}/controlled-copies` with the body. 3. Read the status, `code`, content type. 4. `POST /api/documents/controlled-copies/{:anyCopyId}/close` with `{"outcome":"Returned"}` and read the status and `code`. 5. `GET /api/documents/{:docId}/controlled-copies` and read the status. |
| **Expected UI** | Both the issue control and the Return/Destroy buttons are hidden — `perms.can('documents.edit')` at `document-detail.component.ts:161,183` — while the register table itself still renders. |
| **Expected API** | Steps 2 and 4 → `403 application/problem+json`, `code = "AUTHZ-403"`, title `"You do not have permission to perform this action."` (`RequirePermissionAttribute.cs:54-59`; both routes gated at `DocumentsController.cs:71,76`). Step 5 → `200` — the register read is ungated. |
| **Expected DB** | No new `qams.document_controlled_copy` row and no change to the target copy's `status`. |
| **Expected Audit** | No `audit.field_change` rows — the filter short-circuits before MediatR. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — no state was written. |
| **Evidence** | Two 403 captures · one 200 capture |
| **Result / Defect** | Not Run · — |
| **Notes** | `documents.edit` is the only key protecting the register's write side; there is no separate "issue copy" or "close copy" permission in `PermissionCatalog` (`Domain/Authorization/PermissionCatalog.cs:116-121,145`). |

#### TC-DOC-INT-023 — The register listing returns copies in ascending number with the exact DTO shape  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-025 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — the register review |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · n/a — `GET /{id}/controlled-copies` carries no `[RequirePermission]` (`DocumentsController.cs:66-68`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Three copies for `:docId`: `#1 Issued`, `#2 Returned`, `#3 Destroyed`, inserted so that their `issued_at_utc` order is the reverse of their number order (to prove the sort key is `copy_number`, not time). |
| **Test Data** | Holders `"Bench 3"`, `"Reception"`, `"Store Room"` |
| **Steps** | 1. `GET /api/documents/{:docId}/controlled-copies`. 2. Assert length 3 and the order `1, 2, 3`. 3. Assert each element has exactly the members `id, copyNumber, versionLabel, holder, status, issuedBy, issuedAtUtc, closedAtUtc` (`DocumentContracts.cs:44-46`). 4. Assert `closedAtUtc` is null on `#1` and non-null on `#2` and `#3`. |
| **Expected UI** | Six-column register table `# / version / holder / status / issued / actions`; Return and Destroy buttons render only on `#1` (`document-detail.component.ts:169-189`). |
| **Expected API** | `200 application/json`, a bare array of three objects, ordered by `copyNumber` ascending (`ControlledCopySlice.cs:74`). `status` values are the enum names `"Issued"`, `"Returned"`, `"Destroyed"` (`.ToString()`, `:76`). |
| **Expected DB** | Unchanged — `AsNoTracking` (`ControlledCopySlice.cs:72`). |
| **Expected Audit** | None — read-only. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `DELETE FROM qams.document_controlled_copy WHERE document_id=:docId;` |
| **Evidence** | JSON array capture · member-name assertion |
| **Result / Defect** | Not Run · — |
| **Notes** | `ClosedBy` is deliberately **absent** from `ControlledCopyDto` — the API never discloses who returned or destroyed a copy, though the column is populated. Record that as an observed limitation of the register view. |

#### TC-DOC-INT-024 — The register listing is readable by an account with zero documents privileges  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · no URS covers the read gate — implementation-derived; cite `GAP-DOC-013` · RSK-DOC-026 (minted) |
| **Level / Type / Technique** | API · Security (permissive behaviour, recorded as observed) · Equivalence Partitioning — the "no `documents.*` grant" partition |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Custom tenant role with **no** `documents.*` keys · none required — the route has only `[Authorize]` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `nodocs@demo-lab.local` as in TC-DOC-INT-013. `SOP-CAL-045` has at least one issued copy. |
| **Test Data** | `nodocs@demo-lab.local` |
| **Steps** | 1. Sign in as `nodocs@demo-lab.local`. 2. `GET /api/documents/{:docId}/controlled-copies`. 3. Record the status and the holder values returned. 4. `GET /api/documents/{:docId}/signatures` and record the status. |
| **Expected UI** | The register card renders for this user because the card itself is not permission-gated — only its write controls are (`document-detail.component.ts:157-166`). |
| **Expected API** | Step 2 → `200` with the full register including holder names and issue dates. Step 4 → `200` — the signature manifest is likewise ungated (`DocumentsController.cs:45-47`). |
| **Expected DB** | Unchanged. |
| **Expected Audit** | None — read-only, and **no** access event is recorded for the read. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — read-only. |
| **Evidence** | 200 capture with holder names visible · privileges screen screenshot showing no document access |
| **Result / Defect** | Not Run · — |
| **Notes** | This case records permissive behaviour as **observed**, not as approved. It is the executable evidence under `GAP-DOC-013`; the "should be denied" expectation has no implementation and must not be authored as a passing case. |

#### TC-DOC-INT-025 — Close(Returned) closes the register entry and raises ControlledCopyClosed  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-027 (minted) |
| **Level / Type / Technique** | Integration (API + DB + outbox) · Functional (positive) · State Transition — `Issued → Returned` (front matter §3.4) |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `documents.edit` (`DocumentsController.cs:75-81`) · `demo-lab` |
| **Environment** | API `:5080` Development with `OutboxProcessor` running + live PostgreSQL `ntqams` |
| **Preconditions** | Copy `#1` exists for `:docId` in `status='Issued'`, `closed_by IS NULL`, `closed_at_utc IS NULL`. Note `:copyId` — the close route is document-independent (`POST /api/documents/controlled-copies/{copyId}/close`). |
| **Test Data** | Body `{"outcome":"Returned"}` |
| **Steps** | 1. `POST /api/documents/controlled-copies/{:copyId}/close` with the body. 2. Read the status. 3. `SELECT status, closed_by, closed_at_utc FROM qams.document_controlled_copy WHERE id=:copyId;`. 4. Wait 5 s and `SELECT event_type, payload FROM audit.audit_trail WHERE tenant_id=:tenantId ORDER BY sequence DESC LIMIT 1;`. 5. `GET /api/documents/{:docId}/controlled-copies` and assert `#1` shows `"Returned"` with a non-null `closedAtUtc`. |
| **Expected UI** | The `#1` row's status pill changes to `Returned` and both action buttons disappear — they render only while `cp.status === 'Issued'` (`document-detail.component.ts:183`). |
| **Expected API** | `204 No Content` (`DocumentsController.cs:79-80`). |
| **Expected DB** | `status = 'Returned'`; `closed_by` = the acting user id; `closed_at_utc` = `IClock.UtcNow` (`DocumentControlledCopy.cs:81-83`). |
| **Expected Audit** | Three `audit.field_change` `Modified` rows for `entity_type='DocumentControlledCopy'` — `Status` (`Issued` → `Returned`), `ClosedBy` (null → uuid), `ClosedAtUtc` (null → timestamp) — plus the base-entity `ModifiedAtUtc`/`ModifiedBy` rows if the auditable base stamps them. One `qams.outbox_event` and one `audit.audit_trail` row with `event_type = 'NT.QAMS.Domain.DocumentControl.ControlledCopyClosed, NT.QAMS.Domain'`, payload carrying `CopyId, DocumentId, DocumentCode, CopyNumber, Outcome:"Returned", TenantId` (`DocumentControlledCopy.cs:84,88-89`). |
| **Expected Notification** | n/a — no notification rule references `ControlledCopyClosed` (front matter §1.5). |
| **Cleanup** | `DELETE FROM qams.document_controlled_copy WHERE id=:copyId;` — audit rows remain by design. |
| **Evidence** | 204 capture · SQL row · field-change rows · audit-trail row |
| **Result / Defect** | Not Run · — |
| **Notes** | Contrast with TC-DOC-INT-017: **issue** produces no ledger entry, **closure** does. That asymmetry is `GAP-DOC-006`. |

#### TC-DOC-INT-026 — Close(Destroyed) closes the entry with the Destroyed outcome  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-027 (minted) |
| **Level / Type / Technique** | Integration (API + DB) · Functional (positive) · State Transition — `Issued → Destroyed` |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `documents.edit` · `demo-lab` |
| **Environment** | API `:5080` Development with `OutboxProcessor` running + live PostgreSQL `ntqams` |
| **Preconditions** | Copy `#2` exists for `:docId` in `status='Issued'`. |
| **Test Data** | Body `{"outcome":"Destroyed"}` |
| **Steps** | 1. `POST /api/documents/controlled-copies/{:copy2Id}/close` with the body. 2. Read the status. 3. `SELECT status, closed_by, closed_at_utc FROM qams.document_controlled_copy WHERE id=:copy2Id;`. 4. Wait 5 s and read the newest `audit.audit_trail` row's `payload`. |
| **Expected UI** | The `#2` row's pill reads `Destroyed`; action buttons gone. |
| **Expected API** | `204 No Content`. |
| **Expected DB** | `status = 'Destroyed'` — accepted by `ck_document_controlled_copy_status_domain` (measured live: `CHECK (status IN ('Issued','Returned','Destroyed'))`); `closed_by` and `closed_at_utc` populated. |
| **Expected Audit** | Same shape as TC-DOC-INT-025 with `Status` old `Issued` → new `Destroyed`, and the `audit.audit_trail` payload's `Outcome` member equal to `"Destroyed"` (`outcome.ToString()`, `DocumentControlledCopy.cs:84`). |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `DELETE FROM qams.document_controlled_copy WHERE id=:copy2Id;` |
| **Evidence** | 204 capture · SQL row · audit-trail payload |
| **Result / Defect** | Not Run · — |
| **Notes** | `Destroyed` is the ISO 17025 §8.3 "withdrawn from circulation" outcome; it is a status change only — no file, version or document state changes as a result. |

#### TC-DOC-INT-027 — A closed copy cannot be closed again (CCP-010 one-shot immutability)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-028 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · State Transition — the `Returned`/`Destroyed` rows of the §3.4 matrix; terminal-state re-entry |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `documents.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Copy `#1` is `Returned` (from TC-DOC-INT-025) and copy `#2` is `Destroyed` (from TC-DOC-INT-026); note their `closed_at_utc` values as `:t1`, `:t2`. |
| **Test Data** | Bodies `{"outcome":"Returned"}` and `{"outcome":"Destroyed"}` |
| **Steps** | 1. `POST /api/documents/controlled-copies/{:copyId}/close` with `{"outcome":"Returned"}` (already Returned) → record status, `code`, title. 2. Same copy with `{"outcome":"Destroyed"}` → record status and `code`. 3. `POST …/{:copy2Id}/close` with `{"outcome":"Returned"}` (already Destroyed) → record status, `code`, title. 4. `SELECT status, closed_by, closed_at_utc FROM qams.document_controlled_copy WHERE id IN (:copyId,:copy2Id);`. |
| **Expected UI** | Unreachable from the SPA — the buttons only render while `Issued`; the case is API-only. |
| **Expected API** | All three → `409 application/problem+json`, `code = "CCP-010"`. Titles: step 1 and 2 → `"Only an issued copy can be returned or destroyed (current: Returned)."`; step 3 → `"… (current: Destroyed)."` (`DocumentControlledCopy.cs:75-79`; `InvalidStateTransitionException` → 409 at `DomainExceptionHandler.cs:26-82`). |
| **Expected DB** | Both rows unchanged: `status`, `closed_by` and `closed_at_utc` still equal to `:t1`/`:t2` — the guard precedes every assignment. |
| **Expected Audit** | No new `audit.field_change` rows and no new `audit.audit_trail` rows: the exception aborts before `SaveChangesAsync` (`ControlledCopySlice.cs:59-60`). |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — no state changed. |
| **Evidence** | Three problem+json captures including the parenthesised current status · unchanged SQL rows |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert the parenthesised current status in the title — it is the only way the API discloses which terminal state the copy is in on a refusal. |

#### TC-DOC-INT-028 — Guard order: closing a Destroyed copy as "Issued" returns CCP-003, not CCP-010  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-029 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Multiple-Condition coverage — both guards false simultaneously; the outcome check at `DocumentControlledCopy.cs:70-73` precedes the status check at `:75-79` |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `documents.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | One copy in `status='Destroyed'` (`:copy2Id`) and one in `status='Issued'` (`:copy3Id`). |
| **Test Data** | Body `{"outcome":"Issued"}` |
| **Steps** | 1. `POST /api/documents/controlled-copies/{:copy2Id}/close` with `{"outcome":"Issued"}` → record status and `code`. 2. `POST …/{:copy3Id}/close` with `{"outcome":"Issued"}` → record status and `code`. 3. `POST …/{:copy2Id}/close` with `{"outcome":"Returned"}` → record status and `code` (the contrast case). 4. `SELECT status FROM qams.document_controlled_copy WHERE id IN (:copy2Id,:copy3Id);`. |
| **Expected UI** | n/a — the SPA only ever sends `'Returned'` or `'Destroyed'` (`document-detail.component.ts:184-185`); this is an API-contract case. |
| **Expected API** | Step 1 → `422`, `code = "CCP-003"` — `"Issued"` parses successfully via `Enum.TryParse` (`ControlledCopySlice.cs:52`) and is then refused by the **domain** outcome guard, which runs before the status guard. Step 2 → `422`, `code = "CCP-003"` (same reason, from an Issued copy). Step 3 → `409`, `code = "CCP-010"`. |
| **Expected DB** | Both rows unchanged: `:copy2Id` stays `Destroyed`, `:copy3Id` stays `Issued`. |
| **Expected Audit** | No new `audit.field_change` or `audit.audit_trail` rows. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — no state changed. |
| **Evidence** | Three problem+json captures showing the 422/422/409 split |
| **Result / Defect** | Not Run · — |
| **Notes** | The message text differs by layer for the same code: the slice says `"The outcome must be Returned or Destroyed."` for an unparseable string (`ControlledCopySlice.cs:54`) while the domain says `"A controlled copy can only be closed as Returned or Destroyed."` (`DocumentControlledCopy.cs:72`). Assert the `code`, and record which title was returned. |

#### TC-DOC-INT-029 — Accepted outcome partitions: case-insensitive names and numeric enum text  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-030 (minted) |
| **Level / Type / Technique** | API · Functional (positive/edge) · Equivalence Partitioning — the accepted input classes of `Enum.TryParse<ControlledCopyStatus>(…, ignoreCase: true, …)` (`ControlledCopySlice.cs:52`) |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `documents.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Four fresh copies in `status='Issued'`: `:cA`, `:cB`, `:cC`, `:cD`. Enum ordinals: `Issued=0, Returned=1, Destroyed=2` (`DocumentControlledCopy.cs:6`). |
| **Test Data** | `{"outcome":"returned"}` on `:cA`; `{"outcome":"RETURNED"}` on `:cB`; `{"outcome":"1"}` on `:cC`; `{"outcome":"2"}` on `:cD` |
| **Steps** | 1. Close `:cA` with `"returned"` → record status. 2. Close `:cB` with `"RETURNED"` → record status. 3. Close `:cC` with `"1"` → record status. 4. Close `:cD` with `"2"` → record status. 5. `SELECT id, status FROM qams.document_controlled_copy WHERE id IN (:cA,:cB,:cC,:cD);`. |
| **Expected UI** | n/a — the SPA sends only canonical `'Returned'`/`'Destroyed'`; this documents the API's tolerance. |
| **Expected API** | All four → `204 No Content`. |
| **Expected DB** | `:cA`, `:cB`, `:cC` → `status = 'Returned'`; `:cD` → `status = 'Destroyed'`. The stored value is the canonical enum name because the domain assigns the parsed enum and EF converts with `HasConversion<string>` — the client's casing never reaches the column. |
| **Expected Audit** | Four `ControlledCopyClosed` `audit.audit_trail` entries; each payload's `Outcome` member is the canonical `"Returned"`/`"Destroyed"`, never `"returned"` or `"1"`. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `DELETE FROM qams.document_controlled_copy WHERE id IN (:cA,:cB,:cC,:cD);` |
| **Evidence** | Four 204 captures · SQL status result · four audit payloads |
| **Result / Defect** | Not Run · — |
| **Notes** | Numeric-text acceptance is an artefact of `Enum.TryParse`, not a designed API affordance. Record it as observed; do not treat `"1"`/`"2"` as a supported contract. |

#### TC-DOC-INT-030 — Refused outcome partitions: "0", empty, and unknown text all return CCP-003  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-030 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning + BVA — the refused classes around the enum's numeric domain (`0` in range but not closable, `3` out of range, non-numeric text) |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `documents.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | One copy `:cE` in `status='Issued'`; note its `status`, `closed_by`, `closed_at_utc`. |
| **Test Data** | Outcomes: `"0"`, `"3"`, `"Shredded"`, `""`, `"   "`, `"Returned "` (trailing space) |
| **Steps** | 1. Close `:cE` with `{"outcome":"0"}` → status + `code`. 2. `"3"` → status + `code`. 3. `"Shredded"` → status + `code`. 4. `""` → status + `code`. 5. `"   "` → status + `code`. 6. `"Returned "` → status + `code`. 7. `SELECT status, closed_by, closed_at_utc FROM qams.document_controlled_copy WHERE id=:cE;`. |
| **Expected UI** | n/a — API-only; the SPA cannot produce these values. |
| **Expected API** | Step 1 → `422`, `code = "CCP-003"` (`"0"` parses to `Issued`, then the **domain** guard refuses — title `"A controlled copy can only be closed as Returned or Destroyed."`). Steps 2-5 → `422`, `code = "CCP-003"` from the **slice** guard, title `"The outcome must be Returned or Destroyed."` (`Enum.TryParse` fails; `"3"` is out of the declared range, but note `TryParse` accepts undeclared numerics for some enums — assert the actual code returned and record it). Step 6 → assert the observed result and record whether `Enum.TryParse` trimmed the trailing space; do not assume. |
| **Expected DB** | `:cE` unchanged throughout: `status='Issued'`, `closed_by IS NULL`, `closed_at_utc IS NULL` (except in step 6 if the trailing-space value is accepted — record which). |
| **Expected Audit** | No `audit.field_change` and no `audit.audit_trail` rows for any refused call. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `DELETE FROM qams.document_controlled_copy WHERE id=:cE;` |
| **Evidence** | Six problem+json captures · unchanged SQL row |
| **Result / Defect** | Not Run · — |
| **Notes** | Steps 2 and 6 are deliberately written as "assert the observed result" — `Enum.TryParse`'s handling of out-of-range numerics and of trailing whitespace was **not** read in the BCL source during this pass, so predicting it here would breach the honesty rule. Both throw sites are read; the parser's exact tolerance is not. |

#### TC-DOC-INT-031 — Closing an unknown copy id returns CCP-404  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-031 (minted) |
| **Level / Type / Technique** | API · Functional (negative) · Error Guessing — non-existent and cross-tenant identifiers on a document-independent route |
| **Priority / Severity / Automation** | Medium · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · `documents.edit` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` (two tenants provisioned) |
| **Preconditions** | No `qams.document_controlled_copy` row with id `00000000-0000-0000-0000-0000000000bb`. A second tenant holds a copy with id `:otherTenantCopyId` in `status='Issued'`. |
| **Test Data** | Body `{"outcome":"Returned"}` |
| **Steps** | 1. `POST /api/documents/controlled-copies/00000000-0000-0000-0000-0000000000bb/close` with the body → status + `code`. 2. `POST /api/documents/controlled-copies/{:otherTenantCopyId}/close` as a `demo-lab` user → status + `code`. 3. `SELECT status FROM qams.document_controlled_copy WHERE id=:otherTenantCopyId;` with the **other** tenant's GUC set. |
| **Expected UI** | n/a — the SPA only ever passes ids it received from the register listing. |
| **Expected API** | Steps 1 and 2 → `404 application/problem+json`, `code = "CCP-404"`, title `"Controlled copy not found."` (`ControlledCopySlice.cs:57-58`). Step 2 must **not** return 403 or 409 — the EF global query filter plus the `tenant_isolation` policy (measured `rls=t force=t`) make the row invisible, so the lookup misses. |
| **Expected DB** | Step 3 → the other tenant's copy is still `status='Issued'`, untouched. |
| **Expected Audit** | No `audit.field_change` rows in either tenant. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — no state changed. |
| **Evidence** | Two 404 captures · cross-tenant SQL proof |
| **Result / Defect** | Not Run · — |
| **Notes** | This route takes `copyId` only, with no document in the path (`DocumentsController.cs:75`), so tenant isolation is the **sole** thing preventing a cross-tenant close. The case is the minimum isolation proof for this slice; broader RLS proofs across all five document tables belong to the sibling RLS batch. |

#### TC-DOC-INT-032 — A copy of a superseded version is not marked obsolete in the register  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-029 · RSK-DOC-032 (minted) · **Gap-dependent on `GAP-DOC-005`** |
| **Level / Type / Technique** | Integration (API + DB) · Functional (gap-dependent) · State Transition — the missing `Issued(v1.0) → obsolete-marked` transition after `v1.1` publishes |
| **Priority / Severity / Automation** | High · Critical · No — cannot be automated until `GAP-DOC-005` is resolved |
| **Role / Permission / Tenant** | Quality Manager · `documents.edit` to issue, `documents.sign` to publish · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SOP-CAL-045` published at `1.0`; copy `#1` issued against `1.0` in `status='Issued'`; a `1.1` revision drafted, submitted and recommended (in-flight `Approved`). |
| **Test Data** | Publisher `layla@demo-lab.local` with password + PIN |
| **Steps** | 1. `GET /api/documents/{:docId}/controlled-copies` → confirm `#1` is `v1.0`, `Issued`. 2. `POST /api/documents/{:docId}/publish` → `204`. 3. `GET /api/documents/{:docId}/controlled-copies` again. 4. `SELECT version_label, status FROM qams.document_controlled_copy WHERE document_id=:docId;`. |
| **Expected UI** | **Current build:** the register still renders `#1 · v1.0 · Issued` with no visual distinction from a current copy — the template compares nothing against the published label (`document-detail.component.ts:175-189`). **Required after the gap is closed:** the row must be visibly marked as holding a superseded version. |
| **Expected API** | **Current build:** step 3 returns `#1` with `versionLabel:"1.0"`, `status:"Issued"`, `closedAtUtc:null` — unchanged by the publish; `ControlledCopyDto` has no field able to express obsolescence (`DocumentContracts.cs:44-46`). **Acceptance criterion for the fix:** the register response must expose a derived boolean (e.g. `supersededVersion`) or an equivalent status so a client can distinguish a copy of the current published version from a copy of a superseded one, computed as `versionLabel <> <current published label>`. |
| **Expected DB** | **Current build:** `version_label='1.0'`, `status='Issued'` — no code path updates a copy when its document is revised or retired (verified by absence in `ControlledDocument.Publish` `:151-173` and `Retire` `:229-245`). **Acceptance criterion:** either a persisted marker updated in the same transaction as the publish, or a documented derived-at-read computation; the choice must be recorded in the fix's ADR. |
| **Expected Audit** | **Current build:** no `audit.field_change` row on `DocumentControlledCopy` results from the publish. **Acceptance criterion:** if a persisted marker is chosen, its change must produce a field-change row attributable to the publishing act. |
| **Expected Notification** | **Current build:** none. **Acceptance criterion (for the product owner to confirm):** whether holders of superseded copies must be notified is an open requirement question, not an assumed behaviour. |
| **Cleanup** | Restore the document from a restore point; delete the test copy. |
| **Evidence** | Two register captures bracketing the publish · SQL showing the unchanged row |
| **Result / Defect** | Not Run · — |
| **Notes** | The aggregate's own XML comment claims "a copy of a superseded version is visibly obsolete in the register" (`DocumentControlledCopy.cs:12-13`) — the code does not implement it. Do **not** author this as a passing case; it is the executable form of `GAP-DOC-005`'s acceptance criteria. |

#### TC-DOC-INT-033 — Issuing a controlled copy leaves no audit-ledger entry  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · no URS requires an issue event — implementation-derived; cite `GAP-DOC-006` · RSK-DOC-033 (minted) |
| **Level / Type / Technique** | Integration (API + DB) · Compliance (observed shortfall, recorded as-is) · Data Flow — the absent define of a domain event at the `Issue` mutation site |
| **Priority / Severity / Automation** | High · Major · Yes (integration) |
| **Role / Permission / Tenant** | Quality Manager · `documents.edit` · `demo-lab` |
| **Environment** | API `:5080` Development with `OutboxProcessor` running + live PostgreSQL `ntqams` |
| **Preconditions** | Outbox drained (`SELECT count(*) FROM qams.outbox_event WHERE processed_at_utc IS NULL;` = 0). `SOP-CAL-045` published at `1.0`. |
| **Test Data** | Body `{"holder":"Bench 7"}` |
| **Steps** | 1. `SELECT max(sequence) FROM audit.audit_trail WHERE tenant_id=:tenantId;` → `:seq0`. 2. `SELECT count(*) FROM qams.outbox_event;` → `:n0`. 3. `POST /api/documents/{:docId}/controlled-copies` with the body → `200`. 4. Wait 5 s. 5. Re-run steps 1 and 2. 6. `SELECT entity_type, action, property FROM audit.field_change WHERE entity_type='DocumentControlledCopy' ORDER BY occurred_at_utc DESC LIMIT 5;`. 7. Now close the copy as `Returned` and re-run step 1. |
| **Expected UI** | The register gains the row; the document's audit-trail panel shows **no** new entry for the issue. |
| **Expected API** | Step 3 → `200 {"id":…}`. |
| **Expected DB** | Step 5 → `max(sequence)` is still `:seq0` and `count(*)` on `qams.outbox_event` is still `:n0` — `DocumentControlledCopy.Issue` calls no `Raise(...)` (`DocumentControlledCopy.cs:40-65`), and `OutboxInterceptor.Drain` only enumerates aggregates with events (`Interceptors/OutboxInterceptor.cs:44-47`). |
| **Expected Audit** | Step 6 → exactly one row, `action='Created'`, `property IS NULL` — the field-change ledger is the **only** trace of the issue. Step 7 → `max(sequence)` increments, proving the pipeline works and the absence in step 5 is a missing event, not a broken processor. |
| **Expected Notification** | n/a — no rule references controlled copies. |
| **Cleanup** | `DELETE FROM qams.document_controlled_copy WHERE document_id=:docId AND holder='Bench 7';` |
| **Evidence** | Before/after sequence and count pairs · the single field-change row · the incremented sequence after closure |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 7 is the control that makes the finding defensible: without it a reviewer could attribute the absence to a stalled `OutboxProcessor`. |

#### TC-DOC-INT-034 — Downloading the published version's file streams the stored bytes as an attachment  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-046 (immutable stored files) · RSK-DOC-040 (minted) |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — retrieve the controlled copy of record |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Quality Manager · n/a — `GET /api/files/{id}` carries only `[Authorize]` (`src/NT.QAMS.WebApi/Controllers/FilesController.cs:59-72`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` + the configured `IFileStorage` root |
| **Preconditions** | `SOP-CAL-045` published at `1.0`; its `document_version.file_id` = `:fileId`; `SELECT file_name, content_type, sha256, size_bytes FROM qams.file_reference WHERE id=:fileId;` returns the row uploaded as `calibration-procedure.pdf`. |
| **Test Data** | `:fileId`; the local reference copy of `calibration-procedure.pdf` |
| **Steps** | 1. `GET /api/documents/{:docId}` and read `versions[].fileId` for the `Published` version. 2. `curl.exe -i -H "Authorization: Bearer <token>" http://localhost:5080/api/files/{:fileId} -o downloaded.pdf`. 3. Read the response headers. 4. `sha256sum downloaded.pdf` and compare with `qams.file_reference.sha256`. 5. Compare `Content-Length` with `qams.file_reference.size_bytes`. |
| **Expected UI** | The version table's download link opens the file in a new tab (`document-detail.component.ts:55`) — but see TC-DOC-INT-039 for what that link actually does in a browser. |
| **Expected API** | `200`; `Content-Type: application/pdf` — the **canonical** type resolved by `Security.FileContentPolicy.Inspect` at upload, never the client's claim (`FilesController.cs:41-42,51`); `Content-Disposition: attachment; filename=calibration-procedure.pdf` (forced by passing `fileDownloadName` to `File(...)`, `:69-71`). |
| **Expected DB** | Unchanged — `AsNoTracking` single-row read (`FilesController.cs:62`); `qams.file_reference` rows are never updated (`Domain/Files/FileReference.cs:6-11`). |
| **Expected Audit** | **None** — no `audit.field_change`, no `audit.audit_trail`, no `audit.security_event`. See TC-DOC-INT-035. |
| **Expected Notification** | n/a — no notification is defined for a download. |
| **Cleanup** | Delete `downloaded.pdf` from the working directory. |
| **Evidence** | Response headers · SHA-256 comparison · byte-size comparison |
| **Result / Defect** | Not Run · — |
| **Notes** | The SHA-256 equality is the Part 11 integrity anchor linking the signed version to the exact approved bytes; `ck_file_reference_sha256_sha256 CHECK (sha256 ~ '^[0-9a-f]{64}$')` guarantees the stored form is lower-case hex. |

#### TC-DOC-INT-035 — A file download produces no audit event of any kind  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · no URS requires a download event — implementation-derived; new gap **`GAP-DOC-901`** · RSK-DOC-041 (minted) |
| **Level / Type / Technique** | Integration (API + DB) · Compliance (observed shortfall, recorded as-is) · Data Flow — the absent define of any ledger write on the read path |
| **Priority / Severity / Automation** | High · Major · Yes (integration) |
| **Role / Permission / Tenant** | Quality Manager · none required · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `:fileId` resolves to the published version's file. Outbox drained. Read `audit.*` with `SELECT set_config('app.bypass_rls','on',false);`. |
| **Test Data** | `:fileId`; five repeated downloads |
| **Steps** | 1. Record baselines: `SELECT count(*) FROM audit.field_change;`, `SELECT max(sequence) FROM audit.audit_trail WHERE tenant_id=:tenantId;`, `SELECT count(*) FROM audit.security_event;`. 2. `GET /api/files/{:fileId}` five times with a valid token. 3. Wait 5 s. 4. Re-run all three baseline queries. 5. For contrast, call an export route that does log — `ExportsController` writes `RECORD_EXPORTED` via `ISecurityEventLog` (`src/NT.QAMS.WebApi/Controllers/ExportsController.cs:169`) — and re-check `audit.security_event`. |
| **Expected UI** | No UI change; the SPA shows nothing about who downloaded what. |
| **Expected API** | Five `200` responses. |
| **Expected DB** | Step 4 → all three counts/sequences **identical** to step 1. `FilesController.Download` performs one `AsNoTracking` read and calls no `SaveChanges`, no `ISecurityEventLog` and no `AuditTrailAppender` (`FilesController.cs:59-72`; the controller does not even take `ISecurityEventLog` as a dependency, `:17`). |
| **Expected Audit** | **No** `audit.security_event` row of any type for the retrieval; step 5 must add a `RECORD_EXPORTED` row, proving the security-event pipeline is live and the absence is by omission, not by breakage. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | Delete downloaded files. |
| **Evidence** | Baseline/after triplets · the contrasting `RECORD_EXPORTED` row |
| **Result / Defect** | Not Run · — |
| **Notes** | `GAP-DOC-901` (new, this batch): retrieval of a controlled document's bytes is unattributable. Acceptance criteria — (a) every successful `GET /api/files/{id}` for a file referenced by a `document_version` writes one `audit.security_event` row with a distinct event type, the actor, the tenant and the file id as the subject; (b) the row is written in the same request, before the stream is returned; (c) failed retrievals (404) are distinguishable from successful ones; (d) retrieval of a **superseded** version is separately identifiable, which is what `GAP-DOC-014`'s criterion (b) also asks for. |

#### TC-DOC-INT-036 — An account with no documents privileges can download an obsolete version's bytes  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025 read against the code; permissive behaviour — cite `GAP-DOC-014` · RSK-DOC-042 (minted) |
| **Level / Type / Technique** | API · Security (permissive behaviour, recorded as observed) · Decision Table — DT-3 rows 2 and 7 (front matter §4.3) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Custom tenant role with **zero** `documents.*` keys · none required on either route · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` (two tenants) |
| **Preconditions** | `SOP-CAL-045` is published at `1.1`; its `1.0` version is `state='Obsolete'` with `file_id = :obsoleteFileId`. `nodocs@demo-lab.local` exists as in TC-DOC-INT-013. A second tenant holds a file `:otherTenantFileId`. |
| **Test Data** | `nodocs@demo-lab.local`; `:obsoleteFileId`; `:otherTenantFileId` |
| **Steps** | 1. Sign in as `nodocs@demo-lab.local`. 2. `GET /api/documents/{:docId}` → assert the response lists **all** versions including the `Obsolete` `1.0` with its `fileId` (`Queries/DocumentQueries.cs:59-68`). 3. `GET /api/files/{:obsoleteFileId}` → record the status, `Content-Disposition` and the byte count. 4. `GET /api/files/{:otherTenantFileId}` → record the status. 5. Inspect the downloaded bytes for any obsolescence marking. |
| **Expected UI** | The version table renders a live download link on the obsolete row exactly as on the published one — no visual distinction (`document-detail.component.ts:50-57`). |
| **Expected API** | Step 2 → `200` with the obsolete version and its `fileId` exposed. Step 3 → `200` with the full bytes and `Content-Disposition: attachment`. Step 4 → `404` — the tenant filter plus `tenant_isolation` RLS (measured `rls=t force=t` on `file_reference`) hide the other tenant's row. |
| **Expected DB** | Unchanged. |
| **Expected Audit** | No rows — as established by TC-DOC-INT-035. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | Delete downloaded files. |
| **Evidence** | 200 capture on the obsolete file with the privileges screen showing no document access · 404 capture on the cross-tenant file |
| **Result / Defect** | Not Run · — |
| **Notes** | Steps 3 and 4 together are the honest finding: the tenant boundary holds, the **privilege and document-state** boundary does not. Report under `GAP-DOC-014` (Critical) together with `GAP-DOC-011`. |

#### TC-DOC-INT-037 — An anonymous download is refused with 401  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-046 · RSK-DOC-043 (minted) |
| **Level / Type / Technique** | API · Security (negative authentication) · Equivalence Partitioning — the unauthenticated partition of DT-3 row 1 |
| **Priority / Severity / Automation** | High · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Anonymous · n/a — authentication precedes authorization · n/a — no tenant context |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `:fileId` exists in `demo-lab`. `FallbackPolicy = RequireAuthenticatedUser` (front matter §4.3, `Program.cs:134-135`) and `FilesController` carries `[Authorize]` (`FilesController.cs:16`). |
| **Test Data** | `:fileId`; an expired bearer token; a malformed bearer token `Bearer abc.def.ghi` |
| **Steps** | 1. `curl.exe -i http://localhost:5080/api/files/{:fileId}` with **no** `Authorization` header. 2. Repeat with `Authorization: Bearer abc.def.ghi`. 3. Repeat with an expired token. 4. Assert no response body contains file bytes in any of the three. |
| **Expected UI** | The SPA's auth interceptor would attempt a silent refresh on 401 for an HttpClient call (`frontend/src/app/core/auth.interceptor.ts:19-33`); a raw `curl` has no such path. |
| **Expected API** | All three → `401`. Assert the body is `application/problem+json` (the framework 401/403 handler emits problem+json, conventions §2 / EA Phase 6) and that `Content-Type` is **not** the file's type. |
| **Expected DB** | Unchanged. |
| **Expected Audit** | No `audit.security_event` row — unlike a failed login, an unauthenticated API call writes nothing (only `Login`/`Refresh`/`Logout` paths call `ISecurityEventLog`, verified by the call-site list in `src/`). |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — no state written. |
| **Evidence** | Three `curl.exe -i` transcripts |
| **Result / Defect** | Not Run · — |
| **Notes** | Use `curl.exe`, not PowerShell's web cmdlets — PowerShell 5.1 drops manually supplied headers (conventions §3). |

#### TC-DOC-INT-038 — A file id from another tenant returns 404, not 403  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-046 · RSK-DOC-044 (minted) |
| **Level / Type / Technique** | Integration (API + DB) · Security (tenant isolation) · Equivalence Partitioning — the cross-tenant identifier partition |
| **Priority / Severity / Automation** | High · Critical · Yes (integration) |
| **Role / Permission / Tenant** | Quality Manager of `demo-lab` · none required on the route · `demo-lab`, probing a second tenant |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` with two provisioned tenants |
| **Preconditions** | Tenant B holds `qams.file_reference` row `:otherTenantFileId`. Measured: `file_reference` has `relrowsecurity = t`, `relforcerowsecurity = t`, policy `tenant_isolation`. |
| **Test Data** | `:otherTenantFileId` |
| **Steps** | 1. As a `demo-lab` user, `GET /api/files/{:otherTenantFileId}` → record status and whether a `code` member is present. 2. In psql with the **demo-lab** tenant GUC set: `SELECT count(*) FROM qams.file_reference WHERE id=:otherTenantFileId;`. 3. In psql with **tenant B's** GUC set: the same query. 4. `GET /api/documents/{:otherTenantDocId}` as the `demo-lab` user → record status and `code`. |
| **Expected UI** | n/a — the SPA never surfaces another tenant's ids. |
| **Expected API** | Step 1 → `404` from `NotFound()` (`FilesController.cs:64-66`) — a bare 404 with **no** domain `code` member, distinguishable from the acknowledge path's `DOC-404`. Step 4 → `404` with `code = "DOC-404"`. |
| **Expected DB** | Step 2 → `0`. Step 3 → `1`. The row exists; it is invisible under the wrong tenant GUC. |
| **Expected Audit** | No rows in either tenant. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — read-only. |
| **Evidence** | 404 captures · the two contrasting psql counts under different GUCs |
| **Result / Defect** | Not Run · — |
| **Notes** | 404-not-403 is the correct anti-enumeration answer and matches the module's other cross-tenant behaviour. This is the isolation proof this batch owns for the file surface; the full five-table RLS sweep belongs to the sibling batch. |

#### TC-DOC-INT-039 — The SPA's version-table download link carries no bearer token and is refused with 401  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · URS-025, URS-046 · new gap **`GAP-DOC-902`** · RSK-DOC-045 (minted) |
| **Level / Type / Technique** | E2E (browser) · Functional (negative, defect-revealing) · Path coverage — the anchor-navigation path, which bypasses the `HttpClient` interceptor chain |
| **Priority / Severity / Automation** | High · Major · Yes (Playwright) |
| **Role / Permission / Tenant** | Quality Manager · none required by the route · `demo-lab` |
| **Environment** | SPA `localhost:4200/t/demo-lab` (Chromium) + API `:5080` Development |
| **Preconditions** | Signed in as `admin@demo-lab.local` / `Demo-Admin-Pass-2!`. `SOP-CAL-045` published at `1.0`. Access JWT is SPA-memory-only and the refresh cookie `qams_rt` is scoped `Path=/api/auth` (conventions §2, ADR-0009). `downloadUrl` returns a plain absolute URL with no token (`frontend/src/app/core/api/files-api.service.ts:21-23`), rendered as `<a [href]=… target="_blank" rel="noopener">` (`document-detail.component.ts:55`). The `Authorization` header is attached only inside the HTTP interceptor (`frontend/src/app/core/auth.interceptor.ts:19,40-41`). |
| **Test Data** | The `1.0` row's download link |
| **Steps** | 1. Open the document detail page. 2. Open DevTools → Network and clear it. 3. Click the `1.0` row's download link. 4. Inspect the new tab's request: assert whether an `Authorization` header is present and whether a `qams_rt` cookie was sent to `/api/files/…`. 5. Record the response status. 6. Repeat in a fresh incognito profile after signing in, to rule out a cached response. |
| **Expected UI** | A new tab opens and shows the API's error response rather than the file — no download prompt, no PDF. |
| **Expected API** | `GET /api/files/{fileId}` with **no** `Authorization` header and **no** `qams_rt` cookie (path scope excludes `/api/files`) → `401`. The file is never delivered through this control. |
| **Expected DB** | Unchanged. |
| **Expected Audit** | None. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | Close the extra tab. |
| **Evidence** | Network-tab HAR showing the absent `Authorization` header and the 401 · screenshot of the resulting tab |
| **Result / Defect** | Not Run · — |
| **Notes** | `GAP-DOC-902` (new, this batch): the only download affordance the SPA offers cannot authenticate under ADR-0009's memory-only-token model. Acceptance criteria — (a) the version-table download fetches through `HttpClient` (so the bearer token and the change-reason/correlation interceptors apply) and hands the user a blob URL, **or** the API issues a short-lived single-use download token; (b) a signed-in user with the required privilege receives the bytes; (c) the affordance never places a credential in a URL query string; (d) the resulting retrieval is audited per `GAP-DOC-901`. Execute this case before assuming any download-path finding is exploitable in the SPA — TC-DOC-INT-036 uses `curl` with an explicit token and is unaffected. |

#### TC-DOC-INT-040 — An obsolete version's download carries an "OBSOLETE - UNCONTROLLED" marking  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | DOC · brief requirement with no URS and no implementation — **Gap-dependent on `GAP-DOC-011`** (and `GAP-DOC-014`) · RSK-DOC-046 (minted) |
| **Level / Type / Technique** | API · Functional (gap-dependent) · Decision Table — DT-3's "Watermark applied?" column, which reads **No** in every row |
| **Priority / Severity / Automation** | Critical · Critical · No — cannot be executed until `GAP-DOC-011` is resolved |
| **Role / Permission / Tenant** | Any authenticated tenant user · to be defined by the fix · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SOP-CAL-045` published at `1.1` with `1.0` in `state='Obsolete'` and `file_id = :obsoleteFileId`; the original uploaded PDF is available for byte comparison. |
| **Test Data** | `:obsoleteFileId`; the original `calibration-procedure.pdf` |
| **Steps** | 1. `GET /api/files/{:obsoleteFileId}` and save the response body. 2. `sha256sum` the saved file and compare with `qams.file_reference.sha256`. 3. Extract the PDF text/overlay layer and search case-insensitively for `OBSOLETE` and `UNCONTROLLED`. 4. Repeat steps 1-3 for the **published** `1.1` file and diff the outcomes. |
| **Expected UI** | **Current build:** the obsolete row's link is visually identical to the published row's. **Required after the gap is closed:** the SPA must distinguish a superseded version's retrieval affordance from the current one. |
| **Expected API** | **Current build:** `200` with bytes whose SHA-256 **equals** `file_reference.sha256` — proof that nothing post-processes the stream (`FilesController.cs:68-71`). **Acceptance criterion for the fix:** a superseded version retrieved for printing must be visibly marked as uncontrolled, and the marking must survive printing; the marked artefact must be distinguishable from the stored original (i.e. its hash must differ from `file_reference.sha256`, and the stored original must remain unmodified). |
| **Expected DB** | **Current build:** `qams.file_reference` is untouched by the download. **Acceptance criterion:** whatever mechanism is chosen must not mutate the content-addressed original — `FileReference` rows are never updated by design (`Domain/Files/FileReference.cs:6-11`). |
| **Expected Audit** | **Current build:** none. **Acceptance criterion:** retrieval of a marked superseded copy must be audited as a distinct act (converges with `GAP-DOC-901` and `GAP-DOC-014` criterion (b)). |
| **Expected Notification** | n/a — no notification is proposed for this control. |
| **Cleanup** | Delete downloaded artefacts. |
| **Evidence** | SHA-256 equality proof · text-extraction transcript showing zero matches for `OBSOLETE`/`UNCONTROLLED` |
| **Result / Defect** | Not Run · — |
| **Notes** | The exhaustive search recorded in the front matter (§4.3) found **no** watermark implementation anywhere in the repository — only a doc-comment phrase. Steps 1-3 are executable **today** as evidence of absence; the "marking present" expectation is not, and must not be authored as a passing case. |

---

## Batch coverage note

**Covered (40 cases, `TC-DOC-INT-001` … `TC-DOC-INT-040`).** Acknowledgements: version pinning, handler idempotency and its no-write consequence, revision re-opening with receipt retention, the `ACK-010` refusals from both the never-published and retired sides, `DOC-404`, the `[RequireInternalActor]` / `AUTHZ-002` refusal for `ExternalAuditor` with its query-side asymmetry, all three branches of `my-acknowledgement`, the coverage view's ordering and DTO shape, its `documents.view` / `AUTHZ-403` gate contrasted against the ungated siblings, the `(unknown)` display fallback, the `ux_doc_ack_tenant_document_version_user` constraint at the SQL layer, and the full event → outbox → `audit.audit_trail` chain. Controlled copies: numbered issue pinned to the published label, `lastCopyNumber + 1` including number non-reuse after destruction, `CCP-020`, `CCP-001` and the `varchar(200)` holder boundary with trimming, the `documents.edit` gate on both write routes, register ordering and DTO shape, the ungated register read, `Close(Returned)` and `Close(Destroyed)` with their field-change and ledger consequences, `CCP-010` one-shot immutability from both terminal states, the load-bearing `CCP-003`-before-`CCP-010` guard order, the accepted and refused outcome-parsing partitions, `CCP-404` including the cross-tenant probe, and the two gap-anchored cases (`GAP-DOC-005`, `GAP-DOC-006`). Download/preview: successful retrieval with hash and canonical-content-type assertions, the complete absence of audit on the read path, unprivileged retrieval of obsolete bytes, anonymous 401, cross-tenant 404, the SPA link's missing bearer token, and the watermark expectation as `[GD]`.

**In my slice but not covered, with the reason.**
1. **Holder over-boundary (201 characters).** Not authored as an executable case: no validator bounds `IssueControlledCopyCommand.Holder` and the column is `varchar(200)`, so the outcome would be an Npgsql `22001` with no handler in the pipeline. Raised as `GAP-DOC-903` below rather than fabricated as a 400.
2. **Concurrent controlled-copy issuance.** Owned by `GAP-DOC-018` and charted as `TC-DOC-EXPL-001`…`006`'s `TC-DOC-EXPL-003`; a deterministic case cannot be written against an unguarded read-then-write whose failure mode is an unhandled 500.
3. **`Enum.TryParse` behaviour for out-of-range numerics (`"3"`) and trailing whitespace (`"Returned "`).** The throw sites are read; the BCL parser's exact tolerance was **not** read in this pass, so TC-DOC-INT-030 steps 2 and 6 instruct the executor to record the observed result rather than assert a predicted one.
4. **`ClosedBy` disclosure.** `document_controlled_copy.closed_by` is populated but absent from `ControlledCopyDto`, so no case can assert who closed a copy through the API; TC-DOC-INT-023's notes record the limitation. Whether the register must disclose the closer is an open requirement question, not a defect I can assert.
5. **Preview (in-browser rendering).** There is no preview surface: `GET /api/files/{id}` forces `Content-Disposition: attachment` (`FilesController.cs:69-71`) and the SPA offers only a download link. "Preview" in the assignment therefore maps onto the download path throughout; no separate preview endpoint, route or component exists in the repository.
6. **The five-table RLS sweep and the file-upload allow-list / magic-byte cases** named in the front matter's batch-D reservation. Deliberately not written here — my assigned slice replaced them — leaving them unowned. See `GAP-DOC-900`.

**New gaps found in this batch (numbered `9xx` to avoid colliding with the front matter's sequence).**

- **`GAP-DOC-900` — the batch-D scope reservation and the batch-D assignment disagree.** *Severity: Moderate (traceability).* The front matter's ID-reservation table assigns `13-module-document-control-cases-D.md` the scope "controlled-copy register, file upload/download, content sniffing, tenant isolation and RLS on the five document tables" with ranges `TC-DOC-RLS-001…015`, `TC-DOC-DF-001…010`, `TC-DOC-API-061…085`, while the authoring assignment for this file gave the acknowledgement + controlled-copy + download slice and the reserved block `TC-DOC-INT-001…`. That block is reserved to **batch C** in the same table. This file follows its assignment and consumes `TC-DOC-INT-001…040`; batch C must therefore be re-based (suggested: `TC-DOC-INT-041…070`) or the reservation table amended before the traceability matrix is built. Two consequences must be resolved by the package owner, not silently: (a) `TC-DOC-INT-001…040` now exist in this file and must not be minted elsewhere; (b) the RLS / upload-sniffing / `TC-DOC-API-061…085` scope is currently **unowned** by any case file and is a coverage hole per conventions §7.
- **`GAP-DOC-901` — retrieval of a controlled document's bytes is unaudited and unattributable.** *Severity: Major.* `FilesController.Download` (`src/NT.QAMS.WebApi/Controllers/FilesController.cs:59-72`) performs a single `AsNoTracking` read and calls no `SaveChanges`, no `ISecurityEventLog` and no `AuditTrailAppender`; the controller does not even inject `ISecurityEventLog` (`:17`), in contrast to `ExportsController`, which writes `RECORD_EXPORTED` (`src/NT.QAMS.WebApi/Controllers/ExportsController.cs:169`). No record therefore exists of who retrieved a controlled procedure, when, or which version. Acceptance criteria are stated in full in TC-DOC-INT-035's Notes. Responsible role: Security Lead / Backend Lead.
- **`GAP-DOC-902` — the SPA's only download affordance cannot authenticate.** *Severity: Major.* `FilesApiService.downloadUrl` returns a plain URL (`frontend/src/app/core/api/files-api.service.ts:21-23`) rendered as an `<a href target="_blank">` (`frontend/src/app/features/documents/document-detail.component.ts:55`). Under ADR-0009 the access JWT lives only in SPA memory and is attached solely by the `HttpClient` interceptor (`frontend/src/app/core/auth.interceptor.ts:19,40-41`); the refresh cookie `qams_rt` is scoped `Path=/api/auth`. An anchor navigation to `/api/files/{id}` therefore carries no credential and meets `[Authorize]`. Acceptance criteria in TC-DOC-INT-039's Notes. Note the interaction: this weakens the practical exploitability of `GAP-DOC-014` **through the SPA** while leaving the API exposure untouched — both findings must be reported together so neither is over- nor under-stated. Responsible role: Frontend Lead / Security Lead.
- **`GAP-DOC-903` — `holder` has a column bound but no validator.** *Severity: Minor.* `document_controlled_copy.holder` is `varchar(200)` (measured; `20260726214512_DocumentControlledCopy.cs:25`) and `IssueControlledCopyCommand` has no FluentValidation validator at all — verified by absence in `src/NT.QAMS.Application/DocumentControl/ControlledCopySlice.cs`. This breaches the repo's own convention that a varchar bound must be matched by a validator rule (CLAUDE.md §5, "Column sizing"). A 201-character holder would reach PostgreSQL and raise `22001`, which no pipeline handler translates. Acceptance criteria: (a) a validator rejects a holder longer than 200 characters with `400` and a field-level error naming the limit; (b) a blank holder continues to return `422 CCP-001`; (c) exactly 200 characters is accepted and stored verbatim after trimming. Responsible role: Backend Lead.
