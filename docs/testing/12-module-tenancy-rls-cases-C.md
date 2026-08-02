# TENANT — Detailed Test Cases, Batch C

This batch authors **32 detailed cases, `TC-TENANT-RLS-001` … `TC-TENANT-RLS-032`**, over one slice only: **row-level-security policy behaviour as PostgreSQL actually evaluates it**. Specifically — the two measured predicate shapes on representative tables read both as the *application* role and as the *owning* role (FORCE must filter the owner, and in dev `qams_app` **is** the owner of every application table, measured `pg_get_userbyid(relowner)='qams_app'`); the strict `qams.*` `WITH CHECK` refusing a foreign `tenant_id` with SQLSTATE `42501`; the relaxed `audit.*` `WITH CHECK` accepting a null-tenant append while `USING` keeps that row invisible to tenant reads; **the login regression** — a failed pre-authentication login writes a null-tenant `audit.field_change` row and must surface as `401 AUTH-001`, never HTTP 500; the accepted B9 deviation on `qams.user_account` and `qams.outbox_event`; and **positive** tenant-isolation coverage of `audit.security_event`, whose RLS gap was **closed in v1.51.2** (`Hardening2_RlsGapClosure`) — verified as `rls=true force=true policy=tenant_isolation` plus the tenant filter in `ComplianceLedgerStore.GetSecurityEventsAsync`. **Deliberately left to sibling batches:** the `Tenant` aggregate, `TenantSlug`, `TenantSettings` and the status matrix (batch A); the per-table isolation sweep across all 92 policy tables, owned-child composite-FK drift, elevation-path coverage, migration round-trip, the least-privilege role guard and the cross-tenant attack surface (batch B); the tenancy HTTP surface, tenant-resolution middleware, interceptor component tests, E2E, observability, DR and a11y (the rest of batch C's reservation). **Risk IDs are minted here** — `docs/validation/02-Functional-Risk-Assessment.md:51` records tenant isolation as a named function at **HIGH** with no numeric identifier, so `RSK-TENANT-001` (cross-tenant read leakage), `RSK-TENANT-002` (cross-tenant or forged write), `RSK-TENANT-003` (fail-open with no tenant context), `RSK-TENANT-004` (an isolation refusal breaking the authentication path), `RSK-TENANT-005` (an over-privileged database role voiding RLS) and `RSK-TENANT-006` (B9 residual) are new ids, declared as such per conventions §5. **All live figures below were measured read-only on 2026-08-01** against dev `ntqams` as `qams_app`; where a count is transaction-volume dependent the case asserts the *relationship* and quotes the measured value as the reference reading, never as a fixed oracle. Nothing here was executed; every `Result / Defect` is `Not Run · —`.

#### TC-TENANT-RLS-001 — Shape-S `USING` admits exactly the GUC tenant's rows on `qams.nonconformance`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-001 |
| **Level / Type / Technique** | Database (integration) · Functional (positive) · Equivalence Partitioning — the "GUC equals the row's tenant" partition of the shape-S `USING` predicate |
| **Priority / Severity / Automation** | Critical · Critical · Yes (integration, alongside `tests/NT.QAMS.IntegrationTests/RlsTenantIsolationTests.cs:19`) |
| **Role / Permission / Tenant** | n/a — database session, no application actor · n/a — RLS is evaluated below the permission layer · `demo-lab` = `019f960f-6a78-7481-a4f2-903042af86ae` |
| **Environment** | PostgreSQL 17, database `ntqams`, connected as `qams_app` (`rolsuper=false`, `rolbypassrls=false`, measured 2026-08-01) |
| **Preconditions** | `qams.nonconformance` has `relrowsecurity=t`, `relforcerowsecurity=t` and policy `tenant_isolation` with `USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid OR current_setting('app.bypass_rls', true) = 'on')`, created by `src/NT.QAMS.Infrastructure/Persistence/Migrations/20260726081443_ActivateForcedTenantRls.cs:34-44`. Rows exist for at least two tenants. |
| **Test Data** | Reference reading 2026-08-01: 40 `qams.nonconformance` rows total; 20 for `019f960f-6a78-7481-a4f2-903042af86ae` (`demo-lab`), 4 for `019f962d-7d1e-7b24-84ec-1299a38fcfed` (`arfa`) |
| **Steps** | 1. `SELECT set_config('app.bypass_rls','on',false);` and record `SELECT count(*) FROM qams.nonconformance` as `TOTAL` and `SELECT count(*) FROM qams.nonconformance WHERE tenant_id='019f960f-6a78-7481-a4f2-903042af86ae'` as `DEMO`. 2. `SELECT set_config('app.bypass_rls','off',false), set_config('app.current_tenant','019f960f-6a78-7481-a4f2-903042af86ae',false);`. 3. `SELECT count(*) FROM qams.nonconformance;`. 4. `SELECT count(DISTINCT tenant_id) FROM qams.nonconformance;`. |
| **Expected UI** | n/a — no user interface is involved in a direct database session. |
| **Expected API** | n/a — the case is below the HTTP layer by design. |
| **Expected DB** | Step 3 returns exactly `DEMO` (reference reading 20), strictly less than `TOTAL` (reference reading 40). Step 4 returns `1`, and that single value is `019f960f-6a78-7481-a4f2-903042af86ae`. |
| **Expected Audit** | No `audit.*` row is written — every statement is a `SELECT`. |
| **Expected Notification** | n/a — a read produces no notification. |
| **Cleanup** | `SELECT set_config('app.current_tenant','',false), set_config('app.bypass_rls','off',false);` — no data was written. |
| **Evidence** | psql transcript with both counts · `pg_policies` row for `qams.nonconformance` captured in the same session |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert `DEMO < TOTAL` computed in the same run rather than the literal 40/20; dev data grows. If `TOTAL = DEMO` the case proves nothing and must be re-seeded with a second tenant's row first. |

#### TC-TENANT-RLS-002 — FORCE row-level security filters the table **owner**, not only other roles  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-005 |
| **Level / Type / Technique** | Database (integration) · Security · Condition coverage — `relforcerowsecurity` as the deciding condition, with ownership held true |
| **Priority / Severity / Automation** | Critical · Critical · Yes (integration; today only implied by `tests/NT.QAMS.IntegrationTests/RealPostgresFixture.cs:42-51`) |
| **Role / Permission / Tenant** | Database role `qams_app`, which **owns** `qams.nonconformance` (measured `pg_get_userbyid(relowner)='qams_app'`) · n/a · unresolved (nil GUC) |
| **Environment** | PostgreSQL 17 `ntqams`, session as `qams_app` |
| **Preconditions** | Ordinary PostgreSQL RLS exempts a table's owner; only `ALTER TABLE … FORCE ROW LEVEL SECURITY` (`20260726081443_ActivateForcedTenantRls.cs:32`) subjects the owner to the policy. `qams.nonconformance` measured `relforcerowsecurity = t`. |
| **Test Data** | Reference reading 2026-08-01: owner session, nil GUC → 0 rows; owner session, `demo-lab` GUC → 20 rows; owner session, bypass on → 40 rows |
| **Steps** | 1. `SELECT current_user, pg_get_userbyid(c.relowner) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='qams' AND c.relname='nonconformance';` — confirm they are the same role. 2. `SELECT relrowsecurity, relforcerowsecurity FROM pg_class …` for the same table. 3. `SELECT set_config('app.current_tenant','',false), set_config('app.bypass_rls','off',false); SELECT count(*) FROM qams.nonconformance;`. 4. `SELECT set_config('app.current_tenant','019f960f-6a78-7481-a4f2-903042af86ae',false); SELECT count(*) FROM qams.nonconformance;`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | Step 1 shows `current_user = qams_app` and owner `= qams_app`. Step 2 returns `t, t`. Step 3 returns **0** despite ownership. Step 4 returns the demo-lab count (reference reading 20). |
| **Expected Audit** | No `audit.*` row — reads only. |
| **Expected Notification** | n/a — a read produces no notification. |
| **Cleanup** | Reset both GUCs to `''` / `off`. |
| **Evidence** | psql transcript showing owner identity, the two `pg_class` flags, and the two counts |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the case that makes every other RLS case in the package meaningful in dev, where the runtime role is the owner (`SCHEMA-HARDENING-REPORT.md` §6). In a production role-split installation step 1's two values differ and the case still holds; it is not skipped. |

#### TC-TENANT-RLS-003 — Removing FORCE re-exposes every tenant to the owner (negative control)  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-100 · RSK-TENANT-005 |
| **Level / Type / Technique** | Database (integration) · Security (negative control) · Decision Table — `relrowsecurity` × `relforcerowsecurity` × owner, only the FORCE column varied |
| **Priority / Severity / Automation** | High · Critical · Yes, but **only** in a disposable database — never against `ntqams` |
| **Role / Permission / Tenant** | `qams_app` as owner · n/a · `demo-lab` GUC set, bypass off |
| **Environment** | A throwaway PostgreSQL 17 database restored from `dotnet ef database update`, **not** the shared dev database |
| **Preconditions** | `Down()` of `20260726081443_ActivateForcedTenantRls.cs:63` is the documented way FORCE is removed; this case reproduces that single effect in isolation. |
| **Test Data** | Two nonconformance rows, one per tenant, created under `set_config('app.bypass_rls','on',true)` inside the test transaction |
| **Steps** | 1. `BEGIN;`. 2. Seed one row for tenant A and one for tenant B under a transaction-local bypass. 3. `SELECT set_config('app.bypass_rls','off',true), set_config('app.current_tenant','<A>',true); SELECT count(*) FROM qams.nonconformance WHERE nc_ref LIKE 'MUT-%';` — record as `WITH_FORCE`. 4. `ALTER TABLE qams.nonconformance NO FORCE ROW LEVEL SECURITY;`. 5. Repeat the count as `WITHOUT_FORCE`. 6. `ROLLBACK;`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | `WITH_FORCE = 1`; `WITHOUT_FORCE = 2`. The delta is exactly the other tenant's row, proving FORCE — not the policy alone — is what fences the owner. |
| **Expected Audit** | `audit.field_change` rows are produced by the seed inserts only if the seed goes through EF; via raw SQL none are produced. Either way all are discarded by the rollback. |
| **Expected Notification** | n/a — no domain event is raised by raw SQL seeding. |
| **Cleanup** | `ROLLBACK;` — the `ALTER TABLE` is transactional in PostgreSQL and is undone with the data. Re-assert `relforcerowsecurity = t` after rollback before releasing the database. |
| **Evidence** | psql transcript containing both counts and the post-rollback `pg_class` re-assertion |
| **Result / Defect** | Not Run · — |
| **Notes** | `[ID]` — no URS requires the *absence* of protection to be demonstrated; this exists because a control with no falsification step is an assertion, not evidence. Refuse to run it if `current_database() = 'ntqams'`. |

#### TC-TENANT-RLS-004 — Nil-UUID tenant GUC is fail-closed on a shape-S table  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-003 |
| **Level / Type / Technique** | Database (integration) · Functional (negative) · BVA — the nil UUID is the boundary value the interceptor emits when no tenant is resolved |
| **Priority / Severity / Automation** | Critical · Critical · Yes (`RlsTenantIsolationTests.cs:52` asserts the same shape) |
| **Role / Permission / Tenant** | n/a — database session · n/a · **unresolved** — `app.current_tenant = '00000000-0000-0000-0000-000000000000'` |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app` |
| **Preconditions** | `TenantConnectionInterceptor` binds `currentTenant.TenantId?.ToString() ?? NilTenant` where `NilTenant = Guid.Empty.ToString()` (`src/NT.QAMS.Infrastructure/Persistence/Interceptors/TenantConnectionInterceptor.cs:21,55`), so the nil UUID is the exact value an unresolved request produces. |
| **Test Data** | GUC literal `00000000-0000-0000-0000-000000000000`; target tables `qams.nonconformance`, `qams.controlled_document`, `qams.equipment_item`, `qams.risk_item`, `read.kpi_snapshot` |
| **Steps** | 1. `SELECT set_config('app.current_tenant','00000000-0000-0000-0000-000000000000',false), set_config('app.bypass_rls','off',false);`. 2. `SELECT count(*) FROM qams.nonconformance;`. 3. Repeat for `qams.controlled_document`, `qams.equipment_item`, `qams.risk_item`, `read.kpi_snapshot`. 4. `SELECT count(*) FROM saas.tenant WHERE id='00000000-0000-0000-0000-000000000000';` to confirm no tenant actually owns the nil id. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | Steps 2 and 3 each return **0**. Step 4 returns **0**, so the nil value is not a live tenant that could accidentally own rows. |
| **Expected Audit** | No `audit.*` row — reads only. |
| **Expected Notification** | n/a — a read produces no notification. |
| **Cleanup** | Reset `app.current_tenant` to `''`. |
| **Evidence** | psql transcript, five zero counts plus the `saas.tenant` probe |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 4 matters: fail-closed by "matches no row" is only safe while no row carries the nil tenant. A future backfill that defaults `tenant_id` to the nil UUID would silently convert fail-closed into fail-open. `20260731201114_Hardening4_ChildTenancy.cs:14-252` uses exactly that default transiently before dropping it — worth re-checking after any similar migration. |

#### TC-TENANT-RLS-005 — Empty-string tenant GUC is fail-closed through `NULLIF`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-003 |
| **Level / Type / Technique** | Database (integration) · Functional (negative) · BVA — the empty string is the lower boundary of the GUC's value domain |
| **Priority / Severity / Automation** | High · Critical · Yes |
| **Role / Permission / Tenant** | n/a — database session · n/a · GUC set to the empty string |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app` |
| **Preconditions** | The predicate is `tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid …`; `NULLIF('','')` yields `NULL`, and `tenant_id = NULL` evaluates to `NULL`, which RLS treats as not-permitted. Measured predicate text confirmed in `pg_policies` on 2026-08-01. |
| **Test Data** | `app.current_tenant = ''`, `app.bypass_rls = 'off'` |
| **Steps** | 1. `SELECT set_config('app.current_tenant','',false), set_config('app.bypass_rls','off',false);`. 2. `SELECT count(*) FROM qams.nonconformance;`. 3. `SELECT count(*) FROM audit.security_event;`. 4. `SELECT (NULLIF(current_setting('app.current_tenant', true), '') IS NULL) AS nullified;`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | Steps 2 and 3 return **0**; step 4 returns `t`. No error is raised — the empty string is a *silent* zero-row outcome, distinct from TC-TENANT-RLS-009. |
| **Expected Audit** | No `audit.*` row — reads only. |
| **Expected Notification** | n/a — a read produces no notification. |
| **Cleanup** | None required; the session GUC is already `''`. |
| **Evidence** | psql transcript with the two counts and the `nullified` probe |
| **Result / Defect** | Not Run · — |
| **Notes** | The empty string is not a value the interceptor can emit (it always writes a UUID string), so this partition is reachable only from raw SQL or a future non-EF client. It is authored because the predicate explicitly handles it. |

#### TC-TENANT-RLS-006 — An entirely unset tenant GUC is fail-closed, not an error  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-003 |
| **Level / Type / Technique** | Database (integration) · Functional (negative) · BVA — the "GUC never set on this session" boundary, distinct from empty and from nil |
| **Priority / Severity / Automation** | High · Critical · Yes |
| **Role / Permission / Tenant** | n/a — database session · n/a · no tenant GUC set at all |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app`, a **freshly opened** connection on which no `set_config` has run |
| **Preconditions** | `current_setting('app.current_tenant', true)` uses `missing_ok = true`, so an unset GUC yields `NULL` rather than SQLSTATE `42704`. Same for `app.bypass_rls`. Predicate text as measured in `pg_policies`. |
| **Test Data** | A brand-new psql session; no `set_config` issued before step 2 |
| **Steps** | 1. Open a new connection: `psql -h localhost -U qams_app -d ntqams`. 2. `SELECT current_setting('app.current_tenant', true) IS NULL AS unset;`. 3. `SELECT count(*) FROM qams.nonconformance;`. 4. `SELECT count(*) FROM audit.field_change;`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | Step 2 returns `t`. Steps 3 and 4 return **0** with no error raised. |
| **Expected Audit** | No `audit.*` row — reads only. |
| **Expected Notification** | n/a — a read produces no notification. |
| **Cleanup** | Close the session; nothing was set or written. |
| **Evidence** | psql transcript beginning with the connection banner, proving the session is fresh |
| **Result / Defect** | Not Run · — |
| **Notes** | Do not reuse a session from an earlier case — `set_config(..., false)` is session-scoped (`TenantConnectionInterceptor.cs:53-54`, `is_local = false`) and would leave a value behind, invalidating the boundary. |

#### TC-TENANT-RLS-007 — The bypass token is an exact, case-sensitive match on `'on'`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-001 |
| **Level / Type / Technique** | Database (integration) · Security (negative) · Equivalence Partitioning over the bypass token's value domain, one representative per partition |
| **Priority / Severity / Automation** | High · High · Yes |
| **Role / Permission / Tenant** | n/a — database session · n/a · `demo-lab` = `019f960f-6a78-7481-a4f2-903042af86ae` |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app` |
| **Preconditions** | The predicate term is a literal string equality `current_setting('app.bypass_rls', true) = 'on'` (`20260726081443_ActivateForcedTenantRls.cs:38,42`), and the interceptor emits only `"on"` or `"off"` (`TenantConnectionInterceptor.cs:56`). |
| **Test Data** | Tokens `on`, `ON`, `On`, `true`, `TRUE`, `1`, `yes`, `off`, empty string. Reference reading 2026-08-01: `demo-lab` sees 20 nonconformances, elevated sees 40. |
| **Steps** | 1. `SELECT set_config('app.current_tenant','019f960f-6a78-7481-a4f2-903042af86ae',false);`. 2. For each token T in the list: `SELECT set_config('app.bypass_rls','T',false); SELECT count(*) FROM qams.nonconformance;`. 3. Record the nine counts. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | Token `on` returns the elevated total (reference reading 40). Every other token — `ON`, `On`, `true`, `TRUE`, `1`, `yes`, `off`, `''` — returns the tenant-only count (reference reading 20). Measured on 2026-08-01: `ON` → 20, `true` → 20. |
| **Expected Audit** | No `audit.*` row — reads only. |
| **Expected Notification** | n/a — a read produces no notification. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','off',false), set_config('app.current_tenant','',false);` |
| **Evidence** | psql transcript listing all nine token/count pairs |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert the two distinct counts computed in the same run, not the literals. The security value is that a truthy-looking token is not a bypass — an operator who "turns on" the GUC with `true` gets no elevation and no warning. |

#### TC-TENANT-RLS-008 — MC/DC over the two-condition shape-S `USING` predicate  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-001 |
| **Level / Type / Technique** | Database (integration) · Functional · MC-DC — each of the two conditions shown to independently determine the outcome |
| **Priority / Severity / Automation** | High · Critical · Yes |
| **Role / Permission / Tenant** | n/a — database session · n/a · varies per row of the matrix |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app` |
| **Preconditions** | `USING (C1 OR C2)` where `C1` is `tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid` and `C2` is `current_setting('app.bypass_rls', true) = 'on'`. Rows exist for `demo-lab` and for at least one other tenant. |
| **Test Data** | Four GUC pairs against a `demo-lab`-owned row: (a) tenant=`019f960f-6a78-7481-a4f2-903042af86ae`, bypass=`off`; (b) tenant=`019f962d-7d1e-7b24-84ec-1299a38fcfed`, bypass=`off`; (c) tenant=`019f962d-…`, bypass=`on`; (d) tenant=`019f960f-…`, bypass=`on` |
| **Steps** | 1. Under bypass, pick one `demo-lab` nonconformance id: `SELECT id FROM qams.nonconformance WHERE tenant_id='019f960f-6a78-7481-a4f2-903042af86ae' LIMIT 1;` — call it `R`. 2. For each GUC pair (a)–(d), set both GUCs and run `SELECT count(*) FROM qams.nonconformance WHERE id='R';`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | (a) C1=T, C2=F → **1**. (b) C1=F, C2=F → **0**. (c) C1=F, C2=T → **1**. (d) C1=T, C2=T → **1**. Pair (a)/(b) shows C1 alone flips the outcome with C2 held false; pair (b)/(c) shows C2 alone flips it with C1 held false. Both conditions are therefore MC/DC-covered by four tests. |
| **Expected Audit** | No `audit.*` row — reads only. |
| **Expected Notification** | n/a — a read produces no notification. |
| **Cleanup** | Reset both GUCs. |
| **Evidence** | psql transcript with the four labelled counts and the chosen `R` |
| **Result / Defect** | Not Run · — |
| **Notes** | Row (d) is the redundant masking case and is included so the truth table is complete; it is not what earns the MC/DC claim. Keep `R` fixed across all four steps — re-picking under a different GUC would silently change the subject. |

#### TC-TENANT-RLS-009 — A malformed non-empty tenant GUC fails **loud** with SQLSTATE 22P02, and bypass does not rescue it  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · no URS covers this — see **GAP-TENANT-904** · RSK-TENANT-003 |
| **Level / Type / Technique** | Database (integration) · Robustness (negative) · Error Guessing — a value the interceptor cannot emit but a raw client can |
| **Priority / Severity / Automation** | Medium · Medium · Yes |
| **Role / Permission / Tenant** | n/a — database session · n/a · GUC set to a non-UUID string |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app`, `\set VERBOSITY verbose` so the SQLSTATE is printed |
| **Preconditions** | `NULLIF(current_setting('app.current_tenant', true), '')::uuid` casts unconditionally when the value is non-empty. Measured 2026-08-01: `app.current_tenant='not-a-uuid'` raises `ERROR: 22P02: invalid input syntax for type uuid: "not-a-uuid"` at `string_to_uuid, uuid.c:141`. |
| **Test Data** | `app.current_tenant = 'not-a-uuid'`; then the same with `app.bypass_rls = 'on'` |
| **Steps** | 1. `\set VERBOSITY verbose`. 2. `SELECT set_config('app.current_tenant','not-a-uuid',false), set_config('app.bypass_rls','off',false);`. 3. `SELECT count(*) FROM qams.nonconformance;` — capture the SQLSTATE. 4. `SELECT set_config('app.bypass_rls','on',false);`. 5. `SELECT count(*) FROM qams.nonconformance;` — capture the SQLSTATE again. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a at this level. If the condition were ever reachable through the application it would arrive as `DbUpdateException`/`NpgsqlException` matching no arm of `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:81` and surface as an untyped **HTTP 500** — the same shape as **GAP-TENANT-006**. |
| **Expected DB** | Step 3 raises SQLSTATE **`22P02`**, message `invalid input syntax for type uuid: "not-a-uuid"`, and returns no rows. Step 5 raises the **same** `22P02` — `app.bypass_rls='on'` does **not** short-circuit the cast, so elevation cannot recover a poisoned tenant GUC. |
| **Expected Audit** | No `audit.*` row — the statement aborts before any write. |
| **Expected Notification** | n/a — a failed read produces no notification. |
| **Cleanup** | `SELECT set_config('app.current_tenant','',false), set_config('app.bypass_rls','off',false);` — the session is otherwise poisoned for every subsequent RLS-table query. |
| **Evidence** | psql transcript showing both `22P02` errors with `VERBOSITY verbose` |
| **Result / Defect** | Not Run · — |
| **Notes** | `[ID]` — behaviour measured in the database with no matching requirement. Not reachable from the application today: `TenantConnectionInterceptor.cs:55` binds a `Guid.ToString()` or the nil UUID and never user text. The front matter's §4.2 truth table has no row for a malformed **non-empty** GUC; this case supplies it and **GAP-TENANT-904** records the omission. |

#### TC-TENANT-RLS-010 — Elevation reveals every tenant's rows on a shape-S table  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-001 |
| **Level / Type / Technique** | Database (integration) · Functional (positive) · Equivalence Partitioning — the "bypass on" partition |
| **Priority / Severity / Automation** | High · High · Yes (`RlsTenantIsolationTests.cs:53`) |
| **Role / Permission / Tenant** | n/a — database session standing in for an elevated background scope · n/a · elevated, `app.bypass_rls='on'` |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app` |
| **Preconditions** | `ICurrentTenantSetter.Elevate()` (`src/NT.QAMS.Infrastructure/Services/RequestContext.cs:26`) makes the interceptor stamp `app.bypass_rls='on'` (`TenantConnectionInterceptor.cs:56`). The database session reproduces that stamp directly. |
| **Test Data** | Reference reading 2026-08-01: 40 nonconformances across 23 tenants; `demo-lab` 20, `arfa` 4 |
| **Steps** | 1. `SELECT set_config('app.current_tenant','019f960f-6a78-7481-a4f2-903042af86ae',false), set_config('app.bypass_rls','off',false); SELECT count(*) FROM qams.nonconformance;` — record `SCOPED`. 2. `SELECT set_config('app.bypass_rls','on',false); SELECT count(*) FROM qams.nonconformance;` — record `ELEVATED`. 3. `SELECT count(DISTINCT tenant_id) FROM qams.nonconformance;` under the same elevated session. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | `ELEVATED > SCOPED` (reference reading 40 > 20) and step 3 returns more than 1 distinct tenant. Elevation is shown to widen visibility while the tenant GUC is unchanged — the tenant GUC is irrelevant once `C2` is true. |
| **Expected Audit** | No `audit.*` row — reads only. |
| **Expected Notification** | n/a — a read produces no notification. |
| **Cleanup** | `SELECT set_config('app.bypass_rls','off',false), set_config('app.current_tenant','',false);` |
| **Evidence** | psql transcript with both counts and the distinct-tenant count |
| **Result / Defect** | Not Run · — |
| **Notes** | At the application layer elevation alone is **not** sufficient to read cross-tenant: `AppDbContext`'s global filter compares against a `TenantId` that is `null` under elevation, so `.IgnoreQueryFilters()` is also required (`src/NT.QAMS.Infrastructure/Persistence/AppDbContext.cs:186-208`). That composition belongs to batch B; this case isolates the database half. |

#### TC-TENANT-RLS-011 — Strict `WITH CHECK` accepts an insert stamped with the session's own tenant  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-002 |
| **Level / Type / Technique** | Database (integration) · Functional (positive control) · Decision Table — the accepted row of the shape-S write table |
| **Priority / Severity / Automation** | Critical · High · Yes |
| **Role / Permission / Tenant** | n/a — database session · n/a · `demo-lab` = `019f960f-6a78-7481-a4f2-903042af86ae` |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app`, inside an explicit transaction that is rolled back |
| **Preconditions** | `qams.ref_counter` carries the strict shape created by `20260731181845_Hardening2_RlsGapClosure.cs:37-42`; its `tenant_id` is `NOT NULL` and it has a `DEFERRABLE INITIALLY DEFERRED` FK to `saas.tenant`, so the tenant used must actually exist. |
| **Test Data** | `INSERT INTO qams.ref_counter (tenant_id, ref_type, year, last_value) VALUES ('019f960f-6a78-7481-a4f2-903042af86ae','RLSTC011',2099,1)` |
| **Steps** | 1. `BEGIN;`. 2. `SELECT set_config('app.current_tenant','019f960f-6a78-7481-a4f2-903042af86ae',true), set_config('app.bypass_rls','off',true);`. 3. Execute the insert above. 4. `SELECT last_value FROM qams.ref_counter WHERE ref_type='RLSTC011' AND year=2099;`. 5. `ROLLBACK;`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | Step 3 reports `INSERT 0 1` with no error. Step 4 returns `1`. This is the discriminating control for TC-TENANT-RLS-012 — without it, a refusal there could mean "all writes blocked" rather than "foreign writes blocked". |
| **Expected Audit** | None — raw SQL bypasses `FieldChangeInterceptor`, and `RefCounter` is on its exclusion list anyway (`src/NT.QAMS.Infrastructure/Persistence/Interceptors/FieldChangeInterceptor.cs:29-31`). |
| **Expected Notification** | n/a — no domain event is raised by raw SQL. |
| **Cleanup** | `ROLLBACK;` — nothing persists. |
| **Evidence** | psql transcript with the `INSERT 0 1` acknowledgement and the read-back |
| **Result / Defect** | Not Run · — |
| **Notes** | Use `set_config(..., true)` (transaction-local) so the rollback also restores the GUCs. Choose a `ref_type` that no production counter uses; `RLSTC011` is reserved for this case. |

#### TC-TENANT-RLS-012 — Strict `WITH CHECK` refuses an insert carrying a foreign `tenant_id` with SQLSTATE 42501  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-002 |
| **Level / Type / Technique** | Database (integration) · Security (negative) · Decision Table — the refused row of the shape-S write table |
| **Priority / Severity / Automation** | Critical · Critical · Yes (`RlsTenantIsolationTests.cs:56-75` asserts the exception type; this case pins the SQLSTATE) |
| **Role / Permission / Tenant** | n/a — database session · n/a · session scoped to `demo-lab`, row stamped with `arfa` |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app`, `\set VERBOSITY verbose`, inside a rolled-back transaction |
| **Preconditions** | Shape-S `WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid OR current_setting('app.bypass_rls', true) = 'on')`, measured on `qams.ref_counter` 2026-08-01. |
| **Test Data** | Session tenant `019f960f-6a78-7481-a4f2-903042af86ae` (`demo-lab`); row tenant `019f962d-7d1e-7b24-84ec-1299a38fcfed` (`arfa`) — a real tenant, so the FK cannot be the cause of the refusal |
| **Steps** | 1. `BEGIN;`. 2. `SELECT set_config('app.current_tenant','019f960f-6a78-7481-a4f2-903042af86ae',true), set_config('app.bypass_rls','off',true);`. 3. `INSERT INTO qams.ref_counter (tenant_id, ref_type, year, last_value) VALUES ('019f962d-7d1e-7b24-84ec-1299a38fcfed','RLSTC012',2099,1);`. 4. Capture the SQLSTATE and message. 5. `ROLLBACK;`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a at this level. Through the application the same refusal arrives as `DbUpdateException` wrapping `PostgresException` `42501`, matches no arm of `DomainExceptionHandler.cs:26-82`, and becomes an untyped **HTTP 500** — the defect recorded as **GAP-TENANT-006**. Do not assert a typed code here. |
| **Expected DB** | SQLSTATE **`42501`**, message `new row violates row-level security policy for table "ref_counter"`. Zero rows inserted. The error must be the RLS violation, **not** `23503` — if `23503` appears, the tenant chosen is not a real `saas.tenant` row and the case is invalid. |
| **Expected Audit** | None — the statement aborts, and `RefCounter` is excluded from `FieldChangeInterceptor` regardless. |
| **Expected Notification** | n/a — a refused write produces no notification. |
| **Cleanup** | `ROLLBACK;` |
| **Evidence** | psql transcript with the verbose `42501` line and the failing statement |
| **Result / Defect** | Not Run · — |
| **Notes** | The SQLSTATE distinction is load-bearing: `42501` proves RLS refused it, `23503` would prove only that a foreign key refused it. `OwnedChildTenancyTests.cs:24,114` pins `23503` for the composite-FK case; the two must never be conflated. |

#### TC-TENANT-RLS-013 — An `UPDATE` that moves a row to another tenant is refused by `WITH CHECK`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-002 |
| **Level / Type / Technique** | Database (integration) · Security (negative) · Path — the `UPDATE` path, where `USING` gates the old row and `WITH CHECK` gates the new one |
| **Priority / Severity / Automation** | High · Critical · Yes — this path has **no** existing automated coverage |
| **Role / Permission / Tenant** | n/a — database session · n/a · session scoped to `demo-lab` |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app`, `\set VERBOSITY verbose`, inside a rolled-back transaction |
| **Preconditions** | The policy is `FOR ALL`, so a single `tenant_isolation` policy supplies both `USING` (which row may be updated) and `WITH CHECK` (what it may become). Confirmed `cmd=ALL` in `pg_policies` for `qams.nonconformance` on 2026-08-01. |
| **Test Data** | One `demo-lab` nonconformance id `R`; target tenant `019f962d-7d1e-7b24-84ec-1299a38fcfed` (`arfa`) |
| **Steps** | 1. `BEGIN;`. 2. `SELECT set_config('app.bypass_rls','on',true); SELECT id FROM qams.nonconformance WHERE tenant_id='019f960f-6a78-7481-a4f2-903042af86ae' LIMIT 1;` — record `R`. 3. `SELECT set_config('app.bypass_rls','off',true), set_config('app.current_tenant','019f960f-6a78-7481-a4f2-903042af86ae',true);`. 4. `UPDATE qams.nonconformance SET tenant_id='019f962d-7d1e-7b24-84ec-1299a38fcfed' WHERE id='R';` — capture the SQLSTATE. 5. `UPDATE qams.nonconformance SET tenant_id='019f960f-6a78-7481-a4f2-903042af86ae' WHERE id='R';` — the no-op control. 6. `ROLLBACK;`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — no endpoint permits writing `tenant_id`; `TenantStampInterceptor` sets it and no DTO exposes it. |
| **Expected DB** | Step 4 raises SQLSTATE **`42501`**, `new row violates row-level security policy for table "nonconformance"`, and updates 0 rows. Step 5 reports `UPDATE 1`, proving the same statement shape is accepted when the tenant is unchanged. |
| **Expected Audit** | None from raw SQL (`FieldChangeInterceptor` is an EF interceptor and is not in the path). |
| **Expected Notification** | n/a — no domain event is raised by raw SQL. |
| **Cleanup** | `ROLLBACK;` |
| **Evidence** | psql transcript with the `42501` on step 4 and `UPDATE 1` on step 5 |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 5 is essential — without it the case cannot distinguish "the tenant move was refused" from "the row was invisible to `USING` in the first place". Note the composite PK is `(tenant_id, id)` (schema hardening Phase 5), so this `UPDATE` also rewrites the primary key; the RLS refusal fires first. |

