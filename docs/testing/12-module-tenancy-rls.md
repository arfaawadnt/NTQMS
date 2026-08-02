# 12 — Module TENANT: Multi-Tenancy, Row-Level Security, Tenant Isolation, Platform Control Plane

**Module code:** `TENANT`
**System under test:** NT.QMS **v1.51.2**, repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master`. Inspection date 2026-08-01.
**Binding conventions:** `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md` — read it in full before this file; it governs the 28-field case format, the canonical case block (§8), the evidence labels `[IV]`/`[RNV]`/`[ID]`/`[GD]`, the ID convention (§5) and the honesty rules (§6). Entries marked **[corrected 2026-08-01]** there supersede anything older.

**Live-database facts in this file were re-measured on 2026-08-01** against dev `ntqams` (PostgreSQL 17, role `qams_app`) with read-only `psql`. Every catalog figure below is a measurement, not a repetition of a document.

---

## ID reservation table

This file is **front matter only**. It authors no detailed cases. The ranges below are *reserved* for the case files `12-module-tenancy-rls-cases-A.md`, `-B.md`, `-C.md`; a reservation with no matching case file is a coverage hole, not a delivered case (conventions §7).

| Kind | Reserved range | Count | Owning batch | Slice of scope |
|---|---|---|---|---|
| `TC-TENANT-UNIT-nnn` | 001–030 | 30 | **A** | `Tenant` aggregate, `TenantSlug`, `TenantSettings`, `TenantStatus` — pure domain |
| `TC-TENANT-BVA-nnn` | 001–015 | 15 | **A** | Slug length 1/2/50/51, name length 0/1/200/201, settings numeric defaults |
| `TC-TENANT-EP-nnn` | 001–010 | 10 | **A** | Slug character-class partitions, status partitions |
| `TC-TENANT-STATE-nnn` | 001–020 | 20 | **A** | `TenantStatus` transition matrix incl. every illegal edge |
| `TC-TENANT-API-nnn` | 001–020 | 20 | **A** | `POST /api/tenants`, `GET /api/tenants`, `GET /api/auth/workspace/{slug}` |
| `TC-TENANT-DT-nnn` | 001–006 | 6 | **A** | Provisioning decision table; workspace-lookup disclosure table |
| `TC-TENANT-RLS-nnn` | 001–060 | 60 | **B** | Per-table isolation, fail-closed, WITH CHECK, elevation, owned children, audit-schema relaxation, `security_event` **positive** isolation |
| `TC-TENANT-INT-nnn` | 001–030 | 30 | **B** | Real-PostgreSQL integration: composite FK drift, structural RLS-parity sweep, migration round-trip, least-privilege role guard |
| `TC-TENANT-SEC-nnn` | 001–020 | 20 | **B** | Cross-tenant attack cases, GUC forgery, elevation abuse, `BYPASSRLS` refusal |
| `TC-TENANT-DT-nnn` | 007–012 | 6 | **B** | RLS predicate decision table; elevation-authorisation decision table |
| `TC-TENANT-MUT-nnn` | 001–008 | 8 | **B** | Mutation cases: revert each hardening fix and prove a guard fails |
| `TC-TENANT-API-nnn` | 021–040 | 20 | **C** | `GET`/`PUT /api/tenant-settings/mfa-policy`, `/api/v{version}` mirrors, `PlatformControllers.cs` surface |
| `TC-TENANT-SEC-nnn` | 021–030 | 10 | **C** | Tenant-resolution middleware: claim source, spoofed header/query, platform-admin null tenant |
| `TC-TENANT-COMP-nnn` | 001–010 | 10 | **C** | `TenantConnectionInterceptor`, `TenantStampInterceptor`, EF global query filter — component level |
| `TC-TENANT-E2E-nnn` | 001–010 | 10 | **C** | Provision → sign in at `/t/{slug}` → work → isolation observed in the SPA |
| `TC-TENANT-MCDC-nnn` | 001–006 | 6 | **C** | MC/DC over the composed tenant+scope query filter and the RLS `OR` predicate |
| `TC-TENANT-PATH-nnn` | 001–006 | 6 | **C** | `TenantStampInterceptor.StampOwnedChildren` branch paths |
| `TC-TENANT-DF-nnn` | 001–006 | 6 | **C** | Data flow of `tenant_id` from JWT claim → GUC → policy → row |
| `TC-TENANT-OBS-nnn` | 001–006 | 6 | **C** | Tenant attribution on traces, metrics and the canonical request log |
| `TC-TENANT-DR-nnn` | 001–006 | 6 | **C** | Restore-gate RLS-parity assertion (`deploy/BACKUP-RESTORE-DR.md` §5) |
| `TC-TENANT-PERF-nnn` | 001–006 | 6 | **C** | Per-connection `set_config` overhead; RLS predicate cost on the hot tables |
| `TC-TENANT-A11Y-nnn` | 001–004 | 4 | **C** | Platform tenant-list and tenant-entry screens |

**Reserved but not yet allocated to a batch:** `TC-TENANT-RLS-061…090`, `TC-TENANT-INT-031…040` — held for tables added after this inspection date.

**Consumed by *this* file (not reserved — actually authored below):**

| Kind | Range | Count |
|---|---|---|
| `TC-TENANT-UAT-nnn` | 001–008 | 8 (§6 Gherkin scenarios) |
| `TC-TENANT-EXPL-nnn` | 001–005 | 5 (§7 charters — not detailed cases) |
| `GAP-TENANT-nnn` | 001–014 | 14 (§8) |

**Completeness statement.**
*Complete in this file:* the `Tenant` aggregate and every one of its domain error codes read off source; the `TenantSlug` value object and its regex; `TenantSettings`; the four tenancy domain events and their only consumer; all five logical tenancy endpoints plus the `PlatformControllers.cs` surface; both isolation layers (EF global query filter, PostgreSQL FORCE RLS) with the **measured** 90/90 policy inventory and the exact two predicate shapes; the eight `Elevate()` call sites; the two accepted RLS exceptions (B9) verified against `pg_catalog` rather than quoted; the four in-scope migrations; the tenant-resolution and active-session middleware; the schema-hardening artefacts.
*Deliberately not in this file:* detailed test cases (conventions §7 split rule — they belong in `12-module-tenancy-rls-cases-<letter>.md`).
*Deferred / not executable as written, each raised as a Gap in §8 rather than authored as a case:* tenant suspension, reactivation and termination (no command, no endpoint); the `Provisioning` status; `deploy/harden-runtime-role.sql` execution (it references a schema that does not exist); typed HTTP mapping of an RLS write refusal; tenant-status enforcement on live sessions and on the scheduled sweep.
*Nothing in this file was executed. No `Result` other than `Not Run` appears anywhere in this package.*

---

## 0. Correction to ground truth

One factual correction. Everything else in the conventions file that touches this module was re-measured and **confirmed** (see the confirmations list at the end of this section).

### 0.1 The elevation-path enumeration is incomplete — six components, eight call sites, not five

`docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md:72` states:

> "Elevation is explicit: `ICurrentTenant.IsElevated` / `ICurrentTenantSetter.Elevate()`, used by **exactly** the trusted cross-tenant paths (`ProvisionTenant`, `OutboxProcessor`, `ScheduledSweepService`, `KpiSnapshotService`, LOV backfill)."

A repository-wide search for `Elevate()` in `src/` returns **eight** call sites across **six** components. The list omits the **startup role-and-assignment backfill**, which is a distinct component from the LOV backfill and is the single most privileged of them all — it reads `db.Users` across every tenant under an explicit `tenant-unbounded:` exemption comment:

| # | Component | File:line |
|---|---|---|
| 1 | `ProvisionTenantHandler.Handle` | `src/NT.QAMS.Application/Tenancy/Commands/ProvisionTenant.cs:41` |
| 2 | `StartupSeeding.BackfillStarterListOfValuesAsync` (the "LOV backfill" already listed) | `src/NT.QAMS.WebApi/Startup/StartupSeeding.cs:99` |
| 3 | **`StartupSeeding.BackfillRolesAndAssignmentsAsync` — NOT LISTED** | `src/NT.QAMS.WebApi/Startup/StartupSeeding.cs:132` |
| 4 | `KpiSnapshotService.SnapshotAllTenantsAsync` | `src/NT.QAMS.Infrastructure/Jobs/KpiSnapshotService.cs:63` |
| 5 | `ScheduledSweepService.RunSweepAsync` | `src/NT.QAMS.Infrastructure/Jobs/ScheduledSweepService.cs:64` |
| 6 | `OutboxProcessor.ProcessBatchAsync` | `src/NT.QAMS.Infrastructure/Persistence/Outbox/OutboxProcessor.cs:102` |
| 7 | `OutboxProcessor.RefreshQueueStatsAsync` | `src/NT.QAMS.Infrastructure/Persistence/Outbox/OutboxProcessor.cs:225` |
| 8 | `OutboxProcessor.RunRetentionPurgeAsync` | `src/NT.QAMS.Infrastructure/Persistence/Outbox/OutboxProcessor.cs:246` |

Proof that site 3 is a genuine, separate elevation and not a duplicate of site 2: the two are different methods with different bodies — `src/NT.QAMS.WebApi/Startup/StartupSeeding.cs:94` (`BackfillStarterListOfValuesAsync`) and `:127` (`BackfillRolesAndAssignmentsAsync`) — and the second carries its own exemption comment at `:157`: *"tenant-unbounded: the backfill runs under Elevate() across every tenant by design"*.

**Consequence for this package.** The elevation-path table in §4.3 enumerates all eight, and the batch-B cases must cover eight paths, not five. An RLS-bypass path that no test knows about is exactly the class of hole this module exists to close.

### 0.2 Confirmed correct — re-measured, no correction needed

| Ground-truth claim | Line | Measurement |
|---|---|---|
| `TenantConnectionInterceptor` sets `app.current_tenant` and `app.bypass_rls` via `set_config` on every connection open, fail-closed to nil | :69 | Confirmed — `TenantConnectionInterceptor.cs:23,29` override `ConnectionOpened`/`ConnectionOpenedAsync`; `:53-56`; nil fallback `Guid.Empty` at `:21,55` |
| 90/90 FORCE-RLS policies | :70 | Confirmed — `SELECT count(*) … relforcerowsecurity` = **90**; `pg_policies WHERE policyname='tenant_isolation'` = **90** |
| Policy shape `USING/WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant',true),'')::uuid OR current_setting('app.bypass_rls',true)='on')` | :70 | Confirmed for **86** of the 90 |
| `audit.*` policies relaxed on WRITE with `tenant_id IS NULL OR …`; `qams.*` stays strict | :71 | Confirmed — exactly **4** null-tolerant `WITH CHECK`, and they are exactly the four `audit.*` tables |
| Accepted deviation B9: `user_account` and `outbox_event` stay outside RLS | :73 | Confirmed — the catalog query for "carries `tenant_id`, has no `tenant_isolation` policy" returns exactly `qams.outbox_event` and `qams.user_account`, both `tenant_id` **nullable**, both `relrowsecurity=f relforcerowsecurity=f` |
| `audit.security_event` RLS gap **CLOSED** in v1.51.2; author **positive** isolation coverage | :74 | Confirmed — `rls=true force=true policy=tenant_isolation`, null-tolerant write shape, created by `Hardening2_RlsGapClosure.cs:17-28`; the store read is tenant-filtered at `src/NT.QAMS.Infrastructure/Compliance/ComplianceLedgerServices.cs:199-203` |
| 5 FKs `DEFERRABLE INITIALLY DEFERRED` to `saas.tenant` | :75 | Confirmed — `qams.outbox_event`, `qams.ref_counter`, `qams.user_account`, `qams.branch`, `read.kpi_snapshot`; all `condeferrable=t condeferred=t`, `ON DELETE RESTRICT` |
| 97 tables / 5 schemas | :25 | Confirmed — 97 tables (`qams` 89, `audit` 4, `saas` 2, `read` 1, `public` 1) across schemas `audit, public, qams, read, saas` |
| Exactly one `[Authorize(Roles=…)]` remains, guarding the platform surface | :65 | Confirmed — the sole occurrence in `src/` is `TenantsController.cs:12`; `[RequirePermission(...)]` counts **144** across the controllers; exactly one `[RequireRole]` command policy exists, on `ProvisionTenantCommand` |
| New `ITenantScoped` tables need RLS in their own migration | :76 | Confirmed as behaviour, and now *enforced* by `OwnedChildTenancyTests.Every_owned_child_table_carries_tenant_id_and_full_rls` (`tests/NT.QAMS.IntegrationTests/OwnedChildTenancyTests.cs:128-155`) |

---

## 1. Implementation inventory

### 1.1 Aggregates, value objects and marker interfaces

| Type | Kind | File:line | Notes |
|---|---|---|---|
| `Tenant` | `AggregateRoot` — **not** `ITenantScoped` (it *is* the tenant) | `src/NT.QAMS.Domain/Tenancy/Tenant.cs:11` | Control plane, `saas.tenant`, **outside RLS by design** (`Tenant.cs:6-9`) |
| `TenantSlug` | `ValueObject`, `sealed partial` | `src/NT.QAMS.Domain/Tenancy/TenantSlug.cs:10` | `MaxLength = 50` (`:12`); normalised to lower-invariant + trimmed (`:28`) |
| `TenantSettings` | `record`, EF **owned, same row** | `src/NT.QAMS.Domain/Tenancy/TenantSettings.cs:8` | 1:1 with `Tenant`; `TenantSettings.Default` at `:22` |
| `TenantStatus` | `enum` | `src/NT.QAMS.Domain/Tenancy/TenantStatus.cs:7` | `Provisioning=0, Active=1, Suspended=2, Terminated=3` |
| `ITenantScoped` | marker, `Guid TenantId { get; set; }` | `src/NT.QAMS.SharedKernel/MultiTenancy/ITenantScoped.cs:10` | Triple enforcement declared in the doc comment: EF filter + RLS + composite FKs |
| `IOptionallyTenantScoped` | marker, `Guid? TenantId { get; }` | `src/NT.QAMS.SharedKernel/MultiTenancy/IOptionallyTenantScoped.cs:15` | The `user_account` case; added for defect **RP-D1** (`:11-13`) |
| `IAllocatable` | marker, `Guid? BranchId`, `Guid? DepartmentId` | `src/NT.QAMS.SharedKernel/MultiTenancy/IAllocatable.cs:11` | Drives the *composed* tenant+scope query filter |
| `ICurrentTenant` | read side — `TenantId`, `IsResolved`, `IsElevated` | `src/NT.QAMS.Application/Abstractions/ICurrentTenant.cs:8-20` | Doc: tenant comes from the JWT `tenant_id` claim **only**, never headers or query strings (`:5-6`) |
| `ICurrentTenantSetter` | write side — `Set`, `Clear`, `Elevate` | `src/NT.QAMS.Application/Abstractions/ICurrentTenant.cs:23-35` | `Elevate()` doc: *"Must never be called on a request handling end-user input."* (`:32-33`) |
| `CurrentTenant` | scoped DI implementation of both | `src/NT.QAMS.Infrastructure/Services/RequestContext.cs:12` | `Set` `:18`, `Clear` `:20-24`, `Elevate` `:26`. **`Elevate()` is one-way for the lifetime of the scope — there is no `Demote()`** |

