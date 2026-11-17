import { test, expect } from '@playwright/test';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningBattlePlayer);

// The recap, Where you are (docs/tabletop/async-recap-plan.md, R3): it opens on its own when the seat comes back after
// an hour away with something it was not shown, from the header at any time, and closes on Go to my turn or Replay
// step by step. The host here is a mock: the seat's view, its last visit and a feed of round 2.
const creature = (id, health = 20) => ({ id, name: `Creature ${id}`, health, maxHealth: 20, energy: 5, totalDefense: 2,
  currentInitiative: 7, isAlive: health > 0, isStunned: false, isStunImmune: false, knownSpells: ['strike'], conditions: [] });
const team = (hurt = {}) => [1, 2, 3, 4, 5, 6].map(id => creature(id, hurt[id] ?? 20));
const timeline = team().map(one => ({ creature: one.id, owner: one.id < 4 ? 'Player1' : 'Player2', speed: 'Standard' }));
const legalTargets = { candidates: [4, 5, 6], minTargets: 1, maxTargets: 1 };
// A cast of round 2 with its recorded frame: `actor` strikes `target` for 6.
function cast(sequence, actor, target, before, after) {
  return { sequence, round: 2, event: { kind: 'CombatActionResolved', roundId: 2,
    resolution: { action: { actor, spell: 'strike', targets: [target] }, isCritical: false, fizzled: false },
    appliedOutcomes: [{ kind: 'DamageOutcome', target, amount: 6, critical: false }],
    frame: { before, after, timeline, rollOffs: [] } } };
}
// Round 2 so far: this seat struck first (1), then the opponent twice (3 and 4) while it was away.
const feed = [
  cast(1, 1, 4, team(), team({ 4: 14 })),
  cast(3, 4, 1, team({ 4: 14 }), team({ 4: 14, 1: 14 })),
  cast(4, 5, 2, team({ 4: 14, 1: 14 }), team({ 4: 14, 1: 14, 2: 14 })),
];
const hoursAgo = hours => new Date(Date.now() - hours * 3600000).toISOString();

async function mount(page, { seen = 1, away = 3, entries = feed, extra = {} } = {}) {
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' }));
    const original = window.fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      const alive = setInterval(() => { try { controller.enqueue(new TextEncoder().encode(': keep-alive\n\n')); } catch { clearInterval(alive); } }, 20000);
      window.notifyBattle = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  });
  const board = team({ 4: 14, 1: 14, 2: 14 });
  const state = { seenWrites: [], view: { seat: 'player1', over: false, recording: true,
    board: { slot: 'Player1', roundNumber: 2, phase: 'Combat', subPhase: 'Activation', activationCursor: 3,
      allies: board.slice(0, 3), enemies: board.slice(3), timeline },
    waitingFor: 'Action', waitingAsked: 5, waitingCreature: 3, answerBy: hoursAgo(-20), lastMoveAt: hoursAgo(away - 0.5),
    options: { action: { creature: 3, spells: [{ spell: 'strike', legalTargets }] } },
    seen: seen === null ? null : { sequence: seen, at: hoursAgo(away) }, feed: entries, feedNext: 5, ...extra } };
  await page.route('**/api/**', route => {
    const request = route.request(), url = new URL(request.url());
    if (url.pathname === '/api/me') return route.fulfill({ json: { accounts: 'dev', account: null } });
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'table-a', format: '3v3',
      seats: [{ seat: 'player1', kind: 'person', name: 'Mark' }, { seat: 'player2', kind: 'person', name: 'Bastien' }] }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [{ id: 'strike', name: 'Strike', cost: 2 }], packages: [], effects: [] } });
    if (url.pathname === '/api/seat/player1/seen') { state.seenWrites.push(request.postDataJSON()); return route.fulfill({ json: { seen: { sequence: 4, at: new Date().toISOString() } } }); }
    if (url.pathname === '/api/seat/player1') return route.fulfill({ json: state.view });
    return route.fulfill({ status: 404, json: {} });
  });
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  return { state, errors };
}
const sheet = page => page.locator('#recap-dialog');

