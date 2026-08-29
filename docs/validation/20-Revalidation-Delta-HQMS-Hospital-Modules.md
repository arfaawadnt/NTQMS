# CSV Re-Validation Delta — HQMS Hospital Modules (feature/hqms-hospital-modules)

| Field | Value |
| ----- | ----- |
| Document ID | REVAL-NTQMS-002 (rev 1 — authored 2026-08-29, post-remediation) |
| System | NT.QAMS — HQMS hospital extension (HQMS-MSP-001, modules M02–M24 as delivered) |
| Baseline validated version | 1.0 (docs 00–05) + delta REVAL-NTQMS-001 through v1.53.0 |
| Scope of this delta | The hospital-module train on `feature/hqms-hospital-modules`: 12 new bounded contexts + 4 aggregate completions + the incident→CAPA convergence, **including the 2026-08-28/29 audit remediation** (register `E:\QMS\NT_QAMS_HQMS_Audit_Register_2026-08-28.md`: B-01, M-03, M-05, M-07…M-12, M-14…M-21, M-23, N-01…N-05 closed; commit range `45ebd7b…abcc881`) |
| Parent | VMP-NTQMS-001; URS-NTQMS-001; RTM-NTQMS-001; QP-NTQMS-001; VSR-NTQMS-001; REVAL-NTQMS-001 |
| Status | **DRAFT for QA execution.** Engineering-prepared traceability + qualification templates; **QA owns, executes, witnesses, and signs.** Nothing here closes DOC-001. |

> **How to use this document.** Same convention as REVAL-NTQMS-001: this is a *delta*
> package adding the hospital-module requirements (URS-135+), installation checks (IQ-32+),
> operational cases, and a VSR addendum. Every execution cell in the referenced OQ records
> (docs 21–22) is a **template**; the named automated suite is the *evidence engine* whose
> green run may be attached as executed evidence, with witnessed manual confirmation per the
> baseline QP convention. The final pre-QA engineering run is recorded in
> `verification-log.md` (2026-08-29: backend **920/0**, Karma **133**).

**Signature block (per executed protocol section):**

| Activity | Name | Signature | Date |
| -------- | ---- | --------- | ---- |
| Prepared by (Engineering) | | | |
| Executed by | | | |
| Reviewed by (QA) | | | |
| Approved by (System Owner) | | | |

**Change-control provenance.** Feature train commits `8b80680…d5cf5a4` plus remediation
commits `45ebd7b…abcc881` (one atomic commit per audit finding, each with a test that failed
before the fix). Engineering record: `IMPLEMENTATION_LOG.md` (HQMS train + audit-remediation
entries); conformance evidence: `E:\QMS\NT_QAMS_HQMS_Conformance_Verification_Report_2026-08-28.md`
+ Annexes A–C; audit register: `E:\QMS\NT_QAMS_HQMS_Audit_Register_2026-08-28.md`; upgrade
actions: `deploy/RELEASE-NOTE-HQMS-ROLE-GRANTS.md`.

---

## Part A — Requirements Traceability Matrix (RTM) delta

Verification legend as in RTM-NTQMS-001 (**AUTO** automated test, **OQ** scripted case in docs
21–22, **IQ** install check, **INSP** inspection). All statuses are **Template** until QA signs.

### A.1 Occurrence / incident reporting (HQMS M02)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-135 | Staff shall report occurrences attributed **or anonymously**; an anonymous report shall persist **no reporter identity anywhere** (record, audit stamp, field-change ledger) and shall issue a one-time tracking reference for follow-up. Workflow: triage (or reject with reason) → investigation (factors, timeline, summary) → review → **Part 11 signed close**; sentinel events declared with SLA escalation; a closed incident is immutable (DB trigger) and cannot gain a post-closure CAPA link (INC-032). | `Domain/IncidentReporting/Incident.cs` (+`IIdentitySuppressed`, B-01); `AuditStampInterceptor`/`FieldChangeInterceptor` suppression; `IncidentsController`; `RaiseCapaFromIncidentHandler` convergence; `HqmsFrozenRecordImmutability` migration (M-05) | AUTO `IncidentTests` (incl. INC-032), `AnonymousSuppressionTests` (3), `WorkflowCommandPolicyTests`, `SignedRecordImmutabilityTests.Hqms_frozen_records…`, `MalformedEnumRequestTests`; OQ-HQMS-01…04 (doc 21) | Template |

