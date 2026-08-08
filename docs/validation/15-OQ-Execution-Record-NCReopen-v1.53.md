# Operational Qualification — Execution Record: NC Re-open Signing Ceremony (URS-129)

| Field | Value |
| ----- | ----- |
| Document ID | OQ-EXEC-NTQMS-005 |
| Protocol executed | REVAL-NTQMS-001 (doc 06) Part A — requirement **URS-129** |
| System / version | NT.QMS **v1.53.x** (working tree, pending commit) — migration `AddNcReopenReason` (nullable `text`) |
| Environment | **Development workstation** — API `http://localhost:5080` (Development), PostgreSQL 17 local (`ntqams`, role `qams_app`) |
| Executed by (operator) | Engineering (Claude Code) |
| Witnessed by | _(unsigned — pending)_ |
| Date of execution | 2026-08-08 |
| Test data | Demo laboratory `demo-lab`; operator `admin@demo-lab.local` (TenantAdmin); NC register (no Closed NC present in the dev dataset at execution time) |
| Result | **3 automated case-groups green against the real `ESignatureService`; 1 live backward-compatibility check executed and passed; the witnessed positive-reopen live case NOT executed — needs a Closed NC and a PIN-holding signer** |

> **Scope statement — read before relying on this record.** The automated results were watched to
> completion on real PostgreSQL. The live observation is transcribed from a real session. Nothing is
> inferred from source.
>
> **Declared limitations (must be dispositioned by QA):**
> 1. **Development workstation, not a qualified installation** — this record does not close DOC-001.
> 2. **Independence is limited** — the operator authored the code under test; no witness signature.
> 3. **The positive-reopen live case (OQ-REOPEN-01/step 3) was not executed live**: the dev dataset
>    holds **no Closed nonconformance**, and manufacturing one requires walking an NC through
>    verify + effectiveness with a signer who is **not** the raiser (SoD) and who holds an
>    e-signature PIN — a two-operator, PIN-configured fixture. The re-open ceremony itself is proven
>    by automated test against the real `ESignatureService` (see §2); the witnessed live execution is
>    left for QA.

---

## 1. Live checks — actual results (dev, real PostgreSQL)

### OQ-REOPEN-01 — Re-open is a reasoned signing ceremony offered only for a closed NC

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| The shared e-signature dialog stays backward-compatible for the existing callers (no reason field unless requested) | NC **verify** dialog renders account password + signature PIN only, no reason textarea | dialog open, labels `["Account password","Signature PIN"]`, `hasReasonTextarea=false`, meaning `"I verify that the corrective action on NC-2026-0023 is effective (passed)."` | **Pass** |
| A Closed NC shows a `nc.sign`-gated **Re-open** action that opens the dialog with a mandatory reason field | reason textarea present and required; Sign disabled until reason + password + PIN all present | **NOT EXECUTED — no Closed NC in the dev dataset (see scope limitation 3)** | — |
| **Positive reopen** — sign with correct password + PIN and a reason → 204, NC returns to ActionPlan, exactly one new manifest entry bound to `NC:{id}` whose meaning carries the reason | 204; status ActionPlan; manifest +1 with the reason | **NOT EXECUTED (see scope limitation 3)** | — |

## 2. Automated evidence (watched to completion, 2026-08-08)

| Suite / test | Asserts | Result |
| ------------ | ------- | ------ |
| `ReopenNcSigningTests.Valid_signature_reopens_the_nc_and_records_the_reason_in_the_manifest` | drives the **real** `ESignatureService`: valid password+PIN re-opens a Closed NC → ActionPlan, `ReopenReason` stored, exactly one `signature_record` for subject `NC:{id}` whose meaning contains the reason | Pass |
| `ReopenNcSigningTests.A_wrong_pin_is_refused_and_mints_no_signature_leaving_the_nc_closed` | wrong PIN → SIG-001, zero signatures, NC stays Closed | Pass |
| `ReopenNcSigningTests.A_reopen_in_the_wrong_state_mints_no_signature` | not Closed → NC-023, zero signatures (pre-mint gate) | Pass |
| `NonconformanceTests.Reopen_returns_closed_nc_to_action_plan_and_records_reason` | Closed → ActionPlan, reason recorded, `NcReopened` raised with the actor | Pass |
| `NonconformanceTests.Reopen_requires_a_reason` | blank reason → NC-024, state unchanged | Pass |
| `NonconformanceTests.Reopen_only_from_closed` | from Raised → NC-023, state unchanged | Pass |
| `CommandPolicyTests` | `ReopenNcCommand` carries exactly one authorization policy (`nc.sign`) | Pass |
| `ApiSurface` snapshot | `POST /api/nonconformances/{id}/reopen` (+ versioned twin) added and reviewed as a public-contract change | Pass |
| Full backend suite | Domain 245 / App 93 / Arch 33 / Integration 31 (+1 skip) / Functional 82 = **484** | All green (real PG) |
| Frontend unit | 95 | All green |

## 3. Disposition

Engineering-complete and evidenced: the re-open ceremony (state gate before mint, reason bound to
the signature, no signature on refusal) is proven by automated test against the real
`ESignatureService`, and the shared e-sign dialog is confirmed live to be backward-compatible with
the existing verify/close callers. **QA to execute** the witnessed positive-reopen case
(OQ-REOPEN-01, steps 2–3) on a fixture that first produces a Closed NC (two-operator, PIN-configured)
and sign this record. No new permission key is introduced — re-open reuses `nc.sign` — so there is
**no tenant authorization upgrade action** for this requirement.

---

**Signatures** _(left blank — execution and QA review by a human; engineering does not self-certify)_

| Role | Name | Signature | Date |
| ---- | ---- | --------- | ---- |
| Operator | | | |
| Witness / QA | | | |
| System Owner | | | |
