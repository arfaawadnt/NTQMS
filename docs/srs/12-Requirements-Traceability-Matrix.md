# NT.QMS — Production Software Requirements Specification
## Document 12 · Requirements Traceability Matrix

> [Conventions](00-SRS-Index-and-Conventions.md) · Gap classification:
> [Document 13](13-Implementation-vs-SRS-Gap-Analysis.md)

---

# 12.1 Purpose and relationship to the CSV RTM

Two traceability matrices exist and they answer **different questions**:

| Matrix | Question it answers | Scope |
|---|---|---|
| `docs/validation/04-Requirements-Traceability-Matrix.md` (RTM-NTQMS-001) | *"Is each of the 55 validated user requirements designed, built and verified?"* | the **GAMP 5 CSV** requirement set |
| **This document** | *"Where does every requirement in this SRS live in the code, the UI and the configuration, and is it actually implemented?"* | the **as-built specification** set |

They are complementary; where a requirement appears in both, this matrix cites the URS identifier.

## Legend

| Field | Values |
|---|---|
| **Priority** | **M** = mandatory (regulatory or system-breaking) · **S** = should · **C** = could |
| **Source** | `CODE` (reverse-specified) · `URS-nnn` (CSV requirement) · `ADR-nnnn` · `AUDIT` (remediation finding) · `SRS-v1` (the superseded SRS) |
| **Impl.** | ✅ fully implemented · ⚠ partially implemented · ❌ not implemented · 🔒 implemented but unreachable |
| **Verified** | `AUTO` automated test · `OQ` executed qualification case · `PROBE` security probe · `INSP` inspection only · `—` not verified |

---

# 12.2 Module-to-artefact map

Every module's code, UI and endpoint locations in one table. `Endpoints` counts unversioned routes
(each also exists under `/api/v{version}/`).

