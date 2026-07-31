# Requirements Traceability Matrix (RTM)

| Field | Value |
| ----- | ----- |
| Document ID | RTM-NTQMS-001 |
| System | NT.QMS |
| Version | 1.2 (2026-08-01 — URS-005/009 revised for the v1.51 Role Privilege module; URS-008/011/012 revised for the v1.51.2 schema-hardening programme; post-baseline requirements cross-referenced) |
| Parent | VMP / URS / FRA / QP |

Each URS traces to a **design element** (aggregate / controller / interceptor / migration)
and a **verification method** (automated test project/spec and/or OQ/PQ case). File paths are
relative to the repository root. "Verification status" reflects development-phase evidence;
formal execution status is completed at OQ/PQ execution.

Legend — Verification: **AUTO** = automated test; **OQ/PQ** = scripted qualification case;
**IQ** = installation check; **INSP** = inspection of code/config.

---

## A. Security & Access Control

| URS | Design element(s) | Verification | Status |
| --- | ----------------- | ------------ | ------ |
| URS-001 | `AuthController` `POST /api/auth/login`; `UserAccount`; JWT infra | AUTO `WebApi.FunctionalTests/AuthActorFunctionalTests`; OQ-SEC-01/02 | Verified (dev) |
| URS-002 | `Application/IdentityAccess/PasswordRules.cs`; RegisterUser/ResetUserPassword/ChangePassword | AUTO `Application.UnitTests/IdentityAccess/PasswordRulesTests`; OQ-SEC-04 | Verified (dev) |
| URS-003 | `UserAccount` (MaxFailedAttempts=5, LockoutMinutes=30, `RegisterFailedLogin`/`IsLockedOut`) | AUTO `Domain.UnitTests/IdentityAccess/UserAccountTests`; OQ-SEC-03 | Verified (dev) |
| URS-004 | `AuthController` `mfa/enroll`,`mfa/confirm`; `MfaEnrollmentGateMiddleware`; `TenantSettings.RequireMfaForPrivilegedRoles`; `TenantSettingsController` mfa-policy; TOTP infra | OQ-SEC-05; INSP (F-04) | Verified (dev); UI witnessed at OQ |
| URS-005 | **Revised at v1.51 (Role Privilege module):** `Domain/Authorization/PermissionCatalog.cs` + `Role` aggregate; `[RequirePermission(module, action)]` across all tenant controllers (replaced `[Authorize(Roles=…)]`); `[RequirePermissionPolicy]` command policies; seeded system roles reproduce the former tiers (`SystemRoleCatalog`). `UserRole` enum retained only as the platform/tenant structural tier; `Roles.cs` governs the platform surface only. | AUTO `WebApi.FunctionalTests/RolePrivilegeFlowTests` + `RoleEndpointMatrixTests`; OQ-RP-04/07 **executed** (doc 12); OQ-SEC-06/07 superseded | Verified (dev); OQ executed, unsigned |
| URS-006 | `ActiveSessionMiddleware` (`WebApi/Middleware/RequestIdentity.cs`) | OQ-SEC-08/09 (functional test: deactivate → 401) | Verified (dev) |
| URS-007 | Frontend `auth.service` idle timeout (30 min) | OQ (UI) — witnessed | Template |
| URS-008 | `TenantConnectionInterceptor`; migration `ActivateForcedTenantRls`; `ICurrentTenant.IsElevated`; RLS policies. **Extended at v1.51.2:** the fence now covers the 2 remaining gap tables and all 30 owned child tables — 90 FORCE-RLS tables, 0 parity violations — and cross-tenant children are structurally impossible via tenant-composite FKs (`Hardening2`/`Hardening4`). Two tables keep a documented permanent exception (nullable tenant). | AUTO `IntegrationTests/RlsTenantIsolationTests` + `OwnedChildTenancyTests` + `SecurityEventRlsTests` (real PG); `Architecture.Tests/UserAccountTenantBoundTests` (guards the exception); OQ-ISO-01..04, **OQ-DB-01/02**; IQ-05/06/07, **IQ-27** | Verified (dev); A.10 cases Template |
| URS-009 | `UsersController` `/api/users`; `UserAccount` — **extended at v1.51** with role assignment (`assigned-role`), working scope (`scope`, hard data filter) and language (`language`), gated by `users.manage` | AUTO `Domain.UnitTests/IdentityAccess/UserRoleAndResetTests` + `UserScopeTests`; OQ-RP-06 **executed** (doc 12) | Verified (dev) |
| URS-010 | `UserAccessReview` (`Domain/IdentityAccess`); `AccessReviewsController` `/api/access-reviews` | AUTO `Domain.UnitTests/IdentityAccess/UserAccessReviewTests`; OQ-SEC-10 | Verified (dev) |

