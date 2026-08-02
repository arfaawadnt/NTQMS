# AUTH — Detailed Test Cases, Batch C

This batch authors the **ADR-0009 session model** slice of module `AUTH`: the access-JWT lifetime and its memory-only handling in the SPA, the `qams_rt` refresh cookie and its four hardening attributes (`HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`), SHA-256-only storage of the refresh secret in `qams.refresh_session`, rotation on every `POST /api/auth/refresh`, reuse detection with family revocation, logout revocation, revoked-token reuse, concurrent (multi-family) sessions, the `ActiveSessionMiddleware` re-check that yields `401 AUTH-006` (inactive account) and `401 AUTH-007` (token role ≠ DB role), and — because the code does **not** implement them — session listing, administrative session revocation, self-revocation of an individual session, and a cross-tenant session-revocation attempt, which are written `[GD]` against a new gap. It consumes ids `TC-AUTH-SEC-001…-025` and `TC-AUTH-STATE-001…-016`. It deliberately leaves to sibling batches: all `UserAccount`/`PasswordRules`/`UserAccessReview` unit and boundary cases and the login decision table (batch A/B), the full endpoint × failure-code sweep and the four decision tables of front-matter §4 (batch B), the remaining `SEC` scope of rate-limit partitions on the credential and e-signature surfaces, anti-enumeration, PIN brute force, JWT claim tampering and `scope=mfa_enrollment` containment plus every `RLS` case (batch D — see the ID-block note in `## Batch coverage note`), the credential data-flow, MC/DC and observability cases (batch E), and all E2E/UAT/A11Y/PERF/EXPL cases (batch F).

Conventions are those of `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md` §4, §5 and §8. Every `[IV]`/`[ID]` claim cites `file:line` in the row that makes it. Risk ids are **minted** `RSK-AUTH-03x`: `docs/validation/02-Functional-Risk-Assessment.md:52-58` is area-level only and defines no item-level risk ids, so there is nothing to reuse (conventions §5). Requirement traceability for the session model itself is thin by construction — `URS-001…URS-055` contain no requirement describing refresh-token rotation, reuse detection or cookie hardening; those cases trace to ADR-0009 plus the source line and are counted in `GAP-AUTH-902`.

**Minted risk ids used in this batch**

| Risk ID | Statement |
|---|---|
| `RSK-AUTH-030` | A refresh token captured from the browser, the wire or the database is replayed to mint an unlimited series of access tokens. |
| `RSK-AUTH-031` | A withdrawn account or a changed role keeps working because revocation is lazy, so access outlives the administrative decision. |
| `RSK-AUTH-032` | Credential material (access token, refresh secret) is persisted somewhere durable — web storage, a log, a database column — and is later read. |
| `RSK-AUTH-033` | Neither the user nor an administrator can see or terminate the sessions that exist, so a compromise cannot be contained or evidenced. |
| `RSK-AUTH-034` | Legitimate concurrent use (two tabs, two devices) is mistaken for theft and terminates a working session, or theft is mistaken for concurrency and is not terminated. |
| `RSK-AUTH-035` | The reuse-detection window closes before the theft is exercised, so the family is never revoked and no security event is written. |

---

## Detailed cases

