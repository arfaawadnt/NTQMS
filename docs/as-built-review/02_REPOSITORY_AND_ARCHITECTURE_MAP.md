# NT.QAMS — AS-BUILT Review · Document 02 · Repository, Solution, and Dependency Map

| Field | Value |
|---|---|
| Series / contract | AS-BUILT Review per [`00_REVIEW_MANIFEST.md`](00_REVIEW_MANIFEST.md) (v1.1) |
| Owner prompt | 02 — Repository, Solution, and Dependency Map |
| Commit reviewed | `d74d4bff733d49361a1b4da1e1b3ef3e8338af06` (`master`) — **identical to the manifest baseline; no drift** |
| Review date | 2026-08-02 |
| Method | Static source inspection only; reuses evidence adversarially verified for Document 01 (Appendix B there), plus targeted scans run for this document (duplicate/dead-file checks, marker greps) |

**Evidence-class legend (manifest §5):** `Implemented` · `UI-only` · `Documentation-only` · `Mocked` · `Missing` · `Unknown`. **Status vocabulary:** Fully Implemented / Partially Implemented / Prototype Only / Missing. **Confidence:** High = ≥2 independent artifacts; Medium = single citation/inference; Low = documentation only.

---

## 1. Tree map (grouped)

### 1.1 Backend (`src/` — 6 projects, all `net9.0`)

| Project | Key folders | `.cs` | Role evidence |
|---|---|---|---|
| `src/NT.QAMS.SharedKernel/` | `Abstractions/`, `Primitives/`, `MultiTenancy/`, `Localization/` | 11 | Zero-dependency primitives (AggregateRoot, ITenantScoped, IClock) |
| `src/NT.QAMS.Domain/` | 19 module folders (`AnalyticalQuality/` … `Tenancy/`) | 56 | Aggregates, invariants, `PermissionCatalog`, `WestgardEvaluator`, Part 11 ledger entities |
| `src/NT.QAMS.Contracts/` | 14 module folders | 22 | Wire DTOs only; zero dependencies |
| `src/NT.QAMS.Application/` | 18 module folders + `Abstractions/` + `Behaviors/` | 89 | CQRS slices (219 `ICommandHandler` refs), 5 pipeline behaviors, `IAppDbContext` port |
| `src/NT.QAMS.Infrastructure/` | `Persistence/` (Configurations, **Migrations** 59, Interceptors, Outbox), `Security/`, `Compliance/`, `Authorization/`, `Email/`, `Exports/`, `Storage/`, `Jobs/`, `Health/`, `Observability/`, `Configuration/` | 165 | All adapters; largest project |
| `src/NT.QAMS.WebApi/` | `Controllers/` (42 files / 54 classes), `Middleware/` (6 custom), `Authorization/`, `Security/`, `Startup/`, `Versioning/`, `Dockerfile` | 58 | Host; only `Microsoft.NET.Sdk.Web` project |

### 1.2 Frontend (`frontend/` — single Angular 22 workspace, 221 `.ts` under `src/`)

| Area | Contents |
|---|---|
| `src/app/core/` | AuthService (memory-only token), guards (2), interceptors (2: auth, change-reason), PermissionsService, i18n (1,683-line trilingual dictionary), `core/api/` = **44 typed HTTP services**, `core/help/` |
| `src/app/features/` | **28 lazy feature folders** (analytical is largest at 48 files); 35 signal facades |
| `src/app/shared/ui/` | 22 presentational components (page-header, drawer, load-more, status-pill, risk-matrix, levey-jennings…) |
| `src/app/shell/` | Authenticated chrome + 9-group navigation (`shell.component.ts:266-353`) |
| Root | `app.routes.ts` (84 lazy routes, dual-plane), `main.ts` (bootstrap), single `environments/environment.ts`, `styles.css` (design tokens), `proxy.conf.json`; `e2e/` (3 Playwright specs), `playwright.config.ts` |

