# Developer secrets (F-17)

Secrets are **never** committed to source. They are supplied at runtime:

| Environment    | Source                                                                  |
| -------------- | ----------------------------------------------------------------------- |
| Development    | .NET user-secrets (`UserSecretsId` = `nt-qams-webapi`), loaded automatically |
| Test / CI      | Environment variables (`ConnectionStrings__Postgres`, `Jwt__Secret`, …)  |
| Production      | Environment variables / a secret store (Key Vault, Secrets Manager, …). `appsettings.json` ships the keys **empty**. |

`appsettings.Development.json` is tracked but holds only non-secret config; every
secret value is blank. On a fresh clone the API will refuse to start until the
secrets below are provisioned — that is by design.

## Provision local dev secrets

Run once per developer machine, from the repository root:

```bash
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=ntqams;Username=qams_app;Password=<your-local-db-password>" --project src/NT.QAMS.WebApi
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 48)" --project src/NT.QAMS.WebApi
dotnet user-secrets set "PlatformAdmin:Email" "platform-admin@localhost" --project src/NT.QAMS.WebApi
dotnet user-secrets set "PlatformAdmin:Password" "<your-local-platform-admin-password>" --project src/NT.QAMS.WebApi
```

The JWT secret must be at least 32 characters. Rotating it invalidates all
existing tokens (everyone signs in again) and is otherwise safe.

## Rotation note

The dev credentials that were previously committed in `appsettings.Development.json`
(git history) are considered compromised and must not be reused in any
non-development environment. The JWT signing key has been rotated in dev
user-secrets. Production has always shipped with empty secrets, so no production
secret was ever exposed.
