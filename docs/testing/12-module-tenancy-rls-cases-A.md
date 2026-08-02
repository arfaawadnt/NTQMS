# TENANT — Detailed Test Cases, Batch A

This batch covers **the GUC mechanism itself and the explicit elevation path** — nothing else. In scope: `TenantConnectionInterceptor` stamping `app.current_tenant` and `app.bypass_rls` through `set_config` on every connection open (`src/NT.QAMS.Infrastructure/Persistence/Interceptors/TenantConnectionInterceptor.cs:53-56`); its fail-closed nil-UUID fallback; the guarantee that a pooled physical connection cannot serve one tenant while carrying another's GUC; the three-state `CurrentTenant` scoped holder (`src/NT.QAMS.Infrastructure/Services/RequestContext.cs:12-27`) with `Set` / `Clear` / `Elevate` and the absence of any `Demote`; and each of the **eight** `Elevate()` call sites across **six** components — `ProvisionTenantHandler`, `OutboxProcessor` (three sites), `ScheduledSweepService`, `KpiSnapshotService`, and the two `StartupSeeding` backfills — plus positive proof that an ordinary authenticated HTTP request cannot reach the one elevation that sits on a request path. **Deliberately left to sibling batches:** per-table RLS isolation inventories, `WITH CHECK` refusals per table, owned-child tenancy and composite FKs, the `audit.*` relaxed-write shape and `audit.security_event` positive isolation, cross-tenant attack and GUC-forgery cases, and the structural RLS-parity sweep (all **batch B**, `TC-TENANT-RLS-*`, `TC-TENANT-SEC-*`, `TC-TENANT-INT-018+`); the `Tenant` aggregate, `TenantSlug`, `TenantStatus` transitions, `/api/tenants` payload contracts and the workspace-lookup disclosure table (**batch B/C** per the front matter's reservation table); tenant-resolution middleware claim-source and spoofing cases, the EF global query filter's MC-DC, and observability attribution (**batch C**). IDs consumed here: `TC-TENANT-UNIT-001…013` and `TC-TENANT-INT-001…017`. Every case is `Not Run`.

**Risk IDs.** `docs/validation/02-Functional-Risk-Assessment.md` carries area-level rows keyed by URS, not `RSK-nnn` identifiers (the "Tenant isolation / URS-008" row at `:51` is rated **HIGH**). Per conventions §5 this batch therefore **mints** its own, and says so:

| Risk ID | Statement |
|---|---|
| `RSK-TENANT-001` | A query returns rows belonging to a tenant other than the caller's, because the connection carried the wrong `app.current_tenant`. |
| `RSK-TENANT-002` | A write lands under a tenant other than the caller's, because `app.bypass_rls` was `'on'` when it should not have been. |
| `RSK-TENANT-003` | A pooled physical connection serves request *n+1* while still carrying request *n*'s session GUCs. |
| `RSK-TENANT-004` | Elevation is reached from a path that handles end-user input, converting every subsequent query in that unit of work into a cross-tenant query. |
| `RSK-TENANT-005` | An unresolved tenant fails **open** (all rows) instead of closed (no rows). |
| `RSK-TENANT-006` | A ninth `Elevate()` call site is added and no control notices. |

---

## Part 1 — `CurrentTenant` state machine and interceptor command construction (unit level)

#### TC-TENANT-UNIT-001 — A freshly constructed `CurrentTenant` is Unresolved and not elevated  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-005 |
| **Level / Type / Technique** | Unit · Functional (positive) · State Transition — the initial state of the three-state scoped holder |
| **Priority / Severity / Automation** | High · High · Yes (xUnit fact) |
| **Role / Permission / Tenant** | n/a — no actor, the type is a DI-scoped POCO · n/a — no permission gate on a POCO · Unresolved |
| **Environment** | .NET 9 SDK user-local (`%LOCALAPPDATA%\Microsoft\dotnet`); xUnit in `tests/NT.QAMS.Application.UnitTests` (that project already references `NT.QAMS.Infrastructure`); **no database** |
| **Preconditions** | None. `CurrentTenant` has an implicit parameterless constructor and no field initialisers (`src/NT.QAMS.Infrastructure/Services/RequestContext.cs:12-17`). |
| **Test Data** | n/a — the case asserts default state, so there is no input datum. |
| **Steps** | 1. `var ct = new NT.QAMS.Infrastructure.Services.CurrentTenant();`. 2. Read `ct.TenantId`. 3. Read `ct.IsResolved`. 4. Read `ct.IsElevated`. |
| **Expected UI** | n/a — unit level, no UI is rendered. |
| **Expected API** | n/a — unit level, no HTTP call is made. |
| **Expected DB** | `ct.TenantId` is `null`; `ct.IsResolved` is `false` (`IsResolved => TenantId.HasValue`, `RequestContext.cs:15`); `ct.IsElevated` is `false` (`:16`). No database is touched. |
| **Expected Audit** | n/a — constructing a scoped holder writes no ledger row. |
| **Expected Notification** | n/a — no domain event is raised by `CurrentTenant`. |
| **Cleanup** | n/a — the instance is garbage-collected with the test. |
| **Evidence** | xUnit trx result · the three asserted property values captured in the assertion message |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the state every anonymous request and every platform-administrator request sits in, because `TenantResolutionMiddleware` only calls `Set` when the `tenant_id` claim parses as a `Guid` (`src/NT.QAMS.WebApi/Middleware/RequestIdentity.cs:57-61`). |

#### TC-TENANT-UNIT-002 — `Set(id)` moves Unresolved → Scoped and leaves elevation off  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-001 |
| **Level / Type / Technique** | Unit · Functional (positive) · State Transition — edge Unresolved →`Set`→ Scoped of the §3.3 matrix |
| **Priority / Severity / Automation** | Critical · Critical · Yes (xUnit fact) |
| **Role / Permission / Tenant** | n/a — POCO under test · n/a — no permission gate · `11111111-1111-1111-1111-111111111111` |
| **Environment** | .NET 9 SDK user-local; xUnit in `tests/NT.QAMS.Application.UnitTests`; no database |
| **Preconditions** | A `CurrentTenant` in its initial Unresolved state (TC-TENANT-UNIT-001). |
| **Test Data** | `tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111")` |
| **Steps** | 1. `var ct = new CurrentTenant();`. 2. `ct.Set(tenantId);`. 3. Read `ct.TenantId`, `ct.IsResolved`, `ct.IsElevated`. |
| **Expected UI** | n/a — unit level. |
| **Expected API** | n/a — unit level. |
| **Expected DB** | `ct.TenantId == Guid.Parse("11111111-1111-1111-1111-111111111111")`; `ct.IsResolved == true`; **`ct.IsElevated == false`** — `Set` assigns only `TenantId` (`RequestContext.cs:18`) and must not confer bypass. |
| **Expected Audit** | n/a — no ledger row. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | n/a — no persisted state. |
| **Evidence** | xUnit trx result · asserted property triple |
| **Result / Defect** | Not Run · — |
| **Notes** | The `IsElevated == false` assertion is the load-bearing one: if `Set` ever conferred elevation, every authenticated request would run with `app.bypass_rls='on'`. |

#### TC-TENANT-UNIT-003 — `Set` may be called twice with different ids and nothing objects  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 — no URS covers re-pointing; traced to source · RSK-TENANT-001 |
| **Level / Type / Technique** | Unit · Functional (negative-shaped, pins current behaviour) · Error Guessing — "what if the tenant is re-pointed mid-scope?" |
| **Priority / Severity / Automation** | High · High · Yes (xUnit fact) |
| **Role / Permission / Tenant** | n/a — POCO under test · n/a — no permission gate · `1111…1111` then `2222…2222` |
| **Environment** | .NET 9 SDK user-local; xUnit in `tests/NT.QAMS.Application.UnitTests`; no database |
| **Preconditions** | None. `Set` is an unconditional assignment with **no guard** (`RequestContext.cs:18` — `public void Set(Guid tenantId) => TenantId = tenantId;`). |
| **Test Data** | `a = Guid.Parse("11111111-1111-1111-1111-111111111111")`, `b = Guid.Parse("22222222-2222-2222-2222-222222222222")` |
| **Steps** | 1. `var ct = new CurrentTenant();`. 2. `ct.Set(a);`. 3. Assert `ct.TenantId == a`. 4. `ct.Set(b);` — capture that no exception is thrown. 5. Assert `ct.TenantId == b`. |
| **Expected UI** | n/a — unit level. |
| **Expected API** | n/a — unit level. |
| **Expected DB** | Step 4 throws **nothing**; `ct.TenantId == b` after step 4. No `TENANT-*` code, no `InvalidOperationException`. |
| **Expected Audit** | n/a — no ledger row. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | n/a — no persisted state. |
| **Evidence** | xUnit trx result · the no-throw capture on step 4 |
| **Result / Defect** | Not Run · — |
| **Notes** | This is not merely theoretical: **thirteen** outbox-driven policy handlers call `tenantSetter.Set(e.TenantId)` inside one `OutboxProcessor` scope (`src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:131`, `src/NT.QAMS.Application/Sla/SlaSlice.cs:130,154`, `src/NT.QAMS.Application/Improvement/ComplaintToNcPolicy.cs:26`, and nine more), so a single batch re-points the tenant once per row. See TC-TENANT-INT-014. |

#### TC-TENANT-UNIT-004 — `Elevate()` moves Unresolved → Elevated and leaves `TenantId` null  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-002 |
| **Level / Type / Technique** | Unit · Functional (positive) · State Transition — edge Unresolved →`Elevate`→ Elevated |
| **Priority / Severity / Automation** | Critical · Critical · Yes (xUnit fact) |
| **Role / Permission / Tenant** | n/a — POCO under test · n/a — `Elevate()` carries **no** permission check of its own · Unresolved (elevated) |
| **Environment** | .NET 9 SDK user-local; xUnit in `tests/NT.QAMS.Application.UnitTests`; no database |
| **Preconditions** | A `CurrentTenant` in its initial Unresolved state. |
| **Test Data** | n/a — `Elevate()` takes no argument (`src/NT.QAMS.Application/Abstractions/ICurrentTenant.cs:34`). |
| **Steps** | 1. `var ct = new CurrentTenant();`. 2. `ct.Elevate();`. 3. Read `ct.IsElevated`, `ct.TenantId`, `ct.IsResolved`. |
| **Expected UI** | n/a — unit level. |
| **Expected API** | n/a — unit level. |
| **Expected DB** | `ct.IsElevated == true` (`RequestContext.cs:26`); **`ct.TenantId == null`** and `ct.IsResolved == false` — `Elevate` touches only the flag, so an elevated background scope still stamps the nil tenant GUC. |
| **Expected Audit** | n/a — no ledger row. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | n/a — no persisted state. |
| **Evidence** | xUnit trx result · asserted property triple |
| **Result / Defect** | Not Run · — |
| **Notes** | The `TenantId == null` half is why every elevated read must also call `.IgnoreQueryFilters()` — layer 1 would otherwise filter on `TenantId == null` and match nothing (`src/NT.QAMS.Infrastructure/Persistence/AppDbContext.cs:191`). Proven end-to-end in TC-TENANT-INT-008. |

#### TC-TENANT-UNIT-005 — `Set` after `Elevate` yields the Scoped + Elevated state  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 — no URS covers the composite state; traced to source · RSK-TENANT-002 |
| **Level / Type / Technique** | Unit · Functional (positive) · Pairwise — the 2×2 of (`TenantId` set / not set) × (`IsElevated` true / false), this case covering the (set, true) pair |
| **Priority / Severity / Automation** | High · High · Yes (xUnit fact) |
| **Role / Permission / Tenant** | n/a — POCO under test · n/a — no permission gate · `33333333-3333-3333-3333-333333333333` (elevated) |
| **Environment** | .NET 9 SDK user-local; xUnit in `tests/NT.QAMS.Application.UnitTests`; no database |
| **Preconditions** | None. |
| **Test Data** | `tenantId = Guid.Parse("33333333-3333-3333-3333-333333333333")` |
| **Steps** | 1. `var ct = new CurrentTenant();`. 2. `ct.Elevate();`. 3. `ct.Set(tenantId);`. 4. Read `ct.TenantId`, `ct.IsResolved`, `ct.IsElevated`. |
| **Expected UI** | n/a — unit level. |
| **Expected API** | n/a — unit level. |
| **Expected DB** | `ct.TenantId == Guid.Parse("33333333-3333-3333-3333-333333333333")`, `ct.IsResolved == true`, **`ct.IsElevated == true`** — `Set` does **not** reset the elevation flag (`RequestContext.cs:18` assigns `TenantId` only). |
| **Expected Audit** | n/a — no ledger row. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | n/a — no persisted state. |
| **Evidence** | xUnit trx result · asserted property triple |
| **Result / Defect** | Not Run · — |
| **Notes** | The front matter §3.3 records this composite state as "reachable in principle … **no production path does this**". That statement is contradicted by `OutboxProcessor.ProcessBatchAsync` (`Elevate()` at `src/NT.QAMS.Infrastructure/Persistence/Outbox/OutboxProcessor.cs:102`) followed by any policy's `Set` — raised here as **GAP-TENANT-901** and proven at the database in TC-TENANT-INT-014. |

