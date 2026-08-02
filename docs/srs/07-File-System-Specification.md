# NT.QMS — Production Software Requirements Specification
## Document 07 · File System Specification

> [Conventions](00-SRS-Index-and-Conventions.md) · Configuration:
> [Document 04](04-Configuration-Reference.md) · Deployment:
> [Document 10](10-Deployment-Specification.md)

Every folder and file the system reads, writes, generates or depends on — at build time, at run time
and during operations.

---

# 7.1 Repository layout

```
NT.QAMS/
├── CLAUDE.md                       operating guide (auto-loaded, standing rules)
├── ONBOARDING.md                   quick orientation for a new session/account
├── README.md
├── IMPLEMENTATION_LOG.md           chronological progress log (~52 KB)
├── SCHEMA-HARDENING-PLAN.md        6-phase schema-hardening programme
├── SCHEMA-HARDENING-REPORT.md      its execution record
├── NT.QAMS.sln
├── docker-compose.yml              development compose (unused on the Windows host)
├── .gitattributes / .gitignore
├── .config/dotnet-tools.json       dotnet-ef as a LOCAL tool
├── .github/workflows/ci.yml        the single CI pipeline
├── .claude/skills/ntqms-architecture/   in-repo architecture skill
│
├── src/
│   ├── NT.QAMS.SharedKernel/       11 files · 248 lines — primitives only
│   │   ├── Abstractions/IClock.cs
│   │   ├── Localization/LocalizedText.cs
│   │   ├── MultiTenancy/{ITenantScoped, IOptionallyTenantScoped, IAllocatable}.cs
│   │   └── Primitives/{AggregateRoot, Entity, ValueObject, DomainException,
│   │                   IAuditable, IDomainEvent}.cs
│   │
│   ├── NT.QAMS.Domain/             55 files · 8,694 lines — 20 bounded contexts
│   │   ├── AnalyticalQuality/      17 files (12 studies + QC + PT + uncertainty + Westgard)
│   │   ├── AuditManagement/ Authorization/ Competency/ ComplianceLedger/
│   │   ├── DocumentControl/ Equipment/ Facility/ Files/ IdentityAccess/
│   │   ├── Improvement/ Notifications/ Organization/ Records/ Reporting/
│   │   └── RiskGovernance/ Sla/ SupplierQuality/ Tenancy/
│   │
│   ├── NT.QAMS.Application/        87 files · 10,275 lines
│   │   ├── Abstractions/           ports: IAppDbContext, ICurrentUser, ICurrentTenant,
│   │   │                           ICurrentChangeReason, IFileStorage, IExportService,
│   │   │                           IUserPrivileges, Idempotency, Messaging, Paging
│   │   ├── Behaviors/              5 MediatR behaviours
│   │   └── <Context>/              vertical slices: *Slice.cs, Commands/, Queries/, *Policy.cs
│   │
│   ├── NT.QAMS.Contracts/          20 files · 1,036 lines — request/response DTOs only
│   │
│   ├── NT.QAMS.Infrastructure/     159 files
│   │   ├── (non-migration code)    46 files · 4,703 lines
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs · AdvisoryLock.cs · AdvisoryLockKeys.cs · RefCounter.cs
│   │   │   ├── Configurations/     EF entity type configurations
│   │   │   ├── Interceptors/       6 interceptors (order is load-bearing)
│   │   │   ├── Idempotency/        EfIdempotencyStore, NullIdempotencyKeyAccessor
│   │   │   ├── Outbox/             OutboxProcessor, OutboxInterceptor, OutboxEvent
│   │   │   └── Migrations/         113 files · ≈295,000 generated lines · 56 migrations
│   │   ├── Authorization/ Compliance/ Configuration/ Email/ Exports/
│   │   ├── Health/ Jobs/ Observability/ Security/ Services/ Storage/
│   │
│   └── NT.QAMS.WebApi/             58 files · 4,707 lines
│       ├── Program.cs              the composition root
│       ├── Controllers/            42 files → 54 controller classes → 329 actions
│       ├── Middleware/             7 components
│       ├── Authorization/          Roles.cs, RequirePermissionAttribute.cs
│       ├── Security/               RateLimiting.cs, FileContentPolicy.cs
│       ├── Startup/                StartupSeeding, DeferredStartupSeeder, StartupSeedingState
│       ├── Versioning/             VersionedRouteConvention.cs
│       ├── Dockerfile
│       ├── appsettings.json                 shipped defaults, secrets EMPTY
│       └── appsettings.Development.json     shape only, secrets EMPTY + pointer comment
│
├── tests/
│   ├── NT.QAMS.Domain.UnitTests/         37 files · 3,613 lines
│   ├── NT.QAMS.Application.UnitTests/    17 files · 1,942 lines
│   ├── NT.QAMS.Architecture.Tests/        4 files ·   349 lines — layering law
│   ├── NT.QAMS.IntegrationTests/         12 files · 1,229 lines — real PostgreSQL
│   ├── NT.QAMS.WebApi.FunctionalTests/   28 files · 3,290 lines
│   │   └── ApiSurface.approved.txt       ← 658-line merge gate
│   └── NT.QAMS.LoadTests/                 1 file ·   144 lines
│                                          ⚠ OUTSIDE the solution — run via `dotnet run`,
│                                            NOT `dotnet test`
│
├── frontend/                       Angular 22.0.8 · 215 TS files · 29,110 lines
│   ├── src/app/
│   │   ├── app.routes.ts · app.config.ts · app.component.ts
│   │   ├── core/                   auth, guards, interceptors, i18n, permissions,
│   │   │   ├── api/                44 API service files
│   │   │   └── help/               help-content.ts, help.service.ts
│   │   ├── shell/shell.component.ts
│   │   ├── shared/ui/              17 shared components
│   │   └── features/               31 feature folders · 105 components
│   ├── src/environments/environment.ts   ← ONE file, not the usual dev/prod pair
│   ├── e2e/                        Playwright specs + README
│   └── playwright.config.ts
│
├── scripts/                        12 operational PowerShell/SQL scripts
├── deploy/                         see §7.3
└── docs/
    ├── adr/            ADR-0001 … ADR-0009
    ├── reference/      architecture (law), audits, plans, as-built, load/security reports
    ├── validation/     GAMP 5 CSV set — 14 documents + verification log
    ├── testing/        6 module test-design documents
    └── srs/            ← THIS specification set
```