**Registration:** all three are one scoped instance — `services.AddScoped<CurrentTenant>()` then both interfaces resolve to it (`src/NT.QAMS.Infrastructure/DependencyInjection.cs:21-23`).

### 1.2 `Tenant` invariants — exhaustive

| # | Invariant | Where enforced | Code | Exception type | HTTP |
|---|---|---|---|---|---|
| T-1 | Slug is required | `TenantSlug.Create` | `TENANT-001` | `DomainException` | 422 |
| T-2 | Slug matches `^[a-z0-9](?:-?[a-z0-9]){1,49}$` **and** `length ≤ 50` | `TenantSlug.Create` | `TENANT-002` | `DomainException` | 422 |
| T-3 | Name is required (non-blank after trim) | `Tenant.Provision` | `TENANT-003` | `DomainException` | 422 |
| T-4 | Name ≤ `MaxNameLength` (200), measured **after trim** | `Tenant.Provision` | `TENANT-004` | `DomainException` | 422 |
| T-5 | Slug is globally unique | `ProvisionTenantHandler` pre-check + unique index | `TENANT-005` | `DomainException` | 422 |
| T-6 | Only an `Active` tenant may be suspended | `Tenant.Suspend` | `TENANT-010` | `InvalidStateTransitionException` | **409** |
| T-7 | A suspension reason is required | `Tenant.Suspend` | `TENANT-011` | `DomainException` | 422 |
| T-8 | Only a `Suspended` tenant may be reactivated | `Tenant.Reactivate` | `TENANT-012` | `InvalidStateTransitionException` | **409** |
| T-9 | `Terminated` is terminal — a second terminate is refused | `Tenant.Terminate` | `TENANT-013` | `InvalidStateTransitionException` | **409** |
| T-10 | `UpdateSettings(null)` throws | `Tenant.UpdateSettings` | *(none — `ArgumentNullException`)* | `ArgumentNullException` | **500** (unmapped) |
| T-11 | A tenant-scoped row cannot be persisted without a resolved tenant | `TenantStampInterceptor.Stamp` | `TENANT-000` | `DomainException` | 422 |
| T-12 | An owned child cannot be persisted without a resolvable owner tenant | `TenantStampInterceptor.StampOwnedChildren` | `TENANT-000` | `DomainException` | 422 |
| T-13 | An MFA-policy read/write requires a resolved tenant | `Get/SetTenantMfaPolicyHandler` | `TENANT-000` | `DomainException` | 422 |
| T-14 | The tenant row referenced by the MFA-policy slice must exist | `Get/SetTenantMfaPolicyHandler` | `TENANT-404` | `DomainException` | **404** |

### 1.3 Domain error codes — EXHAUSTIVE for this module

Every `TENANT-*` string literal in `src/`, with its emitting line. There are **eleven**.

| Code | Message (verbatim, truncated) | File:line | Exception | Mapped HTTP |
|---|---|---|---|---|
| `TENANT-000` | "No tenant in context." | `src/NT.QAMS.Application/Tenancy/Commands/TenantMfaPolicy.cs:19` | `DomainException` | 422 |
| `TENANT-000` | "No tenant in context." | `src/NT.QAMS.Application/Tenancy/Commands/TenantMfaPolicy.cs:31` | `DomainException` | 422 |
| `TENANT-000` | "Cannot persist tenant-scoped '{Type}' without a resolved tenant." | `src/NT.QAMS.Infrastructure/Persistence/Interceptors/TenantStampInterceptor.cs:53` | `DomainException` | 422 |
| `TENANT-000` | "Cannot persist owned '{Type}' without a resolved tenant." | `src/NT.QAMS.Infrastructure/Persistence/Interceptors/TenantStampInterceptor.cs:107` | `DomainException` | 422 |
| `TENANT-001` | "Tenant identifier is required." | `src/NT.QAMS.Domain/Tenancy/TenantSlug.cs:25` | `DomainException` | 422 |
| `TENANT-002` | "Tenant identifier must be 2-50 chars of lowercase letters, digits and single hyphens, starting and ending with a letter or digit." | `src/NT.QAMS.Domain/Tenancy/TenantSlug.cs:32` | `DomainException` | 422 |
| `TENANT-003` | "Tenant name is required." | `src/NT.QAMS.Domain/Tenancy/Tenant.cs:45` | `DomainException` | 422 |
| `TENANT-004` | "Tenant name must not exceed 200 characters." | `src/NT.QAMS.Domain/Tenancy/Tenant.cs:50` | `DomainException` | 422 |
| `TENANT-005` | "Tenant identifier '{slug}' is already in use." | `src/NT.QAMS.Application/Tenancy/Commands/ProvisionTenant.cs:48` | `DomainException` | 422 |
| `TENANT-010` | "Only an Active tenant can be suspended (current: {Status})." | `src/NT.QAMS.Domain/Tenancy/Tenant.cs:63` | `InvalidStateTransitionException` | **409** |
| `TENANT-011` | "A suspension reason is required." | `src/NT.QAMS.Domain/Tenancy/Tenant.cs:68` | `DomainException` | 422 |
| `TENANT-012` | "Only a Suspended tenant can be reactivated (current: {Status})." | `src/NT.QAMS.Domain/Tenancy/Tenant.cs:81` | `InvalidStateTransitionException` | **409** |
| `TENANT-013` | "Tenant is already terminated." | `src/NT.QAMS.Domain/Tenancy/Tenant.cs:93` | `InvalidStateTransitionException` | **409** |
| `TENANT-404` | "Tenant not found." | `src/NT.QAMS.Application/Tenancy/Commands/TenantMfaPolicy.cs:21` | `DomainException` | **404** (`-404` suffix rule) |
| `TENANT-404` | "Tenant not found." | `src/NT.QAMS.Application/Tenancy/Commands/TenantMfaPolicy.cs:33` | `DomainException` | **404** |

**`TENANT-006` … `TENANT-009` do not exist.** Do not write a case against them.

**Status-code mapping is read from** `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:26-82`:
the `switch` opens at `:26`; `DbUpdateConcurrencyException` → 409 `CONCURRENCY-409` (arm at `:28`) · `ValidationException` → 400 (`:34`) · `InvalidStateTransitionException` → **409** carrying its own code (`:45`) · `DomainException` whose code starts `AUTH-` → 401 (`:54`) · starts `AUTHZ-` → 403 (`:63`) · ends `-404` → 404 (`:69`) · any other `DomainException` → 422 (`:75`) · **anything else → `null` → unhandled → HTTP 500** (`:81`).

**Adjacent codes this module's cases will assert (not owned here, cited for accuracy):**

| Code | Meaning in a tenancy context | File:line |
|---|---|---|
| `AUTH-001` | Unknown slug at sign-in — deliberately identical to "invalid credentials" | `src/NT.QAMS.Application/IdentityAccess/Commands/Login.cs:51` |
| `AUTH-002` | "This tenant is not active." — the **only** enforcement of a non-`Active` status on a live path | `src/NT.QAMS.Application/IdentityAccess/Commands/Login.cs:62` |
| `AUTH-006` | Account inactive / missing on re-check | `src/NT.QAMS.WebApi/Middleware/RequestIdentity.cs:100` |
| `AUTH-007` | Token role ≠ DB role | `src/NT.QAMS.WebApi/Middleware/RequestIdentity.cs:107` |
| `AUTHZ-000` | Command declares no policy — deny by default | `src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs:52` |
| `AUTHZ-001` | No authenticated actor | `.../AuthorizationBehavior.cs:60` |
| `AUTHZ-002` | Role not permitted — what a non-`PlatformAdmin` gets from `ProvisionTenantCommand` | `.../AuthorizationBehavior.cs:83` |
| `AUTHZ-008` | Command names a permission key the catalogue does not know | `.../AuthorizationBehavior.cs:68` |
| `AUTHZ-403` | HTTP-layer permission refusal (`[RequirePermission]`) | `src/NT.QAMS.WebApi/Middleware/ProblemAuthorizationResultHandler.cs:16` |
| PostgreSQL `42501` | RLS `USING`/`WITH CHECK` refusal — **not mapped**, surfaces as HTTP 500 (**GAP-TENANT-006**) | asserted in `tests/NT.QAMS.IntegrationTests/SecurityEventRlsTests.cs:85` |
| PostgreSQL `23503` | Tenant-composite FK refusal (child tenant ≠ parent tenant) | asserted in `tests/NT.QAMS.IntegrationTests/OwnedChildTenancyTests.cs:24,114` |

### 1.4 Domain events

| Event | Payload | File:line | Consumer(s) actually found |
|---|---|---|---|
| `TenantProvisioned` | `(Guid TenantId, string Slug, string Name)` | `src/NT.QAMS.Domain/Tenancy/TenantEvents.cs:10`; raised at `Tenant.cs:54` | **One** — `src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:126-128` (`INotificationHandler<DomainEventNotification<TenantProvisioned>>`) |
| `TenantSuspended` | `(Guid TenantId, string Slug, string Reason)` | `TenantEvents.cs:12`; raised at `Tenant.cs:73` | **None** — no handler anywhere in `src/` |
| `TenantReactivated` | `(Guid TenantId, string Slug)` | `TenantEvents.cs:14`; raised at `Tenant.cs:86` | **None** |
| `TenantTerminated` | `(Guid TenantId, string Slug)` | `TenantEvents.cs:16`; raised at `Tenant.cs:97` | **None** |

The event-routing comment at `TenantEvents.cs:7-8` promises *"TenantProvisioned → Identity seeds the tenant admin + canonical roles, Organization seeds default LOVs; TenantSuspended/Reactivated gate access."* **Neither half is implemented as an event handler.** The admin/role/LOV seeding is done *inline* inside `ProvisionTenantHandler` (`ProvisionTenant.cs:53-72`), not by an event consumer, and nothing gates access on suspension. → **GAP-TENANT-002**, **GAP-TENANT-003**.

### 1.5 Application slices

| Slice | Type | Policy attribute | Handler | File:line |
|---|---|---|---|---|
| `ProvisionTenantCommand(Identifier, Name, AdminEmail, AdminDisplayName, AdminPassword) : ICommand<Guid>` | Command | `[RequireRole(UserRole.PlatformAdmin)]` — the **only** `[RequireRole]` in the solution | `ProvisionTenantHandler` | `src/NT.QAMS.Application/Tenancy/Commands/ProvisionTenant.cs:17-20,34` |
| `GetTenantsQuery : IQuery<IReadOnlyList<TenantDto>>` | Query | *(queries are not gated by `AuthorizationBehavior` — `AuthorizationBehavior.cs:44-47`)* | `GetTenantsHandler` | `src/NT.QAMS.Application/Tenancy/Queries/GetTenants.cs:8,10` |
| `GetWorkspaceQuery(string? Slug) : IQuery<WorkspaceResponse?>` | Query | anonymous by design | `GetWorkspaceQueryHandler` (`internal`) | `src/NT.QAMS.Application/Tenancy/Queries/GetWorkspace.cs:20,22` |
| `GetTenantMfaPolicyQuery : IQuery<bool>` | Query | — | `GetTenantMfaPolicyHandler` | `src/NT.QAMS.Application/Tenancy/Commands/TenantMfaPolicy.cs:8,14` |
| `SetTenantMfaPolicyCommand(bool Require) : ICommand` | Command | `[RequireInternalActor]` | `SetTenantMfaPolicyHandler` | `src/NT.QAMS.Application/Tenancy/Commands/TenantMfaPolicy.cs:11-12,26` |

**`ProvisionTenantValidator`** (`ProvisionTenant.cs:22-32`): `Identifier` NotEmpty + MaxLength(50) · `Name` NotEmpty + MaxLength(200) · `AdminEmail` NotEmpty + EmailAddress + MaxLength(320) · `AdminDisplayName` NotEmpty + MaxLength(150) · `AdminPassword` `StrongPassword()`. Validator failures are **400**, domain failures **422** — the same over-long slug therefore yields 400 from the validator, never `TENANT-002`, unless the validator is bypassed.

**`ProvisionTenantHandler.Handle` — the provisioning transaction, in order** (`ProvisionTenant.cs:37-77`):

1. `tenantScope.Elevate()` — **before any database work** (`:41`), so the very first connection open stamps `app.bypass_rls='on'`.
2. `TenantSlug.Create(command.Identifier)` (`:43`) — may throw `TENANT-001`/`TENANT-002`.
3. Uniqueness pre-check `db.Tenants.AnyAsync(t => t.Slug == slug)` (`:45`) → `TENANT-005` (`:48`).
4. `Tenant.Provision(slug, name)` (`:51`) → `TENANT-003`/`TENANT-004`; raises `TenantProvisioned`.
5. `UserAccount.Create(tenant.Id, email, displayName, hash, UserRole.TenantAdmin)` (`:53-58`).
6. `SystemRoleCatalog.SeedMissingAsync(db, tenant.Id, ct)` (`:66`) — the five starter roles.
7. `admin.AssignRole(adminRole.Id)` where `adminRole` is `seededRoles.Single(r => r.Name == SystemRoleCatalog.TenantAdministrator)` (`:67-68`) — a `Single`, so a duplicate or missing seeded role throws `InvalidOperationException` → 500.
8. `DefaultLovCatalog.SeedMissingAsync(db, tenant.Id, ct)` (`:72`).
9. **One** `SaveChangesAsync` (`:74`) — tenant, admin, roles, role-permissions, LOVs and the outbox rows all commit atomically. This is why the five `saas.tenant` FKs had to become `DEFERRABLE INITIALLY DEFERRED` (defect **SH-D2**, `SCHEMA-HARDENING-REPORT.md` §10).

**`GetWorkspaceQueryHandler` — the anti-enumeration contract** (`GetWorkspace.cs:25-47`): blank slug → `null` (`:27-30`); malformed slug → the `DomainException` from `TenantSlug.Create` is **swallowed** and answered `null` (`:34-41`); the query filters `t.Slug == slug && t.Status == TenantStatus.Active` (`:44`) and projects **`new WorkspaceResponse(t.Name)` only** (`:45`). Unknown, malformed and non-`Active` are therefore indistinguishable. Pinned by `tests/NT.QAMS.WebApi.FunctionalTests/WorkspaceLookupTests.cs:92-103`.

### 1.6 Endpoints — exhaustive for this module

All five logical tenancy endpoints, each dual-exposed on `/api/v{version}/…` (10 route entries in `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt:32,123,124,140,231,232,415,608,637,649`).

