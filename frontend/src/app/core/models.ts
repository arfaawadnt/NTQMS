// API contracts mirrored from NT.QAMS.Contracts (kept intentionally thin, strongly typed).

/** Authentication result returned by POST /api/auth/login. */
export interface AuthResponse {
  accessToken: string;
  expiresAtUtc: string;
  role: string;
  displayName: string;
  tenantId: string | null;
  mfaRequired: boolean;
}

/** Nonconformance source classifications accepted by the backend. */
export const NC_SOURCE_TYPES = ['Internal', 'Complaint', 'Audit', 'Supplier', 'ProficiencyTest'] as const;
export type NcSourceType = (typeof NC_SOURCE_TYPES)[number];

/** CAPA action types accepted by the backend. */
export const CAPA_ACTION_TYPES = ['Corrective', 'Preventive'] as const;
export type CapaActionType = (typeof CAPA_ACTION_TYPES)[number];

/** Root-cause-analysis methods accepted by the backend. */
export const RCA_METHODS = ['FiveWhys', 'Fishbone', 'Other'] as const;
export type RcaMethod = (typeof RCA_METHODS)[number];

/** Assignable tenant roles (platform-admin is not a tenant role). */
export const TENANT_ROLES = ['TenantAdmin', 'QualityManager', 'DepartmentHead', 'Analyst', 'ExternalAuditor'] as const;
export type TenantRole = (typeof TENANT_ROLES)[number];

export interface NcListItem {
  id: string;
  ncRef: string;
  title: string;
  status: string;
  severity: number;
  rpn: number;
  sourceType: string;
  createdAtUtc: string;
  branchId: string | null;
  departmentId: string | null;
}

export interface CapaAction {
  id: string;
  type: string;
  details: string;
  ownerId: string;
  dueDate: string;
  status: string;
  completedAtUtc: string | null;
}

export interface RcaRecord {
  id: string;
  method: string;
  analysis: string;
  investigatorId: string;
}

export interface NcDetail {
  id: string;
  ncRef: string;
  title: string;
  description: string;
  status: string;
  severity: number;
  likelihood: number;
  rpn: number;
  sourceType: string;
  raisedBy: string;
  assignedTo: string | null;
  rejectionReason: string | null;
  createdAtUtc: string;
  capaActions: CapaAction[];
  rcaRecords: RcaRecord[];
}

/** Request payload for raising a nonconformance. */
export interface RaiseNcRequest {
  title: string;
  description: string;
  severity: number;
  likelihood: number;
  sourceType: NcSourceType;
  branchId: string | null;
  departmentId: string | null;
}

export interface TriageNcRequest { assigneeId: string; }
export interface RejectNcRequest { reason: string; }
export interface RecordRcaRequest { method: RcaMethod; analysis: string; }
export interface PlanCapaActionRequest { type: CapaActionType; details: string; ownerId: string; dueDate: string; }
export interface VerifyNcRequest { passed: boolean; }
export interface ConfirmEffectivenessRequest { effective: boolean; }

// ── Reporting (read models — real data only) ─────────────────────────────────

export interface DashboardKpis {
  openNcs: number;
  overdueCapaActions: number;
  openComplaints: number;
  auditsInProgress: number;
  equipmentOutOfService: number;
  equipmentNeedsCalibration: number;
  highResidualRisks: number;
  overdueTasks: number;
  ptUnsatisfactory: number;
  pendingTrainingAssignments: number;
  suspendedSuppliers: number;
  publishedDocuments: number;
  computedAtUtc: string;
}

export interface KpiHistoryPoint {
  date: string;
  openNcs: number;
  overdueCapaActions: number;
  openComplaints: number;
  equipmentOutOfService: number;
  highResidualRisks: number;
  overdueTasks: number;
}

export interface NcParetoBucket { sourceType: string; count: number; }

export interface SlaCompliance {
  completedTotal: number;
  completedOnTime: number;
  onTimePercent: number;
  openTotal: number;
  openOverdue: number;
  computedAtUtc: string;
}

