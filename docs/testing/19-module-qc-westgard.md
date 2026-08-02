# 19 — Quality Control, Westgard Multi-Rule Evaluation, Levey-Jennings, QC Profiles and Targets

**Module code:** `QC`
**System under test:** NT.QMS **v1.51.2** (repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master`), inspected 2026-08-01.
**Binding conventions:** `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md` — the as-built stack table (§1), the verified functional facts (§2), the 28-field case format (§4), the evidence labels `[IV]`/`[RNV]`/`[ID]`/`[GD]` (§4), the ID convention (§5), the honesty rules (§6) and the canonical detailed-case block (§8) govern every case authored against this module. Entries marked *[corrected 2026-08-01]* in that file supersede anything older.

**This file is FRONT MATTER ONLY.** Per the split convention (conventions §7) it carries the implementation inventory, divergence table, state/decision matrices, UAT scenarios, exploratory charters and the module gap register. It deliberately contains **no `## 5. Detailed test cases` section** — detailed cases are authored into `19-module-qc-westgard-cases-<A|B|C…>.md` by separate passes, each owning a disjoint slice of scope and one of the reserved ID blocks below.

---

## ID reservation table

Reservations are generous on purpose; unconsumed ids are a coverage hole to be reported, never renumbered. Batch letters are indicative — the case-authoring pass that claims a block records its own letter.

| Reserved range | Scope owned | Indicative batch | Notes |
|---|---|---|---|
| `TC-QC-WESTGARD-001` … `060` | The five implemented rules in `WestgardEvaluator.Evaluate` — firing, non-firing, label derivation, multi-rule combination, ordering of the returned `ViolatedRules` list | A | Largest block: this is the algorithmic core |
| `TC-QC-BVA-001` … `030` | Boundary values on every limit comparison (`>` is strict everywhere), on `RunLength - 1` window sufficiency, on `sd` approaching zero, on decimal precision `(18,6)` / `(10,3)` | A | Pairs 1:1 with the WESTGARD block |
| `TC-QC-EP-001` … `020` | Equivalence partitions over z-space (in-band / warning-band / reject-band / beyond-range) and over prior-window length (0, 1, 2…8, 9, 10, 12, >12) | A | |
| `TC-QC-DT-001` … `025` | Decision-table cases derived from §4 tables DT-QC-1 (rule combination) and DT-QC-2 (limits validation) | B | One case per rule-column of the decision table |
| `TC-QC-MCDC-001` … `015` | MC/DC over the compound predicates of 2-2s (three conjuncts) and 10-x (window-length + all-positive/all-negative) | B | |
| `TC-QC-UNIT-001` … `030` | `QcProfile.Create` / `UpdateTargets` / `Deactivate` and `QcRun.Record` / `LogTroubleshooting` invariants and error codes | B | Domain-level, no persistence |
| `TC-QC-STATE-001` … `015` | The `QcRun.Outcome` state machine and the `QcProfile.IsActive` lifecycle (§3) | B | |
| `TC-QC-STAT-001` … `015` | z-score arithmetic, `Math.Round(z, 3)` banker's-rounding behaviour, the handler-vs-evaluator z agreement, verdict-freeze under later target change | C | |
| `TC-QC-API-001` … `045` | All six logical endpoints on `api/qc`: status codes, problem+json shape, `code` extension, request-contract binding, `take` clamping | C | Includes the `/api/v{version}/…` mirror |
| `TC-QC-SEC-001` … `020` | `[RequirePermission(analytical-quality, Manage)]` gating, the ungated endpoints, `[RequireInternalActor]` command policy, `AUTHZ-000/001/002/008` | C | |
| `TC-QC-RLS-001` … `012` | `qams.qc_profile` / `qams.qc_run` FORCE-RLS `tenant_isolation` positive isolation, tenant-composite PK, `TENANT-000` | D | |
| `TC-QC-INT-001` … `025` | EF configuration ↔ schema agreement, the `QcTargetEffectiveDating` migration up/down, `ck_qc_run_outcome_domain`, `xmin` / `CONCURRENCY-409`, field-change ledger rows, outbox row for `QcOutOfControl` | D | Requires live PostgreSQL `ntqams` |
| `TC-QC-DF-001` … `012` | Data flow: prior-window query → evaluator → frozen verdict → DTO → chart; `WindowSize = 12` truncation | D | |
| `TC-QC-E2E-001` … `012` | Playwright journeys over `/t/{slug}/qc` and `/t/{slug}/qc/{id}` | E | |
| `TC-QC-A11Y-001` … `008` | Levey-Jennings SVG `role="img"` / `aria-label`, run-table semantics, troubleshooting-form labelling | E | axe-core |
| `TC-QC-UAT-001` … `012` | Executable counterparts of the Gherkin scenarios in §6 | E | |
| `TC-QC-PERF-001` … `008` | Record-run latency with a primed 12-run window; `GET runs?take=500` | F | |
| `TC-QC-OBS-001` … `008` | OTel span coverage of `RecordQcRunCommand`, outbox `traceparent` propagation, canonical request log | F | |
| `TC-QC-MUT-001` … `010` | Mutation-testing targets against `WestgardEvaluator` (flip `>` to `>=`, flip `Math.Sign` equality, drop the `RunLength - 1` guard) | F | |
| `TC-QC-EXPL-001` … `008` | Exploratory charters (§7) | F | Time-boxed, not scripted |

**Completeness statement.** Complete in this file: the implementation inventory (§1), brief-vs-code divergences (§2), state matrices (§3), decision tables (§4), UAT scenarios (§6), charters (§7) and the module gap register (§8). Deferred to the `-cases-<letter>.md` files: every detailed 28-field case. Explicitly **out of scope** for this module file: the twelve analytical *study* aggregates (precision, linearity, detection limit, method/lot/instrument comparison, interference, carryover, reference interval, outlier screening, uncertainty budget) and proficiency testing — those belong to the method-validation and PT module files. `SigmaAssessment` appears here only where it makes a QC-rule claim (§2, GAP-QC-004).

---

## 1. Implementation inventory

Every claim below cites `file:line` read during this pass. Where a behaviour was **not** located, it is stated as absent, not inferred.

### 1.1 Aggregates and value objects

| Type | Kind | File | Notes |
|---|---|---|---|
| `QcProfile` | Aggregate root, `ITenantScoped` | `src/NT.QAMS.Domain/AnalyticalQuality/QcProfile.cs:12` | Analyte + instrument + control lot + target mean/SD + effective dating |
| `QcRun` | Aggregate root, `ITenantScoped` | `src/NT.QAMS.Domain/AnalyticalQuality/QcProfile.cs:98` | One control measurement with its **frozen** verdict |
| `WestgardVerdict` | `sealed record` | `src/NT.QAMS.Domain/AnalyticalQuality/WestgardEvaluator.cs:11` | `(WestgardOutcome Outcome, IReadOnlyList<string> ViolatedRules)`; static `InControl` singleton at `:13` |
| `WestgardLimits` | `sealed record` (controlled parameter set, finding F-16) | `WestgardEvaluator.cs:24-25` | `WarningSd = 2m, RejectSd = 3m, RangeSd = 4m, RunLength = 10`; `Standard` static at `:28` |
| `WestgardEvaluator` | `static class`, pure function | `WestgardEvaluator.cs:66` | No I/O, no state; `const int TenXWindow = 10` at `:69` is retained for callers/tests only and is **not** used by `Evaluate` |
| `WestgardOutcome` | `enum` | `WestgardEvaluator.cs:3` | `InControl`, `Warning`, `OutOfControl` — exactly three values |
| `QcOutOfControl` | `sealed record : DomainEvent` | `QcProfile.cs:157` | `(Guid RunId, Guid ProfileId, string ViolatedRules, Guid TenantId)` |

There is **no** `LeveyJennings` domain type. Levey-Jennings is a frontend rendering (§1.9).

### 1.2 `QcProfile` — fields and invariants

Fields (`QcProfile.cs:21-33`): `TenantId`, `Analyte`, `Instrument`, `ControlLot`, `TargetMean`, `TargetSd`, `IsActive`, `TargetEffectiveFromUtc` (nullable), `LastTargetChangeReason` (nullable). Inherited from `AggregateRoot`: `Id`, `CreatedByUserId` (`src/NT.QAMS.SharedKernel/Primitives/AggregateRoot.cs:21`).

| Invariant | Enforcement | Code | Location |
|---|---|---|---|
| Analyte and instrument are required (non-blank) | `Create` guard | `QC-001` | `QcProfile.cs:38-41` |
| Target SD must be positive | `Create` guard | `QC-002` | `QcProfile.cs:43-46` |
| Blank control lot is normalised to the literal `"N/A"` (not rejected) | `Create` | — | `QcProfile.cs:52` |
| `Analyte`, `Instrument`, `ControlLot` are `.Trim()`-ed on create | `Create` | — | `QcProfile.cs:51-52` |
| `IsActive` starts `true` | `Create` | — | `QcProfile.cs:55` |
| Target SD must be positive on update too | `UpdateTargets` guard | `QC-002` | `QcProfile.cs:67-70` |
| A reason is mandatory to change targets | `UpdateTargets` guard | `QC-012` | `QcProfile.cs:72-75` |
| Target changes are forward-only | `UpdateTargets` guard — fires only when `TargetEffectiveFromUtc` is already non-null **and** `effectiveFrom < current` | `QC-013` | `QcProfile.cs:77-80` |
| Reason is `.Trim()`-ed before storage | `UpdateTargets` | — | `QcProfile.cs:84` |
| `TargetMean` has **no** validity constraint (negative and zero means are accepted) | — | — | absent from `QcProfile.cs:35-86` |
| `Deactivate()` sets `IsActive = false` and is otherwise unguarded and irreversible (no `Reactivate`) | — | — | `QcProfile.cs:88` |

### 1.3 `QcRun` — fields and invariants

Fields (`QcProfile.cs:107-115`): `TenantId`, `ProfileId`, `Value`, `ZScore`, `Outcome` (string), `ViolatedRules` (string), `Operator`, `MeasuredAtUtc`, `TroubleshootingNote` (nullable).

| Invariant | Enforcement | Code | Location |
|---|---|---|---|
| `ZScore` is persisted rounded to 3 decimals — `Math.Round(zScore, 3)`, i.e. **`MidpointRounding.ToEven`** (banker's rounding) since no mode is passed | `Record` | — | `QcProfile.cs:125` |
| `Outcome` is stored as `verdict.Outcome.ToString()` — one of `InControl` / `Warning` / `OutOfControl` | `Record` | — | `QcProfile.cs:126` |
| `ViolatedRules` is the comma-joined rule list, **no space after the comma**; empty string when none | `Record` | — | `QcProfile.cs:127` |
| A blank operator is normalised to the literal `"unknown"` (not rejected) | `Record` | — | `QcProfile.cs:128` |
| `QcOutOfControl` is raised **only** when the verdict is `OutOfControl` — a `Warning` raises nothing | `Record` | — | `QcProfile.cs:132-135` |
| Troubleshooting notes apply only to out-of-control runs | `LogTroubleshooting` guard, `InvalidStateTransitionException` | `QC-010` | `QcProfile.cs:142-146` |
| A troubleshooting note must be non-blank | `LogTroubleshooting` guard, `DomainException` | `QC-011` | `QcProfile.cs:148-151` |
| The note is `.Trim()`-ed before storage | `LogTroubleshooting` | — | `QcProfile.cs:153` |
| A note can be **overwritten** — `LogTroubleshooting` has no "already noted" guard | — | — | `QcProfile.cs:140-154` (absence) |
| The verdict is **never recomputed** — no method on `QcRun` mutates `Outcome`, `ViolatedRules`, `ZScore` or `Value` after `Record` | — | — | `QcProfile.cs:98-155` (absence); documented at `:92-97` |

### 1.4 `WestgardEvaluator.Evaluate` — the algorithm, as coded

Signature: `Evaluate(decimal value, decimal mean, decimal sd, IReadOnlyList<decimal> priorValues, WestgardLimits? limits = null)` (`WestgardEvaluator.cs:71-72`). `priorValues` is documented as **oldest first** (`:57`).

Execution order (`WestgardEvaluator.cs:73-134`):

1. `:74-78` — if `sd <= 0m` throw `DomainException("QC-SD", "Control SD must be positive to evaluate Westgard rules.")`. This is the **only** input guard; `mean`, `value` and `priorValues` are unvalidated, and a `null` `priorValues` would throw `NullReferenceException`, not a domain code.
2. `:80` — `var lim = limits ?? WestgardLimits.Standard;` — a `null` argument silently falls back to the standard limits. **`Validated()` is NOT called here**; validation happens only at composition root (§1.6).
3. `:81` — `z = (value - mean) / sd` (decimal division).
4. `:82` — `priorZ = priorValues.Select(v => (v - mean) / sd).ToList()`.
5. `:84` — `violations` list, appended in fixed rule order 1-3s → 2-2s → R-4s → 10-x. **The order of `ViolatedRules` is therefore deterministic and asserted-on.**
6. `:122-125` — if `violations.Count > 0` return `OutOfControl` with the full list. The warning rule is not evaluated in this branch.
7. `:127-131` — else if `Math.Abs(z) > lim.WarningSd` return `Warning` with a **single-element** list.
8. `:133` — else return `WestgardVerdict.InControl` (empty list).

Per-rule predicates are tabulated in §4, table DT-QC-1.

Behaviours worth naming explicitly, all read in source:

- **Every comparison is strict `>`.** A value exactly at a limit (`|z| == RejectSd`, `|z| == WarningSd`, `|z - prev| == RangeSd`) does **not** fire (`:87`, `:93`, `:96`, `:106`, `:128`).
- **Rule labels are string-interpolated from the limits** with the `0.#` format specifier: `$"1-{lim.RejectSd:0.#}s"` (`:89`), `$"2-{lim.WarningSd:0.#}s"` (`:98`), `$"R-{lim.RangeSd:0.#}s"` (`:108`), `$"{lim.RunLength}-x"` (`:118`), `$"1-{lim.WarningSd:0.#}s"` (`:130`). Under defaults these render `1-3s`, `2-2s`, `R-4s`, `10-x`, `1-2s`. Under non-default limits the labels change — the unit test at `tests/NT.QAMS.Domain.UnitTests/AnalyticalQuality/WestgardEvaluatorTests.cs:97-103` proves that with `WarningSd=1, RejectSd=2` the 1-3s slot emits the string `"1-2s"` as an **out-of-control** rejection.
- **The 2-2s and warning labels collide by construction.** Both derive from `WarningSd`: the rejection label is `2-{WarningSd}s`, the warning label is `1-{WarningSd}s`. They are distinguishable only by the leading digit and by the returned `Outcome`.
- **2-2s inspects only the single immediately-prior value** — `priorZ[^1]` (`:95`), never a longer run — and requires all three of: at least one prior, `|z| > WarningSd`, `|prev| > WarningSd`, and `Math.Sign(prev) == Math.Sign(z)` (`:93-99`).
- **R-4s does not require opposite sides**, despite the inline comment at `:102` saying "(opposite sides)" and the XML summary at `:60` saying "consecutive pair spanning more than 4SD". The coded predicate is only `Math.Abs(z - prev) > lim.RangeSd` (`:106`). A same-side pair such as `prev z = +4.5`, `z = +0.2` (span 4.3) fires `R-4s` with neither value beyond the reject limit. Recorded as GAP-QC-002.
- **The shift rule needs `priorZ.Count >= lim.RunLength - 1`** (`:113`) and builds its window as the last `RunLength - 1` prior z-scores plus the current one (`:115`), then requires `window.All(x => x > 0m) || window.All(x => x < 0m)` (`:116`). A z of **exactly 0** satisfies neither predicate and therefore **breaks the run in both directions**.
- **`TenXWindow = 10` (`:69`) is dead with respect to `Evaluate`** — the shift length actually used is `lim.RunLength`. The constant is retained for callers/tests per its own doc comment.

### 1.5 Exhaustive domain error codes for this module

Every code below was read at the cited line. The right-hand column is the HTTP status produced by `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:45-80` — `InvalidStateTransitionException` → **409**, `*-404` suffix → **404**, `AUTH-*` prefix → **401**, `AUTHZ-*` prefix → **403**, any other `DomainException` → **422**.

| Code | Message (verbatim) | Thrown by | Location | HTTP |
|---|---|---|---|---|
| `QC-001` | `Analyte and instrument are required.` | `QcProfile.Create` | `QcProfile.cs:40` | 422 |
| `QC-002` | `Target SD must be positive.` | `QcProfile.Create` | `QcProfile.cs:45` | 422 |
| `QC-002` | `Target SD must be positive.` (second site, same code) | `QcProfile.UpdateTargets` | `QcProfile.cs:69` | 422 |
| `QC-010` | `Troubleshooting notes apply only to out-of-control runs.` | `QcRun.LogTroubleshooting` — `InvalidStateTransitionException` | `QcProfile.cs:144-145` | **409** |
| `QC-011` | `A troubleshooting note is required.` | `QcRun.LogTroubleshooting` | `QcProfile.cs:150` | 422 |
| `QC-012` | `A reason is required to change QC targets.` | `QcProfile.UpdateTargets` | `QcProfile.cs:74` | 422 |
| `QC-013` | `Target changes are forward-only; the effective date cannot precede the current one.` | `QcProfile.UpdateTargets` | `QcProfile.cs:79` | 422 |
| `QC-SD` | `Control SD must be positive to evaluate Westgard rules.` | `WestgardEvaluator.Evaluate` | `WestgardEvaluator.cs:76-77` | 422 |
| `QC-LIM-001` | `Westgard SD limits must be positive.` | `WestgardLimits.Validated` | `WestgardEvaluator.cs:35-36` | startup failure (see §1.6) |
| `QC-LIM-002` | `The warning limit must be below the rejection limit.` | `WestgardLimits.Validated` | `WestgardEvaluator.cs:41-42` | startup failure |
| `QC-LIM-003` | `The shift-rule run length must be at least 2.` | `WestgardLimits.Validated` | `WestgardEvaluator.cs:47-48` | startup failure |
| `QC-404` | `QC profile not found.` | `RecordQcRunHandler` | `QcSlice.cs:53` | 404 |
| `QC-404` | `QC profile not found.` (second site, same code) | `UpdateQcTargetsHandler` | `QcSlice.cs:93` | 404 |
| `QC-404` | `QC run not found.` (third site, same code, different message) | `LogQcTroubleshootingHandler` | `QcSlice.cs:117` | 404 |

**There are no other `QC-*` codes in the build.** In particular there is no code for: a deactivated profile refusing a run, a duplicate profile, a run with a future `MeasuredAtUtc`, a note overwrite, or a 4-1s rule.

Cross-cutting codes reachable on QC routes (not QC-owned, but assertable):

| Code | Source | Location | HTTP |
|---|---|---|---|
| `AUTHZ-000` | command with no `CommandPolicyAttribute` (fail-closed) | `src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs:52` | 403 |
| `AUTHZ-001` | unauthenticated actor on a command | `AuthorizationBehavior.cs:60` | 403 |
| `AUTHZ-002` | `RequireInternalActor` refusing `UserRole.ExternalAuditor` | `AuthorizationBehavior.cs:75, 83` | 403 |
| `AUTHZ-008` | declared permission key unknown to the catalogue | `AuthorizationBehavior.cs:68` | 403 |
| `TENANT-000` | tenant-scoped entity persisted with no resolved tenant | `src/NT.QAMS.Infrastructure/Persistence/Interceptors/TenantStampInterceptor.cs:53` | 422 |
| `CONCURRENCY-409` | `xmin` token changed between read and write | `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:21`; convention applied at `src/NT.QAMS.Infrastructure/Persistence/AppDbContext.cs:120-133` | 409 |
| (FluentValidation) | `ValidationException` → 400 with an `errors` dictionary | `DomainExceptionHandler.cs:35-44` | 400 |

`CHANGE-REASON-REQUIRED` is **not reachable on this module** — `ChangeReasonMiddleware` gates DELETE only, and QC exposes no DELETE route (§1.7).

### 1.6 Configuration — `AnalyticalQuality:Westgard:*`

Bound and validated once, at the composition root, as a **singleton**:

```
src/NT.QAMS.Infrastructure/DependencyInjection.cs:86-90
services.AddSingleton(new WestgardLimits(
    ConfigGuard.ReadDecimal(configuration, "AnalyticalQuality:Westgard:WarningSd", 2m),
    ConfigGuard.ReadDecimal(configuration, "AnalyticalQuality:Westgard:RejectSd",  3m),
    ConfigGuard.ReadDecimal(configuration, "AnalyticalQuality:Westgard:RangeSd",   4m),
    ConfigGuard.ReadInt    (configuration, "AnalyticalQuality:Westgard:RunLength", 10)).Validated());
```

Shipped values (`src/NT.QAMS.WebApi/appsettings.json:12-19`): `WarningSd: 2`, `RejectSd: 3`, `RangeSd: 4`, `RunLength: 10`.

Consequences that case authors must respect:

- The limits are **process-wide and tenant-agnostic** — one `WestgardLimits` singleton serves every tenant. There is no per-tenant or per-profile limit override anywhere in the build (GAP-QC-005).
- A malformed value (non-numeric) is refused by `ConfigGuard` itself with an `InvalidOperationException` naming the key and the phrase `Refusing to start` — proven by `tests/NT.QAMS.WebApi.FunctionalTests/ConfigGuardTests.cs:42-49`; a missing key falls back to the documented default (`ConfigGuardTests.cs:20-28`, which asserts `AnalyticalQuality:Westgard:WarningSd` specifically at `:27`).
- A **well-formed but invalid** set (e.g. `WarningSd: 3, RejectSd: 2`) is refused by `Validated()` with `QC-LIM-002`, thrown as a `DomainException` during DI registration — i.e. **startup fails**, it does not surface as an HTTP response. Any case asserting `QC-LIM-001/002/003` must be a startup/unit case, never an API case.
- `Evaluate` itself never calls `Validated()` (`WestgardEvaluator.cs:80`), so a directly-constructed invalid limit set is honoured by the evaluator. Unit cases may exploit this deliberately.
- `RunLength` is validated only against a **lower** bound of 2. It is not validated against the handler's `WindowSize = 12`, so any `RunLength > 13` produces a shift rule that can never fire (GAP-QC-006).

### 1.7 Application layer — commands, queries, endpoints, permissions

All in `src/NT.QAMS.Application/AnalyticalQuality/QcSlice.cs` and `src/NT.QAMS.WebApi/Controllers/AnalyticalQualityControllers.cs:11-49`. Controller: `QualityControlController`, `[ApiController]`, `[Route("api/qc")]`, class-level `[Authorize]` (`:11-14`).

**Six logical endpoints** (twelve routes in the approved surface, counting the `/api/v{version}/…` mirror — `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt:97, 98, 205, 206, 381, 382, 383, 574, 575, 576, 632, 644`):

| # | Route | Method | Controller line | Endpoint permission | Command / query | Command policy | Validator |
|---|---|---|---|---|---|---|---|
| 1 | `GET /api/qc/profiles` | GET | `:16-18` | **none** — `[Authorize]` only | `GetQcProfilesQuery` | n/a — queries bypass the policy switch (`AuthorizationBehavior.cs:44-47`) | none |
| 2 | `POST /api/qc/profiles` | POST | `:20-25` | `[RequirePermission(analytical-quality, Manage)]` `:21` | `CreateQcProfileCommand` `QcSlice.cs:12` | `[RequireInternalActor]` `QcSlice.cs:11` | `CreateQcProfileValidator` `QcSlice.cs:16-24` |
| 3 | `PUT /api/qc/profiles/{id}/targets` | PUT | `:27-33` | `[RequirePermission(analytical-quality, Manage)]` `:28` | `UpdateQcTargetsCommand` `QcSlice.cs:75` | `[RequireInternalActor]` `QcSlice.cs:74` | `UpdateQcTargetsValidator` `QcSlice.cs:78-85` |
| 4 | `GET /api/qc/profiles/{id}/runs` | GET | `:35-37` | **none** | `GetQcRunsQuery(id, take = 60)` | n/a | none |
| 5 | `POST /api/qc/profiles/{id}/runs` | POST | `:39-41` | **none** | `RecordQcRunCommand` `QcSlice.cs:43` | `[RequireInternalActor]` `QcSlice.cs:42` | **none — no `RecordQcRunValidator` exists** |
| 6 | `POST /api/qc/runs/{runId}/troubleshoot` | POST | `:43-48` | **none** | `LogQcTroubleshootingCommand` `QcSlice.cs:100` | `[RequireInternalActor]` `QcSlice.cs:99` | `LogQcTroubleshootingValidator` `QcSlice.cs:103-109` |

Response shapes: #2 returns `200 OK` with `{ id }` (`:23`) — **not** `201 Created`; #3 and #6 return `204 No Content` (`:32`, `:47`); #5 returns `200 OK` with `{ runId }` (`:41`).

Permission key in play: **`analytical-quality.manage`** only. The module string is `PermissionCatalog.AnalyticalQuality = "analytical-quality"` (`src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:100`); the module is registered with the full action set `View, Create, Edit, Approve, Void, Sign, Export, Manage` (`PermissionCatalog.cs:171-173`), so `analytical-quality.view`, `.create`, `.edit`, `.export` **exist in the catalogue but are not enforced on any QC route**. `PermissionAction.Manage` is the only one this controller cites. Keys are built by `PermissionCatalog.Key(module, action)` in `{module}.{action}` lower-case form (conventions §2).

Command-policy CI gate: `RequireInternalActorAttribute` derives from `CommandPolicyAttribute` (`src/NT.QAMS.Application/Abstractions/CommandAuthorization.cs:19`, base at `:12`), so all four QC commands satisfy `tests/NT.QAMS.Architecture.Tests/CommandPolicyTests.cs:25-38`. Its practical effect is only to exclude `UserRole.ExternalAuditor` (`AuthorizationBehavior.cs:75`).

Validator rules, verbatim:

- `CreateQcProfileValidator` (`QcSlice.cs:20-22`): `Analyte` NotEmpty, MaxLength 100; `Instrument` NotEmpty, MaxLength 100; `TargetSd` GreaterThan 0. **`ControlLot` and `TargetMean` are unvalidated** — `ControlLot` is not length-bounded at the API even though the column is `varchar(60)` (§1.8), so a 61-character lot reaches PostgreSQL.
- `UpdateQcTargetsValidator` (`QcSlice.cs:82-83`): `TargetSd` GreaterThan 0; `Reason` NotEmpty, MaxLength 500.
- `LogQcTroubleshootingValidator` (`QcSlice.cs:106-107`): `Note` NotEmpty, MaxLength 2000 — the comment at `QcSlice.cs:102` records that this bound moved from the column to the validator when the column became `text` (schema hardening 1.2).
- **No validator for `RecordQcRunCommand`** — `Value` is unbounded and `Operator` may be empty (the domain then substitutes `"unknown"`, `QcProfile.cs:128`). GAP-QC-007.

### 1.8 `RecordQcRunHandler` — the evaluation pipeline

`src/NT.QAMS.Application/AnalyticalQuality/QcSlice.cs:45-72`. Dependencies: `IAppDbContext db`, `IClock clock`, `WestgardLimits westgardLimits` (the validated singleton).

1. `:52-53` — load the profile by `Id`; `QC-404` if absent. The tenant filter is implicit (EF global query filter + RLS).
2. `:48` — **`public const int WindowSize = 12`**.
3. `:55-60` — prior window: `db.QcRuns.Where(r => r.ProfileId == c.ProfileId).OrderByDescending(r => r.MeasuredAtUtc).Take(12).Select(r => r.Value)`.
4. `:61` — `priorValues.Reverse()` — the comment reads *"oldest first for the evaluator"*.
5. `:63-64` — `WestgardEvaluator.Evaluate(c.Value, profile.TargetMean, profile.TargetSd, priorValues, westgardLimits)`.
6. `:65` — `z = (c.Value - profile.TargetMean) / profile.TargetSd`, computed a **second time** in the handler (the evaluator computes its own internally at `WestgardEvaluator.cs:81`); the two are arithmetically identical.
7. `:67` — `QcRun.Record(..., clock.UtcNow)` — `MeasuredAtUtc` is **always "now"**; the caller cannot supply a measurement time.
8. `:68-69` — add and save.

Properties of this pipeline that constrain test design:

- The prior window is **`Value`-based, not z-based**, and is re-projected against the *current* targets at `WestgardEvaluator.cs:82`. After a target change, historical **stored** z-scores stay frozen on their rows, but the *window* used for the next evaluation is recomputed against the new targets. Both halves are as-designed and both must be asserted.
- The window is capped at 12, so the 10-x rule (needing 9 priors) is satisfiable, but any `RunLength > 13` is not (GAP-QC-006).
- `OrderByDescending(r => r.MeasuredAtUtc)` has **no tie-breaker**. Two runs recorded within the same clock tick have an implementation-defined relative order, which changes which value is `priorZ[^1]` for the 2-2s and R-4s rules (GAP-QC-008).
- The handler does **not** check `profile.IsActive`, and does **not** check `profile.TargetEffectiveFromUtc` against `clock.UtcNow`. A retired profile still accepts runs; a future-dated target is applied immediately (GAP-QC-009, GAP-QC-003).
- `UpdateQcTargetsHandler` (`QcSlice.cs:87-97`) passes **`clock.UtcNow`** as `effectiveFrom` (`:94`). The command carries no effective-date field (`QcSlice.cs:75`) and neither does the request contract `UpdateQcTargetsRequest(TargetMean, TargetSd, Reason)` (`src/NT.QAMS.Contracts/AnalyticalQuality/AnalyticalQualityContracts.cs:9`). Consequence: `QC-013` is **unreachable through the API** because `clock.UtcNow` is monotonically non-decreasing (GAP-QC-003).

Queries: `GetQcProfilesHandler` (`QcSlice.cs:125-133`) returns all profiles ordered by `Analyte`, **including inactive ones**, unpaged. `GetQcRunsHandler` (`:137-149`) filters by `ProfileId`, orders `MeasuredAtUtc` descending and clamps `take` with `Math.Clamp(q.Take, 1, 500)` (`:144`) — so `take=0` yields 1 row and `take=100000` yields 500. Neither query uses the pagination envelope.

### 1.9 Frontend surface (Levey-Jennings is a UI concern)

| Artefact | File | Notes |
|---|---|---|
| Typed API client | `frontend/src/app/core/api/analytical-api.service.ts:19-45` | Methods `qcProfiles`, `createQcProfile`, `qcRuns(profileId, take = 60)`, `recordQcRun`, `troubleshootRun`. **There is no client method for `PUT /api/qc/profiles/{id}/targets`** (GAP-QC-010) |
| Signal facade | `frontend/src/app/features/analytical/qc.facade.ts` | `chartRuns` computed reverses the newest-first API payload to chronological order for plotting; errors are surfaced from the problem+json `title` |
| Levey-Jennings chart | `frontend/src/app/features/analytical/levey-jennings-chart.component.ts` | Self-contained SVG, **no chart library**. Guide lines at mean and ±1/2/3 SD only; the frame spans **±4 SD** and z-scores are **clamped** to that range before plotting, so a `1-3s` outlier at z = +9 renders at the +4 SD edge. Point colour is driven by the **stored** `outcome` string: `OutOfControl` red `#DC3545`, `Warning` amber `#ECB71E`, everything else green `#188038`. `role="img"` with `aria-label="Levey-Jennings chart"` |
| Profile detail | `frontend/src/app/features/analytical/qc-profile-detail.component.ts` | Workflow stepper shows `Active` / `Retired` from `p.isActive`; the troubleshoot link is rendered **only** when `r.outcome === 'OutOfControl' && !r.troubleshootingNote`, so the UI never offers the note-overwrite path the domain permits; audit trail embedded via `<qams-audit-trail [subject]="p.id">` |
| Routes | `frontend/src/app/app.routes.ts:214-222` | `qc` → `QcProfilesComponent`, child `:id` → `QcProfileDetailComponent` |

The chart's ±4 SD frame and the `RangeSd = 4` limit are numerically equal but **causally unrelated** — the chart hard-codes 4 (`levey-jennings-chart.component.ts`, `yFor` and `clamp`), it does not read the configured limits. Under non-default limits the chart's guide lines no longer correspond to the acceptance thresholds (GAP-QC-011).

### 1.10 Persistence

**Tables** (both in schema `qams`), configured at `src/NT.QAMS.Infrastructure/Persistence/Configurations/AnalyticalQualityConfigurations.cs`:

| Table | Config lines | Key | Columns of note | Indexes |
|---|---|---|---|---|
| `qams.qc_profile` | `:7-22` | `HasKey(TenantId, Id)` — tenant-first composite, **no `UNIQUE(id)`** | `analyte varchar(100)`, `instrument varchar(100)`, `control_lot varchar(60)`, `target_mean numeric(18,6)`, `target_sd numeric(18,6)`, `last_target_change_reason varchar(500)`, `target_effective_from_utc timestamptz` | `ix_qc_profile_tenant_id_analyte_instrument_control_lot` — **non-unique**, so duplicate profiles are permitted |
| `qams.qc_run` | `:24-39` | `HasKey(TenantId, Id)` | `value numeric(18,6)`, `z_score numeric(10,3)`, `outcome varchar(15)`, `violated_rules varchar(60)`, `operator varchar(150)`, `troubleshooting_note text` | `ix_qc_run_tenant_id_profile_id_measured_at_utc` — the hot Levey-Jennings window path, per the comment at `:35` |

`DomainEvents` is `Ignore`d on both (`:20`, `:37`).

**Migrations touching QC:**

| Migration | What it did to QC |
|---|---|
| `20260721225752_AnalyticalQuality.cs` | Created both tables (`:41`, `:64`), the two indexes (`:146`, `:152`), and `ENABLE ROW LEVEL SECURITY` + `CREATE POLICY tenant_isolation` on both (`:172-176`) |
| `20260726190957_QcTargetEffectiveDating.cs` | **In scope for this module.** Added `last_target_change_reason varchar(500) NULL` (`:14-20`) and `target_effective_from_utc timestamptz NULL` (`:22-27`); `Down()` drops both (`:33-41`). Pure additive column migration — **no backfill, no CHECK, no index, no data migration** |
| `20260726192118_CreatedByUserIdForSoD.cs` | Added nullable `created_by_user_id uuid` to `qc_run` (`:127-131`) and `qc_profile` (`:134-138`) |
| `20260731180344_Hardening1_TypesAndNames.cs` | Converted `qc_run.troubleshooting_note` from `varchar(2000)` to `text` (`:144-153`); `Down()` restores the bound (`:765-775`) |
| `20260731191212_Hardening3_CheckDomains.cs` | `ALTER TABLE qams.qc_run ADD CONSTRAINT ck_qc_run_outcome_domain CHECK (outcome IN ('InControl','Warning','OutOfControl')) NOT VALID;` then `VALIDATE CONSTRAINT` (`:157-158`); dropped in `Down()` (`:242`) |
| `20260731210953_Hardening5_CompositeKeys.cs` | Dropped the single-column `pk_qc_run` / `pk_qc_profile` (`:319-326`) and re-added them as tenant-first composites (`:885-893`) |

**No foreign key from `qc_run.profile_id` to `qc_profile`** was found in any migration or EF configuration — the only FK-shaped guard is the handler's `QC-404` lookup (GAP-QC-012).

**Neither `qc_profile` nor `qc_run` carries the `frozen_immutability` trigger.** The `Frozen` table list in `20260726084134_SignedRecordImmutability.cs` covers the twelve analytical study roots plus `uncertainty_budget` and does not include either QC table. The "frozen verdict" is an application-layer promise (no mutating method on `QcRun`), **not** a database-enforced one (GAP-QC-013).

**Concurrency:** `xmin` is applied by convention to every `AggregateRoot` with a null base type (`src/NT.QAMS.Infrastructure/Persistence/AppDbContext.cs:120-133`), so both QC aggregates carry it; a lost update surfaces as `CONCURRENCY-409` / HTTP 409.

**Audit:** neither `QcProfile` nor `QcRun` is in the `FieldChangeInterceptor.Excluded` set (`src/NT.QAMS.Infrastructure/Persistence/Interceptors/FieldChangeInterceptor.cs:27-31`), so every insert/update produces `audit.field_change` rows with old/new values. None of the QC property names match the redaction fragments `password, secret, pin, hash, token` (`:34`), so values are stored in clear — which is correct for QC data.

### 1.11 Domain events and downstream reaction

`QcOutOfControl(RunId, ProfileId, ViolatedRules, TenantId)` (`QcProfile.cs:157`) is raised inside the `QcRun.Record` factory (`:134`) and drained into `qams.outbox_event` in the same transaction by `OutboxInterceptor` (`src/NT.QAMS.Infrastructure/Persistence/Interceptors/OutboxInterceptor.cs:44-82`). The outbox row's own `tenant_id` is read from the entity (`OutboxInterceptor.cs:55-56`) and `TenantStampInterceptor` is registered **before** `OutboxInterceptor` (`src/NT.QAMS.Infrastructure/DependencyInjection.cs:60, 62`), so the row is correctly attributed.

However the **serialized payload's** `TenantId` is captured at `QcRun.Record` time (`QcProfile.cs:134`), before the entity has been added to the change tracker and before `TenantStampInterceptor.Stamp` assigns `TenantId` (`TenantStampInterceptor.cs:39-54`). The event body therefore serialises `"tenantId":"00000000-0000-0000-0000-000000000000"`. Recorded as GAP-QC-001.

**There is no consumer.** A repository-wide search for `QcOutOfControl` returns exactly three hits — the doc comment at `QcSlice.cs:40`, the `Raise` at `QcProfile.cs:134`, and the record declaration at `QcProfile.cs:157`. `NotificationPolicies` handles `CalibrationDue`, `EquipmentLockedOut`, `HighImpartialityRiskDeclared`, `ReferenceStandardExpired`, `CompetencyExpired`, `HighResidualRisk`, `SupplierSuspended` and `EscalationTriggered` (`src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:70-116`) — **not** `QcOutOfControl`. No NC is raised, no notification is dispatched, no task is created (GAP-QC-014). Every QC case's **Expected Notification** field must therefore read `n/a — no handler is subscribed to QcOutOfControl`.

### 1.12 Existing automated coverage

`tests/NT.QAMS.Domain.UnitTests/AnalyticalQuality/WestgardEvaluatorTests.cs` — 11 facts, all against mean 100 / SD 5:

| Test | Line | Asserts |
|---|---|---|
| `In_control_value_passes` | `:20` | z = 0.6 → `InControl`, empty rule list |
| `Rule_1_2s_is_warning_only` | `:28` | z = 2.2 → `Warning`, single rule `"1-2s"` |
| `Rule_1_3s_rejects` | `:36` | z = 3.2 → `OutOfControl`, contains `"1-3s"` |
| `Rule_2_2s_rejects_two_consecutive_same_side_beyond_2sd` | `:44` | 111 after 112 → contains `"2-2s"` |
| `Rule_2_2s_does_not_fire_for_opposite_sides` | `:52` | 111 after 89 → no `"2-2s"` |
| `Rule_R_4s_rejects_span_over_4sd` | `:59` | 112 after 88 (span 4.8 SD) → contains `"R-4s"` |
| `Rule_10x_rejects_ten_consecutive_same_side` | `:67` | 9 priors above the mean + a 10th → contains `"10-x"` |
| `Rule_10x_does_not_fire_when_a_value_crosses_the_mean` | `:77` | one prior below the mean → no `"10-x"` |
| `Zero_sd_is_rejected` | `:85` | `sd = 0` → `QC-SD` |
| `Configured_limits_change_the_grading_and_the_rule_labels` | `:92` | `WarningSd=1, RejectSd=2, RunLength=8`: z = 2.2 → `OutOfControl` containing the string `"1-2s"` |
| `An_invalid_limit_set_is_rejected` | `:106` | `WarningSd=3, RejectSd=2` → `QC-LIM-002` |

**Not covered by any existing test** (each is a case-authoring target, not a defect in itself): the exact-boundary `>` behaviour on any of the four limits; `QC-LIM-001`; `QC-LIM-003`; multi-rule simultaneous firing and the resulting list order; the R-4s same-side path; z exactly 0 breaking a 10-x run; every `QcProfile` and `QcRun` invariant (`QC-001`, `QC-002`, `QC-010`, `QC-011`, `QC-012`, `QC-013`); `Math.Round` banker's-rounding on `ZScore`; the `WindowSize = 12` truncation; every endpoint; every permission gate; RLS isolation on the two tables.

`tests/NT.QAMS.WebApi.FunctionalTests/ConfigGuardTests.cs:20-49` covers the config-reading half of `AnalyticalQuality:Westgard:WarningSd` but never asserts that a bad **combination** fails startup.

### 1.13 States

The module has two state carriers; neither is an enum column with a state machine in the classical sense.

- `QcRun.Outcome` — string, constrained by `ck_qc_run_outcome_domain` to `InControl | Warning | OutOfControl`. **Terminal on write**: assigned once in `Record` (`QcProfile.cs:126`) and never reassigned.
- `QcProfile.IsActive` — boolean; `true` on create (`:55`), `false` after `Deactivate()` (`:88`). One-way, and `Deactivate()` has **no caller anywhere in `src`** — verified by search; the only definition/usage is the method body itself.

A secondary, derived state is the *troubleshooting* state of an out-of-control run: `TroubleshootingNote is null` vs non-null.

---

## 2. Divergences from the commissioning brief

| # | What the brief assumes | What the code actually does | file:line | Gap id |
|---|---|---|---|---|
| 1 | Six Westgard rules including **4-1s** (four consecutive beyond the same 1SD limit) | Five rule checks only: 1-3s, 2-2s, R-4s, 10-x (rejection) and 1-2s (warning). No 4-1s, no 8-x, no 2-of-3-2s, no 7-T | `WestgardEvaluator.cs:86-131` (exhaustive) | GAP-QC-015 |
| 2 | "Levey-Jennings module" with server-side charting | No `LeveyJennings` type exists in `src`. L-J is a client-only SVG component with hard-coded ±1/2/3 SD guides and a ±4 SD clamp | `frontend/src/app/features/analytical/levey-jennings-chart.component.ts` | GAP-QC-011 |
| 3 | An out-of-control QC run raises a nonconformance / notifies the Quality Manager | `QcOutOfControl` is raised and written to the outbox, but **no handler subscribes**. No NC, no notification, no task | `QcProfile.cs:157`; `NotificationPolicies.cs:70-116` (absence) | GAP-QC-014 |
| 4 | QC target changes are **effective-dated** — the lab schedules a new mean/SD from a future date | The domain method accepts `effectiveFrom`, but the handler hard-wires `clock.UtcNow` and the request contract has no date field. Effective dating is *recorded*, never *scheduled*, and `QC-013` is unreachable via the API | `QcSlice.cs:94`; `AnalyticalQualityContracts.cs:9`; `QcProfile.cs:77-80` | GAP-QC-003 |
| 5 | R-4s is a **range** rule between opposite-sided values | The coded predicate is an unsigned span test only; a same-side pair can fire it | `WestgardEvaluator.cs:102-110` (comment at `:102` contradicts the code at `:106`) | GAP-QC-002 |
| 6 | QC limits are set per analyte / per control level | A single process-wide `WestgardLimits` singleton, tenant-agnostic and profile-agnostic | `DependencyInjection.cs:86-90` | GAP-QC-005 |
| 7 | Recording a QC run is a privileged action | `POST /api/qc/profiles/{id}/runs` and `POST /api/qc/runs/{runId}/troubleshoot` carry **no** `[RequirePermission]`; any authenticated non-`ExternalAuditor` may record a run and close it out | `AnalyticalQualityControllers.cs:39-48`; `AuthorizationBehavior.cs:75` | GAP-QC-016 |
| 8 | QC data is read-restricted | `GET /api/qc/profiles` and `GET /api/qc/profiles/{id}/runs` carry no permission attribute; `analytical-quality.view` exists in the catalogue but is not enforced here | `AnalyticalQualityControllers.cs:16-18, 35-37`; `PermissionCatalog.cs:171-173` | GAP-QC-016 |
| 9 | The verdict is immutable at the record layer, like a signed study | `QcRun` is not in the `frozen_immutability` trigger list; immutability rests solely on the absence of a mutating domain method. A direct `UPDATE qams.qc_run SET outcome=…` succeeds | `20260726084134_SignedRecordImmutability.cs` `Frozen` list (absence of `qc_run`) | GAP-QC-013 |
| 10 | Sigma-based QC design drives the rules actually applied | `SigmaAssessment.QcRecommendation` emits text recommending `4:1s` and `8:x` at ≥4σ and ≥3σ — rules the evaluator cannot enforce. The recommendation is advisory prose with no wiring to `WestgardLimits` | `src/NT.QAMS.Domain/AnalyticalQuality/SigmaAssessment.cs:114-121` | GAP-QC-004 |
| 11 | A retired control profile stops accepting runs | `Deactivate()` exists but has no caller, no command and no endpoint; and `RecordQcRunHandler` never inspects `IsActive` | `QcProfile.cs:88`; `QcSlice.cs:50-71` | GAP-QC-009 |
| 12 | Duplicate control profiles are prevented | The `(tenant_id, analyte, instrument, control_lot)` index is **not** unique | `AnalyticalQualityConfigurations.cs:19` | GAP-QC-017 |
| 13 | QC target history is queryable | `TargetEffectiveFromUtc` and `LastTargetChangeReason` are persisted but appear in **no** DTO, **no** query and **no** frontend model — they are write-only outside the field-change ledger | `AnalyticalQualityContracts.cs:11-13`; `QcSlice.cs:130-132` | GAP-QC-018 |
| 14 | Every write command is permission-gated (v1.51.0 model) | The four QC commands declare `[RequireInternalActor]`, not `[RequirePermissionPolicy]` — satisfying the CI gate but providing coarse, role-tier authorization only | `QcSlice.cs:11, 42, 74, 99` | GAP-QC-016 |

---

## 3. State-transition matrices

### 3.1 `QcRun.Outcome` — assigned once, terminal

The outcome is a *derived* state, not a workflow state: it is computed by `WestgardEvaluator` and written by `QcRun.Record` (`QcProfile.cs:126`). No transition out of it exists in the domain.

| From \ Event | `QcRun.Record` (creation) | `LogTroubleshooting(note)` | Any other domain call | Direct SQL `UPDATE` |
|---|---|---|---|---|
| *(non-existent)* | → `InControl` \| `Warning` \| `OutOfControl` per DT-QC-1 | n/a | n/a | n/a |
| `InControl` | n/a | **blocked** — `QC-010`, `InvalidStateTransitionException` → 409 (`QcProfile.cs:142-146`) | no mutator exists | **permitted** — no DB trigger (GAP-QC-013) |
| `Warning` | n/a | **blocked** — `QC-010` → 409 (the guard tests only for `OutOfControl`) | no mutator exists | **permitted** (GAP-QC-013) |
| `OutOfControl` | n/a | → `OutOfControl` with `TroubleshootingNote` set; blank note → `QC-011` → 422 | no mutator exists | **permitted** (GAP-QC-013) |

Note the asymmetry a case must cover: a `Warning` run is *not* eligible for a troubleshooting note even though the UI colours it amber and the analyst may well have investigated it.

### 3.2 Troubleshooting sub-state of an out-of-control run

| From | Event | To | Guard / code | Location |
|---|---|---|---|---|
| `OutOfControl`, note `null` | `LogTroubleshooting("…")` | `OutOfControl`, note set | non-blank required, `QC-011` | `QcProfile.cs:148-153` |
| `OutOfControl`, note `null` | `LogTroubleshooting("   ")` | *unchanged* | `QC-011` → 422 | `QcProfile.cs:148-151` |
| `OutOfControl`, note set | `LogTroubleshooting("different")` | note **overwritten** — no guard | none | `QcProfile.cs:140-154` (absence of an idempotency guard) |
| `OutOfControl`, note set | *(UI path)* | link not rendered | `r.outcome === 'OutOfControl' && !r.troubleshootingNote` | `qc-profile-detail.component.ts` template |

The UI hides the overwrite path; the API does not. A case must exercise the API directly to reach it.

### 3.3 `QcProfile.IsActive` lifecycle

| From | Event | To | Reachable through the API? | Location |
|---|---|---|---|---|
| *(non-existent)* | `QcProfile.Create` | `IsActive = true` | Yes — `POST /api/qc/profiles` | `QcProfile.cs:55` |
| `IsActive = true` | `UpdateTargets` | `IsActive = true` (unchanged) | Yes — `PUT …/targets` | `QcProfile.cs:65-86` |
| `IsActive = true` | `Deactivate()` | `IsActive = false` | **No — no command, no endpoint, no caller** | `QcProfile.cs:88` (GAP-QC-009) |
| `IsActive = false` | `Record` a run against it | run accepted, verdict computed normally | Yes — the handler never checks `IsActive` | `QcSlice.cs:50-71` (absence) |
| `IsActive = false` | *(no `Reactivate` exists)* | — | No | `QcProfile.cs:35-89` (absence) |

### 3.4 Target-change transitions

| From (`TargetEffectiveFromUtc`) | `effectiveFrom` supplied | Result | Code |
|---|---|---|---|
| `null` (never changed) | any value, including a past date | accepted; mean/SD/reason/date written | — (`QcProfile.cs:77` short-circuits on `null`) |
| `T0` | `T1 > T0` | accepted | — |
| `T0` | `T1 == T0` | accepted (`<` is strict) | — |
| `T0` | `T1 < T0` | rejected | `QC-013` → 422 |
| any | blank/whitespace reason | rejected | `QC-012` → 422 |
| any | `targetSd <= 0` | rejected | `QC-002` → 422 |

Because `UpdateQcTargetsHandler` always supplies `clock.UtcNow` (`QcSlice.cs:94`), only the first three rows are reachable over HTTP. Rows 4 is reachable **only** at the domain-unit level or by injecting a rewound `IClock` (GAP-QC-003).

---

## 4. Decision tables

### DT-QC-1 — Implemented rule table: predicate → limit constant → outcome

`z = (value − mean) / sd`; `prev = priorZ[^1]` (the z of the immediately preceding run); `priorZ` is the window of prior values re-projected against the **current** targets; `lim` is the injected `WestgardLimits`.

| Rule (emitted label) | Exact predicate as coded | Derived from | Contributes | Guard on window length | Line |
|---|---|---|---|---|---|
| `$"1-{lim.RejectSd:0.#}s"` → **`1-3s`** | `Math.Abs(z) > lim.RejectSd` | `RejectSd` (3) | `violations` → **OutOfControl** | none — fires on the very first run | `:87-90` |
| `$"2-{lim.WarningSd:0.#}s"` → **`2-2s`** | `priorZ.Count >= 1` **and** `Math.Abs(z) > lim.WarningSd` **and** `Math.Abs(prev) > lim.WarningSd` **and** `Math.Sign(prev) == Math.Sign(z)` | `WarningSd` (2) | `violations` → **OutOfControl** | needs ≥ 1 prior | `:93-100` |
| `$"R-{lim.RangeSd:0.#}s"` → **`R-4s`** | `priorZ.Count >= 1` **and** `Math.Abs(z - prev) > lim.RangeSd` | `RangeSd` (4) | `violations` → **OutOfControl** | needs ≥ 1 prior | `:103-110` |
| `$"{lim.RunLength}-x"` → **`10-x`** | `priorZ.Count >= lim.RunLength - 1` **and** ( `window.All(x => x > 0m)` **or** `window.All(x => x < 0m)` ), where `window = last (RunLength−1) of priorZ, then z` | `RunLength` (10) | `violations` → **OutOfControl** | needs ≥ 9 priors at the default | `:113-120` |
| `$"1-{lim.WarningSd:0.#}s"` → **`1-2s`** | evaluated **only if `violations.Count == 0`**, then `Math.Abs(z) > lim.WarningSd` | `WarningSd` (2) | sole rule → **Warning** | none | `:122-131` |

Every comparison is **strict**. Exact-limit values (`|z| = 3.000`, `|z| = 2.000`, `|z − prev| = 4.000`) produce **no** firing — this is the single highest-yield boundary family in the module.

### DT-QC-2 — Full Westgard decision table (default limits, default `RunLength = 10`)

Conditions are evaluated independently; `Y` = condition true, `N` = false, `–` = irrelevant/unreachable. "Priors ≥ 1" and "Priors ≥ 9" refer to `priorZ.Count`.

| # | \|z\| > 3 | Priors ≥ 1 | \|z\| > 2 ∧ \|prev\| > 2 ∧ same sign | \|z − prev\| > 4 | Priors ≥ 9 ∧ all-same-side incl. z | \|z\| > 2 | **Outcome** | **`ViolatedRules` (exact, in order)** |
|---|---|---|---|---|---|---|---|---|
| R1 | N | – | N | N | N | N | `InControl` | *(empty)* |
| R2 | N | – | N | N | N | Y | `Warning` | `1-2s` |
| R3 | Y | N | – | – | N | Y | `OutOfControl` | `1-3s` |
| R4 | Y | Y | N | N | N | Y | `OutOfControl` | `1-3s` |
| R5 | N | Y | Y | N | N | Y | `OutOfControl` | `2-2s` |
| R6 | Y | Y | Y | N | N | Y | `OutOfControl` | `1-3s,2-2s` |
| R7 | N | Y | N | Y | N | N | `OutOfControl` | `R-4s` |
| R8 | N | Y | N | Y | N | Y | `OutOfControl` | `R-4s` |
| R9 | Y | Y | N | Y | N | Y | `OutOfControl` | `1-3s,R-4s` |
| R10 | N | Y | Y | Y | N | Y | `OutOfControl` | `2-2s,R-4s` |
| R11 | Y | Y | Y | Y | N | Y | `OutOfControl` | `1-3s,2-2s,R-4s` |
| R12 | N | Y | N | N | Y | N | `OutOfControl` | `10-x` |
| R13 | N | Y | N | N | Y | Y | `OutOfControl` | `10-x` |
| R14 | Y | Y | N | N | Y | Y | `OutOfControl` | `1-3s,10-x` |
| R15 | N | Y | Y | N | Y | Y | `OutOfControl` | `2-2s,10-x` |
| R16 | N | Y | N | Y | Y | Y | `OutOfControl` | `R-4s,10-x` |
| R17 | Y | Y | Y | N | Y | Y | `OutOfControl` | `1-3s,2-2s,10-x` |
| R18 | Y | Y | N | Y | Y | Y | `OutOfControl` | `1-3s,R-4s,10-x` |
| R19 | N | Y | Y | Y | Y | Y | `OutOfControl` | `2-2s,R-4s,10-x` |
| R20 | Y | Y | Y | Y | Y | Y | `OutOfControl` | `1-3s,2-2s,R-4s,10-x` |

Reachability notes a case author must respect before writing a rule for a row:

- **R2 requires `priorZ.Count == 0` or a prior that defeats 2-2s and R-4s.** With `|z| ∈ (2, 3]` and no prior, R2 is trivially reachable (this is exactly the existing unit test at `WestgardEvaluatorTests.cs:28-33`).
- **`2-2s` implies `|z| > 2`**, so no row can have `2-2s = Y` with `|z| > 2 = N`. Likewise **`1-3s` implies `|z| > 2`** at default limits (since `RejectSd > WarningSd` is enforced by `QC-LIM-002`). Rows R7 and R12 are therefore the only `|z| ≤ 2` rejection rows, both driven purely by the prior window.
- **R10/R11/R19/R20 need `2-2s` and `R-4s` simultaneously**: same sign, both beyond 2SD, and a span > 4SD. Same-signed values ≥ 4SD apart both beyond 2SD requires e.g. `prev z = +2.1`, `z = +6.2` — which also fires 1-3s, giving R11 not R10. **R10 as written is unreachable at default limits**; it becomes reachable under a limit set where `RangeSd < RejectSd − WarningSd`. Author R10 only as a *configured-limits* case and label it accordingly.
- **R16/R18/R19/R20 need `R-4s` and `10-x` simultaneously**: all window values on the same side of the mean *and* a consecutive span > 4SD. Reachable with e.g. nine priors at z ≈ +0.2 and a tenth at z ≈ +4.3 (span 4.1, all positive) — that tenth also fires 1-3s, giving R18. **R16 is unreachable at default limits**; treat it the same way as R10.
- The `4-1s` rule is **not implemented** and appears in no row of this table. It is recorded as GAP-QC-015 and must **not** be matrixed, scripted or executed as if present.

### DT-QC-3 — `WestgardLimits.Validated()` limits-validation table

Evaluated in source order (`WestgardEvaluator.cs:31-52`); the **first** failing rule throws, so a set with two faults reports only the earlier code.

| # | `WarningSd` | `RejectSd` | `RangeSd` | `RunLength` | Result | Code | Message | Line |
|---|---|---|---|---|---|---|---|---|
| L1 | 2 | 3 | 4 | 10 | accepted (the shipped default) | — | — | `:28` |
| L2 | ≤ 0 | any | any | any | rejected | `QC-LIM-001` | `Westgard SD limits must be positive.` | `:33-37` |
| L3 | any | ≤ 0 | any | any | rejected | `QC-LIM-001` | same | `:33-37` |
| L4 | any | any | ≤ 0 | any | rejected | `QC-LIM-001` | same | `:33-37` |
| L5 | 3 | 2 | 4 | 10 | rejected (`WarningSd > RejectSd`) | `QC-LIM-002` | `The warning limit must be below the rejection limit.` | `:39-43` |
| L6 | 3 | 3 | 4 | 10 | rejected (`>=`, equality is a failure) | `QC-LIM-002` | same | `:39-43` |
| L7 | 2 | 3 | 4 | 1 | rejected | `QC-LIM-003` | `The shift-rule run length must be at least 2.` | `:45-49` |
| L8 | 2 | 3 | 4 | 0 or negative | rejected | `QC-LIM-003` | same | `:45-49` |
| L9 | 2 | 3 | 4 | 2 | **accepted** — the minimum legal run length; the shift rule then needs only 1 prior and emits the label `2-x` | — | — | `:45-49` |
| L10 | −1 | 2 | 4 | 1 | rejected with **`QC-LIM-001`**, not `QC-LIM-003` — ordering matters | `QC-LIM-001` | — | `:33-49` |
| L11 | 2 | 3 | 4 | 14 | **accepted by `Validated()`** but the shift rule can never fire: `RunLength − 1 = 13 > WindowSize 12` | — | — | `:45-49` + `QcSlice.cs:48` (GAP-QC-006) |
| L12 | 2 | 3 | 0.5 | 10 | accepted — `RangeSd` is **not** required to exceed `RejectSd`, so R-4s fires on almost every pair | — | — | `:39-49` (absence of a rule) |

L2–L8 and L10 are **startup-failure** cases (the throw happens inside `AddSingleton` at `DependencyInjection.cs:86-90`), not API cases. L11 and L12 are silent-misconfiguration cases with no error at all — the highest-severity findings in this table.

### DT-QC-4 — Endpoint decision table (authorization and outcome)

| # | Route | Actor | Has `analytical-quality.manage` | Role is `ExternalAuditor` | Expected | Code |
|---|---|---|---|---|---|---|
| E1 | `POST /api/qc/profiles` | authenticated | Y | N | `200` `{ id }` | — |
| E2 | `POST /api/qc/profiles` | authenticated | N | N | `403` problem+json | endpoint filter (`RequirePermission`) |
| E3 | `POST /api/qc/profiles` | authenticated | Y | Y | `403` problem+json | `AUTHZ-002` |
| E4 | `POST /api/qc/profiles` | anonymous | – | – | `401` | framework `[Authorize]` |
| E5 | `PUT …/targets` | authenticated | N | N | `403` | endpoint filter |
| E6 | `PUT …/targets`, unknown id | authenticated | Y | N | `404` | `QC-404` |
| E7 | `PUT …/targets`, blank reason | authenticated | Y | N | `400` — FluentValidation intercepts before the domain | validator `Reason.NotEmpty` (`QcSlice.cs:83`) |
| E8 | `PUT …/targets`, `TargetSd = 0` | authenticated | Y | N | `400` — validator, **not** `QC-002` | validator `TargetSd.GreaterThan(0)` (`QcSlice.cs:82`) |
| E9 | `POST …/runs` | authenticated | **N** | N | **`200` `{ runId }`** — no permission required | — |
| E10 | `POST …/runs` | authenticated | – | Y | `403` | `AUTHZ-002` |
| E11 | `POST …/runs`, unknown profile | authenticated | – | N | `404` | `QC-404` |
| E12 | `POST /api/qc/runs/{id}/troubleshoot` on an `InControl` run | authenticated | – | N | **`409`** | `QC-010` |
| E13 | `…/troubleshoot` with a blank note | authenticated | – | N | `400` — validator fires first | `Note.NotEmpty` (`QcSlice.cs:107`) |
| E14 | `…/troubleshoot` with a 2001-char note | authenticated | – | N | `400` | `Note.MaximumLength(2000)` |
| E15 | `GET /api/qc/profiles` | authenticated | **N** | Y | `200` — queries are not policy-gated | — |
| E16 | `GET …/runs?take=0` | authenticated | – | N | `200`, exactly 1 row | `Math.Clamp(q.Take, 1, 500)` (`QcSlice.cs:144`) |
| E17 | `GET …/runs?take=10000` | authenticated | – | N | `200`, at most 500 rows | same |

E7/E8/E13/E14 are the trap rows: the domain codes `QC-002`, `QC-011`, `QC-012` are **shadowed by FluentValidation** on the HTTP path and are only reachable at the domain-unit level. Any API case that expects `422 QC-012` will fail; write it as `400` with an `errors` dictionary keyed on `Reason`.

---

## 6. UAT scenarios (Gherkin)

Business-readable, traced to URS-035/036/037 (`docs/validation/01-User-Requirements-Specification.md:83-85`) and URS-015 (`:43`). Each is a candidate for the reserved `TC-QC-UAT-0nn` block; none is executed here.

```gherkin
Feature: Statistical quality control with Westgard multi-rule evaluation
  As a Quality Manager in an ISO 15189 laboratory
  I want every control measurement graded automatically and the verdict preserved
  So that release decisions are defensible on inspection

  Background:
    Given I am signed in to the "demo-lab" workspace
    And a QC profile exists for analyte "Glucose" on instrument "Cobas-1"
      with control lot "LOT-778", target mean 100 and target SD 5

  Scenario: UAT-1 — An in-control result is accepted without comment
    When the analyst records a control value of 103
    Then the run is graded "InControl"
    And no Westgard rule is listed against it
    And the Levey-Jennings point is plotted in green between the mean and +1 SD

  Scenario: UAT-2 — A borderline result is flagged as a warning but not rejected
    When the analyst records a control value of 111
    Then the run is graded "Warning"
    And the only rule listed is "1-2s"
    And no troubleshooting note is requested
    And the Levey-Jennings point is plotted in amber

  Scenario: UAT-3 — A gross outlier is rejected on the single-value rule
    When the analyst records a control value of 116
    Then the run is graded "OutOfControl"
    And the rules listed include "1-3s"
    And the run row offers a troubleshooting action

  Scenario: UAT-4 — Two consecutive high results reject on the 2-2s rule
    Given the most recent run for the profile has a value of 112
    When the analyst records a control value of 111
    Then the run is graded "OutOfControl"
    And the rules listed include "2-2s"

  Scenario: UAT-5 — A drift across the range rejects on R-4s
    Given the most recent run for the profile has a value of 88
    When the analyst records a control value of 112
    Then the run is graded "OutOfControl"
    And the rules listed include "R-4s"

  Scenario: UAT-6 — A sustained shift rejects on the ten-point rule
    Given the nine most recent runs for the profile all sit above the target mean
    When the analyst records a further value above the target mean
    Then the run is graded "OutOfControl"
    And the rules listed include "10-x"

  Scenario: UAT-7 — An out-of-control run must be documented before it is considered closed
    Given a run for the profile is graded "OutOfControl" and has no troubleshooting note
    When the analyst submits the note "Reagent lot changed; control repeated and recovered"
    Then the note is stored against that run
    And the run row shows the resolved marker instead of the troubleshooting action

  Scenario: UAT-8 — A troubleshooting note cannot be attached to a run that was never rejected
    Given a run for the profile is graded "InControl"
    When a troubleshooting note is submitted against that run
    Then the request is refused with a conflict
    And the refusal carries the code "QC-010"

  Scenario: UAT-9 — Changing a QC target demands a documented reason
    Given I hold the "analytical-quality.manage" privilege
    When I change the target mean to 102 and the target SD to 4.8 without giving a reason
    Then the change is refused and the reason field is reported as required

  Scenario: UAT-10 — Historical verdicts survive a target change
    Given the profile has runs already graded against target mean 100 and SD 5
    When I change the target mean to 120 with the reason "New control lot LOT-901"
    Then the stored grade and z-score of every earlier run are unchanged
    And the change is visible in the record's audit trail with old and new values

  Scenario: UAT-11 — QC acceptance limits are a controlled parameter
    Given the deployment is configured with a warning limit of 2 SD and a rejection limit of 3 SD
    When an administrator configures a warning limit of 3 SD and a rejection limit of 2 SD
    Then the application refuses to start
    And the refusal identifies the QC limit rule that was violated

  Scenario: UAT-12 — An out-of-control run is visible to the reviewer on the chart
    Given the profile has a mix of in-control, warning and out-of-control runs
    When the Quality Manager opens the profile
    Then each point on the Levey-Jennings chart is coloured by its recorded grade
    And hovering a point reveals its value, z-score, grade and the rules that fired
```

Scenario UAT-12's hover text is asserted against the `<title>` element built at `levey-jennings-chart.component.ts` (`points` computed): `` `${r.value} (z=${r.zScore}) — ${r.outcome}${r.violatedRules ? ' · ' + r.violatedRules : ''}` ``.

---

## 7. Exploratory charters

Time-boxed, session-based. Each charter states its target, the oracle it will reason against, and what it must **not** assume. Reserved ids `TC-QC-EXPL-001` … `008`.

**CH-1 — Boundary sweep of the four limit comparisons.** *Explore* `WestgardEvaluator.Evaluate` *with* decimal values placed exactly on, one ULP below and one ULP above each of `RejectSd`, `WarningSd`, `RangeSd` *to discover* whether the strict `>` comparisons behave identically for positive and negative z, and whether decimal arithmetic in `(value − mean) / sd` introduces any representation surprise at the boundary. **Oracle:** `WestgardEvaluator.cs:87, 93, 96, 106, 128`. **Do not assume** that a value "at 3 SD" rejects — it does not. 90 minutes.

**CH-2 — Window-length and ordering sensitivity.** *Explore* `RecordQcRunHandler`'s prior-window query *with* run counts of 0, 1, 8, 9, 12, 13 and 40, and with several runs sharing an identical `MeasuredAtUtc` *to discover* how the 10-x and 2-2s verdicts change with window truncation at `WindowSize = 12` and with the untie-broken `OrderByDescending`. **Oracle:** `QcSlice.cs:48, 55-61`; `WestgardEvaluator.cs:113-120`. 120 minutes; requires live PostgreSQL.

**CH-3 — Configured-limit label and grading drift.** *Explore* the `AnalyticalQuality:Westgard:*` surface *with* limit sets `(1, 2, 4, 8)`, `(2, 2.5, 3, 2)`, `(2, 3, 0.5, 10)` and `(2, 3, 4, 14)` *to discover* which emitted rule labels become ambiguous, which rules become unfireable, and whether any set passes `Validated()` while producing clinically nonsensical grading. **Oracle:** the label interpolations at `WestgardEvaluator.cs:89, 98, 108, 118, 130` and DT-QC-3 rows L9, L11, L12. **Do not assume** `Validated()` catches a bad set — L11 and L12 pass. 90 minutes.

**CH-4 — Verdict-freeze integrity across a target change.** *Explore* the interaction of `UpdateQcTargetsCommand` and subsequent `RecordQcRunCommand` calls *with* a large mean shift (100 → 140) applied mid-series *to discover* whether earlier rows' `z_score`, `outcome` and `violated_rules` remain byte-identical, and how the recomputed prior window distorts the first few post-change verdicts. **Oracle:** `QcProfile.cs:92-97` (the freeze contract), `QcSlice.cs:55-64` (the recomputation), `WestgardEvaluator.cs:82`. 90 minutes; requires live PostgreSQL and SQL-level before/after capture.

**CH-5 — Authorization surface of the ungated QC routes.** *Explore* all six `api/qc` routes and their `/api/v{version}/…` mirrors *with* a tenant user holding **no** `analytical-quality.*` privilege, and separately with an `ExternalAuditor` *to discover* exactly which QC operations a privilege-less internal user can perform, and whether the `v{version}` mirror gates identically to the unversioned route. **Oracle:** `AnalyticalQualityControllers.cs:16-48`; `AuthorizationBehavior.cs:72-85`; `ApiSurface.approved.txt:97-644`. 90 minutes.

**CH-6 — Levey-Jennings rendering under adversarial data.** *Explore* the chart component *with* a single run, 500 runs, all-identical values, z-scores beyond ±4 SD (clamped), and a `violatedRules` string containing every rule *to discover* rendering, legibility, tooltip and axe-a11y failures, and whether the ±4 SD frame silently misrepresents a gross outlier. **Oracle:** `levey-jennings-chart.component.ts` (`yFor`, `clamp`, `points`, `colourFor`). 60 minutes; browser session.

---

## 8. Gap Register (this module)

Full nine-field format. Severity scale: **Critical / High / Medium / Low**. Every gap below was derived from source read in this pass; none is speculative.

---

### GAP-QC-001 — `QcOutOfControl` event payload carries an all-zero `TenantId`

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/AnalyticalQuality/QcProfile.cs:134` (`run.Raise(new QcOutOfControl(run.Id, profileId, run.ViolatedRules, run.TenantId))` inside the static `Record` factory); `src/NT.QAMS.Infrastructure/Persistence/Interceptors/TenantStampInterceptor.cs:39-54`; `src/NT.QAMS.Infrastructure/Persistence/Interceptors/OutboxInterceptor.cs:55-68` |
| **Description** | The event is constructed inside the factory, before the entity is tracked and therefore before `TenantStampInterceptor` assigns `TenantId` at `SavingChanges`. The record's `TenantId` member is consequently `Guid.Empty` and is serialised as `"00000000-0000-0000-0000-000000000000"` into `outbox_event.payload`. The outbox **row's** `tenant_id` column is correct, because `OutboxInterceptor` reads it from the entity after stamping and is registered second (`DependencyInjection.cs:60, 62`). |
| **Impact** | Any future consumer that trusts the payload's `TenantId` (rather than the row's column) would resolve the wrong tenant or none. Today the impact is latent because no consumer exists (GAP-QC-014); it becomes live the moment one is added. |
| **Testing limitation** | Cannot be asserted through the HTTP surface — it requires reading `outbox_event.payload` in SQL and comparing it with `outbox_event.tenant_id`. An integration case is the only viable level. |
| **Recommended clarification** | Product/architecture to confirm whether domain-event payloads are required to be self-describing (carry their own tenant) or whether the outbox row's column is the single source of truth; if the former, `QcOutOfControl` must be raised after stamping or the tenant must be supplied to the factory. |
| **Suggested acceptance criteria** | For every `qams.outbox_event` row whose `event_type` resolves to `QcOutOfControl`, `payload ->> 'tenantId'` equals `tenant_id::text` and is not the nil UUID. |
| **Severity** | Medium |
| **Responsible role** | Backend architect |

---

### GAP-QC-002 — R-4s fires on same-sided pairs, contradicting its own specification comment

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/AnalyticalQuality/WestgardEvaluator.cs:102` (comment: *"R-4s: this value and the immediately prior one span more than RangeSd (opposite sides)"*) versus `:106` (`if (Math.Abs(z - prev) > lim.RangeSd)`); XML summary at `:60` |
| **Description** | The coded predicate is an unsigned span test with no sign check, unlike the 2-2s rule which does test `Math.Sign` at `:96`. A pair such as `prev z = +4.5`, `z = +0.2` (span 4.3, both positive) fires `R-4s` even though the classical Westgard R-4s is a within-run range rule between values on opposite sides of the mean. |
| **Impact** | False rejections on a recovering series: a control that was grossly high and returns to target triggers a second rejection on the *recovery* measurement. Under a configured `RangeSd < RejectSd − WarningSd` the false-rejection rate rises sharply. |
| **Testing limitation** | Cannot be recorded as pass or fail until the intended semantics are fixed: the code and its own comment disagree, so there is no authoritative oracle. Cases must be authored `[GD]` against this gap. |
| **Recommended clarification** | Quality/laboratory-science owner to state whether R-4s is intended as a signed opposite-sides range rule (per Westgard's original definition) or as an unsigned span rule, and the comment or the code corrected to match. |
| **Suggested acceptance criteria** | Given targets mean 100 / SD 5, prior value 122.5 (z = +4.5) and current value 101 (z = +0.2), the returned `ViolatedRules` **[decision required]** either contains `R-4s` (unsigned semantics confirmed, comment corrected) or does not (signed semantics confirmed, `Math.Sign(prev) != Math.Sign(z)` added at `:106`). |
| **Severity** | High |
| **Responsible role** | Quality Manager (scientific owner) with Backend developer |

---

### GAP-QC-003 — QC target effective dating is recorded but cannot be scheduled; `QC-013` is unreachable via the API

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/AnalyticalQuality/QcProfile.cs:65, 77-80, 85`; `src/NT.QAMS.Application/AnalyticalQuality/QcSlice.cs:75, 94`; `src/NT.QAMS.Contracts/AnalyticalQuality/AnalyticalQualityContracts.cs:9`; migration `20260726190957_QcTargetEffectiveDating.cs:22-27` |
| **Description** | `QcProfile.UpdateTargets` takes an `effectiveFrom` parameter and enforces forward-only ordering with `QC-013`, but `UpdateQcTargetsHandler` always passes `clock.UtcNow` (`QcSlice.cs:94`) and neither the command nor the request contract exposes a date. The stored `target_effective_from_utc` therefore always equals the moment of the change, never a scheduled future date, and `QC-013` cannot be triggered over HTTP because the clock is monotonically non-decreasing. Additionally, `RecordQcRunHandler` never compares `TargetEffectiveFromUtc` with the run time (`QcSlice.cs:50-71`), so even a future-dated target would be applied immediately. |
| **Impact** | URS-037 (`docs/validation/01-User-Requirements-Specification.md:85`) requires target changes to be *"forward-only (effective-dated)"*. Only the "reason" and "does not disturb history" halves are demonstrably met; the effective-dating half is a recorded timestamp, not an activation schedule. A lab cannot pre-load next month's control-lot targets. |
| **Testing limitation** | `QC-013` can be exercised only at the domain-unit level or by injecting a rewound `IClock`; it must not be written as an API case. No test can demonstrate scheduled activation because the capability does not exist. |
| **Recommended clarification** | Product owner to confirm whether URS-037 requires (a) an audit timestamp only — in which case the URS wording should drop "effective-dated" — or (b) genuine forward scheduling, in which case `UpdateQcTargetsRequest` needs an `EffectiveFrom` field and `RecordQcRunHandler` needs to select the target set in force at `MeasuredAtUtc`. |
| **Suggested acceptance criteria** | Interpretation (b): `PUT /api/qc/profiles/{id}/targets` accepts `effectiveFrom`; a target dated `now + 1 day` leaves runs recorded today graded against the **old** targets; a second change dated before the first is refused with `422` and code `QC-013`. |
| **Severity** | High |
| **Responsible role** | Product owner with Quality Manager |

---

### GAP-QC-004 — Sigma-based QC recommendations cite rules the evaluator cannot enforce

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/AnalyticalQuality/SigmaAssessment.cs:114-121` (`QcRecommendation` returns strings naming `4:1s` at ≥4σ and `4:1s / 8:x` at ≥3σ); `src/NT.QAMS.Domain/AnalyticalQuality/WestgardEvaluator.cs:86-131` (neither rule implemented) |
| **Description** | The sigma assessment tells the laboratory to run a rule set the QC engine does not contain, and the recommendation is free text with no wiring to `WestgardLimits` — accepting it changes nothing about how runs are graded. |
| **Impact** | A laboratory that documents its QC design from `QcRecommendation` will believe 4:1s and 8:x are being applied when they are not. For a ≥3σ / <4σ method this is a real detection gap, and an inspector comparing the documented QC design with the implemented rules would find them inconsistent. |
| **Testing limitation** | Cannot be tested as a functional defect in either aggregate individually — each behaves as coded. It is only observable as a cross-aggregate consistency check, which has no acceptance criterion until the intended relationship is defined. |
| **Recommended clarification** | Quality Manager to decide whether `QcRecommendation` should be constrained to the implemented rule set, or whether the missing rules should be implemented (see GAP-QC-015), or whether accepting a recommendation should drive a per-profile limit set (see GAP-QC-005). |
| **Suggested acceptance criteria** | Every rule name appearing in any `QcRecommendation` branch is producible by `WestgardEvaluator.Evaluate` under some validated `WestgardLimits`, verified by an architecture test that parses the recommendation strings. |
| **Severity** | Medium |
| **Responsible role** | Quality Manager with Backend architect |

---

### GAP-QC-005 — Westgard limits are process-wide, not per-tenant, per-analyte or per-profile

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Infrastructure/DependencyInjection.cs:86-90` (`services.AddSingleton(new WestgardLimits(...).Validated())`); `src/NT.QAMS.Application/AnalyticalQuality/QcSlice.cs:45` (the singleton is injected into every handler invocation regardless of tenant) |
| **Description** | A single `WestgardLimits` instance is resolved once at startup from `AnalyticalQuality:Westgard:*` and serves every tenant, every analyte and every control level. `QcProfile` carries no limit-override columns, and no per-tenant setting exists. |
| **Impact** | In a multi-tenant SaaS, one laboratory cannot adopt a 2.5 SD warning or an 8-x shift rule without changing grading for every other tenant on the deployment. It also blocks the sigma-driven QC design of GAP-QC-004, which is inherently per-method. |
| **Testing limitation** | Multi-tenant limit-divergence cases cannot be authored at all — there is no mechanism to configure two tenants differently. Cases are limited to proving the single-set behaviour and documenting the constraint. |
| **Recommended clarification** | Product owner to confirm whether per-tenant (or per-profile) QC limits are in scope for the regulated product; if so, whether the override lives on `TenantSettings` or on `QcProfile`. |
| **Suggested acceptance criteria** | Two tenants configured with different warning limits grade the same control value differently, and each tenant's stored verdict cites the label derived from its own limit set. |
| **Severity** | Medium |
| **Responsible role** | Product owner with Backend architect |

---

### GAP-QC-006 — `RunLength` has no upper bound, so a configured shift rule can be silently unfireable

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/AnalyticalQuality/WestgardEvaluator.cs:45-49` (`Validated()` checks only `RunLength < 2`); `:113` (`if (priorZ.Count >= lim.RunLength - 1)`); `src/NT.QAMS.Application/AnalyticalQuality/QcSlice.cs:48` (`public const int WindowSize = 12`) |
| **Description** | The handler supplies at most 12 prior values, so the shift rule requires `RunLength − 1 ≤ 12`, i.e. `RunLength ≤ 13`. `Validated()` accepts any `RunLength ≥ 2`. Configuring `AnalyticalQuality:Westgard:RunLength = 20` starts the application cleanly and disables the shift rule permanently, with no log line, no warning and no error. |
| **Impact** | A silent loss of a rejection rule in a regulated QC engine. The laboratory's documented QC design would claim a 20-point shift rule that never fires. |
| **Testing limitation** | The failure has no observable error signal, so it can only be detected by a positive test that constructs a 20-run same-side series and asserts the rule fires — which will fail, correctly, and must be recorded as `[GD]` against this gap rather than as a defect verdict. |
| **Recommended clarification** | Backend architect to decide whether `Validated()` should reject `RunLength > WindowSize + 1`, or whether `WindowSize` should be derived from `RunLength` (e.g. `Math.Max(12, RunLength - 1)`). |
| **Suggested acceptance criteria** | Startup fails with a distinct code when `RunLength − 1` exceeds the prior-window size; **or** the prior-window size is derived from `RunLength` such that a same-side series of length `RunLength` always fires the shift rule for any validated limit set. |
| **Severity** | High |
| **Responsible role** | Backend architect |

---

### GAP-QC-007 — `RecordQcRunCommand` has no validator

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Application/AnalyticalQuality/QcSlice.cs:43` (command) — no `AbstractValidator<RecordQcRunCommand>` exists in the file or anywhere in `src`, unlike `CreateQcProfileValidator` (`:16`), `UpdateQcTargetsValidator` (`:78`) and `LogQcTroubleshootingValidator` (`:103`) |
| **Description** | `Value` is an unbounded `decimal` and `Operator` an unbounded, optional string. A blank operator is silently replaced with the literal `"unknown"` (`QcProfile.cs:128`); an operator longer than the `varchar(150)` column reaches PostgreSQL and fails as a database error rather than a validation problem; an extreme `Value` can overflow `numeric(18,6)` or the `numeric(10,3)` z-score column. |
| **Impact** | Part 11 attribution is weakened — `"unknown"` is a legitimate stored operator value for a regulated QC record. Malformed input surfaces as a 500-class database error instead of a 400 with a field-keyed `errors` dictionary. |
| **Testing limitation** | The expected status code for an oversized operator or an overflowing value is undefined; cases must be written `[GD]` until the validator exists or the intended failure mode is stated. |
| **Recommended clarification** | Backend developer to add `RecordQcRunValidator` with `Operator` NotEmpty / MaximumLength(150) and a documented bound on `Value`, or to state explicitly that `"unknown"` is an accepted attribution for QC runs. |
| **Suggested acceptance criteria** | `POST /api/qc/profiles/{id}/runs` with an empty `operator` returns `400` with `errors.Operator`; with a 151-character operator returns `400`, not a database error; the string `"unknown"` never appears in `qams.qc_run.operator` for a run created through the API. |
| **Severity** | Medium |
| **Responsible role** | Backend developer |

---

### GAP-QC-008 — Prior-window ordering has no tie-breaker

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Application/AnalyticalQuality/QcSlice.cs:57` (`.OrderByDescending(r => r.MeasuredAtUtc)`); `:67` (`clock.UtcNow` is the only source of `MeasuredAtUtc`, `QcProfile.cs:129`) |
| **Description** | Runs recorded within the same clock resolution share a `measured_at_utc` value, and the query specifies no secondary sort. PostgreSQL may return them in any order, which changes which value becomes `priorZ[^1]` — the sole input to both the 2-2s and R-4s rules. |
| **Impact** | Non-deterministic grading for rapidly-entered runs (batch entry, replayed imports, load tests). Two evaluations of identical data can produce different stored verdicts, which is indefensible for a regulated record. |
| **Testing limitation** | Non-determinism cannot be asserted as pass or fail by a single execution; detecting it requires a repeated-execution or explicit-ordering probe, and any single-shot case would give a misleading green. |
| **Recommended clarification** | Backend developer to confirm the intended tie-break (`ThenByDescending(r => r.Id)` gives a stable, creation-ordered result because ids are UUIDv7) and whether `MeasuredAtUtc` should be caller-supplied rather than clock-derived. |
| **Suggested acceptance criteria** | Ten runs inserted with an identical `measured_at_utc` and distinct values produce the same `outcome` and `violated_rules` on every one of 20 consecutive evaluations of an eleventh run. |
| **Severity** | Medium |
| **Responsible role** | Backend developer |

---

### GAP-QC-009 — A QC profile cannot be retired, and a retired profile still accepts runs

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/AnalyticalQuality/QcProfile.cs:88` (`Deactivate()` — a repository-wide search finds no caller in `src`); `src/NT.QAMS.Application/AnalyticalQuality/QcSlice.cs:50-71` (`RecordQcRunHandler` never reads `IsActive`); `frontend/src/app/features/analytical/qc-profile-detail.component.ts` (the workflow stepper renders `Active` / `Retired`) |
| **Description** | `IsActive` is created `true`, exposed in `QcProfileDto` and rendered as a two-state stepper, but there is no command, no endpoint and no caller that can set it to `false`. `GetQcProfilesHandler` (`QcSlice.cs:128-132`) also returns inactive profiles unfiltered. Even if the flag could be cleared, run recording would be unaffected. |
| **Impact** | Retired control lots cannot be closed out; the UI advertises a lifecycle state the system cannot reach. Dead code in a regulated domain aggregate also contradicts the project standing rule against dead code (`CLAUDE.md` §2.3). |
| **Testing limitation** | The `Retired` branch of the UI stepper and the whole inactive-profile path are untestable end to end. Only a domain-unit case can reach `Deactivate()`. |
| **Recommended clarification** | Product owner to confirm whether profile retirement is required; if yes, a `DeactivateQcProfileCommand` plus a `DELETE`- or `PUT`-shaped endpoint gated on `analytical-quality.manage`, and a decision on whether recording against a retired profile is refused (needing a new error code) or merely warned. |
| **Suggested acceptance criteria** | A profile can be retired through an authorised endpoint; the list endpoint can filter by active state; recording a run against a retired profile is refused with a documented `QC-*` code and HTTP `422`. |
| **Severity** | Medium |
| **Responsible role** | Product owner with Backend developer |

---

### GAP-QC-010 — No frontend path to change QC targets

| Field | Content |
|---|---|
| **Source reference** | `frontend/src/app/core/api/analytical-api.service.ts:23-45` (methods `qcProfiles`, `createQcProfile`, `qcRuns`, `recordQcRun`, `troubleshootRun` — no targets method); `frontend/src/app/features/analytical/qc.facade.ts` (no target-update action); `src/NT.QAMS.WebApi/Controllers/AnalyticalQualityControllers.cs:27-33` (the endpoint exists) |
| **Description** | `PUT /api/qc/profiles/{id}/targets` is implemented, permission-gated, validated and in the approved API surface (`ApiSurface.approved.txt:632, 644`), but no client code calls it. The only way to change a QC target is a direct API call. |
| **Impact** | URS-037 and URS-015 (reason-for-change on QC target changes) cannot be satisfied by a user of the product; the Part 11 reason is collected only by whoever crafts the HTTP request. |
| **Testing limitation** | No E2E or UI-level case can be authored for target change; coverage is limited to the API level, and the accessible-reason-capture pattern used elsewhere in the SPA cannot be verified here. |
| **Recommended clarification** | Product owner / frontend lead to confirm whether target maintenance is an intended UI capability for this release and, if so, whether the reason is collected by the shared change-reason dialog or by a dedicated form field. |
| **Suggested acceptance criteria** | A user holding `analytical-quality.manage` can change a profile's mean and SD from the profile detail page, is required to supply a reason before the request is sent, and sees the change reflected in the embedded audit trail. |
| **Severity** | High |
| **Responsible role** | Product owner with Frontend developer |

---

### GAP-QC-011 — Levey-Jennings guide lines and frame are hard-coded and ignore the configured limits

| Field | Content |
|---|---|
| **Source reference** | `frontend/src/app/features/analytical/levey-jennings-chart.component.ts` — the `guides` array is a literal ±1/2/3 SD set, `yFor` maps a fixed ±4 SD frame, and `clamp` truncates z to ±4; no configuration is read |
| **Description** | The chart draws warning and rejection guides at 2 SD and 3 SD regardless of `AnalyticalQuality:Westgard:WarningSd` / `RejectSd`, and clips every point to ±4 SD. Under a non-default limit set the visual thresholds no longer match the thresholds the engine applied, and a z = +12 rejection is drawn at the same height as a z = +4.1 one. |
| **Impact** | A reviewer reading the chart can reach a different conclusion from the one the engine recorded — the chart is the primary review artefact for QC and is likely to be printed as evidence. Clamping also hides the magnitude of gross failures. |
| **Testing limitation** | Any UI case asserting "the point sits above the +3 SD line" is only valid for the default limit set; such cases must state that dependency explicitly, and no case can currently verify guide lines against configured limits. |
| **Recommended clarification** | Frontend lead / Quality Manager to decide whether the limits must be exposed to the SPA (e.g. via a QC settings endpoint) so the chart derives its guides, and whether clamped points must carry an out-of-frame indicator. |
| **Suggested acceptance criteria** | The chart's warning and rejection guide lines are rendered at the configured `WarningSd` and `RejectSd`; a point whose z exceeds the frame is drawn with a distinct out-of-range marker and its true z is stated in the tooltip. |
| **Severity** | Medium |
| **Responsible role** | Frontend developer with Quality Manager |

---

### GAP-QC-012 — No foreign key from `qc_run.profile_id` to `qc_profile`

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Infrastructure/Persistence/Configurations/AnalyticalQualityConfigurations.cs:24-39` (no `HasOne`/`WithMany`); migration `20260721225752_AnalyticalQuality.cs:64-85` (no `ForeignKey` on `qc_run`); no later migration adds one |
| **Description** | The parent/child relationship is enforced only by the handler's `QC-404` lookup (`QcSlice.cs:52-53`). The database will accept a `qc_run` row whose `profile_id` matches no profile, and deleting a profile leaves orphan runs. This diverges from the project convention that cross-aggregate FKs are tenant-composite (`CLAUDE.md` §5). |
| **Impact** | Orphan QC runs are structurally possible, and the tenant-composite FK protection that makes a cross-tenant child impossible elsewhere is absent here. Data-integrity defence rests entirely on application code. |
| **Testing limitation** | A negative case can prove the orphan is accepted, but there is no defined expected behaviour to assert against — the schema-hardening standard implies an FK should exist; the migrations show none. |
| **Recommended clarification** | Database architect to confirm whether the omission is deliberate (the aggregates are intentionally decoupled for write throughput, per the comment at `QcProfile.cs:8-9`) or an oversight in the Phase-5 composite-FK sweep. |
| **Suggested acceptance criteria** | Either a tenant-composite FK `FOREIGN KEY (profile_id, tenant_id) REFERENCES qams.qc_profile (id, tenant_id)` exists and an orphan insert is refused by PostgreSQL, or the deliberate omission is documented as an accepted deviation with its compensating control named. |
| **Severity** | Medium |
| **Responsible role** | Database architect |

---

### GAP-QC-013 — The "frozen" QC verdict has no database-level immutability

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/AnalyticalQuality/QcProfile.cs:92-97` (the freeze contract, in prose); migration `20260726084134_SignedRecordImmutability.cs` — the `Frozen` list covers the twelve analytical study roots plus `uncertainty_budget` and does **not** include `qc_run` or `qc_profile` |
| **Description** | Immutability of `outcome`, `violated_rules`, `z_score` and `value` is guaranteed only by the absence of a mutating method on the aggregate. There is no `frozen_immutability` trigger on `qams.qc_run`, so a direct `UPDATE` or `DELETE` by any role with table privileges succeeds. `ck_qc_run_outcome_domain` constrains the *value set* of `outcome` but not its mutability. |
| **Impact** | URS-041 extends database-layer immutability to signed analytical studies but not explicitly to QC runs, yet a QC verdict is the record an analyst acted on and is equally a Part 11 §11.10(c) record. The application-layer-only guarantee is weaker than the one the same codebase applies to studies. |
| **Testing limitation** | A `[GD]` case can demonstrate the `UPDATE` succeeds, but recording that as a *failure* presumes a requirement that URS-041 does not currently state for QC. The requirement gap must be closed before an acceptance verdict is possible. |
| **Recommended clarification** | Quality Manager / regulatory owner to confirm whether a recorded QC verdict is an immutable regulated record; if yes, whether the trigger should freeze the whole row (blocking the legitimate `troubleshooting_note` write) or only the verdict columns. |
| **Suggested acceptance criteria** | `UPDATE qams.qc_run SET outcome = 'InControl' WHERE …` is refused by a database trigger for every role including the runtime role, while `UPDATE … SET troubleshooting_note = …` on an out-of-control run continues to succeed. |
| **Severity** | High |
| **Responsible role** | Quality Manager with Database architect |

---

### GAP-QC-014 — `QcOutOfControl` has no subscriber: no nonconformance, no notification, no task

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/AnalyticalQuality/QcProfile.cs:134, 157`; `src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:70-116` (handlers exist for `CalibrationDue`, `EquipmentLockedOut`, `HighImpartialityRiskDeclared`, `ReferenceStandardExpired`, `CompetencyExpired`, `HighResidualRisk`, `SupplierSuspended`, `EscalationTriggered` — not for `QcOutOfControl`); a repository-wide search for `QcOutOfControl` returns only the declaration, the `Raise`, and one doc comment |
| **Description** | The event is raised and durably written to the outbox in the same transaction as the run, then delivered to nobody. Compare `PtUnsatisfactory`, which is consumed by `PtToNcPolicy` (`src/NT.QAMS.Application/AnalyticalQuality/PtToNcPolicy.cs`). No nonconformance is raised, no notification is dispatched, no task is created, and nothing gates result release. |
| **Impact** | An out-of-control QC event — the canonical trigger for investigation under ISO 15189 §7.3 and ISO 17025 §7.7 — produces no workflow. Detection is entirely pull-based: someone must open the profile page. The comment at `QcProfile.cs:94-96` states the release gate "lives at the LIMS boundary, out of scope", but the *internal* escalation is also absent. |
| **Testing limitation** | Every QC case's **Expected Notification** field must read `n/a — no handler is subscribed to QcOutOfControl`; no notification, NC-creation or escalation case can be authored for this module until a policy exists. |
| **Recommended clarification** | Quality Manager to specify the required reaction: raise a `Nonconformance` of type `OutOfSpecification`, notify a role, create a task, or all three — and whether the reaction is unconditional or depends on which rules fired. |
| **Suggested acceptance criteria** | Recording an out-of-control run produces, in the same tenant, exactly one downstream artefact of the specified type within one outbox-processing cycle, carrying the run id, the profile id and the rule list; an in-control or warning run produces none. |
| **Severity** | **Critical** |
| **Responsible role** | Quality Manager with Backend architect |

---

### GAP-QC-015 — The 4-1s rule (and 8-x, 2of3-2s, 7-T) is not implemented

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Domain/AnalyticalQuality/WestgardEvaluator.cs:86-131` — exhaustive; the only rules present are 1-3s, 2-2s, R-4s, 10-x and the 1-2s warning. Also `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md:100` records the same finding |
| **Description** | The commissioning brief's rule set includes 4-1s (four consecutive results beyond the same ±1SD limit). The evaluator has no rule that inspects a run of four, and no 1SD-derived limit constant exists in `WestgardLimits`. The related shift/trend rules 8-x, 2of3-2s and 7-T are likewise absent. |
| **Impact** | Reduced sensitivity to systematic error. A method with a sustained 1.5 SD bias produces neither a rejection nor a warning until it reaches the 10-x threshold, which needs ten consecutive results. `SigmaAssessment.QcRecommendation` compounds this by advertising 4:1s (GAP-QC-004). |
| **Testing limitation** | **No 4-1s execution case may be written.** Per the ground-truth file and the honesty rules, an absent rule is a gap, not a failing test. Coverage of this rule is deferred in its entirety. |
| **Recommended clarification** | Quality Manager to confirm which Westgard rules the product commits to; Backend architect to size the addition, noting that a 4-1s rule needs a new `ShiftSd` limit constant and a corresponding `QC-LIM-*` validation rule. |
| **Suggested acceptance criteria** | With targets mean 100 / SD 5 and prior values 106, 107, 106 (all z > +1), a fourth value of 106.5 returns `OutOfControl` with `4-1s` in `ViolatedRules`; a third-then-crossing series does not. |
| **Severity** | High |
| **Responsible role** | Quality Manager with Backend architect |

---

### GAP-QC-016 — Four of six QC endpoints carry no permission attribute

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.WebApi/Controllers/AnalyticalQualityControllers.cs:16-18` (`GET profiles`), `:35-37` (`GET runs`), `:39-41` (`POST runs`), `:43-48` (`POST troubleshoot`) — none carries `[RequirePermission]`; only `:21` and `:28` do. Command policies are `[RequireInternalActor]` (`QcSlice.cs:11, 42, 74, 99`), never `[RequirePermissionPolicy]`. Catalogue actions available but unused: `View, Create, Edit, Approve, Void, Sign, Export` (`src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:171-173`) |
| **Description** | Any authenticated user who is not an `ExternalAuditor` can list QC profiles, read every run, record a new control run, and close out an out-of-control run with a troubleshooting note — without holding any `analytical-quality.*` privilege. Only profile creation and target maintenance are gated, and both use the same coarse `Manage` action. |
| **Impact** | Contradicts the v1.51.0 permission model, under which authorization comes from tenant-defined roles over the permission catalogue (ground truth §2). Recording QC data and documenting a QC failure are regulated actions attributable to a competent person; the current gating cannot express "may view QC but not record it". |
| **Testing limitation** | The Role & Permission Matrix cannot be completed for this module: there is no permission whose absence changes the outcome of four of the six endpoints, so no meaningful negative authorization case exists for them. |
| **Recommended clarification** | Security architect with Quality Manager to assign an action to each QC operation — the natural mapping being `analytical-quality.view` for the two GETs, `analytical-quality.create` for recording a run, `analytical-quality.edit` for the troubleshooting note, and `analytical-quality.manage` for profile and target maintenance — and to convert the command policies to `[RequirePermissionPolicy]`. |
| **Suggested acceptance criteria** | A tenant user holding no `analytical-quality.*` privilege receives `403` with `application/problem+json` from all six QC routes and both their `/api/v{version}/…` mirrors; a user holding only `analytical-quality.view` can read but receives `403` on every write. |
| **Severity** | **Critical** |
| **Responsible role** | Security architect with Quality Manager |

---

### GAP-QC-017 — Duplicate QC profiles are permitted

| Field | Content |
|---|---|
| **Source reference** | `src/NT.QAMS.Infrastructure/Persistence/Configurations/AnalyticalQualityConfigurations.cs:19` — `builder.HasIndex(p => new { p.TenantId, p.Analyte, p.Instrument, p.ControlLot });` with **no** `.IsUnique()`, in contrast to `ValidationStudyConfiguration` at `:54` which does call `.IsUnique()` on `(TenantId, StudyRef)` |
| **Description** | Two profiles for the same analyte, instrument and control lot can coexist in one tenant, each accumulating its own run series. `CreateQcProfileHandler` (`QcSlice.cs:26-35`) performs no existence check. |
| **Impact** | The Levey-Jennings series for a control silently splits across two profiles, so the 10-x and 2-2s rules see only a fraction of the true history and under-detect systematic error. An analyst recording into the wrong duplicate would see an apparently clean chart. |
| **Testing limitation** | The expected behaviour is undefined — a case creating the duplicate can only record that it succeeds, not whether that is correct. |
| **Recommended clarification** | Product owner to confirm whether `(tenant, analyte, instrument, control lot)` is intended to be unique among **active** profiles, and what error code a duplicate attempt should return. |
| **Suggested acceptance criteria** | A second `POST /api/qc/profiles` with the same analyte, instrument and control lot within one tenant is refused with `422` and a documented `QC-*` code; the same combination in a different tenant is accepted. |
| **Severity** | Medium |
| **Responsible role** | Product owner with Backend developer |

---

### GAP-QC-018 — `TargetEffectiveFromUtc` and `LastTargetChangeReason` are write-only

| Field | Content |
|---|---|
| **Source reference** | Columns added by migration `20260726190957_QcTargetEffectiveDating.cs:14-27`; written at `src/NT.QAMS.Domain/AnalyticalQuality/QcProfile.cs:84-85`; **absent** from `QcProfileDto` (`src/NT.QAMS.Contracts/AnalyticalQuality/AnalyticalQualityContracts.cs:11-13`), from the projection in `GetQcProfilesHandler` (`QcSlice.cs:130-132`) and from the frontend `QcProfile` model |
| **Description** | Both columns are persisted on every target change and never read back by any query, DTO or UI. Their only visible trace is the `audit.field_change` ledger produced by `FieldChangeInterceptor`, and the field-change reason there comes from the middleware's scoped reason, not from `LastTargetChangeReason`. Combined with GAP-QC-010 (no UI path) and GAP-QC-003 (no schedulable date), the whole effective-dating feature is invisible to the product's users. |
| **Impact** | URS-037's "documented, effective-dated target change" is not demonstrable through the product surface. A reviewer must query PostgreSQL or the field-change ledger to see when and why a target moved. |
| **Testing limitation** | No API- or UI-level case can assert either value; verification is confined to SQL, which weakens the evidence quality of any URS-037 traceability claim. |
| **Recommended clarification** | Product owner to confirm whether the current targets' effective date and last change reason should appear on the profile detail page and in `QcProfileDto`, and whether a full target-history endpoint is required. |
| **Suggested acceptance criteria** | `GET /api/qc/profiles` returns `targetEffectiveFromUtc` and `lastTargetChangeReason`; the profile detail page displays both alongside the current mean and SD. |
| **Severity** | Medium |
| **Responsible role** | Product owner with Backend developer |

---

**Gap register total: 18 gaps** — 2 Critical (GAP-QC-014, GAP-QC-016), 6 High (GAP-QC-002, 003, 006, 010, 013, 015), 10 Medium. No Low-severity gaps were recorded; nothing was capped or dropped from this register.