#### TC-TENANT-RLS-014 — `qams.ref_counter` carries the **strict** shape because its `tenant_id` is `NOT NULL`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-100 · RSK-TENANT-002 |
| **Level / Type / Technique** | Database (integration) · Structural · Equivalence Partitioning — strict vs relaxed as the two policy-shape partitions, keyed on column nullability |
| **Priority / Severity / Automation** | High · High · Yes |
| **Role / Permission / Tenant** | n/a — catalog query · n/a · n/a — the assertion is schema-level, not row-level |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app` |
| **Preconditions** | `20260731181845_Hardening2_RlsGapClosure.cs:30-43` states the rationale in the migration itself: "Standard tenant policy; no null allowance (the column is NOT NULL)". |
| **Test Data** | Tables `qams.ref_counter` (strict expected) and `audit.security_event` (relaxed expected) |
| **Steps** | 1. `SELECT a.attnotnull FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='qams' AND c.relname='ref_counter' AND a.attname='tenant_id';`. 2. `SELECT with_check FROM pg_policies WHERE schemaname='qams' AND tablename='ref_counter' AND policyname='tenant_isolation';`. 3. Repeat both for `audit.security_event`. 4. `SELECT relrowsecurity, relforcerowsecurity FROM pg_class …` for `qams.ref_counter`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | Step 1 returns `t` (NOT NULL). Step 2's `with_check` text contains **no** `tenant_id IS NULL` term. Step 3 returns `f` (nullable) and a `with_check` that **does** contain `tenant_id IS NULL`. Step 4 returns `t, t`. Measured 2026-08-01: exactly these values. |
| **Expected Audit** | No `audit.*` row — catalog reads only. |
| **Expected Notification** | n/a — a read produces no notification. |
| **Cleanup** | None — no session state changed. |
| **Evidence** | psql transcript with the four catalog results |
| **Result / Defect** | Not Run · — |
| **Notes** | The pairing "nullable tenant ⇒ relaxed write, NOT NULL tenant ⇒ strict write" is an invariant worth an architecture assertion, not just a case; there is no automated test for it today. Batch B's structural sweep covers presence of RLS, not the *shape* of the check. |

#### TC-TENANT-RLS-015 — A `DELETE` targeting another tenant's row silently affects zero rows rather than erroring  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-001 |
| **Level / Type / Technique** | Database (integration) · Robustness · Error Guessing — the asymmetry between a refused write (`42501`) and an invisible target (0 rows) |
| **Priority / Severity / Automation** | Medium · Medium · Yes |
| **Role / Permission / Tenant** | n/a — database session · n/a · session scoped to `demo-lab`, target row owned by `arfa` |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app`, inside a rolled-back transaction |
| **Preconditions** | For `DELETE`, only `USING` applies — a row the policy hides is simply not a candidate, so PostgreSQL reports success with a zero row count instead of raising. |
| **Test Data** | One `arfa` nonconformance id `Q`, obtained under bypass |
| **Steps** | 1. `BEGIN;`. 2. `SELECT set_config('app.bypass_rls','on',true); SELECT id FROM qams.nonconformance WHERE tenant_id='019f962d-7d1e-7b24-84ec-1299a38fcfed' LIMIT 1;` — record `Q`. 3. `SELECT set_config('app.bypass_rls','off',true), set_config('app.current_tenant','019f960f-6a78-7481-a4f2-903042af86ae',true);`. 4. `DELETE FROM qams.nonconformance WHERE id='Q';`. 5. `SELECT set_config('app.bypass_rls','on',true); SELECT count(*) FROM qams.nonconformance WHERE id='Q';`. 6. `ROLLBACK;`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a at this level. Through the application an EF delete of an entity the query filter never returned cannot occur; a raw `ExecuteDelete` could, and would return 0 with no exception. |
| **Expected DB** | Step 4 reports `DELETE 0` and raises **no** error. Step 5 returns `1` — the row survives untouched. |
| **Expected Audit** | None — no row changed, so no `audit.field_change` row is produced even via EF. |
| **Expected Notification** | n/a — no domain event is raised. |
| **Cleanup** | `ROLLBACK;` |
| **Evidence** | psql transcript showing `DELETE 0` followed by the surviving-row count |
| **Result / Defect** | Not Run · — |
| **Notes** | `[ID]` — a measured behaviour of PostgreSQL RLS with no matching requirement. Operationally important: a cross-tenant delete attempt is **silent**, so it produces no `42501` to alert on. Any monitoring built on "isolation violations raise 42501" will not see it. |

