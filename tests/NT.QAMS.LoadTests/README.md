# NT.QMS load harness (Road-to-100 Phase 8)

A dependency-free (.NET BCL only) concurrent load generator — no NBomber/k6
binary to install. It authenticates once, warms up, then drives a fixed pool
of virtual users against a **running** API for a set duration and reports
latency percentiles, throughput, and error rate per scenario, gating on the
Phase-8 thresholds (reads p95 < 500 ms, error rate < 0.1%).

```bash
# API must be running and reachable; PostgreSQL seeded (demo-lab).
dotnet run --project tests/NT.QAMS.LoadTests -c Release -- \
  --base http://localhost:5080 \
  --tenant demo-lab --email admin@demo-lab.local --password '<pw>' \
  --users 50 --seconds 30
```

Kept OUT of `NT.QAMS.sln` on purpose — it is an operational tool, not part of
the `dotnet test` suite.

## Rate-limit interaction (important)

The global rate limiter (SEC-013) defaults to **300 requests/min per client
IP** — an abuse ceiling, not a concurrency ceiling. A single-origin load run
therefore hits 429 almost immediately (every request shares one IP). That is
correct production behaviour for one abusive client, but to measure the
server's *capacity* you must either:

- run with the limiter sized to the deployment's expected concurrency
  (`RateLimit__GlobalPermitPerMinute` raised for the load-test profile), or
- generate load from many distinct client IPs (a real staging fleet).

**Operational finding:** size `RateLimit:GlobalPermitPerMinute` to expected
legitimate peak concurrency in production, or the limiter will throttle real
users behind shared NAT. Documented in the Phase-8 load report.

## Baseline result (dev workstation, 2026-07-28)

See `docs/reference/NT_QMS_Load_Test_Report.md`. Headline: 50 virtual users,
raised limiter, 30 s/scenario — p95 86–105 ms, 0.00% errors, ~750–800 rps per
read scenario, all well within threshold. Dev-box numbers are a floor;
production hardware and a real staging run (Phase-8 external track) supersede
them.
