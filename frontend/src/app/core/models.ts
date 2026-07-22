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
