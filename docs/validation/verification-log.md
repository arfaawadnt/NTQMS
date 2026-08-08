# Verification Log — full-suite execution record

| Field | Value |
| ----- | ----- |
| Document ID | VLOG-NTQMS-001 |
| Purpose | A durable, dated record of every full test-suite execution: what ran, against what, and what it produced |
| Owner | Engineering appends; QA reads |
| Started | 2026-08-01 |

## Why this exists

Test runs were previously evidenced only by a green tick in CI and by prose in commit messages.
CI logs age out, and prose is not a record. For a system under 21 CFR Part 11 the *fact that
verification was performed* is itself evidence, so it belongs in the repository alongside the
thing it verifies.

**What this log is not.** It is not qualification. Every run below was executed on a development
workstation or in CI — neither is a qualified installation, and none of these entries closes
DOC-001. It records that the suite ran and what it said, nothing more.

## How to append an entry

Run the whole suite, then add one row. Do not record a run you did not watch finish.

```bash
QMS_ITEST_POSTGRES="Host=localhost;Database=ntqams;Username=qams_app;Password=dev-only-local" dotnet test
```

```bash
cd frontend && CHROME_BIN="/c/Program Files (x86)/Google/Chrome/Application/chrome.exe" node node_modules/@angular/cli/bin/ng.js test --watch=false --browsers=ChromeHeadless
```

```bash
cd frontend && node node_modules/@playwright/test/cli.js test
```

Record the **per-suite** numbers, not just a total — a total alone cannot be checked, which is
exactly how two wrong totals reached the commit record (see the note below the table).

---

## Executions

