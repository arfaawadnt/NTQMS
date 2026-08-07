# NT.QAMS — AS-BUILT Review · Document 15 · Final Evidence Reconciliation & Documentation QA

| Field | Value |
|---|---|
| Series / contract | AS-BUILT Review per [`00_REVIEW_MANIFEST.md`](00_REVIEW_MANIFEST.md) (v1.1) — **closing document** |
| Owner prompt | 15 — Final Evidence Reconciliation & Documentation QA |
| Commit reviewed | `d74d4bff733d49361a1b4da1e1b3ef3e8338af06` (`master`) — baseline; no drift across all 16 documents |
| Review date | 2026-08-02 |
| Method | **Documentation QA pass over Documents 00–14, not a new architecture analysis.** Cross-document count reconciliation, contradiction detection, redaction sweep, and Critical/High-risk consistency check. Corrections are **reported explicitly — no prior document was silently edited.** |

---

## 1. QA verdict

**The review pack is internally consistent, fully redacted, and complete.** All 16 documents (00–15) exist and carry source evidence; the headline counts (tables, routes, controllers, tests, migrations, permission keys, FORCE-RLS, SoD rules) are **identical across every document that cites them**; no credential value appears anywhere in the pack. Four minor reconciliation items and one **severity-escalation** are reported in §2 — none changes a material conclusion, and the escalation (e-signature gap) is a *correction toward greater severity* that the Executive Summary should adopt.

## 2. Contradiction register (with correction needed)

| # | Item | Where | Finding | Correction |
|---|---|---|---|---|
| **C-1** | **E-signature gap severity** | Doc 01 §8 (top-10 risks) vs Doc 12 (RISK-03 Critical/blocker) | Doc 01's executive top-10 lists MFA/HS256/FE as #3–5 but **does not rank the Part-11 e-signature-manifestation gap in the top 3**; Doc 08 (SEC-01) and Doc 12 (RISK-03) later rate it **Critical / release-blocking**. The Executive Summary predates the deep Part 11 pass (Docs 03/07/08). | **Elevate the e-signature-ceremony gap (RISK-03/NB-03-02) to a top-3 executive risk.** The Risk Register (Doc 12) is authoritative; treat Doc 01 §8 as superseded on this ranking. *(Reported, not silently edited.)* **Now moot — RISK-03 CLOSED 2026-08-07.** |
| C-2 | Permission-key count | Docs 01/03/06 (**170**) vs one requirement extraction in Doc 11 source (171) | Review docs consistently say **170** (matches `CLAUDE.md`); a single upstream requirement note said 171. | Canonical value **170**; the 171 is an upstream requirement-doc figure, not a review count. |
| C-3 | Module count framing | Doc 06 ("34 modules" matrix) vs Doc 02 ("19 Domain folders / 28 feature folders") | Not a contradiction — Doc 06's 34 rows enumerate business modules **including sub-modules** (Calibration, Maintenance, Privileges) and cross-layer groupings; Doc 02 counts source folders. | Clarify in reading: **19 Domain modules / 28 frontend features / 34 business-capability rows** — three different valid enumerations. |
| C-4 | FORCE-RLS / composite-PK counts | 93 / 91 (v1.52, reviewed commit) vs 90 / 88 (v1.51.2 hardening snapshot) | Both appear across Docs 04/08/11 — always **version-labelled**. | No conflict; reviewed commit is v1.52 → **93 FORCE-RLS, 91 composite PKs** are the as-built figures. |
| C-5 | Risk-register completeness | Doc 12 §2 table vs Appendix A | Complaint role-literal masking (SEC-09/NB-03-06) is dispositioned as **RISK-36** in Doc 12 Appendix A but not added as a row in the §2 table. | Treat **RISK-36 (Med, Improvement module)** as a live register entry; add to the table in any register revision. |

**Not review-doc contradictions (correctly catalogued as repo-doc drift):** `CLAUDE.md` says v1.52.0 but newest tag is v1.51.2 (OBS-01); README/ONBOARDING say Angular 18 (OBS-03/04); repo RTM overstates/understates 4 items (NB-11-01); the target docs carry 3 internal numeric inconsistencies (Doc 13). These are findings *about the repo*, not inconsistencies *within this review*.