### A.2 Patient-safety events & rates (HQMS M08)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-136 | Falls and pressure injuries shall be reported with harm level (and stage/origin for pressure injuries), reviewed and closed; fall and pressure-injury rates shall be computed **per 1,000 patient-days** from the ADT census denominator with windows clipped to elapsed days, hospital-acquired pressure injuries counted separately, and **no rate fabricated when the denominator is zero** (null → "—"). The register shall page. | `Domain/PatientSafety/*`; `GetSafetyRatesHandler` (+`WindowedDays`, M-03); nullable rate DTOs (M-18); paged `GetSafetyEventsQuery` (M-10) | AUTO `SafetyRatesTests`, `SafetyRatesDenominatorTests` (incl. null-rate fact), `HqmsRegisterPagingTests`; OQ-HQMS-05 (doc 21) | Template |

### A.3 HAI surveillance & device exposure (HQMS M09)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-137 | CLABSI/CAUTI/VAP/SSI cases shall be reported, reviewed, closed — or **rejected with a reason** (duplicate/wrong patient), whereupon they **leave the official rates**; device exposures shall accrue device-days; device-associated rates per 1,000 device-days and utilisation ratios shall derive from grouped, non-rejected cases with null (not 0.00) on zero denominators. Registers page. | `HaiCase` (+`Reject`, M-18); `DeviceExposure`; `GetHaiRatesHandler`; `HaiComplicationRejectTransition` migration; paged queries (M-10) | AUTO `HaiCaseTests` (reject facts), `HaiRatesTests` (incl. `Rejected_cases_leave_the_official_rates`), `CheckConstraintTests.The_hai_and_complication_status_domains…`, `HqmsRegisterPagingTests`; OQ-HQMS-06 (doc 21) | Template |

### A.4 Mortality & morbidity review (HQMS M10)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-138 | Deaths shall be reported, classified (expected/unexpected/potentially preventable/preventable), second-reviewed, committee-discussed and closed; complications shall be reported, peer-reviewed with a preventability judgement, closed — or **rejected with a reason** and excluded from morbidity counts; mortality rate per 1,000 patient-days shall be null (not 0.00) without a denominator. | `MortalityReview`, `ComplicationCase` (+`Reject`, M-18); `GetMortalityRatesHandler` | AUTO `MortalityReviewTests`, `ComplicationCaseTests` (reject fact), rates assertions; OQ-HQMS-07 (doc 21) | Template |

### A.5 Quality indicators & SPC (HQMS M04)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-139 | Indicators shall be defined with numerator/denominator/unit/rate factor/frequency/direction and thresholds; measurement periods shall **normalize to the frequency's canonical start day** with one measurement per period; values shall grade against thresholds (breach → analysis task); SPC shall apply Nelson rules R1–R4 including the series-opening R2 window; definition updates shall be validator-bounded. | `QualityIndicator` (+`NormalizePeriod`, M-17); `IndicatorSpc` (R2 opening); `UpdateIndicatorDefinitionValidator`; SPA seeded targets form + month picker | AUTO `QualityIndicatorTests` (normalization facts), `IndicatorSpcTests` (R2 opening), `IndicatorBreachToTaskPolicy` tests; OQ-HQMS-08 (doc 22) | Template |

### A.6 Accreditation standards & evidence (HQMS M05)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-140 | Standard sets (GAHAR/JCI/ISO) shall hold weighted elements (weight ≥ 1, DB CHECK); element compliance shall be assessed and readiness percentages derived; evidence links shall reference **verified in-tenant records** (existence-checked by type) or explicit external references, with tenant-composite FKs to set and element. | `StandardSet`/`StandardElement`; `EvidenceLink` (EVD-003/004, M-15); `HqmsCrossAggregateForeignKeys` (M-08); `HqmsColumnHardening` CHECKs (N-05) | AUTO Accreditation domain suite, `CrossAggregateReferenceTests`, `Postgres_rejects_out_of_range_hqms_numerics`; OQ-HQMS-09 (doc 22) | Template |

