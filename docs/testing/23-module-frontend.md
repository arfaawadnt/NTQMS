# 23 — Angular Frontend: Routes, Guards, Interceptors, Facades, Components, i18n/RTL, E2E

**Module code:** `FE`
**System under test:** NT.QMS **v1.51.2** — repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master`.
**Binding conventions:** [`docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md`](./00-GROUND-TRUTH-AND-CONVENTIONS.md) — the 28-field case format (§4), the evidence labels `[IV]` / `[RNV]` / `[ID]` / `[GD]` (§4), the ID convention (§5), the honesty rules (§6), and the **canonical detailed-case block** (§8). Entries marked *[corrected 2026-08-01]* in that file supersede everything older.
**Inspection date:** 2026-08-01. Every claim below was read in the file cited; nothing is inferred from the commissioning brief.

**Scope inspected (read, not assumed):**
`frontend/src/app/app.routes.ts`, `app.config.ts`, `app.component.ts`, `main.ts`, `src/index.html`, `src/environments/environment.ts`, `proxy.conf.json`;
`core/` — `auth.guard.ts`, `role.guard.ts`, `auth.interceptor.ts`, `change-reason.interceptor.ts`, `auth.service.ts`, `change-reason.service.ts`, `text-prompt.service.ts`, `change-reason-dialog.component.ts`, `text-prompt-dialog.component.ts`, `permissions.service.ts`, `i18n.service.ts`, `org-data.service.ts`, `models.ts`, `nav-icons.ts`, `help/help.service.ts`, `help/help-content.ts`, `api/*` (44 services);
`shell/shell.component.ts`; `shared/ui/*` (13 components); `features/*` (28 folders, 100 components, 34 facades);
all 15 `*.spec.ts` under `src/`; `e2e/*` (3 specs + README); `playwright.config.ts`; `angular.json`; `package.json`; `.github/workflows/ci.yml` (`frontend` job).

---

## Completeness statement

| Delivered here | Deferred |
|---|---|
| Implementation inventory (§1), brief divergences (§2), state-transition matrices (§3), decision tables (§4), UAT scenarios (§6), exploratory charters (§7), gap register (§8). | **§5 Detailed test cases** — authored separately into `23-module-frontend-cases-<A…F>.md` by other passes, per the split convention (conventions §7). The table below is a **reservation**, not a delivery: a reserved range with no matching case file is a coverage hole. |

No `## 0. Correction to ground truth` section appears below: every FE-scope fact asserted in the conventions file was re-measured and found correct (Angular **22.0.8** / TypeScript **6.0.3** / zone.js **0.15.1** — `frontend/package.json:15-25,41`; `i18n.service.ts` **1,518 lines**, in-code dictionaries, `en`/`ar`/`fr` — measured; **86** `path:` entries in `app.routes.ts` — measured; **15** `.spec.ts` under `src/` and **3** Playwright specs (`auth`, `regulated-workflow`, `a11y` with `@axe-core/playwright`) — measured; **76** frontend unit tests and **6** Playwright tests — counted `it()` blocks; no Tailwind, no ngx-translate in `package.json`).

---

## ID reservation table

Module-local sequences, three digits, never renumbered (conventions §5). Ranges are reserved **generously** so a batch can grow without colliding.

| Batch file | Slice of scope | Reserved ID ranges | Est. cases |
|---|---|---|---|
| `23-module-frontend-cases-A.md` | Routing table, guards (`authGuard`, `platformOnlyGuard`, `tenantOnlyGuard`), bootstrap hydration, lazy-load behaviour, wildcard/redirect handling, tenant front door `/t/:tenant` | `TC-FE-COMP-001`…`TC-FE-COMP-040` · `TC-FE-DT-001`…`TC-FE-DT-015` · `TC-FE-PATH-001`…`TC-FE-PATH-010` | ~35 |
| `23-module-frontend-cases-B.md` | Interceptor chain: `authInterceptor` (bearer attach, single-flight 401 refresh, auth-endpoint exclusion), `changeReasonInterceptor` (DELETE gating, cancel, trim, pre-supplied header), `AuthService` session/token/idle lifecycle | `TC-FE-COMP-041`…`TC-FE-COMP-080` · `TC-FE-SEC-001`…`TC-FE-SEC-020` · `TC-FE-MCDC-001`…`TC-FE-MCDC-012` · `TC-FE-DF-001`…`TC-FE-DF-012` | ~45 |
| `23-module-frontend-cases-C.md` | `PermissionsService` affordance model, `OrgDataService` caches, shell navigation/group state, per-item `visible()` gates, `HelpService` / `helpTopicForUrl` | `TC-FE-COMP-081`…`TC-FE-COMP-120` · `TC-FE-EP-001`…`TC-FE-EP-015` · `TC-FE-STATE-001`…`TC-FE-STATE-018` | ~40 |
| `23-module-frontend-cases-D.md` | Shared component contracts: `qams-list-stats` metering, `qams-load-more` paging, `qams-status-pill`, `qams-workflow-stepper`, `qams-user-select`, `qams-drawer`, `qams-lov-select`, `qams-csv-import`, `qams-audit-trail`; facade paging/error-surfacing contract | `TC-FE-COMP-121`…`TC-FE-COMP-170` · `TC-FE-BVA-001`…`TC-FE-BVA-025` · `TC-FE-UNIT-001`…`TC-FE-UNIT-040` | ~60 |
| `23-module-frontend-cases-E.md` | i18n / RTL / accessibility: language matrix, `document.dir` sync, dictionary key resolution and fallback, dialog a11y (focus trap, Escape, `aria-modal`), axe surfaces | `TC-FE-RTL-001`…`TC-FE-RTL-025` · `TC-FE-A11Y-001`…`TC-FE-A11Y-030` | ~40 |
| `23-module-frontend-cases-F.md` | End-to-end journeys through the real stack (SPA → JWT → API `:5080`), SPA↔API integration, build/observability/perf gates | `TC-FE-E2E-001`…`TC-FE-E2E-030` · `TC-FE-INT-001`…`TC-FE-INT-020` · `TC-FE-PERF-001`…`TC-FE-PERF-010` · `TC-FE-OBS-001`…`TC-FE-OBS-008` | ~45 |
| **This file** | Exploratory charters only | `TC-FE-EXPL-001`…`TC-FE-EXPL-006` (consumed in §7) | 6 |

Gap IDs consumed by this file: `GAP-FE-001` … `GAP-FE-014` (§8).
Risk IDs: none of `docs/validation/02-Functional-Risk-Assessment.md` were found to cover the SPA layer within this pass, so all risk references in the case batches must be **minted** as `RSK-FE-<NNN>` and declared as minted (conventions §5).

---

## 1. Implementation inventory

### 1.1 Composition root and bootstrap

| Fact | Evidence |
|---|---|
| Standalone bootstrap, no NgModule: `bootstrapApplication(AppComponent, appConfig)`. | `frontend/src/main.ts:5` |
| Zone-based change detection with event coalescing (not zoneless). | `frontend/src/app/app.config.ts:11` |
| Router provided with `withComponentInputBinding()` — routed components bind `:id` directly to signal inputs. | `frontend/src/app/app.config.ts:14` |
| HTTP client uses **XHR** (`withXhr()`), not fetch, and registers exactly two functional interceptors, in this array order: `[authInterceptor, changeReasonInterceptor]`. | `frontend/src/app/app.config.ts:15` |
| `APP_INITIALIZER` calls `AuthService.hydrate()` — one silent refresh at bootstrap (ADR-0009). The factory **always resolves**; a failure just means "logged out". | `frontend/src/app/app.config.ts:19-24`, `core/auth.service.ts:101-105` |
| Root component renders only `<router-outlet />` and runs one effect that mirrors the active language onto `document.documentElement.lang` and `.dir`. | `frontend/src/app/app.component.ts:9,16-20` |
| API base URL is the relative path `'/api'` (same-origin behind the reverse proxy); a single `environment.ts` exists — **there is no `environment.prod.ts` and no `fileReplacements` block** in `angular.json`. | `frontend/src/environments/environment.ts:6`; `frontend/angular.json:30-51` |
| Dev proxy maps `/api` → `http://localhost:5080`. | `frontend/proxy.conf.json` |
| The SPA calls the **unversioned** `/api/...` surface only; no `/api/v1/...` call site was found. | measured across `core/api/*.ts` |

### 1.2 Route inventory (measured)

| Measure | Count |
|---|---|
| `path:` entries in `app.routes.ts` | **86** |
| Entries with a `loadComponent` (lazy) | **83** |
| Entries without a component (`''` grouping node, `''`→`dashboard` redirect, `**`→`''` wildcard) | **3** |
| `:id` detail children | **31** |
| Top-level tenant pages (inside the shell, under `tenantOnlyGuard`) | **47** |
| Platform (control-plane) pages | **1** (`platform/tenants`) |
| Routes outside the shell | **3** (`t/:tenant`, `login`, `security/mfa-setup`) |
| Route-level `data:` / `title:` / resolvers | **0** — none declared (`grep` over `app.routes.ts` returns nothing) |

**Every** routed component is lazy (`loadComponent: () => import(…)`); there is no eagerly-imported feature component. `app.routes.ts:5` exports `routes: Routes`.

### 1.3 Guards — the complete set (3)

| Guard | Definition | Behaviour read in source |
|---|---|---|
| `authGuard` | `core/auth.guard.ts:5-9` | `auth.isAuthenticated() ? true : router.createUrlTree(['/login'])`. Purely synchronous; reads the in-memory session signal. |
| `platformOnlyGuard` | `core/role.guard.ts:11-15` | `perms.isPlatformAdmin() ? true : router.createUrlTree(['/dashboard'])`. |
| `tenantOnlyGuard` | `core/role.guard.ts:17-21` | `perms.isPlatformAdmin() ? router.createUrlTree(['/platform/tenants']) : true`. |

`PermissionsService.isPlatformAdmin` is `computed(() => this.auth.role() === 'PlatformAdmin')` — derived from the **session tier string**, not from fetched privileges, deliberately so the guards resolve synchronously at bootstrap (`core/permissions.service.ts:36-41`).

**There is no permission-based route guard anywhere in the application.** Only the three guards above appear in `canActivate`, at exactly three places: `app.routes.ts:19` (`security/mfa-setup`), `:24` (shell root), `:30` (`platform/tenants`), `:36` (tenant subtree). Permission keys gate **affordances only** (see §1.6).

### 1.4 Interceptor chain (2, functional)

**`authInterceptor`** — `core/auth.interceptor.ts:15-48`
- Clones the outbound request with `Authorization: Bearer <token>` **only when a token exists** (`:40-42`).
- On error: retries **only** when `err.status === 401` **and** the URL is not an auth endpoint (`:23`).
- `isAuthEndpoint` matches the literal substrings `/auth/login`, `/auth/refresh`, `/auth/logout` (`:44-48`). Note that `/auth/me/privileges` and `/auth/me/language` are **not** excluded, so a 401 on those does trigger a refresh.
- On 401: `auth.refresh()` (single-flight, §1.5). If the refresh yields `null`, it navigates to `/login` and rethrows the **original** error (`:29-31`); otherwise it re-issues `next(withBearer(req, token))` — **with the original `req`**, i.e. downstream interceptor mutations are not preserved on the retry (`:33`).

**`changeReasonInterceptor`** — `core/change-reason.interceptor.ts:17-33`
- Header constant `X-Change-Reason` (`:6`).
- Pass-through unless `req.method === 'DELETE'` **and** the header is absent (`:18`).
- Otherwise `from(reasons.request())`; on `null` (cancel or blank) it returns **`EMPTY`** — the observable completes without emitting, so no request is sent (`:26-29`).
- On a reason, clones with `setHeaders: { 'X-Change-Reason': reason }` (`:30`).

Registration order `[authInterceptor, changeReasonInterceptor]` (`app.config.ts:15`) makes `authInterceptor` the **outer** interceptor and `changeReasonInterceptor` the inner one.

### 1.5 `AuthService` — session, token, refresh, idle (`core/auth.service.ts`)

| Concern | Detail | Line |
|---|---|---|
| Session shape | `{ token, role, displayName, tenantId, expiresAtUtc }` in a private `signal<Session \| null>` | `:7-13,29` |
| Token storage | **Memory only** — the token is never written to `localStorage`/`sessionStorage` (ADR-0009) | `:17-25,60-62` |
| Persisted key | `qams.tenant.slug` only — the lab slug, explicitly "not a credential" | `:15,44-56` |
| Slug normalisation | `slug?.trim().toLowerCase() \|\| null`; a null/blank slug **removes** the key | `:48-55` |
| Derived signals | `isAuthenticated`, `displayName`, `role`, `tenantId`, `tenantSlug` | `:34-37,45` |
| Login | `POST /auth/login` body `{ tenantIdentifier, email, password, mfaCode }`, `withCredentials: true`; applies the session **only when** `!res.mfaRequired && res.accessToken` | `:64-74` |
| Refresh single-flight | `refreshInFlight$ ??= …` with `shareReplay(1)` and `finalize` clearing the field; on error it clears the session and emits `null` | `:82-98` |
| Hydrate | Promise that resolves on both `next` and `error` — never rejects | `:101-105` |
| Other auth calls | `POST /auth/mfa/enroll`, `POST /auth/mfa/confirm`, `POST /auth/signature-pin`, `GET/PUT /tenant-settings/mfa-policy`, `POST /auth/change-password` | `:107-134` |
| Logout | `POST /auth/logout` with credentials; clears the session on **both** success and error | `:137-140` |
| Idle lockout | `IDLE_LIMIT_MS = 30 * 60 * 1000`; `startIdleWatch()` listens to `click`, `keydown`, `visibilitychange` on `document` (passive) and on expiry calls `logout()` then `location.assign('/login')` | `:157,161-174` |
| `startIdleWatch()` caller | `ShellComponent` constructor — so it arms once per shell instantiation; listeners are **never removed** | `shell/shell.component.ts:234` |
| `expiresAtUtc` | Stored on the session but **never read anywhere** in the SPA | `:12,148` |

### 1.6 `PermissionsService` — the affordance model (`core/permissions.service.ts`)

- Privileges are fetched from **`GET {apiBaseUrl}/auth/me/privileges`** inside an `effect()` that re-runs whenever the session changes (`:43-64`).
- The effect **short-circuits for platform admins** — `if (this.isPlatformAdmin()) { return; }` — with the comment "The platform surface is tier-gated; no tenant privileges exist" (`:50-52`).
- `catchError(() => of(null))` — a failed privileges fetch is swallowed and leaves `privileges` at `null` (`:56`).
- `granted` = `new Set(privileges()?.permissions ?? [])` (`:28`), so **before the fetch lands, `can()` answers false** for tenant users (documented at `:16-17`).
- `can(key)` returns `this.isPlatformAdmin() || this.granted().has(key)` — a platform admin is granted **every** key client-side (`:68-70`).
- `canAny(...keys)` is `keys.some(k => this.can(k))` (`:73-75`).
- Side effect on load: if `p.preferredLanguage` is exactly `'en' | 'ar' | 'fr'`, it calls `i18n.setLang()` (`:59-62`).
- `saveMyLanguage(lang)` → `PUT {apiBaseUrl}/auth/me/language` body `{ language }`, errors swallowed (`:81-90`).
- Exposed: `roleName` (`:31`), `branchIds` (`:34`), `isPlatformAdmin` (`:41`). `MyPrivileges` contract = `{ roleId, roleName, isPlatformAdmin, permissions[], branchIds[], departmentIds[], preferredLanguage }` (`core/models.ts:377-385`). Note `departmentIds` is returned by the API and typed, but **no `departmentIds` accessor exists on the service**.

**Permission keys referenced by the SPA — 73 distinct** (extracted exhaustively from every `can('…')` / `canAny('…')` literal; 116 `perms.can` call sites across 63 files, plus 7 `canAny` sites):

`access-reviews.view` · `analytical-quality.approve` · `analytical-quality.create` · `analytical-quality.edit` · `analytical-quality.manage` · `analytical-quality.sign` · `audits.create` · `audits.sign` · `changes.approve` · `changes.void` · `competencies.approve` · `competencies.create` · `competencies.edit` · `complaints.approve` · `complaints.edit` · `complaints.void` · `compliance.approve` · `compliance.create` · `compliance.view` · `conflicts.approve` · `conflicts.void` · `documents.approve` · `documents.edit` · `documents.sign` · `documents.view` · `documents.void` · `equipment.void` · `feedback.edit` · `feedback.void` · `monitoring-points.create` · `monitoring-points.edit` · `monitoring-points.void` · `nc.approve` · `nc.view` · `nc.void` · `notifications.manage` · `objectives.create` · `objectives.void` · `org-context.create` · `org-context.void` · `organization.create` · `organization.manage` · `proficiency-testing.approve` · `proficiency-testing.create` · `proficiency-testing.edit` · `proficiency-testing.void` · `quality-policy.approve` · `quality-policy.create` · `quality-policy.view` · `records.void` · `reference-standards.approve` · `reference-standards.create` · `reference-standards.edit` · `reference-standards.void` · `reviews.create` · `reviews.edit` · `reviews.export` · `reviews.void` · `risks.approve` · `risks.void` · `roles.manage` · `roles.view` · `suppliers.approve` · `suppliers.void` · `tasks.create` · `tasks.manage` · `tenant-settings.manage` · `test-authorizations.approve` · `test-authorizations.create` · `test-authorizations.edit` · `test-authorizations.void` · `training.create` · `users.view`

All 73 conform to the code-defined `{module}.{action}` format (conventions §2 → `PermissionCatalog.Key()`). The **Roles & Privileges** screen does not hard-code the catalogue: it fetches `GET /api/roles/catalog` (`core/api/roles-api.service.ts:17-19`) and renders each module through `i18n.t(m.nameKey)` plus a composed `i18n.t('perm.action.' + a)` key (`features/roles/roles.component.ts:118,126`).

### 1.7 Reason-for-change and text-prompt services

**`ChangeReasonService`** (`core/change-reason.service.ts`)
- Default title key `'changeReason.title'` (`:4`); callers may override (`:29-34`).
- `request(titleKey?)` **cancels any in-flight dialog first** (`settle(null)` at `:30`) so a previous caller never hangs, sets the title, opens, and returns a `Promise<string | null>`.
- `confirm(reason)` trims; **a blank/whitespace-only reason resolves `null`, i.e. is treated as a cancel** (`:37-41`).
- `cancel()` resolves `null` (`:44-47`).
- Signals: `open`, `titleKey` (`:20,23`).
- The only caller that passes a custom title is the **legal-hold** flow: `this.reasons.request('arc.placeHold')`, whose reason is then sent in the **POST body** (`features/records/records.component.ts:145-148`), matching conventions §2 ("Place-legal-hold sends its Part-11 reason in the POST body, not the header").

**`TextPromptService`** (`core/text-prompt.service.ts`)
- Options `{ titleKey, labelKey, placeholderKey?, inputType?: 'text' | 'password' }`, defaulting `placeholderKey: ''` and `inputType: 'text'` (`:4-23,49-59`).
- `confirm(value)` treats a blank value as cancel but **returns the untrimmed value** when non-blank (`:62-65`) — unlike `ChangeReasonService`, which returns the trimmed string.
- Sole production caller: the role-privilege grant change, `titleKey: 'roles.reasonTitle'`, `labelKey: 'roles.reasonLabel'`, whose reason is posted in the `setPermissions` body (`features/roles/roles.component.ts:300-309`).

**Both dialogs** (`core/change-reason-dialog.component.ts`, `core/text-prompt-dialog.component.ts`) are hosted **once**, in the shell (`shell/shell.component.ts:131,134`), and implement the same accessibility contract:

| Contract element | change-reason | text-prompt |
|---|---|---|
| `role="dialog"` + `aria-modal="true"` + `aria-labelledby` to the visible `<h3>` | `:22-23` | `:23-24` |
| Focus moves to the input on open; the previously focused element is restored on close | `:72-85` | `:71-84` |
| Escape dismisses (`@HostListener('document:keydown.escape')`) | `:87-90` | `:86-89` |
| Scrim click dismisses; scrim is `aria-hidden="true"` | `:21` | `:22` |
| Confirm disabled while the value trims to empty | `:34` | `:34` |
| Draft value reset to `''` on every open | `:78` | `:78` |
| **No focus trap** — Tab can leave the dialog; no `inert`/`aria-hidden` applied to the background | not present | not present |

### 1.8 i18n and direction (`core/i18n.service.ts`, 1,518 lines)

| Fact | Evidence |
|---|---|
| `type Lang = 'en' \| 'ar' \| 'fr'` — exactly three languages | `:3` |
| `Dict = Record<string, { en: string; ar: string; fr: string }>` — a flat, in-code dictionary | `:5` |
| **1,417 keys**, each with all three translations (`en:`/`ar:`/`fr:` occurrences all measured at 1,417) | `:16-1499` |
| **No duplicate keys** | measured |
| Persistence key `qams.lang` in `localStorage` | `:14` |
| `lang` is a `signal<Lang>` initialised from `restore()` | `:1501` |
| `isRtl = computed(() => this.lang() === 'ar')` — Arabic is the **only** RTL language | `:1502` |
| `setLang()` writes `localStorage` **then** sets the signal | `:1504-1507` |
| `t(key)` returns the translation or, for an unknown key, **the key itself** — a silent fallback, never a blank | `:1509-1512` |
| `restore()` accepts only `'ar'`/`'fr'` from storage; anything else (including a corrupt value) falls back to `'en'` | `:1514-1517` |
| Direction is applied by the **root component effect**, not by the service | `app.component.ts:16-20` |
| RTL styling hooks: `[dir="rtl"] body` font swap to `--nt-font-ar` and `direction: rtl`; `[dir="rtl"] select` background/padding mirroring; drawer slide-in mirrored via `:host-context([dir="rtl"])`; sidebar chevron mirrored via a `.chev.rtl` class bound to `i18n.isRtl()` | `src/styles.css:95-96,165`; `shared/ui/drawer.component.ts:38-39`; `shell/shell.component.ts:96,203` |
| Layout already uses logical properties (`margin-inline-start`, `border-inline-end`, `border-inline-start`, `text-align: start`) rather than physical ones | `shell/shell.component.ts:153,185,194,209,214,298` |
| Language switchers: shell header (`EN` / `ع` / `FR`) and login header (`EN` / `العربية` / `FR`) | `shell/shell.component.ts:396-400`; `features/login/login.component.ts:347-351` |
| Shell switcher **also persists** to the server (`switchLang` → `i18n.setLang` + `perms.saveMyLanguage`); the **login switcher does not** (`i18n.setLang` only) | `shell/shell.component.ts:408-411`; `features/login/login.component.ts:73` |
| Dynamic (non-literal) `i18n.t(...)` call sites: **17** | measured across `features/`, `shell/`, `shared/`, `core/` |
| Keys referenced by a literal `i18n.t('…')` but **absent from the dictionary: 1** — `lov.category`, used three times on `/org-context` | `features/organization/org-context.component.ts:77,102,131`; absent from `core/i18n.service.ts` (only `lov.manageHint` at `:1117` exists under that prefix) → **GAP-FE-005** |
| Dictionary keys with no literal `i18n.t('…')` reference: **149** — most are resolved dynamically (nav labels, help topic titles, feature pills, permission module/action keys), so they are **not** provably dead; this pass did not resolve the dynamic set | measured |

### 1.9 Shell chrome (`shell/shell.component.ts`, 412 lines)

- Persisted UI keys: `qams.sidebar.collapsed` (`'1'`/`'0'`) and `qams.sidebar.groups` (JSON array of open group keys) — `:29-30,247,250,370,378`.
- `restoreGroupState()` parses the stored JSON inside `try/catch`; a corrupt value silently falls back to the default set `['overview','improvement','platform']` (`:383-393`).
- The constructor force-opens the group containing the current URL, matched by `this.router.url.startsWith(i.path)` (`:236-239`).
- **8 navigation groups** for tenant users — `overview`, `improvement`, `docs`, `risk`, `resources`, `people`, `analytical`, `admin` (`:261-349`) — and **1** for platform admins (`platform`, single item `/platform/tenants`, `:254-258`).
- Group item counts: overview 4, improvement 7, docs 3, risk 3, resources 4, people 3, analytical 16, admin 7 = **47 navigable destinations**, exactly the 47 top-level tenant pages in §1.2.
- **5 of the 47** sidebar items are conditionally visible (`grep -c "visible: () =>"` = 5); the other **42** are unconditional. The conditional ones, all in the `admin` group (`:342-346`):
  `/notification-rules` → `notifications.manage` · `/compliance` → `compliance.view` · `/users` → `users.view` · `/roles` → `roles.view` · `/access-reviews` → `access-reviews.view`. The remaining two `admin` entries — `/settings/security` and `/reference-data` — carry no `visible` predicate.
- Groups whose items all filter out are dropped entirely (`:351-353`).
- Header exposes: burger (`aria-label` from `nav.toggleSidebar`), logo, tenant chip (only for non-platform users with a slug, `:53-55`), three quick-link icons (`/dashboard`, `/tasks`, `/notifications`) hidden for platform admins (`:58-74`), the language switcher (`role="group" aria-label="Language"` — a **hard-coded English literal**, `:75`), the initials avatar (max two, `:357-360`), the raw `auth.role()` string, and Sign out.
- `signOut()` calls `auth.logout()` then `router.navigate(['/login'])` (`:402-405`).
- Rail mode: `.nav.rail` at 56px, labels become `title` tooltips, group separators replace headings (`:101-103,189,221-223`).

### 1.10 Shared UI components — 13

| Selector | File | Public contract |
|---|---|---|
| `qams-list-stats` | `shared/ui/list-stats.component.ts` | `stats: readonly ListStat[]` (required, `:116`), `ratioFromFirst: boolean` with a string-tolerant transform (`:123`). `ListStat = { label, value: number\|string, tone: 'blue'\|'teal'\|'green'\|'gold'\|'orange'\|'red'\|'slate', of?: number, link?: string }` (`:6-19`). Metering rules at `:125-146` — see §4.3. |
| `qams-load-more` | `shared/ui/load-more.component.ts` | Inputs `shown`, `total`, `hasMore` (all required), `loading` (default `false`); output `more`. Count line is `i18n.t('common.showingOf')` with `{shown}`/`{total}` replaced, in a `aria-live="polite"` `<p>` (`:16,34-47`). Button rendered **only** when `hasMore()` (`:17`), disabled while `loading()` (`:18`). |
| `qams-status-pill` | `shared/ui/status-pill.component.ts` | `status` (required). Lower-cases then matches: POSITIVE `closed, approved, active, satisfactory, signedoff, sent` → `ok`; VERIFIED `authorized, published, verified` → `teal`; NEGATIVE `rejected, revoked, suspended, outofservice, unsatisfactory, failed, disposed, obsolete` → `danger`; **everything else → `warn`** (`:19-31`). |
| `qams-workflow-stepper` | `shared/ui/workflow-stepper.component.ts` | `steps: readonly string[]`, `current: string`. `activeIndex = max(indexOf(current), 0)` (`:65`); `offPath = !steps.includes(current)` (`:68`); off-path renders a terminal red badge and **claims no progress** (`:19,25-28`). `pretty()`: length ≤ 3 → upper-case; otherwise camel-case split (`:71-74`). |
| `qams-user-select` | `shared/ui/user-select.component.ts` | `ControlValueAccessor`; `multiple` input (default false); `maxTags = 4` then a `+N` counter; `role="listbox"`/`role="option"`/`aria-expanded`/`aria-haspopup`/`aria-selected` (`:17-21,28,56,67-68,135-159`). |
| `qams-drawer` | `shared/ui/drawer.component.ts` | `open` (required), `title` (required), `width` (default `'560px'`); output `closed`. `role="dialog" aria-modal="true"` with `aria-label` from `title` (`:15-19,55-64`). |
| `qams-page-header` | `shared/ui/page-header.component.ts` | `title` (required), `subtitle` (default `''`); resolves its help topic from the router URL via `helpTopicForUrl` (`:65-77`). |
| `qams-page-help` | `shared/ui/page-help.component.ts` | Global popup bound to `HelpService.topic()`; hosted once in the shell (`shell:128`). |
| `qams-lov-select` | `shared/ui/lov-select.component.ts` | `ControlValueAccessor`; `category` (required), `placeholder`; loads via `OrgDataService.lovEntries()` (`:39-49`). |
| `qams-allocation-picker` | `shared/ui/allocation-picker.component.ts` | `branchCtrl` / `departmentCtrl` as required `FormControl<string>` inputs (`:51-53`). |
| `qams-audit-trail` | `shared/ui/audit-trail.component.ts` | `subject` (required); pulls ledger + field-change rows through `ComplianceApiService`, gated by `PermissionsService` (`:104-117`). |
| `qams-csv-import` | `shared/ui/csv-import.component.ts` | `columns` (required), `result`, `busy`; output `import: string[][]`; `skipHeader` defaults **true**; computes `validCount`/`invalidCount` (`:94-121`). |
| `qams-help-body` | `shared/ui/help-body.component.ts` | `topic` (required); renders the workflow progress strip as `role="img"` with an `aria-label` (`:23,91-97`). |

Consumption measured: `qams-list-stats` appears in **29** feature files, of which **24** opt into `ratioFromFirst`. `qams-load-more` appears in **13** feature components: `audits`, `change`, `competency` (list + training queue), `documents`, `equipment`, `nc`, `notifications`, `records`, `review`, `risk`, `supplier`, `tasks`.

### 1.11 Facades — 34, one uniform contract

Every facade follows the same shape, verified in `features/change/change.facade.ts` and spot-checked across the set:

- Private `signal`s exposed through `.asReadonly()`: `list`, `total`, `hasMore`, `selected`, `loading`, `error` (`:18-38`).
- `loadList(filter?)` stores the filter in `lastStatus`, resets `_page` to 1, and **replaces** the list (`:40-49`).
- `loadMore()` is a **no-op while `loading()` or `!hasMore()`**; otherwise it fetches `page + 1` with the same filter and **appends** (`:51-62`).
- `mutate(id, call)` awaits the call then re-fetches the detail (`:97-102`).
- `run(op)` sets `loading` true, clears `error`, `try/catch/finally`, and returns `null` on failure (`:104-115`).
- `describe(err)` returns `err.error.title` for an `HttpErrorResponse`, else `` `Request failed (${err.status}).` ``, else the literal **`'Unexpected error.'`** (`:117-122`). This identical `describe` body appears in every facade inspected (e.g. `features/analytical/qc.facade.ts:83`, `features/audits/audits.facade.ts:93`).

The facades are the only consumers of the API-004 pagination envelope; the SPA reads `page.items`, `page.total`, `page.hasMore`.

### 1.12 Error codes — the honest position

**The frontend defines no domain error codes of its own.** An exhaustive scan for code-shaped literals across `frontend/src/app/**/*.ts` returns exactly three, and only one of them is in production code:

| Code | Where | Behaviour |
|---|---|---|
| `AUTH-101` | `features/login/login.component.ts:480` | The **only** error code the SPA branches on. On a login error whose `err.error.code === 'AUTH-101'`, the component sets `passwordExpired()` and reveals the new-password field instead of showing an error. |
| `CHG-021` | `features/change/change.facade.spec.ts:54` | Test fixture only. |
| `CMP-020` | `features/complaints/complaints.facade.spec.ts:51` | Test fixture only. |

Every other server error reaches the operator as the free-text `problem+json` **`title`**, via `describe()` (§1.11). The SPA never renders the `code` member, never branches on HTTP `403`/`409`/`422`, and never special-cases `CHANGE-REASON-REQUIRED`, `CONCURRENCY-409`, `AUTHZ-*`, or any `SOD-*` code. See **GAP-FE-006** and **GAP-FE-007**.

### 1.13 Domain events, endpoints, persistence, states

- **Domain events:** none. The SPA has no event bus, no SignalR, no WebSocket — consistent with conventions §1 ("Realtime: SignalR NOT PRESENT"). All state changes are request/response, and the facades re-fetch after each mutation (`change.facade.ts:97-102`).
- **Endpoints:** the SPA consumes the API through **44** typed services in `core/api/`. It issues **13 DELETE** calls, all through `changeReasonInterceptor`:
  `archives /{id}/legal-hold` · `carryover /{id}/readings/{readingId}` · `detection-limit /{id}/measurements/{id}` · `instrument-comparability /{id}/readings/{id}` · `interference /{id}/measurements/{id}` · `linearity /{id}/measurements/{id}` · `lot-comparison /{id}/pairs/{id}` · `method-comparison /{id}/pairs/{id}` · `outlier /{id}/points/{id}` · `precision /{id}/measurements/{id}` · `pt-plans /{id}/items/{id}` · `reference-interval /{id}/samples/{id}` · `uncertainty /{id}/components/{id}`.
- **Client-side persistence — the complete set of browser-storage keys (5, all `localStorage`, none a credential):**

| Key | Written by | Value |
|---|---|---|
| `qams.tenant.slug` | `core/auth.service.ts:15,50,52` | Lower-cased lab slug; removed when null |
| `qams.lang` | `core/i18n.service.ts:14,1505` | `'en'` \| `'ar'` \| `'fr'` |
| `qams.sidebar.collapsed` | `shell/shell.component.ts:29,378` | `'1'` \| `'0'` |
| `qams.sidebar.groups` | `shell/shell.component.ts:30,370` | JSON array of open group keys |
| `qams.login.theme` | `features/login/login.component.ts:11,429` | `'dark'` \| `'light'` |

  There are **no** cookies written by the SPA (the `qams_rt` refresh cookie is set by the server, `HttpOnly`), and **no** `sessionStorage` or `IndexedDB` use.
- **States:** the SPA holds no workflow state of its own. It renders backend status strings through `qams-status-pill` and `qams-workflow-stepper`; the client-side state machines that do exist are the session, the two modal dialogs, the pager, and the tenant/platform routing tier — all enumerated in §3.

### 1.14 Help system

- `HELP_TOPICS` registers **42** topics, keyed by top-level route segment (`core/help/help-content.ts:894-897`).
- `helpTopicForUrl(url)` strips query/hash and the leading slash, takes the **first** path segment, and returns the topic or `undefined` (`:904-908`) — so `/nonconformances/{id}` resolves to the nonconformances topic.
- Of the 47 top-level tenant pages, **5 have no registered topic**: `/access-reviews`, `/manual`, `/quality-policy`, `/roles`, `/settings/security`. (`/manual` is the User Manual itself, so four are substantive gaps.) The page-header `?` icon is therefore inert on those pages → **GAP-FE-011**.

### 1.15 Test assets as built

**Unit — Karma/Jasmine, 15 spec files, 76 `it()` blocks** (counted):

| Spec | `it()` |
|---|---|
| `core/api/access-reviews-api.service.spec.ts` | 2 |
| `core/auth.interceptor.spec.ts` | 4 |
| `core/auth.service.spec.ts` | 5 |
| `core/change-reason-dialog.component.spec.ts` | 7 |
| `core/change-reason.interceptor.spec.ts` | 6 |
| `core/i18n.service.spec.ts` | 4 |
| `core/permissions.service.spec.ts` | 6 |
| `core/text-prompt-dialog.component.spec.ts` | 7 |
| `features/change/change.facade.spec.ts` | 4 |
| `features/complaints/complaints.facade.spec.ts` | 4 |
| `shared/ui/list-stats.component.spec.ts` | 9 |
| `shared/ui/load-more.component.spec.ts` | 4 |
| `shared/ui/status-pill.component.spec.ts` | 5 |
| `shared/ui/user-select.component.spec.ts` | 5 |
| `shared/ui/workflow-stepper.component.spec.ts` | 4 |
| **Total** | **76** |

**Coverage shape:** 15 of 194 non-spec TypeScript files carry a spec. **0 of 34 facades except two** (`change`, `complaints`), **0 of 100 components except the 2 core dialogs and 5 shared-UI controls**, **0 of the 3 guards**, **0 of 44 API services except `access-reviews`**, and **no spec at all** for `ShellComponent`, `LoginComponent`, `OrgDataService`, `HelpService`, `ChangeReasonService`, `TextPromptService`, or `AppComponent` (the direction effect). See **GAP-FE-012**.

**E2E — Playwright, 3 specs, 6 tests:**

| Spec | Tests | Needs API? |
|---|---|---|
| `e2e/auth.spec.ts` | 2 — sign-in form renders; unauthenticated `/nonconformances` redirects to `/login` | No |
| `e2e/a11y.spec.ts` | 2 — axe scan of `/login` and of `/t/demo-lab`, failing only on `serious`/`critical` | No |
| `e2e/regulated-workflow.spec.ts` | 2 — tenant admin signs in and reaches `/nonconformances`; `qams-load-more .count` matches `/Showing\s+\d+\s+of\s+\d+/` | **Yes** (demo seed) |

`playwright.config.ts`: `testDir './e2e'`, timeout 30 s, expect timeout 10 s, `fullyParallel: false`, `workers: 1`, `forbidOnly` and `retries: 1` under CI, `baseURL` from `E2E_BASE_URL` else `http://localhost:4200`, `trace: 'on-first-retry'`, **chromium only**, `webServer` runs `npm start -- --port 4200` with `reuseExistingServer: true` (`:9-29`).

