# 14 — Module NCR: Nonconformance, Quality Events, RCA, CAPA, SLA Escalation, Tasks, Notifications

**Module code:** `NCR`
**System under test:** NT.QMS v1.51.2, repo `D:\SAAS\QAMS\21-7\NT.QAMS`, branch `master`. Inspection date 2026-08-01.
**Binding conventions:** `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md` (read in full before this file).

**ID range consumed by this file**

| Kind | Range | Count |
|---|---|---|
| `TC-NCR-API-nnn` | 001–024 | 24 |
| `TC-NCR-BVA-nnn` | 001–016 | 16 |
| `TC-NCR-STATE-nnn` | 001–026 | 26 |
| `TC-NCR-DT-nnn` | 001–004 | 4 |
| `TC-NCR-SEC-nnn` | 001–010 | 10 |
| `TC-NCR-RLS-nnn` | 001–005 | 5 |
| `TC-NCR-ESC-nnn` | 001–016 | 16 |
| `TC-NCR-INT-nnn` | 001–010 | 10 |
| `TC-NCR-UNIT-nnn` | 001–008 | 8 |
| `TC-NCR-MCDC-nnn` | 001–003 | 3 |
| `TC-NCR-PATH-nnn` | 001–002 | 2 |
| `TC-NCR-DF-nnn` | 001–002 | 2 |
| `TC-NCR-LOOP-nnn` | 001 | 1 |
| `TC-NCR-OBS-nnn` | 001–002 | 2 |
| `TC-NCR-DR-nnn` | 001 | 1 |
| `TC-NCR-E2E-nnn` | 001–003 | 3 |
| `TC-NCR-EXPL-nnn` | 001–005 | 5 (charters, §7 — not counted as detailed cases) |
| `GAP-NCR-nnn` | 001–018 | 18 |

**Detailed test cases authored: 133.** (Charters excluded.)

**Completeness statement.** Complete: the 9-state `Nonconformance` machine and every guard code read off source; the exhaustive 5×5 RPN grid; SoD `SOD-CAPA-001`/`SOD-CAPA-002`; the real escalation mechanism (`EscalationTimer` + `ScheduledSweepService` + `EscalationToTaskPolicy` + `NotificationEventPolicies`); tasks; notification rules and dispatch; source-driven NC sagas (audit finding, PT, complaint); tenant isolation; concurrency. Deferred / not executable: CAPA evidence upload, impacted equipment/analytes, deviation-specific detail validation, structured five-whys/fishbone capture, e-signature on verify/close, closed/rejected record locking, reopened-action re-evaluation, SLA-target-driven escalation — **each of these is raised as a Gap in §8, not written as an executable case.** Nothing in this file was executed; every `Result` is `Not Run`.

---

## 0. Correction to ground truth

> Required by the conventions file §6.3: a contradiction between the ground-truth document and the code must be recorded, not silently reconciled.

**The ground-truth statement at `docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md:107-109` is inverted.** It states:

- "CAPA escalation — **SLA-driven, not fixed T+24/48/72**", and
- "The brief's fixed **T+24 → Action Owner, T+48 → Department Head, T+72 → Quality Manager** ladder is **not** what is implemented. Author the escalation tests against `SlaDefinition` + `EscalationTriggeredPolicy` + `NotificationPolicies`."

Measured against source, the opposite is true on both halves:

1. **A fixed 24 / 48 / 72-hour ladder IS implemented**, hard-coded in the domain:
   - `EscalationTimer.Arm(...)` sets `NextStepAtUtc = deadline.AddHours(24)` — `src/NT.QAMS.Domain/Sla/SlaAndTasks.cs:131`.
   - `EscalationTimer.AdvanceIfDue(...)` sets `NextStepAtUtc = Level >= 3 ? null : Deadline.AddHours(24L * (Level + 1))` — `src/NT.QAMS.Domain/Sla/SlaAndTasks.cs:153`, and stops at `Level >= 3` — line 141.
   - The XML doc on the aggregate says so verbatim: "level 1 at +24h → owner, level 2 at +48h → QM, level 3 at +72h → QM" — `src/NT.QAMS.Domain/Sla/SlaAndTasks.cs:106-107`.
   - The recipient split is `1 => (null, OwnerUserId)` / `_ => (EscalationRole /* "QualityManager" */, null)` — lines 147-151, with `EscalationRole = "QualityManager"` at line 111.
2. **`SlaDefinition` is orphaned configuration.** A repository-wide search for `SlaDefinition` / `db.SlaDefinitions` returns only: the aggregate itself (`src/NT.QAMS.Domain/Sla/SlaAndTasks.cs:7-45`), the `DbSet` (`src/NT.QAMS.Application/Abstractions/IAppDbContext.cs:86`, `src/NT.QAMS.Infrastructure/Persistence/AppDbContext.cs:102`), the upsert/list slice (`src/NT.QAMS.Application/Sla/SlaSlice.cs:16-60`), the EF configuration (`src/NT.QAMS.Infrastructure/Persistence/Configurations/OperationsConfigurations.cs:25-36`), the controller (`src/NT.QAMS.WebApi/Controllers/OperationsControllers.cs:71-85`), migration snapshots, and one domain unit test. **No code path reads `SlaDefinition.TargetHours` to compute a deadline.** `ScheduledSweepService.SweepAsync` never touches `db.SlaDefinitions` — `src/NT.QAMS.Infrastructure/Jobs/ScheduledSweepService.cs:86-152`.
3. There is **no type named `EscalationTriggeredPolicy`**. The file `src/NT.QAMS.Application/Sla/EscalationTriggeredPolicy.cs` contains a class named `EscalationToTaskPolicy` (line 15).