// ── Complaints ───────────────────────────────────────────────────────────────

export const COMPLAINT_CHANNELS = ['Phone', 'Email', 'Portal', 'InPerson', 'Letter'] as const;
export type ComplaintChannel = (typeof COMPLAINT_CHANNELS)[number];

export interface ComplaintListItem {
  id: string;
  complaintRef: string;
  subject: string;
  channel: string;
  status: string;
  confidential: boolean;
  complainantName: string;
  loggedAtUtc: string;
  branchId: string | null;
  departmentId: string | null;
}

export interface ComplaintDetail {
  id: string;
  complaintRef: string;
  channel: string;
  complainantName: string;
  complainantContact: string | null;
  confidential: boolean;
  subject: string;
  description: string;
  status: string;
  loggedAtUtc: string;
  acknowledgedAtUtc: string | null;
  validationVerdict: string | null;
  investigationOutcome: string | null;
  resolution: string | null;
  linkedNcId: string | null;
}

export interface LogComplaintRequest {
  channel: ComplaintChannel;
  complainantName: string;
  complainantContact: string | null;
  confidential: boolean;
  subject: string;
  description: string;
  branchId: string | null;
  departmentId: string | null;
}

export interface ValidateComplaintRequest { justified: boolean; reason: string; }
export interface LogComplaintOutcomeRequest { outcome: string; }
export interface ResolveComplaintRequest { resolution: string; }

export interface NotificationFeedItem {
  id: string;
  eventKey: string;
  subject: string;
  body: string;
  read: boolean;
  emailStatus: string;
  createdAtUtc: string;
}

/** Server identifier envelope returned by create endpoints (e.g. { id }). */
export interface CreatedResource { id: string; }

// ── Document Control ─────────────────────────────────────────────────────────

/** Semantic-version bump kinds accepted by the backend for a new document version. */
export const VERSION_BUMPS = ['Minor', 'Major'] as const;
export type VersionBump = (typeof VERSION_BUMPS)[number];

export interface DocumentVersion {
  id: string;
  version: string;
  state: string;
  fileId: string;
  changeSummary: string;
  authorId: string;
  recommendedBy: string | null;
  recommendedAtUtc: string | null;
  approvedBy: string | null;
  approvedAtUtc: string | null;
  rejectionReason: string | null;
}

export interface DocumentListItem {
  id: string;
  code: string;
  title: string;
  category: string;
  status: string;
  publishedVersion: string | null;
  createdAtUtc: string;
}

export interface DocumentDetail {
  id: string;
  code: string;
  title: string;
  category: string;
  status: string;
  createdAtUtc: string;
  versions: DocumentVersion[];
  reviewCycleMonths: number;
  nextReviewDue: string | null;
}

/** Metadata returned after a file is uploaded to object storage. */
export interface FileUploaded {
  id: string;
  fileName: string;
  sha256: string;
  sizeBytes: number;
}

export interface CreateDocumentRequest {
  code: string;
  title: string;
  category: string;
  fileId: string;
  changeSummary: string;
  reviewCycleMonths: number;
}

export interface DraftNewVersionRequest { fileId: string; changeSummary: string; bump: VersionBump; }
export interface RejectVersionRequest { reason: string; }
export interface PublishDocumentRequest { password: string; pin: string; }

// ── User Management ──────────────────────────────────────────────────────────

export interface UserAccount {
  id: string;
  email: string;
  displayName: string;
  role: string;
  isActive: boolean;
  mfaEnabled: boolean;
}

/** Lightweight directory entry for user pickers. */
export interface UserDirectoryEntry { id: string; displayName: string; role: string; }

export interface RegisterUserRequest {
  email: string;
  displayName: string;
  role: TenantRole;
  initialPassword: string;
}

export interface ChangeUserRoleRequest { role: TenantRole; }
export interface ResetUserPasswordRequest { newPassword: string; }

// ── Platform administration (control plane) ─────────────────────────────────

