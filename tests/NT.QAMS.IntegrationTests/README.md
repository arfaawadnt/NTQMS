# NT.QMS Integration Tests (real PostgreSQL)

Automated verification of the database-enforced controls that cannot be proven
against EF In-Memory — closes audit finding **F-09** without requiring Docker /
Testcontainers.

## What they prove
- **F-01 tenant isolation** — FORCED Row-Level Security isolates tenants at the
  database, fails closed on an unresolved tenant, honours the controlled
  `app.bypass_rls` elevation, and rejects cross-tenant writes via `WITH CHECK`
  (the EF query filter is switched off in the read assertions, so it is RLS on
  trial, not the in-process filter).
- **F-02 signed-record immutability** — the `reject_frozen_mutation` trigger
  rejects raw SQL `UPDATE`/`DELETE` of a signed record, while still allowing the
  legitimate transition *into* the signed state.

Every test runs inside a transaction that is rolled back, so nothing persists —
not even an otherwise-immutable signed row.

## Running
Requires a reachable, **migrated** PostgreSQL database (RLS forced by migration
`ActivateForcedTenantRls`, triggers by `SignedRecordImmutability`).

```bash
# Local dev (default connection — the running dev database):
dotnet test tests/NT.QAMS.IntegrationTests

# CI — point at a freshly-provisioned, migrated database:
QMS_ITEST_POSTGRES="Host=...;Port=5432;Database=ntqms_ci;Username=...;Password=..." \
  dotnet test tests/NT.QAMS.IntegrationTests
```

If no server is reachable (or the schema is not migrated / RLS not forced) the
tests **skip** with a clear reason rather than failing, so the suite stays green
on machines without a database. In CI, provision Postgres, run migrations, then
run these tests as the gate for F-01/F-02.
