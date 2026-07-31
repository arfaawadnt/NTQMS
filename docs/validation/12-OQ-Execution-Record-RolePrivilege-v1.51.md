# Operational Qualification — Execution Record: Role Privilege Module

| Field | Value |
| ----- | ----- |
| Document ID | OQ-EXEC-NTQMS-002 |
| Scope | Role Privilege module shipped in **v1.51.0** (commit `c98ba16`): configurable roles, permission matrix, working scope, language preference, and their enforcement across HTTP, command pipeline, data layer and audit trail |
| System / version | NT.QMS v1.51.0 + defect fix RP-D1 (uncommitted at execution; see §3) |
| Environment | **Development workstation** — API `http://localhost:5080` (ASPNETCORE_ENVIRONMENT=Development), SPA `http://localhost:4200`, PostgreSQL 17 local (`ntqams`, role `qams_app`) |
| Executed by (operator) | Engineering (Claude Code), executing at the System Owner's direction |
| Witnessed by | A. Awad — System Owner / acting QA authority (real-time session, 2026-07-31) |
| Date of execution | 2026-07-31, 10:30–11:05 local |
| Test data | Dedicated tenant **`oq-roles-103114`** provisioned for this session; UI case on `demo-lab` |
| Evidence transcript | Runner output preserved verbatim (evidence file `oq-roles-evidence.txt`, session scratchpad); key bodies transcribed below |

> **Scope statement.** Every result below was **actually observed** against the live
> system during this session; nothing is inferred from the automated test suite.
> HTTP statuses, problem+json bodies and SQL results are transcribed verbatim.
>
> **Declared limitations (to be dispositioned by QA):**
> 1. **Environment is a development workstation**, not a qualified installation.
> 2. **Independence is limited**: the operator authored the code under test; the
>    witness is the System Owner acting as QA. An external assessor will note the
>    absence of segregation of duties.
> 3. **One product defect was found, fixed and re-tested inside this session**
>    (RP-D1, §3) — the fix was applied by the same operator. The failing
>    observation, the fix and the re-test are all recorded; the fix is pending
>    commit and independent review at the time of signing.
> 4. **Two operator/tooling interruptions** occurred and are disclosed in §4;
>    neither is a product finding.
> 5. Department-scope filtering was qualified at unit/functional-test level only;
>    this session exercised the branch dimension live (the mechanism is shared).

---

## 1. Executed cases and actual results

### OQ-RP-01 — Provisioning seeds the governed baseline

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Provision tenant `oq-roles-103114` (platform admin), then `GET /api/roles` as its admin | 5 system roles, all active; admin is the sole member of Tenant Administrator | `HTTP 200`; roles = Analyst (65 grants), Department Head (90), External Auditor (47), Quality Manager (164), Tenant Administrator (170, **memberCount 1**); all `isSystem=true, isActive=true` | **Pass** |
| `GET /api/roles/catalog` | The full permission matrix definition | `HTTP 200`; **31 modules, 8 actions, 170 grantable keys** | **Pass** |

### OQ-RP-02 — The catalogue is closed; names are unique

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Create role with key `nc.frobnicate` | Rejected, stable code | `HTTP 422`; `application/problem+json`; `{"title":"Unknown permission key(s): nc.frobnicate. A privilege must map to a real capability.","status":422,"code":"ROLE-005",…}` | **Pass** |
| Create role **OQ Reader** = `[nc.view]` | Created | `HTTP 200`; `id=019fb715-d814-7209-8a15-33e12ed1c4b7` | **Pass** |
| Create role named `  oq READER ` | Duplicate refused case/space-insensitively | `HTTP 422`; `{"title":"A role named 'oq READER' already exists.","code":"ROLE-007",…}` | **Pass** |

### OQ-RP-03 — System-role protections

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Deactivate seeded role *Analyst* | Refused | `HTTP 422`; `{"title":"A system role cannot be deactivated.","code":"ROLE-004",…}` | **Pass** |
| Rename attempt on *Analyst* via `PUT /api/roles/{id}` | Name fixed (privileges remain tunable) | `PUT` returned `204`; read-back name **"Analyst"** — unchanged, as designed | **Pass** |

