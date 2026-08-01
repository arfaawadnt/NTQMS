---
name: ntqms-compliance
description: >-
  What a change to NT.QMS *obliges* you to do beyond writing the code — 21 CFR Part 11 and
  GAMP 5 duties. Audit trail and hash chain, electronic signatures, reason-for-change,
  segregation of duties, signed-record immutability, deny-by-default command authorization,
  the API-surface snapshot gate, and which validation documents a new requirement must touch
  (URS/FRA/RTM/delta/OQ/verification log). Invoke when adding or changing a feature, endpoint,
  command, or regulated record, and before claiming any work is finished.
---

# NT.QMS — Compliance Obligations

NT.QMS is a validated system under **21 CFR Part 11** and **GAMP 5**. Code is only half of a
change here: the other half is evidence. A feature that works but is undocumented and untraced is
**not done**, and saying it is done is the one failure this project treats as serious.

Ground truth for test authors: `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md`.
Current posture: `docs/reference/NT_QMS_Compliance_Status_Report_v1.51.2.html`.

## 1. Honesty rules (these outrank convenience)

- Never claim built / tests pass / migrations applied unless you **ran it and watched it finish**.
- Never sign a validation document, or write a name, date or initials into a signature block.
  Execution transcripts ship **unsigned**; a human signs them.
- If you find a defect — including one you caused — record it in the report or commit message.
  Several entries in `SCHEMA-HARDENING-REPORT.md` are the author's own mistakes. Keep it that way.
- Do not inflate a score, a count, or a status. If a control was already at full marks, closing a
  related gap earns **nothing**; say so.
- Report per-suite test numbers, never a bare total. A total nobody can check is how two wrong
  numbers reached the commit record (`verification-log.md`).

## 2. Authorization — deny by default

Two independent tiers. A new feature needs **both**.

**Endpoint** — `[RequirePermission(module, action)]` (144 call sites):
```csharp
[HttpPost]
[RequirePermission(PermissionCatalog.Deviations, PermissionAction.Create)]
public async Task<IActionResult> Create(...)
```

**Command** — a `CommandPolicyAttribute` on every command/query:
```csharp
[RequirePermissionPolicy(PermissionCatalog.Deviations, PermissionAction.Create)]
public sealed record CreateDeviationCommand(...) : ICommand<Guid>;
```
Available: `RequirePermissionPolicy` (**use this for new work**), `RequireInternalActor`,
`RequireAuthenticatedActor`, `AllowUnauthenticated`, and `RequireRole` (legacy — the `UserRole`
enum is now the platform/tenant *structural tier*, not the authorization mechanism; do not reach
for it). The census is 193 `RequireInternalActor` vs 13 `RequirePermissionPolicy` — **the majority
is legacy tier-defence, not the pattern to copy.**

**Exactly one** policy attribute per command:
`CommandPolicyTests.Every_command_declares_exactly_one_authorization_policy` fails on zero *and*
on two. Queries are not gated in the pipeline — read authorization stays at the controller,
because auditors must be able to read. Failure codes: `AUTHZ-000` (no policy declared),
`AUTHZ-001` (unauthenticated), `AUTHZ-002` (not permitted), `AUTHZ-008` (permission key not in the
catalogue — fails loudly on every call rather than denying quietly).

MediatR pipeline order is fixed and deliberate:
`Tracing → Logging → **Authorization** → Idempotency → Validation` — authorization decides before
validation can leak request schema.

Permissions are **resolved per request from the database**, so a revoked privilege takes effect on
the next call, with no token refresh.

Adding a module means one entry in `PermissionCatalog.Modules` (31 today). A module declares the
**subset** of actions that make sense for it — not all 8 — by picking a preset:

| Preset | Actions |
| ------ | ------- |
| `SignedRecordLifecycle` | View, Create, Edit, Approve, Void, Sign, Export |
| `FullRecordLifecycle` | View, Create, Edit, Approve, Void, Export |
| `ConfigurationModule` | View, Manage |
| `ReadOnlyModule` | View, Export |