`e2e/regulated-workflow.spec.ts:56-61` documents a **deliberately omitted** journey: the read-only ExternalAuditor affordance, because the dev seed provides no such login.

**CI `frontend` job** (`.github/workflows/ci.yml`): Node **24** → `npm ci` → npm SCA against `.github/npm-audit-allowlist.txt` (fails on un-allowlisted high/critical) → `npm run test:ci` (headless Chrome) → `npm run build` (AOT, production) → `npx playwright install --with-deps chromium` → **`npx playwright test e2e/auth.spec.ts e2e/a11y.spec.ts`**. The workflow spec is **not** in the gate — only **4 of the 6** e2e tests run in CI.

**Build budgets** (`angular.json:31-45`): initial bundle warn 1 MB / error 2 MB; any component style warn 8 kB / error 16 kB. Production default config with `outputHashing: "all"`.

---

## 2. Divergences from the commissioning brief

| # | What the brief assumes | What the code does | Evidence (`file:line`) | Gap |
|---|---|---|---|---|
| 1 | Angular **18** standalone | Angular **22.0.8** (upgraded 18→22 at v1.49.0), TypeScript 6.0.3, zone.js 0.15.1. The `package.json` `description` field still reads "NT.QAMS Angular 18 frontend", and `CLAUDE.md` §1 still says "Angular 18" | `frontend/package.json:4,15-25,41`; `NT.QAMS/CLAUDE.md` §1 | GAP-FE-001 |
| 2 | **Tailwind CSS** | No Tailwind dependency, no config, no utility classes; hand-authored `--nt-*` design tokens in `src/styles.css` and per-component `styles: []` | `frontend/package.json:14-42`; `src/styles.css` | (conventions §1 — already recorded) |
| 3 | **ngx-translate** for i18n | Custom `I18nService` with a 1,417-key in-code trilingual dictionary and a `t()` that falls back to the key | `core/i18n.service.ts:5,1509-1512` | (conventions §1 — already recorded) |
| 4 | Role-based UI gating (`UserRole` enum drives what is shown) | Two orthogonal mechanisms: the **tier** string `'PlatformAdmin'` drives the two route guards, and 73 `{module}.{action}` permission keys drive affordances. No screen keys off `TenantAdmin`/`QualityManager`/`Analyst`/`ExternalAuditor` | `core/role.guard.ts:11-21`; `core/permissions.service.ts:41,68-70` | GAP-FE-002 |
| 5 | Privileges gate access to screens | Privileges gate **buttons and sidebar items only**. Every one of the 47 tenant routes is reachable by typing its URL; the server then answers 403 and the page renders empty or errored | `app.routes.ts:34-413` (no permission guard); `shell/shell.component.ts:342-346` | GAP-FE-003 |
| 6 | Reason-for-change is captured for regulated changes generally | The interceptor gates **only** HTTP `DELETE` (13 endpoints). Two other reason-bearing flows carry their reason in the request **body** and bypass the interceptor entirely: legal hold and role-privilege change | `core/change-reason.interceptor.ts:18`; `features/records/records.component.ts:145-148`; `features/roles/roles.component.ts:300-309` | GAP-FE-004 |
| 7 | "4-digit signature PIN" | The SPA's `setPin(pin: string)` applies **no length, digit, or format validation** whatsoever before POSTing to `/auth/signature-pin`; conventions §2 records that no digit-length constraint exists in the domain either | `core/auth.service.ts:115-117` | GAP-FE-008 |
| 8 | "All active accounts require MFA" | The SPA reveals the MFA field only when the server answers `mfaRequired`, and routes to `/security/mfa-setup` only when the server answers `mfaEnrollmentRequired` — i.e. it faithfully implements the per-tenant **optional** policy | `features/login/login.component.ts:466-473`; `core/auth.service.ts:120-127` | (conventions §2 — already recorded) |
| 9 | Structured domain error codes surface to the operator | Only `AUTH-101` is branched on. Every other code — including `CHANGE-REASON-REQUIRED`, `CONCURRENCY-409`, all `AUTHZ-*`/403s and all `SOD-*`/422s — is flattened to the `problem+json` `title` string, or to the literal `'Unexpected error.'` | `features/login/login.component.ts:480`; `features/change/change.facade.ts:117-122` | GAP-FE-006, GAP-FE-007 |
| 10 | Levey-Jennings is a backend capability | Conventions §2 is correct: L-J is a **frontend SVG chart** with no chart library, colouring points by their stored Westgard outcome | `features/analytical/levey-jennings-chart.component.ts:13-47`; `features/analytical/qc-profile-detail.component.ts:12,15-16` | (conventions §2 — already recorded) |
| 11 | Session timeout is a server concern | The SPA additionally enforces a **30-minute client idle lockout** that ends in a full-page `location.assign('/login')`. This is longer than the 15-minute access token, and the listeners are added per shell instantiation and never removed | `core/auth.service.ts:157,161-174`; `shell/shell.component.ts:234` | GAP-FE-009 |
| 12 | The e2e suite proves the regulated workflow in CI | CI runs only `auth.spec.ts` + `a11y.spec.ts` (4 of 6 tests). The tenant-scoped workflow spec never runs in the gate, and the auditor read-only journey is documented as deliberately absent | `.github/workflows/ci.yml` (frontend job, final step); `e2e/regulated-workflow.spec.ts:56-61` | GAP-FE-013 |
| 13 | The a11y gate covers the application | axe scans **only** the two unauthenticated sign-in surfaces. No authenticated screen, no dialog, no RTL rendering, and no data table has ever been scanned | `e2e/a11y.spec.ts:15-37` | GAP-FE-010 |

