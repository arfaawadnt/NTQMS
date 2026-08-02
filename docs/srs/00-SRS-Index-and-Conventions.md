# NT.QMS — Production Software Requirements Specification
## Document 00 · Master Index, Conventions and Glossary

| Field | Value |
|---|---|
| **Product** | NT.QMS — multi-tenant SaaS Quality Management System for ISO/IEC 17025, ISO 15189, ISO 9001, 21 CFR Part 11 and EU GMP Annex 11 laboratories |
| **Specification baseline** | Source tree `D:\SAAS\QAMS\21-7\NT.QAMS`, release line **v1.51.x** |
| **Specification type** | **As-built** — reverse-specified from the current implementation |
| **Date of analysis** | 2026-08-01 |
| **Supersedes** | `NT_QMS_SRS.html` ("NT.QMS Quality Management System (v1.0)") — see [Document 13](13-Implementation-vs-SRS-Gap-Analysis.md) |
| **Status** | Draft for review — every requirement is traceable to code; unverifiable statements are explicitly flagged |

---

## 0.1 Purpose of this specification set

The previous SRS (`NT_QMS_SRS.html`) was written as a **design-intent** document before and during
early construction. The system as built diverges from it materially — in stack, in permission model,
in error contract, and in scope (the built system contains roughly six times the functional surface
the old SRS describes, and omits several things the old SRS promises).

This document set replaces it with an **as-built specification**: a complete, verifiable description
of the software **that exists today**, at a level of detail sufficient for a new development team to
rebuild the system from these documents alone, without reading the original source code.

### What this set does

- Documents every module, service, screen, workflow, business rule, validation, error path,
  configuration key, background process, and external dependency **that is present in the code**.
- Extracts business logic that currently exists **only** inside code (retry counts, lockout windows,
  stabilisation intervals, backoff curves, grouping rules, filename/reference generation, throttling,
  segregation-of-duty pairings, calculation formulae) and states it as explicit requirements.
- Marks every statement that could not be verified from source as **`[Assumption]`** or
  **`[Needs Business Confirmation]`**.

### What this set deliberately does NOT do

- It does **not** redesign the application.
- It does **not** invent features, screens, endpoints or rules.
- It does **not** present intent, roadmap items or audit-report aspirations as implemented behaviour.

> **Rule applied throughout:** if it is not in the code, it is not a requirement here. If it is in the
> code but its business rationale is unknown, it is recorded as behaviour with a
> **`[Needs Business Confirmation]`** flag.

---

## 0.2 Document map

| # | Document | Contents |
|---|---|---|
| **00** | *(this document)* [Index & Conventions](00-SRS-Index-and-Conventions.md) | Document map, ID schemes, glossary, method, evidence rules |
| **01** | [Software Requirements Specification](01-Software-Requirements-Specification.md) | Scope, context, actors, product perspective, assumptions, dependencies, full NFR set, regulatory mapping |
| **02** | Functional Specification — 4 parts: [1 Quality & Improvement](02-1-Functional-Specification-Quality-and-Improvement.md) · [2 Resources, People & Governance](02-2-Functional-Specification-Resources-People-Governance.md) · [3 Analytical Quality](02-3-Functional-Specification-Analytical-Quality.md) · [4 Operations & Platform](02-4-Functional-Specification-Operations-and-Platform.md) | All 35 modules, each with the same 17-section template (purpose → acceptance criteria) |
| **03** | [Business Rules Document](03-Business-Rules.md) | Complete BR catalogue, full 411-code error catalogue, decision tables, calculation specifications |
| **04** | [Configuration Reference](04-Configuration-Reference.md) | Every configuration key and hard-coded constant: type, default, required, validation, impact |
| **05** | [Screen Specification](05-Screen-Specification.md) | All 100 SPA components: purpose, controls, fields, states, disabled/loading/empty/error, dead controls |
| **06** | [Workflow Specification](06-Workflow-Specification.md) | Every state machine, sequence diagram, activity diagram, background-process timing |
| **07** | [File System Specification](07-File-System-Specification.md) | Every folder, generated file, temp file, retention, cleanup, archive strategy |
| **08** | [API Specification](08-API-Specification.md) | All 326 routes × 2 versioned forms: verbs, auth, contracts, errors, envelopes, headers |
| **09** | [Security Specification](09-Security-Specification.md) | AuthN/AuthZ, tenancy isolation, credentials, e-signature, threats, weaknesses |
| **10** | [Deployment Specification](10-Deployment-Specification.md) | Environments, artefacts, migrations, health, observability, backup/DR, CI gates |
| **11** | [Architecture Constraints](11-Architecture-Constraints.md) | Layering law, enforced boundaries, technology constraints, forbidden patterns |
| **12** | [Requirements Traceability Matrix](12-Requirements-Traceability-Matrix.md) | Requirement → priority → source → implemented? → code / UI / config location |
| **13** | [Implementation vs SRS Gap Analysis](13-Implementation-vs-SRS-Gap-Analysis.md) | Every difference classified: Missing from SRS / Missing from Code / Extra Behaviour / Outdated / Deprecated |
| **14** | [Technical Debt Report](14-Technical-Debt-Report.md) | Hard-coded values, coupling, duplication, large classes, magic values, hidden dependencies |
| **15** | [Recommendations](15-Recommendations.md) | Prioritised remediation and improvement backlog |

