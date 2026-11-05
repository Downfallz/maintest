import { test, expect } from '@playwright/test';
import { returningArenaPlayer } from './arena-help-fixture.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningArenaPlayer);
const roster = [1, 2, 3].map(id => ({ id, name: `Creature ${id}`, isAlive: true, health: 20, maxHealth: 20, energy: 2, conditions: [], acquiredTiers: [], knownSpells: [] }));
const roots = ['Brute', 'Predator', 'Warped'];
const catalogue = { packages: roots.map((name, i) => ({ id: `root${i}`, name, level: 1, initiativeBonus: i, spells: [`spell${i}`], tags: [['Guard', 'Pressure'], ['Speed', 'Bleed'], ['Magic', 'Control']][i] })).concat([
  { id: 'future', name: 'Ironhide', level: 2, prerequisites: ['root0'], initiativeBonus: 1, spells: [] },
  { id: 'cap', name: 'Shared capstone', level: 4, anyOf: ['future'], spells: [], initiativeBonus: 0, passiveLines: ['Defense +2', 'For every creature of the side'] },
]), cards: roots.map((name, i) => ({ id: `spell${i}`, name: ['Strike', 'Devour', 'Death Wail'][i], creatureClass: name, cost: i,
  targeting: 'One enemy', effects: [`Recorded effect ${i}`], casterEffects: ['Recorded caster effect'], critical: '0%' })) };