#### TC-TENANT-RLS-016 — Relaxed audit `WITH CHECK` accepts a null-tenant append while the session is scoped to a tenant  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-011, URS-100 · RSK-TENANT-004 |
| **Level / Type / Technique** | Database (integration) · Functional (positive) · Decision Table — row `G=A, B=off, t=NULL` of the shape-R write table |
| **Priority / Severity / Automation** | High · High · Yes |
| **Role / Permission / Tenant** | n/a — database session · n/a · scoped to `demo-lab`, row written with `tenant_id = NULL` |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app`, inside a rolled-back transaction |
| **Preconditions** | `audit.field_change` policy measured 2026-08-01: `WITH CHECK ((tenant_id IS NULL) OR (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid) OR (current_setting('app.bypass_rls', true) = 'on'))`, from `20260726103650_RelaxAuditRlsWriteCheck.cs:36-40`. |
| **Test Data** | `entity_type='RlsTc016'`, `entity_id='probe'`, `action='Modified'`, `property='FailedLoginAttempts'`, `old_value='0'`, `new_value='1'`, `actor='system'`, `tenant_id=NULL` |
| **Steps** | 1. `BEGIN;`. 2. `SELECT set_config('app.current_tenant','019f960f-6a78-7481-a4f2-903042af86ae',true), set_config('app.bypass_rls','off',true);`. 3. `INSERT INTO audit.field_change (id, tenant_id, entity_type, entity_id, action, property, old_value, new_value, actor, occurred_at_utc) VALUES (gen_random_uuid(), NULL, 'RlsTc016','probe','Modified','FailedLoginAttempts','0','1','system', now());`. 4. `SELECT set_config('app.bypass_rls','on',true); SELECT count(*) FROM audit.field_change WHERE entity_type='RlsTc016';`. 5. `ROLLBACK;`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | Step 3 reports `INSERT 0 1` with no error. Step 4 returns `1`, visible only because bypass was turned on. |
| **Expected Audit** | The inserted row **is** the audit artefact; no second-order audit row is produced (`audit.field_change` is on `FieldChangeInterceptor`'s exclusion list, `FieldChangeInterceptor.cs:29-30`, and raw SQL is not in the interceptor path at all). |
| **Expected Notification** | n/a — the ledger raises no notification. |
| **Cleanup** | `ROLLBACK;` — the append-only trigger `field_change_append_only` (`audit.reject_mutation`) makes `DELETE` impossible, so a rollback is the **only** cleanup available. |
| **Evidence** | psql transcript with the `INSERT 0 1` and the bypassed read-back |
| **Result / Defect** | Not Run · — |
| **Notes** | Never run this outside a transaction. `pg_get_triggerdef` confirms `BEFORE DELETE OR UPDATE ON audit.field_change … EXECUTE FUNCTION audit.reject_mutation()`, which raises `audit ledgers are append-only` — an escaped probe row cannot be removed. |

#### TC-TENANT-RLS-017 — Relaxed audit `WITH CHECK` accepts a null-tenant append under the **nil** tenant GUC  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-011 · RSK-TENANT-004 |
| **Level / Type / Technique** | Database (integration) · Functional (positive) · Decision Table — the pre-authentication row: `G=nil, B=off, t=NULL` |
| **Priority / Severity / Automation** | Critical · Critical · Yes — this is the exact database condition of the login regression |
| **Role / Permission / Tenant** | n/a — database session standing in for an unauthenticated request · n/a · unresolved, GUC = nil UUID |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app`, inside a rolled-back transaction |
| **Preconditions** | An unauthenticated or platform-admin request produces `app.current_tenant = '00000000-0000-0000-0000-000000000000'` (`TenantConnectionInterceptor.cs:21,55`). The relaxed check's **first** disjunct `tenant_id IS NULL` is what admits the row; the second disjunct would evaluate `NULL = '000…0'::uuid` → `NULL`. |
| **Test Data** | Same probe row shape as TC-TENANT-RLS-016 but `entity_type='RlsTc017'`; GUC nil |
| **Steps** | 1. `BEGIN;`. 2. `SELECT set_config('app.current_tenant','00000000-0000-0000-0000-000000000000',true), set_config('app.bypass_rls','off',true);`. 3. Insert the null-tenant `audit.field_change` probe row with `entity_type='RlsTc017'`. 4. Insert a null-tenant `audit.security_event` probe: `INSERT INTO audit.security_event (id, tenant_id, event_type, actor, detail, occurred_at_utc) VALUES (gen_random_uuid(), NULL, 'RLSTC017_LOGIN_FAILED','probe@localhost','bad-password', now());`. 5. `SELECT count(*) FROM audit.field_change WHERE entity_type='RlsTc017';` under the same nil GUC. 6. `SELECT set_config('app.bypass_rls','on',true);` and repeat the count. 7. `ROLLBACK;`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a at this level; the application-level consequence is TC-TENANT-RLS-022. |
| **Expected DB** | Steps 3 and 4 each report `INSERT 0 1`. Step 5 returns **0** — the writer cannot read back what it just wrote, because `USING` was left strict. Step 6 returns **1**. |
| **Expected Audit** | The two inserted rows are themselves the audit artefacts; no further audit row is generated. |
| **Expected Notification** | n/a — the ledgers raise no notification. |
| **Cleanup** | `ROLLBACK;` — mandatory; the append-only triggers on both tables forbid `DELETE`. |
| **Evidence** | psql transcript covering both inserts and the 0-then-1 read-back |
| **Result / Defect** | Not Run · — |
| **Notes** | The write-but-cannot-read asymmetry is deliberate and documented at `20260726103650_RelaxAuditRlsWriteCheck.cs:18`. It is the single most misread property of the audit ledgers: a null-tenant row is **platform-only**, not "everyone's". |

