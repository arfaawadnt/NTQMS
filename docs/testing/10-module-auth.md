# 10 — Authentication, Session, MFA, Password, Electronic Signature, Access Review

**Module code:** `AUTH`
**System under test:** NT.QMS **v1.51.2** (repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master`)
**Binding conventions:** `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md` — the 28-field case format (§4), the canonical case block (§8), evidence labels `[IV]`/`[RNV]`/`[ID]`/`[GD]`, the ID convention (§5) and the honesty rules (§6) govern this module and every case file that consumes its reserved ids.
**Inspection date:** 2026-08-01. **Inspection method:** source read (every file in scope opened) + read-only `psql` against dev DB `ntqams` for schema/RLS facts.

**Completeness statement.** This file is **front matter only**, per the split convention (conventions §7). It delivers: the correction to ground truth (§0), the implementation inventory (§1), brief-vs-code divergences (§2), state-transition matrices (§3), decision tables (§4), UAT scenarios (§6), exploratory charters (§7) and the module gap register (§8). It **deliberately contains no `## 5. Detailed test cases`** — detailed cases are authored into `10-module-auth-cases-<A…F>.md` by separate passes against the id reservation below. A reserved range with no matching case file is a coverage hole, not a delivered case.

---

## Scope actually inspected

| Layer | File | Read |
|---|---|---|
| Domain | `src/NT.QAMS.Domain/IdentityAccess/UserAccount.cs` (294 ln) | full |
| Domain | `src/NT.QAMS.Domain/IdentityAccess/RefreshSession.cs` (90 ln) | full |
| Domain | `src/NT.QAMS.Domain/IdentityAccess/PasswordHistoryEntry.cs` (14 ln) | full |
| Domain | `src/NT.QAMS.Domain/IdentityAccess/UserAccessReview.cs` (62 ln) | full |
| Application | `src/NT.QAMS.Application/IdentityAccess/Commands/Login.cs` (238 ln) | full |
| Application | `src/NT.QAMS.Application/IdentityAccess/Commands/MfaAndPin.cs` (83 ln) | full |
| Application | `src/NT.QAMS.Application/IdentityAccess/Commands/RefreshSessions.cs` (180 ln) | full |
| Application | `src/NT.QAMS.Application/IdentityAccess/Commands/UserManagement.cs` (331 ln) | full |
| Application | `src/NT.QAMS.Application/IdentityAccess/Commands/AccessReviewSlice.cs` (77 ln) | full |
| Application | `src/NT.QAMS.Application/IdentityAccess/PasswordRules.cs` (78 ln) | full |
| Application | `src/NT.QAMS.Application/Tenancy/Queries/GetWorkspace.cs` (48 ln) | full |
| Application | `src/NT.QAMS.Application/Abstractions/CommandAuthorization.cs`, `PasswordPolicyOptions.cs`, `Behaviors/AuthorizationBehavior.cs` | full |
| WebApi | `Controllers/AuthController.cs`, `UsersController.cs`, `AccessReviewsController.cs`, `UserDirectoryController.cs` | full |
| WebApi | `Middleware/RequestIdentity.cs` (200 ln), `Middleware/DomainExceptionHandler.cs`, `Middleware/ProblemAuthorizationResultHandler.cs`, `Authorization/RequirePermissionAttribute.cs` | full |
| WebApi | `Security/RateLimiting.cs` (103 ln); `Program.cs:254-272` (pipeline order) | targeted |
| Infrastructure | `Compliance/ComplianceLedgerServices.cs:68-145` (SecurityEventLog + ESignatureService) | targeted |
| Infrastructure | `Security/SecurityAdapters.cs` (hasher + JwtTokenService), `Security/TotpService.cs` | full |
| Infrastructure | `Persistence/Configurations/RefreshSessionConfiguration.cs`, `IdentityAndImprovementConfigurations.cs:8-53,133-142,177-189` | targeted |
| Infrastructure | `Persistence/Migrations/20260728130923_Phase7RefreshSessions.cs`, `20260726213412_UserAccessReview.cs`; `Persistence/Outbox/OutboxProcessor.cs:243-278` | targeted |
| ADR | `docs/adr/ADR-0009-refresh-token-session-model.md` | full |
| Live DB | `pg_class` / `pg_policy` / `pg_constraint` / `information_schema.columns` for the 6 AUTH tables | measured |

**Not inspected in this pass** (so any claim about them is `[RNV]` for case authors): the Angular sign-in surface beyond `core/auth.service.ts:156-166`; `Infrastructure/Authorization/PrivilegeResolution.cs`; `Application/Authorization/SystemRoleCatalog.cs` / `SeededRoleDefault` / `ManageRolesLockoutGuard` internals (they belong to module `RBAC`); the existing test projects were listed but not read.

---

## ID reservation table

`TC-AUTH-<KIND>-<NNN>`, module-local, never renumbered. Reserved **generously** — an unconsumed id is cheaper than a renumber.

| Range | Kind | Slice of scope | Intended batch file |
|---|---|---|---|
| `TC-AUTH-UNIT-001` … `-040` | UNIT | `UserAccount` invariants (Create/ChangeRole/Reset/Change/RegisterFailedLogin/EnrollMfa/ConfirmMfa/SetPin/SetScope), `RefreshSession` (Start/Rotate/Revoke/IsLive/WasRotated), `UserAccessReview` (Open/Complete/immutability), `PasswordRules` predicates | `10-module-auth-cases-A.md` |
| `TC-AUTH-BVA-001` … `-025` | BVA | password length 11/12/13 and 199/200/201; PIN 3/4/5 digits and non-digit; failed-attempt counter 4/5/6; lockout expiry −1s/0/+1s; TOTP window −2/−1/0/+1/+2 steps; `MaxAgeDays` boundary; `HistoryDepth` boundary; refresh lifetime edge | `10-module-auth-cases-A.md` |
| `TC-AUTH-EP-001` … `-015` | EP | email casing/trimming partitions, slug partitions (valid / malformed / unknown / inactive), role-string partitions for `TenantRole.Parse` | `10-module-auth-cases-A.md` |
| `TC-AUTH-API-001` … `-070` | API | all 23 logical AUTH endpoints × happy path + each documented failure code; problem+json shape; status-code mapping | `10-module-auth-cases-B.md` |
| `TC-AUTH-DT-001` … `-025` | DT | the four decision tables in §4 below, one case per rule row | `10-module-auth-cases-B.md` |
| `TC-AUTH-STATE-001` … `-025` | STATE | the three state machines in §3, one case per legal and per illegal transition | `10-module-auth-cases-C.md` |
| `TC-AUTH-INT-001` … `-030` | INT | handler↔PostgreSQL behaviour: password-history append/prune, lockout persistence, refresh family revocation, access-review `accounts_reviewed` snapshot, security-event writes | `10-module-auth-cases-C.md` |
| `TC-AUTH-SEC-001` … `-040` | SEC | rate-limit partitions (global/auth/refresh/e-signature), cookie hardening, reuse detection, anti-enumeration, credential non-disclosure, PIN brute force, hash-only storage, JWT claim tampering, `scope=mfa_enrollment` containment | `10-module-auth-cases-D.md` |
| `TC-AUTH-RLS-001` … `-015` | RLS | `qams.user_access_review`, `qams.user_branch_access`, `qams.user_department_access`, `audit.security_event` isolation; and the **absence** of RLS on `qams.user_account`, `qams.refresh_session`, `saas.password_history` (assert the as-built fact, do not assert a policy that does not exist) | `10-module-auth-cases-D.md` |
| `TC-AUTH-DF-001` … `-012` | DF | data flow of the secret: plaintext password → PBKDF2 hash → `password_hash` → `saas.password_history`; PIN → `pin_hash`; refresh secret → SHA-256 hex → `token_hash`; assert no plaintext reaches any store, log or response | `10-module-auth-cases-E.md` |
| `TC-AUTH-MCDC-001` … `-012` | MCDC | the compound guard `Login.cs:200-204` (`user is null \|\| !IsActive \|\| IsLockedOut \|\| !Verify`) and `Login.cs:212-213` (reuse ban) | `10-module-auth-cases-E.md` |
| `TC-AUTH-OBS-001` … `-010` | OBS | the 12 security-event types actually emitted (§1.6); correlation/trace stamping on AUTH error paths | `10-module-auth-cases-E.md` |
| `TC-AUTH-E2E-001` … `-015` | E2E | browser flows: sign-in → silent refresh → reload persistence → idle lockout → logout; MFA enrolment; PIN set then document publish | `10-module-auth-cases-F.md` |
| `TC-AUTH-UAT-001` … `-012` | UAT | the Gherkin scenarios in §6, one case per scenario | `10-module-auth-cases-F.md` |
| `TC-AUTH-A11Y-001` … `-008` | A11Y | sign-in, MFA-code entry, PIN entry, change-password and signing dialogs | `10-module-auth-cases-F.md` |
| `TC-AUTH-PERF-001` … `-006` | PERF | PBKDF2 cost under the 10/min auth budget; refresh throughput at 60/min; `ActiveSessionMiddleware` per-request DB round trip | `10-module-auth-cases-F.md` |
| `TC-AUTH-EXPL-001` … `-008` | EXPL | the charters in §7 | authored in `-F`, executed as charters |

**Total reserved: 358 ids across 16 kinds.** Kinds deliberately **not** reserved for AUTH: `COMP`, `PATH`, `LOOP`, `ESC`, `WESTGARD`, `STAT`, `RTL`, `DR`, `MUT` — no behaviour in this scope warrants them, and reserving unused kinds invites phantom coverage.

---

## 0. Correction to ground truth

Four claims in `00-GROUND-TRUTH-AND-CONVENTIONS.md` are factually wrong against the v1.51.2 source and the live schema. All four would mislead an AUTH case author, so they are corrected here with proof. Everything else in that file stood up to inspection.

### 0.1 — The canonical case block's lockout facts are wrong (§8, lines 215 and 220)

The canonical block states `Preconditions: user_account.failed_attempts = 5, locked_until_utc set` and `Expected DB: failed_attempts unchanged at 5`.

**Both are impossible states.** `UserAccount.RegisterFailedLogin` sets the lock **and resets the counter to zero in the same operation**:

```csharp
// src/NT.QAMS.Domain/IdentityAccess/UserAccount.cs:209-218
public void RegisterFailedLogin(DateTimeOffset now)
{
    FailedLoginAttempts++;
    if (FailedLoginAttempts >= MaxFailedAttempts)
    {
        LockedUntilUtc = now.AddMinutes(LockoutMinutes);
        FailedLoginAttempts = 0;          // ← counter is cleared AT lockout
        Raise(new UserLockedOut(Id, Email, LockedUntilUtc.Value));
    }
}
```

A locked account therefore always reads `failed_login_attempts = 0`. Additionally the **column is named `failed_login_attempts`**, not `failed_attempts` (measured: `information_schema.columns`, `qams.user_account`; declared by `EFCore.NamingConventions` from `UserAccount.FailedLoginAttempts`, `UserAccount.cs:53`).

**Consequence for authors:** a locked-account precondition is `failed_login_attempts = 0 AND locked_until_utc > now()`; the assertion after a rejected attempt on an already-locked account is `failed_login_attempts` **still 0** and `locked_until_utc` unchanged (the `AUTH-004` branch at `Login.cs:77-80` returns before `RegisterFailedLogin` is reached, so there is no lock extension). Use the block's **shape**, not its **content**.

### 0.2 — `SIG-010` and `SIG-011` are not electronic-signature codes (§2, line 54)

Ground truth lists `SIG-010`, `SIG-011`, `SIG-404` as e-signature codes. Measured:

| Code | Actually thrown by | Meaning |
|---|---|---|
| `SIG-010` | `src/NT.QAMS.Domain/AnalyticalQuality/SigmaAssessment.cs:72` | "A signed-off assessment is immutable." — six-sigma assessment, not e-signature |
| `SIG-011` | `src/NT.QAMS.Domain/AnalyticalQuality/SigmaAssessment.cs:101` | "The assessment is already signed off." |
| `SIG-404` | `ComplianceLedgerServices.cs:94` ("Signer not found") **and** `SigmaAssessmentSlice.cs:73,112` ("Sigma assessment not found") | overloaded across two subsystems |

The `SIG-` prefix is **shared between two unrelated subsystems** with conflicting meanings for the same numbers: `SIG-001` is "PIN not set or incorrect" in `ComplianceLedgerServices.cs:114` but "An analyte is required" in `SigmaAssessment.cs:53`; `SIG-002` is "Account password is incorrect" (`:108`) vs "allowable total error must be positive" (`SigmaAssessment.cs:77`); `SIG-003` is "Account temporarily locked" (`:101`) vs "CV must be a positive percentage" (`SigmaAssessment.cs:82`).

**Consequence for authors:** the e-signature code set is exactly `SIG-001`, `SIG-002`, `SIG-003`, `SIG-404`. Never assert a `SIG-*` code without also asserting the endpoint, because the code alone does not identify the subsystem. This collision is raised as `GAP-AUTH-011`.

### 0.3 — A PIN length rule exists; "4-digit PIN" tests are not Gap-dependent (§2, line 57)

Ground truth: *"No digit-length constraint on the PIN was found in the domain. Any '4-digit PIN' boundary test is `Gap-dependent` until a length rule is located or added."*

The first sentence is true; the conclusion is not. The rule is in the **application validator**:

```csharp
// src/NT.QAMS.Application/IdentityAccess/Commands/MfaAndPin.cs:63-68
public sealed class SetPinValidator : AbstractValidator<SetPinCommand>
{
    public SetPinValidator() =>
        RuleFor(x => x.Pin).NotEmpty().Matches("^[0-9]{4}$")
            .WithMessage("The e-signature PIN must be exactly 4 digits.");
}
```

**Consequence for authors:** PIN boundary cases against `POST /api/auth/signature-pin` are `[IV]`, not `[GD]`, and expect `400` `application/problem+json` with the FluentValidation `errors` envelope (`DomainExceptionHandler.cs:34-44` — note this branch carries **no** `code` extension, only `errors`). The residual defence-in-depth hole — the domain (`UserAccount.SetPin`, `UserAccount.cs:248-256`, checks only non-empty) and the column (`pin_hash text`, no CHECK) impose nothing — is real and is raised as `GAP-AUTH-002`.

### 0.4 — The list of tables outside RLS is incomplete (§2, line 74)

Ground truth: *"The **accepted, permanent** RLS exceptions are `user_account` and `outbox_event` only."*

Measured on dev DB `ntqams`, 2026-08-01:

```
nspname | relname            | relrowsecurity | relforcerowsecurity | policies
--------+--------------------+----------------+---------------------+-----------------
qams    | user_account       | f              | f                   | (none)
qams    | refresh_session    | f              | f                   | (none)
saas    | password_history   | f              | f                   | (none)
qams    | user_access_review | t              | t                   | tenant_isolation
qams    | user_branch_access | t              | t                   | tenant_isolation
qams    | user_department_access | t          | t                   | tenant_isolation
audit   | security_event     | t              | t                   | tenant_isolation
audit   | field_change       | t              | t                   | tenant_isolation
```

`qams.refresh_session` (created by `Phase7RefreshSessions.cs:14-49` — the migration adds **no** RLS statements) and `saas.password_history` (`IdentityAndImprovementConfigurations.cs:133-142`) hold user-linked credential material and are outside RLS. Structurally they *cannot* carry `tenant_isolation`: neither has a `tenant_id` column at all (measured). That is a defensible design, but the ground-truth sentence as written tells an author only two tables lack RLS, and a test that asserts "every non-`user_account`, non-`outbox_event` table is FORCE-RLS" would fail on these two.

**Consequence for authors:** the RLS-exception set relevant to AUTH is `{qams.user_account, qams.refresh_session, saas.password_history}` plus `qams.outbox_event` outside this module. Raised as `GAP-AUTH-006`.

### 0.5 — Minor: the permission catalogue is 31 modules in 8 groups, not 30 in 7 (§2, line 61)

`PermissionCatalog.Modules` contains **31** entries (`src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:132-186`, counted), across **8** group constants (`PermissionCatalog.cs:68-75`: `quality, documents, risk, resources, people, analytical, operations, administration`). Ground truth's prose says "30 permission modules in 7 groups" while its own enumeration lists 31 names spanning all 8 groups; `URS-095` (`docs/validation/06-Revalidation-Delta-v1.38-v1.50.md:154`) independently states "31 modules". The enumeration is right, the count is a typo. Noted because AUTH cites two of those keys.

---

## 1. Implementation inventory

Every claim below was read in the cited file at the cited line. Nothing here is inferred from documentation.

### 1.1 Aggregates and entities

| Type | Kind | Tenancy | File |
|---|---|---|---|
| `UserAccount` | `AggregateRoot` | `IOptionallyTenantScoped` — `TenantId` is `Guid?` | `Domain/IdentityAccess/UserAccount.cs:27` |
| `UserBranchAccess` | owned child of `UserAccount` | shadow `TenantId` stamped from owner | `UserAccount.cs:262-269`; `IdentityAndImprovementConfigurations.cs:27-36` |
| `UserDepartmentAccess` | owned child of `UserAccount` | shadow `TenantId` stamped from owner | `UserAccount.cs:272-279`; `IdentityAndImprovementConfigurations.cs:38-47` |
| `RefreshSession` | `Entity` (not an aggregate root) | **none** — bound to the user, not the tenant | `Domain/IdentityAccess/RefreshSession.cs:15` |
| `PasswordHistoryEntry` | plain POCO, no base type, `init` setters | **none** | `Domain/IdentityAccess/PasswordHistoryEntry.cs:8-14` |
| `UserAccessReview` | `AggregateRoot` | `ITenantScoped` — `TenantId` non-nullable, public setter | `Domain/IdentityAccess/UserAccessReview.cs:15,19` |
| `SignatureRecord` | ledger POCO (ComplianceLedger) | `TenantId` non-nullable | `Domain/ComplianceLedger/LedgerEntries.cs:27-37` |
| `SecurityEvent` | ledger POCO (ComplianceLedger) | `TenantId` **nullable** — pre-auth events carry null | `Domain/ComplianceLedger/LedgerEntries.cs:43-52` |

### 1.2 `UserAccount` — fields and invariants

Constants: `MaxFailedAttempts = 5`, `LockoutMinutes = 30` (`UserAccount.cs:29-30`).

Properties, all private-set (`UserAccount.cs:42-84`): `TenantId (Guid?)`, `Email`, `DisplayName`, `PasswordHash`, `PasswordChangedAtUtc (DateTimeOffset?)`, `Role (UserRole)`, `IsActive (bool)`, `FailedLoginAttempts (int)`, `LockedUntilUtc (DateTimeOffset?)`, `MfaSecret (string?)`, `MfaEnabled (bool)`, `PinHash (string?)`, `RoleId (Guid?)`, `PreferredLanguage (string?)`, `BranchAccess`, `DepartmentAccess`.

| Behaviour | Rule | Line |
|---|---|---|
| `Create` | email must be non-blank **and contain `@`** (no full RFC validation in the domain) | `:89-92` → `USER-001` |
| `Create` | display name required | `:94-97` → `USER-002` |
| `Create` | `PlatformAdmin` may not have a tenant | `:99-102` → `USER-003` |
| `Create` | non-`PlatformAdmin` must have a tenant | `:104-107` → `USER-004` |
| `Create` | email is trimmed and **lower-cased**; display name trimmed; `IsActive = true` | `:112-116` |
| `ChangeRole` | a tenant user cannot become `PlatformAdmin` | `:127-130` → `USER-005` |
| `ResetPassword` | hash required; **clears `FailedLoginAttempts` and `LockedUntilUtc`**; `PasswordChangedAtUtc = at` where `at` defaults to **`null`** | `:136-147` → `USER-006` |
| `ChangePassword` | hash required; clears lockout; stamps `PasswordChangedAtUtc = at` (non-optional) | `:150-161` → `USER-006` |
| `IsLockedOut(now)` | `LockedUntilUtc is { } until && until > now` — strict `>` | `:163` |
| `AssignRole` | `Guid.Empty` rejected; raises `UserRoleAssigned` | `:166-175` → `USER-010` |
| `SetPreferredLanguage` | blank → `null`; otherwise trimmed + lower-cased | `:178-179` |
| `SetScope` | de-duplicates, drops `Guid.Empty`; **replaces** both lists wholesale; raises `UserScopeChanged` with `IsUnrestricted = both empty` | `:186-197`, `:205-206` |
| `RegisterFailedLogin` | increments; at `>= 5` sets `LockedUntilUtc = now + 30 min`, **zeroes the counter**, raises `UserLockedOut` | `:209-218` |
| `RegisterSuccessfulLogin` | zeroes counter, clears lock | `:220-224` |
| `EnrollMfa` | secret required; sets `MfaEnabled = false` (re-enrolling **disables** an already-enabled MFA) | `:227-236` → `MFA-001` |
| `ConfirmMfa` | requires a stored secret | `:238-246` → `MFA-002` |
| `SetPin` | hash required — **no length, format or digit rule in the domain** | `:248-256` → `PIN-001` |

`Deactivate()` / `Reactivate()` are unguarded one-liners (`:120`, `:122`) — no event, no state check, idempotent.

### 1.3 `RefreshSession` — invariants

| Behaviour | Rule | Line |
|---|---|---|
| `Start` | `id` and `userId` must be non-empty | `:51-54` → `AUTH-000` |
| `Start` | `tokenHash` required | `:56-59` → `AUTH-000` |
| `Start` | `lifetime > TimeSpan.Zero` | `:61-64` → `AUTH-000` |
| `Start` | `familyId == Guid.Empty` is replaced by a fresh UUIDv7 (a session is never family-less) | `:69` |
| `IsLive(now)` | `RevokedAtUtc is null && ExpiresAtUtc > now` | `:42` |
| `WasRotated` | `ReplacedById is not null` | `:45` |
| `Rotate` | a revoked session cannot rotate; sets `RevokedAtUtc` **and** `ReplacedById` | `:77-86` → `AUTH-000` |
| `Revoke` | `RevokedAtUtc ??= now` — **idempotent, first revocation wins** | `:89` |

All four failure modes share the single code `AUTH-000`, which by the `AUTH-` prefix rule maps to **HTTP 401** (`DomainExceptionHandler.cs:54-59`) even though every one of them is a programming error, not an authentication failure.

### 1.4 `UserAccessReview` — invariants

States: `Open`, `Completed` (`UserAccessReview.cs:6`). `Open(reviewRef, openedOn)` performs **no validation at all** (`:30-36`). `Complete(reviewerId, at, accountsReviewed, changesRequired, conclusion)`:

- already `Completed` → `InvalidStateTransitionException("UAR-010")` → **HTTP 409** (`:41-44`; `DomainExceptionHandler.cs:45-50`)
- blank conclusion → `DomainException("UAR-011")` → **HTTP 422** (`:46-49`)
- on success: sets `Status`, `ReviewedBy`, `CompletedAtUtc`, `AccountsReviewed`, `ChangesRequired`, trimmed `Conclusion`; raises `UserAccessReviewCompleted(Id, ReviewRef, ChangesRequired, ReviewedBy, TenantId)` (`:51-57`)

There is **no** `Reopen`, no amendment path and no per-account detail — `AccountsReviewed` is a single integer.

### 1.5 Domain and application error codes — **exhaustive for this module**

Every code an AUTH-scope code path can raise, with its HTTP mapping. Mapping rules: `AUTH-*` → 401; `AUTHZ-*` → 403; `*-404` → 404; `InvalidStateTransitionException` → 409; everything else `DomainException` → 422; FluentValidation → 400 (`DomainExceptionHandler.cs:26-82`).

| Code | HTTP | Message / condition | Thrown at |
|---|---|---|---|
| `AUTH-000` | 401 | refresh session created/rotated with bad arguments (4 distinct conditions) | `RefreshSession.cs:53,58,63,81` |
| `AUTH-001` | 401 | "Invalid credentials." — unknown tenant slug at login; unknown user; inactive user; bad password; **and** every failure branch of change-password | `Login.cs:51,74,86,192,203` |
| `AUTH-002` | 401 | "This tenant is not active." | `Login.cs:62` |
| `AUTH-003` | 401 | "An authenticated user is required." | `MfaAndPin.cs:21,43,75`; `AccessReviewSlice.cs:50` |
| `AUTH-004` | 401 | "Account is temporarily locked. Try again later." | `Login.cs:79` |
| `AUTH-005` | 401 | bad TOTP code (message is the generic "Invalid credentials.") | `Login.cs:113` |
| `AUTH-006` | 401 | "Your session is no longer valid. Please sign in again." — inactive account on refresh, and on every authenticated request | `RefreshSessions.cs:124`; `RequestIdentity.cs:100` |
| `AUTH-007` | 401 | "Your permissions have changed. Please sign in again." — token `role` claim ≠ DB `role` | `RequestIdentity.cs:107` |
| `AUTH-008` | 401 | "The session has been revoked." — **refresh-token reuse detected** | `RefreshSessions.cs:111` |
| `AUTH-009` | 401 | "The session has expired." — unparsable token, unknown session id, hash mismatch, or not live | `RefreshSessions.cs:90,98,116` |
| `AUTH-101` | 401 | "Password has expired and must be changed." | `Login.cs:94-95` |
| `AUTH-102` | 401 | "The new password must differ from the last {HistoryDepth + 1} passwords." | `Login.cs:215-216` |
| `AUTH-401` | 401 | framework challenge (missing/invalid bearer) | `ProblemAuthorizationResultHandler.cs:18,42-44` |
| `AUTHZ-000` | 403 | command declares no policy attribute — fail-closed | `AuthorizationBehavior.cs:52` |
| `AUTHZ-001` | 403 | authenticated actor required | `AuthorizationBehavior.cs:60`; `UserManagement.cs:324` |
| `AUTHZ-002` | 403 | role not permitted to execute the command | `AuthorizationBehavior.cs:83` |
| `AUTHZ-008` | 403 | command requires a permission key the catalogue does not know | `AuthorizationBehavior.cs:68` |
| `AUTHZ-403` | 403 | HTTP-layer privilege refusal (`[RequirePermission]` and framework forbid) | `ProblemAuthorizationResultHandler.cs:16`; `RequirePermissionAttribute.cs:59` |
| `MFA-001` | 422 | "A TOTP secret is required to enroll." | `UserAccount.cs:231` |
| `MFA-002` | 422 | "MFA has not been enrolled." | `UserAccount.cs:242` |
| `MFA-003` | 422 | "The verification code is invalid." | `MfaAndPin.cs:49` |
| `MFA-ENROLL-REQUIRED` | **403** | enrollment-scoped session touched a non-allow-listed path | `RequestIdentity.cs:193` (middleware, not a `DomainException`) |
| `PIN-001` | 422 | "A PIN hash is required." | `UserAccount.cs:252` |
| `USER-001` | 422 | valid email required | `UserAccount.cs:91` |
| `USER-002` | 422 | display name required | `UserAccount.cs:96` |
| `USER-003` | 422 | platform admins cannot belong to a tenant | `UserAccount.cs:101` |
| `USER-004` | 422 | tenant users must belong to a tenant | `UserAccount.cs:106` |
| `USER-005` | 422 | tenant user cannot be made platform admin / "Platform administrator is not a tenant role." | `UserAccount.cs:129`; `UserManagement.cs:23` |
| `USER-006` | 422 | a password hash is required | `UserAccount.cs:140,154` |
| `USER-007` | 422 | "Unknown role '{role}'." | `UserManagement.cs:18` |
| `USER-008` | 422 | "A user with email '{email}' already exists in this tenant." | `UserManagement.cs:59` |
| `USER-010` | 422 | "A role is required." (`Guid.Empty` on `AssignRole`) | `UserAccount.cs:170` |
| `USER-404` | 404 | "User not found." | `MfaAndPin.cs:23,45,77`; `UserManagement.cs:112,326` |
| `UAR-010` | **409** | "The access review is already completed and immutable." | `UserAccessReview.cs:43` |
| `UAR-011` | 422 | "A written conclusion is required…" | `UserAccessReview.cs:48` |
| `UAR-404` | 404 | "Access review not found." | `AccessReviewSlice.cs:52` |
| `SCOPE-003` | 422 | "One or more selected branches do not exist." | `UserManagement.cs:268` |
| `SCOPE-004` | 422 | "One or more selected departments do not exist." | `UserManagement.cs:278` |
| `ROLE-404` | 404 | "Role not found." (raised inside AUTH handlers) | `UserManagement.cs:68,211` |
| `ROLE-008` | 422 | "An inactive role cannot be assigned." | `UserManagement.cs:71,214` |
| `TENANT-000` | 422 | "A tenant context is required." | `UserManagement.cs:54,110,161,188`; `AccessReviewSlice.cs:32,48` |
| `SIG-001` | 422 | "Electronic-signature PIN is not set or is incorrect." | `ComplianceLedgerServices.cs:114` |
| `SIG-002` | 422 | "Account password is incorrect." | `ComplianceLedgerServices.cs:108` |
| `SIG-003` | 422 | "Account is temporarily locked after repeated failed signings." | `ComplianceLedgerServices.cs:101` |
| `SIG-404` | 404 | "Signer not found." | `ComplianceLedgerServices.cs:94` |
| `CHANGE-REASON-REQUIRED` | 400 | DELETE without `X-Change-Reason` (no AUTH DELETE exists today, but the middleware sits in the AUTH pipeline) | `RequestIdentity.cs:154` |
| `CONCURRENCY-409` | 409 | `xmin` conflict on any AUTH write | `DomainExceptionHandler.cs:21,28-33` |

**Reachability caveats for authors:** `MFA-001`, `MFA-002` and `PIN-001` are domain guards with no reachable HTTP path — `EnrollMfaHandler` always supplies a generated secret (`MfaAndPin.cs:25-26`), `ConfirmMfaHandler` checks the secret itself before calling `ConfirmMfa` (`:47`), and `SetPinHandler` always passes a non-empty hash (`:80`). Test them at `UNIT` level; do not author API cases for them.

### 1.6 Domain events and security-event ledger types

**Domain events raised in AUTH scope** (3):

| Event | Payload | Raised at |
|---|---|---|
| `UserLockedOut(UserId, Email, LockedUntilUtc)` | `UserAccount.cs:259` | `UserAccount.cs:216` |
| `UserRoleAssigned(UserId, RoleId)` | `UserAccount.cs:282` | `UserAccount.cs:174` |
| `UserScopeChanged(UserId, BranchIds, DepartmentIds, IsUnrestricted)` | `UserAccount.cs:289-293` | `UserAccount.cs:196` |
| `UserAccessReviewCompleted(ReviewId, ReviewRef, ChangesRequired, ReviewedBy, TenantId)` | `UserAccessReview.cs:61-62` | `UserAccessReview.cs:57` |

Note the asymmetry: **`Deactivate`, `Reactivate`, `ChangeRole`, `ResetPassword`, `ChangePassword`, `EnrollMfa`, `ConfirmMfa` and `SetPin` raise no domain event** (`UserAccount.cs:120,122,132,143-146,157-160,234-235,245,255`). Their auditability rests entirely on the `FieldChangeInterceptor` ledger and, for a subset, the security-event log.

**Security-event types actually written** — 12, exhaustive, grepped across `src/`:

| Type | Written at | Tenant stamped? |
|---|---|---|
| `LOGIN_SUCCESS` | `Login.cs:139` | yes (null for platform admin) |
| `LOGIN_FAILED` | `Login.cs:152` (via `FailAsync`, 5 call sites with `reason` in `Detail`: `unknown-tenant`, `tenant-inactive`, `no-such-user`, `locked-out`, `bad-password`, `bad-mfa`, `password-expired`) | yes once the slug resolved, else null |
| `LOGIN_MFA_REQUIRED` | `Login.cs:103` | yes |
| `LOGIN_MFA_ENROLL_REQUIRED` | `Login.cs:139` | yes |
| `PASSWORD_CHANGED` | `Login.cs:235` | yes |
| `MFA_ENABLED` | `MfaAndPin.cs:54` | yes |
| `REFRESH_INVALID` | `RefreshSessions.cs:97` | **null always** |
| `REFRESH_REUSE_DETECTED` | `RefreshSessions.cs:108-109` | **null always** |
| `LOGOUT` | `RefreshSessions.cs:177` | **null always** |
| `ESIGN_FAILED` | `ComplianceLedgerServices.cs:142` | yes |
| `ESIGN_LOCKED` | `ComplianceLedgerServices.cs:100` | yes |
| `RECORD_EXPORTED` | out of AUTH scope (export service) | — |

`SecurityEvent.Actor` carries the **email** on login/password paths and the **display name** on e-signature paths (`ComplianceLedgerServices.cs:100,142`) — an inconsistency authors must encode in assertions. `SecurityEvent.IpAddress` exists on the entity (`LedgerEntries.cs:49`, typed `IPAddress`/`inet` since `Hardening1_TypesAndNames`) but **`SecurityEventLog.WriteAsync` never sets it** (`ComplianceLedgerServices.cs:68-83`); it is null on every row. Raised as `GAP-AUTH-005`.

### 1.7 Endpoints — 23 logical, all dual-exposed under `/api/v{version}/…`

Verified against `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` (the merge-gated baseline).

**`AuthController`** — `[Route("api/auth")]`, class-level `[EnableRateLimiting(RateLimiting.AuthPolicy)]` (`AuthController.cs:15,18`).

| Method + path | Auth | Permission | Command/query | Line |
|---|---|---|---|---|
| `POST /api/auth/login` | `[AllowAnonymous]` | n/a | `LoginCommand` `[AllowUnauthenticated]` | `:29-38` |
| `GET /api/auth/workspace/{slug}` | `[AllowAnonymous]` | n/a | `GetWorkspaceQuery` | `:47-57` |
| `POST /api/auth/refresh` | `[AllowAnonymous]` + `[EnableRateLimiting(RefreshPolicy)]` | n/a | `RefreshTokenCommand` `[AllowUnauthenticated]` | `:64-72` |
| `POST /api/auth/logout` | `[AllowAnonymous]` | n/a | `LogoutCommand` `[AllowUnauthenticated]` | `:75-82` |
| `POST /api/auth/change-password` | `[AllowAnonymous]` | n/a | `ChangePasswordCommand` `[AllowUnauthenticated]` | `:110-117` |
| `POST /api/auth/mfa/enroll` | `[Authorize]` | none | `EnrollMfaCommand` `[RequireAuthenticatedActor]` | `:120-123` |
| `POST /api/auth/mfa/confirm` | `[Authorize]` | none | `ConfirmMfaCommand` `[RequireAuthenticatedActor]` | `:125-131` |
| `POST /api/auth/signature-pin` | `[Authorize]` | none | `SetPinCommand` `[RequireAuthenticatedActor]` | `:134-140` |
| `GET /api/auth/me/privileges` | `[Authorize]` | none | `GetMyPrivilegesQuery` | `:147-150` |
| `PUT /api/auth/me/language` | `[Authorize]` | none | `SetMyLanguageCommand` `[RequireAuthenticatedActor]` | `:153-159` |

**`UsersController`** — `[Route("api/users")]`, class-level `[Authorize]` (`UsersController.cs:18-19`). **No** rate-limit policy — the global 300/min applies.

| Method + path | Endpoint permission | Command permission | Line |
|---|---|---|---|
| `GET /api/users` | `users.view` | query — not gated by `AuthorizationBehavior` | `:22-25` |
| `POST /api/users` | `users.manage` | `users.manage` | `:27-31` |
| `POST /api/users/{id}/role` | `users.manage` | `users.manage` | `:33-39` |
| `PUT /api/users/{id}/assigned-role` | `users.manage` | `users.manage` | `:42-48` |
| `PUT /api/users/{id}/scope` | `users.manage` | `users.manage` | `:51-57` |
| `PUT /api/users/{id}/language` | `users.manage` | `users.manage` | `:60-66` |
| `POST /api/users/{id}/deactivate` | `users.manage` | `users.manage` | `:68-74` |
| `POST /api/users/{id}/reactivate` | `users.manage` | `users.manage` | `:76-82` |
| `POST /api/users/{id}/reset-password` | `users.manage` | `users.manage` | `:84-90` |

**`UserDirectoryController`** — `[Route("api/users/directory")]`, `[Authorize]`, **no `[RequirePermission]`** (`UserDirectoryController.cs:13-20`). `GET /api/users/directory` returns `(Id, DisplayName, Role)` for every **active** user of the caller's tenant (`UserManagement.cs:190-192`) to **any authenticated tenant user, including `ExternalAuditor`**.

**`AccessReviewsController`** — `[Route("api/access-reviews")]`, `[Authorize]`, class-level `[RequirePermission(PermissionCatalog.AccessReviews, PermissionAction.View)]` (`AccessReviewsController.cs:17-21`).

| Method + path | Endpoint permission | Command policy | Line |
|---|---|---|---|
| `GET /api/access-reviews` | `access-reviews.view` | query | `:23-25` |
| `POST /api/access-reviews` | **`access-reviews.view`** (inherited, no method override) | `[RequireInternalActor]` | `:27-29`; `AccessReviewSlice.cs:13` |
| `POST /api/access-reviews/{id}/complete` | **`access-reviews.view`** (inherited) | `[RequireInternalActor]` | `:31-36`; `AccessReviewSlice.cs:15` |

Two write endpoints on a Part-11 recertification control are gated by a **read** privilege. Raised as `GAP-AUTH-004`.

### 1.8 Permission keys used by this module

Only two of the 31 catalogue modules are referenced from AUTH scope, and both carry the `ConfigurationModule`/`SignedRecordLifecycle` bundles from `PermissionCatalog.cs:120-131`:

| Module key | Bundle | Keys that exist | Keys AUTH actually gates on |
|---|---|---|---|
| `users` (`PermissionCatalog.cs:99`, registered `:168` with `ConfigurationModule`) | View, Manage | `users.view`, `users.manage` | both |
| `access-reviews` (`PermissionCatalog.cs:91`, registered `:156` with `SignedRecordLifecycle`) | View, Create, Edit, Approve, Void, Sign, Export | `access-reviews.view` … `access-reviews.export` | **`access-reviews.view` only** — the other six keys exist in the catalogue and are grantable but gate nothing |

Key format is `{module}.{action}` lower-cased, built by `PermissionCatalog.Key()` (`PermissionCatalog.cs:194-195`). `ManageRoles` (`roles.manage`) is referenced from `AssignUserRoleHandler`'s lockout guard (`UserManagement.cs:219-227`) but belongs to module `RBAC`.

### 1.9 Persistence

| Table | Schema | PK | RLS | Notable columns / constraints |
|---|---|---|---|---|
| `user_account` | `qams` | `PRIMARY KEY (id)` — single column, nullable tenant | **off** | `tenant_id uuid NULL` → `FK … REFERENCES saas.tenant(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED`; `CHECK ck_user_account_role_domain` over the 6 `UserRole` names; `email varchar(320)`, `display_name varchar(150)`, `password_hash varchar(500)`, `role varchar(30)`, `preferred_language varchar(10)`, `mfa_secret text`, `pin_hash text` (**no format CHECK**), `failed_login_attempts int NOT NULL`, `locked_until_utc timestamptz NULL`, `password_changed_at_utc timestamptz NULL`, `role_id uuid NULL`; unique index on `(tenant_id, email)` (`IdentityAndImprovementConfigurations.cs:22`) |
| `user_branch_access` | `qams` | `(tenant_id, user_id, branch_id)` | **on, FORCE**, `tenant_isolation` | `FK (user_id) → user_account(id) ON DELETE CASCADE` |
| `user_department_access` | `qams` | `(tenant_id, user_id, department_id)` | **on, FORCE**, `tenant_isolation` | `FK (user_id) → user_account(id) ON DELETE CASCADE` |
| `refresh_session` | `qams` | `PRIMARY KEY (id)` | **off** — **no `tenant_id` column at all** | `token_hash varchar(64)` with `CHECK ck_refresh_session_token_hash_sha256 (token_hash ~ '^[0-9A-F]{64}$')` — **upper-case hex only**, matching `Convert.ToHexString` (`RefreshSessions.cs:66-67`); indexes `ix_refresh_session_family/user/expires`; **no FK to `user_account`** |
| `password_history` | **`saas`** (not `qams`) | `PRIMARY KEY (id)` | **off** — no `tenant_id`, **no FK to `user_account`** | `password_hash varchar(500)`; index `(user_id, set_at_utc)` |
| `user_access_review` | `qams` | `(tenant_id, id)` — tenant-first composite | **on, FORCE**, `tenant_isolation` (created in-migration, `20260726213412_UserAccessReview.cs:53-67`) | `CHECK ck_user_access_review_status_domain IN ('Open','Completed')`; `conclusion` is `text` since `Hardening1` (bound is the validator's `MaximumLength(4000)`, `AccessReviewSlice.cs:22`); unique `(tenant_id, review_ref)` |
| `security_event` | `audit` | — | **on, FORCE**, `tenant_isolation` (closed in v1.51.2) | `tenant_id` nullable; `ip_address inet` **never populated** |

Concurrency token throughout is `xmin` (no `row_version` column). Refresh sessions are purged by `OutboxProcessor.RunRetentionPurgeAsync`: `DELETE … WHERE expires_at_utc < now - 7 days` (`Persistence/Outbox/OutboxProcessor.cs:264-272`, constant at `:272`), running hourly with the first pass on startup (`:53,59,74-77`).

### 1.10 Cross-cutting mechanics

**JWT** (`Infrastructure/Security/SecurityAdapters.cs:44-115`). HS256; secret must be ≥ 32 chars or startup throws (`:62-67`). Default lifetime **15 minutes**, `Jwt:ExpiryMinutes` (`:36`, `:58`). Claims issued (`:83-95`): `sub`, `email`, `name`, `ClaimTypes.Role`, `scope` (`"full"` or `"mfa_enrollment"`, constants at `:76-78`), and `tenant_id` **only for tenant users**. `notBefore = now`.

**Password hashing** — ASP.NET Core Identity PBKDF2 via `IdentityPasswordHasher` (`SecurityAdapters.cs:13-22`); the same hasher is reused for the e-signature PIN (`MfaAndPin.cs:79-80`).

**TOTP** (`Infrastructure/Security/TotpService.cs`). RFC 6238, HMAC-SHA1, 30-second step, 6 digits, 160-bit Base32 secret. `Verify` accepts **±1 step** (`:34-43`) using `CryptographicOperations.FixedTimeEquals`. `otpauth://` URI issuer `NT.QAMS` (`MfaAndPin.cs:30`).

**Middleware order** (`Program.cs:254-272`): `Observability` → `SecurityHeaders` → `UseAuthentication` → **`UseRateLimiter`** → `TenantResolution` → `ActiveSession` → `MfaEnrollmentGate` → `ChangeReason` → `UseAuthorization` → `MapControllers`. Two consequences authors must encode: (a) a 429 is returned **before** the DB re-check, so a rate-limited request never touches `user_account`; (b) `[RequirePermission]` (an MVC authorization filter) runs **after** `ActiveSessionMiddleware` has populated `IUserPrivileges`, which is why the privilege resolution is per-request and uncached.

**`TenantResolutionMiddleware`** (`RequestIdentity.cs:53-65`) reads the tenant from the `tenant_id` **JWT claim only** — never a header or query string.

**`ActiveSessionMiddleware`** (`RequestIdentity.cs:80-131`) on every authenticated request: reads `sub`, loads `{IsActive, Role}` from `user_account` (`:93-96`); missing or inactive → `401 AUTH-006` (`:100`); token role ≠ DB role → `401 AUTH-007` (`:107`); `PlatformAdmin` → `SetPlatformAdmin()` (`:114-116`); otherwise resolves and sets configured privileges (`:118-121`).

**`MfaEnrollmentGateMiddleware`** (`RequestIdentity.cs:170-200`). If claim `scope == "mfa_enrollment"`, only four path prefixes pass: `/api/auth/mfa/enroll`, `/api/auth/mfa/confirm` and their `/api/v1/` mirrors (`:172-179`). Everything else → `403 MFA-ENROLL-REQUIRED`. **`/api/auth/logout` and `/api/auth/me/privileges` are not on the list** → `GAP-AUTH-007`.

**Rate limiting** (`Security/RateLimiting.cs`). Fixed 1-minute windows, `QueueLimit = 0`, rejection `429` with `Retry-After: 60` (`:51-61`). Defaults from `ConfigGuard` (`:22-26`): global **300**/min per client IP; `auth` **10**/min per client IP (whole `AuthController`); `refresh` **60**/min per client IP; `esignature` **10**/min **per actor** (`sub` claim, falling back to IP, `:101-102`). `RateLimitSettings.Validated()` refuses startup on any non-positive permit (`:16-20`).

**Refresh-token wire format** (`RefreshSessions.cs:36-68`). `"{sessionId:N}.{base64url secret}"`, 32 random bytes; only `SHA256(secret)` as upper-case hex is stored. `TryParse` rejects a missing dot, a leading dot, a trailing dot, and a non-`N`-format GUID (`:48-64`).

**Refresh cookie** (`AuthController.cs:26-27, 92-100`): name `qams_rt`, `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`, `Expires = grant.ExpiresAtUtc`, `IsEssential`. Lifetime `Auth:RefreshTokenDays`, default **14** (`Infrastructure/DependencyInjection.cs:94-95`; `RefreshSessionOptions.Validated()` at `RefreshSessions.cs:14-17`). Logout deletes it with the matching `Path` (`AuthController.cs:80`).

**Password policy** (`Application/Abstractions/PasswordPolicyOptions.cs:8`): `MaxAgeDays = 90`, `HistoryDepth = 5`, both from `ConfigGuard` (`DependencyInjection.cs:76-78`). `MaxAgeDays = 0` disables aging; aging is also skipped when `PasswordChangedAtUtc is null` (`Login.cs:90-92`) — which is the state an **administrative reset** leaves the account in, because `ResetPassword`'s `at` parameter defaults to `null` and `ResetUserPasswordHandler` does not pass one (`UserAccount.cs:136,144`; `UserManagement.cs:147`).

**Password strength** (`PasswordRules.cs`): `MinLength = 12` (`:17`), `MaxLength = 200` (`:20`), all four character classes required — anything not upper/lower/digit counts as "symbol", so a space or a Unicode letter satisfies it (`:63-72`) — and a 62-entry case-insensitive compromised list checked against the **trimmed** input (`:27-39, 76-77`). Applied at `RegisterUserCommand` (`UserManagement.cs:44`), `ResetUserPasswordCommand` (`:102`) and `ChangePasswordCommand` (`Login.cs:170`). It is **not** applied to the platform-admin bootstrap password (`WebApi/Startup/StartupSeeding.cs:68-84`).

**Security options**: `Security:RequireMfaForPrivilegedRoles`, default **false** (`PasswordPolicyOptions.cs:17`; `DependencyInjection.cs:79-80`). Tenant users read `tenant.Settings.RequireMfaForPrivilegedRoles` instead (`Login.cs:66`).

**Anonymous workspace lookup** (`Application/Tenancy/Queries/GetWorkspace.cs:25-47`): blank slug → `null`; `TenantSlug.Create` throwing `DomainException` → caught, `null`; otherwise projects `WorkspaceResponse(t.Name)` filtered on `Slug == slug && Status == Active`. All four misses become an identical `404` `problem+json` titled "Workspace not found." (`AuthController.cs:54-56`). Response contract is a **single property**, `Name` (`Contracts/Tenancy/TenancyContracts.cs:28`).

**Electronic signature** (`Infrastructure/Compliance/ComplianceLedgerServices.cs:86-144`). Order of checks: signer exists (`SIG-404`) → **not locked** (`SIG-003`, writes `ESIGN_LOCKED`) → **password** (`SIG-002`) → **PIN** (`SIG-001`). Each of the last two calls `RecordFailureAsync`, which invokes `UserAccount.RegisterFailedLogin` and writes `ESIGN_FAILED` with `Detail = "{reason}:{subjectRef}"` (`:138-143`) — so failed signings share the **same** 5-attempt / 30-minute counter as login, and five bad PINs lock the account out of **login** too. On success a `SignatureRecord` is appended with `Id, TenantId (Guid.Empty when unresolved), SignerId, SignerDisplay, Meaning, SubjectRef, ContentHash, SignedAtUtc` (`:117-129`).

**The signature service has exactly one call site in the entire application**: `PublishDocumentHandler` (`Application/DocumentControl/Commands/DocumentCommands.cs:121-163`), reached by `POST /api/documents/{id}/publish`, which is the only endpoint carrying `[EnableRateLimiting(ESignaturePolicy)]` (`Controllers/DocumentsController.cs:114-118`). Meaning string is `"Approved and published {code} v{label}"`, subject `"DOC:{id:N}"`, content hash the stored file's SHA-256 (`DocumentCommands.cs:152-157`). Raised as `GAP-AUTH-003`.

**Idle timeout** (URS-007) is **client-side only**: `frontend/src/app/core/auth.service.ts:156-166`, a 30-minute `setTimeout` reset on interaction. There is no server-side idle enforcement. `GAP-AUTH-012`.

### 1.11 States

| State set | Values | Source |
|---|---|---|
| `UserRole` (structural tier, not authorization) | `PlatformAdmin=0, TenantAdmin=1, QualityManager=2, DepartmentHead=3, Analyst=4, ExternalAuditor=5` | `UserAccount.cs:10-18`; DB `CHECK ck_user_account_role_domain` |
| Account activation | `IsActive ∈ {true,false}` | `UserAccount.cs:50` |
| Account lockout (derived) | `Unlocked` / `Locked` via `IsLockedOut(now)` | `UserAccount.cs:163` |
| MFA | `NotEnrolled` (`MfaSecret=null, MfaEnabled=false`) / `Enrolled-Unconfirmed` (`MfaSecret≠null, MfaEnabled=false`) / `Enabled` | `UserAccount.cs:57-58, 227-246` |
| PIN | `Unset` (`PinHash=null`) / `Set` | `UserAccount.cs:61, 248` |
| Password age (derived) | `Current` / `Expired` / `Ageless` (`PasswordChangedAtUtc = null`) | `Login.cs:90-92` |
| Session scope (JWT claim) | `full` / `mfa_enrollment` | `SecurityAdapters.cs:76-78` |
| `RefreshSession` (derived) | `Live` / `Rotated` / `Revoked` / `Expired` / `Purged` | `RefreshSession.cs:42,45,77,89`; `OutboxProcessor.cs:264-272` |
| `UserAccessReviewStatus` | `Open`, `Completed` | `UserAccessReview.cs:6`; DB CHECK |
| `TenantStatus` (consumed at login) | `Active` required; anything else → `AUTH-002` | `Login.cs:60-63` |

---

## 2. Divergences from the commissioning brief

| # | What the brief assumes | What the code does | Proof (file:line) | Gap |
|---|---|---|---|---|
| D-1 | All active accounts require MFA | MFA is per-tenant **opt-in, default off**; even when on it only produces an *enrolment-scoped* session for `PlatformAdmin` and `TenantAdmin` — every other role signs in normally with no MFA | `Login.cs:44,66,120-122`; `PasswordPolicyOptions.cs:17` | `GAP-AUTH-001` |
| D-2 | Electronic signature is "a 4-digit PIN" | Signature requires **password + PIN**, both PBKDF2-verified; the 4-digit rule lives only in the HTTP validator, not the domain or the column | `ComplianceLedgerServices.cs:104-115`; `MfaAndPin.cs:63-68`; `UserAccount.cs:248-256`; DB `pin_hash text` no CHECK | `GAP-AUTH-002` |
| D-3 | Regulated actions across the QMS are electronically signed | `IESignatureService.SignAsync` has **one** call site in the whole solution — document publish | `DocumentCommands.cs:154`; grep of `src/` yields no other caller | `GAP-AUTH-003` |
| D-4 | Privilege codes `USER.CREATE`, `USER.MANAGE`, … | Keys are `{module}.{action}` lower-case: `users.view`, `users.manage`, `access-reviews.view` | `PermissionCatalog.cs:99,168,194-195`; `UsersController.cs:23,28` | `GAP-AUTH-010` |
| D-5 | Fixed roles grant fixed capabilities | `UserRole` is the structural tier; authorization is the tenant's configurable role over the permission catalogue, resolved **per request** | `RequestIdentity.cs:118-121`; `AuthorizationBehavior.cs:77`; `RequirePermissionAttribute.cs:47-52` | — (documented in ground truth §2) |
| D-6 | An administrator can unlock a locked account | **No unlock endpoint exists.** The only ways to clear `locked_until_utc` are `ResetUserPassword` (which clears it as a side effect) or waiting 30 minutes; `deactivate`/`reactivate` do **not** clear it | approved API surface (9 `/api/users` routes, no unlock); `UserAccount.cs:145-146, 120-122` | `GAP-AUTH-013` |
| D-7 | Deactivating a user immediately kills their sessions | `SetUserActiveHandler` flips `IsActive` and nothing else; refresh rows stay `Live` in the DB until the next refresh attempt (`AUTH-006`) or next request (`AUTH-006` from `ActiveSessionMiddleware`) | `UserManagement.cs:131-140`; `RefreshSessions.cs:119-125`; `RequestIdentity.cs:98-102` | `GAP-AUTH-014` |
| D-8 | Access review is a per-account recertification with a per-account decision | `Complete` stores a single `int AccountsReviewed` count and a free-text conclusion; which accounts, which roles, and which were changed are not recorded | `UserAccessReview.cs:38-58`; `AccessReviewSlice.cs:55-58` | `GAP-AUTH-008` |
| D-9 | Access-review actions are administrator-gated | Both write endpoints inherit a **`access-reviews.view`** class filter; the command policy is `[RequireInternalActor]` (any tier except `ExternalAuditor`) | `AccessReviewsController.cs:20,27-36`; `AccessReviewSlice.cs:13,15` | `GAP-AUTH-004` |
| D-10 | Security events record the originating IP | `SecurityEvent.IpAddress` exists but is never written | `LedgerEntries.cs:49`; `ComplianceLedgerServices.cs:68-83` | `GAP-AUTH-005` |
| D-11 | Session idle timeout is enforced by the system | Enforced only by an Angular timer; the API accepts a valid bearer token regardless of idleness | `frontend/src/app/core/auth.service.ts:156-166`; no server counterpart in `RequestIdentity.cs` | `GAP-AUTH-012` |
| D-12 | Credential-guessing is throttled everywhere credentials are checked | `ChangePassword` verifies the current password but **never calls `RegisterFailedLogin`** — no lockout accrues from wrong-current-password attempts; only the 10/min IP budget applies | `Login.cs:200-204` (contrast `Login.cs:82-87`) | `GAP-AUTH-009` |
| D-13 | The user directory is administrator-only | `GET /api/users/directory` has `[Authorize]` and **no** `[RequirePermission]`; every authenticated tenant user — including `ExternalAuditor` — gets the full active-user roster with roles | `UserDirectoryController.cs:13-20`; `UserManagement.cs:189-193` | `GAP-AUTH-015` |
| D-14 | Platform administrators are managed through the API | `TenantRole.Parse` rejects `PlatformAdmin` on every user-management path; platform admins exist only via `PlatformAdmin:Email/Password` startup seeding | `UserManagement.cs:21-24`; `WebApi/Startup/StartupSeeding.cs:68-84` | `GAP-AUTH-016` |

---

## 3. State-transition matrices

### 3.1 Account state machine

Composite state = `(Activation, Lockout)`. MFA, PIN and password-age are orthogonal sub-states, tabulated separately.

**States:** `S1 Active-Unlocked` · `S2 Active-Locked` (`IsActive=true`, `LockedUntilUtc > now`) · `S3 Inactive-Unlocked` · `S4 Inactive-Locked`.

| From \ Event | 5th consecutive failed factor `RegisterFailedLogin` | successful login `RegisterSuccessfulLogin` | 30 min elapse (time) | `Deactivate()` | `Reactivate()` | `ResetPassword()` | `ChangePassword()` |
|---|---|---|---|---|---|---|---|
| **S1** | → **S2** (`locked_until = now+30m`, counter→0, `UserLockedOut` raised) `UserAccount.cs:212-217` | → S1 (counter→0, lock→null) `:220-224` | — | → **S3** `:120` | → S1 (idempotent) | → S1 (counter→0, lock→null, `password_changed_at→null`) `:143-146` | → S1 (counter→0, lock→null, `password_changed_at=at`) `:157-160` |
| **S2** | **unreachable via login** — `Login.cs:77-80` returns `AUTH-004` before password verification, so no increment and **no lock extension**; reachable via e-signature only through `SIG-003`, which also returns first (`ComplianceLedgerServices.cs:98-102`) | **unreachable** — login rejects at `:77` | → **S1** (implicitly: `IsLockedOut` becomes false; the row is *not* rewritten) `UserAccount.cs:163` | → **S4** | → S2 | → **S1** (explicit unlock side effect) `:145-146` | → **S1** — but the handler's guard `Login.cs:200` rejects a locked account with `AUTH-001` first, so unreachable via HTTP |
| **S3** | — (login rejects at `Login.cs:72-75` with `AUTH-001` before any counter touch) | — | → S3 | → S3 | → **S1** `:122` | → S3 (allowed by the domain; `ResetUserPasswordHandler` does not check `IsActive`) | — (`Login.cs:200` rejects inactive) |
| **S4** | — | — | → S3 | → S4 | → **S2** — **reactivation does not clear the lock** | → **S3** | — |

**Illegal / absent transitions to assert:** there is no state guard on `Deactivate`/`Reactivate` (both are unconditional assignments, `UserAccount.cs:120-122`) — deactivating an already-inactive account succeeds silently and returns `204`. There is no "unlock" transition (`GAP-AUTH-013`). `S4 → S2` on reactivate is the trap case: an account deactivated while locked comes back **still locked**.

**MFA sub-machine** (orthogonal):

| From \ Event | `EnrollMfa(secret)` | `ConfirmMfa()` |
|---|---|---|
| `NotEnrolled` | → `Enrolled-Unconfirmed` (`MfaSecret` set, `MfaEnabled=false`) `:227-236` | `MFA-002` (422) `:242` — but unreachable via HTTP, `MfaAndPin.cs:47` returns `MFA-003` first |
| `Enrolled-Unconfirmed` | → `Enrolled-Unconfirmed` (new secret, old one discarded) | valid code → `Enabled` `:245`; invalid → `MFA-003` (422) `MfaAndPin.cs:49` |
| `Enabled` | → **`Enrolled-Unconfirmed`** — **re-enrolling silently disables MFA** (`MfaEnabled = false`, `:235`) with no confirmation and no security event | valid code → `Enabled` (no-op); invalid → `MFA-003` |

The `Enabled → Enrolled-Unconfirmed` edge is the notable one: `POST /api/auth/mfa/enroll` on an MFA-enabled account is a self-service MFA **downgrade** that is not logged (`MFA_ENABLED` is only written on confirm, `MfaAndPin.cs:54`).

**PIN sub-machine:** `Unset → Set` and `Set → Set` via `SetPin` (`UserAccount.cs:248-256`). **No transition back to `Unset`** — a PIN cannot be cleared by any code path.

### 3.2 Refresh-session state machine

**States:** `Live` (`RevokedAtUtc=null ∧ ExpiresAtUtc>now`) · `Rotated` (`RevokedAtUtc≠null ∧ ReplacedById≠null`) · `Revoked` (`RevokedAtUtc≠null ∧ ReplacedById=null`) · `Expired` (`RevokedAtUtc=null ∧ ExpiresAtUtc≤now`) · `Purged` (row deleted).

| From \ Event | `POST /refresh` with this token | `POST /refresh` with a **sibling** token of the family | `POST /logout` | account deactivated, then `/refresh` | time passes `ExpiresAtUtc` | purge sweep |
|---|---|---|---|---|---|---|
| **Live** | → **Rotated**; successor `Live` in the same `FamilyId`; new cookie + new access token; `200` (`RefreshSessions.cs:129-141`) | unaffected | → **Revoked** (whole family) + `LOGOUT` event; `204` (`:173-178`) | → **Revoked** (this session only) + `401 AUTH-006` (`:122-124`) | → **Expired** | no (cutoff not met) |
| **Rotated** | **reuse detected** → every still-live member of the family → `Revoked`; `REFRESH_REUSE_DETECTED` written; `401 AUTH-008` (`:101-112`) | — | no-op path: `LogoutHandler` finds the row and revokes the family; any already-revoked row is untouched (`Revoke` is `??=`, `RefreshSession.cs:89`) | reuse branch fires first (`:101`) — `AUTH-008`, not `AUTH-006` | already revoked | after `ExpiresAtUtc + 7 days` → **Purged** |
| **Revoked** | same reuse branch → family re-revoked (no-op) + `REFRESH_REUSE_DETECTED` + `401 AUTH-008` — **note a plain logout-revoked token also triggers a reuse alert** | — | no-op | reuse branch fires first | — | after `ExpiresAtUtc + 7 days` → **Purged** |
| **Expired** | → `401 AUTH-009` (`:114-117`); the row is **not** revoked, not touched | — | `LogoutHandler` revokes it (no expiry check, `:165-178`) | `401 AUTH-009` (the live check precedes the user load) | — | after `ExpiresAtUtc + 7 days` → **Purged** |
| **Purged** | unknown session id → `REFRESH_INVALID` event + `401 AUTH-009` (`:95-99`) | — | silent no-op (`:167-171`) | `401 AUTH-009` | — | — |

**Ordering facts worth a case each:** the reuse check (`:101`) precedes the liveness check (`:114`) which precedes the user-active check (`:119`). So a **revoked-and-expired** token yields `AUTH-008`, not `AUTH-009`; and a rotated token belonging to a **deactivated** user yields `AUTH-008`, not `AUTH-006`.

Malformed-token handling never reaches the database: `TryParse` returns null for an empty string, a token with no `.`, `".secret"`, `"sessionid."`, or a non-`N`-format GUID → `401 AUTH-009` with **no** `REFRESH_INVALID` event (`:89-90` throws before the log line at `:97`).

### 3.3 `UserAccessReview` state machine

| From \ Event | `Open()` | `Complete(...)` valid | `Complete(...)` blank conclusion | `Complete(...)` again |
|---|---|---|---|---|
| *(none)* | → **Open** (`review_ref` from `IReferenceNumberGenerator.NextAsync(tenantId,"UAR")`, `opened_on = today UTC`) `AccessReviewSlice.cs:33-34` | — | — | — |
| **Open** | — | → **Completed**; `reviewed_by`, `completed_at_utc`, `accounts_reviewed` = count of `IsActive` users in the tenant at that instant, `changes_required`, trimmed `conclusion`; raises `UserAccessReviewCompleted` `UserAccessReview.cs:51-57`; `AccessReviewSlice.cs:55-58` | `UAR-011` → **422**, state unchanged `:46-49` | n/a |
| **Completed** | — | `UAR-010` → **409** `:41-44` | `UAR-010` → **409** (the status check precedes the conclusion check) | `UAR-010` → **409** |

No reopen, no void, no amend. Note `CompleteAccessReviewHandler` loads the review with `FirstOrDefaultAsync(r => r.Id == c.ReviewId)` **without** a tenant predicate (`AccessReviewSlice.cs:51`) — isolation there rests entirely on the EF global filter plus FORCE RLS on `qams.user_access_review`, which is exactly what `TC-AUTH-RLS-*` should prove.

---

## 4. Decision tables

### 4.1 Authentication × MFA-required × lockout — `POST /api/auth/login`

Conditions, evaluated strictly in this order by `LoginHandler.Handle` (`Login.cs:39-147`):

`C1` slug supplied? · `C2` slug resolves? · `C3` tenant `Active`? · `C4` user found for `(tenantId, email)`? · `C5` `IsActive`? · `C6` `IsLockedOut(now)`? · `C7` password verifies? · `C8` password within `MaxAgeDays`? · `C9` `MfaEnabled`? · `C10` MFA code supplied? · `C11` TOTP verifies? · `C12` policy requires MFA **and** role ∈ {PlatformAdmin, TenantAdmin} **and** `!MfaEnabled`?

| R | C1 | C2 | C3 | C4 | C5 | C6 | C7 | C8 | C9 | C10 | C11 | C12 | HTTP | Code | Security event (`Detail`) | Side effects |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| R1 | Y | **N** | – | – | – | – | – | – | – | – | – | – | 401 | `AUTH-001` | `LOGIN_FAILED` (`unknown-tenant`), tenant **null** | none |
| R2 | Y | Y | **N** | – | – | – | – | – | – | – | – | – | 401 | `AUTH-002` | `LOGIN_FAILED` (`tenant-inactive`), tenant stamped | tenant scope set first (`:58`) |
| R3 | Y/N | Y | Y | **N** | – | – | – | – | – | – | – | – | 401 | `AUTH-001` | `LOGIN_FAILED` (`no-such-user`) | none — **no counter for a non-existent user** |
| R4 | Y/N | Y | Y | Y | **N** | – | – | – | – | – | – | – | 401 | `AUTH-001` | `LOGIN_FAILED` (`no-such-user`) — same branch as R3, indistinguishable | none |
| R5 | Y/N | Y | Y | Y | Y | **Y** | – | – | – | – | – | – | 401 | `AUTH-004` | `LOGIN_FAILED` (`locked-out`) | **no increment, no lock extension** |
| R6 | Y/N | Y | Y | Y | Y | N | **N** | – | – | – | – | – | 401 | `AUTH-001` | `LOGIN_FAILED` (`bad-password`) | `RegisterFailedLogin` + `SaveChanges` (`:84-85`); at the 5th → lock + `UserLockedOut` |
| R7 | Y/N | Y | Y | Y | Y | N | Y | **N** | – | – | – | – | 401 | `AUTH-101` | `LOGIN_FAILED` (`password-expired`) | **none — counter untouched, session not started** |
| R8 | Y/N | Y | Y | Y | Y | N | Y | Y | **Y** | **N** | – | – | **200** | — | `LOGIN_MFA_REQUIRED` | body `AuthResponse` with **empty `AccessToken`**, `ExpiresAtUtc = default`, `MfaRequired: true`; **no cookie, no refresh session** (`:104-106`) |
| R9 | Y/N | Y | Y | Y | Y | N | Y | Y | Y | Y | **N** | – | 401 | `AUTH-005` | `LOGIN_FAILED` (`bad-mfa`) | `RegisterFailedLogin` + `SaveChanges` (`:111-112`) |
| R10 | Y/N | Y | Y | Y | Y | N | Y | Y | Y | Y | Y | N | **200** | — | `LOGIN_SUCCESS` | counter reset; refresh family started; `qams_rt` cookie set; `scope=full` JWT |
| R11 | Y/N | Y | Y | Y | Y | N | Y | Y | **N** | – | – | **Y** | **200** | — | `LOGIN_MFA_ENROLL_REQUIRED` | counter reset; **no refresh session, no cookie** (`:128-136`); JWT `scope=mfa_enrollment`; `MfaEnrollmentRequired: true` |
| R12 | Y/N | Y | Y | Y | Y | N | Y | Y | N | – | – | N | **200** | — | `LOGIN_SUCCESS` | full session, MFA never involved |

`C8` is skipped entirely when `MaxAgeDays <= 0` **or** `PasswordChangedAtUtc is null` (`:90-92`) — so R7 is unreachable for an account whose password was set by administrative reset or by `UserAccount.Create` (both leave the stamp null).

`C1 = N` (no slug) means `tenantId = null`, which selects **platform-admin accounts only** (`u.TenantId == tenantId`, `:70`) and uses the global `SecurityOptions` MFA flag (`:44`).

### 4.2 Silent refresh — `POST /api/auth/refresh`

`D1` cookie present and parsable? · `D2` session row found by id? · `D3` stored hash == presented hash (ordinal)? · `D4` `RevokedAtUtc is null`? · `D5` `ExpiresAtUtc > now`? · `D6` user exists and `IsActive`?

| R | D1 | D2 | D3 | D4 | D5 | D6 | HTTP | Code | Event | Side effects |
|---|---|---|---|---|---|---|---|---|---|---|
| R1 | **N** | – | – | – | – | – | 401 | `AUTH-009` | **none** | nothing persisted (`:89-90`) |
| R2 | Y | **N** | – | – | – | – | 401 | `AUTH-009` | `REFRESH_INVALID` (`Detail` = session id, tenant null) | — (`:95-99`) |
| R3 | Y | Y | **N** | – | – | – | 401 | `AUTH-009` | `REFRESH_INVALID` | — (same branch) |
| R4 | Y | Y | Y | **N** | – | – | 401 | **`AUTH-008`** | `REFRESH_REUSE_DETECTED` (`Detail` = `family={id:N}`) | **every live sibling revoked** + `SaveChanges` (`:104-110`) |
| R5 | Y | Y | Y | Y | **N** | – | 401 | `AUTH-009` | **none** | row untouched (`:114-117`) |
| R6 | Y | Y | Y | Y | Y | **N** | 401 | `AUTH-006` | **none** | this session revoked + `SaveChanges` (`:122-123`) |
| R7 | Y | Y | Y | Y | Y | Y | **200** | — | **none** | presented → `Rotated`; successor `Live`; new `qams_rt`; new access token with **claims re-read from the current DB row** (`:136`) |

R7's last clause is the testable ADR-0009 guarantee: a role change propagates within one refresh cycle without re-login.

### 4.3 Electronic signature — password + PIN

Applies to `ESignatureService.SignAsync`, reachable only through `POST /api/documents/{id}/publish`. Evaluated strictly in this order (`ComplianceLedgerServices.cs:90-130`), **after** `PublishDocumentHandler` has already validated the version state and SoD (`DocumentCommands.cs:132-147`).

`E1` signer row exists? · `E2` `IsLockedOut(now)`? · `E3` password verifies? · `E4` `PinHash` set **and** PIN verifies? · `E5` session/permission valid (`[Authorize]` + `documents.sign` + `scope≠mfa_enrollment` + `AUTHZ` behaviour) · `E6` within the 10/min per-actor e-signature budget

| R | E6 | E5 | E1 | E2 | E3 | E4 | HTTP | Code | Event | Account effect |
|---|---|---|---|---|---|---|---|---|---|---|
| R1 | **N** | – | – | – | – | – | **429** | — (`Retry-After: 60`) | none | none — request never reaches the handler |
| R2 | Y | **anonymous** | – | – | – | – | 401 | `AUTH-401` | none | none |
| R3 | Y | **no `documents.sign`** | – | – | – | – | 403 | `AUTHZ-403` | none | none |
| R4 | Y | **`scope=mfa_enrollment`** | – | – | – | – | 403 | `MFA-ENROLL-REQUIRED` | none | none (`RequestIdentity.cs:191-193`) |
| R5 | Y | Y | **N** | – | – | – | 404 | `SIG-404` | none | none |
| R6 | Y | Y | Y | **Y** | – | – | 422 | `SIG-003` | `ESIGN_LOCKED` (`Detail` = `subjectRef`) | **none — no increment while already locked** |
| R7 | Y | Y | Y | N | **N** | – | 422 | `SIG-002` | `ESIGN_FAILED` (`bad-password:{subjectRef}`) | `RegisterFailedLogin` — **shares the login counter**; 5th → 30-min lock that also blocks login |
| R8 | Y | Y | Y | N | Y | **N** (PIN unset) | 422 | `SIG-001` | `ESIGN_FAILED` (`bad-pin:{subjectRef}`) | `RegisterFailedLogin` |
| R9 | Y | Y | Y | N | Y | **N** (PIN wrong) | 422 | `SIG-001` | `ESIGN_FAILED` (`bad-pin:{subjectRef}`) | `RegisterFailedLogin` |
| R10 | Y | Y | Y | N | Y | Y | **204** | — | none — **a successful signing writes no security event** | counter untouched (success does **not** reset it, unlike login) |

Two facts authors must not get wrong: (a) `SIG-001` does not distinguish "PIN never set" from "PIN wrong" — deliberate, single branch at `:112-115`; (b) a **successful** signature does not reset `FailedLoginAttempts`, so four bad PINs followed by a good one leaves the counter at 4 and the next bad login attempt locks the account.

### 4.4 Endpoint reachability by session scope

`F1` authenticated? · `F2` JWT `scope` claim · `F3` account still `IsActive` in DB · `F4` token role == DB role

| R | F1 | F2 | F3 | F4 | Target | Outcome |
|---|---|---|---|---|---|---|
| R1 | N | – | – | – | any `[Authorize]` route | `401 AUTH-401` (`ProblemAuthorizationResultHandler.cs:42-44`) |
| R2 | Y | any | **N** | – | any authenticated route | `401 AUTH-006` (`RequestIdentity.cs:100`) |
| R3 | Y | any | Y | **N** | any authenticated route | `401 AUTH-007` (`RequestIdentity.cs:107`) |
| R4 | Y | `mfa_enrollment` | Y | Y | `/api/auth/mfa/enroll`, `/api/auth/mfa/confirm` (+ `/api/v1/` mirrors) | **allowed** |
| R5 | Y | `mfa_enrollment` | Y | Y | `/api/auth/logout` | **`403 MFA-ENROLL-REQUIRED`** — the user cannot end the session while presenting the token (`GAP-AUTH-007`) |
| R6 | Y | `mfa_enrollment` | Y | Y | `/api/auth/me/privileges`, `/api/users`, anything else | `403 MFA-ENROLL-REQUIRED` |
| R7 | Y | `full` | Y | Y | route with `[RequirePermission(m,a)]`, privilege **not** granted | `403 AUTHZ-403` (`RequirePermissionAttribute.cs:54-59`) |
| R8 | Y | `full` | Y | Y | route with `[RequirePermission]`, privilege granted, command with `[RequirePermissionPolicy]` for a **different** key | `403 AUTHZ-002` (`AuthorizationBehavior.cs:83`) |
| R9 | Y | `full` | Y | Y | `ExternalAuditor` on a `[RequireInternalActor]` command (`OpenAccessReviewCommand`) | `403 AUTHZ-002` — **but only if the auditor's role also grants `access-reviews.view`**, otherwise `403 AUTHZ-403` at the HTTP filter first |

R5 is reachable in practice only if the SPA attaches the bearer token to logout; sending logout **without** the `Authorization` header succeeds, because the middleware finds no `scope` claim. Authors should cover both.

---

## 6. UAT scenarios (Gherkin)

Business-readable, one per scenario; each consumes one `TC-AUTH-UAT-*` id in batch F.

```gherkin
Feature: Signing in to a laboratory workspace

  Scenario: A laboratory is named on its own sign-in page before anyone signs in
    Given the laboratory "Demo Laboratory" is registered at the address /t/demo-lab and is active
    When a visitor opens that address
    Then the sign-in page greets them by the laboratory's registered name
    And the page discloses nothing else about the laboratory

  Scenario: An address that is not a live laboratory is indistinguishable from one that never existed
    Given "demo-lab" is an active laboratory, "closed-lab" is a suspended laboratory,
          and "no-such-lab" has never existed
    When a visitor opens /t/closed-lab, then /t/no-such-lab, then /t/NOT A SLUG
    Then all three answer identically, with no indication of which case applies

  Scenario: An analyst signs in with the correct password
    Given the analyst has an active account in "demo-lab" and multi-factor authentication is not enabled
    When they sign in with their email address and correct password
    Then they reach the dashboard
    And a successful-sign-in entry appears in the laboratory's security log
    And the session survives a browser refresh without them signing in again

  Scenario: Five wrong passwords lock the account for thirty minutes
    Given the analyst's account is active and not locked
    When they enter a wrong password five times in a row
    Then the account is locked and the fifth attempt is refused
    And a sixth attempt with the CORRECT password is still refused while the lock stands
    And the lock does not extend when they keep trying
    And each attempt is recorded in the security log

  Scenario: A quality manager who has been deactivated loses access on their very next click
    Given the quality manager is signed in and working
    When an administrator deactivates their account
    Then their next action in the application is refused and they are returned to sign-in
    And they cannot regain a session by waiting for the page to refresh itself

  Scenario: A tenant administrator must set up multi-factor authentication before doing anything else
    Given the laboratory has switched on "require MFA for privileged roles"
    And the tenant administrator has never set up an authenticator app
    When they sign in with the correct password
    Then they are taken to the multi-factor setup screen
    And every other part of the application is closed to them until setup is confirmed

  Scenario: A password that has been used before is rejected
    Given the laboratory's policy remembers the last five passwords
    And the analyst previously used the password they are now proposing
    When they try to change their password to it
    Then the change is refused and the reason names the reuse rule
    And their existing password continues to work

  Scenario: An expired password can be changed without first signing in
    Given the analyst's password is older than the maximum age
    When they attempt to sign in
    Then they are told the password has expired and must be changed
    And they can change it by supplying the expired password and a new compliant one
    And they can then sign in with the new password

  Scenario: Publishing a controlled document requires both the password and the signature PIN
    Given a document version has been approved by someone other than its author
    And the approver has set a four-digit signature PIN
    When the approver publishes it with the correct password and the correct PIN
    Then the document is published
    And a permanent signature record links the signer, the meaning "Approved and published",
        the document, and a hash of the exact file that was signed

  Scenario: Guessing the signature PIN locks the signer out of the whole system
    Given the approver has set a signature PIN
    When they attempt to publish five times with a wrong PIN
    Then each attempt is refused and logged as a failed signing
    And the account is then locked, so they cannot even sign in until the lockout expires

  Scenario: A stolen session cookie is detected and kills every session in its chain
    Given a signed-in analyst's browser has refreshed its session at least once
    When someone replays the analyst's PREVIOUS session cookie
    Then that replay is refused
    And the analyst's own current session is also terminated
    And the security log records a session-reuse detection for the whole chain

  Scenario: The laboratory records a periodic recertification of who has access
    Given a tenant administrator opens a user-access review
    When they examine the account roster and complete the review with a written conclusion
    Then the review is closed with the number of active accounts recertified at that moment
    And the review can never afterwards be edited or reopened
    And an attempt to complete it a second time is refused
```

---

## 7. Exploratory charters

Time-boxed sessions; findings feed new cases or new gaps. Each consumes one `TC-AUTH-EXPL-*` id.

**EXPL-1 — Credential-endpoint oracle leakage.** *Explore* the five `LOGIN_FAILED` branches of `POST /api/auth/login` *with* differential response inspection (status, body bytes, header set, and wall-clock timing over ≥50 samples per branch) *to discover* whether unknown-tenant, unknown-user, inactive-user, wrong-password, locked-out and expired-password are distinguishable to an unauthenticated attacker. The bodies differ **by design** (`AUTH-001/002/004/101` all reach the client, `Login.cs:51,62,74,79,95`) — the charter is to quantify what that discloses, especially the fact that `AUTH-004` proves an account exists and `AUTH-101` proves it exists *and* the password was correct. Run this **last**: it will exhaust the 10/min auth partition. *Time-box 90 min.*

**EXPL-2 — Refresh-family topology under concurrency.** *Explore* the rotation chain *with* parallel refreshes from two tabs sharing one cookie, an interrupted rotation (kill the connection between `SaveChanges` and the cookie write), and a replay of a token whose successor has itself rotated *to discover* whether the family can fork, whether a legitimate double-tab refresh trips `REFRESH_REUSE_DETECTED`, and whether any sequence leaves two `Live` rows in one family. Anchor on `RefreshSessions.cs:101-134` and the absence of any uniqueness constraint on `(family_id) WHERE revoked_at_utc IS NULL`. *Time-box 120 min.*

**EXPL-3 — Lockout counter as a shared resource between login and signing.** *Explore* the interleaving of `RegisterFailedLogin` calls from `LoginHandler` (`Login.cs:84,111`) and `ESignatureService.RecordFailureAsync` (`ComplianceLedgerServices.cs:140`) *with* mixed sequences (3 bad PINs + 2 bad passwords; 4 bad PINs + 1 good signature + 1 bad password) *to discover* whether a user can be locked out of sign-in by an in-session signing mistake, whether a successful signing resets the counter (it does not), and whether the 10/min per-actor e-signature budget or the 5-attempt counter binds first. *Time-box 75 min.*

**EXPL-4 — The MFA-enrollment-scoped session as a containment boundary.** *Explore* every route reachable with a `scope=mfa_enrollment` bearer token *with* a systematic sweep of the 23 AUTH routes plus a sample of other modules, both `/api/…` and `/api/v1/…`, including path-casing and trailing-slash variants against the `StartsWith` allow-list (`RequestIdentity.cs:187`) *to discover* prefix-matching bypasses (e.g. `/api/auth/mfa/enrollment-anything`) and to confirm the logout dead-end of `GAP-AUTH-007`. *Time-box 90 min.*

**EXPL-5 — Tenant boundary on the non-RLS credential tables.** *Explore* `qams.user_account`, `qams.refresh_session` and `saas.password_history` *with* a two-tenant fixture and direct handler/`psql` probing *to discover* whether any code path reads them without an explicit tenant predicate. Known predicated paths: `Login.cs:70`, `UserManagement.cs:57,111,163,190`. Known **un**predicated paths to scrutinise: `MfaAndPin.cs:22,44,76` (`u.Id == userId` only), `UserManagement.cs:325`, `RefreshSessions.cs:119`, `ComplianceLedgerServices.cs:93`. Each is defensible (the id comes from a signed token), but the charter is to prove no id-only lookup is reachable with an attacker-chosen id. *Time-box 120 min.*

**EXPL-6 — Password-policy edge semantics.** *Explore* `PasswordRules.StrongPassword` (`PasswordRules.cs:45-77`) *with* Unicode input (combining marks, RTL marks, emoji, non-Latin scripts), whitespace-only "symbols", 200/201-character inputs, leading/trailing whitespace against the trimmed blocklist check (`:77`) versus the untrimmed length check, and NFC/NFD normalisation of the same visual password *to discover* whether a password can pass creation and then fail verification, and whether `"  password  "` is correctly rejected while `"pass word"` (space as the "symbol") is accepted. *Time-box 75 min.*

**EXPL-7 — Administrative recovery paths for a stuck account.** *Explore* the recovery matrix for an account that is simultaneously locked, deactivated and MFA-enabled with a lost authenticator *with* only the nine `/api/users` endpoints available *to discover* the minimum sequence an administrator must execute, and to confirm the `S4 → S2` trap of §3.1 (reactivation preserving the lock) and the absence of any MFA-reset path — nothing in the API can clear `mfa_secret`/`mfa_enabled` or `pin_hash` for another user. *Time-box 60 min.*

**EXPL-8 — Access-review evidentiary sufficiency.** *Explore* a completed `UserAccessReview` as an ISO 17025 / Annex 11 §12 auditor would *with* a scripted scenario in which two accounts are deactivated and one role reassigned mid-review *to discover* what the completed record actually proves. Anchor on the single `accounts_reviewed` integer (`UserAccessReview.cs:26,54`) sampled at completion time and the absence of any per-account line item. *Time-box 60 min.*

---

## 8. Gap Register (this module)

Sixteen gaps. Severity scale: **Critical** (regulated control absent or defeated) · **Major** (control present but incomplete or mis-gated) · **Moderate** (traceability/evidence deficit) · **Minor** (documentation or naming).

---

**GAP-AUTH-001 — MFA is optional, role-limited, and off by default**

- **Source reference:** commissioning brief, identity section ("all active accounts require MFA"); URS-004 (`docs/validation/01-User-Requirements-Specification.md:27`, which says *"support per-tenant optional MFA … and be able to require MFA for privileged roles"*).
- **Description:** the brief and the URS disagree, and the code implements the URS. `Security:RequireMfaForPrivilegedRoles` defaults `false` (`Application/Abstractions/PasswordPolicyOptions.cs:17`; `Infrastructure/DependencyInjection.cs:79-80`); tenant users use `tenant.Settings.RequireMfaForPrivilegedRoles` (`Login.cs:66`); and even when the flag is on, the enrolment gate applies **only** to `UserRole.PlatformAdmin` and `UserRole.TenantAdmin` (`Login.cs:120-122`). A `QualityManager` who signs regulated records is never required to enrol.
- **Impact:** an inspector reading the brief expects MFA on every account. As built, a laboratory can run with zero MFA, and no configuration makes MFA mandatory for a `QualityManager` or `Analyst`.
- **Testing limitation:** cases asserting "MFA required for all accounts" cannot pass and must not be authored. Cases against the privileged-role gate require `Security:RequireMfaForPrivilegedRoles=true` **or** a tenant with `require_mfa_privileged=true`, which the dev seed does not provide.
- **Recommended clarification:** confirm with the quality owner whether the brief's blanket-MFA statement is a requirement or an aspiration, and whether the privileged set should be role-tier-based or privilege-based.
- **Suggested acceptance criteria:** *(a)* the URS states the implemented policy verbatim, including which roles the gate covers; *(b)* if blanket MFA is required, `Login.cs:120-122` drops the role predicate and a test proves an `Analyst` receives `scope=mfa_enrollment`; *(c)* the tenant-settings screen states which roles the switch affects.
- **Severity:** **Major**
- **Responsible role:** Quality Manager (requirement) + Lead Developer (implementation)

---

**GAP-AUTH-002 — The four-digit PIN rule exists only at the HTTP boundary**

- **Source reference:** `MfaAndPin.cs:63-68` (rule present) vs `UserAccount.cs:248-256` (domain: non-empty only) vs `qams.user_account.pin_hash text` with no CHECK (measured).
- **Description:** the "exactly 4 digits" constraint is a FluentValidation rule on `SetPinCommand`. It is enforced for `POST /api/auth/signature-pin` and nowhere else. `UserAccount.SetPin` accepts any non-blank hash; the column accepts anything. Any future call path — a seeder, an import, an admin tool, a second command — can set a PIN of any shape, and the signature service will happily verify it (`ComplianceLedgerServices.cs:112-115`).
- **Impact:** CLAUDE.md §2.6 makes "domain protects itself" a standing rule; a Part-11 §11.200(a)(1) identification component is guarded outside the domain. The stated control is one refactor away from silently disappearing. *Note this also corrects ground truth §2 line 57 — see §0.3.*
- **Testing limitation:** none for the HTTP path (author `TC-AUTH-BVA-*` as `[IV]`). The domain-level absence can only be proven by a unit test asserting that `SetPin("x")` succeeds, which documents a defect rather than a requirement — label it `[ID]`.
- **Recommended clarification:** is a 4-digit PIN the intended strength for a Part-11 second factor, or was it a placeholder? Four digits is 10,000 combinations against a 10/min per-actor budget and a 5-attempt lockout — defensible, but it should be a recorded decision.
- **Suggested acceptance criteria:** *(a)* the PIN format rule moves into `UserAccount.SetPin` or a `SignaturePin` value object with its own code (e.g. `PIN-002`); *(b)* a `CHECK` or a documented rationale for its absence; *(c)* an ADR records the chosen PIN entropy against the lockout and rate-limit compensating controls.
- **Severity:** **Major**
- **Responsible role:** Lead Developer + Quality Manager

---

**GAP-AUTH-003 — Electronic signature is wired to exactly one action**

- **Source reference:** `IESignatureService.SignAsync` call sites across `src/`: one — `Application/DocumentControl/Commands/DocumentCommands.cs:154`.
- **Description:** URS-020/021/023/024 are satisfied for document publication only. No other regulated transition in the system — NC closure, CAPA verification, QC target change, analytical study sign-off, access-review completion, supplier approval — mints a `SignatureRecord`. The 14 analytical sign-offs enforce SoD (`SOD-AQ-001`) but not e-signature. The permission catalogue defines `PermissionAction.Sign` for 20+ modules (`PermissionCatalog.cs:26-27, 117-124`), so the *privilege* to sign is grantable on modules where signing is not implemented.
- **Impact:** a Part-11 assessment scoped to "all regulated records" would find e-signature coverage at one action. The grantable-but-inert `*.sign` privileges are worse than absent: a privilege screen shows an administrator granting a capability that does nothing.
- **Testing limitation:** the entire AUTH e-signature decision table (§4.3) can only be exercised through `POST /api/documents/{id}/publish`, which drags document-module preconditions (uploaded file, approved version, non-author approver) into every AUTH signing case. There is no isolated signing endpoint to test against.
- **Recommended clarification:** produce the authoritative list of transitions that require a Part-11 signature in this product, and reconcile it against the `Sign` action assignments in `PermissionCatalog.Modules`.
- **Suggested acceptance criteria:** *(a)* a signature-required matrix exists in the URS; *(b)* every module carrying `PermissionAction.Sign` either implements a signing ceremony or drops the action from its bundle; *(c)* an architecture test fails when a module declares `Sign` with no `IESignatureService` call path.
- **Severity:** **Critical**
- **Responsible role:** Quality Manager (scope) + Solution Architect (matrix)

---

**GAP-AUTH-004 — Access-review writes are gated by a read privilege**

- **Source reference:** `AccessReviewsController.cs:20` (class-level `[RequirePermission(AccessReviews, View)]`), `:27-29` (`POST` open), `:31-36` (`POST` complete) — neither method overrides the class filter.
- **Description:** opening and completing a user-access review — the Part-11 §11.10(d) / Annex 11 §12 recertification control — require only `access-reviews.view`. The catalogue defines six further actions for the module (`Create, Edit, Approve, Void, Sign, Export`, via `SignedRecordLifecycle` at `PermissionCatalog.cs:156`), all grantable, none enforced. The command-layer policy is `[RequireInternalActor]` (`AccessReviewSlice.cs:13,15`), i.e. any tier except `ExternalAuditor`.
- **Impact:** anyone who can *read* the access-review register can *close* one, permanently and immutably, with a conclusion of their choosing. The record then asserts that access was recertified.
- **Testing limitation:** an authorization-matrix case for this module cannot demonstrate a meaningful view/write split, because there is none. Any `TC-AUTH-SEC-*` case must assert the as-built behaviour (a view-only role can complete a review) and be labelled `[ID]`.
- **Recommended clarification:** which privilege should open a review, and which should complete it? A `SignedRecordLifecycle` module arguably wants `Create` to open and `Approve` (or `Sign`) to complete.
- **Suggested acceptance criteria:** *(a)* `POST /api/access-reviews` carries `[RequirePermission(AccessReviews, Create)]`; *(b)* `POST /api/access-reviews/{id}/complete` carries `Approve` or `Sign`; *(c)* the command policies become `[RequirePermissionPolicy]` with matching keys; *(d)* a functional case proves a `view`-only role receives `403 AUTHZ-403` on both writes.
- **Severity:** **Critical**
- **Responsible role:** Lead Developer + Quality Manager

---

**GAP-AUTH-005 — Security events never record the originating address**

- **Source reference:** `Domain/ComplianceLedger/LedgerEntries.cs:49` (`public string? IpAddress`, persisted as `inet` since `Hardening1_TypesAndNames`) vs `Infrastructure/Compliance/ComplianceLedgerServices.cs:68-83` (the only writer, which sets `Id, TenantId, EventType, Actor, Detail, OccurredAtUtc` — not `IpAddress`).
- **Description:** the column exists, is typed, is indexed by the hardening migrations, and is null on every row ever written. `RateLimiting.ClientKey` already reads `context.Connection.RemoteIpAddress` (`RateLimiting.cs:97-98`), so the value is available in the request scope.
- **Impact:** URS-016 requires security-relevant events to be recorded; Part 11 §11.300(d) requires attempts at unauthorised use to be *detected and reported*. Without a source address, a burst of `LOGIN_FAILED` rows cannot be attributed, and a forensic reviewer cannot separate one attacker from many users.
- **Testing limitation:** any case asserting IP capture in `audit.security_event` fails by construction. Cases must assert `ip_address IS NULL` and be labelled `[ID]` until this is closed.
- **Recommended clarification:** is source-address capture required by the laboratory's security procedure, and does the deployment's proxy configuration make `X-Forwarded-For` trustworthy (ADR-0002 terminates TLS at the proxy)?
- **Suggested acceptance criteria:** *(a)* `ISecurityEventLog.WriteAsync` accepts or resolves the client address and persists it; *(b)* the value is the real client IP after forwarded-headers processing; *(c)* an integration case asserts a non-null `ip_address` on a `LOGIN_FAILED` row.
- **Severity:** **Moderate**
- **Responsible role:** Lead Developer

---

**GAP-AUTH-006 — `refresh_session` and `password_history` hold credential material outside RLS and outside the documented deviation**

- **Source reference:** measured `pg_class` (§0.4); `20260728130923_Phase7RefreshSessions.cs:14-49` (no RLS statements); `IdentityAndImprovementConfigurations.cs:133-142`; accepted deviation **B9** as recorded in ground truth §2 line 73, which names only `user_account` and `outbox_event`.
- **Description:** `qams.refresh_session` (live session tokens, hashed) and `saas.password_history` (retired password hashes) have no `tenant_id` column, no RLS, no policy, and no foreign key to `user_account`. They are therefore invisible to the `tenant_isolation` model and to the CASCADE that removes a user's other rows. The design intent is defensible — a session is bound to a user, not a tenant (`RefreshSession.cs:10-13`) — but it is undocumented as a deviation.
- **Impact:** *(a)* the accepted-deviation register understates the RLS exception surface, so a compliance reviewer auditing "every table is fenced" gets an incomplete answer; *(b)* the missing FK means deleting a `user_account` row leaves orphaned session and password-history rows (the FKs that do exist — `user_branch_access`, `user_department_access` — CASCADE); *(c)* `saas.password_history` retains credential hashes indefinitely for deleted users, with no purge (`OutboxProcessor.RunRetentionPurgeAsync` prunes refresh sessions at `:264-272` but not password history).
- **Testing limitation:** `TC-AUTH-RLS-*` cases must assert the *absence* of RLS on these two tables — a positive assertion of the as-built state, not of a requirement. A structural sweep test in the style of `OwnedChildTenancyTests` would flag them as violations unless explicitly excepted.
- **Recommended clarification:** should these two tables be listed in deviation B9? Should `password_history` gain a retention rule and a cascading FK?
- **Suggested acceptance criteria:** *(a)* deviation B9 (or a new B-number) names all four RLS-exempt tables with the rationale for each; *(b)* an FK `password_history.user_id → user_account(id) ON DELETE CASCADE` exists or its absence is justified in writing; *(c)* the structural RLS sweep test carries an explicit, named exception list rather than an implicit one.
- **Severity:** **Moderate**
- **Responsible role:** Solution Architect + Database Owner

---

**GAP-AUTH-007 — An MFA-enrollment-scoped session cannot log out**

- **Source reference:** `Middleware/RequestIdentity.cs:172-179` (allow-list) and `:183-195` (the 403 branch); `AuthController.cs:75-82` (`/api/auth/logout` is `[AllowAnonymous]` but still passes through the middleware).
- **Description:** the allow-list contains four paths: `/api/auth/mfa/enroll`, `/api/auth/mfa/confirm` and their `/api/v1/` mirrors. `/api/auth/logout` and `/api/auth/me/privileges` are absent. A user issued a `scope=mfa_enrollment` token who sends it on a logout request receives `403 MFA-ENROLL-REQUIRED`. Logout *without* the `Authorization` header succeeds (no `scope` claim to read), so whether the user is stuck depends entirely on whether the SPA's interceptor attaches the token — which is not a property the API should rely on.
- **Impact:** a privileged user who reaches the enrolment gate and decides not to enrol has no server-acknowledged way to end the session. It is a usability defect rather than a security hole (the enrolment session holds no refresh cookie, `Login.cs:128-136`, and expires in 15 minutes), but it is an unexpected 403 on a session-termination action.
- **Testing limitation:** requires `Security:RequireMfaForPrivilegedRoles=true` and a privileged account without MFA to reach the state at all. Two cases are needed — with and without the `Authorization` header — and they will disagree, which is the finding.
- **Recommended clarification:** should `/api/auth/logout` and `/api/auth/me/privileges` be reachable from an enrolment-scoped session?
- **Suggested acceptance criteria:** *(a)* `/api/auth/logout` (+ `/api/v1/` mirror) is added to the allow-list; *(b)* a functional case proves an enrolment-scoped token receives `204` on logout; *(c)* the allow-list uses exact-segment matching rather than `StartsWith`, so `/api/auth/mfa/enrollment-x` does not pass.
- **Severity:** **Minor**
- **Responsible role:** Lead Developer

---

**GAP-AUTH-008 — A completed access review records a count, not a recertification**

- **Source reference:** `Domain/IdentityAccess/UserAccessReview.cs:26` (`int? AccountsReviewed`), `:38-58` (`Complete`); `AccessReviewSlice.cs:55-58` (the count is `db.Users.CountAsync(u => u.TenantId == tenantId && u.IsActive)` at the completion instant).
- **Description:** completion stores one integer, one boolean and one free-text conclusion. The record does not identify **which** accounts were examined, what role each held, which were changed, or who approved each decision. The count is sampled at completion, so an account created during the review inflates it and one deactivated during the review deflates it, with no way to tell afterwards.
- **Impact:** ISO 17025 §7.11.2 and Annex 11 §12 expect evidence of what was reviewed. A completed review asserts "N accounts were recertified" without evidence that any specific account was looked at. The immutability guarantee (`UAR-010`) therefore protects a record with limited evidentiary content.
- **Testing limitation:** no case can verify that a review covered a given account, because the data does not exist. Coverage cases can only assert that `accounts_reviewed` equals the active-user count at completion — which is a test of `CountAsync`, not of recertification.
- **Recommended clarification:** what does the laboratory's SOP require a completed access review to evidence? Per-account line items with a decision, or a period attestation?
- **Suggested acceptance criteria:** *(a)* the URS states the required evidentiary content of a completed review; *(b)* if per-account evidence is required, an owned child collection captures `(user_id, role_at_review, decision, reviewer_note)`; *(c)* the account roster is snapshotted at **open**, not at complete, so mid-review churn is visible; *(d)* an integration case proves an account deactivated mid-review still appears in the snapshot.
- **Severity:** **Major**
- **Responsible role:** Quality Manager (requirement) + Lead Developer

---

**GAP-AUTH-009 — Change-password does not feed the lockout counter**

- **Source reference:** `Login.cs:200-204` — the combined guard returns `AUTH-001` without calling `RegisterFailedLogin`; contrast `Login.cs:82-87`, where the login path does.
- **Description:** `POST /api/auth/change-password` is `[AllowAnonymous]` and verifies the current password (`hasher.Verify(user.PasswordHash, command.CurrentPassword)`). A wrong current password produces `401 AUTH-001` and no state change: the counter does not advance, no lockout accrues, and no `LOGIN_FAILED` (or any) security event is written on that branch. The only throttle is the 10/min per-IP `AuthPolicy` (`AuthController.cs:18`).
- **Impact:** it is a second, unmonitored password oracle. An attacker distributing attempts across addresses can test passwords at 10/min/IP indefinitely without ever locking the target account, and without leaving a single row in `audit.security_event` — whereas the same attempts on `/api/auth/login` would lock the account after five and log every one. URS-003 and URS-016 are satisfied on the login path only.
- **Testing limitation:** a case asserting a lockout after five wrong current-passwords will fail. Cases must assert the as-built behaviour (`[ID]`), and a security case should demonstrate the asymmetry between the two endpoints explicitly.
- **Recommended clarification:** should the reuse-ban and credential-verification failures of change-password count toward the same lockout as login?
- **Suggested acceptance criteria:** *(a)* a failed credential verification in `ChangePasswordHandler` calls `RegisterFailedLogin` and persists it; *(b)* the failure writes a security event distinguishable from a login failure; *(c)* a functional case proves five wrong current-passwords lock the account; *(d)* `AUTH-102` (reuse ban) is decided separately — it is a policy rejection with a *correct* current password and arguably should not count.
- **Severity:** **Major**
- **Responsible role:** Lead Developer + Security Owner

---

**GAP-AUTH-010 — The brief's privilege codes do not exist**

- **Source reference:** brief privilege table (`USER.CREATE`, `USER.MANAGE`, …) vs `PermissionCatalog.Key()` (`PermissionCatalog.cs:194-195`) producing `users.view` / `users.manage`, and `PermissionCatalog.AllKeys` (`:189-191`) as the closed set.
- **Description:** no permission key in this build uses the brief's uppercase dotted form. The AUTH module's real keys are `users.view`, `users.manage` and `access-reviews.view` (plus six unenforced `access-reviews.*` keys). This is the module-local instance of the naming divergence ground truth §2 line 64 records once globally.
- **Impact:** any test, RTM row or role matrix written against the brief's codes references nothing. `AuthorizationBehavior` throws `AUTHZ-008` for an unknown key (`:68-69`), so a mistyped key fails loudly at runtime — good — but a *document* carrying the wrong key just misleads.
- **Testing limitation:** none, once the mapping is applied. Every AUTH case must cite the real `{module}.{action}` key in its `Required Permission` field.
- **Recommended clarification:** confirm the mapping table (`USER.CREATE` → `users.manage`; `USER.VIEW` → `users.view`; access-review codes → `access-reviews.view`) with the requirement owner before the RTM is signed.
- **Suggested acceptance criteria:** *(a)* the brief/URS is amended to the real key format; *(b)* the Role & Permission Matrix carries the mapping; *(c)* no test artefact references an uppercase dotted code.
- **Severity:** **Minor**
- **Responsible role:** Quality Manager (documentation)

---

**GAP-AUTH-011 — The `SIG-` code prefix collides across two unrelated subsystems**

- **Source reference:** `ComplianceLedgerServices.cs:94,101,108,114` (e-signature: `SIG-404/003/002/001`) vs `Domain/AnalyticalQuality/SigmaAssessment.cs:53,72,77,82,101` and `Application/AnalyticalQuality/SigmaAssessmentSlice.cs:73,112` (six-sigma: `SIG-001/002/003/010/011/404`).
- **Description:** `SIG-001`, `SIG-002`, `SIG-003` and `SIG-404` each carry two different meanings depending on which subsystem raised them, and all four map to the same HTTP status in the same problem+json shape. CLAUDE.md §2.2 requires structured, non-magic error codes; a code whose meaning depends on the caller is not structured. *This also corrects ground truth §2 line 54 — see §0.2.*
- **Impact:** a client, an alerting rule or a test that keys on `code == "SIG-003"` cannot tell "account locked after repeated failed signings" from "the CV must be a positive percentage". Part-11 §11.300(d) reporting on failed signings cannot be built on the code alone.
- **Testing limitation:** every AUTH e-signature case must assert **endpoint + code** together, and must state in `Notes` that the code is ambiguous outside that endpoint. Cross-module code-uniqueness tests will fail.
- **Recommended clarification:** which subsystem keeps `SIG-`? The e-signature service is the one a regulator will look for.
- **Suggested acceptance criteria:** *(a)* one subsystem is renamed (e.g. six-sigma → `SIGMA-*`); *(b)* an architecture test asserts every error-code string in `src/` maps to exactly one message; *(c)* the error-code register lists each code once.
- **Severity:** **Moderate**
- **Responsible role:** Solution Architect

---

**GAP-AUTH-012 — Idle timeout is client-side only**

- **Source reference:** `frontend/src/app/core/auth.service.ts:156-166` (30-minute `setTimeout`, reset on interaction); no counterpart anywhere in `WebApi/Middleware/RequestIdentity.cs` or the token model. URS-007 (`docs/validation/01-User-Requirements-Specification.md:30`).
- **Description:** the only idle enforcement is a browser timer. The API accepts a valid bearer token for its full 15 minutes and a valid refresh cookie for its full 14 days regardless of user activity. A token captured from a walked-away workstation, or a non-browser client, is unaffected by the timer.
- **Impact:** URS-007 is met by a control that a client can simply not implement. The 15-minute access-token lifetime is a partial compensating control, but the refresh cookie renews indefinitely without any activity signal.
- **Testing limitation:** URS-007 cannot be verified at the API level — no server behaviour changes with idleness. Cases must be `E2E`/browser-scoped and are testing the SPA, not the system. An `[RNV]` label is appropriate for any server-side idle claim.
- **Recommended clarification:** does the laboratory's procedure accept a client-enforced idle timeout, given that the API is directly reachable?
- **Suggested acceptance criteria:** *(a)* URS-007 states explicitly that enforcement is client-side, with the compensating controls named; **or** *(b)* `refresh_session` records `last_used_at_utc` and refresh is refused after an idle window, with an integration case proving it.
- **Severity:** **Moderate**
- **Responsible role:** Security Owner + Quality Manager

---

**GAP-AUTH-013 — No administrative unlock**

- **Source reference:** the nine `/api/users` routes in `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt` — none unlocks; `UserAccount.cs:145-146` (reset clears the lock as a side effect); `UserAccount.cs:120-122` (deactivate/reactivate do not).
- **Description:** once `locked_until_utc` is set, an administrator's only options are to wait 30 minutes or to call `POST /api/users/{id}/reset-password`, which forces a new password on the user purely to clear a lock. Reactivating a deactivated-while-locked account restores it **still locked** (§3.1, `S4 → S2`).
- **Impact:** a routine operational need — "unlock this analyst, they mistyped" — has no first-class operation, and the workaround changes a credential the user did not ask to change. There is also no audit distinction between "reset because forgotten" and "reset because locked".
- **Testing limitation:** no positive unlock case can be authored. Recovery cases must go through reset-password and assert the lock-clearing side effect, which conflates two behaviours.
- **Recommended clarification:** is an explicit unlock operation required by the laboratory's SOP?
- **Suggested acceptance criteria:** *(a)* `POST /api/users/{id}/unlock` gated by `users.manage`, calling a new `UserAccount.Unlock()` that clears `LockedUntilUtc` and `FailedLoginAttempts` and raises an auditable event; *(b)* reactivation of a locked account either clears the lock or the behaviour is documented; *(c)* a functional case proves an unlocked account can sign in immediately.
- **Severity:** **Moderate**
- **Responsible role:** Product Owner + Lead Developer

---

**GAP-AUTH-014 — Deactivation does not eagerly revoke refresh sessions**

- **Source reference:** `UserManagement.cs:131-140` (`SetUserActiveHandler` — `Deactivate()` and `SaveChanges`, nothing else); `RefreshSessions.cs:119-125` (lazy revocation at the next refresh); `RequestIdentity.cs:98-102` (per-request block).
- **Description:** deactivating a user leaves every row in `qams.refresh_session` for that user in state `Live`. Access is correctly denied at the next request (`AUTH-006`) and the next refresh (`AUTH-006`, which revokes that one session only). But sibling sessions in other families — a second browser, a second device — remain `Live` in the database until each is individually presented, and are purged only 7 days after their own expiry (`OutboxProcessor.cs:264-272`).
- **Impact:** the enforcement outcome is correct (nothing works), so this is not an access-control defect. It is an evidence and hygiene defect: a Part-11 reviewer asking "were this user's sessions terminated when they left?" sees live session rows, and the revocation timestamps do not record the deactivation event. Contrast the explicit family revocation that logout performs (`RefreshSessions.cs:173-178`).
- **Testing limitation:** a case asserting "all refresh sessions revoked on deactivate" fails. Cases must assert the as-built lazy behaviour and be labelled `[ID]`, with a DB assertion that `revoked_at_utc IS NULL` immediately after deactivation.
- **Recommended clarification:** should deactivation eagerly revoke the family, as logout does?
- **Suggested acceptance criteria:** *(a)* `SetUserActiveHandler`, on deactivation, revokes every non-revoked `RefreshSession` for that user and writes a security event; *(b)* an integration case proves `revoked_at_utc` is set on all of them within the same transaction; *(c)* the same applies wherever an account is disabled.
- **Severity:** **Moderate**
- **Responsible role:** Lead Developer

---

**GAP-AUTH-015 — The user directory is readable by every authenticated tenant user**

- **Source reference:** `Controllers/UserDirectoryController.cs:13-20` (`[Authorize]`, no `[RequirePermission]`); `UserManagement.cs:186-194` (`GetUserDirectoryHandler`, tenant-filtered, `IsActive` only); it is a **query**, so `AuthorizationBehavior` does not gate it (`AuthorizationBehavior.cs:44-47`, and `:24` documents the choice).
- **Description:** `GET /api/users/directory` returns `(Id, DisplayName, Role)` for every active user of the caller's tenant to any authenticated caller, including `ExternalAuditor` and any role with zero granted privileges. The class comment claims "full user administration stays TenantAdmin-only on UsersController" (`:11-12`), which is itself stale — `UsersController` is privilege-gated, not role-gated, since v1.51.0.
- **Impact:** the exposure is deliberate and bounded (no email, no security fields, `UserDirectoryEntryDto` at `Contracts/IdentityAccess/UserContracts.cs:25`), and name pickers genuinely need it. But the **role tier** of every colleague is disclosed to every user, which is organisational-structure information some laboratories treat as restricted, and there is no privilege by which a tenant can restrict it.
- **Testing limitation:** an authorization case for this endpoint has no negative arm — no authenticated tenant caller can be refused. The only negative cases are anonymous (`401 AUTH-401`) and cross-tenant (which the tenant filter and RLS handle).
- **Recommended clarification:** is the roster with role tiers acceptable for all authenticated users, and specifically for `ExternalAuditor`?
- **Suggested acceptance criteria:** *(a)* the decision is recorded in the URS or an ADR with the exposed field list; *(b)* if restriction is wanted, the endpoint takes `[RequirePermission(Users, View)]` and name pickers degrade gracefully; *(c)* the stale "TenantAdmin-only" comment at `UserDirectoryController.cs:11-12` is corrected; *(d)* a functional case pins the exact DTO shape so a future field addition breaks the build.
- **Severity:** **Minor**
- **Responsible role:** Product Owner + Lead Developer

---

**GAP-AUTH-016 — Platform-administrator accounts have no managed lifecycle**

- **Source reference:** `UserManagement.cs:21-24` (`TenantRole.Parse` rejects `PlatformAdmin` with `USER-005`); `UserAccount.cs:99-102,127-130` (domain refuses a tenanted platform admin); `WebApi/Startup/StartupSeeding.cs:68-84` (the only creation path, from `PlatformAdmin:Email` / `PlatformAdmin:Password`).
- **Description:** platform administrators can be created only by startup seeding from configuration. No API endpoint creates, lists, deactivates, reactivates, resets or role-changes one — `RegisterUserCommand` and `ChangeUserRoleCommand` both refuse the tier, and `UsersController` handlers filter on the caller's tenant (`UserManagement.cs:111`), which a platform admin does not have. The bootstrap password is **not** subject to `PasswordRules.StrongPassword` (it is hashed directly at `StartupSeeding.cs:84`).
- **Impact:** the most privileged identity in the system is provisioned by an environment variable, is unmanaged thereafter, and its password bypasses the policy that URS-002 applies to every other account. There is no in-application record of platform-admin lifecycle events beyond whatever the seeding path writes.
- **Testing limitation:** no API case can create, disable or rotate a platform admin. Cases must use the seeded `platform-admin@localhost` account and cannot cover deactivation, role change or password reset for that tier at all. A password-policy case against the bootstrap credential is impossible via any endpoint.
- **Recommended clarification:** what is the intended lifecycle for platform administrators — configuration-managed by design, or an unimplemented capability? Should the bootstrap password be policy-checked at startup?
- **Suggested acceptance criteria:** *(a)* the URS states the platform-admin provisioning model explicitly as configuration-managed, or an administration surface exists; *(b)* `StartupSeeding` validates the bootstrap password against `PasswordRules` and refuses startup on a weak one; *(c)* platform-admin creation writes a security event; *(d)* an integration case proves a weak `PlatformAdmin:Password` fails startup, in the style of the existing `ConfigGuard` tests.
- **Severity:** **Major**
- **Responsible role:** Security Owner + Lead Developer

---

*End of AUTH front matter. Detailed cases: `10-module-auth-cases-A.md` … `-F.md`.*
