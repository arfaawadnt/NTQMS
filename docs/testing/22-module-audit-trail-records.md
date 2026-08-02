# 22 — Immutable Audit History, Hash Chain, Field-Change Ledger, Security Events, Exports, Records / Archives / Legal Hold

**Module code:** `LEDGER`
**System under test:** NT.QMS v1.51.2, repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master`. Inspection date **2026-08-01**.
**Binding conventions:** `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md` (read in full before this file).

**ID range consumed by this file**

| Prefix | Range consumed | Count |
|---|---|---|
| `TC-LEDGER-INT-` | 001–026 | 26 |
| `TC-LEDGER-SEC-` | 001–024 | 24 |
| `TC-LEDGER-RLS-` | 001–010 | 10 |
| `TC-LEDGER-DF-` | 001–003 | 3 |
| `TC-LEDGER-PATH-` | 001–003 | 3 |
| `TC-LEDGER-API-` | 001–031 | 31 |
| `TC-LEDGER-UNIT-` | 001–010 | 10 |
| `TC-LEDGER-STATE-` | 001–005 | 5 |
| `TC-LEDGER-BVA-` | 001–005 | 5 |
| `TC-LEDGER-EP-` | 001–003 | 3 |
| `TC-LEDGER-DT-` | 001–003 | 3 |
| `TC-LEDGER-MCDC-` | 001–003 | 3 |
| `TC-LEDGER-LOOP-` | 001 | 1 |
| `TC-LEDGER-OBS-` | 001–002 | 2 |
| `TC-LEDGER-DR-` | 001–002 | 2 |
| `TC-LEDGER-PERF-` | 001–002 | 2 |
| `TC-LEDGER-E2E-` | 001–003 | 3 |
| `TC-LEDGER-UAT-` | 001–010 | 10 (Gherkin, §6) |
| `TC-LEDGER-EXPL-` | 001–005 | 5 (charters, §7) |
| `GAP-LEDGER-` | 001–028 | 28 |

**Detailed test cases in §5: 136.** All carry the full 28-field format.

**Completeness statement.** Complete: the four `audit.*` ledgers (`audit_trail`, `field_change`,
`electronic_signature`, `security_event`), their DDL/RLS/trigger protection, the hash chain and its
verification endpoint, the reason-for-change gate, the XLSX exports, the archive / legal-hold /
disposal aggregate, and the 22 mandated audit-event kinds. Deferred / not executable in this package:
(a) every case that depends on the least-privilege runtime role, because `deploy/harden-runtime-role.sql`
is **not applied** to the dev or CI database (measured below) — those cases are marked `[GD]` against
`GAP-LEDGER-011`; (b) privileged-database attack cases, which are authored but flagged
**ISOLATED ENVIRONMENT ONLY**; (c) source-IP, user-agent and correlation-id assertions, which have no
implementation to test (`GAP-LEDGER-001/002/003`).

---

## 0. Correction to ground truth

The conventions file §2 states: *"19,296 legacy null-tenant `field_change` rows are retained by design
(append-only)."* **Measured 2026-08-01 against the dev database `ntqams`, the count is 26,848, not 19,296.**

Proof (psql, `qams_app@localhost/ntqams`, after `SELECT set_config('app.bypass_rls','on',false);`):

```
SELECT count(*) FROM audit.field_change WHERE tenant_id IS NULL;   -- 26848
SELECT count(*) FROM audit.field_change;                           -- 54262
SELECT entity_type, count(*) FROM audit.field_change
 WHERE tenant_id IS NULL GROUP BY 1 ORDER BY 2 DESC LIMIT 5;
   RolePermission 21976 | LocalizedText 3951 | RefreshSession 393 | UserAccount 203 | ReferenceSample 80
