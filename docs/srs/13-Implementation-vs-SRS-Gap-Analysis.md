# NT.QMS — Production Software Requirements Specification
## Document 13 · Current Implementation vs SRS — Gap Analysis

> [Conventions](00-SRS-Index-and-Conventions.md) · Traceability:
> [Document 12](12-Requirements-Traceability-Matrix.md) · Remediation:
> [Document 15](15-Recommendations.md)

---

# 13.1 Scope and method

**Compared:** `NT_QMS_SRS.html` — *"Software Requirements Specification (SRS), NT.QMS Quality
Management System (v1.0)"* — statement by statement against the source tree at **v1.51.x**.

**Method:** every assertion in the old SRS was located in the code or shown to be absent. Every
substantial capability found in the code was checked for a corresponding statement in the old SRS.

## Classification

| Class | Meaning | Count |
|---|---:|---|
| **A · Missing from SRS** | built, working, undocumented — the old SRS never mentions it | **41** |
| **B · Missing from Code** | the SRS promises it; it does not exist | **19** |
| **C · Extra Behaviour** | built and materially different from what the SRS describes | **14** |
| **D · Outdated Requirement** | the SRS describes a real thing that has since changed | **11** |
| **E · Deprecated Feature** | in the SRS, deliberately removed or never adopted | **9** |
| | **Total findings** | **94** |

## The headline

> The old SRS describes a **different, smaller and partly imaginary system**.
>
> It documents roughly **8 functional areas**; the built system has **34 modules and 329 endpoints**.
> It specifies a technology stack — Redis, Hangfire, Dapper, Mapster, ngx-translate, Tailwind,
> SignalR, Seq/Serilog — of which **none is present**. It specifies a privilege model
> (`~70 OBJECT.ACTION` codes) that was never built, in place of the 171-key `module.action` catalogue
> that was. It specifies an error contract that is not the one in use.
>
> **It cannot be used as an engineering baseline and should be formally superseded by this set.**

---

# 13.2 Class A — Missing from the SRS (built, undocumented)

These are working, tested capabilities the old SRS is silent about. Each is now specified in this set.

