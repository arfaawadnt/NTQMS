# NT.QMS — Load Test Report (Road-to-100 Phase 8)

| | |
|---|---|
| **Date** | 2026-07-28 |
| **Build** | `v1.47.0` |
| **Harness** | `tests/NT.QAMS.LoadTests` (BCL concurrent generator) |
| **Environment** | Dev workstation — API + PostgreSQL 17 + load generator all on ONE box (worst case; production separates them) |
| **Profile** | 50 virtual users · 30 s per scenario · authenticated read mix · global rate limiter raised to a load-test ceiling |
| **Thresholds** | reads p95 < 500 ms · error rate < 0.1% |

## Result — PASS

| Scenario | requests | rps | p50 (ms) | p95 (ms) | p99 (ms) | error % |
|---|---|---|---|---|---|---|
| `GET /api/nonconformances` | 22,517 | 750.6 | 62.9 | 104.7 | 178.8 | 0.00 |
| `GET /api/documents` | 20,397 | 679.9 | 75.1 | 101.4 | 121.2 | 0.00 |
| `GET /api/audits` | 24,116 | 803.9 | 61.4 | 85.9 | 97.4 | 0.00 |
| `GET /api/risks` | 24,374 | 812.5 | 60.9 | 85.8 | 95.2 | 0.00 |

Every scenario is ~5× inside the p95 budget with **zero errors** at 50
concurrent users, on a box that is simultaneously running the database and the
load generator. These are a conservative floor.

## Findings

1. **Rate limiter is an abuse ceiling, not a concurrency ceiling.** With the
   default `RateLimit:GlobalPermitPerMinute` = 300, a single-origin run returns
   ~50% 429 within the first minute (correct for one abusive client). Real
   deployments must size this to expected legitimate peak concurrency,
   especially where a lab shares one NAT address. **Action:** set the value
   per site at deployment; the reverse proxy may also partition by
   authenticated principal.
2. **Latency is dominated by the DB round-trip**, not app overhead — p50 tracks
   query cost; the pagination envelope (API-004) and `AsNoTracking` keep reads
   cheap.
3. **No error under sustained concurrency** — the connection pool, retry
   policy (OPS-009) and per-request scope hold up at 50 users.

## Limitations (honest scope)

- Single-box dev run; **not** a production-scale or multi-node test.
- Read-heavy; the write mix (`--with-writes`) is available but was not run
  against the shared dev DB to avoid persisting fixture records.
- No 24-hour soak, no cross-node contention, no real network. These belong to
  the **Phase-8 external track**: run this harness (and a soak) from a separate
  host against staging stood up from `deploy/compose.production.yml` +
  `deploy/observability/`, and record the numbers here as the authoritative
  baseline (residual R-5).
