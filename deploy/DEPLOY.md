# NT.QAMS — Deployment Guide (v1.0 build)

## What this package contains — read first

This deploys the **NT.QAMS backend, increment v1.0**:

- **JWT authentication** (deny-by-default: every endpoint requires a valid token
  except `/api/auth/login` and `/health`), role-gated APIs, platform-admin bootstrap
- **Tenancy control plane** — provision tenants + their admin user atomically
- **Nonconformance & CAPA module** — the full 9-state workflow (raise → submit →
  triage/reject → RCA → CAPA actions → verification → effectiveness → SoD-guarded
  closure) with race-free NC reference numbering and multi-tenant isolation
- **Document Control module** — controlled SOP lifecycle (draft → review →
  approve → publish → obsolete), major/minor versioning with atomic supersession,
  SoD (author ≠ reviewer/approver), content-addressed file upload/download
  (SHA-256 integrity anchors; default storage `data\files` under the app folder —
  set `FileStorage__RootPath` to a dedicated data disk in production)
- **Audit Management module** — schedule audits with ISO-clause checklists,
  execute (answer items, raise findings), and sign off with two hard gates:
  checklist fully answered and **every NC-graded finding automatically gets a
  Nonconformance** (cross-module event saga, idempotent) acknowledged back
  before sign-off; signed-off audits are immutable
- **Equipment & Calibration module** — instrument registry with the auto-lockout
  state machine (due → grace → out-of-service), calibration/maintenance logs with
  certificate files, and an **hourly compliance sweep** that proposes due/lockout
  transitions (aggregates decide — a job can never bypass the guards)
- **Competency & Training module** — assessment scoring with the 80% pass gate,
  SoD (trainee cannot assess/authorize self), authorization with validity expiry
  (the sweep returns expired competencies to requalification), training queue
- **Risk & Governance module** — risk register with explicit 1-5 assessments
  (no defaults), mitigation actions, residual-assessment gate before closure and
  high-residual alerts; change control where **approval requires a linked risk
  assessment**; management reviews with decisions and immutable signed minutes
- **Supplier Quality module** — approval lifecycle with SoD (registrant cannot
  approve own supplier), certificate registry with **expiry auto-suspension via
  the sweep**, weighted periodic evaluations (score of record)
- **Organization & reference data** -- branches, departments (FK-linked at last), test catalog, trilingual (EN/AR/FR) list-of-values
- **Notifications engine** -- rule-driven dispatch on domain events (7 event types seeded per tenant at provisioning), in-app feed (/api/notifications/mine), SMTP email when Smtp__Host is configured (in-app delivery never depends on email), delivery monitor with per-recipient status
- **Analytical Quality module** -- CLSI method-validation studies (protocol -> data -> computed CV%/bias vs TEa -> signed lock), QC with Westgard multi-rule evaluation (1-3s/2-2s/R-4s/10-x reject, 1-2s warn) on each control run, proficiency testing with z-score performance categories; an unsatisfactory PT result auto-raises an NC (cross-module saga)
- **Records & Retention module** -- quality archive with retention classes
  (5yr/10yr/permanent), authorized disposal only after the retention period
  (permanent records never disposable), retrieve/return
- **SLA, Escalation & Tasks module** -- SLA target definitions; an escalation
  timer is armed when a CAPA action is planned and cancelled when it completes;
  the hourly tick advances overdue timers through the +24h(owner)/+48h/+72h(QM)
  ladder, each step creating a My-Tasks work item and firing an escalation
  notification; /api/tasks/mine is the personal + role work queue
