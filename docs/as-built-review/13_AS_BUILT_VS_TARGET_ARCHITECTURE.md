# NT.QAMS — AS-BUILT Review · Document 13 · As-Built vs Target Architecture Conformance

| Field | Value |
|---|---|
| Series / contract | AS-BUILT Review per [`00_REVIEW_MANIFEST.md`](00_REVIEW_MANIFEST.md) (v1.1) |
| Owner prompt | 13 — Target Architecture Conformance Gap Analysis |
| Commit reviewed | `d74d4bff733d49361a1b4da1e1b3ef3e8338af06` (`master`) — **identical to the manifest baseline; no drift** |
| Review date | 2026-08-02 |
| Approved target baseline | `D:\SAAS\QAMS\21-7\NT_QAMS_{Application_Architecture, Database_Architecture, Domain_Model}.md` (all "Designed 2026-07-21"; byte-identical copies verified at `docs/reference/`) — user-selected in the manifest |
| Method | Target decisions extracted from the three target docs; compared against as-built evidence from Documents 01–12. **Comparison only — no redesign of the target, no code changes.** |

**Classification (per Prompt 13):** each gap is one of — **Implemented-as-designed** · **Implemented-differently (justified)** · **Implemented-differently (unjustified drift)** · **Missing** · **Added-beyond-target**. A divergence counts as **justified only if a written record exists** (ADR, `IMPLEMENTATION_LOG.md`, or a CLAUDE.md rule); silent drift is flagged distinctly. The matrix's Conformance column collapses these to **Implemented / Partial / Absent / Contradicted**.

> The target docs carry three internal numeric inconsistencies (aggregates "27" headline vs ~39 enumerated; qams children "23" vs 21; controllers "≈32" vs "~33"). These are the *target's own* figures; where they matter I compare against the enumerated detail, not the headline.

---

## 1. Executive conformance summary

The as-built is a **faithful realization of the target's core architecture, expanded along the approved roadmap, with three genuine divergences that need an architecture-owner ruling.** Clean Architecture, CQRS/MediatR, modular monolith, multi-tenant FORCE RLS, UUIDv7 composite keys, hash-chained audit ledger, `ref_counter` numbering, deny-by-default privilege authorization, SoD, and the outbox saga fabric are all **Implemented-as-designed**. The build went **beyond** the target on the Analytical-Quality context (the target postponed it to "Phase 9") and Quality Analytics reporting — legitimate roadmap advancement.

**The three divergences requiring an owner decision (§4):** (1) **MFA — target mandates it for all active accounts; as-built defaults it off** (the one clear *Contradiction*); (2) **infrastructure stack — SignalR/Hangfire/Redis in the target, none present as-built** (mostly justified substitutions, one unjustified); (3) **file storage — target S3 + virus-scan + WORM; as-built local disk, no AV** (deferred).

## 2. Conformance matrix