| Module | Code | Name | Endpoints | Domain (src/NT.QAMS.Domain/) | Application (src/NT.QAMS.Application/) | Controller (src/NT.QAMS.WebApi/Controllers/) | SPA route |
|---|---|---|---:|---|---|---|---|
| **M-01** | `NC` | Nonconformance & CAPA | 12 | `Improvement/Nonconformance.cs` | `Improvement/Commands/NcWorkflowCommands.cs; Improvement/Queries/NcQueries.cs` | `NonconformancesController.cs` | `/nonconformances` |
| **M-02** | `CMP` | Complaints | 9 | `Improvement/Complaint.cs` | `Improvement/Commands/ComplaintCommands.cs; Improvement/ComplaintToNcPolicy.cs` | `ComplaintsController.cs` | `/complaints` |
| **M-03** | `FBK` | Customer feedback | 6 | `Improvement/FeedbackEntry.cs` | `Improvement/FeedbackSlice.cs` | `FeedbackController.cs` | `/feedback` |
| **M-04** | `OBJ` | Quality objectives | 5 | `Improvement/QualityObjective.cs` | `Improvement/QualityObjectiveSlice.cs` | `QualityObjectivesController.cs` | `/quality-objectives` |
| **M-05** | `QP` | Quality policy | 5 | `Improvement/QualityPolicy.cs` | `Improvement/QualityPolicySlice.cs` | `QualityPolicyController.cs` | `/quality-policy` |
| **M-06** | `AUD` | Audits | 7 | `AuditManagement/Audit.cs` | `AuditManagement/Commands/AuditCommands.cs; AuditManagement/Policies/FindingToNcPolicy.cs` | `AuditsController.cs` | `/audits` |
| **M-07** | `CHG` | Change control | 8 | `RiskGovernance/ChangeAndReview.cs` | `RiskGovernance/RiskGovernanceSlice.cs` | `GovernanceControllers.cs` | `/changes` |
| **M-08** | `MRV` | Management review | 5 | `RiskGovernance/ChangeAndReview.cs` | `RiskGovernance/RiskGovernanceSlice.cs` | `GovernanceControllers.cs` | `/management-reviews` |
| **M-09** | `DOC` | Document control | 17 | `DocumentControl/ControlledDocument.cs; DocumentAcknowledgement.cs; DocumentControlledCopy.cs` | `DocumentControl/Commands/DocumentCommands.cs; ControlledCopySlice.cs; DocumentAcknowledgementSlice.cs; DocumentReviewDuePolicy.cs` | `DocumentsController.cs` | `/documents` |
| **M-10** | `ARC` | Records & archive | 7 | `Records/ArchiveEntry.cs` | `Records/RecordsSlice.cs` | `OperationsControllers.cs` | `/records` |
| **M-11** | `EQP` | Equipment | 7 | `Equipment/EquipmentItem.cs` | `Equipment/EquipmentSlice.cs; IntermediateCheckToNcPolicy.cs` | `EquipmentController.cs` | `/equipment` |
| **M-12** | `RS` | Reference standards | 6 | `Equipment/ReferenceStandard.cs` | `Equipment/ReferenceStandardsSlice.cs` | `ReferenceStandardsController.cs` | `/reference-standards` |
| **M-13** | `ENV` | Environmental monitoring | 8 | `Facility/MonitoringPoint.cs` | `Facility/MonitoringSlice.cs; ExcursionToNcPolicy.cs` | `MonitoringPointsController.cs` | `/monitoring` |
| **M-14** | `SUP` | Supplier quality | 8 | `SupplierQuality/Supplier.cs` | `SupplierQuality/SupplierSlice.cs` | `GovernanceControllers.cs` | `/suppliers` |
| **M-15** | `COMP` | Competency | 6 | `Competency/CompetencyRecord.cs` | `Competency/CompetencySlice.cs; CompetencyLapseAuthorizationPolicy.cs` | `CompetenciesController.cs` | `/competencies` |
| **M-16** | `TRN` | Training assignments | 3 | `Competency/CompetencyRecord.cs` | `Competency/CompetencySlice.cs` | `CompetenciesController.cs` | `/training` |
| **M-17** | `AUTHZ` | Test authorisations | 6 | `Competency/TestAuthorization.cs` | `Competency/AuthorizationSlice.cs` | `TestAuthorizationsController.cs` | `/authorizations` |
| **M-18** | `USER` | User administration | 10 | `IdentityAccess/UserAccount.cs` | `IdentityAccess/Commands/UserManagement.cs` | `UsersController.cs; UserDirectoryController.cs` | `/users` |
| **M-19** | `ROLE` | Roles & privileges | 8 | `Authorization/Role.cs; Authorization/PermissionCatalog.cs` | `Authorization/RolesSlice.cs; SystemRoleCatalog.cs; SeededRoleDefault.cs` | `RolesController.cs` | `/roles` |
| **M-20** | `RSK` | Risk register | 7 | `RiskGovernance/RiskItem.cs` | `RiskGovernance/RiskGovernanceSlice.cs` | `GovernanceControllers.cs` | `/risks` |
| **M-21** | `COI` | Conflicts of interest | 5 | `RiskGovernance/ConflictDeclaration.cs` | `RiskGovernance/ConflictSlice.cs` | `ConflictsController.cs` | `/conflicts` |
| **M-22** | `CTX` | Organisational context | 9 | `Organization/OrganizationContext.cs` | `Organization/OrgContextSlice.cs` | `OrgContextController.cs` | `/org-context` |
| **M-23** | `UAR` | User access review | 3 | `IdentityAccess/UserAccessReview.cs` | `IdentityAccess/Commands/AccessReviewSlice.cs` | `AccessReviewsController.cs` | `/access-reviews` |
| **M-24** | `CLD` | Compliance ledger & e-signatures | 8 | `ComplianceLedger/AuditTrailReview.cs; ComplianceLedger/LedgerEntries.cs` | `ComplianceLedger/AuditTrailReviewSlice.cs; ComplianceQueries.cs` | `ComplianceController.cs` | `/compliance` |
| **M-25** | `AQ.QC` | Westgard QC | 6 | `AnalyticalQuality/QcProfile.cs; WestgardEvaluator.cs` | `AnalyticalQuality/QcSlice.cs` | `AnalyticalQualityControllers.cs` | `/qc` |
| **M-26** | `AQ` | Analytical studies (12) | — | `AnalyticalQuality/*Study.cs; OutlierScreening.cs; SigmaAssessment.cs; UncertaintyBudget.cs` | `AnalyticalQuality/*Slice.cs` | `AnalyticalQualityControllers.cs + 11 study controllers` | `/validation-studies … /uncertainty` |
| **M-27** | `PT` | Proficiency testing | 11 | `AnalyticalQuality/PtPlan.cs; PtEnrollment.cs` | `AnalyticalQuality/PtPlanSlice.cs; PtToNcPolicy.cs; ValidationAndPtSlice.cs` | `PtPlansController.cs; AnalyticalQualityControllers.cs` | `/pt-plans, /proficiency-tests` |
| **M-28** | `TASK` | Tasks, SLA & escalation | 5 | `Sla/SlaAndTasks.cs` | `Sla/SlaSlice.cs; EscalationTriggeredPolicy.cs` | `OperationsControllers.cs` | `/tasks` |
| **M-29** | `NTF` | Notifications | 5 | `Notifications/NotificationAggregates.cs` | `Notifications/NotificationSlice.cs; NotificationDispatcher.cs; NotificationPolicies.cs` | `PlatformControllers.cs` | `/notifications, /notification-rules` |
| **M-30** | `RPT` | Reporting & KPIs | 4 | `Reporting/KpiSnapshot.cs` | `Reporting/ReportingQueries.cs` | `ReportsController.cs` | `/dashboard` |
| **M-31** | `ORG` | Organisation reference data | 3 | `Organization/OrganizationAggregates.cs` | `Organization/OrganizationSlice.cs; DefaultLovCatalog.cs` | `PlatformControllers.cs` | `/reference-data` |
| **M-32** | `TEN` | Tenant settings | 2 | `Tenancy/TenantSettings.cs` | `Tenancy/Commands/TenantMfaPolicy.cs` | `TenantSettingsController.cs` | `/settings/security` |
| **M-33** | `PLT` | Platform control plane | 2 | `Tenancy/Tenant.cs; TenantSlug.cs; TenantStatus.cs; TenantEvents.cs` | `Tenancy/Commands/ProvisionTenant.cs; Queries/GetTenants.cs; GetWorkspace.cs` | `TenantsController.cs` | `/platform/tenants` |
| **M-34** | `AUTH` | Authentication & session | 10 | `IdentityAccess/UserAccount.cs; RefreshSession.cs; PasswordHistoryEntry.cs` | `IdentityAccess/Commands/Login.cs; MfaAndPin.cs; RefreshSessions.cs; PasswordRules.cs` | `AuthController.cs` | `/login, /t/:tenant, /security/mfa-setup` |
| **M-35** | `FILE` | File storage & evidence | 2 | `Files/FileReference.cs` | `Abstractions/IFileStorage.cs` | `FilesController.cs` | `(embedded)` |
| **M-36** | `EXP` | Exports | 4 | `—` | `Abstractions/IExportService.cs` | `ExportsController.cs` | `(buttons on registers)` |


