# ADR-0006 — Outbox reliability model

- **Status:** Accepted (2026-07-28) — formalizes the Phase-1 implementation
- **Finding:** MSG-004/005/006/007 + OPS-002 (durable); required ADR per plan §04

## Decision

The transactional outbox (rows written in the SAME transaction as the
aggregate change) delivers **at-least-once**, with this robustness contract:

1. **Claiming:** processors claim due rows with `FOR UPDATE SKIP LOCKED` plus
   a 2-minute lease (`claimed_until_utc`) — concurrent processors receive
   disjoint batches; a crashed claimant's rows become reclaimable when the
   lease lapses. This is what makes horizontal scale-out safe.
2. **Retry:** failures back off exponentially per event
   (`next_attempt_at_utc` = 5s·2^(n−1) + ≤25% jitter); a failing event never
   head-of-line-blocks healthy ones.
3. **Dead-letter:** after 5 attempts the row leaves the retry stream
   (`dead_lettered_at_utc`), raising the `qams.outbox.dead_lettered` metric
   and an ERROR log; triage query + replay procedure in
   deploy/OBSERVABILITY.md.
4. **Dedup:** consumers are idempotent by natural key — DB-enforced where it
   matters (`ux_nonconformance_source`; notifications dedupe by
   SourceEventId) — so redelivery nets one side-effect.
5. **Retention:** processed rows purge after `Outbox:RetentionDays` (30);
   the hash-chained audit ledger is the permanent record, the outbox is
   transport.
6. **Recurring jobs** (compliance sweep, KPI snapshot) elect a leader per
   round via `pg_try_advisory_xact_lock` (AdvisoryLockKeys registry).

## Consequence

With (1) and (6), ADR-0001's single-replica constraint is no longer a
correctness requirement — it remains the supported default until scale-out is
re-validated under load.