**Consequence for this package.** The escalation suite (`TC-NCR-ESC-001…016`) is authored against the **real** mechanism: `EscalationTimer` armed by `ArmEscalationOnCapaPlannedPolicy` off the **CAPA action's due date**, swept hourly. The residual divergences from the commissioning brief (level-2 recipient, clock origin, the unused SLA table, no working-calendar) are raised as **GAP-NCR-001**, **GAP-NCR-002**, **GAP-NCR-011** and **GAP-NCR-017** with full acceptance criteria.

**Confirmed correct in the ground truth (no correction needed):** `Nonconformance.Rpn = severity * likelihood` at `src/NT.QAMS.Domain/Improvement/Nonconformance.cs:140`; `Verify` throws `SOD-CAPA-002` when actor == RaisedBy (line 245); `QualityEventType` = `Nonconformity | Deviation | OutOfSpecification | OutOfTrend` defaulting to `Nonconformity` (line 21, line 121); `ScheduledSweepService` is a 1-hour `BackgroundService` with a 15-second startup delay (lines 29, 34).

**Also confirmed: the commissioning brief's state names are correct.** The task brief warned they might not be. Read off `src/NT.QAMS.Domain/Improvement/Nonconformance.cs:6-9`, the enum is exactly `Draft, Raised, Assigned, Rca, ActionPlan, PendingVerification, EffectivenessCheck, Closed, Rejected`. The only naming divergence is `Rca` (not `RCA`), which is also the persisted string value — `ck_nonconformance_status_domain` at `src/NT.QAMS.Infrastructure/Persistence/Migrations/20260728073229_Phase5CheckConstraints.cs:27-29`.

---

## 1. Implementation inventory

### 1.1 Aggregates and entities

| Type | Kind | File:line | Notes |
|---|---|---|---|
| `Nonconformance` | `AggregateRoot`, `ITenantScoped`, `IAllocatable` | `src/NT.QAMS.Domain/Improvement/Nonconformance.cs:84` | 9-state machine; owns `CapaAction` and `RcaRecord` collections |
| `CapaAction` | `Entity` (EF owned) | `src/NT.QAMS.Domain/Improvement/Nonconformance.cs:29` | `Type, Details, OwnerId, DueDate, Status, CompletedAtUtc` |
| `RcaRecord` | `Entity` (EF owned) | `src/NT.QAMS.Domain/Improvement/Nonconformance.cs:61` | `Method, Analysis, InvestigatorId` — free text, no structure |
| `SlaDefinition` | `AggregateRoot`, `ITenantScoped` | `src/NT.QAMS.Domain/Sla/SlaAndTasks.cs:7` | `Module, Severity, TargetHours` — **no consumer** (see §0) |
| `WorkTask` | `AggregateRoot`, `ITenantScoped` | `src/NT.QAMS.Domain/Sla/SlaAndTasks.cs:54` | `Subject, SubjectRef, AssigneeUserId, AssigneeRole, DueDate, Status` |
| `EscalationTimer` | `AggregateRoot`, `ITenantScoped` | `src/NT.QAMS.Domain/Sla/SlaAndTasks.cs:109` | `SubjectRef, OwnerUserId, Deadline, Level, NextStepAtUtc, Active` |
| `NotificationRule` | `AggregateRoot`, `ITenantScoped` | `src/NT.QAMS.Domain/Notifications/NotificationAggregates.cs:13` | `EventKey, RecipientRoles, EmailEnabled, SubjectTemplate, BodyTemplate, IsActive` |
| `NotificationDispatch` | `AggregateRoot`, `ITenantScoped` | `src/NT.QAMS.Domain/Notifications/NotificationAggregates.cs:83` | `SourceEventId` is the idempotency key |

### 1.2 Enumerations

| Enum | Values | File:line |
|---|---|---|
| `NcStatus` | `Draft, Raised, Assigned, Rca, ActionPlan, PendingVerification, EffectivenessCheck, Closed, Rejected` | `Nonconformance.cs:6-9` |
| `NcSourceType` | `Internal, Complaint, Audit, Supplier, ProficiencyTest` | `Nonconformance.cs:11` |
| `QualityEventType` | `Nonconformity, Deviation, OutOfSpecification, OutOfTrend` (default `Nonconformity`) | `Nonconformance.cs:21`, default at `:121` |
| `CapaActionType` | `Corrective, Preventive` | `Nonconformance.cs:23` |
| `CapaActionStatus` | `Open, Completed` | `Nonconformance.cs:25` |
| `RcaMethod` | `FiveWhys, Fishbone, Other` | `Nonconformance.cs:27` — **both five-whys and fishbone exist, but only as a label on one free-text `Analysis` string** |
| `WorkTaskStatus` | `Pending, Completed` | `SlaAndTasks.cs:47` |
| `DispatchStatus` | `Queued, Sent, Failed` | `NotificationAggregates.cs:6` |

### 1.3 Domain error codes (exhaustive for this module)