---

## 3. State-transition matrices

### 3.1 Session / routing tier

States: `Anonymous` · `Hydrating` · `TenantSession` · `PlatformSession` · `MfaChallenge` · `MfaEnrollmentPending` · `PasswordExpired`.

| From | Trigger | To | Landing route | Evidence |
|---|---|---|---|---|
| (bootstrap) | `APP_INITIALIZER` → `auth.hydrate()` | `Hydrating` | — | `app.config.ts:19-24` |
| `Hydrating` | `POST /auth/refresh` 2xx, `role !== 'PlatformAdmin'` | `TenantSession` | requested URL | `auth.service.ts:82-98` |
| `Hydrating` | `POST /auth/refresh` 2xx, `role === 'PlatformAdmin'` | `PlatformSession` | requested URL, then `tenantOnlyGuard` bounces to `/platform/tenants` | `role.guard.ts:20` |
| `Hydrating` | refresh fails (any error) | `Anonymous` | `/login` via `authGuard` | `auth.service.ts:90-93`; `auth.guard.ts:8` |
| `Anonymous` | navigate `/t/{slug}` | `Anonymous` (slug pinned) | `/login` with `replaceUrl: true` | `tenant-entry.component.ts:23-30` |
| `TenantSession`/`PlatformSession` | navigate `/t/{slug}` | `Anonymous` | `/login` — **the existing session is logged out** | `tenant-entry.component.ts:26-28` |
| `Anonymous` | login 2xx, `mfaRequired: true` | `MfaChallenge` | stays on `/login`, MFA field revealed | `login.component.ts:466-469`; session **not** applied (`auth.service.ts:70-72`) |
| `MfaChallenge` | login 2xx with a valid code | `TenantSession` \| `PlatformSession` | `/dashboard` or `/platform/tenants` | `login.component.ts:461,476` |
| `Anonymous`/`MfaChallenge` | login 2xx, `mfaEnrollmentRequired: true` | `MfaEnrollmentPending` | `/security/mfa-setup` (outside the shell) | `login.component.ts:471-473`; `app.routes.ts:18-21` |
| `MfaEnrollmentPending` | `POST /auth/mfa/confirm` succeeds | `MfaEnrollmentPending` with `done() === true` | stays on `/security/mfa-setup`; **no automatic navigation** | `security/mfa-setup.component.ts:106-116` |
| `MfaEnrollmentPending`, `done` | operator clicks the go-to-login control | `Anonymous` | `/login` — the component calls `auth.logout()` first, so the next sign-in mints a full session or challenges for the code | `security/mfa-setup.component.ts:120-124` |
| `Anonymous` | login error, `err.error.code === 'AUTH-101'` | `PasswordExpired` | stays on `/login`, new-password field revealed | `login.component.ts:480-482` |
| `PasswordExpired` | `changePassword()` 2xx | re-enters `submit()` with the new password | as per login outcome | `login.component.ts:444-459` |
| `TenantSession` | click "Switch to platform" on `/login` | slug cleared, `Anonymous` context | `/login` (platform variant) | `login.component.ts:433-437` |
| any authenticated | Sign out | `Anonymous` | `/login` | `shell.component.ts:402-405` |
| any authenticated | 30 min without `click`/`keydown`/`visibilitychange` | `Anonymous` | full page load of `/login` | `auth.service.ts:161-174` |
| any authenticated | any 401 on a non-auth endpoint | single-flight refresh → retry, **or** `Anonymous` + `/login` | — | `auth.interceptor.ts:23-35` |