### OQ-RP-04 — Deny-by-default; grants and revocations bite on the very next request

User `reader@…` registered on **OQ Reader** (`nc.view` only); one token used throughout.

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| `GET /api/nonconformances` | 200 (authenticated read) | `HTTP 200` | **Pass** |
| `POST /api/audits` | Denied with the uniform contract | `HTTP 403`; `application/problem+json`; `{"title":"You do not have permission to perform this action.","status":403,"code":"AUTHZ-403",…}` | **Pass** |
| Admin grants `audits.view`+`audits.create` with reason *"OQ-RP-04: grant audit scheduling…"* | 204 | `HTTP 204` | **Pass** |
| **Same token** retries `POST /api/audits` | Admitted immediately (no re-login) | `HTTP 201`; `{"id":"019fb715-f51c-…"}` | **Pass** |
| Admin revokes the grant; **same token** retries | Denied again on the next request | `HTTP 403`; `code=AUTHZ-403` | **Pass** |

### OQ-RP-05 — The tenant cannot lock itself out of privilege administration

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Drop `roles.manage` from the only managing role | Refused | `HTTP 422`; `{"title":"This change would leave no active user able to manage roles and privileges. Grant 'Roles & Privileges - Manage' to another active user's role first.","code":"ROLE-006",…}` | **Pass** |
| Move the only administrator onto *OQ Reader* | Refused | `HTTP 422`; `code=ROLE-006` (same body) | **Pass** |

### OQ-RP-06 — The working scope is a hard data filter, on reads and writes

Branches **A** and **B** created; NCs raised in A, in B, and unattributed; user
`qmscoped@…` (Quality Manager role) confined to branch A.

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Confine user to branch A (`PUT /api/users/{id}/scope`) | 204 | `HTTP 204` | **Pass** |
| Scoped `GET /api/nonconformances` | Branch-A NC and unattributed NC visible; branch-B NC absent | `HTTP 200`; `A in=True, unattributed in=True, B in=False` | **Pass** |
| Scoped `GET` of the branch-B NC by id | Does not exist for this user | `HTTP 404`; `code=NC-404` | **Pass** |
| Scoped `POST` raising an NC **into branch B** | Refused in-transaction | `HTTP 422`; `{"title":"You are not permitted to work in the selected branch.","code":"SCOPE-001",…}` | **Pass** |
| Unrestricted admin `GET` | All three NCs visible | `HTTP 200`; all present | **Pass** |

### OQ-RP-07 — Seeded-role parity deny matrix (5 roles × 5 gates, 25 cells)

Every denial observed was `403` `application/problem+json` with `code=AUTHZ-403`.

| Gate | TenantAdmin | QM (scoped) | DeptHead | Analyst | Auditor | P/F |
| ---- | ----------- | ----------- | -------- | ------- | ------- | --- |
| `GET /api/users` | 200 | 403 | 403 | 403 | 403 | **Pass** |
| `GET /api/access-reviews` | 200 | 200 | 403 | 403 | 403 | **Pass** |
| `GET /api/exports/audit-trail.xlsx` | 200 | 200 | 403 | 403 | **200** | **Pass** |
| `POST /api/audits` | 201 | 201 | 403 | 403 | 403 | **Pass** |
| `POST /api/carryover-studies` | 400¹ | 400¹ | 400¹ | 403 | 403 | **Pass** |

¹ 400 = model-validation error on a deliberately minimal body — evidence the
**gate admitted** the caller (authorization filters run before validation);
denied roles never reach validation and receive 403.