| Code | Exception type | HTTP | Raised at | Message trigger |
|---|---|---|---|---|
| `NC-001` | `DomainException` | 422 | `Nonconformance.cs:125` | Title null/blank at `Raise` |
| `NC-002` | `DomainException` | 422 | `Nonconformance.cs:130` | Severity or likelihood outside 1–5 |
| `NC-010` | `InvalidStateTransitionException` | 409 | `Nonconformance.cs:152` (via `Require`) | `Submit` when status ≠ `Draft` |
| `NC-011` | `InvalidStateTransitionException` | 409 | `Nonconformance.cs:159` | `Triage` when status ≠ `Raised` |
| `NC-012` | `InvalidStateTransitionException` | 409 | `Nonconformance.cs:167` | `Reject` when status ≠ `Raised` |
| `NC-013` | `DomainException` | 422 | `Nonconformance.cs:170` | Blank rejection reason |
| `NC-014` | `InvalidStateTransitionException` | 409 | `Nonconformance.cs:182` | `RecordRca` when status ∉ {`Assigned`,`Rca`} |
| `NC-015` | `DomainException` | 422 | `Nonconformance.cs:187` | Blank RCA analysis |
| `NC-016` | `InvalidStateTransitionException` | 409 | `Nonconformance.cs:198` | `PlanCapaAction` when status ∉ {`Rca`,`ActionPlan`} |
| `NC-017` | `DomainException` | 422 | `Nonconformance.cs:203` | Blank action details |
| `NC-018` | `InvalidStateTransitionException` | 409 | `Nonconformance.cs:223` | `SubmitForVerification` when status ≠ `ActionPlan` |
| `NC-019` | `DomainException` | 422 | `Nonconformance.cs:226` | Zero CAPA actions — **unreachable through the aggregate's own API** (see GAP-NCR-014) |
| `NC-020` | `DomainException` | 422 | `Nonconformance.cs:231` | At least one CAPA action not `Completed` |
| `NC-021` | `InvalidStateTransitionException` | 409 | `Nonconformance.cs:239` | `Verify` when status ≠ `PendingVerification` |
| `NC-022` | `InvalidStateTransitionException` | 409 | `Nonconformance.cs:258` | `ConfirmEffectiveness` when status ≠ `EffectivenessCheck` |
| `NC-404` | `DomainException` | 404 | `NcWorkflowCommands.cs:106`, `NcQueries.cs:59` | NC not found in the tenant's slice |
| `SOD-CAPA-001` | `DomainException` | 422 | `Nonconformance.cs:268` | Raiser confirms effectiveness = **true** on own NC |
| `SOD-CAPA-002` | `DomainException` | 422 | `Nonconformance.cs:245` | Raiser verifies own NC (either verdict) |
| `CAPA-001` | `DomainException` | 422 | `Nonconformance.cs:216` | `actionId` not on this NC |
| `CAPA-002` | `InvalidStateTransitionException` | 409 | `Nonconformance.cs:53` | Completing an already-completed action |
| `TASK-001` | `DomainException` | 422 | `SlaAndTasks.cs:73` | Blank task subject |
| `TASK-002` | `DomainException` | 422 | `SlaAndTasks.cs:77` | Neither assignee user nor assignee role |
| `TASK-003` | `InvalidStateTransitionException` | 409 | `SlaAndTasks.cs:95` | Completing an already-completed task |
| `TASK-404` | `DomainException` | 404 | `SlaSlice.cs:88` | Task not found |
| `SLA-001` | `DomainException` | 422 | `SlaAndTasks.cs:20` | Blank module or severity |
| `SLA-002` | `DomainException` | 422 | `SlaAndTasks.cs:25`, `:39` | `targetHours < 1` |
| `NTF-001…004` | `DomainException` | 422 | `NotificationAggregates.cs:38,42,46,66` | Rule field validation |
| `AUTHZ-002` | `DomainException` | 403 | `AuthorizationBehavior.cs:83` | Command policy refuses the actor's role |
| `AUTHZ-403` | (filter) | 403 | `RequirePermissionAttribute.cs:56-60`, `ProblemAuthorizationResultHandler.cs:16` | Endpoint privilege missing |
| `CONCURRENCY-409` | `DbUpdateConcurrencyException` | 409 | `DomainExceptionHandler.cs:21,28-33` | `xmin` changed between read and write |

**Status-mapping rule (verified):** `InvalidStateTransitionException` → **409**; `DomainException` with an `AUTH-` prefix → 401; `AUTHZ-` prefix → 403; code ending `-404` → 404; every other `DomainException` → **422**. Source: `src/NT.QAMS.WebApi/Middleware/DomainExceptionHandler.cs:26-82`. All error bodies are `application/problem+json` with the code in `extensions.code`.

### 1.4 Domain events

| Event | Payload | Raised at |
|---|---|---|
| `NcRaised` | `NcId, NcRef, Title, Severity, Rpn` | `Nonconformance.cs:154` (in `Submit`, **not** in `Raise`) |
| `NcTriaged` | `NcId, NcRef, AssigneeId` | `Nonconformance.cs:162` |
| `NcRejected` | `NcId, NcRef, Reason` | `Nonconformance.cs:175` |
| `CapaActionPlanned` | `NcId, NcRef, ActionId, OwnerId, DueDate` | `Nonconformance.cs:209` |
| `CapaActionCompleted` | `NcId, NcRef, ActionId` | `Nonconformance.cs:218` |
| `NcVerified` | `NcId, NcRef` | `Nonconformance.cs:251` — **only when `passed == true`** |
| `NcClosed` | `NcId, NcRef, ClosedBy` | `Nonconformance.cs:272` |
| `EscalationTriggered` | `TimerId, SubjectRef, Level, AssigneeUserId, RecipientRole, TenantId` | `SlaAndTasks.cs:155` |

**No event is raised for:** `RecordRca`, `SubmitForVerification`, `Verify(false)`, `ConfirmEffectiveness(false)`.

### 1.5 Endpoints

