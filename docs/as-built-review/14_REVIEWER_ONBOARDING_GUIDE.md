# NT.QAMS — AS-BUILT Review · Document 14 · Reviewer Onboarding Guide

| Field | Value |
|---|---|
| Series / contract | AS-BUILT Review per [`00_REVIEW_MANIFEST.md`](00_REVIEW_MANIFEST.md) (v1.1) |
| Owner prompt | 14 — Reviewer Onboarding Guide |
| Commit reviewed | `d74d4bff733d49361a1b4da1e1b3ef3e8338af06` (`master`) — baseline; no drift |
| Review date | 2026-08-02 |
| Method | Practical guide synthesizing Documents 01–13 + `CLAUDE.md §6`. **No command in this guide was executed by the review** — all are labelled **[discovered]** (found in the repo, not run here). Nothing is marked [verified]. |

**For a new technical reviewer.** This gets you from clone to "I can trace any feature and find any control" as fast as the repo allows. It does **not** certify the build — see Documents 08/12 for risks and the three open blockers.

---

## 1. What you're looking at (30-second orientation)

NT.QAMS is a **multi-tenant SaaS Quality Management System for ISO/IEC 17025 labs** (also ISO 9001, 21 CFR Part 11). Stack: **.NET 9 · ASP.NET Core · Clean Architecture + CQRS/MediatR · EF Core 9 · PostgreSQL 17 · Angular 22** (single-page app). It is an **API-first system with complete vertical slices** (27 of 28 feature areas fully wired) — not a UI prototype (Doc 06). Persistence is **relational** (99 tables, FORCE row-level security per tenant); files sit on content-addressed local disk. No SignalR/Hangfire/Redis (Doc 13).

## 2. Runtime prerequisites, services, ports (from `CLAUDE.md §6`) [discovered]

