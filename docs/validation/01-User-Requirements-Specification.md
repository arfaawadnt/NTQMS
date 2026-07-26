# User Requirements Specification (URS)

| Field | Value |
| ----- | ----- |
| Document ID | URS-NTQMS-001 |
| System | NT.QMS |
| Version | 1.0 |
| Status | Issued |
| Parent | VMP-NTQMS-001 |

Each requirement has an ID (`URS-nnn`), a statement, and its regulatory basis. Requirements
are grounded in capabilities present in the code (controllers, aggregates, interceptors,
migrations). Regulatory abbreviations: **P11** = 21 CFR Part 11; **A11** = EU GMP Annex 11;
**17025** = ISO/IEC 17025:2017; **9001** = ISO 9001:2015; **15189** = ISO 15189:2022.

Priority: **C** (critical GxP), **H** (high), **M** (medium).

---

## A. Security & Access Control

| ID | Requirement | Regulatory basis | Pri |
| -- | ----------- | ---------------- | --- |
| URS-001 | The system shall authenticate users by unique identity (tenant + email) and password before granting access. | P11 §11.10(d), §11.10(g); A11 §12; 17025 §7.11.2 | C |
| URS-002 | The system shall enforce a strong password policy (≥12 chars; upper, lower, digit, symbol; reject common/breached passwords) on registration, reset, and change. | P11 §11.10(g); A11 §12 | H |
| URS-003 | The system shall lock an account after a defined number of consecutive failed authentications (5) for a defined period (30 min). | P11 §11.10(d); A11 §12 | H |
| URS-004 | The system shall support per-tenant optional multi-factor authentication (TOTP, RFC 6238) and be able to require MFA for privileged roles. | P11 §11.10(d); A11 §12 | H |
| URS-005 | The system shall enforce role-based authorization on every protected operation using defined roles (PlatformAdmin, TenantAdmin, QualityManager, DepartmentHead, Analyst, ExternalAuditor). | P11 §11.10(d), (g); A11 §12; 17025 §7.11.2 | C |
| URS-006 | The system shall re-validate the user's active status and role on every authenticated request, so that deactivation or role change takes effect immediately (session revocation). | P11 §11.10(d); A11 §12 | H |
| URS-007 | The system shall enforce a client idle-session timeout. | P11 §11.10(d); A11 §12 | M |
| URS-008 | The system shall isolate each tenant's data such that no tenant can read or write another tenant's records, enforced at the database layer and fail-closed. | P11 §11.10(d),(c); A11 §12, §4.8; 17025 §7.11.3 | C |
| URS-009 | The system shall allow provisioning and lifecycle management of user accounts (create, assign role, reset password, deactivate). | P11 §11.10(d); A11 §12 | H |
| URS-010 | The system shall support periodic user-access review/recertification, recording an active-account snapshot and conclusion. | P11 §11.10(d); A11 §12; 17025 §7.11.2 | M |

## B. Audit Trail & Data Integrity

| ID | Requirement | Regulatory basis | Pri |
| -- | ----------- | ---------------- | --- |
| URS-011 | The system shall maintain a secure, computer-generated, time-stamped audit trail recording creation, modification, and deletion of GxP records, capturing operator, action, old/new values, and timestamp, contemporaneously and without operator intervention. | P11 §11.10(e); A11 §9; 17025 §7.11; 9001 §7.5 | C |
| URS-012 | The audit trail shall be tamper-evident: entries are hash-chained (SHA-256, per tenant) so any alteration or gap is detectable. | P11 §11.10(a),(c),(e); A11 §9 | C |
| URS-013 | The system shall provide an on-demand verification that the audit-trail chain is intact. | P11 §11.10(a),(e); A11 §9 | H |
| URS-014 | The audit trail and signature/security ledgers shall be append-only — no UPDATE or DELETE by the application runtime role. | P11 §11.10(c),(e); A11 §9 | C |
| URS-015 | The system shall capture a reason/justification for changes where required (e.g., deletions/voids, QC target changes). | P11 §11.10(e); A11 §9 | H |
| URS-016 | The system shall record security-relevant events (logins, lockouts, MFA challenges, failed signings, session/privilege changes). | P11 §11.10(d),(e), §11.300(d); A11 §12 | H |
| URS-017 | The audit trail shall be available for review and export in a human-readable form, including an integrity attestation. | P11 §11.10(b),(e); A11 §9 | H |
| URS-018 | The system shall support formal audit-trail review, recording who reviewed, the period, and the conclusion. | P11 §11.10(e); A11 §9; 17025 §7.11 | M |
| URS-019 | Sensitive fields (credentials) shall be redacted at capture in the change ledger. | P11 §11.10(d); A11 §12 | M |

