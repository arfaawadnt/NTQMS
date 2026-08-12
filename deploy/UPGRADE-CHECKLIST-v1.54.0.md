# Upgrade Checklist — NT.QMS → v1.54.0 (Product enhancement program)

| Field | Value |
| ----- | ----- |
| Document ID | UPG-NTQMS-v1.54.0 |
| Target release | **v1.54.0** (git tag `v1.54.0` @ `ecbba17`) |
| Package | `deploy/NT.QAMS-webapi-v1.54.0-win-x64.zip` (self-contained, no .NET runtime needed) + `deploy/NT.QAMS-frontend-v1.54.0-dist.zip` (Angular `browser/` dist) |
| Nature of upgrade | **Code + two additive database migrations.** Unlike v1.53.x, this release **changes the schema** (step 6). **No new permission keys** — no role grants required (step 8). |
| Mechanics reference | `deploy/DEPLOY.md` and `deploy/Deploy-FullStack.ps1` (IIS SPA + self-contained Kestrel Windows service on loopback). This checklist is the v1.54.0 delta over that runbook; feature/deploy detail in `deploy/RELEASE-NOTE-PRODUCT-ENHANCEMENTS.md`. |
| Rollback | Keep the previous folder + a pre-upgrade DB dump. The two migrations are **additive and backward-compatible** (a nullable column + a new table), so a **code-only rollback to v1.53.x runs against the v1.54.0 schema** — a DB restore is only needed to fully revert the schema (step 10). |

> **Release-gate note (read before deploying to a regulated/production customer environment).**
> v1.54.0 is at posture **Pre-production / Approved-with-conditions**. Two conditions remain open —
> **DOC-001** (validation executed and **signed on a qualified environment**) and **SEC-001**
> (independent penetration test). The URS-129…134 validation records ship as Template/unsigned and
> fold under DOC-001. Deploying to a live GxP environment ahead of signed validation is a compliance
> decision for the customer's QA/validation owner — confirm sign-off before proceeding.

---

## 0. Pre-flight (do on a maintenance window)

- [ ] Confirm the customer/QA owner has authorised this deployment (see release-gate note).
- [ ] Record the **currently deployed version** (API `/health` or the app footer). Upgrade path:
      **v1.53.x → v1.54.0** = code + the two migrations (step 6); **< v1.52.0** = the same idempotent
      script also carries every earlier migration.
- [ ] Verify prerequisites unchanged: Windows Server x64, IIS site + app pool, PostgreSQL 17 reachable,
      the API's connection string / JWT secret / `PlatformAdmin` config still valid.
- [ ] Confirm you can apply DDL **as `qams_owner`** (owner role or a superuser) — step 6 needs it.
- [ ] Note current app-pool + service names and the install folders (API folder, SPA web root).

## 1. Backup (mandatory — this is a Part 11 system, and this release changes the schema)

- [ ] **Database dump** (point-in-time restore anchor): `pg_dump` per `deploy/BACKUP-RESTORE-DR.md`.
      This is the rollback anchor for the schema change — do not skip it on this release.
- [ ] **File store** backup (`FileStorage__RootPath` / `data\files`) — content-addressed evidence.
- [ ] **Copy the existing API folder and SPA web root** to a timestamped `-preupgrade` location
      (the code rollback image).

## 2. Stage the new package

