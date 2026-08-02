# TENANT — Detailed Test Cases, Batch D

This batch authors the **control-plane and tenant-lifecycle** slice of module `TENANT` against NT.QMS **v1.51.2** (repo `D:\SAAS\QAMS\21-7\NT.QAMS`, inspected and re-measured 2026-08-01): end-to-end tenant provisioning through `POST /api/tenants` and its atomic seed (tenant + first administrator + five starter roles + starter list-of-values + outbox/ledger rows); slug acceptance, normalisation and boundary handling across the two validation layers that guard it; the full `TenantStatus` transition matrix including every illegal edge and the guard-evaluation order inside `Tenant.Suspend`; tenant-settings read and write (`GET`/`PUT /api/tenant-settings/mfa-policy`) and the two different authorization mechanisms that reach them; platform-admin-only access to `TenantsController` and the five ordinary tenant-scoped controllers that `PlatformControllers.cs` misleadingly names; and the consequences of the tenant-first composite primary key — there is **no `UNIQUE (id)`** on any of the 90 tenant-owned tables, so an id alone is not a lookup key. It deliberately leaves to sibling batches: per-table RLS predicate proofs, fail-closed GUC behaviour, elevation-path abuse, cross-tenant attack cases, migration round-trip and the least-privilege role guard (**batch B**); tenant-resolution middleware internals, the interceptors at component level, MC/DC over the composed query filter, observability, DR, performance and accessibility (**batch C**); and the pure-domain `TenantSlug` / `TenantSettings` unit and equivalence-partition sweeps that overlap the existing `TenantTests` (**batch A**). Every case is `Not Run`.

**ID block consumed:** `TC-TENANT-API-001…025`, `TC-TENANT-STATE-001…013` (38 cases).
**Risk IDs:** `docs/validation/02-Functional-Risk-Assessment.md` records **areas**, not `RSK-nnn` identifiers, so the `RSK-TENANT-nnn` ids below are **minted by this batch** per conventions §5 and mapped to their FRA area in the coverage note.

---

## Provisioning — `POST /api/tenants`

#### TC-TENANT-API-001 — Provisioning creates the tenant, its administrator, five roles and the starter lists in one transaction  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-009 · RSK-TENANT-003 |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — the whole provisioning saga end to end |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional + real PostgreSQL) |
| **Role / Permission / Tenant** | PlatformAdmin · `[Authorize(Roles = Roles.PlatformAdmin)]` (`src/NT.QAMS.WebApi/Controllers/TenantsController.cs:12`) + `[RequireRole(UserRole.PlatformAdmin)]` (`src/NT.QAMS.Application/Tenancy/Commands/ProvisionTenant.cs:17`) · none — the caller carries no `tenant_id` claim (`src/NT.QAMS.Infrastructure/Security/SecurityAdapters.cs:92-95`) |
| **Environment** | API `:5080` `ASPNETCORE_ENVIRONMENT=Development` + live PostgreSQL 17 `ntqams`, role `qams_app` |
| **Preconditions** | Signed in as `platform-admin@localhost` / `Dev-Only-Platform-Pass-1!` (no slug on the login body); `SELECT count(*) FROM saas.tenant WHERE identifier='batchd-prov-001'` returns 0 |
| **Test Data** | `{"identifier":"batchd-prov-001","name":"Batch D Provisioning Laboratory","adminEmail":"admin@batchd-prov-001.test","adminDisplayName":"Batch D Administrator","adminPassword":"Prov-Batch-D-2026!"}` |
| **Steps** | 1. `POST /api/tenants` with the body above and `Authorization: Bearer <platform token>`. 2. Record status, body and the `Location` header. 3. `SELECT id, identifier, name, status, password_expiry_days, calibration_reminder_days, sop_expiry_reminder_months, default_language, time_zone, require_mfa_privileged FROM saas.tenant WHERE identifier='batchd-prov-001'`. 4. `SELECT email, role, role_id, is_active, tenant_id FROM qams.user_account WHERE tenant_id=<new id>`. 5. `SELECT set_config('app.bypass_rls','on',false); SELECT r.name, count(rp.permission_key) FROM qams.role r LEFT JOIN qams.role_permission rp ON rp.role_id=r.id AND rp.tenant_id=r.tenant_id WHERE r.tenant_id=<new id> GROUP BY 1 ORDER BY 1`. 6. `SELECT count(DISTINCT category), count(*) FROM qams.lov_entry WHERE tenant_id=<new id>`. 7. `SELECT event_type, tenant_id, payload FROM audit.audit_trail WHERE payload::jsonb->>'slug'='batchd-prov-001'`. |
| **Expected UI** | `/platform/tenants` shows the confirmation note carrying `batchd-prov-001` (`frontend/src/app/features/platform/tenants.component.ts:61-65,145`) and the list reloads with a new row whose status pill reads `Active`. |
| **Expected API** | `201 Created`; body `{"id":"<uuid>"}` (`TenantsController.cs:28`). |
| **Expected DB** | Exactly one `saas.tenant` row: `status='Active'`, `password_expiry_days=90`, `calibration_reminder_days=30`, `sop_expiry_reminder_months=3`, `default_language='en'`, `time_zone='UTC'`, `require_mfa_privileged=false` (`src/NT.QAMS.Domain/Tenancy/TenantSettings.cs:10-20`). Exactly one `qams.user_account` row: `role='TenantAdmin'`, `is_active=true`, `role_id` = the id of the `Tenant Administrator` role (`ProvisionTenant.cs:66-68`). Exactly **5** `qams.role` rows named `Tenant Administrator`, `Quality Manager`, `Department Head`, `Analyst`, `External Auditor` (`src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs:24-28`); `Tenant Administrator` carries **171** `role_permission` rows (`PermissionCatalog.AllKeys`, `src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:134-192`). `qams.lov_entry`: **13** distinct categories, **50** rows. |
| **Expected Audit** | One `audit.audit_trail` row, `event_type='NT.QAMS.Domain.Tenancy.TenantProvisioned, NT.QAMS.Domain'`, payload `{"tenantId":…,"slug":"batchd-prov-001","name":"Batch D Provisioning Laboratory","eventId":…}`, `tenant_id='00000000-0000-0000-0000-000000000000'` — the provisioning chain is the **nil-tenant** chain, not the new tenant's (measured: 68 such rows, all nil). One `qams.outbox_event` row of the same type. `audit.field_change` gains a `Tenant`/`Created` row whose `tenant_id` is **NULL** (measured: 66/66 such rows are null) — see GAP-TENANT-905. |
| **Expected Notification** | The `TenantProvisioned` handler at `src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:126-128` runs; assert whatever `qams.notification_dispatch` rows it produces for `<new id>` and record the count — this is the only tenancy event with a consumer. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','on',false); DELETE FROM qams.lov_entry WHERE tenant_id=<new id>; DELETE FROM qams.role_permission WHERE tenant_id=<new id>; DELETE FROM qams.user_account WHERE tenant_id=<new id>; DELETE FROM qams.role WHERE tenant_id=<new id>; DELETE FROM qams.outbox_event WHERE tenant_id=<new id>; DELETE FROM saas.tenant WHERE identifier='batchd-prov-001';` |
| **Evidence** | HTTP response capture (status, body, `Location`) · seven SQL result sets · SPA screenshot of the confirmation note |
| **Result / Defect** | Not Run · — |
| **Notes** | Reference distribution measured on `demo-lab` 2026-08-01: `Tenant Administrator` 171 · `Quality Manager` 165 · `Department Head` 90 · `Analyst` 65 · `External Auditor` 47 (538 `role_permission` rows). Re-derive the four non-admin counts from `SystemRoleCatalogTests` before asserting them; only the 171 is computable directly from `PermissionCatalog.AllKeys`. All nine writes commit under a single `SaveChangesAsync` (`ProvisionTenant.cs:74`), which is why the five `saas.tenant` FKs are `DEFERRABLE INITIALLY DEFERRED` (measured 5/5, defect SH-D2). |

#### TC-TENANT-API-002 — The `Location` header of a successful provision is not a resource URI  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-003 · **GAP-TENANT-005** |
| **Level / Type / Technique** | API · Functional (defect-pinning) · Error Guessing — `CreatedAtAction` naming an action with no `{id}` segment |
| **Priority / Severity / Automation** | Low · Low · Yes (functional) |
| **Role / Permission / Tenant** | PlatformAdmin · role gate `Roles.PlatformAdmin` · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | TC-TENANT-API-001 executed, or an equivalent successful provision available in the same run |
| **Test Data** | `identifier=batchd-loc-002`, `name=Batch D Location Laboratory`, `adminEmail=admin@batchd-loc-002.test`, `adminDisplayName=Loc Admin`, `adminPassword=Loc-Batch-D-2026!` |
| **Steps** | 1. `POST /api/tenants` with the data above. 2. Capture the raw `Location` response header verbatim. 3. Issue `GET <Location>` with the same bearer token. 4. Compare the response to `GET /api/tenants`. 5. `grep -n "GET /api/tenants/{id}" tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt`. |
| **Expected UI** | n/a — the SPA never reads `Location`; it calls `load()` again (`tenants.component.ts:147`). |
| **Expected API** | Step 2: `Location: http://localhost:5080/api/tenants?id=<guid>` — the id is appended as a **query string**, because `CreatedAtAction(nameof(GetAll), new { id = tenantId }, …)` (`TenantsController.cs:28`) targets the template `api/tenants` (`:11,31`) which has no `{id}` segment. Step 3: `200` returning the **full tenant list**, not the created tenant. Step 5: **no match** — `GET /api/tenants/{id}` is absent from the approved surface (present entries are lines 126 and 236 only). |
| **Expected DB** | Unchanged by steps 2–5; the tenant row from step 1 exists exactly once. |
| **Expected Audit** | No additional `audit.audit_trail` or `audit.field_change` rows from the `GET`s (reads are not audited). |
| **Expected Notification** | n/a — no notification is defined for a resource-location read. |
| **Cleanup** | Delete the `batchd-loc-002` tenant and its dependents as in TC-TENANT-API-001. |
| **Evidence** | Raw response headers · both `GET` bodies · the `ApiSurface.approved.txt` grep output |
| **Result / Defect** | Not Run · — |
| **Notes** | This case **pins a defect**. Do not record it as conformance. It is satisfied by GAP-TENANT-005 acceptance criterion (1): either `GET /api/tenants/{id:guid}` exists, is platform-gated and `CreatedAtAction` targets it, or the action returns a form with no `Location`. Re-author this case as a positive assertion when the gap closes. |