## C. Electronic Signatures

| ID | Requirement | Regulatory basis | Pri |
| -- | ----------- | ---------------- | --- |
| URS-020 | An electronic signature shall require two distinct identification components (account password + signature PIN). | P11 §11.200(a)(1) | C |
| URS-021 | Each signature record shall bind signer identity, signature meaning, the signed subject, a content hash of the signed record, and a trusted timestamp. | P11 §11.50, §11.70 | C |
| URS-022 | Signature records shall be permanent and append-only, linked to their records so they cannot be excised or transferred. | P11 §11.70, §11.10(c) | C |
| URS-023 | The system shall detect, throttle, and log failed/unauthorized signing attempts and lock the account after repeated failures. | P11 §11.300(d) | H |
| URS-024 | Each signing shall capture the meaning of the signature (e.g., authorship, review, approval). | P11 §11.50(a) | H |

## D. Document Control

| ID | Requirement | Regulatory basis | Pri |
| -- | ----------- | ---------------- | --- |
| URS-025 | The system shall manage the controlled-document lifecycle (draft → submit → recommend/reject → publish → new version → retire) with role-gated transitions. | 17025 §8.3; 9001 §7.5.2/§7.5.3; A11 §; P11 §11.10(k) | H |
| URS-026 | Publication of a controlled document shall be electronically signed. | P11 §11.50; 17025 §8.3.1 | H |
| URS-027 | The system shall maintain document version history and ensure only the current published version is presented as effective. | 17025 §8.3.2; 9001 §7.5.3; P11 §11.10(k) | H |
| URS-028 | The system shall capture per-user "read & understand" acknowledgements pinned to the published version; revising the document re-opens acknowledgement. | 17025 §8.3; 9001 §7.5.3 | M |
| URS-029 | The system shall maintain a controlled printed-copy / distribution register pinned to the published version, with one-shot immutable close (Returned/Destroyed). | 17025 §8.3.2 | M |

## E. Nonconformance / CAPA & Quality Events

| ID | Requirement | Regulatory basis | Pri |
| -- | ----------- | ---------------- | --- |
| URS-030 | The system shall manage the nonconformance / CAPA lifecycle (raise → submit → triage/reject → root-cause → actions → complete → verify → confirm effectiveness). | 17025 §7.10, §8.7; 9001 §10.2 | H |
| URS-031 | The system shall enforce separation of duties: the verifier of a nonconformance may not be the person who raised it. | 17025 §8.7; 9001 §10.2; A11 §2 | C |
| URS-032 | The system shall support distinct quality-event types (Nonconformity, Deviation, Out-of-Specification, Out-of-Trend) over a common investigation/CAPA workflow. | 17025 §7.10; 9001 §10.2; GMP | H |
| URS-033 | The system shall manage customer complaints and feedback and link them to investigations where appropriate. | 17025 §7.9; 9001 §9.1.2 | M |
| URS-034 | The system shall link related quality events (e.g., audit findings and proficiency-test outcomes) to nonconformances. | 17025 §8.7, §8.8; 9001 §9.2 | M |

## F. Analytical Quality / QC