---

# 12.3 Functional requirement traceability

One row per **specified capability**, not per endpoint. `FR-<MOD>-nn` identifiers are defined in
[Document 02](02-1-Functional-Specification-Quality-and-Improvement.md).

| Req ID | Requirement | Pri | Source | Impl. | Code location | UI location | Config |
|---|---|---|---|---|---|---|---|
| FR-NC-01 | Raise an NC with RPN = severity × likelihood | M | CODE, URS | ✅ | `Domain/Improvement/Nonconformance.cs` `Raise` | `/nonconformances` drawer | — |
| FR-NC-02 | Guarded 9-state CAPA workflow with two loop-backs | M | CODE, URS | ✅ AUTO | same, `Submit`…`ConfirmEffectiveness` | `nc-detail` stepper | — |
| FR-NC-03 | SoD on verify and close | M | 21 CFR 11.10(g) | ✅ AUTO | `SOD-CAPA-001/002` | server-enforced | — |
| FR-NC-04 | Verification blocked until all CAPA actions complete | M | CODE | ✅ | `NC-019`, `NC-020` | button disabled | — |
| FR-NC-05 | Four quality-event types on one workflow | S | CODE | ✅ | `QualityEventType` | selector on raise form | — |
| FR-NC-06 | Auto-raise from six upstream policies | M | CODE | ✅ | 6 `*Policy.cs` in Application | n/a (server) | — |
| FR-NC-07 | Structured RCA (5-Whys / Fishbone editors) | S | SRS-v1 | ❌ **not built** — enum + free text only | `RcaMethod` | plain textarea | — |
| FR-CMP-01 | Complaint lifecycle with justified/unjustified gate | M | ISO 17025 §7.9 | ✅ | `Domain/Improvement/Complaint.cs` | `/complaints` | — |
| FR-CMP-02 | Justified complaint auto-raises an NC | M | CODE | ✅ | `ComplaintToNcPolicy` | — | — |
| FR-CMP-03 | Closure blocked while the linked NC is open | M | CODE | ⚠ **defect** — a *rejected* NC never reaches `Closed`, so the complaint becomes unclosable | `CMP-020` | — | — |
| FR-FBK-01 | Feedback lifecycle; only dissatisfaction escalates | M | CODE | ✅ | `FBK-014` | `/feedback` | — |
| FR-OBJ-01 | Objective cannot be declared achieved against contrary evidence | M | CODE | ✅ | `OBJ-011` | `/quality-objectives` | — |
| FR-QP-01 | Versioned policy, one in force, SoD approval | M | ISO 9001 §5.2 | ✅ AUTO | `QualityPolicySlice` | `/quality-policy` | — |
| FR-AUD-01 | Checklist audit with graded findings → NC | M | ISO 17025 §8.8 | ✅ | `Domain/AuditManagement/Audit.cs` | `/audits` | — |
| FR-AUD-02 | Sign-off blocked on unanswered items / unlinked NC findings | M | CODE | ✅ | `AUD-017`, `AUD-018` | button disabled | — |
| FR-AUD-03 | ISO-clause checklist templates | S | SRS-v1 | ❌ **not built** — checklist typed per audit | — | — | — |
| FR-CHG-01 | Change approval requires a linked risk assessment | M | Annex 11 §4 | ✅ | `CHG-012` | `/changes` | — |
| FR-CHG-02 | Post-implementation review, terminal | S | CODE | ✅ AUTO | `RecordPostImplementationReview` | PIR form | — |
| FR-MRV-01 | Management review with immutable minutes | M | ISO 17025 §8.9 | ✅ | `MRV-004` | `/management-reviews` | — |
| FR-MRV-02 | Review decisions become tasks | S | expectation | ❌ **not built** — decisions do not create `WorkTask` rows | — | — | — |
| FR-MRV-03 | §8.9.2 standing input checklist | S | ISO 17025 | ❌ not built | — | — | — |
| FR-DOC-01 | Draft→Review→Approve→Publish→Obsolete with SoD at both gates | M | ISO 17025 §8.3 | ✅ AUTO | `ControlledDocument` | `/documents` | — |
| FR-DOC-02 | Version bump; publishing obsoletes the prior version | M | CODE | ✅ | `DocumentVersionObsoleted` | detail | — |
| FR-DOC-03 | Periodic review cycle (default 24 months) raised by the sweep | M | ISO 17025 §8.3 | ✅ | `MarkReviewDueIfReached` | banner | per-document input |
| FR-DOC-04 | Read-and-understand acknowledgement pinned to the version | M | CODE | ✅ AUTO | `DocumentAcknowledgement` | detail card | — |
| FR-DOC-05 | Numbered controlled-copy register, one-shot closure | S | ISO 17025 §8.3 | ✅ | `DocumentControlledCopy` | detail card | — |
| FR-DOC-06 | Obsolete-version PDF watermark | S | SRS-v1 | ❌ **not built** — no PDF processing exists | — | — | — |
| FR-DOC-07 | Acknowledgement enforcement (block work until acknowledged) | C | expectation | ❌ not built | — | — | — |
| FR-ARC-01 | Archive requires an immutable snapshot | M | 21 CFR 11.10(c) | ✅ | `ARC-002` | `/records` | — |
| FR-ARC-02 | Retention classes; permanent never disposable | M | ISO 17025 §8.4 | ✅ | `ARC-013`, `ARC-014` | — | — |
| FR-ARC-03 | Legal hold blocks disposal regardless of retention | M | CODE | ✅ AUTO | `ARC-015` | hold indicator | — |
| FR-EQP-01 | Calibration interval + grace with automatic lock-out | M | ISO 17025 §6.4 | ✅ | `LockOutIfGraceExhausted` | `/equipment` | per-item |
| FR-EQP-02 | Lock-out **blocks instrument selection** | M | SRS-v1, ISO 17025 | ❌ **status only** — instrument references are free text, not FKs | — | — | — |
| FR-EQP-03 | Failed intermediate check auto-raises an NC | M | CODE | ✅ | `IntermediateCheckToNcPolicy` | — | — |
| FR-EQP-04 | Checks may only cite an active, in-date standard | M | CODE | ✅ | `RS-020` | picker | — |
| FR-RS-01 | Traceability chain mandatory; auto-expiry | M | ISO 17025 §6.5 | ✅ | `RS-002`, `MarkExpiredIfReached` | `/reference-standards` | — |
| FR-ENV-01 | Limits mandatory; excursion auto-raises an NC | M | ISO 17025 §6.3 | ✅ | `ExcursionToNcPolicy` | `/monitoring` | — |
| FR-ENV-02 | Excursion de-bounce / stabilisation window | S | expectation | ❌ **not built** — one NC per out-of-limit reading | — | — | — |
| FR-SUP-01 | Supplier approval with SoD; certificate-expiry suspension | M | ISO 17025 §6.6 | ✅ | `SOD-SUP-001`, `SuspendIfCertificateExpired` | `/suppliers` | — |
| FR-SUP-02 | Weighted periodic evaluation | M | ISO 17025 §6.6 | ✅ | `SupplierEvaluation.Record` | detail | — |
| FR-SUP-03 | Reinstate a suspended supplier | M | expectation | ❌ **not built** — `Suspended` is terminal | — | — | — |
| FR-COMP-01 | Pass mark 80; SoD on assess and authorise; validity expiry | M | ISO 17025 §6.2 | ✅ | `PassMark`, `SOD-COMP-001` | `/competencies` | ❌ **hard-coded** |
| FR-COMP-02 | Lapse cascades to suspend test authorisations and raise an NC | M | CODE | ✅ | `CompetencyLapseAuthorizationPolicy` | — | — |
| FR-TRN-01 | Training queue with due dates | S | ISO 17025 §6.2.5 | ✅ | `TrainingAssignment` | `/training` | — |
| FR-TRN-02 | Training completion feeds competency | S | expectation | ❌ **not linked** | — | — | — |
| FR-AUTHZ-01 | Per-test authorisation evidenced by a current competency | M | ISO 17025 §6.2.6 | ✅ | `AUTHZ-003`, `AUTHZ-004` | `/authorizations` | — |
| FR-AUTHZ-02 | Scope (Perform / ReviewAndRelease / Train) **enforced** | M | ISO 17025 §6.2.6 | ❌ **recorded, never checked** downstream | `AuthorizationScope` | — | — |
| FR-USER-01 | User lifecycle; platform/tenant identity classes disjoint | M | 21 CFR 11.10(d) | ✅ AUTO | `USER-003/004/005` | `/users` | — |
| FR-USER-02 | Password policy, history, expiry, lockout | M | 21 CFR 11.300 | ✅ AUTO | `PasswordRules`, `UserAccount` | — | CFG-12, CFG-13 |
| FR-USER-03 | Branch/department scope restricts visibility | S | CODE | ✅ | `OrgScopeGuardInterceptor` | allocation picker | — |
| FR-USER-04 | Self-service password reset | S | expectation | ❌ not built | — | — | — |
| FR-USER-05 | Active session monitor with per-session revoke | S | SRS-v1 | ❌ **not built** — data exists in `qams.refresh_session`, no surface | — | — | — |
| FR-ROLE-01 | 171 code-defined permission keys; matrix UI from the catalogue | M | 21 CFR 11.10(d) | ✅ | `PermissionCatalog` | `/roles` | — |
| FR-ROLE-02 | Administrative lock-out guard | M | CODE | ✅ AUTO | `ROLE-006` | — | — |
| FR-ROLE-03 | Every permission change carries a reason | M | 21 CFR 11.10(e) | ✅ | `RolePermissionsChanged` | reason field | — |
| FR-RSK-01 | 5×5 risk with mandatory residual assessment before closure | M | ISO 17025 §8.5 | ✅ | `RSK-005`, `RSK-006` | `/risks` | — |
| FR-COI-01 | Impartiality declarations with SoD assessment | M | ISO 17025 §4.1 | ✅ | `SOD-COI-001` | `/conflicts` | — |
| FR-CTX-01 | Interested parties + context issues with risk linkage | M | ISO 9001 §4.1/4.2 | ✅ | `OrganizationContext.cs` | `/org-context` | — |
| FR-UAR-01 | Periodic access review with snapshotted account count | M | 21 CFR 11.10(d) | ✅ | `UserAccessReview` | `/access-reviews` | — |
| FR-UAR-02 | Line-by-line account attestation | S | expectation | ❌ count only | — | — | — |
| FR-CLD-01 | Per-field append-only ledger with actor, time and reason | M | 21 CFR 11.10(e) | ✅ AUTO | `FieldChangeInterceptor` | `/compliance` | — |
| FR-CLD-02 | Hash chain with verification identifying the first break | M | 21 CFR 11.10(e) | ✅ AUTO | `VerifyChainAsync` | tab | — |
| FR-CLD-03 | E-signature: password + PIN, logged, throttled | M | 21 CFR 11.200 | ✅ AUTO | `ESignatureService` | signing dialogs | CFG-17 |
| FR-CLD-04 | Signed records immutable **in the database** | M | 21 CFR 11.10(c) | ✅ AUTO | `reject_frozen_mutation()` | — | — |
| FR-CLD-05 | Audit-trail review with mandatory conclusion | M | Annex 11 §9 | ✅ | `ATR-011` | tab | — |
| FR-CLD-06 | `audit.security_event` tenant-isolated | M | CODE | ❌ **NOT MET** — no RLS policy on that table | — | — | — |
| FR-AQ-01 | 12 study types on one DataEntry→Calculated→SignedOff lifecycle | M | ISO 15189 / CLSI | ✅ | `Domain/AnalyticalQuality/*` | 24 screens | — |
| FR-AQ-02 | Westgard multi-rule with configurable limits, frozen verdict | M | ISO 17025 §7.7 | ✅ AUTO | `WestgardEvaluator` | `/qc` | CFG-18…21 |
| FR-AQ-03 | QC target change requires a reason and is forward-only | M | CODE | ✅ | `QC-012`, `QC-013` | targets form | — |
| FR-AQ-04 | Statistical charts (Levey-Jennings, Passing-Bablok, Bland-Altman) | S | SRS-v1 | ❌ **not built** — numeric results only | — | — | — |
| FR-AQ-05 | CSV import of study data | S | CODE | ⚠ **2 of 12 studies only** (method comparison, precision) | `qams-csv-import` | those two details | — |
| FR-AQ-06 | LIS/instrument data ingestion | C | SRS-v1 | ❌ not built | — | — | — |
| FR-AQ-07 | QC profile deactivation | S | CODE | 🔒 **unreachable** — aggregate method exists, no command, no endpoint | `QcProfile.Deactivate` | — | — |
| FR-PT-01 | z-score grading; unsatisfactory auto-raises an NC | M | ISO 17025 §7.7.2 | ✅ | `PtEnrollment.RecordResult` | `/proficiency-tests` | ❌ hard-coded thresholds |
| FR-PT-02 | Annual PT plan, one per year, fulfilment tracking | M | ISO 17025 §7.7.2 | ✅ | `PtPlan` | `/pt-plans` | — |
| FR-PT-03 | En-number / zeta grading for calibration ILCs | S | ISO 17043 | ❌ **not built** — z-score only | — | — | — |
| FR-PT-04 | Questionable result triggers an investigation | S | expectation | ❌ nothing happens | — | — | — |
| FR-TASK-01 | Task queue; assignment to a user or a role | S | CODE | ✅ | `WorkTask` | `/tasks` | — |
| FR-TASK-02 | Three-level escalation ladder at +24/+48/+72 h | S | ISO 17025 §8.7.1 | ⚠ **built, but level 2 escalates to Quality Manager, not Department Head** as the previous SRS specified | `EscalationTimer` | — | ❌ hard-coded |
| FR-TASK-03 | Escalation deadlines derived from `SlaDefinition` | S | expectation | ❌ **not wired** — the two are independent | — | — | — |
| FR-NTF-01 | 11 event keys → configurable rules → in-app + e-mail | S | CODE | ✅ | `NotificationPolicies` | `/notification-rules` | CFG-24…29 |
| FR-NTF-02 | E-mail retry on transient failure | S | expectation | ❌ **not built** — one attempt, marked `Failed` | — | monitor tab | — |
| FR-NTF-03 | Operator warning when SMTP is unconfigured | S | expectation | ❌ silent degradation to log-only | — | — | — |
| FR-RPT-01 | Dashboard KPIs with real population denominators | S | CODE | ✅ AUTO | `DashboardKpiTotalsDto` | `/dashboard` | — |
| FR-RPT-02 | 6-hourly KPI snapshot series, one row per tenant per day | S | CODE | ✅ | `KpiSnapshotService` | history chart | ❌ hard-coded interval |
| FR-ORG-01 | Branch → department hierarchy; test catalogue; LOVs | M | CODE | ✅ | `OrganizationAggregates` | `/reference-data` | — |
| FR-TEN-01 | Per-tenant MFA policy for privileged roles | M | 21 CFR 11.300 | ✅ | `TenantSettings` | `/settings/security` | CFG-09 |
| FR-PLT-01 | Tenant provisioning with the first administrator | M | CODE | ✅ AUTO | `ProvisionTenant` | `/platform/tenants` | — |
| FR-PLT-02 | Tenant suspend / reactivate / terminate | M | CODE | 🔒 **domain only — no endpoint, no UI** | `Tenant.Suspend/Reactivate/Terminate` | — | — |
| FR-AUTH-01 | Tenant sign-in with optional TOTP | M | 21 CFR 11.300 | ✅ AUTO | `LoginCommand` | `/login` | CFG-04…09 |
| FR-AUTH-02 | Rotating refresh cookie with family-revoking reuse detection | M | ADR-0009 | ✅ **PROBE (live)** | `RefreshSession` | silent refresh | CFG-08 |
| FR-AUTH-03 | Per-request account and role re-check | M | CODE | ✅ AUTO | `ActiveSessionMiddleware` | — | — |
| FR-AUTH-04 | Workspace lookup that cannot probe tenant existence | M | CODE | ✅ AUTO | `GetWorkspaceQuery` | login pill | — |
| FR-FILE-01 | Allow-list + content sniffing; canonical type stored | M | CODE | ✅ AUTO | `FileContentPolicy` | upload controls | — |
| FR-FILE-02 | Content-addressed dedup storage | S | CODE | ✅ | `LocalFileStorage` | — | CFG-23 |
| FR-FILE-03 | Maximum upload size | M | expectation | ❌ **not enforced in application code** | — | — | — |
| FR-EXP-01 | Four inspection exports, each logged as `RECORD_EXPORTED` | M | 21 CFR 11.10(b) | ✅ | `ExportService` | export buttons | — |

