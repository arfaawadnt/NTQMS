# NT.QMS — Staging bring-up runbook (closes R-5 / R-7)

One procedure to stand up a production-shaped staging environment on a Docker
host, wire telemetry, and gate it with the smoke suite. This is the target the
penetration test (PENTEST-SOW-NTQMS-001) and the authoritative load/soak run
against. **It requires a Docker host — it cannot run on the Docker-less dev
workstation; that is exactly why R-5/R-7 remain open until this is executed.**

## 0. Prerequisites
- A Linux Docker host (or Docker Desktop) with `docker compose`.
- DNS/hostname + a TLS certificate for the staging origin (or a proxy that
  provisions one, e.g. Caddy/Traefik/nginx).
- Secrets to hand (never commit): `QAMS_OWNER_PASSWORD`, `QAMS_APP_PASSWORD`,
  `QAMS_JWT_SECRET` (>=32 chars), `GRAFANA_ADMIN_PASSWORD`.

## 1. Database roles + schema
```bash
# On the DB (compose brings up postgres as qams_owner):
docker compose -f deploy/compose.production.yml up -d postgres
# Apply schema as the OWNER, then grant the least-privilege runtime role:
psql "$OWNER_URL" -f deploy/migrations.sql
psql "$SUPERUSER_URL" -d ntqams -f deploy/harden-runtime-role.sql
```

## 2. App (non-root container, replicas: 1 per ADR-0001)
```bash
QAMS_APP_PASSWORD=... QAMS_JWT_SECRET=... \
  docker compose -f deploy/compose.production.yml up -d api
```
Point the API's `Otlp__Endpoint` at `http://otel-collector:4317` (already set
in the compose env when the observability stack shares the network).

## 3. TLS + HSTS (ADR-0002)
Front the loopback-published API (`127.0.0.1:5000`) with the TLS proxy; enable
HTTP->HTTPS redirect. Verify:
```bash
curl -sI https://<staging-host>/health/ready | grep -i strict-transport-security
```

## 4. Observability
```bash
GRAFANA_ADMIN_PASSWORD=... \
  docker compose -f deploy/observability/compose.observability.yml up -d
```
- Prometheus targets UP: `https://<host>:9090/targets` (api + collector).
- Grafana "NT.QMS / Service Overview" panels populate.
- Alerts loaded: `https://<host>:9090/alerts`.

## 5. Smoke gate (must pass before declaring staging ready)
```powershell
# Health + security posture + envelope, against the staging URL:
./scripts/staging-smoke.ps1 -BaseUrl https://<staging-host> `
    -Tenant <seed-tenant> -Email <admin> -Password <pw>
```
This runs readiness, the fast security probe, and the deep probe against the
remote target and fails on any regression.

## 6. Failure-drill confirmation (closes the R-7 "alert fires" gap)
```bash
# Stop the DB container; /health/ready must flip to 503 and the readiness
# alert must fire in Prometheus/Grafana; restart and confirm recovery.
docker compose -f deploy/compose.production.yml stop postgres
#   ... observe alert ...  then:
docker compose -f deploy/compose.production.yml start postgres
# Poison-event drill against the live processor:
./scripts/failure-drills.ps1 -BaseUrl https://<staging-host>
#   ... confirm the OutboxDeadLetter alert fires in Grafana ...
```

## 7. Authoritative load + soak (closes R-5)
```powershell
# From a SEPARATE host (not the app host), sized limiter or exempt IP agreed:
dotnet run --project tests/NT.QAMS.LoadTests -c Release -- `
    --base https://<staging-host> --tenant <t> --email <e> --password <pw> `
    --users 100 --seconds 300
```
Then a 24 h soak with the dashboards recording; capture the numbers into
`docs/reference/NT_QMS_Load_Test_Report.md` as the authoritative baseline.

## 8. Hand to the pen-test vendor
Once §1–§7 are green, the environment satisfies the pen-test SOW's Rules of
Engagement — hand over the staging URL + per-role/per-tenant credentials
(see the readiness checklist) and open the test window.

## Done = residuals closed
- **R-7** closed when §4 + §6 confirm dashboards populate and a drill fires an
  alert.
- **R-5** closed when §7 records a production-scale load + soak result.
- The **pen test** (external) then runs against this same environment.
