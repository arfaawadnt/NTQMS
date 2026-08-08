# Operational Qualification — Execution Record: User Manual PDF Export (URS-131)

| Field | Value |
| ----- | ----- |
| Document ID | OQ-EXEC-NTQMS-007 |
| Protocol executed | REVAL-NTQMS-001 (doc 06) Part A — requirement **URS-131** |
| System / version | NT.QMS **v1.53.x** (working tree, pending commit) — no migration, no schema change |
| Environment | **Development workstation** — API `http://localhost:5080` (Development), PostgreSQL 17 local (`ntqams`, role `qams_app`) |
| Executed by (operator) | Engineering (Claude Code) |
| Witnessed by | _(unsigned — pending)_ |
| Date of execution | 2026-08-08 |
| Test data | Demo laboratory `demo-lab`; operator `admin@demo-lab.local` (TenantAdmin) |
| Result | **1 render smoke case + 3 real-pipeline functional cases green; live export executed and passed** |

> **Scope statement.** The HTTP status line below was **actually observed** in a live session; the
> automated results were watched to completion.
>
> **Declared limitations (must be dispositioned by QA):**
> 1. **Development workstation, not a qualified installation** — this record does not close DOC-001.
> 2. **Independence is limited** — the operator authored the code under test; no witness signature.

---

## 1. Live checks — actual results (dev)

### OQ-MANUAL-01 — the User Manual exports as a professional PDF

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| An "Export PDF Manual" button appears on `/manual` | button renders | rendered (`Export PDF Manual`) | **Pass** |
| Click it → the SPA assembles the localized manual and posts it → `POST /api/exports/manual.pdf` | 200, `application/pdf` | `POST /api/exports/manual.pdf → 200 OK` | **Pass** |
| Direct render for the record (representative payload via API) | a genuine multi-page PDF | `200 OK`, `Content-Type: application/pdf`, `Content-Length: 79822`, body begins `%PDF-` | **Pass** |
| Each export recorded as a security event | `RECORD_EXPORTED` written | logged via `LogExportAsync("manual.pdf")` | **Pass** |

## 2. Automated evidence (watched to completion, 2026-08-08)

| Suite / test | Asserts | Result |
| ------------ | ------- | ------ |
| `ExportServiceTests.Manual_pdf_is_a_genuine_pdf_document` | a manual with step-bearing and step-less topics renders a `%PDF-` document > 1 KB — proving the linked-TOC section cross-references (`SectionLink` / `BeginPageNumberOfSection`) resolve at generation | Pass |
| `ManualExportTests.Manual_endpoint_returns_a_genuine_pdf_to_an_authenticated_caller` | real HTTP + real PostgreSQL: 200, `application/pdf`, `%PDF-` | Pass |
| `ManualExportTests.Empty_manual_payload_is_rejected_before_rendering` | empty payload → 422 `EXPORT-003` (guarded before rendering) | Pass |
| `ManualExportTests.Unauthenticated_caller_is_refused` | 401 | Pass |
| `ApiSurface` snapshot | `POST /api/exports/manual.pdf` (+ versioned twin) added and reviewed | Pass |
| Full backend suite | Domain 245 / App 98 / Arch 33 / Integration 31 (+1 skip) / Functional 88 = **495** | All green (real PG) |
| Frontend production build + unit | clean + 95 Karma | Pass |

## 3. Disposition

Engineering-complete and evidenced end-to-end, including a live export. The manual content is the
SPA's help catalogue, posted already localized; the server lays it out and stamps provenance — it
does not author the content, and the endpoint is auth-only (a copy of the caller's own manual view),
so no permission gate or catalogue key is introduced. **QA to review and sign.**

---

**Signatures** _(left blank — execution and QA review by a human; engineering does not self-certify)_

| Role | Name | Signature | Date |
| ---- | ---- | --------- | ---- |
| Operator | | | |
| Witness / QA | | | |
| System Owner | | | |
