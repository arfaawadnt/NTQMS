# Operational Qualification — Execution Record: Mail Management & HTML Email (URS-133, URS-134)

| Field | Value |
| ----- | ----- |
| Document ID | OQ-EXEC-NTQMS-009 |
| Protocol executed | REVAL-NTQMS-001 (doc 06) Part A — requirements **URS-133, URS-134** |
| System / version | NT.QMS **v1.53.x** (working tree, pending commit) — migration `AddTenantMailSettings` (new tenant table, FORCE RLS) |
| Environment | **Development workstation** — API `http://localhost:5080` (Development), PostgreSQL 17 local (`ntqams`, role `qams_app`); functional suite on EF InMemory + real host; RLS suite on real PostgreSQL |
| Executed by (operator) | Engineering (Claude Code) |
| Witnessed by | _(unsigned — pending)_ |
| Date of execution | 2026-08-08 |
| Test data | Demo laboratory `demo-lab`; operator `admin@demo-lab.local` (TenantAdmin) |
| Result | **All automated case-groups green; live save + persistence verified; a witnessed live SMTP send is left for QA (no relay in dev)** |

> **Scope statement.** The HTTP result and persisted values below were **actually observed** live; the
> automated results were watched to completion.
>
> **Declared limitations (must be dispositioned by QA):**
> 1. **Development workstation, not a qualified installation** — this record does not close DOC-001.
> 2. **Independence is limited** — the operator authored the code under test; no witness signature.
> 3. **No live SMTP relay in dev** (`Smtp:Host` unset ⇒ `LoggingEmailSender`), so a real branded HTML
>    e-mail was not sent end-to-end here. The template output (branding + HTML-escaping) is proven by
>    unit test; a witnessed live send on a configured relay is left for QA.

---

## 1. Live checks — actual results (dev)

### OQ-MAIL-01 — the sender identity is configured and persists

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| `/mail-management` renders the sender form (name, address, reply-to, brand colour, footer, enable) | admin form | all fields + Save rendered | **Pass** |
| First visit reports "not configured" and mail defaulting to enabled | notice shown, `configured=false`, `enabled=true` | GET returned `configured=false`, `enabled=true` | **Pass** |
| Save a sender identity | `PUT → 204` | `PUT /api/notifications/mail-settings → 204 No Content` | **Pass** |
| Reload reflects the saved identity and clears the notice | values persist, `configured=true` | after reload: fromName `Demo Lab Quality Office`, fromAddress `quality@demo-lab.example`, brand `#00B2A9`, notice gone | **Pass** |

## 2. Automated evidence (watched to completion, 2026-08-08)

| Suite / test | Asserts | Result |
| ------------ | ------- | ------ |
| `TenantMailSettingsTests` (9) | invariants MAIL-001…004 (name, from/reply e-mail shape, hex colour) and update/disable | Pass |
| `MailSettingsRlsTests` (2, **real PostgreSQL**) | reads fenced to the acting tenant; fail-closed with no tenant; controlled bypass sees all; WITH CHECK rejects a cross-tenant insert | Pass |
| `MailSettingsEndpointTests` (3, real pipeline) | not-configured default → saved identity reflected on GET; malformed sender → 422 `MAIL-002`; unauthenticated → 401 | Pass |
| `HtmlEmailTemplateTests` (3) | self-contained branded HTML (no external assets); configured brand colour used as accent; every caller value HTML-escaped (incl. a `<script>` payload) | Pass |
| `NotificationDispatcherTests` | the feed-first, best-effort-email, idempotent dispatch guarantee holds over the new `EmailMessage` port | Pass |
| Migration | `AddTenantMailSettings` round-tripped (Down/Up); RLS verified from `pg_class` (`t/t`) + `pg_policies` (`tenant_isolation`) + the brand-colour CHECK | Pass |
| `ApiSurface` snapshot | `GET`/`PUT /api/notifications/mail-settings` (+ versioned twins) added and reviewed | Pass |
| Full backend suite | Domain 254 / App 102 / Arch 33 / Integration 33 (+1 skip) / Functional 93 = **515** | All green (real PG) |
| Frontend production build + unit | clean + 95 Karma | Pass |

## 3. Disposition

Engineering-complete and evidenced. The Mail Management page configures only the tenant's **sender
identity and branding** — SMTP transport credentials are never entered or stored (they stay in server
configuration), so no reversible secret is persisted in the tenant database (the SEC-001 posture
decision). The new `tenant_mail_settings` table is FORCE-RLS isolated and proven so against real
PostgreSQL. No new permission key is introduced (`notifications.manage` is reused), so there is **no
tenant authorization upgrade action**. **QA to review, execute a witnessed live send on a configured
SMTP relay, and sign.**

---

**Signatures** _(left blank — execution and QA review by a human; engineering does not self-certify)_

| Role | Name | Signature | Date |
| ---- | ---- | --------- | ---- |
| Operator | | | |
| Witness / QA | | | |
| System Owner | | | |
