# 17 — Module Test Package: Equipment, Calibration, Maintenance, Reference Standards, Metrological Traceability, Environmental Monitoring

**Module code:** `EQUIP`
**System under test:** NT.QMS v1.51.2, repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master`. Inspection date 2026-08-01.
**Binding conventions:** `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md` (read in full before this file).

**ID ranges consumed by this file**

| Kind | Range | Count |
|---|---|---|
| `TC-EQUIP-EP-` | 001–008 | 8 |
| `TC-EQUIP-BVA-` | 001–016 | 16 |
| `TC-EQUIP-DT-` | 001–007 | 7 |
| `TC-EQUIP-STATE-` | 001–012 | 12 |
| `TC-EQUIP-API-` | 001–012 | 12 |
| `TC-EQUIP-SEC-` | 001–008 | 8 |
| `TC-EQUIP-RLS-` | 001–006 | 6 |
| `TC-EQUIP-INT-` | 001–011 | 11 |
| `TC-EQUIP-MCDC-` | 001–003 | 3 |
| `TC-EQUIP-PATH-` | 001–002 | 2 |
| `TC-EQUIP-DF-` | 001–002 | 2 |
| `TC-EQUIP-LOOP-` | 001–002 | 2 |
| `TC-EQUIP-OBS-` | 001–002 | 2 |
| `TC-EQUIP-DR-` | 001–002 | 2 |
| `TC-EQUIP-E2E-` | 001–003 | 3 |
| `TC-EQUIP-PERF-` | 001 | 1 |
| `TC-EQUIP-A11Y-` | 001 | 1 |
| **Detailed cases total** | | **98** |
| `TC-EQUIP-UAT-` | 001–009 | 9 (Gherkin, §6) |
| `TC-EQUIP-EXPL-` | 001–005 | 5 (charters, §7) |
| `GAP-EQUIP-` | 001–023 | 23 |

**Completeness statement.** Complete: the `EquipmentItem`, `ReferenceStandard` and `MonitoringPoint` aggregates (every public method and every guard clause), their application slices, their three controllers, the calibration-due / grace-lockout / standard-expiry passes of `ScheduledSweepService`, the three named migrations plus the four later hardening migrations that touch these tables, and the Angular equipment/standards/monitoring features. **Deferred / not executable:** electronic signature on the calibration record, measured-deviation vs acceptance-limit evaluation, service-agency master data, calibration/maintenance *scheduling* entities, blocking of out-of-service equipment at the point of use, and advance calibration-due warning — none of these exist in the build; each is a Gap in §8 with an `[ID]`-labelled case authored against the real mechanism where a partial one exists. No case in this file was executed; every `Result` is `Not Run`.

**Risk IDs.** `docs/validation/02-Functional-Risk-Assessment.md` carries **no** equipment-specific risk identifiers — equipment sits inside the area row *"Governance (change/risk/policy/supplier/reviews) — URS-047,048,049,050,051,052 — Low–Medium"*. Per the conventions §5 I therefore **mint** the following and say so:

| Risk ID | Statement |
|---|---|
| RSK-EQUIP-001 | An instrument's identity is duplicated or ambiguous (serial number / code collision). |
| RSK-EQUIP-002 | The next-calibration due date is computed wrongly, so an instrument runs past its interval. |
| RSK-EQUIP-003 | An overdue or out-of-service instrument is still used to produce reportable results. |
| RSK-EQUIP-004 | The grace-period lockout does not fire, or fires early, on the boundary day. |
| RSK-EQUIP-005 | Calibration evidence (certificate) is missing, unverifiable, or detached from the record. |
| RSK-EQUIP-006 | A calibration record cannot be attributed to a responsible signatory (Part 11 §11.50/§11.70). |
| RSK-EQUIP-007 | Equipment, standard or environmental data leaks across tenants or working scopes. |
| RSK-EQUIP-008 | An actor without the granted capability mutates the equipment register. |
| RSK-EQUIP-009 | The metrological traceability chain to the SI is broken or undocumented (ISO 17025 §6.5). |
| RSK-EQUIP-010 | An environmental excursion goes undetected, or detects but raises no nonconformance. |
| RSK-EQUIP-011 | The compliance sweep is unavailable, duplicated across replicas, or non-idempotent. |
| RSK-EQUIP-012 | An equipment state change is absent from the tamper-evident audit trail. |

---

## 1. Implementation inventory

### 1.1 Aggregates, entities and value carriers

| Element | Kind | Interfaces | Location |
|---|---|---|---|
| `EquipmentItem` | Aggregate root | `AggregateRoot, ITenantScoped, IAllocatable` | `src/NT.QAMS.Domain/Equipment/EquipmentItem.cs:77` |
| `CalibrationRecord` | Owned child entity | `Entity` | `src/NT.QAMS.Domain/Equipment/EquipmentItem.cs:8` |
| `MaintenanceRecord` | Owned child entity | `Entity` | `src/NT.QAMS.Domain/Equipment/EquipmentItem.cs:26` |
| `IntermediateCheck` | Owned child entity | `Entity` | `src/NT.QAMS.Domain/Equipment/EquipmentItem.cs:45` |
| `ReferenceStandard` | Aggregate root | `AggregateRoot, ITenantScoped, IAllocatable` | `src/NT.QAMS.Domain/Equipment/ReferenceStandard.cs:18` |
| `MonitoringPoint` | Aggregate root | `AggregateRoot, ITenantScoped, IAllocatable` | `src/NT.QAMS.Domain/Facility/MonitoringPoint.cs:43` |
| `EnvironmentalReading` | Owned child entity | `Entity` | `src/NT.QAMS.Domain/Facility/MonitoringPoint.cs:13` |

`EquipmentItem` persisted fields (`EquipmentItem.cs:90–101`): `TenantId, BranchId?, DepartmentId?, Code, Name, SerialNumber, Location?, Status, CalibrationIntervalDays, GracePeriodDays, LastCalibrationAt?, NextCalibrationDue?`. **There is no manufacturer, model, model-number, asset-tag, supplier or purchase field** — see GAP-EQUIP-007.

### 1.2 Enumerations (all persisted as strings, DB-constrained)

| Enum | Members | Declaration | DB CHECK |
|---|---|---|---|
| `EquipmentStatus` | `NeedsCalibration, Active, OutOfService, Retired` | `EquipmentItem.cs:6` | `ck_equipment_item_status_domain` — `Hardening3_CheckDomains.cs:79` |
| `ReferenceStandardType` | `CertifiedReferenceMaterial, ReferenceStandard, WorkingStandard` | `ReferenceStandard.cs:6` | `ck_reference_standard_type_domain` — `Hardening3_CheckDomains.cs:129` |
| `ReferenceStandardStatus` | `Active, Quarantined, Expired, Retired` | `ReferenceStandard.cs:8` | `ck_reference_standard_status_domain` — `Hardening3_CheckDomains.cs:127` |
| `MonitoringPointStatus` | `Active, Suspended, Retired` | `MonitoringPoint.cs:6` | `ck_monitoring_point_status_domain` — `Hardening3_CheckDomains.cs:99` |

### 1.3 Invariants and domain error codes (every one read in source)

| Code | Exception type → HTTP | Rule | Line |
|---|---|---|---|
| `EQP-001` | `DomainException` → 422 | Equipment name is required (null/whitespace rejected). | `EquipmentItem.cs:113` |
| `EQP-002` | `DomainException` → 422 | Serial number is required. | `EquipmentItem.cs:118` |
| `EQP-003` | `DomainException` → 422 | `calibrationIntervalDays < 1 \|\| gracePeriodDays < 0` rejected. | `EquipmentItem.cs:123` |
| `EQP-004` | `DomainException` → 422 | Serial number already registered (application-level pre-check). | `EquipmentSlice.cs:41` |
| `EQP-010` | `InvalidStateTransitionException` → **409** | Retired equipment cannot be calibrated. | `EquipmentItem.cs:142` |
| `EQP-011` | `DomainException` → 422 | A calibration result string is required. | `EquipmentItem.cs:147` |
| `EQP-012` | `InvalidStateTransitionException` → **409** | Retired equipment cannot receive maintenance. | `EquipmentItem.cs:197` |
| `EQP-013` | `DomainException` → 422 | Maintenance description is required. | `EquipmentItem.cs:201` |
| `EQP-014` | `InvalidStateTransitionException` → **409** | Equipment is already retired. | `EquipmentItem.cs:245` |
| `EQP-020` | `InvalidStateTransitionException` → **409** | Retired equipment cannot receive intermediate checks. | `EquipmentItem.cs:219` |
| `EQP-021` | `DomainException` → 422 | A check type is required. | `EquipmentItem.cs:224` |
| `EQP-404` | `DomainException` → **404** (suffix rule) | Equipment not found (also covers cross-tenant / out-of-scope ids). | `EquipmentSlice.cs:85`, `:204` |
| `FILE-404` | `DomainException` → 404 | Referenced certificate file id does not exist. | `EquipmentSlice.cs:94` |
| `RS-001` | `DomainException` → 422 | Standard name required. | `ReferenceStandard.cs:55` |
| `RS-002` | `DomainException` → 422 | `TraceableTo` required — "an untraceable standard cannot anchor calibrations". | `ReferenceStandard.cs:60` |
| `RS-003` | `DomainException` → 422 | `ExpiresOn` must be strictly after `ReceivedOn`. | `ReferenceStandard.cs:65` |
| `RS-010` | `InvalidStateTransitionException` → 409 | Only an `Active` standard can be quarantined. | `ReferenceStandard.cs:89` |
| `RS-011` | `DomainException` → 422 | Quarantine reason required. | `ReferenceStandard.cs:94` |
| `RS-012` | `InvalidStateTransitionException` → 409 | Only a `Quarantined` standard can be reactivated. | `ReferenceStandard.cs:106` |
| `RS-013` | `DomainException` → 422 | An expired certificate cannot be reactivated. | `ReferenceStandard.cs:111` |
| `RS-014` | `InvalidStateTransitionException` → 409 | Standard already retired. | `ReferenceStandard.cs:134` |
| `RS-020` | `DomainException` → 422 | Intermediate check may only cite an **Active** standard. | `EquipmentSlice.cs:151` |
| `RS-404` | `DomainException` → 404 | Reference standard not found. | `EquipmentSlice.cs:148`, `ReferenceStandardsSlice.cs:99` |
| `ENV-001` | `DomainException` → 422 | Monitoring-point name required. | `MonitoringPoint.cs:76` |
| `ENV-002` | `DomainException` → 422 | Parameter **and** unit required. | `MonitoringPoint.cs:81` |
| `ENV-003` | `DomainException` → 422 | At least one of low/high limit required. | `MonitoringPoint.cs:107` |
| `ENV-004` | `DomainException` → 422 | `lowLimit >= highLimit` rejected (equal limits are rejected). | `MonitoringPoint.cs:112` |
| `ENV-010` | `InvalidStateTransitionException` → 409 | Retired point cannot be re-baselined. | `MonitoringPoint.cs:102` |
| `ENV-011` | `InvalidStateTransitionException` → 409 | Readings only on an `Active` point. | `MonitoringPoint.cs:127` |
| `ENV-012` | `InvalidStateTransitionException` → 409 | Only an `Active` point can be suspended. | `MonitoringPoint.cs:150` |
| `ENV-013` | `InvalidStateTransitionException` → 409 | Only a `Suspended` point can be resumed. | `MonitoringPoint.cs:160` |
| `ENV-014` | `InvalidStateTransitionException` → 409 | Point already retired. | `MonitoringPoint.cs:170` |
| `ENV-404` | `DomainException` → 404 | Monitoring point not found. | `MonitoringSlice.cs:116` |
| `TENANT-000` | `DomainException` → 422 | No tenant context on a register command. | `EquipmentSlice.cs:36`, `ReferenceStandardsSlice.cs:42`, `MonitoringSlice.cs:37` |
| `AUTH-003` | `DomainException` → **401** (prefix rule) | Unauthenticated actor on intermediate check / reading. | `EquipmentSlice.cs:142`, `MonitoringSlice.cs:85` |
| `AUTHZ-000/001/002/008` | `DomainException` → **403** | Command-policy refusals. | `AuthorizationBehavior.cs:52,60,68,83` |
| `CONCURRENCY-409` | `DbUpdateConcurrencyException` → 409 | `xmin` token changed between read and write. | `DomainExceptionHandler.cs:21,28` |

Status-code mapping authority: `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:26–82` — `InvalidStateTransitionException` → **409**, `AUTH-*` → 401, `AUTHZ-*` → 403, `*-404` → 404, every other `DomainException` → **422**, all as `application/problem+json` with `extensions.code`.

### 1.4 Core calculations (verbatim)

| Behaviour | Expression | Line |
|---|---|---|
| Next-due calculation | `NextCalibrationDue = performedAt.AddDays(CalibrationIntervalDays)` | `EquipmentItem.cs:155` |
| Due detection | declines unless `Status == Active && NextCalibrationDue != null && NextCalibrationDue <= asOf` | `EquipmentItem.cs:167` |
| Grace lockout | declines unless `Status == NeedsCalibration && NextCalibrationDue != null`; then declines while `NextCalibrationDue.AddDays(GracePeriodDays) >= asOf` | `EquipmentItem.cs:179,184` |
| Return to service | any `LogCalibration` on a non-`Active` item sets `Active` and raises `EquipmentReturnedToService` | `EquipmentItem.cs:153–161` |
| Standard expiry | declines unless `Status == Active && ExpiresOn != null && ExpiresOn <= asOf` | `ReferenceStandard.cs:121` |
| Reading verdict | `inLimit = (LowLimit is null \|\| value >= LowLimit) && (HighLimit is null \|\| value <= HighLimit)` — **boundary values are in-limit** | `MonitoringPoint.cs:130` |

> **Derived grace arithmetic (important for BVA).** Because the guard is `due + grace >= asOf → decline`, lockout first becomes possible on `asOf = due + grace + 1`. With `GracePeriodDays = 0` the instrument is therefore still *not* locked out on the due date itself and locks out on `due + 1`: the effective grace is **G + 1 calendar days**, not G. All `TC-EQUIP-BVA-008…012` are written against this measured rule, not against the intuitive one.

### 1.5 Domain events and their consumers

| Event | Raised at | Consumer(s) |
|---|---|---|
| `CalibrationDue(EquipmentId, Code, Name, DueDate, TenantId)` | `EquipmentItem.cs:173` | `NotificationEventPolicies` key `EQUIP_CALIB_DUE` (`NotificationPolicies.cs:37,70`); outbox → audit ledger (`OutboxProcessor.cs:126`) |
| `EquipmentLockedOut(EquipmentId, Code, Name, TenantId)` | `EquipmentItem.cs:190` | key `EQUIP_LOCKED_OUT` (`NotificationPolicies.cs:38,78`); seeded rule subject `"EQUIPMENT LOCKED OUT: {ref}"` (`NotificationPolicies.cs:146`) |
| `EquipmentReturnedToService(EquipmentId, Code, TenantId)` | `EquipmentItem.cs:160` | **none** — no notification handler exists (GAP-EQUIP-014); reaches the audit ledger only |
| `EquipmentRetired(EquipmentId, Code, TenantId)` | `EquipmentItem.cs:249` | **none** — ledger only (GAP-EQUIP-014) |
| `IntermediateCheckFailed(...)` | `EquipmentItem.cs:234` | `IntermediateCheckToNcPolicy` → raises + submits an NC, severity 4 / likelihood 3, `SourceRef = "CHK:{checkId}"` (`IntermediateCheckToNcPolicy.cs:36–50`) |
| `ReferenceStandardQuarantined(...)` | `ReferenceStandard.cs:99` | ledger only |
| `ReferenceStandardExpired(...)` | `ReferenceStandard.cs:127` | key `REF_STD_EXPIRED` (`NotificationPolicies.cs:89`) |
| `EnvironmentalExcursionDetected(...)` | `MonitoringPoint.cs:138` | `ExcursionToNcPolicy` → NC severity 4 / likelihood 3, `SourceRef = "ENV:{readingId}"` (`ExcursionToNcPolicy.cs:41–53`) |

`ReferenceStandard.Retire()` (`ReferenceStandard.cs:130–138`) and every `MonitoringPoint` lifecycle method (`Suspend/Resume/Retire/SetLimits`) raise **no** event — see GAP-EQUIP-019 / GAP-EQUIP-020.

### 1.6 Endpoints (all dual-exposed as `/api/…` and `/api/v{version}/…`, `ApiSurface.approved.txt:59,81,105,307–311,342–347,393–396`)

| Method + route | Success status | HTTP permission gate | Command policy | Source |
|---|---|---|---|---|
| `GET /api/equipment?status=&page=&pageSize=` | 200 `PagedResponse<EquipmentListItemDto>` | **none beyond `[Authorize]`** | n/a — query | `EquipmentController.cs:16–21` |
| `GET /api/equipment/{id}` | 200 `EquipmentDetailDto` | **none beyond `[Authorize]`** | n/a — query | `EquipmentController.cs:23–25` |
| `POST /api/equipment` | **201** + `Location` + `{id}` | **none** | `[RequireInternalActor]` | `EquipmentController.cs:27–35`, `EquipmentSlice.cs:12` |
| `POST /api/equipment/{id}/calibrations` | **204** | **none** | `[RequireInternalActor]` | `EquipmentController.cs:37–44`, `EquipmentSlice.cs:56` |
| `POST /api/equipment/{id}/maintenance` | **204** | **none** | `[RequireInternalActor]` | `EquipmentController.cs:46–52`, `EquipmentSlice.cs:61` |
| `POST /api/equipment/{id}/checks` | **200** `{checkId}` | **none** | `[RequireInternalActor]` | `EquipmentController.cs:54–62`, `EquipmentSlice.cs:122` |
| `POST /api/equipment/{id}/retire` | 204 | `[RequirePermission(equipment, Void)]` → `equipment.void` | `[RequireInternalActor]` | `EquipmentController.cs:64–70` |
| `GET /api/reference-standards?status=` | 200 list | **none** | n/a | `ReferenceStandardsController.cs:17–19` |
| `GET /api/reference-standards/{id}` | 200 detail | **none** | n/a | `ReferenceStandardsController.cs:21–23` |
| `POST /api/reference-standards` | 201 | `reference-standards.create` | `[RequireInternalActor]` | `ReferenceStandardsController.cs:25–35` |
| `POST /api/reference-standards/{id}/quarantine` | 204 | `reference-standards.edit` | `[RequireInternalActor]` | `ReferenceStandardsController.cs:37–43` |
| `POST /api/reference-standards/{id}/reactivate` | 204 | `reference-standards.approve` | `[RequireInternalActor]` | `ReferenceStandardsController.cs:45–51` |
| `POST /api/reference-standards/{id}/retire` | 204 | `reference-standards.void` | `[RequireInternalActor]` | `ReferenceStandardsController.cs:53–59` |
| `GET /api/monitoring-points?status=` | 200 list | **none** | n/a | `MonitoringPointsController.cs:17–19` |
| `GET /api/monitoring-points/{id}` | 200 detail (last 100 readings) | **none** | n/a | `MonitoringPointsController.cs:21–23`, `MonitoringSlice.cs:157` |
| `POST /api/monitoring-points` | 201 | `monitoring-points.create` | `[RequireInternalActor]` | `MonitoringPointsController.cs:25–33` |
| `POST /api/monitoring-points/{id}/limits` | 204 | `monitoring-points.edit` | `[RequireInternalActor]` | `MonitoringPointsController.cs:35–41` |
| `POST /api/monitoring-points/{id}/readings` | **200** `{readingId}` | **none** | `[RequireInternalActor]` | `MonitoringPointsController.cs:43–45` |
| `POST /api/monitoring-points/{id}/suspend` | 204 | `monitoring-points.edit` | `[RequireInternalActor]` | `MonitoringPointsController.cs:47–53` |
| `POST /api/monitoring-points/{id}/resume` | 204 | `monitoring-points.edit` | `[RequireInternalActor]` | `MonitoringPointsController.cs:55–61` |
| `POST /api/monitoring-points/{id}/retire` | 204 | `monitoring-points.void` | `[RequireInternalActor]` | `MonitoringPointsController.cs:63–69` |
| `POST /api/files` (certificate upload) | 201 `{id, fileName, sha256, sizeBytes}` | none beyond `[Authorize]`; 50 MiB cap | n/a — controller-direct | `FilesController.cs:20–57` |

### 1.7 Permissions in the catalogue vs permissions actually enforced

`PermissionCatalog.cs:92–94` defines the module keys `equipment`, `reference-standards`, `monitoring-points`; `:159–161` registers all three with the `FullRecordLifecycle` bundle = `View, Create, Edit, Approve, Void, Export` (conventions §2). That is **18 catalogued keys** for this module group. Only **five** are enforced anywhere: `equipment.void`, `reference-standards.create/edit/approve/void`, `monitoring-points.create/edit/void` (eight, counting all three modules). The catalogued keys `equipment.view/create/edit/approve/export`, `reference-standards.view/export`, `monitoring-points.view/approve/export` have **no call site** — GAP-EQUIP-011.

`[RequireInternalActor]` (`AuthorizationBehavior.cs:75`) permits **every authenticated role except `UserRole.ExternalAuditor`**. It is therefore *not* a capability gate: an `Analyst` with no equipment privileges can create equipment, log calibrations and record readings.

### 1.8 Validators (FluentValidation)

| Command | Rules | Line |
|---|---|---|
| `RegisterEquipmentCommand` | `Name` NotEmpty ≤200; `SerialNumber` NotEmpty ≤100; `CalibrationIntervalDays` `InclusiveBetween(1, 3650)`; `GracePeriodDays` `InclusiveBetween(0, 365)` | `EquipmentSlice.cs:18–27` |
| `LogCalibrationCommand` | **NO VALIDATOR EXISTS** | — (GAP-EQUIP-009) |
| `LogMaintenanceCommand` | `WorkDescription` NotEmpty ≤2000 | `EquipmentSlice.cs:66–72` |
| `RecordIntermediateCheckCommand` | `CheckType` NotEmpty ≤200; `Remarks` ≤2000 | `EquipmentSlice.cs:127–134` |
| `RegisterReferenceStandardCommand` | `Name` ≤300; `TraceableTo` NotEmpty ≤500; `Manufacturer` ≤200; `LotNumber` ≤100; `CertificateNumber` ≤100; `CertifiedValue` ≤200; `UncertaintyStatement` ≤200 | `ReferenceStandardsSlice.cs:21–33` |
| `QuarantineReferenceStandardCommand` | `Reason` NotEmpty ≤1000 | `ReferenceStandardsSlice.cs:63–69` |
| `RegisterMonitoringPointCommand` | `Name` NotEmpty ≤200; `Location` ≤200; `Parameter` NotEmpty ≤100; `Unit` NotEmpty ≤30 | `MonitoringSlice.cs:19–28` |
| `RecordReadingCommand` | `Remark` ≤1000 | `MonitoringSlice.cs:60–66` |
| `SetMonitoringLimitsCommand` | **NO VALIDATOR** — domain `ENV-003/004` is the only guard | `MonitoringSlice.cs:50` |

Validation failures surface as **HTTP 400** with `extensions.errors` keyed by property (`DomainExceptionHandler.cs:34–44`).

### 1.9 Persistence, keys, uniqueness and RLS

| Object | Fact | Source |
|---|---|---|
| `qams.equipment_item` PK | **tenant-first composite** `(tenant_id, id)` | `ResourceConfigurations.cs:13`; `Hardening5_CompositeKeys.cs:1088–1092` |
| **Serial-number uniqueness** | `CREATE UNIQUE INDEX ix_equipment_item_tenant_id_serial_number ON (tenant_id, serial_number)` — **PER TENANT, not global.** Two tenants may hold the same serial; one tenant may not. | `ResourceConfigurations.cs:22`; `ResourcesModules.cs:188–193` |
| Code uniqueness | unique `(tenant_id, code)`; code is issued as `EQP-{yyyy}-{0000}` by `PostgresReferenceNumberGenerator` (atomic `INSERT … ON CONFLICT … RETURNING` per `(tenant, ref_type, year)`) | `ResourceConfigurations.cs:21`; `RefCounter.cs:24–44` |
| Owned children | `calibration_record`, `maintenance_record`, `intermediate_check` — each keyed `(TenantId, Id)`, owner FK `("TenantId","equipment_id")` | `ResourceConfigurations.cs:25–61` |
| Child tenant-composite FKs | `fk_calibration_record_equipment_item_tenant FOREIGN KEY (equipment_id, tenant_id) REFERENCES qams.equipment_item (id, tenant_id) ON DELETE CASCADE` (+ the three siblings) | `Hardening4_ChildTenancy.cs:361–362, 376–377, 385–386, 394–395` |
| Child RLS | `ENABLE` + `FORCE` + `tenant_isolation` `FOR ALL USING/WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant',true),'')::uuid OR current_setting('app.bypass_rls',true)='on')` on `calibration_record`, `environmental_reading`, `intermediate_check`, `maintenance_record` | `Hardening4_ChildTenancy.cs:465–468, 510–513, 537–540, 564–567` |
| Root RLS (original, later hardened) | `equipment_item` `ResourcesModules.cs:214–217`; `reference_standard` `MetrologicalTraceability.cs:92–96`; `monitoring_point` `EnvironmentalMonitoring.cs:85–89` |
| CHECK constraints | `ck_equipment_interval_positive (calibration_interval_days > 0)`, `ck_equipment_grace_nonnegative (grace_period_days >= 0)` | `Phase5CheckConstraints.cs:44–49` |
| Concurrency token | `xmin` (no `row_version` column) — conventions §2 | — |
| EF global filter | `ITenantScoped` → tenant filter; `IAllocatable` → **tenant AND branch/department working-scope** composed filter | `AppDbContext.cs:168–182, 200–210` |

