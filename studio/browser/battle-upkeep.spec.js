import { test, expect } from '@playwright/test';
import { WINDUP_MS, IMPACT_MS } from '../../table/battle-timing.js';
import { fitsViewport } from './fit.js';
const creature = (id, health = 20) => ({ id, name: id === 1 ? 'Warden' : `Creature ${id}`, health, maxHealth: 20, energy: 7,
  totalDefense: 2, isAlive: health > 0, knownSpells: ['strike'], conditions: [], acquiredTiers: [] });
const roster = [1, 2, 3, 4, 5, 6].map(id => creature(id));
const energy = round => ({ sequence: round * 10, round: round + 1, event: { kind: 'RoundEnergyGranted', roundId: round, grants: roster.map(one => ({
  creature: one.id, baseRequested: one.id === 6 ? 0 : 3, passiveRequested: one.id === 1 ? 2 : 0,
  gained: one.id === 6 ? 0 : one.id === 1 ? 5 : 3, before: 2, after: one.id === 6 ? 2 : one.id === 1 ? 7 : 5,
})) } });
const ongoing = (round, ticks = false) => ({ sequence: round * 10 + 1, round: round + 1, event: { kind: 'OngoingEffectsApplied', roundId: round,
  ...(ticks ? { energyRegenerationTicks: [{ creature: 1, gained: 2 }], regenerationTicks: [{ creature: 1, healed: 0 }], bleedTicks: [{ creature: 1, damage: 7 }] } : {}) } });
