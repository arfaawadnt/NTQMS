# NT.QAMS — Product Inventory & Reverse-Engineering Report

| | |
|---|---|
| **Product** | NT.QAMS / NT.QMS — Multi-tenant SaaS Quality Assurance Management System (ISO/IEC 17025:2017, ISO 9001:2015, 21 CFR Part 11, EU GMP Annex 11) |
| **Vendor** | National Technology for Software |
| **Version analyzed** | v1.5.0 (code) / v1.2.0 (docs) — snapshot `QMS.zip`, analyzed 2026-07-21 |
| **Stack (as built)** | .NET 9 Clean Architecture (MediatR CQRS, EF Core 9/Npgsql, ASP.NET Identity + JWT, SignalR, Hangfire, Serilog/Seq, OTel) · Angular 18 standalone + Tailwind · PostgreSQL 17 · Redis · Docker Compose · IIS deployment |
| **Report type** | Reverse-engineering of UI, source code, SRS, documentation, workflows, database, and APIs — with per-feature implementation-status classification |

---

## 1. Executive Summary

**What this product actually is:** a **high-fidelity, deeply interactive UI prototype of a 32-module quality-management suite, riding on a minimal generic backend.** The tenant portal (a single 24,966-line Angular component) implements real, state-changing quality workflows — but persists everything as JSON blobs in **one key-value table (`TenantStorages`)** synced from `localStorage`. The backend contains a parallel, "proper" CQRS/REST module layer for ~20 entities, but it is **scaffold-grade (Create + List only) and non-functional against a real database** — only 3 business tables have migrations. The frontend never calls it.

**The five load-bearing facts:**

