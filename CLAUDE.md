# CLAUDE.md — NT.QMS operating guide (READ FIRST)

This file is the single source of truth for how to continue developing NT.QMS. It is
auto-loaded by Claude Code. If you are continuing this project on a new machine or
account: read this file, then `README.md`, then `docs/reference/` — and follow the
standing rules below exactly. They override default behaviour.

---

## 1. What this is
NT.QMS is a **multi-tenant SaaS Quality Management System** for ISO 17025 / 15189 /
9001 / 21 CFR Part 11 / GMP laboratories.
**Stack:** .NET 9 · PostgreSQL 17 (Npgsql, snake_case) · Angular 22 · Clean Architecture + CQRS.

> **Before building anything, load the reference skills in `.claude/skills/`** — they carry the
> as-built detail this file only summarises: `ntqms-architecture` (end-to-end feature playbook),
> `ntqms-database` (schema/migration law), `ntqms-frontend` (Angular conventions),
> `ntqms-compliance` (what a change obliges you to document and prove).
**Solution layout:** `src/NT.QAMS.{Domain, Application, Infrastructure, WebApi, Contracts, SharedKernel}` + `tests/NT.QAMS.{Domain.UnitTests, Application.UnitTests, Architecture.Tests, IntegrationTests, WebApi.FunctionalTests}` + `frontend/` (Angular).

## 2. Standing rules (the "law" — never break these)
1. **Do NOT redesign the domain, database, or public APIs.** The architecture docs in
   `docs/reference/` are law — extend within them, don't re-architect.
2. **No magic strings / magic numbers.** Permissions come from `PermissionCatalog` constants
   (never a literal key string); error codes are structured (`NC-001`, `SOD-AQ-001`, …); config
   is typed/centralized. `WebApi/Authorization/Roles.cs` now governs the **platform** surface
   only — see rule 9.
3. **No dead code, no TODOs, no mocks, no fake/placeholder screens** in production paths.
4. **XML doc comments** on public domain/application types; **strict TypeScript, no `any`**.
5. **Report gaps honestly.** Never claim compiled/tests-pass/migrations-applied unless you
   actually executed them. Don't inflate; surface risks.
6. **Domain protects itself:** private setters, factories, guarded state machines,
   invariants inside the aggregate (not in validators/handlers). DTOs in `Contracts`,
   never expose entities.
7. **Multi-tenancy is sacred:** every `ITenantScoped` table is protected by BOTH the EF
   global query filter AND PostgreSQL FORCE RLS. See rule in §5.
8. **Commit / branch discipline (updated 2026-08-03):** **work on `dev` and push EVERYTHING to
   `origin/dev` ONLY. NEVER push to `master` until the user manually and explicitly says "push to
   master."** New work → `dev` → dev-team review → merge to `master`. A plain "push" means push to
   `dev`. Commit/push only when asked; **stage by explicit path, never `git add -A`** (a blanket add
   once swept an unrelated file into a commit); end commit messages with
   `Co-Authored-By: Claude <noreply@anthropic.com>` (use the current model).
9. **Authorization is the permission catalogue, not the role enum** (v1.51.0). Endpoints use
   `[RequirePermission(module, action)]`; commands use `[RequirePermissionPolicy]`. The
   `UserRole` enum still exists but is the platform/tenant *structural tier*, not the
   authorization mechanism. Do not add new `[Authorize(Roles=…)]` gates to tenant endpoints.
10. **Production change discipline (standing rule, 2026-08-03):** this is a **production application** —
    make **ONLY the exact change requested, nothing more.** Do **not** alter the architecture (it must
    fit inside the existing Clean Architecture / CQRS / DDD / RLS structure, never reshape it) and do
    **not** make any unrequested edits to any page, component, or file (no drive-by refactors, cleanups,
    reformatting, renames, or fixing unrelated stale items). If a request appears to require an
    architectural change or a wider edit, **stop and surface it for the user's decision** instead of
    proceeding. Keep every diff minimal and surgical.