```

The figure 19,296 is the number quoted in the source comment at
`src/NT.QAMS.Infrastructure/Persistence/Interceptors/FieldChangeInterceptor.cs:131` describing the
*privilege-detail* subset at the time that defect was fixed; it is not the total null-tenant population
and it is not current. **Consequence for authoring:** every case in this file that concerns the legacy
null-tenant rows is written against a **baseline captured at execution time**, never against a literal
count. See `TC-LEDGER-INT-025` and `TC-LEDGER-RLS-009`.

A second, smaller correction: the conventions file §2 says *"Chain hashes are computed over DB-read
(microsecond-truncated) timestamps."* This is **true but indirect** — `LedgerHash.Compute` is called with
the in-memory `occurredAt` argument (`src/NT.QAMS.Infrastructure/Compliance/ComplianceLedgerServices.cs:49`);
the truncation happens upstream because the value originates from `OutboxEvent.OccurredAtUtc` **read back
from PostgreSQL** by `ClaimDueBatchAsync` (`src/NT.QAMS.Infrastructure/Persistence/Outbox/OutboxProcessor.cs:171-205`)
before being handed to `AppendAsync` (`OutboxProcessor.cs:126-127`). The property therefore holds only for
the outbox path. `TC-LEDGER-INT-024` pins it.

---

## 1. Implementation inventory

Everything below was read in the cited file at the cited line, or measured against the live dev database
on 2026-08-01. Nothing here is inferred.

### 1.1 Persistence types (aggregates / plain records)

| Type | Table | Key | Fields | Citation |
|---|---|---|---|---|
| `AuditTrailEntry` (plain, append-only) | `audit.audit_trail` | `(tenant_id, id)` composite | `Id, TenantId, Sequence, EventId, EventType, Payload, OccurredAtUtc, PrevHash, EntryHash` | `src/NT.QAMS.Domain/ComplianceLedger/LedgerEntries.cs:9-20`; key `src/NT.QAMS.Infrastructure/Persistence/Configurations/ComplianceConfigurations.cs:20` |
| `SignatureRecord` (plain, append-only) | `audit.electronic_signature` | `(tenant_id, id)` | `Id, TenantId, SignerId, SignerDisplay, Meaning, SubjectRef, ContentHash, SignedAtUtc` | `LedgerEntries.cs:27-37`; `ComplianceConfigurations.cs:29-42` |
| `SecurityEvent` (plain, append-only) | `audit.security_event` | `id` (single column — tenant nullable) | `Id, TenantId?, EventType, Actor?, IpAddress?, Detail?, OccurredAtUtc` | `LedgerEntries.cs:43-52`; `ComplianceConfigurations.cs:44-60` |
| `FieldChangeRecord` (plain, append-only) | `audit.field_change` | `id` (single column — tenant nullable) | `Id, TenantId?, EntityType, EntityId, Action, Property?, OldValue?, NewValue?, ActorId?, Actor, Reason?, OccurredAtUtc` | `LedgerEntries.cs:61-78`; `ComplianceConfigurations.cs:63-77` |
| `AuditTrailReview` (aggregate) | `qams.audit_trail_review` | `(tenant_id, id)` | `ReviewRef, PeriodStart, PeriodEnd, Status, ReviewedBy?, CompletedAtUtc?, EventsReviewed?, FieldChangesReviewed?, AnomaliesFound?, Conclusion?` | `src/NT.QAMS.Domain/ComplianceLedger/AuditTrailReview.cs:16-76`; `ComplianceConfigurations.cs:79-91` |
| `ArchiveEntry` (aggregate) | `qams.archive_entry` | `(tenant_id, id)` | `ArchiveRef, SourceModule, SourceRef, SnapshotFileId?, RetentionClass, ArchivedOn, RetentionExpiry?, State, ArchivedBy, DisposalAuthorizedBy?, IsOnLegalHold, LegalHoldReason?, LegalHoldPlacedBy?` | `src/NT.QAMS.Domain/Records/ArchiveEntry.cs:16-164`; `src/NT.QAMS.Infrastructure/Persistence/Configurations/OperationsConfigurations.cs:8-23` |

Measured column types (`information_schema.columns`, 2026-08-01):
`audit.field_change.old_value` / `.new_value` / `.reason` are **`text`** (not `varchar(4000)`/`varchar(1000)`;
Hardening1 widened them). `audit.security_event.ip_address` is **`inet`**.
`audit.security_event.event_type` is **`varchar(40)`**. `audit.audit_trail.event_type` is **`varchar(400)`**,
`payload` is **`text`**, `prev_hash`/`entry_hash` are **`varchar(64)`**.
`audit.audit_trail.tenant_id` and `audit.electronic_signature.tenant_id` are **NOT NULL**;
`audit.field_change.tenant_id` and `audit.security_event.tenant_id` are **NULLABLE**.

### 1.2 Hash chain

- `LedgerHash.Genesis = "0000…0000"` (64 zeros) — `src/NT.QAMS.Infrastructure/Compliance/ComplianceLedgerServices.cs:14`.
- `LedgerHash.Compute(prevHash, sequence, eventId, eventType, payload, occurredAt)` builds the canonical
  string `"{prevHash}|{sequence}|{eventId}|{eventType}|{payload}|{occurredAt.UtcDateTime:O}"` and returns
  `Convert.ToHexStringLower(SHA256.HashData(UTF8(canonical)))` — `ComplianceLedgerServices.cs:16-20`.
  **The separator is `|` and the timestamp format is round-trip `O` (7 fractional digits).**
- `AuditTrailAppender.AppendAsync` — `ComplianceLedgerServices.cs:33-65`. Chain tenant is
  `tenantId ?? Guid.Empty` (line 36); the tip is cached per tenant in an in-memory
  `Dictionary<Guid,(long Seq,string Hash)>` (line 31) and, on cache miss, read as
  `ORDER BY Sequence DESC → FirstOrDefault` (lines 40-45); `sequence = tip.Seq + 1` (line 48).
- `ComplianceLedgerStore.VerifyChainAsync(tenantId, ct)` — `ComplianceLedgerServices.cs:209-231`. Loads the
  tenant's entries `ORDER BY Sequence`, walks from `Genesis`, and returns
  `(Ok, Verified, BrokenAtSequence)`; it breaks on the **first** row where `PrevHash != prev` **or**
  `EntryHash != recomputed` (line 221).
- **`VerifyChainAsync` never asserts sequence contiguity** — a deleted middle row that left the remaining
  hashes self-consistent is out of scope for it; see `GAP-LEDGER-014`.

### 1.3 Interceptors (the write path)

| Interceptor | Responsibility | Citation |
|---|---|---|
| `FieldChangeInterceptor` (`SaveChangesInterceptor`) | On `SavingChanges(Async)` walks `ChangeTracker.Entries()`; `Added→"Created"`, `Deleted→"Deleted"`, `Modified→` one row **per changed property**; adds them to the same `SaveChanges` | `.../Interceptors/FieldChangeInterceptor.cs:36-84` |
| — exclusion set | `FieldChangeRecord, AuditTrailEntry, SignatureRecord, SecurityEvent, OutboxEvent, KpiSnapshot, NotificationDispatch, RefCounter` | `FieldChangeInterceptor.cs:27-31` |
| — redaction | property-name fragments `password, secret, pin, hash, token` (case-insensitive `Contains`) → value replaced with `«redacted»` | `FieldChangeInterceptor.cs:34`, `:95-99`, `:119-120` |
| — reason stamping | `Reason = changeReason.Reason` on **every** row of the unit of work | `FieldChangeInterceptor.cs:115` |
| — tenant resolution order | `ITenantScoped.TenantId` (non-empty) → shadow `TenantId` property → `IOptionallyTenantScoped.TenantId` → `ICurrentTenant.TenantId` | `FieldChangeInterceptor.cs:135-155` |
| — entity-id rendering | primary-key properties **excluding** `TenantId`, joined with `\|`; `"(keyless)"` when no PK | `FieldChangeInterceptor.cs:167-182` |
| — value rendering | `DateTimeOffset→"O"`, `DateOnly→"yyyy-MM-dd"`, else `ToString()`, `null→null` | `FieldChangeInterceptor.cs:184-190` |
| `AuditStampInterceptor` | Stamps `IAuditable`: `Added→CreatedAtUtc/CreatedBy/CreatedByUserId`; `Modified→ModifiedAtUtc/ModifiedBy`. Actor = `ICurrentUser.DisplayName ?? "system"`, time = `IClock.UtcNow` | `.../Interceptors/AuditStampInterceptor.cs:32-57` |
| `TenantConnectionInterceptor` (`DbConnectionInterceptor`) | On **every** connection open runs `SELECT set_config('app.current_tenant',@tenant,false), set_config('app.bypass_rls',@bypass,false)`; nil UUID when unresolved, `'on'` only when `ICurrentTenant.IsElevated` | `.../Interceptors/TenantConnectionInterceptor.cs:23-58` |
| `OutboxInterceptor` | Drains `AggregateRoot.DomainEvents` into `qams.outbox_event` rows in the same transaction; tenant from `ITenantScoped` **or** `IOptionallyTenantScoped`; carries `TraceParent` | `.../Interceptors/OutboxInterceptor.cs:35-83` |

### 1.4 Ledger writers

- `SecurityEventLog.WriteAsync(eventType, tenantId, actor, detail, ct)` — `ComplianceLedgerServices.cs:70-83`.
  Adds a `SecurityEvent` and immediately calls `db.SaveChangesAsync(ct)` **on the shared request
  `AppDbContext`**. It sets `Id, TenantId, EventType, Actor, Detail, OccurredAtUtc` — **it never sets
  `IpAddress`** (`GAP-LEDGER-001`).
- `OutboxProcessor.ProcessBatchAsync` — `.../Outbox/OutboxProcessor.cs:97-162`. Elevates
  (`:102`), claims a batch with `FOR UPDATE SKIP LOCKED` + a 2-minute lease (`:171-205`), publishes,
  then `ledger.AppendAsync(row.TenantId, row.Id, row.EventType, row.Payload, row.OccurredAtUtc, ct)`
  (`:126-127`) and marks the row processed **in the same `SaveChangesAsync`** (`:128`, `:158`).
  On failure: `Attempts++`, exponential backoff (`:211-216`), dead-letter at `MaxAttempts = 5` (`:44`, `:140-147`).
- `ESignatureService.SignAsync` — `ComplianceLedgerServices.cs:90-131`. Writes `audit.electronic_signature`
  only after **both** password and PIN verify.

### 1.5 Domain / infrastructure error codes in scope

| Code | Meaning | HTTP | Citation |
|---|---|---|---|
| `SIG-404` | Signer not found | 404 | `ComplianceLedgerServices.cs:94` + `.../Middleware/DomainExceptionHandler.cs:69-74` |
| `SIG-003` | Account temporarily locked after repeated failed signings | 422 | `ComplianceLedgerServices.cs:101` |
| `SIG-002` | Account password incorrect | 422 | `ComplianceLedgerServices.cs:108` |
| `SIG-001` | E-signature PIN not set or incorrect | 422 | `ComplianceLedgerServices.cs:114` |
| `CHANGE-REASON-REQUIRED` | DELETE without `X-Change-Reason` | **400** | `.../Middleware/RequestIdentity.cs:149-156` |
| `ARC-001` | Source module and reference are required | 422 | `Records/ArchiveEntry.cs:54` |
| `ARC-002` | An immutable content snapshot is required to archive a record | 422 | `Records/ArchiveEntry.cs:59` |
| `ARC-010` | A disposed record cannot be retrieved | **409** (`InvalidStateTransitionException`) | `Records/ArchiveEntry.cs:113` |
| `ARC-011` | Only a retrieved record can be returned | 409 | `Records/ArchiveEntry.cs:123` |
| `ARC-012` | Record is already disposed | 409 | `Records/ArchiveEntry.cs:133` |
| `ARC-013` | Permanent-retention records cannot be disposed | 422 | `Records/ArchiveEntry.cs:144` |
| `ARC-014` | Retention period runs until `<date>`; disposal not yet permitted | 422 | `Records/ArchiveEntry.cs:149-150` |
| `ARC-015` | Record is under legal hold and cannot be disposed | 422 | `Records/ArchiveEntry.cs:138-139` |
| `ARC-020` | `<module> <ref>` is already archived | 422 | `.../Application/Records/RecordsSlice.cs:41` |
| `ARC-030` | A reason is required to place a legal hold | 422 | `Records/ArchiveEntry.cs:81` |
| `ARC-031` | A disposed record cannot be placed on legal hold | 409 | `Records/ArchiveEntry.cs:86` |
| `ARC-032` | The record is not on legal hold | 409 | `Records/ArchiveEntry.cs:100` |
| `ARC-404` | Archive entry not found | 404 | `RecordsSlice.cs:81` |
| `FILE-404` | Snapshot file not found | 404 | `RecordsSlice.cs:46` |
| `ATR-001` | Review period end must not precede its start | 422 | `AuditTrailReview.cs:37` |
| `ATR-010` | The review is already completed and immutable | 409 | `AuditTrailReview.cs:55` |
| `ATR-011` | A written conclusion is required | 422 | `AuditTrailReview.cs:60` |
| `ATR-404` | Audit-trail review not found | 404 | `.../ComplianceLedger/AuditTrailReviewSlice.cs:57` |
| `TENANT-000` | A tenant context is required | 422 | `RecordsSlice.cs:34`, `AuditTrailReviewSlice.cs:37` |
| `AUTH-003` | An authenticated user is required | **401** | `RecordsSlice.cs:35` + `DomainExceptionHandler.cs:54-59` |
| `AUTHZ-403` | Caller lacks the required privilege | 403 | `.../Authorization/RequirePermissionAttribute.cs:53-60` |
| PostgreSQL `P0001` | `audit ledgers are append-only` | n/a (DB) | `Migrations/20260721232300_ComplianceAndAuth.cs:154-158`; **measured** |
| PostgreSQL `42501` | RLS `WITH CHECK` refusal | n/a (DB) | `tests/NT.QAMS.IntegrationTests/SecurityEventRlsTests.cs:85` |
| PostgreSQL `check_violation` | signed/approved record is immutable | n/a (DB) | `Migrations/20260726084134_SignedRecordImmutability.cs:46-49` |

HTTP mapping rules: `InvalidStateTransitionException → 409`; `DomainException` starting `AUTH- → 401`;
starting `AUTHZ- → 403`; ending `-404 → 404`; otherwise **422**; `FluentValidation.ValidationException → 400`;
`DbUpdateConcurrencyException → 409 CONCURRENCY-409`. All as `application/problem+json`.
Source: `.../Middleware/DomainExceptionHandler.cs:26-82`.

### 1.6 Endpoints in scope

| Method + route | Gate | Handler | Citation |
|---|---|---|---|
| `GET /api/compliance/audit-trail?subject=&take=` | class `[Authorize]` + `[RequirePermission(compliance, View)]` | `GetAuditTrailQuery` | `.../Controllers/ComplianceController.cs:17-26` |
| `GET /api/compliance/field-changes?entityId=&take=` | `compliance.view` | `GetFieldChangesQuery` | `ComplianceController.cs:28-31` |
| `GET /api/compliance/signatures?take=` | `compliance.view` | `GetSignatureLogQuery` | `ComplianceController.cs:33-35` |
| `GET /api/compliance/security-events?take=` | `compliance.view` | `GetSecurityEventsQuery` | `ComplianceController.cs:37-39` |
| `GET /api/compliance/audit-trail-reviews` | `compliance.view` | `GetAuditTrailReviewsQuery` | `ComplianceController.cs:41-43` |
| `POST /api/compliance/audit-trail-reviews` | `compliance.create` | `OpenAuditTrailReviewCommand` | `ComplianceController.cs:45-49` |
| `POST /api/compliance/audit-trail-reviews/{id}/complete` | `compliance.approve` | `CompleteAuditTrailReviewCommand` → **204** | `ComplianceController.cs:51-58` |
| `GET /api/compliance/chain-verification` | `compliance.view` | `VerifyChainQuery`; **400** when the `tenant_id` claim is absent/unparseable | `ComplianceController.cs:60-70` |
| `GET /api/exports/nonconformances.xlsx` | class `[Authorize]` **only — no `[RequirePermission]`** | pages the whole NC register | `.../Controllers/ExportsController.cs:30-58` |
| `GET /api/exports/audit-trail.xlsx?take=1000` | `[RequirePermission(compliance, Export)]` | 3 sheets: Integrity Attestation / Event Trail / Field-Level Changes | `ExportsController.cs:60-103` |
| `GET /api/exports/signatures.xlsx?take=1000` | `[RequirePermission(compliance, Export)]` | 1 sheet: Signatures | `ExportsController.cs:105-123` |
| `GET /api/exports/review-pack/{reviewId}.pdf` | `[RequirePermission(reviews, Export)]` | QuestPDF | `ExportsController.cs:125-158` |
| `GET /api/archives?state=&page=&pageSize=` | class `[Authorize]` only | `GetArchivesQuery` (paged envelope) | `.../Controllers/OperationsControllers.cs:19-24` |
| `POST /api/archives` | class `[Authorize]` only; command `[RequireInternalActor]` | `ArchiveRecordCommand` → `{ id }` | `OperationsControllers.cs:26-30` |
| `POST /api/archives/{id}/retrieve` | class `[Authorize]` only | `RetrieveRecordCommand` → **204** | `OperationsControllers.cs:32-37` |
| `POST /api/archives/{id}/return` | class `[Authorize]` only | `ReturnRecordCommand` → **204** | `OperationsControllers.cs:39-44` |
| `POST /api/archives/{id}/dispose` | `[RequirePermission(records, Void)]` | `DisposeRecordCommand` → **204** | `OperationsControllers.cs:46-52` |
| `POST /api/archives/{id}/legal-hold` | `[RequirePermission(records, Void)]` | **reason in the POST BODY** | `OperationsControllers.cs:54-60` |
| `DELETE /api/archives/{id}/legal-hold` | `[RequirePermission(records, Void)]` | **DELETE → `X-Change-Reason` header mandatory** | `OperationsControllers.cs:62-68` |
| `POST /api/files` | class `[Authorize]` only | upload; **writes no security event** | `.../Controllers/FilesController.cs:22-57` |
| `GET /api/files/{id}` | class `[Authorize]` only | download; **writes no security event** | `FilesController.cs:59-72` |

All routes are dual-exposed at `/api/v{version}/…` (`Asp.Versioning.Mvc`). Read `take` is clamped
`Math.Clamp(take, 1, 1000)` in every ledger query handler
(`.../Application/ComplianceLedger/ComplianceQueries.cs:15, 24, 33, 72`).

### 1.7 Database protection (measured 2026-08-01)

**Append-only triggers** — `pg_trigger` join, schema `audit`, all four present and `tgenabled = 'O'`:

| Table | Trigger | Definition |
|---|---|---|
| `audit.audit_trail` | `audit_trail_append_only` | `BEFORE DELETE OR UPDATE … FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation()` |
| `audit.electronic_signature` | `signature_append_only` | same |
| `audit.security_event` | `security_event_append_only` | same |
| `audit.field_change` | `field_change_append_only` | same |

`audit.reject_mutation()` is `RAISE EXCEPTION 'audit ledgers are append-only'` — SQLSTATE **P0001**
(`Migrations/20260721232300_ComplianceAndAuth.cs:154-158`, `…_FieldChangeLedger.cs:53-54`).
**Measured**: `UPDATE audit.field_change …` and `DELETE FROM audit.audit_trail …`, executed as the
table owner `qams_app` with `app.bypass_rls = 'on'`, both fail with
`ERROR: P0001: audit ledgers are append-only`.

**RLS** — `pg_class.relrowsecurity = t` **and** `relforcerowsecurity = t` on all four audit tables and on
`qams.archive_entry`. Policy `tenant_isolation`, `cmd = ALL`, identical on all four:

```
USING      (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
            OR current_setting('app.bypass_rls', true) = 'on')
