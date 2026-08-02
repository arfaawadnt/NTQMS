# 21 — Module PT: Proficiency Testing, Interlaboratory Comparison, Supplier Quality

**Module code:** `PT` (covers the `PT`/`SUP` functional surface commissioned as one module)
**Test-case ID range consumed:** `TC-PT-UNIT-001` … `TC-PT-EXPL-077` — a single continuous module-local
sequence 001–077 across all KIND codes (001–072 detailed cases, 073–077 exploratory charters), per
§5 of `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md` ("module-local sequence starting at 001").
**Gap ID range consumed:** `GAP-PT-001` … `GAP-PT-024`.
**System under test:** v1.51.2, repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master`. Inspection date 2026-08-01.

**Completeness statement.** Complete for: PT plan aggregate (create/item/approve/fulfil/close), PT
enrollment and z-score computation and banding, the PT→NC saga, the supplier aggregate
(register/certificate/approve/suspend), supplier evaluation scoring, the sweep-driven supplier
suspension proposal, the REST surface of `api/pt-plans`, `api/proficiency-tests` and `api/suppliers`,
their EF configuration, their live PostgreSQL schema (measured), and the Angular supplier/PT feature
shells. **Deferred / not covered here:** the shared audit-ledger hash chain (module `LEDGER`), the
notification dispatch transport (module `NOTIF`), the generic pagination envelope contract (module
`API`), and Playwright a11y sweeps (module `A11Y`) — those are authored in their own files and are
only *referenced* here. **Interlaboratory comparison (ILC) is not a distinct implemented feature** —
see GAP-PT-001; no executable ILC case is written against a mechanism that does not exist.

---

## 1. Implementation inventory

Everything below was read in the cited file at the cited line, or measured against the live dev
database `ntqams` on 2026-08-01 via `psql` (measurements marked **[measured]**).

### 1.1 Aggregates and value types

| Element | Kind | Location | Notes |
|---|---|---|---|
| `PtPlan` | Aggregate root, `ITenantScoped` | `src/NT.QAMS.Domain/AnalyticalQuality/PtPlan.cs:50` | Annual PT/EQA participation plan; owns `PtPlanItem` |
| `PtPlanItem` | Owned entity | `src/NT.QAMS.Domain/AnalyticalQuality/PtPlan.cs:13` | `Scheme, Analyte, Provider?, PlannedCycles, FulfilledCycles, LastEnrollmentRef?, Notes?` |
| `PtPlanStatus` | Enum | `src/NT.QAMS.Domain/AnalyticalQuality/PtPlan.cs:6` | `Draft, Approved, Closed` |
| `PtEnrollment` | Aggregate root, `ITenantScoped` | `src/NT.QAMS.Domain/AnalyticalQuality/PtEnrollment.cs:14` | One PT cycle participation + its result |
| `PtPerformance` | Enum | `src/NT.QAMS.Domain/AnalyticalQuality/PtEnrollment.cs:6` | `Pending, Satisfactory, Questionable, Unsatisfactory` |
| `PtUnsatisfactory` | Domain event | `src/NT.QAMS.Domain/AnalyticalQuality/PtEnrollment.cs:88` | `(PtId, PtRef, Analyte, ZScore, TenantId, RaisedBy)` |
| `Supplier` | Aggregate root, `ITenantScoped`, `IAllocatable` | `src/NT.QAMS.Domain/SupplierQuality/Supplier.cs:29` | Owns `CertificateRecord` |
| `CertificateRecord` | Owned entity | `src/NT.QAMS.Domain/SupplierQuality/Supplier.cs:8` | `CertificateType, ExpiresAt (DateOnly), FileId?` |
| `SupplierStatus` | Enum | `src/NT.QAMS.Domain/SupplierQuality/Supplier.cs:6` | `PendingEvaluation, Approved, Suspended` |
| `SupplierEvaluation` | Separate aggregate root, `ITenantScoped` | `src/NT.QAMS.Domain/SupplierQuality/Supplier.cs:141` | Accretes forever; the weighted total is the record of fact |
| `SupplierApproved` | Domain event | `src/NT.QAMS.Domain/SupplierQuality/Supplier.cs:196` | `(SupplierId, SupplierRef, Name, ApprovedBy, TenantId)` — **no consumer found in `src/`** |
| `SupplierSuspended` | Domain event | `src/NT.QAMS.Domain/SupplierQuality/Supplier.cs:199` | Consumed by `NotificationPolicies.cs:109` |

**No aggregate, entity, table, endpoint, contract or event named for interlaboratory comparison
exists.** The only "comparability" type is `InstrumentComparabilityStudy`
(`src/NT.QAMS.Domain/AnalyticalQuality/InstrumentComparabilityStudy.cs:37`), which is explicitly
*instrument-to-instrument inside one laboratory* ("shared samples measured across a…", line 31), not
lab-to-lab. `PtEnrollment`'s XML comment calls itself "Proficiency-testing / interlaboratory-comparison
enrollment" (`PtEnrollment.cs:8-9`) but carries **no** peer-laboratory roster, participant count,
consensus/robust-statistic field, `z'`, `zeta` or `En` number. → **GAP-PT-001**.

### 1.2 Domain invariants and error codes (PT plan)