### OQ-RP-08 — Effective privileges and the language hierarchy

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| `GET /api/auth/me/privileges` (scoped QM) | Role name + working scope disclosed to the client | `HTTP 200`; `roleName=Quality Manager`; `branchIds=[<branch A>]`; `isPlatformAdmin=False` | **Pass** |
| Self-service `PUT me/language` = `ar` | Persisted; visible on next read | `PUT 204`; read-back `preferredLanguage=ar` | **Pass** |
| Role default language `fr` set on *OQ Reader*; member has no own choice | Member inherits `fr` | `PUT 204`; reader read-back `preferredLanguage=fr` | **Pass** |

### OQ-RP-09 — Privilege changes are regulated records *(failed → defect RP-D1 → fixed → re-passed)*

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| `GET /api/compliance/audit-trail` (admin) after the session's changes | `RolePermissionsChanged` (with the RP-04 reason), `UserRoleAssigned`, `UserScopeChanged` all present | **Initial run: FAIL** — `RolePermissionsChanged` present with reason verbatim (ledger sequence 9, hash-chained); `UserRoleAssigned=False`, `UserScopeChanged=False` | **Fail → RP-D1** |
| **Re-test after fix** (fresh assign + scope change on the same tenant) | Both user events now visible in the tenant's own trail | `HTTP 200`; `UserRoleAssigned` at sequence 14 and `UserScopeChanged` at sequence 15, both stamped `tenantId=019fb715-ba06-…` (the OQ tenant), payloads carrying userId/roleId/branchIds verbatim | **Pass** |

### OQ-RP-10 — Privilege matrix UI (demo-lab, signed in as tenant administrator)

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| Open *Roles & Privileges*, edit *Quality Manager* | Grouped matrix; checked cells equal the role's server-side grants; editable only with `roles.manage` | Drawer rendered **8 groups**; `170` checkboxes total, **`164` checked** (equals the API's permissionCount for QM), `170` editable (caller holds `roles.manage`); browser console clean | **Pass** |

---

## 2. Requirements introduced by this module (trace)

| URS | Requirement | Implementation | Qualified by |
| --- | ----------- | -------------- | ------------ |
| URS-095 | Authorization shall be governed by tenant-configurable roles composed over a closed, code-defined permission catalogue; a grant that maps to no code path shall be impossible to store. | `PermissionCatalog`, `Role` aggregate (ROLE-005) | OQ-RP-01/02; AUTO `RoleTests`, `PermissionCatalogTests` |
| URS-096 | Granting and revoking a privilege shall take effect on the affected user's next request, without waiting for session or token expiry. | Per-request DB resolution (`ActiveSessionMiddleware` + `PrivilegeResolver`) | OQ-RP-04; AUTO `RolePrivilegeFlowTests` |
| URS-097 | A user's allowed branches/departments shall be a hard data boundary: out-of-scope records shall be neither readable nor writable; unattributed records remain visible. | Composed EF tenant+scope filter on all `IAllocatable` aggregates; `OrgScopeGuardInterceptor` (SCOPE-001/002) | OQ-RP-06; AUTO `RolePrivilegeFlowTests`, `UserScopeTests` |
| URS-098 | No sequence of role edits, deactivations or reassignments shall leave a tenant without an active user able to administer privileges. | `ManageRolesLockoutGuard` (ROLE-006) | OQ-RP-05; AUTO `RoleHandlersTests` |
| URS-099 | Every change to who-may-do-what — role grants, role assignment, working-scope changes — shall be captured in the tenant's tamper-evident audit trail with the operator's reason where supplied. | Domain events → outbox → hash-chained `audit.audit_trail`; RP-D1 fix (`IOptionallyTenantScoped`) | OQ-RP-09 (incl. re-test); AUTO `UserEventTenantStampTests` |

---

## 3. Defect record

### RP-D1 — User-account access-control events were invisible to their own tenant's audit trail

- **Found by:** OQ-RP-09 (this session). `RolePermissionsChanged` (raised on the
  tenant-scoped `Role` aggregate) appeared in the tenant trail; `UserRoleAssigned`
  and `UserScopeChanged` (raised on `UserAccount`) did not.
