# ADR-0005 — Optimistic concurrency via PostgreSQL xmin

- **Status:** Accepted (2026-07-28) — formalizes the Phase-1 implementation
- **Finding:** DB-009/VAL-003; required ADR per remediation plan §04

## Decision

Every aggregate root maps PostgreSQL's `xmin` system column as its EF Core
concurrency token, applied by convention in `AppDbContext.OnModelCreating`
(Npgsql provider only) — zero schema change, structurally impossible for a
new module to forget. A lost update surfaces as
`DbUpdateConcurrencyException`, which the API maps to **409 Conflict** with
stable code `CONCURRENCY-409` (problem+json); the client reloads and
reapplies.

## Relationship to trigger-based immutability

The two mechanisms answer different questions and coexist deliberately:
- `xmin` guards **live** records against concurrent-editor lost updates
  (a race, both writers legitimate).
- The `reject_frozen_mutation` triggers guard **signed/frozen** records
  against ANY mutation (no writer legitimate) — Part 11 record protection.

## Alternatives rejected

A `RowVersion`/`bytea` column on `AggregateRoot` (schema change on ~50 tables
+ per-entity mapping opportunities to forget); pessimistic locking (kills lab
concurrency, invites deadlocks).

## Operational note

Mapping `xmin` makes `dotnet ef migrations add` scaffold spurious
`AddColumn xmin` operations — remove them by hand (system column), per the
Npgsql documentation. Recorded in IMPLEMENTATION_LOG (Phase 1).
