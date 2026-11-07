import { test, expect } from '@playwright/test';
import { fitsViewport } from './fit.js';
import { WINDUP_MS, IMPACT_MS } from '../../table/battle-timing.js';
const creature = (id, health = 20) => ({ id, name: `Creature ${id}`, health, maxHealth: 20, energy: 4,
  totalDefense: 2, isAlive: health > 0, knownSpells: ['strike'], conditions: [], acquiredTiers: [] });
const roster = [1, 2, 3, 4, 5, 6].map(id => creature(id));
const entry = (sequence, round = 1) => ({ sequence, event: { kind: 'CombatActionResolved', roundId: round,
  resolution: { action: { actor: 4, spell: 'strike', targets: [1] }, isCritical: true },
  appliedOutcomes: [{ kind: 'DamageOutcome', target: 1, amount: 7, critical: true }],
  frame: { before: roster, after: roster.map(one => creature(one.id, one.id === 1 ? 13 : 20)), timeline: [], rollOffs: [] } } });
async function mount(page, { waiting = null, mode = 'AtActivation', kind = 'Evolution' } = {}) {
  const state = { errors: [], writes: [], shown: [], status: 200, view: {
    board: { slot: 'Player1', roundNumber: 1, phase: 'Planning', subPhase: 'Evolution', nextEvolutionRound: 1, allies: roster.slice(0, 3), enemies: roster.slice(3), timeline: [] },
    waiting, waitingFor: kind, waitingAsked: 11, waitingCreature: 1,
    options: { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['root'] }] } },
    feed: [], feedNext: 0 } };
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
    // The host's stamp names the declaration only when it is not the hidden intent; `mode: null` serves it that way.
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { rules: { roundCap: 12, ...(mode ? { combatDeclaration: mode } : {}) }, cards: [{ id: 'strike', name: 'Strike', cost: 2 }], packages: [{ id: 'root', name: 'Warden', level: 1, spells: ['strike'] }] } });
    if (url.pathname === '/api/seat/player1') {
      if (url.searchParams.has('shown')) { state.shown.push(url.searchParams.get('shown')); if (state.ack) await state.ack; }
      return route.fulfill({ status: state.status, json: state.view });
    }
    return route.fulfill({ status: 500 });
  });
  await page.clock.pauseAt(new Date(Date.now() + 1000));
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#loading')).toBeHidden(); await expect(page.locator('#connection')).toHaveText('LIVE');
  // Discovery POSTs the saved capabilities to resume; measure guide writes after this baseline.
  state.writes.length = 0;
  const publish = async (feed, extra = {}) => {
    state.view = { ...state.view, ...extra, feed, feedNext: Math.max(state.view.feedNext, ...feed.map(one => one.sequence + 1)) };
    await page.evaluate(() => window.notifyBattle());
  };
  return { state, publish };
}
const close = page => page.locator('#help-close').click();
test('first mutation tip explains team picks, highlights Command and acknowledges nothing', async ({ page }, info) => {
  const h = await mount(page);
  await expect(page.locator('#help-dialog')).toBeVisible(); await expect(page.locator('#help-title')).toHaveText('What is a mutation?');
  await expect(page.locator('#help-objective')).toContainText('2 team picks left');
  await expect(page.locator('#help-explanation')).toContainText('all your unused picks');
  await expect(page.locator('#help-flow [aria-current]')).toContainText('Command');
  await expect(page.locator('#help-round')).toHaveText('Round 1 of 12');
  await expect(page.locator('#help-close')).toBeFocused(); await fitsViewport(page, '#help-close');
  await page.screenshot({ path: info.outputPath('mutation-guide.jpg'), type: 'jpeg', quality: 85 });
  expect(h.state.shown).toEqual([]); expect(h.state.writes).toEqual([]);
  // Closing the tip opens the tree it explained (ADR 0138), which acknowledges the asking and takes focus.
  await page.keyboard.press('Escape'); await expect(page.locator('#planning-dialog')).toBeVisible();
  await expect(page.locator('#planning-creatures [aria-pressed="true"]')).toBeFocused(); await expect.poll(() => h.state.shown).toEqual(['11']);
  await page.locator('#planning-close').click();
  await expect(page.locator('#instruction')).toContainText('Choose a creature on the battlefield'); expect(h.state.errors).toEqual([]);
});
test('waiting room including empty seats defers the first tip until actual arrival', async ({ page }) => {
  const h = await mount(page, { waiting: { seats: [] } });
  await expect(page.locator('#help-dialog')).toBeHidden(); await expect(page.locator('#instruction')).toContainText('Waiting for players');
  await page.locator('#help-open').click(); await expect(page.locator('#help-round')).toHaveText('Match not started');
  await close(page); await h.publish([], { waiting: null });
  await expect(page.locator('#help-title')).toHaveText('What is a mutation?'); await expect(page.locator('#help-dialog')).toBeVisible();
  expect(h.state.shown).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('dismissal survives polls, next creature and reload; a new subject gets its own tip', async ({ page }) => {
  const h = await mount(page); await close(page);
  await h.publish([], { waitingAsked: 12, waitingCreature: 2 }); await expect(page.locator('#help-dialog')).toBeHidden();
  await page.reload(); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#loading')).toBeHidden();
  await expect(page.locator('#help-dialog')).toBeHidden();
  await h.publish([], { waitingFor: 'Speed', waitingAsked: 13, options: { speed: { missing: [1, 2, 3] } } });
  await expect(page.locator('#help-dialog')).toBeVisible(); await expect(page.locator('#help-explanation')).toContainText('only Precision can crit');
  await close(page); await h.publish([], { waitingAsked: 14, waitingCreature: 3 });
  // Only the mutation tree acknowledged, on the Evolution askings it opened for; no tip and no tempo asking did.
  await expect(page.locator('#help-dialog')).toBeHidden(); expect(h.state.shown).toContain('12');
  expect(h.state.shown.filter(one => !['11', '12'].includes(one))).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('mute prevents future tips, keeps History, and Help reopens the current subject', async ({ page }) => {
  const h = await mount(page); await page.locator('#help-mute').check(); await close(page); await page.locator('#planning-close').click();
  await h.publish([entry(1)], { waitingFor: 'Speed', waitingAsked: 12, options: { speed: { missing: [1, 2, 3] } } });
  // An opponent's action is read with OK, never skipped (ADR 0139).
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '1'); await page.clock.runFor(WINDUP_MS + IMPACT_MS);
  await page.locator('#playback-next').click(); await expect(page.locator('#help-dialog')).toBeHidden();
  await page.locator('#history-open').click(); await expect(page.locator('#history-list details')).toHaveCount(1); await page.locator('#history-close').click();
  await page.locator('#help-open').click(); await expect(page.locator('#help-title')).toHaveText('Choose how your team acts');
  await expect(page.locator('#help-mute')).toBeChecked(); expect(h.state.shown).toEqual(['11']); expect(h.state.errors).toEqual([]);
});
test('manual Help preserves a mutation draft and fences a delayed acknowledgement', async ({ page }) => {
  const h = await mount(page); let release; h.state.ack = new Promise(resolve => { release = resolve; });
  await close(page); await page.locator('[data-mutation="root"]').click();
  await page.locator('#planning-confirm').click(); await page.locator('#planning-dialog [data-help-open]').click();
  await expect(page.locator('#planning-confirm')).toBeDisabled(); release();
  await expect.poll(() => h.state.shown.length).toBe(1); expect(h.state.writes).toEqual([]);
  await close(page); await expect(page.locator('#planning-selection')).toHaveText('Creature 1 · Warden');
  await expect(page.locator('#planning-confirm')).toBeEnabled();
  await expect(page.locator('#planning-dialog [data-help-open]')).toBeFocused(); expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
for (const [kind, phrase] of [['Action', 'No hidden spell declaration'], ['Intent', 'choose targets later'], ['Target', 'already declared']]) test(`${kind} teaches the actual declaration mode`, async ({ page }) => {
  const h = await mount(page, { kind, mode: kind === 'Action' ? 'AtActivation' : 'HiddenIntent' });
  await expect(page.locator('#help-dialog')).toBeVisible(); await expect(page.locator('#help-explanation')).toContainText(phrase);
  const flow = page.locator('#help-flow li').nth(2);
  if (kind === 'Action') await expect(flow).not.toContainText('declare hidden spells'); else await expect(flow).toContainText('declare hidden spells');
  expect(h.state.shown).toEqual([]); expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('a next-round tip waits for the closing action and never shows its budget on the old frame', async ({ page }) => {
  const h = await mount(page, { kind: null }); await expect(page.locator('#help-dialog')).toBeHidden();
  await h.publish([entry(1, 2)], { board: { ...h.state.view.board, roundNumber: 3, nextEvolutionRound: 3 }, waitingFor: 'Evolution', waitingAsked: 12 });
  await expect(page.locator('#playback-next')).toBeVisible(); await expect(page.locator('#playback-skip')).toBeHidden();
  await expect(page.locator('#help-dialog')).toBeHidden();
  await expect(page.locator('#instruction')).not.toContainText('team picks');
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '1'); await page.clock.runFor(WINDUP_MS + IMPACT_MS);
  await page.locator('#playback-next').click(); await expect(page.locator('#help-dialog')).toBeVisible();
  await expect(page.locator('#help-round')).toHaveText('Round 3 of 12'); expect(h.state.shown).toEqual([]); expect(h.state.errors).toEqual([]);
});
for (const reason of ['revoked', 'hidden']) test(`${reason} clears guide text and stale focus`, async ({ page }) => {
  const h = await mount(page); await expect(page.locator('#help-dialog')).toBeVisible();
  if (reason === 'revoked') { h.state.status = 403; await page.evaluate(() => window.notifyBattle()); }
  if (reason === 'hidden') await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
  await expect(page.locator('#help-dialog')).toBeHidden(); await expect(page.locator('#help-objective')).toBeEmpty();
  await expect(page.locator('#help-context')).toBeEmpty(); await expect(page.locator('#help-open')).toBeDisabled();
  expect(h.state.errors).toEqual([]);
});
test('tips, primary choices and round guide fit short portrait, landscape and 200% text', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'Viewport sweep runs once.');
  const h = await mount(page);
  for (const size of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(size); await page.clock.runFor(150); await fitsViewport(page, '#help-close');
    await close(page); if (await page.locator('#planning-dialog').isVisible()) await page.locator('#planning-close').click();
    await fitsViewport(page, '#help-open, #planning-open, #history-open');
    await page.locator('#help-open').click();
  }
  await page.setViewportSize({ width: 390, height: 844 }); await page.addStyleTag({ content: '.help-body, .help-body p, .help-footer label { font-size: 28px !important; }' });
  await fitsViewport(page, '#help-close, .help-footer label');
  expect(await page.locator('.help-body').evaluate(node => node.scrollHeight > node.clientHeight)).toBe(true);
  await page.locator('#help-round-guide summary').click(); await page.locator('#help-flow li').last().scrollIntoViewIfNeeded();
  await expect(page.locator('#help-flow li').last()).toBeInViewport();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await close(page); expect(h.state.errors).toEqual([]);
});

test('Help preserves a spell and selected field target without another acknowledgement', async ({ page }) => {
  const h = await mount(page); await close(page);
  await h.publish([], { board: { ...h.state.view.board, phase: 'Combat', subPhase: 'Activation' },
    waitingFor: 'Action', waitingAsked: 12, options: { action: { creature: 1,
      spells: [{ spell: 'strike', legalTargets: { candidates: [4, 5, 6], minTargets: 1, maxTargets: 1 } }] } } });
  await expect(page.locator('#help-dialog')).toBeVisible(); await close(page);
  await page.locator('#combat-open').click(); await page.locator('[data-spell="strike"]').click();
  await page.locator('[data-creature="4"]').click(); await expect(page.locator('#combat-confirm')).toBeEnabled();
  await page.locator('#help-open').click(); await expect(page.locator('#combat-confirm')).toBeDisabled();
  await h.publish([]); await close(page);
  await expect(page.locator('[data-creature="4"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('#combat-confirm')).toBeEnabled(); await expect(page.locator('#help-open')).toBeFocused();
  // Help adds no acknowledgement: the action's one is from opening combat. Whether the tree that opened after
  // the first tip acknowledged the Evolution asking before the next question replaced it is a race, not the subject.
  expect(h.state.shown.filter(one => one === '12')).toEqual(['12']); expect(h.state.shown.filter(one => !['11', '12'].includes(one))).toEqual([]);
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});

test('an unseen tip waits until the player closes another dialog', async ({ page }) => {
  const h = await mount(page); await close(page); await page.locator('#history-open').click();
  await h.publish([], { waitingFor: 'Speed', waitingAsked: 12, options: { speed: { missing: [1, 2, 3] } } });
  await expect(page.locator('#help-dialog')).toBeHidden(); await expect(page.locator('#history-dialog')).toBeVisible();
  await page.locator('#history-close').click(); await expect(page.locator('#help-dialog')).toBeVisible();
  await expect(page.locator('#help-title')).toHaveText('Choose how your team acts');
  expect(h.state.shown).toEqual(['11']); expect(h.state.errors).toEqual([]);
});

test('blocked preference storage keeps first-use help and in-memory dismissal working', async ({ page }) => {
  await page.addInitScript(() => {
    for (const method of ['getItem', 'setItem']) {
      const original = Storage.prototype[method];
      Storage.prototype[method] = function (key, ...rest) {
        if (key === 'downfall.arena.help.v1') throw new DOMException('Blocked', 'SecurityError');
        return original.call(this, key, ...rest);
      };
    }
  });
  const h = await mount(page); await expect(page.locator('#help-dialog')).toBeVisible(); await close(page);
  await h.publish([], { waitingAsked: 12 }); await expect(page.locator('#help-dialog')).toBeHidden();
  await page.locator('#help-open').click(); await expect(page.locator('#help-dialog')).toBeVisible();
  expect(h.state.shown).toContain('12'); expect(h.state.shown.filter(one => !['11', '12'].includes(one))).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('a hidden-intent table, whose catalogue names no declaration, teaches hidden spells from the first tip', async ({ page }) => {
  const h = await mount(page, { mode: null });
  await expect(page.locator('#help-title')).toHaveText('What is a mutation?');
  await expect(page.locator('#help-flow li').nth(2)).toContainText('declare hidden spells');
  await expect(page.locator('#help-dialog')).not.toContainText('does not supply');
  expect(h.state.errors).toEqual([]);
});