## B. Audit Trail & Data Integrity

| URS | Design element(s) | Verification | Status |
| --- | ----------------- | ------------ | ------ |
| URS-011 | `FieldChangeRecord` (`ComplianceLedger/LedgerEntries.cs`); `FieldChangeInterceptor`. **Corrected at v1.51.2:** `TenantOf` now reads an owned child's shadow `TenantId`, so a child's change reaches the owning tenant's own view (it previously landed unattributed); `RenderKey` excludes the tenant so `entity_id` remains the record identity after keys became composite. | AUTO `WebApi.FunctionalTests/FieldChangeInterceptorTests` (incl. the owned-child elevated-write pin); OQ-AUD-01, **OQ-DB-07** | Verified (dev); A.10 case Template |
| URS-012 | `AuditTrailEntry`; `LedgerHash.Compute` (SHA-256 chain). **v1.51.2:** the ledger's primary key became tenant-first for partition-readiness and the five hash columns gained `^[0-9a-f]{64}$` format constraints; the chain columns themselves are unchanged in type and the chain re-verified intact (`ok: true`) after the re-key. | AUTO `Application.UnitTests/Compliance/ComplianceHardeningTests`; INSP live chain verification; OQ-AUD-02, **OQ-DB-03/04** | Verified (dev); A.10 cases Template |
| URS-013 | `ComplianceController` `GET /api/compliance/chain-verification`; `VerifyChainQuery` | OQ-AUD-02; AUTO compliance tests | Verified (dev) |
| URS-014 | `deploy/harden-runtime-role.sql` (append-only grants); append-only trigger design | IQ-08; OQ-AUD-03; INSP | Verified (dev) via role grants |
| URS-015 | `FieldChangeRecord.Reason`; `ChangeReasonMiddleware`; `ICurrentChangeReason(Setter)` | AUTO `FieldChangeInterceptorTests` (reason); OQ-AUD-04/05 | Verified (dev) |
| URS-016 | `SecurityEvent`; `SecurityEventLog`; `ComplianceController` `GET /security-events` | AUTO compliance tests; OQ-SEC-03, OQ-SIG-02..04 | Verified (dev) |
| URS-017 | `ExportsController` `/api/exports` (audit-trail.xlsx + Integrity Attestation); `ComplianceController` `GET /audit-trail`,`/field-changes` | AUTO `Application.UnitTests/Compliance/ExportServiceTests`; OQ-AUD-06 | Verified (dev) |
| URS-018 | `AuditTrailReview` (`ComplianceLedger`); `ComplianceController` `audit-trail-reviews`(+`/complete`) | AUTO `Domain.UnitTests/ComplianceLedger/AuditTrailReviewTests`; OQ-AUD-07 | Verified (dev) |
| URS-019 | `FieldChangeInterceptor` (credential redaction at capture) | AUTO `FieldChangeInterceptorTests`; OQ-AUD-08 | Verified (dev) |

## C. Electronic Signatures

| URS | Design element(s) | Verification | Status |
| --- | ----------------- | ------------ | ------ |
| URS-020 | `ESignatureService.SignAsync` (verifies password **and** PIN); `AuthController` `signature-pin` | AUTO `Application.UnitTests/Compliance/ComplianceHardeningTests`; OQ-SIG-01/02/03 | Verified (dev) |
| URS-021 | `SignatureRecord` (signer, meaning, subject, content hash, timestamp) | AUTO compliance tests; OQ-SIG-01/05 | Verified (dev) |
| URS-022 | `SignatureRecord` append-only; `harden-runtime-role.sql` (no UPDATE/DELETE) | IQ-08; OQ-SIG-05; INSP | Verified (dev) |
| URS-023 | `ESignatureService` lockout + ESIGN_FAILED/ESIGN_LOCKED (F-08) | OQ-SIG-02/03/04 | Verified (dev) |
| URS-024 | `SignatureRecord.Meaning` captured per signing | OQ-SIG-05 | Verified (dev) |

## D. Document Control

