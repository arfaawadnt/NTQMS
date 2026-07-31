# Schema Hardening — Discovery Report & Table-by-Table Plan

| Field | Value |
| ----- | ----- |
| Status | **DISCOVERY — awaiting approval before any migration is written** |
| Date | 2026-07-31 |
| Verified against | live `ntqams` (PostgreSQL 17) at commit `d979974`, 49 EF migrations applied |
| Method | direct catalog introspection (`pg_class`, `pg_constraint`, `pg_policies`, `information_schema`) + code sweep; every number below was measured, not taken from a document |
| Pre-flight scripts | `scripts/preflight-data-checks.sql`, `scripts/preflight-enum-domains.sql` (both re-runnable; both executed clean on 2026-07-31) |

---

## 0. Discovery findings — where reality differs from the brief

| # | Brief said | Measured reality | Consequence |
|---|-----------|------------------|-------------|
| D1 | 56 varchar ≥ 1000 columns | **56** ✓ (35 tables) | as planned |
| D2 | ~53 status/state/role/event_type/action columns | **47** | Phase 3 scope corrected; separately, **67 enum-persisted columns** exist (many not named `status`-like: `source_type`, `channel`, `grade`, `verdict`, `type`…). Recommend constraining all 67-minus-exclusions, not just the 47 (§Phase 3) |
| D3 | 27 owned child tables (doc says 33) | **30** — the doc's figure predates the v1.51 Role Privilege module which added `role_permission`, `user_branch_access`, `user_department_access` | Phase 4 covers 30 tables; 2 are special-cased (§Phase 4) |
| D4 | 28 FKs | **31** (30 CASCADE + 1 RESTRICT `department→branch`) | same cause (v1.51 added 3) |
| D5 | ≥1 index name already truncated at 63 | **No identifier exceeds 62 chars.** Three sit at 61–62, two visibly cut mid-word by EF client-side truncation: `ix_document_acknowledgement_tenant_id_document_id_version_labe` (62), `ix_notification_dispatch_tenant_id_recipient_user_id_read_by_r` (62), `ix_document_controlled_copy_tenant_id_document_id_copy_number` (61) | Phase 1.4 renames these 3 and installs the abbreviation map; the real future risk is Phase 5 regenerating longer names — the map is applied there too |
| D6 | Phase-5 blockers: 3 known + verify `outbox_event` | Exactly **4 nullable-`tenant_id`** tables: `user_account`, `audit.field_change`, `audit.security_event`, **`qams.outbox_event` (confirmed nullable)** | Phase 5 excludes all 4, as the brief requires |
| D7 | `security_event` is the RLS gap | **`qams.ref_counter` is a second, undocumented parity violation**: `tenant_id NOT NULL`, composite PK `(tenant_id, ref_type, year)`, written by raw SQL (`PostgresReferenceNumberGenerator`), **no RLS, no policy** | Added to Phase 2 |
| D8 | `docs/reference/NT_QMS_Database_Architecture.html` is the as-built reference | **That file does not exist in the repo.** The only DB doc is `docs/reference/NT_QAMS_Database_Architecture.md` — the *design/target-state* document (it does document the dedicated-DB escape hatch via `Tenant.ConnectionString`, target < 5 % of tenants). No repo file contains "93 tables" or the "honest register" | Deliverable "update the as-built doc" needs a decision: **create** the as-built reference fresh, or amend the design doc (§Questions Q4) |
| D9 | — (not in brief) | `audit.security_event.ip_address` is **never populated**: `ISecurityEventLog.WriteAsync` has no ip parameter and nothing sets it. Every row is NULL | The `inet` conversion is trivially safe; actually *capturing* the caller IP is a functional gap → backlog (§Out of scope B6) |
| D10 | verify `qams_app` holds no DELETE on `audit.*` | On **dev**, `qams_app` is the table owner and holds DELETE/UPDATE/TRUNCATE on everything; the append-only *triggers* are the effective guard. `deploy/harden-runtime-role.sql` (prod path) grants audit SELECT,INSERT only + explicit REVOKE DELETE | The verification step must run against a role-split database (clean-DB round-trip in CI uses owner too); wording adjusted (§Verification) |
| D11 | migrations.sql stale, covers 1–10 of 49 | Confirmed: exactly 10 of 49 (`InitialFoundation` … `ComplianceAndAuth`) | regenerate as planned |
| D12 | 93 tables | **96 data tables** (89 qams + 4 audit + 2 saas + 1 read) + `__EFMigrationsHistory` = 97; the 93 figure predates v1.51 (+4 tables) | doc correction |
| D13 | — | Exactly **one** `Find/FindAsync` call site in the codebase (`ExportsController.cs:163`, on `db.Tenants` — excluded from Phase 5) | Phase 5's feared application ripple is **zero call sites** |
| D14 | — | The migration round-trip test (`GovernanceTests`) reverts the **latest** migration against the shared dev DB. Phase 4/5 migrations will make that revert drop `tenant_id` columns / restore old PKs mid-test — destructive on dev data (roles wipe already observed on 2026-07-31, self-healed by the boot backfill) | run the suite against a scratch DB for these phases, or accept dev-data churn (§Verification note) |