---

## 0.3 Identifier schemes

All identifiers are stable and cross-referenced between documents.

| Prefix | Meaning | Example | Defined in |
|---|---|---|---|
| `FR-<MOD>-nn` | Functional requirement | `FR-NC-07` | 02 |
| `NFR-<CAT>-nn` | Non-functional requirement | `NFR-PERF-03` | 01 |
| `BR-<MOD>-nn` | Business rule | `BR-DOC-04` | 03 |
| `VR-<MOD>-nn` | Validation rule | `VR-NC-02` | 03 |
| `ERR-<code>` | Error / refusal code (uses the real in-code code) | `ERR-SOD-CAPA-001` | 03 |
| `CFG-nn` | Configuration key | `CFG-14` | 04 |
| `CON-nn` | Hard-coded constant (not configurable) | `CON-09` | 04 |
| `SCR-nn` | Screen | `SCR-12` | 05 |
| `WF-nn` | Workflow / state machine | `WF-03` | 06 |
| `FS-nn` | File-system element | `FS-05` | 07 |
| `API-nn` | API surface requirement | `API-11` | 08 |
| `SEC-nn` | Security requirement | `SEC-08` | 09 |
| `DEP-nn` | Deployment requirement | `DEP-06` | 10 |
| `AC-nn` | Architecture constraint | `AC-04` | 11 |
| `UC-nn` | Use case | `UC-05` | 02 |
| `AT-<REQ>` | Acceptance criteria for a requirement | `AT-FR-NC-07` | 02 / 12 |
| `GAP-nn` | Gap-analysis finding | `GAP-17` | 13 |
| `TD-nn` | Technical-debt item | `TD-22` | 14 |
| `REC-nn` | Recommendation | `REC-09` | 15 |
| `LIM-nn` | Known limitation | `LIM-11` | 02 / 14 |

### Module codes (`<MOD>`)

Module codes follow the system's own `PermissionCatalog` module keys wherever one exists, so that a
requirement ID, a permission key and a navigation group all name the same thing.

| Code | Module | Permission key | Nav group |
|---|---|---|---|
| `NC` | Nonconformance & CAPA | `nc` | quality |
| `CMP` | Complaints | `complaints` | quality |
| `FBK` | Customer feedback | `feedback` | quality |
| `AUD` | Internal / external audits | `audits` | quality |
| `OBJ` | Quality objectives | `objectives` | quality |
| `CHG` | Change control | `changes` | quality |
| `MRV` | Management review | `reviews` | quality |
| `DOC` | Document control | `documents` | documents |
| `QP` | Quality policy | `quality-policy` | documents |
| `ARC` | Records & archive | `records` | documents |
| `RSK` | Risk register | `risks` | risk |
| `CLD` | Compliance ledger (audit trail, signatures) | `compliance` | risk |
| `COI` | Conflict of interest / impartiality | `conflicts` | risk |
| `CTX` | Organisational context | `org-context` | risk |
| `UAR` | User access review | `access-reviews` | risk |
| `EQP` | Equipment | `equipment` | resources |
| `RS` | Reference standards | `reference-standards` | resources |
| `ENV` | Environmental monitoring | `monitoring-points` | resources |
| `SUP` | Supplier quality | `suppliers` | resources |
| `COMP` | Competency | `competencies` | people |
| `TRN` | Training assignments | `training` | people |
| `AUTHZ` | Test authorisations | `test-authorizations` | people |
| `USER` | User administration | `users` | people |
| `AQ` | Analytical quality (QC + 12 study types + uncertainty) | `analytical-quality` | analytical |
| `PT` | Proficiency testing & PT plans | `proficiency-testing` | analytical |
| `TASK` | Tasks & SLA | `tasks` | operations |
| `NTF` | Notifications | `notifications` | operations |
| `RPT` | Reporting & KPIs | `reports` | operations |
| `ORG` | Organisation (branches, departments, test catalogue, LOVs) | `organization` | administration |
| `TEN` | Tenant settings | `tenant-settings` | administration |
| `ROLE` | Roles & privileges | `roles` | administration |
| `AUTH` | Authentication & session | *(no module key — cross-cutting)* | — |
| `PLT` | Platform control plane (tenant provisioning) | *(platform-admin only)* | — |
| `FILE` | File storage & evidence | *(cross-cutting)* | — |

