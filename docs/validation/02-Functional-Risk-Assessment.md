# Functional Risk Assessment (FRA)

| Field | Value |
| ----- | ----- |
| Document ID | FRA-NTQMS-001 |
| System | NT.QMS |
| Version | 1.0 |
| Parent | VMP-NTQMS-001; URS-NTQMS-001 |
| Method | GAMP 5 risk-based assessment (severity × probability × detectability → risk priority → testing rigor) |

---

## 1. Method

For each URS area the risk to patient safety, product quality, and **data integrity** is
assessed. Following GAMP 5 / ICH Q9:

**Step 1 — Risk Class** from **Severity (S)** × **Probability (P)**:

| | P: Low | P: Medium | P: High |
| - | ------ | --------- | ------- |
| **S: High** | Medium | High | High |
| **S: Medium** | Low | Medium | High |
| **S: Low** | Low | Low | Medium |

**Step 2 — Risk Priority** from Risk Class × **Detectability (D)** (Low D = hard to detect =
higher priority):

| Risk Class | D: High | D: Medium | D: Low |
| ---------- | ------- | --------- | ------ |
| High | Medium | High | **High** |
| Medium | Low | Medium | High |
| Low | Low | Low | Medium |

**Step 3 — Testing rigor by Risk Priority:**

| Risk Priority | Rigor | Qualification focus |
| ------------- | ----- | ------------------- |
| **High** | Rigorous, scripted, positive **and** negative tests; independent review | IQ + OQ + PQ |
| **Medium** | Scripted functional tests of primary paths | OQ (+ PQ where workflow) |
| **Low** | Verification by demonstration / existing automated tests | OQ (light) |

S/P/D scale: High / Medium / Low.

---

## 2. Area-Level Risk Assessment

| Area | URS | S | P | D | Risk Priority | Rigor / Qualification |
| ---- | --- | - | - | - | ------------- | --------------------- |
| **Tenant isolation** | URS-008 | High | Med | Low | **HIGH** | IQ (RLS FORCE active) + OQ + PQ; positive+negative+fail-closed |
| **Audit trail immutability & hash chain** | URS-011,012,013,014 | High | Med | Low | **HIGH** | IQ (append-only grants) + OQ + PQ; tamper/gap detection |
| **Electronic signatures (dual component)** | URS-020,021,022,023 | High | Med | Low | **HIGH** | OQ + PQ; positive + negative (bad pw / bad PIN / lockout) |
| **Signed-record / analytical immutability** | URS-041,042 | High | Low | Low | **HIGH** | IQ (trigger + no-DELETE grant) + OQ; DB-level UPDATE/DELETE rejection |
| **Separation of duties (NC + analytical + policy)** | URS-031,039,049 | High | Med | Med | **HIGH** | OQ; negative test (self-verify / self-approve rejected) |
| **Reason for change** | URS-015,037 | High | Med | Med | **HIGH** | OQ; delete-without-reason rejected; QC target reason enforced |
| **Access control / RBAC** | URS-005,006 | High | Med | Med | **HIGH** | OQ; role matrix positive+negative; live session revocation |
| **Authentication hardening** | URS-001,002,003,004,007 | High | Med | Med | High | OQ; policy, lockout, MFA |
| **Security event logging** | URS-016,019 | Med | Med | Med | Medium | OQ |
| **Document control lifecycle** | URS-025,026,027 | Med | Med | Med | Medium | OQ + PQ (draft→publish→acknowledge) |
| **Document acknowledgement / controlled copy** | URS-028,029 | Med | Low | Med | Low–Medium | OQ |
| **NC / CAPA lifecycle & event types** | URS-030,032,033,034 | Med | Med | Med | Medium | OQ + PQ (raise→CAPA→close) |
| **QC / Westgard evaluation** | URS-035,036,037 | High | Med | Med | High | OQ + PQ (QC run→verdict→sign-off) |
| **Method-validation studies** | URS-038,040 | Med | Med | Med | Medium | OQ (per study type; sign-off) |
| **Records retention & archival** | URS-043,044,045,046 | Med | Med | Med | Medium | OQ; snapshot mandatory, legal hold blocks disposal |
| **Governance (change/risk/policy/supplier/reviews)** | URS-047,048,049,050,051,052 | Med | Low | Med | Low–Medium | OQ |
| **Backup / DR** | URS-053,054,055 | High | Low | Med | Medium–High | IQ (config) + periodic restore-drill (PQ-style) |

