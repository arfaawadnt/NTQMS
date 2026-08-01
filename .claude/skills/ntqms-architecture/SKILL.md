---
name: ntqms-architecture
description: >-
  The NT.QMS architecture and the exact end-to-end playbook for adding or changing any feature —
  Clean Architecture + custom CQRS over MediatR, multi-tenant PostgreSQL FORCE RLS, DDD aggregates
  with in-domain invariants, permission-based authorization, 21 CFR Part 11 audit trail and
  e-signatures, EF Core 9 snake_case, Angular 22 standalone/signals. Invoke before writing or
  modifying ANY backend or frontend code in this repo so the change stays inside the architecture.
---

# NT.QMS — Architecture & Feature-Authoring Playbook

Use this whenever you add or change a feature. It encodes the architecture the team works in and
the precise steps to extend it without breaking the boundaries. It complements `CLAUDE.md`
(always-on rules) and `docs/reference/` (the design "law").

**Companion skills — load the relevant one, this file does not repeat them:**
- `ntqms-database` — schema and migration law. **Required** before any migration or EF config.
- `ntqms-frontend` — Angular 22 conventions. Required before touching `frontend/`.
- `ntqms-compliance` — what a change obliges you to document, gate and prove.

## 0. Golden rules (never break)
- **Don't redesign** the domain, DB, or public APIs — extend within `docs/reference/`.
- **No magic strings/numbers**, no dead code, no TODOs/mocks/fake screens; XML doc comments;
  strict TS (no `any`).
- **Domain protects itself**: private setters, factories, guarded state machines, invariants
  inside the aggregate — not in validators or handlers.
- **Multi-tenancy is sacred**: every tenant table has the EF global filter **and** FORCE RLS
  **and** a tenant-first composite key.
- **Report honestly**: never claim build/tests/migrations pass unless you executed them.

## 1. Layers & dependency direction (strict)
```
Angular(frontend) → WebApi → Application → Domain
                              Infrastructure → Application, Domain
Contracts ← WebApi, Application (DTOs only, no domain types)
SharedKernel ← everything (primitives; zero packages)
```
- **Domain** (`src/NT.QAMS.Domain`): aggregates, value objects, domain events, invariants.
  **Zero framework/EF/MediatR/Npgsql refs** (only `SharedKernel`). Enforced by
  `tests/NT.QAMS.Architecture.Tests/LayerRulesTests.cs` — a boundary violation fails CI.
- **Application**: use cases as vertical slices (`*Slice.cs` or `Commands/`+`Queries/`),
  FluentValidation, ports in `Abstractions/`. Persists through `IAppDbContext` (exposes
  `DbSet<T>`), never a repository.
- **Infrastructure**: EF `AppDbContext`, `Persistence/Configurations/*`, `Persistence/Migrations/*`,
  interceptors, `Persistence/Outbox/*`, `Jobs/*` (BackgroundService), `Security/*`, `Compliance/*`,
  `Storage/*`.
- **WebApi**: thin controllers (dispatch to `ISender`), middleware
  (`Middleware/RequestIdentity.cs`), `Authorization/` (permission attribute + handler),
  `Program.cs` composition root.
- **Contracts**: request/response DTOs per module — **never expose entities**.

## 2. The end-to-end playbook — add a tenant-scoped feature

Do these in order; each has a matching test. Worked examples: `Authorization` (Role/permissions,
newest), `QualityPolicy`, `UserAccessReview`.

1. **Domain aggregate** — `src/NT.QAMS.Domain/<Module>/<Aggregate>.cs`: `sealed`, implements
   `AggregateRoot, ITenantScoped` (from `SharedKernel.MultiTenancy`); `public Guid TenantId
   { get; set; }`; private parameterless ctor for EF; all mutating props `private set`; child
   collections as `IReadOnlyList` over a private `List`; a static factory that validates
   invariants and throws coded `DomainException("XXX-001", …)`; guarded state transitions;
   `Raise(new <Event>(…))` for meaningful facts — events derive from `DomainEvent`, and **do not
   carry `TenantId`** (the ledger attributes tenancy itself); segregation of duties enforced
   in-aggregate with a `SOD-*` code.
2. **DbSet** — add to `Application/Abstractions/IAppDbContext.cs` **and**
   `Infrastructure/Persistence/AppDbContext.cs`.
3. **EF config + 4. migration** — **see `ntqms-database`**. In short: tenant-first composite key
   `HasKey(x => new { x.TenantId, x.Id })`, tenant-composite FKs, mandatory FORCE RLS in the
   migration, CHECK domains derived from the enum, `Down()` must work. Do not improvise here —
   that skill exists because this step has produced real defects.
5. **Application slice** — `record …Command(...) : ICommand<Guid>` / `record …Query(...) :
   IQuery<Dto>`, each carrying a **command policy attribute** (see step 7); `AbstractValidator<T>`
   for shape (including `MaximumLength` for any column that is now `text`); handler loads via
   `IAppDbContext`, calls the aggregate method, `SaveChangesAsync(ct)`; queries `AsNoTracking()`
   `.Select(...Dto)` — **never return `IQueryable`**; tenant/actor from `ICurrentTenant` /
   `ICurrentUser`; lists return `PagedResponse<T>(Items, Total, Page, PageSize)`.
