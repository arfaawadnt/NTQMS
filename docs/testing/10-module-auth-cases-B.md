# AUTH — Detailed Test Cases, Batch B

This batch authors the **multi-factor-authentication slice end to end** and consumes `TC-AUTH-DT-001…014` and `TC-AUTH-API-030…053` out of the module's reservation in `10-module-auth.md`. It covers: the login decision-table rows in which MFA participates (§4.1 R8–R12) and the two policy sources that drive them — the per-tenant `saas.tenant.require_mfa_privileged` column and the platform-admin fallback key `Security:RequireMfaForPrivilegedRoles`; the enrolment-scoped JWT (`scope=mfa_enrollment`, `SecurityAdapters.cs:76-78,89`) and the `MfaEnrollmentGateMiddleware` allow-list (§4.4 R4–R6); `POST /api/auth/mfa/enroll` and `POST /api/auth/mfa/confirm` including RFC 6238 valid / invalid / clock-skew-window / replay behaviour and the 10-per-minute credential budget that fronts them; and the tenant-settings MFA policy endpoints `GET|PUT /api/tenant-settings/mfa-policy` with their authorization, tenant containment and audit consequences. It deliberately leaves to sibling batches: all non-MFA login rows and the password/lockout machinery (**A**), the refresh/rotation and access-review state machines (**C**), the wider rate-limit, cookie-hardening, reuse-detection and RLS surface (**D**), the secret-lifecycle data-flow and MC/DC decomposition of `Login.cs:200-204` (**E**), and the browser E2E, UAT, a11y and performance work (**F**). Every claim below was read in the cited file at the cited line; the e-signature PIN is **not** in this batch (it belongs with the signature ceremony in **D**), and `MFA-001` / `MFA-002` are deliberately absent because the front matter proves them unreachable over HTTP (`10-module-auth.md` §1.5, reachability caveats).

**Standing conventions for every case below.** Risk IDs are **minted here** — `docs/validation/02-Functional-Risk-Assessment.md` carries only area-level rows (`Authentication hardening`, URS-001/002/003/004/007) with no per-item identifiers, so this batch mints `RSK-AUTH-004…008` and says so. Two environments recur:

- **ENV-A** — API `:5080` Development, default configuration (`Security:RequireMfaForPrivilegedRoles` absent → `false`, `PasswordPolicyOptions.cs:17`), live PostgreSQL `ntqams`.
- **ENV-B** — the same, **restarted** with `Security__RequireMfaForPrivilegedRoles=true`. The restart is mandatory: `SecurityOptions` is a singleton bound once at composition (`src/NT.QAMS.Infrastructure/DependencyInjection.cs:79-80`).

The deterministic TOTP seed used throughout is the Base32 string **`JBSWY3DPEHPK3PXP`**, written directly into `qams.user_account.mfa_secret` by `psql` (that table has no RLS — measured, `10-module-auth.md` §0.4 — so no GUC is needed). `C(k)` means the 6-digit code for counter `floor(unixEpochSeconds / 30) + k`, computed with any RFC 6238 HMAC-SHA1 / 30 s / 6-digit generator; the in-repo reference implementation is `TotpService.Compute` (`src/NT.QAMS.Infrastructure/Security/TotpService.cs:53-72`). Audit reads use `SELECT set_config('app.bypass_rls','on',false);` first (`audit.*` rows are RLS-hidden in `psql`).

---

## 1. Decision-table cases — MFA rows of the login table and the scope gate

#### TC-AUTH-DT-001 — MFA-enabled account posts no code: 200 with an empty token and no cookie (§4.1 R8)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-004 (minted here) |
| **Level / Type / Technique** | API · Functional (positive-intermediate) · Decision Table — §4.1 rule R8 (`C9=Y`, `C10=N`) |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — `POST /api/auth/login` is `[AllowAnonymous]` (`AuthController.cs:29-30`) · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `analyst-mfa@demo-lab.local` exists, `is_active=true`, `locked_until_utc IS NULL`, `failed_login_attempts=0`, `mfa_secret='JBSWY3DPEHPK3PXP'`, `mfa_enabled=true`, `password_changed_at_utc IS NULL` (so the `C8` age check is skipped, `Login.cs:90-92`). |
| **Test Data** | `{"tenantIdentifier":"demo-lab","email":"analyst-mfa@demo-lab.local","password":"Analyst-Mfa-Pass-1!","mfaCode":null}` |
| **Steps** | 1. `POST /api/auth/login` with the body above via `curl.exe -i`. 2. Read status, all `Set-Cookie` headers and the JSON body. 3. `SELECT failed_login_attempts FROM qams.user_account WHERE email='analyst-mfa@demo-lab.local';`. 4. `SELECT count(*) FROM qams.refresh_session rs JOIN qams.user_account u ON u.id=rs.user_id WHERE u.email='analyst-mfa@demo-lab.local';`. |
| **Expected UI** | `login.component.ts:466-468` sets `mfaRequired` true; the form reveals the input `id="mfa"` with `inputmode="numeric"` and the `login.mfaPrompt` hint; the user stays on `/login`. |
| **Expected API** | `200` `application/json`; `accessToken` is the **empty string**; `expiresAtUtc` is the CLR default `0001-01-01T00:00:00+00:00`; `mfaRequired: true`; `mfaEnrollmentRequired: false`; `role: "Analyst"`. **No `Set-Cookie: qams_rt`** header at all (`Login.cs:104-106` returns `Refresh: null`, so `AuthController.cs:90` never appends). |
| **Expected DB** | `failed_login_attempts` still `0` (this branch calls neither `RegisterFailedLogin` nor `RegisterSuccessfulLogin`); refresh-session count for that user unchanged from the pre-step value. |
| **Expected Audit** | Exactly one new `audit.security_event` row: `event_type='LOGIN_MFA_REQUIRED'`, `tenant_id` = the `demo-lab` id, `actor='analyst-mfa@demo-lab.local'`, `detail IS NULL` (`Login.cs:103`), `ip_address IS NULL` (`GAP-AUTH-005`). No `audit.field_change` row. |
| **Expected Notification** | n/a — no notification is defined for an MFA challenge. |
| **Cleanup** | `DELETE FROM audit.security_event` is impossible (append-only trigger); leave the row. No state to reset. |
| **Evidence** | `curl -i` capture including the full header block · SQL result set · security-event row |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert the **absence** of `Set-Cookie` explicitly — a passing status code alone would not distinguish R8 from R10. |