| Verb + route | Controller:line | Class gate | Action gate | Success | Documented failures |
|---|---|---|---|---|---|
| `POST /api/tenants` | `TenantsController.cs:15-29` | `[Authorize(Roles = Roles.PlatformAdmin)]` (`:12`) | — | **201** `CreatedAtAction(nameof(GetAll), new { id }, new { id })` (`:28`) | 400 `ProblemDetails` (`:17`), 422 (`:18`) |
| `GET /api/tenants` | `TenantsController.cs:31-34` | same | — | **200** `IReadOnlyList<TenantDto>` | — |
| `GET /api/auth/workspace/{slug}` | `AuthController.cs:47-56` | — | `[AllowAnonymous]` (`:48`) | **200** `WorkspaceResponse` | **404** `application/problem+json` "Workspace not found." (`:55`) |
| `GET /api/tenant-settings/mfa-policy` | `TenantSettingsController.cs:22-24` | `[Authorize]` (`:17`) + `[RequirePermission(PermissionCatalog.TenantSettings, PermissionAction.Manage)]` (`:18`) | — | **200** `TenantMfaPolicyDto` | 401, 403 `AUTHZ-403`, 404 `TENANT-404`, 422 `TENANT-000` |
| `PUT /api/tenant-settings/mfa-policy` | `TenantSettingsController.cs:27-32` | same | — | **204** `NoContent` | as above + `AUTHZ-002` (external auditor blocked by `[RequireInternalActor]`) |

**There is no `GET /api/tenants/{id}`.** `CreatedAtAction(nameof(GetAll), new { id = tenantId }, …)` at `TenantsController.cs:28` names an action whose route template (`api/tenants`, `TenantsController.cs:11,31`) has no `{id}` segment, so the `id` route value is appended as a **query string** — the `Location` header is `/api/tenants?id=<guid>`, not a canonical resource URI. → **GAP-TENANT-005**.

**`PlatformControllers.cs` is in scope but is not a platform control plane.** All five controllers in that file are ordinary tenant-scoped surfaces reached with `[Authorize]` and `[RequirePermission]`; none carries a platform gate:

| Controller | Route | File:line | Actions and gates |
|---|---|---|---|
| `BranchesController` | `api/branches` | `:13,15` | `GET` (no permission gate, `:18`) · `POST` `organization.create` (`:22`) · `POST {id}/deactivate` `organization.manage` (`:27`) |
| `DepartmentsController` | `api/departments` | `:36,38` | `GET` ungated (`:41`) · `POST` `organization.create` (`:45`) · `POST {id}/deactivate` `organization.manage` (`:51`) |
| `TestCatalogController` | `api/test-catalog` | `:60,62` | `GET` ungated (`:65`) · `POST` `organization.create` (`:69`) |
| `LovsController` | `api/lovs` | `:76,78` | `GET` ungated (`:81`) · `POST` `organization.edit` (`:85`) |
| `NotificationsController` | `api/notifications` | `:93,95` | `GET mine` / `POST {id}/read` ungated (`:97,103`) · `GET rules` / `POST rules` / `GET monitor` `notifications.manage` (`:111,116,123`) |

Their isolation is therefore carried entirely by the tenant context, the EF filter and RLS — which is precisely why they belong in this module's test scope even though their business behaviour belongs to `ORG` and `NOTIF`. → **GAP-TENANT-004** (file naming vs contents).

### 1.7 Permission keys

| Key | Built from | Used at | Notes |
|---|---|---|---|
| `tenant-settings.manage` | `PermissionCatalog.TenantSettings` (`src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:106`) × `PermissionAction.Manage` via `PermissionCatalog.Key` (`:194`) | `TenantSettingsController.cs:18` (class-level, so **both** GET and PUT) | The only tenancy permission gate in the build |
| `tenant-settings.view` | same module, `ConfigurationModule` action set = `[View, Manage]` (`PermissionCatalog.cs:125-126`) | **nowhere** | The key exists in `AllKeys` and is seedable, but no endpoint or command requires it — reading the MFA policy demands `manage`. → **GAP-TENANT-007** |
| `organization.create` / `.edit` / `.manage` | `PermissionCatalog.Organization` (`:105`) | `PlatformControllers.cs:22,27,45,51,69,85` | Owned by the `ORG` module; listed for completeness of this file's scope |
| `notifications.manage` | `PermissionCatalog.Notifications` | `PlatformControllers.cs:111,116,123` | Owned by `NOTIF` |

**Seeded-role reach:** `SystemRoleCatalog` grants `tenant-settings.*` to **Tenant Administrator only** — Quality Manager is explicitly excluded (`src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs:111`), Department Head and Analyst are not granted it by their explicit tables (`:123-177`), and External Auditor is excluded because the module is in `GroupAdministration` (`:192,196-198`). Pinned by `tests/NT.QAMS.Application.UnitTests/Authorization/SystemRoleCatalogTests.cs:80`.

**Platform administrators bypass the privilege system entirely:** `ActiveSessionMiddleware` calls `privilegeSetter.SetPlatformAdmin()` for `UserRole.PlatformAdmin` (`src/NT.QAMS.WebApi/Middleware/RequestIdentity.cs:114-117`) instead of resolving a tenant role.

### 1.8 Isolation layer 1 — the EF global query filter

Applied **by convention** in `OnModelCreating`, so a module cannot forget it (`src/NT.QAMS.Infrastructure/Persistence/AppDbContext.cs:149-183`):

- Every entity type is inspected; if `typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType)` (`:168`) one of two filters is applied by reflection (`:174-175`).
- **Plain tenant filter** — `ApplyTenantFilter<TEntity>` (`:186-191`): `HasQueryFilter(e => e.TenantId == _currentTenant.TenantId)` (`:190`).
- **Composed tenant + working-scope filter** — `ApplyTenantAndScopeFilter<TEntity>` for types that are also `IAllocatable` (`:200-208`): tenant equality **AND** (`!HasBranchRestriction || BranchId == null || AllowedBranchIds.Contains(...)`) **AND** the department equivalent (`:203-208`).
- `_currentTenant` is captured once at `:40` from the injected `ICurrentTenant`.

**Consequence that every batch-B case must respect:** under `Elevate()` the setter never assigns `TenantId`, so `_currentTenant.TenantId` is `null` and the layer-1 filter matches nothing. Every elevated code path therefore also calls `.IgnoreQueryFilters()` — verified at `ScheduledSweepService.cs:91,98,105,112,120,129,136` and `KpiSnapshotService.cs:97,105`. **Elevation alone does not grant cross-tenant reads; elevation + `IgnoreQueryFilters()` does.**

### 1.9 Isolation layer 2 — `TenantConnectionInterceptor`

`src/NT.QAMS.Infrastructure/Persistence/Interceptors/TenantConnectionInterceptor.cs`

- Extends `DbConnectionInterceptor`; overrides **only** `ConnectionOpened` (`:23`) and `ConnectionOpenedAsync` (`:29`).
- Emits one statement per open (`:53-54`):
  `SELECT set_config('app.current_tenant', @tenant, false), set_config('app.bypass_rls', @bypass, false)`
- `@tenant` = `currentTenant.TenantId?.ToString() ?? NilTenant` where `NilTenant = Guid.Empty.ToString()` (`:21,55`) — **fail-closed**: the nil UUID matches no row.
- `@bypass` = `currentTenant.IsElevated ? "on" : "off"` (`:56`).
- `is_local = false` → **session** scope, deliberately re-applied on every open (comment `:51-52`).
- Both values are bound parameters, never interpolated (`:60-66`).
- Registered scoped (`DependencyInjection.cs:40`) and added to the context options at `:58`.

**Timing property with teeth:** the GUCs are stamped **when the connection opens**. Changing the tenant or elevating *after* a connection is already open (e.g. inside an open transaction) leaves the stale value in force. `LoginHandler` is written around this — `tenantScope.Set(tenant.Id)` at `src/NT.QAMS.Application/IdentityAccess/Commands/Login.cs:58` executes before any tenant-stamped write, and the ordering is pinned by `tests/NT.QAMS.IntegrationTests/SecurityEventRlsTests.cs:140-161` whose comment states the failure shape explicitly (`:147-151`). `ChangePasswordHandler` does the same at `Login.cs:195`.

### 1.10 `TenantStampInterceptor`

`src/NT.QAMS.Infrastructure/Persistence/Interceptors/TenantStampInterceptor.cs` — a `SaveChangesInterceptor` (`:14`) that runs on both sync and async save (`:17,24`).

- **Aggregates** (`:39-55`): for each `Added` `ITenantScoped` entry, if `TenantId != Guid.Empty` it is left alone (explicit provisioning scope, `:46-49`); otherwise it is set from `currentTenant.TenantId`, or `TENANT-000` is thrown (`:51-54`).
- **Owned children** (`:68-111`): for each `Added` owned entry that has a **shadow** `TenantId` property (`:79-83`), and whose current value is unset (`:85-89`), the value is taken from the tracked owner via `CollectOwnerTenants` (`:91-101`), falling back to the request tenant, else `TENANT-000` (`:105-108`).
- `CollectOwnerTenants` (`:114-131`) maps `AggregateRoot.Id → TenantId` from both `ITenantScoped` (non-empty only, `:121-123`) and `IOptionallyTenantScoped` (`:124-126`).
- The doc at `:60-67` is explicit that the composite FK makes a mismatch impossible regardless — the interceptor is convenience, the FK is the guarantee.

### 1.11 Tenant resolution and session middleware

Pipeline order, read from `src/NT.QAMS.WebApi/Program.cs`:

| Order | Middleware | Line |
|---|---|---|
| 1 | `ObservabilityMiddleware` | `:254` |
| 2 | `SecurityHeadersMiddleware` | `:255` |
| 3 | `UseAuthentication()` | `:263` |
| 4 | `UseRateLimiter()` | `:265` |
| 5 | **`TenantResolutionMiddleware`** | `:266` |
| 6 | **`ActiveSessionMiddleware`** | `:267` |
| 7 | `MfaEnrollmentGateMiddleware` | `:268` |
| 8 | `ChangeReasonMiddleware` | `:269` |
| 9 | `UseAuthorization()` | `:270` |
| 10 | `MapControllers()` | `:272` |

`TenantResolutionMiddleware` (`src/NT.QAMS.WebApi/Middleware/RequestIdentity.cs:53-65`) reads **`context.User.FindFirstValue("tenant_id")`** and calls `tenantSetter.Set(...)` only when it parses as a `Guid` (`:57-61`). It reads **no header, no query string, no route value, no host name** — a spoofed `X-Tenant-Id` has no effect anywhere in the pipeline. If the claim is absent or unparseable the setter is never called and the tenant stays `null`, which the interceptor turns into the nil GUC.

Because resolution sits **after** `UseAuthentication()`, the claim is on a validated principal, and because it sits **before** `ActiveSessionMiddleware`'s database read (`:93-96`), that read already runs under the caller's tenant GUC.

`ActiveSessionMiddleware` (`:80-131`) re-reads `user_account` by `sub` on every authenticated request and denies with `AUTH-006` if the row is missing or inactive (`:98-101`) or `AUTH-007` if the token role differs from the database role (`:104-108`). **It does not read `saas.tenant` and never checks tenant status** — see **GAP-TENANT-001**.

### 1.12 Persistence

**`saas.tenant`** — `TenantConfiguration` (`src/NT.QAMS.Infrastructure/Persistence/Configurations/TenantConfiguration.cs`): table `("tenant", "saas")` (`:11`); single-column PK `Id` (`:13`); `Slug` converted to/from `TenantSlug`, column `identifier`, max 50 (`:15-18`); **unique index on `Slug`** (`:20`); `Name` max 200 (`:22`); `Status` `HasConversion<string>()` max 20 (`:25-27`); `SuspensionReason` max 500 (`:29`); `Settings` **owned, same row** with explicit column names (`:34-42`); `DomainEvents` ignored (`:44`).

Measured columns of `saas.tenant` (16): `id, identifier, name, status, password_expiry_days, calibration_reminder_days, sop_expiry_reminder_months, default_language, time_zone, suspension_reason, created_at_utc, created_by, modified_at_utc, modified_by, require_mfa_privileged, created_by_user_id`.

Measured constraints on `saas.tenant`: `pk_tenant PRIMARY KEY (id)` and
`ck_tenant_status_domain CHECK (status = ANY (ARRAY['Provisioning','Active','Suspended','Terminated']))`.

**`saas.tenant` has no RLS** (`relrowsecurity=f, relforcerowsecurity=f`) — correct: it *is* the control plane. Its confidentiality rests entirely on (a) the `[Authorize(Roles=PlatformAdmin)]` gate on `TenantsController` and (b) every other reader keying by `ICurrentTenant.TenantId` rather than by a caller-supplied id. Both tenancy MFA-policy handlers do exactly that (`TenantMfaPolicy.cs:19-20,31-32`).

**Measured schema totals (2026-08-01):** 97 tables across 5 schemas — `qams` 89, `audit` 4, `saas` 2, `read` 1, `public` 1. **The `ref` schema does not exist** even though `deploy/harden-runtime-role.sql` grants on it six times → **GAP-TENANT-008**.

**Migrations in scope**

| Migration | File | What it does |
|---|---|---|
| `ActivateForcedTenantRls` | `.../Migrations/20260726081443_ActivateForcedTenantRls.cs` | `DO` block iterating `pg_policies WHERE policyname='tenant_isolation'` (`:29`); per table `ENABLE` + `FORCE` RLS, drops and recreates the policy with the GUC predicate on **both** `USING` and `WITH CHECK` (`:31-44`). `Down()` restores the dormant `USING`-only policy and `NO FORCE` (`:56-72`). **Because it iterates existing policies it can only harden tables that already had one — this is the mechanism that left `audit.security_event` and `qams.ref_counter` behind.** |
| `RelaxAuditRlsWriteCheck` | `.../20260726103650_RelaxAuditRlsWriteCheck.cs` | Same loop restricted to `schemaname='audit'` (`:26-27`); rewrites `WITH CHECK` to `tenant_id IS NULL OR tenant_id = GUC OR bypass` (`:36-40`); `USING` unchanged, so null-tenant rows stay invisible to tenant reads (comment `:18`) |
| `Hardening2_RlsGapClosure` | `.../20260731181845_Hardening2_RlsGapClosure.cs` | `audit.security_event`: `ENABLE` + `FORCE` + `CREATE POLICY … FOR ALL` with the null-tolerant write shape (`:17-28`). `qams.ref_counter`: same but **strict** write, because its `tenant_id` is `NOT NULL` (`:33-43`). `Down()` drops both (`:49-56`) |
| `Hardening4_ChildTenancy` | `.../20260731201114_Hardening4_ChildTenancy.cs` (1 095 lines) | Adds `tenant_id uuid NOT NULL DEFAULT '000…0'` to **30** owned child tables (`:14-252`); declares a **transaction-local** `SELECT set_config('app.bypass_rls','on',true)` at the top of the SQL block (`:260`) because FORCE RLS otherwise makes the backfill a silent no-op *and* breaks the FK integrity check; backfills each child from its parent then drops the default (`:263-322`); adds **24** parent `UNIQUE (id, tenant_id)` constraints (`:325-348`); swaps **28** single-column CASCADE FKs for tenant-composite ones (`:353-435`); then `ENABLE` + `FORCE` + `CREATE POLICY tenant_isolation` on all **30** children (`:438-…`, 30 `ENABLE ROW LEVEL SECURITY` statements) |

