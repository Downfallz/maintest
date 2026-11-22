import { test, expect } from '@playwright/test';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { servePageArt } from './battle-spells-fixture.js';
import { fitsViewport } from './fit.js';
import { decided } from './decision.js';
test.beforeEach(returningBattlePlayer);
const roster = [1, 2, 3].map(id => ({ id, name: `Creature ${id}`, isAlive: true, health: 20, maxHealth: 20, energy: 2, conditions: [], acquiredTiers: [], knownSpells: [] }));
const roots = ['Brute', 'Predator', 'Warped'];
const catalogue = { packages: roots.map((name, i) => ({ id: `root${i}`, name, visual: { family: name.toLowerCase(), artwork: `${name.toLowerCase()}-base` }, level: 1, initiativeBonus: i, spells: [`spell${i}`], tags: [['Guard', 'Pressure'], ['Speed', 'Bleed'], ['Magic', 'Control']][i] })).concat([
  { id: 'future', name: 'Ironhide', visual: { family: 'brute', artwork: 'brute-stage2-ironhide' }, level: 2, prerequisites: ['root0'], initiativeBonus: 1, spells: [] },
  { id: 'cap', name: 'Shared capstone', visual: { family: 'brute', portrait: 'titan' }, level: 4, anyOf: ['future'], spells: [], initiativeBonus: 0, passiveLines: ['Defense +2', 'For every creature of the team'] },
]), cards: roots.map((name, i) => ({ id: `spell${i}`, name: ['Strike', 'Devour', 'Death Wail'][i], creatureClass: name, cost: i,
  targeting: 'One enemy', effects: [`Recorded effect ${i}`], casterEffects: ['Recorded caster effect'], critical: i === 1 ? '15%' : '0%',
  criticalThreshold: i === 1 ? 18 : null, criticalNote: i === 1 ? 'A natural 18 or more doubles the damage.' : null,
  marks: [{ kind: 'DamageEffect', onCaster: false }, { kind: 'EnergyEffect', onCaster: true }] })),
effects: [{ kind: 'DamageEffect', glyph: '♥', harmful: true }, { kind: 'EnergyEffect', glyph: 'ϟ', harmful: false }] };
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
      // A host says it is still there every twenty seconds (ADR 0119): without it the page takes a quiet stream for a
      // dropped one after 45 seconds (ADR 0164) and opens another, and a long test would notify the closed one.
      const alive = setInterval(() => { try { controller.enqueue(new TextEncoder().encode(': keep-alive\n\n')); } catch { clearInterval(alive); } }, 20000);
      window.notifyBattle = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  });
  await servePageArt(page);
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'a', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: content ?? {} });
    if (url.pathname === '/api/seat/player1') {
      if (url.searchParams.has('shown')) { state.shown.push(url.searchParams.get('shown')); if (state.ack) await state.ack; }
      // An acknowledgement reaches no board, so it never says a change arrived.
      const view = state.view; await route.fulfill({ status: state.status, json: view }); if (!url.searchParams.has('shown')) state.answered = view; return;
    }
    if (url.pathname.endsWith('/decision')) {
      state.writes.push(decided(route.request())); if (state.hold) await state.hold;
      state.view = state.next ? state.next(state.view) : { ...state.view, waitingFor: null, waitingAsked: null }; state.next = null;
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({ status: 500 });
  });
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#loading')).toBeHidden(); await expect(page.locator('#battle')).toHaveAttribute('data-connection', 'live');
  // The mutation phase opens its tree by itself (ADR 0138); a test can still reopen it from a creature.
  await expect(page.locator('#planning-dialog')).toBeVisible();
  if (open) await page.locator(open).click();
  await page.clock.pauseAt(new Date(Date.now() + 1500));
  const publish = async () => {
    state.view = { ...state.view }; const published = state.view;
    await page.evaluate(() => window.notifyBattle());
    // A change told while a read is in flight is owed one more read, which the page schedules at no delay: a paused
    // clock runs it only when it is moved. Only a seat read that is no acknowledgement, answered with the published view
    // (a refusal too), says the change reached the page (#768, #785).
    await expect.poll(async () => { if (state.answered === published) { return true; } await page.clock.runFor(0); return false; }).toBe(true);
  };
  return { state, publish };
}
test('a battlefield creature opens three branches and only explicit confirmation submits the named pick', async ({ page }, info) => {
  const h = await mount(page, { open: '#actor-layer [data-creature="2"]' });
  const nodes = page.locator('[data-mutation]');
  await expect(nodes).toHaveCount(3); await expect(page.locator('#atlas-dialog')).toBeHidden();
  await expect(page.locator('#planning-creatures [aria-pressed=true]')).toHaveText('Creature 2');
  await expect(page.locator('#planning-confirm')).toBeDisabled();
  // The links are drawn on the frame after the tree is laid out (onNextFrame); the paused clock gives it one.
  await page.clock.runFor(50); await expect(page.locator('.mutation-links path')).toHaveCount(3);
  expect(new Set(await page.locator('.mutation-node').evaluateAll(nodes => nodes.map(node => node.style.getPropertyValue('--mutation-colour')))).size).toBe(3);
  for (let i = 0; i < 3; i++) {
    await nodes.nth(i).click(); await expect(page.locator('.mutation-preview')).toContainText(`+${i} initiative`);
  }
  await nodes.first().click(); const card = page.locator('.mutation-preview .spell-card');
  await expect(card.locator('.spell-card-name')).toHaveText('Strike'); await expect(card.locator('.spell-chip-cost')).toHaveText('0 energy');
  await expect(card.locator('.spell-chip-crit')).toHaveCount(0);
  await expect(card.locator('.spell-card-effect[data-caster="true"]')).toHaveText('Recorded caster effect');
  await nodes.nth(1).click(); await expect(page.locator('.mutation-preview')).toContainText('Devour');
  await expect(page.locator('#actor-layer [data-creature="2"]')).toHaveAttribute('data-form', 'base-creature');
  await expect(page.locator('#actor-layer [data-creature="2"]')).toHaveAttribute('data-preview-form', 'predator');
  await expect(page.locator('#planning-selection')).toHaveText('Creature 2 · Predator');
  await expect(page.locator('#planning-confirm')).toHaveAccessibleName('Confirm Predator · 1 pick · Creature 2');
  await expect(nodes.nth(1)).toHaveAccessibleDescription(/Speed.*Bleed.*Devour/);
  await page.clock.runFor(150);
  await fitsViewport(page, '#planning-creatures, #planning-confirm, #planning-close');
  await page.screenshot({ path: info.outputPath('mutation-opening.jpg'), type: 'jpeg', quality: 90 });
  await expect.poll(() => h.state.shown).toEqual(['11']); expect(h.state.writes).toEqual([]);
  await page.locator('#planning-confirm').dblclick();
  await expect.poll(() => h.state.writes.length).toBe(1);
  expect(h.state.writes[0]).toEqual({ kind: 'Evolution', creature: 2, tier: 'root1', asked: 11 }); expect(h.state.errors).toEqual([]);
});
test('polls and selection preserve detail nodes, focus and deliberate scroll', async ({ page }) => {
  const h = await mount(page); const offer = page.locator('[data-mutation="root2"]');
  await offer.click(); await offer.evaluate(node => { node.dataset.sentinel = 'kept'; node.focus(); });
  const scroll = await page.locator('#planning-choices').evaluate(node => node.scrollTop);
  await h.publish(); await page.clock.runFor(50);
  await expect(offer).toBeFocused(); await expect(offer).toHaveAttribute('data-sentinel', 'kept');
  expect(await page.locator('#planning-choices').evaluate(node => node.scrollTop)).toBe(scroll);
  await expect(page.locator('.mutation-preview .spell-card')).toHaveAttribute('data-spell', 'spell2');
  await h.publish(); await expect(page.locator('[data-mutation="root2"]')).toHaveAttribute('aria-pressed', 'true');
  await expect.poll(() => h.state.shown).toEqual(['11']); expect(h.state.errors).toEqual([]);
});
test('a keyboard switch of creature keeps focus on the tab that was chosen', async ({ page }) => {
  const h = await mount(page); const tab = page.locator('#planning-creatures [data-creature="2"]');
  await tab.focus(); await page.keyboard.press('Enter');
  await expect(page.locator('#planning-creatures [data-creature="2"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('#planning-creatures [data-creature="2"]')).toBeFocused();
  await page.keyboard.press('Shift+Tab'); await page.keyboard.press('Enter');
  await expect(page.locator('#planning-creatures [data-creature="1"]')).toBeFocused(); expect(h.state.errors).toEqual([]);
});
test('the phone tree opens expanded beside the whole battlefield, its tools in its header, and switches recipients on the field', async ({ page }, info) => {
  test.skip(info.project.name === 'desktop', 'Phone layout.');
  const h = await mount(page);
  await page.clock.runFor(150);
  // Always at its full height: no Expand or Compact; the field ends where the sheet begins.
  const scene = await page.locator('#battle-zone').boundingBox(), dock = await page.locator('#planning-dialog').boundingBox();
  await expect(page.locator('#mutation-expand')).toHaveCount(0);
  expect(Math.abs(scene.y + scene.height - dock.y)).toBeLessThan(2);
  expect(dock.height).toBeGreaterThan(page.viewportSize().height * 0.45);
  // The header holds the full tree and the pass, in place of Help and History the field's header already offers.
  const header = await page.locator('#planning-dialog .planning-header').boundingBox();
  for (const id of ['#mutation-atlas-open', '#planning-pass']) {
    const box = await page.locator(id).boundingBox();
    expect(box.y).toBeGreaterThanOrEqual(header.y); expect(box.y + box.height).toBeLessThanOrEqual(header.y + header.height + 1);
  }
  for (const node of await page.locator('#planning-dialog .history-reference').all()) await expect(node).toBeHidden();
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
  await page.locator('#mutation-atlas-open').click();
  await expect(page.locator('#atlas-board [data-package="root0"]')).toHaveAttribute('data-status', 'picked');
  await expect(page.locator('#atlas-board [data-package="root0"]')).toContainText('Picked'); await page.locator('#atlas-close').click();
  await page.locator('#planning-pass').click(); await expect(page.locator('#planning-confirm')).toHaveText('Pass all remaining picks');
  await page.locator('#planning-confirm').click(); await expect.poll(() => h.state.writes.length).toBe(1);
  expect(h.state.writes[0]).toEqual({ kind: 'Evolution', pass: true, asked: 11 }); expect(h.state.errors).toEqual([]);
});
test('capstones preserve shared passives, no fake tags or spells, and the tree remains read-only', async ({ page }) => {
  const h = await mount(page, { ids: ['cap'], allies: [{ ...roster[0], acquiredTiers: ['root0', 'future'] }] });
  await expect(page.locator('[data-mutation]')).toHaveCount(1); await page.locator('[data-mutation="cap"]').click();
  await expect(page.locator('.mutation-preview .spell-card')).toHaveCount(0); await expect(page.locator('.mutation-preview')).toContainText('For every creature of the team');
  const portrait = 'img.spell-portrait[src*="/page-art/spell-titan-40."][sizes="24px"]';
  const loaded = image => image.complete && image.naturalWidth > 0;
  await expect.poll(() => page.locator('.mutation-preview').locator(portrait).evaluate(loaded)).toBe(true);
  // The full tree reads what is held without offering it; only the offer is chosen, and only from its card.
  await page.locator('#mutation-atlas-open').click(); const tree = page.locator('#atlas-board'), detail = page.locator('#atlas-detail-body');
  await expect.poll(() => detail.locator(portrait).evaluate(loaded)).toBe(true);
  await expect(tree.locator('[data-package="cap"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(tree.locator('[data-package="cap"]')).toHaveAttribute('data-path', 'true');
  await expect(tree.locator('[data-package="root0"]')).toHaveAttribute('data-path', 'true');
  await tree.locator('[data-package="root0"]').click(); await expect(detail).toContainText('Acquired');
  await expect(detail.locator('.atlas-choose')).toHaveCount(0); expect(h.state.writes).toEqual([]);
  await detail.getByRole('button', { name: 'Ironhide' }).click(); await expect(detail.locator('.atlas-card-name')).toHaveText('Ironhide');
  await detail.getByRole('button', { name: 'Shared capstone' }).click(); await detail.getByRole('button', { name: 'Choose Shared capstone' }).click();
  await expect(page.locator('#atlas-dialog')).toBeHidden(); await expect(page.locator('#planning-confirm')).toBeEnabled();
  await expect(page.locator('#planning-selection')).toHaveText('Creature 1 · Shared capstone'); expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('later offers compare multiple upgrades and multiclass roots without forcing three cards', async ({ page }, info) => {
  const content = structuredClone(catalogue);
  content.packages.push({ id: 'upgrade2', name: 'Second upgrade', level: 2, prerequisites: ['root0'], initiativeBonus: 2, spells: ['spell0'] });
  const h = await mount(page, { content, ids: ['future', 'upgrade2', 'root1', 'root2'], allies: [{ ...roster[0], acquiredTiers: ['root0'] }] });
  await expect(page.locator('[data-mutation]')).toHaveCount(4);
  await expect(page.locator('[data-mutation="root0"]')).toHaveCount(0);
  await expect(page.locator('[data-mutation="future"]')).toHaveAccessibleName(/Brute · Stage 2\b/);
  await expect(page.locator('[data-mutation="root2"]')).toHaveAccessibleName(/Warped · Stage 1\b/);
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
  await page.setViewportSize({ width: 390, height: 844 }); await page.addStyleTag({ content: '.mutation-select, .mutation-passive, .spell-card, .planning-action { font-size: 26px !important; }' });
  await page.locator('#mutation-atlas-open').scrollIntoViewIfNeeded(); await expect(page.locator('#mutation-atlas-open')).toBeInViewport();
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
  await expect(page.locator('#planning-open')).toBeHidden(); await expect.poll(() => h.state.shown).toEqual(['11']); expect(h.state.writes).toEqual([]);
  await page.locator('#planning-close').click(); await expect(page.locator('#planning-dialog')).toBeHidden();
  await h.publish(); await page.clock.runFor(100);
  await expect(page.locator('#planning-dialog')).toBeHidden(); await expect(page.locator('#planning-open')).toBeVisible();
  await page.locator('#actor-layer [data-creature="1"]').click(); await expect(page.locator('#planning-dialog')).toBeVisible();
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});

test('a pick that leaves team picks keeps the tree open on the next creature with its remaining picks', async ({ page }) => {
  const h = await mount(page);
  await expect(page.locator('#planning-help')).toHaveText('2 team picks remaining');
  // The budget is the team's: a token per pick, and a line saying two of the three creatures mutate now. The note
  // opens the tree and scrolls with it, so it takes none of a short sheet's fixed room.
  const pips = page.locator('#planning-pick-rule .pick-pip'), rule = page.locator('#planning-choices > #planning-pick-rule');
  await expect(pips).toHaveCount(2); await expect(page.locator('#planning-pick-rule .pick-pip[data-spent="true"]')).toHaveCount(0);
  await expect(rule).toBeVisible();
  await expect(rule).toHaveText('3 creatures can mutate, 2 picks left: choose which 2 mutate now, one pick each. Rules');
  await expect(rule.locator('a')).toHaveAttribute('href', '/rules#evolution');
  h.state.next = view => ({ ...view, waitingAsked: 12, board: { ...view.board, evolutionChoices: [{ creature: 1, tier: 'root0' }] },
    options: { evolution: { remainingPicks: 1, creatures: view.options.evolution.creatures.filter(one => one.creature !== 1) } } });
  await page.locator('[data-mutation="root0"]').click(); await page.locator('#planning-confirm').click();
  await expect.poll(() => h.state.writes.length).toBe(1);
  await expect(page.locator('#planning-dialog')).toBeVisible();
  await h.publish();
  await expect(page.locator('#planning-help')).toHaveText('1 team pick remaining');
  await expect(pips).toHaveCount(2); await expect(page.locator('#planning-pick-rule .pick-pip[data-spent="true"]')).toHaveCount(1);
  await expect(rule).toContainText('2 creatures can mutate, 1 pick left: choose which one mutates now');
  await expect(page.locator('#planning-creatures [aria-pressed=true]')).toHaveText('Creature 2');
  await expect.poll(() => h.state.shown).toEqual(['11', '12']);
  await expect(page.locator('#planning-dialog :focus')).toHaveCount(1);
  await page.locator('[data-mutation="root1"]').click(); await page.locator('#planning-confirm').click();
  await expect.poll(() => h.state.writes).toEqual([{ kind: 'Evolution', creature: 1, tier: 'root0', asked: 11 }, { kind: 'Evolution', creature: 2, tier: 'root1', asked: 12 }]);
  await expect(page.locator('#planning-dialog')).toBeHidden(); expect(h.state.errors).toEqual([]);
});

test('the full mutation tree opens over the pick on its offer, shows each spell as a card and chooses only from that card', async ({ page }, info) => {
  const h = await mount(page); await page.locator('[data-mutation="root1"]').click();
  await page.locator('#mutation-atlas-open').click();
  const dialog = page.locator('#atlas-dialog'), tree = page.locator('#atlas-board'), detail = page.locator('#atlas-detail-body');
  await expect(dialog).toBeVisible(); await expect(page.locator('#atlas-creature')).toHaveValue('1');
  await expect(tree.locator('.atlas-family')).toHaveCount(3);
  await expect(tree.locator('[data-package="root1"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(tree.locator('[data-status="available"]')).toHaveCount(3); await expect(tree.locator('[data-package="future"]')).toHaveAttribute('data-status', 'future');
  const card = detail.locator('.spell-card');
  await expect(card.locator('.spell-card-name')).toHaveText('Devour'); await expect(card.locator('.spell-chip-cost')).toHaveText('1 energy');
  await expect(card.locator('.spell-chip-crit')).toContainText('15%'); await expect(card.locator('.spell-chip-crit')).toContainText('d20 18+');
  await expect(card.locator('.spell-card-effect').first()).toHaveAttribute('data-cue', 'health-loss');
  await expect(card.locator('.spell-card-effect').last()).toHaveAttribute('data-cue', 'energy-gain');
  await expect(card.locator('.spell-card-note')).toHaveText('A natural 18 or more doubles the damage.');
  await expect(detail.locator('.atlas-chip-init')).toHaveText('+1 initiative');
  // Choose sits right under the offer's name, before its spells, and stays pinned at the top as the card scrolls.
  const bar = detail.locator('.atlas-choose-bar');
  expect(await detail.evaluate(body => [...body.children].findIndex(child => child.matches('.atlas-choose-bar')))).toBe(1);
  await detail.evaluate(body => { body.append(Object.assign(document.createElement('div'), { style: 'height: 2000px' })); body.scrollTop = body.scrollHeight; });
  const [panel, pinned] = await Promise.all([detail.boundingBox(), bar.boundingBox()]);
  expect(Math.abs(pinned.y - panel.y)).toBeLessThanOrEqual(1); await expect(bar.getByRole('button', { name: 'Choose Predator' })).toBeInViewport();
  // The pinned bar spans the card's top edge, but the card's own close button stays on top of it and takes the tap.
  expect(await page.locator('#atlas-detail-close').evaluate(close => {
    const box = close.getBoundingClientRect(); return document.elementFromPoint(box.left + box.width / 2, box.top + box.height / 2) === close;
  })).toBe(true);
  await detail.evaluate(body => { body.lastElementChild.remove(); body.scrollTop = 0; });
  await fitsViewport(page, '#atlas-close, #atlas-creature'); await page.clock.runFor(150);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath('mutation-atlas.jpg'), type: 'jpeg', quality: 90 });
  // Another creature's tree is read without changing the recipient; choosing there switches to it.
  await page.locator('#atlas-creature').selectOption('2'); await expect(page.locator('#planning-selection')).toHaveText('Creature 1 · Predator');
  await tree.locator('[data-package="root2"]').click(); await detail.getByRole('button', { name: 'Choose Warped' }).click();
  await expect(dialog).toBeHidden(); await expect(page.locator('#mutation-atlas-open')).toBeFocused();
  await expect(page.locator('#planning-creatures [aria-pressed=true]')).toHaveText('Creature 2');
  await expect(page.locator('#planning-selection')).toHaveText('Creature 2 · Warped'); expect(h.state.writes).toEqual([]);
  await page.locator('#planning-confirm').click(); await expect.poll(() => h.state.writes.length).toBe(1);
  expect(h.state.writes[0]).toEqual({ kind: 'Evolution', creature: 2, tier: 'root2', asked: 11 }); expect(h.state.errors).toEqual([]);
});

test('on a phone the tree shows a triangle on each side that still has families, and a tap moves one family over', async ({ page }, info) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  const h = await mount(page); await page.locator('#mutation-atlas-open').click();
  const tree = page.locator('#atlas-board'), prev = page.locator('#atlas-prev'), next = page.locator('#atlas-next');
  await expect(page.locator('#atlas-dialog')).toBeVisible(); await expect(tree.locator('.atlas-family')).toHaveCount(3);
  if (page.viewportSize().width >= 820) { await expect(prev).toBeHidden(); await expect(next).toBeHidden(); return; }
  // Nothing is selected on arrival: the tree opens whole, no card over it.
  await expect(page.locator('#atlas-detail')).toBeHidden();
  await tree.evaluate(node => { node.scrollLeft = 0; }); await page.clock.runFor(50);
  await expect(prev).toBeHidden(); await expect(next).toBeVisible();
  await page.screenshot({ path: info.outputPath('mutation-atlas-swipe.png') });
  await next.click(); await expect.poll(() => tree.evaluate(node => node.scrollLeft)).toBeGreaterThan(0);
  await expect(prev).toBeVisible();
  await next.click(); await expect(next).toBeHidden(); await expect(prev).toBeVisible();
  await prev.click(); await expect(next).toBeVisible();
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});

test('the full mutation tree closes when the question it chose for is gone', async ({ page }) => {
  const h = await mount(page); await page.locator('#mutation-atlas-open').click(); await expect(page.locator('#atlas-dialog')).toBeVisible();
  h.state.view = { ...h.state.view, waitingFor: null, waitingAsked: null, options: {} }; await h.publish(); await page.clock.runFor(50);
  await expect(page.locator('#atlas-dialog')).toBeHidden(); await expect(page.locator('#atlas-board')).toBeEmpty();
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('a private preview draws the illustration the drafted mutation would give the creature', async ({ page }) => {
  // The catalogue names each root's illustration (ADR 0151); a draft shows it on its recipient before any confirm.
  const content = { ...catalogue, packages: catalogue.packages.map((pack, index) => index < 3 ? { ...pack, visual: { ...pack.visual, artwork: ['brute-base', 'predator-base', 'warped-base'][index] } } : pack) };
  const h = await mount(page, { content });
  await page.locator('[data-mutation="root1"]').click();
  await expect(page.locator('#planning-selection')).toHaveText('Creature 1 · Predator');
  await page.clock.runFor(200);
  await expect(page.locator('#actor-layer [data-creature="1"]')).toHaveAttribute('data-artwork-shown', 'predator-base');
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});

// The forms a mutation leads to (UX12 P5): the catalogue's `artwork`, 64 px wide (the paintings are 3:2), from its own 64 or 128 px files.
const formsAsked = page => {
  const asked = [];
  page.on('request', request => { const name = /\/page-art\/mutation-([a-z0-9-]+?)-(?:64|128)\./.exec(request.url()); if (name) asked.push(name[1]); });
  return asked;
};
const drawnAt64 = image => image.evaluate(one => [one.getBoundingClientRect().width, Math.round(one.getBoundingClientRect().height), one.alt]);

test('each offer of the pick shows the form it leads to at 64 px, named by its words, in a short portrait sheet', async ({ page }, info) => {
  await page.setViewportSize({ width: 320, height: 568 });
  const asked = formsAsked(page); const h = await mount(page);
  const offers = page.locator('[data-mutation]'); await expect(offers).toHaveCount(3);
  for (const [i, name] of roots.entries()) {
    const offer = page.locator(`[data-mutation="root${i}"]`), form = offer.locator('img.mutation-form');
    await expect(form).toHaveCount(1); await expect(offer.locator('svg')).toHaveCount(0);
    await expect(form).toHaveAttribute('loading', 'lazy');
    await expect(form).toHaveAttribute('srcset', new RegExp(`mutation-${name.toLowerCase()}-base-64\\.[0-9a-f]+\\.webp 64w, .*mutation-${name.toLowerCase()}-base-128\\.`));
    expect(await drawnAt64(form)).toEqual([64, 43, '']);
    // The form adds no word: the offer is still named by the mutation it shows.
    await expect(offer.locator('.mutation-name')).toHaveText(name); await expect(offer).toHaveAccessibleName(new RegExp(`^${name}`));
  }
  await offers.first().scrollIntoViewIfNeeded();
  await expect.poll(() => offers.first().locator('img').evaluate(image => image.currentSrc)).toMatch(/mutation-brute-base-(64|128)\.[0-9a-f]+\.webp$/);
  await expect(page.locator('#planning-creatures')).toBeHidden();
  await offers.first().click(); await page.clock.runFor(150);
  await fitsViewport(page, '#planning-confirm, #planning-close');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath('mutation-forms-pick.png') });
  // The tree's forms are its own: none is asked for before the Full Mutation Tree opens.
  expect(asked.filter(name => !['brute-base', 'predator-base', 'warped-base'].includes(name))).toEqual([]);
  expect(h.state.errors).toEqual([]);
});

test('the phone tree shows each mutation\'s form on its board and in its detail, a capstone its portrait, none before it opens', async ({ page }, info) => {
  await page.setViewportSize({ width: 320, height: 568 });
  const asked = formsAsked(page); const h = await mount(page);
  await page.locator('[data-mutation="root1"]').click(); await page.locator('#mutation-atlas-open').click();
  const tree = page.locator('#atlas-board'), detail = page.locator('#atlas-detail-body');
  await expect(page.locator('#atlas-dialog')).toBeVisible();
  for (const [id, artwork] of [['root0', 'brute-base'], ['root1', 'predator-base'], ['root2', 'warped-base'], ['future', 'brute-stage2-ironhide']]) {
    const form = tree.locator(`[data-package="${id}"] img.mutation-form`);
    await expect(form).toHaveCount(1); await expect(form).toHaveAttribute('src', new RegExp(`mutation-${artwork}-`));
    expect(await drawnAt64(form)).toEqual([64, 43, '']);
  }
  await expect(tree.locator('[data-package="future"]')).toHaveAccessibleName(/^Ironhide/);
  await expect(tree.locator('[data-package="future"] .atlas-node-name')).toHaveText('Ironhide');
  // A capstone names no form: it keeps its portrait, at 24 px.
  const cap = tree.locator('[data-package="cap"]');
  await expect(cap.locator('img.mutation-form')).toHaveCount(0); await expect(cap.locator('img.spell-portrait')).toHaveCount(1);
  expect(await cap.locator('img').evaluate(image => image.getBoundingClientRect().width)).toBe(24);
  await tree.locator('[data-package="future"]').scrollIntoViewIfNeeded();
  await expect.poll(() => asked).toContain('brute-stage2-ironhide');
  // The detail's form stands left of the name, and Choose stays on the detail's first screen.
  const head = detail.locator('.atlas-card-head'), form = head.locator('img.mutation-form');
  await expect(head.locator('.atlas-card-name')).toHaveText('Predator');
  await expect(form).toHaveAttribute('src', /mutation-predator-base-/); expect(await drawnAt64(form)).toEqual([64, 43, '']);
  const [picture, name] = await Promise.all([form.boundingBox(), head.locator('.atlas-card-name').boundingBox()]);
  expect(picture.x + picture.width).toBeLessThanOrEqual(name.x);
  await expect(detail.getByRole('button', { name: 'Choose Predator' })).toBeInViewport();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath('mutation-forms-tree.png') });
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});

test('a form that fails to load leaves no gap, and the mutation keeps its name', async ({ page }) => {
  const h = await mount(page);
  await page.route('**/page-art/mutation-*', route => route.fulfill({ status: 404, body: '' }));
  await page.locator('#mutation-atlas-open').click(); await expect(page.locator('#atlas-dialog')).toBeVisible();
  const tile = page.locator('#atlas-board [data-package="future"]'), form = tile.locator('img.mutation-form');
  await tile.scrollIntoViewIfNeeded();
  await expect(form).toHaveClass(/missing/); await expect(form).toBeHidden();
  await expect(tile.locator('.atlas-node-name')).toHaveText('Ironhide'); await expect(tile).toHaveAccessibleName(/^Ironhide/);
  // In the detail too: without its form the head is the family above the name, not the two on one line. Ironhide
  // is no offer of the pick, so its form was never loaded before the failure.
  await tile.click(); const head = page.locator('#atlas-detail-body .atlas-card-head');
  await expect(head.locator('.mutation-form')).toHaveClass(/missing/); await expect(head.locator('.atlas-card-name')).toHaveText('Ironhide');
  const [family, name] = await Promise.all([head.locator('.atlas-card-family').boundingBox(), head.locator('.atlas-card-name').boundingBox()]);
  expect(family.y + family.height).toBeLessThanOrEqual(name.y);
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});

test('the tree opens on the moult behind its title, and each family column on its scene, loaded only when it opens', async ({ page }, info) => {
  const asked = [];
  page.on('request', request => { const name = /\/page-art\/(brute-moult|ironhide-scar|blightweaver-pressure-sac|warped-listening)-/.exec(request.url()); if (name) asked.push(name[1]); });
  const h = await mount(page);
  // docs/art/scene-paintings-plan.md, S11: no scene of the tree loads with the pick.
  expect(asked).toEqual([]);
  await page.locator('#mutation-atlas-open').click(); await expect(page.locator('#atlas-dialog')).toBeVisible();
  const band = page.locator('#atlas-dialog .atlas-band');
  await expect(band).toHaveClass(/scene-head/); await expect(band).toHaveAttribute('data-side', 'start');
  await expect(band.locator('img[data-scene]')).toHaveAttribute('src', /page-art\/brute-moult-/);
  await expect(band.locator('img[data-scene]')).toHaveAttribute('alt', '');
  await expect(band.locator('.atlas-words')).toHaveClass(/scene-words/);
  // The Close keeps its corner of the band; the creature's list sits under the band on a phone, in its lower corner on
  // a wide screen. Neither covers the title, and both are in view and take taps.
  const [close, list, title, box] = await Promise.all(['#atlas-close', '#atlas-creature', '#atlas-title', '#atlas-dialog .atlas-band'].map(one => page.locator(one).boundingBox()));
  const apart = (a, b) => a.x + a.width <= b.x || b.x + b.width <= a.x || a.y + a.height <= b.y || b.y + b.height <= a.y;
  expect(apart(close, title)).toBe(true); expect(apart(list, title)).toBe(true); expect(apart(list, close)).toBe(true);
  if (page.viewportSize().width < 820) expect(list.y).toBeGreaterThanOrEqual(box.y + box.height - 1);
  else expect(list.y + list.height).toBeLessThanOrEqual(box.y + box.height);
  await fitsViewport(page, '#atlas-close, #atlas-creature');
  // The band's room is a box of its own in the Close's cell: it never takes the Close's taps.
  for (const control of ['#atlas-close', '#atlas-creature']) {
    expect(await page.locator(control).evaluate(one => { const box = one.getBoundingClientRect(); return document.elementFromPoint(box.left + box.width / 2, box.top + box.height / 2) === one; }), control).toBe(true);
  }
  const heads = page.locator('#atlas-board .atlas-family-head');
  await expect(heads).toHaveCount(3);
  const scenes = await heads.evaluateAll(all => all.map(head => head.querySelector('img[data-scene]')?.getAttribute('src')?.match(/page-art\/([a-z-]+?)-\d+\./)?.[1]));
  expect(scenes).toEqual(['ironhide-scar', 'blightweaver-pressure-sac', 'warped-listening']);
  for (const head of await heads.all()) {
    await expect(head).toHaveClass(/scene-head/);
    expect(Math.round((await head.boundingBox()).height)).toBe(48);
    await expect(head.locator('.atlas-family-words')).toHaveClass(/scene-words/);
    // Drawn at the column's width, not the showcase's: the phone takes its twice-dense file.
    await expect(head.locator('img[data-scene]')).toHaveAttribute('sizes', '(max-width: 819px) min(94vw, 400px), 400px');
    const width = Math.round((await head.boundingBox()).width);
    expect(Math.abs(width - Math.min(Math.round(page.viewportSize().width * 0.94), 400))).toBeLessThanOrEqual(12);
  }
  await expect.poll(() => asked).toContain('brute-moult');
  await page.screenshot({ path: info.outputPath('mutation-tree-heads.png') });
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});

test('on a phone on its side the creature list stands beside the tree\'s band, not under it', async ({ page }) => {
  test.skip(page.viewportSize().width < 820, 'a wide, short screen');
  await page.setViewportSize({ width: 844, height: 390 });
  const h = await mount(page); await page.locator('#mutation-atlas-open').click(); await expect(page.locator('#atlas-dialog')).toBeVisible();
  const [band, list, close] = await Promise.all(['#atlas-dialog .atlas-band', '#atlas-creature', '#atlas-close'].map(one => page.locator(one).boundingBox()));
  expect(list.x).toBeGreaterThanOrEqual(band.x + band.width - 1);
  expect(list.y).toBeLessThan(band.y + band.height);
  expect(close.x + close.width).toBeLessThanOrEqual(band.x + band.width + 1);
  await fitsViewport(page, '#atlas-close, #atlas-creature');
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});

// A mutation followed back onto the field (UX12b, docs/art/journeys/windows.md, `mutation-followed`).
test('the team\'s last pick, once the host accepts it, keeps its creature in its new form, tagged PICKED, and the focus follows it', async ({ page }) => {
  const h = await mount(page, { remaining: 1 });
  h.state.next = view => ({ ...view, waitingFor: null, waitingAsked: null, options: {}, board: { ...view.board, evolutionChoices: [{ creature: 2, tier: 'root1' }] } });
  await page.locator('#actor-layer [data-creature="2"]').click();
  await page.locator('[data-mutation="root1"]').click(); await page.locator('#planning-confirm').click();
  await expect.poll(() => h.state.writes).toEqual([{ kind: 'Evolution', creature: 2, tier: 'root1', asked: 11 }]);
  await expect(page.locator('#planning-dialog')).toBeHidden();
  const two = page.locator('#actor-layer [data-creature="2"]');
  await expect(two).toBeFocused();
  // Read from the pick the host holds, not from the draft: the draft is gone, the form stays until the reveal.
  await expect(two).toHaveAttribute('data-picked', 'root1'); await expect(two).toHaveAttribute('data-form', 'base-creature');
  await page.clock.runFor(200); await expect(two).toHaveAttribute('data-artwork-shown', 'predator-base');
  await expect(two.locator('.creature-tag')).toHaveText('PICKED');
  await expect(two).toHaveAccessibleName(/picked Predator, hidden until reveal/);
  await expect(page.locator('#actor-layer [data-creature="1"] .creature-tag')).not.toHaveText('PICKED');
  // A repeated snapshot keeps the mark and never moves the focus again.
  await page.locator('#settings').focus(); await h.publish(); await page.clock.runFor(50);
  await expect(page.locator('#settings')).toBeFocused(); await expect(two).toHaveAttribute('data-picked', 'root1');
  // The reveal applies the pick: PICKED goes, the form stays.
  h.state.view = { ...h.state.view, board: { ...h.state.view.board, subPhase: 'Speed', evolutionChoices: [],
    allies: h.state.view.board.allies.map(one => one.id === 2 ? { ...one, acquiredTiers: ['root1'], knownSpells: ['spell1'] } : one) } };
  await h.publish(); await page.clock.runFor(50);
  await expect(two).not.toHaveAttribute('data-picked'); await expect(two).toHaveAttribute('data-form', 'predator');
  await expect(two).toHaveAttribute('data-artwork-shown', 'predator-base');
  await expect(two.locator('.creature-tag')).not.toHaveText('PICKED'); await expect(page.locator('#settings')).toBeFocused();
  expect(h.state.errors).toEqual([]);
});

test('passing the team\'s picks sends the focus to its first living creature', async ({ page }) => {
  const h = await mount(page, { allies: [{ ...roster[0], isAlive: false }, roster[1], roster[2]] });
  await page.locator('#planning-pass').click(); await page.locator('#planning-confirm').click();
  await expect.poll(() => h.state.writes.length).toBe(1);
  await expect(page.locator('#planning-dialog')).toBeHidden();
  await expect(page.locator('#actor-layer [data-creature="2"]')).toBeFocused();
  await expect(page.locator('#actor-layer [data-picked]')).toHaveCount(0); expect(h.state.errors).toEqual([]);
});

test('a pick closed without a confirm drops its draft and gives the focus back to what opened the sheet', async ({ page }) => {
  const h = await mount(page);
  // The phase opened the sheet by itself: the command that opens it again takes the focus.
  await page.locator('#planning-close').click(); await expect(page.locator('#planning-dialog')).toBeHidden();
  await expect(page.locator('#planning-open')).toBeFocused();
  // Opened from a creature on the field, a draft made, then Escape: the creature has the focus, and its old form.
  const two = page.locator('#actor-layer [data-creature="2"]');
  await two.click(); await page.locator('[data-mutation="root1"]').click();
  await expect(two).toHaveAttribute('data-preview-form', 'predator');
  await page.keyboard.press('Escape'); await expect(page.locator('#planning-dialog')).toBeHidden();
  await expect(two).toBeFocused(); await expect(two).not.toHaveAttribute('data-preview-form');
  await expect(two).toHaveAttribute('data-form', 'base-creature');
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});

test('a reveal marks the new spell in the inspection, and a repeated snapshot never shows the reveal again', async ({ page }) => {
  const h = await mount(page);
  await page.locator('#planning-close').click();
  h.state.view = { ...h.state.view, waitingFor: null, waitingAsked: null, options: {}, feedNext: 6,
    feed: [{ sequence: 5, round: 1, event: { kind: 'PurchasesRevealed', roundId: 1, choices: [{ creature: 1, tier: 'root0' }] } }],
    board: { ...h.state.view.board, subPhase: 'Speed', allies: roster.map(one => one.id === 1 ? { ...one, acquiredTiers: ['root0'], knownSpells: ['spell0'] } : one) } };
  await h.publish(); await page.clock.runFor(50);
  await expect(page.locator('#growth-receipt')).toBeVisible();
  await page.locator('#actor-layer [data-creature="1"]').click(); await expect(page.locator('#inspect-dialog')).toBeVisible();
  const spell = page.locator('#inspect-spells [data-inspect-spell="spell0"]');
  await expect(spell).toHaveAttribute('data-fresh', 'true');
  await expect(spell.locator('.inspect-spell-source')).toHaveText('New · Learned from Brute');
  await page.keyboard.press('Escape');
  // Read once: Continue sets it aside, and the same snapshot again brings neither the receipt nor a second reveal.
  await page.locator('#growth-dismiss').click(); await expect(page.locator('#growth-receipt')).toBeHidden();
  await h.publish(); await page.clock.runFor(50);
  await expect(page.locator('#growth-receipt')).toBeHidden();
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