WITH CHECK (tenant_id IS NULL
            OR tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
            OR current_setting('app.bypass_rls', true) = 'on')
```

Provenance: `Migrations/20260726081443_ActivateForcedTenantRls.cs:24-48` (FORCE + bypass clause),
`…_20260726103650_RelaxAuditRlsWriteCheck.cs:19-46` (null-tenant write allowance, `audit` schema only),
`…_20260731181845_Hardening2_RlsGapClosure.cs:17-28` (`audit.security_event`, closed in v1.51.2).

**Grants — the environment gap.** `deploy/harden-runtime-role.sql` specifies
`GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA audit TO qams_app` and
`REVOKE DELETE ON ALL TABLES IN SCHEMA qams, audit, read, saas, ref FROM qams_app`, plus
`ALTER ROLE qams_app NOSUPERUSER NOBYPASSRLS`. **Measured on the dev database, `qams_app` holds
`SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER` on all four `audit` tables** — the
script has not been applied and `qams_app` owns the schema. Every "no UPDATE / no DELETE permission"
case is therefore `[GD]` against `GAP-LEDGER-011` and must run in a hardened environment.

**Ledger population (dev, 2026-08-01)** — the execution baseline:

| Ledger | Rows | Notes |
|---|---|---|
| `audit.audit_trail` | 458 | 28 distinct `tenant_id`; `sequence` 1…95 |
| `audit.field_change` | 54,262 | `Created` 53,137 · `Modified` 1,113 · `Deleted` 12; `reason IS NOT NULL` on **14** rows; 26,848 null-tenant |
| `audit.security_event` | 493 | `ip_address IS NOT NULL` on **0** rows |
| `audit.electronic_signature` | 2 | |

Security-event types actually present: `LOGIN_SUCCESS` 351, `LOGIN_FAILED` 89, `LOGOUT` 24,
`RECORD_EXPORTED` 10, `REFRESH_INVALID` 6, `REFRESH_REUSE_DETECTED` 6, `LOGIN_MFA_ENROLL_REQUIRED` 4,
`PASSWORD_CHANGED` 1 (plus 2 integration-test probe rows).

### 1.8 Security-event catalogue — what the code can actually emit

| Event type | Emitted by | Citation |
|---|---|---|
| `LOGIN_SUCCESS` | `LoginHandler` | `.../Application/IdentityAccess/Commands/Login.cs:139` |
| `LOGIN_MFA_ENROLL_REQUIRED` | `LoginHandler` (same ternary) | `Login.cs:139` |
| `LOGIN_MFA_REQUIRED` | `LoginHandler` | `Login.cs:103` |
| `LOGIN_FAILED` | `LoginHandler` | `Login.cs:152` |
| `PASSWORD_CHANGED` | `ChangePasswordHandler` | `Login.cs:235` |
| `MFA_ENABLED` | `ConfirmMfaHandler` | `.../IdentityAccess/Commands/MfaAndPin.cs:54` |
| `REFRESH_INVALID` | `RefreshTokenHandler` | `.../IdentityAccess/Commands/RefreshSessions.cs:97` |
| `REFRESH_REUSE_DETECTED` | `RefreshTokenHandler` (family revocation) | `RefreshSessions.cs:108` |
| `LOGOUT` | `LogoutHandler` | `RefreshSessions.cs:177` |
| `ESIGN_LOCKED` | `ESignatureService` | `ComplianceLedgerServices.cs:100` |
| `ESIGN_FAILED` | `ESignatureService.RecordFailureAsync` | `ComplianceLedgerServices.cs:142` |
| `RECORD_EXPORTED` | `ExportsController.LogExportAsync` | `ExportsController.cs:168-169` |

**Exhaustive.** `grep -rn "WriteAsync(\"" src --include=*.cs` returns exactly these 12 call sites.
There is **no** `ACCOUNT_LOCKED`, no permission/role-change event, no file upload/download event, no
SoD-violation event, no cross-tenant-attempt event and no background-escalation event
(`GAP-LEDGER-004/005/006/007/008/009/010`).

### 1.9 Reason-for-change

- `ChangeReasonMiddleware` — `.../Middleware/RequestIdentity.cs:143-161`. Reads
  `Request.Headers["X-Change-Reason"]`. If `HttpMethods.IsDelete(...)` **and** the header is
  null/empty/whitespace → `ProblemResponse.WriteAsync(400, "A reason is required for this change.",
  "CHANGE-REASON-REQUIRED")` and the pipeline **short-circuits** (no handler runs). Otherwise
  `reasonSetter.Set(reason)` runs for **every** method — a non-DELETE that supplies the header is
  honoured (`:158`).
- The scoped reason lands on **every** `FieldChangeRecord` produced by that unit of work
  (`FieldChangeInterceptor.cs:115`), pinned by
  `tests/NT.QAMS.WebApi.FunctionalTests/FieldChangeInterceptorTests.cs:113-127`.
- Legal-hold **placement** carries its Part-11 reason in the **POST body**
  (`PlaceLegalHoldRequest.Reason` → `PlaceLegalHoldCommand`, `OperationsControllers.cs:54-60`,
  `RecordsSlice.cs:67`, validator `RecordsSlice.cs:71-75`), because the route is a POST, not a DELETE.
  Legal-hold **release** is `DELETE /api/archives/{id}/legal-hold` and therefore **does** require the
  header (`OperationsControllers.cs:62-68`).
- Frontend: `frontend/src/app/core/change-reason.interceptor.ts` + `change-reason-dialog.component.ts`
  (accessible dialog); an already-present header is not overwritten
  (`change-reason.interceptor.spec.ts:84`).

### 1.10 Exports

- `ExportService.ToXlsx` (ClosedXML 0.105.0) — `.../Infrastructure/Exports/ExportService.cs:22-61`.
  One worksheet per `ExportTable`; **row 1** = pack title (bold, 14pt); **row 2** =
  `"Tenant: {TenantName} · Generated by {GeneratedBy} at {GeneratedAtUtc:u} · {table.Title}"`;
  **row 4** = header (bold, white on `#3B4658`); data from **row 5**; `FreezeRows(4)`.
  Sheet name = `"{index:00} {title truncated to 27 chars}"`, `\ / * [ ] : ?` stripped (`:134-140`).
