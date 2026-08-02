# Windows Server Full-Stack Deploy Prompt (FROM SOURCE) — NT.QAMS v1.52.0

Builds **from source** on a build machine, then installs on a Windows Server:
- **Backend API** — self-contained ASP.NET Core (Kestrel) as a Windows service on `127.0.0.1:5000` (loopback only).
- **Frontend SPA** — Angular 22 static files served by **IIS on 443 (TLS)**; IIS also reverse-proxies `/api` and `/health` to the backend, so the SPA is same-origin (no CORS, backend never network-exposed).
- **Database** — PostgreSQL 17, with the owner/runtime role split.

There are **two roles** in this runbook: a **BUILD MACHINE** (has the .NET 9 SDK + Node 24) that produces the artifacts, and the **TARGET SERVER** (needs no SDK — the backend is self-contained). They can be the same machine.

Paste everything inside the fence below into your deployment agent (Claude / Antigravity). Run Part 0 on the build machine; run Parts A–D on the target server.

---

```
You are deploying NT.QAMS (ASP.NET Core API + Angular 22 SPA) to a Windows Server,
BUILDING FROM SOURCE. You are an operations agent: follow this runbook exactly,
verify every step, and STOP and report if any verification fails. Do not improvise
alternative architectures, do not edit application code, do not skip verification.

TARGET TOPOLOGY
- Backend: self-contained Kestrel Windows service on http://127.0.0.1:5000 (loopback).
- Frontend: static Angular files served by IIS on 443 (TLS). IIS reverse-proxies
  /api and /health to the backend. SPA calls same-origin /api -> no CORS.
- Only IIS (443) faces the network; the API is never directly exposed.
- Exactly ONE API instance per database (ADR-0001). Never run 2+ replicas.

HARD RULES
1. Backend binds 127.0.0.1 only. Never expose port 5000 to the network.
2. Generate ALL secrets fresh; never reuse examples or dev values. Record what you
   generate in C:\install\SECRETS-README.txt (ACL: Administrators only) and tell the
   operator to move them to a vault and delete the file.
3. Secrets live in machine-scope environment variables only — never in appsettings.json
   or any file under the web root.
4. On any command error or unexpected verification result, STOP and produce a
   diagnosis report instead of continuing.

=================================================================================
=== PART 0 — BUILD & PUBLISH FROM SOURCE  (run on the BUILD MACHINE) ===
=================================================================================

Prereqs on the build machine:
- .NET 9 SDK (verify: dotnet --version -> 9.x). Install EF tools: dotnet tool install --global dotnet-ef
- Node.js >= 20.19 (Node 24 recommended; Angular 22 will not build on older Node).
- Git, and network access to restore NuGet + npm packages.

0.1 Get the source
    git clone https://github.com/arfaawadnt/NTQMS.git NT.QAMS
    cd NT.QAMS
    git checkout master        # or the specific release tag for v1.52.0

0.2 Publish the backend — self-contained win-x64 (no runtime needed on the server)
    dotnet restore NT.QAMS.sln
    dotnet publish src/NT.QAMS.WebApi/NT.QAMS.WebApi.csproj `
      -c Release -r win-x64 --self-contained true `
      -o C:\build\ntqams-api
    Verify: C:\build\ntqams-api\NT.QAMS.WebApi.exe exists.

0.3 Generate a FRESH idempotent migration script (do NOT use deploy/migrations.sql —
    it is stale). This produces the full schema up to the current release:
    dotnet ef migrations script --idempotent `
      --project src/NT.QAMS.Infrastructure `
      --startup-project src/NT.QAMS.WebApi `
      -o C:\build\ntqams-api\migrations.sql
    Also copy the two static DB scripts alongside it:
    copy deploy\db-init.sql            C:\build\ntqams-api\
    copy deploy\harden-runtime-role.sql C:\build\ntqams-api\

0.4 Build the Angular SPA (production)
    cd frontend
    npm ci
    node node_modules/@angular/cli/bin/ng.js build --configuration production
    The output is at frontend\dist\nt-qams-frontend\browser\  (index.html + assets).
    That BROWSER folder is the IIS web root content. cd ..

