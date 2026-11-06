import { test, expect } from '@playwright/test';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningBattlePlayer);
const creature = (id, health = 20) => ({ id, name: `Creature ${id}`, health, maxHealth: 20, energy: 4,
  totalDefense: 2, isAlive: health > 0, knownSpells: ['strike'], conditions: [], acquiredTiers: [] });
const roster = [1, 2, 3, 4, 5, 6].map(id => creature(id));
const entry = sequence => ({ sequence, event: { kind: 'CombatActionResolved', roundId: 2,
  resolution: { action: { actor: 4, spell: 'strike', targets: [1] }, isCritical: true },
  appliedOutcomes: [{ kind: 'DamageOutcome', target: 1, amount: 7, critical: true }],
  frame: { before: roster, after: roster.map(one => creature(one.id, one.id === 1 ? 13 : 20)), timeline: [], rollOffs: [] } } });
async function mount(page, feed = [entry(10)]) {
  const state = { errors: [], writes: [], shown: [], reads: [], historyFeed: null, historyStatus: 200, historyGate: null, status: 200, view: {
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
      window.notifyBattle = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  });
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url());
    if (request.method() !== 'GET') state.writes.push(url.pathname);
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'a', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [{ id: 'strike', name: 'Strike', cost: 2 }], packages: [{ id: 'root', name: 'Warden', level: 1, spells: ['strike'] }] } });
    if (url.pathname === '/api/seat/player1') {
      state.reads.push(url.search);
      if (!url.search && state.historyFeed) {
        if (state.historyGate) await state.historyGate;
        return route.fulfill({ status: state.historyStatus, json: { ...state.view, board: { ...state.view.board, roundNumber: 1 }, feed: state.historyFeed, feedNext: 3 } });
      }
      if (url.searchParams.has('shown')) state.shown.push(url.searchParams.get('shown'));
      return route.fulfill({ status: state.status, json: state.view });
    }
    return route.fulfill({ status: 500 });
  });
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 10000));
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  // Freeze before connecting: advancing an active poll can leave a zero-delay owed refresh
  // suspended just as the fixture publishes its first notification.
  await expect(page.locator('#loading')).toBeHidden(); await expect(page.locator('#connection')).toHaveText('LIVE');
  // Discovery POSTs the saved capabilities to resume; measure journal writes after this baseline.
  state.writes.length = 0;
  const publish = async (feed, extra = {}) => {
    state.view = { ...state.view, ...extra, feed, feedNext: Math.max(state.view.feedNext, ...feed.map(one => one.sequence + 1)) };
    await page.evaluate(() => window.notifyBattle());
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
  if (reason === 'revoked') { h.state.status = 403; await page.evaluate(() => window.notifyBattle()); }
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
test('actions arriving while the journal is open settle on the board and stay readable', async ({ page }) => {
  const h = await mount(page); await page.locator('#history-open').click();
  await h.publish([entry(11), entry(12)]); await expect(rows(page)).toHaveCount(3);
  await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'decision');
  await expect(page.locator('#playback-skip')).toBeHidden();
  await page.locator('#history-close').click();
  await expect(page.locator('#playback-result')).toBeVisible();
  await expect(page.locator('#result-text')).toContainText('Creature 4 (opponent) · Strike → Creature 1');
  expect(h.state.errors).toEqual([]);
});
test('the action result opens the journal over itself and takes the focus back', async ({ page }) => {
  const h = await mount(page); await h.publish([entry(11)]);
  await page.locator('#playback-skip').click(); await page.locator('#playback-result').click();
  await expect(page.locator('#result-dialog')).toBeVisible();
  await page.locator('#result-dialog [data-history-open]').click(); await expect(rows(page)).toHaveCount(2);
  await page.keyboard.press('Escape');
  await expect(page.locator('#result-dialog [data-history-open]')).toBeFocused();
  await expect(page.locator('#result-dialog')).toBeVisible(); await expect(page.locator('#history-dialog')).toBeHidden();
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('upkeep ticks, reveals and phase markers read beside the attacks', async ({ page }) => {
  const h = await mount(page, [
    { sequence: 8, event: { kind: 'RoundStarted', roundId: 2 } },
    { sequence: 9, event: { kind: 'OngoingEffectsApplied', roundId: 2, energyRegenerationTicks: [], regenerationTicks: [], bleedTicks: [{ creature: 1, damage: 2 }] } },
    { sequence: 10, event: { kind: 'ActionRevealed', roundId: 2, action: { actor: 4, spell: 'strike', targets: [1] } } },
    entry(11)]);
  await page.locator('#history-open').click();
  await expect(page.locator('#history-list [data-sequence]')).toHaveCount(4);
  await expect(page.locator('#history-list [data-sequence="8"]')).toHaveClass(/history-marker/);
  await expect(page.locator('#history-list [data-sequence="8"] summary')).toHaveCount(0);
  await expect(page.locator('#history-list [data-sequence="8"]')).toContainText('Round begins');
  const upkeep = page.locator('#history-list [data-sequence="9"]'); await upkeep.locator('summary').click();
  await expect(upkeep.locator('[data-tone="harm"]')).toHaveText('Creature 1 (Your team) · Ongoing damage −2 HP');
  await expect(page.locator('#history-list [data-sequence="10"] summary')).toContainText('Creature 4 (Opponent) · reveals Strike');
  await expect(page.locator('#history-range')).toHaveText('Loaded round 2 · 4 events');
  expect(h.state.errors).toEqual([]);
});

test('ordinary energy history keeps actual gains, zero gains and the order before ongoing ticks', async ({ page }) => {
  const energy = { sequence: 7, event: { kind: 'RoundEnergyGranted', roundId: 2, grants: [
    { creature: 1, baseRequested: 4, passiveRequested: 1, gained: 3, before: 7, after: 10 },
    { creature: 4, baseRequested: 0, passiveRequested: 0, gained: 0, before: 2, after: 2 },
  ] } };
  const h = await mount(page, [energy, { sequence: 8, event: { kind: 'OngoingEffectsApplied', roundId: 2,
    energyRegenerationTicks: [], regenerationTicks: [], bleedTicks: [{ creature: 1, damage: 7 }] } }]);
  await page.locator('#history-open').click(); await expect(rows(page)).toHaveCount(2);
  await rows(page).first().locator('summary').click();
  await expect(rows(page).first()).toContainText('Upkeep · round energy');
  await expect(rows(page).first().locator('[data-tone="energy"]')).toHaveCount(2);
  await expect(rows(page).first()).toContainText('+3 energy · 7 → 10 (base 4, passive 1)');
  await expect(rows(page).first()).toContainText('+0 energy · 2 → 2 (base 0, passive 0)');
  await expect(rows(page).first()).toContainText('Creature 4 (Opponent)');
  await rows(page).nth(1).locator('summary').click();
  await expect(rows(page).nth(1)).toContainText('Upkeep · ongoing effects');
  await expect(rows(page).nth(1).locator('[data-tone="harm"]')).toHaveText('Creature 1 (Your team) · Ongoing damage −7 HP');
  await h.publish([energy]); await expect(rows(page)).toHaveCount(2);
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});

test('filters distinguish no match from no history and details use exact boundaries while the live round advances', async ({ page }, info) => {
  const h = await mount(page, [entry(10), entry(11)]); await page.locator('#history-open').click();
  await page.locator('#history-filters > summary').click();
  await page.locator('#history-filter-team').selectOption('Opponent'); await expect(rows(page)).toHaveCount(2);
  await page.locator('#history-filter-creature').selectOption('1'); await expect(rows(page)).toHaveCount(2);
  await page.locator('#history-filter-round').selectOption('2');
  await page.locator('#history-filter-kind').selectOption('CombatActionResolved');
  await rows(page).first().locator('summary').click(); await rows(page).first().getByRole('button', { name: 'Inspect event' }).click();
  await expect(page.locator('#history-detail')).toContainText('Caster and spell'); await expect(page.locator('#history-detail')).toContainText('Applied effects');
  await expect(page.locator('.history-creature').first()).toContainText('13/20 HP');
  await page.locator('#history-before').click(); await expect(page.locator('.history-creature').first()).toContainText('20/20 HP');
  await h.publish([{ sequence: 20, event: { kind: 'RoundStarted', roundId: 5 } }], { board: { ...h.state.view.board, roundNumber: 5, allies: [creature(1, 1), ...roster.slice(1, 3)] } });
  await expect(page.locator('#round-number')).toHaveText('05'); await expect(page.locator('.history-creature').first()).toContainText('20/20 HP');
  await expect(page.locator('#history-before')).toBeFocused();
  await page.locator('#history-next').click(); await expect(page.locator('#history-detail')).toContainText('event 11');
  await page.locator('#history-previous').click(); await expect(page.locator('#history-detail')).toContainText('event 10');
  await page.locator('#history-filters > summary').click();
  await fitsViewport(page, '#history-close, #history-latest');
  await page.screenshot({ path: info.outputPath('history-review.jpg'), type: 'jpeg', quality: 85 });
  await page.locator('#history-back').click(); await page.locator('#history-filters > summary').click();
  await page.locator('#history-filter-kind').selectOption('RoundStarted'); await expect(rows(page)).toHaveCount(0);
  await expect(page.locator('#history-empty')).toContainText('No events match');
  await page.locator('#history-latest:visible, #history-new:visible').click(); await expect(page.locator('#history-review')).toBeHidden();
  await expect(page.locator('#history-list')).toContainText('Round begins');
  expect(h.state.shown).toEqual([]); expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('older history can arrive after a live update without moving its cursor or the battlefield backwards', async ({ page }) => {
  const h = await mount(page); h.state.historyFeed = [{ ...entry(1), event: { ...entry(1).event, roundId: 1 } }];
  let release; h.state.historyGate = new Promise(resolve => { release = resolve; });
  await page.locator('#history-open').click(); await page.locator('#history-earlier').click(); await expect(page.locator('#history-earlier')).toBeDisabled();
  await h.publish([entry(30)], { board: { ...h.state.view.board, roundNumber: 3 } });
  await expect(page.locator('#round-number')).toHaveText('03'); release();
  await expect(page.locator('#history-list [data-sequence="1"]')).toHaveCount(1);
  await expect(page.locator('#round-number')).toHaveText('03'); await expect(page.locator('#history-latest')).toContainText('1 new');
  await h.publish([entry(31)]); await expect.poll(() => h.state.reads.includes('?since=31')).toBe(true);
  await expect(page.locator('#history-list [data-sequence="1"]')).toHaveCount(1);
  await page.locator('#history-latest').click(); await expect(page.locator('#history-list [data-sequence="31"]')).toHaveCount(1);
  expect(h.state.reads.filter(query => query === '').length).toBe(2); expect(h.state.shown).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('historical loading reports failure, supports retry, and a legacy event never shows current creature stats', async ({ page }) => {
  const h = await mount(page); h.state.historyFeed = [{ ...entry(1), event: { ...entry(1).event, frame: undefined, roundId: 1 } }]; h.state.historyStatus = 503;
  await page.locator('#history-open').click(); await page.locator('#history-earlier').click();
  await expect(page.locator('#history-load-error')).toContainText('History unavailable'); await expect(rows(page)).toHaveCount(1);
  h.state.historyStatus = 200; await page.locator('#history-earlier').click();
  await expect(page.locator('#history-load-error')).toBeEmpty(); await expect(page.locator('#history-earlier')).toHaveText('Beginning of available history');
  await rows(page).locator('summary').click(); await rows(page).getByRole('button', { name: 'Inspect event' }).click();
  await expect(page.locator('#history-detail')).toContainText('Historical battlefield unavailable'); await expect(page.locator('.history-creature')).toHaveCount(0);
  await expect(page.locator('#history-before')).toBeDisabled(); expect(h.state.errors).toEqual([]);
});
test('access loss erases historical filters, review and late history responses', async ({ page }) => {
  const h = await mount(page); h.state.historyFeed = [entry(1)]; let release;
  h.state.historyGate = new Promise(resolve => { release = resolve; });
  await page.locator('#history-open').click(); await rows(page).locator('summary').click(); await rows(page).getByRole('button', { name: 'Inspect event' }).click();
  await page.locator('#history-earlier').click(); h.state.status = 403; await h.publish([]);
  await expect(page.locator('#history-dialog')).toBeHidden(); release(); await page.clock.runFor(100);
  await expect(page.locator('#history-detail')).toBeEmpty(); await expect(page.locator('#history-filter-creature option')).toHaveCount(1);
  await expect(rows(page)).toHaveCount(0); expect(h.state.errors).toEqual([]);
});
test('reload makes earlier rounds available through an explicit seat history read', async ({ page }) => {
  const h = await mount(page); h.state.view.board.roundNumber = 6;
  const old = { ...entry(1), event: { ...entry(1).event, roundId: 1 } }, recent = { ...entry(50), event: { ...entry(50).event, roundId: 6 } };
  h.state.view.feed = [old, recent]; h.state.view.feedNext = 51;
  await page.reload(); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true'); await expect(page.locator('#loading')).toBeHidden();
  await page.locator('#history-open').click(); await expect(page.locator('#history-list [data-sequence="1"]')).toHaveCount(0);
  h.state.historyFeed = [old, recent]; await page.locator('#history-earlier').click();
  await expect(page.locator('#history-list [data-sequence="1"]')).toHaveCount(1); expect(h.state.shown).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('unread counts stay reachable in the closed journal launcher and clear when it is opened', async ({ page }) => {
  const h = await mount(page);
  await h.publish(Array.from({ length: 1000 }, (_, index) => ({ sequence: 20 + index, event: { kind: 'RoundEnded', roundId: 2 } })));
  await expect(page.locator('#history-unread')).toContainText('1000');
  await expect(page.locator('#history-open')).toHaveAccessibleDescription(/1000 new/);
  const bounds = await page.locator('#history-open').evaluate(node => ({ button: node.getBoundingClientRect().toJSON(), header: node.closest('header').getBoundingClientRect().toJSON() }));
  expect(bounds.button.top).toBeGreaterThanOrEqual(bounds.header.top); expect(bounds.button.bottom).toBeLessThanOrEqual(bounds.header.bottom);
  await page.locator('#history-open').click(); await expect(page.locator('#history-unread')).toBeEmpty();
  expect(h.state.shown).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('Back returns focus to the inspected row and the kind filter speaks player words', async ({ page }) => {
  const h = await mount(page, [entry(10), entry(11), { sequence: 12, event: { kind: 'RoundStarted', roundId: 2 } }]);
  await page.locator('#history-open').click();
  const second = page.locator('#history-list [data-sequence="11"]');
  await second.locator('summary').click(); await second.getByRole('button', { name: 'Inspect event' }).click();
  await page.locator('#history-back').click(); await expect(second.locator('summary')).toBeFocused();
  await page.locator('#history-filters > summary').click();
  await expect(page.locator('#history-filter-kind option')).toHaveText(['All event kinds', 'Spells cast', 'Rounds begun']);
  expect(h.state.errors).toEqual([]);
});
test('a covered question stays unacknowledged while an event is inspected and earlier rounds load', async ({ page }) => {
  const h = await mount(page); h.state.historyFeed = [{ ...entry(1), event: { ...entry(1).event, roundId: 1 } }];
  await page.locator('#planning-open').click(); await expect.poll(() => h.state.shown).toEqual(['11']);
  await page.locator('#planning-dialog [data-history-open]').click();
  await rows(page).first().locator('summary').click(); await rows(page).first().getByRole('button', { name: 'Inspect event' }).click();
  await page.locator('#history-earlier').click(); await expect(page.locator('#history-list [data-sequence="1"]')).toHaveCount(1);
  await h.publish([], { waitingAsked: 12 }); await page.clock.runFor(200);
  expect(h.state.shown).toEqual(['11']); expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
