# Operational Qualification — Execution Record (Witnessed Session)

| Field | Value |
| ----- | ----- |
| Document ID | OQ-EXEC-NTQMS-001 |
| Protocol executed | REVAL-NTQMS-001 (doc 06) Part C — OQ manual/witnessed cases |
| System / version | NT.QMS **v1.49.0**, commit `1beb3bf` (CI all-green), Angular 22.0.8 SPA |
| Environment | **Development workstation** — API `http://localhost:5080` (ASPNETCORE_ENVIRONMENT=Development), SPA `http://localhost:4200`, PostgreSQL 17 local (`ntqams`, role `qams_app`) |
| Executed by (operator) | Engineering (Claude Code), executing at the System Owner's direction |
| Witnessed by | A. Awad — System Owner / acting QA authority (real-time session, 2026-07-29) |
| Date of execution | 2026-07-29, 07:39–07:52 local |
| Test data | Demo tenant `demo-lab`, user `admin@demo-lab.local` (TenantAdmin) |

> **Scope statement — read before relying on this record.** Every result below was
> **actually observed** during a live session; nothing is inferred, predicted, or copied
> from a test suite. Actual outputs (HTTP status lines, problem+json bodies, SQL query
> results, captured request headers) are transcribed verbatim.
>
> **Declared limitations of this execution (must be dispositioned by QA before this record
> can support a validation claim):**
> 1. **Environment is a development workstation, not a qualified/staging installation.**
>    The Part B IQ steps that require a deployed host (IQ-17 Production role-guard boot
>    refusal, IQ-18 TLS/HSTS at proxy, IQ-19 deployed non-root image, IQ-23 observability
>    stack) were **NOT** executed here.
> 2. **Independence is limited**: the operator is the same party that authored the code, and
>    the witness is the same person as the System Owner and QA authority. An external
>    assessor will note the absence of segregation of duties.
> 3. **One deviation was recorded** (§3, DEV-01) — an intentional test-data intervention.
> 4. **Part D (PQ) was not executed** — load, soak, and alert-fires drills require staging.

---

## 1. Executed cases and actual results

### OQ-API-01 — Every error response is `application/problem+json` with a stable code

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Validation error (POST `/api/nonconformances`, empty title, severity 9) | 400, problem+json, field errors | `HTTP/1.1 400 Bad Request`; `Content-Type: application/problem+json`; body `{"title":"Validation failed.","status":400,"errors":{"Title":["'Title' must not be empty."],"Severity":["'Severity' must be between 1 and 5. You entered 9."],"Likelihood":[...]},"traceId":"ccd6c779…","correlationId":"ccd6c779…"}` | **Pass** |
| Unauthenticated (no bearer) | 401, problem+json, stable code | `HTTP/1.1 401 Unauthorized`; problem+json; `{"title":"Authentication is required.","status":401,"code":"AUTH-401",…}` | **Pass** |
| Not found (random GUID) | 404, problem+json, stable code | `HTTP/1.1 404 Not Found`; problem+json; `{"title":"Nonconformance not found.","status":404,"code":"NC-404",…}` | **Pass** |

### OQ-API-02 — Bounded pagination envelope; page size clamped (no silent caps)

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| `GET /api/v1/nonconformances?page=1&pageSize=5` | Envelope w/ items/total/page/pageSize/hasMore | `items=5  total=15  page=1  pageSize=5  hasMore=true` | **Pass** |
| Same with `pageSize=100000` | Clamped, not honoured verbatim | `items=15  total=15  page=1  pageSize=200  hasMore=false` — **clamped to 200** | **Pass** |

### OQ-API-03 — Upload allow-list + magic-byte sniffing; forced attachment download

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Upload MZ-header executable renamed `.pdf`, declared `application/pdf` | Rejected, stable code | `HTTP/1.1 422 Unprocessable Entity`; `{"title":"The content does not match the .pdf file signature.","status":422,"code":"FILE-415",…}` | **Pass** |
| Upload genuine PDF | Accepted, canonical metadata | `HTTP/1.1 201 Created`; `{"id":"019fac2c-d490-7bad-8f3c-51cc4e246db8","fileName":"good.pdf","sha256":"383901304b98a9…","sizeBytes":81}` | **Pass** |
| Download the stored file | 200, forced attachment, byte-exact | `HTTP/1.1 200 OK`; `Content-Disposition: attachment; filename=good.pdf; filename*=UTF-8''good.pdf`; `Content-Type: application/pdf`; 81 bytes received (81 uploaded) | **Pass** |

