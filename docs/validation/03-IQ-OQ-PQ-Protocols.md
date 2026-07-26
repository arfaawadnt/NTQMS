# IQ / OQ / PQ Qualification Protocols

| Field | Value |
| ----- | ----- |
| Document ID | QP-NTQMS-001 |
| System | NT.QMS |
| Version | 1.0 (executable templates — not yet executed) |
| Parent | VMP-NTQMS-001; URS-NTQMS-001; FRA-NTQMS-001 |

> **Execution note.** All tables below are **templates ready for execution**. "Actual",
> "Pass/Fail", "Executed by", and "Date" columns are to be completed during formal execution
> and signed. Where an automated test is named, it is the OQ evidence engine and may be
> attached as executed evidence; a manual confirmation step is still recorded.

**Signature block (per executed protocol):**

| Activity | Name | Signature | Date |
| -------- | ---- | --------- | ---- |
| Prepared by | | | |
| Executed by | | | |
| Reviewed by (QA) | | | |
| Approved by (System Owner) | | | |

---

# Part 1 — Installation Qualification (IQ)

**Objective:** confirm the system is installed and configured correctly in the qualified
environment before functional testing.

## IQ Pre-requisites
- Signed VMP, URS, FRA.
- Target environment provisioned (application host, PostgreSQL 17 host, object storage).
- Release artifact from a tagged CI build (`deploy/NT.QAMS-webapi-*.zip`, frontend dist).

## IQ Test Cases

| Step | Verification | Expected | Actual | P/F | Evidence |
| ---- | ------------ | -------- | ------ | --- | -------- |
| IQ-01 | Runtime platform | .NET 9 runtime present on app host | | | `dotnet --info` output |
| IQ-02 | Database engine | PostgreSQL 17 running and reachable | | | `SELECT version();` |
| IQ-03 | Frontend runtime/build | Angular 18 production (AOT) build deployed | | | build manifest / `npm run build` log |
| IQ-04 | Schema migrations | All EF Core migrations applied; `__EFMigrationsHistory` matches release | | | migration list vs `dotnet ef migrations list` |
| IQ-05 | RLS active | On every `ITenantScoped` table: `relrowsecurity = t` AND `relforcerowsecurity = t` | | | `SELECT relname,relrowsecurity,relforcerowsecurity FROM pg_class …` |
| IQ-06 | Tenant-isolation policy | Policy `tenant_isolation` present on each tenant table (USING + WITH CHECK) | | | `SELECT * FROM pg_policies` |
| IQ-07 | Least-privilege runtime role | App connects as `qams_app`; role is `NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE` | | | `\du qams_app`; `deploy/harden-runtime-role.sql` applied |
| IQ-08 | No DELETE grant | `qams_app` has SELECT/INSERT/UPDATE on `qams`/`read`/`saas`, SELECT+INSERT on `audit`, SELECT on `ref`; **no DELETE** anywhere | | | `information_schema.role_table_grants` |
| IQ-09 | Immutability trigger | `qams.reject_frozen_mutation()` exists and is bound BEFORE UPDATE/DELETE on the 12 analytical roots + `uncertainty_budget` | | | `SELECT tgname FROM pg_trigger …` |
| IQ-10 | Audit-schema RLS write policy | `audit.*` policies accept null-tenant appends; `qams.*` strict | | | `pg_policies` for audit schema |
| IQ-11 | Secrets provisioned | JWT signing key + DB credentials sourced from secret store; `appsettings.*.json` contains no secrets | | | secret-store manifest; `deploy/DEV-SECRETS.md` |
| IQ-12 | TLS | API served over TLS; DB connections encrypted | | | TLS handshake / cert |
| IQ-13 | Backup configured | WAL archiving on (`archive_mode=on`, `wal_level=replica`, `archive_timeout=300`); nightly `pg_dump` scheduled | | | `deploy/BACKUP-RESTORE-DR.md`; `postgresql.conf` |
| IQ-14 | Build provenance | Deployed artifact traces to a tagged, green CI run (build+test+migrations) | | | CI run URL; `.github/workflows/ci.yml` |
| IQ-15 | Health | `/health` (or equivalent) returns healthy; app starts and validates config (e.g., Westgard limits) at startup | | | startup log |

---

# Part 2 — Operational Qualification (OQ)

**Objective:** demonstrate each function operates per specification across positive and
negative paths. The automated suites are the primary OQ evidence engine; manual scripts below
are provided for witnessed execution.

## OQ Evidence Engine (automated suites)