Pass an explicit array only when none of the four fits. `AllKeys` (170 today) is derived from the
declared actions (`{module}.{action}`, lower-case), **persisted verbatim — a key must never be
renamed.**

Adding a module obliges five more things:
1. i18n label `perm.mod.<x>` in EN/AR/FR (and `perm.group.<g>` for a new group) — the matrix and
   `GET /api/roles/catalog` render from `NameKey`.
2. `SystemRoleCatalog.Definitions()`: `TenantAdministrator` uses `AllKeys` so it picks the module
   up automatically; `QualityManager`/`ExternalAuditor` use predicates whose default arms will
   **silently include** it; `DepartmentHead`/`Analyst` use an explicit grants table and will
   **silently exclude** it. Decide each deliberately.
3. **The upgrade gap:** seeding is additive and idempotent *per role name*, so **existing tenants'
   already-seeded roles do not gain the new keys.** An administrator must grant them, or you write
   a deliberate data migration. Say which in the release note, or the feature looks broken on
   first use.
4. `[RequirePermission]` on the actions and `[RequirePermissionPolicy]` on the commands.
5. Extend `SystemRoleCatalogTests`, `RolePrivilegeFlowTests`, `RoleEndpointMatrixTests`,
   `AuditorDenyMatrixTests`.

Until the module is in the catalogue it is ungoverned: `[RequirePermissionPolicy]` throws
`AUTHZ-008` on every call and `Role.ReplacePermissions` rejects the key with `ROLE-005`.

## 3. What happens automatically vs. what you must do

Interceptors on `SaveChanges`, in this order — the order matters:

| Interceptor | Does |
| ----------- | ---- |
| `TenantConnectionInterceptor` | sets the tenant GUCs **first**, before any query the others trigger |
| `AuditStampInterceptor` | `CreatedAt/By`, `UpdatedAt/By` |
| `TenantStampInterceptor` | fills `TenantId`, including shadow properties on owned children |
| `FieldChangeInterceptor` | before/after field deltas → `audit.field_change` |
| `OutboxInterceptor` | drains domain events → `outbox_event`, in the same transaction |
| `OrgScopeGuardInterceptor` | enforces branch/department scope |

**You must do explicitly:**
- **Reason for change** — a destructive or regulated edit requires `X-Change-Reason`. A DELETE
  without one is refused (`WebApi/Middleware/RequestIdentity.cs`).
- **Electronic signature** — approvals/closures take a signature (password + PIN) recorded with
  its meaning. Follow `DocumentCommands.cs`; never invent a lighter path.
- **Segregation of duties** — enforced **inside the aggregate**, never in a handler or validator.
  Prefer the shared helper on `AggregateRoot` (15 domain files use it):
  ```csharp
  protected void EnsureSignerIsNotPreparer(Guid signerId, string code)
  // e.g. inside the approve/sign method:
  EnsureSignerIsNotPreparer(approverId, "SOD-QP-001");
  ```
  It throws when `CreatedByUserId == signerId`, and is a deliberate no-op when the preparer is
  unknown (legacy or system-created records). Where the rule compares two *specific* parties
  rather than preparer-vs-signer, throw directly with a `SOD-*` code — see
  `ControlledDocument.cs:156` and `Supplier.cs:91`.
- **Immutability** — a signed/approved record is frozen by a database trigger. Model corrections
  as a **new version or an amendment record**. Never plan an UPDATE to a signed row; the trigger
  will reject it and it would be a Part 11 violation if it didn't.

**Domain events are the only route into the audit trail.** If you want a fact in the tamper-evident
ledger, raise it as a domain event on a tracked aggregate — nothing else gets there.

