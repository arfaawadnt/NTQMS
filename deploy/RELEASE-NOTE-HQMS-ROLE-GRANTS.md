# Release Note — HQMS Hospital Modules: Permission Keys & Role Grants (M-07)

**Action required for existing tenants before the HQMS modules are used** — grant the new
permission keys (§2). Nothing is granted automatically to a tenant that already exists.

| Field | Value |
| ----- | ----- |
| Document ID | RN-NTQMS-HQMS01-001 |
| Applies to | HQMS hospital-module line (`feature/hqms-hospital-modules`, pre-release — first release tag to be recorded here at cut) |
| Audience | Tenant Administrators / role maintainers (§1–§4); QA (§5) |
| Type | Post-deploy configuration — **no permission data is migrated or granted automatically** |

## What changed

The HQMS hospital extension (HQMS-MSP-001, modules M02–M06) adds eleven permission modules —
**65 new permission keys** — and re-uses three existing modules for hospital features (§3).

| Module key | Guards | Actions |
| ---------- | ------ | ------- |
| `incidents` | Occurrence/incident reporting (incl. sentinel events) | view, create, edit, approve, void, **sign**, export |
| `patient-safety` | Patient-safety event registry & rates | view, create, edit, approve, void, export |
| `infection-control` | HAI case registry, device exposure, rates | view, create, edit, approve, void, export |
| `mortality-review` | Mortality & complication reviews and rates | view, create, edit, approve, void, export |
| `credentialing` | Practitioner files, privileges, point-of-care verify | view, create, edit, approve, void, export |
| `environment-of-care` | EOC rounds, findings, drills | view, create, edit, approve, void, export |
| `indicators` | Quality-indicator definitions & data points | view, create, edit, approve, void, export |
| `standards` | Accreditation standards, evidence links | view, create, edit, approve, void, export |
| `committees` | Committees, meetings, minutes | view, create, edit, approve, void, export |
| `surveys` | Survey definitions **and response entry** (`surveys.create`) | view, create, edit, approve, void, export |
| `integration` | ADT interface endpoints, message monitor, **census** | view, create, edit, manage |

## Why action is needed

Role seeding is **additive and idempotent per role name**: a role your tenant already has is never
touched. Every tenant provisioned before this release therefore holds **none** of the 65 new keys
on any role — every HQMS endpoint answers **403 AUTHZ-403** until keys are granted. Tenants
provisioned **after** this release pick up the seeded defaults in §1 automatically.

Because this catalog ships **before the first HQMS release**, no production tenant ever received a
wider interim grant; there is no revocation step.

## 1. Seeded defaults (new tenants) — and the recommended grants for existing tenants

These are the decisions now encoded in `SystemRoleCatalog` (audit finding M-07). For an existing
tenant, granting the same sets reproduces the intended posture.

| Module | Tenant Admin | Quality Manager | Department Head | Analyst | External Auditor |
| ------ | ------------ | --------------- | --------------- | ------- | ---------------- |
| `incidents` | all | all (incl. `sign`) | view, create, edit, export | view, create, export | view, export |
| `patient-safety` | all | all | view, create, export | view, create, export | **none** |
| `infection-control` | all | all | view, create, export | view, create, export | **none** |
| `mortality-review` | all | all | view, create, export | view, create, export | **none** |
| `credentialing` | all | all | view, export | view | **none** |
| `environment-of-care` | all | all | view, create, edit, export | view, export | view, export |
| `indicators` | all | all | view, export | view, export | view, export |
| `standards` | all | all | view, export | view | view, export |
| `committees` | all | all | view, export | view | view, export |
| `surveys` | all | all | view, create, export | view, create | view, export |
| `integration` | all | all | **none** | **none** | **none** |

Rationale recorded in the catalog itself:

- **Department Head** runs their unit's patient-safety work — manages occurrences (no sign-off),
  records HAI and mortality/complication cases, conducts EOC rounds, enters survey responses, and
  checks a practitioner's privileges at the point of care. Incident sign-off, credential decisions
  and committee/indicator/accreditation governance stay QM+.
