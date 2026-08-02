# NT.QMS — Production Software Requirements Specification
## Document 04 · Configuration Reference

> [Conventions](00-SRS-Index-and-Conventions.md) · Deployment procedure:
> [Document 10](10-Deployment-Specification.md) · Security implications:
> [Document 09](09-Security-Specification.md)

This document is the **complete** configuration surface: every key the application reads, plus every
hard-coded constant that a laboratory might reasonably expect to be configurable but is not.

---

# 4.1 Configuration model

## Sources and precedence

Standard ASP.NET Core layering, highest precedence last:

```
1. appsettings.json                       (shipped defaults, no secrets)
2. appsettings.{Environment}.json         (Development shape only, no secrets)
3. .NET user-secrets                      (Development ONLY — UserSecretsId "nt-qams-webapi")
4. Environment variables                  (Production secret channel; "__" is the section separator)
5. Command-line arguments
```

**Secrets are never in source control.** `appsettings.json` ships every secret as an empty string;
`appsettings.Development.json` does the same and carries an explicit comment pointing at
`deploy/DEV-SECRETS.md`. A fresh clone **will not start** until the developer provisions user-secrets
— by design.

Environment-variable form: `Jwt:Secret` → `Jwt__Secret`; `ConnectionStrings:Postgres` →
`ConnectionStrings__Postgres`.

## Read discipline — `ConfigGuard` (CFG-001 / CFG-002)

Typed settings are **not** read with `GetValue<T>()`. They go through
`NT.QAMS.Infrastructure.Configuration.ConfigGuard`, which implements one rule:

> **A missing key falls back to its documented default. A present-but-invalid value throws at
> start-up.**

```
ReadInt(config, key, fallback)      → missing ⇒ fallback ; unparseable ⇒ InvalidOperationException
ReadBool(config, key, fallback)     → same, expects true/false
ReadDecimal(config, key, fallback)  → same, InvariantCulture
```

The refusal message is deliberately explicit:
> `Configuration '{key}' has invalid value '{raw}' — expected {expected}. Refusing to start rather
> than silently applying the default (CFG-002).`

The motivating case is named in the source comment: a mistyped `"Securty:RequireMfa=treu"` must never
quietly disable an MFA policy.

## Validation at composition

Four settings objects self-validate at DI registration and abort start-up if invalid:

| Object | Rule | Refusal |
|---|---|---|
| `WestgardLimits.Validated()` | all SD limits > 0; `WarningSd < RejectSd`; `RunLength ≥ 2` | `InvalidOperationException` |
| `RateLimitSettings.Validated()` | all four permits > 0 | `"RateLimit:* permits must all be positive."` |
| `OutboxOptions.Validated()` | `RetentionDays > 0` | `"Outbox:RetentionDays must be a positive number of days."` |
| `RefreshSessionOptions.Validated()` | `RefreshTokenDays > 0` | `"Auth:RefreshTokenDays must be a positive number of days."` |

Two keys are hard-required with no default — the application throws immediately if absent:
`ConnectionStrings:Postgres` and `Jwt:Secret`.

---

# 4.2 Configuration key catalogue

Legend — **Req**: ✅ required, ⭕ optional. **Restart**: all keys are read once at start-up; nothing is
hot-reloaded.

## Connectivity

| ID | Key | Type | Default | Req | Validation | Impact of change |
|---|---|---|---|---|---|---|
| **CFG-01** | `ConnectionStrings:Postgres` | connection string | *(empty in all shipped files)* | ✅ | Absent ⇒ `InvalidOperationException: "ConnectionStrings:Postgres is not configured."` at start-up | The single database. Also read directly by `DatabaseRoleGuard` and `PostgresReadinessHealthCheck`. Wrong value ⇒ readiness 503 and deferred seeding retries every 15 s. |
| **CFG-02** | `Database:MigrateOnStartup` | bool | `false` | ⭕ | `ConfigGuard`-style bool via `GetValue<bool>` | `true` applies pending EF migrations at boot. **Deliberate residual: this path still fails fast if the schema gate refuses — keep it OFF in production.** Pipelines should use the idempotent SQL script instead. |
| **CFG-03** | `AllowedHosts` | string | `"*"` | ⭕ | none | ASP.NET host filtering. Left permissive because TLS and host routing terminate at the proxy. |