| Code | Exception type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `PTP-001` | `DomainException` | 422 | `PtPlan.cs:70` | `year < 2000` or `year > 2100` |
| `PTP-002` | `DomainException` | 422 | `PtPlan.cs:81` | Scheme or analyte blank/whitespace on `AddItem` |
| `PTP-003` | `DomainException` | 422 | `PtPlan.cs:86` | `plannedCycles < 1` |
| `PTP-010` | `InvalidStateTransitionException` | 409 | `PtPlan.cs:111` | `Approve` when status ≠ `Draft` |
| `PTP-011` | `DomainException` | 422 | `PtPlan.cs:116` | `Approve` with zero items |
| `PTP-012` | `InvalidStateTransitionException` | 409 | `PtPlan.cs:129` | `RecordFulfilment` when status ≠ `Approved` |
| `PTP-013` | `InvalidStateTransitionException` | 409 | `PtPlan.cs:141` | `Close` when status ≠ `Approved` |
| `PTP-014` | `DomainException` | 422 | `PtPlan.cs:146` | Blank closure summary |
| `PTP-015` | `InvalidStateTransitionException` | 409 | `PtPlan.cs:157` | `AddItem`/`RemoveItem` when status ≠ `Draft` |
| `PTP-020` | `DomainException` | 422 | `PtPlanSlice.cs:57` | A plan already exists for that year in the tenant |
| `PTP-021` | `DomainException` | 422 | `PtPlanSlice.cs:107` | Fulfilment cited an enrollment whose `Performance == Pending` |
| `PTP-404` | `DomainException` | 404 | `PtPlan.cs:102`, `PtPlan.cs:133`, `PtPlanSlice.cs:124` | Plan or plan line not found |
| `SOD-AQ-001` | `DomainException` | 422 | `PtPlan.cs:108` → `AggregateRoot.cs:40` | Approver == `CreatedByUserId` |
| `TENANT-000` | `DomainException` | 422 | `PtPlanSlice.cs:54` | No tenant on the ambient context |
| `AUTH-003` | `DomainException` | **401** | `PtPlanSlice.cs:93` | No authenticated user id at approval |

### 1.3 Domain invariants and error codes (PT enrollment)

| Code | Exception type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `PT-001` | `DomainException` | 422 | `PtEnrollment.cs:41` | Scheme or analyte blank on `Enroll` |
| `PT-010` | `InvalidStateTransitionException` | 409 | `PtEnrollment.cs:62` | `RecordResult` when `Performance != Pending` |
| `PT-011` | `DomainException` | 422 | `PtEnrollment.cs:67` | `standardDeviation <= 0` |
| `PT-404` | `DomainException` | 404 | `ValidationAndPtSlice.cs:157`, `PtPlanSlice.cs:104` | Enrollment not found |
| `AUTH-003` | `DomainException` | 401 | `ValidationAndPtSlice.cs:155` | No authenticated actor on result entry |

### 1.4 The real z-score implementation — pinned

`ZScore` appears in 18 non-migration source files; only **one** of them computes the *PT* z-score.
The PT implementation is `PtEnrollment.RecordResult`:

```
src/NT.QAMS.Domain/AnalyticalQuality/PtEnrollment.cs:73-79
    var z = (submitted - assigned) / standardDeviation;   // line 73
    ZScore = Math.Round(z, 3);                            // line 74
    var absZ = Math.Abs(z);                               // line 76  <-- UNROUNDED
    Performance = absZ >= UnsatisfactoryThreshold ? PtPerformance.Unsatisfactory   // line 77
        : absZ > QuestionableThreshold ? PtPerformance.Questionable               // line 78
        : PtPerformance.Satisfactory;                                             // line 79
```

Pinned facts:

1. **Formula.** `z = (submitted − assigned) / SD`, `decimal` arithmetic throughout (all three inputs
   are `decimal`). Classic ISO 13528 z with an operator-supplied assigned value and SD; **no**
   robust/consensus estimation, **no** `z'`, `zeta` or `En`.
2. **Rounding.** `Math.Round(z, 3)` — three decimals, `MidpointRounding.ToEven` (banker's rounding,
   the .NET default; no `MidpointRounding` argument is supplied). Stored to
   `qams.pt_enrollment.z_score numeric(10,3)` **[measured]**.
3. **Thresholds.** `QuestionableThreshold = 2m` (`PtEnrollment.cs:15`), `UnsatisfactoryThreshold = 3m`
   (`PtEnrollment.cs:16`). Compile-time constants — **not** configurable, not tenant-scoped.
4. **Band comparison strictness (verified, and it is not what the doc comment says).** Line 56's
   comment claims "|z| ≤ 2 satisfactory, 2 < |z| < 3 questionable, |z| ≥ 3 unsatisfactory". Line 77–79
   implements: `|z| >= 3` → Unsatisfactory; **else** `|z| > 2` → Questionable; else Satisfactory.
   These agree. The real bands are therefore:

   | Condition on unrounded \|z\| | Performance |
   |---|---|
   | `0 ≤ |z| ≤ 2.000000…` (inclusive of exactly 2) | `Satisfactory` |
   | `2 < |z| < 3` | `Questionable` |
   | `|z| ≥ 3` (inclusive of exactly 3) | `Unsatisfactory` |

5. **Banding uses the UNROUNDED quotient; storage uses the rounded one.** A result with a true
   `|z| = 2.0004` is banded `Questionable` while `z_score` persists as `2.000` — a value that, read
   back against the published band table, reads as `Satisfactory`. This is a real, reproducible
   inconsistency in the record of truth → **GAP-PT-002**, and case `TC-PT-BVA-055`.
6. **Frozen.** There is no recompute path: `RecordResult` is one-shot (`PT-010` on the second call),
   and no other code writes `ZScore` or `Performance`.
7. **The other `ZScore` members are different features and are out of scope here:**
   `QcRun.ZScore` (`src/NT.QAMS.Domain/AnalyticalQuality/QcProfile.cs:110`, module `QC`) and
   `OutlierPoint.ZScore` / `ModifiedZScore` (`src/NT.QAMS.Domain/AnalyticalQuality/OutlierScreening.cs:25`,
   module `MV`). Both also round to 3 dp but neither participates in PT interpretation.

### 1.5 Domain invariants and error codes (suppliers)