The 30 child tables: `assessment_result, audit_checklist_item, audit_finding, calibration_record, capa_action, carryover_reading, detection_measurement, document_version, environmental_reading, instrument_reading, interference_measurement, intermediate_check, linearity_measurement, lot_sample_pair, maintenance_record, measurement_pair, mitigation_action, objective_progress, outlier_point, precision_measurement, pt_plan_item, rca_record, reference_sample, review_decision, role_permission, supplier_certificate, uncertainty_component, user_branch_access, user_department_access, validation_replicate`.

### 1.13 Tenant lifecycle states

`TenantStatus` (`src/NT.QAMS.Domain/Tenancy/TenantStatus.cs:7-14`) declares four values. The database `CHECK` accepts all four. **Three are reachable; `Provisioning` is not** — `Tenant.Provision` sets `Status = TenantStatus.Active` directly in the private constructor (`Tenant.cs:27`), and no other assignment to `Provisioning` exists in `src/` or `tests/`. Live data agrees: `SELECT status, count(*) FROM saas.tenant` = `Active|23`, no other value. → **GAP-TENANT-009**.

### 1.14 Existing automated coverage (what the case files must not duplicate)

| Test | File:line | Proves |
|---|---|---|
| `Rls_isolates_tenants_fails_closed_and_honours_bypass` | `tests/NT.QAMS.IntegrationTests/RlsTenantIsolationTests.cs:19-54` | Tenant A sees 1, B sees 1, nil sees 0, elevated sees 2 — with `IgnoreQueryFilters()` so RLS alone is on trial (`:44`) |
| `With_check_rejects_writing_a_row_for_another_tenant` | `.../RlsTenantIsolationTests.cs:56-75` | `DbUpdateException` on a foreign-tenant insert |
| `Security_events_are_tenant_isolated_and_preauth_rows_are_platform_only` | `.../SecurityEventRlsTests.cs:29-59` | **Positive** `audit.security_event` isolation: 1 / 1 / 0 / 3 |
| `Security_event_write_check_allows_own_and_preauth_but_rejects_foreign` | `.../SecurityEventRlsTests.cs:61-88` | Own + null accepted, foreign rejected with SqlState **42501** (`:85`) |
| `Ref_counter_is_tenant_isolated` | `.../SecurityEventRlsTests.cs:90-132` | `qams.ref_counter` isolation through raw SQL |
| `Login_shaped_write_passes_when_the_request_is_scoped_to_the_events_tenant` | `.../SecurityEventRlsTests.cs:140-161` | The GUC-timing pin |
| `Child_rows_are_visible_only_to_their_own_tenant` (theory, 7 families) | `.../OwnedChildTenancyTests.cs:31-93` | Per-tenant counts sum to the elevated total; nil sees 0; no foreign row visible |
| `A_child_cannot_be_written_with_a_tenant_that_differs_from_its_parent` | `.../OwnedChildTenancyTests.cs:95-126` | `23503` on drift, **with an accepted control insert** so the constraint is shown to discriminate |
| `Every_owned_child_table_carries_tenant_id_and_full_rls` | `.../OwnedChildTenancyTests.cs:128-155` | Structural sweep — any future `NOT NULL tenant_id` table without RLS fails the build |
| `Rls_suite_runs_as_a_least_privilege_role` · `Guard_findings_match_the_catalog_facts` · `Boot_as_owner_is_rejected` | `.../RuntimeRolePrivilegeTests.cs:35-101` | The suite refuses to prove nothing under `SUPERUSER`/`BYPASSRLS`; `DatabaseRoleGuard` matches the catalog; Production boot refuses |
| `TenantTests` (7 facts/theories) | `tests/NT.QAMS.Domain.UnitTests/Tenancy/TenantTests.cs:13-94` | `TENANT-003/010/011/012/013`, slug normalisation, 7 invalid-slug partitions |
| `ProvisionTenantTests` (2 facts) | `tests/NT.QAMS.Application.UnitTests/Tenancy/ProvisionTenantTests.cs:34-78` | Atomic tenant+admin+outbox; `TENANT-005` on a case-different duplicate |
| `WorkspaceLookupTests` (2 facts + 3-case theory) | `tests/NT.QAMS.WebApi.FunctionalTests/WorkspaceLookupTests.cs:41-103` | Name resolves; payload is exactly `{name}`; unknown/malformed/short all 404 `problem+json` |
| `RegulatedFlowRealDatabaseTests` (4 skippable facts) | `tests/NT.QAMS.WebApi.FunctionalTests/RegulatedFlowRealDatabaseTests.cs:92-197` | Provisioning against real FKs; sign-in writes its security event through RLS; owned-child ledger visibility; a tenant sees only its own users over HTTP |
| `UserAccountTenantBoundTests` (9 cases) | `tests/NT.QAMS.Architecture.Tests/UserAccountTenantBoundTests.cs:31-…` | Source-level guard: every `db.Users` / `Set<UserAccount>()` query is tenant-bounded, actor-bounded, or carries a written `tenant-unbounded:` exemption |

**Guard on the guards:** `RealPostgresFixture` refuses to run the suite if FORCE RLS is absent (`tests/NT.QAMS.IntegrationTests/RealPostgresFixture.cs:42-51`) or if the connection role has `SUPERUSER`/`BYPASSRLS` (`:53-65`), so these tests can never pass while proving nothing. `RuntimeRolePrivilegeTests.RequireDatabase` (`:24-33`) turns a skip into a **failure** whenever `QMS_ITEST_POSTGRES` is set, which CI always does.

---

## 2. Divergences from the commissioning brief

| # | What the brief assumes | What the code actually does | File:line | Gap |
|---|---|---|---|---|
| D-1 | Tenant is resolved from a sub-domain / host header / `X-Tenant-Id` | Resolved from the validated JWT `tenant_id` claim **only**; no header, query, route or host path exists | `src/NT.QAMS.WebApi/Middleware/RequestIdentity.cs:53-64`; doc `src/NT.QAMS.Application/Abstractions/ICurrentTenant.cs:5-6` | — (brief is weaker; code is stricter — no gap, but batch-C `SEC` cases must prove the spoof is inert) |
| D-2 | Tenant lifecycle `Provisioning → Active → Suspended ⇄ Active → Terminated` is operable | Only `Provision` is reachable. `Suspend`, `Reactivate` and `Terminate` exist on the aggregate but have **no command, no handler, no endpoint and no test beyond the domain unit tests** | `Tenant.cs:8,58,76,89`; absent from `ApiSurface.approved.txt`; `src/NT.QAMS.Contracts/Tenancy/TenancyContracts.cs` has no suspend/terminate request record | **GAP-TENANT-002** |
| D-3 | `TenantSuspended` / `TenantReactivated` "gate access" | No handler subscribes to them; the only tenancy event with a consumer is `TenantProvisioned` | `TenantEvents.cs:7-8` (the promise) vs `src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:126-128` (the only handler) | **GAP-TENANT-003** |
| D-4 | Suspending a tenant cuts off its users | Only the **sign-in** path checks status (`AUTH-002`). A user holding a valid 15-minute access token keeps working after suspension; `ActiveSessionMiddleware` never reads `saas.tenant` | `Login.cs:60-63` vs `RequestIdentity.cs:80-131` | **GAP-TENANT-001** |
| D-5 | A tenant record starts in `Provisioning` | The factory assigns `Active` directly; `Provisioning` is unreachable dead state (live data: 23/23 `Active`) | `Tenant.cs:27`; `TenantStatus.cs:9` | **GAP-TENANT-009** |
| D-6 | `PlatformControllers.cs` holds the platform control plane | It holds five ordinary tenant-scoped controllers (branches, departments, test catalog, LOVs, notifications); the real control plane is `TenantsController` alone | `PlatformControllers.cs:12-129` vs `TenantsController.cs:12` | **GAP-TENANT-004** |
| D-7 | `POST /api/tenants` returns a `Location` for the new tenant | `CreatedAtAction(nameof(GetAll), new { id }, …)` targets a template with no `{id}`, so `Location` becomes `/api/tenants?id=<guid>`; there is no `GET /api/tenants/{id}` | `TenantsController.cs:11,28,31` | **GAP-TENANT-005** |
| D-8 | An isolation violation is reported to the caller as a typed authorization error | An RLS `WITH CHECK` refusal arrives as `DbUpdateException`(`PostgresException` 42501) and matches **no arm** of the exception handler → unhandled → **HTTP 500** with no code | `DomainExceptionHandler.cs:26-82` (no `DbUpdateException` or `PostgresException` arm); SqlState asserted at `SecurityEventRlsTests.cs:85` | **GAP-TENANT-006** |
| D-9 | The tenant list and the MFA policy are governed by the same permission model | The tenant list is governed by a **role string** (`[Authorize(Roles=PlatformAdmin)]`), the MFA policy by a **permission key**. Two different authorization mechanisms guard one module | `TenantsController.cs:12` vs `TenantSettingsController.cs:18` | **GAP-TENANT-010** |
| D-10 | Reading a setting requires a *view* privilege | `tenant-settings.view` exists in the catalogue, is seeded, and is required by nothing — the GET is gated on `tenant-settings.manage` | `PermissionCatalog.cs:106,125-126,184`; `TenantSettingsController.cs:18,22` | **GAP-TENANT-007** |
| D-11 | Production RLS enforcement is achievable by running the documented hardening script | `deploy/harden-runtime-role.sql` sets `ON_ERROR_STOP on` at `:22` and then grants on a schema named `ref` at `:40`, `:48`, `:55`, `:71`, `:76-77`, `:80`. **No `ref` schema exists** — the measured schema list is `audit, public, qams, read, saas`, and no migration ever calls `EnsureSchema("ref")`. The script aborts at line 40 | `deploy/harden-runtime-role.sql:22,40,48,55,71,76-80`; migrations create only `qams` (`20260721211309_InitialFoundation.cs:14-15`), `saas` (`:17-18`), `audit` (`20260721232300_ComplianceAndAuth.cs:14-15`), `read` (`20260724235242_ReportingKpiSnapshots.cs:14-15`) | **GAP-TENANT-008** |
| D-12 | Suspended/terminated tenants are excluded from background processing | `KpiSnapshotService` filters `Status == Active` (`KpiSnapshotService.cs:90`), but `ScheduledSweepService` sweeps **every** tenant's records with no status filter — calibration lockouts, competency expiry, supplier suspension and their notifications continue for a suspended or terminated tenant | `ScheduledSweepService.cs:86-152` (no `db.Tenants` reference anywhere in the file) | **GAP-TENANT-011** |
| D-13 | `TENANT-nnn` codes are a single namespace | `TENANT-004` is **both** the domain code "Tenant name must not exceed 200 characters" (`Tenant.cs:50`) **and** the identifier of the Phase-0 database-role finding, used as a code in two source comments | `Tenant.cs:50` vs `src/NT.QAMS.Infrastructure/Security/DatabaseRoleGuard.cs:6` and `tests/NT.QAMS.IntegrationTests/RuntimeRolePrivilegeTests.cs:9`, `RealPostgresFixture.cs:53` | **GAP-TENANT-012** |
| D-14 | `Tenant.UpdateSettings` is guarded like every other domain mutation | It throws a bare `ArgumentNullException` (`Tenant.cs:102`), which no handler arm maps → 500, not 422. It is also **unreachable**: nothing in `src/` calls it | `Tenant.cs:100-104`; `DomainExceptionHandler.cs:81` | **GAP-TENANT-013** |
| D-15 | Every cross-tenant elevation is enumerated and reviewable | Eight `Elevate()` call sites across six components exist; the conventions file lists five, and there is no automated test that enumerates or caps them | see §0.1 and §4.3 | **GAP-TENANT-014** |

---

## 3. State-transition matrices

### 3.1 `TenantStatus` — aggregate-level transition matrix

Rows are the current state; columns are the transition method. `✔` = permitted; a code = refused with that code (all three refusals are `InvalidStateTransitionException` → **HTTP 409** by `DomainExceptionHandler.cs:45-51`); `—` = not applicable.

| From \ Method | `Provision` (factory) | `Suspend(reason)` | `Reactivate()` | `Terminate()` | `SetPrivilegedMfaPolicy(b)` |
|---|---|---|---|---|---|
| *(none — factory)* | ✔ → **Active**, raises `TenantProvisioned` (`Tenant.cs:27,54`) | — | — | — | — |
| `Provisioning` (**unreachable**, `Tenant.cs:27`) | — | `TENANT-010` (`:63`) | `TENANT-012` (`:81`) | ✔ → `Terminated` (`:96`) | ✔ (unguarded, `:107`) |
| `Active` | — | ✔ → `Suspended`, sets `SuspensionReason`, raises `TenantSuspended` (`:71-73`) | `TENANT-012` (`:81`) | ✔ → `Terminated`, raises `TenantTerminated` (`:96-97`) | ✔ (unguarded) |
| `Suspended` | — | `TENANT-010` (`:63`) | ✔ → `Active`, **clears** `SuspensionReason`, raises `TenantReactivated` (`:84-86`) | ✔ → `Terminated` (`:96`) | ✔ (unguarded) |
| `Terminated` | — | `TENANT-010` (`:63`) | `TENANT-012` (`:81`) | `TENANT-013` (`:93`) | ✔ (unguarded) |

Additional guard inside `Suspend`, independent of state: a blank/whitespace reason throws `TENANT-011` (`:66-69`) — and it is checked **after** the state guard, so on a `Suspended` tenant a blank reason still yields `TENANT-010`, not `TENANT-011`. Batch A must order its cases accordingly.

**Reachability, honestly stated.** Every transition except `Provision` is **unreachable through the API** (§2 D-2). Cases for rows 2–5 of this matrix are therefore `Unit` level against the aggregate, or `[GD]` Gap-dependent on **GAP-TENANT-002** if written at API level.

### 3.2 Effect of tenant status on downstream behaviour — measured, not assumed

| Behaviour | `Active` | `Suspended` | `Terminated` | Where enforced |
|---|---|---|---|---|
| Sign in at `/t/{slug}` | permitted | **refused** `AUTH-002` / 401 | **refused** `AUTH-002` / 401 | `Login.cs:60-63` |
| Anonymous workspace lookup | 200 `{name}` | **404** | **404** | `GetWorkspace.cs:44` |
| Existing access token keeps working | yes | **yes — not revoked** | **yes — not revoked** | nothing checks it: `RequestIdentity.cs:80-131` |
| Refresh-token rotation | yes | *not verified in this pass* `[RNV]` | *not verified* `[RNV]` | — |
| Appears in `GET /api/tenants` | yes | yes (`Status` string in `TenantDto`) | yes | `GetTenants.cs:16-25` — **no status filter** |
| KPI snapshot job | runs | skipped | skipped | `KpiSnapshotService.cs:90` |
| Scheduled compliance sweep | runs | **runs** | **runs** | `ScheduledSweepService.cs:86-152` — no status filter |
| Outbox draining of its events | runs | runs | runs | `OutboxProcessor.cs:102` — cross-tenant by design |
| RLS still fences its rows | yes | yes | yes | policy is status-blind |

