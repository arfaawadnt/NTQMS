# 13 — Controlled Documents, SOP Lifecycle, Review Cycles, Acknowledgements, Controlled Copies

| Field | Value |
|---|---|
| **Module code** | `DOC` |
| **System under test** | NT.QMS **v1.51.2** — repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master` |
| **Binding conventions** | `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md` — read it first; the 28-field case format (§4), the canonical case block (§8), the evidence labels `[IV]`/`[RNV]`/`[ID]`/`[GD]` (§4), the ID convention (§5) and the honesty rules (§6) are binding on every case authored against this front matter. |
| **Inspection date** | 2026-08-01 |
| **Inspection method** | Source read (Domain / Application / WebApi / Infrastructure / Angular), migration read, and **live read-only `psql`** against dev DB `ntqams` for schema, RLS, constraints, indexes and row counts. |
| **File type** | **Front matter only** (per conventions §7 split rule). Detailed cases are authored separately into `13-module-document-control-cases-<A…E>.md`. |

## Completeness statement

**Complete in this file:** the implementation inventory (§1), brief-vs-code divergences (§2), the
document-lifecycle and controlled-copy state-transition matrices (§3), the review/approval-SoD and
obsolete-version-read decision tables (§4), UAT scenarios (§6), exploratory charters (§7) and the
module Gap Register (§8).

**Deliberately absent:** `## 5. Detailed test cases`. Section 5 is owned by the case files listed in
the reservation table below. A reserved range with no matching case file is a **coverage hole, not a
delivered case** (conventions §7).

**Scope actually opened (every file read in full unless noted):**

`src/NT.QAMS.Domain/DocumentControl/ControlledDocument.cs` · `DocumentAcknowledgement.cs` ·
`DocumentControlledCopy.cs` · `src/NT.QAMS.Domain/Files/FileReference.cs` ·
`src/NT.QAMS.Application/DocumentControl/Commands/DocumentCommands.cs` ·
`DocumentAcknowledgementSlice.cs` · `ControlledCopySlice.cs` · `DocumentReviewDuePolicy.cs` ·
`Queries/DocumentQueries.cs` · `src/NT.QAMS.Contracts/DocumentControl/DocumentContracts.cs` ·
`src/NT.QAMS.WebApi/Controllers/DocumentsController.cs` · `FilesController.cs` ·
`src/NT.QAMS.WebApi/Security/FileContentPolicy.cs` ·
`src/NT.QAMS.WebApi/Authorization/RequirePermissionAttribute.cs` ·
`src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs` · `RequestIdentity.cs` (ChangeReasonMiddleware) ·
`src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs` ·
`src/NT.QAMS.Application/Abstractions/CommandAuthorization.cs` ·
`src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs` (documents rows) ·
`src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs` ·
`src/NT.QAMS.Infrastructure/Persistence/Configurations/DocumentControlConfigurations.cs` ·
`Interceptors/OutboxInterceptor.cs` · `Interceptors/FieldChangeInterceptor.cs` (header + exclusion set) ·
`Compliance/ComplianceLedgerServices.cs` (`AuditTrailAppender`, `ESignatureService`) ·
`Jobs/ScheduledSweepService.cs` (document branch) ·
`src/NT.QAMS.Application/Notifications/NotificationDispatcher.cs` · `NotificationPolicies.cs` ·
migrations `20260721215255_DocumentControl`, `20260725054703_DocumentReviewCycles`,
`20260726204141_DocumentAcknowledgement`, `20260726214512_DocumentControlledCopy`, plus the document
slices of `Hardening1_TypesAndNames`, `Hardening3_CheckDomains`, `Hardening4_ChildTenancy`,
`Hardening5_CompositeKeys` ·
`frontend/src/app/features/documents/{document-list,document-detail}.component.ts`, `documents.facade.ts`,
`frontend/src/app/core/api/documents-api.service.ts`.

**Not opened in this pass (declared, not silently omitted):** `frontend/src/app/core/api/files-api.service.ts`
body, the `shared/ui` components the detail screen composes (`workflow-stepper`, `status-pill`,
`audit-trail`), `IFileStorage`'s concrete implementation, and the Playwright `regulated-workflow` spec.
Any case that depends on those must carry `[RNV]` until the file is read.

---

## ID reservation table

Ranges are **reserved, not consumed**, by this file. Each batch file owns a disjoint slice of scope and
its own ID block; ids are never renumbered (conventions §5).

| Batch file | Scope slice | Reserved ID ranges | Approx. cases |
|---|---|---|---|
| `13-module-document-control-cases-A.md` | Aggregate lifecycle state machine, version numbering, SoD in the domain, guard codes `DOC-001…DOC-020`, `SOD-DOC-001/002` | `TC-DOC-UNIT-001` … `TC-DOC-UNIT-040`<br>`TC-DOC-STATE-001` … `TC-DOC-STATE-040`<br>`TC-DOC-DT-001` … `TC-DOC-DT-020` | ~50 |
| `13-module-document-control-cases-B.md` | HTTP surface of `/api/documents` — every route, status code, permission gate, the publish e-signature ceremony (`SIG-001/002/003/404`), rate-limit partition, problem+json shapes | `TC-DOC-API-001` … `TC-DOC-API-060`<br>`TC-DOC-SEC-001` … `TC-DOC-SEC-025` | ~55 |
| `13-module-document-control-cases-C.md` | Periodic review cycles (sweep → `DocumentReviewDue` → policy → WorkTask/notification), acknowledgements (`ACK-001/002/003/010`), idempotency, boundary values on `ReviewCycleMonths` | `TC-DOC-INT-001` … `TC-DOC-INT-030`<br>`TC-DOC-BVA-001` … `TC-DOC-BVA-015`<br>`TC-DOC-EP-001` … `TC-DOC-EP-015` | ~45 |
| `13-module-document-control-cases-D.md` | Controlled-copy register (`CCP-001/002/003/010/020/404`), file upload/download (`FILE-001/002/404/415`), content sniffing, tenant isolation and RLS on the five document tables | `TC-DOC-RLS-001` … `TC-DOC-RLS-015`<br>`TC-DOC-DF-001` … `TC-DOC-DF-010`<br>`TC-DOC-API-061` … `TC-DOC-API-085` | ~45 |
| `13-module-document-control-cases-E.md` | End-to-end SPA journeys, accessibility of the publish/acknowledge/copy surfaces, list pagination, performance smoke | `TC-DOC-E2E-001` … `TC-DOC-E2E-012`<br>`TC-DOC-A11Y-001` … `TC-DOC-A11Y-006`<br>`TC-DOC-PERF-001` … `TC-DOC-PERF-004` | ~20 |
| **this file** | UAT scenarios (§6) and exploratory charters (§7) | `TC-DOC-UAT-001` … `TC-DOC-UAT-010`<br>`TC-DOC-EXPL-001` … `TC-DOC-EXPL-006` | 16 (delivered here) |

**Requirement IDs in scope:** `URS-025`, `URS-026`, `URS-027`, `URS-028`, `URS-029`
(`docs/validation/01-User-Requirements-Specification.md:63-67`), plus `URS-046` (immutable stored files,
line 99). **No URS covers the periodic-review cycle** — see `GAP-DOC-001`.

**Risk IDs:** `docs/validation/02-Functional-Risk-Assessment.md` carries **area-level** rows only
(`Document control lifecycle | URS-025,026,027 | Med/Med/Med | Medium | OQ + PQ`, line ~60) — it mints no
per-requirement `RSK-` identifiers. Per conventions §5, case authors **mint `RSK-DOC-<NNN>` and say so**
in the `Risk` field.

---

## 1. Implementation inventory

### 1.1 Aggregates and entities

| Type | Kind | Tenancy | File:line |
|---|---|---|---|
| `ControlledDocument` | `AggregateRoot`, `ITenantScoped` | tenant-scoped | `src/NT.QAMS.Domain/DocumentControl/ControlledDocument.cs:51` |
| `DocumentVersion` | `Entity`, **owned collection** of `ControlledDocument` | shadow `TenantId` stamped from owner | `ControlledDocument.cs:13`; mapping `Infrastructure/Persistence/Configurations/DocumentControlConfigurations.cs:23-36` |
| `DocumentAcknowledgement` | `AggregateRoot`, `ITenantScoped` | tenant-scoped | `src/NT.QAMS.Domain/DocumentControl/DocumentAcknowledgement.cs:13` |
| `DocumentControlledCopy` | `AggregateRoot`, `ITenantScoped` | tenant-scoped | `src/NT.QAMS.Domain/DocumentControl/DocumentControlledCopy.cs:17` |
| `FileReference` | `AggregateRoot`, `ITenantScoped` | tenant-scoped | `src/NT.QAMS.Domain/Files/FileReference.cs:12` |

### 1.2 Enumerations (exhaustive)

| Enum | Members | File:line | DB CHECK constraint (live-verified) |
|---|---|---|---|
| `DocumentStatus` | `Draft, Published, Obsolete` | `ControlledDocument.cs:6` | `ck_controlled_document_status_domain CHECK (status IN ('Draft','Published','Obsolete'))` |
| `VersionState` | `Draft, UnderReview, Approved, Published, Obsolete, Rejected` | `ControlledDocument.cs:8` | `ck_document_version_state_domain CHECK (state IN ('Draft','UnderReview','Approved','Published','Obsolete','Rejected'))` |
| `VersionBump` | `Major, Minor` | `ControlledDocument.cs:10` | n/a — not persisted; a command parameter only |
| `ControlledCopyStatus` | `Issued, Returned, Destroyed` | `DocumentControlledCopy.cs:6` | `ck_document_controlled_copy_status_domain CHECK (status IN ('Issued','Returned','Destroyed'))` |

> **`VersionState.Rejected` is a dead value.** No code path assigns it — `ControlledDocument.RejectVersion`
> sets `version.State = VersionState.Draft` (`ControlledDocument.cs:146`). It is nonetheless declared in the
> enum and admitted by the CHECK constraint. → `GAP-DOC-002`.

### 1.3 Aggregate state and invariants — `ControlledDocument`

| Property | Type | Default | File:line |
|---|---|---|---|
| `TenantId` | `Guid` | — | `ControlledDocument.cs:62` |
| `Code` | `string` | normalised `Trim().ToUpperInvariant()` at create | `:63`, set at `:100` |
| `Title` | `string` | `Trim()` | `:64`, `:101` |
| `Category` | `string` | `"SOP"` when blank, else `Trim()` | `:65`, `:102` |
| `Status` | `DocumentStatus` | `Draft` | `:66`, `:103` |
| `ReviewCycleMonths` | `int` | `24`; **silently coerced to 24 when outside `> 0 and <= 120`** | `:69`, coercion `:104` |
| `NextReviewDue` | `DateOnly?` | `null` until first publish | `:72`, stamped `:170` and `:202` |
| `ReviewDueRaised` | `bool` | `false` | `:75`, set `:187`, cleared `:171` and `:203` |
| `Versions` | `IReadOnlyList<DocumentVersion>` | one `1.0 Draft` at create | `:77`, seeded `:106` |
| `PublishedVersion` | computed `SingleOrDefault(State == Published)` | — | `:79` |
| `InFlightVersion` | computed `SingleOrDefault(State is Draft or UnderReview or Approved)` | — | `:81-82` |

**Invariants enforced in the aggregate:**

1. **At most one published version.** Publishing v(n) atomically flips v(n−1) to `Obsolete` in the same
   call (`:159-164`). `PublishedVersion` uses `SingleOrDefault`, so a second published row would throw at
   read time.
2. **At most one in-flight version.** `DraftNewVersion` refuses when `InFlightVersion is not null`
   → `DOC-016` (`:214-217`). `InFlightVersion` uses `SingleOrDefault`.
3. **SoD on review.** `Recommend` refuses `actorId == version.AuthorId` → `SOD-DOC-001` (`:120-123`).
4. **SoD on approval/publish.** `Publish` refuses `actorId == version.AuthorId` → `SOD-DOC-002`
   (`:154-157`), and the handler pre-checks the same rule before minting the signature
   (`Application/DocumentControl/Commands/DocumentCommands.cs:142-146`).
   **Note:** the reviewer and the approver may be the **same person** — nothing compares
   `RecommendedBy` with the publishing actor. → `GAP-DOC-003`.
5. **Revision requires a published basis.** `DraftNewVersion` throws `DOC-017` when
   `PublishedVersion is null` (`:219-220`).
6. **Version numbering.** `Major` bump → `(basis.Major + 1, 0)`; `Minor` bump → `(basis.Major, basis.Minor + 1)`
   (`:222-224`). `VersionLabel => $"{Major}.{Minor}"` (`:39`), `[NotMapped]` via `version.Ignore(v => v.VersionLabel)`
   (`DocumentControlConfigurations.cs:35`).
7. **Review cadence.** On publish, `NextReviewDue = publishDate.AddMonths(ReviewCycleMonths)` and
   `ReviewDueRaised = false` (`:170-171`). On `ConfirmPeriodicReview`, `NextReviewDue = reviewedOn.AddMonths(ReviewCycleMonths)`
   and the flag clears (`:202-203`).
8. **Review-due flag raises once per cycle.** `MarkReviewDueIfReached` returns silently unless
   `Status == Published && !ReviewDueRaised && NextReviewDue is {} due && due <= today` (`:181-185`).

**Invariants NOT enforced (read in code, absent by inspection):**

- `Retire` does **not** cancel or obsolete the in-flight version — it touches only `PublishedVersion`
  (`:236-241`). Combined with `Publish` setting `Status = DocumentStatus.Published` unconditionally
  (`:169`), a retired document can be **resurrected**. → `GAP-DOC-004`.
- `Retire` carries no SoD guard and no e-signature.
- `ConfirmPeriodicReview` carries no SoD guard and no e-signature, despite being gated on
  `documents.sign` at the HTTP layer (`DocumentsController.cs:37`).
- Nothing marks a controlled copy of a superseded version as obsolete. → `GAP-DOC-005`.

### 1.4 Domain error codes — **exhaustive for this module**

Every code below was read at the throw site. HTTP status is derived from
`src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:26-82`:
`InvalidStateTransitionException` → **409**; `DomainException` with prefix `AUTH-` → **401**; prefix
`AUTHZ-` → **403**; suffix `-404` → **404**; every other `DomainException` → **422**;
`DbUpdateConcurrencyException` → **409 `CONCURRENCY-409`**; FluentValidation → **400**.

#### `ControlledDocument` (`src/NT.QAMS.Domain/DocumentControl/ControlledDocument.cs`)