## 3. Current state (as of 2026-08-07)
- Code at **`v1.53.0`** — latest: **v1.53.0 (tagged 2026-08-07) — RISK-03 21 CFR Part 11 e-signature
  ceremony** extended from document-publish to **every** signed-record gate (NC verify/close, all 14
  analytical-quality sign-offs incl. PtPlan, audit sign-off, quality-policy & change approval,
  management-review close, the 4 borderline SoD gates, and both periodic-review completions); 4 new
  `.sign` catalogue keys (`proficiency-testing`/`suppliers`/`conflicts`/`compliance`); self-fetching
  signature manifest on every gate; backend **478** + frontend **95** green; live-PostgreSQL proofs.
  **Admin upgrade action for existing tenants:** grant the new `.sign` keys —
  `deploy/RELEASE-NOTE-RISK-03-SIGNING-KEYS.md` (detail in `docs/validation/06-…` §A.13–A.19,
  URS-123–128). Release posture unchanged — Pre-production; DOC-001 + SEC-001 remain open.
  Prior history — EA remediation COMPLETE (Phases 0–6, closed at v1.44); **Role Privilege
  module** (v1.51.0, dynamic tenant-defined roles over a 170-key permission catalogue, branch/
  department as a hard data filter); **schema hardening** (v1.51.2, six `Hardening*` migrations);
  **Quality Analytics** (v1.52.0 — one computation serving a Quality Statistics dashboard and an
  ISO 17025 §8.9.2 Management Review pack; nine sub-systems, tenant-configurable weighted Quality
  Health Score gated by `reports.manage`; server-side privilege/branch scoping; URS-108…114) plus
  a **usability / self-service set** (v1.52.0 — My-Tasks role resolution, grid-overlap fix,
  route↔help parity, management-review agenda/link/participants + dispatch, self-service and
  admin-issued e-signature PINs, self-service password change, tabbed equipment workspace with
  maintenance certificates; URS-115…122). Production blockers cleared at v1.41.
  Repo: `github.com/arfaawadnt/NTQMS`.
  Closure report: `docs/reference/NT_QMS_EA_Remediation_Closure_Report.md`; hardening record:
  `SCHEMA-HARDENING-REPORT.md`; as-built schema: `docs/reference/NT_QMS_Database_AsBuilt.md`.
- **Schema posture (v1.52.0):** 91 tenant-first composite PKs · 93 FORCE-RLS tables · 87 CHECK
  constraints · 59 migrations · deferrable tenant-composite FKs. (v1.52.0 added the three
  tenant-scoped tables `quality_health_profile`, `quality_health_weight`, `review_participant` —
  each composite-PK + FORCE-RLS — and two CHECK domains on `quality_health_weight`.) Two accepted
  permanent deviations (B9, B10) — see the report §8.
- Tests green at last run (2026-08-01, v1.52.0): **460 backend** (242 domain / 72 app / 33 arch /
  31 +1 skip integration / 82 functional) + **87 frontend unit** + **6 Playwright e2e**. Per-run
  history: `docs/validation/verification-log.md`.
- **Auth model (ADR-0009, supersedes ADR-0003):** access JWT in SPA memory (15-min default); rotating httpOnly `Secure SameSite=Strict` refresh cookie `qams_rt` (Path=/api/auth) with reuse-detection family revocation; `POST /api/auth/refresh` + `/logout`; silent refresh on 401 and at SPA bootstrap.
- **All 18 CSV/regulatory-audit findings are CLOSED** (release train v1.25→v1.37): tenant
  RLS, signed-record immutability, MFA, SoD, reason-for-change, session revocation,
  e-sig logging, integration tests, backup/DR, governance modules, exports, password
  policy, config externalization, secrets, frontend+e2e tests, GAMP 5 CSV doc set.
- Two audits delivered: CSV/Part-11 (all closed) and **Enterprise Architecture**, now at **88%**,
  0 critical. Current posture: `docs/reference/NT_QMS_Compliance_Status_Report_v1.51.2.html`.
- **Still open (do not report as closed):** `SEC-001` independent penetration test · `DOC-001`
  validation in a qualified environment with signatures · `OPS-001` staging observability/load ·
  OQ transcripts 12 and 13 awaiting human signature.

## 4. What to do NEXT
**The remediation train is COMPLETE** (v1.38→v1.44) and the schema hardening is delivered
(v1.51.2). New work comes from the **product backlog** — feature and page enhancements. Before
starting any of it, load the skills in `.claude/skills/` and follow `ntqms-architecture`'s
end-to-end playbook; it is the current, measured procedure, and it supersedes the phase plan below.

