# NT.QAMS — AS-BUILT Review · Document 09 · Testing, Quality Gates & CI/CD Audit

| Field | Value |
|---|---|
| Series / contract | AS-BUILT Review per [`00_REVIEW_MANIFEST.md`](00_REVIEW_MANIFEST.md) (v1.1) |
| Owner prompt | 09 — Tests, Quality Gates & CI/CD Audit |
| Commit reviewed | `d74d4bff733d49361a1b4da1e1b3ef3e8338af06` (`master`) — **identical to the manifest baseline; no drift** |
| Review date | 2026-08-02 |
| Method | Static inspection only — **tests were NOT executed**. One focused agent for the full CI stage breakdown + test-class inventory + quality spot-check; static counts carried from Doc 01 (adversarially verified) and Doc 05 (frontend) |

**Evidence-class legend (manifest §5).** **Confidence:** High = source-cited. **Static cap:** test *pass/fail status and coverage %* are **Documentation-only** here — no suite was run; "tests found" = static counts of test attributes/`it()` calls. CLAUDE.md's "green at last run" and "460 backend" figures are Documentation-only.

---

## 1. Test estate at a glance

| Project | In `.sln`? | Framework | Static test count | Provider |
|---|---|---|---|---|
| `NT.QAMS.Domain.UnitTests` | ✓ | xUnit + FluentAssertions | **228** (222 `[Fact]` + 6 `[Theory]`) | none (pure domain) |
| `NT.QAMS.Application.UnitTests` | ✓ | xUnit + EFCore.InMemory | **55** (50 + 5) | InMemory / fakes |
| `NT.QAMS.Architecture.Tests` | ✓ | xUnit + NetArchTest | **10** (6 + 4) | none (static analysis) |
| `NT.QAMS.IntegrationTests` | ✓ | xUnit + SkippableFact | **26** (1 `[Fact]` + 24 `[SkippableFact]` + 1 `[SkippableTheory]`) | **real PostgreSQL 17** |
| `NT.QAMS.WebApi.FunctionalTests` | ✓ | xUnit + Mvc.Testing | **76** (68 + 4 + 4 SkippableFact) | InMemory (21 classes) + real PG (1 class, 4 tests) |
| **Backend total** | — | — | **395 static methods** | — |
| `frontend` unit | — | Karma + Jasmine | **87 `it()`** across 17 spec files | ChromeHeadless |
| `frontend` e2e | — | Playwright + axe | **6 `test()`** across 3 specs | browser |
| `NT.QAMS.LoadTests` | **✗ (outside .sln)** | BCL console harness | 0 (not xUnit) | drives a running API |

CLAUDE.md's claimed "460 backend" reconciles with 395 static methods via `[Theory]` data-row expansion (consistent, not verifiable without execution). Frontend "87 + 6" matches exactly.

## 2. Coverage by area (required table)

