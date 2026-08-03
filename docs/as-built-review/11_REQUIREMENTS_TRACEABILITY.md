# NT.QAMS — AS-BUILT Review · Document 11 · Requirements Traceability

| Field | Value |
|---|---|
| Series / contract | AS-BUILT Review per [`00_REVIEW_MANIFEST.md`](00_REVIEW_MANIFEST.md) (v1.1) |
| Owner prompt | 11 — Requirements Traceability Matrix |
| Commit reviewed | `d74d4bff733d49361a1b4da1e1b3ef3e8338af06` (`master`) — **identical to the manifest baseline; no drift** |
| Review date | 2026-08-02 |
| Method | Requirement catalog extracted from `docs/validation/` (URS) + `docs/srs/` (FR/NFR, RTM); traced against **independently-verified as-built evidence from Documents 01–10** |

**The traceability bar (Prompt 11):** a requirement is **Fully Implemented only when UI + backend + data persistence + workflow enforcement + authorization + tests all have evidence appropriate to that requirement.** This is a *stricter, multi-layer* bar than the repo's own RTM, which grades on implementation presence. Where my as-built verdict diverges from the repo's status claim, §8 lists it explicitly. **Confidence:** High = ≥2 as-built artifacts (cross-referenced to the owning review document).

**Requirement scheme (as found):** `URS-nnn` (122 user requirements — baseline 001-055 authoritative, 056-122 in the revalidation delta), `FR-<MOD>-nn` (94 as-built functional rows), `NFR-<CAT>-nn` (~70 non-functional), `BR-*` business rules, `M-01…M-36` module codes. The repo's own RTM tallies **FR: 62 ✅ / 4 ⚠ / 3 🔒 / 25 ❌**; baseline URS **55/55 traced to design + verification, 47/55 with automated tests**; post-baseline URS-056-122 mostly **"executed (dev), unsigned."**

---

## 1. Method & status legend

**Actual status** (my as-built verdict, applying the multi-layer bar): **Fully Implemented** (all applicable layers incl. a test) · **Partially Implemented** (a layer thin/absent — usually tests or a compliance sub-control) · **Backend-only** (works server-side, no UI or vice-versa) · **Documentation-only** (claimed, no source evidence for a required layer) · **Missing**. Each row cites the owning review document for its evidence.

---

## 2. Functional QMS module requirements (representative)

| Req | UI | Backend | DB | Workflow | AuthZ | Test | Status | Gap | Conf |
|---|---|---|---|---|---|---|---|---|---|
| URS-025/027 Document lifecycle + versioning | ✓ (Doc05) | ✓ (Doc03) | ✓ (Doc04) | ✓ Draft→Published→Obsolete (Doc07) | ~ create ungated (Doc03) | ✓ domain (Doc09) | **Fully** | authoring ungated | High |
| URS-026 Publication e-signed | ✓ | ✓ IESignatureService | ✓ signature_record | ✓ publish ceremony | ✓ Sign | ~ **unit only, no HTTP test** (Doc09 T-9) | **Partially** | ceremony untested over HTTP | High |
| URS-028 Read-&-understand acknowledgement | ✓ | ✓ | ✓ document_acknowledgement | ✓ | ✓ | ✓ domain | **Fully** | FR-DOC-07 enforcement gap noted by repo | High |
| URS-030/032 NC/CAPA lifecycle, 4 event types | ✓ | ✓ 12 actions | ✓ | ✓ 9-state (Doc07) | ~ 7/10 `[RequireInternalActor]` | ✓ domain | **Fully** | coarse authz | High |
| URS-031 SoD verifier≠raiser | — | ✓ | — | ✓ SOD-CAPA-002 | ✓ | ✓ domain | **Fully** | — | High |
| URS-034 Link audit findings & PT → NC | ✓ | ✓ sagas | ✓ | ✓ outbox policies | ✓ | ~ FindingToNc tested; **PtToNc path untested** (Doc09 T-1) | **Partially** | PT→NC ungated+untested | High |
| URS-035/036 QC Westgard verdict + limits | ✓ L-J chart | ✓ | ✓ qc_run | ✓ | ~ run capture ungated | ✓ `WestgardEvaluatorTests` | **Fully** | run-capture authz | High |
| URS-037 QC target change: reason, forward-only | ✓ | ✓ | ✓ | ✓ QC-012/013 | ~ (`PUT targets` = the UI-less orphan NB-03-01) | ✓ domain | **Partially** | **no UI caller** (Doc03/05) | High |
| URS-038/039 Method-validation studies + SoD sign-off | ✓ 16 registers | ✓ | ✓ 12 study tables | ✓ + SOD-AQ-001 | ~ child writes ungated | ~ domain math ✓, **HTTP surface untested** (Doc09 T-1) | **Partially** | AQ HTTP untested; sign-off no e-sig | High |
| URS-040 PT plans/results + escalation | ✓ | ✓ | ✓ | ✓ write-once → NC | ✗ **PT controller zero `[RequirePermission]`** | ✗ **untested** | **Partially** | ungated + untested (SEC-04) | High |
| URS-042/043/044 No-hard-delete / archive snapshot / legal hold | ✓ | ✓ | ✓ + grant-level append-only | ✓ ARC-002/013/015 | ~ retrieve/return ungated | ✓ `RecordsAndSlaTests` | **Fully** | circulation gates | High |
| URS-045/046 Retention export + immutable files | ✓ export-menu | ✓ | ✓ file_reference | ✓ | ~ **download ungated+unlogged (SEC-03)** | ✓ FileHardening (upload) | **Partially** | download authz/audit (NB-08-01) | High |
| URS-009/010 User provisioning + access review | ✓ | ✓ 10 actions | ✓ | ✓ | ✓ + lockout guard | ✓ `RolePrivilegeFlowTests` | **Fully** | reset-pw no security event (SEC-06) | High |
| URS-047/048/049/050 Change/Risk/Policy/Supplier+Review | ✓ | ✓ | ✓ | ✓ (Doc07) | ~ change-approve **no SoD** | ✓ domain | **Fully** | change-approve SoD gap | High |
| URS-052 Dashboards/KPIs | ✓ tabbed | ✓ 7 reads | ✓ kpi_snapshot | n/a | ✓ Reports.View | ~ `DashboardKpiTotals`; analytics endpoint untested | **Partially** | quality-analytics untested e2e | High |
| Notifications / Tasks (FR-NTF/TASK) | ✓ | ✓ | ✓ | ✓ | ~ task-complete no ownership | ✓ NotificationDispatcher | **Fully** | email best-effort (NB-10-01); task ownership | High |