## Naming and layout conventions (normative)

| Convention | Rule |
|---|---|
| Vertical slice | one `*Slice.cs` per feature area holding its commands, validators, handlers and queries together |
| Larger contexts | split into `Commands/`, `Queries/`, `Policies/` folders |
| Policies | `*Policy.cs` — a domain-event handler that produces a downstream consequence |
| Controllers | one file may hold several controller classes when they are one cohesive surface (`GovernanceControllers.cs` → 3, `PlatformControllers.cs` → 5, `AnalyticalQualityControllers.cs` → 2, `OperationsControllers.cs` → 3) |
| Angular | `*.component.ts`, `*-api.service.ts`, `*.facade.ts`, specs alongside as `*.spec.ts` |
| Index names | PostgreSQL truncates at 63 bytes and **EF truncates client-side at 62 — silently, mid-word**. Any index whose EF-default name would exceed 62 chars **must** be pinned with `HasDatabaseName()` using the abbreviation map below. Unique indexes use the `ux_` prefix. |

### Index-name abbreviation map (mandatory)

| Full table | Abbreviation |
|---|---|
| `document_acknowledgement` | `doc_ack` |
| `document_controlled_copy` | `doc_copy` |
| `notification_dispatch` | `notif_dispatch` |
| `document_version` | `doc_ver` |
| `supplier_evaluation` | `sup_eval` |
| `instrument_comparability_study` | `icp_study` |
| `user_department_access` | `user_dept_access` |

---

# 7.2 Runtime file system

## FS-01 · Evidence storage (the only application-managed data directory)

```
{FileStorage:RootPath}/
└── {tenantId:N}/                       32-hex tenant id, no hyphens
    ├── {sha256}                        the stored object — NO extension
    ├── {sha256}
    └── .upload-{guid:N}.tmp            transient, during a single upload
```

| Property | Value |
|---|---|
| **Default root** | `{AppContext.BaseDirectory}/data/files` — **inside the deployment folder** |
| **Created** | at start-up (`Directory.CreateDirectory(_root)`) and per tenant on first upload |
| **Object name** | the content's lower-case hex SHA-256; **no extension, no original filename** |
| **Original filename** | stored in the database (`FileReference.FileName`), never on disk |
| **Content type** | the **canonical** type for the extension, stored in the database; the client's declared type is discarded |
| **Deduplication** | identical content in the same tenant maps to the same path; the second upload's temp file is simply deleted |
| **Atomicity** | stream → temp file (hashing as it goes) → `File.Move` into place. A crash never leaves a partial object at a final key |
| **Temp cleanup** | the temp file is deleted on success **and** in the `catch` on failure |
| **Orphaned temp files** | possible only if the process is killed mid-upload. **Nothing sweeps `.upload-*.tmp`** |
| **Deletion** | **none** — there is no delete path, no retention, no cleanup, no orphan collection |
| **Reference counting** | **none** — two records may share one blob with no counter |
| **Size limit** | **none in application code** — only the host body limit |
| **Growth** | monotonic |

