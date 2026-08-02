# AUTH — Detailed Test Cases, Batch E

This batch authors the **electronic-signature ceremony** end to end and the **user-access-review record**, and nothing else. In scope: the password+PIN signing ceremony reachable only through `POST /api/documents/{id}/publish` (`Application/DocumentControl/Commands/DocumentCommands.cs:154` is the sole `IESignatureService.SignAsync` call site) — its happy path and the eight persisted `SignatureRecord` fields, `SIG-002` wrong password, `SIG-001` wrong-or-unset PIN, `SIG-003` signing lockout and the 5-attempt / 30-minute counter it shares with login, `SIG-404`, the `ESIGN_FAILED` / `ESIGN_LOCKED` security-event emissions, the 10/min per-actor e-signature rate partition, replay via `Idempotency-Key`, two concurrent signings of one document, client-side and mid-flight cancellation, direct-API attempts to skip or bypass the ceremony, the assertion that the PIN never reaches a response body, a response header, the canonical request log or the `audit.field_change` ledger, and append-only enforcement on `audit.electronic_signature`; plus `UserAccessReview` open, complete, re-complete, blank/oversized conclusion, the completion-instant account count, and cross-tenant isolation of the completion handler. **Deliberately left to sibling batches:** all `SIG-010` / `SIG-011` cases (batch **B** / module `MV` — §0.2 of the front matter proves those two codes belong to `SigmaAssessment`, not to e-signature, so no e-signature case may claim them); the `access-reviews.view`-gates-a-write authorization matrix and the `[RequireInternalActor]` refusal of `ExternalAuditor` (batch **D**, `TC-AUTH-SEC-001…029`); PIN **format** boundary cases against `POST /api/auth/signature-pin` (batch **A**, `TC-AUTH-BVA-*`); login-side lockout, refresh, MFA and password policy (batches **A**–**D**); and the browser signing flow as a UI journey (batch **F**, `TC-AUTH-E2E-*`).

**ID block consumed:** `TC-AUTH-SEC-030` … `TC-AUTH-SEC-040` (11) and `TC-AUTH-INT-001` … `TC-AUTH-INT-021` (21) — **32 cases**. Requirement IDs trace to `docs/validation/01-User-Requirements-Specification.md`. `docs/validation/02-Functional-Risk-Assessment.md` carries **no numbered risk IDs** (it is an area-level table), so risk IDs below are **minted** as `RSK-AUTH-0NN` and are new to this package.

**Shared fixture, referenced by every case as `FIX-ESIG`.** Tenant `demo-lab`. Two tenant users created through `POST /api/users` by `admin@demo-lab.local`: `author@demo-lab.local` (display name `Ada Author`) and `approver@demo-lab.local` (display name `Ben Approver`, password `Approver-Pass-2026!`, signature PIN `4417` set by `POST /api/auth/signature-pin` while signed in as that user). `Ben Approver` holds a tenant role granting `documents.sign` and `documents.approve`. One controlled document `SOP-0001` authored by `Ada Author`, one uploaded file, its in-flight version at `VersionState.Approved` with `version_label = '1.0'` — the state `PublishDocumentHandler` pre-validates at `DocumentCommands.cs:136-146` before any signature is minted. `<DOCID>` denotes the document id and `<DOCID_N>` its `:N`-format (32 hex, no dashes) rendering, so `subject_ref = 'DOC:<DOCID_N>'`.

---

#### TC-AUTH-INT-001 — Valid password + PIN mints exactly one signature with all eight fields populated  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-020, URS-021, URS-024, URS-026 · RSK-AUTH-010 (minted) |
| **Level / Type / Technique** | Integration (API → PostgreSQL) · Functional (positive) · Use Case — the complete Part-11 signing ceremony, decision table §4.3 rule R10 |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional, `WebApi.FunctionalTests`) |
| **Role / Permission / Tenant** | `Ben Approver` (tier `QualityManager`) · `documents.sign` — endpoint filter `DocumentsController.cs:115` and command policy `DocumentCommands.cs:65-66` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `FIX-ESIG`. `SELECT pin_hash IS NOT NULL, failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='approver@demo-lab.local'` returns `t, 0, NULL`. `SELECT count(*) FROM audit.electronic_signature WHERE subject_ref='DOC:<DOCID_N>'` returns `0`. |
| **Test Data** | `POST /api/documents/<DOCID>/publish` body `{"password":"Approver-Pass-2026!","pin":"4417"}`, bearer token of `approver@demo-lab.local`, no `Idempotency-Key` header |
| **Steps** | 1. Sign in as `approver@demo-lab.local` and keep the access token. 2. `POST /api/documents/<DOCID>/publish` with the body above. 3. Read the status line and body length. 4. `SELECT set_config('app.bypass_rls','on',false);` then `SELECT id, tenant_id, signer_id, signer_display, meaning, subject_ref, content_hash, signed_at_utc FROM audit.electronic_signature WHERE subject_ref='DOC:<DOCID_N>';`. 5. `SELECT status FROM qams.controlled_document WHERE id='<DOCID>';`. 6. `GET /api/documents/<DOCID>/signatures`. |
| **Expected UI** | The publish form disappears and the document header shows status `Published`; the signature manifest panel lists one entry reading `Ben Approver — Approved and published SOP-0001 v1.0`. |
| **Expected API** | Step 2 → `204 No Content`, zero-length body. Step 6 → `200` with a one-element JSON array whose keys are exactly `id, tenantId, signerId, signerDisplay, meaning, subjectRef, contentHash, signedAtUtc`. |
| **Expected DB** | Exactly **one** row in `audit.electronic_signature`: `tenant_id` = the `demo-lab` tenant uuid (`ComplianceLedgerServices.cs:120` — `tenant.TenantId ?? Guid.Empty`); `signer_id` = the `sub` claim uuid of `approver@demo-lab.local`; `signer_display` = `'Ben Approver'` (the **display name**, `:122`, not the email); `meaning` = `'Approved and published SOP-0001 v1.0'` (`DocumentCommands.cs:156`); `subject_ref` = `'DOC:<DOCID_N>'` (`:157`); `content_hash` = the 64-character lower-case hex in `qams.file_reference.sha256` for the approved version's file; `signed_at_utc` non-null. `qams.controlled_document.status = 'Published'`. |
| **Expected Audit** | No `audit.security_event` row is written on a successful signing (`ComplianceLedgerServices.cs:117-130` has no `security.WriteAsync` on the success path) — assert `count(*)=0` for `event_type IN ('ESIGN_FAILED','ESIGN_LOCKED')` since step 1. One `audit.audit_trail` entry for the `DocumentPublished` event arrives via the outbox within one `OutboxProcessor` cycle. |
| **Expected Notification** | n/a — no notification policy subscribes to a signature record; `DocumentPublished` notification behaviour belongs to module `DOC`. |
| **Cleanup** | `audit.electronic_signature` rows **cannot** be removed — trigger `signature_append_only` (`20260721232300_ComplianceAndAuth.cs:162`) raises `audit ledgers are append-only`. Restore the dev database from the restore-point dump, or re-run against a fresh `SOP-####`. |
| **Evidence** | HTTP response capture (steps 2, 6) · psql output of step 4 with all eight columns · `controlled_document.status` row |
| **Result / Defect** | Not Run · — |
| **Notes** | The DB table is `audit.electronic_signature`, **not** `signature_record` — `SignatureRecordConfiguration.ToTable("electronic_signature","audit")`, `ComplianceConfigurations.cs:33`. `GET /api/documents/{id}/signatures` serialises the domain entity `SignatureRecord` directly (`DocumentsController.cs:47`), not a Contracts DTO; pin the eight keys so a future DTO refactor is caught. |

---

#### TC-AUTH-INT-002 — `content_hash` binds the exact approved file and satisfies the lower-case-hex CHECK  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-021 · RSK-AUTH-012 (minted) |
| **Level / Type / Technique** | Integration · Functional (positive) · Data Flow — file bytes → SHA-256 → `file_reference.sha256` → `electronic_signature.content_hash` |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `FIX-ESIG` with the approved version's file uploaded through `POST /api/documents/files`; the local file-storage root holds the object at `{tenant:N}/{sha}` (`LocalFileStorage.cs:47`). The publish of `TC-AUTH-INT-001` has been executed. |
| **Test Data** | Known file content: the 11 ASCII bytes `hello world` → SHA-256 `b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9` |
| **Steps** | 1. Compute the SHA-256 of the uploaded file locally: `certutil -hashfile <path> SHA256`. 2. `SELECT sha256 FROM qams.file_reference WHERE id=(SELECT file_id FROM qams.document_version WHERE document_id='<DOCID>' AND state='Published');`. 3. `SELECT content_hash FROM audit.electronic_signature WHERE subject_ref='DOC:<DOCID_N>';`. 4. Compare all three, case-sensitively. 5. Attempt `INSERT INTO audit.electronic_signature (id,tenant_id,signer_id,signer_display,meaning,subject_ref,content_hash,signed_at_utc) VALUES (gen_random_uuid(),'<TENANT>','<SIGNER>','X','X','DOC:x','B94D27B9934D3E08A52E52D7DA7DABFAC484EFE37A5380EE9088F7ACE2EFCDE9', now());` after `set_config('app.bypass_rls','on',false)`. |
| **Expected UI** | n/a — no screen displays the content hash; the signature panel shows signer, meaning and timestamp only. |
| **Expected API** | n/a — this case asserts persisted state; the HTTP call was made by `TC-AUTH-INT-001`. |
| **Expected DB** | Steps 1–4: all three values are byte-identical, 64 characters, **lower-case** hex (`Convert.ToHexStringLower`, `LocalFileStorage.cs:44`). Step 5 fails with `ERROR: new row for relation "electronic_signature" violates check constraint "ck_electronic_signature_content_hash_sha256"` — the constraint is `content_hash ~ '^[0-9a-f]{64}$'` (measured on `ntqams`, 2026-08-01). |
| **Expected Audit** | n/a — a rejected INSERT writes no ledger row; the failed statement appears only in the PostgreSQL server log. |
| **Expected Notification** | n/a — no notification is defined for signature persistence. |
| **Cleanup** | n/a — step 5 is rejected before insert, and steps 1–4 are read-only. |
| **Evidence** | `certutil` output · two psql column values · the CHECK-violation error text |
| **Result / Defect** | Not Run · — |
| **Notes** | The hash is read from `qams.file_reference` **before** signing (`DocumentCommands.cs:149-150`), so it is the hash of the bytes as stored, not recomputed from disk at signing time. A storage-layer corruption after upload would not be detected by this ceremony — out of scope here, raise against module `DOC` if wanted. |

---

#### TC-AUTH-INT-003 — Signature `meaning` and `subject_ref` are the exact composed strings  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-024 · RSK-AUTH-012 (minted) |
| **Level / Type / Technique** | Integration · Functional (positive) · Equivalence Partitioning — one representative per composed-string partition (code, version label, id format) |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Three approved documents in `demo-lab`, each authored by `Ada Author`: `SOP-0001` v`1.0`, `SOP-0002` v`2.3` (a minor bump), `WI-0007` v`10.0` (a two-digit major). |
| **Test Data** | Three publishes, each `{"password":"Approver-Pass-2026!","pin":"4417"}` |
| **Steps** | 1. Publish all three documents as `Ben Approver`. 2. `SELECT meaning, subject_ref, length(subject_ref) FROM audit.electronic_signature ORDER BY signed_at_utc DESC LIMIT 3;` under `set_config('app.bypass_rls','on',false)`. 3. Compare each `subject_ref` with the document id rendered `:N`. |
| **Expected UI** | The signature manifest on each document shows the meaning verbatim, e.g. `Approved and published WI-0007 v10.0`. |
| **Expected API** | Each publish → `204 No Content`. |
| **Expected DB** | `meaning` values exactly `'Approved and published SOP-0001 v1.0'`, `'Approved and published SOP-0002 v2.3'`, `'Approved and published WI-0007 v10.0'` — the interpolation at `DocumentCommands.cs:156` with no trailing period. `subject_ref` values exactly `'DOC:' || replace(id::text,'-','')`, each **36 characters** (`4 + 32`), well inside `varchar(120)`. |
| **Expected Audit** | No security event on any of the three success paths. Three `audit.audit_trail` entries for `DocumentPublished`. |
| **Expected Notification** | n/a — signature persistence raises no notification. |
| **Cleanup** | n/a — ledger rows are append-only by design; use fresh document codes on re-runs. |
| **Evidence** | psql result of step 2 · the three document ids |
| **Result / Defect** | Not Run · — |
| **Notes** | `meaning` is `varchar(500)` (`ComplianceConfigurations.cs:36`); a document code plus label can never approach it, so no BVA case is warranted here. The `Sign` permission action exists on 20+ catalogue modules but only `documents.sign` reaches a ceremony (`GAP-AUTH-003`) — do not generalise this meaning string to other modules. |

---

