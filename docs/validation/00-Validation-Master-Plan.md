# Validation Master Plan (VMP)

| Field | Value |
| ----- | ----- |
| Document ID | VMP-NTQMS-001 |
| Title | Validation Master Plan — NT.QMS Computerized System |
| System | NT.QMS (multi-tenant SaaS Quality Management System) |
| Version | 1.0 (DRAFT — framework for execution) |
| Status | Issued for execution readiness |
| Regulatory frameworks | 21 CFR Part 11; EU GMP Annex 11; ISO/IEC 17025:2017; ISO 15189:2022; ISO 9001:2015 |
| GAMP reference | ISPE GAMP 5 (2nd ed.) — A Risk-Based Approach to Compliant GxP Computerized Systems |

> **Statement of honesty.** This VMP and the accompanying document set constitute the
> **validation framework** for NT.QMS. Protocols (IQ/OQ/PQ) are provided as executable
> templates. No executed, signed, or dated verification evidence is asserted in this set.
> Automated-test results referenced herein are the continuous-verification engine and are
> cited as design/development evidence, not as a substitute for formal, signed qualification.

---

## 1. Purpose and Scope

### 1.1 Purpose
This VMP defines the strategy, deliverables, responsibilities, and acceptance criteria for
the computerized system validation (CSV) of NT.QMS, following a GAMP 5 risk-based V-model.
It governs how the system is qualified for use in a GxP / accredited-laboratory environment
where it manages quality records subject to 21 CFR Part 11 and equivalent controls.

### 1.2 In scope
- The NT.QMS application: .NET 9 Web API backend, PostgreSQL 17 database, Angular 18 frontend.
- GxP-relevant functions: security & access control, audit trail, electronic signatures,
  document control, nonconformance / CAPA / quality events, analytical quality (QC / method
  validation / proficiency testing), records retention & archival, governance, backup/DR.
- The validated build & release pipeline (`.github/workflows/ci.yml`).
- The production database hardening model (`deploy/harden-runtime-role.sql`).

### 1.3 Out of scope
- Qualification of the underlying cloud hosting infrastructure (covered by a supplier /
  infrastructure qualification and a hosting-provider assessment — see VSR outstanding items).
- Validation of client browsers and operating systems (standard, commercially available).
- Business SOPs that surround system use (owned by the Quality Unit).

---

## 2. System Description

NT.QMS is a multi-tenant Software-as-a-Service QMS for ISO 17025 / 15189 / 9001,
21 CFR Part 11 and GMP laboratories. It is built on Clean Architecture with CQRS.

| Layer | Project | Responsibility |
| ----- | ------- | -------------- |
| Domain | `src/NT.QAMS.Domain` | Aggregates, invariants, domain events (19 modules) |
| Application | `src/NT.QAMS.Application` | CQRS commands/queries, ports, policies |
| Contracts | `src/NT.QAMS.Contracts` | DTOs / request-response contracts |
| Infrastructure | `src/NT.QAMS.Infrastructure` | EF Core, interceptors, ledgers, JWT, TOTP, storage, jobs |
| WebApi | `src/NT.QAMS.WebApi` | REST controllers, middleware pipeline, authorization |
| Frontend | `frontend/` | Angular 18 SPA |

**Key architectural GxP controls (grounded in code):**
- **Multi-tenant isolation** — PostgreSQL Row-Level Security, `ENABLE`+`FORCE`, set per
  connection via `TenantConnectionInterceptor` (GUC `app.current_tenant`); fail-closed.
- **Tamper-evident audit trail** — per-tenant SHA-256 **hash chain**
  (`AuditTrailEntry`, `LedgerHash.Compute`), append-only, chain-verifiable.
- **Field-level change history** — `FieldChangeInterceptor` writes contemporaneous
  `FieldChangeRecord` rows (who/what/old/new/when/reason) in the same transaction.
- **Dual-component electronic signatures** — `ESignatureService` verifies password **and**
  PIN (21 CFR Part 11 §11.200(a)(1)), throttled and lockable.