### 3.2 `ChangeReasonService` dialog

States: `Closed` · `Open` · `Settled`.

| From | Trigger | To | Promise resolves | Evidence |
|---|---|---|---|---|
| `Closed` | `request(titleKey?)` | `Open` | — | `change-reason.service.ts:29-34` |
| `Open` | **`request()` again** (second caller) | `Open` (title replaced) | the **first** caller receives `null` | `:30` (`settle(null)` before re-opening) |
| `Open` | `confirm(reason)`, `reason.trim() !== ''` | `Closed` | trimmed reason | `:37-41` |
| `Open` | `confirm(reason)`, `reason.trim() === ''` | `Closed` | `null` | `:39` |
| `Open` | `cancel()` (button, scrim, Escape) | `Closed` | `null` | `:44-47`; dialog `:21,37,87-90` |

`TextPromptService` has the identical machine with one difference: `confirm` resolves the **untrimmed** value when non-blank (`text-prompt.service.ts:62-65`).

### 3.3 Facade list/pager

States: `Idle` · `Loading` · `Loaded(page=n)` · `Exhausted` · `Errored`.

| From | Trigger | To | Effect | Evidence |
|---|---|---|---|---|
| any | `loadList(filter?)` | `Loading` → `Loaded(1)` | `lastStatus = filter`; `_page = 1`; list **replaced**; `total`/`hasMore` from the envelope | `change.facade.ts:40-49` |
| `Loaded(n)`, `hasMore` | `loadMore()` | `Loading` → `Loaded(n+1)` | list **appended**; `total`/`hasMore` refreshed | `:51-62` |
| `Loading` | `loadMore()` | no change | guarded no-op | `:53` |
| `Loaded(n)`, `!hasMore` | `loadMore()` | no change (`Exhausted`) | guarded no-op | `:53` |
| any | mutation succeeds | `Loaded` | `selected` re-fetched by id | `:97-102` |
| any | any thrown error | `Errored` | `error` set from `problem+json` `title`, or `Request failed (nnn).`, or `Unexpected error.`; the operation returns `null` | `:104-122` |
| `Errored` | next `loadList`/`loadMore`/mutation | `Loading` | `error` cleared first | `:106` |

