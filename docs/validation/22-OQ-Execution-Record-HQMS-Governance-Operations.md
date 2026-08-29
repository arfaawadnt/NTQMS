# Operational Qualification — Execution Record TEMPLATE: HQMS Governance & Operations Modules

| Field | Value |
| ----- | ----- |
| Document ID | OQ-EXEC-NTQMS-007 |
| Protocol executed | REVAL-NTQMS-002 (doc 20) Part A — URS-139…145, URS-147 |
| System / version | NT.QAMS — `feature/hqms-hospital-modules` (record the deployed commit/tag here) |
| Environment | _(record the qualified environment here — NOT a development workstation)_ |
| Executed by (operator) | _(unsigned — template)_ |
| Witnessed by | _(unsigned — template)_ |
| Date of execution | |
| Test data | _(record tenant and operators; SOD-CRD-001 needs two operators)_ |
| Result | **TEMPLATE — not executed.** Engineering evidence engines in §2 ran green on 2026-08-29 (backend 920/0, `verification-log.md`). |

---

## 1. Witnessed cases

### OQ-HQMS-08 — Indicator loop with normalized periods and SPC (URS-139)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Define a Monthly indicator (rate factor > 0), set targets/thresholds | Saved; thresholds visible after reload (form seeded) | | |
| Record a measurement for the 3rd of a month; then attempt another for the 17th of the same month | First stored under the 1st of the month; second refused IND-016 | | |
| Record a breaching value | Status Breached; an analysis task opens | | |
| Enter enough points that the first two sit far above the rest | SPC chart flags the opening pair (R2) as special cause | | |
| Edit the definition with a 3,000-char description | 400 validation (bounded) | | |

### OQ-HQMS-09 — Accreditation standards & verified evidence (URS-140)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Define a standard set with weighted elements; activate | Readiness renders from weights | | |
| Link evidence of type Document choosing a record from the picker | Link stores the verified record id | | |
| Attempt to link a Document evidence with a fabricated GUID via API | Refused EVD-004 (record must exist in-tenant) | | |
| DBA (witnessed): UPDATE an evidence link's element id to a random GUID | Rejected 23503 (tenant-composite FK) | | |

### OQ-HQMS-10 — Committee governance integrity (URS-141)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Create a committee (quorum 2) with two members; schedule a meeting | Scheduled | | |
| Record attendance for a non-member | Refused CMT-017 | | |
| Record one member present; attempt to hold | Refused MTG-014 (not quorate) | | |
| Record the second member; hold; record minutes; approve minutes | MinutesApproved | | |
| DBA (witnessed): UPDATE the approved meeting's minutes | Rejected 23514 (frozen) | | |
| Close a decision on the approved meeting | Allowed (action items stay live) | | |
| Disband the committee; attempt to schedule a new meeting | Refused CMT-016 | | |

### OQ-HQMS-11 — Survey capture immutability & results (URS-142)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Define a survey with scored questions in two domains; activate; capture responses for two departments | Captured | | |
| Open results | Means per question/domain/department match a hand-check of the entered scores | | |
| DBA (witnessed): UPDATE a response's service line and an answer's score | Both rejected 23514 (immutable from capture) | | |

### OQ-HQMS-12 — Credentialing with PSV independence (URS-143)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Operator A adds a licence to a practitioner; operator A attempts to verify it | Refused SOD-CRD-001 | | |
| Operator B verifies against a named source; attempt to verify again | First: verified; second: refused CRD-014 | | |
| Request + grant a privilege; credential with an appointment end | Credentialed | | |
| Point-of-care verify the privilege with today inside the window, then set the check date past `AppointedUntil` (or use a short window) | Inside: true; lapsed: false with "Appointment lapsed on …" | | |
| Let the licence expire (short-dated test licence); attempt reappointment | Refused CRD-032 (current evidence required) | | |
| Let a grant lapse; request the same privilege again | Renewal request opens (lapsed grant does not block) | | |

### OQ-HQMS-13 — Environment of care (URS-144)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Schedule a round; record a Critical finding; complete the round; resolve the finding with a corrective note | Dashboard open/critical counts track each step | | |
| Schedule and hold a drill; record an evaluation score | Drill coverage + mean score update | | |

### OQ-HQMS-14 — Training delivery integrity & currency (URS-145)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Attempt to schedule a session for a Draft course | Refused CRS-013 | | |
| Activate the course (pass mark 70); schedule; register two trainees; hold | Session Held; pass mark frozen at 70 on the session | | |
| Record both trainees with post-score 75 | Both Passed (judged at 70) | | |
| On a course with 12-month validity, verify the compliance dashboard splits current vs lapsed for a pass older than 12 months (seed a back-dated session) | Lapsed counted; "Lapsed trainings" stat > 0 | | |

### OQ-HQMS-16 — FMEA scales & audit-program bounds (URS-147)

| Step | Expected | Actual observed | P/F |
| ---- | -------- | --------------- | --- |
| Create an FMEA study; add a failure mode scored 5/5/5 | RPN 125 | | |
| Attempt severity 11 via API | Refused (domain guard); DBA UPDATE to 11 rejected 23514 | | |
| Create an audit program (current year); plan an audit in quarter 2; attempt quarter 5 via DBA UPDATE | Quarter 5 rejected 23514 | | |

## 2. Evidence engines (engineering reference — attach the QA-environment run)

| Suite | Covers | Engineering run (2026-08-29, `abcc881`) |
| ----- | ------ | --------------------------------------- |
| `QualityIndicatorTests` + `IndicatorSpcTests` | OQ-HQMS-08 | Green |
| Accreditation suites + `CrossAggregateReferenceTests` | OQ-HQMS-09 | Green |
| `CommitteeAndMeetingTests` + `CommitteeGovernanceTests` + `CommitteeIntegrityTests` + frozen probe | OQ-HQMS-10 | Green |
| Survey suites + `reject_any_mutation` probes | OQ-HQMS-11 | Green |
| `PractitionerTests` (M-19 facts) + `CredentialingQueriesTests` | OQ-HQMS-12 | Green |
| EOC suites + summary aggregates | OQ-HQMS-13 | Green |
| `TrainingSessionTests` + `TrainingComplianceTests` | OQ-HQMS-14 | Green |
| FMEA/audit-program suites + `Postgres_rejects_out_of_range_hqms_numerics` | OQ-HQMS-16 | Green |

## 3. Disposition

_(QA completes after execution: per-case P/F summary, deviations, sign-off.)_