async function mount(page, { waiting = null, round = 1, feed = [energy(round), ongoing(round)], tips = false, next = round } = {}) {
  const state = { errors: [], writes: [], shown: [], answered: null, status: 200, view: { board: { slot: 'Player1', roundNumber: round, phase: 'Planning', subPhase: 'Evolution',
    nextEvolutionRound: next, allies: roster.slice(0, 3), enemies: roster.slice(3), timeline: [] }, waiting,
    waitingFor: 'Evolution', waitingAsked: 11, waitingCreature: 1,
    options: { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['root'] }] } }, feed, feedNext: round * 10 + 2 } };
  page.on('pageerror', error => state.errors.push(error.message));
  await page.clock.install();
  await page.addInitScript(({ tips }) => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' }));
    if (!tips) localStorage.setItem('downfall.arena.help.v1', JSON.stringify({ version: 1, muted: true, seen: [] }));
    // A returning player who reads in Auto (ADR 0140); tips stay as each test asks.
    localStorage.setItem('downfall.arena.game-settings.v1', JSON.stringify({ pace: 'auto', speed: 1, help: Boolean(tips), reducedMotion: false }));
    const original = window.fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      // A host says it is still there every twenty seconds (ADR 0119): without it the page takes a quiet stream for a
      // dropped one after 45 seconds (ADR 0164) and opens another, and a long test would notify the closed one.
      const alive = setInterval(() => { try { controller.enqueue(new TextEncoder().encode(': keep-alive\n\n')); } catch { clearInterval(alive); } }, 20000);
      window.notifyBattle = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  }, { tips });
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url());
    // Discovering the seats posts their tokens to read them, at load and again when the opponent is seated.
    // The seat's seen mark (ADR 0194) is its own word, not a decision.
    if (request.method() !== 'GET' && url.pathname !== '/api/lobby/resume' && !url.pathname.endsWith('/seen')) state.writes.push(url.pathname);
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'a', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { rules: { energyPerRound: 99, combatDeclaration: 'AtActivation' }, cards: [{ id: 'strike', name: 'Strike', cost: 2 }], packages: [{ id: 'root', name: 'Warden', level: 1, spells: ['strike'] }] } });
    if (url.pathname === '/api/seat/player1') {
      if (url.searchParams.has('shown')) state.shown.push(url.searchParams.get('shown'));
      // A host answers with the board it had when it was asked. A board read held here is answered only once the board
      // has changed under it: the read that was still on its way when the change was told. An acknowledgement reaches
      // no board, so it never says a change arrived.
      const view = state.view, board = !url.searchParams.has('shown');
      if (board && state.hold) {
        state.hold = false; state.held = true;
        try { await expect.poll(() => state.view !== view).toBe(true); } finally { await route.fulfill({ status: state.status, json: view }); }
        state.answered = view; return;
      }
      await route.fulfill({ status: state.status, json: view }); if (board) state.answered = view; return;
    }
    return route.fulfill({ status: 500 });
  });
  await page.clock.pauseAt(new Date(await page.evaluate(() => Date.now() + 10000)));
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  // Freeze before connecting, as the history and playback fixtures do: pausing later fast-forwards the first
  // poll, and a notification landing while it is in flight owes a zero-delay refresh that a paused clock strands.
  // The loading card is put away before the first read is asked: only the board it brought says it arrived (#768).
  await expect(page.locator('#loading')).toBeHidden(); await expect(page.locator('#connection')).toHaveText(/^LIVE/);
  state.writes.length = 0;
  const publish = async (feed, extra = {}) => {
    state.view = { ...state.view, ...extra, feed, feedNext: Math.max(state.view.feedNext, ...feed.map(one => one.sequence + 1)) };
    const published = state.view;
    await page.evaluate(() => window.notifyBattle());
    // A change told while a read is in flight is owed one more read, which the page schedules at no delay: a paused
    // clock runs it only when it is moved, and a loaded machine is when the two overlap. The read in flight answers
    // with the board it was asked for, so only a read that carried this one says the change arrived (#768).
    await expect.poll(async () => { if (state.answered === published) { return true; } await page.clock.runFor(0); return false; }).toBe(true);
  };
  return { state, publish };
}
const close = page => page.locator('#upkeep-close').click();
test('arrivals gate one grouped receipt, then the mutation tip, without acknowledging a question', async ({ page }, info) => {
  const h = await mount(page, { waiting: { seats: [] }, tips: true });
  await expect(page.locator('#upkeep-dialog')).toBeHidden(); await expect(page.locator('#help-dialog')).toBeHidden();
  await h.publish([], { waiting: null });
  await expect(page.locator('#upkeep-dialog')).toBeVisible(); await expect(page.locator('#help-dialog')).toBeHidden();
  await expect(page.locator('#upkeep-close')).toBeFocused();
  await expect(page.locator('#upkeep-body')).toContainText('+5 energy · 2 → 7 (base 3, passive 2)');
  await expect(page.locator('#upkeep-body')).toContainText('+0 energy · 2 → 2');
  await expect(page.locator('#upkeep-body')).toContainText('No ongoing effects ticked');
  await expect(page.locator('#upkeep-body')).not.toContainText('99');
  await expect(page.locator('#upkeep-body')).toBeHidden();
  await expect(page.locator('#upkeep-intro')).toHaveText('Round energy · 6 creatures');
  await page.clock.runFor(350);
  const sheet = await page.locator('#upkeep-dialog').boundingBox();
  const zone = await page.locator('#battle-zone').boundingBox();
  expect(sheet.y).toBeGreaterThanOrEqual(zone.y + zone.height);
  await fitsViewport(page, '#upkeep-close, #upkeep-details');
  await page.screenshot({ path: info.outputPath('upkeep-receipt.jpg'), type: 'jpeg', quality: 85 });
  expect(h.state.shown).toEqual([]); expect(h.state.writes).toEqual([]);
  await close(page); await expect(page.locator('#help-title')).toHaveText('What is a mutation?');
  await expect(page.locator('#help-close')).toBeFocused(); expect(h.state.errors).toEqual([]);
});
test('inspection is current while receipt and journal retain historical zero healing and lethal damage', async ({ page }) => {
  const h = await mount(page, { feed: [energy(1), ongoing(1, true)] });
  await h.publish([], { board: { ...h.state.view.board, allies: [creature(1, 0), ...roster.slice(1, 3)] } });
  await page.locator('#upkeep-details').click();
  await page.getByRole('button', { name: 'Inspect Warden · Your team', exact: true }).first().click();
  await expect(page.locator('#inspect-stats')).toHaveText(/^0\/20 HP/); await expect(page.locator('#inspect-effects')).toContainText('Defeated');
  await expect(page.locator('#inspect-effects')).not.toContainText('Bleed'); await page.keyboard.press('Escape');
  const rows = page.locator('#upkeep-body section').nth(1).locator('li');
  await expect(rows).toHaveCount(3); await expect(rows.nth(0)).toContainText('Energy from effects +2');
  await expect(rows.nth(1)).toContainText('Healing over time +0'); await expect(rows.nth(2)).toContainText('Ongoing damage −7');
  await h.publish([]); await expect(rows).toHaveCount(3);
  await page.locator('#upkeep-dialog [data-history-open]').click(); await page.locator('#history-mode-events').click();
  await page.locator('#history-list details').last().locator('summary').click();
  await expect(page.locator('#history-list')).toContainText('Healing over time +0');
  // Closing the receipt opens the mutation tree (ADR 0138): that, not the receipt, acknowledges the asking.
  await page.locator('#history-close').click(); await close(page);
  await expect.poll(() => h.state.shown).toEqual(['11']); expect(h.state.errors).toEqual([]);
});
test('a change told while a read is on its way reaches the board the read did not carry', async ({ page }) => {
  const h = await mount(page);
  // Under load, the first board can still be on its way when the scene is ready (#768): a read asked before the
  // change is answered after it, with the board it was asked for, and the change's notice is owed one more read.
  h.state.hold = true; await page.evaluate(() => window.notifyBattle()); await expect.poll(() => h.state.held).toBe(true);
  await h.publish([], { board: { ...h.state.view.board, allies: [creature(1, 0), ...roster.slice(1, 3)] } });
  await expect(page.locator('[data-creature="1"]')).toHaveAccessibleName(/Warden of your team, 0 of 20 HP/);
  expect(h.state.errors).toEqual([]);
});
test('inspection reads current conditions while the receipt keeps the damage of a bleed since expired', async ({ page }) => {
  const h = await mount(page, { feed: [energy(1), ongoing(1, true)] });
  const warden = conditions => ({ ...creature(1, 13), conditions });
  await h.publish([], { board: { ...h.state.view.board, allies: [warden([{ effect: { kind: 'Bleed', amountPerRound: 7 }, remainingRounds: 1 }]), ...roster.slice(1, 3)] } });
  await page.locator('#upkeep-details').click();
  const inspect = page.getByRole('button', { name: 'Inspect Warden · Your team', exact: true }).first();
  await inspect.click(); await expect(page.locator('#inspect-effects')).toContainText(/Bleed 7 · 1 round(?!s)/); await page.keyboard.press('Escape');
  // The bleed has run out; a defense with its own countdown is what the creature carries now.
  await h.publish([], { board: { ...h.state.view.board, allies: [warden([{ effect: { kind: 'Defense', amount: 2 }, remainingRounds: 2 }]), ...roster.slice(1, 3)] } });
  await inspect.click(); await expect(page.locator('#inspect-effects')).toContainText('Defense 2 · 2 rounds');
  await expect(page.locator('#inspect-effects')).not.toContainText('Bleed'); await page.keyboard.press('Escape');
  await expect(page.locator('#upkeep-body section').nth(1).locator('li').nth(2)).toContainText('Ongoing damage −7');
  await expect(page.locator('#upkeep-body')).not.toContainText('Defense');
  expect(h.state.shown).toEqual([]); expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('closing attack playback finishes before the next-round receipt and phase tip', async ({ page }) => {
  const h = await mount(page, { round: 2, feed: [], tips: true });
  await page.locator('#help-close').click(); await page.locator('#planning-close').click();
  const attack = { sequence: 25, round: 3, event: { kind: 'CombatActionResolved', roundId: 2,
    resolution: { action: { actor: 4, spell: 'strike', targets: [1] }, isCritical: false }, appliedOutcomes: [{ kind: 'DamageOutcome', target: 1, amount: 7 }],
    frame: { before: roster, after: roster.map(one => creature(one.id, one.id === 1 ? 13 : 20)), timeline: [], rollOffs: [] } } };
  await h.publish([attack, energy(3), ongoing(3)], { board: { ...h.state.view.board, roundNumber: 3 }, waitingFor: 'Speed', waitingAsked: 12, options: { speed: { missing: [1, 2, 3] } } });
  await expect(page.locator('#playback-next')).toBeVisible(); await expect(page.locator('#playback-skip')).toBeHidden();
  await expect(page.locator('#upkeep-dialog')).toBeHidden(); await expect(page.locator('#help-dialog')).toBeHidden();
  await page.locator('#settings').click(); await page.selectOption('#playback-preference', 'step'); await page.getByRole('button', { name: 'Close settings' }).click();
  // From the windup's timer to the impact's, rather than every software frame between them (#749).
  await page.clock.fastForward(WINDUP_MS); await page.clock.fastForward(IMPACT_MS);
  await page.locator('#playback-next').click(); await expect(page.locator('#reading-title')).toHaveText('Round start');
  await expect(page.locator('#result-text')).toContainText('+5 energy');
  await expect(page.locator('#upkeep-dialog')).toBeHidden();
  await page.locator('#playback-next').click();
  await expect(page.locator('#help-title')).toHaveText('Choose how your team acts');
  // The tip read, the team's tempos open by themselves (ADR 0150) and acknowledge their asking; the last result is
  // still a tap away once they are closed.
  await page.locator('#help-close').click(); await expect(page.locator('#planning-dialog')).toHaveAttribute('data-kind', 'Speed');
  await page.locator('#planning-close').click(); await page.locator('#playback-result').click();
  await expect(page.locator('#result-text')).toContainText('base 3'); await expect.poll(() => h.state.shown).toEqual(['11', '12']); expect(h.state.errors).toEqual([]);
});
test('polls, hidden-tab reconnection and reload do not repeat the same receipt', async ({ page }) => {
  const h = await mount(page); await close(page); await h.publish([]);
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
  await expect(page.locator('#upkeep-body')).toBeEmpty();
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: false }); document.dispatchEvent(new Event('visibilitychange')); });
  await expect(page.locator('#loading')).toBeHidden(); await expect(page.locator('#battle')).toHaveAttribute('data-connection', 'live'); await expect(page.locator('#upkeep-dialog')).toBeHidden();
  h.state.view.feed = [energy(1), ongoing(1)]; await page.reload(); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#loading')).toBeHidden(); await expect(page.locator('#battle')).toHaveAttribute('data-connection', 'live');
  await expect(page.locator('#upkeep-dialog')).toBeHidden(); await page.locator('#planning-close').click();
  await h.publish([energy(2), ongoing(2)], { board: { ...h.state.view.board, roundNumber: 2 } });
  await expect(page.locator('#reading-title')).toHaveText('Round start'); await expect(page.locator('#round-number')).toHaveText('02');
  await expect(page.locator('#upkeep-dialog')).toBeHidden();
  // The tree opened once before the reload and once after; neither receipt acknowledged anything.
  await expect.poll(() => h.state.shown).toEqual(['11', '11']); expect(h.state.errors).toEqual([]);
});
test('a legacy receipt states missing ordinary energy alongside actual effects', async ({ page }) => {
  const h = await mount(page, { feed: [ongoing(1, true)] });
  await expect(page.locator('#upkeep-body')).toContainText('actual gain is unknown');
  await expect(page.locator('#upkeep-body')).toContainText('Ongoing damage −7');
  await expect(page.locator('#upkeep-body')).not.toContainText('99');
  await close(page);
  expect(h.state.errors).toEqual([]);
});
test('an existing draft delays the receipt and closing settings unblocks it without a write', async ({ page }) => {
  const h = await mount(page, { feed: [energy(1)] });
  await expect(page.locator('#planning-dialog')).toBeVisible(); await page.locator('[data-mutation="root"]').click();
  await h.publish([ongoing(1)]); await expect(page.locator('#upkeep-dialog')).toBeHidden();
  await expect(page.locator('#planning-selection')).toHaveText('Warden · Warden');
  await page.locator('#planning-close').click(); await expect(page.locator('#upkeep-dialog')).toBeVisible();
  await close(page); await page.locator('#settings').click();
  await h.publish([energy(2), ongoing(2)], { board: { ...h.state.view.board, roundNumber: 2 } });
  // The round arrives under the settings, not after them: under a dialog it settles instead of playing unseen.
  await expect(page.locator('#round-number')).toHaveText('02');
  await expect(page.locator('#upkeep-dialog')).toBeHidden(); await page.keyboard.press('Escape');
  await expect(page.locator('#upkeep-dialog')).toBeVisible(); expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
for (const reason of ['revoked', 'hidden', 'renderer']) test(`${reason} clears the receipt and inspection`, async ({ page }) => {
  const h = await mount(page); await expect(page.locator('#upkeep-dialog')).toBeVisible();
  await page.locator('#upkeep-details').click();
  await page.getByRole('button', { name: 'Inspect Warden · Your team', exact: true }).click();
  if (reason === 'revoked') { h.state.status = 403; await page.evaluate(() => window.notifyBattle()); }
  if (reason === 'hidden') await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
  if (reason === 'renderer') await page.locator('canvas').evaluate(node => node.dispatchEvent(new Event('webglcontextlost', { cancelable: true })));
  await expect(page.locator('#upkeep-dialog')).toBeHidden(); await expect(page.locator('#upkeep-body')).toBeEmpty();
  await expect(page.locator('#inspect-dialog')).toBeHidden(); expect(h.state.errors).toEqual([]);
});
test('Continue stays reachable in short landscape and at 200 percent text', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'Viewport sweep runs once.');
  const h = await mount(page, { feed: [energy(1), ongoing(1, true)] });
  await page.locator('#upkeep-details').click();
  for (const size of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(size); await page.clock.runFor(150); await fitsViewport(page, '#upkeep-close');
    await page.locator('#upkeep-body section').last().scrollIntoViewIfNeeded();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
  await page.setViewportSize({ width: 390, height: 844 }); await page.addStyleTag({ content: '.upkeep-scroll, .upkeep-rows button, .upkeep-rows p { font-size: 28px !important; }' });
  await fitsViewport(page, '#upkeep-close'); await page.locator('#upkeep-body li').last().scrollIntoViewIfNeeded();
  await expect(page.locator('#upkeep-body li').last()).toBeInViewport(); await close(page); expect(h.state.errors).toEqual([]);
});

test('connection loss settles an animation without opening the queued receipt offline', async ({ page }) => {
  const h = await mount(page, { round: 2, feed: [] }); await page.locator('#planning-close').click();
  const attack = { sequence: 25, event: { kind: 'CombatActionResolved', roundId: 2,
    resolution: { action: { actor: 4, spell: 'strike', targets: [1] } }, appliedOutcomes: [],
    frame: { before: roster, after: roster, timeline: [], rollOffs: [] } } };
  await h.publish([attack, energy(3), ongoing(3)], { board: { ...h.state.view.board, roundNumber: 3 } });
  await expect(page.locator('#playback-next')).toBeVisible();
  h.state.status = 500; await page.evaluate(() => window.notifyBattle());
  await expect(page.locator('#battle')).toHaveAttribute('data-connection', 'stale');
  await expect(page.locator('#playback-next')).toBeHidden(); await expect(page.locator('#upkeep-dialog')).toBeHidden();
  h.state.status = 200; await h.publish([]);
  await expect(page.locator('#upkeep-dialog')).toBeVisible(); await expect(page.locator('#upkeep-title')).toHaveText('Round 3 · Start');
  expect(h.state.writes).toEqual([]); await expect.poll(() => h.state.shown).toEqual(['11']); expect(h.state.errors).toEqual([]);
});

test('a newer round never replaces the receipt currently being read under History', async ({ page }) => {
  const h = await mount(page); await expect(page.locator('#upkeep-title')).toHaveText('Round 1 · Start');
  await page.locator('#upkeep-dialog [data-history-open]').click();
  await h.publish([energy(2), ongoing(2)], { board: { ...h.state.view.board, roundNumber: 2 } });
  await expect(page.locator('#upkeep-title')).toHaveText('Round 1 · Start');
  await page.locator('#history-close').click(); await close(page);
  await expect(page.locator('#upkeep-dialog')).toBeVisible(); await expect(page.locator('#upkeep-title')).toHaveText('Round 2 · Start');
  await close(page); await h.publish([]); await expect(page.locator('#upkeep-dialog')).toBeHidden();
  await expect.poll(() => h.state.shown).toEqual(['11']); expect(h.state.errors).toEqual([]);
});

// F3: the receipt says when the mutation round is, in one line under its summary (docs/art/journeys/match.md).
for (const [name, options, line] of [
  ['on a mutation round', { next: 1 }, 'This is a mutation round.'],
  ['on the round before one', { next: 2 }, 'Next round is a mutation round.'],
  ['on a round two before one', { next: 3 }, ''],
  ['when the host names no more mutation rounds', { next: null }, ''],
]) test(`the round's start receipt ${line ? `says "${line}"` : 'has no mutation line'} ${name}`, async ({ page }) => {
  const h = await mount(page, options);
  await expect(page.locator('#upkeep-dialog')).toBeVisible(); await expect(page.locator('#upkeep-title')).toHaveText('Round 1 · Start');
  await expect(page.locator('#upkeep-dialog')).toHaveAttribute('aria-describedby', 'upkeep-intro upkeep-mutation');
  if (line) { await expect(page.locator('#upkeep-mutation')).toBeVisible(); await expect(page.locator('#upkeep-mutation')).toHaveText(line); }
  else { await expect(page.locator('#upkeep-mutation')).toBeHidden(); await expect(page.locator('#upkeep-mutation')).toHaveText(''); }
  expect(h.state.errors).toEqual([]);
});

test('each round\'s receipt carries its own mutation line, never the previous one\'s', async ({ page }) => {
  const h = await mount(page, { next: 2 });
  await expect(page.locator('#upkeep-title')).toHaveText('Round 1 · Start'); await expect(page.locator('#upkeep-mutation')).toHaveText('Next round is a mutation round.');
  // Round 2 is a mutation round: while round 1's receipt is read, a newer round waits behind it (as under History).
  await h.publish([energy(2), ongoing(2)], { board: { ...h.state.view.board, roundNumber: 2, nextEvolutionRound: 2 } });
  await expect(page.locator('#upkeep-title')).toHaveText('Round 1 · Start'); await expect(page.locator('#upkeep-mutation')).toHaveText('Next round is a mutation round.');
  await close(page);
  await expect(page.locator('#upkeep-dialog')).toBeVisible(); await expect(page.locator('#upkeep-title')).toHaveText('Round 2 · Start');
  await expect(page.locator('#upkeep-mutation')).toBeVisible(); await expect(page.locator('#upkeep-mutation')).toHaveText('This is a mutation round.');
  await h.publish([energy(3), ongoing(3)], { board: { ...h.state.view.board, roundNumber: 3, nextEvolutionRound: 5 } });
  await close(page);
  await expect(page.locator('#upkeep-dialog')).toBeVisible(); await expect(page.locator('#upkeep-title')).toHaveText('Round 3 · Start');
  await expect(page.locator('#upkeep-mutation')).toBeHidden(); await expect(page.locator('#upkeep-mutation')).toHaveText('');
  expect(h.state.errors).toEqual([]);
});

test('the receipt with its mutation line keeps Continue in sight and its header inside the sheet at 320 by 568', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'Viewport sweep runs once.');
  const h = await mount(page, { next: 2 });
  await expect(page.locator('#upkeep-mutation')).toBeVisible();
  await page.setViewportSize({ width: 320, height: 568 }); await page.clock.runFor(150);
  await fitsViewport(page, '#upkeep-close');
  const fit = await page.evaluate(() => {
    const header = document.querySelector('.upkeep-header').getBoundingClientRect(), sheet = document.getElementById('upkeep-dialog').getBoundingClientRect();
    const header$ = document.querySelector('.upkeep-header');
    return { across: header$.scrollWidth <= header$.clientWidth + 1, inside: header.left >= sheet.left - 1 && header.right <= sheet.right + 1 && header.top >= sheet.top - 1 && header.bottom <= sheet.bottom + 1,
      page: document.documentElement.scrollWidth <= innerWidth };
  });
  expect(fit).toEqual({ across: true, inside: true, page: true });
  await expect(page.locator('#upkeep-mutation')).toBeInViewport(); expect(h.state.errors).toEqual([]);
});
