# NT.QMS observability stack (Road-to-100 Phase 8)

A drop-in telemetry backend for a staging/production host: an OpenTelemetry
Collector, Prometheus (with the alert rules), and Grafana (with a provisioned
overview dashboard). It consumes what the app already emits — no app change.

## Wire it up

1. Bring the app up from `../compose.production.yml` on the same Docker network.
2. Point the API at the collector: set `Otlp__Endpoint=http://otel-collector:4317`
   in the api service environment (traces + metrics + logs flow over OTLP).
   Prometheus also scrapes the API's own `/metrics` directly.
3. `GRAFANA_ADMIN_PASSWORD=... docker compose -f compose.observability.yml up -d`
4. Grafana → http://host:3000 (NT.QMS folder → "Service Overview"); Prometheus
   alerts → http://host:9090/alerts.

## What's here

| File | Purpose |
|---|---|
| `compose.observability.yml` | collector + Prometheus + Grafana |
| `otel-collector.yaml` | OTLP in → Prometheus exposition (traces/logs to a backend when provisioned) |
| `prometheus.yml` | scrape the API `/metrics` and the collector |
| `alert.rules.yml` | the OBSERVABILITY.md alert set as PromQL (dead-letter, backlog age, sweep/snapshot liveness, 5xx rate, p95 latency) |
| `grafana/` | auto-provisioned Prometheus datasource + RED / outbox / job-liveness / pool dashboard |

## Status (honest scope — closure-report R-7)

Authored and reviewed; **not run on this Docker-less dev workstation.** The
alert PromQL and dashboard queries target the exact metric names the app emits
(verified live via `/metrics`: `qams_outbox_*`, `qams_job_*`,
`http_server_request_duration_seconds_*`, `db_client_connections_usage`). First
staging host must `docker compose up` this stack, confirm targets are UP and
panels populate, then fire the failure drills (`scripts/failure-drills.ps1`)
and confirm the dead-letter + readiness alerts trigger end-to-end. Record the
result against R-7.