## Summary

| Impl. | Count |
|---|---:|
| ✅ fully implemented | 62 |
| ⚠ partial or defective | 4 |
| 🔒 implemented but unreachable | 3 |
| ❌ not implemented | 25 |

**All 25 "not implemented" rows are capabilities named by the previous SRS or by reasonable
expectation — none is a regression.** They are classified in
[Document 13](13-Implementation-vs-SRS-Gap-Analysis.md).

---

# 12.4 Non-functional requirement traceability

| Req ID | Requirement | Pri | Impl. | Evidence | Location |
|---|---|---|---|---|---|
| NFR-PERF-01 | Reads p95 < 500 ms at 50 users | M | ✅ | measured 85.8–104.7 ms, 0 % errors | `docs/reference/NT_QMS_Load_Test_Report.md` |
| NFR-PERF-04 | No unbounded list query | M | ⚠ | `pageSize` clamped to 200; **`take` and `days` not clamped**; 15+ lists unpaged | `Application/Abstractions/Paging.cs` |
| NFR-PERF-05 | Rate limit sized per site | M | ⚠ | default 300 is an abuse ceiling, not a concurrency ceiling | CFG-14 |
| NFR-REL-01…04 | Retry, timeout, at-least-once outbox, backoff + dead-letter | M | ✅ | drill executed | `OutboxProcessor` |
| NFR-REL-06 | `xmin` concurrency → 409 | M | ✅ AUTO | — | ADR-0005 |
| NFR-REL-07 | Idempotency-Key replay protection | S | ✅ | — | `IdempotencyBehavior` |
| NFR-REL-08 | Cold start survives an unreachable database | M | ✅ **OQ** | OPS-010 fix + 6 regression tests | `DeferredStartupSeeder` |
| NFR-AVL-01…04 | Liveness DB-independent; readiness DB-backed; probes exempt | M | ✅ OQ | — | `Program.cs` |
| NFR-AVL-05 | Availability target | — | ❌ **none defined** | — | — |
| NFR-RCV-01…04 | RPO ≤ 5 min, RTO ≤ 4 h, WAL PITR, verified restore | M | ⚠ **documented, never drilled** | — | `deploy/BACKUP-RESTORE-DR.md` |
| NFR-MNT-01…05 | Layering, command policy, API surface, module boundary, migration round-trip gates | M | ✅ AUTO | 7 merge gates | `tests/NT.QAMS.Architecture.Tests`, CI |
| NFR-MNT-06 | 436 backend / 74 frontend / 6 e2e green, 0 skipped | M | ✅ | last recorded run | CI |
| NFR-SCL-01 | Single replica | M | ✅ by design | sentinel | ADR-0001 |
| NFR-SCL-02 | Partition-ready schema | S | ✅ | 88 composite PKs, no `UNIQUE(id)` | schema hardening Phase 5 |
| NFR-OBS-01…06 | JSON logs, correlation, end-to-end traces, Prometheus metrics | M | ✅ | — | `Program.cs`, `QamsMetrics` |
| NFR-OBS-07 | Seven actionable alerts | M | ⚠ **defined, never deployed or observed firing** | residual R-7 | `deploy/OBSERVABILITY.md` |
| NFR-SEC-01…16 | See [Document 09 §9.13](09-Security-Specification.md) | M | ✅ ×16 | probes, tests, OQ | — |
| NFR-SEC-17 | `audit.security_event` tenant-isolated | M | ❌ **NOT MET** | — | T-01 |
| NFR-SEC-18 | Uploads size-bounded | M | ❌ **NOT MET** | — | T-10 |
| NFR-SEC-19 | Independent penetration test | M | ❌ **NOT PERFORMED** | dev-instance self-assessment only | T-22 |
| NFR-LOC-01…04 | en/ar/fr, RTL, three-step language resolution, localised LOVs | M | ✅ | — | `core/i18n.service.ts` |
| NFR-A11Y-01…04 | axe always-on; violations fixed; accessible dialogs; no colour-only meaning | S | ✅ | CI | — |
| NFR-A11Y-05 | Declared WCAG conformance level | S | ❌ none claimed | — | — |
| NFR-RES-01 | Memory/CPU/disk budgets | S | ❌ **none defined** | — | — |
| NFR-RES-04 | Non-root container | M | ✅ AUTO | CI asserts the uid | `Dockerfile` |