| # | Capability | Scale | Now in |
|---|---|---|---|
| A-01 | **Analytical-quality subsystem** — 12 statistical study types with full CLSI-style calculations | 24 screens, ~110 endpoints | [02-3](02-3-Functional-Specification-Analytical-Quality.md) |
| A-02 | Measurement-uncertainty budgeting (GUM root-sum-square, coverage factor) | 1 module | 02-3 §26.13 |
| A-03 | Sigma-metric assessment with grade bands and QC recommendations | 1 module | 02-3 §26.12 |
| A-04 | **PT plans** — annual plan, one per year, fulfilment tracking, closure summary | 1 module | 02-3 M-27 |
| A-05 | Customer feedback register with escalation to complaints | 1 module | 02-1 M-03 |
| A-06 | Quality-policy versioning with one-in-force enforcement | 1 module | 02-1 M-05 |
| A-07 | Quality objectives with evidence-checked closure | 1 module | 02-1 M-04 |
| A-08 | Change control with mandatory risk linkage and PIR | 1 module | 02-1 M-07 |
| A-09 | Management review with immutable minutes | 1 module | 02-1 M-08 |
| A-10 | Records archive with retention classes and **legal hold** | 1 module | 02-1 M-10 |
| A-11 | Reference-standards register with traceability chain and auto-expiry | 1 module | 02-2 M-12 |
| A-12 | Environmental monitoring with excursion→NC automation | 1 module | 02-2 M-13 |
| A-13 | Supplier quality with weighted evaluations and certificate-expiry suspension | 1 module | 02-2 M-14 |
| A-14 | Per-test authorisations evidenced by competency | 1 module | 02-2 M-17 |
| A-15 | Conflict-of-interest / impartiality register | 1 module | 02-2 M-21 |
| A-16 | Organisational context (interested parties + context issues) | 1 module | 02-2 M-22 |
| A-17 | **Periodic user-access review** | 1 module | 02-2 M-23 |
| A-18 | **Audit-trail review** process | part of CLD | 02-2 M-24 |
| A-19 | Document **read-and-understand acknowledgements**, version-pinned | part of DOC | 02-1 M-09 |
| A-20 | **Controlled printed-copy register** with one-shot closure | part of DOC | 02-1 M-09 |
| A-21 | Document periodic-review cycle with sweep-raised due events | part of DOC | 02-1 M-09 |
| A-22 | **Six automatic event→NC bridges** | 6 policies | [03 §3.9](03-Business-Rules.md) |
| A-23 | **Transactional outbox** with SKIP LOCKED claim leases, jittered backoff, dead-lettering, retention purge | infrastructure | [06 §6.4](06-Workflow-Specification.md) |
| A-24 | **Hourly compliance sweep** ageing eight aggregate types in one transaction | infrastructure | 06 §6.4 |
| A-25 | **6-hourly KPI snapshot** service with leader election | infrastructure | 02-4 M-30 |
| A-26 | **Single-replica sentinel** and per-job advisory-lock leader election | infrastructure | 10 §10.1 |
| A-27 | **Deferred start-up seeding** (OPS-010) — DB-down cold start does not crash | infrastructure | 06 §6.5 |
| A-28 | `DatabaseRoleGuard` — Production refuses to boot on an over-privileged role | security | 09 §9.5 |
| A-29 | **`xmin` optimistic concurrency** → 409 | reliability | ADR-0005 |
| A-30 | **`Idempotency-Key`** replay protection | reliability | 08 §8.7 |
| A-31 | **Reason-for-change enforcement** on every DELETE, stamped into the ledger | compliance | 03 §3.8 |
| A-32 | **Rotating refresh-token sessions** with family-revoking reuse detection | security | 09 §9.2 |
| A-33 | **Per-request session revocation** (`AUTH-006`/`AUTH-007`) | security | 09 §9.2 |
| A-34 | **Upload allow-list + content sniffing** with canonical type storage | security | 09 §9.9 |
| A-35 | **Four-partition rate limiting** including per-actor e-signature throttling | security | 09 §9.8 |
| A-36 | **Security headers** (CSP, nosniff, DENY, no-referrer, HSTS) | security | 09 §9.7 |
| A-37 | **Full observability stack** — JSON logs, correlation ids, end-to-end traces incl. outbox, 7 Prometheus instruments, 7 alert definitions | ops | 10 §10.7 |
| A-38 | **Health/readiness separation** with DB-independent liveness | ops | 10 §10.4 |
| A-39 | **Seven CI merge gates** (API surface, command policy, module boundary, layering, migration round-trip, audit tamper, role matrix) | quality | 10 §10.9 |
| A-40 | **Four XLSX/PDF exports** with a live integrity attestation and `RECORD_EXPORTED` logging | compliance | 02-4 M-36 |
| A-41 | **Branch/department organisational scoping** (`IAllocatable` + scope guard) | authorisation | 09 §9.4 |

---

# 13.3 Class B — Missing from the code (promised, absent)

The old SRS states these as requirements. **None exists.** Each is a decision the business must make:
drop the requirement, or schedule the work.