| URS | Design element(s) | Verification | Status |
| --- | ----------------- | ------------ | ------ |
| URS-025 | `ControlledDocument` (`Domain/DocumentControl`); `DocumentsController` (submit/recommend/reject/publish/versions/retire) | AUTO `Domain.UnitTests/DocumentControl/ControlledDocumentTests`; OQ-DOC-01/02 | Verified (dev) |
| URS-026 | `DocumentsController` publish + `GET {id}/signatures`; `ESignatureService` | AUTO ControlledDocumentTests; OQ-DOC-01; PQ-02 | Verified (dev) |
| URS-027 | `ControlledDocument` versioning; `POST {id}/versions` | AUTO ControlledDocumentTests; OQ-DOC-03 | Verified (dev) |
| URS-028 | `DocumentAcknowledgement`; `DocumentsController` acknowledge / my-acknowledgement / acknowledgements | AUTO `Domain.UnitTests/DocumentControl/DocumentAcknowledgementTests`; OQ-DOC-04 | Verified (dev) |
| URS-029 | `DocumentControlledCopy` (`ControlledCopySlice`); `DocumentsController` controlled-copies(+close) | AUTO `Domain.UnitTests/DocumentControl/DocumentControlledCopyTests`; OQ-DOC-05 | Verified (dev) |

## E. Nonconformance / CAPA & Quality Events

| URS | Design element(s) | Verification | Status |
| --- | ----------------- | ------------ | ------ |
| URS-030 | `Nonconformance` (`Domain/Improvement`); `NonconformancesController` (full lifecycle) | AUTO `Domain.UnitTests/Improvement/NonconformanceTests`; OQ-NC-02; PQ-01 | Verified (dev) |
| URS-031 | `Nonconformance.Verify` (SOD-CAPA-002 when actor==RaisedBy); `VerifyNcHandler` | AUTO `NonconformanceTests`; OQ-SOD-01; PQ-01/7 | Verified (dev) |
| URS-032 | `QualityEventType` enum on `Nonconformance`; `GetNcsQuery` eventType filter | AUTO `NonconformanceTests`; OQ-NC-01 | Verified (dev) |
| URS-033 | `Complaint`, `FeedbackEntry`; `ComplaintsController`, `FeedbackController` | AUTO `Domain.UnitTests/Improvement/ComplaintTests`,`FeedbackEntryTests`; OQ | Verified (dev) |
| URS-034 | `Application.UnitTests/AnalyticalQuality/PtToNcPolicyTests`; `AuditManagement/FindingToNcPolicyTests` | AUTO (both policy tests); OQ-QC-07 | Verified (dev) |

## F. Analytical Quality / QC

| URS | Design element(s) | Verification | Status |
| --- | ----------------- | ------------ | ------ |
| URS-035 | `WestgardEvaluator`; `QcProfile`; `AnalyticalQualityControllers` `/api/qc` runs | AUTO `Domain.UnitTests/AnalyticalQuality/WestgardEvaluatorTests`,`QcProfileTests`; OQ-QC-01; PQ-03 | Verified (dev) |
| URS-036 | `WestgardLimits` record; config `AnalyticalQuality:Westgard:*`; DI startup validation (F-16) | AUTO WestgardEvaluatorTests; OQ-QC-05 | Verified (dev) |
| URS-037 | `QcProfile.UpdateTargets` (QC-012 reason, QC-013 forward-only, effective dating); `PUT /api/qc/profiles/{id}/targets` | AUTO `QcProfileTests`; OQ-QC-02/03/04 | Verified (dev) |
| URS-038 | Analytical study aggregates (Precision, Linearity, DetectionLimit, MethodComparison, LotComparison, InstrumentComparability, Interference, Carryover, ReferenceInterval, OutlierScreening, Sigma, Uncertainty, ValidationStudy) + respective controllers | AUTO `Domain.UnitTests/AnalyticalQuality/*` (10 study specs); OQ-QC-06 | Verified (dev) |
| URS-039 | `AggregateRoot.EnsureSignerIsNotPreparer` (SOD-AQ-001) on 14 analytical sign-offs | AUTO `Domain.UnitTests/AnalyticalQuality/AnalyticalSodTests`; OQ-SOD-02 | Verified (dev) |
| URS-040 | `PtPlan`, `PtEnrollment`; `PtPlansController`, `/api/proficiency-tests` | AUTO `Domain.UnitTests/AnalyticalQuality/PtPlanTests`; OQ-QC-07 | Verified (dev) |
| URS-041 | Migration `SignedRecordImmutability` (`qams.reject_frozen_mutation()` on 12 roots + uncertainty_budget) | AUTO `IntegrationTests/SignedRecordImmutabilityTests` (real PG); OQ-IMM-01..04; IQ-09 | Verified (dev) |

## G. Records & Retention