#### TC-TENANT-API-003 — A slug already in use is refused case-insensitively with `TENANT-005`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-006 |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning — the "slug already taken" partition, entered through a case variant |
| **Priority / Severity / Automation** | High · High · Yes (functional + real PostgreSQL) |
| **Role / Permission / Tenant** | PlatformAdmin · role gate `Roles.PlatformAdmin` · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SELECT identifier FROM saas.tenant WHERE identifier='demo-lab'` returns one row (measured present, `status='Active'`) |
| **Test Data** | `{"identifier":"DEMO-LAB","name":"Duplicate Slug Probe","adminEmail":"dup@batchd.test","adminDisplayName":"Dup Admin","adminPassword":"Dup-Batch-D-2026!"}` |
| **Steps** | 1. `POST /api/tenants` with the body above. 2. Read status, `content-type` and the `code` extension. 3. `SELECT count(*) FROM saas.tenant WHERE identifier IN ('demo-lab','DEMO-LAB')`. 4. `SELECT count(*) FROM qams.user_account WHERE email='dup@batchd.test'`. |
| **Expected UI** | The drawer's `.error` div shows the problem `title` verbatim: `Tenant identifier 'demo-lab' is already in use.` (`tenants.component.ts:57,162`). |
| **Expected API** | `422 Unprocessable Entity`, `application/problem+json`, `code = "TENANT-005"`, `title = "Tenant identifier 'demo-lab' is already in use."` — the message carries the **normalised** slug because `TenantSlug.Create` lower-cases before the uniqueness check (`src/NT.QAMS.Domain/Tenancy/TenantSlug.cs:28`; check at `ProvisionTenant.cs:45-49`; 422 arm at `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:75-80`). |
| **Expected DB** | Step 3 returns exactly **1** (the pre-existing `demo-lab`); step 4 returns **0** — nothing partial is written, because the throw precedes the single `SaveChangesAsync` (`ProvisionTenant.cs:74`). |
| **Expected Audit** | No new `audit.audit_trail` row and no new `qams.outbox_event` row of type `…TenantProvisioned…` — the aggregate is never constructed. |
| **Expected Notification** | n/a — no notification is defined for a rejected provisioning attempt. |
| **Cleanup** | None — the request must leave no trace. Re-run step 3 after the case to confirm. |
| **Evidence** | HTTP response capture · SQL result sets for steps 3 and 4 |
| **Result / Defect** | Not Run · — |
| **Notes** | Partially overlapped by `tests/NT.QAMS.Application.UnitTests/Tenancy/ProvisionTenantTests.cs` (case-different duplicate at handler level). This case adds the HTTP status, the media type, the exact `code`, and the "nothing partially written" database assertion, none of which a handler-level unit test can make. The unique index `ix_tenant_identifier` on `saas.tenant(identifier)` (measured) is the second line of defence if the pre-check races. |

#### TC-TENANT-API-004 — A 51-character slug is refused by the validator with 400, never reaching `TENANT-002`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-006 |
| **Level / Type / Technique** | API · Functional (negative) · BVA — one character above `TenantSlug.MaxLength` |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | PlatformAdmin · role gate `Roles.PlatformAdmin` · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Platform-admin bearer token held |
| **Test Data** | `identifier` = `"a"` followed by 50 `"b"` characters (51 chars total: `abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb`); other fields as TC-TENANT-API-001 with `adminEmail=len51@batchd.test` |
| **Steps** | 1. `POST /api/tenants` with a 51-character `identifier`. 2. Read status, `content-type`, the `errors` extension and whether a `code` extension exists. 3. `SELECT count(*) FROM saas.tenant WHERE length(identifier)>50`. |
| **Expected UI** | The SPA submit button is **not** disabled for this input — its pattern `^[a-z0-9][a-z0-9-]{1,62}$` (`tenants.component.ts:117`) permits up to 63 characters — so the request is sent and the error surfaces in `.error` as `Validation failed.` |
| **Expected API** | `400 Bad Request`, `application/problem+json`, `title = "Validation failed."`, `errors` contains the key `Identifier`; **no `code` extension is present** — the `ValidationException` arm sets only `errors` (`DomainExceptionHandler.cs:34-44`). The domain code `TENANT-002` is **not** returned: `RuleFor(x => x.Identifier).NotEmpty().MaximumLength(TenantSlug.MaxLength)` (`ProvisionTenant.cs:26`) short-circuits before `TenantSlug.Create`. |
| **Expected DB** | Step 3 returns **0** — the column is `character varying(50)` (measured on `saas.tenant`), so an over-long value could not be stored even if the validator were removed. |
| **Expected Audit** | No `audit.audit_trail`, `audit.field_change` or `qams.outbox_event` row is written. |
| **Expected Notification** | n/a — validator rejections raise no notification. |
| **Cleanup** | None required. |
| **Evidence** | HTTP response capture including the full `errors` object · SQL result |
| **Result / Defect** | Not Run · — |
| **Notes** | Pair with TC-TENANT-API-005 (50 chars, accepted) and TC-TENANT-API-006 (1 char, `TENANT-002`) to complete the boundary triple. The two validation layers disagree about which one owns length: the validator caps at 50, and `TenantSlug.Create` re-checks `normalized.Length > MaxLength` (`TenantSlug.cs:30`) — so `TENANT-002` for over-length is reachable only from a non-HTTP caller. |

#### TC-TENANT-API-005 — A slug of exactly 50 characters provisions successfully  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-006 |
| **Level / Type / Technique** | API · Functional (positive) · BVA — exactly at `TenantSlug.MaxLength` (`TenantSlug.cs:12`) |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional + real PostgreSQL) |
| **Role / Permission / Tenant** | PlatformAdmin · role gate `Roles.PlatformAdmin` · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | The 50-character slug below is unused |
| **Test Data** | `identifier` = `a` + 49 × `b` = `abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb` (50 chars); `name=Batch D Fifty Char Laboratory`; `adminEmail=len50@batchd.test`; `adminDisplayName=Fifty Admin`; `adminPassword=Fifty-Batch-D-2026!` |
| **Steps** | 1. `POST /api/tenants` with the 50-character `identifier`. 2. Read status and body. 3. `SELECT identifier, length(identifier), status FROM saas.tenant WHERE identifier = <the 50-char value>`. 4. `GET /api/auth/workspace/<the 50-char value>` anonymously. |
| **Expected UI** | The confirmation note renders the full 50-character identifier without truncation; the new row appears in the platform tenant table. |
| **Expected API** | Step 1: `201 Created`, body `{"id":"<uuid>"}`. Step 4: `200` with `{"name":"Batch D Fifty Char Laboratory"}` — the tenant is `Active`, so the anonymous lookup resolves (`src/NT.QAMS.Application/Tenancy/Queries/GetWorkspace.cs:44-46`). |
| **Expected DB** | One row; `length(identifier)=50`; `status='Active'`. The regex `^[a-z0-9](?:-?[a-z0-9]){1,49}$` (`TenantSlug.cs:47`) admits this input as 1 leading char + 49 single-character groups. |
| **Expected Audit** | One `audit.audit_trail` row `NT.QAMS.Domain.Tenancy.TenantProvisioned, NT.QAMS.Domain` with `payload::jsonb->>'slug'` equal to the 50-character value. |
| **Expected Notification** | As TC-TENANT-API-001 — the single `TenantProvisioned` handler fires. |
| **Cleanup** | Delete the tenant and its dependents in the order given in TC-TENANT-API-001. |
| **Evidence** | HTTP response captures for steps 1 and 4 · SQL result |
| **Result / Defect** | Not Run · — |
| **Notes** | The upper boundary is the same number on both layers (validator `MaximumLength(50)`, regex ceiling 50), so 50 must pass and 51 must fail — a single off-by-one in either would be caught by this pair. The SPA pattern permits 63 characters (`tenants.component.ts:117`) and therefore does not protect this boundary. |

#### TC-TENANT-API-006 — A one-character slug is refused with `TENANT-002` at 422, not by the validator  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-006 |
| **Level / Type / Technique** | API · Functional (negative) · BVA — one character below the regex's two-character floor |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | PlatformAdmin · role gate `Roles.PlatformAdmin` · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Platform-admin bearer token held |
| **Test Data** | `{"identifier":"a","name":"Batch D One Char Laboratory","adminEmail":"len1@batchd.test","adminDisplayName":"One Admin","adminPassword":"One-Batch-D-2026!"}` |
| **Steps** | 1. `POST /api/tenants` with `identifier="a"`. 2. Read status, `content-type`, `code` and `title`. 3. Repeat with `identifier="ab"` and read the status. 4. `SELECT count(*) FROM saas.tenant WHERE identifier IN ('a','ab')`. |
| **Expected UI** | The SPA blocks submission of `"a"` client-side — its pattern requires at least two characters (`^[a-z0-9][a-z0-9-]{1,62}$`, `tenants.component.ts:117`) — so this case must be driven at the API, not through the form. |
| **Expected API** | Step 1: `422 Unprocessable Entity`, `application/problem+json`, `code = "TENANT-002"`, `title = "Tenant identifier must be 2-50 chars of lowercase letters, digits and single hyphens, starting and ending with a letter or digit."` (`TenantSlug.cs:30-35`). It is **not** 400: `NotEmpty()` passes for `"a"` and `MaximumLength(50)` passes, so the validator lets it through to the domain. Step 3: `201 Created` — `"ab"` matches the regex at its two-character floor. |
| **Expected DB** | Step 4 returns **1** (only `ab`, from step 3). |
| **Expected Audit** | Step 1 writes nothing. Step 3 writes one `TenantProvisioned` `audit.audit_trail` row. |
| **Expected Notification** | Step 1: none. Step 3: the `TenantProvisioned` handler fires. |
| **Cleanup** | Delete the `ab` tenant and its dependents. |
| **Evidence** | Two HTTP response captures · SQL result |
| **Result / Defect** | Not Run · — |
| **Notes** | The existing `TenantTests.Slug_rejects_invalid_identifiers` theory includes `"a"` but asserts only `Throw<DomainException>()` without pinning the code, and never reaches HTTP. This case fixes both the code and the status. |

#### TC-TENANT-API-007 — A slug with surrounding whitespace and mixed case is normalised before storage  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-006 |
| **Level / Type / Technique** | API · Functional (positive) · Statement/Branch — both statements of `TenantSlug.Create`'s normalisation line (`TenantSlug.cs:28`: `Trim()` then `ToLowerInvariant()`) |
| **Priority / Severity / Automation** | High · Medium · Yes (functional + real PostgreSQL) |
| **Role / Permission / Tenant** | PlatformAdmin · role gate `Roles.PlatformAdmin` · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `amman-central-lab` is unused: `SELECT count(*) FROM saas.tenant WHERE identifier='amman-central-lab'` returns 0 |
| **Test Data** | `{"identifier":"  Amman-Central-Lab  ","name":"  Amman Central Laboratory  ","adminEmail":"admin@amman.test","adminDisplayName":"Amman Admin","adminPassword":"Amman-Batch-D-2026!"}` |
| **Steps** | 1. `POST /api/tenants` with the body above (21-character raw `identifier`, leading and trailing double spaces). 2. Read status. 3. `SELECT identifier, name, length(identifier), length(name) FROM saas.tenant WHERE identifier='amman-central-lab'`. 4. `GET /api/auth/workspace/amman-central-lab` anonymously. 5. `GET /api/auth/workspace/Amman-Central-Lab` anonymously. |
| **Expected UI** | Navigating to `/t/amman-central-lab` pins the slug and redirects to `/login` (`frontend/src/app/features/login/tenant-entry.component.ts:23-30`); the sign-in page greets `Amman Central Laboratory`. |
| **Expected API** | Step 2: `201 Created`. Step 4: `200` `{"name":"Amman Central Laboratory"}`. Step 5: **also `200`** with the same body — `GetWorkspaceQueryHandler` runs the raw route value through `TenantSlug.Create` (`GetWorkspace.cs:35`), which normalises it identically. |
| **Expected DB** | Exactly one row: `identifier='amman-central-lab'` with `length=17` (**not** 21 — the raw whitespace is trimmed), `name='Amman Central Laboratory'` with `length=24` (**not** 28 — `Tenant.Provision` stores `name.Trim()`, `src/NT.QAMS.Domain/Tenancy/Tenant.cs:53`). |
| **Expected Audit** | The `TenantProvisioned` payload carries the **normalised** slug: `payload::jsonb->>'slug' = 'amman-central-lab'` and `payload::jsonb->>'name' = 'Amman Central Laboratory'` (`Tenant.cs:54`). |
| **Expected Notification** | The `TenantProvisioned` handler fires once. |
| **Cleanup** | Delete the `amman-central-lab` tenant and its dependents. |
| **Evidence** | HTTP captures for steps 1, 4, 5 · SQL result showing both lengths |
| **Result / Defect** | Not Run · — |
| **Notes** | `Validators.pattern` in the SPA (`tenants.component.ts:117`) rejects both the leading space and the upper-case letters, so this case is API-only. That is itself a client/server divergence — the API accepts input the form cannot express (see GAP-TENANT-903). Note the validator's `MaximumLength(50)` measures the **raw** 21-character string, before trimming. |

#### TC-TENANT-API-008 — Every malformed-slug character-class partition returns `TENANT-002` at 422  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-006 |
| **Level / Type / Technique** | API · Functional (negative) · Equivalence Partitioning — one representative per rejected character-class partition of `^[a-z0-9](?:-?[a-z0-9]){1,49}$` |
| **Priority / Severity / Automation** | High · Medium · Yes (functional, data-driven) |
| **Role / Permission / Tenant** | PlatformAdmin · role gate `Roles.PlatformAdmin` · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Platform-admin bearer token held; none of the six probe values exists in `saas.tenant` |
| **Test Data** | Six `identifier` values, all with `name=Malformed Slug Probe`, `adminEmail=<n>@batchd.test`, `adminDisplayName=Probe Admin`, `adminPassword=Probe-Batch-D-2026!`: (a) `-amman` leading hyphen · (b) `amman-` trailing hyphen · (c) `am--man` consecutive hyphens · (d) `amman_lab` underscore · (e) `Amman Lab` embedded space · (f) `ammán-lab` non-ASCII letter |
| **Steps** | 1. For each of (a)…(f), `POST /api/tenants` and record status, `code` and `title`. 2. After all six, `SELECT count(*) FROM saas.tenant WHERE name='Malformed Slug Probe'`. 3. `SELECT count(*) FROM qams.user_account WHERE email LIKE '%@batchd.test'`. |
| **Expected UI** | The SPA form blocks (a), (d), (e) and (f) client-side but **accepts (b) and (c)** — `[a-z0-9-]{1,62}` permits both a trailing hyphen and consecutive hyphens (`tenants.component.ts:117`) — so for those two the request reaches the server and the `.error` div shows the `TENANT-002` message. |
| **Expected API** | All six: `422 Unprocessable Entity`, `application/problem+json`, `code = "TENANT-002"`, identical `title` (`TenantSlug.cs:32-34`). Note (b) and (c) never see 400: `NotEmpty()` and `MaximumLength(50)` both pass, so only the domain regex refuses them. |
| **Expected DB** | Step 2 returns **0**; step 3 returns **0** — six rejections, zero rows anywhere. |
| **Expected Audit** | No `audit.audit_trail`, `audit.field_change` or `qams.outbox_event` row for any of the six. |
| **Expected Notification** | n/a — rejected provisioning raises no notification. |
| **Cleanup** | None required; confirm with steps 2 and 3. |
| **Evidence** | Six HTTP response captures in one table · two SQL results |
| **Result / Defect** | Not Run · — |
| **Notes** | `TenantTests.Slug_rejects_invalid_identifiers` already covers `-leading`, `trailing-`, `no--double`, `has space`, `UPPER_underscore` at unit level but asserts only `Throw<DomainException>()` — no code, no status, no HTTP. Partition (f) `ammán-lab` is **new**: no existing test exercises a non-ASCII letter, and `ToLowerInvariant()` leaves `á` intact so the regex is the only thing that stops it. |

#### TC-TENANT-API-009 — A 201-character tenant name is refused with 400; `TENANT-004` is unreachable over HTTP  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-003 |
| **Level / Type / Technique** | API · Functional (negative) · BVA — one character above `Tenant.MaxNameLength` (`Tenant.cs:13`) |
| **Priority / Severity / Automation** | Medium · Low · Yes (functional) |
| **Role / Permission / Tenant** | PlatformAdmin · role gate `Roles.PlatformAdmin` · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Platform-admin bearer token held |
| **Test Data** | `identifier=batchd-name-009`; `name` = the letter `N` repeated **201** times; then a second request with `name` = `N` × **200**; `adminEmail=name009@batchd.test`; `adminDisplayName=Name Admin`; `adminPassword=Name-Batch-D-2026!` |
| **Steps** | 1. `POST /api/tenants` with the 201-character name. 2. Read status, `errors` and whether `code` is present. 3. `POST /api/tenants` with the 200-character name (change `identifier` to `batchd-name-009b`). 4. Read status. 5. `SELECT length(name) FROM saas.tenant WHERE identifier='batchd-name-009b'`. |
| **Expected UI** | The SPA blocks the 201-character name client-side (`Validators.maxLength(200)`, `tenants.component.ts:118`), so step 1 is API-only; step 3 submits normally. |
| **Expected API** | Step 2: `400 Bad Request`, `title = "Validation failed."`, `errors` contains key `Name`, **no `code` extension**. Step 4: `201 Created`. |
| **Expected DB** | Step 5 returns **200**; the `name` column is `character varying(200)` (measured). |
| **Expected Audit** | Step 1 writes nothing; step 3 writes one `TenantProvisioned` `audit.audit_trail` row. |
| **Expected Notification** | Step 3 only — the `TenantProvisioned` handler fires once. |
| **Cleanup** | Delete the `batchd-name-009b` tenant and its dependents. |
| **Evidence** | Two HTTP captures · SQL length result |
| **Result / Defect** | Not Run · — |
| **Notes** | **`TENANT-004` cannot be produced through this endpoint.** `RuleFor(x => x.Name).MaximumLength(200)` (`ProvisionTenant.cs:27`) measures the raw string, and `Tenant.Provision` only throws when `name.Trim().Length > 200` (`Tenant.cs:48-51`) — any input long enough to trip the domain has already tripped the validator. The same argument makes `TENANT-003` unreachable: FluentValidation's `NotEmpty()` treats a whitespace-only string as empty. Both codes are therefore Unit-level only (batch A), which is a coverage fact this batch records rather than a defect. |

#### TC-TENANT-API-010 — An 11-character administrator password is refused; the SPA's floor of 10 disagrees with the API's 12  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-002, URS-009 · RSK-TENANT-003 · **GAP-TENANT-904** |
| **Level / Type / Technique** | API · Functional (negative) + client/server contract · BVA — one character below `PasswordRules.MinLength` |
| **Priority / Severity / Automation** | High · Medium · Yes (functional + Karma spec on the form) |
| **Role / Permission / Tenant** | PlatformAdmin · role gate `Roles.PlatformAdmin` · none |
| **Environment** | API `:5080` Development + SPA `:4200` (started via `scripts/dev-up.ps1`) |
| **Preconditions** | Platform-admin session in the SPA at `/platform/tenants`; `batchd-pw-010` unused |
| **Test Data** | (i) API: `adminPassword="Str0ng-Pass"` (11 chars, all four character classes) · (ii) API: `adminPassword="Str0ng-Passw"` (12 chars) · (iii) SPA form: `adminPassword="Str0ng-Pas"` (10 chars). All with `identifier=batchd-pw-010`, `name=Batch D Password Laboratory`, `adminEmail=pw010@batchd.test`, `adminDisplayName=PW Admin` |
| **Steps** | 1. `POST /api/tenants` with (i). 2. Read status and `errors`. 3. `POST /api/tenants` with (ii). 4. Read status. 5. In the SPA drawer, enter (iii) with the other fields and observe whether the submit button is enabled. 6. If enabled, submit and read the rendered `.error` text. |
| **Expected UI** | Step 5: the submit button is **enabled** — the reactive form requires only `Validators.minLength(10)` (`tenants.component.ts:121`). Step 6: the request is sent and rejected; `.error` renders `Validation failed.` (the problem `title`), which does **not** tell the operator that the password is too short — the per-field `errors` object is discarded by `describe()` (`tenants.component.ts:160-165`). |
| **Expected API** | Step 2: `400 Bad Request`, `errors["AdminPassword"]` contains `"The password must be at least 12 characters."` (`src/NT.QAMS.Application/IdentityAccess/PasswordRules.cs:17,47-48`, applied at `ProvisionTenant.cs:30`). Step 4: `201 Created`. Step 6: `400 Bad Request` with the same `errors` payload. |
| **Expected DB** | After step 4 exactly one `saas.tenant` row for `batchd-pw-010`; after steps 1 and 6, no additional rows and no `qams.user_account` row for `pw010@batchd.test` beyond the one created by step 3. |
| **Expected Audit** | Only step 3 writes a `TenantProvisioned` `audit.audit_trail` row. No `audit.field_change` row ever records the password value — `FieldChangeInterceptor.IsSensitive` redacts any property whose name contains `password` or `hash` (`src/NT.QAMS.Infrastructure/Persistence/Interceptors/FieldChangeInterceptor.cs:34,93-97`). |
| **Expected Notification** | Step 3 only. |
| **Cleanup** | Delete the `batchd-pw-010` tenant and its dependents. |
| **Evidence** | Three HTTP captures · SPA screenshot of the enabled submit button with a 10-character password · the redaction check on `audit.field_change` |
| **Result / Defect** | Not Run · — |
| **Notes** | Raises **GAP-TENANT-904**. Two further divergences in the same form are in scope for that gap and asserted here as observations, not as separate cases: `adminDisplayName` is `maxLength(200)` client-side (`:120`) against `MaximumLength(150)` server-side (`ProvisionTenant.cs:29`); and the form applies no complexity or breach-list check at all, so a 12-character all-lower-case password passes the form and fails the API. |

#### TC-TENANT-API-011 — A tenant administrator is refused the control plane with 403 `AUTHZ-403`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-005, URS-008 · RSK-TENANT-002 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — row `POST /api/tenants` × actor `TenantAdmin` of §4.5 |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional) |
| **Role / Permission / Tenant** | TenantAdmin · refused by the class gate `[Authorize(Roles = Roles.PlatformAdmin)]` (`TenantsController.cs:12`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Signed in at `localhost:4200/t/demo-lab` as `admin@demo-lab.local` / `Demo-Admin-Pass-2!`; the JWT carries `role=TenantAdmin` and a `tenant_id` claim |
| **Test Data** | `{"identifier":"batchd-escalate-011","name":"Escalation Probe","adminEmail":"esc@batchd.test","adminDisplayName":"Esc Admin","adminPassword":"Esc-Batch-D-2026!"}` |
| **Steps** | 1. `POST /api/tenants` with the tenant-admin bearer token. 2. Read status, `content-type`, `code`. 3. `GET /api/tenants` with the same token; read status and `code`. 4. `SELECT count(*) FROM saas.tenant WHERE identifier='batchd-escalate-011'`. 5. In the SPA, navigate to `/platform/tenants` while signed in as the tenant administrator. |
| **Expected UI** | Step 5: `platformOnlyGuard` redirects to `/dashboard` — the control-plane screen never loads (`frontend/src/app/core/role.guard.ts:11-15`). |
| **Expected API** | Steps 2 and 3: `403 Forbidden`, `application/problem+json`, `code = "AUTHZ-403"`, `title = "You do not have permission to perform this action."` — emitted by `ProblemAuthorizationResultHandler.HandleAsync` on the framework's `Forbidden` result (`src/NT.QAMS.WebApi/Middleware/ProblemAuthorizationResultHandler.cs:16,27-32`). The body discloses **nothing** about whether tenants exist. |
| **Expected DB** | Step 4 returns **0**. `saas.tenant` is not RLS-protected (measured `relrowsecurity=f`), so the role gate is the *only* thing standing between a tenant administrator and the whole tenant list — which is exactly why this case is Critical. |
| **Expected Audit** | No `audit.audit_trail` row. Confirm whether an `audit.security_event` row is written for the refusal and record the finding either way — the HTTP-layer role gate runs before any handler and is not shown to log. |
| **Expected Notification** | n/a — an authorization refusal raises no notification. |
| **Cleanup** | None required. |
| **Evidence** | Two HTTP response captures · SQL count · SPA redirect screenshot |
| **Result / Defect** | Not Run · — |
| **Notes** | Because the request never reaches MediatR, the *second* gate — `[RequireRole(UserRole.PlatformAdmin)]` on the command (`ProvisionTenant.cs:17`), which would raise `AUTHZ-002` at 403 via `AuthorizationBehavior.cs:76,83` — is not exercised here. To exercise it independently, dispatch `ProvisionTenantCommand` through `ISender` in an Application-level test with `ICurrentUser.Role = UserRole.TenantAdmin`; that belongs to batch A. GAP-TENANT-010 records that this module is guarded by two different mechanisms. |

#### TC-TENANT-API-012 — An anonymous caller is challenged with 401 `AUTH-401` on both control-plane routes  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-005 · RSK-TENANT-002 |
| **Level / Type / Technique** | API · Security (negative) · Equivalence Partitioning — the "no credential" partition against both actions of the gated controller |
| **Priority / Severity / Automation** | Critical · High · Yes (functional) |
| **Role / Permission / Tenant** | anonymous · n/a — no credential presented · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | No `Authorization` header and no `qams_rt` cookie on the request |
| **Test Data** | Two requests: `GET /api/tenants`; `POST /api/tenants` with `{"identifier":"batchd-anon-012","name":"Anon Probe","adminEmail":"anon@batchd.test","adminDisplayName":"Anon","adminPassword":"Anon-Batch-D-2026!"}` |
| **Steps** | 1. `GET /api/tenants` with no credential; record status, `code`, `WWW-Authenticate`. 2. `POST /api/tenants` with no credential; record the same. 3. `GET /api/v1/tenants` with no credential; record status. 4. `SELECT count(*) FROM saas.tenant WHERE identifier='batchd-anon-012'`. |
| **Expected UI** | The SPA never issues these calls unauthenticated — `authGuard` gates the whole shell (`frontend/src/app/app.routes.ts:23`) — so this case is API-only. |
| **Expected API** | Steps 1, 2 and 3: `401 Unauthorized`, `application/problem+json`, `code = "AUTH-401"`, `title = "Authentication is required."`, with the JWT scheme's `WWW-Authenticate: Bearer` header preserved (`ProblemAuthorizationResultHandler.cs:19,35-48`). Not 403 — the challenge branch, not the forbid branch. |
| **Expected DB** | Step 4 returns **0**. |
| **Expected Audit** | No `audit.audit_trail` row. `audit.security_event` may receive a row with `tenant_id IS NULL`; if so it is writable under the relaxed `audit` `WITH CHECK` and readable **only** under `app.bypass_rls='on'` — record which. |
| **Expected Notification** | n/a — an unauthenticated challenge raises no notification. |
| **Cleanup** | None required. |
| **Evidence** | Three HTTP response captures including headers · SQL count |
| **Result / Defect** | Not Run · — |
| **Notes** | Run this **before** any credential-burst probe in the same session: the auth rate-limit partition is 10 requests/minute and poisoning it changes the observed status from 401 to 429. |

#### TC-TENANT-API-013 — The tenant list returns every tenant regardless of status, with no pagination envelope  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-004 |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — the platform operator's tenant inventory |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional + real PostgreSQL) |
| **Role / Permission / Tenant** | PlatformAdmin · role gate `Roles.PlatformAdmin` · none |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SELECT status, count(*) FROM saas.tenant GROUP BY 1` recorded immediately before the run (measured 2026-08-01: `Active|23`, no other value) |
| **Test Data** | None — a read of live control-plane state |
| **Steps** | 1. `GET /api/tenants` as platform admin. 2. Read status and body shape. 3. Count the array elements and compare with the SQL total from the precondition. 4. Confirm each element carries exactly `id`, `identifier`, `name`, `status`, `createdAtUtc`. 5. Confirm the array is ordered by `name` ascending. 6. Confirm the body is a bare JSON array, not a `{items, page, pageSize, total}` envelope. |
| **Expected UI** | `/platform/tenants` renders one table row per element with columns identifier / name / status pill / created date (`tenants.component.ts:74-90`). |
| **Expected API** | `200 OK`; a bare JSON array of `TenantDto` (`src/NT.QAMS.Contracts/Tenancy/TenancyContracts.cs:17-22`); element count equal to `SELECT count(*) FROM saas.tenant`; ordered by `name` (`src/NT.QAMS.Application/Tenancy/Queries/GetTenants.cs:18`); `status` rendered as the **string** form of the enum (`GetTenants.cs:23`). |
| **Expected DB** | No write. `saas.tenant` is read with `AsNoTracking()` and **no status predicate** (`GetTenants.cs:16-25`), so a `Suspended` or `Terminated` tenant appears in the list — deliberate, and asserted rather than assumed. |
| **Expected Audit** | No `audit.field_change` row — reads are not audited. |
| **Expected Notification** | n/a — a list read raises no notification. |
| **Cleanup** | None — read-only. |
| **Evidence** | HTTP response body · the precondition SQL result for comparison |
| **Result / Defect** | Not Run · — |
| **Notes** | The absence of a pagination envelope is a real divergence from the v1.42 pagination contract, but this endpoint predates it and is platform-only; recorded here as an observation for the API module rather than raised as a tenancy gap. The "no status filter" behaviour is what makes TC-TENANT-STATE-011's assertion possible. |

