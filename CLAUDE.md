# CLAUDE.md — NT.QMS operating guide (READ FIRST)

This file is the single source of truth for how to continue developing NT.QMS. It is
auto-loaded by Claude Code. If you are continuing this project on a new machine or
account: read this file, then `README.md`, then `docs/reference/` — and follow the
standing rules below exactly. They override default behaviour.

---

## 1. What this is
NT.QMS is a **multi-tenant SaaS Quality Management System** for ISO 17025 / 15189 /
9001 / 21 CFR Part 11 / GMP laboratories.
**Stack:** .NET 9 · PostgreSQL 17 (Npgsql, snake_case) · Angular 18 · Clean Architecture + CQRS.
**Solution layout:** `src/NT.QAMS.{Domain, Application, Infrastructure, WebApi, Contracts, SharedKernel}` + `tests/NT.QAMS.{Domain.UnitTests, Application.UnitTests, Architecture.Tests, IntegrationTests, WebApi.FunctionalTests}` + `frontend/` (Angular).

## 2. Standing rules (the "law" — never break these)
1. **Do NOT redesign the domain, database, or public APIs.** The architecture docs in
   `docs/reference/` are law — extend within them, don't re-architect.
2. **No magic strings / magic numbers.** Roles live in `WebApi/Authorization/Roles.cs`;
   error codes are structured (`NC-001`, `SOD-AQ-001`, …); config is typed/centralized.
3. **No dead code, no TODOs, no mocks, no fake/placeholder screens** in production paths.
4. **XML doc comments** on public domain/application types; **strict TypeScript, no `any`**.
5. **Report gaps honestly.** Never claim compiled/tests-pass/migrations-applied unless you
   actually executed them. Don't inflate; surface risks.
6. **Domain protects itself:** private setters, factories, guarded state machines,
   invariants inside the aggregate (not in validators/handlers). DTOs in `Contracts`,
   never expose entities.
7. **Multi-tenancy is sacred:** every `ITenantScoped` table is protected by BOTH the EF
   global query filter AND PostgreSQL FORCE RLS. See rule in §5.
8. **Commit discipline:** work on `master`; commit/push only when asked; end commit
   messages with `Co-Authored-By: Claude <noreply@anthropic.com>` (use the current model).

## 3. Current state (as of 2026-07-28)
- Code at tag **`v1.42.0`** (EA remediation Phases 0–4 shipped — **all production blockers cleared at v1.41**; `restore-point-20260727` = v1.37.0). Repo: `github.com/arfaawadnt/NTQMS`.
- **All 18 CSV/regulatory-audit findings are CLOSED** (release train v1.25→v1.37): tenant
  RLS, signed-record immutability, MFA, SoD, reason-for-change, session revocation,
  e-sig logging, integration tests, backup/DR, governance modules, exports, password
  policy, config externalization, secrets, frontend+e2e tests, GAMP 5 CSV doc set.
- Tests green when last run: **~270 backend + 37 frontend unit + 3 Playwright e2e**.
- Two audits delivered: CSV/Part-11 (all closed) and **Enterprise Architecture** (~76%,
  0 critical, "approved with conditions").

## 4. What to do NEXT — the active plan
Follow **`docs/reference/NT_QMS_Enterprise_Architecture_Remediation_Plan.html`** — a gated
7-phase train (v1.38 → v1.44), in order. Ship each phase behind the CI gate; each finding
has acceptance criteria + a proving test. Summary:
- **Phase 0 (P0, v1.38): ✅ SHIPPED** — role guard (Production refuses over-privileged DB role), `/health/live`+`/health/ready`, single-replica ADR-0001 + advisory-lock sentinel.
- **Phase 1 (P1, v1.39): ✅ SHIPPED** — `xmin` concurrency + 409 `CONCURRENCY-409`; outbox dead-letter/backoff/retention + `SKIP LOCKED` claim lease; sweep leader election.
- **Phase 2 (P1, v1.40): ✅ SHIPPED** — JSON logs + canonical request log; OTel traces HTTP→MediatR→EF→Outbox (traceparent persisted on outbox rows); X-Correlation-Id + ProblemDetails traceId; /metrics + alert set (deploy/OBSERVABILITY.md).
- **Phase 3 (P1/P2, v1.41): ✅ SHIPPED** — rate limiting (global + auth/e-sign partitions, 429); security headers + locked CSP; TLS-at-proxy + in-app HSTS (ADR-0002); token-storage risk acceptance + 60-min tokens (ADR-0003).
- **Phase 4 (P2, v1.42): ✅ SHIPPED** — problem+json everywhere; pagination envelope (API+SPA); file allow-list/sniffing; deny-by-default command authorization (211 annotated + CI gate); Idempotency-Key replay; api/v1 versioning (ADR-0004).
- **Phase 5 (P2, v1.43):** DB CHECK constraints, `ValidateOnStart` options, non-root container, DB retry.
- **Phase 6 (P2/P3, v1.44):** test-coverage gaps, ADRs, module-boundary arch test, frontend a11y polish.
Completing **Phases 0–3** clears every production blocker → unconditional release.

