import { test, expect } from '@playwright/test';
import { returningArenaPlayer } from './arena-help-fixture.js';
import { fitsViewport, fitsBattlefield } from './fit.js';
test.beforeEach(returningArenaPlayer);
test.describe.configure({ mode: 'default' });
const creature = id => ({ id, name: `Creature ${id}`, health: 20, maxHealth: 20, energy: 4, isAlive: true, conditions: [], acquiredTiers: [], knownSpells: ['strike'] });
const roster = [1, 2, 3, 4, 5, 6].map(creature);
const board = { slot: 'Player1', roundNumber: 1, phase: 'Planning', subPhase: 'Evolution', allies: roster.slice(0, 3), enemies: roster.slice(3), timeline: [] };
const feed = [{ sequence: 1, round: 1, event: { kind: 'OngoingEffectsApplied', roundId: 1, bleedTicks: [{ creature: 4, damage: 2 }] } },
  { sequence: 2, round: 1, event: { kind: 'CombatActionResolved', roundId: 1, resolution: { action: { actor: 1, spell: 'strike', targets: [4] } }, appliedOutcomes: [{ kind: 'DamageOutcome', amount: 20, target: 4 }], frame: { before: roster, after: roster.map(one => ({ ...one, health: one.id === 4 ? 0 : 20, isAlive: one.id !== 4 })), timeline: [], rollOffs: [] } } }];
const catalogue = { cards: [{ id: 'strike', name: 'Strike', effects: ['Host damage text'] }], packages: [
  { id: 'root', name: 'Warden', level: 1, initiativeBonus: 2, spells: ['strike'], passiveLines: ['Host passive'] },
  { id: 'future', name: 'Guardian', level: 2, initiativeBonus: 3, spells: [], prerequisites: ['root'] } ] };
