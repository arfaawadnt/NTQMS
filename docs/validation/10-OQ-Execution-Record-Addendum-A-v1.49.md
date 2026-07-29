# OQ Execution Record — Addendum A (remaining cases)

| Field | Value |
| ----- | ----- |
| Document ID | OQ-EXEC-NTQMS-001-A |
| Parent record | [`09-OQ-Execution-Record-v1.49.md`](09-OQ-Execution-Record-v1.49.md) (OQ-EXEC-NTQMS-001) |
| Protocol | REVAL-NTQMS-001 (doc 06) — cases listed "not executed" in the parent record §2 |
| System / version | NT.QMS **v1.49.0**, commit `1beb3bf` |
| Environment | Development workstation; API instances started per case (`:5080` dev, `:5096` Staging, `:5097` Production, `:5098` DB-unreachable), PostgreSQL 17 local |
| Executed by (operator) | Engineering (Claude Code), at the System Owner's direction |
| Witnessed by | A. Awad — System Owner / acting QA authority (same live session) |
| Date of execution | 2026-07-29, 07:56–08:10 local |
| Outcome | **6 cases closed, 1 case partially closed, 1 NEW FINDING raised (OPS-010), 4 items remain environment-blocked** |

> Same scope limits as the parent record apply: development environment, limited
> independence, no signature applied by Engineering. **This addendum raises a genuine
> product finding (§3) — it is not a clean sheet.**

---

## 1. Cases executed and closed

### OQ-DEP-01 / IQ-17 — Production refuses to boot on an over-privileged DB role

Started the API with `ASPNETCORE_ENVIRONMENT=Production` against the dev role `qams_app`
(which owns the dev schema, i.e. deliberately over-privileged).

**Actual observed** — process refused to start:

> `System.InvalidOperationException: Refusing to start with an over-privileged database role: connection role 'qams_app' owns (or inherits ownership of) 92 application table(s) — an owner can drop the RLS policies and immutability triggers. Run the application as the least-privilege runtime role (qams_app) — provision it with deploy/harden-runtime-role.sql (migrations run separately as qams_owner).`
> at `DatabaseRoleGuard.EnsureLeastPrivilegeAsync` (`DatabaseRoleGuard.cs:94`)

**P/F: Pass.** The guard names the violation, the risk (RLS/immutability could be dropped),
and the remediation script. Non-production behaviour also confirmed: the same condition
logs a warning instead of blocking (`Database role guard: … Production will refuse to start`).

### OQ-MSG-02 — Poison outbox event dead-letters without head-of-line blocking

Executed `scripts/failure-drills.ps1` Drill 2 against the live API.

**Actual observed:**
```
Drill 2 - poison outbox event
  injected poison row 25071ba2-eef5-4533-b3a8-8e3e535a1f34 (attempts=4, due now)
  PASS  poison event moved to the dead-letter state
  PASS  it stopped at MaxAttempts (5)
  cleaned up drill row
```
**P/F: Pass.** Drill self-cleaned; no residual test data.

### OQ-SEC-14 — Refresh-token reuse revokes the whole family (ADR-0009)

Executed `scripts/security-probe-deep.ps1` against the live API (9 adversarial checks).

**Actual observed:**
```
[I] Refresh-token reuse detection (ADR-0009)
  PASS  a valid refresh rotates to a new token (200)
  PASS  replaying the rotated (stale) token is rejected (401)
  PASS  reuse revoked the WHOLE family (successor also rejected: 401)
[J] PASS tenant B cannot read tenant A's record by id (404, RLS fail-closed)
[K] PASS refresh with no cookie is 401, not 500
[L] PASS no Access-Control-Allow-Origin reflected for a foreign Origin
[M] PASS CSP + nosniff present on the 403 response
[N] PASS TRACE does not echo request headers (no XST)
SUMMARY: 9 passed, 0 failed
```
**P/F: Pass** (also re-confirms cross-tenant isolation, CORS, and XST).

### OQ-SEC-15 (full matrix) — All roles × gated surface, executed

Ran the automated matrix suites against real PostgreSQL. **Individual cases executed:**

| Test | Result |
| ---- | ------ |
| `RoleEndpointMatrixTests.Every_role_against_every_gated_endpoint_is_2xx_404_or_problem_json_403` | **Passed** (7 s) |
| `RoleEndpointMatrixTests.The_read_only_auditor_and_analyst_are_denied_the_admin_surface` | **Passed** (856 ms) |
| `AuditorDenyMatrixTests.The_auditor_reads_the_quality_ledger_but_every_write_is_403` | **Passed** (4 s) |