#### TC-TENANT-API-014 — The `/api/v{version}` mirror behaves identically to the unversioned control-plane route  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-002 |
| **Level / Type / Technique** | API · Functional (positive + negative) · Pairwise — route form {`/api/…`, `/api/v1/…`} × actor {PlatformAdmin, TenantAdmin} × action {GET list, POST provision} |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | PlatformAdmin and TenantAdmin · role gate `Roles.PlatformAdmin` · none / `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Both bearer tokens held; `batchd-ver-014` unused |
| **Test Data** | Provision body with `identifier=batchd-ver-014`, `name=Batch D Version Laboratory`, `adminEmail=ver014@batchd.test`, `adminDisplayName=Ver Admin`, `adminPassword=Ver-Batch-D-2026!` |
| **Steps** | 1. `GET /api/tenants` as PlatformAdmin. 2. `GET /api/v1/tenants` as PlatformAdmin. 3. `GET /api/v1/tenants` as TenantAdmin. 4. `POST /api/v1/tenants` as TenantAdmin. 5. `POST /api/v1/tenants` as PlatformAdmin with the test data. 6. Diff the bodies of steps 1 and 2 byte for byte after normalising `createdAtUtc` ordering. 7. `grep -n "tenants" tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt`. |
| **Expected UI** | n/a — the SPA calls only the unversioned routes; this is an API-contract case. |
| **Expected API** | Steps 1 and 2: both `200`, identical bodies. Step 3: `403` `AUTHZ-403`. Step 4: `403` `AUTHZ-403` — the class gate applies to the mirror because `Asp.Versioning.Mvc` dual-exposes the *same* controller. Step 5: `201 Created` with `Location` pointing at the versioned list route (assert the exact value; it inherits GAP-TENANT-005). Step 7: the approved surface lists `GET /api/tenants` (line 126), `GET /api/v{version}/tenants` (line 236), `POST /api/tenants` (line 419), `POST /api/v{version}/tenants` (line 612) — four entries, no `{id}` route. |
| **Expected DB** | Exactly one `saas.tenant` row for `batchd-ver-014` after step 5; nothing after steps 3 and 4. |
| **Expected Audit** | One `TenantProvisioned` `audit.audit_trail` row from step 5 only. |
| **Expected Notification** | Step 5 only. |
| **Cleanup** | Delete the `batchd-ver-014` tenant and its dependents. |
| **Evidence** | Six HTTP captures · the body diff · the `ApiSurface.approved.txt` grep output |
| **Result / Defect** | Not Run · — |
| **Notes** | Line numbers in `ApiSurface.approved.txt` shifted between the front matter's inspection and this one because migration `20260801131521_QualityHealthProfile` added routes; assert on the **route strings**, never on line numbers. Any change to this surface must update the approved file in the same commit or the merge gate fails. |

---

## Tenant settings — `GET` / `PUT /api/tenant-settings/mfa-policy`

#### TC-TENANT-API-015 — A tenant administrator reads their own MFA policy and sees the default `false`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-004 · RSK-TENANT-005 |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — the administrator inspects the tenant's own security setting |
| **Priority / Severity / Automation** | High · Medium · Yes (functional + real PostgreSQL) |
| **Role / Permission / Tenant** | TenantAdmin · **`tenant-settings.manage`** (`[RequirePermission(PermissionCatalog.TenantSettings, PermissionAction.Manage)]` at class level, `src/NT.QAMS.WebApi/Controllers/TenantSettingsController.cs:18`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Signed in as `admin@demo-lab.local`; `SELECT require_mfa_privileged FROM saas.tenant WHERE identifier='demo-lab'` returns `f`; the account holds the seeded `Tenant Administrator` role, which carries all 171 keys |
| **Test Data** | None — a read |
| **Steps** | 1. `GET /api/tenant-settings/mfa-policy` with the tenant-admin bearer token. 2. Read status and body. 3. `SELECT require_mfa_privileged FROM saas.tenant WHERE identifier='demo-lab'` and compare. |
| **Expected UI** | `/settings/security` shows the "require MFA for privileged users" control in the off position. |
| **Expected API** | `200 OK`; body `{"requireMfaForPrivilegedRoles":false}` (`TenantMfaPolicyDto`, `TenancyContracts.cs:11`; wrapped at `TenantSettingsController.cs:24`). |
| **Expected DB** | `saas.tenant.require_mfa_privileged` is `false` and **unchanged** — the handler reads `AsNoTracking()` and keys strictly on `ICurrentTenant.TenantId`, never on a caller-supplied id (`src/NT.QAMS.Application/Tenancy/Commands/TenantMfaPolicy.cs:19-22`). |
| **Expected Audit** | No `audit.field_change` row — the read is `AsNoTracking()` and the change tracker sees nothing. |
| **Expected Notification** | n/a — reading a setting raises no notification. |
| **Cleanup** | None — read-only. |
| **Evidence** | HTTP response capture · SQL result |
| **Result / Defect** | Not Run · — |
| **Notes** | `saas.tenant` carries **no** RLS (measured `relrowsecurity=f relforcerowsecurity=f`), so the only thing preventing a tenant administrator from reading another tenant's settings is that the handler keys on `ICurrentTenant.TenantId` rather than on a request parameter. There is no request parameter to tamper with — assert that by confirming the route template carries no id segment. |

#### TC-TENANT-API-016 — Turning the MFA policy on returns 204 and writes an attributed field-change row  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-004, URS-106 · RSK-TENANT-005 |
| **Level / Type / Technique** | API · Functional (positive) · Use Case — the tenant opts in to enforced MFA for privileged users |
| **Priority / Severity / Automation** | High · High · Yes (functional + real PostgreSQL) |
| **Role / Permission / Tenant** | TenantAdmin · `tenant-settings.manage` (HTTP) + `[RequireInternalActor]` (command, `TenantMfaPolicy.cs:11`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `require_mfa_privileged = false` for `demo-lab`; record `SELECT count(*) FROM audit.field_change WHERE entity_type='TenantSettings' AND action='Modified'` (measured 2026-08-01: **0** — no such row has ever been written in dev) |
| **Test Data** | `PUT` body `{"require":true}` (`SetTenantMfaPolicyRequest`, `TenancyContracts.cs:14`) |
| **Steps** | 1. `PUT /api/tenant-settings/mfa-policy` with `{"require":true}` and the tenant-admin token. 2. Read status and body length. 3. `SELECT require_mfa_privileged FROM saas.tenant WHERE identifier='demo-lab'`. 4. `GET /api/tenant-settings/mfa-policy` and read the body. 5. `SELECT set_config('app.bypass_rls','on',false); SELECT entity_type, entity_id, action, property, old_value, new_value, actor, tenant_id FROM audit.field_change WHERE entity_type='TenantSettings' AND action='Modified' ORDER BY occurred_at_utc DESC LIMIT 1`. 6. `PUT` again with `{"require":false}` and repeat steps 3 and 5. |
| **Expected UI** | The security-settings toggle moves to on and stays on after a page reload; no page-level error is shown. |
| **Expected API** | Step 2: `204 No Content` with an empty body (`TenantSettingsController.cs:31`). Step 4: `200` with `{"requireMfaForPrivilegedRoles":true}`. Step 6: `204` again, then `{"requireMfaForPrivilegedRoles":false}`. |
| **Expected DB** | Step 3: `require_mfa_privileged = t`. After step 6: `f`. The write goes through `Tenant.SetPrivilegedMfaPolicy` (`Tenant.cs:107-108`), a `with`-expression replacement of the owned `TenantSettings` record — the other five columns (`password_expiry_days=90`, `calibration_reminder_days=30`, `sop_expiry_reminder_months=3`, `default_language='en'`, `time_zone='UTC'`) must be **unchanged**; assert all five. |
| **Expected Audit** | Step 5 returns exactly one **new** row: `entity_type='TenantSettings'`, `action='Modified'`, `property='RequireMfaForPrivilegedRoles'`, `old_value='False'`, `new_value='True'`, `actor` = the administrator's display name, `tenant_id` = the `demo-lab` id (the request tenant, via `FieldChangeInterceptor.TenantOf`, `FieldChangeInterceptor.cs:101-135`). No `X-Change-Reason` header is required — `ChangeReasonMiddleware` demands one only on `DELETE` (`src/NT.QAMS.WebApi/Middleware/RequestIdentity.cs:149-156`), so `reason` will be empty; assert that and record it. |
| **Expected Notification** | n/a — no `NotificationPolicies` handler subscribes to a settings change; there is no `TenantSettingsChanged` domain event (`Tenant.cs:107-108` raises nothing). |
| **Cleanup** | Step 6 restores `require_mfa_privileged=false`. Do **not** delete the resulting `audit.field_change` rows — the ledger is append-only by design. |
| **Evidence** | Four HTTP captures · three SQL result sets · the before/after `audit.field_change` counts |
| **Result / Defect** | Not Run · — |
| **Notes** | The measured baseline of **zero** `TenantSettings`/`Modified` rows means this audit assertion has never been exercised in dev; treat a null or missing row as a finding, not as an environment quirk. `SetPrivilegedMfaPolicy` has **no state guard** — see TC-TENANT-STATE-008. Turning the policy on will force MFA enrolment for `demo-lab`'s privileged users on their next sign-in (`MfaEnrollmentGateMiddleware`, `RequestIdentity.cs:170-200`), so step 6 must run in the same session. |

#### TC-TENANT-API-017 — A quality manager is refused both MFA-policy actions with 403 `AUTHZ-403`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-004, URS-005 · RSK-TENANT-005 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — row `tenant-settings` × actor `QualityManager` of §4.5 |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | QualityManager (seeded role) · **lacks** `tenant-settings.manage` — `SystemRoleCatalog` excludes the whole `TenantSettings` module from the Quality Manager grant (`src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs:108-111`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | A `demo-lab` account assigned the seeded `Quality Manager` role; verify with `SELECT count(*) FROM qams.role_permission rp JOIN qams.role r ON r.id=rp.role_id WHERE r.name='Quality Manager' AND rp.permission_key LIKE 'tenant-settings.%'` → **0** |
| **Test Data** | `PUT` body `{"require":true}` |
| **Steps** | 1. `GET /api/tenant-settings/mfa-policy` as the quality manager; read status, `content-type`, `code`. 2. `PUT /api/tenant-settings/mfa-policy` with `{"require":true}`; read the same. 3. `SELECT require_mfa_privileged FROM saas.tenant WHERE identifier='demo-lab'`. |
| **Expected UI** | The security-settings screen either hides the control or renders it disabled via `PermissionsService`; a direct navigation must not produce a populated but non-functional form. |
| **Expected API** | Both steps: `403 Forbidden`, `application/problem+json`, `code = "AUTHZ-403"`, `title = "You do not have permission to perform this action."` — from `RequirePermissionAttribute.OnAuthorizationAsync` (`src/NT.QAMS.WebApi/Authorization/RequirePermissionAttribute.cs:49-59`), because `RequestPrivileges.Has("tenant-settings.manage")` is false (`src/NT.QAMS.Infrastructure/Authorization/PrivilegeResolution.cs:39`). |
| **Expected DB** | Step 3: unchanged at `f`. No row of `saas.tenant` is touched — the filter runs before the controller action. |
| **Expected Audit** | No `audit.field_change` row for `TenantSettings`. |
| **Expected Notification** | n/a — an authorization refusal raises no notification. |
| **Cleanup** | None required. |
| **Evidence** | Two HTTP response captures · SQL result |
| **Result / Defect** | Not Run · — |
| **Notes** | Pinned at unit level by `tests/NT.QAMS.Application.UnitTests/Authorization/SystemRoleCatalogTests.cs:80`; this case adds the HTTP status, media type and code. The refusal path differs from TC-TENANT-API-011's: that one is the framework `Forbidden` result, this one is the `[RequirePermission]` filter writing directly — GAP-TENANT-010 requires both to emit the same shape, and running the two cases together is the proof. |

#### TC-TENANT-API-018 — A platform administrator passes the permission gate and fails in the handler with 422 `TENANT-000`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-004, URS-005 · RSK-TENANT-002 · **GAP-TENANT-902** |
| **Level / Type / Technique** | API · Security (negative) · Multiple-Condition — both operands of `Has(key) = IsPlatformAdmin ∨ Permissions.Contains(key)` (`PrivilegeResolution.cs:39`) against a null request tenant |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | PlatformAdmin · `[RequirePermission(TenantSettings, Manage)]` is **satisfied** by the platform-admin short circuit · **none** — the platform token carries no `tenant_id` claim (`SecurityAdapters.cs:92-95`; verified `SELECT tenant_id IS NULL FROM qams.user_account WHERE email='platform-admin@localhost'` → `t`) |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Signed in as `platform-admin@localhost` / `Dev-Only-Platform-Pass-1!` with **no** tenant slug on the login body |
| **Test Data** | `PUT` body `{"require":true}` |
| **Steps** | 1. Decode the platform token and confirm there is no `tenant_id` claim. 2. `GET /api/tenant-settings/mfa-policy` with the platform token; read status, `content-type`, `code`, `title`. 3. `PUT /api/tenant-settings/mfa-policy` with `{"require":true}`; read the same. 4. `SELECT identifier, require_mfa_privileged FROM saas.tenant ORDER BY identifier` and confirm no row changed. |
| **Expected UI** | `tenantOnlyGuard` redirects a platform admin away from every tenant screen to `/platform/tenants` (`role.guard.ts:17-21`), so the SPA never reaches this endpoint as a platform admin — the case is API-only. |
| **Expected API** | Steps 2 and 3: **`422 Unprocessable Entity`**, `application/problem+json`, `code = "TENANT-000"`, `title = "No tenant in context."` — **not 403**. The HTTP filter admits the caller because `Has()` returns true unconditionally for a platform admin (`PrivilegeResolution.cs:39`, set at `RequestIdentity.cs:114-117`); the `PUT`'s `[RequireInternalActor]` also admits them because `UserRole.PlatformAdmin != UserRole.ExternalAuditor` (`src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs:75`); the request then dies in the handler on `tenant.TenantId ?? throw` (`TenantMfaPolicy.cs:19` for the GET, `:31` for the PUT). |
| **Expected DB** | Step 4: **no** `saas.tenant` row changed. Critically, no row was *selected* either — the handler throws before its `SingleOrDefaultAsync`, so `TENANT-404` (`TenantMfaPolicy.cs:21,33`) is unreachable from this actor. |
| **Expected Audit** | No `audit.field_change` row. Record whether an `audit.security_event` row is written; if one is, its `tenant_id` will be **NULL** (no request tenant) and therefore invisible to every tenant read. |
| **Expected Notification** | n/a — a failed settings call raises no notification. |
| **Cleanup** | None required. |
| **Evidence** | Decoded JWT claim set · two HTTP response captures · SQL snapshot of all `require_mfa_privileged` values before and after |
| **Result / Defect** | Not Run · — |
| **Notes** | **This corrects the front matter.** `12-module-tenancy-rls.md` §4.5 marks the platform-admin cells `403 †` and labels them `[RNV]` pending resolution of `IUserPrivileges.SetPlatformAdmin`/`Has`. Resolved here by reading `PrivilegeResolution.cs:39`: the answer is `422 TENANT-000`, not 403. Raised as **GAP-TENANT-902** because a platform administrator silently clearing a *permission* gate on a tenant-scoped Administration endpoint is a defence-in-depth weakness that today is stopped only by the absence of a tenant, not by an authorization decision. |

#### TC-TENANT-API-019 — An external auditor granted `tenant-settings.manage` still cannot write, but can read  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-004, URS-005 · RSK-TENANT-005 |
| **Level / Type / Technique** | API · Security (negative + positive) · Decision Table — the defence-in-depth row where the HTTP gate and the command gate deliberately disagree |
| **Priority / Severity / Automation** | High · High · Yes (functional) |
| **Role / Permission / Tenant** | ExternalAuditor · holds `tenant-settings.manage` **by explicit tenant configuration** (not by seeding — `SystemRoleCatalog` excludes it) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | A `demo-lab` account with `user_account.role='ExternalAuditor'` assigned to a role that has been granted `tenant-settings.manage`; verify with `SELECT count(*) FROM qams.role_permission WHERE permission_key='tenant-settings.manage' AND role_id=<the auditor's role id>` → **1**; `require_mfa_privileged='f'` |
| **Test Data** | `PUT` body `{"require":true}` |
| **Steps** | 1. `GET /api/tenant-settings/mfa-policy` as the external auditor; read status and body. 2. `PUT /api/tenant-settings/mfa-policy` with `{"require":true}`; read status, `content-type`, `code`, `title`. 3. `SELECT require_mfa_privileged FROM saas.tenant WHERE identifier='demo-lab'`. 4. Revoke the grant and repeat step 1. |
| **Expected UI** | An external auditor's SPA is read-only by design; verify the toggle renders disabled even while the privilege is granted, and record any disagreement between the screen and the API. |
| **Expected API** | Step 1: **`200 OK`** `{"requireMfaForPrivilegedRoles":false}` — queries are *not* gated by `AuthorizationBehavior` (`AuthorizationBehavior.cs:44-47`), so with the HTTP permission granted, the read succeeds. Step 2: **`403 Forbidden`**, `application/problem+json`, `code = "AUTHZ-002"`, `title = "Role 'ExternalAuditor' is not permitted to execute this action."` — the HTTP gate passes, then `[RequireInternalActor]` refuses the command (`TenantMfaPolicy.cs:11`; decision at `AuthorizationBehavior.cs:75`; throw at `:83`; 403 mapping at `DomainExceptionHandler.cs:63-68`). Step 4: `403` `AUTHZ-403`. |
| **Expected DB** | Step 3: unchanged at `f`. |
| **Expected Audit** | No `audit.field_change` row for `TenantSettings`. Assert the `403 AUTHZ-002` body carries the role name but **no** tenant identifier and no permission key. |
| **Expected Notification** | n/a — an authorization refusal raises no notification. |
| **Cleanup** | Revoke `tenant-settings.manage` from the auditor role (step 4 already does this) and confirm `SELECT count(*) … = 0`. |
| **Evidence** | Four HTTP response captures · SQL results before and after the grant |
| **Result / Defect** | Not Run · — |
| **Notes** | The two codes must not be conflated: `AUTHZ-403` is the HTTP filter, `AUTHZ-002` is the MediatR behaviour, and both map to 403 through different writers. The read/write asymmetry in step 1 versus step 2 is by design (`AuthorizationBehavior.cs:44-47`, "auditors must read") but means a laboratory that grants `tenant-settings.manage` to its auditor role has given away read access to its security posture — worth stating in the release note for the privilege matrix. |