| Method | Route | `[RequirePermission]` | Command / query | Success |
|---|---|---|---|---|
| GET | `/api/nonconformances` | none | `GetNcsQuery` | 200 paged envelope |
| GET | `/api/nonconformances/{id}` | none | `GetNcByIdQuery` | 200 |
| POST | `/api/nonconformances` | **none** | `RaiseNcCommand` | 201 `CreatedAtAction` |
| POST | `/api/nonconformances/{id}/submit` | **none** | `SubmitNcCommand` | 204 |
| POST | `/api/nonconformances/{id}/triage` | `nc.approve` | `TriageNcCommand` | 204 |
| POST | `/api/nonconformances/{id}/reject` | `nc.void` | `RejectNcCommand` | 204 |
| POST | `/api/nonconformances/{id}/rca` | **none** | `RecordRcaCommand` | 204 |
| POST | `/api/nonconformances/{id}/actions` | **none** | `PlanCapaActionCommand` | 200 `{actionId}` |
| POST | `/api/nonconformances/{id}/actions/{actionId}/complete` | **none** | `CompleteCapaActionCommand` | 204 |
| POST | `/api/nonconformances/{id}/submit-verification` | **none** | `SubmitNcForVerificationCommand` | 204 |
| POST | `/api/nonconformances/{id}/verify` | `nc.approve` | `VerifyNcCommand` | 204 |
| POST | `/api/nonconformances/{id}/confirm-effectiveness` | `nc.approve` | `ConfirmNcEffectivenessCommand` | 204 |
| GET | `/api/sla-definitions` | none | `GetSlaDefinitionsQuery` | 200 |
| POST | `/api/sla-definitions` | `tasks.manage` | `UpsertSlaCommand` | 200 `{id}` |
| GET | `/api/tasks/mine` | none | `GetMyTasksQuery` | 200 paged |
| POST | `/api/tasks` | `tasks.create` | `CreateTaskCommand` | 200 `{id}` |
| POST | `/api/tasks/{id}/complete` | none | `CompleteTaskCommand` | 204 |
| GET | `/api/notifications/mine` | none | `GetMyNotificationsQuery` | 200 paged |
| POST | `/api/notifications/{id}/read` | none | `MarkNotificationReadCommand` | 204 |
| GET/POST | `/api/notifications/rules` | `notifications.manage` | rules slice | 200 |
| GET | `/api/notifications/monitor` | `notifications.manage` | `GetDispatchMonitorQuery` | 200 paged |

Sources: `src/NT.QAMS.WebApi/Controllers/NonconformancesController.cs:23-114`; `src/NT.QAMS.WebApi/Controllers/OperationsControllers.cs:71-114`; `src/NT.QAMS.WebApi/Controllers/PlatformControllers.cs:92-128`. Every route is dual-exposed under `/api/v{version}/…` (`Asp.Versioning.Mvc 8.1.0`, ground truth §1).

**Every NC write command carries `[RequireInternalActor]`** — `src/NT.QAMS.Application/Improvement/Commands/NcWorkflowCommands.cs:12,58,60,62,64,66,69,71,73,75` — so `ExternalAuditor` is refused at the MediatR pipeline with `AUTHZ-002` → 403 (`src/NT.QAMS.Application/Behaviors/AuthorizationBehavior.cs:75,83`). Seven of the twelve NC endpoints carry **no** `[RequirePermission]` gate → **GAP-NCR-012**.

### 1.6 Permission keys in use (real, `{module}.{action}` lower-case)

`nc.approve`, `nc.void`, `tasks.create`, `tasks.manage`, `notifications.manage`. `PermissionCatalog.Nonconformances = "nc"` (`src/NT.QAMS.Domain/Authorization/PermissionCatalog.cs:78`), `Tasks = "tasks"` (`:102`), `Notifications = "notifications"` (`:103`). The `nc` module is registered with the `SignedRecordLifecycle` bundle — View/Create/Edit/Approve/Void/Export/Sign (`:136`) — **but `nc.sign` is granted by the catalogue and consumed by nothing** (no e-signature envelope on any NC transition → GAP-NCR-009).

### 1.7 Persistence

| Table | Key | Relevant constraints / indexes | Source |
|---|---|---|---|
| `qams.nonconformance` | `(tenant_id, id)` | `ck_nonconformance_severity_range CHECK (severity BETWEEN 1 AND 5)`; `ck_nonconformance_likelihood_range`; `ck_nonconformance_rpn_range CHECK (rpn BETWEEN 1 AND 25)`; `ck_nonconformance_status_domain` (9 values); `ck_nonconformance_event_type_domain` (4 values); `ck_nonconformance_source_type_domain` (5 values); `ix_nonconformance_tenant_id_nc_ref` UNIQUE; `ix_nonconformance_tenant_id_status`; `ux_nonconformance_source` UNIQUE `(tenant_id, source_ref) WHERE source_ref IS NOT NULL` | `Phase5CheckConstraints.cs:23-30`; `Hardening3_CheckDomains.cs:101-104`; `IdentityAndImprovementConfigurations.cs:55-100`; `AppDbContext.cs:141-146` |
| `qams.capa_action` | `(tenant_id, id)`, shadow `tenant_id`, composite FK `(TenantId, nc_id)` | `ck_capa_action_status_domain`, `ck_capa_action_type_domain` | `IdentityAndImprovementConfigurations.cs:71-83`; `Hardening3_CheckDomains.cs:43-46` |
| `qams.rca_record` | `(tenant_id, id)`, shadow `tenant_id`, composite FK | — | `IdentityAndImprovementConfigurations.cs:85-95` |
| `qams.escalation_timer` | `(tenant_id, id)` | `ix_escalation_timer_subject_ref`; partial `ix_escalation_timer_next_step_at_utc … WHERE active = true` | `OperationsConfigurations.cs:55-67` |
| `qams.work_task` | `(tenant_id, id)` | `ck_work_task_status_domain`; `ck_work_task_completion_order CHECK (completed_at_utc IS NULL OR completed_at_utc >= created_at_utc)`; `ix_work_task_subject_ref` | `OperationsConfigurations.cs:38-53`; `Phase5CheckConstraints.cs:59-62`; `Hardening3_CheckDomains.cs:153-154` |
| `qams.sla_definition` | `(tenant_id, id)` | UNIQUE `(tenant_id, module, severity)` | `OperationsConfigurations.cs:25-36` |
| `qams.notification_rule` | `(tenant_id, id)` | `ix_notification_rule_tenant_id_event_key` | `PlatformConfigurations.cs:78-90` |
| `qams.notification_dispatch` | `(tenant_id, id)` | `ck_notification_dispatch_email_status_domain`; `ix_notification_dispatch_source_event_id`; `ix_notif_dispatch_tenant_recipient_read` | `PlatformConfigurations.cs:92-116`; `Hardening3_CheckDomains.cs:105-106` |