## 3. QA checks (Prompt-15 mandated)

| Check | Result |
|---|---|
| Contradictory counts (projects/endpoints/tables/tests/modules) | **Consistent** — see §5; only the framing note C-3 |
| Unsupported claims / no source evidence | None found — every material claim carries a `file:line` or a cross-doc reference; runtime claims are confidence-capped and labelled |
| UI-presence-confused-with-end-to-end | **None** — Doc 06 explicitly distinguishes API-backed (27/28) from UI-only (Manual by design); parity was adversarially verified (Doc 03) |
| Unredacted credentials/secrets | **None** — repo-wide scan of all 15 docs returns **zero credential values**; dev/CI literals shown as `<REDACTED>` |
| Diagram/table consistency | Mermaid diagrams (Docs 02/05/06/07/08/10/12/13) agree with their tables; no diagram asserts a count not in its doc |
| Requirement-status consistency across module/workflow/API/DB/security/traceability docs | **Consistent** — e.g. e-signature = "document-publish-only" in Docs 03/06/07/08/11; PT ungated+untested in Docs 03/06/09; MFA-off in Docs 01/08/11/13 |
| Missing artifacts / empty sections | **None** — all 16 documents present, each with its required sections + attestation |
| Critical/High risks in Exec Summary AND Risk Register | **One escalation (C-1)** — e-signature gap is Critical in Doc 12 but not top-3 in Doc 01; otherwise consistent (DOC-001/SEC-001/OPS-001/MFA/HS256/FE all in both) |

## 4. Evidence coverage score per artifact

Scored on: source-citation density, adversarial verification, and independence from repo documentation. (H = High, M = Medium)

| Doc | Title | Evidence score | Notes |
|---|---|---|---|
| 00 | Review Manifest | H | baseline verified; migration count self-corrected (OBS-02) |
| 01 | Executive Summary | H | 6-sweep + 18 adversarial verifications (13 confirmed/5 adjusted/0 refuted); C-1 escalation applies |
| 02 | Repository & Architecture Map | H | reuses verified Doc 01 evidence + fresh dup/marker scans |
| 03 | Backend & API Inventory | H | 5 agents over all 54 controllers; parity adversarially re-verified |
| 04 | Database Deep Audit | H | 3 agents over 59 migrations + snapshot; static-cap on runtime RLS noted |
| 05 | Frontend Deep Audit | H | 3 agents; counts cross-checked |
| 06 | Business Module Coverage | H | synthesis of verified 03/04/05 |
| 07 | Workflows & Business Rules | H | state enums read from aggregates (one agent re-run after a stall) |
| 08 | Security & Compliance | H | 2 agents ran the mandated special searches; 0 code blockers |
| 09 | Testing & CI/CD | H | full CI + test-class inventory; pass/fail labelled Documentation-only |
| 10 | Integrations & Observability | H | 1 agent over Email/Jobs/Outbox/OTel/deploy |
| 11 | Requirements Traceability | H (M on repo-RTM claims) | 122 URS traced; divergences from repo RTM flagged |
| 12 | Risk Register | H | every row cites its owning doc |
| 13 | Target Conformance | H | target decisions extracted from the 3 approved docs |
| 14 | Reviewer Onboarding | H (commands **[discovered]**, unverified) | nothing executed — correctly labelled |
| 15 | This QA pass | H | cross-doc reconciliation |

**Pack-level coverage: High.** The one systematic cap is that **nothing was executed** — runtime behavior (RLS enforcement, job cadences, health, performance) is reconstructed from source + the CI integration suite, never observed. This is disclosed in every document and is precisely the DOC-001/OPS-001 gap.

## 5. Final count summary (reconciled, with confidence)