#### TC-TENANT-API-020 — Holding `tenant-settings.view` alone confers nothing; the read still demands `manage`  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-004, URS-005 · RSK-TENANT-005 · **GAP-TENANT-007** |
| **Level / Type / Technique** | API · Security (defect-pinning) · Equivalence Partitioning — the "holds view, lacks manage" partition, which the catalogue creates and no endpoint honours |
| **Priority / Severity / Automation** | Medium · Medium · Yes (functional) |
| **Role / Permission / Tenant** | A tenant-defined role holding exactly one key, `tenant-settings.view` · `[RequirePermission(TenantSettings, Manage)]` at class level covers the GET too (`TenantSettingsController.cs:18,22`) · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Create a `demo-lab` role `Batch D Settings Reader` whose only permission is `tenant-settings.view`; assign a non-auditor account to it; verify `SELECT permission_key FROM qams.role_permission WHERE role_id=<id>` returns exactly one row |
| **Test Data** | None for the GET; `{"require":true}` for the PUT |
| **Steps** | 1. Confirm `tenant-settings.view` is a real key: it is generated into `PermissionCatalog.AllKeys` from `ConfigurationModule = [View, Manage]` (`PermissionCatalog.cs:125-126,188`). 2. `GET /api/tenant-settings/mfa-policy` as the settings-reader; read status and `code`. 3. `PUT /api/tenant-settings/mfa-policy`; read status and `code`. 4. `grep -rn "PermissionCatalog.TenantSettings" src/ --include=*.cs` and classify every hit. |
| **Expected UI** | The privilege-matrix screen displays `tenant-settings.view` as a grantable capability, so an administrator can grant it and reasonably expect read access — the screen and the behaviour disagree. |
| **Expected API** | Step 2: **`403 Forbidden`**, `code = "AUTHZ-403"` — the class-level attribute requires `manage`, so a `view` holder is refused the read. Step 3: `403` `AUTHZ-403`. Step 4: three hits, **none** requiring `PermissionAction.View`. |
| **Expected DB** | `require_mfa_privileged` unchanged; the `Batch D Settings Reader` role's single `role_permission` row is unchanged. |
| **Expected Audit** | No `audit.field_change` row for `TenantSettings`. |
| **Expected Notification** | n/a — an authorization refusal raises no notification. |
| **Cleanup** | Reassign the test account to its previous role, then delete the `Batch D Settings Reader` role and its `role_permission` row (delete the child first — the FK is tenant-composite `ON DELETE CASCADE`, but delete explicitly so the intent is auditable). |
| **Evidence** | Two HTTP response captures · the `grep` classification table · a screenshot of the granted-but-inert key in the privilege matrix |
| **Result / Defect** | Not Run · — |
| **Notes** | This case **pins a defect** (GAP-TENANT-007) and must be re-authored when the gap closes. Acceptance criterion (1) of that gap makes step 2 a `200`; criterion (2) adds an architecture test asserting that every key in `AllKeys` is required by at least one gate, which would fail today on `tenant-settings.view`. Measured catalogue size for the assertion in step 1: **171** keys. |

---

## The platform surface that is not a platform surface — `PlatformControllers.cs`

#### TC-TENANT-API-021 — Four `GET` actions in `PlatformControllers.cs` carry no permission gate at all  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-005 · RSK-TENANT-002 · **GAP-TENANT-004** |
| **Level / Type / Technique** | API · Security (exploratory-derived, defect-pinning) · Error Guessing — "the file is named for a platform surface, so who actually reaches it?" |
| **Priority / Severity / Automation** | High · Medium · Yes (functional, data-driven over five routes) |
| **Role / Permission / Tenant** | Analyst (seeded role) · **no** `organization.*` or `notifications.*` key is required by these actions · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | A `demo-lab` account assigned the seeded `Analyst` role (measured 65 keys); confirm `SELECT count(*) FROM qams.role_permission rp JOIN qams.role r ON r.id=rp.role_id WHERE r.name='Analyst' AND rp.permission_key LIKE 'organization.%'` and record the result |
| **Test Data** | Five `GET` routes: `/api/branches`, `/api/departments`, `/api/test-catalog`, `/api/lovs`, `/api/notifications/mine` |
| **Steps** | 1. For each of the five routes, issue a `GET` with the analyst's bearer token and record the status and the row count in the body. 2. `grep -n "RequirePermission" src/NT.QAMS.WebApi/Controllers/PlatformControllers.cs` and list the lines that carry one. 3. Cross-check every returned row's `tenant_id` in the database against the analyst's tenant. 4. Repeat step 1 with the analyst's account moved to a role holding **zero** permission keys. |
| **Expected UI** | The organisation and notification screens load for an analyst; assert whether the SPA hides them by permission and record any disagreement with the API. |
| **Expected API** | Step 1: all five return **`200 OK`** — `BranchesController.Tree` (`PlatformControllers.cs:17-19`), `DepartmentsController.List` (`:40-42`), `TestCatalogController.List` (`:64-66`), `LovsController.List` (`:80-82`) and `NotificationsController.Mine` (`:97-101`) carry `[Authorize]` only. Step 2: gates exist on lines 22, 27, 45, 51, 69, 85, 111, 116 and 123 — **none** on a `GET` other than `notifications/rules` and `notifications/monitor`. Step 4: still `200` — an authenticated tenant user with no privileges at all reads the organisation tree, the department list, the test catalogue and the list-of-values. |
| **Expected DB** | Every returned row's `tenant_id` equals the analyst's tenant — isolation is carried entirely by the EF global query filter (`src/NT.QAMS.Infrastructure/Persistence/AppDbContext.cs:169-182,187-192`) and PostgreSQL FORCE RLS, not by authorization. Assert **zero** foreign-tenant rows. |
| **Expected Audit** | No `audit.field_change` row — all five are reads. |
| **Expected Notification** | n/a — reads raise no notification. |
| **Cleanup** | Restore the test account to its original role. |
| **Evidence** | Five HTTP captures at each privilege level · the `grep` output · the per-row `tenant_id` cross-check |
| **Result / Defect** | Not Run · — |
| **Notes** | Documents GAP-TENANT-004's second half (the ungated reads the misleading file name obscures) and satisfies its acceptance criterion (2)'s evidence need: each ungated `GET` must either receive `[RequirePermission(…, View)]` or be listed in a documented "any authenticated tenant user" set. The case asserts **current** behaviour; it becomes a negative case once the gap closes. It belongs to `TENANT` rather than `ORG` because what is being proved is that tenant isolation — not authorization — is the only control on these routes. |

