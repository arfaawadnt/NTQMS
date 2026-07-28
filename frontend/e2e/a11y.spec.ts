import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

/**
 * Automated accessibility gate (Road-to-100, Phase 9). The unauthenticated
 * sign-in surface is backend-free, so an axe-core scan runs on every CI push
 * alongside auth.spec.ts. We fail the build only on 'serious'/'critical'
 * violations — 'minor'/'moderate' are allowed to avoid churn on cosmetic
 * findings while still catching genuine barriers.
 */

/** WCAG barriers we treat as blocking. */
const BLOCKING_IMPACTS = ['serious', 'critical'];

test.describe('Accessibility — sign-in surface (no backend)', () => {
  test('the platform login page has no serious/critical axe violations', async ({ page }) => {
    await page.goto('/login');
    await expect(page.locator('input[name="email"]')).toBeVisible();

    const results = await new AxeBuilder({ page }).analyze();
    const blocking = results.violations.filter((v) => BLOCKING_IMPACTS.includes(v.impact ?? ''));

    expect(blocking, blocking.map((v) => `${v.impact}: ${v.id} — ${v.help}`).join('\n')).toEqual([]);
  });

  test('the tenant-scoped login page has no serious/critical axe violations', async ({ page }) => {
    // /t/{slug} pins the lab and hands over to /login, rendering the distinct
    // tenant variant of the sign-in card (tenant chip + platform switch).
    await page.goto('/t/demo-lab');
    await expect(page).toHaveURL(/\/login/);
    await expect(page.locator('input[name="email"]')).toBeVisible();

    const results = await new AxeBuilder({ page }).analyze();
    const blocking = results.violations.filter((v) => BLOCKING_IMPACTS.includes(v.impact ?? ''));

    expect(blocking, blocking.map((v) => `${v.impact}: ${v.id} — ${v.help}`).join('\n')).toEqual([]);
  });
});