### 3.3 Tenant-context state machine (per request / per unit of work)

`CurrentTenant` (`src/NT.QAMS.Infrastructure/Services/RequestContext.cs:12-27`) is a three-state scoped object. Transitions are one-way within a scope: **there is no `Demote()`**.

| State | `TenantId` | `IsResolved` | `IsElevated` | GUCs stamped on next connection open | Reached by |
|---|---|---|---|---|---|
| **Unresolved** (initial) | `null` | `false` | `false` | `current_tenant='00000000-0000-0000-0000-000000000000'`, `bypass_rls='off'` | scope creation; anonymous request; a request whose JWT has no `tenant_id` (platform admin) |
| **Scoped** | the tenant | `true` | `false` | `current_tenant=<tenant>`, `bypass_rls='off'` | `Set(id)` — `TenantResolutionMiddleware:60`, `Login.cs:58`, `Login.cs:195` |
| **Elevated** | `null` *(normally)* | `false` | `true` | `current_tenant='000…0'`, `bypass_rls='on'` | `Elevate()` — the eight sites in §4.3 |
| **Scoped + Elevated** | the tenant | `true` | `true` | `current_tenant=<tenant>`, `bypass_rls='on'` | reachable in principle (`Set` then `Elevate`); **no production path does this** — only `tests/.../SecurityEventRlsTests.cs` style fixtures |

| From \ Call | `Set(id)` | `Clear()` | `Elevate()` |
|---|---|---|---|
| Unresolved | → Scoped | → Unresolved (no-op) | → Elevated |
| Scoped | → Scoped (re-pointed, **no guard against re-pointing**) | → Unresolved | → Scoped + Elevated |
| Elevated | → Scoped + Elevated | → **Unresolved** (`Clear()` resets `IsElevated=false`, `RequestContext.cs:20-24`) | → Elevated (no-op) |
| Scoped + Elevated | → re-pointed | → Unresolved | → no-op |

Two properties worth a case each: **(a)** `Set` may be called repeatedly with different ids inside one scope and nothing objects; **(b)** `Clear()` is the *only* way back from elevation, and nothing in the request pipeline calls it — the scope's disposal is what ends elevation.

### 3.4 Row-visibility state machine (what a query actually returns)

Two independent layers compose. `EF` = the global query filter (`AppDbContext.cs:186-208`); `RLS` = the PostgreSQL policy.

| Context | `IgnoreQueryFilters()`? | EF filter admits | RLS admits | Net result |
|---|---|---|---|---|
| Scoped to T | no | rows of T | rows of T | **rows of T** |
| Scoped to T | yes | all | rows of T | rows of T (RLS alone — the shape the integration tests assert) |
| Unresolved | no | `TenantId == null` → nothing | nothing (nil GUC) | **empty, fail-closed both layers** |
| Unresolved | yes | all | nothing | **empty — RLS is the last line** |
| Elevated | no | `TenantId == null` → nothing | all | **empty** — the trap: elevation without `IgnoreQueryFilters()` returns nothing |
| Elevated | yes | all | all | **all tenants** — the intended cross-tenant read |
| Scoped to T **and** elevated | no | rows of T | all | rows of T |
| Scoped to T **and** elevated | yes | all | all | all tenants |

---

## 4. Decision tables

### 4.1 Every RLS-protected table and its policy predicate

Measured 2026-08-01 from `pg_policies` / `pg_class`. **90 tables, 90 `tenant_isolation` policies, 90 `relforcerowsecurity=true`, 0 parity violations.** Exactly **two** predicate shapes exist.

**Shape S — strict (86 tables).** Applied to every `qams.*` and `read.*` table with a `NOT NULL tenant_id`.

```sql
USING      (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
            OR current_setting('app.bypass_rls', true) = 'on')
WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
            OR current_setting('app.bypass_rls', true) = 'on')
```
Source: `20260726081443_ActivateForcedTenantRls.cs:34-44` (bulk), `20260731181845_Hardening2_RlsGapClosure.cs:37-42` (`ref_counter`), `20260731201114_Hardening4_ChildTenancy.cs:438+` (the 30 children).

**Shape R — relaxed on write (4 tables, the whole `audit` schema).** `USING` identical to S; `WITH CHECK` additionally admits `tenant_id IS NULL`.

```sql
WITH CHECK (tenant_id IS NULL
            OR tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
            OR current_setting('app.bypass_rls', true) = 'on')
```
Source: `20260726103650_RelaxAuditRlsWriteCheck.cs:36-40`; `20260731181845_Hardening2_RlsGapClosure.cs:25-27` for `security_event`.
**Because `USING` is unchanged, a null-tenant audit row is writable by anyone but readable only under `bypass_rls='on'`** — it is a platform-only record. That asymmetry is the single most important thing for a `LEDGER`/`TENANT` case author to get right.

| Shape | Schema | Tables |
|---|---|---|
| **R** | `audit` | `audit_trail`, `electronic_signature`, `field_change`, `security_event` |
| **S** | `read` | `kpi_snapshot` |
| **S** | `qams` (85) | `archive_entry`, `assessment_result`, `audit`, `audit_checklist_item`, `audit_finding`, `audit_trail_review`, `branch`, `calibration_record`, `capa_action`, `carryover_reading`, `carryover_study`, `change_request`, `competency_record`, `complaint`, `conflict_declaration`, `context_issue`, `controlled_document`, `department`, `detection_limit_study`, `detection_measurement`, `document_acknowledgement`, `document_controlled_copy`, `document_version`, `environmental_reading`, `equipment_item`, `escalation_timer`, `feedback_entry`, `file_reference`, `instrument_comparability_study`, `instrument_reading`, `interested_party`, `interference_measurement`, `interference_study`, `intermediate_check`, `linearity_measurement`, `linearity_study`, `lot_comparison_study`, `lot_sample_pair`, `lov_entry`, `maintenance_record`, `management_review`, `measurement_pair`, `method_comparison_study`, `mitigation_action`, `monitoring_point`, `nonconformance`, `notification_dispatch`, `notification_rule`, `objective_progress`, `outlier_point`, `outlier_screening`, `precision_measurement`, `precision_study`, `pt_enrollment`, `pt_plan`, `pt_plan_item`, `qc_profile`, `qc_run`, `quality_objective`, `quality_policy`, `rca_record`, `ref_counter`, `reference_interval_study`, `reference_sample`, `reference_standard`, `review_decision`, `risk_item`, `role`, `role_permission`, `sigma_assessment`, `sla_definition`, `supplier`, `supplier_certificate`, `supplier_evaluation`, `test_authorization`, `test_catalog_item`, `training_assignment`, `uncertainty_budget`, `uncertainty_component`, `user_access_review`, `user_branch_access`, `user_department_access`, `validation_replicate`, `validation_study`, `work_task` |

**Not RLS-protected, and correctly so:** `saas.tenant` and `saas.password_history` (control plane / no `tenant_id`), `public.__EFMigrationsHistory`. **Not RLS-protected as an accepted deviation:** `qams.user_account`, `qams.outbox_event` — see §4.4.

### 4.2 Predicate truth table (per row, per operation)

Let `G` = `app.current_tenant`, `B` = `app.bypass_rls`, `t` = the row's `tenant_id`.

| # | `G` | `B` | `t` | Shape S — read | Shape S — write | Shape R — read | Shape R — write |
|---|---|---|---|---|---|---|---|
| 1 | nil / `'00000000-…'` | `off` | any | **hidden** | **refused 42501** | **hidden** | refused unless `t IS NULL` |
| 2 | `''` (empty string) | `off` | any | `NULLIF→NULL`, `t = NULL` is `NULL` ⇒ **hidden** | **refused** | **hidden** | refused unless `t IS NULL` |
| 3 | unset GUC | `off` | any | `current_setting(...,true)` → `NULL` ⇒ **hidden** | **refused** | **hidden** | refused unless `t IS NULL` |
| 4 | `A` | `off` | `A` | visible | accepted | visible | accepted |
| 5 | `A` | `off` | `B` | hidden | **refused 42501** | hidden | **refused 42501** |
| 6 | `A` | `off` | `NULL` | hidden (`NULL = A` is `NULL`) | n/a — column is `NOT NULL` | **hidden** | **accepted** ← the pre-auth case |
| 7 | `A` | `on` | any | visible | accepted | visible | accepted |
| 8 | nil | `on` | any | visible | accepted | visible | accepted |
| 9 | `A` | `'ON'` / `'true'` / `'1'` | `B` | hidden — the comparison is `= 'on'`, **case-sensitive, exact** | refused | hidden | refused |

Row 9 is worth an explicit case: the predicate is a literal string equality against `'on'` (`ActivateForcedTenantRls.cs:38,42`), so only the exact lower-case token elevates.

### 4.3 Elevation-path table — which code may call `Elevate()`, and why

`Elevate()` (`src/NT.QAMS.Infrastructure/Services/RequestContext.cs:26`) sets `IsElevated=true` for the remaining life of the DI scope; the next connection open stamps `app.bypass_rls='on'` (`TenantConnectionInterceptor.cs:56`). The contract in `ICurrentTenant.cs:32-33` is *"Must never be called on a request handling end-user input."*

| # | Call site | File:line | Scope kind | Why it must bypass RLS | Also needs `IgnoreQueryFilters()`? | Reachable from an HTTP request? |
|---|---|---|---|---|---|---|
| 1 | `ProvisionTenantHandler.Handle` | `ProvisionTenant.cs:41` | **request** (`POST /api/tenants`) | The tenant does not exist yet, so the platform-admin request carries no tenant; the seed writes (tenant, admin, 5 roles, ~536 role-permissions, LOVs, outbox rows) would all fail `WITH CHECK` | No — every write is explicitly stamped with `tenant.Id` | **YES** — the one elevation on a request path. Gated by `[Authorize(Roles=PlatformAdmin)]` (`TenantsController.cs:12`) **and** `[RequireRole(UserRole.PlatformAdmin)]` (`ProvisionTenant.cs:17`) — two independent gates |
| 2 | `StartupSeeding.BackfillStarterListOfValuesAsync` | `StartupSeeding.cs:99` | hosted-service scope at boot | Iterates every tenant to add missing LOV categories | Reads `db.Tenants` (not `ITenantScoped`, unfiltered) then `DefaultLovCatalog` scopes explicitly | No |
| 3 | `StartupSeeding.BackfillRolesAndAssignmentsAsync` | `StartupSeeding.cs:132` | hosted-service scope at boot | Seeds starter roles per tenant and assigns unassigned accounts — **reads `db.Users` across every tenant**, the sole `tenant-unbounded:` exemption in the codebase (`:157-158`) | Yes — `db.Roles.IgnoreQueryFilters()` at `:149`; `db.Users` is bounded by `RoleId == null && TenantId != null` (`:160-161`) | No |
| 4 | `KpiSnapshotService.SnapshotAllTenantsAsync` | `KpiSnapshotService.cs:63` | `BackgroundService` scope | Upserts one snapshot row per **Active** tenant | Yes — `.IgnoreQueryFilters()` on every read (`:97,105,…`) | No. Additionally leader-elected via `AdvisoryLockKeys.KpiSnapshot` (`:70`) |
| 5 | `ScheduledSweepService.RunSweepAsync` | `ScheduledSweepService.cs:64` | `BackgroundService` scope | Proposes calibration-due, grace-lockout, competency-expiry, authorization-expiry, supplier-suspension, standard-expiry and document-review transitions across all tenants | Yes — `.IgnoreQueryFilters()` on all eight reads (`:91,98,105,112,120,129,136,145`) | No. Leader-elected via `AdvisoryLockKeys.ComplianceSweep` (`:71`) |
| 6 | `OutboxProcessor.ProcessBatchAsync` | `OutboxProcessor.cs:102` | `BackgroundService` scope | Chains `audit_trail` rows for many tenants in one `SaveChanges` (comment `:100-101`) | n/a — `outbox_event` is outside RLS (B9) | No |
| 7 | `OutboxProcessor.RefreshQueueStatsAsync` | `OutboxProcessor.cs:225` | `BackgroundService` scope | Backlog / dead-letter / oldest-age gauges over all tenants | n/a | No |
| 8 | `OutboxProcessor.RunRetentionPurgeAsync` | `OutboxProcessor.cs:246` | `BackgroundService` scope | Deletes processed outbox rows and expired idempotency records past retention | n/a | No |

**Decision rule for a reviewer (and for the case authors):** an `Elevate()` call is legitimate only when **all four** hold — (i) the unit of work is genuinely cross-tenant or pre-tenant; (ii) it is not reachable from unauthenticated or ordinary-user input; (iii) every read it performs either declares `IgnoreQueryFilters()` deliberately or is explicitly bounded; (iv) every write it performs carries an explicitly stamped `tenant_id`. Site 1 is the only one where (ii) is satisfied by an authorization gate rather than by being unreachable — so it carries **two** gates. **No test enumerates or caps this list** → **GAP-TENANT-014**.

### 4.4 Accepted-deviation table — B9

`SCHEMA-HARDENING-REPORT.md` §8: *"Decision: permanently accepted. Accepted by A. Awad (System Owner) on 2026-08-01. … This closes B9; it is no longer a backlog item."*

Re-measured 2026-08-01: the catalog query for "table carries `tenant_id` but has no `tenant_isolation` policy" returns **exactly two rows** — the deviation has not grown.

| Table | `tenant_id` | `relrowsecurity` | `relforcerowsecurity` | Why RLS cannot express the rule | Compensating control | Verification |
|---|---|---|---|---|---|---|
| `qams.user_account` | **nullable** | `f` | `f` | The `tenant_isolation` predicate is *false for NULL*, so every platform administrator would become invisible to the platform itself, and authentication — which necessarily runs before a tenant is resolved — would break. A null-tolerant predicate would make every platform row visible to every tenant, i.e. isolate nothing (`SCHEMA-HARDENING-REPORT.md` §8) | All 27 access sites bounded by (a) an explicit `TenantId ==` predicate, (b) the authenticated actor's own id from the validated JWT `sub`, (c) an id set already derived from a tenant-filtered query, or (d) a tenant-resolved `RoleId`; plus `users.view`/`users.manage` gating and `ActiveSessionMiddleware`'s per-request re-check | **Build-time, not prose** — `tests/NT.QAMS.Architecture.Tests/UserAccountTenantBoundTests.cs:49-68` matches query starts and required bounds source-side; a `tenant-unbounded:` comment is the only exemption and is used exactly once (`StartupSeeding.cs:157`). Also `RegulatedFlowRealDatabaseTests.A_tenant_sees_only_its_own_users_over_http` (`:196`) |
| `qams.outbox_event` | **nullable** | `f` | `f` | The outbox processor drains cross-tenant by design; a tenant predicate would stop delivery outright | Only three code paths touch the table (`OutboxInterceptor` writes, `OutboxProcessor` drains, its EF configuration); no tenant-facing read surface exists; the processor runs under `Elevate()` (`OutboxProcessor.cs:102`) | `tests/NT.QAMS.IntegrationTests/OutboxResilienceTests.cs` |

