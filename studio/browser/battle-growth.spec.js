import { test, expect } from '@playwright/test';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningBattlePlayer);
const creature = (id, name) => ({ id, name, isAlive: true, health: 20, maxHealth: 20, energy: 6,
  totalDefense: 0, currentInitiative: 0, conditions: [], acquiredTiers: [], knownSpells: ['focus'] });
const catalogue = { cards: [{ id: 'focus', name: 'Focus', cost: 0, targeting: 'Self', effects: ['Gain energy'] },
  { id: 'devour', name: 'Devour', cost: 2, targeting: 'One enemy', effects: ['Deal damage'] }], packages: [
  { id: 'predator', name: 'Predator', level: 1, initiativeBonus: 1, spells: ['devour'] },
  { id: 'brute', name: 'Brute', level: 1, initiativeBonus: 0, spells: ['focus'] },
  { id: 'warped', name: 'Warped', level: 1, initiativeBonus: 2, spells: [] },
  { id: 'upgrade', name: 'Stalker', level: 2, prerequisites: ['predator'], spells: [], initiativeBonus: 1 },
  { id: 'alternative', name: 'Blighted', level: 2, prerequisites: ['predator'], spells: [], initiativeBonus: 2 },
  { id: 'cap', name: 'Shared capstone', level: 4, anyOf: ['upgrade', 'alternative'], spells: [], initiativeBonus: 0, passiveLines: ['Defense +2', 'For every creature of the side'] },
] };
const initial = () => ({ board: { slot: 'Player1', roundNumber: 3, nextEvolutionRound: 7, phase: 'Planning', subPhase: 'Evolution',
  allies: [creature(1, 'First'), creature(2, 'Second')], enemies: [creature(3, 'Rival')], evolutionChoices: [{ creature: 2, tier: 'warped' }], timeline: [] },
  waitingFor: 'Evolution', waitingAsked: 10, options: { evolution: { remainingPicks: 1, creatures: [{ creature: 1, availableTiers: ['predator', 'brute'] }] } }, feed: [], feedNext: 20 });
