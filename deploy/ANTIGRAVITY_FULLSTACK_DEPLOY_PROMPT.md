# Antigravity Full-Stack Deployment Prompt — NT.QAMS v1.51.1

Deploys the **backend API** (Windows service on loopback) and the **Angular SPA**
(served by IIS, which also reverse-proxies `/api` to the backend so they are
same-origin). Copy both packages to the target server first:

- `C:\install\NT.QAMS-webapi-v1.51.1-win-x64.zip`   (backend)
- `C:\install\NT.QAMS-frontend-v1.51.1-dist.zip`      (SPA + web.config)

Then paste everything inside the fence into Antigravity **on the target server**.

---

```
You are deploying NT.QAMS (backend API + Angular SPA) to this Windows server.
You are an operations agent: follow this runbook exactly, verify every step,
and STOP and report if any verification fails. Do not improvise alternative
architectures, do not edit application code, do not skip verification steps.

TOPOLOGY
- Backend: self-contained ASP.NET Core (Kestrel) as a Windows service on
  http://127.0.0.1:5000 (loopback only). Requires PostgreSQL 17.
- Frontend: static Angular files served by IIS on 443 (TLS). IIS also
  reverse-proxies /api, /health, /hubs to the backend. The SPA calls same-origin
  /api, so no CORS and no backend internet exposure.
- Net effect: only IIS (443) faces the network; the API is never directly exposed.

HARD RULES
1. The backend binds to 127.0.0.1 only. Never expose port 5000 to the network.
2. Generate all secrets; never reuse examples/dev values. Record what you
   generate in C:\install\SECRETS-README.txt (ACL: Administrators only) and tell
   the operator to move them to a vault and delete the file.
3. Secrets go in environment variables / service config only, never in files
   under the web root or appsettings.json.
4. On any command error or unexpected verification result, STOP and produce a
   diagnosis report instead of continuing.

=== PART A — BACKEND API ===

A1. Preflight
- [Environment]::Is64BitOperatingSystem must be True.
- PostgreSQL 17 reachable (local service "postgresql*17*" running, or the
  operator's DBHOST answers on 5432). If none exists and the operator authorized
  it, install PostgreSQL 17 (official EDB), generate a strong superuser password.
- Port 5000 must be free on loopback.

A2. Extract backend
- Expand NT.QAMS-webapi-v1.0-win-x64.zip to C:\apps\ntqams\ so that
  C:\apps\ntqams\NT.QAMS.WebApi.exe exists.

A3. Database
- Edit a COPY of db-init.sql: replace CHANGE_ME_BEFORE_RUNNING with a generated
  32-char password (record it). Run once as superuser:
    psql -U postgres -f <edited db-init.sql>
  (If the role/database already exist from a prior install, skip creation — do
  not drop.)
- Apply schema (idempotent, safe to re-run on upgrades):
    psql -U qams_app -d ntqams -f C:\apps\ntqams\...\migrations.sql
  (migrations.sql ships inside the backend zip.)

A4. Backend configuration (machine-scope environment variables)
Generate: JWT_SECRET (48 random alphanumerics), PLATFORM_ADMIN_PASSWORD (20 chars).
  ConnectionStrings__Postgres = Host=<DBHOST|localhost>;Port=5432;Database=ntqams;Username=qams_app;Password=<db pwd>
  Jwt__Secret                 = <JWT_SECRET>
  PlatformAdmin__Email        = admin@<company-domain>   (ask operator)
  PlatformAdmin__Password     = <PLATFORM_ADMIN_PASSWORD>
  ASPNETCORE_URLS             = http://127.0.0.1:5000
  ASPNETCORE_ENVIRONMENT      = Production
  Database__MigrateOnStartup  = false
  FileStorage__RootPath       = D:\ntqams-data\files   (create it; on a data disk)
  # Optional email: Smtp__Host, Smtp__Port, Smtp__User, Smtp__Password, Smtp__From

A5. Backend service
    sc.exe create NTQAMS binPath= "C:\apps\ntqams\NT.QAMS.WebApi.exe" start= auto
    sc.exe start NTQAMS
- Confirm it reaches RUNNING and stays up 30s. If it exits immediately, run the
  exe in a console to read the startup error (usually missing Jwt__Secret or an
  unreachable database), fix, retry once.

A6. Backend smoke (loopback)
- curl http://127.0.0.1:5000/health                         -> 200 "Healthy"
- curl -i http://127.0.0.1:5000/api/tenants (no token)      -> 401
- Platform-admin login returns a token:
    POST http://127.0.0.1:5000/api/auth/login  {email:<PlatformAdmin__Email>, password:<...>}  -> 200

=== PART B — FRONTEND SPA (IIS) ===

B1. IIS prerequisites
- Ensure IIS is installed with the URL Rewrite module AND Application Request
  Routing (ARR). If missing and the operator authorized it, install them
  (Web Platform Installer or the standalone MSIs). Enable ARR proxy:
    Import-Module WebAdministration
    Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/proxy" -name "enabled" -value "True"

B2. Deploy the SPA
- Create C:\inetpub\wwwroot\qams-ui and expand NT.QAMS-frontend-v1-dist.zip into
  it, so index.html and web.config sit at the site root.
- web.config (shipped in the zip) reverse-proxies /api,/health,/hubs to
  http://127.0.0.1:5000 and does SPA fallback to index.html. If the backend
  listens on a different host/port, update the rewrite target in web.config.

B3. IIS site + TLS
- Create (or repoint) an IIS site whose physical path is C:\inetpub\wwwroot\qams-ui.
- Bind HTTPS on 443 with the operator-provided certificate (ask for the cert /
  friendly name; do NOT generate a public cert yourself). Bind HTTP:80 only to
  redirect to HTTPS.
- App pool: "No Managed Code" (static files + proxy; .NET runs as the separate
  Kestrel service).

=== PART C — FULL-STACK VERIFICATION (all must pass) ===

C1. curl -k https://localhost/health                        -> 200 "Healthy"   (proxied to Kestrel)
C2. curl -k https://localhost/                              -> 200, HTML containing "<qams-root>"
C3. curl -k https://localhost/nonconformances               -> 200 index.html   (SPA fallback works)
C4. curl -k https://localhost/api/tenants  (no token)       -> 401              (proxy + deny-by-default)
C5. Provision a tenant via the proxied API (Bearer = platform-admin token from A6):
      POST https://localhost/api/tenants
        {identifier:"<slug>", name:"<Lab>", adminEmail:"<op-provided>",
         adminDisplayName:"<name>", adminPassword:"<generate>"}   -> 201
C6. Tenant login through the proxy:
      POST https://localhost/api/auth/login {tenantIdentifier:"<slug>", email:<adminEmail>, password:<...>} -> 200
C7. Open https://<server-hostname>/ in a browser: the sign-in page renders; log in
    as the tenant admin; the dashboard loads its KPI cards. (If a browser isn't
    available on the server, confirm C1-C6 and note that the UI must be checked
    from a client machine.)

=== PART D — REPORT ===
Produce a deployment report: server name; backend path + service state; database
host; file-storage path; IIS site name + binding + cert; which checks (A6, C1-C7)
passed with actual responses; where secrets were recorded; any deviations. Remind
the operator to:
- move secrets from SECRETS-README.txt into a vault, then delete the file;
- back up BOTH PostgreSQL and FileStorage__RootPath;
- have tenant admins enroll MFA (POST /api/auth/mfa/enroll) and set an
  e-signature PIN (POST /api/auth/signature-pin) before go-live;
- upgrade procedure: stop NTQAMS -> replace C:\apps\ntqams -> run the new
  migrations.sql -> start NTQAMS; for the SPA, replace the files under
  C:\inetpub\wwwroot\qams-ui.
```