#### TC-TENANT-UNIT-006 — `Clear()` is the only exit from elevation, and it resets both fields  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-002 |
| **Level / Type / Technique** | Unit · Functional (positive) · Multiple-Condition — both assignments inside `Clear()` exercised from the Scoped + Elevated state, the only state where both are non-default |
| **Priority / Severity / Automation** | High · High · Yes (xUnit fact) |
| **Role / Permission / Tenant** | n/a — POCO under test · n/a — no permission gate · `33333333-3333-3333-3333-333333333333` → Unresolved |
| **Environment** | .NET 9 SDK user-local; xUnit in `tests/NT.QAMS.Application.UnitTests`; no database |
| **Preconditions** | A `CurrentTenant` in the Scoped + Elevated state produced by TC-TENANT-UNIT-005. |
| **Test Data** | `tenantId = Guid.Parse("33333333-3333-3333-3333-333333333333")` |
| **Steps** | 1. `var ct = new CurrentTenant(); ct.Elevate(); ct.Set(tenantId);`. 2. `ct.Clear();`. 3. Read `ct.TenantId`, `ct.IsResolved`, `ct.IsElevated`. 4. Enumerate `typeof(ICurrentTenantSetter).GetMethods()` and assert the method-name set is exactly `{ "Set", "Clear", "Elevate" }`. |
| **Expected UI** | n/a — unit level. |
| **Expected API** | n/a — unit level. |
| **Expected DB** | After step 2: `ct.TenantId == null`, `ct.IsResolved == false`, `ct.IsElevated == false` — `Clear()` assigns both (`RequestContext.cs:20-24`). Step 4 returns exactly three method names; **no `Demote` exists** (`src/NT.QAMS.Application/Abstractions/ICurrentTenant.cs:23-35`). |
| **Expected Audit** | n/a — no ledger row. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | n/a — no persisted state. |
| **Evidence** | xUnit trx result · the reflected method-name set |
| **Result / Defect** | Not Run · — |
| **Notes** | A repository-wide search for a tenant-scope `Clear()` call in `src/` returns **zero** hits — elevation ends only when the DI scope is disposed. That is safe for the six `IServiceScopeFactory.CreateScope()` owners, and it is the reason `ProvisionTenantHandler`'s request-scope elevation must be gated rather than unwound. |

#### TC-TENANT-UNIT-007 — Interceptor rule 1: an Unresolved, non-elevated context stamps the nil tenant and `'off'`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-005 |
| **Level / Type / Technique** | Unit · Functional (positive) · Decision Table — rule 1 of the 2×2 over (`ICurrentTenant.TenantId` null/non-null) × (`IsElevated` false/true) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (xUnit fact with a recording `DbConnection` double) |
| **Role / Permission / Tenant** | n/a — the interceptor takes `ICurrentTenant`, not an actor · n/a — no permission gate · Unresolved |
| **Environment** | .NET 9 SDK user-local; xUnit in `tests/NT.QAMS.Application.UnitTests`; no database — a `RecordingDbConnection : DbConnection` whose `CreateDbCommand()` returns a `RecordingDbCommand` capturing `CommandText` and `Parameters` |
| **Preconditions** | A stub `ICurrentTenant` with `TenantId = null`, `IsResolved = false`, `IsElevated = false` (the shape `tests/NT.QAMS.Application.UnitTests/TestDoubles.cs:10` already exposes). |
| **Test Data** | Stub state: `TenantId = null`, `IsElevated = false`. Expected nil literal: `00000000-0000-0000-0000-000000000000`. |
| **Steps** | 1. `var interceptor = new TenantConnectionInterceptor(stub);`. 2. Call `interceptor.ConnectionOpened(recordingConnection, eventData)`. 3. Read the recorded `CommandText`. 4. Read parameter `@tenant`. 5. Read parameter `@bypass`. |
| **Expected UI** | n/a — unit level. |
| **Expected API** | n/a — unit level. |
| **Expected DB** | `CommandText` is exactly `SELECT set_config('app.current_tenant', @tenant, false), set_config('app.bypass_rls', @bypass, false)` (`TenantConnectionInterceptor.cs:53-54`); `@tenant` value is the string `"00000000-0000-0000-0000-000000000000"` (`:21,55` — `Guid.Empty.ToString()`); `@bypass` value is the string `"off"` (`:56`). |
| **Expected Audit** | n/a — the interceptor writes no ledger row. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | Dispose the recording connection. |
| **Evidence** | xUnit trx result · captured `CommandText` string · captured parameter values |
| **Result / Defect** | Not Run · — |
| **Notes** | The nil UUID is not a wildcard: the policy predicate `tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid` compares it against real `tenant_id` values, and no row carries the nil UUID. Fail-closed by data, not by branch. |

#### TC-TENANT-UNIT-008 — Interceptor rule 2: a Scoped, non-elevated context stamps that tenant and `'off'`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-001 |
| **Level / Type / Technique** | Unit · Functional (positive) · Decision Table — rule 2 of the same 2×2 |
| **Priority / Severity / Automation** | Critical · Critical · Yes (xUnit fact with a recording `DbConnection` double) |
| **Role / Permission / Tenant** | n/a — interceptor takes `ICurrentTenant` · n/a — no permission gate · `44444444-4444-4444-4444-444444444444` |
| **Environment** | .NET 9 SDK user-local; xUnit in `tests/NT.QAMS.Application.UnitTests`; no database — recording `DbConnection` double |
| **Preconditions** | A stub `ICurrentTenant` with `TenantId = Guid.Parse("44444444-4444-4444-4444-444444444444")`, `IsElevated = false`. |
| **Test Data** | `44444444-4444-4444-4444-444444444444` (36-character lower-case "D" format, which is what `Guid.ToString()` emits and what `::uuid` accepts). |
| **Steps** | 1. Construct the interceptor over the stub. 2. Call `ConnectionOpened`. 3. Read `@tenant`. 4. Read `@bypass`. 5. Assert `@tenant` is not the nil literal. |
| **Expected UI** | n/a — unit level. |
| **Expected API** | n/a — unit level. |
| **Expected DB** | `@tenant == "44444444-4444-4444-4444-444444444444"` — the left branch of `currentTenant.TenantId?.ToString() ?? NilTenant` (`TenantConnectionInterceptor.cs:55`); `@bypass == "off"`. |
| **Expected Audit** | n/a — no ledger row. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | Dispose the recording connection. |
| **Evidence** | xUnit trx result · captured parameter values |
| **Result / Defect** | Not Run · — |
| **Notes** | Together with TC-TENANT-UNIT-007 this covers both arms of the `??` on `:55`, i.e. full branch coverage of the tenant-value expression. |

#### TC-TENANT-UNIT-009 — Interceptor rule 3: an Elevated, unresolved context stamps the nil tenant and `'on'`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-002 |
| **Level / Type / Technique** | Unit · Functional (positive) · Decision Table — rule 3 of the same 2×2; also the true arm of the ternary on `:56` |
| **Priority / Severity / Automation** | Critical · Critical · Yes (xUnit fact with a recording `DbConnection` double) |
| **Role / Permission / Tenant** | n/a — interceptor takes `ICurrentTenant` · n/a — no permission gate · Unresolved (elevated) |
| **Environment** | .NET 9 SDK user-local; xUnit in `tests/NT.QAMS.Application.UnitTests`; no database — recording `DbConnection` double |
| **Preconditions** | A stub `ICurrentTenant` with `TenantId = null`, `IsElevated = true` — exactly the state a `BackgroundService` scope is in after `Elevate()`. |
| **Test Data** | Stub state: `TenantId = null`, `IsElevated = true`. |
| **Steps** | 1. Construct the interceptor over the stub. 2. Call `ConnectionOpened`. 3. Read `@tenant`. 4. Read `@bypass`. |
| **Expected UI** | n/a — unit level. |
| **Expected API** | n/a — unit level. |
| **Expected DB** | `@tenant == "00000000-0000-0000-0000-000000000000"`; **`@bypass == "on"`** — exactly the three lower-case characters, no surrounding whitespace, no `"ON"` (`TenantConnectionInterceptor.cs:56`). |
| **Expected Audit** | n/a — no ledger row. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | Dispose the recording connection. |
| **Evidence** | xUnit trx result · captured parameter values, byte-compared against `"on"` |
| **Result / Defect** | Not Run · — |
| **Notes** | Casing matters at the database: the policy compares `current_setting('app.bypass_rls', true) = 'on'`, a literal string equality (`src/NT.QAMS.Infrastructure/Migrations/20260726081443_ActivateForcedTenantRls.cs:38,42`). The negative counterpart is TC-TENANT-INT-009. |

#### TC-TENANT-UNIT-010 — Interceptor rule 4: a Scoped **and** Elevated context stamps that tenant and `'on'`  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 — the composite state has no URS; traced to source · RSK-TENANT-002 |
| **Level / Type / Technique** | Unit · Functional (positive) · Decision Table — rule 4, the fourth and last combination, completing MC-DC over the two independent inputs of `CreateCommand` |
| **Priority / Severity / Automation** | High · High · Yes (xUnit fact with a recording `DbConnection` double) |
| **Role / Permission / Tenant** | n/a — interceptor takes `ICurrentTenant` · n/a — no permission gate · `55555555-5555-5555-5555-555555555555` (elevated) |
| **Environment** | .NET 9 SDK user-local; xUnit in `tests/NT.QAMS.Application.UnitTests`; no database — recording `DbConnection` double |
| **Preconditions** | A stub `ICurrentTenant` with `TenantId = Guid.Parse("55555555-5555-5555-5555-555555555555")`, `IsElevated = true`. |
| **Test Data** | `55555555-5555-5555-5555-555555555555`, elevated. |
| **Steps** | 1. Construct the interceptor over the stub. 2. Call `ConnectionOpened`. 3. Read `@tenant`. 4. Read `@bypass`. |
| **Expected UI** | n/a — unit level. |
| **Expected API** | n/a — unit level. |
| **Expected DB** | `@tenant == "55555555-5555-5555-5555-555555555555"` **and** `@bypass == "on"` — the two parameters are computed independently (`TenantConnectionInterceptor.cs:55` and `:56`), so elevation does **not** blank the tenant. |
| **Expected Audit** | n/a — no ledger row. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | Dispose the recording connection. |
| **Evidence** | xUnit trx result · captured parameter values |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the GUC pair every outbox delivery produces once a policy calls `Set` (see TC-TENANT-UNIT-005, TC-TENANT-INT-014, **GAP-TENANT-901**). Under it the RLS predicate is satisfied by its **second** disjunct for every row, so the tenant value is inert — but it is the value stamped onto rows by `TenantStampInterceptor`, so it is not cosmetic. |