| Suite (project) | Coverage | Cited for |
| --------------- | -------- | --------- |
| `NT.QAMS.Domain.UnitTests` | Aggregate invariants: NC/CAPA, documents, analytical studies, QC/Westgard, SoD, policy, access review, acknowledgement, controlled copy | Domain-rule OQ |
| `NT.QAMS.Application.UnitTests` | CQRS handlers/policies: password rules, provisioning, escalation, export, PT→NC, finding→NC, notifications | Application-logic OQ |
| `NT.QAMS.IntegrationTests` | **Real PostgreSQL**: RLS isolation/fail-closed/bypass/WITH-CHECK; signed-record UPDATE/DELETE rejection | High-risk DB-control OQ |
| `NT.QAMS.WebApi.FunctionalTests` | End-to-end API: auth/actor, bulk import, child persistence, LOV seeding, field-change reason | API-behavior OQ |
| `NT.QAMS.Architecture.Tests` | Clean-Architecture layer/module boundaries (merge gate) | Design-integrity control |
| Frontend (Karma/Jasmine, ~37 tests / 10 specs) + Playwright (`auth.spec.ts`, `regulated-workflow.spec.ts`) | UI unit + auth-gate/route-guard + tenant-scoped workflow e2e | UI OQ |

Execution: `dotnet test NT.QAMS.sln` (with `QMS_ITEST_POSTGRES` set for the integration
suite); `npm run test:ci`; `npx playwright test`.

## OQ Manual Test Cases (witnessed)

### Security & Access

| ID | Step | Expected | Actual | P/F | Evidence |
| -- | ---- | -------- | ------ | --- | -------- |
| OQ-SEC-01 | Login with valid tenant/email/password | 200 + JWT issued | | | screenshot / response |
| OQ-SEC-02 | Login with wrong password | 401 (not 500); attempt counter incremented | | | response; `audit` security event |
| OQ-SEC-03 | 5 consecutive failed logins | Account locked ~30 min; lockout event logged | | | security-event ledger |
| OQ-SEC-04 | Weak password on register/reset/change | Rejected by `PasswordRules.StrongPassword()` (≥12, complexity, blocklist) | | | response |
| OQ-SEC-05 | Enroll + confirm TOTP MFA; enable per-tenant policy | MFA required at next login for privileged role | | | `/api/auth/mfa/*`; `/api/tenant-settings/mfa-policy` |
| OQ-SEC-06 | Analyst calls `GET /api/compliance/*` | 403 (role-gated) | | | response |
| OQ-SEC-07 | TenantAdmin/QM calls `GET /api/compliance/*` | 200 | | | response |
| OQ-SEC-08 | Deactivate a user, reuse its still-valid token | Immediate 401 (AUTH-006) via `ActiveSessionMiddleware` | | | response |
| OQ-SEC-09 | Token role ≠ current DB role | 401 (AUTH-007) | | | response |
| OQ-SEC-10 | Open + complete a user-access review with conclusion | Snapshot of active accounts recorded; re-complete → 409 | | | `/api/access-reviews` |

### Tenant Isolation (high risk)

| ID | Step | Expected | Actual | P/F | Evidence |
| -- | ---- | -------- | ------ | --- | -------- |
| OQ-ISO-01 | As tenant A, query records seeded for tenant A | Only tenant-A rows returned | | | `RlsTenantIsolationTests` + manual |
| OQ-ISO-02 | Attempt to read tenant-B rows (as A) | Zero rows (RLS hides) | | | integration test |
| OQ-ISO-03 | Connection with no tenant GUC set | Fail-closed: no tenant rows visible | | | integration test |
| OQ-ISO-04 | Insert with mismatched tenant (WITH CHECK) | Rejected | | | integration test |

### Audit Trail & Data Integrity (high risk)

| ID | Step | Expected | Actual | P/F | Evidence |
| -- | ---- | -------- | ------ | --- | -------- |
| OQ-AUD-01 | Create/modify/delete a record | Contemporaneous `FieldChangeRecord` rows (actor, action, old/new, timestamp) written in same txn | | | `FieldChangeInterceptorTests`; `audit.field_change` |
| OQ-AUD-02 | Call `GET /api/compliance/chain-verification` | Chain reported intact | | | response |
| OQ-AUD-03 | Attempt raw UPDATE/DELETE on `audit.*` as `qams_app` | Denied (no grant) | | | psql error |
| OQ-AUD-04 | DELETE without `X-Change-Reason` header | 400 CHANGE-REASON-REQUIRED | | | response |
| OQ-AUD-05 | DELETE with reason | 204; `field_change.reason` populated | | | ledger row |
| OQ-AUD-06 | Export audit trail (XLSX) | File includes rows, Reason column, Integrity Attestation sheet (live chain verify) | | | `/api/exports` audit-trail.xlsx |
| OQ-AUD-07 | Record + complete an audit-trail review | Reviewer, period, conclusion recorded; immutable | | | `/api/compliance/audit-trail-reviews` |
| OQ-AUD-08 | Inspect a credential-bearing change | Sensitive field redacted in ledger | | | ledger row |

