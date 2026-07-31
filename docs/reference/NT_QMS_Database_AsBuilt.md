# NT.QMS — Database As-Built Reference

| Field | Value |
| ----- | ----- |
| Document ID | DB-ASBUILT-NTQMS-001 |
| Status | **As-built.** Every figure below was read from `pg_catalog` / `information_schema`, not from a design document |
| Captured | 2026-08-01, database `ntqams` (PostgreSQL 17) at migration `20260731210953_Hardening5_CompositeKeys` (55 applied) |
| Companion | `NT_QAMS_Database_Architecture.md` is the **design/target-state** document ("No DDL, no EF classes, no migrations"). This file records what was actually built, and supersedes it wherever the two differ |
| Re-verify | Every claim here has a query in §7. Re-run them rather than trusting this snapshot |

---

## 1. Measured shape

| Metric | Value |
| ------ | ----- |
| Tables | **96** — `qams` 89 · `audit` 4 · `saas` 2 · `read` 1 (+ `public.__EFMigrationsHistory`) |
| Tables with FORCE row-level security | **90** |
| `tenant_isolation` policies | **90** |
| Secondary indexes | 265 |
| Foreign keys | **36** — 29 tenant-composite, 5 to `saas.tenant`, 2 single-column to `user_account` |
| CHECK constraints | **85** — 67 enum domains, 5 hash formats, 13 pre-existing range/order rules |
| Tenant-first composite primary keys | **88** |
| Applied migrations | 55 |

Type posture after the hardening program: 172 `text` columns (free text, bounded by command
validators), 1 `jsonb`, 3 `inet`. Bounded codes, refs, enum strings and hashes keep explicit
`varchar(n)` under 1000.

## 2. Multi-tenancy — three layers, all verifiable

1. **EF global query filter** on every `ITenantScoped` entity, applied by convention in
   `AppDbContext.OnModelCreating` so a module cannot forget it. `IAllocatable` entities get the
   per-user branch/department working scope composed into the same filter.
2. **PostgreSQL FORCE row-level security** — 90 tables, each with a `tenant_isolation` policy:
   `USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid OR
   current_setting('app.bypass_rls', true) = 'on')`. Session GUCs are stamped on every
   connection open by `TenantConnectionInterceptor`; an unresolved tenant is the nil UUID, which
   matches no row (**fail-closed**).
3. **Tenant-composite foreign keys** — a child row whose tenant differs from its parent's is
   rejected by the engine, not merely detected. This replaced 28 single-column CASCADE FKs that
   protected deletion integrity but never read isolation.

The three audit ledgers with a tenant additionally allow a **null-tenant write** (`WITH CHECK
(tenant_id IS NULL OR …)`): pre-authentication events legitimately have no tenant. They are
platform-level and are not visible in any tenant's view.

## 3. Conventions actually in force

| Area | Rule |
| ---- | ---- |
| Primary keys | `(tenant_id, id)`, tenant-first, on every table whose tenant is NOT NULL. UUIDv7, `ValueGeneratedNever` — ids are minted in the domain |
| No `UNIQUE (id)` | A unique index omitting the partition key is illegal on a partitioned table; adding one would defeat partition-readiness |
| Naming | `snake_case`; `pk_ / fk_ / ix_ / ux_ / ck_`. Identifiers **must** stay ≤ 62 chars (PostgreSQL truncates at 63; EF truncates client-side at 62, silently and mid-word). Abbreviation map in `CLAUDE.md` §5 |
| Enums | Stored as strings, and every one is fenced by a `ck_<table>_<column>_domain` CHECK derived from the C# enum |
| Hashes | `varchar(64)` + `CHECK (col ~ '^[0-9a-f]{64}$')`; `refresh_session.token_hash` is uppercase (`Convert.ToHexString`) and uses `[0-9A-F]` |
| Free text | ≥ 1000 chars → `text`; the bound lives in the FluentValidation command validator |
| Time / money | `DateTimeOffset` from the injected `IClock`; `decimal` |

## 4. Append-only and immutability

17 triggers, all enabled: **4 append-only** guards on the audit ledgers
(`audit.reject_mutation()` on UPDATE/DELETE) and **13 `frozen_immutability`** guards on signed
analytical studies. They survived every hardening migration — DDL does not disturb them, and
each phase re-verified that they still fire.

`audit.audit_trail` keeps its hash chain intact (`prev_hash`, `entry_hash`, `sequence`,
`payload`, `event_type`, `occurred_at_utc` unchanged in type). Chain verification returns
`ok: true` after the Phase-5 re-key.

