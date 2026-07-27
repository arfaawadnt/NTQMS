# ADR-0004 — API versioning and contract evolution

- **Status:** Accepted (2026-07-28)
- **Finding:** API-001 (Enterprise Architecture remediation plan, Phase 4)

## Decision

`Asp.Versioning.Mvc` with URL-segment versions. Every endpoint resolves at
BOTH `api/...` (implicitly the default version, currently **v1.0**) and
`api/v1/...` — added by one central route convention
(`VersionedRouteConvention`), not per-controller attributes.
`api-supported-versions` is reported on responses.

## Contract-evolution policy

1. **Additive changes never version.** New endpoints, new OPTIONAL request
   fields, new response fields, and new enum VALUES ship inside v1. Clients
   must ignore unknown response fields.
2. **Breaking changes require a new version.** Removing/renaming a field or
   endpoint, changing a field's type/semantics, making an optional field
   required, or changing a status code contract ⇒ introduce `api/v2/...` for
   the affected controller(s) via `[ApiVersion("2.0")]` + a `MapToApiVersion`
   split, and keep v1 serving until deprecation completes.
3. **Deprecation:** announce in release notes, mark the old version
   deprecated (`api-deprecated-versions` header via
   `[ApiVersion("1.0", Deprecated = true)]`), keep it for ≥ 2 minor releases,
   then remove.
4. **The unversioned `api/...` alias always means the DEFAULT version.** The
   default is only advanced together with a major product release and a
   migration note; integrators who need stability pin `api/v1/...`.
5. Error contracts (problem+json + `code`, API-003) and auth semantics are
   cross-version invariants — they never fork per version.