0.5 Author the IIS web.config (the repo does not ship one). Write this file to
    C:\build\ntqams-ui\web.config  and copy the contents of
    frontend\dist\nt-qams-frontend\browser\  into C:\build\ntqams-ui\  next to it:

    <?xml version="1.0" encoding="utf-8"?>
    <configuration>
      <system.webServer>
        <rewrite>
          <rules>
            <!-- Reverse-proxy API + health to the loopback Kestrel service -->
            <rule name="proxy-api" stopProcessing="true">
              <match url="^(api|health)(/.*)?$" />
              <action type="Rewrite" url="http://127.0.0.1:5000/{R:0}" />
            </rule>
            <!-- SPA fallback: anything not a real file goes to index.html -->
            <rule name="spa-fallback" stopProcessing="true">
              <match url=".*" />
              <conditions logicalGrouping="MatchAll">
                <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
                <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
              </conditions>
              <action type="Rewrite" url="/index.html" />
            </rule>
          </rules>
        </rewrite>
      </system.webServer>
    </configuration>

0.6 Hand-off. Copy to the target server (e.g. into C:\install\):
    - C:\build\ntqams-api\   (backend exe + migrations.sql + db-init.sql + harden-runtime-role.sql)
    - C:\build\ntqams-ui\    (index.html, assets, web.config)

=================================================================================
=== PART A — BACKEND API  (run on the TARGET SERVER) ===
=================================================================================

A1. Preflight
- [Environment]::Is64BitOperatingSystem must be True.
- PostgreSQL 17 reachable: local service "postgresql*17*" running, or the operator's
  DBHOST answers on 5432. If none exists and the operator authorized it, install
  PostgreSQL 17 (official EDB installer) and generate a strong superuser password.
- Port 5000 free on loopback (Get-NetTCPConnection -LocalPort 5000 -EA SilentlyContinue).

A2. Place the backend
- Copy C:\install\ntqams-api\  ->  C:\apps\ntqams\  so C:\apps\ntqams\NT.QAMS.WebApi.exe exists.
- Create the evidence-file data dir on a data disk, e.g. D:\ntqams-data\files.

A3. Database (owner/runtime role split — do NOT collapse into one role)
- Edit a COPY of db-init.sql: replace the CHANGE_ME placeholders with two generated
  passwords (record both) — one for qams_owner (DDL) and one for qams_app (runtime).
  Run once as the postgres superuser:
      psql -U postgres -f <edited db-init.sql>
  (If the roles/database already exist from a prior install, skip creation — do not drop.)
- Apply the schema AS THE OWNER (idempotent; safe to re-run on every upgrade):
      psql -U qams_owner -d ntqams -f C:\apps\ntqams\migrations.sql
- Apply least-privilege runtime grants:
      psql -U postgres -d ntqams -f C:\apps\ntqams\harden-runtime-role.sql
  The app connects as qams_app (NOSUPERUSER, NOBYPASSRLS, no DDL). It REFUSES to start
  in Production if its role is superuser / has BYPASSRLS / owns the tables — that would
  void tenant RLS isolation and signed-record immutability.

A4. Backend configuration (MACHINE-scope environment variables)
Generate: JWT_SECRET (>=48 random alphanumerics), PLATFORM_ADMIN_PASSWORD (>=20 chars).
  ConnectionStrings__Postgres = Host=<DBHOST|localhost>;Port=5432;Database=ntqams;Username=qams_app;Password=<qams_app pwd>
  Jwt__Secret                 = <JWT_SECRET>            # app refuses to start without it
  PlatformAdmin__Email        = admin@<company-domain>  # ask operator
  PlatformAdmin__Password     = <PLATFORM_ADMIN_PASSWORD>
  ASPNETCORE_URLS             = http://127.0.0.1:5000
  ASPNETCORE_ENVIRONMENT      = Production
  Database__MigrateOnStartup  = false                   # schema applied via migrations.sql as qams_owner
  FileStorage__RootPath       = D:\ntqams-data\files
  # Optional email (in-app notifications work without it): Smtp__Host, Smtp__Port,
  #   Smtp__User, Smtp__Password, Smtp__From
Set them machine-wide, e.g.:
  [Environment]::SetEnvironmentVariable('Jwt__Secret','<...>','Machine')   # repeat per var

