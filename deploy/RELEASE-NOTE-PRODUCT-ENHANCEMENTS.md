# Release Note — Product Enhancement Program (URS-129…134)

**Action required for existing tenants:** apply the two database migrations (§1). **No role/permission
grant is required** — unlike the RISK-03 release, this program adds **no new permission keys** (§3).
Optional post-deploy configuration enables branded e-mail (§4).

| Field | Value |
| ----- | ----- |
| Document ID | RN-NTQMS-ENH-001 |
| Applies to | The product-enhancement program on `dev` (commit **`b8259ee`**), on top of v1.53.1 — proposed tag **v1.54.0** (not yet tagged) |
| Audience | Whoever runs the install/upgrade (§1–§2); Tenant Administrators (§4, optional) |
| Type | **Schema migration required** (two additive migrations) + new API/UI surface. No new permission key; no automatic data migration. |

## What changed

Five backlog features, each traced in `docs/validation/06-Revalidation-Delta-v1.38-v1.50.md`
(URS-129…134) with an OQ execution record (docs 15–19):

| # | Feature | Surface added |
| - | ------- | ------------- |
| URS-129 | **NC re-open** — a closed nonconformance can be re-opened for further corrective action, requiring a documented reason and the actor's e-signature (password + PIN). Returns the NC to the action-plan stage. | `POST /api/nonconformances/{id}/reopen`; column `nonconformance.reopen_reason` |
| URS-130 | **Quality Analytics report** — the dashboard exports a branded PDF (health-score gauge, weighted-component bars, Pareto, 5×5 risk matrix) and a multi-sheet Excel workbook. | `GET /api/exports/quality-analytics.pdf` and `.xlsx` |
| URS-131 | **User Manual PDF** — the manual exports a professional PDF (cover chart, linked table of contents, per-topic workflow progress bars). | `POST /api/exports/manual.pdf` |
| URS-132 | **My Tasks unified action centre** — one live feed of every pending action awaiting the user (tasks, NC/CAPA, NC sign-offs, risk treatments, objectives, review participation). | `GET /api/tasks/my-actions`; reworked `/tasks` page |
| URS-133/134 | **Mail Management + HTML e-mail** — a per-tenant mail *sender identity* (from name/address, reply-to, on/off, brand accent, footer) and a branded HTML e-mail template for notifications. **SMTP transport credentials are not stored in the tenant database** — the relay stays in server configuration. | `GET`/`PUT /api/notifications/mail-settings`; new `/mail-management` page; table `qams.tenant_mail_settings` |

## 1. Database migrations — required

Two additive migrations ship with this release:

| Migration | Effect |
| --------- | ------ |
| `20260808073533_AddNcReopenReason` | Adds nullable `reopen_reason text` to `qams.nonconformance`. No backfill. |
| `20260808152142_AddTenantMailSettings` | Creates `qams.tenant_mail_settings` (new tenant-scoped table) with **FORCE row-level security + `tenant_isolation` policy** and a `#RRGGBB` CHECK on `brand_color`. |

- **Apply as `qams_owner`, not `qams_app`** (the v1.53.1 rule). Applying DDL as the runtime role
  fails or trips the TENANT-004 start-up guard. `MigrateOnStartup` runs as the owner on the normal
  service path; a manual apply is `psql -f deploy/migrations.sql` (regenerated for this release) or
  `dotnet ef database update` **as `qams_owner`**.
- **No `harden-runtime-role.sql` re-run is needed** for the new table. `harden-runtime-role.sql`
  already sets `ALTER DEFAULT PRIVILEGES FOR ROLE qams_owner … GRANT SELECT, INSERT, UPDATE …
  TO qams_app`, so a table created by `qams_owner` (as above) grants the runtime role automatically.
  If — and only if — the migration was applied by a different owner, run `harden-runtime-role.sql`
  as a superuser afterward.
- **RLS is in the migration** (not a separate step). Verify after apply:
  `SELECT relrowsecurity, relforcerowsecurity FROM pg_class WHERE relname='tenant_mail_settings';`
  → `t | t`, and a `tenant_isolation` row in `pg_policies` for the table.