#### TC-TENANT-API-022 — Writing to the same controllers is permission-gated and refuses an analyst with `AUTHZ-403`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-005 · RSK-TENANT-002 |
| **Level / Type / Technique** | API · Security (negative) · Decision Table — write actions of `PlatformControllers.cs` × the seeded Analyst role |
| **Priority / Severity / Automation** | High · High · Yes (functional, data-driven) |
| **Role / Permission / Tenant** | Analyst · required keys `organization.create`, `organization.manage`, `organization.edit`, `notifications.manage` · `demo-lab` |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | The analyst account of TC-TENANT-API-021, on the seeded `Analyst` role |
| **Test Data** | (a) `POST /api/branches` `{"code":"BD1","name":"Batch D Branch","city":"Amman"}` · (b) `POST /api/departments` `{"branchId":"<a demo-lab branch id>","code":"BDD","name":"Batch D Dept"}` · (c) `POST /api/test-catalog` `{"testCode":"BD-T1","testName":"Batch D Test","methodology":"ISO","turnaroundHours":24}` · (d) `POST /api/lovs` `{"category":"batchd","code":"X","nameEn":"X","nameAr":"X","nameFr":"X","sortOrder":1}` · (e) `POST /api/notifications/rules` with a minimal valid body |
| **Steps** | 1. Issue (a)…(e) with the analyst token; record status, `content-type` and `code` for each. 2. `SELECT count(*) FROM qams.branch WHERE code='BD1'`, and the equivalent for department, test-catalog item, LOV entry and notification rule. 3. Repeat (a) as the `demo-lab` tenant administrator and record the status. |
| **Expected UI** | The create buttons on the organisation screens are hidden or disabled for an analyst; a direct API call must be refused regardless of what the screen shows. |
| **Expected API** | Step 1: all five return `403 Forbidden`, `application/problem+json`, `code = "AUTHZ-403"`, `title = "You do not have permission to perform this action."` — `RequirePermissionAttribute` at `PlatformControllers.cs:22` (a), `:45` (b), `:69` (c), `:85` (d) and `:116` (e). Step 3: `200 OK` with `{"id":"<uuid>"}`. |
| **Expected DB** | Step 2: all five counts are **0** after the analyst attempts. After step 3, `qams.branch` gains exactly one row with `tenant_id` = the `demo-lab` id and PK `(tenant_id, id)` (measured `pk_branch PRIMARY KEY (tenant_id, id)`). |
| **Expected Audit** | Nothing from step 1. Step 3 writes an `audit.field_change` row `entity_type='Branch'`, `action='Created'`, `tenant_id` = the `demo-lab` id. |
| **Expected Notification** | n/a for the refusals. Record whatever step 3 produces. |
| **Cleanup** | `DELETE FROM qams.branch WHERE code='BD1' AND tenant_id=<demo-lab id>` — note that `qams.branch` holds one of the five `DEFERRABLE INITIALLY DEFERRED` FKs to `saas.tenant` (`fk_branch_tenant`, measured `ON DELETE RESTRICT`), so the tenant row cannot be removed while this child exists. |
| **Evidence** | Six HTTP captures · six SQL counts |
| **Result / Defect** | Not Run · — |
| **Notes** | Pairs with TC-TENANT-API-021 to give GAP-TENANT-004 its full evidence: the writes are gated, the reads are not, and the file name tells a reviewer neither. The business behaviour of branches, departments, the test catalogue, LOVs and notifications belongs to modules `ORG` and `NOTIF`; only the authorization-and-isolation posture is claimed here. |

#### TC-TENANT-API-023 — A platform administrator reading a tenant-scoped list gets an empty array, not another tenant's data  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-001 |
| **Level / Type / Technique** | API · Security (positive fail-closed) · Data Flow — `tenant_id` claim absent → `CurrentTenant.TenantId = null` → EF filter predicate `TenantId == null` → nil GUC → zero rows |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional + real PostgreSQL) |
| **Role / Permission / Tenant** | PlatformAdmin · `Has()` returns true for every key (`PrivilegeResolution.cs:39`), so no permission gate stops them · **none** |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | `SELECT set_config('app.bypass_rls','on',false); SELECT count(*) FROM qams.lov_entry` returns a non-zero total across all 23 tenants (measured 50 for `demo-lab` alone), so an empty response cannot be mistaken for an empty database |
| **Test Data** | None — reads only |
| **Steps** | 1. Sign in as `platform-admin@localhost` with no tenant slug; confirm the token has no `tenant_id` claim. 2. `GET /api/lovs`; record status and array length. 3. `GET /api/departments`; record the same. 4. `GET /api/branches`; record the same. 5. `GET /api/notifications/mine`; record status and the envelope's `total`. 6. `POST /api/lovs` `{"category":"batchd","code":"P","nameEn":"P","nameAr":"P","nameFr":"P","sortOrder":1}`; record status and `code`. 7. `SELECT set_config('app.bypass_rls','on',false); SELECT count(*) FROM qams.lov_entry WHERE category='batchd'`. |
| **Expected UI** | `tenantOnlyGuard` sends a platform admin to `/platform/tenants` (`role.guard.ts:17-21`), so these screens are unreachable in the SPA; the case is API-only and exists to prove the server does not depend on that redirect. |
| **Expected API** | Steps 2, 3 and 4: `200 OK` with an **empty array**. Step 5: `200` with `total = 0`. Step 6: `422 Unprocessable Entity`, `code = "TENANT-000"`, `title = "Cannot persist tenant-scoped 'LovEntry' without a resolved tenant."` (`src/NT.QAMS.Infrastructure/Persistence/Interceptors/TenantStampInterceptor.cs:51-54`); assert the exact CLR type name in the title before execution. |
| **Expected DB** | Step 7 returns **0**. Two independent layers each return nothing: the EF filter compares `e.TenantId == _currentTenant.TenantId` with a `null` right-hand side, which is never true in SQL (`AppDbContext.cs:190`); and the connection interceptor stamps `app.current_tenant = '00000000-0000-0000-0000-000000000000'` with `app.bypass_rls='off'` (`TenantConnectionInterceptor.cs:21,53-56`), which matches no row under the FORCE-RLS policy. |
| **Expected Audit** | Steps 2–5 write nothing. Step 6 writes nothing — the interceptor throws inside `SavingChanges`, before the transaction commits. |
| **Expected Notification** | n/a — reads and a refused write raise no notification. |
| **Cleanup** | None required; confirm with step 7. |
| **Evidence** | Decoded JWT claim set · five HTTP captures · two SQL counts (all-tenant total, and the `batchd` category) |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the positive statement of the fail-closed property from the platform actor's side, and it is the case that proves a platform administrator is *not* a super-reader of tenant data. Note the contrast with TC-TENANT-API-018: the same actor clears the *permission* gate on `tenant-settings` and is stopped only by the missing tenant. Batch B owns the per-table RLS proofs; this case asserts the composed HTTP-level outcome only. |

---

## Composite primary keys — no `UNIQUE (id)`, so an id alone is not a key

#### TC-TENANT-API-024 — A by-id fetch is tenant-qualified: another tenant's record id returns 404, never 200  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-103 · RSK-TENANT-001, RSK-TENANT-007 |
| **Level / Type / Technique** | API · Security (negative) · Data Flow — the id travels from the route to `SingleOrDefaultAsync(x => x.Id == q.EquipmentId)`, and the tenant predicate is supplied only by the global query filter |
| **Priority / Severity / Automation** | Critical · Critical · Yes (functional + real PostgreSQL) |
| **Role / Permission / Tenant** | TenantAdmin of tenant **A** · `[Authorize]` on `EquipmentController` (`src/NT.QAMS.WebApi/Controllers/EquipmentController.cs:13`); the by-id action carries no `[RequirePermission]` (`:23-25`) · tenant A |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Two tenants exist (use `iso-test-2` and `demo-lab`, both measured `Active`); each has at least one `qams.equipment_item` row. Record tenant B's equipment id: `SELECT set_config('app.bypass_rls','on',false); SELECT id FROM qams.equipment_item WHERE tenant_id=<B> LIMIT 1` |
| **Test Data** | `<B-equipment-id>` — a UUIDv7 that exists in the database but under a different tenant |
| **Steps** | 1. Sign in as an administrator of tenant A. 2. `GET /api/equipment/<B-equipment-id>`; record status, `content-type`, `code` and the full body. 3. `GET /api/equipment/<A-equipment-id>`; record status. 4. `GET /api/equipment/00000000-0000-0000-0000-000000000001` (an id that exists nowhere); record status and `code`. 5. Compare the bodies of steps 2 and 4 field by field. 6. `SELECT set_config('app.bypass_rls','on',false); SELECT tenant_id FROM qams.equipment_item WHERE id='<B-equipment-id>'` to confirm the row genuinely exists. |
| **Expected UI** | Deep-linking the SPA to another tenant's equipment id shows the module's not-found state; no field of the foreign record is rendered, not even transiently. |
| **Expected API** | Step 2: **`404 Not Found`**, `application/problem+json`, `code = "EQP-404"`, `title = "Equipment not found."` (`src/NT.QAMS.Application/Equipment/EquipmentSlice.cs:203-204`; 404 mapping by the `-404` suffix rule at `DomainExceptionHandler.cs:69-74`). Step 3: `200`. Step 4: `404` `EQP-404`. Step 5: the two bodies are **byte-identical apart from `traceId`** — "exists but is not yours" and "does not exist" must be indistinguishable, or the endpoint becomes an id oracle. |
| **Expected DB** | Step 6 confirms the row exists with `tenant_id = <B>`. No write occurs. `qams.equipment_item` has `pk_equipment_item PRIMARY KEY (tenant_id, id)` and `ux_equipment_item_id_tenant UNIQUE (id, tenant_id)` (both measured) — and **no** `UNIQUE (id)`, so the id alone does not identify a row. |
| **Expected Audit** | No `audit.field_change` row. Record whether an `audit.security_event` row is written for the cross-tenant probe; if none is, note that a targeted id-enumeration attempt leaves no trace — a finding for the `LEDGER` module. |
| **Expected Notification** | n/a — a not-found read raises no notification. |
| **Cleanup** | None — read-only. |
| **Evidence** | Three HTTP response captures · the step-5 body diff · the step-6 SQL result proving the row exists |
| **Result / Defect** | Not Run · — |
| **Notes** | The handler's predicate is `x.Id == q.EquipmentId` with **no** tenant term (`EquipmentSlice.cs:198-204`); the tenant term is injected by convention in `AppDbContext.OnModelCreating` (`:169-182`) and enforced again by RLS. That is the intended design, but it means every by-id handler in the codebase is correct only because two layers it does not mention are working. `EquipmentController` is representative: the same shape appears at `/api/documents/{id}` and `/api/risks/{id}`. |

#### TC-TENANT-API-025 — Two tenants may hold the same record id, and an elevated unfiltered `Single` on that id fails  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-103 · RSK-TENANT-007 |
| **Level / Type / Technique** | Integration (database + EF) · Structural (negative) · Path — the `Single`-shaped read path under `Elevate()` + `IgnoreQueryFilters()`, where the tenant term that normally makes the id unique is removed |
| **Priority / Severity / Automation** | Medium · High · Yes (`NT.QAMS.IntegrationTests`, `SkippableFact` on `QMS_ITEST_POSTGRES`) |
| **Role / Permission / Tenant** | n/a — an integration fixture, not an HTTP actor · n/a — RLS and the query filter are the subject · two tenants, A and B |
| **Environment** | `dotnet test tests/NT.QAMS.IntegrationTests` with `QMS_ITEST_POSTGRES="Host=localhost;Database=ntqams;Username=qams_app;Password=dev-only-local"`; the suite runs inside a rollback transaction and `RealPostgresFixture` refuses to run under `SUPERUSER`/`BYPASSRLS` (`tests/NT.QAMS.IntegrationTests/RealPostgresFixture.cs:53-65`) |
| **Preconditions** | Two tenant rows A and B exist in `saas.tenant`; measured facts to assert in-test: **0** tables in `qams`/`read` carry a primary key or unique constraint on `id` alone except `outbox_event`, `user_account`, `idempotency_record` and `refresh_session`; **90** tables carry a tenant-first primary key (86 of arity 2, 4 of arity 3) |
| **Test Data** | One fixed GUID `Q` = `019fbd00-0000-7000-8000-000000000001`, inserted as `qams.equipment_item.id` under **both** A and B with distinct `code` values `BD-COLL-A` and `BD-COLL-B` |
| **Steps** | 1. Under `Elevate()`, insert equipment `Q` for tenant A and equipment `Q` for tenant B in one transaction; assert the insert **succeeds** (no unique violation). 2. Scope the context to A (no elevation) and run `db.EquipmentItems.SingleOrDefaultAsync(x => x.Id == Q)`; assert it returns the row whose `Code = 'BD-COLL-A'`. 3. Scope to B and repeat; assert `Code = 'BD-COLL-B'`. 4. Under `Elevate()` **and** `IgnoreQueryFilters()`, run the same `SingleOrDefaultAsync(x => x.Id == Q)`; capture the exception. 5. Under `Elevate()` and `IgnoreQueryFilters()`, run `CountAsync(x => x.Id == Q)`. 6. Raw SQL under `set_config('app.bypass_rls','on',true)`: `SELECT tenant_id, code FROM qams.equipment_item WHERE id = Q ORDER BY tenant_id`. |
| **Expected UI** | n/a — no UI participates in an integration fixture. |
| **Expected API** | n/a — no HTTP request is issued; the case is deliberately below the API to isolate the key semantics. |
| **Expected DB** | Step 1: both inserts commit — PostgreSQL permits them because the key is `(tenant_id, id)` and `ux_equipment_item_id_tenant` is also `(id, tenant_id)`, the same column set. Step 5: `2`. Step 6: exactly two rows, `(A,'BD-COLL-A')` and `(B,'BD-COLL-B')`. |
| **Expected Audit** | Two `audit.field_change` rows of `entity_type='EquipmentItem'`, `action='Created'`; assert each carries the **owning** tenant's id, not null and not the other tenant's — `FieldChangeInterceptor.TenantOf` reads the `ITenantScoped` value first (`FieldChangeInterceptor.cs:101-135`), which is set explicitly by the fixture, so elevation must not blank it (URS-106). |
| **Expected Notification** | n/a — an integration fixture raises no notification. |
| **Cleanup** | The suite's rollback transaction discards everything; additionally assert `SELECT count(*) FROM qams.equipment_item WHERE id = Q` is `0` after the test class disposes. |
| **Evidence** | Test output including the step-4 exception type and message · the step-6 SQL result · the catalogue assertions from the preconditions |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 4 is the point of the case: `SingleOrDefaultAsync` on an id that is no longer unique throws `InvalidOperationException` ("Sequence contains more than one element"), which matches **no arm** of `DomainExceptionHandler` (`:81`) and would therefore surface as an untyped HTTP 500 if such a query ever ran on a request path. Today no request path combines `Elevate()` with `IgnoreQueryFilters()` and a by-id `Single` — the eight elevation sites are enumerated in the front matter §4.3 — but nothing prevents a ninth from doing so, which is exactly GAP-TENANT-014. This case is the executable statement of that risk and is new coverage: no existing test inserts a deliberate id collision. |

---

## Tenant lifecycle states

