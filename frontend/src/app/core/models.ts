// API contracts mirrored from NT.QAMS.Contracts (kept intentionally thin, strongly typed).

/** Authentication result returned by POST /api/auth/login. */
export interface AuthResponse {
  accessToken: string;
  expiresAtUtc: string;
  role: string;
  displayName: string;
  tenantId: string | null;
  mfaRequired: boolean;
  /** Privileged user of an MFA-enforcing tenant who must enrol before full access (F-04). */
  mfaEnrollmentRequired?: boolean;
}

/** Nonconformance source classifications accepted by the backend. */
export const NC_SOURCE_TYPES = ['Internal', 'Complaint', 'Audit', 'Supplier', 'ProficiencyTest'] as const;
export type NcSourceType = (typeof NC_SOURCE_TYPES)[number];

/** Quality-event classification (F-11): plain NC, deviation, out-of-spec, out-of-trend. */
export const QUALITY_EVENT_TYPES = ['Nonconformity', 'Deviation', 'OutOfSpecification', 'OutOfTrend'] as const;
export type QualityEventType = (typeof QUALITY_EVENT_TYPES)[number];

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
  eventType: string;
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
  eventType: string;
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
  eventType: QualityEventType;
}

export interface TriageNcRequest { assigneeId: string; }
export interface RejectNcRequest { reason: string; }
export interface RecordRcaRequest { method: RcaMethod; analysis: string; }
export interface PlanCapaActionRequest { type: CapaActionType; details: string; ownerId: string; dueDate: string; }
export interface VerifyNcRequest { passed: boolean; password: string; pin: string; }
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
  totals: DashboardKpiTotals;
}

