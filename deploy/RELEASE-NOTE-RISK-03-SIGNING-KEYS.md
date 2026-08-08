# Release Note — Part 11 Electronic-Signature Ceremony (RISK-03)

**Action required for existing tenants before this release is used in production.**

| Field | Value |
| ----- | ----- |
| Document ID | RN-NTQMS-RISK03-001 |
| Applies to | The RISK-03 e-signature close-out (on `dev` through commit `6b60386`) |
| Audience | Tenant Administrators / whoever maintains role privileges |
| Type | Post-deploy configuration — **no data is migrated automatically** |

## What changed

The 21 CFR Part 11 electronic-signature ceremony (account **password + signature PIN**,
§11.200(a)(1)) was previously required only when publishing a controlled document. It is now
required on **every regulated sign-off gate** — each mints an immutable signature record bound to
the record and its outcome (§11.50/§11.70).

## Why action is needed

Role seeding is **additive and idempotent per role name**. That has two consequences for tenants
that already exist:

1. **Four brand-new permission keys** did not exist when your roles were seeded, so **no existing
   role holds them** — not even Tenant Administrator or Quality Manager. Any role that performs
   these actions is denied (HTTP **403 AUTHZ-403**) until the key is granted.
2. **Twelve endpoints were tightened** from their old action to the module's `.sign` action. For
   most of these the `.sign` key already existed and Quality Manager / Tenant Administrator already
   hold it — so **only custom roles** that were granted the *old* action (e.g. `approve`) without
   `.sign` are affected.

Tenants created **after** this release are unaffected: Tenant Administrator picks the keys up via
its all-keys grant and Quality Manager via its default rule.

## 1. New keys — grant to every signing role on every existing tenant

No existing role has these. Grant to **Quality Manager**, **Tenant Administrator**, and any custom
role expected to sign. Do **not** grant to read-only roles (e.g. External Auditor) or to
record-only roles (Analyst) — those are deliberately excluded.

| Permission key | Gate it now guards |
| -------------- | ------------------ |
| `proficiency-testing.sign` | Approve a PT/EQA plan |
| `suppliers.sign` | Approve a supplier |
| `conflicts.sign` | Assess a conflict of interest |
| `compliance.sign` | Complete a periodic audit-trail review |

## 2. Tightened endpoints — check custom roles

The `.sign` key here already existed (QM / Tenant Admin already hold it). Grant the `.sign` key to
any **custom** role that held only the old action.

| Gate | Old permission | Now requires |
| ---- | -------------- | ------------ |
| NC verify | `nc.approve` | `nc.sign` |
| NC confirm-effectiveness (close) | `nc.approve` | `nc.sign` |
| Uncertainty-budget approve | `analytical-quality.approve` | `analytical-quality.sign` |
| Quality-policy approve | `quality-policy.approve` | `quality-policy.sign` |
| Change-control approve | `changes.approve` | `changes.sign` |
| Management-review close | `reviews.void` | `reviews.sign` |
| Competency authorize | `competencies.approve` | `competencies.sign` |
| Test-authorization grant | `test-authorizations.create` | `test-authorizations.sign` |
| Access-review complete | *(none — was ungated at action level)* | `access-reviews.sign` |

*(Supplier approve, conflict assess and audit-trail-review complete were also tightened, but their
new keys are the brand-new ones in §1.)*

## How to grant

- **UI:** Administration → Roles → edit the role → enable the `.sign` permission(s) above → save.
  Permissions are resolved per request, so the change takes effect on the signer's next action —
  no re-login required.
- **API:** `PUT /api/roles/{id}/permissions` with the role's full key set **including** the new
  `.sign` keys (the endpoint replaces the set, so send the complete list, not a delta).
- **No data migration ships with this release, by design.** Which custom roles may sign is a tenant
  policy decision, not a system default; if a tenant wants a bulk grant, author a deliberate,
  reviewed data migration for that tenant's roles.

## Verification

1. As a role **without** the key: attempt the gate → expect **403 AUTHZ-403** (confirms the key is
   wired and enforced — *not* `AUTHZ-008`, which would mean the key is missing from the catalogue).
2. Grant the key, then repeat with a correct password + PIN → the action succeeds and a signature
   appears in the record's signature manifest and in the compliance signature log.
3. A wrong PIN returns **422 SIG-001** and mints nothing; a missing password/PIN returns **400**.

## Does not change

Overall release posture is unchanged — **Pre-production / Approved-with-conditions**. This note
concerns configuration only; it does not affect the two open release blockers: **DOC-001** (signed
validation on a qualified environment) and **SEC-001** (independent penetration test). RISK-03's own
validation-record execution and signatures (URS-123–128) fold under DOC-001.

## Deploy-path corrections (v1.53.1)

Tagged **`v1.53.1`** on top of the v1.53.0 e-signature release. These are **deploy/upgrade
mechanics only** — no application behaviour, API surface, permission, or database schema change (the
schema is identical to v1.53.0), and they do **not** alter the signing-key grant steps in §1–§2
above. They matter only to whoever runs the install/upgrade:

- **Apply migrations as `qams_owner`, not `qams_app`,** then run `harden-runtime-role.sql` as a
  superuser. Applying DDL as the runtime role either fails (no DDL rights) or trips the TENANT-004
  start-up guard (the app refuses to boot if `qams_app` owns the tables). The two ANTIGRAVITY
  prompts were corrected and `DEPLOY.md`'s package manifest now lists `harden-runtime-role.sql`.
- **From-scratch `psql -f deploy/migrations.sql` now applies.** Two migrations opened their
  FORCE-RLS bypass with `SELECT set_config('app.bypass_rls','on',true)`, which aborts with **42601**
  inside the idempotent `DO $EF$` wrapper — so a fresh idempotent apply failed (the `MigrateOnStartup`
  path was unaffected, which is why it went unnoticed). Now `SET LOCAL app.bypass_rls = 'on'`, valid
  in both paths. **If deploying < v1.52.0 → v1.53.x, use the v1.53.1 `migrations.sql`** (or regenerate
  it with `dotnet ef migrations script --idempotent`).
- **`harden-runtime-role.sql` no longer aborts** on grants to a non-existent `ref` schema, and its
  dead role-creation branch (roles come from `db-init.sql`) was removed.
- **The Windows service reaches `Running`.** `Program.cs` now calls `UseWindowsService()`, so an
  `sc.exe`-registered service integrates with the SCM instead of timing out (error 1053) — relevant
  if you deploy from source / recompile rather than the shipped self-contained build.

Two deploy-side checks remain QA-owned and are **not** proven by the automated suite: **IQ-30**
(from-empty-DB `psql -f migrations.sql` applies) and **IQ-31** (Windows-service start). See the
v1.53.0 upgrade checklist (`deploy/UPGRADE-CHECKLIST-v1.53.0.md`) for the full mechanics.

## Reference

Full engineering detail and the authoritative upgrade table:
`docs/validation/06-Revalidation-Delta-v1.38-v1.50.md` **§A.19** (and §A.13–A.18 per gate).
Change log: `IMPLEMENTATION_LOG.md` (RISK-03 close-out entry). Commits `ddd1551`…`6b60386` on `dev`.
Deploy-path corrections (v1.53.1): `IMPLEMENTATION_LOG.md` "Deploy remediation" entry; commits
`6eabfbb`…`9271bd6` on `dev`; IQ-30/IQ-31 in `docs/validation/06-…` Part B.