| Date | Commit | Env | Domain | App | Arch | Integ | Func | Backend | FE unit | E2E | Result |
| ---- | ------ | --- | -----: | --: | ---: | ----: | ---: | ------: | ------: | --: | ------ |
| 2026-08-08 | (pending commit) | dev + real PG | 254 | 102 | 33 | 33 (+1 skip) | 93 | **515** | 95 | — (not run) | All green (URS-133/134 Mail Management + HTML email: new `TenantMailSettings` tenant table (FORCE RLS + hex CHECK, round-tripped), `GET`/`PUT /api/notifications/mail-settings` gated `notifications.manage`; `HtmlEmailTemplate` branded/escaped + `EmailMessage` port + per-tenant sender identity in `NotificationDispatcher`. `TenantMailSettingsTests` +9, `HtmlEmailTemplateTests` +3, `MailSettingsRlsTests` +2 (real-PG isolation), `MailSettingsEndpointTests` +3; ApiSurface +4 reviewed. FE mail-management page + 95 Karma. Live: settings saved (PUT 204) and persisted across reload) |
| 2026-08-08 | (pending commit) | dev + real PG | 245 | 99 | 33 | 31 (+1 skip) | 90 | **498** | 95 | — (not run) | All green (URS-132 My Tasks unified action centre: live read model `GetMyActionsQuery` unioning task/NC/CAPA/NC-sign/risk/objective/review providers via IAppDbContext; owned-collection providers use owner-then-flatten so they translate on EF InMemory + PG; `GET /api/tasks/my-actions`. `MyActionsHandlerTests` +1 (InMemory, 6 categories), `MyActionsTests` +2 (real-pipeline); ApiSurface +2 reviewed. FE tasks page reworked to grouped feed + 95 Karma. Live: grouped feed renders, inline Complete/Done, SLA editor retained) |
| 2026-08-08 | (pending commit) | dev + real PG | 245 | 98 | 33 | 31 (+1 skip) | 88 | **495** | 95 | — (not run) | All green (URS-131 User Manual PDF: `ToManualPdf` QuestPDF renderer — cover coverage-by-section chart, linked TOC with page numbers (Section/SectionLink/BeginPageNumberOfSection), per-topic numbered progress bars + steps + usage; `POST /api/exports/manual.pdf` auth-only, EXPORT-003 ceiling. `ExportServiceTests` +1, `ManualExportTests` +3 (real-pipeline 200/422/401); ApiSurface +2 reviewed. FE prod build + 95 Karma. Live: button → POST 200, 78 KB PDF) |
| 2026-08-08 | (pending commit) | dev + real PG | 245 | 97 | 33 | 31 (+1 skip) | 85 | **491** | 95 | — (not run) | All green (URS-130 Quality Analytics report: branded QuestPDF renderer — score gauge, weighted-component progress bars, Pareto bars, 5×5 risk heat-matrix — + multi-sheet ClosedXML workbook; `GET /api/exports/quality-analytics.pdf\|.xlsx` gated `reports.export`. `ExportServiceTests` +4 (full + empty DTO render), `QualityAnalyticsExportTests` +3 (real-pipeline 200/401); ApiSurface +4 reviewed. FE prod build clean. Live: both GETs 200, 67 KB PDF / 17.5 KB XLSX from demo-lab) |
| 2026-08-08 | (pending commit) | dev + real PG | 245 | 93 | 33 | 31 (+1 skip) | 82 | **484** | 95 | — (not run) | All green (URS-129 NC re-open: `Nonconformance.Reopen` Closed→ActionPlan + reason + `NcReopened`; `ReopenNcCommand` signing ceremony reusing `nc.sign`; migration `AddNcReopenReason`. Domain +3, App `ReopenNcSigningTests` +3; ApiSurface +2 reviewed. Full backend suite watched finish on real PG; FE prod build + 95 Karma green) |
| 2026-08-07 | 904710d | dev + real PG | 242 | 90 | 33 | 31 (+1 skip) | 82 | **478** | 95 | 6 | All green — full suite incl. e2e, run to validate the deploy-remediation series (SET LOCAL RLS-bypass, `UseWindowsService`, harden-runtime-role/`ref`, deploy-doc role fixes). Integration round-trip + `RegulatedFlowRealDatabaseTests` on real PG green; the migration-source edit round-trips. Counts unchanged from the RISK-03 baseline (no tests added) |
| 2026-08-07 | (pending commit) | dev + real PG | 242 | 90 | 33 | 31 (+1 skip) | 82 | **478** | 95 | — (not run) | All green (RISK-03 close-out: PtPlan manifest endpoint+display; periodic-review completion signing — audit-trail-review + access-review, new compliance.sign key; document-detail migrated to shared e-sign dialog. AccessReviewSigningTests +2; ApiSurface +2 PtPlan signatures routes, reviewed) |
| 2026-08-06 | (pending commit) | dev + real PG | 242 | 88 | 33 | 31 (+1 skip) | 82 | **476** | 95 | — (not run) | All green (RISK-03 AQ manifest endpoints: 13 GET /api/<study>/{id}/signatures added; ApiSurface snapshot regenerated +26 routes, reviewed) |
| 2026-08-06 | (pending commit) | dev + real PG | 242 | 88 | 33 | 31 (+1 skip) | 82 | **476** | 95 | — (not run) | All green (RISK-03 borderline gates frontend: supplier/competency/conflict/test-auth UIs wired to the e-sign dialog; prod build + karma green) |
| 2026-08-06 | (pending commit) | dev + real PG | 242 | 88 | 33 | 31 (+1 skip) | 82 | **476** | 95 | — (not run) | All green (RISK-03 borderline gates backend: supplier/conflict/competency/test-auth signing ceremonies + suppliers.sign & conflicts.sign catalogue keys; role/catalog tests green) |
| 2026-08-06 | (pending commit) | dev + real PG | 242 | 88 | 33 | 31 (+1 skip) | 82 | **476** | 95 | — (not run) | All green (RISK-03 review-close gate: ManagementReview.Close signing ceremony, backend+frontend; live positive-mint proof MRV:{id} on real PG) |
| 2026-08-06 | (pending commit) | dev + real PG | 242 | 88 | 33 | 31 (+1 skip) | 82 | **476** | 95 | — (not run) | All green (RISK-03 PtPlan gate: new proficiency-testing.sign catalogue key + approve signing ceremony, backend+frontend; role/catalog tests green) |
| 2026-08-06 | (pending commit) | dev + real PG | 242 | 88 | 33 | 31 (+1 skip) | 82 | **476** | 95 | — (not run) | All green (RISK-03 non-AQ frontend: NC-close, audit, QP, change UIs wired to the e-sign dialog; prod build + karma green) |
| 2026-08-06 | (pending commit) | dev + real PG | 242 | 88 | 33 | 31 (+1 skip) | 82 | **476** | 95 | — (not run) | All green (RISK-03 non-AQ backend: NC-close, audit sign-off, QP approve, change approve e-signature ceremonies) |
| 2026-08-06 | (pending commit) | dev + real PG | 242 | 85 | 33 | 31 (+1 skip) | 82 | **473** | 95 | — (not run) | All green (RISK-03 AQ frontend: 13 sign-off UIs wired to the e-sign dialog; prod build + karma green) |
| 2026-08-06 | (pending commit) | dev + real PG | 242 | 85 | 33 | 31 (+1 skip) | 82 | **473** | 95 | — (not run) | All green (RISK-03 AQ batch backend: 13 analytical sign-off e-signature ceremonies) |
| 2026-08-06 | (pending commit) | dev + real PG | 242 | 81 | 33 | 31 (+1 skip) | 82 | **469** | 95 | — (not run) | All green (RISK-03 pilot: NC-verify e-signature ceremony + reusable e-sign components) |
| 2026-08-01 | (pending commit) | dev + real PG | 242 | 72 | 33 | 31 (+1 skip) | 82 | **460** | 87 | 6 | All green (six-item set + admin-issued PIN + equipment tabs/certificate) |
| 2026-08-01 | `b3f0cea` | dev + real PG | 237 | 72 | 33 | 31 (+1 skip) | 82 | **455** | 81 | — | All green (Quality Analytics) |
| 2026-08-01 | `063f98f` | dev + real PG | 228 | 72 | 33 | 31 (+1 skip) | 82 | **446** | 76 | 6 | **All green** |
| 2026-08-01 | `4be6b27` | dev + real PG | 228 | 72 | 33 | 31 (+1 skip) | 82 | **446** | 76 | 6 | All green |
| 2026-08-01 | `4be6b27` | dev + real PG | 228 | 72 | 33 | 31 (+1 skip) | 82 | **446** | 76 | 6 | All green (VER-001 tests added) |
| 2026-08-01 | `84d4ca7` | dev + real PG | 228 | 72 | 33 | 31 (+1 skip) | 78 | **442** | — | — | All green |
| 2026-07-31 | `647e70c` | dev + real PG | 228 | 72 | 33 | 31 (+1 skip) | 77 | **441** | — | — | All green |
| 2026-07-31 | `089fdf6` | dev + real PG | 228 | 72 | 24 | 31 (+1 skip) | 77 | **432** | — | — | All green |
| 2026-07-31 | `fdc08df` | dev + real PG | 228 | 72 | 24 | 31 (+1 skip) | 77 | **432** | — | — | All green (Phase 5) |
| 2026-07-31 | `9e7c3eb` | dev + real PG | 228 | 72 | 24 | 31 (+1 skip) | 77 | **432** | — | — | All green (Phase 4) |
| 2026-07-31 | `ea9eb24` | dev + real PG | 228 | 72 | 24 | 23 | 77 | **424** | — | — | All green (Phase 3) |
| 2026-07-31 | `8be2c13` | dev + real PG | 228 | 72 | 24 | 22 | 77 | **423** | — | — | All green (Phase 2) |
| 2026-07-31 | `28ba880` | dev + real PG | 228 | 72 | 24 | 18 | 77 | **419** | 76 | — | All green (Phase 1) |