#### TC-AUTH-INT-004 — Wrong password is refused `SIG-002`, logs `ESIGN_FAILED`, and advances the shared lockout counter  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-020, URS-023, URS-016 · RSK-AUTH-013 (minted) |
| **Level / Type / Technique** | Integration · Functional (negative) · Decision Table — §4.3 rule R7 |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `FIX-ESIG` with a **fresh** approved document `SOP-0003` v`1.0`. `failed_login_attempts = 0`, `locked_until_utc IS NULL` for `approver@demo-lab.local`. |
| **Test Data** | `{"password":"Approver-Pass-2026?","pin":"4417"}` — the correct PIN with a one-character-different password |
| **Steps** | 1. `POST /api/documents/<DOC3>/publish` with the body above. 2. Read status, `content-type` and the `code` extension. 3. `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='approver@demo-lab.local';`. 4. `SELECT event_type, actor, detail, tenant_id, ip_address FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 1;` under bypass. 5. `SELECT count(*) FROM audit.electronic_signature WHERE subject_ref='DOC:<DOC3_N>';`. 6. `SELECT status FROM qams.controlled_document WHERE id='<DOC3>';`. |
| **Expected UI** | The publish form stays open with both fields cleared of focus errors; a problem banner reads `Account password is incorrect.`; the document status stays `Draft`/in-flight `Approved`. |
| **Expected API** | `422 Unprocessable Entity`, `content-type: application/problem+json`, `title` = `Account password is incorrect.`, extension `code` = `SIG-002` (`ComplianceLedgerServices.cs:108`; mapping `DomainExceptionHandler.cs:73-78`). |
| **Expected DB** | `failed_login_attempts` = `1` (was 0) — `RegisterFailedLogin` at `ComplianceLedgerServices.cs:141`, persisted by `SecurityEventLog.WriteAsync`'s own `SaveChangesAsync` (`:82`). `locked_until_utc` still `NULL`. **Zero** rows in `audit.electronic_signature` for `DOC:<DOC3_N>`. `controlled_document.status` unchanged. |
| **Expected Audit** | One `audit.security_event` row: `event_type='ESIGN_FAILED'`, `actor='Ben Approver'` (**display name**, `:142` — contrast the login paths, which write the email), `detail='bad-password:DOC:<DOC3_N>'`, `tenant_id` = the `demo-lab` uuid, `ip_address IS NULL` (`GAP-AUTH-005`). Plus one `audit.field_change` row for `UserAccount` property `FailedLoginAttempts`, `old_value='0'`, `new_value='1'`. |
| **Expected Notification** | n/a — no notification policy subscribes to `ESIGN_FAILED`. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='approver@demo-lab.local';` (run under `set_config('app.bypass_rls','on',false)` is not needed — `qams.user_account` has no RLS, measured). |
| **Evidence** | HTTP problem+json capture · psql counter row · security-event row · signature-count query |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert **endpoint + code together**: `SIG-002` also means "allowable total error must be positive" in `SigmaAssessment.cs:77` (`GAP-AUTH-011`). The counter increment is committed by `WriteAsync` even though the handler then throws — the security event and the increment share one `SaveChanges`, so a rollback of the thrown request does not undo them (there is no ambient transaction; grep for `BeginTransaction` in `src/NT.QAMS.Application` and `src/NT.QAMS.WebApi` returns nothing). |

---

#### TC-AUTH-INT-005 — Wrong PIN with a correct password is refused `SIG-001` and logged `bad-pin`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-020, URS-023 · RSK-AUTH-011 (minted) |
| **Level / Type / Technique** | Integration · Functional (negative) · Decision Table — §4.3 rule R9 |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fresh approved document `SOP-0004` v`1.0`. `pin_hash` set from PIN `4417`. `failed_login_attempts = 0`, `locked_until_utc IS NULL`. |
| **Test Data** | `{"password":"Approver-Pass-2026!","pin":"4418"}` — correct password, PIN off by one digit |
| **Steps** | 1. `POST /api/documents/<DOC4>/publish` with the body above. 2. Read status and the `code` extension. 3. `SELECT failed_login_attempts FROM qams.user_account WHERE email='approver@demo-lab.local';`. 4. `SELECT event_type, actor, detail FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 1;` under bypass. 5. `SELECT count(*) FROM audit.electronic_signature WHERE subject_ref='DOC:<DOC4_N>';`. |
| **Expected UI** | Problem banner reads `Electronic-signature PIN is not set or is incorrect.`; the PIN input keeps focus; document status unchanged. |
| **Expected API** | `422`, `application/problem+json`, `title` = `Electronic-signature PIN is not set or is incorrect.`, `code` = `SIG-001` (`ComplianceLedgerServices.cs:114`). |
| **Expected DB** | `failed_login_attempts` = `1`. `locked_until_utc` `NULL`. Zero signature rows for `DOC:<DOC4_N>`. |
| **Expected Audit** | One `audit.security_event`: `event_type='ESIGN_FAILED'`, `actor='Ben Approver'`, `detail='bad-pin:DOC:<DOC4_N>'`, `ip_address IS NULL`. One `audit.field_change` row for `FailedLoginAttempts` `0 → 1`. |
| **Expected Notification** | n/a — no notification policy subscribes to `ESIGN_FAILED`. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='approver@demo-lab.local';` |
| **Evidence** | HTTP problem+json capture · counter row · security-event row |
| **Result / Defect** | Not Run · — |
| **Notes** | The PIN is verified with the same PBKDF2 hasher as the password (`MfaAndPin.cs:79-80`), so a wrong-PIN attempt costs a full key-derivation — relevant to `TC-AUTH-SEC-033`'s throttle reasoning. |

---

#### TC-AUTH-INT-006 — An unset PIN is indistinguishable from a wrong PIN — both `SIG-001`, both `bad-pin`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-020 · RSK-AUTH-011 (minted) |
| **Level / Type / Technique** | Integration · Functional (negative) · Equivalence Partitioning — the deliberate merge of two partitions (`PinHash IS NULL` and `PinHash` mismatch) into one output class |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | `Cara Signer` (`signer2@demo-lab.local`, tier `QualityManager`, **no PIN ever set**) · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `signer2@demo-lab.local` exists with password `Second-Signer-2026!`, holds `documents.sign`, and `SELECT pin_hash FROM qams.user_account WHERE email='signer2@demo-lab.local'` returns `NULL`. Fresh approved document `SOP-0005` v`1.0` authored by `Ada Author`. |
| **Test Data** | `{"password":"Second-Signer-2026!","pin":"0000"}` |
| **Steps** | 1. Sign in as `signer2@demo-lab.local`. 2. `POST /api/documents/<DOC5>/publish` with the body above. 3. Capture status, `title`, `code`. 4. Diff this response byte-for-byte against the `TC-AUTH-INT-005` response (ignoring `traceId`/`X-Correlation-Id`). 5. `SELECT detail FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 1;` under bypass. |
| **Expected UI** | Identical banner text to the wrong-PIN case: `Electronic-signature PIN is not set or is incorrect.` — the SPA offers no "you have not set a PIN" hint. |
| **Expected API** | `422`, `code` = `SIG-001`, `title` = `Electronic-signature PIN is not set or is incorrect.` — **identical** to `TC-AUTH-INT-005` apart from `traceId`. The short-circuit at `ComplianceLedgerServices.cs:111` (`string.IsNullOrWhiteSpace(signer.PinHash) || !hasher.Verify(...)`) makes both conditions reach the same throw at `:114`. |
| **Expected DB** | `failed_login_attempts` for `signer2@demo-lab.local` = `1`. Zero signature rows for `DOC:<DOC5_N>`. |
| **Expected Audit** | `audit.security_event` row `ESIGN_FAILED` with `detail='bad-pin:DOC:<DOC5_N>'` and `actor='Cara Signer'` — the same `reason` token as a wrong PIN, so the ledger also cannot distinguish the two. |
| **Expected Notification** | n/a — no notification policy subscribes to `ESIGN_FAILED`. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0 WHERE email='signer2@demo-lab.local';` |
| **Evidence** | Two HTTP captures and their diff · security-event `detail` values |
| **Result / Defect** | Not Run · — |
| **Notes** | This indistinguishability is **intentional single-branch design**, not a defect — record it as the as-built contract so a future "PIN not configured" message is caught as a behaviour change. Because the short-circuit `||` never evaluates `hasher.Verify` when `PinHash` is null, this case is also the false-arm of the multiple-condition coverage that `TC-AUTH-INT-007` completes. |

---

#### TC-AUTH-INT-007 — Wrong password **and** wrong PIN yields `SIG-002`: the password is checked first  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-020 · RSK-AUTH-010 (minted) |
| **Level / Type / Technique** | Integration · Functional (negative) · Multiple-Condition / MC-DC over the two guards at `ComplianceLedgerServices.cs:105` and `:111` — the (F,F) combination proves ordering, and with `TC-AUTH-INT-004` / `-005` / `-001` all four condition combinations are covered |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fresh approved document `SOP-0006` v`1.0`. `failed_login_attempts = 0`. |
| **Test Data** | `{"password":"Approver-Pass-2026?","pin":"9999"}` — both components wrong |
| **Steps** | 1. `POST /api/documents/<DOC6>/publish` with the body above. 2. Read `code`. 3. `SELECT failed_login_attempts FROM qams.user_account WHERE email='approver@demo-lab.local';`. 4. `SELECT detail FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 2;` under bypass. |
| **Expected API** | `422`, `code` = **`SIG-002`** (not `SIG-001`) — the password guard at `:105` returns before the PIN guard at `:111` is reached. |
| **Expected UI** | Banner reads `Account password is incorrect.` only; no PIN-specific message is shown even though the PIN is also wrong. |
| **Expected DB** | `failed_login_attempts` = `1` — **one** increment, not two: `RecordFailureAsync` runs once per request (`:107`). |
| **Expected Audit** | Exactly **one** new `audit.security_event` row, `detail='bad-password:DOC:<DOC6_N>'`. The second row returned by the `LIMIT 2` query belongs to an earlier case — assert its `occurred_at_utc` predates step 1. |
| **Expected Notification** | n/a — no notification policy subscribes to `ESIGN_FAILED`. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0 WHERE email='approver@demo-lab.local';` |
| **Evidence** | HTTP capture with `code` · counter row · the two most recent security events with timestamps |
| **Result / Defect** | Not Run · — |
| **Notes** | Ordering matters to a Part-11 assessor: a caller who guesses the password learns from the code switching `SIG-002 → SIG-001` that the password is now correct. That is an oracle, bounded by the 10/min per-actor budget and the 5-attempt lockout; record it here rather than restating it as a separate finding. |

---