#### TC-TENANT-RLS-018 — Relaxed audit `WITH CHECK` still refuses a **foreign non-null** tenant on the ledger  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-011 · RSK-TENANT-002 |
| **Level / Type / Technique** | Database (integration) · Security (negative) · Decision Table — row `G=A, B=off, t=B` of the shape-R write table |
| **Priority / Severity / Automation** | Critical · Critical · Yes (`SecurityEventRlsTests.cs:81-86` asserts this for `security_event`; this case extends it to `audit.field_change`) |
| **Role / Permission / Tenant** | n/a — database session · n/a · scoped to `demo-lab`, row stamped `arfa` |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app`, `\set VERBOSITY verbose`, inside a rolled-back transaction |
| **Preconditions** | Relaxing the write check must not have relaxed *forgery*: only `NULL` and the session's own tenant are admitted, per `20260726103650_RelaxAuditRlsWriteCheck.cs:16-17` ("still blocking a request from forging an audit row tagged to a DIFFERENT (non-null) tenant"). |
| **Test Data** | Session tenant `019f960f-6a78-7481-a4f2-903042af86ae`; forged row tenant `019f962d-7d1e-7b24-84ec-1299a38fcfed`; `entity_type='RlsTc018'` |
| **Steps** | 1. `BEGIN;`. 2. Scope the session to `019f960f-6a78-7481-a4f2-903042af86ae`, bypass off (transaction-local). 3. Insert an `audit.field_change` row with `tenant_id='019f962d-7d1e-7b24-84ec-1299a38fcfed'` — capture the SQLSTATE. 4. Insert an `audit.security_event` row with the same foreign tenant — capture the SQLSTATE. 5. Insert the same `audit.field_change` row with `tenant_id='019f960f-6a78-7481-a4f2-903042af86ae'` — the accepted control. 6. `ROLLBACK;`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a at this level; through the application it would be an untyped HTTP 500 per **GAP-TENANT-006**. |
| **Expected DB** | Steps 3 and 4 each raise SQLSTATE **`42501`** (`new row violates row-level security policy for table "field_change"` / `"security_event"`). Step 5 reports `INSERT 0 1`. |
| **Expected Audit** | Only the step-5 row exists, and it is discarded by the rollback. |
| **Expected Notification** | n/a — a refused write produces no notification. |
| **Cleanup** | `ROLLBACK;` — mandatory (append-only triggers). |
| **Evidence** | psql transcript with two `42501` lines and one `INSERT 0 1` |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 5 is the discriminator: relaxation must be provably narrow. Without it, the two refusals could equally indicate that the ledger rejects all writes from a scoped session, which would be a far larger defect. |

#### TC-TENANT-RLS-019 — Null-tenant audit rows are invisible to every tenant read (the `USING`/`WITH CHECK` asymmetry)  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-011 · RSK-TENANT-001 |
| **Level / Type / Technique** | Database (integration) · Functional · Decision Table — read vs write outcome for the same `t=NULL` row |
| **Priority / Severity / Automation** | High · High · Yes (`SecurityEventRlsTests.cs:54` asserts the `security_event` half) |
| **Role / Permission / Tenant** | n/a — database session · n/a · two tenants plus nil plus elevated |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app` |
| **Preconditions** | `RelaxAuditRlsWriteCheck` changed **only** `WITH CHECK`; `USING` remained `tenant_id = GUC OR bypass`, so a `NULL` tenant can never satisfy it. Confirmed in `pg_policies` for all four `audit.*` tables on 2026-08-01. |
| **Test Data** | Reference reading 2026-08-01 for `audit.security_event`: 506 rows total; 222 for `demo-lab`; 81 for `arfa`; **142 with `tenant_id IS NULL`** |
| **Steps** | 1. Under bypass, record `TOTAL`, `DEMO`, `ARFA` and `NULLS` for `audit.security_event`. 2. Scope to `demo-lab`, bypass off: `SELECT count(*) FROM audit.security_event;` and `SELECT count(*) FROM audit.security_event WHERE tenant_id IS NULL;`. 3. Scope to `arfa`, bypass off: same two counts. 4. Set the GUC to the nil UUID, bypass off: same two counts. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | Step 2 returns `DEMO` and **0**. Step 3 returns `ARFA` and **0**. Step 4 returns **0** and **0**. Under bypass, `DEMO + ARFA + (other tenants) + NULLS = TOTAL` and `NULLS > 0` (reference reading 142) — so the invisible population is non-empty and the case is meaningful. |
| **Expected Audit** | No `audit.*` row — reads only. |
| **Expected Notification** | n/a — a read produces no notification. |
| **Cleanup** | Reset both GUCs. |
| **Evidence** | psql transcript with the bypassed baseline and the three scoped readings |
| **Result / Defect** | Not Run · — |
| **Notes** | Assert `NULLS > 0` before trusting the zeros; on a freshly migrated database with no failed logins the null population is empty and every zero is vacuous. |

