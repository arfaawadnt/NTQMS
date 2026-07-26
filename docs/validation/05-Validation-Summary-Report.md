# Validation Summary Report (VSR) — Interim

| Field | Value |
| ----- | ----- |
| Document ID | VSR-NTQMS-001 |
| System | NT.QMS |
| Version | 1.0 (INTERIM — framework ready for formal execution) |
| Parent | VMP-NTQMS-001 |
| Date | (to be dated on approval) |

---

## 1. Purpose and Overall Statement

This interim Validation Summary Report records the **current validation status** of NT.QMS
and confirms that the validation **framework** — VMP, URS, FRA, IQ/OQ/PQ protocols, and a
fully closed RTM — is complete and ready for **formal, signed execution**.

> **Overall status: VALIDATION FRAMEWORK COMPLETE; FORMAL EXECUTION PENDING.**
>
> This set does **not** assert executed, signed, or dated qualification evidence. The
> automated test suites (~270 backend tests across 5 projects, ~37 frontend unit tests, and a
> Playwright e2e suite) and the remediation program (F-01…F-18) are cited as **development /
> continuous-verification evidence** that the GxP controls exist and behave correctly. They
> substantiate readiness; they do not replace formally executed IQ/OQ/PQ with recorded actual
> results and approver signatures. NT.QMS should **not** be declared validated for production
> GxP use until the outstanding items in §4 are completed.

---

## 2. Remediation Program as Hardening Evidence (F-01 … F-18)

An independent CSV / regulatory-readiness audit identified 3 critical, 7 major, and 8 minor
findings. All were remediated across a gated release train (v1.25.0 → v1.34.0). This
demonstrates the system was systematically hardened against Part 11 / Annex 11 / ISO 17025
expectations before validation execution.

| Finding | Area | Resolution (evidence) |
| ------- | ---- | --------------------- |
| **F-01** | Tenant isolation (RLS) | `TenantConnectionInterceptor` sets GUC per connection; migration `ActivateForcedTenantRls` (ENABLE+FORCE + policies on 53 tenant tables); fail-closed; verified vs real PostgreSQL. |
| **F-02** | Signed-record immutability | `SignedRecordImmutability` migration adds `qams.reject_frozen_mutation()` trigger; `harden-runtime-role.sql` least-privilege runtime role (no DELETE). |
| **F-03** | Full GAMP 5 CSV documentation | **This document set** (VMP/URS/FRA/IQ-OQ-PQ/RTM/VSR). |
| **F-04** | Per-tenant optional MFA | TOTP enrollment gate + `TenantSettings.RequireMfaForPrivilegedRoles`; endpoints + frontend. |
| **F-05** | Separation of duties | NC verify (SOD-CAPA-002); `EnsureSignerIsNotPreparer` on 14 analytical sign-offs (SOD-AQ-001); `CreatedByUserId` stamping. |
| **F-06** | Reason for change | `X-Change-Reason` enforced on every DELETE; `FieldChangeRecord.Reason`; QC target effective-dating with reason. |
| **F-07** | Session revocation | `ActiveSessionMiddleware` re-validates user/role each request (AUTH-006/007). |
| **F-08** | Failed e-signature logging | `ESignatureService` logs ESIGN_FAILED/ESIGN_LOCKED, throttles + locks. |
| **F-09** | Real-PostgreSQL integration tests | `NT.QAMS.IntegrationTests` (RLS + immutability) run in CI against postgres:17. |
| **F-10** | Backup / restore / DR | `deploy/BACKUP-RESTORE-DR.md` (RPO ≤ 5 min, RTO ≤ 4 h, PITR + nightly dump, off-site, drills). |
| **F-11** | Governance capabilities | Quality policy, read-&-understand acknowledgement, distinct Deviation/OOS/OOT event types, change-control PIR, controlled-copy register, periodic user-access review. |
| **F-13** | Audit-trail / signature export | `/api/exports` XLSX with Integrity Attestation sheet + `signatures.xlsx`. |
| **F-14** | Archive snapshot + legal hold | Mandatory snapshot (ARC-002); legal hold blocks disposal (ARC-015). |
| **F-15** | Password policy | Single `PasswordRules.StrongPassword()` (≥12, complexity, breach blocklist). |
| **F-16** | Configuration control | Externalized Westgard limits (validated at startup); role magic-strings → central `Roles` constants. |
| **F-17** | Secrets hygiene | Dev secrets in .NET user-secrets; `appsettings` blanked; prod ships empty secrets. |
| **F-18** | Frontend + e2e coverage | Karma/Jasmine unit tests + Playwright e2e (`auth.spec.ts`, `regulated-workflow.spec.ts`); both are CI gates. |

