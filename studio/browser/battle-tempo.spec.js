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
    if (!next) state.afterLast?.();
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
  await expect(page.locator('#loading')).toBeHidden(); await openTempos(page);
  return state;
}
// The team's tempos open by themselves, docked beside the field (ADR 0150); a journey reaches them whether they
// opened, or were closed and wait behind the dock's button.
async function openTempos(page) {
  const dialog = page.locator('#planning-dialog'), button = page.locator('#planning-open');
  await expect.poll(async () => {
    if (await dialog.isVisible()) return true;
    if (await button.isVisible()) await button.click({ timeout: 1000 }).catch(() => {});
    return dialog.isVisible();
  }, { timeout: 15000 }).toBe(true);
}
async function fill(page, count) { for (let i = 0; i < count; i++) await page.locator('[data-speed="Standard"]').click(); }
for (const count of [1, 2, 3]) test(`${count} creature tabs keep an editable team draft and send once per asking`, async ({ page }, info) => {
  test.setTimeout(60000);
  const h = await mount(page, [1, 2, 3].slice(0, count));
  // The creature's spells are the spell choice's cards, their crit chip at Precision with the host's current chance.
  const strike = page.locator('.tempo-reference [data-reference-spell="strike"]');
  await expect(strike.locator('.spell-chip-crit')).toContainText('40%'); await expect(strike.locator('.spell-chip-crit small')).toHaveText('Precision · current');
  await expect(page.locator('#planning-dialog [data-spell]')).toHaveCount(0);
  await fill(page, count);
  if (count > 1) await expect(strike.locator('.spell-chip-crit small')).toHaveText('Precision · catalogue');
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
  await openTempos(page); await expect(page.locator('[data-tempo-tab]')).toHaveCount(2);
  await expect(page.locator('#planning-confirm')).toBeDisabled(); expect(h.writes.length).toBe(1); expect(h.errors).toEqual([]);
});
test('keyboard tabs and short landscapes keep the footer reachable without losing drafts to Help', async ({ page }) => {
  await page.setViewportSize({ width: 844, height: 390 }); const h = await mount(page);
  await page.locator('[data-tempo-tab="1"]').focus(); await page.keyboard.press('ArrowRight');
  await expect(page.locator('[data-tempo-tab="2"]')).toHaveAttribute('aria-selected', 'true');
  await page.locator('[data-speed="Quick"]').click(); await page.locator('#help-open').click();
  await page.locator('#help-close').click(); await expect(page.locator('[data-tempo-tab="2"]')).toContainText('Instinct');
  await fitsViewport(page, '#planning-confirm, #planning-close'); expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
test('the team\'s tempos open by themselves, docked beside a field that stays in view, each tab wearing its tempo', async ({ page }, info) => {
  const h = await mount(page);
  const dialog = page.locator('#planning-dialog');
  await expect(dialog).toHaveAttribute('data-kind', 'Speed');
  expect(await dialog.evaluate(node => node.matches(':modal'))).toBe(false);
  await expect(page.locator('#planning-open')).toBeHidden();
  // The field gives up the room the sheet takes rather than sitting behind it.
  const [sheet, field] = await Promise.all([dialog.boundingBox(), page.locator('#battle-zone').boundingBox()]);
  const beside = sheet.x >= field.x + field.width - 1, below = sheet.y >= field.y + field.height - 1;
  expect(beside || below).toBe(true); expect(field.height).toBeGreaterThan(120);
  await expect(page.locator('#planning-choices [data-speed="Quick"]')).toBeFocused();
  await expect(page.locator('#planning-choices .actor-chip[data-kind="energy"]')).toHaveText('3 energy');
  await page.locator('[data-speed="Quick"]').click();
  await expect(page.locator('[data-tempo-tab="1"]')).toHaveAttribute('data-tempo', 'Quick');
  await expect(page.locator('[data-tempo-tab="1"] .tempo-tab-value')).toHaveText('⚡ Instinct');
  await expect(page.locator('[data-tempo-tab="2"] .tempo-tab-value')).toHaveText('Not chosen');
  await fitsViewport(page, '#planning-confirm, #planning-close, [data-speed]');
  await page.screenshot({ path: info.outputPath('tempo-docked.png') });
  // Escape closes the docked sheet and keeps the draft; the dock's button opens it again, not by itself.
  await page.keyboard.press('Escape'); await expect(dialog).toBeHidden(); await expect(page.locator('#planning-open')).toBeVisible();
  await page.locator('#planning-open').click(); await expect(page.locator('[data-tempo-tab="1"]')).toHaveAttribute('data-tempo', 'Quick');
  expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
test('confirming the last tempo leads to the turn order before the first spell choice of the creature acting first', async ({ page }) => {
  const h = await mount(page); h.afterLast = null;
  const slot = (id, speed) => ({ creature: id, owner: id < 4 ? 'Player1' : 'Player2', speed, initiative: 5 });
  // Once the last tempo is accepted the host builds the order, and the first question is this seat's.
  h.afterLast = () => Object.assign(h.view, { waitingFor: 'Action', waitingCreature: 1, waitingAsked: 20,
    board: { ...h.view.board, phase: 'Combat', subPhase: 'Activation', activationCursor: 0, timeline: [1, 4, 2, 5, 3, 6].map(id => slot(id, 'Standard')) },
    feed: [{ sequence: 7, event: { kind: 'TimelineBuilt', roundId: 1, timeline: { slots: [], rollOffs: [] } } }], feedNext: 8,
    options: { action: { creature: 1, spells: [{ spell: 'strike', legalTargets: { candidates: [4, 5, 6], minTargets: 1, maxTargets: 1 } }] } } });
  await fill(page, 3); await page.locator('#planning-confirm').click();
  await expect(page.locator('#order-continue')).toBeVisible(); await expect(page.locator('#combat-dialog')).toBeHidden();
  await page.locator('#order-continue').click(); await expect(page.locator('#combat-dialog')).toBeVisible();
  expect(h.errors).toEqual([]);
});
test('on a phone the field keeps its two rows beside the tempos and marks the creature they are about', async ({ page }, info) => {
  test.skip(page.viewportSize().width >= 620, 'a phone held upright');
  const h = await mount(page);
  await expect(page.locator('#battle')).toHaveAttribute('data-docked', 'true');
  // Two rows: the team on one line, the opponent on another, never the wide screen's staggered formations.
  const tops = await page.locator('#actor-layer .creature').evaluateAll(nodes => nodes.map(node => [node.classList.contains('enemy'), Math.round(node.getBoundingClientRect().top)]));
  expect(new Set(tops.filter(([enemy]) => !enemy).map(([, top]) => top)).size).toBe(1);
  expect(new Set(tops.filter(([enemy]) => enemy).map(([, top]) => top)).size).toBe(1);
  await expect(page.locator('#actor-layer [data-creature="1"]')).toHaveAttribute('data-sheet-focus', 'true');
  await expect(page.locator('#actor-layer [data-creature="1"] .creature-tag')).toHaveText('CHOOSING TEMPO');
  await expect(page.locator('#actor-layer [data-creature="2"] .creature-tag')).toHaveText('TEMPO ?');
  await page.locator('[data-speed="Quick"]').click();
  await expect(page.locator('#actor-layer [data-creature="1"] .creature-tag')).toHaveText('⚡ INSTINCT');
  await expect(page.locator('#actor-layer [data-creature="2"]')).toHaveAttribute('data-sheet-focus', 'true');
  await page.screenshot({ path: info.outputPath('tempo-field.png') });
  // The creature in hand and its two tempos stay pinned while its spells scroll beneath them.
  await page.locator('#planning-choices').evaluate(node => { node.scrollTop = node.scrollHeight; });
  await expect(page.locator('[data-speed="Quick"]')).toBeInViewport(); await expect(page.locator('[data-speed="Standard"]')).toBeInViewport();
  expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