> **Uniqueness verdict, stated explicitly as the brief demands.** Serial-number uniqueness in this build is **per tenant**. The DB index is `(tenant_id, serial_number)`; the tenant-composite PK `(tenant_id, id)` makes any global `UNIQUE(id)`/`UNIQUE(serial_number)` deliberately absent (partition-readiness, URS-103). The application pre-check `db.EquipmentItems.AnyAsync(e => e.SerialNumber == serial)` (`EquipmentSlice.cs:39`) runs through the **composed tenant+scope** filter, so for a branch-restricted actor it is *narrower than the index* — see GAP-EQUIP-010 and `TC-EQUIP-SEC-007`.

### 1.10 Scheduled sweep (`src/NT.QAMS.Infrastructure/Jobs/ScheduledSweepService.cs`)

| Fact | Value | Line |
|---|---|---|
| Type | `BackgroundService` (no Hangfire — conventions §1) | `:24` |
| Interval | `TimeSpan.FromHours(1)` — the XML doc says "daily compliance sweep"; the code is hourly | `:15,29` |
| Startup delay | 15 s | `:34` |
| Cross-tenant read | `ICurrentTenantSetter.Elevate()` then `IgnoreQueryFilters()` | `:64, 91, 98` |
| Leader election | `AdvisoryLock.TryRunExclusiveAsync(db, AdvisoryLockKeys.ComplianceSweep, …)`; non-leader returns `ran == false` and skips | `:70–76` |
| "As of" date | `DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)` — **UTC calendar day, no tenant time zone** | `:88` |
| Calibration-due pass | `Status == Active && NextCalibrationDue != null && NextCalibrationDue <= today` → `MarkCalibrationDue(today)` | `:90–95` |
| Grace-lockout pass | `Status == NeedsCalibration && NextCalibrationDue != null` → `LockOutIfGraceExhausted(today)`; `locked` counted after | `:97–102` |
| Standard-expiry pass | `Status == Active && ExpiresOn != null && ExpiresOn <= today` → `MarkExpiredIfReached(today)` | `:128–133` |
| Persistence | one `SaveChangesAsync` for the whole round | `:150` |
| Liveness metric | `QamsMetrics.RecordJobSuccess("compliance-sweep", now)` only when the round actually ran | `:80` |
| Failure handling | catch-all logs `LogSweepFailed` and continues to the next interval | `:50–53, 158` |

