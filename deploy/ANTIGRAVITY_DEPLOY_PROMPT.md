# Antigravity Deployment Prompt -- NT.QAMS v1.0

Copy everything inside the fence below into Antigravity on (or with remote access
to) the **target server**, after copying the deployment package
`NT.QAMS-webapi-v1.0-win-x64.zip` to the server (e.g. `C:\install\`).

---

```
You are deploying the NT.QAMS backend (v1.0) to this Windows server. You are an
operations agent: follow this runbook exactly, verify every step, and STOP and
report if any verification fails. Do not improvise alternative architectures,
do not edit application code, and do not skip verification steps.

CONTEXT
- Package: C:\install\NT.QAMS-webapi-v1.0-win-x64.zip
  Contents: publish-win-x64\ (self-contained .NET app, no runtime install needed),
  migrations.sql (idempotent), db-init.sql, DEPLOY.md (authoritative reference).
- The app is an ASP.NET Core API listening via Kestrel. It requires PostgreSQL 17.
- Security posture: JWT deny-by-default. /health and /api/auth/login are the only
  anonymous endpoints. The app REFUSES to start without a Jwt__Secret env var.

HARD RULES
1. Bind the app to 127.0.0.1 or a private-network IP only. NEVER expose the port
   to the internet. If public access is required, report that a TLS reverse proxy
   must be configured first -- do not configure one on your own initiative.
2. Generate secrets; never reuse examples or dev values. Store what you generate
   in C:\install\SECRETS-README.txt with restricted ACL (Administrators only),
   and tell the operator to move them to a password manager and delete the file.
3. Never put secrets in appsettings.json. Environment variables only.
4. If any command errors or any verification returns an unexpected result, stop
   and produce a diagnosis report instead of continuing.

RUNBOOK

Step 1 -- Preflight
- Verify Windows x64: [Environment]::Is64BitOperatingSystem must be True.
- Verify PostgreSQL 17 is reachable: either a local service "postgresql*17*"
  running, or the operator-provided DBHOST answers on port 5432
  (Test-NetConnection DBHOST -Port 5432). If no PostgreSQL exists and the
  operator authorized installing it, install PostgreSQL 17 from the official
  EDB distribution, set a strong superuser password (generate it, rule 2).
- Verify port 5000 is free: (Test-NetConnection 127.0.0.1 -Port 5000) must fail.

Step 2 -- Extract
- Expand the zip to C:\apps\ntqams\ so that C:\apps\ntqams\NT.QAMS.WebApi.exe exists.

Step 3 -- Database
- Edit a COPY of db-init.sql: replace CHANGE_ME_BEFORE_RUNNING with a generated
  32-char password (record per rule 2).
- Run it once as superuser:  psql -U postgres -f <edited-db-init.sql>
  (If role/database already exist from a prior install, skip creation -- do not drop.)
- Apply schema AS THE OWNER:  psql -U qams_owner -d ntqams -f <package>\migrations.sql
  This script is idempotent -- safe on re-runs and upgrades. DDL MUST run as
  qams_owner, NEVER qams_app: the runtime role has no DDL rights, and in Production
  the app refuses to start if qams_app owns the tables (TENANT-004 guard).
- Grant the runtime role its least-privilege DML surface (as superuser):
    psql -U postgres -d ntqams -f <package>\harden-runtime-role.sql
- Verify: psql -U qams_app -d ntqams -c "\dt qams.*"  must list nonconformance,
  capa_action, rca_record, user_account, ref_counter, outbox_event,
  controlled_document, document_version, file_reference, audit,
  audit_checklist_item, audit_finding, equipment_item, calibration_record,
  maintenance_record, competency_record, assessment_result, training_assignment,
  risk_item, mitigation_action, change_request, management_review,
  review_decision, supplier, supplier_certificate, supplier_evaluation,
  branch, department, test_catalog_item, lov_entry, notification_rule,
  notification_dispatch, qc_profile, qc_run, validation_study,
  validation_replicate, pt_enrollment, archive_entry, sla_definition,
  work_task, escalation_timer.
  Plus the audit schema: audit.audit_trail, audit.electronic_signature,
  audit.security_event (append-only, guard-triggered).

Step 4 -- Configuration (machine-level environment variables)
Generate (rule 2): JWT_SECRET = 48 random alphanumeric chars;
PLATFORM_ADMIN_PASSWORD = 20 random chars incl. symbols.
Set machine-scope env vars:
  ConnectionStrings__Postgres = Host=<DBHOST|localhost>;Port=5432;Database=ntqams;Username=qams_app;Password=<db password>
  Jwt__Secret                 = <JWT_SECRET>
  PlatformAdmin__Email        = admin@<company-domain>   (ask operator; else admin@ntqams.local)
  PlatformAdmin__Password     = <PLATFORM_ADMIN_PASSWORD>
  ASPNETCORE_URLS             = http://127.0.0.1:5000
  ASPNETCORE_ENVIRONMENT      = Production
  Database__MigrateOnStartup  = false
  FileStorage__RootPath       = D:\ntqams-data\files   (or the operator's data disk;
                                create the directory; exclude it from antivirus
                                real-time scans only if the operator approves)

Step 5 -- Windows service
  sc.exe create NTQAMS binPath= "C:\apps\ntqams\NT.QAMS.WebApi.exe" start= auto
  sc.exe start NTQAMS
- Verify the service reaches RUNNING and stays RUNNING for 30 seconds.
- If it exits immediately: run the exe in a console to capture the startup error
  (most common: missing Jwt__Secret or unreachable database), fix, retry once.

Step 6 -- Verification suite (ALL must pass; use curl or Invoke-WebRequest)
  a) GET  /health                        -> 200 "Healthy"
  b) GET  /api/tenants  (no token)       -> 401        (deny-by-default proof)
  c) POST /api/auth/login  {email: <PlatformAdmin__Email>, password: <...>}
                                         -> 200, role "PlatformAdmin"; save TOKEN
  d) POST /api/tenants (Bearer TOKEN)
       {identifier:"<slug operator chose, e.g. main-lab>", name:"<Lab name>",
        adminEmail:"<operator-provided>", adminDisplayName:"<name>",
        adminPassword:"<generate, rule 2>"}            -> 201
  e) POST /api/auth/login {tenantIdentifier:"<slug>", email:<adminEmail>, ...}
                                         -> 200; save TTOKEN
  f) POST /api/nonconformances (Bearer TTOKEN)
       {title:"Deployment verification NC", description:"installer smoke test",
        severity:2, likelihood:2, sourceType:"Internal"}    -> 201
  g) GET  /api/nonconformances (Bearer TTOKEN) -> 200, contains ref "NC-<year>-0001"
  h) File pipeline: create a small test.pdf, then
       POST /api/files (Bearer TTOKEN, multipart field "file")  -> 201, note fileId
       POST /api/documents (Bearer TTOKEN)
         {code:"SOP-TEST-001", title:"Deployment verification SOP",
          category:"SOP", fileId:"<fileId>", changeSummary:"install check"} -> 201
       GET  /api/files/<fileId> (Bearer TTOKEN) -> 200, bytes match the upload
  i) psql: SELECT count(*) FROM qams.outbox_event WHERE processed_at_utc IS NOT NULL;
     -> must be >= 1 within 30 seconds (event pipeline works)

Step 7 -- Report
Produce a deployment report: server name, paths, service name/state, database
host, file-storage path, which verifications passed (a-i with actual responses),
where secrets were recorded, and any deviations. Remind the operator:
- move secrets from SECRETS-README.txt to a vault, then delete the file
- the API is loopback-only by design; network exposure requires a TLS reverse proxy
- back up BOTH PostgreSQL and the FileStorage__RootPath directory
- upgrades = stop service -> replace C:\apps\ntqams -> run new migrations.sql -> start
```