#### TC-AUTH-SEC-001 — `qams_rt` carries all four hardening attributes on the login `Set-Cookie`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · no URS covers the refresh cookie — ADR-0009 §Decision.2 (`docs/adr/ADR-0009-refresh-token-session-model.md:22-26`), gap `GAP-AUTH-902` · `RSK-AUTH-030` |
| **Level / Type / Technique** | API · Security (positive) · Decision Table — one condition per cookie attribute, all four asserted in one observation |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional — extends `tests/NT.QAMS.WebApi.FunctionalTests/RefreshSessionTests.cs:47-62`) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — `POST /api/auth/login` is `[AllowAnonymous]` (`AuthController.cs:29-30`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; use `curl.exe -i` — PowerShell 5.1 drops manual `Cookie`/`Set-Cookie` handling (conventions §3) |
| **Preconditions** | `admin@demo-lab.local` is `is_active=true`, `locked_until_utc IS NULL`, `mfa_enabled=false`; tenant `demo-lab` is `Active`; `Security:RequireMfaForPrivilegedRoles=false` so login yields a **full** session with a grant (`Login.cs:120-136`) |
| **Test Data** | `{"tenantIdentifier":"demo-lab","email":"admin@demo-lab.local","password":"Demo-Admin-Pass-2!","mfaCode":null}` |
| **Steps** | 1. `curl.exe -i -X POST http://localhost:5080/api/auth/login -H "Content-Type: application/json" --data "@login.json"`. 2. Capture the full `Set-Cookie` response header. 3. Assert the header begins `qams_rt=`. 4. Assert it contains, case-insensitively, `httponly`, `secure`, `samesite=strict` and `path=/api/auth`. 5. Assert it contains an `expires=` value 14 days ahead (`Auth:RefreshTokenDays` default 14, `Infrastructure/DependencyInjection.cs:92-94`). 6. Assert the JSON body's `accessToken` is non-empty and that **no** `Set-Cookie` header carries the access token. |
| **Expected UI** | n/a — API-level case; the browser surface is covered by `TC-AUTH-SEC-004`. |
| **Expected API** | `200` `application/json`; exactly one `Set-Cookie` header, matching `^qams_rt=[0-9a-f]{32}\.[A-Za-z0-9_-]{43}; expires=…; path=/api/auth; secure; samesite=strict; httponly$` (attribute order is Kestrel's, assert by containment not by order) — attributes set at `AuthController.cs:92-100`, name and path constants at `:26-27`. |
| **Expected DB** | Exactly one new row in `qams.refresh_session` for that `user_id`: `revoked_at_utc IS NULL`, `replaced_by_id IS NULL`, `expires_at_utc = created_at_utc + 14 days`, `family_id` freshly minted (`Login.cs:131-135`). |
| **Expected Audit** | One `audit.security_event` row, `event_type='LOGIN_SUCCESS'`, `actor='admin@demo-lab.local'`, `detail IS NULL`, `tenant_id` = the `demo-lab` id (`Login.cs:139`). Read it with `SELECT set_config('app.bypass_rls','on',false);` first. |
| **Expected Notification** | n/a — no notification policy subscribes to any AUTH security event (no handler for `UserLockedOut`/`UserRoleAssigned`/`UserScopeChanged` exists in `src/NT.QAMS.Application`). |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = (SELECT id FROM qams.user_account WHERE email='admin@demo-lab.local');` |
| **Evidence** | Raw HTTP response capture including headers · `SELECT * FROM qams.refresh_session` result · security-event row |
| **Result / Defect** | Not Run · — |
| **Notes** | `Secure` is emitted even on plain HTTP by Kestrel; a browser will only store it because `localhost` is a secure context. Do not conclude from an HTTP-only dev run that the attribute is honoured in a browser — that is `TC-AUTH-SEC-004`. |

#### TC-AUTH-SEC-002 — Rotation re-emits the cookie with identical hardening attributes  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · ADR-0009 §Decision.3 (`ADR-0009…md:27-31`), gap `GAP-AUTH-902` · `RSK-AUTH-030` |
| **Level / Type / Technique** | API · Security (positive) · State Transition — `Live → Rotated`, asserting the *new* cookie is as hardened as the first |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — `POST /api/auth/refresh` is `[AllowAnonymous]`; the cookie is the credential (`AuthController.cs:64-72`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; `curl.exe` with an explicit `-H "Cookie: qams_rt=…"` |
| **Preconditions** | A `Live` session exists from `TC-AUTH-SEC-001`; its token value `T1` is held out of band |
| **Test Data** | `T1` = the `qams_rt` value captured at login |
| **Steps** | 1. `curl.exe -i -X POST http://localhost:5080/api/auth/refresh -H "Cookie: qams_rt=T1"`. 2. Capture the response `Set-Cookie` as `T2`. 3. Assert `T2 != T1`. 4. Assert `T2`'s attribute set equals `T1`'s (`httponly`, `secure`, `samesite=strict`, `path=/api/auth`). 5. Assert the `T2` session-id prefix (chars before the `.`) differs from `T1`'s. 6. `SELECT id, family_id, revoked_at_utc, replaced_by_id FROM qams.refresh_session ORDER BY created_at_utc;`. |
| **Expected UI** | n/a — silent refresh is invisible; the SPA path is `TC-AUTH-SEC-005`. |
| **Expected API** | `200` with a fresh `accessToken`, `mfaRequired:false`; one `Set-Cookie` for `qams_rt` with the same four attributes and a new `expires` = now + 14 days (`AuthController.cs:88-104`; grant built at `RefreshSessions.cs:129-140`). |
| **Expected DB** | Two rows sharing one `family_id`. Row 1 (`T1`): `revoked_at_utc` set to the refresh instant, `replaced_by_id` = row 2's `id` (`RefreshSession.Rotate`, `RefreshSession.cs:77-86`). Row 2 (`T2`): `revoked_at_utc IS NULL`, `replaced_by_id IS NULL`, `expires_at_utc` = refresh instant + 14 days. |
| **Expected Audit** | **No** `audit.security_event` row — the successful-refresh path writes none (`RefreshSessions.cs:127-141` has no `security.WriteAsync` call). Assert the count of `audit.security_event` rows is unchanged. |
| **Expected Notification** | n/a — no notification is defined for session rotation. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE family_id = '<captured family_id>';` |
| **Evidence** | Two HTTP captures · the two-row SQL result showing the rotation link |
| **Result / Defect** | Not Run · — |
| **Notes** | `T1` must be preserved for `TC-AUTH-SEC-012` / `TC-AUTH-STATE-002`; do not clean up before those run. |

#### TC-AUTH-SEC-003 — Logout deletes the cookie on the matching `Path`, not the default path  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-006 (`docs/validation/01-User-Requirements-Specification.md:29`) · `RSK-AUTH-030` |
| **Level / Type / Technique** | API · Security (positive) · Error Guessing — a `Set-Cookie` deletion with a mismatched `Path` silently leaves the cookie in the browser |
| **Priority / Severity / Automation** | High · High · Yes (functional — extends `RefreshSessionTests.cs:107-123`) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — `POST /api/auth/logout` is `[AllowAnonymous]` (`AuthController.cs:75-77`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | A `Live` session with token `T1` exists |
| **Test Data** | `T1` |
| **Steps** | 1. `curl.exe -i -X POST http://localhost:5080/api/auth/logout -H "Cookie: qams_rt=T1"`. 2. Assert the status. 3. Parse the `Set-Cookie` header: assert the value part is empty, `path=/api/auth` is present, and `expires` is in the past (the ASP.NET Core deletion form). 4. Re-`POST /api/auth/refresh` with `T1`. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Step 2: `204 No Content` with `Set-Cookie: qams_rt=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/api/auth` (`AuthController.cs:80` passes `new CookieOptions { Path = RefreshCookiePath }`). Step 4: `401` `application/problem+json`, `code` = `AUTH-008` — a logout-revoked token re-presented at `/refresh` takes the **reuse** branch, not an expiry branch (`RefreshSessions.cs:101-111`). |
| **Expected DB** | Every row of that `family_id` has `revoked_at_utc` non-null after step 1 (`RefreshSessions.cs:173-176`); `replaced_by_id` stays `NULL` on the logged-out link (revocation, not rotation). |
| **Expected Audit** | One row `event_type='LOGOUT'`, `actor IS NULL`, `detail='family=<familyId:N>'`, **`tenant_id IS NULL`** — `LogoutHandler` passes `null` for both tenant and actor (`RefreshSessions.cs:177`). Then a second row `event_type='REFRESH_REUSE_DETECTED'`, `detail='family=<familyId:N>'`, `tenant_id IS NULL` from step 4 (`:108-109`). |
| **Expected Notification** | n/a — no notification is defined for logout. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE family_id = '<familyId>';` |
| **Evidence** | HTTP captures for both calls · `SELECT id, revoked_at_utc, replaced_by_id FROM qams.refresh_session WHERE family_id=…` · both security-event rows |
| **Result / Defect** | Not Run · — |
| **Notes** | The step-4 `AUTH-008` is the as-built behaviour and is arguably wrong signalling — a user who logged out and whose stale cookie is retried is reported as a theft event. Captured as an observation, not a defect, in the batch note. |

#### TC-AUTH-SEC-004 — In a browser the cookie rides `/api/auth/*` only, and script cannot read it  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · ADR-0009 §Decision.2 and §Consequences (`ADR-0009…md:22-26, 44-51`), gap `GAP-AUTH-902` · `RSK-AUTH-030`, `RSK-AUTH-032` |
| **Level / Type / Technique** | E2E (browser) · Security (positive) · Equivalence Partitioning — request paths partition into `/api/auth/*` (cookie attached) and everything else (not attached) |
| **Priority / Severity / Automation** | Critical · Critical · Yes (Playwright — the `auth` e2e spec is the natural home) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — cookie behaviour is transport-level · `demo-lab` |
| **Environment** | SPA `localhost:4200` + API `:5080`, both started via `scripts/dev-up.ps1`; Chromium via Playwright |
| **Preconditions** | Both dev servers healthy per `scripts/dev-status.ps1`; the account signs in without MFA |
| **Test Data** | `admin@demo-lab.local` / `Demo-Admin-Pass-2!` at `localhost:4200/t/demo-lab` |
| **Steps** | 1. Sign in through the SPA. 2. Evaluate `document.cookie` in the page and assert the string contains no `qams_rt` substring (`HttpOnly`). 3. Read the browser context's cookie jar via the automation API and assert one `qams_rt` cookie with `httpOnly=true`, `secure=true`, `sameSite='Strict'`, `path='/api/auth'`. 4. Record the network trace while navigating to a permission-gated page that calls `GET /api/users/directory`; assert that request carries **no** `Cookie: qams_rt`. 5. Trigger a silent refresh (wait past the access-token expiry, or call `POST /api/auth/refresh` from the app); assert that request **does** carry `Cookie: qams_rt`. |
| **Expected UI** | Sign-in succeeds and the dashboard renders; step 4's page loads normally with data, proving bearer-token auth is what carries non-auth requests. |
| **Expected API** | Step 4's `GET /api/users/directory` returns `200` on the `Authorization: Bearer …` header alone. Step 5's `POST /api/auth/refresh` returns `200` with a rotated `Set-Cookie`. |
| **Expected DB** | Step 5 adds one successor row in the same `family_id` and stamps `replaced_by_id` on the presented row. |
| **Expected Audit** | No `audit.security_event` rows beyond the single `LOGIN_SUCCESS` from step 1. |
| **Expected Notification** | n/a — no notification is defined for sign-in. |
| **Cleanup** | Sign out in the SPA (revokes the family), then `DELETE FROM qams.refresh_session WHERE user_id = …;` |
| **Evidence** | Playwright trace (HAR) showing the two contrasting requests · cookie-jar dump · `document.cookie` assertion output |
| **Result / Defect** | Not Run · — |
| **Notes** | `SameSite=Strict` cannot be exercised cross-site in this deployment — ADR-0002/0007 make the SPA same-origin, so there is no legitimate cross-site request to test with. Assert the attribute's presence, not its effect. |

#### TC-AUTH-SEC-005 — The access token exists only in SPA memory: absent from `localStorage`, `sessionStorage` and cookies  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · ADR-0009 §Decision.1 (`ADR-0009…md:19-21`), gap `GAP-AUTH-902` · `RSK-AUTH-032` |
| **Level / Type / Technique** | E2E (browser) · Security (positive) · Data Flow — define at `AuthService.apply`, use at the interceptor, assert no durable store is written on the path |
| **Priority / Severity / Automation** | Critical · Critical · Yes (Playwright + the existing `auth.service.spec.ts` Karma spec) |
| **Role / Permission / Tenant** | Analyst · n/a — client-side storage concern · `demo-lab` |
| **Environment** | SPA `localhost:4200` + API `:5080` |
| **Preconditions** | Browser storage cleared before the run |
| **Test Data** | `admin@demo-lab.local` / `Demo-Admin-Pass-2!` |
| **Steps** | 1. Clear `localStorage`, `sessionStorage` and cookies. 2. Sign in. 3. Enumerate every `localStorage` key/value and every `sessionStorage` key/value. 4. Assert the only `localStorage` entry written by the app is the tenant-slug key and that its value is `demo-lab` (`auth.service.ts:44,50`). 5. Assert no storage value and no cookie value parses as a three-segment JWT (`^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$`). 6. Reload the page (F5) and assert the session is restored **and** that storage still holds no JWT — restoration came from the cookie via `hydrate()` (`auth.service.ts:100-105`). |
| **Expected UI** | After the reload the user stays signed in; no sign-in form is shown; the display name still renders. |
| **Expected API** | The reload issues exactly one `POST /api/auth/refresh` (single-flight, `auth.service.ts:83-97`) returning `200` with a new `accessToken`. |
| **Expected DB** | One additional `qams.refresh_session` row per reload, in the same `family_id`. |
| **Expected Audit** | No security-event row for the refresh. |
| **Expected Notification** | n/a — no notification is defined for session hydration. |
| **Cleanup** | Sign out; `DELETE FROM qams.refresh_session WHERE user_id = …;` |
| **Evidence** | Storage dump before/after · reload trace · screenshot of the retained session |
| **Result / Defect** | Not Run · — |
| **Notes** | The token is held in an Angular `signal<Session\|null>` (`auth.service.ts:29`) and exposed by the `token` getter (`:60-62`); an XSS can still read it while the page lives — that is the accepted residual in ADR-0009 §Consequences, not a defect to raise. |

#### TC-AUTH-SEC-006 — The access token's lifetime is exactly 900 seconds  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · ADR-0009 §Decision.1 (`ADR-0009…md:19`), gap `GAP-AUTH-902` · `RSK-AUTH-031` |
| **Level / Type / Technique** | API · Functional (positive) · BVA — the configured boundary `Jwt:ExpiryMinutes` at its default value |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — token issuance is pre-authorization · `demo-lab` |
| **Environment** | API `:5080` Development, `Jwt:ExpiryMinutes` **unset** so `ConfigGuard.ReadInt(…, 15)` applies (`SecurityAdapters.cs:59`, default at `:37`) |
| **Preconditions** | No `Jwt__ExpiryMinutes` environment variable and no user-secret override is present |
| **Test Data** | `admin@demo-lab.local` / `Demo-Admin-Pass-2!` |
| **Steps** | 1. `POST /api/auth/login`. 2. Base64url-decode the JWT payload. 3. Assert `exp - nbf == 900`. 4. Assert `exp - iat` is within 1 s of 900. 5. Assert the body's `expiresAtUtc` equals the `exp` claim converted to UTC. 6. Assert the claim set is exactly `{sub, email, name, http://schemas.microsoft.com/ws/2008/06/identity/claims/role, scope, tenant_id, nbf, exp, iat, iss, aud}` and that `scope == "full"` (`SecurityAdapters.cs:83-95`). |
| **Expected UI** | n/a — token internals are not surfaced. |
| **Expected API** | `200`; `alg` header `HS256` (`SecurityAdapters.cs:69-71`); `iss` and `aud` both `nt-qams` unless configured (`:56-57`). |
| **Expected DB** | One new `qams.refresh_session` row; nothing about the access token is persisted anywhere. |
| **Expected Audit** | One `LOGIN_SUCCESS` row. |
| **Expected Notification** | n/a — no notification is defined for token issuance. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = …;` |
| **Evidence** | Decoded JWT header + payload JSON · the arithmetic assertion output |
| **Result / Defect** | Not Run · — |
| **Notes** | `notBefore` is `clock.UtcNow` with no skew allowance at issuance (`SecurityAdapters.cs:101`); validation-side clock skew is the framework default and is **not** asserted here — do not claim a skew tolerance that was not read. |

#### TC-AUTH-SEC-007 — An expired access token is refused while its refresh cookie still works  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-001 (`01-User-Requirements-Specification.md:24`); ADR-0009 §Decision.4 · `RSK-AUTH-030`, `RSK-AUTH-031` |
| **Level / Type / Technique** | API · Functional (negative) · BVA — just inside and just outside the 15-minute access-token boundary |
| **Priority / Severity / Automation** | High · High · Yes (functional, with `Jwt:ExpiryMinutes=1` to make the boundary reachable) |
| **Role / Permission / Tenant** | TenantAdmin · `users.view` on the probe call `GET /api/users` (`UsersController.cs:22-25`) · `demo-lab` |
| **Environment** | API `:5080` started with `Jwt__ExpiryMinutes=1` (restart via `scripts/dev-rebuild.ps1`; the running API locks its DLLs) + PostgreSQL `ntqams` |
| **Preconditions** | The account holds `users.view` through its assigned role; a fresh login has just been performed |
| **Test Data** | Access token `A1`, refresh cookie `T1` from the same login |
| **Steps** | 1. At t = 0 s call `GET /api/users?page=1&pageSize=1` with `Authorization: Bearer A1`. 2. At t = 55 s repeat. 3. At t = 65 s repeat. 4. At t = 66 s call `POST /api/auth/refresh` with `Cookie: qams_rt=T1`. 5. Retry `GET /api/users` with the new token `A2`. |
| **Expected UI** | n/a — API-level case; the SPA equivalent (transparent retry) is `TC-AUTH-SEC-005`. |
| **Expected API** | Steps 1–2: `200` with the pagination envelope. Step 3: `401` `application/problem+json` with `code` `AUTH-401` and a `WWW-Authenticate` header — the framework challenge, written by `ProblemAuthorizationResultHandler.cs:18,42-44`, **not** an `AUTH-006`/`AUTH-007` from `ActiveSessionMiddleware` (which never runs, because the request is unauthenticated at `RequestIdentity.cs:88`). Step 4: `200` with a new token. Step 5: `200`. |
| **Expected DB** | Step 4 rotates: presented row gains `revoked_at_utc` + `replaced_by_id`; successor row is `Live`. Steps 1–3 write nothing. |
| **Expected Audit** | No security-event row for any of the five steps — token expiry is not a logged security event (`RequestIdentity.cs:80-131` writes none; `RefreshSessions.cs:127-141` writes none). |
| **Expected Notification** | n/a — no notification is defined for token expiry. |
| **Cleanup** | Restore the API to default configuration (`scripts/dev-rebuild.ps1` without the env override); `DELETE FROM qams.refresh_session WHERE user_id = …;` |
| **Evidence** | Timestamped HTTP captures for all five calls · the two problem+json bodies |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert the `code` extension on the 401 body, not just the status — a bare framework 401 without the problem+json `code` would be a regression of the Phase-6 `AUTHZ→problem+json` work. |

#### TC-AUTH-SEC-008 — Only the SHA-256 hex of the refresh secret is stored; the secret appears in no column  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · ADR-0009 §Decision.2 (`ADR-0009…md:22-24`), gap `GAP-AUTH-902` · `RSK-AUTH-032` |
| **Level / Type / Technique** | Integration · Security (positive) · Data Flow — define (mint) → transform (SHA-256) → store (`token_hash`) → use (compare); assert the plaintext is killed at the transform |
| **Priority / Severity / Automation** | Critical · Critical · Yes (integration, `QMS_ITEST_POSTGRES`) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — persistence-level assertion · `demo-lab` (irrelevant: `qams.refresh_session` has **no** `tenant_id` column, measured) |
| **Environment** | API `:5080` + PostgreSQL `ntqams`; read-only `psql` for the column sweep |
| **Preconditions** | One fresh login has produced token `T1 = "<sessionId:N>.<secret>"` |
| **Test Data** | `T1`; its parts `S1` (32 hex chars before the `.`) and `SEC1` (the base64url secret after it) |
| **Steps** | 1. `SELECT id, token_hash, length(token_hash) FROM qams.refresh_session WHERE id = '<S1 as UUID>';`. 2. Assert `length(token_hash) = 64` and `token_hash ~ '^[0-9A-F]{64}$'`. 3. Compute `upper(encode(sha256(convert_to('SEC1','UTF8')),'hex'))` in `psql` and assert it equals the stored `token_hash` (matching `Convert.ToHexString(SHA256.HashData(UTF8(secret)))`, `RefreshSessions.cs:66-67`). 4. Run `SELECT count(*) FROM qams.refresh_session WHERE token_hash LIKE '%' || 'SEC1' || '%';` and assert `0`. 5. Grep the API log directory `%TEMP%\ntqms-dev\` for the literal `SEC1` and assert no hit. 6. Assert the login/refresh JSON response bodies contain no `qams_rt` value (the grant travels in the header only, `RefreshSessions.cs:21-22` and `AuthController.cs:88-104`). |
| **Expected UI** | n/a — persistence and logging assertion. |
| **Expected API** | No API call in this case beyond the setup login; the response-body assertion in step 6 is made against the captured login response. |
| **Expected DB** | `qams.refresh_session` columns are exactly `id, user_id, family_id, token_hash, created_at_utc, expires_at_utc, revoked_at_utc, replaced_by_id` (measured 2026-08-01 against `ntqams`) — there is no column capable of holding the plaintext. |
| **Expected Audit** | No security-event row is produced by this case's own steps. |
| **Expected Notification** | n/a — persistence-level assertion. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE id = '<S1>';` |
| **Evidence** | `psql` transcript of steps 1–4 · log-grep output · the captured login response body |
| **Result / Defect** | Not Run · — |
| **Notes** | The session id **is** in the token in the clear by design — it is the lookup key (`RefreshSessions.cs:45,61-62`). Do not report the id's presence in `qams.refresh_session.id` as a leak. |

#### TC-AUTH-SEC-009 — `ck_refresh_session_token_hash_sha256` rejects any value that is not 64 upper-case hex  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-100…107 schema hardening (`docs/validation/06-Revalidation-Delta-v1.38-v1.50.md` Part A.10) · `RSK-AUTH-032` |
| **Level / Type / Technique** | Integration (DB) · Structural (negative) · BVA — 63 / 64 / 65 characters, and the case boundary between `a-f` and `A-F` |
| **Priority / Severity / Automation** | Medium · Medium · Yes (integration, inside a rollback transaction) |
| **Role / Permission / Tenant** | n/a — DDL-level constraint, no application role involved · n/a — no permission gates raw SQL · n/a — the table has no `tenant_id` |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app`; each INSERT inside `BEGIN … ROLLBACK` |
| **Preconditions** | Constraint present: measured `CHECK (token_hash::text ~ '^[0-9A-F]{64}$')` on `qams.refresh_session`, created by `20260731191212_Hardening3_CheckDomains.cs:169-170` |
| **Test Data** | H64U = 64 upper-case hex chars; H64L = the same string lower-cased; H63 = H64U minus its last char; H65 = H64U plus `A`; H64X = H64U with position 10 replaced by `G` |
| **Steps** | 1. `INSERT INTO qams.refresh_session (id,user_id,family_id,token_hash,created_at_utc,expires_at_utc) VALUES (gen_random_uuid(),gen_random_uuid(),gen_random_uuid(),'H64U',now(),now()+interval '14 days');` — expect success. 2–5. Repeat with H64L, H63, H65, H64X. 6. `ROLLBACK`. |
| **Expected UI** | n/a — DB-level case. |
| **Expected API** | n/a — no HTTP call is made. |
| **Expected DB** | Step 1 inserts one row. Steps 2–5 each fail with `SQLSTATE 23514` and the message `new row for relation "refresh_session" violates check constraint "ck_refresh_session_token_hash_sha256"`. Note H65 additionally violates `character varying(64)` (`SQLSTATE 22001`) — assert that either code is raised for step 4, and record which. |
| **Expected Audit** | n/a — raw DDL/DML probes write no application audit row. |
| **Expected Notification** | n/a — DB-level case. |
| **Cleanup** | `ROLLBACK` — nothing persists. |
| **Evidence** | `psql` transcript with each SQLSTATE |
| **Result / Defect** | Not Run · — |
| **Notes** | The constraint is the only thing enforcing hash shape; `RefreshSessionConfiguration.cs:16` sets only `HasMaxLength(64)`. A future writer producing lower-case hex would be rejected at the database, which is the point of the case. |

#### TC-AUTH-SEC-010 — A `token_hash` read out of the database is not replayable as a cookie  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · ADR-0009 §Decision.2 ("a database leak yields nothing replayable", `RefreshSessions.cs:31-35`), gap `GAP-AUTH-902` · `RSK-AUTH-030`, `RSK-AUTH-032` |
| **Level / Type / Technique** | API · Security (negative) · Error Guessing — model the attacker who has read access to the table but not the wire |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Anonymous attacker · n/a — `/api/auth/refresh` is `[AllowAnonymous]` · n/a — no tenant claim is involved |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | One `Live` session row exists with `id = S1` and `token_hash = H1` |
| **Test Data** | Forged cookies: (a) `qams_rt=<S1:N>.<H1>`; (b) `qams_rt=<S1:N>.<lower(H1)>`; (c) `qams_rt=<S1:N>.` + 43 chars of `A` |
| **Steps** | 1. `POST /api/auth/refresh` with forged cookie (a). 2. With (b). 3. With (c). 4. After each, `SELECT revoked_at_utc, replaced_by_id FROM qams.refresh_session WHERE id='S1';`. |
| **Expected API** | All three: `401` `application/problem+json`, `code` `AUTH-009`, title `The session has expired. Please sign in again.` — the presented value is re-hashed before comparison, so presenting the stored hash yields `SHA256(H1) ≠ H1` and fails the ordinal comparison at `RefreshSessions.cs:95`. |
| **Expected UI** | n/a — API-level attack simulation. |
| **Expected DB** | Row `S1` unchanged after all three: `revoked_at_utc IS NULL`, `replaced_by_id IS NULL` — the hash-mismatch branch returns before any write (`RefreshSessions.cs:95-99`). |
| **Expected Audit** | Exactly three `audit.security_event` rows, `event_type='REFRESH_INVALID'`, `actor IS NULL`, `detail = '<S1 as 32-hex>'`, **`tenant_id IS NULL`** (`RefreshSessions.cs:97`). |
| **Expected Notification** | n/a — no notification policy consumes `REFRESH_INVALID`. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE id='S1';` and leave the three audit rows (append-only by design). |
| **Evidence** | Three HTTP captures · the unchanged-row SQL result · the three `REFRESH_INVALID` rows |
| **Result / Defect** | Not Run · — |
| **Notes** | This case must run **after** the reuse-detection cases in the same session family — three `REFRESH_INVALID` rows will otherwise complicate the audit assertions of neighbouring cases. It also consumes 3 of the 60/min `refresh` partition. |

#### TC-AUTH-SEC-011 — The refresh cookie authenticates nothing but `/api/auth/refresh` and `/logout`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · ADR-0009 §Consequences ("No other endpoint is cookie-authenticated", `ADR-0009…md:48-51`), gap `GAP-AUTH-902` · `RSK-AUTH-030` |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — {cookie-only, bearer-only, both, neither} × {auth endpoint, protected endpoint} |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin · `users.view` for the protected probe (`UsersController.cs:22-23`) · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams`; `curl.exe` so the `Cookie` header is under test control |
| **Preconditions** | A `Live` session with cookie `T1` and a currently valid access token `A1` |
| **Test Data** | `T1`, `A1` |
| **Steps** | 1. `GET /api/users?page=1&pageSize=1` with **only** `Cookie: qams_rt=T1`. 2. Same call with **only** `Authorization: Bearer A1`. 3. `GET /api/auth/me/privileges` with only the cookie. 4. `POST /api/auth/refresh` with only `Authorization: Bearer A1` and no cookie. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Step 1: `401` `code` `AUTH-401` — cookie authentication is not configured; the only scheme is JWT bearer. Step 2: `200`. Step 3: `401` `code` `AUTH-401` (`/api/auth/me/privileges` is `[Authorize]`, `AuthController.cs:147-150`). Step 4: `401` `code` `AUTH-009` — `Request.Cookies["qams_rt"]` is null and `TryParse` returns null (`AuthController.cs:69`; `RefreshSessions.cs:89-90`), and the bearer token is irrelevant on that route. |
| **Expected DB** | No row in `qams.refresh_session` is created, revoked or rotated by any of the four steps. |
| **Expected Audit** | **No** `REFRESH_INVALID` row from step 4 — `TryParse` throws before the log line (`RefreshSessions.cs:89-90` precedes `:97`). Assert the `audit.security_event` count is unchanged across all four steps. |
| **Expected Notification** | n/a — no notification is defined for authentication refusals. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = …;` |
| **Evidence** | Four HTTP captures · the unchanged audit-row count |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 4's silence is the notable asymmetry: a malformed/absent cookie produces no ledger evidence, while a well-formed unknown one does (`TC-AUTH-SEC-010`). Encode it, do not smooth it over. |

#### TC-AUTH-SEC-012 — Reuse detection revokes every still-live member of the family  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · ADR-0009 §Decision.3 (`ADR-0009…md:27-31`), gap `GAP-AUTH-902` · `RSK-AUTH-030` |
| **Level / Type / Technique** | Integration · Security (negative) · State Transition — `Rotated --replay--> family Revoked` |
| **Priority / Severity / Automation** | Critical · Critical · Yes (integration — deepens `RefreshSessionTests.cs:80-104`, which asserts HTTP only, not the rows) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | A family `F` built by: login → `T1`; refresh(`T1`) → `T2`; refresh(`T2`) → `T3`. Rows: `T1` and `T2` `Rotated`, `T3` `Live`. |
| **Test Data** | `T1` (the oldest, twice-superseded link) |
| **Steps** | 1. `SELECT id, revoked_at_utc, replaced_by_id FROM qams.refresh_session WHERE family_id='F' ORDER BY created_at_utc;` — record the pre-state. 2. `POST /api/auth/refresh` with `Cookie: qams_rt=T1`. 3. Re-run the query. 4. `POST /api/auth/refresh` with `Cookie: qams_rt=T3`. |
| **Expected UI** | n/a — API/DB-level case. |
| **Expected API** | Step 2: `401`, `code` `AUTH-008`, title `The session has been revoked. Please sign in again.` (`RefreshSessions.cs:111`). Step 4: `401`, `code` `AUTH-008` as well — `T3` is now revoked, so it re-enters the same reuse branch. |
| **Expected DB** | After step 2 all three rows have `revoked_at_utc` non-null. `T1` and `T2` keep their original `revoked_at_utc` (rotation timestamps) because `Revoke` is `RevokedAtUtc ??= now` (`RefreshSession.cs:89`) and only touches rows filtered on `RevokedAtUtc == null` (`RefreshSessions.cs:104-107`). `T3` gains `revoked_at_utc` = the replay instant and keeps `replaced_by_id IS NULL`. **No** new row is inserted. |
| **Expected Audit** | Step 2 writes one row `event_type='REFRESH_REUSE_DETECTED'`, `actor IS NULL`, `detail='family=<F:N>'`, `tenant_id IS NULL` (`RefreshSessions.cs:108-109`). Step 4 writes a **second** identical-typed row — reuse detection is not deduplicated. |
| **Expected Notification** | n/a — no notification policy consumes `REFRESH_REUSE_DETECTED`; a theft signal reaches no one in real time. Record this as an observation. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE family_id='F';` |
| **Evidence** | Pre/post SQL results · both HTTP captures · both security-event rows |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert the *timestamps*, not just nullability — a naive implementation that overwrote `revoked_at_utc` on the already-rotated links would still pass a nullability-only check and would destroy the rotation chronology. |

#### TC-AUTH-SEC-013 — Reuse detection is scoped to one family and leaves the user's other sessions alive  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · ADR-0009 §Decision.3 · `RSK-AUTH-034` |
| **Level / Type / Technique** | Integration · Security (positive) · Decision Table — condition `same family_id?` × action `revoke` |
| **Priority / Severity / Automation** | High · High · Yes (integration) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | Two independent logins by the same account produce families `FA` (tokens `A1 → A2` after one refresh) and `FB` (token `B1`, never refreshed). `FA.A1` is `Rotated`; `FA.A2` and `FB.B1` are `Live`. |
| **Test Data** | `A1`, `A2`, `B1` |
| **Steps** | 1. `POST /api/auth/refresh` with `A1` (the replay). 2. `SELECT family_id, id, revoked_at_utc FROM qams.refresh_session WHERE user_id = … ORDER BY family_id, created_at_utc;`. 3. `POST /api/auth/refresh` with `B1`. |
| **Expected UI** | n/a — API/DB-level case. |
| **Expected API** | Step 1: `401` `AUTH-008`. Step 3: `200` with a rotated cookie — `FB` was never touched, because the revocation predicate is `s.FamilyId == session.FamilyId` (`RefreshSessions.cs:105`). |
| **Expected DB** | After step 1: both `FA` rows `revoked_at_utc` non-null; the single `FB` row `revoked_at_utc IS NULL`. After step 3: `FB` has two rows — the presented one `Rotated`, a successor `Live`. |
| **Expected Audit** | One `REFRESH_REUSE_DETECTED` row with `detail='family=<FA:N>'` and nothing referencing `FB`. |
| **Expected Notification** | n/a — no notification policy consumes session events. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = …;` |
| **Evidence** | The grouped SQL result · both HTTP captures · the single security-event row |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the containment property that makes concurrent sessions usable: theft on one device does not sign the user out everywhere. It is also, read the other way, the limitation — theft on one device does **not** sign the user out everywhere, and nothing offers to. See `TC-AUTH-SEC-023`. |

#### TC-AUTH-SEC-014 — Two concurrent sessions for one account are independent and both usable  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-001 (`01-User-Requirements-Specification.md:24`) — no URS caps concurrent sessions; ADR-0009 mints a fresh family per full login (`Login.cs:126-136`) · `RSK-AUTH-033`, `RSK-AUTH-034` |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — one analyst, two devices |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin · `users.view` for the protected probe · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams`; two independent `curl.exe` cookie jars |
| **Preconditions** | No pre-existing rows for the account: `DELETE FROM qams.refresh_session WHERE user_id = …;` |
| **Test Data** | Same credentials used twice: `admin@demo-lab.local` / `Demo-Admin-Pass-2!` |
| **Steps** | 1. Login into jar 1 → cookie `X1`, token `AX`. 2. Login into jar 2 → cookie `Y1`, token `AY`. 3. Assert `X1`'s and `Y1`'s session-id prefixes differ. 4. `GET /api/users?page=1&pageSize=1` with `AX`, then with `AY`. 5. Refresh jar 1, then refresh jar 2. 6. `SELECT family_id, count(*) FROM qams.refresh_session WHERE user_id=… GROUP BY family_id;`. |
| **Expected UI** | n/a — API-level case; the browser variant is a batch-F E2E. |
| **Expected API** | Both logins `200`; both probes `200`; both refreshes `200` with distinct rotated cookies. Neither login invalidates the other — there is no single-session enforcement anywhere in `LoginHandler`. |
| **Expected DB** | Exactly two distinct `family_id` values, two rows each after step 5 (one `Rotated`, one `Live` per family). |
| **Expected Audit** | Two `LOGIN_SUCCESS` rows, both `tenant_id` = the `demo-lab` id, both `actor='admin@demo-lab.local'`; no other rows. |
| **Expected Notification** | n/a — no notification is raised for a second concurrent sign-in (a "new device" alert does not exist). |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = …;` |
| **Evidence** | The grouped SQL result · four HTTP captures · the two `LOGIN_SUCCESS` rows |
| **Result / Defect** | Not Run · — |
| **Notes** | Unlimited concurrent sessions is the as-built behaviour, not a stated requirement — there is no configuration, no cap and no visibility. Combined with `TC-AUTH-SEC-022`/`-023`, this is the substance of `GAP-AUTH-901`. |

#### TC-AUTH-SEC-015 — Logout revokes only the presented family, leaving the user's other sessions alive  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-006 (`01-User-Requirements-Specification.md:29`) · `RSK-AUTH-031`, `RSK-AUTH-033` |
| **Level / Type / Technique** | Integration · Functional (negative — scope limitation) · State Transition — `Live → Revoked` for family A while family B is unchanged |
| **Priority / Severity / Automation** | High · High · Yes (integration) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — `/api/auth/logout` is `[AllowAnonymous]` · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | Two families `FA` (cookie `X1`) and `FB` (cookie `Y1`), both `Live`, per `TC-AUTH-SEC-014` steps 1–2 |
| **Test Data** | `X1`, `Y1` |
| **Steps** | 1. `POST /api/auth/logout` with `Cookie: qams_rt=X1`. 2. `SELECT family_id, revoked_at_utc FROM qams.refresh_session WHERE user_id=…;`. 3. `POST /api/auth/refresh` with `Y1`. 4. `POST /api/auth/refresh` with `X1`. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Step 1: `204` with the deletion `Set-Cookie`. Step 3: `200` with a rotated cookie. Step 4: `401` `code` `AUTH-008`. |
| **Expected DB** | After step 1: the single `FA` row has `revoked_at_utc` non-null and `replaced_by_id IS NULL`; the `FB` row is untouched. After step 3: `FB` holds two rows. |
| **Expected Audit** | One `LOGOUT` row with `detail='family=<FA:N>'`, `tenant_id IS NULL`, `actor IS NULL`; then one `REFRESH_REUSE_DETECTED` row from step 4 with the same family. |
| **Expected Notification** | n/a — no notification is defined for logout. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = …;` |
| **Evidence** | SQL result grouped by family · four HTTP captures · both security-event rows |
| **Result / Defect** | Not Run · — |
| **Notes** | "Sign out everywhere" does not exist: `LogoutHandler` filters on `FamilyId`, never on `UserId` (`RefreshSessions.cs:173-176`). Feeds `GAP-AUTH-901`. |

#### TC-AUTH-SEC-016 — Two tabs sharing one cookie: the second refresh is reported as theft  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · no URS — implementation-derived consequence of `RefreshSessions.cs:101-112`; charter EXPL-2 (front matter §7) · `RSK-AUTH-034` |
| **Level / Type / Technique** | Integration · Security (negative — false-positive characterisation) · Path — the two interleavings of two concurrent requests over the same row |
| **Priority / Severity / Automation** | High · High · Partially (integration; the true race needs deterministic scheduling — run it as a repeated-N probe, N = 20) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | One `Live` session, cookie `T1`; the SPA's single-flight guard (`auth.service.ts:83-97`) is bypassed by driving raw HTTP, which is exactly the two-browser-tabs-without-shared-memory case |
| **Test Data** | `T1`, presented twice |
| **Steps** | 1. Fire two `POST /api/auth/refresh` requests with `Cookie: qams_rt=T1` as closely in time as the client allows (same process, both started before either completes). 2. Record both statuses and bodies. 3. `SELECT id, revoked_at_utc, replaced_by_id FROM qams.refresh_session WHERE family_id='F' ORDER BY created_at_utc;`. 4. Repeat the whole case 20 times on fresh families and tally the outcome distribution. |
| **Expected UI** | n/a — API-level probe; the SPA is protected by the single-flight guard and would not produce this. |
| **Expected API** | Serialised interleaving: one `200` + one `401 AUTH-008`. True-concurrent interleaving (both read the row before either writes): either one `200` + one `401 AUTH-008`, or one `200` + one `409 CONCURRENCY-409` from the `xmin` token (`DomainExceptionHandler.cs:21,28-33`), or **two** `200`s leaving two `Live` rows in one family. Record which occurred in each of the 20 iterations; do not assume. |
| **Expected DB** | Assert against the observed API outcome. The finding to look for is any family that ends with **more than one** row where `revoked_at_utc IS NULL` — there is no unique index enforcing one live link per family (measured: only `pk_refresh_session`, `ix_refresh_session_family/user/expires`). |
| **Expected Audit** | Zero or one `REFRESH_REUSE_DETECTED` row per iteration, matching the observed 401s. |
| **Expected Notification** | n/a — no notification policy consumes session events. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = …;` after each iteration |
| **Evidence** | Outcome tally over 20 iterations · the per-iteration SQL snapshots · any multi-live-row family found |
| **Result / Defect** | Not Run · — |
| **Notes** | `[ID]` because no requirement states what should happen when two clients present the same valid cookie. If a family with two live rows is observed, that is a **new** defect and must be raised as its own gap, not folded into this case. |

#### TC-AUTH-SEC-017 — A deactivated account is refused on its very next authenticated request (`AUTH-006`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · **URS-006** (`01-User-Requirements-Specification.md:29`) · `RSK-AUTH-031` |
| **Level / Type / Technique** | API · Functional (negative) · State Transition — account `S1 Active-Unlocked → S3 Inactive-Unlocked` with a token already in hand |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Victim: Analyst `analyst-c@demo-lab.local`; actor: TenantAdmin · victim needs `users.view` for the probe; actor needs `users.manage` (`UsersController.cs:68-74`) · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | `analyst-c@demo-lab.local` exists, `is_active=true`, has a role granting `users.view`, and holds a fresh access token `AV` and cookie `TV` |
| **Test Data** | Victim token `AV`; admin token `AA`; victim `user_account.id` = `UV` |
| **Steps** | 1. `GET /api/users?page=1&pageSize=1` with `AV` → confirm `200` (baseline). 2. As the admin: `POST /api/users/{UV}/deactivate` with `AA`. 3. Immediately repeat step 1 with the **same, unexpired** `AV`. 4. `POST /api/auth/refresh` with `Cookie: qams_rt=TV`. 5. `SELECT is_active FROM qams.user_account WHERE id='UV';` and `SELECT id, revoked_at_utc FROM qams.refresh_session WHERE user_id='UV';`. |
| **Expected UI** | In the SPA the victim's next click returns them to the sign-in screen (the interceptor's refresh attempt also fails); no partial page renders. |
| **Expected API** | Step 2: `204`. Step 3: `401` `application/problem+json`, `code` `AUTH-006`, title `Your session is no longer valid. Please sign in again.` (`RequestIdentity.cs:98-100`). Step 4: `401`, `code` `AUTH-006` (`RefreshSessions.cs:120-124`). |
| **Expected DB** | `is_active=false`. After step 3 the refresh rows are **still** `revoked_at_utc IS NULL` — deactivation writes nothing to `qams.refresh_session` (`UserManagement.cs:131-139`). After step 4 the **presented** row alone has `revoked_at_utc` set (`RefreshSessions.cs:122`); any sibling family row remains null. |
| **Expected Audit** | No `audit.security_event` row for either refusal — `ActiveSessionMiddleware` and the refresh `AUTH-006` branch write none. `audit.field_change` records the `is_active` `true→false` transition on `UserAccount`/`UV` via `FieldChangeInterceptor`, with `actor` = the admin. |
| **Expected Notification** | n/a — no notification is defined for account deactivation. |
| **Cleanup** | `POST /api/users/{UV}/reactivate` as the admin; `DELETE FROM qams.refresh_session WHERE user_id='UV';` |
| **Evidence** | Four HTTP captures · both SQL results · the `field_change` row |
| **Result / Defect** | Not Run · — |
| **Notes** | Reactivation does **not** clear a lockout (front matter §3.1, `S4 → S2`) — irrelevant here because the account was never locked, but do not reuse this cleanup for a locked account. |

#### TC-AUTH-SEC-018 — A role change invalidates the outstanding token on the next request (`AUTH-007`)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · **URS-006** (`01-User-Requirements-Specification.md:29`) · `RSK-AUTH-031` |
| **Level / Type / Technique** | API · Functional (negative) · State Transition — the token's `role` claim and the DB `role` column diverge |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | Victim: Analyst `analyst-c@demo-lab.local`; actor: TenantAdmin · actor needs `users.manage` (`UsersController.cs:33-39`) · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | Victim is `role='Analyst'`, active, unlocked, holds a fresh token `AV` whose `ClaimTypes.Role` value is the literal `Analyst` |
| **Test Data** | `POST /api/users/{UV}/role` body `{"role":"DepartmentHead"}` (`ChangeUserRoleRequest`, `Contracts/IdentityAccess/UserContracts.cs:20`; parsed by `TenantRole.Parse`, `UserManagement.cs:18-24`) |
| **Steps** | 1. Baseline `GET /api/users?page=1&pageSize=1` with `AV` → `200`. 2. Decode `AV` and record the role claim = `Analyst`. 3. As the admin, `POST /api/users/{UV}/role` with the body above. 4. `SELECT role, role_id FROM qams.user_account WHERE id='UV';`. 5. Repeat step 1 with the same `AV`. 6. `POST /api/auth/refresh` with the victim's cookie `TV`, then repeat step 1 with the new token. |
| **Expected UI** | The victim sees the "your permissions have changed, please sign in again" message and is routed to sign-in. |
| **Expected API** | Step 3: `204`. Step 5: `401` `application/problem+json`, `code` `AUTH-007`, title `Your permissions have changed. Please sign in again.` — the ordinal comparison `tokenRole != row.Role.ToString()` at `RequestIdentity.cs:104-107`. Step 6: the refresh returns `200` and the **new** token carries `role=DepartmentHead` (claims re-read from the current row, `RefreshSessions.cs:136`), so the follow-up probe's outcome depends on whether the new seeded role grants `users.view` — assert the actual status and record it rather than predicting it. |
| **Expected DB** | `qams.user_account.role = 'DepartmentHead'` (permitted by `CHECK ck_user_account_role_domain`) and `role_id` re-pointed to the seeded role for that tier (`SeededRoleDefault.AssignAsync`, `UserManagement.cs:124-126`). |
| **Expected Audit** | `audit.field_change` rows for `role` (`Analyst → DepartmentHead`) and `role_id`, actor = the admin. **No** `audit.security_event` row — `AUTH-007` is not logged as a security event, although URS-016 lists "privilege changes" among the events to record. Note the discrepancy in the batch note. |
| **Expected Notification** | n/a — no notification policy subscribes to `UserRoleAssigned` (grep of `src/NT.QAMS.Application` returns no handler). |
| **Cleanup** | `POST /api/users/{UV}/role` back to `{"role":"Analyst"}`; `DELETE FROM qams.refresh_session WHERE user_id='UV';` |
| **Evidence** | Decoded token before and after · six HTTP captures · the `field_change` rows |
| **Result / Defect** | Not Run · — |
| **Notes** | The comparison is against the **structural tier** only. A change to the user's *assigned role* (`PUT /api/users/{id}/assigned-role`) that leaves the tier unchanged does **not** produce `AUTH-007`; privileges simply re-resolve on the next request (`RequestIdentity.cs:118-121`). Do not conflate the two — the assigned-role case belongs to module `RBAC`. |

#### TC-AUTH-SEC-019 — When an account is both inactive and role-changed, `AUTH-006` wins  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-006 · `RSK-AUTH-031` |
| **Level / Type / Technique** | API · Functional (negative) · Multiple-Condition — the two guards of `ActiveSessionMiddleware` evaluated with both conditions true, proving the short-circuit order |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Victim: Analyst `analyst-c@demo-lab.local`; actor: TenantAdmin · actor needs `users.manage` · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | Victim active as `Analyst` with fresh token `AV` |
| **Test Data** | Two admin calls in order: `POST /api/users/{UV}/role` `{"role":"DepartmentHead"}` then `POST /api/users/{UV}/deactivate` |
| **Steps** | 1. Capture `AV` (role claim `Analyst`). 2. Change the role to `DepartmentHead`. 3. Deactivate the account. 4. `GET /api/users?page=1&pageSize=1` with `AV`. 5. Reactivate; repeat step 4. |
| **Expected UI** | n/a — API-level ordering case. |
| **Expected API** | Step 4: `401` `code` **`AUTH-006`** — the `row is null \|\| !row.IsActive` branch returns at `RequestIdentity.cs:98-101` before the role comparison at `:104`. Step 5: `401` `code` **`AUTH-007`** — with activity restored, the second condition is reached. |
| **Expected DB** | `is_active` transitions `true → false → true`; `role='DepartmentHead'` throughout steps 3–5. |
| **Expected Audit** | `audit.field_change` rows for `role`, `role_id`, and `is_active` twice. No `audit.security_event` rows. |
| **Expected Notification** | n/a — no notification is defined for either transition. |
| **Cleanup** | Restore `role='Analyst'` via the API; `DELETE FROM qams.refresh_session WHERE user_id='UV';` |
| **Evidence** | Two problem+json bodies showing the different codes from the same token · the `field_change` rows |
| **Result / Defect** | Not Run · — |
| **Notes** | The pair of observations is the whole point: one token, two different codes, distinguished only by the DB state. Assert both in one case so the ordering is pinned. |

#### TC-AUTH-SEC-020 — A rate-limited request never reaches the account re-check  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-006 (`01-User-Requirements-Specification.md:29`) — the re-check is bypassed when the limiter rejects; pipeline order at `Program.cs:263-269` · `RSK-AUTH-031` |
| **Level / Type / Technique** | API · Functional (negative) · Path — the short pipeline path `UseRateLimiter → 429` versus the long path through `ActiveSessionMiddleware` |
| **Priority / Severity / Automation** | Medium · Low · Yes (functional — must run last in any suite; it exhausts the 60/min `refresh` partition) |
| **Role / Permission / Tenant** | Anonymous · n/a — `/api/auth/refresh` is `[AllowAnonymous]` · n/a — no tenant claim on this route |
| **Environment** | API `:5080` + PostgreSQL `ntqams`; a single client IP so the fixed-window partition key is stable (`RateLimiting.ClientKey`, `RateLimiting.cs:97-98`) |
| **Preconditions** | The `refresh` partition is fresh (wait out a full 60-second window before starting); `RateLimit:RefreshPermitPerMinute` unset so the default 60 applies (`RateLimiting.cs:25`) |
| **Test Data** | A deliberately invalid cookie `qams_rt=00000000000000000000000000000000.AAAA`, repeated |
| **Steps** | 1. Record `SELECT count(*) FROM audit.security_event WHERE event_type='REFRESH_INVALID';`. 2. Send the request 60 times inside one minute; assert each returns `401`. 3. Send the 61st inside the same window. 4. Re-run the count query. 5. Wait 60 s and send once more. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Requests 1–60: `401` `code` `AUTH-009`. Request 61: `429 Too Many Requests` with `Retry-After: 60` (`RateLimiting.cs:56-61`) and **no** problem+json `code` extension — the rejection is written by the limiter, not by `DomainExceptionHandler`. Request 62 (new window): `401` `AUTH-009`. |
| **Expected DB** | No `qams.refresh_session` row is created or modified at any point. |
| **Expected Audit** | The `REFRESH_INVALID` count increases by exactly **61**, not 62 — the 429'd request produced no ledger row because it never reached the handler. (60 from step 2 + 1 from step 5.) |
| **Expected Notification** | n/a — no notification is defined for throttling. |
| **Cleanup** | Wait one full window before running any other AUTH case; the audit rows are append-only and stay. |
| **Evidence** | The 61st response headers showing `Retry-After: 60` · the before/after audit counts |
| **Result / Defect** | Not Run · — |
| **Notes** | The `refresh` partition is per **IP**, while the `esignature` partition is per **actor** (`RateLimiting.cs:96-102`). A shared-NAT laboratory consumes one 60/min budget for the whole site — a capacity observation, not a defect, and out of scope for this batch's assertions. |

#### TC-AUTH-SEC-021 — Deactivation leaves every refresh row `Live` in the database  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · `GAP-AUTH-014` (front matter §8) — no URS requires eager revocation; the as-built behaviour is at `UserManagement.cs:131-139` · `RSK-AUTH-031`, `RSK-AUTH-033` |
| **Level / Type / Technique** | Integration · Functional (characterisation) · State Transition — the *absent* edge `account Deactivate → sessions Revoked` |
| **Priority / Severity / Automation** | High · Medium · Yes (integration) |
| **Role / Permission / Tenant** | Actor: TenantAdmin with `users.manage`; subject: Analyst `analyst-c@demo-lab.local` · `users.manage` · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | The subject has **three** families open (three separate logins), all `Live` |
| **Test Data** | Subject id `UV` |
| **Steps** | 1. `SELECT count(*) FROM qams.refresh_session WHERE user_id='UV' AND revoked_at_utc IS NULL;` → expect `3`. 2. `POST /api/users/{UV}/deactivate`. 3. Re-run the query **in the same second**. 4. Present family 1's cookie at `/api/auth/refresh`. 5. Re-run the query. |
| **Expected UI** | n/a — DB-state characterisation. |
| **Expected API** | Step 2: `204`. Step 4: `401` `code` `AUTH-006`. |
| **Expected DB** | Step 3: still `3` — **assert the count is unchanged**, which is the finding. Step 5: `2` — only the presented family's row was revoked, lazily (`RefreshSessions.cs:122`). Families 2 and 3 remain `revoked_at_utc IS NULL` until each is individually presented or until the retention purge removes them 7 days after their own expiry (`OutboxProcessor.cs:264-269`, constant `RefreshSessionRetentionDays = 7` at `:272`). |
| **Expected Audit** | `audit.field_change` row for `is_active`. **No** security event marking the session termination — contrast the explicit `LOGOUT` row that a user-initiated logout writes. |
| **Expected Notification** | n/a — no notification is defined for deactivation. |
| **Cleanup** | Reactivate the subject; `DELETE FROM qams.refresh_session WHERE user_id='UV';` |
| **Evidence** | The three count snapshots · the `field_change` row · the absence of a `LOGOUT`/revocation event |
| **Result / Defect** | Not Run · — |
| **Notes** | Do **not** write this as a failing case. Access is correctly denied at every entry point; the deficiency is evidentiary, exactly as `GAP-AUTH-014` states. If `GAP-AUTH-014` is closed, this case inverts and its expected DB result becomes `0` at step 3. |

#### TC-AUTH-SEC-022 — No endpoint lists a user's active sessions  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · **`GAP-AUTH-901`** (new, this batch) — no URS covers session inventory; nothing in ADR-0009 promises one · `RSK-AUTH-033` |
| **Level / Type / Technique** | API · Functional (absence) · Error Guessing — probe the route shapes a reviewer would expect to exist |
| **Priority / Severity / Automation** | High · High · Yes, once the gap is closed (until then it is an absence assertion against the merge-gated API surface) |
| **Role / Permission / Tenant** | TenantAdmin · would require a new permission key — none exists; `PermissionCatalog` has no `sessions` module (`PermissionCatalog.cs:132-186`, 31 modules, none session-related) · `demo-lab` |
| **Environment** | API `:5080` + the approved surface file `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` |
| **Preconditions** | Two `Live` families exist for the caller |
| **Test Data** | Probe paths: `GET /api/auth/sessions`, `GET /api/auth/me/sessions`, `GET /api/users/{id}/sessions`, `GET /api/sessions` (and the `/api/v1/…` mirrors) |
| **Steps** | 1. `grep -i session tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` — assert **zero** matches (measured 2026-08-01: no output). 2. Call each probe path with a valid bearer token. 3. Record every status. |
| **Expected UI** | There is no session-management screen; `frontend/src/app/app.routes.ts` carries no session route. Assert its absence rather than describing one. |
| **Expected API** | Every probe returns `404 Not Found` from routing (no controller matches). Assert `404`, and specifically assert it is a routing 404 and **not** a `USER-404`/`UAR-404` problem+json body carrying a domain `code`. |
| **Expected DB** | Unchanged — `qams.refresh_session` is read by no query the API exposes. |
| **Expected Audit** | No `audit.security_event` row. |
| **Expected Notification** | n/a — the capability does not exist. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = …;` |
| **Evidence** | The empty grep result over `ApiSurface.approved.txt` · the four (eight with mirrors) 404 captures |
| **Result / Defect** | Not Run · — |
| **Notes** | **Acceptance criteria to implement against:** `GET /api/auth/me/sessions` returns, for the calling user, one entry per row in `qams.refresh_session` where `revoked_at_utc IS NULL AND expires_at_utc > now()`, projecting `{sessionId, familyId, createdAtUtc, expiresAtUtc, isCurrent}` and **never** `token_hash`; the current session is identified by matching the presented cookie's session id; the route is `[Authorize]` with no `[RequirePermission]` (it is self-scoped); the response is the standard paged envelope; and the API-surface snapshot is updated in the same commit or the merge gate fails. |

#### TC-AUTH-SEC-023 — No administrative endpoint revokes another user's sessions  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · **`GAP-AUTH-901`**; relates to `GAP-AUTH-014` (front matter §8) · `RSK-AUTH-031`, `RSK-AUTH-033` |
| **Level / Type / Technique** | API · Security (absence) · Error Guessing — probe the containment action an incident responder would reach for |
| **Priority / Severity / Automation** | Critical · High · Yes, once the gap is closed |
| **Role / Permission / Tenant** | TenantAdmin · would require `users.manage` (the only existing write key on the user surface, `PermissionCatalog.cs:99,168`) · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | Subject `analyst-c@demo-lab.local` has two `Live` families; the caller holds `users.manage` |
| **Test Data** | Probe paths: `POST /api/users/{UV}/revoke-sessions`, `DELETE /api/users/{UV}/sessions`, `POST /api/auth/sessions/{sessionId}/revoke` |
| **Steps** | 1. Call each probe path with the admin bearer token; for the `DELETE` include `X-Change-Reason: incident response` so the refusal cannot be attributed to `CHANGE-REASON-REQUIRED` (`RequestIdentity.cs:149-155`). 2. Record every status. 3. `SELECT count(*) FROM qams.refresh_session WHERE user_id='UV' AND revoked_at_utc IS NULL;` and assert it is still `2`. 4. Establish what the administrator *can* do instead: `POST /api/users/{UV}/deactivate`, then re-run the count. |
| **Expected UI** | The user-administration screen offers deactivate/reactivate/reset-password only; there is no "terminate sessions" control. |
| **Expected API** | Steps 1–2: `404` from routing on every probe. Step 4: `204`. |
| **Expected DB** | Step 3: `2` live rows. Step 4: still `2` live rows — deactivation is not revocation (`TC-AUTH-SEC-021`). |
| **Expected Audit** | Only the `audit.field_change` row from step 4's `is_active` change. No security event records the containment attempt. |
| **Expected Notification** | n/a — the capability does not exist. |
| **Cleanup** | Reactivate the subject; `DELETE FROM qams.refresh_session WHERE user_id='UV';` |
| **Evidence** | The routing 404 captures · the unchanged live-session count across the whole sequence |
| **Result / Defect** | Not Run · — |
| **Notes** | **Acceptance criteria to implement against:** `POST /api/users/{id}/revoke-sessions`, gated `[RequirePermission(PermissionCatalog.Users, PermissionAction.Manage)]` with a matching `[RequirePermissionPolicy]` on the command, loads the subject through `TenantUserLoader` (so a cross-tenant id yields `USER-404`, not a revocation), calls `Revoke(now)` on every `qams.refresh_session` row for that `user_id` with `revoked_at_utc IS NULL`, writes one `audit.security_event` of a new type (e.g. `SESSIONS_REVOKED`) with `tenant_id` stamped and `detail = "user=<id:N>;count=<n>"`, and returns `204`. An integration case must then prove the count of live rows for that user is `0` inside the same transaction. |

#### TC-AUTH-SEC-024 — A user cannot revoke one of their own sessions without ending all of them  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · **`GAP-AUTH-901`** · `RSK-AUTH-033` |
| **Level / Type / Technique** | API · Functional (absence) · Use Case — "I left myself signed in on the shared workstation" |
| **Priority / Severity / Automation** | High · Medium · Yes, once the gap is closed |
| **Role / Permission / Tenant** | Analyst (self-service) · none — a self-scoped action needs no permission key, matching `/api/auth/me/*` (`AuthController.cs:147-159`) · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams`; two cookie jars representing two devices |
| **Preconditions** | The account holds families `FA` (device 1, the caller) and `FB` (device 2, to be terminated), both `Live` |
| **Test Data** | Probe paths: `DELETE /api/auth/me/sessions/{sessionId}`, `POST /api/auth/me/sessions/{sessionId}/revoke`, `POST /api/auth/logout-all` |
| **Steps** | 1. Call each probe from device 1 with its bearer token (and `X-Change-Reason: user request` on the DELETE). 2. Record every status. 3. Demonstrate the only available workaround: from device 1, `POST /api/auth/logout` with `FA`'s cookie, and assert it does **not** touch `FB`. 4. `SELECT family_id, revoked_at_utc FROM qams.refresh_session WHERE user_id = …;`. |
| **Expected UI** | No account-security screen exists; the SPA's only session control is the sign-out action (`auth.service.ts:137-140`), which posts to `/api/auth/logout`. |
| **Expected API** | Step 1: `404` from routing on all three probes. Step 3: `204`. |
| **Expected DB** | After step 3: `FA`'s row `revoked_at_utc` non-null; `FB`'s row still `revoked_at_utc IS NULL` — the user has terminated the device they are *holding*, not the one they left behind. |
| **Expected Audit** | One `LOGOUT` row for `FA` only, `tenant_id IS NULL`. |
| **Expected Notification** | n/a — the capability does not exist. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id = …;` |
| **Evidence** | Probe 404 captures · the family-grouped SQL result showing `FB` alive after logout |
| **Result / Defect** | Not Run · — |
| **Notes** | **Acceptance criteria to implement against:** (a) `DELETE /api/auth/me/sessions/{sessionId}` revokes the whole family containing `{sessionId}` when — and only when — that session's `user_id` equals the caller's `sub`, returning `404` (not `403`) for any session id belonging to anyone else so the endpoint is not an existence oracle; (b) `POST /api/auth/logout-all` revokes every `revoked_at_utc IS NULL` row for the caller and clears the cookie; (c) both write a security event distinguishable from `LOGOUT`; (d) the `X-Change-Reason` header is **not** required — ending one's own session is not a GxP record change (contrast `RequestIdentity.cs:149-155`, which would otherwise reject the DELETE with `400 CHANGE-REASON-REQUIRED`). |

#### TC-AUTH-SEC-025 — A cross-tenant session-revocation attempt has no endpoint to refuse it  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · **`GAP-AUTH-901`**; interacts with `GAP-AUTH-006` (`qams.refresh_session` is outside RLS and has no `tenant_id`) · `RSK-AUTH-033` |
| **Level / Type / Technique** | API · Security (absence + isolation) · Decision Table — {same tenant, other tenant} × {revoke endpoint exists?}; only the second column is currently determinable |
| **Priority / Severity / Automation** | Critical · Critical · Yes, once the gap is closed |
| **Role / Permission / Tenant** | TenantAdmin of tenant **A** attacking a subject in tenant **B** · `users.manage` held in tenant A only · two tenants: `demo-lab` (A) and a second active tenant (B) provisioned for this case |
| **Environment** | API `:5080` + PostgreSQL `ntqams`; a second tenant provisioned via the platform surface |
| **Preconditions** | Tenant B exists and is `Active` with user `analyst-b@lab-b.local`, who holds one `Live` refresh family `FB`; tenant A's admin is signed in and holds `users.manage` |
| **Test Data** | Subject id `UB` (tenant B's user), session id `SB` (from `FB`) |
| **Steps** | 1. As tenant A's admin, probe `POST /api/users/{UB}/revoke-sessions` and `POST /api/auth/sessions/{SB}/revoke`. 2. Record the statuses. 3. Probe the *existing* cross-tenant write to establish the isolation baseline: `POST /api/users/{UB}/deactivate` as tenant A's admin. 4. `SELECT is_active FROM qams.user_account WHERE id='UB';` and `SELECT revoked_at_utc FROM qams.refresh_session WHERE family_id='FB';` (with `SELECT set_config('app.bypass_rls','on',false);` since `user_account` is outside RLS but the audit reads are not). 5. Confirm tenant B's user can still refresh with `FB`'s cookie. |
| **Expected UI** | n/a — API-level isolation probe. |
| **Expected API** | Step 1: `404` from routing (the endpoints do not exist). Step 3: `404` `application/problem+json` with `code` **`USER-404`** — `TenantUserLoader.LoadAsync` filters on `u.TenantId == tenant.TenantId` and throws `USER-404` (`UserManagement.cs:105-113`), so a cross-tenant id is indistinguishable from a non-existent one. Step 5: `200` with a rotated cookie. |
| **Expected DB** | `is_active` for `UB` unchanged at `true`; `FB`'s rows unchanged until step 5 rotates them normally. |
| **Expected Audit** | No `audit.security_event` row for the refused attempts. The `USER-404` in step 3 leaves no ledger trace of a cross-tenant probe — record this as an observation against URS-016. |
| **Expected Notification** | n/a — the capability does not exist. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE user_id='UB';` and remove the tenant-B fixture per the tenant module's teardown. |
| **Evidence** | Probe captures · the `USER-404` problem+json body · both SQL results |
| **Result / Defect** | Not Run · — |
| **Notes** | **Acceptance criteria to implement against:** any future session-revocation endpoint must resolve its subject through `TenantUserLoader.LoadAsync` (not by `session_id` alone) so the tenant predicate is applied to the **user**, since `qams.refresh_session` carries no `tenant_id` and therefore no RLS policy can fence it (measured 2026-08-01: `relrowsecurity=f`, `relforcerowsecurity=f`, no policies). A revoke-by-session-id endpoint that looks the row up by its primary key alone would be a cross-tenant hole by construction; the acceptance case must prove that a tenant-A admin presenting a tenant-B session id receives `404 USER-404` and that `FB` remains live. |

#### TC-AUTH-STATE-001 — `Live → Rotated`: a refresh retires the presented link and issues a successor  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · ADR-0009 §Decision.3, gap `GAP-AUTH-902` · `RSK-AUTH-030` |
| **Level / Type / Technique** | Integration · Functional (positive) · State Transition — the single legal transition out of `Live` on a refresh |
| **Priority / Severity / Automation** | Critical · Critical · Yes (integration) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | One `Live` row: `revoked_at_utc IS NULL`, `replaced_by_id IS NULL`, `expires_at_utc > now()` |
| **Test Data** | Cookie `T1`; the presented row's id `S1`, family `F` |
| **Steps** | 1. `POST /api/auth/refresh` with `T1`. 2. Capture the new cookie `T2` and its session-id prefix `S2`. 3. `SELECT id, family_id, created_at_utc, expires_at_utc, revoked_at_utc, replaced_by_id FROM qams.refresh_session WHERE family_id='F' ORDER BY created_at_utc;`. |
| **Expected UI** | n/a — the SPA equivalent is `TC-AUTH-SEC-005` step 6. |
| **Expected API** | `200`; body `accessToken` non-empty, `mfaRequired:false`, `role` and `displayName` re-read from the current `user_account` row (`RefreshSessions.cs:136-139`); one `Set-Cookie` for `qams_rt`. |
| **Expected DB** | Two rows. `S1`: `revoked_at_utc` = the refresh instant, `replaced_by_id = S2`. `S2`: `family_id = F` (unchanged), `created_at_utc` = the refresh instant, `expires_at_utc = created_at_utc + 14 days` (a **full new** lifetime, not the remainder of the old one — `RefreshSession.Start` computes `now + lifetime`, `RefreshSession.cs:72`), `revoked_at_utc IS NULL`, `replaced_by_id IS NULL`. |
| **Expected Audit** | No `audit.security_event` row. |
| **Expected Notification** | n/a — no notification is defined for rotation. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE family_id='F';` |
| **Evidence** | HTTP capture · the two-row SQL result with both timestamps |
| **Result / Defect** | Not Run · — |
| **Notes** | The sliding 14-day window means an actively used session never expires by age. That is the design, but it makes `GAP-AUTH-012` (no server-side idle enforcement) materially worse — note it, do not assert a failure here. |

#### TC-AUTH-STATE-002 — `Rotated → Revoked` (family) on replay of a superseded link  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · ADR-0009 §Decision.3, gap `GAP-AUTH-902` · `RSK-AUTH-030` |
| **Level / Type / Technique** | API · Security (negative) · State Transition — the theft edge |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional — `RefreshSessionTests.cs:80-104` covers the HTTP half) |
| **Role / Permission / Tenant** | Attacker holding a stolen cookie · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | Family `F` with `T1` `Rotated` and `T2` `Live` |
| **Test Data** | `T1` |
| **Steps** | 1. `POST /api/auth/refresh` with `T1`. 2. Immediately `POST /api/auth/refresh` with `T2` (the legitimate holder's next attempt). |
| **Expected UI** | The legitimate user's SPA silently attempts one refresh, receives 401, clears the in-memory session (`auth.service.ts:90-93`) and routes to sign-in. |
| **Expected API** | Step 1: `401` `code` `AUTH-008`. Step 2: `401` `code` `AUTH-008` — the victim is signed out as collateral, which is the intended containment. |
| **Expected DB** | Both rows `revoked_at_utc` non-null; no successor row inserted by either call. |
| **Expected Audit** | Two `REFRESH_REUSE_DETECTED` rows (one per call), each `detail='family=<F:N>'`, `tenant_id IS NULL`, `actor IS NULL`. |
| **Expected Notification** | n/a — no notification policy consumes `REFRESH_REUSE_DETECTED`; nobody is told their session was stolen. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE family_id='F';` |
| **Evidence** | Both HTTP captures · the two security-event rows · the fully-revoked family |
| **Result / Defect** | Not Run · — |
| **Notes** | The victim's only signal is being signed out. Whether that is acceptable evidence for Part 11 §11.300(d) ("detect and report attempts at unauthorised use") is a question for the batch note, not an assertion here. |

#### TC-AUTH-STATE-003 — `Revoked (by logout) → refresh` yields `AUTH-008`, not an expiry code  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · no URS distinguishes the two revocation causes; behaviour at `RefreshSessions.cs:101-112` · `RSK-AUTH-035` |
| **Level / Type / Technique** | API · Functional (characterisation) · State Transition — the `Revoked` state has only one outbound refresh edge, shared with `Rotated` |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | Family `F` with one row, logged out — `revoked_at_utc` non-null, `replaced_by_id IS NULL` |
| **Test Data** | `T1` (the logged-out cookie value) |
| **Steps** | 1. `POST /api/auth/refresh` with `T1`. 2. `SELECT revoked_at_utc FROM qams.refresh_session WHERE family_id='F';`. 3. `SELECT event_type, detail FROM audit.security_event WHERE detail='family=<F:N>' ORDER BY occurred_at_utc;`. |
| **Expected UI** | n/a — a signed-out browser has already cleared its state. |
| **Expected API** | `401` `code` `AUTH-008`, title `The session has been revoked. Please sign in again.` — the code says "revoked", which is accurate, but the **event** written says theft. |
| **Expected DB** | `revoked_at_utc` unchanged at the logout instant — `Revoke` is `??=` (`RefreshSession.cs:89`), so the second revocation is a no-op. |
| **Expected Audit** | Two rows in order: `LOGOUT`, then `REFRESH_REUSE_DETECTED` — both with `detail='family=<F:N>'`. A stale cookie retried after a normal logout is indistinguishable, in the ledger, from a stolen token. |
| **Expected Notification** | n/a — no notification is defined. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE family_id='F';` |
| **Evidence** | HTTP capture · the ordered two-row audit query result |
| **Result / Defect** | Not Run · — |
| **Notes** | `[ID]` — no requirement asks for the two causes to be distinguishable. It is raised as an observation in the batch note rather than a gap, because the security posture (deny) is correct and only the forensic label is imprecise. |

#### TC-AUTH-STATE-004 — `Expired → refresh` yields `AUTH-009` and does not revoke the row  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · ADR-0009 §Decision.2 (14-day horizon), gap `GAP-AUTH-902` · `RSK-AUTH-035` |
| **Level / Type / Technique** | Integration · Functional (negative) · BVA — `expires_at_utc` at now−1 s (expired) versus now+1 s (live), against the strict `>` in `IsLive` (`RefreshSession.cs:42`) |
| **Priority / Severity / Automation** | High · Medium · Yes (integration; the boundary is reached by updating `expires_at_utc` directly, since 14 days cannot be waited out) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams`; direct `UPDATE` on `qams.refresh_session` (permitted — the table is not append-only and carries no immutability trigger) |
| **Preconditions** | One `Live` row `S1` with cookie `T1` |
| **Test Data** | Two probes: `expires_at_utc = now() + interval '1 second'` then `expires_at_utc = now() - interval '1 second'` |
| **Steps** | 1. `UPDATE qams.refresh_session SET expires_at_utc = now() + interval '30 seconds' WHERE id='S1';` and refresh with `T1` → expect success; capture the successor. 2. Rebuild a `Live` row. 3. `UPDATE qams.refresh_session SET expires_at_utc = now() - interval '1 second' WHERE id='S1';`. 4. `POST /api/auth/refresh` with `T1`. 5. `SELECT revoked_at_utc, replaced_by_id FROM qams.refresh_session WHERE id='S1';`. |
| **Expected UI** | The SPA's bootstrap `hydrate()` receives the 401, clears the session and shows the sign-in form. |
| **Expected API** | Step 1: `200`. Step 4: `401` `code` `AUTH-009`, title `The session has expired. Please sign in again.` (`RefreshSessions.cs:114-117`). |
| **Expected DB** | After step 4: `revoked_at_utc IS NULL` and `replaced_by_id IS NULL` — the expiry branch throws before any write. The row stays in the table, expired but unrevoked, until the purge. |
| **Expected Audit** | **No** `audit.security_event` row for step 4 — the expiry branch writes none. Assert the count is unchanged. |
| **Expected Notification** | n/a — no notification is defined for session expiry. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE id='S1';` |
| **Evidence** | Both HTTP captures · the post-step-4 SQL result showing the untouched row · the unchanged audit count |
| **Result / Defect** | Not Run · — |
| **Notes** | An expired-but-unrevoked row is still reuse-detectable? **No** — the liveness check at `:114` is reached only when `revoked_at_utc IS NULL`, so an expired unrevoked token returns `AUTH-009` and never trips reuse detection. The reuse and expiry states are disjoint by construction. |

#### TC-AUTH-STATE-005 — An unknown session id writes `REFRESH_INVALID` and returns `AUTH-009`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-016 (`01-User-Requirements-Specification.md:44`) · `RSK-AUTH-030` |
| **Level / Type / Technique** | API · Security (negative) · State Transition — the `Purged`/never-existed state |
| **Priority / Severity / Automation** | High · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Anonymous attacker · n/a — anonymous endpoint · n/a — the event is written with `tenant_id = null` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | The chosen session id does not exist: `SELECT count(*) FROM qams.refresh_session WHERE id='<probe>';` returns `0` |
| **Test Data** | Cookie `qams_rt=0192f3a47b8c7d1e9f0a1b2c3d4e5f60.` + 43 arbitrary base64url chars (a syntactically valid, semantically unknown token) |
| **Steps** | 1. Record the current `REFRESH_INVALID` row count. 2. `POST /api/auth/refresh` with the probe cookie. 3. `SELECT event_type, actor, detail, tenant_id FROM audit.security_event ORDER BY occurred_at_utc DESC LIMIT 1;` (after `SELECT set_config('app.bypass_rls','on',false);`). |
| **Expected UI** | n/a — API-level probe. |
| **Expected API** | `401` `application/problem+json`, `code` `AUTH-009`, title `The session has expired. Please sign in again.` — identical to the hash-mismatch and expired cases, so an attacker cannot distinguish "no such session" from "wrong secret" (`RefreshSessions.cs:95-99` and `:114-117` share the code and the title). |
| **Expected DB** | No row inserted into `qams.refresh_session`. |
| **Expected Audit** | Exactly one new row: `event_type='REFRESH_INVALID'`, `actor IS NULL`, `detail='0192f3a47b8c7d1e9f0a1b2c3d4e5f60'` (the session id in `N` format), `tenant_id IS NULL`, `ip_address IS NULL` (`GAP-AUTH-005` — `SecurityEventLog.WriteAsync` never sets it, `ComplianceLedgerServices.cs:68-83`). |
| **Expected Notification** | n/a — no notification policy consumes `REFRESH_INVALID`. |
| **Cleanup** | None — the audit row is append-only and is intended to persist. |
| **Evidence** | HTTP capture · the new security-event row with all four asserted columns |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert `ip_address IS NULL` explicitly. It is the as-built state under `GAP-AUTH-005`; when that gap closes, this row's expectation must be inverted, and pinning it now makes the change visible. |

#### TC-AUTH-STATE-006 — Malformed tokens are rejected before the database and leave no ledger row  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-016 (`01-User-Requirements-Specification.md:44`) — the *absence* of an event on this path is the finding · `RSK-AUTH-030` |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — the five rejection partitions of `RefreshTokenFormat.TryParse` (`RefreshSessions.cs:48-64`) |
| **Priority / Severity / Automation** | High · Medium · Yes (functional; also a natural unit test over `TryParse`) |
| **Role / Permission / Tenant** | Anonymous attacker · n/a — anonymous endpoint · n/a |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | The `refresh` rate-limit partition has ≥ 6 permits left in the current window |
| **Test Data** | (a) `qams_rt=` (empty → `IsNullOrWhiteSpace`, `:50`); (b) `qams_rt=nodotatall` (no separator, `IndexOf('.') == -1 ≤ 0`, `:56`); (c) `qams_rt=.secretonly` (leading dot, `separator == 0 ≤ 0`); (d) `qams_rt=0192f3a47b8c7d1e9f0a1b2c3d4e5f60.` (trailing dot, `separator == length-1`); (e) `qams_rt=not-a-guid.AAAA` (`Guid.TryParseExact` with `"N"` fails, `:61`); (f) `qams_rt=0192f3a4-7b8c-7d1e-9f0a-1b2c3d4e5f60.AAAA` (a **dashed** GUID — `"N"` format rejects it) |
| **Steps** | 1. Record the `audit.security_event` total count. 2. Send `POST /api/auth/refresh` once per partition (a)–(f). 3. Re-run the count. 4. Repeat (b) against `POST /api/auth/logout`. |
| **Expected UI** | n/a — API-level probe. |
| **Expected API** | (a)–(f) each: `401` `application/problem+json`, `code` `AUTH-009`. Step 4 (`/logout`): `204 No Content` — `LogoutHandler` returns silently on an unparsable token (`RefreshSessions.cs:159-163`), with a `Set-Cookie` deletion still emitted by the controller (`AuthController.cs:80`). |
| **Expected DB** | No row created, read-matched or modified in `qams.refresh_session`. |
| **Expected Audit** | The total count is **unchanged** across all six probes — `TryParse` throws at `RefreshSessions.cs:89-90`, before the `REFRESH_INVALID` write at `:97`. Contrast `TC-AUTH-STATE-005`, where a *well-formed* unknown token does produce a row. |
| **Expected Notification** | n/a — no notification is defined. |
| **Cleanup** | None — nothing was written. |
| **Evidence** | Six HTTP captures · the before/after audit counts · the `204` from the logout probe |
| **Result / Defect** | Not Run · — |
| **Notes** | Partition (f) is the easy mistake: a dashed GUID looks valid to a human and is rejected by the `"N"` exact-format parse. Include it so a future change to `Guid.TryParse` (non-exact) would be caught. |

#### TC-AUTH-STATE-007 — `Live → Revoked` for the whole family on logout  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · URS-006 (`01-User-Requirements-Specification.md:29`); ADR-0009 §Decision.5 · `RSK-AUTH-031` |
| **Level / Type / Technique** | Integration · Functional (positive) · State Transition — every `Live` member of the family moves to `Revoked` in one operation |
| **Priority / Severity / Automation** | Critical · High · Yes (integration) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | Family `F` built as login → refresh → refresh, giving `T1` and `T2` `Rotated` and `T3` `Live` |
| **Test Data** | `T3` (the current cookie) |
| **Steps** | 1. `POST /api/auth/logout` with `Cookie: qams_rt=T3`. 2. `SELECT id, revoked_at_utc, replaced_by_id FROM qams.refresh_session WHERE family_id='F' ORDER BY created_at_utc;`. 3. Attempt `POST /api/auth/refresh` with `T3`. |
| **Expected UI** | The SPA clears its in-memory session in the `next` handler and the sign-in page renders (`auth.service.ts:137-140`). |
| **Expected API** | Step 1: `204` with the deletion `Set-Cookie` on `path=/api/auth`. Step 3: `401` `code` `AUTH-008`. |
| **Expected DB** | All three rows `revoked_at_utc` non-null. `T1`/`T2` keep their earlier rotation timestamps (`??=`); only `T3` gains the logout instant. `replaced_by_id` remains `NULL` on `T3`. |
| **Expected Audit** | Exactly one `LOGOUT` row: `actor IS NULL`, `detail='family=<F:N>'`, `tenant_id IS NULL`. Note that the row identifies the family but **not the user** — `LogoutHandler` passes `null` for the actor even though `session.UserId` is in hand (`RefreshSessions.cs:177`). |
| **Expected Notification** | n/a — no notification is defined for logout. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE family_id='F';` |
| **Evidence** | HTTP captures · the three-row SQL result with distinct timestamps · the `LOGOUT` row |
| **Result / Defect** | Not Run · — |
| **Notes** | The actor-less `LOGOUT` row is an evidence weakness under URS-016: a reviewer cannot answer "when did this user sign out?" from the ledger without joining through `qams.refresh_session`, which the purge deletes after 7 days. Raised in the batch note. |

#### TC-AUTH-STATE-008 — Logout revokes an already-expired session (no liveness check on the logout path)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · no URS covers logout of a dead session; behaviour at `RefreshSessions.cs:157-179` (there is no `IsLive` call) · `RSK-AUTH-031` |
| **Level / Type / Technique** | Integration · Functional (positive) · State Transition — the `Expired → Revoked` edge that only logout can take |
| **Priority / Severity / Automation** | Low · Low · Yes (integration) |
| **Role / Permission / Tenant** | TenantAdmin · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | One row `S1` with `revoked_at_utc IS NULL` and `expires_at_utc` forced to `now() - interval '1 day'` |
| **Test Data** | Cookie `T1` for `S1` |
| **Steps** | 1. `POST /api/auth/refresh` with `T1` → expect `401 AUTH-009` and no state change (confirms the row is `Expired`). 2. `POST /api/auth/logout` with `T1`. 3. `SELECT revoked_at_utc FROM qams.refresh_session WHERE id='S1';`. |
| **Expected UI** | n/a — API-level case. |
| **Expected API** | Step 1: `401` `code` `AUTH-009`. Step 2: `204`. |
| **Expected DB** | After step 2: `revoked_at_utc` set to the logout instant. The logout path filters only on `FamilyId` and `RevokedAtUtc == null` (`RefreshSessions.cs:173-175`) — expiry is not consulted. |
| **Expected Audit** | One `LOGOUT` row from step 2; **no** row from step 1 (the expiry branch is silent). |
| **Expected Notification** | n/a — no notification is defined. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE id='S1';` |
| **Evidence** | Both HTTP captures · the before/after `revoked_at_utc` values |
| **Result / Defect** | Not Run · — |
| **Notes** | Harmless but worth pinning: it means an expired row's `revoked_at_utc` can post-date its `expires_at_utc`, which any reporting query over session lifetimes must tolerate. |

#### TC-AUTH-STATE-009 — Logout with an absent or unknown cookie is a silent `204` no-op  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · ADR-0009 §Decision.5 ("an absent/invalid cookie is a no-op", `RefreshSessions.cs:146-152`), gap `GAP-AUTH-902` · `RSK-AUTH-033` |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning — {no cookie, unparsable cookie, well-formed unknown session, well-formed known session with a wrong secret} |
| **Priority / Severity / Automation** | Medium · Low · Yes (functional) |
| **Role / Permission / Tenant** | Anonymous · n/a — anonymous endpoint · n/a |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | One unrelated `Live` row exists (to prove it is not collaterally revoked); its id is `S1` and its correct secret is `SEC1` |
| **Test Data** | (a) no `Cookie` header; (b) `qams_rt=garbage`; (c) `qams_rt=0192aaaabbbbccccddddeeeeffff0000.AAAA` (unknown id); (d) `qams_rt=<S1:N>.WRONGSECRET` (known id, wrong secret) |
| **Steps** | 1. Record the `audit.security_event` total count. 2. `POST /api/auth/logout` once per partition. 3. `SELECT revoked_at_utc FROM qams.refresh_session WHERE id='S1';`. 4. Re-run the count. |
| **Expected UI** | The SPA treats any logout response — success or error — as a sign-out (`auth.service.ts:139` clears on both `next` and `error`). |
| **Expected API** | All four: `204 No Content`, each with the `qams_rt` deletion `Set-Cookie` on `path=/api/auth` (the controller sets it unconditionally, `AuthController.cs:79-81`). |
| **Expected DB** | `S1.revoked_at_utc IS NULL` after all four — partition (d) exits at the hash comparison (`RefreshSessions.cs:167-171`) without revoking anything. |
| **Expected Audit** | The count is **unchanged** — `LogoutHandler` writes its `LOGOUT` row only after a successful hash match (`RefreshSessions.cs:177`, reached only past the guard at `:167`). |
| **Expected Notification** | n/a — no notification is defined. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE id='S1';` |
| **Evidence** | Four HTTP captures · the unchanged `S1` row · before/after audit counts |
| **Result / Defect** | Not Run · — |
| **Notes** | Partition (d) is the security-relevant one: a known session id with a guessed secret must not revoke the session, or an attacker who learns an id could sign a user out at will. Assert `S1` alive explicitly. |

#### TC-AUTH-STATE-010 — `Live` + inactive user: refresh revokes that one session and returns `AUTH-006`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · **URS-006** (`01-User-Requirements-Specification.md:29`); ADR-0009 §Decision.5 · `RSK-AUTH-031` |
| **Level / Type / Technique** | Integration · Functional (negative) · State Transition — `Live → Revoked` driven by an *account* state change rather than a session operation |
| **Priority / Severity / Automation** | Critical · Critical · Yes (integration) |
| **Role / Permission / Tenant** | Subject: Analyst `analyst-c@demo-lab.local`; actor: TenantAdmin with `users.manage` · `users.manage` · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | The subject has exactly **two** families, `FA` (cookie `A1`, `Live`) and `FB` (cookie `B1`, `Live`); the subject is `is_active=true` |
| **Test Data** | `A1`, `B1`, subject id `UV` |
| **Steps** | 1. `POST /api/users/{UV}/deactivate` as the admin. 2. `POST /api/auth/refresh` with `A1`. 3. `SELECT family_id, revoked_at_utc FROM qams.refresh_session WHERE user_id='UV';`. 4. `POST /api/auth/refresh` with `B1`. 5. Re-run the query. |
| **Expected UI** | n/a — API/DB-level case; the browser view is `TC-AUTH-SEC-017`. |
| **Expected API** | Steps 2 and 4: `401` `application/problem+json`, `code` `AUTH-006`, title `Your session is no longer valid. Please sign in again.` (`RefreshSessions.cs:120-124`). |
| **Expected DB** | After step 2: `FA`'s row `revoked_at_utc` set, `FB`'s row still `NULL` — the handler revokes only `session`, not the user's other rows (`RefreshSessions.cs:122`). After step 4: both non-null. **No** successor row is inserted in either family. |
| **Expected Audit** | **No** `audit.security_event` row for either 401 — the `AUTH-006` branch writes none, so the ledger contains no record that two sessions were terminated by deactivation. Assert the count is unchanged. |
| **Expected Notification** | n/a — no notification is defined. |
| **Cleanup** | `POST /api/users/{UV}/reactivate`; `DELETE FROM qams.refresh_session WHERE user_id='UV';` |
| **Evidence** | Both HTTP captures · the two SQL snapshots showing the lazy, one-at-a-time revocation · the unchanged audit count |
| **Result / Defect** | Not Run · — |
| **Notes** | Ordering matters: this branch is reached only after the reuse check (`:101`) and the liveness check (`:114`). See `TC-AUTH-STATE-011` for the case where the earlier branch wins. |

#### TC-AUTH-STATE-011 — A rotated token belonging to a deactivated user yields `AUTH-008`, not `AUTH-006`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · no URS specifies branch precedence; ordering at `RefreshSessions.cs:101, 114, 119` · `RSK-AUTH-031` |
| **Level / Type / Technique** | API · Functional (negative) · Path — the guard sequence, exercised with two conditions simultaneously true |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | Subject: Analyst; actor: TenantAdmin with `users.manage` · `users.manage` · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | Subject has family `F` with `T1` `Rotated` and `T2` `Live`, and is then deactivated |
| **Test Data** | `T1`, subject id `UV` |
| **Steps** | 1. Build the rotated `T1`. 2. `POST /api/users/{UV}/deactivate`. 3. `POST /api/auth/refresh` with `T1`. 4. `POST /api/auth/refresh` with `T2`. |
| **Expected UI** | n/a — API-level ordering case. |
| **Expected API** | Step 3: `401` `code` **`AUTH-008`** — the reuse branch at `:101` precedes the user-active load at `:119`, so the account state is never consulted. Step 4: `401` `code` **`AUTH-008`** as well, because step 3 already revoked the family. |
| **Expected DB** | After step 3 both rows are revoked (family revocation), so no `AUTH-006`-style single-row revocation occurs at all. |
| **Expected Audit** | Two `REFRESH_REUSE_DETECTED` rows, `tenant_id IS NULL`. **No** row attributes the termination to the deactivation, even though that is the operative cause. |
| **Expected Notification** | n/a — no notification is defined. |
| **Cleanup** | Reactivate the subject; `DELETE FROM qams.refresh_session WHERE user_id='UV';` |
| **Evidence** | Both problem+json bodies showing `AUTH-008` · the two audit rows |
| **Result / Defect** | Not Run · — |
| **Notes** | The deny outcome is correct in every case; only the code and the ledger label mislead. Pin the ordering so a refactor that hoists the user load above the reuse check is caught — that refactor would change the code from `AUTH-008` to `AUTH-006` and would also stop revoking the family. |

#### TC-AUTH-STATE-012 — A token that is both revoked and expired yields `AUTH-008`, not `AUTH-009`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · no URS specifies branch precedence; ordering at `RefreshSessions.cs:101` before `:114` · `RSK-AUTH-035` |
| **Level / Type / Technique** | Integration · Functional (negative) · Path — the two guards with both conditions true |
| **Priority / Severity / Automation** | Low · Low · Yes (integration) |
| **Role / Permission / Tenant** | Anonymous holder of a dead cookie · n/a — anonymous endpoint · `demo-lab` |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | Row `S1`: `revoked_at_utc` non-null (from a logout) **and** `expires_at_utc` forced to `now() - interval '1 day'` |
| **Test Data** | Cookie `T1` for `S1` |
| **Steps** | 1. `UPDATE qams.refresh_session SET expires_at_utc = now() - interval '1 day' WHERE id='S1';` (the row is already revoked). 2. `POST /api/auth/refresh` with `T1`. 3. Inspect the response `code`. |
| **Expected UI** | n/a — API-level ordering case. |
| **Expected API** | `401` `code` **`AUTH-008`** with the title `The session has been revoked. Please sign in again.` — the revocation check runs first (`:101`), so expiry is never evaluated. |
| **Expected DB** | The family revocation re-runs over a set that is already fully revoked, so nothing changes: `revoked_at_utc` retains its original value (`??=`, `RefreshSession.cs:89`). |
| **Expected Audit** | One new `REFRESH_REUSE_DETECTED` row — reuse detection fires even though the token could not have been used regardless, because it was expired. |
| **Expected Notification** | n/a — no notification is defined. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE id='S1';` |
| **Evidence** | HTTP capture with the `code` · the unchanged `revoked_at_utc` · the new audit row |
| **Result / Defect** | Not Run · — |
| **Notes** | Together with `TC-AUTH-STATE-011` this pins the complete guard order: reuse → liveness → user-active. Three cases, one invariant; do not merge them, because each one fails differently under a reordering. |

#### TC-AUTH-STATE-013 — Domain: rotating an already-revoked session throws `AUTH-000`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · no URS — domain invariant at `RefreshSession.cs:77-86`, gap `GAP-AUTH-902` · `RSK-AUTH-030` |
| **Level / Type / Technique** | Unit · Structural (negative) · Branch — the single `if (RevokedAtUtc is not null)` guard inside `Rotate` |
| **Priority / Severity / Automation** | Medium · Medium · Yes (Domain.UnitTests — no database, no HTTP) |
| **Role / Permission / Tenant** | n/a — pure domain unit test · n/a — no permission gate applies below the application layer · n/a — `RefreshSession` is not tenant-scoped (`RefreshSession.cs:11-13`) |
| **Environment** | `dotnet test tests/NT.QAMS.Domain.UnitTests` with the user-local SDK prefix (conventions §3) |
| **Preconditions** | None — the aggregate is constructed in the test |
| **Test Data** | `RefreshSession.Start(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.Empty, "A".PadRight(64,'B'), now, TimeSpan.FromDays(14))` |
| **Steps** | 1. `Start` a session; assert `IsLive(now)` is `true` and `WasRotated` is `false`. 2. `Revoke(now)`. 3. Call `Rotate(Guid.CreateVersion7(), now)` and capture the exception. 4. Assert the session's `ReplacedById` is still `null`. 5. Separately, `Start` a session, `Rotate` it once, then `Rotate` again and capture the exception. |
| **Expected UI** | n/a — unit level. |
| **Expected API** | n/a — no HTTP is involved. Note for traceability: were this reachable over HTTP, `AUTH-000` maps to `401` by the `AUTH-` prefix rule (`DomainExceptionHandler.cs:54-59`) despite being a programming error. |
| **Expected DB** | n/a — no persistence in a domain unit test. |
| **Expected Audit** | n/a — the domain writes no ledger rows. |
| **Expected Notification** | n/a — unit level. |
| **Cleanup** | n/a — no state outside the test. |
| **Evidence** | Test output showing `DomainException` with `Code == "AUTH-000"` and message `A revoked session cannot rotate.` |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 5 proves the guard also blocks double rotation, because `Rotate` sets `RevokedAtUtc` as well as `ReplacedById` (`RefreshSession.cs:84-85`). `Guid.Empty` as `familyId` must be asserted to produce a **generated** family, not an empty one (`:69`) — assert that in step 1. |

#### TC-AUTH-STATE-014 — Domain: `Revoke` is idempotent and the first revocation timestamp wins  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · no URS — domain invariant at `RefreshSession.cs:89` (`RevokedAtUtc ??= now`), gap `GAP-AUTH-902` · `RSK-AUTH-031` |
| **Level / Type / Technique** | Unit · Structural (positive) · Condition — the null-coalescing assignment's two states |
| **Priority / Severity / Automation** | Medium · Medium · Yes (Domain.UnitTests) |
| **Role / Permission / Tenant** | n/a — pure domain unit test · n/a — no permission gate below the application layer · n/a — not tenant-scoped |
| **Environment** | `dotnet test tests/NT.QAMS.Domain.UnitTests` |
| **Preconditions** | None |
| **Test Data** | `t0 = 2026-08-01T10:00:00Z`, `t1 = 2026-08-01T10:05:00Z` |
| **Steps** | 1. `Start` a session at `t0`. 2. `Revoke(t0)`; assert `RevokedAtUtc == t0` and `IsLive(t0) == false`. 3. `Revoke(t1)`; assert `RevokedAtUtc` is **still** `t0`. 4. Assert `ReplacedById` is still `null` (revocation is not rotation). 5. On a second instance: `Rotate(successorId, t0)` then `Revoke(t1)`; assert `RevokedAtUtc == t0` and `ReplacedById == successorId` — the rotation stamp survives the later revocation. |
| **Expected UI** | n/a — unit level. |
| **Expected API** | n/a — no HTTP is involved. |
| **Expected DB** | n/a — no persistence. This invariant is what makes the family-revocation assertions of `TC-AUTH-SEC-012` and `TC-AUTH-STATE-007` meaningful. |
| **Expected Audit** | n/a — the domain writes no ledger rows. |
| **Expected Notification** | n/a — unit level. |
| **Cleanup** | n/a. |
| **Evidence** | Test output asserting the two timestamps and the preserved `ReplacedById` |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 5 is the one that matters at the integration level: family revocation sweeps rows that include already-rotated links, and the rotation chronology must survive it. |

#### TC-AUTH-STATE-015 — `Expired → Purged` seven days after `expires_at_utc`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · ADR-0009 §Decision.5 ("Long-dead sessions are purged in the outbox retention cycle", `ADR-0009…md:39-40`), gap `GAP-AUTH-902` · `RSK-AUTH-035` |
| **Level / Type / Technique** | Integration · Functional (positive) · BVA — `expires_at_utc` at the retention cutoff minus 1 s, exactly at it, and plus 1 s |
| **Priority / Severity / Automation** | Medium · Low · Yes (integration; the cutoff is reached by back-dating `expires_at_utc`, not by waiting) |
| **Role / Permission / Tenant** | n/a — the purge runs under `ICurrentTenantSetter.Elevate()` in a hosted service (`OutboxProcessor.cs:245-247`) · n/a — no HTTP permission applies · n/a — the table has no `tenant_id` |
| **Environment** | API `:5080` running (the `OutboxProcessor` hosted service must be alive) + PostgreSQL `ntqams` |
| **Preconditions** | Three rows seeded with `expires_at_utc` = `now() - interval '7 days' + interval '1 second'` (row X, inside the window), `now() - interval '7 days'` (row Y, exactly at the cutoff), `now() - interval '7 days' - interval '1 second'` (row Z, past it) |
| **Test Data** | Row ids X, Y, Z; retention constant `RefreshSessionRetentionDays = 7` (`OutboxProcessor.cs:272`) |
| **Steps** | 1. Insert X, Y, Z with valid 64-upper-hex `token_hash` values. 2. Restart the API so the processor's first pass runs (the cycle runs hourly with a startup pass — `OutboxProcessor.cs:53,59,74-77`) **or** wait one hour. 3. `SELECT id FROM qams.refresh_session WHERE id IN (X,Y,Z);`. |
| **Expected UI** | n/a — background-service case. |
| **Expected API** | n/a — no HTTP call; the purge is a `BackgroundService`, not an endpoint. |
| **Expected DB** | X survives. Z is deleted. Y's fate depends on the strict `<` in `s.ExpiresAtUtc < refreshCutoff` (`OutboxProcessor.cs:267-269`) evaluated against a cutoff computed at execution time, which will have advanced past Y's timestamp by then — expect Y deleted, and **record the observed outcome** rather than asserting a knife-edge that depends on scheduler latency. |
| **Expected Audit** | No `audit.security_event` row — the purge is silent. There is no record that session rows were destroyed. |
| **Expected Notification** | n/a — no notification is defined for retention purges. |
| **Cleanup** | `DELETE FROM qams.refresh_session WHERE id IN (X,Y,Z);` for whatever survived |
| **Evidence** | The seeded-row list · the post-purge query result · the API log line for the purge cycle from `%TEMP%\ntqms-dev\` |
| **Result / Defect** | Not Run · — |
| **Notes** | The purge deletes on `expires_at_utc` alone — a **revoked** row whose `expires_at_utc` is still in the future is retained, which is deliberate: it keeps reuse detection working on a stolen token for the rest of its nominal lifetime (`OutboxProcessor.cs:263-266`). Assert that too by seeding a revoked, not-yet-expired row and confirming it survives. |

#### TC-AUTH-STATE-016 — After the purge, a stolen rotated token yields `AUTH-009` and no theft signal  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | AUTH · no URS — consequence of the purge predicate (`OutboxProcessor.cs:267-269`) meeting the reuse branch (`RefreshSessions.cs:101`) · `RSK-AUTH-035` |
| **Level / Type / Technique** | Integration · Security (characterisation) · Error Guessing — the window in which reuse detection stops working |
| **Priority / Severity / Automation** | Medium · Medium · Yes (integration) |
| **Role / Permission / Tenant** | Attacker holding an old stolen cookie · n/a — anonymous endpoint · n/a |
| **Environment** | API `:5080` + PostgreSQL `ntqams` |
| **Preconditions** | Family `F` with `T1` `Rotated`; both rows back-dated so `expires_at_utc < now() - interval '7 days'`; one purge cycle has run and removed them (confirm with `SELECT count(*) … WHERE family_id='F'` = 0) |
| **Test Data** | `T1` (the stolen, now-orphaned cookie) |
| **Steps** | 1. Confirm the family rows are gone. 2. `POST /api/auth/refresh` with `T1`. 3. Query `audit.security_event` for the most recent row. |
| **Expected UI** | n/a — API-level probe. |
| **Expected API** | `401` `code` **`AUTH-009`** — the session id no longer resolves, so the unknown-session branch fires (`RefreshSessions.cs:92-99`); reuse detection cannot run on a row that no longer exists. |
| **Expected DB** | Nothing changes; `qams.refresh_session` has no row for `F`. |
| **Expected Audit** | One `REFRESH_INVALID` row with `detail` = the session id, `tenant_id IS NULL` — **not** `REFRESH_REUSE_DETECTED`. The theft signal is downgraded to a generic invalid-token event once the rows age out. |
| **Expected Notification** | n/a — no notification policy consumes either event. |
| **Cleanup** | None — the audit row is append-only. |
| **Evidence** | The zero-row family query · HTTP capture · the `REFRESH_INVALID` row |
| **Result / Defect** | Not Run · — |
| **Notes** | `[ID]` — the behaviour is a designed consequence (the comment at `OutboxProcessor.cs:263-266` explicitly reasons about keeping reuse detection alive "within the window"), and the window is `expires_at_utc + 7 days`, i.e. **21 days** after a session's last rotation at the default 14-day lifetime. That is generous; the case documents where the guarantee ends rather than alleging a defect. |

---

## Batch coverage note

**Covered.** All 41 cases in this file are authored against source that was opened and read in this pass, plus a read-only `psql` measurement of `qams.refresh_session` on dev DB `ntqams` (2026-08-01: `relrowsecurity=f`, `relforcerowsecurity=f`, no policies, PK `(id)`, `CHECK (token_hash ~ '^[0-9A-F]{64}$')`, columns exactly `id, user_id, family_id, token_hash, created_at_utc, expires_at_utc, revoked_at_utc, replaced_by_id`). Files read in full: `Application/IdentityAccess/Commands/RefreshSessions.cs`, `Domain/IdentityAccess/RefreshSession.cs`, `WebApi/Controllers/AuthController.cs`, `WebApi/Middleware/RequestIdentity.cs`, `Infrastructure/Security/SecurityAdapters.cs`, `Infrastructure/Persistence/Configurations/RefreshSessionConfiguration.cs`, `Infrastructure/Persistence/Migrations/20260728130923_Phase7RefreshSessions.cs`, `Application/IdentityAccess/Commands/Login.cs`, `docs/adr/ADR-0009-refresh-token-session-model.md`, `tests/NT.QAMS.WebApi.FunctionalTests/RefreshSessionTests.cs`; read in part: `WebApi/Security/RateLimiting.cs`, `Infrastructure/Persistence/Outbox/OutboxProcessor.cs:243-272`, `Infrastructure/Persistence/Migrations/20260731191212_Hardening3_CheckDomains.cs:169-170`, `Infrastructure/DependencyInjection.cs:91-94`, `Infrastructure/Compliance/ComplianceLedgerServices.cs:68-83`, `WebApi/Program.cs:248-272`, `frontend/src/app/core/auth.service.ts:18-175`, `Application/IdentityAccess/Commands/UserManagement.cs:105-140`, `Contracts/IdentityAccess/UserContracts.cs:1-28`.

By slice item: access-JWT lifetime and memory-only handling — `TC-AUTH-SEC-005`, `-006`, `-007`; cookie attributes — `-001`, `-002`, `-003`, `-004`; SHA-256-only storage — `-008`, `-009`, `-010`; rotation on every refresh — `-002`, `TC-AUTH-STATE-001`; reuse detection and family revocation — `TC-AUTH-SEC-012`, `-013`, `TC-AUTH-STATE-002`, `-011`, `-012`, `-016`; logout revocation — `TC-AUTH-SEC-003`, `-015`, `TC-AUTH-STATE-007`, `-008`, `-009`; revoked-token reuse — `TC-AUTH-STATE-003`; concurrent sessions — `TC-AUTH-SEC-014`, `-016`; `AUTH-006` / `AUTH-007` — `TC-AUTH-SEC-017`, `-018`, `-019`, `-021`, `TC-AUTH-STATE-010`; session listing, revocation, self-revocation and the cross-tenant attempt — `TC-AUTH-SEC-022`…`-025`, all `[GD]`.

**Could not cover, and why.**

1. **Session listing, administrative revocation, self-revocation of one session, and any cross-tenant session-revocation refusal are not implemented.** `grep -i session tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` returns nothing, and `grep -rin session src/NT.QAMS.WebApi/Controllers/` matches only a code comment in `DocumentsController.cs:117`. Four cases are therefore written `[GD]` against `GAP-AUTH-901` with implementable acceptance criteria in their `Notes` rows. No positive case was fabricated.
2. **The true concurrent-refresh race (`TC-AUTH-SEC-016`) cannot be made deterministic from the client.** It is authored as a 20-iteration characterisation probe with the outcome tallied rather than predicted, because predicting one would be inventing behaviour. If any iteration leaves two `revoked_at_utc IS NULL` rows in one `family_id`, that is a new defect requiring its own gap — the case says so rather than pre-judging it.
3. **`SameSite=Strict` cannot be exercised for effect.** ADR-0002/0007 make the deployment same-origin, so there is no legitimate cross-site request to test the attribute against; `TC-AUTH-SEC-004` asserts the attribute's presence in the cookie jar and nothing more.
4. **The retention-purge boundary at exactly the cutoff (`TC-AUTH-STATE-015` row Y) is scheduler-dependent.** The case records the observed outcome instead of asserting a knife-edge, since `RunRetentionPurgeAsync` computes its cutoff at execution time and the hosted service's latency is not controllable from the test.
5. **RLS cases for `qams.refresh_session` were deliberately not authored here** even though the measurement is in hand, because the `TC-AUTH-RLS-*` block belongs to batch D per the front matter's reservation table. The measured fact is recorded in `TC-AUTH-SEC-025`'s `Notes` so batch D can cite it without re-measuring.

**ID-block conflict to resolve before the traceability matrix is built.** The front matter's ID reservation table (`10-module-auth.md:53-55`) assigns `TC-AUTH-STATE-001…-025` **and** `TC-AUTH-INT-001…-030` to batch C, and `TC-AUTH-SEC-001…-040` to batch D. This authoring pass was instructed to consume `TC-AUTH-SEC-001…` and `TC-AUTH-STATE-001…`, which is what it did. Consequences: (a) `TC-AUTH-SEC-001…-025` are now **consumed by batch C**, so batch D must start at `TC-AUTH-SEC-026`; (b) `TC-AUTH-INT-001…-030` remains **entirely unconsumed** — the integration-level session behaviour that reservation anticipated is delivered here under `SEC`/`STATE` ids, so the `INT` range is a coverage hole in name only, but the front-matter table should be corrected rather than left to imply thirty missing cases. Raising this rather than silently renumbering, per conventions §6.3.

**New gaps found in this batch.**

---

**GAP-AUTH-901 — There is no session inventory and no way to terminate a session other than the one you are holding**

- **Source reference:** `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` contains no route matching `session` (measured, empty grep); `AuthController.cs:29-159` exposes ten endpoints, none of which reads or revokes sessions; `LogoutHandler` filters on `FamilyId` only (`RefreshSessions.cs:173-175`); `SetUserActiveHandler` touches no session row (`UserManagement.cs:131-139`); `PermissionCatalog.Modules` (`PermissionCatalog.cs:132-186`) defines no session-related module; `frontend/src/app/app.routes.ts` has no session route.
- **Description:** `qams.refresh_session` can hold an unbounded number of live families per user (`TC-AUTH-SEC-014`), each with a sliding 14-day horizon that renews on every use (`TC-AUTH-STATE-001`). Neither the user nor a tenant administrator can enumerate them, and neither can revoke one. Logout revokes only the family whose cookie is presented; deactivation revokes nothing eagerly (`GAP-AUTH-014`, confirmed by `TC-AUTH-SEC-021`). The only mechanisms that ever clear a session row are the user presenting its own cookie, reuse detection on that family, or the 7-day-past-expiry purge.
- **Impact:** an incident responder told "this analyst's laptop was stolen" has no containment action available. Deactivating the account denies access (correctly) but leaves the rows live and writes no security event naming the termination, so the Part-11 §11.10(d) question "were this user's sessions terminated, and when?" cannot be answered from the system. A user who left themselves signed in on a shared workstation cannot end that session from anywhere else. Concurrent-session count is also unbounded and unobservable, so anomalous accumulation cannot be detected.
- **Testing limitation:** four cases in this batch (`TC-AUTH-SEC-022`…`-025`) can only assert routing `404`s and the unchanged live-session count. There is no negative-authorization arm to test, because there is no endpoint to be refused by. The cross-tenant case can only establish the isolation baseline through the *existing* `USER-404` behaviour of `TenantUserLoader`.
- **Recommended clarification:** does the laboratory's security procedure require an administrator to be able to terminate a user's sessions, and does it require the user to see their own? Should there be a concurrent-session cap?
- **Suggested acceptance criteria:** *(a)* `GET /api/auth/me/sessions` returns the caller's live sessions with `{sessionId, familyId, createdAtUtc, expiresAtUtc, isCurrent}` and never `token_hash`; *(b)* `POST /api/auth/logout-all` revokes every live row for the caller; *(c)* `POST /api/users/{id}/revoke-sessions` gated `[RequirePermission(PermissionCatalog.Users, PermissionAction.Manage)]` with a matching command policy, resolving the subject through `TenantUserLoader.LoadAsync` so a cross-tenant id yields `USER-404`; *(d)* each of the three writes a distinct `audit.security_event` type with `tenant_id` stamped; *(e)* deactivation calls the same revocation path (closing `GAP-AUTH-014` in the same change); *(f)* the API-surface snapshot is updated in the same commit.
- **Severity:** **Major**
- **Responsible role:** Security Owner + Lead Developer

---

**GAP-AUTH-902 — The ADR-0009 session model has no requirement to trace to**

- **Source reference:** `docs/validation/01-User-Requirements-Specification.md:24-33` (`URS-001`…`URS-010`) and the post-baseline set `URS-056`…`URS-107` in `docs/validation/06-Revalidation-Delta-v1.38-v1.50.md` Part A. `URS-001` covers authentication by identity and password; `URS-006` covers per-request re-validation; `URS-007` covers a client idle timeout. **No requirement states** that sessions are carried by a rotating refresh token, that the token is stored only as a SHA-256 hash, that the cookie is `HttpOnly`/`Secure`/`SameSite=Strict`/`Path=/api/auth`, that reuse triggers family revocation, that the access token is memory-only, or that the access-token and refresh horizons are 15 minutes and 14 days.
- **Description:** the entire session architecture is documented only in `docs/adr/ADR-0009-refresh-token-session-model.md`, an engineering decision record. Twenty-two of the forty-one cases in this batch have no URS to cite and trace to the ADR plus a source line instead.
- **Impact:** the RTM cannot show requirement coverage for the controls that actually protect the session, so a validation reviewer reading the RTM sees `URS-001`/`URS-006` satisfied and never learns that cookie hardening, hash-only storage and reuse detection exist — or, more to the point, would never notice if one of them were removed. An ADR is a design record, not a user requirement, and a change to it triggers no revalidation obligation.
- **Testing limitation:** none for execution — every case is executable. The limitation is traceability: these cases cannot be rolled up under a requirement id, so they will appear in the RTM as implementation-derived coverage with no requirement parent.
- **Recommended clarification:** should the ADR-0009 controls be promoted into the URS as post-baseline requirements (the `URS-056`…`URS-107` mechanism already exists for exactly this), and if so at what granularity — one requirement for the session model, or one per control?
- **Suggested acceptance criteria:** *(a)* new post-baseline URS entries in `docs/validation/06-Revalidation-Delta-v1.38-v1.50.md` Part A state the access-token lifetime and memory-only handling, the refresh-cookie attribute set, hash-only storage, rotation-with-reuse-detection, and the logout revocation scope; *(b)* the FRA gains a matching area row so the testing rigor is derived, not asserted; *(c)* this batch's cases are re-traced from `GAP-AUTH-902` to those ids; *(d)* the RTM shows no ADR-only trace for a security control.
- **Severity:** **Moderate**
- **Responsible role:** Quality Manager (requirement) + Solution Architect

---

**Observations recorded but not raised as gaps** (the security outcome is correct in each; only the evidence or the label is imprecise):

- A cookie retried after a **normal logout** produces `REFRESH_REUSE_DETECTED`, indistinguishable in the ledger from an actual theft (`TC-AUTH-SEC-003`, `TC-AUTH-STATE-003`).
- Termination caused by **deactivation** is attributed to reuse when the presented link happens to be a rotated one (`TC-AUTH-STATE-011`), and is recorded by no security event at all when it is not (`TC-AUTH-STATE-010`).
- The `LOGOUT` security-event row carries `actor IS NULL` and `tenant_id IS NULL` although `session.UserId` is in hand at the write (`RefreshSessions.cs:177`), so the ledger cannot answer "when did this user sign out?" without joining to a table the purge deletes (`TC-AUTH-STATE-007`).
- `AUTH-007` (role changed) writes **no** security event, although URS-016 lists "privilege changes" among the events to be recorded (`TC-AUTH-SEC-018`).
- The refresh lifetime is a **sliding** 14 days renewed on every rotation (`RefreshSession.cs:72`), so an actively used session never ages out — which sharpens the existing `GAP-AUTH-012` (idle timeout is client-side only) rather than constituting a new one.

*End of AUTH detailed cases, batch C. Ids consumed: `TC-AUTH-SEC-001`…`-025`, `TC-AUTH-STATE-001`…`-016` (41 cases).*