<details><summary>Historical — the completed EA remediation train (v1.38 → v1.44)</summary>
- **Phase 0 (P0, v1.38): ✅ SHIPPED** — role guard (Production refuses over-privileged DB role), `/health/live`+`/health/ready`, single-replica ADR-0001 + advisory-lock sentinel.
- **Phase 1 (P1, v1.39): ✅ SHIPPED** — `xmin` concurrency + 409 `CONCURRENCY-409`; outbox dead-letter/backoff/retention + `SKIP LOCKED` claim lease; sweep leader election.
- **Phase 2 (P1, v1.40): ✅ SHIPPED** — JSON logs + canonical request log; OTel traces HTTP→MediatR→EF→Outbox (traceparent persisted on outbox rows); X-Correlation-Id + ProblemDetails traceId; /metrics + alert set (deploy/OBSERVABILITY.md).
- **Phase 3 (P1/P2, v1.41): ✅ SHIPPED** — rate limiting (global + auth/e-sign partitions, 429); security headers + locked CSP; TLS-at-proxy + in-app HSTS (ADR-0002); token-storage risk acceptance + 60-min tokens (ADR-0003).
- **Phase 4 (P2, v1.42): ✅ SHIPPED** — problem+json everywhere; pagination envelope (API+SPA); file allow-list/sniffing; deny-by-default command authorization (211 annotated + CI gate); Idempotency-Key replay; api/v1 versioning (ADR-0004).
- **Phase 5 (P2, v1.43): ✅ SHIPPED** — CHECK constraints on regulated tables; ConfigGuard fail-fast config; Npgsql retry+timeout (execution-strategy-safe locks); non-root container + compose.production.yml.
- **Phase 6 (P2/P3, v1.44): ✅ SHIPPED** — module-boundary + API-surface snapshot merge gates; migration round-trip + audit-tamper tests; perf smoke; AUTHZ→403 + problem+json on framework 401/403; accessible change-reason dialog; ADR-0005…0008.

</details>