#### TC-AUTH-INT-008 — A locked signer is refused `SIG-003`, logged `ESIGN_LOCKED`, and the counter is **not** advanced  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003, URS-023 · RSK-AUTH-014 (minted) |
| **Level / Type / Technique** | Integration · Functional (negative) · Decision Table — §4.3 rule R6, with State Transition from `S2 Active-Locked` |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | The signer is in `S2 Active-Locked`. Per front-matter §0.1 that state reads `failed_login_attempts = 0 AND locked_until_utc > now()` — the counter is **zeroed at lockout** (`UserAccount.cs:215`). Set it deterministically: `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc = now() + interval '30 minutes' WHERE email='approver@demo-lab.local';`. Fresh approved document `SOP-0007` v`1.0`. The signer already holds a valid access token issued **before** the lock (the lock does not invalidate a token — `ActiveSessionMiddleware` checks `IsActive` and `role`, not the lock, `RequestIdentity.cs:93-107`). |
| **Test Data** | `{"password":"Approver-Pass-2026!","pin":"4417"}` — **both components correct** |
| **Steps** | 1. `POST /api/documents/<DOC7>/publish` with the correct credentials. 2. Read status and `code`. 3. `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='approver@demo-lab.local';` and compare `locked_until_utc` with the precondition value. 4. `SELECT event_type, actor, detail FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 1;` under bypass. 5. `SELECT count(*) FROM audit.electronic_signature WHERE subject_ref='DOC:<DOC7_N>';`. |
| **Expected UI** | Banner reads `Account is temporarily locked after repeated failed signings.`; the form remains enabled (the SPA does not disable it), so the user can retry and be refused again. |
| **Expected API** | `422`, `application/problem+json`, `code` = `SIG-003`, `title` = `Account is temporarily locked after repeated failed signings.` (`ComplianceLedgerServices.cs:101`). Note the status is **422, not 401 or 429** — `SIG-` is neither an `AUTH-` nor an `AUTHZ-` prefix, so it falls through to the default `DomainException` arm (`DomainExceptionHandler.cs:73-78`). |
| **Expected DB** | `failed_login_attempts` still `0` and `locked_until_utc` **byte-identical to the precondition value** — the guard at `:98` returns before `RecordFailureAsync`, so there is **no lock extension**. Zero signature rows for `DOC:<DOC7_N>`; document status unchanged. |
| **Expected Audit** | One `audit.security_event`: `event_type='ESIGN_LOCKED'`, `actor='Ben Approver'`, `detail='DOC:<DOC7_N>'` — the **bare subject ref with no `reason:` prefix**, unlike `ESIGN_FAILED` (`:100` passes `subjectRef` directly, `:142` passes `$"{reason}:{subjectRef}"`). `ip_address IS NULL`. **No** `audit.field_change` row, because nothing on `UserAccount` changed. |
| **Expected Notification** | n/a — no notification policy subscribes to `ESIGN_LOCKED`. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='approver@demo-lab.local';` |
| **Evidence** | HTTP capture · before/after `locked_until_utc` values · `ESIGN_LOCKED` row showing the prefix-free `detail` |
| **Result / Defect** | Not Run · — |
| **Notes** | Correct credentials are used deliberately: the case proves the lock is checked **before** either credential, so a locked account cannot even confirm its own password. The differing `detail` shape between `ESIGN_LOCKED` and `ESIGN_FAILED` is a parsing trap for any alerting rule — state it in the RTM. |

---

#### TC-AUTH-INT-009 — `SIG-404` for an unknown signer is reachable only below HTTP  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · no URS covers a missing-signer guard — trace to `ComplianceLedgerServices.cs:93-94`; open `GAP-AUTH-903` for the missing requirement · RSK-AUTH-010 (minted) |
| **Level / Type / Technique** | Integration (service-level, `IntegrationTests` host with a real `AppDbContext`) · Structural · Statement/Branch coverage of the `SingleOrDefaultAsync ?? throw` at `:93-94`, which no HTTP request can reach |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (integration test) |
| **Role / Permission / Tenant** | n/a — the service is invoked directly, outside the HTTP authorization pipeline · n/a — no `[RequirePermission]` applies below MVC · `demo-lab` set via `ICurrentTenantSetter` |
| **Environment** | `QMS_ITEST_POSTGRES=Host=localhost;Database=ntqams;Username=qams_app;Password=dev-only-local`, inside the suite's rollback transaction |
| **Preconditions** | A resolved `ICurrentTenant` for `demo-lab`. The uuid `00000000-0000-0000-0000-0000000000ff` does **not** exist in `qams.user_account`. |
| **Test Data** | `SignAsync(signerId: 00000000-0000-0000-0000-0000000000ff, password: "anything", pin: "0000", meaning: "Approved and published X v1.0", subjectRef: "DOC:ffffffffffffffffffffffffffffffff", contentHash: "b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9")` |
| **Steps** | 1. Resolve `IESignatureService` from the test host. 2. Call `SignAsync` with the data above and capture the thrown exception. 3. Assert `DomainException.Code == "SIG-404"` and `Message == "Signer not found."`. 4. Assert `DomainExceptionHandler` maps a `*-404` code to `404 Not Found` (`DomainExceptionHandler.cs:66-71`) by exercising the handler directly. 5. `SELECT count(*) FROM audit.security_event WHERE occurred_at_utc > <t0>;`. |
| **Expected UI** | n/a — no screen can produce this state; `signerId` is always the authenticated `sub`. |
| **Expected API** | Not reachable over HTTP: `PublishDocumentHandler` passes `DocumentLoader.RequireActor(user)` as `signerId` (`DocumentCommands.cs:127,155`), and `ActiveSessionMiddleware` already returns `401 AUTH-006` when the `sub` has no `user_account` row (`RequestIdentity.cs:98-100`). If a mapping test is run in isolation the shape would be `404` `application/problem+json` with `code` = `SIG-404`. |
| **Expected DB** | No row written to `audit.electronic_signature`; no `qams.user_account` mutation. |
| **Expected Audit** | **Zero** new `audit.security_event` rows — the missing-signer branch throws before any `WriteAsync`. A Part-11 §11.300(d) reviewer therefore has no record of a signing attempt against a non-existent identity. |
| **Expected Notification** | n/a — no notification is defined for this branch. |
| **Cleanup** | n/a — the integration suite rolls the transaction back. |
| **Evidence** | Test assertion output showing code and message · the zero-row security-event count |
| **Result / Defect** | Not Run · — |
| **Notes** | Labelled `[ID]`: the guard exists in code with no URS behind it, and it is dead from every HTTP entry point. `SIG-404` is also thrown by `SigmaAssessmentSlice.cs:73,112` meaning "Sigma assessment not found" (`GAP-AUTH-011`) — never assert this code without the caller. |

---

#### TC-AUTH-INT-010 — Two concurrent publishes of one document mint **two** signatures and one 409  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-021, URS-022 · RSK-AUTH-016 (minted) — new gap `GAP-AUTH-901` |
| **Level / Type / Technique** | Integration · Concurrency (negative) · Error Guessing informed by Data Flow — the signature `SaveChanges` and the document `SaveChanges` are two separate transactions |
| **Priority / Severity / Automation** | High · Major · Yes (integration test with two parallel `HttpClient` calls) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fresh approved document `SOP-0008` v`1.0` authored by `Ada Author`. `Ben Approver` unlocked, PIN `4417`. Zero rows in `audit.electronic_signature` for `DOC:<DOC8_N>`. The e-signature budget is 10/min per actor, so two requests are within it. |
| **Test Data** | Two identical requests `POST /api/documents/<DOC8>/publish` `{"password":"Approver-Pass-2026!","pin":"4417"}`, dispatched with `Task.WhenAll` and **no** `Idempotency-Key` header |
| **Steps** | 1. Fire both requests simultaneously. 2. Record both status lines and both `code` extensions. 3. `SELECT count(*), array_agg(id) FROM audit.electronic_signature WHERE subject_ref='DOC:<DOC8_N>';` under bypass. 4. `SELECT status FROM qams.controlled_document WHERE id='<DOC8>';` and `SELECT state, version_label FROM qams.document_version WHERE document_id='<DOC8>';`. 5. `GET /api/documents/<DOC8>/signatures`. |
| **Expected UI** | The tab that lost shows the concurrency banner `The record was modified by someone else since it was loaded — reload and retry.`; the signature panel on reload shows **two** identical-meaning entries for one publication. |
| **Expected API** | One request → `204 No Content`. The other → `409 Conflict`, `application/problem+json`, `code` = `CONCURRENCY-409` (`DomainExceptionHandler.cs:21,28-33`) raised by the `xmin` token on `ControlledDocument` (`AppDbContext.cs:121-134`). Step 5 → `200` with a **two**-element array. |
| **Expected DB** | **Two** rows in `audit.electronic_signature` for `DOC:<DOC8_N>`, differing only in `id` and `signed_at_utc`. One document row at `status='Published'`, one version at `state='Published'` — a single publication carrying two signatures. |
| **Expected Audit** | One `audit.audit_trail` entry for `DocumentPublished` (only the winner raised the event). No `ESIGN_FAILED`. The ledger therefore asserts two signing ceremonies for one state change. |
| **Expected Notification** | n/a — the losing request raises no domain event, so no notification policy fires. |
| **Cleanup** | The orphan signature **cannot be deleted** (`signature_append_only`). Restore from the dev restore-point dump if a clean ledger is needed for later cases. |
| **Evidence** | Both HTTP captures with timings · the two-row psql result · the two-element `GET …/signatures` response |
| **Result / Defect** | Not Run · — |
| **Notes** | `GAP-AUTH-901`. `ESignatureService.SignAsync` commits the signature with its own `db.SaveChangesAsync` at `ComplianceLedgerServices.cs:129`, **before** `doc.Publish(...)` and its `SaveChangesAsync` at `DocumentCommands.cs:159-160`; there is no `TransactionBehavior` and no `BeginTransaction` anywhere in `src/NT.QAMS.Application` or `src/NT.QAMS.WebApi` (verified by grep). The comment at `DocumentCommands.cs:133-135` claims "a signature must never exist for a publish that then fails" — it pre-validates state and SoD but **not** concurrency. Do **not** rewrite this case to expect one signature: expecting a single row is asserting a requirement, not the build. |

---

#### TC-AUTH-INT-011 — Aborting the publish request after the signature commit leaves an orphan signature  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-021, URS-022 · RSK-AUTH-016 (minted) — `GAP-AUTH-901` |
| **Level / Type / Technique** | Integration · Reliability (negative) · Path coverage — the interrupted path between the two `SaveChangesAsync` calls |
| **Priority / Severity / Automation** | Medium · Major · No (requires deterministic connection abort; run manually or with a fault-injecting proxy) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; a TCP proxy (e.g. `toxiproxy`) in front of `:5080` able to cut the client connection on demand |
| **Preconditions** | Fresh approved document `SOP-0009` v`1.0`. Zero signature rows for `DOC:<DOC9_N>`. A `pg_stat_statements` or log-based trigger point that fires when the `INSERT INTO audit.electronic_signature` commits. |
| **Test Data** | `{"password":"Approver-Pass-2026!","pin":"4417"}` |
| **Steps** | 1. Start `POST /api/documents/<DOC9>/publish` through the proxy. 2. As soon as the `audit.electronic_signature` INSERT has committed (observe in the PostgreSQL statement log), cut the client connection so ASP.NET Core cancels `HttpContext.RequestAborted` and the `CancellationToken` passed to `doc.Publish`'s `SaveChangesAsync` (`DocumentCommands.cs:160`). 3. `SELECT count(*) FROM audit.electronic_signature WHERE subject_ref='DOC:<DOC9_N>';` under bypass. 4. `SELECT status FROM qams.controlled_document WHERE id='<DOC9>';` and the version state. 5. Retry the publish with the same credentials and observe the result. |
| **Expected UI** | The browser reports a network failure; on reload the document is still awaiting publication while the signature manifest already lists one signature. |
| **Expected API** | Step 2 produces no HTTP response (the connection is gone). No `OperationCanceledException` handler exists in the WebApi pipeline (grep of `src/NT.QAMS.WebApi` finds one only in `Startup/DeferredStartupSeeder.cs:41`), so nothing is mapped to problem+json. Step 5 → `204 No Content`. |
| **Expected DB** | After step 3: **one** signature row for a document whose `status` is still the pre-publish value and whose in-flight version is still `state='Approved'`. After step 5: **two** signature rows and `status='Published'`. |
| **Expected Audit** | No `ESIGN_FAILED` / `ESIGN_LOCKED` rows. No `audit.audit_trail` entry for `DocumentPublished` until step 5, so the trail shows a signature preceding its publication event. |
| **Expected Notification** | n/a — no domain event was raised at step 2. |
| **Cleanup** | Orphan signature rows are permanent; restore the dev restore-point dump. |
| **Evidence** | PostgreSQL statement log showing the committed INSERT · psql row counts before and after step 5 · document/version state |
| **Result / Defect** | Not Run · — |
| **Notes** | `GAP-AUTH-901`, same root cause as `TC-AUTH-INT-010` reached by a different path. Acceptance criteria to implement against: *(a)* `PublishDocumentHandler` wraps the signature mint and the state change in one explicit transaction (or the signature is written in the same `SaveChanges` as `doc.Publish`), so no signature can outlive a failed publish; *(b)* an integration case proves a forced `DbUpdateConcurrencyException` on the document leaves **zero** rows in `audit.electronic_signature`; *(c)* if minting first is deliberate, the URS states that a signature record may exist without a corresponding published version and the compliance viewer flags such rows. |

---

#### TC-AUTH-INT-012 — Cancelling the signing form writes nothing anywhere  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-020, URS-021 · RSK-AUTH-015 (minted) |
| **Level / Type / Technique** | Integration (browser + DB) · Functional (negative) · Use Case — the abandoned-ceremony path |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (Playwright, alongside the existing `regulated-workflow` spec) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | Chrome via Playwright against `localhost:4200/t/demo-lab`, API `:5080`, live PostgreSQL `ntqams` |
| **Preconditions** | Fresh approved document `SOP-0010` v`1.0`. Zero signature rows for `DOC:<DOC10_N>`. Browser devtools network log recording. |
| **Test Data** | Type `Approver-Pass-2026!` into the password input and `4417` into the PIN input of the publish form (`document-detail.component.ts:81-87`), then **do not submit** |
| **Steps** | 1. Open `/documents/<DOC10>` signed in as `Ben Approver`. 2. Fill both inputs of the publish form. 3. Navigate away to `/documents` using the in-app list link (the component is destroyed; there is no explicit Cancel control in the template). 4. Inspect the network log for any request to `/api/documents/<DOC10>/publish`. 5. Navigate back to `/documents/<DOC10>` and inspect the publish form's inputs. 6. `SELECT count(*) FROM audit.electronic_signature WHERE subject_ref='DOC:<DOC10_N>';` under bypass. 7. `SELECT count(*) FROM audit.security_event WHERE event_type IN ('ESIGN_FAILED','ESIGN_LOCKED') AND occurred_at_utc > <t0>;`. 8. `SELECT failed_login_attempts FROM qams.user_account WHERE email='approver@demo-lab.local';`. |
| **Expected UI** | Step 3 leaves the page without a confirmation prompt. Step 5 shows both inputs **empty** — `publishForm` is a component-scoped `FormGroup` (`document-detail.component.ts:268-271`) recreated on re-entry, so the typed PIN does not survive navigation. The document still shows the publish form, i.e. it is not published. |
| **Expected API** | Zero requests to `/api/documents/<DOC10>/publish` in the network log. |
| **Expected DB** | Zero rows in `audit.electronic_signature` for `DOC:<DOC10_N>`; `failed_login_attempts` unchanged at its pre-test value; `controlled_document.status` unchanged. |
| **Expected Audit** | Zero new `ESIGN_FAILED` / `ESIGN_LOCKED` rows. Zero new `audit.field_change` rows for the `UserAccount` of `Ben Approver`. |
| **Expected Notification** | n/a — no request was made. |
| **Cleanup** | n/a — nothing was written. |
| **Evidence** | Playwright trace with the network log · the two zero-count psql results · screenshot of the re-entered empty form |
| **Result / Defect** | Not Run · — |
| **Notes** | The template has **no** dedicated Cancel button (`document-detail.component.ts:81-89`); "cancellation" is navigation away or page reload. `pin` carries `Validators.pattern(/^\d{4}$/)` and `maxlength="4"` client-side (`:85`, `:270`), so a submit attempt with a 3-digit PIN never leaves the browser — that client-side boundary belongs to batch A's BVA set, not here. |

---

#### TC-AUTH-INT-013 — `audit.electronic_signature` refuses UPDATE and DELETE at the database  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-014, URS-022 · RSK-AUTH-012 (minted) |
| **Level / Type / Technique** | Integration (SQL) · Security / data-integrity (negative) · Decision Table over the trigger's two firing events (UPDATE, DELETE) × two column classes (`meaning`, `content_hash`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (integration test, alongside the existing audit-tamper tests) |
| **Role / Permission / Tenant** | Database role `qams_app` (dev **owner**; in Production the role holds INSERT/SELECT only) · n/a — this case bypasses the application entirely · `demo-lab` |
| **Environment** | psql against `ntqams` as `qams_app`, with `SELECT set_config('app.bypass_rls','on',false);` executed first |
| **Preconditions** | The signature row from `TC-AUTH-INT-001` exists; capture its `id` as `<SIGID>` and its `tenant_id` as `<TENANT>`. |
| **Test Data** | Target row `(tenant_id=<TENANT>, id=<SIGID>)`; attempted new meaning `'Approved and published SOP-0001 v9.9'`; attempted new hash `'0000000000000000000000000000000000000000000000000000000000000000'` |
| **Steps** | 1. `SELECT set_config('app.bypass_rls','on',false);`. 2. `UPDATE audit.electronic_signature SET meaning='Approved and published SOP-0001 v9.9' WHERE id='<SIGID>';`. 3. `UPDATE audit.electronic_signature SET content_hash='0000…0000' WHERE id='<SIGID>';`. 4. `DELETE FROM audit.electronic_signature WHERE id='<SIGID>';`. 5. Re-`SELECT` all eight columns of the row. 6. `SELECT tgname FROM pg_trigger WHERE tgrelid='audit.electronic_signature'::regclass AND NOT tgisinternal;`. |
| **Expected UI** | n/a — no screen can mutate a signature; the API exposes no PUT/DELETE on this ledger. |
| **Expected API** | n/a — this case operates below the API. |
| **Expected DB** | Steps 2, 3 and 4 each fail with `ERROR: audit ledgers are append-only` raised by `audit.reject_mutation()` (`20260721232300_ComplianceAndAuth.cs:154-158`). Step 5 returns the row byte-identical to `TC-AUTH-INT-001`'s assertion. Step 6 returns `signature_append_only`. Also assert `relrowsecurity='t'` and `relforcerowsecurity='t'` for `audit.electronic_signature` (measured `t/t` with one policy `tenant_isolation`, 2026-08-01). |
| **Expected Audit** | The rejected statements write **no** ledger row of their own; the failure is visible only in the PostgreSQL server log. Assert `count(*)` of `audit.field_change` is unchanged. |
| **Expected Notification** | n/a — a database-level rejection raises no application event. |
| **Cleanup** | n/a — all three statements are rejected before any row changes. |
| **Evidence** | psql transcript of the three rejections with the exact error text · the unchanged eight-column row · `pg_trigger` output |
| **Result / Defect** | Not Run · — |
| **Notes** | In dev, `qams_app` **owns** the schema, so the trigger — not the grant — is what stops the mutation; that is exactly what makes this case worth running in dev. In Production the grant is the first line of defence and this test would fail earlier with `permission denied`; record which environment produced the evidence. |

---

#### TC-AUTH-INT-014 — `ESIGN_FAILED` row shape: display-name actor, prefixed detail, null IP  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-016, URS-023 · RSK-AUTH-013 (minted) — `GAP-AUTH-005` |
| **Level / Type / Technique** | Integration · Functional (observability) · Data Flow — actor identity from `UserAccount.DisplayName` to `audit.security_event.actor`, and the never-written `ip_address` sink |
| **Priority / Severity / Automation** | High · Moderate · Yes (integration test) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `failed_login_attempts=0`, `locked_until_utc IS NULL`. Fresh approved document `SOP-0011` v`1.0`. Note the timestamp `<t0>` before the first request. Also perform one **login** failure so both actor conventions can be compared in one query. |
| **Test Data** | (a) publish with `{"password":"Approver-Pass-2026?","pin":"4417"}`; (b) `POST /api/auth/login` `{"slug":"demo-lab","email":"approver@demo-lab.local","password":"Nope-Wrong-Pass-1!"}` |
| **Steps** | 1. Execute (a). 2. Execute (b). 3. `SELECT set_config('app.bypass_rls','on',false);` then `SELECT event_type, actor, detail, tenant_id, ip_address, occurred_at_utc FROM audit.security_event WHERE occurred_at_utc > '<t0>' ORDER BY occurred_at_utc;`. 4. Compare the `actor` column of the two rows. 5. `SELECT count(*) FROM audit.security_event WHERE ip_address IS NOT NULL;`. |
| **Expected UI** | n/a — security events are surfaced only on the compliance screen, which is out of this batch's scope. |
| **Expected API** | (a) → `422` `SIG-002`. (b) → `401` `AUTH-001`. |
| **Expected DB** | Two new rows. Row 1: `event_type='ESIGN_FAILED'`, `actor='Ben Approver'`, `detail='bad-password:DOC:<DOC11_N>'`, `tenant_id` = the `demo-lab` uuid. Row 2: `event_type='LOGIN_FAILED'`, `actor='approver@demo-lab.local'` (the **email**), `detail='bad-password'`. `ip_address IS NULL` on both. |
| **Expected Audit** | Step 5 returns `0` for the whole table — `SecurityEventLog.WriteAsync` sets only `Id, TenantId, EventType, Actor, Detail, OccurredAtUtc` (`ComplianceLedgerServices.cs:73-81`) and never `IpAddress`, although the column is typed `inet` (`ComplianceConfigurations.cs:54-56`) and the address is already in scope at `RateLimiting.cs:97-98`. |
| **Expected Notification** | n/a — no notification policy subscribes to security events. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='approver@demo-lab.local';` |
| **Evidence** | The two-row psql result showing both actor conventions side by side · the `ip_address IS NOT NULL` count of zero |
| **Result / Defect** | Not Run · — |
| **Notes** | Labelled `[ID]`: `ip_address IS NULL` is the as-built state, not the requirement — URS-016 and Part 11 §11.300(d) expect attribution. Do not "fix" this case to expect an address; the fix is `GAP-AUTH-005`. The two `actor` conventions in one table make any single-column join across event types wrong — record that in the RTM. Two credential attempts on `approver@demo-lab.local` in one run consume two of the five lockout attempts; run this before the multi-failure cases. |