**Note (verified, not inferred):** when a DELETE is cancelled in the reason dialog, `changeReasonInterceptor` returns `EMPTY`, and the facade's `await firstValueFrom(call())` therefore **rejects with `EmptyError`**, which is not an `HttpErrorResponse`, so `describe()` falls through to `'Unexpected error.'` and the facade enters `Errored`. `change-reason.interceptor.ts:26-29` + `change.facade.ts:99,117-122`. → **GAP-FE-014**.

### 3.4 Sidebar

| From | Trigger | To | Persistence | Evidence |
|---|---|---|---|---|
| expanded | burger click | rail (56px) | `qams.sidebar.collapsed = '1'` | `shell.component.ts:375-381` |
| rail | burger click | expanded | `'0'` | same |
| group closed | group-head click | group open | `qams.sidebar.groups` rewritten | `:366-373` |
| group open | group-head click | group closed | same | same |
| first visit | — | `overview`, `improvement`, `platform` open | default set | `:391-392` |
| corrupt stored JSON | shell construction | default set, silently | `try/catch` | `:386-388` |
| any | shell construction with an active URL | the matching group is force-opened | not persisted | `:236-239` |
| rail mode | — | all groups render their items regardless of open state | — | `:104` |

---

## 4. Decision tables

### 4.1 Route table — path → component → guards → permission → lazy?

All rows are lazy (`loadComponent`). "Required permission" is the key that hides the **sidebar entry**; it is **not** enforced on the route (see GAP-FE-003). `—` means no gate.

**Outside the shell**

| Path | Component | Guards | Permission | Lazy |
|---|---|---|---|---|
| `t/:tenant` | `TenantEntryComponent` | — | — | ✔ |
| `login` | `LoginComponent` | — | — | ✔ |
| `security/mfa-setup` | `MfaSetupComponent` | `authGuard` | — | ✔ |
| `**` | — (`redirectTo: ''`) | — | — | n/a |

**Shell root** — `''` → `ShellComponent`, `canActivate: [authGuard]` (`app.routes.ts:23-25`)

| Path | Component | Guards | Permission | Lazy |
|---|---|---|---|---|
| `platform/tenants` | `TenantsComponent` | `authGuard` + `platformOnlyGuard` | — (tier-gated) | ✔ |

**Tenant subtree** — `''` under `canActivate: [tenantOnlyGuard]` (`app.routes.ts:35-36`); `''` redirects to `dashboard` (`:38`). Every row below inherits `authGuard` + `tenantOnlyGuard`.

| Path | Component | Detail child | Sidebar permission | Help topic |
|---|---|---|---|---|
| `dashboard` | `DashboardComponent` | — | — | ✔ |
| `settings/security` | `SecuritySettingsComponent` | — | — | **✘** |
| `manual` | `ManualComponent` | — | — | n/a (is the manual) |
| `nonconformances` | `NcListComponent` | `NcDetailComponent` | — | ✔ |
| `documents` | `DocumentListComponent` | `DocumentDetailComponent` | — | ✔ |
| `quality-policy` | `QualityPolicyComponent` | — | — | **✘** |
| `quality-objectives` | `ObjectiveListComponent` | `ObjectiveDetailComponent` | — | ✔ |
| `feedback` | `FeedbackListComponent` | `FeedbackDetailComponent` | — | ✔ |
| `complaints` | `ComplaintListComponent` | `ComplaintDetailComponent` | — | ✔ |
| `audits` | `AuditListComponent` | `AuditDetailComponent` | — | ✔ |
| `equipment` | `EquipmentListComponent` | `EquipmentDetailComponent` | — | ✔ |
| `monitoring` | `MonitoringListComponent` | `MonitoringDetailComponent` | — | ✔ |
| `reference-standards` | `StandardsListComponent` | `StandardsDetailComponent` | — | ✔ |
| `competencies` | `CompetencyListComponent` | `CompetencyDetailComponent` | — | ✔ |
| `authorizations` | `AuthorizationMatrixComponent` | `AuthorizationDetailComponent` | — | ✔ |
| `training` | `TrainingQueueComponent` | — | — | ✔ |
| `risks` | `RiskListComponent` | `RiskDetailComponent` | — | ✔ |
| `conflicts` | `ConflictListComponent` | `ConflictDetailComponent` | — | ✔ |
| `org-context` | `OrgContextComponent` | — | — | ✔ |
| `changes` | `ChangeListComponent` | `ChangeDetailComponent` | — | ✔ |
| `management-reviews` | `ReviewListComponent` | `ReviewDetailComponent` | — | ✔ |
| `qc` | `QcProfilesComponent` | `QcProfileDetailComponent` | — | ✔ |
| `validation-studies` | `StudyListComponent` | `StudyDetailComponent` | — | ✔ |
| `method-comparisons` | `MethodComparisonListComponent` | `MethodComparisonDetailComponent` | — | ✔ |
| `linearity-studies` | `LinearityListComponent` | `LinearityDetailComponent` | — | ✔ |
| `detection-limits` | `DetectionLimitListComponent` | `DetectionLimitDetailComponent` | — | ✔ |
| `reference-intervals` | `ReferenceIntervalListComponent` | `ReferenceIntervalDetailComponent` | — | ✔ |
| `sigma-metrics` | `SigmaListComponent` | `SigmaDetailComponent` | — | ✔ |
| `precision-studies` | `PrecisionListComponent` | `PrecisionDetailComponent` | — | ✔ |
| `outlier-screenings` | `OutlierListComponent` | `OutlierDetailComponent` | — | ✔ |
| `carryover-studies` | `CarryoverListComponent` | `CarryoverDetailComponent` | — | ✔ |
| `lot-comparisons` | `LotComparisonListComponent` | `LotComparisonDetailComponent` | — | ✔ |
| `interference-studies` | `InterferenceListComponent` | `InterferenceDetailComponent` | — | ✔ |
| `instrument-comparabilities` | `InstrumentComparabilityListComponent` | `InstrumentComparabilityDetailComponent` | — | ✔ |
| `uncertainty` | `UncertaintyListComponent` | `UncertaintyDetailComponent` | — | ✔ |
| `pt-plans` | `PtPlanListComponent` | `PtPlanDetailComponent` | — | ✔ |
| `proficiency-tests` | `PtListComponent` | — | — | ✔ |
| `suppliers` | `SupplierListComponent` | `SupplierDetailComponent` | — | ✔ |
| `tasks` | `TasksComponent` | — | — | ✔ |
| `records` | `RecordsComponent` | — | — | ✔ |
| `users` | `UsersComponent` | — | `users.view` | ✔ |
| `roles` | `RolesComponent` | — | `roles.view` | **✘** |
| `access-reviews` | `AccessReviewsComponent` | — | `access-reviews.view` | **✘** |
| `notifications` | `NotificationsComponent` | — | — | ✔ |
| `reference-data` | `ReferenceDataComponent` | — | — | ✔ |
| `compliance` | `ComplianceComponent` | — | `compliance.view` | ✔ |
| `notification-rules` | `NotificationAdminComponent` | — | `notifications.manage` | ✔ |

Row count: 47 top-level + 31 detail children = 78 tenant routes; + 1 platform + 4 outside/wildcard = **86** `path:` entries, reconciling with §1.2.

### 4.2 Interceptor chain — order and conditions

Registration order `[authInterceptor, changeReasonInterceptor]` (`app.config.ts:15`). Angular applies the array left-to-right on the **outbound** leg, so:

```
request  →  authInterceptor  →  changeReasonInterceptor  →  backend
response ←  authInterceptor  ←  changeReasonInterceptor  ←  backend
```

| # | Request shape | authInterceptor does | changeReasonInterceptor does | Net outcome |
|---|---|---|---|---|
| 1 | `GET /api/nc`, token present | adds `Authorization: Bearer …` (`:41`) | pass-through, method ≠ DELETE (`:18`) | request sent with bearer |
| 2 | `GET /api/auth/workspace/{slug}`, no token | no header (`token === null`, `:41`) | pass-through | anonymous request |
| 3 | `DELETE /api/uncertainty/{id}/components/{cid}`, no header | adds bearer | opens the dialog; on a reason, clones with `X-Change-Reason` (`:30`) | request sent with both headers |
| 4 | same, operator **cancels** or enters blank | adds bearer | returns `EMPTY` (`:27`) | **no request sent**; the caller's `firstValueFrom` rejects with `EmptyError` → facade shows `'Unexpected error.'` (GAP-FE-014) |
| 5 | `DELETE …` with `X-Change-Reason` already set by the caller | adds bearer | pass-through (`:18`) | no dialog; no production call site does this |
| 6 | any request → **401**, URL not `/auth/(login\|refresh\|logout)` | one single-flight `refresh()`; on a token, re-issues `next(withBearer(req, token))` with the **original** `req` (`:33`) | re-entered with the original request | see rows 7–8 |
| 7 | row 6 where the original was a **DELETE** | as above | the retried DELETE has **no** `X-Change-Reason` again → the dialog **re-opens** | the operator is asked for the reason **twice** (GAP-FE-014) |
| 8 | row 6 where `refresh()` yields `null` | `router.navigate(['/login'])` then rethrows the **original 401** (`:29-31`) | — | session cleared, operator on `/login` |
| 9 | `POST /api/auth/login` → 401 | `isAuthEndpoint` true → rethrow, **no refresh** (`:23,45`) | pass-through | login error surfaces to the form |
| 10 | `POST /api/auth/refresh` → 401 | `isAuthEndpoint` true → rethrow | pass-through | no recursion |
| 11 | `GET /api/auth/me/privileges` → 401 | **not** an auth endpoint → refresh + retry | pass-through | privileges re-fetched after refresh |
| 12 | any request → 403 / 409 / 422 / 500 | not 401 → rethrow (`:23`) | pass-through | facade `describe()` renders `problem+json` `title` |

### 4.3 `qams-list-stats` — metering rules (`list-stats.component.ts:125-146`)

A tile draws a meter **only** when every condition holds:

| Condition | Source |
|---|---|
| `typeof s.value === 'number'` | `:136` |
| a denominator `whole` is resolved and `!== null` | `:133,137` |
| `Number.isFinite(whole)` | `:137` |
| `whole > 0` | `:137` |
| `s.value >= 0` | `:138` |
| `s.value <= whole` | `:138` |

Denominator resolution (`:128-133`, precedence top-down):

| # | Case | `whole` |
|---|---|---|
| 1 | `s.of` supplied | `s.of` — **always wins**, even over `ratioFromFirst` |
| 2 | `ratioFromFirst` on **and** `index === 0` | `null` — the total tile never meters itself |
| 3 | `ratioFromFirst` on, `index > 0`, `stats[0].value` is a number | `stats[0].value` |
| 4 | `ratioFromFirst` off and no `of` | `null` — no meter (the documented "no honest denominator" case) |
| 5 | `ratioFromFirst` on but `stats[0].value` is a string | `null` for every tile |

`percent = Math.round((value / whole) * 100)` (`:143`); the meter is `role="img"` with `aria-label` = `"<label>: <value> of <whole>"` (`:70`); the caption reads `"<value> / <whole>"` (`:73`). Rendering: a tile with `link` is an `<a routerLink>`, otherwise a `<div>` (`:54-62`); a tile whose value is exactly `0` gets `.zero`, which greys the value and the meter (`:55,59,109-111`).

**Boundary cases for the case authors:** `value === 0` with `whole > 0` → meter drawn at 0 % **and** `.zero` applied; `value === whole` → 100 %; `value > whole` → **no meter** (refused, not clamped); `value < 0` → no meter; `whole === 0` → no meter; non-numeric `value` → no meter.

### 4.4 `qams-load-more` — paging contract (`load-more.component.ts`)

| Inputs | Count line | Button |
|---|---|---|
| `hasMore = false`, any `loading` | rendered (`aria-live="polite"`) | **absent from the DOM** (`:17`) |
| `hasMore = true`, `loading = false` | rendered | rendered, enabled; click emits `more` (`:18`) |
| `hasMore = true`, `loading = true` | rendered | rendered, `disabled` (`:18`) |

The count text is `i18n.t('common.showingOf')` with the literal placeholders `{shown}` and `{total}` string-replaced (`:45-47`) — **one occurrence each**; a dictionary entry repeating a placeholder would only substitute the first. The facade contract behind it: `loadMore()` is a no-op while `loading()` or `!hasMore()` (`change.facade.ts:53`), so a double-click cannot double-page.

