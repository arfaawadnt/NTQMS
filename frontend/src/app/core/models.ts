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
}

export interface TriageNcRequest { assigneeId: string; }
export interface RejectNcRequest { reason: string; }
export interface RecordRcaRequest { method: RcaMethod; analysis: string; }
export interface PlanCapaActionRequest { type: CapaActionType; details: string; ownerId: string; dueDate: string; }
export interface VerifyNcRequest { passed: boolean; }
export interface ConfirmEffectivenessRequest { effective: boolean; }

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
}

export interface DraftNewVersionRequest { fileId: string; changeSummary: string; bump: VersionBump; }
export interface RejectVersionRequest { reason: string; }
export interface PublishDocumentRequest { pin: string; }

// ── User Management ──────────────────────────────────────────────────────────

export interface UserAccount {
  id: string;
  email: string;
  displayName: string;
  role: string;
  isActive: boolean;
  mfaEnabled: boolean;
}

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
}

export interface RegisterEquipmentRequest {
  name: string;
  serialNumber: string;
  location: string | null;
  calibrationIntervalDays: number;
  gracePeriodDays: number;
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

export interface AssessRiskRequest { title: string; category: string; likelihood: number; impact: number; }
export interface AddMitigationRequest { description: string; ownerId: string; dueDate: string; }
export interface ResidualAssessmentRequest { likelihood: number; impact: number; }

// ── Change Control ───────────────────────────────────────────────────────────

export interface ChangeListItem {
  id: string;
  changeRef: string;
  title: string;
  status: string;
  riskItemId: string | null;
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

export interface ProposeChangeRequest { title: string; impactAnalysis: string; }
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

export interface ScheduleReviewRequest { title: string; reviewDate: string; participants: string; }
export interface AddDecisionRequest { description: string; ownerId: string; dueDate: string; }
export interface CloseReviewRequest { minutes: string; }