#### TC-AUTH-DT-002 — MFA-enabled account posts a wrong code: 401 AUTH-005 and the lockout counter advances (§4.1 R9)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004, URS-003 · RSK-AUTH-004 |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — §4.1 rule R9 (`C11=N`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | As TC-AUTH-DT-001, and `failed_login_attempts = 0` immediately before step 1. |
| **Test Data** | password `Analyst-Mfa-Pass-1!` (correct), `mfaCode` = `"000000"` — verified beforehand not to equal `C(-1)`, `C(0)` or `C(+1)`; if it does, use `"111111"`. |
| **Steps** | 1. `POST /api/auth/login` with the correct password and `mfaCode:"000000"`. 2. Read status and the `code` extension of the problem body. 3. `SELECT failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='analyst-mfa@demo-lab.local';`. 4. Read the newest `audit.security_event` row. |
| **Expected UI** | The MFA input stays visible; the login component renders the problem `title` — the generic `"Invalid credentials."`, which does **not** disclose that the password was correct. |
| **Expected API** | `401` `application/problem+json`; `code` = **`AUTH-005`**; `title` = `"Invalid credentials."` (`Login.cs:113`, message constant `Login.cs:37`); `traceId` present (`ProblemResponse`). |
| **Expected DB** | `failed_login_attempts` = `1` (was 0) — `RegisterFailedLogin` + `SaveChangesAsync` at `Login.cs:111-112`; `locked_until_utc` still `NULL`. |
| **Expected Audit** | One `audit.security_event` row: `event_type='LOGIN_FAILED'`, `detail='bad-mfa'`, `tenant_id` = `demo-lab`, `actor='analyst-mfa@demo-lab.local'`. One `audit.field_change` row: `entity_type='UserAccount'`, `property='FailedLoginAttempts'`, `old_value='0'`, `new_value='1'`, `actor='system'` (no authenticated actor on this path, `FieldChangeInterceptor.cs:114`). |
| **Expected Notification** | n/a — no notification is defined for a failed MFA attempt. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='analyst-mfa@demo-lab.local';` |
| **Evidence** | HTTP response capture · SQL before/after of `failed_login_attempts` · both ledger rows |
| **Result / Defect** | Not Run · — |
| **Notes** | The code is `AUTH-005` but the message is deliberately identical to `AUTH-001`'s. Assert the **code extension**, never the title, to tell the two apart. |

#### TC-AUTH-DT-003 — MFA-enabled account posts the current code: full session issued (§4.1 R10)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004, URS-001 · RSK-AUTH-004 |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — §4.1 rule R10 (`C11=Y`, `C12=N`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | As TC-AUTH-DT-001; `failed_login_attempts` deliberately pre-set to `3` so the reset is observable. |
| **Test Data** | password `Analyst-Mfa-Pass-1!`, `mfaCode` = `C(0)` computed at the instant of the call from `JBSWY3DPEHPK3PXP`. |
| **Steps** | 1. Compute `C(0)`. 2. `POST /api/auth/login` with password + that code, within the same 30-second step. 3. Read status, `Set-Cookie`, body. 4. Base64url-decode the JWT payload and read `scope`, `sub`, `tenant_id`, `role`. 5. `SELECT failed_login_attempts FROM qams.user_account WHERE email='analyst-mfa@demo-lab.local';`. 6. `SELECT id, family_id, revoked_at_utc, replaced_by_id, expires_at_utc, token_hash FROM qams.refresh_session ORDER BY expires_at_utc DESC LIMIT 1;`. |
| **Expected UI** | `auth.service.ts:70-72` stores the token in memory and the router lands on the dashboard shell. |
| **Expected API** | `200`; `accessToken` non-empty; `mfaRequired: false`; `mfaEnrollmentRequired: false`; `expiresAtUtc` ≈ now + 15 min (`JwtOptions.ExpiryMinutes = 15`, `SecurityAdapters.cs:37`). Header `Set-Cookie: qams_rt=…; expires=…; path=/api/auth; secure; samesite=strict; httponly` (`AuthController.cs:92-100`). JWT payload carries `"scope":"full"` (`SecurityAdapters.cs:77,89`) and `tenant_id` = the `demo-lab` id. |
| **Expected DB** | `failed_login_attempts` = `0` (reset by `RegisterSuccessfulLogin`, `UserAccount.cs:220-224`); exactly one new `qams.refresh_session` row with `revoked_at_utc IS NULL`, `replaced_by_id IS NULL`, `token_hash` matching `^[0-9A-F]{64}$` (CHECK `ck_refresh_session_token_hash_sha256`), `expires_at_utc` ≈ now + 14 days (`Auth:RefreshTokenDays` default 14). |
| **Expected Audit** | One `audit.security_event` row `event_type='LOGIN_SUCCESS'`, `detail IS NULL`, `tenant_id` = `demo-lab`. `audit.field_change` shows `entity_type='RefreshSession'` `action='Created'` and `entity_type='UserAccount'` `property='FailedLoginAttempts'` `old_value='3'` `new_value='0'`. |
| **Expected Notification** | n/a — no notification is defined for a successful sign-in. |
| **Cleanup** | `POST /api/auth/logout` with the cookie, then `UPDATE qams.user_account SET failed_login_attempts=0 WHERE email='analyst-mfa@demo-lab.local';` |
| **Evidence** | HTTP capture · decoded JWT payload (redact the signature) · `qams.refresh_session` row · security-event row |
| **Result / Defect** | Not Run · — |
| **Notes** | Over plain HTTP on `:5080` a browser will refuse to store a `Secure` cookie; run this with `curl.exe`, which still exposes the header. Compute and send `C(0)` inside one 30-second step or the case silently becomes TC-AUTH-DT-002. |

#### TC-AUTH-DT-004 — Tenant policy ON + TenantAdmin without MFA: enrolment-scoped session, no refresh cookie (§4.1 R11)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-004 |
| **Level / Type / Technique** | API · Functional (positive-intermediate) · Decision Table — §4.1 rule R11 (`C9=N`, `C12=Y`) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | ENV-A (the tenant column, not the config key, drives this rule for a tenant user) |
| **Preconditions** | `UPDATE saas.tenant SET require_mfa_privileged=true WHERE identifier='demo-lab';` · `admin@demo-lab.local` has `role='TenantAdmin'`, `mfa_enabled=false`, `mfa_secret IS NULL`, `is_active=true`, `locked_until_utc IS NULL`. |
| **Test Data** | `{"tenantIdentifier":"demo-lab","email":"admin@demo-lab.local","password":"Demo-Admin-Pass-2!","mfaCode":null}` |
| **Steps** | 1. Apply the precondition UPDATE. 2. `POST /api/auth/login` with the body above. 3. Read status, `Set-Cookie`, body. 4. Decode the JWT payload and read `scope`. 5. `SELECT count(*) FROM qams.refresh_session rs JOIN qams.user_account u ON u.id=rs.user_id WHERE u.email='admin@demo-lab.local' AND rs.revoked_at_utc IS NULL;` before and after. |
| **Expected UI** | `login.component.ts:471-473` navigates to the standalone route `/security/mfa-setup` (`app.routes.ts:18-20`, `canActivate: [authGuard]`), which sits outside the shell. |
| **Expected API** | `200`; `accessToken` **non-empty**; `mfaRequired: false`; `mfaEnrollmentRequired: **true**`; `role: "TenantAdmin"`. **No `Set-Cookie: qams_rt`** (`Login.cs:128-136` mints no refresh grant). JWT payload contains `"scope":"mfa_enrollment"` (`SecurityAdapters.cs:76,89`). |
| **Expected DB** | Live refresh-session count for that user **unchanged**; `failed_login_attempts` = 0 (reset at `Login.cs:124`); `mfa_secret` still `NULL`, `mfa_enabled` still `false`. |
| **Expected Audit** | One `audit.security_event` row `event_type='LOGIN_MFA_ENROLL_REQUIRED'`, `tenant_id` = `demo-lab`, `actor='admin@demo-lab.local'`, `detail IS NULL` (`Login.cs:139`). |
| **Expected Notification** | n/a — no notification is defined for the enrolment gate. |
| **Cleanup** | `UPDATE saas.tenant SET require_mfa_privileged=false WHERE identifier='demo-lab';` |
| **Evidence** | HTTP capture · decoded JWT `scope` claim · session-count query · security-event row |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the only login outcome that returns a usable token with **no** refresh cookie — the enrolment session must not outlive the 15-minute access token. `GAP-AUTH-001` records that the gate covers only `PlatformAdmin`/`TenantAdmin`. |

#### TC-AUTH-DT-005 — Tenant policy ON + Analyst without MFA: full session, gate does not apply (§4.1 R12)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-004 |
| **Level / Type / Technique** | API · Functional (negative-of-control) · Decision Table — §4.1 R12; the role predicate of `C12` |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `UPDATE saas.tenant SET require_mfa_privileged=true WHERE identifier='demo-lab';` · `analyst-nomfa@demo-lab.local` exists with `role='Analyst'`, `mfa_enabled=false`, `mfa_secret IS NULL`. |
| **Test Data** | `{"tenantIdentifier":"demo-lab","email":"analyst-nomfa@demo-lab.local","password":"Analyst-Nomfa-Pass-1!","mfaCode":null}` |
| **Steps** | 1. Apply the precondition UPDATE. 2. `POST /api/auth/login`. 3. Read `mfaEnrollmentRequired` and the `Set-Cookie` header. 4. Decode the JWT `scope`. 5. With that bearer token call `GET /api/auth/me/privileges`. |
| **Expected UI** | The user lands on the dashboard shell; the MFA-setup route is never entered. |
| **Expected API** | Step 2: `200`, `mfaEnrollmentRequired: **false**`, `mfaRequired: false`, `Set-Cookie: qams_rt=…` **present**, JWT `"scope":"full"`. Step 5: `200` (a full session reaches `/api/auth/me/privileges`, which the enrolment gate would have blocked). |
| **Expected DB** | One new `qams.refresh_session` row, `revoked_at_utc IS NULL`. |
| **Expected Audit** | One `audit.security_event` row `event_type='LOGIN_SUCCESS'` — **not** `LOGIN_MFA_ENROLL_REQUIRED`. |
| **Expected Notification** | n/a — no notification is defined for a successful sign-in. |
| **Cleanup** | `POST /api/auth/logout`; `UPDATE saas.tenant SET require_mfa_privileged=false WHERE identifier='demo-lab';` |
| **Evidence** | HTTP captures for steps 2 and 5 · decoded `scope` claim · security-event row |
| **Result / Defect** | Not Run · — |
| **Notes** | This case **documents** the divergence D-1 / `GAP-AUTH-001`: an `Analyst` who signs regulated records is never required to enrol, even with the tenant switch on. It is authored against the as-built rule, not the brief. |

#### TC-AUTH-DT-006 — Tenant policy OFF + TenantAdmin without MFA: full session  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-004 |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — §4.1 R12 with `C12=N` via the policy condition |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `SELECT require_mfa_privileged FROM saas.tenant WHERE identifier='demo-lab';` returns `f` (the migration default, `20260726132544_TenantMfaPolicy.cs:14`) · `admin@demo-lab.local` has `mfa_enabled=false`. |
| **Test Data** | `{"tenantIdentifier":"demo-lab","email":"admin@demo-lab.local","password":"Demo-Admin-Pass-2!","mfaCode":null}` |
| **Steps** | 1. Verify the precondition SELECT returns `f`. 2. `POST /api/auth/login`. 3. Read `mfaEnrollmentRequired`, `Set-Cookie`, and the decoded JWT `scope`. |
| **Expected UI** | Dashboard shell; no navigation to `/security/mfa-setup`. |
| **Expected API** | `200`, `mfaEnrollmentRequired: false`, `Set-Cookie: qams_rt=…` present, JWT `"scope":"full"`. |
| **Expected DB** | One new `qams.refresh_session` row; `saas.tenant.require_mfa_privileged` still `false`. |
| **Expected Audit** | `audit.security_event` row `event_type='LOGIN_SUCCESS'`. |
| **Expected Notification** | n/a — no notification is defined for a successful sign-in. |
| **Cleanup** | `POST /api/auth/logout`. |
| **Evidence** | HTTP capture · precondition SELECT output · decoded `scope` |
| **Result / Defect** | Not Run · — |
| **Notes** | The default-OFF baseline. Pair this with TC-AUTH-DT-004 — same account, same credentials, one column flipped — so the column is proven to be the sole cause. |

#### TC-AUTH-DT-007 — Tenant policy ON + TenantAdmin **with** MFA already enabled: full session after the code  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-004 |
| **Level / Type / Technique** | API · Functional (positive) · Multiple-Condition Coverage — the three-term conjunction at `Login.cs:120-122` with `!user.MfaEnabled` false |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `UPDATE saas.tenant SET require_mfa_privileged=true WHERE identifier='demo-lab';` · `UPDATE qams.user_account SET mfa_secret='JBSWY3DPEHPK3PXP', mfa_enabled=true WHERE email='admin@demo-lab.local';` |
| **Test Data** | password `Demo-Admin-Pass-2!`, `mfaCode` = `C(0)`. |
| **Steps** | 1. Apply both precondition UPDATEs. 2. Compute `C(0)`. 3. `POST /api/auth/login` with password + code. 4. Read `mfaEnrollmentRequired`, `Set-Cookie`, JWT `scope`. |
| **Expected UI** | Dashboard shell. |
| **Expected API** | `200`, `mfaRequired: false`, `mfaEnrollmentRequired: **false**`, `Set-Cookie: qams_rt=…` present, JWT `"scope":"full"` — `mustEnrollMfa` is false because `!user.MfaEnabled` evaluates false even though the policy and the role terms are both true. |
| **Expected DB** | One new `qams.refresh_session` row; `mfa_enabled` still `true`. |
| **Expected Audit** | `audit.security_event` row `event_type='LOGIN_SUCCESS'`. |
| **Expected Notification** | n/a — no notification is defined for a successful sign-in. |
| **Cleanup** | `POST /api/auth/logout`; `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false WHERE email='admin@demo-lab.local';` `UPDATE saas.tenant SET require_mfa_privileged=false WHERE identifier='demo-lab';` |
| **Evidence** | HTTP capture · decoded `scope` claim · pre/post `mfa_enabled` |
| **Result / Defect** | Not Run · — |
| **Notes** | Together with DT-004, DT-005 and DT-006 this gives all three single-term flips of the `Login.cs:120-122` conjunction. |

#### TC-AUTH-DT-008 — Platform admin with `Security:RequireMfaForPrivilegedRoles=true`: enrolment-scoped session  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-004 |
| **Level / Type / Technique** | API · Functional (positive-intermediate) · Decision Table — §4.1 R11 on the `C1=N` (no-slug) path |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional — mirrors `AuthActorFunctionalTests.cs:62-89`) |
| **Role / Permission / Tenant** | PlatformAdmin · n/a — anonymous endpoint · **none** — platform admins have no tenant (`UserAccount.cs:99-102`) |
| **Environment** | **ENV-B** |
| **Preconditions** | The API was restarted with `Security__RequireMfaForPrivilegedRoles=true`. `platform-admin@localhost` has `tenant_id IS NULL`, `role='PlatformAdmin'`, `mfa_enabled=false`, `mfa_secret IS NULL`. |
| **Test Data** | `{"email":"platform-admin@localhost","password":"Dev-Only-Platform-Pass-1!"}` — **`tenantIdentifier` omitted**, which selects `u.TenantId == null` (`Login.cs:70`) and keeps `requireMfaPolicy` on the global flag (`Login.cs:44`, never overwritten because the slug branch at `:46-67` is skipped). |
| **Steps** | 1. Confirm the API is running under ENV-B. 2. `POST /api/auth/login` with the body above. 3. Read `mfaEnrollmentRequired` and `Set-Cookie`. 4. Decode the JWT and assert `scope` and the **absence** of a `tenant_id` claim. 5. With that bearer, `GET /api/tenants`. |
| **Expected UI** | The SPA routes to `/security/mfa-setup`. |
| **Expected API** | Step 2: `200`, `mfaEnrollmentRequired: true`, no `Set-Cookie: qams_rt`, JWT `"scope":"mfa_enrollment"`, **no** `tenant_id` claim (`SecurityAdapters.cs:92-95`). Step 5: `403` `application/problem+json`, `code='MFA-ENROLL-REQUIRED'` (`RequestIdentity.cs:191-193`). |
| **Expected DB** | No new `qams.refresh_session` row for that user. |
| **Expected Audit** | One `audit.security_event` row `event_type='LOGIN_MFA_ENROLL_REQUIRED'`, **`tenant_id IS NULL`** (a platform admin stamps no tenant), `actor='platform-admin@localhost'`. |
| **Expected Notification** | n/a — no notification is defined for the enrolment gate. |
| **Cleanup** | Restart the API back to ENV-A; `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false WHERE email='platform-admin@localhost';` |
| **Evidence** | HTTP captures for steps 2 and 5 · decoded JWT claim set · security-event row showing the null tenant |
| **Result / Defect** | Not Run · — |
| **Notes** | Configuration is read once at composition; a `Security__…` change without a restart makes this case silently test ENV-A instead. Verify the restart before trusting a green result. |

#### TC-AUTH-DT-009 — Platform admin with the config key absent (default false): full session  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-004 |
| **Level / Type / Technique** | API · Functional (positive) · Equivalence Partitioning — the "unset ⇒ documented default" partition of `ConfigGuard.ReadBool` |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | PlatformAdmin · n/a — anonymous endpoint · none |
| **Environment** | ENV-A |
| **Preconditions** | `Security:RequireMfaForPrivilegedRoles` is absent from `appsettings*.json`, user-secrets and the environment (grep the effective configuration before running). `platform-admin@localhost` has `mfa_enabled=false`. |
| **Test Data** | `{"email":"platform-admin@localhost","password":"Dev-Only-Platform-Pass-1!"}` |
| **Steps** | 1. Prove the key is absent. 2. `POST /api/auth/login`. 3. Read `mfaEnrollmentRequired`, `Set-Cookie`, JWT `scope`. 4. With that bearer, `GET /api/tenants`. |
| **Expected UI** | The platform control-plane shell loads (`/platform/tenants`). |
| **Expected API** | Step 2: `200`, `mfaEnrollmentRequired: **false**`, `Set-Cookie: qams_rt=…` present, JWT `"scope":"full"`. Step 4: `200`. |
| **Expected DB** | One new `qams.refresh_session` row. |
| **Expected Audit** | `audit.security_event` row `event_type='LOGIN_SUCCESS'`, `tenant_id IS NULL`. |
| **Expected Notification** | n/a — no notification is defined for a successful sign-in. |
| **Cleanup** | `POST /api/auth/logout`. |
| **Evidence** | Effective-configuration dump proving absence · HTTP captures · decoded `scope` |
| **Result / Defect** | Not Run · — |
| **Notes** | The as-built default means the **most privileged identity in the system signs in with one factor out of the box**. `GAP-AUTH-016` covers its unmanaged lifecycle; this case pins the default. |

#### TC-AUTH-DT-010 — Tenant OFF overrides global ON for a tenant user (`Login.cs:66` is an unconditional overwrite)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-008 (minted here — policy-source precedence) |
| **Level / Type / Technique** | API · Functional (negative-of-control) · Decision Table — the precedence rule between the two policy sources |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | **ENV-B** — global flag ON |
| **Preconditions** | API restarted with `Security__RequireMfaForPrivilegedRoles=true`; `UPDATE saas.tenant SET require_mfa_privileged=false WHERE identifier='demo-lab';`; `admin@demo-lab.local` has `mfa_enabled=false`. |
| **Test Data** | `{"tenantIdentifier":"demo-lab","email":"admin@demo-lab.local","password":"Demo-Admin-Pass-2!"}` |
| **Steps** | 1. Confirm ENV-B and the tenant column = `f`. 2. `POST /api/auth/login`. 3. Read `mfaEnrollmentRequired` and `Set-Cookie`. 4. In the same API instance, `POST /api/auth/login` **without** `tenantIdentifier` as `platform-admin@localhost` and read `mfaEnrollmentRequired`. |
| **Expected UI** | The tenant admin reaches the dashboard shell; no MFA-setup navigation. |
| **Expected API** | Step 2: `200`, `mfaEnrollmentRequired: **false**` — `requireMfaPolicy` is initialised from the global flag at `Login.cs:44` and then **overwritten** by `tenant.Settings.RequireMfaForPrivilegedRoles` at `Login.cs:66`, so the tenant's `false` wins. Step 4: `200`, `mfaEnrollmentRequired: **true**` — the same instance still enforces the global flag for the no-slug path. |
| **Expected DB** | Step 2 creates one `qams.refresh_session` row; step 4 creates none. |
| **Expected Audit** | Two `audit.security_event` rows: `LOGIN_SUCCESS` with `tenant_id` = `demo-lab`, and `LOGIN_MFA_ENROLL_REQUIRED` with `tenant_id IS NULL`. |
| **Expected Notification** | n/a — no notification is defined for either outcome. |
| **Cleanup** | `POST /api/auth/logout` for the tenant session; restart the API back to ENV-A. |
| **Evidence** | Two HTTP captures from one API instance · both security-event rows |
| **Result / Defect** | Not Run · — |
| **Notes** | The precedence is **not** "either source may require MFA" — for a tenant user the platform key is inert. A deployment-wide MFA mandate is therefore impossible today; this is the operational face of `GAP-AUTH-001`. |

#### TC-AUTH-DT-011 — Enrolment-scoped token reaches `/api/auth/mfa/enroll` (§4.4 R4, allow-list positive)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-005 (minted here — enrolment-session containment) |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — §4.4 rule R4 |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin holding a `scope=mfa_enrollment` token · n/a — `[Authorize]` only, no `[RequirePermission]` (`AuthController.cs:120-123`) · `demo-lab` |
| **Environment** | ENV-A with `saas.tenant.require_mfa_privileged=true` for `demo-lab` |
| **Preconditions** | An enrolment-scoped token obtained exactly as in TC-AUTH-DT-004, less than 15 minutes old. |
| **Test Data** | `Authorization: Bearer <enrolment token>`; empty body. |
| **Steps** | 1. Obtain the enrolment token (TC-AUTH-DT-004 steps 1-2). 2. `POST /api/auth/mfa/enroll` with that bearer and no body. 3. Read status and body. 4. `SELECT mfa_secret, mfa_enabled FROM qams.user_account WHERE email='admin@demo-lab.local';`. |
| **Expected UI** | `MfaSetupComponent.ngOnInit` (`mfa-setup.component.ts:99-105`) populates the `secret()` signal, renders the Base32 string in `<code class="secret">` and the `otpauth://` URI beneath it, and enables the code input. |
| **Expected API** | `200` `application/json` `{"secret":"<32-char Base32>","otpAuthUri":"otpauth://totp/NT.QAMS%3Aadmin%40demo-lab.local?secret=<same>&issuer=NT.QAMS&algorithm=SHA1&digits=6&period=30"}` (`TotpService.cs:46-51`; issuer supplied at `MfaAndPin.cs:30`). |
| **Expected DB** | `mfa_secret` now holds the returned 32-character Base32 string (160 bits, `TotpService.cs:20`); `mfa_enabled` still `false` (`UserAccount.cs:235`). |
| **Expected Audit** | One `audit.field_change` row: `entity_type='UserAccount'`, `property='MfaSecret'`, `old_value='«redacted»'`, `new_value='«redacted»'` (`FieldChangeInterceptor.cs:34,98-99`). **No** `audit.security_event` row — enrolment writes none; only confirmation does (`MfaAndPin.cs:54`). |
| **Expected Notification** | n/a — no notification is defined for starting enrolment. |
| **Cleanup** | `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false WHERE email='admin@demo-lab.local';` `UPDATE saas.tenant SET require_mfa_privileged=false WHERE identifier='demo-lab';` |
| **Evidence** | HTTP capture · `mfa_secret` before/after (length only — do **not** paste the value into the report) · field-change row proving redaction |
| **Result / Defect** | Not Run · — |
| **Notes** | The response body **does** carry the secret in clear — necessarily, it is the enrolment payload. The evidence artefact must record its length and Base32 alphabet conformance, not its value. |