## Authentication & session

| ID | Key | Type | Default | Req | Validation | Impact |
|---|---|---|---|---|---|---|
| **CFG-04** | `Jwt:Secret` | string | *(empty)* | ✅ | Absent ⇒ throw; **must be ≥ 32 characters** | HMAC signing key for access tokens. Rotating it invalidates every live access token immediately (refresh cookies survive until their own rotation fails validation). |
| **CFG-05** | `Jwt:Issuer` | string | `"nt-qams"` | ⭕ | validated on every token | Must match between issuer and validator or **all** tokens fail. |
| **CFG-06** | `Jwt:Audience` | string | `"nt-qams"` | ⭕ | validated on every token | As above. |
| **CFG-07** | `Jwt:ExpiryMinutes` | int | **120** | ⭕ | — | Access-token lifetime. **⚠ ADR-0009 specifies 15 minutes; the shipped value is 120.** This is a real drift between the decision record and the configuration — see [GAP](13-Implementation-vs-SRS-Gap-Analysis.md). Longer ⇒ a stolen in-memory token is useful for longer; shorter ⇒ more silent-refresh traffic. |
| **CFG-08** | `Auth:RefreshTokenDays` | int | **14** | ⭕ | `Validated()` — must be > 0 | The sign-in horizon: how long a user stays signed in across silent refreshes. |
| **CFG-09** | `Security:RequireMfaForPrivilegedRoles` | bool | **false** | ⭕ | `ConfigGuard.ReadBool` | **Platform-admin fallback only** — tenant users are governed by the per-tenant `TenantSettings.RequireMfaForPrivilegedRoles`. |
| **CFG-10** | `PlatformAdmin:Email` | string | *(empty)* | ⭕ | — | Bootstrap platform administrator. When both this and the password are set and no such account exists, one is created at start-up. |
| **CFG-11** | `PlatformAdmin:Password` | string | *(empty)* | ⭕ | hashed on use | As above. **Must be removed or rotated after first boot** — it is a standing credential in configuration. |

## Password policy

| ID | Key | Type | Default | Req | Validation | Impact |
|---|---|---|---|---|---|---|
| **CFG-12** | `PasswordPolicy:MaxAgeDays` | int | **90** | ⭕ | `ConfigGuard.ReadInt` | Password expiry horizon. |
| **CFG-13** | `PasswordPolicy:HistoryDepth` | int | **5** | ⭕ | `ConfigGuard.ReadInt` | A new password must differ from the last `HistoryDepth + 1` (= 6 by default) — see `AUTH-102`, whose message quotes the computed number. |

> Length (12), maximum (200), character-class requirements and the breached-password blocklist are
> **not configurable** — see §4.4 CON-01…CON-04.

## Rate limiting

All four are per-minute permits in a **fixed window** with `QueueLimit = 0` (excess is rejected, never
queued). Rejection is **429** with `Retry-After: 60`.

| ID | Key | Type | Default | Partition | Impact |
|---|---|---|---|---|---|
| **CFG-14** | `RateLimit:GlobalPermitPerMinute` | int | **300** | client IP address | Whole API surface. **This is an abuse ceiling, not a concurrency ceiling** — the load test showed ~50 % 429s from one origin at 50 virtual users. **Must be sized per site to legitimate peak concurrency, especially where a laboratory shares one NAT address.** |
| **CFG-15** | `RateLimit:AuthPermitPerMinute` | int | **10** | client IP address | `/api/auth/*`. A burst here is credential guessing, not a workload. |
| **CFG-16** | `RateLimit:RefreshPermitPerMinute` | int | **60** | client IP address | Deliberately more generous than the credential budget: whole laboratories share NAT addresses and every signed-in tab refreshes periodically. |
| **CFG-17** | `RateLimit:ESignaturePermitPerMinute` | int | **10** | **actor (`sub` claim)** | Signing ceremonies are authenticated, so the *person* is throttled, not the address — this is PIN-guessing protection. |

All four must be positive or the application refuses to start.

## Analytical quality