#### TC-TENANT-STATE-001 — Provisioning lands the tenant in `Active`; `Provisioning` is never assigned  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-004 · **GAP-TENANT-009** |
| **Level / Type / Technique** | Unit + database · Functional (positive) · State Transition — the factory edge of the §3.1 matrix, plus the reachability claim about the enum's first value |
| **Priority / Severity / Automation** | Medium · Medium · Yes (`NT.QAMS.Domain.UnitTests` + a catalogue query) |
| **Role / Permission / Tenant** | n/a — a domain factory takes no actor · n/a — no gate at the domain layer · n/a — `Tenant` is the tenant, not tenant-scoped (`Tenant.cs:6-9`) |
| **Environment** | `$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"; dotnet test tests/NT.QAMS.Domain.UnitTests` (no database) + one read-only `psql` query against `ntqams` |
| **Preconditions** | None for the unit half. For the database half, `saas.tenant` exists with `ck_tenant_status_domain` |
| **Test Data** | `TenantSlug.Create("amman-central-lab")`, name `"Amman Central Laboratory"` |
| **Steps** | 1. `Tenant.Provision(slug, name)`; assert `Status == TenantStatus.Active`. 2. Assert `Settings == TenantSettings.Default`, i.e. `PasswordExpiryDays=90`, `CalibrationReminderDays=30`, `SopExpiryReminderMonths=3`, `DefaultLanguage="en"`, `TimeZone="UTC"`, `RequireMfaForPrivilegedRoles=false`. 3. Assert `SuspensionReason` is `null`. 4. Assert exactly one domain event, of type `TenantProvisioned`, carrying `(TenantId = tenant.Id, Slug = "amman-central-lab", Name = "Amman Central Laboratory")`. 5. `grep -rn "TenantStatus.Provisioning" src/ tests/ --include=*.cs` and record the hit count. 6. `SELECT status, count(*) FROM saas.tenant GROUP BY 1`. |
| **Expected UI** | n/a — a domain unit test has no UI. |
| **Expected API** | n/a — the factory is not reached over HTTP in this case; TC-TENANT-API-001 covers the API path. |
| **Expected DB** | Step 6 returns `Active|23` and no other value (measured 2026-08-01). `ck_tenant_status_domain` accepts all four literals `Provisioning`, `Active`, `Suspended`, `Terminated` (measured), so the database permits a state the application cannot produce. |
| **Expected Audit** | n/a — a domain unit test writes no ledger row. |
| **Expected Notification** | n/a — the event is raised, not dispatched, at unit level. |
| **Cleanup** | n/a — no state is created. |
| **Evidence** | Test output · the `grep` hit count · the SQL status distribution |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 5 must return **zero** hits — that is the assertion, and it is what makes GAP-TENANT-009 real: `Provisioning = 0` (`src/NT.QAMS.Domain/Tenancy/TenantStatus.cs:9`) is both the CLR default and unreachable, so a `Tenant` materialised without a status would silently read as `Provisioning`. Steps 1, 2 and 4 partly overlap `TenantTests.Provision_creates_active_tenant_and_raises_event`, which asserts only the slug on the event; steps 3, 5 and 6 are new. |

#### TC-TENANT-STATE-002 — On a suspended tenant a blank reason yields `TENANT-010`, not `TENANT-011`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-004 |
| **Level / Type / Technique** | Unit · Functional (negative) · Path — the two-guard sequence inside `Tenant.Suspend`, where the state check at `Tenant.cs:60-64` precedes the reason check at `:66-69` |
| **Priority / Severity / Automation** | Medium · Medium · Yes (`NT.QAMS.Domain.UnitTests`) |
| **Role / Permission / Tenant** | n/a — domain method · n/a — no gate at the domain layer · n/a |
| **Environment** | `dotnet test tests/NT.QAMS.Domain.UnitTests` (no database) |
| **Preconditions** | None |
| **Test Data** | Reasons: `""`, `"   "` (three spaces), `"\t"` — each applied to a tenant already in `Suspended` and again to one in `Active` |
| **Steps** | 1. `var t = Tenant.Provision(TenantSlug.Create("guard-order"), "Guard Order Lab"); t.Suspend("Non-payment");` — `t.Status` is now `Suspended`. 2. For each of the three blank reasons, call `t.Suspend(reason)` and capture the exception type and `Code`. 3. On a fresh `Active` tenant, call `Suspend("")` and capture the exception type and `Code`. 4. Assert `t.Status` is still `Suspended` and `t.SuspensionReason` is still `"Non-payment"` after step 2. 5. Assert no new domain event was raised in step 2 or 3. |
| **Expected UI** | n/a — no endpoint exists for suspension (GAP-TENANT-002), so no UI can reach this path. |
| **Expected API** | n/a — `POST /api/tenants/{id}/suspend` does not exist in `ApiSurface.approved.txt`. |
| **Expected DB** | n/a — the case runs entirely in memory. |
| **Expected Audit** | n/a — no persistence, no ledger row. |
| **Expected Notification** | n/a. |
| **Cleanup** | n/a. |
| **Evidence** | Test output showing the exception type and code for each of the four sub-cases |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 2 must yield `InvalidStateTransitionException` with `Code = "TENANT-010"` and message `"Only an Active tenant can be suspended (current: Suspended)."` — **not** `DomainException`/`TENANT-011`. Step 3 must yield `DomainException` with `Code = "TENANT-011"` and message `"A suspension reason is required."`. The distinction matters because the two map to different HTTP statuses once the endpoint exists (409 versus 422, `DomainExceptionHandler.cs:45-51` and `:75-80`). `TenantTests.Suspend_requires_reason_and_active_state` exercises both codes but never in this order, so the precedence is currently unpinned. |

#### TC-TENANT-STATE-003 — A suspended tenant may be terminated directly, skipping `Active`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-004 |
| **Level / Type / Technique** | Unit · Functional (positive) · State Transition — the `Suspended → Terminated` edge of §3.1, which the documented lifecycle string `Provisioning → Active → Suspended ⇄ Active → Terminated` (`Tenant.cs:8`) does not describe |
| **Priority / Severity / Automation** | Medium · Medium · Yes (`NT.QAMS.Domain.UnitTests`) |
| **Role / Permission / Tenant** | n/a — domain method · n/a · n/a |
| **Environment** | `dotnet test tests/NT.QAMS.Domain.UnitTests` (no database) |
| **Preconditions** | None |
| **Test Data** | Slug `susp-term`, name `Suspend Then Terminate Lab`, suspension reason `Regulatory hold 2026-08` |
| **Steps** | 1. Provision, then `Suspend("Regulatory hold 2026-08")`. 2. Assert `Status == Suspended` and `SuspensionReason == "Regulatory hold 2026-08"`. 3. Call `Terminate()`. 4. Assert `Status == Terminated`. 5. Assert `SuspensionReason` is **still** `"Regulatory hold 2026-08"` — `Terminate` does not clear it (`Tenant.cs:89-98`). 6. Assert the raised events, in order: `TenantProvisioned`, `TenantSuspended(Id, "susp-term", "Regulatory hold 2026-08")`, `TenantTerminated(Id, "susp-term")`. |
| **Expected UI** | n/a — no endpoint exists (GAP-TENANT-002). |
| **Expected API** | n/a — no route. |
| **Expected DB** | n/a — in-memory only. |
| **Expected Audit** | n/a — no persistence. |
| **Expected Notification** | n/a — `TenantSuspended` and `TenantTerminated` have **no** subscriber anywhere in `src/` (GAP-TENANT-003), so even a persisted transition would notify nobody. |
| **Cleanup** | n/a. |
| **Evidence** | Test output listing the three events in order with their payloads |
| **Result / Defect** | Not Run · — |
| **Notes** | `Terminate` guards only against a **second** termination (`Status == TenantStatus.Terminated`, `Tenant.cs:91-94`); every other state may terminate. Step 5 records a real behaviour worth an explicit decision: a terminated tenant retains the reason it was suspended for, which reads as a suspension reason on a terminated record. No existing test covers this edge. |

#### TC-TENANT-STATE-004 — Reactivating a terminated tenant is refused with `TENANT-012`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-004 |
| **Level / Type / Technique** | Unit · Functional (negative) · State Transition — the `Terminated → Reactivate` illegal edge of §3.1 |
| **Priority / Severity / Automation** | Medium · High · Yes (`NT.QAMS.Domain.UnitTests`) |
| **Role / Permission / Tenant** | n/a — domain method · n/a · n/a |
| **Environment** | `dotnet test tests/NT.QAMS.Domain.UnitTests` (no database) |
| **Preconditions** | None |
| **Test Data** | Slug `term-react`, name `Terminate Then Reactivate Lab` |
| **Steps** | 1. Provision, then `Terminate()`. 2. Call `Reactivate()` and capture the exception type, `Code` and message. 3. Assert `Status` is still `Terminated`. 4. Assert no `TenantReactivated` event was raised. 5. Repeat the whole sequence via `Suspend("x")` → `Terminate()` → `Reactivate()` and assert the identical outcome. |
| **Expected UI** | n/a — no endpoint exists. |
| **Expected API** | n/a — no route; once GAP-TENANT-002 closes this must be `409 Conflict` with `code = "TENANT-012"` (`DomainExceptionHandler.cs:45-51`). |
| **Expected DB** | n/a — in-memory only. |
| **Expected Audit** | n/a. |
| **Expected Notification** | n/a. |
| **Cleanup** | n/a. |
| **Evidence** | Test output for both sequences |
| **Result / Defect** | Not Run · — |
| **Notes** | `InvalidStateTransitionException` with `Code = "TENANT-012"` and message `"Only a Suspended tenant can be reactivated (current: Terminated)."` (`Tenant.cs:78-82`). `TenantTests.Reactivate_only_from_suspended` covers `Active → Reactivate` only, so the terminated arm — the one that matters, because it is the difference between "offboarded" and "recoverable" — is currently untested. Termination is irreversible in the domain, which is a business fact GAP-TENANT-002's clarification must confirm. |

#### TC-TENANT-STATE-005 — Suspending a terminated tenant is refused with `TENANT-010`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-004 |
| **Level / Type / Technique** | Unit · Functional (negative) · State Transition — the `Terminated → Suspend` illegal edge of §3.1 |
| **Priority / Severity / Automation** | Low · Medium · Yes (`NT.QAMS.Domain.UnitTests`) |
| **Role / Permission / Tenant** | n/a — domain method · n/a · n/a |
| **Environment** | `dotnet test tests/NT.QAMS.Domain.UnitTests` (no database) |
| **Preconditions** | None |
| **Test Data** | Slug `term-susp`, name `Terminate Then Suspend Lab`, reason `Late payment` |
| **Steps** | 1. Provision, then `Terminate()`. 2. Call `Suspend("Late payment")` and capture the exception type, `Code` and message. 3. Assert `Status` is still `Terminated` and `SuspensionReason` is still `null`. 4. Assert no `TenantSuspended` event was raised. |
| **Expected UI** | n/a — no endpoint exists. |
| **Expected API** | n/a — no route; would be `409` `TENANT-010` once GAP-TENANT-002 closes. |
| **Expected DB** | n/a — in-memory only. |
| **Expected Audit** | n/a. |
| **Expected Notification** | n/a. |
| **Cleanup** | n/a. |
| **Evidence** | Test output |
| **Result / Defect** | Not Run · — |
| **Notes** | `InvalidStateTransitionException`, `Code = "TENANT-010"`, message `"Only an Active tenant can be suspended (current: Terminated)."` (`Tenant.cs:60-64`). Together with TC-TENANT-STATE-002, -003 and -004 this closes the four illegal edges of the reachable part of the §3.1 matrix; the `Provisioning` row remains unreachable (GAP-TENANT-009) and is not authored as an executable case. |

#### TC-TENANT-STATE-006 — Suspending an active tenant trims the reason and raises `TenantSuspended`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-004 |
| **Level / Type / Technique** | Unit · Functional (positive) · State Transition — the `Active → Suspended` edge, including the reason's normalisation |
| **Priority / Severity / Automation** | Medium · Medium · Yes (`NT.QAMS.Domain.UnitTests`) |
| **Role / Permission / Tenant** | n/a — domain method · n/a · n/a |
| **Environment** | `dotnet test tests/NT.QAMS.Domain.UnitTests` (no database) |
| **Preconditions** | None |
| **Test Data** | Reason `"   Non-payment of the 2026 Q3 invoice   "` (leading and trailing whitespace); also a 500-character reason and a 501-character reason |
| **Steps** | 1. Provision, then `Suspend("   Non-payment of the 2026 Q3 invoice   ")`. 2. Assert `Status == Suspended`. 3. Assert `SuspensionReason == "Non-payment of the 2026 Q3 invoice"` — trimmed, length 34, not 40 (`Tenant.cs:72`). 4. Assert the raised event is `TenantSuspended(Id, "…", "Non-payment of the 2026 Q3 invoice")` — the **trimmed** value (`Tenant.cs:73`). 5. On a fresh tenant, `Suspend(new string('R', 500))`; assert it succeeds. 6. On a fresh tenant, `Suspend(new string('R', 501))`; assert the **domain accepts it** and record that fact. |
| **Expected UI** | n/a — no endpoint exists. |
| **Expected API** | n/a — no route. |
| **Expected DB** | n/a in memory; but record that `saas.tenant.suspension_reason` is `character varying(500)` (measured), so the 501-character reason of step 6 would fail at the database with a value-too-long error, not at the domain — there is **no** `MaxLength` guard in `Tenant.Suspend`. |
| **Expected Audit** | n/a — no persistence at unit level. |
| **Expected Notification** | n/a — `TenantSuspended` has no subscriber (GAP-TENANT-003). |
| **Cleanup** | n/a. |
| **Evidence** | Test output including the exact `SuspensionReason` string and its length for each sub-case |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 6 is the finding: the 500-character column bound has no matching domain or validator rule, which is precisely the pattern `CLAUDE.md` §5 ("column sizing") forbids in the other direction. Because no suspend command exists, no FluentValidation rule exists either — so GAP-TENANT-002's acceptance criteria should require `MaximumLength(500)` on `SuspendTenantCommand.Reason`. Recorded here rather than raised as a separate gap, since it cannot manifest until the command is written. |

#### TC-TENANT-STATE-007 — Reactivating clears the suspension reason and raises `TenantReactivated`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-004 |
| **Level / Type / Technique** | Unit · Functional (positive) · State Transition — the `Suspended → Active` edge and the field reset it performs |
| **Priority / Severity / Automation** | Medium · Medium · Yes (`NT.QAMS.Domain.UnitTests`) |
| **Role / Permission / Tenant** | n/a — domain method · n/a · n/a |
| **Environment** | `dotnet test tests/NT.QAMS.Domain.UnitTests` (no database) |
| **Preconditions** | None |
| **Test Data** | Slug `react-cycle`, reason `Audit hold` |
| **Steps** | 1. Provision, `Suspend("Audit hold")`, `Reactivate()`. 2. Assert `Status == Active`. 3. Assert `SuspensionReason` is `null` (`Tenant.cs:85`). 4. Assert the raised event is `TenantReactivated(Id, "react-cycle")` — a two-field payload with **no** reason (`src/NT.QAMS.Domain/Tenancy/TenantEvents.cs:14`). 5. Repeat the suspend/reactivate cycle three times and assert the state and reason after each half, proving the `Active ⇄ Suspended` edge is idempotent in shape. 6. Assert the full event sequence across three cycles is `TenantProvisioned`, then `TenantSuspended`/`TenantReactivated` × 3, in order. |
| **Expected UI** | n/a — no endpoint exists. |
| **Expected API** | n/a — no route. |
| **Expected DB** | n/a in memory; once persisted, `suspension_reason` would return to `NULL`, and the column is nullable (measured `is_nullable = YES`). |
| **Expected Audit** | n/a at unit level. Once a command exists, each half must write an `audit.field_change` row for `Status` and one for `SuspensionReason`, both attributed to the tenant. |
| **Expected Notification** | n/a — `TenantReactivated` has no subscriber (GAP-TENANT-003). |
| **Cleanup** | n/a. |
| **Evidence** | Test output listing state, reason and events after each of the six half-cycles |
| **Result / Defect** | Not Run · — |
| **Notes** | `TenantTests.Reactivate_only_from_suspended` asserts one cycle's state and reason; the repeated-cycle assertion in steps 5 and 6, and the event payload assertion in step 4, are new. The event carries no reason at all, so an audit reader cannot learn *why* a tenant was reactivated from the event stream — worth naming in GAP-TENANT-002's clarification. |

