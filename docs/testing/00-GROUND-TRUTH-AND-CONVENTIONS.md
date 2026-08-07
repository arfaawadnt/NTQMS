# NT.QMS Test Suite — Ground Truth and Authoring Conventions

**Binding for every author of this test package.** Read this file before writing a single test case.
Version of the system under test: **v1.51.2** (repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master`).
Inspection date: 2026-08-01.

> **Amended 2026-08-01 (commit `1aa3803` → this commit).** Five as-built claims were re-measured
> against the live build and corrected: the `audit.security_event` RLS gap (closed in v1.51.2, and the
> original text told authors to write a failing test for it), the endpoint-gating mechanism, the role
> enum's remaining purpose, the requirement-ID ceiling, and the test baseline. Corrections are marked
> **[corrected 2026-08-01]** in place. Everything else is as originally authored.

---

## 1. VERIFIED as-built stack (measured from the repository, not assumed)

| Concern | Commissioning brief said | **Actually implemented (verified)** |
|---|---|---|
| API | ASP.NET Core 9 Web API | **Confirmed** — .NET 9, `src/NT.QAMS.WebApi` |
| CQRS | MediatR | **Confirmed** — MediatR `12.4.*` (pinned; v13 changes licence + `next()` signature) |
| Validation | FluentValidation | **Confirmed** — `FluentValidation.DependencyInjectionExtensions 11.11.*`, 46 files |
| Mapping | Mapster | **NOT PRESENT** — hand-written mapping/projection in slices |
| ORM | EF Core 9 | **Confirmed** — `Npgsql.EntityFrameworkCore.PostgreSQL 9.0.*`, `EFCore.NamingConventions` |
| Reporting queries | Dapper | **NOT PRESENT** — no Dapper package, no `.Query<` raw SQL layer |
| Database | PostgreSQL 17 + RLS | **Confirmed** — 97 tables / 5 schemas, 90 FORCE-RLS policies, 56–57 migrations |
| Cache | Redis distributed cache | **NOT PRESENT** — no Redis, no `IDistributedCache`, no `IMemoryCache` |
| Background jobs | Hangfire | **NOT PRESENT** — replaced by `BackgroundService` hosted services (`ScheduledSweepService`, `OutboxProcessor`, `DeferredStartupSeeder`, `KpiSnapshotService`) |
| Realtime | SignalR | **NOT PRESENT** |
| Logging | Serilog + Seq | **NOT PRESENT** — `Microsoft.Extensions.Logging` (`ILogger<T>`, 17 files, source-generated log messages) |
| Tracing/metrics | OpenTelemetry | **Confirmed** — OTLP exporter, Prometheus exporter, ASP.NET Core + Npgsql instrumentation (v1.17.0) |
| Frontend | Angular 18 standalone | **Angular 22.0.8** standalone (upgraded 18→22 at v1.49.0); TypeScript 6.0.3; zone.js 0.15.1 |
| CSS | Tailwind CSS | **NOT PRESENT** — hand-authored CSS design tokens (`--nt-*`) |
| i18n | ngx-translate | **NOT PRESENT** — custom `core/i18n.service.ts` (1,518 lines, in-code dictionaries) for `en` / `ar` / `fr` |
| Docs/export | — | `ClosedXML 0.105.0` (XLSX exports), `QuestPDF 2026.7.1` (PDF) |
| API versioning | — | `Asp.Versioning.Mvc 8.1.0` — every route is dual-exposed as `/api/...` and `/api/v{version}/...` |

**Authoring rule:** any test that would exercise Hangfire, Redis, Dapper, Mapster, SignalR, Serilog/Seq, Tailwind or ngx-translate MUST NOT be written as an executable test case. Raise a **Gap** entry instead (architecture-vs-brief divergence) and, where the capability exists under a different mechanism, write the test against the **implemented** mechanism and label it `Implementation-derived`.

---

## 2. VERIFIED functional facts (cite these; do not re-invent)

### Identity & access
- **[corrected 2026-08-01]** The enum `UserRole` — `PlatformAdmin=0, TenantAdmin=1, QualityManager=2, DepartmentHead=3, Analyst=4, ExternalAuditor=5` (`src/NT.QAMS.Domain/IdentityAccess/UserAccount.cs:10`) — still exists, but since v1.51.0 it is the **platform/tenant structural tier, not the authorization mechanism**. Authorization comes from tenant-defined roles over the permission catalogue (see *Authorization / privileges* below). Do not write tests that assume a fixed role grants a fixed capability.
- `UserAccount.MaxFailedAttempts = 5`, `UserAccount.LockoutMinutes = 30` (same file, lines 29–30).
- `UserAccount` is `IOptionallyTenantScoped` — deliberately **outside RLS** (accepted deviation B9, guarded by `UserAccountTenantBoundTests`). Platform admins have no tenant.
- **MFA is per-tenant OPTIONAL, default OFF** — `TenantSettings.RequireMfaForPrivilegedRoles` (column `require_mfa_privileged`, default `false`), platform-admin fallback config `Security:RequireMfaForPrivilegedRoles` (default `false`). TOTP RFC 6238. The brief's claim "all active accounts require MFA" is **NOT the implemented behaviour** → Gap.
- Sessions: **ADR-0009** — access JWT is SPA-memory-only (15 min default); rotating httpOnly/Secure/SameSite=Strict cookie `qams_rt` on `Path=/api/auth`; SHA-256-only storage in `qams.refresh_session`; refresh rotates with **reuse-detection → family revocation**. `ActiveSessionMiddleware` re-checks the account on every authenticated request (inactive → `401 AUTH-006`; token role ≠ DB role → `401 AUTH-007`).
- Password policy: one shared `PasswordRules.StrongPassword()` — ≥12 chars, upper+lower+digit+symbol, offline breached-password blocklist (`src/NT.QAMS.Application/IdentityAccess/PasswordRules.cs`).
- Anonymous workspace lookup `GET /api/auth/workspace/{slug}` returns the lab **name only**, and answers 404 identically for unknown/malformed/inactive slugs (anti-enumeration).

### Electronic signature — **password + PIN**, not "4-digit PIN" alone
`ESignatureService` (`src/NT.QAMS.Infrastructure/Compliance/ComplianceLedgerServices.cs:87+`) requires **both** the account password and a separate signature PIN, each verified against a hash (`IPasswordHasher.Verify`):
- `SIG-001` PIN not set or incorrect · `SIG-002` password incorrect · `SIG-003` account temporarily locked after repeated failed signings · `SIG-010`, `SIG-011`, `SIG-404` also in use.
- Failed signings are logged as `ESIGN_FAILED` via `ISecurityEventLog` and throttled through `UserAccount.RegisterFailedLogin` (same 5-attempt / 30-minute lockout as login).
- Signature record fields (`SignatureRecord`, `src/NT.QAMS.Domain/ComplianceLedger/LedgerEntries.cs:29+`): `Id, TenantId, SignerId, SignerDisplay, Meaning, SubjectRef, ContentHash, SignedAtUtc`.
- **No digit-length constraint on the PIN was found in the domain.** Any "4-digit PIN" boundary test is `Gap-dependent` until a length rule is located or added.

### Authorization / privileges
- Privileges are **code-defined**, key format `{module}.{action}` (lower-case), built by `PermissionCatalog.Key()` (`src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:194`).
- **30 permission modules** in 7 groups: `nc, complaints, feedback, audits, objectives, changes, reviews` (Quality); `documents, quality-policy, records` (Documents); `risks, compliance, conflicts, org-context, access-reviews` (Risk); `equipment, reference-standards, monitoring-points, suppliers` (Resources); `competencies, training, test-authorizations, users` (People); `analytical-quality, proficiency-testing` (Analytical); `tasks, notifications, reports` (Operations); `organization, tenant-settings, roles` (Administration).
- `PermissionAction` values seen in use: `View, Create, Edit, Approve, Void, Sign, Export, Manage`.
- Action bundles: `FullRecordLifecycle` = View/Create/Edit/Approve/Void/Export; `SignedRecordLifecycle` = + Sign; `ReadOnlyModule` = View/Export; `ConfigurationModule` = View/Manage.
- **The brief's privilege codes (`USER.CREATE`, `DOC.APPROVE`, `NCR.TRIAGE`, `EQUIP.CALIB_SCHED`, …) DO NOT EXIST** in this build. Map each to its real `{module}.{action}` equivalent in the Role & Permission Matrix and record the naming divergence once as a Gap — do not write tests against the fictional codes.
- **[corrected 2026-08-01]** Endpoint gating is `[RequirePermission(module, action)]` — **144 call sites** across the controllers. v1.51.0 converted the former `[Authorize(Roles=…)]` gates; exactly **one** remains, guarding the platform (non-tenant) surface, and `NT.QAMS.WebApi.Authorization.Roles` now governs that surface only. Command-level gating is `[RequirePermissionPolicy]`, with `RequireInternalActor` retained as tier defence-in-depth. Any new write command still needs a policy attribute or `CommandPolicyTests` fails CI.
- Data-scoped access: `qams.role`, `qams.role_permission`, `qams.user_branch_access`, `qams.user_department_access`, `user_account.role_id` (v1.51.0 Role Privilege module).

### Tenancy & RLS
- `TenantConnectionInterceptor` sets `app.current_tenant` and `app.bypass_rls` via `set_config` on **every connection open**, fail-closed to nil.
- 90/90 FORCE-RLS policies; policy shape `USING/WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant',true),'')::uuid OR current_setting('app.bypass_rls',true)='on')`.
- `audit.*` schema policies are deliberately **relaxed on WRITE**: `WITH CHECK (tenant_id IS NULL OR tenant_id = GUC OR bypass)` — audit ledgers must accept null-tenant appends (a failed pre-auth login writes a null-tenant `field_change` row). `qams.*` stays strict.
- Elevation is explicit: `ICurrentTenant.IsElevated` / `ICurrentTenantSetter.Elevate()`, used by exactly the trusted cross-tenant paths (`ProvisionTenant`, `OutboxProcessor`, `ScheduledSweepService`, `KpiSnapshotService`, LOV backfill).
- **Accepted deviation B9:** `user_account` and `outbox_event` stay outside RLS (nullable tenant).
- **[corrected 2026-08-01] CLOSED — do not author a failing-condition test.** `audit.security_event` formerly had the append-only trigger but **no RLS policy** (both earlier RLS migrations iterated `pg_policies` and so skipped it), and its store reads were not tenant-filtered. Both halves were fixed in v1.51.2 by `Hardening2_RlsGapClosure`: measured now `rls=true force=true policy=tenant_isolation`, and `ComplianceLedgerStore.GetSecurityEventsAsync` filters by tenant like its siblings. Author **positive** isolation cases against it (see `SecurityEventRlsTests`, and OQ-DB-01 in `docs/validation/13-OQ-Execution-Record-SchemaHardening-v1.51.2.md`). The **accepted, permanent** RLS exceptions are `user_account` and `outbox_event` only — those are deviation B9, not defects.
- Schema hardening (v1.51.x): tenant-first composite PKs `(tenant_id, id)` on 88 tables, **no `UNIQUE(id)`**; 36 FKs (29 tenant-composite CASCADE, 5 DEFERRABLE INITIALLY DEFERRED to `saas.tenant`, 2 to `user_account`); 85 CHECK constraints; **`xmin` concurrency token (there is no `row_version` column)**.
- New `ITenantScoped` tables need RLS added in their **own** migration — EF will not generate it.

### Signed-record immutability & audit trail
- `qams.reject_frozen_mutation()` BEFORE UPDATE/DELETE trigger on the 12 analytical study roots (`state='SignedOff'`) + `uncertainty_budget` (`status='Approved'`) — allows the transition **into** signed, blocks mutation afterwards.
- Audit ledger is a **hash chain**: `AuditLedgerEntry` = `Id, TenantId, Sequence, EventId, EventType, Payload, OccurredAtUtc, PrevHash, EntryHash`. `GET /api/compliance/chain-verification` verifies it. Chain hashes are computed over **DB-read (microsecond-truncated) timestamps**.
- `FieldChangeRecord` = `Id, TenantId?, EntityType, EntityId, Action, Property, OldValue, NewValue, ActorId?, Actor, Reason?, OccurredAtUtc`.
- **Reason for change:** `ChangeReasonMiddleware` refuses any **DELETE** lacking header `X-Change-Reason` → `400 CHANGE-REASON-REQUIRED`; the scoped reason is stamped onto every ledger row in the same transaction by `FieldChangeInterceptor`. Frontend `changeReasonInterceptor` collects it via an accessible dialog. **Place-legal-hold sends its Part-11 reason in the POST body**, not the header.
- 19,296 legacy null-tenant `field_change` rows are retained by design (append-only).
- In `psql`, audit rows are RLS-hidden unless you `set_config('app.bypass_rls','on',false)` or set the tenant GUC.

### Segregation of duties (implemented codes)
- `AggregateRoot.EnsureSignerIsNotPreparer(signerId, code)` — **no-op when the preparer is unknown** (legacy/system records; accepted residual F-05b). Guards all 14 analytical sign-offs/approvals with `SOD-AQ-001`.
- `Nonconformance.Verify(passed, actorId)` throws `SOD-CAPA-002` when actor == RaisedBy.
- `QualityPolicy.Approve` guarded by `SOD-QP-001`.
- `CompetencyRecord`: `PassMark = 80` (`src/NT.QAMS.Domain/Competency/CompetencyRecord.cs:33`); requires an assessor who is not the trainee.
- SoD violations surface as **`AUTHZ-*` → HTTP 403** with `application/problem+json` (`ProblemAuthorizationResultHandler`). Domain rule breaches surface as **HTTP 422** with the domain code (e.g. `SOD-QP-001`). The brief's `SOD_VIOLATION` code does not exist → map to the real codes.

### Risk / NC scoring
- `Nonconformance.Rpn = severity * likelihood` (`src/NT.QAMS.Domain/Improvement/Nonconformance.cs:140`); `NcRaised` event carries `Severity` and `Rpn`.
- `RiskItem.Rpn = likelihood * impact`; `ResidualRpn = likelihood * impact`; **RPN > 12 raises `HighResidualRisk`**; a defaulted RPN of 9 is explicitly banned (`src/NT.QAMS.Domain/RiskGovernance/RiskItem.cs`).
- `QualityEventType` enum on `Nonconformance`: `Nonconformity | Deviation | OutOfSpecification | OutOfTrend` (default `Nonconformity`).

### QC / Westgard — **five rules, not six**
`src/NT.QAMS.Domain/AnalyticalQuality/WestgardEvaluator.cs`, a pure function:
- Rejection rules **1-3s, 2-2s, R-4s, 10-x**; warning rule **1-2s**. **`4-1s` IS NOT IMPLEMENTED** → Gap, do not write a `4-1s` execution case.
- Limits are configuration-controlled: `WestgardLimits(WarningSd=2, RejectSd=3, RangeSd=4, RunLength=10)`, bound from `AnalyticalQuality:Westgard:*`, `.Validated()` at startup — `QC-LIM-001` non-positive SD, `QC-LIM-002` warning ≥ reject, `QC-LIM-003` run length < 2. Rule **labels are derived from the limits**.
- `Evaluate` throws `QC-SD` when `sd <= 0`.
- The verdict is **frozen on the run** — never recomputed, because the window statistics move as later runs arrive.
- `QcProfile.UpdateTargets(mean, sd, reason, effectiveFrom)` requires a reason (`QC-012`) and is forward-only (`QC-013`).
- **No class named `LeveyJennings` exists** — L-J is a frontend chart rendering over the QC series. Test it as a UI concern.

### CAPA escalation — **SLA-driven, not fixed T+24/48/72**
- `SlaDefinition(Module, Severity, TargetHours)` — `SLA-001` module/severity required, `SLA-002` target hours must be positive. `ScheduledSweepService` is a `BackgroundService` on a **1-hour interval** with a 15-second startup delay; it proposes calibration-due, grace-lockout, competency-expiry and supplier-suspension transitions across all tenants and is **idempotent by construction** (a declined proposal is a no-op).
- The brief's fixed **T+24 → Action Owner, T+48 → Department Head, T+72 → Quality Manager** ladder is **not** what is implemented. Author the escalation tests against `SlaDefinition` + `EscalationTriggeredPolicy` + `NotificationPolicies`, and raise a Gap for the fixed-ladder requirement.

### Other confirmed facts
- **652 routes** in the approved API-surface baseline `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` (386 POST / 216 GET / 26 DELETE / 24 PUT), including the `/api/v{version}/…` mirror — i.e. **~326 logical endpoints**. Any API-surface change must update that file in the same commit or the merge gate fails.
- Change-request route is **`/api/changes`** (not `/api/change-requests`).
- 41 controllers; 86 `path:` entries in `frontend/src/app/app.routes.ts`.
- Frontend tests: **15 `.spec.ts` files** (Jasmine/Karma) + **3 Playwright e2e specs** (`auth`, `regulated-workflow`, `a11y` with `@axe-core/playwright`).
- Backend test projects: Domain.UnitTests (43 files), Application.UnitTests (23), WebApi.FunctionalTests (31), IntegrationTests (15), Architecture.Tests (10), LoadTests (4, **outside the solution** — run with `dotnet run`). **[corrected 2026-08-01]** Green baseline **446 backend tests** (228 domain / 72 application / 33 architecture / 31 integration +1 skipped / 82 functional), plus 76 frontend unit and 6 Playwright e2e. Per-run history: `docs/validation/verification-log.md`.
- CI jobs (`.github/workflows/ci.yml`): `build-test`, `frontend`, `container`. Gates include .NET SCA, npm SCA against `.github/npm-audit-allowlist.txt` (currently empty), Trivy image scan, non-root container assertion, module-boundary + API-surface-snapshot merge gates, axe a11y.
- No PDF **watermark** implementation ("OBSOLETE - UNCONTROLLED") was found anywhere in `src` → Gap.

---

## 3. Environment for execution

- .NET 9 SDK is **user-local**: prefix every command with
  `$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"; $env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"`.
- Node 20.18.1 user-local at `%LOCALAPPDATA%\nodejs-portable\node-v20.18.1-win-x64` — **CI must use Node 24** (npm 10 falsely rejects the npm-11 lockfile).
- PostgreSQL 17 at `C:\Program Files\PostgreSQL\17\bin`; dev DB `ntqams`, app role `qams_app` / `dev-only-local` (**owner** in dev — a Production start legitimately refuses, which is what OQ-DEP-01 proves).
- Integration tests: env `QMS_ITEST_POSTGRES`, run inside rollback transactions, `SkippableFact` when no PostgreSQL. **No Docker on the dev machine** — Testcontainers is unavailable; a local/CI PostgreSQL service is the supported path.
- Dev logins: tenant `admin@demo-lab.local` / `Demo-Admin-Pass-2!` at `localhost:4200/t/demo-lab`; platform `platform-admin@localhost` / `Dev-Only-Platform-Pass-1!`. API on `:5080`.
- Dev servers must be started via `scripts/dev-up.ps1` (detached) or they die with the agent session. `ng build` does **not** refresh a running `ng serve`.
- Known env traps: PowerShell 5.1 mangles non-ASCII in `.ps1` (keep scripts ASCII-only) and drops manual `Cookie` headers (use `curl.exe` with `--data "@file"`); `urllib` GETs can spuriously 401 (verify with `curl`); run credential-burst probes **last** (they poison the 10/min auth rate-limit partition).

---

## 4. Mandatory test-case format

Every detailed case is a row or block carrying **all** of these fields. Never abbreviate the set.

`Test Case ID · Title · Module · Requirement ID · Risk ID · Test Level · Test Type · Technique · Priority · Severity if Failed · Automation Candidate · User Role · Required Permission · Tenant Context · Preconditions · Test Data · Environment · Steps · Expected UI Result · Expected API Result · Expected Database Result · Expected Audit Result · Expected Notification · Cleanup · Evidence · Result · Defect ID · Notes`

- `Result` is always `Not Run` in this package — **we are authoring, not executing.** Never record a Pass.
- Steps are numbered and use exact inputs, exact status codes, exact domain codes. Banned: "verify it works", "test the feature", "check the result".
- Where a field is genuinely not applicable, write `n/a — <one-clause reason>`, never blank.

### Evidence label (mandatory, one per case)
- `[IV]` **Requirement-based and implementation-verified** — the behaviour was read in the cited source file.
- `[RNV]` **Requirement-based, implementation not verified** — required by URS/brief, not located in code within this pass.
- `[ID]` **Implementation-derived** — behaviour found in code with no matching requirement.
- `[GD]` **Gap-dependent** — cannot execute until the referenced Gap is resolved.

Cite the source as `file_path:line` whenever you claim `[IV]` or `[ID]`.

---

## 5. Test-case ID convention (stable, never renumber)

`TC-<MODULE>-<KIND>-<NNN>` — three digits, zero-padded, module-local sequence starting at 001.

**MODULE codes (fixed):**
`AUTH` `RBAC` `TENANT` `DOC` `NCR` `CAPA` `RISK` `AUDIT` `EQUIP` `COMP` `QC` `MV` `PT` `SUP` `LEDGER` `REC` `ORG` `NOTIF` `RPT` `LOC` `FE` `DB` `API` `NFR` `COMPLIANCE`

**KIND codes (fixed):**
`UNIT` `COMP` (component) `INT` (integration) `API` `E2E` `UAT` `SEC` `PERF` `RLS` `STATE` `BVA` `EP` `DT` (decision table) `MCDC` `PATH` `LOOP` `DF` (data flow) `ESC` (escalation) `WESTGARD` `STAT` `RTL` `A11Y` `OBS` `DR` `EXPL` (exploratory charter) `MUT` (mutation)

Examples: `TC-AUTH-API-001`, `TC-TENANT-RLS-014`, `TC-QC-WESTGARD-007`, `TC-COMP-BVA-003`.

**Gap IDs:** `GAP-<MODULE>-<NNN>`. **Risk IDs:** reuse `docs/validation/02-Functional-Risk-Assessment.md` IDs where they exist; otherwise mint `RSK-<MODULE>-<NNN>` and say so.

**Requirement IDs [corrected 2026-08-07]:** the baseline set is `URS-001`…`URS-055` in `docs/validation/01-User-Requirements-Specification.md`. Everything after the 1.0 baseline — `URS-056`…`URS-128` — is defined in `docs/validation/06-Revalidation-Delta-v1.38-v1.50.md` **Part A**, which is their single source of truth (A.9 role privileges `URS-095`…`099`; A.10 schema hardening `URS-100`…`107`; A.11/A.12 Quality Analytics + usability `URS-108`…`122`; A.13–A.19 the RISK-03 Part 11 e-signature ceremony `URS-123`…`128`). The next free id is `URS-129`. Trace to those first. Where no URS requirement covers the behaviour, trace to the source file and open a Gap for the missing requirement.

---

## 6. Honesty rules (non-negotiable)

1. **Never** claim a behaviour is implemented without having read it. If you did not open the file, the label is `[RNV]`.
2. **Never** invent a module, endpoint, column, permission key, error code, or rule that you did not observe.
3. **Never** silently reconcile a contradiction between the commissioning brief and the code — record a Gap with both readings.
4. **Never** mark a compliance control "compliant". The only permitted verdicts are **Conforms / Partially conforms / Does not conform / Cannot be assessed**.
5. **Never** record an execution result. This package is authored, not executed.
6. Report coverage honestly: if you capped a list, say what you dropped and why.

---

## 7. File-naming for deliverables

Write to `docs/testing/`. One file per part, numbered so the package reads in order:

`NN-<slug>.md` — e.g. `10-module-auth.md`, `24-traceability-matrix.md`.

Start every file with a heading, the module code, the ID range you consumed, and a one-line statement of what is complete vs deferred.

**Split convention [added 2026-08-01].** A module package is split so that no single authoring pass has to emit more output than it can complete:

- `NN-module-<slug>.md` — **front matter only**: implementation inventory, divergences, state-transition matrices, decision tables, UAT scenarios, exploratory charters, gap register. **No detailed cases.**
- `NN-module-<slug>-cases-<A|B|C…>.md` — **detailed cases only**, one batch per file, each batch owning a disjoint slice of scope and a reserved ID block.

The front-matter file's ID-range table is a *reservation*; the case files are what actually consume the ids. A reservation with no matching case file is a coverage hole, not a delivered case.

---

## 8. Canonical detailed-case block [added 2026-08-01]

All 28 mandatory fields are present, paired onto rows so a batch of ~25 cases fits one authoring pass. **Use this exact shape** — the HTML renderer keys off it.

```markdown
#### TC-AUTH-API-014 — Login rejected on the 6th consecutive failure  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003 · RSK-AUTH-002 |
| **Level / Type / Technique** | API · Functional (negative) · BVA — at the lockout threshold |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `user_account.failed_attempts = 5`, `locked_until_utc` set, per `UserAccount.cs:29-30` |
| **Test Data** | `admin@demo-lab.local` / `WrongPass-9!` |
| **Steps** | 1. `POST /api/auth/login` with the above. 2. Read status + body. 3. `SELECT failed_attempts, locked_until_utc FROM qams.user_account WHERE email=…`. |
| **Expected UI** | Sign-in form shows the locked message; inputs remain enabled. |
| **Expected API** | `401` `application/problem+json`, code `AUTH-00x` — assert the exact code from the handler. |
| **Expected DB** | `failed_attempts` unchanged at 5; `locked_until_utc` unchanged (no lock extension on an already-locked account). |
| **Expected Audit** | One `audit.security_event` row, type `LOGIN_FAILED`, `tenant_id` null-tolerant per the relaxed audit WITH CHECK. |
| **Expected Notification** | n/a — no notification is defined for a failed login. |
| **Cleanup** | `UPDATE qams.user_account SET failed_attempts=0, locked_until_utc=NULL WHERE email=…` |
| **Evidence** | HTTP response capture · SQL result · security-event row |
| **Result / Defect** | Not Run · — |
| **Notes** | Verify the code string against the handler before execution; do not assume. |
```

Rules for the block:
- The `####` line carries **id — title — evidence label**. Nothing else.
- `Result / Defect` is always `Not Run · —`.
- Any field genuinely inapplicable reads `n/a — <one-clause reason>`.
- Cite `file:line` inside the row that makes the claim, not in a footnote.
- Steps are numbered inside the single `Steps` cell, separated by `. ` — keep them exact and executable.