#### TC-TENANT-RLS-020 — Exactly four tables carry the relaxed write shape, and they are exactly the `audit` schema  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-100 · RSK-TENANT-002 |
| **Level / Type / Technique** | Database (integration) · Structural · Pairwise — schema (`qams` / `audit` / `read` / `saas`) × write shape (strict / relaxed), asserting only one pairing exists |
| **Priority / Severity / Automation** | High · Critical · Yes — no automated test asserts this today |
| **Role / Permission / Tenant** | n/a — catalog query · n/a · n/a — schema-level assertion |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app` |
| **Preconditions** | Only `RelaxAuditRlsWriteCheck` (loop restricted to `schemaname='audit'`, `:26-27`) and `Hardening2_RlsGapClosure` (`audit.security_event`, `:25-27`) create a null-tolerant `WITH CHECK`. Any other table carrying it is an escape. |
| **Test Data** | Expected relaxed set: `audit.audit_trail`, `audit.electronic_signature`, `audit.field_change`, `audit.security_event` |
| **Steps** | 1. `SELECT schemaname, tablename FROM pg_policies WHERE policyname='tenant_isolation' AND with_check LIKE '%tenant_id IS NULL%' ORDER BY 1,2;`. 2. `SELECT count(*)` of the same query. 3. `SELECT count(*) FROM pg_policies WHERE policyname='tenant_isolation' AND schemaname='audit';`. 4. `SELECT count(*) FROM pg_policies WHERE policyname='tenant_isolation' AND schemaname <> 'audit' AND with_check LIKE '%tenant_id IS NULL%';`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | Step 1 lists exactly the four `audit.*` tables above. Step 2 returns **4**. Step 3 returns **4** — every audit table is relaxed, none is left strict. Step 4 returns **0** — no non-audit table is relaxed. Measured 2026-08-01: 4 / 4 / 0. |
| **Expected Audit** | No `audit.*` row — catalog reads only. |
| **Expected Notification** | n/a — a read produces no notification. |
| **Cleanup** | None — no session state changed. |
| **Evidence** | psql transcript with the four-row listing and the three counts |
| **Result / Defect** | Not Run · — |
| **Notes** | Steps 3 and 4 are the two directions of the same claim and both are needed: step 3 catches an audit table that missed the relaxation (which would break the pre-auth write path), step 4 catches a business table that gained it (which would let any session forge a tenant-less business row). |

#### TC-TENANT-RLS-021 — The append-only trigger and RLS are independent controls on `audit.security_event`  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-011, URS-012 · RSK-TENANT-002 |
| **Level / Type / Technique** | Database (integration) · Security (negative) · Decision Table — trigger fires × row visible, both varied |
| **Priority / Severity / Automation** | High · Critical · Yes |
| **Role / Permission / Tenant** | `qams_app` (table owner) · n/a · scoped to `demo-lab`, then elevated |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app`, `\set VERBOSITY verbose`, inside a rolled-back transaction |
| **Preconditions** | `pg_get_triggerdef` measured 2026-08-01: `CREATE TRIGGER security_event_append_only BEFORE DELETE OR UPDATE ON audit.security_event FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation()`, whose body is `RAISE EXCEPTION 'audit ledgers are append-only'`. The same trigger exists on the other three audit tables. |
| **Test Data** | One visible `demo-lab` security-event id `S`, taken under the demo-lab GUC |
| **Steps** | 1. `BEGIN;`. 2. Scope to `019f960f-6a78-7481-a4f2-903042af86ae`, bypass off; `SELECT id FROM audit.security_event LIMIT 1;` — record `S`. 3. `UPDATE audit.security_event SET detail='tampered' WHERE id='S';` — capture the error. 4. `DELETE FROM audit.security_event WHERE id='S';` — capture the error. 5. `SELECT set_config('app.bypass_rls','on',true); DELETE FROM audit.security_event WHERE id='S';` — capture the error. 6. `ROLLBACK;`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — no endpoint writes the ledgers; `ComplianceController` is read-only for these tables (`src/NT.QAMS.WebApi/Controllers/ComplianceController.cs:23-39`). |
| **Expected DB** | Steps 3, 4 and 5 each raise `ERROR: audit ledgers are append-only` (PL/pgSQL `RAISE EXCEPTION`, SQLSTATE `P0001`). Step 5 proves **elevation does not defeat the trigger** — bypassing RLS makes the row visible but still not mutable. Zero rows changed throughout. |
| **Expected Audit** | The targeted row is unchanged; verify with `SELECT detail FROM audit.security_event WHERE id='S';` under bypass before the rollback. |
| **Expected Notification** | n/a — a refused mutation produces no notification. |
| **Cleanup** | `ROLLBACK;` |
| **Evidence** | psql transcript with three `append-only` errors and the unchanged `detail` read-back |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 5 is the point of the case: `app.bypass_rls` is an RLS control only. A reader who assumes elevation is "god mode" will mis-scope every immutability claim in the package. |

#### TC-TENANT-RLS-022 — Failed **platform-admin** login returns `401 AUTH-001`, not HTTP 500, and appends a null-tenant `field_change` row  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-001, URS-008, URS-011 · RSK-TENANT-004 |
| **Level / Type / Technique** | API (real database) · Functional (negative) · Use Case — the pre-authentication failure path end to end |
| **Priority / Severity / Automation** | **Critical** · **Critical** · Yes — and it is **not** automated today (see the Notes) |
| **Role / Permission / Tenant** | Platform administrator (`UserRole.PlatformAdmin`, `user_account.tenant_id IS NULL`) · n/a — `[AllowUnauthenticated]` on `LoginCommand` (`src/NT.QAMS.Application/IdentityAccess/Commands/Login.cs:17`) · **none** — no `tenantIdentifier` in the body, so `app.current_tenant` is the nil UUID |
| **Environment** | API `:5080` Development, backed by the **real** PostgreSQL `ntqams` (`RealDatabaseWebAppFactory`), never the EF InMemory factory |
| **Preconditions** | `platform-admin@localhost` exists, `is_active = true`, `failed_login_attempts < 4`, `locked_until_utc IS NULL`. Path: `hasher.Verify` fails at `Login.cs:82` → `user.RegisterFailedLogin` (`src/NT.QAMS.Domain/IdentityAccess/UserAccount.cs:209-218`) → `db.SaveChangesAsync` at `Login.cs:85` → `FieldChangeInterceptor.Capture` stamps `TenantId` from `TenantOf`, which returns `null` for a platform admin (`FieldChangeInterceptor.cs:128-148`) → the relaxed `WITH CHECK` admits it → then `FailAsync` writes `LOGIN_FAILED` and throws `AUTH-001` (`Login.cs:86,149-154`). |
| **Test Data** | `POST /api/auth/login` body `{"email":"platform-admin@localhost","password":"Definitely-Wrong-Pass-9!"}` — no `tenantIdentifier` field |
| **Steps** | 1. Under bypass in psql, record `B0 = SELECT failed_login_attempts FROM qams.user_account WHERE email='platform-admin@localhost'` and `F0 = SELECT count(*) FROM audit.field_change WHERE tenant_id IS NULL AND entity_type='UserAccount' AND property='FailedLoginAttempts'`. 2. `POST /api/auth/login` with the body above. 3. Record the HTTP status, `Content-Type` and the `code` member of the body. 4. Re-read `B1` and `F1` with the same two queries under bypass. 5. `SELECT count(*) FROM audit.security_event WHERE tenant_id IS NULL AND event_type='LOGIN_FAILED' AND occurred_at_utc > now() - interval '2 minutes';` under bypass. |
| **Expected UI** | The sign-in form shows the generic "Invalid credentials." message; no field indicates which factor failed; the email and password inputs stay enabled. |
| **Expected API** | **`401`** with `Content-Type: application/problem+json`, body `code` = **`AUTH-001`**, `title` = `Invalid credentials.`, and a non-empty `traceId`. **It must not be `500`.** Mapping: `DomainException` whose code starts `AUTH-` → 401 at `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:54-59`. |
| **Expected DB** | `B1 = B0 + 1` in `qams.user_account`. `F1 = F0 + 1` — exactly one new `audit.field_change` row with `tenant_id IS NULL`, `entity_type='UserAccount'`, `action='Modified'`, `property='FailedLoginAttempts'`, `old_value = B0::text`, `new_value = B1::text`, `actor='system'`. |
| **Expected Audit** | Step 5 returns at least 1 — one new `audit.security_event` row, `event_type='LOGIN_FAILED'`, `tenant_id IS NULL`, `actor='platform-admin@localhost'`, `detail='bad-password'` (`Login.cs:86,152`). Both ledger rows are admitted only by the `tenant_id IS NULL` disjunct of the relaxed `WITH CHECK`. |
| **Expected Notification** | n/a — no notification policy subscribes to a failed login. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='platform-admin@localhost';` under bypass. The two ledger rows **cannot** be cleaned up — the append-only trigger forbids `DELETE` — and are retained by design. |
| **Evidence** | HTTP response capture including headers · the four SQL readings · the new `field_change` row rendered in full |
| **Result / Defect** | Not Run · — |
| **Notes** | This is the regression `RelaxAuditRlsWriteCheck` exists to prevent: with the pre-relaxation strict `WITH CHECK`, the null-tenant `field_change` insert raised `42501`, the resulting `DbUpdateException` matched no arm of `DomainExceptionHandler.cs:81`, and the whole login returned **HTTP 500** instead of 401 — an availability failure on the authentication path caused by an isolation control. **No test covers it today:** `ProblemContractTests.Domain_exception_handler_speaks_problem_json` asserts exactly this 401 (`tests/NT.QAMS.WebApi.FunctionalTests/ProblemContractTests.cs:42-52`) but runs on **EF InMemory** (`QamsWebAppFactory.cs:19,74`), where no policy is evaluated; and `RegulatedFlowRealDatabaseTests` exercises only *successful* logins. Recorded as **GAP-TENANT-902**. |

#### TC-TENANT-RLS-023 — Failed **tenant** login is scoped before the write, so both ledger rows are tenant-stamped  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-001, URS-008, URS-106 · RSK-TENANT-004 |
| **Level / Type / Technique** | API (real database) · Functional (negative) · Data Flow — `tenantIdentifier` → `TenantSlug` → `saas.tenant.id` → `ICurrentTenantSetter.Set` → connection GUC → policy → the stamped ledger rows |
| **Priority / Severity / Automation** | Critical · Critical · Yes |
| **Role / Permission / Tenant** | Tenant administrator of `demo-lab` · n/a — anonymous endpoint · `demo-lab` = `019f960f-6a78-7481-a4f2-903042af86ae` |
| **Environment** | API `:5080` Development against real PostgreSQL `ntqams` |
| **Preconditions** | `LoginHandler` calls `tenantScope.Set(tenant.Id)` at `Login.cs:58` — **after** the slug lookup at `:49-50` and **before** any tenant-stamped write, with the comment at `:53-57` naming the `security_event` `WITH CHECK` as the reason. `admin@demo-lab.local` exists, is active and is not locked. |
| **Test Data** | `POST /api/auth/login` body `{"tenantIdentifier":"demo-lab","email":"admin@demo-lab.local","password":"Definitely-Wrong-Pass-9!"}` |
| **Steps** | 1. Under bypass record `B0` = `failed_login_attempts` for `admin@demo-lab.local`, and `E0` = `SELECT count(*) FROM audit.security_event WHERE tenant_id='019f960f-6a78-7481-a4f2-903042af86ae' AND event_type='LOGIN_FAILED'`. 2. Send the request. 3. Record status, `Content-Type` and `code`. 4. Re-read `B1` and `E1`. 5. Scoped as `demo-lab` (bypass **off**), `SELECT count(*) FROM audit.field_change WHERE entity_type='UserAccount' AND property='FailedLoginAttempts' AND occurred_at_utc > now() - interval '2 minutes';`. |
| **Expected UI** | Generic "Invalid credentials." on the `/t/demo-lab` sign-in page; the workspace name stays displayed. |
| **Expected API** | **`401`** `application/problem+json`, `code` = **`AUTH-001`**, `title` = `Invalid credentials.` — identical to the unknown-user and unknown-slug responses, by design. |
| **Expected DB** | `B1 = B0 + 1`. The new `audit.field_change` row carries `tenant_id = '019f960f-6a78-7481-a4f2-903042af86ae'`, **not** `NULL` — the account is `IOptionallyTenantScoped` with a non-null tenant, matched at `FieldChangeInterceptor.cs:143-146`. |
| **Expected Audit** | `E1 = E0 + 1`; the new `audit.security_event` row has `tenant_id = '019f960f-…'`, `event_type='LOGIN_FAILED'`, `actor='admin@demo-lab.local'`, `detail='bad-password'`. Step 5 returns at least 1 **without** bypass — the tenant can see its own failed-login trail, which is the stated purpose of scoping early (`Login.cs:56-57`). |
| **Expected Notification** | n/a — no notification policy subscribes to a failed login. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='admin@demo-lab.local';` under bypass. Ledger rows are permanent by design. |
| **Evidence** | HTTP response capture · the four SQL readings · the scoped (non-bypassed) `field_change` count |
| **Result / Defect** | Not Run · — |
| **Notes** | Contrast with TC-TENANT-RLS-022 deliberately: the same failure code, two different ledger tenancies, admitted by two different disjuncts of the same relaxed `WITH CHECK`. The closest existing coverage is `SecurityEventRlsTests.Login_shaped_write_passes_when_the_request_is_scoped_to_the_events_tenant` (`:140-161`), which simulates the write but never calls the endpoint. |

