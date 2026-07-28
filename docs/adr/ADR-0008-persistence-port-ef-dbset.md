# ADR-0008 — The persistence port exposes EF Core DbSet (accepted coupling)

- **Status:** Accepted (2026-07-28) — formalizes the standing convention
- **Finding:** required ADR per remediation plan §04

## Decision

`IAppDbContext` — the Application layer's persistence port — deliberately
exposes `DbSet<T>` and `SaveChangesAsync`, coupling Application to EF Core's
abstractions (not to Npgsql, not to SQL). Handlers compose LINQ against the
port; there is **no generic repository / unit-of-work wrapper**.

## Why this is the right trade

- EF Core already IS the repository + unit-of-work; wrapping it re-implements
  a worse version of both and forfeits `Include`, compiled queries, change
  tracking, `AsNoTracking`, `ExecuteDelete`, paging composition (API-004's
  `ToPagedAsync`) and the interceptor pipeline the compliance controls hang on
  (tenant stamping, field-change ledger, outbox drain).
- The things worth isolating stay isolated: the DOMAIN has zero EF references
  (enforced by LayerRulesTests); provider-specific SQL lives in
  Infrastructure; tests swap the provider (InMemory) through the same port.

## Boundaries that keep the coupling honest

Queries never return `IQueryable` to callers; DTO projection happens inside
handlers; migrations/configurations live in Infrastructure only; raw SQL is
Infrastructure-only. Replacing EF wholesale would be a rewrite decision — a
risk consciously accepted and documented here.