A `sealed record`, past-tense, deriving from `DomainEvent` (which supplies
`EventId = Guid.CreateVersion7()`), JSON-serializable, carrying **refs not object graphs**:
```csharp
public sealed record NcRaised(Guid NcId, string NcRef, string Title, int Severity, int Rpn) : DomainEvent;
```
**Do not put `TenantId` in the payload of a new event** — the outbox attributes tenancy itself (via
`ITenantScoped` / `IOptionallyTenantScoped`), so a payload copy is redundant and can drift. 9 of the
32 existing events still carry it; the v1.51 `Role*` events do not. Follow the newer convention and
leave the legacy ones alone — renaming an event type breaks replay (below).

Three transactional hops: `OutboxInterceptor` drains events into `outbox_event` in the *same*
SaveChanges (so an event without its change is impossible), `OutboxProcessor` claims rows with
`FOR UPDATE SKIP LOCKED` and republishes as `DomainEventNotification<T>`, and `AuditTrailAppender`
chains the hashed entry. Two consequences:
- The processor resolves the type by name, so **renaming or moving an event type breaks replay of
  already-stored rows.** Treat event type names as a persisted contract.
- Delivery is at-least-once, so `INotificationHandler<DomainEventNotification<T>>` **must be
  idempotent**.

## 4. Gates that fail the build

| Gate | Trigger | Correct response |
| ---- | ------- | ---------------- |
| **API-surface snapshot** — `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` (652 lines) | any route, verb, or signature change | Read `ApiSurface.received.txt`, review the diff as a **public-contract change**, copy it over the approved file **in the same commit**. Never regenerate it to make red go green. Note each new route adds **two** lines — `VersionedRouteConvention` dual-exposes every `api/…` template as `api/v{version}/…` |
| `CommandPolicyTests` | a command with no policy attribute | Add the right attribute; do not add `AllowUnauthenticated` to silence it |
| Module-boundary tests | a cross-module reference | Route through the published contract |
| `UserAccountTenantBoundTests` | an unbounded `db.Users` query | Add a tenant predicate or an id bound; the `tenant-unbounded:` comment is a last resort with a written reason |
| `GovernanceTests` | latest migration does not round-trip | Fix `Down()` |

### House shapes a new endpoint must honour

**Error codes decide the HTTP status.** `DomainExceptionHandler` maps by *prefix and suffix*, so
the code you pick is the contract — get it wrong and the endpoint returns the wrong status:

| Code shape | Status |
| ---------- | ------ |
| `AUTH-*` | 401 |
| `AUTHZ-*` | 403 |
| `*-404` (suffix) | 404 |
| `InvalidStateTransitionException` | 409 + its code |
| `DbUpdateConcurrencyException` | 409 `CONCURRENCY-409` |
| FluentValidation failure | 400 with an `errors` map |
| any other `DomainException` | 422 |

`ProblemResponse` is the **only** writer for error bodies (`application/problem+json`, stamped
with `traceId` and `correlationId`). Anonymous-object error shapes are banned.

**Pagination:** a list query declares `int Page = 1, int PageSize = PageRequest.DefaultPageSize`
and returns `IQuery<PagedResponse<TDto>>`; the handler filters → **orders** → `.Select(dto)` →
`.ToPagedAsync(PageRequest.Normalized(q.Page, q.PageSize), ct)`. `Normalized` clamps to
`MaxPageSize = 200` so a hostile `pageSize` can never become an unbounded query. Add the new list
endpoint to `ContractCoverageTests.ListEndpoints`.

**Idempotency** is automatic for commands when an `Idempotency-Key` header is present and the
actor is known — but the behaviour caches the response as JSON, so **the response type must
round-trip losslessly through System.Text.Json** (records, `Guid`, DTOs are fine; interfaces and
polymorphism are not). It protects *sequential* retries only; a new aggregate that can be
double-created still needs its own tenant-scoped unique index.

**Versioning** needs no code — controllers carry no version attributes and are implicitly v1.0.

## 5. Validation documents — what a change obliges

A change that adds or alters **user-visible behaviour** is a requirement change, and requirements
are traced. Touch these, in this order:

1. **`docs/validation/06-Revalidation-Delta-v1.38-v1.50.md`** — the single source of truth for
   everything after the 1.0 baseline. **Part A** the requirement row (next free id — the ceiling
   is `URS-107`, so a new feature starts at `URS-108`; columns `URS | Requirement (delta) | Design
   element(s) | Verification | Status`, Status starts as `Template`). **Part B** an IQ case if the
   change adds an install/config/schema check. **Part C** register the automated suite *and* add a
   witnessed OQ case template. **Part D** a PQ case if there is a load dimension. **Part E** the
   VSR-addendum paragraph. **Part F** the QA execution checklist.
   Do **not** add requirements to doc 01 — `URS-001`…`055` is the frozen baseline.
2. **`docs/validation/02-Functional-Risk-Assessment.md`** — a risk row (S × P × D → priority) for a
   new functional area. Risk class drives IQ/OQ/PQ rigour.
3. **`docs/validation/04-Requirements-Traceability-Matrix.md`** — only when the change **revises an
   existing baseline requirement**; edit that row and bump the header `Version` with a dated note.
   New requirements trace inside the delta doc instead. Either way every requirement must reach a
   design element **and** a named, executable test — an untested requirement is an audit finding.
4. **A new OQ execution record** — `docs/validation/NN-OQ-Execution-Record-<Feature>-v<ver>.md`
   (docs 09–13 are the pattern), with real observed output. Never edit the protocol to record
   execution. Fabricated evidence is the worst outcome available here. Leave the signature block
   **empty**.
5. **`docs/validation/verification-log.md`** — append one row per full-suite run you actually
   watched finish: date, commit, environment, per-suite counts.
6. **`docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md`** — amend the "Requirement IDs" paragraph
   when the URS ceiling moves, or the as-built table if a mechanism changed.

Governing rule is `00-Validation-Master-Plan.md` §8: assess impact and risk → determine
re-validation scope → execute targeted IQ/OQ/PQ → update traceability → obtain QA approval.
**Engineering never self-certifies:** every delta row ships as `Template`/DRAFT; QA owns
execution, review and signature.

Then `CHANGELOG.md` and the version tag. If a change is purely internal with no behavioural
effect, say so explicitly in the commit rather than skipping the question.

## 6. Testing obligations

Baseline: **446 backend** (228 domain / 72 application / 33 architecture / 31 +1 skipped
integration / 82 functional) + **76 frontend unit** + **6 Playwright e2e**.

Match the test to the claim:
- an **invariant** → domain unit test;
- a **handler/policy** → application test (EF InMemory);
- **isolation, triggers, CHECK constraints, migrations** → integration test against **real
  PostgreSQL**;
- a **regulated end-to-end flow** → `RegulatedFlowRealDatabaseTests`.

**InMemory does not enforce RLS, triggers, or CHECK constraints.** This is not academic: SH-D1 —
RLS on `security_event` breaking sign-in with a 500 — passed all 419 tests and was caught by
opening the app in a browser. That is why VER-001 exists. If a change touches isolation, the audit
ledger, or authentication, prove it against a real database **and** exercise it in the running app.

Locally the integration and real-database suites **skip rather than fail** when no server is
reachable, so read the skip count, not only the pass count. **CI does not let you off**: it runs
the whole solution against a real PostgreSQL 17 as a non-superuser (`qams_app`, NOSUPERUSER
NOBYPASSRLS) with `QMS_ITEST_POSTGRES` set, so the RLS suite hard-fails instead of skipping — and
it proves the migrations apply from nothing.

## 7. Known-open items (do not report these as closed)

`SEC-001` independent penetration test · `DOC-001` validation in a qualified environment with
signatures · `OPS-001` staging observability and load testing · unsigned OQ transcripts (docs 12
and 13) awaiting signature. **B9** (no RLS on `user_account`/`outbox_event`) and **B10** are
*accepted deviations*, not open defects — see `SCHEMA-HARDENING-REPORT.md` §8.