### Risks

| ID | Risk |
|---|---|
| **FS-R1** | **The default root is inside the deployment directory.** A clean redeploy (delete-and-copy) destroys every uploaded document, calibration certificate and archive snapshot. `FileStorage:RootPath` **must** point at a durable volume in any real deployment. |
| **FS-R2** | Files are never deleted, so storage grows without bound and disposed archive records keep their snapshot blobs forever. |
| **FS-R3** | No orphan sweep for `.upload-*.tmp`. |
| **FS-R4** | No maximum upload size. |
| **FS-R5** | Files are **not** in the PostgreSQL dump — they need their own backup leg (`deploy/backup.sh` handles this, but only when told the correct path). |

## FS-02 · Logs

| Environment | Destination | Format |
|---|---|---|
| Production | **stdout** | JSON console, scopes included, UTC, ISO-8601 `"O"` |
| Non-production | stdout | default console |
| Development scripts | `%TEMP%\ntqms-dev\` | plain text, written by `scripts/dev-up.ps1` |
| Optional | OTLP collector | when `Otlp:Endpoint` is set |

**The application writes no log files of its own.** Log retention is entirely the host's concern
(container runtime, IIS, systemd, or the collector). No rotation, size cap or retention is configured
anywhere in the repository.

## FS-03 · Temporary files

| Producer | Path | Lifetime |
|---|---|---|
| Upload staging | `{root}/{tenant}/.upload-{guid}.tmp` | one request; deleted on success and on failure |
| XLSX / PDF exports | **in memory**, streamed to the response | never touch disk |
| .NET runtime | OS temp | framework-managed |

There is **no application-managed temp directory** beyond upload staging.

## FS-04 · Backup artefacts (`deploy/backup.sh`)

```
$OUT_DIR/
├── ntqms-{timestamp}.dump            pg_dump --format=custom --compress=9
├── filestore-{timestamp}.tar         tar of $FILESTORE_DIR  (default /var/lib/ntqms/files)
└── manifest-{timestamp}.sha256       checksums of both
```

**Two legs, both required.** If the file-store directory is absent the script emits
`WARNING: file store '…' not found — DB-only backup.` and continues — a DB-only backup **is not a
complete backup**: every controlled-document version, calibration certificate and archive snapshot
would be unrecoverable.

Targets: **RPO ≤ 5 minutes** (continuous WAL archiving with PITR) and **RTO ≤ 4 hours**.
**Post-restore verification is mandatory and includes audit-trail hash-chain verification.**
`deploy/restore.sh` is the counterpart. **`[Not Executed]`** — no restore drill has been performed in
this environment.

## FS-05 · Database on-disk layout (informational)

97 tables across five schemas — `qams` (~85), `audit` (4), `saas` (2), `read` (1), `public` (1).
90 tables carry FORCE row-level security. `ref` is granted in `harden-runtime-role.sql` and appears in
the design documents but **was never created** — a known gap.

Two tables grow monotonically and are the disk-growth drivers:

| Table | Growth | Retention |
|---|---|---|
| `audit.field_change` | one row **per changed column per mutation** (already 19,296 null-tenant rows in the dev dataset alone) | **none — append-only, never purged** |
| `audit.security_event` | one row per auth/signature/export/authorisation event | **none** |
| `qams.outbox_event` | one row per domain event | processed rows purged after `Outbox:RetentionDays` (30); **dead-lettered rows are never purged** |

---

# 7.3 Deployment artefacts (`deploy/`)

| Path | Kind | Purpose |
|---|---|---|
| `DEPLOY.md` | runbook | the deployment procedure |
| `DEV-SECRETS.md` | runbook | provisioning user-secrets on a fresh clone (id `nt-qams-webapi`) |
| `BACKUP-RESTORE-DR.md` | runbook | RPO/RTO, WAL PITR, nightly dump, DR failover, mandatory post-restore verification |
| `OBSERVABILITY.md` | runbook | logs/traces/metrics and the **seven** actionable alert definitions |
| `bring-up-staging.md` | runbook | staging bring-up for the external test track |
| `backup.sh` / `restore.sh` | script | the two backup legs and their restore |
| `db-init.sql` | SQL | database/role bootstrap |
| `harden-runtime-role.sql` | SQL | **least-privilege non-owner runtime role** — the thing `DatabaseRoleGuard` checks for |
| `migrations.sql` | SQL | idempotent migration script. **⚠ STALE — it covers only migrations 1–10 of 56, while `DEPLOY.md` says to re-run it on each upgrade.** Regenerate with `dotnet ef migrations script --idempotent`. |
| `compose.production.yml` | compose | hardened production compose (non-root container) |
| `observability/` | compose + config | `otel-collector.yaml`, `prometheus.yml`, `alert.rules.yml`, `grafana/`. **Authored, never brought up (no Docker on the build host) — residual R-7.** |
| `iis/Install-NTQMS-IIS.ps1` | script | Windows/IIS installation |
| `iis/Verify-NTQMS-IIS.ps1` | script | post-install verification |
| `web.config` | config | IIS ASP.NET Core module configuration |
| `publish-win-x64/` | **build output committed to the repository** | a full `win-x64` publish — framework DLLs and all |
| `NT.QAMS-webapi-v1.0-win-x64.zip` | **binary committed to the repository** | packaged API |
| `NT.QAMS-frontend-v1-dist.zip` | **binary committed to the repository** | packaged SPA |
| `ANTIGRAVITY_DEPLOY_PROMPT.md`, `ANTIGRAVITY_FULLSTACK_DEPLOY_PROMPT.md` | prompt text | tooling prompts, not part of the product |

> **FS-R6 — build output in version control.** `deploy/publish-win-x64/` plus two ZIPs are committed
> binaries. They bloat the repository, go stale silently, and can be deployed by mistake in place of a
> current build. See [Document 14](14-Technical-Debt-Report.md).

## Container image layout

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0     AS build     # WORKDIR /src
FROM mcr.microsoft.com/dotnet/aspnet:9.0  AS runtime   # WORKDIR /app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3   → /health/ready
USER $APP_UID                                          # NON-ROOT
ENTRYPOINT ["dotnet", "NT.QAMS.WebApi.dll"]
```