---

# 12.5 Architecture-constraint traceability

| Constraint | Enforced by | Automated? |
|---|---|---|
| AC-01…AC-06 layering | `LayerRulesTests` | ✅ **build gate** |
| AC-07 one command policy | `CommandPolicyTests` | ✅ **build gate** |
| AC-08 bounded `user_account` queries | `UserAccountTenantBoundTests` (self-proving) | ✅ **build gate** |
| AC-19/AC-20 RLS on every tenant table | migration discipline | ❌ **manual — no gate** |
| AC-24 tenant-first composite PKs | schema hardening | ❌ manual |
| AC-29 `xmin` concurrency | ADR + review | ❌ manual (scaffolded ops must be hand-removed) |
| AC-31 column sizing ↔ validator | review | ❌ manual |
| AC-32 index naming ≤ 62 chars | review + abbreviation map | ❌ manual |
| AC-33 migration RLS bypass | review | ❌ manual |
| AC-36 MediatR 12.4 pin | `.csproj` | ✅ compile |
| AC-41 single replica | `SingleReplicaGuardService` | ⚠ detects, does not prevent |
| AC-45 dual routing | `VersionedRouteConvention` + surface gate | ✅ build gate |
| AC-47 DB role guard | `DatabaseRoleGuard` | ✅ **runtime gate (Production)** |
| AC-49 non-root container | CI assertion | ✅ build gate |
| AC-50 strict TS, no `any` | compiler | ✅ compile |

