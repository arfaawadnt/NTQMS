# 20 — Module MV: Method Validation and Analytical Studies, Statistical Verification

**Module code:** `MV`
**System under test:** v1.51.2, repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master`. Inspection date 2026-08-01.
**Binding conventions:** `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md` — the 28-field case format (§4),
the canonical detailed-case block (§8), the evidence labels `[IV]`/`[RNV]`/`[ID]`/`[GD]` (§4), the ID
convention (§5), the honesty rules (§6) and the split convention (§7) govern this file and every
`20-module-method-validation-cases-<letter>.md` batch that consumes the ids reserved below.

**This file is FRONT MATTER ONLY.** It contains no `## 5. Detailed test cases` section by design.
Detailed cases are authored into the `-cases-<letter>` batch files. The table in §0.2 is a
*reservation*; a reserved range with no matching case file is a coverage hole, not a delivered case.

---

## 0.1 Scope of this module

**In scope (read line-by-line for this pass):**

- `src/NT.QAMS.Domain/AnalyticalQuality/` — the twelve study aggregates commissioned as MV, plus
  `ValidationStudy` (the thirteenth, `api/validation-studies`, which shares the immutability trigger
  and the SoD guard and is therefore inventoried here rather than orphaned).
- `src/NT.QAMS.Application/AnalyticalQuality/` — the twelve command/query slices, their validators,
  their command-authorization policies and the two bulk-import handlers.
- The twelve study controllers plus `ValidationStudiesController`.
- `src/NT.QAMS.Infrastructure/Persistence/Configurations/AnalyticalQualityConfigurations.cs`.
- Migrations `20260726084134_SignedRecordImmutability`, `20260725114820_MethodComparison`,
  `20260725120132_LinearityStudies`, `20260725122046_DetectionLimitStudies`,
  `20260725175812_ReferenceIntervalStudies`, `20260725182042_SigmaAssessments`,
  `20260725183152_PrecisionStudies`, `20260725201422_AnalyticalComplianceModules`,
  `20260725061912_UncertaintyBudgets`, `20260721225752_AnalyticalQuality`,
  `20260731191212_Hardening3_CheckDomains`, `20260731201114_Hardening4_ChildTenancy`,
  `20260731210953_Hardening5_CompositeKeys`.

**Explicitly NOT in this module (referenced only):** QC / Westgard (`QcProfile`, `QcRun`,
`WestgardEvaluator`) → module `QC`; PT plans and enrollments → module `PT`; the audit-ledger hash
chain and `field_change` ledger → module `LEDGER`; the generic RLS/tenancy contract → module
`TENANT`; the problem+json envelope and pagination → module `API`.

**Completeness statement.** Complete for: all 13 aggregates and their owned child entities, every
domain error code raised inside `src/NT.QAMS.Domain/AnalyticalQuality/` and
`src/NT.QAMS.Application/AnalyticalQuality/`, every domain event they raise, the full REST surface as
snapshotted in `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt`, the permission gating
at both the HTTP and the command layer, the EF mapping, the tenant-composite keys/FKs, the CHECK
domains and the signed-record immutability trigger. **Deferred:** Angular component internals beyond
route/permission wiring (module `FE`), live-`psql` measurement of the study tables (none of the
claims below depend on it — every claim is source-read and labelled), and load/perf characterisation
(module `NFR`).

---

## 0.2 ID reservation table

**Numbering rule used here:** `TC-MV-<KIND>-<NNN>` with a **separate three-digit sequence per KIND**,
starting at 001, exactly as the examples in conventions §5 show (`TC-AUTH-API-001`,
`TC-TENANT-RLS-014`, `TC-QC-WESTGARD-007`). Case-batch authors MUST stay inside their reserved block
and MUST NOT renumber.

| Batch file | Slice of scope | Reserved ID blocks |
|---|---|---|
| `20-module-method-validation-cases-a.md` | **Precision (EP05) + Method Comparison (EP09)** — ANOVA components, Deming, Passing–Bablok, Bland–Altman, Pearson, bulk import | `TC-MV-UNIT-001`…`030` · `TC-MV-STAT-001`…`030` · `TC-MV-API-001`…`030` · `TC-MV-BVA-001`…`015` · `TC-MV-DT-001`…`010` · `TC-MV-STATE-001`…`012` · `TC-MV-DF-001`…`006` · `TC-MV-EP-001`…`008` |
| `20-module-method-validation-cases-b.md` | **Linearity (EP06) + Detection Limit (EP17) + Reference Interval (EP28)** — OLS on level means, AMR window search, LoB/LoD/LoQ, binomial transference rule | `TC-MV-UNIT-031`…`062` · `TC-MV-STAT-031`…`060` · `TC-MV-API-031`…`060` · `TC-MV-BVA-016`…`032` · `TC-MV-DT-011`…`020` · `TC-MV-STATE-013`…`024` · `TC-MV-DF-007`…`012` · `TC-MV-EP-009`…`016` · `TC-MV-PATH-001`…`006` |
| `20-module-method-validation-cases-c.md` | **Sigma + Carryover (EP10) + Interference (EP07)** — σ metric and grade bands, QC-design string, carryover ratio, per-interferent bias | `TC-MV-UNIT-063`…`090` · `TC-MV-STAT-061`…`080` · `TC-MV-API-061`…`086` · `TC-MV-BVA-033`…`050` · `TC-MV-DT-021`…`030` · `TC-MV-STATE-025`…`036` · `TC-MV-MCDC-001`…`006` · `TC-MV-EP-017`…`022` |
| `20-module-method-validation-cases-d.md` | **Lot Comparison + Instrument Comparability + Outlier Screening** — mean-of-means vs mean-of-biases, reference-instrument pairing, Tukey + Iglewicz–Hoaglin | `TC-MV-UNIT-091`…`120` · `TC-MV-STAT-081`…`100` · `TC-MV-API-087`…`112` · `TC-MV-BVA-051`…`066` · `TC-MV-DT-031`…`040` · `TC-MV-STATE-037`…`048` · `TC-MV-MCDC-007`…`012` · `TC-MV-PATH-007`…`012` |
| `20-module-method-validation-cases-e.md` | **Uncertainty budget + the shared cross-study surface** — GUM RSS/expansion, approval, the shared state machine, `reject_frozen_mutation`, SoD, RLS, permission matrix, `ValidationStudy` | `TC-MV-UNIT-121`…`145` · `TC-MV-STAT-101`…`112` · `TC-MV-API-113`…`140` · `TC-MV-STATE-049`…`066` · `TC-MV-INT-001`…`024` · `TC-MV-RLS-001`…`016` · `TC-MV-SEC-001`…`028` · `TC-MV-BVA-067`…`078` · `TC-MV-DT-041`…`048` |
| *(reserved, unassigned)* | End-to-end, UI, accessibility, observability, performance, disaster-recovery | `TC-MV-E2E-001`…`012` · `TC-MV-COMP-001`…`020` · `TC-MV-A11Y-001`…`008` · `TC-MV-OBS-001`…`006` · `TC-MV-PERF-001`…`008` · `TC-MV-DR-001`…`004` · `TC-MV-MUT-001`…`006` |
| **This file** | UAT scenarios and exploratory charters (authored below, §6 and §7) | `TC-MV-UAT-001`…`008` · `TC-MV-EXPL-001`…`005` |

