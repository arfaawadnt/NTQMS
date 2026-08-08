# Operational Qualification — Execution Record: Quality Analytics Report Export (URS-130)

| Field | Value |
| ----- | ----- |
| Document ID | OQ-EXEC-NTQMS-006 |
| Protocol executed | REVAL-NTQMS-001 (doc 06) Part A — requirement **URS-130** |
| System / version | NT.QMS **v1.53.x** (working tree, pending commit) — no migration, no schema change |
| Environment | **Development workstation** — API `http://localhost:5080` (Development), PostgreSQL 17 local (`ntqams`, role `qams_app`) |
| Executed by (operator) | Engineering (Claude Code) |
| Witnessed by | _(unsigned — pending)_ |
| Date of execution | 2026-08-08 |
| Test data | Demo laboratory `demo-lab`; operator `admin@demo-lab.local` (TenantAdmin) |
| Result | **4 automated case-groups green; 3 real-pipeline functional cases green; live positive export executed and passed for both formats** |

> **Scope statement.** HTTP status lines and response headers below were **actually observed** in a
> live session and are transcribed verbatim; the automated results were watched to completion.
>
> **Declared limitations (must be dispositioned by QA):**
> 1. **Development workstation, not a qualified installation** — this record does not close DOC-001.
> 2. **Independence is limited** — the operator authored the code under test; no witness signature.

---

## 1. Live checks — actual results (dev, real PostgreSQL)

### OQ-QAREP-01 — the analytics dashboard exports a branded report in both formats

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Export PDF / Export Excel buttons appear on the Quality Analytics dashboard, gated `reports.export` | both buttons render for a TenantAdmin | both rendered (`Export PDF`, `Export Excel`) | **Pass** |
| Click Export PDF → `GET /api/exports/quality-analytics.pdf` | 200, `application/pdf` | `GET /api/exports/quality-analytics.pdf → 200 OK`; `Content-Type: application/pdf`; `Content-Length: 67014`; `Content-Disposition: attachment; filename=quality-analytics-…​.pdf`; body begins `%PDF-` | **Pass** |
| Click Export Excel → `GET /api/exports/quality-analytics.xlsx` | 200, spreadsheet content-type | `GET /api/exports/quality-analytics.xlsx → 200 OK`; XLSX (PK-zip), 17 583 bytes | **Pass** |
| Unauthenticated caller | refused | `GET /api/exports/quality-analytics.pdf → 401` (functional test) | **Pass** |
| Each export recorded as a security event | `RECORD_EXPORTED` written | logged via `LogExportAsync("quality-analytics.pdf/.xlsx")` | **Pass** |

## 2. Automated evidence (watched to completion, 2026-08-08)

| Suite / test | Asserts | Result |
| ------------ | ------- | ------ |
| `ExportServiceTests.Analytics_report_pdf_is_a_genuine_pdf` (2 cases) | a fully-populated and an all-empty `QualityAnalyticsDto` both render a `%PDF-` document > 1 KB — proving the gauge, progress bars, Pareto bars and 5×5 matrix compose without a QuestPDF zero-weight/overflow layout fault | Pass |
| `ExportServiceTests.Analytics_report_xlsx_is_a_genuine_workbook` (2 cases) | both DTOs render a genuine PK-zip workbook > 1 KB | Pass |
| `QualityAnalyticsExportTests.Pdf_endpoint_returns_a_genuine_pdf_to_an_authorized_caller` | real HTTP + real PostgreSQL: 200, `application/pdf`, `%PDF-` | Pass |
| `QualityAnalyticsExportTests.Xlsx_endpoint_returns_a_genuine_workbook_to_an_authorized_caller` | 200, spreadsheet content-type, PK-zip | Pass |
| `QualityAnalyticsExportTests.Unauthenticated_caller_is_refused` | 401 | Pass |
| `ApiSurface` snapshot | the two new routes (+ versioned twins) added and reviewed as a public-contract change | Pass |
| Full backend suite | Domain 245 / App 97 / Arch 33 / Integration 31 (+1 skip) / Functional 85 = **491** | All green (real PG) |
| Frontend production build | clean | Pass |

## 3. Disposition

Engineering-complete and evidenced end-to-end, including the positive live export of both formats.
The report re-computes strictly under the caller's own view permissions and branch/department scope
(a section the caller cannot see is absent from the report), so the export cannot widen the caller's
access — it is a Part 11 §11.10(b) copy of the analytics. No new permission key is introduced
(`reports.export` already exists), so there is **no tenant authorization upgrade action**. **QA to
review and sign.**

---

**Signatures** _(left blank — execution and QA review by a human; engineering does not self-certify)_

| Role | Name | Signature | Date |
| ---- | ---- | --------- | ---- |
| Operator | | | |
| Witness / QA | | | |
| System Owner | | | |
