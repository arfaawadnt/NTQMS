# NT.QMS — Observability runbook (EA remediation Phase 2, v1.40)

## Logs (OBS-001)

- **Format:** structured JSON on stdout in Production (built-in `JsonConsole`
  formatter, UTC timestamps, scopes included). Development keeps the readable
  console.
- **Enrichment:** every request-scoped record carries `Service`,
  `Environment`, `CorrelationId`, `TraceId` (log scope). Each request also
  emits ONE canonical completion record with `Service`, `Environment`,
  `Method`, `Path`, `Operation`, `Status`, `Outcome`
  (`success | client-error | server-error`), `DurationMs`, `TenantId`,
  `UserId`, `CorrelationId` — this shape is asserted by test
  (`ObservabilityTests`).
- **Sink:** collect stdout with the platform's log shipper, and/or set
  `Otlp__Endpoint` to export logs (with scopes) over OTLP to
  Seq/Loki/Elastic/etc.
- **Retention:** align with the DR runbook (`BACKUP-RESTORE-DR.md`):
  application logs ≥ **35 days** hot (matches daily-backup retention), audit
  evidence stays in the database ledger (7-year class) — logs are diagnostics,
  never the compliance record.

## Traces (OBS-002)

- One trace spans **HTTP → MediatR → EF/Npgsql → Outbox delivery**: the outbox
  row stores the writing trace's W3C `traceparent`
  (`qams.outbox_event.trace_parent`) and the processor parents the delivery
  span on it across the async boundary.
- Sources: `NT.QAMS.Application` (MediatR requests), `NT.QAMS.Outbox`
  (event delivery), `NT.QAMS.Jobs` (compliance sweep, KPI snapshot), plus
  ASP.NET Core server spans and Npgsql command spans.
- Export: set `Otlp__Endpoint` (e.g. `http://collector:4317`). Health/metrics
  probes are filtered out of tracing.
- **Correlation:** clients may send `X-Correlation-Id` (≤64 chars,
  `[A-Za-z0-9._-]`); the API echoes it on EVERY response and stamps it plus
  `traceId` into all ProblemDetails bodies — quote both in support tickets.

## Metrics (OBS-003)

Scrape endpoint: **`GET /metrics`** (Prometheus text format, anonymous —
measurements only). Also exported over OTLP when `Otlp__Endpoint` is set.

| Metric | Meaning |
|---|---|
| `http.server.request.duration` (ASP.NET) | RED: rate, errors, latency |
| Npgsql meter (`db.client.*`) | connection pool usage/wait |
| `qams.outbox.processed` / `.failed` / `.dead_lettered` | delivery counters |
| `qams.outbox.backlog` | live (unprocessed) outbox rows |
| `qams.outbox.oldest_pending_age_seconds` | delivery lag |
| `qams.outbox.dead_letters` | rows awaiting manual triage |
| `qams.job.last_success_timestamp_seconds{job=…}` | job liveness (`compliance-sweep`, `kpi-snapshot`) |

## Actionable alerts (define in the monitoring stack)

| Alert | Condition (PromQL-style) | Action |
|---|---|---|
| Error rate | 5xx ratio > 5% over 5m | page — check recent deploy, DB health |
| Latency | p95 `http.server.request.duration` > 2s over 10m | investigate slow queries / pool wait |
| **Outbox dead-letter** | `qams_outbox_dead_letters > 0` | triage `qams.outbox_event WHERE dead_lettered_at_utc IS NOT NULL`; fix, then clear `dead_lettered_at_utc`+`attempts` to replay |
| Outbox backlog age | `qams_outbox_oldest_pending_age_seconds > 600` | processor stuck/crashed — check logs, DB |
| Sweep liveness | `time() - qams_job_last_success_timestamp_seconds{job="compliance-sweep"} > 7200` (2× interval) | sweep not running — check leader election / errors |
| Snapshot liveness | same, `job="kpi-snapshot"` > 43200 (2× 6h) | as above |
| Readiness flapping | `/health/ready` non-200 | PostgreSQL down/unreachable |

The dead-letter ERROR log (`Outbox event … DEAD-LETTERED`) doubles as a
log-based alert channel where no metrics stack exists yet.
