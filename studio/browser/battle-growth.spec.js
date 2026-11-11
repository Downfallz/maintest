import { test, expect } from '@playwright/test';
import { openSpells } from './battle-spells-fixture.js';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { fitsViewport } from './fit.js';
import { decided } from './decision.js';
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
    if (url.pathname.endsWith('/decision')) { h.writes.push(decided(route.request())); return route.fulfill({ status: 204 }); }
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
test('Help reference opens the full mutation tree: read-only, keyboard navigable and without private pending picks', async ({ page }, info) => {
  const h = await mount(page); await h.reference();
  const board = page.locator('#atlas-board'), detail = page.locator('#atlas-detail-body'), progress = page.locator('#atlas-progress');
  await expect(page.locator('#atlas-dialog')).toBeVisible(); await expect(progress).toContainText('Next mutation opportunity · Round 7');
  await expect(board.locator('[data-package="predator"]')).toHaveAttribute('data-status', 'available');
  await page.locator('#atlas-creature').selectOption('2');
  await expect(progress).toContainText('No revealed mutations yet');
  await expect(board.locator('[data-package="warped"]')).toHaveAttribute('data-status', 'future');
  await expect(board).not.toContainText('Picked'); await expect(board.locator('[data-status="available"]')).toHaveCount(0);
  const cap = board.locator('[data-package="cap"]');
  await cap.focus(); await page.keyboard.press('Enter'); await expect(cap).toHaveAttribute('aria-pressed', 'true');
  await expect(detail).toContainText('Requires one of: Stalker / Blighted');
  await expect(detail).toContainText('For every creature of the side');
  await fitsViewport(page, '#atlas-close, #atlas-creature');
  // A poll keeps the creature, the open card and the focused mutation.
  h.view.board.nextEvolutionRound = 8; await h.publish();
  await expect(progress).toContainText('Next mutation opportunity · Round 8'); await expect(page.locator('#atlas-creature')).toHaveValue('2');
  await expect(cap).toHaveAttribute('aria-pressed', 'true'); await expect(cap).toBeFocused(); await expect(detail).toContainText('Shared capstone');
  expect(h.shown).toEqual(['10']); expect(h.writes).toEqual([]);
  await page.screenshot({ path: info.outputPath('mutation-reference.png') });
  if (info.project.name !== 'desktop') { await page.keyboard.press('Escape'); await expect(page.locator('#atlas-detail')).toBeHidden(); }
  await page.keyboard.press('Escape'); await expect(page.locator('#atlas-dialog')).toBeHidden();
  await expect(page.locator('#help-dialog [data-growth-open]')).toBeFocused();
  expect(h.errors).toEqual([]);
});
test('reference over a mutation draft preserves its selection and does not acknowledge again', async ({ page }) => {
  const h = await mount(page); await expect(page.locator('#planning-dialog')).toBeVisible(); await page.locator('[data-mutation="predator"]').click();
  await page.locator('#help-open').click(); await page.locator('#help-dialog [data-growth-open]').click();
  await h.publish(); await expect(page.locator('#planning-confirm')).toBeDisabled();
  expect(h.shown).toEqual(['10']); expect(h.writes).toEqual([]);
  await page.keyboard.press('Escape'); await page.keyboard.press('Escape');
  await expect(page.locator('[data-mutation="predator"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('#planning-confirm')).toBeEnabled(); expect(h.errors).toEqual([]);
});
test('inspection selects the opponent and reveals its progress only after public purchase', async ({ page }) => {
  const h = await mount(page); await page.locator('.creature.enemy').click(); await page.locator('#inspect-growth').click();
  const brute = page.locator('#atlas-board [data-package="brute"]');
  await expect(page.locator('#atlas-creature')).toHaveValue('3'); await expect(page.locator('#atlas-progress')).toContainText('No revealed mutations yet');
  await expect(brute).toHaveAttribute('data-status', 'future');
  await expect(page.locator('#atlas-board [data-status="available"]')).toHaveCount(0);
  unlock(h); await h.publish(); await expect(page.locator('#atlas-progress')).toContainText('Brute · Stage 1');
  await expect(brute).toHaveAttribute('data-status', 'known');
  await expect(page.locator('#growth-receipt')).toBeHidden(); expect(h.shown).toEqual(['10']); expect(h.writes).toEqual([]);
  await page.keyboard.press('Escape'); await page.keyboard.press('Escape'); await expect(page.locator('#growth-receipt')).toBeVisible();
  await page.locator('#growth-receipt [data-growth-open]').click();
  await expect(page.locator('#growth-reveals .reveal-group').first()).toContainText('First');
  await expect(page.locator('#growth-reveals .reveal-group').first()).toContainText('+ Devour');
  await expect(page.locator('#growth-reveals .reveal-group').last()).toContainText('Rival'); expect(h.errors).toEqual([]);
});
test('a reveal reads as one line and crests on the field, opens on cards by side, and each card leads to its path', async ({ page }, info) => {
  const h = await mount(page); await page.locator('#planning-close').click(); unlock(h); await h.publish();
  await expect(page.locator('#growth-receipt')).toBeVisible(); await expect(page.locator('#growth-receipt-summary')).toHaveText('You 1 · Opponent 1 · Round 3');
  await expect(page.locator('#growth-announcement')).toHaveText(/First, your team: Predator, stage 1, new Devour\. Rival, opponent: Brute, stage 1\./);
  await expect(page.locator('#battle')).toHaveAttribute('data-reveal', '1 3');
  await fitsViewport(page, '#growth-receipt [data-growth-open], #growth-dismiss');
  await page.locator('#growth-receipt [data-growth-open]').click();
  await expect(page.locator('#growth-title')).toHaveText('Mutations revealed');
  const [own, rival] = [page.locator('.reveal-group').first(), page.locator('.reveal-group').last()];
  await expect(own.locator('h3')).toHaveText('Your team'); await expect(rival.locator('h3')).toHaveText('Opponent');
  await expect(own.locator('.reveal-mutation')).toHaveText('Predator'); await expect(own.locator('.reveal-stage')).toHaveText('Stage 1');
  await expect(own.locator('.reveal-spells')).toContainText('+ Devour'); await expect(own.locator('.reveal-spells')).toContainText('2 energy · One enemy');
  await expect(own.locator('.reveal-meta')).toHaveText('+1 initiative');
  await expect(rival.locator('.reveal-passive')).toHaveText('No new spell this time.');
  await page.screenshot({ path: info.outputPath('mutation-reveal.jpg'), type: 'jpeg', quality: 85 });
  // Each card's path opens the full mutation tree on that creature, the mutation in hand and its path lit.
  await rival.getByRole('button', { name: "See Rival's mutation path" }).click();
  await expect(page.locator('#growth-dialog')).toBeHidden(); await expect(page.locator('#atlas-dialog')).toBeVisible();
  await expect(page.locator('#atlas-creature')).toHaveValue('3');
  const tree = page.locator('#atlas-board');
  await expect(tree.locator('[data-package="brute"]')).toHaveAttribute('data-path', 'true');
  await expect(tree.locator('[data-package="brute"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(tree.locator('[data-package="predator"]')).not.toHaveAttribute('data-path', 'true');
  await expect(page.locator('#atlas-detail-body .atlas-card-name')).toHaveText('Brute');
  await expect(page.locator('#atlas-detail-body .spell-card')).toHaveCount(1);
  await page.screenshot({ path: info.outputPath('mutation-path.jpg'), type: 'jpeg', quality: 85 });
  await page.locator('#atlas-close').click(); await page.locator('#growth-receipt [data-growth-open]').click();
  await page.locator('#growth-explore').click(); await expect(page.locator('#atlas-creature')).toHaveValue('1');
  await expect(tree.locator('[data-package="predator"]')).toHaveAttribute('data-path', 'true');
  await page.keyboard.press('Escape'); if (await page.locator('#atlas-dialog').isVisible()) await page.keyboard.press('Escape');
  await page.locator('#growth-dismiss').click();
  await expect(page.locator('#battle')).toHaveAttribute('data-reveal', ''); expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
test('crests stay beside the docked spell choice and leave the field to its targets or to history', async ({ page }) => {
  const h = await mount(page); await page.locator('#planning-close').click(); unlock(h); spellChoice(h); await h.publish();
  // The spell choice opens by itself, docked: the field and its crests stay in view (ADR 0149).
  await expect(page.locator('#combat-dialog')).toBeVisible(); await expect(page.locator('#battle')).toHaveAttribute('data-reveal', '1 3');
  await page.locator('[data-spell="devour"]').click(); await expect(page.locator('#battle')).toHaveAttribute('data-reveal', '');
  await page.locator('#combat-cancel').click(); await expect(page.locator('#battle')).toHaveAttribute('data-reveal', '1 3');
  await page.locator('#history-open').click(); await expect(page.locator('#battle')).toHaveAttribute('data-reveal', '');
  expect(h.errors).toEqual([]);
});
test('public unlock receipt and new spell badge appear once; reload is a silent baseline', async ({ page }) => {
  const h = await mount(page); unlock(h); spellChoice(h); await h.publish();
  await expect(page.locator('#growth-receipt')).toBeVisible(); await page.locator('#growth-dismiss').click();
  await h.publish(); await expect(page.locator('#growth-receipt')).toBeHidden();
  await openSpells(page); await expect(page.locator('[data-spell="devour"]')).toContainText('New · Available');
  await expect(page.locator('[data-spell="devour"]')).toHaveClass(/spell-new/); await expect(page.locator('[data-spell="focus"]')).not.toHaveClass(/spell-new/);
  await expect(page.locator('[data-spell="focus"]')).not.toContainText('New');
  await page.locator('#combat-dialog-close').click(); await openSpells(page);
  await expect(page.locator('[data-spell="devour"]')).toContainText('New · Available');
  spellChoice(h, 22); await h.publish(); await openSpells(page);
  await expect(page.locator('[data-spell="devour"]')).not.toContainText('New');
  await page.reload(); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true'); await expect(page.locator('#loading')).toBeHidden();
  await expect(page.locator('#growth-receipt')).toBeHidden(); await openSpells(page);
  await expect(page.locator('[data-spell="devour"]')).not.toContainText('New'); expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
test('a pending acknowledgement cannot send through the reference and the target draft survives', async ({ page }) => {
  // The choice opens, and is acknowledged, as soon as the question arrives (ADR 0149): the acknowledgement is held from then.
  const input = initial(); const h = await mount(page, input); unlock(h); spellChoice(h);
  let release; h.ack = new Promise(resolve => { release = resolve; }); await h.publish();
  await openSpells(page); await page.locator('[data-spell="devour"]').click();
  await page.locator('.is-targetable').click(); await page.locator('#combat-confirm').click();
  await h.reference(); release(); await page.clock.runFor(50);
  expect(h.writes).toEqual([]); await page.keyboard.press('Escape'); await page.keyboard.press('Escape');
  await expect(page.locator('#combat-selection')).toContainText('1 selected'); await expect(page.locator('#combat-confirm')).toBeEnabled();
  expect(h.errors).toEqual([]);
});
test('seat access loss clears open reference, pending receipts and private command context', async ({ page }) => {
  const h = await mount(page); unlock(h); await h.publish(); await h.reference();
  await expect(page.locator('#atlas-dialog')).toBeVisible();
  h.status = 403; await h.publish(); await expect(page.locator('#atlas-dialog')).not.toBeVisible();
  await expect(page.locator('#atlas-board')).toBeEmpty(); await expect(page.locator('#atlas-detail-body')).toBeEmpty(); await expect(page.locator('#growth-reveals')).toBeEmpty();
  await expect(page.locator('#growth-receipt')).toBeHidden(); expect(h.errors).toEqual([]);
});
test('multiclass reference keeps close and creature selection reachable in short screens and large text', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'Viewport sweep runs once.');
  const input = initial(); input.board.allies[0].acquiredTiers = ['predator', 'brute', 'upgrade', 'cap'];
  const h = await mount(page, input); await h.reference(); await expect(page.locator('#atlas-progress')).toContainText('Multiclass · Predator · Stage 4 / Brute · Stage 1');
  for (const size of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(size); await page.clock.runFor(150); await fitsViewport(page, '#atlas-close, #atlas-creature');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
  await page.setViewportSize({ width: 390, height: 844 }); await page.addStyleTag({ content: '#atlas-dialog { font-size: 28px; }' });
  const last = page.locator('#atlas-board .atlas-node').last();
  await last.scrollIntoViewIfNeeded(); await expect(last).toBeInViewport();
  await fitsViewport(page, '#atlas-close, #atlas-creature'); expect(h.errors).toEqual([]);
});
test('a future unlock waits behind a held closing cast and keeps its badge for the live spell choice', async ({ page }) => {
  const h = await mount(page); await page.locator('#planning-close').click(); await page.locator('#settings').click(); await page.selectOption('#playback-preference', 'step'); await page.getByRole('button', { name: 'Close settings' }).click();
  const before = structuredClone([...h.view.board.allies, ...h.view.board.enemies]);
  unlock(h); h.view.feed[0].sequence = 21; h.view.feedNext = 22;
  h.view.feed.unshift({ sequence: 20, event: { kind: 'CombatActionResolved', roundId: 2,
    // The opponent's closing cast: it holds for its OK, where the player's own would move on (ADR 0141).
    resolution: { action: { actor: 4, spell: 'focus', targets: [4] } }, appliedOutcomes: [],
    frame: { before, after: before, timeline: [], rollOffs: [] } } });
  spellChoice(h, 22); await h.publish();
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '20');
  await page.clock.runFor(3400); await expect(page.locator('#growth-receipt')).toBeHidden();
  await expect(page.locator('#round-number')).toHaveText('02');
  await page.locator('[data-creature="1"]').click();
  await expect(page.locator('#inspect-spells')).not.toContainText('Devour');
  await page.getByRole('button', { name: 'Close inspection' }).click();
  expect(h.shown).toEqual(['10']); expect(h.writes).toEqual([]);
  await page.locator('#playback-next').click();
  await expect(page.locator('#growth-receipt')).toBeVisible();
  await expect(page.locator('#growth-announcement')).toContainText('Devour');
  await openSpells(page);
  await expect(page.locator('[data-spell="devour"]')).toHaveClass(/spell-new/);
  expect(h.shown).toEqual(['10', '22']); expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
test('opening the team tempos by hand reads the reveal: its receipt and crests leave the field', async ({ page }) => {
  const h = await mount(page); await page.locator('#planning-close').click();
  unlock(h, { waitingFor: 'Speed', waitingAsked: 21, waitingCreature: 1, options: { speed: { missing: [1, 2] } } }); await h.publish();
  await expect(page.locator('#growth-receipt')).toBeVisible(); await expect(page.locator('#battle')).not.toHaveAttribute('data-reveal', '');
  await page.locator('#planning-open').click();
  await expect(page.locator('#planning-dialog')).toHaveAttribute('data-kind', 'Speed'); await expect(page.locator('#planning-dialog')).toBeVisible();
  await expect(page.locator('#growth-receipt')).toBeHidden(); await expect(page.locator('#battle')).toHaveAttribute('data-reveal', '');
  // Read for good: closing the tempos does not bring it back.
  await page.locator('#planning-close').click(); await page.clock.runFor(500); await expect(page.locator('#growth-receipt')).toBeHidden();
  expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
for (const read of ['continue', 'see']) test(`the team's tempos wait for a fresh mutation reveal to be read, here with ${read === 'see' ? 'See' : 'Continue'}`, async ({ page }) => {
  const h = await mount(page); await page.locator('#planning-close').click();
  unlock(h, { waitingFor: 'Speed', waitingAsked: 21, waitingCreature: 1, options: { speed: { missing: [1, 2] } } }); await h.publish();
  await expect(page.locator('#growth-receipt')).toBeVisible(); await page.clock.runFor(1500);
  await expect(page.locator('#planning-dialog')).toBeHidden();
  if (read === 'see') { await page.locator('#growth-receipt [data-growth-open]').click(); await page.locator('#growth-close').click(); }
  else await page.locator('#growth-dismiss').click();
  await expect(page.locator('#planning-dialog')).toHaveAttribute('data-kind', 'Speed'); await expect(page.locator('#planning-dialog')).toBeVisible();
  expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
