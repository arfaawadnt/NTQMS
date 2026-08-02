# AUTH — Detailed Test Cases, Batch D

This batch consumes `TC-AUTH-BVA-001` … `TC-AUTH-BVA-016` and `TC-AUTH-API-060` … `TC-AUTH-API-081` (38 cases) and covers exactly four slices of the AUTH module: (1) the shared password-strength rule `PasswordRules.StrongPassword()` — boundary-value analysis at 11/12/13 and 200/201 characters, each of the four character classes omitted in turn, a blocklisted password, whitespace-only input, Unicode input, and proof that the same rule is reused by administrative reset and registration; (2) the password lifecycle around that rule — self-service change, the `HistoryDepth` reuse ban and the `saas.password_history` prune, and administrative reset; (3) the anonymous workspace-lookup anti-enumeration contract on `GET /api/auth/workspace/{slug}` — name-only payload, identical `404` for unknown / malformed / suspended slugs, and the slug-normalisation and slug-length boundaries; (4) the `auth` rate-limit partition (10 requests per minute per client address) — the 10/11 boundary, the `429` wire contract, poisoning of the shared partition across the whole `AuthController`, the separate `refresh` partition, forwarded-address repartitioning, and the fixed-window reset. It deliberately leaves to sibling batches: all `UserAccount`/`RefreshSession`/`UserAccessReview` unit invariants and the PIN/TOTP/lockout-counter boundaries (batch A); the login, MFA, refresh and logout endpoint matrices and the four decision tables (batch B); the three state machines and the handler↔PostgreSQL integration set (batch C); the RLS assertions on the six AUTH tables, the e-signature partition and the reuse-detection security cases (also batch D's `TC-AUTH-SEC-*`/`TC-AUTH-RLS-*` reservations, which this pass does **not** consume); the secret-dataflow, MC/DC and observability sets (batch E); and every browser, UAT, accessibility, performance and exploratory case (batch F).

**Risk IDs.** `docs/validation/02-Functional-Risk-Assessment.md` is area-level and mints no per-item risk identifiers, so this batch mints its own, as conventions §5 permits: `RSK-AUTH-010` weak or reusable password accepted · `RSK-AUTH-011` pre-authentication tenant-state disclosure · `RSK-AUTH-012` reuse ban desynchronised from the retained history · `RSK-AUTH-013` credential-guessing throttle absent, evadable or self-denying · `RSK-AUTH-014` error-contract drift on throttled responses. All five sit inside the FRA's **Authentication hardening** (URS-001…004, 007, risk priority High) and **Security event logging** (URS-016, 019, Medium) areas.

---

## Password strength — `PasswordRules.StrongPassword()`

All sixteen cases below drive the rule through `POST /api/auth/change-password`, which is the only anonymous endpoint that applies it (`ChangePasswordValidator`, `src/NT.QAMS.Application/IdentityAccess/Commands/Login.cs:170`), except `TC-AUTH-BVA-016`, which proves the same rule object is reused by the two privileged endpoints. The validator chain is `NotEmpty → MinimumLength(12) → MaximumLength(200) → Must(HasComplexity) → Must(NotCompromised)` (`src/NT.QAMS.Application/IdentityAccess/PasswordRules.cs:45-53`); FluentValidation's default rule-level cascade is `Continue`, so **every** failing validator in the chain contributes a message and the assertions below count them.

#### TC-AUTH-BVA-001 — An 11-character password is one below the minimum and is refused  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Functional (negative) · BVA — lower boundary − 1 on `PasswordRules.MinLength = 12` (`PasswordRules.cs:17`) |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — `POST /api/auth/change-password` is `[AllowAnonymous]` (`AuthController.cs:110-111`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; `PasswordPolicy:MaxAgeDays=90`, `PasswordPolicy:HistoryDepth=5` (`appsettings.json:8-11`) |
| **Preconditions** | Fixture account `pwbva@demo-lab.local` exists, `is_active = true`, `locked_until_utc IS NULL`, current password `Qms-Batch-Cur1!`; created by `admin@demo-lab.local` via `POST /api/users` (`UsersController.cs:27-31`, `users.manage`). |
| **Test Data** | `{"tenantIdentifier":"demo-lab","email":"pwbva@demo-lab.local","currentPassword":"Qms-Batch-Cur1!","newPassword":"Qms-Batch1!"}` — the new password is exactly 11 characters (`Q m s - B a t c h 1 !`) and satisfies all four classes. |
| **Steps** | 1. `POST /api/auth/change-password` with the payload above. 2. Assert status `400` and `Content-Type: application/problem+json`. 3. Parse the body and read `errors.NewPassword`. 4. `SELECT password_hash, password_changed_at_utc FROM qams.user_account WHERE email='pwbva@demo-lab.local';` and compare `password_hash` against the value captured in the precondition. 5. `SELECT count(*) FROM saas.password_history WHERE user_id = <fixture id>;`. |
| **Expected UI** | On the `/t/demo-lab` sign-in page in the expired-password state, the inline alert (`login.component.ts:135`, `role="alert"`) reads exactly `Validation failed.` — the SPA renders `err.error?.title` only and never the `errors` array (`login.component.ts:456`). The new-password field stays populated and enabled. |
| **Expected API** | `400` `application/problem+json`; `title` = `Validation failed.`; **no** `code` extension (the `ValidationException` arm of `DomainExceptionHandler.cs:34-44` sets `errors` only); `errors.NewPassword` contains exactly one message, `The password must be at least 12 characters.` (`PasswordRules.cs:48`); `traceId` present (`Program.cs:174-182`). |
| **Expected DB** | `qams.user_account.password_hash` byte-identical to the precondition value; `password_changed_at_utc` unchanged; `failed_login_attempts` still `0`; `locked_until_utc` still `NULL`. |
| **Expected Audit** | Zero new rows in `audit.field_change` for `entity_type='UserAccount'` and this `entity_id` — the request is rejected in `ValidationBehavior` (`Behaviors/ValidationBehavior.cs:34-37`) before the handler and therefore before any `SaveChanges`. Zero new rows in `audit.security_event` (read after `SELECT set_config('app.bypass_rls','on',false);`). |
| **Expected Notification** | n/a — grepped `src/`: no `INotificationHandler` or notification policy subscribes to any AUTH domain event or security-event type. |
| **Cleanup** | None — the request changes nothing. Retain the fixture account for `TC-AUTH-BVA-002`…`-015`. |
| **Evidence** | HTTP response capture (status, headers, full body) · SQL result set for step 4 · row counts for step 5 |
| **Notes** | Assert the message string verbatim: it is a `.WithMessage($"…{MinLength}…")` interpolation, so a change to `MinLength` changes the text and the test must fail loudly. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-002 — A 12-character password sits exactly on the minimum and is accepted  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Functional (positive) · BVA — on the lower boundary `MinLength = 12` |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `pwbva@demo-lab.local` active, unlocked, current password `Qms-Batch-Cur1!`. Capture `password_hash` and `password_changed_at_utc` before the call. |
| **Test Data** | `newPassword` = `Qms-Batch12!` — exactly 12 characters, upper `Q,B`, lower `m,s,a,t,c,h`, digit `1,2`, symbols `-,!`; not on the 62-entry compromised list (`PasswordRules.cs:27-39`). |
| **Steps** | 1. `POST /api/auth/change-password` with `tenantIdentifier=demo-lab`, `email=pwbva@demo-lab.local`, `currentPassword=Qms-Batch-Cur1!`, `newPassword=Qms-Batch12!`. 2. Assert `204` and an empty body. 3. `SELECT password_hash, password_changed_at_utc, failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='pwbva@demo-lab.local';`. 4. `SELECT count(*), max(set_at_utc) FROM saas.password_history WHERE user_id = <fixture id>;`. 5. `POST /api/auth/login` with `password=Qms-Batch12!` and assert `200`. |
| **Expected UI** | The SPA's expired-password branch clears `passwordExpired`, copies the new password into the sign-in field and immediately re-submits `POST /api/auth/login` (`login.component.ts:447-452`), so the user lands on `/dashboard` without a second prompt. |
| **Expected API** | Step 1 → `204 No Content`, no body, no `Set-Cookie`. Step 5 → `200` with `AuthResponse` carrying a non-empty `accessToken`, `mfaRequired:false`, and a `Set-Cookie: qams_rt=…; path=/api/auth; secure; httponly; samesite=strict` (`AuthController.cs:92-100`). |
| **Expected DB** | `password_hash` differs from the captured value; `password_changed_at_utc` set to the change instant (`UserAccount.ChangePassword`, `UserAccount.cs:151-160`); `failed_login_attempts = 0`; `locked_until_utc IS NULL`. `saas.password_history` count incremented by exactly 1, and that row's `password_hash` equals the **pre-change** hash captured in the preconditions (`Login.cs:219-224`). |
| **Expected Audit** | `audit.field_change`: one `UserAccount` / `Modified` row for `property='PasswordHash'` with `old_value = new_value = «redacted»` (`FieldChangeInterceptor.cs:34,95-99`), one for `property='PasswordChangedAtUtc'` with real timestamps, and one `PasswordHistoryEntry` / `Created` row; `actor='system'` (anonymous caller, `FieldChangeInterceptor.cs:113`); `tenant_id` = the `demo-lab` id (the handler scopes the tenant at `Login.cs:195`). `audit.security_event`: one `PASSWORD_CHANGED` row, `actor='pwbva@demo-lab.local'`, `tenant_id` = `demo-lab`, `ip_address IS NULL` (GAP-AUTH-005). |
| **Expected Notification** | n/a — no notification policy subscribes to `PASSWORD_CHANGED`. |
| **Cleanup** | Reset the fixture to a known state: `POST /api/users/{fixtureId}/reset-password` as `admin@demo-lab.local` with `{"newPassword":"Qms-Batch-Cur1!"}`, then `DELETE FROM saas.password_history WHERE user_id = <fixture id>;`. |
| **Evidence** | Two HTTP captures · SQL result sets for steps 3 and 4 · `audit.field_change` and `audit.security_event` rows |
| **Notes** | `SecurityEventLog.WriteAsync` calls `SaveChangesAsync` on the same scoped `AppDbContext` (`ComplianceLedgerServices.cs:82`), so the security event, the user row and the history row all commit at `Login.cs:235`; the `SaveChangesAsync` at `Login.cs:236` is a no-op second save. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-003 — A 13-character password is one above the minimum and is accepted  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Functional (positive) · BVA — lower boundary + 1 |
| **Priority / Severity / Automation** | Medium · Minor · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `pwbva@demo-lab.local` active, unlocked, current password `Qms-Batch12!` (the value left by `TC-AUTH-BVA-002`, or restored by the cleanup above). |
| **Test Data** | `newPassword` = `Qms-Batch123!` — exactly 13 characters, all four classes. |
| **Steps** | 1. `POST /api/auth/change-password` with `currentPassword=Qms-Batch12!`, `newPassword=Qms-Batch123!`. 2. Assert `204`. 3. `SELECT password_changed_at_utc FROM qams.user_account WHERE email='pwbva@demo-lab.local';` and assert it advanced. 4. `POST /api/auth/login` with the 13-character password and assert `200`. |
| **Expected UI** | Same as `TC-AUTH-BVA-002` — no inline alert; the SPA auto-signs-in. |
| **Expected API** | Step 1 → `204`; step 4 → `200` with a non-empty `accessToken`. |
| **Expected DB** | `password_hash` changed; `password_changed_at_utc` strictly greater than the value read in `TC-AUTH-BVA-002`; `saas.password_history` gains one row holding the hash of `Qms-Batch12!`. |
| **Expected Audit** | One `PASSWORD_CHANGED` row in `audit.security_event`; the same three `audit.field_change` rows as `TC-AUTH-BVA-002`. |
| **Expected Notification** | n/a — no policy subscribes to `PASSWORD_CHANGED`. |
| **Cleanup** | Administrative reset back to `Qms-Batch-Cur1!` and `DELETE FROM saas.password_history WHERE user_id = <fixture id>;`. |
| **Evidence** | Two HTTP captures · SQL result set for step 3 |
| **Notes** | Together with `-001` and `-002` this closes the 11/12/13 triple. The three cases must run in this order because each consumes the previous password as its `currentPassword`. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-004 — A 200-character password sits exactly on the maximum and is accepted  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Functional (positive) · BVA — on the upper boundary `PasswordRules.MaxLength = 200` (`PasswordRules.cs:20`) |
| **Priority / Severity / Automation** | Medium · Minor · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `pwbva@demo-lab.local` active, unlocked, current password `Qms-Batch-Cur1!`. |
| **Test Data** | `newPassword` = the 11-character prefix `Qms-Batch1!` followed by 189 repetitions of `a` → exactly 200 characters. |
| **Steps** | 1. `POST /api/auth/change-password` with that 200-character `newPassword`. 2. Assert `204`. 3. `SELECT length(password_hash) FROM qams.user_account WHERE email='pwbva@demo-lab.local';`. 4. `POST /api/auth/login` with the same 200-character password and assert `200`. |
| **Expected UI** | No inline alert; the SPA auto-signs-in. |
| **Expected API** | Step 1 → `204`; step 4 → `200`. |
| **Expected DB** | `qams.user_account.password_hash` is the PBKDF2 output of the ASP.NET Core Identity hasher (`SecurityAdapters.cs:13-22`) and its length is unchanged by input length — assert `length(password_hash) <= 500`, the column bound (`varchar(500)`, `IdentityAndImprovementConfigurations.cs`). |
| **Expected Audit** | One `PASSWORD_CHANGED` row; `audit.field_change` `PasswordHash` row still `«redacted»` on both sides — the 200-character secret must not appear anywhere in the ledger. |
| **Expected Notification** | n/a — no policy subscribes to `PASSWORD_CHANGED`. |
| **Cleanup** | Administrative reset back to `Qms-Batch-Cur1!`; `DELETE FROM saas.password_history WHERE user_id = <fixture id>;`. |
| **Evidence** | Two HTTP captures · `length(password_hash)` result · the redacted field-change row |
| **Notes** | `MaxLength` exists to bound hashing input (`PasswordRules.cs:19`); this case also serves as the positive control for `TC-AUTH-BVA-005`. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-005 — A 201-character password is one above the maximum and is refused  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Functional (negative) · BVA — upper boundary + 1 |
| **Priority / Severity / Automation** | Medium · Minor · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `pwbva@demo-lab.local` active, unlocked, current password `Qms-Batch-Cur1!`; capture `password_hash`. |
| **Test Data** | `newPassword` = `Qms-Batch1!` followed by 190 repetitions of `a` → exactly 201 characters. |
| **Steps** | 1. `POST /api/auth/change-password` with the 201-character `newPassword`. 2. Assert `400` and `application/problem+json`. 3. Read `errors.NewPassword`. 4. Re-read `password_hash` and assert it is byte-identical to the precondition capture. |
| **Expected UI** | Inline alert reads exactly `Validation failed.` |
| **Expected API** | `400`; `title` = `Validation failed.`; no `code` extension; `errors.NewPassword` contains exactly one message — FluentValidation's default `MaximumLength` text `The length of 'New Password' must be 200 characters or fewer. You entered 201 characters.` (no `.WithMessage()` override is applied to that link of the chain, `PasswordRules.cs:49`). |
| **Expected DB** | `password_hash` unchanged; no new `saas.password_history` row. |
| **Expected Audit** | No new `audit.field_change` or `audit.security_event` rows — rejection occurs in `ValidationBehavior` before the handler. |
| **Expected Notification** | n/a — the request never reaches a handler. |
| **Cleanup** | None. |
| **Evidence** | HTTP capture (full body) · `password_hash` comparison |
| **Notes** | The exact default message text is framework-generated and locale-dependent; capture it verbatim at first execution and pin it thereafter. The assertion that must never be relaxed is `errors.NewPassword` having **exactly one** entry — complexity and blocklist both pass on this input, so a second message would mean the chain changed. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-006 — Omitting the upper-case class fails the complexity rule  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Functional (negative) · Multiple-Condition — the `upper` operand of `upper && lower && digit && symbol` (`PasswordRules.cs:72`) driven false with the other three true |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `pwbva@demo-lab.local` active, unlocked, current password `Qms-Batch-Cur1!`; capture `password_hash`. |
| **Test Data** | `newPassword` = `qms-batch12!` — 12 characters; lower `q,m,s,b,a,t,c,h`, digit `1,2`, symbols `-,!`; **no** character satisfies `char.IsUpper`. |
| **Steps** | 1. `POST /api/auth/change-password` with that `newPassword`. 2. Assert `400`. 3. Assert `errors.NewPassword` = exactly one entry. 4. Re-read `password_hash` and assert it is unchanged. |
| **Expected UI** | Inline alert `Validation failed.` |
| **Expected API** | `400` `application/problem+json`; `errors.NewPassword` = `["The password must include upper- and lower-case letters, a digit, and a symbol."]` (`PasswordRules.cs:51`) — exactly one message, proving length and blocklist both passed. |
| **Expected DB** | `password_hash` unchanged; no `saas.password_history` row. |
| **Expected Audit** | No new `audit.field_change` or `audit.security_event` rows. |
| **Expected Notification** | n/a — the request never reaches a handler. |
| **Cleanup** | None. |
| **Evidence** | HTTP capture · `password_hash` comparison |
| **Notes** | `HasComplexity` uses an `if/else if` ladder (`PasswordRules.cs:66-69`), so each character contributes to exactly one class; `-` and `!` both land in `symbol` via the terminal `else`. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-007 — Omitting the lower-case class fails the complexity rule  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Functional (negative) · Multiple-Condition — `lower` false, other three true |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `pwbva@demo-lab.local` active, unlocked, current password `Qms-Batch-Cur1!`; capture `password_hash`. |
| **Test Data** | `newPassword` = `QMS-BATCH12!` — 12 characters; upper `Q,M,S,B,A,T,C,H`, digit `1,2`, symbols `-,!`; no `char.IsLower` character. |
| **Steps** | 1. `POST /api/auth/change-password` with that `newPassword`. 2. Assert `400`. 3. Assert `errors.NewPassword` has exactly one entry equal to the complexity message. 4. Re-read `password_hash` and assert it is unchanged. |
| **Expected UI** | Inline alert `Validation failed.` |
| **Expected API** | `400`; `errors.NewPassword` = `["The password must include upper- and lower-case letters, a digit, and a symbol."]`, one entry only. |
| **Expected DB** | `password_hash` unchanged; no `saas.password_history` row. |
| **Expected Audit** | No new `audit.field_change` or `audit.security_event` rows. |
| **Expected Notification** | n/a — the request never reaches a handler. |
| **Cleanup** | None. |
| **Evidence** | HTTP capture · `password_hash` comparison |
| **Notes** | Paired with `-006` this is the upper/lower half of the four-way class sweep; `-008` and `-009` complete it. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-008 — Omitting the digit class fails the complexity rule  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Functional (negative) · Multiple-Condition — `digit` false, other three true |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `pwbva@demo-lab.local` active, unlocked, current password `Qms-Batch-Cur1!`; capture `password_hash`. |
| **Test Data** | `newPassword` = `Qms-Batchaa!` — 12 characters; upper `Q,B`, lower `m,s,a,t,c,h,a,a`, symbols `-,!`; no `char.IsDigit` character. |
| **Steps** | 1. `POST /api/auth/change-password` with that `newPassword`. 2. Assert `400`. 3. Assert `errors.NewPassword` has exactly one entry equal to the complexity message. 4. Re-read `password_hash` and assert it is unchanged. |
| **Expected UI** | Inline alert `Validation failed.` |
| **Expected API** | `400`; `errors.NewPassword` = `["The password must include upper- and lower-case letters, a digit, and a symbol."]`, one entry only. |
| **Expected DB** | `password_hash` unchanged; no `saas.password_history` row. |
| **Expected Audit** | No new `audit.field_change` or `audit.security_event` rows. |
| **Expected Notification** | n/a — the request never reaches a handler. |
| **Cleanup** | None. |
| **Evidence** | HTTP capture · `password_hash` comparison |
| **Notes** | `char.IsDigit` is `Nd`-category only, so an Arabic-Indic digit `٥` (U+0665) would also satisfy it — out of scope here, covered by charter EXPL-6. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-009 — Omitting the symbol class fails the complexity rule  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Functional (negative) · Multiple-Condition — `symbol` false, other three true |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `pwbva@demo-lab.local` active, unlocked, current password `Qms-Batch-Cur1!`; capture `password_hash`. |
| **Test Data** | `newPassword` = `QmsBatch1234` — 12 characters; upper `Q,B`, lower `m,s,a,t,c,h`, digits `1,2,3,4`; every character is a letter or a digit, so the terminal `else` at `PasswordRules.cs:69` is never taken. |
| **Steps** | 1. `POST /api/auth/change-password` with that `newPassword`. 2. Assert `400`. 3. Assert `errors.NewPassword` has exactly one entry equal to the complexity message. 4. Re-read `password_hash` and assert it is unchanged. |
| **Expected UI** | Inline alert `Validation failed.` |
| **Expected API** | `400`; `errors.NewPassword` = `["The password must include upper- and lower-case letters, a digit, and a symbol."]`, one entry only. |
| **Expected DB** | `password_hash` unchanged; no `saas.password_history` row. |
| **Expected Audit** | No new `audit.field_change` or `audit.security_event` rows. |
| **Expected Notification** | n/a — the request never reaches a handler. |
| **Cleanup** | None. |
| **Evidence** | HTTP capture · `password_hash` comparison |
| **Notes** | This is the only one of the four class cases whose failing input a user is likely to type; it is also the negative control for `TC-AUTH-BVA-010`, which shows what the "symbol" class actually accepts. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-010 — A single space satisfies the "symbol" class, so `Qms Batch123` is accepted  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 (partially conforms — the URS says "symbol", the code says "anything not upper, lower or digit") · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Security (as-built characterisation) · Error Guessing — probing the terminal `else` branch of the class ladder (`PasswordRules.cs:69`) |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `pwbva@demo-lab.local` active, unlocked, current password `Qms-Batch-Cur1!`. |
| **Test Data** | `newPassword` = `Qms Batch123` — 12 characters, the 4th being U+0020 SPACE; upper `Q,B`, lower `m,s,a,t,c,h`, digits `1,2,3`, and the space is the **only** character reaching the `symbol` branch. |
| **Steps** | 1. `POST /api/auth/change-password` with that `newPassword`. 2. Assert `204` — the password is accepted. 3. `POST /api/auth/login` with `password` = `Qms Batch123` and assert `200`, proving the space survives the round trip through JSON, PBKDF2 hashing and verification unaltered. 4. Repeat step 1 with `newPassword` = `Qms\tBatch123` (U+0009 TAB in the same position) from a fresh known password and record the status. |
| **Expected UI** | No inline alert on either submission — the SPA auto-signs-in as in `TC-AUTH-BVA-002`. |
| **Expected API** | Step 1 → `204`. Step 3 → `200` with a non-empty `accessToken`. Step 4 → `204` (TAB also lands in the terminal `else`). |
| **Expected DB** | `password_hash` changes on each accepted call; `password_changed_at_utc` advances; one `saas.password_history` row per accepted call. |
| **Expected Audit** | One `PASSWORD_CHANGED` row per accepted call; the `PasswordHash` field-change row remains `«redacted»`, so the whitespace-bearing secret is not disclosed in the ledger. |
| **Expected Notification** | n/a — no policy subscribes to `PASSWORD_CHANGED`. |
| **Cleanup** | Administrative reset to `Qms-Batch-Cur1!`; `DELETE FROM saas.password_history WHERE user_id = <fixture id>;`. |
| **Evidence** | Three HTTP captures · the successful login response · `password_changed_at_utc` progression |
| **Notes** | `[ID]` because this documents an implemented behaviour that no requirement states. URS-002 says "upper, lower, digit, symbol"; the implementation's fourth class is the complement of the other three, so `Aa1` plus nine spaces would also pass. Raised as **GAP-AUTH-904**. Do not "fix" the expectation to a rejection — the case must record what the build does. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-011 — A 12-character all-whitespace password fails `NotEmpty` and complexity together  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Functional (negative) · Multiple-Condition — two independent validators in the chain fail on one input under FluentValidation's default `Continue` cascade |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `pwbva@demo-lab.local` active, unlocked, current password `Qms-Batch-Cur1!`; capture `password_hash`. |
| **Test Data** | `newPassword` = exactly 12 U+0020 SPACE characters (`"            "`). Length 12, so `MinimumLength(12)` and `MaximumLength(200)` both pass; `NotEmpty` treats a whitespace-only string as empty; `HasComplexity` finds `symbol=true` but `upper=lower=digit=false`; `NotCompromised` trims to `""` and returns `false` on the `IsNullOrEmpty` guard (`PasswordRules.cs:77`). |
| **Steps** | 1. `POST /api/auth/change-password` with a `newPassword` of 12 spaces (send raw JSON from a file with `curl.exe --data "@payload.json"` so no shell trims it). 2. Assert `400`. 3. Enumerate `errors.NewPassword` and record every message. 4. Re-read `password_hash` and assert it is unchanged. |
| **Expected UI** | Inline alert `Validation failed.` |
| **Expected API** | `400` `application/problem+json`; `errors.NewPassword` contains **three** messages: FluentValidation's `NotEmpty` default text, `The password must include upper- and lower-case letters, a digit, and a symbol.`, and `This password is too common or appears in known breach lists. Choose another.`; it must **not** contain the `at least 12 characters` message. |
| **Expected DB** | `password_hash` unchanged; no `saas.password_history` row. |
| **Expected Audit** | No new `audit.field_change` or `audit.security_event` rows. |
| **Expected Notification** | n/a — the request never reaches a handler. |
| **Cleanup** | None. |
| **Evidence** | HTTP capture with the full `errors` object · `password_hash` comparison |
| **Notes** | Three claims must be confirmed at first execution rather than assumed: that `NotEmpty` counts whitespace as empty, that `MinimumLength` counts the spaces (so no length error appears), and that the blocklist message fires via the `IsNullOrEmpty` guard on the trimmed value. Record the actual message set as the pinned baseline. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-012 — Twelve caseless Arabic letters fail complexity (no upper, no lower, no digit)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning — the "caseless script" partition of the Unicode input domain |
| **Priority / Severity / Automation** | Medium · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; the SPA ships `ar` as a supported interface language (`core/i18n.service.ts`), so Arabic input is a realistic partition |
| **Preconditions** | `pwbva@demo-lab.local` active, unlocked, current password `Qms-Batch-Cur1!`; capture `password_hash`. |
| **Test Data** | `newPassword` = U+0627 ARABIC LETTER ALEF repeated exactly 12 times. Every character is Unicode category `Lo` (other letter): `char.IsUpper` false, `char.IsLower` false, `char.IsDigit` false, so all 12 fall to the terminal `else` and only `symbol` is set. |
| **Steps** | 1. Write the payload to a UTF-8 file (PowerShell 5.1 mangles non-ASCII in `.ps1` — ground truth §3) and `POST /api/auth/change-password` with `curl.exe --data "@payload.json" -H "Content-Type: application/json"`. 2. Assert `400`. 3. Assert `errors.NewPassword` has exactly one entry, the complexity message. 4. Re-read `password_hash` and assert it is unchanged. |
| **Expected UI** | Inline alert `Validation failed.`; in the `ar` locale the alert text is still the untranslated server `title` (`login.component.ts:456` renders `err.error?.title` verbatim). |
| **Expected API** | `400`; `errors.NewPassword` = `["The password must include upper- and lower-case letters, a digit, and a symbol."]`, one entry only — 12 UTF-16 code units means `MinimumLength(12)` passes. |
| **Expected DB** | `password_hash` unchanged; no `saas.password_history` row. |
| **Expected Audit** | No new `audit.field_change` or `audit.security_event` rows. |
| **Expected Notification** | n/a — the request never reaches a handler. |
| **Cleanup** | None. |
| **Evidence** | HTTP capture (raw bytes, to prove the UTF-8 round trip) · `password_hash` comparison |
| **Notes** | The consequence for an Arabic-speaking laboratory is that a compliant password must contain Latin (or Greek/Cyrillic) cased letters — record it, do not editorialise. Combining marks and NFC/NFD normalisation are deliberately out of scope here and belong to charter EXPL-6. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-013 — Twelve Greek characters with case, digits and a symbol are accepted  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — the "cased non-Latin script" partition, the complement of `TC-AUTH-BVA-012` |
| **Priority / Severity / Automation** | Medium · Minor · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `pwbva@demo-lab.local` active, unlocked, current password `Qms-Batch-Cur1!`. |
| **Test Data** | `newPassword` = `ΑΒΓαβγ123-Δε` — 12 characters: U+0391 U+0392 U+0393 (upper) U+03B1 U+03B2 U+03B3 (lower) `1` `2` `3` `-` U+0394 (upper) U+03B5 (lower). `char.IsUpper`/`char.IsLower` are Unicode-aware, so all four classes are satisfied. |
| **Steps** | 1. `POST /api/auth/change-password` with that `newPassword`, sent from a UTF-8 payload file. 2. Assert `204`. 3. `POST /api/auth/login` with the same string and assert `200`. 4. `SELECT password_changed_at_utc FROM qams.user_account WHERE email='pwbva@demo-lab.local';` and assert it advanced. |
| **Expected UI** | No inline alert; SPA auto-signs-in. |
| **Expected API** | Step 1 → `204`; step 3 → `200` with a non-empty `accessToken`. |
| **Expected DB** | `password_hash` changed; `password_changed_at_utc` advanced; one new `saas.password_history` row. |
| **Expected Audit** | One `PASSWORD_CHANGED` row; the `PasswordHash` field-change row `«redacted»` on both sides. |
| **Expected Notification** | n/a — no policy subscribes to `PASSWORD_CHANGED`. |
| **Cleanup** | Administrative reset to `Qms-Batch-Cur1!`; `DELETE FROM saas.password_history WHERE user_id = <fixture id>;`. |
| **Evidence** | Two HTTP captures (raw bytes) · `password_changed_at_utc` before/after |
| **Notes** | Step 3 is the load-bearing assertion: it proves the same byte sequence hashes and verifies identically, i.e. that no layer between the SPA and PBKDF2 normalises or re-encodes the string. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-014 — A blocklisted password wrapped in whitespace is still refused  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Security (negative) · Error Guessing — attacking the trim asymmetry between the untrimmed length check and the trimmed blocklist check (`PasswordRules.cs:47,77`) |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `pwbva@demo-lab.local` active, unlocked, current password `Qms-Batch-Cur1!`; capture `password_hash`. |
| **Test Data** | (a) `newPassword` = `"  Password1  "` — 13 characters (two leading and two trailing spaces around the 9-character blocklist entry). Length 13 passes; upper `P`, lower `assword`, digit `1`, symbol space → complexity passes; `Trim()` yields `Password1`, which matches the list entry `password1` under `StringComparer.OrdinalIgnoreCase` (`PasswordRules.cs:27,29`). (b) Control: `newPassword` = `Password1234` — 12 characters, not an exact list entry. |
| **Steps** | 1. `POST /api/auth/change-password` with payload (a). 2. Assert `400` and that `errors.NewPassword` has exactly one entry. 3. Re-read `password_hash` and assert it is unchanged. 4. `POST /api/auth/change-password` with payload (b) and record the status. 5. Re-read `password_hash`. |
| **Expected UI** | Step 1 → inline alert `Validation failed.`; step 4 → no alert, SPA auto-signs-in. |
| **Expected API** | Step 1 → `400`; `errors.NewPassword` = `["This password is too common or appears in known breach lists. Choose another."]` (`PasswordRules.cs:53`), one entry only. Step 4 → `204` — the blocklist is **exact-match after trim**, not substring, so `Password1234` is accepted. |
| **Expected DB** | After step 1, `password_hash` unchanged. After step 4, `password_hash` changed and one `saas.password_history` row added. |
| **Expected Audit** | Step 1 writes nothing. Step 4 writes one `PASSWORD_CHANGED` row and the usual three `audit.field_change` rows. |
| **Expected Notification** | n/a — no policy subscribes to `PASSWORD_CHANGED`. |
| **Cleanup** | Administrative reset to `Qms-Batch-Cur1!`; `DELETE FROM saas.password_history WHERE user_id = <fixture id>;`. |
| **Evidence** | Two HTTP captures · `password_hash` before/after each step |
| **Notes** | Step 4 is not a defect: `PasswordRules.cs:23-25` documents the list as deliberately non-exhaustive ("it rejects the passwords attackers try first"). The case records the boundary of the control so a reviewer is not misled into thinking substring screening exists. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-015 — `password` fails three validators at once and returns all three messages  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Functional (negative) · Multiple-Condition — three of the five chained validators fail simultaneously; proves the rule-level cascade is `Continue`, not `Stop` |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `pwbva@demo-lab.local` active, unlocked, current password `Qms-Batch-Cur1!`; capture `password_hash`. |
| **Test Data** | `newPassword` = `password` — 8 characters. `NotEmpty` passes; `MinimumLength(12)` fails; `MaximumLength(200)` passes; `HasComplexity` fails (no upper, no digit, no symbol); `NotCompromised` fails (`"password"` is the first list entry, `PasswordRules.cs:29`). |
| **Steps** | 1. `POST /api/auth/change-password` with `newPassword=password`. 2. Assert `400`. 3. Assert `errors` has exactly one key, `NewPassword`, and that its array length is exactly `3`. 4. Assert the array contains, in the chain order of `PasswordRules.cs:47-53`, the `at least 12 characters` message, the complexity message and the breach-list message. 5. Re-read `password_hash` and assert it is unchanged. |
| **Expected UI** | Inline alert `Validation failed.` — the user is told nothing about which of the three rules they broke. |
| **Expected API** | `400` `application/problem+json`; `title` = `Validation failed.`; no `code` extension; `errors.NewPassword` has exactly three entries as enumerated above; `traceId` present. |
| **Expected DB** | `password_hash` unchanged; no `saas.password_history` row. |
| **Expected Audit** | No new `audit.field_change` or `audit.security_event` rows. |
| **Expected Notification** | n/a — the request never reaches a handler. |
| **Cleanup** | None. |
| **Evidence** | HTTP capture with the full `errors` object |
| **Notes** | The single-key assertion in step 3 matters: `ChangePasswordValidator` also has rules on `Email` and `CurrentPassword` (`Login.cs:168-169`), and a valid payload for those two must produce no additional keys. The gap between the informative server body and the SPA's `title`-only rendering is **GAP-AUTH-907**, tested directly by `TC-AUTH-API-074`. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-BVA-016 — The same 12-character floor is enforced by administrative reset and by registration  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002, URS-009 · RSK-AUTH-010 |
| **Level / Type / Technique** | API · Functional (negative) · BVA — the same lower boundary − 1 applied at the two privileged call sites, proving `StrongPassword()` is a single source of truth |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin (`admin@demo-lab.local`) · **`users.manage`** on both endpoints (`UsersController.cs:28,84` and the command policies at `UserManagement.cs:32,96`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Signed in as `admin@demo-lab.local` / `Demo-Admin-Pass-2!` with a `scope=full` token; fixture `pwbva@demo-lab.local` exists; capture its `password_hash`. Record the current row count of `qams.user_account` for tenant `demo-lab`. |
| **Test Data** | Reset: `POST /api/users/{fixtureId}/reset-password` with `{"newPassword":"Qms-Batch1!"}` (11 characters). Registration: `POST /api/users` with `{"email":"pwbva2@demo-lab.local","displayName":"Password BVA Two","role":"Analyst","initialPassword":"Qms-Batch1!"}`. |
| **Steps** | 1. Send the reset request. 2. Assert `400` and read `errors`. 3. Re-read the fixture's `password_hash` and assert it is unchanged. 4. Send the registration request. 5. Assert `400` and read `errors`. 6. `SELECT count(*) FROM qams.user_account WHERE tenant_id = <demo-lab id>;` and assert it is unchanged. 7. Repeat step 4 with `initialPassword` = `Qms-Batch12!` (12 characters) and assert `200`. |
| **Expected UI** | In the Users screen, the accessible masked reset prompt closes and the facade's error banner shows exactly `Validation failed.` (`users.facade.ts:64-69` renders `title` only, `users.component.ts:280-288`). No row appears in the user list for the rejected registration. |
| **Expected API** | Steps 2 and 5 → `400` `application/problem+json`, `title` = `Validation failed.`, no `code`; `errors.NewPassword` (reset) and `errors.InitialPassword` (registration) each contain exactly one message, `The password must be at least 12 characters.`. Step 7 → `200` with body `{"id":"<uuid>"}` (`UsersController.cs:29-31`). |
| **Expected DB** | Fixture `password_hash` unchanged; `qams.user_account` row count unchanged after step 6; after step 7 exactly one new row with `email='pwbva2@demo-lab.local'`, `role='Analyst'`, `is_active=true`, `tenant_id` = `demo-lab`, `password_changed_at_utc IS NULL` (`UserAccount.Create` never stamps it), and a non-null `role_id` from the seeded-role default (`UserManagement.cs:81`). |
| **Expected Audit** | Steps 1–6 write no `audit.field_change` rows — `AuthorizationBehavior` passes (the actor holds `users.manage`) and `ValidationBehavior` rejects before the handler (`Application/DependencyInjection.cs:22-24` puts authorization *before* validation). Step 7 writes one `UserAccount` / `Created` row with `actor='<admin display name>'` and `tenant_id` = `demo-lab`. No `audit.security_event` rows on any step — user creation emits none. |
| **Expected Notification** | n/a — no policy subscribes to user registration. |
| **Cleanup** | `POST /api/users/{newId}/deactivate` for the account created in step 7, then `DELETE FROM saas.password_history WHERE user_id = '<newId>'; DELETE FROM qams.user_account WHERE id = '<newId>';`. |
| **Evidence** | Three HTTP captures · `password_hash` comparison · user-count query before/after |
| **Notes** | The property name in the `errors` key differs by endpoint (`NewPassword` vs `InitialPassword`) because FluentValidation keys on the command property; assert the key, not just the message. Self-service change-password is covered by `-001`, so this case completes the three call sites listed at `PasswordRules.cs:8-10`. Note that `RegisterUserValidator` adds `.EmailAddress()` on the email (`UserManagement.cs:41`) whereas the domain only checks for an `@` (`UserAccount.cs:89-92`) — that divergence belongs to batch A. |
| **Result / Defect** | Not Run · — |

---

## Anonymous workspace lookup — `GET /api/auth/workspace/{slug}`

#### TC-AUTH-API-060 — An active laboratory resolves to its name and discloses nothing else  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-089, URS-090 · RSK-AUTH-011 |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — the pre-authentication branding lookup driven end to end |
| **Priority / Severity / Automation** | High · Major · Yes (functional — extends `WorkspaceLookupTests`) |
| **Role / Permission / Tenant** | Anonymous · n/a — `[AllowAnonymous]` (`AuthController.cs:47-48`) · none — the endpoint is read before any tenant context exists |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SELECT identifier, name, status FROM saas.tenant WHERE identifier='demo-lab';` returns `demo-lab | Demo Laboratory | Active` (measured 2026-08-01). No `Authorization` header and no `qams_rt` cookie on the request. |
| **Test Data** | `GET /api/auth/workspace/demo-lab` |
| **Steps** | 1. Issue the GET with no credentials. 2. Assert `200` and `Content-Type: application/json`. 3. Parse the body and enumerate its top-level JSON property names. 4. Assert the set of names is exactly `["name"]`. 5. Assert `name` equals `Demo Laboratory`. 6. Assert the response carries **no** `Set-Cookie` header. 7. Assert the defensive header set is present (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Content-Security-Policy`) per `SecurityHeadersMiddleware`. |
| **Expected UI** | On `/t/demo-lab` the workspace pill avatar shows the initials `DL` and the label shows `Demo Laboratory` (`login.component.ts:54-55, 381-388`), replacing the slug-derived fallback `Demo Lab`. |
| **Expected API** | `200`; body exactly `{"name":"Demo Laboratory"}` — `WorkspaceResponse` is a single-property record (`Contracts/Tenancy/TenancyContracts.cs:28`) and the projection selects only `t.Name` (`GetWorkspace.cs:45`). No `id`, no `status`, no `identifier`, no settings field of any kind. |
| **Expected DB** | Read-only: one `SELECT` against `saas.tenant` filtered on `Slug == slug && Status == Active` (`GetWorkspace.cs:44`). No row is written anywhere. |
| **Expected Audit** | Zero new rows in `audit.security_event` and `audit.field_change` — the query path writes nothing and no security-event type exists for workspace lookup. |
| **Expected Notification** | n/a — anonymous read. |
| **Cleanup** | None — read-only. |
| **Evidence** | HTTP capture (status, all headers, raw body bytes) · the enumerated JSON key set |
| **Notes** | This is the positive control for `-061`…`-064`. Step 4 must enumerate keys rather than deserialise into a typed record, or a future added property would pass unnoticed. Mirrors OQ-SEC-16(a) (`docs/validation/06-Revalidation-Delta-v1.38-v1.50.md:338`). |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-061 — A well-formed but unknown slug answers 404 with no discriminating content  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-090 · RSK-AUTH-011 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — the "syntactically valid, no such tenant" partition |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Anonymous · n/a — `[AllowAnonymous]` · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SELECT count(*) FROM saas.tenant WHERE identifier='no-such-lab-here';` returns `0`. |
| **Test Data** | `GET /api/auth/workspace/no-such-lab-here` — 16 characters, matches `^[a-z0-9](?:-?[a-z0-9]){1,49}$` (`TenantSlug.cs:47`), so `TenantSlug.Create` succeeds and the miss occurs at the database query. |
| **Steps** | 1. Issue the GET with no credentials. 2. Assert `404`. 3. Assert `Content-Type: application/problem+json`. 4. Assert `title` = `Workspace not found.` (`AuthController.cs:55`). 5. Assert the body has **no** `code` extension. 6. Record the exact response body bytes and the full header set for the differential comparison in `TC-AUTH-API-064`. |
| **Expected UI** | The workspace pill falls back silently to the title-cased slug label `No Such Lab Here` (`login.component.ts:381, 391-399`); the SPA's `error:` arm sets `resolvedName` to `null` and shows **no** alert (`login.component.ts:421`). A visitor cannot tell from the page that the slug is unknown. |
| **Expected API** | `404` `application/problem+json` produced by `ControllerBase.Problem(...)` through the framework `ProblemDetailsFactory`, so the body carries `type`, `title` = `Workspace not found.`, `status` = 404 and `traceId` (`Program.cs:174-182`) — and **no** `code`, because this path does not go through `ProblemResponse.WriteAsync`. |
| **Expected DB** | Read-only: one `SELECT` returning zero rows. Nothing written. |
| **Expected Audit** | Zero new `audit.security_event` rows — an anti-enumeration probe leaves no trace, which is itself worth recording (see the coverage note). |
| **Expected Notification** | n/a — anonymous read. |
| **Cleanup** | None — read-only. |
| **Evidence** | HTTP capture (status, all headers, raw body bytes) |
| **Notes** | Run this before the credential-burst cases: every `AuthController` request consumes one of the 10 permits in the `auth` partition (`AuthController.cs:18`), and `TC-AUTH-API-075`…`-081` exhaust it. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-062 — A malformed slug answers 404 identically, never 400  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-090 · RSK-AUTH-011 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — the "fails `TenantSlug` syntax" partition |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Anonymous · n/a — `[AllowAnonymous]` · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | None beyond a running API. |
| **Test Data** | Three malformed values, each issued separately: (a) `Not_A_Valid_Slug` — underscores and upper case; after `Trim().ToLowerInvariant()` (`TenantSlug.cs:28`) it is `not_a_valid_slug`, which the regex rejects on `_`. (b) `-leading-hyphen` — the first character must be `[a-z0-9]`. (c) `NOT%20A%20SLUG` — decodes to `NOT A SLUG`; spaces fail the regex. |
| **Steps** | 1. `GET /api/auth/workspace/Not_A_Valid_Slug`. 2. `GET /api/auth/workspace/-leading-hyphen`. 3. `GET /api/auth/workspace/NOT%20A%20SLUG`. 4. For each: assert `404` (never `400`, never `500`), `Content-Type: application/problem+json`, `title` = `Workspace not found.`, and the absence of a `code` extension. 5. Assert none of the three bodies mentions `TENANT-001` or `TENANT-002`. |
| **Expected UI** | The pill falls back to the slug-derived label; no alert is shown in any of the three cases. |
| **Expected API** | All three → `404` `application/problem+json` with `title` = `Workspace not found.`. Critically, the `DomainException("TENANT-002", …)` thrown by `TenantSlug.Create` (`TenantSlug.cs:32-35`) is caught inside the handler (`GetWorkspace.cs:37-41`) and converted to `null`, so it never reaches `DomainExceptionHandler` and never surfaces as a `422` with a `code`. |
| **Expected DB** | No query is issued at all for these inputs — the handler returns before touching `db.Tenants` (`GetWorkspace.cs:41`). Confirm with `pg_stat_statements` or Npgsql OpenTelemetry spans that no `SELECT` against `saas.tenant` is emitted. |
| **Expected Audit** | Zero new `audit.security_event` and `audit.field_change` rows. |
| **Expected Notification** | n/a — anonymous read. |
| **Cleanup** | None — read-only. |
| **Evidence** | Three HTTP captures (raw body bytes) · the trace/span list for step 3 |
| **Notes** | The DB assertion is the discriminator that a body comparison cannot make: a malformed slug is cheaper to answer than an unknown one, which is the timing channel `TC-AUTH-API-064` measures. Three URL-unsafe candidates are deliberately excluded because ASP.NET routing would reject them before the controller: an empty segment, a segment containing `/`, and a segment containing `?`. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-063 — A suspended laboratory answers 404, indistinguishable from never having existed  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-090 · RSK-AUTH-011 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — the "exists but not `Active`" partition; the one an attacker most wants to distinguish |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Anonymous · n/a — `[AllowAnonymous]` · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Provision a throwaway tenant as `platform-admin@localhost`: `POST /api/tenants` with `{"identifier":"susp-probe-lab","name":"Suspended Probe Laboratory","adminEmail":"admin@susp.test","adminDisplayName":"TA","adminPassword":"Tenant-Admin-Pass-1!"}` → `201`. Then set its status directly, because **no suspend endpoint exists** (`TenantsController.cs` exposes only `POST` and `GET`): `UPDATE saas.tenant SET status='Suspended' WHERE identifier='susp-probe-lab';` — permitted because `saas.tenant` carries no RLS (measured: `relrowsecurity=false`) and the value is inside `ck_tenant_status_domain`. |
| **Test Data** | `GET /api/auth/workspace/susp-probe-lab` |
| **Steps** | 1. Before suspension, `GET /api/auth/workspace/susp-probe-lab` and assert `200` with `{"name":"Suspended Probe Laboratory"}` — the positive control. 2. Apply the `UPDATE` above and confirm `status='Suspended'`. 3. Re-issue the same GET. 4. Assert `404`, `application/problem+json`, `title` = `Workspace not found.`, no `code`. 5. Diff the step-3 body against the `TC-AUTH-API-061` body captured for `no-such-lab-here`, ignoring only `traceId`. 6. Repeat steps 2–4 with `status='Terminated'` and then `status='Provisioning'`. |
| **Expected UI** | After suspension the pill reverts to the slug-derived label `Susp Probe Lab`; no alert, no status wording, nothing that distinguishes a suspended laboratory from a nonexistent one. |
| **Expected API** | Step 1 → `200`. Steps 4 and 6 → `404` `application/problem+json`, `title` = `Workspace not found.`, no `code`. Step 5 → the two bodies are identical apart from `traceId`. All three non-`Active` statuses behave alike because the projection filters `Status == TenantStatus.Active` (`GetWorkspace.cs:44`), not `Status != Suspended`. |
| **Expected DB** | The `SELECT` runs and returns zero rows (unlike `-062`, where no query runs). `saas.tenant` row remains present with the mutated `status`. |
| **Expected Audit** | Zero new `audit.security_event` rows. The direct `UPDATE` bypasses EF, so it produces **no** `audit.field_change` row either — record this explicitly so the transcript does not claim a ledger entry that will not exist. |
| **Expected Notification** | n/a — anonymous read. |
| **Cleanup** | `DELETE FROM qams.user_account WHERE tenant_id = (SELECT id FROM saas.tenant WHERE identifier='susp-probe-lab'); DELETE FROM saas.tenant WHERE identifier='susp-probe-lab';` — the tenant FK from `user_account` is `ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED`, so the user rows must go first. |
| **Evidence** | Four HTTP captures · the body diff from step 5 · `SELECT identifier, status FROM saas.tenant` before and after |
| **Notes** | Executes OQ-SEC-16(d). The precondition's direct `UPDATE` is a genuine testing limitation, not a shortcut: the API has no tenant-suspension operation, which belongs to module `TENANT`, not `AUTH`. Step 6 extends OQ-SEC-16 beyond `Suspended` to the other two non-`Active` states. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-064 — The three miss classes are byte-identical and timing-comparable  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-090 · RSK-AUTH-011 |
| **Level / Type / Technique** | API · Security (differential) · Pairwise — miss class × response dimension (status, header set, body bytes, wall-clock), plus the slug-length boundaries 1/2/50/51 |
| **Priority / Severity / Automation** | Critical · Critical · Partial — status/header/body assertions automate; the timing arm is a manual charter measurement |
| **Role / Permission / Tenant** | Anonymous · n/a — `[AllowAnonymous]` · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; the `auth` rate-limit partition allows 10 requests per minute per client address, so the sampling loop must pace itself or run with `RateLimit__AuthPermitPerMinute` temporarily raised |
| **Preconditions** | `demo-lab` `Active`; `no-such-lab-here` absent; `susp-probe-lab` present and `Suspended` (from `TC-AUTH-API-063`, not yet cleaned up). |
| **Test Data** | Miss classes: (a) unknown `no-such-lab-here`, (b) malformed `Not_A_Valid_Slug`, (c) suspended `susp-probe-lab`. Length boundaries: `a` (1 char, below the regex minimum of 2), `ab` (2 chars, at the minimum), a 50-character all-`a` string (at `TenantSlug.MaxLength`, `TenantSlug.cs:12`), a 51-character all-`a` string (one above). |
| **Steps** | 1. For each of (a), (b), (c): capture status, the ordered header-name list, the header values other than `Date`/`traceId`, and the raw body bytes. 2. Assert all three statuses are `404`. 3. Assert the three header-name lists are identical. 4. Assert the three bodies are identical after removing the `traceId` member. 5. For each of the four length boundaries, assert `404` with the same body shape (`a`, the 50-char and the 51-char strings all miss; `ab` misses unless a two-character tenant exists — verify with `SELECT count(*) FROM saas.tenant WHERE length(identifier)=2;` first). 6. With the auth budget temporarily raised, issue 50 requests per miss class and record the p50 and p95 wall-clock times. 7. Report whether class (b) is measurably faster than (a) and (c). |
| **Expected UI** | n/a — this case operates below the SPA; the browser surface is covered by `TC-AUTH-API-060`…`-063`. |
| **Expected API** | Steps 2–5: `404` `application/problem+json`, `title` = `Workspace not found.`, no `code`, for all seven inputs; the three miss-class bodies byte-identical modulo `traceId`. Step 7: class (b) is expected to be faster, because `GetWorkspace.cs:37-41` returns before any database round trip while (a) and (c) each execute one `SELECT` — record the measured delta; do not assert a threshold this pass. |
| **Expected DB** | Classes (a) and (c) and the length boundaries `ab`/50-char each execute exactly one `SELECT` against `saas.tenant`; class (b) and the 51-char input execute none (the 51-char value fails `normalized.Length > MaxLength` at `TenantSlug.cs:30`). |
| **Expected Audit** | Zero new rows in `audit.security_event` across all ~157 requests. |
| **Expected Notification** | n/a — anonymous read. |
| **Cleanup** | Restore `RateLimit__AuthPermitPerMinute` to its default (unset) and restart the API; clean up `susp-probe-lab` per `TC-AUTH-API-063`. |
| **Evidence** | Seven raw HTTP captures · the byte-diff output · the timing table (p50/p95 per class, n=50) |
| **Notes** | The body-identity assertion is the requirement (URS-090); the timing measurement is a characterisation, and a measurable difference is a finding to raise, not a failure of this case. Charter EXPL-1 covers the equivalent differential on `POST /api/auth/login`, where the bodies differ **by design** — do not conflate the two surfaces. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-065 — An upper-case slug is normalised and resolves, so casing is not an enumeration signal  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-089, URS-090 · RSK-AUTH-011 |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — the "valid slug, non-canonical casing and padding" partition |
| **Priority / Severity / Automation** | Medium · Minor · Yes (functional) |
| **Role / Permission / Tenant** | Anonymous · n/a — `[AllowAnonymous]` · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `demo-lab` `Active` with `name = 'Demo Laboratory'`. |
| **Test Data** | (a) `GET /api/auth/workspace/DEMO-LAB` · (b) `GET /api/auth/workspace/Demo-Lab` · (c) `GET /api/auth/workspace/%20demo-lab%20` (a space-padded slug; `TenantSlug.Create` trims before validating, `TenantSlug.cs:28`). |
| **Steps** | 1. Issue (a); assert `200` and `{"name":"Demo Laboratory"}`. 2. Issue (b); assert the same. 3. Issue (c); assert the same. 4. Assert all three bodies are byte-identical to the `TC-AUTH-API-060` body. |
| **Expected UI** | Visiting `/t/DEMO-LAB` shows the pill label `Demo Laboratory`, not `DEMO LAB`. |
| **Expected API** | All three → `200` with `{"name":"Demo Laboratory"}` — `TenantSlug.Create` applies `Trim().ToLowerInvariant()` before the regex, and the EF comparison is against the normalised `TenantSlug` value object (`GetWorkspace.cs:35,44`). |
| **Expected DB** | One `SELECT` per request, each matching the `demo-lab` row. |
| **Expected Audit** | Zero new `audit.security_event` and `audit.field_change` rows. |
| **Expected Notification** | n/a — anonymous read. |
| **Cleanup** | None — read-only. |
| **Evidence** | Three HTTP captures · byte-diff against the `-060` body |
| **Notes** | Recorded because it is the counter-intuitive arm of the anti-enumeration story: a probe does not have to guess the exact casing, so the search space for slug enumeration is case-insensitive. That is consistent with `TenantSlug`'s design (slugs are canonically lower case) and is not raised as a gap; it is stated so the security assessment is not surprised by it. |
| **Result / Defect** | Not Run · — |

---

## Password change, reset and history reuse

#### TC-AUTH-API-066 — A valid self-service change rotates the password, retires the old hash and logs it  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002, URS-016, URS-019 · RSK-AUTH-012 |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — the full change-password ceremony including its ledger side effects |
| **Priority / Severity / Automation** | Critical · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — `POST /api/auth/change-password` is `[AllowAnonymous]` and the command is `[AllowUnauthenticated]` (`Login.cs:160`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; `PasswordPolicy:HistoryDepth=5`, `MaxAgeDays=90` |
| **Preconditions** | Fixture `pwhist@demo-lab.local` created by `admin@demo-lab.local`, active, unlocked, current password `Qms-Hist-000!`. `DELETE FROM saas.password_history WHERE user_id = <fixture id>;` so the history starts empty. Capture `password_hash` as `H0` and note `password_changed_at_utc IS NULL` (registration never stamps it). |
| **Test Data** | `{"tenantIdentifier":"demo-lab","email":"pwhist@demo-lab.local","currentPassword":"Qms-Hist-000!","newPassword":"Qms-Hist-001!"}` |
| **Steps** | 1. Send the request. 2. Assert `204` and an empty body. 3. `SELECT password_hash, password_changed_at_utc, failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='pwhist@demo-lab.local';`. 4. `SELECT id, user_id, password_hash, set_at_utc FROM saas.password_history WHERE user_id = <fixture id> ORDER BY set_at_utc DESC;`. 5. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type, tenant_id, actor, detail, ip_address FROM audit.security_event WHERE actor='pwhist@demo-lab.local' ORDER BY occurred_at_utc DESC LIMIT 3;`. 6. `SELECT entity_type, action, property, old_value, new_value, actor, reason, tenant_id FROM audit.field_change WHERE entity_id = '<fixture id>' ORDER BY occurred_at_utc DESC LIMIT 5;`. 7. `POST /api/auth/login` with `Qms-Hist-001!` → `200`; then with `Qms-Hist-000!` → `401` `AUTH-001`. |
| **Expected UI** | In the SPA's expired-password branch the new password is copied into the sign-in field and re-submitted automatically, landing the analyst on `/dashboard` (`login.component.ts:447-452`). |
| **Expected API** | Step 1 → `204`, empty body, no `Set-Cookie`. Step 7 first call → `200` with a non-empty `accessToken` and a `qams_rt` cookie; second call → `401` `application/problem+json`, `code` = `AUTH-001`, `title` = `Invalid credentials.`. |
| **Expected DB** | `password_hash` ≠ `H0`; `password_changed_at_utc` non-null and equal to the change instant; `failed_login_attempts = 0`; `locked_until_utc IS NULL`. `saas.password_history` holds exactly **one** row, with `user_id` = the fixture id, `password_hash` = `H0` (the *retired* hash, `Login.cs:219-224`) and `set_at_utc` = the change instant. |
| **Expected Audit** | `audit.security_event`: exactly one new row, `event_type='PASSWORD_CHANGED'`, `tenant_id` = the `demo-lab` id (the handler calls `tenantScope.Set` at `Login.cs:195`), `actor='pwhist@demo-lab.local'`, `detail IS NULL`, `ip_address IS NULL` (GAP-AUTH-005). `audit.field_change`: one `UserAccount`/`Modified`/`PasswordHash` row with `old_value = new_value = '«redacted»'`, one `UserAccount`/`Modified`/`PasswordChangedAtUtc` row with real timestamps, one `PasswordHistoryEntry`/`Created` row; all with `actor='system'` (no authenticated caller, `FieldChangeInterceptor.cs:113`), `reason IS NULL` (no `X-Change-Reason` is required on a POST) and `tenant_id` = `demo-lab`. |
| **Expected Notification** | n/a — no policy subscribes to `PASSWORD_CHANGED` (grepped `src/`). |
| **Cleanup** | Leave the fixture at `Qms-Hist-001!` for `TC-AUTH-API-067`…`-070`, which build on this history. |
| **Evidence** | HTTP capture · four SQL result sets · the two login responses |
| **Notes** | The `«redacted»` assertion satisfies URS-019 and must be exact — `FieldChangeInterceptor.cs:34` matches the substrings `password`, `secret`, `pin`, `hash`, `token` case-insensitively, so `PasswordHash` and `PasswordChangedAtUtc` are treated differently (the latter contains none of them). Note also that `PasswordHistoryEntry` is **not** in the interceptor's `Excluded` set (`FieldChangeInterceptor.cs:27-31`), which is why step 6 expects a `Created` row for it. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-067 — Re-proposing the current password is refused with AUTH-102  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · no URS covers the reuse ban — traced to `Login.cs:212-217`, opened as **GAP-AUTH-901** · RSK-AUTH-012 |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — the first disjunct of `hasher.Verify(user.PasswordHash, new) \|\| history.Any(...)` (`Login.cs:212-213`) driven true with the second false |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; `HistoryDepth = 5` |
| **Preconditions** | `pwhist@demo-lab.local` current password `Qms-Hist-001!`; `saas.password_history` holds exactly one row (hash of `Qms-Hist-000!`) from `TC-AUTH-API-066`. Capture `password_hash` and `password_changed_at_utc`. |
| **Test Data** | `{"tenantIdentifier":"demo-lab","email":"pwhist@demo-lab.local","currentPassword":"Qms-Hist-001!","newPassword":"Qms-Hist-001!"}` — old and new identical. |
| **Steps** | 1. Send the request. 2. Assert `422`. 3. Assert `code` = `AUTH-102` and read `title`. 4. Re-read `password_hash` and `password_changed_at_utc` and assert both unchanged. 5. `SELECT count(*) FROM saas.password_history WHERE user_id = <fixture id>;` and assert it is still `1`. 6. `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='pwhist@demo-lab.local';`. |
| **Expected UI** | Inline alert shows the server `title` verbatim: `The new password must differ from the last 6 passwords.` — this is one of the few change-password failures the SPA renders informatively, because the message lives in `title`, not in `errors`. |
| **Expected API** | `422` `application/problem+json` (the code has no `AUTH-`-prefixed 401 mapping issue here: `AUTH-102` **does** start with `AUTH-`, so `DomainExceptionHandler.cs:54-59` maps it to **`401`**, not 422 — assert the status the handler actually produces and record the discrepancy against the front matter's §1.5 table, which lists `AUTH-102` as `401`). Body: `code` = `AUTH-102`, `title` = `The new password must differ from the last 6 passwords.` (`Login.cs:215-216`, interpolating `HistoryDepth + 1 = 6`). |
| **Expected DB** | `password_hash` and `password_changed_at_utc` unchanged; `saas.password_history` count still `1`; `failed_login_attempts` still `0`; `locked_until_utc` still `NULL` (the reuse ban does not feed the lockout counter). |
| **Expected Audit** | Zero new `audit.security_event` rows — the `AUTH-102` branch throws before `security.WriteAsync` at `Login.cs:235`, so a reuse rejection is completely unlogged. Zero new `audit.field_change` rows. |
| **Expected Notification** | n/a — no policy subscribes. |
| **Cleanup** | None — nothing changed. |
| **Evidence** | HTTP capture · two SQL result sets |
| **Notes** | Two things to pin at execution: the exact HTTP status (`AUTH-102` matches the `AUTH-` prefix rule, so `401` is expected — the case must assert what the handler emits, and any mismatch with the front-matter table is a documentation defect to raise, not a silent reconciliation); and the literal `6` in the message, which is `HistoryDepth + 1` and changes with configuration. That an unlogged reuse rejection leaves no audit trail is recorded here and folded into **GAP-AUTH-901**. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-068 — The fifth-most-recent retired password is inside the ban and is refused  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · GAP-AUTH-901 (no URS) — traced to `Login.cs:207-217` · RSK-AUTH-012 |
| **Level / Type / Technique** | API · Functional (negative) · BVA — the last element inside `.Take(Math.Max(HistoryDepth,0))` = `.Take(5)` (`Login.cs:210`) |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; `HistoryDepth = 5` |
| **Preconditions** | Drive `pwhist@demo-lab.local` through six successive self-service changes: `Qms-Hist-000!` → `-001!` → `-002!` → `-003!` → `-004!` → `-005!` → `-006!`, each a distinct 13-character compliant password. After the sixth change the current password is `Qms-Hist-006!` and `SELECT count(*) FROM saas.password_history WHERE user_id = <id>;` returns **6** (rows holding the hashes of `-000!` … `-005!`). Confirm that count before proceeding. |
| **Test Data** | `{"tenantIdentifier":"demo-lab","email":"pwhist@demo-lab.local","currentPassword":"Qms-Hist-006!","newPassword":"Qms-Hist-001!"}` — `Qms-Hist-001!` is the fifth-most-recent retired hash by `set_at_utc DESC`, i.e. the last row `.Take(5)` returns. |
| **Steps** | 1. `SELECT password_hash, set_at_utc FROM saas.password_history WHERE user_id = <id> ORDER BY set_at_utc DESC;` and record the six rows in order. 2. Send the request. 3. Assert the status and that `code` = `AUTH-102`. 4. Re-read `qams.user_account.password_hash` and assert it is unchanged. 5. Assert `saas.password_history` still holds 6 rows. |
| **Expected UI** | Inline alert `The new password must differ from the last 6 passwords.` |
| **Expected API** | `401` `application/problem+json` (per the `AUTH-` prefix mapping at `DomainExceptionHandler.cs:54-59`); `code` = `AUTH-102`; `title` = `The new password must differ from the last 6 passwords.`. |
| **Expected DB** | `password_hash` unchanged; no new `saas.password_history` row; count still 6; `failed_login_attempts` still `0`. |
| **Expected Audit** | Zero new `audit.security_event` and `audit.field_change` rows. |
| **Expected Notification** | n/a — no policy subscribes. |
| **Cleanup** | Leave the fixture in this state for `TC-AUTH-API-069` and `-070`. |
| **Evidence** | The ordered six-row history listing from step 1 · HTTP capture · post-call history count |
| **Notes** | Step 1 is not optional: the boundary is defined by `set_at_utc DESC` ordering, so the test must prove which row is fifth before asserting that it is banned. The banned set is the current hash plus the newest five retired hashes = `{-001!, -002!, -003!, -004!, -005!, -006!}`, exactly the six the message names. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-069 — The sixth-most-recent retired password is outside the ban and is accepted, though it is still stored  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · GAP-AUTH-901, GAP-AUTH-903 — traced to `Login.cs:210` vs `Login.cs:228-233` · RSK-AUTH-012 |
| **Level / Type / Technique** | API · Functional (positive, as-built characterisation) · BVA — the first element **outside** `.Take(5)` |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; `HistoryDepth = 5` |
| **Preconditions** | Exactly the state left by `TC-AUTH-API-068`: current password `Qms-Hist-006!`, six history rows holding `-000!` … `-005!`, with `-000!` the oldest by `set_at_utc`. |
| **Test Data** | `{"tenantIdentifier":"demo-lab","email":"pwhist@demo-lab.local","currentPassword":"Qms-Hist-006!","newPassword":"Qms-Hist-000!"}` — `Qms-Hist-000!` is the **sixth**-most-recent retired hash: present in `saas.password_history`, but excluded by `.Take(5)`. |
| **Steps** | 1. Confirm with `SELECT count(*) FROM saas.password_history WHERE user_id = <id> AND password_hash = <H(-000!)>;` that the row for `-000!` is still stored (expect `1`). 2. Send the request. 3. Assert `204` — the password is accepted. 4. `POST /api/auth/login` with `Qms-Hist-000!` and assert `200`, proving the user is back on a password the system still has on file. 5. `SELECT count(*) FROM saas.password_history WHERE user_id = <id>;`. |
| **Expected UI** | No alert; the SPA auto-signs-in. Nothing warns the analyst that they have reverted to a stored former password. |
| **Expected API** | Step 2 → `204`. Step 4 → `200` with a non-empty `accessToken`. |
| **Expected DB** | `password_hash` now equals the stored `H(-000!)` value byte-for-byte only if PBKDF2 were deterministic — it is **not** (the ASP.NET Core Identity hasher salts per call), so assert instead that `hasher.Verify(H(-000!), 'Qms-Hist-000!')` is true for the retained row *and* that the new `qams.user_account.password_hash` also verifies against `Qms-Hist-000!`. `saas.password_history` count: 6 after the prune (`Login.cs:228-233` removes the oldest row and the new one is added). |
| **Expected Audit** | One `PASSWORD_CHANGED` row; `audit.field_change` gains the usual `PasswordHash`/`PasswordChangedAtUtc`/`PasswordHistoryEntry Created` rows **plus** one `PasswordHistoryEntry`/`Deleted` row for the pruned entry. |
| **Expected Notification** | n/a — no policy subscribes. |
| **Cleanup** | Leave for `TC-AUTH-API-070`. |
| **Evidence** | Step-1 count · HTTP capture · successful login response · post-call history count · the `Deleted` field-change row |
| **Notes** | `[ID]` because this documents behaviour no requirement states, and the behaviour is arguably wrong: the row proving reuse is **still in the table** and is simply not consulted, because `.Take(5)` is applied to a table the prune leaves holding 6 rows. Folded into **GAP-AUTH-903**. The salted-hash caveat in the DB row matters — never assert hash equality for PBKDF2; assert verification. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-070 — The history prune is off by one: six rows are retained for a depth of five  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · GAP-AUTH-903 — traced to `Login.cs:219-233` · RSK-AUTH-012 |
| **Level / Type / Technique** | API · Integration (data retention) · Loop — the invariant `count(saas.password_history) <= HistoryDepth` asserted across successive iterations of the change ceremony |
| **Priority / Severity / Automation** | High · Moderate · Yes (integration, needs a real PostgreSQL) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; `HistoryDepth = 5` |
| **Preconditions** | A **fresh** fixture `pwprune@demo-lab.local`, active, unlocked, initial password `Qms-Prune-00!`, with `DELETE FROM saas.password_history WHERE user_id = <id>;` executed first so the count starts at `0`. |
| **Test Data** | Eight successive self-service changes: `Qms-Prune-00!` → `-01!` → `-02!` → `-03!` → `-04!` → `-05!` → `-06!` → `-07!` → `-08!`. Each is 13 characters and satisfies all four classes. |
| **Steps** | 1. For n = 1..8: `POST /api/auth/change-password` moving from `Qms-Prune-{n-1:00}!` to `Qms-Prune-{n:00}!`, assert `204`, then immediately `SELECT count(*) FROM saas.password_history WHERE user_id = <id>;` and record the value. 2. Tabulate the eight counts. 3. Assert the observed sequence is `1, 2, 3, 4, 5, 6, 6, 6`. 4. After iteration 8, `SELECT password_hash, set_at_utc FROM saas.password_history WHERE user_id = <id> ORDER BY set_at_utc;` and verify the oldest retained row corresponds to `Qms-Prune-02!`. 5. Attempt a change from `-08!` to `Qms-Prune-02!` and assert it is **accepted** (`204`) even though that hash is row 1 of the table. |
| **Expected UI** | n/a — this case is asserted entirely at the API and database layers; the SPA shows nothing distinguishable. |
| **Expected API** | All nine change requests → `204`. |
| **Expected DB** | The count sequence is `1, 2, 3, 4, 5, 6, 6, 6` — it stabilises at **6**, one above `HistoryDepth = 5`. Mechanism: `Login.cs:219` stages the new entry with `db.PasswordHistory.Add(...)` but the prune query at `:228-232` executes against the database, where the staged row is not yet visible; so `Skip(5)` on a table holding 5 rows removes nothing, and the sixth row is committed. From iteration 7 onward the query sees 6 rows, removes exactly 1, and the count is pinned at 6. Step 5 succeeds because `.Take(5)` never reaches the oldest of the six. |
| **Expected Audit** | Iterations 1–6 each produce one `PasswordHistoryEntry`/`Created` field-change row and no `Deleted` row; iterations 7 and 8 each produce one `Created` **and** one `Deleted` row. Nine `PASSWORD_CHANGED` rows in `audit.security_event`, all tenant-stamped to `demo-lab`. |
| **Expected Notification** | n/a — no policy subscribes. |
| **Cleanup** | `POST /api/users/{id}/deactivate` as `admin@demo-lab.local`, then `DELETE FROM saas.password_history WHERE user_id = '<id>'; DELETE FROM qams.user_account WHERE id = '<id>';`. |
| **Evidence** | The nine-row count table from step 2 · the ordered history listing from step 4 · the accepted step-5 response · the `Created`/`Deleted` field-change rows |
| **Notes** | `[ID]` — this is an as-built characterisation of a defect, not of a requirement. Two consequences: retention overshoots the configured depth by one credential hash (a data-minimisation concern on a table that has no RLS and no purge, GAP-AUTH-006), and the extra row is dead weight because the reuse check never reads it. Raised as **GAP-AUTH-903**. Do not "fix" the expected sequence to `1,2,3,4,5,5,5,5` — the case must record the measured behaviour; the corrected sequence belongs in the gap's acceptance criteria. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-071 — A wrong current password neither advances the lockout counter nor writes a security event  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003, URS-016 (both **partially conform** — satisfied on `/api/auth/login`, not here) · RSK-AUTH-013 |
| **Level / Type / Technique** | API · Security (negative, as-built characterisation) · Decision Table — the fourth disjunct of the compound guard at `Login.cs:200-204`, contrasted against `Login.cs:82-87` |
| **Priority / Severity / Automation** | Critical · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; the `auth` partition permits 10 requests per minute, so this case fits inside one window |
| **Preconditions** | Fixture `pwguess@demo-lab.local`, active, `failed_login_attempts = 0`, `locked_until_utc IS NULL`, current password `Qms-Guess-01!`. Record the current maximum `occurred_at_utc` in `audit.security_event` as the watermark. |
| **Test Data** | Five requests, each `{"tenantIdentifier":"demo-lab","email":"pwguess@demo-lab.local","currentPassword":"Wrong-Guess-9!","newPassword":"Qms-Guess-02!"}`. Control: one `POST /api/auth/login` with `{"tenantIdentifier":"demo-lab","email":"pwguess@demo-lab.local","password":"Wrong-Guess-9!"}`. |
| **Steps** | 1. Send the change-password request five times. 2. Assert each returns `401` with `code` = `AUTH-001` and `title` = `Invalid credentials.`. 3. `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='pwguess@demo-lab.local';`. 4. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type, count(*) FROM audit.security_event WHERE actor='pwguess@demo-lab.local' AND occurred_at_utc > <watermark> GROUP BY event_type;`. 5. Send the control login request once. 6. Re-run the queries in steps 3 and 4. 7. `POST /api/auth/login` with the **correct** password `Qms-Guess-01!` and assert `200`. |
| **Expected UI** | The sign-in page shows `Invalid credentials.` on each of the five attempts and never displays a lockout message. |
| **Expected API** | All five change-password attempts → `401` `application/problem+json`, `code` = `AUTH-001`, `title` = `Invalid credentials.`. The control login → `401` `AUTH-001` with the same body. Step 7 → `200`. |
| **Expected DB** | After step 3: `failed_login_attempts = 0` and `locked_until_utc IS NULL` — five wrong current-password attempts leave the account completely untouched, because the guard at `Login.cs:200-204` throws without calling `RegisterFailedLogin`. After step 6: `failed_login_attempts = 1` — the single control login *did* increment it (`Login.cs:84-85`), which is the discriminating measurement. |
| **Expected Audit** | Step 4 → **zero** rows: no `LOGIN_FAILED`, no event of any type, for the five change-password failures. Step 6 → exactly one `LOGIN_FAILED` row with `detail='bad-password'` from the control login (`Login.cs:86,152`). The asymmetry between two credential-verifying endpoints is the finding. |
| **Expected Notification** | n/a — no policy subscribes to login failures. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts = 0, locked_until_utc = NULL WHERE email='pwguess@demo-lab.local';` |
| **Evidence** | Six HTTP captures · the two `user_account` reads · the two security-event group-by results |
| **Notes** | `[ID]` and pre-registered as **GAP-AUTH-009** in the front matter. Do not author a case asserting a lockout after five wrong current-passwords — it will fail. The only throttle on this oracle is the 10/min `auth` partition, which `TC-AUTH-API-075`…`-081` characterise; note that the partition is per client address, so an attacker distributing across addresses is unbounded and, per step 4, invisible. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-072 — An administrative reset clears the lockout, leaves the age stamp null and writes no history row  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-009 (conforms), URS-002 (partially conforms) · RSK-AUTH-012 |
| **Level / Type / Technique** | API · Functional (positive, as-built characterisation) · State Transition — `S2 Active-Locked → S1 Active-Unlocked` via `ResetPassword`, the only implemented unlock path (front matter §3.1) |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin (`admin@demo-lab.local`) · **`users.manage`** (`UsersController.cs:84`, and command policy `[RequirePermissionPolicy(Users, Manage)]` at `UserManagement.cs:96`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fixture `pwreset@demo-lab.local`, active, current password `Qms-Reset-01!`, with at least one self-service change already performed so `password_changed_at_utc IS NOT NULL` and `saas.password_history` holds exactly 1 row. Then lock it: five failed `POST /api/auth/login` attempts, or directly `UPDATE qams.user_account SET locked_until_utc = now() + interval '30 minutes', failed_login_attempts = 0 WHERE email='pwreset@demo-lab.local';` (note the counter is **zero** on a locked account — `RegisterFailedLogin` clears it at lockout, `UserAccount.cs:212-217`). Signed in as `admin@demo-lab.local` with `users.manage`. |
| **Test Data** | `POST /api/users/{fixtureId}/reset-password` with `{"newPassword":"Qms-Reset-02!"}` |
| **Steps** | 1. Capture `password_hash`, `password_changed_at_utc`, `locked_until_utc`, `failed_login_attempts`, and `SELECT count(*) FROM saas.password_history WHERE user_id = <id>;`. 2. Send the reset. 3. Assert `204`. 4. Re-read all five values from step 1. 5. `POST /api/auth/login` with `Qms-Reset-02!` immediately and assert `200` — no waiting out the 30-minute lock. 6. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type FROM audit.security_event WHERE actor='pwreset@demo-lab.local' ORDER BY occurred_at_utc DESC LIMIT 3;`. |
| **Expected UI** | In the Users screen the masked reset prompt closes, the facade reloads the list (`users.facade.ts:42,45-49`) and no error banner appears. The list shows no lock indicator — the DTO carries no lockout field (`UserDto`, `Contracts/IdentityAccess/UserContracts.cs:3-14`), so an administrator cannot see that the lock was cleared. |
| **Expected API** | Step 3 → `204 No Content` (`UsersController.cs:88`). Step 5 → `200` with a non-empty `accessToken` and a `qams_rt` cookie. |
| **Expected DB** | `password_hash` changed. `locked_until_utc` → `NULL` and `failed_login_attempts` → `0` (`UserAccount.ResetPassword`, `UserAccount.cs:144-147`). **`password_changed_at_utc` → `NULL`** — `ResetPassword`'s `at` parameter defaults to `null` and `ResetUserPasswordHandler` passes nothing (`UserAccount.cs:136`; `UserManagement.cs:147`), so a previously stamped account is reset to "ageless" and the 90-day expiry check at `Login.cs:90-92` is thereafter skipped for it. **`saas.password_history` count unchanged at 1** — the reset appends no retired hash. |
| **Expected Audit** | `audit.field_change`: `UserAccount`/`Modified` rows for `PasswordHash` (`«redacted»` both sides), `PasswordChangedAtUtc` (old = the previous timestamp, new = empty/null rendering), and `LockedUntilUtc` (old = the lock instant, new = null); `actor` = the administrator's display name; `tenant_id` = `demo-lab`. **No** `PasswordHistoryEntry`/`Created` row. `audit.security_event`: **no** event for the reset itself — only the `LOGIN_SUCCESS` from step 5. |
| **Expected Notification** | n/a — no policy subscribes to password reset. |
| **Cleanup** | `DELETE FROM saas.password_history WHERE user_id = '<id>';` then deactivate and delete the fixture. |
| **Evidence** | Two five-value SQL captures (before/after) · HTTP captures for steps 2 and 5 · the field-change rows · the security-event listing |
| **Notes** | `[ID]` — three implemented behaviours that no requirement states, all consequential: the reset is the only unlock (GAP-AUTH-013), it silently disables password aging for that account, and it writes no security event at all, so a Part-11 reviewer cannot see who reset whose credential from `audit.security_event` (only from `audit.field_change`). The missing history append is the setup for `TC-AUTH-API-073` and is raised as **GAP-AUTH-902**. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-073 — An administrative reset lets the user immediately return to the pre-reset password  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 (**partially conforms** — the reuse ban is bypassable), GAP-AUTH-902 · RSK-AUTH-012 |
| **Level / Type / Technique** | API · Security (negative, as-built characterisation) · Data Flow — tracing the retired hash from `qams.user_account.password_hash` to `saas.password_history` and showing the administrative-reset path never makes that write |
| **Priority / Severity / Automation** | Critical · Major · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (victim) + TenantAdmin (`admin@demo-lab.local`, actor for step 2) · **`users.manage`** for the reset; anonymous for the change · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; `HistoryDepth = 5` |
| **Preconditions** | Fixture `pwbypass@demo-lab.local`, active, unlocked, with an **empty** `saas.password_history` and current password `Qms-Bypass-A1!`. |
| **Steps** | 1. As the user, `POST /api/auth/change-password` from `Qms-Bypass-A1!` to `Qms-Bypass-B2!`; assert `204` and that `saas.password_history` now holds exactly 1 row (the hash of `A1`). 2. As the user, attempt to change from `Qms-Bypass-B2!` back to `Qms-Bypass-A1!`; assert it is **refused** with `code` = `AUTH-102` — the reuse ban working correctly, and the control arm for this case. 3. As `admin@demo-lab.local`, `POST /api/users/{id}/reset-password` with `{"newPassword":"Qms-Bypass-C3!"}`; assert `204`. 4. `SELECT count(*) FROM saas.password_history WHERE user_id = <id>;` and assert it is still `1` (the reset appended nothing — the `B2` hash was never retired). 5. As the user, `POST /api/auth/change-password` from `Qms-Bypass-C3!` back to `Qms-Bypass-B2!`. 6. Assert `204` — the immediately-previous password is accepted. 7. `POST /api/auth/login` with `Qms-Bypass-B2!` and assert `200`. |
| **Test Data** | Three 14-character compliant passwords: `Qms-Bypass-A1!`, `Qms-Bypass-B2!`, `Qms-Bypass-C3!`. |
| **Expected UI** | Step 2 shows the alert `The new password must differ from the last 6 passwords.`; steps 5 and 7 show no alert at all — the SPA gives the user no signal that they have circled back to a password that was in force minutes earlier. |
| **Expected API** | Step 1 → `204`. Step 2 → `401` `application/problem+json`, `code` = `AUTH-102`. Step 3 → `204`. Step 5 → `204`. Step 7 → `200`. |
| **Expected DB** | After step 4, `saas.password_history` holds exactly 1 row (hash of `A1`) — no row for `B2`. After step 6 it holds 2 rows (hashes of `A1` and `C3`); still no row for `B2`, which was retired by the reset rather than by a change and is therefore permanently absent from the ban set. |
| **Expected Audit** | `audit.security_event`: `PASSWORD_CHANGED` for steps 1, 5; none for the `AUTH-102` rejection at step 2; none for the administrative reset at step 3; `LOGIN_SUCCESS` for step 7. `audit.field_change`: the reset (step 3) produces `PasswordHash`/`PasswordChangedAtUtc` `Modified` rows but **no** `PasswordHistoryEntry`/`Created` row — that absence is the primary evidence artefact for this case. |
| **Expected Notification** | n/a — no policy subscribes. |
| **Cleanup** | `DELETE FROM saas.password_history WHERE user_id = '<id>';` then deactivate and delete the fixture. |
| **Evidence** | Seven HTTP captures · history counts after steps 1, 4 and 6 · the field-change listing for step 3 showing the missing `Created` row |
| **Notes** | `[ID]`. The exploit is mundane and realistic: a user who wants to keep a familiar password asks an administrator for a reset and changes straight back. Step 2 is the essential control — without it the case could be misread as the reuse ban never working. Raised as **GAP-AUTH-902**; acceptance criteria are stated there. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-074 — A rejected password tells the API caller which rule failed but tells the user only "Validation failed."  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002, URS-075 · RSK-AUTH-010 · **depends on GAP-AUTH-907** |
| **Level / Type / Technique** | E2E (browser) + API · Usability / contract · Use Case — the analyst's rotate-an-expired-password journey, asserted at both layers |
| **Priority / Severity / Automation** | Medium · Moderate · Partial — the API arm automates; the SPA arm needs Playwright |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; SPA at `localhost:4200/t/demo-lab`, started via `scripts/dev-up.ps1` |
| **Preconditions** | Fixture `pwui@demo-lab.local`, active, unlocked, current password `Qms-Ui-Cur-01!`, with `password_changed_at_utc` back-dated past the 90-day limit (`UPDATE qams.user_account SET password_changed_at_utc = now() - interval '100 days' WHERE email='pwui@demo-lab.local';`) so signing in returns `AUTH-101` and the SPA enters its expired-password branch (`login.component.ts:480-483`). |
| **Test Data** | Rejected new password `short1!A` (8 characters — fails minimum length, complexity passes, blocklist passes: exactly one message expected). |
| **Steps** | 1. **API arm:** `POST /api/auth/change-password` with `newPassword=short1!A`; capture the full body. 2. Assert `400`, `title` = `Validation failed.`, no `code`, and `errors.NewPassword` = `["The password must be at least 12 characters."]`. 3. **SPA arm:** open `localhost:4200/t/demo-lab`, sign in with `pwui@demo-lab.local` / `Qms-Ui-Cur-01!`. 4. Assert the new-password field appears (`#newPassword`, `login.component.ts:118-122`) and its hint text is read. 5. Enter `short1!A` and submit. 6. Read the text of the `role="alert"` element (`login.component.ts:135`). 7. Assert it reads exactly `Validation failed.` and contains none of the substrings `12`, `characters`, `upper`, `digit`, `symbol`, `breach`. |
| **Expected UI** | Step 4: the hint under the new-password field reads `At least 10 characters; must differ from your recent passwords.` in `en` (`core/i18n.service.ts:1015`) — **wrong by two characters**, see `TC-AUTH-API-074`'s sibling finding below. Step 6/7: the alert reads exactly `Validation failed.`; the analyst is given no way to learn which rule they broke and must guess. The password field retains its value and stays enabled. |
| **Expected API** | `400` `application/problem+json`; `title` = `Validation failed.`; `errors.NewPassword` = one message naming the 12-character floor; `traceId` present; **no** `code` extension — the `ValidationException` arm of `DomainExceptionHandler.cs:34-44` is the only error arm in the whole handler that omits `code`, so URS-075's "stable machine-readable code on every error response" is not met on validation failures. |
| **Expected DB** | `password_hash` and `password_changed_at_utc` unchanged; no `saas.password_history` row. |
| **Expected Audit** | Zero new `audit.field_change` and `audit.security_event` rows. |
| **Expected Notification** | n/a — the request never reaches a handler. |
| **Cleanup** | `UPDATE qams.user_account SET password_changed_at_utc = now() WHERE email='pwui@demo-lab.local';` then deactivate and delete the fixture. |
| **Evidence** | HTTP capture with the full `errors` object · Playwright screenshot of the alert · the DOM text of the `role="alert"` node · the hint text captured at step 4 |
| **Notes** | `[GD]` on **GAP-AUTH-907**: the case cannot record a passing outcome, because the informative server payload is discarded by every SPA error path inspected (`login.component.ts:456,485`; `users.facade.ts:64-69`). Acceptance criteria for the gap: (a) the SPA renders every message in `problem.errors[*]` beneath the offending field, keyed by property name, and falls back to `title` only when `errors` is absent; (b) the `ValidationException` arm of `DomainExceptionHandler` adds a stable `code` (e.g. `VALIDATION-400`) so URS-075 holds on every error path; (c) a Playwright assertion pins the rendered message text for a too-short password. The step-4 hint discrepancy is a separate finding, **GAP-AUTH-908**: `core/i18n.service.ts:1015` states 10 characters in all three of `en`/`ar`/`fr` while `PasswordRules.MinLength = 12`, so the interface actively misinstructs the user; acceptance criteria: the three strings state 12, and a frontend unit test binds the hint to a shared constant rather than a literal. |
| **Result / Defect** | Not Run · — |

---

## The `auth` rate-limit partition — 10 requests per minute per client address

All seven cases below run **last** in the AUTH execution order: they exhaust the shared `auth` partition for the executing client address, and every other `AuthController` case (`-060`…`-074`, plus batches A–C's login and MFA cases) will 429 until the window rolls (ground truth §3, "run credential-burst probes last").

#### TC-AUTH-API-075 — The tenth request passes and the eleventh is rejected with 429 and Retry-After 60  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-071 · RSK-AUTH-013 |
| **Level / Type / Technique** | API · Security (negative) · BVA — the permit boundary `AuthPermitPerMinute = 10` (`RateLimiting.cs:24`), asserted at n=10 and n=11 |
| **Priority / Severity / Automation** | Critical · Major · Yes (functional — extends `SecurityHardeningTests.A_burst_of_login_attempts_is_rejected_with_429`) |
| **Role / Permission / Tenant** | Anonymous · n/a — the limiter runs before authorization (`Program.cs:263-270`) · none (the burst targets a nonexistent account) |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`. `RateLimit:*` is absent from `appsettings.json`, so `RateLimitSettings.From` supplies the defaults 300 / **10** / 10 / 60 (`RateLimiting.cs:22-26`); window is a fixed 1 minute with `QueueLimit = 0` (`RateLimiting.cs:51,71-77`). Confirm no `RateLimit__AuthPermitPerMinute` environment override is set on the running process. |
| **Preconditions** | The `auth` partition for this client address is unused — wait ≥ 60 s after any prior `AuthController` traffic, or restart the API. Record the current maximum `occurred_at_utc` in `audit.security_event` as the watermark. |
| **Test Data** | Eleven sequential `POST /api/auth/login` with `{"tenantIdentifier":"demo-lab","email":"burst@nowhere.test","password":"Wrong-Burst-1!"}` — a nonexistent account, so no real user is locked out. |
| **Steps** | 1. Issue requests 1–10 sequentially from a single client address, recording the status of each. 2. Assert requests 1–10 all return `401` and none returns `429`. 3. Issue request 11. 4. Assert its status is `429`. 5. Assert the response carries `Retry-After: 60`. 6. Issue request 12 immediately and assert it is also `429`. |
| **Expected UI** | The sign-in page's alert shows `Sign-in failed.` — the SPA's fallback text, because a `429` from the limiter carries no `title` for `err.error?.title` to read (`login.component.ts:485`). |
| **Expected API** | Requests 1–10 → `401` `application/problem+json`, `code` = `AUTH-001`, `title` = `Invalid credentials.`. Requests 11 and 12 → `429 Too Many Requests` with header `Retry-After: 60` (`RateLimiting.cs:55-61`; the value is `((int)Window.TotalSeconds).ToString()` with `Window = 1 minute`). |
| **Expected DB** | Requests 1–10 each execute the tenant lookup and the user lookup (`Login.cs:49,70`) and write one security-event row; requests 11 and 12 execute no query at all — `UseRateLimiter` sits at `Program.cs:265`, before `TenantResolutionMiddleware` and `ActiveSessionMiddleware`, and rejects before MVC routing reaches the handler. |
| **Expected Audit** | Exactly **10** new `audit.security_event` rows above the watermark, all `event_type='LOGIN_FAILED'`, `detail='no-such-user'` (`Login.cs:74`), `actor='burst@nowhere.test'`, `tenant_id` = the `demo-lab` id (the slug resolved before the user lookup failed, `Login.cs:58`), `ip_address IS NULL`. Zero rows for requests 11 and 12 — the throttled attempts are invisible to the security ledger, which `TC-AUTH-API-081` asserts directly. |
| **Expected Notification** | n/a — no policy subscribes to login failures. |
| **Cleanup** | Wait ≥ 60 s for the fixed window to roll before running any further `AuthController` case, or restart the API with `scripts/dev-rebuild.ps1`. |
| **Evidence** | Twelve HTTP captures with status and full header set · the `audit.security_event` count above the watermark |
| **Notes** | The existing functional test proves only "some request after the budget is 429" against a shrunk budget of 3; this case pins the **production default of 10** and the exact 10/11 boundary against the running API. Sequential issue matters: with `QueueLimit = 0` a concurrent burst can interleave non-deterministically around the boundary. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-076 — A 429 carries no problem+json body and no machine-readable code  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-075 (**does not conform** on the throttled path), URS-071 · RSK-AUTH-014 · **depends on GAP-AUTH-906** |
| **Level / Type / Technique** | API · Contract (negative) · Error Guessing — probing the one error path that bypasses `ProblemResponse.WriteAsync` |
| **Priority / Severity / Automation** | High · Moderate · Yes (functional — belongs in `ProblemContractTests`) |
| **Role / Permission / Tenant** | Anonymous · n/a — the limiter runs before authorization · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | The `auth` partition already exhausted by `TC-AUTH-API-075` (or by 10 fresh requests), so the next request is guaranteed to be rejected. |
| **Test Data** | One `POST /api/auth/login` with any well-formed body, issued while the partition is exhausted. |
| **Steps** | 1. Issue the request and capture the complete response: status line, every header, and the raw body bytes. 2. Assert the status is `429`. 3. Assert `Retry-After: 60` is present. 4. Assert the `Content-Type` header is **absent** or does not equal `application/problem+json`. 5. Assert `Content-Length: 0` (or an empty body). 6. Assert the body contains no `code`, no `title`, no `traceId` and no `correlationId`. 7. Assert the defensive header set from `SecurityHeadersMiddleware` **is** present (that middleware sits at `Program.cs:255`, ahead of the limiter at `:265`), specifically `X-Content-Type-Options: nosniff` and `X-Frame-Options: DENY`. 8. Repeat steps 1–6 against `POST /api/documents/{id}/publish` with the `esignature` partition exhausted, to confirm the defect is in the shared `OnRejected` handler and not endpoint-specific. |
| **Expected UI** | The SPA alert falls back to `Sign-in failed.` (login) or `Request failed (429).` (`users.facade.ts:66`) — in both cases the user is not told they were throttled or when to retry, even though `Retry-After` is on the wire. |
| **Expected API** | `429` with `Retry-After: 60`, the security headers, and **an empty body**. `RateLimiting.Configure`'s `OnRejected` sets only the status code and the `Retry-After` header (`RateLimiting.cs:55-61`) and never calls `ProblemResponse.WriteAsync`, so no `code`, `title`, `traceId` or `correlationId` is emitted. Step 8 behaves identically. |
| **Expected DB** | Nothing read, nothing written. |
| **Expected Audit** | Zero new `audit.security_event` and `audit.field_change` rows. |
| **Expected Notification** | n/a — the request never reaches a handler. |
| **Cleanup** | Wait ≥ 60 s for the window to roll. |
| **Evidence** | Raw HTTP capture (status line, all headers, body bytes, `Content-Length`) for both endpoints |
| **Notes** | `[GD]` on **GAP-AUTH-906**. URS-075 states "**every** error response shall be `application/problem+json` with a stable machine-readable code (incl. framework 401/403)" (`docs/validation/06-Revalidation-Delta-v1.38-v1.50.md:87`), and `ProblemContractTests` does not exercise `429` — grepped, the file contains no `TooManyRequests` assertion — so the merge gate does not catch it. Acceptance criteria: (a) `OnRejected` writes through `ProblemResponse.WriteAsync` with `status=429`, a stable `code` (e.g. `RATE-LIMIT-429`), a `title` naming the retry window, and the `retryAfterSeconds` value as an extension; (b) `ProblemContractTests` gains a `429` case; (c) the SPA surfaces the retry window to the user. Until then this case records the as-built empty body — do not write it as a pass. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-077 — A credential burst poisons the shared partition and disables the anonymous workspace lookup for the whole address  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-071, URS-089, URS-090 · RSK-AUTH-013 |
| **Level / Type / Technique** | API · Security (denial of service) · Decision Table — endpoint × partition membership, showing that all ten `AuthController` routes share one budget key |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Anonymous for the burst and the lookup; Analyst with a valid `scope=full` bearer for the privileges probe · n/a for the anonymous arms; **no permission** is required by `GET /api/auth/me/privileges` (`AuthController.cs:147-150`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; all requests must originate from the **same** client address, because the partition key is the address alone (`RateLimiting.ClientKey`, `RateLimiting.cs:97-98`) |
| **Preconditions** | Fresh `auth` window (wait ≥ 60 s). Obtain a valid access token for `admin@demo-lab.local` **before** starting the burst, so the privileges probe does not itself need a login. |
| **Test Data** | Burst: 10 × `POST /api/auth/login` with `{"tenantIdentifier":"demo-lab","email":"burst2@nowhere.test","password":"Wrong-Burst-2!"}`. Probes, issued immediately after and from the same address: (a) `GET /api/auth/workspace/demo-lab` with no credentials; (b) `GET /api/auth/me/privileges` with `Authorization: Bearer <valid token>`; (c) `POST /api/auth/logout` with no credentials; (d) control — `GET /api/users` with the same bearer. |
| **Steps** | 1. Issue the 10-request burst; confirm all ten return `401`. 2. Immediately issue probe (a); record the status. 3. Issue probe (b); record the status. 4. Issue probe (c); record the status. 5. Issue probe (d); record the status. 6. Wait 61 s and re-issue probe (a); record the status. |
| **Expected UI** | While the partition is exhausted, a visitor opening `/t/demo-lab` from the same address sees the workspace pill fall back to the slug label `Demo Lab` instead of `Demo Laboratory` — the SPA's `error:` arm treats a `429` exactly like a `404` (`login.component.ts:421`), so the branding silently degrades with no explanation. A signed-in user's privilege bootstrap fails and the SPA's navigation renders as if no privileges were granted. |
| **Expected API** | Probes (a), (b) and (c) → `429` with `Retry-After: 60` and an empty body: all three routes live on `AuthController`, which carries the class-level `[EnableRateLimiting(RateLimiting.AuthPolicy)]` (`AuthController.cs:18`), and the policy's partition key ignores the path entirely. Probe (d) → `200`: `UsersController` declares no rate-limit policy (front matter §1.7), so only the 300/min global limiter applies. Step 6 → `200` with `{"name":"Demo Laboratory"}`. |
| **Expected DB** | The ten burst requests each write a security-event row; the three throttled probes execute no query. Probe (d) executes the user-list query normally. |
| **Expected Audit** | Ten `LOGIN_FAILED` rows with `detail='no-such-user'` from the burst; nothing from probes (a)–(c). |
| **Expected Notification** | n/a — no policy subscribes. |
| **Cleanup** | Wait ≥ 60 s before any further `AuthController` case. |
| **Evidence** | Fourteen HTTP captures · a table of probe → status · the step-6 recovery capture |
| **Notes** | `[ID]` — the shared partition is deliberate (the whole credential surface is one budget by design, `RateLimiting.cs:29-34`), but three consequences are undocumented and are folded into **GAP-AUTH-905**: an unauthenticated attacker can suppress a laboratory's pre-authentication branding for everyone behind the same NAT address; a signed-in user's `me/privileges` bootstrap and `me/language` preference share the credential-guessing budget; and logout — a session-termination action — is throttled by the same attacker-controlled counter. `RateLimiting.cs:45-48` already acknowledges the NAT-sharing problem for `refresh` and gives it a 60/min budget; the same reasoning has not been applied to the read-only and session-ending routes on the same controller. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-078 — The refresh endpoint's method-level policy replaces the controller's, giving it its own 60/min budget  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-071, URS-074 · RSK-AUTH-013 |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — policy-attribute resolution: controller-level vs method-level `[EnableRateLimiting]` on the same endpoint |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | Anonymous — the `qams_rt` cookie is the credential (`AuthController.cs:59-72`) · n/a · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; defaults `AuthPermitPerMinute = 10`, `RefreshPermitPerMinute = 60` (`RateLimiting.cs:24,26`) |
| **Preconditions** | Fresh `auth` and `refresh` windows. Sign in once as `pwrefresh@demo-lab.local` and retain the `qams_rt` cookie in a file for `curl.exe -b cookies.txt` — PowerShell drops manually-set `Cookie` headers (ground truth §3). |
| **Test Data** | Burst: 10 × `POST /api/auth/login` with a bad password against a nonexistent account, exhausting the `auth` partition. Probe: `POST /api/auth/refresh` with **no** cookie (so the outcome is a deterministic `AUTH-009` rather than a rotation that would invalidate the fixture's session). |
| **Steps** | 1. Exhaust the `auth` partition with the 10-request burst; confirm the 11th login is `429`. 2. Immediately `POST /api/auth/refresh` with no cookie. 3. Assert the status is **not** `429`. 4. Assert it is `401` with `code` = `AUTH-009` and `title` = `The session has expired.` (`RefreshSessions.cs:89-90`). 5. Issue 59 further cookieless refresh requests (60 in total for the window) and assert none returns `429`. 6. Issue the 61st and assert it returns `429` with `Retry-After: 60`. 7. Re-issue a login and confirm it is still `429` — the two partitions expire independently. |
| **Expected UI** | The SPA's silent-refresh interceptor is unaffected by a credential burst: a signed-in user's session survives an attacker exhausting the login budget from the same address. That is the design intent recorded at `RateLimiting.cs:45-48`. |
| **Expected API** | Step 2 → `401` `application/problem+json`, `code` = `AUTH-009`. Steps 3–5 → no `429` within the first 60 refresh requests. Step 6 → `429` with `Retry-After: 60`. Step 7 → `429`. |
| **Expected DB** | Cookieless refreshes return before any query (`RefreshSessions.cs:89-90` throws on `TryParse` failure), so no `SELECT` against `qams.refresh_session` is issued and **no** `REFRESH_INVALID` event is written (that log line at `:97` sits after the parse guard). |
| **Expected Audit** | Zero new `audit.security_event` rows from the 61 refresh requests. Ten `LOGIN_FAILED` rows from the burst. |
| **Expected Notification** | n/a — no policy subscribes. |
| **Cleanup** | Wait ≥ 60 s for both windows; delete the cookie jar. |
| **Evidence** | HTTP captures for the 11th login, the first and 61st refresh, and the step-7 login · a status tally across the 61 refresh requests |
| **Notes** | `[ID]` — the behaviour follows from ASP.NET Core's `Endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()` returning the **last** matching attribute, and action-level metadata is ordered after controller-level, so `RefreshPolicy` (`AuthController.cs:66`) replaces rather than compounds `AuthPolicy` (`:18`). Step 5 is the load-bearing assertion: if the two policies *compounded*, the 11th refresh would 429 on the auth budget. Whichever way it measures, record the observed count at which the first `429` appears — that number is the finding. The global 300/min limiter applies on top of whichever policy wins (`Program.cs:265`; `RateLimiting.cs:63-69`) and is not exercised here. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-079 — A forwarded client address from a trusted peer repartitions the budget  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-071, URS-073 · RSK-AUTH-013 · relates to **GAP-AUTH-905** |
| **Level / Type / Technique** | API · Security (evasion) · Pairwise — source address (`::1`, `127.0.0.1`) × forwarded header (absent, value A, value B) |
| **Priority / Severity / Automation** | Critical · Major · Partial — needs control of the source address family, so it automates only in an environment where both loopback families are reachable |
| **Role / Permission / Tenant** | Anonymous · n/a — the limiter runs before authorization · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`. `Program.cs:147-148` configures `ForwardedHeadersOptions.ForwardedHeaders = XForwardedFor \| XForwardedProto` and **sets neither `KnownProxies` nor `KnownNetworks`**, so both stay at the framework defaults (IPv6 loopback only), with `ForwardLimit = 1`. `app.UseForwardedHeaders()` runs first in the pipeline (`Program.cs:250`), ahead of `UseRateLimiter` at `:265`. |
| **Preconditions** | Fresh `auth` window. Both loopback endpoints reachable: verify `curl.exe -6 -g "http://[::1]:5080/health/live"` and `curl.exe -4 "http://127.0.0.1:5080/health/live"` both return `200`. |
| **Test Data** | Arm 1 (IPv6 loopback source, header A): 10 × `POST http://[::1]:5080/api/auth/login` with `X-Forwarded-For: 203.0.113.10`. Arm 2 (same source, header B): 1 × the same request with `X-Forwarded-For: 203.0.113.11`. Arm 3 (same source, no header): 1 × the same request with no `X-Forwarded-For`. Arm 4 (IPv4 loopback source, header A): 10 × `POST http://127.0.0.1:5080/api/auth/login` with `X-Forwarded-For: 198.51.100.10`, then 1 × with `X-Forwarded-For: 198.51.100.11`. |
| **Steps** | 1. Run arm 1 and assert all ten return `401`. 2. Run an 11th request identical to arm 1 and assert `429` — the `203.0.113.10` partition is exhausted. 3. Run arm 2 and record the status. 4. Run arm 3 and record the status. 5. Wait ≥ 60 s. 6. Run arm 4 and record the status of its 11th request. 7. Tabulate source × header × status. |
| **Expected UI** | n/a — this case operates below the SPA. |
| **Expected API** | Arm 1 requests 1–10 → `401`; request 11 → `429`. Arm 2 → **`401`, not `429`** — the forwarded address is honoured because the peer `::1` is in the default `KnownProxies`, so changing the header value moves the request into a fresh partition. Arm 3 → `401` or `429` depending on whether the unforwarded `::1` partition is separately tracked; record the observed value. Arm 4 request 11 → **`429`**, because the peer `127.0.0.1` is an IPv4 address that matches neither the default `KnownProxies` entry (`::1`) nor the default `KnownNetworks` entry (`::1/128`), so the forwarded header is discarded and all ten requests share the `127.0.0.1` partition. |
| **Expected DB** | Every non-throttled request executes the login queries and writes one security-event row; throttled ones execute nothing. |
| **Expected Audit** | Count `LOGIN_FAILED` rows above the watermark and assert the total equals the number of non-`429` responses across all arms — an exact accounting that also serves as the evidence for `TC-AUTH-API-081`. |
| **Expected Notification** | n/a — no policy subscribes. |
| **Cleanup** | Wait ≥ 60 s for every partition touched. |
| **Evidence** | The source × header × status table · raw captures for arms 1(11), 2, 3 and 4(11) |
| **Notes** | `[ID]`. Two opposite operational consequences, both real and both undocumented, folded into **GAP-AUTH-905**: (i) any client that can reach the API from a trusted peer address can mint an unlimited number of rate-limit partitions by varying one header, defeating URS-071 entirely; (ii) in the ADR-0002 topology, where TLS terminates on a proxy that is *not* on loopback, `KnownProxies` is empty of that proxy, the forwarded header is discarded, and **every** client of the laboratory collapses into the single partition keyed on the proxy's address — a 10/min credential budget for the whole site. ADR-0002 line 41-42 states the loopback-only trust and the obligation to add the proxy explicitly, but neither the rate-limit consequence nor a deployment check for it is recorded anywhere. Acceptance criteria: (a) `KnownProxies`/`KnownNetworks` are configuration-driven and `ConfigGuard`-validated; (b) startup refuses, or logs a named warning, when `ForwardedHeaders` is enabled with no trusted proxy configured outside Development; (c) an integration case pins both arms of this table. Also assert `RateLimiting.ClientKey`'s `?? "unknown"` fallback (`RateLimiting.cs:98`) is unreachable in this environment — every request must produce a real address, since a null address would put all such clients into one shared 10/min bucket. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-080 — The fixed window rolls after 60 seconds and the budget is restored in full  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-071 · RSK-AUTH-013 |
| **Level / Type / Technique** | API · Functional (recovery) · Loop — the limiter's window iteration, sampled at the boundary and at boundary + 1 |
| **Priority / Severity / Automation** | High · Moderate · Yes (functional, but slow — the case takes >2 minutes of wall clock) |
| **Role / Permission / Tenant** | Anonymous · n/a · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; `Window = TimeSpan.FromMinutes(1)`, `QueueLimit = 0`, `AutoReplenishment` at its default (`RateLimiting.cs:51,71-77`) |
| **Preconditions** | Fresh `auth` window. A monotonic clock source for timing (`Stopwatch`, not wall-clock strings). |
| **Test Data** | `POST /api/auth/login` with `{"tenantIdentifier":"demo-lab","email":"burst3@nowhere.test","password":"Wrong-Burst-3!"}` repeated. |
| **Steps** | 1. Start the stopwatch and issue 10 requests as fast as possible; assert all `401`; record the elapsed time `t10`. 2. Issue request 11; assert `429`; record `t11`. 3. At `t11 + 30 s`, issue one request; record the status. 4. At `t11 + 59 s`, issue one request; record the status. 5. At `t11 + 61 s`, issue one request; assert `401` — the budget is available again. 6. Immediately issue 9 more; assert all `401`. 7. Issue one more; assert `429` — the full budget of 10 was restored, not a partial refill. |
| **Expected UI** | n/a — asserted at the API layer. |
| **Expected API** | Steps 1 → ten `401`. Step 2 → `429` with `Retry-After: 60`. Step 3 → `429`. Step 4 → `429` (the window began at the first request, `t0`, not at `t11`, so it may already have rolled if `t10` was slow — record the observed status and the elapsed time rather than forcing the assertion). Step 5 → `401`. Steps 6–7 → nine `401` then one `429`, proving a fixed window resets the counter wholesale rather than sliding. |
| **Expected DB** | Twenty non-throttled requests each write one security-event row. |
| **Expected Audit** | Exactly **20** new `LOGIN_FAILED` rows above the watermark, `detail='no-such-user'`, `actor='burst3@nowhere.test'`. |
| **Expected Notification** | n/a — no policy subscribes. |
| **Cleanup** | Wait ≥ 60 s. |
| **Evidence** | The timestamped status log for all 23 requests · the elapsed times `t10` and `t11` · the security-event count |
| **Notes** | The window anchors on the **first** acquisition in the partition, not on the rejection, which is why step 4's expectation is recorded rather than asserted: if `t10` took more than one second the window may already have rolled by `t11 + 59 s`. Steps 6–7 are the discriminating assertions — under a sliding window the budget would refill gradually and step 6 would 429 partway through. `Retry-After` is a fixed constant `60` regardless of how much of the window remains (`RateLimiting.cs:58-59`), so it over-states the wait for a client that hits the limit late in the window; note it, do not assert against a computed remainder. |
| **Result / Defect** | Not Run · — |

#### TC-AUTH-API-081 — A throttled credential attempt reaches neither the security ledger nor the account row  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-016, URS-071 · RSK-AUTH-013, RSK-AUTH-014 |
| **Level / Type / Technique** | API · Security (observability) · Statement/Branch — proving the request terminates in `UseRateLimiter` (`Program.cs:265`) and never enters `TenantResolutionMiddleware`, `ActiveSessionMiddleware` or `LoginHandler` |
| **Priority / Severity / Automation** | High · Moderate · Yes (integration, needs a real PostgreSQL) |
| **Role / Permission / Tenant** | Anonymous · n/a · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Fresh `auth` window. A **real** target account `pwthrottle@demo-lab.local`, active, `failed_login_attempts = 0`, `locked_until_utc IS NULL`. Record the watermark `max(occurred_at_utc)` in `audit.security_event`. |
| **Test Data** | Twenty sequential `POST /api/auth/login` with `{"tenantIdentifier":"demo-lab","email":"pwthrottle@demo-lab.local","password":"Wrong-Throttle-1!"}` — a real account with the wrong password, so the first attempts do exercise `RegisterFailedLogin`. |
| **Steps** | 1. Issue all twenty requests sequentially within one minute and record each status. 2. Tally how many returned `401` and how many `429`. 3. `SELECT set_config('app.bypass_rls','on',false); SELECT count(*) FROM audit.security_event WHERE occurred_at_utc > <watermark> AND actor='pwthrottle@demo-lab.local';`. 4. `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='pwthrottle@demo-lab.local';`. 5. Assert the step-3 count equals the number of non-`429` responses exactly. 6. Assert `locked_until_utc` is set and `failed_login_attempts = 0` — the account locked on its fifth *processed* attempt, and `RegisterFailedLogin` zeroes the counter at lockout (`UserAccount.cs:212-217`). 7. Confirm attempts 6–10 returned `401` with `code` = `AUTH-004` (locked), not `AUTH-001`. |
| **Expected UI** | The sign-in page shows `Account is temporarily locked. Try again later.` from attempt 6 onward, then falls back to `Sign-in failed.` once the throttle engages — the user cannot distinguish a lockout from a throttle. |
| **Expected API** | Attempts 1–5 → `401` `code` = `AUTH-001`; attempts 6–10 → `401` `code` = `AUTH-004` (`Login.cs:79`); attempts 11–20 → `429` with `Retry-After: 60` and an empty body. |
| **Expected DB** | `locked_until_utc` ≈ attempt 5's instant + 30 minutes; `failed_login_attempts = 0`. Attempts 11–20 execute no query — assert via Npgsql OpenTelemetry spans (or `pg_stat_statements` deltas) that the tenant and user `SELECT`s occur exactly ten times, not twenty. |
| **Expected Audit** | Exactly **10** new `audit.security_event` rows: five `LOGIN_FAILED` with `detail='bad-password'` and five `LOGIN_FAILED` with `detail='locked-out'` (`Login.cs:79,86`). Zero rows for the ten throttled attempts. Also assert exactly one `audit.field_change` row for `property='LockedUntilUtc'` and none for the throttled attempts. |
| **Expected Notification** | n/a — no policy subscribes to `UserLockedOut`; the `UserLockedOut` domain event (`UserAccount.cs:216`) is raised but has no handler in `src/`. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts = 0, locked_until_utc = NULL WHERE email='pwthrottle@demo-lab.local';` then wait ≥ 60 s for the window. |
| **Evidence** | The twenty-row status log · the security-event count and its `detail` breakdown · the `user_account` read · the OpenTelemetry span or `pg_stat_statements` delta |
| **Notes** | The exact accounting in step 5 is what makes this a control rather than a smoke test: it proves that throttling is silent, so a sustained credential attack against a single account produces a *bounded* number of security-event rows no matter how many attempts are made. Combined with `TC-AUTH-API-071` (change-password logs nothing at all) and GAP-AUTH-005 (no source address on any row), the evidence a Part-11 §11.300(d) reviewer has for "attempts at unauthorised use" is materially thinner than the row count suggests. Recorded here; the aggregate finding belongs to the module's compliance assessment, not to a new gap. |
| **Result / Defect** | Not Run · — |

---

## Batch coverage note

**Covered.** 38 cases, all `Not Run`. `TC-AUTH-BVA-001` … `-016` cover `PasswordRules.StrongPassword()` completely against the read source (`src/NT.QAMS.Application/IdentityAccess/PasswordRules.cs`, full file): the length boundaries 11/12/13 and 200/201, each of the four character classes omitted in turn with all others held true, the terminal `else` "symbol" branch probed with a space and a tab, whitespace-only and empty-adjacent input, both Unicode partitions (caseless Arabic rejected, cased Greek accepted), the trimmed blocklist with padding plus its exact-match-only boundary, a three-simultaneous-failure multiple-condition case that pins the `Continue` cascade, and proof that the identical rule object is applied at all three call sites (`Login.cs:170`, `UserManagement.cs:44`, `UserManagement.cs:102`). `TC-AUTH-API-060` … `-065` cover the anonymous workspace lookup: the name-only payload asserted by key enumeration, the three miss classes (unknown, malformed, suspended) asserted byte-identical modulo `traceId`, the additional `Terminated`/`Provisioning` statuses, the slug-length boundaries 1/2/50/51, and slug normalisation. `TC-AUTH-API-066` … `-074` cover the password lifecycle: the full change ceremony with its `saas.password_history`, `audit.field_change` (including `«redacted»` per URS-019) and `audit.security_event` side effects; the reuse ban at the current hash and at both sides of the `HistoryDepth` boundary; the prune loop across eight iterations; the unthrottled wrong-current-password oracle; and administrative reset with its three side effects. `TC-AUTH-API-075` … `-081` cover the `auth` partition: the 10/11 boundary against the production default, the `429` wire contract, poisoning across the shared controller budget, the separate `refresh` partition, forwarded-address repartitioning in both directions, the fixed-window roll, and the silence of throttled attempts in the ledger.

**Could not cover, and why.**
1. **`RateLimit:*` non-positive-permit startup refusal** (`RateLimiting.cs:16-20`). Exercising it requires restarting the API with a deliberately invalid configuration, which is a `ConfigGuard`/startup concern already owned by `ConfigGuardTests` and OQ-DEP-04. Not duplicated here.
2. **The global 300/min partition.** Exhausting it needs 300 requests inside one minute against a non-`AuthController` route and would collide with every other case running against the same dev API. Deferred to the module's `TC-AUTH-PERF-*` reservation in batch F.
3. **The `esignature` per-actor partition** (`RateLimiting.cs:87-93,101-102`). It is reachable only through `POST /api/documents/{id}/publish`, which drags the document module's preconditions (uploaded file, approved version, non-author approver) into an AUTH case — GAP-AUTH-003's testing limitation. `TC-AUTH-API-076` step 8 touches it only to prove the empty-body defect is in the shared `OnRejected`, not to characterise the partition. The full characterisation belongs to the `TC-AUTH-SEC-*` reservation.
4. **Tenant suspension through the API.** `TC-AUTH-API-063` sets `saas.tenant.status` with a direct `UPDATE` because `TenantsController` exposes only `POST` (provision) and `GET` (list) — there is no suspend, resume or terminate endpoint. That absence belongs to module `TENANT`; no AUTH gap is minted for it, but the transcript must state that the precondition was applied outside the application, so the case proves nothing about how a tenant reaches the `Suspended` state.
5. **The `ClientKey` `?? "unknown"` fallback** (`RateLimiting.cs:98`). No way was found to make Kestrel report a null `RemoteIpAddress` over TCP; `TC-AUTH-API-079` asserts only that the branch is not taken. Reaching it would need a Unix-socket or in-memory transport, which the dev environment does not run.
6. **NFC/NFD normalisation and combining marks** in password input. `TC-AUTH-BVA-012`/`-013` cover the two script partitions but not the "same visual password, different byte sequence" failure mode; that is charter EXPL-6's territory and needs an exploratory session, not a scripted case.
7. **Anything requiring a second concurrent client address.** `TC-AUTH-API-077` and `-079` assume single-source execution; proving that two genuinely distinct addresses get independent partitions needs a second host, which the single-machine dev environment does not provide.

**New gaps found in this slice.** Eight, numbered from `GAP-AUTH-901` to avoid colliding with the front matter's `GAP-AUTH-001`…`-016`. Each is source-cited above in the case that discovers it; acceptance criteria are stated in the `Notes` row of that case.

| Gap | Summary | Discovered by | Severity |
|---|---|---|---|
| `GAP-AUTH-901` | No URS covers password **aging** (`AUTH-101`, `PasswordPolicyOptions.MaxAgeDays = 90`) or the **reuse ban** (`AUTH-102`, `HistoryDepth = 5`). URS-002 covers strength only. Both behaviours are implemented, configurable and user-visible, and both are untraceable in the RTM. A reuse rejection additionally writes **no** security event (`Login.cs:215` throws before `:235`). | `TC-AUTH-API-067` | Moderate |
| `GAP-AUTH-902` | **Administrative reset bypasses the reuse ban.** `ResetUserPasswordHandler` (`UserManagement.cs:145-149`) calls `UserAccount.ResetPassword` and nothing else: the outgoing hash is never appended to `saas.password_history`, so a user can be reset and then immediately change straight back to the password that was in force minutes earlier. The reset also leaves `password_changed_at_utc` **null** (`UserAccount.cs:136,144`), permanently disabling the 90-day expiry for that account, and writes **no** `audit.security_event` row. | `TC-AUTH-API-072`, `-073` | **Major** |
| `GAP-AUTH-903` | **The history prune is off by one.** `Login.cs:219` stages the new `PasswordHistoryEntry` in the change tracker, but the prune at `:228-232` queries the database, where the staged row is not yet visible; `Skip(HistoryDepth)` therefore under-deletes by exactly one and the table stabilises at **6** rows for `HistoryDepth = 5`. The extra row is retained credential material that the ban never consults, because the check uses `.Take(5)` on the same table. | `TC-AUTH-API-069`, `-070` | Moderate |
| `GAP-AUTH-904` | **The "symbol" class is the complement of the other three.** `HasComplexity`'s terminal `else` (`PasswordRules.cs:69`) counts any character that is not upper, lower or digit — including U+0020 SPACE, U+0009 TAB and every caseless letter — as a symbol, so `Qms Batch123` satisfies URS-002's four-class rule with a space as its only "symbol". | `TC-AUTH-BVA-010` | Moderate |
| `GAP-AUTH-905` | **The rate-limit partition key is fragile in both directions.** `Program.cs:147-148` enables `XForwardedFor` but leaves `KnownProxies`/`KnownNetworks` at the framework default (IPv6 loopback), so: from a trusted peer, varying one header mints unlimited fresh partitions and defeats URS-071; and in the ADR-0002 topology with a non-loopback proxy the header is discarded and the whole laboratory collapses into one 10/min partition. Separately, the shared `auth` partition (`AuthController.cs:18`) means a credential burst also throttles the anonymous workspace lookup, `me/privileges`, `me/language` and **logout** for every client on that address. | `TC-AUTH-API-077`, `-079` | **Major** |
| `GAP-AUTH-906` | **A 429 is the one error response with no problem+json body.** `RateLimiting.Configure`'s `OnRejected` (`RateLimiting.cs:55-61`) sets only the status and `Retry-After`, bypassing `ProblemResponse.WriteAsync`; there is no `code`, `title`, `traceId` or `correlationId`. URS-075 requires the contrary on every error path, and `ProblemContractTests` contains no `429` case, so the merge gate does not catch it. | `TC-AUTH-API-076` | Moderate |
| `GAP-AUTH-907` | **The SPA discards `problem.errors` on every path inspected.** `login.component.ts:456,485` and `users.facade.ts:64-69` render `err.error?.title` only, so a user whose password is rejected for any of the five reasons sees exactly `Validation failed.` and is given no way to comply. The server payload is informative; the interface throws it away. | `TC-AUTH-API-074` | Moderate |
| `GAP-AUTH-908` | **The new-password hint misstates the policy.** `core/i18n.service.ts:1015` says "At least 10 characters" in `en`, `ar` and `fr`, while `PasswordRules.MinLength = 12` (`PasswordRules.cs:17`). A user following the on-screen instruction is guaranteed to be rejected — and, per `GAP-AUTH-907`, will not be told why. | `TC-AUTH-API-074` | Minor |

**One documentation defect to reconcile, not silently fixed.** The front matter's §1.5 error table maps `AUTH-102` to HTTP **401** via the `AUTH-` prefix rule (`DomainExceptionHandler.cs:54-59`), which is what `TC-AUTH-API-067` and `-068` assert. That is almost certainly not the intended status for a policy rejection on an otherwise correctly-authenticated request — a password-reuse refusal is a business-rule failure (422), and returning 401 tells the SPA the session is broken. The status is asserted as-built in both cases and the design question is raised here rather than resolved; it is a candidate for the module owner to fold into `GAP-AUTH-901`'s acceptance criteria.