---

## 3. High-Risk GxP Functions — Detailed Rationale

These functions receive the most rigorous, independently reviewed testing (positive and
negative), because a failure is both **severe** (data-integrity / Part 11 breach) and
**hard to detect** at the point of use:

1. **Audit-trail immutability & hash chain (URS-011–014).** The audit trail is the primary
   evidence of data integrity. Silent corruption or a gap would be undetectable to an
   operator. Controls: SHA-256 per-tenant hash chain (`LedgerHash.Compute`), append-only DB
   grants (no UPDATE/DELETE for `qams_app`), on-demand chain verification. Must be verified
   by both demonstrating capture and attempting/observing tamper detection.

2. **Electronic signatures (URS-020–023).** Directly implements Part 11 subpart C. Requires
   both components (`ESignatureService` verifies password **and** PIN). Negative testing is
   mandatory: wrong password → SIG-002, wrong/missing PIN → SIG-001, repeated failure →
   lockout SIG-003, each logged (ESIGN_FAILED / ESIGN_LOCKED).

3. **Tenant isolation (URS-008).** In a multi-tenant SaaS, cross-tenant leakage is a
   catastrophic confidentiality/integrity failure and is invisible to a single-tenant user.
   Controls: PostgreSQL RLS `ENABLE`+`FORCE`, GUC set per connection, fail-closed when
   tenant is nil, `qams_app` is `NOBYPASSRLS`. Verified positive (own rows visible), negative
   (other-tenant rows hidden even to the owning role), and fail-closed.

4. **Signed-record / analytical immutability (URS-041,042).** After sign-off, records must
   not change. Controls: `qams.reject_frozen_mutation()` BEFORE UPDATE/DELETE trigger on 12
   analytical roots + approved uncertainty budgets; runtime role holds no DELETE grant. Must
   be verified at the database layer (raw UPDATE/DELETE rejected).

5. **Separation of duties (URS-031,039,049).** Prevents a single actor from
   authoring-and-approving. Controls: `Nonconformance.Verify` throws SOD-CAPA-002 when actor
   == raiser; `AggregateRoot.EnsureSignerIsNotPreparer` guards 14 analytical sign-offs and
   quality-policy approval. Negative testing (self-action rejected) is the key evidence.

6. **Reason for change (URS-015,037).** Part 11 §11.10(e) requires the reason for change to
   be captured. Controls: `X-Change-Reason` header enforced by `ChangeReasonMiddleware` on
   every DELETE (400 if absent), stamped into `FieldChangeRecord.Reason`; QC target changes
   require a reason and are effective-dated.

---

## 4. Risk Controls Already Verified in Development (evidence pointers)

The remediation program (F-01…F-18) implemented and unit/integration-tested the high-risk
controls above. These are development-phase evidence and are cited in the RTM; they do **not**
replace formal executed OQ/PQ:

- RLS isolation / fail-closed / bypass / WITH-CHECK — `tests/NT.QAMS.IntegrationTests/RlsTenantIsolationTests.cs` (real PostgreSQL).
- Signed-record UPDATE/DELETE rejection — `tests/NT.QAMS.IntegrationTests/SignedRecordImmutabilityTests.cs`.
- SoD (NC verify, analytical sign-off) — `AnalyticalSodTests.cs`, `NonconformanceTests.cs`.
- Reason-for-change stamping — `tests/NT.QAMS.WebApi.FunctionalTests/FieldChangeInterceptorTests.cs`.
- Hardening controls — see VSR §2 (F-01…F-18).