| Code | Exception type | HTTP | Raised at | Condition |
|---|---|---|---|---|
| `SUP-001` | `DomainException` | 422 | `Supplier.cs:57` | Blank supplier name |
| `SUP-002` | `DomainException` | 422 | `Supplier.cs:74` | Blank certificate type |
| `SUP-010` | `InvalidStateTransitionException` | 409 | `Supplier.cs:86` | `Approve` when already `Approved` |
| `SUP-011` | `InvalidStateTransitionException` | 409 | `Supplier.cs:104` | `Suspend` when status ≠ `Approved` |
| `SUP-012` | `DomainException` | 422 | `Supplier.cs:109` | Blank suspension reason |
| `SUP-020` | `DomainException` | 422 | `Supplier.cs:164` | `periodEnd < periodStart` |
| `SUP-021` | `DomainException` | 422 | `Supplier.cs:169` | Zero criteria |
| `SUP-022` | `DomainException` | 422 | `Supplier.cs:175` | `Σ weight <= 0` |
| `SUP-023` | `DomainException` | 422 | `Supplier.cs:180` | Any `score < 0` or `score > 100` or `weight < 0` |
| `SUP-404` | `DomainException` | 404 | `SupplierSlice.cs:68`, `SupplierSlice.cs:154` | Supplier not found |
| `FILE-404` | `DomainException` | 404 | `SupplierSlice.cs:77` | Certificate `FileId` not in `qams.file` |
| `SOD-SUP-001` | `DomainException` | 422 | `Supplier.cs:91` | `actorId == RegisteredBy` |
| `SCOPE-001` / `SCOPE-002` | `DomainException` | 422 | `OrgScopeGuardInterceptor.cs:56,62` | Out-of-scope `BranchId`/`DepartmentId` on an `IAllocatable` write |

**Supplier SoD — the direct answer to the commissioning question.** The rule *"a user cannot approve
a supplier profile they created"* **is guarded**, but **not** by `AggregateRoot.EnsureSignerIsNotPreparer`.
`Supplier.Approve` uses a bespoke comparison against the aggregate's own `RegisteredBy` field:

```
src/NT.QAMS.Domain/SupplierQuality/Supplier.cs:89-92
    if (actorId == RegisteredBy)
    {
        throw new DomainException("SOD-SUP-001", "Segregation of duties: the registrant cannot approve their own supplier.");
    }
```

`RegisteredBy` is set from `GovernanceHelpers.RequireActor(user)` at registration
(`SupplierSlice.cs:28`) and is a non-nullable `uuid NOT NULL` column **[measured]**. The bespoke check
is therefore *stronger* than the shared helper, which is a documented no-op when `CreatedByUserId` is
null (`AggregateRoot.cs:38`, accepted residual F-05b). The divergence itself — two SoD mechanisms with
different failure modes and different error codes for the same class of rule — is **GAP-PT-013**.
The *PT plan* approval, by contrast, uses the shared helper (`PtPlan.cs:108`) and therefore inherits the
null-preparer no-op → **GAP-PT-014**.

### 1.6 Supplier evaluation scoring — pinned

```
src/NT.QAMS.Domain/SupplierQuality/Supplier.cs:172-190
    var totalWeight = criteria.Sum(c => c.Weight);                       // 172
    if (totalWeight <= 0) throw SUP-022;                                 // 173-176
    if (criteria.Any(c => c.Score is < 0 or > 100 || c.Weight < 0)) throw SUP-023;  // 178-181
    WeightedTotal = Math.Round(criteria.Sum(c => c.Weight * c.Score) / totalWeight, 2);  // 190
```