- **Signed-record immutability** — DB trigger `qams.reject_frozen_mutation()` blocks
  UPDATE/DELETE after sign-off; runtime role has no DELETE grant.
- **Least-privilege runtime role** — `qams_app` (DML only, no DELETE, `NOBYPASSRLS`),
  separate from the migration-owning `qams_owner`.
- **Per-tenant optional MFA** — TOTP (RFC 6238), enrollment-gated.

**Module inventory (Domain):** AnalyticalQuality, AuditManagement, Competency,
ComplianceLedger, DocumentControl, Equipment, Facility, Files, IdentityAccess, Improvement,
Notifications, Organization, Records, Reporting, RiskGovernance, Sla, SupplierQuality,
Tenancy.

**Approximate verification asset base:** ~270 backend automated tests across 5 test
projects; ~37 frontend unit tests (10 spec files); Playwright e2e suite (`auth.spec.ts`,
`regulated-workflow.spec.ts`).

---

## 3. GAMP 5 Software Category Determination

| Candidate category | Fit for NT.QMS |
| ------------------ | -------------- |
| Cat 1 (infrastructure) | Applies to .NET runtime, PostgreSQL, OS — supporting, not the system |
| Cat 3 (non-configured COTS) | No — not off-the-shelf; substantial bespoke domain logic |
| Cat 4 (configured product) | Partial — per-tenant configuration (MFA policy, Westgard limits, LOVs, roles) |
| **Cat 5 (bespoke / custom application)** | **YES — determined category** |

**Determination: GAMP 5 Category 5 (bespoke), with Category 4 configuration aspects.**

**Justification.** NT.QMS is custom-developed software: its GxP-critical logic — hash-chained
audit trail, dual-component e-signature, tenant RLS enforcement, signed-record immutability
triggers, Westgard QC evaluation, CAPA/NC state machines, separation-of-duties guards — is
purpose-built code in this repository, not vendor configuration. Bespoke code carries the
highest validation rigor: the full software lifecycle must be evidenced (requirements,
design, code, and structured/functional testing) and the supplier (the development
organization) is subject to the same scrutiny. Configurable elements (per-tenant MFA
enforcement `TenantSettings.RequireMfaForPrivilegedRoles`, externalized Westgard limits
`AnalyticalQuality:Westgard:*`, list-of-values, role assignments) are handled as Category 4
configuration items within the Category 5 lifecycle.

---

## 4. Validation Approach & Lifecycle (V-Model)

A risk-based V-model links each specification to a corresponding verification:

```
 User Requirements (URS) ─────────────────────────► Performance Qualification (PQ)
   │                                                      ▲
   └─ Functional / Risk Assessment (FRA) ──────► Operational Qualification (OQ)
        │                                              ▲
        └─ Design (aggregates / controllers) ──► Installation Qualification (IQ)
             │                                       ▲
             └─────────── Build / Unit & Integration Tests ───────────┘
```

- **Left leg (specify & design):** URS → FRA → design elements (Domain aggregates,
  WebApi controllers, Infrastructure interceptors/migrations).
- **Bottom (build & verify unit):** automated test suites + CI pipeline as the continuous
  verification engine.
- **Right leg (qualify):** IQ (environment), OQ (function-by-function), PQ (end-to-end
  business workflows).
- **Traceability:** the RTM (`04-Requirements-Traceability-Matrix.md`) is the spine — every
  URS traces to a design element and a verification method (automated test and/or OQ/PQ case).

Risk drives rigor: high-risk GxP functions (audit-trail immutability, e-signature, tenant
isolation, reason-for-change) receive the most stringent testing (see FRA).

---

## 5. Roles & Responsibilities

| Role | Responsibilities |
| ---- | ---------------- |
| System Owner (Business) | Owns the validated state; approves VMP, VSR; authorizes go-live |
| Quality Unit / QA | Approves all validation deliverables; owns change control & periodic review |
| Validation Lead (CSV) | Authors/maintains the validation set; coordinates execution |
| Development Supplier (internal dev team) | Design, code, unit/integration tests, CI pipeline; supplier documentation |
| IT / Infrastructure | IQ execution; environment, DB roles, secrets, backup/DR |
| Test Executors (SMEs) | Execute OQ/PQ scripts; record actual results & evidence |
| Approvers (signatories) | Review and sign executed protocols and reports |

