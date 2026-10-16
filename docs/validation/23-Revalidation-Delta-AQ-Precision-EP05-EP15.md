# CSV Re-Validation Delta — Analytical Quality: Precision EP05/EP15 Gap Closure

| Field | Value |
| ----- | ----- |
| Document ID | REVAL-NTQMS-003 (rev 1 — 2026-10-16/17; rev 1 adds URS-157/158 reproducibility) |
| System | NT.QAMS — Analytical Quality module (Precision, Method Validation, Measurement Uncertainty, Sigma Metrics) |
| Baseline validated version | 1.0 (docs 00–05) + REVAL-NTQMS-001 (doc 06) + REVAL-NTQMS-002 (doc 20) |
| Scope of this delta | Closure of seven gaps found when the imprecision study was reviewed against CLSI EP05-A3 / EP15-A3 and ISO 15189:2022 §7.3: (1) no between-day level; (2) no measurement conditions per replicate; (3) claims judged by a plain "CV ≤ claim" instead of the EP15 upper verification limit; (4) no confidence intervals; (5) the signed-off within-lab CV did not feed measurement uncertainty or sigma; (6) one concentration level per study with no linkage; (7) Method Validation pooled every level into one CV (defect **V-D1**). **Rev 1** adds reproducibility: (8) a manufacturer reproducibility claim and (9) the EP05-A3 multi-site design (between-site component and reproducibility SD). Rev 0 shipped as `5205de1` (migration `20261016090659_PrecisionEp05Ep15Gaps`, deployed to production 2026-10-16); rev 1 adds migration `20261016211656_PrecisionReproducibility`. |
| Parent | VMP-NTQMS-001; URS-NTQMS-001; RTM-NTQMS-001; QP-NTQMS-001; VSR-NTQMS-001; REVAL-NTQMS-001; REVAL-NTQMS-002 |
| Status | **DRAFT for QA execution.** Engineering-prepared traceability + qualification templates; **QA owns, executes, witnesses, and signs.** Nothing here closes DOC-001. |

> **How to use this document.** Same convention as REVAL-NTQMS-001/002: a *delta* package
> adding requirements `URS-150`…`URS-156`, one IQ check, operational cases, and a VSR
> addendum. The engineering run is recorded in `verification-log.md` (2026-10-16) and the
> observed output in OQ record doc 24. Every execution cell is a **template** until QA executes it.

**Signature block (per executed protocol section):**

| Activity | Name | Signature | Date |
| -------- | ---- | --------- | ---- |
| Prepared by (Engineering) | | | |
| Executed by | | | |
| Reviewed by (QA) | | | |
| Approved by (System Owner) | | | |

---

## Part A — Requirements Traceability Matrix (RTM) delta

Terminology used below (VIM / ISO 5725 / CLSI EP05-A3): **repeatability** = within-run
precision; **within-laboratory (intermediate) precision** = one laboratory across days, runs,
operators, lots; **reproducibility** = between laboratories. The laboratory computes the first
two from its own data; reproducibility comes from manufacturer multi-site studies or PT/EQA.