**Repo-flagged not-implemented (25 FR ❌, corroborated as out-of-scope/expectation items, not regressions):** structured RCA templates (FR-NC-07), LJ/Passing-Bablok/Bland-Altman *charts* (FR-AQ-04 — note: the *math* is implemented, only the plotted charts differ from spec), LIS/instrument ingestion (FR-AQ-06), obsolete-PDF watermark (FR-DOC-06), session monitor/revoke UI (FR-USER-05), En/zeta PT scoring (FR-PT-03), et al. These are **Missing** as-built and correctly marked so by the repo RTM.

## 3. Multi-tenancy requirements

| Req | Backend | DB | Test | Status | Gap | Conf |
|---|---|---|---|---|---|---|
| URS-008/100 Every tenant table RLS forced, fail-closed | ✓ interceptor | ✓ 93 FORCE-RLS | ✓ `RlsTenantIsolationTests` (real PG) | **Fully** | **except** `user_account`,`outbox_event` (B9, accepted) | High |
| URS-101 Owned child readable only by parent's tenant | ✓ | ✓ composite FK | ✓ `OwnedChildTenancyTests` | **Fully** | — | High |
| URS-103 Partition-ready tenant-first PK | — | ✓ 91 composite PKs | ✓ structural sweep | **Fully** | — | High |
| URS-106 Change attributed to owning tenant incl. children | ✓ FieldChangeInterceptor | ✓ | ✓ `UserEventTenantStamp` | **Fully** | historical rows (B10) | High |
| URS-107 Deferrable tenant FKs (ORM-order-independent) | — | ✓ Hardening6 | ✓ provisioning 201 | **Fully** | — | High |
| BR-MT-10 4 nullable-tenant exceptions | — | ✓ | ✓ arch test | **Fully** (as designed) | `audit.security_event` RLS closed (Hardening2); `user_account`/`outbox_event` remain B9 | High |

**Multi-tenancy is the best-traced requirement group** — every URS maps to a real-PostgreSQL integration test, the compliance-critical suite CI can't skip. Fully Implemented across the board bar the two accepted B9 exceptions.

## 4. Security requirements