| # | SRS statement | Reality | Impact |
|---|---|---|---|
| **B-01** | *"MFA via TOTP for **all active accounts**"* (FR-AUTH-01, "must") | MFA is **optional per tenant, default OFF**; every tenant in the dev dataset has it off | **Compliance-relevant.** If the accreditation body was told MFA is mandatory, it is not. |
| **B-02** | *"Diagonal 'OBSOLETE — UNCONTROLLED' watermark on PDFs of older versions"* (FR-DOC-03) | **No PDF processing of any kind.** Files are stored and served byte-for-byte; an obsolete version downloads unmarked | **ISO 17025 §8.3.2 relevant** — obsolete documents must be clearly identified |
| **B-03** | *"Overdue equipment calibration … **block its selection**"* (FR-GOV-01, "must") | Status changes to `OutOfService`, but **nothing prevents the instrument being named** anywhere — instrument fields are free text, not foreign keys | **ISO 17025 §6.4.7 relevant** |
| **B-04** | *"Interactive 5-Whys text flows and Fishbone (Ishikawa) diagram inputs"* | `RcaMethod` enum + one free-text field | UX/usability |
| **B-05** | *"Renders questions based on selected ISO clauses (e.g. ISO 17025 Section 6.4)"* — checklist library | Checklists are typed in per audit; **no template store** | Efficiency |
| **B-06** | *"SOP Tree Directory: left sidebar pane rendering folder trees categorized by department"* | `category` is a flat string; no tree | UX |
| **B-07** | *"Active Session Monitor: user handles, login timestamps, device OS, IP addresses, revoke buttons"* | **Not built.** `qams.refresh_session` holds the data; nothing surfaces it | Security operations |
| **B-08** | *"Dynamic SVG line/bar chart displaying NCR trends over the last 12 months"* | KPI history data exists; **no chart component** | UX |
| **B-09** | *"Levey-Jennings SVG plot"*, *"Passing-Bablok plots, Bland-Altman differences, histograms"* | Statistics are **computed and stored**; **no chart renders them** | **Significant** — analytical review is much harder without plots |
| **B-10** | *"LIS connection loopback fetches"* for data import | **No instrument or LIS interface exists** | Scope |
| **B-11** | *"Data Entry Tabs supporting direct manual entry, CSV uploads, or LIS"* | CSV import exists on **2 of 12** studies | Partial |
| **B-12** | *"Redis distributed cache … cache hit ratio ≥ 85 %"* (NFR-PERF-02) | **No Redis.** No distributed cache at all | The NFR is unmeasurable |
| **B-13** | *"Hangfire background processing"*, *"daily Hangfire jobs"* | `IHostedService` background services | Different, not worse |
| **B-14** | *"Dapper for fast report queries … < 500 ms"* | **No Dapper.** All reads are EF Core with `AsNoTracking` | The NFR is unmeasurable |
| **B-15** | *"Mapster"* | Hand-written projections | Different |
| **B-16** | *"ngx-translate"*, *"Tailwind CSS"* | In-app typed i18n service; hand-written CSS with design tokens | Different |
| **B-17** | *"SignalR"* real-time | **No push of any kind** | Scope |
| **B-18** | *"Seq/Serilog structured logging"* | `Microsoft.Extensions.Logging` JSON console + OpenTelemetry | Different |
| **B-19** | *"Hash Routing"* in the Angular client | Path routing | Different |

---

# 13.4 Class C — Extra behaviour (built differently)

The capability exists but **materially differs** from the SRS's description. These are the most
dangerous entries: a reader of the old SRS would build the wrong thing.