/** The population each dashboard KPI is a subset of, for proportion tiles. */
export interface DashboardKpiTotals {
  nonconformances: number;
  capaActions: number;
  complaints: number;
  audits: number;
  equipmentItems: number;
  risks: number;
  workTasks: number;
  ptEnrollments: number;
  trainingAssignments: number;
  suppliers: number;
  documents: number;
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

// ── Quality analytics ────────────────────────────────────────────────────────
// Mirrors NT.QAMS.Contracts.Reporting. A `null` percentage means "no population
// to measure", which the UI must render as an em dash — never as 0%.

export interface CategoryCount { label: string; count: number; }

// ── Page export (PDF / Excel copies of a register view) ─────────────────────

export interface PageExportStat { label: string; value: string; tone: string | null; }

/** The caller's own view of a register: title, filters in force, tiles, grid. */
export interface PageExportRequest {
  title: string;
  filtersSummary: string | null;
  stats: PageExportStat[];
  columns: string[];
  rows: string[][];
}

export interface AnalyticsRow {
  reference: string;
  title: string;
  detail: string | null;
  status: string;
}

export interface DocumentControlStats {
  totalActive: number;
  current: number;
  percentCurrent: number | null;
  overdueReviews: number;
  dueWithin30: number;
  due31To60: number;
  due61To90: number;
  acknowledgementsRecorded: number;
  upcomingReviews: AnalyticsRow[];
}

export interface NcCapaStats {
  openNcs: number;
  totalNcs: number;
  overdueCapa: number;
  totalCapa: number;
  capaClosedOnTime: number;
  capaClosedTotal: number;
  capaOnTimePercent: number | null;
  /** Not-overdue rate — the figure the composite score uses. */
  capaOnSchedulePercent: number | null;
  byStatus: CategoryCount[];
  bySource: CategoryCount[];
  byDepartment: CategoryCount[];
  active: AnalyticsRow[];
}

export interface ComplaintsStats {
  open: number;
  total: number;
  resolvedWithinSla: number;
  resolvedTotal: number;
  percentWithinSla: number | null;
  averageResolutionDays: number | null;
  byChannel: CategoryCount[];
  active: AnalyticsRow[];
}

export interface AuditStats {
  completed: number;
  totalPlanned: number;
  planCompletionPercent: number | null;
  majorFindings: number;
  minorFindings: number;
  observations: number;
  recent: AnalyticsRow[];
}

export interface EquipmentStats {
  total: number;
  calibrationCurrent: number;
  calibrationCompliancePercent: number | null;
  outOfService: number;
  availabilityPercent: number | null;
  overdueCalibration: number;
  byStatus: CategoryCount[];
  upcomingCalibrations: AnalyticsRow[];
}

export interface CompetencyStats {
  authorized: number;
  total: number;
  percentCompetent: number | null;
  expiringWithin90: number;
  revoked: number;
  pendingTraining: number;
  recent: AnalyticsRow[];
}

export interface PtStats {
  satisfactory: number;
  questionable: number;
  unsatisfactory: number;
  pending: number;
  total: number;
  satisfactionRatePercent: number | null;
  recent: AnalyticsRow[];
}

export interface SupplierStats {
  approved: number;
  total: number;
  approvedPercent: number | null;
  suspended: number;
  averageEvaluationScore: number | null;
  recent: AnalyticsRow[];
}

export interface RiskMatrixCell { likelihood: number; impact: number; count: number; }

export interface RiskStats {
  highOrExtreme: number;
  total: number;
  highMitigated: number;
  highMitigatedPercent: number | null;
  overdueTreatments: number;
  matrix: RiskMatrixCell[];
  top: AnalyticsRow[];
}

export interface QualityHealthComponent {
  category: string;
  weight: number;
  achievedScore: number | null;
  contributed: boolean;
  excludedReason: string | null;
}

export interface QualityHealthScore {
  score: number | null;
  components: QualityHealthComponent[];
  contributingCategories: number;
  totalCategories: number;
}

export interface QualityAnalyticsScope {
  branchId: string | null;
  departmentId: string | null;
  filterApplied: boolean;
  /** Sections a branch/department filter could not narrow (no attribution on the record). */
  unscopedSections: string[];
  /** Sections omitted because the caller lacks the module's view permission. */
  hiddenSections: string[];
}

export interface QualityAnalytics {
  health: QualityHealthScore;
  documentControl: DocumentControlStats | null;
  ncCapa: NcCapaStats | null;
  complaints: ComplaintsStats | null;
  audits: AuditStats | null;
  equipment: EquipmentStats | null;
  competency: CompetencyStats | null;
  proficiencyTesting: PtStats | null;
  suppliers: SupplierStats | null;
  risk: RiskStats | null;
  scope: QualityAnalyticsScope;
  computedAtUtc: string;
}

export interface QualityHealthWeight { category: string; weight: number; }
export interface QualityHealthProfile { weights: QualityHealthWeight[]; }

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

/** Pagination envelope returned by list endpoints; mirrors the backend PagedResponse (EA finding API-004). */
export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number; hasMore: boolean; }

/** Default page size requested by list screens (matches the API-004 envelope default). */
export const DEFAULT_PAGE_SIZE = 50;

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

/** Whether the current user has acknowledged the document's current published version. */
export interface MyDocumentAcknowledgement {
  publishedVersion: string | null;
  acknowledged: boolean;
  acknowledgedAtUtc: string | null;
}

export interface DocumentAcknowledgement {
  userId: string;
  userDisplay: string;
  versionLabel: string;
  acknowledgedAtUtc: string;
}

export interface ControlledCopy {
  id: string;
  copyNumber: number;
  versionLabel: string;
  holder: string;
  status: string;
  issuedBy: string;
  issuedAtUtc: string;
  closedAtUtc: string | null;
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
  roleId: string | null;
  roleName: string | null;
  branchIds: string[];
  departmentIds: string[];
  preferredLanguage: string | null;
  /** Whether an e-signature PIN is on file — the fact only, never the value. */
  pinConfigured: boolean;
}

// ── Roles & privileges ────────────────────────────────────────────────────────

/** One configurable module of the privilege matrix. */
export interface PermissionModule {
  key: string;
  group: string;
  nameKey: string;
  actions: string[];
}

/** The whole permission catalogue, in render order. */
export interface PermissionCatalog {
  modules: PermissionModule[];
  actions: string[];
}