#### TC-TENANT-UNIT-011 — Both GUC values travel as bound parameters, never interpolated, and `is_local` is `false`  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 — no URS states the binding requirement; traced to source · RSK-TENANT-001 |
| **Level / Type / Technique** | Unit · Security (positive) · Data Flow — definition of the tenant value at `:55` to its use as a `DbParameter` at `:60-66`, asserting no def-use path passes through string concatenation |
| **Priority / Severity / Automation** | High · Critical · Yes (xUnit fact with a recording `DbConnection` double) |
| **Role / Permission / Tenant** | n/a — interceptor takes `ICurrentTenant` · n/a — no permission gate · `66666666-6666-6666-6666-666666666666` |
| **Environment** | .NET 9 SDK user-local; xUnit in `tests/NT.QAMS.Application.UnitTests`; no database — recording `DbConnection` double |
| **Preconditions** | A stub `ICurrentTenant` with `TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666")`, `IsElevated = false`. |
| **Test Data** | `66666666-6666-6666-6666-666666666666` |
| **Steps** | 1. Construct the interceptor over the stub. 2. Call `ConnectionOpened`. 3. Assert the recorded `CommandText` **contains** the literal substrings `@tenant` and `@bypass`. 4. Assert the recorded `CommandText` **does not contain** the substring `66666666`. 5. Assert `Parameters.Count == 2` and that the parameter names are `tenant` and `bypass`. 6. Assert the recorded `CommandText` contains the literal `, false)` twice. |
| **Expected UI** | n/a — unit level. |
| **Expected API** | n/a — unit level. |
| **Expected DB** | Step 3 passes; step 4 passes (the GUID never appears inside the SQL text); step 5 yields exactly 2 parameters named `tenant` and `bypass` (`TenantConnectionInterceptor.cs:60-66`); step 6 confirms `set_config(..., false)` twice — **session** scope, not transaction-local (comment `:51-52`). |
| **Expected Audit** | n/a — no ledger row. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | Dispose the recording connection. |
| **Evidence** | xUnit trx result · full recorded `CommandText` · parameter-name list |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 6 pins the semantics that TC-TENANT-INT-005 measures at the database: session scope means the value survives a `ROLLBACK`, which is exactly why re-stamping on every open (TC-TENANT-INT-003) is the safety property rather than transaction-locality. |

#### TC-TENANT-UNIT-012 — Only the two `ConnectionOpened` overrides exist; nothing runs on close, create or dispose  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-003 |
| **Level / Type / Technique** | Unit · Structural · Statement/Branch coverage of the interceptor's override surface, enumerated by reflection |
| **Priority / Severity / Automation** | Medium · High · Yes (xUnit fact) |
| **Role / Permission / Tenant** | n/a — reflection over a type · n/a — no permission gate · n/a — no tenant is resolved for a reflection assertion |
| **Environment** | .NET 9 SDK user-local; xUnit in `tests/NT.QAMS.Application.UnitTests`; no database |
| **Preconditions** | None. |
| **Test Data** | Expected override-name set: `{ "ConnectionOpened", "ConnectionOpenedAsync" }`. |
| **Steps** | 1. `var declared = typeof(TenantConnectionInterceptor).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => m.IsVirtual && m.GetBaseDefinition().DeclaringType == typeof(DbConnectionInterceptor)).Select(m => m.Name).ToHashSet();`. 2. Assert `declared` equals `{ "ConnectionOpened", "ConnectionOpenedAsync" }`. 3. Invoke `ConnectionOpenedAsync(recordingConnection, eventData, CancellationToken.None)` and assert one command was recorded. |
| **Expected UI** | n/a — unit level. |
| **Expected API** | n/a — unit level. |
| **Expected DB** | Step 2 passes with exactly two names (`TenantConnectionInterceptor.cs:23,29`); step 3 records exactly one command whose text matches TC-TENANT-UNIT-007's. No override of `ConnectionClosed`, `ConnectionCreated`, `ConnectionDisposing` or `ConnectionFailed` is declared. |
| **Expected Audit** | n/a — no ledger row. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | Dispose the recording connection. |
| **Evidence** | xUnit trx result · the reflected override-name set |
| **Result / Defect** | Not Run · — |
| **Notes** | The absence of a `ConnectionClosed` override is deliberate and is the reason the pooled-reuse guarantee rests entirely on **re-stamping at open** rather than on clearing at close. TC-TENANT-INT-003 and TC-TENANT-INT-004 are the database-level proof of that design choice. |

#### TC-TENANT-UNIT-013 — An architecture test caps the `Elevate()` call sites at the eight allow-listed ones  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 — no URS demands the control; the gap does · RSK-TENANT-006 |
| **Level / Type / Technique** | Unit (architecture) · Structural / Regression-prevention · Error Guessing — "a ninth call site is added and nothing notices" |
| **Priority / Severity / Automation** | Critical · Critical · Yes — this case **is** the automation |
| **Role / Permission / Tenant** | n/a — source-file analysis, no actor · n/a — no permission gate · n/a — no tenant is resolved for a source scan |
| **Environment** | .NET 9 SDK user-local; xUnit in `tests/NT.QAMS.Architecture.Tests` (which currently holds only `CommandPolicyTests.cs`, `LayerRulesTests.cs`, `ModuleBoundaryTests.cs`, `UserAccountTenantBoundTests.cs` — **none mentions `Elevate`**); no database |
| **Preconditions** | **Blocked on GAP-TENANT-014.** No such test exists today. Measured 2026-08-01: `grep -rn "Elevate()" src/ --include=*.cs` returns 8 call sites plus the interface declaration (`src/NT.QAMS.Application/Abstractions/ICurrentTenant.cs:34`) and the implementation (`src/NT.QAMS.Infrastructure/Services/RequestContext.cs:26`). |
| **Test Data** | Allow-list, one entry per site with a justification: `ProvisionTenant.cs:41`; `StartupSeeding.cs:99`; `StartupSeeding.cs:132`; `KpiSnapshotService.cs:63`; `ScheduledSweepService.cs:64`; `OutboxProcessor.cs:102`; `OutboxProcessor.cs:225`; `OutboxProcessor.cs:246`. |
| **Steps** | 1. Enumerate every `.cs` file under `src/`, excluding `bin/` and `obj/`. 2. Match the regular expression `\.Elevate\(\)` on each line, excluding the declaration line in `ICurrentTenant.cs` and the implementation line in `RequestContext.cs`. 3. Project each match to `relativePath:lineNumber`. 4. Assert the resulting set equals the allow-list exactly — no extras **and** no missing entries. 5. Mutation check: insert `tenantSetter.Elevate();` into a scratch command handler, re-run, and assert the test fails naming that file and line. 6. Revert the mutation. |
| **Expected UI** | n/a — architecture test, no UI. |
| **Expected API** | n/a — architecture test, no HTTP. |
| **Expected DB** | n/a — the test reads source files only; no connection is opened. |
| **Expected Audit** | n/a — no ledger row is produced by a source scan. |
| **Expected Notification** | n/a — a CI failure is the notification channel, and it is not a QMS notification. |
| **Cleanup** | Revert the step-5 mutation; confirm `git status` is clean before committing. |
| **Evidence** | The allow-list file · xUnit trx result for both the clean run and the mutated run · the failure message naming the injected file and line |
| **Result / Defect** | Not Run · — |
| **Notes** | **Acceptance criteria to implement against** (mirrors GAP-TENANT-014): (1) the allow-list lives in one file and carries a one-line justification per entry; (2) the test fails on an addition **and** on a silent removal, so the list cannot rot; (3) the failure message names file and line; (4) `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md:72` is regenerated from the allow-list so the document and the control can never disagree. Do **not** execute this case as a passing test before the control exists — it has nothing to run. |

---

## Part 2 — GUC behaviour and elevation paths against a real PostgreSQL 17 (integration level)

#### TC-TENANT-INT-001 — A scoped context stamps its tenant into the session GUCs on connection open  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-001 |
| **Level / Type / Technique** | Integration · Functional (positive) · State Transition — Scoped state observed at the database boundary |
| **Priority / Severity / Automation** | Critical · Critical · Yes (`SkippableFact`, `[Collection("real-postgres")]`) |
| **Role / Permission / Tenant** | n/a — the fixture drives `ICurrentTenant` directly, no HTTP actor · n/a — RLS is not permission-gated · a fresh `Guid.CreateVersion7()`, call it `tenantA` |
| **Environment** | `tests/NT.QAMS.IntegrationTests` with `QMS_ITEST_POSTGRES=Host=localhost;Database=ntqams;Username=qams_app;Password=dev-only-local`; PostgreSQL 17 at `C:\Program Files\PostgreSQL\17\bin`; migrated schema; the run aborts unless FORCE RLS is present and the role lacks `SUPERUSER`/`BYPASSRLS` (`tests/NT.QAMS.IntegrationTests/RealPostgresFixture.cs:42-65`) |
| **Preconditions** | `fx.Available == true`. `fx.CreateContext(out var ctx)` wires the production `TenantConnectionInterceptor` (`RealPostgresFixture.cs:89`). |
| **Test Data** | `tenantA = Guid.CreateVersion7()` |
| **Steps** | 1. `using var db = fx.CreateContext(out var ctx);`. 2. `ctx.Set(tenantA);`. 3. `await using var tx = await db.Database.BeginTransactionAsync();` — this opens the connection, firing the interceptor. 4. `SELECT current_setting('app.current_tenant', true)` via `db.Database.SqlQueryRaw<string>`. 5. `SELECT current_setting('app.bypass_rls', true)`. 6. `await tx.RollbackAsync();`. |
| **Expected UI** | n/a — integration level, no UI. |
| **Expected API** | n/a — integration level, no HTTP. |
| **Expected DB** | Step 4 returns exactly `tenantA.ToString()` (36-character lower-case form). Step 5 returns exactly `off`. Both are session-scoped because `set_config`'s third argument is `false` (`TenantConnectionInterceptor.cs:54`). |
| **Expected Audit** | n/a — reading a GUC writes no `audit.*` row. |
| **Expected Notification** | n/a — no notification is defined for a connection open. |
| **Cleanup** | `await tx.RollbackAsync()` (step 6) — the fixture's standing pattern; nothing is written, so nothing persists. |
| **Evidence** | xUnit trx result · the two `current_setting` return values · the Npgsql backend PID from `SELECT pg_backend_pid()` for correlation with TC-TENANT-INT-003 |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert the exact string, not a case-insensitive comparison: `NULLIF(current_setting(...),'')::uuid` will parse either case, but a drift from `Guid.ToString()`'s "D" format would be a real change worth catching. |

#### TC-TENANT-INT-002 — An unresolved context fails closed at both isolation layers  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-005 |
| **Level / Type / Technique** | Integration · Functional (negative) · Equivalence Partitioning — the "no tenant resolved" partition, covering both the EF filter and the RLS policy |
| **Priority / Severity / Automation** | Critical · Critical · Yes (`SkippableFact`) |
| **Role / Permission / Tenant** | n/a — no actor; this is the anonymous / platform-admin request shape · n/a — no permission gate · Unresolved (nil GUC) |
| **Environment** | `tests/NT.QAMS.IntegrationTests` with `QMS_ITEST_POSTGRES` as above; PostgreSQL 17; migrated schema |
| **Preconditions** | `fx.Available == true`. Two `qams.outlier_screening` rows seeded under an elevated context, one per tenant, inside the rollback transaction — the same seeding shape as `tests/NT.QAMS.IntegrationTests/RlsTenantIsolationTests.cs:24-35`. |
| **Test Data** | `tenantA`, `tenantB` = two fresh `Guid.CreateVersion7()`; rows `OutlierScreening.Configure("TC-INT-002-A", "dataset A", "u")` and `OutlierScreening.Configure("TC-INT-002-B", "dataset B", "u")` |
| **Steps** | 1. Seed the two rows under `ctx.Elevate()` and `SaveChangesAsync`. 2. `ctx.Clear();` so `TenantId` is `null` and `IsElevated` is `false`. 3. Force a reopen by disposing and recreating the context over the same connection string, then `SELECT current_setting('app.current_tenant', true)`. 4. `db.OutlierScreenings.IgnoreQueryFilters().CountAsync(s => s.ScreeningRef == "TC-INT-002-A" || s.ScreeningRef == "TC-INT-002-B")`. 5. Repeat step 4 **without** `IgnoreQueryFilters()`. 6. Roll back. |
| **Expected UI** | n/a — integration level. |
| **Expected API** | n/a — integration level. |
| **Expected DB** | Step 3 returns `00000000-0000-0000-0000-000000000000`. Step 4 returns **0** — RLS alone hides both rows, because `tenant_id = '000…0'::uuid` is false for both and `bypass_rls` is `off`. Step 5 returns **0** — the EF filter `e.TenantId == _currentTenant.TenantId` resolves to `TenantId == null`, which matches nothing (`src/NT.QAMS.Infrastructure/Persistence/AppDbContext.cs:191`). |
| **Expected Audit** | n/a — a `SELECT` writes no `audit.*` row. |
| **Expected Notification** | n/a — no notification for an empty read. |
| **Cleanup** | Roll back the transaction; the two seeded rows never persist. |
| **Evidence** | xUnit trx result · the nil GUC string · both count values |
| **Result / Defect** | Not Run · — |
| **Notes** | Steps 4 and 5 must both be asserted: step 5 alone would pass even if RLS were disabled, and step 4 alone would pass even if the EF filter were removed. Fail-closed is a claim about **both** layers. |