- **Analyst** (front-line staff) reports occurrences and records cases; `credentialing.view` is the
  bedside privilege-verification check. No edit or governance rights.
- **External Auditor** audits the quality **system**, not patients: the clinical registries,
  practitioner credential files and the ADT census are deliberately excluded (§4).
- **Integration** is an administrative interface-monitor surface: QM/Tenant Admin only.

## 2. How to grant (existing tenants)

- **UI:** Administration → Roles → edit the role → enable the keys per the table above → save.
  Permissions are resolved per request; changes take effect on the user's next action.
- **API:** `PUT /api/roles/{id}/permissions` with the role's **complete** key set including the new
  keys (the endpoint replaces the set, so send the full list, not a delta).
- **No bulk data migration ships, by design** — which roles reach clinical registries is a tenant
  policy decision. If a tenant wants a scripted grant, author a deliberate, reviewed migration.

## 3. Re-used keys — existing holders gain new reach (disclosure)

Three hospital features are gated by **pre-existing** permission modules. Anyone who already holds
those keys gains access to the new record types **without any grant change**. Review custom roles
holding these keys before enabling the HQMS modules:

| Existing key(s) | New reach added by HQMS |
| --------------- | ----------------------- |
| `risks.*` | FMEA / proactive risk studies (`/api/fmea`) — team-based failure-mode analyses |
| `audits.*` | Accreditation audit programs & tracer rounds (`/api/audit-programs`) |
| `training.*` | Hospital training extensions (orientation/BLS validity, session holds, pass marks) |

If a custom role held (say) `risks.view` for laboratory risk registers only, that role can now also
read FMEA studies. Where that is not wanted, move the role to a narrower custom key set before
rollout.

## 4. External Auditor exclusion — what it means

The seeded External Auditor keeps its read surface over quality records (incidents, indicators,
standards, committees, surveys, EOC, plus the audit trail and signature manifest) but **cannot
reach** `patient-safety`, `infection-control`, `mortality-review`, `credentialing`, or
`integration` (including `/api/integration/census`). These registries carry patient- and
practitioner-adjacent detail outside an external QMS auditor's need-to-know.

A tenant hosting a **clinical** surveyor (e.g. an accreditation tracer team) should create a custom
role granting exactly the clinical `view`/`export` keys required, on the record, rather than
widening the seeded role.

## 5. Verification

1. As a role **without** a key: call the gated endpoint → expect **403** with a `code` starting
   `AUTHZ-` (problem+json).
2. Grant the key → the very next request succeeds (no re-login).
3. As the seeded External Auditor: `GET /api/incidents` → 200; `GET /api/patient-safety/events`,
   `GET /api/integration/census` → 403.
4. Automated pins: `SystemRoleCatalogTests.The_hqms_clinical_grants_are_explicit_per_role_decisions`,
   `AuditorDenyMatrixTests.The_auditor_is_excluded_from_clinical_registries_and_the_census`,
   `RoleEndpointMatrixTests` (patient-safety/credentialing/census rows),
   `RolePrivilegeFlowTests.Provisioning_seeds_the_five_system_roles_and_the_catalog_renders`.

## Does not change

Overall release posture is unchanged — the HQMS line remains **pre-release**; this note concerns
permission configuration only. The lab-edition v1.53.x/v1.54.x posture and its open blockers
(DOC-001, SEC-001) are unaffected. The HQMS validation package is authored as REVAL-NTQMS-002
(`docs/validation/20-Revalidation-Delta-HQMS-Hospital-Modules.md` + OQ templates 21–22, DRAFT —
QA executes and signs; folds under DOC-001).

## Also in this line — API error-contract change (M-11)

A malformed enum value in any request (e.g. `"category": "NotACategory"` or an out-of-range numeric
string) now answers **400** problem+json with code **`REQ-001`** and a message naming the invalid
value and field type. Previously the fleet answered **500** for unknown names, and numeric strings
could smuggle undefined values to the database CHECK constraints. This applies to legacy endpoints
too (the mapping is in the shared exception handler). Clients that treated the old 500 as a retry
signal should treat `REQ-001` as a permanent request error.

## Reference