| ID | Requirement | Regulatory basis | Pri |
| -- | ----------- | ---------------- | --- |
| URS-035 | The system shall record QC runs and evaluate them against Westgard multi-rule criteria, producing a documented accept/reject verdict. | 17025 §7.7; 15189 §7.3; 9001 §9.1 | H |
| URS-036 | QC evaluation limits (Westgard SD thresholds / run length) shall be configurable and validated at startup, without changing historical verdicts. | 17025 §7.7; A11 §7 | M |
| URS-037 | Changing a QC target (mean/SD) shall require a reason, be forward-only (effective-dated), and preserve historical verdicts (frozen z-scores). | P11 §11.10(e); 17025 §7.7 | H |
| URS-038 | The system shall manage method validation / verification studies (precision, linearity, detection limit, method/lot/instrument comparison, interference, carryover, reference interval, outlier screening, sigma, uncertainty) with calculation and electronic sign-off. | 17025 §7.2.2; 15189 §7.3; A11 §5 | H |
| URS-039 | Analytical study sign-off/approval shall enforce separation of duties (signer ≠ preparer). | 17025 §7.2.2; A11 §2 | C |
| URS-040 | The system shall manage proficiency-testing plans and results, and support escalation of unsatisfactory outcomes. | 17025 §7.7.1; 15189 §7.3.7 | M |
| URS-041 | Once an analytical study is signed off (and an uncertainty budget approved), the record shall be immutable to further edit/deletion at the database layer. | P11 §11.10(c),(e); 17025 §7.5 | C |

## G. Records & Retention

| ID | Requirement | Regulatory basis | Pri |
| -- | ----------- | ---------------- | --- |
| URS-042 | The system shall protect regulated records against deletion; no hard-delete is available to the runtime role. | P11 §11.10(c); A11 §7; 17025 §7.5 | C |
| URS-043 | The system shall support archival of records with a mandatory immutable snapshot reference. | P11 §11.10(c); A11 §7/§17; 17025 §7.5 | H |
| URS-044 | The system shall support legal hold that blocks disposal regardless of retention schedule, with placement/release recorded and reasoned. | P11 §11.10(c); A11 §7 | M |
| URS-045 | The system shall retain records for their defined retention period and provide accurate, complete copies on export (XLSX). | P11 §11.10(b),(c); A11 §7/§8 | H |
| URS-046 | Stored files/document snapshots shall be immutable and referenced by the record set. | P11 §11.10(c); A11 §7 | H |

## H. Governance

| ID | Requirement | Regulatory basis | Pri |
| -- | ----------- | ---------------- | --- |
| URS-047 | The system shall manage change control (change request → link → approve → close → post-implementation review). | 17025 §8.5; 9001 §6.3; A11 §10 | H |
| URS-048 | The system shall manage risk items and a risk register. | 17025 §8.5; 9001 §6.1; A11 §1 | M |
| URS-049 | The system shall manage a versioned quality policy with one-in-force approval enforcing signer ≠ preparer. | 17025 §8.2; 9001 §5.2 | M |
| URS-050 | The system shall manage supplier/external-provider evaluation and management reviews. | 17025 §6.6, §8.9; 9001 §8.4, §9.3 | M |
| URS-051 | The system shall manage quality objectives, conflict-of-interest / impartiality declarations, equipment & reference standards, competency & test authorizations, and facility monitoring points. | 17025 §6.2, §6.4, §6.5, §4.1; 9001 §6.2 | M |
| URS-052 | The system shall provide dashboards/KPIs and reporting over quality data. | 17025 §8.9; 9001 §9.1/§9.3 | M |

## I. Backup / Disaster Recovery

| ID | Requirement | Regulatory basis | Pri |
| -- | ----------- | ---------------- | --- |
| URS-053 | The system shall be backed up per a defined schedule (continuous WAL/PITR + nightly logical dump), meeting RPO ≤ 5 min and RTO ≤ 4 h. | P11 §11.10(c); A11 §7, §16; 17025 §7.11 | H |
| URS-054 | Backups (database + file storage + secrets) shall be encrypted, off-site replicated, and retained ≥ the record-retention period. | P11 §11.10(c); A11 §7, §16 | H |
| URS-055 | Restore capability shall be verifiable by documented, periodic restore drills. | A11 §16; 17025 §7.11 | H |

---

**Total requirements: 55 (URS-001 … URS-055).**
