import { test, expect } from '@playwright/test';

/**
 * A real tenant-scoped, full-stack workflow (F-18): sign in as the demo tenant
 * administrator and reach a regulated register (Nonconformances). The tenant
 * context is normally derived from the lab's host; here we seed it in
 * localStorage before the app boots. Requires the API running with the demo seed.
 *
 * Credentials are the documented dev-only demo seed — never real secrets.
 */
const TENANT = 'demo-lab';
const EMAIL = 'admin@demo-lab.local';
const PASSWORD = 'Demo-Admin-Pass-2!';

test.describe('Regulated workflow — authenticated tenant user', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript((slug) => {
      window.localStorage.setItem('qams.tenant.slug', slug);
    }, TENANT);
  });

  test('a tenant admin signs in and reaches the Nonconformance register', async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[name="email"]').fill(EMAIL);
    await page.locator('input[name="password"]').fill(PASSWORD);
    await page.locator('button[type="submit"]').click();

    // Successful sign-in leaves the login route for the authenticated shell.
    await expect(page).not.toHaveURL(/\/login/, { timeout: 15_000 });

    // The route guard admits an authenticated session into the regulated register.
    await page.goto('/nonconformances');
    await expect(page).toHaveURL(/\/nonconformances/);
    await expect(page.locator('qams-page-header')).toBeVisible();
  });

  test('the Nonconformance list shows the shared load-more pager count', async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[name="email"]').fill(EMAIL);
    await page.locator('input[name="password"]').fill(PASSWORD);
    await page.locator('button[type="submit"]').click();
    await expect(page).not.toHaveURL(/\/login/, { timeout: 15_000 });

    await page.goto('/nonconformances');
    await expect(page).toHaveURL(/\/nonconformances/);

    // The R-3 shared pager footer (qams-load-more) renders a polite live
    // "Showing X of Y" count over the API-004 envelope. When total <= page
    // size there is no "Load more" button — the count line still renders and
    // must be well-formed, so we assert only on it to stay resilient.
    const count = page.locator('qams-load-more .count[aria-live="polite"]');
    await expect(count).toBeVisible();
    await expect(count).toHaveText(/Showing\s+\d+\s+of\s+\d+/);
  });

  // Auditor read-only affordance: SKIPPED. The documented dev seed provides
  // only a tenant admin (admin@demo-lab.local) and the platform admin — there
  // is no seeded ExternalAuditor login, and the e2e infra has no way to mint
  // one. Faking it would prove nothing, so this journey is intentionally left
  // out (see the task report).
});
