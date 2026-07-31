# Schema Hardening — Completion Report

| Field | Value |
| ----- | ----- |
| Programme | 8 changes, delivered as 5 phased EF Core migrations (`Hardening1` … `Hardening5`) |
| Database | `ntqams`, PostgreSQL 17, EF Core 9.0.18 + Npgsql |
| Commits | `28ba880` P1 · `8be2c13` P2 · `ea9eb24` P3 · `9e7c3eb` P4 · `fdc08df` P5 (+ this docs pass) |
| Plan | `SCHEMA-HARDENING-PLAN.md`, approved with recommendations after the discovery step |
| Environment | Development workstation. **Not executed on a qualified/staging installation** |

Every figure below was read from `pg_catalog` / `information_schema` after the migrations ran.
Nothing here is inferred from the migration source.

---

## 1. What changed, per phase

### Phase 1 — `Hardening1_TypesAndNames`

| Change | Result |
| ------ | ------ |
| 1.1 `audit.security_event.ip_address` → `inet` | Raw SQL `USING ip_address::inet`. The CLR type stays `string` behind an EF value converter, because the endpoint returns the entity directly — an `IPAddress` property would have changed the wire contract |
| 1.2 `varchar(≥1000)` → `text` | 55 columns (the 56th is 1.3). All matching `HasMaxLength` removed |
| 1.3 `criteria_json` → `criteria jsonb` | Renamed end-to-end (aggregate, EF config, DTO, SPA model) |
| 1.4 Index names | 3 EF-truncated names renamed and pinned with `HasDatabaseName`; abbreviation map added to `CLAUDE.md` |
| Q6 validators | **17** FluentValidation rules added/extended so the bound moved from the column to the API |

### Phase 2 — `Hardening2_RlsGapClosure`

`audit.security_event` and (discovered) `qams.ref_counter` both gained ENABLE + FORCE RLS and a
`tenant_isolation` policy. `GetSecurityEventsAsync` — the one ledger read with no tenant filter —
now filters like its siblings.

### Phase 3 — `Hardening3_CheckDomains`

71 constraints: 64 enum domains + 2 closed literal sets + 5 hash formats, all `NOT VALID` then
`VALIDATE`. Total user CHECKs 14 → 85.

### Phase 4 — `Hardening4_ChildTenancy`

30 owned child tables gained `tenant_id NOT NULL` (backfilled from the parent), RLS, and 28
tenant-composite FKs backed by 24 parent `UNIQUE (id, tenant_id)`. 5 FKs to `saas.tenant`
(`RESTRICT`) on the elevated-writer tables only.

### Phase 5 — `Hardening5_CompositeKeys`

88 tenant-first composite primary keys; `department → branch` converted to composite; ownership
FKs widened. No `UNIQUE (id)` added.

## 2. What the catalog proves

| Assertion | Measured |
| --------- | -------- |
| Tables | 96 (`qams` 89 · `audit` 4 · `saas` 2 · `read` 1) |
| FORCE-RLS tables / `tenant_isolation` policies | **90 / 90** |
| RLS parity violations among NOT-NULL-tenant tables | **0** |
| Tenant-first composite PKs | **88**; single-id PKs left with NOT NULL tenant: **0** |
| Foreign keys | 36 (29 tenant-composite, 5 to `saas.tenant`, 2 to `user_account`) |
| CHECK constraints | 85 (67 domain, 5 hash, 13 pre-existing); left `NOT VALID`: **0** |
| Identifiers > 62 chars | **0** |
| Guard triggers | **17**, all enabled (4 append-only + 13 `frozen_immutability`) |
| Audit hash chain | `ok: true`, 82 entries, no break — after re-keying `audit_trail` |
| Types | 172 `text`, 1 `jsonb`, 3 `inet` |

**The headline result.** Before Phase 4, `SELECT * FROM qams.rca_record` returned every tenant's
rows. Measured after: 2 rows total, demo-lab sees **1**, foreign rows **0**, nil tenant **0**;
a child insert carrying a tenant different from its parent's is rejected `23503`, while the same
insert with the parent's tenant is accepted.

## 3. Defects found — and where they came from