| Code | Exception type | HTTP | Message / condition | Line |
|---|---|---|---|---|
| `DOC-001` | `DomainException` | 422 | "Document code is required (e.g. SOP-CAL-045)." — blank/whitespace `code` | `:90` |
| `DOC-002` | `DomainException` | 422 | "Document title is required." — blank/whitespace `title` | `:95` |
| `DOC-010` | `InvalidStateTransitionException` | 409 | "No version available to submit for review." (no in-flight) **or** "Cannot submit for review a version in state {State}." | `:112` → `:250`/`:254` |
| `DOC-011` | `InvalidStateTransitionException` | 409 | "No version available to recommend." **or** "Cannot recommend a version in state {State}." | `:119` → `:250`/`:254` |
| `DOC-012` | `InvalidStateTransitionException` | 409 | "No version is awaiting review or approval." **or** "Cannot reject a version in state {State}." | `:134`, `:138` |
| `DOC-013` | `DomainException` | 422 | "A rejection reason is required." (blank reason reaching the domain) | `:143` |
| `DOC-014` | `InvalidStateTransitionException` | 409 | "No version available to publish." **or** "Cannot publish a version in state {State}." | `:153` → `:250`/`:254` |
| `DOC-015` | `InvalidStateTransitionException` | 409 | "A retired document cannot receive new versions." | `:211` |
| `DOC-016` | `DomainException` | **422** | "A version is already in progress; publish or reject it first." | `:216` |
| `DOC-017` | `InvalidStateTransitionException` | 409 | "Only a published document can be revised." | `:220` |
| `DOC-018` | `InvalidStateTransitionException` | 409 | "Document is already obsolete." | `:233` |
| `DOC-020` | `InvalidStateTransitionException` | 409 | "Only a published document undergoes periodic review." | `:199` |
| `SOD-DOC-001` | `DomainException` | **422** | "Segregation of duties: the author cannot review their own document." | `:122` |
| `SOD-DOC-002` | `DomainException` | **422** | "Segregation of duties: the author cannot approve their own document." | `:156` |

#### Application layer — `Commands/DocumentCommands.cs`

| Code | Exception type | HTTP | Condition | Line |
|---|---|---|---|---|
| `AUTH-003` | `DomainException` | **401** | "An authenticated user is required." — `ICurrentUser.UserId` is null | `:34`, `:86` |
| `DOC-003` | `DomainException` | 422 | "Document code '{code}' is already in use." — tenant-local duplicate (normalised upper-case) | `:39` |
| `DOC-404` | `DomainException` | 404 | "Document not found." | `:83` |
| `FILE-404` | `DomainException` | 404 | "Uploaded file not found." — create (`:45`) and draft-new-version (`:171`) |
| `DOC-014` | **`DomainException`** | **422** | "No version is awaiting approval." — publish handler pre-check. **Same code, different status than the aggregate's 409.** | `:131` |
| `DOC-014` | `InvalidStateTransitionException` | 409 | "Cannot publish a version in state {State}." — publish handler pre-check | `:138-139` |
| `SOD-DOC-002` | `DomainException` | 422 | publish handler pre-check, before the signature is minted | `:144-145` |

#### `DocumentAcknowledgement` / acknowledgement slice

| Code | Exception type | HTTP | Condition | File:line |
|---|---|---|---|---|
| `ACK-001` | `DomainException` | 422 | "A document is required to record an acknowledgement." — `documentId == Guid.Empty` | `DocumentAcknowledgement.cs:33` |
| `ACK-002` | `DomainException` | 422 | "An acknowledging user is required." — `userId == Guid.Empty` | `:38` |
| `ACK-003` | `DomainException` | 422 | "A published version is required to acknowledge." — blank `versionLabel` | `:43` |
| `ACK-010` | `DomainException` | 422 | "Only a published document can be acknowledged." | `DocumentAcknowledgementSlice.cs:29` |
| `AUTH-003` | `DomainException` | 401 | unauthenticated | `DocumentAcknowledgementSlice.cs:22`, `:64` |
| `DOC-404` | `DomainException` | 404 | document not found | `DocumentAcknowledgementSlice.cs:26`, `:68` |

#### `DocumentControlledCopy` / controlled-copy slice

| Code | Exception type | HTTP | Condition | File:line |
|---|---|---|---|---|
| `CCP-001` | `DomainException` | 422 | "A copy holder (person, role, or location) is required." | `DocumentControlledCopy.cs:46` |
| `CCP-002` | `DomainException` | 422 | "The copy number must be positive." — `copyNumber < 1` (unreachable from the API: the handler always passes `last + 1 >= 1`) | `:51` |
| `CCP-003` | `DomainException` | 422 | domain: "A controlled copy can only be closed as Returned or Destroyed."; slice: "The outcome must be Returned or Destroyed." (unparseable string) | `:72`; `ControlledCopySlice.cs:54` |
| `CCP-010` | `InvalidStateTransitionException` | 409 | "Only an issued copy can be returned or destroyed (current: {Status})." | `:77-78` |
| `CCP-020` | `DomainException` | 422 | "Only a published document can have a controlled copy issued." | `ControlledCopySlice.cs:30` |
| `CCP-404` | `DomainException` | 404 | "Controlled copy not found." | `ControlledCopySlice.cs:58` |
| `AUTH-003` | `DomainException` | 401 | unauthenticated | `ControlledCopySlice.cs:23`, `:51` |
| `DOC-404` | `DomainException` | 404 | document not found | `ControlledCopySlice.cs:27` |

#### Files (`src/NT.QAMS.Domain/Files/FileReference.cs`, `WebApi/Controllers/FilesController.cs`)

| Code | Exception type | HTTP | Condition | File:line |
|---|---|---|---|---|
| `FILE-001` | `DomainException` | 422 | "File name is required." | `FileReference.cs:34` |
| `FILE-002` | `DomainException` | 422 | "File is empty." — `sizeBytes <= 0` | `:39` |
| `FILE-415` | `DomainException` | **422** (not 415 — the suffix is not `-404` and the prefix is neither `AUTH-` nor `AUTHZ-`) | allow-list / magic-byte sniff refusal | `FilesController.cs:45` |
| `TENANT-000` | `DomainException` | 422 | "A tenant context is required." — upload without a tenant | `FilesController.cs:33` |

#### Cross-cutting codes reachable on this module's routes

| Code | HTTP | Source | File:line |
|---|---|---|---|
| `AUTHZ-403` | 403 | `[RequirePermission]` denial on a document route | `WebApi/Authorization/RequirePermissionAttribute.cs:59`; constant at `Middleware/ProblemAuthorizationResultHandler.cs:16` |
| `AUTHZ-000` | 422 | a **command** with no `CommandPolicyAttribute` — fail-closed | `Application/Behaviors/AuthorizationBehavior.cs:52-53` |
| `AUTHZ-001` | 403 | command executed with no authenticated actor/role | `AuthorizationBehavior.cs:60` |
| `AUTHZ-002` | 403 | "Role '{role}' is not permitted…" — e.g. `ExternalAuditor` hitting any `[RequireInternalActor]` document command | `AuthorizationBehavior.cs:83-84` |
| `AUTHZ-008` | 403 | declared permission key not in the catalogue | `AuthorizationBehavior.cs:68-69` |
| `SIG-001` | 422 | "Electronic-signature PIN is not set or is incorrect." | `Infrastructure/Compliance/ComplianceLedgerServices.cs:113` |
| `SIG-002` | 422 | "Account password is incorrect." | `:106` |
| `SIG-003` | 422 | "Account is temporarily locked after repeated failed signings." | `:102` |
| `SIG-404` | 404 | "Signer not found." | `:94` |
| `CONCURRENCY-409` | 409 | `xmin` token mismatch on save | `Middleware/DomainExceptionHandler.cs:21,28-33` |
| `CHANGE-REASON-REQUIRED` | 400 | any **DELETE** without `X-Change-Reason`. **Not reachable in this module — the document surface exposes no DELETE route.** | `Middleware/RequestIdentity.cs:149-156` |

### 1.5 Domain events

| Event | Raised by | File:line | Handler(s) | Reaches audit ledger? |
|---|---|---|---|---|
| `DocumentSubmittedForReview(DocumentId, Code, Version)` | `SubmitForReview` | `ControlledDocument.cs:114`, decl `:262` | **none** | Yes — via outbox |
| `DocumentRecommended(DocumentId, Code, Version, RecommendedBy)` | `Recommend` | `:128`, decl `:263` | **none** | Yes |
| `DocumentVersionRejected(DocumentId, Code, Version, RejectedBy, Reason)` | `RejectVersion` | `:148`, decl `:264` | **none** | Yes |
| `DocumentPublished(DocumentId, Code, Title, Version, ApprovedBy)` | `Publish` | `:172`, decl `:265` | `NotificationEventPolicies.Handle` → `DOC_PUBLISHED` | Yes |
| `DocumentVersionObsoleted(DocumentId, Code, Version, FileId)` | `Publish` (`:163`) and `Retire` (`:240`) | decl `:266` | **none** | Yes |
| `DocumentRetired(DocumentId, Code, RetiredBy)` | `Retire` | `:244`, decl `:267` | **none** | Yes |
| `DocumentReviewDue(DocumentId, Code, Title, DueOn)` | `MarkReviewDueIfReached` | `:188`, decl `:269-270` | `DocumentReviewDuePolicy` | Yes |
| `DocumentReviewConfirmed(DocumentId, Code, ReviewerId, ReviewedOn)` | `ConfirmPeriodicReview` | `:204`, decl `:272-273` | **none** | Yes |
| `DocumentAcknowledged(DocumentId, DocumentCode, VersionLabel, UserId)` | `DocumentAcknowledgement.Record` | `DocumentAcknowledgement.cs:54`, decl `:59-60` | **none** | Yes |
| `ControlledCopyClosed(CopyId, DocumentId, DocumentCode, CopyNumber, Outcome, TenantId)` | `DocumentControlledCopy.Close` | `DocumentControlledCopy.cs:84`, decl `:88-89` | **none** | Yes |

**Events NOT raised (verified by absence at the mutation site):**

- `DraftNewVersion` raises nothing (`ControlledDocument.cs:207-227`) — a new version appears in the
  ledger only as a `FieldChangeRecord` "Created" row, never as a domain event.
- `DocumentControlledCopy.Issue` raises nothing (`:40-65`) — issuing a numbered copy produces no domain
  event, no audit-ledger entry and no notification; only the field-change row. Its **closure** does raise.
  → `GAP-DOC-006`.

**Event → ledger path (verified):** `OutboxInterceptor.Drain` walks `ChangeTracker.Entries<AggregateRoot>()`,
serialises each `DomainEvent` to `qams.outbox_event` in the **same** `SaveChanges`
(`Infrastructure/Persistence/Interceptors/OutboxInterceptor.cs:44-82`); `OutboxProcessor` publishes it and
calls `AuditTrailAppender.AppendAsync` in the same `SaveChanges` as marking the row processed
(`Outbox/OutboxProcessor.cs:120-131`), chaining `PrevHash → EntryHash` per tenant
(`Compliance/ComplianceLedgerServices.cs:29-66`). `DocumentVersion` is an **owned entity, not an
`AggregateRoot`**, so events raised on it would not drain — none are.

### 1.6 Endpoints

**`DocumentsController`** — `[ApiController] [Route("api/documents")] [Authorize]`
(`src/NT.QAMS.WebApi/Controllers/DocumentsController.cs:19-22`). Every route is dual-exposed under
`/api/v{version}/…` by `Asp.Versioning`; both forms are in
`tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` (document rows: lines 53-58, 67, 296-306, 316).

| # | Method + route | Endpoint gate | Command/query policy | Success | Line |
|---|---|---|---|---|---|
| 1 | `GET /api/documents` | **none beyond `[Authorize]`** | query — behaviour skips non-commands (`AuthorizationBehavior.cs:44-47`) | `200` `PagedResponse<DocumentListItemDto>` | `:24-29` |
| 2 | `GET /api/documents/{id}` | **none beyond `[Authorize]`** | query | `200 DocumentDetailDto` | `:31-33` |
| 3 | `POST /api/documents/{id}/confirm-review` | `[RequirePermission(documents, Sign)]` | `[RequireInternalActor]` | `204` | `:36-42` |
| 4 | `GET /api/documents/{id}/signatures` | **none beyond `[Authorize]`** | query (`GetSignaturesForSubjectQuery("DOC:{id:N}")`) | `200` | `:45-47` |
| 5 | `POST /api/documents/{id}/acknowledge` | **none beyond `[Authorize]`** | `[RequireInternalActor]` | `200 { id }` | `:50-52` |
| 6 | `GET /api/documents/{id}/my-acknowledgement` | **none beyond `[Authorize]`** | query | `200 MyDocumentAcknowledgementDto` | `:55-57` |
| 7 | `GET /api/documents/{id}/acknowledgements` | `[RequirePermission(documents, View)]` | query | `200 DocumentAcknowledgementDto[]` | `:60-63` |
| 8 | `GET /api/documents/{id}/controlled-copies` | **none beyond `[Authorize]`** | query | `200 ControlledCopyDto[]` | `:66-68` |
| 9 | `POST /api/documents/{id}/controlled-copies` | `[RequirePermission(documents, Edit)]` | `[RequireInternalActor]` | `200 { id }` | `:70-73` |
| 10 | `POST /api/documents/controlled-copies/{copyId}/close` | `[RequirePermission(documents, Edit)]` | `[RequireInternalActor]` | `204` | `:75-81` |
| 11 | `POST /api/documents` | **none beyond `[Authorize]`** | `[RequireInternalActor]` | `201` + `Location` → `GetById` | `:83-89` |
| 12 | `POST /api/documents/{id}/submit` | **none beyond `[Authorize]`** | `[RequireInternalActor]` | `204` | `:91-96` |
| 13 | `POST /api/documents/{id}/recommend` | `[RequirePermission(documents, Approve)]` | `[RequireInternalActor]` | `204` | `:98-104` |
| 14 | `POST /api/documents/{id}/reject` | `[RequirePermission(documents, Approve)]` | `[RequireInternalActor]` | `204` | `:106-112` |
| 15 | `POST /api/documents/{id}/publish` | `[RequirePermission(documents, Sign)]` **+ `[EnableRateLimiting(ESignaturePolicy)]`** | `[RequirePermissionPolicy(documents, Sign)]` | `204` | `:114-123` |
| 16 | `POST /api/documents/{id}/versions` | **none beyond `[Authorize]`** | `[RequireInternalActor]` | `204` | `:125-132` |
| 17 | `POST /api/documents/{id}/retire` | `[RequirePermission(documents, Void)]` | `[RequireInternalActor]` | `204` | `:134-140` |

**`FilesController`** — `[ApiController] [Route("api/files")] [Authorize]`
(`src/NT.QAMS.WebApi/Controllers/FilesController.cs:14-18`).

| # | Method + route | Gate | Behaviour | Line |
|---|---|---|---|---|
| 18 | `POST /api/files` | **none beyond `[Authorize]`**; `[RequestSizeLimit(MaxUploadBytes)]` where `MaxUploadBytes = 50 * 1024 * 1024` (`:20`) | multipart `IFormFile file`; empty → `ValidationProblem`; allow-list + 512-byte magic-byte sniff via `FileContentPolicy.Inspect`; stores the **canonical** content type, never the client's; `201 FileUploadedDto(Id, FileName, Sha256, SizeBytes)` | `:22-57` |
| 19 | `GET /api/files/{id}` | **none beyond `[Authorize]`** | `404` when absent (tenant filter + RLS scope the lookup); otherwise streams with `Content-Disposition: attachment` via `File(stream, ContentType, FileName)`. **No document-state check, no permission check, no watermark.** | `:59-72` |

