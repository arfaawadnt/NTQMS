// API contracts mirrored from NT.QAMS.Contracts (kept intentionally thin).

export interface AuthResponse {
  accessToken: string;
  expiresAtUtc: string;
  role: string;
  displayName: string;
  tenantId: string | null;
  mfaRequired: boolean;
}

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

export interface NotificationFeedItem {
  id: string;
  eventKey: string;
  subject: string;
  body: string;
  read: boolean;
  emailStatus: string;
  createdAtUtc: string;
}