Audit register: `E:\QMS\NT_QAMS_HQMS_Audit_Register_2026-08-28.md`, findings **M-07**, **M-11**.
Catalog source of truth: `src/NT.QAMS.Application/Authorization/SystemRoleCatalog.cs`.
Change log: `IMPLEMENTATION_LOG.md` (M-07 entry).

## Also in this line — indicator period normalization (M-17)

A measurement's period is now normalized to its frequency's canonical start day (Monday /
1st of month / first day of quarter / 1 January) before the one-per-period check. Two periods in
the same month can no longer coexist on a Monthly indicator. Dates that previously slipped through
misaligned are rejected with `IND-016`; the SPA's Monthly picker now captures a month directly.

## Also in this line — HAI/complication rejection and honest rates (M-18)

HAI cases and complication cases gain a guarded **Reject** transition (`<module>.void`, reason
required): a duplicate or wrong-patient entry is rejected on the record and **leaves the official
rates and morbidity counts**. Rate endpoints change shape additively: `ratePer1000`,
`utilizationRatio` and `mortalityRatePer1000` are now **nullable** — a window with no denominator
returns `null` (rendered "—"), never a fabricated `0.00`. Rate values will change once rejected
cases exist; that is the point.

## Also in this line — credentialing integrity (M-19)

**Point-of-care answers change for lapsed appointments**: `verify-privilege` now answers
**false** once `AppointedUntil` has passed (previously a lapsed appointment still answered
"holds privilege = true" — clinically significant, intended). Also: PSV self-verification is
refused (`SOD-CRD-001` — the verifier must differ from whoever keyed the credential in, now
recorded as `added_by_user_id`); a verified licence cannot be silently re-verified in place
(`CRD-014`); (re)appointment requires **current** evidence (an expired licence or lapsed grant no
longer qualifies); and a lapsed privilege grant no longer blocks its own renewal request.

## Also in this line — ADT inbox behavior and permission split (M-12)

The ADT ingest endpoint now behaves as a true inbox: every message is **stored first** (status
`Received`) in its own transaction, then processed; a malformed event type or any processing
failure — domain or not — is recorded as a **Failed** message against the endpoint's health and
returned in the result (previously a malformed type bounced with 400 and left no trace, and a
mid-processing crash lost the message entirely). Duplicate concurrent deliveries resolve
idempotently instead of a 500. `rawPayload` is bounded at 100,000 characters. A repeated admit
whose patient differs from the stored encounter is refused (`STAY-023`) instead of silently
refreshing the census. **Permission split**: endpoint registration/suspend/resume now require
`integration.manage` (previously `create`/`edit`); the adapter-facing ingest keeps
`integration.create` — grant a machine identity only `integration.create` and it can deliver
messages but never reconfigure the wire. Patient identifiers in `rawPayload` are masked at store time and the stored payload is purged after a retention window (default 90 days, `Integration:PayloadRetentionDays`) — ADR-0011.

## Also in this line — clinical registers page (M-10)

`GET /api/patient-safety/events`, `/api/infection-control/cases` and
`/api/infection-control/devices` now return the standard paging envelope
(`items`/`total`/`page`/`pageSize`/`hasMore`, `page`/`pageSize` query parameters, clamped at 200)
instead of a bare array — a hospital tenant's lifetime of events cannot travel as one response.
Dashboards and roll-ups across the HQMS modules now aggregate in the database (grouped counts and
projections); response contents are unchanged.

## Also in this line — committee minutes approval is now a signed gate (M-16)

Approving meeting minutes is now a **21 CFR Part 11 signing ceremony** (account password +
signature PIN), consistent with every other regulated sign-off. The `committees` module gains the
`.sign` action; `POST /api/meetings/{id}/approve-minutes` now takes `{ password, pin }` and requires
`committees.sign` (previously `committees.approve` with no signature). **Grant `committees.sign`** to
roles that approve minutes (Quality Manager and Tenant Administrator hold it via their catch-all/all-keys
grants; grant it to any custom role that chairs committees). A wrong PIN returns 422 SIG-001 and mints
nothing; the approval is refused if minutes were never recorded or the committee is disbanded.