| ID | Key | Type | Default | Validation | Impact |
|---|---|---|---|---|---|
| **CFG-18** | `AnalyticalQuality:Westgard:WarningSd` | decimal | **2** | > 0 and `< RejectSd` | The `1-2s` warning limit and the `2-2s` rejection limit. **Rule labels are derived from these values**, so changing it changes the emitted rule name (e.g. `1-2.5s`). |
| **CFG-19** | `AnalyticalQuality:Westgard:RejectSd` | decimal | **3** | > 0 and `> WarningSd` | The `1-3s` rejection limit. |
| **CFG-20** | `AnalyticalQuality:Westgard:RangeSd` | decimal | **4** | > 0 | The `R-4s` range-rule span. |
| **CFG-21** | `AnalyticalQuality:Westgard:RunLength` | int | **10** | ≥ 2 | The `10-x` shift-rule window. |

> These are **global to the deployment**, not per tenant and not per QC profile. A multi-tenant host
> cannot give two laboratories different Westgard limits. See [LIM-QC-04](02-3-Functional-Specification-Analytical-Quality.md).

## Messaging & storage

| ID | Key | Type | Default | Req | Impact |
|---|---|---|---|---|---|
| **CFG-22** | `Outbox:RetentionDays` | int | **30** | ⭕ (validated > 0) | How long **processed** outbox rows are kept before the hourly purge deletes them. Processed rows are transport, not the record — the hash-chained ledger keeps the history (MSG-007). |
| **CFG-23** | `FileStorage:RootPath` | path | `{AppContext.BaseDirectory}/data/files` | ⭕ | Root of content-addressed evidence storage. **The default is inside the deployment directory and would be lost on a clean redeploy — set this to a durable, backed-up volume.** The directory is created at start-up if absent. |

## E-mail (optional)

The **presence of `Smtp:Host` alone** decides which sender is bound:

```
Smtp:Host empty/absent  → LoggingEmailSender  (e-mails written to the log, marked Sent)
Smtp:Host set           → SmtpEmailSender     (real delivery)
```

| ID | Key | Type | Default | Impact |
|---|---|---|---|---|
| **CFG-24** | `Smtp:Host` | string | *(unset)* | **The switch.** Unset ⇒ no e-mail leaves the system, silently. |
| **CFG-25** | `Smtp:Port` | int | **587** | Unparseable values fall back to 587 (this key does **not** use `ConfigGuard`, so a typo is silently tolerated). |
| **CFG-26** | `Smtp:From` | string | `Smtp:User`, else `ntqams@localhost` | Sender address. |
| **CFG-27** | `Smtp:User` | string | *(unset)* | When set, credentials are attached. |
| **CFG-28** | `Smtp:Password` | string | *(unset)* | Paired with `Smtp:User`. **A secret — use the environment/secret store.** |
| **CFG-29** | `Smtp:Ssl` | string | *(unset ⇒ SSL **enabled**)* | Inverted logic: SSL is on unless the value is literally `"false"` (case-insensitive). |

## Observability

| ID | Key | Type | Default | Impact |
|---|---|---|---|---|
| **CFG-30** | `Otlp:Endpoint` | URI | *(unset)* | When set, adds OTLP exporters for **traces, metrics and logs**. When unset, tracing and metrics still run locally and `/metrics` still serves Prometheus text — only the push exporters are omitted. |
| **CFG-31** | `Logging:LogLevel:Default` | string | `"Information"` | Standard .NET logging. |
| **CFG-32** | `Logging:LogLevel:Microsoft.AspNetCore` | string | `"Warning"` | Suppresses framework request noise. |

## Environment (not a config key, but decisive)

| ID | Variable | Values | Impact |
|---|---|---|---|
| **CFG-33** | `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` / other | **Four behaviours switch on this:** (1) `Production` ⇒ JSON console logging with scopes and UTC ISO-8601 timestamps; anything else ⇒ default console. (2) `Production` ⇒ `DatabaseRoleGuard.EnsureLeastPrivilegeAsync` **refuses to boot** on an over-privileged role; otherwise it logs warnings and continues. (3) `Development` ⇒ OpenAPI document mapped at `/openapi` (anonymous); otherwise not exposed. (4) non-`Development` ⇒ HSTS header emitted. |
| **CFG-34** | `ASPNETCORE_URLS` | URL list | Listen addresses (dev: `http://localhost:5080`). |
| **CFG-35** | `QMS_ITEST_POSTGRES` | connection string | **Test-only.** Integration tests run against this live PostgreSQL; absent ⇒ those tests skip cleanly. |