---

## 0.4 Evidence and confidence conventions

Every requirement statement carries an evidence marker.

| Marker | Meaning |
|---|---|
| *(none)* | **Verified in source.** The behaviour is directly readable in the cited file. |
| **`[Assumption]`** | Inferred from code structure or naming, but not directly provable; the inference is stated. |
| **`[Needs Business Confirmation]`** | The behaviour is verified, but the *business intent* behind a specific value or rule is not documented anywhere and should be confirmed by the process owner. |
| **`[Not Executed]`** | The capability exists in code but has never been observed running in this environment (e.g. anything requiring Docker, SMTP, or a staging host). |
| **`[Dead / Unused]`** | Code or UI exists but is unreachable or has no effect. |

**Code citations** use repository-relative paths: `src/NT.QAMS.Domain/Improvement/Nonconformance.cs:212`.

### How this specification was produced

1. **Static extraction** — scripted parsing of the whole source tree produced machine-generated
   inventories: 54 controllers → 329 route actions with their authorisation attributes and dispatched
   command/query; 215 commands + 105 queries with their policies; 88 FluentValidation validators with
   every rule chain; 86 domain classes with every property, method and thrown error code; 68 enums;
   80 domain events; 416 distinct error codes with their messages; 100 SPA components with their
   controls, bound fields, state signals and branch conditions.
2. **Targeted reading** — the composition root, middleware pipeline, background services,
   interceptors, security policy, rate limiter, file policy and configuration guard were read in full.
3. **Cross-checking** — extracted counts were reconciled against the repository's own
   `ApiSurface.approved.txt` merge gate (658 lines = 329 routes × 2 route forms) and against the
   as-built architecture documents in `docs/reference/`.
4. **Gap comparison** — the previous SRS was compared statement-by-statement against the extraction.

**Counts are stated as measured, not as remembered.** Where a repository document disagrees with the
code, the code wins and the discrepancy is recorded in Document 13.

---

## 0.5 Measured system inventory

These figures are the factual baseline for the whole specification set.

> ### ⚠ Baseline note — the tree moved during analysis
>
> Extraction was first run against a tree with **326 endpoints / 214 commands / 103 queries /
> 411 error codes / 85 domain classes / 100 SPA components**. Part-way through, an **uncommitted
> Quality Analytics + Quality Health Score module** appeared in the working tree, moving the figures
> to those in the table below.
>
> The table states the **current** measurements. Two machine-generated appendices —
> [the route catalogue (Doc 08 §8.13)](08-API-Specification.md) and
> [the component inventory (Doc 05 §5.8)](05-Screen-Specification.md) — were generated at the earlier
> baseline; the delta is specified in
> [Doc 08 §8.14](08-API-Specification.md) and [Doc 02 M-38](02-4-Functional-Specification-Operations-and-Platform.md).
> The [error catalogue (Doc 03)](03-Business-Rules.md) was regenerated after the change and is current.
>
> Nothing else in the tree changed. `git status` at the time of writing showed 15 modified and
> 5 untracked files, all belonging to that one module (plus this specification set itself).