| # | Approved target decision | As-built evidence (doc) | Classification | Conformance | Exact gap / note |
|---|---|---|---|---|---|
| **Stack** |
| 2.1 | .NET 9 · ASP.NET Core · Clean Architecture · CQRS/MediatR · EF Core 9 · PostgreSQL 17 | identical (Doc02) | Implemented-as-designed | Implemented | — |
| 2.2 | **Angular 18** | **Angular 22** (Doc05) | Implemented-differently (**justified** — `IMPLEMENTATION_LOG`/CI closed 10 npm advisories via 18→22) | Implemented | version advanced; stale metadata (OBS-04) |
| 2.3 | **SignalR** (real-time push) | **Absent** — no hub; `/hubs` proxy route reserved unused (Doc02/10) | Missing (**justified** — README/deploy list as future work; ADR-0007 same-origin) | Absent | real-time notification deferred |
| 2.4 | **Hangfire** (background jobs) | **Absent** — 5 in-process `IHostedService`s + transactional outbox (Doc10) | Implemented-differently (**justified** — ADR-0006 outbox reliability, ADR-0001 single-replica) | Implemented | job engine substituted, functionally equivalent |
| 2.5 | **Redis** (privilege cache + token-claimless authz) | **Absent** — no distributed cache; `IUserPrivileges` re-reads DB per request (Doc03/08) | Implemented-differently (**partially justified** — the DB re-read is *stronger* for revocation URS-006, but no ADR records the no-Redis choice) | Partial | scale ceiling unproven; ratify or add cache |
| 2.6 | S3 storage, ClamAV virus scan, WORM bucket | **local disk content-addressed**, S3 aspiration-only, **no AV** (Doc04/08) | Missing (deferred) | Partial | S3 + AV + WORM not built (SEC-07/RISK-17) |
| 2.7 | QuestPDF (PDF), ClosedXML (Excel) | identical (Doc02) | Implemented-as-designed | Implemented | — |
| **Structure** |
| 2.8 | Modular monolith, 6 src projects, inward dependency rule, contexts-as-folders enforced by Architecture.Tests | identical — 6 projects, folders, NetArchTest merge gates (Doc02) | Implemented-as-designed | Implemented | — |
| 2.9 | SharedKernel = TenantId/UserRef/LocalizedText + abstractions, no business logic | as-built SharedKernel matches (Doc02) | Implemented-as-designed | Implemented | — |
| **Domain** |
| 2.10 | 14 bounded contexts (6 core/4 supporting/4 generic) | all 14 present; as-built has **19 Domain module folders** (finer split) (Doc02/06) | Added-beyond-target (finer modularization) | Implemented | more granular, all target contexts covered |
| 2.11 | 27 aggregates (target headline) | ~55 aggregate types as-built (Doc06) | Added-beyond-target | Implemented | AQ build-out expanded the count |
| 2.12 | LIMS / EHS excluded; AI Copilot excluded | both absent as-built (Doc06) | Implemented-as-designed (deferrals honored) | Implemented | — |
| 2.13 | Reporting **not** a bounded context (read models only) | as-built adds `QualityHealthProfile` **aggregate** (Reporting) (Doc04/06) | Implemented-differently (**justified** — v1.52 URS-110/111 needs a persisted weighted config) | Partial | mild contradiction of "read-models-only"; ratify |
| **Database** |
| 2.14 | 5 schemas: saas, qams, **ref**, audit, read | **4 schemas** — qams, audit, saas, read; **no `ref`** (ref data in `qams.lov_entry`) (Doc04) | Implemented-differently (**unjustified drift** — no ADR for merging `ref` into `qams`) | Partial | reference data not schema-isolated |
| 2.15 | ~73 tables | **99 tables** (Doc04) | Added-beyond-target (AQ + hardening) | Implemented | expansion, not a gap |
| 2.16 | RLS enabled + forced on **every** qams/audit/read table | **93 FORCE-RLS**; 2 exceptions `user_account`,`outbox_event` (B9) (Doc04) | Implemented-differently (**justified** — B9 accepted deviation, `SCHEMA-HARDENING-REPORT §8`) | Partial | 2 tenant tables discipline-not-structure |
| 2.17 | UUIDv7 app-side, composite `(tenant_id, id)` keys | identical — 91 composite PKs, `ValueGeneratedNever` (Doc04) | Implemented-as-designed | Implemented | — |
| 2.18 | Two-layer audit: SHA-256 hash-chained `audit_trail` (interceptor, same txn) + business ledgers | identical + richer chain input `(prev|seq|eventId|type|payload|ts)` (Doc04) | Implemented-as-designed | Implemented | — |
| 2.19 | `ref_counter (tenant, ref_type, year)` atomic `UPDATE…RETURNING` | identical (Doc04) | Implemented-as-designed | Implemented | — |
| 2.20 | Soft-delete 4 classes; global `ON DELETE RESTRICT` | no-hard-delete + status/is_active; cascades intra-aggregate only (Doc04) | Implemented-as-designed | Implemented | — |
| 2.21 | 3 **partitioned** tables (audit_trail, qc_run, notification_dispatch) | schema is **partition-ready** (tenant-first PK) but **no table is actually partitioned** (Doc04) | Missing (deferred; schema-ready) | Absent | partitioning not applied |
| 2.22 | ~118 FKs, ~205-225 indexes, ~90 CHECKs | ~60 FKs, 148 indexes, 87 CHECKs (Doc04) | Implemented-differently | Partial | fewer FKs (authorship/file refs bare-Guid by design, NB-04-01); CHECK count on target |
| **CQRS / API** |
| 2.23 | 111 commands / **71 queries** / ~222 handlers | **146 commands / 71 queries** / 214 policies (Doc03) | Added-beyond-target | Implemented | **queries match exactly**; commands expanded by AQ |
| 2.24 | ~32 controllers / ~200 endpoints | **54 controllers / 333 routes** (Doc03) | Added-beyond-target | Implemented | AQ 16 controllers + versioning dual-route |
| 2.25 | 10 process managers + 5 capability services; event sourcing rejected | outbox sagas (ComplaintToNc, FindingToNc, PtToNc, escalation, notification, ledger appender, provisioning) + services; no event sourcing (Doc07/10) | Implemented-as-designed | Implemented | SignalR projection engine (2.3) is the one absent piece |
| 2.26 | 9 domain services (ReferenceNumberGenerator, SoD, ESignature, WestgardEvaluator, ZScore, EquipmentLockout, Retention, RiskScoring, SlaClock) | all present in domain (Doc07) | Implemented-as-designed | Implemented | — |
| **Authorization** |
| 2.27 | Privilege catalog **~70 codes**, `OBJECT.ACTION`, deny-by-default, MediatR authz behavior | **170-key** catalogue, deny-by-default, `AuthorizationBehavior` (Doc03) | Implemented-differently (**justified** — v1.51.0 dynamic-role module, CLAUDE.md rule 9) | Implemented | catalogue evolved finer |
| 2.28 | **No roles/privileges in token**; per-request privilege eval | JWT carries a **role claim** (for synchronous guard) but permissions re-read from DB per request (Doc03/08) | Implemented-differently (**justified** — platform/tenant tier needs synchronous resolution at bootstrap) | Partial | role tier in token; permissions are not |
| 2.29 | Org-scope row-level (`user_org_scope`) | branch/department hard filter (`user_branch_access`/`user_department_access`) (Doc03) | Implemented-as-designed | Implemented | — |
| 2.30 | SoD + state-machine guards in aggregates (5 rules) | 10 SoD pairs in aggregates (Doc07) | Added-beyond-target (more SoD) | Implemented | change-approve is the one gate without SoD |
| 2.31 | **MFA (TOTP) mandatory for all active accounts** | **MFA off by default**, per-tenant/per-role opt-in (Doc08 SEC-02) | Implemented-differently (**unjustified drift** — code comment says "on in production" but no artifact sets it; contradicts the target's "mandatory") | **Contradicted** | **the sharpest conformance gap** |
| 2.32 | 8 canonical seed roles, tenant-editable | 5 seed roles + dynamic tenant roles over the catalogue (Doc03) | Implemented-differently (**justified** — v1.51.0 dynamic roles supersede fixed seeds) | Implemented | fewer fixed seeds, but tenant-editable as designed |
| 2.33 | Lockout 5/30, JWT 15-min + rotating server-revocable refresh | identical (ADR-0009) (Doc08) | Implemented-as-designed | Implemented | — |

## 3. Conformance tally

| Classification | Count (of 33 decisions) |
|---|---|
| Implemented-as-designed | 16 |
| Added-beyond-target (roadmap-legitimate) | 6 |
| Implemented-differently — **justified** (ADR/log/rule) | 6 |
| Implemented-differently — **unjustified drift** | 2 (2.14 `ref` schema, 2.31 MFA) |
| Missing / deferred | 3 (2.3 SignalR, 2.6 S3/AV, 2.21 partitioning) |
| **Contradicted** | **1** (2.31 MFA mandatory) |

**~85% of target decisions are Implemented-as-designed or a justified/roadmap variant.** The two unjustified drifts are the `ref`-schema merge (cosmetic) and the MFA default (material). No decision is silently reversed except MFA.

## 4. Conflicts requiring an architecture-owner decision (not developer interpretation)

| # | Conflict | Target | As-built | Decision needed |
|---|---|---|---|---|
| **AOD-1** | **MFA enforcement** | mandatory for all active accounts (App Arch:242) | off by default; per-tenant/role opt-in; reference prod compose never sets it | **Ratify the as-built opt-in (and document the rationale + set it on in reference prod), or restore the target's mandatory-MFA.** This is the one true contradiction and ties to RISK-04/SEC-02. |
| **AOD-2** | **Privilege caching / token model** | Redis-cached PrivilegeEvaluator, no privileges in token | no Redis; DB re-read per request; role tier in token | Ratify the no-Redis DB-read (stronger for revocation, adequate for single-replica) or mandate a cache for scale. Record an ADR either way. |
| **AOD-3** | **File storage** | S3-compatible + ClamAV + WORM object-lock | local disk content-addressed, no AV, no WORM | Decide whether local disk is acceptable for the production tier or S3+AV+WORM is required before GA (RISK-17). |
| **AOD-4** | **Reporting as an aggregate** | Reporting is read-models-only, not a bounded context | `QualityHealthProfile` is a persisted aggregate | Ratify the v1.52 weighted-config aggregate as a sanctioned exception to "read-models-only." |
| **AOD-5** | **Partitioning** | audit_trail, qc_run, notification_dispatch partitioned | partition-ready but not partitioned | Decide the volume trigger for applying partitioning (schema is already prepared). |
| **AOD-6** | **`ref` schema** | separate `ref` schema for platform catalogs | reference data in `qams.lov_entry` | Ratify the merge or split reference data into `ref` (low urgency, cosmetic). |

## 5. Phased implementation delta (aligned to the approved roadmap — not a re-plan)

The target's own roadmap postponed Analytical Quality to Phase 9 and listed S3/AI/LIMS as later — the as-built has already advanced past several of those. The remaining delta to reach full target conformance, sequenced to the approved phase model:

1. **Close the contradiction (AOD-1):** set MFA per the owner's ruling — the only Contradicted item. (Ties to the RISK-04 quick win.)
2. **Ratify or record the justified substitutions (AOD-2, AOD-4, AOD-6):** write ADRs for no-Redis, the Reporting aggregate, and the `ref`-schema merge, converting "unjustified drift" into recorded decisions.
3. **File-storage tier (AOD-3):** if the owner requires the target's S3+AV+WORM, this is a bounded Infrastructure adapter swap (`IFileStorage` already abstracts it — the target's own design point) plus an AV integration.
4. **Deferred-by-roadmap items:** SignalR real-time push (2.3) and table partitioning (2.21) remain the target's own later-phase work — no conformance action needed until their trigger conditions (real-time requirement / volume) arrive.
5. **Everything else is conformant or beyond target** — no delta.