### Electronic Signatures (high risk)

| ID | Step | Expected | Actual | P/F | Evidence |
| -- | ---- | -------- | ------ | --- | -------- |
| OQ-SIG-01 | Sign with correct password + PIN | SignatureRecord created (signer, meaning, subject, content hash, timestamp) | | | `signatures.xlsx`; ledger |
| OQ-SIG-02 | Sign with wrong password | SIG-002; ESIGN_FAILED logged; no signature | | | response; security event |
| OQ-SIG-03 | Sign with wrong/missing PIN | SIG-001; ESIGN_FAILED logged | | | response |
| OQ-SIG-04 | Repeated failed signings | Account locked; SIG-003; ESIGN_LOCKED logged | | | security event |
| OQ-SIG-05 | Verify signature binds to content | Content hash matches signed payload; meaning captured | | | ledger |

### Signed-Record / Analytical Immutability (high risk)

| ID | Step | Expected | Actual | P/F | Evidence |
| -- | ---- | -------- | ------ | --- | -------- |
| OQ-IMM-01 | Raw UPDATE of a SignedOff analytical root | Rejected by `reject_frozen_mutation()` trigger | | | `SignedRecordImmutabilityTests`; psql |
| OQ-IMM-02 | Raw DELETE of a signed row | Rejected | | | integration test |
| OQ-IMM-03 | Transition INTO signed state | Allowed (trigger permits the sign transition) | | | integration test |
| OQ-IMM-04 | Approved uncertainty budget mutation | Rejected | | | psql |

### Separation of Duties (high risk)

| ID | Step | Expected | Actual | P/F | Evidence |
| -- | ---- | -------- | ------ | --- | -------- |
| OQ-SOD-01 | Raiser attempts to verify own NC | Rejected SOD-CAPA-002 | | | `NonconformanceTests`; API |
| OQ-SOD-02 | Preparer attempts to sign own analytical study | Rejected SOD-AQ-001 (`EnsureSignerIsNotPreparer`) | | | `AnalyticalSodTests` |
| OQ-SOD-03 | Author attempts to approve own quality policy | Rejected SOD-QP-001 | | | `QualityPolicyTests`; API |

### Document Control

| ID | Step | Expected | Actual | P/F | Evidence |
| -- | ---- | -------- | ------ | --- | -------- |
| OQ-DOC-01 | Draft → submit → recommend → publish (role-gated) | State transitions succeed only for authorized roles; publish e-signed | | | `/api/documents/*` |
| OQ-DOC-02 | Non-privileged user attempts publish | 403 | | | response |
| OQ-DOC-03 | Create new version | History preserved; current version is effective | | | `/api/documents/{id}/versions` |
| OQ-DOC-04 | Acknowledge published document (idempotent) | Ack recorded pinned to version; QM coverage view lists it | | | `/api/documents/{id}/acknowledge` |
| OQ-DOC-05 | Issue + close controlled copy | Copy pinned to version; Close(Returned/Destroyed) one-shot (CCP-010) | | | `/api/documents/{id}/controlled-copies` |

### NC / CAPA & Quality Events

| ID | Step | Expected | Actual | P/F | Evidence |
| -- | ---- | -------- | ------ | --- | -------- |
| OQ-NC-01 | Raise NC with event type (Deviation/OOS/OOT/Nonconformity) | Type persisted; filterable | | | `/api/nonconformances` |
| OQ-NC-02 | Full lifecycle raise→triage→rca→actions→complete→verify→confirm | Each transition role-gated and recorded | | | API |
| OQ-NC-03 | Triage/verify by non-QM/Admin | 403 | | | response |

### Analytical Quality / QC

| ID | Step | Expected | Actual | P/F | Evidence |
| -- | ---- | -------- | ------ | --- | -------- |
| OQ-QC-01 | Record a QC run | Westgard multi-rule verdict produced (1-3s/2-2s/R-4s/10-x/1-2s) | | | `WestgardEvaluatorTests`; `/api/qc` |
| OQ-QC-02 | Update QC target without reason | Rejected QC-012 | | | response |
| OQ-QC-03 | Update QC target backwards in time | Rejected QC-013 (forward-only) | | | response |
| OQ-QC-04 | Update QC target with reason, effective date | Accepted; historical verdicts unchanged (frozen z-score) | | | `QcProfileTests` |
| OQ-QC-05 | Configure Westgard limits via config | Validated at startup; labels derived; default output identical | | | startup; `WestgardEvaluatorTests` |
| OQ-QC-06 | Method-validation study calculate + sign-off | Result computed; sign-off e-signed and SoD-guarded | | | study controllers; `*StudyTests` |
| OQ-QC-07 | Record PT result unsatisfactory | Escalation / NC linkage available | | | `PtToNcPolicyTests` |