### 4.5 `qams-status-pill` — tone decision table (`status-pill.component.ts:19-31`)

| Lower-cased status | Tone |
|---|---|
| `closed`, `approved`, `active`, `satisfactory`, `signedoff`, `sent` | `ok` (green) |
| `authorized`, `published`, `verified` | `teal` |
| `rejected`, `revoked`, `suspended`, `outofservice`, `unsatisfactory`, `failed`, `disposed`, `obsolete` | `danger` (red) |
| **anything else, including an unknown or misspelled backend enum** | `warn` (amber) |

Matching is exact after `toLowerCase()`; there is no substring or prefix matching. A new backend state therefore renders amber silently — deliberate per the class doc, but a fact the case authors must not read as "in progress" without checking the domain.

### 4.6 i18n language / direction matrix

| `lang` | `isRtl` | `documentElement.lang` | `documentElement.dir` | Body font | Persisted as |
|---|---|---|---|---|---|
| `en` (default) | `false` | `en` | `ltr` | default | `qams.lang = 'en'` |
| `ar` | `true` | `ar` | `rtl` | `var(--nt-font-ar)` | `qams.lang = 'ar'` |
| `fr` | `false` | `fr` | `ltr` | default | `qams.lang = 'fr'` |

Evidence: `i18n.service.ts:1502,1504-1507`; `app.component.ts:16-20`; `src/styles.css:95-96`.

**Resolution / precedence at startup and thereafter**

| # | Situation | Active language | Source |
|---|---|---|---|
| 1 | No `qams.lang` in storage | `en` | `i18n.service.ts:1516` |
| 2 | `qams.lang` is `'ar'` or `'fr'` | that value | `:1516` |
| 3 | `qams.lang` is any other string (corrupt, `'de'`, `''`) | `en` | `:1516` |
| 4 | Tenant user signs in and `MyPrivileges.preferredLanguage` ∈ {`en`,`ar`,`fr`} | **overrides** the stored value and rewrites storage | `permissions.service.ts:59-62` |
| 5 | `preferredLanguage` is `null` or any other string | stored value retained | `:60` |
| 6 | Platform admin signs in | privileges are never fetched, so **no override** ever occurs | `permissions.service.ts:50-52` |
| 7 | Switcher used **inside** the shell | applied locally **and** `PUT /auth/me/language` | `shell.component.ts:408-411` |
| 8 | Switcher used **on the login page** | applied locally only — no server persistence (the user is not yet authenticated) | `login.component.ts:73` |

**Key resolution**

| # | Key state | Rendered |
|---|---|---|
| 1 | Key present | the `en`/`ar`/`fr` string for the active language |
| 2 | Key absent | **the key string itself** (e.g. the literal `lov.category`) — `i18n.service.ts:1511` |
| 3 | Composed key (`'perm.action.' + a`) present | translation | `roles.component.ts:126` |
| 4 | Hard-coded English literal outside the dictionary | untranslated in every language — one instance found: `aria-label="Language"` on the shell switcher | `shell.component.ts:75` |

**RTL-mirrored elements verified in source:** document direction and body font (`styles.css:95-96`), `<select>` arrow position and padding (`styles.css:165`), drawer slide direction (`drawer.component.ts:38-39`), sidebar group chevron (`shell.component.ts:96,203`), and the Arabic-only `dir="rtl"` on the reference-data Arabic name input (`reference-data.component.ts:195`). Everything else relies on CSS logical properties.

---

## 6. UAT scenarios (Gherkin)

Business-readable, executed by a laboratory reviewer against the running SPA (`localhost:4200`) with the API on `:5080`. `Result` for all of these remains **Not Run** — this package is authored, not executed.

```gherkin
Feature: Signing in through a laboratory's own address

  Scenario: UAT-FE-01 — the lab's door names the lab
    Given I open "http://localhost:4200/t/demo-lab"
    When the sign-in page appears
    Then the workspace pill shows the laboratory's real name resolved from the server
    And the sign-in form asks only for my email and password
    And there is no field anywhere on the form for choosing a laboratory
    And a "Sign in to the administration portal" link is offered as a separate choice

  Scenario: UAT-FE-02 — a second laboratory's door ends the first session
    Given I am signed in to "demo-lab"
    When I open the address of a different laboratory
    Then my session for the first laboratory is ended before the sign-in page appears
    And the browser is now pinned to the second laboratory
```

```gherkin
Feature: Only the buttons I am entitled to use

  Scenario: UAT-FE-03 — the sidebar reflects my privileges
    Given I am signed in as a laboratory user whose role does not grant "users.view"
    When the application shell loads
    Then no "Users" entry appears in the Administration group
    And no "Roles & Privileges" entry appears unless my role grants "roles.view"

  Scenario: UAT-FE-04 — an entitlement I do not hold is not offered
    Given I am signed in as a laboratory user whose role does not grant "nc.approve"
    When I open a nonconformance that is awaiting approval
    Then the approval control is not offered to me
    And the record's current status and history remain fully readable
```

```gherkin
Feature: Recording why regulated evidence was removed

  Scenario: UAT-FE-05 — a deletion cannot proceed without a stated reason
    Given I am signed in and viewing an uncertainty budget with a contributing component
    When I choose to remove that component
    Then a modal appears asking me to state the reason for the change
    And the confirm control stays unavailable until I have typed a non-blank reason
    And pressing Escape closes the modal and nothing is removed

  Scenario: UAT-FE-06 — placing a legal hold states its justification
    Given I am signed in and viewing an archived record
    When I place a legal hold on it
    Then I am asked for the litigation or investigation reference before the hold is applied
    And declining to give one leaves the record unchanged
```

```gherkin
Feature: Reading the system in my own language

  Scenario: UAT-FE-07 — Arabic turns the whole interface right-to-left
    Given I am signed in to a laboratory workspace
    When I choose Arabic from the language switcher in the header
    Then the entire page reads right-to-left
    And the sidebar sits on the right with its group arrows mirrored
    And my choice is remembered for the next time I sign in, on any device

  Scenario: UAT-FE-08 — French leaves the layout left-to-right
    Given I have chosen Arabic
    When I switch to French
    Then the page returns to a left-to-right layout
    And all navigation, headings and buttons read in French
```

```gherkin
Feature: Working through long registers

  Scenario: UAT-FE-09 — a register tells me how much of it I am seeing
    Given a nonconformance register holding more records than one page
    When I open it
    Then a line beneath the list states how many records are shown out of the total
    And a "Load more" control is offered
    When I use it
    Then the next page is added below the records already shown, and the count line updates
    And when every record is shown, the "Load more" control is no longer offered
```

```gherkin
Feature: A working session that does not expire under my hands

  Scenario: UAT-FE-10 — a routine token renewal is invisible
    Given I have been reading a long document for longer than the access token lives
    When I next act on the page
    Then the action completes normally
    And I am not returned to the sign-in page

  Scenario: UAT-FE-11 — an abandoned workstation locks itself
    Given I am signed in and leave the workstation untouched for thirty minutes
    When I return
    Then the application has signed me out and shows the sign-in page
```

---

## 7. Exploratory charters

Timeboxed session-based charters (SBTM). Each states the target, the resources, and what evidence to bring back. None is a substitute for a scripted case.

**`TC-FE-EXPL-001` — Direct-URL reachability of privilege-gated screens**
*Explore* every one of the 47 tenant routes *with* a role that lacks the corresponding permission key, *to discover* what a user actually sees when the sidebar entry is hidden but the URL is typed.
Resources: `app.routes.ts` (no permission guard anywhere), `shell.component.ts:342-346`, browser address bar, DevTools network panel.
Report: for each of the five gated screens (`/users`, `/roles`, `/access-reviews`, `/compliance`, `/notification-rules`) whether the page renders empty, renders an error, renders partial data, or crashes; the HTTP status of every request it fires; and whether any privileged data leaks into the DOM before the 403 lands. Timebox 90 min. Feeds **GAP-FE-003**.

**`TC-FE-EXPL-002` — The 401-during-DELETE double prompt**
*Explore* the interaction of `authInterceptor`'s retry with `changeReasonInterceptor`, *with* an access token forced to expire mid-flow, *to discover* how many times the operator is asked for a Part 11 reason and what the audit ledger ends up recording.
Resources: `auth.interceptor.ts:33`, `change-reason.interceptor.ts:18,30`, a DELETE endpoint from §1.13, `SELECT * FROM audit.field_change ORDER BY occurred_at_utc DESC` with `set_config('app.bypass_rls','on',false)`.
Report: prompt count, whether the two reasons can differ, which reason reaches the ledger, and whether a request is ever sent without the header. Timebox 90 min. Feeds **GAP-FE-014**.

**`TC-FE-EXPL-003` — Arabic RTL sweep of the data-dense screens**
*Explore* the register and detail screens that carry wide tables, SVG charts and steppers — `/qc` (Levey-Jennings), `/method-comparisons`, `/sigma-metrics`, `/records`, `/roles` (the privilege matrix), `/authorizations` — *in Arabic*, *to discover* mirroring defects that the logical-property CSS does not cover.
Resources: `styles.css:95-96,165`, `drawer.component.ts:38-39`, `shell.component.ts:203`, `levey-jennings-chart.component.ts`, browser zoom 100 %/200 %.
Report: screenshots of anything that reads left-to-right inside an RTL page, any horizontal scrollbar, any clipped text, any chart axis or stepper connector that did not mirror, and any untranslated English string. Timebox 120 min.

**`TC-FE-EXPL-004` — Modal accessibility under keyboard-only operation**
*Explore* the change-reason dialog, the text-prompt dialog and `qams-drawer`, *with* keyboard only and a screen reader (NVDA or Narrator), *to discover* whether focus can escape the modal and what is announced.
Resources: `change-reason-dialog.component.ts:72-90`, `text-prompt-dialog.component.ts:71-89`, `drawer.component.ts:15-19`.
Report: whether Tab/Shift-Tab reaches controls behind the scrim (no focus trap was found in source), whether background content is announced, whether focus returns to the invoking control in every dismissal path (button, scrim, Escape), and whether the disabled Confirm state is announced. Timebox 90 min. Feeds **GAP-FE-010**.

**`TC-FE-EXPL-005` — Storage tampering and session-continuity resilience**
*Explore* the five `localStorage` keys of §1.13, *with* hand-edited, truncated and hostile values, *to discover* whether the SPA degrades safely.
Resources: `auth.service.ts:44-56`, `i18n.service.ts:1514-1517`, `shell.component.ts:383-393`, `login.component.ts:368`.
Report: behaviour for `qams.lang = 'de'` / `'<script>'`, `qams.sidebar.groups = '{'` / `'null'` / a 10 000-element array, `qams.tenant.slug` set to another tenant's slug while signed in, and `qams.tenant.slug` set to a path-traversal string. Confirm that no access token, role, or privilege list is ever written to any web storage. Timebox 90 min.

**`TC-FE-EXPL-006` — Dictionary coverage sweep across all three languages**
*Explore* all 47 tenant screens and both dialogs, *in each of `en`, `ar`, `fr`*, *to discover* every place where `t()` falls through to the raw key or where an English literal is hard-coded.
Resources: `i18n.service.ts:1509-1512` (the silent key fallback), the confirmed miss `lov.category` on `/org-context`, the hard-coded `aria-label="Language"` at `shell.component.ts:75`, and the 149 dictionary keys with no literal reference (§1.8).
Report: a list of every visible string matching `/^[a-z]+\.[a-zA-Z]+$/` in the rendered DOM, per language; every untranslated English string; and, for each of the 149 unreferenced keys, whether it is resolved dynamically or genuinely dead. Timebox 150 min. Feeds **GAP-FE-005**.

---

## 8. Gap Register (this module)

---

### GAP-FE-001 — Framework version in the brief, the package metadata and CLAUDE.md all disagree with the build