| # | SRS says | Code does | Why it matters |
|---|---|---|---|
| **C-01** | *"~70 atomic privileges structured in an **OBJECT.ACTION** format"* — `NCR.CREATE`, `DOC.APPROVE`, `LAB.CONFIG`, `EQUIP.CALIB_SCHED` | **171 keys** in `{module}.{action}` **lower case** — `nc.create`, `documents.approve`. None of the SRS's example codes exists | **Every privilege identifier in the old SRS is wrong.** |
| **C-02** | Roles: *SysAdmin, QualityManager, LabDirector, TechManager, Analyst, EquipmentOwner* | **PlatformAdmin, TenantAdmin, QualityManager, DepartmentHead, Analyst, ExternalAuditor** | Four of six role names differ; `ExternalAuditor` (read-only by construction) is absent from the SRS |
| **C-03** | Error model `{success, errorCode, message, errors[], timestamp}` | **RFC 7807 `application/problem+json`** `{type, title, status, code, traceId, correlationId}` | Any client written to the SRS breaks |
| **C-04** | *"Overdue actions escalate: +24h Owner, **+48h Dept Head**, +72h Quality Manager"* | +24 h owner, **+48 h Quality Manager**, +72 h Quality Manager | **The department head is never notified.** |
| **C-05** | *"…and updates are locked"* at T+72 h | The ladder **stops** at level 3; **nothing is locked** | |
| **C-06** | NCR state model: *Draft → Raised → Assigned → RCA → Action Plan → Pending Verification → Effectiveness Check → Closed* | **Identical** — plus `Rejected`, plus two loop-backs the SRS's table hints at but its diagram omits | Rare accurate match |
| **C-07** | *"Competency authorization requires score ≥ 80 % **and a 4-digit signature PIN**"* | Score ≥ 80 ✅; **no PIN is required at that endpoint** | Part 11 signing is not applied where the SRS says it is |
| **C-08** | *"Manager … clicks 'Approve & Sign'. Enters 4-digit signature PIN"* for documents | Publish requires the `documents.sign` **permission**; the PIN ceremony is invoked by `ESignatureService` where wired, not universally at publish | **`[Needs Business Confirmation]`** — which gates must demand a full e-signature ceremony? |
| **C-09** | *"TLS 1.3"* required | TLS terminates at the proxy; the app is version-agnostic and emits HSTS | Deployment concern, not app |
| **C-10** | *"Data at rest must use AES-256 encryption"* | **No application-level encryption at rest.** Relies on the database/volume | **`[Needs Business Confirmation]`** — is disk-level encryption in place? |
| **C-11** | *"95 % of API requests < 200 ms"* | Measured **p95 86–105 ms** — better than the target, but on a different definition and only for reads | Target met, differently |
| **C-12** | RLS example `CREATE POLICY tenant_rls_policy ON "QamsEntities" FOR ALL USING ("TenantId" = current_setting(...))` | 90 tables, **snake_case**, `ENABLE + FORCE`, with a bypass clause and `WITH CHECK` | The SRS shows a weaker policy without `FORCE` or `WITH CHECK` |
| **C-13** | `set_config('app.current_tenant', tenant_id, false)` | Correct — **plus** `app.bypass_rls`, plus fail-closed to nil, plus a controlled elevation path | Understated |
| **C-14** | *"Angular 18 Standalone"* | **Angular 22.0.8**, TypeScript 6.0.3 | Version drift — and the **repository's own docs still say 18** |

---

# 13.5 Class D — Outdated requirements (true once, changed since)

| # | Statement | Change | Where |
|---|---|---|---|
| **D-01** | Angular 18 | → **22.0.8** (forced by 10 high-severity advisories fixable only by semver-major; upgraded one major at a time) | 01 §2.4 |
| **D-02** | `[Authorize(Roles = …)]` role gate | → **`[RequirePermission(module, action)]`**; exactly **one** role attribute remains (`PlatformAdmin`) | 08 §8.2 |
| **D-03** | `Roles.QmOrAdmin` / `QmDeptAdmin` / `QmAdminAuditor` / `TenantAdminOnly` groups | **dead code** — referenced only as *test labels* | 08 §8.2 |
| **D-04** | ADR-0003 token storage | **superseded by ADR-0009** | 09 §9.2 |
| **D-05** | 60-minute access tokens (Phase 3) | shipped **120 minutes**, while **ADR-0009 specifies 15** | CFG-07 |
| **D-06** | *"~270 backend tests"* (ONBOARDING/CLAUDE.md) | **436** backend, 74 frontend, 6 e2e | 10 §10.9 |
| **D-07** | *"93 tables / 49 migrations"* | **97 tables / 56 migrations / 90 FORCE-RLS** | 01 §0.5 |
| **D-08** | `deploy/migrations.sql` current | **stale — covers migrations 1–10 of 56** while DEPLOY.md says to re-run it | 10 §10.3 |
| **D-09** | Repository docs describe the design-time architecture as "law" | The as-built has moved on in the authorisation model and the SPA version | 11 |
| **D-10** | CLAUDE.md §3 "code at tag v1.46.0" | Code is at **v1.51.x** | — |
| **D-11** | Enterprise-audit figures (~76 %, ~88 %, ≈98 %) | Successive re-audits; the current figure is a moving target and none is a certification | — |