| Need | Value |
|---|---|
| .NET SDK | .NET 9 (user-local: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe"`) — **note:** `dotnet-ef` is pinned to 10.0.10, so the EF CLI needs a .NET 10 runtime present (OBS-06) |
| Node | Node 24 at `C:\Program Files\nodejs` (Angular 22 needs Node ≥20.19); run CLI via `node node_modules/@angular/cli/bin/ng.js …` |
| PostgreSQL | 17 at `C:\Program Files\PostgreSQL\17\bin`; dev DB `ntqams`, runtime role `qams_app` |
| Ports | API `5080`, SPA `4200` (dev); production compose binds `127.0.0.1:5000→8080` |
| Secrets | not in git — user-secrets id `nt-qams-webapi`; provision per `deploy/DEV-SECRETS.md` (`ConnectionStrings:Postgres`, `Jwt:Secret`, `PlatformAdmin:Email/Password`) |
| Config sources | `appsettings.json` (secret keys empty) → `appsettings.Development.json` → user-secrets/env; `ConfigGuard` fails startup on present-but-invalid values |
| Known limitation | the running WebApi **locks its own DLLs** — stop the API before `dotnet build`/`dotnet ef`, then restart |

## 3. Build / run / test (all commands **[discovered]** from `CLAUDE.md §6` + `scripts/` — none executed here)

**Preferred: use the scripts (they start servers detached and stop-by-port, avoiding the "app randomly stopped" traps).**

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\dev-status.ps1   # FIRST when "app not working" — distinguishes port-down vs 503-DB-down vs healthy
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\dev-up.ps1       # start API :5080 + SPA :4200 (idempotent)
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\dev-rebuild.ps1  # stop → build (-Test, -Migrate) → always restart
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\dev-down.ps1      # stop both (-ApiOnly)
```

Manual equivalents (also [discovered]):

```bash
# API (Development)
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 Database__MigrateOnStartup=true \
  "$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" run --project src/NT.QAMS.WebApi --no-launch-profile
# Full test suite (integration tests need a real PostgreSQL)
QMS_ITEST_POSTGRES="Host=localhost;Database=ntqams;Username=qams_app;Password=<DEV-SECRET-REDACTED>" \
  "$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test
# EF migration apply
"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" ef database update --project src/NT.QAMS.Infrastructure --startup-project src/NT.QAMS.WebApi
# Frontend
cd frontend && npm ci
node node_modules/@angular/cli/bin/ng.js build --configuration production
node node_modules/@angular/cli/bin/ng.js test --watch=false --browsers=ChromeHeadless   # unit
node node_modules/@playwright/test/cli.js test                                           # e2e (API must be running)
```

**Fresh clone** (from `CLAUDE.md §8`): `git clone … && dotnet restore NT.QAMS.sln && (cd frontend && npm ci)`, create empty PG 17 db `ntqams` owned by `qams_app`, provision secrets, `dotnet ef database update`, then run.

> **Reviewer caution:** if you set `QMS_ITEST_POSTGRES`, the RLS integration suite **runs and hard-fails** rather than skipping (Doc 09). Without it, those tests silently skip — a green local `dotnet test` can hide RLS breakage. Set it to exercise the compliance-critical suite.

## 4. Startup paths [discovered from source]

- **Backend:** `src/NT.QAMS.WebApi/Program.cs` (single-file host). Order: config + `ConfigGuard` → JWT/authn → OpenTelemetry → EF + 6 interceptors → 5 hosted services → (Production) `DatabaseRoleGuard` refuses over-privileged DB role → optional `Database:MigrateOnStartup` → inline startup seeding (platform admin + LOVs + roles), deferred to `DeferredStartupSeeder` if the DB is unreachable → `app.Run()`. Middleware order and the request pipeline are in Doc 02 §5.
- **Frontend:** `frontend/src/main.ts` → `app.config.ts` (providers: router with input-binding, HTTP with auth + change-reason interceptors, `provideZoneChangeDetection`) → `APP_INITIALIZER` runs `AuthService.hydrate()` (one silent refresh from the httpOnly cookie) → `AppComponent` (sets `document.dir` for RTL) → routes.

## 5. Auth, tenant selection, persistence — as actually implemented [discovered]

- **Tenant front door:** navigate `/t/{lab-slug}` — this **pins the lab** for the browser (`auth.setTenantSlug`), logs out any cross-lab session, and redirects to `/login`. The tenant is defined by the URL, **never typed into the login form**.
- **Login:** tenant + email + password (+ MFA code if enabled). Access token lives in a **memory signal only** (ADR-0009); the refresh token is an httpOnly `Secure SameSite=Strict` cookie `qams_rt`. On a 401 the auth interceptor does **one silent refresh** and retries — routine 15-min expiry never bounces you.
- **Tenant isolation at runtime:** the tenant comes **only from the validated JWT `tenant_id` claim** (headers/query are banned), is stamped onto every DB connection via a GUC, and PostgreSQL FORCE RLS enforces it. Fail-closed: no tenant ⇒ nil UUID ⇒ zero rows.
- **Persistence:** all business state is relational (per-tenant RLS); files are content-addressed objects on disk (`{root}/{tenantId}/{sha256}`) with a `file_reference` row. **No business data is stored client-side** — only 5 UX-preference localStorage keys (§10).
- **Dev logins** (`CLAUDE.md §6`, **dev-only, passwords REDACTED**): tenant `demo-lab` → `admin@demo-lab.local`; platform admin → `platform-admin@localhost`. **Do not reuse dev credentials anywhere real.**

## 6. Recommended review order (read these first — from Doc 01 §9)

1. **`CLAUDE.md`** — the operating law *and* the honest open-items register (SEC-001/DOC-001/OPS-001).
2. **`src/NT.QAMS.WebApi/Program.cs`** — the entire host: authn, deny-by-default fallback policy, middleware order, DB-role boot guard.
3. **`src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs`** (+ `DependencyInjection.cs:20-24`) — the fail-closed authorization heart.
4. **`src/NT.QAMS.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`** — the whole 99-table schema in one file.
5. **`frontend/src/app/app.routes.ts`** — the complete product surface (84 lazy routes, dual-plane).

*Then:* `src/NT.QAMS.Infrastructure/Compliance/ComplianceLedgerServices.cs` (Part 11 machinery) and `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` (the 666-line API contract). And this review's own `docs/as-built-review/01_EXECUTIVE_SUMMARY.md` for the verdict.

## 7. How to trace a UI action end-to-end (the money skill)

Every feature follows the same chain — pick any register (e.g. Nonconformances):

```
UI component (features/nc/nc-list.component.ts)
  → signal facade (features/nc/nc.facade.ts)              // state: _list/_loading/_error/_selected
    → typed API service (core/api/nc-api.service.ts)       // one method per endpoint, base /api
      → HTTP → controller (WebApi/Controllers/NonconformancesController.cs)  // [RequirePermission] or [RequireInternalActor]
        → ISender.Send(command) → MediatR pipeline          // Tracing→Logging→Authz(fail-closed)→Idempotency→Validation
          → handler (Application/Improvement/…Handlers.cs)
            → domain aggregate (Domain/Improvement/Nonconformance.cs)  // guarded transition + SoD, raises event
              → EF SaveChanges (single txn) → tables + field_change + outbox co-committed
                → OutboxProcessor later publishes event → sagas + hash-chained audit_trail
```

Doc 03 has the full 333-route inventory; Doc 07 has every state machine.

## 8. Where things live (locator)

| Looking for | Location |
|---|---|
| A business **module** | backend `src/NT.QAMS.{Domain,Application}/<Module>/`, `WebApi/Controllers/<...>Controller.cs`; frontend `frontend/src/app/features/<module>/` + `core/api/<module>-api.service.ts` |
| A **workflow / state machine** | the domain aggregate (`src/NT.QAMS.Domain/<Module>/<Aggregate>.cs`) — states in the enum, transitions in the methods, guards throw structured codes (Doc 07) |
| **Permissions** | `src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs` (170 keys); endpoint gates `[RequirePermission(...)]`; command gates in the Application slices |
| **Audit log / e-signatures** | `audit` schema (`audit_trail` hash-chained, `electronic_signature`, `security_event`, `field_change`); code in `Infrastructure/Compliance/ComplianceLedgerServices.cs`; UI `shared/ui/audit-trail.component.ts` + `/compliance` |
| **Integrations / jobs** | `Infrastructure/Email/` (SMTP), `Infrastructure/Jobs/` (sweep, KPI, single-replica guard), `Infrastructure/Persistence/Outbox/` (event pump), `Infrastructure/Storage/` (files) |
| **Business rules** | domain aggregate guards (structured codes `NC-001`, `SOD-AQ-001`, …); `docs/srs/03-Business-Rules.md` |
| **API contract** | `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` |
| **This review** | `docs/as-built-review/00…14` |

## 9. Known unsafe / demo-only behavior — do NOT test these against production data

| Item | Why it's unsafe outside dev |
|---|---|
| **Dev logins & script credentials** (`scripts/dev-*.ps1`, `CLAUDE.md §6`) | dev-only passwords; never valid or safe against a real environment |
| **`Database:MigrateOnStartup=true`** | auto-applies migrations at boot — fine in dev, dangerous against a production DB |
| **Running as the DB owner in dev** | the least-privilege `DatabaseRoleGuard` runs **in Production only** (NB-04-03); dev runs as owner, which **can bypass RLS** — so a dev environment does **not** prove tenant isolation the way the CI real-PG suite does |
| **EF InMemory functional tests** | 21/22 functional test classes run on InMemory (Doc 09) — they do **not** prove RLS/FK/CHECK behavior; only the 4 real-PG tests do |
| **File download** | permission-ungated + unlogged (SEC-03) — do not treat download access as authorized/audited |
| **MFA** | off by default (SEC-02) — privileged accounts may be single-factor unless explicitly enabled |
| **SMTP** | best-effort, no retry (NB-10-01) — a dev SMTP outage silently drops email; the in-app feed is the record |

## 10. Questions to resolve with the owners (before trusting/releasing)

**Product Owner / Architecture Owner** (from Doc 13 AOD-1…6):
- **MFA policy (AOD-1):** the target mandates MFA for all active accounts; the build defaults it off. Which is the intended production posture?
- **File-storage tier (AOD-3):** is local-disk storage acceptable for production, or is the target's S3 + virus-scan + WORM required before GA?
- **Reporting aggregate (AOD-4)** and **no-Redis privilege eval (AOD-2):** ratify with ADRs or change?
- **E-signature scope (RISK-03):** is Part 11 signature manifestation on document-publish-only acceptable, or must audit/NC/quality-policy/change/review/AQ sign-offs also mint signatures?

**Compliance Officer / QA:**
- **DOC-001:** when will IQ/OQ/PQ be executed and **signed on a qualified environment**? (OQ transcripts 12/13 are currently unsigned; validation ran on a dev workstation.)
- Are the accepted deviations (**B9** two tables without RLS; **B10** historical nil-tenant rows) formally signed off for the regulated release?

**DevOps / Security:**
- **SEC-001:** who runs the independent penetration test, and when?
- **OPS-001:** when is the staging observability + ≥100-VU load + 24h soak scheduled? The 7 Prometheus alerts are defined but never deployed.
- **Secrets & rotation:** confirm the single HS256 `Jwt:Secret` rotation procedure (SEC-05, no `kid`); confirm `X-Forwarded-*` is restricted to the real proxy (NB-08-03).

## 11. Glossary

**Modules / abbreviations:** NC = Nonconformance · CAPA = Corrective/Preventive Action · PT/ILC = Proficiency Testing / Interlaboratory Comparison · QC = Quality Control (Westgard) · MV = Method Validation · MU = Measurement Uncertainty · SIG = Sigma metrics · SoD = Segregation of Duties · RLS = Row-Level Security · LOV = List of Values · ATR = Audit-Trail Review · UAR = User Access Review · COI = Conflict of Interest (impartiality) · MRV = Management Review · CHG = Change control · QP = Quality Policy · OBJ = Quality Objective · ARC = Archive · ENV = Environmental monitoring · RS = Reference Standard.

**Storage keys (localStorage, all non-secret UX):** `qams.tenant.slug` (last lab), `qams.login.theme`, `qams.sidebar.collapsed`, `qams.sidebar.groups`, `qams.lang`. Access token = memory only; refresh = httpOnly cookie `qams_rt`.

**Key routes:** `/t/:tenant` (front door) · `/login` · `/dashboard` · `/quality-analytics` · `/platform/tenants` (control plane) · `/nonconformances`, `/documents`, `/audits`, `/equipment`, `/qc`, `/roles`, `/compliance`, … (48 tenant routes; Doc 05 §2).

**Representative status vocabularies (Doc 07):** NC: `Draft→Raised→Assigned→Rca→ActionPlan→PendingVerification→EffectivenessCheck→Closed/Rejected` · Document version: `Draft→UnderReview→Approved→Published→Obsolete` · Analytical study: `DataEntry→Calculated→SignedOff` · Complaint: `Logged→Acknowledged→Validated/Invalid→Investigating→OutcomeLogged→Resolved→Closed` · Tenant: `Provisioning→Active⇄Suspended→Terminated`.

**Error-code → HTTP:** `*-404`→404 · `AUTH-*`→401 · `AUTHZ-*`→403 · concurrency→409 · other `DomainException`→422 · FluentValidation→400 (Doc 07 §4). **Watch:** `TestAuthorization` `AUTHZ-*` *domain* codes mis-map to 403 (NB-07-01).

---

## Appendix A — What this guide can and cannot tell you

Everything here is **[discovered]** from source — no command was executed by the review, no server started, no DB connected. Treat run/build/test instructions as accurate *descriptions of the repo's intent*, not as verified-working steps: verify them yourself in a dev environment. The runtime behaviors described (RLS enforcement, job cadences, health responses) are reconstructed from source + the CI integration suite, not observed here — the residual unknowns are exactly the DOC-001/SEC-001/OPS-001 items (Doc 12 Appendix B).

## Appendix B — Reviewer no-modification attestation (manifest §8 model)

- [x] No file was created, modified, or deleted; nothing was built, run, served, or connected to a database.
- [x] Only read-only synthesis of Documents 01–13 and `CLAUDE.md` was used.
- [x] The only filesystem write is this document: `docs/as-built-review/14_REVIEWER_ONBOARDING_GUIDE.md`.
- [x] No secret values reproduced — dev credentials and the test connection password are shown as REDACTED placeholders.
- [x] Nothing invented; every locator/behavior traces to an as-built finding in Documents 01–13; all commands are labelled **[discovered]**, none **[verified]**.

---

*End of Document 14. Reviewed at manifest baseline `d74d4bf` (no drift). Next: Prompt 15 → `15_FINAL_RECONCILIATION_AND_QA.md` (final step).*