export interface Tenant {
  id: string;
  identifier: string;
  name: string;
  status: string;
  createdAtUtc: string;
}

/** Provisions a tenant together with its first tenant administrator (atomic). */
export interface ProvisionTenantRequest {
  identifier: string;
  name: string;
  adminEmail: string;
  adminDisplayName: string;
  adminPassword: string;
}

// ── Audit Management ─────────────────────────────────────────────────────────

export const AUDIT_TYPES = ['Internal', 'ExternalHosted'] as const;
export type AuditType = (typeof AUDIT_TYPES)[number];

export const CHECKLIST_VERDICTS = ['Conform', 'Ofi', 'NonConform'] as const;
export type ChecklistVerdict = (typeof CHECKLIST_VERDICTS)[number];

export const FINDING_GRADES = ['Ofi', 'MinorNc', 'MajorNc'] as const;
export type FindingGrade = (typeof FINDING_GRADES)[number];

export interface ChecklistItem {
  id: string;
  isoClause: string;
  question: string;
  verdict: string;
  evidence: string | null;
}

export interface AuditFinding {
  id: string;
  grade: string;
  description: string;
  ncId: string | null;
}

export interface AuditListItem {
  id: string;
  auditRef: string;
  title: string;
  type: string;
  status: string;
  leadAuditorId: string;
  plannedDate: string;
  createdAtUtc: string;
  branchId: string | null;
  departmentId: string | null;
}

export interface AuditDetail {
  id: string;
  auditRef: string;
  title: string;
  type: string;
  status: string;
  leadAuditorId: string;
  plannedDate: string;
  signedOffBy: string | null;
  signedOffAtUtc: string | null;
  checklist: ChecklistItem[];
  findings: AuditFinding[];
}

export interface ChecklistItemRequest { isoClause: string; question: string; }
export interface ScheduleAuditRequest {
  title: string;
  type: AuditType;
  leadAuditorId: string;
  plannedDate: string;
  checklist: ChecklistItemRequest[];
  branchId: string | null;
  departmentId: string | null;
}
export interface AnswerChecklistItemRequest { verdict: ChecklistVerdict; evidence: string | null; }
export interface RaiseFindingRequest { grade: FindingGrade; description: string; }

// ── Equipment & Calibration ──────────────────────────────────────────────────

export interface CalibrationRecord {
  id: string;
  performedAt: string;
  provider: string;
  result: string;
  certificateFileId: string | null;
}

export interface MaintenanceRecord { id: string; performedAt: string; workDescription: string; }

export interface EquipmentListItem {
  id: string;
  code: string;
  name: string;
  serialNumber: string;
  location: string | null;
  status: string;
  nextCalibrationDue: string | null;
  branchId: string | null;
  departmentId: string | null;
}

export interface EquipmentDetail {
  id: string;
  code: string;
  name: string;
  serialNumber: string;
  location: string | null;
  status: string;
  calibrationIntervalDays: number;
  gracePeriodDays: number;
  lastCalibrationAt: string | null;
  nextCalibrationDue: string | null;
  calibrations: CalibrationRecord[];
  maintenance: MaintenanceRecord[];
  intermediateChecks: IntermediateCheck[];
}

export interface IntermediateCheck {
  id: string;
  performedOn: string;
  performedById: string;
  checkType: string;
  passed: boolean;
  referenceStandardId: string | null;
  remarks: string | null;
}

export interface RecordIntermediateCheckRequest {
  performedOn: string;
  checkType: string;
  passed: boolean;
  referenceStandardId: string | null;
  remarks: string | null;
}

// ── Periodic Audit-Trail Review (21 CFR Part 11 §11.10(e)) ──────────────────

export interface AuditTrailReview {
  id: string;
  reviewRef: string;
  periodStart: string;
  periodEnd: string;
  status: string;
  reviewedBy: string | null;
  completedAtUtc: string | null;
  eventsReviewed: number | null;
  fieldChangesReviewed: number | null;
  anomaliesFound: boolean | null;
  conclusion: string | null;
}

