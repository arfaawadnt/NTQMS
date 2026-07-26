# Frontend end-to-end tests (F-18)

Playwright end-to-end tests for regulated workflows, complementing the Karma/Jasmine
component and service unit tests (`*.spec.ts` under `src/`).

## Layout

| Suite | Needs the API? | Covers |
| ----- | -------------- | ------ |
| `auth.spec.ts` | No | Sign-in form renders; the route guard redirects unauthenticated users to `/login`. |
| `regulated-workflow.spec.ts` | Yes (demo seed) | A demo tenant admin signs in through the full stack (SPA → JWT → API) and reaches the Nonconformance register. |

The tenant context is normally derived from the lab's host; the workflow spec seeds
`qams.tenant.slug` in `localStorage` before the app boots so it can run on `localhost`.

## Running

The Playwright `webServer` starts `ng serve` (with `proxy.conf.json`, so `/api`
reaches the backend) on port 4200 and reuses an already-running one.

```bash
# 1. Start the API (from the repo root) so the workflow spec can authenticate:
dotnet run --project src/NT.QAMS.WebApi

# 2. Run the e2e suite (from frontend/):
npm run e2e
```

Credentials used by the workflow spec are the **documented dev-only demo seed**
(`admin@demo-lab.local`), never real secrets. First-time setup downloads the
browser once with `npx playwright install chromium`.