> **Two-pass artefact (measured, not assumed).** The lockout pass issues a **fresh SQL query** after the due pass has mutated only the in-memory entities. An instrument that is `Active` in the database and already past `due + grace` is therefore marked `NeedsCalibration` in round *n* and can only be locked out in round *n+1* — a one-interval (≈1 h) deferral. `TC-EQUIP-LOOP-001` and GAP-EQUIP-018 record this.

### 1.11 What the equipment status is actually used for

Exhaustive consumer list (grep over `src/`, excluding migrations): `ReportingQueries.cs:42–45` (dashboard `OutOfService` / `NeedsCalibration` counters), `KpiSnapshotService.cs:118` (`EquipmentOutOfService` snapshot), `ScheduledSweepService.cs:92,99,102`, the equipment list/detail projections, and the Angular list/detail components. **No code path anywhere consults `EquipmentStatus` before allowing an instrument to be used, selected, or attached to a result, a QC run, an analytical study or a test authorization** — GAP-EQUIP-004.

### 1.12 Frontend surface

| Feature | Facts | Source |
|---|---|---|
| Equipment register | server-side `status` filter; **client-side** free-text search over `code name serialNumber status location`; branch filter; live stat tiles Active / NeedsCalibration / OutOfService | `frontend/src/app/features/equipment/equipment-list.component.ts:33–38, 124–140, 160–161` |
| Load-more pager | 1-based page, appends, honours `hasMore` from the API-004 envelope | `equipment.facade.ts:50–60` |
| Equipment workspace | workflow stepper `NeedsCalibration → Active → Retired`; calibration form (`performedAt` required, `provider` ≤200, `result` required ≤500, file input); maintenance form; intermediate-check form with active-standards-only dropdown; retire button gated on `perms.can('equipment.void')`; `<qams-audit-trail [subject]="e.id">` | `equipment-detail.component.ts:33, 40–42, 56–64, 110–139, 142, 175, 180–199` |
| Certificate flow | file uploaded to `POST /api/files` **first**, then its id posted with the calibration | `equipment.facade.ts:70–78` |
| Upload allow-list | `.pdf` accepted on extension **and** magic bytes `25 50 44 46`; canonical stored type `application/pdf`; client `Content-Type` never trusted; refusal code `FILE-415` | `WebApi/Security/FileContentPolicy.cs:24, 44–52`; `FilesController.cs:41–47` |
| LOVs | `EQUIPMENT_LOCATION` (6 seeds), `INTERMEDIATE_CHECK_TYPE` (5 seeds), `ENV_PARAMETER` (4 seeds) | `Application/Organization/DefaultLovCatalog.cs:65, 74, 82` |