---

#### TC-AUTH-INT-015 — A successful signing writes no security event  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-016 · RSK-AUTH-013 (minted) |
| **Level / Type / Technique** | Integration · Functional (observability, negative assertion) · Branch coverage — the success path of `SignAsync` has no `WriteAsync` call |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (integration test) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fresh approved document `SOP-0012` v`1.0`. Timestamp `<t0>` captured. `failed_login_attempts` set to a known non-zero value `3` via three prior wrong-PIN attempts, so the "does success reset the counter?" assertion is meaningful. |
| **Test Data** | `{"password":"Approver-Pass-2026!","pin":"4417"}` |
| **Steps** | 1. Confirm `failed_login_attempts = 3`. 2. `POST /api/documents/<DOC12>/publish` with the correct credentials. 3. `SELECT event_type, occurred_at_utc FROM audit.security_event WHERE occurred_at_utc > '<t0>';` under bypass. 4. `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='approver@demo-lab.local';`. 5. `SELECT count(*) FROM audit.electronic_signature WHERE subject_ref='DOC:<DOC12_N>';`. |
| **Expected UI** | The document becomes `Published`; nothing indicates that the signing was or was not logged. |
| **Expected API** | `204 No Content`. |
| **Expected DB** | One signature row. `failed_login_attempts` **still `3`** — a successful signing does **not** call `RegisterSuccessfulLogin`, unlike `LoginHandler`. `locked_until_utc` still `NULL`. |
| **Expected Audit** | **Zero** rows returned by step 3 for `event_type LIKE 'ESIGN%'`. The only trace of the successful signing is the `audit.electronic_signature` row itself plus the `DocumentPublished` `audit.audit_trail` entry. |
| **Expected Notification** | n/a — no notification policy subscribes to a successful signing. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0 WHERE email='approver@demo-lab.local';` |
| **Evidence** | The empty step-3 result · the unchanged counter · the single signature row |
| **Result / Defect** | Not Run · — |
| **Notes** | Two as-built asymmetries in one case, both `[ID]`: the security ledger records failed signings but not successful ones (login records both), and the shared lockout counter is advanced by a failed signing but never cleared by a successful one. The second is the mechanism behind `TC-AUTH-SEC-032`. |

---

#### TC-AUTH-INT-016 — Opening a user-access review creates an `Open` row with a generated reference  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-010 · RSK-AUTH-020 (minted) |
| **Level / Type / Technique** | Integration · Functional (positive) · State Transition — `(none) → Open`, front-matter §3.3 row 1 |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | `admin@demo-lab.local` (tier `TenantAdmin`) · `access-reviews.view` — the class filter at `AccessReviewsController.cs:20`, inherited by the POST because the method declares no override (`:27-29`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SELECT last_value FROM qams.ref_counter WHERE tenant_id='<TENANT>' AND ref_type='UAR' AND year=2026;` — record it as `<N>` (or note the row is absent, in which case the first issue is `1`). |
| **Test Data** | `POST /api/access-reviews` with an empty body |
| **Steps** | 1. `POST /api/access-reviews` as `admin@demo-lab.local`. 2. Read the status and the `id` field. 3. `SELECT id, tenant_id, review_ref, opened_on, status, reviewed_by, completed_at_utc, accounts_reviewed, changes_required, conclusion FROM qams.user_access_review WHERE id='<UARID>';`. 4. `SELECT last_value FROM qams.ref_counter WHERE tenant_id='<TENANT>' AND ref_type='UAR' AND year=2026;`. 5. `GET /api/access-reviews`. |
| **Expected UI** | The access-review list gains a row showing the new reference with status `Open` and today's date; no reviewer, no conclusion. |
| **Expected API** | `200 OK` with body `{"id":"<uuid>"}` (`AccessReviewsController.cs:29` returns `Ok(new { id = … })` — **not** `201`, and there is no `Location` header). Step 5 → `200` with a `UserAccessReviewDto` array whose newest element has `status:"Open"` and nulls for `reviewedBy`, `completedAtUtc`, `accountsReviewed`, `changesRequired`, `conclusion`. |
| **Expected DB** | One row: `tenant_id` = the `demo-lab` uuid (stamped by the tenant interceptor); `review_ref` = `'UAR-2026-'` followed by `<N>+1` zero-padded to four digits (`PostgresReferenceNumberGenerator.NextAsync`, `RefCounter.cs:41`); `opened_on` = today's UTC date (`AccessReviewSlice.cs:34`); `status='Open'` (satisfying `ck_user_access_review_status_domain`); `reviewed_by`, `completed_at_utc`, `accounts_reviewed`, `changes_required`, `conclusion` all `NULL`. `qams.ref_counter.last_value` incremented by exactly 1. |
| **Expected Audit** | One `audit.field_change` row per persisted property with `action='Created'` for `EntityType='UserAccessReview'`. **No** domain event: `UserAccessReview.Open` raises nothing (`UserAccessReview.cs:30-36`), so there is no `audit.audit_trail` entry for the opening. |
| **Expected Notification** | n/a — no notification policy subscribes to review opening. |
| **Cleanup** | `DELETE FROM qams.user_access_review WHERE id='<UARID>';` under `set_config('app.bypass_rls','on',false)` — `qams.user_access_review` carries no append-only trigger. The `ref_counter` gap is permanent and intended (the generator is gapless only within committed history). |
| **Evidence** | HTTP capture of steps 1 and 5 · the ten-column psql row · the before/after `ref_counter` values |
| **Result / Defect** | Not Run · — |
| **Notes** | `UserAccessReview.Open` performs **no validation at all** — no duplicate-open check, so a tenant can hold any number of simultaneously `Open` reviews. That is the as-built contract; do not assert a uniqueness rule. Whether the write should require more than `access-reviews.view` is `GAP-AUTH-004`, covered in batch D. |

---

#### TC-AUTH-INT-017 — Completing a review snapshots the active-account count and the conclusion  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-010 · RSK-AUTH-020 (minted) |
| **Level / Type / Technique** | Integration · Functional (positive) · State Transition — `Open → Completed`, front-matter §3.3 row 2 |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | `admin@demo-lab.local` · `access-reviews.view` + command policy `[RequireInternalActor]` (`AccessReviewSlice.cs:15`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | The `Open` review `<UARID>` from `TC-AUTH-INT-016`. Establish the exact active-account count: `SELECT count(*) FROM qams.user_account WHERE tenant_id='<TENANT>' AND is_active=true;` — record it as `<ACTIVE>`. Ensure at least one **inactive** tenant user exists (`POST /api/users/{id}/deactivate` on a throwaway account) so the `is_active` predicate is exercised. |
| **Test Data** | `POST /api/access-reviews/<UARID>/complete` body `{"changesRequired":true,"conclusion":"  Roster recertified; 1 dormant analyst account deactivated.  "}` — note the deliberate leading and trailing double spaces |
| **Steps** | 1. Send the request. 2. Read the status. 3. `SELECT status, reviewed_by, completed_at_utc, accounts_reviewed, changes_required, conclusion, length(conclusion) FROM qams.user_access_review WHERE id='<UARID>';`. 4. Compare `accounts_reviewed` with `<ACTIVE>`. 5. Compare `reviewed_by` with the `sub` claim of `admin@demo-lab.local`. 6. `GET /api/access-reviews` and read the completed element. |
| **Expected UI** | The list row flips to status `Completed`, shows the reviewer's name, the completion timestamp, the account count and the conclusion **without** the surrounding whitespace. |
| **Expected API** | `204 No Content` (`AccessReviewsController.cs:35`). Step 6 → `200`; the element carries `status:"Completed"`, `accountsReviewed:<ACTIVE>`, `changesRequired:true`, and the trimmed conclusion. |
| **Expected DB** | `status='Completed'`; `reviewed_by` = the actor's uuid (`ICurrentUser.UserId`, `AccessReviewSlice.cs:49`); `completed_at_utc` non-null and from `IClock`; `accounts_reviewed` = `<ACTIVE>` exactly — the handler counts `u.TenantId == tenantId && u.IsActive` at the completion instant (`AccessReviewSlice.cs:55-56`), so the deactivated throwaway account is **excluded**; `changes_required = true`; `conclusion = 'Roster recertified; 1 dormant analyst account deactivated.'` with `length` = the trimmed length (`UserAccessReview.cs:56` calls `.Trim()`). |
| **Expected Audit** | `audit.field_change` rows with `action='Modified'` for `Status`, `ReviewedBy`, `CompletedAtUtc`, `AccountsReviewed`, `ChangesRequired`, `Conclusion`. One `audit.audit_trail` entry for `UserAccessReviewCompleted` (`UserAccessReview.cs:57`) after the outbox cycle, its payload carrying `ReviewId`, `ReviewRef`, `ChangesRequired`, `ReviewedBy`, `TenantId`. |
| **Expected Notification** | n/a — no notification policy subscribes to `UserAccessReviewCompleted`; verify by asserting zero new `qams.notification_dispatch` rows for the review id. |
| **Cleanup** | `DELETE FROM qams.user_access_review WHERE id='<UARID>';` under bypass; reactivate the throwaway account via `POST /api/users/{id}/reactivate`. |
| **Evidence** | HTTP `204` capture · the seven-column psql row including `length(conclusion)` · the `<ACTIVE>` count taken before and after |
| **Result / Defect** | Not Run · — |
| **Notes** | `accounts_reviewed` is a single integer sampled at completion — it identifies no account and proves no per-account decision (`GAP-AUTH-008`). This case asserts only that the count equals the active-user count at that instant; it must not be described as evidence of recertification. |

---

#### TC-AUTH-INT-018 — A completed review is immutable: re-completion is `409 UAR-010` and changes nothing  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-010, URS-022 · RSK-AUTH-021 (minted) |
| **Level / Type / Technique** | Integration · Functional (negative) · State Transition — the illegal `Completed → Completed` edge, front-matter §3.3 row 3 |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | `admin@demo-lab.local` · `access-reviews.view` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Review `<UARID>` completed by `TC-AUTH-INT-017`. Snapshot every column: `SELECT * FROM qams.user_access_review WHERE id='<UARID>';` and keep the output as `<BEFORE>`. |
| **Test Data** | (a) same-conclusion retry `{"changesRequired":true,"conclusion":"Roster recertified; 1 dormant analyst account deactivated."}`; (b) contradicting retry `{"changesRequired":false,"conclusion":"Actually nothing required."}`; (c) blank-conclusion retry `{"changesRequired":false,"conclusion":"   "}` |
| **Steps** | 1. `POST /api/access-reviews/<UARID>/complete` with (a); read status, `title`, `code`. 2. Repeat with (b). 3. Repeat with (c). 4. `SELECT * FROM qams.user_access_review WHERE id='<UARID>';` and diff against `<BEFORE>`. 5. `SELECT count(*) FROM audit.field_change WHERE entity_type='UserAccessReview' AND entity_id='<UARID>' AND occurred_at_utc > <t1>;`. |
| **Expected UI** | The Complete control is not rendered for a `Completed` review; a forced retry (devtools replay) surfaces the banner `The access review is already completed and immutable.` |
| **Expected API** | (a) and (b) → `409 Conflict`, `application/problem+json`, `title` = `The access review is already completed and immutable.`, `code` = `UAR-010` (`UserAccessReview.cs:43`; `InvalidStateTransitionException` → 409, `DomainExceptionHandler.cs:45-50`). (c) → **`400 Bad Request`** with a FluentValidation `errors` envelope keyed `Conclusion` and **no** `code` extension — `CompleteAccessReviewValidator` (`AccessReviewSlice.cs:22`) runs in `ValidationBehavior`, which sits after `AuthorizationBehavior`/`IdempotencyBehavior` but **before** the handler (`Application/DependencyInjection.cs:20-24`), so the request never reaches the domain's status check. |
| **Expected DB** | Step 4 diff is empty — every column identical to `<BEFORE>`, including `xmin` (no write occurred, so the row version is unchanged). |
| **Expected Audit** | Step 5 returns `0` — a rejected completion writes no `audit.field_change` row and no `audit.audit_trail` entry. Assert also that no second `UserAccessReviewCompleted` event reaches `qams.outbox_event`. |
| **Expected Notification** | n/a — no event was raised. |
| **Cleanup** | `DELETE FROM qams.user_access_review WHERE id='<UARID>';` under bypass. |
| **Evidence** | Three HTTP captures showing `409/409/400` · the empty column diff including `xmin` · the zero field-change count |
| **Result / Defect** | Not Run · — |
| **Notes** | There is **no** reopen, void or amend path on `UserAccessReview` — the aggregate exposes only `Open` and `Complete` (`UserAccessReview.cs:30,38`) and the DB CHECK admits only `'Open'` and `'Completed'`. Case (c) proves the ordering explicitly: validation precedes the immutability guard, so a blank-conclusion retry on a completed review reports the *wrong* reason (400 validation) rather than 409 immutability. Record that ordering in the RTM; it is as-built, not a defect. |

---

#### TC-AUTH-INT-019 — A blank conclusion is refused at the validator, leaving `UAR-011` unreachable over HTTP  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-010 · RSK-AUTH-020 (minted) |
| **Level / Type / Technique** | Integration + Unit · Functional (negative) · Equivalence Partitioning over the blank partitions (`""`, `"   "`, `"\t\n"`, JSON `null`) plus Statement coverage of the unreachable domain guard |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional + domain unit test) |
| **Role / Permission / Tenant** | `admin@demo-lab.local` · `access-reviews.view` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; the domain arm runs in `Domain.UnitTests` with no database |
| **Preconditions** | A freshly opened review `<UARID2>` in state `Open`. |
| **Test Data** | Four bodies, all with `"changesRequired":false`: `"conclusion":""`, `"conclusion":"   "`, `"conclusion":"\t\n"`, `"conclusion":null` |
| **Steps** | 1. Send each of the four bodies to `POST /api/access-reviews/<UARID2>/complete`. 2. For each, read status, `content-type`, and whether a `code` extension is present. 3. `SELECT status FROM qams.user_access_review WHERE id='<UARID2>';`. 4. In `Domain.UnitTests`, call `UserAccessReview.Open("UAR-2026-0099", today).Complete(Guid.NewGuid(), now, 7, false, "   ")` and assert the thrown `DomainException.Code == "UAR-011"`. |
| **Expected UI** | The Complete button stays disabled while the conclusion textarea is empty; a forced submit shows the field-level message `'Conclusion' must not be empty.` |
| **Expected API** | All four → `400 Bad Request`, `application/problem+json`, `title` = `Validation failed.`, an `errors` object keyed `"Conclusion"`, and **no** `code` extension (`DomainExceptionHandler.cs:34-44`). FluentValidation's `NotEmpty()` rejects whitespace-only strings, so `"   "` and `"\t\n"` never reach the aggregate; `UAR-011` (422) is therefore **unreachable through the API**. |
| **Expected DB** | `status` still `'Open'` after all four attempts; no column of the row is written. |
| **Expected Audit** | Zero new `audit.field_change` rows for `<UARID2>`; zero `audit.audit_trail` entries. |
| **Expected Notification** | n/a — no event was raised. |
| **Cleanup** | `DELETE FROM qams.user_access_review WHERE id='<UARID2>';` under bypass. |
| **Evidence** | Four HTTP captures showing the identical 400 envelope · the unchanged `status` · the unit-test assertion output for `UAR-011` |
| **Result / Defect** | Not Run · — |
| **Notes** | The domain guard at `UserAccessReview.cs:46-49` is real and correct but dead from the API — that is defence in depth, not a defect. Author the `UAR-011` assertion at unit level only (step 4); do **not** author an API case that expects `422 UAR-011`, because it cannot be produced. |