### A.7 Committee governance (HQMS M16/M17)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-141 | Committees shall hold members (unique per user) and quorum; meetings shall record agendas, **member-only attendance** (unique per attendee), hold only when quorate, record and approve minutes (approved minutes DB-immutable); a **disbanded committee schedules/holds/approves nothing**; decisions remain trackable to closure after approval. | `Committee`/`Meeting` (M-16 guards MTG-024, CMT-016/017); `CommitteeIntegrityIndexes`; `HqmsFrozenRecordImmutability` (meeting) | AUTO `CommitteeAndMeetingTests`, `CommitteeGovernanceTests` (3), `CommitteeIntegrityTests` (23505 probes), immutability probe; OQ-HQMS-10 (doc 22) | Template |

### A.8 Patient-experience surveys (HQMS M18)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-142 | Surveys shall define scored questions by domain; responses shall be captured per department/service line and be **immutable from capture** (DB trigger, answers included); results shall aggregate means by question, domain and department from database-grouped sums. | `SatisfactionSurvey`/`SurveyResponse` (create-only); `reject_any_mutation` triggers (M-05); grouped results query (M-10) | AUTO survey domain suite, immutability probe, results assertions; OQ-HQMS-11 (doc 22) | Template |

### A.9 Practitioner credentialing (HQMS M13)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-143 | Practitioner licences shall be primary-source verified by a verifier **independent of whoever keyed the credential in** (SOD-CRD-001), never silently re-verified; (re)appointment shall require **current** evidence (unexpired verified licence + active grant); lapsed grants shall not block renewal; the point-of-care check shall answer false once the **appointment window** lapses, with the lapse named. | `Practitioner`/`LicenceCredential` (+`AddedByUserId`, M-19); `LicenceAddedByForPsvSod` migration; `VerifyPrivilegeHandler` | AUTO `PractitionerTests` (5 M-19 facts), `CredentialingQueriesTests`; OQ-HQMS-12 (doc 22) | Template |

### A.10 Environment of care (HQMS M15)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-144 | Safety rounds shall record findings (severity-graded, resolved with corrective notes); drills shall be scheduled, held and evaluated; the EOC dashboard shall aggregate round completion, open/critical findings and drill coverage in the database. | `SafetyRound`/`Drill`; `GetEocSummaryHandler` (M-10 aggregates) | AUTO EOC domain suite + summary assertions; OQ-HQMS-13 (doc 22) | Template |

### A.11 Training & competence extension (HQMS M12)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-145 | Courses shall carry validity months and a pass mark; sessions shall be schedulable/holdable **only for Active courses**, with the pass threshold **frozen at Hold** so every attendee of one session is judged identically and "Passed" is reproducible; the compliance dashboard shall report **currency** (current vs lapsed passes per validity window) alongside effectiveness. | `TrainingCourse`/`TrainingSession` (+`PassMarkAtHold`, M-20); `SessionPassMarkSnapshot` migration; currency computation + SPA lapsed stat | AUTO `TrainingSessionTests`, `TrainingComplianceTests` (Draft-schedule, snapshot, lapsed-pass facts); OQ-HQMS-14 (doc 22) | Template |

### A.12 ADT integration & census (HQMS M24)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-146 | The ADT inbox shall **store first** (Received, own transaction) then process; malformed event types and non-domain failures shall be **recorded as Failed messages** against endpoint health, never bounced traceless; duplicate deliveries shall resolve idempotently (dedup key, DB-backed, 23505-safe); a repeated admit carrying a **different patient** for the same encounter shall be refused (STAY-023); patient-stay projections shall accrue clipped patient-days (no future-day accrual) feeding the census and every rate denominator; endpoint configuration shall require `integration.manage` while adapters ingest with `integration.create` only. | `IngestAdtEventHandler` (M-12); `IDatabaseErrorClassifier` port + Npgsql impl; `PatientStay` + `WindowedDays` (M-03); permission split (M-12) | AUTO `IngestAdtEventTests` (incl. patient-mismatch), `AdtInboxTests` (functional), `ux_integration_message_dedup` probe, census/denominator suites; OQ-HQMS-15 (doc 21) | Template |

