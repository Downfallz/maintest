import { test, expect } from '@playwright/test';

test('the lobby opens every seat in the battle view, with no view to choose, whatever an older choice said (ADR 0156)', async ({ page }) => {
  const held = { table: { id: 'release-table', format: '3v3', round: 2, openSeats: 0 }, seats: [{ seat: 'player1', token: 'accepted-seat' }] };
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/lobby/resume') return route.fulfill({ json: { tables: [held] } });
    if (path === '/api/lobby') return route.fulfill({ json: { tables: [], recording: true } });
    return route.fulfill({ json: { mode: 'off' } });
  });
  await page.route('**/battle?**', route => route.fulfill({ contentType: 'text/html', body: '<p>Chosen battle</p>' }));
  await page.route('**/play?**', route => route.fulfill({ contentType: 'text/html', body: '<p>Chosen table</p>' }));
  // A browser that once chose the table in the old dropdown plays the battle too.
  await page.addInitScript(() => localStorage.setItem('downfall.presentation', 'table'));
  await page.goto('/table/lobby.html'); await expect(page.locator('#presentation')).toHaveCount(0);
  await page.getByRole('button', { name: 'Resume 3v3 table release-table' }).click();
  await expect(page).toHaveURL(/\/battle\?player1=accepted-seat$/);
});