**Residual risk, as accepted (quoting the decision, not softening it):** *"These controls are discipline, not structure. A future query that lists `user_account` without a bound would leak across tenants and nothing in the database would stop it."* The guard found a real defect on its first run — `GetRolesHandler` was scanning every tenant's users to build a member-count map.

**Revisit triggers (all three must be tested as *watch* conditions, not as pass/fail cases):** (1) `user_account` is split into platform and tenant tables; (2) a user-listing endpoint is added that is not privilege-gated; (3) an external assessor or penetration test flags it.

**These two are the *only* permanent exceptions.** `audit.security_event` was formerly a third and is **CLOSED** in v1.51.2 (`Hardening2_RlsGapClosure.cs:17-28`; measured `rls=true force=true policy=tenant_isolation`). Author **positive** isolation coverage against it. A failing-condition case for `security_event` RLS would be authoring a defect that no longer exists.

### 4.5 Authorization decision table for the tenancy surface

`PA` = PlatformAdmin · `TA` = TenantAdmin (holding `tenant-settings.manage`) · `QM`/`DH`/`AN` = seeded Quality Manager / Department Head / Analyst · `EA` = External Auditor · `anon` = unauthenticated.

| Endpoint | anon | PA | TA | QM | DH / AN | EA | Deciding line |
|---|---|---|---|---|---|---|---|
| `GET /api/auth/workspace/{slug}` | **200/404** | 200/404 | 200/404 | 200/404 | 200/404 | 200/404 | `[AllowAnonymous]` `AuthController.cs:48` |
| `POST /api/tenants` | 401 | **201** | 403 | 403 | 403 | 403 | `[Authorize(Roles=PlatformAdmin)]` `TenantsController.cs:12`; second gate `AUTHZ-002` from `[RequireRole]` `ProvisionTenant.cs:17` |
| `GET /api/tenants` | 401 | **200** | 403 | 403 | 403 | 403 | same class gate |
| `GET /api/tenant-settings/mfa-policy` | 401 | 403 † | **200** | 403 | 403 | 403 | `[RequirePermission(tenant-settings, Manage)]` `TenantSettingsController.cs:18` → `AUTHZ-403` |
| `PUT /api/tenant-settings/mfa-policy` | 401 | 403 † | **204** | 403 | 403 | 403 ‡ | as above, plus `[RequireInternalActor]` `TenantMfaPolicy.cs:11` → `AUTHZ-002`/403 |

† A platform administrator holds `SetPlatformAdmin()` privileges (`RequestIdentity.cs:116`) and no tenant. Whether that satisfies `privileges.Has("tenant-settings.manage")` was **not read in this pass** — `IUserPrivileges.SetPlatformAdmin`/`Has` is outside this module's scope. Batch C must resolve it against `src/NT.QAMS.Infrastructure/Authorization/` before asserting a code. Until then the cell is `[RNV]`.
‡ Even if a laboratory granted `tenant-settings.manage` to its External Auditor role, `[RequireInternalActor]` refuses the **command** at `AuthorizationBehavior.cs:75` — the two layers disagree by design, and that is the defence-in-depth case worth writing.

### 4.6 Tenant-resolution decision table

| # | Request condition | `context.User` | `tenant_id` claim | `CurrentTenant` after middleware | GUC on next open | Observable |
|---|---|---|---|---|---|---|
| 1 | Anonymous | unauthenticated | absent | Unresolved | nil / `off` | Only `[AllowAnonymous]` endpoints reachable; any tenant-scoped read returns empty |
| 2 | Tenant user, valid JWT | authenticated | valid GUID | Scoped | that tenant / `off` | Normal operation |
| 3 | Platform admin, valid JWT | authenticated | **absent** | Unresolved | nil / `off` | Platform surface only; a tenant-scoped read returns empty and a tenant-scoped write throws `TENANT-000` |
| 4 | JWT with malformed `tenant_id` (not a GUID) | authenticated | unparseable | Unresolved (`Guid.TryParse` false, `RequestIdentity.cs:58`) | nil / `off` | Fails closed — **no error is raised**; the request proceeds and simply sees nothing. Worth an explicit case |
| 5 | Header `X-Tenant-Id: <other>` present | authenticated | own tenant | Scoped to **own** tenant | own tenant | Header is never read anywhere → inert |
| 6 | Query `?tenantId=<other>` | authenticated | own tenant | Scoped to own | own tenant | Inert |
| 7 | Host `other-lab.qms.local` | authenticated | own tenant | Scoped to own | own tenant | Inert |
| 8 | Valid JWT for a **suspended** tenant, issued before suspension | authenticated | valid | Scoped | that tenant | **Request succeeds** — GAP-TENANT-001 |
| 9 | Valid JWT, account deactivated | authenticated | valid | Scoped | that tenant | `401 AUTH-006` from `ActiveSessionMiddleware:100` |
| 10 | Valid JWT, DB role changed since issue | authenticated | valid | Scoped | that tenant | `401 AUTH-007` from `:107` |

---

## 6. UAT scenarios (Gherkin)

Business-readable, for a quality manager / system owner to sign. Each is `Not Run`.

### TC-TENANT-UAT-001 — A new laboratory is set up complete and usable on day one  [IV]
```gherkin
Given I am signed in as the platform administrator
When I create a new laboratory called "Amman Central Laboratory" with the address "amman-central-lab"
  And I give its first administrator the email "admin@amman.test"
Then the laboratory is created together with its administrator in a single step
  And the laboratory starts with the five standard roles: Tenant Administrator, Quality Manager,
      Department Head, Analyst and External Auditor
  And its administrator holds the Tenant Administrator role, so privileges are governable
      from the very first sign-in
  And every drop-down list in the system already has usable starter values
  And if any part of that set-up fails, none of it is saved
```
*Traces:* URS-008 · `ProvisionTenant.cs:41-76` · `SystemRoleCatalog.cs:52-80` · `DefaultLovCatalog`.

### TC-TENANT-UAT-002 — A laboratory is greeted by name, and no more than that  [IV]
```gherkin
Given a laboratory exists at the sign-in address "/t/amman-central-lab"
When someone who has not signed in opens that address
Then the sign-in page greets them with "Amman Central Laboratory"
  And nothing else about the laboratory is disclosed — no identifier, no status, no settings

When someone opens "/t/a-lab-that-does-not-exist"
  Or opens "/t/Not_A_Valid_Slug"
  Or opens the address of a laboratory that has been suspended
Then all three answer identically, so no one can probe which laboratories exist
```
*Traces:* URS-008 · `GetWorkspace.cs:27-46` · `WorkspaceLookupTests.cs:41-103`.

### TC-TENANT-UAT-003 — One laboratory can never see another's records  [IV]
```gherkin
Given laboratory "Alpha" and laboratory "Beta" both use the system
  And each has raised its own nonconformances, documents and equipment records
When a quality manager of Alpha searches, exports or reports on any module
Then only Alpha's records are returned, in every module without exception
  And this holds even if the application's own filtering were removed,
      because the database itself refuses to return Beta's rows
  And an attempt to file a record against Beta is rejected by the database, not merely by the screen
```
*Traces:* URS-008, URS-100 · `RlsTenantIsolationTests.cs:19-75` · §4.1.

### TC-TENANT-UAT-004 — A record's details belong to the laboratory that owns the parent  [IV]
```gherkin
Given laboratory Alpha has a nonconformance with corrective actions and a root-cause record
When a Beta user's session is used to read the corrective-action or root-cause tables directly
Then no Alpha row is returned
  And it is impossible to file a corrective action for an Alpha nonconformance under Beta's name —
      the database refuses the record outright rather than storing it and detecting it later
```
*Traces:* URS-101 · `OwnedChildTenancyTests.cs:31-126` · `Hardening4_ChildTenancy.cs:353-435`.

### TC-TENANT-UAT-005 — With no laboratory identified, the system shows nothing  [IV]
```gherkin
Given a request reaches the system without a laboratory identified
  Or with an unreadable laboratory identifier
When any list, report or export is requested
Then it comes back empty rather than showing another laboratory's data
  And any attempt to save a record is refused
  And no error discloses which laboratories exist
```
*Traces:* URS-008 · `TenantConnectionInterceptor.cs:21,55` · `TenantStampInterceptor.cs:51-54` · §4.2 rows 1–3.

### TC-TENANT-UAT-006 — Each laboratory decides its own multi-factor policy  [IV]
```gherkin
Given I am the administrator of laboratory Alpha
When I turn on "require multi-factor authentication for privileged users"
Then the setting applies to Alpha only, and Beta is untouched
  And a quality manager of Alpha cannot change it, because it is an administrator setting
  And an external auditor cannot change it under any circumstances
  And the change is recorded in Alpha's own audit trail with who made it and when
```
*Traces:* URS-004 · `TenantMfaPolicy.cs:26-36` · `TenantSettingsController.cs:18` · `SystemRoleCatalog.cs:111`.

### TC-TENANT-UAT-007 — Only the platform operator manages laboratories  [IV]
```gherkin
Given I am signed in as the administrator, quality manager, department head, analyst
      or external auditor of a laboratory
When I attempt to create a new laboratory or list the laboratories on the platform
Then I am refused, and the refusal explains that I lack permission rather than
      revealing whether such laboratories exist
```
*Traces:* URS-005, URS-008 · `TenantsController.cs:12` · `ProvisionTenant.cs:17` · §4.5.

### TC-TENANT-UAT-008 — The isolation guarantee survives a restore and a deployment  [IV]
```gherkin
Given a database has been restored from backup, or a new environment has been deployed
When the restore gate runs
Then it confirms that every table holding laboratory data still has row-level security
      enabled and forced, with its isolation rule in place
  And the application refuses to start in Production if it would connect using a database
      account powerful enough to ignore those rules
  And a deployment that would leave one table unprotected fails the build rather than shipping
```
*Traces:* URS-100 · `deploy/BACKUP-RESTORE-DR.md` §5 · `DatabaseRoleGuard.cs:64-79` · `OwnedChildTenancyTests.cs:128-155`.

---

## 7. Exploratory charters

Time-boxed, chartered exploration. Each states an **explicit stop condition** so it cannot become open-ended browsing.

### TC-TENANT-EXPL-001 — Hunt for a request path that reaches an elevated DI scope
**Explore** the eight `Elevate()` sites (§4.3) and every service that shares a scope with them
**With** the DI lifetime graph, `IServiceScopeFactory.CreateScope()` usages, and any MediatR handler or `IHostedService` that resolves `AppDbContext` from the same scope
**To discover** whether user input can reach — directly or transitively — a scope that is already elevated, or whether any handler could be made to run inside one (e.g. a nested `sender.Send` from inside `ProvisionTenantHandler`).
**Box:** 90 min. **Stop when** every scope containing an `Elevate()` has been traced to its creator and each creator classified as request-triggered or host-triggered.
**Escalate immediately if** any elevated scope is reachable from a route other than `POST /api/tenants`.

### TC-TENANT-EXPL-002 — Attack the GUC lifetime across connection pooling and transactions
**Explore** `TenantConnectionInterceptor` under real Npgsql pooling
**With** `set_config(..., false)` (session scope) at `TenantConnectionInterceptor.cs:54`, explicit transactions, `ExecuteSqlRaw`, `DbContext` reuse across scopes, and connection resets
**To discover** (a) whether a pooled physical connection can ever serve tenant B while still carrying tenant A's GUC; (b) what happens when `Set()` or `Elevate()` is called **after** a connection is already open — the failure shape the `Login_shaped_write…` pin exists to catch (`SecurityEventRlsTests.cs:147-151`); (c) whether an explicitly begun transaction can outlive a tenant change.
**Box:** 120 min. **Stop when** each of the three questions has a reproduced observation or a documented reason it cannot occur.
**Escalate immediately if** any sequence produces a cross-tenant read.

### TC-TENANT-EXPL-003 — Probe the `saas.tenant` control plane, which has no RLS
**Explore** every read and write of `db.Tenants` in `src/`
**With** the fact that `saas.tenant` carries **no** RLS and **no** EF query filter (it is not `ITenantScoped`), so its only protection is that callers key by `ICurrentTenant.TenantId`
**To discover** whether any handler accepts a caller-supplied tenant id, slug or index into `db.Tenants`; whether `GetTenantsQuery` can be reached by a non-platform actor through the versioned route, a batch/bulk endpoint, an export, or a report; and whether the `TenantDto.Status` field leaks anywhere pre-auth.
**Box:** 90 min. **Stop when** every `db.Tenants` occurrence has been classified as (i) keyed by the request tenant, (ii) keyed by a slug from an anonymous-safe path, or (iii) platform-gated.
**Escalate immediately if** a tenant id or slug arrives from the request body, route or query on any authenticated path.

### TC-TENANT-EXPL-004 — Falsify the B9 compensating controls for `user_account`
**Explore** the `UserAccountTenantBoundTests` matcher against real query shapes
**With** the seven `Bounds` regexes (`UserAccountTenantBoundTests.cs:59-68`) and the `tenant-unbounded:` exemption mechanism
**To discover** query shapes that are genuinely unbounded yet **pass** the matcher — e.g. `db.Users.Where(u => u.Id == someUnvalidatedId)` where the id came from the request body rather than from `sub`; a projection reached through a navigation property that never mentions `db.Users`; a raw-SQL read of `qams.user_account`; or a bound satisfied by a comment rather than by code.
**Box:** 120 min. **Stop when** at least three candidate evasions have been written and run against the guard.
**Escalate immediately if** any evasion both passes the guard and returns another tenant's user rows — that reopens B9 under revisit trigger 3.

### TC-TENANT-EXPL-005 — Stress the isolation error surface a caller actually sees
**Explore** what HTTP the client receives when isolation refuses something
**With** `DomainExceptionHandler.cs:26-82`, PostgreSQL SqlStates `42501` (RLS) and `23503` (composite FK), and the tenant-composite FKs from `Hardening4`
**To discover** how many distinct 500s the isolation layer can produce; whether any of them leaks a table name, tenant id, constraint name or SQL fragment into the response body, the `problem+json` `detail`, the correlation-id log line, or an OpenTelemetry span attribute.
**Box:** 90 min. **Stop when** every reachable isolation refusal has been triggered once and its full response body plus emitted log line captured.
**Escalate immediately if** any response or log discloses another tenant's identifier.

---

## 8. Gap Register (this module)

### GAP-TENANT-001 — Suspending or terminating a tenant does not end its users' live sessions

