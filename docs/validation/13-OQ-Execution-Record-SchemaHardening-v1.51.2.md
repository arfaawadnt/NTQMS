# Operational Qualification — Execution Record: Database Schema Hardening

| Field | Value |
| ----- | ----- |
| Document ID | OQ-EXEC-NTQMS-003 |
| Protocol executed | REVAL-NTQMS-001 (doc 06) Part C — cases **OQ-DB-01 … OQ-DB-08** (§A.10) |
| System / version | NT.QMS **v1.51.2**, commit `f156e06`, migration head `20260731223800_Hardening6_DeferrableTenantFks` (56 applied) |
| Environment | **Development workstation** — API `http://localhost:5080` (Development), PostgreSQL 17 local (`ntqams`, role `qams_app`) |
| Executed by (operator) | Engineering (Claude Code), executing at the System Owner's direction |
| Witnessed by | A. Awad — System Owner / acting QA authority |
| Date of execution | 2026-08-01, 02:42–02:44 local |
| Test data | Two laboratories provisioned for this session: `oqdb-a-024231` (`019fba8e-f4d6-…`) and `oqdb-b-024231` (`019fba8e-f5c7-…`) |
| Result | **23 checks across 8 cases — 23 passed, 0 failed, 0 deviations** |

> **Scope statement — read before relying on this record.** Every result below was **actually
> observed** during a live session. HTTP status lines, problem+json bodies and psql output are
> transcribed verbatim; nothing is inferred from the migration source or from a test suite.
>
> **Declared limitations (must be dispositioned by QA):**
> 1. **The environment is a development workstation, not a qualified installation.** This record
>    does not close DOC-001.
> 2. **Independence is limited**: the operator authored the code under test, and the witness is
>    also the System Owner and QA authority. An external assessor will note the absence of
>    segregation of duties.
> 3. The two laboratories created for this session were **left in place** as evidence; they are
>    dev-only data.

---

## 1. Executed cases and actual results

### OQ-DB-01 — Every tenant table is fenced at the database

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| RLS parity query (as-built §7) | returns 0 rows | `0 rows` | **Pass** |
| Count FORCE-RLS tables and `tenant_isolation` policies | 90 / 90 | `90 forced / 90 policies` | **Pass** |
| Tenant-carrying tables with no RLS | exactly the two accepted exceptions | `qams.outbox_event, qams.user_account` | **Pass** |

### OQ-DB-02 — Owned children are isolated, and cross-tenant parentage is impossible

Isolation here rests on **two independent layers**, and they were proved separately — a single
probe would not have distinguished them.

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Tenant A reads its own owned-child register (RCA on its NC) | children returned to their owner | `HTTP 200; rcaRecords=1` (RCA write `HTTP 204`) | **Pass** |
| **Layer 1 — read fence.** A inserts a child under B's parent, using that parent's *real* id, in A's context | no row written: B's parent does not exist for A | `INSERT 0 0` | **Pass** |
| **Layer 2 — structural fence.** Elevated insert of a child whose tenant differs from its parent's | refused by the composite FK | `ERROR: insert or update on table "capa_action" violates foreign key constraint "fk_capa_action_nonconformance_tenant_id_nc_id" DETAIL: Key is not present in table "nonconformance".` | **Pass** |
| Control: the same insert under A's **own** parent | accepted — the constraint discriminates | `INSERT 0 1` | **Pass** |
| Owned-child visibility, A vs elevated | A sees only its own subset | `tenant A sees 0; elevated total 3` | **Pass** |

> Note on the last row: A legitimately sees **0** because its only `capa_action` write was the
> control insert, which was rolled back with its probe transaction. The evidence is that the other
> **3** rows — belonging to other tenants — are not visible to A.

### OQ-DB-03 — Value domains hold, and the append-only guard survived the DDL

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Set a status column outside its C# enum | refused, naming the domain constraint | `ERROR: new row for relation "audit" violates check constraint "ck_audit_status_domain"` | **Pass** |
| Ledger `action` outside its literal set | refused | `ERROR: new row for relation "field_change" violates check constraint "ck_field_change_action_domain"` | **Pass** |
| Malformed integrity hash (wrong case) | refused | `ERROR: new row for relation "file_reference" violates check constraint "ck_file_reference_sha256_sha256"` | **Pass** |
| **UPDATE an append-only ledger row after the Phase-3 DDL** | still refused by the trigger | `ERROR: audit ledgers are append-only CONTEXT: PL/pgSQL function audit.reject_mutation() line 3 at RAISE` | **Pass** |

### OQ-DB-04 — Keys are tenant-first and partition-ready

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Tables with NOT NULL tenant still on a single-column PK | 0 | `0` | **Pass** |
| Tenant-first composite primary keys | 88 | `88` | **Pass** |
| The nullable-tenant tables retain single-column keys | the 4 known tables | `field_change, outbox_event, security_event, user_account` | **Pass** |

