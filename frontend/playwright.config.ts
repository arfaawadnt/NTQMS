import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end tests (F-18) for regulated workflows. The dev server proxies /api to
 * the running API (proxy.conf.json), so the specs exercise the full stack —
 * SPA → JWT auth → API — the way a user does. Chromium only in CI to keep the
 * gate fast; add more projects locally as needed.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [['list']],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:4200',
    trace: 'on-first-retry',
    headless: true,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm start -- --port 4200',
    url: 'http://localhost:4200',
    reuseExistingServer: true,
    timeout: 180_000,
  },
});