#### TC-TENANT-INT-003 — A pooled physical connection is re-stamped, so tenant A's GUC cannot serve tenant B  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-003 |
| **Level / Type / Technique** | Integration · Security (negative) · Error Guessing — the pooled-connection carryover attack named in the interceptor's own doc comment (`TenantConnectionInterceptor.cs:9-11`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (`SkippableFact`) |
| **Role / Permission / Tenant** | n/a — the fixture drives `ICurrentTenant` directly · n/a — no permission gate · `tenantA` then `tenantB` on the **same** backend |
| **Environment** | `tests/NT.QAMS.IntegrationTests`; connection string forced to a single physical connection: `Host=localhost;Database=ntqams;Username=qams_app;Password=dev-only-local;Maximum Pool Size=1;Minimum Pool Size=1` — so the second context is guaranteed to reuse the first's backend |
| **Preconditions** | `fx.Available == true`. One `qams.outlier_screening` row exists for `tenantA`, committed **outside** the assertion transaction is not permitted (the suite never persists), so seed inside a transaction that stays open across steps 2–6 and roll back at the end. |
| **Test Data** | `tenantA`, `tenantB` = two fresh `Guid.CreateVersion7()`; row `OutlierScreening.Configure("TC-INT-003-A", "dataset A", "u")` stamped to `tenantA` |
| **Steps** | 1. Open context 1 with `ctx1.Set(tenantA)`; run `SELECT pg_backend_pid()` and record it as `pid1`; seed the `tenantA` row under `ctx1.Elevate()`. 2. `SELECT current_setting('app.current_tenant', true)` → record. 3. Dispose context 1's connection back to the pool **without** clearing anything. 4. Open context 2 with `ctx2.Set(tenantB)` on the same connection string; run `SELECT pg_backend_pid()` and assert it equals `pid1`. 5. `SELECT current_setting('app.current_tenant', true)` and `current_setting('app.bypass_rls', true)`. 6. `db2.OutlierScreenings.IgnoreQueryFilters().CountAsync(s => s.ScreeningRef == "TC-INT-003-A")`. 7. Roll back. |
| **Expected UI** | n/a — integration level. |
| **Expected API** | n/a — integration level. |
| **Expected DB** | Step 2 returns `tenantA.ToString()`. Step 4 asserts the **same** backend PID, proving physical reuse rather than a fresh connection. Step 5 returns `tenantB.ToString()` and `off` — the interceptor re-stamped on open (`TenantConnectionInterceptor.cs:23,29,53-56`), so tenant A's session value is gone even though `set_config` used session scope. Step 6 returns **0** — tenant B cannot see tenant A's row. |
| **Expected Audit** | n/a — no `audit.*` row is written by a read. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | Roll back; dispose both contexts; call `NpgsqlConnection.ClearPool` on the single-connection pool so the forced `Maximum Pool Size=1` does not affect neighbouring tests. |
| **Evidence** | Both `pg_backend_pid()` values (must be equal) · the two `current_setting` readings · the count from step 6 |
| **Result / Defect** | Not Run · — |
| **Notes** | The PID equality assertion in step 4 is what makes this case meaningful. Without it a pass could mean "the pool handed us a fresh backend", which proves nothing. If the PIDs differ, the case is **inconclusive**, not passing — record it as blocked and re-run with `Minimum Pool Size=1` and no concurrent test collection. |

#### TC-TENANT-INT-004 — Pooled reuse into an *unresolved* context fails closed, not open  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-003, RSK-TENANT-005 |
| **Level / Type / Technique** | Integration · Security (negative) · Boundary Value Analysis — the boundary between a resolved and an unresolved context on one physical connection, the worst-case direction of TC-TENANT-INT-003 |
| **Priority / Severity / Automation** | Critical · Critical · Yes (`SkippableFact`) |
| **Role / Permission / Tenant** | n/a — the fixture drives `ICurrentTenant` directly · n/a — no permission gate · `tenantA` then Unresolved on the **same** backend |
| **Environment** | `tests/NT.QAMS.IntegrationTests`; `Maximum Pool Size=1;Minimum Pool Size=1` as in TC-TENANT-INT-003; PostgreSQL 17 |
| **Preconditions** | `fx.Available == true`. Same single-connection pool arrangement. |
| **Test Data** | `tenantA = Guid.CreateVersion7()`; row `OutlierScreening.Configure("TC-INT-004-A", "dataset A", "u")` stamped to `tenantA` |
| **Steps** | 1. Context 1: `ctx1.Set(tenantA)`, record `pg_backend_pid()` as `pid1`, seed the row under `ctx1.Elevate()`. 2. Return the connection to the pool. 3. Context 2: leave the stub **unresolved** — do not call `Set`, do not call `Elevate`. 4. Assert `pg_backend_pid() == pid1`. 5. `SELECT current_setting('app.current_tenant', true)` and `current_setting('app.bypass_rls', true)`. 6. `db2.OutlierScreenings.IgnoreQueryFilters().CountAsync(s => s.ScreeningRef == "TC-INT-004-A")`. 7. Roll back. |
| **Expected UI** | n/a — integration level. |
| **Expected API** | n/a — integration level. |
| **Expected DB** | Step 4: same backend PID. Step 5: `00000000-0000-0000-0000-000000000000` and `off` — **not** the inherited `tenantA` value and **not** the inherited `on` from context 1's elevation. Step 6 returns **0**. |
| **Expected Audit** | n/a — no `audit.*` row. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | Roll back; `NpgsqlConnection.ClearPool`. |
| **Evidence** | Both PIDs · both `current_setting` readings · the count |
| **Result / Defect** | Not Run · — |
| **Notes** | The `bypass_rls` half is the sharper assertion: context 1 elevated, and if elevation leaked across the pool an unresolved caller would read **every** tenant. This case is the reason `@bypass` is unconditionally re-stamped rather than only written when elevation is on. |

#### TC-TENANT-INT-005 — The GUCs are session-scoped and survive a transaction rollback  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-003 |
| **Level / Type / Technique** | Integration · Functional (characterisation) · Decision Table — `set_config(..., is_local)` over {`false` = session, `true` = transaction} × {COMMIT, ROLLBACK}, this case covering the implemented `false` arm |
| **Priority / Severity / Automation** | High · Medium · Yes (`SkippableFact`) |
| **Role / Permission / Tenant** | n/a — fixture-driven · n/a — no permission gate · `tenantA` |
| **Environment** | `tests/NT.QAMS.IntegrationTests` with `QMS_ITEST_POSTGRES`; PostgreSQL 17 |
| **Preconditions** | `fx.Available == true`. |
| **Test Data** | `tenantA = Guid.CreateVersion7()` |
| **Steps** | 1. `ctx.Set(tenantA)`; open the connection with a first `BeginTransactionAsync`. 2. Read `current_setting('app.current_tenant', true)` → expect `tenantA`. 3. `await tx.RollbackAsync();`. 4. Without closing the connection, read `current_setting('app.current_tenant', true)` again. 5. Begin a second transaction, read again, and roll back. |
| **Expected UI** | n/a — integration level. |
| **Expected API** | n/a — integration level. |
| **Expected DB** | Steps 2, 4 and 5 all return `tenantA.ToString()`. The rollback in step 3 does **not** revert the GUC, because the interceptor passes `false` as `set_config`'s third argument (`TenantConnectionInterceptor.cs:54`); a transaction-local `true` would have reverted it, and step 4 would have returned the empty string. |
| **Expected Audit** | n/a — no `audit.*` row. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | Roll back the second transaction; dispose the context. |
| **Evidence** | The three `current_setting` readings across the rollback boundary |
| **Result / Defect** | Not Run · — |
| **Notes** | Session scope is a deliberate trade: it makes the value robust across the many short transactions a single request issues, at the cost of making **re-stamping on open** the sole guarantee against pool carryover — which is why TC-TENANT-INT-003 and TC-TENANT-INT-004 are rated Critical while this one is Medium. Note the contrast with the migrations, which use transaction-local `set_config('app.bypass_rls','on',true)` (`src/NT.QAMS.Infrastructure/Migrations/20260731201114_Hardening4_ChildTenancy.cs:260`). |

#### TC-TENANT-INT-006 — Calling `Set` **after** the connection is open leaves the stale nil GUC and the write is refused `42501`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-001 |
| **Level / Type / Technique** | Integration · Functional (negative) · State Transition — the illegal ordering edge; the negative counterpart of the pin `Login_shaped_write_passes_when_the_request_is_scoped_to_the_events_tenant` (`tests/NT.QAMS.IntegrationTests/SecurityEventRlsTests.cs:140-161`) |
| **Priority / Severity / Automation** | Critical · High · Yes (`SkippableFact`) |
| **Role / Permission / Tenant** | n/a — fixture-driven · n/a — no permission gate · Unresolved at open, `tenantA` afterwards |
| **Environment** | `tests/NT.QAMS.IntegrationTests` with `QMS_ITEST_POSTGRES`; PostgreSQL 17 |
| **Preconditions** | `fx.Available == true`. The context must be created **unresolved** so the first open stamps the nil tenant. |
| **Test Data** | `tenantA = Guid.CreateVersion7()`; row `OutlierScreening.Configure("TC-INT-006", "late scope", "u")` with `((ITenantScoped)row).TenantId = tenantA` |
| **Steps** | 1. `using var db = fx.CreateContext(out var ctx);` — do **not** call `Set`. 2. `await using var tx = await db.Database.BeginTransactionAsync();` — the connection opens with the nil GUC. 3. **Now** call `ctx.Set(tenantA);`. 4. Read `current_setting('app.current_tenant', true)` on the still-open connection. 5. Add the row and call `SaveChangesAsync()`. 6. Catch the exception, unwrap `InnerException` to `Npgsql.PostgresException`, read `SqlState`. 7. Roll back. |
| **Expected UI** | n/a — integration level. |
| **Expected API** | n/a — integration level; the HTTP mapping of this failure is **GAP-TENANT-006** and is not asserted here. |
| **Expected DB** | Step 4 returns `00000000-0000-0000-0000-000000000000` — the late `Set` had no effect on the already-open connection. Step 5 throws `Microsoft.EntityFrameworkCore.DbUpdateException`; step 6 yields `SqlState == "42501"` — the strict `WITH CHECK` on `qams.outlier_screening` refused a row whose `tenant_id` matches neither the nil GUC nor `bypass_rls='on'`. No row exists in `qams.outlier_screening` with `screening_ref = 'TC-INT-006'`. |
| **Expected Audit** | n/a — the transaction is refused before any `audit.*` append is committed; assert `SELECT count(*) FROM audit.field_change WHERE entity_id = <row id>` is `0` after rollback. |
| **Expected Notification** | n/a — no notification is defined for an RLS refusal. |
| **Cleanup** | Roll back; nothing persists. |
| **Evidence** | The nil GUC reading at step 4 · the `PostgresException.SqlState` value `42501` · the post-rollback row count of 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the failure shape the login regression produced before `LoginHandler` was reordered to call `tenantScope.Set(tenant.Id)` at `src/NT.QAMS.Application/IdentityAccess/Commands/Login.cs:58`, i.e. before any database work. Assert the SqlState, not the exception message — the message text is not a contract. |