### 1.13 Existing automated coverage (baseline this package must not duplicate blindly)

`tests/NT.QAMS.Domain.UnitTests/Equipment/IntermediateCheckTests.cs` (56 lines), `…/Equipment/ReferenceStandardTests.cs` (77), `…/Facility/MonitoringPointTests.cs` (85). **There is no `EquipmentItemTests` file** — the calibration-due, grace-lockout, return-to-service and retire transitions of the central aggregate have **no dedicated domain unit test** in the baseline. There is no functional test for any of the 21 equipment/standard/monitoring endpoints (`tests/NT.QAMS.WebApi.FunctionalTests/` contains only `RefreshSessionTests.cs` plus the shared harness files). This is the single largest coverage hole in the module and drives the `Automation Candidate: Yes` marking on most cases below.

---

## 2. Divergences from the commissioning brief

| # | Commissioning brief asks for | Actually implemented | Verdict | Gap |
|---|---|---|---|---|
| D-01 | Electronic signature on the calibration record | `IESignatureService.SignAsync` is invoked from exactly one place — document approval (`DocumentCommands.cs:154`). `LogCalibrationHandler` never signs. | Does not conform | GAP-EQUIP-001 |
| D-02 | Measured deviation compared against acceptance limits | `CalibrationRecord.Result` is one free-text `varchar(500)`. No numeric measurement, no limits, no pass/fail evaluation. | Does not conform | GAP-EQUIP-002 |
| D-03 | Service agency and certificate number on the calibration | `Provider` free text `varchar(200)`; **no certificate-number column** on `calibration_record`; no supplier/agency link. | Partially conforms (free-text provider only) | GAP-EQUIP-003 |
| D-04 | Overdue equipment is blocked from selection | Status is read only by dashboard counters and the sweep. No selection gate anywhere. | Does not conform | GAP-EQUIP-004 |
| D-05 | Calibration **and maintenance scheduling** | One derived `NextCalibrationDue`; **no maintenance schedule, no plan entity, no frequency** for maintenance. | Partially conforms (calibration only) | GAP-EQUIP-005 |
| D-06 | Maintenance statuses and return-to-service | `LogMaintenance` appends a record and changes nothing; `MaintenanceRecord` has no status. Return-to-service is a side effect of `LogCalibration` only. | Partially conforms | GAP-EQUIP-006 |
| D-07 | Model info (manufacturer / model / model number) | Absent from `EquipmentItem`. Present on `ReferenceStandard` (`Manufacturer`, `LotNumber`). | Does not conform | GAP-EQUIP-007 |
| D-08 | Privilege `EQUIP.CALIB_SCHED` | Does not exist. Real keys are `equipment.{view,create,edit,approve,void,export}` and only `equipment.void` is enforced. | Naming divergence + enforcement gap | GAP-EQUIP-017, GAP-EQUIP-011 |
| D-09 | Dashboard visibility of overdue calibration | Implemented as two counters (`EquipmentOutOfService`, `EquipmentNeedsCalibration`) plus KPI snapshot. No overdue *list*, no drill-down. | Partially conforms | — (case authored `[ID]`) |
| D-10 | Advance notification before calibration falls due | Notification fires when the item is **already** due (`CalibrationDue` on `due <= today`). No T-30 / T-7 ladder. | Does not conform | GAP-EQUIP-013 |
| D-11 | Full traceability chain to the SI | Single free-text `TraceableTo varchar(500)`; standards link to **intermediate checks** only, never to a `CalibrationRecord`. | Partially conforms | GAP-EQUIP-015 |
| D-12 | Hangfire scheduled jobs | `BackgroundService` `ScheduledSweepService`, 1-hour interval, advisory-lock leader election. | Architecture divergence (conventions §1) — test the implemented mechanism | — |
| D-13 | Daily sweep | Interval is `FromHours(1)`; the XML doc comment says "daily". | Doc/code divergence | GAP-EQUIP-018 (note) |
| D-14 | Granular equipment requirements | The whole module traces to **one** URS sentence, URS-051. | Traceability gap | GAP-EQUIP-022 |