| Dimension | Count | Evidence |
|---|---:|---|
| Solution projects (source) | 6 | `src/NT.QAMS.{Domain,Application,Infrastructure,WebApi,Contracts,SharedKernel}` |
| Solution projects (test) | 6 | `tests/NT.QAMS.{Domain.UnitTests,Application.UnitTests,Architecture.Tests,IntegrationTests,WebApi.FunctionalTests,LoadTests}` |
| Hand-written backend C# | ≈29,700 lines / 376 files | excludes 113 EF migration files |
| EF Core migrations | 113 files / 56 migrations | `src/NT.QAMS.Infrastructure/Persistence/Migrations` |
| Frontend TypeScript | ≈29,100 lines / 215 files | `frontend/src` |
| HTTP controllers | 54 classes in 42 files | `src/NT.QAMS.WebApi/Controllers` |
| HTTP route actions | 329 | each also exposed under `/api/v{version}/…` → 652 routable paths |
| Commands (`ICommand`) | 215 | all carry an authorisation policy attribute |
| Queries (`IQuery`) | 105 | |
| FluentValidation validators | 88 | |
| Domain classes | 86 | aggregates, entities and value objects |
| Domain enums | 69 | |
| Domain events | 81 | |
| Distinct error / refusal codes | 416 | |
| Permission modules × actions | 31 × up to 8 = **171 keys** | `PermissionCatalog.AllKeys` |
| Built-in roles | 6 | `PlatformAdmin, TenantAdmin, QualityManager, DepartmentHead, Analyst, ExternalAuditor` |
| SPA components | 105 | |
| SPA route entries | 87 | 56 feature routes + 31 `:id` detail children; 84 lazy-loaded |
| Database tables | 97 | 5 schemas: `qams` (≈85), `audit` (4), `saas` (2), `read` (1), `public` (1) |
| Tables under FORCE row-level security | 90 | |
| Background hosted services | 5 | outbox, sweep, KPI snapshot, replica sentinel, deferred seeder |
| MediatR pipeline behaviours | 5 | Tracing → Logging → Authorization → Idempotency → Validation |
| HTTP middleware components | 7 (custom) | + framework auth/rate-limit/exception |
| Architecture Decision Records | 9 | `docs/adr/ADR-0001 … ADR-0009` |

---

## 0.6 Glossary

| Term | Definition as used in this system |
|---|---|
| **Tenant** | One laboratory organisation. The unit of data isolation. Identified by a URL-safe `slug` and a `Guid`. |
| **Platform / control plane** | The cross-tenant administrative surface (tenant provisioning, tenant listing). Only `PlatformAdmin` reaches it; a platform admin is **not** a member of any tenant. |
| **Workspace** | User-facing name for a tenant, resolved from a slug by `GET /api/auth/workspace/{slug}`. |
| **Aggregate / aggregate root** | A DDD consistency boundary. All invariants live *inside* the aggregate; handlers never enforce business rules. |
| **Slice** | A vertical feature file holding a module's commands, validators, handlers and queries together (e.g. `QualityPolicySlice.cs`). |
| **Command / Query** | CQRS request types. Commands mutate and are authorisation-gated deny-by-default; queries read. |
| **Command policy** | An attribute on a command record declaring who may execute it. A command with no policy fails the `CommandPolicyTests` CI gate (`AUTHZ-000` deny-by-default). |
| **RLS / FORCE RLS** | PostgreSQL row-level security, applied with `FORCE` so it binds even the table owner. Second, independent layer of tenant isolation beneath the EF global query filter. |
| **GUC** | PostgreSQL session variable. `app.current_tenant` and `app.bypass_rls` drive the RLS policies. |
| **Elevation** | The controlled, explicit path (`ICurrentTenantSetter.Elevate()`) by which trusted cross-tenant processes bypass RLS. |
| **Field-change ledger** | `audit.field_change` — append-only per-column before/after record of every tracked mutation, with actor, timestamp and reason. |
| **Security event log** | `audit.security_event` — append-only record of authentication, signature, export and authorisation events. |
| **Hash chain** | Tamper-evident linkage over ledger rows; verified by `GET /api/compliance/chain-verification`. |
| **E-signature** | 21 CFR Part 11 signing ceremony: password **and** 4-digit PIN, throttled per actor, failures logged as `ESIGN_FAILED`. |
| **Reason for change** | Mandatory `X-Change-Reason` header on every `DELETE`; stamped onto the ledger row in the same transaction. |
| **SoD** | Segregation of duties. Enforced as `EnsureSignerIsNotPreparer(actor, code)` inside aggregates. |
| **Outbox** | Transactional outbox table; a hosted processor publishes domain events at-least-once with backoff and dead-lettering. |
| **Sweep** | Hourly time-based reconciliation job that ages records (calibration due, competency expiry, escalation, etc.). |
| **LOV** | List of values — tenant-scoped, localisable reference data (`LovEntry`). |
| **Allocatable** | An aggregate that carries optional `BranchId`/`DepartmentId` so a user's organisational scope can filter it (`IAllocatable`). |
| **Study** | Any of the 12 analytical-quality investigations (precision, linearity, method comparison, …). All share the `DataEntry → Calculated → SignedOff` shape. |
| **Signed record** | A record in a terminal signed state. A database trigger physically rejects `UPDATE`/`DELETE` on it. |
| **Problem+json** | RFC 7807 error contract used by every error path, including framework 401/403. |
| **Envelope** | Paged list response shape (`items`, `total`, `page`, `pageSize`) introduced by API-004; legacy bare-array responses are also still served. |