### A.13 Proactive risk / FMEA (HQMS M03) and audit programs (HQMS M14)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-147 | FMEA studies shall score failure modes on 1–10 severity/occurrence/detection scales (RPN 1–1,000, DB CHECKs) with recommended actions bounded; annual audit programs shall plan audits by quarter (1–4) within a sane year window and link scheduled audits by tenant-composite FK. **Note:** FMEA rides on `risks.*` and audit programs on `audits.*` — reused keys disclosed in the release note. | `FmeaStudy`; `AuditProgram`/`PlannedAudit`; `HqmsColumnHardening` CHECKs (N-05); `HqmsCrossAggregateForeignKeys` (M-08) | AUTO FMEA/audit-program domain suites, numeric CHECK probes; OQ-HQMS-16 (doc 22) | Template |

### A.14 Access governance for the hospital modules (cross-cutting)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-148 | Every hospital endpoint shall be permission-gated at the controller **and** its workflow commands policy-gated at the application tier; seeded-role reach over the 11 new permission modules shall be an **explicit per-role decision** — Department Head and Analyst hold clinical intake/recording grants, the **External Auditor is excluded** from the clinical registries, credential files and the ADT census; malformed request enums shall answer 400 `REQ-001` problem+json, never 500 or a smuggled undefined value. | `SystemRoleCatalog` (M-07); `RequestEnum` + `DomainExceptionHandler` arm (M-11); `RELEASE-NOTE-HQMS-ROLE-GRANTS.md` | AUTO `SystemRoleCatalogTests`, `AuditorDenyMatrixTests`, `RoleEndpointMatrixTests`, `RolePrivilegeFlowTests`, `MalformedEnumRequestTests`, `RequestEnumTests`, `WorkflowCommandPolicyTests`, `UngatedActions.approved.txt`; OQ-HQMS-17 (doc 21) | Template |

### A.15 Hospital data-protection tier (cross-cutting)

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-149 | Every new tenant-scoped table shall carry FORCE RLS with the tenant-isolation policy; frozen-state records (closed incidents, approved minutes, captured survey responses/answers) shall be **database-immutable**; cross-aggregate references shall be tenant-composite foreign keys; free text ≥ 1,000 chars shall be `text` with validator bounds; numeric domains shall be CHECK-constrained; relational identifiers shall never silently truncate (guard test) and unique indexes shall use the `ux_` convention. | Migrations `HqmsFrozenRecordImmutability`, `HqmsCrossAggregateForeignKeys`, `HqmsColumnHardening`, `PinTruncatedForeignKeyNames`, `PinUniqueIndexNames`, `CommitteeIntegrityIndexes` (M-05/M-08/N-05/M-14/N-04/M-16) | AUTO `SignedRecordImmutabilityTests`, `CrossAggregateReferenceTests`, `CheckConstraintTests`, `RelationalNameTruncationTests` (3 model guards), `CommitteeIntegrityTests`, `RlsTenantIsolationTests`; IQ-32…34 | Template |

---

## Part B — Installation Qualification (IQ) delta

| IQ | Check | Method | Acceptance | Status |
| -- | ----- | ------ | ---------- | ------ |
| IQ-32 | All HQMS migrations apply from zero by both paths (`MigrateOnStartup` and idempotent `psql -f deploy/migrations.sql`) | Fresh database, both applies | Both complete; table parity with the EF snapshot | Template |
| IQ-33 | FORCE RLS + `tenant_isolation` policy active on every new tenant-scoped table | `SELECT relname FROM pg_class WHERE relforcerowsecurity` vs the new-table list | Every new tenant table listed | Template |
| IQ-34 | The six data-protection migrations round-trip (Down restores, re-Up reapplies): frozen triggers, cross-aggregate FKs, CHECKs, index/FK renames | `dotnet ef database update <prev>` then head, per migration | Each Down/Up completes; object counts match (engineering evidence: executed 2026-08-28/29 on a throwaway instance, recorded in IMPLEMENTATION_LOG) | Template |
| IQ-35 | Role grants for existing tenants applied per `RELEASE-NOTE-HQMS-ROLE-GRANTS.md` §1–§2 (no automatic grants), reused-key review (§3) performed | Admin walk-through + role review | Grant matrix matches §1; custom roles holding `risks.*`/`audits.*`/`training.*` reviewed | Template |