---

## 3. State-transition matrices

### 3.1 `EquipmentStatus` (`EquipmentItem.cs:6`, transitions at `:132, 153–156, 172, 189, 248`)

| From \ Trigger | `Register` | `LogCalibration` | `MarkCalibrationDue(asOf)` | `LockOutIfGraceExhausted(asOf)` | `LogMaintenance` | `RecordIntermediateCheck` | `Retire` |
|---|---|---|---|---|---|---|---|
| *(none)* | → **NeedsCalibration** | — | — | — | — | — | — |
| **NeedsCalibration** | — | → **Active** + `EquipmentReturnedToService` | no-op (guard `Status != Active`) | → **OutOfService** + `EquipmentLockedOut` **iff** `due + grace < asOf`; else no-op | stays NeedsCalibration | stays NeedsCalibration | → **Retired** + `EquipmentRetired` |
| **Active** | — | → **Active** (no event; `wasOutOfUse == false`) | → **NeedsCalibration** + `CalibrationDue` iff `due <= asOf`; else no-op | no-op (guard) | stays Active | stays Active | → **Retired** + `EquipmentRetired` |
| **OutOfService** | — | → **Active** + `EquipmentReturnedToService` | no-op (guard) | no-op (guard) | stays OutOfService | stays OutOfService | → **Retired** + `EquipmentRetired` |
| **Retired** | — | **`EQP-010` / 409** | no-op (guard) | no-op (guard) | **`EQP-012` / 409** | **`EQP-020` / 409** | **`EQP-014` / 409** |