| Metric | As-built value | Confidence | Target (Doc 13) |
|---|---|---|---|
| Solution projects | 11 in `.sln` (6 src + 5 test) + LoadTests outside | High | 6 src + 5 test |
| Bounded-context modules | 19 Domain / 28 frontend features / **34 capability rows** | High | 14 contexts |
| Aggregates | ~55 types | High | 27 (headline) |
| Controllers / routes | **54 controllers / 333 routes** (666 with version mirror) | High | ~32 / ~200 |
| Commands / queries | **146 / 71** | High | 111 / 71 |
| Validators / permission keys | 90 / **170** | High | ~115 / ~70 |
| Migrations | **59** | High | — |
| Tables / schemas | **99 / 4** (qams 92, audit 4, saas 2, read 1) | High | ~73 / 5 |
| FORCE-RLS tables / composite PKs / CHECKs | **93 / 91 / 87** (v1.52) | High (static) | every table / composite / ~90 |
| SoD rules | **10 pairs** | High | 5 |
| Backend tests (static) | **395** methods (Domain 228 / App 55 / Arch 10 / Integ 26 / Func 76) | High | — |
| Frontend tests | **87** unit `it()` / **6** Playwright | High | — |
| CLAUDE.md "460 backend" | Documentation-only (Theory expansion of 395) | Low | — |
| Modules Fully Implemented | **26 / 34** | High | — |
| E-signature-manifesting gates | **1 of ~20** (document publish) | High | all signed records |

## 6. Sign-off checklist (per review discipline)

| Discipline | Ready to sign? | Blocking items |
|---|---|---|
| **Architecture review** | ✅ with 2 ratifications | Ratify AOD-2 (no-Redis) and AOD-6 (`ref` schema) with ADRs; otherwise conformant (Doc 13, ~85% as-designed) |
| **Security review** | ⚠ **not yet** | SEC-001 pentest (blocker); SEC-02 MFA default (AOD-1); SEC-03 file-download gate; SEC-05 key rotation (Doc 08) |
| **Database review** | ✅ | Clean 3NF, RLS + hardening complete; B9/B10 formally accepted; TRUNCATE note (NB-04-02) low (Doc 04) |
| **QA review** | ⚠ **not yet** | AQ HTTP surface untested (T-1); FE coverage (T-2); regulated e2e not in CI (T-3); no coverage gate (Doc 09) |
| **Product / Compliance review** | ⚠ **not yet** | DOC-001 signed validation (blocker); validation on qualified env (Doc 08/11/12). *RISK-03 e-signature ceremony scope — **CLOSED 2026-08-07**, no longer outstanding.* |
| **Operations review** | ⚠ **not yet** | OPS-001 staging observability + load/soak; alerts not deployed; trace/log backend unwired (Doc 10) |

**Overall: Pre-production (Doc 01 verdict confirmed).** Database and architecture are sign-off-ready; security, QA, compliance, and operations each have named, tracked gate items — none a code-execution blocker. **RISK-03 (the Part 11 e-signature ceremony) is now CLOSED (2026-08-07)**, leaving **two** items genuinely release-gating for a regulated launch: DOC-001 (signed validation on a qualified environment) and SEC-001 (independent penetration test).

## 7. Top 20 questions to resolve before any production release

**Compliance / validation (release-gating):**
1. When will IQ/OQ/PQ be executed and **signed on a qualified environment** (closing DOC-001)? OQ transcripts 12/13 are currently unsigned.
2. Is Part 11 e-signature on **document-publish-only** acceptable, or must audit/NC/quality-policy/change/review/AQ sign-offs also mint `signature_record`s (RISK-03)? — *was the top code question; **RESOLVED 2026-08-07**: the System Owner directed full scope, and the ceremony now mints a `signature_record` on every signed-record gate.*
3. Are deviations **B9** (two tables without RLS) and **B10** (historical nil-tenant rows) formally signed off for the regulated release?
4. Who performs the **independent penetration test** (SEC-001), and when?

**Security / architecture-owner decisions (Doc 13):**
5. **MFA policy (AOD-1):** target mandates MFA for all active accounts; build defaults it off — which is the production posture?
6. Is **local-disk file storage** acceptable for production, or is S3 + virus-scan + WORM required (AOD-3)?
7. Ratify the **no-Redis, DB-read-per-request** privilege model (AOD-2) — or add a cache for scale?
8. **JWT key rotation** (SEC-05): confirm the single HS256 secret rotation procedure (no `kid`).
9. Should **file download** be permission-gated and audited before GA (SEC-03)?
10. Is `X-Forwarded-*` restricted to the real proxy in the deployment (NB-08-03)?