#### TC-TENANT-RLS-024 — Scoping the tenant **after** the connection is open leaves a stale GUC and the write is refused  [ID]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-004 |
| **Level / Type / Technique** | Component (integration) · Robustness (negative) · Path — the ordering path that `SecurityEventRlsTests.cs:147-151` exists to pin |
| **Priority / Severity / Automation** | High · Critical · Yes (integration, as a sibling of the existing pin) |
| **Role / Permission / Tenant** | n/a — a test `DbContext` driven by `TestContext` · n/a · tenant set late, after `BeginTransactionAsync` |
| **Environment** | `tests/NT.QAMS.IntegrationTests` against real PostgreSQL `ntqams`; `RealPostgresFixture.CreateContext` wires the production interceptor pipeline (`RealPostgresFixture.cs:78-92`) |
| **Preconditions** | `TenantConnectionInterceptor` overrides only `ConnectionOpened` / `ConnectionOpenedAsync` (`:23,29`), so the GUCs are stamped **once per open**. An open transaction pins the connection, so a later `Set()` cannot restamp it. |
| **Test Data** | A fresh tenant GUID `T`; one `SecurityEvent` stamped `TenantId = T` |
| **Steps** | 1. `CreateContext(out var ctx)` **without** calling `ctx.Set`. 2. `await db.Database.BeginTransactionAsync();` — the connection opens now, stamping `app.current_tenant` = nil, `app.bypass_rls` = `off`. 3. `ctx.Set(T);` — too late. 4. `db.Set<SecurityEvent>().Add(new SecurityEvent { Id = Guid.CreateVersion7(), TenantId = T, EventType = "RLSTC024", OccurredAtUtc = DateTimeOffset.UtcNow });` then `await db.SaveChangesAsync();`. 5. Capture the exception type and the inner `PostgresException.SqlState`. 6. `await tx.RollbackAsync();`. |
| **Expected UI** | n/a — component-level case. |
| **Expected API** | n/a — component-level case; through HTTP the same shape would be an untyped 500 (**GAP-TENANT-006**). |
| **Expected DB** | `SaveChangesAsync` throws `DbUpdateException` with an inner `PostgresException` whose `SqlState` is **`42501`**: the connection still carries the nil tenant, so `tenant_id = T` satisfies neither the equality disjunct nor `tenant_id IS NULL`. Zero rows inserted. |
| **Expected Audit** | None — the insert aborts. |
| **Expected Notification** | n/a — a refused write produces no notification. |
| **Cleanup** | `RollbackAsync()` on the transaction; dispose the context. |
| **Evidence** | xUnit failure capture with the exception chain and SqlState · the ordering of the two calls in the test source |
| **Result / Defect** | Not Run · — |
| **Notes** | `[ID]` — this negative is not a requirement; it is the falsification of the positive pin. Run it immediately before `Login_shaped_write_passes_when_the_request_is_scoped_to_the_events_tenant` so the pair reads as one argument: correct ordering passes, reversed ordering yields `42501`. Without this negative the pin cannot distinguish "ordering matters" from "the write always succeeds". |

#### TC-TENANT-RLS-025 — Unknown workspace slug at sign-in appends a null-tenant `LOGIN_FAILED` event under the nil GUC  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-001, URS-008 · RSK-TENANT-004 |
| **Level / Type / Technique** | API (real database) · Functional (negative) · Equivalence Partitioning — the "slug resolves to no tenant" partition, which exits **before** `tenantScope.Set` |
| **Priority / Severity / Automation** | High · High · Yes |
| **Role / Permission / Tenant** | Anonymous · n/a — `[AllowUnauthenticated]` · none resolved; GUC stays the nil UUID for the whole request |
| **Environment** | API `:5080` Development against real PostgreSQL `ntqams` |
| **Preconditions** | `Login.cs:49-51` throws `AUTH-001` via `FailAsync(..., tenantId: null, ..., "unknown-tenant", ...)` **before** the `tenantScope.Set(tenant.Id)` at `:58`. No tenant with identifier `no-such-lab-9x` exists. |
| **Test Data** | `POST /api/auth/login` body `{"tenantIdentifier":"no-such-lab-9x","email":"admin@demo-lab.local","password":"Demo-Admin-Pass-2!"}` — a **valid** password, so only the slug is wrong |
| **Steps** | 1. Under bypass record `N0 = SELECT count(*) FROM audit.security_event WHERE tenant_id IS NULL AND event_type='LOGIN_FAILED' AND detail='unknown-tenant'`. 2. Send the request. 3. Record status, `Content-Type`, `code`. 4. Re-read `N1`. 5. Under bypass, `SELECT tenant_id, actor, detail FROM audit.security_event WHERE event_type='LOGIN_FAILED' ORDER BY occurred_at_utc DESC LIMIT 1;`. 6. Confirm `failed_login_attempts` for `admin@demo-lab.local` is unchanged. |
| **Expected UI** | The `/t/no-such-lab-9x` route shows the generic workspace-not-found state; the sign-in attempt returns the same "Invalid credentials." as a wrong password. |
| **Expected API** | **`401`** `application/problem+json`, `code` = **`AUTH-001`**, `title` = `Invalid credentials.` — indistinguishable from a real account with a wrong password, which is the anti-enumeration contract. |
| **Expected DB** | `qams.user_account.failed_login_attempts` for `admin@demo-lab.local` is **unchanged** — the handler never reached the password check, so no user row was modified and **no** `audit.field_change` row is produced. |
| **Expected Audit** | `N1 = N0 + 1`. Step 5's newest row has `tenant_id IS NULL`, `actor = 'admin@demo-lab.local'` (the raw `command.Email`, un-normalised at this point — `Login.cs:51` passes `command.Email`, not the lower-cased `email` computed later at `:69`), `detail = 'unknown-tenant'`. Admitted by the `tenant_id IS NULL` disjunct under a nil GUC. |
| **Expected Notification** | n/a — no notification policy subscribes to a failed login. |
| **Cleanup** | None possible or required — one immutable ledger row is appended by design; no mutable state changed. |
| **Evidence** | HTTP response capture · the before/after counts · the newest security-event row |
| **Result / Defect** | Not Run · — |
| **Notes** | The `actor` casing detail is worth asserting exactly: an unknown-slug event records the email as typed, whereas every later failure records the trimmed lower-invariant form. A monitoring query grouping on `actor` will otherwise split one attacker across two buckets. |