### OQ-API-05 — `Idempotency-Key` replay nets exactly one side-effect

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| POST NC twice with identical `Idempotency-Key: oq-final-1553` | One record; 2nd call replays 1st response | call 1 `201 Created id=019fac2d-b6a2-7df4-a394-896d8c5376b6`; call 2 `201 Created id=019fac2d-b6a2-7df4-a394-896d8c5376b6` — **identical id** | **Pass** |
| DB proof | Exactly 1 row | `SELECT count(*) … WHERE title='OQ-WITNESSED-FINAL idempotency probe 1553'` → **1** | **Pass** |
| Idempotency ledger | 1 record, correct request type | 1 record; `request_type=NT.QAMS.Application.Improvement.Commands.RaiseNcCommand` | **Pass** |

### OQ-SEC-12 — Defensive header set on success **and** error responses

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| `GET /api/nonconformances` (200) | Full header set | `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'`; `X-Content-Type-Options: nosniff`; `X-Frame-Options: DENY`; `Referrer-Policy: no-referrer` | **Pass** |
| Same on a 404 error response | Identical set (not dropped on errors) | Identical four headers present on `404` | **Pass** |
| HSTS | Absent in Development, present at TLS host | Absent here — **expected**, Development over plain HTTP (ADR-0002). TLS/HSTS = **IQ-18, not executed** | n/a (deferred) |

### OQ-API-04 / OQ-SEC-15 — Deny-by-default authorization, denial shape

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| `GET /api/tenants` (PlatformAdmin-only) as TenantAdmin | 403 problem+json, no data leak | `HTTP/1.1 403 Forbidden`; `Content-Type: application/problem+json`; `{"title":"You do not have permission to perform this action.","status":403,"code":"AUTHZ-403",…}` | **Pass** |

### OQ-OBS-02 — Correlation id echoed; traceId in problem body

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Request with `X-Correlation-Id: oq-final-corr-1553`, forced 404 | Echoed + present in body | Response header `X-Correlation-Id: oq-final-corr-1553`; body `status=404 code=NC-404 traceId=f7d53cb759059533578838298223dbfc correlationId=oq-final-corr-1553` | **Pass** |

### OQ-MSG-01 — Concurrent mutation of one record: exactly one succeeds

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Two simultaneous `POST /{id}/submit` on the same NC | One succeeds, other rejected with 409 | call A `204`; call B `409` — `{"title":"Cannot submit a nonconformance in state Raised.","status":409,"code":"NC-010",…}` | **Pass, with note** |

> **Note (accuracy):** the 409 observed here was raised by the **aggregate state-machine
> guard** (`NC-010`), not the `xmin` optimistic-concurrency token (`CONCURRENCY-409`). The
> case's intent — no silent lost update under concurrency — is demonstrated. The specific
> `xmin`→`CONCURRENCY-409` path is evidenced by the automated suites
> (`IntegrationTests/OptimisticConcurrencyTests`, `WebApi.FunctionalTests/ConcurrencyConflictMappingTests`)
> and was **not** reproduced manually in this session.

### OQ-SEC-11 — Credential endpoint rate limiting

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Burst `POST /api/auth/login` with wrong password | 429 + `Retry-After` after the budget | attempts 1–9 → `401`; **attempt 10 → `429`** with `Retry-After: 60` | **Pass** |
| (Unplanned, additional evidence) account lockout | — | After 5 failures the account locked: `{"title":"Account is temporarily locked. Try again later.","status":401,"code":"AUTH-004"}`; policy `MaxFailedAttempts=5`, `LockoutMinutes=30` (`UserAccount.cs`) | **Pass (additional control confirmed)** |