---

## 0.7 Regulatory reference map

The system's compliance obligations, and where this specification addresses each.

| Standard / clause | Obligation | Where specified |
|---|---|---|
| **21 CFR Part 11 §11.10(a)** | Validation of systems | Doc 10 §10.9, `docs/validation/` |
| **§11.10(b)** | Accurate, complete record copies | Doc 02 `CLD`, Doc 08 exports |
| **§11.10(c)** | Record protection / retrievability | Doc 07, Doc 02 `ARC` |
| **§11.10(d)** | Limited system access | Doc 09 §9.2–9.4 |
| **§11.10(e)** | Audit trail, secure, computer-generated, time-stamped, retains originals, includes **reason for change** | Doc 02 `CLD`, Doc 03 BR-CLD-*, Doc 09 §9.7 |
| **§11.10(g)** | Authority checks / segregation of duties | Doc 03 §3.4 (SoD table) |
| **§11.10(k)** | Controlled documentation / revision control | Doc 02 `DOC` |
| **§11.200** | Electronic signature components & controls | Doc 09 §9.6 |
| **§11.300** | Identification-code/password controls | Doc 09 §9.3 |
| **EU GMP Annex 11 §4** | Qualification / change control | Doc 02 `CHG` |
| **Annex 11 §9** | Audit trails | Doc 02 `CLD` |
| **Annex 11 §12** | Security & access | Doc 09 |
| **ISO/IEC 17025 §6.2** | Personnel competence, authorisation | Doc 02 `COMP`, `AUTHZ` |
| **§6.4** | Equipment, calibration, intermediate checks | Doc 02 `EQP`, `RS` |
| **§6.6** | Externally provided products & services | Doc 02 `SUP` |
| **§7.7** | Validity of results — QC, PT/ILC | Doc 02 `AQ`, `PT` |
| **§7.9** | Complaints | Doc 02 `CMP` |
| **§7.10** | Nonconforming work | Doc 02 `NC` |
| **§7.11** | Control of data & information management | Doc 09, Doc 11 |
| **§8.2** | Management-system documentation / quality policy | Doc 02 `QP` |
| **§8.3** | Control of documents; controlled copies | Doc 02 `DOC` |
| **§8.4** | Control of records; retention | Doc 02 `ARC`, Doc 07 |
| **§8.5** | Risk & opportunity | Doc 02 `RSK` |
| **§8.6** | Improvement; customer feedback | Doc 02 `FBK`, `OBJ` |
| **§8.7** | Corrective action | Doc 02 `NC` |
| **§8.8** | Internal audit | Doc 02 `AUD` |
| **§8.9** | Management review | Doc 02 `MRV` |
| **§4.1** | Impartiality | Doc 02 `COI` |
| **ISO 9001 §4.1/4.2** | Context of the organisation; interested parties | Doc 02 `CTX` |
| **ISO 9001 §5.2** | Quality policy | Doc 02 `QP` |
| **ISO 9001 §6.2** | Quality objectives | Doc 02 `OBJ` |
| **ISO 15189** | Medical-laboratory analytical quality (reference intervals, sigma, comparability) | Doc 02 `AQ` |

> **`[Needs Business Confirmation]`** — the system implements controls that *support* these clauses.
> Whether the laboratory's own procedures satisfy each clause is an accreditation judgement outside
> the software's scope. No claim of certification is made here.

---

## 0.8 Reading order for a rebuild team

1. **Doc 01** — understand scope, actors and constraints.
2. **Doc 11** — understand the architectural law before designing anything.
3. **Doc 06 + Doc 03** — the state machines and rules *are* the product.
4. **Doc 02** — module by module, build order can follow the dependency notes.
5. **Doc 08 + Doc 05** — surface contracts (API first, then screens).
6. **Doc 04 + Doc 07 + Doc 10** — make it run.
7. **Doc 09** — security is not a phase; read it before, during and after.
8. **Doc 12–15** — verify what you built and inherit the known debt list knowingly.