| Area / Module | Test type | Tests found | Meaningful coverage evidence | Missing tests | Risk |
|---|---|---|---|---|---|
| Domain — AnalyticalQuality | unit | ~13 classes | Westgard rules incl. negative/boundary/error cases; validation math; PT scoring; SoD invariants | HTTP surface (see below) | Low (domain), High (HTTP) |
| Domain — Improvement/Governance/Records/Identity/etc. | unit | ~25 classes | state-machine transitions, SoD, immutability per aggregate | Files, Notifications domain (0 refs) | Low |
| Application — behaviors | unit | `AuthorizationBehaviorTests`, `IdempotencyBehaviorTests` | deny-by-default + idempotency replay proven | — | Low |
| Application — sagas/outbox | unit | `OutboxProcessorTests`, `FindingToNcPolicyTests`, `PtToNcPolicyTests`, `EscalationFlowTests`, `ScheduledSweepTests` | poison/backoff/dead-letter, finding→NC, PT→NC, SLA arm/cancel | — | Low |
| Application — 7 module slices | unit | **none** | — | DocumentControl, Facility, Organization, Records, Reporting, RiskGovernance, SupplierQuality handlers | Med |
| Architecture (merge gates) | NetArchTest/source | 4 classes | layer rules, module boundary, one-policy-per-command, user_account tenant-bounding | — | Low (strong) |
| RLS / tenant isolation | integration (real PG) | `RlsTenantIsolation`, `SecurityEventRls`, `OwnedChildTenancy`, `RuntimeRolePrivilege` | RLS tried with EF filter OFF; fail-closed; composite-FK drift blocked; structural sweep of every NOT-NULL-tenant table | — | Low (excellent) |
| Authorization / deny-by-default | functional (InMemory) | `AuditorDenyMatrix`, `RoleEndpointMatrix`, `RolePrivilegeFlow` + arch `CommandPolicy` | 6 roles × endpoints; every write 403+`AUTHZ-` problem+json; grants flip endpoints | — | Low |
| Audit chain / tamper | integration + unit | `GovernanceTests` (mid-chain tamper), `AuditTrailChainTests`, `FieldChangeInterceptorTests` | tamper detected at exact broken sequence; per-field capture + redaction | — | Low |
| E-signature | unit + integration | `ESignatureServiceTests`, `SignedRecordImmutabilityTests` | dual-component sign; raw UPDATE/DELETE on frozen row rejected (23514) | **no functional test of the publish signing ceremony over HTTP** | Med |
| Migrations round-trip | integration | `GovernanceTests` | last migration Down()+Up() clean | only the *last* migration round-trips (not full chain) | Med |
| Concurrency | integration + unit | `OptimisticConcurrency`, `ConcurrencyConflictMapping` | `xmin` race → 409 | — | Low |
| **Analytical Quality HTTP surface** | — | **none** (domain-only + snapshot gate) | — | **107 AQ routes have no functional/integration test**; PT-result→NC untested end-to-end | **High** |
| Frontend components | unit | 7 of 107 | 5 shared-ui + 2 core dialogs | ~84 feature pages, 32/35 facades | **High** |
| Frontend facades | unit | 3 of 35 (`change`, `complaints`, `quality-analytics`) | signal state + API call shape | 32 facades | High |
| Full-stack regulated e2e | Playwright | 1 spec (`regulated-workflow`) | NC journey over real API | **not run in CI** (needs seeded API) | Med-High |

## 3. CI/CD pipeline (required table)

Single workflow `.github/workflows/ci.yml` (187 lines), **3 parallel jobs** (no `needs:`), triggers push→`main|master|dev` / PR→`main|master`.

| Pipeline stage | Tool / command | Trigger | Gate (fail condition) | Evidence | Gap |
|---|---|---|---|---|---|
| Build | `dotnet build … Release` | push/PR | compile error | `ci.yml:48-49` | — |
| **NuGet SCA** | `dotnet list … --vulnerable --include-transitive` + grep | push/PR | any High/Critical (incl. transitive) | `ci.yml:54-60` | — |
| Provision least-priv role | `psql` CREATE ROLE `NOSUPERUSER NOBYPASSRLS` + `ntqms_ci` DB | push/PR | psql `ON_ERROR_STOP` | `ci.yml:62-69` | superuser used only to mint the role |
| Migrate | `dotnet tool restore` + `ef database update` | push/PR | migration failure | `ci.yml:71-79` | tool needs .NET 10 runtime (OBS-06) |
| **Backend test (real PG, RLS forced)** | `dotnet test NT.QAMS.sln` with `QMS_ITEST_POSTGRES` set | push/PR | any test fail; **RLS suite hard-fails, cannot skip** | `ci.yml:89-92`; `RuntimeRolePrivilegeTests.cs:24-33` | LoadTests excluded (not in .sln) |
| Frontend deps | `npm ci` (Node 24) | push/PR | lockfile mismatch | `ci.yml:108-116` | — |
| **npm SCA** | `npm audit --omit=dev --json` vs allowlist (`comm -23`) | push/PR | un-allowlisted high/critical (allowlist currently empty) | `ci.yml:122-131` | prod deps only (dev excluded) |
| Frontend unit | `ng test --watch=false --browsers=ChromeHeadless` | push/PR | test fail | `ci.yml:134-135` | **no coverage threshold** |
| Frontend build | `npm run build` (AOT prod) | push/PR | build/budget error | `ci.yml:137-138` | — |
| E2E smoke | `playwright test e2e/auth.spec.ts e2e/a11y.spec.ts` | push/PR | auth-gate or axe serious/critical | `ci.yml:147-148` | **`regulated-workflow.spec.ts` excluded from CI** |
| Container build + non-root | `docker build` + `id -u`≠0 + writable-volume probe | push/PR | root UID or unwritable volume | `ci.yml:160-173` | — |
| **Trivy image scan** | `trivy image --severity HIGH,CRITICAL --ignore-unfixed --exit-code 1` | push/PR | fixable High/Critical CVE | `ci.yml:184-187` | unfixed base-image CVEs tolerated |
| API-surface snapshot | `ApiSurfaceSnapshotTests` (within backend test) | push/PR | any unreviewed route add/rename/remove vs `ApiSurface.approved.txt` | `ApiSurfaceSnapshotTests.cs:10-15` | — |
| Architecture merge gates | `LayerRules`/`ModuleBoundary`/`CommandPolicy`/`UserAccountTenantBound` (within backend test) | push/PR | layer/boundary/policy violation | Architecture.Tests | — |
| **Artifact publish / deploy / promotion** | — | — | **NONE** | no upload-artifact, no docker push, no `environment:` | build-verify only; deploy is out-of-band via `deploy/` |