### OQ-DEP-04 — Fail-fast configuration validation

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Start API with `PasswordPolicy__MaxAgeDays=not-a-number` | Startup fails naming the key | Process refused to start: `System.InvalidOperationException: Configuration 'PasswordPolicy:MaxAgeDays' has invalid value 'not-a-number' — expected an integer. Refusing to start rather than silently applying the default (CFG-002).` at `ConfigGuard.ReadInt` | **Pass** |

### OQ-UI-01 — Part 11 reason-for-change capture (accessible, no `window.prompt`)

Executed in the live Angular 22 SPA against the running API.

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Trigger a reason-required action (place legal hold, ARC-2026-0001) | Accessible modal, not `window.prompt` | `role="dialog"`, `aria-modal="true"`, heading "Place hold", notice "This action is audited (21 CFR Part 11). A reason is recorded with the change.", labelled **Reason** textarea, Confirm/Cancel | **Pass** |
| Focus management | Focus moves into the dialog | Reason `<textarea>` held document focus on open (`textareaFocused: true`) | **Pass** |
| Empty reason | Cannot proceed | Confirm button `disabled: true` while reason empty; enabled after typing | **Pass** |
| Keyboard dismissal | Escape closes | After `Escape`, reason dialogs in DOM = **0** | **Pass** |
| Reason transmitted (DELETE path) | Sent as `X-Change-Reason` | Captured on the wire: `DELETE /api/archives/019f965d-…/legal-hold` with `X-Change-Reason: "OQ-WITNESSED release of legal hold - REVAL-NTQMS-001 evidence"` (header keys: Authorization, **X-Change-Reason**, Accept) | **Pass** |
| Reason transmitted (POST path) | Reason captured | `POST …/legal-hold` sends the reason in the request **body** (`{reason}`) per `archives-api.service.ts` — by design; the header interceptor covers DELETE only | **Pass (design clarified)** |
| Reason persisted | Recorded against the change | `audit.field_change` rows: `ArchiveEntry \| Modified \| prop=IsOnLegalHold \| reason=OQ-WITNESSED release of legal hold - REVAL-NTQMS-001 evidence` (also LegalHoldReason, LegalHoldPlacedBy, ModifiedAtUtc) | **Pass** |

### OQ-SEC-13 — Session model (ADR-0009): memory-only token, session survives reload, sign-out revokes

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Token storage after sign-in | No token in web storage; refresh cookie not JS-readable | `localStorage` keys = `["qams.tenant.slug"]` only; `sessionStorage` = `[]`; `document.cookie` = **(none)** → refresh cookie is httpOnly | **Pass** |
| Reload / deep-link while signed in | Session survives via silent refresh | Navigated to `/nonconformances` after reload — remained authenticated, list rendered (17 NCs), 0 console errors | **Pass** |
| Sign out | Session ends, redirect, server-side revocation | `POST /api/auth/logout` observed; redirected to `/login` with sign-in form; `audit.security_event` → `LOGOUT` at 07:51:56; `qams.refresh_session` → 6 of 10 revoked | **Pass** |

### OQ-AUD — Hash-chained audit trail integrity (Part 11 §11.10(e))

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Session actions appear in the ledger | New entries recorded | `seq=60 NcRaised` (07:41:46), `seq=61 ArchiveLegalHoldPlaced` (07:50:04), `seq=62 ArchiveLegalHoldReleased` (07:51:18) | **Pass** |
| Chain continuity | Each `prev_hash` = prior `entry_hash`; no breaks | `total entries checked: 81 | breaks found: 0` | **Pass** |
| Security events logged | Sign-in/out and failures recorded | `LOGOUT`, `LOGIN_SUCCESS` ×2, `LOGIN_FAILED` ×3 with timestamps | **Pass** |

### Supporting: OQ-SCA — supply-chain gates (v1.49 additions)

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| `npm audit --omit=dev` on shipped lock | 0 high/critical | `{"info":0,"low":0,"moderate":0,"high":0,"critical":0,"total":0}` | **Pass** |
| Exception register | Empty or fully justified | `.github/npm-audit-allowlist.txt` — **0 advisory URLs** | **Pass** |

---

## 2. Cases NOT executed in this session (remain open)

