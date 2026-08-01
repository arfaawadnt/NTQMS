---
name: ntqms-database
description: >-
  NT.QMS database law — PostgreSQL 17 schema conventions and the exact EF Core migration
  procedure. Tenant-first composite primary keys, FORCE row-level security parity, composite
  tenant foreign keys, CHECK value domains derived from C# enums, varchar/text sizing,
  identifier-length limits, and the two migration traps that have already caused production-
  breaking defects. Invoke before writing ANY migration, EF entity configuration, or schema
  change in this repo, and before adding a table, column, index, constraint or enum.
---

# NT.QMS — Database & Migration Law

The schema was hardened in six migrations (`Hardening1`…`Hardening6`, v1.51.2). These rules are
what that hardening established. Breaking one does not merely offend a convention — several of
them are enforced by tests that fail the build, and two of them exist because their absence
already broke sign-in and tenant provisioning in this codebase.

As-built reference (measured, not designed): `docs/reference/NT_QMS_Database_AsBuilt.md`.
Programme record incl. every defect: `SCHEMA-HARDENING-REPORT.md`.

## 0. Non-negotiables

| Rule | Why it is absolute |
| ---- | ------------------ |
| Every tenant table has FORCE RLS **and** a `tenant_isolation` policy | An architecture test sweeps for violations and fails the build |
| Primary keys are **tenant-first composite** `(tenant_id, id)` | Partition-readiness: PostgreSQL requires the partition key in every PK and unique index, and cannot convert an existing table |
| Never add `UNIQUE (id)` | A unique index omitting the partition key is illegal on a partitioned table — it would defeat the above |
| Cross-aggregate FKs are **tenant-composite** | Makes a child under another tenant's parent structurally impossible, which a single-column FK never did |
| Identifiers stay ≤ **62** characters | PostgreSQL truncates at 63; EF truncates client-side at 62, silently and mid-word. The longest identifier today is exactly 62 — there is no headroom |
| Every schema change is an **EF migration** | No manual DDL, ever. Use `migrationBuilder.Sql(...)` for what EF cannot model |

## 1. Adding a tenant-scoped table

**Domain**: `sealed class X : AggregateRoot, ITenantScoped` with `public Guid TenantId { get; set; }`.

**EF configuration** — note the composite key, which is *not* EF's default:
```csharp
builder.ToTable("x", "qams");
builder.HasKey(x => new { x.TenantId, x.Id });          // tenant-FIRST
builder.Property(x => x.Code).HasMaxLength(50);          // bounded code: keep varchar
builder.Property(x => x.Notes);                          // free text >=1000: leave unbounded -> text
builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
builder.Ignore(x => x.DomainEvents);
```

**Owned children** (their own table) carry a **shadow** tenant column and a composite ownership FK:
```csharp
builder.OwnsMany(x => x.Lines, line =>
{
    line.ToTable("x_line", "qams");
    line.Property<Guid>("TenantId");                     // shadow; TenantStampInterceptor fills it
    line.WithOwner().HasForeignKey("TenantId", "x_id");  // composite -> owner's (TenantId, Id)
    line.HasKey("TenantId", "Id");
});
```

**Migration** — after `CreateTable`, RLS is **mandatory and EF will not generate it**:
```sql
ALTER TABLE qams.x ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.x FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.x;
CREATE POLICY tenant_isolation ON qams.x
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
```
`audit.*` ledgers differ: their `WITH CHECK` also allows `tenant_id IS NULL`, because
pre-authentication events legitimately have no tenant.

**Verify by introspection, never from the migration source:**
```sql
SELECT relrowsecurity, relforcerowsecurity FROM pg_class WHERE relname = 'x';   -- t, t
```

## 2. The two migration traps (both have already caused defects)

### Trap 1 — FORCE RLS blocks the migration's own work
FORCE row-level security applies to the migration session **and to PostgreSQL's
referential-integrity checks**. Without a bypass:
- a backfill `UPDATE child SET … FROM parent` silently updates **zero rows** (parents invisible), and
- a later `ADD CONSTRAINT … FOREIGN KEY` fails, because the RI check cannot see the parent either.

Put this at the top of **both** `Up()` and `Down()` in any migration that backfills from, or adds
an FK to, a FORCE-RLS table. Transaction-local, so nothing leaks:
```csharp
migrationBuilder.Sql("SELECT set_config('app.bypass_rls', 'on', true);");
```

### Trap 2 — EF's model snapshot does not learn raw-SQL DDL
If a migration renames or replaces a constraint via `migrationBuilder.Sql(...)`, EF still believes
the old name. The **next** scaffolded migration then emits a drop for a constraint that does not
exist. Reconcile generated names against `pg_constraint` before applying, and remember `Down()`
must drop what `Up()` **created**, not what it replaced.

## 3. Value domains, sizing, naming

**Enums** are stored as strings and fenced by a CHECK derived from the C# enum:
```sql
ALTER TABLE qams.x ADD CONSTRAINT ck_x_status_domain
  CHECK (status IN ('Draft','Approved','Closed')) NOT VALID;
ALTER TABLE qams.x VALIDATE CONSTRAINT ck_x_status_domain;
```
`NOT VALID` then `VALIDATE` avoids a long exclusive lock. **Never guess the value set** — derive it
from the enum. A CHECK with a wrong set is worse than no CHECK. Generate the pre-flight scan and
the constraint from the *same* source so they cannot disagree
(`scripts/preflight-enum-domains.sql` is the worked example).

