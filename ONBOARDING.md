# NT.QMS — Onboarding (start here on a new session/account)

You are continuing an in-flight project. **First action: read `CLAUDE.md` in the repo
root** — it is the authoritative operating guide (standing rules, dev setup, run/test
commands, conventions). This file is the quick orientation; `CLAUDE.md` is the law.

## What this is
NT.QMS — a multi-tenant SaaS Quality Management System for ISO 17025/15189/9001/
21 CFR Part 11/GMP labs. **.NET 9 · PostgreSQL 17 · Angular 18 · Clean Architecture + CQRS.**
Repo: `github.com/arfaawadnt/NTQMS`. Layers: `src/NT.QAMS.{Domain, Application,
Infrastructure, WebApi, Contracts, SharedKernel}` + 5 test projects + `frontend/`.

## Current status (2026-07-27, tag v1.37.0 / restore-point-20260727)
- **All 18 CSV / 21 CFR Part 11 audit findings are CLOSED** (release train v1.25→v1.37).
- Tests green: **~270 backend + 37 frontend unit + 3 Playwright e2e**.
- Two audits delivered: CSV/Part-11 (all closed) and **Enterprise Architecture**
  (~76%, 0 critical, "approved with conditions").

## The standing rules (do not break — full detail in CLAUDE.md §2)
Don't redesign the domain/DB/APIs (docs in `docs/reference/` are law) · no magic
strings/dead code/TODOs/mocks/fake screens · XML doc comments · strict TS, no `any` ·
domain protects itself (private setters, factories, invariants inside aggregates) ·
multi-tenancy is sacred (EF filter **and** FORCE RLS on every tenant table) · report
gaps honestly, never claim build/tests/migrations pass unless executed · commit footer
`Co-Authored-By: Claude <noreply@anthropic.com>`.

## What to do next — the active plan
Follow **`docs/reference/NT_QMS_Enterprise_Architecture_Remediation_Plan.html`**, a gated
7-phase train (v1.38→v1.44), in order, one phase per increment, each behind the CI gate:
- **Phase 0 (P0):** `qams_app` role guard · DB readiness health-check · replica-topology decision.
- **Phase 1 (P1):** optimistic concurrency (`xmin`)+409 · Outbox dead-letter/backoff/dedup/retention.
- **Phase 2 (P1):** observability — structured JSON logs · OpenTelemetry + correlation IDs · metrics/alerts.
- **Phase 3 (P1/P2):** rate limiting · security headers/CSP · TLS/HSTS · token-storage decision.
- **Phases 4–6 (P2/P3):** API polish, DB CHECK constraints/config validation/non-root container, test-coverage + ADRs.
Completing **Phases 0–3** clears every production blocker → unconditional release.

## First-session checklist (same machine)
1. Open the repo folder; read `CLAUDE.md`.
2. Ensure dev secrets exist (`deploy/DEV-SECRETS.md`) — same Windows user = already there;
   different Windows user = copy/re-provision the `nt-qams-webapi` user-secrets.
3. Confirm the green baseline before changing anything:
   `dotnet build src/NT.QAMS.WebApi -c Debug` → then run the test suite with
   `QMS_ITEST_POSTGRES=...ntqams...` (commands in CLAUDE.md §6). Stop the API before building (DLL lock).
4. Start API + frontend (CLAUDE.md §6) and begin **Phase 0**.

## Suggested first prompt
> "Read CLAUDE.md and docs/reference/NT_QMS_Enterprise_Architecture_Remediation_Plan.html,
> confirm the current test baseline is green, then start Phase 0 (deployment safety gates)
> — one finding at a time, tests + commit as we go."

## Key paths
Architecture/plans/audits → `docs/reference/` · CSV validation set → `docs/validation/` ·
ops runbooks/secrets → `deploy/` · progress log → `IMPLEMENTATION_LOG.md` ·
local restore point (this machine) → `D:\SAAS\QAMS\backups\`.