#### TC-TENANT-INT-007 — Calling `Elevate` **after** the connection is open does not turn on bypass for that connection  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 — no URS states the timing property; traced to source · RSK-TENANT-002 |
| **Level / Type / Technique** | Integration · Functional (characterisation) · State Transition — the elevation direction of the same illegal-ordering edge as TC-TENANT-INT-006 |
| **Priority / Severity / Automation** | High · High · Yes (`SkippableFact`) |
| **Role / Permission / Tenant** | n/a — fixture-driven · n/a — no permission gate · Unresolved, then Elevated |
| **Environment** | `tests/NT.QAMS.IntegrationTests` with `QMS_ITEST_POSTGRES`; PostgreSQL 17 |
| **Preconditions** | `fx.Available == true`. Two `qams.outlier_screening` rows for two different tenants exist inside the rollback transaction, seeded first under an elevated context on a **separate** context instance. |
| **Test Data** | `tenantA`, `tenantB` = two fresh `Guid.CreateVersion7()`; rows `"TC-INT-007-A"` and `"TC-INT-007-B"` |
| **Steps** | 1. Seed the two rows with context 1 under `ctx1.Elevate()`. 2. Create context 2 unresolved and not elevated; open the connection with `BeginTransactionAsync`. 3. Read `current_setting('app.bypass_rls', true)`. 4. Call `ctx2.Elevate()`. 5. Read `current_setting('app.bypass_rls', true)` again on the same open connection. 6. `db2.OutlierScreenings.IgnoreQueryFilters().CountAsync(...)` for the two refs. 7. Close and reopen the connection (dispose the transaction, then issue a new query), read `bypass_rls` a third time, and repeat the count. 8. Roll back. |
| **Expected UI** | n/a — integration level. |
| **Expected API** | n/a — integration level. |
| **Expected DB** | Step 3: `off`. Step 5: **still `off`** — `Elevate()` mutates only the in-process flag (`src/NT.QAMS.Infrastructure/Services/RequestContext.cs:26`); the GUC changes only on the next open. Step 6 returns **0**. Step 7: `on`, and the count becomes **2**. |
| **Expected Audit** | n/a — reads only. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | Roll back; dispose both contexts. |
| **Evidence** | The three `bypass_rls` readings · the two count values (0 then 2) |
| **Result / Defect** | Not Run · — |
| **Notes** | This is why all six elevated components call `Elevate()` **before** the first query. Five of the eight sites also call it before resolving `AppDbContext` from the scope; `StartupSeeding.BackfillRolesAndAssignmentsAsync` inverts that order — see TC-TENANT-INT-017 and **GAP-TENANT-903**. |

#### TC-TENANT-INT-008 — Elevation alone grants nothing: cross-tenant reads need `IgnoreQueryFilters()` too  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-002 |
| **Level / Type / Technique** | Integration · Functional (positive + negative) · Decision Table — rows 5 and 6 of the front matter's §3.4 row-visibility table: (Elevated, no `IgnoreQueryFilters`) and (Elevated, `IgnoreQueryFilters`) |
| **Priority / Severity / Automation** | Critical · High · Yes (`SkippableFact`) |
| **Role / Permission / Tenant** | n/a — fixture-driven · n/a — no permission gate · Unresolved (elevated) |
| **Environment** | `tests/NT.QAMS.IntegrationTests` with `QMS_ITEST_POSTGRES`; PostgreSQL 17 |
| **Preconditions** | `fx.Available == true`. |
| **Test Data** | `tenantA`, `tenantB` = two fresh `Guid.CreateVersion7()`; rows `"TC-INT-008-A"` (tenantA) and `"TC-INT-008-B"` (tenantB) |
| **Steps** | 1. `ctx.Elevate();` **before** any query. 2. Seed both rows with explicit `((ITenantScoped)row).TenantId` assignments and `SaveChangesAsync()` inside a transaction. 3. `db.OutlierScreenings.CountAsync(s => s.ScreeningRef.StartsWith("TC-INT-008"))` — **without** `IgnoreQueryFilters()`. 4. `db.OutlierScreenings.IgnoreQueryFilters().CountAsync(s => s.ScreeningRef.StartsWith("TC-INT-008"))`. 5. Read `current_setting('app.bypass_rls', true)`. 6. Roll back. |
| **Expected UI** | n/a — integration level. |
| **Expected API** | n/a — integration level. |
| **Expected DB** | Step 3 returns **0** — the EF layer-1 filter is `e.TenantId == _currentTenant.TenantId`, and under elevation `_currentTenant.TenantId` is `null` (`AppDbContext.cs:191`; `RequestContext.cs:26` leaves `TenantId` untouched). Step 4 returns **2**. Step 5 returns `on`, confirming the database fence was open the whole time and it was EF that filtered. |
| **Expected Audit** | Step 2's `SaveChangesAsync` writes `audit.field_change` rows for the two inserts; both must carry `tenant_id` equal to their own row's tenant, not null — assert `SELECT count(*) FROM audit.field_change WHERE tenant_id IS NULL AND entity_type = 'OutlierScreening' AND entity_id IN (…)` is `0`. |
| **Expected Notification** | n/a — `OutlierScreening.Configure` raises no notification-bearing event. |
| **Cleanup** | Roll back; both rows and their ledger entries disappear. |
| **Evidence** | The two counts (0 then 2) · the `bypass_rls` reading · the null-tenant ledger count of 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 3 is the "elevation trap": an author who elevates and forgets `IgnoreQueryFilters()` sees an empty result and may conclude the data is missing. All eight production sites that need cross-tenant reads declare `IgnoreQueryFilters()` explicitly — verified at `ScheduledSweepService.cs:91,98,105,112,120,129,136` and `KpiSnapshotService.cs:97,105`. |

#### TC-TENANT-INT-009 — The bypass token is an exact lower-case `'on'`; `'ON'`, `'true'` and `'1'` do not elevate  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-002 |
| **Level / Type / Technique** | Integration · Security (negative) · Equivalence Partitioning over truthy-looking tokens, with BVA on the casing boundary of the literal comparison |
| **Priority / Severity / Automation** | High · Critical · Yes (`SkippableTheory` with four `InlineData` rows) |
| **Role / Permission / Tenant** | n/a — the GUCs are driven directly by SQL in this case · n/a — no permission gate · `tenantA` for the row, a foreign tenant for the reader |
| **Environment** | `tests/NT.QAMS.IntegrationTests` with `QMS_ITEST_POSTGRES`; PostgreSQL 17 |
| **Preconditions** | `fx.Available == true`. One `qams.outlier_screening` row seeded for `tenantA` under an elevated context inside the rollback transaction. |
| **Test Data** | Theory rows for `app.bypass_rls`: `"ON"` → expect hidden; `"true"` → expect hidden; `"1"` → expect hidden; `"on"` → expect visible. Reader GUC `app.current_tenant` fixed at `tenantB = Guid.CreateVersion7()` in all four rows. Row ref `"TC-INT-009-A"`. |
| **Steps** | 1. Seed the `tenantA` row under `ctx.Elevate()`. 2. For each theory row: `await db.Database.ExecuteSqlRawAsync("SELECT set_config('app.current_tenant', {0}, true), set_config('app.bypass_rls', {1}, true)", tenantB.ToString(), token);`. 3. `db.OutlierScreenings.IgnoreQueryFilters().CountAsync(s => s.ScreeningRef == "TC-INT-009-A")`. 4. Assert the expected count. 5. Roll back. |
| **Expected UI** | n/a — integration level. |
| **Expected API** | n/a — integration level. |
| **Expected DB** | `"ON"` → **0**; `"true"` → **0**; `"1"` → **0**; `"on"` → **1**. The policy's second disjunct is `current_setting('app.bypass_rls', true) = 'on'`, a case-sensitive literal string equality (`20260726081443_ActivateForcedTenantRls.cs:38,42`); PostgreSQL performs no boolean coercion here. |
| **Expected Audit** | n/a — reads only, no `audit.*` row. |
| **Expected Notification** | n/a — no notification. |
| **Cleanup** | Roll back; reset both GUCs to the nil/`off` pair before the next test by disposing the context. |
| **Evidence** | The four count values, one per theory row · the exact token strings echoed in the assertion messages |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 2 uses transaction-local `set_config(..., true)` deliberately so the forged tokens cannot outlive the test transaction. The application never produces anything but `"on"`/`"off"` (`TenantConnectionInterceptor.cs:56`); this case exists so that a future change to that expression, or a manual `psql` session, cannot quietly widen the predicate. |