1. **Two backends exist; only the primitive one is used.** The frontend calls exactly 4 endpoints: `POST /api/auth/login`, `GET/POST /api/tenantstorages` (whole-tenant JSON blob), `POST /api/email/send`. The 20 per-module REST controllers (`/api/nonconformances`, `/api/documents`, …) are never invoked by any UI, expose only Create + List, have no FluentValidation (except the sample `QamsEntities` module), and their tables **do not exist in the migrations** — calls would fail with *relation does not exist* on real PostgreSQL.
2. **All business data is client-seeded mock data.** On first load the portal generates ISO-17025-flavored seed records (`checkAndSeedMockData`, flag `qams_seeded_v3`) for every module, stores them in `localStorage`, and mirrors them to the blob store.
3. **Security is demonstration-grade.** Roles/permissions are enforced **only in the browser** (backend has zero role checks — no roles seeded, no `[Authorize(Roles)]`, no policies). E-signature is a universal hardcoded PIN `0000`. The blob-store and email endpoints are `[AllowAnonymous]`. A leftover `/debug-db` endpoint dumps all tenants, users and claims without authentication. JWT secret, Gmail app password and DB credentials are committed to source.
4. **The documentation describes an aspirational system, not the built one.** The 32-module SRS, product documentation, and in-app manual "technical specs" reference relational tables, per-module APIs, Hangfire sweeps, SLA engines, MFA, and Dapper reporting that do not exist. The docs themselves were auto-generated from the app's `user-manual-data.ts` (unrendered template literals prove it).
5. **The whole product was produced by prompt-driven AI agents** ("Antigravity"/Gemini workspace — evidenced by `.agents/AGENTS.md`, build-note paths, the design-system PDF's source path, and a saved generation prompt that explicitly states *"No backend — all data is deterministic mock data"*).

**Classification totals** (36 tenant modules/cross-cutting features + 8 platform features, detail in §3 and §13):

| Classification | Count | Share |
|---|---|---|
| Fully Implemented | 3 | ~7% |
| Partially Implemented | 22 | ~50% |
| Prototype | 12 | ~27% |
| Missing (spec'd, not built) | 7 | ~16% |

**What is genuinely good and reusable:** the Clean Architecture backend skeleton with a correct reference module (`QamsEntities`: full CRUD, validation, pagination, RLS), a real 3-layer multi-tenancy design (EF global filters + save interceptor + connection-level `set_config`, RLS on the 2 real tables), a working SignalR live-sync channel, a genuinely computed Quality Health Score statistics engine, complete trilingual (EN/AR-RTL/FR) UI content, a full design system, and an unusually complete specification corpus (state machines, requirement IDs, acceptance criteria) to rebuild against.

---

## 2. Architecture — As Documented vs. As Built

| Aspect | Documented / claimed | Actually built |
|---|---|---|
| Persistence | 22 relational tables, PostgreSQL RLS on all | 3 tables with migrations (`Tenants`, `QamsEntities`, `TenantStorages`) + Identity tables. RLS only on `QamsEntities` + `TenantStorages`. Everything else = JSON blobs in `TenantStorages` |
| API | Per-module REST (`/api/documents`, `/api/nonconformities`, …) | 4 endpoints actually used; 20 scaffold controllers (Create+List) unused by UI; blob-store controller is the de-facto API and is anonymous |
| Frontend | Angular 18 modular app, ngx-translate | 2 routes, 2 monolithic components (tenant portal 24,966 lines; admin portal 1,603 lines); hand-rolled i18n dictionary; ngx-translate installed but never imported |
| AuthZ | RBAC, ~70 atomic `OBJECT.ACTION` privileges, SoD engine | Binary `[Authorize]` on JWT only; all role/permission logic client-side; no SoD code anywhere |
| Background jobs | Hangfire expiry sweeps, SLA escalation ladder | Hangfire server + dashboard mounted, **zero jobs registered**; no SLA engine |
| Billing | Stripe subscriptions (Free/Pro/Enterprise) | `StripeService` hard mock (`sk_test_mock`, fake checkout URL), never called; admin-portal revenue figure hardcoded `$14,500/mo` |
| Caching / reporting | Redis cache ≥85% hit; Dapper report queries <500ms | Redis used only by a health check (that reports Healthy on failure); Dapper wired in DI, never used |
| Multi-app suite | QAMS + LIMS + EHS products | LIMS/EHS are in-memory UI demos inside the tenant portal (data lost on refresh) |
| Tenant isolation | 3 layers incl. low-privilege `qams_app_user` | EF filter + interceptors real; app connects as `postgres` superuser (bypasses RLS); anonymous endpoints accept tenant from header/query |

**Deployment (as built):** Docker Compose (postgres:17, redis, seq, api on :5000) for the backend; Angular build copied to IIS `C:\inetpub\wwwroot\qams-ui`; CI = GitHub Actions backend-only build+test (which cannot pass — see §14).

---

## 3. Module Inventory — Master Classification

Classification rubric:
- **Fully Implemented** — works end-to-end as designed with real persistence appropriate to its purpose.
- **Partially Implemented** — genuine working workflow/UI, but persisted only via the KV blob store, and/or backend counterpart is scaffold-only; significant spec'd behavior absent.
- **Prototype** — interactive visuals over hardcoded/in-memory data, faked computation, or simulation fallbacks.
- **Missing** — specified in SRS/docs, no meaningful code.

### 3.1 Tenant portal — QAMS product (28 modules)

| # | Module (sidebar key) | UI reality | Backend reality | **Classification** |
|---|---|---|---|---|
| 1 | Quality Dashboard (`dashboard`) | 5 live KPI cards, click-through, priority-task banner, 7 module summaries — computed client-side from blob data | none (no dashboard API) | **Partially Implemented** |
| 2 | Document Control (`documentControl`) | Full lifecycle DRAFT→PENDING_APPROVAL→PUBLISHED, major/minor versioning, prior-version archival, approve/reject/revision-request, files as base64 data-URLs, notifications | `Documents` module = Create+List scaffold, no migration | **Partially Implemented** |
| 3 | NC & CAPA (`ncCapa`) | Full state machine OPEN→CAPA_IN_PROGRESS→VERIFICATION→CLOSED (+reopen), CAPA add/close, timeline events, e-sign gates | Create (ref-gen `NC-2026-###` + timeline seed) + List w/ children; no CAPA write, no transitions, no migration | **Partially Implemented** |
| 4 | Complaints (`complaints`) | CRUD, detail drawer, status flow, confidential masking, notifications | Create+List scaffold | **Partially Implemented** |
| 5 | Internal Audit (`internalAudit`) | CRUD, checklist + findings drawer tabs, timeline | Create+List scaffold | **Partially Implemented** |
| 6 | Competency & Training (`competencyTraining`) | CRUD, drawer, certificate preview | Create+List scaffold (spec'd ≥80%+PIN authorization gate not enforced) | **Partially Implemented** |
| 7 | Equipment & Calibration (`equipment`) | CRUD, **real auto status evaluation** (In-Cal/Due/Overdue/OOS from dates), maintenance tab, due alerts | Create+List scaffold (auto-lockout via Hangfire spec'd — absent) | **Partially Implemented** |
| 8 | Risk & Opportunity (`riskOpportunity`) | CRUD, 5×5 heat-map, mitigation, RPN | Create computes RPN=Likelihood×Impact (real), List; nothing else | **Partially Implemented** |
| 9 | Change Control (`changeControl`) | CRUD, drawer, filters | Create+List scaffold | **Partially Implemented** |
| 10 | Management Review (`managementReview`) | CRUD, sign-off drawer | Create+List scaffold (Decisions never writable server-side) | **Partially Implemented** |
| 11 | Supplier Quality (`supplierQuality`) | CRUD, scoring drawer | Create+List scaffold (EvaluationScore hardcoded 0.0) | **Partially Implemented** |
| 12 | PT / ILC (`ptIlc`) | CRUD, result drawer | Create+List scaffold; ResultValue/ZScore never set by any command — spec'd z-score calc absent | **Partially Implemented** |
| 13 | Quality Archive (`qualityArchive`) | CRUD, bulk-archive-by-age | Create+List scaffold | **Partially Implemented** |
| 14 | Quality Statistics (`qualityStatistics`) | Real computation engine (`stats-model.service.ts`): weighted Quality Health Score, per-module metrics, filters, print pack — but monthly trends use a seeded PRNG and some ratios are hardcoded (e.g. `×0.72`) | Dapper reporting layer spec'd — dead wiring | **Partially Implemented** |
| 15 | Management Review Pack (`managementReviewPack`) | Chart-dense printable multi-chapter pack (cover, TOC) | none (client print) | **Partially Implemented** |
| 16 | Method Validation (`methodValidation`) | 3-step wizard shell; "Run Analysis" sets a flag and shows a **static hardcoded SVG histogram**; CSV import, LIS test-connection and fetch are `setTimeout` fakes | none | **Prototype** |
| 17 | Quality Control / Westgard (`qualityControl`) | CSV upload UI, Levey-Jennings-style visuals; no real rule engine observed | none | **Prototype** |
| 18 | AI Quality Copilot (`aiAssistant`) | 4 canned keyword-matched responses behind an 800 ms delay; no LLM/API call | none (docs spec OpenAI/Gemini) | **Prototype** |
| 19 | My Tasks Queue (`tasksQueue`) | Task list view, escalation highlighting | none | **Prototype** |
| 20 | Users Management (`users`) | CRUD via generic engine, blob persist | Creates decorative `QamsUser` record only — no Identity user, no password, no role binding | **Partially Implemented** |
| 21 | Roles & Privileges Matrix (`privileges`) | Editable role×module×(View/Create/Edit/Approve/Delete) matrix, **actually enforced in UI** (sidebar hiding, button gating, branch/dept row filters); manager-only tab | **No server-side authorization at all** | **Partially Implemented** (client-side only) |
| 22 | LOVs (`lovs`) | Trilingual CRUD; "auto-translation" is an explicit simulation | Create+List scaffold (trilingual fields real) | **Partially Implemented** |
| 23 | Branches (`branches`) | CRUD | Create+List scaffold; no FK integrity (free-text BranchId everywhere) | **Partially Implemented** |
| 24 | Departments (`departments`) | CRUD | Create+List scaffold | **Partially Implemented** |
| 25 | Test Catalog (`testCatalog`) | CRUD | Create+List scaffold | **Partially Implemented** |
| 26 | System Audit Trail (`auditTrail`) | Filterable table over seeded data | Create+List (Take 500); **no automatic logging from any action**; tamper-evidence spec'd, absent | **Partially Implemented** |
| 27 | Active User Sessions (`userSessions`) | Table on seeded data | No session tracking/revocation backend | **Prototype** |
| 28 | Notification Settings + Monitor (`notificationsManagement`, `notificationsMonitor`) | Rule config, template editor, delivery log, retry UI; real dispatch engine (§11) | Email relay endpoint only | **Partially Implemented** |
| 29 | SLA & TAT Analytics (`slaConfig` view) | Config UI on `localStorage` key `qams_sla_configs`; docs label its API "Route Mock" | `SlaConfig` entity + Create/List exist; **no engine consumes them** | **Prototype** |
| 30 | User Manual (`userManual`) | 28-module trilingual handbook (guide/steps/rules/tech tabs), contextual Quick Help, PDF export | n/a (content) | **Fully Implemented** (content; its "tech specs" describe an unbuilt backend) |
| 31 | Profile Settings (`profileSettings`) | Language/theme/prefs | none | **Partially Implemented** |

### 3.2 Sibling products inside tenant portal

| Product | Screens | Reality | **Classification** |
|---|---|---|---|
| **LIMS** | Sample Tracking, Test Management, Lab Inventory, Calibration Logs, Maintenance Schedule, LIMS Statistics | Hardcoded in-memory arrays; `addSample()` pushes to array — **lost on refresh**; not even blob-persisted | **Prototype** |
| **EHS** | Incident Reporting, Safety Risk Assessment, EHS Audits, Waste Disposal, Emission Monitor, Safety Training | Same in-memory pattern | **Prototype** |

### 3.3 SaaS Admin Portal (`/#/admin`)

| Feature | Reality | **Classification** |
|---|---|---|
| Admin login | Hardcoded `admin@qams.com` / `Password123!`, session in `localStorage` | **Prototype** |
| Subscription request wizard | 4 steps; OTP hardcoded `1234` (UI says so); requests in `localStorage` | **Prototype** |
| Tenant provisioning | The **one real API call**: `POST /api/auth/register-tenant`; on failure silently simulates success via `localStorage` ("SaaS Server Offline… simulated") | **Partially Implemented** |
| Tenant list / edit / approve-reject | 3 seeded labs, `localStorage` CRUD | **Prototype** |
| Billing & revenue metrics | Revenue literal `$14,500/mo`; plan prices static strings; no Stripe client, no invoices | **Prototype** |
| Per-app config (QAMS/LIMS/EHS) | In-memory objects + success toast; no persistence | **Prototype** |
| Impersonation / launch | Builds tenant hash-URL only | **Prototype** |

### 3.4 Platform / cross-cutting features

| Feature | Reality | **Classification** |
|---|---|---|
| Authentication (JWT) | Register-tenant + login + HS256 JWT (120 min, `tenant_id` claim) work; no refresh/logout/lockout config; login UI accepts anything non-empty and falls back to `'demo-token'` | **Partially Implemented** |
| MFA / TOTP (FR-AUTH-01) | Nothing in code | **Missing** |
| Account lockout 5-fails/30-min (FR-AUTH-02) | Not configured | **Missing** |
| E-signature PIN (21 CFR Part 11) | Universal hardcoded `0000`; user attribution hardcoded `'arfa'` in NC timelines | **Prototype** |
| Segregation of Duties (5 spec'd rules) | No SoD code anywhere (docs even quote an exception class that doesn't exist) | **Missing** |
| Multi-tenancy | EF global filter on all `ITenantScoped` (real), save interceptor (real), `set_config('app.current_tenant')` interceptor (real), RLS on 2 tables only; app runs as `postgres` superuser; anonymous endpoints accept tenant via header/query | **Partially Implemented** |
| Billing (Stripe) | Mock service, never invoked; no billing controller | **Missing** (stub only) |
| Background jobs (Hangfire) | Server + `/jobs` dashboard mounted; **zero jobs defined** (expiry sweeps, escalations all spec'd) | **Missing** |
| Redis caching | Health-check only; check returns Healthy even when down | **Missing** |
| Dapper reporting layer | Factory implemented and injected, never consumed | **Missing** |
| Real-time (SignalR) | Hub + tenant groups real; client connects, auto-reconnects, live-syncs data cross-tab on `TenantDataUpdated`; only trigger is blob-store save | **Partially Implemented** |
| Email (SMTP) | Working on-demand relay `POST /api/email/send` (anonymous; Gmail creds hardcoded fallback); used by notification engine | **Partially Implemented** |
| i18n (EN/AR/FR + RTL) | Complete hand-rolled dictionary (thousands of keys), full RTL mirroring, per-role default language, localized charts and manual; ngx-translate installed but unused | **Fully Implemented** |
| Design system | 13-page spec (NT-QAMS-DS-2026): tokens, dual fonts (Encode Sans/Cairo), Title-Case + protected-acronym rules — enforced via global CSS and agent rules | **Fully Implemented** |
| Observability | Serilog+Seq, OpenTelemetry OTLP tracing/metrics, Scalar API docs, `/health` | **Partially Implemented** |
| CI/CD | GitHub Actions backend build+test only; no frontend CI, no deploy; docker-compose lacks frontend service | **Partially Implemented** |

---

## 4. Screen Inventory

**Tenant portal** — one URL per tenant (`/#/<tenantId>`), views switched by `activeTab`:

- **Shell**: gradient header (58 px) with notification bell dropdown, language cycler (EN→AR→FR), profile menu; collapsible grouped sidebar (288 px/80 px); toast system.
- **Screen patterns**: KPI-card dashboard · list screens with stats-row + search/status/branch/dept filters + table · right-side sliding **detail drawers** with per-module tabs (12 custom drawers: NC `details/capa/timeline`, Audit `details/checklist/findings/timeline`, Equipment `details/maintenance/timeline`, plus Documents, Complaints, Training, Risk, Change, Review, Supplier, PT, Archive) · generic add-record modal with per-module form fields · Method Validation multi-step wizard · privileges matrix editor · notification template editor modal · SLA config panel · user-manual reader (3 sub-tabs) · print-pack layouts (cover page, TOC, chapters) for Statistics and Review Pack.
- **QAMS sidebar groups**: Home (dashboard, AI assistant) · Analytics & Review (quality statistics, review pack) · Quality Processes (documents, NC/CAPA, complaints, change control, risk, method validation) · Audits & Review (internal audit, management review) · Resources & Competence (training, equipment, QC, suppliers, PT/ILC) · Records (archive) · Organization Setup (branches, departments, test catalog) · Administration (users, privileges, LOVs, notifications) · System Monitoring (audit trail, sessions, notifications monitor).
- **LIMS screens** (6) and **EHS screens** (6) as listed in §3.2.
- **Non-sidebar**: profile settings, user manual, tasks queue, login screen.

**Admin portal** (`/#/admin`): landing (3 cards) → admin login → dashboard with tabs `overview | qams | lims | ehs`, tenant table with search/plan filter, provision-tenant form, pending-request approval queue, edit-tenant modal, plan/revenue cards; separate 4-step subscription-request wizard with OTP step.

**Standalone artifact** (not linked from the app): `NT.QAMS Statistics & Review.html` — a bundled single-page CAPA/Pareto statistics dashboard in the NT.LIMS (Oracle APEX "Vita", purple #893C83) design; fully self-contained mock. **Prototype**.

---

## 5. Workflow Inventory

### 5.1 Specified state machines (docs/diagrams) vs implementation

| Workflow | Specified states (SRS/.mmd) | Implemented in UI | Implemented in backend |
|---|---|---|---|
| **Document lifecycle** | Draft→Review→Approved→Published→Obsolete, SoD, PIN release, obsolete watermark | DRAFT→PENDING_APPROVAL→PUBLISHED + versioning + reject/revision (no Obsolete stage, no watermark, no SoD) | Status default `"DRAFT"` only; no transitions |
| **NCR / CAPA** | Draft→Raised→Assigned→RCA→ActionPlan→PendingVerification→EffectivenessCheck→Closed (+Rejected, +fail loop-backs) | OPEN→CAPA_IN_PROGRESS→VERIFICATION→CLOSED (+reopen) with CAPA actions & timeline — simplified but functional | Create seeds status `"OPEN"` + timeline entry; no transition endpoints |
| **Equipment calibration** | Active→NeedsCalibration (auto sweep)→OutOfService (auto lockout)→Active | Client-side auto status evaluation from dates (In-Cal/Due/Overdue/OOS) — no LIMS blocking | Status default `"OPERATIONAL"`; no sweep (no Hangfire jobs) |
| **Competency** | PendingTraining→Evaluated (≥80%)→Authorized (assessor PIN) | Basic record CRUD; gate not enforced | Create+List only |
| **QC / Westgard** | ProfileLoaded→ValueEntered→EvaluatingRules→InControl/OutOfControl→Troubleshoot | Upload UI + visuals; no rule engine | none |
| **Method validation** | ProtocolConfigured→DataEntered→StatsCalculated→SignedOff (locks study) | Wizard shell with faked analysis/results | none |
| **Complaints** | LOGGED→ACKNOWLEDGED→VALIDATED→INVESTIGATING→OUTCOME_LOGGED→RESOLVED→CLOSED | Simplified status field + CRUD | Status default `"NEW"` |
| **Audit** | SCHEDULED→IN_PROGRESS→CHECKLIST→FINDINGS→CLOSED (+ auto-NCR from findings) | Checklist/findings drawer; no auto-NCR generation | Status default `"PLANNED"` |
| **Change / Review / Supplier / PT / Archive / Tasks** | Simple 2–4-state flows | Status fields editable via CRUD | Create-only defaults |

### 5.2 Functional workflows (verified state-changing code paths)

1. **NC/CAPA**: `transitionToCapa` → `submitForVerification` → `verifyCapaEffectiveness` → `closeNcCase` / `reopenNcCase`; each appends a timeline event, persists, refilters; close/verify/reopen gated by PIN (hardcoded `0000`).
2. **Document control**: draft → submit → `approveDocument` (e-signed) / `rejectDocumentUpdate` / `requestDocumentRevision`; version bump (+1.0/+0.1); prior version pushed to history log.
3. **Equipment**: `evaluateEquipmentStatuses` recomputes calibration state on load; due alerts dispatched.
4. **Record creation** across complaints/audits/reviews/equipment fires the notification dispatch engine.
5. **Tenant provisioning**: admin portal → `POST /api/auth/register-tenant` → tenant row + Identity admin user with `tenant_id` claim (with a silent client-side simulation fallback).
6. **Live sync**: any blob save → SignalR `TenantDataUpdated` → other sessions refresh that module's data in place.

---

## 6. User Journeys

**Specified in docs (3 canonical journeys):**
1. **NCR & CAPA effectiveness loop** — Analyst detects deviation → logs NC → QM triage + RPN → assign investigator → RCA (5-Whys/Fishbone) → CAPA plan → execute + evidence → QM verify (fail loops back) → effectiveness check → PIN sign-off + close.
2. **Controlled SOP drafting & PIN release** — Author drafts + uploads → Dept Manager verifies (reject → draft) → QM compliance review (reject loops) → PIN e-sign → publish + supersede prior versions.
3. **Audit program execution** — QM schedules program → Lead Auditor runs checklist (Conform/OFI/NC per item) → NC findings auto-generate NCRs → report sign-off + record lock.

**Implemented journeys (as-built):**
- **Lab user daily flow**: open `/#/<tenant>` → login (any non-empty credentials reach the API; demo fallback) → dashboard KPIs → drill into module → work list/drawer → create/transition records → receive bell + email notifications → other open sessions live-update via SignalR.
- **Quality Manager flow**: NC triage → CAPA management → verification/closure with PIN `0000`; document approval; privileges matrix and notification-rule administration (manager-gated in UI).
- **Prospect/tenant onboarding**: marketing landing → subscription request wizard (fake OTP `1234`) → admin approves (localStorage) → provision (real API or simulated) → tenant URL issued.
- **Auditor flow (read-only)**: role `auditor` with view-only matrix defaults; branch/dept row-level filtering applied client-side.

Missing vs spec: investigator assignment/RCA stages, audit→NCR auto-generation, effectiveness-check scheduling, any journey that requires background processing or server-enforced roles.

---

## 7. Business Rules Registry

**Implemented (somewhere real):**

| Rule | Where | Status |
|---|---|---|
| RPN = Likelihood × Impact | Backend `CreateRiskCommand` + UI heat-map | ✅ both sides |
| NC reference numbering `NC-2026-{seq}` | Backend create handler (count-based — race-prone) | ✅ backend |
| Document version bump major/minor (+1.0/+0.1) | UI | ✅ UI only |
| Equipment calibration status derivation (In-Cal/Due/Overdue/OOS) | UI `evaluateEquipmentStatuses` | ✅ UI only |
| Default lifecycle statuses per module (OPEN/DRAFT/PLANNED/NEW/…) | Backend entity defaults + UI | ✅ (as magic strings, no enums) |
| Tenant stamping + audit fields on save | `TenantSaveChangesInterceptor` | ✅ backend |
| Unique (TenantId, Key) blob constraint; unique tenant Identifier | Migrations | ✅ backend |
| Role×module×action gating; branch/dept record scoping; confidential-complaint masking | UI privilege engine | ✅ client-side only |
| Quality Health Score weights (NC 20 / PT 15 / Doc 15 / Equip 15 / Comp 10 / Audit 10 / Risk 10 / Complaint 5) | `stats-model.service.ts` | ✅ UI (with partly synthetic trend data) |
| Notification recipient resolution (role/user/entity-context) + channel prefs | UI dispatch engine | ✅ UI |

**Specified but NOT implemented (Missing):**

| Rule | Source |
|---|---|
| MFA/TOTP (FR-AUTH-01 / FR-SEC-MFA) | SRS |
| Lockout 30 min after 5 failed logins (FR-AUTH-02) | SRS |
| Author ≠ reviewer/approver + 4 other SoD rules (FR-DOC-02, FR-DOC-SOD) | SRS |
| Escalation ladder +24h→Owner, +48h→Dept Head, +72h→QM (FR-CAPA-03) | SRS |
| Auto OUT-OF-SERVICE + LIMS test blocking on overdue calibration (FR-GOV-01 / FR-EQUIP-LOCK) | SRS |
| Competency authorization = score ≥80% + assessor PIN (FR-GOV-02 / FR-TRAIN-SCORE) | SRS |
| PT z-score computation (FR-GOV-03) | SRS |
| Westgard rules 1-3s/2-2s/R-4s/10-x rejection; 1-2s warning | Product doc |
| "OBSOLETE — UNCONTROLLED" watermark on superseded PDFs (FR-DOC-03) | SRS |
| Per-user salted SHA-256 4-digit signature PINs | Product doc (built: universal `0000`) |
| Tamper-evident append-only audit trail (trigger-protected) | Product doc |
| Supplier cert-expiry auto-suspend + 30-day alert; SOP/calibration expiry sweeps | Product doc (no Hangfire jobs) |
| Retention classes / auto bulk-archive >5 years server-side | Product doc (UI-only approximation) |
| TenantSettings defaults (PasswordExpiryDays=90, CalibrationReminderDays=30, SopExpiryReminderMonths=3) | Product doc (no TenantSettings table) |
| Closure blocking while linked NCR/CAPA open (complaints/audits) | Product doc |
| AES-256 encryption of confidential complaints | Product doc (UI masks names only) |

---

## 8. Roles

**As documented** — three inconsistent vocabularies (a spec defect in itself):
- RBAC register: `SysAdmin, QualityManager, LabDirector, TechManager, Analyst, EquipmentOwner`
- Product doc personas: `Admin, QA Officer, Section Manager, Lab Technician, External Auditor/Guest`
- Method-validation module: `Technician, Section Head, QC Manager, Director`

**As built:**
- **Backend: no roles exist.** ASP.NET Identity role tables are created but never seeded, assigned, or checked. `QamsUser.Role` is a decorative string.
- **Frontend (tenant portal)**: 3 seeded editable roles — `manager` (Quality Manager), `tech` (Laboratory Technician), `auditor` (Internal Auditor). Role resolution string-matches the user's role text and **defaults unknown users to `manager`** (i.e., full rights).
- **SaaS admin**: a single hardcoded admin identity in the admin portal (`admin@qams.com`); backend seed user `admin@ammanlab.com` / `Password123!` for the demo tenant.

---

## 9. Permissions

- **Specified**: ~70 atomic `OBJECT.ACTION` privileges (only ~11 enumerated: `USER.CREATE`, `ROLE.MANAGE`, `LAB.CONFIG`, `DOC.CREATE/REVIEW/APPROVE/OBSOLETE`, `NCR.CREATE/TRIAGE/INVESTIGATE/ACTION_PLAN/VERIFY/CLOSE`, `AUDIT.PLAN`, `EQUIP.CALIB_SCHED/CALIB_LOG`), stored in a `RolePrivileges` table, JWT carrying a `privileges` claim.
- **As built**: a client-side matrix — role × ~28 modules × `View / Create / Edit / Approve / Delete` booleans, persisted as a blob (`privilegeMatrixData`). Enforced in the UI: `hasViewPrivilege` hides sidebar/views, `canAddDirectly`/`canDeleteTab` hide buttons, workflow methods guard transitions, `isRecordAuthorized` applies branch/department row scoping, and a per-role "system rights" set controls default language, color mode, and confidential-data visibility.
- **Server-side**: `[Authorize]` (any valid JWT) vs `[AllowAnonymous]` — nothing finer. **Any authenticated user can call any module endpoint; the blob store (all business data) and email relay require no authentication at all.** JWT contains no role or privilege claims.

---

## 10. Reports & Analytics

| Report | As built | Classification |
|---|---|---|
| Quality Statistics dashboard | Real client-side computation (Quality Health Score, per-module KPIs, SLA %, calibration availability, PT %, risk heat cells) + 7 hand-rolled SVG chart components (ring, gauge, donut, Pareto, line, h-bars, 5×5 heat-map); trends partly synthetic (seeded PRNG) | **Partially Implemented** |
| Management Review Pack | Printable multi-chapter pack (cover, TOC, chapters) via print window; spec'd PowerPoint export absent | **Partially Implemented** |
| Per-page exports | PDF = `window.print()` popup; Excel = HTML-table-in-Blob `.xls` (no real XLSX lib) | **Partially Implemented** |
| Levey-Jennings QC chart | Visuals present, no rule engine | **Prototype** |
| Method-validation statistics (histogram, Passing-Bablok, Bland-Altman, linearity) | Static hardcoded SVGs | **Prototype** |
| SLA & TAT analytics | Config UI only; no measurement engine | **Prototype** |
| Backend reporting layer (Dapper, <500 ms) | Dead wiring | **Missing** |
| User manual trilingual PDF export | Print-window based, works | **Partially Implemented** |
| Standalone Statistics & Review HTML | Self-contained mock (CAPA/Pareto), different design family | **Prototype** |

---

## 11. Notifications

| Channel / capability | As built | Classification |
|---|---|---|
| In-app bell + notifications monitor | Working over blob-persisted list; delivery log with status, retry UI | **Partially Implemented** |
| Dispatch engine | Real client-side engine: rule lookup by event id (`NC_CREATED`, `NC_REOPENED`, `DOC_PUBLISHED`, `DOC_REVISION`, `COMPLAINT_LOGGED`, `AUDIT_SCHEDULED`, `REVIEW_SCHEDULED`, `EQUIP_CALIB_DUE`), recipient resolution (users/roles/entity context), channel preference (System/Mail/Both), subject/body template compilation | **Partially Implemented** |
| Email | Works via anonymous `POST /api/email/send` (SmtpClient, Gmail; creds committed) | **Partially Implemented** |
| SignalR real-time | Hub + tenant groups + client auto-reconnect real; used as a **data-sync** channel (`TenantDataUpdated` on blob save), not per-event alerts; `ReceiveNotification` broadcast path exists but nothing calls it from domain logic | **Partially Implemented** |
| Template editor + settings UI | Present, blob-persisted | **Partially Implemented** |
| Scheduled expiry/SLA scanners (Hangfire) | Spec'd (SOP/calibration/supplier-cert/CAPA sweeps, hourly/daily config); zero jobs exist | **Missing** |
| Escalation ladder notifications (+24/48/72 h) | Spec'd; absent | **Missing** |

---

## 12. Integrations

| Integration | Claimed | Reality | Classification |
|---|---|---|---|
| PostgreSQL 17 | ✔ | Real (3 business tables + Identity; blob store is the workhorse) | Partially Implemented |
| SMTP (Gmail) | ✔ | Real, anonymous endpoint, hardcoded fallback creds | Partially Implemented |
| SignalR | ✔ | Real hub + client | Partially Implemented |
| Serilog + Seq | ✔ | Configured (console + Seq sink) | Fully Implemented |
| OpenTelemetry | ✔ | Tracing + metrics + OTLP exporter configured | Fully Implemented |
| Scalar / OpenAPI | ✔ | Dev-only, works | Fully Implemented |
| Stripe | ✔ (billing, plans) | Hard mock (`sk_test_mock`, fake checkout URL), never invoked, no controller | **Missing** (stub) |
| Hangfire | ✔ (sweeps/escalations) | Server + dashboard, zero jobs | **Missing** |
| Redis | ✔ (cache/sessions/rate-limit) | Health check only (and it lies) | **Missing** |
| Dapper | ✔ (reports) | Never consumed | **Missing** |
| OpenAI GPT-4 / Gemini (AI copilot) | ✔ | Canned client-side responses | **Missing** |
| HL7 v2.5 / ASTM LIS interface | ✔ (method validation) | `setTimeout` simulation | **Missing** |
| S3 file storage | ✔ (document versions) | Files stored as base64 data-URLs in JSON blobs | **Missing** |
| Docker Compose / IIS | ✔ | Real (compose for backend stack; IIS for frontend) | Partially Implemented |

---

## 13. Data Layer & API Reality

**Physical schema (migrations):** `Tenants` (control plane), `QamsEntities` (+RLS), `TenantStorages` (+RLS, unique (TenantId,Key)), ASP.NET Identity tables. A `qams_app_user` low-privilege role is created **but the app connects as `postgres`**, so RLS is bypassed in practice. The other ~22 domain entities have DbSets and entity classes but **no migrations** — both migration Designer snapshots are empty stubs and `PendingModelChangesWarning` is suppressed, so `Migrate()` succeeds while leaving the model unbuilt.

**Actual data flow:** UI state (any[] fields) ⇄ `localStorage` (per-tenant keys) ⇄ `POST/GET /api/tenantstorages` (JSON blob per module key) → SignalR fan-out. `TenantStoragesController` additionally contains ~350 lines of ad-hoc JSON→relational sync for 9 keys (using `IgnoreQueryFilters()`), foreshadowing a migration to the relational model; a companion "test" (`DbDataSeederTest`) is really a one-off migration script against live localhost Postgres with zero assertions.

**Complete API surface:**

| Controller | Auth | Endpoints | Used by UI |
|---|---|---|---|
| Auth | anonymous | `POST register-tenant`, `POST login` | ✅ both |
| TenantStorages | **anonymous** | `GET` (all keys), `POST` (save key) | ✅ core data path |
| Email | **anonymous** | `POST send` | ✅ |
| QamsEntities | `[Authorize]` | full CRUD + paged/filtered GET (the reference module) | ❌ |
| Nonconformances | `[Authorize]` | `GET`, `POST` | ❌ |
| 17 more module controllers (Archives, AuditLogs, Audits, Branches, Changes, Complaints, Departments, Documents, Equipment, Lovs, PtPrograms, QualityManual, Reviews, Risks, SlaConfigs, Suppliers, TestsCatalog, Users) | `[Authorize]` | `GET` list + `POST` create only | ❌ |
| WeatherForecast | **none** | `GET /debug-db` — dumps all tenants, users, claims | ❌ (security leak) |

---

## 14. Quality, Security & Delivery Findings

**Security (blocking for any real deployment):**
1. Committed secrets: JWT signing key, Gmail app password, DB password (`appsettings.json` + code fallbacks). Rotate immediately.
2. `[AllowAnonymous]` on the entire business-data store and email relay; tenant asserted via header/query for anonymous callers → cross-tenant read/write and mail-relay abuse.
3. Unauthenticated `/debug-db` dumps users + claims.
4. No server-side authorization (roles/permissions UI-only), universal e-sign PIN `0000`, hardcoded `'arfa'` attribution.
5. CORS `SetIsOriginAllowed(_ => true)` + `AllowCredentials` (insecure combination).
6. App connects as `postgres` superuser → RLS inert.

**Build/test health:**
- The test project **does not compile as-is**: 20 of 24 tests call a 3-argument `ApplicationDbContext` constructor; the real one takes 2. CI (`dotnet test`) therefore cannot pass; one "test" also requires a live localhost Postgres.
- No frontend CI; frontend absent from docker-compose.
- Domain uses magic strings throughout (no enums); `BranchId`/`DeptId` are free text (no FKs).
- Dependency drift: `@ngx-translate` installed/unused, `ws` (Node server lib) in a browser app.

**Provenance:** built via prompt-driven AI agents (Antigravity/Gemini): `.agents/AGENTS.md` agent rules, saved generation prompt (`antigravity_prompt_method_validation.md` — *"No backend — all data is deterministic mock data"*), doc-generation pipeline in `BUILD_NOTES.md`, Gemini workspace paths in the design PDF. Docs were generated from UI data structures, which explains the systematic doc-vs-code inflation.

**Notable spec-vs-spec contradictions (flagged for the product owner):** NCR state-machine richness (9-state diagram vs 4-state doc text vs 4-state UI), three incompatible role vocabularies, PIN "exactly 4" vs "4+" digits, MFA scope (all users vs managers), 22 PascalCase data-dictionary tables vs lowercase per-module table names, doc appendix API list vs per-module route claims.

---

## 15. What Exists to Build On (assets inventory)

1. **Specification corpus** — 32-module SRS with requirement IDs, Given-When-Then acceptance criteria, 16 diagrams incl. 6 state machines, C4 set, data dictionary (22 tables), compliance traceability matrix (ISO 17025/9001/Part 11). Needs de-duplication and contradiction resolution, then usable as rebuild input.
2. **Reference backend module** — `QamsEntities` (CRUD + validation + pagination + RLS) as the pattern to replicate across the 20 scaffold modules; multi-tenancy interceptors; JWT/Identity bootstrap; JSON→relational sync code as a data-migration head start.
3. **Front-end behavior spec** — the tenant portal itself is effectively an executable functional specification: complete screens, drawers, filters, workflows, notification rules, privilege matrix semantics, and trilingual copy for every module.
4. **i18n content** — thousands of translated EN/AR/FR strings + the 28-module trilingual user manual.
5. **Design system** — full token/typography/layout/casing spec plus enforcing CSS.
6. **Statistics engine** — the Quality Health Score model is portable to a server-side reporting layer.

---

*Report generated 2026-07-21 by reverse-engineering the QMS.zip snapshot (backend 145 C# files; frontend 24,966-line tenant portal + 1,603-line admin portal; SRS/product docs/diagrams; migrations; tests; CI/devops). No code was modified.*

---

## 13. HQMS Hospital Extension — Inventory Addendum (2026-08)

The `feature/hqms-hospital-modules` train extends the product beyond the laboratory inventory above with the hospital-QMS module set (spec HQMS-MSP-001; gap analysis `E:\QMS\NT_QAMS_HQMS_Gap_Analysis_Implementation_Plan_2026-08-25.html`):

- **New modules (12):** M02 Incident & Occurrence Reporting · M04 FMEA/HFMEA (within Risk) · M05 Annual Audit Programme · M06 Quality Indicators + SPC · M07 Accreditation & Standards · M08 Patient Safety (falls/pressure injuries) · M09 Infection Prevention & Control · M10 Mortality, Morbidity & Peer Review · M11 Patient-Satisfaction Surveys · M12 Training Management · M13 Credentialing & Privileging · M15 Environment of Care & Emergency Preparedness · M17 Committees & Governance · M24 Integration Hub foundation (ADT census projection).
- **Completed modules (4):** M01 Document Control Read-and-Understand · M14 Equipment downtime/recalls · M16 Supplier contracts/CARs/outsourced oversight · M18 Change Control emergency pathway.
- **Surface delta vs v1.54.0:** OpenAPI operations 710 → **1,062** (+352, none removed or changed); `ApiSurface.approved.txt` 1,062 lines at 531/531 versioned parity; **16 new SPA pages** under a new "Clinical Governance" navigation group; i18n dictionary at **2,479 trilingual (EN/AR/FR) entries** covering all new enum families; 63 help topics with full route↔help parity.
- **Verification posture:** conformance report + annexes + findings register in `E:\QMS\NT_QAMS_HQMS_Conformance_*_2026-08-28.md` / `NT_QAMS_HQMS_Audit_Register_2026-08-28.md`; architecture suite 33 → 190 tests + 3 shrink-only decision snapshots. Validation/CSV delta for the train is **not yet authored** (register B-02; folds under DOC-001).

*Addendum recorded 2026-08-28.*
