# System Owner Release Decision — NT.QMS v1.49.0

| Field | Value |
| ----- | ----- |
| Document ID | SODR-NTQMS-001 |
| System / version | NT.QMS at tag **v1.49.0** (commit `1beb3bf`, CI all-green) |
| Decision maker | A. Awad — System Owner (also acting QA authority) |
| Decision date | 2026-07-29 |
| Decision type | **Conditional release approval with documented risk acceptance** |
| Recorded by | Engineering, on the System Owner's instruction of 2026-07-29 |

## 1. Decision

The System Owner, having reviewed the evidence base listed in §2, **approves NT.QMS
v1.49.0 for use** and **accepts the residual risk** of the items in §3 remaining open,
subject to the conditions in §4.

This document records a **management release decision**. It is **not** the executed
re-validation: REVAL-NTQMS-001 (doc 06) remains open until its IQ/OQ/PQ protocols are
formally executed and signed. This decision does not close doc 06, and no entry in doc
06's signature blocks is made by virtue of this document.

## 2. Evidence reviewed (all verifiable in-repo / in-CI)

- **Automated qualification evidence:** CI run `1beb3bf` — backend suite against real
  PostgreSQL 17 executed as the least-privilege `qams_app` role (RLS enforced), 67
  frontend specs on Angular 22.0.8, auth + axe-a11y e2e, container non-root assertion,
  .NET SCA / npm SCA / Trivy scan gates — all green, 0 skipped.
- **Supply chain:** `npm audit --omit=dev` = 0 advisories; exception register empty;
  .NET dependency tree clean; image scan clean of fixable HIGH/CRITICAL.
- **Security:** in-house adversarial assessment 24/24 checks, 0 findings
  (`NT_QMS_Security_Assessment_Report.html`; dev-instance scope stated).
- **Engineering dry-run** of the dev-executable re-validation subset with actual results
  (doc 07, REVAL-NTQMS-001-DR1).
- **Audit posture:** EAC-NTQMS-001 ≈88%, 0 critical, 0 P0 blockers, approved with
  conditions.

## 3. Residual risks knowingly accepted (open items)

| Item | Open activity | Interim mitigation |
| ---- | ------------- | ------------------ |
| SEC-001 | Independent penetration test (staging) | In-house 24/24 adversarial probe; defense-in-depth controls (RLS, deny-by-default authz, CSP, rate limiting, rotating sessions) |
| DOC-001 | Formal witnessed execution of REVAL-NTQMS-001 IQ/OQ/PQ on a qualified environment | Engineering dry-run (doc 07) executed with recorded actuals; automated evidence engines green on every push |
| OPS-001 / R-5 / R-7 | Staging observability bring-up, load ≥100 VU + 24 h soak, alert-fires drill | Dev-host load baseline (p95 86–105 ms, 0% errors); alert rules authored and reviewed |

## 4. Conditions attached to this approval

1. The open items in §3 are executed when the corresponding environment/party is
   available; their reports attach to doc 06 and this decision is then superseded by the
   signed VSR addendum.
2. Any new high/critical SCA finding, failed CI gate, or security incident suspends this
   approval pending System Owner review.
3. This document is signed below by the System Owner in their own hand (or via the
   organization's e-signature system). **Until signed, it records a communicated
   decision, not an applied signature.**

## 5. Signature

The undersigned confirms the decision, evidence review, and risk acceptance above.

| Role | Name | Signature | Date |
| ---- | ---- | --------- | ---- |
| System Owner / QA authority | A. Awad | ____________________ | __________ |

*Prepared by Engineering as a faithful record of the System Owner's decision communicated
on 2026-07-29. Engineering does not certify validation and has applied no signature.*