// ── PT/EQA Annual Plan (ISO 17025 §7.7.2) ───────────────────────────────────

export interface PtPlanItem {
  id: string;
  scheme: string;
  analyte: string;
  provider: string | null;
  plannedCycles: number;
  fulfilledCycles: number;
  lastEnrollmentRef: string | null;
  notes: string | null;
}

export interface PtPlanListItem {
  id: string;
  planRef: string;
  year: number;
  status: string;
  itemCount: number;
  plannedCycles: number;
  fulfilledCycles: number;
}

export interface PtPlanDetail {
  id: string;
  planRef: string;
  year: number;
  status: string;
  approvedBy: string | null;
  approvedAtUtc: string | null;
  closureSummary: string | null;
  items: PtPlanItem[];
}

// ── Environmental & Facility Monitoring (ISO 17025 §6.3) ────────────────────

export interface EnvironmentalReading {
  id: string;
  value: number;
  recordedAtUtc: string;
  recordedById: string;
  inLimit: boolean;
  remark: string | null;
}

export interface MonitoringPointListItem {
  id: string;
  pointRef: string;
  name: string;
  location: string | null;
  parameter: string;
  unit: string;
  lowLimit: number | null;
  highLimit: number | null;
  status: string;
  lastValue: number | null;
  lastRecordedAtUtc: string | null;
  lastInLimit: boolean | null;
  excursionCount: number;
  branchId: string | null;
  departmentId: string | null;
}

export interface MonitoringPointDetail {
  id: string;
  pointRef: string;
  name: string;
  location: string | null;
  parameter: string;
  unit: string;
  lowLimit: number | null;
  highLimit: number | null;
  status: string;
  branchId: string | null;
  departmentId: string | null;
  readings: EnvironmentalReading[];
}

export interface RegisterMonitoringPointRequest {
  name: string;
  location: string | null;
  parameter: string;
  unit: string;
  lowLimit: number | null;
  highLimit: number | null;
  branchId: string | null;
  departmentId: string | null;
}

// ── Personnel Authorization Matrix (ISO 17025 §6.2.6) ───────────────────────

export const AUTHORIZATION_SCOPES = ['Perform', 'ReviewAndRelease', 'Train'] as const;
export type AuthorizationScope = (typeof AUTHORIZATION_SCOPES)[number];

export interface TestAuthorizationListItem {
  id: string;
  userId: string;
  testCatalogItemId: string;
  testCode: string;
  testName: string;
  scope: string;
  status: string;
  grantedOn: string;
  expiresOn: string;
}

export interface TestAuthorizationDetail {
  id: string;
  userId: string;
  testCatalogItemId: string;
  testCode: string;
  testName: string;
  competencyRecordId: string;
  competencySubject: string | null;
  scope: string;
  status: string;
  grantedBy: string;
  grantedOn: string;
  expiresOn: string;
  suspensionReason: string | null;
  revocationReason: string | null;
}

export interface GrantTestAuthorizationRequest {
  userId: string;
  testCatalogItemId: string;
  competencyRecordId: string;
  scope: AuthorizationScope;
}

// ── Metrological Traceability (ISO 17025 §6.5) ──────────────────────────────

export const REFERENCE_STANDARD_TYPES = ['CertifiedReferenceMaterial', 'ReferenceStandard', 'WorkingStandard'] as const;
export type ReferenceStandardType = (typeof REFERENCE_STANDARD_TYPES)[number];

export interface ReferenceStandardListItem {
  id: string;
  standardRef: string;
  name: string;
  type: string;
  traceableTo: string;
  status: string;
  expiresOn: string | null;
  branchId: string | null;
  departmentId: string | null;
}

export interface ReferenceStandardDetail {
  id: string;
  standardRef: string;
  name: string;
  type: string;
  traceableTo: string;
  manufacturer: string | null;
  lotNumber: string | null;
  certificateNumber: string | null;
  certifiedValue: string | null;
  uncertaintyStatement: string | null;
  receivedOn: string;
  expiresOn: string | null;
  status: string;
  quarantineReason: string | null;
  branchId: string | null;
  departmentId: string | null;
}

