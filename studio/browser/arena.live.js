import { test, expect } from '@playwright/test';
import { startHost, stopHost } from './host.js';

// The live arena reads the same authenticated projection as a real table host.

let host, join;

test.beforeAll(async () => {
  ({ host, join } = await startHost('at-activation'));
});

test.afterAll(async () => {
  await stopHost(host);
});

test('the real host supplies the arena and external decisions update it without arena writes', async ({ page, context }, info) => {
  await page.goto(join);
  await page.locator('#invitation-take').click();
  await expect(page.locator('#table, #pass').filter({ visible: true }).first()).toBeVisible({ timeout: 20000 });
  const tokens = await page.evaluate(() => JSON.parse(localStorage.getItem('downfall.table.seats')));
  const origin = new URL(join).origin;
  const headers = { 'X-Seat-Token': tokens.player1 };
  const before = await (await context.request.get(`${origin}/api/seat/player1`, { headers })).json();
  const arena = await context.newPage();
  const requests = [];
  arena.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/')) requests.push({ method: request.method(), url: request.url() }); });
  await arena.goto(`${origin}/arena`);
  await expect(arena.locator('#arena')).toHaveAttribute('data-ready', 'true');
  await arena.locator('#seat-choices button').filter({ hasText: 'Player 1' }).click();
  await expect(arena.locator('#loading')).toBeHidden();
  for (const one of before.board.allies) {
    await expect(arena.locator(`[data-creature="${one.id}"]`)).toHaveClass(/ally/);
    await expect(arena.locator(`[data-creature="${one.id}"] .creature-stats`)).toContainText(`${one.health}/${one.maxHealth} HP`);
  }
  await arena.screenshot({ path: info.outputPath('real-live-board.jpg'), type: 'jpeg', quality: 85 });
  // The driver acts through another client. The arena itself owns no command or shown acknowledgement.
  for (let attempt = 0; attempt < 4; attempt++) {
    const current = await (await context.request.get(`${origin}/api/seat/player1`, { headers })).json();
    if (current.waitingFor !== 'Evolution') break;
    const result = await context.request.post(`${origin}/api/seat/player1/decision`, { headers, data: { kind: 'Evolution', pass: true, asked: current.waitingAsked } });
    expect(result.status()).toBe(204);
  }
  await expect(arena.locator('#actor-meta')).toHaveText('Tempo', { timeout: 15000 });
  expect(requests.filter(one => one.method !== 'GET').map(one => new URL(one.url).pathname)).toEqual(['/api/lobby/resume']);
  expect(requests.some(one => new URL(one.url).searchParams.has('shown'))).toBe(false);
  await arena.close();
});