CI asserts the non-root uid **and** volume writability, and runs a Trivy image scan.
**`[Not Executed]`** locally — no Docker on the build host.

---

# 7.4 Operational scripts (`scripts/`)

| Script | Purpose | Notes |
|---|---|---|
| `dev-status.ps1` | **run this FIRST when "the app is not working"** | separates the three look-alike failures: port DOWN (ERR_CONNECTION_REFUSED) · API up but readiness **503** (PostgreSQL unreachable) · both healthy (so it is credentials or tenant, not the stack) |
| `dev-up.ps1` | start API + SPA **detached** | idempotent; detached so they survive the launching shell |
| `dev-rebuild.ps1` | stop → build → **always restart** | `-Test`, `-Migrate` switches; always restarts even if the build fails |
| `dev-down.ps1` | stop **only the process owning the port** | `-ApiOnly`; never a blanket `taskkill /IM dotnet.exe` |
| `perf-smoke.ps1` | latency smoke test | dev-box p95: ready 20.6 ms, login 69.6 ms, list 6.3 ms |
| `failure-drills.ps1` | poison→dead-letter and PG-down drills | dead-letter drill runs live; the PG-down drill needs elevation and skips gracefully |
| `security-probe.ps1` | 15 fast security probes | 15/15 pass |
| `security-probe-deep.ps1` | 9 deep probes | cross-tenant IDOR, refresh reuse-detection proven **live**, CORS/XST — 9/9 pass |
| `staging-smoke.ps1` | staging smoke test | for the external track |
| `verify-e2e.ps1` | e2e verification wrapper | — |
| `preflight-data-checks.sql` | SQL | pre-migration data validation |
| `preflight-enum-domains.sql` | SQL | enum-domain validation |

### Three structural causes of "the app randomly stops" — and their fixes

1. Dev servers were started as children of whatever shell launched them, so they died with the
   session → **the scripts start them detached**.
2. The running WebApi **locks its own DLLs**, so every `dotnet build`/`test`/`ef` needs the API stopped
   first — and it stayed down when someone forgot to restart → **`dev-rebuild.ps1` always restarts**.
3. A blanket `taskkill /IM dotnet.exe` killed unrelated tooling → **`dev-down.ps1` stops only the port
   owner**.