export interface RegisterReferenceStandardRequest {
  name: string;
  type: ReferenceStandardType;
  traceableTo: string;
  manufacturer: string | null;
  lotNumber: string | null;
  certificateNumber: string | null;
  certifiedValue: string | null;
  uncertaintyStatement: string | null;
  receivedOn: string;
  expiresOn: string | null;
  branchId: string | null;
  departmentId: string | null;
}

export interface RegisterEquipmentRequest {
  name: string;
  serialNumber: string;
  location: string | null;
  calibrationIntervalDays: number;
  gracePeriodDays: number;
  branchId: string | null;
  departmentId: string | null;
}
export interface LogCalibrationRequest { performedAt: string; provider: string; result: string; certificateFileId: string | null; }
export interface LogMaintenanceRequest { performedAt: string; workDescription: string; }

// ── Competency & Training ────────────────────────────────────────────────────

export interface AssessmentResult { id: string; score: number; assessorId: string; assessedAtUtc: string; }

export interface CompetencyListItem {
  id: string;
  traineeId: string;
  subject: string;
  status: string;
  expiresAt: string | null;
}

export interface CompetencyDetail {
  id: string;
  traineeId: string;
  subject: string;
  documentId: string | null;
  status: string;
  validityMonths: number;
  expiresAt: string | null;
  authorizedBy: string | null;
  revocationReason: string | null;
  assessments: AssessmentResult[];
}

export interface AssignCompetencyRequest { traineeId: string; subject: string; documentId: string | null; validityMonths: number; }
export interface ScoreAssessmentRequest { score: number; }
export interface RevokeCompetencyRequest { reason: string; }

export interface TrainingAssignment {
  id: string;
  traineeId: string;
  subject: string;
  documentId: string | null;
  dueDate: string;
  completed: boolean;
  completedAtUtc: string | null;
}

export interface AssignTrainingRequest { traineeId: string; subject: string; documentId: string | null; dueDate: string; }

// ── Risk & Governance ────────────────────────────────────────────────────────

/** Common risk categories (backend accepts any string; these avoid magic-string drift). */
export const RISK_CATEGORIES = ['Operational', 'Technical', 'Compliance', 'Safety', 'Financial', 'Strategic'] as const;
export type RiskCategory = (typeof RISK_CATEGORIES)[number];

/** Residual RPN above this threshold is flagged high-risk (mirrors RiskItem.HighResidualThreshold). */
export const HIGH_RESIDUAL_RPN_THRESHOLD = 12;

export interface MitigationAction {
  id: string;
  description: string;
  ownerId: string;
  dueDate: string;
  completed: boolean;
}

export interface RiskListItem {
  id: string;
  riskRef: string;
  title: string;
  category: string;
  status: string;
  rpn: number;
  residualRpn: number | null;
  branchId: string | null;
  departmentId: string | null;
}

export interface RiskDetail {
  id: string;
  riskRef: string;
  title: string;
  category: string;
  status: string;
  likelihood: number;
  impact: number;
  rpn: number;
  residualLikelihood: number | null;
  residualImpact: number | null;
  residualRpn: number | null;
  actions: MitigationAction[];
}

export interface AssessRiskRequest { title: string; category: string; likelihood: number; impact: number; branchId: string | null; departmentId: string | null; }
export interface AddMitigationRequest { description: string; ownerId: string; dueDate: string; }
export interface ResidualAssessmentRequest { likelihood: number; impact: number; }

// ── Change Control ───────────────────────────────────────────────────────────

export interface ChangeListItem {
  id: string;
  changeRef: string;
  title: string;
  status: string;
  riskItemId: string | null;
  branchId: string | null;
  departmentId: string | null;
}

export interface ChangeDetail {
  id: string;
  changeRef: string;
  title: string;
  impactAnalysis: string;
  status: string;
  proposedBy: string;
  riskItemId: string | null;
  approvedBy: string | null;
  approvedAtUtc: string | null;
  rejectionReason: string | null;
  implementationNotes: string | null;
}

