# AUTH — Detailed Test Cases, Batch A

This batch authors **33 detailed cases** — `TC-AUTH-API-001` … `TC-AUTH-API-025` and `TC-AUTH-EP-001` … `TC-AUTH-EP-008` — over one slice of module `AUTH`: the `POST /api/auth/login` success and failure paths; the 5-attempt / 30-minute lockout with boundary analysis at attempts 4 / 5 / 6 and at lock expiry T−1s / T / T+1s; the failed-attempt counter reset on success; account creation, activation, deactivation and the **absent** deletion path; login against a deactivated, locked, unknown or cross-tenant account; tenant-scoped login (unknown slug, malformed slug, suspended tenant); and the `audit.security_event` / `audit.field_change` / `audit.audit_trail` rows those flows write. It deliberately leaves to sibling batches: every non-login AUTH endpoint and its problem+json shape, and the four decision tables as row-per-rule cases (batch B); the refresh-session and access-review state machines and the handler↔PostgreSQL integration set (batch C); rate-limit partitions, cookie hardening, reuse detection, anti-enumeration timing and the RLS assertions (batch D); the credential data-flow, MC/DC of the compound guards and the observability set (batch E); browser E2E, UAT, a11y and performance (batch F). MFA, TOTP, e-signature, password history, refresh rotation and access reviews are **out of this batch entirely**. Requirement ids trace to `docs/validation/01-User-Requirements-Specification.md`. `docs/validation/02-Functional-Risk-Assessment.md` §2 carries **area names, not numeric risk ids**, so per conventions §5 this batch **mints** `RSK-AUTH-001` … `RSK-AUTH-006` and maps each to its FRA area: `RSK-AUTH-001` Authentication hardening (FRA "Authentication hardening", URS-001/002/003/004/007) · `RSK-AUTH-002` Lockout defeat / credential guessing (same area) · `RSK-AUTH-003` Tenant isolation at the credential boundary (FRA "Tenant isolation", URS-008) · `RSK-AUTH-004` Security-event logging deficiency (FRA "Security event logging", URS-016/019) · `RSK-AUTH-005` Account provisioning and de-provisioning (URS-009, no FRA area) · `RSK-AUTH-006` Session revocation on status change (FRA "Access control / RBAC", URS-006).

Two conventions apply throughout and are not repeated in every case. First, the canonical block in `00-GROUND-TRUTH-AND-CONVENTIONS.md` §8 states a locked account reads `failed_attempts = 5`; that is **impossible** and the column is `failed_login_attempts` — `RegisterFailedLogin` zeroes the counter in the same operation that sets the lock (`src/NT.QAMS.Domain/IdentityAccess/UserAccount.cs:211-217`), so every locked-account precondition and assertion below reads `failed_login_attempts = 0 AND locked_until_utc > now()`, per the front matter's correction §0.1. Second, `audit.security_event` and `audit.field_change` both carry `POLICY tenant_isolation` whose `USING` clause is `tenant_id = GUC OR bypass` (measured on `ntqams`, 2026-08-01) — a **null-tenant** row therefore satisfies neither disjunct and is invisible to a tenant-scoped read; every SQL assertion on a null-tenant audit row below is prefixed with `SELECT set_config('app.bypass_rls','on',false);`.

---

#### TC-AUTH-API-001 — Successful tenant login issues a full session, resets the counter and writes LOGIN_SUCCESS  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-001, URS-016 · RSK-AUTH-001 |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — the primary sign-in path, decision-table §4.1 rule R12 |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional, `WebApi.FunctionalTests`) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — `[AllowAnonymous]` on `AuthController.cs:30` · `demo-lab` |
| **Environment** | API `:5080` `ASPNETCORE_ENVIRONMENT=Development` (started via `scripts/dev-up.ps1`) + live PostgreSQL 17 dev DB `ntqams`, role `qams_app` |
| **Preconditions** | `qams.user_account` row for `admin@demo-lab.local` has `is_active = true`, `failed_login_attempts = 0`, `locked_until_utc IS NULL`, `mfa_enabled = false`, `password_changed_at_utc IS NULL` (the seeded state, measured 2026-08-01); `saas.tenant` row `identifier='demo-lab'` has `status='Active'`, `require_mfa_privileged = false` |
| **Test Data** | Body `{"tenantIdentifier":"demo-lab","email":"admin@demo-lab.local","password":"Demo-Admin-Pass-2!","mfaCode":null}` |
| **Steps** | 1. `POST /api/auth/login` with the body above and `Content-Type: application/json`. 2. Record status, response body and every `Set-Cookie` header. 3. `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='admin@demo-lab.local';`. 4. `SELECT count(*) FROM qams.refresh_session s JOIN qams.user_account u ON u.id=s.user_id WHERE u.email='admin@demo-lab.local' AND s.revoked_at_utc IS NULL;`. 5. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type, tenant_id, actor, detail, ip_address FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 1;`. |
| **Expected UI** | `/t/demo-lab` sign-in form submits, spinner clears, the browser lands on the dashboard route; the access token is held in SPA memory only (ADR-0009) and appears in no `localStorage`/`sessionStorage` key. |
| **Expected API** | `200 application/json`. Body `AuthResponse` (`Contracts/IdentityAccess/AuthContracts.cs:9-11`): `accessToken` a non-empty three-segment JWT, `expiresAtUtc` ≈ now + 15 min (`Jwt:ExpiryMinutes` default 15, `SecurityAdapters.cs:36`), `role":"TenantAdmin"`, `displayName` non-empty, `tenantId` the `demo-lab` uuid, `mfaRequired:false`, `mfaEnrollmentRequired:false`. Exactly one `Set-Cookie: qams_rt=<32-hex>.<base64url>; expires=…; path=/api/auth; secure; httponly; samesite=strict` (`AuthController.cs:92-100`). |
| **Expected DB** | `failed_login_attempts = 0`; `locked_until_utc IS NULL`. Exactly **one new** `qams.refresh_session` row: `revoked_at_utc IS NULL`, `replaced_by_id IS NULL`, `token_hash` matching `^[0-9A-F]{64}$` (CHECK `ck_refresh_session_token_hash_sha256`), `expires_at_utc` ≈ now + 14 days (`Auth:RefreshTokenDays` default 14). |
| **Expected Audit** | One `audit.security_event` row: `event_type='LOGIN_SUCCESS'`, `tenant_id` = the `demo-lab` uuid, `actor='admin@demo-lab.local'`, `detail IS NULL`, `ip_address IS NULL` (never written — `ComplianceLedgerServices.cs:73-81`, GAP-AUTH-005). No `audit.field_change` row is expected for `RegisterSuccessfulLogin` when the counter and lock were **already** 0/NULL: the interceptor skips unmodified properties (`FieldChangeInterceptor.cs:90`). |
| **Expected Notification** | n/a — no `NotificationPolicy` subscribes to a successful login; grep of `src/` finds no consumer. |
| **Cleanup** | `POST /api/auth/logout` with the returned `qams_rt` cookie, then `DELETE FROM qams.refresh_session WHERE user_id = (SELECT id FROM qams.user_account WHERE email='admin@demo-lab.local');` |
| **Evidence** | HTTP request/response capture including headers · decoded JWT claim set · 4 SQL result sets |
| **Result / Defect** | Not Run · — |
| **Notes** | Decode the JWT and confirm the claim set is exactly `sub, email, name, role, scope="full", tenant_id` (`SecurityAdapters.cs:83-95`); `scope` must be `full`, not `mfa_enrollment`, because `require_mfa_privileged` is false. |

---

