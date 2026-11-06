import { test, expect } from '@playwright/test';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningBattlePlayer);
const roster = [1, 2, 3].map(id => ({ id, name: `Creature ${id}`, isAlive: true, health: 20, maxHealth: 20, energy: 2, conditions: [], acquiredTiers: [], knownSpells: [] }));
const roots = ['Brute', 'Predator', 'Warped'];
const catalogue = { packages: roots.map((name, i) => ({ id: `root${i}`, name, visual: { family: name.toLowerCase() }, level: 1, initiativeBonus: i, spells: [`spell${i}`], tags: [['Guard', 'Pressure'], ['Speed', 'Bleed'], ['Magic', 'Control']][i] })).concat([
  { id: 'future', name: 'Ironhide', level: 2, prerequisites: ['root0'], initiativeBonus: 1, spells: [] },
  { id: 'cap', name: 'Shared capstone', level: 4, anyOf: ['future'], spells: [], initiativeBonus: 0, passiveLines: ['Defense +2', 'For every creature of the side'] },
]), cards: roots.map((name, i) => ({ id: `spell${i}`, name: ['Strike', 'Devour', 'Death Wail'][i], creatureClass: name, cost: i,
  targeting: 'One enemy', effects: [`Recorded effect ${i}`], casterEffects: ['Recorded caster effect'], critical: '0%' })) };