export interface ProposeChangeRequest { title: string; impactAnalysis: string; branchId: string | null; departmentId: string | null; }
export interface LinkRiskRequest { riskItemId: string; }
export interface RejectChangeRequest { reason: string; }
export interface CloseChangeRequest { implementationNotes: string; }

// ── Management Review ────────────────────────────────────────────────────────

export interface ReviewDecision {
  id: string;
  description: string;
  ownerId: string;
  dueDate: string;
}

export interface ReviewListItem {
  id: string;
  reviewRef: string;
  title: string;
  reviewDate: string;
  status: string;
  decisionCount: number;
  branchId: string | null;
  departmentId: string | null;
}

export interface ReviewDetail {
  id: string;
  reviewRef: string;
  title: string;
  reviewDate: string;
  participants: string;
  status: string;
  minutes: string | null;
  closedBy: string | null;
  decisions: ReviewDecision[];
}

export interface ScheduleReviewRequest { title: string; reviewDate: string; participants: string; branchId: string | null; departmentId: string | null; }
export interface AddDecisionRequest { description: string; ownerId: string; dueDate: string; }
export interface CloseReviewRequest { minutes: string; }

// ── Supplier Quality ─────────────────────────────────────────────────────────

/** Common supplier classifications (backend accepts any string; kept for consistency). */
export const SUPPLIER_TYPES = ['Reagents', 'Consumables', 'Equipment', 'Calibration', 'ReferenceMaterials', 'Services'] as const;
export type SupplierType = (typeof SUPPLIER_TYPES)[number];

export interface SupplierCertificate {
  id: string;
  certificateType: string;
  expiresAt: string;
  fileId: string | null;
}

export interface SupplierListItem {
  id: string;
  supplierRef: string;
  name: string;
  supplierType: string;
  status: string;
  branchId: string | null;
  departmentId: string | null;
}

export interface SupplierDetail {
  id: string;
  supplierRef: string;
  name: string;
  supplierType: string;
  status: string;
  registeredBy: string;
  approvedBy: string | null;
  suspensionReason: string | null;
  certificates: SupplierCertificate[];
}

export interface SupplierEvaluation {
  id: string;
  supplierId: string;
  periodStart: string;
  periodEnd: string;
  weightedTotal: number;
  evaluatedBy: string;
  criteriaJson: string;
}

export interface RegisterSupplierRequest { name: string; supplierType: string; branchId: string | null; departmentId: string | null; }
export interface AddCertificateRequest { certificateType: string; expiresAt: string; fileId: string | null; }
export interface SuspendSupplierRequest { reason: string; }
export interface EvaluationCriterion { criterion: string; weight: number; score: number; }
export interface RecordEvaluationRequest { periodStart: string; periodEnd: string; criteria: EvaluationCriterion[]; }

// ── Analytical Quality: QC / Westgard ────────────────────────────────────────

export interface QcProfile {
  id: string;
  analyte: string;
  instrument: string;
  controlLot: string;
  targetMean: number;
  targetSd: number;
  isActive: boolean;
}

export interface QcRun {
  id: string;
  profileId: string;
  value: number;
  zScore: number;
  outcome: string;
  violatedRules: string;
  operator: string;
  measuredAtUtc: string;
  troubleshootingNote: string | null;
}

export interface CreateQcProfileRequest { analyte: string; instrument: string; controlLot: string; targetMean: number; targetSd: number; }
export interface RecordQcRunRequest { value: number; operator: string; }
export interface QcTroubleshootRequest { note: string; }

// ── Analytical Quality: Method Validation ────────────────────────────────────

export interface StudyReplicate { id: string; level: string; measured: number; reference: number | null; }

export interface ValidationStudyListItem {
  id: string;
  studyRef: string;
  analyte: string;
  protocol: string;
  state: string;
  passed: boolean | null;
}