## 6. Assessment

Measured against its own approved architecture, the build is **high-conformance and honestly expanded.** The target's foundational decisions — Clean Architecture, CQRS, modular monolith, FORCE RLS, UUIDv7 composite keys, hash-chained ledger, deny-by-default privilege authz, SoD, outbox sagas — are realized as designed and enforced by merge gates. The expansions (Analytical Quality, Quality Analytics, finer modules, 170-key catalogue) are roadmap-legitimate and recorded. The substitutions (Hangfire→hosted services, Angular 18→22) are ADR/log-justified. **The single material conformance breach is MFA defaulting off against a target that mandates it (AOD-1)** — a config decision, not an architectural one, and already tracked as RISK-04. The `ref`-schema merge and the no-Redis choice are the two decisions that lack a written record and should be ratified with ADRs. Nothing in the target was silently abandoned.

---

## Appendix A — Observation carry-forward

| ID | Note |
|---|---|
| **NB-13-01** (new) | MFA-off-by-default **Contradicts** the target's "MFA mandatory for all active accounts" — architecture-owner decision AOD-1; ties to RISK-04/SEC-02. Doc 15. |
| **NB-13-02** (new) | Two divergences lack a written justification record: `ref`-schema merge (AOD-6) and no-Redis privilege eval (AOD-2). Recommend ADRs. Doc 15. |
| Target doc inconsistencies | Aggregates 27-vs-39, children 23-vs-21, controllers 32-vs-33 are internal to the target docs — flag for the target's own maintainers. |
| Deferrals honored | LIMS/EHS/AI-Copilot correctly absent; the AQ and Reporting build-outs advanced the target's Phase 9 roadmap. |

## Appendix B — Reviewer no-modification attestation (manifest §8 model)

- [x] No file was created, modified, or deleted; nothing was built, run, or connected to a database. The target docs were read, not modified; no redesign of the target was performed.
- [x] Only read-only access was used (target-doc extraction + synthesis of Docs 01–12).
- [x] The only filesystem write is this document: `docs/as-built-review/13_AS_BUILT_VS_TARGET_ARCHITECTURE.md`.
- [x] No secret values reproduced.
- [x] Nothing invented — every as-built claim cites its owning review document; every target claim cites the target doc; justification classifications require a named ADR/log/rule.

---

*End of Document 13. Reviewed at manifest baseline `d74d4bf` (no drift). Next: Prompt 14 → `14_REVIEWER_ONBOARDING_GUIDE.md`.*