| # | Defect | Found by | Disposition |
| - | ------ | -------- | ----------- |
| 1 | **Login broke (HTTP 500) after `security_event` RLS.** `LoginHandler` writes tenant-stamped `LOGIN_*` events before any tenant context exists, so the new `WITH CHECK` refused them (42501) | Phase 2 **live browser check** — the 419-test suite passed, because functional tests run on InMemory where RLS does not exist | Fixed: both pre-auth handlers scope the request tenant as soon as the slug resolves. Side benefit — failed logins are now visible to their own tenant |
| 2 | **Audit-ledger identity would have been corrupted.** `FieldChangeInterceptor.RenderKey` joins all PK properties, so `entity_id` silently became `"<tenant>\|<id>"` — a different value from every historical row and from what `GetFieldChangesQuery(entityId)` looks up | Phase 5 functional test | Fixed: renders the record identity only; tenant is already its own column. Verified live |
| 3 | **`refresh_session.token_hash` is uppercase hex.** The brief specified `^[0-9a-f]{64}$` for all five hash columns; all 59 rows would have violated it and `VALIDATE` would have failed | Phase 3 **pre-flight scan** | Constraint matches the writer (`[0-9A-F]`); the other four stay lowercase |
| 4 | **FORCE RLS blocks a migration's own work.** The Phase-4 backfill ran as a silent no-op (parents invisible to the tenant-less session) and the first composite FK then failed on a nil tenant. The `Down()` hits it differently: PostgreSQL's referential-integrity check is *itself* subject to FORCE RLS | Phase 4 round-trip | Both directions declare a transaction-local bypass. Written up in `CLAUDE.md` as a standing rule |
| 5 | **EF's model snapshot never learns raw-SQL DDL.** Phase 4 renamed 28 FKs in SQL; Phase 5's scaffold then emitted drops for constraints that no longer existed | Phase 5 apply | 56 names reconciled against `pg_constraint`; `Down()` corrected so its drops target what `Up()` creates |

### Mistakes I made during execution (recorded, not hidden)

- A model-level PK convention that broke same-row owned types — abandoned for explicit `HasKey`.
- A rewrite regex that hardcoded the builder parameter name `builder`, missing 23 configurations
  — **and a verification script that reported a false pass**, because it matched
  `HasIndex(… new { s.TenantId … })`. Both replaced with an indentation-scoped rewrite and a
  check that looks only at `HasKey` lines.
- A later regex that rewrote a `HasKey` *inside* an owned-collection lambda; reverted with
  `git checkout` and redone.
- The Phase-2 `ref_counter` test invented tenant GUIDs; Phase 4's new tenant FK correctly
  rejected them. The FK was right, the test was wrong.

## 4. Contradiction in the approved plan (Q5)

`SCHEMA-HARDENING-PLAN.md` §Phase 5 recommended **keeping** `UNIQUE (id)`; §10's decision table
recommended **dropping** it. You approved "with recommendations", so the conflict was mine to
resolve and I should have caught it before you approved.

**Resolved: no `UNIQUE (id)`** — for a reason neither entry gave. PostgreSQL forbids a unique
index that omits the partition key, so keeping it would defeat the whole purpose of Phase 5.
Reversible in one migration.

## 5. Corrections to the brief's assumptions

| Brief said | Measured |
| ---------- | -------- |
| 27 owned child tables (doc: 33) | **30** — the doc predates v1.51's Role Privilege module |
| ~53 status-ish columns | **47** (67 enum-backed columns in total, all constrained) |
| ≥ 1 index name already truncated at 63 | **0 stored names exceed 62**; EF had truncated 2 client-side to exactly 62, mid-word |
| 88 tables in Phase 5 scope | **88 keyed**, from 84 single-id + 3 natural-key children + 2 audit ledgers − 1 already tenant-first |
| `docs/reference/NT_QMS_Database_Architecture.html` | Does not exist. The `.md` is the design document; `NT_QMS_Database_AsBuilt.md` was created for the as-built state (Q4) |
| 93 tables / 28 FKs / 14 CHECKs | 96 / 36 / 85 after this programme |

## 6. Verification performed

- **Round-trip** `up → down → up` on every phase, against the real database.
- **Introspection** after each phase (§2) — asserted from the catalog, never from source.
- **432 backend tests green** (228 domain, 72 application, 24 architecture, 32 integration
  incl. 1 skipped, 77 functional) — was 419 before the programme; **13 new tests**:
  4 RLS (security_event/ref_counter/login-shape), 9 owned-child tenancy (7 per-family isolation
  cases, drift rejection with control, and a structural sweep that fails if any future table
  carries a NOT NULL tenant without full RLS), plus a Phase-3 CHECK-family probe.