| URS | Requirement (delta) | Design element(s) | Verification | Status |
| --- | ------------------- | ----------------- | ------------ | ------ |
| URS-150 | An imprecision study shall accept replicates grouped into runs and, optionally, runs nested within **days**. At Calculate the system shall resolve the design from the labels — runs only (one-way ANOVA), days with one run each (EP15 5 × 5, one-way over days) or days × runs (EP05 20 × 2 × 2, two-level nested ANOVA) — and report repeatability, between-run and **between-day** components and the within-laboratory SD/CV, flooring negative components at 0. A days × runs design shall be balanced (PR-014); a design that is only partly day-labelled shall be refused (PR-015). | `PrecisionStudy.Calculate` / `OneWay` / `DayLabelled` (`Domain/AnalyticalQuality/PrecisionStudy.cs`); `PrecisionDesign` enum + `ck_precision_study_design_domain`; `precision_measurement.day_label`; runs keyed by (day, run) | AUTO `PrecisionStudyTests` (nested 2 × 2 × 2 hand-computed reference: S²r 2, S²run 7, S²day 46, S²WL 55; days-only design; nesting of equal run labels; PR-014/PR-015); `PrecisionWorkflowTests`; OQ-AQP-01 | Template |
| URS-151 | Each replicate may record its **measurement conditions** — operator, instrument, reagent lot, calibrator lot, date measured — individually or by CSV import, and the study shall show which intermediate-precision factors the data spans (distinct days, runs, operators, instruments, reagent and calibrator lots, date range) together with whether the design meets the EP15 minimum (≥ 5 days or runs, ≥ 25 results). | `PrecisionConditions` value object; `PrecisionMeasurement` columns; `PrecisionStudy.ConditionsCoverage()`; `PrecisionLimits` (validator + import bounds); detail "Conditions Covered" card | AUTO `PrecisionStudyTests.Conditions_coverage_*`, `PrecisionWorkflowTests` (conditions round-trip, oversized import row rejected per row); OQ-AQP-02 | Template |
| URS-152 | Each repeatability and within-laboratory estimate shall be reported with its **degrees of freedom** (Satterthwaite effective df for the within-laboratory SD) and a **95 % χ² confidence interval**. A manufacturer claim shall be verified per CLSI EP15-A3: the claim is accepted when the observed CV does not exceed the **upper verification limit** UVL = claim · √(χ²(1 − α/L, ν) / ν), α = 5 %, L = levels in the study series; the UVL is reported, and the UI distinguishes "claim met", "above the claim but within the UVL" and "not verified". | `ChiSquareDistribution` (Lanczos log-gamma + regularised incomplete gamma, monotone bisection inverse — no third-party package); `PrecisionStudy.Satterthwaite` / `CvInterval` / `VerificationLimit`; new df/CI/UVL columns; `precision-detail.component.ts` verdict wording | AUTO `ChiSquareDistributionTests` (11 published table values, CDF/quantile inversion at non-integer df, argument guards); `PrecisionStudyTests` (UVL 8.654 % at df 2; CI 4.707–22.577 % at df 4; Satterthwaite 1.26 / 1.21); OQ-AQP-03 | Template |
| URS-153 | A study may be created for **several concentration levels at once** (a series, 2–10 levels, each with its own claims): one linked study per level is created in one transaction under a shared series reference, each remains its own signed record, every member verifies at α / L, and each study shows the other levels of its series. | `CreatePrecisionSeriesCommand` (`RequirePermissionPolicy(analytical-quality, Create)`) + validator; `PrecisionStudy.Configure(…, seriesRef, seriesLevelCount)` (PR-016); `ck_precision_study_series_shape`; `ix_precision_study_tenant_id_series_ref`; `POST /api/precision-studies/series` | AUTO `PrecisionWorkflowTests` (series creation, validator: distinct levels / bounds / positive claims), `PrecisionStudyTests` (α/L UVL 9.603 %, PR-016 theory); `PrecisionStudyConstraintTests` (real PG CHECK); OQ-AQP-04 | Template |
| URS-154 | The within-laboratory CV of a **signed-off** precision study shall be importable (a) into a measurement-uncertainty budget as the Type A u(Rw) component (ISO/TS 20914) and (b) into a sigma assessment as the CV input (TEa and bias kept). Both shall refuse an unsigned study (MU-008 / SIG-006) or a study of a different analyte (MU-009 / SIG-007), MU shall refuse a duplicate import (MU-012), and each consumer shall keep a tenant-composite reference to the source study; a hand-typed sigma CV breaks the link. | `UncertaintyBudget.AddPrecisionComponent`, `SigmaAssessment.UsePrecisionStudy`; `AddPrecisionComponentCommand`, `UseSigmaPrecisionStudyCommand` (`RequirePermissionPolicy(analytical-quality, Edit)`); `POST /api/uncertainty-budgets/{id}/components/from-precision-study`, `POST /api/sigma-assessments/{id}/use-precision-study`; `fk_unc_component_precision_study_tenant`, `fk_sigma_assessment_precision_study_tenant` (RESTRICT); `GET /api/precision-studies?state=SignedOff&analyte=` | AUTO `PrecisionDownstreamTests` (6), `PrecisionWorkflowTests` (handlers, PR-404); `PrecisionStudyConstraintTests` (real PG: cross-tenant and dangling references → 23503); OQ-AQP-05 | Template |
| URS-155 | The precision workspace shall show recorded PT/EQA results for the same analyte as **between-laboratory context** (assigned value, scheme SD, scheme SD as a CV, z-score), and state that reproducibility is not computed from one laboratory's data. | `GetPrecisionStudyByIdHandler` (query-side same-module read of `PtEnrollment`, latest 10); `PrecisionPtContextDto`; "Between-Laboratory Context" card | AUTO `PrecisionWorkflowTests.The_workspace_shows_series_siblings_and_pt_scheme_context_*` (analyte match case-insensitive, pending enrolments excluded, scheme CV 4.0 %); OQ-AQP-06 | Template |
| URS-156 | Method Validation shall compute precision and bias **per level**: each level's total error \|bias\| + 1.65 · CV is judged against TEa and the study passes only when every level does; the stored CV is the df-weighted pooled **within-level** CV; every level needs ≥ 2 replicates (MV-016). The per-level table is shown on the study. (Closes defect **V-D1**: the previous single SD over all levels counted the spread between levels as imprecision.) | `ValidationStudy.CalculateStatistics` / `LevelResults` / `ComputeLevels`; `ValidationLevelResultDto`; `study-detail.component.ts` per-level table | AUTO `ValidationStudyTests` (+3: Low/High per-level CVs 12.856 % / 1.400 %, pooled 9.144 %; any failing level fails the study; MV-016 and the non-throwing read); OQ-AQP-07 | Template |