| Ref | Case | Reason not executed |
| --- | ---- | ------------------- |
| OQ-DEP-01 | Production boot refusal on over-privileged DB role | Requires a Production-configured host + privileged role (IQ-17) |
| OQ-DEP-02 | `/health/ready` → 503 with PostgreSQL stopped | Requires stopping the DB service (elevation); covered by `scripts/failure-drills.ps1` Drill 1 + `HealthEndpointTests` |
| OQ-MSG-02 | Poison outbox event → dead-letter | Previously executed via `failure-drills.ps1` Drill 2 (not re-run in this session) |
| OQ-SEC-14 | Refresh-token reuse → family revocation | Previously proven live via `security-probe-deep.ps1` + `RefreshSessionTests`; not re-run manually here |
| OQ-SEC-15 (full matrix) | All 6 roles × gated surface | One representative role pair executed here; full matrix is `RoleEndpointMatrixTests` (automated) |
| PQ-PERF-01/02, PQ-OBS-01 | Load, 24 h soak, alert-fires | Staging only (Part D) |
| IQ-17/18/19/23 | Deployed-host install checks | Qualified/staging environment required |

---

## 3. Deviations recorded during execution

| # | Deviation | Impact assessment | Disposition |
| - | --------- | ----------------- | ----------- |
| **DEV-01** | During OQ-SEC-11, the credential burst locked the test account (control working as designed). To continue the session without a 30-minute wait, the operator cleared the lock directly in the **development** database: `UPDATE qams.user_account SET locked_until_utc=NULL, failed_login_attempts=0 WHERE email='admin@demo-lab.local'`. | Test-data state only; no application code, configuration, or audit record was altered. The lockout control itself was **observed working** before the reset and is recorded as evidence. Executed on a dev database, not a regulated instance. | Accepted — disclosed here in full. QA to confirm acceptability; on a qualified environment, either wait out the lockout or use a dedicated throwaway account. |
| **OBS-01** (observation, not a deviation) | The manual concurrency case produced a state-machine 409 (`NC-010`) rather than the `xmin` `CONCURRENCY-409`. | The requirement's intent is met; the specific token path rests on automated evidence. | Accepted with note; QA may script a same-version double-PUT on the qualified environment if the specific path must be manually witnessed. |

---

## 4. Result summary

- **Cases executed: 12** (OQ-API-01/02/03/04-05, OQ-SEC-11/12/13/15-representative, OQ-OBS-02, OQ-MSG-01, OQ-DEP-04, OQ-UI-01, OQ-AUD, OQ-SCA)
- **Passed: 12** — including 2 with recorded notes (OQ-MSG-01 mechanism note; OQ-UI-01 POST-body vs DELETE-header clarification)
- **Failed: 0**
- **Additional controls confirmed unplanned: 1** (account lockout, AUTH-004)
- **Deviations: 1** (DEV-01, disclosed above)
- **Defects raised: 0**

No test in this session revealed a functional or security defect. All harness problems encountered during execution (MSYS path handling, JSON parsing, an initial mis-click, RLS-hidden verification queries) were **operator/tooling issues**, were corrected, and the affected cases were re-executed to completion — they are not product findings.

---

## 5. Signatures

By signing, the witness confirms that the cases in §1 were executed in their presence, that
the transcribed actual results match what was observed, and that the limitations in the
scope statement and the deviation in §3 have been read and dispositioned.

| Role | Name | Signature | Date |
| ---- | ---- | --------- | ---- |
| Executed by (operator) | Engineering — Claude Code (automated operator) | *n/a — machine-executed; results transcribed verbatim* | 2026-07-29 |
| Witnessed by | A. Awad (System Owner / acting QA authority) | ____________________ | __________ |
| Reviewed & approved by (QA) | | ____________________ | __________ |

> Engineering applies no signature on QA's or the System Owner's behalf. Until the witness
> line is signed, this document is an **unsigned execution transcript**: the results are
> real, the attestation is pending. Doc 06 (REVAL-NTQMS-001) is closed only when the
> remaining §2 items are executed on a qualified environment and the VSR addendum is signed.
