# ADR-0010 — Cross-module reads are allowed on the query side only

- **Status:** Accepted (2026-08-29; amended 2026-08-30 to admit the M-22 EOC→CAPA convergence)
- **Finding:** HQMS conformance audit M-04, extended by M-22 (`E:\QMS\NT_QAMS_HQMS_Audit_Register_2026-08-28.md`)
- **Related:** ADR-0008 (persistence port = EF DbSet), ADR-0006 (outbox), `ModuleBoundaryTests` (domain boundary), `WorkflowCommandPolicyTests`

## Context

The modular monolith enforces one hard boundary in the DOMAIN: a domain module
references another only by Id, integration travels by events — proven by
`ModuleBoundaryTests`. The APPLICATION layer is deliberately not subject to that
rule, and the HQMS train leaned on it: several **query** handlers read other
modules' tables directly through `IAppDbContext` —

- HAI, patient-safety and mortality rate handlers join the **ADT** module's
  `PatientStay` projection for the patient-day denominator;
- the accreditation `EvidenceLink` existence check reads seven registers
  (documents, incidents, nonconformances, audits, quality indicators, training
  courses, committees) to confirm a linked record exists in-tenant;
- dashboards union or count across sibling modules.

The audit asked whether this is a boundary violation to restructure, or a
CQRS-legitimate read to bless.

## Decision

**Cross-module reads are allowed on the query side; cross-module writes are not.**

1. **Query handlers** (`IQueryHandler<,>`) may read any module's tables through
   `IAppDbContext`, projecting to a DTO. A reporting/read concern that spans
   modules is a first-class use of CQRS — reassembling it through per-module
   service calls would reimplement the database's join worse and forfeit the
   tenant query filter, RLS and paging composition the read path already has.

2. **Command handlers** (`ICommandHandler<>` / `ICommandHandler<,>`) mutate their
   own module's aggregate. A command that needs another module's state normally
   reads it only to **decide** — a guard (e.g. an evidence link checks the cited
   record exists; sign-in reads the tenant's lockout policy) — then writes its
   own aggregate. Reaching the **shared cross-cutting** modules is always free:
   the Part 11 signing/audit ledger (`ComplianceLedger`), file attachments
   (`Files`), and the permission/actor vocabulary (`Authorization`,
   `IdentityAccess`).

3. **Cross-module writes are sanctioned only for CAPA convergence** — the
   "one loop, many sources" hand-off, where a significant event in a source
   module converges into the single corrective-action pipeline by creating a
   `Nonconformance` (Improvement) in one transaction:
   - incident→CAPA (`RaiseCapaFromIncidentHandler`, HQMS M03) — creates the NC
     and links it back onto the incident;
   - environment-of-care finding→CAPA (`RaiseNcFromRoundFindingHandler`, HQMS
     M22) — a **manual, suggested** hand-off from a safety-round finding; once
     raised the NC follows the ordinary lifecycle, keyed by its EOC source ref
     for idempotency.

   These share one rationale and are the *only* sanctioned convergence writes.
   Any *new* cross-module command write must be a conscious decision recorded
   here, or refactored to the event/outbox path (ADR-0006).

4. The DOMAIN boundary (`ModuleBoundaryTests`) is unchanged and remains the hard
   line: no domain type may reference another module's types. This ADR governs
   only the APPLICATION tier, where that rule deliberately does not reach.

## Enforcement

`CommandHandlerScopeTests` (architecture suite) scans each `ICommandHandler`
implementation — **including its async state-machine bodies** — for references to
another business module's domain namespace, and fails the build on any not in the
approved map. The shared cross-cutting modules are excluded; every other entry is
annotated as a read-guard or, for the convergence hand-offs, an accepted write. A new
cross-module command dependency fails the suite until the author confirms its
nature and records it (SHRINK-ONLY: entries are removed when a handler stops
reaching across, never relaxed). Query handlers are unrestricted.

## Consequences

- The rate/evidence/dashboard query handlers and the six existing read-guards
  stay as they are — blessed, not flagged.
- A future command that writes across modules fails the architecture suite,
  forcing the author to either justify it (convergence-style) or move it to the
  event/outbox path.
- The trade is explicit: application-tier read coupling is accepted for the
  leverage of a single database; new write coupling requires a conscious,
  recorded decision so consistency and the audit/outbox guarantees hold. Revisit
  if the modules are ever split into separate databases, at which point
  cross-module reads become cross-service queries and this ADR is superseded.
