# OQ Execution Record — Analytical Quality: Precision EP05/EP15 Gap Closure

| Field | Value |
| ----- | ----- |
| Document ID | OQR-NTQMS-024 (rev 0 — 2026-10-16) |
| Protocol | REVAL-NTQMS-003 (doc 23), Parts B and C |
| Build | branch `feature/aq-precision-ep05-ep15` off `8669ec9`, uncommitted working tree at time of execution |
| Environment | Engineering workstation (Windows Server 2022). **Throwaway** PostgreSQL 17.2 at `127.0.0.1:55432`, database `qams_verify`, non-superuser owner, full migration chain applied from empty. API run from source on `127.0.0.1:5180`, SPA dev server on `localhost:4300`. The production instance (`:5000`) and the test instance (`:5080`) on the same host were **not touched**. |
| Status | **Engineering dry run — NOT a qualified execution.** Unsigned. QA re-executes on a qualified environment. |

**Signature block:**

| Activity | Name | Signature | Date |
| -------- | ---- | --------- | ---- |
| Executed by (Engineering dry run) | | | |
| Reviewed by (QA) | | | |

## 1. Automated evidence engines

| Suite | Result (observed) |
| ----- | ----------------- |
| Domain unit (`NT.QAMS.Domain.UnitTests`) | 504 passed, 0 failed (AQ subset 108 at first run) |
| Application unit | 164 passed, 0 failed |
| Architecture | 202 passed, 0 failed — `CommandsWithoutValidators.approved.txt` shrank by one (`AddPrecisionMeasurementCommand` now has a validator) |
| Integration (real PostgreSQL, throwaway) | 34 passed, 8 skipped (environment/data-conditional), 0 failed — incl. `PrecisionStudyConstraintTests` (2) and `GovernanceTests.The_last_migration_reverts_and_reapplies_cleanly` |
| WebApi functional | 106 passed, **1 failed**: `StartupSeedingResilienceTests.An_unreachable_database_defers_the_seeding_instead_of_throwing` — the test assumes nothing listens on `localhost:5433`; on this host another PostgreSQL instance does, so the probe gets `28P01` instead of a connection failure. Environmental; the test and the seeding code are untouched by this change. `ApiSurface.approved.txt` +6 lines (3 routes × versioned alias), reviewed. |
| Frontend unit (Karma, ChromeHeadless) | 141 of 141 SUCCESS (+8: precision, uncertainty, sigma facade specs) |
| Frontend production build | Clean (one pre-existing NG8113 warning in `sigma-detail.component.ts`, unrelated) |

## 2. IQ-35 — catalogue observations (throwaway PG)

- Migration applied from empty, reverted to `20260829220248_AddEnvironmentOfCareNcSource` (columns
  removed: `day_label` count 0), re-applied; the four constraints present after re-apply.
- RLS-parity query: 0 rows. Identifiers > 62 chars: 0.
- `frozen_immutability`: `validation_study:O, precision_study:O, sigma_assessment:O, uncertainty_budget:O`.

## 3. OQ-AQP observations (live, engineering)

Test tenant `verify-lab` with a preparer (tenant administrator) and a separate QM signer; all
credentials generated for the throwaway environment only.

