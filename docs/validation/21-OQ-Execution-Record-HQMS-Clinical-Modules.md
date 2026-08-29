# Operational Qualification — Execution Record TEMPLATE: HQMS Clinical Modules

| Field | Value |
| ----- | ----- |
| Document ID | OQ-EXEC-NTQMS-006 |
| Protocol executed | REVAL-NTQMS-002 (doc 20) Part A — URS-135…138, URS-146, URS-148 |
| System / version | NT.QAMS — `feature/hqms-hospital-modules` (record the deployed commit/tag here) |
| Environment | _(record the qualified environment here — NOT a development workstation)_ |
| Executed by (operator) | _(unsigned — template)_ |
| Witnessed by | _(unsigned — template)_ |
| Date of execution | |
| Test data | _(record tenant, operators — SoD cases need two operators, one PIN-holding signer)_ |
| Result | **TEMPLATE — not executed.** Engineering evidence engines listed in §2 ran green on 2026-08-29 (backend 920/0, `verification-log.md`); that run is reference evidence, not this record's execution. |

> Every "Actual / P-F" cell below is blank by design. QA executes, observes, records and signs.
> Where a case needs prepared state (a closed incident, a rejected HAI case), the setup steps are
> part of the case.

---

## 1. Witnessed cases

### OQ-HQMS-01 — Incident lifecycle with Part 11 signed close (URS-135)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Report an attributed incident (Fall, Minor) | Created; visible in the register; status Reported | | |
| Triage → start investigation → add factor + timeline entry → record summary → submit for review | Status walks Triaged → UnderInvestigation → PendingReview | | |
| Close with the e-signature ceremony (password + PIN) as a signer who is NOT the reporter | 204; status Closed; exactly one new manifest entry bound to the incident | | |
| Attempt to edit the closed incident by API (any mutation) and by direct SQL UPDATE (DBA, witnessed) | API: 409/422 coded error; SQL: rejected 23514 by `frozen_immutability` | | |
| Attempt raise-capa on the closed incident | Refused with INC-032 | | |

### OQ-HQMS-02 — Anonymous reporting keeps its promise (URS-135)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Submit an anonymous report while signed in as operator A; keep the returned tracking reference | Receipt with one-time reference | | |
| As a quality manager, open the incident's detail, audit trail and field-change history | No trace of operator A anywhere: created-by shows "anonymous", no user id on the Created row | | |
| DBA check (witnessed): `created_by`, `created_by_user_id` on the row; `actor`, `actor_id` on the Created field-change row | "anonymous"/NULL on all four | | |
| Track the report by its reference (unauthenticated tracking surface) | Status visible by reference | | |
| Triage the incident as operator B | The triage transition is fully attributed to B (audit trail shows B) | | |

### OQ-HQMS-03 — Sentinel declaration and SLA (URS-135)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Declare a triaged incident sentinel | Flagged; escalation task/SLA per configuration | | |
| Declare again | Refused INC-027 | | |

### OQ-HQMS-04 — Access matrix on the incident surface (URS-148)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| As a seeded Analyst: report and view incidents | Allowed | | |
| As a seeded Analyst: attempt triage/close | 403 problem+json, code AUTHZ-* | | |
| As the seeded External Auditor: view incidents | 200 | | |
| As the seeded External Auditor: `GET /api/patient-safety/events`, `/api/infection-control/cases`, `/api/mortality-review/reviews`, `/api/credentialing/practitioners`, `/api/integration/census` | All 403 (M-07 exclusion) | | |
| POST an incident with `category = "Bogus"` | 400 problem+json code REQ-001 (never 500) | | |

### OQ-HQMS-05 — Patient-safety rates from the ADT denominator (URS-136)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| With zero patient-days in the window, open the patient-safety page | Rate cards show "—", not 0.00 | | |
| Ingest admits so the window holds a known number of patient-days; report one fall | Falls rate = 1000 × falls ÷ patient-days (hand-check) | | |
| Report a hospital-acquired pressure injury | HAPI counted separately with its own rate | | |

### OQ-HQMS-06 — HAI case rejection leaves the rates (URS-137)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Record a device exposure (central line) and two CLABSI cases, one a duplicate | Both cases in the register | | |
| Reject the duplicate with a reason (void-gated) | Status Rejected; reason on the record | | |
| Open the HAI rates | CLABSI count = 1; rate computed on device-days; rejected case absent from numerator | | |
| Attempt to reject a Closed case | Refused HAI-013 | | |

### OQ-HQMS-07 — Mortality & complication review (URS-138)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Report a death; classify (second operator); record second review; mark committee-discussed; close | Full walk succeeds; classification counts on the dashboard | | |
| Report a complication; peer-review with preventability; reject a duplicate complication with a reason | Rejected case leaves the morbidity counts | | |

### OQ-HQMS-15 — ADT inbox behaviour (URS-146)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Register an endpoint as an `integration.manage` holder; attempt registration as a create-only role | Manage: created; create-only: 403 | | |
| Ingest a valid admit; re-deliver the same dedup key | First: Processed; second: idempotent result, no duplicate row | | |
| Ingest with `eventType = "Nonsense"` | 200 with status Failed + error; the message row visible on the endpoint with the raw payload; endpoint health shows the failure | | |
| Ingest an admit for an existing encounter with a DIFFERENT patientRef | Failed with STAY-023; the stay keeps its original patient | | |
| Ingest a discharge dated in the future | 400 validation (future event time refused) | | |

### OQ-HQMS-17 — Register paging (URS-136/137, M-10)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Seed > 50 patient-safety events; open the register | First page renders; "Load more" appends; shown/total counter correct | | |
| `GET /api/patient-safety/events?page=2&pageSize=10` | Envelope with items/total/page/pageSize/hasMore | | |

## 2. Evidence engines (engineering reference — attach the QA-environment run)

| Suite | Covers | Engineering run (2026-08-29, `abcc881`) |
| ----- | ------ | --------------------------------------- |
| `IncidentTests` + `AnonymousSuppressionTests` + `SignedRecordImmutabilityTests` | OQ-HQMS-01…03 | Green |
| `AuditorDenyMatrixTests` + `RoleEndpointMatrixTests` + `MalformedEnumRequestTests` | OQ-HQMS-04 | Green |
| `SafetyRatesTests` + `SafetyRatesDenominatorTests` | OQ-HQMS-05 | Green |
| `HaiCaseTests` + `HaiRatesTests` + status-domain probe | OQ-HQMS-06 | Green |
| `MortalityReviewTests` + `ComplicationCaseTests` | OQ-HQMS-07 | Green |
| `IngestAdtEventTests` + `AdtInboxTests` + dedup-index probe | OQ-HQMS-15 | Green |
| `HqmsRegisterPagingTests` | OQ-HQMS-17 | Green |

## 3. Disposition

_(QA completes after execution: per-case P/F summary, deviations, sign-off.)_