| Field | Value |
|---|---|
| **Source reference** | `src/NT.QAMS.Application/IdentityAccess/Commands/Login.cs:60-63` (the only status check) vs `src/NT.QAMS.WebApi/Middleware/RequestIdentity.cs:80-131` (`ActiveSessionMiddleware` — reads `user_account` only, never `saas.tenant`) |
| **Description** | Tenant status is enforced at sign-in (`AUTH-002`) and in the anonymous workspace lookup. Nothing enforces it on an already-authenticated request. `ActiveSessionMiddleware` re-checks the *account* on every request — precisely the mechanism that would be needed — but does not read the tenant. A user holding a valid access token (15 min default) therefore continues to work normally after their laboratory is suspended or terminated, and can continue to refresh, since refresh is likewise not shown to check tenant status `[RNV]`. |
| **Impact** | A suspension imposed for non-payment, a regulatory hold, or a security incident is not effective until every outstanding token expires. For a termination this is worse: records may be created in a laboratory that the platform considers closed. |
| **Testing limitation** | Cannot be tested end-to-end at all today, because there is **no way to suspend a tenant** (GAP-TENANT-002). The condition can only be reached by an out-of-band `UPDATE saas.tenant SET status='Suspended'`, which is not a supported operation and makes any resulting case `[GD]` on both gaps. |
| **Recommended clarification** | State the required revocation latency for a suspension and for a termination: immediate (next request), within the access-token lifetime, or at next sign-in. State whether refresh must also be refused, and whether a suspended tenant's users see a distinct message or a generic 401. |
| **Suggested acceptance criteria** | (1) `ActiveSessionMiddleware` (or an equivalent gate) refuses any request whose tenant is not `Active`, with a distinct documented code (e.g. `AUTH-008`) at HTTP 401. (2) `POST /api/auth/refresh` refuses likewise. (3) A functional test suspends a tenant and proves the **same** access token that worked one request earlier is refused on the next. (4) Platform-administrator requests, which carry no tenant, are unaffected. |
| **Severity** | **High** |
| **Responsible role** | Product Owner (policy) + Backend Lead (implementation) |

### GAP-TENANT-002 — Tenant suspension, reactivation and termination are unreachable

| Field | Value |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/Tenancy/Tenant.cs:58` (`Suspend`), `:76` (`Reactivate`), `:89` (`Terminate`); no matching command in `src/NT.QAMS.Application/Tenancy/Commands/`; no route in `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt`; no request record in `src/NT.QAMS.Contracts/Tenancy/TenancyContracts.cs` |
| **Description** | Three of the four lifecycle transitions declared in the aggregate's own doc comment (`Tenant.cs:8`) exist only as domain methods. There is no command, no handler, no endpoint and no UI. Their guards (`TENANT-010`, `TENANT-011`, `TENANT-012`, `TENANT-013`) and their events (`TenantSuspended`, `TenantReactivated`, `TenantTerminated`) are exercised only by `TenantTests`. |
| **Impact** | The platform cannot suspend a laboratory for non-payment, a regulatory hold or a security incident, nor offboard one, without direct database manipulation — which is itself unauditable and bypasses the domain guards. |
| **Testing limitation** | `TC-TENANT-STATE-002…020` can only be authored at **Unit** level. Any API or E2E case for these transitions is `[GD]` on this gap. |
| **Recommended clarification** | Confirm whether tenant lifecycle management is in scope for v1.x. If yes, specify: who may perform each transition; whether a reason is required for termination as it is for suspension; whether termination is reversible; what happens to the tenant's users, sessions, scheduled jobs and data-retention obligations. |
| **Suggested acceptance criteria** | (1) `SuspendTenantCommand(reason)`, `ReactivateTenantCommand`, `TerminateTenantCommand` exist, each `[RequireRole(UserRole.PlatformAdmin)]`. (2) `POST /api/tenants/{id}/suspend|reactivate|terminate` on `TenantsController`, added to `ApiSurface.approved.txt` in the same commit. (3) Each surfaces its guard code at HTTP 409 (`TENANT-010/012/013`) and `TENANT-011` at 422. (4) Each writes a hash-chained `audit_trail` entry and a `security_event`. (5) The 4×5 matrix in §3.1 is covered at API level. |
| **Severity** | **High** |
| **Responsible role** | Product Owner |

### GAP-TENANT-003 — Tenancy domain events have no consumers; the routing comment is not implemented

| Field | Value |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/Tenancy/TenantEvents.cs:5-8` (the routing promise) vs `src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:126-128` (the only handler in `src/`, for `TenantProvisioned`) |
| **Description** | The comment states *"TenantProvisioned → Identity seeds the tenant admin + canonical roles, Organization seeds default LOVs; TenantSuspended/Reactivated gate access."* Neither half is an event handler. Seeding is done inline inside `ProvisionTenantHandler` (`ProvisionTenant.cs:53-72`), which is arguably better (one transaction) but is not what the comment says. `TenantSuspended`, `TenantReactivated` and `TenantTerminated` have **no** subscriber, so they are written to the outbox and the ledger and then discarded. |
| **Impact** | A reader of the domain believes access gating happens on suspension. It does not (GAP-TENANT-001). The documentation actively misleads a reviewer or auditor about where a control lives. |
| **Testing limitation** | No case can assert an effect of `TenantSuspended`. Any such case would be `[GD]`. |
| **Recommended clarification** | Decide whether suspension gating is event-driven (a handler that revokes sessions) or synchronous (a middleware check). Then correct the comment to describe the design that exists. |
| **Suggested acceptance criteria** | (1) The comment at `TenantEvents.cs:5-8` describes the implemented routing exactly. (2) Either a handler for `TenantSuspended`/`TenantReactivated` exists with a proving test, or the comment states that these events are recorded for audit only. (3) An architecture test asserts that every domain event either has a handler or is listed in a documented "audit-only events" set. |
| **Severity** | **Medium** |
| **Responsible role** | Backend Lead |

### GAP-TENANT-004 — `PlatformControllers.cs` contains no platform controllers

| Field | Value |
|---|---|
| **Source reference** | `src/NT.QAMS.WebApi/Controllers/PlatformControllers.cs:12-129` — `BranchesController` (`:15`), `DepartmentsController` (`:38`), `TestCatalogController` (`:62`), `LovsController` (`:78`), `NotificationsController` (`:95`); every one is `[Authorize]` + `[RequirePermission]`, none is platform-gated. Contrast `TenantsController.cs:12` |
| **Description** | The file name asserts a platform control plane. The five controllers it holds are ordinary tenant-scoped organisation and notification surfaces. The real control plane is `TenantsController` in a separate file. |
| **Impact** | A reviewer auditing "the platform surface" reads the wrong file and either over-scopes (treating tenant endpoints as platform ones) or under-scopes (missing `TenantsController`). For a regulated audit of privileged access, that is a material navigation error. Additionally, four `GET` actions in this file carry **no** `[RequirePermission]` at all (`:18,41,65,81,97`) — deliberate or not, that fact is easier to miss under a misleading file name. |
| **Testing limitation** | None on behaviour. It is a documentation/structure defect that raises the risk of a coverage hole in the authorization matrix. |
| **Recommended clarification** | Confirm whether the ungated `GET` actions (`branches`, `departments`, `test-catalog`, `lovs`, `notifications/mine`) are intentionally readable by any authenticated tenant user. |
| **Suggested acceptance criteria** | (1) The file is renamed to reflect its contents (e.g. `OrganizationControllers.cs`) or its controllers are split by module. (2) Each ungated `GET` either receives a `[RequirePermission(..., View)]` or is listed in a documented "any authenticated tenant user" set with a rationale. (3) `RoleEndpointMatrixTests` covers all five controllers. |
| **Severity** | **Low** (naming) / **Medium** (the ungated reads it obscures) |
| **Responsible role** | Backend Lead |

### GAP-TENANT-005 — `POST /api/tenants` returns a `Location` that is not a resource URI

| Field | Value |
|---|---|
| **Source reference** | `src/NT.QAMS.WebApi/Controllers/TenantsController.cs:28` — `CreatedAtAction(nameof(GetAll), new { id = tenantId }, new { id = tenantId })`; route template `api/tenants` at `:11` with `GetAll` at `:31-34` taking no `id` |
| **Description** | `CreatedAtAction` names an action whose template contains no `{id}` segment, so ASP.NET Core appends the route value as a query string: `Location: /api/tenants?id=<guid>`. There is no `GET /api/tenants/{id}` in the approved surface (`ApiSurface.approved.txt:124,232`), so the header cannot be made correct without adding one. |
| **Impact** | A client following `Location` per RFC 9110 receives the full tenant list, not the created tenant. Minor for a platform-only endpoint; it becomes a real defect the moment the control plane is automated. |
| **Testing limitation** | The 201 case can be authored, but no case can assert a canonical `Location` — only the current query-string form. Asserting the current form pins a defect. |
| **Recommended clarification** | Confirm whether a `GET /api/tenants/{id}` is wanted, or whether `Created(...)` with no location, or `Ok(...)`, is the intended contract. |
| **Suggested acceptance criteria** | (1) Either `GET /api/tenants/{id:guid}` exists, is platform-gated, and `CreatedAtAction` targets it; or the action returns a form whose `Location` is absent by design. (2) `ApiSurface.approved.txt` is updated in the same commit. (3) A functional test asserts the exact `Location` value. |
| **Severity** | **Low** |
| **Responsible role** | Backend Lead |

### GAP-TENANT-006 — An RLS or tenant-FK refusal reaches the caller as an untyped HTTP 500

| Field | Value |
|---|---|
| **Source reference** | `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:26-82` — the switch handles `DbUpdateConcurrencyException`, `ValidationException`, `InvalidStateTransitionException` and `DomainException`, then `_ => null` at `:82`. A plain `DbUpdateException` wrapping `PostgresException` matches nothing. SqlState `42501` is asserted at `tests/NT.QAMS.IntegrationTests/SecurityEventRlsTests.cs:85`; SqlState `23503` at `tests/NT.QAMS.IntegrationTests/OwnedChildTenancyTests.cs:24,114` |
| **Description** | When RLS `WITH CHECK` refuses a cross-tenant write (`42501`), or a tenant-composite FK refuses a child whose tenant differs from its parent's (`23503`), the resulting `DbUpdateException` is unhandled. The response is a generic 500 with no `code`, no `problem+json` semantics from this handler, and no distinction from an infrastructure fault. |
| **Impact** | Three consequences. (a) The SPA cannot distinguish "you attempted a cross-tenant operation" from "the database is down". (b) A 500 is an alertable operational event, so a security-relevant refusal is buried in noise or, worse, pages the on-call for what is actually the system working correctly. (c) An unhandled exception risks including a table name, constraint name or SQL fragment in a diagnostic body or log — an information-disclosure path (see TC-TENANT-EXPL-005). |
| **Testing limitation** | An API-level case can only assert "500". No case can assert a typed code, so the expected-API cell would have to record a defective behaviour as expected. Cases asserting the correct behaviour are `[GD]` on this gap. |
| **Recommended clarification** | Decide the intended status for each: is a cross-tenant write attempt a 403 (authorization), a 422 (business rule), or deliberately opaque? Confirm no PostgreSQL detail may reach the client. |
| **Suggested acceptance criteria** | (1) `DomainExceptionHandler` maps `PostgresException` SqlState `42501` to a documented code at a documented status, and `23503` likewise. (2) The response body contains no table, column, constraint or tenant identifier. (3) A real-PostgreSQL functional test triggers each and asserts status, code and the absence of schema detail. (4) The mapped statuses are excluded from the 5xx alert rule in `deploy/OBSERVABILITY.md`. |
| **Severity** | **Medium** |
| **Responsible role** | Backend Lead + Security |

### GAP-TENANT-007 — `tenant-settings.view` exists but gates nothing; reading requires `manage`