| OQ | Observed |
| -- | -------- |
| OQ-AQP-02 | API import of a 5-day × 5-replicate Glucose Low set with operator/instrument/lot/date: 25 imported, 0 rejected. Workspace coverage: 5 days, 5 runs, 2 operators, 1 instrument, 2 reagent lots, 1 calibrator lot, Oct 1 – Oct 5 2026, "Meets the EP15 minimum". |
| OQ-AQP-03 | Calculate (UI): design "Days, one run each (EP15)"; grand mean 5.5136; repeatability CV 0.55 % (SD 0.0304, df 20, 95 % CI 0.42–0.8 %), claim 1.2 %, UVL 1.57 %, "claim met"; between-day CV 1.0 % (SD 0.055); within-lab CV 1.14 % (SD 0.0628, df 5.99, 95 % CI 0.73–2.51 %), claim 1.6 %, UVL 2.48 %, "claim met". An independent recomputation (separate script, one-way ANOVA + Satterthwaite) gave grand 5.5136, S_r 0.0304, S_day 0.0550, S_WL 0.0628, CV_WL 1.139 %, df 5.99. |
| OQ-AQP-04 | New Precision Study form with two levels (Cholesterol: Normal, Pathological) → `PRS-2026-0002`, PR-2026-0003/0004, first level opened, "Other levels in this series: Pathological". |
| OQ-AQP-01 (partial) | UI replicate entry with day D01, run R1, operator and reagent lot saved; the form kept day/run/conditions and cleared the value. The nested EP05 reference set is covered by the automated suite only in this dry run. |
| OQ-AQP-05 | Preparer self-sign-off → 422 `SOD-AQ-001`; QM sign-off (password + PIN) → 204. MU import of the unsigned High level → 422 `MU-008`. `GET /precision-studies?state=SignedOff&analyte=GLUCOSE` → PR-2026-0001 (case-insensitive). MU UI import → component "Within-laboratory imprecision u(Rw) — PR-2026-0001", Type A, 1.1391, source text + "open study" link. Sigma UI "Use within-lab CV" → CV 1.14 % "within-lab CV from PR-2026-0001", σ = (6.9 − 0.8) / 1.1391 = 5.36. |
| OQ-AQP-06 | PT-2026-0001 (CAP CHM 2026-A, assigned 5.5, SD 0.22, z 0.5) listed with scheme CV 4.0 %, with the reproducibility note. |
| OQ-AQP-07 | Two-level validation study (Low ×3, High ×3, TEa 6.9 %): per-level CV 0.637 % / 0.68 %, TE 1.293 % / 1.263 %, both Satisfactory; pooled within-level CV 0.659 %. |

Browser console: only 401s from a deliberate first sign-in attempt on the platform-admin page;
no API error log entries.

## 5. Rev 1 — reproducibility (URS-157/158), engineering dry run 2026-10-16/17

Same throwaway environment; migration `20261016211656_PrecisionReproducibility` executed Up → Down → Up
(`site_label` column 1/0/1; widened design CHECK t/f/t).

| Suite | Result (observed) |
| ----- | ----------------- |
| Domain unit | 510 passed, 0 failed (+6 multi-site / reproducibility) |
| Application unit | 165 passed, 0 failed |
| Architecture | 202 passed, 0 failed |
| Integration (real PG) | 34 passed, 8 skipped, 0 failed |
| WebApi functional | 106 passed, 1 failed — the same environmental `StartupSeedingResilienceTests` case (D-1); API surface unchanged (no new routes) |
| Frontend unit | 142 of 142 SUCCESS; production build clean |

| OQ | Observed |
| -- | -------- |
| OQ-AQP-08 | Single-site Glucose study (Main lab, 5 × 5), reproducibility claim 2.6 %: design DaysOnly, within-lab CV 0.8853 %, UVL 3.2988 %, consistent = true. |
| OQ-AQP-09 | 3 sites × 5 days × 5 replicates imported via the API (75 imported, 0 rejected); Calculate in the UI: design "Sites × days (multi-site EP05)", grand mean 8.0295; repeatability 0.77 % (df 60, CI 0.66–0.94 %, claim 1.2 % / UVL 1.38 % met); between-day 0.43 %; within-lab 0.88 % (df 53.03, claim 1.8 % / UVL 2.08 % met); between-site 1.23 % (SD 0.0987); reproducibility 1.51 % (SD 0.1216, df 4.22, CI 0.92–4.17 %, claim 2.6 % / UVL 3.97 % met); variance shares 26 / 8 / 66 %; "Meets the EP05 multi-site minimum". An independent script (separate balanced nested ANOVA) gave grand 8.0295, S_r 0.0620, S_day 0.0345, S_site 0.0987, S_WL 0.0710, S_R 0.1216, CV_R 1.514 %, ν_R 4.22 — identical. PR-017 partial labelling covered by the automated suite only. |

## 4. Deviations

| # | Deviation | Disposition |
| - | --------- | ----------- |
| D-1 | `StartupSeedingResilienceTests` fails on this host (port 5433 occupied by another PostgreSQL). | Environmental, pre-existing, unrelated to this change. Re-run on a host where 5433 is free. |
| D-2 | The throwaway PostgreSQL process was terminated once mid-session (its launching shell timed out) and restarted; crash recovery completed and committed data was intact. | No effect on results; all recorded observations were re-read after recovery. |
