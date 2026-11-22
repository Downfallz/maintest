// The match's windows as one family (UX12, docs/art/journeys/windows.md): each window a player opens takes the focus
// in, keeps it in while it is open, closes on Escape and gives the focus back to what opened it, or, when that is
// gone, to the control on the frame that reaches the window again. How each enters and leaves is
// motion-kit.spec.js's; what each window holds is its own spec's.
import { test, expect } from '@playwright/test';
import { servePageArt } from './battle-spells-fixture.js';
import { returningBattlePlayer } from './battle-help-fixture.js';
test.beforeEach(returningBattlePlayer);
const creature = (id, health = 20) => ({ id, name: `Creature ${id}`, health, maxHealth: 20, energy: 4,
  totalDefense: 2, isAlive: health > 0, knownSpells: ['strike'], conditions: [], acquiredTiers: [] });
const roster = [1, 2, 3, 4, 5, 6].map(id => creature(id));
const entry = { sequence: 10, event: { kind: 'CombatActionResolved', roundId: 1,
  resolution: { action: { actor: 4, spell: 'strike', targets: [1] }, isCritical: false },
  appliedOutcomes: [{ kind: 'DamageOutcome', target: 1, amount: 5, critical: false }],
  frame: { before: roster, after: roster.map(one => creature(one.id, one.id === 1 ? 15 : 20)), timeline: [], rollOffs: [] } } };

// A seat in round 2's mutations, with a journal entry, so the bar's windows are all within reach.
async function mount(page) {
  const errors = [];
  const view = {
    board: { slot: 'Player1', roundNumber: 2, phase: 'Planning', subPhase: 'Evolution', allies: roster.slice(0, 3), enemies: roster.slice(3), timeline: [] },
    waitingFor: 'Evolution', waitingAsked: 11, waitingCreature: 1,
    options: { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['root'] }] } },
    feed: [entry], feedNext: 11 };
  page.on('pageerror', error => errors.push(error.message));
  await page.clock.install();
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' }));
    const original = window.fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      const alive = setInterval(() => { try { controller.enqueue(new TextEncoder().encode(': keep-alive\n\n')); } catch { clearInterval(alive); } }, 20000);
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  });
  await servePageArt(page);
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'a', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [{ id: 'strike', name: 'Strike', cost: 2, visual: { portrait: 'strike' } }], packages: [{ id: 'root', name: 'Warden', level: 1, spells: ['strike'] }] } });
    if (url.pathname === '/api/seat/player1') return route.fulfill({ json: view });
    return route.fulfill({ status: 500 });
  });
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 10000));
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#battle')).toHaveAttribute('data-connection', 'live');
  // The mutation phase opens its sheet by itself (ADR 0138); the bar's windows are read with it put away.
  await expect(page.locator('#planning-dialog')).toBeVisible(); await page.locator('#planning-close').click();
  await expect(page.locator('#planning-dialog')).not.toHaveJSProperty('open', true);
  return errors;
}

const focusedIn = (page, id) => page.evaluate(id => document.getElementById(id).contains(document.activeElement), id);

// Each window by the way a player reaches it: the control pressed, and the one the focus comes back to.
const WINDOWS = [
  { id: 'settings-dialog', open: async page => page.locator('#settings'), back: '#settings' },
  { id: 'help-dialog', open: async page => page.locator('#help-open'), back: '#help-open' },
  { id: 'history-dialog', open: async page => page.locator('#history-open'), back: '#history-open' },
  // A window opened from another comes back to the control inside it, the first window still open.
  { id: 'history-dialog', from: 'help', open: async page => {
    await page.locator('#help-open').click(); await expect(page.locator('#help-dialog')).toHaveJSProperty('open', true);
    return page.locator('#help-dialog').getByRole('button', { name: 'History', exact: true });
  }, back: '#help-dialog [data-history-open]', still: 'help-dialog' },
  { id: 'inspect-dialog', open: async page => page.locator('#actor-layer [data-creature="4"]'), back: '#actor-layer [data-creature="4"]' },
  { id: 'atlas-dialog', open: async page => {
    await page.locator('#planning-open').click(); await expect(page.locator('#planning-dialog')).toHaveJSProperty('open', true);
    return page.locator('#mutation-atlas-open');
  }, back: '#mutation-atlas-open' },
];