- [ ] Copy both zips to the server and unblock/extract to a staging folder:
      - `NT.QAMS-webapi-v1.54.0-win-x64.zip` → `...\staging\api`
      - `NT.QAMS-frontend-v1.54.0-dist.zip` → `...\staging\web` (yields a `browser\` folder)
- [ ] Confirm `staging\api\NT.QAMS.WebApi.exe` and `staging\web\browser\index.html` exist.

## 3. Stop the running app

- [ ] Stop the API Windows service (or `Deploy-FullStack.ps1` service name).
- [ ] Stop the IIS site / app pool serving the SPA.
- [ ] Confirm the API port (loopback) is released.

## 4. Deploy the backend

- [ ] Replace the API folder contents with `staging\api\*`. **Preserve** the server's own
      `appsettings.Production.json` / environment config and the `data\` folder — do not overwrite
      real connection strings, secrets, or the file store with the package's placeholder
      `appsettings.json`.
- [ ] Re-apply `FileStorage__RootPath` and any site-specific settings if the folder was replaced wholesale.

## 5. Deploy the frontend

- [ ] Replace the SPA web root with the contents of `staging\web\browser\*`
      (the site root should contain `index.html` at top level).
- [ ] Confirm the SPA `environment` API base (`/api`) matches the reverse-proxy path (unchanged from prior release).

## 6. Database migrations — REQUIRED (the substantive v1.54.0 action)

Two additive migrations ship: `AddNcReopenReason` (nullable `nonconformance.reopen_reason`) and
`AddTenantMailSettings` (new tenant table `qams.tenant_mail_settings` with **FORCE RLS**).

- [ ] Apply **as `qams_owner`** (not the runtime role `qams_app` — DDL as the runtime role fails or
      trips the TENANT-004 start-up guard) by **either**:
      - starting the API once with `Database__MigrateOnStartup=true` (runs as the owner on the service path), **or**
      - `psql -f deploy/migrations.sql` **as `qams_owner`** (idempotent — safe to re-run; regenerate with
        `dotnet ef migrations script --idempotent` if unsure it is current).
- [ ] **No `harden-runtime-role.sql` re-run is needed.** That script's `ALTER DEFAULT PRIVILEGES FOR
      ROLE qams_owner … GRANT … TO qams_app` already grants the runtime role on any table `qams_owner`
      creates — so the new `tenant_mail_settings` table is granted automatically. Re-run it **only** if
      the migrations were applied by a different owner.
- [ ] **Verify RLS on the new table** (do not read this from the migration source):
      `SELECT relrowsecurity, relforcerowsecurity FROM pg_class WHERE relname='tenant_mail_settings';`
      → `t | t`, and a `tenant_isolation` row exists in `pg_policies` for it.
- [ ] Confirm both migration ids appear in `__EFMigrationsHistory`.

## 7. Start the app

- [ ] Start the API service; confirm `GET /health/ready` → **200** (readiness proves PostgreSQL reachable;
      **503** = DB unreachable/credentials).
- [ ] Start the IIS site / app pool; load the SPA sign-in page.
- [ ] Sign in as a tenant admin to confirm end-to-end auth.

## 8. Permissions — no action required

Unlike v1.53.0, this release introduces **no new permission keys**. Every feature reuses an existing
key, so roles that already hold it gain the capability on deploy — nothing to grant:

- [ ] (No grants.) For reference: NC re-open uses `nc.sign`; Quality Analytics export uses
      `reports.export`; Mail Management uses `notifications.manage`; the User Manual PDF and the My-Tasks
      action centre are available to any authenticated user. Custom roles behave per their existing grants.

## 9. Mail Management — optional configuration (to enable branded e-mail)

Both parts are optional; e-mail delivery is unchanged unless configured.

- [ ] **Transport (server, once):** the SMTP relay stays in server config (`Smtp:Host/Port/Ssl/User/Password`).
      With no `Smtp:Host`, e-mail is a logged no-op and the in-app feed still delivers — unchanged.
- [ ] **Sender identity (per tenant, optional):** a Tenant Administrator opens **Mail Management** and sets
      the From name/address, reply-to, brand accent and footer, and the enable switch. No SMTP credentials
      are entered here. With no row configured, notifications use the server-default sender.

## 10. Post-upgrade verification (smoke test)

- [ ] `GET /health/ready` → 200; SPA loads and authenticates.
- [ ] **NC re-open:** as a `nc.sign` holder, open a **closed** NC → a "Re-open" action prompts for a reason
      + PIN; a wrong PIN → **422 SIG-001** (nothing minted); a correct one re-opens it to the action-plan
      stage and adds a signature to the record's manifest.
- [ ] **Quality Analytics report:** on the dashboard, Export PDF and Export Excel each return a file (200).
- [ ] **User Manual:** `/manual` "Export PDF Manual" returns a PDF.
- [ ] **My Tasks:** `/tasks` renders the grouped action centre.
- [ ] **Mail Management:** `/mail-management` saves a sender identity (`PUT → 204`) that persists across a
      reload; with an SMTP relay configured, a triggered Mail notification arrives as branded HTML.
- [ ] Audit-trail hash-chain verification endpoint still reports an intact chain.
- [ ] Record the deployed version and the smoke-test result in the site's deployment log.

## 11. Rollback (if verification fails)

- [ ] Stop the API service and IIS site.
- [ ] Restore the `-preupgrade` API folder and SPA web root (revert to v1.53.x).
- [ ] **DB:** the two migrations are additive and backward-compatible — v1.53.x runs against the
      v1.54.0 schema (it ignores the extra nullable column and the unused table), so a **code-only
      rollback is safe**. Restore the pre-upgrade DB dump **only** if you must fully revert the schema
      (e.g. compliance requires the exact prior state); note that `tenant_mail_settings` rows and any
      `reopen_reason` values written post-upgrade are lost on a DB restore.
- [ ] Start the previous version; confirm `/health/ready` → 200 and sign-in.
- [ ] Note the failure and rollback in the deployment log.

---

## Notes

- The **two migrations (step 6)** are the only infrastructure action unique to v1.54.0; there are **no
  permission grants** (contrast v1.53.0). Everything else is a standard file-swap upgrade over v1.53.x.
- Change record: `IMPLEMENTATION_LOG.md` ("Product enhancement program" entry) and
  `docs/validation/06-Revalidation-Delta-v1.38-v1.50.md` **URS-129…134**; feature-by-feature deploy
  notes in `deploy/RELEASE-NOTE-PRODUCT-ENHANCEMENTS.md`.
- This checklist covers deployment mechanics only. It does **not** substitute for the QA-owned
  validation execution and signatures (URS-129…134 / OQ records 15–19) that fold under DOC-001.