async function mount(page, { content = catalogue, allies = roster, picks = [], ids = ['root0', 'root1', 'root2'], remaining = 2 } = {}) {
  const state = { errors: [], writes: [], shown: [], status: 200, view: {
    board: { slot: 'Player1', roundNumber: 1, phase: 'Planning', subPhase: 'Evolution', allies, enemies: [], evolutionChoices: picks, timeline: [] },
    waitingFor: 'Evolution', waitingAsked: 11, options: { evolution: { remainingPicks: remaining, creatures: allies.filter(one => one.isAlive && !picks.some(pick => pick.creature === one.id)).map(one => ({ creature: one.id, availableTiers: ids })) } }, feed: [], feedNext: 0 } };
  page.on('pageerror', error => state.errors.push(error.message));
  await page.clock.install();
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' }));
    const original = window.fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      window.notifyArena = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
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
      state.writes.push(route.request().postDataJSON());
      state.view = { ...state.view, waitingFor: null, waitingAsked: null };
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({ status: 500 });
  });
  await page.goto('/table/arena.html'); await expect(page.locator('#arena')).toHaveAttribute('data-ready', 'true');
  await page.locator('#seat-choices button').click(); await expect(page.locator('#loading')).toBeHidden();
  await page.locator('#planning-open').click(); await page.clock.pauseAt(new Date(Date.now() + 1500));
  const publish = async () => page.evaluate(() => window.notifyArena());
  return { state, publish };
}
test('three offered families compare by tags, spells and initiative; details do not select or write', async ({ page }, info) => {
  const h = await mount(page);
  await expect(page.locator('.mutation-card')).toHaveCount(3); await expect(page.locator('#mutation-tree')).not.toHaveAttribute('open');
  for (let i = 0; i < 3; i++) {
    await expect(page.locator('.mutation-card').nth(i)).toContainText(roots[i]);
    await expect(page.locator('.mutation-card').nth(i)).toContainText(`+${i} initiative`);
  }
  expect(new Set(await page.locator('.mutation-card').evaluateAll(nodes => nodes.map(node => node.style.getPropertyValue('--mutation-colour')))).size).toBe(3);
  await expect(page.locator('.mutation-card').first()).toContainText('Guard');
  await expect(page.locator('.mutation-card').nth(1)).toContainText('Devour');
  await page.locator('.mutation-details summary').first().click(); await expect(page.locator('.mutation-details').first()).toContainText('Strike · 0 energy');
  await expect(page.locator('.mutation-details').first()).toContainText('Recorded caster effect');
  await expect(page.locator('#planning-confirm')).toBeDisabled(); expect(h.state.writes).toEqual([]);
  await page.locator('.mutation-details summary').first().click();
  await fitsViewport(page, '#planning-creatures, #planning-confirm, #planning-close');
  if (info.project.name === 'desktop') {
    const boxes = await page.locator('.mutation-card').evaluateAll(nodes => nodes.map(node => ({ x: node.getBoundingClientRect().x, y: node.getBoundingClientRect().y })));
    expect(new Set(boxes.map(one => one.y)).size).toBe(1); expect(new Set(boxes.map(one => one.x)).size).toBe(3);
  }
  await page.screenshot({ path: info.outputPath('mutation-cards.jpg'), type: 'jpeg', quality: 85 });
  await page.locator('[data-mutation="root1"]').click(); await expect(page.locator('#planning-selection')).toHaveText('Creature 1 · Predator');
  await expect(page.locator('#planning-confirm')).toHaveAccessibleName('Acquire · 1 pick: Creature 1 · Predator');
  await expect(page.locator('[data-mutation="root1"]')).toHaveAccessibleName('Predator Predator · Stage 1');
  await expect(page.locator('[data-mutation="root1"]')).toHaveAccessibleDescription(/\+1 initiative.*Devour/);
  expect(h.state.writes).toEqual([]); await page.locator('#planning-confirm').dblclick();
  await expect.poll(() => h.state.writes.length).toBe(1);
  expect(h.state.writes[0]).toEqual({ kind: 'Evolution', creature: 1, tier: 'root1', asked: 11 }); expect(h.state.errors).toEqual([]);
});
test('polls and selection preserve detail nodes, focus and deliberate scroll', async ({ page }) => {
  const h = await mount(page); const summary = page.locator('.mutation-details summary').last();
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
test('phone cards stack in one scrolling column while the tabs and the confirmation stay in view', async ({ page }, info) => {
  test.skip(info.project.name === 'desktop', 'The phone projects own this layout.');
  const h = await mount(page); const choices = page.locator('#planning-choices');
  const boxes = await page.locator('.mutation-card').evaluateAll(nodes => nodes.map(node => node.getBoundingClientRect()).map(box => ({ x: Math.round(box.x), y: box.y })));
  expect(new Set(boxes.map(one => one.x)).size).toBe(1);
  expect(boxes.every((one, i) => i === 0 || one.y > boxes[i - 1].y)).toBe(true);
  expect(await choices.evaluate(node => [getComputedStyle(node).overflowY, node.scrollHeight > node.clientHeight])).toEqual(['auto', true]);
  await choices.evaluate(node => { node.scrollTop = node.scrollHeight; });
  await expect(page.locator('#planning-creatures')).toBeInViewport(); await expect(page.locator('#planning-confirm')).toBeInViewport();
  await fitsViewport(page, '#planning-confirm, #planning-close'); expect(h.state.errors).toEqual([]);
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
  await expect(page.locator('#planning-help')).toContainText('1 team pick available. One mutation per creature');
  await expect(page.locator('#planning-creatures')).toContainText('Defeated'); await expect(page.locator('#planning-creatures')).toContainText('Picked · hidden until reveal');
  await page.locator('#planning-creatures [data-creature="3"]').click(); await expect(page.locator('.mutation-card')).toHaveCount(0);
  await expect(page.locator('.mutation-empty')).toContainText('Choose another creature');
  await page.locator('#mutation-tree > summary').click(); await expect(page.locator('#mutation-tree')).toContainText('Brute · Stage 1 · Picked');
  await page.locator('#planning-pass').click(); await expect(page.locator('#planning-confirm')).toHaveText('Pass all remaining picks');
  await page.locator('#planning-confirm').click(); await expect.poll(() => h.state.writes.length).toBe(1);
  expect(h.state.writes[0]).toEqual({ kind: 'Evolution', pass: true, asked: 11 }); expect(h.state.errors).toEqual([]);
});
test('capstones preserve shared passives, no fake tags or spells, and the tree remains read-only', async ({ page }) => {
  const h = await mount(page, { ids: ['cap'], allies: [{ ...roster[0], acquiredTiers: ['root0', 'future'] }] });
  await expect(page.locator('.mutation-card')).toHaveCount(1); await expect(page.locator('.mutation-tags')).toHaveCount(0);
  await expect(page.locator('.mutation-spell')).toHaveCount(0); await expect(page.locator('.mutation-card')).toContainText('For every creature of the side');
  await page.locator('#mutation-tree > summary').click(); await expect(page.locator('#mutation-tree')).toContainText('Brute · Stage 1 · Acquired');
  await expect(page.locator('#mutation-tree button')).toHaveCount(0); expect(h.state.writes).toEqual([]);
  await page.locator('[data-mutation="cap"]').click(); await expect(page.locator('#planning-confirm')).toBeEnabled(); expect(h.state.errors).toEqual([]);
});
test('later offers compare multiple upgrades and multiclass roots without forcing three cards', async ({ page }) => {
  const content = structuredClone(catalogue);
  content.packages.push({ id: 'upgrade2', name: 'Second upgrade', level: 2, prerequisites: ['root0'], initiativeBonus: 2, spells: ['spell0'] });
  const h = await mount(page, { content, ids: ['future', 'upgrade2', 'root1', 'root2'], allies: [{ ...roster[0], acquiredTiers: ['root0'] }] });
  await expect(page.locator('.mutation-card')).toHaveCount(4);
  await expect(page.locator('[data-mutation="root0"]')).toHaveCount(0);
  await expect(page.locator('[data-mutation="future"] .mutation-family')).toHaveText('Brute · Stage 2');
  await expect(page.locator('[data-mutation="root2"] .mutation-family')).toHaveText('Warped · Stage 1');
  await page.locator('[data-mutation="upgrade2"]').click(); await page.locator('[data-mutation="root2"]').click();
  expect(h.state.writes).toEqual([]); await page.locator('#planning-confirm').click();
  await expect.poll(() => h.state.writes.length).toBe(1); expect(h.state.writes[0].tier).toBe('root2'); expect(h.state.errors).toEqual([]);
});
test('missing catalogue still offers the host id with an explicit unknown detail state', async ({ page }) => {
  const h = await mount(page, { content: null, ids: ['unpublished:mutation'] });
  await expect(page.locator('.mutation-card')).toContainText('Catalogue details unavailable'); await expect(page.locator('.mutation-card')).toContainText('Initiative unavailable');
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