RLS `tenant_isolation` FORCE policies: `nonconformance` (`IdentityAndImprovement.cs:158-159`), `notification_rule`/`notification_dispatch` (`OrgAndNotifications.cs:225-229`), `sla_definition`/`work_task`/`escalation_timer` (`RecordsAndSla.cs:164-171`), plus the owned children under `Hardening4_ChildTenancy`. `xmin` is the concurrency token on every aggregate root (`AppDbContext.cs:114-134`) — a lost update surfaces as `409 CONCURRENCY-409`.

**There is NO `frozen_immutability` trigger on `qams.nonconformance`.** The trigger list at `src/NT.QAMS.Infrastructure/Persistence/Migrations/20260726084134_SignedRecordImmutability.cs:14-28` covers 12 analytical study roots plus `uncertainty_budget` — the NC register is not among them → **GAP-NCR-006**.

### 1.8 Reference numbering

`PostgresReferenceNumberGenerator.NextAsync` issues `NC-{year}-{n:0000}` from `qams.ref_counter` via a single `INSERT … ON CONFLICT (tenant_id, ref_type, year) DO UPDATE SET last_value = last_value + 1 RETURNING last_value` — `src/NT.QAMS.Infrastructure/Persistence/RefCounter.cs:24-44`. Race-free by row lock; gapless within committed history; `refType = "NC"` supplied by `RaiseNcHandler` (`NcWorkflowCommands.cs:41`).

### 1.9 Escalation mechanism (the real one)

```
CapaActionPlanned  --ArmEscalationOnCapaPlannedPolicy-->  escalation_timer
                    subject_ref = "CAPA:{ActionId:N}"
                    deadline    = DueDate @ TimeOnly.MaxValue, offset +00:00
                    next_step   = deadline + 24h ; level = 0 ; active = true
                    (idempotent: skips if subject_ref already present)

ScheduledSweepService  (BackgroundService, 15s startup delay, 1h interval,
                        Elevate() + AdvisoryLockKeys.ComplianceSweep leader election)
   WHERE active AND next_step_at_utc IS NOT NULL AND next_step_at_utc <= now
      -> EscalationTimer.AdvanceIfDue(now)
            guard: !Active || NextStepAtUtc is null || NextStepAtUtc > now || Level >= 3  -> no-op
            Level++            ; L1 -> assignee = OwnerUserId, role = null
                                 L2,L3 -> assignee = null, role = "QualityManager"
            NextStepAtUtc = Level >= 3 ? null : Deadline + 24h*(Level+1)
            Raise EscalationTriggered(TimerId, SubjectRef, Level, assignee, role, TenantId)

EscalationTriggered --EscalationToTaskPolicy-->  work_task subject_ref "{SubjectRef}#L{Level}"
                                                 (idempotent by that subject_ref, due = today UTC)
                    --NotificationEventPolicies--> dispatcher key "SLA_ESCALATED"
                                                 (idempotent by SourceEventId)

CapaActionCompleted --CancelEscalationOnCapaCompletedPolicy--> timer.Cancel() (active = false)
```

Sources: `src/NT.QAMS.Application/Sla/SlaSlice.cs:122-164`; `src/NT.QAMS.Domain/Sla/SlaAndTasks.cs:123-156`; `src/NT.QAMS.Infrastructure/Jobs/ScheduledSweepService.cs:29-34,59-84,143-150`; `src/NT.QAMS.Application/Sla/EscalationTriggeredPolicy.cs:15-38`; `src/NT.QAMS.Application/Notifications/NotificationPolicies.cs:42,116-121`.

### 1.10 Notification pipeline

`NotificationDispatcher.DispatchAsync(sourceEventId, tenantId, eventKey, context)` — `src/NT.QAMS.Application/Notifications/NotificationDispatcher.cs:29-102`:
1. `tenantSetter.Set(tenantId)`.
2. Short-circuits if any `notification_dispatch` row already has that `SourceEventId` (idempotency).
3. Loads active `notification_rule` rows for the key; returns if none.
4. Splits `RecipientRoles` on `,`, resolves `db.Users` where `TenantId == tenantId && IsActive && roles.Contains(u.Role.ToString())` — **the structural `UserAccount.Role` enum, not tenant-defined roles** → GAP-NCR-016.
5. Renders `{placeholder}` tokens case-insensitively.
6. Persists in-app rows **first**, then attempts email best-effort; failures set `email_status='Failed'` with a truncated (≤1500 char) error.