| Req | Backend | AuthZ/DB | Test | Status | Gap | Conf |
|---|---|---|---|---|---|---|
| URS-001/002/003 Auth + strong password + lockout | ✓ | ✓ | ✓ `PasswordRulesTests`, functional | **Fully** | — | High |
| URS-004 Per-tenant MFA (TOTP) | ✓ | ✓ | ✓ `TotpServiceTests` | **Fully** | **off by default in reference prod (SEC-02)** | High |
| URS-005/095/096 Permission-catalogue RBAC (170 keys), effective next request | ✓ | ✓ | ✓ `RolePrivilegeFlowTests` | **Fully** | — | High |
| URS-006 Re-validate active/role each request | ✓ IUserPrivileges | ✓ | ✓ | **Fully** | — | High |
| URS-007 Idle-session timeout | ✓ client 30-min | — | — (config/inspection per repo RTM) | **Fully** | UI-side | Med |
| URS-056 Refuse over-privileged DB role | ✓ DatabaseRoleGuard | ✓ | ✓ `RuntimeRolePrivilegeTests` | **Fully** | **Production-only (NB-04-03)** | High |
| URS-071 Rate-limit credential/e-sign → 429 | ✓ | ✓ per-actor e-sign | ✓ `SecurityHardeningTests` | **Fully** | — | High |
| URS-072 Defensive headers + locked CSP + HSTS | ✓ | ✓ | ✓ `SecurityHardeningTests` | **Fully** | — | High |
| URS-074 Memory token + rotating reuse-detecting refresh cookie | ✓ | ✓ SameSite=Strict | ✓ `RefreshSessionTests` | **Fully** | — | High |
| URS-078/083 Deny-by-default; no role×endpoint leakage | ✓ | ✓ | ✓ `AuditorDenyMatrix`,`RoleEndpointMatrix` | **Fully** | coarse on some writes (SEC-04) | High |
| URS-086/087/088 SCA + Trivy + SPA currency | CI | — | CI gates | **Fully** | — | High |
| URS-090 Workspace lookup no existence leak | ✓ | ✓ | ✓ `WorkspaceLookupTests` | **Fully** | — | High |
| URS-097 Branch/dept hard data boundary | ✓ | ✓ query filter | ✓ `RolePrivilegeFlowTests` | **Fully** | — | High |
| URS-098 No lockout of last privilege admin | ✓ lockout guard | ✓ | ✓ `RoleHandlersTests` | **Fully** | — | High |
| URS-119/120/121 self-service PIN/password + admin PIN | ✓ | ✓ | ~ (some auth endpoints **untested**, Doc03) | **Partially** | change-password/MFA-confirm untested | High |
| **NFR-SEC-17** `audit.security_event` RLS | ✓ | ✓ **closed by Hardening2** | ✓ `SecurityEventRlsTests` | **Fully** (repo RTM ❌ is stale) | see §8 divergence | High |
| **NFR-SEC-18** upload size bound | ✓ 50 MB | — | ✓ FileHardening | **Fully** (repo RTM ❌ appears stale) | see §8 | High |
| **NFR-SEC-19** independent pentest | — | — | — | **Missing (SEC-001)** | not performed | High |
| Single HS256 key, no rotation | ✓ | — | — | **Partially** (SEC-05) | key-mgmt best practice | High |

## 5. Compliance / 21 CFR Part 11 requirements

| Req | Backend | DB | Test | Status | Gap | Conf |
|---|---|---|---|---|---|---|
| URS-011/012/013 Audit trail + SHA-256 hash chain + verify | ✓ | ✓ append-only trigger | ✓ `GovernanceTests` (tamper), `AuditTrailChainTests` | **Fully** | trigger allows TRUNCATE (NB-04-02) | High |
| URS-014 Ledgers append-only (no runtime UPDATE/DELETE) | ✓ | ✓ grant-level | ✓ integration | **Fully** | — | High |
| URS-015/084 Reason-for-change + accessible capture | ✓ ChangeReason middleware+dialog | ✓ field_change | ✓ `FieldChangeInterceptorTests` | **Fully** | — | High |
| URS-016/019 Security-event recording + credential redaction | ✓ | ✓ | ✓ | **Fully** | admin reset no event (SEC-06) | High |
| URS-018 Formal audit-trail review | ✓ | ✓ audit_trail_review | ✓ ATR domain | **Fully** | — | High |
| **URS-020/021/022 E-signature: 2-component, binds signer/meaning/content-hash, append-only** | ✓ (ESignatureService) | ✓ electronic_signature | ~ **unit only** | **Partially** | **manifested on document publish ONLY — ~19 of ~20 signed-record gates write signer fields but NO signature_record (NB-03-02 / SEC-01)** | High |
| URS-023 Throttle/log/lock failed signings | ✓ e-sign rate limit + lockout | ✓ | ✓ | **Fully** | — | High |
| URS-024 Capture signature meaning | ✓ | ✓ meaning column | ✓ | **Fully** (where signature minted) | scope limited by URS-020 gap | High |
| URS-031/039 SoD (NC verify; study sign-off) | ✓ | — | ✓ domain | **Fully** | change-approve exempt; null-preparer no-op (SEC-13) | High |
| URS-041 Signed studies immutable at DB | ✓ | ✓ reject_frozen_mutation on 13 tables | ✓ `SignedRecordImmutabilityTests` | **Fully** | **only analytical studies DB-enforced; NC/audit/policy/change/review immutability is domain-guard-only** | High |
| URS-059/102 DB rejects out-of-domain values | — | ✓ 87 CHECK | ✓ `CheckConstraintTests` | **Fully** | — | High |
| URS-042…046 no-hard-delete / archive / legal hold / retention / immutable files | ✓ | ✓ | ✓ | **Fully** | download authz (SEC-03) | High |
| **DOC-001 Formal signed validation on qualified env** | — | — | OQ executed **unsigned**, dev workstation only | **Missing** | release blocker | High |