- **Root cause (verified by SQL):** the outbox drain stamped tenant only from
  `ITenantScoped` aggregates. `UserAccount` is deliberately **not** tenant-scoped
  (platform administrators have no tenant), so its events were written with
  `tenant_id = 00000000-0000-0000-0000-000000000000` — present in the ledger,
  hidden from the tenant's RLS-scoped compliance view.
- **Blast radius (measured):** 29 × `UserRoleAssigned`, 2 × `UserScopeChanged`,
  and — **pre-existing, before this module** — 1 × `UserLockedOut` (a security
  event) affected in the dev ledger. `TenantProvisioned` rows are genuinely
  platform-level and correct.
- **Fix:** new `IOptionallyTenantScoped` contract (SharedKernel) implemented by
  `UserAccount`; the outbox drain now falls back to it. Platform-admin events
  (tenant `null`) remain platform-level by design.
- **Fix verification:** unit pins `UserEventTenantStampTests` (2 green — tenant
  user events carry the owning tenant; platform lockout stays tenant-null);
  full regression **419 backend tests green**; OQ-RP-09 re-executed live →
  **Pass** (§1).
- **Residual:** ledger rows written before the fix keep their empty tenant id —
  the ledger is append-only/tamper-evident and is not restated. Disposition of
  the historical rows is a QA decision (they remain queryable platform-side).

---

## 4. Observations and disclosed interruptions (not product findings)

- **OBS-1 — shared dev database vs the migration round-trip test.** During this
  session the full test suite was run between OQ cases; `GovernanceTests`
  (Phase 6) reverts and re-applies the **latest** migration against the shared
  dev database, which drops and recreates this module's four tables — all roles
  and assignments vanished mid-session (observed as 403s in the SPA). The
  **startup backfill self-healed on the next API boot exactly as designed**
  (90 roles re-seeded across 18 tenants; 0 tenant users left unassigned —
  verified by SQL). Operational note: on a dev database, run the API once after
  the integration suite; custom (non-seeded) roles and scopes created only in
  dev are lost by that test. On a qualified environment the suite is never
  pointed at the production database.
- **TOOL-1** — the first runner pass aborted at OQ-RP-06 on an operator script
  bug (tuple unpacking); the entire session was re-executed from a fresh tenant.
  The aborted pass had 18/18 passes; it is superseded by the recorded run.
- **TOOL-2** — the auth-endpoint rate limiter (SEC-013, 10/min) throttled the
  11th `/api/auth/*` call of the run; the two remaining cases were executed in a
  paced continuation against the same tenant. This is the control working as
  specified, not a failure.

---

## 5. Result summary

- **Cases executed: 10** (OQ-RP-01 … OQ-RP-10), comprising 30 recorded checks
  including the 25-cell deny matrix.
- **Passed: 29 of 30 checks on first execution; 1 failed** (OQ-RP-09).
- **Defects raised: 1 (RP-D1)** — fixed and **re-tested to Pass** in-session;
  fix pending commit + review at time of writing.
- **Deviations: 0.** Observations: 1 (OBS-1). Tooling interruptions: 2.
- Post-fix regression: **419 backend tests green** (228 domain, 72 application,
  24 architecture, 18 integration, 77 functional); SPA console clean on the UI case.

---

## 6. Signatures

By signing, the witness confirms that the cases in §1 were executed as
transcribed, that defect RP-D1's failure, fix and re-test are accurately
recorded, and that the limitations (§0) and observations (§4) have been read
and dispositioned.

| Role | Name | Signature | Date |
| ---- | ---- | --------- | ---- |
| Executed by (operator) | Engineering — Claude Code (automated operator) | *n/a — machine-executed; results transcribed verbatim* | 2026-07-31 |
| Witnessed by | A. Awad (System Owner / acting QA authority) | ____________________ | __________ |
| Reviewed & approved by (QA) | | ____________________ | __________ |

> Engineering applies no signature on QA's or the System Owner's behalf. Until
> the witness line is signed, this document is an **unsigned execution
> transcript**: the results are real, the attestation is pending.