| URS-157 | A precision study (and each level of a series) may record the manufacturer's **reproducibility claim** (between-laboratory CV%). For a **single-site** study the system shall check that the within-laboratory CV is consistent with that claim — not above UVL = claim · √(χ²(1 − α/L, ν_WL) / ν_WL) — and show the claim beside the within-lab CV, the study's reproducibility (when multi-site) and the PT/EQA scheme CVs. | `PrecisionStudy.ClaimedReproducibilityCvPct`, `WithinLabConsistentWithReproducibilityClaim`, `ReproducibilityVerificationLimitCvPct`; `CreatePrecisionStudyRequest` / `PrecisionSeriesLevelRequest` `ClaimedReproducibilityCvPct`; new-study form fourth claim field; within-lab card consistency line; PT context card | AUTO `PrecisionStudyTests.A_single_site_study_checks_its_within_lab_cv_against_the_reproducibility_claim` (UVL ≈ 9.6 % below the observed 23.08 % → inconsistent); live: single-site Glucose study, within-lab 0.89 % vs claim 2.6 % (UVL 3.30 %) consistent; OQ-AQP-08 | Template |
| URS-158 | Replicates may carry a **site** label. When every replicate is site-labelled the study is an EP05-A3 **multi-site** design (sites × runs, sites × days, sites × days × runs — balanced, ≥ 2 sites, ≥ 2 days or runs per site): a nested ANOVA with sites on top reports the **between-site** component and the **reproducibility** SD S_R = √(S_WL² + S_site²) with Satterthwaite df, 95 % CI and, when claimed, EP15 verification of the reproducibility claim; the within-laboratory figure is pooled over sites. Partly site-labelled data (PR-017) and a single site (PR-018) are refused; the coverage panel shows the site count and whether the EP05 multi-site minimum (≥ 3 sites, each ≥ 5 days and ≥ 25 results) is met. | `PrecisionStudy.MultiSite` + generic balanced `Nested` ANOVA (also now used for single-site days × runs); `PrecisionDesign.SitesAndRuns/SitesAndDays/SitesDaysAndRuns` + widened `ck_precision_study_design_domain`; `precision_measurement.site_label`; new `BetweenSite*`/`Reproducibility*` columns; site field, site columns, between-site and reproducibility cards, CSV column 9 "Site" | AUTO `PrecisionStudyTests` (+6: 2 sites × 2 days reference σ²e 2 / σ²day 7 / σ²site 46, S_R 7.4162, ν_R 1.21, ν_WL 2.51; claim verified/not; PR-017/018/014; site-nested day labels; 3 × 5 × 5 minimum), `PrecisionWorkflowTests` (multi-site import → detail), `PrecisionStudyConstraintTests` (real PG accepts `SitesDaysAndRuns`); live 3 × 5 × 5 study matched an independent recomputation exactly (doc 24 §5); OQ-AQP-09 | Template |

**Behaviour change disclosed (URS-152).** Before this change a claim failed whenever the observed
CV exceeded it. Under EP15-A3 an observed CV above the claim but within the UVL **verifies** the
claim — small studies have wide UVLs. Studies calculated before the upgrade keep their stored
booleans (signed-off records are immutable); recalculating an unsigned study applies the EP15 rule.

**Behaviour change disclosed (URS-156).** Validation studies signed off before the upgrade keep
their stored (pooled-over-all-levels) CV and verdict; the per-level table on those records is
recomputed from their immutable replicates for display and may disagree with the stored figure.
QA should decide whether affected signed-off studies need a superseding study.

**Authorization.** No new permission key. The two new consumer commands and the series command
use `analytical-quality.edit` / `analytical-quality.create`, already in the catalogue — **no
tenant upgrade gap**. The existing add-replicate / calculate commands keep their legacy
`RequireInternalActor` tier (unchanged; tracked separately as an open medium finding).

