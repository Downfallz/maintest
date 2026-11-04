import { test, expect } from '@playwright/test';
import { fitsViewport } from './fit.js';
const creature = (id, health = 20) => ({ id, name: `Creature ${id}`, health, maxHealth: 20, energy: 4,
  totalDefense: 2, isAlive: health > 0, knownSpells: ['strike'], conditions: [], acquiredTiers: [] });
const roster = [1, 2, 3, 4, 5, 6].map(id => creature(id));
const entry = sequence => ({ sequence, event: { kind: 'CombatActionResolved', roundId: 2,
  resolution: { action: { actor: 4, spell: 'strike', targets: [1] }, isCritical: true },
  appliedOutcomes: [{ kind: 'DamageOutcome', target: 1, amount: 7, critical: true }],
  frame: { before: roster, after: roster.map(one => creature(one.id, one.id === 1 ? 13 : 20)), timeline: [], rollOffs: [] } } });
async function mount(page, feed = [entry(10)]) {
  const state = { errors: [], writes: [], shown: [], status: 200, view: {
    board: { slot: 'Player1', roundNumber: 2, phase: 'Planning', subPhase: 'Evolution', allies: roster.slice(0, 3), enemies: roster.slice(3), timeline: [] },
    waitingFor: 'Evolution', waitingAsked: 11, waitingCreature: 1,
    options: { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['root'] }] } },
    feed, feedNext: Math.max(0, ...feed.map(one => one.sequence + 1)) } };
  page.on('pageerror', error => state.errors.push(error.message));
  await page.clock.install();
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' }));
    const original = window.fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      window.notifyArena = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  });
  await page.route('**/api/**', route => {
    const request = route.request(), url = new URL(request.url());
    if (request.method() !== 'GET') state.writes.push(url.pathname);
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'a', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [{ id: 'strike', name: 'Strike', cost: 2 }], packages: [{ id: 'root', name: 'Warden', level: 1, spells: ['strike'] }] } });
    if (url.pathname === '/api/seat/player1') {
      if (url.searchParams.has('shown')) state.shown.push(url.searchParams.get('shown'));
      return route.fulfill({ status: state.status, json: state.view });
    }
    return route.fulfill({ status: 500 });
  });
  await page.goto('/table/arena.html'); await expect(page.locator('#arena')).toHaveAttribute('data-ready', 'true');
  await page.locator('#seat-choices button').click(); await expect(page.locator('#connection')).toHaveText('LIVE');
  // Discovery POSTs the saved capabilities to resume; measure journal writes after this baseline.
  state.writes.length = 0;
  await page.clock.pauseAt(new Date(Date.now() + 1000));
  const publish = async (feed, extra = {}) => {
    state.view = { ...state.view, ...extra, feed, feedNext: Math.max(state.view.feedNext, ...feed.map(one => one.sequence + 1)) };
    await page.evaluate(() => window.notifyArena());
  };
  return { state, publish };
}
const rows = page => page.locator('#history-list details');
test('initial history shows confirmed attacks with accessible details and no hidden choices or writes', async ({ page }, info) => {
  const h = await mount(page, [entry(10), { sequence: 11, event: { kind: 'IntentSubmitted', spell: 'PRIVATE_SENTINEL' } },
    { sequence: 12, event: { kind: 'UnknownEvent', secret: 'UNKNOWN_SENTINEL' } }]);
  await expect(page.locator('#playback-skip')).toBeHidden();
  await page.locator('#history-open').click(); await expect(rows(page)).toHaveCount(1);
  await rows(page).locator('summary').click(); await expect(rows(page)).toContainText('HP 20 → 13');
  await expect(rows(page)).toContainText('Creature 4 (Opponent)'); await expect(rows(page)).toContainText('Critical');
  await expect(rows(page)).toContainText('Damage 7!');
  await expect(page.locator('#history-dialog')).not.toContainText('SENTINEL');
  await fitsViewport(page, '#history-close');
  await page.screenshot({ path: info.outputPath('history.jpg'), type: 'jpeg', quality: 85 });
  await page.keyboard.press('Escape'); await expect(page.locator('#history-open')).toBeFocused();
  expect(h.state.shown).toEqual([]); expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('skip and duplicate polls keep every action without replaying it', async ({ page }) => {
  const h = await mount(page); await h.publish([entry(11), entry(12)]);
  await expect(page.locator('#playback-skip')).toBeVisible(); await page.locator('#history-open').click();
  await expect(page.locator('#playback-skip')).toBeHidden(); await expect(rows(page)).toHaveCount(3);
  await h.publish([entry(12), entry(10)]); await expect(rows(page)).toHaveCount(3);
  await page.locator('#history-close').click(); await page.locator('#history-open').click(); await expect(rows(page)).toHaveCount(3);
  expect(h.state.errors).toEqual([]);
});
test('reading preserves an open draft and does not acknowledge a new question', async ({ page }) => {
  const h = await mount(page); await page.locator('#planning-open').click();
  await expect.poll(() => h.state.shown).toEqual(['11']); await page.locator('[data-mutation="root"]').click();
  await page.locator('#planning-dialog [data-history-open]').click();
  await expect(page.locator('#planning-confirm')).toBeDisabled();
  await h.publish([entry(10)]); await page.locator('#history-close').click();
  await expect(page.locator('#planning-dialog [data-history-open]')).toBeFocused();
  await expect(page.locator('#planning-selection')).toHaveText('Creature 1 · Warden');
  await expect(page.locator('#planning-confirm')).toBeEnabled();
  await page.locator('#planning-dialog [data-history-open]').click();
  await h.publish([entry(11)], { waitingAsked: 20 }); await expect(rows(page)).toHaveCount(2);
  expect(h.state.shown).toEqual(['11']); expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('new events preserve expanded details, keyboard focus and reading position', async ({ page }) => {
  const h = await mount(page, Array.from({ length: 25 }, (_, i) => entry(i + 10)));
  await page.locator('#history-open').click(); const first = rows(page).first();
  await first.locator('summary').click(); await first.locator('summary').focus();
  const scroll = await page.locator('#history-list').evaluate(node => node.scrollTop);
  await h.publish([entry(35)]); await expect(rows(page)).toHaveCount(26);
  await expect(first).toHaveAttribute('open', ''); await expect(first.locator('summary')).toBeFocused();
  expect(await page.locator('#history-list').evaluate(node => node.scrollTop)).toBe(scroll);
  await expect(page.locator('#history-new')).toHaveText(/^1 new events? · Latest$/);
  await page.locator('#history-new').click(); await expect(page.locator('#history-new')).toBeHidden();
  await expect.poll(() => page.locator('#history-list').evaluate(node => node.scrollHeight - node.scrollTop - node.clientHeight)).toBeLessThan(2);
  expect(h.state.errors).toEqual([]);
});
for (const reason of ['seat', 'hidden', 'revoked']) test(`${reason} erases all journal surfaces and pending updates`, async ({ page }) => {
  const h = await mount(page); await page.locator('#history-open').click();
  if (reason === 'seat') { await page.locator('#history-close').click(); await page.locator('#switch-seat').click(); }
  if (reason === 'hidden') await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
  if (reason === 'revoked') { h.state.status = 403; await page.evaluate(() => window.notifyArena()); }
  await expect(rows(page)).toHaveCount(0); await expect(page.locator('#history-dialog')).toBeHidden();
  await expect(page.locator('#history-open')).not.toHaveAttribute('title');
  await expect(page.locator('#history-open')).toBeDisabled();
  await page.clock.runFor(3000); await expect(rows(page)).toHaveCount(0); expect(h.state.errors).toEqual([]);
});
test('two-round retention is explicit and capped while viewport controls stay reachable', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'Viewport sweep runs once.');
  const h = await mount(page, Array.from({ length: 205 }, (_, i) => entry(i + 1)));
  for (const size of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(size); await page.clock.runFor(150); await fitsViewport(page, '#history-open, #switch-seat, #settings');
    await page.locator('#history-open').click(); await expect(rows(page)).toHaveCount(200);
    await expect(page.locator('#history-limit')).toContainText('Older entries are not included');
    await fitsViewport(page, '#history-close'); await page.locator('#history-close').click();
  }
  await h.publish([], { board: { ...h.state.view.board, roundNumber: 4 } });
  await page.locator('#history-open').click(); await expect(rows(page)).toHaveCount(0);
  await expect(page.locator('#history-empty')).toBeVisible(); expect(h.state.errors).toEqual([]);
});

test('a selected spell and field target survive reading the journal', async ({ page }) => {
  const h = await mount(page);
  await h.publish([], { board: { ...h.state.view.board, phase: 'Combat', subPhase: 'Activation' },
    waitingFor: 'Action', waitingAsked: 12, options: { action: { creature: 1,
      spells: [{ spell: 'strike', legalTargets: { candidates: [4, 5, 6], minTargets: 1, maxTargets: 1 } }] } } });
  await page.locator('#combat-open').click(); await page.locator('[data-spell="strike"]').click();
  await page.locator('[data-creature="4"]').click(); await expect(page.locator('#combat-confirm')).toBeEnabled();
  await page.locator('#history-open').click(); await expect(page.locator('#combat-confirm')).toBeDisabled();
  await h.publish([entry(10)]); await page.locator('#history-close').click();
  await expect(page.locator('[data-creature="4"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('#combat-confirm')).toBeEnabled();
  await expect(page.locator('#history-open')).toBeFocused();
  expect(h.state.shown).toEqual(['12']); expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