#### TC-AUTH-DT-012 — Enrolment-scoped token refused everywhere else: 403 MFA-ENROLL-REQUIRED (§4.4 R6)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004, URS-005 · RSK-AUTH-005 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — §4.4 rule R6, applied as a small sweep |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin holding `scope=mfa_enrollment` · the target routes' own permissions are never evaluated · `demo-lab` |
| **Environment** | ENV-A with `require_mfa_privileged=true` for `demo-lab` |
| **Preconditions** | A fresh enrolment-scoped token as in TC-AUTH-DT-004. |
| **Test Data** | Target routes, all with the enrolment bearer: `GET /api/auth/me/privileges`, `GET /api/users`, `GET /api/users/directory`, `PUT /api/tenant-settings/mfa-policy` body `{"require":false}`, `GET /api/nonconformances`, `POST /api/auth/signature-pin` body `{"pin":"1234"}`. |
| **Steps** | 1. Obtain the enrolment token. 2. Issue each of the six requests. 3. Record status, `code` extension and `content-type` for each. 4. Confirm no state changed: `SELECT require_mfa_privileged FROM saas.tenant WHERE identifier='demo-lab';` and `SELECT pin_hash FROM qams.user_account WHERE email='admin@demo-lab.local';`. |
| **Expected UI** | The SPA's error interceptor keeps the user on `/security/mfa-setup`; no shell navigation succeeds. |
| **Expected API** | All six: `403` `application/problem+json`, `code='MFA-ENROLL-REQUIRED'`, `title='Multi-factor authentication must be set up before continuing.'` (`RequestIdentity.cs:191-193`). Note `POST /api/auth/signature-pin` is refused with `MFA-ENROLL-REQUIRED`, **not** with a validation error — the middleware runs before MVC (`Program.cs:268` vs `:270`). |
| **Expected DB** | `require_mfa_privileged` unchanged at `true`; `pin_hash` unchanged (still `NULL` if it was). No row written by any of the six calls. |
| **Expected Audit** | No `audit.security_event` and no `audit.field_change` row from any of the six — the gate short-circuits before any handler. |
| **Expected Notification** | n/a — a refused request raises none. |
| **Cleanup** | `UPDATE saas.tenant SET require_mfa_privileged=false WHERE identifier='demo-lab';` |
| **Evidence** | Six HTTP captures in one transcript · the two post-condition SELECTs |
| **Result / Defect** | Not Run · — |
| **Notes** | Six requests against the 10/min `AuthPolicy` budget only for the two `/api/auth/*` targets; the other four ride the 300/min global partition. Sequence them so the auth budget is not exhausted mid-case. |

#### TC-AUTH-DT-013 — Enrolment-scoped token cannot log out: 403 on `/api/auth/logout` (§4.4 R5)  [GD — GAP-AUTH-007]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-005 |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — §4.4 rule R5, the allow-list omission |
| **Priority / Severity / Automation** | Medium · Low · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin holding `scope=mfa_enrollment` · n/a — `/api/auth/logout` is `[AllowAnonymous]` (`AuthController.cs:75-76`) yet still traverses the gate · `demo-lab` |
| **Environment** | ENV-A with `require_mfa_privileged=true` for `demo-lab` |
| **Preconditions** | A fresh enrolment-scoped token as in TC-AUTH-DT-004. |
| **Test Data** | `POST /api/auth/logout` with header `Authorization: Bearer <enrolment token>` and no `qams_rt` cookie (the enrolment login issued none). |
| **Steps** | 1. Obtain the enrolment token. 2. `POST /api/auth/logout` **with** the `Authorization` header. 3. Record status, `code`, and whether a `Set-Cookie` deletion for `qams_rt` was emitted. |
| **Expected UI** | A "sign out" click from `/security/mfa-setup` fails; the component's own cancel button (`mfa-setup.component.ts:57`) only routes client-side and leaves the server session untouched. |
| **Expected API** | `403` `application/problem+json`, `code='MFA-ENROLL-REQUIRED'`. **No** `Set-Cookie: qams_rt=; expires=Thu, 01 Jan 1970 …; path=/api/auth` — `AuthController.cs:80` is never reached. |
| **Expected DB** | No `qams.refresh_session` row changes state (there is none for this session anyway). |
| **Expected Audit** | **No** `LOGOUT` security event (`RefreshSessions.cs:177` unreached). |
| **Expected Notification** | n/a — logout raises none. |
| **Cleanup** | `UPDATE saas.tenant SET require_mfa_privileged=false WHERE identifier='demo-lab';` |
| **Evidence** | HTTP capture including the full response header block |
| **Result / Defect** | Not Run · — |
| **Notes** | **Gap-dependent.** `GAP-AUTH-007` acceptance criterion (b) requires this call to return `204` once `/api/auth/logout` (+ the `/api/v1/` mirror) joins the allow-list at `RequestIdentity.cs:172-179`. Until then the case documents the defect; do **not** re-label it `[IV]` and record a pass. |

#### TC-AUTH-DT-014 — The same logout **without** the bearer header succeeds: 204  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-005 |
| **Level / Type / Technique** | API · Functional (positive) · Decision Table — §4.4 R5 complement; the gate reads only the `scope` claim (`RequestIdentity.cs:183`) |
| **Priority / Severity / Automation** | Medium · Low · Yes (functional) |
| **Role / Permission / Tenant** | Anonymous (no token presented) · n/a — `[AllowAnonymous]` · none resolved (no `tenant_id` claim without a token) |
| **Environment** | ENV-A with `require_mfa_privileged=true` for `demo-lab` |
| **Preconditions** | The same enrolment-scoped session as TC-AUTH-DT-013 exists, but the request omits the header. |
| **Test Data** | `POST /api/auth/logout`, **no** `Authorization` header, **no** `qams_rt` cookie. |
| **Steps** | 1. Issue the request exactly as specified. 2. Record status and headers. 3. Repeat the identical request a second time. |
| **Expected UI** | The SPA clears its in-memory session and returns to `/login`. |
| **Expected API** | Both calls: `204 No Content`, with `Set-Cookie: qams_rt=; …; path=/api/auth` emitted by `AuthController.cs:80` (the deletion is unconditional). No problem body. |
| **Expected DB** | No `qams.refresh_session` row is touched — `LogoutHandler` receives a null token and returns silently (`RefreshSessions.cs:167-171`). |
| **Expected Audit** | **No** `LOGOUT` security event on either call (the event is written only when a session row is found). |
| **Expected Notification** | n/a — logout raises none. |
| **Cleanup** | `UPDATE saas.tenant SET require_mfa_privileged=false WHERE identifier='demo-lab';` |
| **Evidence** | Two HTTP captures showing identical `204` responses |
| **Result / Defect** | Not Run · — |
| **Notes** | Read as a pair with TC-AUTH-DT-013 the two cases **contradict each other by design**: whether a user can end their enrolment session depends on whether the client attaches a token. That inconsistency is the finding recorded in `GAP-AUTH-007`. |

---

## 2. API cases — enrolment, confirmation, TOTP windows, throttling, and the tenant MFA-policy endpoints

