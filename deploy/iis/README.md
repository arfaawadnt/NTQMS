# Hosting NT.QMS on IIS so it survives reboots

This folder automates the topology already specified in [`../DEPLOY.md`](../DEPLOY.md)
(sections 1–4) and ADR-0001/0002/0007. It does not introduce a new architecture.

```
browser --HTTPS--> IIS site "NTQMS"                    <- TLS terminates here (ADR-0002)
                     static SPA + URL Rewrite/ARR
                     |  /api/* proxied over loopback
                     v
                   Kestrel, Windows service "NTQAMS"   <- start=auto (reboot survival)
                     |
                     v
                   PostgreSQL 17, role qams_app        <- least privilege (TENANT-004)
```

**Why this survives reboots and `scripts/dev-up.ps1` does not.** The dev scripts start
processes that belong to a user session. Here, two independent Windows services are set to
**Automatic**: `NTQAMS` (Kestrel) and `W3SVC` (IIS). Neither needs a logged-in user, and the
service is additionally configured to auto-restart if the process dies. `Verify-NTQMS-IIS.ps1`
asserts both start modes, so "it will come back" is a checked fact, not an assumption.

## Status on this machine (checked 2026-07-29)

| Prerequisite | State here | Who can install it |
| ------------ | ---------- | ------------------ |
| IIS (W3SVC) | **not installed** | you, elevated — or the script with `-EnableIisFeatures` |
| URL Rewrite 2.1 | **not installed** | you — [download](https://www.iis.net/downloads/microsoft/url-rewrite) |
| Application Request Routing | **not installed** | you — [download](https://www.iis.net/downloads/microsoft/application-request-routing) |
| Elevation | **not available to the agent** | you |
| .NET 9 publish | verified working | — |

Both IIS modules are Microsoft installers, and enabling Windows features plus creating
services requires administrator rights that the agent does not have. **The install itself is
therefore yours to run** — the scripts here make it one command plus one secret-setting step.

## Order of operations

1. **Database first — this is a hard blocker.** In Production the application *refuses to
   start* if its connection role owns the tables or holds SUPERUSER/BYPASSRLS, because either
   would void RLS tenant isolation and signed-record immutability (TENANT-004; the refusal was
   executed and verified as OQ-DEP-01, which reported *"connection role 'qams_app' owns 92
   application table(s)"* against the development database). A dev database will be rejected.
   ```
   psql -U postgres -f ..\db-init.sql                    # edit both passwords first
   psql -U qams_owner -d ntqams -f ..\migrations.sql     # DDL as the OWNER role
   psql -U postgres -d ntqams -f ..\harden-runtime-role.sql
   ```
2. **Install IIS + URL Rewrite + ARR** (table above). Without URL Rewrite/ARR every request
   returns `500.19`; the verify script names that cause explicitly.
3. **Run the installer from an elevated PowerShell:**
   ```powershell
   .\Install-NTQMS-IIS.ps1 -SiteHostName qms.lab.local -TargetRoot C:\apps\ntqams `
       -CertificateThumbprint <thumbprint-in-LocalMachine\My> -EnableIisFeatures
   ```
   It publishes the API and SPA, deploys `../web.config` with the proxy port rewritten,
   creates the `NTQAMS` service with `start=auto` and failure-restart actions, creates the app
   pool (no managed code, AlwaysRunning, no idle timeout, no periodic recycle) and the site
   with `serverAutoStart`, and sets least-privilege ACLs. Re-running it is safe.
4. **Set the secrets once** — the installer deliberately does not invent or store credentials.
   It prints the exact `New-ItemProperty ... -Name Environment` command for the service key,
   then you `Start-Service NTQAMS`. Required: connection string, `Jwt__Secret` (≥48 random
   chars), platform-admin bootstrap, `ASPNETCORE_ENVIRONMENT=Production`,
   `Database__MigrateOnStartup=false`.
5. **Verify, then reboot and verify again:**
   ```powershell
   .\Verify-NTQMS-IIS.ps1 -SiteHostName qms.lab.local
   ```
   The second run — after a real restart, starting nothing by hand — is the actual proof.

## Settings that matter for *this* application

- **`Database__MigrateOnStartup=false`.** The runtime role has no DDL rights, and a cold start
  with the database unreachable fails fast on migration by design (documented residual of
  OPS-010). Migrations run separately as `qams_owner`.
- **App pool: no idle timeout, no periodic recycle.** The app runs `BackgroundService` workers
  (outbox processor, scheduled sweeps, KPI snapshots). IIS defaults would let the front end go
  idle; the settings applied here keep the edge warm and avoid overlapped rotation.
- **One instance per database (ADR-0001).** Do not add a web garden or a second host against
  the same database — a second instance logs a contended-advisory-lock warning and may
  double-process background jobs.
- **TLS at the proxy (ADR-0002).** HSTS is emitted by the application outside Development
  (`max-age=63072000; includeSubDomains`, verified), but it is only meaningful once IIS serves
  HTTPS. Without `-CertificateThumbprint` the installer configures HTTP only and says so.
- **Same-origin, no CORS (ADR-0007).** The SPA and API share the site origin via the proxy
  rule; do not split them across origins without revisiting the ADR.

## Rollback

```powershell
Stop-Website NTQMS; Remove-Website NTQMS; Remove-WebAppPool NTQMS-web
Stop-Service NTQAMS; sc.exe delete NTQAMS
# then delete C:\apps\ntqams
```
The database is untouched by rollback. Restore from `../BACKUP-RESTORE-DR.md` if needed.

## Alternative if you do not want IIS on this box

`scripts/dev-up.ps1` does **not** survive reboots. If you want reboot survival without
installing IIS/ARR, the smaller step is the Kestrel Windows service on its own (`DEPLOY.md`
section 3) and reaching it directly on its port — you lose TLS termination, the static-file
host, and the security headers at the edge, so it suits an internal trial only.