## 6. Performance / scalability / operational NFRs

| Req | Evidence | Status | Gap | Conf |
|---|---|---|---|---|
| NFR-PERF-01/02 reads p95<500ms, err<0.1% | repo measured 85-105ms / 0% **on dev box** | **Documentation-only** | not measured on qualified env (OPS-001) | Med |
| NFR-PERF-04 no unbounded lists | `take`/`days` clamped in handlers; some lists unpaged (Doc03) | **Partially** | some registers unpaged | High |
| NFR-REL-01…09 retry/outbox/backoff/dead-letter/xmin/idempotency/cold-start | ✓ (Doc10); `OutboxResilienceTests`, `OptimisticConcurrencyTests` (real PG) | **Fully** | — | High |
| NFR-AVL-01…04 liveness/readiness/probes rate-exempt | ✓ (Doc10); `HealthEndpointTests`, `ReadinessAndTopology` | **Fully** | AVL-05 no SLA defined | High |
| NFR-RCV-01…04 RPO/RTO, PITR, verified restore incl. hash-chain | scripts + 5 mandatory post-restore checks (Doc10) | **Fully (defined)** | **RCV-05 no drill executed** | High |
| NFR-MNT-01…08 arch gates / API snapshot / migration round-trip / codes | ✓ (Doc09); architecture merge gates | **Fully** | only last migration round-trips (NB-09-01) | High |
| NFR-SCL-01…05 single-replica, partition-ready, tenant-count untested | ✓ (Doc10); ADR-0001 + advisory lock | **Fully (as designed)** | tenant-count scale untested | High |
| NFR-OBS-01…06 JSON logs/correlation/trace/metrics/instruments | ✓ (Doc10) | **Fully** | **OBS-07 7 alerts specified, never deployed (OPS-001)**; trace/log backend unwired (NB-10-02) | High |
| NFR-RES-04 non-root container | ✓ CI assertion | **Fully** | RES-01 no budgets | High |
| NFR-A11Y-01…04 | axe on **login only** (Doc05) | **Partially** | A11Y-05 no WCAG level claimed; authed screens unscanned (NB-05-03) | High |

## 7. Reporting / integration requirements

| Req | Evidence | Status | Gap | Conf |
|---|---|---|---|---|
| URS-052/108…114 Quality Analytics (9 sub-systems, composite health score, per-section scoping) | ✓ full (Doc03/06); domain tests, per-section privilege | **Fully (impl)** / **Partially (test)** | endpoint untested e2e; **executed dev, unsigned** | High |
| URS-091/092 real denominators, no proportion>population | ✓ `DashboardKpiTotalsTests` | **Fully** | — | High |
| URS-109/112/113 no-population reported as such; withheld sections server-side | ✓ explicit honesty rules (Doc03) | **Fully** | untested | High |
| FR-EXP-01 four logged inspection exports | ✓ (Doc03) | **Fully** | — | High |
| FR-AQ-05 CSV import | ✓ **2 of 12 studies** | **Partially** | 10 study families lack import | High |
| **FR-AQ-06 LIS/instrument ingestion** | — | **Missing** | explicitly out of scope | High |
| Integration surface: PostgreSQL, SMTP, OTLP, local FS | ✓ (Doc10) | **Fully** | SMTP best-effort (NB-10-01); S3/SMS/payment/webhooks absent | High |

## 8. Divergences — as-built verdict vs the repo's RTM status

Where my independent, multi-layer traceability differs from the repo's own RTM (both directions):