Event keys relevant here: `NC_RAISED` (`NotificationPolicies.cs:35`) and `SLA_ESCALATED` (`:42`). Defaults are seeded per tenant on `TenantProvisioned` with `RecipientRoles = "QualityManager,TenantAdmin"` and `emailEnabled: true` (`NotificationPolicies.cs:138-160`). **No notification rule exists for `NcTriaged`, `NcRejected`, `CapaActionPlanned`, `CapaActionCompleted`, `NcVerified` or `NcClosed`** — those events have no `INotificationHandler`.

### 1.11 Source-driven NC sagas

| Trigger | Policy | SourceRef | Severity / Likelihood | Post-state |
|---|---|---|---|---|
| `FindingRaised` grade `MajorNc` | `FindingToNcPolicy` (`src/NT.QAMS.Application/AuditManagement/Policies/FindingToNcPolicy.cs:28-65`) | `"{AuditRef}#{FindingId:N}"` | 4 / 3 | `Raised` (`Submit()` at `:60`) |
| `FindingRaised` grade `MinorNc` | same | same | 2 / 3 | `Raised` |
| `FindingRaised` grade `Ofi` | same, line 32-34 | — | — | **no NC created** |
| `PtUnsatisfactory` | `PtToNcPolicy` (`src/NT.QAMS.Application/AnalyticalQuality/PtToNcPolicy.cs:24-49`) | `"PT:{PtRef}"` | 4 / 3 | `Raised` |
| `ComplaintValidated` (justified) | `ComplaintToNcPolicy` (`src/NT.QAMS.Application/Improvement/ComplaintToNcPolicy.cs:23-57`) | `"CMP:{ComplaintRef}"` | 3 / 3 | `Raised`, complaint back-linked |

All three run from the outbox (at-least-once) and are idempotent by `SourceRef`, backstopped by `ux_nonconformance_source`.

---

## 2. Divergences from the commissioning brief

| # | Brief requirement | As-built | Verdict | Gap |
|---|---|---|---|---|
| D-01 | Escalation ladder T+24 → Action Owner, T+48 → **Department Head**, T+72 → Quality Manager | 24/48/72 ladder exists, but L2 **and** L3 both target the literal role string `"QualityManager"`; there is no Department-Head step | Partially conforms | GAP-NCR-001 |
| D-02 | Escalation clock starts at the NC/CAPA raise time | Clock starts at the **CAPA action's due date** (`deadline = DueDate @ 23:59:59.9999999 +00:00`), so L1 fires ~24 h *after the action was already late* | Does not conform | GAP-NCR-001 |
| D-03 | (Ground truth) Escalation is SLA-driven via `SlaDefinition(Module,Severity,TargetHours)` | `SlaDefinition` is CRUD-only dead configuration; nothing reads `TargetHours` | Does not conform | GAP-NCR-002 |
| D-04 | CAPA action **evidence upload** | No attachment, file id, or evidence field on `CapaAction`; no NC file endpoint | Does not conform | GAP-NCR-003 |
| D-05 | Impacted **equipment / analytes** on the quality event | `Nonconformance` has `BranchId`, `DepartmentId` and free-text `Description` only | Does not conform | GAP-NCR-004 |
| D-06 | **Deviation detail** validation (deviation-specific mandatory fields) | `QualityEventType.Deviation` changes one string column; no conditional validation whatsoever | Does not conform | GAP-NCR-007 |
| D-07 | RCA methods **five-whys** and **fishbone** as structured capture | Both are `RcaMethod` labels over a single free-text `Analysis` (max 8000 chars); no five "why" slots, no 6M categories | Partially conforms | GAP-NCR-008 |
| D-08 | Rejection **locks the record** | `Rejected` is terminal only because every other method's `Require(...)` fails; `CompleteCapaAction` has **no state guard**, and there is no DB trigger | Partially conforms | GAP-NCR-006 |
| D-09 | Closure **locks the record** | Same as D-08 for `Closed` | Partially conforms | GAP-NCR-006 |
| D-10 | Verification / closure carry an **electronic signature** | `nc.sign` is in the permission catalogue but no NC transition creates a `SignatureRecord` or calls `ESignatureService` | Does not conform | GAP-NCR-009 |
| D-11 | Reopened actions are re-evaluated by the escalation clock | `CapaAction` has no reopen operation; `CapaActionStatus` is `Open → Completed`, one-way | Cannot be assessed (feature absent) | GAP-NCR-010 |
| D-12 | Privilege codes `NCR.TRIAGE` etc. | Real keys are `nc.approve` / `nc.void`; `NCR.TRIAGE` does not exist | Does not conform | recorded once in the package-level RBAC file; restated here as context for GAP-NCR-012 |
| D-13 | Every NC write action is privilege-gated | 7 of 12 NC endpoints have no `[RequirePermission]`; the only gate is `[Authorize]` + `[RequireInternalActor]` | Partially conforms | GAP-NCR-012 |
| D-14 | Escalation respects the laboratory's working calendar / timezone | Deadlines are computed in UTC from a `DateOnly` with `TimeSpan.Zero`; no tenant timezone, no working-hours calendar, no DST handling | Does not conform | GAP-NCR-011 |
| D-15 | Notifications route to the tenant's configured roles | Recipients resolve against the structural `UserAccount.Role` enum, which v1.51.0 demoted from being the authorization mechanism | Partially conforms | GAP-NCR-016 |

---

## 3. State-transition matrix

**States** (`src/NT.QAMS.Domain/Improvement/Nonconformance.cs:6-9`): `Draft` · `Raised` · `Assigned` · `Rca` · `ActionPlan` · `PendingVerification` · `EffectivenessCheck` · `Closed` · `Rejected`.