#### TC-TENANT-INT-010 — `ProvisionTenantHandler` elevates before the first query and commits the whole seed atomically  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-107 · RSK-TENANT-002 |
| **Level / Type / Technique** | Integration · Functional (positive) · Use Case — elevation site 1, the only one on a request path |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional test against real PostgreSQL, in the shape of `tests/NT.QAMS.WebApi.FunctionalTests/RegulatedFlowRealDatabaseTests.cs:92-197`) |
| **Role / Permission / Tenant** | `PlatformAdmin` (`platform-admin@localhost`) · n/a — the gate is the role string `Roles.PlatformAdmin`, not a permission key (`src/NT.QAMS.WebApi/Controllers/TenantsController.cs:12`) · **no tenant on the request**; the handler elevates instead |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; started via `scripts/dev-up.ps1` |
| **Preconditions** | No `saas.tenant` row exists with `identifier = 'tc-int-010-lab'`. Platform administrator seeded by `StartupSeeding.BootstrapPlatformAdminAsync`. |
| **Test Data** | `POST /api/tenants` body: `{"identifier":"tc-int-010-lab","name":"TC-INT-010 Laboratory","adminEmail":"admin@tc-int-010.test","adminDisplayName":"TC-INT-010 Admin","adminPassword":"Provision-Pass-2026!"}` |
| **Steps** | 1. Sign in as `platform-admin@localhost` / `Dev-Only-Platform-Pass-1!` and capture the access token. 2. `POST /api/tenants` with the body above. 3. Read the status code and the `id` from the response body; call it `T`. 4. `SELECT count(*) FROM saas.tenant WHERE id = T` with `SELECT set_config('app.bypass_rls','on',false)` first. 5. `SELECT count(*) FROM qams.user_account WHERE tenant_id = T`. 6. `SELECT count(*) FROM qams.role WHERE tenant_id = T`. 7. `SELECT count(*) FROM qams.role_permission WHERE tenant_id = T`. 8. `SELECT count(*) FROM qams.lov_entry WHERE tenant_id = T`. 9. `SELECT count(*) FROM qams.outbox_event WHERE tenant_id = T AND event_type LIKE '%TenantProvisioned%'`. |
| **Expected UI** | n/a — this case is driven over HTTP; the SPA platform screens are batch C's `TC-TENANT-A11Y-*` scope. |
| **Expected API** | **201 Created**, body `{"id":"<uuid>"}`. `Location` header is `/api/tenants?id=<uuid>` — the known non-canonical form recorded as **GAP-TENANT-005**; assert the current value, and do not treat the query-string form as correct. |
| **Expected DB** | Step 4 = **1**. Step 5 ≥ **1** (the seeded tenant administrator, `ProvisionTenant.cs:53-58`). Step 6 = **5** — the starter roles from `SystemRoleCatalog.SeedMissingAsync` (`:66`). Step 7 > **0**. Step 8 > **0** — `DefaultLovCatalog.SeedMissingAsync` (`:72`). Step 9 ≥ **1**. All of it lands in the single `SaveChangesAsync` at `ProvisionTenant.cs:74`, which is only possible because the five `saas.tenant` FKs are `DEFERRABLE INITIALLY DEFERRED` (URS-107). |
| **Expected Audit** | `SELECT count(*) FROM audit.field_change WHERE tenant_id = T` > 0 after `set_config('app.bypass_rls','on',false)`; no row for tenant `T` has `tenant_id IS NULL`. |
| **Expected Notification** | The `TenantProvisioned` outbox row drains to `SeedDefaultNotificationRulesPolicy` (`src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:125-131`), after which `SELECT count(*) FROM qams.notification_rule WHERE tenant_id = T` > 0. Poll for up to 30 seconds — the outbox poll interval is 2 seconds (`OutboxProcessor.cs:52`). |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM saas.tenant WHERE identifier = 'tc-int-010-lab';` — the 29 tenant-composite FKs are `ON DELETE CASCADE`, and the five FKs *to* `saas.tenant` are `ON DELETE RESTRICT`, so delete the dependent `qams.user_account`, `qams.outbox_event`, `qams.ref_counter`, `qams.branch` and `read.kpi_snapshot` rows for `T` first. |
| **Evidence** | HTTP response capture including the `Location` header · the six SQL counts · the notification-rule count after the outbox drains |
| **Result / Defect** | Not Run · — |
| **Notes** | The elevation happens at `ProvisionTenant.cs:41`, **before** `TenantSlug.Create` and before the uniqueness pre-check at `:45`, so the very first connection open of the unit of work already carries `app.bypass_rls='on'`. Removing that line would make step 5 onwards fail with `42501`; that inversion belongs to batch B's `TC-TENANT-MUT-*`. |

#### TC-TENANT-INT-011 — An ordinary tenant user cannot reach the request-path elevation: the controller gate refuses first  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-005, URS-008 · RSK-TENANT-004 |
| **Level / Type / Technique** | Integration · Security (negative) · Use Case (misuse case) — the outer of the two independent gates on elevation site 1 |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional test) |
| **Role / Permission / Tenant** | `TenantAdmin` (`admin@demo-lab.local`, the highest tenant-side tier) · n/a — the endpoint is gated by the role string `Roles.PlatformAdmin`, **not** by a permission key · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `demo-lab` exists and is `Active`; `admin@demo-lab.local` is active and holds the `Tenant Administrator` seeded role. No `saas.tenant` row exists with `identifier = 'tc-int-011-lab'`. |
| **Test Data** | `POST /api/tenants` body: `{"identifier":"tc-int-011-lab","name":"TC-INT-011 Laboratory","adminEmail":"admin@tc-int-011.test","adminDisplayName":"TC-INT-011 Admin","adminPassword":"Provision-Pass-2026!"}` |
| **Steps** | 1. Sign in at `POST /api/auth/login` as `admin@demo-lab.local` / `Demo-Admin-Pass-2!` for workspace `demo-lab`; capture the access token. 2. `POST /api/tenants` with that bearer token and the body above. 3. Read the status code, `Content-Type` and body. 4. `SELECT count(*) FROM saas.tenant WHERE identifier = 'tc-int-011-lab'`. 5. Repeat steps 2–4 against the version mirror `POST /api/v1/tenants`. |
| **Expected UI** | n/a — the SPA does not expose a platform tenant-creation screen to a tenant user; this case is driven over HTTP. |
| **Expected API** | **403 Forbidden**, `Content-Type: application/problem+json`, code `AUTHZ-403` written by `src/NT.QAMS.WebApi/Middleware/ProblemAuthorizationResultHandler.cs:16`. The refusal comes from `[Authorize(Roles = Roles.PlatformAdmin)]` at `TenantsController.cs:12`, before MVC model binding reaches `Provision`, so `ProvisionTenantHandler` — and therefore `tenantScope.Elevate()` at `ProvisionTenant.cs:41` — never executes. Step 5 must return the identical status, content type and code. |
| **Expected DB** | Step 4 returns **0** in both step 4 and step 5. No `qams.role`, `qams.lov_entry` or `qams.outbox_event` row is created for any new tenant. |
| **Expected Audit** | No `audit.audit_trail` entry for a `TenantProvisioned` event; assert `SELECT count(*) FROM audit.audit_trail WHERE event_type LIKE '%TenantProvisioned%' AND occurred_at_utc > <test start>` is `0` under `set_config('app.bypass_rls','on',false)`. |
| **Expected Notification** | n/a — no notification is emitted for an authorization refusal. |
| **Cleanup** | n/a — nothing was created; verify with step 4 rather than deleting. |
| **Evidence** | HTTP response capture for both routes (status, `Content-Type`, body) · the `saas.tenant` count of 0 · the audit-trail count of 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | Both the `/api/tenants` and `/api/v1/tenants` forms must be asserted: every route is dual-exposed by `Asp.Versioning.Mvc`, and a gate that applied to only one form would be a real hole. `TenantsController` carries the **only** remaining `[Authorize(Roles=…)]` in `src/` (**GAP-TENANT-010**), so its 403 shape cannot be inferred from the 144 `[RequirePermission]` sites — it must be measured here. |

#### TC-TENANT-INT-012 — Second gate: `AUTHZ-002` is thrown before `Elevate()` is reached  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-005, URS-008 · RSK-TENANT-004 |
| **Level / Type / Technique** | Integration · Security (negative) · Branch coverage of `AuthorizationBehavior` — specifically the `RequireRoleAttribute required => required.Roles.Contains(role)` arm evaluating false |
| **Priority / Severity / Automation** | Critical · Critical · Yes (in-process test resolving `ISender` from the host's service provider with a stubbed `ICurrentUser`) |
| **Role / Permission / Tenant** | `QualityManager` — chosen because it is neither `PlatformAdmin` nor `ExternalAuditor`, so only the `[RequireRole]` arm can refuse · n/a — `ProvisionTenantCommand` declares `[RequireRole(UserRole.PlatformAdmin)]`, the **only** `[RequireRole]` in the solution (`src/NT.QAMS.Application/Tenancy/Commands/ProvisionTenant.cs:17`) · Unresolved on the request |
| **Environment** | `tests/NT.QAMS.WebApi.FunctionalTests` over the composed MediatR pipeline, with `QMS_ITEST_POSTGRES` pointing at `ntqams` so the elevation would be observable if it occurred |
| **Preconditions** | An `ICurrentUser` stub with `IsAuthenticated = true` and `Role = UserRole.QualityManager`. A `CurrentTenant` resolved from the same scope so `IsElevated` can be inspected afterwards. |
| **Test Data** | `new ProvisionTenantCommand("tc-int-012-lab", "TC-INT-012 Laboratory", "admin@tc-int-012.test", "TC-INT-012 Admin", "Provision-Pass-2026!")` |
| **Steps** | 1. Resolve `ICurrentTenant` from the scope and assert `IsElevated == false`. 2. `await sender.Send(command)` and capture the thrown exception. 3. Assert the exception type and its `Code`. 4. Re-read `ICurrentTenant.IsElevated` from the **same** scope. 5. `SELECT count(*) FROM saas.tenant WHERE identifier = 'tc-int-012-lab'`. |
| **Expected UI** | n/a — the case bypasses HTTP deliberately, to prove the second gate holds even if the controller attribute were removed. |
| **Expected API** | n/a — no HTTP call is made; if it were, `AUTHZ-002` maps to **403** via the `AUTHZ-` prefix arm at `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:63`. |
| **Expected DB** | Step 2 throws `NT.QAMS.SharedKernel.Primitives.DomainException`; step 3 asserts `Code == "AUTHZ-002"` and a message of the form `Role 'QualityManager' is not permitted to execute this action.` (`src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs:83-84`). **Step 4 asserts `IsElevated == false`** — the behaviour runs before the handler, so `ProvisionTenant.cs:41` was never reached. Step 5 returns **0**. |
| **Expected Audit** | No `audit.audit_trail` or `audit.field_change` row is written; assert both counts for the interval are `0`. |
| **Expected Notification** | n/a — no notification on an authorization refusal. |
| **Cleanup** | Dispose the scope; nothing persisted. |
| **Evidence** | The exception type and `Code` string · the post-refusal `IsElevated` reading · the `saas.tenant` count of 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 4 is what distinguishes this case from an ordinary authorization test: it proves the *order* of the pipeline, i.e. that a refused command leaves the scope un-elevated rather than elevating and then failing. Together with TC-TENANT-INT-011 it discharges the "reachable from an HTTP request" column of the front matter's §4.3 elevation table for site 1. |

#### TC-TENANT-INT-013 — `OutboxProcessor.ProcessBatchAsync` elevates and chains two tenants' ledger rows in one `SaveChanges`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-002 |
| **Level / Type / Technique** | Integration · Functional (positive) · Use Case — elevation site 6, the highest-volume elevated path |
| **Priority / Severity / Automation** | Critical · High · Yes (`SkippableFact` invoking `internal ProcessBatchAsync` via `InternalsVisibleTo`) |
| **Role / Permission / Tenant** | n/a — a `BackgroundService`, no HTTP actor · n/a — background scopes carry no privileges · Unresolved (elevated), then re-pointed per row |
| **Environment** | `tests/NT.QAMS.IntegrationTests` with `QMS_ITEST_POSTGRES`; PostgreSQL 17; the hosted `OutboxProcessor` **stopped** so the test's manual invocation is the only claimant |
| **Preconditions** | `fx.Available == true`. Two unprocessed `qams.outbox_event` rows exist, one with `tenant_id = tenantA` and one with `tenant_id = tenantB`, both with `processed_at_utc IS NULL`, `dead_lettered_at_utc IS NULL`, `next_attempt_at_utc IS NULL`, `claimed_until_utc IS NULL`. `qams.outbox_event` is outside RLS by accepted deviation B9, so they can be seeded without elevation. |
| **Test Data** | `tenantA`, `tenantB` = two fresh `Guid.CreateVersion7()`; two outbox rows carrying a serialisable domain-event payload each |
| **Steps** | 1. Seed the two outbox rows. 2. Call `ProcessBatchAsync(CancellationToken.None)` once. 3. Read the returned batch count. 4. `SELECT set_config('app.bypass_rls','on',false); SELECT tenant_id, count(*) FROM audit.audit_trail WHERE event_id IN (<the two outbox ids>) GROUP BY tenant_id;`. 5. `SELECT processed_at_utc, claimed_until_utc FROM qams.outbox_event WHERE id IN (<the two ids>)`. 6. Re-read the two rows under a **tenant-A-scoped** unresolved-bypass session and confirm tenant B's `audit_trail` row is invisible. |
| **Expected UI** | n/a — background service, no UI. |
| **Expected API** | n/a — background service, no HTTP. |
| **Expected DB** | Step 3 returns **2**. Step 4 returns exactly two groups, `tenantA → 1` and `tenantB → 1` — both chained in the single `SaveChangesAsync` at `src/NT.QAMS.Infrastructure/Persistence/Outbox/OutboxProcessor.cs:158`, which is only possible because `Elevate()` at `:102` put `app.bypass_rls='on'` on the connection. Step 5 shows both rows with a non-null `processed_at_utc` and `claimed_until_utc IS NULL` (`:128-129`). Step 6 returns **1** row for tenant A and **0** for tenant B, proving the write was cross-tenant but the read is not. |
| **Expected Audit** | Precisely the step-4 assertion — two `audit.audit_trail` entries, each carrying its own row's `tenant_id`, appended by `AuditTrailAppender.AppendAsync(row.TenantId, …)` (`OutboxProcessor.cs:126-127`). Neither entry has `tenant_id IS NULL`. |
| **Expected Notification** | Whatever the two payload event types subscribe — assert only that no exception was thrown and `row.Attempts` stayed `0`; the notification content belongs to module `NOTIF`. |
| **Cleanup** | Delete the two `qams.outbox_event` rows and their `audit.audit_trail` entries under `set_config('app.bypass_rls','on',false)`; `audit.audit_trail` is append-only, so run this case inside the fixture's rollback transaction instead wherever the appender permits it. |
| **Evidence** | The batch count · the grouped `audit_trail` counts by tenant · the processed/claim columns · the tenant-A-scoped visibility count |
| **Result / Defect** | Not Run · — |
| **Notes** | The claim query uses `FOR UPDATE SKIP LOCKED` (`OutboxProcessor.cs:177-193`), so a concurrently running hosted `OutboxProcessor` would silently steal the batch and the test would see `0`. Stop the API before running, or accept that a `0` result is **inconclusive**, not a failure. |

#### TC-TENANT-INT-014 — An outbox policy's `Set` inside the elevated scope produces the Scoped + Elevated GUC pair  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 — no URS describes the composite state; traced to source · RSK-TENANT-002 |
| **Level / Type / Technique** | Integration · Functional (characterisation) · Data Flow — the `Elevate()` definition at `OutboxProcessor.cs:102` and the `Set(e.TenantId)` definition inside the handler, both reaching the same use at `TenantConnectionInterceptor.cs:55-56` |
| **Priority / Severity / Automation** | High · High · Yes (`SkippableFact`) |
| **Role / Permission / Tenant** | n/a — background scope, no HTTP actor · n/a — no permission gate · Unresolved (elevated) → `tenantA` (still elevated) |
| **Environment** | `tests/NT.QAMS.IntegrationTests` with `QMS_ITEST_POSTGRES`; PostgreSQL 17; hosted `OutboxProcessor` stopped |
| **Preconditions** | `fx.Available == true`. One unprocessed `qams.outbox_event` row carrying a `TenantProvisioned` payload for `tenantA`, so `SeedDefaultNotificationRulesPolicy` is the handler that runs (`src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:125-131`). |
| **Test Data** | `tenantA = Guid.CreateVersion7()`; payload `{"TenantId":"<tenantA>","Slug":"tc-int-014-lab","Name":"TC-INT-014 Laboratory"}`, `event_type` set to the `TenantProvisioned` assembly-qualified name the deserialiser expects (`OutboxProcessor.cs:286-294`) |
| **Steps** | 1. Seed the outbox row. 2. Instrument: resolve `ICurrentTenant` from the same scope the processor uses, or add a probe handler registered after `SeedDefaultNotificationRulesPolicy`. 3. Call `ProcessBatchAsync`. 4. Immediately after the policy has run and before the scope is disposed, read `current_setting('app.current_tenant', true)` and `current_setting('app.bypass_rls', true)` on a **newly opened** connection from that scope. 5. Read `ICurrentTenant.TenantId` and `ICurrentTenant.IsElevated`. |
| **Expected UI** | n/a — background service, no UI. |
| **Expected API** | n/a — background service, no HTTP. |
| **Expected DB** | Step 4 returns `tenantA.ToString()` **and** `on` — the Scoped + Elevated pair. Step 5 returns `TenantId == tenantA` and `IsElevated == true`, because `Set` does not reset the flag (`src/NT.QAMS.Infrastructure/Services/RequestContext.cs:18`). |
| **Expected Audit** | The policy's own writes carry `tenant_id = tenantA` in `audit.field_change`; assert `SELECT count(*) FROM audit.field_change WHERE tenant_id IS NULL AND occurred_at_utc > <test start>` is `0`. |
| **Expected Notification** | `SELECT count(*) FROM qams.notification_rule WHERE tenant_id = tenantA` > 0 after the batch, since the policy seeds the default rules. |
| **Cleanup** | Delete the seeded outbox row and the `qams.notification_rule` rows for `tenantA` under `set_config('app.bypass_rls','on',false)`. |
| **Evidence** | The two `current_setting` values · the `ICurrentTenant` property pair · the notification-rule count |
| **Result / Defect** | Not Run · — |
| **Notes** | **New finding — GAP-TENANT-901.** The front matter's §3.3 records Scoped + Elevated as "reachable in principle … **no production path does this** — only `tests/.../SecurityEventRlsTests.cs` style fixtures". Measured 2026-08-01, **thirteen** `INotificationHandler` implementations call `tenantSetter.Set(...)` and `DomainEventNotification<T>` is published from exactly one place — `OutboxProcessor.cs:123`, inside the elevated scope — so this composite state occurs on **every** outbox delivery that routes to a tenant-setting policy. The state is benign here (bypass already permits every row), but the documentation is wrong and any reasoning built on "this never happens" is unsound. |

#### TC-TENANT-INT-015 — `ScheduledSweepService.RunSweepAsync` elevates before the first query and sweeps every tenant  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-002 |
| **Level / Type / Technique** | Integration · Functional (positive) · Use Case — elevation site 5, with a Loop-coverage element over the eight `IgnoreQueryFilters()` reads |
| **Priority / Severity / Automation** | High · High · Yes (`SkippableFact` invoking `internal RunSweepAsync`) |
| **Role / Permission / Tenant** | n/a — a `BackgroundService`, no HTTP actor · n/a — no permission gate on a hosted service · Unresolved (elevated) |
| **Environment** | `tests/NT.QAMS.IntegrationTests` with `QMS_ITEST_POSTGRES`; PostgreSQL 17; the hosted `ScheduledSweepService` stopped so the advisory lock `AdvisoryLockKeys.ComplianceSweep` is free |
| **Preconditions** | `fx.Available == true`. Two `qams.equipment_item` rows, one for `tenantA` and one for `tenantB`, both `status = 'Active'` with `next_calibration_due <= today`. Seeded under an elevated context. |
| **Test Data** | `tenantA`, `tenantB` = two fresh `Guid.CreateVersion7()`; both equipment rows given `next_calibration_due = <today − 1 day>` against the injected `TestClock` (`tests/NT.QAMS.IntegrationTests/TestContext.cs:36`, `2026-07-26T12:00:00Z`) |
| **Steps** | 1. Seed the two equipment rows. 2. Call `RunSweepAsync(CancellationToken.None)` and capture the returned tuple `(Due, Locked, Expired, Suspended)`. 3. Under `set_config('app.bypass_rls','on',false)`, `SELECT tenant_id, status FROM qams.equipment_item WHERE id IN (<the two ids>)`. 4. Assert both rows changed. 5. Read `current_setting('app.bypass_rls', true)` from the sweep's own scope during step 2 (probe) to confirm it was `on`. |
| **Expected UI** | n/a — background service, no UI. |
| **Expected API** | n/a — background service, no HTTP. |
| **Expected DB** | Step 2's `Due` component is **≥ 2**. Step 3 shows **both** rows with `status = 'NeedsCalibration'` — one per tenant, so the sweep crossed the tenant boundary, which is only possible with `Elevate()` at `src/NT.QAMS.Infrastructure/Jobs/ScheduledSweepService.cs:64` **and** `.IgnoreQueryFilters()` at `:91`. Step 5 reads `on`. |
| **Expected Audit** | Each transition writes an `audit.field_change` row stamped with its **own** row's tenant; assert two rows, `tenant_id` values equal to `tenantA` and `tenantB`, and **zero** rows with `tenant_id IS NULL`. |
| **Expected Notification** | `CalibrationDue` outbox rows are enqueued, one per tenant; assert `SELECT count(*) FROM qams.outbox_event WHERE tenant_id IN (tenantA, tenantB) AND event_type LIKE '%CalibrationDue%'` is `2`. Their delivery is `NOTIF`'s scope. |
| **Cleanup** | Delete the two equipment rows, their `audit.field_change` entries and the two outbox rows under `set_config('app.bypass_rls','on',false)`; prefer running the whole case inside the fixture's rollback transaction. |
| **Evidence** | The returned sweep tuple · the two status values with their tenant ids · the `bypass_rls` probe reading · the outbox row count |
| **Result / Defect** | Not Run · — |
| **Notes** | Elevation is called at `:64`, before `AppDbContext` is resolved at `:65` and before `AdvisoryLock.TryRunExclusiveAsync` opens the connection at `:70` — the ordering TC-TENANT-INT-007 shows to be mandatory. Separately, this service applies **no** tenant-status filter (`GAP-TENANT-011`); do not extend this case to a suspended tenant, because no supported operation can suspend one (`GAP-TENANT-002`). |

#### TC-TENANT-INT-016 — `KpiSnapshotService.SnapshotAllTenantsAsync` elevates and writes one snapshot per **Active** tenant  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-002 |
| **Level / Type / Technique** | Integration · Functional (positive) · Decision Table — tenant `Status` ∈ {`Active`, `Suspended`, `Terminated`} × "snapshot written?", exercising the `Active` rule and the negative rules by construction |
| **Priority / Severity / Automation** | High · Medium · Yes (`SkippableFact` invoking `internal SnapshotAllTenantsAsync`) |
| **Role / Permission / Tenant** | n/a — a `BackgroundService`, no HTTP actor · n/a — no permission gate · Unresolved (elevated) |
| **Environment** | `tests/NT.QAMS.IntegrationTests` with `QMS_ITEST_POSTGRES`; PostgreSQL 17; hosted `KpiSnapshotService` stopped so `AdvisoryLockKeys.KpiSnapshot` is free |
| **Preconditions** | `fx.Available == true`. Two `saas.tenant` rows: `tenantA` with `status = 'Active'`, `tenantS` with `status = 'Suspended'`. `saas.tenant` carries **no** RLS (`relrowsecurity = f`), so the rows can be inserted directly; the `ck_tenant_status_domain` CHECK accepts both literals. |
| **Test Data** | `tenantA` (`identifier = 'tc-int-016-active'`, `status = 'Active'`), `tenantS` (`identifier = 'tc-int-016-susp'`, `status = 'Suspended'`); `today` derived from the injected `TestClock` value `2026-07-26T12:00:00Z` → `2026-07-26` |
| **Steps** | 1. Insert the two `saas.tenant` rows. 2. Call `SnapshotAllTenantsAsync(CancellationToken.None)` and capture the returned tenant count. 3. Under `set_config('app.bypass_rls','on',false)`, `SELECT tenant_id, date FROM read.kpi_snapshot WHERE tenant_id IN (tenantA, tenantS) AND date = DATE '2026-07-26'`. 4. Call `SnapshotAllTenantsAsync` a second time and re-run step 3. |
| **Expected UI** | n/a — background service, no UI. |
| **Expected API** | n/a — background service, no HTTP. |
| **Expected DB** | Step 3 returns **exactly one** row, for `tenantA` — `SnapshotAsync` filters `t.Status == TenantStatus.Active` (`src/NT.QAMS.Infrastructure/Jobs/KpiSnapshotService.cs:90`), so `tenantS` gets nothing. Writing into `read.kpi_snapshot`, a FORCE-RLS table with the strict predicate, is only possible because of `Elevate()` at `:63`. Step 4 still returns exactly one row — the upsert is idempotent (`:96-103`), it updates today's row in place rather than inserting a duplicate. |
| **Expected Audit** | `read.kpi_snapshot` is a projection; assert `SELECT count(*) FROM audit.field_change WHERE entity_type = 'KpiSnapshot'` is unchanged across the run, and in particular that no null-tenant row appears. |
| **Expected Notification** | n/a — the snapshot service raises no domain event and enqueues no outbox row. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM read.kpi_snapshot WHERE tenant_id IN (tenantA, tenantS); DELETE FROM saas.tenant WHERE identifier IN ('tc-int-016-active','tc-int-016-susp');` — delete the snapshot rows first, since the FK to `saas.tenant` is `ON DELETE RESTRICT`. |
| **Evidence** | The returned tenant count · the step-3 and step-4 result sets · the unchanged field-change count |
| **Result / Defect** | Not Run · — |
| **Notes** | This case is also the *contrast* evidence for **GAP-TENANT-011**: two elevated background services read across all tenants, and only this one filters by status. Note that `db.Tenants` at `:89` needs no `IgnoreQueryFilters()` because `Tenant` is not `ITenantScoped` and carries no query filter (`src/NT.QAMS.Domain/Tenancy/Tenant.cs:11`). |