Weighted arithmetic mean, 2-dp `Math.Round` (banker's), stored to
`qams.supplier_evaluation.weighted_total numeric(5,2)` with `CHECK (weighted_total >= 0)` **[measured]**.
**Guard ordering matters:** `SUP-022` (line 173) is evaluated *before* `SUP-023` (line 178), so weights
`[-1, +1]` (sum 0) yield `SUP-022`, while `[-1, +5]` (sum 4) yield `SUP-023`. There is **no** acceptance
threshold, no grade band, and no consequence: the total is recorded and nothing reads it → **GAP-PT-015**.

### 1.7 The sweep-proposed supplier suspension

`ScheduledSweepService` is a `BackgroundService`, 1-hour `Interval`
(`src/NT.QAMS.Infrastructure/Jobs/ScheduledSweepService.cs:29`), 15-second startup delay (line 34),
per-round `ICurrentTenantSetter.Elevate()` (line 64) and PostgreSQL advisory-lock leader election
(`AdvisoryLock.TryRunExclusiveAsync`, line 70). The supplier limb is:

```
src/NT.QAMS.Infrastructure/Jobs/ScheduledSweepService.cs:119-126
    var supplierCandidates = await db.Suppliers
        .IgnoreQueryFilters()
        .Include(s => s.Certificates)
        .Where(s => s.Status == SupplierStatus.Approved
                    && s.Certificates.Any(c => c.ExpiresAt < today))
        .ToListAsync(ct);
    supplierCandidates.ForEach(s => s.SuspendIfCertificateExpired(today));
    var suspended = supplierCandidates.Count(s => s.Status == SupplierStatus.Suspended);
```

and the aggregate decides:

```
src/NT.QAMS.Domain/SupplierQuality/Supplier.cs:118-134
    if (Status != SupplierStatus.Approved) return;                       // 120-122  (declines)
    var expired = _certificates.FirstOrDefault(c => c.ExpiresAt < asOf);  // 125
    if (expired is null) return;                                          // 126-129 (declines)
    Status = SupplierStatus.Suspended;                                    // 131
    SuspensionReason = $"Certificate '{expired.CertificateType}' expired {expired.ExpiresAt:yyyy-MM-dd}.";  // 132
    Raise(new SupplierSuspended(...));                                    // 133
```

Pinned: comparison is **strict `<`** (a certificate expiring *on* `today` is not yet expired);
`today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)` (line 88) — **UTC calendar day, not the
tenant's local day** (→ GAP-PT-020 note); idempotent because a suspended supplier fails the
`Status != Approved` guard on the next round; and `FirstOrDefault` over an *unordered* owned
collection makes the reason text non-deterministic when two certificates are expired → **GAP-PT-011**.

### 1.8 Endpoints (measured against `tests/NT.QAMS.WebApi.FunctionalTests/ApiSurface.approved.txt`)

Every route is dual-exposed under `/api/v{version}/…`; only the canonical form is listed.

| Method + route | Controller line | Endpoint permission gate | Command policy | Success |
|---|---|---|---|---|
| `GET /api/pt-plans` | `PtPlansController.cs:17` | **none** (`[Authorize]` only) | n/a — query | 200 |
| `GET /api/pt-plans/{id}` | `PtPlansController.cs:21` | **none** | n/a — query | 200 |
| `POST /api/pt-plans` | `PtPlansController.cs:25` | `proficiency-testing.create` (line 26) | `[RequireInternalActor]` (`PtPlanSlice.cs:13`) | 201 + `Location` |
| `POST /api/pt-plans/{id}/items` | `PtPlansController.cs:33` | `proficiency-testing.edit` (line 34) | `[RequireInternalActor]` (`:15`) | 200 `{itemId}` |
| `DELETE /api/pt-plans/{id}/items/{itemId}` | `PtPlansController.cs:42` | `proficiency-testing.edit` (line 43) | `[RequireInternalActor]` (`:18`) | 204 |
| `POST /api/pt-plans/{id}/approve` | `PtPlansController.cs:50` | `proficiency-testing.approve` (line 51) | `[RequireInternalActor]` (`:20`) | 204 |
| `POST /api/pt-plans/{id}/fulfilments` | `PtPlansController.cs:58` | **NONE — no `[RequirePermission]`** | `[RequireInternalActor]` (`:22`) | 204 |
| `POST /api/pt-plans/{id}/close` | `PtPlansController.cs:65` | `proficiency-testing.void` (line 66) | `[RequireInternalActor]` (`:24`) | 204 |
| `GET /api/proficiency-tests` | `AnalyticalQualityControllers.cs:101` | **none** | n/a — query | 200 |
| `POST /api/proficiency-tests` | `AnalyticalQualityControllers.cs:105` | **NONE** | `[RequireInternalActor]` (`ValidationAndPtSlice.cs:127`) | 200 `{id}` |
| `POST /api/proficiency-tests/{id}/result` | `AnalyticalQualityControllers.cs:109` | **NONE** | `[RequireInternalActor]` (`:146`) | 204 |
| `GET /api/suppliers` | `GovernanceControllers.cs:175` | **none** | n/a — query | 200 paged |
| `GET /api/suppliers/{id}` | `GovernanceControllers.cs:182` | **none** | n/a — query | 200 |
| `POST /api/suppliers` | `GovernanceControllers.cs:186` | **NONE** | `[RequireInternalActor]` (`SupplierSlice.cs:11`) | 201 + `Location` |
| `POST /api/suppliers/{id}/certificates` | `GovernanceControllers.cs:194` | **NONE** | `[RequireInternalActor]` (`:37`) | 200 `{certificateId}` |
| `POST /api/suppliers/{id}/approve` | `GovernanceControllers.cs:199` | `suppliers.approve` (line 200) | `[RequireInternalActor]` (`:40`) | 204 |
| `POST /api/suppliers/{id}/suspend` | `GovernanceControllers.cs:207` | `suppliers.void` (line 208) | `[RequireInternalActor]` (`:42`) | 204 |
| `GET /api/suppliers/{id}/evaluations` | `GovernanceControllers.cs:215` | **none** | n/a — query | 200 |
| `POST /api/suppliers/{id}/evaluations` | `GovernanceControllers.cs:219` | **`suppliers.approve`** (line 220) | `[RequireInternalActor]` (`:44`) | 200 `{evaluationId}` |

Permission module keys are `PermissionCatalog.ProficiencyTesting = "proficiency-testing"`
(`src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:101`, `FullRecordLifecycle` bundle at line 174)
and `PermissionCatalog.Suppliers = "suppliers"` (line 95, `FullRecordLifecycle` at line 162), i.e. each
grants `View/Create/Edit/Approve/Void/Export`. Neither module has a `Sign` action, so **no PT or
supplier action can be e-signed** → GAP-PT-019.

### 1.9 Validation bounds actually declared

| Command | Validator | Rules | File:line |
|---|---|---|---|
| `AddPtPlanItemCommand` | `AddPtPlanItemValidator` | `Scheme` NotEmpty ≤200; `Analyte` NotEmpty ≤200; `Provider` ≤200; `PlannedCycles` InclusiveBetween(1,52); `Notes` ≤1000 | `PtPlanSlice.cs:36-46` |
| `ClosePtPlanCommand` | `ClosePtPlanValidator` | `ClosureSummary` NotEmpty ≤4000 | `PtPlanSlice.cs:28-34` |
| `RegisterSupplierCommand` | `RegisterSupplierValidator` | `Name` NotEmpty ≤200 — **`SupplierType` unbounded** | `SupplierSlice.cs:15-18` |
| `RecordEvaluationCommand` | `RecordEvaluationValidator` | `Criteria` NotEmpty, `Count ≤ 50`, each `Criterion` non-blank ≤200 | `SupplierSlice.cs:50-62` |
| `CreatePtPlanCommand` | **none** | — | — |
| `RemovePtPlanItemCommand` | **none** | — | — |
| `ApprovePtPlanCommand` | **none** | — | — |
| `RecordPtPlanFulfilmentCommand` | **none** | — | — |
| `EnrollPtCommand` | **none** | `Scheme`→`varchar(100)`, `Analyte`→`varchar(100)`, `Cycle`→`varchar(50)` unguarded | — |
| `RecordPtResultCommand` | **none** | — | — |
| `AddCertificateCommand` | **none** | `CertificateType`→`varchar(100)` unguarded | — |
| `ApproveSupplierCommand` | **none** | — | — |
| `SuspendSupplierCommand` | **none** | `Reason`→`varchar(500)` unguarded | — |

The unbounded string commands violate URS-104 ("the limit shall exist in the command validator") for
their own columns; an over-long value reaches PostgreSQL and raises `22001 string_data_right_truncation`,
which `DomainExceptionHandler` does **not** map (`_ => null`, `DomainExceptionHandler.cs:81`) → HTTP 500.
→ **GAP-PT-009**.

### 1.10 Persistence and database (measured 2026-08-01, `psql` on `ntqams`)

| Table | RLS enabled | RLS forced | Policy | Primary key | Other constraints |
|---|---|---|---|---|---|
| `qams.pt_plan` | `t` | `t` | `tenant_isolation` | `(tenant_id, id)` | `ux_pt_plan_id_tenant UNIQUE (id, tenant_id)`; `ck_pt_plan_status_domain CHECK status IN ('Draft','Approved','Closed')` |
| `qams.pt_plan_item` | `t` | `t` | `tenant_isolation` | `(tenant_id, id)` | `fk_pt_plan_item_pt_plan_tenant_id_plan_id FOREIGN KEY (tenant_id, plan_id) REFERENCES qams.pt_plan(tenant_id, id) ON DELETE CASCADE` |
| `qams.pt_enrollment` | `t` | `t` | `tenant_isolation` | `(tenant_id, id)` | `ck_pt_enrollment_performance_domain CHECK performance IN ('Pending','Satisfactory','Questionable','Unsatisfactory')` |
| `qams.supplier` | `t` | `t` | `tenant_isolation` | `(tenant_id, id)` | `ux_supplier_id_tenant UNIQUE (id, tenant_id)`; `ck_supplier_status_domain CHECK status IN ('PendingEvaluation','Approved','Suspended')` |
| `qams.supplier_certificate` | `t` | `t` | `tenant_isolation` | `(tenant_id, id)` | `fk_supplier_certificate_supplier_tenant_id_supplier_id FOREIGN KEY (tenant_id, supplier_id) REFERENCES qams.supplier(tenant_id, id) ON DELETE CASCADE` |
| `qams.supplier_evaluation` | `t` | `t` | `tenant_isolation` | `(tenant_id, id)` | `ck_supplier_evaluation_score_nonnegative CHECK (weighted_total >= 0)` — **no FK to `qams.supplier`** |

Column facts that drive boundary cases **[measured]**:
`pt_enrollment.submitted_value / assigned_value / standard_deviation numeric(18,6) NULL`;
`pt_enrollment.z_score numeric(10,3) NULL`; `pt_enrollment.pt_ref varchar(30)`,
`scheme varchar(100)`, `analyte varchar(100)`, `cycle varchar(50) NOT NULL`;
`pt_plan.closure_summary text` (widened by `Hardening1_TypesAndNames`);
`pt_plan_item.notes text`, `scheme/analyte/provider varchar(200)`, `last_enrollment_ref varchar(30)`;
`supplier.supplier_type varchar(50) NOT NULL`, `supplier.suspension_reason varchar(500)`,
`supplier.registered_by uuid NOT NULL`, `supplier.approved_by uuid NULL`;
`supplier_certificate.certificate_type varchar(100)`, `expires_at date NOT NULL`;
`supplier_evaluation.criteria jsonb NOT NULL`, `weighted_total numeric(5,2) NOT NULL`.

**Triggers: none.** `SELECT … FROM information_schema.triggers` over all six tables returned
**0 rows** **[measured]** — `qams.reject_frozen_mutation()` is *not* attached to `pt_plan`,
`pt_enrollment`, `supplier` or `supplier_evaluation`. An Approved plan, a resulted enrollment and an
Approved supplier are all freely `UPDATE`-able at the database layer → **GAP-PT-005**, **GAP-PT-006**.

**Optimistic concurrency:** `xmin` is applied by convention to every `AggregateRoot`
(`AppDbContext.cs:120-133`), so `PtPlan`, `PtEnrollment`, `Supplier` and `SupplierEvaluation` all carry
it; a lost update surfaces as `DbUpdateConcurrencyException` → **409 `CONCURRENCY-409`**
(`DomainExceptionHandler.cs:21,28-33`).

**Migration `20260725075423_PtPlanAndAuditTrailReview`** created `pt_plan`, `pt_plan_item` and
`audit_trail_review` with single-column PKs and this RLS block (lines 124–131):

```sql
ALTER TABLE qams.pt_plan ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON qams.pt_plan
    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
```

— i.e. **`USING` only: no `FORCE`, no `WITH CHECK`, no `app.bypass_rls` disjunct**, and
`pt_plan_item` got **no policy at all**. The current measured state (FORCE + full policy on both) is
produced by the *later* blanket migrations `20260726081443_ActivateForcedTenantRls`
(which iterates `pg_policies` for `policyname='tenant_isolation'` and adds FORCE + the standard
`USING/WITH CHECK`) and `20260731201114_Hardening4_ChildTenancy` (lines 618–621 for `pt_plan_item`,
663–666 for `supplier_certificate`). A deployment halted at `PtPlanAndAuditTrailReview` is write-leaky
on `pt_plan` and wholly unfenced on `pt_plan_item` → **GAP-PT-024**.
`Down()` (lines 135–148) drops the three tables and does **not** drop the policies explicitly (they
fall with the table), and does not restore `pt_plan_item`'s pre-Hardening4 shape.

### 1.11 Cross-module integrations

| Integration | Location | Behaviour |
|---|---|---|
| PT → NC saga | `src/NT.QAMS.Application/AnalyticalQuality/PtToNcPolicy.cs:23-50` | On `PtUnsatisfactory`: sets tenant from the event (line 26); idempotency key `SourceRef = "PT:{PtRef}"` (line 28-31); raises NC with `severity: 4, likelihood: 3` → `Rpn = 12`, `NcSourceType.ProficiencyTest`, title `"Unsatisfactory PT result {PtRef} ({Analyte})"`, then `nc.Submit()` (line 45) |
| Supplier suspension → notification | `NotificationPolicies.cs:109-114` | Dispatch key `SUP_SUSPENDED` with tokens `ref`, `title`, `reason`; default rule seeded per tenant at provisioning with audience `"QualityManager,TenantAdmin"` (`NotificationPolicies.cs:152-153,160`) |
| PT KPI | `src/NT.QAMS.Infrastructure/Jobs/KpiSnapshotService.cs:126-127` | `snapshot.PtUnsatisfactory = count(pt_enrollment where performance = 'Unsatisfactory')`, per tenant, `IgnoreQueryFilters` |
| KPI export | `src/NT.QAMS.WebApi/Controllers/ExportsController.cs:144` | `PtUnsatisfactory` emitted as a column in the KPI XLSX |
| Reference numbering | `src/NT.QAMS.Infrastructure/Persistence/RefCounter.cs:24-43` | `"{refType}-{year}-{next:0000}"`, atomic upsert per `(tenant, type, year)`; prefixes in scope: `PTP` (`PtPlanSlice.cs:60`), `PT` (`ValidationAndPtSlice.cs:138`), `SUP` (`SupplierSlice.cs:26`) |
| Org scope | `OrgScopeGuardInterceptor.cs:46-65` | `Supplier` is `IAllocatable`; out-of-scope branch → `SCOPE-001`, department → `SCOPE-002`. **`PtPlan` and `PtEnrollment` are not `IAllocatable`** |

### 1.12 Frontend surface

| Artefact | Path | Notes |
|---|---|---|
| Supplier list | `frontend/src/app/features/supplier/supplier-list.component.ts` (173 lines) | Route `/suppliers` (`app.routes.ts:368`) |
| Supplier detail | `frontend/src/app/features/supplier/supplier-detail.component.ts` (220 lines) | Route `/suppliers/:id` (`app.routes.ts:373`); approve button rendered only when `perms.can('suppliers.approve') && s.status === 'PendingEvaluation'`; suspend form only when `s.status === 'Approved' && perms.can('suppliers.void')` |
| Supplier facade | `frontend/src/app/features/supplier/supplier.facade.ts` (132 lines) | Signal store; `loadMore()` load-more pager over the API-004 envelope; certificate file uploaded first then linked by id |
| PT plan list / detail / facade | `frontend/src/app/features/analytical/pt-plan-{list,detail}.component.ts`, `pt-plans.facade.ts` | Route `/pt-plans`, `/pt-plans/:id` (`app.routes.ts:354-359`); detail gates on `proficiency-testing.{approve,edit,void}` |
| PT enrollment list | `frontend/src/app/features/analytical/pt-list.component.ts`, `pt.facade.ts` | Route `/proficiency-tests` (`app.routes.ts:364`); z-score rendered `{{ e.zScore | number:'1.2-2' }}` (line 62) — **2 dp**, against a 3-dp record of truth → GAP-PT-022 |

### 1.13 Existing automated coverage (baseline — do not duplicate blindly)

| Test | File | What it already pins |
|---|---|---|
| `PtPlanTests` (3 facts) | `tests/NT.QAMS.Domain.UnitTests/AnalyticalQuality/PtPlanTests.cs` | PTP-011/PTP-015, PTP-012 + fulfilment counting, PTP-013/PTP-014 |
| PT z-score theory (4 rows) + 3 facts | `tests/NT.QAMS.Domain.UnitTests/AnalyticalQuality/WestgardEvaluatorTests.cs:196-236` | z = 0 / 2.4 / ±3.2 banding, event raise/no-raise, PT-010 |
| `SupplierTests` (3 facts) | `tests/NT.QAMS.Domain.UnitTests/Governance/GovernanceAndSupplierTests.cs:153-215` | SOD-SUP-001, sweep decline/accept/idempotence, weighted total 84.00 + SUP-020/SUP-023 |
| `PtToNcPolicyTests` | `tests/NT.QAMS.Application.UnitTests/AnalyticalQuality/PtToNcPolicyTests.cs` | Saga raise + idempotency |
| `CommandPolicyTests` | `tests/NT.QAMS.Architecture.Tests/CommandPolicyTests.cs` | Every command carries exactly one `CommandPolicyAttribute` |

**No boundary case at |z| = 2.00 or 3.00 exists today, and no functional/API test exists for any
route in §1.8.** That absence is what most of §5 addresses.

---

## 2. Divergences from the commissioning brief

| # | Brief / URS expectation | As built (verified) | Evidence | Disposition |
|---|---|---|---|---|
| D-01 | Interlaboratory comparison (ILC) as a feature alongside PT | No ILC aggregate/table/endpoint. `PtEnrollment` is named for both but models only a provider-scheme PT cycle. `InstrumentComparabilityStudy` is intra-lab | `PtEnrollment.cs:8`, `InstrumentComparabilityStudy.cs:31-37`; `ApiSurface.approved.txt` has no ILC route | **GAP-PT-001** — no executable ILC case |
| D-02 | Configurable PT interpretation bands | `2m` / `3m` compile-time constants | `PtEnrollment.cs:15-16` | **GAP-PT-003** |
| D-03 | ISO 13528 statistics (`z'`, `zeta`, `En`, robust/consensus assigned value) | Only classic `z` with operator-entered assigned value and SD | `PtEnrollment.cs:58-79` | **GAP-PT-003** |
| D-04 | PT scheduling (due dates, reminders) | A plan carries only `Year` and per-line `PlannedCycles`; no dates, no reminder, no sweep limb for PT | `PtPlan.cs:56-64`; `ScheduledSweepService.cs:86-152` has no PT branch | **GAP-PT-017** |
| D-05 | PT participant data | No participant count, no peer/consensus data, no provider-report attachment, no method code | `PtEnrollment.cs:26-35` (full property set) | **GAP-PT-016** |
| D-06 | PT reports and trends | No PT report endpoint; only a scalar `PtUnsatisfactory` KPI and the plan's planned/fulfilled counters | `KpiSnapshotService.cs:126`; `PtPlanSlice.cs:137-141`; no PT route in `ExportsController` | **GAP-PT-004** |
| D-07 | Approved PT plan / signed PT result immutable | No DB trigger; `xmin` only | 0 triggers **[measured]**; `AppDbContext.cs:120-133` | **GAP-PT-005** |
| D-08 | PT result electronically signed (Part 11) | `RecordPtResult` is an unsigned write; `proficiency-testing` module has no `Sign` action | `PermissionCatalog.cs:174` (`FullRecordLifecycle`); `ValidationAndPtSlice.cs:150-160` | **GAP-PT-019** |
| D-09 | Supplier *disqualification* | Only `Suspended`. No terminal disqualified/blacklisted state, no reinstatement workflow, no approval expiry | `Supplier.cs:6` | **GAP-PT-012** |
| D-10 | SoD: user cannot approve a supplier they created | **Implemented**, but by a bespoke `RegisteredBy` check with code `SOD-SUP-001`, not `EnsureSignerIsNotPreparer` | `Supplier.cs:89-92` vs `AggregateRoot.cs:36-42` | **GAP-PT-013** (naming/mechanism divergence only — the rule holds) |
| D-11 | SoD on PT plan approval | Uses `EnsureSignerIsNotPreparer` → **no-op when `created_by_user_id IS NULL`** | `PtPlan.cs:108`; `AggregateRoot.cs:38` | **GAP-PT-014** |
| D-12 | Every write endpoint permission-gated | 6 write routes in this module carry no `[RequirePermission]`; only tier defence (`RequireInternalActor`) | §1.8 rows marked **NONE** | **GAP-PT-007** |
| D-13 | Recording a periodic evaluation is an *edit*, not an approval | `POST /api/suppliers/{id}/evaluations` gated on `suppliers.approve` | `GovernanceControllers.cs:220` | **GAP-PT-008** |
| D-14 | URS-104: every free-text field bounded in a validator | 9 commands in this module have no validator at all | §1.9 | **GAP-PT-009** |
| D-15 | Supplier evaluation drives supplier status | `WeightedTotal` is written and never read by any rule | grep of `WeightedTotal` → only `SupplierSlice.cs:176` (projection) | **GAP-PT-015** |
| D-16 | Referential integrity supplier ↔ evaluation | `supplier_evaluation` has no FK to `supplier` | **[measured]** `pg_constraint` | **GAP-PT-010** |
| D-17 | Deterministic suspension reason | `FirstOrDefault` over an unordered owned collection | `Supplier.cs:125` | **GAP-PT-011** |
| D-18 | Stored z-score is the value the band was decided on | Band uses unrounded `absZ`; storage uses `Math.Round(z,3)` | `PtEnrollment.cs:74,76` | **GAP-PT-002** |
| D-19 | New tenant-scoped table gets FORCE RLS + `WITH CHECK` in its own migration (CLAUDE.md §5) | `PtPlanAndAuditTrailReview` shipped `USING`-only, no FORCE, and nothing for `pt_plan_item` | migration lines 124-131 | **GAP-PT-024** |
| D-20 | One enrollment per scheme/analyte/cycle | No uniqueness beyond `(tenant_id, pt_ref)` | EF config `AnalyticalQualityConfigurations.cs:89` | **GAP-PT-018** |
| D-21 | Warning ahead of certificate expiry | Suspension is the first and only signal | `ScheduledSweepService.cs:119-126` | **GAP-PT-025** *(recorded; folded into GAP-PT-012's register entry family — see §8)* |
| D-22 | UI shows the record of truth | z-score rendered to 2 dp | `pt-list.component.ts:62` | **GAP-PT-022** |
| D-23 | SoD on suspend/reinstate | Same actor may suspend then re-approve (the `RegisteredBy` check only blocks the *registrant*) | `Supplier.cs:82-98,100-115` | **GAP-PT-023** |

---

## 3. State-transition matrices

### 3.1 `PtPlan` (`PtPlanStatus`)

Legend: cell = outcome. `→X` = transition to X. Code in parentheses is the domain code; HTTP in brackets.

| From \ Event | `AddItem` | `RemoveItem` | `Approve` (actor ≠ preparer) | `Approve` (actor == preparer) | `RecordFulfilment` | `Close` (non-blank) | `Close` (blank) |
|---|---|---|---|---|---|---|---|
| **Draft** (0 items) | →Draft, item added | `PTP-404` [404] | `PTP-011` [422] | `SOD-AQ-001` [422] *(checked first, line 108)* | `PTP-012` [409] | `PTP-013` [409] | `PTP-013` [409] |
| **Draft** (≥1 item) | →Draft | →Draft, item removed | **→Approved**, `ApprovedBy`/`ApprovedAtUtc` set | `SOD-AQ-001` [422] | `PTP-012` [409] | `PTP-013` [409] | `PTP-013` [409] |
| **Approved** | `PTP-015` [409] | `PTP-015` [409] | `PTP-010` [409] | `SOD-AQ-001` [422] *(guard precedes the state check)* | →Approved, `FulfilledCycles++` | **→Closed** | `PTP-014` [422] |
| **Closed** | `PTP-015` [409] | `PTP-015` [409] | `PTP-010` [409] | `SOD-AQ-001` [422] | `PTP-012` [409] | `PTP-013` [409] | `PTP-013` [409] |

**Ordering note pinned from source:** `EnsureSignerIsNotPreparer` is line 108, *before* the status
check at line 109 and the emptiness check at line 115. Self-approval therefore returns **422
`SOD-AQ-001`** even on an already-Approved or empty plan — the SoD code masks `PTP-010`/`PTP-011`.
That precedence is exercised by `TC-PT-MCDC-024`.

### 3.2 `PtEnrollment` (`PtPerformance`)

| From \ Event | `RecordResult` (sd > 0) | `RecordResult` (sd ≤ 0) | second `RecordResult` |
|---|---|---|---|
| **Pending** | →`Satisfactory` \| `Questionable` \| `Unsatisfactory` per §1.4 band table; `ZScore` written | `PT-011` [422], state unchanged | n/a |
| **Satisfactory** | `PT-010` [409] | `PT-010` [409] *(state guard is line 60, before the sd guard at line 65)* | `PT-010` [409] |
| **Questionable** | `PT-010` [409] | `PT-010` [409] | `PT-010` [409] |
| **Unsatisfactory** | `PT-010` [409] | `PT-010` [409] | `PT-010` [409] |

There is **no** transition out of a resulted state — no amend, no void, no re-open.

### 3.3 `Supplier` (`SupplierStatus`)

| From \ Event | `Approve` (actor ≠ RegisteredBy) | `Approve` (actor == RegisteredBy) | `Suspend` (reason non-blank) | `Suspend` (reason blank) | `SuspendIfCertificateExpired` (an expired cert) | `SuspendIfCertificateExpired` (none expired) | `AddCertificate` |
|---|---|---|---|---|---|---|---|
| **PendingEvaluation** | **→Approved**, `ApprovedBy` set, `SuspensionReason` cleared, `SupplierApproved` raised | `SOD-SUP-001` [422] | `SUP-011` [409] | `SUP-011` [409] *(state guard is line 102, before the reason guard at line 107)* | no-op (declined, line 120) | no-op | allowed |
| **Approved** | `SUP-010` [409] | `SUP-010` [409] *(state guard line 84 precedes the SoD guard line 89)* | **→Suspended**, reason stored trimmed, `SupplierSuspended` raised | `SUP-012` [422] | **→Suspended**, generated reason, event raised | no-op | allowed |
| **Suspended** | **→Approved** (reinstatement; `SuspensionReason` set to null) | `SOD-SUP-001` [422] | `SUP-011` [409] | `SUP-011` [409] | no-op (declined) | no-op | allowed |

**Reinstatement `Suspended → Approved` is legal in the domain and unreachable in the SPA** (the button
is rendered only for `PendingEvaluation`, `supplier-detail.component.ts` approve block). API-only path.

---

## 4. Decision tables

### 4.1 DT-PT-1 — `PtEnrollment.RecordResult` band selection (`PtEnrollment.cs:76-79`)

| Rule | `Performance == Pending` | `sd > 0` | `|z| ≥ 3` | `|z| > 2` | Outcome |
|---|---|---|---|---|---|
| R1 | F | – | – | – | `PT-010` [409], no write |
| R2 | T | F | – | – | `PT-011` [422], no write |
| R3 | T | T | T | (T) | `Unsatisfactory`; `PtUnsatisfactory` raised |
| R4 | T | T | F | T | `Questionable`; no event |
| R5 | T | T | F | F | `Satisfactory`; no event |

Infeasible combination: `|z| ≥ 3` ∧ `¬(|z| > 2)` — masked by the ternary chain; recorded here so the
MC-DC case `TC-PT-MCDC-053` can state why only 4 of 8 condition vectors are reachable.

### 4.2 DT-PT-2 — `Supplier.Approve` (`Supplier.cs:82-98`)

| Rule | `Status == Approved` | `actorId == RegisteredBy` | Outcome |
|---|---|---|---|
| R1 | T | T | `SUP-010` [409] — state guard evaluated first (line 84) |
| R2 | T | F | `SUP-010` [409] |
| R3 | F | T | `SOD-SUP-001` [422] |
| R4 | F | F | →`Approved`, `ApprovedBy = actorId`, `SuspensionReason = null`, `SupplierApproved` raised |

### 4.3 DT-PT-3 — `Supplier.SuspendIfCertificateExpired(asOf)` (`Supplier.cs:118-134`)

| Rule | `Status == Approved` | Any cert with `ExpiresAt < asOf` | Outcome |
|---|---|---|---|
| R1 | F | – | Silent no-op (`return`, line 122) — declined proposal |
| R2 | T | F | Silent no-op (`return`, line 128) |
| R3 | T | T | →`Suspended`; `SuspensionReason = "Certificate '{type}' expired {yyyy-MM-dd}."`; `SupplierSuspended` raised |

### 4.4 DT-PT-4 — `SupplierEvaluation.Record` guard precedence (`Supplier.cs:161-181`)

| Rule | `periodEnd < periodStart` | `criteria` empty/null | `Σw ≤ 0` | any `score∉[0,100]` or `w<0` | Outcome |
|---|---|---|---|---|---|
| R1 | T | – | – | – | `SUP-020` [422] |
| R2 | F | T | – | – | `SUP-021` [422] |
| R3 | F | F | T | – | `SUP-022` [422] — **fires even when a negative weight is the cause** |
| R4 | F | F | F | T | `SUP-023` [422] |
| R5 | F | F | F | F | Record created; `WeightedTotal = round(Σ(w·s)/Σw, 2)` |

### 4.5 DT-PT-5 — Authorization outcome per PT/supplier write route

| Rule | Authenticated | Role `ExternalAuditor` | Endpoint declares `[RequirePermission]` | Actor's tenant role grants that key | Result |
|---|---|---|---|---|---|
| A1 | F | – | – | – | `401` (framework `[Authorize]`) |
| A2 | T | T | – | – | `403` `AUTHZ-002` from `AuthorizationBehavior.cs:75,83` |
| A3 | T | F | T | F | `403` `AUTHZ-403` from `ProblemAuthorizationResultHandler.cs:16` (endpoint filter fires first) |
| A4 | T | F | T | T | Command executes |
| A5 | T | F | **F** | (irrelevant) | Command executes — **any internal role succeeds** (GAP-PT-007) |

---

## 5. Detailed test cases

Format: one field block per case, all 28 mandatory fields, in the order given by
`00-GROUND-TRUTH-AND-CONVENTIONS.md` §4. `Result` is `Not Run` throughout — this package is authored,
not executed.

Shared environment shorthand used in the `Environment` field:
- **ENV-UNIT** — `dotnet test tests/NT.QAMS.Domain.UnitTests`, no database, no host.
- **ENV-APP** — `dotnet test tests/NT.QAMS.Application.UnitTests`, EF InMemory provider (no `xmin`, no RLS, no CHECK constraints).
- **ENV-API** — `tests/NT.QAMS.WebApi.FunctionalTests` `WebApplicationFactory`, or the live dev API at `http://localhost:5080` started by `scripts/dev-up.ps1`; tenant `demo-lab`.
- **ENV-PG** — `tests/NT.QAMS.IntegrationTests` against PostgreSQL 17 `ntqams` with `QMS_ITEST_POSTGRES` set, inside a rollback transaction; or direct `psql` as `qams_app`.
- **ENV-FE** — `frontend`, `ng test --watch=false --browsers=ChromeHeadless` (Jasmine/Karma) or Playwright against a running API.

<!-- CASES -->

<!-- APPEND -->