**Nine of fifteen constraint families are automatically enforced.** The six manual ones — all
database-schema disciplines — are the highest-risk maintenance surface; see
[Document 15](15-Recommendations.md).

---

# 12.6 Verification-method coverage

| Method | Count | What it covers |
|---|---|---|
| Domain unit tests | 37 files / 3,613 lines | aggregate invariants and state machines |
| Application unit tests | 17 files / 1,942 lines | handlers, validators, policies |
| Architecture tests | 4 files / 24 tests | layering, command policy, module boundaries, tenant-bound queries |
| Integration tests | 12 files / 1,229 lines | **real PostgreSQL**: RLS isolation, fail-closed, bypass, `WITH CHECK`, signed-record rejection |
| Functional tests | 28 files / 3,290 lines | HTTP contract, role matrix, envelope coverage, API surface, auth flows |
| Playwright e2e | 6 specs | login, regulated workflow, load-more journey, axe |
| Load tests | outside the solution | read-mix latency (run with `dotnet run`) |
| Security probes | 15 fast + 9 deep = 24 | IDOR, refresh reuse, CORS/XST, credential burst |
| OQ execution records | 18 cases, 18 passed | witnessed qualification |
| Failure drills | poison→dead-letter ✅ · PG-down ⚠ (needs elevation) | recovery |

**Total automated backend tests: 436, 0 skipped.**

---

# 12.7 Requirements with no verification

Named explicitly so they are not mistaken for verified behaviour.

| Requirement | Why unverified |
|---|---|
| Backup / restore / DR | no restore drill executed — **`[Not Executed]`** |
| Alert set | observability stack never brought up (no Docker) — **R-7** |
| Load behaviour under a write mix | the harness exists; the mix was never run against a shared database |
| 24-hour soak | needs a staging host — **R-5** |
| Multi-node contention | not applicable at one replica, and untested |
| E-mail delivery | SMTP never configured in any tested environment |
| Container deployment | authored, never executed here |
| CSV re-validation | **R-6** |
| Independent penetration test | **T-22** — the largest assurance gap |
| Browser compatibility matrix | none declared |
| Formal signed IQ/OQ/PQ | execution and signature are events that have not happened |