6. **Contracts** — request/response DTOs in `Contracts/<Module>/…`.
7. **Authorization — both tiers** (deny by default):
   - New module? add it to `Domain/Authorization/PermissionCatalog.cs`; that mints 8
     `{module}.{action}` keys. Granted to **nobody** until an admin assigns them — note that in
     the release note.
   - Command: `[RequirePermissionPolicy(PermissionCatalog.X, PermissionAction.Create)]`. Omit it
     and `CommandPolicyTests` fails CI.
   - Controller: `[ApiController][Route("api/<res>")][Authorize]`, actions dispatch
     `sender.Send(...)` and carry `[RequirePermission(PermissionCatalog.X, PermissionAction.Y)]`.
     **Do not use `[Authorize(Roles=…)]`** — v1.51.0 replaced it for tenant endpoints.
   - Return DTOs/ids, never entities. Honour problem+json, `Idempotency-Key`, `api/v1`.
8. **Frontend** — **see `ntqms-frontend`**.
9. **Tests** — domain unit tests for invariants/transitions/SoD; application tests for the
   handler and policy; **integration tests against real PostgreSQL** for anything touching
   isolation, triggers, or CHECK constraints (EF InMemory enforces none of them); functional
   tests for the endpoint. Update `ApiSurface.approved.txt` deliberately.
10. **Validation documents** — **see `ntqms-compliance` §5**. A behavioural change is a
    requirement change: URS in delta doc 06 Part A, FRA if risk-bearing, RTM trace, OQ record,
    verification-log row.
11. **Build/commit** — stop the running API before `dotnet build`/`ef` (DLL lock); run the suite;
    **verify in the running app in a browser**; stage by explicit path; commit with footer
    `Co-Authored-By: Claude <noreply@anthropic.com>`.

## 3. Invariant checklists

**Multi-tenancy:** tenant id from the JWT `tenant_id` claim only — never body/route/query;
`ITenantScoped` gets the EF filter automatically; the migration forces RLS; background jobs use a
fresh DI scope + `ICurrentTenantSetter.Elevate()`; cross-tenant references by Id, no navigation FK.
Pre-authentication code paths (login, tenant resolution) have **no tenant yet** — set it as soon
as the slug resolves, or ledger writes fail under RLS. That was defect SH-D1.

**Compliance / Part 11:** state-changing DELETE requires `X-Change-Reason`; reason-for-change on
regulated edits; approvals carry the SoD guard and an e-signature; signed records become immutable
via the `frozen_immutability` trigger (function `qams.reject_frozen_mutation`) — add your table
there if it has a frozen state, and model corrections as a new version, never an UPDATE; meaningful
events flow to the hash-chained audit trail via the Outbox; sensitive property names are redacted
by the interceptor.

**Persistence:** UUIDv7 PKs via the `Entity` base ctor (`ValueGeneratedNever`); `DateTimeOffset`
from injected `IClock` — **never `DateTime.Now`**; money `decimal` with precision; no generic
repository or UnitOfWork wrapper; migrations additive and reversible.

**Interceptor order** (`Infrastructure/DependencyInjection.cs`) — tenant GUCs must be set first:
`TenantConnection → AuditStamp → TenantStamp → FieldChange → Outbox → OrgScopeGuard`.

## 4. Commands (Windows dev; full detail in CLAUDE.md §6)

Prefer the scripts — they start detached and always restart the API:
```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev-status.ps1    # FIRST when "app not working"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev-up.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev-rebuild.ps1   # -Test -Migrate
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev-down.ps1      # -ApiOnly
```
```bash
DOTNET="$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe"
"$DOTNET" build src/NT.QAMS.WebApi -c Debug     # stop the API first: it locks its DLLs
QMS_ITEST_POSTGRES="Host=localhost;Database=ntqams;Username=qams_app;Password=dev-only-local" "$DOTNET" test
cd frontend && node node_modules/@angular/cli/bin/ng.js build --configuration production
CHROME_BIN="/c/Program Files (x86)/Google/Chrome/Application/chrome.exe" node node_modules/@angular/cli/bin/ng.js test --watch=false --browsers=ChromeHeadless
node node_modules/@playwright/test/cli.js test   # e2e (API must be running)
```
Dev logins: tenant `demo-lab` → `admin@demo-lab.local` / `Demo-Admin-Pass-2!`; platform admin →
`platform-admin@localhost` / `Dev-Only-Platform-Pass-1!`.
Audit `audit.*` rows are RLS-hidden in psql unless you first
`SELECT set_config('app.bypass_rls','on',false);`.

## 5. Current state

Code at **v1.51.2**. All 18 CSV/Part-11 findings closed; the EA remediation train finished at
v1.44; the **Role Privilege module** shipped at v1.51.0 and **schema hardening** at v1.51.2.
New work comes from the **product backlog** — there is no active remediation phase plan.
Green baseline: 446 backend + 76 frontend unit + 6 e2e.
Open: `SEC-001`, `DOC-001`, `OPS-001`, and two unsigned OQ transcripts. See `CLAUDE.md` §3.

## 6. Reference map

Architecture (law): `docs/reference/NT_QAMS_{Domain_Model,Database_Architecture,Application_Architecture,Product_Inventory}.md`.
As-built schema: `docs/reference/NT_QMS_Database_AsBuilt.md`. Hardening record:
`SCHEMA-HARDENING-REPORT.md`. Test-author ground truth:
`docs/testing/00-GROUND-TRUTH-AND-CONVENTIONS.md`. CSV set: `docs/validation/`. Ops: `deploy/`.
Rules: `CLAUDE.md`.
