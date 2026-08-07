# Upgrade Checklist — NT.QMS → v1.53.0 (RISK-03 e-signature ceremony)

| Field | Value |
| ----- | ----- |
| Document ID | UPG-NTQMS-v1.53.0 |
| Target release | **v1.53.0** (git tag `v1.53.0` @ `20cb849`) |
| Package | `deploy/NT.QAMS-webapi-v1.53.0-win-x64.zip` (self-contained, no .NET runtime needed) + `deploy/NT.QAMS-frontend-v1.53.0-dist.zip` (Angular `browser/` dist) |
| Nature of upgrade | **Code + configuration only.** No new database migrations since v1.52.0 — a v1.52.x install needs **no schema change**. (Upgrading from **< v1.52.0**? apply migrations — see step 6.) |
| Mechanics reference | `deploy/DEPLOY.md` and `deploy/Deploy-FullStack.ps1` (IIS SPA + self-contained Kestrel Windows service on loopback). This checklist is the v1.53.0 delta over that runbook. |
| Rollback | Keep the previous folder + a pre-upgrade DB dump; rollback = stop, restore folders, start (details in step 10). |

> **Release-gate note (read before deploying to a regulated/production customer environment).**
> v1.53.0 is at posture **Pre-production / Approved-with-conditions**. Two conditions remain open —
> **DOC-001** (validation executed and **signed on a qualified environment**) and **SEC-001**
> (independent penetration test). Deploying the Part 11 e-signature ceremony to a live GxP environment
> ahead of signed validation is a compliance decision for the customer's QA/validation owner, not a
> routine push. Confirm sign-off before proceeding.

---

## 0. Pre-flight (do on a maintenance window)

- [ ] Confirm the customer/QA owner has authorised this deployment (see release-gate note).
- [ ] Record the **currently deployed version** (API `/health` or the app footer). Upgrade path:
      **v1.52.x → v1.53.0** = code+config only; **< v1.52.0** = also run migrations (step 6).
- [ ] Verify prerequisites unchanged: Windows Server x64, IIS site + app pool, PostgreSQL 17 reachable,
      the API's connection string / JWT secret / `PlatformAdmin` config still valid.
- [ ] Note current app-pool + service names and the install folders (API folder, SPA web root).

## 1. Backup (mandatory — this is a Part 11 system)

- [ ] **Database dump** (point-in-time restore anchor): `pg_dump` per `deploy/BACKUP-RESTORE-DR.md`.
- [ ] **File store** backup (`FileStorage__RootPath` / `data\files`) — content-addressed evidence.
- [ ] **Copy the existing API folder and SPA web root** to a timestamped `-preupgrade` location
      (this is the rollback image).

## 2. Stage the new package

- [ ] Copy both zips to the server and unblock/extract to a staging folder:
      - `NT.QAMS-webapi-v1.53.0-win-x64.zip` → `...\staging\api`
      - `NT.QAMS-frontend-v1.53.0-dist.zip` → `...\staging\web` (yields a `browser\` folder)
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

## 6. Database migrations — conditional

- [ ] **From v1.52.x:** nothing to do — v1.53.0 (RISK-03) added **no migrations**.
- [ ] **From < v1.52.0:** apply schema changes by **either** starting the API once with
      `Database__MigrateOnStartup=true`, **or** running the bundled idempotent script
      `deploy/migrations.sql` (regenerate with
      `dotnet ef migrations script --idempotent` if unsure it is current). Idempotent — safe to re-run.
- [ ] `deploy/harden-runtime-role.sql` only needs re-running if the DB runtime role was rebuilt.

## 7. Start the app

- [ ] Start the API service; confirm `GET /health/ready` → **200** (readiness proves PostgreSQL reachable;
      **503** = DB unreachable/credentials).
- [ ] Start the IIS site / app pool; load the SPA sign-in page.
- [ ] Sign in as a tenant admin to confirm end-to-end auth.

## 8. Grant the new `.sign` permission keys (the substantive v1.53.0 action)

RISK-03 requires an electronic signature on every regulated sign-off gate. Because role seeding is
additive per role name, **existing tenants' roles do not gain the new keys automatically.** Follow
`deploy/RELEASE-NOTE-RISK-03-SIGNING-KEYS.md` for the full table. Per tenant:

- [ ] Grant the **four new keys** to Quality Manager, Tenant Administrator, and any custom signer role:
      `proficiency-testing.sign`, `suppliers.sign`, `conflicts.sign`, `compliance.sign`.
- [ ] For any **custom** role that held only the *old* action, grant the tightened `.sign` key
      (`nc.sign`, `analytical-quality.sign`, `quality-policy.sign`, `changes.sign`, `reviews.sign`,
      `competencies.sign`, `test-authorizations.sign`, `access-reviews.sign`).
- [ ] Do **not** grant `.sign` to read-only (External Auditor) or record-only (Analyst) roles.
- [ ] New keys take effect on the signer's next request (permissions resolve per request — no re-login).

## 9. Post-upgrade verification (smoke test)

- [ ] `GET /health/ready` → 200; SPA loads and authenticates.
- [ ] **Signing works:** on any signed-record gate, a role **without** the key → **403 AUTHZ-403**
      (confirms the key is enforced); after granting, a correct password + PIN completes the action and
      the signature appears in the record's manifest and the compliance signature log.
- [ ] **Ceremony fences:** a wrong PIN → **422 SIG-001** (nothing minted); missing password/PIN → **400**.
- [ ] Spot-check one manifest renders on an AQ study / PtPlan page and on a periodic-review completion.
- [ ] Audit-trail hash-chain verification endpoint still reports an intact chain.
- [ ] Record the deployed version and the smoke-test result in the site's deployment log.

## 10. Rollback (if verification fails)

- [ ] Stop the API service and IIS site.
- [ ] Restore the `-preupgrade` API folder and SPA web root.
- [ ] **Only if step 6 applied migrations** (i.e., upgrading from < v1.52.0): restore the pre-upgrade
      DB dump. From v1.52.x there was no schema change, so no DB restore is needed to roll back.
- [ ] Start the previous version; confirm `/health/ready` → 200 and sign-in.
- [ ] Note the failure and rollback in the deployment log.

---

## Notes

- The permission grants (step 8) are the only functional action unique to v1.53.0; everything else is a
  standard file-swap upgrade over v1.52.x.
- No CHANGELOG.md in the repo — the change record is `IMPLEMENTATION_LOG.md` (RISK-03 close-out entry)
  and `docs/validation/06-…` §A.13–A.19 (URS-123–128).
- This checklist covers deployment mechanics only. It does **not** substitute for the QA-owned
  validation execution and signatures (URS-123–128 / OQ) that close DOC-001.