---

#### TC-AUTH-INT-020 — Conclusion length boundary: 4000 characters accepted, 4001 refused  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-010 · RSK-AUTH-020 (minted) |
| **Level / Type / Technique** | Integration · Functional (boundary) · BVA at the validator's `MaximumLength(4000)` — test points 3999 / 4000 / 4001 |
| **Priority / Severity / Automation** | Medium · Minor · Yes (functional) |
| **Role / Permission / Tenant** | `admin@demo-lab.local` · `access-reviews.view` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Three freshly opened reviews `<UAR-A>`, `<UAR-B>`, `<UAR-C>`, all `Open`. `SELECT data_type FROM information_schema.columns WHERE table_schema='qams' AND table_name='user_access_review' AND column_name='conclusion';` returns `text` (measured) — the bound is the validator's, not the column's. |
| **Test Data** | `'A'` repeated 3999 times → `<UAR-A>`; `'A'` repeated 4000 times → `<UAR-B>`; `'A'` repeated 4001 times → `<UAR-C>`. All with `"changesRequired":false`. |
| **Steps** | 1. Complete `<UAR-A>` with the 3999-character conclusion. 2. Complete `<UAR-B>` with 4000. 3. Complete `<UAR-C>` with 4001. 4. `SELECT id, status, length(conclusion) FROM qams.user_access_review WHERE id IN ('<UAR-A>','<UAR-B>','<UAR-C>');`. |
| **Expected UI** | The conclusion textarea accepts long text; on the 4001-character submit the field-level message reads `The length of 'Conclusion' must be 4000 characters or fewer. You entered 4001 characters.` |
| **Expected API** | Steps 1 and 2 → `204 No Content`. Step 3 → `400 Bad Request`, `application/problem+json`, `errors` keyed `"Conclusion"`, no `code` extension. |
| **Expected DB** | `<UAR-A>`: `status='Completed'`, `length(conclusion)=3999`. `<UAR-B>`: `status='Completed'`, `length(conclusion)=4000`. `<UAR-C>`: `status='Open'`, `conclusion IS NULL`. |
| **Expected Audit** | Two sets of `Modified` `audit.field_change` rows (for A and B) and none for C. Two `UserAccessReviewCompleted` entries in `audit.audit_trail`. |
| **Expected Notification** | n/a — no notification policy subscribes to review completion. |
| **Cleanup** | `DELETE FROM qams.user_access_review WHERE id IN ('<UAR-A>','<UAR-B>','<UAR-C>');` under bypass. |
| **Evidence** | Three HTTP captures · the three-row psql result with `length()` |
| **Result / Defect** | Not Run · — |
| **Notes** | Because `conclusion` is `text` since `Hardening1`, removing `MaximumLength(4000)` from `CompleteAccessReviewValidator` would silently uncap a Part-11 record field with no database backstop — that is the standing rule in CLAUDE.md §5 ("never drop a varchar bound without a matching validator rule") read in the other direction. Note it in the RTM rather than opening a gap: the pairing is currently correct. |

---

#### TC-AUTH-INT-021 — Completing another tenant's review returns `404 UAR-404`, not the record  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-008, URS-010 · RSK-AUTH-022 (minted) |
| **Level / Type / Technique** | Integration · Security (negative) · Data Flow — an attacker-chosen review id flowing into a handler query that carries **no** tenant predicate, fenced by the EF global filter and FORCE RLS |
| **Priority / Severity / Automation** | Critical · Critical · Yes (integration test with a two-tenant fixture) |
| **Role / Permission / Tenant** | Tenant A: `admin@demo-lab.local` · `access-reviews.view` · `demo-lab`; the target row belongs to tenant B (`other-lab`) |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` with a second provisioned tenant `other-lab` and its own admin |
| **Preconditions** | An `Open` review `<UAR-B1>` exists in tenant `other-lab`; capture its uuid out of band with `set_config('app.bypass_rls','on',false)`. Snapshot its columns as `<BEFORE-B>`. Tenant A's admin is signed in and their JWT carries `tenant_id` = the `demo-lab` uuid (`RequestIdentity.cs:53-65` reads the tenant from the claim only — a header or query parameter cannot override it). |
| **Test Data** | `POST /api/access-reviews/<UAR-B1>/complete` `{"changesRequired":false,"conclusion":"Cross-tenant completion attempt."}` sent with tenant A's bearer token |
| **Steps** | 1. Send the request as tenant A. 2. Read status, `title` and `code`. 3. Under bypass, `SELECT * FROM qams.user_access_review WHERE id='<UAR-B1>';` and diff against `<BEFORE-B>`. 4. `GET /api/access-reviews` as tenant A and confirm `<UAR-B1>` is absent. 5. In psql **without** bypass and with `set_config('app.current_tenant','<TENANT-A>',false)`, `SELECT count(*) FROM qams.user_access_review WHERE id='<UAR-B1>';`. 6. Repeat step 1 with an `Idempotency-Key` header to confirm the refusal is not cached as a success. |
| **Expected UI** | Not reachable from the SPA — the list never contains another tenant's review, so no control targets it. A crafted request via devtools shows the banner `Access review not found.` |
| **Expected API** | `404 Not Found`, `application/problem+json`, `title` = `Access review not found.`, `code` = `UAR-404` (`AccessReviewSlice.cs:52`). Step 4 → `200` with an array containing only tenant A's reviews. Step 6 → `404` again. |
| **Expected DB** | Step 3 diff is empty — `<UAR-B1>` is untouched, still `status='Open'`. Step 5 returns `0`: the FORCE-RLS policy `tenant_isolation` on `qams.user_access_review` (`USING (tenant_id = NULLIF(current_setting('app.current_tenant',true),'')::uuid OR current_setting('app.bypass_rls',true)='on')`, measured) hides the row even from raw SQL under tenant A's GUC. |
| **Expected Audit** | No `audit.field_change` and no `audit.audit_trail` row for `<UAR-B1>`. **No** security event either — a cross-tenant miss is indistinguishable in the ledger from a mistyped id. |
| **Expected Notification** | n/a — no event was raised. |
| **Cleanup** | Delete `<UAR-B1>` under bypass; leave tenant `other-lab` in place for sibling batches. |
| **Evidence** | HTTP capture with `UAR-404` · the empty column diff · the RLS-hidden `count(*) = 0` under tenant A's GUC |
| **Result / Defect** | Not Run · — |
| **Notes** | `CompleteAccessReviewHandler` loads with `FirstOrDefaultAsync(r => r.Id == c.ReviewId)` and **no** tenant predicate (`AccessReviewSlice.cs:51`) — isolation rests entirely on the EF global query filter (`AppDbContext.cs:188-191`) plus FORCE RLS. That makes this the load-bearing isolation case for the whole slice: if the global filter is ever disabled for this entity, only RLS stands between tenants. Contrast `OpenAccessReviewHandler`, which resolves the tenant explicitly and throws `TENANT-000` when absent (`:31-32`). |

---

#### TC-AUTH-SEC-030 — Five consecutive wrong PINs lock the signer; the fifth attempt is the threshold  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003, URS-023 · RSK-AUTH-011 (minted) |
| **Level / Type / Technique** | API + Integration · Security (negative) · BVA at the `MaxFailedAttempts = 5` threshold (`UserAccount.cs:29`) — attempts 4, 5 and 6 are the boundary points |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='approver@demo-lab.local';`. A fresh approved document `SOP-0013` v`1.0`. Six requests fit inside the 10/min per-actor e-signature budget, so the throttle does not interfere — but run this case in its **own minute**. |
| **Test Data** | Attempts 1–5: `{"password":"Approver-Pass-2026!","pin":"0001"}` … `"0005"` (all wrong). Attempt 6: `{"password":"Approver-Pass-2026!","pin":"4417"}` (**correct**). |
| **Steps** | 1. Send attempts 1–4, reading `code` and `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='approver@demo-lab.local';` after each. 2. Send attempt 5 and read the same. 3. Send attempt 6 with the correct PIN and read `code`. 4. `SELECT event_type, detail FROM audit.security_event WHERE occurred_at_utc > <t0> ORDER BY occurred_at_utc;` under bypass. 5. `SELECT count(*) FROM audit.electronic_signature WHERE subject_ref='DOC:<DOC13_N>';`. |
| **Expected UI** | Attempts 1–5 show `Electronic-signature PIN is not set or is incorrect.`; attempt 6 shows `Account is temporarily locked after repeated failed signings.` even though the PIN typed was correct. |
| **Expected API** | Attempts 1–5 → `422` `SIG-001`. Attempt 6 → `422` **`SIG-003`**. |
| **Expected DB** | After attempts 1–4: `failed_login_attempts` = 1, 2, 3, 4 and `locked_until_utc IS NULL`. After attempt 5: `failed_login_attempts` = **`0`** and `locked_until_utc` ≈ `now() + 30 minutes` — the counter is zeroed at lockout (`UserAccount.cs:214-215`; front-matter §0.1 corrects the ground-truth block, which asserts the impossible `failed_attempts = 5`). After attempt 6: both values **unchanged** (no lock extension). Zero signature rows throughout. |
| **Expected Audit** | Five `ESIGN_FAILED` rows with `detail='bad-pin:DOC:<DOC13_N>'`, then one `ESIGN_LOCKED` row with `detail='DOC:<DOC13_N>'`. One `audit.audit_trail` entry for the `UserLockedOut` domain event raised at `UserAccount.cs:216`, carrying `UserId`, `Email` and `LockedUntilUtc`. `audit.field_change` shows `FailedLoginAttempts` `4 → 0` and `LockedUntilUtc` `null → <ts>` on the fifth attempt. |
| **Expected Notification** | n/a — no notification policy subscribes to `UserLockedOut` in this build; assert zero new `qams.notification_dispatch` rows for this user. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='approver@demo-lab.local';` |
| **Evidence** | Six HTTP captures with codes in order · the counter/lock values after each attempt · the six-row security-event sequence · the `UserLockedOut` trail entry |
| **Result / Defect** | Not Run · — |
| **Notes** | A 4-digit PIN is 10,000 combinations; the compensating controls are exactly this 5-attempt lockout and the 10/min per-actor budget of `TC-AUTH-SEC-033`. `GAP-AUTH-002` records that the 4-digit rule lives only in `SetPinValidator` — the signing service will verify a PIN of any shape, so this brute-force arithmetic holds only while every PIN is set through `POST /api/auth/signature-pin`. |

---

#### TC-AUTH-SEC-031 — A signing lockout also locks the account out of **login**  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003, URS-023 · RSK-AUTH-014 (minted) |
| **Level / Type / Technique** | Integration · Security (negative) · State Transition — the shared `(Activation, Lockout)` machine entered from the e-signature path and observed from the login path |
| **Priority / Severity / Automation** | Critical · Major · Yes (functional) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` for the signing arm; anonymous for the login arm · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Run `TC-AUTH-SEC-030` steps 1–2 first so the account is in `S2 Active-Locked` with `locked_until_utc` ≈ `now() + 30 min`. Record that timestamp as `<LOCK>`. The `/api/auth/*` partition is 10/min **per client IP** and is a different partition from the e-signature budget, so the login attempts below are independently budgeted. |
| **Test Data** | (a) `POST /api/auth/login` `{"slug":"demo-lab","email":"approver@demo-lab.local","password":"Approver-Pass-2026!"}` — the **correct** password. (b) the same after `<LOCK>` has passed. |
| **Steps** | 1. Send (a) immediately after the signing lockout. 2. Read status, `title` and `code`. 3. `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='approver@demo-lab.local';` and compare with `<LOCK>`. 4. `SELECT event_type, actor, detail FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 1;` under bypass. 5. `UPDATE qams.user_account SET locked_until_utc = now() - interval '1 second' WHERE email='approver@demo-lab.local';` to simulate expiry, then send (b). |
| **Expected UI** | The sign-in form shows the locked message; the inputs stay enabled. The user has no way to distinguish a login-caused lock from a signing-caused one. |
| **Expected API** | (a) → `401 Unauthorized`, `application/problem+json`, `title` = `Account is temporarily locked. Try again later.`, `code` = **`AUTH-004`** (`Login.cs:79`) — **not** `SIG-003`, because the login handler owns this branch. (b) → `200 OK` with a populated `accessToken` and a `Set-Cookie: qams_rt=…; Path=/api/auth; HttpOnly; Secure; SameSite=Strict`. |
| **Expected DB** | After (a): `failed_login_attempts` still `0`, `locked_until_utc` **identical to `<LOCK>`** — the `AUTH-004` branch returns before `RegisterFailedLogin`, so a correct password against a locked account neither extends the lock nor advances the counter. After (b): `failed_login_attempts=0`, `locked_until_utc IS NULL` (`RegisterSuccessfulLogin`, `UserAccount.cs:220-224`). |
| **Expected Audit** | After (a): one `audit.security_event` `event_type='LOGIN_FAILED'`, `actor='approver@demo-lab.local'` (the **email** on login paths), `detail='locked-out'`. After (b): one `LOGIN_SUCCESS`. |
| **Expected Notification** | n/a — no notification is defined for a failed or successful login. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='approver@demo-lab.local';` and `POST /api/auth/logout` to revoke the family created by (b). |
| **Evidence** | Both HTTP captures with codes · the `<LOCK>` comparison · the `LOGIN_FAILED`/`locked-out` row |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the availability consequence of one shared counter: a signer who fumbles their PIN five times inside a valid session is locked out of the **whole product** for 30 minutes, and there is **no administrative unlock** (`GAP-AUTH-013`) — the only remedies are waiting or `POST /api/users/{id}/reset-password`, which forcibly changes a credential the user did not ask to change. Exploratory charter EXPL-3 explores the interleaving further. |

---

#### TC-AUTH-SEC-032 — A successful signing does not reset the counter, so four bad PINs plus one bad login lock the account  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003, URS-023 · RSK-AUTH-014 (minted) |
| **Level / Type / Technique** | Integration · Security (negative) · Error Guessing over a mixed-source interleaving of the shared counter, with Data Flow on `UserAccount.FailedLoginAttempts` |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign`, then anonymous for the login step · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `failed_login_attempts=0`, `locked_until_utc IS NULL`. Two fresh approved documents: `SOP-0014` (target of the four failures) and `SOP-0015` (target of the successful signing), both v`1.0`, both authored by `Ada Author`. |
| **Test Data** | Four publishes of `<DOC14>` with `{"password":"Approver-Pass-2026!","pin":"1111"}`; one publish of `<DOC15>` with `{"password":"Approver-Pass-2026!","pin":"4417"}`; one `POST /api/auth/login` with `{"slug":"demo-lab","email":"approver@demo-lab.local","password":"Wrong-Pass-2026!"}` |
| **Steps** | 1. Send the four wrong-PIN publishes against `<DOC14>`. 2. `SELECT failed_login_attempts FROM qams.user_account WHERE email='approver@demo-lab.local';`. 3. Send the correct-credential publish against `<DOC15>`. 4. Re-read the counter. 5. Send the single wrong-password login. 6. Re-read the counter and `locked_until_utc`. 7. `SELECT event_type, detail FROM audit.security_event WHERE occurred_at_utc > <t0> ORDER BY occurred_at_utc;` under bypass. |
| **Expected UI** | Nothing in the UI warns the user that a successful signature left four strikes standing; the next login failure locks them out with no prior indication. |
| **Expected API** | Step 1 → four `422` `SIG-001`. Step 3 → `204 No Content`. Step 5 → `401` `AUTH-001` (`Login.cs:86`), **and** the account is now locked. |
| **Expected DB** | Step 2 → `failed_login_attempts = 4`. Step 4 → **still `4`** — the success path of `SignAsync` never calls `RegisterSuccessfulLogin` (`ComplianceLedgerServices.cs:117-130`). Step 6 → `failed_login_attempts = 0` and `locked_until_utc ≈ now() + 30 minutes`, because the fifth increment came from the login path and crossed the threshold. One signature row for `DOC:<DOC15_N>`; zero for `DOC:<DOC14_N>`. |
| **Expected Audit** | Four `ESIGN_FAILED` (`bad-pin:DOC:<DOC14_N>`), **no** event for the successful signing, then one `LOGIN_FAILED` (`bad-password`). One `UserLockedOut` entry in `audit.audit_trail`. |
| **Expected Notification** | n/a — none of these events has a notification policy. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='approver@demo-lab.local';` |
| **Evidence** | Counter readings at steps 2, 4 and 6 · the six-row security-event sequence showing the gap where the success should be · the lock timestamp |
| **Result / Defect** | Not Run · — |
| **Notes** | Labelled `[ID]`: no URS states whether a successful signing should clear the counter, and the code's asymmetry with `LoginHandler` (which calls `RegisterSuccessfulLogin` at every success) is unstated anywhere. Do not "correct" the expectation to a reset. If the product decides a successful signing should reset, the acceptance criterion is that `ESignatureService.SignAsync` calls `RegisterSuccessfulLogin` before persisting the signature and an integration case proves the counter reads `0` after the sequence above. |

---

#### TC-AUTH-SEC-033 — The 11th signing attempt in one minute is throttled `429` before the ceremony runs  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-023 · RSK-AUTH-011 (minted) |
| **Level / Type / Technique** | API · Security (negative) · BVA at the `RateLimit:ESignaturePermitPerMinute` default of 10 (`RateLimiting.cs:25`) — attempts 10 and 11 are the boundary points |
| **Priority / Severity / Automation** | High · Major · Yes (functional; run **last** in any suite — it exhausts the actor's partition for a full minute) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; confirm `RateLimit:ESignaturePermitPerMinute` is unset (default 10) before starting |
| **Preconditions** | `failed_login_attempts=0`. A fresh approved document `SOP-0016` v`1.0`. **Important interaction:** the 5-attempt lockout binds before the 10/min budget when every attempt is a credential failure, so attempts 1–4 use a wrong PIN and attempts 5–11 use a **wrong document id** (`<NOSUCHDOC>`, a random uuid) so they are refused by the document loader without touching the lockout counter. Note the exact wall-clock second `<t0>` of attempt 1. |
| **Test Data** | Attempts 1–4: `POST /api/documents/<DOC16>/publish` `{"password":"Approver-Pass-2026!","pin":"1111"}`. Attempts 5–11: `POST /api/documents/<NOSUCHDOC>/publish` with the same body. All eleven within 60 seconds of `<t0>`. |
| **Steps** | 1. Send attempts 1–4 and confirm each returns `422 SIG-001`. 2. Send attempts 5–10 and record their statuses. 3. Send attempt 11 within the same fixed window. 4. Read attempt 11's status line, its `Retry-After` header and its body. 5. `SELECT failed_login_attempts FROM qams.user_account WHERE email='approver@demo-lab.local';`. 6. `SELECT count(*) FROM audit.security_event WHERE occurred_at_utc > <t0>;` under bypass. 7. Wait for the next fixed window and repeat attempt 11. |
| **Expected UI** | The publish form shows a generic failure; the SPA has no dedicated 429 message for the signing endpoint. |
| **Expected API** | Attempts 1–4 → `422` `SIG-001`. Attempts 5–10 → `404` `DOC-404` from the document loader (assert the code observed; the loader's not-found code belongs to module `DOC`). Attempt 11 → **`429 Too Many Requests`** with header `Retry-After: 60` (`RateLimiting.cs:57-60`) and an **empty body** — `OnRejected` writes no `application/problem+json`, unlike every other error path. Step 7 → the request is admitted again. |
| **Expected DB** | `failed_login_attempts` = `4` after step 5 — attempts 5–11 never reach `ESignatureService`, so none increments the counter. Zero signature rows. |
| **Expected Audit** | Exactly **four** `ESIGN_FAILED` rows since `<t0>` and no row for attempt 11: `UseRateLimiter` sits before `TenantResolution`/`ActiveSession` in the pipeline (`Program.cs:254-272`), so a throttled request never reaches the handler, the DB, or the security-event log. |
| **Expected Notification** | n/a — a 429 raises no application event. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0 WHERE email='approver@demo-lab.local';` Wait 60 seconds before the next e-signature case. |
| **Evidence** | Eleven timestamped HTTP captures · the `Retry-After: 60` header and empty body of attempt 11 · the counter value · the four-row security-event count |
| **Result / Defect** | Not Run · — |
| **Notes** | The window is a **fixed** 1-minute window with `QueueLimit = 0` (`RateLimiting.cs:51,88-92`), not sliding — so 10 attempts at 00:59 and 10 more at 01:01 are both admitted. That doubling at the window edge is inherent to the chosen limiter; record it, do not treat it as a defect. The empty 429 body is a genuine inconsistency with the problem+json contract asserted everywhere else — see `GAP-AUTH-904`. |