- **Live application** after each risky phase: sign-in, NC workflow through triage → RCA → CAPA,
  compliance ledger, dashboard; 8 API endpoints 200; console clean; hash chain verified.
- **Least privilege** — `RuntimeRolePrivilegeTests` passes; `DatabaseRoleGuard` unchanged.

**Environment caveat:** dev is owner-role, so `qams_app` holds grants it will not hold in
production. The audit `SELECT`+`INSERT`-only control lives in `deploy/harden-runtime-role.sql`
and must be re-verified on a role-split installation.

## 7. Also-done

- `deploy/migrations.sql` regenerated `--idempotent`: **10 → 55 migrations**
  (`InitialFoundation` … `Hardening5_CompositeKeys`).
- `CLAUDE.md` §5: index abbreviation map, `varchar`/`text` rule, composite-PK convention,
  tenant-composite FK rule, and the two migration lessons (FORCE-RLS bypass; EF snapshot vs
  raw SQL).
- `docs/reference/NT_QMS_Database_AsBuilt.md` created (Q4) — measured as-built state with
  re-runnable verification queries.
- `deploy/BACKUP-RESTORE-DR.md` §5: restore gate now asserts the RLS parity query, the 17 guard
  triggers, and `(tenant_id, id)` row identity in manifests.
- Pre-flight scripts kept re-runnable: `scripts/preflight-data-checks.sql` (33 checks),
  `scripts/preflight-enum-domains.sql` (67 checks).

## 8. Accepted deviation — RLS on `user_account` and `outbox_event` (was B9)

**Decision: permanently accepted.** Accepted by A. Awad (System Owner) on 2026-08-01, on the
engineering analysis below. This closes B9; it is no longer a backlog item.

### What the deviation is

Two tables carry a `tenant_id` but have **no** row-level security: `qams.user_account` and
`qams.outbox_event`. Every other tenant-carrying table (90 of them) has RLS enabled, forced and
policied. These two are pre-existing — they were not introduced by this programme.

### Why RLS cannot express their rule

Both columns are **nullable by design**, and the `tenant_isolation` policy predicate
(`tenant_id = current_setting('app.current_tenant')`) is *false* for NULL. Applying it would:

- **`user_account`** — make every platform administrator invisible to the platform itself, and
  break authentication, which necessarily runs *before* a tenant is resolved (Phase 2 already
  demonstrated this failure mode on `security_event`: login returned HTTP 500 until the handler
  was changed to scope the tenant as soon as the slug resolves — a fix that is impossible here,
  because a platform admin has no slug).
- **`outbox_event`** — stop the outbox processor draining events, since it runs cross-tenant by
  design.

A null-tolerant policy (`tenant_id IS NULL OR tenant_id = …`) would not isolate anything: it
would make every platform-level row visible to every tenant.

### Compensating controls (verified 2026-08-01, not asserted)

**`user_account`** — all 27 access sites were read. Every one falls into exactly one of three
shapes, and none lists users without a bound:

| Shape | Sites | Example |
| ----- | ----- | ------- |
| Explicit tenant predicate | `GetUsersHandler`, user directory | `.Where(u => u.TenantId == tenantId)` |
| Keyed by the authenticated actor's own id, taken from the validated JWT `sub` | MFA/PIN, refresh, `PrivilegeResolver`, `ActiveSessionMiddleware` | `.Where(u => u.Id == userId)` |
| Keyed by an id set already derived from a tenant-filtered query | acknowledgement names, role member counts, the `roles.manage` lockout guard | `userIds.Contains(u.Id)` where `userIds` came from tenant-filtered rows |

Plus: authentication itself is the gate (handlers resolve by `(TenantId, Email)`);
`ActiveSessionMiddleware` re-checks the account on **every** authenticated request; and
`users.view` / `users.manage` privileges gate the administrative surface.

**`outbox_event`** — only three code paths touch the table (`OutboxInterceptor` writes,
`OutboxProcessor` drains, its EF configuration). The processor runs under
`ICurrentTenantSetter.Elevate()`, i.e. deliberately cross-tenant. It is never reachable from a
request-scoped path, and it carries no tenant-facing read surface.