export interface ValidationStudyDetail {
  id: string;
  studyRef: string;
  analyte: string;
  protocol: string;
  totalAllowableError: number;
  state: string;
  meanBias: number | null;
  cv: number | null;
  passed: boolean | null;
  signedOffBy: string | null;
  signedOffAtUtc: string | null;
  replicates: StudyReplicate[];
}

export interface ConfigureStudyRequest { analyte: string; protocol: string; totalAllowableError: number; }
export interface EnterReplicateRequest { level: string; measured: number; reference: number | null; }

// ── Analytical Quality: Proficiency Testing ──────────────────────────────────

export interface PtEnrollment {
  id: string;
  ptRef: string;
  scheme: string;
  analyte: string;
  cycle: string;
  submittedValue: number | null;
  assignedValue: number | null;
  zScore: number | null;
  performance: string;
}

export interface EnrollPtRequest { scheme: string; analyte: string; cycle: string; }
export interface RecordPtResultRequest { submitted: number; assigned: number; standardDeviation: number; }

// ── Records & Retention ──────────────────────────────────────────────────────

export const RETENTION_CLASSES = ['FiveYears', 'TenYears', 'Permanent'] as const;
export type RetentionClass = (typeof RETENTION_CLASSES)[number];

/** Modules whose records are archived (informative options for the archive form). */
export const ARCHIVE_SOURCE_MODULES = [
  'Nonconformance', 'Audit', 'Document', 'Equipment', 'Competency',
  'Risk', 'Change', 'ManagementReview', 'Supplier', 'ValidationStudy',
] as const;

export interface ArchiveListItem {
  id: string;
  archiveRef: string;
  sourceModule: string;
  sourceRef: string;
  retentionClass: string;
  archivedOn: string;
  retentionExpiry: string | null;
  state: string;
}

export interface ArchiveRecordRequest {
  sourceModule: string;
  sourceRef: string;
  snapshotFileId: string | null;
  retentionClass: RetentionClass;
}

// ── Organization & Reference Data ────────────────────────────────────────────

export interface Branch {
  id: string;
  code: string;
  name: string;
  city: string | null;
  isActive: boolean;
}

export interface Department {
  id: string;
  branchId: string;
  code: string;
  name: string;
  isActive: boolean;
}

export interface TestCatalogItem {
  id: string;
  testCode: string;
  testName: string;
  methodology: string;
  turnaroundHours: number;
  isActive: boolean;
}

export interface LovEntry {
  id: string;
  category: string;
  code: string;
  nameEn: string;
  nameAr: string | null;
  nameFr: string | null;
  sortOrder: number;
  isActive: boolean;
}

export interface CreateBranchRequest { code: string; name: string; city: string | null; }
export interface CreateDepartmentRequest { branchId: string; code: string; name: string; }
export interface CreateTestRequest { testCode: string; testName: string; methodology: string; turnaroundHours: number; }
export interface UpsertLovRequest {
  category: string;
  code: string;
  nameEn: string;
  nameAr: string | null;
  nameFr: string | null;
  sortOrder: number;
}

// ── Notifications administration ─────────────────────────────────────────────

/** Event keys the backend dispatcher raises (NotificationPolicies constants). */
export const NOTIFICATION_EVENT_KEYS = [
  'NC_RAISED', 'DOC_PUBLISHED', 'EQUIP_CALIB_DUE', 'EQUIP_LOCKED_OUT',
  'COMP_EXPIRED', 'RISK_HIGH_RESIDUAL', 'SUP_SUSPENDED', 'SLA_ESCALATED',
] as const;
export type NotificationEventKey = (typeof NOTIFICATION_EVENT_KEYS)[number];

export interface NotificationRule {
  id: string;
  eventKey: string;
  recipientRoles: string;
  emailEnabled: boolean;
  subjectTemplate: string;
  bodyTemplate: string;
  isActive: boolean;
}

export interface UpsertNotificationRuleRequest {
  eventKey: string;
  recipientRoles: string;
  emailEnabled: boolean;
  subjectTemplate: string;
  bodyTemplate: string;
}