## Frontend

| ID | Location | Key | Default | Impact |
|---|---|---|---|---|
| **CFG-36** | `frontend/src/environments/environment.ts` | `production` | `true` | Angular build flag. |
| **CFG-37** | same | `apiBaseUrl` | `'/api'` | **Same-origin by default** (ADR-0007 — no CORS is configured server-side). Overriding this to an absolute cross-origin URL **will fail**: there is no CORS policy and the refresh cookie is `SameSite=Strict`. Change it only for a same-origin path change. |

> Note: the frontend has **one** environment file, not the usual `environment.ts` +
> `environment.prod.ts` pair. The deployment procedure is "edit this file before building".

---

# 4.3 Configuration by environment — recommended values

| Key | Development | Production |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` | **`Production`** (required for the role guard and JSON logs) |
| `ConnectionStrings:Postgres` | user-secrets, owner role acceptable | env var, **non-owner, non-superuser, non-BYPASSRLS role** |
| `Jwt:Secret` | user-secrets | env var / secret store, ≥ 32 chars, rotated on a schedule |
| `Jwt:ExpiryMinutes` | 120 (convenience) | **15** (align with ADR-0009) |
| `Database:MigrateOnStartup` | `true` (convenience) | **`false`** — run migrations as the owner role out of band |
| `RateLimit:GlobalPermitPerMinute` | 300 | **sized to the site's peak concurrent users**, not left at 300 |
| `Security:RequireMfaForPrivilegedRoles` | false | site decision |
| `FileStorage:RootPath` | default | **durable backed-up volume**, outside the deployment folder |
| `Smtp:Host` | unset (log-only) | set, or accept that notifications are in-app only |
| `Otlp:Endpoint` | unset | set to the collector |
| `PlatformAdmin:Email/Password` | user-secrets | set for first boot, **then removed** |

---

# 4.4 Hard-coded constants (not configurable)

These behave like configuration from a laboratory's point of view but require a code change. Each is a
candidate for externalisation — see [Document 15](15-Recommendations.md).

## Security & session

| ID | Constant | Value | Location | Why it matters |
|---|---|---|---|---|
| **CON-01** | Password minimum length | **12** | `PasswordRules.MinLength` | A site policy requiring 14 cannot be expressed. |
| **CON-02** | Password maximum length | **200** | `PasswordRules.MaxLength` | — |
| **CON-03** | Character-class requirements | upper + lower + digit + symbol | `PasswordRules` | — |
| **CON-04** | Breached-password blocklist | shipped list, offline | `PasswordRules` | No refresh mechanism; no HIBP integration. |
| **CON-05** | Max failed login attempts | **5** | `UserAccount.MaxFailedAttempts` | Also governs e-signature failures. |
| **CON-06** | Lockout duration | **30 minutes** | `UserAccount.LockoutMinutes` | — |
| **CON-07** | E-signature PIN format | exactly 4 digits | `SetPinValidator` regex | 10,000-value keyspace; mitigated only by the 10/min per-actor throttle and the shared lockout counter. |
| **CON-08** | JWT clock skew | **1 minute** | `TokenValidationParameters` | — |
| **CON-09** | SPA idle timeout | **30 minutes** | `auth.service` | Part-11 §11.10(d) automatic logoff — a site requiring 15 minutes cannot configure it. |
| **CON-10** | Refresh cookie name / path | `qams_rt` / `/api/auth` | `AuthController` | — |
| **CON-11** | HSTS max-age | **63,072,000 s** + `includeSubDomains` | `SecurityHeadersMiddleware` | 2 years; no `preload`. |
| **CON-12** | Content-Security-Policy | `default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'` | `SecurityHeadersMiddleware` | The **API's** policy. The SPA is served by the proxy and needs its own. |
| **CON-13** | Rate-limit window | **1 minute**, `QueueLimit = 0` | `RateLimiting.Window` | Only the permits are configurable, not the window. |
| **CON-14** | Upload sniff window | **512 bytes** | `FileContentPolicy.HeaderLength` | — |
| **CON-15** | Upload allow-list | pdf, png, jpg, jpeg, docx, xlsx, doc, xls, csv, txt | `FileContentPolicy.Allowed` | A laboratory needing `.zip` or `.xml` evidence cannot add it. |
| **CON-16** | Maximum upload size | **none in application code** | — | Only the host body limit applies. **This is a gap, not a constant.** |