**Total: 19 logical endpoints in scope** (38 rows in the approved surface once the `/api/v{version}/…`
mirror is counted).

**Upload allow-list** (`src/NT.QAMS.WebApi/Security/FileContentPolicy.cs:22-34`), exhaustive —
`.pdf` (`25 50 44 46`), `.png` (`89 50 4E 47`), `.jpg`/`.jpeg` (`FF D8 FF`), `.docx`/`.xlsx` (ZIP
`50 4B 03 04`), `.doc`/`.xls` (OLE `D0 CF 11 E0 A1 B1 1A E1`), `.csv`/`.txt` (text — refused if any NUL byte
appears in the first 512). `HeaderLength = 512` (`:14`).

### 1.7 Permission keys

`documents` is a `SignedRecordLifecycle` module (`Domain/Authorization/PermissionCatalog.cs:145`), so its
seven keys are `View, Create, Edit, Approve, Void, Sign, Export` (`:116-121`), composed by
`PermissionCatalog.Key()` (`:194`) as `{module}.{action}` lower-case.

| Key | Enforced at | Notes |
|---|---|---|
| `documents.view` | `GET /{id}/acknowledgements` only (`DocumentsController.cs:61`); frontend `document-detail.component.ts:138,324` | Every other GET in the module is ungated |
| `documents.create` | **nowhere** — no endpoint, no UI gate | Granted by the role catalogue but unenforceable. → `GAP-DOC-007` |
| `documents.edit` | issue/close controlled copy (`:71`, `:76`); frontend `:161`, `:183` | |
| `documents.approve` | recommend (`:99`), reject (`:107`); frontend `:70` | |
| `documents.void` | retire (`:135`); frontend `:99` | |
| `documents.sign` | publish (`:115`) **and** confirm-review (`:37`); command policy on `PublishDocumentCommand` (`Commands/DocumentCommands.cs:65-66`); frontend `:80`, `:118` | Only key gated at **both** HTTP and command level |
| `documents.export` | **nowhere** — the module exposes no export route | → `GAP-DOC-007` |

**Seeded system-role grants** (`src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs`):

| System role | `documents.*` granted | File:line |
|---|---|---|
| Tenant Administrator | all 7 (`PermissionCatalog.AllKeys`) | `:97-100` |
| Quality Manager | all 7 (predicate excludes only `users`/`tenant-settings`/`roles`/`organization.manage`) | `:102-118` |
| Department Head | `view, create, edit, approve, export` — **no `sign`, no `void`** | `:131` |
| Analyst | `view, create, edit, export` | `:161` |
| External Auditor | `view, export` (`ReadActions`, `:31`) | `:179-193` |

`ExternalAuditor` is additionally blocked from every `[RequireInternalActor]` document command by
`AuthorizationBehavior.cs:75` → `AUTHZ-002` / 403.

### 1.8 Persistence — tables, keys, constraints, RLS (live-verified against `ntqams`)

All five tables live in schema `qams`. `relrowsecurity = t`, `relforcerowsecurity = t`, policy
`tenant_isolation` on **all five** (measured).

| Table | PK | Unique indexes | Other indexes | CHECK | FKs | Migration |
|---|---|---|---|---|---|---|
| `controlled_document` | `(tenant_id, id)` | `ix_controlled_document_tenant_id_code (tenant_id, code)` UNIQUE; **plus redundant** `ux_controlled_document_id_tenant UNIQUE (id, tenant_id)` | `ix_controlled_document_tenant_id_status` | `ck_controlled_document_status_domain` | none outbound | `20260721215255_DocumentControl`; cycle columns `20260725054703_DocumentReviewCycles`; PK `Hardening5`; `ux_` from `Hardening4:328` |
| `document_version` | `(tenant_id, id)` | — | `ix_document_version_tenant_id_document_id` | `ck_document_version_state_domain` | `fk_document_version_controlled_document_tenant_id_document_id FOREIGN KEY (tenant_id, document_id) REFERENCES qams.controlled_document(tenant_id, id) ON DELETE CASCADE` | created `DocumentControl`; `tenant_id` backfilled and FK made tenant-composite by `Hardening4:277,373-374`; RLS enabled `Hardening4:501-504` |
| `document_acknowledgement` | `(tenant_id, id)` | `ux_doc_ack_tenant_document_version_user (tenant_id, document_id, version_label, user_id)` | `ix_document_acknowledgement_tenant_id_user_id` | none | **none** | `20260726204141_DocumentAcknowledgement`; index renamed by `Hardening1:39-42` |
| `document_controlled_copy` | `(tenant_id, id)` | `ux_doc_copy_tenant_document_number (tenant_id, document_id, copy_number)` | `ix_document_controlled_copy_tenant_id_status` | `ck_document_controlled_copy_status_domain` | **none** | `20260726214512_DocumentControlledCopy` |
| `file_reference` | `(tenant_id, id)` | — | `ix_file_reference_tenant_id_sha256` | `ck_file_reference_sha256_sha256 CHECK (sha256 ~ '^[0-9a-f]{64}$')` | none | `20260721215255_DocumentControl` |

**Columns of note (live `information_schema`):**

- `controlled_document.review_cycle_months integer NOT NULL DEFAULT 0` — the migration default is **0**,
  the domain default is **24** (`ControlledDocument.cs:69`), and there is no backfill.
  **Measured in dev:** `review_cycle_months` = `0` on **2** rows, `12` on 2, `24` on 2. → `GAP-DOC-008`.
- `controlled_document.next_review_due date NULL`, `review_due_raised boolean NOT NULL DEFAULT false`.
- `document_version.change_summary` and `rejection_reason` are `text` (widened from `varchar(1000)` by
  `Hardening1:403,414`); the 1000-char bound now lives only in the validators
  (`Commands/DocumentCommands.cs:25`, `:76`) and the Angular forms (`document-detail.component.ts:267,274`).
- `document_acknowledgement.document_code varchar(60)` / `version_label varchar(20)`;
  `document_controlled_copy.holder varchar(200)`, `document_code varchar(60)`, `version_label varchar(20)`.
- `file_reference.file_name varchar(260)`, `content_type varchar(150)`, `sha256 varchar(64)`,
  `storage_key varchar(120)`.

**Triggers:** measured — **zero** non-internal triggers on all five tables. The
`qams.reject_frozen_mutation()` freeze trigger (conventions §2, "Signed-record immutability") does **not**
cover any document table: a published, e-signed `document_version` row is mutable at the SQL layer.
→ `GAP-DOC-009`.

**RLS policy shape** (`20260726204141_DocumentAcknowledgement.cs:52-65`, identical for
`document_controlled_copy` at `:57-70` and applied to `document_version` at `Hardening4:501-504`):
`USING/WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid OR current_setting('app.bypass_rls', true) = 'on')`.
`controlled_document` and `file_reference` were created in `20260721215255_DocumentControl:115-122` with a
weaker `USING`-only, non-FORCE policy; the measured live state is FORCE + `tenant_isolation`, brought to
parity by `ActivateForcedTenantRls` / `Hardening2_RlsGapClosure`.

**Concurrency:** `xmin` token, no `row_version` column (conventions §2) → `CONCURRENCY-409` on a stale write.

### 1.9 Background processing and notifications

| Element | Behaviour | File:line |
|---|---|---|
| Sweep query | `db.Documents.IgnoreQueryFilters().Where(Status == Published && !ReviewDueRaised && NextReviewDue != null && NextReviewDue <= today)`, then `MarkReviewDueIfReached(today)` on each | `Infrastructure/Jobs/ScheduledSweepService.cs:135-141` |
| Sweep cadence | `BackgroundService`, **1-hour interval**, 15-second startup delay, leader-elected, cross-tenant elevated (conventions §2) | — |
| `DocumentReviewDuePolicy` | resolves the tenant from the document row with `IgnoreQueryFilters`, sets the tenant, dispatches `DOC_REVIEW_DUE`, then opens a `WorkTask` — title `"Periodic review due: {Code} — {Title}"`, `SubjectRef = "DOCREV:{Code}"`, `assigneeUserId: null`, `assigneeRole: "QualityManager"`, `dueDate = today + 30 days`. Idempotent: skips when an open `Pending` task with the same `SubjectRef` exists. | `Application/DocumentControl/DocumentReviewDuePolicy.cs:27-58` |
| `DOC_PUBLISHED` rule | seeded on tenant provisioning: subject `"Document published: {ref} v{version}"`, body `"{title} ({ref}) version {version} is now the controlled copy."`, recipients `QualityManager,TenantAdmin`, email on | `Notifications/NotificationPolicies.cs:141-142`, seed loop `:158-163` |
| `DOC_REVIEW_DUE` rule | **key declared** (`NotificationPolicies.cs:43`) but **absent from the default seed list** (`:138-156`). `NotificationDispatcher` returns early when `rules.Count == 0` (`NotificationDispatcher.cs:44-47`) → **no notification is produced**. Confirmed in dev: `SELECT DISTINCT event_key FROM qams.notification_rule` returns 8 keys, and `DOC_REVIEW_DUE` is not one of them. → `GAP-DOC-010` |
| Recipient resolution | by the **structural** `UserRole` enum (`u.Role.ToString()`), not by tenant-defined role or permission | `NotificationDispatcher.cs:57-60` |
| Dispatch idempotency | one dispatch per `SourceEventId` | `NotificationDispatcher.cs:35-38` |

### 1.10 Frontend surface

| Concern | Implementation | File:line |
|---|---|---|
| Routes | `/documents` (list) and a child detail route | `frontend/src/app/app.routes.ts:62-67` |
| Facade | signal-based; owns list/detail/loading/error, load-more pager over the API-004 envelope; `create` and `draftNewVersion` are **upload-then-persist** two-call flows | `features/documents/documents.facade.ts:42-92` |
| Stepper | `flowSteps = ['Draft','Published','Obsolete']` — the version-level `UnderReview`/`Approved` states are not on the stepper | `document-detail.component.ts:246` |
| Publish form | `password` required; `pin` `Validators.pattern(/^\d{4}$/)` and `maxlength="4"` — **client-only**; the backend enforces no PIN length | `document-detail.component.ts:268-271`, `:85` |
| Action gating | `documents.approve` (`:70`), `documents.sign` (`:80`, `:118`), `documents.void` (`:99`), `documents.edit` (`:161`, `:183`), `documents.view` (`:138`, `:324`) | — |
| Version table | lists **every** version including `Obsolete`, each with a live download link `facade.downloadUrl(v.fileId)`; **no obsolescence marking on the link** | `:50-57` |
| Copy register | shows `#`, `v{versionLabel}`, holder, status, issued date; Return/Destroy buttons only while `Issued` — **no comparison against the current published label** | `:175-189` |
| Acknowledgement | own-state banner + (with `documents.view`) full coverage table | `:125-155` |
| Error surfacing | `problem+json` `title` shown verbatim; the domain `code` is not surfaced to the user | `documents.facade.ts:118-123` |

### 1.11 Existing automated coverage (baseline — do not duplicate)

| File | Tests |
|---|---|
| `tests/NT.QAMS.Domain.UnitTests/DocumentControl/ControlledDocumentTests.cs` (171 lines) | code normalisation + v1.0 draft; full lifecycle with reviewer/approver recorded; author cannot recommend or approve; revision obsoletes predecessor atomically; major bump resets minor; one in-flight at a time; rejection returns to draft with reason; retire obsoletes and blocks new versions; publish arms the cycle; review flag raises once and confirm re-arms; periodic review only for published |
| `tests/NT.QAMS.Domain.UnitTests/DocumentControl/DocumentAcknowledgementTests.cs` (48 lines) | not enumerated in this pass — `[RNV]` for any claim about its contents |
| `tests/NT.QAMS.Domain.UnitTests/DocumentControl/DocumentControlledCopyTests.cs` (58 lines) | not enumerated in this pass — `[RNV]` |
| `tests/NT.QAMS.WebApi.FunctionalTests/AuditorDenyMatrixTests.cs:83,102` | auditor `GET /api/documents` → 200; auditor "create document" denied |
| `tests/NT.QAMS.WebApi.FunctionalTests/RoleEndpointMatrixTests.cs:44` | `GET /api/documents/{id}/controlled-copies` reachable by all five roles |
| `tests/NT.QAMS.IntegrationTests/OwnedChildTenancyTests.cs` | owned-child tenancy (includes `document_version`) |

---

## 2. Divergences from the commissioning brief