for (const window of WINDOWS) {
  test(`${window.id}${window.from ? ` from ${window.from}` : ''}: the focus goes in, stays in, comes back, and Escape closes it`, async ({ page }) => {
    const errors = await mount(page);
    const opener = await window.open(page);
    await opener.focus(); await page.keyboard.press('Enter');
    const dialog = page.locator(`#${window.id}`);
    await expect(dialog).toHaveJSProperty('open', true);
    await expect.poll(() => focusedIn(page, window.id), 'the focus goes in').toBe(true);
    for (let step = 0; step < 14; step++) {
      await page.keyboard.press(step % 3 === 2 ? 'Shift+Tab' : 'Tab');
      expect(await focusedIn(page, window.id), `the focus stays in after ${step + 1} presses`).toBe(true);
    }
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveJSProperty('open', false);
    await expect(page.locator(window.back), 'the focus comes back').toBeFocused();
    if (window.still) await expect(page.locator(`#${window.still}`)).toHaveJSProperty('open', true);
    expect(errors).toEqual([]);
  });
}

test('a window whose opener went gives the focus to the frame\'s control that reaches it again', async ({ page }) => {
  const errors = await mount(page);
  await page.locator('#help-open').click(); await expect(page.locator('#help-dialog')).toHaveJSProperty('open', true);
  await page.locator('#help-dialog').getByRole('button', { name: 'History', exact: true }).click();
  await expect(page.locator('#history-dialog')).toHaveJSProperty('open', true);
  // Help closes under the journal, as a round that moves on closes its tip: the button that opened History is gone.
  await page.evaluate(() => document.getElementById('help-dialog').close());
  await expect(page.locator('#help-dialog')).toBeHidden();
  await page.keyboard.press('Escape');
  await expect(page.locator('#history-dialog')).toHaveJSProperty('open', false);
  await expect(page.locator('#history-open')).toBeFocused();
  expect(errors).toEqual([]);
});

// The windows' floor (UX12), as the frame's (UX11, battle-density.spec.js): no word a window shows under 11 px.
const under11 = (page, id) => page.evaluate(id => {
  const found = new Set(), walker = document.createTreeWalker(document.getElementById(id), NodeFilter.SHOW_TEXT);
  while (walker.nextNode()) {
    const text = walker.currentNode, parent = text.parentElement;
    if (!text.textContent.trim() || !parent.getClientRects().length) continue;
    if (parseFloat(getComputedStyle(parent).fontSize) < 11) found.add(text.textContent.trim());
  }
  return [...found];
}, id);

test('no word a window shows is under 11 px', async ({ page }) => {
  await mount(page);
  const read = {};
  for (const [id, opener] of [['help-dialog', '#help-open'], ['history-dialog', '#history-open'], ['settings-dialog', '#settings'],
    ['inspect-dialog', '#actor-layer [data-creature="4"]']]) {
    await page.locator(opener).click(); await expect(page.locator(`#${id}`)).toHaveJSProperty('open', true);
    read[id] = await under11(page, id);
    await page.keyboard.press('Escape'); await expect(page.locator(`#${id}`)).toBeHidden();
  }
  await page.locator('#planning-open').click(); await page.locator('#mutation-atlas-open').click();
  await page.locator('#atlas-dialog .atlas-node').first().click();
  read['atlas-dialog'] = await under11(page, 'atlas-dialog');
  expect(read).toEqual({ 'help-dialog': [], 'history-dialog': [], 'settings-dialog': [], 'inspect-dialog': [], 'atlas-dialog': [] });
});

// The floor raises what was under 11 px and leaves alone what was above it: a heading of the journal or the recap
// keeps its own size.
test('the floor shrinks nothing that was above 11 px', async ({ page }) => {
  await mount(page);
  const sizes = await page.evaluate(() => [['history-detail', null], ['history-dialog', 'history-creature'], ['recap-dialog', 'recap-section']].map(([host, wrap]) => {
    let parent = document.getElementById(host);
    if (wrap) { const box = document.createElement('div'); box.className = wrap; parent.append(box); parent = box; }
    const probe = document.createElement('h4'); probe.textContent = 'Probe'; parent.append(probe);
    const size = getComputedStyle(probe).fontSize; (wrap ? parent : probe).remove(); return size;
  }));
  expect(sizes).toEqual(['14px', '13px', '12px']);
});