**QA / testing:**
11. When will the **Analytical-Quality HTTP surface** (107 routes, incl. PT-result→NC) get functional tests and permission gates (T-1/SEC-04)?
12. Will the **full-stack regulated e2e** run in CI against a seeded API (T-3)?
13. Will **coverage thresholds** be added (frontend especially) (T-2/T-6)?

**Operations:**
14. When is **staging observability + ≥100-VU load + 24h soak** scheduled (OPS-001)? The 7 alerts are defined but not deployed.
15. Should **SMTP** get send-timeout + retry/re-drive, or is best-effort-with-in-app-feed the accepted posture (NB-10-01)?
16. When is a **persistent trace/log backend** wired (currently `debug` exporter only, NB-10-02)?

**Hygiene / governance:**
17. Tag **v1.52.0** and reconcile the stale README/ONBOARDING/RTM (OBS-01/03/04, NB-11-01).
18. Pin NuGet versions + add `global.json` for reproducible restores (RISK-20).
19. Rename the `TestAuthorization` `AUTHZ-*` domain codes so validation errors stop mis-mapping to HTTP 403 (RISK-23).
20. Remove the byte-identical duplicate SRS HTML (`NT_QMS_Complete_SRS_Sameh.html`, RISK-35) and confirm which SRS is authoritative.

## 8. What a reviewer can now answer from the files alone

Per the acceptance criteria, the pack lets a reviewer answer, from documents 00–15:
- **What runs:** a .NET 9 / Angular 22 multi-tenant lab QMS, API-first, 27/28 features end-to-end (Docs 01/06).
- **What persists:** 99 relational tables under FORCE RLS + content-addressed file objects; no client-side business data (Doc 04/05).
- **Who can access it:** deny-by-default permission catalogue (170 keys) + tenant RLS + branch/dept scope; MFA available but off by default (Docs 03/08).
- **What is enforced:** guarded state machines, 10 SoD rules, hash-chained immutable audit trail, e-signature (document publish); the gaps are named (Docs 07/08).
- **What is merely a screen:** exactly one — the Manual (static by design); everything else is API-backed (Doc 06).

---

## Appendix A — Series completeness

All 16 documents delivered (00–15), each at baseline `d74d4bf`, each carrying its evidence-class legend and no-modification attestation. Manifest Appendix-A observations (OBS-01…10) and all `NB-xx` findings are dispositioned into the Doc 12 register (RISK-01…36). The review wrote **only** to `docs/as-built-review/` — 16 files, one folder, zero application-code changes.

## Appendix B — Reviewer no-modification attestation (manifest §8 model) — FINAL, covering the entire series

- [x] Across all 16 prompts, the review created, modified, or deleted **no** file under `src/`, `tests/`, `frontend/`, `scripts/`, `deploy/`, `.github/`, `.config/`, `docs/srs/`, `docs/validation/`, or `docs/reference/`.
- [x] No build, test, migration, restore, package, container, or database operation was executed at any point; no application was run or served; no penetration testing was performed. All runtime claims are source-reconstructed and confidence-capped.
- [x] The **only** writes across the entire series are the 16 documents in `docs/as-built-review/` (00–15). Verified: `git status` delta is exactly the one untracked folder; the pre-existing dirty files recorded in manifest §1.2 were never touched.
- [x] **No secret value appears in any of the 16 documents** — repo-wide scan confirms zero credential literals; all dev/CI/test values are shown as `<REDACTED>` or by key name only.
- [x] Nothing was invented; every material claim across the series traces to a repository `file:line`, an adversarially-verified finding, or a cited requirement/target source. UI labels, SRS/RTM claims, diagrams, and comments were treated as requirements references, never as proof of implementation.
- [x] This review **documents** the system as-built; it **does not certify** regulatory compliance, security, or release-readiness — those require the signed validation (DOC-001), independent pentest (SEC-001), and operational assurance (OPS-001) that remain open.

---

*End of Document 15 and of the NT.QAMS AS-BUILT Review series (Documents 00–15). Reviewed at manifest baseline `d74d4bf` (no drift). Verdict: **Pre-production** — engineering-complete and high-conformance, gated for a regulated launch by validation, independent security testing, and operational assurance. The Part 11 e-signature-ceremony scope (RISK-03) has since been **CLOSED** (2026-08-07) and is no longer a gate.*