**Pre-flight executed 2026-07-31 — all clean:** 0 enum-domain violations across all 67 enum-backed columns; 0 invalid JSON in `criteria_json`; 0 non-null `ip_address` (hence 0 unparseable); 0 orphaned child rows (CASCADE FKs make them impossible); 0 platform-admin-owned scope rows.

---

## Phase 1 — low-risk independent changes (one migration: `Hardening1_TypesAndNames`)

### 1.1 `inet` (1 column)

| Item | Plan |
|---|---|
| Column | `audit.security_event.ip_address varchar(60) → inet` |
| Raw SQL? | **Yes** — EF `AlterColumn` cannot emit `USING ip_address::inet`; `migrationBuilder.Sql("ALTER TABLE audit.security_event ALTER COLUMN ip_address TYPE inet USING ip_address::inet")`. `Down()`: `TYPE varchar(60) USING host(ip_address)` |
| EF/CLR | `SecurityEvent.IpAddress` becomes `System.Net.IPAddress?` (Npgsql maps it natively); the single projection to the DTO renders `?.ToString()` so the SPA contract (`ipAddress: string \| null`) is unchanged |
| Trigger | `security_event_append_only` is BEFORE UPDATE/DELETE — ALTER TYPE is unaffected; re-verified post-migration |
| Data risk | none — column is entirely NULL today (measured) |

### 1.2 `varchar(n≥1000) → text` (56 columns, 35 tables)

Full list (column(length)): see Appendix A1. Notes:

- `qams.supplier_evaluation.criteria_json(8000)` is **handled by 1.3 instead** (jsonb) — 1.2 touches 55.
- 3 of the columns live on `audit.field_change` (append-only schema) — ALTER TYPE is plain DDL; trigger re-verified after.
- EF change: remove the matching `HasMaxLength()` from the configurations (agent-inventoried, 56 exact locations); columns become `text`. EF generates the `AlterColumn`s — no raw SQL needed.
- `Down()`: restore `varchar(n)` — safe only if no row exceeds n; the down-path adds a guard query and this is called out in the migration comment.

**Your question — does API validation still bound these fields? Audit complete (Appendix A2): NO for 29 of 56.**
27 have a matching FluentValidation `MaximumLength` (none mismatched). Of the 29 without one,
**6 are system-written** — never API input (`field_change.old_value/new_value`,
`outbox_event.last_error`, `notification_dispatch.body/error`, `management_review.participants`
is assembled server-side) — for these the varchar cap was an error path, not a validation; and
**23 are genuine request-DTO gaps** (full list in A2). Per your instruction nothing is added
silently — **Q6** asks whether Phase 1 adds `MaximumLength` rules (values = the current DB
bounds) to those 23 validators in the same commit as the type change. My recommendation: yes —
dropping the DB bound while those validators stay silent is the one combination that allows
unbounded input.

### 1.3 `criteria_json → criteria jsonb`

