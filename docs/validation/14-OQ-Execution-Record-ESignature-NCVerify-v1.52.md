# Operational Qualification — Execution Record: E-Signature Ceremony on NC Verification (RISK-03 pilot)

| Field | Value |
| ----- | ----- |
| Document ID | OQ-EXEC-NTQMS-004 |
| Protocol executed | REVAL-NTQMS-001 (doc 06) Part A §A.13 — requirement **URS-123** |
| System / version | NT.QMS **v1.52.0** (working tree, pending commit), no migration change |
| Environment | **Development workstation** — API `http://localhost:5080` (Development), PostgreSQL 17 local (`ntqams`, role `qams_app`) |
| Executed by (operator) | Engineering (Claude Code) |
| Witnessed by | _(unsigned — pending)_ |
| Date of execution | 2026-08-06 |
| Test data | Demo laboratory `demo-lab`; operator `admin@demo-lab.local` (TenantAdmin, `019f960f-6ae2-7fde-9c2d-1def183d2afb`); nonconformance `019fd616-df23-7a2b-9491-e2a1e41227cb` (`NC-…`, raised by the operator) |
| Result | **4 automated case-groups green; 3 of 4 live checks executed and passed; 1 live check (positive mint) NOT executed — needs a second operator account** |

> **Scope statement — read before relying on this record.** HTTP status lines and problem+json
> bodies below were **actually observed** in a live session and are transcribed verbatim. The
> automated results were watched to completion. Nothing is inferred from source.
>
> **Declared limitations (must be dispositioned by QA):**
> 1. **Development workstation, not a qualified installation** — this record does not close DOC-001.
> 2. **Independence is limited** — the operator authored the code under test; no witness signature.
> 3. **The positive-mint live case (OQ-ESIG-01/step 4) was not executed live**: a successful
>    verification requires the verifier to differ from the raiser (SoD), i.e. a second tenant
>    operator account holding `nc.sign` **with an e-signature PIN configured**. That path is proven
>    by automated test against the real `ESignatureService` (see §2); the witnessed live execution
>    is left for QA with a two-operator fixture.

---

## 1. Live checks — actual results (dev, real PostgreSQL)

### OQ-ESIG-01 — NC verification is a signing ceremony that fences before it mints

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Operator sets an e-signature PIN (`POST /api/auth/signature-pin`) | 204 | `204` | **Pass** |
| Walk a fresh NC to PendingVerification (submit → triage → rca → plan action → complete → submit-verification) | each 204 | `submit=204 triage=204 rca=204 action=019fd616-e38a-… complete=204 submit-verification=204` | **Pass** |
| Verify **as the raiser** with the correct password + PIN → refused by SoD **before** any signature is minted | 422 `SOD-CAPA-002` | `{"title":"Segregation of duties: the raiser cannot verify their own nonconformance.","status":422,"code":"SOD-CAPA-002",…}` `HTTP=422` | **Pass** |
| Signature manifest for that NC after the refused verify (`GET /api/nonconformances/{id}/signatures`) | `[]` (append-only: nothing minted for a refused ceremony) | `[]` | **Pass** |
| Verify a **Raised** NC (wrong state) with credentials → refused by the state gate before minting | 409 `NC-021` | `{"title":"Cannot verify a nonconformance in state Raised.","status":409,"code":"NC-021",…}` `HTTP=409` | **Pass** |
| Endpoint authorization moved to `nc.sign` | reachable by TenantAdmin (has `nc.sign`) — reaches the ceremony rather than 403 | reached the SoD/state gates (422/409), not 403 | **Pass** |
| **Positive mint** — verify as a second operator (verifier ≠ raiser) with correct credentials → 204 and exactly one manifest entry bound to the record | 204; manifest length 1 | **NOT EXECUTED (see scope limitation 3)** | — |

## 2. Automated evidence (watched to completion, 2026-08-06)

| Suite / test | Asserts | Result |
| ------------ | ------- | ------ |
| `VerifyNcSigningTests.Valid_signature_advances_the_nc_and_records_exactly_one_manifest_entry` | drives the **real** `ESignatureService`: valid password+PIN mints exactly one `signature_record` for subject `NC:{id}`, meaning contains "passed", NC → EffectivenessCheck | Pass |
| `VerifyNcSigningTests.A_wrong_pin_is_refused_and_mints_no_signature_leaving_the_nc_pending` | wrong PIN → SIG-001, zero signatures, NC stays PendingVerification | Pass |
| `VerifyNcSigningTests.The_raiser_cannot_sign_their_own_verification_and_no_signature_is_minted` | raiser == signer → SOD-CAPA-002, zero signatures (pre-mint gate) | Pass |
| `VerifyNcSigningTests.A_verification_in_the_wrong_state_mints_no_signature` | not PendingVerification → NC-021, zero signatures (pre-mint gate) | Pass |
| `SignatureContentHashTests` (5) | determinism, value-sensitivity, order-sensitivity, null≠empty≠marker, delimiter-injection resistance | Pass |
| `EsignDialogComponent` spec (6) | withholds signing until both components present; emits credentials on confirm; no emit while busy; cancel path | Pass |
| `SignatureManifestComponent` spec (2) | renders nothing when empty; one row per signature | Pass |
| Full backend suite | Domain 242 / App 81 / Arch 33 / Integration 31 (+1 skip) / Functional 82 | All green |
| Frontend unit | 95 | All green |

## 3. Disposition

Engineering-complete and evidenced for the negative/gate paths live and the positive path by
automated test. **QA to execute** the witnessed positive-mint case (OQ-ESIG-01, final step) on a
two-operator fixture and sign this record. The authorization upgrade note in doc 06 §A.13 must be
actioned (grant `nc.sign` to any tenant role that previously verified via `nc.approve`) before or
with release.

---

**Signatures** _(left blank — execution and QA review by a human; engineering does not self-certify)_

| Role | Name | Signature | Date |
| ---- | ---- | --------- | ---- |
| Operator | | | |
| Witness / QA | | | |
| System Owner | | | |