## Timing & reliability

| ID | Constant | Value | Location |
|---|---|---|---|
| **CON-17** | DB retry attempts / max delay | **5** / **10 s** | `DependencyInjection` |
| **CON-18** | DB command timeout | **30 s** | `DependencyInjection` |
| **CON-19** | Outbox poll interval | **2 s** | `OutboxProcessor` |
| **CON-20** | Outbox batch size | **50** | `OutboxProcessor.BatchSize` |
| **CON-21** | Outbox max attempts | **5** | `OutboxProcessor.MaxAttempts` |
| **CON-22** | Outbox backoff base | **5 s** exponential | `OutboxProcessor.BackoffBase` |
| **CON-23** | Outbox claim lease | **2 min** | `OutboxProcessor.ClaimLease` |
| **CON-24** | Outbox purge interval | **1 h** | `OutboxProcessor.PurgeInterval` |
| **CON-25** | Outbox queue-stats interval | **30 s** | `OutboxProcessor` |
| **CON-26** | Compliance sweep interval | **1 h** | `ScheduledSweepService.Interval` |
| **CON-27** | Compliance sweep start delay | **15 s** | `ScheduledSweepService` |
| **CON-28** | KPI snapshot interval | **6 h** | `KpiSnapshotService.Interval` |
| **CON-29** | KPI snapshot start delay | **20 s** | `KpiSnapshotService` |
| **CON-30** | Replica sentinel retry | **60 s** | `SingleReplicaGuardService.RetryInterval` |
| **CON-31** | Deferred seeding retry | **15 s** | `DeferredStartupSeeder.RetryInterval` |
| **CON-32** | Escalation ladder | +24 h / +48 h / +72 h, 3 levels | `EscalationTimer` |
| **CON-33** | Escalation recipient role | `"QualityManager"` (string literal) | `EscalationTimer.EscalationRole` |

## Business thresholds

| ID | Constant | Value | Location | Laboratory impact |
|---|---|---|---|---|
| **CON-34** | Competency pass mark | **80** | `CompetencyRecord.PassMark` | A site whose procedure uses 85 % cannot configure it. **Highest-value externalisation candidate.** |
| **CON-35** | PT questionable threshold | `\|z\| > 2` | `PtEnrollment.QuestionableThreshold` | ISO 13528 convention. |
| **CON-36** | PT unsatisfactory threshold | `\|z\| ≥ 3` | `PtEnrollment.UnsatisfactoryThreshold` | — |
| **CON-37** | Detection-limit Z | **1.645** | `DetectionLimitStudy.Z` | One-sided 95 %; CLSI EP17. A site wanting 99 % cannot change it. |
| **CON-38** | Minimum blank replicates | **10** | `DetectionLimitStudy` | — |
| **CON-39** | Minimum low-level replicates | **10** | `DetectionLimitStudy` | — |
| **CON-40** | Minimum linearity levels | **4** | `LinearityStudy.MinimumLevels` | Message says EP06 recommends 5–9. |
| **CON-41** | Minimum precision runs / replicates | **2** / **2** | `PrecisionStudy` | Well below EP05's 20×2×2 design. |
| **CON-42** | Reference-interval sample count | **20** | `ReferenceIntervalStudy.RecommendedSampleCount` | CLSI EP28-A3c verification. |
| **CON-43** | Reference-interval allowed-outside fraction | **0.10** | `ReferenceIntervalStudy.AllowedOutsideFraction` | 10 % — the EP28 verification rule. |
| **CON-44** | Method-comparison minimum pairs | **2** enforced, **40** recommended | `MethodComparisonStudy` | The EP09 recommendation of 40 is **documented but not enforced**. |
| **CON-45** | Carryover minimum readings | 1 high / 3 low | `CarryoverStudy` | — |
| **CON-46** | Interference minimum controls | **3** | `InterferenceStudy` | — |
| **CON-47** | Lot-comparison minimum pairs | **3** | `LotComparisonStudy` | — |
| **CON-48** | Outlier minimum points | **4** | `OutlierScreening` | — |
| **CON-49** | Outlier Tukey multiplier | **1.5 × IQR** | `OutlierScreening` | — |
| **CON-50** | Method-validation total-error factor | **1.65** | `ValidationStudy` | One-sided 95 %. |
| **CON-51** | Bland–Altman limit factor | **1.96 SD** | `MethodComparisonStudy` | **`[Assumption]`** — standard 95 % limits. |
| **CON-52** | Sigma grade bands | 3 / 4 / 5 / 6 | `SigmaAssessment.GradeFor` | — |
| **CON-53** | Deming λ | **1.0** | `MethodComparisonStudy` | Ordinary Deming; unequal error variances cannot be modelled. |
| **CON-54** | Supplier evaluation criteria cap | **50** | `RecordEvaluationValidator` | — |
| **CON-55** | Document default review cycle | **24 months** | `ControlledDocument.Create` default parameter | Per-document override at creation; no tenant default. |
| **CON-56** | Retention classes | 5 years / 10 years / Permanent | `RetentionClass` enum | A 7-year jurisdiction cannot be represented. |