#### TC-TENANT-INT-017 — Startup backfills: both elevate, but only one elevates before resolving the `DbContext`  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 — no URS covers startup backfill ordering; traced to source · RSK-TENANT-002, RSK-TENANT-004 |
| **Level / Type / Technique** | Integration · Functional (positive) + Structural · Path coverage — the two distinct code paths through `StartupSeeding` that reach `Elevate()`, plus a static ordering assertion on each |
| **Priority / Severity / Automation** | High · High · Yes (`SkippableFact` in `tests/NT.QAMS.WebApi.FunctionalTests`, alongside the existing `StartupSeedingResilienceTests.cs`) |
| **Role / Permission / Tenant** | n/a — host-startup scopes, no HTTP actor · n/a — startup seeding is not permission-gated · Unresolved (elevated) |
| **Environment** | `tests/NT.QAMS.WebApi.FunctionalTests` with `QMS_ITEST_POSTGRES=Host=localhost;Database=ntqams;Username=qams_app;Password=dev-only-local`; PostgreSQL 17; the API **not** running, so the two backfills are invoked directly through `StartupSeeding.RunAsync` |
| **Preconditions** | `fx.Available == true`. At least two `saas.tenant` rows exist, one of them missing at least one `DefaultLovCatalog` category and holding at least one `qams.user_account` with `role_id IS NULL` and `tenant_id IS NOT NULL`. |
| **Test Data** | `tenantA`, `tenantB` = two existing tenant ids; one account per tenant with `role_id = NULL` |
| **Steps** | 1. `await StartupSeeding.RunAsync(services, configuration, CancellationToken.None);`. 2. Under `set_config('app.bypass_rls','on',false)`, `SELECT tenant_id, count(*) FROM qams.lov_entry WHERE tenant_id IN (tenantA, tenantB) GROUP BY tenant_id`. 3. `SELECT tenant_id, count(*) FROM qams.role WHERE tenant_id IN (tenantA, tenantB) GROUP BY tenant_id`. 4. `SELECT count(*) FROM qams.user_account WHERE tenant_id IN (tenantA, tenantB) AND role_id IS NULL`. 5. Static ordering assertion: read `src/NT.QAMS.WebApi/Startup/StartupSeeding.cs` and assert that in `BackfillStarterListOfValuesAsync` the `Elevate()` line precedes the `GetRequiredService<AppDbContext>()` line, and record that in `BackfillRolesAndAssignmentsAsync` it does **not**. 6. Re-run step 1 and confirm every count is unchanged (idempotence). |
| **Expected UI** | n/a — host startup, no UI. |
| **Expected API** | n/a — host startup, no HTTP. |
| **Expected DB** | Step 2 returns a positive count for **both** tenants — cross-tenant writes into the FORCE-RLS `qams.lov_entry`, possible only under `Elevate()` at `StartupSeeding.cs:99`. Step 3 returns **5** for each tenant (`SystemRoleCatalog.SeedMissingAsync` via `:139`). Step 4 returns **0** — every previously unassigned account now carries a `role_id` (`:160-176`). Step 5: the LOV backfill has `Elevate()` at `:99` and resolves `AppDbContext` at `:100` (correct order); the roles backfill resolves `AppDbContext` at `:131` and calls `Elevate()` at `:132` (**inverted**). Step 6: all counts identical to the first run. |
| **Expected Audit** | Every seeded `qams.role`, `qams.role_permission` and `qams.lov_entry` row produces an `audit.field_change` entry carrying its own tenant; assert `SELECT count(*) FROM audit.field_change WHERE tenant_id IS NULL AND occurred_at_utc > <test start>` is `0`. |
| **Expected Notification** | n/a — the backfills raise no domain event. |
| **Cleanup** | The backfills are additive and idempotent by design; leave the seeded rows in place and record the pre/post counts as the evidence rather than deleting them. If a scratch database is used, drop it. |
| **Evidence** | The four SQL result sets · the two source-line orderings from step 5 · the identical counts from the idempotence re-run |
| **Result / Defect** | Not Run · — |
| **Notes** | **New finding — GAP-TENANT-903.** The inverted ordering at `StartupSeeding.cs:131-132` is currently harmless: resolving `AppDbContext` from a DI scope constructs the context but opens no connection, so the first open still happens after `Elevate()`. It is nonetheless a latent trap — inserting any query, `Database.CanConnectAsync()` or `Database.BeginTransaction()` between those two lines would open a connection with `app.bypass_rls='off'` and the whole backfill would silently see and write nothing for other tenants. Seven of the eight call sites use the safe order; this one does not. Note also that `BootstrapPlatformAdminAsync` (`:65-87`) writes a null-tenant `qams.user_account` row with **no** elevation at all, which succeeds only because `user_account` is outside RLS under accepted deviation **B9** — an isolation property that is invisible from the code and worth stating in the seeding doc comment. |