Unreachable/absent transitions (assert as negative evidence): `OutOfService → NeedsCalibration`, `Retired → *` (no un-retire path exists), and there is **no API** that invokes `MarkCalibrationDue` or `LockOutIfGraceExhausted` — the sweep is their only caller.

### 3.2 `ReferenceStandardStatus` (`ReferenceStandard.cs:8`)

| From \ Trigger | `Register` | `Quarantine(reason)` | `Reactivate(asOf)` | `MarkExpiredIfReached(asOf)` | `Retire` |
|---|---|---|---|---|---|
| *(none)* | → **Active** | — | — | — | — |
| **Active** | — | → **Quarantined** + event | **`RS-012` / 409** | → **Expired** + event iff `ExpiresOn <= asOf` | → **Retired** (no event) |
| **Quarantined** | — | **`RS-010` / 409** | → **Active**, `QuarantineReason = null`; **`RS-013` / 422** if `ExpiresOn <= asOf` | no-op (guard `Status != Active`) | → **Retired** (no event) |
| **Expired** | — | **`RS-010` / 409** | **`RS-012` / 409** | no-op | → **Retired** (no event) |
| **Retired** | — | **`RS-010` / 409** | **`RS-012` / 409** | no-op | **`RS-014` / 409** |

Note the asymmetry: `Expired` is terminal-but-for-retirement — there is **no** un-expire path even if a new certificate arrives; the documented remedy is "register a replacement standard" (`ReferenceStandard.cs:111`).