## 5. Reusable lessons / conventions (must-follow)
- **New `ITenantScoped` table ⇒ add RLS in its OWN migration** (EF won't). In `Up()` after
  CreateTable: `ALTER TABLE qams.<t> ENABLE ROW LEVEL SECURITY; ALTER TABLE ... FORCE ROW LEVEL SECURITY; DROP POLICY IF EXISTS tenant_isolation ...; CREATE POLICY tenant_isolation ... USING/WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant',true),'')::uuid OR current_setting('app.bypass_rls',true)='on')`. Verify: `SELECT relforcerowsecurity FROM pg_class WHERE relname='<t>'` → `t`.
- **UUIDv7 PKs**, `ValueGeneratedNever`; enums stored as strings (`HasConversion<string>`); money `decimal`; time `DateTimeOffset` from injected `IClock` (never `DateTime.Now`).
- **After adding EF columns**, run `dotnet ef database update` (Development env) against the dev DB **before** integration tests, or they fail on model↔schema drift.
- **Build discipline:** the WebApi locks its DLLs while running — **stop the running API before `dotnet build`/`dotnet ef`**, then restart it.
- Audit `audit.*` rows are RLS-hidden in psql unless you `SELECT set_config('app.bypass_rls','on',false);` first.

## 6. Dev environment setup (Windows)
- **.NET 9 SDK** (user-local): invoke as `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe"`.
- **Node.js** at `C:\Program Files\nodejs`; run Angular CLI via `node node_modules/@angular/cli/bin/ng.js …`.
- **PostgreSQL 17** at `C:\Program Files\PostgreSQL\17\bin`. Dev DB `ntqams`, role `qams_app` / `dev-only-local` (owner in dev).
- **Secrets** (not in git — F-17): provision per `deploy/DEV-SECRETS.md` (user-secrets id `nt-qams-webapi`): `ConnectionStrings:Postgres`, `Jwt:Secret`, `PlatformAdmin:Email/Password`.
- **Dev logins (dev-only):** tenant `demo-lab` → `admin@demo-lab.local` / `Demo-Admin-Pass-2!`; platform admin → `platform-admin@localhost` / `Dev-Only-Platform-Pass-1!`.

### Run / test commands
```bash
# API (Development) — hold this running for the app / e2e
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 Database__MigrateOnStartup=true \
  "$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" run --project src/NT.QAMS.WebApi --no-launch-profile --no-build
# backend build (stop API first) + full test suite (needs a real PG for integration tests)
"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" build src/NT.QAMS.WebApi -c Debug
QMS_ITEST_POSTGRES="Host=localhost;Database=ntqams;Username=qams_app;Password=dev-only-local" \
  "$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test
# EF migration + apply
"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" ef migrations add <Name> --project src/NT.QAMS.Infrastructure --startup-project src/NT.QAMS.WebApi
"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" ef database update --project src/NT.QAMS.Infrastructure --startup-project src/NT.QAMS.WebApi
# frontend
cd frontend && npm ci
node node_modules/@angular/cli/bin/ng.js build --configuration production
CHROME_BIN="/c/Program Files (x86)/Google/Chrome/Application/chrome.exe" \
  node node_modules/@angular/cli/bin/ng.js test --watch=false --browsers=ChromeHeadless   # unit
node node_modules/@playwright/test/cli.js test    # e2e (API must be running)
```

## 7. Where things are
- **Architecture (law):** `docs/reference/NT_QAMS_Domain_Model.md`, `NT_QAMS_Database_Architecture.md`, `NT_QAMS_Application_Architecture.md`, `NT_QAMS_Product_Inventory.md`.
- **Audits & plans:** `docs/reference/NT_QMS_Enterprise_Architecture_Compliance_Audit.html`, `..._Remediation_Plan.html`, `NT_QMS_Validation_Audit_Report.html`, `NT_QMS_Final_Compliance_Validation_Audit_Report.html`, `NT_QMS_Remediation_Implementation_Plan.html`.
- **CSV/validation set:** `docs/validation/` (VMP, URS, FRA, IQ/OQ/PQ, RTM, VSR).
- **Ops:** `deploy/` (BACKUP-RESTORE-DR.md, DEV-SECRETS.md, harden-runtime-role.sql, backup.sh/restore.sh).
- **Progress log:** `IMPLEMENTATION_LOG.md`.

## 8. First run on a fresh clone
```bash
git clone https://github.com/arfaawadnt/NTQMS.git NT.QAMS && cd NT.QAMS
git checkout master            # or a specific tag / restore-point-YYYYMMDD
dotnet restore NT.QAMS.sln && (cd frontend && npm ci)
# create empty PostgreSQL 17 db 'ntqams' owned by qams_app, then provision secrets (DEV-SECRETS.md)
dotnet ef database update --project src/NT.QAMS.Infrastructure --startup-project src/NT.QAMS.WebApi
# (optional) restore the dev dataset from the backup dump: pg_restore … ntqms-db-restorepoint-*.dump
```
Then run the API + frontend (§6) and continue with the Phase-0 plan (§4).
