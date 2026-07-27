---
name: ntqms-architecture
description: >-
  The NT.QMS architecture and the exact end-to-end playbook for adding or changing any
  feature — Clean Architecture + custom CQRS over MediatR, multi-tenant PostgreSQL FORCE
  RLS, DDD aggregates with in-domain invariants, 21 CFR Part 11 audit trail + e-signatures,
  EF Core 9 snake_case, Angular 18 standalone/signals. Invoke before writing or modifying
  ANY backend or frontend code in this repo so the change stays inside the architecture.
---

# NT.QMS — Architecture & Feature-Authoring Playbook

Use this whenever you add or change a feature in NT.QMS. It encodes the architecture the
team works in and the precise steps to extend it without breaking the boundaries. It
complements `CLAUDE.md` (always-on rules) and `docs/reference/` (the design "law").

## 0. Golden rules (never break)
- **Don't redesign** the domain, DB, or public APIs — extend within `docs/reference/`.
- **No magic strings/numbers**, no dead code, no TODOs/mocks/fake screens; XML doc comments; strict TS (no `any`).
- **Domain protects itself**: private setters, factories, guarded state machines, invariants inside the aggregate.
- **Multi-tenancy is sacred**: every tenant table has the EF global filter **and** FORCE RLS.
- **Report honestly**: never claim build/tests/migrations pass unless you executed them.

## 1. Layers & dependency direction (strict)
```
Angular(frontend) → WebApi → Application → Domain
                              Infrastructure → Application, Domain
Contracts ← WebApi, Application (DTOs only, no domain types)
SharedKernel ← everything (primitives; zero packages)
```
- **Domain** (`src/NT.QAMS.Domain`): aggregates, value objects, domain events, invariants. **Zero framework/EF/MediatR/Npgsql refs** (only `SharedKernel`). Enforced by `tests/NT.QAMS.Architecture.Tests/LayerRulesTests.cs` — a boundary violation fails CI.
- **Application** (`…Application`): use cases as vertical slices (`*Slice.cs` or `Commands/`+`Queries/`), FluentValidation, ports in `Abstractions/`. Persists through `IAppDbContext` (exposes `DbSet<T>`), never a repository.
- **Infrastructure** (`…Infrastructure`): EF `AppDbContext`, `Persistence/Configurations/*`, `Persistence/Migrations/*`, interceptors, `Persistence/Outbox/*`, `Jobs/*` (BackgroundService), `Security/*`, `Compliance/*`, `Storage/*`.
- **WebApi** (`…WebApi`): thin controllers (dispatch to `ISender`), middleware (`Middleware/RequestIdentity.cs`), `Authorization/Roles.cs`, `Program.cs` composition root.
- **Contracts** (`…Contracts`): request/response DTOs per module — **never expose entities**.

## 2. The end-to-end playbook — add a tenant-scoped feature
Do these in order; each has a matching test. (See any recent module e.g. `QualityPolicy`,
`UserAccessReview`, `DocumentControlledCopy` as a worked example.)

1. **Domain aggregate** — `src/NT.QAMS.Domain/<Module>/<Aggregate>.cs`:
   `sealed`, implements `AggregateRoot, ITenantScoped`; `public Guid TenantId { get; set; }`;
   private parameterless ctor for EF; all mutating props `private set`; child collections as
   `IReadOnlyList` over a private `List`; a static factory (`Create`/`Configure`/`Draft`/`Raise`)
   that validates invariants and throws coded `DomainException("XXX-001", …)`; guarded state
   transitions; `Raise(new <Event>(…, TenantId))` for meaningful facts; SoD via
   `EnsureSignerIsNotPreparer(actorId, "SOD-XX-001")` on approvals/sign-offs.
2. **DbSet** — add `DbSet<Aggregate>` to `Application/Abstractions/IAppDbContext.cs` and
   `Infrastructure/Persistence/AppDbContext.cs`.
3. **EF config** — a new `IEntityTypeConfiguration<Aggregate>` in the matching
   `Persistence/Configurations/*.cs`: `ToTable("<snake>", "qams")`, `HasKey`, `HasMaxLength`,
   enums `HasConversion<string>().HasMaxLength(..)`, tenant-scoped unique index
   `HasIndex(x => new { x.TenantId, x.Ref }).IsUnique()`, `Ignore(x => x.DomainEvents)`,
   owned children via `OwnsMany(...).WithOwner().HasForeignKey("…_id")`.