#### TC-AUTH-API-030 — `POST /api/auth/mfa/enroll` returns a 160-bit Base32 secret and a conformant `otpauth://` URI  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-004 |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — the first step of the enrolment ceremony |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst with a `scope=full` session · n/a — `[Authorize]` only, no `[RequirePermission]` (`AuthController.cs:120-121`) · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `analyst-nomfa@demo-lab.local` is active, signed in with a full session, `mfa_secret IS NULL`, `mfa_enabled=false`. |
| **Test Data** | `POST /api/auth/mfa/enroll`, bearer = the full-session token, empty body. |
| **Steps** | 1. Sign in and capture the token. 2. `POST /api/auth/mfa/enroll`. 3. Assert `secret` matches `^[A-Z2-7]{32}$` (the alphabet at `TotpService.cs:16`; 20 random bytes → 32 Base32 characters). 4. Assert `otpAuthUri` equals `otpauth://totp/NT.QAMS%3Aanalyst-nomfa%40demo-lab.local?secret=<secret>&issuer=NT.QAMS&algorithm=SHA1&digits=6&period=30`. 5. `SELECT length(mfa_secret), mfa_enabled FROM qams.user_account WHERE email='analyst-nomfa@demo-lab.local';`. |
| **Expected UI** | `/security/mfa-setup` shows the three numbered steps, the secret in `<code class="secret">`, a "copy" link, the `otpauth://` URI in the `.uri` block, and the code input; the confirm button stays disabled until six characters are typed (`mfa-setup.component.ts:48`). |
| **Expected API** | `200` `application/json` with exactly the two properties `secret` and `otpAuthUri` (`MfaEnrollmentResponse`, `AuthContracts.cs:14`). No other field. |
| **Expected DB** | `length(mfa_secret) = 32`; `mfa_enabled = false`. |
| **Expected Audit** | One `audit.field_change` row `entity_type='UserAccount'`, `property='MfaSecret'`, both values `'«redacted»'`, `actor` = the caller's JWT `name` claim. No `audit.security_event` row. |
| **Expected Notification** | n/a — no notification is defined for starting enrolment. |
| **Cleanup** | `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false WHERE email='analyst-nomfa@demo-lab.local';` |
| **Evidence** | HTTP capture with the secret masked · regex assertions · SQL length/flag result · field-change row |
| **Result / Defect** | Not Run · — |
| **Notes** | The URI's label is `Uri.EscapeDataString($"{issuer}:{account}")` (`TotpService.cs:48`), which percent-encodes both `:` and `@` — assert the encoded form, not the readable one. |

#### TC-AUTH-API-031 — `POST /api/auth/mfa/enroll` anonymously: 401 AUTH-401  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004, URS-001 · RSK-AUTH-004 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — §4.4 rule R1 |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Anonymous · n/a — authentication, not authorization, refuses this · none |
| **Environment** | ENV-A |
| **Preconditions** | None beyond a running API. |
| **Test Data** | `POST /api/auth/mfa/enroll` with **no** `Authorization` header; and a second attempt with `Authorization: Bearer not.a.jwt`. |
| **Steps** | 1. Issue the header-less request. 2. Issue the malformed-bearer request. 3. Record status, `code` and `content-type` for both. 4. `SELECT count(*) FROM audit.field_change WHERE entity_type='UserAccount' AND property='MfaSecret' AND occurred_at_utc > <t0>;`. |
| **Expected UI** | The SPA's auth interceptor attempts one silent refresh and, on failure, clears the session and routes to `/login` (`auth.service.ts:83-95`). |
| **Expected API** | Both: `401` `application/problem+json`, `code='AUTH-401'` (`ProblemAuthorizationResultHandler.cs:18,42-44`). |
| **Expected DB** | No change to any `qams.user_account` row. |
| **Expected Audit** | The step-4 count is `0` — no enrolment occurred. No `audit.security_event` row (the framework challenge writes none). |
| **Expected Notification** | n/a — a refused request raises none. |
| **Cleanup** | n/a — nothing was created. |
| **Evidence** | Two HTTP captures · the step-4 count query |
| **Result / Defect** | Not Run · — |
| **Notes** | `AUTH-401` comes from the framework challenge handler, not from `DomainExceptionHandler`; it is the only `AUTH-` code in the module with no `DomainException` behind it. |

#### TC-AUTH-API-032 — Re-enrolling an MFA-**enabled** account silently disables MFA  [ID — GAP-AUTH-904]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · no URS covers self-service MFA downgrade — trace to `UserAccount.cs:227-236` · RSK-AUTH-007 (minted here — unlogged MFA downgrade) |
| **Level / Type / Technique** | API · Security (as-built) · State Transition — the `Enabled → Enrolled-Unconfirmed` edge of §3.1's MFA sub-machine |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin with a `scope=full` session · n/a — `[Authorize]` only · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `UPDATE qams.user_account SET mfa_secret='JBSWY3DPEHPK3PXP', mfa_enabled=true WHERE email='admin@demo-lab.local';` and a full session obtained by logging in with `C(0)`. |
| **Test Data** | `POST /api/auth/mfa/enroll`, bearer = that full-session token, empty body. |
| **Steps** | 1. Establish the precondition and sign in. 2. `SELECT mfa_secret, mfa_enabled FROM qams.user_account WHERE email='admin@demo-lab.local';` — record. 3. `POST /api/auth/mfa/enroll`. 4. Repeat the SELECT. 5. `SELECT event_type FROM audit.security_event WHERE actor='admin@demo-lab.local' AND occurred_at_utc > <t0>;`. 6. Attempt `POST /api/auth/login` with `mfaCode` computed from the **old** secret `JBSWY3DPEHPK3PXP`. |
| **Expected UI** | `/security/mfa-setup` reached voluntarily from security settings (`security-settings.component.ts:89`) shows a brand-new secret with no warning that the existing factor has just been turned off. |
| **Expected API** | Step 3: `200` with a **new** `secret`. Step 6: `200` with `mfaRequired: false` and a **full** session — the account no longer has MFA, so no code is demanded at all (`Login.cs:98`). |
| **Expected DB** | After step 3: `mfa_secret` is the new 32-character value (≠ `JBSWY3DPEHPK3PXP`) and **`mfa_enabled = false`** (`UserAccount.cs:235`). |
| **Expected Audit** | Two `audit.field_change` rows: `property='MfaSecret'` (`'«redacted»'` → `'«redacted»'`) and `property='MfaEnabled'` `old_value='True'` `new_value='False'`. Step 5 returns **no** security-event row — the downgrade is invisible to `audit.security_event`, which only records `MFA_ENABLED` on confirm (`MfaAndPin.cs:54`). |
| **Expected Notification** | n/a — no notification is defined; that absence is part of the finding. |
| **Cleanup** | `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false WHERE email='admin@demo-lab.local';` |
| **Evidence** | Before/after SQL of `mfa_enabled` · both field-change rows · the empty security-event query · the step-6 login capture |
| **Result / Defect** | Not Run · — |
| **Notes** | **Implementation-derived, no requirement.** A single unauthenticated-by-second-factor POST removes the second factor and writes no security event. Raised as **GAP-AUTH-904**. Do not author this as a "requirement satisfied" case. |

#### TC-AUTH-API-033 — A second enrolment invalidates the first secret at confirmation  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-006 (minted here — TOTP verification correctness) |
| **Level / Type / Technique** | API · Functional (negative) · State Transition — the `Enrolled-Unconfirmed → Enrolled-Unconfirmed` self-loop with secret replacement |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Analyst with a full session · n/a — `[Authorize]` only · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `analyst-nomfa@demo-lab.local` signed in, `mfa_secret IS NULL`, `mfa_enabled=false`. |
| **Test Data** | Secret `S1` from the first enroll; secret `S2` from the second; codes `C_S1(0)` and `C_S2(0)`. |
| **Steps** | 1. `POST /api/auth/mfa/enroll` → record `S1`. 2. `POST /api/auth/mfa/enroll` again → record `S2`; assert `S2 ≠ S1`. 3. `POST /api/auth/mfa/confirm` with `{"code":"<C_S1(0)>"}`. 4. `POST /api/auth/mfa/confirm` with `{"code":"<C_S2(0)>"}`. 5. `SELECT mfa_enabled FROM qams.user_account WHERE email='analyst-nomfa@demo-lab.local';`. |
| **Expected UI** | The setup screen shows only `S2`; a code from a stale authenticator entry produces the inline error text from the problem `title`. |
| **Expected API** | Step 3: `422` `application/problem+json`, `code='MFA-003'`, `title='The verification code is invalid.'` (`MfaAndPin.cs:49`). Step 4: `204 No Content`. |
| **Expected DB** | After step 2, `mfa_secret` = `S2`; after step 4, `mfa_enabled = true`. |
| **Expected Audit** | Two `MfaSecret` field-change rows (both redacted); one `MfaEnabled` row `False → True`; one `audit.security_event` row `event_type='MFA_ENABLED'` written only after step 4. |
| **Expected Notification** | n/a — no notification is defined for MFA activation. |
| **Cleanup** | `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false WHERE email='analyst-nomfa@demo-lab.local';` |
| **Evidence** | Both enrol responses (secrets masked, first 4 characters shown to prove difference) · both confirm captures · SQL flag |
| **Result / Defect** | Not Run · — |
| **Notes** | Confirms the domain's "last enrolment wins" rule (`UserAccount.cs:234`) — there is no pending/committed secret pair, so a user who scans the first QR and then reloads the page is locked out of their own enrolment until they rescan. |

#### TC-AUTH-API-034 — `POST /api/auth/mfa/confirm` with the current code: 204 and `MFA_ENABLED`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004, URS-016 · RSK-AUTH-004 |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — completion of the enrolment ceremony |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Analyst with a full session · n/a — `[Authorize]` only (`AuthController.cs:125-126`) · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `UPDATE qams.user_account SET mfa_secret='JBSWY3DPEHPK3PXP', mfa_enabled=false WHERE email='analyst-nomfa@demo-lab.local';` and a valid full-session bearer for that user. |
| **Test Data** | `{"code":"<C(0)>"}` computed at call time from `JBSWY3DPEHPK3PXP`. |
| **Steps** | 1. Apply the precondition. 2. Compute `C(0)` and immediately `POST /api/auth/mfa/confirm`. 3. Read status and body. 4. `SELECT mfa_enabled FROM qams.user_account WHERE email='analyst-nomfa@demo-lab.local';`. 5. `SELECT event_type, tenant_id, actor, detail, ip_address FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 1;`. |
| **Expected UI** | `MfaSetupComponent.confirm()` sets `done()`; the card replaces the form with the `mfa.enabled` success panel and a "back to login" button (`mfa-setup.component.ts:26-29`). |
| **Expected API** | `204 No Content`, empty body (`AuthController.cs:127-131`). |
| **Expected DB** | `mfa_enabled = true`; `mfa_secret` unchanged. |
| **Expected Audit** | One `audit.security_event` row: `event_type='MFA_ENABLED'`, `tenant_id` = the `demo-lab` id, `actor='analyst-nomfa@demo-lab.local'` (the **email**, `MfaAndPin.cs:54`), `detail IS NULL`, `ip_address IS NULL` (`GAP-AUTH-005`). One `audit.field_change` row `property='MfaEnabled'`, `'False' → 'True'`. |
| **Expected Notification** | n/a — no notification is defined for MFA activation. |
| **Cleanup** | `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false WHERE email='analyst-nomfa@demo-lab.local';` |
| **Evidence** | HTTP capture · SQL flag · the security-event row verbatim |
| **Result / Defect** | Not Run · — |
| **Notes** | `MFA_ENABLED` is the **only** MFA lifecycle event written to `audit.security_event`; enrolment, re-enrolment and the implicit disable write none. Assert `actor` is the email — the e-signature paths use display name instead (`10-module-auth.md` §1.6). |

#### TC-AUTH-API-035 — `POST /api/auth/mfa/confirm` with a wrong code: 422 MFA-003, no state change  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-006 |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — the `!totp.Verify(...)` arm of `MfaAndPin.cs:47` |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst with a full session · n/a — `[Authorize]` only · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | As TC-AUTH-API-034, and `failed_login_attempts = 0`. |
| **Test Data** | `{"code":"000000"}` — pre-verified not to equal `C(-1)`, `C(0)` or `C(+1)`. |
| **Steps** | 1. Apply the precondition. 2. `POST /api/auth/mfa/confirm` with the body above. 3. Read status, `code`, `title`. 4. `SELECT mfa_enabled, failed_login_attempts, locked_until_utc FROM qams.user_account WHERE email='analyst-nomfa@demo-lab.local';`. 5. Count new `audit.security_event` rows since `t0`. |
| **Expected UI** | The `error()` signal renders the problem `title` under the code input; the input stays enabled for a retry. |
| **Expected API** | `422 Unprocessable Entity` `application/problem+json`, `code='MFA-003'`, `title='The verification code is invalid.'` (`MfaAndPin.cs:49`; mapped by the catch-all `DomainException` arm, `DomainExceptionHandler.cs:73-78`). |
| **Expected DB** | `mfa_enabled` still `false`; **`failed_login_attempts` still `0`** and `locked_until_utc` still `NULL` — `ConfirmMfaHandler` never calls `RegisterFailedLogin`. |
| **Expected Audit** | Step 5 returns `0` new rows — a failed MFA **confirmation** writes no security event, unlike a failed MFA **login** (`Login.cs:113`). No `audit.field_change` row. |
| **Expected Notification** | n/a — no notification is defined for a failed confirmation. |
| **Cleanup** | `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false WHERE email='analyst-nomfa@demo-lab.local';` |
| **Evidence** | HTTP capture · SQL result showing all three columns unchanged · the zero-row event count |
| **Result / Defect** | Not Run · — |
| **Notes** | The unlogged, uncounted failure path is raised as **GAP-AUTH-903**: MFA-confirm guessing is throttled only by the 10/min per-IP `AuthPolicy` and leaves no forensic trace. |