### 1.3 Tests (`tests/`)

5 in-solution projects (Domain.UnitTests 38 files, Application.UnitTests 17, Architecture.Tests 4, IntegrationTests 12, WebApi.FunctionalTests 28 incl. `ApiSurface.approved.txt`) + **`NT.QAMS.LoadTests` deliberately outside the `.sln`** (BCL-only console harness; csproj comment: "Not part of the solution's test suite; invoked explicitly").

### 1.4 Documentation

`docs/adr/` (9 ADRs) · `docs/reference/` (24 files incl. vendored byte-identical copies of the 4 target-architecture docs) · `docs/srs/` (19 md + 21 html) · `docs/testing/` (24) · `docs/validation/` (15, GAMP 5 set) · `docs/as-built-review/` (this series) · root: `CLAUDE.md`, `README.md`, `ONBOARDING.md`, `IMPLEMENTATION_LOG.md` (778 lines / 29 entries), `SCHEMA-HARDENING-PLAN/REPORT.md`.

### 1.5 CI/CD

`.github/workflows/ci.yml` (single workflow, 3 jobs — see §9) + `.github/npm-audit-allowlist.txt` (exception register, currently zero active entries).

### 1.6 Infrastructure / operations

`docker-compose.yml` (dev PG only) · `deploy/` (8 runbooks, `compose.production.yml`, `db-init.sql`, `harden-runtime-role.sql`, generated `migrations.sql`, `Deploy-FullStack.ps1`, `backup.sh`/`restore.sh`, `web.config`, `iis/`, `observability/` OTel+Prometheus+Grafana stack, versioned build zips) · `scripts/` (10 `.ps1` + 2 pre-flight `.sql`) · `.config/dotnet-tools.json` · `.claude/skills/` (4 repo skills).

## 2. Solution/project dependency matrix