## 5. Honest register — known deviations

| Item | Status |
| ---- | ------ |
| `qams.user_account` — no RLS | **Permanently accepted** by the System Owner, 2026-08-01. Its tenant is nullable (platform administrators have none), so a `tenant_id`-based policy cannot express its rule — and applying one would break authentication, which necessarily runs before a tenant is resolved. Compensating controls verified across all 27 access sites: every read is either explicitly tenant-filtered, keyed by the authenticated actor's own id from the JWT, or keyed by an id set already derived from a tenant-filtered query. Full record and residual risk: `SCHEMA-HARDENING-REPORT.md` §8 |
| `qams.outbox_event` — no RLS | **Permanently accepted** by the System Owner, 2026-08-01. Nullable tenant; only three code paths touch it and the processor runs deliberately cross-tenant under `Elevate()`. No tenant-facing read surface. Same record, §8 |
| Historical `audit.field_change` / `security_event` rows with an empty tenant | **Open — QA disposition.** Written before defect RP-D1 was fixed (v1.51.1). The ledger is append-only and is not restated; the rows remain queryable platform-side |
| `audit.security_event.ip_address` never populated | **Open.** The column is `inet` and correct, but no writer sets it — `ISecurityEventLog.WriteAsync` takes no IP parameter |
| Independent penetration test | **Open external activity** |

## 6. Partition-readiness (prepared, not enabled)

Phase 5 made the schema partition-ready; no table is partitioned yet. When volume justifies it:

| Target | Strategy | Why |
| ------ | -------- | --- |
| `audit.audit_trail` | HASH on `tenant_id` | Time-range would break `UNIQUE (tenant_id, sequence)` and weaken tamper-evidence |
| `audit.field_change`, `audit.security_event` | RANGE, monthly | Volume growth is time-shaped |
| `qams.outbox_event` | RANGE | Purge becomes `DROP PARTITION` |

Note: the three `audit.*` targets and `outbox_event` are among the tables that keep single-column
PKs (nullable tenant), so partitioning them needs its own key decision first.

## 7. Verification queries

```sql
-- RLS parity: must return 0 rows
SELECT n.nspname||'.'||c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
WHERE c.relkind='r' AND n.nspname IN ('qams','audit','read')
  AND EXISTS (SELECT 1 FROM information_schema.columns col
              WHERE col.table_schema=n.nspname AND col.table_name=c.relname
                AND col.column_name='tenant_id' AND col.is_nullable='NO')
  AND (NOT c.relrowsecurity OR NOT c.relforcerowsecurity
       OR NOT EXISTS (SELECT 1 FROM pg_policies p WHERE p.schemaname=n.nspname
                        AND p.tablename=c.relname AND p.policyname='tenant_isolation'));

-- Every tenant-scoped table is tenant-first keyed: must return 0
SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
JOIN pg_constraint pk ON pk.conrelid=c.oid AND pk.contype='p'
WHERE c.relkind='r' AND n.nspname IN ('qams','audit','read') AND array_length(pk.conkey,1)=1
  AND EXISTS (SELECT 1 FROM information_schema.columns col WHERE col.table_schema=n.nspname
                AND col.table_name=c.relname AND col.column_name='tenant_id' AND col.is_nullable='NO');

-- No identifier may exceed 62 characters: must return 0
SELECT count(*) FROM pg_class WHERE length(relname) > 62;
SELECT count(*) FROM pg_constraint WHERE length(conname) > 62;

-- Guard triggers present and enabled: expect 17, all 'O'
SELECT count(*), string_agg(DISTINCT tgenabled::text, ',') FROM pg_trigger
WHERE NOT tgisinternal AND (tgname LIKE '%append_only' OR tgname='frozen_immutability');
```

Application-level proof lives in `tests/NT.QAMS.IntegrationTests`
(`RlsTenantIsolationTests`, `OwnedChildTenancyTests`, `SecurityEventRlsTests`,
`CheckConstraintTests`, `SignedRecordImmutabilityTests`) — all run against real PostgreSQL.

## 8. Deployment

`deploy/migrations.sql` is the idempotent script for all **55** migrations
(`InitialFoundation` … `Hardening5_CompositeKeys`), regenerated 2026-08-01. Run it as
`qams_owner`, then `deploy/harden-runtime-role.sql` (which grants `audit.*` SELECT+INSERT only
and explicitly revokes DELETE); the application connects as `qams_app`.
`DatabaseRoleGuard.EnsureLeastPrivilegeAsync` refuses to boot in Production against an
over-privileged role.