## Part C — Operational Qualification (OQ) delta

### New OQ evidence-engine suites (add to the QP evidence-engine table)

| Suite | Scope |
| ----- | ----- |
| `NT.QAMS.Domain.UnitTests` — IncidentReporting, PatientSafety, InfectionControl, MortalityReview, QualityIndicators, Accreditation, Committees, PatientExperience, Credentialing, EnvironmentOfCare, TrainingManagement, RiskGovernance (FMEA), AuditManagement, Integration | Aggregate invariants incl. every audit-remediation guard |
| `NT.QAMS.Application.UnitTests` — the same modules + `Persistence/RelationalNameTruncationTests` | Handlers, rates/denominators, SoD, catalog decisions, model-level schema guards |
| `NT.QAMS.Architecture.Tests` (192) | Conformance pack: boundaries, command policies, gated actions, decision snapshots |
| `NT.QAMS.IntegrationTests` (real PostgreSQL) — `SignedRecordImmutabilityTests`, `CrossAggregateReferenceTests`, `CheckConstraintTests`, `CommitteeIntegrityTests` | Database-tier defenses actually fire (23514/23503/23505) |
| `NT.QAMS.WebApi.FunctionalTests` — `AuditorDenyMatrixTests`, `RoleEndpointMatrixTests`, `RolePrivilegeFlowTests`, `MalformedEnumRequestTests`, `AdtInboxTests`, `HqmsRegisterPagingTests` + real-PG four | The live pipeline: authorization matrix, error contract, inbox, paging |

### OQ manual/witnessed case templates

The scripted witnessed cases live in **doc 21** (`21-OQ-Execution-Record-HQMS-Clinical-Modules.md`,
OQ-HQMS-01…07, 15, 17) and **doc 22** (`22-OQ-Execution-Record-HQMS-Governance-Operations.md`,
OQ-HQMS-08…14, 16). Every Actual/P-F/Executed-by/Date cell is a template for QA.

## Part D — Performance Qualification (PQ) delta

PQ remains as the baseline QP defines it, with one hospital-specific addition to schedule after
go-live: a month-boundary review that the rate dashboards (patient-safety, HAI, mortality) match
a manual recomputation from the registers for one completed month (numerators exclude rejected
cases; denominators from the ADT census). Record as PQ-HQMS-01 when scheduled.

## Part E — Validation Summary Report (VSR) addendum

Engineering status at authoring (2026-08-29, commit `abcc881`):

- Backend **920 passed / 0 failed** (+9 environment-conditional skips): Domain 450 · Application
  142 · Architecture 192 · Integration 30 (+9) · Functional 106. Frontend Karma 133; production
  build clean. Per-suite history in `verification-log.md`.
- The 2026-08-28 conformance pack (Gates 1–6) passed with evidence on throwaway databases; Gate 7's
  adversarial review produced the audit register whose approved findings are remediated in this
  branch (2 Blockers, 18 Majors/Minors closed; the Group C items below remain OPEN).
- **Open engineering decisions excluded from this delta** (tracked in the audit register): M-02
  org-scope stance, M-04 cross-module-read ADR, M-16 signed-gate list (minutes approval ceremony),
  M-12 retention/PHI ADR for `integration_message.raw_payload`, and the N-06…N-14 minors not
  individually approved. The RawPayload retention/redaction ADR is the most material to a
  hospital deployment and must be closed before PHI-bearing feeds are connected.
- Release posture **unchanged — Pre-production**; DOC-001 (signed validation on a qualified
  environment) and SEC-001 (independent penetration test) remain the open release blockers, and
  this package folds under DOC-001 until QA executes and signs it.

## Part F — Execution checklist for QA (what "done" requires)

1. Execute IQ-32…35 on the qualified environment; attach outputs.
2. Run the five backend suites + Karma on the qualified build; attach the run (append a
   `verification-log.md` row).
3. Execute and witness OQ-HQMS-01…17 (docs 21–22), recording Actual/P-F/name/date per case.
4. Review the reused-key disclosure and the External-Auditor exclusion with the system owner
   (release note §3–§4) and record acceptance.
5. Sign Part A statuses from Template to Executed; complete the signature block above.
6. Schedule PQ-HQMS-01 for the first full month after go-live.