### OQ-DB-05 — The bound moved to the API when the column became `text`

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Reject an NC with a 1500-character reason (former column bound 1000) | 400, validation error naming the field | `HTTP 400; {"Reason": ["The length of 'Reason' must be 1000 characters or fewer. You entered 1500 characters."]}` | **Pass** |
| Control: the same field within bound | accepted | `HTTP 204` | **Pass** |

### OQ-DB-06 — Identifier length

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Identifier lengths across `pg_class` and `pg_constraint` | none exceeds 62 | `0 over the limit; 62 (longest identifier)` | **Pass** |

> The longest identifier sits **exactly at 62** — the self-imposed limit, one byte inside
> PostgreSQL's 63. There is no headroom left, which is precisely why the abbreviation map in
> `CLAUDE.md` §5 is a rule rather than a suggestion.

### OQ-DB-07 — Owned-child changes reach the owning tenant's ledger

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Field changes written by provisioning (elevated path) | all attributed, none NULL | `0 unattributed of 2144 total` | **Pass** |
| Tenant A opens its own field-change ledger over HTTP | its privilege detail is visible | `HTTP 200; 407 RolePermission rows visible of 500 returned` | **Pass** |
| Tenant A queries for another tenant's field changes | 0 | `0` | **Pass** |

### OQ-DB-08 — Referential integrity does not depend on ORM insert order

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Provision a laboratory (tenant + administrator + outbox events in one transaction) | 201, no `23503` | `HTTP 201` for both laboratories; no foreign-key violation | **Pass** |
| Confirm the mechanism | all 5 tenant FKs deferred to COMMIT | `fk_branch_tenant`, `fk_kpi_snapshot_tenant`, `fk_outbox_event_tenant`, `fk_ref_counter_tenant`, `fk_user_account_tenant` — all `deferrable=true initdeferred=true` | **Pass** |

---

## 2. Observations (not defects)

| Ref | Observation |
| --- | ----------- |
| OBS-DB-1 | The composite FK's actual name is `fk_capa_action_nonconformance_tenant_id_nc_id` — EF regenerated it during Phase 5, superseding the `fk_capa_action_nonconformance_tenant` name Phase 4 created. Both the constraint and its behaviour are correct; only the name differs from the Phase-4 commit message. |
| OBS-DB-2 | The longest database identifier is exactly 62 characters (OQ-DB-06). The convention holds, with zero margin. |
| OBS-DB-3 | These cases were executed against a database that has accumulated dev data across many sessions (2 144 recent field-change rows). A qualified-environment execution will see smaller numbers; the assertions are shaped as "none unattributed" rather than exact counts so they remain valid there. |

## 3. Result summary

- **Cases executed: 8 of 8** (OQ-DB-01 … OQ-DB-08), comprising **23 recorded checks**.
- **Passed: 23. Failed: 0. Deviations: 0. Defects raised: 0.**
- Three cases were deliberately written with a **control step** (OQ-DB-02 own-parent insert,
  OQ-DB-05 within-bound field) so that a constraint which refuses *everything* could not be
  mistaken for one that discriminates correctly.
- No test in this session revealed a functional or data-integrity defect. Two harness problems
  encountered during preparation — a `set_config` echo leaking into transcribed output, and a
  first-draft cross-tenant probe that proved only the read fence rather than the foreign key —
  were operator issues, were corrected, and the affected cases were re-executed to completion.

## 4. What this record does and does not close

**Closes:** the A.10 execution gap — OQ-DB-01…08 are no longer Template. URS-100…107 now carry
executed evidence rather than engineering assertion alone.

**Does not close:** **DOC-001**. This is a development workstation, and the signature lines below
are unsigned. It also does not touch **SEC-001** (independent penetration test) or **OPS-001**
(staging observability and load).

## 5. Signatures

By signing, the witness confirms that the cases in §1 were executed in their presence, that the
transcribed actual results match what was observed, and that the limitations in the scope
statement and the observations in §2 have been read and dispositioned.

| Role | Name | Signature | Date |
| ---- | ---- | --------- | ---- |
| Executed by (operator) | Engineering — Claude Code (automated operator) | *n/a — machine-executed; results transcribed verbatim* | 2026-08-01 |
| Witnessed by | A. Awad (System Owner / acting QA authority) | ____________________ | __________ |
| Reviewed & approved by (QA) | | ____________________ | __________ |

> Engineering applies no signature on QA's or the System Owner's behalf. Until the witness line is
> signed, this document is an **unsigned execution transcript**: the results are real, the
> attestation is pending.