- `ExportsController.AuditTrail` — `ExportsController.cs:60-103`. Sheet 1 **Integrity Attestation**:
  columns `Chain integrity | Entries verified | First break at sequence | Entries in this export`, values
  `"OK — chain intact"` or `"BROKEN"`, `VerifiedEntries`, `BrokenAtSequence ?? "—"`, `entries.Count`
  (`:74-82`). Sheet 2 **Event Trail**: `Seq | Occurred (UTC) | Event | Payload | Entry Hash`. Sheet 3
  **Field-Level Changes**: `Occurred (UTC) | Entity | Record | Action | Field | From | To | Actor | Reason`.
  File name `audit-trail-{yyyyMMdd-HHmm}.xlsx`, content type
  `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.
- `ExportsController.SignatureManifest` — `ExportsController.cs:105-123`. Single sheet **Signatures**:
  `Signed (UTC) | Signer | Meaning | Subject | Content Hash`; file `signature-manifest-{yyyyMMdd-HHmm}.xlsx`.
- Every export calls `LogExportAsync` → `SecurityEventLog.WriteAsync("RECORD_EXPORTED", tenantId,
  user.DisplayName, what, ct)` **before** returning the file (`ExportsController.cs:54, 99, 119, 155, 168-169`).
- **The NC register pages every page of the API-004 envelope** (`ExportsController.cs:35-43`); the
  audit-trail and signature exports do **not** — they pass `take` straight into a handler that clamps at
  1000 (`GAP-LEDGER-023`).

### 1.11 Archives / retention / legal hold

State machine `ArchiveState = Archived | Retrieved | Disposed` (`ArchiveEntry.cs:8`).
Retention `RetentionClass = FiveYears | TenYears | Permanent` (`ArchiveEntry.cs:6`); expiry
`FiveYears → archivedOn.AddYears(5)`, `TenYears → +10`, `Permanent → null` (`ArchiveEntry.cs:158-163`).
`ArchiveEntry.Archive(...)` requires a non-empty `sourceModule`/`sourceRef` (`ARC-001`) **and** a
non-empty `snapshotFileId` (`ARC-002`, `ArchiveEntry.cs:57-60`) — F-14 / Part 11 §11.10(c).
`AuthorizeDisposal(actorId, asOf)` evaluates, **in this order**: already-disposed (`ARC-012`) →
legal hold (`ARC-015`) → permanent-or-null-expiry (`ARC-013`) → expiry in the future (`ARC-014`)
(`ArchiveEntry.cs:129-156`). Legal hold **outranks retention expiry**: `ARC-015` fires before the
expiry test is ever reached.

### 1.12 Existing automated coverage this package must not duplicate

`tests/NT.QAMS.IntegrationTests/SecurityEventRlsTests.cs` (4 tests, `[Collection("real-postgres")]`,
`SkippableFact`), `.../RuntimeRolePrivilegeTests.cs` (3), `.../SignedRecordImmutabilityTests.cs`,
`.../RlsTenantIsolationTests.cs`, `tests/NT.QAMS.WebApi.FunctionalTests/FieldChangeInterceptorTests.cs`
(7 tests, in-memory provider), `tests/NT.QAMS.Domain.UnitTests/ComplianceLedger/AuditTrailReviewTests.cs`,
`tests/NT.QAMS.Domain.UnitTests/Operations/RecordsAndSlaTests.cs`.

---

## 2. Divergences from the commissioning brief

| # | Brief / URS expectation | As-built reality | Evidence | Disposition |
|---|---|---|---|---|
| D-1 | Audit rows carry the **source IP** | `audit.security_event.ip_address` exists (`inet`) but `SecurityEventLog.WriteAsync` never assigns it; measured 0 of 493 rows populated | `ComplianceLedgerServices.cs:73-81`; measured | `GAP-LEDGER-001` |
| D-2 | Audit rows carry the **user agent** | No user-agent column on any of the four ledgers | `LedgerEntries.cs:9-78`; `information_schema.columns` | `GAP-LEDGER-002` |
| D-3 | Audit rows carry the **correlation id** | Correlation id exists on the response header and in logs only; never persisted | `.../Middleware/ObservabilityMiddleware.cs:26-97` | `GAP-LEDGER-003` |
| D-4 | An audit event exists for **account lock** | `UserAccount.RegisterFailedLogin` raises the domain event `UserLockedOut`; no `ACCOUNT_LOCKED` security event | `src/NT.QAMS.Domain/IdentityAccess/UserAccount.cs:209-216`; §1.8 | `GAP-LEDGER-004`; alternative mechanism tested `[ID]` |
| D-5 | An audit event exists for **permission change / role change** | No security event; covered by `audit.field_change` rows on `Role`/`RolePermission`/`UserAccount` plus `audit.audit_trail` `RoleCreated` / `RolePermissionsChanged` / `UserRoleAssigned` (measured 227/4/68 rows) | §1.8; measured | `GAP-LEDGER-005`; alternative mechanism tested `[ID]` |
| D-6 | An audit event exists for **file upload / download** | Neither `FilesController` action logs anything | `FilesController.cs:22-72` | `GAP-LEDGER-006` |
| D-7 | An audit event exists for an **SoD violation** | SoD breaches surface as HTTP 403 `AUTHZ-*` or 422 `SOD-*`; nothing is written to any ledger | `RequirePermissionAttribute.cs:53-60`; `DomainExceptionHandler.cs:63-68` | `GAP-LEDGER-007` |
| D-8 | An audit event exists for a **cross-tenant access attempt** | RLS returns an empty result set silently; no refusal is recorded | `ActivateForcedTenantRls.cs:24-48` | `GAP-LEDGER-008` |
| D-9 | An audit event exists for **background escalation** | `EscalationTriggered` produces a `NotificationDispatch` keyed `SLA_ESCALATED`, and an `audit.audit_trail` row **only if** the outbox row is processed successfully | `.../Application/Notifications/NotificationPolicies.cs:42,116`; `OutboxProcessor.cs:126` | `GAP-LEDGER-009`; alternative mechanism tested `[ID]` |
| D-10 | **Runtime role has no UPDATE / DELETE** on `audit.*` (URS-014) | Specified in `deploy/harden-runtime-role.sql` but **not applied** in dev/CI: `qams_app` owns the schema and holds `UPDATE, DELETE, TRUNCATE` | measured `information_schema.table_privileges` | `GAP-LEDGER-011` — cases are `[GD]` |
| D-11 | Ledger cannot be **TRUNCATE**d | The row-level `BEFORE UPDATE OR DELETE` trigger does not fire for `TRUNCATE`; no statement-level guard, no `REVOKE TRUNCATE` in the hardening script | `ComplianceAndAuth.cs:160-165`; `harden-runtime-role.sql` (no TRUNCATE clause) | `GAP-LEDGER-012` |
| D-12 | Triggers cannot be **disabled** | The owner can `ALTER TABLE … DISABLE TRIGGER`; no PostgreSQL event trigger prevents it | schema inspection | `GAP-LEDGER-013` |
| D-13 | **Chronological `Sequence` ordering** is what a reader sees | `GetTrailAsync` orders `OrderByDescending(e => e.OccurredAtUtc)`, not by `Sequence`; the exported Event Trail sheet inherits that order | `ComplianceLedgerServices.cs:172` | `GAP-LEDGER-018` |
| D-14 | Chain verification detects a **missing entry** | `VerifyChainAsync` verifies hash linkage only; it never checks that `Sequence` is contiguous from 1 | `ComplianceLedgerServices.cs:209-231` | `GAP-LEDGER-014` |
| D-15 | Chain verification runs **nightly** | The XML doc says "Nightly (or on-demand)" but no `BackgroundService` invokes `VerifyChainQuery`; only the on-demand endpoint and the export attestation call it | `.../Application/ComplianceLedger/ComplianceQueries.cs:36`; `grep VerifyChainQuery` → 2 call sites | `GAP-LEDGER-015` |
| D-16 | Export is an **accurate and complete copy** (URS-045, Part 11 §11.10(b)) | `audit-trail.xlsx` and `signatures.xlsx` default `take = 1000` and the handler clamps at 1000, so a tenant with >1000 entries silently exports a truncated copy **while the Integrity Attestation reports the whole chain as verified** | `ExportsController.cs:62,107`; `ComplianceQueries.cs:15,24` | `GAP-LEDGER-023` (High) |
| D-17 | Every export is privilege-gated | `GET /api/exports/nonconformances.xlsx` carries **no** `[RequirePermission]` — any authenticated actor may draw the full NC register | `ExportsController.cs:30-31` | `GAP-LEDGER-024` |
| D-18 | Archiving is privilege-gated | `POST /api/archives`, `/retrieve`, `/return` carry no `[RequirePermission]`; only class `[Authorize]` + command `[RequireInternalActor]` | `OperationsControllers.cs:26-44` | `GAP-LEDGER-025` |
| D-19 | Every event reaches the audit trail | A dead-lettered outbox row (5 failed attempts) is never chained — a domain event with **no** `audit_trail` entry, and nothing records the omission in the ledger | `OutboxProcessor.cs:140-147` | `GAP-LEDGER-017` |
| D-20 | `ARC-030` ("a reason is required to place a legal hold") is reachable | `PlaceLegalHoldValidator.RuleFor(x => x.Reason).NotEmpty()` returns **400** before the aggregate is touched, so `ARC-030`/422 is unreachable through HTTP | `RecordsSlice.cs:71-75` | `GAP-LEDGER-020` |
| D-21 | A reason is required for every destructive act | The gate keys on the HTTP **method**, not the semantics: `POST /api/archives/{id}/dispose` destroys a regulated record and needs no reason | `RequestIdentity.cs:149` | `GAP-LEDGER-021` |
| D-22 | Archive rows are immutable once disposed | `qams.archive_entry` is **not** in the `SignedRecordImmutability` frozen list; a `Disposed` row remains `UPDATE`-able at the database | `SignedRecordImmutability.cs:14-29` | `GAP-LEDGER-026` |
| D-23 | The exported workbook is itself tamper-evident | The Integrity Attestation is a plain worksheet; the `.xlsx` carries no signature, no checksum, no protection | `ExportService.cs:22-61` | `GAP-LEDGER-027` |
| D-24 | Multiple API replicas are safe for the ledger | The chain tip is cached in-process (`AuditTrailAppender._tips`) and only ADR-0001's single-replica pin prevents two processors forking a tenant chain; `SKIP LOCKED` guards the outbox **row**, not the chain | `ComplianceLedgerServices.cs:31`; `OutboxProcessor.cs:171-205` | `GAP-LEDGER-028` |
| D-25 | Ledger writes never flush unrelated work | `SecurityEventLog.WriteAsync` calls `SaveChangesAsync` on the **shared** request `DbContext`, committing every other tracked change in flight | `ComplianceLedgerServices.cs:82` | `GAP-LEDGER-016` |
| D-26 | `audit_trail` records create/update/delete of records | `audit_trail` records **domain events**, not row changes; per-row create/update/delete lives in `audit.field_change`. The two ledgers are not interchangeable | `OutboxProcessor.cs:126`; `FieldChangeInterceptor.cs:66-77` | Documented, not a defect — but every case must name the right ledger |
| D-27 | Legacy null-tenant rows are readable by their tenant | The `USING` clause requires `tenant_id = GUC`; a `NULL` tenant matches nothing, so the 26,848 legacy rows are visible **only** under `app.bypass_rls='on'` | `RelaxAuditRlsWriteCheck.cs:33-35`; measured | `GAP-LEDGER-022` |
| D-28 | Brief's "4-digit PIN" boundary | Ground truth §2: no digit-length constraint exists in the domain. Not re-litigated here | conventions §2 | Owned by module `AUTH` |

---

## 3. State-transition matrix

### 3.1 `ArchiveEntry.State` (`src/NT.QAMS.Domain/Records/ArchiveEntry.cs:109-156`)

| From \ Event | `Retrieve()` | `Return()` | `AuthorizeDisposal()` | `PlaceLegalHold()` | `ReleaseLegalHold()` |
|---|---|---|---|---|---|
| **Archived** (hold=false) | → `Retrieved` | `ARC-011` / 409 | → `Disposed` **iff** expiry ≤ asOf and class ≠ Permanent; else `ARC-013`/`ARC-014` (422) | hold=true, `ArchiveLegalHoldPlaced` raised | `ARC-032` / 409 |
| **Archived** (hold=true) | → `Retrieved` | `ARC-011` / 409 | **`ARC-015` / 422** — blocked regardless of retention | hold stays true, reason overwritten, event re-raised | hold=false, `ArchiveLegalHoldReleased` raised |
| **Retrieved** (hold=false) | → `Retrieved` (idempotent, no guard) | → `Archived` | → `Disposed` (same retention rules; **no state guard against disposing a retrieved record**) | hold=true | `ARC-032` / 409 |
| **Retrieved** (hold=true) | → `Retrieved` | → `Archived` | `ARC-015` / 422 | hold stays true | hold=false |
| **Disposed** | `ARC-010` / 409 | `ARC-011` / 409 | `ARC-012` / 409 | **`ARC-031` / 409** | `ARC-032` / 409 (hold is always false after disposal) |

Notes read from the code, not assumed: `Retrieve()` has **no** guard against being called on an already-`Retrieved`
row (`ArchiveEntry.cs:109-117`) — it is idempotent. `AuthorizeDisposal` has **no** guard requiring `Archived`;
a `Retrieved` record can be disposed directly (`ArchiveEntry.cs:129-156`). `ReleaseLegalHold` clears
`LegalHoldReason` and `LegalHoldPlacedBy` to `null` (`ArchiveEntry.cs:103-105`) — **the reason is not
retained on the row after release**; the only surviving record is the `field_change` `Modified` rows and the
`ArchiveLegalHoldPlaced` payload in `audit.audit_trail`.

### 3.2 `AuditTrailReview.Status` (`src/NT.QAMS.Domain/ComplianceLedger/AuditTrailReview.cs:33-75`)

| From \ Event | `Open(ref, start, end)` | `Complete(reviewer, at, events, changes, anomalies, conclusion)` |
|---|---|---|
| *(none)* | → `Open` when `end >= start`; else `ATR-001` / 422 | n/a |
| **Open** | n/a | → `Completed`; sets `ReviewedBy`, `CompletedAtUtc`, `EventsReviewed`, `FieldChangesReviewed`, `AnomaliesFound`, trimmed `Conclusion`. Empty conclusion → `ATR-011` / 422. `AnomaliesFound == true` → raises `AuditTrailAnomalyFound` |
| **Completed** | n/a | **`ATR-010` / 409** — immutable |

### 3.3 Ledger row lifecycle (all four `audit.*` tables)

| From \ Operation | `INSERT` | `SELECT` | `UPDATE` | `DELETE` | `TRUNCATE` |
|---|---|---|---|---|---|
| *(no row)* | permitted when the `WITH CHECK` predicate holds; else SQLSTATE `42501` | n/a | n/a | n/a | n/a |
| **row exists** | n/a | visible iff `tenant_id = GUC` **or** `bypass_rls = 'on'` | **SQLSTATE `P0001`** `audit ledgers are append-only` | **SQLSTATE `P0001`** | **NOT BLOCKED** by the row trigger (`GAP-LEDGER-012`) |

There is no other transition. No API, no MediatR handler and no EF configuration exposes an update or
delete path for any of the four types — `grep -rn "Set<AuditTrailEntry>()"` yields two call sites
(`AppendAsync` add, `VerifyChainAsync`/`GetTrailAsync` read), and neither `Remove`, `RemoveRange`,
`ExecuteUpdate` nor `ExecuteDelete` appears against any ledger type.

---

## 4. Decision tables

### 4.1 DT-A — `ChangeReasonMiddleware` (`.../Middleware/RequestIdentity.cs:143-161`)

| Rule | HTTP method is DELETE | `X-Change-Reason` present and non-whitespace | Outcome |
|---|---|---|---|
| A1 | Y | Y | `reasonSetter.Set(reason)`; pipeline continues; reason stamped on every `field_change` row of the unit of work |
| A2 | Y | N | **400** `application/problem+json`, `code = CHANGE-REASON-REQUIRED`, title `"A reason is required for this change."`; handler never runs; **no** `field_change` row written |
| A3 | N | Y | `reasonSetter.Set(reason)`; pipeline continues; reason stamped (POST/PUT/PATCH honour the header) |
| A4 | N | N | `reasonSetter.Set("")`; pipeline continues; `Reason` persisted as `""` — *not* `NULL* (`Headers[...].ToString()` on a missing header yields `string.Empty`, and `FieldChangeInterceptor.cs:115` assigns it verbatim) |