Segregation of duties: an author of a record may not be its sole verifier/approver — enforced
in the system (`EnsureSignerIsNotPreparer`, NC verify SoD) and mirrored in the validation
process (execution vs approval performed by different individuals).

---

## 6. Deliverables (this document set)

| ID | Deliverable | File |
| -- | ----------- | ---- |
| VMP | Validation Master Plan | `00-Validation-Master-Plan.md` |
| URS | User Requirements Specification | `01-User-Requirements-Specification.md` |
| FRA | Functional Risk Assessment | `02-Functional-Risk-Assessment.md` |
| IQ/OQ/PQ | Qualification Protocols | `03-IQ-OQ-PQ-Protocols.md` |
| RTM | Requirements Traceability Matrix | `04-Requirements-Traceability-Matrix.md` |
| VSR | Validation Summary Report (interim) | `05-Validation-Summary-Report.md` |

Supporting controlled documents (existing in repo): `deploy/BACKUP-RESTORE-DR.md`,
`deploy/harden-runtime-role.sql`, `deploy/DEPLOY.md`, `.github/workflows/ci.yml`.

---

## 7. Acceptance Criteria

The system is considered validated for its intended use when:
1. Every URS requirement traces to at least one satisfied verification (RTM 100% closed).
2. IQ confirms the qualified environment (correct .NET 9 / PostgreSQL 17 versions, all EF
   migrations applied, RLS `ENABLE`+`FORCE` active, least-privilege runtime role in force,
   secrets provisioned from the secret store, backups configured).
3. OQ demonstrates each GxP function behaves per specification (all high-risk cases PASS;
   no open critical/major deviations).
4. PQ demonstrates the intended business workflows end-to-end in the production-equivalent
   environment.
5. All deviations are resolved or risk-assessed and accepted by the Quality Unit.
6. The VSR is approved and signed by the System Owner and Quality Unit.

Defect classification: **Critical** (GxP data integrity / Part 11 control failure) — must be
fixed and retested before release; **Major** — fixed or formally risk-accepted; **Minor** —
tracked, may be deferred with justification.

---

## 8. Change Control & Periodic Review

- **Change control.** After validation, any change to GxP-relevant code, configuration, or
  environment follows documented change control: assess impact & risk, determine
  re-validation scope, execute targeted IQ/OQ/PQ, update the RTM, obtain QA approval. The
  system supports this with the in-app Change Request workflow (`/api/changes`, states
  Draft→…→Closed→Reviewed incl. post-implementation review) and the CI pipeline
  (architecture-boundary tests are a merge gate; migrations are applied and integration
  tests run against real PostgreSQL on every push).
- **Configuration management.** Source, migrations, and pipeline are version-controlled;
  releases are tagged (e.g., v1.25.0 … v1.34.0). Secrets are excluded from source (F-17).
- **Periodic review.** At a defined interval (recommended annually, or on major change), the
  validated state is reviewed: audit-trail chain integrity (`/api/compliance/chain-verification`),
  user-access recertification (`/api/access-reviews`), audit-trail review completion
  (`/api/compliance/audit-trail-reviews`), open deviations, and cumulative changes.

---

## 9. Referenced Documents

- URS, FRA, IQ/OQ/PQ, RTM, VSR (this set).
- `README.md`, `IMPLEMENTATION_LOG.md` (development records).
- `deploy/BACKUP-RESTORE-DR.md` (F-10), `deploy/harden-runtime-role.sql` (F-01/F-02),
  `deploy/DEPLOY.md`, `deploy/DEV-SECRETS.md` (F-17).
- CSV & Regulatory Readiness Audit and Remediation Plan (findings F-01…F-18) — summarized
  in the VSR as hardening evidence.