export interface DispatchMonitorItem {
  id: string;
  eventKey: string;
  recipientUserId: string;
  recipientEmail: string | null;
  subject: string;
  emailStatus: string;
  error: string | null;
  createdAtUtc: string;
}

// ── Tasks & SLA ──────────────────────────────────────────────────────────────

export interface WorkTask {
  id: string;
  subject: string;
  subjectRef: string | null;
  assigneeUserId: string | null;
  assigneeRole: string | null;
  dueDate: string;
  status: string;
  overdue: boolean;
}

/** A task must name a user or a role (TASK-002). */
export interface CreateTaskRequest {
  subject: string;
  subjectRef: string | null;
  assigneeUserId: string | null;
  assigneeRole: string | null;
  dueDate: string;
}

export interface SlaDefinition {
  id: string;
  module: string;
  severity: string;
  targetHours: number;
}

export interface UpsertSlaRequest { module: string; severity: string; targetHours: number; }

// ── Measurement Uncertainty (ISO 17025 §7.6) ─────────────────────────────────

export const UNCERTAINTY_COMPONENT_TYPES = ['TypeA', 'TypeB'] as const;
export type UncertaintyComponentType = (typeof UNCERTAINTY_COMPONENT_TYPES)[number];

export interface UncertaintyComponent {
  id: string;
  name: string;
  type: string;
  relativeStandardUncertainty: number;
  source: string | null;
}

export interface UncertaintyBudgetListItem {
  id: string;
  budgetRef: string;
  analyte: string;
  method: string;
  level: string;
  status: string;
  expandedUncertainty: number | null;
  meetsTarget: boolean | null;
}

export interface UncertaintyBudgetDetail {
  id: string;
  budgetRef: string;
  analyte: string;
  method: string;
  unit: string;
  level: string;
  coverageFactor: number;
  targetExpandedUncertainty: number | null;
  status: string;
  combinedStandardUncertainty: number | null;
  expandedUncertainty: number | null;
  meetsTarget: boolean | null;
  approvedBy: string | null;
  approvedAtUtc: string | null;
  components: UncertaintyComponent[];
}

export interface CreateUncertaintyBudgetRequest {
  analyte: string;
  method: string;
  unit: string;
  level: string;
  coverageFactor: number;
  targetExpandedUncertainty: number | null;
}

export interface AddUncertaintyComponentRequest {
  name: string;
  type: UncertaintyComponentType;
  relativeStandardUncertainty: number;
  source: string | null;
}

// ── Compliance Ledger ────────────────────────────────────────────────────────

/** One hash-chained, append-only audit-trail entry. */
export interface AuditTrailEntry {
  id: string;
  tenantId: string;
  sequence: number;
  eventId: string;
  eventType: string;
  payload: string;
  occurredAtUtc: string;
  prevHash: string;
  entryHash: string;
}

/** One field-level change row (Part 11 §11.10(e)): who changed what, from → to. */
export interface FieldChange {
  id: string;
  tenantId: string | null;
  entityType: string;
  entityId: string;
  action: string;
  property: string | null;
  oldValue: string | null;
  newValue: string | null;
  actorId: string | null;
  actor: string;
  occurredAtUtc: string;
}

/** A 21 CFR Part 11 electronic-signature record (§11.50/§11.70). */
export interface SignatureRecord {
  id: string;
  tenantId: string;
  signerId: string;
  signerDisplay: string;
  meaning: string;
  subjectRef: string;
  contentHash: string;
  signedAtUtc: string;
}

/** A security-relevant event (logins, lockouts, MFA challenges…). */
export interface SecurityEvent {
  id: string;
  tenantId: string | null;
  eventType: string;
  actor: string | null;
  ipAddress: string | null;
  detail: string | null;
  occurredAtUtc: string;
}

/** Result of on-demand hash-chain verification over the tenant's audit trail. */
export interface ChainVerification {
  ok: boolean;
  verifiedEntries: number;
  brokenAtSequence: number | null;
}