### Residual risk, stated plainly

These controls are **discipline, not structure**. A future query that lists `user_account`
without a bound would leak across tenants and nothing in the database would stop it — which is
precisely the class of defect RLS exists to make impossible, and precisely what Phase 4 fixed for
the 30 owned children. The risk is accepted on the basis that the surface is small (27 sites),
enumerated above, and gated by privileges.

**Guard implemented — `UserAccountTenantBoundTests`** (Architecture.Tests, 9 cases). It converts
the discipline back into a build-time guarantee: every `db.Users` / `Set<UserAccount>()` query
across Application, Infrastructure and WebApi must be bounded by a tenant predicate, a specific
user/actor id, or an id set derived from a tenant-filtered query. Source-level by necessity —
the bound lives in a LINQ predicate, which is not recoverable from compiled IL the way a type
reference is, so NetArchTest (used by the sibling rules) cannot express it. Genuinely
cross-tenant infrastructure opts out with a `tenant-unbounded:` comment stating why, so an
exemption is a written decision rather than a silent omission — currently used exactly once, by
the elevated startup role-backfill. The rule carries its own positive and negative cases, and
was mutation-tested: an injected `db.Users.Where(u => u.IsActive)` fails the build with the
offending file and line.

**The guard found a real defect on its first run.** `GetRolesHandler` built its member-count map
with `db.Users.Where(u => u.RoleId != null)` — an unqualified scan reading **every tenant's**
users into memory. The response was not wrong (role ids are unique, so a tenant only read its
own counts back out), but the query crossed the boundary and was one refactor away from leaking.
Now bounded to the tenant's own role ids. Two further sites (`RolesSlice` lines 39 and 273) were
correctly bounded by tenant-resolved *role* ids — a legitimate fourth shape the first version of
the matcher did not recognise, now added.

### Revisit triggers

Re-open this decision if any of the following becomes true:

1. `user_account` is split into platform and tenant tables (the tenant half could then be
   fully RLS-fenced).
2. A user-listing endpoint is added that is not privilege-gated.
3. An external assessor or the independent penetration test flags it.

---

## 9. Follow-up backlog (reported, not implemented)

| # | Item | Rationale |
| - | ---- | --------- |
| B1 | Partition `audit.audit_trail` (HASH `tenant_id`), `field_change`/`security_event` (RANGE monthly), `outbox_event` (RANGE → `DROP PARTITION` purge) | Phase 5 made the schema ready; enabling needs volume data, an ops window, and a key decision for the four nullable-tenant tables |
| B2 | `citext` or lower-normalisation for `user_account.email` | `Alice@x.com` and `alice@x.com` are two accounts under `UNIQUE (tenant_id, email)`; needs dedup analysis first |
| B3 | Actor-column naming: `_by_user_id` (uuid) vs `_by_name` (text) | `created_by`/`modified_by` are `text` while five `_by` columns are `uuid` — same suffix, different type |
| B4 | Split polymorphic `subject_ref` (`MODULE:REF`) | `archive_entry` already models the target shape with `source_module` + `source_ref` |
| B5 | Orphan detection for `file_reference` by-id edges | `document_version.file_id`, `archive_entry.snapshot_file_id` have no FK |
| B6 | Populate `security_event.ip_address` | The column is `inet` and correct, but nothing writes it. GDPR note: IPs are personal data — needs a retention rule |
| B7 | Type `QcRun.Outcome` as `WestgardOutcome` | Closes the string/enum gap the Phase-3 CHECK papers over |
| B8 | Composite FK `user_account(role_id) → role` | No FK exists today; deferred because `user_account` keeps a single-column PK |
| B10 | Disposition pre-RP-D1 ledger rows with an empty tenant | Append-only ledger, not restated — QA decision |

## 10. Status

The eight requested changes are delivered, verified by introspection and by live use, and
committed. **B9 is closed** — permanently accepted as a documented deviation (§8), with
compensating controls verified rather than asserted. One item still needs your decision rather
than more engineering: **B10**, the historical ledger rows written with an empty tenant before
the RP-D1 fix (append-only, so they cannot be restated). Nothing in this programme has been
executed on a qualified environment — that remains open, as it was before it started.