- **Security & compliance hardening (21 CFR Part 11)** -- account lockout
  (5 failures -> 30-minute lock), TOTP MFA (enroll via /api/auth/mfa/enroll,
  enforced at login), a **hash-chained tamper-evident audit trail** appended for
  every domain event (chain-verification endpoint), a **security-event log**
  (all logins/lockouts/MFA), and **electronic-signature PINs** with an immutable
  signature ledger -- document publish is wired as the signing ceremony (requires
  the approver's 4-digit PIN + records a Part 11 signature linked by content hash).
  Audit ledgers are append-only at the database (RLS + guard triggers)
- Transactional outbox, audit stamping, RLS policies on tenant business tables

All 14 functional bounded contexts plus the Part 11 compliance layer are now
built. **Not yet included:** the fine-grained ~70-privilege matrix (role-based
gates are in place today), e-signature PIN ceremonies on the remaining signing
points beyond document-publish (same pattern, not yet retrofitted everywhere),
SignalR real-time push, and the Angular UI (this build is API-only).
Later increments deploy onto this same installation — upgrades are: stop service,
replace folder, run new `migrations.sql`, start.

## Package contents

| Item | Purpose |
|---|---|
| `publish-win-x64/` | Self-contained Windows x64 build — **no .NET runtime needed on the server** |
| `migrations.sql` | Idempotent schema script (safe to re-run on every upgrade) |
| `db-init.sql` | One-time role + database bootstrap (run as postgres superuser) |
| `DEPLOY.md` | This guide |
| `ANTIGRAVITY_DEPLOY_PROMPT.md` | Copy-paste prompt for AI-assisted deployment |

Linux/containers: build from source with `docker build -f src/NT.QAMS.WebApi/Dockerfile .`

## Prerequisites

- Windows Server x64 (2016+), or any Docker host
- PostgreSQL **17** reachable from the app server
- One open TCP port (examples use 5000)

## Steps

### 1 — Database (once)

```
psql -U postgres -f db-init.sql          # edit the password inside first
psql -U qams_app -d ntqams -f migrations.sql
```

### 2 — Copy & configure

Copy `publish-win-x64\` to e.g. `C:\apps\ntqams\`. Configure via **environment
variables only** (never put secrets in appsettings.json):

| Variable | Value |
|---|---|
| `ConnectionStrings__Postgres` | `Host=DBHOST;Port=5432;Database=ntqams;Username=qams_app;Password=***` |
| `Jwt__Secret` | **Random ≥ 48 chars** — generate: `powershell -c "-join ((48..57)+(65..90)+(97..122) | Get-Random -Count 48 | % {[char]$_})"`. The app refuses to start without it. |
| `PlatformAdmin__Email` | Bootstrap platform admin login (created on first start if absent) |
| `PlatformAdmin__Password` | Strong password, ≥ 12 chars |
| `ASPNETCORE_URLS` | `http://127.0.0.1:5000` (loopback; front with a TLS reverse proxy for network exposure) |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Database__MigrateOnStartup` | `false` (or `true` for first boot instead of running migrations.sql) |

### 3 — Run as a Windows service

```
sc.exe create NTQAMS binPath= "C:\apps\ntqams\NT.QAMS.WebApi.exe" start= auto
sc.exe start NTQAMS
```

Set the variables machine-wide (`[Environment]::SetEnvironmentVariable(..., 'Machine')`)
or on the service key `HKLM\SYSTEM\CurrentControlSet\Services\NTQAMS\Environment`.

### 4 — Verify (complete end-to-end check)

```bat
:: 1. Health (anonymous)
curl http://127.0.0.1:5000/health                              → 200 Healthy

:: 2. Deny-by-default proof (no token)
curl -i http://127.0.0.1:5000/api/tenants                      → 401

:: 3. Platform admin login
curl -X POST http://127.0.0.1:5000/api/auth/login -H "Content-Type: application/json" ^
  -d "{\"email\":\"<PlatformAdmin__Email>\",\"password\":\"<PlatformAdmin__Password>\"}"
::  → 200 { accessToken: "...", role: "PlatformAdmin" }   — save TOKEN

:: 4. Provision a tenant + its admin
curl -X POST http://127.0.0.1:5000/api/tenants -H "Authorization: Bearer TOKEN" ^
  -H "Content-Type: application/json" ^
  -d "{\"identifier\":\"first-lab\",\"name\":\"First Laboratory\",\"adminEmail\":\"qa@first-lab.test\",\"adminDisplayName\":\"QA Manager\",\"adminPassword\":\"ChangeMe-Initial-1!\"}"
::  → 201

:: 5. Tenant login
curl -X POST http://127.0.0.1:5000/api/auth/login -H "Content-Type: application/json" ^
  -d "{\"tenantIdentifier\":\"first-lab\",\"email\":\"qa@first-lab.test\",\"password\":\"ChangeMe-Initial-1!\"}"
::  → 200 — save TTOKEN

:: 6. Raise a nonconformance (gets ref NC-YYYY-0001)
curl -X POST http://127.0.0.1:5000/api/nonconformances -H "Authorization: Bearer TTOKEN" ^
  -H "Content-Type: application/json" ^
  -d "{\"title\":\"Deployment smoke NC\",\"description\":\"verification record\",\"severity\":2,\"likelihood\":2,\"sourceType\":\"Internal\"}"
::  → 201

:: 7. List — confirms tenant scoping + persistence
curl http://127.0.0.1:5000/api/nonconformances -H "Authorization: Bearer TTOKEN"   → 200 [ ...NC... ]
```

Outbox check (optional): `SELECT processed_at_utc FROM qams.outbox_event` — rows
gain timestamps within seconds, proving the event pipeline runs.

## Operations

- **Upgrades:** stop service → replace folder → run new `migrations.sql` → start.
- **Backups:** standard PostgreSQL dumps; app folder is stateless.
- **Never** expose the port directly to the internet; TLS-terminating reverse
  proxy (IIS ARR / nginx) in front, Kestrel on loopback.