#### TC-TENANT-RLS-026 — The fifth consecutive failure writes a lockout `field_change` row that the relaxed check must also admit  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-003, URS-008, URS-011 · RSK-TENANT-004 |
| **Level / Type / Technique** | API (real database) · Functional (negative) · BVA — the 5th attempt, `UserAccount.MaxFailedAttempts = 5` (`src/NT.QAMS.Domain/IdentityAccess/UserAccount.cs:29`) |
| **Priority / Severity / Automation** | High · High · Yes |
| **Role / Permission / Tenant** | Platform administrator (null tenant — the harder half of the boundary) · n/a · none; nil GUC throughout |
| **Environment** | API `:5080` Development against real PostgreSQL `ntqams`. **Run last in any session** — the auth rate-limit partition is 10/min. |
| **Preconditions** | `RegisterFailedLogin` increments, and on reaching 5 sets `LockedUntilUtc = now + 30 min` **and resets** `FailedLoginAttempts` to 0 (`UserAccount.cs:209-218`). That single `SaveChanges` therefore produces **two** modified-property `field_change` rows plus the audit-stamp properties, all with `tenant_id IS NULL` for a platform admin. |
| **Test Data** | Five sequential `POST /api/auth/login` bodies `{"email":"platform-admin@localhost","password":"Definitely-Wrong-Pass-9!"}`, spaced ≥ 7 s apart to stay under the 10/min auth partition |
| **Steps** | 1. Reset the account under bypass: `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='platform-admin@localhost';`. 2. Record `F0 = SELECT count(*) FROM audit.field_change WHERE tenant_id IS NULL AND entity_type='UserAccount'`. 3. Send attempts 1–4; record each status. 4. Send attempt 5; record status and `code`. 5. Under bypass read `failed_login_attempts`, `locked_until_utc`. 6. Under bypass, `SELECT property, old_value, new_value FROM audit.field_change WHERE tenant_id IS NULL AND entity_type='UserAccount' AND occurred_at_utc > now() - interval '5 minutes' ORDER BY occurred_at_utc;`. |
| **Expected UI** | Attempts 1–4 show "Invalid credentials."; attempt 5 shows the same message (the lockout message appears on attempt 6, which is out of this case's scope). |
| **Expected API** | Attempts 1–5 all return **`401`** `application/problem+json` with `code` = **`AUTH-001`**. **None** returns 500 — every one of the five appended null-tenant ledger rows through the relaxed `WITH CHECK`. |
| **Expected DB** | After step 5: `failed_login_attempts = 0` and `locked_until_utc` ≈ the attempt-5 timestamp + 30 minutes (`UserAccount.LockoutMinutes = 30`, `:30`). Step 6 lists rows including `property='FailedLoginAttempts'` with `old_value='4'`/`new_value='5'` **and** `property='LockedUntilUtc'` with `old_value` null and a non-null `new_value`, followed by `property='FailedLoginAttempts'` `'5'`→`'0'` if the reset is captured as a separate diff. Every row has `tenant_id IS NULL`. |
| **Expected Audit** | Five new `audit.security_event` rows, `event_type='LOGIN_FAILED'`, `tenant_id IS NULL`, `detail='bad-password'`. A `UserLockedOut` domain event is raised at `UserAccount.cs:216`; assert its outbox row separately — that belongs to the `AUTH` module, not here. |
| **Expected Notification** | n/a for this module — any lockout notification is owned by `NOTIF`; assert only that no notification failure blocks the 401. |
| **Cleanup** | `UPDATE qams.user_account SET failed_login_attempts=0, locked_until_utc=NULL WHERE email='platform-admin@localhost';` under bypass. Ledger rows are permanent. |
| **Evidence** | Five HTTP captures · the account row before and after · the ordered `field_change` listing |
| **Result / Defect** | Not Run · — |
| **Notes** | The exact set and order of `field_change` rows on the 5th attempt was **not** read from a live run in this pass — the property names come from `RegisterFailedLogin`'s source. Record what the run actually produces rather than forcing it to match; if `AuditStampInterceptor` also emits `ModifiedAtUtc`/`ModifiedBy` diffs (measured: 75 and 32 such null-tenant rows exist historically), those are expected too. |

#### TC-TENANT-RLS-027 — `audit.security_event` is RLS-enabled, FORCED and carries `tenant_isolation` — the v1.51.2 gap closure  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-100 · RSK-TENANT-001 |
| **Level / Type / Technique** | Database (integration) · Structural (positive) · Statement coverage — every DDL statement of `Hardening2_RlsGapClosure.Up()` observable in the catalog |
| **Priority / Severity / Automation** | Critical · Critical · Yes |
| **Role / Permission / Tenant** | n/a — catalog query · n/a · n/a — schema-level assertion |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app` |
| **Preconditions** | `src/NT.QAMS.Infrastructure/Persistence/Migrations/20260731181845_Hardening2_RlsGapClosure.cs:17-28` issues `ENABLE`, `FORCE`, `DROP POLICY IF EXISTS` and `CREATE POLICY tenant_isolation … FOR ALL` on `audit.security_event`. The earlier migrations could not reach it because both iterate `pg_policies` (`20260726081443:29`, `20260726103650:26-27`) and it had no policy to iterate. |
| **Test Data** | Expected: `relrowsecurity=t`, `relforcerowsecurity=t`, one policy named `tenant_isolation`, `cmd='ALL'`, `roles={public}` |
| **Steps** | 1. `SELECT c.relrowsecurity, c.relforcerowsecurity FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='audit' AND c.relname='security_event';`. 2. `SELECT policyname, cmd, roles, qual, with_check FROM pg_policies WHERE schemaname='audit' AND tablename='security_event';`. 3. `SELECT count(*) FROM pg_policies WHERE schemaname='audit' AND tablename='security_event';`. 4. `SELECT "MigrationId" FROM public."__EFMigrationsHistory" WHERE "MigrationId" LIKE '%Hardening2_RlsGapClosure';`. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | Step 1 returns `t, t`. Step 2 returns exactly one row: `tenant_isolation`, `ALL`, `{public}`, `qual` = the strict read predicate, `with_check` = the null-tolerant write predicate. Step 3 returns **1** — no second, looser policy exists (multiple permissive policies are OR-ed, so a stray one would silently widen access). Step 4 returns the migration id, proving the closure is applied and not merely written. Measured 2026-08-01: all four as stated. |
| **Expected Audit** | No `audit.*` row — catalog reads only. |
| **Expected Notification** | n/a — a read produces no notification. |
| **Cleanup** | None — no session state changed. |
| **Evidence** | psql transcript with the four results · cross-reference to `docs/validation/13-OQ-Execution-Record-SchemaHardening-v1.51.2.md` OQ-DB-01 |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 3 is the non-obvious assertion. PostgreSQL OR-s permissive policies, so "the right policy exists" is insufficient — "and only that policy exists" is the actual control. |

#### TC-TENANT-RLS-028 — Positive tenant isolation of `audit.security_event` at the database: own / other / nil / elevated  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-001 |
| **Level / Type / Technique** | Database (integration) · Functional (positive) · Equivalence Partitioning — four visibility partitions over one population |
| **Priority / Severity / Automation** | Critical · Critical · Yes (`SecurityEventRlsTests.cs:29-59` asserts the same four partitions on synthetic rows; this case asserts them on the live population) |
| **Role / Permission / Tenant** | n/a — database session · n/a · `demo-lab`, then `arfa`, then nil, then elevated |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app` |
| **Preconditions** | The closure of TC-TENANT-RLS-027 is in place. At least two tenants have security events and at least one null-tenant event exists. |
| **Test Data** | Reference reading 2026-08-01: `TOTAL = 506`; `demo-lab` (`019f960f-6a78-7481-a4f2-903042af86ae`) = 222; `arfa` (`019f962d-7d1e-7b24-84ec-1299a38fcfed`) = 81; `NULLS = 142` |
| **Steps** | 1. Bypass on: record `TOTAL`, `DEMO`, `ARFA`, `NULLS` from `audit.security_event`. 2. Bypass off, GUC = `019f960f-…`: `SELECT count(*) FROM audit.security_event;`. 3. Bypass off, GUC = `019f962d-…`: same count. 4. Bypass off, GUC = nil UUID: same count. 5. Bypass on, GUC = `019f960f-…`: same count. |
| **Expected UI** | n/a — database-level case. |
| **Expected API** | n/a — the HTTP half is TC-TENANT-RLS-029. |
| **Expected DB** | Step 2 = `DEMO` exactly. Step 3 = `ARFA` exactly. Step 4 = **0**. Step 5 = `TOTAL`. Preconditions for meaningfulness, asserted in the same run: `DEMO > 0`, `ARFA > 0`, `NULLS > 0`, `DEMO + ARFA < TOTAL`. |
| **Expected Audit** | No `audit.*` row — reads only. |
| **Expected Notification** | n/a — a read produces no notification. |
| **Cleanup** | Reset both GUCs. |
| **Evidence** | psql transcript with the bypassed baseline and the four scoped counts |
| **Result / Defect** | Not Run · — |
| **Notes** | Author this as a **positive** case only. The v1.51.2 closure means a failing-condition case for `security_event` RLS would be authoring a defect that no longer exists — explicitly forbidden by `00-GROUND-TRUTH-AND-CONVENTIONS.md:74`. |

#### TC-TENANT-RLS-029 — `GetSecurityEventsAsync` filters by tenant, so `GET /api/compliance/security-events` returns only the caller's events  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-011 · RSK-TENANT-001 |
| **Level / Type / Technique** | API (real database) · Functional (positive) · Use Case — a compliance viewer reading the security log |
| **Priority / Severity / Automation** | Critical · Critical · Yes |
| **Role / Permission / Tenant** | Tenant Administrator of `demo-lab` (seeded with `PermissionCatalog.AllKeys`, `src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs:97-100`) · **`compliance.view`** — `[RequirePermission(PermissionCatalog.Compliance, PermissionAction.View)]` at `src/NT.QAMS.WebApi/Controllers/ComplianceController.cs:20`, key built from `PermissionCatalog.Compliance = "compliance"` (`src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:89`) × `PermissionCatalog.Key` (`:198`) · `demo-lab` |
| **Environment** | API `:5080` Development against real PostgreSQL `ntqams` |
| **Preconditions** | Two isolation layers must both be exercised: the application filter `if (tenant.TenantId is { } tid) query = query.Where(s => s.TenantId == tid)` (`src/NT.QAMS.Infrastructure/Compliance/ComplianceLedgerServices.cs:199-203`) and the database policy. `GetSecurityEventsHandler` clamps `take` to `[1,1000]` (`src/NT.QAMS.Application/ComplianceLedger/ComplianceQueries.cs:33`). |
| **Test Data** | Sign in as `admin@demo-lab.local` / `Demo-Admin-Pass-2!` at `demo-lab`; then `GET /api/compliance/security-events?take=1000` |
| **Steps** | 1. `POST /api/auth/login` with the credentials above; capture `accessToken`. 2. `GET /api/compliance/security-events?take=1000` with `Authorization: Bearer <token>`. 3. Record the status and the array length `L`. 4. Assert every element's `tenantId` equals `019f960f-6a78-7481-a4f2-903042af86ae`; assert **no** element has `tenantId` null. 5. Under bypass in psql, `SELECT count(*) FROM audit.security_event WHERE tenant_id='019f960f-6a78-7481-a4f2-903042af86ae';` — call it `DEMO`. 6. `GET /api/compliance/security-events?take=5` and record the length. |
| **Expected UI** | The compliance security-events screen lists only `demo-lab` events, newest first; no row shows another laboratory's identifier. |
| **Expected API** | Step 2: **`200`** with a JSON array. Step 3: `L = min(DEMO, 1000)` (reference reading `DEMO = 222`, so `L = 222`). Step 6: exactly **5** elements, ordered by `occurredAtUtc` descending (`ComplianceLedgerServices.cs:205`). |
| **Expected DB** | No write occurs beyond the login's own `LOGIN_SUCCESS` security event; `DEMO` therefore increases by exactly 1 between step 1 and step 5 if step 5 runs after the login. Account for that off-by-one explicitly rather than treating it as noise. |
| **Expected Audit** | One new `audit.security_event` row from the login itself: `event_type='LOGIN_SUCCESS'`, `tenant_id='019f960f-…'` (`Login.cs:139`). Reading the ledger writes nothing. |
| **Expected Notification** | n/a — a compliance read raises no notification. |
| **Cleanup** | `POST /api/auth/logout` to revoke the refresh family; no data cleanup — the ledger is append-only. |
| **Evidence** | HTTP response body (truncated to first and last elements plus the length) · the psql `DEMO` count · the token's `tenant_id` claim decoded |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 4's "no null `tenantId`" assertion is the one that would have caught the pre-v1.51.2 defect, where this read was the only ledger read without a tenant filter and returned every tenant's events to any compliance viewer (`ComplianceLedgerServices.cs:193-197` records that history in the code). |

#### TC-TENANT-RLS-030 — Pre-authentication (null-tenant) security events never appear in a tenant's compliance view  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-011 · RSK-TENANT-001 |
| **Level / Type / Technique** | API (real database) · Security (positive) · Decision Table — app filter (`TenantId == tid`) × RLS `USING`, both excluding the null-tenant row |
| **Priority / Severity / Automation** | High · High · Yes |
| **Role / Permission / Tenant** | Tenant Administrator of `demo-lab` · `compliance.view` · `demo-lab` |
| **Environment** | API `:5080` Development against real PostgreSQL `ntqams` |
| **Preconditions** | A recent null-tenant `LOGIN_FAILED` event exists — produce one by running TC-TENANT-RLS-022 first, or confirm `SELECT count(*) FROM audit.security_event WHERE tenant_id IS NULL` is greater than zero under bypass (reference reading 142). |
| **Test Data** | The distinct `detail` marker `unknown-tenant` and the platform-admin actor `platform-admin@localhost`, both of which only ever appear on null-tenant rows |
| **Steps** | 1. Under bypass, `SELECT id, event_type, actor, occurred_at_utc FROM audit.security_event WHERE tenant_id IS NULL ORDER BY occurred_at_utc DESC LIMIT 3;` — record the three ids. 2. Sign in as `admin@demo-lab.local` at `demo-lab`. 3. `GET /api/compliance/security-events?take=1000`. 4. Search the response for each of the three ids. 5. Search the response for any element whose `actor` is `platform-admin@localhost`. |
| **Expected UI** | The compliance security-events screen shows no platform-level or pre-authentication entries; there is no filter or toggle that would reveal them. |
| **Expected API** | **`200`**. **None** of the three ids appears in the response. No element has `actor = 'platform-admin@localhost'`. No element has a null `tenantId`. |
| **Expected DB** | The three rows still exist under bypass after the call — they are hidden, not deleted. Re-run step 1 afterwards and confirm the same three ids. |
| **Expected Audit** | Only the login's own `LOGIN_SUCCESS` row is added. |
| **Expected Notification** | n/a — a compliance read raises no notification. |
| **Cleanup** | `POST /api/auth/logout`. No data cleanup — the ledger is append-only. |
| **Evidence** | The three null-tenant ids from psql · the full API response searched for each · the post-call re-read proving persistence |
| **Result / Defect** | Not Run · — |
| **Notes** | Both layers independently exclude these rows: the app filter compares `s.TenantId == tid` (never true for `NULL`) and `USING` compares `tenant_id = GUC` (also never true for `NULL`). Prove the database layer alone with TC-TENANT-RLS-019; this case proves the composed HTTP outcome. |

#### TC-TENANT-RLS-031 — A platform administrator sees an **empty** security-event list, because the app filter is skipped but RLS is not  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008 · RSK-TENANT-003 |
| **Level / Type / Technique** | API (real database) · Functional · Multiple-Condition — `IsPlatformAdmin` (permission gate) × `tenant.TenantId is null` (app filter) × nil GUC (RLS), all three in one request |
| **Priority / Severity / Automation** | Medium · Medium · Yes |
| **Role / Permission / Tenant** | Platform administrator · `compliance.view` is **satisfied unconditionally** — `Has(key) => IsPlatformAdmin \|\| Permissions.Contains(key)` (`src/NT.QAMS.Infrastructure/Authorization/PrivilegeResolution.cs:39`), and `ActiveSessionMiddleware` calls `SetPlatformAdmin()` for `UserRole.PlatformAdmin` (`src/NT.QAMS.WebApi/Middleware/RequestIdentity.cs:114-117`) · **none** — the platform JWT carries no `tenant_id` claim, so `TenantResolutionMiddleware` never calls `Set` (`RequestIdentity.cs:53-64`) |
| **Environment** | API `:5080` Development against real PostgreSQL `ntqams` |
| **Preconditions** | `GetSecurityEventsAsync` applies its filter only when `tenant.TenantId` has a value (`ComplianceLedgerServices.cs:200-203`); with no tenant the filter is skipped and the query is unrestricted at the application layer. The connection GUC is nevertheless the nil UUID (`TenantConnectionInterceptor.cs:55`). |
| **Test Data** | Sign in as `platform-admin@localhost` / `Dev-Only-Platform-Pass-1!` (no `tenantIdentifier`); then `GET /api/compliance/security-events?take=1000` |
| **Steps** | 1. `POST /api/auth/login` with the platform credentials; capture `accessToken`. 2. Decode the token and confirm there is **no** `tenant_id` claim. 3. `GET /api/compliance/security-events?take=1000` with the bearer token. 4. Record the status and array length. 5. Under bypass in psql, confirm `SELECT count(*) FROM audit.security_event` is greater than zero (reference reading 506). |
| **Expected UI** | If the SPA exposes this screen to a platform administrator it renders the empty state, not an error and not another laboratory's data. |
| **Expected API** | **`200`** with an **empty JSON array** (length 0). **Not** 403 — the permission gate passes for a platform admin. **Not** a full listing — RLS refuses every row under the nil GUC. |
| **Expected DB** | Step 5 shows the table is non-empty, so the empty response is caused by isolation, not by absence of data. |
| **Expected Audit** | One `LOGIN_SUCCESS` `audit.security_event` row with `tenant_id IS NULL` from the platform login itself (`Login.cs:139`, `tenantId` null on this path). |
| **Expected Notification** | n/a — a compliance read raises no notification. |
| **Cleanup** | `POST /api/auth/logout`. |
| **Evidence** | Decoded JWT claim set · the empty array response · the non-zero bypassed count |
| **Result / Defect** | Not Run · — |
| **Notes** | This resolves the `[RNV]` footnote † in the front matter's §4.5 for this endpoint: a platform administrator is **not** refused by `[RequirePermission]`, because `PrivilegeResolution.cs:39` short-circuits on `IsPlatformAdmin`. The refusal, when it comes, is the tenant fence rather than the permission gate — an important distinction for the authorization matrix. It also means the platform operator has **no** route to the pre-authentication events they are the intended audience for; raised as **GAP-TENANT-905**. |

#### TC-TENANT-RLS-032 — B9: `qams.user_account` and `qams.outbox_event` are the only two tenant-bearing tables outside RLS  [IV]

| Field | Value |
|---|---|
| **Module / Requirement / Risk** | TENANT · URS-008, URS-100 · RSK-TENANT-006 |
| **Level / Type / Technique** | Database (integration) · Structural · Decision Table — "carries `tenant_id`" × "has `tenant_isolation`", asserting the exception set is closed at exactly two |
| **Priority / Severity / Automation** | Critical · Critical · Yes — the exception set must be a build-time assertion, not prose |
| **Role / Permission / Tenant** | n/a — catalog query, plus one scoped read · n/a · `demo-lab` for the scoped read |
| **Environment** | PostgreSQL 17 `ntqams` as `qams_app` |
| **Preconditions** | Deviation **B9** is permanently accepted (`SCHEMA-HARDENING-REPORT.md` §8): a null-tolerant policy on `user_account` would isolate nothing, and a tenant predicate on `outbox_event` would stop cross-tenant delivery. The compensating control for `user_account` is source-level (`tests/NT.QAMS.Architecture.Tests/UserAccountTenantBoundTests.cs:49-71`), not database-level. |
| **Test Data** | Reference readings 2026-08-01 under a `demo-lab`-scoped session with bypass **off**: `qams.user_account` 36 rows visible, of which 5 belong to `demo-lab` and 2 have `tenant_id IS NULL`; `qams.outbox_event` 440 rows visible, of which 100 have `tenant_id IS NULL` |
| **Steps** | 1. Run the closure query: `SELECT n.nspname, c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind='r' AND n.nspname IN ('qams','audit','read','saas') AND EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid=c.oid AND a.attname='tenant_id') AND NOT EXISTS (SELECT 1 FROM pg_policies p WHERE p.schemaname=n.nspname AND p.tablename=c.relname) ORDER BY 1,2;`. 2. `SELECT c.relname, c.relrowsecurity, c.relforcerowsecurity, a.attnotnull FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_attribute a ON a.attrelid=c.oid AND a.attname='tenant_id' WHERE n.nspname='qams' AND c.relname IN ('user_account','outbox_event');`. 3. Scope to `019f960f-6a78-7481-a4f2-903042af86ae`, bypass off; `SELECT count(*) FROM qams.user_account;` and `SELECT count(*) FROM qams.user_account WHERE tenant_id='019f960f-6a78-7481-a4f2-903042af86ae';`. 4. Same session: `SELECT count(*) FROM qams.outbox_event;`. |
| **Expected UI** | n/a — database-level case. The compensating control at the HTTP layer is `RegulatedFlowRealDatabaseTests.A_tenant_sees_only_its_own_users_over_http` and is out of scope here. |
| **Expected API** | n/a — database-level case. |
| **Expected DB** | Step 1 returns **exactly two rows**: `qams.outbox_event` and `qams.user_account`. Step 2 returns `f, f, f` for both — RLS off, FORCE off, `tenant_id` nullable. Step 3's first count is **strictly greater** than its second (reference reading 36 > 5), proving the absence of the fence is real and observable. Step 4 returns a non-zero count spanning multiple tenants (reference reading 440). |
| **Expected Audit** | No `audit.*` row — reads only. |
| **Expected Notification** | n/a — a read produces no notification. |
| **Cleanup** | Reset both GUCs. |
| **Evidence** | psql transcript with the two-row closure result, the flag matrix, and the 36-vs-5 comparison |
| **Result / Defect** | Not Run · — |
| **Notes** | Step 3's inequality is what makes this a test rather than a restatement of the decision: it demonstrates the accepted residual risk concretely — *"A future query that lists `user_account` without a bound would leak across tenants and nothing in the database would stop it"* (`SCHEMA-HARDENING-REPORT.md` §8). Step 1 must be re-run on every schema change; a third row appearing is a new, unaccepted deviation, not an extension of B9. |

## Batch coverage note

**Covered.** Thirty-two cases, `TC-TENANT-RLS-001` … `TC-TENANT-RLS-032`, all `Not Run`. Shape-S read behaviour on `qams.nonconformance` across all four GUC partitions plus MC/DC over the two-condition `USING` predicate (001, 004–006, 008, 010); FORCE row-level security proven to filter the **table owner** in dev, where `qams_app` owns every application table, with a falsifying `NO FORCE` control confined to a throwaway database (002, 003); the bypass token proven to be an exact case-sensitive `'on'` match against eight rejected alternatives (007); strict `qams.*` `WITH CHECK` accepting an own-tenant insert and refusing a foreign `tenant_id` with SQLSTATE `42501`, including the `UPDATE`-moves-tenant path that has no existing automated coverage and the silent zero-row `DELETE` (011–013, 015); the nullability-drives-shape invariant on `qams.ref_counter` versus `audit.security_event` (014); the relaxed `audit.*` `WITH CHECK` in all three of its dispositions — null accepted under a scoped GUC, null accepted under the nil GUC, foreign non-null still refused — plus the `USING`/`WITH CHECK` asymmetry that makes a null-tenant row platform-only, the closure assertion that exactly four tables and exactly the `audit` schema carry the relaxed shape, and the independence of the append-only trigger from RLS under elevation (016–021); the login regression end to end — platform-admin failure returning `401 AUTH-001` with a null-tenant `field_change` row, tenant failure returning the same code with tenant-stamped rows, the reversed-ordering negative that yields `42501`, the unknown-slug partition, and the fifth-attempt lockout boundary (022–026); positive `audit.security_event` isolation at both layers — catalog closure with the single-policy assertion, the four live visibility partitions, the tenant-filtered `GetSecurityEventsAsync` read over `GET /api/compliance/security-events`, the invisibility of pre-authentication rows, and the platform administrator's empty list (027–031); and the B9 closure assertion with its observable 36-vs-5 leak demonstration (032). Every predicate, flag, count and SQLSTATE quoted above was measured read-only against dev `ntqams` on 2026-08-01, or read in the cited source line; nothing was inferred from the front matter without re-measurement.

**In slice but not covered, with the reason.** *(a)* Shape-S behaviour on the other 87 strict tables is not enumerated case by case — `qams.nonconformance`, `qams.ref_counter` and `read.kpi_snapshot` are the representatives, and the per-table sweep is batch B's `TC-TENANT-INT` reservation. *(b)* `audit.audit_trail` and `audit.electronic_signature` are asserted structurally (020) but have **no** behavioural isolation case here; their content semantics belong to the `LEDGER` module and a behavioural case needs signature/chain fixtures this batch does not own. *(c)* No case asserts a typed HTTP status for an RLS refusal, because none exists — every such refusal is an untyped 500 (**GAP-TENANT-006**), and asserting 500 would pin a defect as expected behaviour; cases 012, 018 and 024 therefore stop at the database boundary and say so. *(d)* Production-grade least privilege is untestable here: dev runs as the owner, `deploy/harden-runtime-role.sql` cannot execute (**GAP-TENANT-008**), and case 002 is written so that it holds in both postures rather than pretending to qualify the production one. *(e)* Refresh-token behaviour for a suspended tenant remains `[RNV]` and is untouched. *(f)* Connection-pool GUC reuse across two different tenants' requests is not authored here — it needs a concurrency harness and is chartered as `TC-TENANT-EXPL-002`.

**New gaps found in this pass.**

- **GAP-TENANT-901 — ID-block reservation conflict between the front matter and this batch.** `12-module-tenancy-rls.md` reserves `TC-TENANT-RLS-001…060` to **batch B**; this batch was commissioned to author `TC-TENANT-RLS-001…` as **batch C**. Both cannot be true, and a collision corrupts the traceability matrix. *Acceptance criteria:* the reservation table names exactly one owning batch per id range; if batch B also authors `TC-TENANT-RLS` cases they start at `061`; the traceability matrix build fails on any duplicate `TC-` id across `docs/testing/`. *Severity:* High (package integrity). *Owner:* Validation Lead.
- **GAP-TENANT-902 — the login regression has no real-database regression test.** The only assertion of `401 AUTH-001` on a failed login is `tests/NT.QAMS.WebApi.FunctionalTests/ProblemContractTests.cs:42-52`, which runs on **EF InMemory** (`QamsWebAppFactory.cs:19,74`) where no RLS policy is evaluated; `RegulatedFlowRealDatabaseTests` covers only *successful* logins. The 500-versus-401 failure mode that `RelaxAuditRlsWriteCheck` was written to fix is therefore invisible to CI, and a `Down()` of that migration would break authentication with a green suite. *Acceptance criteria:* a `SkippableFact` on `RealDatabaseWebAppFactory` posts a wrong password for a **null-tenant** account and asserts 401 with `code = AUTH-001` **and** the presence of the new null-tenant `audit.field_change` row; a sibling asserts the tenant-scoped variant; both run whenever `QMS_ITEST_POSTGRES` is set. *Severity:* High. *Owner:* Backend Lead.
- **GAP-TENANT-903 — the structural RLS-parity sweep excludes the entire `audit` schema.** `OwnedChildTenancyTests.Every_owned_child_table_carries_tenant_id_and_full_rls` (`tests/NT.QAMS.IntegrationTests/OwnedChildTenancyTests.cs:136-152`) restricts itself to `n.nspname IN ('qams','read')` **and** `col.is_nullable = 'NO'`. Both filters exclude every `audit.*` ledger — precisely the shape of table that produced the original `audit.security_event` hole. Only `security_event` has behavioural coverage; `audit_trail`, `electronic_signature` and `field_change` have neither structural nor behavioural RLS coverage. *Acceptance criteria:* a second structural assertion covers schemas `qams, audit, read, saas` for tables carrying a `tenant_id` column of any nullability, listing exactly `qams.user_account` and `qams.outbox_event` as allowed exceptions and failing on any third; and a behavioural isolation test exists for at least `audit.field_change`. *Severity:* High. *Owner:* Backend Lead + Validation Lead.
- **GAP-TENANT-904 — a malformed tenant GUC fails loud, and elevation does not rescue it.** Measured: `app.current_tenant = 'not-a-uuid'` raises SQLSTATE `22P02` on every query against an RLS table, **including** with `app.bypass_rls = 'on'`. The front matter's §4.2 truth table covers nil, empty and unset but has no row for a malformed non-empty value, and no requirement states whether the fence should fail closed (0 rows) or fail loud (error) in that case. Unreachable from the application today because `TenantConnectionInterceptor.cs:55` binds a `Guid` or the nil UUID — but it is one non-EF client away. *Acceptance criteria:* §4.2 gains the row; the intended behaviour is stated; if fail-closed is intended, the predicate is rewritten so a non-castable value yields `NULL` rather than an error, with a test for each of the three malformed partitions (non-UUID text, a UUID with a trailing space, a numeric string). *Severity:* Medium. *Owner:* Security Architect.
- **GAP-TENANT-905 — the platform operator has no route to the pre-authentication security events.** Null-tenant `audit.security_event` rows (measured 142 of 506 on 2026-08-01, including every unknown-slug and platform-admin login failure) are invisible to every tenant by design, and also invisible to a platform administrator, because `GET /api/compliance/security-events` runs under the nil tenant GUC and RLS refuses the rows (case 031). They are readable only by a `psql` session that sets `app.bypass_rls='on'` by hand. Failed platform-admin authentication attempts therefore have no supervised review path — a Part 11 §11.300(d) "detect and report attempts at unauthorized use" concern. *Acceptance criteria:* a platform-gated read exists for null-tenant security events, running under an explicit elevation with its own audit entry; or the operations documentation states that these rows are reviewed out of band and names the procedure and its evidence. *Severity:* Medium. *Owner:* Product Owner + Security Architect.

**Two measured drifts against documents, recorded here rather than silently reconciled.** *(i)* The policy inventory is **92 `tenant_isolation` policies and 92 FORCE-RLS tables across 99 tables**, not the 90/90/97 stated in `00-GROUND-TRUTH-AND-CONVENTIONS.md:25,70` and the front matter's §4.1. The two additions are `qams.quality_health_profile` and `qams.quality_health_weight`, created by migration `20260801131521_QualityHealthProfile` on the inspection date; both measured `rls=t force=t` with the correct strict shape, so the parity is intact and the count is simply stale. No test pins the number, so the drift is invisible to CI. *(ii)* `00-GROUND-TRUTH-AND-CONVENTIONS.md:83` states 19,296 legacy null-tenant `field_change` rows; the measured population on 2026-08-01 is **27,214 of 57,731**. The 19,296 figure is specifically the `RolePermission` rows written at 2026-07-31 10:00; a further 2,680 null-stamped `RolePermission` rows were written at 2026-08-01 02:00, and the most recent provisioning batch at 2026-08-01 16:00 wrote 2,690 rows with **zero** nulls — so the `FieldChangeInterceptor.TenantOf` shadow fix (URS-106) is working and the residual is genuinely historical. Both drifts are documentation-accuracy items, not defects, and neither changes any expected result above.