| # | What the brief assumes | What the code actually does | file:line | Gap ID |
|---|---|---|---|---|
| 1 | An **"OBSOLETE - UNCONTROLLED"** watermark is stamped on printed/downloaded superseded documents | No watermark implementation exists anywhere. An exhaustive search of `src/`, `frontend/src/` and the repo for `OBSOLETE - UNCONTROLLED` / `watermark` / `uncontrolled` returns **one** hit: the XML doc-comment phrase "prevents uncontrolled paper from circulating". `GET /api/files/{id}` streams the raw stored bytes untouched. | `src/NT.QAMS.Domain/DocumentControl/DocumentControlledCopy.cs:14` (comment only); `WebApi/Controllers/FilesController.cs:59-72` | `GAP-DOC-011` |
| 2 | Publishing requires a **4-digit** e-signature PIN | The backend enforces **no digit count and no length** on the PIN — `ESignatureService` only compares against `signer.PinHash`. The `/^\d{4}$/` rule exists solely in the Angular form; the contract's own XML comment repeats the false claim. | `Infrastructure/Compliance/ComplianceLedgerServices.cs:111-115`; `frontend/.../document-detail.component.ts:270`; comment `Contracts/DocumentControl/DocumentContracts.cs:11` | `GAP-DOC-012` |
| 3 | The document lifecycle is **role-gated** (`DOC.APPROVE`-style privilege codes) | Privilege codes are `{module}.{action}` (`documents.approve`, `documents.sign`, `documents.void`, `documents.edit`, `documents.view`); the brief's codes do not exist. Gating is `[RequirePermission]` at the endpoint plus `CommandPolicyAttribute` at the command. | `Domain/Authorization/PermissionCatalog.cs:145,194`; `WebApi/Controllers/DocumentsController.cs:37-135` | conventions §2 records the naming divergence globally; no new gap |
| 4 | Every lifecycle transition is privilege-gated | **Six routes carry no permission attribute at all** — create, submit, draft-new-version, acknowledge, and three GETs (list, detail, controlled-copies, signatures, my-acknowledgement). Any authenticated tenant user may create a document, submit it, draft a revision and read every version. | `DocumentsController.cs:24,31,45,50,55,66,83,91,125` | `GAP-DOC-013` |
| 5 | A superseded version is withdrawn from circulation | Superseded versions stay fully readable and downloadable by **any authenticated tenant user** — `GET /api/documents/{id}` returns every version with its `fileId` and no gate; `GET /api/files/{id}` streams the bytes with no permission check and no document-state check. | `DocumentQueries.cs:59-68`; `DocumentsController.cs:31-33`; `FilesController.cs:59-72` | `GAP-DOC-014` |
| 6 | Retiring a document ends its life | `Retire` obsoletes only the published version and leaves the in-flight version untouched; `Publish` then sets `Status = Published` unconditionally, resurrecting the retired document. | `ControlledDocument.cs:229-245`, `:169` | `GAP-DOC-004` |
| 7 | Reviewer and approver are distinct people | Only **author ≠ reviewer** (`SOD-DOC-001`) and **author ≠ approver** (`SOD-DOC-002`) are enforced. `RecommendedBy` is never compared with the publishing actor, so one non-author may both recommend and publish. | `ControlledDocument.cs:117-129`, `:151-173` | `GAP-DOC-003` |
| 8 | Signed/published records are immutable at the database layer | The `reject_frozen_mutation()` BEFORE UPDATE/DELETE trigger covers the 12 analytical study roots + `uncertainty_budget`; **measured: zero triggers on `controlled_document`, `document_version`, `document_acknowledgement`, `document_controlled_copy`, `file_reference`.** | live `pg_trigger` query; conventions §2 "Signed-record immutability" | `GAP-DOC-009` |
| 9 | Periodic review of controlled documents is a stated requirement | URS section D stops at `URS-029`; **no URS covers the periodic-review cycle**, the sweep, the `DOC_REVIEW_DUE` event or the 30-day Quality-Manager task. The whole review-cycle feature is `[ID]` — implementation-derived. | `docs/validation/01-User-Requirements-Specification.md:63-67`; feature at `ControlledDocument.cs:179-205` | `GAP-DOC-001` |
| 10 | A due periodic review notifies the Quality Manager | The `WorkTask` is created, but the `DOC_REVIEW_DUE` notification rule is **not seeded**, so `NotificationDispatcher` returns before writing any dispatch row. Confirmed in dev: 8 distinct `event_key` values, none of them `DOC_REVIEW_DUE`. | `NotificationPolicies.cs:43` vs seed list `:138-156`; `NotificationDispatcher.cs:44-47`; live `qams.notification_rule` | `GAP-DOC-010` |
| 11 | The copy register makes a copy of a superseded version "visibly obsolete" (the aggregate's own doc-comment) | Nothing computes or stores that. `ControlledCopyDto` returns only `VersionLabel` + `Status`; the UI renders both without comparing against the current published label. | comment `DocumentControlledCopy.cs:12-13`; DTO `DocumentContracts.cs:44-46`; UI `document-detail.component.ts:175-189` | `GAP-DOC-005` |
| 12 | Out-of-range review cadences are rejected | `ReviewCycleMonths` outside `(0, 120]` is **silently coerced to 24** with no error and no audit note; the command validator has no rule for the field. | `ControlledDocument.cs:104`; validator `Commands/DocumentCommands.cs:15-27` | `GAP-DOC-015` |
| 13 | One domain code maps to one HTTP status | `DOC-014` returns **422** from the publish handler's "no version awaiting approval" branch (a `DomainException`) but **409** from the aggregate's identically-coded `InvalidStateTransitionException`. | `Commands/DocumentCommands.cs:130-131` vs `ControlledDocument.cs:153` → `:250` | `GAP-DOC-016` |
| 14 | Acknowledgements and controlled copies are children of the document | Neither table carries a foreign key to `controlled_document` — measured `pg_constraint` shows no FK on `document_acknowledgement` or `document_controlled_copy`. Only `document_version` has the tenant-composite FK. | live `pg_constraint`; `20260726204141_…:32-35`, `20260726214512_…:37-40` | `GAP-DOC-017` |
| 15 | Issuing a controlled copy is an auditable act | `DocumentControlledCopy.Issue` raises no domain event, so no audit-ledger row is chained for issue — only closure raises `ControlledCopyClosed`. A `FieldChangeRecord` "Created" row is written by the interceptor. | `DocumentControlledCopy.cs:40-65` vs `:84`; `FieldChangeInterceptor.cs:67-69` | `GAP-DOC-006` |
| 16 | Copy numbering is safe under concurrency | `IssueControlledCopyHandler` computes `lastCopyNumber + 1` in a read-then-write with no lock; two concurrent issues collide on `ux_doc_copy_tenant_document_number`. No `23505` / `PostgresException` handler exists in the request pipeline, so the collision surfaces as an unhandled **500**, not a domain code. | `ControlledCopySlice.cs:32-40`; absence verified by repo-wide grep for `23505`/`UniqueViolation` (only `StartupSeeding.cs:196` matches, for `42P01`/`3D000`) | `GAP-DOC-018` |
| 17 | Hangfire schedules the review sweep | Hangfire is not present; the sweep is `ScheduledSweepService`, a `BackgroundService` on a 1-hour interval (conventions §1). Author against the implemented mechanism, label `Implementation-derived`. | `Infrastructure/Jobs/ScheduledSweepService.cs:135-141` | conventions §1 covers this; no new gap |

---

## 3. State-transition matrices

### 3.1 Composite lifecycle state key

A document's observable state is the pair **(`DocumentStatus`, in-flight `VersionState`)**, because
`InFlightVersion` is `SingleOrDefault(State is Draft or UnderReview or Approved)`
(`ControlledDocument.cs:81-82`) and `Status` is a separate field. Reachable states:

| Key | `Status` | In-flight version | How reached |
|---|---|---|---|
| **S1** | `Draft` | `Draft` (v1.0) | `Create` (`:98-107`) |
| **S2** | `Draft` | `UnderReview` | S1 + `SubmitForReview` |
| **S3** | `Draft` | `Approved` | S2 + `Recommend` |
| **S4** | `Published` | *none* | S3/S7 + `Publish` |
| **S5** | `Published` | `Draft` (v(n+1)) | S4 + `DraftNewVersion` |
| **S6** | `Published` | `UnderReview` | S5 + `SubmitForReview` |
| **S7** | `Published` | `Approved` | S6 + `Recommend` |
| **S8** | `Obsolete` | *none* | S4 + `Retire`, or S1..S3 + `Retire` where no version was ever published **and** the in-flight version was published/rejected away — in practice reached from S4 |
| **S9** | `Obsolete` | `Draft` \| `UnderReview` \| `Approved` | `Retire` from S1/S2/S3/S5/S6/S7 — **`Retire` never touches the in-flight version** (`:236-241`) |

### 3.2 Document lifecycle — every state × every operation

Cells give **outcome** and, for refusals, the **real guard code + HTTP status**. Codes verified at the
throw site; statuses derived from `DomainExceptionHandler.cs:26-82`.

| Operation → | **S1** Draft/Draft | **S2** Draft/UnderReview | **S3** Draft/Approved | **S4** Published/— | **S5** Pub/Draft | **S6** Pub/UnderReview | **S7** Pub/Approved | **S8** Obsolete/— | **S9** Obsolete/in-flight |
|---|---|---|---|---|---|---|---|---|---|
| `SubmitForReview` (`:110`) | ✅ → S2, raises `DocumentSubmittedForReview` | ❌ `DOC-010` 409 "Cannot submit for review a version in state UnderReview." | ❌ `DOC-010` 409 (…state Approved) | ❌ `DOC-010` 409 "No version available to submit for review." | ✅ → S6 | ❌ `DOC-010` 409 | ❌ `DOC-010` 409 | ❌ `DOC-010` 409 (no in-flight) | ⚠️ **✅ succeeds** on an obsolete document — no status guard (`:110-115`) → `GAP-DOC-004` |
| `Recommend(actor)` (`:117`) | ❌ `DOC-011` 409 (…state Draft) | ✅ → S3 if `actor != AuthorId`; ❌ `SOD-DOC-001` **422** if `actor == AuthorId` | ❌ `DOC-011` 409 (…state Approved) | ❌ `DOC-011` 409 (no in-flight) | ❌ `DOC-011` 409 | ✅ → S7 / ❌ `SOD-DOC-001` 422 | ❌ `DOC-011` 409 | ❌ `DOC-011` 409 | ⚠️ **✅ succeeds** when in-flight is `UnderReview` → `GAP-DOC-004` |
| `RejectVersion(actor, reason)` (`:131`) | ❌ `DOC-012` 409 "Cannot reject a version in state Draft." | ✅ → S1, `State = Draft`, `RejectionReason` set, raises `DocumentVersionRejected`; ❌ `DOC-013` 422 on blank reason reaching the domain (validator returns **400** first, `Commands/DocumentCommands.cs:76`) | ✅ → S1 (`Approved` is rejectable, `:136`) | ❌ `DOC-012` 409 "No version is awaiting review or approval." | ❌ `DOC-012` 409 (state Draft) | ✅ → S5 | ✅ → S5 | ❌ `DOC-012` 409 | ⚠️ ✅ when in-flight is `UnderReview`/`Approved` |
| `Publish(actor)` (`:151`) | ❌ handler `DOC-014` **409** "Cannot publish a version in state Draft." (`Commands/DocumentCommands.cs:138`) | ❌ `DOC-014` 409 (state UnderReview) | ✅ e-sign then → S4: predecessor (none) untouched, `Status = Published`, `NextReviewDue = at + ReviewCycleMonths`, `ReviewDueRaised = false`; ❌ `SOD-DOC-002` **422** if `actor == AuthorId` (pre-checked **before** the signature, `:142-146`) | ❌ handler `DOC-014` **422** "No version is awaiting approval." (`:130-131`) — **note the 422/409 split**, `GAP-DOC-016` | ❌ `DOC-014` 409 | ❌ `DOC-014` 409 | ✅ → S4: predecessor flips to `Obsolete` + `DocumentVersionObsoleted`, new version published, cycle re-armed | ❌ `DOC-014` 422 (no in-flight) | ⚠️ **✅ resurrects the document to `Published`** when in-flight is `Approved` (`:166-172`) → `GAP-DOC-004` |
| `DraftNewVersion(fileId, summary, bump, author)` (`:207`) | ❌ `DOC-016` **422** "A version is already in progress…" | ❌ `DOC-016` 422 | ❌ `DOC-016` 422 | ✅ → S5, no domain event raised | ❌ `DOC-016` 422 | ❌ `DOC-016` 422 | ❌ `DOC-016` 422 | ❌ `DOC-015` 409 "A retired document cannot receive new versions." | ❌ `DOC-015` 409 (status check precedes the in-flight check, `:209-217`) |
| `Retire(actor)` (`:229`) | ✅ → S9 (`Status = Obsolete`, in-flight **left alone**, no `DocumentVersionObsoleted` since nothing is published) | ✅ → S9 | ✅ → S9 | ✅ → S8, published flips to `Obsolete` + `DocumentVersionObsoleted`, then `DocumentRetired` | ✅ → S9 | ✅ → S9 | ✅ → S9 | ❌ `DOC-018` 409 "Document is already obsolete." | ❌ `DOC-018` 409 |
| `ConfirmPeriodicReview(reviewer, on)` (`:195`) | ❌ `DOC-020` 409 | ❌ `DOC-020` 409 | ❌ `DOC-020` 409 | ✅ `NextReviewDue = on + cycle`, `ReviewDueRaised = false`, raises `DocumentReviewConfirmed` | ✅ | ✅ | ✅ | ❌ `DOC-020` 409 | ❌ `DOC-020` 409 |
| `MarkReviewDueIfReached(today)` (`:179`) | silent no-op (`Status != Published`) | no-op | no-op | ✅ raises `DocumentReviewDue` **iff** `!ReviewDueRaised && NextReviewDue <= today`; otherwise silent no-op | same as S4 | same as S4 | same as S4 | no-op | no-op |
| `AcknowledgeDocument` (`DocumentAcknowledgementSlice.cs:19`) | ❌ `ACK-010` 422 "Only a published document can be acknowledged." | ❌ `ACK-010` 422 | ❌ `ACK-010` 422 | ✅ new receipt, or the **existing receipt id** when already acknowledged for this label (`:32-41`) | ✅ (pins to the *published* label, not the draft) | ✅ | ✅ | ❌ `ACK-010` 422 (`PublishedVersion` is null after retire) | ❌ `ACK-010` 422 |
| `IssueControlledCopy` (`ControlledCopySlice.cs:21`) | ❌ `CCP-020` 422 | ❌ `CCP-020` 422 | ❌ `CCP-020` 422 | ✅ copy `n+1` pinned to the published label | ✅ | ✅ | ✅ | ❌ `CCP-020` 422 | ❌ `CCP-020` 422 |
| `GET /api/documents/{id}` | ✅ 200 — **all** versions, all `fileId`s, no permission gate | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

**Legend:** ✅ permitted · ❌ refused with the stated code · ⚠️ permitted **but should not be** (defect).

### 3.3 Version-state machine (`document_version.state`)

| From ↓ / Operation → | `SubmitForReview` | `Recommend` | `RejectVersion` | `Publish` | new `Publish` of a successor | `Retire` |
|---|---|---|---|---|---|---|
| `Draft` | → `UnderReview` | ❌ `DOC-011` | ❌ `DOC-012` | ❌ `DOC-014` | n/a — not the published version | untouched |
| `UnderReview` | ❌ `DOC-010` | → `Approved` (+ `RecommendedBy`, `RecommendedAtUtc`) or `SOD-DOC-001` | → `Draft` (+ `RejectionReason`) | ❌ `DOC-014` | n/a | untouched |
| `Approved` | ❌ `DOC-010` | ❌ `DOC-011` | → `Draft` | → `Published` (+ `ApprovedBy`, `ApprovedAtUtc`) or `SOD-DOC-002` | n/a | untouched |
| `Published` | ❌ (not in-flight) | ❌ | ❌ | ❌ | → `Obsolete` (+ `DocumentVersionObsoleted`) | → `Obsolete` (+ `DocumentVersionObsoleted`) |
| `Obsolete` | ❌ | ❌ | ❌ | ❌ | — terminal | — terminal |
| `Rejected` | **unreachable** — no code path assigns it (`GAP-DOC-002`) | | | | | |

`RejectionReason` is set on rejection (`:147`) and **never cleared** on a later resubmission — a version
that was rejected, fixed and published retains the stale rejection text in `DocumentVersionDto.RejectionReason`
(`Queries/DocumentQueries.cs:66`), which the UI renders next to the change summary
(`document-detail.component.ts:54`). Case authors should assert this behaviour as observed, not as intended.

### 3.4 Controlled-copy state machine

| From ↓ / Operation → | `Close("Returned")` | `Close("Destroyed")` | `Close("Issued")` | `Close("<garbage>")` | second `Close` |
|---|---|---|---|---|---|
| `Issued` | ✅ → `Returned`, `ClosedBy`/`ClosedAtUtc` set, raises `ControlledCopyClosed` | ✅ → `Destroyed`, same | ❌ `CCP-003` **422** (`DocumentControlledCopy.cs:70-73`) | ❌ `CCP-003` 422 — `Enum.TryParse` fails in the slice (`ControlledCopySlice.cs:52-55`) | n/a |
| `Returned` | ❌ `CCP-010` **409** "Only an issued copy can be returned or destroyed (current: Returned)." | ❌ `CCP-010` 409 | ❌ `CCP-003` 422 (outcome check precedes the status check, `:70` before `:75`) | ❌ `CCP-003` 422 | ❌ `CCP-010` 409 |
| `Destroyed` | ❌ `CCP-010` 409 (current: Destroyed) | ❌ `CCP-010` 409 | ❌ `CCP-003` 422 | ❌ `CCP-003` 422 | ❌ `CCP-010` 409 |

**Order of guards matters and is testable:** `Close` validates the *outcome* first (`:70-73`), the
*current status* second (`:75-79`). Closing an already-`Destroyed` copy as `"Issued"` therefore returns
`CCP-003`/422, not `CCP-010`/409.

`Enum.TryParse<ControlledCopyStatus>(c.Outcome, ignoreCase: true, …)` (`ControlledCopySlice.cs:52`) accepts
`"returned"`, `"RETURNED"`, `"Destroyed"`, and — because `TryParse` also accepts numeric text — `"1"` and
`"2"` parse to `Returned`/`Destroyed`, while `"0"` parses to `Issued` and is then refused by `CCP-003`.
Author boundary cases against this exact behaviour.

**Not modelled anywhere:** a copy is never auto-transitioned when its document is revised or retired. A
`v1.0` copy stays `Issued` forever after `v1.1` publishes. → `GAP-DOC-005`.

---

## 4. Decision tables

### 4.1 DT-1 — Recommend (department-head review)

Conditions read from `DocumentsController.cs:98-104`, `AuthorizationBehavior.cs:44-85`,
`ControlledDocument.cs:117-129`.

| # | Authenticated? | `documents.approve`? | `UserRole == ExternalAuditor`? | In-flight state | `actor == AuthorId`? | Outcome |
|---|---|---|---|---|---|---|
| 1 | No | — | — | — | — | `401` (framework `[Authorize]` / `FallbackPolicy`) |
| 2 | Yes | No | any | any | any | `403 AUTHZ-403` — endpoint filter refuses before the command runs |
| 3 | Yes | Yes | Yes | any | any | `403 AUTHZ-002` — `[RequireInternalActor]` (`AuthorizationBehavior.cs:75`). *Unreachable via the seeded catalogue: `ExternalAuditor` is not granted `documents.approve` (`SystemRoleCatalog.cs:179-193`) — reachable only through a custom tenant role.* |
| 4 | Yes | Yes | No | none | — | `409 DOC-011` "No version available to recommend." |
| 5 | Yes | Yes | No | `Draft` | — | `409 DOC-011` "Cannot recommend a version in state Draft." |
| 6 | Yes | Yes | No | `Approved` | — | `409 DOC-011` "…state Approved." |
| 7 | Yes | Yes | No | `UnderReview` | Yes | `422 SOD-DOC-001` |
| 8 | Yes | Yes | No | `UnderReview` | No | `204` — state → `Approved`, `RecommendedBy`/`RecommendedAtUtc` stamped, `DocumentRecommended` raised |

### 4.2 DT-2 — Publish (approval + Part 11 signing ceremony), including SoD

Conditions read from `DocumentsController.cs:114-123`, `Commands/DocumentCommands.cs:121-162`,
`ComplianceLedgerServices.cs:90-131`, `ControlledDocument.cs:151-173`.
**Guard order is load-bearing** — every precondition is checked *before* the signature is minted, so a
signature never exists for a publish that then fails (`Commands/DocumentCommands.cs:133-135`).

| # | `documents.sign`? | Rate-limit partition | In-flight state | `actor == AuthorId`? | Account locked? | Password | PIN set + correct? | Outcome | Signature minted? |
|---|---|---|---|---|---|---|---|---|---|
| 1 | No | — | — | — | — | — | — | `403 AUTHZ-403` (endpoint filter) | No |
| 2 | Yes, but command policy denies | — | — | — | — | — | — | `403 AUTHZ-002` (`RequirePermissionPolicy`, `Commands/DocumentCommands.cs:65-66`) | No |
| 3 | Yes | **exceeded** | — | — | — | — | — | `429` (`ESignaturePolicy`, `DocumentsController.cs:118`) | No |
| 4 | Yes | ok | none | — | — | — | — | `422 DOC-014` "No version is awaiting approval." | No |
| 5 | Yes | ok | `Draft`/`UnderReview` | — | — | — | — | `409 DOC-014` "Cannot publish a version in state {State}." | No |
| 6 | Yes | ok | `Approved` | **Yes** | — | — | — | `422 SOD-DOC-002` | **No** — pre-checked at `:142-146` |
| 7 | Yes | ok | `Approved` | No | **Yes** | — | — | `422 SIG-003`; `ESIGN_LOCKED` written to `audit.security_event` | No |
| 8 | Yes | ok | `Approved` | No | No | **wrong** | — | `422 SIG-002`; `ESIGN_FAILED` (`bad-password:DOC:{id:N}`) + `RegisterFailedLogin` | No |
| 9 | Yes | ok | `Approved` | No | No | correct | **not set or wrong** | `422 SIG-001`; `ESIGN_FAILED` (`bad-pin:DOC:{id:N}`) + `RegisterFailedLogin` | No |
| 10 | Yes | ok | `Approved` | No | No | correct | correct | `204` | **Yes** — `SignatureRecord(Meaning = "Approved and published {Code} v{label}", SubjectRef = "DOC:{id:N}", ContentHash = file SHA-256)` (`:154-157`) |
| 11 | signer row missing | ok | `Approved` | No | — | — | — | `404 SIG-404` | No |

**Post-conditions of row 10** (each independently assertable):
`document_version.state` = `Published`, `approved_by` = actor, `approved_at_utc` = `IClock.UtcNow`;
predecessor (if any) → `Obsolete` + `DocumentVersionObsoleted`;
`controlled_document.status` = `Published`, `next_review_due` = publish date + `review_cycle_months`,
`review_due_raised` = `false`;
`DocumentPublished` → outbox → `DOC_PUBLISHED` notification to `QualityManager,TenantAdmin` and an
`audit.audit_ledger` chain entry;
one `SignatureRecord` visible at `GET /api/documents/{id}/signatures`;
`FieldChangeRecord` rows for every modified property, PIN/password redacted by
`FieldChangeInterceptor.cs:33` (`Sensitive` fragments `password, secret, pin, hash, token`).

**SoD reach summary:** enforced pairs are **author ≠ recommender** and **author ≠ approver**. The pair
**recommender ≠ approver** is NOT enforced (`GAP-DOC-003`), and neither `Retire` nor
`ConfirmPeriodicReview` carries any SoD guard.

### 4.3 DT-3 — Who may read an obsolete version (and download its bytes)

Read from `DocumentsController.cs:31-33` (no attribute), `DocumentQueries.cs:51-69` (returns **every**
version with its `fileId`), `FilesController.cs:59-72` (no attribute, no state check),
`AuthorizationBehavior.cs:44-47` (queries bypass the policy gate entirely),
`Program.cs:134-135` (`FallbackPolicy = RequireAuthenticatedUser`).

| # | Actor | `documents.view`? | Same tenant? | `GET /api/documents/{id}` (obsolete version metadata) | `GET /api/files/{fileId}` (obsolete bytes) | Watermark applied? |
|---|---|---|---|---|---|---|
| 1 | Anonymous | — | — | `401` | `401` | n/a |
| 2 | Authenticated, **no** `documents.*` grant at all | No | Yes | **`200` — full version list incl. `Obsolete` + `fileId`** | **`200` — bytes streamed as an attachment** | **No** |
| 3 | Analyst (`view, create, edit, export`) | Yes | Yes | `200` | `200` | No |
| 4 | Department Head | Yes | Yes | `200` | `200` | No |
| 5 | Quality Manager / Tenant Admin | Yes | Yes | `200` | `200` | No |
| 6 | External Auditor (`view, export`) | Yes | Yes | `200` (query, not a command — `AUTHZ-002` never applies) | `200` | No |
| 7 | Any actor | any | **No** (other tenant) | `404 DOC-404` — EF global query filter + `tenant_isolation` RLS | `404` — same | n/a |
| 8 | Platform admin with no tenant context | — | n/a | tenant GUC nil → fail-closed; **`[RNV]` — not exercised in this pass** | `[RNV]` | n/a |

**Verdict for the watermark question (searched exhaustively, as instructed):** there is **no
"OBSOLETE - UNCONTROLLED" watermark implementation anywhere in the repository.** A case-insensitive search
of `src/`, `frontend/src/` and the whole tree for `OBSOLETE - UNCONTROLLED`, `OBSOLETE-UNCONTROLLED`,
`watermark` and `uncontrolled` returns exactly two hits: the XML doc-comment in
`src/NT.QAMS.Domain/DocumentControl/DocumentControlledCopy.cs:14` ("prevents uncontrolled paper from
circulating") and the line in `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md:118` that already records
its absence. No PDF post-processing, no `QuestPDF` overlay on the download path, no CSS print overlay in
the Angular version table. Any watermark case must be `[GD]` against `GAP-DOC-011`. **Do not author it as
an executable expectation.**

### 4.4 DT-4 — Acknowledgement recording

From `DocumentAcknowledgementSlice.cs:19-49` and `DocumentAcknowledgement.cs:28-56`.

| # | Authenticated? | `ExternalAuditor`? | Document exists? | Has a published version? | Prior receipt for **this** label + user? | Outcome |
|---|---|---|---|---|---|---|
| 1 | No | — | — | — | — | `401` (framework) / `401 AUTH-003` if reached |
| 2 | Yes | Yes | — | — | — | `403 AUTHZ-002` — `[RequireInternalActor]` (`DocumentAcknowledgementSlice.cs:13`) |
| 3 | Yes | No | No | — | — | `404 DOC-404` |
| 4 | Yes | No | Yes | No | — | `422 ACK-010` |
| 5 | Yes | No | Yes | Yes | Yes | `200 { id }` — **the existing receipt id**, no new row, no event (`:32-41`) |
| 6 | Yes | No | Yes | Yes | No | `200 { id }` — new row, `DocumentAcknowledged` raised, unique index `ux_doc_ack_tenant_document_version_user` satisfied |
| 7 | Yes | No | Yes | Yes — but a **newer** version has since published | prior receipt is for the older label | `200 { id }` — **new** receipt for the new label; the old receipt persists and no longer counts as coverage (`URS-028`, "revising re-opens acknowledgement") |

Note: no endpoint permission gate on `POST /{id}/acknowledge` — the only gate is `[RequireInternalActor]`.
`GET /{id}/my-acknowledgement` is likewise ungated; `GET /{id}/acknowledgements` requires `documents.view`.

### 4.5 DT-5 — File upload acceptance (`POST /api/files`)

From `FilesController.cs:22-57` and `FileContentPolicy.cs:42-75`.

| # | Body | Extension | First 512 bytes | Size | Outcome |
|---|---|---|---|---|---|
| 1 | absent or `Length == 0` | — | — | — | `400` `ValidationProblem` "A non-empty file is required." |
| 2 | present | not on the allow-list (or none) | — | — | `422 FILE-415` "File type '{ext}' is not on the evidence allow-list (…)" |
| 3 | present | `.pdf` | not `25 50 44 46` | — | `422 FILE-415` "The content does not match the .pdf file signature." |
| 4 | present | `.csv`/`.txt` | contains a `0x00` byte | — | `422 FILE-415` "The content is binary, not the .csv text format it claims." |
| 5 | present | `.docx` | ZIP magic `50 4B 03 04` | — | `201` — stored content type is the **canonical** `application/vnd.openxmlformats-…wordprocessingml.document`, never the client's claim (`:51`) |
| 6 | present | allowed + matching | — | `> 50 MiB` | `413` from `[RequestSizeLimit(52428800)]` (`:20,23`) — **`[RNV]`: the exact status/body was not exercised in this pass** |
| 7 | present | allowed + matching | — | ok | `201 FileUploadedDto`; `Location` → `GET /api/files/{id}`; `sha256` must satisfy `ck_file_reference_sha256_sha256 ~ '^[0-9a-f]{64}$'` |
| 8 | present | allowed + matching | — | ok, but **no tenant context** | `422 TENANT-000` (`:32-33`) |

---

## 6. UAT scenarios (Gherkin)

Business-readable, executed by a quality user against the SPA at `localhost:4200/t/demo-lab`. Each maps to
the requirement named in its tag.

```gherkin
@URS-025 @URS-026 @TC-DOC-UAT-001
Scenario: A new SOP reaches controlled status through review and signed approval
  Given Nadia (Analyst) is signed in to the demo-lab workspace
  And no controlled document exists with the code "SOP-CAL-045"
  When Nadia uploads "calibration-procedure.pdf", enters the code "SOP-CAL-045",
       the title "Balance calibration", the category "SOP" and creates the document
  Then the document appears in the register with status "Draft" and version "1.0"
  When Nadia submits version 1.0 for review
  And Omar (Department Head) recommends it
  Then version 1.0 shows state "Approved" and records Omar as the recommender
  When Layla (Quality Manager) opens the document, enters her account password and her
       signature PIN, and publishes it
  Then the document status becomes "Published", version 1.0 shows state "Published",
       and the signature panel lists Layla with the meaning
       "Approved and published SOP-CAL-045 v1.0"

@URS-025 @TC-DOC-UAT-002
Scenario: An author may not sign off their own procedure
  Given Nadia (Analyst) authored version 1.0 of "SOP-CAL-045"
  And Nadia has been granted the document approval and signing privileges
  When Nadia submits version 1.0 and then attempts to recommend it herself
  Then the system refuses the recommendation and states that the author cannot
       review their own document
  And when Nadia attempts to publish it herself the system refuses on the same
       segregation-of-duties ground and no electronic signature is recorded

@URS-027 @TC-DOC-UAT-003
Scenario: Publishing a revision retires the previous version in the same action
  Given "SOP-CAL-045" is published at version 1.0
  When Nadia drafts a minor revision, uploading the amended file with the change
       summary "Added weekly check", and the revision is reviewed and published by Layla
  Then version 1.1 shows state "Published"
  And version 1.0 shows state "Obsolete"
  And exactly one version of the document is published at any moment

@URS-027 @TC-DOC-UAT-004
Scenario: Only one revision may be in progress at a time
  Given "SOP-CAL-045" is published at version 1.0 and a version 1.1 draft is already open
  When Nadia attempts to start a second revision
  Then the system refuses and explains that a version is already in progress and must be
       published or rejected first

@URS-025 @TC-DOC-UAT-005
Scenario: A reviewer sends a revision back with a reason
  Given version 1.1 of "SOP-CAL-045" is under review
  When Omar rejects it with the reason "Section 4 contradicts the equipment manual"
  Then version 1.1 returns to state "Draft"
  And the rejection reason is visible beside the version's change summary
  And Nadia can upload a corrected file and resubmit the same version for review

@URS-028 @TC-DOC-UAT-006
Scenario: Read-and-understand is captured per person and per version
  Given "SOP-CAL-045" is published at version 1.0
  And Nadia has not yet acknowledged it
  When Nadia opens the document and confirms she has read and understood it
  Then her acknowledgement is shown with the version 1.0 label and a timestamp
  And Layla, viewing the coverage table, sees Nadia listed against version 1.0
  When version 1.1 is later published
  Then Nadia is prompted to acknowledge again, and her version 1.0 receipt remains on
       record but no longer counts as coverage of the current version

@URS-029 @TC-DOC-UAT-007
Scenario: The printed-copy register tracks a numbered copy from issue to destruction
  Given "SOP-CAL-045" is published at version 1.0
  When Layla issues a controlled copy to the holder "Balance Room — Bench 3"
  Then the register shows copy number 1, pinned to version 1.0, with status "Issued"
  When Layla later records that copy as "Destroyed"
  Then the register shows copy 1 as "Destroyed" with the closing date
  And no further return or destruction can be recorded against that copy

@URS-029 @TC-DOC-UAT-008
Scenario: A controlled copy cannot be issued for an unpublished document
  Given a document "SOP-QC-101" exists in status "Draft" with no published version
  When Layla attempts to issue a controlled copy of it
  Then the system refuses and states that only a published document can have a
       controlled copy issued

@ID @TC-DOC-UAT-009
Scenario: A document whose review falls due raises a Quality Manager task
  Given "SOP-CAL-045" was published with a 12-month review cycle
  And the next review date has passed
  When the scheduled sweep runs
  Then a pending task "Periodic review due: SOP-CAL-045 — Balance calibration" is
       assigned to the Quality Manager role with a due date 30 days ahead
  And running the sweep again does not create a second task
  # Implementation-derived: no URS covers periodic review (GAP-DOC-001), and the
  # DOC_REVIEW_DUE notification rule is not seeded (GAP-DOC-010), so the task is the
  # only observable outcome.

@ID @TC-DOC-UAT-010
Scenario: Confirming a review re-arms the cycle
  Given "SOP-CAL-045" is published with a 12-month cycle and its review is flagged due
  When Layla records that the periodic review has been completed
  Then the next review date moves 12 months beyond today
  And the document is no longer flagged as review-due
  And the confirmation appears in the document's audit trail
```

---

## 7. Exploratory charters

Time-boxed, session-based. Each names its target, its resources and the specific information it should
produce. Findings feed the Gap Register, not the case files.

| ID | Charter | Time-box | Resources | Oracles / what "interesting" looks like |
|---|---|---|---|---|
| `TC-DOC-EXPL-001` | **Explore the retire-then-publish resurrection path** with the aggregate's state pair (`Status`, in-flight version) **to discover** every sequence that returns an obsolete document to `Published`, and whether the resurrected document's `NextReviewDue`, acknowledgement coverage and controlled-copy register are coherent afterwards. | 90 min | `ControlledDocument.cs:151-173,229-245`; API `:5080`; `psql` on `controlled_document`/`document_version` | Any path where `status` goes `Obsolete → Published`; any acknowledgement receipt or `Issued` copy that survives the round trip pointing at a version that is now obsolete. Anchored on `GAP-DOC-004`. |
| `TC-DOC-EXPL-002` | **Explore the ungated document endpoints** (`POST /api/documents`, `/{id}/submit`, `/{id}/versions`, `/{id}/acknowledge`, and the four ungated GETs) **with a tenant user holding zero `documents.*` privileges** **to discover** how much of the controlled-document lifecycle an unprivileged account can drive and read. | 90 min | `DocumentsController.cs:24-132`; a custom tenant role with no document keys; `curl.exe` | Any successful create/submit/revision, or any obsolete-version file download, by an account the privilege screen shows as having no document access. Anchored on `GAP-DOC-013`, `GAP-DOC-014`. |
| `TC-DOC-EXPL-003` | **Explore concurrent controlled-copy issuance** (two simultaneous `POST /{id}/controlled-copies`) **to discover** the failure mode of the unlocked `last + 1` numbering and whether the client ever sees a domain code rather than a 500. | 60 min | `ControlledCopySlice.cs:32-40`; `ux_doc_copy_tenant_document_number`; two parallel curl sessions | A `23505` surfacing as an unhandled 500, a skipped copy number, or two copies sharing a number in different tenants' views. Anchored on `GAP-DOC-018`. |
| `TC-DOC-EXPL-004` | **Explore the review-cycle arithmetic** across `review_cycle_months` values `0`, `1`, `120`, `121`, `-1` and month-end publish dates (31 Jan, 29 Feb of a leap year) **to discover** silent coercion, immediately-due documents, and `AddMonths` day-clamping surprises in `NextReviewDue`. | 90 min | `ControlledDocument.cs:104,170,202`; the two live rows with `review_cycle_months = 0`; sweep on a 1-hour interval | Any document that publishes already review-due; any coerced value the user is never told about; any next-due date that drifts backwards across successive confirmations. Anchored on `GAP-DOC-008`, `GAP-DOC-015`. |
| `TC-DOC-EXPL-005` | **Explore the file-download surface** (`GET /api/files/{id}`) **with fileIds harvested from obsolete versions, rejected drafts and other modules' evidence attachments** **to discover** whether any document-state, permission or watermark control exists on the byte path, and whether `Content-Disposition`/`Content-Type` can be steered by the uploader. | 90 min | `FilesController.cs:59-72`; `FileContentPolicy.cs:42-75`; `file_reference` rows | Any obsolete or never-published draft's bytes served identically to the current controlled version; any stored `content_type` that echoes a client claim. Anchored on `GAP-DOC-011`, `GAP-DOC-014`. |
| `TC-DOC-EXPL-006` | **Explore the document audit trail** after a full lifecycle (create → submit → reject → resubmit → recommend → publish → acknowledge → issue copy → close copy → retire) **to discover** which acts leave a hash-chained ledger entry, which leave only a field-change row, and which leave nothing a QA reviewer would recognise. | 120 min | `OutboxInterceptor.cs:44-82`; `ComplianceLedgerServices.cs:29-66`; `FieldChangeInterceptor.cs:27-33`; `GET /api/compliance/chain-verification`; `psql` with `set_config('app.bypass_rls','on',false)` | Acts with no ledger row at all (`DraftNewVersion`, copy **issue**); PIN/password values reaching `field_change`; any chain-verification failure across the sequence. Anchored on `GAP-DOC-006`. |

---

## 8. Gap Register (this module)

Severity scale: **Critical** (regulatory finding / data-integrity defect) · **Major** (defect or
requirement void that blocks a compliant claim) · **Moderate** (correctness or traceability defect with a
workaround) · **Minor** (hygiene).

---

### GAP-DOC-001 — No URS requirement covers the periodic-review cycle

| Field | Content |
|---|---|
| **Source reference** | `docs/validation/01-User-Requirements-Specification.md:63-67` (section D ends at `URS-029`); feature at `src/NT.QAMS.Domain/DocumentControl/ControlledDocument.cs:68-75,179-205`; sweep at `src/NT.QAMS.Infrastructure/Jobs/ScheduledSweepService.cs:135-141`; policy at `src/NT.QAMS.Application/DocumentControl/DocumentReviewDuePolicy.cs:27-58` |
| **Description** | A complete periodic-review capability is implemented — `ReviewCycleMonths`, `NextReviewDue`, `ReviewDueRaised`, the hourly sweep, the `DocumentReviewDue` event, the `DOC_REVIEW_DUE` dispatch attempt and a 30-day Quality-Manager `WorkTask` — but **no user requirement authorises it**. URS section D covers lifecycle (025), e-signature (026), version history (027), acknowledgements (028) and the copy register (029) only. The delta document (`06-Revalidation-Delta-v1.38-v1.50.md`, `URS-056…107`) adds nothing for document review. |
| **Impact** | Every review-cycle test is `[ID]` implementation-derived and cannot be traced in the RTM. A regulator asking "which requirement drives the 24-month default?" has no answer. Conversely, an ISO 17025 §8.3 review obligation the laboratory believes is covered is not formally specified, so a change to the cadence logic could pass validation unnoticed. |
| **Testing limitation** | Cases can assert the observed behaviour but cannot claim requirement coverage; no acceptance criteria exist against which to judge the default cadence, the 30-day task window or the assignee role. |
| **Recommended clarification** | Product owner + QA manager to add `URS-0nn` "The system shall enforce a configurable periodic-review cadence for published controlled documents, flag reviews as due exactly once per cycle, and record review completion", specifying the default cadence, the permitted range, the assignee and the task window. |
| **Suggested acceptance criteria** | (a) A published document carries a next-review date equal to the publish date plus its configured cadence. (b) The cadence is configurable per document within a documented range with a documented default. (c) When the next-review date passes, exactly one review-due signal is produced per cycle. (d) Recording review completion moves the next-review date one cadence beyond the completion date and clears the flag. (e) Both the due signal and the completion appear in the document's audit trail. |
| **Severity** | **Major** |
| **Responsible role** | Product Owner (requirement); QA Manager (validation traceability) |

---

### GAP-DOC-002 — `VersionState.Rejected` is declared but unreachable

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/DocumentControl/ControlledDocument.cs:8` (enum) vs `:146` (`RejectVersion` sets `VersionState.Draft`); DB `ck_document_version_state_domain` admits `'Rejected'` (live `pg_constraint`; created `20260731191212_Hardening3_CheckDomains.cs:77`) |
| **Description** | The enum and the CHECK constraint both admit `Rejected`, but no code path ever assigns it. A rejected version returns to `Draft` and retains its `RejectionReason`. |
| **Impact** | The API contract (`DocumentVersionDto.State`) advertises a state that can never be returned; consumers and UI code may branch on it forever. A reader of the schema reasonably concludes rejections are terminal, which is the opposite of the behaviour. |
| **Testing limitation** | No positive test can be written for the value; a negative test ("`Rejected` never appears") is only as strong as the sample of paths exercised. |
| **Recommended clarification** | Architect to decide: either remove `Rejected` from the enum and the CHECK constraint, or make `RejectVersion` a terminal transition and add an explicit resubmission command. Whichever is chosen, `RejectionReason` handling must be specified for the next cycle. |
| **Suggested acceptance criteria** | Either (a) `VersionState` contains no value that no code path can produce, and the CHECK constraint matches the enum exactly; or (b) rejection sets `Rejected`, and a distinct, audited command creates the corrected draft. |
| **Severity** | Minor |
| **Responsible role** | Architect / Backend Lead |

---

### GAP-DOC-003 — Recommender and approver may be the same person

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/DocumentControl/ControlledDocument.cs:117-129` (`SOD-DOC-001`: author ≠ recommender) and `:151-173` (`SOD-DOC-002`: author ≠ approver); `DocumentVersion.RecommendedBy` at `:33` is never compared with the publishing actor |
| **Description** | Segregation of duties covers only the author. A single non-author holding both `documents.approve` and `documents.sign` — which the seeded **Quality Manager** and **Tenant Administrator** roles both do (`SystemRoleCatalog.cs:97-118`) — can recommend and then publish the same version alone. |
| **Impact** | The two-person control implied by a "recommend then approve" workflow reduces to one person for the majority of privileged users. ISO 17025 §8.3 / 21 CFR Part 11 reviewers reading the workflow diagram would expect two distinct approvers. |
| **Testing limitation** | Whether this is a defect or accepted design cannot be decided from the code; a test asserting either behaviour would be asserting an assumption. Cases must record the observed behaviour and reference this gap. |
| **Recommended clarification** | Quality Manager to confirm whether the document workflow requires two distinct non-author participants. If yes, add a `SOD-DOC-003` guard comparing the publishing actor with `RecommendedBy`. If no, record it as an accepted deviation with rationale. |
| **Suggested acceptance criteria** | If required: publishing a version whose `RecommendedBy` equals the publishing actor is refused with a distinct domain code and HTTP 422, and no electronic signature is minted; a version recommended by A and published by B (A ≠ B ≠ author) succeeds. |
| **Severity** | **Major** |
| **Responsible role** | Quality Manager (policy); Backend Lead (implementation) |

---

### GAP-DOC-004 — A retired document can be resurrected by publishing its in-flight version

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/DocumentControl/ControlledDocument.cs:229-245` (`Retire` touches only `PublishedVersion`) and `:166-172` (`Publish` sets `Status = DocumentStatus.Published` unconditionally); the only obsolete-state guard is `DOC-015` on `DraftNewVersion` (`:209-212`) |
| **Description** | `Retire` obsoletes the published version and sets `Status = Obsolete` but leaves any in-flight `Draft`/`UnderReview`/`Approved` version untouched. `SubmitForReview`, `Recommend`, `RejectVersion` and `Publish` have **no** document-status guard, so the in-flight version can be advanced and published on a retired document, returning `Status` to `Published`, re-arming `NextReviewDue` and re-opening acknowledgements. |
| **Impact** | A withdrawn SOP can silently return to controlled status through the normal workflow, with no distinct event, no e-signature meaning that says "reinstated", and no reviewer visibility. Anyone auditing "which documents were retired" from the current status column gets a false answer. |
| **Testing limitation** | The intended behaviour is unknown: is reinstatement a legitimate operation that simply lacks its own command, or is it a defect? Cases must be `[GD]` for the "should be refused" expectation and `[ID]` for the observed behaviour. |
| **Recommended clarification** | Architect + Quality Manager to decide whether retirement is terminal. If terminal, `Retire` must also cancel the in-flight version, and every lifecycle transition must guard on `Status != Obsolete`. If reinstatement is legitimate, it needs its own signed, audited command. |
| **Suggested acceptance criteria** | If terminal: after retirement, `SubmitForReview`, `Recommend`, `RejectVersion` and `Publish` are each refused with a stated code and HTTP 409; the previously in-flight version is left in a terminal state; and `controlled_document.status` can never transition `Obsolete → Published`. |
| **Severity** | **Critical** |
| **Responsible role** | Architect / Backend Lead |

---

### GAP-DOC-005 — The copy register never marks a copy of a superseded version as obsolete

| Field | Content |
|---|---|
| **Source reference** | Doc-comment claim at `src/NT.QAMS.Domain/DocumentControl/DocumentControlledCopy.cs:12-13` ("Pinned to the version issued, so a copy of a superseded version is visibly obsolete in the register"); actual DTO `src/NT.QAMS.Contracts/DocumentControl/DocumentContracts.cs:44-46`; query `src/NT.QAMS.Application/DocumentControl/ControlledCopySlice.cs:71-78`; UI `frontend/src/app/features/documents/document-detail.component.ts:175-189` |
| **Description** | Nothing computes, stores or displays whether an `Issued` copy's `VersionLabel` still matches the document's current published label. The DTO carries `VersionLabel` and `Status` only; the register table renders both without comparison. Publishing a revision leaves every outstanding copy showing status `Issued` with no visual or data-level indication that it is now uncontrolled paper. |
| **Impact** | Directly weakens the ISO 17025 §8.3.2 / ISO 9001 §7.5.3 control the register exists to provide: a quality manager cannot see, from the register, which physical copies must be recalled after a revision. |
| **Testing limitation** | The expectation stated in the code comment cannot be tested against the code, and the requirement (`URS-029`) does not mention obsolescence marking either — so there is no oracle for "visibly obsolete". |
| **Recommended clarification** | Quality Manager to specify the required behaviour: a derived `Superseded` indicator on the register, an automatic status transition when a revision publishes, or an explicit recall workflow. |
| **Suggested acceptance criteria** | After a revision publishes, every copy still open against the previous version is distinguishable in the register (by a derived flag or a status value) without the reader having to compare labels manually, and the count of outstanding superseded copies is available to the quality manager. |
| **Severity** | **Major** |
| **Responsible role** | Quality Manager (requirement); Backend + Frontend Lead |

---

### GAP-DOC-006 — Issuing a controlled copy produces no domain event or audit-ledger entry

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/DocumentControl/DocumentControlledCopy.cs:40-65` (`Issue` raises nothing) versus `:84` (`Close` raises `ControlledCopyClosed`); ledger path `src/NT.QAMS.Infrastructure/Persistence/Interceptors/OutboxInterceptor.cs:44-82` → `Compliance/ComplianceLedgerServices.cs:29-66`. Same pattern for `ControlledDocument.DraftNewVersion` (`ControlledDocument.cs:207-227`). |
| **Description** | Only aggregates that `Raise` a domain event reach the tamper-evident, hash-chained audit ledger. Issuing a numbered controlled copy — the act that puts a physical document into circulation — raises nothing, so it appears only as a `FieldChangeRecord` "Created" row. Drafting a new version is likewise event-less. |
| **Impact** | Asymmetric evidence: the closure of a copy is hash-chained, its issuance is not. A reviewer reconstructing the distribution history from the audit ledger sees returns and destructions with no matching issues. The field-change ledger is a weaker artefact (not hash-chained per tenant). |
| **Testing limitation** | Audit-completeness cases for the copy register can only assert what exists; they cannot assert a ledger entry for issuance without failing by design. |
| **Recommended clarification** | Architect to confirm whether the field-change row is accepted as sufficient Part 11 evidence for issuance and for new-version drafting, or whether `ControlledCopyIssued` and `DocumentVersionDrafted` events should be raised. |
| **Suggested acceptance criteria** | Either (a) issuing a controlled copy and drafting a new version each produce a hash-chained audit-ledger entry naming the actor, the document, the version and (for copies) the copy number and holder; or (b) a documented, reviewed rationale records why the field-change row alone satisfies the requirement. |
| **Severity** | Moderate |
| **Responsible role** | Architect / Compliance Lead |

---

### GAP-DOC-007 — `documents.create` and `documents.export` are granted but enforced nowhere

| Field | Content |
|---|---|
| **Source reference** | Keys defined by the `SignedRecordLifecycle` bundle at `src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:116-121,145`; granted by every seeded role at `src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs:97-100,102-118,131,161,179-193`; repo-wide search shows no `[RequirePermission(PermissionCatalog.Documents, PermissionAction.Create)]` or `…Export` call site (`grep PermissionCatalog.Documents` returns only View/Edit/Approve/Void/Sign) and no `perms.can('documents.create'|'documents.export')` in the SPA |
| **Description** | Two of the seven `documents.*` keys are configurable in the privilege matrix and grantable to roles, but no endpoint, command or UI element consults them. `POST /api/documents` carries no permission attribute at all, and the module exposes no export route. |
| **Impact** | The privilege screen misrepresents the system: an administrator who revokes `documents.create` from a role changes nothing, and the role can still create controlled documents. This is a false control — worse than an absent one — in an access-review context. |
| **Testing limitation** | A role-matrix case for `documents.create` cannot produce a meaningful negative result; asserting "revoking it denies creation" would fail by design. |
| **Recommended clarification** | Backend Lead to either gate `POST /api/documents` on `documents.create` and add the module's export route (or remove `Export` from the module's action set), or document why these keys exist unenforced. |
| **Suggested acceptance criteria** | Every key advertised in the privilege matrix for the `documents` module is enforced by at least one code path, and revoking any one of them demonstrably denies the corresponding action with 403 `AUTHZ-403`. |
| **Severity** | **Major** |
| **Responsible role** | Backend Lead / Security Lead |

---

### GAP-DOC-008 — `review_cycle_months` defaults to 0 in the database and was never backfilled

| Field | Content |
|---|---|
| **Source reference** | Migration `src/NT.QAMS.Infrastructure/Persistence/Migrations/20260725054703_DocumentReviewCycles.cs:21-27` (`defaultValue: 0`, no backfill) versus the domain default 24 at `src/NT.QAMS.Domain/DocumentControl/ControlledDocument.cs:69,104`. **Measured live in `ntqams`:** `SELECT review_cycle_months, count(*) FROM qams.controlled_document GROUP BY 1` → `0 → 2 rows`, `12 → 2`, `24 → 2`. |
| **Description** | Documents created before the review-cycle migration carry `review_cycle_months = 0`. Publishing such a document computes `NextReviewDue = publishDate.AddMonths(0)` — i.e. the publish date — so the document is review-due the moment it is published and the sweep flags it on its next hourly pass. The aggregate's coercion (`> 0 and <= 120`) protects only *new* values passed through `Create`; it never repairs a loaded row. |
| **Impact** | A false "review overdue" state on legacy documents, spurious Quality-Manager tasks (`DOCREV:{code}`), and an overdue-highlighted date in the SPA (`document-detail.component.ts:113,278-280`). It also means a value the domain considers invalid is present and persistable. |
| **Testing limitation** | Environment-dependent: on a freshly seeded database all rows are 24, and the defect is invisible. Cases must pin the precondition to a row with `review_cycle_months = 0` and state that dependency. |
| **Recommended clarification** | Backend Lead to add a data-fix migration setting `review_cycle_months = 24 WHERE review_cycle_months = 0`, change the column default to 24, and add a CHECK constraint mirroring the domain range. |
| **Suggested acceptance criteria** | (a) No row in `qams.controlled_document` has `review_cycle_months` outside `(0, 120]`. (b) A database CHECK constraint enforces the range. (c) The column default matches the domain default. (d) No published document has a next-review date on or before its publish date. |
| **Severity** | Moderate |
| **Responsible role** | Backend Lead / DBA |

---

### GAP-DOC-009 — No database-level immutability on published, e-signed document versions

| Field | Content |
|---|---|
| **Source reference** | Conventions §2 "Signed-record immutability": `qams.reject_frozen_mutation()` covers the 12 analytical study roots plus `uncertainty_budget`. **Measured live:** `SELECT tgname FROM pg_trigger t JOIN pg_class c … WHERE NOT tgisinternal AND relname IN ('controlled_document','document_version','document_acknowledgement','document_controlled_copy','file_reference')` → **0 rows**. Application-layer protection is limited to `DocumentVersion.State`'s `internal set` (`ControlledDocument.cs:31`). |
| **Description** | A `document_version` row in state `Published` — the exact bytes an approver electronically signed, linked by SHA-256 content hash (`Commands/DocumentCommands.cs:149-157`) — carries no BEFORE UPDATE/DELETE trigger. The same is true of the acknowledgement receipts (described in code as "append-only", `DocumentAcknowledgement.cs:10`) and the closed controlled-copy register entries. Nothing at the SQL layer prevents `UPDATE qams.document_version SET file_id = … WHERE state = 'Published'`. |
| **Impact** | The Part 11 §11.10(c)/(e) claim for controlled documents rests on application discipline alone. Any path that reaches the database with the app role — a future migration, an admin script, a bug in another slice — can alter a signed record while its signature and content hash continue to assert integrity. Acknowledgement receipts described as append-only are equally mutable. |
| **Testing limitation** | A negative DB-level immutability case (`OQ-DB`-style, as written for the analytical roots) cannot be authored for this module: it would fail by design. Cases must be `[GD]` against this gap. |
| **Recommended clarification** | Compliance Lead + DBA to decide whether `document_version` (state `Published`/`Obsolete`), `document_acknowledgement` (all rows) and `document_controlled_copy` (closed rows) should be added to the freeze-trigger set, matching the analytical study roots. |
| **Suggested acceptance criteria** | (a) A direct `UPDATE` or `DELETE` against a published or obsolete `document_version` row is rejected by the database with the frozen-mutation error. (b) The transition **into** `Published` still succeeds. (c) `document_acknowledgement` rejects `UPDATE`/`DELETE` outright. (d) A closed `document_controlled_copy` row rejects further mutation. |
| **Severity** | **Critical** |
| **Responsible role** | Compliance Lead / DBA |

---

### GAP-DOC-010 — The `DOC_REVIEW_DUE` notification rule is never seeded, so no notification is produced

| Field | Content |
|---|---|
| **Source reference** | Key declared at `src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:43`; dispatch attempted at `src/NT.QAMS.Application/DocumentControl/DocumentReviewDuePolicy.cs:34-38`; the default-rule seed list at `NotificationPolicies.cs:138-156` contains eight keys and **not** `DOC_REVIEW_DUE`; early return on no matching rule at `src/NT.QAMS.Application/Notifications/NotificationDispatcher.cs:44-47`. **Measured live:** `SELECT DISTINCT event_key FROM qams.notification_rule` → `COMP_EXPIRED, DOC_PUBLISHED, EQUIP_CALIB_DUE, EQUIP_LOCKED_OUT, NC_RAISED, RISK_HIGH_RESIDUAL, SLA_ESCALATED, SUP_SUSPENDED` (8 keys; `DOC_REVIEW_DUE` absent, as are `STD_EXPIRED` and `COI_HIGH`). |
| **Description** | When a review falls due, `DocumentReviewDuePolicy` calls the dispatcher with `DOC_REVIEW_DUE`; the dispatcher finds no active rule and returns before writing any dispatch row or sending any email. The Quality-Manager `WorkTask` is still created, so the failure is silent — the feature appears to work. |
| **Impact** | No in-app feed entry and no email for an overdue controlled-document review. A laboratory relying on notification for ISO 17025 §8.3 review timeliness receives nothing; the only signal is a task the user must go looking for. |
| **Testing limitation** | A notification case for review-due fails by design on a stock tenant. Cases must either be `[GD]` against this gap or explicitly precondition on a manually created rule and say so. |
| **Recommended clarification** | Backend Lead to add `DOC_REVIEW_DUE` (and confirm the intent for `STD_EXPIRED` and `COI_HIGH`) to `SeedDefaultNotificationRulesPolicy`, with subject/body templates using the `{ref}`, `{title}`, `{due}` placeholders the policy already supplies. |
| **Suggested acceptance criteria** | (a) A newly provisioned tenant has an active notification rule for every event key the application dispatches. (b) When a document's review falls due, at least one notification dispatch row is written to each recipient in the configured roles. (c) An architecture or integration test fails when a dispatched key has no seeded rule. |
| **Severity** | **Major** |
| **Responsible role** | Backend Lead |

---

### GAP-DOC-011 — No "OBSOLETE - UNCONTROLLED" watermark exists

| Field | Content |
|---|---|
| **Source reference** | Exhaustive repository search for `OBSOLETE - UNCONTROLLED`, `OBSOLETE-UNCONTROLLED`, `watermark`, `uncontrolled` across `src/`, `frontend/src/` and the tree: two hits only — the doc-comment at `src/NT.QAMS.Domain/DocumentControl/DocumentControlledCopy.cs:14` and the existing note at `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md:118`. Download path `src/NT.QAMS.WebApi/Controllers/FilesController.cs:66-71` streams the stored bytes unmodified; the SPA version table links straight to that URL (`frontend/src/app/features/documents/document-detail.component.ts:55`). |
| **Description** | The commissioning brief's watermark control — stamping "OBSOLETE - UNCONTROLLED" on superseded or printed copies — is not implemented in any form: no PDF post-processing (QuestPDF is present but not on this path), no server-side overlay, no CSS print rule, no UI badge on the download link. This gap restates and confirms conventions §2 line 118 with the module-level evidence. |
| **Impact** | Printed or downloaded superseded versions are indistinguishable from the current controlled version. This is the classic ISO 17025 §8.3.2 / ISO 9001 §7.5.3 finding: uncontrolled documents in circulation with no marking. |
| **Testing limitation** | The watermark cannot be tested at all. Every case referencing it must be `[GD]` against this gap and must **not** be written as an executable expectation (conventions §1 authoring rule). |
| **Recommended clarification** | Quality Manager + Product Owner to specify the control: which artefacts are marked (obsolete-version downloads, all downloads, printed copies), what the mark reads, and whether it is applied server-side to the PDF or presentation-side. |
| **Suggested acceptance criteria** | Downloading a version whose state is `Obsolete` yields an artefact visibly marked as obsolete and uncontrolled, the mark cannot be removed by the requesting client, and downloading the current published version yields an unmarked artefact. |
| **Severity** | **Major** |
| **Responsible role** | Quality Manager (requirement); Backend Lead (implementation) |

---

### GAP-DOC-012 — The "4-digit PIN" is a client-side rule only

| Field | Content |
|---|---|
| **Source reference** | Backend: `src/NT.QAMS.Infrastructure/Compliance/ComplianceLedgerServices.cs:111-115` verifies `pin` against `signer.PinHash` with no length or character rule. Frontend: `frontend/src/app/features/documents/document-detail.component.ts:270` (`Validators.pattern(/^\d{4}$/)`) and `:85` (`maxlength="4"`). Contract comment repeating the false claim: `src/NT.QAMS.Contracts/DocumentControl/DocumentContracts.cs:11`. i18n hint "4 digits": `frontend/src/app/core/i18n.service.ts:1198`. |
| **Description** | The publish ceremony's PIN component is constrained to exactly four digits only by the Angular reactive form. A direct API call to `POST /api/documents/{id}/publish` may carry a PIN of any length or composition, and it succeeds if it matches the stored hash. The contract's XML documentation asserts a 4-digit rule that does not exist. This is the module-level instance of the general finding in conventions §2 ("No digit-length constraint on the PIN was found in the domain"). |
| **Impact** | The e-signature's second identification component has no enforced entropy floor at the boundary that matters. If PIN enrolment likewise enforces nothing (not verified in this pass — `[RNV]`), a one-character PIN is possible. Any BVA case on PIN length is untestable against the server. |
| **Testing limitation** | PIN boundary cases (3 / 4 / 5 digits, non-numeric) can be authored against the SPA form but not against the API; the API cases must be `[GD]`. The enrolment-side rule must be read before any claim is made about it. |
| **Recommended clarification** | Security Lead to specify the PIN policy (length, character set, reuse, rotation) and enforce it at enrolment **and** at verification, then correct the contract comment and the i18n hint. |
| **Suggested acceptance criteria** | (a) A documented PIN policy exists. (b) Enrolment refuses a PIN violating it with a stated code. (c) `POST /api/documents/{id}/publish` with a policy-violating PIN is refused server-side regardless of client. (d) The contract documentation matches the enforced rule. |
| **Severity** | **Major** |
| **Responsible role** | Security Lead |

---

### GAP-DOC-013 — Six document routes carry no privilege gate

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.WebApi/Controllers/DocumentsController.cs` — ungated write routes: `POST /api/documents` (`:83`), `POST /{id}/submit` (`:91`), `POST /{id}/versions` (`:125`), `POST /{id}/acknowledge` (`:50`); ungated reads: `GET /api/documents` (`:24`), `GET /{id}` (`:31`), `GET /{id}/signatures` (`:45`), `GET /{id}/my-acknowledgement` (`:55`), `GET /{id}/controlled-copies` (`:66`). Only `[Authorize]` at class level (`:21`) and `[RequireInternalActor]` on the commands applies. Queries bypass the command policy entirely (`src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs:44-47`). |
| **Description** | Any authenticated non-auditor tenant user can create a controlled document, submit it for review, draft a new version of any published document, and acknowledge any document — regardless of their `documents.*` grants. Any authenticated tenant user, including one with zero document privileges, can list documents, read full version history with file ids, read the signature manifest and read the copy register. |
| **Impact** | The privilege matrix does not govern the document module's entry points. An access review that inspects role grants will conclude a role cannot create or revise controlled documents when in fact it can. `URS-025`'s "role-gated transitions" is only partially met. |
| **Testing limitation** | Role-matrix negative cases for create/submit/draft-version cannot pass; they must be authored as `[GD]` expectations or as `[ID]` records of the permissive behaviour, never both silently. |
| **Recommended clarification** | Security Lead + Backend Lead to assign the correct `[RequirePermission]` to each route (`documents.create` for create, `documents.edit` for submit and draft-version, `documents.view` for the reads) and to decide whether acknowledgement should remain open to every internal actor (arguably correct — everyone must be able to acknowledge). |
| **Suggested acceptance criteria** | (a) Every state-changing document route is gated by a `documents.*` key, or is documented as deliberately open with a rationale. (b) A user without `documents.view` receives 403 `AUTHZ-403` from every document read route. (c) The role-endpoint matrix test covers all 19 document/file routes. |
| **Severity** | **Critical** |
| **Responsible role** | Security Lead / Backend Lead |

---

### GAP-DOC-014 — Obsolete-version bytes are downloadable by any authenticated tenant user

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.WebApi/Controllers/FilesController.cs:59-72` — `[Authorize]` only, no `[RequirePermission]`, no document-state lookup, no watermark; `src/NT.QAMS.Application/DocumentControl/Queries/DocumentQueries.cs:59-68` returns every version with its `FileId`; `src/NT.QAMS.WebApi/Controllers/DocumentsController.cs:31-33` is ungated. See decision table §4.3. |
| **Description** | `GET /api/files/{id}` resolves a `FileReference` by id within the tenant and streams the bytes with `Content-Disposition: attachment`. It never asks which document the file belongs to, what state that document's version is in, or whether the caller holds `documents.view`. Combined with the ungated detail endpoint that hands out every `fileId`, an obsolete SOP is one click away for any signed-in user. |
| **Impact** | Superseded procedures circulate as ordinary, unmarked files. Together with `GAP-DOC-011` (no watermark) this is the substantive ISO 17025 §8.3.2 exposure for this module: the system does not prevent the unintended use of obsolete documents. |
| **Testing limitation** | The "should be denied" expectation has no implementation to test; cases are `[GD]`. The permissive behaviour is testable and should be recorded as `[ID]`. |
| **Recommended clarification** | Security Lead + Quality Manager to decide the download policy: require `documents.view`; optionally restrict obsolete-version downloads to `documents.approve`/`documents.sign` holders or to an explicit "retrieve superseded version" action that is itself audited. |
| **Suggested acceptance criteria** | (a) `GET /api/files/{id}` requires an appropriate privilege for files attached to controlled documents. (b) Retrieving a superseded version is either refused or audited as a distinct, attributable act. (c) Cross-tenant retrieval continues to return 404. |
| **Severity** | **Critical** |
| **Responsible role** | Security Lead / Quality Manager |

---

### GAP-DOC-015 — Out-of-range `ReviewCycleMonths` is silently coerced to 24

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/DocumentControl/ControlledDocument.cs:104` — `ReviewCycleMonths = reviewCycleMonths is > 0 and <= 120 ? reviewCycleMonths : 24`; `CreateDocumentValidator` (`src/NT.QAMS.Application/DocumentControl/Commands/DocumentCommands.cs:15-27`) has **no** rule for the field; the contract default is 24 (`src/NT.QAMS.Contracts/DocumentControl/DocumentContracts.cs:5`). |
| **Description** | A caller submitting `reviewCycleMonths = 0`, `-6`, `121` or `9999` receives `201 Created` and a document silently configured to 24 months. No validation error, no warning, no audit note recording that the submitted value was discarded. |
| **Impact** | Violates the ALCOA+ expectation that recorded data is accurate and attributable to what the user actually entered. A laboratory intending a 6-week cadence who mistypes `0` gets 24 months and is never told. |
| **Testing limitation** | A BVA case at the `120/121` boundary cannot assert a rejection; it can only assert the coercion, which documents a defect as expected behaviour unless the case cites this gap. |
| **Recommended clarification** | Backend Lead to add a FluentValidation rule `InclusiveBetween(1, 120)` on `CreateDocumentCommand.ReviewCycleMonths` so an invalid value returns 400 with a field-level error, keeping the domain coercion as defence in depth. Add a matching database CHECK (see `GAP-DOC-008`). |
| **Suggested acceptance criteria** | (a) `reviewCycleMonths` of 0, negative, or > 120 returns `400` with a field-level validation error naming the permitted range. (b) 1 and 120 are accepted. (c) An accepted value is stored verbatim and is visible on the detail screen. |
| **Severity** | Moderate |
| **Responsible role** | Backend Lead |

---

### GAP-DOC-016 — `DOC-014` maps to two different HTTP statuses

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Application/DocumentControl/Commands/DocumentCommands.cs:130-131` throws `DomainException("DOC-014", "No version is awaiting approval.")` → **422** by `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:75-80`; `src/NT.QAMS.Domain/DocumentControl/ControlledDocument.cs:153` → `:250` throws `InvalidStateTransitionException("DOC-014", "No version available to publish.")` → **409** by `DomainExceptionHandler.cs:45-50`. |
| **Description** | The publish path pre-validates in the handler and again in the aggregate. The handler's "no in-flight version" branch uses `DomainException` while the aggregate's identical condition uses `InvalidStateTransitionException`, so the same domain code reaches the client as 422 from one path and 409 from the other. In practice the handler's check runs first, so callers observe 422 — but the aggregate's 409 is reachable through the domain layer and through any future caller that bypasses the handler. |
| **Impact** | A client that branches on HTTP status (the SPA treats 409 as a concurrency/state conflict and 422 as a business-rule failure) behaves inconsistently for one condition. It also breaks the package's own convention that a domain code implies a status. |
| **Testing limitation** | An API case asserting "`DOC-014` ⇒ 409" or "⇒ 422" is correct only for the path it exercises; the case must state which layer it drove and cite this gap. |
| **Recommended clarification** | Backend Lead to make the handler's no-in-flight branch throw `InvalidStateTransitionException` (409), matching the aggregate, or to assign a distinct code to the handler pre-check. |
| **Suggested acceptance criteria** | Each domain code in the document module maps to exactly one HTTP status across every path that can raise it, and a test asserts the mapping for `DOC-010` … `DOC-020`. |
| **Severity** | Minor |
| **Responsible role** | Backend Lead |

---

### GAP-DOC-017 — Acknowledgements and controlled copies have no foreign key to the document

| Field | Content |
|---|---|
| **Source reference** | Live `pg_constraint`: `qams.document_acknowledgement` has `pk_document_acknowledgement` only; `qams.document_controlled_copy` has its PK and `ck_document_controlled_copy_status_domain` only. No `f`-type constraint on either. Compare `qams.document_version`, which does carry `fk_document_version_controlled_document_tenant_id_document_id FOREIGN KEY (tenant_id, document_id) REFERENCES qams.controlled_document(tenant_id, id) ON DELETE CASCADE`. Creating migrations: `20260726204141_DocumentAcknowledgement.cs:32-35`, `20260726214512_DocumentControlledCopy.cs:37-40`. |
| **Description** | Both child tables denormalise `document_id`, `document_code` and `version_label` with no referential constraint back to `controlled_document`. The repo convention (CLAUDE.md §5, "Cross-aggregate FKs are tenant-composite") is applied to `document_version` but not to these two. They are separate aggregate roots, so the omission may be deliberate DDD practice — but it is undocumented. |
| **Impact** | A receipt or a copy can reference a `document_id` that does not exist in the tenant; the denormalised `document_code` can drift from the document's actual code (the code is immutable today, but nothing enforces that). The structural guarantee that a child under another tenant's parent is impossible — the stated purpose of the tenant-composite FK convention — does not hold for these two tables. |
| **Testing limitation** | A referential-integrity case would fail by design. Data-integrity cases must be authored as detective checks (orphan queries) rather than preventive assertions. |
| **Recommended clarification** | Architect to confirm whether the aggregate boundary deliberately forbids the FK. If so, record it as an accepted deviation alongside B9. If not, add tenant-composite FKs in their own migration. |
| **Suggested acceptance criteria** | Either (a) `document_acknowledgement` and `document_controlled_copy` carry `FOREIGN KEY (tenant_id, document_id) REFERENCES qams.controlled_document (tenant_id, id)`, or (b) an ADR records the aggregate-boundary rationale and a detective query for orphans is added to the data-integrity check set. |
| **Severity** | Moderate |
| **Responsible role** | Architect / DBA |

---

### GAP-DOC-018 — Controlled-copy numbering has an unguarded read-then-write race

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Application/DocumentControl/ControlledCopySlice.cs:32-40` — `lastCopyNumber` is read with `OrderByDescending(...).FirstOrDefaultAsync()` and incremented, with no lock, no advisory lock and no retry; unique index `ux_doc_copy_tenant_document_number (tenant_id, document_id, copy_number)` (live `pg_indexes`). No `23505` / `PostgresException` / unique-violation handler exists in the request pipeline — a repo-wide search finds `PostgresException` handled only at `src/NT.QAMS.WebApi/Startup/StartupSeeding.cs:196` for `42P01`/`3D000`. `DomainExceptionHandler.cs:26-82` handles `DbUpdateConcurrencyException` but not `DbUpdateException` from a constraint violation. |
| **Description** | Two concurrent `POST /api/documents/{id}/controlled-copies` requests read the same `lastCopyNumber` and both attempt `n + 1`. The unique index correctly rejects the second, but the resulting `DbUpdateException`/`PostgresException 23505` falls through every handler and surfaces as an unhandled **500** with no domain code. |
| **Impact** | A quality user issuing copies quickly sees an opaque server error instead of a retryable, coded response. The stack trace path also risks leaking implementation detail depending on the environment's developer-exception settings. |
| **Testing limitation** | Reproducing the race requires two genuinely concurrent requests; a serialised functional test cannot demonstrate it. Cases must be authored as concurrency/load cases and must not claim a specific error body until the handler exists. |
| **Recommended clarification** | Backend Lead to allocate the copy number atomically (a `RefCounter`-style sequence, an `INSERT … SELECT max+1` inside the same statement, or a transaction-scoped advisory lock) and to add a unique-violation branch to `DomainExceptionHandler` returning a stable code and 409. |
| **Suggested acceptance criteria** | (a) N concurrent issue requests against one document yield N copies numbered 1..N with no gaps and no duplicates. (b) No request returns 500. (c) If a collision is still possible, it returns a documented domain code with HTTP 409. |
| **Severity** | Moderate |
| **Responsible role** | Backend Lead |

---

### Gap Register summary

| Gap | Title | Severity |
|---|---|---|
| `GAP-DOC-001` | No URS covers the periodic-review cycle | Major |
| `GAP-DOC-002` | `VersionState.Rejected` unreachable | Minor |
| `GAP-DOC-003` | Recommender and approver may be the same person | Major |
| `GAP-DOC-004` | Retired document resurrectable by publishing the in-flight version | **Critical** |
| `GAP-DOC-005` | Copy register never marks superseded copies | Major |
| `GAP-DOC-006` | Copy issuance (and version drafting) produce no domain event | Moderate |
| `GAP-DOC-007` | `documents.create` / `documents.export` granted but unenforced | Major |
| `GAP-DOC-008` | `review_cycle_months` default 0, never backfilled (2 live rows) | Moderate |
| `GAP-DOC-009` | No DB-level immutability on published/signed document versions | **Critical** |
| `GAP-DOC-010` | `DOC_REVIEW_DUE` notification rule never seeded | Major |
| `GAP-DOC-011` | No "OBSOLETE - UNCONTROLLED" watermark anywhere | Major |
| `GAP-DOC-012` | 4-digit PIN enforced client-side only | Major |
| `GAP-DOC-013` | Six document routes carry no privilege gate | **Critical** |
| `GAP-DOC-014` | Obsolete-version bytes downloadable by any tenant user | **Critical** |
| `GAP-DOC-015` | Out-of-range `ReviewCycleMonths` silently coerced to 24 | Moderate |
| `GAP-DOC-016` | `DOC-014` maps to both 409 and 422 | Minor |
| `GAP-DOC-017` | No FK from acknowledgements / copies to the document | Moderate |
| `GAP-DOC-018` | Controlled-copy numbering race surfaces as 500 | Moderate |

**Totals: 18 gaps — 4 Critical, 7 Major, 5 Moderate, 2 Minor.**

---

## Compliance posture (verdicts per conventions §6 rule 4)

| Control | Basis | Verdict |
|---|---|---|
| Controlled-document lifecycle with gated transitions | `URS-025`; ISO 17025 §8.3; ISO 9001 §7.5.2/§7.5.3 | **Partially conforms** — the state machine and SoD-on-author are implemented and guarded; six routes are ungated (`GAP-DOC-013`) and retirement is not terminal (`GAP-DOC-004`) |
| Electronic signature on publication | `URS-026`; 21 CFR Part 11 §11.50, §11.200(a)(1) | **Partially conforms** — dual-component signing with content-hash binding, pre-validated before minting, throttled and logged; PIN policy unenforced server-side (`GAP-DOC-012`) |
| Only the current published version is effective | `URS-027`; ISO 17025 §8.3.2 | **Does not conform** — obsolete versions are readable and downloadable, unmarked, by any authenticated tenant user (`GAP-DOC-014`, `GAP-DOC-011`) |
| Read-and-understand acknowledgements pinned to the version | `URS-028` | **Conforms** — pinned to the published label, idempotent per (document, version, user) via `ux_doc_ack_tenant_document_version_user`, re-opened by a revision |
| Controlled printed-copy register with one-shot close | `URS-029`; ISO 17025 §8.3.2 | **Partially conforms** — issue/close and the one-shot terminal guard (`CCP-010`) are correct; superseded copies are not marked (`GAP-DOC-005`) and issuance is not event-audited (`GAP-DOC-006`) |
| Immutable stored file snapshots | `URS-046`; Part 11 §11.10(c) | **Partially conforms** — `FileReference` is content-addressed and never updated at the application layer; no database-level freeze on the referencing signed version (`GAP-DOC-009`) |
| Tenant isolation of document data | conventions §2; `URS-008` | **Conforms** — all five tables measured `rls=t force=t policy=tenant_isolation`, tenant-first composite PKs, tenant-composite FK on `document_version` |
| Periodic review of controlled documents | ISO 17025 §8.3 (no URS) | **Cannot be assessed** — no requirement exists to assess against (`GAP-DOC-001`), and the notification half is inert (`GAP-DOC-010`) |