Rule A4 is a genuine implementation detail worth pinning: the ledger's `reason` column will hold the empty
string, not `NULL`, for ordinary non-DELETE traffic. Measured population shows `reason IS NOT NULL` on 14 of
54,262 rows, which means the historical rows predate the column or were written with `null` — the A4
prediction must be verified at execution, not assumed. See `TC-LEDGER-DT-001`.

### 4.2 DT-B — `ArchiveEntry.AuthorizeDisposal(actorId, asOf)` (`ArchiveEntry.cs:129-156`)

| Rule | `State == Disposed` | `IsOnLegalHold` | `RetentionClass == Permanent \|\| RetentionExpiry is null` | `RetentionExpiry > asOf` | Result |
|---|---|---|---|---|---|
| B1 | Y | – | – | – | `ARC-012` / **409** |
| B2 | N | Y | – | – | `ARC-015` / **422** |
| B3 | N | N | Y | – | `ARC-013` / **422** |
| B4 | N | N | N | Y | `ARC-014` / **422**, message carries `RetentionExpiry:yyyy-MM-dd` |
| B5 | N | N | N | N | `State = Disposed`, `DisposalAuthorizedBy = actorId`, `RecordDisposed` raised → **204** |

Short-circuit order is B1 → B2 → B3 → B4 → B5. Rule B2 preceding B3/B4 is the "legal hold blocks disposal
regardless of retention" requirement (URS-044) and is the reason a *permanent* record on hold reports
`ARC-015`, not `ARC-013`.