### 3.3 `MonitoringPointStatus` (`MonitoringPoint.cs:6`)

| From \ Trigger | `Register` | `RecordReading` | `SetLimits` | `Suspend` | `Resume` | `Retire` |
|---|---|---|---|---|---|---|
| *(none)* | → **Active** (limits validated by `SetLimits` at `:93`) | — | — | — | — | — |
| **Active** | — | appends reading; excursion event if out of limits | re-baselines | → **Suspended** | **`ENV-013` / 409** | → **Retired** |
| **Suspended** | — | **`ENV-011` / 409** | re-baselines (allowed) | **`ENV-012` / 409** | → **Active** | → **Retired** |
| **Retired** | — | **`ENV-011` / 409** | **`ENV-010` / 409** | **`ENV-012` / 409** | **`ENV-013` / 409** | **`ENV-014` / 409** |

---

## 4. Decision tables

### 4.1 DT-A — `EquipmentItem.MarkCalibrationDue(asOf)` (`EquipmentItem.cs:165–174`)

| Rule | `Status == Active` | `NextCalibrationDue != null` | `NextCalibrationDue <= asOf` | Outcome |
|---|---|---|---|---|
| A1 | T | T | T | `Status → NeedsCalibration`, raise `CalibrationDue` |
| A2 | T | T | F | no-op (proposal declined) |
| A3 | T | F | – | no-op |
| A4 | F | – | – | no-op |

