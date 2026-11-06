import { test, expect } from '@playwright/test';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningBattlePlayer);
const creature = id => ({ id, name: `Creature ${id}`, health: 20, maxHealth: 20, energy: id + 2, isAlive: true, conditions: [], knownSpells: ['strike'] });
async function mount(page, ids = [1, 2, 3]) {
  const state = { errors: [], writes: [], shown: [], refuse: null, lost: false, hold: null,
    view: { waitingFor: 'Speed', waitingAsked: 11, waitingCreature: ids[0], feed: [], feedNext: 0,
      board: { slot: 'Player1', phase: 'Planning', subPhase: 'Speed', roundNumber: 1, allies: ids.map(creature), enemies: [4, 5, 6].map(creature), timeline: [], speedChoices: [] },
      options: { speed: { missing: ids } }, guidance: [{ spell: 'strike', standardCriticalChance: .4 }] } };
  const accept = command => {
    state.view.board.speedChoices.push({ creature: command.creature, speed: command.speed });
    const next = ids.find(id => !state.view.board.speedChoices.some(one => one.creature === id));
    state.view.waitingAsked++; state.view.waitingCreature = next ?? null; state.view.waitingFor = next ? 'Speed' : null;
  };
  page.on('pageerror', error => state.errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' })));
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url());
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'tempo', format: `${ids.length}v${ids.length}` }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [{ id: 'strike', name: 'Strike', cost: 2, critical: '10%', targeting: 'One enemy', effects: ['Recorded spell effect'] }] } });
    if (url.pathname.endsWith('/events')) return route.fulfill({ contentType: 'text/event-stream', body: '' });
    if (url.pathname.endsWith('/decision')) {
      const command = request.postDataJSON(); state.writes.push(command);
      if (state.hold) await state.hold;
      if (state.refuse === command.creature) return route.fulfill({ status: 409, json: { message: 'Review this tempo.' } });
      accept(command);
      if (state.lost) { state.lost = false; return route.abort('failed'); }
      return route.fulfill({ status: 204 });
    }
    if (url.pathname === '/api/seat/player1') {
      if (url.searchParams.has('shown')) state.shown.push(Number(url.searchParams.get('shown')));
      return route.fulfill({ json: state.view });
    }
    return route.fulfill({ status: 500 });
  });
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#loading')).toBeHidden(); await page.locator('#planning-open').click();
  return state;
}
async function fill(page, count) { for (let i = 0; i < count; i++) await page.locator('[data-speed="Standard"]').click(); }
for (const count of [1, 2, 3]) test(`${count} creature tabs keep an editable team draft and send once per asking`, async ({ page }, info) => {
  test.setTimeout(60000);
  const h = await mount(page, [1, 2, 3].slice(0, count));
  await expect(page.locator('.tempo-reference')).toContainText('40% Precision crit · current');
  await fill(page, count);
  if (count > 1) await expect(page.locator('.tempo-reference')).toContainText('10% Precision crit · catalogue');
  await page.locator('[data-tempo-tab="1"]').click(); await page.locator('[data-speed="Quick"]').click();
  expect(h.writes).toEqual([]); await expect(page.locator('#planning-confirm')).toHaveText(`Confirm tempos · ${count}/${count}`);
  await fitsViewport(page, '#planning-confirm, #planning-close');
  if (count === 3) await page.screenshot({ path: info.outputPath('au5-team-tempos.png') });
  await page.locator('#planning-confirm').dblclick();
  await expect(page.locator('#planning-dialog')).toBeHidden();
  expect(h.writes).toEqual([1, 2, 3].slice(0, count).map((creature, index) => ({ kind: 'Speed', creature, speed: index ? 'Standard' : 'Quick', asked: 11 + index })));
  expect([...new Set(h.shown)]).toEqual([11, 12, 13].slice(0, count)); expect(h.errors).toEqual([]);
});
test('a refusal after one accepted creature leaves a reviewable draft and requires another confirmation', async ({ page }) => {
  const h = await mount(page); h.refuse = 2; await fill(page, 3); await page.locator('#planning-confirm').click();
  await expect(page.locator('#planning-dialog-error')).toHaveText('Review this tempo.');
  await expect(page.locator('.tempo-accepted')).toContainText('Creature 1: Precision');
  await expect(page.locator('[data-tempo-tab]')).toHaveCount(2); expect(h.writes.map(one => one.creature)).toEqual([1, 2]);
  h.refuse = null; await page.locator('[data-speed="Quick"]').click(); await page.locator('#planning-confirm').click();
  await expect(page.locator('#planning-dialog')).toBeHidden(); expect(h.writes.map(one => one.creature)).toEqual([1, 2, 2, 3]); expect(h.errors).toEqual([]);
});
test('a lost response after acceptance reconciles without resending the accepted creature', async ({ page }) => {
  const h = await mount(page); h.lost = true; await fill(page, 3); await page.locator('#planning-confirm').click();
  await expect(page.locator('#planning-dialog-error')).toContainText('may have arrived');
  await expect(page.locator('[data-tempo-tab]')).toHaveCount(2); await expect(page.locator('#planning-confirm')).toBeEnabled();
  expect(h.writes.map(one => one.creature)).toEqual([1]); await page.locator('#planning-confirm').click();
  await expect(page.locator('#planning-dialog')).toBeHidden(); expect(h.writes.map(one => one.creature)).toEqual([1, 2, 3]); expect(h.errors).toEqual([]);
});
test('backgrounding during a write clears the batch and reopening shows only authoritative remaining choices', async ({ page }) => {
  const h = await mount(page); let release; h.hold = new Promise(resolve => { release = resolve; });
  await fill(page, 3); await page.locator('#planning-confirm').click(); await expect.poll(() => h.writes.length).toBe(1);
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, get: () => true }); document.dispatchEvent(new Event('visibilitychange')); });
  release(); await expect(page.locator('#planning-dialog')).toBeHidden();
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, get: () => false }); document.dispatchEvent(new Event('visibilitychange')); });
  await page.locator('#planning-open').click(); await expect(page.locator('[data-tempo-tab]')).toHaveCount(2);
  await expect(page.locator('#planning-confirm')).toBeDisabled(); expect(h.writes.length).toBe(1); expect(h.errors).toEqual([]);
});
test('keyboard tabs and short landscapes keep the footer reachable without losing drafts to Help', async ({ page }) => {
  await page.setViewportSize({ width: 844, height: 390 }); const h = await mount(page);
  await page.locator('[data-tempo-tab="1"]').focus(); await page.keyboard.press('ArrowRight');
  await expect(page.locator('[data-tempo-tab="2"]')).toHaveAttribute('aria-selected', 'true');
  await page.locator('[data-speed="Quick"]').click(); await page.locator('#planning-dialog [data-help-open]').click();
  await page.locator('#help-close').click(); await expect(page.locator('[data-tempo-tab="2"]')).toContainText('Instinct');
  await fitsViewport(page, '#planning-confirm, #planning-close'); expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