### 4.3 DT-C — RLS policy evaluation on `audit.*` (measured policy text, §1.7)

| Rule | `app.bypass_rls` | row `tenant_id` | `app.current_tenant` GUC | `SELECT` visible | `INSERT` accepted |
|---|---|---|---|---|---|
| C1 | `on` | anything | anything | **yes** | **yes** |
| C2 | `off` | `T` | `T` | yes | yes |
| C3 | `off` | `T` | `U ≠ T` | no | **no — `42501`** |
| C4 | `off` | `T` | `''` / nil | no (fail-closed) | no — `42501` |
| C5 | `off` | `NULL` | `T` | **no** | **yes** (the `tenant_id IS NULL` limb of `WITH CHECK`) |
| C6 | `off` | `NULL` | `''` / nil | no | yes |

C5 is the asymmetry that makes pre-authentication auditing work and simultaneously hides the 26,848 legacy
null-tenant rows from every tenant reader (`GAP-LEDGER-022`).

### 4.4 DT-D — `ESignatureService.SignAsync` (`ComplianceLedgerServices.cs:90-131`)

| Rule | Signer row exists | `IsLockedOut(now)` | password verifies | PIN set and verifies | Result |
|---|---|---|---|---|---|
| D1 | N | – | – | – | `SIG-404` / **404**; no ledger write |
| D2 | Y | Y | – | – | `SIG-003` / **422**; `ESIGN_LOCKED` security event written |
| D3 | Y | N | N | – | `SIG-002` / **422**; `RegisterFailedLogin` + `ESIGN_FAILED` detail `bad-password:{subjectRef}` |
| D4 | Y | N | Y | N | `SIG-001` / **422**; `RegisterFailedLogin` + `ESIGN_FAILED` detail `bad-pin:{subjectRef}` |
| D5 | Y | N | Y | Y | `audit.electronic_signature` row inserted; `SignatureRecord` returned |