## 5. Reusable lessons / conventions (must-follow)
- **New `ITenantScoped` table ⇒ add RLS in its OWN migration** (EF won't). In `Up()` after
  CreateTable: `ALTER TABLE qams.<t> ENABLE ROW LEVEL SECURITY; ALTER TABLE ... FORCE ROW LEVEL SECURITY; DROP POLICY IF EXISTS tenant_isolation ...; CREATE POLICY tenant_isolation ... USING/WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant',true),'')::uuid OR current_setting('app.bypass_rls',true)='on')`. Verify: `SELECT relforcerowsecurity FROM pg_class WHERE relname='<t>'` → `t`.
- **UUIDv7 PKs**, `ValueGeneratedNever`; enums stored as strings (`HasConversion<string>`); money `decimal`; time `DateTimeOffset` from injected `IClock` (never `DateTime.Now`).
- **After adding EF columns**, run `dotnet ef database update` (Development env) against the dev DB **before** integration tests, or they fail on model↔schema drift.
- **Build discipline:** the WebApi locks its DLLs while running — **stop the running API before `dotnet build`/`dotnet ef`**, then restart it.
- Audit `audit.*` rows are RLS-hidden in psql unless you `SELECT set_config('app.bypass_rls','on',false);` first.
- **Index naming (schema hardening 1.4):** PostgreSQL truncates identifiers at 63 bytes and EF
  truncates client-side at 62 - silently, mid-word. Any index whose EF-default name would exceed
  62 chars MUST be pinned with `HasDatabaseName()` using the abbreviation map:
  `document_acknowledgement->doc_ack` , `document_controlled_copy->doc_copy` ,
  `notification_dispatch->notif_dispatch` , `document_version->doc_ver` ,
  `supplier_evaluation->sup_eval` , `instrument_comparability_study->icp_study` ,
  `user_department_access->user_dept_access`. Unique indexes use the `ux_` prefix.
- **Column sizing (schema hardening 1.2):** free-text columns sized >=1000 are `text` - the bound
  lives in the command validator (`MaximumLength`), not the column. Bounded codes, refs, enum
  strings and hashes keep explicit `varchar(n)` under 1000. Never drop a varchar bound without a
  matching validator rule.
- **Composite primary keys (schema hardening Phase 5):** every table whose `tenant_id` is
  NOT NULL has a **tenant-first** PK `(tenant_id, id)` - 88 of them - so the schema is
  partition-ready (PostgreSQL requires the partition key in every PK and unique index, and
  cannot convert an existing table into a partitioned one). A new tenant-scoped entity declares
  `builder.HasKey(x => new { x.TenantId, x.Id });`, and an owned child declares
  `child.HasKey("TenantId", "Id");` plus `child.WithOwner().HasForeignKey("TenantId", "<fk>")`.
  **Do not add `UNIQUE (id)`** - a unique index that omits the partition key is illegal on a
  partitioned table. The four nullable-tenant tables (`user_account`, `outbox_event`,
  `audit.security_event`, `audit.field_change`) keep single-column PKs: a key column cannot be
  null.
- **Cross-aggregate FKs are tenant-composite:** `FOREIGN KEY (fk, tenant_id) REFERENCES parent
  (id, tenant_id)`. This makes a child under another tenant's parent structurally impossible,
  which a single-column FK never did. (PostgreSQL matches an FK target by column *set*, so the
  order in the REFERENCES clause need not match the PK's.)
- **Migrations that touch FORCE-RLS tables must declare a bypass.** FORCE row-level security
  applies to the migration's own session *and to PostgreSQL's referential-integrity checks*.
  Without it, a data-backfill `UPDATE ... FROM parent` silently updates **zero rows** and a
  later `ADD CONSTRAINT ... FOREIGN KEY` fails because the RI check cannot see the parent. Put
  `SELECT set_config('app.bypass_rls', 'on', true);` (transaction-local) at the top of **both**
  `Up()` and `Down()` in any migration that backfills from, or adds an FK to, a FORCE-RLS table.
- **EF's model snapshot does not learn raw-SQL DDL.** If a migration renames or replaces a
  constraint via `migrationBuilder.Sql(...)`, EF still believes the old name, and the *next*
  scaffolded migration will emit a drop for a constraint that no longer exists. Reconcile
  generated names against `pg_constraint` before applying, and remember the `Down()` half must
  drop what `Up()` *created*, not what it replaced.

## 6. Dev environment setup (Windows)
- **.NET 9 SDK** (user-local): invoke as `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe"`.
- **Node.js** at `C:\Program Files\nodejs`; run Angular CLI via `node node_modules/@angular/cli/bin/ng.js …`.
- **PostgreSQL 17** at `C:\Program Files\PostgreSQL\17\bin`. Dev DB `ntqams`, role `qams_app` / `dev-only-local` (owner in dev).
- **Secrets** (not in git — F-17): provision per `deploy/DEV-SECRETS.md` (user-secrets id `nt-qams-webapi`): `ConnectionStrings:Postgres`, `Jwt:Secret`, `PlatformAdmin:Email/Password`.
- **Dev logins (dev-only):** tenant `demo-lab` → `admin@demo-lab.local` / `Demo-Admin-Pass-2!`; platform admin → `platform-admin@localhost` / `Dev-Only-Platform-Pass-1!`.

### Running the dev stack — USE THE SCRIPTS (added 2026-07-29)
The app "randomly stopping" was **not** an app defect. Three structural causes, now fixed:
1. Both dev servers used to be started ad-hoc as children of whatever shell launched them,
   so they died when that shell/session ended. The scripts start them **detached**.
2. The running WebApi **locks its own DLLs**, so every `dotnet build`/`test`/`ef` needs the
   API stopped first — and it stayed down whenever someone forgot to restart it.
   `dev-rebuild.ps1` does stop → build → **always restart**, even if the build fails.
3. Blanket `taskkill /IM dotnet.exe` killed unrelated tooling. `dev-down.ps1` stops only the
   process owning the port.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\dev-status.ps1   # FIRST when "app not working"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\dev-up.ps1       # start both (idempotent)
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\dev-rebuild.ps1  # after code changes (-Test, -Migrate)
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\dev-down.ps1     # stop both (-ApiOnly)
```
`dev-status.ps1` separates the three look-alike failures: port DOWN (browser shows
ERR_CONNECTION_REFUSED) · API up but readiness **503** (PostgreSQL unreachable) · both healthy
(so the problem is credentials/tenant, not the stack). Logs: `%TEMP%\ntqms-dev\`.
**Angular 22 needs Node >= 20.19** — use system Node at `C:\Program Files\nodejs` (Node 24);
the old portable Node 20.18.1 in `.claude/launch.json` could not start the upgraded SPA.

### Run / test commands (manual equivalents — prefer the scripts above)
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
Then run the API + frontend (§6). For new work, load `.claude/skills/ntqms-architecture` and
follow its playbook (§4).