#### TC-AUTH-API-002 — Platform-admin login with no tenant identifier selects the null-tenant partition  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-001 · RSK-AUTH-003 |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — `tenantIdentifier` absent ⇒ `tenantId = null` ⇒ `u.TenantId == null` (`Login.cs:41,70`) |
| **Priority / Severity / Automation** | High · Critical · Yes (functional) |
| **Role / Permission / Tenant** | PlatformAdmin · n/a — anonymous endpoint · none (platform surface) |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | `qams.user_account` row `platform-admin@localhost` exists with `tenant_id IS NULL`, `role='PlatformAdmin'`, `is_active=true`, `failed_login_attempts=0`, `locked_until_utc IS NULL` (measured 2026-08-01); config `Security:RequireMfaForPrivilegedRoles` is `false` (default, `PasswordPolicyOptions.cs:17`) |
| **Test Data** | Body `{"tenantIdentifier":null,"email":"platform-admin@localhost","password":"Dev-Only-Platform-Pass-1!","mfaCode":null}` |
| **Steps** | 1. `POST /api/auth/login` with the body above. 2. Decode the returned `accessToken`. 3. `POST /api/auth/login` a second time with `"tenantIdentifier":"demo-lab"` and the same platform credentials. 4. Compare status codes and bodies. |
| **Expected UI** | The platform sign-in surface (no `/t/{slug}` prefix) reaches the platform console; the tenant switcher is absent. |
| **Expected API** | Step 1: `200`, `role":"PlatformAdmin"`, `tenantId":null`, `mfaRequired:false`, `mfaEnrollmentRequired:false`. Step 3: `401 application/problem+json`, `code":"AUTH-001"`, `title":"Invalid credentials."` — the `demo-lab` predicate `u.TenantId == tenantId` excludes the null-tenant row (`Login.cs:70`). |
| **Expected DB** | After step 1: one new `qams.refresh_session` row for the platform admin. After step 3: `failed_login_attempts` on `platform-admin@localhost` **still 0** — step 3 never located the row, so `RegisterFailedLogin` is never reached (`Login.cs:72-75` returns first). |
| **Expected Audit** | Step 1: `audit.security_event` `event_type='LOGIN_SUCCESS'`, **`tenant_id IS NULL`** (`Login.cs:139` passes `tenantId`, which is null here) — readable only after `set_config('app.bypass_rls','on',false)`. Step 3: `event_type='LOGIN_FAILED'`, `detail='no-such-user'`, `tenant_id` = the `demo-lab` uuid (the slug resolved before the user lookup failed). |
| **Expected Notification** | n/a — no notification is defined for any login outcome. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = (SELECT id FROM qams.user_account WHERE email='platform-admin@localhost');` |
| **Evidence** | Both HTTP captures · decoded JWT (assert **no** `tenant_id` claim, `SecurityAdapters.cs:92-94`) · 2 SQL result sets |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 3 is the load-bearing half: it proves the null-tenant partition is closed to a tenant-scoped request, which is the credential-boundary half of URS-008. |

---

#### TC-AUTH-API-003 — First wrong password increments the counter to 1 and returns AUTH-001  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003, URS-016 · RSK-AUTH-002 |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — §4.1 rule R6, the `!hasher.Verify` branch at `Login.cs:82-87` |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | Fixture account created by TC-AUTH-API-016 exists: `bva.analyst@demo-lab.local`, role `Analyst`, `is_active=true`, `failed_login_attempts=0`, `locked_until_utc IS NULL`, `mfa_enabled=false` |
| **Test Data** | Body `{"tenantIdentifier":"demo-lab","email":"bva.analyst@demo-lab.local","password":"Wrong-Pass-0000!","mfaCode":null}` |
| **Steps** | 1. `SELECT failed_login_attempts FROM qams.user_account WHERE email='bva.analyst@demo-lab.local';` — confirm 0. 2. `POST /api/auth/login` with the body above. 3. Re-run the SELECT in step 1. 4. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type, detail, actor, tenant_id FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 1;`. |
| **Expected UI** | Sign-in form shows "Invalid credentials."; email and password inputs remain enabled and the password field is cleared; no attempt counter is disclosed to the user. |
| **Expected API** | `401 application/problem+json` with body `{"title":"Invalid credentials.","status":401,"code":"AUTH-001","traceId":"<32-hex>"}` — the `code` extension is stamped by `DomainExceptionHandler.cs:54-59`, `traceId` by `ProblemResponse.cs:24-25`. No `Set-Cookie` header. |
| **Expected DB** | `failed_login_attempts = 1`; `locked_until_utc IS NULL`. The increment **persists despite the 401** — there is no transaction behaviour in the MediatR pipeline (`Application/DependencyInjection.cs:20-24` registers Tracing, Logging, Authorization, Idempotency, Validation only), so the `SaveChangesAsync` at `Login.cs:85` commits before the throw at `:86`. |
| **Expected Audit** | One `audit.security_event` row: `event_type='LOGIN_FAILED'`, `detail='bad-password'`, `actor='bva.analyst@demo-lab.local'`, `tenant_id` = the `demo-lab` uuid. One `audit.field_change` row: `entity_type='UserAccount'`, `action='Modified'`, `property='FailedLoginAttempts'`, `old_value='0'`, `new_value='1'`, `actor='system'` (unauthenticated caller, `FieldChangeInterceptor.cs:114`), `tenant_id` = the `demo-lab` uuid. |
| **Expected Notification** | n/a — no notification is defined for a failed login. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='bva.analyst@demo-lab.local';` |
| **Evidence** | HTTP response capture · 2 SQL result sets · both audit rows |
| **Result / Defect** | Not Run · — |
| **Notes** | The `AUTH-001` body is byte-identical to the unknown-user body of TC-AUTH-API-011 — that indistinguishability is the intended anti-enumeration property and is quantified by charter EXPL-1 in batch F, not asserted here. |

---

#### TC-AUTH-API-004 — BVA below the threshold: the 4th consecutive failure leaves the account unlocked  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003 · RSK-AUTH-002 |
| **Level / Type / Technique** | API · Functional (negative) · BVA — one below `UserAccount.MaxFailedAttempts = 5` (`UserAccount.cs:29`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | `bva.analyst@demo-lab.local` at `failed_login_attempts=0`, `locked_until_utc IS NULL`, `is_active=true`. The 10/min `AuthPolicy` budget (`RateLimiting.cs:24`) is unspent for this client IP — run this case, TC-AUTH-API-005 and TC-AUTH-API-006 as **one** 6-request sequence inside a single minute, or wait 60 s between groups. |
| **Test Data** | Four identical bodies `{"tenantIdentifier":"demo-lab","email":"bva.analyst@demo-lab.local","password":"Wrong-Pass-0000!","mfaCode":null}` |
| **Steps** | 1. `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='bva.analyst@demo-lab.local';`. 2. `POST /api/auth/login` (attempt 1). 3. `POST /api/auth/login` (attempt 2). 4. `POST /api/auth/login` (attempt 3). 5. `POST /api/auth/login` (attempt 4). 6. `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='bva.analyst@demo-lab.local';`. |
| **Expected UI** | Each of the four attempts shows the same "Invalid credentials." message; no lockout message, no countdown, no disabled control. |
| **Expected API** | All four responses `401 application/problem+json`, `code":"AUTH-001"`, `title":"Invalid credentials."` — **not** `AUTH-004`. No `Set-Cookie`. |
| **Expected DB** | After step 5: `failed_login_attempts = 4` **exactly**; `locked_until_utc IS NULL`. The threshold test is `FailedLoginAttempts >= 5` after the pre-increment (`UserAccount.cs:211-212`), so 4 does not fire it. |
| **Expected Audit** | Exactly four `audit.security_event` rows, all `event_type='LOGIN_FAILED'`, `detail='bad-password'`, `tenant_id` = the `demo-lab` uuid. Four `audit.field_change` rows, `property='FailedLoginAttempts'`, `old_value`/`new_value` pairs `0→1`, `1→2`, `2→3`, `3→4`. **No** `LockedUntilUtc` field-change row and **no** `qams.outbox_event` row of type `NT.QAMS.Domain.IdentityAccess.UserLockedOut, NT.QAMS.Domain`. |
| **Expected Notification** | n/a — no notification is defined for a failed login or a lockout (grep of `src/` finds no subscriber to `UserLockedOut`). |
| **Cleanup** | None — TC-AUTH-API-005 consumes this exact end state (`failed_login_attempts = 4`) as its precondition. |
| **Evidence** | Four HTTP response captures · final SQL result set · the four field-change rows · a `SELECT count(*) FROM qams.outbox_event WHERE event_type LIKE '%UserLockedOut%'` before/after delta of 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | Six requests to `/api/auth/*` fit inside the 10/min budget; a seventh (TC-AUTH-API-010's success) does not if all seven are issued in one window — split at the minute boundary. Rate-limit behaviour itself is batch D. |

---

#### TC-AUTH-API-005 — BVA at the threshold: the 5th consecutive failure locks for 30 minutes and zeroes the counter  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003, URS-016 · RSK-AUTH-002 |
| **Level / Type / Technique** | API · Functional (negative) · BVA — exactly at `MaxFailedAttempts = 5` / `LockoutMinutes = 30` (`UserAccount.cs:29-30`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | End state of TC-AUTH-API-004: `bva.analyst@demo-lab.local` at `failed_login_attempts = 4`, `locked_until_utc IS NULL`, `is_active = true` |
| **Test Data** | Body `{"tenantIdentifier":"demo-lab","email":"bva.analyst@demo-lab.local","password":"Wrong-Pass-0000!","mfaCode":null}`; capture the wall-clock instant `T0` immediately before the request |
| **Steps** | 1. Record `T0 = now()` from `SELECT now();`. 2. `POST /api/auth/login` (attempt 5). 3. `SELECT failed_login_attempts, locked_until_utc, locked_until_utc - $T0 AS delta FROM qams.user_account WHERE email='bva.analyst@demo-lab.local';`. 4. `SELECT event_type, payload, processed_at_utc FROM qams.outbox_event WHERE event_type='NT.QAMS.Domain.IdentityAccess.UserLockedOut, NT.QAMS.Domain' ORDER BY occurred_at_utc DESC LIMIT 1;`. 5. Wait for the next `OutboxProcessor` pass (hourly loop, first pass on startup — `OutboxProcessor.cs:53,59`; force it with `scripts/dev-rebuild.ps1` if needed) and re-run step 4, then `SELECT set_config('app.bypass_rls','on',false); SELECT sequence, event_type, prev_hash, entry_hash FROM audit.audit_trail WHERE event_type LIKE '%UserLockedOut%' ORDER BY sequence DESC LIMIT 1;`. |
| **Expected UI** | The 5th attempt shows "Invalid credentials." — **not** a lockout message. The account is now locked, but the user is not told so until the *next* attempt (the `AUTH-004` branch runs on entry, `Login.cs:77-80`). |
| **Expected API** | `401 application/problem+json`, `code":"AUTH-001"`, `title":"Invalid credentials."`. The 5th failure is still an `AUTH-001` — the lock is a **side effect**, not the response. |
| **Expected DB** | `failed_login_attempts = 0` (zeroed at `UserAccount.cs:215`); `locked_until_utc` non-null and `locked_until_utc - T0` between `29 min 55 s` and `30 min 05 s` (`now.AddMinutes(30)`, `UserAccount.cs:214`, tolerance for request latency). One `qams.outbox_event` row `event_type='NT.QAMS.Domain.IdentityAccess.UserLockedOut, NT.QAMS.Domain'`, `tenant_id` = the `demo-lab` uuid, `payload` JSON containing `UserId`, `Email` and `LockedUntilUtc`, `processed_at_utc IS NULL` initially. |
| **Expected Audit** | One `audit.security_event` row `event_type='LOGIN_FAILED'`, `detail='bad-password'`. Two `audit.field_change` rows from the same `SaveChanges`: `property='FailedLoginAttempts'` `old_value='4'` `new_value='0'`, and `property='LockedUntilUtc'` `old_value IS NULL` `new_value` = the lock instant. After the outbox pass, one `audit.audit_trail` row, `tenant_id` = the `demo-lab` uuid, `sequence` = previous tip + 1, `prev_hash` = the previous row's `entry_hash`, `entry_hash` 64 lower-case hex (`LedgerHash.Compute`, `ComplianceLedgerServices.cs:16-20`). |
| **Expected Notification** | n/a — `UserLockedOut` has **zero** MediatR subscribers (grep of `src/` returns only its declaration at `UserAccount.cs:259` and its raise at `:216`); the outbox row is published to an empty handler set and marked processed. |
| **Cleanup** | None — TC-AUTH-API-006 consumes the locked end state. Final teardown is in TC-AUTH-API-009. |
| **Evidence** | HTTP response capture · SQL result with the computed 30-minute delta · outbox row · both field-change rows · the `audit_trail` row with its hash pair |
| **Result / Defect** | Not Run · — |
| **Notes** | The counter reading **0 while locked** is the fact the canonical block in conventions §8 gets wrong; assert 0, never 5. Do **not** assert a `LOGIN_LOCKED` security-event type — no such type exists; the 12 emitted types are enumerated in the front matter §1.6. |

---

#### TC-AUTH-API-006 — BVA above the threshold: the 6th attempt with the CORRECT password is refused with AUTH-004 and does not extend the lock  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003 · RSK-AUTH-002 |
| **Level / Type / Technique** | API · Functional (negative) · BVA — one above the threshold, and the `IsLockedOut` short-circuit at `Login.cs:77-80` |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | End state of TC-AUTH-API-005: `failed_login_attempts = 0`, `locked_until_utc > now()`, `is_active = true`. Record the exact `locked_until_utc` value as `L0`. |
| **Test Data** | Attempt 6a — **correct** password: `{"tenantIdentifier":"demo-lab","email":"bva.analyst@demo-lab.local","password":"Bva-Analyst-Pass-7!","mfaCode":null}`. Attempt 6b — wrong password: same body with `"password":"Wrong-Pass-0000!"`. |
| **Steps** | 1. `SELECT locked_until_utc FROM qams.user_account WHERE email='bva.analyst@demo-lab.local';` — record `L0`. 2. `POST /api/auth/login` with the **correct** password (6a). 3. Re-run the SELECT. 4. `POST /api/auth/login` with the wrong password (6b). 5. Re-run the SELECT. 6. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type, detail FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 2;`. |
| **Expected UI** | Both attempts show the locked message "Account is temporarily locked. Try again later."; inputs remain enabled; no countdown timer is rendered (the API discloses no remaining duration). |
| **Expected API** | Both 6a and 6b: `401 application/problem+json`, `code":"AUTH-004"`, `title":"Account is temporarily locked. Try again later."`. No `Set-Cookie`. A **correct** password is refused identically to a wrong one — the lock check precedes `hasher.Verify` (`Login.cs:77` before `:82`). |
| **Expected DB** | After both attempts: `failed_login_attempts` **still 0** and `locked_until_utc` **exactly `L0`, unchanged** — the `AUTH-004` branch returns before `RegisterFailedLogin`, so there is **no lock extension and no counter movement** while locked. |
| **Expected Audit** | Two `audit.security_event` rows, both `event_type='LOGIN_FAILED'`, both `detail='locked-out'`, `tenant_id` = the `demo-lab` uuid. **Zero** new `audit.field_change` rows — nothing on the aggregate was modified, so the interceptor emits nothing (`FieldChangeInterceptor.cs:66-77`). |
| **Expected Notification** | n/a — no notification is defined for a rejected login against a locked account. |
| **Cleanup** | None — TC-AUTH-API-007 consumes the locked state. |
| **Evidence** | Both HTTP captures · three SQL snapshots of `locked_until_utc` proving it is byte-identical · two security-event rows · a `count(*)` of `audit.field_change` before/after showing a delta of 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | `detail='locked-out'` proves to an attacker that the account **exists** — a deliberate oracle recorded in charter EXPL-1. Do not treat it as a defect in this case; assert the as-built code and leave the disclosure question to batch F. |

---

#### TC-AUTH-API-007 — Lock-expiry BVA at T−1s: the account is still locked  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003 · RSK-AUTH-002 |
| **Level / Type / Technique** | API · Functional (negative) · BVA — lower side of the `IsLockedOut` boundary `until > now` (`UserAccount.cs:163`) |
| **Priority / Severity / Automation** | Critical · Major · Yes (functional; deterministic only if `locked_until_utc` is set by SQL relative to `now()`) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams`. The API resolves time from `IClock.UtcNow` (system clock) — there is **no** injectable test clock over HTTP, so the boundary is controlled by writing `locked_until_utc`, not by moving time. |
| **Preconditions** | `bva.analyst@demo-lab.local` is active with a known-good password |
| **Test Data** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc = now() + interval '1 second' WHERE email='bva.analyst@demo-lab.local';` then request immediately with the **correct** password `Bva-Analyst-Pass-7!` |
| **Steps** | 1. Run the `UPDATE` above and note the returned `locked_until_utc` as `L`. 2. Within 900 ms, `POST /api/auth/login` with `{"tenantIdentifier":"demo-lab","email":"bva.analyst@demo-lab.local","password":"Bva-Analyst-Pass-7!","mfaCode":null}`. 3. Record the response and `SELECT now() < '<L>'::timestamptz AS was_before_boundary;` to prove the request landed before `L`. 4. `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='bva.analyst@demo-lab.local';`. |
| **Expected UI** | Sign-in form shows "Account is temporarily locked. Try again later." |
| **Expected API** | `401 application/problem+json`, `code":"AUTH-004"`. No `Set-Cookie`. |
| **Expected DB** | `failed_login_attempts = 0`; `locked_until_utc` unchanged at `L`; **no** new `qams.refresh_session` row for this user. |
| **Expected Audit** | One `audit.security_event` row `event_type='LOGIN_FAILED'`, `detail='locked-out'`, `tenant_id` = the `demo-lab` uuid. |
| **Expected Notification** | n/a — none defined for a locked-account rejection. |
| **Cleanup** | None — TC-AUTH-API-008 resets the boundary itself. |
| **Evidence** | HTTP capture with its wall-clock timestamp · the `was_before_boundary = true` SQL result · post-condition SQL |
| **Result / Defect** | Not Run · — |
| **Notes** | If step 3 returns `was_before_boundary = false` the case is **inconclusive, not failed** — re-run with `interval '3 seconds'`. Record the actual margin in the execution evidence. |

---

#### TC-AUTH-API-008 — Lock-expiry BVA exactly at T: `until > now` is strict, so login succeeds  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003 · RSK-AUTH-002 |
| **Level / Type / Technique** | API · Functional (positive) · BVA — the boundary value itself; `IsLockedOut` uses strict `>`, so `until == now` is **not** locked (`UserAccount.cs:163`) |
| **Priority / Severity / Automation** | High · Major · No — sub-millisecond equality against a `timestamptz` cannot be hit reliably over HTTP; automate the equivalent as a domain unit case in batch A's `TC-AUTH-UNIT-*` reservation |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | `bva.analyst@demo-lab.local` is active with a known-good password |
| **Test Data** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc = now() WHERE email='bva.analyst@demo-lab.local';` then request with the correct password |
| **Steps** | 1. Run the `UPDATE` above; record the stored `locked_until_utc` as `L`. 2. `POST /api/auth/login` with the correct password. 3. `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='bva.analyst@demo-lab.local';`. 4. Assert in the evidence that the request instant was `> L` (it necessarily is, by network latency), which is the *same side of the boundary* the strict comparison places `L` itself on. |
| **Expected UI** | Sign-in succeeds; the dashboard loads; no lockout message appears. |
| **Expected API** | `200 application/json` with a full `AuthResponse` and one `Set-Cookie: qams_rt=…; path=/api/auth; secure; httponly; samesite=strict`. |
| **Expected DB** | `failed_login_attempts = 0`; `locked_until_utc IS NULL` — `RegisterSuccessfulLogin` clears it (`UserAccount.cs:220-224`). One new `qams.refresh_session` row, `revoked_at_utc IS NULL`. |
| **Expected Audit** | One `audit.security_event` row `event_type='LOGIN_SUCCESS'`, `tenant_id` = the `demo-lab` uuid, `detail IS NULL`. One `audit.field_change` row `property='LockedUntilUtc'`, `old_value` = `L`, `new_value IS NULL`. |
| **Expected Notification** | n/a — none defined for login success. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = (SELECT id FROM qams.user_account WHERE email='bva.analyst@demo-lab.local');` |
| **Evidence** | HTTP capture with timestamp · `L` value · post-condition SQL showing `locked_until_utc IS NULL` · the `LockedUntilUtc` field-change row |
| **Result / Defect** | Not Run · — |
| **Notes** | The true `until == now` equality point is only reachable with a controlled clock. State in the execution record that this case demonstrates the **open** boundary (`>` not `>=`) by the T-or-later side; the exact-equality assertion belongs in a `UserAccount.IsLockedOut` unit case with an injected `IClock`. |

---

#### TC-AUTH-API-009 — Lock-expiry BVA at T+1s: the lock lapses and login succeeds without any unlock action  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003 · RSK-AUTH-002 |
| **Level / Type / Technique** | API · Functional (positive) · BVA — upper side of the lock boundary; State Transition `S2 Active-Locked → S1 Active-Unlocked` by elapse (front matter §3.1) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | `bva.analyst@demo-lab.local` is active with a known-good password |
| **Test Data** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc = now() - interval '1 second' WHERE email='bva.analyst@demo-lab.local';` then request with the correct password `Bva-Analyst-Pass-7!` |
| **Steps** | 1. Run the `UPDATE` above; record `locked_until_utc` as `L`. 2. `SELECT locked_until_utc, now() FROM qams.user_account WHERE email='bva.analyst@demo-lab.local';` — confirm `L < now()` and that the **row still holds a non-null lock** (nothing rewrote it). 3. `POST /api/auth/login` with the correct password. 4. `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='bva.analyst@demo-lab.local';`. |
| **Expected UI** | Sign-in succeeds and the dashboard loads; the user is never shown an "unlocked" confirmation — the lapse is silent. |
| **Expected API** | `200 application/json`, full `AuthResponse` (`mfaRequired:false`, `mfaEnrollmentRequired:false`), one `qams_rt` `Set-Cookie`. |
| **Expected DB** | Before step 3: `locked_until_utc = L` (non-null, in the past) — proving the lapse is **derived**, not persisted; the row is not rewritten by the passage of time. After step 3: `failed_login_attempts = 0`, `locked_until_utc IS NULL`. One new `qams.refresh_session` row. |
| **Expected Audit** | One `audit.security_event` row `event_type='LOGIN_SUCCESS'`. One `audit.field_change` row `property='LockedUntilUtc'`, `old_value` = `L`, `new_value IS NULL`. |
| **Expected Notification** | n/a — none defined for login success or lock lapse. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = (SELECT id FROM qams.user_account WHERE email='bva.analyst@demo-lab.local'); UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='bva.analyst@demo-lab.local';` |
| **Evidence** | Step-2 SQL proving the stale non-null lock · HTTP capture · post-condition SQL · the field-change row |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 2 is the interesting assertion: a lapsed lock stays in the column until the *next successful* authentication clears it, so any report that counts "currently locked accounts" by `locked_until_utc IS NOT NULL` over-counts. Raised as `GAP-AUTH-904` in the coverage note. |

---

#### TC-AUTH-API-010 — A successful login resets a partial failed-attempt counter to zero  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003 · RSK-AUTH-002 |
| **Level / Type / Technique** | API · Functional (positive) · State Transition — `S1 →(4 failures)→ S1 →(success)→ S1` with counter reset (`UserAccount.cs:220-224`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams`. Split across two 60-second rate-limit windows: four failures, wait 60 s, one success. |
| **Preconditions** | `bva.analyst@demo-lab.local` active, `failed_login_attempts=0`, `locked_until_utc IS NULL` |
| **Test Data** | Wrong password `Wrong-Pass-0000!` ×4, then correct password `Bva-Analyst-Pass-7!` ×1 |
| **Steps** | 1. `POST /api/auth/login` with the wrong password four times. 2. `SELECT failed_login_attempts FROM qams.user_account WHERE email='bva.analyst@demo-lab.local';` — expect 4. 3. Wait 60 s (fresh `AuthPolicy` window). 4. `POST /api/auth/login` with the **correct** password. 5. Re-run the SELECT. 6. `POST /api/auth/login` with the wrong password once. 7. Re-run the SELECT. |
| **Expected UI** | Four "Invalid credentials." messages, then a successful sign-in, then one more "Invalid credentials." — with no lockout at any point. |
| **Expected API** | Steps 1: four × `401` `code":"AUTH-001"`. Step 4: `200` with `AuthResponse` and a `qams_rt` cookie. Step 6: `401` `code":"AUTH-001"` — **not** `AUTH-004`. |
| **Expected DB** | Step 2: `failed_login_attempts = 4`. Step 5: `failed_login_attempts = 0`, `locked_until_utc IS NULL`. Step 7: `failed_login_attempts = 1` — the counter restarted from zero, proving the reset was real and not merely a display artefact. The account is **never** locked in this sequence even though 5 wrong passwords were entered in total. |
| **Expected Audit** | Six `audit.security_event` rows in order: `LOGIN_FAILED`(bad-password) ×4, `LOGIN_SUCCESS`, `LOGIN_FAILED`(bad-password). `audit.field_change` rows on `FailedLoginAttempts`: `0→1`, `1→2`, `2→3`, `3→4`, `4→0`, `0→1`. |
| **Expected Notification** | n/a — none defined for any login outcome. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = (SELECT id FROM qams.user_account WHERE email='bva.analyst@demo-lab.local'); UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='bva.analyst@demo-lab.local';` |
| **Evidence** | Six HTTP captures · three SQL snapshots · the ordered field-change sequence |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the "consecutive" in URS-003: the counter measures a consecutive run, not a lifetime total. Step 7 is what distinguishes a genuine reset from a coincidence. |

---

#### TC-AUTH-API-011 — Login with an unknown email returns AUTH-001 and touches no counter anywhere  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-001, URS-016 · RSK-AUTH-001 |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — §4.1 rule R3, `user is null` at `Login.cs:72-75` |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | n/a — no account exists · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | No `qams.user_account` row exists with `email='ghost.user@demo-lab.local'` — verify with `SELECT count(*) FROM qams.user_account WHERE email='ghost.user@demo-lab.local';` returning 0 |
| **Test Data** | Body `{"tenantIdentifier":"demo-lab","email":"ghost.user@demo-lab.local","password":"Any-Pass-1234!","mfaCode":null}` |
| **Steps** | 1. `SELECT count(*) FROM qams.user_account;` — record `N`. 2. `POST /api/auth/login` with the body above five times in succession. 3. Re-run step 1 — expect `N` unchanged. 4. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type, detail, actor, tenant_id FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 5;`. |
| **Expected UI** | "Invalid credentials." on every attempt — the same string and the same layout as a wrong-password failure. |
| **Expected API** | All five: `401 application/problem+json`, `code":"AUTH-001"`, `title":"Invalid credentials."` — **byte-identical** to TC-AUTH-API-003's body except `traceId`. No `Set-Cookie`. Five attempts never produce `AUTH-004`: there is no row to lock. |
| **Expected DB** | `qams.user_account` row count unchanged at `N`; **no** row is created, and no existing row's `failed_login_attempts` moves. There is no per-email or per-IP attempt counter in the schema — the only throttle on this path is the 10/min `AuthPolicy` window. |
| **Expected Audit** | Five `audit.security_event` rows, all `event_type='LOGIN_FAILED'`, `detail='no-such-user'`, `actor='ghost.user@demo-lab.local'` (the **normalised** email — trimmed and lower-cased at `Login.cs:69` before `FailAsync` at `:74`), `tenant_id` = the `demo-lab` uuid. **Zero** `audit.field_change` rows. |
| **Expected Notification** | n/a — none defined for a failed login. |
| **Cleanup** | n/a — no state was created. |
| **Evidence** | Five HTTP captures · before/after row count · five security-event rows |
| **Result / Defect** | Not Run · — |
| **Notes** | `detail='no-such-user'` is written for **both** "no row" and "row exists but inactive" (`Login.cs:72`), so the ledger cannot distinguish the two — see TC-AUTH-API-012 and `GAP-AUTH-905` in the coverage note. |

---

#### TC-AUTH-API-012 — Login to a deactivated account is indistinguishable from an unknown account  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-006, URS-009 · RSK-AUTH-006 |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — §4.1 rule R4, the `!user.IsActive` disjunct at `Login.cs:72` |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (deactivated) · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | `bva.analyst@demo-lab.local` exists with a known-good password and has been deactivated via `POST /api/users/{id}/deactivate` (see TC-AUTH-API-018), so `is_active = false`, `failed_login_attempts = 0`, `locked_until_utc IS NULL` |
| **Test Data** | Correct-password body `{"tenantIdentifier":"demo-lab","email":"bva.analyst@demo-lab.local","password":"Bva-Analyst-Pass-7!","mfaCode":null}` and, for comparison, the TC-AUTH-API-011 ghost body |
| **Steps** | 1. Confirm `SELECT is_active FROM qams.user_account WHERE email='bva.analyst@demo-lab.local';` is `f`. 2. `POST /api/auth/login` with the **correct** password. 3. `POST /api/auth/login` with the ghost email from TC-AUTH-API-011. 4. Diff the two response bodies field-by-field, ignoring `traceId`. 5. `SELECT failed_login_attempts FROM qams.user_account WHERE email='bva.analyst@demo-lab.local';`. 6. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type, detail, actor FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 2;`. |
| **Expected UI** | "Invalid credentials." — the SPA gives no hint that the account exists but is disabled, and offers no "contact your administrator" path. |
| **Expected API** | Step 2: `401 application/problem+json`, `code":"AUTH-001"`, `title":"Invalid credentials."`. Step 4: the two bodies differ **only** in `traceId`. A correct password against a deactivated account is refused with the same code as a nonexistent account. |
| **Expected DB** | `failed_login_attempts` **still 0** — the inactive branch returns before `RegisterFailedLogin`, so a deactivated account can never be locked out by login attempts. `is_active` remains `f`. |
| **Expected Audit** | Two `audit.security_event` rows, both `event_type='LOGIN_FAILED'`, both `detail='no-such-user'` — one with `actor='bva.analyst@demo-lab.local'`, one with `actor='ghost.user@demo-lab.local'`. The **detail string does not distinguish a disabled account from a nonexistent one**, which is a forensic limitation, not a defect of this case. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `POST /api/users/{id}/reactivate` as a caller holding `users.manage`, restoring `is_active = true`. |
| **Evidence** | Both HTTP captures · the field-by-field body diff · post-condition SQL · both security-event rows |
| **Result / Defect** | Not Run · — |
| **Notes** | The indistinguishability is deliberate at the wire (anti-enumeration) but is carried into the **ledger**, where it is not wanted — Part 11 §11.300(d) reporting cannot separate "someone is probing a disabled ex-employee's account" from "someone typed a wrong address". `GAP-AUTH-905`. |

---

#### TC-AUTH-API-013 — Login with an unknown tenant slug returns AUTH-001 and a null-tenant security event  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-001, URS-008, URS-016 · RSK-AUTH-003 |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — §4.1 rule R1, `SingleOrDefaultAsync` miss at `Login.cs:49-51` |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | n/a — no tenant resolves · n/a — anonymous endpoint · none — `tenantScope.Set` is never reached |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | No `saas.tenant` row has `identifier='no-such-lab'` — verify with `SELECT count(*) FROM saas.tenant WHERE identifier='no-such-lab';` returning 0. Note the slug **must be well-formed** (matches `^[a-z0-9](?:-?[a-z0-9]){1,49}$`, `TenantSlug.cs:47`) or a different code is produced — see TC-AUTH-EP-004. |
| **Test Data** | Body `{"tenantIdentifier":"no-such-lab","email":"admin@demo-lab.local","password":"Demo-Admin-Pass-2!","mfaCode":null}` |
| **Steps** | 1. `POST /api/auth/login` with the body above. 2. Record status, body and headers. 3. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type, tenant_id, actor, detail FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 1;`. 4. Re-run step 3 **without** the bypass but with `SELECT set_config('app.current_tenant','<demo-lab-uuid>',false);` and confirm the row is **not** returned. 5. `SELECT failed_login_attempts FROM qams.user_account WHERE email='admin@demo-lab.local';`. |
| **Expected UI** | The SPA at `/t/no-such-lab` will already have failed the anonymous workspace lookup with `404` (batch B); if the login form is reached directly, it shows "Invalid credentials." |
| **Expected API** | `401 application/problem+json`, `code":"AUTH-001"`, `title":"Invalid credentials."` — **not** `TENANT-404`, and not `AUTH-002`. No `Set-Cookie`. |
| **Expected DB** | No change anywhere: `admin@demo-lab.local`'s `failed_login_attempts` is unchanged (the handler throws before the user lookup at `Login.cs:70`), and no `saas.tenant` row is created. |
| **Expected Audit** | Step 3: one `audit.security_event` row `event_type='LOGIN_FAILED'`, **`tenant_id IS NULL`**, `actor='admin@demo-lab.local'` (the **raw** `command.Email`, not the normalised form — `Login.cs:51` passes `command.Email` before the `Trim().ToLowerInvariant()` at `:69`), `detail='unknown-tenant'`. Step 4: the row is **absent** from a tenant-scoped read, because the `tenant_isolation` `USING` clause `tenant_id = GUC OR bypass` is false for a null `tenant_id`. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — no state was created. |
| **Evidence** | HTTP capture · the bypass-read row · the tenant-scoped read proving invisibility · post-condition SQL on the user row |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 4 is the load-bearing assertion and the reason `RSK-AUTH-004` exists: an unknown-tenant credential probe is logged, but **no tenant can see it in their own compliance view** — only a platform operator with `app.bypass_rls='on'` can. Also assert the `actor` casing divergence in step 3: mixed-case input is stored verbatim on this branch and normalised on every later branch. |

---

#### TC-AUTH-API-014 — Login against a suspended tenant returns AUTH-002 with the tenant stamped on the event  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-001, URS-008 · RSK-AUTH-003 |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — §4.1 rule R2, `tenant.Status != Active` at `Login.cs:60-63`, with `tenantScope.Set` already applied at `:58` |
| **Priority / Severity / Automation** | High · Major · Partially — the precondition has no API path (see below), so automate as an integration case with direct SQL setup |
| **Role / Permission / Tenant** | TenantAdmin of the suspended tenant · n/a — anonymous endpoint · `iso-test-2` (suspended for the duration of the case) |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | A second tenant `iso-test-2` exists and is `Active` (measured 2026-08-01) with at least one active user. **There is no API endpoint that suspends a tenant** — the approved surface exposes only `GET /api/tenants` and `POST /api/tenants`; `Tenant.Suspend(reason)` exists in the domain (`src/NT.QAMS.Domain/Tenancy/Tenant.cs:58-73`) but is unreachable over HTTP. The precondition is therefore established by SQL: `UPDATE saas.tenant SET status='Suspended', suspension_reason='OQ test TC-AUTH-API-014' WHERE identifier='iso-test-2';` (the value passes `CHECK ck_tenant_status_domain`). |
| **Test Data** | Body `{"tenantIdentifier":"iso-test-2","email":"<an active user of iso-test-2>","password":"<that user's correct password>","mfaCode":null}` |
| **Steps** | 1. Run the suspend `UPDATE`; confirm `SELECT status FROM saas.tenant WHERE identifier='iso-test-2';` is `Suspended`. 2. `POST /api/auth/login` with the **correct** credentials for that tenant. 3. Record status and body. 4. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type, tenant_id, actor, detail FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 1;`. 5. `SELECT failed_login_attempts FROM qams.user_account WHERE email='<that user>';`. |
| **Expected UI** | The sign-in page shows "This tenant is not active."; the message names the workspace state, unlike every other failure, which says "Invalid credentials." |
| **Expected API** | `401 application/problem+json`, `code":"AUTH-002"`, `title":"This tenant is not active."`. No `Set-Cookie`. Correct credentials are refused solely on tenant state. |
| **Expected DB** | `failed_login_attempts` on the user **unchanged** — the handler throws at `Login.cs:62`, before the user is loaded at `:70`. `saas.tenant.status` remains `Suspended` (the login path never mutates tenant state). |
| **Expected Audit** | One `audit.security_event` row `event_type='LOGIN_FAILED'`, `detail='tenant-inactive'`, **`tenant_id` = the `iso-test-2` uuid** — non-null, because `tenantScope.Set(tenant.Id)` runs at `Login.cs:58` **before** the status check, which is precisely what lets the tenant-stamped row satisfy the `security_event` RLS `WITH CHECK`. This row **is** visible to a read scoped to `iso-test-2`; verify by re-reading with `set_config('app.current_tenant','<iso-test-2-uuid>',false)` and no bypass. |
| **Expected Notification** | n/a — none defined for a tenant-state login refusal. |
| **Cleanup** | `UPDATE saas.tenant SET status='Active', suspension_reason=NULL WHERE identifier='iso-test-2';` then re-confirm a successful login for that user. |
| **Evidence** | HTTP capture · the tenant-scoped audit read (no bypass) proving visibility · before/after `saas.tenant.status` · post-condition SQL on the user row |
| **Result / Defect** | Not Run · — |
| **Notes** | Contrast deliberately with TC-AUTH-API-013: unknown tenant ⇒ `AUTH-001` + **null**-tenant event (invisible to tenants); suspended tenant ⇒ `AUTH-002` + **stamped** event (visible). The pair proves the ordering of `tenantScope.Set` at `Login.cs:58`. `Provisioning` and `Terminated` are covered as partitions in TC-AUTH-EP-008. |

---

#### TC-AUTH-API-015 — Valid credentials presented against the wrong tenant slug are refused  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-008 · RSK-AUTH-003 |
| **Level / Type / Technique** | API · Security-functional (negative) · Equivalence Partitioning — the `(TenantId, Email)` composite key partition at `Login.cs:70`, backed by unique index `ix_user_account_tenant_id_email` |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin of `demo-lab` · n/a — anonymous endpoint · `iso-test-2` (the wrong tenant) |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | Both `demo-lab` and `iso-test-2` are `Active`. `admin@demo-lab.local` exists **only** under `demo-lab` — verify `SELECT t.identifier FROM qams.user_account u JOIN saas.tenant t ON t.id=u.tenant_id WHERE u.email='admin@demo-lab.local';` returns exactly one row, `demo-lab`. |
| **Test Data** | Body `{"tenantIdentifier":"iso-test-2","email":"admin@demo-lab.local","password":"Demo-Admin-Pass-2!","mfaCode":null}` — a **correct** email/password pair aimed at the wrong workspace |
| **Steps** | 1. `POST /api/auth/login` with the body above. 2. Record status and body. 3. `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='admin@demo-lab.local';`. 4. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type, tenant_id, actor, detail FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 1;`. 5. Repeat step 1 five more times, then attempt a **legitimate** `demo-lab` login with the correct password and confirm it succeeds. |
| **Expected UI** | The `/t/iso-test-2` sign-in page shows "Invalid credentials."; nothing indicates the address belongs to a different laboratory. |
| **Expected API** | `401 application/problem+json`, `code":"AUTH-001"`, `title":"Invalid credentials."`. No `Set-Cookie`. Step 5's legitimate login: `200` with a full `AuthResponse`. |
| **Expected DB** | `failed_login_attempts` on `admin@demo-lab.local` **remains 0** after all six wrong-tenant attempts — the `(tenantId, email)` predicate finds no row, so `RegisterFailedLogin` is never called. This is the security-relevant consequence: **an attacker cannot lock a known account out by attacking it through the wrong workspace slug**, and equally, cross-tenant probing accrues no evidence on the target row. |
| **Expected Audit** | Six `audit.security_event` rows, all `event_type='LOGIN_FAILED'`, `detail='no-such-user'`, `actor='admin@demo-lab.local'`, `tenant_id` = the **`iso-test-2`** uuid — the probed workspace, not the account's own. The victim tenant `demo-lab` sees **nothing** in its compliance view. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = (SELECT id FROM qams.user_account WHERE email='admin@demo-lab.local');` after the step-5 legitimate login. |
| **Evidence** | Six HTTP captures · post-condition SQL showing `failed_login_attempts = 0` · the six security-event rows with the `iso-test-2` stamp · the step-5 success capture |
| **Result / Defect** | Not Run · — |
| **Notes** | Two findings to record in the execution notes, both as-built and neither a failure of this case: cross-tenant probing is invisible to the victim tenant, and it is un-throttled by the lockout (only the 10/min per-IP budget applies). Both feed `GAP-AUTH-906`. |

---

#### TC-AUTH-API-016 — Create a tenant user, then sign in with the issued credentials  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-009, URS-002, URS-011 · RSK-AUTH-005 |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — provisioning through to first sign-in (`RegisterUserHandler`, `UserManagement.cs:51-87`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin (caller) · **`users.manage`** — `[RequirePermission(PermissionCatalog.Users, PermissionAction.Manage)]` on `UsersController.cs:28` and `[RequirePermissionPolicy(Users, Manage)]` on the command, `UserManagement.cs:32` · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | Caller is signed in as `admin@demo-lab.local` holding a role that grants `users.manage`. No `qams.user_account` row exists with `email='bva.analyst@demo-lab.local'` in `demo-lab`. |
| **Test Data** | `POST /api/users` body `{"email":"BVA.Analyst@Demo-Lab.local","displayName":"  BVA Analyst  ","role":"analyst","initialPassword":"Bva-Analyst-Pass-7!","roleId":null}` — note the deliberate mixed case, surrounding whitespace and lower-case role string |
| **Steps** | 1. `POST /api/users` with the bearer token and the body above. 2. Record status and body. 3. `SELECT id, email, display_name, role, is_active, role_id, failed_login_attempts, locked_until_utc, password_changed_at_utc, mfa_enabled, pin_hash FROM qams.user_account WHERE email='bva.analyst@demo-lab.local';`. 4. `SELECT entity_type, action, property, old_value, new_value, actor, tenant_id FROM audit.field_change WHERE entity_type='UserAccount' ORDER BY occurred_at_utc DESC LIMIT 3;`. 5. `POST /api/auth/login` (no bearer) with `{"tenantIdentifier":"demo-lab","email":"bva.analyst@demo-lab.local","password":"Bva-Analyst-Pass-7!","mfaCode":null}`. |
| **Expected UI** | The Users administration page adds the row "BVA Analyst · Analyst · Active"; the new user can sign in at `/t/demo-lab` on the next attempt. |
| **Expected API** | Step 1: `200 application/json` with body `{"id":"<uuid>"}` — **200, not 201**, and the payload is an anonymous `{ id }` wrapper (`UsersController.cs:30-31`); no `Location` header is emitted. Step 5: `200` with a full `AuthResponse`, `role":"Analyst"`, `mfaRequired:false`. |
| **Expected DB** | One new row: `email='bva.analyst@demo-lab.local'` (trimmed + lower-cased by `UserAccount.Create`, `UserAccount.cs:112`), `display_name='BVA Analyst'` (trimmed, `:113`), `role='Analyst'` (satisfying `CHECK ck_user_account_role_domain`), `is_active=true` (`:116`), `role_id` **non-null** — `SeededRoleDefault.AssignAsync` back-fills the tier-equivalent configurable role when `roleId` is omitted (`UserManagement.cs:81`), `failed_login_attempts=0`, `locked_until_utc IS NULL`, **`password_changed_at_utc IS NULL`** (`Create` never stamps it), `mfa_enabled=false`, `pin_hash IS NULL`. `password_hash` is a PBKDF2 string of ≥ 60 chars that is **not** the plaintext. After step 5, one `qams.refresh_session` row. |
| **Expected Audit** | One `audit.field_change` row `entity_type='UserAccount'`, `action='Created'`, `property IS NULL`, `old_value IS NULL`, `new_value IS NULL` (`FieldChangeInterceptor.cs:69`), `actor` = the caller's display name, `actor_id` = the caller's user id, `tenant_id` = the `demo-lab` uuid. One `qams.outbox_event` of type `NT.QAMS.Domain.IdentityAccess.UserRoleAssigned, NT.QAMS.Domain` from the seeded-role assignment (`UserAccount.cs:174`). After step 5, one `audit.security_event` `event_type='LOGIN_SUCCESS'`. |
| **Expected Notification** | n/a — no welcome or credential-delivery notification exists; the initial password is chosen by the administrator and conveyed out of band. |
| **Cleanup** | This fixture is consumed by TC-AUTH-API-003 through -012 and -018 through -022. Final teardown (after the whole batch) is `DELETE FROM qams.refresh_session WHERE user_id=<id>; DELETE FROM qams.user_branch_access WHERE user_id=<id>; DELETE FROM qams.user_department_access WHERE user_id=<id>; DELETE FROM qams.user_account WHERE id=<id>;` executed directly in SQL — **there is no API delete** (see TC-AUTH-API-021). |
| **Evidence** | HTTP captures for steps 1 and 5 · the full new-row SELECT · the `Created` field-change row · the `UserRoleAssigned` outbox row |
| **Result / Defect** | Not Run · — |
| **Notes** | `password_changed_at_utc IS NULL` matters downstream: with a null stamp the `MaxAgeDays` check is skipped entirely (`Login.cs:90-92`), so a freshly created account can never hit `AUTH-101`. Record it here so the password-aging cases in batch B do not build on a false precondition. |

---

#### TC-AUTH-API-017 — Creating a second account with an existing email in the same tenant is refused with USER-008  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-001, URS-009 · RSK-AUTH-005 |
| **Level / Type / Technique** | API · Functional (negative) · Error Guessing — the pre-check at `UserManagement.cs:57-60` and its backing unique index `ix_user_account_tenant_id_email` |
| **Priority / Severity / Automation** | High · Major · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin · `users.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | `bva.analyst@demo-lab.local` exists in `demo-lab` (created by TC-AUTH-API-016). Caller holds `users.manage`. |
| **Test Data** | Body A (exact duplicate) `{"email":"bva.analyst@demo-lab.local","displayName":"Duplicate One","role":"Analyst","initialPassword":"Dup-Attempt-Pass-3!"}`. Body B (case variant) same but `"email":"BVA.ANALYST@DEMO-LAB.LOCAL"`. |
| **Steps** | 1. `SELECT count(*) FROM qams.user_account WHERE tenant_id=(SELECT id FROM saas.tenant WHERE identifier='demo-lab');` — record `N`. 2. `POST /api/users` with body A. 3. `POST /api/users` with body B. 4. Re-run step 1. |
| **Expected UI** | The Users page keeps the create drawer open and shows "A user with email 'bva.analyst@demo-lab.local' already exists in this tenant."; the row list is unchanged. |
| **Expected API** | Both requests: `422 application/problem+json` (a `DomainException` whose code neither starts with `AUTH-`/`AUTHZ-` nor ends with `-404`, `DomainExceptionHandler.cs:75-80`), `code":"USER-008"`, `title":"A user with email 'bva.analyst@demo-lab.local' already exists in this tenant."` — the title carries the **normalised** address for both bodies, because the handler lower-cases before the existence check (`UserManagement.cs:56`). |
| **Expected DB** | Row count unchanged at `N`. The refusal comes from the application pre-check, **not** from the unique index — no `DbUpdateException` and therefore no `CONCURRENCY-409`. |
| **Expected Audit** | **Zero** `audit.field_change` rows — the handler throws before `db.Users.Add` (`UserManagement.cs:59` before `:84`). Zero `audit.security_event` rows: user administration writes to the field-change ledger, not the security-event ledger. |
| **Expected Notification** | n/a — none defined for a rejected creation. |
| **Cleanup** | n/a — no state was created. |
| **Evidence** | Both HTTP captures · before/after row counts · a `count(*)` on `audit.field_change` showing a delta of 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | Body B proves the uniqueness is case-insensitive **by normalisation**, not by a case-insensitive index: `ix_user_account_tenant_id_email` is a plain btree on `(tenant_id, email)` and would happily admit two casings if any writer skipped `ToLowerInvariant()`. The invariant is upheld in two places (`UserManagement.cs:56` and `UserAccount.cs:112`) and in no constraint. |

---

#### TC-AUTH-API-018 — Deactivation blocks login but leaves live refresh sessions un-revoked  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-006, URS-009 · RSK-AUTH-006 |
| **Level / Type / Technique** | API · Functional (negative) · State Transition — `S1 Active-Unlocked → S3 Inactive-Unlocked` via `SetUserActiveHandler` (`UserManagement.cs:134-139`); implementation-derived because the assertion documents `GAP-AUTH-014` rather than a requirement |
| **Priority / Severity / Automation** | Critical · Major · Yes (integration) |
| **Role / Permission / Tenant** | TenantAdmin (actor) over Analyst (subject) · `users.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | `bva.analyst@demo-lab.local` is `is_active=true` and has signed in **twice from two different clients**, so `SELECT count(*) FROM qams.refresh_session WHERE user_id=<id> AND revoked_at_utc IS NULL;` returns 2, in two distinct `family_id` values. Caller holds `users.manage`. |
| **Test Data** | `POST /api/users/{id}/deactivate` where `{id}` is the analyst's uuid; empty body |
| **Steps** | 1. Record `SELECT id, family_id, revoked_at_utc, expires_at_utc FROM qams.refresh_session WHERE user_id=<id>;`. 2. `POST /api/users/{id}/deactivate` with the administrator's bearer token. 3. `SELECT is_active FROM qams.user_account WHERE id=<id>;`. 4. Re-run step 1 **immediately**. 5. `POST /api/auth/login` with the analyst's correct credentials. 6. `SELECT action, property, old_value, new_value, actor, actor_id FROM audit.field_change WHERE entity_id=<id::text> ORDER BY occurred_at_utc DESC LIMIT 1;`. |
| **Expected UI** | The Users page flips the row's status pill from Active to Inactive with no confirmation dialog and no "this will end their sessions" warning. |
| **Expected API** | Step 2: `204 No Content`, empty body (`UsersController.cs:70-74`). Step 5: `401 application/problem+json`, `code":"AUTH-001"` (the inactive disjunct at `Login.cs:72`), **not** `AUTH-006` — `AUTH-006` belongs to the authenticated paths, not to login. |
| **Expected DB** | Step 3: `is_active = false`. Step 4: **both** `qams.refresh_session` rows still have `revoked_at_utc IS NULL` and unchanged `expires_at_utc` — deactivation revokes nothing (`SetUserActiveHandler` calls `Deactivate()` and `SaveChangesAsync` only). They persist until each is individually presented, or until `OutboxProcessor.RunRetentionPurgeAsync` deletes them 7 days after their own expiry (`OutboxProcessor.cs:264-272`). |
| **Expected Audit** | One `audit.field_change` row: `entity_type='UserAccount'`, `action='Modified'`, `property='IsActive'`, `old_value='True'`, `new_value='False'`, `actor` = the administrator's display name, `actor_id` = the administrator's uuid, `tenant_id` = the `demo-lab` uuid. **No** `audit.security_event` row of any type — account deactivation is not one of the 12 emitted security-event types. |
| **Expected Notification** | n/a — no notification is sent to the deactivated user or to anyone else. |
| **Cleanup** | `POST /api/users/{id}/reactivate` (restores `is_active=true`), then `DELETE FROM qams.refresh_session WHERE user_id=<id>;` |
| **Evidence** | Before/after `refresh_session` snapshots proving `revoked_at_utc IS NULL` on both rows · the `204` capture · the `401 AUTH-001` capture · the `IsActive` field-change row · a `count(*)` on `audit.security_event` showing a delta of 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | The **enforcement** outcome is correct — TC-AUTH-API-020 proves the next authenticated request is refused — so this is an evidence defect, not an access-control defect (`GAP-AUTH-014`). Do **not** author a passing case asserting eager family revocation; contrast with logout, which does revoke the whole family (`RefreshSessions.cs:173-178`). |

---

#### TC-AUTH-API-019 — Reactivating an account that was deactivated while locked returns it still locked  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-003, URS-009 · RSK-AUTH-005 |
| **Level / Type / Technique** | API · Functional (negative) · State Transition — the `S4 Inactive-Locked → S2 Active-Locked` edge of front matter §3.1; `Reactivate()` is an unguarded one-liner (`UserAccount.cs:122`) |
| **Priority / Severity / Automation** | High · Major · Yes (integration) |
| **Role / Permission / Tenant** | TenantAdmin (actor) over Analyst (subject) · `users.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | `bva.analyst@demo-lab.local` is `is_active=true` and **locked**: `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc = now() + interval '30 minutes' WHERE email='bva.analyst@demo-lab.local';`. Record the value as `L`. Caller holds `users.manage`. |
| **Test Data** | `POST /api/users/{id}/deactivate` then `POST /api/users/{id}/reactivate`, both with empty bodies |
| **Steps** | 1. `SELECT is_active, locked_until_utc FROM qams.user_account WHERE id=<id>;` — expect `t`, `L`. 2. `POST /api/users/{id}/deactivate`. 3. Re-run the SELECT — expect `f`, `L`. 4. `POST /api/users/{id}/reactivate`. 5. Re-run the SELECT. 6. `POST /api/auth/login` with the analyst's **correct** password. |
| **Expected UI** | The Users page shows the row returning to Active. The user, believing the administrator has "restored" the account, still cannot sign in, and the sign-in page gives them the lockout message with no explanation of why reactivation did not help. |
| **Expected API** | Steps 2 and 4: `204 No Content` each. Step 6: `401 application/problem+json`, `code":"AUTH-004"`, `title":"Account is temporarily locked. Try again later."` |
| **Expected DB** | Step 3: `is_active=false`, `locked_until_utc = L` (unchanged — `Deactivate()` touches only `IsActive`). Step 5: `is_active=true`, **`locked_until_utc` still `L`** — reactivation clears nothing. Step 6 leaves both values untouched (the `AUTH-004` branch returns first). |
| **Expected Audit** | Two `audit.field_change` rows, both `property='IsActive'`: `'True'→'False'` then `'False'→'True'`, each with the administrator as `actor`. **No** `LockedUntilUtc` row in either — nothing modified it. One `audit.security_event` row from step 6: `event_type='LOGIN_FAILED'`, `detail='locked-out'`. |
| **Expected Notification** | n/a — none defined for activation changes. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='bva.analyst@demo-lab.local';` |
| **Evidence** | Three SQL snapshots showing `locked_until_utc = L` throughout · both `204` captures · the `401 AUTH-004` capture · both `IsActive` field-change rows |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the trap edge of the account state machine and the operational face of `GAP-AUTH-013` (no administrative unlock): the only two ways out are to wait for `L` to pass or to force a password reset (TC-AUTH-API-022). Deactivate/reactivate is not a recovery procedure and the UI should not imply it is. |

---

#### TC-AUTH-API-020 — A deactivated user's next authenticated request is refused with AUTH-006  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-006 · RSK-AUTH-006 |
| **Level / Type / Technique** | API · Functional (negative) · State Transition — `ActiveSessionMiddleware` re-checks `{IsActive, Role}` on every authenticated request (`RequestIdentity.cs:93-102`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst (subject, holding a valid unexpired JWT) · n/a — the refusal precedes any permission filter · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | `bva.analyst@demo-lab.local` is active and has just signed in; capture the access token `J` (valid ~15 min). A second session as a `users.manage` holder is open in parallel. |
| **Test Data** | Token `J`; probe endpoint `GET /api/auth/me/privileges` (`[Authorize]`, no `[RequirePermission]`, `AuthController.cs:147-150`) |
| **Steps** | 1. `GET /api/auth/me/privileges` with `Authorization: Bearer <J>` — confirm `200`. 2. In the administrator session, `POST /api/users/{analystId}/deactivate` → `204`. 3. Immediately re-issue `GET /api/auth/me/privileges` with the **same, still-unexpired** token `J`. 4. `POST /api/auth/refresh` presenting the analyst's `qams_rt` cookie. 5. `SELECT revoked_at_utc FROM qams.refresh_session WHERE user_id=<analystId>;`. |
| **Expected UI** | The analyst's next click bounces to the sign-in page; the SPA's silent-refresh attempt also fails, so no automatic recovery occurs. |
| **Expected API** | Step 1: `200`. Step 3: `401 application/problem+json`, `code":"AUTH-006"`, `title":"Your session is no longer valid. Please sign in again."` — written by `ActiveSessionMiddleware`'s `Deny` (`RequestIdentity.cs:100,128-130`), **not** by `DomainExceptionHandler`. Step 4: `401 application/problem+json`, `code":"AUTH-006"` (`RefreshSessions.cs:124`). |
| **Expected DB** | After step 3: **no** row change — the middleware read is `AsNoTracking` and denies without writing (`RequestIdentity.cs:93-96`). After step 4: the **presented** session row has `revoked_at_utc` set; any sibling session in another family is still `revoked_at_utc IS NULL` (`RefreshSessions.cs:122-123` revokes only the presented one). |
| **Expected Audit** | **Zero** `audit.security_event` rows for either refusal — neither `ActiveSessionMiddleware` nor the `AUTH-006` branch of the refresh handler writes one (the 12 emitted types do not include a session-denial type). One `audit.field_change` row for the refresh-session revocation in step 4: `entity_type='RefreshSession'`, `action='Modified'`, `property='RevokedAtUtc'`. |
| **Expected Notification** | n/a — none defined for a session refusal. |
| **Cleanup** | `POST /api/users/{analystId}/reactivate`; `DELETE FROM qams.refresh_session WHERE user_id=<analystId>;` |
| **Evidence** | Three HTTP captures (200, 401 AUTH-006, 401 AUTH-006) with the identical bearer token · the refresh-session SQL before and after · a `count(*)` on `audit.security_event` showing a delta of 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | This case discharges URS-006's "takes effect immediately" clause. The **absence** of any security-event row for a revoked-session denial is the reportable observation: URS-016 lists "session/privilege changes" as security-relevant, and nothing is logged here. Folded into `GAP-AUTH-907`. |

---

#### TC-AUTH-API-021 — There is no account-deletion endpoint; de-provisioning cannot be performed through the API  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-009 · RSK-AUTH-005 |
| **Level / Type / Technique** | API · Structural / negative · Error Guessing over the approved API-surface baseline; **Gap-dependent** on `GAP-AUTH-901` |
| **Priority / Severity / Automation** | High · Major · Yes (functional — the surface assertion automates as an `ApiSurface.approved.txt` diff) |
| **Role / Permission / Tenant** | TenantAdmin · `users.manage` (the privilege that would gate it, if it existed) · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | `bva.analyst@demo-lab.local` exists. Caller holds `users.manage`. |
| **Test Data** | `DELETE /api/users/{id}` and `DELETE /api/v1/users/{id}`, each with and without header `X-Change-Reason: OQ de-provisioning test` |
| **Steps** | 1. `grep -c "DELETE /api/users" tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` — expect **0** (measured 2026-08-01: the baseline lists exactly 9 `/api/users` routes, none of them DELETE). 2. `DELETE /api/users/{id}` with the bearer token and the `X-Change-Reason` header. 3. Repeat without the header. 4. `DELETE /api/v1/users/{id}`. 5. `SELECT count(*) FROM qams.user_account WHERE id=<id>;`. |
| **Expected UI** | The Users administration page offers Deactivate and Reactivate; there is no Delete control and no archive/purge action. |
| **Expected API** | Steps 2–4: `405 Method Not Allowed` from MVC routing (the `{id:guid}` template exists for POST/PUT but no DELETE action is mapped). If the observed status is `404` instead, record the observed value — the point of the case is that **no** `2xx` and no `CHANGE-REASON-REQUIRED` (`400`) response is produced, because `ChangeReasonMiddleware` (`RequestIdentity.cs:154`) never sees a matched DELETE endpoint on this route. |
| **Expected DB** | `qams.user_account` row count for that id is **1** after every attempt — the account is not deleted, not soft-deleted and not flagged. Note there is no `deleted_at`/`is_deleted` column on `qams.user_account` (measured: the table has 21 columns, none of them a deletion marker). |
| **Expected Audit** | **Zero** `audit.field_change` rows of `action='Deleted'` for `entity_type='UserAccount'`. Note the ledger's `CHECK ck_field_change_action_domain` does admit `'Deleted'`, and the interceptor would emit it (`FieldChangeInterceptor.cs:71-73`) — the capability exists in the trail and has no producer for this entity. |
| **Expected Notification** | n/a — no de-provisioning workflow exists to notify anyone. |
| **Cleanup** | n/a — no state changes. |
| **Evidence** | The API-surface grep output · three HTTP captures with their exact statuses · the row-count SQL · the `\d qams.user_account` column list showing no deletion marker |
| **Result / Defect** | Not Run · — |
| **Notes** | **Acceptance criteria for `GAP-AUTH-901`, precise enough to implement against:** *(a)* the URS states whether account **deletion** is required at all, or whether deactivation plus retention is the intended de-provisioning model — Part 11 §11.10(e) and ISO 17025 §7.11 both favour retention over erasure, so "no delete" may be the correct answer and simply needs recording; *(b)* if erasure is required (e.g. for a GDPR erasure request), it is a distinct operation from deactivation, gated by a **new** permission key rather than `users.manage`, requiring `X-Change-Reason` per `ChangeReasonMiddleware`, and it must leave the account's `audit.field_change` and `audit.security_event` history intact while nulling identifying columns; *(c)* the orphan problem is closed first — `qams.refresh_session` and `saas.password_history` carry **no FK to `user_account`** (measured; `GAP-AUTH-006`), so a hard delete would strand credential material; *(d)* a functional case proves the chosen model, and `ApiSurface.approved.txt` is updated in the same commit or the merge gate fails. |

---

#### TC-AUTH-API-022 — Administrative password reset is the only API action that clears a lockout  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002, URS-003, URS-009 · RSK-AUTH-005 |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — the recovery matrix of front matter §3.1 row `S2`; `ResetPassword` clears both lock fields (`UserAccount.cs:143-146`) |
| **Priority / Severity / Automation** | High · Major · Yes (integration) |
| **Role / Permission / Tenant** | TenantAdmin (actor) over Analyst (subject) · `users.manage` — `UsersController.cs:85` · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | `bva.analyst@demo-lab.local` is `is_active=true` and locked: `locked_until_utc = now() + interval '30 minutes'`, `failed_login_attempts = 0`. Record `password_hash` as `H0` and `password_changed_at_utc` as `P0`. Caller holds `users.manage`. |
| **Test Data** | `POST /api/users/{id}/reset-password` body `{"newPassword":"Reset-Analyst-Pass-9!"}` |
| **Steps** | 1. `SELECT password_hash, password_changed_at_utc, failed_login_attempts, locked_until_utc FROM qams.user_account WHERE id=<id>;` — record `H0`, `P0`. 2. `POST /api/users/{id}/reset-password` with the body above. 3. Re-run the SELECT. 4. `POST /api/auth/login` with the **new** password. 5. `POST /api/auth/login` with the **old** password `Bva-Analyst-Pass-7!`. 6. `SELECT action, property, old_value, new_value FROM audit.field_change WHERE entity_id=<id::text> ORDER BY occurred_at_utc DESC LIMIT 5;`. |
| **Expected UI** | The Users page confirms the reset; the administrator must convey the new password out of band (no email is sent). The analyst can sign in immediately, before the 30 minutes elapse. |
| **Expected API** | Step 2: `204 No Content`. Step 4: `200` with a full `AuthResponse` and a `qams_rt` cookie — **the lock is gone**, though the administrator's stated intent was only to change a password. Step 5: `401 application/problem+json`, `code":"AUTH-001"`. |
| **Expected DB** | After step 2: `password_hash != H0`; **`password_changed_at_utc IS NULL`** — `ResetUserPasswordHandler` calls `ResetPassword(hash)` with the optional `at` parameter defaulted, so the stamp is set to `null`, not to now (`UserManagement.cs:147`; `UserAccount.cs:136,144`); `failed_login_attempts = 0`; **`locked_until_utc IS NULL`**. No `saas.password_history` row is written — history is appended by `ChangePasswordHandler` only (`Login.cs:219-224`), never by an administrative reset. |
| **Expected Audit** | `audit.field_change` rows from the reset: `property='PasswordHash'` with **`old_value='«redacted»'` and `new_value='«redacted»'`** (the property name contains both "password" and "hash", `FieldChangeInterceptor.cs:34,95-99`), `property='LockedUntilUtc'` `old_value` = the lock instant `new_value IS NULL`, and — if `P0` was non-null — `property='PasswordChangedAtUtc'` `new_value IS NULL`. Actor is the administrator. **No** `audit.security_event` row: `PASSWORD_CHANGED` is written by the self-service handler only (`Login.cs:235`), so an administrative reset leaves **no security-event trace at all**. |
| **Expected Notification** | n/a — the reset user is not notified that their password was changed by someone else. |
| **Cleanup** | Restore the fixture password with a second reset to `Bva-Analyst-Pass-7!`, then `DELETE FROM qams.refresh_session WHERE user_id=<id>; UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE id=<id>;` |
| **Evidence** | Before/after SQL of all four columns · both login captures · the redacted `PasswordHash` field-change row (proof of URS-019) · a `count(*)` on `audit.security_event` showing a delta of 0 |
| **Result / Defect** | Not Run · — |
| **Notes** | Three as-built facts to record, none of them a failure of this case: the unlock is a **side effect** of a credential change (`GAP-AUTH-013`); the reset leaves `password_changed_at_utc` null, which permanently exempts the account from `MaxAgeDays` aging until the user performs a self-service change; and an administrative reset is invisible to the security-event ledger while a self-service change is not — an asymmetry worth raising against URS-016 (`GAP-AUTH-907`). The `«redacted»` assertion is the AUTH-side proof of URS-019. |

---

#### TC-AUTH-API-023 — The security-event ledger for a complete five-failure lockout sequence  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-016 · RSK-AUTH-004 |
| **Level / Type / Technique** | API · Functional (audit) · Use Case with Data Flow — the six `ISecurityEventLog.WriteAsync` call sites reachable from `LoginHandler` (`Login.cs:103,139,152`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (integration) |
| **Role / Permission / Tenant** | Analyst (subject) · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams`. Sequence spans two 60-second `AuthPolicy` windows. |
| **Preconditions** | `bva.analyst@demo-lab.local` active, `failed_login_attempts=0`, `locked_until_utc IS NULL`. Record `SELECT max(occurred_at_utc) FROM audit.security_event;` as `W0` after `set_config('app.bypass_rls','on',false)`. |
| **Test Data** | Wrong password `Wrong-Pass-0000!` ×5, then wrong password ×1 (locked), then wait for the lock and sign in with `Bva-Analyst-Pass-7!` |
| **Steps** | 1. Five `POST /api/auth/login` with the wrong password. 2. Wait 60 s. 3. One `POST /api/auth/login` with the wrong password (now locked). 4. `UPDATE qams.user_account SET locked_until_utc = now() - interval '1 second' WHERE email='bva.analyst@demo-lab.local';` to shortcut the 30-minute wait. 5. `POST /api/auth/login` with the correct password. 6. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type, tenant_id, actor, detail, ip_address, occurred_at_utc FROM audit.security_event WHERE occurred_at_utc > '<W0>' ORDER BY occurred_at_utc;`. 7. Attempt `UPDATE audit.security_event SET detail='tampered' WHERE occurred_at_utc > '<W0>';` and `DELETE FROM audit.security_event WHERE occurred_at_utc > '<W0>';`. |
| **Expected UI** | n/a — this case asserts the ledger, not a screen; no AUTH screen renders `audit.security_event`. |
| **Expected API** | Step 1: five × `401 AUTH-001`. Step 3: `401 AUTH-004`. Step 5: `200`. |
| **Expected DB** | `failed_login_attempts` ends at 0, `locked_until_utc IS NULL`. |
| **Expected Audit** | Step 6 returns exactly **seven** rows in this order: `LOGIN_FAILED`/`bad-password` ×5, `LOGIN_FAILED`/`locked-out` ×1, `LOGIN_SUCCESS`/`detail IS NULL` ×1. Every row has `event_type` within `varchar(40)`, `actor='bva.analyst@demo-lab.local'` (normalised), `tenant_id` = the `demo-lab` uuid, and **`ip_address IS NULL`** on all seven — the column is `inet` and populated by nothing (`ComplianceLedgerServices.cs:73-81`; `GAP-AUTH-005`). Step 7: **both** statements are rejected by the `security_event_append_only` BEFORE UPDATE OR DELETE trigger calling `audit.reject_mutation()` (measured on `ntqams`), with 0 rows affected and an error raised. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='bva.analyst@demo-lab.local'; DELETE FROM qams.refresh_session WHERE user_id=<id>;` The seven ledger rows are **not** removable by design (step 7 proves it) and remain in the dev DB. |
| **Evidence** | The seven-row ordered SELECT · the two rejected DDL/DML attempts with their PostgreSQL error text · seven HTTP captures |
| **Result / Defect** | Not Run · — |
| **Notes** | The `ip_address IS NULL` assertion is a **positive** assertion of the as-built state; do not write it as a failing expectation. Two of the seven rows are the compliance-relevant pair Part 11 §11.300(d) asks for (the lockout-triggering failure and the locked-out refusal) — but neither carries a distinct `event_type`, so a monitoring rule must key on `detail`, a `varchar(500)` free-text column with no CHECK. Recorded in `GAP-AUTH-907`. |

---

#### TC-AUTH-API-024 — The field-change ledger for the lockout write, including tenant attribution and redaction  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-011, URS-019 · RSK-AUTH-004 |
| **Level / Type / Technique** | API · Functional (audit) · Data Flow — `FieldChangeInterceptor.Capture` over the `SaveChanges` at `Login.cs:85` |
| **Priority / Severity / Automation** | High · Major · Yes (integration) |
| **Role / Permission / Tenant** | Analyst (subject) · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | `bva.analyst@demo-lab.local` at `failed_login_attempts = 4`, `locked_until_utc IS NULL`, `is_active = true`. Record `SELECT max(occurred_at_utc) FROM audit.field_change;` as `F0` (with bypass). |
| **Test Data** | One wrong-password login: `{"tenantIdentifier":"demo-lab","email":"bva.analyst@demo-lab.local","password":"Wrong-Pass-0000!","mfaCode":null}` |
| **Steps** | 1. `POST /api/auth/login` with the body above (the 5th consecutive failure). 2. `SELECT set_config('app.bypass_rls','on',false); SELECT entity_type, entity_id, action, property, old_value, new_value, actor, actor_id, reason, tenant_id FROM audit.field_change WHERE occurred_at_utc > '<F0>' ORDER BY occurred_at_utc;`. 3. Re-run the SELECT **without** bypass but with `set_config('app.current_tenant','<demo-lab-uuid>',false)` and confirm the same rows are returned. 4. Repeat steps 1–2 for the **platform admin** (`platform-admin@localhost`, no tenant identifier, wrong password) and compare `tenant_id`. |
| **Expected UI** | n/a — no AUTH screen renders `audit.field_change`; the compliance module surfaces it elsewhere. |
| **Expected API** | Step 1: `401 application/problem+json`, `code":"AUTH-001"`. |
| **Expected DB** | `failed_login_attempts = 0`, `locked_until_utc` = now + 30 min. |
| **Expected Audit** | Step 2 returns exactly **two** rows, both `entity_type='UserAccount'`, `entity_id` = the analyst's uuid rendered **without** the tenant component (`RenderKey` drops `TenantId`, `FieldChangeInterceptor.cs:171-174`), `action='Modified'`, `actor='system'` and **`actor_id IS NULL`** (the caller is unauthenticated, `FieldChangeInterceptor.cs:113-114`), `reason IS NULL` (no `X-Change-Reason` on a POST), `tenant_id` = the `demo-lab` uuid (resolved via `IOptionallyTenantScoped`, `FieldChangeInterceptor.cs:145-148`). Row 1: `property='FailedLoginAttempts'`, `old_value='4'`, `new_value='0'`. Row 2: `property='LockedUntilUtc'`, `old_value IS NULL`, `new_value` = the lock instant. **Neither value is redacted** — the redaction list is `password, secret, pin, hash, token` (`:34`) and neither property name matches. Step 3 returns the same two rows (they are tenant-stamped, unlike the null-tenant security event of TC-AUTH-API-013). Step 4's platform-admin rows carry **`tenant_id IS NULL`** — the account has no tenant and no request tenant is set. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email IN ('bva.analyst@demo-lab.local','platform-admin@localhost');` The ledger rows are append-only and remain. |
| **Evidence** | Both SELECT variants (bypass and tenant-scoped) · the platform-admin comparison rows · the HTTP capture |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 4 is the tenancy assertion that matters for reporting: a platform-admin lockout produces a **null-tenant** field-change row, which no tenant compliance view can see. Step 2's `actor='system'` is the correct as-built value for an anonymous credential path — do not expect the subject's own name; the ledger records who *performed* the write, and nobody was authenticated. |

---

#### TC-AUTH-API-025 — Per-tenant `password_expiry_days` is persisted but never consulted at login  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 (partial) · RSK-AUTH-001 |
| **Level / Type / Technique** | API · Functional (negative) · Statement coverage of the unreached read — **Gap-dependent** on `GAP-AUTH-903` |
| **Priority / Severity / Automation** | Medium · Major · Yes (integration, once the gap is closed) |
| **Role / Permission / Tenant** | Analyst (subject) · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams`. `Security:PasswordMaxAgeDays` is the **global** setting (`PasswordPolicyOptions.MaxAgeDays = 90`, `Infrastructure/DependencyInjection.cs:76-78`). |
| **Preconditions** | `saas.tenant.password_expiry_days` for `demo-lab` is at its default (`90`, measured). `bva.analyst@demo-lab.local` has `password_changed_at_utc` set to a **non-null** past instant (perform one self-service `POST /api/auth/change-password` first, since `UserAccount.Create` and administrative reset both leave it null and thereby skip aging entirely, `Login.cs:90-92`). |
| **Test Data** | `UPDATE saas.tenant SET password_expiry_days = 1 WHERE identifier='demo-lab';` then `UPDATE qams.user_account SET password_changed_at_utc = now() - interval '5 days' WHERE email='bva.analyst@demo-lab.local';` then log in with the correct password |
| **Steps** | 1. Apply both `UPDATE`s above. 2. `POST /api/auth/login` with the analyst's correct password. 3. Record the status and `code`. 4. `UPDATE saas.tenant SET password_expiry_days = 90 WHERE identifier='demo-lab';` and re-run step 2. 5. `grep -rn "PasswordExpiryDays" src/ --include=*.cs | grep -v Migrations` and record the output. |
| **Expected UI** | The analyst signs in normally; no "your password has expired" screen is shown, despite the tenant setting saying the password expired four days ago. |
| **Expected API** | Steps 2 and 4 both: `200 application/json` with a full `AuthResponse` and a `qams_rt` cookie. **`AUTH-101` is not raised** — the age check reads `passwordPolicy.MaxAgeDays` (the global 90), never `tenant.Settings.PasswordExpiryDays` (`Login.cs:90-92`). The tenant setting has **no effect on any response**. |
| **Expected DB** | `saas.tenant.password_expiry_days = 1` is stored and readable, and changes nothing. `failed_login_attempts = 0`, `locked_until_utc IS NULL` after each success. |
| **Expected Audit** | Two `audit.security_event` rows, both `event_type='LOGIN_SUCCESS'` — **not** `LOGIN_FAILED`/`password-expired`. |
| **Expected Notification** | n/a — no password-expiry warning notification exists. |
| **Cleanup** | `UPDATE saas.tenant SET password_expiry_days = 90 WHERE identifier='demo-lab'; UPDATE qams.user_account SET password_changed_at_utc = NULL WHERE email='bva.analyst@demo-lab.local'; DELETE FROM qams.refresh_session WHERE user_id=<id>;` |
| **Evidence** | Both HTTP captures showing `200` · the tenant-setting SQL before and after · the step-5 grep output showing the property is referenced only at `src/NT.QAMS.Domain/Tenancy/TenantSettings.cs:10` (declaration, default 90) and `src/NT.QAMS.Infrastructure/Persistence/Configurations/TenantConfiguration.cs:36` (column mapping) — **no reader anywhere in the application layer** |
| **Result / Defect** | Not Run · — |
| **Notes** | **Acceptance criteria for `GAP-AUTH-903`, precise enough to implement against:** *(a)* decide whether password aging is a per-tenant or a platform setting and state it in the URS alongside URS-002; *(b)* if per-tenant, `LoginHandler` reads `tenant.Settings.PasswordExpiryDays` for tenant users and falls back to `PasswordPolicyOptions.MaxAgeDays` for platform admins — exactly the pattern already used for MFA at `Login.cs:44,66` — and a value of `0` disables aging for that tenant; *(c)* the setting is exposed on the tenant-settings surface as `require_mfa_privileged` is, or removed from `TenantSettings` and the column dropped, so no inert configuration remains; *(d)* an integration case proves a tenant with `password_expiry_days = 1` and a 5-day-old password receives `401 AUTH-101` while a sibling tenant at 90 receives `200`. Until then this case must **not** be written as passing. |

---

#### TC-AUTH-EP-001 — Email normalisation partitions at login: casing, surrounding whitespace and interior whitespace  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-001 · RSK-AUTH-001 |
| **Level / Type / Technique** | API · Functional (positive + negative) · Equivalence Partitioning — `command.Email.Trim().ToLowerInvariant()` at `Login.cs:69` |
| **Priority / Severity / Automation** | High · Major · Yes (functional, data-driven over the five partitions) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams`. Five requests — inside the 10/min `AuthPolicy` budget. |
| **Preconditions** | `bva.analyst@demo-lab.local` is active, unlocked, `failed_login_attempts = 0`, with password `Bva-Analyst-Pass-7!`. The stored `email` column value is the lower-case form (guaranteed by `UserAccount.cs:112`). |
| **Test Data** | P1 exact `"bva.analyst@demo-lab.local"` · P2 upper `"BVA.ANALYST@DEMO-LAB.LOCAL"` · P3 mixed `"Bva.Analyst@Demo-Lab.Local"` · P4 padded `"   bva.analyst@demo-lab.local   "` (three leading and three trailing spaces) · P5 interior space `"bva. analyst@demo-lab.local"`. All five with the **correct** password. |
| **Steps** | 1. `POST /api/auth/login` with P1. 2. …with P2. 3. …with P3. 4. …with P4. 5. …with P5. 6. `SELECT failed_login_attempts FROM qams.user_account WHERE email='bva.analyst@demo-lab.local';`. 7. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type, actor, detail FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 5;`. |
| **Expected UI** | P1–P4 sign in; P5 shows "Invalid credentials." The sign-in form does not itself trim or lower-case, so the raw string reaches the API. |
| **Expected API** | P1, P2, P3, P4: `200 application/json` with a full `AuthResponse` and a `qams_rt` cookie — normalisation makes all four the same identity. P5: `401 application/problem+json`, `code":"AUTH-001"` — `Trim()` removes only **leading and trailing** whitespace, so the interior space survives and no row matches. |
| **Expected DB** | Step 6: `failed_login_attempts = 0` — P5 never located a row, so `RegisterFailedLogin` was not called; **a typo with an interior space costs the user nothing toward lockout**. Four new `qams.refresh_session` rows (one per successful partition), all `revoked_at_utc IS NULL`, in four distinct `family_id` values. |
| **Expected Audit** | Four `LOGIN_SUCCESS` rows and one `LOGIN_FAILED`/`no-such-user` row. Every `actor` value is the **normalised** `bva.analyst@demo-lab.local` for P1–P4; the P5 row's `actor` is `bva. analyst@demo-lab.local` — the normalised-but-still-spaced string (`Login.cs:69` runs before `:74`), so the ledger preserves the typo. |
| **Expected Notification** | n/a — none defined for any login outcome. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = (SELECT id FROM qams.user_account WHERE email='bva.analyst@demo-lab.local');` |
| **Evidence** | Five HTTP captures · the four distinct `family_id` values · the five ordered security-event rows showing the actor strings |
| **Result / Defect** | Not Run · — |
| **Notes** | P4 is worth its own line in the report: the padded form succeeds at **login** but the same padded string at **registration** would be stored trimmed, so the two normalisation sites (`UserManagement.cs:56`, `UserAccount.cs:112`, `Login.cs:69`) must stay in agreement. There is no database constraint enforcing that agreement. |

---

#### TC-AUTH-EP-002 — Email field partitions at login: empty, whitespace-only, 320 and 321 characters  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-001 · RSK-AUTH-001 |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning with BVA on the length bound — `LoginValidator` `NotEmpty().MaximumLength(320)` (`Login.cs:25`) against column `email varchar(320)` |
| **Priority / Severity / Automation** | Medium · Major · Yes (functional, data-driven) |
| **Role / Permission / Tenant** | n/a — no identity is supplied · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | None beyond a running API; no account is touched. |
| **Test Data** | P1 `""` · P2 `"   "` (three spaces) · P3 a 320-character address `"<308 × 'a'>@demo-lab.local"` (308 + 1 + 11 = 320) · P4 a 321-character address `"<309 × 'a'>@demo-lab.local"`. All with `"tenantIdentifier":"demo-lab"` and `"password":"Any-Pass-1234!"`. |
| **Steps** | 1. `POST /api/auth/login` with P1. 2. …P2. 3. …P3. 4. …P4. 5. For each, record status, `code` extension presence and the `errors` object. 6. `SELECT set_config('app.bypass_rls','on',false); SELECT count(*) FROM audit.security_event WHERE occurred_at_utc > '<start>';`. |
| **Expected UI** | The sign-in form's own required-field validation blocks P1 and P2 client-side; P3 and P4 reach the API. |
| **Expected API** | P1 and P2: `400 application/problem+json`, `title":"Validation failed."`, an `errors` object keyed `"Email"` containing the `NotEmpty` message, and **no `code` extension** — the `ValidationException` branch (`DomainExceptionHandler.cs:34-44`) emits `errors` only. P2 fails because FluentValidation's `NotEmpty` treats a whitespace-only string as empty. P4: `400` with `errors["Email"]` containing the `MaximumLength(320)` message. P3: `401`, `code":"AUTH-001"` — 320 is **inside** the bound, so validation passes and the request proceeds to a user lookup that misses. |
| **Expected DB** | No rows created or modified anywhere. In particular P3 never reaches `qams.user_account` with an over-length value, so the `varchar(320)` bound is never tested at the database. |
| **Expected Audit** | Exactly **one** `audit.security_event` row across all four requests — from P3 only (`LOGIN_FAILED`/`no-such-user`). P1, P2 and P4 are rejected by `ValidationBehavior` **before** the handler runs (`Application/DependencyInjection.cs:24`), so no security event is written for a malformed credential submission. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | n/a — no state was created. |
| **Evidence** | Four HTTP captures with full bodies · the exact `errors` key names · the single-row audit count |
| **Result / Defect** | Not Run · — |
| **Notes** | The audit asymmetry is the reportable finding: a 321-character credential submission — a plausible fuzzing signature — leaves **no** trace in `audit.security_event`, while a 320-character one does. Folded into `GAP-AUTH-907`. Note also that the 400 body carries no `code`, so a client cannot branch on a machine-readable value for validation failures the way it can for every other error class. |

---

#### TC-AUTH-EP-003 — Password field partitions at login: empty, whitespace-only, and a sub-policy-strength value  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-001, URS-002 · RSK-AUTH-001 |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning — `LoginValidator` applies **`NotEmpty()` only** (`Login.cs:26`), not `StrongPassword()` |
| **Priority / Severity / Automation** | Medium · Major · Yes (functional, data-driven) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | `bva.analyst@demo-lab.local` active, unlocked, `failed_login_attempts = 0` |
| **Test Data** | P1 `""` · P2 `" "` (single space) · P3 `"abc"` (3 chars, one class — far below `PasswordRules.MinLength = 12`) · P4 a 201-character string of mixed classes (one above `PasswordRules.MaxLength = 200`). All with the analyst's correct email and `"tenantIdentifier":"demo-lab"`. |
| **Steps** | 1. `POST /api/auth/login` with P1. 2. …P2. 3. …P3. 4. …P4. 5. `SELECT failed_login_attempts FROM qams.user_account WHERE email='bva.analyst@demo-lab.local';`. |
| **Expected UI** | The form blocks P1 client-side; P2, P3 and P4 submit and return "Invalid credentials." |
| **Expected API** | P1: `400 application/problem+json`, `title":"Validation failed."`, `errors["Password"]` with the `NotEmpty` message, no `code`. P2, P3, P4: `401 application/problem+json`, `code":"AUTH-001"` — **login applies no strength rule whatsoever**; a 3-character password and a 201-character password are both simply wrong passwords. `StrongPassword()` is applied only at registration (`UserManagement.cs:44`), administrative reset (`:102`) and self-service change (`Login.cs:170`). |
| **Expected DB** | `failed_login_attempts = 3` after the sequence — P2, P3 and P4 each reach `hasher.Verify`, fail, and call `RegisterFailedLogin` (`Login.cs:82-85`); P1 does not, because validation rejects it first. The account is **not** locked (3 < 5). |
| **Expected Audit** | Three `audit.security_event` rows `event_type='LOGIN_FAILED'`, `detail='bad-password'` (for P2, P3, P4), and none for P1. Three `audit.field_change` rows on `FailedLoginAttempts`: `0→1`, `1→2`, `2→3`. **No credential value appears in any row** — the interceptor only writes the property it changed, and `Password` is not a persisted property of `UserAccount`. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='bva.analyst@demo-lab.local';` |
| **Evidence** | Four HTTP captures · post-condition SQL showing exactly 3 · the three security-event rows |
| **Result / Defect** | Not Run · — |
| **Notes** | P4 is a denial-of-service consideration, not a correctness one: the 200-character cap exists to bound PBKDF2 input (`PasswordRules.cs:19-20`) but is **not** applied at login, so an unauthenticated caller can force a hash over an arbitrarily long string within the 10/min budget. Recorded as an observation for batch D's rate-limit and performance work; not raised as a new AUTH gap here because the compensating budget exists. |

---

#### TC-AUTH-EP-004 — Tenant-identifier partitions at login: absent, blank, valid, unknown, malformed, over-length  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-001, URS-008 · RSK-AUTH-003 |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning over `TenantSlug.Create` (`src/NT.QAMS.Domain/Tenancy/TenantSlug.cs:22-37`) as invoked **uncaught** at `Login.cs:48`; implementation-derived because P5/P6 behaviour matches no requirement |
| **Priority / Severity / Automation** | High · Major · Yes (functional, data-driven over six partitions) |
| **Role / Permission / Tenant** | TenantAdmin credentials, various workspaces · n/a — anonymous endpoint · varies by partition |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams`. Six requests — inside the 10/min budget. |
| **Preconditions** | `demo-lab` exists and is `Active`; `no-such-lab` does not exist (verified). Credentials `admin@demo-lab.local` / `Demo-Admin-Pass-2!` are correct for `demo-lab`. |
| **Test Data** | P1 `null` (field absent) · P2 `"   "` (whitespace) · P3 `"demo-lab"` · P4 `"no-such-lab"` (well-formed, unregistered) · P5 `"NOT A SLUG"` (spaces and upper case — fails `^[a-z0-9](?:-?[a-z0-9]){1,49}$` after lower-casing) · P6 a 51-character all-lower-case slug (one over `TenantSlug.MaxLength = 50`). Email and password constant at the `demo-lab` admin's correct values. |
| **Steps** | 1–6. `POST /api/auth/login` once per partition. 7. Record status, `code` and `title` for each. 8. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type, tenant_id, actor, detail FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 6;`. |
| **Expected UI** | The SPA reaches the login form only via `/t/{slug}`, whose anonymous workspace lookup already 404s for P4, P5 and P6; P5's and P6's API behaviour is therefore reachable mainly by a direct API client. |
| **Expected API** | P1 and P2: `401`, `code":"AUTH-001"` — `string.IsNullOrWhiteSpace` sends both down the **platform-admin** path (`Login.cs:46`), where `admin@demo-lab.local` has no null-tenant row. P3: `200` with a full `AuthResponse`. P4: `401`, `code":"AUTH-001"`, `title":"Invalid credentials."`. **P5 and P6: `422 application/problem+json`, `code":"TENANT-002"`, `title` beginning "Tenant identifier must be 2-50 chars of lowercase letters, digits and single hyphens…"** — `TenantSlug.Create` throws before the `?? throw await FailAsync(...)` at `Login.cs:51` can run, and `LoginHandler` does **not** catch it (contrast `GetWorkspaceQuery`, which does catch and returns a uniform 404, `Application/Tenancy/Queries/GetWorkspace.cs:25-47`). |
| **Expected DB** | No change on any partition except P3, which adds one `qams.refresh_session` row. `failed_login_attempts` on `admin@demo-lab.local` stays 0 throughout — no partition reaches `RegisterFailedLogin`. |
| **Expected Audit** | Exactly **four** `audit.security_event` rows across the six requests: P1 and P2 → `LOGIN_FAILED`/`no-such-user`/`tenant_id IS NULL`; P3 → `LOGIN_SUCCESS`/tenant stamped; P4 → `LOGIN_FAILED`/`unknown-tenant`/`tenant_id IS NULL`. **P5 and P6 write nothing at all** — the exception escapes before any `security.WriteAsync` call. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = (SELECT id FROM qams.user_account WHERE email='admin@demo-lab.local');` |
| **Evidence** | Six HTTP captures with exact statuses and codes · the four-row audit SELECT · a `count(*)` proving P5 and P6 produced zero audit rows |
| **Result / Defect** | Not Run · — |
| **Notes** | P5/P6 are a **new finding**, raised as `GAP-AUTH-902`: a malformed workspace slug is distinguishable from an unregistered one by both status (`422` vs `401`) and code (`TENANT-002` vs `AUTH-001`), and it leaves no security-event trace. The anonymous workspace lookup deliberately collapses these two cases into an identical 404 for anti-enumeration; the login endpoint does not. **Acceptance criteria:** *(a)* `LoginHandler` wraps `TenantSlug.Create` so a malformed identifier takes the same path as an unknown one — `401 AUTH-001` plus a `LOGIN_FAILED` event with a distinguishing `detail` such as `malformed-tenant` for the ledger only; *(b)* a functional case proves P4, P5 and P6 return byte-identical bodies apart from `traceId`; *(c)* the same treatment is applied to `ChangePasswordHandler`, which calls `TenantSlug.Create` uncaught at `Login.cs:190`. |

---

#### TC-AUTH-EP-005 — Role-string partitions at user creation: valid casing, unknown value, PlatformAdmin, empty  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-005, URS-009 · RSK-AUTH-005 |
| **Level / Type / Technique** | API · Functional (positive + negative) · Equivalence Partitioning over `TenantRole.Parse` (`UserManagement.cs:14-27`) and `RegisterUserValidator` (`:37-46`) |
| **Priority / Severity / Automation** | High · Major · Yes (functional, data-driven) |
| **Role / Permission / Tenant** | TenantAdmin · `users.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | Caller holds `users.manage`. None of the P1–P6 email addresses exist yet in `demo-lab`. |
| **Test Data** | Six `POST /api/users` bodies, each with a distinct email `ep005-p<N>@demo-lab.local`, `displayName":"EP005 P<N>"`, `initialPassword":"Ep005-Fixture-Pass-4!"`, differing only in `role`: P1 `"Analyst"` · P2 `"analyst"` · P3 `"ANALYST"` · P4 `"Chemist"` · P5 `"PlatformAdmin"` · P6 `""` |
| **Steps** | 1–6. `POST /api/users` once per partition with the administrator's bearer token. 7. `SELECT email, role FROM qams.user_account WHERE email LIKE 'ep005-p%@demo-lab.local' ORDER BY email;`. 8. Record status, `code` and `title` for each. |
| **Expected UI** | The role selector on the create-user drawer offers only the five tenant tiers, so P4, P5 and P6 are reachable only by a direct API client. |
| **Expected API** | P1, P2, P3: `200 application/json`, body `{"id":"<uuid>"}` — `Enum.TryParse(..., ignoreCase: true)` (`UserManagement.cs:16`) accepts all three casings. P4: `422 application/problem+json`, `code":"USER-007"`, `title":"Unknown role 'Chemist'."` — the title echoes the **raw** input string. P5: `422`, `code":"USER-005"`, `title":"Platform administrator is not a tenant role."` (`UserManagement.cs:23`) — note this is the *handler's* message, distinct from the domain's `USER-005` message at `UserAccount.cs:129`, which the same code carries. P6: `400 application/problem+json`, `title":"Validation failed."`, `errors["Role"]` with the `NotEmpty` message, no `code` — `RegisterUserValidator` rejects it before the handler (`UserManagement.cs:43`). |
| **Expected DB** | Exactly **three** new rows (P1, P2, P3), all with `role='Analyst'` — the canonical enum name, not the submitted casing, because the value is round-tripped through the enum before `HasConversion<string>` writes it, and `CHECK ck_user_account_role_domain` admits only the six canonical names. Each row has a non-null `role_id` from `SeededRoleDefault.AssignAsync`. No row exists for P4, P5 or P6. |
| **Expected Audit** | Three `audit.field_change` rows `entity_type='UserAccount'`, `action='Created'`, `actor` = the administrator. Three `qams.outbox_event` rows of type `NT.QAMS.Domain.IdentityAccess.UserRoleAssigned, NT.QAMS.Domain`. Zero rows of any kind for P4, P5, P6 — each throws before `db.Users.Add`. |
| **Expected Notification** | n/a — no notification accompanies user creation. |
| **Cleanup** | `DELETE FROM qams.user_branch_access WHERE user_id IN (SELECT id FROM qams.user_account WHERE email LIKE 'ep005-p%@demo-lab.local'); DELETE FROM qams.user_department_access WHERE user_id IN (…); DELETE FROM qams.user_account WHERE email LIKE 'ep005-p%@demo-lab.local';` — direct SQL, because no API delete exists (TC-AUTH-API-021). |
| **Evidence** | Six HTTP captures with exact codes · the three-row SELECT showing canonical `'Analyst'` for all three casings · the three `Created` field-change rows |
| **Result / Defect** | Not Run · — |
| **Notes** | P5 is the API face of `GAP-AUTH-016`: the platform tier is unreachable through user administration by design, so the *only* platform-admin provisioning path is `PlatformAdmin:Email`/`PlatformAdmin:Password` startup seeding. Also record that `USER-005` is emitted with two different messages from two different layers — a client keying on the code alone cannot tell which guard fired. |

---

#### TC-AUTH-EP-006 — Email-format partitions diverge between registration and login  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-001, URS-009 · RSK-AUTH-005 |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning across two validators — `RegisterUserValidator` `.EmailAddress()` (`UserManagement.cs:41`) vs `LoginValidator`, which has **no** format rule (`Login.cs:25`), vs the domain's `email.Contains('@')` (`UserAccount.cs:89`) |
| **Priority / Severity / Automation** | Medium · Moderate · Yes (functional, data-driven) |
| **Role / Permission / Tenant** | TenantAdmin (for the registration arm) · `users.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | Caller holds `users.manage`. None of the test addresses exist. |
| **Test Data** | P1 `"not-an-email"` (no `@`) · P2 `"two@@at.local"` · P3 `"trailing@"` · P4 `"ep006-valid@demo-lab.local"`. Each submitted **twice**: once to `POST /api/users` with `displayName":"EP006"`, `role":"Analyst"`, `initialPassword":"Ep006-Fixture-Pass-6!"`, and once to `POST /api/auth/login` with `tenantIdentifier":"demo-lab"`, `password":"Any-Pass-1234!"`. |
| **Steps** | 1–4. `POST /api/users` once per partition. 5–8. `POST /api/auth/login` once per partition. 9. Record status, `code` and `errors` keys for all eight. 10. `SELECT count(*) FROM qams.user_account WHERE email LIKE '%ep006%' OR email IN ('not-an-email','two@@at.local','trailing@');`. |
| **Expected UI** | The create-user drawer's `type="email"` input blocks P1–P3 client-side; the sign-in form does not validate format at all. |
| **Expected API** | Registration arm — P1, P2, P3: `400 application/problem+json`, `title":"Validation failed."`, `errors["Email"]` with FluentValidation's `EmailAddress` message, **no `code`**. P4: `200` with `{"id":"<uuid>"}`. Login arm — P1, P2, P3, P4 **all**: `401 application/problem+json`, `code":"AUTH-001"` — login accepts any non-empty string ≤ 320 chars and simply fails to find a matching row. |
| **Expected DB** | Exactly one new row, for P4, with `email='ep006-valid@demo-lab.local'`. Step 10 returns 1. No malformed address is ever persisted — but note the **domain's** own guard is only `!email.Contains('@')` (`UserAccount.cs:89`), so `"two@@at.local"` and `"trailing@"` would both satisfy the aggregate; they are stopped by the HTTP validator alone. |
| **Expected Audit** | One `audit.field_change` `action='Created'` row (P4). Four `audit.security_event` rows from the login arm, all `LOGIN_FAILED`/`no-such-user`, with `actor` equal to the lower-cased submitted string including the malformed ones. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `DELETE FROM qams.user_account WHERE email='ep006-valid@demo-lab.local';` (direct SQL — no API delete). |
| **Evidence** | Eight HTTP captures · the step-10 count · the four security-event rows showing malformed actor strings persisted in the ledger |
| **Result / Defect** | Not Run · — |
| **Notes** | Two as-built observations for the report, neither a failure here: the domain guard is materially weaker than the HTTP validator, so any future non-HTTP writer (seeder, import) could persist `"two@@at.local"` — the same defence-in-depth shape as `GAP-AUTH-002`'s PIN rule; and `audit.security_event.actor` (`varchar(320)`, no CHECK) faithfully stores whatever malformed string was submitted, which is correct for forensics but means the column is attacker-controlled text and must be treated as such by any report that renders it. |

---

#### TC-AUTH-EP-007 — Initial-password partitions at registration: the four `StrongPassword` equivalence classes  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-002 · RSK-AUTH-001 |
| **Level / Type / Technique** | API · Functional (negative + positive) · Equivalence Partitioning over `PasswordRules.StrongPassword` (`src/NT.QAMS.Application/IdentityAccess/PasswordRules.cs:45-53`); the 11/12/13 and 199/200/201 length boundaries are batch A's `TC-AUTH-BVA-*` reservation and are **not** re-tested here |
| **Priority / Severity / Automation** | High · Major · Yes (functional, data-driven over five partitions) |
| **Role / Permission / Tenant** | TenantAdmin · `users.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | Caller holds `users.manage`. None of the P1–P5 addresses exist. |
| **Test Data** | Five `POST /api/users` bodies, emails `ep007-p<N>@demo-lab.local`, `displayName":"EP007 P<N>"`, `role":"Analyst"`, differing only in `initialPassword`: P1 `"Short-1!"` (8 chars — below `MinLength = 12`) · P2 `"alllowercase1!"` (14 chars, **no upper case**) · P3 `"NoDigitsHere!!"` (14 chars, **no digit**) · P4 `"Password123!"` — 12 chars, all four classes, but `"password123"` is **not** on the list while `"Password123!"` as a whole is not either, so use the literal compromised entry `"password123"` padded to satisfy nothing else: submit exactly `"password123"` (11 chars) to hit both the length and blocklist rules, and additionally P4b `"  password123  "` (15 chars with padding) to exercise the **trimmed** blocklist check at `PasswordRules.cs:77` against the **untrimmed** length check at `:47` · P5 `"Ep007-Valid-Pass-2!"` (19 chars, all four classes, not on the list) |
| **Steps** | 1–6. `POST /api/users` once per partition (P1, P2, P3, P4, P4b, P5). 7. Record status and the exact strings in `errors["InitialPassword"]` for each. 8. `SELECT email FROM qams.user_account WHERE email LIKE 'ep007-p%@demo-lab.local';`. |
| **Expected UI** | The create-user drawer shows the policy hint and surfaces each returned message under the password field; the drawer stays open on failure. |
| **Expected API** | P1: `400`, `errors["InitialPassword"]` contains `"The password must be at least 12 characters."` (`PasswordRules.cs:48`). P2: `400`, contains `"The password must include upper- and lower-case letters, a digit, and a symbol."` (`:51`). P3: `400`, same complexity message. P4: `400`, contains **both** the length message and `"This password is too common or appears in known breach lists. Choose another."` (`:53`) — FluentValidation reports every failing rule in the array. P4b: `400`, contains the **blocklist** message but **not** the length message — 15 characters passes `MinimumLength`, while `NotCompromised` trims before lookup (`:77`), so a padded common password is still rejected. P5: `200` with `{"id":"<uuid>"}`. All 400s carry `title":"Validation failed."` and **no `code` extension**. |
| **Expected DB** | Exactly one new row, for P5. Its `password_hash` is a PBKDF2 string that does not contain the plaintext as a substring; `password_changed_at_utc IS NULL`. |
| **Expected Audit** | One `audit.field_change` `action='Created'` row for P5. **Zero** rows for P1–P4b — the validator runs in `ValidationBehavior` before the handler, so nothing is written and, critically, no rejected password value reaches any ledger, log or response echo. Verify the latter explicitly: grep the P1–P4b response bodies for the submitted plaintext and expect no match. |
| **Expected Notification** | n/a — none defined. |
| **Cleanup** | `DELETE FROM qams.user_account WHERE email LIKE 'ep007-p%@demo-lab.local';` (direct SQL). |
| **Evidence** | Six HTTP captures with the verbatim `errors["InitialPassword"]` arrays · the single-row SELECT · the plaintext-echo grep results |
| **Result / Defect** | Not Run · — |
| **Notes** | P4b is the interesting partition: length is measured **untrimmed** and the blocklist is checked **trimmed**, so `"  password123  "` is long enough and still rejected — the two rules disagree about what the password is. That disagreement is benign in this direction (it rejects) but is the subject of exploratory charter EXPL-6, which probes the opposite direction. Note also that `HasComplexity` classifies anything that is not upper, lower or digit as a "symbol" (`PasswordRules.cs:69`), so a space satisfies the symbol class — do not author a case asserting that a space is rejected. |

---

#### TC-AUTH-EP-008 — Tenant-status partitions at login: Provisioning, Active, Suspended, Terminated  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-001, URS-008 · RSK-AUTH-003 |
| **Level / Type / Technique** | API · Functional (negative + positive) · Equivalence Partitioning over the complete `TenantStatus` domain — `Login.cs:60-63` tests `!= Active`, so the three non-Active values form **one** equivalence class; `CHECK ck_tenant_status_domain` bounds the set to four values (measured) |
| **Priority / Severity / Automation** | High · Major · Partially — no API path sets tenant status (see TC-AUTH-API-014); automate as an integration case with direct SQL setup |
| **Role / Permission / Tenant** | TenantAdmin of the subject tenant · n/a — anonymous endpoint · `iso-test-2` |
| **Environment** | API `:5080` Development + PostgreSQL `ntqams` |
| **Preconditions** | Tenant `iso-test-2` exists and is `Active` with at least one active user whose correct password is known. Record its current status for restoration. |
| **Test Data** | Four runs of the same correct-credential login body, each preceded by `UPDATE saas.tenant SET status='<S>' WHERE identifier='iso-test-2';` for `S` in `Provisioning`, `Active`, `Suspended`, `Terminated` |
| **Steps** | 1. Set `status='Provisioning'`; `POST /api/auth/login` with correct credentials. 2. Set `status='Active'`; repeat. 3. Set `status='Suspended'`; repeat. 4. Set `status='Terminated'`; repeat. 5. Attempt `UPDATE saas.tenant SET status='Dormant' WHERE identifier='iso-test-2';` and record the rejection. 6. `SELECT set_config('app.bypass_rls','on',false); SELECT event_type, tenant_id, detail FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 4;`. |
| **Expected UI** | For Provisioning, Suspended and Terminated the sign-in page shows "This tenant is not active." — the identical message for all three; the user cannot tell a lab still being set up from one that has been shut down. |
| **Expected API** | Steps 1, 3, 4: `401 application/problem+json`, `code":"AUTH-002"`, `title":"This tenant is not active."` — **identical** responses for all three non-Active values; the handler does not differentiate. Step 2: `200` with a full `AuthResponse` and a `qams_rt` cookie. Step 5: PostgreSQL rejects the `UPDATE` with a check-constraint violation on `ck_tenant_status_domain`, confirming the partition set is closed at four. |
| **Expected DB** | Only step 2 creates a `qams.refresh_session` row. The subject user's `failed_login_attempts` stays 0 across all four runs — every non-Active run throws at `Login.cs:62`, before the user is loaded at `:70`. |
| **Expected Audit** | Four `audit.security_event` rows: three `LOGIN_FAILED`/`detail='tenant-inactive'` and one `LOGIN_SUCCESS`. All four carry `tenant_id` = the `iso-test-2` uuid — even the failures, because `tenantScope.Set` runs at `Login.cs:58` before the status test. The `detail` value is the same string for all three refused statuses, so the ledger **cannot distinguish Provisioning from Suspended from Terminated** either. |
| **Expected Notification** | n/a — none defined for a tenant-state login refusal. |
| **Cleanup** | `UPDATE saas.tenant SET status='Active', suspension_reason=NULL WHERE identifier='iso-test-2'; DELETE FROM qams.refresh_session WHERE user_id=<subject id>;` then re-confirm a successful login. |
| **Evidence** | Four HTTP captures · the rejected step-5 `UPDATE` with its PostgreSQL error text · the four ordered audit rows · before/after `saas.tenant.status` |
| **Result / Defect** | Not Run · — |
| **Notes** | The reportable observation is the collapse of three distinct tenant lifecycle states into one indistinguishable outcome in both the response **and** the ledger. Toward an end user that is arguably correct (do not leak commercial state); toward a compliance reviewer it is a gap — "why could this laboratory not sign in on that date?" is not answerable from `audit.security_event` alone. Folded into `GAP-AUTH-907`. Do **not** author a case expecting distinct codes per status: `Login.cs:60` tests only `!= Active`. |

---

## Batch coverage note

**Covered.** 33 complete cases, all `Result / Defect = Not Run · —`, consuming `TC-AUTH-API-001` … `-025` and `TC-AUTH-EP-001` … `-008`. Login success on the tenant path (API-001) and the platform null-tenant path (API-002). The lockout mechanism end to end: first failure (API-003), BVA at 4 / 5 / 6 attempts (API-004, -005, -006) including the proof that an already-locked account neither increments nor extends and that a **correct** password is refused identically to a wrong one, and BVA at lock expiry T−1s / T / T+1s (API-007, -008, -009) against the strict `until > now` comparison. Counter reset on success, proven by a post-reset failure landing at 1 (API-010). Login against unknown (API-011), deactivated (API-012), unknown-tenant (API-013), suspended-tenant (API-014) and wrong-tenant (API-015) identities, with the tenant-stamping asymmetry between `AUTH-001`/null-tenant and `AUTH-002`/stamped proven by direct RLS reads. Account creation through to first sign-in (API-016), duplicate rejection (API-017), deactivation with its un-revoked sessions (API-018), the reactivate-while-locked trap (API-019), mid-session revocation via `AUTH-006` (API-020), the absent deletion path (API-021) and reset-password as the sole unlock (API-022). The full seven-row `audit.security_event` sequence with append-only trigger proof (API-023) and the `audit.field_change` pair for a lockout write including `«redacted»` credential handling and platform-admin null-tenant attribution (API-024). Equivalence partitions for email normalisation (EP-001), email validation bounds (EP-002), password field handling at login (EP-003), the six tenant-identifier partitions (EP-004), role strings at creation (EP-005), the registration-vs-login format divergence (EP-006), the `StrongPassword` classes (EP-007) and the four `TenantStatus` values (EP-008).

**Could not cover, and why.** *(1)* The true `locked_until_utc == now` equality point (API-008) is unreachable over HTTP — `IClock.UtcNow` is the system clock and no test clock is injectable through the API; the case demonstrates the open boundary from the T-or-later side and defers exact equality to a `UserAccount.IsLockedOut` unit case under the `TC-AUTH-UNIT-*` reservation. *(2)* Tenant status cannot be changed through the approved API surface — `Tenant.Suspend` exists at `src/NT.QAMS.Domain/Tenancy/Tenant.cs:58-73` but only `GET`/`POST /api/tenants` are exposed — so API-014 and EP-008 use direct SQL preconditions and are integration-level, not pure API. *(3)* Account deletion has no implementation, so API-021 is `[GD]` with acceptance criteria rather than an executable positive case. *(4)* Per-tenant password expiry has no reader, so API-025 is `[GD]`. *(5)* Password-length boundaries 11/12/13 and 199/200/201 and PIN boundaries are deliberately **not** consumed here — they belong to the `TC-AUTH-BVA-001` … `-025` reservation, which this batch does not own. *(6)* Rate-limit partition behaviour, cookie hardening, refresh reuse detection and anti-enumeration timing are named only as execution constraints; they are batch D's slice.

**ID-reservation conflict to resolve.** The front matter's reservation table assigns `TC-AUTH-API-001` … `-070` to `10-module-auth-cases-B.md` and `TC-AUTH-EP-001` … `-015` to this file. This batch was directed to consume `TC-AUTH-API-001..` and `TC-AUTH-EP-001..`, and has done so, taking API-001 … -025 and EP-001 … -008. **Batch B must therefore start at `TC-AUTH-API-026`**, and the front matter's reservation table needs that one-line amendment before the traceability matrix is built. Flagged rather than silently reconciled.

**New gaps found in this slice** (numbered `GAP-AUTH-9xx` to avoid colliding with the front matter's `GAP-AUTH-001` … `-016`):

- **`GAP-AUTH-901` — No account de-provisioning path.** `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` exposes nine `/api/users` routes, none of them DELETE, and `qams.user_account` carries no deletion marker column (21 columns, measured). URS-009 names "create, assign role, reset password, deactivate" and is silent on erasure, so the code may be correct and the requirement incomplete. Full acceptance criteria are in TC-AUTH-API-021's Notes. **Severity: Major** (an unrecorded lifecycle decision on a Part-11 identity store). Responsible: Quality Manager (requirement) + Lead Developer.
- **`GAP-AUTH-902` — A malformed tenant slug at login escapes as `422 TENANT-002` with no security event.** `LoginHandler` calls `TenantSlug.Create` uncaught at `Login.cs:48`; `GetWorkspaceQuery` catches the same exception and collapses it into a uniform 404 (`Application/Tenancy/Queries/GetWorkspace.cs:25-47`). A malformed identifier is therefore distinguishable from an unregistered one by status **and** code, and leaves no ledger trace. `ChangePasswordHandler` has the identical uncaught call at `Login.cs:190`. Acceptance criteria in TC-AUTH-EP-004's Notes. **Severity: Moderate.** Responsible: Lead Developer.
- **`GAP-AUTH-903` — `TenantSettings.PasswordExpiryDays` is persisted and inert.** Column `saas.tenant.password_expiry_days` (NOT NULL, default 90) is declared at `src/NT.QAMS.Domain/Tenancy/TenantSettings.cs:10` and mapped at `src/NT.QAMS.Infrastructure/Persistence/Configurations/TenantConfiguration.cs:36`, and **no application code reads it** (grep of `src/` excluding migrations returns exactly those two hits). `LoginHandler` uses the global `PasswordPolicyOptions.MaxAgeDays` for every tenant. A laboratory can set its own expiry to 1 day and nothing changes. Acceptance criteria in TC-AUTH-API-025's Notes. **Severity: Major** (a tenant-visible compliance control that does nothing). Responsible: Lead Developer + Quality Manager.
- **`GAP-AUTH-904` — A lapsed lockout is not cleared from the row.** `IsLockedOut` is derived (`UserAccount.cs:163`); `locked_until_utc` keeps its past value until the next *successful* authentication calls `RegisterSuccessfulLogin`. Any query, report or access-review that counts "locked accounts" as `locked_until_utc IS NOT NULL` over-counts indefinitely, and there is no sweep that clears it (`ScheduledSweepService` proposes grace-lockout transitions for other domains, not this column). **Suggested criteria:** either the read model computes `locked_until_utc > now()` everywhere, or a sweep nulls lapsed values with an audited write. **Severity: Minor.** Responsible: Lead Developer.
- **`GAP-AUTH-905` — The ledger cannot distinguish a disabled account from a nonexistent one.** `Login.cs:72` merges `user is null` and `!user.IsActive` into one branch and writes `detail='no-such-user'` for both. Wire-level indistinguishability is the intended anti-enumeration property; carrying it into `audit.security_event` defeats Part 11 §11.300(d) reporting, which needs to see probing against a known ex-employee's disabled account. **Suggested criteria:** keep the response identical and split the ledger `detail` into `no-such-user` and `inactive-user`; a functional case proves the two responses stay byte-identical apart from `traceId` while the two ledger rows differ. **Severity: Moderate.** Responsible: Security Owner + Lead Developer.
- **`GAP-AUTH-906` — Cross-tenant credential probing is invisible to the victim tenant and un-throttled by the lockout.** Attacking `admin@demo-lab.local` through the `iso-test-2` slug produces `LOGIN_FAILED`/`no-such-user` rows stamped with the **probed** tenant, never the account's own, and never touches `failed_login_attempts` (TC-AUTH-API-015). `demo-lab`'s compliance view shows nothing. The only control is the 10/min per-IP budget. **Suggested criteria:** decide whether a failed login should be attributed to the tenant that owns the email as well as the tenant that was probed; at minimum, document that a tenant's security-event view is not a complete record of attacks against its users. **Severity: Moderate.** Responsible: Security Owner.
- **`GAP-AUTH-907` — Security-event coverage has four holes on the paths this batch exercised.** Consolidated from API-020, -022, -023, EP-002 and EP-008: *(a)* an `AUTH-006` session denial by `ActiveSessionMiddleware` writes **no** security event, although URS-016 names "session/privilege changes"; *(b)* an **administrative** password reset writes none, while a self-service change writes `PASSWORD_CHANGED` — the more privileged action is the less audited one; *(c)* a request rejected by `ValidationBehavior` (empty or over-length credentials) writes none, so a malformed-credential fuzzing burst is invisible; *(d)* the lockout-triggering failure and the locked-out refusal share `event_type='LOGIN_FAILED'` and are separable only by the free-text `detail` column (`varchar(500)`, no CHECK), and three distinct tenant lifecycle states collapse into one `detail='tenant-inactive'`. **Suggested criteria:** enumerate the required security-event types against URS-016 in the URS itself; add `event_type` values (or a bounded `detail` vocabulary with a CHECK) so monitoring rules key on a closed set; write an event for administrative password reset and for session revocation. **Severity: Major** (URS-016 is only partially discharged on the paths audited here). Responsible: Security Owner + Quality Manager.

**Corrections to the front matter carried forward, not re-derived.** §0.1 (locked accounts read `failed_login_attempts = 0`; the column is `failed_login_attempts`) is applied in every relevant precondition and assertion above. §0.4's RLS-exception set is relied on in TC-AUTH-API-018's cleanup. No claim in this batch contradicts the front matter; where this batch goes beyond it — the uncaught `TenantSlug.Create`, the inert `password_expiry_days`, the lapsed-lock residue and the four security-event holes — the finding is registered as a new `9xx` gap rather than folded silently into an existing one.