---

# 13.6 Class E — Deprecated / never adopted

| # | Item | Status |
|---|---|---|
| **E-01** | Redis distributed cache | **Never adopted.** No caching layer exists |
| **E-02** | Hangfire | Never adopted — `IHostedService` instead |
| **E-03** | Dapper | Never adopted |
| **E-04** | Mapster | Never adopted |
| **E-05** | ngx-translate | Never adopted |
| **E-06** | Tailwind CSS | Never adopted |
| **E-07** | SignalR | Never adopted |
| **E-08** | Parallel **CSV export** endpoints | **Built and deliberately reverted** — XLSX is the single tabular format. Do not reintroduce |
| **E-09** | ADR-0003 (access-token storage risk acceptance) | **Formally superseded** by ADR-0009 |

---

# 13.7 Findings that are defects, not documentation gaps

Separated out because they need engineering work, not a document update.

| # | Defect | Severity | Evidence |
|---|---|---|---|
| **G-01** | **`audit.security_event` has no RLS policy.** Both RLS migrations iterate `pg_policies` and skipped a table that had none. Its store reads are not tenant-filtered | **High** | [09 T-01](09-Security-Specification.md) |
| **G-02** | **Complaint closure interlock is unsatisfiable when the linked NC is *rejected*.** `CMP-020` requires the NC to be `Closed`; a rejected NC is terminal but not closed ⇒ the complaint can never be closed | **Medium** | [02-1 M-02](02-1-Functional-Specification-Quality-and-Improvement.md) |
| **G-03** | **Suspended suppliers cannot be reinstated.** `Suspended` is terminal; a supplier auto-suspended for an expired certificate cannot be restored after renewal | **Medium** | 02-2 M-14 |
| **G-04** | **No maximum upload size** in application code | **Medium** | 09 T-10 |
| **G-05** | **`take` / `days` query parameters are unbounded** (only `pageSize` is clamped) | **Medium** | 08 LIM-API-03 |
| **G-06** | **Tenant suspend/reactivate/terminate have no endpoint** — lifecycle management is database-only | **Medium** | 02-4 M-33 |
| **G-07** | **`QcProfile.Deactivate()` is unreachable** — no command, no endpoint, no UI | **Low** | 02-3 LIM-QC-01 |
| **G-08** | **Test-authorisation scope is never enforced** — a `Perform`-only holder is not prevented from reviewing and releasing | **Medium** | 02-2 FR-AUTHZ-02 |
| **G-09** | **Environmental excursions have no de-bounce** — one NC and one e-mail per recipient per out-of-limit reading | **Medium** | 02-2 M-13 |
| **G-10** | **E-mail failures are never retried** — a transient SMTP outage silently loses the channel | **Medium** | 02-4 M-29 |
| **G-11** | **Silent degradation to log-only e-mail** when `Smtp:Host` is unset — no operator warning | **Medium** | 02-4 BR-NTF-05 |
| **G-12** | **Management-review decisions do not become tasks** — owner and due date are recorded but never chased | **Medium** | 02-1 M-08 |
| **G-13** | **`SlaDefinition` and `EscalationTimer` are not wired together** — SLA targets are stored but do not drive escalation deadlines | **Medium** | 02-4 M-28 |
| **G-14** | **`Jwt:ExpiryMinutes` = 120 contradicts ADR-0009's 15** | **Low–Medium** | CFG-07 |
| **G-15** | **`deploy/migrations.sql` is stale** while the runbook instructs re-running it | **Medium** | 10 §10.3 |
| **G-16** | **No purge for expired `refresh_session` rows**; no partitioning or archival for the two append-only ledgers | **Low (growing)** | 07 §7.6 |
| **G-17** | **`ref` schema is granted in `harden-runtime-role.sql` and designed in the docs but was never created** | **Low** | 07 §7.2 |
| **G-18** | **Training completion does not feed competency** | **Low** | 02-2 M-16 |
| **G-19** | **PT "Questionable" results trigger nothing** | **Low–Medium** | 02-3 M-27 |
| **G-20** | **SoD is a no-op when `CreatedByUserId` is null** (legacy and system-raised records) — accepted residual F-05b, but still a real bypass | **Medium** | 03 §3.4 |