**Controlled build pipeline (`.github/workflows/ci.yml`):** on every push, restores, builds
Release, provisions a **non-superuser** `qams_app` role, applies EF migrations, and runs the
full solution with the integration suite against real PostgreSQL 17; architecture-boundary
tests are a merge gate; the frontend job runs unit tests, an AOT production build, and the
Playwright auth-gate e2e smoke.

---

## 3. Current Validation Status by Deliverable

| Deliverable | Status |
| ----------- | ------ |
| VMP | Complete (draft, ready for approval) |
| URS (55 requirements) | Complete |
| FRA | Complete |
| IQ/OQ/PQ protocols | Complete as **executable templates** — not yet executed |
| RTM | Complete; 100% of URS traced to design + verification |
| Development/automated evidence | Present and green (~270 backend, ~37 frontend, e2e) |
| Formal executed & signed IQ/OQ/PQ | **Not performed** |

---

## 4. Outstanding Items Before Production Go-Live

1. **Execute IQ/OQ/PQ formally** in the qualified production-equivalent environment, with
   recorded actual results, evidence attachments, and approver signatures (protocols are
   ready in `03-IQ-OQ-PQ-Protocols.md`).
2. **Supplier / hosting assessment.** Perform and document a supplier assessment of the cloud
   hosting provider and, where relevant, infrastructure qualification (out of scope of the
   application code; required for Category 1 supporting infrastructure and Annex 11 §3.1).
3. **Backup/DR restore drill** executed and evidenced (PQ-04), demonstrating RPO/RTO targets.
4. **Environment IQ evidence** captured: exact .NET 9 / PostgreSQL 17 build numbers,
   migration history hash, RLS `ENABLE`+`FORCE` state, `qams_app` grant matrix (no DELETE),
   secret-store provisioning, TLS.
5. **Standard Operating Procedures** for system use, security administration, periodic
   review, and change control approved by the Quality Unit.
6. **Periodic-review schedule** established (chain verification, access recertification,
   audit-trail review, deviation log).
7. **GAMP 5 traceability formalities**: approver signatures on VMP/URS/FRA/RTM; deviation log
   template instantiated for OQ/PQ execution.
8. **Data migration / initial load validation** (if any legacy data is loaded at go-live).

---

## 5. Capabilities Requested vs Found (transparency note)

All GxP capabilities named in the validation brief were located in the code and are traced in
the RTM. Items that are **configuration/operational** rather than application code — and are
therefore verified at IQ / by restore drill rather than by automated test — are the
backup/DR controls (URS-053/054/055, evidenced by `deploy/BACKUP-RESTORE-DR.md` and the
restore scripts) and the client idle-session timeout (URS-007, frontend behavior confirmed at
OQ). No URS requirement was written for a capability absent from the system.

---

## 6. Conclusion

The NT.QMS validation **framework is complete and internally consistent**: 55 user
requirements, a risk assessment prioritizing the high-risk GxP functions, executable
IQ/OQ/PQ protocols, and a 100%-closed RTM, all grounded in the actual codebase and reinforced
by a completed hardening program (F-01…F-18) and a real-PostgreSQL CI pipeline. Upon formal,
signed execution of the protocols and completion of the outstanding items in §4, NT.QMS will
be positioned for a defensible declaration of validated status for production GxP use.

**Approval (to be signed on formal issue):**

| Activity | Name | Signature | Date |
| -------- | ---- | --------- | ---- |
| Prepared by (Validation Lead) | | | |
| Reviewed by (Quality Unit) | | | |
| Approved by (System Owner) | | | |