A5. Backend service
    sc.exe create NTQAMS binPath= "C:\apps\ntqams\NT.QAMS.WebApi.exe" start= auto
    sc.exe start NTQAMS
- Confirm it reaches RUNNING and stays up 30s. If it exits immediately, run the exe in a
  console to read the startup error (usually missing Jwt__Secret, an unreachable DB, or a
  too-privileged DB role), fix, retry once.

A6. Backend smoke (loopback)
- curl http://127.0.0.1:5000/health/live                    -> 200 Healthy
- curl http://127.0.0.1:5000/health/ready                   -> 200 Healthy (503 if DB down)
- curl -i http://127.0.0.1:5000/api/tenants  (no token)     -> 401  (deny-by-default)
- Platform-admin login returns a token:
    POST http://127.0.0.1:5000/api/auth/login
      {"email":"<PlatformAdmin__Email>","password":"<...>"}  -> 200  (save TOKEN)

=================================================================================
=== PART B — FRONTEND SPA (IIS)  (run on the TARGET SERVER) ===
=================================================================================

B1. IIS prerequisites
- IIS installed WITH the URL Rewrite module AND Application Request Routing (ARR).
  If missing and the operator authorized it, install them (standalone MSIs). Enable
  the ARR proxy (required for the /api rewrite to forward):
    Import-Module WebAdministration
    Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
      -filter "system.webServer/proxy" -name "enabled" -value "True"

B2. Deploy the SPA
- Create C:\inetpub\wwwroot\qams-ui and copy the contents of C:\install\ntqams-ui\ into it,
  so index.html and web.config sit at the site root.
- If the backend listens on a host/port other than 127.0.0.1:5000, update the rewrite
  target in web.config to match.

B3. IIS site + TLS
- Create (or repoint) an IIS site whose physical path is C:\inetpub\wwwroot\qams-ui.
- App pool: "No Managed Code" (static files + proxy; .NET runs as the separate Kestrel service).
- Bind HTTPS on 443 with the OPERATOR-PROVIDED certificate (ask for the cert / friendly
  name; do NOT generate a public cert yourself). Bind HTTP:80 only to redirect to HTTPS.

=================================================================================
=== PART C — FULL-STACK VERIFICATION  (all must pass) ===
=================================================================================

C1. curl -k https://localhost/health/ready                  -> 200 Healthy     (proxied to Kestrel)
C2. curl -k https://localhost/                               -> 200 HTML containing "<qams-root"
C3. curl -k https://localhost/nonconformances                -> 200 index.html  (SPA fallback works)
C4. curl -k https://localhost/api/tenants  (no token)        -> 401             (proxy + deny-by-default)
C5. Provision a tenant via the proxied API (Bearer = platform-admin TOKEN from A6):
      POST https://localhost/api/tenants
        {"identifier":"<slug>","name":"<Lab>","adminEmail":"<op-provided>",
         "adminDisplayName":"<name>","adminPassword":"<generate>"}   -> 201
C6. Tenant login through the proxy:
      POST https://localhost/api/auth/login
        {"tenantIdentifier":"<slug>","email":"<adminEmail>","password":"<...>"} -> 200
C7. Open https://<server-hostname>/ in a browser: the sign-in page renders; log in as the
    tenant admin; the dashboard loads its KPI cards. (If no browser on the server, confirm
    C1-C6 and note the UI must be checked from a client machine.)

=================================================================================
=== PART D — REPORT ===
=================================================================================
Produce a deployment report: server name; backend path + service state; database host;
file-storage path; IIS site name + binding + cert; which checks (A6, C1-C7) passed with
actual responses; where secrets were recorded; any deviations. Remind the operator to:
- move secrets from SECRETS-README.txt into a vault, then delete the file;
- back up BOTH PostgreSQL and FileStorage__RootPath (see deploy/BACKUP-RESTORE-DR.md);
- have tenant admins enroll MFA and set an e-signature PIN from the app (My-account drawer)
  before go-live;
- upgrade procedure: on the build machine re-run Part 0 for the new release; on the server
  stop NTQAMS -> replace C:\apps\ntqams -> run the new migrations.sql as qams_owner ->
  re-run harden-runtime-role.sql -> start NTQAMS; for the SPA, replace the files under
  C:\inetpub\wwwroot\qams-ui.
```