## Pagination & query defaults

| ID | Default | Applies to |
|---|---|---|
| **CON-57** | `pageSize = 50` | all 13 paged list endpoints |
| **CON-58** | `take = 200` | compliance audit-trail / field-change / signature / security-event reads |
| **CON-59** | `take = 1000` | XLSX exports |
| **CON-60** | `take = 60` | QC run history (the Levey-Jennings window) |
| **CON-61** | `days = 90` | KPI history |

**None of these have an enforced upper bound** — a caller may request an arbitrarily large page or
`take`. See [Document 14](14-Technical-Debt-Report.md).

---

# 4.5 Secrets inventory

| Secret | Key | Dev channel | Production channel | Rotation impact |
|---|---|---|---|---|
| Database password | `ConnectionStrings:Postgres` | user-secrets | env var / secret store | Restart required |
| JWT signing key | `Jwt:Secret` | user-secrets | env var / secret store | **All access tokens invalid immediately** |
| Platform-admin bootstrap password | `PlatformAdmin:Password` | user-secrets | env var, then removed | Only used when the account does not exist |
| SMTP password | `Smtp:Password` | *(unset)* | env var / secret store | Restart required |

**Provisioning a fresh clone** (`deploy/DEV-SECRETS.md`, UserSecretsId `nt-qams-webapi`): set
`ConnectionStrings:Postgres`, `Jwt:Secret`, `PlatformAdmin:Email`, `PlatformAdmin:Password`. Without
them the API will not start — that is intentional (finding F-17).

---

# 4.6 Configuration acceptance criteria

| ID | Given | When | Then |
|---|---|---|---|
| **AT-CFG-01** | `Security:RequireMfaForPrivilegedRoles = "treu"` | the application starts | it **refuses to start** with the `CFG-002` message naming the key and the bad value |
| **AT-CFG-02** | `Security:RequireMfaForPrivilegedRoles` absent | the application starts | it starts with the documented default `false` |
| **AT-CFG-03** | `AnalyticalQuality:Westgard:WarningSd = 3`, `RejectSd = 3` | the application starts | it **refuses to start** (`WarningSd >= RejectSd`) |
| **AT-CFG-04** | `RateLimit:GlobalPermitPerMinute = 0` | the application starts | it **refuses to start** ("permits must all be positive") |
| **AT-CFG-05** | `Jwt:Secret` set to a 20-character string | the application starts | it **refuses to start** (≥ 32 characters required) |
| **AT-CFG-06** | `ConnectionStrings:Postgres` absent | the application starts | it throws immediately with the named key |
| **AT-CFG-07** | `Smtp:Host` unset | a notification fires | the dispatch row exists and is marked sent; the e-mail body appears **in the log**, not in a mailbox |
| **AT-CFG-08** | `ASPNETCORE_ENVIRONMENT=Production` and the DB role owns the tables | the application starts | it **refuses to boot** (`DatabaseRoleGuard`) |
| **AT-CFG-09** | `AnalyticalQuality:Westgard:WarningSd = 2.5` | a QC run at z = 2.6 is recorded | the emitted rule label is `1-2.5s`, not `1-2s` |