async function mount(page, { content = catalogue, allies = roster, picks = [], ids = ['root0', 'root1', 'root2'], remaining = 2, open = null } = {}) {
  const state = { errors: [], writes: [], shown: [], status: 200, view: {
    board: { slot: 'Player1', roundNumber: 1, phase: 'Planning', subPhase: 'Evolution', allies, enemies: [], evolutionChoices: picks, timeline: [] },
    waitingFor: 'Evolution', waitingAsked: 11, options: { evolution: { remainingPicks: remaining, creatures: allies.filter(one => one.isAlive && !picks.some(pick => pick.creature === one.id)).map(one => ({ creature: one.id, availableTiers: ids })) } }, feed: [], feedNext: 0 } };
  page.on('pageerror', error => state.errors.push(error.message));
  await page.clock.install();
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' }));
    const original = window.fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      window.notifyBattle = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  });
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'a', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: content ?? {} });
    if (url.pathname === '/api/seat/player1') {
      if (url.searchParams.has('shown')) { state.shown.push(url.searchParams.get('shown')); if (state.ack) await state.ack; }
      return route.fulfill({ status: state.status, json: state.view });
    }
    if (url.pathname.endsWith('/decision')) {
      state.writes.push(route.request().postDataJSON()); if (state.hold) await state.hold;
      state.view = state.next ? state.next(state.view) : { ...state.view, waitingFor: null, waitingAsked: null }; state.next = null;
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({ status: 500 });
  });
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#loading')).toBeHidden();
  // The mutation phase opens its tree by itself (ADR 0138); a test can still reopen it from a creature.
  await expect(page.locator('#planning-dialog')).toBeVisible();
  if (open) await page.locator(open).click();
  await page.clock.pauseAt(new Date(Date.now() + 1500));
  const publish = async () => page.evaluate(() => window.notifyBattle());
  return { state, publish };
}
test('a battlefield creature opens three branches and only explicit confirmation submits the named pick', async ({ page }, info) => {
  const h = await mount(page, { open: '#actor-layer [data-creature="2"]' });
  const nodes = page.locator('[data-mutation]');
  await expect(nodes).toHaveCount(3); await expect(page.locator('#mutation-tree')).not.toHaveAttribute('open');
  await expect(page.locator('#planning-creatures [aria-pressed=true]')).toHaveText('Creature 2');
  await expect(page.locator('#planning-confirm')).toBeDisabled();
  await expect(page.locator('.mutation-links path')).toHaveCount(3);
  expect(new Set(await page.locator('.mutation-node').evaluateAll(nodes => nodes.map(node => node.style.getPropertyValue('--mutation-colour')))).size).toBe(3);
  for (let i = 0; i < 3; i++) {
    await nodes.nth(i).click(); await expect(page.locator('.mutation-preview')).toContainText(`+${i} initiative`);
  }
  await nodes.first().click(); await page.locator('.mutation-details summary').click();
  await expect(page.locator('.mutation-details')).toContainText('Strike · 0 energy');
  await expect(page.locator('.mutation-details')).toContainText('Recorded caster effect');
  await nodes.nth(1).click(); await expect(page.locator('.mutation-preview')).toContainText('Devour');
  await expect(page.locator('#actor-layer [data-creature="2"]')).toHaveAttribute('data-form', 'base-creature');
  await expect(page.locator('#actor-layer [data-creature="2"]')).toHaveAttribute('data-preview-form', 'predator');
  await expect(page.locator('#planning-selection')).toHaveText('Creature 2 · Predator');
  await expect(page.locator('#planning-confirm')).toHaveAccessibleName('Confirm Predator · 1 pick · Creature 2');
  await expect(nodes.nth(1)).toHaveAccessibleDescription(/Speed.*Bleed.*Devour/);
  await page.clock.runFor(150);
  await fitsViewport(page, '#planning-creatures, #planning-confirm, #planning-close');
  await page.screenshot({ path: info.outputPath('mutation-opening.jpg'), type: 'jpeg', quality: 90 });
  expect(h.state.shown).toEqual(['11']); expect(h.state.writes).toEqual([]);
  await page.locator('#planning-confirm').dblclick();
  await expect.poll(() => h.state.writes.length).toBe(1);
  expect(h.state.writes[0]).toEqual({ kind: 'Evolution', creature: 2, tier: 'root1', asked: 11 }); expect(h.state.errors).toEqual([]);
});
test('polls and selection preserve detail nodes, focus and deliberate scroll', async ({ page }) => {
  const h = await mount(page); await page.locator('[data-mutation="root2"]').click(); const summary = page.locator('.mutation-details summary').last();
  await summary.click(); await summary.evaluate(node => { node.dataset.sentinel = 'kept'; node.focus(); });
  const scroll = await page.locator('#planning-choices').evaluate(node => node.scrollTop);
  await h.publish(); await page.clock.runFor(50);
  await expect(summary).toBeFocused(); await expect(summary).toHaveAttribute('data-sentinel', 'kept');
  expect(await page.locator('#planning-choices').evaluate(node => node.scrollTop)).toBe(scroll);
  await page.locator('[data-mutation="root2"]').click(); await expect(summary.locator('..')).toHaveAttribute('open');
  await h.publish(); await expect(page.locator('[data-mutation="root2"]')).toHaveAttribute('aria-pressed', 'true');
  expect(h.state.shown).toEqual(['11']); expect(h.state.errors).toEqual([]);
});
test('a keyboard switch of creature keeps focus on the tab that was chosen', async ({ page }) => {
  const h = await mount(page); const tab = page.locator('#planning-creatures [data-creature="2"]');
  await tab.focus(); await page.keyboard.press('Enter');
  await expect(page.locator('#planning-creatures [data-creature="2"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('#planning-creatures [data-creature="2"]')).toBeFocused();
  await page.keyboard.press('Shift+Tab'); await page.keyboard.press('Enter');
  await expect(page.locator('#planning-creatures [data-creature="1"]')).toBeFocused(); expect(h.state.errors).toEqual([]);
});
test('the phone tree leaves the whole battlefield visible and enables switching recipients directly on it', async ({ page }, info) => {
  test.skip(info.project.name === 'desktop', 'Phone layout.');
  const h = await mount(page);
  await page.clock.runFor(150);
  const scene = await page.locator('#battle-zone').boundingBox(), dock = await page.locator('#planning-dialog').boundingBox();
  expect(scene.y + scene.height).toBeLessThanOrEqual(dock.y + 1);
  await page.locator('#mutation-expand').click();
  const expanded = await page.locator('#planning-dialog').boundingBox();
  expect(expanded.height).toBeGreaterThan(dock.height);
  const field = await page.locator('#battle-zone').boundingBox();
  expect(Math.abs(field.y + field.height - expanded.y)).toBeLessThan(2);
  await page.locator('#mutation-expand').click();
  expect(h.state.writes).toEqual([]);
  const boxes = await page.locator('[data-mutation]').evaluateAll(nodes => nodes.map(node => node.getBoundingClientRect().y));
  expect(new Set(boxes).size).toBe(1);
  await page.locator('[data-mutation="root1"]').click(); await page.locator('#actor-layer [data-creature="3"]').click();
  await expect(page.locator('#planning-confirm')).toBeDisabled();
  await expect(page.locator('#planning-creatures [aria-pressed=true]')).toHaveText('Creature 3');
  await page.locator('#mutation-inspect').click(); await expect(page.locator('#inspect-dialog')).toBeVisible();
  await page.keyboard.press('Escape'); await expect(page.locator('#planning-dialog')).toBeVisible();
  await page.keyboard.press('Escape'); await expect(page.locator('#planning-dialog')).toBeHidden();
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('switching recipients clears the old pick and confirmation names the new creature', async ({ page }) => {
  const h = await mount(page); await page.locator('[data-mutation="root0"]').click();
  await page.locator('#planning-creatures [data-creature="2"]').click(); await expect(page.locator('#planning-confirm')).toBeDisabled();
  await page.locator('[data-mutation="root2"]').click(); await expect(page.locator('#planning-selection')).toHaveText('Creature 2 · Warped');
  await page.locator('#planning-confirm').click(); await expect.poll(() => h.state.writes.length).toBe(1);
  expect(h.state.writes[0].creature).toBe(2); expect(h.state.errors).toEqual([]);
});
test('dead and already-picked creatures explain their state and empty offers; pass uses whole-team semantics', async ({ page }) => {
  // As the host sends it: the picks left are already capped at the one creature it can still offer a pick to.
  const h = await mount(page, { allies: [roster[0], { ...roster[1], isAlive: false }, roster[2]], picks: [{ creature: 3, tier: 'root0' }], remaining: 1 });
  await expect(page.locator('#planning-help')).toContainText('1 team pick remaining');
  await expect(page.locator('#planning-creatures [data-creature="2"]')).toHaveAccessibleName(/Defeated/); await expect(page.locator('#planning-creatures [data-creature="3"]')).toHaveAccessibleName(/Picked · hidden until reveal/);
  await page.locator('#planning-creatures [data-creature="3"]').click(); await expect(page.locator('[data-mutation]')).toHaveCount(0);
  await expect(page.locator('.mutation-empty')).toContainText('Choose another creature');
  await page.locator('#mutation-tree > summary').click(); await expect(page.locator('#mutation-tree')).toContainText('Brute · Stage 1 · Picked');
  await page.locator('#planning-pass').click(); await expect(page.locator('#planning-confirm')).toHaveText('Pass all remaining picks');
  await page.locator('#planning-confirm').click(); await expect.poll(() => h.state.writes.length).toBe(1);
  expect(h.state.writes[0]).toEqual({ kind: 'Evolution', pass: true, asked: 11 }); expect(h.state.errors).toEqual([]);
});
test('capstones preserve shared passives, no fake tags or spells, and the tree remains read-only', async ({ page }) => {
  const h = await mount(page, { ids: ['cap'], allies: [{ ...roster[0], acquiredTiers: ['root0', 'future'] }] });
  await expect(page.locator('[data-mutation]')).toHaveCount(1); await page.locator('[data-mutation="cap"]').click();
  await expect(page.locator('.mutation-spell')).toHaveCount(0); await expect(page.locator('.mutation-preview')).toContainText('For every creature of the side');
  await page.locator('#mutation-tree > summary').click(); await expect(page.locator('#mutation-tree')).toContainText('Brute · Stage 1 · Acquired');
  await expect(page.locator('#mutation-tree button')).toHaveCount(0); expect(h.state.writes).toEqual([]);
  await page.locator('[data-mutation="cap"]').click(); await expect(page.locator('#planning-confirm')).toBeEnabled(); expect(h.state.errors).toEqual([]);
});
test('later offers compare multiple upgrades and multiclass roots without forcing three cards', async ({ page }, info) => {
  const content = structuredClone(catalogue);
  content.packages.push({ id: 'upgrade2', name: 'Second upgrade', level: 2, prerequisites: ['root0'], initiativeBonus: 2, spells: ['spell0'] });
  const h = await mount(page, { content, ids: ['future', 'upgrade2', 'root1', 'root2'], allies: [{ ...roster[0], acquiredTiers: ['root0'] }] });
  await expect(page.locator('[data-mutation]')).toHaveCount(4);
  await expect(page.locator('[data-mutation="root0"]')).toHaveCount(0);
  await expect(page.locator('[data-mutation="future"]')).toHaveAccessibleName(/Brute · Stage 2/);
  await expect(page.locator('[data-mutation="root2"]')).toHaveAccessibleName(/Warped · Stage 1/);
  await expect(page.locator('.mutation-node[data-status=known]')).toContainText('Brute');
  await page.locator('[data-mutation="future"]').click(); await page.clock.runFor(200);
  await page.screenshot({ path: info.outputPath('mutation-later.jpg'), type: 'jpeg', quality: 90 });
  await page.locator('[data-mutation="upgrade2"]').click(); await page.locator('[data-mutation="root2"]').click();
  expect(h.state.writes).toEqual([]); await page.locator('#planning-confirm').click();
  await expect.poll(() => h.state.writes.length).toBe(1); expect(h.state.writes[0].tier).toBe('root2'); expect(h.state.errors).toEqual([]);
});
test('missing catalogue still offers the host id with an explicit unknown detail state', async ({ page }) => {
  const h = await mount(page, { content: null, ids: ['unpublished:mutation'] });
  await page.locator('[data-mutation="unpublished:mutation"]').click();
  await expect(page.locator('.mutation-preview')).toContainText('Catalogue details unavailable'); await expect(page.locator('.mutation-preview')).toContainText('Initiative unavailable');
  await page.locator('[data-mutation="unpublished:mutation"]').click(); await page.locator('#planning-confirm').click();
  await expect.poll(() => h.state.writes.length).toBe(1); expect(h.state.writes[0].tier).toBe('unpublished:mutation'); expect(h.state.errors).toEqual([]);
});
test('a changed offer drops the selection while a pending acknowledgement cannot send the stale card', async ({ page }) => {
  const h = await mount(page); await page.locator('#planning-close').click();
  let release; h.state.ack = new Promise(resolve => { release = resolve; });
  await page.locator('#planning-open').click(); await page.locator('[data-mutation="root0"]').click(); await page.locator('#planning-confirm').click();
  h.state.view.options.evolution.creatures[0].availableTiers = ['root1']; await h.publish();
  await expect(page.locator('[data-mutation="root0"]')).toHaveCount(0); release();
  await expect(page.locator('#planning-confirm')).toBeDisabled(); expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('long names and 200 percent text retain reachable confirmation in short portrait and landscape', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'Viewport sweep runs once.');
  const content = structuredClone(catalogue); content.packages[0].name = 'The exceptionally long mutation name of a shared ancestral guardian';
  const h = await mount(page, { content }); await page.locator('[data-mutation="root0"]').click();
  for (const size of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(size); await page.clock.runFor(150); await fitsViewport(page, '#planning-confirm, #planning-close, #planning-creatures');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
  await page.setViewportSize({ width: 390, height: 844 }); await page.addStyleTag({ content: '.mutation-select, .mutation-spell > span, .mutation-passive, .mutation-details { font-size: 26px !important; }' });
  await page.locator('.mutation-details summary').last().scrollIntoViewIfNeeded(); await expect(page.locator('.mutation-details summary').last()).toBeInViewport();
  await fitsViewport(page, '#planning-confirm, #planning-close'); expect(h.state.errors).toEqual([]);
});

test('a short portrait sheet without recipient tabs still starts keyboard focus inside the tree', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 568 }); const h = await mount(page);
  await expect(page.locator('#planning-creatures')).toBeHidden();
  await expect(page.locator('[data-mutation="root0"]')).toBeFocused(); expect(h.state.errors).toEqual([]);
});

test('a creature tapped while the pick is being sent is inspected, not left dead or switched', async ({ page }) => {
  const h = await mount(page, { open: '#actor-layer [data-creature="2"]' });
  let release; h.state.hold = new Promise(resolve => { release = resolve; });
  await page.locator('[data-mutation="root0"]').click(); await page.locator('#planning-confirm').click();
  await expect(page.locator('#planning-confirm')).toHaveText('Sending…');
  await expect(page.locator('#mutation-inspect')).toBeEnabled();
  await page.locator('#actor-layer [data-creature="1"]').click();
  await expect(page.locator('#inspect-dialog')).toBeVisible(); await expect(page.locator('#inspect-name')).toHaveText('Creature 1');
  await page.keyboard.press('Escape'); release();
  await expect.poll(() => h.state.writes).toEqual([{ kind: 'Evolution', creature: 2, tier: 'root0', asked: 11 }]);
  expect(h.state.errors).toEqual([]);
});

test('the mutation phase opens its tree by itself, once per asking', async ({ page }) => {
  const h = await mount(page);
  await expect(page.locator('#planning-open')).toBeHidden(); expect(h.state.shown).toEqual(['11']); expect(h.state.writes).toEqual([]);
  await page.locator('#planning-close').click(); await expect(page.locator('#planning-dialog')).toBeHidden();
  await h.publish(); await page.clock.runFor(100);
  await expect(page.locator('#planning-dialog')).toBeHidden(); await expect(page.locator('#planning-open')).toBeVisible();
  await page.locator('#actor-layer [data-creature="1"]').click(); await expect(page.locator('#planning-dialog')).toBeVisible();
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});

test('a pick that leaves team picks keeps the tree open on the next creature with its remaining picks', async ({ page }) => {
  const h = await mount(page);
  await expect(page.locator('#planning-help')).toHaveText('2 team picks remaining');
  h.state.next = view => ({ ...view, waitingAsked: 12, board: { ...view.board, evolutionChoices: [{ creature: 1, tier: 'root0' }] },
    options: { evolution: { remainingPicks: 1, creatures: view.options.evolution.creatures.filter(one => one.creature !== 1) } } });
  await page.locator('[data-mutation="root0"]').click(); await page.locator('#planning-confirm').click();
  await expect.poll(() => h.state.writes.length).toBe(1);
  await expect(page.locator('#planning-dialog')).toBeVisible();
  await h.publish();
  await expect(page.locator('#planning-help')).toHaveText('1 team pick remaining');
  await expect(page.locator('#planning-creatures [aria-pressed=true]')).toHaveText('Creature 2');
  await expect.poll(() => h.state.shown).toEqual(['11', '12']);
  await expect(page.locator('#planning-dialog :focus')).toHaveCount(1);
  await page.locator('[data-mutation="root1"]').click(); await page.locator('#planning-confirm').click();
  await expect.poll(() => h.state.writes).toEqual([{ kind: 'Evolution', creature: 1, tier: 'root0', asked: 11 }, { kind: 'Evolution', creature: 2, tier: 'root1', asked: 12 }]);
  await expect(page.locator('#planning-dialog')).toBeHidden(); expect(h.state.errors).toEqual([]);
});
