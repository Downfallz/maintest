import { test, expect } from '@playwright/test';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { startHost, stopHost } from './host.js';
test.beforeEach(returningBattlePlayer);

// The live battle reads the same authenticated projection as a real table host.

let host, join;

test.beforeAll(async () => {
  ({ host, join } = await startHost('at-activation'));
});

test.afterAll(async () => {
  await stopHost(host);
});

test('the real host supplies the battle and external decisions update it without battle writes', async ({ page, context }, info) => {
  await page.goto(join);
  await page.locator('#invitation-take').click();
  await page.waitForURL(/\/battle(?:\?|$)/, { timeout: 20000 }); // The accepted seat goes on to the battle view (ADR 0157).
  const tokens = await page.evaluate(() => JSON.parse(localStorage.getItem('downfall.table.seats')));
  // The page that accepted went on to the battle itself; only the page this test watches may read the seat.
  await page.goto('about:blank');
  const origin = new URL(join).origin;
  const headers = { 'X-Seat-Token': tokens.player1 };
  const before = await (await context.request.get(`${origin}/api/seat/player1`, { headers })).json();
  const battle = await context.newPage();
  const requests = [];
  battle.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/')) requests.push({ method: request.method(), url: request.url() }); });
  await battle.goto(`${origin}/battle`);
  await expect(battle.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(battle.locator('#seat-choices')).toBeEmpty();
  await expect(battle.locator('#loading')).toBeHidden();
  await expect(battle.locator('#upkeep-dialog')).toBeVisible();
  await battle.locator('#upkeep-close').click();
  for (const one of before.board.allies) {
    await expect(battle.locator(`#actor-layer [data-creature="${one.id}"]`)).toHaveClass(/ally/);
    await expect(battle.locator(`#actor-layer [data-creature="${one.id}"] .creature-stats`)).toContainText(`${one.health}/${one.maxHealth} HP`);
  }
  await battle.screenshot({ path: info.outputPath('real-live-board.jpg'), type: 'jpeg', quality: 85 });
  // The driver acts through another client. The battle sends no command; the only acknowledgements it owns are
  // the sheets it opens by itself: the mutation tree on an Evolution asking (ADR 0138) and the team's tempos on a
  // Speed asking (ADR 0150).
  const evolutions = new Set();
  for (let attempt = 0; attempt < 4; attempt++) {
    const current = await (await context.request.get(`${origin}/api/seat/player1`, { headers })).json();
    if (current.waitingFor !== 'Evolution') break;
    evolutions.add(current.waitingAsked);
    const result = await context.request.post(`${origin}/api/seat/player1/decision`, { headers, data: { kind: 'Evolution', pass: true, asked: current.waitingAsked } });
    expect(result.status()).toBe(204);
  }
  // A round's mutations are revealed first, and the team's tempos wait until that reveal is read (ADR 0150).
  await expect(battle.locator('#growth-receipt, #planning-dialog[data-kind="Speed"][open]').first()).toBeVisible({ timeout: 15000 });
  if (await battle.locator('#growth-receipt').isVisible()) await battle.locator('#growth-dismiss').click();
  await expect(battle.locator('#planning-dialog')).toHaveAttribute('data-kind', 'Speed', { timeout: 15000 });
  await expect(battle.locator('#planning-dialog')).toBeVisible();
  const speed = (await (await context.request.get(`${origin}/api/seat/player1`, { headers })).json()).waitingAsked;
  expect(requests.filter(one => one.method !== 'GET').map(one => new URL(one.url).pathname)).toEqual(['/api/lobby/resume']);
  const shown = requests.map(one => new URL(one.url).searchParams.get('shown')).filter(Boolean).map(Number);
  expect(shown.filter(asked => !evolutions.has(asked) && asked !== speed)).toEqual([]);
  await battle.close();
});