CI additionally runs the backend suite against a **clean, freshly migrated PostgreSQL** on every
push; those runs are green for every commit listed above. A CI run is stronger evidence than a
developer machine for schema work, because it proves the migrations apply from nothing.

### Correction — two commit messages carry wrong totals

`647e70c` states "435 backend tests green" and `84d4ca7` states "436". Both are wrong: the
per-suite counts observed in those runs sum to **441** and **442** respectively. The runs were
green and the per-suite numbers were correct; only the totals in the prose were carried forward
stale. The table above records the arithmetic that the per-suite figures actually support.

This is a small error, and it is the first thing this log caught — which is the argument for
recording per-suite numbers rather than a single total that nobody can check.

---

## Suite composition

| Suite | Runs against | Notes |
| ----- | ------------ | ----- |
| Domain unit | in-process | Pure aggregate/invariant tests |
| Application unit | in-process, EF InMemory | Handlers, behaviours, policies |
| Architecture | compiled assemblies + source | Module boundaries, command policies, the `user_account` query-bound guard |
| Integration | **real PostgreSQL** | RLS isolation, immutability triggers, CHECK domains, migration round-trip. Skips if no server; 1 case skips when its table has no rows |
| WebApi functional | real HTTP host | 78 on EF InMemory; **4 on real PostgreSQL** (`RegulatedFlowRealDatabaseTests`, closing VER-001) |
| Frontend unit | ChromeHeadless | Angular components/services |
| E2E | live stack | Playwright, requires API + SPA running |

**Environment caveat.** The integration and real-database functional suites need a migrated
PostgreSQL. They **skip rather than fail** when none is reachable — so a green local run without
a database proves less than the count suggests. Read the skip count, not only the pass count.
