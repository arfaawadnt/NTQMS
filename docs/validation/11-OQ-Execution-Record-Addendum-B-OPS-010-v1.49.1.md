# OQ Execution Record — Addendum B: OPS-010 remediation & re-test

| Field | Value |
| ----- | ----- |
| Document ID | OQ-EXEC-NTQMS-001-B |
| Parent records | [`09-…`](09-OQ-Execution-Record-v1.49.md) (OQ-EXEC-NTQMS-001), [`10-…`](10-OQ-Execution-Record-Addendum-A-v1.49.md) (Addendum A, which raised OPS-010) |
| Finding addressed | **OPS-010** — cold start with PostgreSQL unreachable crashed instead of reporting unready |
| System / version | NT.QMS **v1.49.1** (fix release) |
| Environment | Development workstation; PostgreSQL 17 local; instances on `:5094`–`:5098` per case |
| Executed by (operator) | Engineering (Claude Code), at the System Owner's direction |
| Witnessed by | A. Awad — System Owner / acting QA authority |
| Date | 2026-07-29, 08:15–08:40 local |
| Outcome | **OPS-010 REMEDIATED and re-tested — closed.** Full backend suite green (374 tests, 0 skipped). One documented residual caveat (§4). |

---

## 1. Change made

| Item | Detail |
| ---- | ------ |
| New | `src/NT.QAMS.WebApi/Startup/StartupSeeding.cs` — the two idempotent startup steps (platform-admin bootstrap, starter LOV backfill) extracted from `Program.cs`. `TryRunAsync` completes normally on a healthy boot and **defers** when the database is unreachable. |
| New | `src/NT.QAMS.WebApi/Startup/DeferredStartupSeeder.cs` — `BackgroundService` that retries the deferred seeding every 15 s after the host is listening, then stops. Never faults the host. |
| New | `src/NT.QAMS.WebApi/Startup/StartupSeedingState.cs` — singleton flag so the retry is a no-op on a healthy boot. |
| Changed | `Program.cs` — the two fatal inline blocks replaced by one `TryRunAsync` call; `StartupSeedingState` + `DeferredStartupSeeder` registered. |
| Changed | `NT.QAMS.WebApi.csproj` — `InternalsVisibleTo` for the functional-test project (matching the existing Infrastructure convention). |
| New tests | `tests/NT.QAMS.WebApi.FunctionalTests/StartupSeedingResilienceTests.cs` — 6 cases. |

**Design rationale (why not a pure hosted-service move).** The functional-test host strips
every `IHostedService` and relies on the platform admin existing before the first request, so
moving seeding entirely into a background service would have made the suite race. Attempting
inline first preserves the deterministic pre-listen ordering on a healthy boot; deferral is
used **only** when the database is unreachable. Connectivity failures defer; anything else
still propagates, so a genuinely broken seed continues to fail loudly.

---

## 2. Re-test of the failing scenario (the OPS-010 acceptance criterion)

**Procedure.** Cold start with `ConnectionStrings__Postgres` pointed at `localhost:5433`
(nothing listening), `Database__MigrateOnStartup=false`.

| Check | Before the fix (Addendum A) | **After the fix — actual observed** | P/F |
| ----- | --------------------------- | ----------------------------------- | --- |
| Process starts and listens | **No** — unhandled `NpgsqlException`, exited | **Yes** — "HOST STARTED and is serving" | **Pass** |
| `GET /health/live` | unreachable (`000`) | **`200` / body `Healthy`** | **Pass** |
| `GET /health/ready` | unreachable (`000`) | **`503` / body `Unhealthy`** | **Pass** |
| Operator diagnosis available | crash log only | log: *"Startup data seeding deferred: the database is not answering yet. The application is starting anyway so /health/ready reports the outage (OPS-010); seeding retries automatically."* | **Pass** |

## 3. Recovery path (new case — OQ-DEP-02c)

**Procedure.** Start with the database unreachable, then make it reachable while the process
runs (TCP bridge opened to the live PostgreSQL), and observe whether the deferred work
completes without a restart.