### 4.2 DT-B — `EquipmentItem.LockOutIfGraceExhausted(asOf)` (`EquipmentItem.cs:177–191`)

| Rule | `Status == NeedsCalibration` | `NextCalibrationDue != null` | `due + grace >= asOf` | Outcome |
|---|---|---|---|---|
| B1 | T | T | F | `Status → OutOfService`, raise `EquipmentLockedOut` |
| B2 | T | T | T | no-op (still within grace) |
| B3 | T | F | – | no-op — **an instrument never calibrated can never be locked out** (GAP-EQUIP-008) |
| B4 | F | – | – | no-op |

### 4.3 DT-C — `MonitoringPoint.RecordReading` in-limit verdict (`MonitoringPoint.cs:130`)

| Rule | `LowLimit` | `HighLimit` | `value` vs low | `value` vs high | `inLimit` | Event |
|---|---|---|---|---|---|---|
| C1 | null | set | – | `<= high` | T | none |
| C2 | null | set | – | `> high` | F | `EnvironmentalExcursionDetected` |
| C3 | set | null | `>= low` | – | T | none |
| C4 | set | null | `< low` | – | F | excursion |
| C5 | set | set | `>= low` | `<= high` | T | none |
| C6 | set | set | `< low` | `<= high` | F | excursion |
| C7 | set | set | `>= low` | `> high` | F | excursion |
| C8 | null | null | – | – | *unreachable* — `ENV-003` forbids registering a point with both limits null (`:107`) | n/a |

### 4.4 DT-D — `RecordIntermediateCheckHandler` standard validation (`EquipmentSlice.cs:139–161`)

| Rule | `ReferenceStandardId` supplied | Standard exists | Standard `Status` | Outcome |
|---|---|---|---|---|
| D1 | no | – | – | Proceed; `reference_standard_id` NULL |
| D2 | yes | no | – | `RS-404` → **404** |
| D3 | yes | yes | `Active` | Proceed |
| D4 | yes | yes | `Quarantined` | `RS-020` → **422** |
| D5 | yes | yes | `Expired` | `RS-020` → **422** |
| D6 | yes | yes | `Retired` | `RS-020` → **422** |

### 4.5 DT-E — Calibration certificate upload (`FilesController.cs:26–57`, `FileContentPolicy.cs:44–75`)

| Rule | Extension on allow-list | Magic bytes match | Size | Outcome |
|---|---|---|---|---|
| E1 | `.pdf` | `25 50 44 46` | ≤ 50 MiB, > 0 | **201**, stored `application/pdf` |
| E2 | `.pdf` | mismatched (e.g. `4D 5A`) | any | `FILE-415` → **422** |
| E3 | `.exe` | – | any | `FILE-415` → **422** |
| E4 | any allowed | matching | 0 bytes | **400** `ValidationProblem` "A non-empty file is required." |
| E5 | any allowed | matching | > 50 MiB | **413** (`[RequestSizeLimit]`) |

<!--APPEND-->