test('coming back after hours away, the sheet opens on its own and tells what was not seen', async ({ page }, info) => {
  const h = await mount(page);

  await expect(sheet(page)).toBeVisible();
  await expect(page.locator('#recap-title')).toHaveText('Where you are');
  await expect(page.locator('#recap-context')).toHaveText('Against Bastien · Round 2 · Combat');
  await expect(page.locator('#recap-timing')).toContainText('Last move 2 h ago');
  await expect(page.locator('#recap-unseen')).toHaveText('2 new since your last visit');
  await expect(page.locator('.recap-cast[data-unseen="true"]')).toHaveCount(2);
  await expect(page.locator('.recap-cast:not([data-unseen])')).toHaveCount(1);
  await expect(page.locator('#recap-yours')).toContainText('−6 HP');
  await expect(page.locator('#recap-question')).toHaveText('Choose a spell');
  await expect(page.locator('#recap-replay')).toBeVisible();
  // The question's sheet waits behind the recap, and the catch-up has not played.
  await expect(page.locator('#combat-dialog')).toBeHidden();
  await expect(page.locator('#battle')).not.toHaveAttribute('data-sequence', /^\d+$/);
  await fitsViewport(page, '#recap-go, #recap-replay');
  await page.screenshot({ path: info.outputPath('recap-sheet.png') });

  await page.locator('#recap-go').click();
  await expect(sheet(page)).toBeHidden();
  await expect(page.locator('#battle')).toHaveAttribute('data-reading', 'false');
  await expect(page.locator('#combat-dialog')).toBeVisible();
  expect(h.errors).toEqual([]);
});

test('Replay step by step hands the opponent\'s actions to the playback, each waiting for its OK', async ({ page }) => {
  const h = await mount(page);
  await expect(sheet(page)).toBeVisible();

  await page.locator('#recap-replay').click();

  await expect(sheet(page)).toBeHidden();
  await expect(page.locator('#battle')).toHaveAttribute('data-reading', 'true');
  await expect(page.locator('#reading-kind')).toContainText("Opponent's action");
  expect(h.errors).toEqual([]);
});

test('the header opens the sheet at any time; with nothing unseen it still tells the round, and offers no replay', async ({ page }, info) => {
  const h = await mount(page, { seen: 4 });
  await expect(page.locator('#combat-dialog')).toBeVisible();
  await expect(sheet(page)).toBeHidden();
  await page.locator('#combat-dialog-close').click();

  await page.locator('#recap-open').click();

  await expect(sheet(page)).toBeVisible();
  await expect(page.locator('#recap-unseen')).toHaveText('Nothing new since your last visit');
  await expect(page.locator('.recap-cast')).toHaveCount(3);
  await expect(page.locator('#recap-dialog .recap-new')).toHaveCount(0);
  await expect(page.locator('#recap-replay')).toBeHidden();
  await fitsViewport(page, '#recap-go');
  await page.keyboard.press('Escape');
  await fitsViewport(page, '#recap-open, #history-open, #help-open, #settings');
  await page.locator('#recap-open').click();
  await page.screenshot({ path: info.outputPath('recap-from-header.png') });
  await page.keyboard.press('Escape');
  await expect(sheet(page)).toBeHidden();
  await expect(page.locator('#recap-open')).toBeFocused();
  expect(h.errors).toEqual([]);
});

test('below an hour away the catch-up plays as before, and the sheet never opens on its own over the question', async ({ page }) => {
  const h = await mount(page, { away: 0.5 });

  await expect(page.locator('#battle')).toHaveAttribute('data-reading', 'true');
  await expect(sheet(page)).toBeHidden();
  for (let read = 0; read < 2; read++) {
    await page.locator('#playback-next[aria-disabled="false"]').click();
  }
  await expect(page.locator('#combat-dialog')).toBeVisible();
  await expect(sheet(page)).toBeHidden();
  expect(h.errors).toEqual([]);
});

test('a match that ended while the seat was away opens on its result, and the button leads to it', async ({ page }) => {
  const h = await mount(page, { extra: { over: true, outcome: { winner: 'Player2', reason: 'Conceded' }, waitingFor: null, options: null, answerBy: null } });

  await expect(sheet(page)).toBeVisible();
  await expect(page.locator('#recap-question')).toHaveText('The match is over.');
  await expect(page.locator('#match-dialog')).toBeHidden();
  await page.locator('#recap-go').filter({ hasText: 'See the result' }).click();

  await expect(page.locator('#match-dialog')).toBeVisible();
  expect(h.errors).toEqual([]);
});

test('the journal marks where the seat left off', async ({ page }) => {
  await mount(page);
  await page.locator('#recap-go').click();
  await page.locator('#combat-dialog-close').click();

  await page.locator('#history-open').click();

  const marker = page.locator('#history-list .history-left-here');
  await expect(marker).toHaveText('You left here');
  await expect(marker.locator('xpath=following-sibling::*[1]')).toHaveAttribute('data-sequence', '3');
});