#### TC-TENANT-STATE-008 — A terminated tenant's MFA policy can still be changed; the setter has no state guard  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-004, URS-008 · RSK-TENANT-005 · **GAP-TENANT-013** |
| **Level / Type / Technique** | Unit · Functional (defect-pinning) · Decision Table — the `SetPrivilegedMfaPolicy` column of §3.1, which is `✔ (unguarded)` in every one of the four state rows |
| **Priority / Severity / Automation** | Medium · Medium · Yes (`NT.QAMS.Domain.UnitTests`) |
| **Role / Permission / Tenant** | n/a — domain method · at API level `tenant-settings.manage` + `[RequireInternalActor]`, neither of which is a state check · n/a |
| **Environment** | `dotnet test tests/NT.QAMS.Domain.UnitTests` (no database) |
| **Preconditions** | None |
| **Test Data** | `require = true`, then `require = false` |
| **Steps** | 1. Provision a tenant and call `Terminate()`. 2. Call `SetPrivilegedMfaPolicy(true)`; assert it **succeeds** and `Settings.RequireMfaForPrivilegedRoles == true`. 3. Assert `Status` is still `Terminated`. 4. Assert **no** domain event was raised by step 2 (`Tenant.cs:107-108` raises nothing). 5. Repeat steps 1–4 on a `Suspended` tenant. 6. Assert that the other five settings are unchanged after step 2: `PasswordExpiryDays=90`, `CalibrationReminderDays=30`, `SopExpiryReminderMonths=3`, `DefaultLanguage="en"`, `TimeZone="UTC"` — the `with` expression replaces only one property. |
| **Expected UI** | n/a — no UI can reach a terminated tenant, because no suspension or termination endpoint exists (GAP-TENANT-002). |
| **Expected API** | n/a at unit level. Note the consequence once GAP-TENANT-002 closes: `PUT /api/tenant-settings/mfa-policy` would return `204` for a terminated tenant unless a state guard is added, because nothing in the pipeline reads `saas.tenant.status` (GAP-TENANT-001). |
| **Expected DB** | n/a in memory. |
| **Expected Audit** | n/a at unit level. Note that `SetPrivilegedMfaPolicy` raises no event, so the change reaches only `audit.field_change` (via the interceptor) and never the hash-chained `audit.audit_trail`. |
| **Expected Notification** | n/a — no event, therefore no notification. |
| **Cleanup** | n/a. |
| **Evidence** | Test output for both the terminated and suspended sub-cases, including the untouched five settings |
| **Result / Defect** | Not Run · — |
| **Notes** | Pins the behaviour named in GAP-TENANT-013's acceptance criterion (3): *"`SetPrivilegedMfaPolicy` states whether a non-`Active` tenant may change it, and enforces the answer."* Today the answer is "yes, silently". The absence of an event (step 4) is the second half of the finding: a security-relevant setting changes with no entry in the tamper-evident chain. |

#### TC-TENANT-STATE-009 — `UpdateSettings(null)` throws an unmapped `ArgumentNullException`, and the method has no caller  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-005 · **GAP-TENANT-013** |
| **Level / Type / Technique** | Unit + static analysis · Functional (defect-pinning) · Statement — the single guarded statement of `Tenant.UpdateSettings` (`Tenant.cs:100-104`) and its zero call sites |
| **Priority / Severity / Automation** | Low · Medium · Yes (`NT.QAMS.Domain.UnitTests` + an architecture assertion) |
| **Role / Permission / Tenant** | n/a — domain method · n/a — unreachable, therefore ungated · n/a |
| **Environment** | `dotnet test tests/NT.QAMS.Domain.UnitTests` and `tests/NT.QAMS.Architecture.Tests` |
| **Preconditions** | None |
| **Test Data** | `null`, and a valid `TenantSettings` with `PasswordExpiryDays = 45` |
| **Steps** | 1. `Tenant.Provision(...).UpdateSettings(null!)`; capture the exception type. 2. `UpdateSettings(new TenantSettings { PasswordExpiryDays = 45 })`; assert `Settings.PasswordExpiryDays == 45`. 3. `grep -rn "\.UpdateSettings(" src/ --include=*.cs` and record the hit count. 4. Map the exception type against every arm of `DomainExceptionHandler.TryHandleAsync` (`src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:26-82`) and record which arm, if any, matches. 5. Enumerate every `TenantSettings` property and, for each, `grep` for a mutation path in `src/`. |
| **Expected UI** | n/a — the method is unreachable from any screen. |
| **Expected API** | n/a — no endpoint calls it. If one did, step 4 shows the outcome: `ArgumentNullException` matches the `_ => null` arm at `:81`, so the handler returns `false` and the request becomes an **unhandled HTTP 500** with no `code` and no `problem+json` body from this handler. |
| **Expected DB** | n/a in memory. Record that all six settings columns exist and are `NOT NULL` on `saas.tenant` (measured), so five of them are persisted, queryable, and permanently equal to their declared defaults. |
| **Expected Audit** | n/a. |
| **Expected Notification** | n/a. |
| **Cleanup** | n/a. |
| **Evidence** | Test output · the two `grep` outputs · the handler-arm mapping table |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 3 must return **zero** hits — that is the dead-code assertion, contrary to `CLAUDE.md` §2.3. Step 5 must show that `PasswordExpiryDays`, `CalibrationReminderDays`, `SopExpiryReminderMonths`, `DefaultLanguage` and `TimeZone` (`TenantSettings.cs:10-14`) have **no** mutation path anywhere, while `RequireMfaForPrivilegedRoles` (`:20`) has exactly one (`Tenant.cs:107`). Five documented per-tenant settings are therefore advertised, persisted and unconfigurable. `[GD]` on GAP-TENANT-013: a case that *exercises* those five settings cannot be written until they have commands. |

#### TC-TENANT-STATE-010 — The database rejects a status value the enum does not declare  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-102 · RSK-TENANT-004 |
| **Level / Type / Technique** | Integration (database) · Structural (negative) · Equivalence Partitioning at the persistence boundary — declared literals versus everything else |
| **Priority / Severity / Automation** | Medium · Medium · Yes (`NT.QAMS.IntegrationTests`, `SkippableFact`) |
| **Role / Permission / Tenant** | n/a — raw SQL under the test fixture · n/a · n/a — `saas.tenant` is the control plane and carries no RLS (measured `relrowsecurity=f`) |
| **Environment** | `QMS_ITEST_POSTGRES="Host=localhost;Database=ntqams;Username=qams_app;Password=dev-only-local"`, inside the suite's rollback transaction |
| **Preconditions** | `ck_tenant_status_domain` exists on `saas.tenant` (measured: `CHECK (status = ANY (ARRAY['Provisioning','Active','Suspended','Terminated']))`) |
| **Test Data** | Rejected: `'Archived'`, `'active'` (lower case), `'ACTIVE'`, `''`. Accepted: `'Provisioning'`, `'Active'`, `'Suspended'`, `'Terminated'` |
| **Steps** | 1. For each rejected value, `UPDATE saas.tenant SET status = <value> WHERE identifier='demo-lab'` and capture the PostgreSQL `SqlState` and constraint name. 2. For each accepted value, run the same update and assert it succeeds, then roll back. 3. Assert `SELECT enumlabel`-equivalence by comparing the CHECK array against `Enum.GetNames<TenantStatus>()` element by element. 4. Confirm the column is `character varying(20)` and the EF conversion is `HasConversion<string>()` (`src/NT.QAMS.Infrastructure/Persistence/Configurations/TenantConfiguration.cs:25-27`). |
| **Expected UI** | n/a — a raw-SQL integration case has no UI. |
| **Expected API** | n/a — no HTTP request. |
| **Expected DB** | Step 1: every rejected value fails with `SqlState 23514` naming `ck_tenant_status_domain`; note that `'active'` and `'ACTIVE'` fail because the CHECK is a case-sensitive literal comparison. Step 2: all four declared values succeed. Step 3: the CHECK array and the enum names match exactly, in the same four values. Step 4: `character varying(20)` (measured), long enough for the longest literal `Provisioning` (12 characters). |
| **Expected Audit** | Raw SQL bypasses EF, so no `audit.field_change` row is written — assert that explicitly, because it is why direct database manipulation of tenant status is unauditable and why GAP-TENANT-002 matters. |
| **Expected Notification** | n/a. |
| **Cleanup** | The suite's rollback transaction; additionally assert `SELECT status FROM saas.tenant WHERE identifier='demo-lab'` is `'Active'` after the test class disposes. |
| **Evidence** | Eight SQL outcomes with `SqlState` and constraint name · the enum-versus-CHECK comparison |
| **Result / Defect** | Not Run · — |
| **Notes** | The CHECK accepts `'Provisioning'`, which the application never produces (GAP-TENANT-009) — step 2 proves the database is *more* permissive than the code, which is the inverse of the usual concern and is the reason step 3 exists. This is one of the 85 CHECK constraints delivered by `Hardening3_CheckDomains`; the module-wide sweep belongs to batch B. |