| Step | **Actual observed** | P/F |
| ---- | ------------------- | --- |
| Host starts with DB down | up, `/health/ready` = **503** | **Pass** |
| Database becomes reachable | `/health/ready` flips to **200** with no restart | **Pass** |
| Deferred seeding completes | log: *"Deferred startup data seeding completed — the database became available."* | **Pass** |
| The work actually happened (not just logged) | DB query confirms the bootstrap user was created by the deferred run: `ops010-recovery@test.local | role=PlatformAdmin | tenant=(platform)` | **Pass** |

Test artifact `ops010-recovery@test.local` was deleted after verification (`DELETE 1`).

## 4. Residual caveat — stated explicitly, not fixed

**`Database:MigrateOnStartup=true` still fails fast when the database is unreachable.**
Re-executed and confirmed: the process exits with `NpgsqlException` at
`NpgsqlMigrator.MigrateAsync`.

This is **deliberate and left unchanged**: applying schema migrations is a deploy gate, and
starting an instance that might later serve traffic against an unmigrated or half-migrated
schema is a worse failure than a crash-loop. The code comment already directs pipeline
deployments to the idempotent SQL script in `deploy/` and keep the flag off — which is the
production configuration, and the configuration under which the OPS-010 fix applies.
**Operators must therefore not enable `MigrateOnStartup` in an environment where the
readiness-reports-outage behaviour is required.**

## 5. Regression tests added (executed, all passed)

| Test | Result |
| ---- | ------ |
| `An_unreachable_database_defers_the_seeding_instead_of_throwing` | **Passed** (3 s) |
| `Seeding_completes_and_is_idempotent_when_the_database_answers` | **Passed** (1 s) |
| `A_real_seed_fault_is_not_swallowed` | **Passed** |
| `Connectivity_failures_are_classified_as_database_unavailable` (NpgsqlException) | **Passed** |
| `Connectivity_failures_are_classified_as_database_unavailable` (TimeoutException) | **Passed** |
| `Connectivity_failures_are_classified_as_database_unavailable` (EF-wrapped inner) | **Passed** |

*Operator note (transparency): the idempotency test failed on its first run because of a
defect in the **test**, not the product — `Guid.NewGuid()` inside the EF options lambda gave
each scope a different in-memory store. Corrected and re-run green.*

## 6. Full regression suite (executed after the fix)

| Project | Result |
| ------- | ------ |
| `NT.QAMS.Domain.UnitTests` | **211 passed**, 0 failed, 0 skipped |
| `NT.QAMS.Application.UnitTests` | **57 passed**, 0 failed, 0 skipped |
| `NT.QAMS.Architecture.Tests` | **24 passed**, 0 failed, 0 skipped |
| `NT.QAMS.IntegrationTests` | **18 passed**, 0 failed, 0 skipped |
| `NT.QAMS.WebApi.FunctionalTests` | **64 passed**, 0 failed, 0 skipped |
| **Total** | **374 passed / 0 failed / 0 skipped** (was 368 — +6 regression tests) |

Build: `Build succeeded. 0 Warning(s)`. Module-boundary, command-policy, and API-surface
snapshot merge gates all still pass (no public API change).

---

## 7. Disposition

**OPS-010: CLOSED — remediated and re-tested**, with the §4 caveat recorded as an
operational constraint rather than an open defect. Addendum A's finding table should be read
together with this document.

| Role | Name | Signature | Date |
| ---- | ---- | --------- | ---- |
| Executed by (operator) | Engineering — Claude Code (automated operator) | *n/a — machine-executed; results transcribed verbatim* | 2026-07-29 |
| Witnessed by | A. Awad (System Owner / acting QA authority) | ____________________ | __________ |
| Reviewed & approved by (QA) | | ____________________ | __________ |

> Environment limits from the parent records still apply: this remains development-environment
> execution with limited independence, and Engineering applies no signature on QA's behalf.