async function mount(page, { kind = 'Evolution', replay = false, over = false } = {}) {
  const state = { errors: [], writes: [], shown: [], concessions: 0, reads: [], status: 204, view: { board: structuredClone(board), over, feed: over ? feed : [], feedNext: over ? 3 : 0, waitingFor: over ? null : kind, waitingAsked: over ? null : 11, waitingCreature: 1,
    outcome: over ? { winner: 'player1', reason: 'RoundCap' } : null,
    options: { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['root'] }] }, speed: { missing: [1, 2, 3] }, tieOrder: { ties: [[1, 2, 3]] } } } };
  page.on('pageerror', error => state.errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'first' })));
  await page.route('**/api/**', async route => {
    const req = route.request(), url = new URL(req.url()); state.reads.push(url.pathname);
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'a', format: '3v3' }, seats: [{ seat: 'player1', token: 'first' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: catalogue });
    if (url.pathname.endsWith('/replay')) return route.fulfill({ json: { catalogue, feed, boards: { player1: board, player2: { ...board, allies: board.enemies, enemies: board.allies } }, outcome: { winner: 'player1', reason: 'Elimination' } } });
    if (url.pathname.endsWith('/events')) return route.fulfill({ contentType: 'text/event-stream', body: '' });
    if (url.pathname.endsWith('/concede')) { state.concessions++; state.view.over = true; state.view.waitingFor = null; state.view.outcome = { winner: 'player2', reason: 'Concession' }; return route.fulfill({ status: 204 }); }
    if (url.pathname.endsWith('/decision')) {
      state.writes.push(req.postDataJSON());
      if (state.status !== 204) return route.fulfill({ status: state.status, json: { message: 'The offered pick has changed.' } });
      state.view.waitingFor = null; state.view.waitingAsked = null; return route.fulfill({ status: 204 });
    }
    if (url.pathname === '/api/seat/player1') {
      if (url.searchParams.has('shown')) state.shown.push(Number(url.searchParams.get('shown')));
      return route.fulfill({ json: state.view });
    }
    return route.fulfill({ status: 500 });
  });
  await page.goto(`/table/arena.html${replay ? '?replay=finished&seat=player2' : ''}`);
  await expect(page.locator('#arena')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#loading')).toBeHidden();
  await expect(page.locator('#loading')).toBeHidden(); return state;
}
test('offered cards keep the full tree as a read-only reference and confirm only a legal purchase', async ({ page }, info) => {
  const h = await mount(page); expect(h.shown).toEqual([]); await page.locator('#planning-open').click();
  await expect.poll(() => h.shown).toEqual([11]); await expect(page.locator('#planning-choices')).toContainText('Host passive');
  await page.locator('#mutation-tree > summary').click();
  await page.getByText('Guardian · Stage 2', { exact: false }).click(); await expect(page.locator('[data-mutation="future"]')).toHaveCount(0);
  await expect(page.locator('#planning-choices')).toContainText('Requires: Warden'); await page.locator('[data-mutation="root"]').click(); expect(h.writes).toEqual([]);
  await fitsViewport(page, '#planning-confirm, #planning-close'); await page.screenshot({ path: info.outputPath('mutation-tree.jpg'), type: 'jpeg', quality: 85 });
  await page.locator('#planning-confirm').click(); await expect.poll(() => h.writes).toEqual([{ kind: 'Evolution', creature: 1, tier: 'root', asked: 11 }]);
  await expect(page.locator('#planning-dialog')).toBeHidden(); expect(h.errors).toEqual([]);
});
test('switching creatures drops a mutation picked for the previous one', async ({ page }) => {
  const h = await mount(page); await page.locator('#planning-open').click(); await page.locator('[data-mutation="root"]').click();
  await expect(page.locator('#planning-selection')).toHaveText('Creature 1 · Warden'); await expect(page.locator('#planning-confirm')).toBeEnabled();
  await page.locator('#planning-creatures [data-creature="2"]').click();
  await expect(page.locator('#planning-selection')).toHaveText('Select a choice'); await expect(page.locator('#planning-confirm')).toBeDisabled();
  await page.locator('#planning-creatures [data-creature="1"]').click(); await expect(page.locator('#planning-confirm')).toBeDisabled();
  await page.locator('[data-mutation="root"]').click(); await page.locator('#planning-confirm').click();
  await expect.poll(() => h.writes).toEqual([{ kind: 'Evolution', creature: 1, tier: 'root', asked: 11 }]); expect(h.errors).toEqual([]);
});
test('tempo needs explicit confirmation and a refusal stays visible', async ({ page }) => {
  const h = await mount(page, { kind: 'Speed' }); h.status = 409; await page.locator('#planning-open').click();
  await expect(page.locator('#planning-help')).toContainText('only Precision can crit');
  for (let index = 0; index < 3; index++) await page.locator('[data-speed="Standard"]').click();
  expect(h.writes).toEqual([]);
  await page.locator('#planning-confirm').click(); await expect(page.locator('#planning-dialog-error')).toHaveText('The offered pick has changed.');
  expect(h.writes).toEqual([{ kind: 'Speed', creature: 1, speed: 'Standard', asked: 11 }]); expect(h.errors).toEqual([]);
});
test('ties require a complete order within their group', async ({ page }) => {
  const h = await mount(page, { kind: 'TieOrder' }); await page.locator('#planning-open').click();
  await page.locator('[data-tie="3"]').click(); await expect(page.locator('#planning-confirm')).toBeDisabled(); await page.locator('[data-tie="1"]').click(); await page.locator('#planning-confirm').click();
  await expect.poll(() => h.writes).toEqual([{ kind: 'TieOrder', order: [3, 1, 2], asked: 11 }]); expect(h.errors).toEqual([]);
});
test('concession can be cancelled and displays the host result after confirmation', async ({ page }) => {
  const h = await mount(page); await page.locator('#settings').click(); await page.locator('#concede-open').click(); await page.locator('#concede-cancel').click(); expect(h.concessions).toBe(0);
  await page.locator('#settings').click(); await page.locator('#concede-open').click(); await page.locator('#concede-confirm').click();
  await page.locator('#match-open').click(); await expect(page.locator('#match-outcome')).toHaveText('You conceded · Player 2 wins.'); expect(h.concessions).toBe(1); expect(h.errors).toEqual([]);
});
test('finished matches replay before and after frames, then return to result', async ({ page }, info) => {
  const h = await mount(page, { over: true }); await page.locator('#round-review').click(); await expect(page.locator('#round-notes')).toContainText('Ongoing damage −2'); await page.keyboard.press('Escape');
  await page.locator('#match-open').click(); await expect(page.locator('#match-outcome')).toContainText('round cap'); await page.locator('#match-replay').click();
  await expect(page.locator('#replay-note')).toContainText('Reference:'); await page.locator('#replay-next').click(); await expect(page.locator('[data-creature="4"] .creature-stats')).toContainText('20/20');
  await page.locator('#replay-next').click(); await expect(page.locator('[data-creature="4"] .creature-tag')).toHaveText('DEFEATED');
  await fitsBattlefield(page); await fitsViewport(page, '.creature, #replay-next, #replay-previous, #replay-close'); await page.screenshot({ path: info.outputPath('match-replay.jpg'), type: 'jpeg', quality: 85 });
  await page.locator('#replay-close').click(); await fitsBattlefield(page); await expect(page.locator('#match-open')).toBeVisible(); expect(h.errors).toEqual([]);
});
test('a session replay link reads only that recording and preserves perspective', async ({ page }) => {
  const h = await mount(page, { replay: true }); await page.locator('#match-open').click(); await page.locator('#match-replay').click(); await expect(page.locator('[data-creature="4"]')).toHaveClass(/ally/);
  expect([...new Set(h.reads)]).toEqual(['/api/sessions/finished/replay']); expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
for (const viewport of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) test(`planning confirmation fits ${viewport.width}x${viewport.height}`, async ({ page }) => {
  await page.setViewportSize(viewport); const h = await mount(page); await page.locator('#planning-open').click(); await page.locator('[data-mutation="root"]').click();
  await fitsViewport(page, '#planning-close, #planning-confirm'); await page.locator('#planning-confirm').click(); await expect.poll(() => h.writes.length).toBe(1); expect(h.errors).toEqual([]);
});

test('recorded zero ordinary energy stays visible in replay before ongoing effects', async ({ page }) => {
  const h = await mount(page, { over: true });
  h.view.feed = [{ sequence: 0, round: 1, event: { kind: 'RoundEnergyGranted', roundId: 1, grants: [
    { creature: 1, baseRequested: 0, passiveRequested: 0, gained: 0, before: 7, after: 7 },
  ] } }, ...feed];
  await page.locator('#match-open').click(); await page.locator('#match-replay').click();
  await expect(page.locator('#replay-text')).toHaveText('Upkeep · round energy · Creature 1: +0 energy · 7 → 7 (base 0, passive 0)');
  await page.locator('#replay-next').click(); await expect(page.locator('#replay-text')).toContainText('Ongoing damage');
  await page.locator('#replay-previous').click(); await expect(page.locator('#replay-text')).toContainText('+0 energy');
  await fitsViewport(page, '#replay-next, #replay-previous, #replay-close');
  expect(h.writes).toEqual([]); expect(h.shown).toEqual([]); expect(h.errors).toEqual([]);
});