async function mount(page, input = initial()) {
  const h = { view: input, writes: [], shown: [], errors: [], status: 200 };
  page.on('pageerror', error => h.errors.push(error.message));
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
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'growth', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: catalogue });
    if (url.pathname === '/api/seat/player1') {
      if (url.searchParams.has('shown')) { h.shown.push(url.searchParams.get('shown')); if (h.ack) await h.ack; }
      return route.fulfill({ status: h.status, json: h.view });
    }
    if (url.pathname.endsWith('/decision')) { h.writes.push(route.request().postDataJSON()); return route.fulfill({ status: 204 }); }
    return route.fulfill({ status: 500 });
  });
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1000));
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#loading')).toBeHidden();
  // Freeze relative to the browser clock, not the wall time spent compiling/loading the page.
  h.publish = async () => { await page.evaluate(() => window.notifyBattle()); await page.clock.runFor(50); };
  h.reference = async () => { await page.locator('#help-open').click(); await page.locator('#help-dialog [data-growth-open]').click(); };
  return h;
}
function unlock(h, options = {}) {
  const own = h.view.board.allies[0], enemy = h.view.board.enemies[0];
  own.acquiredTiers = ['predator']; own.knownSpells = ['focus', 'devour'];
  enemy.acquiredTiers = ['brute'];
  h.view.board.subPhase = 'Speed';
  h.view.feed = [{ sequence: 20, round: 3, event: { kind: 'PurchasesRevealed', roundId: 3, choices: [{ creature: 1, tier: 'predator' }, { creature: 3, tier: 'brute' }] } }];
  h.view.feedNext = 21; Object.assign(h.view, { waitingFor: null, waitingAsked: null, options: {}, ...options });
}
function spellChoice(h, asked = 21) {
  h.view.board.phase = 'Combat'; h.view.board.subPhase = 'Combat';
  Object.assign(h.view, { waitingFor: 'Action', waitingAsked: asked, waitingCreature: 1,
    options: { action: { spells: ['focus', 'devour'].map(spell => ({ spell, legalTargets: { candidates: [3], minTargets: 1, maxTargets: 1 } })) } } });
}
test('Help reference is read-only, keyboard navigable and excludes private pending picks', async ({ page }, info) => {
  const h = await mount(page); await h.reference();
  await expect(page.locator('#growth-dialog')).toBeVisible(); await expect(page.locator('#growth-next')).toHaveText('Next mutation opportunity · Round 7');
  await expect(page.locator('#growth-tree [data-package="predator"] > summary')).toContainText('Available');
  await page.locator('#growth-creature').selectOption('2');
  await expect(page.locator('#growth-progress')).toHaveText('No revealed mutations yet');
  await expect(page.locator('#growth-tree')).not.toContainText('Picked');
  const root = page.locator('#growth-tree [data-package="predator"] > summary');
  const branch = page.locator('#growth-tree [data-package="predator"]');
  await root.focus(); await page.keyboard.press('Enter'); await expect(branch).not.toHaveAttribute('open');
  await page.keyboard.press('Enter'); await expect(branch).toHaveAttribute('open');
  await page.locator('#growth-tree [data-package="upgrade"] > summary').click();
  await page.locator('#growth-tree [data-package="cap"] > summary').first().click();
  await expect(page.locator('#growth-tree')).toContainText('Requires one of: Stalker / Blighted');
  await expect(page.locator('#growth-tree')).toContainText('For every creature of the side');
  await fitsViewport(page, '#growth-close, #growth-creature');
  await h.publish(); await expect(page.locator('#growth-tree [data-package="cap"]').first()).toHaveAttribute('open');
  const cap = page.locator('#growth-tree [data-package="cap"]').first();
  await cap.locator(':scope > summary').focus(); h.view.board.nextEvolutionRound = 8; await h.publish();
  await expect(page.locator('#growth-next')).toHaveText('Next mutation opportunity · Round 8');
  await expect(cap).toHaveAttribute('open'); await expect(cap.locator(':scope > summary')).toBeFocused();
  await expect(page.locator('#growth-tree [data-package="cap"]').last()).not.toHaveAttribute('open');
  expect(h.shown).toEqual([]); expect(h.writes).toEqual([]);
  await page.screenshot({ path: info.outputPath('mutation-reference.png') });
  await page.keyboard.press('Escape'); await expect(page.locator('#help-dialog [data-growth-open]')).toBeFocused();
  expect(h.errors).toEqual([]);
});
test('reference over a mutation draft preserves its selection and does not acknowledge again', async ({ page }) => {
  const h = await mount(page); await page.locator('#planning-open').click(); await page.locator('[data-mutation="predator"]').click();
  await page.locator('#planning-dialog [data-help-open]').click(); await page.locator('#help-dialog [data-growth-open]').click();
  await h.publish(); await expect(page.locator('#planning-confirm')).toBeDisabled();
  expect(h.shown).toEqual(['10']); expect(h.writes).toEqual([]);
  await page.keyboard.press('Escape'); await page.keyboard.press('Escape');
  await expect(page.locator('[data-mutation="predator"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('#planning-confirm')).toBeEnabled(); expect(h.errors).toEqual([]);
});
test('inspection selects the opponent and reveals its progress only after public purchase', async ({ page }) => {
  const h = await mount(page); await page.locator('.creature.enemy').click(); await page.locator('#inspect-growth').click();
  await expect(page.locator('#growth-creature')).toHaveValue('3'); await expect(page.locator('#growth-progress')).toHaveText('No revealed mutations yet');
  await expect(page.locator('#growth-tree [data-package="brute"] > summary')).toContainText('Not acquired');
  await expect(page.locator('#growth-tree')).not.toContainText('Not offered this pick');
  unlock(h); await h.publish(); await expect(page.locator('#growth-progress')).toHaveText('Brute · Stage 1');
  await expect(page.locator('#growth-tree [data-package="brute"] > summary')).toContainText('Acquired');
  await expect(page.locator('#growth-receipt')).toBeHidden(); expect(h.shown).toEqual([]); expect(h.writes).toEqual([]);
  await page.keyboard.press('Escape'); await page.keyboard.press('Escape'); await expect(page.locator('#growth-receipt')).toBeVisible();
  await page.locator('#growth-receipt [data-growth-open]').click(); await page.locator('#growth-reveal-details summary').click();
  await expect(page.locator('#growth-reveals')).toContainText('First (Your team) · Predator · New: Devour');
  await expect(page.locator('#growth-reveals')).toContainText('Rival (Opponent) · Brute'); expect(h.errors).toEqual([]);
});
test('public unlock receipt and new spell badge appear once; reload is a silent baseline', async ({ page }) => {
  const h = await mount(page); unlock(h); spellChoice(h); await h.publish();
  await expect(page.locator('#growth-receipt')).toBeVisible(); await page.locator('#growth-dismiss').click();
  await h.publish(); await expect(page.locator('#growth-receipt')).toBeHidden();
  await page.locator('#combat-open').click(); await expect(page.locator('[data-spell="devour"]')).toContainText('New · Available');
  await expect(page.locator('[data-spell="devour"]')).toHaveClass(/spell-new/); await expect(page.locator('[data-spell="focus"]')).not.toHaveClass(/spell-new/);
  await expect(page.locator('[data-spell="focus"]')).not.toContainText('New');
  await page.locator('#combat-dialog-close').click(); await page.locator('#combat-open').click();
  await expect(page.locator('[data-spell="devour"]')).toContainText('New · Available');
  spellChoice(h, 22); await h.publish(); await page.locator('#combat-open').click();
  await expect(page.locator('[data-spell="devour"]')).not.toContainText('New');
  await page.reload(); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true'); await expect(page.locator('#loading')).toBeHidden();
  await expect(page.locator('#growth-receipt')).toBeHidden(); await page.locator('#combat-open').click();
  await expect(page.locator('[data-spell="devour"]')).not.toContainText('New'); expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
test('a pending acknowledgement cannot send through the reference and the target draft survives', async ({ page }) => {
  const input = initial(); const h = await mount(page, input); unlock(h); spellChoice(h); await h.publish();
  let release; h.ack = new Promise(resolve => { release = resolve; });
  await page.locator('#combat-open').click(); await page.locator('[data-spell="devour"]').click();
  await page.locator('.is-targetable').click(); await page.locator('#combat-confirm').click();
  await h.reference(); release(); await page.clock.runFor(50);
  expect(h.writes).toEqual([]); await page.keyboard.press('Escape'); await page.keyboard.press('Escape');
  await expect(page.locator('#combat-selection')).toContainText('1 selected'); await expect(page.locator('#combat-confirm')).toBeEnabled();
  expect(h.errors).toEqual([]);
});
test('seat access loss clears open reference, pending receipts and private command context', async ({ page }) => {
  const h = await mount(page); unlock(h); await h.publish(); await h.reference();
  h.status = 403; await h.publish(); await expect(page.locator('#growth-dialog')).not.toBeVisible();
  await expect(page.locator('#growth-tree')).toBeEmpty(); await expect(page.locator('#growth-reveals')).toBeEmpty();
  await expect(page.locator('#growth-receipt')).toBeHidden(); expect(h.errors).toEqual([]);
});
test('multiclass reference keeps close and creature selection reachable in short screens and large text', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'Viewport sweep runs once.');
  const input = initial(); input.board.allies[0].acquiredTiers = ['predator', 'brute', 'upgrade', 'cap'];
  const h = await mount(page, input); await h.reference(); await expect(page.locator('#growth-progress')).toHaveText('Multiclass · Predator · Stage 4 / Brute · Stage 1');
  for (const size of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(size); await page.clock.runFor(150); await fitsViewport(page, '#growth-close, #growth-creature');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
  await page.setViewportSize({ width: 390, height: 844 }); await page.addStyleTag({ content: '#growth-body { font-size: 28px; }' });
  await page.locator('#growth-tree > details').last().locator(':scope > summary').scrollIntoViewIfNeeded();
  await expect(page.locator('#growth-tree > details').last().locator(':scope > summary')).toBeInViewport();
  await fitsViewport(page, '#growth-close, #growth-creature'); expect(h.errors).toEqual([]);
});
test('a future unlock waits behind a held closing cast and keeps its badge for the live spell choice', async ({ page }) => {
  const h = await mount(page); await page.selectOption('#playback-mode', 'step');
  const before = structuredClone([...h.view.board.allies, ...h.view.board.enemies]);
  unlock(h); h.view.feed[0].sequence = 21; h.view.feedNext = 22;
  h.view.feed.unshift({ sequence: 20, event: { kind: 'CombatActionResolved', roundId: 2,
    resolution: { action: { actor: 1, spell: 'focus', targets: [1] } }, appliedOutcomes: [],
    frame: { before, after: before, timeline: [], rollOffs: [] } } });
  spellChoice(h, 22); await h.publish();
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '20');
  await page.clock.runFor(3400); await expect(page.locator('#growth-receipt')).toBeHidden();
  await expect(page.locator('#round-number')).toHaveText('02');
  await page.locator('[data-creature="1"]').click();
  await expect(page.locator('#inspect-spells')).not.toContainText('Devour');
  await page.getByRole('button', { name: 'Close inspection' }).click();
  expect(h.shown).toEqual([]); expect(h.writes).toEqual([]);
  await page.locator('#playback-skip').click();
  await expect(page.locator('#growth-receipt')).toBeVisible();
  await expect(page.locator('#growth-announcement')).toContainText('Devour');
  await page.locator('#combat-open').click();
  await expect(page.locator('[data-spell="devour"]')).toHaveClass(/spell-new/);
  expect(h.shown).toEqual(['22']); expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