---

## Part B — Installation Qualification (IQ) delta

| IQ | Check | Expected |
| -- | ----- | -------- |
| IQ-35 | After applying `20261016090659_PrecisionEp05Ep15Gaps`, query `pg_constraint` for `ck_precision_study_design_domain`, `ck_precision_study_series_shape`, `fk_unc_component_precision_study_tenant`, `fk_sigma_assessment_precision_study_tenant`; re-run the RLS-parity and identifier-length queries (`ntqms-database` §6); confirm `frozen_immutability` is still enabled on `precision_study`, `sigma_assessment`, `uncertainty_budget`, `validation_study`. | 4 constraints present; RLS violations 0; identifiers > 62 chars 0; 4 triggers `tgenabled = 'O'`. Engineering observation in doc 24 §2. |

## Part C — Operational Qualification (OQ) delta

Evidence-engine suites: `ChiSquareDistributionTests`, `PrecisionStudyTests`,
`PrecisionDownstreamTests`, `ValidationStudyTests`, `PrecisionWorkflowTests`,
`PrecisionStudyConstraintTests` (real PostgreSQL).

| OQ | Case (witnessed) | Acceptance |
| -- | ---------------- | ---------- |
| OQ-AQP-01 | Enter the EP05 reference set (or the 2 × 2 × 2 set in `PrecisionStudyTests`) with day labels; Calculate. | Design "Days × runs (nested, EP05)"; components match the reference to 4 dp; a missing day label on one replicate → PR-015 shown. |
| OQ-AQP-02 | Import a 5 × 5 CSV with operator/instrument/lot/date columns. | 25 imported; coverage card shows the distinct counts and date range; "Meets the EP15 minimum" shown. |
| OQ-AQP-03 | With claims set, Calculate a 5 × 5 study. | df, 95 % CI and UVL shown per component; verdict wording matches the CV vs claim vs UVL relationship. |
| OQ-AQP-04 | Create a 2-level series from the New Precision Study form. | Two studies with one series reference; each links to the other; UVLs use α/2. |
| OQ-AQP-05 | As a second user, sign off a study; import its within-lab CV into an MU budget and a sigma assessment of the same analyte. Repeat with an unsigned study and a different analyte. | Component / CV equal the study's within-lab CV and link back; refusals MU-008/MU-009/SIG-006/SIG-007 shown as the error title. |
| OQ-AQP-06 | Record a PT result for the analyte; open the study. | PT row with scheme CV = SD / assigned × 100; reproducibility note shown. |
| OQ-AQP-07 | Enter a Low/High validation study; Calculate. | Per-level table; study passes only if every level passes; a level with one replicate → MV-016. |
| OQ-AQP-08 | Create a single-site study with a reproducibility claim; Calculate. | The within-lab card states whether the within-lab CV is consistent with the claim, with the UVL; the PT context card shows claim, within-lab CV and scheme CVs. |
| OQ-AQP-09 | Import a 3-site × 5-day × 5-replicate CSV (Site in column 9) with a reproducibility claim; Calculate. Then remove the site label from one replicate (re-enter it without a site) and Calculate. | Design "Sites × days (multi-site EP05)"; between-site and reproducibility cards with df, CI, UVL verdict; coverage "Meets the EP05 multi-site minimum"; the partly-labelled attempt → PR-017. |

## Part D — Performance Qualification (PQ) delta

None — no load dimension. A Calculate is O(replicates); the χ² quantile is a bounded bisection.

## Part E — Validation Summary Report (VSR) addendum

The imprecision study now implements the CLSI EP05-A3 day/run/replicate model and the EP15-A3
claim verification, reports df and confidence intervals, records the measurement conditions it
spans, links multi-level studies, and feeds its signed-off within-laboratory CV into
measurement uncertainty and sigma with database-enforced, tenant-scoped traceability. Method
Validation no longer pools levels (V-D1 closed). Engineering verification is green except one
pre-existing environment-dependent functional test (doc 24 §3). QA execution of OQ-AQP-01…07 and
IQ-35 on a qualified environment remains open; DOC-001 and SEC-001 are unaffected.

## Part F — Execution checklist for QA

- [ ] IQ-35 executed on the qualified environment.
- [ ] OQ-AQP-01…07 executed and witnessed; screenshots attached.
- [ ] Decision recorded on pre-upgrade signed-off validation studies (URS-156 disclosure).
- [ ] FRA row "Analytical precision statistics & downstream feed" (doc 02) reviewed.
- [ ] Doc 24 reviewed and signed.