- Raw SQL: `ALTER TABLE qams.supplier_evaluation RENAME COLUMN criteria_json TO criteria; ALTER ... TYPE jsonb USING criteria::jsonb` (rename via EF `RenameColumn` + raw `Sql` for the USING cast).
- Ripple (measured): domain `Supplier.cs` `CriteriaJson` property → `Criteria`; EF config `HasMaxLength(8000)` → `HasColumnType("jsonb")`; DTO `SupplierEvaluationDto.CriteriaJson` → `Criteria`; SPA `models.ts criteriaJson` + its one consumer; `SupplierSlice.cs` projection. **DTO rename is a wire-contract change** — only our SPA consumes it, but say so now: approve or keep the DTO name `criteriaJson` while the column becomes `criteria` (my recommendation: rename everywhere, it's one screen).
- Gated on the JSON pre-flight (passed: 0 invalid rows).

### 1.4 Identifier shortening + abbreviation map

Renames (raw SQL `ALTER INDEX ... RENAME TO`, pinned with `HasDatabaseName`):

| Current (len) | New (len) |
|---|---|
| `ix_document_acknowledgement_tenant_id_document_id_version_labe` (62) | `ix_doc_ack_tenant_id_document_id_version_label` (47) |
| `ix_notification_dispatch_tenant_id_recipient_user_id_read_by_r` (62) | `ix_notif_dispatch_tenant_id_recipient_read` (43)¹ |
| `ix_document_controlled_copy_tenant_id_document_id_copy_number` (61) | `ux_doc_copy_tenant_id_document_id_copy_number` (46)² |

¹ exact trailing column list confirmed at implementation from the model. ² currently `ix_` though UNIQUE — will confirm uniqueness flag and use the correct prefix.

Abbreviation map → CLAUDE.md: `document_acknowledgement→doc_ack`, `document_controlled_copy→doc_copy`, `notification_dispatch→notif_dispatch`, `instrument_comparability_study→icp_study`, `user_department_access→user_dept_access`, `supplier_evaluation→supplier_eval` (the last three pre-emptively for Phase 5 name growth). Collision check across all shortened names is part of the phase's verification. The 8 existing `HasDatabaseName` pins stay.

---

## Phase 2 — RLS on `audit.security_event` **and `qams.ref_counter`** (migration: `Hardening2_RlsGapClosure`)

- `security_event`: exactly the policy in your brief (null-tenant `WITH CHECK` allowance; matches the three sibling ledgers post-`RelaxAuditRlsWriteCheck`).
- `ref_counter` (discovered gap): standard tenant policy — `tenant_id NOT NULL`, written under tenant context by `PostgresReferenceNumberGenerator` (raw `INSERT ... ON CONFLICT`; runs on the tenant connection, so `app.current_tenant` is set — verified the interceptor applies to raw commands on the same connection).

**Read-path behaviour changes you asked me to report (verified first-hand):**

1. `ComplianceLedgerStore.GetSecurityEventsAsync` (`ComplianceLedgerServices.cs:192-194`) has **no tenant filter** — unlike its siblings. Today `GET /api/compliance/security-events` (gate: `compliance.view`) returns **every tenant's events**: a real cross-tenant leak that Phase 2 closes. After RLS the same endpoint silently narrows to the caller's tenant. I will also add the explicit `TenantId == tid` filter to the store (defense-in-depth + parity with siblings + keeps InMemory-based tests honest).
2. **Pre-auth events (tenant NULL — `LOGIN_FAILED`, `REFRESH_INVALID`…) disappear from every tenant's view** under this policy (USING has no null allowance, matching the sibling ledgers). They remain queryable only via the elevated/bypass path, which currently has **no endpoint**. → Question Q2: accept (platform-level telemetry, future platform endpoint — my recommendation), or add `tenant_id IS NULL` to USING so every tenant sees pre-auth events (leaks cross-tenant login-failure patterns; not recommended).

---

## Phase 3 — CHECK domains (migration: `Hardening3_CheckDomains`)

Source of truth: 68 C# enums inventoried; **67 persisted string-enum columns mapped** (65 via `HasConversion<string>` + 2 closed-set literals). The scan proving zero violating rows is `scripts/preflight-enum-domains.sql`.

- **Constrain (63 columns):** every `HasConversion<string>` column (Appendix A3 lists all with exact value sets) **plus** `qams.qc_run.outcome` (plain string, but written exclusively as `WestgardOutcome.ToString()` → `InControl|Warning|OutOfControl`) **plus** `audit.field_change.action` (single writer, literals `Created|Modified|Deleted`).
- **Skip, with reasons (4):**
  - `audit.security_event.event_type` — **open telemetry set** (9 literals today, new call sites add more); per your rule, skipped.
  - `audit.audit_trail.event_type` and `qams.outbox_event.event_type` — CLR type names, open by design.
  - `qams.work_task.assignee_role` — **not** `UserRole`-governed: carries dynamic role strings from escalation events and API input. A `UserRole`-derived CHECK would be wrong. (Post-v1.51 this column is also conceptually legacy — backlog note.)
- `ck_<table>_<column>_domain` naming; the existing `ck_nonconformance_status_domain` stays as-is (its value set re-verified against `NcStatus` — matches).
- **Hash columns (5, measured):** `audit_trail.prev_hash/entry_hash`, `electronic_signature.content_hash`, `file_reference.sha256`, `refresh_session.token_hash` → `CHECK (col ~ '^[0-9a-f]{64}$')`. All are `varchar(64) NOT NULL`; hex-lowercase confirmed by sampling. Constraint names `ck_<table>_<column>_sha256`.
- Append-only tables receiving DDL (`field_change`): trigger survival re-verified in the phase's verification step.

---

## Phase 4 — `tenant_id` on the 30 owned children + composite FKs (migration: `Hardening4_ChildTenancy`)

Full per-table map in Appendix A4 (child → parent → FK column). Standard recipe per child (28 of 30):

1. `ADD COLUMN tenant_id uuid` → backfill `UPDATE child SET tenant_id = p.tenant_id FROM parent p WHERE p.id = child.<fk>` → `SET NOT NULL` (raw SQL).
2. Parent gains `UNIQUE (id, tenant_id)` (`ux_<parent>_id_tenant`) — 18 distinct parents.
3. Child FK re-pointed to the composite: `FOREIGN KEY (<fk>, tenant_id) REFERENCES parent (id, tenant_id) ON DELETE CASCADE`; old single-column FK dropped; supporting index extended `(fk, tenant_id)`. Tenant drift becomes structurally impossible; **no direct FK to `saas.tenant` on children** (your 4.2 reasoning).
4. `ENABLE/FORCE RLS` + standard `tenant_isolation` policy.

**Special cases (2):** `user_branch_access`, `user_department_access` — parent `user_account.tenant_id` is **nullable**, so a composite FK against it is impossible (no `UNIQUE (id, tenant_id)` on a nullable column pair worth relying on). Plan: `tenant_id NOT NULL` backfilled from the parent (pre-flight proved 0 platform-admin rows), existing plain FK retained, RLS + policy applied, and the drift guard is the `WITH CHECK` clause + the stamping interceptor. Stated honestly: these two lack the structural drift-proof the other 28 get.

**4.3 decision — shadow property, not `ITenantScoped` (my recommendation, with the trade-off you asked for):**
EF Core cannot put a global query filter on an owned type (filters attach to root entity types only), so the "children implement `ITenantScoped` and inherit the EF filter" option **does not exist structurally**. Options that do exist: (a) real domain property on 30 owned classes — invasive, pollutes aggregates with plumbing; (b) **EF shadow property + one generic stamping step in `TenantStampInterceptor`** that walks tracked owned entries and copies the root's `TenantId` via FK metadata — no domain change, single seam, and reads stay guarded by RLS (which is precisely the point of Phase 4: RLS is the fence FKs never were). I recommend (b). Cost: the EF filter genuinely does not apply to direct child queries — but the only paths that query children directly are through their parents today (verified: children are materialized via aggregate `Include`/owned navigation only).

**4.5 FKs to `saas.tenant`** — only the elevated-writer list from your brief: `outbox_event` (nullable → FK permits NULL), `ref_counter`, `read.kpi_snapshot`, `branch`, `user_account` (nullable). `ON DELETE RESTRICT`, added `NOT VALID` then `VALIDATE CONSTRAINT` (per your suggestion — cheap on dev, correct pattern for prod). **No other tenant FKs** — the dedicated-DB escape hatch (§3.1-equivalent in the design doc, `Tenant.ConnectionString`) is real and blanket FKs would foreclose it. Flagged as you asked: the five above are shared-DB-only objects (outbox/counters/read-models/org roots), which would be provisioned per-database anyway in the escape-hatch case; I see no conflict, but it is your call (Q3).

---

## Phase 5 — composite PK `(tenant_id, id)` (migration: `Hardening5_CompositeKeys`)

Scope confirmed by introspection: **58 tables with `tenant_id NOT NULL` today** + **30 children** (tenant-carrying after Phase 4) − special shapes = 84 tables of PK work; the 4 nullable-tenant tables keep `id` PKs (hard blocker honoured). Details:

- `ref_counter` already has PK `(tenant_id, ref_type, year)` — **no change**.
- 3 natural-key children (`role_permission`, `user_branch_access`, `user_department_access`) get tenant_id **prepended**: e.g. `(tenant_id, role_id, permission_key)`.
- Remaining 81: `PRIMARY KEY (tenant_id, id)`; every FK that referenced the old PK becomes composite (Phase 4 already made the 30 child FKs composite; `department→branch` RESTRICT edge becomes `(branch_id, tenant_id)`).
- EF: `HasKey(e => new { e.TenantId, e.Id })` per configuration + owned-type key adjustments; index/PK names regenerated — **the Phase-1.4 abbreviation map is applied and every name explicitly pinned**, with a global ≤62 assertion in verification.
- **Application ripple: zero** — one `FindAsync` in the codebase, on excluded `saas.tenant` (D13). Queries use `Id ==` predicates throughout.
- **`UNIQUE (id)` — recommendation: keep it, per table, for now.** Reasoning: (a) `xmin`-based optimistic concurrency and the outbox/idempotency correlation code assume `id` alone identifies a row when reconstructing from events; (b) UUIDv7 makes collisions a non-issue, but a **uniqueness regression from a buggy elevated writer** (bypassing RLS) would otherwise be silently absorbed into different tenants; (c) cost is one btree per table (~none at current volume). If you prefer the leaner schema, I'll drop them — the indexes are trivially removable later; the reverse (discovering duplicates after the fact) is not.

---

## Verification (every phase)

Clean-DB `ef database update` → `Down()` → up again; catalog proofs by introspection (RLS flags, policies, constraint definitions, column types, ≤62 identifier assertion — queries saved next to the pre-flights); `QMS_ITEST_POSTGRES` suite (`RlsTenantIsolationTests`, tamper detection, signed-record immutability, migration round-trip); **new tests**: cross-tenant read rejection on newly-scoped children (representative set incl. `rca_record`, `role_permission`) + CHECK rejection on a status column; role-guard/least-privilege check runs against a `harden-runtime-role.sql`-hardened scratch DB (D10). Note D14: for Phases 4–5 the round-trip test's down-step is destructive on shared dev data — I will run the suite against a scratch database and restart the dev API after (backfill self-heals seeded roles).

## Also-do (after Phase 5)

Regenerate `deploy/migrations.sql --idempotent` (10→54); CLAUDE.md additions (abbreviation map, composite-PK convention, `varchar` ≥1000→`text` rule); as-built doc per Q4; `deploy/BACKUP-RESTORE-DR.md` — restore-gate unaffected by types/CHECKs, but the composite-PK change alters the documented `pg_restore` verification query for row identity — one line updated.

## Out-of-scope backlog (reported, not implemented)

- B1 Partitioning (`audit_trail` HASH by tenant; `field_change`/`security_event` RANGE monthly; `outbox_event` RANGE→`DROP PARTITION` purge) — Phase 5 makes all of them partition-ready.
- B2 `citext`/lower-normalization for `user_account.email` — duplicate-by-case accounts possible today.
- B3 Actor column typing: propose `_by_user_id uuid` vs `_by_name text` split; today `created_by/modified_by` are text while five `_by` columns are uuid.
- B4 Split polymorphic `subject_ref` (`MODULE:REF`) like `archive_entry` already does.
- B5 Orphan detection for `file_reference` by-id edges (no FKs on `document_version.file_id`, `archive_entry.snapshot_file_id`).
- B6 **(new)** `security_event.ip_address` is never written — plumb the client IP from `HttpContext` through `ISecurityEventLog` (privacy note: IPs are personal data under GDPR — retention policy needed).
- B7 **(new)** `work_task.assignee_role` carries free-form role strings post-v1.51 — migrate to role ids or constrain once the legacy queue drains.

## Questions needing your decision before Phase 1

- **Q1 (1.3)** Rename the DTO/SPA field `criteriaJson → criteria` along with the column (my recommendation), or keep the wire name?
- **Q2 (Phase 2)** Pre-auth (null-tenant) security events become invisible to tenant compliance viewers — accept as platform-level telemetry (recommended) or add `tenant_id IS NULL` to USING?
- **Q3 (4.5)** Confirm the five-table `saas.tenant` FK list; no others will be added.
- **Q4 (docs)** The as-built HTML reference in the brief does not exist in this repo — create `docs/reference/NT_QMS_Database_Architecture_AsBuilt.html` fresh from introspection (recommended), or fold as-built corrections into the design `.md`?
- **Q5 (Phase 5)** Keep per-table `UNIQUE (id)` (recommended, reasoning above) or drop for leanness?
- **Q6 (1.2)** Add the 23 missing `MaximumLength` validator rules (values = current DB bounds) in Phase 1 (recommended), or accept unbounded input on those fields?

---

## Appendix A1 — the 56 `varchar(n≥1000)` columns (35 tables)

audit.field_change: new_value(4000), old_value(4000), reason(1000) · qams.archive_entry: legal_hold_reason(1000) · qams.audit_checklist_item: evidence(2000), question(1000) · qams.audit_finding: description(4000) · qams.audit_trail_review: conclusion(4000) · qams.capa_action: details(2000) · qams.change_request: impact_analysis(4000), implementation_notes(4000), rejection_reason(1000) · qams.competency_record: revocation_reason(1000) · qams.complaint: description(4000), investigation_outcome(4000), resolution(4000), validation_verdict(2000) · qams.conflict_declaration: closure_note(2000), description(2000), mitigation(2000) · qams.context_issue: description(4000), impact(4000), resolution(4000) · qams.document_version: change_summary(1000), rejection_reason(1000) · qams.environmental_reading: remark(1000) · qams.feedback_entry: action_summary(2000), details(4000), review_notes(2000) · qams.interested_party: needs_and_expectations(4000), relevant_requirements(4000) · qams.intermediate_check: remarks(2000) · qams.maintenance_record: work_description(2000) · qams.management_review: minutes(20000), participants(2000) · qams.mitigation_action: description(2000) · qams.nonconformance: description(4000), rejection_reason(1000) · qams.notification_dispatch: body(8000), error(1500) · qams.notification_rule: body_template(4000) · qams.objective_progress: comment(1000) · qams.outbox_event: last_error(2000) · qams.pt_plan: closure_summary(4000) · qams.pt_plan_item: notes(1000) · qams.qc_run: troubleshooting_note(2000) · qams.quality_objective: closure_note(2000), description(2000) · qams.quality_policy: statement(8000) · qams.rca_record: analysis(8000) · qams.reference_standard: quarantine_reason(1000) · qams.review_decision: description(2000) · qams.supplier_evaluation: criteria_json(8000) → **Phase 1.3** · qams.test_authorization: revocation_reason(1000), suspension_reason(1000) · qams.user_access_review: conclusion(4000)

## Appendix A2 — API-bound audit of the 56 (measured 2026-07-31)

**Bounded at the API layer (27):** every remaining 1.2 column not listed below has a
`MaximumLength` rule matching its property in the owning command validator; no validator bound
disagrees with its column bound.

**System-written, not API input (6)** — no validator needed; DB bound removal is safe:
`audit.field_change.old_value/new_value` (FieldChangeInterceptor), `qams.outbox_event.last_error`
(outbox processor), `qams.notification_dispatch.body/error` (dispatcher), plus
`qams.management_review.participants` (assembled from picker ids server-side).

**Genuine gaps — 23 request-bound fields with NO MaximumLength rule (Q6):**

| Table.column (db bound) | Reaches the DB via |
| --- | --- |
| `archive_entry.legal_hold_reason` (1000) | PlaceLegalHold command |
| `audit_checklist_item.question` (1000), `.evidence` (2000) | Schedule/AnswerChecklist commands |
| `change_request.implementation_notes` (4000), `.rejection_reason` (1000) | change-control commands |
| `competency_record.revocation_reason` (1000) | Revoke command |
| `complaint.investigation_outcome` (4000), `.resolution` (4000), `.validation_verdict` (2000) | complaint workflow commands |
| `conflict_declaration.closure_note` (2000) | Close command |
| `context_issue.resolution` (4000) | CloseIssue command |
| `document_version.rejection_reason` (1000) | Reject command |
| `maintenance_record.work_description` (2000) | LogMaintenance command |
| `management_review.minutes` (20000) | review commands |
| `nonconformance.rejection_reason` (1000) | Reject command |
| `notification_rule.body_template` (4000) | UpsertRule command |
| `pt_plan.closure_summary` (4000) | Close command |
| `qc_run.troubleshooting_note` (2000) | QC run commands |
| `quality_objective.closure_note` (2000) | Close command |
| `reference_standard.quarantine_reason` (1000) | Quarantine command |
| `supplier_evaluation.criteria_json` (8000) | RecordEvaluation (→ 1.3 jsonb; still needs a size rule) |
| `test_authorization.revocation_reason` (1000), `.suspension_reason` (1000) | Revoke/Suspend commands |

Proposed rule value for each = its current DB bound, so behaviour is unchanged — the limit just
moves from the column to the validator where it belongs.

## Appendix A3 — 67 enum-backed columns and their value sets

Machine-derived map: `scripts/preflight-enum-domains.sql` contains every column with its exact `IN (...)` set as executed (zero violations). The same generated sets feed the Phase-3 CHECK definitions, so the constraint and the scan can never disagree.

## Appendix A4 — the 30 owned children (child → parent, FK column)

assessment_result→competency_record(competency_id) · audit_checklist_item→audit(audit_id) · audit_finding→audit(audit_id) · calibration_record→equipment_item(equipment_id) · capa_action→nonconformance(nc_id) · carryover_reading→carryover_study(study_id) · detection_measurement→detection_limit_study(study_id) · document_version→controlled_document(document_id) · environmental_reading→monitoring_point(point_id) · instrument_reading→instrument_comparability_study(study_id) · interference_measurement→interference_study(study_id) · intermediate_check→equipment_item(equipment_id) · linearity_measurement→linearity_study(study_id) · lot_sample_pair→lot_comparison_study(study_id) · maintenance_record→equipment_item(equipment_id) · measurement_pair→method_comparison_study(study_id) · mitigation_action→risk_item(risk_id) · objective_progress→quality_objective(objective_id) · outlier_point→outlier_screening(screening_id) · precision_measurement→precision_study(study_id) · pt_plan_item→pt_plan(plan_id) · rca_record→nonconformance(nc_id) · reference_sample→reference_interval_study(study_id) · review_decision→management_review(review_id) · role_permission→role(role_id) · supplier_certificate→supplier(supplier_id) · uncertainty_component→uncertainty_budget(budget_id) · **user_branch_access→user_account(user_id)** ⚠ · **user_department_access→user_account(user_id)** ⚠ · validation_replicate→validation_study(study_id)

⚠ = nullable-tenant parent, special-cased in §Phase 4.