**Secrets in CI:** none from `secrets.*` — every credential is an inline CI-only throwaway (Postgres, the `qams_app` CI password, the test factories' JWT/PlatformAdmin env values); nothing production, nothing echoed beyond the already-in-repo redacted literals.

## 4. Test quality (spot-check)

- **`WestgardEvaluatorTests`** — strong: documented z-scores per case, asserts both `Outcome` and exact `ViolatedRules`, includes negative cases (2-2s not firing on opposite sides; 10-x not firing across the mean) and error cases (zero SD → `QC-SD`). Pure/deterministic — no flakiness.
- **`RlsTenantIsolationTests`** — high quality and correctly isolated: drives DB GUCs and reads with `IgnoreQueryFilters()` so **PostgreSQL RLS, not the EF filter, is on trial**; fresh UUIDv7 tenants per test inside rolled-back transactions; the fixture refuses to run if FORCE RLS is absent or the role is SUPERUSER/BYPASSRLS (guards against false green).
- **`AuditorDenyMatrixTests`** — real e2e assertions: unique-slug tenant per run, three real JWT logins, asserts read=200 / write=403 **and** problem+json `AUTHZ-` code (not just status).
- **Isolation/flakiness:** assembly parallelization disabled in FunctionalTests to prevent the two web-factories racing over process-global env vars; no `Thread.Sleep`/timing hacks in the spot-checked files; committed-row tests clean up in `finally` with unique markers. **Race risk: low.**

**Verdict: where tests exist, they are meaningful** (real assertions, real PostgreSQL for the compliance-critical suite, an anti-false-green fixture, negative cases). The problem is *distribution*, not depth.

## 5. Build health & discovered commands

Reported, not executed (Documentation-only for outcomes): local build/test/migration/run commands are in `CLAUDE.md §6` and `scripts/dev-*.ps1`; the WebApi locks its DLLs so the API must be stopped before `dotnet build`/`ef`. `TreatWarningsAsErrors=true` on all six `src` projects (not on tests) is a real compile-time quality gate. **No coverage tooling gates the build** — no `coverlet`/`CollectCoverage`/threshold in any test csproj; frontend `karma-coverage` is a devDependency but `test:ci` runs without `--code-coverage` and no threshold.

## 6. Gaps & missing quality gates

| # | Gap | Severity |
|---|---|---|
| T-1 | **Analytical-Quality HTTP surface (107 routes) has no functional/integration test** — domain math is well-tested, but no test exercises the AQ controllers/authz/child-writes; **PT-result→NC is ungated *and* untested** (SEC-04) | High |
| T-2 | **Frontend coverage is thin** — 7/107 components, 3/35 facades; the layer users sign records through is least-tested | High |
| T-3 | **Full-stack regulated e2e (`regulated-workflow.spec.ts`) is not in CI** — the one end-to-end journey never runs on the merge path | Med-High |
| T-4 | **InMemory provider fidelity** — 21 of 22 functional classes run on EF InMemory; the repo's own VER-001 records defects that escaped a green suite; only 4 real-PG HTTP tests compensate | Med (documented + partially mitigated) |
| T-5 | **7 Application module slices + 2 Domain modules have zero unit tests** (DocumentControl/Facility/Organization/Records/Reporting/RiskGovernance/SupplierQuality handlers; Files/Notifications domain) | Med |
| T-6 | **No coverage gate** anywhere (backend or frontend) | Med |
| T-7 | **No performance test in CI** — LoadTests is outside the solution, invoked manually; `perf-smoke.ps1` not wired to CI (OPS-001) | Med |
| T-8 | **Only the last migration round-trips** in `GovernanceTests`; the full 59-migration chain is not round-trip-tested | Low-Med |
| T-9 | **No document-publish e-signature ceremony functional test** — the one real signature path is unit-tested but not exercised over HTTP | Med |

## 7. Minimum production release-gate checklist

**Implemented gates (present and enforced in CI):**
- [x] Compile clean + `TreatWarningsAsErrors` (src)
- [x] Full solution test suite (395 methods) on **real PostgreSQL** with RLS suite **forced to run** (anti-skip sentinel)
- [x] Architecture merge gates (layers, module boundary, one-policy-per-command, user_account tenant-bounding)
- [x] API-surface snapshot (no unreviewed route change)
- [x] NuGet High/Critical SCA gate
- [x] npm audit gate (prod deps) vs auditable allowlist
- [x] Trivy image scan (fixable High/Critical)
- [x] Container non-root + writable-volume assertions
- [x] Frontend unit (Karma) + AOT build + bundle budgets
- [x] E2E smoke (auth gate + axe a11y on login)

**Recommended (not yet gated — feeds Doc 12):**
- [ ] Functional/integration tests for the Analytical-Quality HTTP surface (close T-1) — **highest priority**
- [ ] Run `regulated-workflow.spec.ts` (and add auditor/e-sign journeys) in CI against a seeded API (T-3)
- [ ] Coverage thresholds, especially frontend feature pages/facades (T-2, T-6)
- [ ] A document-publish e-signature ceremony functional test (T-9)
- [ ] Full-chain migration round-trip (T-8)
- [ ] Performance test in CI or a scheduled gate (T-7 / OPS-001)
- [ ] Unit tests for the 7 untested Application slices (T-5)
- [ ] axe a11y coverage of authenticated screens (Doc 05 NB-05-03)

## 8. Assessment

Test maturity is **high for the regulated backend core and weak at the edges.** The real-PostgreSQL integration suite with an anti-false-green fixture, executable architecture merge gates, the API-surface contract, and meaningful negative/boundary cases make the compliance-critical backend genuinely regression-guarded — this is well above typical. The estate is **lopsided**: the UI layer users actually sign records through has ~7/107 components tested, the sole full-stack journey is CI-absent, the entire Analytical-Quality HTTP surface is functionally untested, and there is no coverage gate. **Net: the backend compliance spine is trustworthy; frontend and the AQ HTTP surface are the coverage debt to close before a regulated go-live.**

---

## Appendix A — Observation carry-forward

| ID | Note |
|---|---|
| OBS-05 (LoadTests outside sln) | Confirmed — no perf test in CI (T-7). |
| OBS-06 (dotnet-ef 10.0.10) | The CI migrate step needs a .NET 10 runtime for the pinned tool. |
| OBS-07 (docs/testing case gaps) | The `docs/testing/` module case files (module 13 missing B/C; 14-23 none) are documentation, not automated tests — do not count toward coverage. |
| SEC-04 (PT ungated) | Compounded here: PT-result→NC is both ungated **and** untested (T-1). Doc 12. |
| **NB-09-01** (new) | Only the last migration round-trips; 58 earlier migrations are not round-trip-tested (T-8). Doc 12 (Low-Med). |
| **NB-09-02** (new) | No coverage gate on backend or frontend (T-6). Doc 12 (Med). |

## Appendix B — Reviewer no-modification attestation (manifest §8 model)

- [x] No file was created, modified, or deleted; **no test suite was executed**, no build/migration was run, no DB connection opened.
- [x] Only read-only access (file reads, grep, read-only git) was used, including by the evidence agent.
- [x] The only filesystem write is this document: `docs/as-built-review/09_TESTING_QUALITY_AND_CICD_AUDIT.md`.
- [x] No secret values reproduced — CI credentials are CI-only throwaways, reported as such, never quoted.
- [x] Nothing invented — every count and class is source-cited; test pass/fail status and coverage % are labelled Documentation-only because no suite was run.

---

*End of Document 09. Reviewed at manifest baseline `d74d4bf` (no drift). Next: Prompt 10 → `10_INTEGRATIONS_OPERATIONS_AND_OBSERVABILITY.md`.*