4. **Migration + RLS parity (CRITICAL)** — `dotnet ef migrations add <Name>`; then in the
   migration `Up()` **after `CreateTable`** add (a new `ITenantScoped` table MUST force RLS —
   EF won't):
   ```
   ALTER TABLE qams.<t> ENABLE ROW LEVEL SECURITY;
   ALTER TABLE qams.<t> FORCE ROW LEVEL SECURITY;
   DROP POLICY IF EXISTS tenant_isolation ON qams.<t>;
   CREATE POLICY tenant_isolation ON qams.<t>
     USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
            OR current_setting('app.bypass_rls', true) = 'on')
     WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
            OR current_setting('app.bypass_rls', true) = 'on');
   ```
   New non-nullable columns on existing rows need a `defaultValue`. Then rebuild + `dotnet ef database update`.
   Verify: `SELECT relforcerowsecurity FROM pg_class WHERE relname='<t>'` → `t`.
5. **Application slice** — `Commands`/`Queries` as `record …Command(...) : ICommand<Guid>` /
   `record …Query(...) : IQuery<Dto>`; `AbstractValidator<T>` for shape; handler loads via
   `IAppDbContext`, calls the aggregate method, `SaveChangesAsync(ct)`; queries `AsNoTracking()`
   `.Select(...Dto)` — **never return `IQueryable`**; get tenant/actor from `ICurrentTenant`/`ICurrentUser`.
6. **Contracts** — request/response DTOs in `Contracts/<Module>/…`.
7. **Controller** — `[ApiController][Route("api/<res>")][Authorize]`; actions dispatch
   `sender.Send(...)`; role-gate with `Roles.*` constants (`WebApi/Authorization/Roles.cs`),
   never string literals; correct HTTP verbs; return DTOs/ids, not entities.
8. **Frontend** — `core/models.ts` interface + `core/api/<x>-api.service.ts` (typed, one method
   per endpoint) + a signal `*.facade.ts` + a standalone component (`ChangeDetectionStrategy.OnPush`,
   typed reactive forms, `loading/error` states) + lazy route in `app.routes.ts` + nav entry in
   `shell/shell.component.ts` + icon in `core/nav-icons.ts` + i18n keys (EN/AR/FR) in
   `core/i18n.service.ts`. No hardcoded strings; authorization visibility is affordance-only.
9. **Tests** — domain unit tests for invariants/transitions/SoD; add integration/functional
   tests for new endpoints where relevant; keep the suite green.
10. **Build/commit** — stop the running API before `dotnet build`/`ef` (DLL lock); run tests;
    commit with footer `Co-Authored-By: Claude <noreply@anthropic.com>`.

## 3. Invariant checklists
**Multi-tenancy:** tenant id from JWT `tenant_id` claim only (never body/route/query); `ITenantScoped`
gets the EF filter automatically; new table forces RLS in its migration; background jobs use a fresh
DI scope + `ICurrentTenantSetter.Elevate()`; cross-tenant references by Id (no navigation FK).

**Compliance / Part 11:** state-changing DELETE requires a reason (the `ChangeReasonMiddleware`
enforces `X-Change-Reason`; the `FieldChangeInterceptor` stamps it); reason-for-change on regulated
edits; sign-off/approval carries the SoD guard; signed records become immutable (DB trigger
`reject_frozen_mutation` — add the table there if it has a frozen state); meaningful events flow to
the hash-chained audit trail via the Outbox; sensitive property names are redacted by the interceptor.

**Persistence:** UUIDv7 PKs (`ValueGeneratedNever`); `DateTimeOffset` from injected `IClock` (never
`DateTime.Now`); money `decimal` with precision; no generic repository / UnitOfWork wrapper; migrations
additive & reversible; after adding columns run `ef database update` before integration tests.

## 4. Commands (Windows dev; full detail in CLAUDE.md §6)
```bash
DOTNET="$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe"
# stop the API before building (it locks its DLLs), then:
"$DOTNET" build src/NT.QAMS.WebApi -c Debug
"$DOTNET" ef migrations add <Name> --project src/NT.QAMS.Infrastructure --startup-project src/NT.QAMS.WebApi
"$DOTNET" ef database update      --project src/NT.QAMS.Infrastructure --startup-project src/NT.QAMS.WebApi
QMS_ITEST_POSTGRES="Host=localhost;Database=ntqams;Username=qams_app;Password=dev-only-local" "$DOTNET" test
# run the API (Development):
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 Database__MigrateOnStartup=true \
  "$DOTNET" run --project src/NT.QAMS.WebApi --no-launch-profile --no-build
# frontend
cd frontend && npm ci
node node_modules/@angular/cli/bin/ng.js build --configuration production
CHROME_BIN="/c/Program Files (x86)/Google/Chrome/Application/chrome.exe" node node_modules/@angular/cli/bin/ng.js test --watch=false --browsers=ChromeHeadless
node node_modules/@playwright/test/cli.js test   # e2e (API must be running)
```
Audit `audit.*` rows are RLS-hidden in psql unless you first
`SELECT set_config('app.bypass_rls','on',false);`.

## 5. Current state & active plan
Code at `v1.37.0`; all 18 CSV/Part-11 findings closed. The active work is the **Enterprise
Architecture Remediation Plan** (`docs/reference/NT_QMS_Enterprise_Architecture_Remediation_Plan.html`),
a gated 7-phase train v1.38→v1.44 — start at Phase 0 (deployment safety gates). See `CLAUDE.md` §4.

## 6. Reference map
Architecture (law): `docs/reference/NT_QAMS_{Domain_Model,Database_Architecture,Application_Architecture,Product_Inventory}.md`.
Audits/plans: `docs/reference/NT_QMS_*`. CSV set: `docs/validation/`. Ops: `deploy/`. Rules: `CLAUDE.md`.