| Field | Content |
|---|---|
| **Source reference** | Commissioning brief ("Angular 18 standalone"); `frontend/package.json:4` (`"description": "NT.QAMS Angular 18 frontend"`); `NT.QAMS/CLAUDE.md` §1; against `frontend/package.json:15-25,41` |
| **Description** | The shipped SPA is Angular **22.0.8** on TypeScript **6.0.3** and zone.js **0.15.1**. Three separate authoritative-looking sources still say Angular 18. The conventions file (§1) has the correct value; the repository's own metadata does not. |
| **Impact** | A validation reader reconciling the CSV package against `package.json` or `CLAUDE.md` will conclude the documentation set is stale, which undermines the whole traceability argument. It also risks an author writing Angular 18-era test techniques (NgModules, `HttpClientTestingModule` patterns) that no longer apply. |
| **Testing limitation** | None for execution — the correct version is measurable. The limitation is documentary: no requirement states the target framework version, so a version-conformance test has nothing to assert against. |
| **Recommended clarification** | Product owner to confirm Angular 22 as the validated baseline, then correct `package.json:4` and `CLAUDE.md` §1 in the same commit, and add a URS requirement naming the supported framework major. |
| **Suggested acceptance criteria** | `package.json.description` and `CLAUDE.md` §1 name Angular 22; a URS requirement (new, e.g. `URS-108`) states the validated framework major; a CI assertion fails the build if the installed `@angular/core` major diverges from the URS-stated one. |
| **Severity** | Low (documentation integrity; no functional risk) |
| **Responsible role** | Technical Lead / QA Documentation Owner |

---

### GAP-FE-002 — No requirement defines the client-side authorization model

| Field | Content |
|---|---|
| **Source reference** | `core/role.guard.ts:11-21`; `core/permissions.service.ts:41,68-70`; conventions §2 *[corrected 2026-08-01]* ("Authorization comes from tenant-defined roles over the permission catalogue") |
| **Description** | The SPA uses two orthogonal mechanisms — the tier string `'PlatformAdmin'` for the two route guards, and 73 `{module}.{action}` keys for affordances — and neither is described by any URS requirement located in this pass. `isPlatformAdmin` is derived from a magic string literal `'PlatformAdmin'` compared against `auth.role()`, in contravention of `CLAUDE.md` §2.2 ("No magic strings; roles live in `WebApi/Authorization/Roles.cs`"). |
| **Impact** | Every affordance test is `[ID]` (implementation-derived) rather than requirement-traceable, so the RTM cannot demonstrate that the UI enforces the intended access model. A typo in the literal `'PlatformAdmin'` would silently grant every tenant screen to a platform admin and vice versa, with no compile-time or test-time detection. |
| **Testing limitation** | Cases can only assert what the code does, not what it should do. No oracle exists for "which control should this role see", so a wrongly-hidden or wrongly-shown control cannot be called a defect. |
| **Recommended clarification** | Business/QA owner to state, as a requirement, (a) that route access is tier-gated and permission-gating is affordance-only, and (b) the canonical tier string list. Engineering to replace the literal with a shared constant mirroring `Roles.cs`. |
| **Suggested acceptance criteria** | A URS requirement covers the two-mechanism model; `'PlatformAdmin'` appears exactly once in the SPA, in a named constant; a unit test asserts the constant matches the value the API returns in `AuthResponse.role`. |
| **Severity** | Medium |
| **Responsible role** | Quality Manager (requirement) + Technical Lead (constant) |

---

### GAP-FE-003 — Every tenant route is reachable by direct URL regardless of privilege

| Field | Content |
|---|---|
| **Source reference** | `app.routes.ts:34-413` — no `canActivate` other than `authGuard`/`tenantOnlyGuard` on any of the 47 tenant routes; `shell/shell.component.ts:342-346` — five routes are hidden from the sidebar by a permission key |
| **Description** | `/users`, `/roles`, `/access-reviews`, `/compliance` and `/notification-rules` are hidden from the sidebar when the user lacks `users.view`, `roles.view`, `access-reviews.view`, `compliance.view` or `notifications.manage` respectively — but typing the URL loads the component. The server correctly answers 403 to its API calls, so no data is disclosed by the server; the client-side behaviour on that 403 is undefined and unspecified. |
| **Impact** | An operator who bookmarks or is sent a link to a screen they no longer hold the privilege for sees a broken page rather than a clear denial. For an ISO 17025 / Part 11 assessor this reads as an access-control inconsistency between the navigation model and the routing model, even though the server boundary holds. |
| **Testing limitation** | The expected UI for an unprivileged direct navigation is not specified anywhere, so a case can record the observed rendering but cannot pass or fail it. All such cases must be labelled `[GD]` against this gap. |
| **Recommended clarification** | Decide whether the intended behaviour is (a) a permission route guard that redirects to `/dashboard` with a message, (b) a rendered "not entitled" state, or (c) accept the current behaviour and document it as designed. |
| **Suggested acceptance criteria** | For each of the five gated routes, a signed-in user lacking the key who navigates directly is presented with the agreed outcome deterministically; no privileged payload appears in the DOM at any point; an automated test covers all five. |
| **Severity** | Medium |
| **Responsible role** | Product Owner + Technical Lead |

---

### GAP-FE-004 — Reason-for-change is collected on three different paths with three different contracts

| Field | Content |
|---|---|
| **Source reference** | `core/change-reason.interceptor.ts:18,30` (DELETE → `X-Change-Reason` header); `features/records/records.component.ts:145-148` (legal hold → POST body); `features/roles/roles.component.ts:300-309` (privilege change → `setPermissions` body via `TextPromptService`); conventions §2 |
| **Description** | Three regulated flows capture a Part 11 reason through **two different services** (`ChangeReasonService`, `TextPromptService`) with **two different trimming rules** (`ChangeReasonService.confirm` returns the trimmed string, `TextPromptService.confirm` returns the untrimmed value, `text-prompt.service.ts:62-65`) and **two different transports** (header vs body). No requirement enumerates which operations require a reason. |
| **Impact** | A future regulated operation is as likely to be built with no reason capture as with one, and nothing detects the omission. The inconsistent trimming means a role-privilege reason can reach the audit trail with leading/trailing whitespace while a deletion reason cannot — the same record type, two data qualities. |
| **Testing limitation** | Without an authoritative list of reason-requiring operations, coverage cannot be proven complete: a test suite can only cover the flows that happen to exist today. |
| **Recommended clarification** | Quality Manager to enumerate every operation that requires a documented reason under 21 CFR Part 11 §11.10(e), and state whether reasons are normalised (trimmed, min/max length) before persistence. |
| **Suggested acceptance criteria** | An enumerated requirement lists the reason-requiring operations; both prompt services apply identical normalisation; a test asserts every listed operation cannot complete without a non-blank reason and that the persisted reason is trimmed. |
| **Severity** | Medium–High (Part 11 data-integrity scope) |
| **Responsible role** | Quality Manager + Technical Lead |

---

### GAP-FE-005 — A missing dictionary key renders a raw key string to the operator

| Field | Content |
|---|---|
| **Source reference** | `features/organization/org-context.component.ts:77,102,131` reference `i18n.t('lov.category')`; the key is absent from `core/i18n.service.ts` (only `lov.manageHint` at `:1117` exists under that prefix); the fallback is `core/i18n.service.ts:1511` |
| **Description** | `t()` returns the key itself for an unknown key. On `/org-context`, one table header and two form labels therefore render the literal text `lov.category` in English, Arabic and French. Verified by exhaustive diff of the 1,417 dictionary keys against every literal `i18n.t('…')` call site: this is the **only** such miss in the application. |
| **Impact** | A regulated screen displays developer-facing text to a laboratory user in all three languages — a visible quality defect on an ISO-scope screen, and a demonstration that the silent fallback hides such misses rather than surfacing them. |
| **Testing limitation** | None — this is directly executable. It does, however, mean the i18n suite currently has no completeness assertion; a case can catch this instance but not the class. |
| **Recommended clarification** | None needed for the defect itself. Confirm whether the silent key fallback should remain in production builds or be made loud (console warning / build-time check) in non-production. |
| **Suggested acceptance criteria** | `lov.category` exists with `en`/`ar`/`fr` values; a build-time or unit-test check fails when any literal `i18n.t('…')` key is absent from the dictionary; no rendered string matches `/^[a-z]+\.[a-zA-Z]+$/` on any of the 47 screens in any of the three languages. |
| **Severity** | Low functionally, Medium for regulated presentation |
| **Responsible role** | Technical Lead |

---

### GAP-FE-006 — Domain error codes never reach the operator or the screen

| Field | Content |
|---|---|
| **Source reference** | `features/change/change.facade.ts:117-122` (and the identical `describe()` in all 34 facades, e.g. `features/analytical/qc.facade.ts:83`); `features/login/login.component.ts:480` — the only code the SPA branches on is `AUTH-101` |
| **Description** | The API returns `application/problem+json` carrying a structured `code` member (`SOD-QP-001`, `CHG-021`, `CMP-020`, `CHANGE-REASON-REQUIRED`, `CONCURRENCY-409`, `AUTHZ-*`, …). The SPA reads only `title` and discards `code`, `traceId` and every extension member. A 403, a 409 optimistic-concurrency conflict, a 422 domain-rule breach and a 500 are all rendered identically as one line of grey text. |
| **Impact** | An operator cannot tell "you are not permitted" from "someone else changed this record" from "the system failed", and cannot quote a code to support. It also removes the one artefact that makes an error reproducible in an audit finding. Testing is affected directly: a case cannot assert the exact domain code at the UI layer, only at the API layer, so UI-level negative cases lose their strongest oracle. |
| **Testing limitation** | Every UI-layer negative case must assert on a free-text `title` string, which is fragile and untranslatable. Domain-code assertions must be pushed down to the API-layer batches. |
| **Recommended clarification** | Product Owner to decide whether the code and correlation id should be shown (e.g. a collapsed "reference" line) and whether 409 concurrency conflicts warrant a distinct recovery affordance. |
| **Suggested acceptance criteria** | `describe()` surfaces `code` and `traceId` alongside `title`; a 409 renders a distinguishable "the record changed — reload" state; a unit test per facade asserts the code is preserved from the `problem+json` body to the exposed `error` signal. |
| **Severity** | Medium |
| **Responsible role** | Product Owner + Technical Lead |

---

### GAP-FE-007 — `'Unexpected error.'` is shown for at least one non-error condition

| Field | Content |
|---|---|
| **Source reference** | `features/change/change.facade.ts:117-122` (`describe()` returns the literal for any non-`HttpErrorResponse`), reached from `:99` (`await firstValueFrom(call())`) |
| **Description** | `describe()` treats anything that is not an `HttpErrorResponse` as an unexpected failure. Because `changeReasonInterceptor` returns `EMPTY` on cancel, `firstValueFrom` rejects with RxJS `EmptyError`, which is not an `HttpErrorResponse` — so a **deliberate operator cancellation** is reported as an unexpected system error. The literal is also hard-coded English, outside the i18n dictionary, so it is untranslated in Arabic and French. |
| **Impact** | An operator who correctly declines to delete regulated evidence is told the system malfunctioned. In a regulated setting this both erodes trust in the error channel and risks a spurious deviation report. |
| **Testing limitation** | Until the two conditions are separated, a case cannot distinguish "cancelled" from "failed" at the UI layer; cancellation cases must assert on the absence of a network request rather than on the UI state. |
| **Recommended clarification** | Confirm the intended UI outcome of a cancelled DELETE: silent return to the prior state (recommended) or an informational "no changes made" notice. |
| **Suggested acceptance criteria** | Cancelling the reason dialog leaves `error()` empty and `loading()` false; the fallback message is a dictionary key with `en`/`ar`/`fr` values; a facade unit test covers the cancel path end-to-end. |
| **Severity** | Medium |
| **Responsible role** | Technical Lead |

---

### GAP-FE-008 — The signature PIN has no client-side format rule, and none exists to test against

| Field | Content |
|---|---|
| **Source reference** | `core/auth.service.ts:115-117` — `setPin(pin: string)` POSTs verbatim to `/auth/signature-pin` with no validation; conventions §2 ("No digit-length constraint on the PIN was found in the domain") |
| **Description** | The brief speaks of a "4-digit PIN". Neither the SPA nor (per the conventions file) the domain constrains the PIN's length, character set or entropy. The SPA will submit a one-character PIN. |
| **Impact** | The e-signature second factor may be arbitrarily weak, which is material to 21 CFR Part 11 §11.200(a)(1) (two distinct identification components). |
| **Testing limitation** | Any PIN boundary-value case is `[GD]` against this gap: with no rule, there is no boundary. Cases can only record the observed acceptance. |
| **Recommended clarification** | Quality Manager and Technical Lead to define the PIN rule (length, character set, reuse restrictions, whether it may equal the password) as a requirement, and state where it is enforced — domain, validator, or both. |
| **Suggested acceptance criteria** | A URS requirement states the PIN rule; the domain rejects a non-conforming PIN with a named code; the SPA blocks submission client-side with a translated message; BVA cases exist at length−1 / length / length+1. |
| **Severity** | High (Part 11 signature control) |
| **Responsible role** | Quality Manager + Technical Lead |