| Project | SDK / output | TFM | Project refs | Packages (versions per csproj) | Entry point |
|---|---|---|---|---|---|
| SharedKernel | Sdk / lib | net9.0 | — | **none** | — |
| Domain | Sdk / lib | net9.0 | SharedKernel | **none** (purity verified by grep + `LayerRulesTests`) | — |
| Contracts | Sdk / lib | net9.0 | — | **none** | — |
| Application | Sdk / lib | net9.0 | Domain, Contracts | MediatR `12.4.*`, FluentValidation.DI `11.11.*`, **EF Core `9.0.*`** (see §6.3), Logging.Abstractions `9.0.*` | — |
| Infrastructure | Sdk / lib | net9.0 | Application | EF Core + Relational + Npgsql `9.0.*`, EFCore.NamingConventions `9.0.*`, Identity.Core `9.0.*`, Jwt `8.2.*`, ClosedXML `0.105.0`, QuestPDF `2026.7.1`, HealthChecks/Hosting abstractions | — |
| WebApi | **Sdk.Web** / exe | net9.0 | Application, Contracts, Infrastructure | JwtBearer `9.0.*`, Asp.Versioning.Mvc `8.1.0`, OpenApi `9.0.*`, EFCore.Design `9.0.*`, OpenTelemetry `1.17.0` set (+ Prometheus exporter **`1.17.0-beta.1`**, Npgsql.OTel `9.0.4`) | `Program.cs` (top-level host) |
| 5 test projects | Sdk / lib | net9.0 | per target layer | xunit `2.9.*`, FluentAssertions `6.12.*`, NetArchTest `1.3.*`, Mvc.Testing `9.0.*`, EFCore.InMemory `9.0.*`, SkippableFact `1.5.*` | — |
| LoadTests (outside sln) | Sdk / **exe** | net9.0 | — | **none** (BCL-only) | `Program.cs` |
| frontend (npm) | Angular CLI workspace | TS ~6.0.3 | — | @angular/* `^22.0.8`, rxjs `~7.8.0`, zone.js `~0.15.1`; dev: Karma/Jasmine, Playwright `^1.62.0`, axe | `src/main.ts` → `app.config.ts` |

**Determinism facts (High):** no `Directory.Build.props`/`Directory.Packages.props`/`global.json`/`nuget.config`; NuGet versions are floating wildcards; `.config/dotnet-tools.json` pins `dotnet-ef` **10.0.10** (`rollForward:false`) — one major ahead of the net9.0 stack (OBS-06). `InternalsVisibleTo`: Infrastructure → Application.UnitTests + IntegrationTests; WebApi → WebApi.FunctionalTests.

## 3. Actual architectural layers and responsibilities

| Layer | Responsibility as-built | Enforcement |
|---|---|---|
| SharedKernel | Cross-cutting primitives, tenancy/clock abstractions | `LayerRulesTests` (references nothing internal) |
| Domain | Aggregates with private setters/factories/guarded state machines; permission catalogue; lab statistics; Part 11 record types | Zero packages; module-boundary test (18 modules, cross-module by Id only) |
| Contracts | Wire DTOs, module-grouped | No Domain/Application types (tested) |
| Application | CQRS handlers/validators; 5-behavior MediatR pipeline (Tracing→Logging→Authorization→Idempotency→Validation, `DependencyInjection.cs:20-24`); ports (`IAppDbContext`, `IFileStorage`, `IESignatureService`…) | `CommandPolicyTests`: every command carries exactly one authorization policy |
| Infrastructure | EF Core persistence (6 interceptors, tenant GUC first), outbox processor + 4 recurring jobs, compliance ledger services, security adapters (JWT/TOTP/hashing), exports, email adapter, local file storage, health, observability | Never references WebApi (tested) |
| WebApi | Host composition: authn, deny-by-default fallback policy, 6 custom middlewares in fixed order (`Program.cs:250-272`), versioned controllers, health/metrics endpoints, DB-role boot guard | `ApiSurfaceSnapshotTests` (666-line route contract) |
| Frontend core | Auth/session (memory token + silent refresh), permission gating (UX), i18n/RTL, typed API clients | Zero `HttpClient` outside `core/` (verified grep) |
| Frontend features | 28 lazy folders, standalone OnPush components + signal facades | Bundle budgets (2 MB error) |

## 4. Component/dependency diagram

```mermaid
flowchart TB
  subgraph Frontend["frontend/ (Angular 22)"]
    FEAT["features/ (28)"] --> FAC["facades (35)"] --> APIS["core/api (44 services)"]
    SHELL["shell + guards + interceptors"] --> APIS
  end
  subgraph Backend["src/ (.NET 9)"]
    WEB["NT.QAMS.WebApi"] --> APP["NT.QAMS.Application"]
    WEB --> CON["NT.QAMS.Contracts"]
    WEB --> INF["NT.QAMS.Infrastructure"]
    INF --> APP
    APP --> DOM["NT.QAMS.Domain"]
    APP --> CON
    DOM --> SK["NT.QAMS.SharedKernel"]
  end
  APIS -->|same-origin /api (ADR-0007)| WEB
  INF --> PG[("PostgreSQL 17<br/>qams/audit/saas/read · FORCE RLS")]
  INF --> DISK[("Local file store<br/>content-addressed SHA-256")]
  INF -.->|adapter present; behavior → Doc 10| SMTP[("SMTP")]
  WEB -->|OTLP when Otlp:Endpoint set| OTEL[("OTel collector / Prometheus")]
  RP["Reverse proxy (IIS/ARR or compose loopback)<br/>TLS termination (ADR-0002)"] --> WEB
  BR["Browser"] --> RP
  TESTS["tests/ (5 projects + LoadTests)"] -.-> WEB & INF & DOM & APP
```

External systems as-built: **PostgreSQL 17, local disk, optional OTLP collector, SMTP adapter** — nothing else (no queue, no Redis, no third-party APIs; verified negative search, Doc 01 §2).

## 5. Request lifecycle (browser → persistence, currently implemented path)

```mermaid
sequenceDiagram
  participant B as Browser (SPA component)
  participant F as Signal facade → core/api service
  participant I as Angular interceptors<br/>(auth bearer, X-Change-Reason)
  participant M as WebApi middleware chain<br/>Forwarded→Observability→SecurityHeaders→ExceptionHandler→AuthN→RateLimiter→TenantResolution→ActiveSession→MfaGate→ChangeReason→AuthZ
  participant C as Controller<br/>[RequirePermission(module, action)]
  participant P as MediatR pipeline<br/>Tracing→Logging→Authorization→Idempotency→Validation
  participant H as Command/Query handler
  participant D as Domain aggregate<br/>(invariants, events)
  participant E as EF SaveChanges interceptors<br/>TenantGUC→AuditStamp→TenantStamp→FieldChange→Outbox→OrgScopeGuard
  participant G as PostgreSQL 17<br/>FORCE RLS on app.current_tenant

  B->>F: user action
  F->>I: typed HTTP call (/api/v1/…)
  I->>M: request + JWT + change reason
  M->>C: tenant context resolved from JWT only
  C->>P: ISender.Send(command)
  P->>H: policy verified (AUTHZ-000 fail-closed), idempotency, validation
  H->>D: load aggregate, invoke guarded transition
  D-->>H: domain events raised
  H->>E: SaveChanges (single transaction)
  E->>G: SQL under tenant GUC; outbox + field-change rows co-committed
  G-->>B: DTO or problem+json (stable error codes)
  Note over E,G: async: OutboxProcessor publishes events in-process,<br/>appends hash-chained audit_trail entries (SKIP LOCKED, backoff, dead-letter)
```

Every hop above carries verified citations in Document 01 (§2, §4) — middleware order `Program.cs:250-272` *(ADJUSTED→CONFIRMED)*, pipeline order `DependencyInjection.cs:20-24` *(CONFIRMED)*, interceptor order `Infrastructure/DependencyInjection.cs:44-64`, GUC stamping `TenantConnectionInterceptor.cs:23-56` *(CONFIRMED)*.

## 6. Violations of Clean Architecture / CQRS claims

| # | Check | Verdict | Evidence |
|---|---|---|---|
| 6.1 | Controllers accessing DbContext directly | **2 deliberate, narrow cases** — `FilesController` (streaming) and `ExportsController` (formatting) query via the Application-layer **`IAppDbContext` abstraction**, never concrete `AppDbContext`/`DbSet` fields; all other 52 controller classes go through `ISender`. | grep over `Controllers/` → only `FilesController.cs:17`, `ExportsController.cs:26` (High) |
| 6.2 | Domain depending on EF/ASP.NET/MediatR | **Clean** — zero packages, zero matching usings; enforced by executable `LayerRulesTests`. | `Domain.csproj:10-12`; grep; `LayerRulesTests.cs:35-48` (High) |
| 6.3 | Framework leakage into Application | **Declared deviation, not drift**: Application references `Microsoft.EntityFrameworkCore 9.0.*` because the persistence port is expressed as EF `DbSet`s (`IAppDbContext`) — a recorded architecture decision (`docs/adr/ADR-0008-persistence-port-ef-dbset.md`). Purist Clean Architecture would forbid this; as-built it is intentional, documented, and consistently applied. | `Application.csproj:11-14`; ADR-0008 (High) |
| 6.4 | Business logic in UI | **None found**: zero `HttpClient` imports under `features/`; QC verdicts, z-scores, and state transitions computed server-side (facade doc comments state "computed server-side"); UI permission checks are UX-only duplicates of server gates. | Doc 01 §2/§4 citations (High) |
| 6.5 | CQRS integrity | Commands: fail-closed policy gate + exactly-one-policy merge gate. **Queries deliberately bypass the pipeline authorization behavior** — read authorization lives at the controller layer only (2 layers, not 3). Not a defect, but an asymmetry reviewers must know. | `AuthorizationBehavior.cs:44-47`; round-2 ADJUSTED verdict (High) |
| 6.6 | Layer-2 endpoint gate is opt-in | `[RequirePermission]` appears 152× but **no test forces an endpoint to carry it**; an endpoint that omits it falls back to authenticated-user-only. Compensated for commands by 6.5; reads rely on convention. | round-2 ADJUSTED verdict (High) |
| 6.7 | Facade-pattern consistency | `roles` and `platform` components inject API services directly (no facade) — cosmetic deviation from the standard component→facade→service shape. | `roles.component.ts:171`; `tenants.component.ts:106` (High) |
| 6.8 | Transitional authorization | NC state-transition commands use coarse `[RequireInternalActor]` pending fine-grained catalogue gates (controller comment defers to "full Phase 1"). | `NonconformancesController.cs` (High) |

**Net:** the Clean Architecture/CQRS claims survive inspection; deviations are few, deliberate, and mostly self-documented. Do not infer stricter purity than ADR-0008 actually promises.

## 7. Duplicate, generated, dead, unused, obsolete, suspicious (nothing deleted)

| Category | Finding | Evidence |
|---|---|---|
| **Duplicate** | `NT_QMS_Complete_SRS_Sameh.html` is **byte-identical** to `NT_QMS_Complete_SRS.html` (378,227 B; both untracked) — a named copy for a person, pure duplication | `cmp` verified this review (High) |
| Duplicate (intentional) | `docs/reference/NT_QAMS_*.md` ×4 are byte-identical to the repo-parent target-architecture originals — vendored baseline, correct to keep | `cmp` verified (High) |
| **Generated** (excluded as evidence) | 59 `*.Designer.cs` + `AppDbContextModelSnapshot.cs`; `deploy/migrations.sql` (349 KB bundle); `deploy/publish-win-x64/` + 6 versioned zips (~186 MB, gitignored); `frontend/dist/`, `frontend/.angular/`, `frontend/test-results/`; `package-lock.json` | listings (High) |
| **Dead code markers** | **Zero** `TODO`/`FIXME`/`HACK` in all `src/**/*.cs` and all non-spec `frontend/src/**/*.ts`; zero `*.bak/*.old/*.orig/*.tmp` anywhere in source trees — the CLAUDE.md "no dead code, no TODOs" policy holds as-built | greps run this review (High) |
| **Obsolete/stale** | `README.md` (v1.0 API-only narrative, "114 tests", Angular 18); `ONBOARDING.md` (Angular 18); `deploy/ANTIGRAVITY_DEPLOY_PROMPT.md` (superseded v1.0 backend-only deploy prompt, two newer prompts coexist); `compose.production.yml` image tag `1.43` vs claimed v1.52.0; `frontend/package.json:4` description "Angular 18 frontend" | OBS-01/03/04 confirmations (High) |
| **Unused** | `deploy/web.config:8` reserves a `/hubs` proxy route for SignalR — **no hub exists anywhere in code** | round-2 ADJUSTED verdict (High) |
| **Suspicious dependencies** | `dotnet-ef` 10.0.10 pinned with `rollForward:false` (requires a .NET 10 runtime on any machine running EF CLI, incl. CI); OpenTelemetry Prometheus exporter is **pre-release** (`1.17.0-beta.1`) in the production host; floating `9.0.*`-style wildcards across all csproj (non-reproducible restores); `istanbul-lib-instrument` pinned explicitly in frontend devDeps (coverage-chain pin — unusual but benign) | `.config/dotnet-tools.json`; `WebApi.csproj:24-27`; `frontend/package.json` (High) |
| Working-tree extras | 3 modified files + 3 untracked SRS HTML artifacts predate this review (manifest §1.2) and remain uncommitted | `git status` (High) |

## 8. Configuration sources and environment assumptions

**Sources (values REDACTED; keys only):**

| Source | Keys / behavior |
|---|---|
| `src/NT.QAMS.WebApi/appsettings.json` | `ConnectionStrings`, `Database`, `PasswordPolicy`, `AnalyticalQuality` (Westgard limits — DI-validated at startup), `Jwt`, `Logging`, `AllowedHosts` — secret-valued keys are **empty by design** |
| `appsettings.Development.json` | `_comment` ("No secrets in source… user-secrets… Production supplies secrets via environment variables"), `ConnectionStrings`, `Jwt`, `PlatformAdmin` (bootstrap admin; seeding skips when empty), `Logging` |
| User secrets | `UserSecretsId = nt-qams-webapi` (`WebApi.csproj`); documented in `deploy/DEV-SECRETS.md` |
| Environment variables | `ConnectionStrings__Postgres`, `Jwt__Secret`, `Otlp__Endpoint`, `POSTGRES_PASSWORD` as required `${VAR:?}` placeholders in `deploy/compose.production.yml`; `Security__RequireMfaForPrivilegedRoles` is honored but **set by no artifact** (Doc 01 risk #3); `QMS_ITEST_POSTGRES` gates the real-DB test suite |
| Fail-fast guard | `ConfigGuard` refuses startup on present-but-invalid values (proven by `ConfigGuardTests`); `Jwt:Secret` < 32 chars aborts startup; production boot refuses SUPERUSER/BYPASSRLS DB roles (warns-and-defers only if DB unreachable at probe) |
| Frontend | Single `environment.ts` (`production: true`, `apiBaseUrl: '/api'`), **no `fileReplacements`** — only one buildable configuration exists; dev proxy `proxy.conf.json` → `http://localhost:5080` |

**Environment assumptions (as coded/declared):** PostgreSQL 17 reachable with the two-role model (`qams_owner` DDL / `qams_app` runtime, `deploy/db-init.sql`); same-origin reverse proxy — **no CORS configured anywhere** (ADR-0007); TLS terminated upstream (IIS/ARR or loopback compose, ADR-0002) with `ForwardedHeaders` first in the pipeline; **single replica** (ADR-0001, actively enforced by `SingleReplicaGuardService`); writable local disk at `FileStorage:RootPath` (default `./data/files`); OTLP collector optional; dev ports 5080 (API) / 4200 (SPA), production compose binds `127.0.0.1:5000→8080`; CI assumes .NET SDK 9.0.x + Node 24; **EF CLI requires a .NET 10 runtime** (tool pin).

## 9. Build, test, migration, lint, and run commands discovered

⚠ **None of these were executed by this review** (attestation, Appendix C). "CI-defined" means the command appears in `ci.yml` and runs on push/PR per the workflow definition — pipeline outcomes are unobservable here.

| Purpose | Command | Source | Execution evidence |
|---|---|---|---|
| Restore/build | `dotnet restore NT.QAMS.sln` · `dotnet build --configuration Release` | `ci.yml` build-test job | CI-defined |
| Backend tests | `dotnet test NT.QAMS.sln --no-build` with `QMS_ITEST_POSTGRES` set (RLS suite hard-fails instead of skipping) | `ci.yml:85-92` | CI-defined |
| Migrations | `dotnet tool restore` then `dotnet ef database update -p src/NT.QAMS.Infrastructure -s src/NT.QAMS.WebApi` | `ci.yml`; `.config/dotnet-tools.json` | CI-defined (needs .NET 10 runtime for the tool) |
| NuGet SCA | `dotnet list … package --vulnerable --include-transitive` (fail on High/Critical) | `ci.yml:54-60` | CI-defined |
| Frontend | `npm ci` · `npm run test:ci` (ChromeHeadless) · `npm run build` · `npm start` (dev, proxied) · `npm run e2e` | `frontend/package.json` scripts; `ci.yml` frontend job | CI-defined (unit+build+auth/a11y e2e); `regulated-workflow` e2e **not** in CI |
| Container | `docker build -f src/NT.QAMS.WebApi/Dockerfile …` + non-root assertion + Trivy scan | `ci.yml` container job | CI-defined |
| Dev lifecycle | `scripts/dev-up.ps1` / `dev-down.ps1` / `dev-rebuild.ps1` / `dev-status.ps1` (API :5080, SPA :4200, detached; stop-by-port-owner) | script headers | Discovered — unverified |
| Ops drills | `scripts/failure-drills.ps1`, `perf-smoke.ps1`, `security-probe.ps1`, `security-probe-deep.ps1`, `staging-smoke.ps1`, `verify-e2e.ps1` — all against a **running** system | script headers | Discovered — unverified; none in CI |
| Load | `dotnet run --project tests/NT.QAMS.LoadTests` (against a running API) | LoadTests csproj/README | Discovered — unverified; never in CI |
| Deploy | `deploy/Deploy-FullStack.ps1`; `docker compose -f deploy/compose.production.yml up`; `psql -f deploy/db-init.sql` / `harden-runtime-role.sql`; `backup.sh`/`restore.sh`; `deploy/iis/*.ps1` | deploy tree | Discovered — unverified |
| **Lint** | **No lint command exists** — no ESLint/Prettier/dotnet-format/.editorconfig anywhere; quality gates are TS strict + `TreatWarningsAsErrors` (src only) + architecture tests + bundle budgets | verified absence | As-built fact |

---

## Appendix A — Manifest Appendix A observation updates (touched by this document)

| OBS | Update |
|---|---|
| OBS-01 (version drift) | Re-confirmed; §7 adds the stale deploy-prompt trio as related staleness. Carry to Doc 15. |
| OBS-03 (README stale) / OBS-04 (Angular-18 metadata) | Re-confirmed in §7. Carry to Docs 05/14/15. |
| OBS-05 (LoadTests outside sln) | Re-confirmed (§1.3, §9). Carry to Doc 09. |
| OBS-06 (toolchain determinism) | **Confirmed in full** (§2, §7): floating wildcards + no global.json/CPM + dotnet-ef 10.0.10 pin. Carry to Docs 09/12. |
| OBS-10 (pre-release Prometheus exporter) | Re-confirmed (§2). Carry to Docs 10/12. |
| **New — NB-02-01** | `NT_QMS_Complete_SRS_Sameh.html` is a byte-identical untracked duplicate of `NT_QMS_Complete_SRS.html` — flag for cleanup decision and for the Doc 15 contradiction register. |
| **New — NB-02-02** | Zero TODO/FIXME/HACK markers and zero backup files in source — positive conformance to the repo's own hygiene policy (useful baseline for Doc 12's debt hotspot analysis). |

## Appendix B — Reviewer no-modification attestation (manifest §8 model)

- [x] No file under `src/`, `tests/`, `frontend/`, `scripts/`, `deploy/`, `.github/`, `.config/` was created, modified, or deleted.
- [x] No build, test, migration, restore, package, or container operation was executed; no database connection was opened.
- [x] Only read-only access was used (file reads, `grep`/`find`/`cmp`, read-only `git`).
- [x] The only filesystem write is this document: `docs/as-built-review/02_REPOSITORY_AND_ARCHITECTURE_MAP.md`.
- [x] No secret values are reproduced; configuration is cited by key name only.
- [x] Nothing was invented; claims reuse adversarially verified Document 01 evidence or carry fresh citations from this document's scans.

---

*End of Document 02. Reviewed at manifest baseline `d74d4bf` (no drift). Next: Prompt 03 → `03_BACKEND_AND_API_INVENTORY.md`.*