---

## Batch coverage note

**Covered.** The GUC mechanism end to end. All four rules of the interceptor's `(TenantId, IsElevated)` decision table are pinned at unit level with the exact emitted SQL text, the exact nil literal `00000000-0000-0000-0000-000000000000`, the exact `'on'`/`'off'` tokens and the `is_local = false` session-scope argument (`TC-TENANT-UNIT-007…011`). The full `CurrentTenant` state machine is covered — initial state, `Set`, unguarded re-pointing, `Elevate`, the composite Scoped + Elevated state, `Clear` as the only exit, and the reflected absence of any `Demote` (`TC-TENANT-UNIT-001…006`). The interceptor's override surface is enumerated, establishing that re-stamping at open is the *sole* pooled-reuse guarantee (`TC-TENANT-UNIT-012`), and that guarantee is then measured at the database with a forced single-connection pool and a `pg_backend_pid()` equality assertion, in both the tenant→tenant and tenant→unresolved directions (`TC-TENANT-INT-003`, `TC-TENANT-INT-004`). Fail-closed is proven at **both** layers separately (`TC-TENANT-INT-002`); the elevation trap — elevation without `IgnoreQueryFilters()` returning nothing — is proven (`TC-TENANT-INT-008`); the `'on'` literal is shown to be case-sensitive and non-coercing against `'ON'`, `'true'` and `'1'` (`TC-TENANT-INT-009`); and both illegal orderings (`Set` after open, `Elevate` after open) are pinned, the first landing on SqlState `42501` (`TC-TENANT-INT-006`, `TC-TENANT-INT-007`). All six elevated components are covered: `ProvisionTenantHandler` (`INT-010`), `OutboxProcessor` (`INT-013`, `INT-014`), `ScheduledSweepService` (`INT-015`), `KpiSnapshotService` (`INT-016`), and both `StartupSeeding` backfills (`INT-017`). The proof that an ordinary HTTP request cannot reach elevation is carried by two independent cases against the two independent gates on the only request-path elevation — the controller role gate returning `403 AUTHZ-403` on both the `/api/tenants` and `/api/v1/tenants` forms (`INT-011`), and the command-level `[RequireRole]` throwing `AUTHZ-002` with `IsElevated` still `false` afterwards (`INT-012`).

**In scope but not covered, and why.**
1. **An enumerating control over the `Elevate()` call sites.** Written as `TC-TENANT-UNIT-013` and labelled `[GD]` on **GAP-TENANT-014** with implementable acceptance criteria, because no such test exists in `tests/NT.QAMS.Architecture.Tests/` (measured: the project holds only `CommandPolicyTests.cs`, `LayerRulesTests.cs`, `ModuleBoundaryTests.cs`, `UserAccountTenantBoundTests.cs`, none of which mentions `Elevate`). Until it exists, "all eight elevation paths are covered" is a point-in-time claim, not a verified one.
2. **The three `OutboxProcessor` sites are covered as two, not three.** `ProcessBatchAsync` (`:102`) is covered by `INT-013`. `RefreshQueueStatsAsync` (`:225`) and `RunRetentionPurgeAsync` (`:246`) are **not** given their own cases: both operate exclusively on `qams.outbox_event` and `qams.idempotency_record`, and `outbox_event` is outside RLS under accepted deviation **B9**, so their `Elevate()` calls have no observable RLS effect to assert. Asserting "elevation that does nothing" would be a case with no failure mode. Recorded here rather than padded.
3. **HTTP mapping of an RLS refusal.** `TC-TENANT-INT-006` asserts SqlState `42501` at the database and deliberately does **not** assert an HTTP status, because `DomainExceptionHandler` has no `PostgresException` arm and the refusal surfaces as an untyped 500 (**GAP-TENANT-006**). Asserting `500` would pin a defect as expected behaviour.
4. **Elevation under a suspended tenant.** Not authored at all: no supported operation can suspend a tenant (**GAP-TENANT-002**), so any such case would be `[GD]` on two gaps and executable only via an unsupported `UPDATE saas.tenant`.
5. **Production-grade least privilege.** Every integration case here runs in dev, where `qams_app` is the schema **owner**. `RealPostgresFixture` refuses only `SUPERUSER`/`BYPASSRLS`, not ownership, and the documented remedy — `deploy/harden-runtime-role.sql` — cannot execute (**GAP-TENANT-008**, severity Critical). A green run of this batch therefore does not by itself qualify the isolation claim for a role-split production installation. Stated plainly so it is not read as more assurance than it is.
6. **ID-block divergence, resolved in favour of the assignment.** The front matter's reservation table assigns `TC-TENANT-INT-001…030` to **batch B** and `TC-TENANT-UNIT-001…030` to batch A. This batch's brief reserved `TC-TENANT-INT-001…` and `TC-TENANT-UNIT-001…` to batch A. The brief was followed. Batch B must therefore start its integration sequence at **`TC-TENANT-INT-018`**, and the reservation table needs correcting — raised as **GAP-TENANT-904**.

**New gaps found by this batch.**

| Gap | Statement | Severity | Evidence |
|---|---|---|---|
| **GAP-TENANT-901** | The **Scoped + Elevated** tenant-context state is a routine production state, not the theoretical one the front matter's §3.3 describes ("no production path does this"). `DomainEventNotification<T>` is published from exactly one place — `src/NT.QAMS.Infrastructure/Persistence/Outbox/OutboxProcessor.cs:123`, inside the scope elevated at `:102` — and **thirteen** `INotificationHandler` implementations call `tenantSetter.Set(...)` inside it (`NotificationPolicies.cs:131`, `Sla/SlaSlice.cs:130,154`, `Sla/EscalationTriggeredPolicy.cs:21`, `Improvement/ComplaintToNcPolicy.cs:26`, `AnalyticalQuality/PtToNcPolicy.cs:26`, `AuditManagement/Policies/FindingToNcPolicy.cs:37`, `Competency/CompetencyLapseAuthorizationPolicy.cs:27,46`, `ComplianceLedger/AuditTrailReviewSlice.cs:86`, `DocumentControl/DocumentReviewDuePolicy.cs:32`, `Equipment/IntermediateCheckToNcPolicy.cs:27`, `Facility/ExcursionToNcPolicy.cs:28`, `Notifications/NotificationDispatcher.cs:33`). Because `Set` does not reset `IsElevated` (`RequestContext.cs:18`), every such delivery stamps `(tenant, 'on')`. **Acceptance criteria:** (1) §3.3 of `12-module-tenancy-rls.md` is corrected to record Scoped + Elevated as a production state with the outbox as its source; (2) a documented statement of whether a policy handler is *expected* to run elevated, or whether `OutboxProcessor` should demote per row; (3) if demotion is intended, `ICurrentTenantSetter` gains a scoped `Demote()`/`using`-disposable and the processor uses it, with a test asserting `bypass_rls='off'` while a policy runs. | **Medium** | `OutboxProcessor.cs:102,123`; `RequestContext.cs:18,26`; the thirteen `Set` sites above; `TC-TENANT-UNIT-005`, `TC-TENANT-INT-014` |
| **GAP-TENANT-902** | The elevation *surface* is far wider than the eight call sites the front matter enumerates: `ICurrentTenantSetter` — the interface that carries `Elevate()` — is constructor-injected into **fifteen** `NT.QAMS.Application` types, including `LoginHandler` (`IdentityAccess/Commands/Login.cs:34`), `ChangePasswordHandler` (`:182`) and twelve event policies, every one of which runs on a request or outbox path and every one of which therefore holds a live handle to the RLS off-switch. None calls it today (verified: `grep -rn "Elevate()" src/` returns 8 call sites). Counting call sites measures today; counting injection sites measures how easily tomorrow changes. **Acceptance criteria:** (1) split the interface so ordinary handlers receive a `Set`/`Clear`-only abstraction and `Elevate()` lives on a separate `ICrossTenantScope` registered for hosted services and `ProvisionTenantHandler` only; (2) an architecture test asserts no `NT.QAMS.Application` type other than the allow-listed one depends on the elevating abstraction; (3) the GAP-TENANT-014 allow-list test is written against the narrowed interface so both the sites and the surface are capped. | **High** | `src/NT.QAMS.Application/Abstractions/ICurrentTenant.cs:23-35`; the 15 injection sites; `src/NT.QAMS.Infrastructure/DependencyInjection.cs:23` (scoped, resolvable anywhere) |
| **GAP-TENANT-903** | `StartupSeeding.BackfillRolesAndAssignmentsAsync` resolves `AppDbContext` at `src/NT.QAMS.WebApi/Startup/StartupSeeding.cs:131` and calls `Elevate()` at `:132` — the inverse of the order used at the other seven sites. Harmless today because DI resolution opens no connection, but a single added query, `CanConnectAsync()` or `BeginTransaction()` between those lines would open the connection with `app.bypass_rls='off'`, and the cross-tenant backfill would then read and write **nothing** for other tenants, silently and without error. **Acceptance criteria:** (1) the two lines are swapped so `Elevate()` precedes the resolve; (2) a comment states that elevation must precede the first connection open, citing the session-GUC timing; (3) the architecture test from GAP-TENANT-014 additionally asserts, per allow-listed site, that the `Elevate()` line number is lower than the first `GetRequiredService<AppDbContext>` line number in the same method. | **Low** (latent; **Medium** if any database call is added between the two lines) | `StartupSeeding.cs:99-100` (correct order) vs `:131-132` (inverted); timing property proven by `TC-TENANT-INT-007` |
| **GAP-TENANT-904** | The front matter's ID-reservation table (`12-module-tenancy-rls.md:23-24`) assigns `TC-TENANT-INT-001…030` and `TC-TENANT-RLS-*` to batch B, but this batch's authoring brief reserved `TC-TENANT-INT-001…` to batch A, which is what was written. Left uncorrected, batch B will mint colliding `TC-TENANT-INT-0nn` ids and corrupt the traceability matrix. **Acceptance criteria:** (1) the reservation table is amended to record `TC-TENANT-INT-001…017` as consumed by batch A and `TC-TENANT-INT-018…030` as batch B's range; (2) `TC-TENANT-UNIT-001…013` is recorded as consumed, leaving `014…030` free; (3) the traceability matrix (`24-traceability-matrix.md`) is built from the case files, not from the reservation table, so a reservation error can never again masquerade as coverage. | **Medium** (traceability integrity) | `docs/testing/12-module-tenancy-rls.md:13-24` vs the ids consumed in this file |

*Nothing in this file was executed. Every `Result / Defect` cell reads `Not Run · —`.*