Note D3 precedes D4: a caller with a wrong password **and** a wrong PIN always sees `SIG-002`.

### 4.5 DT-E — `VerifyChainAsync` verdict (`ComplianceLedgerServices.cs:209-231`)

| Rule | tenant has entries | every `PrevHash` links | every `EntryHash` recomputes | Result |
|---|---|---|---|---|
| E1 | N | – | – | `(Ok = true, Verified = 0, BrokenAtSequence = null)` — **an empty chain reports OK** |
| E2 | Y | Y | Y | `(true, n, null)` |
| E3 | Y | N at row *k* | – | `(false, k-1 verified, BrokenAtSequence = Sequence(k))` |
| E4 | Y | Y | N at row *k* | `(false, k-1, Sequence(k))` |

E1 matters for the export attestation: a brand-new tenant's `audit-trail.xlsx` prints
`"OK — chain intact" / 0 / — / 0`. That is correct behaviour, and it is also indistinguishable from a
successfully truncated ledger — see `TC-LEDGER-SEC-021`.

---

## 5. Detailed test cases

Format: one field block per case, all 28 fields, in the order fixed by the conventions file. `Result` is
`Not Run` throughout — this package is authored, not executed.

**Environments referenced**

- `ENV-DEV` — local dev: API `http://localhost:5080`, SPA `http://localhost:4200`, PostgreSQL 17 `ntqams`,
  role `qams_app` (**schema owner — not hardened**). Start via `scripts/dev-up.ps1`.
