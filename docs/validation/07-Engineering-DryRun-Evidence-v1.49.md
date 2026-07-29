# Engineering Dry-Run Evidence — REVAL-NTQMS-001 pre-execution (v1.49.0)

| Field | Value |
| ----- | ----- |
| Document ID | REVAL-NTQMS-001-DR1 |
| System | NT.QMS at tag **v1.49.0** (commit `1beb3bf`, CI all-green) |
| Environment | **Development workstation** (Windows dev host; PostgreSQL 17 local; API `http://localhost:5080`, Development environment) |
| Executed by | Engineering (automated, unwitnessed) |
| Date executed | 2026-07-29 |
| Status | **ENGINEERING DRY-RUN — NOT the formal qualification.** |

> **Scope and limits — read first.** This record is an engineering *pre-execution* of the
> dev-executable subset of REVAL-NTQMS-001 (doc 06). Its purpose is to de-risk QA's formal
> execution: every check below ran for real and its actual output is recorded, so QA's
> session on the qualified environment should be a witnessed re-run, not a debugging
> exercise. It does **not** satisfy the protocol: it was not run on a qualified/staging
> environment, was not witnessed, and carries **no QA or System Owner signatures — by
> design**. The re-validation is *done* only when QA executes doc 06 Parts B–D on the
> qualified environment and the System Owner approves the VSR addendum. Nothing in this
> file may be transcribed into doc 06 signature cells.

## 1. Dry-run results (executed 2026-07-29 against the live dev instance)

| Ref | Check | Actual result (dev) | Dry-run P/F |
| --- | ----- | ------------------- | ----------- |
| IQ-16 (part) | `GET /health/live`; `GET /health/ready` | Both **HTTP 200** (DB up). DB-down 503 leg NOT run here (needs service stop — elevation); covered by `scripts/failure-drills.ps1` Drill 1 and `HealthEndpointTests` | Pass (partial) |
| IQ-20 | CHECK constraints present on regulated tables | Live `pg_constraint` counts: nonconformance 4, risk_item 4, equipment_item 2, audit 1, supplier_evaluation 1, training_assignment 1, work_task 1 | Pass |
| IQ-21 | Refresh-session + idempotency schema | `qams.refresh_session` and `qams.idempotency_record` both present | Pass |
| IQ-22 | `/metrics` Prometheus exposition | Live scrape returned RED histograms + `qams_outbox_backlog`, `qams_outbox_dead_letters`, `qams_outbox_oldest_pending_age_seconds`, `qams_job_last_success_timestamp_seconds` | Pass |
| IQ-24 | CI vulnerability-scan gates active | CI run `1beb3bf` (v1.49.0): `.NET SCA`, `npm SCA`, `Install Trivy` + `Trivy image vulnerability scan` steps all executed, all jobs green | Pass |
| IQ-25 | Frontend framework version | `frontend/package.json` → `@angular/{core,common,compiler} ^22.0.8`; CI `Setup Node 24`; production AOT build green | Pass |
| OQ-SCA-01 | npm audit vs exception register | `npm audit --omit=dev` = **0 advisories (all severities)**; `.github/npm-audit-allowlist.txt` contains **no active exceptions** | Pass |
| OQ-SCA-02 (part) | Post-upgrade SPA smoke | Live browser session on the Angular 22 dev build: sign-in (demo-lab TenantAdmin), dashboard KPI tiles, navigation — rendered, **0 console errors**. Full regulated-flow smoke (NC pager, reason-dialog delete, sign-out) remains for the witnessed run | Pass (partial) |
| OQ evidence engines | Automated suites | CI `1beb3bf`: backend suite vs real PostgreSQL 17 (RLS as `qams_app`), frontend 67 specs, auth + axe-a11y e2e — green. Prior local evidence: security probes 24/24 (`scripts/security-probe*.ps1`), poison→dead-letter drill live | Pass |

## 2. Not executable outside a qualified/staging environment (open)

| Ref | Item | Why open |
| --- | ---- | -------- |
| IQ-17/18/19, IQ-23 | Role-guard boot refusal (Production), TLS/HSTS at proxy, non-root deployed image, observability stack | Need the deployed/staging host (IQ-19 is CI-proven per build, deployment instance still to be checked) |
| OQ (witnessed set) | Doc 06 Part C manual cases | Require a witness per baseline QP convention |
| PQ-PERF-01/02, PQ-OBS-01 | Load ≥100 VU, 24 h soak, alert-fires drill | Staging only |
| SEC-001 | Independent penetration test | Independent party only |

## 3. What "done" requires (unchanged)

QA executes doc 06 Parts B–D on the qualified environment (attaching this dry-run as
supporting evidence), dispositions the pen-test report, and the System Owner signs the VSR
addendum. Engineering does not self-certify; this document deliberately contains no
signature block for QA or the System Owner.