> **Script constraint:** Windows PowerShell 5.1 misreads non-ASCII (em-dash, arrows, box-drawing) in a
> UTF-8-no-BOM `.ps1`, producing phantom "missing terminator" parse errors. **Keep scripts ASCII-only.**

---

# 7.5 Build outputs (not in version control)

| Path | Producer | Notes |
|---|---|---|
| `src/**/bin/`, `src/**/obj/` | `dotnet build` | `.gitignore`d |
| `frontend/dist/` | `ng build --configuration production` | the deployable SPA |
| `frontend/node_modules/` | `npm ci` | — |
| `frontend/.angular/` | Angular CLI cache | — |
| `TestResults/` | `dotnet test` | — |
| `frontend/playwright-report/`, `test-results/` | Playwright | — |

> **Build gotcha:** `ng build` does **not** refresh a running `ng serve`. After editing component
> styles the SPA must be restarted, or a stale bundle is served.

---

# 7.6 Retention, cleanup and archive strategy — consolidated

| Artefact | Retention | Cleanup mechanism | Automated? |
|---|---|---|---|
| Uploaded evidence files | **forever** | none | ❌ |
| `audit.field_change` | **forever** (append-only) | none | ❌ |
| `audit.security_event` | **forever** (append-only) | none | ❌ |
| `qams.outbox_event` — processed | `Outbox:RetentionDays` (30) | hourly purge inside the processor | ✅ |
| `qams.outbox_event` — dead-lettered | **forever** until an operator clears it | manual SQL | ❌ |
| `qams.outbox_event` — pending | until processed or dead-lettered | — | ✅ |
| `qams.refresh_session` | `Auth:RefreshTokenDays` (14) logically | **no purge job** — expired rows accumulate | ❌ |
| `qams.idempotency` records | **`[Assumption]`** no purge observed | — | ❌ |
| `KpiSnapshot` rows | forever, one per tenant per day | none | ❌ |
| Application logs | host's concern | none configured | ❌ |
| Upload temp files | per request | in-code delete on success and failure | ✅ |
| Backups | operator policy | not scripted | ❌ |
| **Business records** (the archive module) | 5 years / 10 years / Permanent, with legal hold | `AuthorizeDisposal` — **a manual, authorised act**, never automatic | ✅ (by design) |

## The archive strategy in one paragraph

Business-record retention is **deliberately manual**. A record is archived with an immutable content
snapshot; its retention expiry is computed from its class; disposal is refused before expiry
(`ARC-014`), refused forever for `Permanent` (`ARC-013`), and refused while a legal hold is in place
(`ARC-015`) regardless of expiry. Nothing in the system deletes a business record automatically —
disposal is always an authorised human act by a holder of `records.void`, and it emits `RecordDisposed`
into the tamper-evident ledger. **Archiving does not remove or lock the source record**; the archive is
a parallel retention register, not a move operation.

## Cleanup gaps worth naming

| ID | Gap |
|---|---|
| **FS-G1** | No purge of expired `refresh_session` rows. |
| **FS-G2** | No purge or partitioning of `audit.field_change` / `audit.security_event`. These are the fastest-growing tables and have no archival strategy. |
| **FS-G3** | No file-blob lifecycle: disposed archive records keep their snapshot forever. |
| **FS-G4** | No orphan sweep for `.upload-*.tmp`. |
| **FS-G5** | Dead-lettered outbox rows accumulate silently once triaged. |
| **FS-G6** | No log retention configuration anywhere in the repository. |

---

# 7.7 File-system acceptance criteria

| ID | Given | When | Then |
|---|---|---|---|
| **AT-FS-01** | a fresh deployment | the API starts | `{FileStorage:RootPath}` exists (created if absent) |
| **AT-FS-02** | the same content uploaded twice in one tenant | the second upload completes | exactly **one** blob exists and both `FileReference` rows share the storage key |
| **AT-FS-03** | an upload that fails mid-stream | the request ends | no `.upload-*.tmp` remains and no partial object exists at a final key |
| **AT-FS-04** | two tenants upload identical content | both complete | **two** blobs exist, one per tenant directory |
| **AT-FS-05** | `backup.sh` run with a valid file-store path | it completes | three artefacts exist: `.dump`, `.tar`, `.sha256` manifest |
| **AT-FS-06** | `backup.sh` run with a missing file-store path | it completes | a WARNING is emitted and the backup is **DB-only** |
| **AT-FS-07** | a restore | it completes | verification includes an audit-trail **hash-chain** check |
| **AT-FS-08** | processed outbox rows older than the retention window | the hourly purge runs | they are deleted; dead-lettered rows are **not** |
