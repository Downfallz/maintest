import { test, expect } from '@playwright/test';
import { startHost, stopHost } from './host.js';

// The rulebook offline (ADR 0191), on the real host: the service worker is at the root there, as in production. A
// reader who opened the rules once reads them again, at any deep link, once the host cannot be reached at all; any
// other page is still the offline page. The host is the reader's own each time, since the test stops it.

test('the rulebook read once is read again at a deep link with the host gone, and any other page is the offline page', async ({ page }) => {
  const { host, join } = await startHost('at-activation');
  const origin = new URL(join).origin;
  try {
    await page.goto(`${origin}/rules`);
    await expect(page.locator('body')).toHaveAttribute('data-rules', 'ready');
    await expect.poll(() => page.evaluate(() => Boolean(navigator.serviceWorker?.controller)), { timeout: 15000, message: 'the worker controls the page' }).toBe(true);

    await stopHost(host);
    // A new tab, so the deep link is a load and not a jump within the page already open.
    const reader = await page.context().newPage();
    await reader.goto(`${origin}/rules#sunder`);

    await expect(reader.locator('body')).toHaveAttribute('data-rules', 'ready');
    await expect(reader.locator('#sunder h3')).toBeInViewport();
    await expect(reader.getByRole('link', { name: 'Downfall Instinct home' })).toBeVisible();
    await reader.goto(`${origin}/leaderboard`);
    await expect(reader).toHaveTitle('Downfall Instinct · Offline');
  } finally {
    await stopHost(host);
  }
});