**Operations:** `Sub` = `Submit`, `Tri` = `Triage`, `Rej` = `Reject`, `Rca` = `RecordRca`, `Pln` = `PlanCapaAction`, `Cmp` = `CompleteCapaAction`, `SFV` = `SubmitForVerification`, `Ver` = `Verify`, `Eff` = `ConfirmEffectiveness`.

### 3.1 Complete transition table (9 states × 9 operations = 81 cells)

Legend: `→X` valid transition to state X; `✗CODE` refused with that domain code; `↻` self-loop (state unchanged, aggregate mutated); `(no state change)` the operation is permitted but does not move the NC state.

| From \ Op | Sub | Tri | Rej | Rca | Pln | Cmp | SFV | Ver | Eff |
|---|---|---|---|---|---|---|---|---|---|
| **Draft** | →Raised | ✗NC-011 | ✗NC-012 | ✗NC-014 | ✗NC-016 | ✗CAPA-001 | ✗NC-018 | ✗NC-021 | ✗NC-022 |
| **Raised** | ✗NC-010 | →Assigned | →Rejected | ✗NC-014 | ✗NC-016 | ✗CAPA-001 | ✗NC-018 | ✗NC-021 | ✗NC-022 |
| **Assigned** | ✗NC-010 | ✗NC-011 | ✗NC-012 | →Rca | ✗NC-016 | ✗CAPA-001 | ✗NC-018 | ✗NC-021 | ✗NC-022 |
| **Rca** | ✗NC-010 | ✗NC-011 | ✗NC-012 | ↻Rca | →ActionPlan | ✗CAPA-001 | ✗NC-018 | ✗NC-021 | ✗NC-022 |
| **ActionPlan** | ✗NC-010 | ✗NC-011 | ✗NC-012 | ✗NC-014 | ↻ActionPlan | **allowed** (no state change) | →PendingVerification *(all complete)* / ✗NC-020 *(any open)* / ✗NC-019 *(zero — unreachable)* | ✗NC-021 | ✗NC-022 |
| **PendingVerification** | ✗NC-010 | ✗NC-011 | ✗NC-012 | ✗NC-014 | ✗NC-016 | **allowed** → ✗CAPA-002 in practice (all actions already complete) | ✗NC-018 | →EffectivenessCheck *(passed, actor≠raiser)* / →ActionPlan *(¬passed, actor≠raiser)* / ✗SOD-CAPA-002 *(actor=raiser, either verdict)* | ✗NC-022 |
| **EffectivenessCheck** | ✗NC-010 | ✗NC-011 | ✗NC-012 | ✗NC-014 | ✗NC-016 | **allowed** → ✗CAPA-002 in practice | ✗NC-018 | ✗NC-021 | →Closed *(effective, actor≠raiser)* / ✗SOD-CAPA-001 *(effective, actor=raiser)* / →ActionPlan *(¬effective, **any actor**)* |
| **Closed** | ✗NC-010 | ✗NC-011 | ✗NC-012 | ✗NC-014 | ✗NC-016 | **allowed — no state guard** → ✗CAPA-002 (already complete) | ✗NC-018 | ✗NC-021 | ✗NC-022 |
| **Rejected** | ✗NC-010 | ✗NC-011 | ✗NC-012 | ✗NC-014 | ✗NC-016 | **allowed — no state guard** → ✗CAPA-001 (no actions exist) | ✗NC-018 | ✗NC-021 | ✗NC-022 |

**Reachability notes (read off source, not assumed):**
- `Raise()` produces `Draft` — the factory does **not** raise `NcRaised` (`Nonconformance.cs:144`). Only `Submit()` does (`:154`). The three source-driven sagas call `Submit()` immediately, so a saga-created NC enters at `Raised`.
- `Rca` and `ActionPlan` are the only self-looping states.
- `ActionPlan` is re-entered from `Verify(false)` (`:248`) and `ConfirmEffectiveness(false)` (`:261`) — these are the rework loops.
- `Closed` and `Rejected` have zero outgoing NC-state transitions. They are **not** locked against child mutation (`CompleteCapaAction`, `Nonconformance.cs:213-219`, has no `Require(...)`) → GAP-NCR-006.
- `NC-019` (zero actions at `SubmitForVerification`) is unreachable through the aggregate API because the only way into `ActionPlan` is `PlanCapaAction`, which appends an action before setting the state → GAP-NCR-014.

### 3.2 EscalationTimer state machine

| From (Active, Level, NextStep) | Trigger | To | Event |
|---|---|---|---|
| — | `Arm(subjectRef, owner, deadline)` | `(true, 0, deadline+24h)` | none |
| `(true, 0, D+24h)` and `now < D+24h` | `AdvanceIfDue(now)` | unchanged | none |
| `(true, 0, D+24h)` and `now >= D+24h` | `AdvanceIfDue(now)` | `(true, 1, D+48h)` | `EscalationTriggered(L1, assignee=Owner, role=null)` |
| `(true, 1, D+48h)` and `now >= D+48h` | `AdvanceIfDue(now)` | `(true, 2, D+72h)` | `EscalationTriggered(L2, assignee=null, role="QualityManager")` |
| `(true, 2, D+72h)` and `now >= D+72h` | `AdvanceIfDue(now)` | `(true, 3, null)` | `EscalationTriggered(L3, assignee=null, role="QualityManager")` |
| `(true, 3, null)` | `AdvanceIfDue(now)` | unchanged | none (`NextStepAtUtc is null` **and** `Level >= 3`) |
| `(true, n, t)` | `Cancel()` | `(false, n, t)` | none |
| `(false, n, t)` | `AdvanceIfDue(now)` | unchanged | none |