#### TC-TENANT-STATE-011 — A non-active tenant is refused at sign-in and in the workspace lookup, but stays visible on the control plane  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-001, URS-008 · RSK-TENANT-004 · **GAP-TENANT-002** |
| **Level / Type / Technique** | API · Functional (negative) · Decision Table — the §3.2 row set "effect of tenant status on downstream behaviour", driven across all three reachable statuses |
| **Priority / Severity / Automation** | High · High · Partly — automatable only once a suspension command exists; today the precondition is a manual `UPDATE` |
| **Role / Permission / Tenant** | anonymous (workspace lookup and sign-in) and PlatformAdmin (list) · `[AllowAnonymous]` (`src/NT.QAMS.WebApi/Controllers/AuthController.cs:48`) and `Roles.PlatformAdmin` · the probe tenant |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams` |
| **Preconditions** | Provision a disposable tenant `batchd-status-011` via TC-TENANT-API-001's procedure, with administrator `status011@batchd.test` / `Status-Batch-D-2026!`. Then, **out of band and only because no command exists**, `UPDATE saas.tenant SET status='Suspended', suspension_reason='Batch D probe' WHERE identifier='batchd-status-011'` |
| **Test Data** | Login body `{"tenantIdentifier":"batchd-status-011","email":"status011@batchd.test","password":"Status-Batch-D-2026!"}` |
| **Steps** | 1. While the tenant is `Active`: `GET /api/auth/workspace/batchd-status-011` anonymously; `POST /api/auth/login`; record both. 2. Apply the out-of-band `UPDATE` to `Suspended`. 3. Repeat both requests; record status, `content-type`, `code`, `title`. 4. `GET /api/auth/workspace/a-lab-that-does-not-exist` and `GET /api/auth/workspace/Not_A_Valid_Slug`; diff all three 404 bodies. 5. `GET /api/tenants` as platform admin and locate the probe tenant. 6. `UPDATE saas.tenant SET status='Terminated'` and repeat steps 3 and 5. |
| **Expected UI** | `/t/batchd-status-011` still pins the slug and redirects to `/login` (`tenant-entry.component.ts:23-30`); the sign-in page shows the generic identifier because the workspace lookup 404s, and the sign-in attempt shows a generic failure. Nothing on screen reveals that the laboratory is suspended. |
| **Expected API** | Step 1: `200` `{"name":"…"}` and a successful login. Step 3 (Suspended) and step 6 (Terminated): the workspace lookup returns `404` `application/problem+json` `title = "Workspace not found."` (`AuthController.cs:55`), because `GetWorkspaceQueryHandler` filters `t.Status == TenantStatus.Active` (`GetWorkspace.cs:44`); the login returns `401` with `code = "AUTH-002"`, `title = "This tenant is not active."` (`src/NT.QAMS.Application/IdentityAccess/Commands/Login.cs:60-63`). Step 4: all three 404 bodies are **identical apart from `traceId`** — unknown, malformed and non-active are indistinguishable. Step 5: the probe tenant is **present** in the list with `"status":"Suspended"`, then `"Terminated"` — `GetTenantsQuery` applies no status filter. |
| **Expected DB** | `saas.tenant.status` and `suspension_reason` are exactly what the out-of-band `UPDATE` set; no application write occurred. |
| **Expected Audit** | The out-of-band `UPDATE` writes **no** `audit.field_change` row — it bypasses EF entirely. The failed login writes an `audit.security_event`; because `LoginHandler` calls `tenantScope.Set(tenant.Id)` at `Login.cs:58` **before** the status check at `:60`, that row carries the probe tenant's id and is visible in its own compliance view. Assert both. |
| **Expected Notification** | n/a — neither a refused sign-in nor a suppressed workspace lookup raises a notification. |
| **Cleanup** | `UPDATE saas.tenant SET status='Active', suspension_reason=NULL WHERE identifier='batchd-status-011'`, then delete the tenant and its dependents in the TC-TENANT-API-001 order. |
| **Evidence** | Eight HTTP captures · the three-way 404 body diff · the `audit.security_event` row · the `audit.field_change` absence |
| **Result / Defect** | Not Run · — |
| **Notes** | `[GD]` on GAP-TENANT-002: the precondition is a direct database mutation, which is not a supported operation and is itself unauditable — a fact this case demonstrates rather than works around. The anti-enumeration half (step 4) overlaps `tests/NT.QAMS.WebApi.FunctionalTests/WorkspaceLookupTests.cs:92-103`, which covers unknown/malformed/short but **not** the suspended case, because it cannot suspend a tenant either. |

#### TC-TENANT-STATE-012 — An access token issued before suspension keeps working afterwards  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-001, URS-008 · RSK-TENANT-004 · **GAP-TENANT-001**, **GAP-TENANT-002** |
| **Level / Type / Technique** | API · Security (defect-pinning) · State Transition across a session boundary — the same credential evaluated either side of the `Active → Suspended` edge |
| **Priority / Severity / Automation** | Critical · High · Partly — the suspension half needs an out-of-band `UPDATE` until GAP-TENANT-002 closes |
| **Role / Permission / Tenant** | TenantAdmin of the probe tenant · the token's own privileges, resolved fresh on every request by `ActiveSessionMiddleware` (`RequestIdentity.cs:118-121`) · the probe tenant |
| **Environment** | API `:5080` Development + live PostgreSQL `ntqams`; `Jwt:ExpiryMinutes` at its default of 15 (`SecurityAdapters.cs:59`) |
| **Preconditions** | The `batchd-status-011` probe tenant of TC-TENANT-STATE-012's sibling case exists and is `Active`; a fresh access token has just been issued to `status011@batchd.test` |
| **Test Data** | The bearer token from the precondition; a `PUT /api/tenant-settings/mfa-policy` body `{"require":true}` |
| **Steps** | 1. With the token, `GET /api/tenant-settings/mfa-policy`; record status. 2. `UPDATE saas.tenant SET status='Suspended', suspension_reason='Batch D revocation probe' WHERE identifier='batchd-status-011'`. 3. Immediately re-issue the **same** `GET` with the **same** token; record status and body. 4. `PUT /api/tenant-settings/mfa-policy` `{"require":true}` with the same token; record status. 5. `SELECT require_mfa_privileged FROM saas.tenant WHERE identifier='batchd-status-011'`. 6. `POST /api/auth/refresh` presenting the `qams_rt` cookie; record status. 7. `POST /api/auth/login` with the same credentials; record status and `code`. 8. `UPDATE … SET status='Terminated'` and repeat steps 3, 4 and 6. |
| **Expected UI** | The SPA continues to operate normally for the suspended laboratory's user — no banner, no forced sign-out, no degraded state — which is precisely the impact GAP-TENANT-001 describes. |
| **Expected API** | Step 1: `200`. Step 3: **`200`** — the request succeeds after suspension. `ActiveSessionMiddleware` re-reads `qams.user_account` on every authenticated request (`RequestIdentity.cs:93-96`) but **never reads `saas.tenant`**, so no code path evaluates tenant status on a live session. Step 4: **`204`** — a write succeeds for a suspended laboratory. Step 6: record the outcome verbatim; refresh was **not** read for a tenant-status check in this pass, so this observation is `[RNV]` until the refresh handler is inspected. Step 7: `401` `AUTH-002` — only the sign-in path enforces status. Step 8: identical outcomes for `Terminated`. |
| **Expected DB** | Step 5 returns `t` — a suspended tenant's security policy was changed by one of its users after suspension. |
| **Expected Audit** | Step 4 writes an `audit.field_change` row for `TenantSettings`/`Modified` attributed to the suspended tenant. That row is the evidence that a regulated change was accepted after the platform considered the laboratory closed. |
| **Expected Notification** | n/a — no notification is defined for a post-suspension action. |
| **Cleanup** | `UPDATE saas.tenant SET status='Active', suspension_reason=NULL, require_mfa_privileged=false WHERE identifier='batchd-status-011'`, then delete the tenant and its dependents. Leave the `audit.field_change` rows in place — the ledger is append-only. |
| **Evidence** | Seven HTTP captures with timestamps proving they fall inside the 15-minute token lifetime · the SQL result of step 5 · the resulting ledger row |
| **Result / Defect** | Not Run · — |
| **Notes** | This case **pins a defect** and must be inverted when GAP-TENANT-001 closes: its acceptance criteria require step 3 to become `401` with a distinct code (proposed `AUTH-008`), step 6 to be refused, and platform-administrator requests — which carry no tenant — to remain unaffected. Steps 4 and 5 strengthen the gap's evidence beyond its own wording: it is not merely that reads continue, but that a **security-relevant write** is accepted. Step 6 is deliberately labelled `[RNV]`; do not report a refresh behaviour that was not read. |

#### TC-TENANT-STATE-013 — No lifecycle transition is reachable through the approved API surface  [GD]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-004 · **GAP-TENANT-002** |
| **Level / Type / Technique** | API + static analysis · Structural (coverage-hole proof) · Decision Table — every transition of §3.1 against the routes that exist |
| **Priority / Severity / Automation** | High · High · Yes (a surface assertion in `NT.QAMS.WebApi.FunctionalTests`) |
| **Role / Permission / Tenant** | PlatformAdmin · would be `[RequireRole(UserRole.PlatformAdmin)]` under GAP-TENANT-002's acceptance criterion (1) · none |
| **Environment** | API `:5080` Development + the repository working tree |
| **Preconditions** | Platform-admin bearer token held; a known tenant id `<T>` from `GET /api/tenants` |
| **Test Data** | Candidate routes: `POST /api/tenants/<T>/suspend`, `POST /api/tenants/<T>/reactivate`, `POST /api/tenants/<T>/terminate`, `PUT /api/tenants/<T>`, `PATCH /api/tenants/<T>`, `DELETE /api/tenants/<T>` |
| **Steps** | 1. Issue each of the six candidate requests as platform admin (the `DELETE` with header `X-Change-Reason: Batch D probe`, to isolate routing from `ChangeReasonMiddleware`). 2. Record each status. 3. `grep -nE "^(POST|PUT|PATCH|DELETE) /api/tenants" tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt`. 4. `grep -rn "SuspendTenant\|ReactivateTenant\|TerminateTenant" src/ --include=*.cs`. 5. `grep -n "record" src/NT.QAMS.Contracts/Tenancy/TenancyContracts.cs` and list every request contract. 6. `SELECT status, count(*) FROM saas.tenant GROUP BY 1`. |
| **Expected UI** | `/platform/tenants` offers **no** suspend, reactivate or terminate control — the component renders a create drawer and a read-only table only (`tenants.component.ts:24-91`). |
| **Expected API** | Step 2: all six return `404 Not Found` (no matching route) or `405 Method Not Allowed`; record which for each, since the distinction reveals whether the path or only the verb is unmatched. Step 3: exactly one match, `POST /api/tenants` (line 419) plus its versioned mirror (line 612) — **no** lifecycle route. |
| **Expected DB** | Step 6 returns `Active|23` and no other value (measured) — the live corollary of steps 3 and 4: with no way to leave `Active`, no tenant ever has. |
| **Expected Audit** | No `audit.audit_trail` or `audit.field_change` row from any of the six requests. |
| **Expected Notification** | n/a — unroutable requests raise no notification. |
| **Cleanup** | None — no state is created. |
| **Evidence** | Six HTTP captures · three `grep` outputs · the SQL status distribution |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 4 must return **zero** hits, and step 5 must show `TenancyContracts.cs` holds only `ProvisionTenantRequest`, `TenantMfaPolicyDto`, `SetTenantMfaPolicyRequest`, `TenantDto` and `WorkspaceResponse` (verified) — no suspend, reactivate or terminate request record exists. This case is the executable evidence for GAP-TENANT-002 and becomes the regression guard for its acceptance criterion (2), which requires the three new routes to appear in `ApiSurface.approved.txt` in the same commit that adds them. It also explains why TC-TENANT-STATE-002 through -008 are Unit-level: there is nothing above the aggregate to test. |

---

## Batch coverage note

**Covered.** Twenty-five API cases and thirteen state cases, all `Not Run`, all against source read in this pass.

- *Provisioning end to end* — the atomic seed and every artefact it writes (`TC-TENANT-API-001`), with the exact measured shape of a provisioned tenant: 1 tenant row with all six settings columns at their declared defaults, 1 `TenantAdmin` account on the seeded `Tenant Administrator` role, 5 roles, **171** permission keys on the administrator role (recomputed from `PermissionCatalog.Modules`, `PermissionCatalog.cs:134-192`), 13 LOV categories / 50 entries, 1 outbox row and 1 hash-chained ledger row.
- *Slug handling and normalisation* — the full boundary set 1 / 2 / 50 / 51 (`API-004`, `-005`, `-006`), trim-and-lower normalisation on both the provisioning and the anonymous-lookup paths (`API-007`), six rejected character-class partitions including a **new** non-ASCII partition (`API-008`), and case-insensitive uniqueness (`API-003`).
- *Lifecycle states* — every reachable transition of the §3.1 matrix and all four illegal edges, plus the guard-evaluation **order** inside `Suspend` (`STATE-002`), the `Suspended → Terminated` shortcut the doc comment omits (`STATE-003`), the reason's trimming and its unbounded length (`STATE-006`), the reactivation cycle's event payloads (`STATE-007`), the unguarded MFA setter (`STATE-008`), the dead `UpdateSettings` and the five frozen settings (`STATE-009`), the `CHECK` domain (`STATE-010`), the downstream effects of a non-active status (`STATE-011`), post-suspension session survival (`STATE-012`), and the proof that no transition is reachable over HTTP (`STATE-013`).
- *Settings read/write and authorization* — the positive read and write with their exact ledger row (`API-015`, `-016`), and four distinct refusal shapes: `AUTHZ-403` from the HTTP filter (`API-017`), `AUTHZ-002` from the MediatR behaviour (`API-019`), `AUTHZ-403` for a `view`-only holder (`API-020`), and `TENANT-000` at 422 for a platform administrator (`API-018`).
- *Platform control plane and platform-admin-only access* — 403/401 on both `TenantsController` actions (`API-011`, `-012`), the versioned mirror (`API-014`), the four ungated `GET`s and the five gated writes in `PlatformControllers.cs` (`API-021`, `-022`), and the proof that a platform administrator reading a tenant-scoped list gets nothing (`API-023`).
- *Composite-PK consequences* — the tenant-qualified by-id fetch and its indistinguishable 404s (`API-024`), and a deliberate same-id-different-tenant collision with the `Single`-on-a-non-unique-id failure it enables (`API-025`).

**Could not cover, and why.**

1. **`TENANT-003` and `TENANT-004` at API level.** Both are structurally unreachable over HTTP — `ProvisionTenantValidator` rejects a blank or over-long name at 400 before `Tenant.Provision` can throw (`ProvisionTenant.cs:27` versus `Tenant.cs:43-51`). `TC-TENANT-API-009` records the proof; the codes themselves are Unit-level only and belong to batch A.
2. **Everything downstream of a supported suspension.** `TC-TENANT-STATE-011` and `-012` reach `Suspended` only by direct `UPDATE`, which bypasses the domain guards and writes no audit row. Both are `[GD]` on GAP-TENANT-002 and cannot be automated until a command exists.
3. **`TenantStatus.Provisioning`.** No supported operation reaches it (zero `TenantStatus.Provisioning` references in `src/` and `tests/`), so row 2 of the §3.1 matrix has no executable case. Asserted as unreachable in `TC-TENANT-STATE-001` rather than fabricated.
4. **Refresh-token behaviour for a non-active tenant.** `TC-TENANT-STATE-012` step 6 is labelled `[RNV]`: the refresh handler was **not read** in this pass, so no behaviour is claimed.
5. **Whether the HTTP-layer role-gate 403 emits a security event.** `TC-TENANT-API-011`'s audit row asks the question rather than asserting an answer — `ProblemAuthorizationResultHandler` was read and contains no logging call, but whether an upstream middleware records it was not established.
6. **The four non-administrator seeded-role permission counts.** Measured on `demo-lab` (165 / 90 / 65 / 47) but recorded as *reference* values, because `SystemRoleCatalog.SeedMissingAsync` is additive by role name (`SystemRoleCatalog.cs:53-79`) and an older tenant's role set can therefore lag the catalogue. Only the 171 is independently computable.

**New gaps found by this batch.** Numbered from 900 to avoid colliding with the front matter's `GAP-TENANT-001…014`.

| Gap | Finding | Source | Severity |
|---|---|---|---|
| **GAP-TENANT-901** | The front matter's measured isolation inventory is **stale**. Migration `20260801131521_QualityHealthProfile` was applied to dev *after* the 2026-08-01 inspection and added `qams.quality_health_profile` and `qams.quality_health_weight`. Re-measured now: **92** FORCE-RLS tables and **92** `tenant_isolation` policies (was 90/90); **90** tenant-first primary keys, 86 of arity 2 and 4 of arity 3 (URS-103 records 88); **0** NOT NULL-`tenant_id` tables without a tenant-first PK; **0** parity violations. Nothing has regressed — but every count in `12-module-tenancy-rls.md` §4.1, in URS-100 and in URS-103 is now wrong, and `ApiSurface.approved.txt` line numbers have shifted. **Recommendation:** cite the *sets*, never the counts, and add a CI assertion that regenerates the inventory. | `pg_class`/`pg_policies`/`pg_constraint` measured 2026-08-01; `src/NT.QAMS.Infrastructure/Persistence/Migrations/20260801131521_QualityHealthProfile.cs:15-16,76-77` | **Medium** (documentation integrity in a regulated package) |
| **GAP-TENANT-902** | A platform administrator **satisfies every `[RequirePermission]` gate unconditionally** — `RequestPrivileges.Has(key) => IsPlatformAdmin \|\| Permissions.Contains(key)` (`PrivilegeResolution.cs:39`), with `SetPlatformAdmin()` called for the `PlatformAdmin` tier at `RequestIdentity.cs:114-117`. On `TenantSettingsController` this means the permission gate does **not** stop them; they are stopped only by the absence of a `tenant_id` claim, arriving as `422 TENANT-000` rather than `403`. This **resolves the front-matter `[RNV]` footnote †** in §4.5 and corrects its `403` cells. **Acceptance criteria:** (1) state whether a platform administrator is intended to hold every tenant permission; (2) if not, the short circuit is removed or narrowed to a documented platform-permission set; (3) a functional test asserts the status and code for a platform administrator on every `[RequirePermission]`-gated tenant endpoint; (4) §4.5 is regenerated from that test. | `src/NT.QAMS.Infrastructure/Authorization/PrivilegeResolution.cs:39`; `src/NT.QAMS.WebApi/Middleware/RequestIdentity.cs:114-117`; `src/NT.QAMS.Application/Tenancy/Commands/TenantMfaPolicy.cs:19,31` | **High** |
| **GAP-TENANT-903** | The SPA's tenant-identifier validator disagrees with `TenantSlug` in four ways: it permits up to **63** characters against the domain's 50; it permits **consecutive hyphens** (`am--man`) and a **trailing hyphen** (`amman-`), both of which `^[a-z0-9](?:-?[a-z0-9]){1,49}$` rejects; and it rejects upper-case and surrounding whitespace, which the domain accepts and normalises. The operator therefore sees a server-side rejection for input the form approved, and cannot enter input the API would accept. **Acceptance criteria:** (1) the client pattern is derived from, or asserted equal to, the domain regex by a Karma spec; (2) the pattern's maximum matches `TenantSlug.MaxLength`; (3) `describe()` surfaces the per-field `errors` object rather than only the problem `title`. | `frontend/src/app/features/platform/tenants.component.ts:117` versus `src/NT.QAMS.Domain/Tenancy/TenantSlug.cs:12,47` | **Medium** |
| **GAP-TENANT-904** | The same form's password and display-name rules disagree with the API: `Validators.minLength(10)` against `PasswordRules.MinLength = 12`, with **no** complexity or breach-list check client-side; and `Validators.maxLength(200)` on `adminDisplayName` against `MaximumLength(150)` server-side. A first-administrator password is therefore rejected only after submission, and the message the operator sees is the generic `Validation failed.`. **Acceptance criteria:** (1) the form applies the same 12-character floor and the same four-character-class rule; (2) `adminDisplayName` is capped at 150; (3) the error renderer shows the field-level messages the API returns; (4) a Karma spec asserts the client and server floors are equal. | `frontend/src/app/features/platform/tenants.component.ts:120-121,160-165` versus `src/NT.QAMS.Application/IdentityAccess/PasswordRules.cs:17` and `src/NT.QAMS.Application/Tenancy/Commands/ProvisionTenant.cs:29-30` | **Medium** |
| **GAP-TENANT-905** | **A tenant's own creation is not in its own audit trail.** Because `ProvisionTenantHandler` calls `Elevate()` before any database work (`ProvisionTenant.cs:41`), the request tenant is never set, so every provisioning artefact is attributed to no tenant: measured **66/66** `audit.field_change` rows of `entity_type='Tenant'` carry `tenant_id IS NULL`, and all **68** `audit.audit_trail` rows of type `NT.QAMS.Domain.Tenancy.TenantProvisioned, NT.QAMS.Domain` carry `tenant_id = '00000000-0000-0000-0000-000000000000'` — the nil-tenant chain, sequences 1–100. Since the `audit` `USING` predicate is unchanged by the relaxed write rule, a null-tenant row is **invisible to every tenant read**, and a nil-tenant row matches no real tenant either. A laboratory's compliance view therefore cannot show when, by whom or under what identifier it was created. **Acceptance criteria:** (1) decide whether tenant creation is a platform record or a tenant record; (2) if the latter, the provisioning handler sets the tenant scope after `Tenant.Provision` so the ledger rows are attributed, and the hash chain entry lands in the new tenant's sequence; (3) if the former, `docs/reference` states it explicitly and the compliance-view documentation says the creation record is platform-only; (4) either way a test asserts the chosen attribution for a freshly provisioned tenant. | Measured `audit.field_change` and `audit.audit_trail`; `src/NT.QAMS.Application/Tenancy/Commands/ProvisionTenant.cs:41`; `src/NT.QAMS.Infrastructure/Persistence/Interceptors/FieldChangeInterceptor.cs:101-135`; `20260726103650_RelaxAuditRlsWriteCheck.cs:18` | **Medium** |

**ID-reservation conflict, recorded not resolved.** The front matter's reservation table assigns `TC-TENANT-API-001…020` and `TC-TENANT-STATE-001…020` to **batch A** and `TC-TENANT-API-021…040` to **batch C**; this assignment directed batch **D** to the same `API-001…` and `STATE-001…` blocks. No case file existed for this module when batch D was authored (`docs/testing/` held only the front matter), so no collision has occurred — but the reservation table in `12-module-tenancy-rls.md` must be amended to record that `TC-TENANT-API-001…025` and `TC-TENANT-STATE-001…013` are **consumed by batch D**, and batches A and C re-pointed, before either is written. A reservation and a consumption that disagree corrupt the traceability matrix exactly as surely as a duplicated id.