**Gap ID range consumed by this file:** `GAP-MV-001` … `GAP-MV-021`.
**Requirement IDs traced:** `URS-038`, `URS-039`, `URS-041` (baseline set,
`docs/validation/01-User-Requirements-Specification.md:86,87,89`); `URS-100`…`107` (schema hardening,
`docs/validation/06-Revalidation-Delta-v1.38-v1.50.md` Part A.10) for the composite-key / child-tenancy
claims. **Risk IDs:** `docs/validation/02-Functional-Risk-Assessment.md` names its rows but does not
number them (rows "Method-validation studies", "Signed-record / analytical immutability", "Separation
of duties (NC + analytical + policy)" at lines 54, 55, 64). Per conventions §5, this module therefore
**mints** `RSK-MV-001`…`RSK-MV-008` and says so:

| Risk ID | Statement | FRA row it refines |
|---|---|---|
| `RSK-MV-001` | A study statistic is computed by an algorithm other than the one documented, so a method is released on a wrong number | "Method-validation studies" (line 64) |
| `RSK-MV-002` | A signed study is altered or deleted after sign-off | "Signed-record / analytical immutability" (line 55) |
| `RSK-MV-003` | The preparer of a study signs it off | "Separation of duties" (line 54) |
| `RSK-MV-004` | Derived statistics survive a data change and are reported stale | "Method-validation studies" (line 64) |
| `RSK-MV-005` | A study or its measurements leak across tenants | "Signed-record / analytical immutability" (line 55) |
| `RSK-MV-006` | An unprivileged actor mutates study evidence | "Separation of duties" (line 54) |
| `RSK-MV-007` | A malformed input reaches the handler and returns 500 instead of a typed refusal | "Method-validation studies" (line 64) |
| `RSK-MV-008` | Evidence is deleted without a recorded reason for change | "Signed-record / analytical immutability" (line 55) |

---

## 0. Correction to ground truth

One factual error in `00-GROUND-TRUTH-AND-CONVENTIONS.md` was found. It matters because case authors
would otherwise assert the wrong subsystem for two error codes.

**§2 "Electronic signature", line 54** reads:

> `SIG-001` PIN not set or incorrect · `SIG-002` password incorrect · `SIG-003` account temporarily
> locked after repeated failed signings · `SIG-010`, `SIG-011`, `SIG-404` also in use.

**`SIG-010` and `SIG-011` are not electronic-signature codes and do not appear in
`ESignatureService` at all.** They belong to `SigmaAssessment`:

- `src/NT.QAMS.Domain/AnalyticalQuality/SigmaAssessment.cs:72` — `InvalidStateTransitionException("SIG-010", "A signed-off assessment is immutable.")`
- `src/NT.QAMS.Domain/AnalyticalQuality/SigmaAssessment.cs:101` — `InvalidStateTransitionException("SIG-011", "The assessment is already signed off.")`

The complete set of `SIG-*` strings in `src/` is: `ComplianceLedgerServices.cs:94` (`SIG-404` "Signer
not found"), `:101` (`SIG-003` locked), `:108` (`SIG-002` password incorrect), `:114` (`SIG-001` PIN);
and `SigmaAssessment.cs:53` (`SIG-001` analyte required), `:77` (`SIG-002` TEa must be positive),
`:82` (`SIG-003` CV must be positive), `:72` (`SIG-010`), `:101` (`SIG-011`); and
`SigmaAssessmentSlice.cs:73,112` (`SIG-404` "Sigma assessment not found").

So the real position is worse than the ground truth records: **`SIG-001`, `SIG-002`, `SIG-003` and
`SIG-404` are each double-booked across two unrelated subsystems with different meanings and
different HTTP statuses** (`SIG-003` is 422 from the sigma aggregate and 422 from the e-signature
service but for entirely different causes; `SIG-404` is 404 in both but names a different missing
entity). Raised as **GAP-MV-001**. Ground-truth §2 line 54 should be amended to remove `SIG-010`
and `SIG-011` and to flag the collision.

No other ground-truth statement touching this module was found to be wrong. In particular the
following were re-verified and are **correct as written**: the `reject_frozen_mutation` trigger
covers "the 12 analytical study roots (`state='SignedOff'`) + `uncertainty_budget`
(`status='Approved'`)" (`20260726084134_SignedRecordImmutability.cs:14-29` lists exactly 13 tuples);
`EnsureSignerIsNotPreparer` "guards all 14 analytical sign-offs/approvals with `SOD-AQ-001`" (14
call sites — 13 study aggregates plus `PtPlan.Approve`, `PtPlan.cs:108`); and the no-op-when-preparer-
unknown behaviour (`AggregateRoot.cs:38`).

---

## 1. Implementation inventory

Every claim below was read in the cited file at the cited line. Nothing is inferred from naming.

### 1.1 The thirteen aggregates

| Aggregate | Root type | Location | Owned child (table) | State property |
|---|---|---|---|---|
| Imprecision | `PrecisionStudy` | `src/NT.QAMS.Domain/AnalyticalQuality/PrecisionStudy.cs:35` | `PrecisionMeasurement` (`precision_measurement`) | `PrecisionState State` |
| Method comparison | `MethodComparisonStudy` | `.../MethodComparisonStudy.cs:35` | `MeasurementPair` (`measurement_pair`) | `MethodComparisonState State` |
| Linearity / AMR | `LinearityStudy` | `.../LinearityStudy.cs:39` | `LinearityMeasurement` (`linearity_measurement`) | `LinearityState State` |
| Detection capability | `DetectionLimitStudy` | `.../DetectionLimitStudy.cs:42` | `DetectionMeasurement` (`detection_measurement`) | `DetectionLimitState State` |
| Reference interval | `ReferenceIntervalStudy` | `.../ReferenceIntervalStudy.cs:35` | `ReferenceSample` (`reference_sample`) | `ReferenceIntervalState State` |
| Six-Sigma | `SigmaAssessment` | `.../SigmaAssessment.cs:19` | *(none — flat)* | `SigmaAssessmentState State` |
| Carryover | `CarryoverStudy` | `.../CarryoverStudy.cs:37` | `CarryoverReading` (`carryover_reading`) | `CarryoverState State` |
| Interference | `InterferenceStudy` | `.../InterferenceStudy.cs:41` | `InterferenceMeasurement` (`interference_measurement`) | `InterferenceState State` |
| Lot comparison | `LotComparisonStudy` | `.../LotComparisonStudy.cs:32` | `LotSamplePair` (`lot_sample_pair`) | `LotComparisonState State` |
| Instrument comparability | `InstrumentComparabilityStudy` | `.../InstrumentComparabilityStudy.cs:37` | `InstrumentReading` (`instrument_reading`) | `InstrumentComparabilityState State` |
| Outlier screening | `OutlierScreening` | `.../OutlierScreening.cs:35` | `OutlierDataPoint` (`outlier_point`) | `OutlierScreeningState State` |
| Uncertainty budget | `UncertaintyBudget` | `.../UncertaintyBudget.cs:45` | `UncertaintyComponent` (`uncertainty_component`) | `UncertaintyBudgetStatus Status` |
| CLSI validation study | `ValidationStudy` | `.../ValidationStudy.cs:30` | `ValidationReplicate` (`validation_replicate`) | `ValidationState State` |

All thirteen are `AggregateRoot, ITenantScoped` with `public Guid TenantId { get; set; }`. All
children are EF **owned collections** (`builder.OwnsMany`, `AnalyticalQualityConfigurations.cs:56,
110, 138, 173, 204, 233, 265, 314, 342, 369, 398, 425, 453`), so they have no independent repository
and no independent endpoint — they are reachable only through the root.

### 1.2 Enumerations (exact members — do not invent values)

| Enum | Members | Location |
|---|---|---|
| `PrecisionState` | `DataEntry, Calculated, SignedOff` | `PrecisionStudy.cs:6` |
| `MethodComparisonState` | `DataEntry, Calculated, SignedOff` | `MethodComparisonStudy.cs:6` |
| `LinearityState` | `DataEntry, Calculated, SignedOff` | `LinearityStudy.cs:6` |
| `DetectionLimitState` | `DataEntry, Calculated, SignedOff` | `DetectionLimitStudy.cs:6` |
| `DetectionSampleKind` | `Blank, LowLevel` | `DetectionLimitStudy.cs:8` |
| `ReferenceIntervalState` | `DataEntry, Calculated, SignedOff` | `ReferenceIntervalStudy.cs:6` |
| `ReferenceIntervalVerdict` | `Verified, Rejected` | `ReferenceIntervalStudy.cs:8` |
| `SigmaAssessmentState` | `Draft, SignedOff` | `SigmaAssessment.cs:6` |
| `SigmaGrade` | `Unacceptable, Marginal, Good, Excellent, WorldClass` | `SigmaAssessment.cs:9` |
| `CarryoverState` | `DataEntry, Calculated, SignedOff` | `CarryoverStudy.cs:6` |
| `CarryoverSampleKind` | `High, Low` | `CarryoverStudy.cs:8` |
| `InterferenceState` | `DataEntry, Calculated, SignedOff` | `InterferenceStudy.cs:6` |
| `LotComparisonState` | `DataEntry, Calculated, SignedOff` | `LotComparisonStudy.cs:6` |
| `InstrumentComparabilityState` | `DataEntry, Calculated, SignedOff` | `InstrumentComparabilityStudy.cs:6` |
| `OutlierScreeningState` | `DataEntry, Calculated, SignedOff` | `OutlierScreening.cs:6` |
| `UncertaintyBudgetStatus` | `Draft, Calculated, Approved` | `UncertaintyBudget.cs:6` |
| `UncertaintyComponentType` | `TypeA, TypeB` | `UncertaintyBudget.cs:9` |
| `ValidationState` | `ProtocolConfigured, DataEntered, StatsCalculated, SignedOff` | `ValidationStudy.cs:6` |

There is **no** `Void`, `Rejected`, `Superseded`, `Reopened`, `UnderReview` or `Archived` state on any
MV aggregate. Any case that assumes one is invalid.

### 1.3 Domain constants (the numbers the tests must assert)

| Constant | Value | Location |
|---|---|---|
| `PrecisionStudy.MinimumRuns` | `2` | `PrecisionStudy.cs:37` |
| `PrecisionStudy.MinimumReplicatesPerRun` | `2` | `PrecisionStudy.cs:38` |
| `MethodComparisonStudy.RecommendedMinimumPairs` | `40` | `MethodComparisonStudy.cs:38` |
| `LinearityStudy.MinimumLevels` | `4` | `LinearityStudy.cs:42` |
| `DetectionLimitStudy.Z` | `1.645m` | `DetectionLimitStudy.cs:45` |
| `DetectionLimitStudy.MinimumBlankReplicates` | `10` | `DetectionLimitStudy.cs:48` |
| `DetectionLimitStudy.MinimumLowLevelReplicates` | `10` | `DetectionLimitStudy.cs:49` |
| `ReferenceIntervalStudy.RecommendedSampleCount` | `20` | `ReferenceIntervalStudy.cs:38` |
| `ReferenceIntervalStudy.AllowedOutsideFraction` | `0.10m` | `ReferenceIntervalStudy.cs:41` |
| `CarryoverStudy.MinimumHigh` | `1` | `CarryoverStudy.cs:39` |
| `CarryoverStudy.MinimumLow` | `3` | `CarryoverStudy.cs:40` |
| `InterferenceStudy.MinimumControlReplicates` | `3` | `InterferenceStudy.cs:43` |
| `LotComparisonStudy.MinimumPairs` | `3` | `LotComparisonStudy.cs:34` |
| `OutlierScreening.MinimumPoints` | `4` | `OutlierScreening.cs:37` |
| `OutlierScreening.ModifiedZThreshold` (private) | `3.5m` | `OutlierScreening.cs:38` |
| Bland–Altman LoA multiplier | `1.96` (literal) | `MethodComparisonStudy.cs:181-182` |
| Deming error-variance ratio λ | `1.0` (literal `const`) | `MethodComparisonStudy.cs:159` |
| Tukey fence multiplier | `1.5` (literal) | `OutlierScreening.cs:129-130, 186` |
| Iglewicz–Hoaglin constant | `0.6745` (literal) | `OutlierScreening.cs:160, 187` |
| `ValidationStudy` total-error z | `1.65m` (literal) | `ValidationStudy.cs:137` |
| `InstrumentComparabilityStudy` minimum instruments | **none declared** — enforced only as "reference has readings" + "≥1 other instrument" | `InstrumentComparabilityStudy.cs:127, 138` |

### 1.4 Domain error codes — EXHAUSTIVE for this module

Every `DomainException` / `InvalidStateTransitionException` string raised in
`src/NT.QAMS.Domain/AnalyticalQuality/` (excluding `QcProfile`, `PtPlan`, `PtEnrollment`,
`WestgardEvaluator`, which belong to modules `QC` and `PT`) and in
`src/NT.QAMS.Application/AnalyticalQuality/` for the thirteen MV aggregates. HTTP status is derived
from `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:26-82` — `InvalidStateTransitionException`
→ **409**, code ending `-404` → **404**, code starting `AUTH-` → **401**, code starting `AUTHZ-` →
**403**, any other `DomainException` → **422**, FluentValidation → **400**.

#### Precision (`PR-*`)

| Code | Type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `PR-001` | `DomainException` | 422 | `PrecisionStudy.cs:84` | `analyte` null/blank/whitespace on `Configure` |
| `PR-002` | `DomainException` | 422 | `PrecisionStudy.cs:89` | `claimedRepeatabilityCvPct <= 0` **or** `claimedWithinLabCvPct <= 0` (either, when supplied) |
| `PR-003` | `DomainException` | 422 | `PrecisionStudy.cs:109` | `runLabel` null/blank/whitespace on `AddMeasurement` |
| `PR-010` | `DomainException` | 422 | `PrecisionStudy.cs:142` | distinct run count `< 2` on `Calculate` |
| `PR-011` | `DomainException` | 422 | `PrecisionStudy.cs:147` | any run has `< 2` replicates on `Calculate` |
| `PR-012` | `InvalidStateTransitionException` | **409** | `PrecisionStudy.cs:203` | `SignOff` when `State != Calculated` |
| `PR-013` | `InvalidStateTransitionException` | **409** | `PrecisionStudy.cs:234` | any mutating call when `State == SignedOff` |
| `PR-404` | `DomainException` | 404 | `PrecisionStudy.cs:122` (measurement) · `PrecisionSlice.cs:93,112,176` (study) | measurement id unknown · study id unknown |

#### Method comparison (`MC-*`)

| Code | Type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `MC-001` | `DomainException` | 422 | `MethodComparisonStudy.cs:81` | `analyte` blank |
| `MC-002` | `DomainException` | 422 | `MethodComparisonStudy.cs:86` | `referenceMethod` **or** `testMethod` blank |
| `MC-003` | `DomainException` | 422 | `MethodComparisonStudy.cs:105` | `referenceValue <= 0` **or** `testValue <= 0` on `AddPair` |
| `MC-010` | `DomainException` | 422 | `MethodComparisonStudy.cs:134` | fewer than 2 pairs on `Calculate` |
| `MC-011` | `DomainException` | 422 | `MethodComparisonStudy.cs:153` (`sxx==0 \|\| syy==0`) · `:229` (no non-vertical pairwise slope) | no spread in X or Y |
| `MC-012` | `InvalidStateTransitionException` | **409** | `MethodComparisonStudy.cs:193` | `SignOff` when `State != Calculated` |
| `MC-013` | `InvalidStateTransitionException` | **409** | `MethodComparisonStudy.cs:281` | mutating call when `SignedOff` |
| `MC-404` | `DomainException` | 404 | `MethodComparisonStudy.cs:119` · `MethodComparisonSlice.cs:93,113,177` | pair id unknown · study id unknown |

#### Linearity (`LIN-*`)

| Code | Type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `LIN-001` | `DomainException` | 422 | `LinearityStudy.cs:82` | `analyte` **or** `method` blank |
| `LIN-002` | `DomainException` | 422 | `LinearityStudy.cs:87` | `allowableDeviationPct <= 0` or `> 50` |
| `LIN-003` | `DomainException` | 422 | `LinearityStudy.cs:106` | `assignedValue <= 0` on `AddMeasurement` |
| `LIN-010` | `DomainException` | 422 | `LinearityStudy.cs:134` | fewer than 4 **distinct** assigned values on `Calculate` |
| `LIN-011` | `DomainException` | 422 | `LinearityStudy.cs:153` | `sxx == 0` (all levels share one assigned value) |
| `LIN-012` | `InvalidStateTransitionException` | **409** | `LinearityStudy.cs:190` | `SignOff` when `State != Calculated` |
| `LIN-013` | `InvalidStateTransitionException` | **409** | `LinearityStudy.cs:297` | mutating call when `SignedOff` |
| `LIN-404` | `DomainException` | 404 | `LinearityStudy.cs:119` · `LinearitySlice.cs:93,133` | measurement id unknown · study id unknown |

#### Detection limit (`DL-*`)

| Code | Type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `DL-001` | `DomainException` | 422 | `DetectionLimitStudy.cs:89` | `analyte` **or** `method` blank |
| `DL-002` | `DomainException` | 422 | `DetectionLimitStudy.cs:94` | `loqCvTargetPct <= 0` or `> 50` |
| `DL-003` | `DomainException` | 422 | `DetectionLimitStudy.cs:113` | `LowLevel` row without a positive `assignedValue` |
| `DL-004` | `DomainException` | 422 | `DetectionLimitStudy.cs:118` | `Blank` row **with** an `assignedValue` |
| `DL-010` | `DomainException` | 422 | `DetectionLimitStudy.cs:145` | fewer than 10 blank replicates |
| `DL-011` | `DomainException` | 422 | `DetectionLimitStudy.cs:150` | fewer than 10 low-level replicates **in total** (not per level) |
| `DL-012` | `DomainException` | 422 | `DetectionLimitStudy.cs:178` | every low-level group has a single replicate (`dfWithin == 0`) |
| `DL-013` | `InvalidStateTransitionException` | **409** | `DetectionLimitStudy.cs:213` | `SignOff` when `State != Calculated` |
| `DL-014` | `InvalidStateTransitionException` | **409** | `DetectionLimitStudy.cs:267` | mutating call when `SignedOff` |
| `DL-404` | `DomainException` | 404 | `DetectionLimitStudy.cs:131` · `DetectionLimitSlice.cs:94,134` | measurement id unknown · study id unknown |

#### Reference interval (`RI-*`)

| Code | Type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `RI-001` | `DomainException` | 422 | `ReferenceIntervalStudy.cs:84` | `analyte` **or** `population` blank |
| `RI-002` | `DomainException` | 422 | `ReferenceIntervalStudy.cs:89` | `source` blank |
| `RI-003` | `DomainException` | 422 | `ReferenceIntervalStudy.cs:94` | `claimedUpper <= claimedLower` |
| `RI-010` | `DomainException` | 422 | `ReferenceIntervalStudy.cs:133` | fewer than 20 samples on `Calculate` |
| `RI-011` | `InvalidStateTransitionException` | **409** | `ReferenceIntervalStudy.cs:156` | `SignOff` when `State != Calculated` |
| `RI-012` | `InvalidStateTransitionException` | **409** | `ReferenceIntervalStudy.cs:180` | mutating call when `SignedOff` |
| `RI-404` | `DomainException` | 404 | `ReferenceIntervalStudy.cs:123` · `ReferenceIntervalSlice.cs:94,134` | sample id unknown · study id unknown |

#### Sigma (`SIG-*` — see GAP-MV-001 for the collision with e-signature)

| Code | Type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `SIG-001` | `DomainException` | 422 | `SigmaAssessment.cs:53` | `analyte` blank |
| `SIG-002` | `DomainException` | 422 | `SigmaAssessment.cs:77` | `allowableTotalErrorPct <= 0` |
| `SIG-003` | `DomainException` | 422 | `SigmaAssessment.cs:82` | `cvPct <= 0` |
| `SIG-010` | `InvalidStateTransitionException` | **409** | `SigmaAssessment.cs:72` | `SetInputs` when `State != Draft` |
| `SIG-011` | `InvalidStateTransitionException` | **409** | `SigmaAssessment.cs:101` | `SignOff` when `State != Draft` |
| `SIG-404` | `DomainException` | 404 | `SigmaAssessmentSlice.cs:73,112` | assessment id unknown |

#### Carryover (`CAR-*`)

| Code | Type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `CAR-001` | `DomainException` | 422 | `CarryoverStudy.cs:76` | `analyte` blank |
| `CAR-002` | `DomainException` | 422 | `CarryoverStudy.cs:81` | `allowableCarryoverPct <= 0` or `> 50` |
| `CAR-010` | `DomainException` | 422 | `CarryoverStudy.cs:120` | zero `High` readings on `Calculate` |
| `CAR-011` | `DomainException` | 422 | `CarryoverStudy.cs:125` | fewer than 3 `Low` readings |
| `CAR-012` | `DomainException` | 422 | `CarryoverStudy.cs:134` | `meanHigh == steadyLow` (zero denominator) |
| `CAR-013` | `InvalidStateTransitionException` | **409** | `CarryoverStudy.cs:152` | `SignOff` when `State != Calculated` |
| `CAR-014` | `InvalidStateTransitionException` | **409** | `CarryoverStudy.cs:175` | mutating call when `SignedOff` |
| `CAR-404` | `DomainException` | 404 | `CarryoverStudy.cs:107` · `CarryoverSlice.cs:85,117` | reading id unknown · study id unknown |

#### Interference (`INT-*`)

| Code | Type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `INT-001` | `DomainException` | 422 | `InterferenceStudy.cs:77` | `analyte` blank |
| `INT-002` | `DomainException` | 422 | `InterferenceStudy.cs:82` | `allowableBiasPct <= 0` or `> 100` |
| `INT-003` | `DomainException` | 422 | `InterferenceStudy.cs:109` | `AddTest` with blank interferent name |
| `INT-010` | `DomainException` | 422 | `InterferenceStudy.cs:135` | fewer than 3 control replicates |
| `INT-011` | `DomainException` | 422 | `InterferenceStudy.cs:140` | zero test replicates |
| `INT-012` | `DomainException` | 422 | `InterferenceStudy.cs:146` | control mean is exactly `0` |
| `INT-013` | `InvalidStateTransitionException` | **409** | `InterferenceStudy.cs:172` | `SignOff` when `State != Calculated` |
| `INT-014` | `InvalidStateTransitionException` | **409** | `InterferenceStudy.cs:214` | mutating call when `SignedOff` |
| `INT-404` | `DomainException` | 404 | `InterferenceStudy.cs:122` · `InterferenceSlice.cs:88,120` | measurement id unknown · study id unknown |

#### Lot comparison (`LOT-*`)

| Code | Type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `LOT-001` | `DomainException` | 422 | `LotComparisonStudy.cs:74` | `analyte` blank |
| `LOT-002` | `DomainException` | 422 | `LotComparisonStudy.cs:79` | `currentLot` **or** `newLot` blank |
| `LOT-003` | `DomainException` | 422 | `LotComparisonStudy.cs:84` | `allowableBiasPct <= 0` or `> 50` |
| `LOT-004` | `DomainException` | 422 | `LotComparisonStudy.cs:104` | `currentLotValue <= 0` **or** `newLotValue <= 0` |
| `LOT-010` | `DomainException` | 422 | `LotComparisonStudy.cs:128` | fewer than 3 pairs |
| `LOT-011` | `DomainException` | 422 | `LotComparisonStudy.cs:135` | `meanCurrent == 0` — **unreachable** because `LOT-004` already bars non-positive values |
| `LOT-012` | `InvalidStateTransitionException` | **409** | `LotComparisonStudy.cs:151` | `SignOff` when `State != Calculated` |
| `LOT-013` | `InvalidStateTransitionException` | **409** | `LotComparisonStudy.cs:176` | mutating call when `SignedOff` |
| `LOT-404` | `DomainException` | 404 | `LotComparisonStudy.cs:118` · `LotComparisonSlice.cs:88,120` | pair id unknown · study id unknown |

#### Instrument comparability (`ICP-*`)

| Code | Type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `ICP-001` | `DomainException` | 422 | `InstrumentComparabilityStudy.cs:73` | `analyte` blank |
| `ICP-002` | `DomainException` | 422 | `InstrumentComparabilityStudy.cs:78` | `referenceInstrument` blank |
| `ICP-003` | `DomainException` | 422 | `InstrumentComparabilityStudy.cs:83` | `allowableBiasPct <= 0` or `> 50` |
| `ICP-004` | `DomainException` | 422 | `InstrumentComparabilityStudy.cs:102` | `instrument` **or** `sampleId` blank on `AddReading` |
| `ICP-010` | `DomainException` | 422 | `InstrumentComparabilityStudy.cs:129` | the reference instrument has no readings |
| `ICP-011` | `DomainException` | 422 | `InstrumentComparabilityStudy.cs:140` | no non-reference instrument present |
| `ICP-012` | `DomainException` | 422 | `InstrumentComparabilityStudy.cs:146` | some instrument shares no sample with the reference |
| `ICP-013` | `InvalidStateTransitionException` | **409** | `InstrumentComparabilityStudy.cs:198` | `SignOff` when `State != Calculated` |
| `ICP-014` | `InvalidStateTransitionException` | **409** | `InstrumentComparabilityStudy.cs:221` | mutating call when `SignedOff` |
| `ICP-404` | `DomainException` | 404 | `InstrumentComparabilityStudy.cs:115` · `InstrumentComparabilitySlice.cs:87,120` | reading id unknown · study id unknown |

#### Outlier screening (`OUT-*`)

| Code | Type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `OUT-001` | `DomainException` | 422 | `OutlierScreening.cs:75` | `dataset` blank |
| `OUT-010` | `DomainException` | 422 | `OutlierScreening.cs:111` | fewer than 4 points |
| `OUT-011` | `InvalidStateTransitionException` | **409** | `OutlierScreening.cs:173` | `SignOff` when `State != Calculated` |
| `OUT-012` | `InvalidStateTransitionException` | **409** | `OutlierScreening.cs:231` | mutating call when `SignedOff` |
| `OUT-404` | `DomainException` | 404 | `OutlierScreening.cs:101` · `OutlierScreeningSlice.cs:84,116` | point id unknown · screening id unknown |

There is **no** `OUT-002` and **no** unit validation on `OutlierScreening.Configure`.

#### Uncertainty budget (`MU-*`)

| Code | Type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `MU-001` | `DomainException` | 422 | `UncertaintyBudget.cs:87` | `analyte` **or** `method` blank |
| `MU-002` | `DomainException` | 422 | `UncertaintyBudget.cs:92` | `coverageFactor < 1` or `> 4` |
| `MU-003` | `DomainException` | 422 | `UncertaintyBudget.cs:97` | `targetExpandedUncertainty <= 0` when supplied |
| `MU-004` | `DomainException` | 422 | `UncertaintyBudget.cs:118` | component `name` blank |
| `MU-005` | `DomainException` | 422 | `UncertaintyBudget.cs:123` | `relativeStandardUncertainty < 0` |
| `MU-006` | `DomainException` | 422 | `UncertaintyBudget.cs:139` | component id unknown on `RemoveComponent` — **note the `-006` suffix, so this is 422, not 404** |
| `MU-007` | `DomainException` | 422 | `UncertaintyBudget.cs:153` | `Calculate` with zero components |
| `MU-010` | `InvalidStateTransitionException` | **409** | `UncertaintyBudget.cs:169` | `Approve` when `Status != Calculated` |
| `MU-011` | `InvalidStateTransitionException` | **409** | `UncertaintyBudget.cs:182` | mutating call when `Status == Approved` |
| `MU-404` | `DomainException` | 404 | `UncertaintySlice.cs:108,148` | budget id unknown |

#### CLSI validation study (`MV-*`)

| Code | Type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `MV-001` | `DomainException` | 422 | `ValidationStudy.cs:60` | `analyte` **or** `protocol` blank |
| `MV-002` | `DomainException` | 422 | `ValidationStudy.cs:65` | `totalAllowableError <= 0` |
| `MV-010` | `InvalidStateTransitionException` | **409** | `ValidationStudy.cs:82` | `EnterReplicate` when `SignedOff` |
| `MV-011` | `DomainException` | 422 | `ValidationStudy.cs:95` | `level` blank |
| `MV-012` | `InvalidStateTransitionException` | **409** | `ValidationStudy.cs:110` | `CalculateStatistics` when state is not `DataEntered`/`StatsCalculated` |
| `MV-013` | `DomainException` | 422 | `ValidationStudy.cs:115` | fewer than 2 replicates |
| `MV-014` | `DomainException` | 422 | `ValidationStudy.cs:122` | mean of measured values is exactly `0` |
| `MV-015` | `InvalidStateTransitionException` | **409** | `ValidationStudy.cs:148` | `SignOff` when `State != StatsCalculated` |
| `MV-404` | `DomainException` | 404 | `ValidationAndPtSlice.cs:55,116` | study id unknown |

#### Cross-cutting codes reachable from every MV endpoint

| Code | Type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `SOD-AQ-001` | `DomainException` | **422** | `AggregateRoot.cs:40`, via 13 MV call sites | signer id equals `CreatedByUserId`. **No-op when `CreatedByUserId` is null** (`AggregateRoot.cs:38`) |
| `TENANT-000` | `DomainException` | 422 | every `Create…Handler` (e.g. `PrecisionSlice.cs:35`) | `ICurrentTenant.TenantId` is null |
| `AUTH-003` | `DomainException` | **401** | every `SignOff…`/`Approve…` handler (e.g. `PrecisionSlice.cs:85`) | `ICurrentUser.UserId` is null |
| `AUTHZ-000` | `DomainException` | 403 | `AuthorizationBehavior.cs:52` | a command carries no `CommandPolicyAttribute` (CI-gated, should be unreachable) |
| `AUTHZ-001` | `DomainException` | 403 | `AuthorizationBehavior.cs:60` | command reached with no authenticated actor |
| `AUTHZ-002` | `DomainException` | 403 | `AuthorizationBehavior.cs:83` | `ExternalAuditor` executing any MV command (`RequireInternalActor`) |
| `AUTHZ-403` | *filter-written problem* | 403 | `RequirePermissionAttribute.OnAuthorizationAsync` → `ProblemAuthorizationResultHandler.cs:16` | authenticated actor lacks the `{module}.{action}` key |
| `CHANGE-REASON-REQUIRED` | *middleware problem* | **400** | `RequestIdentity.cs:154` (`ChangeReasonMiddleware`, `:149`) | any DELETE without an `X-Change-Reason` header — applies to all 11 MV DELETE endpoints |
| `CONCURRENCY-409` | `DbUpdateConcurrencyException` map | 409 | `DomainExceptionHandler.cs:21,28` | `xmin` moved between read and write |

**Codes that do NOT exist in this module (do not write cases against them):** `MV-003`…`MV-009`,
`OUT-002`…`OUT-009`, `SIG-004`…`SIG-009`, `MU-008`, `MU-009`, `CAR-003`…`CAR-009`,
`ICP-005`…`ICP-009`, `LIN-004`…`LIN-009`, `PR-004`…`PR-009`, `MC-004`…`MC-009`,
`DL-005`…`DL-009`, `RI-004`…`RI-009`, `INT-004`…`INT-009`, `LOT-005`…`LOT-009`. There is no
`AQ-*` prefix, no `STUDY-*` prefix, no `STAT-*` prefix.

### 1.5 Domain events

| Event | Payload | Raised at | Consumer found in `src/` |
|---|---|---|---|
| `PrecisionStudySignedOff` | `(StudyId, StudyRef, Analyte, TenantId)` | `PrecisionStudy.cs:210` | **none** |
| `MethodComparisonSignedOff` | `(StudyId, StudyRef, Analyte, TenantId)` | `MethodComparisonStudy.cs:200` | **none** |
| `LinearityStudySignedOff` | `(StudyId, StudyRef, Analyte, IsLinear, TenantId)` | `LinearityStudy.cs:197` | **none** |
| `DetectionLimitSignedOff` | `(StudyId, StudyRef, Analyte, Lod, Loq, TenantId)` | `DetectionLimitStudy.cs:220` | **none** |
| `ReferenceIntervalSignedOff` | `(StudyId, StudyRef, Analyte, Verdict, TenantId)` | `ReferenceIntervalStudy.cs:163` | **none** |
| `SigmaAssessmentSignedOff` | `(AssessmentId, AssessmentRef, Analyte, SigmaValue, TenantId)` | `SigmaAssessment.cs:107` | **none** |
| `CarryoverStudySignedOff` | `(StudyId, StudyRef, Analyte, Passes, TenantId)` | `CarryoverStudy.cs:158` | **none** |
| `InterferenceStudySignedOff` | `(StudyId, StudyRef, Analyte, SignificantCount, TenantId)` | `InterferenceStudy.cs:179` | **none** |
| `LotComparisonSignedOff` | `(StudyId, StudyRef, Analyte, Passes, TenantId)` | `LotComparisonStudy.cs:158` | **none** |
| `InstrumentComparabilitySignedOff` | `(StudyId, StudyRef, Analyte, NonComparableCount, TenantId)` | `InstrumentComparabilityStudy.cs:205` | **none** |
| `OutlierScreeningSignedOff` | `(ScreeningId, ScreeningRef, OutlierCount, TenantId)` | `OutlierScreening.cs:180` | **none** |
| `UncertaintyBudgetApproved` | `(BudgetId, BudgetRef, Analyte, ExpandedUncertainty, CoverageFactor)` — **carries no `TenantId`**, unlike all twelve siblings | `UncertaintyBudget.cs:175` | **none** |
| `ValidationStudySignedOff` | `(StudyId, StudyRef, Analyte, Passed, SignedOffBy, TenantId)` | `ValidationStudy.cs:154` | **none** |

`NotificationPolicies` (`src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:24-33`) handles
exactly ten event types — `NcRaised, DocumentPublished, CalibrationDue, EquipmentLockedOut,
CompetencyExpired, HighResidualRisk, SupplierSuspended, EscalationTriggered,
ReferenceStandardExpired, HighImpartialityRiskDeclared`. **No MV event is among them.** Every MV
sign-off event therefore reaches the outbox and stops there. Consequence for case authors: the
**Expected Notification** field of every MV sign-off case must read
`n/a — no notification policy handles this event (NotificationPolicies.cs:24-33)`, never a
speculative notification. → **GAP-MV-002**.

### 1.6 REST surface

Confirmed against the approved snapshot `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt`
(lines 2-13, 34-134, 264-435, 636). **84 logical endpoints across the twelve in-scope controllers**
(90 including `api/validation-studies`); each is dual-exposed as `/api/…` and `/api/v{version}/…`
per `Asp.Versioning.Mvc`, so the snapshot carries 168 (180) rows for this module.

Every controller is `[ApiController]` + `[Authorize]` at class level. The `[RequirePermission]`
gating is **per-action and sparse** — see §1.7.

| Controller | Route base | GET | POST | PUT | DELETE |
|---|---|---|---|---|---|
| `PrecisionStudiesController` (`PrecisionStudiesController.cs:13`) | `api/precision-studies` | `/`, `/{id}` | `/`, `/{id}/measurements`, `/{id}/measurements/import`, `/{id}/calculate`, `/{id}/sign-off` | — | `/{id}/measurements/{measurementId}` |
| `MethodComparisonsController` (`:13`) | `api/method-comparisons` | `/`, `/{id}` | `/`, `/{id}/pairs`, `/{id}/pairs/import`, `/{id}/calculate`, `/{id}/sign-off` | — | `/{id}/pairs/{pairId}` |
| `LinearityStudiesController` (`:13`) | `api/linearity-studies` | `/`, `/{id}` | `/`, `/{id}/measurements`, `/{id}/calculate`, `/{id}/sign-off` | — | `/{id}/measurements/{measurementId}` |
| `DetectionLimitStudiesController` (`:13`) | `api/detection-limit-studies` | `/`, `/{id}` | `/`, `/{id}/measurements`, `/{id}/calculate`, `/{id}/sign-off` | — | `/{id}/measurements/{measurementId}` |
| `ReferenceIntervalStudiesController` (`:13`) | `api/reference-interval-studies` | `/`, `/{id}` | `/`, `/{id}/samples`, `/{id}/calculate`, `/{id}/sign-off` | — | `/{id}/samples/{sampleId}` |
| `SigmaAssessmentsController` (`:13`) | `api/sigma-assessments` | `/`, `/{id}` | `/`, `/{id}/sign-off` | `/{id}` | — |
| `CarryoverStudiesController` (`:13`) | `api/carryover-studies` | `/`, `/{id}` | `/`, `/{id}/readings`, `/{id}/calculate`, `/{id}/sign-off` | — | `/{id}/readings/{readingId}` |
| `InterferenceStudiesController` (`:13`) | `api/interference-studies` | `/`, `/{id}` | `/`, `/{id}/measurements`, `/{id}/calculate`, `/{id}/sign-off` | — | `/{id}/measurements/{measurementId}` |
| `LotComparisonsController` (`:13`) | `api/lot-comparisons` | `/`, `/{id}` | `/`, `/{id}/pairs`, `/{id}/calculate`, `/{id}/sign-off` | — | `/{id}/pairs/{pairId}` |
| `InstrumentComparabilitiesController` (`:13`) | `api/instrument-comparabilities` | `/`, `/{id}` | `/`, `/{id}/readings`, `/{id}/calculate`, `/{id}/sign-off` | — | `/{id}/readings/{readingId}` |
| `OutlierScreeningsController` (`:13`) | `api/outlier-screenings` | `/`, `/{id}` | `/`, `/{id}/points`, `/{id}/calculate`, `/{id}/sign-off` | — | `/{id}/points/{pointId}` |
| `UncertaintyController` (`:13`) | `api/uncertainty-budgets` | `/`, `/{id}` | `/`, `/{id}/components`, `/{id}/calculate`, `/{id}/approve` | — | `/{id}/components/{componentId}` |
| `ValidationStudiesController` (`AnalyticalQualityControllers.cs:52`) | `api/validation-studies` | `/`, `/{id}` | `/`, `/{id}/replicates`, `/{id}/calculate`, `/{id}/sign-off` | — | — |

**Endpoints that DO NOT exist** — do not write cases against them: there is no `DELETE` for any study
root, no `PUT`/`PATCH` on any study except `PUT api/sigma-assessments/{id}`, no `/void`, no
`/reopen`, no `/supersede`, no `/export`, no `/report`, no `/{id}/signatures`, and **no bulk-import
endpoint on any study other than precision and method comparison**.

`POST /` returns `201 CreatedAtAction` with body `{ id }` for the ten `Create…` actions that use it
(e.g. `PrecisionStudiesController.cs:32`); the sub-resource POSTs return `200` with a small anonymous
body (`{ measurementId }`, `{ pairId }`, `{ readingId }`, `{ pointId }`, `{ sampleId }`,
`{ componentId }`); `calculate`, `sign-off`, `approve` and the DELETEs return `204 NoContent`;
`SigmaAssessmentsController` `POST /` returns `200 { id }` (not 201 — `SigmaAssessmentsController.cs`
`Create` does not use `CreatedAtAction`).

### 1.7 Authorization — HTTP layer and command layer

**Permission key in use:** `analytical-quality.<action>`, built by
`PermissionCatalog.Key(PermissionCatalog.AnalyticalQuality /* = "analytical-quality" */, action)`
(`src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:100,194`). The module declares **eight**
actions (`PermissionCatalog.cs:171-173`): `View, Create, Edit, Approve, Void, Sign, Export, Manage`.

Measured across `src/`, the actions actually referenced by a `[RequirePermission]` or
`[RequirePermissionPolicy]` attribute are: `Create` (13 sites), `Sign` (12), `Edit` (4), `Manage` (2),
`Approve` (1). **`analytical-quality.view`, `analytical-quality.export` and `analytical-quality.void`
gate nothing anywhere in the API.** → **GAP-MV-003**.

| Endpoint class | HTTP gate | Command gate | Effective audience |
|---|---|---|---|
| All `GET` list/detail (26 endpoints) | `[Authorize]` only | *(queries are not gated by `AuthorizationBehavior` — `AuthorizationBehavior.cs:44-47`)* | **any authenticated user in the tenant, including `ExternalAuditor`, regardless of `analytical-quality.view`** |
| `POST /` create (12) | `[RequirePermission(AnalyticalQuality, Create)]` | `[RequireInternalActor]` | holders of `analytical-quality.create` |
| `POST /{id}/{measurements\|pairs\|readings\|points\|samples\|replicates}` (11) | **none** — `[Authorize]` only | `[RequireInternalActor]` | **any authenticated non-auditor**, no analytical permission needed |
| `POST /{id}/…/import` (2) | **none** | `[RequireInternalActor]` | **any authenticated non-auditor** |
| `POST /{id}/calculate` (11) | **none** | `[RequireInternalActor]` | **any authenticated non-auditor** |
| `DELETE …` child (11) | **none** | `[RequireInternalActor]` | **any authenticated non-auditor** (+ `X-Change-Reason` header) |
| `POST /{id}/sign-off` (12) | `[RequirePermission(AnalyticalQuality, Sign)]` | `[RequireInternalActor]` | holders of `analytical-quality.sign` |
| `PUT api/sigma-assessments/{id}` | `[RequirePermission(AnalyticalQuality, Edit)]` | `[RequireInternalActor]` | holders of `analytical-quality.edit` |
| `POST api/uncertainty-budgets/{id}/components` and `DELETE …/components/{componentId}` and `POST …/calculate` | `[RequirePermission(AnalyticalQuality, Edit)]` (`UncertaintyController.cs:36,42,50`) | `[RequireInternalActor]` | holders of `analytical-quality.edit` |
| `POST api/uncertainty-budgets/{id}/approve` | `[RequirePermission(AnalyticalQuality, Approve)]` (`UncertaintyController.cs:58`) | `[RequireInternalActor]` | holders of `analytical-quality.approve` |

Note the asymmetry: **the uncertainty budget is the only MV aggregate whose data-entry and
calculate endpoints are permission-gated.** The other eleven leave measurement entry, bulk import,
calculation and evidence deletion open to any internal actor. → **GAP-MV-004**.

**Seeded system-role grants** (`src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs`):

| Seeded role | `analytical-quality` keys granted | Line |
|---|---|---|
| `PlatformAdmin` / `TenantAdmin` / `QualityManager` | all eight (`KeysWhere(… _ => true)`) | `SystemRoleCatalog.cs:116` |
| `DepartmentHead` | `View, Create, Edit, Export` — **no `Sign`** | `SystemRoleCatalog.cs:143` |
| `Analyst` | `View, Export` only — **no `Create`** | `SystemRoleCatalog.cs:173` |
| `ExternalAuditor` | `View, Export` (read surface); every command refused `AUTHZ-002` | `SystemRoleCatalog.cs:180+` |

The Angular shell mirrors this with `can('analytical-quality.create' \| '.edit' \| '.sign' \|
'.approve' \| '.manage')` in the `features/analytical/*` components — a **UI-only** hint that does not
constitute enforcement for the ungated endpoints above.

### 1.8 Persistence

Schema `qams`. Thirteen root tables + thirteen owned child tables:

| Root table | Child table | Child FK (tenant-composite, `Hardening4_ChildTenancy`) |
|---|---|---|
| `precision_study` | `precision_measurement` | `fk_precision_measurement_precision_study_tenant (study_id, tenant_id)` (`Hardening4_ChildTenancy.cs:409-410`) |
| `method_comparison_study` | `measurement_pair` | `fk_measurement_pair_method_comparison_study_tenant` (`:397-398`) |
| `linearity_study` | `linearity_measurement` | `fk_linearity_measurement_linearity_study_tenant` (`:388-389`) |
| `detection_limit_study` | `detection_measurement` | `fk_detection_measurement_detection_limit_study_tenant` (`:370-371`) |
| `reference_interval_study` | `reference_sample` | `fk_ref_sample_ri_study_tenant` (name pinned in EF, `AnalyticalQualityConfigurations.cs:273`) |
| `sigma_assessment` | *(none)* | — |
| `carryover_study` | `carryover_reading` | `fk_carryover_reading_carryover_study_tenant` (`:367-368`) |
| `interference_study` | `interference_measurement` | `fk_interference_measurement_interference_study_tenant` (`:382-383`) |
| `lot_comparison_study` | `lot_sample_pair` | `fk_lot_sample_pair_lot_comparison_study_tenant` (`:391-392`) |
| `instrument_comparability_study` | `instrument_reading` | `fk_instrument_reading_instrument_comparability_study_tenant` (`:379-380`) |
| `outlier_screening` | `outlier_point` | `fk_outlier_point_outlier_screening_tenant (screening_id, tenant_id)` (`:406-407`) |
| `uncertainty_budget` | `uncertainty_component` | `fk_unc_component_unc_budget_tenant` (pinned, `AnalyticalQualityConfigurations.cs:118`) |
| `validation_study` | `validation_replicate` | `fk_validation_replicate_validation_study_tenant` (`:433-434`) |

- **Keys:** every root and every child uses the tenant-first composite `HasKey(TenantId, Id)`
  (`AnalyticalQualityConfigurations.cs:12, 46, 100, 163, 195, 224, 254, 287, 305, 335, 362, 389, 418,
  445`, children at `:64, 119, 181, 212, 241, 274, 322, 350, 377, 406, 433, 461`). There is **no
  `UNIQUE(id)`** — per the standing rule in `CLAUDE.md` §5, a case that asserts one is wrong.
- **Concurrency token:** `xmin`. There is **no `row_version` column**.
- **Unique index per tenant:** `ux`/`ix_<table>_tenant_id_<ref>` `IsUnique()` on every root
  (`StudyRef` / `AssessmentRef` / `ScreeningRef` / `BudgetRef`), plus a non-unique
  `(tenant_id, state)` index on eleven roots and `(tenant_id, status)` on `uncertainty_budget`
  (`AnalyticalQualityConfigurations.cs:107-108`).
- **Reference-number prefixes** (`IReferenceNumberGenerator.NextAsync(tenantId, prefix)`):
  `PR` (`PrecisionSlice.cs:36`), `MC` (`:36`), `LIN` (`LinearitySlice.cs:36`),
  `DL` (`DetectionLimitSlice.cs:36`), `RI` (`ReferenceIntervalSlice.cs:37`),
  `SIG` (`SigmaAssessmentSlice.cs:36`), `CAR` (`CarryoverSlice.cs:30`),
  `INT` (`InterferenceSlice.cs:30`), `LOT` (`LotComparisonSlice.cs:33`),
  `ICP` (`InstrumentComparabilitySlice.cs:32`), `OUT` (`OutlierScreeningSlice.cs:29`),
  `MU` (`UncertaintySlice.cs:38`), `MV` (`ValidationAndPtSlice.cs:35`).
- **RLS:** all thirteen roots and all thirteen children carry `ENABLE`+`FORCE ROW LEVEL SECURITY`
  with policy `tenant_isolation`. Roots from `20260725201422_AnalyticalComplianceModules.cs:371-390`
  and the earlier per-study migrations; children retro-fitted by
  `20260731201114_Hardening4_ChildTenancy.cs:483+`.
- **CHECK domains** (`20260731191212_Hardening3_CheckDomains.cs`): eleven roots get
  `ck_<table>_state_domain CHECK (state IN ('DataEntry','Calculated','SignedOff'))`;
  `ck_sigma_assessment_state_domain CHECK (state IN ('Draft','SignedOff'))` (`:135`);
  `ck_sigma_assessment_grade_domain CHECK (grade IN ('Unacceptable','Marginal','Good','Excellent','WorldClass'))` (`:133`);
  `ck_reference_interval_study_verdict_domain CHECK (verdict IN ('Verified','Rejected'))` (`:125`);
  `ck_uncertainty_budget_status_domain CHECK (status IN ('Draft','Calculated','Approved'))` (`:143`);
  `ck_validation_study_state_domain CHECK (state IN ('ProtocolConfigured','DataEntered','StatsCalculated','SignedOff'))` (`:151`).
  All are added `NOT VALID` then `VALIDATE`d in the same migration.
- **Enum storage:** every state/verdict/grade/kind is `HasConversion<string>()` with an explicit
  `HasMaxLength` (10–20).
- **Precision:** `HasPrecision` is declared only on `validation_study`
  (`TotalAllowableError (10,3)`, `MeanBias (10,3)`, `Cv (10,3)` — `AnalyticalQualityConfigurations.cs:51-53`)
  and on the QC tables. **None of the twelve MV study tables declares precision on any derived
  decimal column** (slope, intercept, `PearsonR`, `Lob`, `Lod`, `Loq`, `CarryoverPct`,
  `CombinedStandardUncertainty`, …), so PostgreSQL stores them as unconstrained `numeric`. The domain
  rounds to 2–4 decimal places before assignment, so no data is lost, but there is no schema-level
  guarantee. → **GAP-MV-005**.

### 1.9 Signed-record immutability (`reject_frozen_mutation`)

`src/NT.QAMS.Infrastructure/Persistence/Migrations/20260726084134_SignedRecordImmutability.cs`.

- The function (`:38-53`) is `qams.reject_frozen_mutation()`, `plpgsql`, parameterised by
  `TG_ARGV[0]` = frozen column and `TG_ARGV[1]` = frozen value. It reads
  `row_to_json(OLD) ->> frozen_col` and, when it equals the frozen value, raises with
  `ERRCODE = 'check_violation'` (**SQLSTATE `23514`**, asserted by
  `tests/NT.QAMS.IntegrationTests/SignedRecordImmutabilityTests.cs:42`).
- The trigger `frozen_immutability` is `BEFORE UPDATE OR DELETE … FOR EACH ROW` (`:60-62`) on
  **exactly 13 tables** (`:14-29`): `validation_study, method_comparison_study, precision_study,
  linearity_study, detection_limit_study, reference_interval_study, sigma_assessment,
  outlier_screening, carryover_study, lot_comparison_study, interference_study,
  instrument_comparability_study` (all on `state = 'SignedOff'`) and `uncertainty_budget`
  (on `status = 'Approved'`).
- Because the guard inspects **OLD**, the transition *into* the frozen state passes (`:34-36`, proved
  by `SignedRecordImmutabilityTests.cs:34`).
- **No child measurement table carries the trigger.** A grep for `frozen_immutability` across all 57
  migrations returns only `20260726084134`. `precision_measurement`, `measurement_pair`,
  `linearity_measurement`, `detection_measurement`, `reference_sample`, `carryover_reading`,
  `interference_measurement`, `lot_sample_pair`, `instrument_reading`, `outlier_point`,
  `uncertainty_component` and `validation_replicate` are DB-mutable even when their parent is signed.
  The domain blocks it (`RequireEditable`), the database does not. → **GAP-MV-006**.
- A trigger rejection surfaces as `Npgsql.PostgresException` inside `DbUpdateException`, which
  `DomainExceptionHandler.cs:26-82` does **not** match → falls through to the default handler → **500**,
  not a typed problem+json. No API path reaches it today (the domain guard fires first), so this is
  latent. → **GAP-MV-007**.

### 1.10 Audit trail and reason for change

- `FieldChangeInterceptor` (`src/NT.QAMS.Infrastructure/Persistence/Interceptors/FieldChangeInterceptor.cs:22`)
  emits `FieldChangeRecord` rows in the **same transaction** for every tracked entity that is
  `Added` (`"Created"`), `Deleted` (`"Deleted"`) or `Modified` (per-property old/new). MV roots and
  their owned children are not in the `Excluded` set (`:27-30`), so every study create, measurement
  add and measurement delete writes to `audit.field_change`.
- Property names containing `password, secret, pin, hash, token` are redacted (`:33`) — no MV
  property matches.
- `ChangeReasonMiddleware` (`src/NT.QAMS.WebApi/Middleware/RequestIdentity.cs:143-161`) refuses **any**
  DELETE without `X-Change-Reason` with `400 CHANGE-REASON-REQUIRED` (`:154`) *before* routing, and
  stamps an accepted reason onto the ledger row. This applies to all eleven MV child-DELETE
  endpoints. Non-DELETE requests pass through and the header is honoured if present — so
  `POST /{id}/calculate` and `POST /{id}/sign-off` do **not** require a reason.
- **No MV sign-off invokes `IESignatureService`.** The only consumer of the port in `src/` is
  `src/NT.QAMS.Application/DocumentControl/Commands/DocumentCommands.cs:122`. MV sign-off is a bare
  `POST` gated on `analytical-quality.sign`; no password, no PIN, and **no `SignatureRecord` row is
  written**. → **GAP-MV-008**.

---

## 2. Divergences from the commissioning brief

The brief is reproduced in the binding conventions file (§1 stack table, §2 functional facts). Each
row below states the brief's assumption, what the code does, and the gap it opens.

| # | What the brief assumes | What the code does | file:line | Gap |
|---|---|---|---|---|
| 1 | `SIG-010`/`SIG-011` are electronic-signature codes | They are `SigmaAssessment` state-transition codes; `SIG-001/002/003/404` are additionally double-booked across two subsystems | `SigmaAssessment.cs:53,72,77,82,101`; `ComplianceLedgerServices.cs:94,101,108,114` | GAP-MV-001 |
| 2 | "electronic sign-off" of analytical studies (URS-038) implies a Part-11 §11.200 signing act | MV sign-off is a plain permission-gated POST; `IESignatureService` is never called and no `SignatureRecord` is produced | `PrecisionStudiesController.cs:60-66`; `DocumentCommands.cs:122` is the only consumer | GAP-MV-008 |
| 3 | Analytical study sign-off notifies someone | No `INotificationHandler` exists for any of the 13 MV events; the outbox is the terminus | `NotificationPolicies.cs:24-33` | GAP-MV-002 |
| 4 | Endpoint gating is `[RequirePermission(module, action)]` on the write surface | 35 of the 84 MV write actions carry no `[RequirePermission]`; measurement entry, bulk import, calculate and evidence deletion are open to any internal actor | `LinearityStudiesController.cs:34,42,49`; identical shape in 10 sibling controllers | GAP-MV-004 |
| 5 | The `analytical-quality` module publishes 8 governable actions | `view`, `export` and `void` gate nothing; only `create/sign/edit/manage/approve` are wired | `PermissionCatalog.cs:171-173` vs 32 attribute sites | GAP-MV-003 |
| 6 | Signed-record immutability protects the analytical record at the DB layer (URS-041) | The trigger protects the 13 **roots** only; the 12 child evidence tables are DB-mutable while the parent is signed | `SignedRecordImmutability.cs:14-29`; no other migration mentions `frozen_immutability` | GAP-MV-006 |
| 7 | Domain rule breaches surface as HTTP **422** with the domain code | True for `DomainException`, but **every `InvalidStateTransitionException` is 409**, and that covers 25 of the module's codes (`PR-012/013`, `MC-012/013`, `LIN-012/013`, `DL-013/014`, `RI-011/012`, `SIG-010/011`, `CAR-013/014`, `INT-013/014`, `LOT-012/013`, `ICP-013/014`, `OUT-011/012`, `MU-010/011`, `MV-010/012/015`) | `DomainExceptionHandler.cs:45-50` | GAP-MV-009 |
| 8 | Westgard multirule QC is 1-3s / 2-2s / R-4s / 10-x + warning 1-2s, and 4-1s is **not implemented** | `SigmaAssessment.QcRecommendation` advises laboratories to run `4:1s` and `8:x` — two rules the evaluator cannot apply | `SigmaAssessment.cs:118-119` vs `WestgardEvaluator.cs` (conventions §2 "QC / Westgard") | GAP-MV-010 |
| 9 | Bulk import is a general LIS/analyzer capability | Only `precision-studies` and `method-comparisons` expose `/import`; the other ten studies have no bulk path | `ApiSurface.approved.txt:340,372` | GAP-MV-011 |
| 10 | CSV import exists | The **API** takes typed JSON `rows`; CSV parsing lives entirely in the Angular `qams-csv-import` component, which posts parsed rows | `AnalyticalQualityContracts.cs:42,46-50`; `frontend/src/app/shared/ui/csv-import.component.ts`; wired at `method-comparison-detail.component.ts:142` and `precision-detail.component.ts:115` | GAP-MV-012 |
| 11 | A study protocol identifies a CLSI guideline | `ValidationStudy.Protocol` is free text, `Trim().ToUpperInvariant()`, `varchar(30)`, with no enumeration, no catalogue, no validation | `ValidationStudy.cs:72`; `AnalyticalQualityConfigurations.cs:49` | GAP-MV-013 |
| 12 | Study types are labelled with the CLSI protocol they implement | CLSI strings (`EP05/EP06/EP07/EP09/EP10/EP17/EP28`) exist **only** in XML doc comments, contract section comments, controller summaries, help text and i18n strings — never in a column, a validator, an enum or a response field | see §3.1 "Where the CLSI string actually lives" | GAP-MV-014 |

---

## 3. State-transition matrices

### 3.1 THE STUDY INVENTORY TABLE

*Read this as the authoritative map. "CLSI protocol" is filled in **only where the source says so**,
and the column states exactly where the string lives. "Statistics computed" names the algorithm and
the denominator, because that is what the `TC-MV-STAT-*` cases must assert.*

| # | Study type | Aggregate (file:line) | CLSI protocol — **and where the string actually is** | Exact statistics computed (algorithm · denominator) | Endpoints | States |
|---|---|---|---|---|---|---|
| 1 | Imprecision | `PrecisionStudy` (`PrecisionStudy.cs:35`) | **EP05** — XML doc `PrecisionStudy.cs:27`; controller summary `PrecisionStudiesController.cs:11`; contract comment `AnalyticalQualityContracts.cs:222`; i18n `help-content.ts:628`. **Not stored, not validated, not returned.** | **One-way random-effects ANOVA** (`:132-188`). `MSW = SSW/(n−k)` = repeatability variance; `MSB = SSB/(k−1)`; `n₀ = (n − Σnᵢ²/n)/(k−1)`; between-run variance `= max(0, (MSB − MSW)/n₀)`; `RepeatabilitySd = √MSW`; `BetweenRunSd = √betweenVar`; `WithinLabSd = √(MSW + betweenVar)`. CV% `= sd/grandMean × 100`, **null when grandMean == 0** (`:213-214`). All rounded to **4 dp** (`:216`). Claims verdicts: `MeetsRepeatabilityClaim = RepeatabilityCvPct ≤ claim`, likewise within-lab; **null when the claim is null** (`:184-185`). Denominators are **n−k and k−1, never n−1**. | `api/precision-studies` — 2 GET, 5 POST (incl. `/measurements/import`), 1 DELETE | `DataEntry → Calculated → SignedOff` |
| 2 | Method comparison | `MethodComparisonStudy` (`:35`) | **EP09** — XML doc `:26`; `MethodComparisonsController.cs:11`; `AnalyticalQualityContracts.cs:111`; `help-content.ts:615`; `i18n.service.ts:613`. `RecommendedMinimumPairs = 40` is labelled "EP09 recommends" (`:37`) but is **advisory only** — exposed as the computed flag `MeetsRecommendedPower` (`:204`, EF-`Ignore`d at `AnalyticalQualityConfigurations.cs:185`); it never blocks `Calculate` or `SignOff`. | **Four fits, all from the same pairs** (`:129-186`). (a) **Pearson r** `= sxy/√(sxx·syy)`. (b) **Ordinary Deming, λ = 1 hard-coded** (`:159`): `slope = (syy − λ·sxx + √((syy − λ·sxx)² + 4λ·sxy²))/(2·sxy)`, `intercept = ȳ − slope·x̄`. (c) **Passing–Bablok** (`:211-252`): all pairwise slopes `(yⱼ−yᵢ)/(xⱼ−xᵢ)` **excluding equal-X pairs**, sorted; shift `K = count(slope < −1.0)`; odd count → `slopes[(count−1)/2 + K]`, even → mean of `slopes[count/2+K−1]` and `slopes[count/2+K]`; intercept = **median** of `yᵢ − slope·xᵢ`. (d) **Bland–Altman** on `dᵢ = yᵢ − xᵢ`: `MeanBias = d̄`; `BiasSd` from **sample variance, denominator n−1** (`:177`); `LoA = d̄ ± 1.96·sd` (`:181-182`). All rounded **4 dp**. | `api/method-comparisons` — 2 GET, 5 POST (incl. `/pairs/import`), 1 DELETE | `DataEntry → Calculated → SignedOff` |
| 3 | Linearity / AMR | `LinearityStudy` (`:39`) | **EP06** — XML doc `:30` ("EP06 2nd-edition style"); `LinearityStudiesController.cs:11`; `AnalyticalQualityContracts.cs:132`; `help-content.ts:641`. `MinimumLevels = 4` with the message "(EP06 recommends 5–9)" (`:135`) — **the code enforces 4, not 5**. | **Ordinary least squares on the per-level MEANS**, not on the raw replicates (`:131-160`): levels grouped by `AssignedValue`, `x = assigned`, `y = mean(measured)`, `slope = sxy/sxx`, `intercept = ȳ − slope·x̄`, `CorrelationR = sxy/√(sxx·syy)` or **exactly `1m` when `syy == 0`** (`:160`). Per level (`:207-223`): `fitted = slope·assigned + intercept`; `deviation% = (mean − fitted)/fitted × 100` rounded **3 dp**, forced to `0` when `fitted == 0`; `recovery% = mean/assigned × 100` rounded **2 dp**; `Passes = |deviation%| ≤ AllowableDeviationPct`. `IsLinear = all levels pass on the FULL-range fit`. **AMR** (`:225-278`): brute-force search over every contiguous window of ≥4 levels; each window is **re-fitted on its own levels** and must have every deviation inside the criterion; the winner is the window with the **most levels**, ties broken by the **widest assigned-value span**. `AmrLow/AmrHigh` are null when no window passes. | `api/linearity-studies` — 2 GET, 4 POST, 1 DELETE | `DataEntry → Calculated → SignedOff` |
| 4 | Detection capability | `DetectionLimitStudy` (`:42`) | **EP17** — XML doc `:33` ("EP17, classical parametric approach"); `Z = 1.645m` documented as "z for α = β = 0.05 per EP17's classical option" (`:44-45`); `DetectionLimitStudiesController.cs:11`; `AnalyticalQualityContracts.cs:157`; `help-content.ts:654`. | (`:136-193`) `BlankMean = mean(blanks)`; `BlankSd` = **sample SD, denominator n−1** (`:240-249`); **`LoB = BlankMean + 1.645·BlankSd`**. `PooledLowSd = √(Σ_g SS_g / Σ_g (n_g − 1))` — a true pooled within-sample SD across low-level groups, **skipping any group with a single replicate** (`:160-181`); `dfWithin == 0` → `DL-012`. **`LoD = LoB + 1.645·PooledLowSd`**. **`LoQ` = functional sensitivity**: the *lowest* assigned low-level concentration whose group has `n > 1`, `CV ≤ LoqCvTargetPct` **and** `mean ≥ LoD` (`:233`); **null when none qualifies** — the aggregate does not fall back to LoD. All rounded **4 dp**; per-level CV rounded **2 dp** (`:232`). | `api/detection-limit-studies` — 2 GET, 4 POST, 1 DELETE | `DataEntry → Calculated → SignedOff` |
| 5 | Reference-interval verification | `ReferenceIntervalStudy` (`:35`) | **EP28-A3c** — XML doc `:26`; constant comment `:37`; error message `:134`; `ReferenceIntervalStudiesController.cs:11`; `AnalyticalQualityContracts.cs:182`; `help-content.ts:667`. | **Binomial small-N transference rule, no distribution fitting** (`:128-145`): `OutsideCount = count(v < ClaimedLower OR v > ClaimedUpper)`; `AllowedOutside = floor(n × 0.10)`; `Verdict = Verified` iff `OutsideCount ≤ AllowedOutside`, else `Rejected`. No percentile estimation, no non-parametric CI, no partitioning. `n ≥ 20` enforced. | `api/reference-interval-studies` — 2 GET, 4 POST, 1 DELETE | `DataEntry → Calculated → SignedOff` |
| 6 | Six-Sigma metric | `SigmaAssessment` (`:19`) | **None.** No CLSI string anywhere in the aggregate, the slice, the controller or the contracts. Documented as "Analytical Six-Sigma assessment" (`:16`). | (`:68-94`) `σ = (TEa% − |bias%|) / CV%` rounded **2 dp**, **floored at exactly `0m` when the numerator ≤ 0** (`:92`). Grade bands, inclusive lower bound (`:123-130`): `≥6 WorldClass`, `≥5 Excellent`, `≥4 Good`, `≥3 Marginal`, else `Unacceptable`. `QcRecommendation` (`:114-121`) is a **computed string**, EF-`Ignore`d (`AnalyticalQualityConfigurations.cs:295`), with the same five bands. | `api/sigma-assessments` — 2 GET, 2 POST, **1 PUT** (the only PUT in the module), no DELETE | `Draft → SignedOff` (**two states only**) |
| 7 | Carryover | `CarryoverStudy` (`:37`) | **EP10-style** — XML doc `:29` says "CLSI EP10-**style**"; `CarryoverStudiesController.cs:11`; `AnalyticalQualityContracts.cs:64`; `help-content.ts:706`. The hedge "-style" is in the source; do not upgrade it to a conformance claim. | (`:112-144`) Lows ordered by `Sequence` (LINQ `OrderBy`, stable on ties). `MeanHigh = mean(all High)`; `FirstLow = lows[0].Value` (**stored unrounded**, `:139`); `SteadyLow = mean(lows.Skip(1))`. **`Carryover% = (FirstLow − SteadyLow)/(MeanHigh − SteadyLow) × 100`** rounded **4 dp**; `CAR-012` when the denominator is exactly zero. `Passes = |Carryover%| ≤ AllowableCarryoverPct`. | `api/carryover-studies` — 2 GET, 4 POST, 1 DELETE | `DataEntry → Calculated → SignedOff` |
| 8 | Interference / specificity | `InterferenceStudy` (`:41`) | **EP07** — XML doc `:34`; `InterferenceStudiesController.cs:11`; `AnalyticalQualityContracts.cs:86`; `help-content.ts:732`. | (`:127-198`) `ControlMean = mean(control replicates)`, `≥3` required, `INT-012` if exactly zero. Per interferent group (ordered by name): `MeanTest = mean(group)`; **`Bias% = (MeanTest − ControlMean)/ControlMean × 100`** rounded **3 dp**; `SignificantInterference = |Bias%| > AllowableBiasPct` (**strictly greater** — the boundary value is *not* significant). Persisted: `ControlMean` (4 dp), `InterferentCount`, `SignificantCount`; the per-interferent table is recomputed on read from the stored `ControlMean` (`:157-165`). | `api/interference-studies` — 2 GET, 4 POST, 1 DELETE | `DataEntry → Calculated → SignedOff` |
| 9 | Reagent/control lot comparison | `LotComparisonStudy` (`:32`) | **None.** No CLSI string in aggregate, slice, controller or contracts. | (`:123-144`) **Bias of the means, not the mean of the biases**: `MeanCurrent = mean(CurrentLotValue)`, `MeanNew = mean(NewLotValue)`, **`MeanBiasPct = (MeanNew − MeanCurrent)/MeanCurrent × 100`** rounded **4 dp**; `Passes = |MeanBiasPct| ≤ AllowableBiasPct`. Contrast with study #10, which averages per-sample biases — the two give different numbers on the same data. `≥3` pairs; both values must be `> 0`. | `api/lot-comparisons` — 2 GET, 4 POST, 1 DELETE | `DataEntry → Calculated → SignedOff` |
| 10 | Instrument comparability | `InstrumentComparabilityStudy` (`:37`) | **None.** XML doc `:31` describes it as intra-laboratory instrument-to-instrument; it is **not** an interlaboratory comparison. | (`:120-191`) Reference readings indexed by `SampleId` (`StringComparer.OrdinalIgnoreCase`). For each non-reference instrument: **mean of the per-shared-sample percentage biases** `(value − refValue)/refValue × 100`, skipping samples the reference did not run and samples whose reference value is `0`; rounded **3 dp**; `Comparable = pairedSamples > 0 AND |meanBias| ≤ AllowableBiasPct`. Persisted: `InstrumentCount` (non-reference distinct instruments) and `NonComparableCount`; the per-instrument table is recomputed on read (`:155-166`). | `api/instrument-comparabilities` — 2 GET, 4 POST, 1 DELETE | `DataEntry → Calculated → SignedOff` |
| 11 | Outlier screening / normalisation | `OutlierScreening` (`:35`) | **None** — the source names the *statistical* methods (Tukey, Iglewicz–Hoaglin), not a CLSI protocol (`:28-33`). | (`:106-213`) `Mean`; `Sd` = **sample SD, denominator n−1** (`:117`); `Median`; **`Q1`/`Q3` by linear-interpolation quantile "type 7"**, explicitly documented as matching spreadsheet `PERCENTILE` (`:199-213`); `IQR = Q3 − Q1`; **Tukey fences** `Q1 − 1.5·IQR` / `Q3 + 1.5·IQR`; `MAD = median(|x − median|)`; **modified z** `= 0.6745·(x − median)/MAD`, **`0` when MAD == 0**. A point is an outlier when **beyond a Tukey fence OR |modified z| > 3.5** (strictly greater, `:188`). Each point also carries a plain z (`(x − mean)/sd`, `0` when `sd == 0`). Rounded **4 dp**. `n ≥ 4`. | `api/outlier-screenings` — 2 GET, 4 POST, 1 DELETE | `DataEntry → Calculated → SignedOff` |
| 12 | Measurement uncertainty | `UncertaintyBudget` (`:45`) | **None (not CLSI).** Cited standards are **ISO 17025 §7.6 / ISO 15189 §7.3.4 / GUM** — XML doc `:37-38`; `UncertaintyController.cs:11`. | (`:148-161`) **GUM root-sum-of-squares on RELATIVE standard uncertainties (%)**: `u_c = √(Σ uᵢ²)` rounded **4 dp**; `U = k · u_c` rounded **4 dp**; `MeetsTarget = U ≤ TargetExpandedUncertainty`, **null when no target is set**. `k ∈ [1,4]` (`:90-93`). Components are typed `TypeA`/`TypeB` for reporting only — **the type does not affect the arithmetic**. Adding or removing any component resets `Status` to `Draft` and nulls all three results (`:128-131, 141-144`). | `api/uncertainty-budgets` — 2 GET, 4 POST (`/components`, `/calculate`, `/approve`), 1 DELETE | `Draft → Calculated → Approved` (**"Approved", not "SignedOff"**) |
| 13 | CLSI validation study *(adjacent)* | `ValidationStudy` (`:30`) | **Free-text.** `Protocol` is whatever the user typed, upper-cased, `varchar(30)`. The only CLSI hint in the product is the Angular placeholder "e.g. CLSI EP15-A3" (`frontend/src/app/core/i18n.service.ts:261`) — a hint, not a constraint. | (`:106-141`) `CV% = sd/|mean| × 100` rounded **3 dp**, `sd` from **sample variance, denominator count−1** (`:125`); `MeanBias%` = mean of per-replicate `(measured − reference)/reference × 100` over replicates whose `Reference > 0`, rounded **3 dp** (null when none has a reference); **`TotalError = |MeanBias| + 1.65 · CV`** (one-sided 95 %, `:136-137`); `Passed = TotalError ≤ TotalAllowableError`. | `api/validation-studies` — 2 GET, 4 POST, **no DELETE** | `ProtocolConfigured → DataEntered → StatsCalculated → SignedOff` |

**Verification of the EP05 / EP09 / EP06 / EP17 strings — the direct answer.** All four (and EP07,
EP10, EP28) appear in exactly four kinds of place, and **govern nothing**:

1. C# XML doc comments on the aggregate (`PrecisionStudy.cs:27`, `MethodComparisonStudy.cs:26,37,203`,
   `LinearityStudy.cs:30,41,135,167`, `DetectionLimitStudy.cs:33,44,47`,
   `ReferenceIntervalStudy.cs:26,37,134`, `InterferenceStudy.cs:34`, `CarryoverStudy.cs:29`).
2. Section comments in `src/NT.QAMS.Contracts/AnalyticalQuality/AnalyticalQualityContracts.cs:64, 86,
   111, 132, 157, 182, 222`.
3. `<summary>` on the controllers (`PrecisionStudiesController.cs:11` and the six siblings).
4. Angular help text and i18n dictionaries (`frontend/src/app/core/help/help-content.ts:615, 628,
   641, 654, 667, 706, 732`; `frontend/src/app/core/i18n.service.ts:261, 613`).

There is **no `Protocol` column, no protocol enum, no protocol validator and no protocol field in any
response DTO** for the twelve MV studies. The single `protocol` column in the module belongs to
`validation_study` and is free text. A case must therefore never assert "the API returns protocol
EP05" and must never treat the CLSI reference as a conformance claim. → **GAP-MV-014**.

**Verification of Passing–Bablok, Bland–Altman and CSV/LIS import — the direct answer.**

- **Passing–Bablok: EXISTS.** Real non-parametric implementation at
  `MethodComparisonStudy.cs:211-252`, persisted as `PassingBablokSlope` / `PassingBablokIntercept`
  (`:64-65`) and returned in `MethodComparisonDetailDto` (`MethodComparisonSlice.cs:182`). It is the
  shifted-median-of-pairwise-slopes method with `K = count(slope < −1)`. **It does not compute a
  confidence interval, does not exclude slopes equal to −1, and does not handle the tied-X case other
  than by dropping the pair.** The shift index is unguarded (see GAP-MV-015).
- **Bland–Altman: EXISTS, partially.** Mean bias, bias SD and 95 % limits of agreement are computed
  and persisted (`MethodComparisonStudy.cs:169-182`; `MeanBias, BiasSd, LimitOfAgreementLower,
  LimitOfAgreementUpper`). **There is no proportional-bias regression on the differences, no CI on
  the limits, and no percentage/ratio Bland–Altman variant.** The differences are absolute
  (`y − x`), never relative.
- **CSV import: DOES NOT EXIST SERVER-SIDE.** No `text/csv` content type, no CSV parser, no file
  upload, no `IFormFile` anywhere in the MV scope. The two `/import` endpoints
  (`PrecisionStudiesController.cs:42`, `MethodComparisonsController.cs:42`) accept
  `ImportPrecisionMeasurementsRequest` / `ImportMeasurementPairsRequest` — typed JSON `Rows`
  collections (`AnalyticalQualityContracts.cs:46-50`). CSV parsing is a **frontend** concern
  (`frontend/src/app/shared/ui/csv-import.component.ts`), wired into exactly two detail screens
  (`method-comparison-detail.component.ts:142`, `precision-detail.component.ts:115`).
- **LIS import: DOES NOT EXIST.** The phrase "LIS / analyzer CSV" appears once, as a comment
  (`AnalyticalQualityContracts.cs:42`) and in two handler doc comments
  (`PrecisionSlice.cs:100-104`, `MethodComparisonSlice.cs:100-105`). There is no LIS connector, no
  HL7/ASTM parser, no inbound integration endpoint and no scheduled ingestion anywhere in `src/`.
  → **GAP-MV-011**, **GAP-MV-012**.

### 3.2 The shared study state machine (the eleven `DataEntry / Calculated / SignedOff` studies)

Applies verbatim to Precision, Method Comparison, Linearity, Detection Limit, Reference Interval,
Carryover, Interference, Lot Comparison, Instrument Comparability and Outlier Screening (10 studies;
Sigma and Uncertainty have their own machines below).

| From \ Trigger | `Configure` (create) | `Add…` child | `Remove…` child | `Calculate` | `SignOff` |
|---|---|---|---|---|---|
| *(none)* | → **DataEntry** | — | — | — | — |
| **DataEntry** | — | stays **DataEntry** + `Invalidate()` | stays **DataEntry** + `Invalidate()` | → **Calculated** *(or the study's precondition code)* | ✗ `*-012`/`*-011`/`*-013` **409** |
| **Calculated** | — | **→ DataEntry** (silent demotion + all derived nulled) | **→ DataEntry** (silent demotion) | → **Calculated** (recomputed in place) | → **SignedOff** *(or `SOD-AQ-001` 422 first)* |
| **SignedOff** | — | ✗ `*-013`/`*-014`/`*-012` **409** | ✗ same **409** | ✗ same **409** | ✗ `*-012`/`*-011`/`*-013` **409** |

Notes the case authors must encode:

1. **`Invalidate()` is a silent demotion.** `PrecisionStudy.cs:218-228` (and its eleven twins) nulls
   every derived property and, *only if the state was `Calculated`*, sets it back to `DataEntry`. The
   HTTP response to `POST /{id}/measurements` is still `200` with the new child id — **the state
   change is invisible in the response body**. A `TC-MV-DF-*` case must re-`GET` the study to observe it.
2. **`Calculate` is idempotent-in-place.** `RequireEditable()` allows `Calculated → Calculated`; the
   statistics are recomputed from the same data and must be byte-identical.
3. **SoD is evaluated BEFORE the state guard.** In all thirteen aggregates
   `EnsureSignerIsNotPreparer(actorId, "SOD-AQ-001")` is the **first** statement of `SignOff`
   (`PrecisionStudy.cs:200` before the `State != Calculated` check at `:201`). Therefore a preparer
   attempting to sign a **`DataEntry`** study receives **422 `SOD-AQ-001`**, not 409. This ordering is
   uniform and must be asserted.
4. **SoD is a no-op when `CreatedByUserId` is null** (`AggregateRoot.cs:38`) — accepted residual
   F-05b. Any legacy or system-seeded study can be self-signed. Cases covering this must be labelled
   `[ID]`, not treated as a defect.
5. There is **no transition out of `SignedOff`.** No unlock, no supersede, no void.

### 3.3 `SigmaAssessment` state machine

| From \ Trigger | `Create` | `SetInputs` (`PUT /{id}`) | `SignOff` |
|---|---|---|---|
| *(none)* | → **Draft** (σ and grade computed immediately inside `Create` via `SetInputs`, `SigmaAssessment.cs:63`) | — | — |
| **Draft** | — | stays **Draft**, σ + grade recomputed | → **SignedOff** *(or `SOD-AQ-001` 422 first)* |
| **SignedOff** | — | ✗ `SIG-010` **409** | ✗ `SIG-011` **409** |

There is **no `Calculated` state** — σ is always current because it is derived synchronously on every
input change. A case asserting a `Calculate` endpoint for sigma is invalid; none exists.

### 3.4 `UncertaintyBudget` state machine

| From \ Trigger | `Create` | `AddComponent` | `RemoveComponent` | `Calculate` | `Approve` |
|---|---|---|---|---|---|
| *(none)* | → **Draft** | — | — | — | — |
| **Draft** | — | stays **Draft** | stays **Draft** (or `MU-006` **422**) | → **Calculated** (or `MU-007` **422** if empty) | ✗ `MU-010` **409** |
| **Calculated** | — | **→ Draft** (`:128`) | **→ Draft** (`:141`) | → **Calculated** | → **Approved** *(or `SOD-AQ-001` 422 first)* |
| **Approved** | — | ✗ `MU-011` **409** | ✗ `MU-011` **409** | ✗ `MU-011` **409** | ✗ `MU-010` **409** |

Two asymmetries versus the shared machine: the frozen value is **`Approved`**, not `SignedOff`, and
the frozen column is **`status`**, not `state` (`SignedRecordImmutability.cs:28`). The
frozen-state guard `MU-011` is raised by `RequireMutable()`, and `Approve` on an already-approved
budget hits the `Status != Calculated` branch first, so it returns **`MU-010`**, not `MU-011`.

### 3.5 `ValidationStudy` state machine

| From \ Trigger | `Configure` | `EnterReplicate` | `CalculateStatistics` | `SignOff` |
|---|---|---|---|---|
| *(none)* | → **ProtocolConfigured** | — | — | — |
| **ProtocolConfigured** | — | → **DataEntered** | ✗ `MV-012` **409** | ✗ `MV-015` **409** |
| **DataEntered** | — | stays **DataEntered** | → **StatsCalculated** (or `MV-013`/`MV-014` **422**) | ✗ `MV-015` **409** |
| **StatsCalculated** | — | **→ DataEntered**, results nulled (`:85-91`) | → **StatsCalculated** | → **SignedOff** *(or `SOD-AQ-001` 422 first)* |
| **SignedOff** | — | ✗ `MV-010` **409** | ✗ `MV-012` **409** | ✗ `MV-015` **409** |

Unlike the other twelve, `ValidationStudy` has **no `RemoveReplicate`** operation and therefore no
DELETE endpoint — replicates can only accumulate.

### 3.6 Interaction between the state machine and `reject_frozen_mutation`

| Layer | Trigger fires? | Observable outcome |
|---|---|---|
| `POST /{id}/sign-off` on a `Calculated` study | **No** — OLD.state is `Calculated`, not the frozen value | `204`, row updated to `SignedOff` |
| `POST /{id}/measurements` on a `SignedOff` study | **No** — the domain refuses in memory; EF never issues SQL | `409` `<PREFIX>-013`/`-014` problem+json |
| `POST /{id}/sign-off` on a `SignedOff` study | **No** — domain refuses first | `409` `<PREFIX>-012` |
| Raw `UPDATE qams.<root> …` on a signed row (psql, script, migration) | **Yes** | `PostgresException` SQLSTATE **`23514`**, message `signed/approved record is immutable and cannot be modified or deleted (qams.<table> is SignedOff)` (`SignedRecordImmutability.cs:46-49`) |
| Raw `DELETE FROM qams.<root>` on a signed row | **Yes** | same `23514` (`SignedRecordImmutabilityTests.cs:58-62`) |
| Raw `UPDATE`/`DELETE` on a **child** row of a signed study | **No trigger exists** | Succeeds silently → **GAP-MV-006** |
| Any path that did reach the trigger through EF | **Yes** | `DbUpdateException` unmapped by `DomainExceptionHandler` → **HTTP 500**, not problem+json → **GAP-MV-007** |

Cascade note: the child FKs are `ON DELETE CASCADE` on `(study_id, tenant_id)`
(`Hardening4_ChildTenancy.cs:367-434`), but a `DELETE` of a signed root is refused by the trigger
before any cascade is evaluated, so children of a signed study cannot be removed *via the parent*.

---

## 4. Decision tables

### DT-MV-01 — Precision claim verdicts (`PrecisionStudy.cs:184-185`)

| `ClaimedRepeatabilityCvPct` | `ClaimedWithinLabCvPct` | `RepeatabilityCvPct ≤ claim` | `WithinLabCvPct ≤ claim` | `MeetsRepeatabilityClaim` | `MeetsWithinLabClaim` |
|---|---|---|---|---|---|
| null | null | — | — | **null** | **null** |
| set | null | true | — | `true` | **null** |
| set | null | false | — | `false` | **null** |
| set | set | true | true | `true` | `true` |
| set | set | true | false | `true` | `false` |
| set | set | false | true | `false` | `true` |
| set | set | false | false | `false` | `false` |

Boundary: the comparison is `≤`, so a CV exactly equal to the claim **passes**. Additional rule: if
`GrandMean == 0` both CVs are **null** (`:213-214`), and the `≤` comparison against a null CV yields
`false` in C# (`null <= rc` is `false`), so **`MeetsRepeatabilityClaim` becomes `false`, not null**, on
a zero grand mean — assert this explicitly.

### DT-MV-02 — Linearity per-level and AMR (`LinearityStudy.cs:162-169, 207-278`)

| All levels pass full-range fit | ≥1 contiguous ≥4-level window passes its own refit | `IsLinear` | `AmrLow`/`AmrHigh` |
|---|---|---|---|
| yes | yes (necessarily the full range) | `true` | full range (widest window wins) |
| no | yes | `false` | the passing window with the **most levels**; tie → **widest span** |
| no | no | `false` | **null / null** |
| — | fewer than 4 distinct levels | *unreachable* — `LIN-010` **422** first | — |

### DT-MV-03 — Detection-limit LoQ qualification (`DetectionLimitStudy.cs:223-238`)

| Group replicate count | `CV ≤ LoqCvTargetPct` | `mean ≥ Lod` | `QualifiesForLoq` |
|---|---|---|---|
| 1 | — | — | **false** (`values.Length > 1` fails) |
| ≥2 | false | — | false |
| ≥2 | true | false | false |
| ≥2 | true | true | **true** |

`Loq` = the **lowest** `AssignedValue` among qualifying groups (`:186-190`); **null** when the set is
empty. Boundary: `CV == target` qualifies; `mean == Lod` qualifies.

### DT-MV-04 — Reference-interval verdict (`ReferenceIntervalStudy.cs:137-143`)

| n | `AllowedOutside = floor(n·0.10)` | `OutsideCount` | `Verdict` |
|---|---|---|---|
| 19 | — | — | *unreachable* — `RI-010` **422** |
| 20 | 2 | 0, 1, 2 | `Verified` |
| 20 | 2 | 3+ | `Rejected` |
| 29 | 2 | 2 | `Verified` |
| 29 | 2 | 3 | `Rejected` |
| 30 | 3 | 3 | `Verified` |
| 30 | 3 | 4 | `Rejected` |

### DT-MV-05 — Sigma grade and QC recommendation (`SigmaAssessment.cs:114-130`)

| σ (rounded 2 dp) | `Grade` | `QcRecommendation` (exact leading text) |
|---|---|---|
| `≥ 6` | `WorldClass` | `1:3s, N=2, R=1 — a single rule with minimal QC (world-class capability).` |
| `5 ≤ σ < 6` | `Excellent` | `1:3s / 2:2s / R:4s, N=2, R=1 — a short multirule.` |
| `4 ≤ σ < 5` | `Good` | `1:3s / 2:2s / R:4s / 4:1s, N=4, R=1 — full multirule.` |
| `3 ≤ σ < 4` | `Marginal` | `1:3s / 2:2s / R:4s / 4:1s / 8:x, N=6 — maximum multirule QC.` |
| `σ < 3` (incl. the floored `0`) | `Unacceptable` | `Below 3σ — the method does not meet the minimum; review the process or replace the method.` |

The `4:1s` and `8:x` strings in the Good/Marginal rows name rules the QC engine does not implement
(conventions §2: rejection rules are 1-3s, 2-2s, R-4s, 10-x; warning 1-2s; **4-1s is not
implemented**). → **GAP-MV-010**.

### DT-MV-06 — Carryover verdict (`CarryoverStudy.cs:128-142`)

| High readings | Low readings | `MeanHigh == SteadyLow` | Outcome |
|---|---|---|---|
| 0 | — | — | `CAR-010` **422** |
| ≥1 | ≤2 | — | `CAR-011` **422** |
| ≥1 | ≥3 | yes | `CAR-012` **422** |
| ≥1 | ≥3 | no | `Carryover% ` computed; `Passes = |Carryover%| ≤ AllowableCarryoverPct` |

A **negative** carryover (first low *below* the steady state) is legal and is judged on its absolute
value — the boundary case `Carryover% == −AllowableCarryoverPct` **passes**.

### DT-MV-07 — Outlier flag (`OutlierScreening.cs:183-189`)

| Beyond a Tukey fence | `|modified z| > 3.5` | `MAD == 0` | `IsOutlier` |
|---|---|---|---|
| yes | — | — | **true** |
| no | yes | no | **true** |
| no | — | **yes** | modified z forced to `0` → decided by Tukey alone → **false** |
| no | no | no | false |

Boundary: `|modified z| == 3.5` exactly → **not** an outlier (strict `>`). A value exactly on a Tukey
fence → **not** an outlier (strict `<` / `>` at `:186`).

### DT-MV-08 — Uncertainty target verdict (`UncertaintyBudget.cs:159`)

| `TargetExpandedUncertainty` | `U ≤ target` | `MeetsTarget` |
|---|---|---|
| null | — | **null** |
| set | true (incl. equality) | `true` |
| set | false | `false` |

### DT-MV-09 — Instrument comparability per instrument (`InstrumentComparabilityStudy.cs:168-191`)

| Shared samples with reference (`refValue != 0`) | `|meanBias| ≤ AllowableBiasPct` | `Comparable` | Effect on `Calculate` |
|---|---|---|---|
| 0 | — | `false` (and `MeanBiasPct = 0m`) | **`ICP-012` 422** — `Calculate` refuses the whole study |
| ≥1 | true | `true` | counted comparable |
| ≥1 | false | `false` | increments `NonComparableCount` |

### DT-MV-10 — HTTP status resolution for any MV failure (`DomainExceptionHandler.cs:26-82`)

| Exception / condition | Order evaluated | HTTP | `code` extension |
|---|---|---|---|
| Missing `X-Change-Reason` on DELETE | middleware, before routing | **400** | `CHANGE-REASON-REQUIRED` |
| Unauthenticated | `[Authorize]` | **401** | framework problem |
| Lacks `analytical-quality.<action>` | `RequirePermissionAttribute` filter | **403** | `AUTHZ-403` |
| FluentValidation failure | 2nd | **400** | *(no `code`; `errors` dictionary instead)* |
| `DbUpdateConcurrencyException` | 1st | **409** | `CONCURRENCY-409` |
| `InvalidStateTransitionException` | 3rd | **409** | the domain code |
| `DomainException` with code starting `AUTH-` | 4th | **401** | e.g. `AUTH-003` |
| `DomainException` with code starting `AUTHZ-` | 5th | **403** | e.g. `AUTHZ-002` |
| `DomainException` with code ending `-404` | 6th | **404** | e.g. `PR-404` |
| any other `DomainException` | 7th | **422** | e.g. `SOD-AQ-001`, `LIN-002` |
| anything else (`ArgumentException`, `PostgresException`, …) | unmatched | **500** | none |

The last row is load-bearing for GAP-MV-016 and GAP-MV-017.

---

## 6. UAT scenarios (Gherkin)

Business-readable, written for a laboratory quality manager to sign off. `Result` for all: **Not Run**.

```gherkin
Feature: Verifying a method before it is used on patient samples

  Background:
    Given I am signed in to the workspace "demo-lab"
    And my role grants "analytical-quality.create" and "analytical-quality.sign"

  # TC-MV-UAT-001  [IV]
  Scenario: Imprecision study is accepted against the manufacturer's claim
    Given I create an imprecision study for "Glucose" at level "Level 2"
      And I declare a manufacturer within-run claim of 2.0 % and a within-lab claim of 3.0 %
     When I enter 5 runs of 4 replicates each
      And I ask the system to calculate
     Then the study reports a repeatability CV, a between-run CV and a within-laboratory CV
      And each CV is judged against its declared claim as met or not met
      And the study moves to "Calculated" so that it can be signed

  # TC-MV-UAT-002  [IV]
  Scenario: Adding one more replicate invalidates a calculated study
    Given an imprecision study in state "Calculated" with published CVs
     When a colleague enters one further replicate
     Then all calculated statistics are cleared
      And the study returns to "Data entry"
      And no sign-off is possible until the study is recalculated

  # TC-MV-UAT-003  [IV]
  Scenario: The person who ran the study cannot approve it
    Given I created a linearity study and calculated it myself
     When I attempt to sign it off
     Then the system refuses with a segregation-of-duties message
      And the study stays in "Calculated"
      And a second, different authorised person is able to sign it off

  # TC-MV-UAT-004  [IV]
  Scenario: A signed study is evidence and can no longer be changed
    Given a carryover study that has been signed off
     When anyone attempts to add a reading, delete a reading, recalculate or re-sign it
     Then every attempt is refused as "a signed-off study is immutable"
      And a direct database edit of the study row is also rejected by the database itself

  # TC-MV-UAT-005  [IV]
  Scenario: Verifying a manufacturer's reference interval on 20 subjects
    Given I record a claimed interval of 3.5 to 5.5 for "Adult female", sourced from the kit insert
     When I enter 20 reference-individual results of which 2 fall outside the interval
     Then the system reports 2 outside against an allowance of 2
      And the verdict is "Verified"
     When a 21st result outside the interval is added and the study recalculated
     Then the verdict becomes "Rejected" and the interval must not be transferred

  # TC-MV-UAT-006  [IV]
  Scenario: Establishing the reportable range from a dilution series
    Given a linearity study with an allowable deviation of 10 %
      And 7 dilution levels of which the top level is markedly non-linear
     When I calculate the study
     Then the study is reported as not linear across the full range
      And the verified analytical measurement range is reported as the widest run of
          contiguous levels that passes when re-fitted on itself

  # TC-MV-UAT-007  [IV]
  Scenario: Comparing a candidate analyser against the reference method
    Given a method-comparison study with 45 paired patient results
     When I calculate the study
     Then I am shown a Deming slope and intercept, a Passing-Bablok slope and intercept,
          a Pearson correlation, and a mean bias with 95 % limits of agreement
      And the study is flagged as meeting the recommended sample count
     When the same study is re-created with only 12 pairs
     Then the calculation still succeeds but the study is flagged as not meeting the
          recommended sample count

  # TC-MV-UAT-008  [IV]
  Scenario: Approving a measurement-uncertainty budget
    Given an uncertainty budget for "Creatinine" with a coverage factor of 2 and a target of 8 %
     When I add a 3.0 % QC repeatability component and a 2.0 % calibrator component and calculate
     Then the combined standard uncertainty is reported as the root sum of squares
      And the expanded uncertainty is reported as twice that value
      And the budget is judged against the 8 % target
     When a colleague with approval rights approves the budget
     Then the budget becomes "Approved" and can no longer be edited
      And revising it requires creating a successor budget
```

---

## 7. Exploratory charters

Time-boxed, session-based. Each charter records observations, not pass/fail. `Result`: **Not Run**.

**TC-MV-EXPL-001 — The invalidation cascade** *(90 min)*
Explore **the eleven `Invalidate()` implementations** with **rapid interleaved add/remove/calculate
sequences and concurrent sessions** to discover **whether a stale statistic can ever be observed
alongside a `Calculated` state, and whether the silent `Calculated → DataEntry` demotion is ever
lost under `xmin` conflict (`CONCURRENCY-409`)**.
Oracles: `GET /{id}` after every mutation; `SELECT state, <derived cols> FROM qams.<table>`;
`audit.field_change` rows for the state property.

**TC-MV-EXPL-002 — Numeric extremes in the statistical kernels** *(120 min)*
Explore **`Calculate` on all thirteen aggregates** with **decimal values at the edges of `decimal`
and `double` (1e-28, 1e28, values differing only past the 4th decimal, all-identical replicates,
alternating high/low series, strongly negatively correlated pairs)** to discover **unhandled
`OverflowException`, `DivideByZeroException`, `IndexOutOfRangeException`, `NaN`/`Infinity` written to
a `numeric` column, and any 500 where a typed 422 is expected**.
Specific hypotheses to probe: the Passing–Bablok shift index (GAP-MV-015); `(decimal)Math.Sqrt(...)`
narrowing in `DetectionLimitStudy.Sqrt` (`:251`); `Math.Round((decimal)value, 4)` on a `double` that
exceeds `decimal` range in every `Round` helper.

**TC-MV-EXPL-003 — Untyped string and duplicate-key inputs** *(75 min)*
Explore **the `Kind` string parameters and the sample/instrument identifiers** with **unknown enum
names, mixed case, whitespace, unicode, empty strings, and duplicate `SampleId` on the reference
instrument** to discover **which inputs escape validation and surface as HTTP 500 rather than 400/422**.
Entry points: `POST /api/carryover-studies/{id}/readings` (`Enum.Parse<CarryoverSampleKind>`,
`CarryoverSlice.cs:56`), `POST /api/detection-limit-studies/{id}/measurements`
(`Enum.Parse<DetectionSampleKind>`, `DetectionLimitSlice.cs:64`),
`POST /api/instrument-comparabilities/{id}/readings` then `/calculate`
(`ToDictionary` on `SampleId`, `InstrumentComparabilityStudy.cs:125`),
`POST /api/interference-studies/{id}/measurements` with `Kind` neither `Control` nor anything else
(`InterferenceSlice.cs:57-59`).

**TC-MV-EXPL-004 — The ungated write surface** *(90 min)*
Explore **the 35 MV write actions that carry no `[RequirePermission]`** as **an `Analyst`-seeded
account holding only `analytical-quality.view` and `analytical-quality.export`** to discover **how
much analytical evidence such an actor can create, alter and destroy, and what the audit trail
records about it**.
Oracles: HTTP status per action; `audit.field_change` actor and reason columns;
`SystemRoleCatalog.cs:173` for the seeded grant.

**TC-MV-EXPL-005 — Child-row immutability under a signed parent** *(60 min, requires psql)*
Explore **the twelve child evidence tables** with **direct `UPDATE`/`DELETE` against rows whose parent
study is `SignedOff`/`Approved`** to discover **exactly which mutations the database permits, whether
`audit.field_change` sees them, and whether the parent's reported statistics then disagree with its
own evidence**.
Run inside a rolled-back transaction, following the pattern in
`tests/NT.QAMS.IntegrationTests/SignedRecordImmutabilityTests.cs`. Feeds GAP-MV-006.

---

## 8. Gap Register (this module)

---

### GAP-MV-001 — `SIG-*` error codes are double-booked across Six-Sigma and electronic signature

- **Source reference:** `src/NT.QAMS.Domain/AnalyticalQuality/SigmaAssessment.cs:53,72,77,82,101`; `src/NT.QAMS.Application/AnalyticalQuality/SigmaAssessmentSlice.cs:73,112`; `src/NT.QAMS.Infrastructure/Compliance/ComplianceLedgerServices.cs:94,101,108,114`; conventions §2 line 54.
- **Description:** `SIG-001`, `SIG-002`, `SIG-003` and `SIG-404` each carry two unrelated meanings — a Six-Sigma input-validation failure and an electronic-signature credential failure. `SIG-010`/`SIG-011` are Six-Sigma only, despite the ground truth listing them under electronic signature.
- **Impact:** A client, a support engineer or a Part-11 reviewer cannot determine from the code alone what refused a request. Error-code-driven UI branching and log alerting are unsafe. Violates the `CLAUDE.md` §2 rule "error codes are structured".
- **Testing limitation:** Any case asserting a `SIG-*` code must additionally assert the endpoint, otherwise the assertion is ambiguous. No test can distinguish the two families by code alone.
- **Recommended clarification:** Product owner decides which subsystem keeps the `SIG-` prefix. Suggested: rename the Six-Sigma family to `SGM-001/002/003/010/011/404` and reserve `SIG-` for electronic signature; amend conventions §2 line 54.
- **Suggested acceptance criteria:** Every `SIG-*` string in `src/` maps to exactly one subsystem; an architecture test asserts prefix uniqueness across the `Domain`, `Application` and `Infrastructure` assemblies; conventions §2 lists the corrected sets.
- **Severity:** Major
- **Responsible role:** Solution Architect (code owner) + Test Package Owner (conventions amendment)

---

### GAP-MV-002 — No notification is raised on any analytical study sign-off

- **Source reference:** `src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:24-33` (ten handled event types, none from MV); 13 MV events listed in §1.5.
- **Description:** All thirteen sign-off/approval events are raised and drained to the outbox, but no `INotificationHandler<DomainEventNotification<T>>` exists for any of them. Nothing informs the quality manager, the method owner or the department head that a method has been released.
- **Impact:** A signed method-validation study is a release decision. With no notification, downstream authorisation (test authorisation, competency, QC target setting) is coordinated out-of-band. URS-038 says "with calculation and electronic sign-off" but is silent on notification, so this is a requirements gap as much as an implementation gap.
- **Testing limitation:** The **Expected Notification** field of every MV sign-off case must read `n/a` with the citation. No positive notification case can be authored. This makes ~40 planned cases weaker than their equivalents in modules NCR and DOC.
- **Recommended clarification:** Product owner states whether analytical sign-off must notify, and whom (method owner? QM? department head?).
- **Suggested acceptance criteria:** If required — a new URS requirement; a `NotificationPolicies` handler per MV event; a `notification_dispatch` row asserted in an integration test. If not required — an explicit "no notification by design" note added to URS-038.
- **Severity:** Medium
- **Responsible role:** Product Owner (Quality)

---

### GAP-MV-003 — `analytical-quality.view`, `.export` and `.void` are declared but gate nothing

- **Source reference:** `src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:171-173` declares eight actions; a repo-wide grep of `PermissionCatalog.AnalyticalQuality` finds attribute usage only for `Create` (13), `Sign` (12), `Edit` (4), `Manage` (2), `Approve` (1).
- **Description:** Three of the eight published privileges are inert. All 26 MV `GET` endpoints are protected by `[Authorize]` alone, so any authenticated tenant user reads every study regardless of `analytical-quality.view`. There is no export endpoint in the module at all, and no void operation.
- **Impact:** The privilege matrix presented to a laboratory administrator promises read segregation the system does not enforce. An `ExternalAuditor` and any tenant user can read all analytical evidence. Because a tenant administrator can *revoke* `analytical-quality.view` and observe no change, the matrix is misleading.
- **Testing limitation:** No negative case can be written for `analytical-quality.view`; a case that revokes it and expects 403 would fail and would be recorded as a defect rather than as designed behaviour. Cases must be `[GD]` until resolved.
- **Recommended clarification:** Decide per action: (a) add `[RequirePermission(AnalyticalQuality, View)]` to the 26 GETs, (b) remove `View`/`Export`/`Void` from the catalogue entry, or (c) implement the missing export/void surface.
- **Suggested acceptance criteria:** Every action declared in `PermissionCatalog.Modules` for `analytical-quality` is referenced by at least one attribute, asserted by an architecture test; or the declaration is trimmed to what exists.
- **Severity:** Major
- **Responsible role:** Solution Architect + Product Owner (Quality)

---

### GAP-MV-004 — Measurement entry, bulk import, calculation and evidence deletion are not permission-gated on eleven of twelve studies

- **Source reference:** `src/NT.QAMS.WebApi/Controllers/LinearityStudiesController.cs:34,42,49` (no `[RequirePermission]`), and the identical shape in `Precision`, `MethodComparison`, `DetectionLimit`, `ReferenceInterval`, `Carryover`, `Interference`, `LotComparison`, `InstrumentComparability`, `OutlierScreening` and `ValidationStudies` controllers. Contrast `src/NT.QAMS.WebApi/Controllers/UncertaintyController.cs:36,42,50,58`, which does gate them.
- **Description:** 35 of the 84 MV write actions carry only `[Authorize]` at the HTTP layer and `[RequireInternalActor]` at the command layer. The effective audience is "any authenticated user in the tenant who is not an `ExternalAuditor`" — including the seeded `Analyst`, whose only analytical grants are `View` and `Export` (`SystemRoleCatalog.cs:173`).
- **Impact:** A user explicitly denied `analytical-quality.create` and `analytical-quality.edit` can still add replicates to someone else's study, bulk-import 10 000 rows, recalculate the statistics and delete evidence rows. Only the final sign-off is properly gated. This undercuts URS-039 (separation of duties) in spirit even though the signer≠preparer check still holds.
- **Testing limitation:** Cases exercising these endpoints cannot assert a meaningful `Required Permission`; the field must read `n/a — endpoint carries no [RequirePermission]; internal-actor policy only` with the citation. The Role & Permission Matrix for module MV will show a large ungated block.
- **Recommended clarification:** Confirm the intended audience for data entry versus sign-off. The uncertainty-budget controller is the reference implementation.
- **Suggested acceptance criteria:** Every MV write action carries a `[RequirePermission(AnalyticalQuality, <action>)]` consistent with the uncertainty controller (`Edit` for entry/import/calculate/delete, `Create` for create, `Sign`/`Approve` for the terminal transition); `RoleEndpointMatrixTests` is extended to assert 403 for an `Analyst`-seeded account on each.
- **Severity:** Critical
- **Responsible role:** Solution Architect

---

### GAP-MV-005 — No `HasPrecision` on any derived decimal column in the twelve MV study tables

- **Source reference:** `src/NT.QAMS.Infrastructure/Persistence/Configurations/AnalyticalQualityConfigurations.cs` — `HasPrecision` appears only at `:16-17` (`qc_profile`), `:30-31` (`qc_run`), `:51-53` (`validation_study`) and `:85-88` (`pt_enrollment`). No study configuration from `:158` onward declares it.
- **Description:** Slope, intercept, `PearsonR`, `PassingBablokSlope/Intercept`, `MeanBias`, `BiasSd`, the limits of agreement, `Lob`, `Lod`, `Loq`, `BlankMean/Sd`, `PooledLowSd`, `CarryoverPct`, `MeanBiasPct`, `SigmaValue`, `CombinedStandardUncertainty`, `ExpandedUncertainty`, the Tukey fences and the study criteria (`AllowableDeviationPct`, `LoqCvTargetPct`, `AllowableBiasPct`, `AllowableCarryoverPct`, `ClaimedRepeatabilityCvPct`, …) are all mapped to unconstrained PostgreSQL `numeric`.
- **Impact:** Low today — the domain rounds every derived value to 2–4 decimal places before assignment — but there is no schema-level guarantee, so a future code path or a raw insert could store arbitrary precision, and index/statistics behaviour is unbounded. Diverges from the pattern the same file applies to `validation_study` and the QC tables.
- **Testing limitation:** A `TC-MV-STAT-*` case cannot assert a column's declared scale. Precision assertions must be made against the API response value, not the column definition.
- **Recommended clarification:** Confirm the intended scale per class of value (statistics 4 dp, percentages 3 dp, criteria 2 dp are the values the domain already uses).
- **Suggested acceptance criteria:** Every decimal property on the twelve MV aggregates declares `HasPrecision(p, s)`; a migration applies the types; `CheckConstraintTests` or an equivalent asserts `numeric_scale` from `information_schema.columns`.
- **Severity:** Minor
- **Responsible role:** Database Architect

---

### GAP-MV-006 — The signed-record immutability trigger protects study roots but not their evidence rows

- **Source reference:** `src/NT.QAMS.Infrastructure/Persistence/Migrations/20260726084134_SignedRecordImmutability.cs:14-29` (13 tables, all roots) and `:56-64` (trigger creation loop). A grep for `frozen_immutability` across all 57 migrations returns only this file.
- **Description:** `precision_measurement`, `measurement_pair`, `linearity_measurement`, `detection_measurement`, `reference_sample`, `carryover_reading`, `interference_measurement`, `lot_sample_pair`, `instrument_reading`, `outlier_point`, `uncertainty_component` and `validation_replicate` carry no `frozen_immutability` trigger. A raw `UPDATE`/`DELETE` against a child row succeeds while its parent study is `SignedOff`/`Approved`.
- **Impact:** URS-041 requires that "once an analytical study is signed off … the record shall be immutable to further edit/deletion **at the database layer**". The *record* is the study plus its evidence. Today a privileged database actor can silently alter the replicates a signed conclusion rests on, leaving the frozen root reporting statistics its own data no longer supports. Part 11 §11.10(c)/(e). Mitigating factors: the domain guard blocks every application path, and the runtime role holds no DELETE grant (see `deploy/harden-runtime-role.sql`) — so this is defence-in-depth, not an open API hole.
- **Testing limitation:** `TC-MV-INT-*` cases for child immutability must be authored as **currently-failing expectations** or omitted. Per the honesty rules, they are labelled `[GD]` on this gap and are **not** written as executable positive cases until it is closed.
- **Recommended clarification:** Confirm whether URS-041's "the record" includes owned children. Trigger design option: a `BEFORE INSERT OR UPDATE OR DELETE` trigger on each child that joins to the parent's frozen column.
- **Suggested acceptance criteria:** A migration adds a child-level guard for all twelve child tables; `SignedRecordImmutabilityTests` gains a case per child table proving SQLSTATE `23514` on `UPDATE` and on `DELETE` while the parent is frozen; the legitimate insert path during data entry remains unaffected.
- **Severity:** Major
- **Responsible role:** Database Architect + Validation Lead

---

### GAP-MV-007 — A trigger rejection reaching EF returns HTTP 500, not problem+json

- **Source reference:** `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:26-82` — the switch matches `DbUpdateConcurrencyException`, `ValidationException`, `InvalidStateTransitionException` and `DomainException`; `_ => null` at `:81` lets everything else fall through. `Npgsql.PostgresException` (SQLSTATE `23514`) arrives wrapped in `DbUpdateException`, which matches nothing.
- **Description:** If any application path ever reaches the `frozen_immutability` trigger — a future bulk operation, a repair script run through EF, a race between two sign-offs — the caller receives an untyped 500 rather than a typed refusal.
- **Impact:** Latent. No current API path reaches it because the in-memory `RequireEditable()` guard fires first. But the 500 would carry a raw PostgreSQL message into the response pipeline and would not be distinguishable by the SPA from an outage.
- **Testing limitation:** Cannot be exercised through the API today. A case can only be written at the integration layer against a deliberately-bypassed domain guard, which is not a supported test seam.
- **Recommended clarification:** Whether `DbUpdateException` wrapping SQLSTATE `23514` should map to `409` with a stable code (e.g. `IMMUTABLE-409`).
- **Suggested acceptance criteria:** `DomainExceptionHandler` gains a branch mapping SQLSTATE `23514` from `qams.reject_frozen_mutation` to `409` with a documented code; a functional test asserts the mapping.
- **Severity:** Minor
- **Responsible role:** Solution Architect

---

### GAP-MV-008 — Analytical sign-off is not an electronic signature

- **Source reference:** `IESignatureService` is declared at `src/NT.QAMS.Application/Abstractions/ComplianceAndAuthPorts.cs:25` and consumed in `src/` only by `src/NT.QAMS.Application/DocumentControl/Commands/DocumentCommands.cs:122`. Every MV sign-off handler (e.g. `src/NT.QAMS.Application/AnalyticalQuality/PrecisionSlice.cs:82-89`) calls only `study.SignOff(actor, clock.UtcNow)`.
- **Description:** MV "sign-off" is a permission-gated POST with no body. It does not re-authenticate the signer, does not require the signature PIN, and writes **no `SignatureRecord`** row. The study stores `SignedOffBy` and `SignedOffAtUtc` only.
- **Impact:** URS-038 promises "electronic sign-off" and the FRA rates analytical sign-off HIGH. 21 CFR Part 11 §11.200(a)(1) requires two distinct identification components for a signing that is not part of a continuous session, and §11.50 requires the signature manifestation to record the signer, the date/time and the **meaning** of the signature. None of the three is captured for an MV sign-off, and `GET /api/compliance/signatures` will never list one. Compliance verdict for this control: **Does not conform**.
- **Testing limitation:** No `TC-MV-SEC-*` case can assert a `qams.signature_record` row for an MV sign-off; the **Expected Audit** field must cite only the `audit.field_change` rows for `state`, `signed_off_by`, `signed_off_at_utc`. Any case reusing the document-approval e-signature pattern would be fabricating behaviour.
- **Recommended clarification:** Product owner and QA confirm whether analytical sign-off is intended to be a Part-11 electronic signature (as document approval is) or an internal workflow transition. If the former, the meaning string must also be defined ("Reviewed and approved", "Method verified", …).
- **Suggested acceptance criteria:** MV sign-off accepts password + PIN, calls `IESignatureService.SignAsync` with a defined `Meaning` and a `SubjectRef` of the study, persists a `SignatureRecord` bound to the study, and the signature appears in the signature manifest; failures surface as `SIG-001`/`SIG-002`/`SIG-003` and are logged as `ESIGN_FAILED`.
- **Severity:** Critical
- **Responsible role:** Compliance Lead + Product Owner (Quality)

---

### GAP-MV-009 — State-transition refusals return 409, not the 422 the conventions describe

- **Source reference:** `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:45-50` maps `InvalidStateTransitionException` to `Status409Conflict`; conventions §2 "Segregation of duties" states "Domain rule breaches surface as HTTP 422 with the domain code".
- **Description:** Twenty-five MV codes are raised as `InvalidStateTransitionException` and therefore return **409**: `PR-012/013`, `MC-012/013`, `LIN-012/013`, `DL-013/014`, `RI-011/012`, `SIG-010/011`, `CAR-013/014`, `INT-013/014`, `LOT-012/013`, `ICP-013/014`, `OUT-011/012`, `MU-010/011`, `MV-010/012/015`. Only the plain `DomainException` codes return 422.
- **Impact:** Documentation-versus-behaviour divergence, not a defect. But `409` is also the concurrency status (`CONCURRENCY-409`), so a client distinguishing "someone else changed this, reload" from "this study is frozen" must read the `code` extension, which is easy to get wrong.
- **Testing limitation:** None once documented — this file records the correct mapping in DT-MV-10. Case authors must use 409 for these 25 codes, and must not copy a 422 expectation from a sibling module.
- **Recommended clarification:** Amend conventions §2 to distinguish `DomainException` (422) from `InvalidStateTransitionException` (409), or decide that frozen-record refusals should be 422 and change the handler.
- **Suggested acceptance criteria:** The conventions file states both mappings; `ProblemContractTests` asserts one representative code from each family.
- **Severity:** Minor
- **Responsible role:** Test Package Owner

---

### GAP-MV-010 — The sigma QC recommendation prescribes Westgard rules the QC engine does not implement

- **Source reference:** `src/NT.QAMS.Domain/AnalyticalQuality/SigmaAssessment.cs:118-119` recommends `4:1s` (Good band) and `4:1s / 8:x` (Marginal band). Conventions §2 "QC / Westgard": the evaluator implements rejection rules **1-3s, 2-2s, R-4s, 10-x** and warning **1-2s**; "**`4-1s` IS NOT IMPLEMENTED**". No `8:x` rule exists either (the run-length rule is `10-x`, configured by `WestgardLimits.RunLength = 10`).
- **Description:** A laboratory acting on the sigma module's QC-design advice would configure rules the QC module cannot evaluate. The advice text is also stylistically divorced from the evaluator's labels, which are derived from the configured limits.
- **Impact:** Cross-module inconsistency in a regulated recommendation. A method assessed at 3.5σ is told to run maximum multirule QC including two rules that will never fire.
- **Testing limitation:** A `TC-MV-DT-*` case can assert the exact recommendation string (it is deterministic), but cannot assert that following it produces the intended QC behaviour. The linkage is untestable end-to-end.
- **Recommended clarification:** Either implement `4-1s` and an `8-x` run-length option in `WestgardEvaluator` (making the run length configurable to 8 already exists via `WestgardLimits.RunLength`), or rewrite the recommendation strings to name only implemented rules.
- **Suggested acceptance criteria:** Every rule label appearing in `SigmaAssessment.QcRecommendation` is producible by `WestgardEvaluator` under some valid `WestgardLimits`; a unit test asserts the intersection is complete.
- **Severity:** Medium
- **Responsible role:** Product Owner (Quality) + Solution Architect

---

### GAP-MV-011 — Bulk import exists for only two of the thirteen studies

- **Source reference:** `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt:340,372` — the only `/import` routes are `POST /api/method-comparisons/{id}/pairs/import` and `POST /api/precision-studies/{id}/measurements/import`. Handlers at `MethodComparisonSlice.cs:106` and `PrecisionSlice.cs:105`.
- **Description:** Linearity, detection limit, reference interval, carryover, interference, lot comparison, instrument comparability, outlier screening, uncertainty and validation studies have no bulk path. A reference-interval study needs at least 20 individual POSTs; a detection-limit study needs at least 20.
- **Impact:** Usability and data-integrity risk — 20+ manual transcriptions per study is exactly the failure mode ALCOA+ transcription controls exist to prevent. No URS requirement covers bulk import at all, so this is also a requirements gap.
- **Testing limitation:** `TC-MV-API-*` cases for the ten studies without import must build data one row at a time, making them slow and making a "partial import with integrity report" case impossible for them.
- **Recommended clarification:** Product owner states whether bulk import is expected for all study types.
- **Suggested acceptance criteria:** If required — a URS requirement is added; each study exposes `/import` returning `BulkImportResultDto`; the partial-import semantics (bad row reported and skipped, batch commits once) match the two existing handlers; `BulkImportTests` covers each.
- **Severity:** Medium
- **Responsible role:** Product Owner (Quality)

---

### GAP-MV-012 — "CSV / LIS import" is a client-side CSV parse, not a server capability

- **Source reference:** `src/NT.QAMS.Contracts/AnalyticalQuality/AnalyticalQualityContracts.cs:42` comment "Bulk import (LIS / analyzer CSV)"; the request types at `:46-50` are typed JSON row collections. CSV parsing is `frontend/src/app/shared/ui/csv-import.component.ts`, wired at `frontend/src/app/features/analytical/method-comparison-detail.component.ts:142` and `precision-detail.component.ts:115`.
- **Description:** No CSV media type, no file upload, no `IFormFile`, no HL7/ASTM parser and no LIS connector exists in `src/`. The server only ever sees parsed JSON rows.
- **Impact:** Any validation, plan or SOP that claims "the system imports analyser CSV / LIS files" describes the browser, not the system. A headless or integration client gains nothing from the `/import` endpoints beyond batching. The **audit trail records only the parsed rows** — the original CSV file is never retained, so the source record for a bulk-imported study does not exist in the system (ALCOA+ "Original").
- **Testing limitation:** A server-side CSV case (malformed delimiter, BOM, wrong column order, quoted values) cannot be authored against the API at all; those are `TC-MV-COMP-*` frontend cases. Server-side cases can only cover the JSON contract.
- **Recommended clarification:** Confirm whether the imported source file must be retained as a record, and whether a server-side CSV/LIS ingestion path is required.
- **Suggested acceptance criteria:** Either the contract comments are corrected to "JSON row batch (client parses CSV)", or a server-side upload endpoint is added that stores the source file against the study through the existing file-hardening path and records its hash.
- **Severity:** Medium
- **Responsible role:** Product Owner (Quality) + Compliance Lead

---

### GAP-MV-013 — `ValidationStudy.Protocol` is unvalidated free text

- **Source reference:** `src/NT.QAMS.Domain/AnalyticalQuality/ValidationStudy.cs:60` (only a non-blank check), `:72` (`protocol.Trim().ToUpperInvariant()`); `src/NT.QAMS.Infrastructure/Persistence/Configurations/AnalyticalQualityConfigurations.cs:49` (`varchar(30)`); `src/NT.QAMS.Application/AnalyticalQuality/ValidationAndPtSlice.cs` has no `RuleFor(x => x.Protocol)` beyond what the domain enforces.
- **Description:** Any 30-character string is a valid "CLSI protocol". The Angular placeholder "e.g. CLSI EP15-A3" (`frontend/src/app/core/i18n.service.ts:261`) is a hint only. Typos, obsolete editions and free prose are all accepted and are then upper-cased.
- **Impact:** The protocol field cannot be used for filtering, reporting or accreditation evidence. `MV-001` says "Analyte and **CLSI protocol** are required", implying a controlled value the system does not control.
- **Testing limitation:** No equivalence-partition or decision-table case can be written over a protocol catalogue, because none exists. `TC-MV-EP-*` cases are limited to blank/whitespace/length boundaries.
- **Recommended clarification:** Whether protocols should come from the list-of-values (LOV) infrastructure that already exists in this build.
- **Suggested acceptance criteria:** `Protocol` is bound to a seeded LOV; an unknown value is refused with a typed code; the seeded set is documented.
- **Severity:** Minor
- **Responsible role:** Product Owner (Quality)

---

### GAP-MV-014 — CLSI protocol identity is documentation-only and is never persisted or returned

- **Source reference:** All CLSI strings in the module are XML doc comments, contract section comments, controller `<summary>` text, or Angular help/i18n dictionaries — enumerated in §3.1 "Where the CLSI string actually lives". No column, enum, validator or response DTO carries one for the twelve MV studies.
- **Description:** The system implements EP05/EP06/EP07/EP09/EP10/EP17/EP28-shaped calculations but never records which guideline (or which edition) a given study claims to follow.
- **Impact:** For ISO 17025 §7.2.2 and ISO 15189 §7.3 evidence, the study record cannot state the protocol it was run under. Guideline editions change (`LinearityStudy.cs:30` already hedges "EP06 2nd-edition **style**"; `CarryoverStudy.cs:29` hedges "EP10-**style**"); nothing in the record captures which behaviour was in force at sign-off.
- **Testing limitation:** No case may assert that an API response identifies a CLSI protocol. Every study-inventory assertion must be against the *behaviour* (the algorithm, the minimums), never against a protocol label.
- **Recommended clarification:** Whether each study type should persist a protocol/edition reference at creation, and whether the hedged "style" claims should be stated in the validation summary report.
- **Suggested acceptance criteria:** Each study root gains a `Protocol` (or `GuidelineRef`) column populated from a controlled list at creation and returned in the detail DTO; the validation summary report states, per study type, which guideline and edition the implementation approximates and where it deviates.
- **Severity:** Medium
- **Responsible role:** Compliance Lead + Product Owner (Quality)

---

### GAP-MV-015 — Passing–Bablok shift index is unguarded and can throw `IndexOutOfRangeException`

- **Source reference:** `src/NT.QAMS.Domain/AnalyticalQuality/MethodComparisonStudy.cs:232-243`. With `count = slopes.Count` and `k = slopes.Count(s => s < -1.0)`, the odd branch indexes `slopes[(count - 1) / 2 + k]` and the even branch `slopes[count / 2 + k]`. Neither index is bounded by `count - 1`.
- **Description:** When more than half the pairwise slopes are below −1 (strongly negatively associated data), the shifted index exceeds the array bound and the aggregate throws `IndexOutOfRangeException`. That exception is not a `DomainException`, so `DomainExceptionHandler.cs:81` returns `null` and the caller receives **HTTP 500**.
- **Impact:** `POST /api/method-comparisons/{id}/calculate` can 500 on legitimate (if clinically unusual) data. The study is left in whatever state it was in, and the operator has no typed error to act on. Note the standard Passing–Bablok offset `K` is normally the count of slopes **strictly less than −1** *and* the method is only defined for positively-associated methods, so the mathematical soundness of the result in this regime is also questionable.
- **Testing limitation:** A `TC-MV-STAT-*` case that constructs such a dataset would record a 500 — a defect, not a specification. Until resolved, such cases are `[GD]` on this gap; the boundary must not be authored as an executable positive case.
- **Recommended clarification:** Confirm the intended behaviour for a strongly negative association: refuse with a typed code (e.g. a new `MC-014` "the methods are inversely associated; Passing–Bablok is not applicable"), or clamp the index.
- **Suggested acceptance criteria:** `Calculate` never throws an unhandled exception for any finite input; a domain unit test covers `k > count/2`; the refusal (if chosen) is a documented `MC-*` code returning 422.
- **Severity:** Major
- **Responsible role:** Solution Architect

---

### GAP-MV-016 — Unvalidated `Enum.Parse` on `Kind` strings returns HTTP 500

- **Source reference:** `src/NT.QAMS.Application/AnalyticalQuality/CarryoverSlice.cs:56` — `Enum.Parse<CarryoverSampleKind>(c.Kind, true)`; `src/NT.QAMS.Application/AnalyticalQuality/DetectionLimitSlice.cs:64` — `Enum.Parse<DetectionSampleKind>(c.Kind, ignoreCase: true)`. Neither `AddCarryoverReadingCommand` nor `AddDetectionMeasurementCommand` has an `AbstractValidator` (no `RuleFor(x => x.Kind)` exists in either slice).
- **Description:** `POST /api/carryover-studies/{id}/readings` with `{"kind":"Medium",...}` and `POST /api/detection-limit-studies/{id}/measurements` with `{"kind":"Spiked",...}` throw `ArgumentException` inside the handler. `DomainExceptionHandler` does not match it, so the response is **500** rather than 400/422.
- **Impact:** A client typo produces an unhandled server error and a logged stack trace instead of a typed refusal. Only two of the enum-carrying endpoints are affected; `InterferenceSlice.cs:57-59` uses a string comparison with an `AddTest` fallback and therefore degrades to `INT-003` (422) instead — inconsistent handling of the same class of input.
- **Testing limitation:** `TC-MV-EP-*` cases for invalid `Kind` values on these two endpoints must be authored as `[GD]`; a 400/422 expectation would fail today.
- **Recommended clarification:** Whether `Kind` should be a validated string (FluentValidation `IsEnumName`) or a typed enum on the contract.
- **Suggested acceptance criteria:** An unknown `Kind` returns 400 with the FluentValidation `errors` dictionary (or 422 with a typed code) on both endpoints; a functional test covers each; `InterferenceSlice`'s fallback is aligned to the same behaviour.
- **Severity:** Medium
- **Responsible role:** Solution Architect

---

### GAP-MV-017 — Duplicate reference-instrument `SampleId` throws inside `Calculate`

- **Source reference:** `src/NT.QAMS.Domain/AnalyticalQuality/InstrumentComparabilityStudy.cs:123-125` builds `reference = _readings.Where(reference instrument).ToDictionary(r => r.SampleId, r => r.Value, StringComparer.OrdinalIgnoreCase)`. `AddReading` (`:97-109`) enforces only that `instrument` and `sampleId` are non-blank — nothing prevents two readings of the same sample on the reference instrument.
- **Description:** Recording the reference instrument twice for one sample (a replicate, or a re-run) makes `ToDictionary` throw `ArgumentException: An item with the same key has already been added`. Unmatched by `DomainExceptionHandler` → **HTTP 500**. Note `Results()` (`:162-165`) builds the same dictionary, so `GET /{id}` on such a study also 500s once the study leaves `DataEntry`.
- **Impact:** A realistic data-entry pattern (replicate measurements on the reference analyser) bricks the study: it cannot be calculated and, after any prior calculation, cannot be read. There is no domain code for it and no recovery other than deleting the duplicate reading.
- **Testing limitation:** The duplicate-sample case must be `[GD]`. It also constrains the happy-path `TC-MV-API-*` fixtures for this study: they must use one reading per instrument per sample.
- **Recommended clarification:** Whether replicates on the reference instrument should be averaged, rejected at `AddReading` with a typed code, or rejected at `Calculate`.
- **Suggested acceptance criteria:** `AddReading` or `Calculate` refuses (or averages) duplicate `(Instrument, SampleId)` pairs with a documented `ICP-*` code returning 422; `Results()` never throws; a domain unit test and a functional test cover it.
- **Severity:** Major
- **Responsible role:** Solution Architect

---

### GAP-MV-018 — `LOT-011` is unreachable dead code

- **Source reference:** `src/NT.QAMS.Domain/AnalyticalQuality/LotComparisonStudy.cs:133-136` raises `LOT-011` when `meanCurrent == 0m`, but `AddPair` at `:102-105` already rejects `currentLotValue <= 0m` with `LOT-004`, and `Calculate` requires at least three pairs. The mean of three or more strictly-positive decimals cannot be zero.
- **Description:** A declared error code that no input can produce.
- **Impact:** Minor. Violates the `CLAUDE.md` §2 "no dead code" rule and inflates the module's apparent error surface. It also means the error-code catalogue over-states testable behaviour.
- **Testing limitation:** No executable case can cover `LOT-011`. Coverage reporting for this module must state that 1 of 66 domain codes is unreachable, or the code must be removed. This file states it here so the omission is honest rather than silent.
- **Recommended clarification:** Remove the branch, or relax `LOT-004` (if a zero/negative lot value is ever legitimate for a signed-magnitude analyte) so that `LOT-011` becomes reachable.
- **Suggested acceptance criteria:** Either `LOT-011` is deleted and the catalogue updated, or a domain unit test demonstrates an input that reaches it.
- **Severity:** Minor
- **Responsible role:** Solution Architect

---

### GAP-MV-019 — `UncertaintyBudgetApproved` omits `TenantId`

- **Source reference:** `src/NT.QAMS.Domain/AnalyticalQuality/UncertaintyBudget.cs:187-188` — `UncertaintyBudgetApproved(Guid BudgetId, string BudgetRef, string Analyte, decimal ExpandedUncertainty, decimal CoverageFactor)`. Compare all twelve sibling events, each of which ends `…, Guid TenantId`.
- **Description:** The one MV event that does not carry its tenant.
- **Impact:** The outbox row is written under the ambient tenant context, so nothing leaks today. But any future consumer that reads the event payload rather than the outbox row's `tenant_id` column has no tenant to scope on — the exact shape of a cross-tenant defect. It also breaks the uniformity that a generic MV event consumer would rely on.
- **Testing limitation:** A `TC-MV-RLS-*` case asserting tenant propagation through the event payload cannot be written for this one event; it must assert against the outbox row column instead, and must say so.
- **Recommended clarification:** Confirm the intended event contract.
- **Suggested acceptance criteria:** `UncertaintyBudgetApproved` carries `TenantId` as its final positional member, matching its twelve siblings; an architecture test asserts every `AnalyticalQuality` domain event record declares a `TenantId` member.
- **Severity:** Minor
- **Responsible role:** Solution Architect

---

### GAP-MV-020 — `MU-006` returns 422 where every sibling "not found" returns 404

- **Source reference:** `src/NT.QAMS.Domain/AnalyticalQuality/UncertaintyBudget.cs:139` raises `DomainException("MU-006", "Component not found.")`. `DomainExceptionHandler.cs:69` routes on the `-404` **suffix**, which `MU-006` does not have, so it falls through to the 422 branch at `:75`. Every other MV child-not-found uses the `-404` suffix (`PR-404`, `MC-404`, `LIN-404`, `DL-404`, `RI-404`, `CAR-404`, `INT-404`, `LOT-404`, `ICP-404`, `OUT-404`).
- **Description:** `DELETE /api/uncertainty-budgets/{id}/components/{componentId}` with an unknown component id returns **422 `MU-006`**; the same operation on any other study returns **404 `<PREFIX>-404`**.
- **Impact:** Inconsistent REST semantics across one module. A client written against the eleven other studies will mis-handle the uncertainty budget.
- **Testing limitation:** None once documented — case authors must expect 422 here and 404 everywhere else. Recorded so that the inconsistency is not "silently reconciled".
- **Recommended clarification:** Whether to renumber to `MU-404` (which would collide with the existing budget-not-found `MU-404` at `UncertaintySlice.cs:108,148`) or introduce a distinct suffixed code such as `MU-COMPONENT-404`.
- **Suggested acceptance criteria:** Unknown-child deletion returns 404 uniformly across all twelve studies, with a distinct code per entity; `ProblemContractTests` asserts the status for the uncertainty component.
- **Severity:** Minor
- **Responsible role:** Solution Architect

---

### GAP-MV-021 — No requirement covers the derived-statistics invalidation contract

- **Source reference:** The behaviour is implemented uniformly — `PrecisionStudy.cs:218-228` and its eleven twins, plus `ValidationStudy.cs:85-91` and `UncertaintyBudget.cs:128-131,141-144`. `docs/validation/01-User-Requirements-Specification.md:86,87,89` (URS-038/039/041) says nothing about it, and no `URS-056`…`URS-107` row in `docs/validation/06-Revalidation-Delta-v1.38-v1.50.md` Part A covers it.
- **Description:** "Any change to the evidence silently voids the derived statistics and demotes the study out of `Calculated`" is one of the module's most important data-integrity properties, and it is implementation-derived only.
- **Impact:** Roughly a quarter of the planned `TC-MV-DF-*`, `TC-MV-STATE-*` and `TC-MV-UNIT-*` cases have no requirement to trace to, so the traceability matrix for module MV will show them as `[ID]` with a source-file trace. That weakens the RTM for a HIGH-rated risk area (FRA "Method-validation studies", line 64).
- **Testing limitation:** Those cases must carry `Requirement ID = n/a — no URS requirement; traced to <file:line>` and the `[ID]` label, per conventions §4 and §6.
- **Recommended clarification:** Add a URS requirement stating that derived analytical statistics are recomputed-only, are cleared whenever the underlying evidence changes, and that a study demoted out of the calculated state cannot be signed until recalculated.
- **Suggested acceptance criteria:** A new `URS-1xx` row exists in `06-Revalidation-Delta` Part A; the RTM maps the invalidation cases to it; the OQ execution record proves the demotion for at least one study of each of the three state-machine shapes.
- **Severity:** Medium
- **Responsible role:** Validation Lead

---

*End of front matter for module MV. Detailed cases: see `20-module-method-validation-cases-a.md` …
`-e.md`. Nothing in this file records an execution result.*