/** A role as listed in the privileges screen. */
export interface RoleSummary {
  id: string;
  name: string;
  description: string | null;
  isSystem: boolean;
  isActive: boolean;
  defaultLanguage: string | null;
  permissionCount: number;
  memberCount: number;
}

/** A role opened for editing: the summary plus its granted keys. */
export interface RoleDetail {
  id: string;
  name: string;
  description: string | null;
  isSystem: boolean;
  isActive: boolean;
  defaultLanguage: string | null;
  permissionKeys: string[];
  memberCount: number;
}

/**
 * The signed-in user's effective privileges: what the UI should offer. The
 * server enforces the same facts independently on every request.
 */
export interface MyPrivileges {
  roleId: string | null;
  roleName: string | null;
  isPlatformAdmin: boolean;
  permissions: string[];
  branchIds: string[];
  departmentIds: string[];
  preferredLanguage: string | null;
  /** Whether an e-signature PIN is on file — the fact only, never the value. */
  pinConfigured: boolean;
}

/** Lightweight directory entry for user pickers. */
export interface UserDirectoryEntry { id: string; displayName: string; role: string; }

export interface RegisterUserRequest {
  email: string;
  displayName: string;
  role: TenantRole;
  initialPassword: string;
  roleId?: string | null;
  /** Optional admin-issued signing PIN (4 digits); the user can rotate it later. */
  initialPin?: string | null;
}

export interface SetUserPinRequest { pin: string; }

export interface CreateRoleRequest {
  name: string;
  description: string | null;
  permissionKeys: string[];
  defaultLanguage: string | null;
}

export interface UpdateRoleRequest {
  name: string;
  description: string | null;
  defaultLanguage: string | null;
}

export interface SetRolePermissionsRequest { permissionKeys: string[]; reason: string; }
export interface AssignUserRoleRequest { roleId: string; }
export interface SetUserScopeRequest { branchIds: string[]; departmentIds: string[]; }
export interface SetUserLanguageRequest { language: string | null; }

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

export interface MaintenanceRecord { id: string; performedAt: string; workDescription: string; certificateFileId: string | null; }

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

// ── Impartiality / COI Register (ISO 17025 §4.1) ────────────────────────────

export const CONFLICT_RISK_LEVELS = ['Low', 'Medium', 'High'] as const;
export const CONFLICT_OUTCOMES = ['Accepted', 'Mitigated', 'Withdrawn'] as const;

export interface ConflictListItem {
  id: string;
  conflictRef: string;
  declarantId: string;
  relatedParty: string;
  declaredOn: string;
  status: string;
  riskLevel: string | null;
  outcome: string | null;
}

export interface ConflictDetail extends ConflictListItem {
  description: string;
  mitigation: string | null;
  assessedBy: string | null;
  closureNote: string | null;
}

// ── Context & Interested Parties (ISO 9001 §4.1/§4.2) ───────────────────────

export interface InterestedParty {
  id: string;
  partyRef: string;
  name: string;
  category: string;
  needsAndExpectations: string;
  relevantRequirements: string | null;
  reviewedOn: string;
  status: string;
}

export interface ContextIssue {
  id: string;
  issueRef: string;
  type: string;
  category: string;
  description: string;
  impact: string;
  linkedRiskId: string | null;
  status: string;
  resolution: string | null;
}

// ── Quality Objectives & Targets (ISO 9001 §6.2 / ISO 17025 §8.2) ───────────

export const OBJECTIVE_DIRECTIONS = ['AtLeast', 'AtMost'] as const;
export type ObjectiveDirection = (typeof OBJECTIVE_DIRECTIONS)[number];

export interface ObjectiveProgress {
  id: string;
  measuredOn: string;
  value: number;
  recordedById: string;
  comment: string | null;
}

export interface QualityPolicy {
  id: string;
  policyRef: string;
  version: number;
  statement: string;
  status: string;
  effectiveDate: string | null;
  approvedById: string | null;
  approvedAtUtc: string | null;
}

export interface QualityObjectiveListItem {
  id: string;
  objectiveRef: string;
  title: string;
  metric: string;
  unit: string;
  targetValue: number;
  direction: string;
  ownerId: string;
  periodStart: string;
  periodEnd: string;
  status: string;
  currentValue: number | null;
  onTarget: boolean | null;
  branchId: string | null;
  departmentId: string | null;
}