Source: `src/NT.QAMS.Domain/Sla/SlaAndTasks.cs:123-156`.

---

## 4. Decision tables

### 4.1 RPN grid — exhaustive 5 × 5 (`Rpn = Severity * Likelihood`, `Nonconformance.cs:140`)

All 25 cells are valid inputs; all satisfy `ck_nonconformance_rpn_range CHECK (rpn BETWEEN 1 AND 25)`.

| Severity \ Likelihood | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| **1** | 1 | 2 | 3 | 4 | 5 |
| **2** | 2 | 4 | 6 | 8 | 10 |
| **3** | 3 | 6 | 9 | 12 | 15 |
| **4** | 4 | 8 | 12 | 16 | 20 |
| **5** | 5 | 10 | 15 | 20 | 25 |

Enumerated rows for the data-driven case `TC-NCR-DT-001` (25 rows, `sev,lik → rpn`):
`1,1→1` · `1,2→2` · `1,3→3` · `1,4→4` · `1,5→5` · `2,1→2` · `2,2→4` · `2,3→6` · `2,4→8` · `2,5→10` · `3,1→3` · `3,2→6` · `3,3→9` · `3,4→12` · `3,5→15` · `4,1→4` · `4,2→8` · `4,3→12` · `4,4→16` · `4,5→20` · `5,1→5` · `5,2→10` · `5,3→15` · `5,4→20` · `5,5→25`.

**Note (honest reporting):** unlike `RiskItem`, which bans a defaulted RPN of 9 and raises `HighResidualRisk` above 12 (ground truth §"Risk / NC scoring"), `Nonconformance` applies **no threshold semantics at all** — RPN 25 and RPN 1 behave identically in the workflow. The value is only carried into the `NcRaised` event and the `NC_RAISED` notification body.

### 4.2 Invalid severity / likelihood inputs

| Input value | Where refused | Status | Code |
|---|---|---|---|
| `0` | `RaiseNcValidator.InclusiveBetween(1,5)` (`NcWorkflowCommands.cs:25-26`) | 400 | `errors["Severity"]` |
| `6` | same | 400 | `errors["Severity"]` |
| `-1` | same | 400 | `errors["Severity"]` |
| `3.5` | System.Text.Json binding to `int` | 400 | ASP.NET `ValidationProblemDetails` |
| `null` | JSON binding to non-nullable `int` | 400 | ASP.NET `ValidationProblemDetails` |
| `"three"` | JSON binding to non-nullable `int` | 400 | ASP.NET `ValidationProblemDetails` |
| `0` / `6` / `-1` bypassing the validator (direct aggregate call) | `Nonconformance.Raise` (`:128-131`) | 422 | `NC-002` |
| Any value reaching the DB outside 1–5 | `ck_nonconformance_severity_range` / `_likelihood_range` | 500 (`23514`) | `check_violation` |

### 4.3 `SubmitForVerification` decision table (`Nonconformance.cs:221-235`)

| Rule | Status == `ActionPlan` | Action count | All actions `Completed` | Outcome |
|---|---|---|---|---|
| R1 | No | any | any | `409 NC-018` |
| R2 | Yes | 0 | n/a | `422 NC-019` (unreachable through the aggregate API) |
| R3 | Yes | ≥1 | No | `422 NC-020` |
| R4 | Yes | ≥1 | Yes | → `PendingVerification`, no event raised |

### 4.4 `Verify` decision table (`Nonconformance.cs:237-253`)

| Rule | Status == `PendingVerification` | `actorId == RaisedBy` | `passed` | Outcome | Event |
|---|---|---|---|---|---|
| R1 | No | any | any | `409 NC-021`, state unchanged | none |
| R2 | Yes | Yes | `true` | `422 SOD-CAPA-002`, **state unchanged** | none |
| R3 | Yes | Yes | `false` | `422 SOD-CAPA-002`, **state unchanged** | none |
| R4 | Yes | No | `true` | → `EffectivenessCheck` | `NcVerified` |
| R5 | Yes | No | `false` | → `ActionPlan` | **none** |

### 4.5 `ConfirmEffectiveness` decision table (`Nonconformance.cs:255-273`)

| Rule | Status == `EffectivenessCheck` | `effective` | `actorId == RaisedBy` | Outcome | Event |
|---|---|---|---|---|---|
| R1 | No | any | any | `409 NC-022`, state unchanged | none |
| R2 | Yes | `false` | **any (SoD not evaluated)** | → `ActionPlan` | **none** |
| R3 | Yes | `true` | Yes | `422 SOD-CAPA-001`, state unchanged | none |
| R4 | Yes | `true` | No | → `Closed` | `NcClosed(actorId)` |

**R2 is an implementation-derived asymmetry:** the early `return` at line 263 happens *before* the SoD check at line 266, so the raiser **can** push their own NC back to `ActionPlan`. `Verify` has no such hole (its SoD check precedes the verdict branch).

### 4.6 `AdvanceIfDue` condition table (`SlaAndTasks.cs:141`)

Guard: `if (!Active || NextStepAtUtc is null || NextStepAtUtc > now || Level >= 3) return;`

| Rule | `Active` | `NextStepAtUtc is null` | `NextStepAtUtc > now` | `Level >= 3` | Advance? |
|---|---|---|---|---|---|
| C1 | false | — | — | — | No (short-circuit on condition 1) |
| C2 | true | true | — | — | No (condition 2) |
| C3 | true | false | true | — | No (condition 3) |
| C4 | true | false | false | true | No (condition 4) |
| C5 | true | false | false | false | **Yes** |

---
