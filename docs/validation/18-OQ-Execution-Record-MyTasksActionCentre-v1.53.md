# Operational Qualification — Execution Record: My Tasks Unified Action Centre (URS-132)

| Field | Value |
| ----- | ----- |
| Document ID | OQ-EXEC-NTQMS-008 |
| Protocol executed | REVAL-NTQMS-001 (doc 06) Part A — requirement **URS-132** |
| System / version | NT.QMS **v1.53.x** (working tree, pending commit) — no migration, no schema change |
| Environment | **Development workstation** — API `http://localhost:5080` (Development), PostgreSQL 17 local (`ntqams`, role `qams_app`); functional suite on EF InMemory + real host |
| Executed by (operator) | Engineering (Claude Code) |
| Witnessed by | _(unsigned — pending)_ |
| Date of execution | 2026-08-08 |
| Test data | Demo laboratory `demo-lab`; operator `admin@demo-lab.local` (TenantAdmin) |
| Result | **1 handler case (6 sources) + 2 real-pipeline functional cases green; live feed rendered and verified** |

> **Scope statement.** The page content below was **actually observed** live; the automated results
> were watched to completion.
>
> **Declared limitations (must be dispositioned by QA):**
> 1. **Development workstation, not a qualified installation** — this record does not close DOC-001.
> 2. **Independence is limited** — the operator authored the code under test; no witness signature.
> 3. **Live cross-source breadth not shown end-to-end**: the demo operator owns only manual tasks, so
>    the live page showed the `task` source. The union across all six sources is proven by the handler
>    test on a seeded fixture (§2); a witnessed multi-source live walk is left for QA.

---

## 1. Live checks — actual results (dev)

### OQ-MYTASKS-01 — the action centre aggregates and groups pending work

| Step | Expected | **Actual observed** | P/F |
| ---- | -------- | ------------------- | --- |
| `/tasks` renders the unified action centre | grouped feed, not the old flat table | header "My Tasks — Everything across the system that needs your attention, in one place."; a grouped **MY TASKS** section with a count badge (2) | **Pass** |
| A pending manual task offers inline completion | Complete action | row "Review the dynamic-role task routing … Complete" | **Pass** |
| A completed task stays visible (URS-115) | shown as done, not hidden | row "Review Q2 supplier evaluations … Done" | **Pass** |
| The SLA editor and task creation remain | retained for the privileged caller | "New Task", "SLA Definitions" editor both present | **Pass** |
| The feed endpoint responds | 200 with the item array | `GET /api/tasks/my-actions` returned the two task items | **Pass** |

## 2. Automated evidence (watched to completion, 2026-08-08)

| Suite / test | Asserts | Result |
| ------------ | ------- | ------ |
| `MyActionsHandlerTests.Feed_unions_pending_actions_across_sources_for_the_user` | on a seeded EF-InMemory fixture the feed contains all six sources (`task`, `nc`, `capa`, `risk`, `objective`, `review`) for the user — also proving the CAPA/risk owned-collection providers translate on InMemory (a prior `SelectMany` over the owned collection had thrown) | Pass |
| `MyActionsTests.A_task_assigned_to_the_caller_appears_in_the_action_centre` | real HTTP: a task routed to the caller's tier appears in `GET /api/tasks/my-actions` | Pass |
| `MyActionsTests.Unauthenticated_caller_is_refused` | 401 | Pass |
| `ApiSurface` snapshot | `GET /api/tasks/my-actions` (+ versioned twin) added and reviewed | Pass |
| Full backend suite | Domain 245 / App 99 / Arch 33 / Integration 31 (+1 skip) / Functional 90 = **498** | All green (real PG) |
| Frontend production build + unit | clean + 95 Karma | Pass |

## 3. Disposition

Engineering-complete and evidenced. The feed is a live read model computed under the caller's own
identity and permissions (the NC sign-off source is gated on `nc.sign` and excludes the caller's own
NCs), so it cannot surface work the caller may not act on. It adds no write path, no migration and no
permission key. The **coverage note in doc 06 §URS-132** lists the sources included in v1.53 and those
deliberately deferred, so coverage is not overstated. **QA to review, execute a multi-source live walk
on a seeded fixture, and sign.**

---

**Signatures** _(left blank — execution and QA review by a human; engineering does not self-certify)_

| Role | Name | Signature | Date |
| ---- | ---- | --------- | ---- |
| Operator | | | |
| Witness / QA | | | |
| System Owner | | | |