export interface QualityObjectiveDetail extends QualityObjectiveListItem {
  description: string | null;
  closureNote: string | null;
  updates: ObjectiveProgress[];
}

export interface DefineQualityObjectiveRequest {
  title: string;
  description: string | null;
  metric: string;
  unit: string;
  targetValue: number;
  direction: ObjectiveDirection;
  ownerId: string;
  periodStart: string;
  periodEnd: string;
  branchId: string | null;
  departmentId: string | null;
}

// ── General Feedback & Satisfaction (ISO 17025 §8.6.2) ──────────────────────

export const FEEDBACK_TYPES = ['Compliment', 'Suggestion', 'Dissatisfaction'] as const;
export type FeedbackType = (typeof FEEDBACK_TYPES)[number];

export interface FeedbackListItem {
  id: string;
  feedbackRef: string;
  source: string;
  channel: string;
  type: string;
  subject: string;
  satisfactionScore: number | null;
  receivedOn: string;
  status: string;
  branchId: string | null;
  departmentId: string | null;
}

export interface FeedbackDetail extends FeedbackListItem {
  details: string;
  loggedBy: string;
  reviewNotes: string | null;
  actionSummary: string | null;
  complaintId: string | null;
}

export interface LogFeedbackRequest {
  source: string;
  channel: string;
  type: FeedbackType;
  subject: string;
  details: string;
  satisfactionScore: number | null;
  receivedOn: string;
  branchId: string | null;
  departmentId: string | null;
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

/** Periodic user-access review / recertification (Part 11 §11.10(d) / Annex 11 §12). */
export interface UserAccessReview {
  id: string;
  reviewRef: string;
  openedOn: string;
  status: string;
  reviewedBy: string | null;
  completedAtUtc: string | null;
  accountsReviewed: number | null;
  changesRequired: boolean | null;
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

// ── Bulk import (LIS / analyzer CSV) ────────────────────────────────────────

export interface BulkReject { row: number; reason: string; }
export interface BulkImportResult { imported: number; rejected: BulkReject[]; }

// ── Method Comparison (CLSI EP09) ───────────────────────────────────────────

export interface MeasurementPair {
  id: string;
  referenceValue: number;
  testValue: number;
  sampleId: string | null;
}

export interface MethodComparisonListItem {
  id: string;
  studyRef: string;
  analyte: string;
  referenceMethod: string;
  testMethod: string;
  state: string;
  pairCount: number | null;
  demingSlope: number | null;
  demingIntercept: number | null;
  pearsonR: number | null;
  meanBias: number | null;
}

export interface MethodComparisonDetail {
  id: string;
  studyRef: string;
  analyte: string;
  unit: string;
  referenceMethod: string;
  testMethod: string;
  state: string;
  pairCount: number | null;
  pearsonR: number | null;
  demingSlope: number | null;
  demingIntercept: number | null;
  passingBablokSlope: number | null;
  passingBablokIntercept: number | null;
  meanBias: number | null;
  biasSd: number | null;
  limitOfAgreementLower: number | null;
  limitOfAgreementUpper: number | null;
  meetsRecommendedPower: boolean;
  signedOffBy: string | null;
  signedOffAtUtc: string | null;
  pairs: MeasurementPair[];
}

export interface CreateMethodComparisonRequest {
  analyte: string;
  unit: string;
  referenceMethod: string;
  testMethod: string;
}

// ── Linearity / AMR (CLSI EP06) ─────────────────────────────────────────────

export interface LinearityMeasurement {
  id: string;
  assignedValue: number;
  measuredValue: number;
}

export interface LinearityLevel {
  assignedValue: number;
  replicateCount: number;
  meanMeasured: number;
  fittedValue: number;
  deviationPct: number;
  recoveryPct: number;
  passes: boolean;
}

export interface LinearityListItem {
  id: string;
  studyRef: string;
  analyte: string;
  method: string;
  state: string;
  isLinear: boolean | null;
  amrLow: number | null;
  amrHigh: number | null;
  slope: number | null;
  correlationR: number | null;
}

export interface LinearityDetail {
  id: string;
  studyRef: string;
  analyte: string;
  unit: string;
  method: string;
  allowableDeviationPct: number;
  state: string;
  slope: number | null;
  intercept: number | null;
  correlationR: number | null;
  isLinear: boolean | null;
  amrLow: number | null;
  amrHigh: number | null;
  signedOffBy: string | null;
  signedOffAtUtc: string | null;
  measurements: LinearityMeasurement[];
  levels: LinearityLevel[];
}

export interface CreateLinearityStudyRequest {
  analyte: string;
  unit: string;
  method: string;
  allowableDeviationPct: number;
}

// ── Detection Capability: LoB / LoD / LoQ (CLSI EP17) ───────────────────────

export const DETECTION_SAMPLE_KINDS = ['Blank', 'LowLevel'] as const;
export type DetectionSampleKind = (typeof DETECTION_SAMPLE_KINDS)[number];

export interface DetectionMeasurement {
  id: string;
  kind: string;
  assignedValue: number | null;
  measuredValue: number;
}

export interface LowLevelAssessment {
  assignedValue: number;
  replicateCount: number;
  mean: number;
  sd: number;
  cvPct: number;
  qualifiesForLoq: boolean;
}

export interface DetectionLimitListItem {
  id: string;
  studyRef: string;
  analyte: string;
  method: string;
  state: string;
  lob: number | null;
  lod: number | null;
  loq: number | null;
}

export interface DetectionLimitDetail {
  id: string;
  studyRef: string;
  analyte: string;
  unit: string;
  method: string;
  loqCvTargetPct: number;
  state: string;
  blankMean: number | null;
  blankSd: number | null;
  pooledLowSd: number | null;
  lob: number | null;
  lod: number | null;
  loq: number | null;
  signedOffBy: string | null;
  signedOffAtUtc: string | null;
  measurements: DetectionMeasurement[];
  lowLevels: LowLevelAssessment[];
}

export interface CreateDetectionLimitStudyRequest {
  analyte: string;
  unit: string;
  method: string;
  loqCvTargetPct: number;
}

// ── Reference-Interval Verification (CLSI EP28) ─────────────────────────────

export interface ReferenceSample {
  id: string;
  value: number;
  subjectRef: string | null;
  outside: boolean;
}

export interface ReferenceIntervalListItem {
  id: string;
  studyRef: string;
  analyte: string;
  population: string;
  claimedLower: number;
  claimedUpper: number;
  state: string;
  outsideCount: number | null;
  allowedOutside: number | null;
  verdict: string | null;
}

export interface ReferenceIntervalDetail {
  id: string;
  studyRef: string;
  analyte: string;
  unit: string;
  population: string;
  source: string;
  claimedLower: number;
  claimedUpper: number;
  state: string;
  sampleCount: number | null;
  outsideCount: number | null;
  allowedOutside: number | null;
  verdict: string | null;
  signedOffBy: string | null;
  signedOffAtUtc: string | null;
  samples: ReferenceSample[];
}

export interface CreateReferenceIntervalStudyRequest {
  analyte: string;
  unit: string;
  population: string;
  source: string;
  claimedLower: number;
  claimedUpper: number;
}

// ── Sigma Metrics ───────────────────────────────────────────────────────────

export interface SigmaAssessmentListItem {
  id: string;
  assessmentRef: string;
  analyte: string;
  allowableTotalErrorPct: number;
  biasPct: number;
  cvPct: number;
  sigmaValue: number;
  grade: string;
  state: string;
}

export interface SigmaAssessmentDetail {
  id: string;
  assessmentRef: string;
  analyte: string;
  unit: string;
  allowableTotalErrorPct: number;
  biasPct: number;
  cvPct: number;
  sigmaValue: number;
  grade: string;
  qcRecommendation: string;
  state: string;
  signedOffBy: string | null;
  signedOffAtUtc: string | null;
}

export interface CreateSigmaAssessmentRequest {
  analyte: string;
  unit: string;
  allowableTotalErrorPct: number;
  biasPct: number;
  cvPct: number;
}

// ── Precision Study (CLSI EP05) ─────────────────────────────────────────────

export interface PrecisionMeasurement {
  id: string;
  runLabel: string;
  value: number;
}

export interface PrecisionRun {
  runLabel: string;
  replicateCount: number;
  mean: number;
}

export interface PrecisionListItem {
  id: string;
  studyRef: string;
  analyte: string;
  level: string;
  state: string;
  repeatabilityCvPct: number | null;
  withinLabCvPct: number | null;
  meetsWithinLabClaim: boolean | null;
}

export interface PrecisionDetail {
  id: string;
  studyRef: string;
  analyte: string;
  unit: string;
  level: string;
  claimedRepeatabilityCvPct: number | null;
  claimedWithinLabCvPct: number | null;
  state: string;
  grandMean: number | null;
  repeatabilitySd: number | null;
  repeatabilityCvPct: number | null;
  betweenRunSd: number | null;
  betweenRunCvPct: number | null;
  withinLabSd: number | null;
  withinLabCvPct: number | null;
  meetsRepeatabilityClaim: boolean | null;
  meetsWithinLabClaim: boolean | null;
  signedOffBy: string | null;
  signedOffAtUtc: string | null;
  measurements: PrecisionMeasurement[];
  runs: PrecisionRun[];
}

export interface CreatePrecisionStudyRequest {
  analyte: string;
  unit: string;
  level: string;
  claimedRepeatabilityCvPct: number | null;
  claimedWithinLabCvPct: number | null;
}

// ── Outlier Detection & Normalisation (Tukey + modified-z) ──────────────────

export interface OutlierPoint {
  id: string;
  value: number;
  label: string | null;
  zScore: number;
  modifiedZScore: number;
  isOutlier: boolean;
}

export interface OutlierScreeningListItem {
  id: string;
  screeningRef: string;
  dataset: string;
  state: string;
  pointCount: number | null;
  outlierCount: number | null;
}

export interface OutlierScreeningDetail {
  id: string;
  screeningRef: string;
  dataset: string;
  unit: string;
  state: string;
  pointCount: number | null;
  mean: number | null;
  sd: number | null;
  median: number | null;
  q1: number | null;
  q3: number | null;
  tukeyLower: number | null;
  tukeyUpper: number | null;
  outlierCount: number | null;
  signedOffBy: string | null;
  signedOffAtUtc: string | null;
  points: OutlierPoint[];
}

export interface CreateOutlierScreeningRequest {
  dataset: string;
  unit: string;
}

// ── Carryover Study (CLSI EP10) ─────────────────────────────────────────────

export const CARRYOVER_KINDS = ['High', 'Low'] as const;
export type CarryoverKind = (typeof CARRYOVER_KINDS)[number];

export interface CarryoverReading {
  id: string;
  kind: string;
  sequence: number;
  value: number;
}

export interface CarryoverListItem {
  id: string;
  studyRef: string;
  analyte: string;
  state: string;
  carryoverPct: number | null;
  passes: boolean | null;
}

export interface CarryoverDetail {
  id: string;
  studyRef: string;
  analyte: string;
  unit: string;
  allowableCarryoverPct: number;
  state: string;
  meanHigh: number | null;
  firstLow: number | null;
  steadyLow: number | null;
  carryoverPct: number | null;
  passes: boolean | null;
  signedOffBy: string | null;
  signedOffAtUtc: string | null;
  readings: CarryoverReading[];
}

export interface CreateCarryoverStudyRequest {
  analyte: string;
  unit: string;
  allowableCarryoverPct: number;
}

// ── Lot-to-Lot Comparison ───────────────────────────────────────────────────

export interface LotPair {
  id: string;
  currentLotValue: number;
  newLotValue: number;
  sampleId: string | null;
}

export interface LotComparisonListItem {
  id: string;
  studyRef: string;
  analyte: string;
  currentLot: string;
  newLot: string;
  state: string;
  meanBiasPct: number | null;
  passes: boolean | null;
}

export interface LotComparisonDetail {
  id: string;
  studyRef: string;
  analyte: string;
  unit: string;
  currentLot: string;
  newLot: string;
  allowableBiasPct: number;
  state: string;
  pairCount: number | null;
  meanCurrent: number | null;
  meanNew: number | null;
  meanBiasPct: number | null;
  passes: boolean | null;
  signedOffBy: string | null;
  signedOffAtUtc: string | null;
  pairs: LotPair[];
}

export interface CreateLotComparisonRequest {
  analyte: string;
  unit: string;
  currentLot: string;
  newLot: string;
  allowableBiasPct: number;
}

// ── Interference / Specificity (CLSI EP07) ──────────────────────────────────

export const INTERFERENCE_KINDS = ['Control', 'Test'] as const;
export type InterferenceKind = (typeof INTERFERENCE_KINDS)[number];

export interface InterferenceMeasurement {
  id: string;
  isControl: boolean;
  interferent: string | null;
  value: number;
}

export interface InterferenceResult {
  interferent: string;
  replicateCount: number;
  meanTest: number;
  biasPct: number;
  significantInterference: boolean;
}

export interface InterferenceListItem {
  id: string;
  studyRef: string;
  analyte: string;
  state: string;
  interferentCount: number | null;
  significantCount: number | null;
}

export interface InterferenceDetail {
  id: string;
  studyRef: string;
  analyte: string;
  unit: string;
  allowableBiasPct: number;
  state: string;
  controlMean: number | null;
  interferentCount: number | null;
  significantCount: number | null;
  signedOffBy: string | null;
  signedOffAtUtc: string | null;
  measurements: InterferenceMeasurement[];
  results: InterferenceResult[];
}

export interface CreateInterferenceStudyRequest {
  analyte: string;
  unit: string;
  allowableBiasPct: number;
}

// ── Instrument-to-Instrument Comparability ──────────────────────────────────

export interface InstrumentReading {
  id: string;
  instrument: string;
  sampleId: string;
  value: number;
}

export interface InstrumentResult {
  instrument: string;
  pairedSamples: number;
  meanBiasPct: number;
  comparable: boolean;
}

export interface InstrumentComparabilityListItem {
  id: string;
  studyRef: string;
  analyte: string;
  referenceInstrument: string;
  state: string;
  instrumentCount: number | null;
  nonComparableCount: number | null;
}

export interface InstrumentComparabilityDetail {
  id: string;
  studyRef: string;
  analyte: string;
  unit: string;
  referenceInstrument: string;
  allowableBiasPct: number;
  state: string;
  instrumentCount: number | null;
  nonComparableCount: number | null;
  signedOffBy: string | null;
  signedOffAtUtc: string | null;
  readings: InstrumentReading[];
  results: InstrumentResult[];
}

export interface CreateInstrumentComparabilityRequest {
  analyte: string;
  unit: string;
  referenceInstrument: string;
  allowableBiasPct: number;
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
export interface LogMaintenanceRequest { performedAt: string; workDescription: string; certificateFileId: string | null; }

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
  changeEffective: boolean | null;
  postImplementationReviewNotes: string | null;
  postImplementationReviewedBy: string | null;
  postImplementationReviewedAtUtc: string | null;
}

export interface ProposeChangeRequest { title: string; impactAnalysis: string; branchId: string | null; departmentId: string | null; }
export interface ReviewChangeRequest { effective: boolean; notes: string; }
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
  agenda: string | null;
  meetingLink: string | null;
}

/** Participants are user ids; names are resolved server-side. Empty meetingLink → one is generated. */
export interface ScheduleReviewRequest {
  title: string;
  reviewDate: string;
  participantUserIds: string[];
  agenda: string | null;
  meetingLink: string | null;
  branchId: string | null;
  departmentId: string | null;
}
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
  criteria: string;
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
  isOnLegalHold: boolean;
}

export interface ArchiveRecordRequest {
  sourceModule: string;
  sourceRef: string;
  snapshotFileId: string;
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

/** Pre-authentication branding for a laboratory's own sign-in address. */
export interface Workspace {
  name: string;
}