---

#### TC-AUTH-SEC-034 — The signing budget partitions by actor, not by address  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-023 · RSK-AUTH-011 (minted) |
| **Level / Type / Technique** | API · Security (positive) · Pairwise over (partition key ∈ {same actor, different actor}) × (source address ∈ {same, same}) — the second factor is held constant deliberately, because the claim is that it does **not** participate |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional) |
| **Role / Permission / Tenant** | `Ben Approver` and `Cara Signer`, both from the same client IP · `documents.sign` for both · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; both requests issued from one machine so `RemoteIpAddress` is identical |
| **Preconditions** | Both signers unlocked. Two fresh approved documents `SOP-0017` and `SOP-0018`, v`1.0`, both authored by `Ada Author`. `<t0>` recorded. |
| **Test Data** | `Ben Approver`: 10 publishes of `<DOC17>` with wrong document ids (as in `TC-AUTH-SEC-033`) to exhaust his budget, then an 11th. `Cara Signer`: one publish of `<DOC18>` with `{"password":"Second-Signer-2026!","pin":"0000"}` immediately after Ben's 11th, inside the same minute. |
| **Steps** | 1. Exhaust `Ben Approver`'s e-signature budget with 10 requests. 2. Send Ben's 11th and confirm `429`. 3. Immediately send `Cara Signer`'s single request from the **same** IP. 4. Read its status and `code`. 5. Send a request to a **globally** budgeted endpoint (`GET /api/users/directory`) from the same IP and confirm it is not throttled. |
| **Expected UI** | n/a — this case is driven from `curl.exe`, not the SPA. |
| **Expected API** | Step 2 → `429` with `Retry-After: 60`. Step 4 → **not** 429: `422` `SIG-001` (Cara has no PIN set), proving her partition is untouched — `ActorKey` reads the `sub` claim and falls back to the address only when unauthenticated (`RateLimiting.cs:101-102`). Step 5 → `200`, well inside the 300/min global per-IP budget. |
| **Expected DB** | `Cara Signer`'s `failed_login_attempts` = `1`; `Ben Approver`'s unchanged by the throttled requests. Zero signature rows for either document. |
| **Expected Audit** | One `ESIGN_FAILED` row with `actor='Cara Signer'`, `detail='bad-pin:DOC:<DOC18_N>'`. No row for Ben's throttled 11th. |
| **Expected Notification** | n/a — no notification policy subscribes to throttling or failed signings. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email IN ('approver@demo-lab.local','signer2@demo-lab.local');` Wait 60 seconds. |
| **Evidence** | Timestamped captures of Ben's 11th (429) and Cara's request (422) within the same minute · the two-row counter query |
| **Result / Defect** | Not Run · — |
| **Notes** | The design choice is deliberate and documented in code (`RateLimiting.cs:100` — "Signing ceremonies are authenticated — throttle the ACTOR, not the address"). The corollary worth stating in the RTM: a whole laboratory behind one NAT address is **not** collectively throttled for signing, unlike `/api/auth/*`, which is per-IP at 10/min and therefore *is* shared. |

---

#### TC-AUTH-SEC-035 — Direct-API publish without `documents.sign` is refused 403 and mints no signature  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-005, URS-020 · RSK-AUTH-017 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — §4.3 rules R2 and R3, exercised from a raw HTTP client with no SPA involvement |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | `Dev Viewer` (`viewer@demo-lab.local`, tier `Analyst`) holding a tenant role with `documents.view` but **not** `documents.sign` · required key `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; requests issued with `curl.exe` (PowerShell 5.1 drops manual `Cookie` headers) |
| **Preconditions** | `viewer@demo-lab.local` exists with password `Viewer-Pass-2026!` and a **correct** signature PIN `2244` set. A fresh approved document `SOP-0019` v`1.0` authored by `Ada Author`. Confirm the role's grants: `GET /api/auth/me/privileges` as the viewer returns a key list containing `documents.view` and **not** `documents.sign`. |
| **Test Data** | (a) no `Authorization` header at all; (b) the viewer's bearer token, body `{"password":"Viewer-Pass-2026!","pin":"2244"}` — **both components correct for that user** |
| **Steps** | 1. `curl.exe -i -X POST http://localhost:5080/api/documents/<DOC19>/publish -H "content-type: application/json" --data "@publish.json"` with no `Authorization`. 2. Repeat with the viewer's bearer token. 3. Read status, `www-authenticate` (case a) and the `code` extension for both. 4. `SELECT count(*) FROM audit.electronic_signature WHERE subject_ref='DOC:<DOC19_N>';` under bypass. 5. `SELECT failed_login_attempts FROM qams.user_account WHERE email='viewer@demo-lab.local';`. 6. `SELECT count(*) FROM audit.security_event WHERE occurred_at_utc > <t0>;`. 7. Repeat step 2 against the `/api/v1/documents/<DOC19>/publish` mirror. |
| **Expected UI** | The Publish form is not rendered for this user — `perms.can('documents.sign')` gates it client-side — which is precisely why the case is driven from `curl.exe`: the server must refuse independently of the hidden control. |
| **Expected API** | (a) → `401 Unauthorized`, `application/problem+json`, `code` = `AUTH-401` (`ProblemAuthorizationResultHandler.cs:18,42-44`). (b) → `403 Forbidden`, `application/problem+json`, `code` = **`AUTHZ-403`** from the MVC filter `[RequirePermission(Documents, Sign)]` (`RequirePermissionAttribute.cs:59`), refused **before** MediatR — so the command-level `[RequirePermissionPolicy]` at `DocumentCommands.cs:65-66` never runs. Step 7 → identical `403 AUTHZ-403` on the versioned mirror. |
| **Expected DB** | Zero signature rows for `DOC:<DOC19_N>`. `viewer@demo-lab.local`'s `failed_login_attempts` still `0` — the correct credentials in the body were never evaluated, so no lockout accrues from an unauthorised caller. Document status unchanged. |
| **Expected Audit** | Step 6 returns `0` new rows: an authorization refusal writes **no** security event, so a repeated bypass attempt leaves no `audit.security_event` trail. Assert this explicitly — it is the finding, not an omission in the case. |
| **Expected Notification** | n/a — no notification policy subscribes to authorization refusals. |
| **Cleanup** | n/a — nothing was written. |
| **Evidence** | Four `curl.exe -i` captures (anonymous, viewer, versioned mirror) · the zero signature count · the zero security-event count |
| **Result / Defect** | Not Run · — |
| **Notes** | Two independent gates exist and only the outer one fires here; to prove the inner one, batch D should drive a command with a **different** granted key and assert `403 AUTHZ-002` from `AuthorizationBehavior.cs:83`. The silence in `audit.security_event` on a 403 is worth a requirement conversation: Part 11 §11.300(d) expects attempts at unauthorised use to be detected **and reported** — see `GAP-AUTH-905`. |

---

#### TC-AUTH-SEC-036 — The PIN component cannot be omitted, blanked or null-ed out of the request  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-020 · RSK-AUTH-010 (minted) |
| **Level / Type / Technique** | API · Security (negative) · Multiple-Condition coverage of `string.IsNullOrWhiteSpace(signer.PinHash) \|\| !hasher.Verify(signer.PinHash, pin)` (`ComplianceLedgerServices.cs:111`) driven from the wire, with Equivalence Partitioning over the absent/empty/null/whitespace input classes |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; `curl.exe` with `--data "@body.json"` |
| **Preconditions** | `failed_login_attempts=0`. Four fresh approved documents `SOP-0020`…`SOP-0023`, v`1.0`. `PublishDocumentRequest` is `record PublishDocumentRequest(string Password, string Pin)` (`Contracts/DocumentControl/DocumentContracts.cs:12`) and there is **no** FluentValidation validator registered for `PublishDocumentCommand` (only `RejectDocumentVersionValidator` exists in that file, `DocumentCommands.cs:74-77`), so nothing rejects a blank PIN before the ceremony. |
| **Test Data** | (a) `{"password":"Approver-Pass-2026!"}` — `pin` key absent; (b) `{"password":"Approver-Pass-2026!","pin":""}`; (c) `{"password":"Approver-Pass-2026!","pin":null}`; (d) `{"password":"Approver-Pass-2026!","pin":"    "}` |
| **Steps** | 1. Send (a) to `<DOC20>`, (b) to `<DOC21>`, (c) to `<DOC22>`, (d) to `<DOC23>`. 2. Record each status and `code`. 3. `SELECT failed_login_attempts FROM qams.user_account WHERE email='approver@demo-lab.local';`. 4. `SELECT count(*) FROM audit.electronic_signature WHERE subject_ref IN ('DOC:<DOC20_N>','DOC:<DOC21_N>','DOC:<DOC22_N>','DOC:<DOC23_N>');` under bypass. 5. `SELECT event_type, detail FROM audit.security_event WHERE occurred_at_utc > <t0> ORDER BY occurred_at_utc;`. 6. `SELECT status FROM qams.controlled_document WHERE id IN (<the four ids>);`. |
| **Expected UI** | Not reachable from the SPA: `publishForm` marks `pin` `Validators.required` with `pattern(/^\d{4}$/)` and disables submit while invalid (`document-detail.component.ts:270,87`). The case exists precisely to prove the server does not rely on that. |
| **Expected API** | (a), (b), (d) → `422` `SIG-001` — an absent JSON key binds to `null` and an empty/whitespace string fails `hasher.Verify` against the stored PBKDF2 hash. (c) → `422` `SIG-001` as well. Assert **all four** return `SIG-001`, never `204` and never a `500`. If any returns `400`, that is a behaviour change (a validator was added) and must be reported, not absorbed. |
| **Expected DB** | `failed_login_attempts` = `4` (one increment per attempt — this consumes four of the five before lockout, so do **not** append a fifth attempt to this case). Zero signature rows across all four subjects. All four documents still at their pre-publish status. |
| **Expected Audit** | Four `ESIGN_FAILED` rows, `detail='bad-pin:DOC:<each>'`, `actor='Ben Approver'`. |
| **Expected Notification** | n/a — no notification policy subscribes to `ESIGN_FAILED`. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='approver@demo-lab.local';` |
| **Evidence** | Four `curl.exe -i` captures showing identical `SIG-001` · the counter at 4 · the four security-event rows |
| **Result / Defect** | Not Run · — |
| **Notes** | The point of the case is that the **second identification component cannot be skipped** (Part 11 §11.200(a)(1)): omitting it is treated as supplying a wrong one, with the same lockout cost. The absence of a `PublishDocumentValidator` is deliberate — validation of the PIN's shape would create a distinguishable "malformed PIN" oracle. Record that reasoning; do not file it as a gap. |

---

#### TC-AUTH-SEC-037 — The PIN never appears in any response, header, log line or change ledger  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-019, URS-020 · RSK-AUTH-015 (minted) |
| **Level / Type / Technique** | Integration · Security (negative, exhaustive sink sweep) · Data Flow — the PIN's every sink: HTTP response body, response headers, the canonical request log, `audit.field_change`, `audit.security_event`, and the `audit.audit_trail` payload |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional + log assertion) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign`, plus `compliance.view` for the ledger reads (or psql under bypass) · `demo-lab` |
| **Environment** | API `:5080` Development with JSON console logging captured to a file + live PostgreSQL `ntqams` |
| **Preconditions** | A **distinctive** PIN so a substring search is unambiguous: reset `Ben Approver`'s PIN to `7391` via `POST /api/auth/signature-pin` `{"pin":"7391"}`. A distinctive password `Zq7-Marker-Pass-2026!`. Two fresh approved documents `SOP-0024` (successful publish) and `SOP-0025` (failed publish). Start capturing API stdout to `%TEMP%\ntqms-dev\api.log` from timestamp `<t0>`. |
| **Test Data** | Success: `{"password":"Zq7-Marker-Pass-2026!","pin":"7391"}` on `<DOC24>`. Failure: `{"password":"Zq7-Marker-Pass-2026!","pin":"7392"}` on `<DOC25>`. Also `POST /api/auth/signature-pin` `{"pin":"7391"}`. |
| **Steps** | 1. Call `POST /api/auth/signature-pin` and capture the full response including headers. 2. Publish `<DOC24>` (success) and `<DOC25>` (failure), capturing both full responses with `curl.exe -i`. 3. `GET /api/documents/<DOC24>/signatures` and `GET /api/compliance/signatures?take=50`; capture both bodies. 4. `GET /api/compliance/security-events?take=50`; capture the body. 5. Search `%TEMP%\ntqms-dev\api.log` since `<t0>` for the literal strings `7391`, `7392` and `Zq7-Marker-Pass-2026!`. 6. Under bypass, `SELECT old_value, new_value, property FROM audit.field_change WHERE entity_type='UserAccount' AND occurred_at_utc > '<t0>';`. 7. `SELECT detail, actor FROM audit.security_event WHERE occurred_at_utc > '<t0>';`. 8. `SELECT payload FROM audit.audit_trail WHERE occurred_at_utc > '<t0>';`. 9. `SELECT pin_hash FROM qams.user_account WHERE email='approver@demo-lab.local';`. |
| **Expected UI** | The PIN input is a free-text field with `inputmode="numeric"` (`document-detail.component.ts:85`) — note that it is **not** `type="password"`, so the digits are visible on screen and to a shoulder-surfer. Record this observation; it is the only UI exposure and it is client-side by design choice, not a server leak. |
| **Expected API** | Step 1 → `204 No Content` with an empty body. Step 2 → `204` (empty) and `422` whose problem+json contains `title`, `code`, `traceId` and nothing resembling the PIN. Steps 3 and 4 → `200` bodies containing none of the marker strings. |
| **Expected DB** | Step 9: `pin_hash` is an ASP.NET Core Identity PBKDF2 composite (base64, ~84+ characters, beginning `AQAAAA`), containing neither `7391` as a decoded value nor any recoverable form of it. |
| **Expected Audit** | Step 5 → **zero** matches for all three markers: `ObservabilityMiddleware` logs only method, path, status, outcome, duration, operation, tenant, user and correlation (`ObservabilityMiddleware.cs:99-105`) and never a request body. Step 6 → the `PinHash` property row reads `old_value='«redacted»'`, `new_value='«redacted»'` — `FieldChangeInterceptor.Sensitive` matches the fragments `pin`, `hash` and `password` (`FieldChangeInterceptor.cs:34,95-99`). Step 7 → `detail` values are only `bad-pin:DOC:<DOC25_N>` — the reason token, never the value. Step 8 → no `audit_trail` payload contains a marker. |
| **Expected Notification** | n/a — no notification carries credential material; assert zero `qams.notification_dispatch` rows created since `<t0>`. |
| **Cleanup** | Reset the PIN to the fixture value `4417`; reset the password to `Approver-Pass-2026!` via `POST /api/users/{id}/reset-password`; `UPDATE qams.user_account SET failed_login_attempts=0 WHERE email='approver@demo-lab.local';` archive the log capture as evidence and delete the working copy. |
| **Evidence** | All captured HTTP bodies and headers · the `findstr /C:"7391"` output showing zero hits in the log · the `«redacted»` field-change row · the `pin_hash` prefix |
| **Result / Defect** | Not Run · — |
| **Notes** | The redaction list is fragment-based, not property-based (`FieldChangeInterceptor.cs:34`), so it also redacts any future property whose name contains `pin` — including a false positive such as `ShippingPincode`. Harmless here; note it so a future over-redaction is not read as a bug. The visible (non-masked) PIN input is a UI observation for batch F's a11y/UX charter, not a server finding. |

---

#### TC-AUTH-SEC-038 — The PIN is stored only as a salted PBKDF2 hash — two identical PINs produce different hashes  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-019, URS-020 · RSK-AUTH-015 (minted) |
| **Level / Type / Technique** | Integration · Security (positive) · Data Flow — plaintext PIN → `IPasswordHasher.Hash` → `user_account.pin_hash`, with the salt asserted by non-determinism |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | `Ben Approver` and `Cara Signer`, each setting their own PIN · n/a — `POST /api/auth/signature-pin` carries `[Authorize]` with no `[RequirePermission]` (`AuthController.cs:134-135`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Both accounts active and unlocked. |
| **Test Data** | The **same** PIN `4417` set three times: once by `Ben Approver`, once by `Cara Signer`, and a second time by `Ben Approver` |
| **Steps** | 1. As `Ben Approver`, `POST /api/auth/signature-pin` `{"pin":"4417"}`. 2. `SELECT pin_hash FROM qams.user_account WHERE email='approver@demo-lab.local';` → `H1`. 3. As `Cara Signer`, set the same PIN; read `H2`. 4. As `Ben Approver`, set `4417` again; read `H3`. 5. Compare `H1`, `H2`, `H3`. 6. Verify the ceremony still accepts `4417` for Ben by publishing a fresh approved document `SOP-0026`. 7. `SELECT column_name, data_type, character_maximum_length FROM information_schema.columns WHERE table_schema='qams' AND table_name='user_account' AND column_name='pin_hash';` and `SELECT conname FROM pg_constraint WHERE conrelid='qams.user_account'::regclass AND contype='c';`. |
| **Expected UI** | The PIN-setting screen confirms success with no echo of the value; there is no "show PIN" affordance and no way to read back a stored PIN. |
| **Expected API** | Steps 1, 3, 4 → `204 No Content` each. Step 6 → `204` with a signature minted, proving the newest hash verifies. |
| **Expected DB** | `H1`, `H2` and `H3` are **all different** despite the identical input — the hasher salts per call (`IdentityPasswordHasher`, `SecurityAdapters.cs:13-22`, reused for the PIN at `MfaAndPin.cs:79-80`). Each is a base64 composite beginning `AQAAAA`. Step 7 shows `pin_hash` is `text` with **no** `character_maximum_length` and the CHECK list contains **no** constraint on `pin_hash` — only `ck_user_account_role_domain`. |
| **Expected Audit** | Three `audit.field_change` rows for property `PinHash`, every `old_value` and `new_value` reading `«redacted»`. No security event — `SetPinHandler` writes none (`MfaAndPin.cs:73-82`), so setting or rotating a Part-11 identification component is **not** in the security ledger. |
| **Expected Notification** | n/a — no notification policy subscribes to PIN changes. |
| **Cleanup** | Leave Ben's PIN at `4417` (the fixture value); clear Cara's is **not possible** — the aggregate has no transition back to `Unset` (`UserAccount.cs:248-256` only sets). Note this in the run record. |
| **Evidence** | The three distinct `pin_hash` values · the successful publish of step 6 · the `information_schema` and `pg_constraint` output |
| **Result / Defect** | Not Run · — |
| **Notes** | Two as-built facts recorded here rather than as separate cases: *(a)* `GAP-AUTH-002` — the "exactly 4 digits" rule exists only in `SetPinValidator` (`MfaAndPin.cs:66`); the domain and the column impose nothing, which step 7 proves; *(b)* a PIN can be set and rotated but **never cleared**, and no security event marks either — worth a requirement conversation alongside `GAP-AUTH-002`. |

---

#### TC-AUTH-SEC-039 — Replaying a signing request with the same `Idempotency-Key` returns 204 without verifying credentials  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-020, URS-021 · RSK-AUTH-016 (minted) — new gap `GAP-AUTH-902` |
| **Level / Type / Technique** | Integration · Security (negative) · Path coverage of the `IdempotencyBehavior` short-circuit (`IdempotencyBehavior.cs:42-46`), which returns before `ValidationBehavior` and before the handler |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | `Ben Approver` · `documents.sign` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fresh approved document `SOP-0027` v`1.0`. `failed_login_attempts=0`. Zero rows in `qams.idempotency_record` for this actor and key. Choose the key `pub-sop0027-001` (≤ 100 characters, `HeaderIdempotencyKeyAccessor.cs:16`). |
| **Test Data** | Request 1: `POST /api/documents/<DOC27>/publish`, header `Idempotency-Key: pub-sop0027-001`, body `{"password":"Approver-Pass-2026!","pin":"4417"}`. Request 2: identical headers, body `{"password":"WRONG-Pass-0000!","pin":"0000"}` — **both components wrong**. Request 3: same wrong body, header `Idempotency-Key: pub-sop0027-002` (a **different** key). |
| **Steps** | 1. Send request 1; read status. 2. `SELECT count(*) FROM audit.electronic_signature WHERE subject_ref='DOC:<DOC27_N>';` under bypass. 3. `SELECT actor_id, idempotency_key, request_type, response_json FROM qams.idempotency_record WHERE idempotency_key='pub-sop0027-001';`. 4. Send request 2 (same key, wrong credentials); read status. 5. Re-run the signature count and read `failed_login_attempts`. 6. Send request 3 (new key, wrong credentials); read status and `code`. 7. `SELECT event_type, detail FROM audit.security_event WHERE occurred_at_utc > <t0> ORDER BY occurred_at_utc;`. |
| **Expected UI** | Not reachable from the SPA — the Angular client sends no `Idempotency-Key` on publish. Drive with `curl.exe`. |
| **Expected API** | Request 1 → `204 No Content`. Request 2 → **`204 No Content`** — the stored response is replayed and the handler is never invoked, so the wrong password and wrong PIN are **never evaluated**. Request 3 → `422` `SIG-002` (a new key means normal execution, and the password is checked first). |
| **Expected DB** | After step 2: one signature row. After step 5: **still one** — the replay mints no second signature, which is the intended protection. `failed_login_attempts` **still `0`** after request 2 — the replay costs the caller nothing and leaves no strike; it becomes `1` only after request 3. Step 3 shows one `qams.idempotency_record` row keyed `(actor_id, 'pub-sop0027-001', 'NT.QAMS.Application.DocumentControl.Commands.PublishDocumentCommand')` with `response_json` = `{}` (the serialised MediatR `Unit`), retained 24 hours (`IdempotencyRecord.Retention`). |
| **Expected Audit** | Step 7 shows exactly **one** `ESIGN_FAILED` row — from request 3 only. Request 2 produced no ledger entry at all: the replay is invisible to `audit.security_event`, `audit.field_change` and `audit.audit_trail`. |
| **Expected Notification** | n/a — the replay raises no domain event. |
| **Cleanup** | `DELETE FROM qams.idempotency_record WHERE idempotency_key IN ('pub-sop0027-001','pub-sop0027-002');` `UPDATE qams.user_account SET failed_login_attempts=0 WHERE email='approver@demo-lab.local';` The signature row is permanent. |
| **Evidence** | Three `curl.exe -i` captures showing `204 / 204 / 422` · the single-row signature count after the replay · the `idempotency_record` row · the one-row security-event result |
| **Result / Defect** | Not Run · — |
| **Notes** | `GAP-AUTH-902`. Replay protection working as designed (`IdempotencyBehavior` docs, `:7-16`) is **also** a credential-verification bypass on a Part-11 signing ceremony: for 24 hours, anyone holding the actor's bearer token and the used key gets a success response from an endpoint whose entire purpose is to re-authenticate. The exposure is bounded — the token already proves possession, no second signature is minted, and the key is scoped per actor (`EfIdempotencyStore.TryGetResponseAsync`) — but a signing ceremony should arguably be excluded from replay caching, or the cached response should be re-gated on the credentials. Acceptance criteria: *(a)* `PublishDocumentCommand` (and any future signing command) opts out of `IdempotencyBehavior`, **or** the behaviour re-verifies both identification components before replaying; *(b)* a functional case proves a replay with a wrong password returns `422 SIG-002`; *(c)* the decision is recorded in an ADR against Part 11 §11.200(a)(1). Do not "fix" the expectation to `422` before the code changes. |

---

#### TC-AUTH-SEC-040 — Signature and security ledgers are tenant-isolated on read by both RLS and the in-app filter  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-008, URS-016, URS-021 · RSK-AUTH-022 (minted) |
| **Level / Type / Technique** | Integration · Security (negative) · Decision Table over (reader tenant ∈ {A, B, none}) × (mechanism ∈ {API in-app filter, raw SQL under RLS}) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (integration test, alongside `SecurityEventRlsTests`) |
| **Role / Permission / Tenant** | Tenant A compliance viewer (`compliance.view`) and tenant B compliance viewer · `compliance.view` for `/api/compliance/*`; `GET /api/documents/{id}/signatures` requires only `[Authorize]` (no method or class `[RequirePermission]` on `DocumentsController`) · `demo-lab` (A) and `other-lab` (B) |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` with both tenants provisioned |
| **Preconditions** | Tenant A holds the signature from `TC-AUTH-INT-001` on `DOC:<DOCID_N>` and at least one `ESIGN_FAILED` event. Tenant B holds its own published document `<DOCB>` with one signature and one `ESIGN_FAILED`. Confirm the posture: `SELECT relrowsecurity, relforcerowsecurity FROM pg_class WHERE relname IN ('electronic_signature','security_event')` → `t,t` for both (measured 2026-08-01, one `tenant_isolation` policy each). |
| **Test Data** | Tenant A's bearer token; tenant B's document id `<DOCB>`; tenant B's tenant uuid `<TENANT-B>` |
| **Steps** | 1. As tenant A, `GET /api/compliance/signatures?take=200`; assert every element's `tenantId` equals A's uuid. 2. As tenant A, `GET /api/compliance/security-events?take=200`; assert the same and that **no** element has `tenantId: null`. 3. As tenant A, `GET /api/documents/<DOCB>/signatures`. 4. As tenant B, repeat steps 1–2 and assert the mirror-image result. 5. In psql, `SELECT set_config('app.current_tenant','<TENANT-A>',false); SELECT count(*) FROM audit.electronic_signature WHERE tenant_id='<TENANT-B>';`. 6. `SELECT set_config('app.current_tenant','',false); SELECT count(*) FROM audit.security_event;`. 7. `SELECT set_config('app.bypass_rls','on',false); SELECT count(*) FROM audit.electronic_signature;`. |
| **Expected UI** | The compliance screens show only the caller's own tenant's ledgers; there is no tenant selector. |
| **Expected API** | Steps 1, 2, 4 → `200` with strictly own-tenant rows. Step 3 → `200` with an **empty array**: the subject ref `DOC:<DOCB_N>` is real but `GetSignaturesForSubjectAsync` filters `s.TenantId == tenant.TenantId` (`ComplianceLedgerServices.cs:188`) and RLS hides the row regardless — the endpoint leaks neither the signature nor the existence of the document. |
| **Expected DB** | Step 5 → `0` (tenant A's GUC cannot see tenant B's signatures through the `tenant_isolation` `USING` clause). Step 6 → `0`, including the null-tenant pre-auth events, because an empty GUC makes `NULLIF(...)::uuid` null and `tenant_id = NULL` is never true. Step 7 → the full row count, proving the bypass switch is the only way through. |
| **Expected Audit** | Read-only case: assert `count(*)` of `audit.field_change` and `audit.audit_trail` is unchanged across the whole run. |
| **Expected Notification** | n/a — ledger reads raise no events. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','off',false);` before leaving the psql session; no data changes to revert. |
| **Evidence** | Four API captures with the `tenantId` distributions · the three psql counts (0 / 0 / full) · the `pg_class` posture query |
| **Result / Defect** | Not Run · — |
| **Notes** | Two mechanisms are asserted independently on purpose: `ComplianceLedgerStore` filters in-app (`:178-183`, `:200-203`) **and** FORCE RLS filters at the database. Step 6's result is the one to state carefully — null-tenant `security_event` rows (`REFRESH_INVALID`, `REFRESH_REUSE_DETECTED`, `LOGOUT`, pre-auth `LOGIN_FAILED`) are invisible to **every** tenant view and reachable only under `app.bypass_rls`, which is a deliberate platform-level design, not a data-loss defect. Contrast the relaxed WITH CHECK on the same tables, which still permits null-tenant **appends**. |

---

## Batch coverage note

**Covered.** Thirty-two cases against 21 source files read in this pass. The electronic-signature ceremony is covered end to end: the positive path with all eight persisted `SignatureRecord` fields asserted individually (`TC-AUTH-INT-001`), the content-hash binding and its lower-case-hex CHECK (`-002`), the composed `meaning`/`subject_ref` strings (`-003`), and every negative rule of decision table §4.3 — `SIG-002` wrong password (`-004`), `SIG-001` wrong PIN (`-005`) and unset PIN as the same output class (`-006`), the password-before-PIN evaluation order that completes multiple-condition coverage of the two guards (`-007`), `SIG-003` on a locked signer with the prefix-free `ESIGN_LOCKED` detail and no lock extension (`-008`), and `SIG-404` proven reachable only below HTTP (`-009`). The 5/30 throttle is covered at its boundary (`TC-AUTH-SEC-030`), shown to be shared with login (`-031`), and shown not to be reset by a successful signing (`-032`). The 10/min per-actor budget is covered at the permit boundary (`-033`) and proven to partition by actor rather than address (`-034`). `ESIGN_FAILED` and `ESIGN_LOCKED` emission is asserted row-by-row in `TC-AUTH-INT-004/005/008/014` including the display-name-versus-email actor inconsistency and the permanently null `ip_address`; the absence of an event on success is asserted in `-015`. Replay (`SEC-039`), concurrency (`INT-010`), mid-flight abort (`INT-011`), UI cancellation (`INT-012`), direct-API bypass (`SEC-035`) and PIN omission (`SEC-036`) are covered. PIN non-disclosure is swept across six sinks (`SEC-037`) and PIN storage is proven salted with no domain or column constraint (`SEC-038`). Append-only enforcement (`INT-013`) and cross-tenant ledger isolation (`SEC-040`) close the record-integrity side. User access review is covered open (`INT-016`), complete with the completion-instant count and conclusion trimming (`-017`), re-complete immutability including the validation-before-immutability ordering trap (`-018`), blank-conclusion partitions with `UAR-011` proven HTTP-unreachable (`-019`), the 3999/4000/4001 boundary (`-020`), and cross-tenant completion (`-021`).

**Not covered, and why.** (1) **`SIG-010` / `SIG-011`** — the assignment brief lists them as e-signature paths, but front-matter §0.2 proves they are raised only by `SigmaAssessment.cs:72,101` (six-sigma assessment immutability). Authoring an e-signature case for them would fabricate behaviour, so they are deliberately omitted and belong to module `MV`; this is the brief-versus-code contradiction the assignment required me to raise rather than reconcile — it is already registered as `GAP-AUTH-011`. (2) **A signing ceremony isolated from document control** does not exist (`GAP-AUTH-003`): every case above drags `SOP-####` preconditions (uploaded file, `Approved` version, non-author approver) through the document module, so a `DOC`-module regression can fail an AUTH case. (3) **The `access-reviews.view`-gates-a-write finding (`GAP-AUTH-004`) and the `ExternalAuditor` `[RequireInternalActor]` refusal** are authorization-matrix cases owned by batch D and are not duplicated here. (4) **PIN format boundaries** (3/4/5 digits, non-digit) against `POST /api/auth/signature-pin` belong to batch A's `TC-AUTH-BVA-*` block; `SEC-038` only asserts that no domain or column rule backs the validator. (5) **`TC-AUTH-INT-011` cannot be automated** — it needs a fault-injecting proxy to cut the connection in the window between two commits; it is authored as a manual case. (6) **A per-account access-review coverage case is impossible**: `AccountsReviewed` is one integer with no line items (`GAP-AUTH-008`), so no test can prove a given account was examined.

**Assignment-versus-front-matter ID conflict (raised, not silently reconciled).** My assignment reserves `TC-AUTH-SEC-030..` and `TC-AUTH-INT-001..` for batch E, but the front matter's ID-reservation table assigns `TC-AUTH-INT-001…030` to batch **C** and `TC-AUTH-SEC-001…040` to batch **D**, and reserves `DF`/`MCDC`/`OBS` kinds for batch E. I followed the assignment's explicit block (`SEC-030…040`, `INT-001…021`) because it is the narrower, orchestrator-issued allocation, and the front matter itself states the table is a *reservation* that the case files consume. **Action required before the traceability matrix is compiled:** batch C must not consume `TC-AUTH-INT-001…021`, and batch D must not consume `TC-AUTH-SEC-030…040`. Registered as `GAP-AUTH-900`.

**New gaps found in this pass.**

- **`GAP-AUTH-900` — ID-reservation conflict between the assignment blocks and the front matter's reservation table.** *Severity: Moderate (traceability).* Front matter §"ID reservation table" gives `INT-001…030` to batch C and `SEC-001…040` to batch D; batch E's assignment gives it `SEC-030..` and `INT-001..`. Two batches can therefore mint the same id. **Acceptance criteria:** *(a)* the front-matter table is amended so `TC-AUTH-INT-001…021` and `TC-AUTH-SEC-030…040` are attributed to `-cases-E.md`; *(b)* batches C and D are re-checked for collisions before the RTM is generated; *(c)* the RTM build fails on a duplicate `TC-AUTH-*` id.
- **`GAP-AUTH-901` — a signature can be committed for a publish that never completes.** *Severity: Major.* `ESignatureService.SignAsync` commits with its own `db.SaveChangesAsync` (`ComplianceLedgerServices.cs:129`) before `doc.Publish` and its separate `SaveChangesAsync` (`DocumentCommands.cs:159-160`); there is no `TransactionBehavior` and no `BeginTransaction` anywhere in `src/NT.QAMS.Application` or `src/NT.QAMS.WebApi` (verified by grep). Two concurrent publishes therefore produce two `audit.electronic_signature` rows and one `409 CONCURRENCY-409` (`TC-AUTH-INT-010`), and an aborted request can leave a signature for an unpublished document (`TC-AUTH-INT-011`). The rows cannot be removed — `signature_append_only` blocks DELETE. The comment at `DocumentCommands.cs:133-135` asserts the opposite guarantee, which holds only for the state and SoD gates it pre-validates. **Acceptance criteria:** *(a)* the signature mint and the state change occur in one transaction, so a failed publish leaves zero signature rows; *(b)* an integration case forces a `DbUpdateConcurrencyException` on the document and asserts `count(*)=0` on `audit.electronic_signature`; *(c)* if minting first is deliberate, the URS states that orphan signature records are expected and the compliance viewer distinguishes them. **Responsible role:** Lead Developer + Solution Architect.
- **`GAP-AUTH-902` — `Idempotency-Key` replay bypasses credential verification on the signing ceremony.** *Severity: Major.* `IdempotencyBehavior` short-circuits before `ValidationBehavior` and before the handler (`IdempotencyBehavior.cs:42-46`; pipeline order `Application/DependencyInjection.cs:20-24`), and `PublishDocumentCommand` is an `ICommand` with no opt-out. For the 24-hour retention window (`IdempotencyRecord.Retention`), a replay with a **wrong password and wrong PIN** returns `204`, writes no `ESIGN_FAILED`, and advances no lockout counter (`TC-AUTH-SEC-039`). No second signature is minted and the key is actor-scoped, which bounds the exposure — but Part 11 §11.200(a)(1) requires both components at each signing. **Acceptance criteria:** *(a)* signing commands opt out of replay caching, or the behaviour re-verifies both components before replaying; *(b)* a functional case proves a replay with a wrong password returns `422 SIG-002`; *(c)* an ADR records the decision. **Responsible role:** Lead Developer + Security Owner.
- **`GAP-AUTH-903` — no URS covers the missing-signer guard `SIG-404`.** *Severity: Minor (traceability).* `ComplianceLedgerServices.cs:93-94` throws `SIG-404` on an unknown `signerId`, but no URS-020…024 clause mentions it, and the branch is unreachable from HTTP because `ActiveSessionMiddleware` already 401s an absent account. It also writes no security event, so an attempted signing under a non-existent identity is unrecorded. **Acceptance criteria:** *(a)* the URS states the expected behaviour when a signer identity cannot be resolved; *(b)* the branch writes a security event or the URS records that it need not; *(c)* the branch has a unit or integration test so it is not deleted as dead code.
- **`GAP-AUTH-904` — the 429 rejection response is not `application/problem+json`.** *Severity: Minor.* `RateLimiting.Configure`'s `OnRejected` sets only the status and `Retry-After` (`RateLimiting.cs:55-61`) and writes no body, while API-003 gives every other error path a problem+json envelope with `code`, `traceId` and `X-Correlation-Id`. A client cannot key on a machine-readable code for throttling. **Acceptance criteria:** *(a)* `OnRejected` writes problem+json with a stable code (e.g. `RATE-LIMIT-429`) through the shared `ProblemResponse` writer; *(b)* a functional case asserts the media type and the code on the 11th e-signature attempt; *(c)* the SPA surfaces the retry interval.
- **`GAP-AUTH-905` — authorization refusals on the signing endpoint leave no security-event trail.** *Severity: Moderate.* A `403 AUTHZ-403` from `[RequirePermission(Documents, Sign)]` (`RequirePermissionAttribute.cs:59`) and a `401 AUTH-401` from `ProblemAuthorizationResultHandler.cs:42-44` write **no** `audit.security_event` row (`TC-AUTH-SEC-035`), so a sustained attempt to sign without the privilege is invisible to the ledger — only the canonical request log records it, and that log is not a Part-11 record. Part 11 §11.300(d) requires attempts at unauthorised use to be detected **and reported**. **Acceptance criteria:** *(a)* the HTTP authorization filter and the framework challenge/forbid handler write a security event with the attempted permission key and route; *(b)* an integration case proves a `403` on `POST /api/documents/{id}/publish` produces exactly one such row; *(c)* the event type is added to the security-event catalogue in the compliance documentation. **Responsible role:** Security Owner + Lead Developer.

*End of AUTH detailed test cases, batch E. Ids consumed: `TC-AUTH-SEC-030` … `TC-AUTH-SEC-040`, `TC-AUTH-INT-001` … `TC-AUTH-INT-021`.*