- `ENV-ITEST` — `dotnet test` with `QMS_ITEST_POSTGRES` set; `[Collection("real-postgres")]`, every case
  inside a rolled-back transaction.
- `ENV-UNIT` — in-process xUnit, EF in-memory provider, no database.
- `ENV-HARDENED` — dedicated validation instance with `deploy/harden-runtime-role.sql` applied
  (`qams_owner` owns DDL, `qams_app` is `NOSUPERUSER NOBYPASSRLS` with `SELECT, INSERT` only on `audit.*`).
  **Does not exist yet** — see `GAP-LEDGER-011`.
- `ENV-ISOLATED-ATTACK` — throwaway instance restored from a backup dump, network-isolated, destroyed after
  the session. **Never production, never the shared dev database.**

**Risk IDs.** `docs/validation/02-Functional-Risk-Assessment.md` names risk *areas*, not identified risks,
so this file mints `RSK-LEDGER-001…015` and maps each to its FRA row:

| Risk ID | Description | FRA row (`02-Functional-Risk-Assessment.md`) |
|---|---|---|
| `RSK-LEDGER-001` | Audit trail is mutable or the hash chain is forgeable | line 52 "Audit trail immutability & hash chain" — HIGH |
| `RSK-LEDGER-002` | A regulated action occurs with no ledger entry | line 52 |
| `RSK-LEDGER-003` | One tenant reads another tenant's ledger | line 51 "Tenant isolation" — HIGH |
| `RSK-LEDGER-004` | A destructive change is recorded with no justification | line 56 "Reason for change" — HIGH |
| `RSK-LEDGER-005` | Security-relevant events are not recorded | line 59 "Security event logging" — Medium |
| `RSK-LEDGER-006` | An export is not an accurate and complete copy | line 52 / line 65 |
| `RSK-LEDGER-007` | A record is disposed while it should be retained | line 65 "Records retention & archival" — Medium |
| `RSK-LEDGER-008` | A privileged database actor alters history undetected | line 52 / line 54 |
| `RSK-LEDGER-009` | A signature record is excised or altered | line 53 "Electronic signatures" — HIGH |
| `RSK-LEDGER-010` | Two writers fork a tenant's chain | line 52 |
| `RSK-LEDGER-011` | Periodic audit-trail review lacks evidence of coverage | line 52 |
| `RSK-LEDGER-012` | Actor / tenant / timestamp / record-id attribution is wrong | line 52 |
| `RSK-LEDGER-013` | A rolled-back change leaves a ledger row, or a committed change leaves none | line 52 |
| `RSK-LEDGER-014` | The ledger is lost or unrecoverable | line 67 "Backup / DR" — Medium–High |
| `RSK-LEDGER-015` | Ledger reads are unusable at production volume | line 52 |

<!--APPEND-->
