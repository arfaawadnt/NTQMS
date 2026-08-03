# NT.QAMS — AS-BUILT Reverse-Engineering Review

## Document 00 · Review Manifest & Audit Contract

| Field | Value |
|---|---|
| Series | NT.QAMS AS-BUILT Review (`docs/as-built-review/`) |
| Document | 00 — Review Manifest & Audit Contract |
| Owner prompt | Prompt 00 (this document) |
| Manifest version | 1.1 (v1.0 2026-08-02 baseline; v1.1 same day — §6 series replaced by the engagement's official 16-prompt pack) |
| Baseline captured | 2026-08-02 17:05 (+03:00) |
| Status | Baseline established — deep analysis NOT yet performed |
| Review roles | Principal Software Architect · Senior .NET Engineer · Angular Architect · Enterprise Database Architect · Security Auditor · QA Lead · Business Analyst |

---

## 0. Purpose & review contract

This is the audit manifest and review contract for an **as-built reverse-engineering review** of the NT.QAMS repository. It records the evidence baseline, the analysis boundaries, the source-of-truth hierarchy, the planned deliverables, and the evidence-quality rubric that every subsequent review document in this series MUST follow.

**Rules of engagement (binding on all documents 01–15):**

1. Inspect only: source code, migrations, configuration, tests, package manifests, scripts, and existing documentation. **No modification** of application code, tests, configuration, database, infrastructure, or dependencies.
2. **No implementation code is generated** by this review.
3. **Nothing is invented.** Functionality, data models, endpoints, workflows, security controls, and tests are reported only where source evidence exists.
4. UI labels, SRS claims, diagrams, comments, and mock data are **not proof of implementation**. Source code is authoritative. Anything unsupported by source evidence is marked `Unknown`, `UI-only`, `Documentation-only`, `Mocked`, or `Missing` (see §5).
5. Every material conclusion cites repository-relative file paths (and line numbers where possible).
6. **All secret values are redacted.** No passwords, API keys, JWT secrets, SMTP credentials, connection strings, access tokens, or personally identifiable values appear in any review document. Configuration is reported by key/section name only.
7. This review is **static**: no application execution, no test execution, no database connection. Runtime behavior claims are confidence-graded per §7, never asserted as runtime-proven.

---

## 1. Repository identification, baseline & limitations

### 1.1 Identification

| Item | Value | Evidence |
|---|---|---|
| Repository root | `D:\SAAS\QAMS\21-7\NT.QAMS` (all paths below are repo-relative) | filesystem |
| VCS | Git | `.git/` present |
| Branch | `master` | `git branch --show-current` |
| HEAD commit | `d74d4bff733d49361a1b4da1e1b3ef3e8338af06` | `git log -1` |
| HEAD date / subject | 2026-08-02T15:51:48+03:00 — "deploy: add Deploy-FullStack.ps1 (scripted from-source Windows full-stack deploy)" | `git log -1` |
| Commit count | 170 | `git rev-list --count HEAD` |
| Remote | `origin → https://github.com/arfaawadnt/NTQMS.git` (no embedded credentials) | `git remote -v` |
| Latest tags (creatordate desc) | `v1.51.2`, `v1.51.1`, `v1.51.0`, `v1.50.0`, `v1.49.1`, `v1.49.0` | `git tag --sort=-creatordate` |
| Baseline timestamp | 2026-08-02 17:05:24 +03:00 | captured at manifest creation |

### 1.2 Working tree state at baseline (dirty — recorded verbatim)

The review baseline is HEAD `d74d4bf` **plus** the following uncommitted state. Review documents must state which of the two they cite when the distinction matters.

| State | Path |
|---|---|
| Modified | `frontend/package.json` |
| Modified | `scripts/dev-status.ps1` |
| Modified | `scripts/dev-up.ps1` |
| Untracked | `NT_QMS_Complete_SRS.html` |
| Untracked | `NT_QMS_Complete_SRS_Sameh.html` |
| Untracked | `docs/srs/html/NT_QMS_SRS_Complete_Standalone.html` |

### 1.3 Tooling & runtime limitations of this review

| Limitation | Consequence |
|---|---|
| Static analysis only (Claude Code session on Windows 10 Pro 10.0.19045; read-only file inspection + read-only `git` commands) | No claim in this series is runtime-verified |
| No live PostgreSQL connection | Schema facts derive from EF migrations + model snapshot + SQL scripts, not `pg_catalog` introspection |
| Application and tests are **not executed** | Test pass/fail status, coverage, and perf figures are reported only as documented by repo artifacts, graded Low/Medium confidence |
| No access to GitHub Actions run history | CI facts describe pipeline *definition* (`.github/workflows/ci.yml`), not execution outcomes |
| No external services (SMTP, OTLP collector, IIS, Docker daemon) | Integration behavior is source-inferred only |
| Binary/generated artifacts not parsed | `deploy/*.zip`, `deploy/publish-win-x64/`, `frontend/dist/`, `deploy/migrations.sql` (generated bundle) excluded from evidence (§4) |

---

## 2. Solution & project inventory

### 2.1 .NET solution — `NT.QAMS.sln`

11 C# projects (verified count), 2 solution folders (`src`, `tests`). Configurations `Debug|Release` × `Any CPU|x64|x86` (x64/x86 map to Any CPU).

| Project | Path | Role (naming/structure only — behavior deferred to Docs 01–02) | `.cs` files |
|---|---|---|---|
| NT.QAMS.SharedKernel | `src/NT.QAMS.SharedKernel/` | Cross-cutting primitives (Abstractions, Primitives, MultiTenancy, Localization); zero dependencies | 11 |
| NT.QAMS.Domain | `src/NT.QAMS.Domain/` | DDD aggregates in 19 bounded-context folders | 56 |
| NT.QAMS.Contracts | `src/NT.QAMS.Contracts/` | Wire DTOs, module-grouped; zero dependencies | 22 |
| NT.QAMS.Application | `src/NT.QAMS.Application/` | CQRS handlers/validators, MediatR pipeline behaviors | 89 |
| NT.QAMS.Infrastructure | `src/NT.QAMS.Infrastructure/` | EF Core persistence + migrations, authorization, exports, jobs, storage, email, observability | 165 |
| NT.QAMS.WebApi | `src/NT.QAMS.WebApi/` | ASP.NET Core host (only `Microsoft.NET.Sdk.Web` project); `Dockerfile`; `UserSecretsId=nt-qams-webapi` | 58 |
| NT.QAMS.Domain.UnitTests | `tests/NT.QAMS.Domain.UnitTests/` | xUnit + FluentAssertions | 38 |
| NT.QAMS.Application.UnitTests | `tests/NT.QAMS.Application.UnitTests/` | xUnit + EFCore.InMemory | 17 |
| NT.QAMS.Architecture.Tests | `tests/NT.QAMS.Architecture.Tests/` | xUnit + NetArchTest.Rules (layer/module/boundary rules) | 4 |
| NT.QAMS.IntegrationTests | `tests/NT.QAMS.IntegrationTests/` | xUnit + SkippableFact against real PostgreSQL (RLS, immutability, constraints, outbox) | 12 |
| NT.QAMS.WebApi.FunctionalTests | `tests/NT.QAMS.WebApi.FunctionalTests/` | xUnit + Mvc.Testing; includes API-surface snapshot gate `ApiSurface.approved.txt` | 28 |

**Outside the solution (deliberate):** `tests/NT.QAMS.LoadTests/NT.QAMS.LoadTests.csproj` — dependency-free console load harness (`OutputType=Exe`, zero package/project references); its csproj comment states it is intentionally excluded from the solution test suite and invoked explicitly. `dotnet test NT.QAMS.sln` never runs it.

Dependency direction (from ProjectReferences only): SharedKernel ← Domain ← Application (+Contracts) ← Infrastructure ← WebApi. `InternalsVisibleTo`: Infrastructure → Application.UnitTests, IntegrationTests; WebApi → WebApi.FunctionalTests.

### 2.2 Database migrations

| Item | Value |
|---|---|
| Location (single) | `src/NT.QAMS.Infrastructure/Persistence/Migrations/` |
| Migration count | **59** (verified: 119 `.cs` files = 59 migrations + 59 `.Designer.cs` + 1 `AppDbContextModelSnapshot.cs`) |
| First | `20260721211309_InitialFoundation.cs` |
| Last | `20260801194628_MaintenanceCertificate.cs` |
| SQL scripts (non-EF) | `scripts/preflight-data-checks.sql`, `scripts/preflight-enum-domains.sql`, `deploy/db-init.sql`, `deploy/harden-runtime-role.sql`, `deploy/migrations.sql` (generated bundle, ~349 KB, excluded as evidence) |

### 2.3 Angular workspace — `frontend/`

| Item | Value |
|---|---|
| Project | `nt-qams-frontend` (single application, prefix `qams`, `sourceRoot: src`) — `frontend/angular.json` |
| Builder | `@angular-devkit/build-angular:application` (esbuild); configurations `production` (default; budgets 1 MB warn / 2 MB error) and `development` |
| Unit tests | Karma + Jasmine via `:karma` builder — **no `karma.conf.js`** (builder-inline config); 17 `*.spec.ts` under `frontend/src` |
| E2E | Playwright (`frontend/playwright.config.ts`, chromium-only, workers 1): `e2e/auth.spec.ts`, `e2e/a11y.spec.ts` (axe), `e2e/regulated-workflow.spec.ts` |
| Source shape | 221 `.ts` files: 107 components, 51 services (44 in `core/api/`), 35 signal facades, 2 guards, 2 interceptors, 0 pipes, 0 directives; 28 lazy feature folders under `app/features/`; all components standalone with inline templates/styles (only 2 `.html`/`.css` files: `index.html`, `styles.css`) |
| i18n | Hand-rolled trilingual dictionary `frontend/src/app/core/i18n.service.ts` (~1,559 EN/AR/FR keys, RTL flip for Arabic); no `@angular/localize`, no locale asset files |
| Environments | Single `frontend/src/environments/environment.ts` (`production: true`, `apiBaseUrl: '/api'`); no `fileReplacements` |
| Dev proxy | `frontend/proxy.conf.json` → `/api` → `http://localhost:5080` |

### 2.4 Scripts — `scripts/` (13 files)

`dev-up.ps1`, `dev-down.ps1`, `dev-rebuild.ps1`, `dev-status.ps1` (dev stack lifecycle) · `failure-drills.ps1`, `perf-smoke.ps1` (operational/perf drills) · `security-probe.ps1`, `security-probe-deep.ps1`, `staging-smoke.ps1`, `verify-e2e.ps1` (security/e2e probes against a *running* system) · `preflight-data-checks.sql`, `preflight-enum-domains.sql` (schema-hardening pre-flight).

### 2.5 Containers, CI, deployment

| Artifact | Contents |
|---|---|
| `docker-compose.yml` (root) | Dev stack: single service `postgres` (`postgres:17-alpine`, port 5432, volume `ntqams-pgdata`); credentials REDACTED (header states dev-only) |
| `deploy/compose.production.yml` | `api` (`ntqams-webapi:1.43`, read-only rootfs, loopback-published `127.0.0.1:5000:8080`) + `postgres` (no published ports); all secrets as `${VAR:?}` placeholders — REDACTED |
| `deploy/observability/compose.observability.yml` | `otel-collector` 0.109.0, `prometheus` v2.54.1, `grafana` 11.2.0 (+ alert rules, provisioned dashboard) |
| `.github/workflows/ci.yml` (only workflow) | Jobs: **build-test** ("Build & Test (with real PostgreSQL)": restore/build Release, NuGet SCA gate failing High/Critical, EF `database update` against service PG, `dotnet test` with RLS suite forced-on) · **frontend** ("Frontend (unit + build + e2e smoke)": Node 24, `npm ci`, npm-audit gate vs `.github/npm-audit-allowlist.txt`, Karma CI, AOT build, Playwright smoke) · **container** ("Container (build + non-root assertion)": Docker build, non-root + writable-volume assertions, Trivy HIGH/CRITICAL gate) |
| `deploy/` runbooks | `DEPLOY.md`, `WINDOWS-FULLSTACK-FROMSOURCE-DEPLOY-PROMPT.md`, `ANTIGRAVITY_FULLSTACK_DEPLOY_PROMPT.md`, `ANTIGRAVITY_DEPLOY_PROMPT.md`, `BACKUP-RESTORE-DR.md`, `DEV-SECRETS.md`, `OBSERVABILITY.md`, `bring-up-staging.md` |
| `deploy/` executables/config | `Deploy-FullStack.ps1`, `backup.sh`, `restore.sh`, `db-init.sql`, `harden-runtime-role.sql`, `web.config`, `iis/Install-NTQMS-IIS.ps1`, `iis/Verify-NTQMS-IIS.ps1` |

### 2.6 Documentation & repo tooling inventory

| Area | Contents |
|---|---|
| `docs/adr/` | 9 ADRs (0001 single-replica … 0009 refresh-token session model) |
| `docs/reference/` | 24 files: 10 `.md` (incl. byte-identical copies of the 4 parent-folder target-architecture docs, plus `NT_QMS_Database_AsBuilt.md`, EA closure/load/pen-test docs) + 14 audit/compliance `.html` |
| `docs/srs/` | 19 markdown documents (00–15, with 02 split in 4 parts) + `docs/srs/html/` (21 files) |
| `docs/testing/` | 24 files: ground-truth conventions + module test docs 10–23 (+ case files A–E for modules 10–13 only, partial) |
| `docs/validation/` | 15 files: GAMP 5 CSV set (VMP, URS, FRA, IQ/OQ/PQ, RTM, VSR, OQ execution records, verification log) |
| Root docs | `CLAUDE.md` (operating guide), `README.md`, `ONBOARDING.md`, `IMPLEMENTATION_LOG.md`, `SCHEMA-HARDENING-PLAN.md`, `SCHEMA-HARDENING-REPORT.md` |
| `.claude/skills/` | 4 repo skills: `ntqms-architecture`, `ntqms-database`, `ntqms-frontend`, `ntqms-compliance` |
| `.config/dotnet-tools.json` | `dotnet-ef` **10.0.10** (`rollForward: false`) |

---

## 3. Technology & version inventory (from project files / package manifests only)

### 3.1 Backend (.NET)

| Item | Value | Evidence |
|---|---|---|
| Target framework | `net9.0` — all 12 projects; `Nullable` + `ImplicitUsings` enabled; the 6 `src` projects set `TreatWarningsAsErrors=true` | each `.csproj` |
| Web host | ASP.NET Core 9 (`Microsoft.NET.Sdk.Web` — WebApi only) | `src/NT.QAMS.WebApi/NT.QAMS.WebApi.csproj` |
| CQRS/mediator | MediatR `12.4.*` | `src/NT.QAMS.Application/NT.QAMS.Application.csproj` |
| Validation | FluentValidation.DependencyInjectionExtensions `11.11.*` | same |
| ORM | Microsoft.EntityFrameworkCore `9.0.*`; Npgsql.EntityFrameworkCore.PostgreSQL `9.0.*`; EFCore.NamingConventions `9.0.*` | Application/Infrastructure csproj |
| Identity/JWT | Microsoft.Extensions.Identity.Core `9.0.*`; System.IdentityModel.Tokens.Jwt `8.2.*`; Microsoft.AspNetCore.Authentication.JwtBearer `9.0.*` | Infrastructure/WebApi csproj |
| API versioning | Asp.Versioning.Mvc `8.1.0` | WebApi csproj |
| OpenAPI | Microsoft.AspNetCore.OpenApi `9.0.*` | WebApi csproj |
| Exports | ClosedXML `0.105.0`; QuestPDF `2026.7.1` | Infrastructure csproj |
| Observability | OpenTelemetry.Extensions.Hosting / Instrumentation.AspNetCore / Exporter.OTLP `1.17.0`; Exporter.Prometheus.AspNetCore `1.17.0-beta.1` (pre-release); Npgsql.OpenTelemetry `9.0.4` | WebApi csproj |
| Test stack | xunit `2.9.*`; xunit.runner.visualstudio `2.8.*`; FluentAssertions `6.12.*`; Microsoft.NET.Test.Sdk `17.12.*`; NetArchTest.Rules `1.3.*`; Microsoft.AspNetCore.Mvc.Testing `9.0.*`; Xunit.SkippableFact `1.5.*`; Microsoft.EntityFrameworkCore.InMemory `9.0.*` | tests csproj |
| Database engine (declared) | PostgreSQL 17 (`postgres:17-alpine` in compose; `postgres:17` CI service) | `docker-compose.yml`, `deploy/compose.production.yml`, `.github/workflows/ci.yml` |

### 3.2 Frontend (npm)

| Item | Value | Evidence |
|---|---|---|
| Framework | Angular `^22.0.8` (all `@angular/*` packages) | `frontend/package.json` |
| Language | TypeScript `~6.0.3`, `strict: true` + `strictTemplates` | `frontend/package.json`, `frontend/tsconfig.json` |
| Runtime deps | rxjs `~7.8.0`, zone.js `~0.15.1`, tslib `^2.3.0` — **no other runtime dependencies** (no NgRx, no UI kit, no i18n package) | `frontend/package.json` |
| Test/E2E | karma `^6.4.4`, jasmine-core `^6.3.0`, @playwright/test `^1.62.0`, @axe-core/playwright `^4.12.1` | `frontend/package.json` |
| Lint/format | **None** — no ESLint/Prettier/Tailwind/PostCSS config anywhere under `frontend/` (quality gates = TS strict + Angular strictTemplates + bundle budgets) | verified absence |
| Metadata drift | `package.json` `description` says "Angular 18 frontend" while deps are `^22.0.8` (see Appendix A) | `frontend/package.json` |

### 3.3 Build/tooling determinism facts

| Fact | Evidence |
|---|---|
| **No** `global.json` — .NET SDK version unpinned locally (CI pins `9.0.x` via `actions/setup-dotnet@v4`) | verified absence; `.github/workflows/ci.yml` |
| **No** `Directory.Build.props` / `Directory.Packages.props` / `nuget.config` / `.editorconfig` — no central package management | verified absence |
| NuGet versions are mostly **floating wildcards** (`9.0.*`, `12.4.*`, `6.12.*`) → restores are not reproducible over time | all csproj |
| `dotnet-ef` pinned at **10.0.10** with `rollForward: false` — one major version ahead of the `net9.0` / EF Core `9.0.*` stack; requires a .NET 10 runtime wherever `dotnet ef` runs | `.config/dotnet-tools.json` |
| Node pinned only in CI (`node-version: 24`); npm lockfile present (`frontend/package-lock.json`) | `.github/workflows/ci.yml` |

### 3.4 Runtime configuration surface (keys only — ALL VALUES REDACTED)

| File | Top-level sections |
|---|---|
| `src/NT.QAMS.WebApi/appsettings.json` | `ConnectionStrings` (L2), `Database` (L5), `PasswordPolicy` (L8), `AnalyticalQuality` (L12), `Jwt` (L20), `Logging` (L26), `AllowedHosts` (L32) |
| `src/NT.QAMS.WebApi/appsettings.Development.json` | `_comment` (L2), `ConnectionStrings` (L3), `Jwt` (L6), `PlatformAdmin` (L9), `Logging` (L13) |
| Absent | No `appsettings.Production.json` / `.Staging.json` / `.Test.json` — production config via environment variables / user-secrets per `deploy/DEV-SECRETS.md` |

---

## 4. Analysis boundaries

### 4.1 Excluded from analysis (not evidence)

| Exclusion | Reason |
|---|---|
| `**/bin/`, `**/obj/`, `.git/` | Build/VCS internals |
| `frontend/node_modules/`, `frontend/.angular/`, `frontend/dist/`, `frontend/test-results/`, `frontend/playwright-report/` | Third-party/generated |
| `deploy/publish-win-x64/`, `deploy/*.zip` (~186 MB), `data/` | Git-ignored build artifacts / local runtime data |
| `deploy/migrations.sql` | Generated EF bundle — the migration `.cs` sources are authoritative |
| `*.Designer.cs` + `AppDbContextModelSnapshot.cs` | Generated; read only to corroborate hand-written migrations |
| Parent-folder HTML reports (`D:\SAAS\QAMS\21-7\*.html`) and `docs/reference/*.html` | Documentation-only evidence class (§5); never proof of implementation |

### 4.2 Unavailable dependencies/services (cannot be exercised)

Live PostgreSQL 17 catalog · running WebApi/Kestrel · SMTP relay · OTLP collector / Prometheus / Grafana · IIS/ARR · Docker daemon · GitHub Actions execution history.

### 4.3 Claims that cannot be runtime-verified in this review (confidence-capped)

- RLS enforcement behavior and tenant isolation under real sessions (source: migrations + `tests/NT.QAMS.IntegrationTests/RlsTenantIsolationTests.cs` — test *definitions*, not executions).
- Audit-trail hash-chain integrity at runtime; signed-record immutability enforcement.
- Any test pass/fail counts (e.g., CLAUDE.md's "460 backend + 87 frontend" figures) — reported as Documentation-only until re-executed.
- Performance figures (`scripts/perf-smoke.ps1` thresholds, `docs/reference/NT_QMS_Load_Test_Report.md`).
- Backup/restore/DR drill effectiveness (`deploy/BACKUP-RESTORE-DR.md`).
- CI gate effectiveness (pipeline definitions exist; run outcomes unobservable).

---

## 5. Source-of-truth hierarchy & evidence classes

When sources conflict, the **higher** tier wins; conflicts are reported, not silently resolved.

| Tier | Source | Examples |
|---|---|---|
| 1 (highest) | Compiled application source | `src/**/*.cs`, `frontend/src/**/*.ts` |
| 2 | EF Core migrations + model snapshot + SQL scripts | `src/NT.QAMS.Infrastructure/Persistence/Migrations/`, `deploy/db-init.sql`, `deploy/harden-runtime-role.sql` |
| 3 | Tests and executable gates | `tests/**`, `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt`, `frontend/e2e/**`, `scripts/security-probe*.ps1` |
| 4 | Runtime configuration & manifests | `*.csproj`, `frontend/package.json`, `angular.json`, appsettings *keys*, `.github/workflows/ci.yml`, compose files, `Dockerfile` |
| 5 | Repository documentation | `CLAUDE.md`, `docs/srs/**`, `docs/adr/**`, `docs/validation/**`, `IMPLEMENTATION_LOG.md`, `README.md` |
| 6 (lowest) | UI text/labels, HTML reports, diagrams, comments, mock/seed data | `i18n.service.ts` strings, `docs/reference/*.html`, parent-folder audit HTML |

**Evidence classes** (every functional claim in Docs 01–11 carries exactly one):

| Class | Meaning |
|---|---|
| `Implemented` | Behavior exists in Tier 1–2 source; cite file:line |
| `UI-only` | Frontend surface exists; no backend counterpart found |
| `Documentation-only` | Claimed in Tier 5–6 only; no source evidence |
| `Mocked` | Present but backed by stub/fake/hardcoded data |
| `Missing` | Expected by target architecture/SRS; absent from source |
| `Unknown` | Evidence inconclusive; requires runtime or further inspection |

**Approved target-architecture baseline** (for Doc 09 gap analysis, per review commission): `D:\SAAS\QAMS\21-7\NT_QAMS_Application_Architecture.md`, `NT_QAMS_Database_Architecture.md`, `NT_QAMS_Domain_Model.md` (+ context: `NT_QAMS_Product_Inventory.md`, the legacy-system inventory). Byte-identical copies exist in-repo at `docs/reference/` (verified by size parity); citations use the in-repo copies.

---

## 6. Review deliverables — document series, owner prompts, prerequisites

> **Revision v1.1 (2026-08-02):** the provisional series defined at baseline (v1.0) was superseded the same day by the engagement's official prompt pack (`CLAUDE_AS_BUILT_REVIEW_PROMPTS.md`, 16 prompts 00–15; copy-paste-ready HTML edition at `../QAMS_AsBuilt_Review_Prompts.html`, repo-parent). The table below is the binding series. No document other than this manifest had been produced under the v1.0 numbering, so nothing else required renumbering.

All documents live in `docs/as-built-review/`. Prompts run in listed order; Prompt 15 runs last.

| # | Document | Owner prompt | Focus | Prerequisites (reads first) |
|---|---|---|---|---|
| 00 | `00_REVIEW_MANIFEST.md` | 00 (this) | Repository intake & evidence baseline | — |
| 01 | `01_EXECUTIVE_SUMMARY.md` | 01 | Executive product & maturity assessment | 00 |
| 02 | `02_REPOSITORY_AND_ARCHITECTURE_MAP.md` | 02 | Repository, solution & dependency map | 00 |
| 03 | `03_BACKEND_AND_API_INVENTORY.md` | 03 | Backend & API inventory (endpoints, CQRS usage) | 00–02 |
| 04 | `04_DATABASE_AS_BUILT_DEEP_AUDIT.md` | 04 | Database & persistence deep audit (ERD, RLS, migrations) | 00–03 |
| 05 | `05_FRONTEND_AS_BUILT_DEEP_AUDIT.md` | 05 | Frontend as-built inventory (routes, screens, storage, HTTP) | 00–04 |
| 06 | `06_BUSINESS_MODULE_COVERAGE.md` | 06 | Business module coverage matrix (layer-by-layer proof) | 00–05 |
| 07 | `07_WORKFLOWS_AND_BUSINESS_RULES.md` | 07 | Workflows, user journeys & business-rule catalog | 00–06 |
| 08 | `08_SECURITY_AND_COMPLIANCE_DEEP_AUDIT.md` | 08 | Security & compliance deep audit (Part 11 / ISO evidence) | 00–07 |
| 09 | `09_TESTING_QUALITY_AND_CICD_AUDIT.md` | 09 | Tests, quality gates & CI/CD audit | 00–08 |
| 10 | `10_INTEGRATIONS_OPERATIONS_AND_OBSERVABILITY.md` | 10 | Integrations, jobs, observability & operations | 00–09 |
| 11 | `11_REQUIREMENTS_TRACEABILITY.md` | 11 | Requirements traceability matrix | all prior + `docs/srs/` |
| 12 | `12_TECHNICAL_DEBT_AND_RISK_REGISTER.md` | 12 | Technical debt, risk & release-blocker register | all prior |
| 13 | `13_AS_BUILT_VS_TARGET_ARCHITECTURE.md` | 13 | Target-architecture conformance gap analysis | all prior + target package (§5) |
| 14 | `14_REVIEWER_ONBOARDING_GUIDE.md` | 14 | Reviewer onboarding guide | all prior |
| 15 | `15_FINAL_RECONCILIATION_AND_QA.md` | 15 | Final evidence reconciliation & documentation QA | everything in this folder |

Pack operating rules binding on Documents 01–15, in addition to §0: implementation status uses exactly the vocabulary **Fully Implemented / Partially Implemented / Prototype Only / Missing** (the §5 evidence classes remain for typing individual claims); all documents are Markdown and all diagrams are Mermaid; SRS/architecture documents are requirements references only, never implementation proof. Each document must also: (a) restate its evidence-class legend, (b) cite this manifest's baseline commit and record any drift from it, (c) update Appendix A observation statuses it touches, and (d) end with its own §8-style no-modification attestation.

---

## 7. Evidence-quality rubric

| Confidence | Definition | Examples |
|---|---|---|
| **High** | Direct Tier 1–2 citation (file:line) corroborated by ≥2 independent artifacts (code + migration, code + test, code + config) | Aggregate invariant in Domain + covering unit test |
| **Medium** | Single uncorroborated Tier 1–4 citation; or manifest/config-derived; or structure/naming inference | Package version from csproj; endpoint inferred from controller name only |
| **Low** | Tier 5–6 only (documentation, UI text, HTML reports, generated artifacts); or extrapolation | "460 tests pass" per CLAUDE.md; compliance percentages in audit HTML |

Rules: runtime behavior claims cap at **Medium** in this static review. `Documentation-only`/`Unknown` findings are always **Low**. Confidence is stated per finding, not per document.

---

## 8. Reviewer checklist — no-modification attestation (Prompt 1)

- [x] No file under `src/`, `tests/`, `frontend/`, `scripts/`, `deploy/`, `.github/`, `.config/` was created, modified, or deleted by this review.
- [x] No build, test, migration, restore, or package operation was executed (`dotnet`/`npm`/`docker`/`ef` untouched).
- [x] No database connection was opened; no infrastructure was started or stopped.
- [x] Only read-only `git` commands were run (`branch`, `log`, `rev-list`, `tag`, `status`, `remote -v`); no commit, stage, tag, or push.
- [x] The only filesystem write is this document: `docs/as-built-review/00_REVIEW_MANIFEST.md` (new folder + file). Expected `git status` delta vs §1.2: exactly one new untracked path `docs/as-built-review/`.
- [x] Pre-existing dirty files (§1.2) were left untouched.
- [x] No secret values reproduced: configuration reported by section/key name only; compose/CI credentials marked REDACTED; remote URL verified credential-free.
- [x] No functionality, endpoints, data models, or tests were invented; deep behavioral analysis deferred to Docs 01–11.

---

## Appendix A — Pre-registered observations (baseline stage; NOT yet analyzed)

Recorded during evidence collection for follow-up by the owning documents. These are observations, not findings; each will be confirmed/refuted with citations by the listed document.

| ID | Observation | Evidence | Follow-up |
|---|---|---|---|
| OBS-01 | Version drift: `CLAUDE.md` states code at **v1.52.0**; newest git tag is **v1.51.2**; `IMPLEMENTATION_LOG.md`'s last *numbered* entry is v1.48.0 (subsequent entries unnumbered) | `CLAUDE.md` §3; `git tag`; `IMPLEMENTATION_LOG.md` tail | Doc 01, 02, 15 |
| OBS-02 | Migration count **verified consistent**: 59 migrations on disk = CLAUDE.md's claim of 59 (an earlier draft count of 58 was a filename-filter artifact — `ReportingKpiSnapshots` matches "Snapshot") | §2.2 verification | Closed at baseline |
| OBS-03 | `README.md` materially stale: "114 automated tests", "API-only; frontend remaining", Angular 18, increments stop at v1.0 — contradicts Angular 22 / v1.5x reality | `README.md`; `frontend/package.json` | Doc 01, 14, 15 |
| OBS-04 | "Angular 18" drift also in `ONBOARDING.md` and `frontend/package.json` `description` (deps are `^22.0.8`) | those files | Doc 05 |
| OBS-05 | `tests/NT.QAMS.LoadTests` outside the solution — intentional per csproj comment; means default `dotnet test` excludes it | `tests/NT.QAMS.LoadTests/NT.QAMS.LoadTests.csproj` | Doc 09 |
| OBS-06 | Toolchain determinism: floating NuGet wildcards, no `global.json`, no central package management; `dotnet-ef` 10.0.10 (`rollForward:false`) vs net9.0 stack requires .NET 10 runtime for EF CLI | §3.3 | Doc 02, 09, 12 |
| OBS-07 | `docs/testing/` case-file gaps: module 13 has cases A and D only; modules 14–23 have no case files | `docs/testing/` listing | Doc 09 |
| OBS-08 | Repo docs state schema hardening "not executed on a qualified/staging installation" and OQ transcripts 12–13 unsigned; CLAUDE.md lists SEC-001 (pen test), DOC-001 (qualified-env validation), OPS-001 (staging observability/load) as open | `SCHEMA-HARDENING-REPORT.md` header; `CLAUDE.md`; `docs/validation/12-…`, `13-…` | Doc 08, 09, 12 |
| OBS-09 | Baseline working tree dirty: 3 modified files + 3 untracked SRS HTML artifacts (§1.2) — review cites HEAD unless stated | `git status --short` | All docs |
| OBS-10 | OpenTelemetry Prometheus exporter is a **pre-release** package (`1.17.0-beta.1`) in the production WebApi | `src/NT.QAMS.WebApi/NT.QAMS.WebApi.csproj` | Doc 02, 10, 12 |

---

*End of Document 00 (v1.1). Baseline established; Prompt 01 (`01_EXECUTIVE_SUMMARY.md` — Executive Product and Maturity Assessment) may proceed against commit `d74d4bf`.*