**Open sets are deliberately unconstrained**: `security_event.event_type` (telemetry labels),
`audit_trail.event_type` and `outbox_event.event_type` (CLR type names), `work_task.assignee_role`.

**Sizing**: free text ≥ 1000 chars is `text`, and the bound moves to the command validator
(`MaximumLength`). Bounded codes, refs, enum strings and hashes keep `varchar(n)` under 1000.
**Never drop a varchar bound without adding the validator rule** — that is how unbounded input gets in.

**Hashes**: `varchar(64)` + `CHECK (col ~ '^[0-9a-f]{64}$')`. Note `refresh_session.token_hash` is
**uppercase** (`Convert.ToHexString`) and uses `[0-9A-F]` — check the writer before assuming case.

**Naming**: `snake_case`; `pk_ / fk_ / ix_ / ux_ / ck_`. Pin any index whose EF-default name would
exceed 62 chars with `HasDatabaseName()`, using the abbreviation map in `CLAUDE.md` §5
(`document_acknowledgement→doc_ack`, `notification_dispatch→notif_dispatch`, …).

## 4. What must never change

- `audit.audit_trail` hash-chain columns — `prev_hash`, `entry_hash`, `sequence`, `payload`,
  `event_type`, `occurred_at_utc` — keep their exact types. Altering them invalidates chain
  verification. (The PK *was* changed to tenant-first for partition-readiness; the chain was
  re-verified intact afterwards.)
- The **4 append-only triggers** on the audit ledgers and the **13 `frozen_immutability`**
  triggers. If a migration alters one of those tables, re-verify the trigger still fires by
  probing an UPDATE, not by reading the migration.
- The four **nullable-tenant** tables keep single-column PKs and cannot be tenant-keyed:
  `user_account`, `outbox_event`, `audit.security_event`, `audit.field_change`. A key column
  cannot be null.

## 5. Accepted permanent deviation

`qams.user_account` and `qams.outbox_event` have **no RLS** — their tenant is nullable, so the
policy predicate (false for NULL) would hide every platform administrator and break
authentication, which runs before a tenant exists. Accepted by the System Owner 2026-08-01.
The compensating control is enforced at build time: `UserAccountTenantBoundTests` fails if a new
`db.Users` query lacks a tenant predicate, an id bound, or an explicit `tenant-unbounded:`
justification comment. **Do not "fix" this by adding a policy** — read
`SCHEMA-HARDENING-REPORT.md` §8 first.

## 6. Procedure and verification

```bash
DOTNET="$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe"
# The running API locks its DLLs - stop it first.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev-down.ps1 -ApiOnly
"$DOTNET" ef migrations add <Name> --project src/NT.QAMS.Infrastructure --startup-project src/NT.QAMS.WebApi
"$DOTNET" ef database update       --project src/NT.QAMS.Infrastructure --startup-project src/NT.QAMS.WebApi
# Round-trip is mandatory - a migration without a working Down() fails CI:
"$DOTNET" ef database update <PreviousMigration> --project src/NT.QAMS.Infrastructure --startup-project src/NT.QAMS.WebApi
"$DOTNET" ef database update       --project src/NT.QAMS.Infrastructure --startup-project src/NT.QAMS.WebApi
```

Then **prove it from the catalogue**, not from your own diff. These must all hold:
```sql
-- RLS parity: 0 rows, always
SELECT n.nspname||'.'||c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
WHERE c.relkind='r' AND n.nspname IN ('qams','audit','read')
  AND EXISTS (SELECT 1 FROM information_schema.columns col WHERE col.table_schema=n.nspname
              AND col.table_name=c.relname AND col.column_name='tenant_id' AND col.is_nullable='NO')
  AND (NOT c.relrowsecurity OR NOT c.relforcerowsecurity
       OR NOT EXISTS (SELECT 1 FROM pg_policies p WHERE p.schemaname=n.nspname
                      AND p.tablename=c.relname AND p.policyname='tenant_isolation'));

-- No single-column PK on a NOT NULL tenant table: 0
-- No identifier over 62 chars: 0 from pg_class and 0 from pg_constraint
-- Guard triggers: 17, all tgenabled='O'
```
Audit rows are RLS-hidden in `psql` unless you first
`SELECT set_config('app.bypass_rls','on',false);`.

**Regenerate the deployment script** when you add migrations:
`dotnet ef migrations script --idempotent -o deploy/migrations.sql`.

## 7. Tests that will catch you

| Test | Fails when |
| ---- | ---------- |
| `OwnedChildTenancyTests` | any NOT-NULL-tenant table lacks full RLS; a cross-tenant child insert succeeds |
| `SecurityEventRlsTests` | ledger isolation or the pre-auth null-tenant write regresses |
| `CheckConstraintTests` | a domain/hash CHECK stops rejecting bad values |
| `RegulatedFlowRealDatabaseTests` | sign-in, provisioning, ledger attribution or isolation break **against real PostgreSQL** |
| `GovernanceTests` | the newest migration does not round-trip |
| `UserAccountTenantBoundTests` | a new unbounded `db.Users` query appears |

Note `GovernanceTests` reverts and re-applies the latest migration against whatever database it
is pointed at. On the shared dev database that empties the newest migration's tables; the startup
backfill re-seeds on the next API start. Restart the API after running the integration suite on dev.
