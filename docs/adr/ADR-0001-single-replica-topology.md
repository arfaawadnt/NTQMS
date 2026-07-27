# ADR-0001 — Single-replica API topology (until Phase-1 scale-out controls)

- **Status:** Accepted (2026-07-28)
- **Finding:** OPS-002 (Enterprise Architecture audit / remediation plan, Phase 0)
- **Decision owners:** NT.QMS engineering

## Context

The API host runs three background workers against the shared PostgreSQL
database: the transactional-outbox processor (audit/event fan-out), the
scheduled compliance sweeps (calibration lockout, competency expiry, supplier
certificate suspension, …) and the daily KPI snapshot. None of them yet uses a
cross-instance claim protocol — the outbox reads pending rows without
`FOR UPDATE SKIP LOCKED`, and the sweeps have no leader election. Running two
API replicas would therefore double-process outbox messages (duplicate audit
ledger entries and notifications) and race the sweeps.

## Decision

**Exactly one API instance per database** is the supported production topology
until the Phase-1 remediation items ship (outbox `SKIP LOCKED` claiming with
dead-letter/backoff, and advisory-lock leader election for the sweeps).

Enforcement and visibility:

1. **Deployment manifests must pin `replicas: 1`** (or the platform's
   equivalent — one Windows service instance, one container). Documented in
   `deploy/DEPLOY.md`.
2. **Runtime sentinel:** at startup every instance tries to take the
   session-scoped PostgreSQL advisory lock `SingleReplicaGuardService.SingletonLockKey`
   and holds it for the process lifetime. An instance that finds the lock
   contended logs a prominent warning naming this ADR, then keeps re-probing —
   so an accidental scale-out is visible in the logs within seconds, without
   taking the extra instance down (a hard exit could flap a misconfigured
   orchestrator into a crash loop).

The sentinel is observability, not a hard gate: correctness still requires the
manifest to pin one replica. That is acceptable for Phase 0 — today's
deployments are single-node — and the hard multi-replica safety lands in
Phase 1 (v1.39).

## Consequences

- Horizontal scale-out of the API is **not** available until Phase 1; vertical
  scaling and a fast single-replica restart (readiness gate `/health/ready`,
  OPS-008) are the availability levers in the interim.
- A contended-lock warning in production logs means two instances share one
  database — scale back to one immediately and audit the outbox/notification
  tables for duplicates.
- Phase 1 replaces this ADR's constraint with: outbox rows claimed via
  `FOR UPDATE SKIP LOCKED`, sweep leader election on the same advisory-lock
  primitive, and this sentinel demoted to an informational metric.