| Req | Repo RTM says | As-built verdict | Why |
|---|---|---|---|
| **URS-020/021/022** e-signature | Built ✓ | **Partially** | E-signature *manifestation* (`signature_record`) exists only on document publish; ~19 of ~20 signed-record gates record signer fields but mint no signature (NB-03-02). The **most material traceability divergence.** |
| **URS-038/039/040** AQ sign-off + PT | Built ✓ | **Partially** | Domain math tested, but the entire AQ HTTP surface is functionally untested and PT-result is ungated (Doc09 T-1) — the "tests appropriate to the requirement" layer is absent. |
| **URS-046** immutable files / download | Built ✓ | **Partially** | File download is permission-ungated and unlogged (SEC-03/NB-08-01). |
| **NFR-SEC-17** security_event RLS | ❌ NOT MET | **Fully** | Closed by `Hardening2_RlsGapClosure` + `SecurityEventRlsTests` — repo RTM status appears **stale** (predates the hardening train). |
| **NFR-SEC-18** upload size bound | ❌ | **Fully** | 50 MB `[RequestSizeLimit]` + `FileHardeningTests` — repo status appears stale. |
| **NFR-PERF-01/02** | PASS (measured) | **Documentation-only** | Measured on a dev workstation only; not a qualified-environment result (OPS-001). |
| **URS-041** DB immutability | Built ✓ (broad) | **Fully but narrower** | DB-level immutability is on 13 analytical tables only; other "immutable" signed records rely on domain guards, not a DB trigger. |

**Net:** the repo RTM is largely accurate on *implementation presence*, but (a) **overstates two Part-11-adjacent items** by grading on signer-field presence rather than signature manifestation (URS-020-022) and on domain tests rather than HTTP tests (URS-038-040), and (b) **understates two hardened items** (NFR-SEC-17/18) whose ❌ status predates the schema-hardening train. Both directions are worth reconciling in the repo's next RTM revision.

## 9. Aggregate traceability posture

- **Functional QMS coverage:** every in-scope module requirement has working code (Doc 06: 26/34 modules Fully). The gaps are **layer-specific** (tests, fine-grained authz, e-signature manifestation), not missing features.
- **Multi-tenancy & core security:** the best-traced groups — near-universal Fully Implemented with real-PostgreSQL tests CI cannot skip.
- **21 CFR Part 11:** audit trail / reason-for-change / SoD / immutability / retention are Fully traced; **e-signature manifestation (URS-020-022) is the one Part 11 requirement group only Partially met.**
- **Requirements with no source evidence for a required layer (flag list):** URS-020-022 (signature-record layer, most gates), URS-038-040 (test layer), URS-046 (download authz/audit layer), NFR-PERF-01/02 (qualified-env measurement), NFR-OBS-07 (alert deployment), DOC-001 (signed validation), SEC-001 (pentest).
- **UI-only / backend-only mismatches:** `PUT /api/qc/profiles/{id}/targets` (URS-037) is backend-only (no UI); the Manual is UI-only by design.
- **Validation posture (do not treat as "validated"):** GAMP 5 doc set complete; role-privilege & schema-hardening OQ **executed but unsigned**; URS-108-122 **executed on dev, unsigned**; formal signed IQ/OQ/PQ (DOC-001), independent pentest (SEC-001), and staging observability/load (OPS-001) remain **open** — the three release blockers.

---

## Appendix A — Observation carry-forward

| ID | Note |
|---|---|
| NB-03-02 → RTM | Quantified as a Part 11 traceability gap (URS-020-022 Partially met). Doc 12. |
| **NB-11-01** (new) | Repo RTM overstates URS-020-022 (e-sig) and URS-038-040 (AQ tests); understates NFR-SEC-17/18 (stale ❌). Reconcile in next RTM revision. Doc 12/15. |
| Repo RTM claims | 62/94 FR ✅, 55/55 baseline URS traced — largely accurate on implementation presence; test-layer and signature-layer nuances added here. |
| DOC-001/SEC-001/OPS-001 | The three requirement groups with no evidence for their final layer (signed validation / pentest / qualified-env ops). Doc 12. |

## Appendix B — Reviewer no-modification attestation (manifest §8 model)

- [x] No file was created, modified, or deleted; nothing was built, run, or connected to a database.
- [x] Only read-only access was used (requirement-catalog extraction + synthesis of Docs 01-10).
- [x] The only filesystem write is this document: `docs/as-built-review/11_REQUIREMENTS_TRACEABILITY.md`.
- [x] No secret values reproduced.
- [x] Nothing invented — every status traces to an as-built finding in Documents 01-10 or a cited requirement source; SRS/URS claims are treated as requirements references, never as proof of implementation (Prompt-11 rule).

---

*End of Document 11. Reviewed at manifest baseline `d74d4bf` (no drift). Next: Prompt 12 → `12_TECHNICAL_DEBT_AND_RISK_REGISTER.md`.*