---

### GAP-FE-009 — The client idle lockout is unspecified, exceeds the token lifetime, and leaks listeners

| Field | Content |
|---|---|
| **Source reference** | `core/auth.service.ts:157` (`IDLE_LIMIT_MS = 30 * 60 * 1000`), `:161-174` (listeners on `click`, `keydown`, `visibilitychange`, never removed; expiry calls `logout()` then `location.assign('/login')`); armed from `shell/shell.component.ts:234` |
| **Description** | A 30-minute client-side idle lockout exists with no requirement behind it. It is longer than the 15-minute access token (conventions §2), so the SPA silently refreshes the session up to twice during an idle window. `startIdleWatch()` adds three `document` listeners every time a `ShellComponent` is constructed and never removes them, so a sign-out/sign-in cycle in one page load leaves the earlier closures attached. Mouse movement and scrolling do **not** count as activity — only clicks, key presses and tab visibility changes. |
| **Impact** | A reviewer reading a long document with the mouse alone can be signed out mid-task; conversely, an unattended workstation stays authenticated for up to 30 minutes. Neither behaviour is traceable to a requirement, so neither can be defended in an audit. |
| **Testing limitation** | Without a stated timeout requirement, timing cases have no oracle; they can only confirm the coded 30 minutes. The listener accumulation is observable only through DevTools, not through a functional assertion. |
| **Recommended clarification** | Quality Manager to state the required inactivity timeout and what counts as activity; Technical Lead to confirm whether the timeout is intended to be client-side, server-side, or both, and to make the SPA value configurable rather than a compiled constant. |
| **Suggested acceptance criteria** | A URS requirement states the timeout and the activity definition; the SPA value derives from configuration; listeners are registered exactly once and torn down with the shell; a case proves sign-out occurs at the stated boundary and not before. |
| **Severity** | Medium |
| **Responsible role** | Quality Manager + Technical Lead |

---

### GAP-FE-010 — The accessibility gate covers two anonymous pages out of forty-nine

| Field | Content |
|---|---|
| **Source reference** | `e2e/a11y.spec.ts:15-37` — axe runs on `/login` and `/t/demo-lab` only, failing on `serious`/`critical`; `.github/workflows/ci.yml` frontend job runs `e2e/auth.spec.ts e2e/a11y.spec.ts` |
| **Description** | No authenticated screen has ever been scanned: not the 47 tenant pages, not the platform page, not the two modal dialogs, not the drawer, not the RTL rendering, not the wide data tables or the SVG charts. Source review additionally found **no focus trap** in either modal (`change-reason-dialog.component.ts`, `text-prompt-dialog.component.ts`) — focus is moved in and restored out, but Tab can leave the dialog — and one hard-coded English `aria-label="Language"` in the shell header (`shell/shell.component.ts:75`). |
| **Impact** | The CI a11y gate provides assurance for roughly 4 % of the application's surface while presenting as a whole-application control. Modal focus escape is a WCAG 2.1 §2.4.3 / §2.1.2 concern on the exact dialogs that gate regulated actions. |
| **Testing limitation** | Authenticated axe scans require a seeded API in CI, which the current pipeline does not provide (see GAP-FE-013); until then, authenticated a11y cases are manual and cannot be gated. |
| **Recommended clarification** | Agree the target WCAG level and conformance scope (which screens, which impact thresholds, LTR and RTL), and whether the CI gate should be extended to authenticated surfaces or a separate scheduled job. |
| **Suggested acceptance criteria** | Axe scans at least one representative screen from each of the eight navigation groups plus both modals, in `en` and `ar`, with zero `serious`/`critical` violations; both modals trap focus and mark background content inert; every `aria-label` resolves through the dictionary. |
| **Severity** | Medium–High |
| **Responsible role** | Quality Manager + Technical Lead |

---

### GAP-FE-011 — Four regulated screens have no in-app help topic

| Field | Content |
|---|---|
| **Source reference** | `core/help/help-content.ts:894-908` (42 topics keyed by first path segment) diffed against the 47 top-level tenant routes in `app.routes.ts` |
| **Description** | `/access-reviews`, `/quality-policy`, `/roles` and `/settings/security` have no registered help topic, so `helpTopicForUrl` returns `undefined` and the page-header `?` control has nothing to open. (`/manual` is also unmapped, correctly — it is the manual itself.) These four are among the most compliance-sensitive screens in the product: periodic access review, the quality policy, the privilege matrix, and MFA/PIN settings. |
| **Impact** | The in-app user manual is incomplete precisely where an ISO 17025 §8.2 / Part 11 §11.10(i) training-and-guidance argument would lean on it. |
| **Testing limitation** | A help-coverage case can only enumerate the miss; it cannot assert the content is correct, since no requirement defines what the help must say. |
| **Recommended clarification** | Quality Manager to confirm that in-app help is a controlled training aid (and therefore in validation scope) and to supply the four missing topics in `en`/`ar`/`fr`. |
| **Suggested acceptance criteria** | Every routed screen except `/manual` resolves a help topic; a unit test asserts `helpTopicForUrl` returns a topic for each top-level tenant route; each new topic carries all three languages. |
| **Severity** | Low–Medium |
| **Responsible role** | Quality Manager |

---

### GAP-FE-012 — Frontend unit coverage reaches 15 of 194 source files, with the guards and 32 of 34 facades untested

| Field | Content |
|---|---|
| **Source reference** | 15 `*.spec.ts` under `frontend/src` (§1.15) against 194 non-spec `.ts` files; `core/auth.guard.ts` and `core/role.guard.ts` have no spec; only `features/change/change.facade.spec.ts` and `features/complaints/complaints.facade.spec.ts` exist among 34 facades; `ShellComponent`, `LoginComponent`, `OrgDataService`, `HelpService`, `ChangeReasonService`, `TextPromptService` and `AppComponent` have none |
| **Description** | The 76 existing tests are well targeted (interceptors, dialogs, permissions, shared controls) but the suite has no coverage of the three route guards, of the shell's permission-driven navigation assembly, of the login state machine (MFA / enrollment / `AUTH-101`), of the language-direction effect, or of 32 facades that share an identical — and therefore identically defective, per GAP-FE-007 — `describe()` implementation. |
| **Impact** | A regression in the guards would remove the SPA's only client-side access boundary silently. A regression in the shared facade contract would propagate to all 34 modules at once. Neither is currently detectable before manual testing. |
| **Testing limitation** | Coverage is not currently measured or gated: `angular.json` configures the Karma builder with no `codeCoverage` option, and the CI `frontend` job runs `npm run test:ci` without a threshold, so there is no baseline to trend against. |
| **Recommended clarification** | Agree a coverage floor for `core/` (guards, interceptors, services) and for the shared facade contract, and whether it should be a merge gate. |
| **Suggested acceptance criteria** | Specs exist for all three guards, for the login state machine, for `AppComponent`'s direction effect, and for the shared facade `run`/`describe`/`loadMore` contract (tested once against a representative facade plus a contract test applied across all 34); CI enforces an agreed statement-coverage floor for `src/app/core`. |
| **Severity** | Medium |
| **Responsible role** | Technical Lead |

---

### GAP-FE-013 — The regulated-workflow e2e spec is written but never runs in CI

| Field | Content |
|---|---|
| **Source reference** | `.github/workflows/ci.yml` frontend job — final step is `npx playwright test e2e/auth.spec.ts e2e/a11y.spec.ts`; `e2e/regulated-workflow.spec.ts` (2 tests) is excluded; `e2e/README.md` explains it needs a seeded API; `e2e/regulated-workflow.spec.ts:56-61` records a deliberately omitted auditor journey |
| **Description** | Four of the six Playwright tests run in the gate, all against the anonymous sign-in surface. The only spec that exercises the full stack (SPA → JWT → API → PostgreSQL) — sign-in as a tenant admin and reach a regulated register — runs only when someone runs it locally with the API up. There is also no e2e journey at all for the reason-for-change dialog, the language switch, or any regulated state transition. |
| **Impact** | The CI evidence for "the SPA works end to end" covers only unauthenticated pages. A break in the authenticated path (session hydration, bearer attachment, tenant pinning, pagination envelope) reaches the dev machines rather than the pipeline. For a GAMP 5 argument, the e2e control is materially weaker than the pipeline's naming (`Frontend (unit + build + e2e smoke)`) implies. |
| **Testing limitation** | No seeded API service exists in the frontend CI job, and the dev machines have no Docker (conventions §3), so a Testcontainers-backed seed is unavailable. Authenticated e2e cases must be marked as manual-execution until a CI PostgreSQL + API service is added. |
| **Recommended clarification** | Decide whether to add a PostgreSQL service and an API container to the frontend job, or to run the authenticated e2e suite as a separate scheduled/nightly job with a documented seed. |
| **Suggested acceptance criteria** | `e2e/regulated-workflow.spec.ts` runs in an automated pipeline on every push to `master`; at least one e2e journey covers the reason-for-change dialog on a real DELETE and one covers an Arabic RTL render; a seeded read-only role exists so the auditor journey at `:56-61` can be written. |
| **Severity** | Medium–High |
| **Responsible role** | Technical Lead / DevOps |

---

### GAP-FE-014 — The 401 retry re-enters the reason interceptor, prompting for a Part 11 reason twice

| Field | Content |
|---|---|
| **Source reference** | `core/auth.interceptor.ts:33` — the retry is `next(withBearer(req, token))` using the **original** `req`; `core/change-reason.interceptor.ts:18` — the gate is "method is DELETE **and** the header is absent"; `app.config.ts:15` — `authInterceptor` is registered before, and therefore wraps, `changeReasonInterceptor` |
| **Description** | Downstream interceptor mutations do not propagate back up the chain, so on a 401-triggered retry the `authInterceptor` re-issues the request **without** the `X-Change-Reason` header that `changeReasonInterceptor` added on the first attempt. The inner interceptor sees a DELETE with no header and opens the reason dialog a second time. A second, possibly different, reason is then attached to the request that actually reaches the server — and it is that second reason the `FieldChangeInterceptor` stamps onto the ledger row. The first reason is discarded silently. Compounding this, `ChangeReasonService.request()` cancels any in-flight dialog first (`change-reason.service.ts:30`), and the cancel path returns `EMPTY`, which surfaces as `'Unexpected error.'` (GAP-FE-007). |
| **Impact** | Direct 21 CFR Part 11 §11.10(e) concern: the reason the operator believes they gave for voiding analytical evidence may not be the reason recorded in the audit trail. It is also a usability defect that will read to an operator as the system losing their input. |
| **Testing limitation** | Reproducing it requires forcing an access-token expiry between the reason prompt and the server response — feasible by holding the dialog open past the 15-minute token lifetime, but slow, so it is an exploratory charter (`TC-FE-EXPL-002`) rather than a routine scripted case. The double-prompt is not covered by `core/change-reason.interceptor.spec.ts`, which tests the interceptor in isolation without the auth interceptor above it. |
| **Recommended clarification** | Technical Lead to confirm the intended behaviour: either the retry must reuse the fully-decorated request, or the reason must be captured above the auth interceptor (e.g. in the calling facade) so it survives a retry. Quality Manager to confirm whether a reason may ever be re-collected for a single logical operation. |
| **Suggested acceptance criteria** | A DELETE whose first attempt returns 401 prompts for a reason exactly **once**; the reason that reaches `audit.field_change` is the one the operator entered; an integration test drives a forced 401 through the full interceptor chain and asserts both the prompt count and the persisted reason; cancelling at any point sends no request and shows no error. |
| **Severity** | High (Part 11 audit-trail fidelity) |
| **Responsible role** | Technical Lead + Quality Manager |

---

*End of front matter for module FE. Detailed cases are authored in `23-module-frontend-cases-A.md` … `-F.md` against the ranges reserved above.*
