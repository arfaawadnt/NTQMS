import { test, expect } from '@playwright/test';

/**
 * Access control is the first regulated control (21 CFR Part 11 §11.10(d)): the
 * sign-in gate and the route guard. These specs need no backend seed.
 */
test.describe('Authentication gate', () => {
  test('the login page renders the sign-in form', async ({ page }) => {
    await page.goto('/login');

    await expect(page.locator('input[name="email"]')).toBeVisible();
    await expect(page.locator('input[name="password"]')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeVisible();
  });

  test('an unauthenticated user is redirected to login from a protected route', async ({ page }) => {
    await page.goto('/nonconformances');
    await expect(page).toHaveURL(/\/login/);
  });
});