---

# 13.8 Where the old SRS was right

Recorded for fairness, and because these are the parts worth carrying forward.

| Statement | Verdict |
|---|---|
| The NCR/CAPA state model (8 named states, loop-backs on failed verification and effectiveness) | ✅ **accurate** |
| The document lifecycle Draft → Review → Approve → Published → Obsolete | ✅ **accurate** |
| SoD rules: NCR closure, document review/approve, competency self-authorisation, supplier self-approval | ✅ **all four implemented** (plus five more the SRS does not list) |
| Account lockout: 30 minutes after 5 failed attempts | ✅ **exact match** |
| Competency pass mark ≥ 80 % | ✅ **exact match** |
| PostgreSQL RLS keyed on `app.current_tenant` via `set_config` | ✅ **correct in principle**, understated in strength |
| Append-only tamper-evident audit trail, runner without write permission | ✅ **implemented and exceeded** (hash chain + immutability triggers + role guard) |
| Three languages (en/ar/fr) with dynamic direction | ✅ **accurate** |
| Clean layered architecture with CQRS over MediatR and FluentValidation | ✅ **accurate** |
| .NET 9 + PostgreSQL 17 + EF Core 9 + Npgsql | ✅ **accurate** |
| ISO 17025 / 9001 / 21 CFR Part 11 / Annex 11 as the regulatory frame | ✅ **accurate** |
| UAT-SEC-01 multi-tenancy isolation acceptance test | ✅ **implemented and verified** |
| UAT-SOD-01 SoD acceptance test | ⚠ correct in intent; the actual status is **422**, not the 403 the SRS specifies |
| UAT-DOC-01 obsolete watermark acceptance test | ❌ **cannot pass — the feature does not exist** |

---

# 13.9 Disposition — what to do with each class

| Class | Recommended disposition |
|---|---|
| **A (41)** | **No action beyond this document.** These are now specified. Retire the old SRS. |
| **B (19)** | **Business decision required per item.** B-01, B-02, B-03 and B-09 have compliance or operational weight and should be decided explicitly, not by default. B-12…B-19 are stack statements that should simply be deleted. |
| **C (14)** | **Delete the old statements.** C-01, C-02, C-03, C-04 and C-07 are actively misleading and would cause a rebuild to implement the wrong thing. |
| **D (11)** | **Update in place.** D-05 (token lifetime vs ADR-0009) and D-08 (stale migration script) are also defects — see G-14, G-15. |
| **E (9)** | **Delete.** Record E-08 (the reverted CSV export) as a *decision*, so nobody rebuilds it. |
| **G (20)** | **Engineering backlog** — prioritised in [Document 15](15-Recommendations.md). |

## The four decisions that need a business owner

| Decision | Question |
|---|---|
| **D1 — MFA** | The SRS says MFA is mandatory for all accounts; the system makes it optional and defaults it off. **Which is the requirement?** If an accreditation body has been told "MFA enforced", the system does not currently support that claim. |
| **D2 — Obsolete-document marking** | ISO 17025 §8.3.2 requires obsolete documents to be clearly identified. The system marks the *record* obsolete but the *file* downloads unmarked. **Is the record-level marking sufficient for your assessor?** |
| **D3 — Equipment lock-out** | Out-of-service equipment is flagged but not blocked from being named in any record, because instrument references are free text. **Is a procedural control acceptable, or must this be enforced by the software?** |
| **D4 — Analytical charts** | Twelve study types compute their statistics but render no plots. Reviewers must interpret numbers. **Is that acceptable for method-validation review and QC sign-off?** |