| URS | Design element(s) | Verification | Status |
| --- | ----------------- | ------------ | ------ |
| URS-042 | `deploy/harden-runtime-role.sql` (REVOKE DELETE); runtime role `qams_app` | IQ-08; OQ-REC-01; INSP | Verified (dev) via grants |
| URS-043 | `ArchiveEntry.Archive` (mandatory `snapshotFileId`, ARC-002); `OperationsControllers` `/api/archives` | AUTO `Domain.UnitTests/Operations/RecordsAndSlaTests`; OQ-REC-02 | Verified (dev) |
| URS-044 | `ArchiveEntry.PlaceLegalHold`/`ReleaseLegalHold` (ARC-015/030/031/032); `/api/archives/{id}/legal-hold` | AUTO RecordsAndSlaTests; OQ-REC-03/04 | Verified (dev) |
| URS-045 | `ExportsController` `/api/exports` (XLSX); retention config | AUTO `Application.UnitTests/Compliance/ExportServiceTests`; OQ-AUD-06, OQ | Verified (dev) |
| URS-046 | `FileReference` (`Domain/Files`); `FilesController` `/api/files` | AUTO `WebApi.FunctionalTests/ChildEntityPersistenceTests`; OQ; INSP | Verified (dev) |

## H. Governance

| URS | Design element(s) | Verification | Status |
| --- | ----------------- | ------------ | ------ |
| URS-047 | `ChangeAndReview` (`Domain/RiskGovernance`), ChangeStatus incl. Reviewed/PIR; `GovernanceControllers` `/api/changes` | AUTO `Domain.UnitTests/Governance/GovernanceAndSupplierTests`; OQ-GOV-01 | Verified (dev) |
| URS-048 | `RiskItem`; `/api/risks` | AUTO GovernanceAndSupplierTests; OQ-GOV-03 | Verified (dev) |
| URS-049 | `QualityPolicy` (`Domain/Improvement`); `EnsureSignerIsNotPreparer` (SOD-QP-001); `QualityPolicyController` | AUTO `Domain.UnitTests/Improvement/QualityPolicyTests`; OQ-SOD-03, OQ-GOV-02 | Verified (dev) |
| URS-050 | `Supplier`; management reviews; `/api/suppliers`, `/api/management-reviews` | AUTO GovernanceAndSupplierTests; OQ-GOV-03 | Verified (dev) |
| URS-051 | `QualityObjective`, `ConflictDeclaration`, `EquipmentItem`/`ReferenceStandard`, `CompetencyRecord`/`TestAuthorization`, `MonitoringPoint` + respective controllers | AUTO `Domain.UnitTests/{Improvement/QualityObjectiveTests, RiskGovernance/ConflictDeclarationTests, Resources/EquipmentAndCompetencyTests, Equipment/*, Competency/TestAuthorizationTests, Facility/MonitoringPointTests}`; OQ | Verified (dev) |
| URS-052 | `KpiSnapshot` (`Domain/Reporting`); `ReportsController` `/api/reports` | AUTO (reporting/KPI); OQ | Verified (dev) |

## I. Backup / Disaster Recovery

| URS | Design element(s) | Verification | Status |
| --- | ----------------- | ------------ | ------ |
| URS-053 | `deploy/BACKUP-RESTORE-DR.md`; `deploy/backup.sh` (WAL + nightly dump) | IQ-13; PQ-04 (restore drill) | Template (config) |
| URS-054 | `deploy/BACKUP-RESTORE-DR.md` (encryption, off-site, retention); `deploy/DEV-SECRETS.md` | IQ-11/13; INSP | Template (config) |
| URS-055 | `deploy/restore.sh`; DR runbook restore-drill cadence | PQ-04 | Template (drill) |

---

## Post-baseline requirements (change program v1.38 → v1.51.1)

Requirements **URS-056 … URS-099** were introduced after the validated 1.0 baseline and
are maintained, with full trace rows, in **REVAL-NTQMS-001 Part A** (doc 06, rev 4):
A.1–A.6 hardening phases, A.7 supply chain, A.8 sign-in/statistics, **A.9 Role Privilege
module (URS-095…099, qualified by executed record OQ-EXEC-NTQMS-002 / doc 12, unsigned)**, and
**A.10 database schema hardening (URS-100…107, engineering-verified; OQ cases OQ-DB-01…08 are
Template — no witnessed session has been run)**.
This matrix deliberately does not duplicate those rows — doc 06 is their single source of
truth until the next full RTM consolidation.

---

## Coverage Summary

- **URS requirements (baseline):** 55 (URS-001 … URS-055); post-baseline URS-056…107 traced in doc 06 Part A.
- **Traced to a design element:** 55 / 55 (100%).
- **Traced to a verification method:** 55 / 55 (100%).
- **Backed by automated tests (dev evidence):** 47 / 55.
- **Verified by IQ/config/inspection or pending formal OQ/PQ execution:** URS-007 (UI idle
  timeout), URS-014/022/042 (grant-based, confirmed at IQ), URS-053/054/055 (backup/DR —
  configuration + restore drill).

Every URS row traces to both a design element and a verification method. No orphan
requirements; no design element without a governing requirement.