**P/F: Pass.** The full 6-role matrix is now executed evidence, superseding the parent
record's single representative role pair.

### OBS-01 resolved — the `xmin` concurrency path, executed

The parent record noted its manual concurrency case produced a state-machine 409 (`NC-010`)
rather than the `xmin` `CONCURRENCY-409`. Both specific paths have now been executed:

| Test | Result |
| ---- | ------ |
| `OptimisticConcurrencyTests.Two_racing_edits_exactly_one_wins_and_the_loser_conflicts` | **Passed** (3 s) |
| `ConcurrencyConflictMappingTests.Concurrency_exception_maps_to_409_with_the_stable_code` | **Passed** (128 ms) |

**Disposition: OBS-01 closed.**

### IQ-18 (application leg) — HSTS emitted outside Development

Started the API with `ASPNETCORE_ENVIRONMENT=Staging` and inspected response headers.

**Actual observed on `GET /health/live` (200):**
```
Content-Security-Policy: default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: no-referrer
Strict-Transport-Security: max-age=63072000; includeSubDomains
```
**P/F: Pass (application leg only).** Confirms `SecurityHeadersMiddleware` emits the SEC-012
HSTS commitment (2 years, includeSubDomains) in every non-Development environment, and its
absence in Development is by design. **Still open:** the deployment leg — TLS actually
terminating at the reverse proxy with a valid certificate (ADR-0002) — requires the
qualified host.

---

## 2. OQ-DEP-02 — partially closed

The case reads: *"Stop PostgreSQL; hit `/health/ready`; restart."*

**Runtime-outage leg — PASS (automated, executed this session):**

| Test | Result |
| ---- | ------ |
| `HealthEndpointTests.Readiness_returns_service_unavailable_when_the_database_is_down` | **Passed** (3 s) |
| `HealthEndpointTests.Liveness_is_healthy_even_with_the_database_down` | **Passed** (7 ms) |
| `HealthEndpointTests.Legacy_health_alias_still_answers_as_liveness` | **Passed** (6 ms) |
| `ReadinessAndTopologyTests.Readiness_is_unhealthy_when_postgres_is_unreachable` | **Passed** (4 s) |
| `ReadinessAndTopologyTests.Readiness_is_healthy_when_postgres_answers` | **Passed** (44 ms) |
| `ReadinessAndTopologyTests.Singleton_advisory_lock_is_contended_for_a_second_instance` (OQ-DEP-03) | **Passed** (57 ms) |

**Live service-stop leg — NOT EXECUTED.** Stopping the Windows service
`postgresql-x64-17` was **denied by the operator sandbox** (and `failure-drills.ps1` Drill 1
reported the same: *"stopping 'postgresql-x64-17' was denied (needs an elevated shell)"*).
No workaround was attempted. QA must execute this leg on the qualified environment with
appropriate rights.

**Cold-start leg — FAILED. See finding OPS-010 below.**

---

## 3. NEW FINDING — OPS-010: cold start with the database unreachable crashes instead of reporting unready

| Attribute | Value |
| --------- | ----- |
| ID | **OPS-010** |
| Discovered | During OQ-DEP-02 execution, 2026-07-29 (this session) |
| Severity | **Medium** (operational robustness / availability) |
| Priority | P2 |
| Production blocker | **No** — see risk assessment |
| Status | **OPEN** — raised, not fixed |

**What was executed.** An API instance was started with the connection string pointed at an
unreachable PostgreSQL (`Port=5433`, nothing listening), `MigrateOnStartup=false`, to observe
readiness behaviour on a cold start.

**Expected** (per the OPS-008 readiness design intent): the process starts, `/health/live`
returns 200, `/health/ready` returns 503 until PostgreSQL answers.

**Actual observed:** the process **never began listening** and terminated with an unhandled
`NpgsqlException` — `Failed to connect to 127.0.0.1:5433`. Both `/health/live` and
`/health/ready` were unreachable (curl `000`). Two distinct DB-dependent startup steps run
**before** the host starts serving:

1. `Program.cs:247` — platform-admin bootstrap (`db.Users.AnyAsync(...)`)
2. `Program.cs:264` — starter list-of-values backfill (`db.Tenants.Select(...).ToListAsync()`)

Reproduced twice: once with bootstrap seeding configured (crash at line 247), and again with
bootstrap disabled (crash at line 264), confirming both are unconditional startup DB reads.
The role guard's own DB call is correctly defensive — it catches `NpgsqlException` and logs
*"could not verify the connection role's privileges (database unreachable)"* — so the guard is
**not** the cause.

**Risk assessment (honest):**
- **Not a data-integrity or security issue.** No regulated record, audit entry, or access
  control is affected. Nothing is silently mis-persisted.
- **It is an availability/robustness gap.** During a database outage the application cannot
  start to report itself unready; an orchestrator restart (or any deploy/reboot coinciding
  with a DB outage) yields a crash-loop instead of a running-but-unready instance. That
  weakens the value of the readiness probe precisely when it matters, and can make an outage
  harder to diagnose (no `/health/ready` to interrogate, only restart logs).
- **Partially mitigated in practice:** Npgsql retry-on-failure (OPS-009) covers transient
  blips *after* startup, and a crash-loop is a recognised (if blunt) failure mode that
  orchestrators surface. The previously executed Drill 1 / automated tests cover the
  *runtime* outage path, which is the more common scenario.
- **Why the prior evidence missed it:** all earlier readiness evidence exercised a database
  that went down **after** startup. Cold-start-with-DB-down was never executed until now.
  This is exactly the class of gap witnessed OQ execution exists to find.

**Recommended remediation** (for QA/engineering triage, not applied here):
make the startup seeding/backfill resilient — wrap both blocks so a database-connectivity
failure is logged and deferred rather than fatal (or move them behind a hosted service that
runs after the host is listening, leaving `/health/ready` to report the DB state). Then
re-execute the cold-start leg of OQ-DEP-02 as a regression check.

**Impact on the audit record:** the enterprise audit's OPS/§20 claim that readiness returns
503 when PostgreSQL is down is **true for a runtime outage** and **not true for a cold
start**. That distinction should be reflected rather than left implicit.

---

## 4. Items still environment-blocked (cannot be closed on a development workstation)

| Ref | Item | Blocker |
| --- | ---- | ------- |
| OQ-DEP-02 (service-stop leg) | Stop PostgreSQL service, observe 503, restart | Requires elevated rights; sandbox denied |
| IQ-18 (deployment leg) | TLS terminating at the reverse proxy with a valid certificate | Qualified host + proxy |
| IQ-19 | Deployed container runs non-root, evidence volume writable | No Docker on this host; **proven per-build in CI** (`container` job) |
| IQ-23 | Observability stack up, targets UP, alert rules loaded | Needs Docker / staging host |
| PQ-PERF-01/02, PQ-OBS-01 | Load ≥100 VU, 24 h soak, alert-fires drill | Staging + time; dev-box baseline is informational only |

---

## 5. Cumulative result across both records

| Metric | Parent record | This addendum | **Total** |
| ------ | ------------- | ------------- | --------- |
| OQ cases executed | 12 | 6 closed + 1 partial | **18 + 1 partial** |
| Passed | 12 | 6 (+ runtime leg of OQ-DEP-02) | **18** |
| Failed | 0 | 0 | **0** |
| **Findings raised** | 0 | **1 (OPS-010)** | **1** |
| Deviations | 1 (DEV-01) | 0 | 1 |
| Observations resolved | — | OBS-01 closed | — |
| Environment-blocked items | 7 | 5 remaining | **5** |

Automated suites executed this session as OQ evidence: **11 individual test cases, all
passed** (role matrix ×2, auditor deny matrix, health ×3, readiness/topology ×3,
concurrency ×2).

---

## 6. Signatures

| Role | Name | Signature | Date |
| ---- | ---- | --------- | ---- |
| Executed by (operator) | Engineering — Claude Code (automated operator) | *n/a — machine-executed; results transcribed verbatim* | 2026-07-29 |
| Witnessed by | A. Awad (System Owner / acting QA authority) | ____________________ | __________ |
| Reviewed & approved by (QA) | | ____________________ | __________ |

> **Finding OPS-010 must be dispositioned before this addendum supports a validation
> claim** — accepted with justification, or remediated and re-tested. Engineering applies no
> signature on QA's or the System Owner's behalf.