#### TC-AUTH-API-036 — Confirming before enrolling yields MFA-003, never MFA-002  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-006 |
| **Level / Type / Technique** | API · Functional (negative) · Branch Coverage — the short-circuit left operand of `MfaAndPin.cs:47` (`string.IsNullOrWhiteSpace(account.MfaSecret)`) |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Analyst with a full session · n/a — `[Authorize]` only · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false WHERE email='analyst-nomfa@demo-lab.local';` — the account has never enrolled. |
| **Test Data** | `{"code":"123456"}` |
| **Steps** | 1. Apply the precondition and confirm `mfa_secret IS NULL`. 2. `POST /api/auth/mfa/confirm` with the body above. 3. Read status and the `code` extension. 4. `SELECT mfa_secret, mfa_enabled FROM qams.user_account WHERE email='analyst-nomfa@demo-lab.local';`. |
| **Expected UI** | Not reachable through the SPA — `MfaSetupComponent` enrols on init, so this state is only reachable by a direct API call. |
| **Expected API** | `422`, `code='**MFA-003**'`, `title='The verification code is invalid.'`. **Not** `MFA-002` — the handler's own null check fires first, so `UserAccount.ConfirmMfa`'s `MFA-002` guard (`UserAccount.cs:242`) is unreachable over HTTP. |
| **Expected DB** | `mfa_secret` still `NULL`; `mfa_enabled` still `false`. |
| **Expected Audit** | No `audit.security_event` and no `audit.field_change` row. |
| **Expected Notification** | n/a — no notification is defined for a failed confirmation. |
| **Cleanup** | n/a — no state changed. |
| **Evidence** | HTTP capture showing the `code` extension · SQL result |
| **Result / Defect** | Not Run · — |
| **Notes** | Pins the reachability caveat in `10-module-auth.md` §1.5: `MFA-002` exists in the domain but no HTTP path can produce it. A future refactor that reorders `MfaAndPin.cs:47` would surface `MFA-002` and break this case — which is the point. |

#### TC-AUTH-API-037 — Clock-skew window, lower edge: the `−1` step code is accepted  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-006 |
| **Level / Type / Technique** | API · Functional (positive) · BVA — at the inclusive lower bound of the `for (window = -1; window <= 1; window++)` loop (`TotpService.cs:33`) |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst with a full session · n/a — `[Authorize]` only · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `mfa_secret='JBSWY3DPEHPK3PXP'`, `mfa_enabled=false`; a full-session bearer for `analyst-nomfa@demo-lab.local`. |
| **Test Data** | `{"code":"<C(-1)>"}` — the code for `floor(epoch/30) - 1`. |
| **Steps** | 1. Note `t = floor(unixEpochSeconds/30)` at the moment of the call. 2. Compute the code for counter `t-1`. 3. `POST /api/auth/mfa/confirm` immediately. 4. Read status. 5. `SELECT mfa_enabled FROM qams.user_account WHERE email='analyst-nomfa@demo-lab.local';`. |
| **Expected UI** | The success panel appears, exactly as for the current code — the user cannot tell which window matched. |
| **Expected API** | `204 No Content`. |
| **Expected DB** | `mfa_enabled = true`. |
| **Expected Audit** | One `audit.security_event` row `event_type='MFA_ENABLED'`; one `audit.field_change` row `property='MfaEnabled'` `'False' → 'True'`. |
| **Expected Notification** | n/a — no notification is defined for MFA activation. |
| **Cleanup** | `UPDATE qams.user_account SET mfa_secret='JBSWY3DPEHPK3PXP', mfa_enabled=false WHERE email='analyst-nomfa@demo-lab.local';` |
| **Evidence** | The recorded `t` value · the generated code · HTTP capture · SQL flag |
| **Result / Defect** | Not Run · — |
| **Notes** | Record `t` in the evidence. Without it a `204` cannot be distinguished from an accidental `C(0)` when the request straddles a step boundary; re-run if `floor(epoch/30)` changed between steps 1 and 3. |

#### TC-AUTH-API-038 — Clock-skew window, upper edge: the `+1` step code is accepted  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-006 |
| **Level / Type / Technique** | API · Functional (positive) · BVA — at the inclusive upper bound of the verification loop (`TotpService.cs:33`) |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst with a full session · n/a — `[Authorize]` only · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | As TC-AUTH-API-037. |
| **Test Data** | `{"code":"<C(+1)>"}` — the code for `floor(epoch/30) + 1`, i.e. a code from the *future* step. |
| **Steps** | 1. Note `t`. 2. Compute the code for counter `t+1`. 3. `POST /api/auth/mfa/confirm` immediately. 4. Read status. 5. `SELECT mfa_enabled …`. |
| **Expected UI** | The success panel appears. |
| **Expected API** | `204 No Content`. |
| **Expected DB** | `mfa_enabled = true`. |
| **Expected Audit** | One `MFA_ENABLED` security-event row; one `MfaEnabled` field-change row. |
| **Expected Notification** | n/a — no notification is defined for MFA activation. |
| **Cleanup** | `UPDATE qams.user_account SET mfa_secret='JBSWY3DPEHPK3PXP', mfa_enabled=false WHERE email='analyst-nomfa@demo-lab.local';` |
| **Evidence** | The recorded `t` · the generated code · HTTP capture · SQL flag |
| **Result / Defect** | Not Run · — |
| **Notes** | Together with TC-AUTH-API-037 this proves the acceptance window is **90 seconds wide** (three 30-second steps), which is the input to the replay finding in TC-AUTH-API-041. |

#### TC-AUTH-API-039 — Clock-skew window, outside lower: the `−2` step code is rejected  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-006 |
| **Level / Type / Technique** | API · Functional (negative) · BVA — one step below the accepted partition |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst with a full session · n/a — `[Authorize]` only · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | As TC-AUTH-API-037. |
| **Test Data** | `{"code":"<C(-2)>"}` |
| **Steps** | 1. Note `t`. 2. Compute the code for counter `t-2`. 3. `POST /api/auth/mfa/confirm` immediately. 4. Read status and `code`. 5. `SELECT mfa_enabled …`. |
| **Expected UI** | The inline error text from the problem `title` appears beneath the code input. |
| **Expected API** | `422`, `code='MFA-003'`, `title='The verification code is invalid.'`. |
| **Expected DB** | `mfa_enabled` still `false`. |
| **Expected Audit** | No `audit.security_event` row; no `audit.field_change` row. |
| **Expected Notification** | n/a — no notification is defined for a failed confirmation. |
| **Cleanup** | n/a — no state changed. |
| **Evidence** | The recorded `t` · the generated code · HTTP capture · SQL flag |
| **Result / Defect** | Not Run · — |
| **Notes** | Must be issued inside the same step as step 1, otherwise `t-2` becomes `t-1` relative to the server clock and the case inverts. |

#### TC-AUTH-API-040 — Clock-skew window, outside upper: the `+2` step code is rejected  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-006 |
| **Level / Type / Technique** | API · Functional (negative) · BVA — one step above the accepted partition |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst with a full session · n/a — `[Authorize]` only · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | As TC-AUTH-API-037. |
| **Test Data** | `{"code":"<C(+2)>"}` |
| **Steps** | 1. Note `t`. 2. Compute the code for counter `t+2`. 3. `POST /api/auth/mfa/confirm` immediately. 4. Read status and `code`. 5. `SELECT mfa_enabled …`. |
| **Expected UI** | The inline error text appears; the input stays enabled. |
| **Expected API** | `422`, `code='MFA-003'`. |
| **Expected DB** | `mfa_enabled` still `false`. |
| **Expected Audit** | No security-event and no field-change row. |
| **Expected Notification** | n/a — no notification is defined for a failed confirmation. |
| **Cleanup** | n/a — no state changed. |
| **Evidence** | The recorded `t` · the generated code · HTTP capture · SQL flag |
| **Result / Defect** | Not Run · — |
| **Notes** | Cases 037–040 form the complete four-point boundary set `{−2, −1, +1, +2}` around the `±1` window; `C(0)` is covered by TC-AUTH-API-034. |

#### TC-AUTH-API-041 — The same TOTP code is accepted twice: codes are not single-use  [ID — GAP-AUTH-901]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 (RFC 6238 §5.2 requires one-time acceptance; the URS does not restate it) · RSK-AUTH-006 |
| **Level / Type / Technique** | API · Security (as-built) · Error Guessing — replay of an already-accepted authenticator code |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst with a full session · n/a — `[Authorize]` only · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `mfa_secret='JBSWY3DPEHPK3PXP'`, `mfa_enabled=false`; a full-session bearer. |
| **Test Data** | One code `X = C(0)`, submitted twice inside the same 30-second step. |
| **Steps** | 1. Note `t`; compute `X = C(0)`. 2. `POST /api/auth/mfa/confirm` `{"code":"X"}` — record status. 3. `UPDATE qams.user_account SET mfa_enabled=false WHERE email='analyst-nomfa@demo-lab.local';` (return the account to `Enrolled-Unconfirmed` without changing the secret). 4. `POST /api/auth/mfa/confirm` `{"code":"X"}` again, still inside step `t`. 5. Record status. 6. Verify `floor(epoch/30)` is still `t`. |
| **Expected UI** | Indistinguishable from a first-time confirmation — the success panel appears both times. |
| **Expected API** | Step 2: `204`. Step 4: **`204` again** — `TotpService.Verify` (`TotpService.cs:24-44`) holds no record of consumed codes and no store exists anywhere in `src/` for one. |
| **Expected DB** | `mfa_enabled = true` after both calls; no table records the consumed counter (there is no such column on `qams.user_account`). |
| **Expected Audit** | **Two** `audit.security_event` rows with `event_type='MFA_ENABLED'`, same `actor`, seconds apart. |
| **Expected Notification** | n/a — no notification is defined for MFA activation. |
| **Cleanup** | `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false WHERE email='analyst-nomfa@demo-lab.local';` |
| **Evidence** | Both HTTP captures with timestamps inside one step · the recorded `t` before and after · both security-event rows |
| **Result / Defect** | Not Run · — |
| **Notes** | **Implementation-derived defect, raised as GAP-AUTH-901.** Combined with TC-AUTH-API-037/038 the reuse horizon is up to **90 seconds**. Record the observed status of step 4 exactly; a future single-use store would make it `422 MFA-003`. |

#### TC-AUTH-API-042 — The same TOTP code logs in twice within one step  [ID — GAP-AUTH-901]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-006 |
| **Level / Type / Technique** | API · Security (as-built) · Error Guessing — replay of an intercepted login code |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Analyst · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `analyst-mfa@demo-lab.local` has `mfa_secret='JBSWY3DPEHPK3PXP'`, `mfa_enabled=true`, `is_active=true`, `failed_login_attempts=0`. |
| **Test Data** | password `Analyst-Mfa-Pass-1!`, code `X = C(0)`, two separate HTTP clients (distinct cookie jars). |
| **Steps** | 1. Note `t`; compute `X`. 2. Client 1: `POST /api/auth/login` with password + `X` — record status, token and `Set-Cookie`. 3. Client 2: the **same** body with the **same** `X`, still inside step `t` — record status, token and `Set-Cookie`. 4. Verify `floor(epoch/30)` is still `t`. 5. `SELECT id, family_id FROM qams.refresh_session ORDER BY expires_at_utc DESC LIMIT 2;`. |
| **Expected UI** | Both browsers reach the dashboard shell; neither is warned. |
| **Expected API** | Both: `200`, `mfaRequired: false`, distinct non-empty `accessToken` values, each with its own `Set-Cookie: qams_rt=…`. |
| **Expected DB** | **Two** new `qams.refresh_session` rows with **different** `family_id` values, both `revoked_at_utc IS NULL` — two independent 14-day sessions from one authenticator code. |
| **Expected Audit** | Two `audit.security_event` rows `event_type='LOGIN_SUCCESS'`, same `actor`, seconds apart, with nothing recording that the same second factor was used twice. |
| **Expected Notification** | n/a — no notification is defined for a successful sign-in. |
| **Cleanup** | `POST /api/auth/logout` from both clients; `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false WHERE email='analyst-mfa@demo-lab.local';` |
| **Evidence** | Two HTTP captures with sub-step timestamps · the two `refresh_session` rows · both security-event rows |
| **Result / Defect** | Not Run · — |
| **Notes** | The login-side face of **GAP-AUTH-901**, and the materially worse half: replaying a confirm code re-enables a factor the attacker already has, while replaying a login code mints a **full 14-day session family**. |

#### TC-AUTH-API-043 — Confirm-code input partitions: whitespace, wrong length and non-numeric  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-006 |
| **Level / Type / Technique** | API · Functional (negative + positive) · Equivalence Partitioning over the `code` string, with BVA on length 5/6/7 |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Analyst with a full session · n/a — `[Authorize]` only · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `mfa_secret='JBSWY3DPEHPK3PXP'`, `mfa_enabled=false`; a full-session bearer; reset `mfa_enabled=false` between sub-steps. |
| **Test Data** | P1 `" <C(0)> "` (padded, valid) · P2 `""` · P3 `"   "` · P4 `"12345"` (5 digits) · P5 `"1234567"` (7 digits) · P6 `"abcdef"` · P7 `"<C(0)>x"`. |
| **Steps** | 1. For each partition P1…P7 send `POST /api/auth/mfa/confirm` `{"code":"<value>"}` and record status + `code` extension. 2. After each, `SELECT mfa_enabled …`. 3. Reset `mfa_enabled=false` before the next sub-step. |
| **Expected UI** | The SPA's confirm button is disabled below six characters (`mfa-setup.component.ts:48`), so P2, P3 and P4 are unreachable through the UI and only reproducible by direct API call. |
| **Expected API** | P1: `204` — `TotpService.Verify` trims (`TotpService.cs:31`). P2 and P3: `422 MFA-003` — the whitespace guard at `TotpService.cs:26-29` returns false. P4, P5, P6, P7: `422 MFA-003` — `FixedTimeEquals` fails on unequal byte lengths and on any non-matching content. **No `400` is possible on any partition**: `ConfirmMfaCommand` has no FluentValidation validator anywhere in `src/`, so the FluentValidation branch of `DomainExceptionHandler.cs:34-44` is never entered. |
| **Expected DB** | `mfa_enabled = true` only after P1; `false` after every other partition. |
| **Expected Audit** | Exactly one `MFA_ENABLED` security-event row across the whole case (from P1). No rows from P2…P7. |
| **Expected Notification** | n/a — no notification is defined for confirmation attempts. |
| **Cleanup** | `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false WHERE email='analyst-nomfa@demo-lab.local';` |
| **Evidence** | A table of seven request/response pairs with statuses and `code` extensions · the per-partition SQL flag |
| **Result / Defect** | Not Run · — |
| **Notes** | Seven requests hit the 10/min `AuthPolicy` partition — reset `mfa_enabled` by SQL, not by re-enrolling, or the budget is exhausted before P7. The missing validator is part of **GAP-AUTH-903**. |

#### TC-AUTH-API-044 — The 11th `/api/auth/mfa/confirm` in one minute is throttled: 429 with `Retry-After: 60`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004, URS-016 · RSK-AUTH-006 |
| **Level / Type / Technique** | API · Security (negative) · BVA — at the `AuthPermitPerMinute` boundary (10th permitted, 11th refused) |
| **Priority / Severity / Automation** | High · High · Yes (functional — but see Notes) |
| **Role / Permission / Tenant** | Analyst with a full session · n/a — `[Authorize]` only; the limiter partitions on client IP, not on the actor (`RateLimiting.cs:97-98`) · `demo-lab` |
| **Environment** | ENV-A with the default `RateLimit:AuthPermitPerMinute = 10` (`RateLimiting.cs:24`) |
| **Preconditions** | `mfa_secret='JBSWY3DPEHPK3PXP'`, `mfa_enabled=false`; a full-session bearer; **no other `/api/auth/*` call from this source address in the current fixed minute window** (`RateLimiting.cs:51`). |
| **Test Data** | Eleven consecutive `POST /api/auth/mfa/confirm` `{"code":"000000"}` from one client address, inside one 60-second window. |
| **Steps** | 1. Wait for a fresh minute boundary. 2. Send requests 1–10 and record each status. 3. Send request 11 and record status plus the `Retry-After` header. 4. `SELECT mfa_enabled FROM qams.user_account WHERE email='analyst-nomfa@demo-lab.local';`. 5. Count new `audit.security_event` rows. |
| **Expected UI** | The SPA surfaces the generic error text; there is no dedicated throttling message on the MFA screen. |
| **Expected API** | Requests 1–10: `422` `code='MFA-003'`. Request 11: **`429 Too Many Requests`** with header `Retry-After: 60` (`RateLimiting.cs:55-61`), and **no** problem-details `code` extension — the limiter's `OnRejected` writes only the header and status. |
| **Expected DB** | `mfa_enabled` still `false`; request 11 never reaches `ActiveSessionMiddleware`, so it performs **no** `qams.user_account` read at all (`Program.cs:264-266` — `UseRateLimiter` precedes it). |
| **Expected Audit** | Step 5 returns `0` new rows for all eleven requests. |
| **Expected Notification** | n/a — throttling raises none. |
| **Cleanup** | Wait 60 seconds for the window to roll; `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false WHERE email='analyst-nomfa@demo-lab.local';` |
| **Evidence** | Eleven statuses in order with timestamps · the `Retry-After` header · the zero-row audit count |
| **Result / Defect** | Not Run · — |
| **Notes** | **Run this case last in any session** — it poisons the 10/min auth partition for the whole source address, which will make unrelated AUTH cases fail with `429`. The budget is shared with `/api/auth/login`, so a login attempt in the same minute counts toward the eleven. |

#### TC-AUTH-API-045 — `GET /api/tenant-settings/mfa-policy` as Tenant Administrator: 200 with the current flag  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-008 |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — the administrator reads their laboratory's MFA policy |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin on the seeded role `Tenant Administrator` · **`tenant-settings.manage`** — the class-level filter (`TenantSettingsController.cs:18`) applies to the GET too · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `admin@demo-lab.local` signed in with a full session; `role_id` points at the seeded `Tenant Administrator` role, which holds `PermissionCatalog.AllKeys` (`SystemRoleCatalog.cs:101-104`); `saas.tenant.require_mfa_privileged = false` for `demo-lab`. |
| **Test Data** | `GET /api/tenant-settings/mfa-policy` with the full-session bearer. |
| **Steps** | 1. Sign in and capture the token. 2. `GET /api/auth/me/privileges` and assert the returned key set contains `tenant-settings.manage`. 3. `GET /api/tenant-settings/mfa-policy`. 4. Compare the body with `SELECT require_mfa_privileged FROM saas.tenant WHERE identifier='demo-lab';`. 5. Repeat step 3 against the versioned mirror `GET /api/v1/tenant-settings/mfa-policy`. |
| **Expected UI** | `security-settings.component.ts:28,67-70` renders the "require MFA" toggle only when `perms.can('tenant-settings.manage')`, initialised from this response. |
| **Expected API** | Steps 3 and 5: `200` `application/json` `{"requireMfaForPrivilegedRoles":false}` — exactly one property (`TenantMfaPolicyDto`, `TenancyContracts.cs:11`). Both routes are in the approved surface (`ApiSurface.approved.txt:125,235`). |
| **Expected DB** | No write; `require_mfa_privileged` still `false`. |
| **Expected Audit** | No `audit.field_change` and no `audit.security_event` row — a query writes nothing. |
| **Expected Notification** | n/a — a read raises none. |
| **Cleanup** | n/a — read-only. |
| **Evidence** | Both HTTP captures · the privilege-key list from step 2 · the SQL value |
| **Result / Defect** | Not Run · — |
| **Notes** | The tenant is resolved from the JWT `tenant_id` claim only (`RequestIdentity.cs:57`); no header or query parameter can redirect this read to another tenant. |

#### TC-AUTH-API-046 — `GET /api/tenant-settings/mfa-policy` as Quality Manager: 403 AUTHZ-403  [ID — GAP-AUTH-902]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004, URS-005 · RSK-AUTH-008 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — §4.4 rule R7 (`[RequirePermission]` refusal) |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager on the seeded role `Quality Manager` · required `tenant-settings.manage`, **not held** — the seeded set excludes every `PermissionCatalog.TenantSettings` key (`SystemRoleCatalog.cs:109-110`) · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `qm-mfa@demo-lab.local` exists with `role='QualityManager'` and `role_id` = the seeded `Quality Manager` role; signed in with a full session. |
| **Test Data** | `GET /api/tenant-settings/mfa-policy` and `PUT /api/tenant-settings/mfa-policy` `{"require":true}`, both with the QM bearer. |
| **Steps** | 1. Sign in as the QM. 2. `GET /api/auth/me/privileges`; assert neither `tenant-settings.view` nor `tenant-settings.manage` is present. 3. `GET /api/tenant-settings/mfa-policy`. 4. `PUT /api/tenant-settings/mfa-policy` `{"require":true}`. 5. `SELECT require_mfa_privileged FROM saas.tenant WHERE identifier='demo-lab';`. |
| **Expected UI** | The MFA toggle is not rendered at all for this role (`security-settings.component.ts:28`); the section shows only the personal "set up MFA" action. |
| **Expected API** | Steps 3 and 4: `403` `application/problem+json`, `code='AUTHZ-403'`, `title='You do not have permission to perform this action.'` (`RequirePermissionAttribute.cs:54-59`). The GET is refused **by the same `Manage` filter as the PUT**. |
| **Expected DB** | `require_mfa_privileged` unchanged at `false`. |
| **Expected Audit** | No `audit.field_change` row; no `audit.security_event` row (privilege refusals are not written to the security ledger). |
| **Expected Notification** | n/a — a refused request raises none. |
| **Cleanup** | n/a — nothing changed. |
| **Evidence** | Both HTTP captures · the privilege-key list from step 2 · the SQL value |
| **Result / Defect** | Not Run · — |
| **Notes** | **Implementation-derived.** `tenant-settings.view` exists in the catalogue (`PermissionCatalog.cs:188`, `ConfigurationModule` bundle) and is grantable, but the controller carries only a class-level `Manage` filter, so no role can be given read-only visibility of the MFA policy. Raised as **GAP-AUTH-902**. |

#### TC-AUTH-API-047 — `PUT /api/tenant-settings/mfa-policy` `{"require":true}`: 204, column flips, next login is gated  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-008 |
| **Level / Type / Technique** | API · Integration (positive) · Use Case — the administrator switches the laboratory to enforced MFA, end to end |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional — mirrors `AuthActorFunctionalTests.cs:91-128`) |
| **Role / Permission / Tenant** | TenantAdmin · `tenant-settings.manage` at the HTTP filter, `[RequireInternalActor]` at the command layer (`TenantMfaPolicy.cs:11`) · `demo-lab` |
| **Environment** | ENV-A — **no restart**: unlike the config key, the tenant column is read on every login (`Login.cs:66`) |
| **Preconditions** | `require_mfa_privileged = false` for `demo-lab`; `admin@demo-lab.local` has `mfa_enabled=false` and a full session. |
| **Test Data** | `{"require":true}` (`SetTenantMfaPolicyRequest`, `TenancyContracts.cs:14`). |
| **Steps** | 1. `PUT /api/tenant-settings/mfa-policy` `{"require":true}`. 2. Read status. 3. `SELECT require_mfa_privileged FROM saas.tenant WHERE identifier='demo-lab';`. 4. `GET /api/tenant-settings/mfa-policy` and compare. 5. **Without restarting the API**, log in again as `admin@demo-lab.local` and read `mfaEnrollmentRequired`. 6. Read the newest `audit.field_change` rows for `entity_id` = the `demo-lab` tenant id. |
| **Expected UI** | The toggle in `security-settings.component.ts:78` flips and the component re-reads the policy. |
| **Expected API** | Step 1: `204 No Content`. Step 4: `200 {"requireMfaForPrivilegedRoles":true}`. Step 5: `200` with `mfaEnrollmentRequired: **true**` and **no** `Set-Cookie: qams_rt`. |
| **Expected DB** | `saas.tenant.require_mfa_privileged = true` for `demo-lab` and **unchanged for every other row** — assert `SELECT identifier, require_mfa_privileged FROM saas.tenant ORDER BY identifier;`. |
| **Expected Audit** | At least one `audit.field_change` row with `entity_type='TenantSettings'` and `entity_id` = the tenant id (`FieldChangeInterceptor.cs:167-182` drops `TenantId` from the rendered key, leaving the tenant id itself for this owned type). **Record the observed `action`, `property`, `old_value` and `new_value` verbatim** — `Tenant.SetPrivilegedMfaPolicy` replaces the owned value object with a `with`-expression (`Tenant.cs:107-108`), and whether EF surfaces that as a `Modified` diff on `RequireMfaForPrivilegedRoles` or as a `Deleted`+`Created` pair was **not** determined by source reading. See `GAP-AUTH-906`. |
| **Expected Notification** | n/a — no notification is defined for a settings change. |
| **Cleanup** | `PUT /api/tenant-settings/mfa-policy` `{"require":false}`; verify the column returns to `false`. |
| **Evidence** | HTTP captures for steps 1, 4 and 5 · the full `saas.tenant` flag listing · the field-change rows verbatim |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 5 is the load-bearing assertion: the policy takes effect on the **next login** with no restart and no cache, because `LoginHandler` reads `tenant.Settings` per request. Contrast TC-AUTH-DT-008, where the config key needs a restart. |

#### TC-AUTH-API-048 — `PUT /api/tenant-settings/mfa-policy` anonymously: 401 AUTH-401, no write  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004, URS-001 · RSK-AUTH-008 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — §4.4 rule R1 on a state-changing route |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | Anonymous · n/a — authentication refuses before the permission filter runs (`RequirePermissionAttribute.cs:43-46`) · none |
| **Environment** | ENV-A |
| **Preconditions** | `require_mfa_privileged = false` for `demo-lab`. |
| **Test Data** | `PUT /api/tenant-settings/mfa-policy` `{"require":true}` with **no** `Authorization` header. |
| **Steps** | 1. Issue the request. 2. Read status, `code`, `content-type`. 3. `SELECT require_mfa_privileged FROM saas.tenant WHERE identifier='demo-lab';`. 4. Repeat with `Authorization: Bearer <expired token>` (a token older than 15 minutes). |
| **Expected UI** | The SPA never reaches this state signed out; a stale tab attempts one silent refresh, then routes to `/login`. |
| **Expected API** | Both attempts: `401` `application/problem+json`, `code='AUTH-401'` (`ProblemAuthorizationResultHandler.cs:42-44`). |
| **Expected DB** | `require_mfa_privileged` still `false`. |
| **Expected Audit** | No `audit.field_change` row for `entity_type='TenantSettings'`. |
| **Expected Notification** | n/a — a refused request raises none. |
| **Cleanup** | n/a — nothing changed. |
| **Evidence** | Two HTTP captures · the SQL value before and after |
| **Result / Defect** | Not Run · — |
| **Notes** | Distinguish `401 AUTH-401` (no/!valid identity) from `403 AUTHZ-403` (identity without the privilege, TC-AUTH-API-046) — the SPA treats only the former as a session problem. |

#### TC-AUTH-API-049 — Platform admin on the tenant MFA policy: 422 TENANT-000  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-008 |
| **Level / Type / Technique** | API · Functional (negative) · Error Guessing — the tenant-less actor on a tenant-scoped setting |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | PlatformAdmin — `IUserPrivileges.Has` returns true for **every** key (`PrivilegeResolution.cs:39`), so the HTTP filter passes · effective permission `tenant-settings.manage` granted by the platform-admin bypass · **no tenant** |
| **Environment** | ENV-A |
| **Preconditions** | `platform-admin@localhost` signed in with a full session; the JWT carries **no** `tenant_id` claim, so `TenantResolutionMiddleware` sets nothing (`RequestIdentity.cs:57-61`). |
| **Test Data** | `GET /api/tenant-settings/mfa-policy`, then `PUT /api/tenant-settings/mfa-policy` `{"require":true}`, both with the platform-admin bearer. |
| **Steps** | 1. Sign in as platform admin (ENV-A → full session, TC-AUTH-DT-009). 2. `GET /api/tenant-settings/mfa-policy`; record status and `code`. 3. `PUT /api/tenant-settings/mfa-policy` `{"require":true}`; record status and `code`. 4. `SELECT identifier, require_mfa_privileged FROM saas.tenant ORDER BY identifier;`. |
| **Expected UI** | The platform shell has no tenant security-settings page; this is reachable only by direct API call. |
| **Expected API** | Both: `422 Unprocessable Entity` `application/problem+json`, `code='**TENANT-000**'`, `title='No tenant in context.'` (`TenantMfaPolicy.cs:19,31`; mapped by the catch-all `DomainException` arm). **Not** `403` — the permission gate passes for a platform admin and the handler fails afterwards. |
| **Expected DB** | Every `saas.tenant.require_mfa_privileged` value unchanged. |
| **Expected Audit** | No `audit.field_change` row for `entity_type='TenantSettings'`. |
| **Expected Notification** | n/a — a refused request raises none. |
| **Cleanup** | n/a — nothing changed. |
| **Evidence** | Both HTTP captures showing `TENANT-000` · the full tenant flag listing |
| **Result / Defect** | Not Run · — |
| **Notes** | The failure is a **422 business error**, not an authorization refusal — an operator reading only status codes would mis-triage it. There is no platform-level endpoint to set another tenant's MFA policy; the only path is a tenant admin's own session. |

#### TC-AUTH-API-050 — Tenant containment: one laboratory's MFA policy cannot touch another's  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004, URS-008 · RSK-AUTH-008 |
| **Level / Type / Technique** | API · Security (negative) · Pairwise — {tenant A, tenant B} × {read, write} across two authenticated sessions |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Two TenantAdmins, one per tenant · `tenant-settings.manage` in their own tenant only · `demo-lab` and a second provisioned tenant |
| **Environment** | ENV-A |
| **Preconditions** | A second tenant `mfa-lab-b` provisioned via `POST /api/tenants` with admin `admin@mfa-lab-b.test` / `Tenant-Admin-Pass-1!`; both tenants have `require_mfa_privileged = false`. |
| **Test Data** | Session A = `admin@demo-lab.local`; Session B = `admin@mfa-lab-b.test`. Body `{"require":true}`. |
| **Steps** | 1. Sign in on both sessions. 2. Session B: `PUT /api/tenant-settings/mfa-policy` `{"require":true}` → expect `204`. 3. `SELECT identifier, require_mfa_privileged FROM saas.tenant WHERE identifier IN ('demo-lab','mfa-lab-b');`. 4. Session A: `GET /api/tenant-settings/mfa-policy`. 5. Session A: log out and log back in; read `mfaEnrollmentRequired`. 6. Session B: log out and log back in; read `mfaEnrollmentRequired`. |
| **Expected UI** | Tenant A's security-settings toggle remains off; tenant B's shows on. |
| **Expected API** | Step 2: `204`. Step 4: `200 {"requireMfaForPrivilegedRoles":**false**}` — tenant A is unaffected. Step 5: `200`, `mfaEnrollmentRequired: false`, `Set-Cookie: qams_rt` present. Step 6: `200`, `mfaEnrollmentRequired: **true**`, no `Set-Cookie`. |
| **Expected DB** | `mfa-lab-b` → `t`; `demo-lab` → `f`. No other tenant row changed. |
| **Expected Audit** | One `audit.field_change` row for `entity_type='TenantSettings'` with `tenant_id` = **tenant B's** id (stamped from `ICurrentTenant`, `FieldChangeInterceptor.cs:154`) and `entity_id` = tenant B's id. Nothing under tenant A's id. |
| **Expected Notification** | n/a — no notification is defined for a settings change. |
| **Cleanup** | Session B: `PUT` `{"require":false}`. Optionally suspend the throwaway tenant; do **not** delete it (`saas.tenant` FKs are `ON DELETE RESTRICT`). |
| **Evidence** | The two-row SQL result · four HTTP captures · the single field-change row with tenant B's id |
| **Result / Defect** | Not Run · — |
| **Notes** | The containment here is the JWT `tenant_id` claim plus `GetTenantMfaPolicyHandler`'s explicit `t.Id == id` predicate (`TenantMfaPolicy.cs:20,32`), **not** RLS — `saas.tenant` is the tenant registry itself. Deeper `saas.tenant` isolation belongs to module `TENANT`. |

#### TC-AUTH-API-051 — An enrolment-scoped session cannot change the MFA policy that trapped it  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-005 |
| **Level / Type / Technique** | API · Security (negative) · Error Guessing — the privilege-escalation attempt an enrolment-gated administrator would actually try |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin holding `scope=mfa_enrollment` · would hold `tenant-settings.manage`, but the middleware refuses before the filter runs (`Program.cs:268` precedes `:270`) · `demo-lab` |
| **Environment** | ENV-A with `require_mfa_privileged=true` for `demo-lab` |
| **Preconditions** | An enrolment-scoped token for `admin@demo-lab.local`, obtained as in TC-AUTH-DT-004. |
| **Test Data** | `PUT /api/tenant-settings/mfa-policy` `{"require":false}` with the enrolment bearer; then the same against `/api/v1/tenant-settings/mfa-policy`. |
| **Steps** | 1. Obtain the enrolment token. 2. `PUT /api/tenant-settings/mfa-policy` `{"require":false}`. 3. `PUT /api/v1/tenant-settings/mfa-policy` `{"require":false}`. 4. Record status and `code` for both. 5. `SELECT require_mfa_privileged FROM saas.tenant WHERE identifier='demo-lab';`. |
| **Expected UI** | `/security/mfa-setup` stands alone outside the shell; the security-settings page is unreachable from it. |
| **Expected API** | Both: `403` `application/problem+json`, `code='MFA-ENROLL-REQUIRED'` — **not** `AUTHZ-403`, because the gate precedes MVC authorization filters. |
| **Expected DB** | `require_mfa_privileged` still `true` — the administrator cannot switch the policy off to escape enrolment. |
| **Expected Audit** | No `audit.field_change` row for `entity_type='TenantSettings'`. |
| **Expected Notification** | n/a — a refused request raises none. |
| **Cleanup** | `UPDATE saas.tenant SET require_mfa_privileged=false WHERE identifier='demo-lab';` |
| **Evidence** | Two HTTP captures · the SQL value before and after |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the containment property that makes the enrolment gate meaningful: the only escape is to complete enrolment (or for another administrator with a full session to flip the column). Combined with `GAP-AUTH-905`, a laboratory whose sole tenant admin loses their authenticator has **no** in-application recovery. |

#### TC-AUTH-API-052 — The allow-list is a prefix match: `/api/auth/mfa/enrollment-probe` passes the gate  [ID — GAP-AUTH-007(c)]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-004 · RSK-AUTH-005 |
| **Level / Type / Technique** | API · Security (as-built) · Error Guessing — prefix-boundary probing of `path.StartsWith(p, OrdinalIgnoreCase)` (`RequestIdentity.cs:187`) |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin holding `scope=mfa_enrollment` · n/a — the probe targets the gate, not a permission · `demo-lab` |
| **Environment** | ENV-A with `require_mfa_privileged=true` for `demo-lab` |
| **Preconditions** | A fresh enrolment-scoped token as in TC-AUTH-DT-004. |
| **Test Data** | Probe paths, all with the enrolment bearer: (a) `POST /api/auth/mfa/enrollment-probe` · (b) `POST /api/auth/mfa/enroll/../../../users` (unencoded) · (c) `POST /API/AUTH/MFA/ENROLL` (upper case) · (d) `POST /api/auth/mfa/enroll/` (trailing slash) · (e) `POST /api/auth/mfa/confirmation` . |
| **Steps** | 1. Obtain the enrolment token. 2. Issue each probe. 3. For each, record the status and whether the body is a `MFA-ENROLL-REQUIRED` problem or a routing outcome. 4. `SELECT mfa_secret IS NULL AS no_secret FROM qams.user_account WHERE email='admin@demo-lab.local';`. |
| **Expected UI** | n/a — a direct-API security probe with no UI surface. |
| **Expected API** | (a) and (e): the gate **permits** the request (the path starts with an allow-listed prefix) and MVC then answers `404` — the response is **not** a `403 MFA-ENROLL-REQUIRED` problem. (c): permitted by `OrdinalIgnoreCase` and routed case-insensitively, so `200` with an enrolment payload. (b): record the observed status — path normalisation happens before the middleware, so the effective path may or may not start with the prefix; capture what the server actually saw. (d): permitted; record whether MVC routes it to `EnrollMfa` (`200`) or `404`. |
| **Expected DB** | `mfa_secret` becomes non-null only for the probes that actually reached `EnrollMfaHandler` (at minimum (c)); record which. |
| **Expected Audit** | One `audit.field_change` row `property='MfaSecret'` per probe that reached the handler; none for the `404` outcomes. |
| **Expected Notification** | n/a — probes raise none. |
| **Cleanup** | `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false WHERE email='admin@demo-lab.local';` `UPDATE saas.tenant SET require_mfa_privileged=false WHERE identifier='demo-lab';` |
| **Evidence** | A five-row table of probe path → status → body kind → resulting `mfa_secret` state |
| **Result / Defect** | Not Run · — |
| **Notes** | No **privilege** escapes today because no route exists behind those prefixes — the finding is that the containment boundary is a string prefix, not a route match. This is exactly `GAP-AUTH-007` acceptance criterion (c) ("exact-segment matching"). The expected outcomes for (b) and (d) are deliberately written as *record what happens*: they were not determined by source reading, and inventing them would violate the honesty rules. |

#### TC-AUTH-API-053 — No API path disables or resets another user's MFA  [GD — GAP-AUTH-905]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · no URS covers MFA recovery — `URS-009` names create/assign-role/reset-password/deactivate only · RSK-AUTH-007 |
| **Level / Type / Technique** | API · Functional (negative, absence-of-capability) · Use Case — the administrator's recovery path for a lost authenticator |
| **Priority / Severity / Automation** | High · High · No (surface-inventory assertion; re-run by the API-surface merge gate) |
| **Role / Permission / Tenant** | TenantAdmin holding `users.manage` · `users.manage` (`UsersController.cs:27-90`) · `demo-lab` |
| **Environment** | ENV-A |
| **Preconditions** | `analyst-mfa@demo-lab.local` has `mfa_secret='JBSWY3DPEHPK3PXP'`, `mfa_enabled=true`; the administrator is signed in with a full session and holds `users.manage`. |
| **Test Data** | Every write route under `/api/users` from the approved surface: `POST /api/users`, `POST /api/users/{id}/role`, `PUT /api/users/{id}/assigned-role`, `PUT /api/users/{id}/scope`, `PUT /api/users/{id}/language`, `POST /api/users/{id}/deactivate`, `POST /api/users/{id}/reactivate`, `POST /api/users/{id}/reset-password` body `{"newPassword":"Recovery-Pass-2026!"}`. |
| **Steps** | 1. `grep -i mfa tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` and assert the only matches are `POST /api/auth/mfa/enroll`, `POST /api/auth/mfa/confirm`, `GET|PUT /api/tenant-settings/mfa-policy` and their `/api/v1/` mirrors. 2. Execute `POST /api/users/{id}/deactivate`, then `/reactivate`, then `/reset-password` against the analyst. 3. After each, `SELECT mfa_secret IS NOT NULL AS has_secret, mfa_enabled FROM qams.user_account WHERE email='analyst-mfa@demo-lab.local';`. |
| **Expected UI** | `users.component.ts` exposes no MFA-reset action; the users grid shows `mfaEnabled` (`UserDto`, `UserContracts.cs:11`) as read-only information. |
| **Expected API** | Step 1: exactly six matching lines, none of which resets MFA. Step 2: `204` from deactivate/reactivate/reset-password (each succeeds at its own job). |
| **Expected DB** | After **every** step: `has_secret = true` and `mfa_enabled = true` — no `/api/users` operation clears either column (`SetUserActiveHandler`, `UserManagement.cs:131-140`; `ResetUserPasswordHandler`, `:147` touch neither). |
| **Expected Audit** | `audit.field_change` rows for `IsActive` and `PasswordHash` (redacted) only; **no** row with `property IN ('MfaSecret','MfaEnabled')`. |
| **Expected Notification** | n/a — no notification is defined for these administrative actions. |
| **Cleanup** | `UPDATE qams.user_account SET mfa_secret=NULL, mfa_enabled=false, password_hash=<original> WHERE email='analyst-mfa@demo-lab.local';` — or simply recreate the fixture account. |
| **Evidence** | The `ApiSurface.approved.txt` grep output · the SQL result after each of the three operations · the field-change rows showing no MFA property |
| **Result / Defect** | Not Run · — |
| **Notes** | **Gap-dependent.** This case asserts an **absence**, which is legitimate evidence for `GAP-AUTH-905` but must be re-authored as a positive case the moment an unlock/MFA-reset endpoint exists. Read together with `GAP-AUTH-013` (no administrative unlock): a user who is locked *and* has lost their authenticator has no in-application recovery whatsoever. |

---

## Batch coverage note

**Covered.** 38 complete cases across the two reserved blocks — `TC-AUTH-DT-001…014` and `TC-AUTH-API-030…053`, all with `Result / Defect = Not Run · —`.

- **Login decision table, MFA rows** — §4.1 R8 (challenge issued, empty token, no cookie), R9 (bad code, `AUTH-005`, counter advances), R10 (full session), R11 on both the tenant path and the platform-admin path, and R12 in its two distinct shapes (role predicate false; policy false). The three-term conjunction at `Login.cs:120-122` has each term flipped individually (DT-004/005/006/007).
- **Both policy sources and their precedence** — the per-tenant `saas.tenant.require_mfa_privileged` column, the platform-admin fallback `Security:RequireMfaForPrivilegedRoles`, the fact that the config key is a composition-time singleton needing a restart while the column is read per login, and the unconditional overwrite at `Login.cs:66` that makes the global key inert for every tenant user (DT-010).
- **Enrolment-scoped containment** — allow-list positive (DT-011), the negative sweep over six routes (DT-012), the logout dead-end in both its contradictory forms (DT-013/014), the escalation attempt against the policy endpoint itself (API-051), and the prefix-matching probe set (API-052).
- **Enrol / confirm ceremony** — payload contract and `otpauth://` URI (API-030), anonymous refusal (API-031), the silent MFA downgrade (API-032), secret replacement (API-033), success with `MFA_ENABLED` (API-034), `MFA-003` on a wrong code (API-035), the proof that `MFA-002` is unreachable (API-036), the four-point clock-skew boundary set `{−2, −1, +1, +2}` (API-037…040), code replay at both confirm and login (API-041/042), the seven input partitions (API-043), and the 10/min boundary with `Retry-After: 60` (API-044).
- **Tenant-settings MFA policy** — read as `Tenant Administrator` (API-045), refusal for `Quality Manager` on **both** verbs (API-046), the write with its end-to-end effect on the next login (API-047), anonymous and expired-token refusal (API-048), the tenant-less platform admin's `TENANT-000` (API-049), and cross-tenant containment (API-050).

**Not covered, and why.**

1. **A truly deterministic TOTP oracle.** Every window case pins the boundary by recording `floor(epoch/30)` around the call rather than by freezing the clock. `IClock` is injectable but not overridable over HTTP, so a request that straddles a step boundary silently changes which rule it exercises. Cases 037–040 therefore carry an explicit re-run condition instead of a pure assertion. A `WebApplicationFactory`-hosted variant with a fake `IClock` would remove the flakiness; that belongs with the functional-test project, not this HTTP-level batch.
2. **The exact audit shape of a tenant MFA-policy change** (API-047, Expected Audit). `Tenant.SetPrivilegedMfaPolicy` replaces an owned value object with a `with`-expression; whether EF Core emits a `Modified` property diff or a `Deleted`+`Created` pair for a table-split owned type was not resolvable by reading the source. The case instructs the executor to record what is observed rather than assert an invented shape. Raised as `GAP-AUTH-906`.
3. **Probes (b) and (d) of API-052** (dot-segment and trailing slash). ASP.NET Core path normalisation relative to middleware ordering was not read in this pass, so the expected outcomes are written as *record what happens*.
4. **MFA in the e-signature ceremony.** Whether an MFA-enabled signer is re-challenged at `POST /api/documents/{id}/publish` is out of this batch by assignment (the signing ceremony is batch **D**); reading `ComplianceLedgerServices.cs:86-144` shows no TOTP check there, but the case belongs with the e-signature slice.
5. **Browser-level MFA flows** (QR rendering, clipboard copy, the `/security/mfa-setup` route under an enrolment session, RTL/a11y of the code input) — reserved for batch **F** (`TC-AUTH-E2E-*`, `TC-AUTH-A11Y-*`).
6. **`MFA-001` and `MFA-002` at unit level** — reserved for batch **A** (`TC-AUTH-UNIT-*`); the front matter proves both unreachable over HTTP, so no API case was fabricated for them.

**New gaps found in this slice** (numbered `9xx` so they do not collide with the front matter's `GAP-AUTH-001…016`):

- **GAP-AUTH-901 — TOTP codes are not single-use.** `TotpService.Verify` (`src/NT.QAMS.Infrastructure/Security/TotpService.cs:24-44`) compares against three counters and keeps no record of accepted codes; no store for consumed OTPs exists anywhere in `src/`. A code therefore remains valid for its whole ±1-step window (up to 90 s) and any number of submissions, at both `POST /api/auth/mfa/confirm` and `POST /api/auth/login`. RFC 6238 §5.2 requires the verifier to reject a previously accepted OTP. *Severity: Major.* *Acceptance criteria:* (a) a per-user record of the highest accepted counter (or a short-lived consumed-code set) is persisted and checked before acceptance; (b) a replay inside the same window returns `401 AUTH-005` at login and `422 MFA-003` at confirm; (c) an integration case proves the second submission of one code fails while a code from the next step succeeds. Evidence: TC-AUTH-API-041, TC-AUTH-API-042.
- **GAP-AUTH-902 — `tenant-settings.view` gates nothing.** `TenantSettingsController` carries a single class-level `[RequirePermission(PermissionCatalog.TenantSettings, PermissionAction.Manage)]` (`src/NT.QAMS.WebApi/Controllers/TenantSettingsController.cs:18`) with no method override, so `GET /api/tenant-settings/mfa-policy` requires `tenant-settings.manage`. The `ConfigurationModule` bundle registers `tenant-settings.view` as a grantable key (`PermissionCatalog.cs:125-126,188`) that no endpoint honours, so a laboratory cannot grant read-only visibility of its own security policy. *Severity: Minor.* *Acceptance criteria:* (a) the GET carries `[RequirePermission(TenantSettings, View)]` and the PUT keeps `Manage`; (b) a role holding only `tenant-settings.view` receives `200` on the GET and `403 AUTHZ-403` on the PUT; (c) or, if read-only visibility is unwanted, `View` is removed from the module's bundle so no inert key is grantable. Evidence: TC-AUTH-API-046.
- **GAP-AUTH-903 — MFA-confirm failures are unvalidated, uncounted and unlogged.** `ConfirmMfaCommand` has no FluentValidation validator (grep of `src/`: only `SetPinValidator` and `LoginValidator` exist in `MfaAndPin.cs` / `Login.cs`), so any string reaches `TotpService.Verify`; `ConfirmMfaHandler` (`MfaAndPin.cs:41-55`) never calls `UserAccount.RegisterFailedLogin` and writes no `audit.security_event` row on failure. Confirm-code guessing is therefore bounded only by the 10/min per-IP `AuthPolicy` and leaves no forensic trace, whereas the equivalent failure at `/api/auth/login` both counts toward the 5-attempt lockout and writes `LOGIN_FAILED` (`Login.cs:111-113`). This is the MFA sibling of `GAP-AUTH-009`. *Severity: Major.* *Acceptance criteria:* (a) a validator rejects anything that is not exactly six digits with `400` and the FluentValidation `errors` envelope; (b) a failed confirmation calls `RegisterFailedLogin` and persists it; (c) a failed confirmation writes a distinguishable security event (e.g. `MFA_CONFIRM_FAILED`); (d) a functional case proves five wrong confirm codes lock the account. Evidence: TC-AUTH-API-035, TC-AUTH-API-043.
- **GAP-AUTH-904 — Self-service MFA downgrade is silent and unlogged.** `POST /api/auth/mfa/enroll` on an already-enabled account sets `MfaEnabled = false` unconditionally (`src/NT.QAMS.Domain/IdentityAccess/UserAccount.cs:227-236`), with no confirmation step, no re-authentication, and no `audit.security_event` row — `MFA_ENABLED` is written only on confirm (`MfaAndPin.cs:54`). A single POST from a live session removes the second factor, and the security ledger shows nothing. *Severity: Major.* *Acceptance criteria:* (a) re-enrolment stores a **pending** secret and leaves `MfaEnabled` true until the new secret is confirmed; (b) starting a re-enrolment writes a security event (e.g. `MFA_REENROLL_STARTED`); (c) turning MFA off writes `MFA_DISABLED`; (d) a functional case proves the old code still works between re-enrol and confirm. Evidence: TC-AUTH-API-032.
- **GAP-AUTH-905 — No administrative MFA reset or disable.** The approved API surface contains exactly six MFA lines (`ApiSurface.approved.txt:125,235,262,263,455,456,642,655` — enroll/confirm and the tenant policy, plus mirrors); none clears `mfa_secret` or `mfa_enabled` for another user, and none of the nine `/api/users` operations touches those columns. A user who loses their authenticator can only be recovered by direct SQL. Combined with `GAP-AUTH-013` (no administrative unlock), a locked user with a lost authenticator has no in-application recovery, and in a single-admin tenant with `require_mfa_privileged=true` the laboratory can lock itself out entirely. *Severity: Major.* *Acceptance criteria:* (a) `POST /api/users/{id}/mfa-reset` gated by `users.manage`, calling a new `UserAccount.ResetMfa()` that clears both columns and raises an auditable event; (b) the reset writes an `audit.security_event` row naming both actor and subject; (c) the operation is refused when the actor is the subject (self-service reset must go through re-enrolment); (d) a functional case proves the reset user is re-gated to enrolment on their next login while the tenant policy is on. Evidence: TC-AUTH-API-053, TC-AUTH-API-051.
- **GAP-AUTH-906 — The audit shape of a tenant MFA-policy change is undetermined.** `Tenant.SetPrivilegedMfaPolicy` replaces the owned `TenantSettings` value object wholesale (`src/NT.QAMS.Domain/Tenancy/Tenant.cs:107-108`); `FieldChangeInterceptor` diffs per **property** on `Modified` entries but writes a single valueless row on `Added`/`Deleted` (`FieldChangeInterceptor.cs:66-77`). Whether a Part-11 reviewer sees `RequireMfaForPrivilegedRoles: False → True` or an untyped create/delete pair for a table-split owned type was not resolvable by source reading, and it decides whether URS-011's "old/new values" obligation is met for every tenant setting, not just this one. *Severity: Moderate (evidence deficit).* *Acceptance criteria:* (a) the observed shape is measured and recorded; (b) if it is not a property-level diff, `SetPrivilegedMfaPolicy` mutates in place (or the interceptor special-cases replaced owned references) so old/new values appear; (c) an integration case asserts one `audit.field_change` row with `property='RequireMfaForPrivilegedRoles'`, `old_value='False'`, `new_value='True'`. Evidence: TC-AUTH-API-047.

**Correction carried forward.** Nothing in this batch contradicts `10-module-auth.md`; the front matter's §0 corrections to ground truth were applied as written (in particular, no case asserts a locked account with `failed_attempts = 5`, and the column is cited as `failed_login_attempts` throughout).
