import { test, expect } from '@playwright/test';

test('lobby rollout defaults to table and explicitly resumes the same seat in either view', async ({ page }) => {
  const held = { table: { id: 'release-table', format: '3v3', round: 2, openSeats: 0 }, seats: [{ seat: 'player1', token: 'accepted-seat' }] };
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/lobby/resume') return route.fulfill({ json: { tables: [held] } });
    if (path === '/api/lobby') return route.fulfill({ json: { tables: [], recording: true } });
    return route.fulfill({ json: { mode: 'off' } });
  });
  await page.route('**/battle?**', route => route.fulfill({ contentType: 'text/html', body: '<p>Chosen battle</p>' }));
  await page.route('**/play?**', route => route.fulfill({ contentType: 'text/html', body: '<p>Chosen table</p>' }));
  await page.goto('/table/lobby.html'); await expect(page.locator('#presentation')).toHaveValue('table');
  await page.locator('#presentation').selectOption('battle'); await page.reload();
  await expect(page.locator('#presentation')).toHaveValue('battle');
  await page.getByRole('button', { name: 'Resume 3v3 table release-table' }).click();
  await expect(page).toHaveURL(/\/battle\?player1=accepted-seat$/);
  await page.goto('/table/lobby.html'); await page.locator('#presentation').selectOption('table');
  await page.getByRole('button', { name: 'Resume 3v3 table release-table' }).click();
  await expect(page).toHaveURL(/\/play\?player1=accepted-seat$/);
});