## 2. Application artifacts — deploy both tiers

The features add both server endpoints and SPA pages, so **deploy the WebApi and the frontend
together** — a frontend-only or backend-only deploy leaves the new page or its endpoints missing.
The API-surface snapshot (`ApiSurface.approved.txt`) was updated for every new route.

## 3. Permissions — no action required

Unlike RISK-03, this program introduces **no new permission keys**. Every capability reuses an
existing key, so roles that already hold it gain the feature on deploy — nothing to grant:

| Capability | Existing key that gates it | Who already holds it (default roles) |
| ---------- | -------------------------- | ------------------------------------ |
| Re-open a closed NC | `nc.sign` | Quality Manager, Tenant Administrator |
| Export the Quality Analytics report | `reports.export` | Quality Manager, Tenant Administrator, Department Head, Analyst |
| Export the User Manual PDF | *(authenticated — no key)* | any signed-in user |
| My Tasks action centre | *(authenticated — no key)* | any signed-in user |
| Configure Mail Management | `notifications.manage` | Quality Manager, Tenant Administrator |

Custom roles behave per their existing grants: a custom role that can already sign NCs
(`nc.sign`) can re-open them; one with `notifications.manage` can configure mail; and so on.

## 4. Mail configuration — optional, to enable branded e-mail

E-mail delivery is unchanged unless configured; both parts below are optional and safe to defer.

1. **Transport (server, once):** the SMTP relay is configured in server settings exactly as before
   (`Smtp:Host`, `Smtp:Port`, `Smtp:Ssl`, `Smtp:User`, `Smtp:Password`). With no `Smtp:Host`, e-mail
   is a logged no-op and the in-app feed still delivers — unchanged behaviour.
2. **Sender identity (per tenant, optional):** a Tenant Administrator opens **Mail Management** and
   sets the From name/address, reply-to, brand accent and footer, and the enable switch. With no row
   configured, notifications use the server-default sender (unchanged). Setting *enabled = false*
   suppresses e-mail for that tenant while the in-app feed keeps delivering.

No credentials are entered or stored on the Mail Management page — only the sender identity and
branding; the relay stays in server configuration.

## Verification (smoke, post-deploy)

1. **Migrations:** the two migrations appear in `__EFMigrationsHistory`; `tenant_mail_settings` is
   `t|t` for RLS with a `tenant_isolation` policy.
2. **NC re-open:** as a `nc.sign` holder, open a closed NC → a "Re-open" action prompts for a reason
   + PIN; a wrong PIN returns **422 SIG-001** and mints nothing; a correct one re-opens it to the
   action-plan stage and adds a signature to the manifest.
3. **Analytics report:** on the Quality Analytics dashboard, Export PDF / Excel return **200** with
   `application/pdf` / the spreadsheet content-type.
4. **User Manual:** the `/manual` "Export PDF Manual" button returns a PDF.
5. **My Tasks:** `/tasks` renders the grouped action feed; a task assigned to the caller appears.
6. **Mail Management:** `/mail-management` saves a sender identity (`PUT → 204`) that persists across
   a reload; with an SMTP relay configured, a triggered Mail notification arrives as branded HTML.

## Does not change

Overall release posture is unchanged — **Pre-production / Approved-with-conditions**. This program
does not affect the two open release blockers, **DOC-001** (signed validation in a qualified
environment) and **SEC-001** (independent penetration test); the URS-129…134 validation-record
execution and signatures fold under DOC-001. The new `tenant_mail_settings` table is tenant-isolated
by FORCE RLS like every other tenant table, and the Mail Management design deliberately keeps SMTP
transport credentials in server configuration (no reversible secret persisted in the tenant
database) — the SEC-001 posture decision.

## Reference

Engineering detail and traceability: `docs/validation/06-Revalidation-Delta-v1.38-v1.50.md`
**URS-129…134**; OQ execution records `docs/validation/15…19-OQ-Execution-Record-*.md`;
full-suite counts in `docs/validation/verification-log.md` (backend 515 / frontend 95, real
PostgreSQL). Commit **`b8259ee`** on `dev`. Deployment script: `deploy/migrations.sql` (regenerated).