| Field | Value |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:106` (module key), `:125-126` (`ConfigurationModule = [View, Manage]`), `:184` (registration); `src/NT.QAMS.WebApi/Controllers/TenantSettingsController.cs:18` (class-level `Manage`) applying to the `GET` at `:22`. Repository-wide search for `PermissionCatalog.TenantSettings` returns three hits, none requiring `View` |
| **Description** | The key `tenant-settings.view` is generated into `PermissionCatalog.AllKeys`, is seedable onto a role, and is displayed in the privilege-matrix UI — but no endpoint or command requires it. Granting it confers nothing; reading the MFA policy requires `tenant-settings.manage`. |
| **Impact** | An administrator configuring least privilege grants `tenant-settings.view` expecting read-only visibility and finds the screen still refused. Conversely, granting read access forces granting write access. This is a privilege-granularity failure in an Administration-group module, and it undermines the audit statement that privileges are meaningful. |
| **Testing limitation** | A positive case for `tenant-settings.view` cannot be written — there is nothing to assert. A negative case ("holder of `view` alone is refused the GET with 403 `AUTHZ-403`") **can** be written and pins the current behaviour, but pins a defect. |
| **Recommended clarification** | Should `GET /api/tenant-settings/mfa-policy` require `view` (with `manage` implying `view`), or should `tenant-settings.view` be removed from the catalogue? Note the same question applies to every `ConfigurationModule` module. |
| **Suggested acceptance criteria** | (1) Either the `GET` is annotated `[RequirePermission(TenantSettings, View)]` at the action and `Manage` moves to the `PUT`; or `tenant-settings` declares `[Manage]` only. (2) An architecture test asserts that every key in `PermissionCatalog.AllKeys` is required by at least one `[RequirePermission]` or `[RequirePermissionPolicy]`, so no unreachable key can be added. (3) `SystemRoleCatalogTests` is updated for the resulting key set. |
| **Severity** | **Medium** |
| **Responsible role** | Product Owner (policy) + Backend Lead |

### GAP-TENANT-008 — `deploy/harden-runtime-role.sql` cannot execute: it grants on a schema that does not exist

| Field | Value |
|---|---|
| **Source reference** | `deploy/harden-runtime-role.sql:22` (`\set ON_ERROR_STOP on`), `:40` (`GRANT USAGE ON SCHEMA qams, audit, ref, read, saas TO qams_app;`), `:48`, `:55`, `:71`, `:76-77`, `:80`. Measured schema list on dev `ntqams`: `audit, public, qams, read, saas` — **no `ref`**. No migration ever calls `EnsureSchema("ref")`; only `qams` (`20260721211309_InitialFoundation.cs:14-15`), `saas` (`:17-18`), `audit` (`20260721232300_ComplianceAndAuth.cs:14-15`) and `read` (`20260724235242_ReportingKpiSnapshots.cs:14-15`) are created |
| **Description** | The production least-privilege hardening script — the control that makes FORCE RLS *enforceable*, because a table owner or `BYPASSRLS` role ignores it — references a non-existent schema in six statements. With `ON_ERROR_STOP on` set at line 22, `psql` aborts at line 40, before a single grant is applied. The script header claims idempotency and safe re-run. |
| **Impact** | **This is the load-bearing gap of the module.** Every isolation claim in this package — 90/90 FORCE RLS, fail-closed nil tenant, `WITH CHECK` refusal — is conditional on the runtime role being neither owner nor `SUPERUSER`/`BYPASSRLS`. `RealPostgresFixture:53-65` and `RuntimeRolePrivilegeTests` acknowledge this by refusing to run under such a role. `SCHEMA-HARDENING-REPORT.md` §6 already flags "dev is owner-role, so `qams_app` holds grants it will not hold in production" and defers the control to this script. If the script cannot run, the deferral has no landing place, and `DatabaseRoleGuard.EnsureLeastPrivilegeAsync` will simply refuse the Production boot with remediation that does not work. |
| **Testing limitation** | The role-split installation cannot be qualified at all until this is fixed. Every case that depends on production-grade least privilege — `TC-TENANT-INT-*` for the role guard, `TC-TENANT-SEC-*` for owner-bypass, `TC-TENANT-DR-*` for the restore gate — is `[GD]` on this gap when executed in a role-split environment. In dev (owner role) they skip by design, so **a green suite here proves nothing about production**. |
| **Recommended clarification** | Was a `ref` schema planned and dropped, or is this a copy-paste artefact? `DatabaseRoleGuard.ApplicationSchemas` (`src/NT.QAMS.Infrastructure/Security/DatabaseRoleGuard.cs:18`) also lists `ref` — harmless in a `pg_tables` query, but it confirms the name is believed to exist in two places. |
| **Suggested acceptance criteria** | (1) The script executes to completion with `ON_ERROR_STOP on` against a freshly migrated database, either by removing `ref` from all six statements or by having a migration create it. (2) A CI job runs the script against a migrated throwaway database and fails the build on any error. (3) After the script, `RuntimeRolePrivilegeTests.Rls_suite_runs_as_a_least_privilege_role` passes with `Boot_as_owner_is_rejected` finding **no** violation to reject. (4) `DatabaseRoleGuard.ApplicationSchemas` matches the schemas that exist. (5) An OQ record captures execution on a role-split installation. |
| **Severity** | **Critical** |
| **Responsible role** | DevOps / Infrastructure Lead + Validation Lead (qualification impact) |

### GAP-TENANT-009 — `TenantStatus.Provisioning` is unreachable dead state

| Field | Value |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/Tenancy/TenantStatus.cs:9` (declared, `= 0`) vs `src/NT.QAMS.Domain/Tenancy/Tenant.cs:27` (the private constructor assigns `Active`). Repository-wide search for `TenantStatus.Provisioning` in `src/` and `tests/`: **zero** hits. `ck_tenant_status_domain` on `saas.tenant` accepts it. Live data: `Active|23`, no other value |
| **Description** | The first value of the lifecycle enum, and the first state named in the aggregate's own doc comment (`Tenant.cs:8`), can never occur. Because it is `= 0` it is also the CLR default, so a `Tenant` materialised without a status would silently read as `Provisioning`. |
| **Impact** | The documented lifecycle does not match the implemented one, which matters for a GAMP 5 design-vs-build review. A future asynchronous provisioning saga would need this state and would find the enum already claiming it while nothing sets it. |
| **Testing limitation** | Row 2 of the §3.1 matrix cannot be reached by any supported operation. `TC-TENANT-STATE-*` cases for it must be `[GD]` or constructed by reflection, which tests nothing real. |
| **Recommended clarification** | Is provisioning intended to become asynchronous (justifying the state), or is provisioning atomic forever (in which case the value should be removed and the doc comment corrected)? |
| **Suggested acceptance criteria** | Either (a) `Provisioning` is removed from the enum, the `CHECK` constraint and the doc comment, in one migration plus one code change; or (b) `Tenant.Provision` assigns `Provisioning`, a documented transition to `Active` exists, and `Login`/`GetWorkspace`/`KpiSnapshotService` are re-verified against the new initial state. In either case a domain unit test asserts the exhaustive set of reachable states. |
| **Severity** | **Low** |
| **Responsible role** | Backend Lead |

### GAP-TENANT-010 — Two authorization mechanisms guard one module

| Field | Value |
|---|---|
| **Source reference** | `src/NT.QAMS.WebApi/Controllers/TenantsController.cs:12` — `[Authorize(Roles = Roles.PlatformAdmin)]`, the **only** remaining `[Authorize(Roles=…)]` in `src/`; vs `src/NT.QAMS.WebApi/Controllers/TenantSettingsController.cs:18` — `[RequirePermission(PermissionCatalog.TenantSettings, PermissionAction.Manage)]`, one of 144 such gates |
| **Description** | v1.51.0 converted endpoint gating from role strings to permission keys, leaving exactly one role-string gate: the platform surface. That is a documented and defensible choice (a platform administrator is not a tenant member and holds no tenant role — `RequestIdentity.cs:111-117`). But it means the tenancy module is guarded two different ways, and the residual role gate depends on the JWT `role` claim string matching `UserRole.PlatformAdmin.ToString()` exactly. |
| **Impact** | (a) A reviewer verifying "all endpoint authorization is permission-based" finds one exception and must understand why. (b) The role-string path has a different failure mode: a framework 403 via `ProblemAuthorizationResultHandler` rather than the `[RequirePermission]` filter, so the two paths must be separately proven to emit the same `AUTHZ-403` shape. (c) The claim-string coupling means a rename of the enum member silently changes an authorization boundary — `Roles.cs:6-8` says a rename would be a compile error there, but the **JWT already issued** carries the old string. |
| **Testing limitation** | The 403 contract must be asserted twice, once per mechanism. A single matrix test over `[RequirePermission]` sites will not cover `TenantsController`. |
| **Recommended clarification** | Confirm the platform surface stays role-gated permanently, or state the conditions under which it would move to a platform permission catalogue. |
| **Suggested acceptance criteria** | (1) An architecture test asserts that `[Authorize(Roles=…)]` appears **at most once** in `src/` and only on the platform surface, so a re-introduction fails CI. (2) A functional test proves the role-gated 403 and a permission-gated 403 produce identical `problem+json` shape and the same `AUTHZ-403` code. (3) The exception is recorded in the authorization design document with its rationale. |
| **Severity** | **Low** |
| **Responsible role** | Security Architect |

### GAP-TENANT-011 — The scheduled compliance sweep processes suspended and terminated tenants

| Field | Value |
|---|---|
| **Source reference** | `src/NT.QAMS.Infrastructure/Jobs/ScheduledSweepService.cs:64` (`Elevate()`) and `:86-152` (eight `.IgnoreQueryFilters()` reads, none referencing `db.Tenants` or `TenantStatus`). Contrast `src/NT.QAMS.Infrastructure/Jobs/KpiSnapshotService.cs:90` — `.Where(t => t.Status == TenantStatus.Active)` |
| **Description** | Two background services run elevated across all tenants. The KPI snapshot filters to `Active` tenants; the compliance sweep does not filter at all. It therefore continues to mark equipment calibration-due, lock out equipment on grace exhaustion, expire competencies and test authorisations, suspend suppliers, expire reference standards and raise document-review-due flags — and to emit the resulting notifications — for laboratories that are suspended or terminated. |
| **Impact** | State transitions and notifications are generated for a laboratory the platform considers closed, producing audit-trail entries and emails after suspension. For a termination this may conflict with data-retention or contract-closure obligations. It also wastes work proportional to the number of dead tenants. |
| **Testing limitation** | Unreachable without the ability to suspend a tenant (GAP-TENANT-002), so any case is `[GD]` on both. |
| **Recommended clarification** | Should background processing stop at `Suspended`, at `Terminated`, or neither? Should in-flight escalation timers be paused or cancelled on suspension? |
| **Suggested acceptance criteria** | (1) The intended behaviour is stated per status in the operations documentation. (2) `ScheduledSweepService` applies the agreed filter, consistently with `KpiSnapshotService`. (3) An integration test with one `Active` and one `Suspended` tenant proves the sweep touches only the intended one. (4) An architecture or review checklist item requires every new cross-tenant background job to declare its tenant-status filter explicitly. |
| **Severity** | **Medium** |
| **Responsible role** | Backend Lead + Product Owner |

### GAP-TENANT-012 — `TENANT-004` is used both as a domain error code and as an audit-finding identifier

| Field | Value |
|---|---|
| **Source reference** | Domain code: `src/NT.QAMS.Domain/Tenancy/Tenant.cs:50` — `throw new DomainException("TENANT-004", "Tenant name must not exceed 200 characters.")`. Finding identifier: `src/NT.QAMS.Infrastructure/Security/DatabaseRoleGuard.cs:6` — *"Deployment safety gate (TENANT-004)"*; also `tests/NT.QAMS.IntegrationTests/RuntimeRolePrivilegeTests.cs:9` and `tests/NT.QAMS.IntegrationTests/RealPostgresFixture.cs:53` |
| **Description** | The same token identifies two unrelated things: a 422 validation failure on tenant name length, and the Phase-0 finding "the runtime database role must not be over-privileged". Both are written in code, one as a thrown code and three as documentation comments. |
| **Impact** | Traceability corruption. A traceability matrix, a defect search, or a log query for `TENANT-004` returns two unrelated populations. In a regulated package where every code must map to exactly one requirement or control, that is a review finding in its own right. |
| **Testing limitation** | A case asserting `TENANT-004` must state which meaning it intends, in prose, because the code string alone is ambiguous. |
| **Recommended clarification** | Which namespace owns `TENANT-nnn` — domain error codes, or Phase-0 findings? |
| **Suggested acceptance criteria** | (1) One of the two is renamed; the natural choice is the finding, to something like `OPS-TENANT-004` or `F-TENANT-ROLE`, since the domain code is thrown at runtime and appears in client contracts. (2) All four source references are updated. (3) A traceability check confirms every `TENANT-nnn` token in `src/` and `docs/` resolves to exactly one meaning. |
| **Severity** | **Low** |
| **Responsible role** | Validation Lead |

### GAP-TENANT-013 — `Tenant.UpdateSettings` is unreachable and throws an unmapped exception

| Field | Value |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/Tenancy/Tenant.cs:100-104` — `ArgumentNullException.ThrowIfNull(settings)` then `Settings = settings`. Repository-wide search for `.UpdateSettings(` in `src/`: **zero** call sites. Exception mapping: `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:81` (`_ => null` → unhandled → 500) |
| **Description** | The only public way to replace the whole `TenantSettings` value object is dead code. The one settings mutation that *is* reachable, `SetPrivilegedMfaPolicy` (`Tenant.cs:107-108`), bypasses it with a `with` expression and performs no validation at all — it accepts any `bool`, raises no domain event, and has no guard on tenant status (a `Terminated` tenant's MFA policy can still be changed, per §3.1). `TenantSettings` declares six properties (`TenantSettings.cs:10-20`); **five of the six** — `PasswordExpiryDays` (90), `CalibrationReminderDays` (30), `SopExpiryReminderMonths` (3), `DefaultLanguage` (`"en"`), `TimeZone` (`"UTC"`) — have **no** mutation path anywhere and are frozen at their declared defaults (`TenantSettings.cs:10-14`). Only `RequireMfaForPrivilegedRoles` (`:20`) is changeable. |
| **Impact** | (a) Dead code in a regulated domain aggregate, contrary to the standing rule "no dead code" (`CLAUDE.md` §2.3). (b) If it were called with `null`, the caller would receive a bare 500 rather than a typed domain error. (c) Five documented per-tenant settings are advertised in the domain and persisted as columns but are not configurable by anyone — a functional gap that a laboratory will discover as "I cannot change my password-expiry period". |
| **Testing limitation** | No case can exercise `UpdateSettings`. Cases for the five frozen settings are `[GD]`. |
| **Recommended clarification** | Are `PasswordExpiryDays`, `CalibrationReminderDays`, `SopExpiryReminderMonths`, `DefaultLanguage` and `TimeZone` intended to be tenant-configurable in v1.x? If yes, each needs a command, an endpoint, validation bounds and an audit entry. |
| **Suggested acceptance criteria** | (1) `UpdateSettings` is either removed, or given a domain code (e.g. `TENANT-014`) and a caller. (2) Each intended-configurable setting has a command with validated bounds, a permission-gated endpoint, an audit-trail entry, and a case. (3) `SetPrivilegedMfaPolicy` states whether a non-`Active` tenant may change it, and enforces the answer. |
| **Severity** | **Medium** |
| **Responsible role** | Product Owner (scope) + Backend Lead (dead code) |

### GAP-TENANT-014 — No automated control enumerates or caps the RLS-bypass call sites

| Field | Value |
|---|---|
| **Source reference** | Eight `Elevate()` call sites in `src/` (§4.3), against a conventions document that lists five (`docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md:72`, corrected in §0.1). `tests/NT.QAMS.Architecture.Tests/` contains `CommandPolicyTests`, `LayerRulesTests`, `ModuleBoundaryTests` and `UserAccountTenantBoundTests` — **none** of which mentions `Elevate` |
| **Description** | `Elevate()` is the single mechanism that turns off the database's tenant fence for a unit of work. It is `public` on `ICurrentTenantSetter`, which is registered scoped and therefore injectable into **any** handler, controller or service. Nothing prevents a new command handler from taking `ICurrentTenantSetter` and calling `Elevate()`; nothing lists the sites; nothing fails a build when a ninth appears. The parallel and far weaker risk — an unbounded `user_account` query — *does* have a build-time guard (`UserAccountTenantBoundTests`). |
| **Impact** | This is the highest-leverage single line of code in the isolation design and it is unguarded. One `Elevate()` on a request path handling end-user input silently converts every subsequent query in that request into a cross-tenant query — exactly the catastrophic-confidentiality scenario the FRA rates **HIGH** for URS-008 (`docs/validation/02-Functional-Risk-Assessment.md:51,88-92`). The `ICurrentTenant.cs:32-33` doc comment states the prohibition; a comment is not a control. |
| **Testing limitation** | The case authors cannot know the true elevation set from any single authoritative artefact — this file's §4.3 is a point-in-time measurement that will drift. Coverage of "all elevation paths" is unverifiable without an enumerating test. |
| **Recommended clarification** | Should `Elevate()` be restricted by construction (e.g. a separate `ICrossTenantScope` interface registered only for hosted services, or an `[AllowElevation]` marker with an architecture test), or governed by an allow-list test? |
| **Suggested acceptance criteria** | (1) An architecture test enumerates every `Elevate()` call site in `src/` and asserts it against an explicit allow-list, failing the build on any addition — the same shape and rigour as `UserAccountTenantBoundTests`, including its mutation test (inject a new call, prove the build fails with file and line). (2) The allow-list carries a one-line justification per entry. (3) A negative test proves that a MediatR command handler calling `Elevate()` fails the build unless allow-listed. (4) `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md:72` is regenerated from the allow-list so the two can never disagree. |
| **Severity** | **High** |
| **Responsible role** | Security Architect + Backend Lead |

---

*End of front matter for module `TENANT`. Detailed cases are authored separately into `12-module-tenancy-rls-cases-A.md`, `-B.md` and `-C.md` against the ID reservations at the top of this file.*