### Records & Retention

| ID | Step | Expected | Actual | P/F | Evidence |
| -- | ---- | -------- | ------ | --- | -------- |
| OQ-REC-01 | Attempt hard-delete of a regulated record via runtime role | Not possible (no DELETE grant) | | | grant matrix |
| OQ-REC-02 | Archive without snapshot file id | Rejected ARC-002 | | | `/api/archives` |
| OQ-REC-03 | Place legal hold, attempt disposal | Disposal blocked ARC-015 regardless of retention | | | API; `RecordsAndSlaTests` |
| OQ-REC-04 | Release legal hold (DELETE) without reason | 400 (ChangeReasonMiddleware) | | | response |

### Governance

| ID | Step | Expected | Actual | P/F | Evidence |
| -- | ---- | -------- | ------ | --- | -------- |
| OQ-GOV-01 | Change request propose→approve→close→post-implementation review | States progress to Reviewed; PIR notes required | | | `/api/changes` |
| OQ-GOV-02 | Approve quality policy as author | Rejected SOD-QP-001; approving supersedes prior active | | | `/api/quality-policy` |
| OQ-GOV-03 | Create supplier / management review / risk item | Recorded | | | governance controllers |

---

# Part 3 — Performance Qualification (PQ)

**Objective:** demonstrate the system supports the intended business workflows end-to-end in
the production-equivalent environment, by trained users, over representative data.

## PQ-01 — Quality Event to CAPA Closure (raise → investigate → CAPA → close)

| Step | Action | Expected | Actual | P/F | Evidence |
| ---- | ------ | -------- | ------ | --- | -------- |
| 1 | Analyst raises an OOS quality event | Event created with type OutOfSpecification, audit-trailed | | | |
| 2 | Analyst submits for triage | State → submitted | | | |
| 3 | QM triages / accepts | State advances; role-gated | | | |
| 4 | Investigator records root-cause analysis | RCA stored | | | |
| 5 | Assign + complete corrective actions | Actions tracked to completion | | | |
| 6 | Verification submitted by preparer | State → verification | | | |
| 7 | **Different** QM verifies (SoD) | Passes; raiser could not self-verify (SOD-CAPA-002) | | | |
| 8 | Confirm effectiveness; close | Closed; full trail intact; chain verifies | | | |

## PQ-02 — Controlled Document Lifecycle (draft → review → publish → acknowledge)

| Step | Action | Expected | Actual | P/F | Evidence |
| ---- | ------ | -------- | ------ | --- | -------- |
| 1 | Author creates draft SOP | Draft created | | | |
| 2 | Submit for review | State → submitted | | | |
| 3 | Dept head recommends | State → recommended (role-gated) | | | |
| 4 | QM publishes with e-signature (pw + PIN) | Published; SignatureRecord bound to content | | | |
| 5 | Users acknowledge (read & understand) | Acks recorded, pinned to version; QM coverage complete | | | |
| 6 | Issue a controlled printed copy | Copy pinned to version in register | | | |
| 7 | Publish a new version | Prior version superseded; acknowledgement re-opened | | | |

## PQ-03 — QC Run to Sign-off (QC run → Westgard verdict → sign-off)

| Step | Action | Expected | Actual | P/F | Evidence |
| ---- | ------ | -------- | ------ | --- | -------- |
| 1 | Configure QC profile targets (mean/SD) | Targets effective-dated | | | |
| 2 | Record QC run values | Run stored with frozen z-score | | | |
| 3 | System evaluates Westgard rules | Accept/reject verdict produced and recorded | | | |
| 4 | On reject, record troubleshooting | Troubleshooting captured | | | |
| 5 | QM reviews and (where applicable) signs off | E-signed; SoD enforced | | | |
| 6 | Later, change target with reason (forward-only) | Historical verdicts unchanged | | | |

## PQ-04 — Backup & Restore Drill (per `deploy/BACKUP-RESTORE-DR.md`)

| Step | Action | Expected | Actual | P/F | Evidence |
| ---- | ------ | -------- | ------ | --- | -------- |
| 1 | Trigger nightly logical dump + confirm WAL archiving | Dump created; WAL shipping ≤ 5 min | | | |
| 2 | Restore to isolated environment (PITR to a chosen point) | Restore completes within RTO ≤ 4 h | | | |
| 3 | Verify record + file-snapshot consistency | DB and object storage match; no orphan snapshots | | | |
| 4 | Verify audit chain post-restore | `chain-verification` intact | | | |
| 5 | Document RPO/RTO achieved | Meets targets | | | |
