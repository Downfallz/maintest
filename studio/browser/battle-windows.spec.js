// The match's windows as one family (UX12, docs/art/journeys/windows.md): each window a player opens takes the focus
// in, keeps it in while it is open, closes on Escape and gives the focus back to what opened it, or, when that is
// gone, to the control on the frame that reaches the window again. How each enters and leaves is
// motion-kit.spec.js's; what each window holds is its own spec's.
import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { openSpells, servePageArt } from './battle-spells-fixture.js';
import { returningBattlePlayer, withoutCanvas } from './battle-help-fixture.js';
import { WINDUP_MS, IMPACT_MS } from '../../table/battle-timing.js';
test.beforeEach(returningBattlePlayer);
// One seat of a game as the host serves it: `view` is the seat's view, read again when the host says it changed.
// `publish` changes the view and waits until a read that carried it was answered (#768, #785).
async function seat(page, { view, catalogue = { cards: [], packages: [] }, table = { id: 'a', format: '3v3' }, clock = true, art = false } = {}) {
  const state = { errors: [], view };
  page.on('pageerror', error => state.errors.push(error.message));
  if (clock) await page.clock.install();
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' }));
    const original = window.fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      const alive = setInterval(() => { try { controller.enqueue(new TextEncoder().encode(': keep-alive\n\n')); } catch { clearInterval(alive); } }, 20000);
      window.notifyBattle = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  });
  if (art) await servePageArt(page);
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/me') return route.fulfill({ json: { accounts: 'dev', account: null } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: catalogue });
    if (url.pathname === '/api/seat/player1/seen') return route.fulfill({ json: { seen: { sequence: 0, at: new Date().toISOString() } } });
    if (url.pathname === '/api/seat/player1') {
      // An acknowledgement reaches no board, so it never says a change arrived.
      const view = state.view; await route.fulfill({ json: view }); if (!url.searchParams.has('shown')) state.answered = view; return;
    }
    return route.fulfill({ status: 500 });
  });
  if (clock) await page.clock.pauseAt(await page.evaluate(() => Date.now() + 10000));
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#battle')).toHaveAttribute('data-connection', 'live');
  state.publish = async extra => {
    state.view = { ...state.view, ...extra }; const published = state.view;
    await page.evaluate(() => window.notifyBattle());
    await expect.poll(async () => { if (state.answered === published) return true; if (clock) await page.clock.runFor(0); return false; }).toBe(true);
  };
  return state;
}

const creature = (id, health = 20) => ({ id, name: `Creature ${id}`, health, maxHealth: 20, energy: 4,
  totalDefense: 2, currentInitiative: 5, isAlive: health > 0, isStunned: false, isStunImmune: false, knownSpells: ['strike'], conditions: [], acquiredTiers: [] });
const roster = [1, 2, 3, 4, 5, 6].map(id => creature(id));
const strike = { cards: [{ id: 'strike', name: 'Strike', cost: 2, visual: { portrait: 'strike' } }], packages: [{ id: 'root', name: 'Warden', level: 1, spells: ['strike'] }] };
const cast = (sequence, round = 1) => ({ sequence, event: { kind: 'CombatActionResolved', roundId: round,
  resolution: { action: { actor: 4, spell: 'strike', targets: [1] }, isCritical: false },
  appliedOutcomes: [{ kind: 'DamageOutcome', target: 1, amount: 5, critical: false }],
  frame: { before: roster, after: roster.map(one => creature(one.id, one.id === 1 ? 15 : 20)), timeline: [], rollOffs: [] } } });
const entry = cast(10);

// A seat in round 2's mutations, with a journal entry, so the bar's windows are all within reach.
async function mount(page) {
  const state = await seat(page, { art: true, catalogue: strike, view: {
    board: { slot: 'Player1', roundNumber: 2, phase: 'Planning', subPhase: 'Evolution', allies: roster.slice(0, 3), enemies: roster.slice(3), timeline: [] },
    waitingFor: 'Evolution', waitingAsked: 11, waitingCreature: 1,
    options: { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['root'] }] } },
    feed: [entry], feedNext: 11 } });
  // The mutation phase opens its sheet by itself (ADR 0138); the bar's windows are read with it put away.
  await expect(page.locator('#planning-dialog')).toBeVisible(); await page.locator('#planning-close').click();
  await expect(page.locator('#planning-dialog')).not.toHaveJSProperty('open', true);
  return state;
}

// A seat whose opponent's cast arrives to be read, as battle-history.spec.js plays it: the last result is then a window.
async function lastResult(page) {
  const state = await mount(page);
  await state.publish({ feed: [entry, cast(11, 2)], feedNext: 12 });
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '11');
  await page.clock.fastForward(WINDUP_MS); await page.clock.fastForward(IMPACT_MS);
  await page.locator('#playback-next').click(); await expect(page.locator('#playback-result')).toBeVisible();
  return state;
}

// A match over, as battle-services.spec.js serves it: its result opens by itself once and is put away here.
async function matchOver(page) {
  await withoutCanvas({ context: page.context() });
  const state = await seat(page, { clock: false, view: { over: true, recording: true, outcome: { winner: 'player1', reason: 'Elimination' },
    waitingFor: null, feed: [], feedNext: 0, board: { slot: 'Player1', roundNumber: 7, phase: 'Combat', subPhase: 'Activation',
      allies: [creature(1, 17), creature(2, 4), creature(3, 0)], enemies: [creature(4, 0), creature(5, 0), creature(6, 0)], timeline: [] } } });
  await expect(page.locator('#match-dialog')).toBeVisible(); await page.keyboard.press('Escape');
  await expect(page.locator('#match-dialog')).toBeHidden();
  return state;
}

// A seat back after hours away in round 2's combat, as battle-recap.spec.js serves it, against a named opponent with
// reactions on (battle-reactions.spec.js): the recap, the opponent's card and the reactions are all reached from it.
const hoursAgo = hours => new Date(Date.now() - hours * 3600000).toISOString();
async function away(page) {
  const timeline = roster.map(one => ({ creature: one.id, owner: one.id < 4 ? 'Player1' : 'Player2', speed: 'Standard' }));
  const state = await seat(page, { clock: false, catalogue: strike, table: { id: 'table-a', format: '3v3',
    seats: [{ seat: 'player1', kind: 'person', name: 'Mark' }, { seat: 'player2', kind: 'person', name: 'Bastien' }] }, view: {
    seat: 'player1', over: false, recording: true, reactions: { available: true, cooldownMs: 4000, recent: [] },
    board: { slot: 'Player1', roundNumber: 2, phase: 'Combat', subPhase: 'Activation', activationCursor: 3, allies: roster.slice(0, 3), enemies: roster.slice(3), timeline },
    waitingFor: 'Action', waitingAsked: 5, waitingCreature: 3, answerBy: hoursAgo(-20), lastMoveAt: hoursAgo(2.5),
    options: { action: { creature: 3, spells: [{ spell: 'strike', legalTargets: { candidates: [4, 5, 6], minTargets: 1, maxTargets: 1 } }] } },
    seen: { sequence: 4, at: hoursAgo(3) }, feed: [cast(1, 2), cast(3, 2), cast(4, 2)], feedNext: 5 } });
  // The spell choice opens by itself on the seat's question; the frame's windows are read with it put away.
  await expect(page.locator('#combat-dialog')).toBeVisible(); await page.locator('#combat-dialog-close').click();
  await expect(page.locator('#combat-dialog')).not.toHaveJSProperty('open', true);
  return state;
}

// A revealed turn order, as battle-order.spec.js serves it.
async function revealedOrder(page) {
  const slot = (id, speed = 'Quick', initiative = 5) => ({ creature: id, owner: id < 4 ? 'Player1' : 'Player2', speed, initiative });
  const timeline = [slot(1), slot(4), slot(2), slot(5, 'Standard', 3), slot(3, 'Standard', 3), slot(6, 'Standard', 1)];
  const rollOffs = [{ creature: 1, rolls: [11, 18] }, { creature: 4, rolls: [11, 8] }];
  return seat(page, { catalogue: { cards: [], packages: [], rules: { combatDeclaration: 'AtActivation' } }, view: {
    board: { slot: 'Player1', roundNumber: 2, phase: 'Combat', subPhase: 'Activation', activationCursor: 1, allies: roster.slice(0, 3), enemies: roster.slice(3), timeline, rollOffs },
    feed: [{ sequence: 10, event: { kind: 'TimelineBuilt', roundId: 2, timeline: { slots: timeline, rollOffs } } }], feedNext: 11, options: {} } });
}

// A round's upkeep, as battle-upkeep.spec.js serves it: its receipt opens by itself. No question waits behind it, so
// nothing opens when it closes and the focus is the receipt's to give back.
async function upkeep(page) {
  const grants = roster.map(one => ({ creature: one.id, baseRequested: 3, passiveRequested: 0, gained: 3, before: 2, after: 5 }));
  return seat(page, { catalogue: { ...strike, rules: { energyPerRound: 3, combatDeclaration: 'AtActivation' } }, view: {
    board: { slot: 'Player1', roundNumber: 1, phase: 'Planning', subPhase: 'Evolution', nextEvolutionRound: 1, allies: roster.slice(0, 3), enemies: roster.slice(3), timeline: [] },
    waitingFor: null, feed: [{ sequence: 10, round: 2, event: { kind: 'RoundEnergyGranted', roundId: 1, grants } },
      { sequence: 11, round: 2, event: { kind: 'OngoingEffectsApplied', roundId: 1 } }], feedNext: 12 } });
}

// A public mutation reveal, as battle-growth.spec.js publishes it: its receipt stands on the field and opens the window.
async function revealed(page) {
  const state = await mount(page);
  const allies = roster.slice(0, 3).map((one, at) => at === 0 ? { ...one, acquiredTiers: ['root'] } : one);
  await state.publish({ board: { ...state.view.board, subPhase: 'Speed', allies }, waitingFor: null, waitingAsked: null, options: {},
    feed: [entry, { sequence: 20, round: 2, event: { kind: 'PurchasesRevealed', roundId: 2, choices: [{ creature: 1, tier: 'root' }] } }], feedNext: 21 });
  await expect(page.locator('#growth-receipt')).toBeVisible();
  return state;
}

// A scenario's practice, as battle-services.spec.js opens it: it starts on its briefing, put away here.
async function practice(page) {
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  const view = { over: false, recording: false, waitingFor: null, feed: [], feedNext: 0, practice: { scenario: 'sc-1', title: 'Bleed after a stun', goal: 'Keep Creature 5 alive', round: 4 },
    board: { slot: 'Player2', roundNumber: 4, phase: 'Planning', subPhase: 'Speed', allies: [creature(4), creature(5, 9)], enemies: [creature(1, 18), creature(2, 12)], timeline: [], speedChoices: [] } };
  let opened = 0;
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url());
    if (url.pathname === '/api/scenarios/sc-1/practice') {
      if (request.method() === 'GET' && opened === 0) return route.fulfill({ status: 404, json: { error: 'Scenario.NoPractice', message: 'None.' } });
      if (request.method() === 'POST') opened++;
      return route.fulfill({ json: { seat: 'player2', token: `practice-${opened}`, table: { id: 'practice-sc-1', format: '2v2', seats: [{ seat: 'player2', name: 'Learner', kind: 'person' }, { seat: 'player1', name: 'Recorded opponent', kind: 'bot' }] }, practice: view.practice } });
    }
    if (url.pathname === '/api/me') return route.fulfill({ json: { accounts: 'dev', account: { id: 'acct_learnerlearnerlea', username: 'Learner' } } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [], packages: [] } });
    if (url.pathname === '/api/seat/player2') return route.fulfill({ json: view });
    if (url.pathname.endsWith('/events')) return route.fulfill({ status: 204 });
    return route.fulfill({ status: 404, json: {} });
  });
  await page.goto('/table/battle.html?scenario=sc-1');
  await expect(page.locator('#practice-brief')).toBeVisible({ timeout: 10000 });
  await page.locator('#practice-brief-begin').click(); await expect(page.locator('#practice-brief')).toBeHidden();
  return { errors };
}

// Tutorial 1's first step, as battle-tutorial.spec.js serves it: the briefing, then its concept cards before the spell
// choice. The cards are put away, so the coach card's own button opens one again.
const lesson = JSON.parse(readFileSync(new URL('../../scenarios/tutorials/your-first-combat.json', import.meta.url), 'utf8'));
async function tutorial(page) {
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  const id = 'sc-repo-tutorial-your-first-combat', step = lesson.tutorial.steps[0];
  const practice = { scenario: id, title: lesson.title, goal: lesson.goal, round: 2, tutorial: { order: 1, of: 4, title: lesson.tutorial.title,
    teaches: lesson.tutorial.teaches, assisted: ['Evolution', 'Speed', 'TieOrder', 'Intent', 'Target'], steps: lesson.tutorial.steps, shown: 1, done: false } };
  const view = { over: false, outcome: null, recording: false, feed: [], feedNext: 0, practice,
    board: { slot: 'Player1', roundNumber: 2, phase: 'Combat', subPhase: 'Activation', activationCursor: 0, allies: roster.slice(0, 3).map(one => ({ ...one, knownSpells: ['shock'] })),
      enemies: [creature(4, 9), creature(5), creature(6)], timeline: [{ creature: step.at.creature, owner: 'Player1', speed: 'Standard' }], speedChoices: [] },
    waitingFor: 'Action', waitingAsked: 20, waitingCreature: step.at.creature,
    options: { action: { actor: step.at.creature, spells: [{ spell: 'shock', legalTargets: { candidates: [4, 5, 6], minTargets: 1, maxTargets: 1 } }] } } };
  await page.addInitScript(() => {
    const original = window.fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      const alive = setInterval(() => { try { controller.enqueue(new TextEncoder().encode(': keep-alive\n\n')); } catch { clearInterval(alive); } }, 20000);
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  });
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url());
    if (url.pathname === `/api/scenarios/${id}/practice`) {
      if (request.method() === 'GET') return route.fulfill({ status: 404, json: { error: 'Scenario.NoPractice', message: 'None.' } });
      return route.fulfill({ json: { seat: 'player1', token: 'lesson', table: { id: 'practice-lesson-1', format: '3v3', seats: [{ seat: 'player1', name: 'Learner', kind: 'person' }, { seat: 'player2', name: 'Practice bot', kind: 'bot' }] }, practice } });
    }
    if (url.pathname === '/api/tutorials') return route.fulfill({ status: 404, json: {} });
    if (url.pathname === '/api/me') return route.fulfill({ json: { accounts: 'dev', account: { id: 'acct_learnerlearnerlea', username: 'Learner' } } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { rules: { roundCap: 12, combatDeclaration: 'AtActivation' }, cards: [{ id: 'shock', name: 'Shock', cost: 2, targeting: 'One enemy', effects: ['Damage 7'] }], packages: [] } });
    if (url.pathname === '/api/seat/player1') return route.fulfill({ json: view });
    return route.fulfill({ status: 404, json: {} });
  });
  await page.goto(`/table/battle.html?scenario=${id}`);
  await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true', { timeout: 20000 });
  await expect(page.locator('#practice-brief')).toBeVisible({ timeout: 10000 }); await page.locator('#practice-brief-begin').click();
  await expect(page.locator('#concept-dialog')).toBeVisible({ timeout: 15000 }); await page.keyboard.press('Escape');
  await expect(page.locator('#concept-dialog')).toBeHidden();
  await openSpells(page);
  return { errors };
}

const focusedIn = (page, id) => page.evaluate(id => document.getElementById(id).contains(document.activeElement), id);

// Each window by the way a player reaches it: the control pressed, and the one the focus comes back to.
const WINDOWS = [
  { id: 'settings-dialog', open: async page => page.locator('#settings'), back: '#settings' },
  { id: 'help-dialog', open: async page => page.locator('#help-open'), back: '#help-open' },
  { id: 'history-dialog', open: async page => page.locator('#history-open'), back: '#history-open' },
  // A window opened from another comes back to the control inside it, the first window still open.
  { id: 'history-dialog', from: 'help', open: async page => {
    await page.locator('#help-open').click(); await expect(page.locator('#help-dialog')).toHaveJSProperty('open', true);
    return page.locator('#help-dialog').getByRole('button', { name: 'History', exact: true });
  }, back: '#help-dialog [data-history-open]', still: 'help-dialog' },
  { id: 'inspect-dialog', open: async page => page.locator('#actor-layer [data-creature="4"]'), back: '#actor-layer [data-creature="4"]' },
  // The Mutation Tree opened from the inspection: the window on top comes later in the page than the one under it.
  { id: 'atlas-dialog', from: 'inspection', open: async page => {
    await page.locator('#actor-layer [data-creature="4"]').click(); await expect(page.locator('#inspect-dialog')).toHaveJSProperty('open', true);
    return page.locator('#inspect-growth');
  }, back: '#inspect-growth', still: 'inspect-dialog' },
  { id: 'atlas-dialog', open: async page => {
    await page.locator('#planning-open').click(); await expect(page.locator('#planning-dialog')).toHaveJSProperty('open', true);
    return page.locator('#mutation-atlas-open');
  }, back: '#mutation-atlas-open' },
  // Settings hands over to Concede and closes: the focus comes back to Settings on the frame.
  { id: 'concede-dialog', open: async page => {
    await page.locator('#settings').click(); await expect(page.locator('#settings-dialog')).toHaveJSProperty('open', true);
    return page.locator('#concede-open');
  }, back: '#settings' },
  // The feedback sheet, added by a script later, opens over Settings, which stays open under it.
  { id: 'feedback-dialog', from: 'settings', open: async page => {
    await page.locator('#settings').click(); await expect(page.locator('#settings-dialog')).toHaveJSProperty('open', true);
    return page.locator('#feedback-open-settings');
  }, back: '#feedback-open-settings', still: 'settings-dialog' },
  { id: 'result-dialog', mount: lastResult, open: async page => page.locator('#playback-result'), back: '#playback-result' },
  // History opened from the result comes back inside the result, which stays open.
  { id: 'history-dialog', from: 'the result', mount: lastResult, open: async page => {
    await page.locator('#playback-result').click(); await expect(page.locator('#result-dialog')).toHaveJSProperty('open', true);
    return page.locator('#result-dialog [data-history-open]');
  }, back: '#result-dialog [data-history-open]', still: 'result-dialog' },
  { id: 'match-dialog', mount: matchOver, open: async page => page.locator('#match-open'), back: '#match-open' },
  { id: 'recap-dialog', mount: away, open: async page => page.locator('#recap-open'), back: '#recap-open' },
  { id: 'opponent-dialog', mount: away, open: async page => page.locator('#opponent-open'), back: '#opponent-open' },
  // Under 360 px the reactions are a button of the opponent's card, which closes as they open (#767): the focus
  // comes back to the opponent on the frame, the next control the frame reaches them by.
  { id: 'reactions-dialog', mount: away, open: async page => {
    if (!await narrow(page)) return page.locator('#reactions-open');
    await page.locator('#opponent-open').click(); await expect(page.locator('#opponent-dialog')).toHaveJSProperty('open', true);
    return page.locator('#opponent-react');
  }, back: async page => await narrow(page) ? '#opponent-open' : '#reactions-open' },
  { id: 'order-dialog', mount: revealedOrder, open: async page => page.locator('#order-open'), back: '#order-open' },
  // The upkeep opens by itself, with no opener: the focus goes back to the frame's control that reaches it.
  { id: 'upkeep-dialog', mount: upkeep, open: null, back: '#recap-open' },
  { id: 'growth-dialog', mount: revealed, open: async page => page.locator('#growth-receipt [data-growth-open]'), back: '#growth-receipt [data-growth-open]' },
  { id: 'practice-brief', mount: practice, open: async page => page.locator('#practice-brief-open'), back: '#practice-brief-open' },
  // A concept card opened again from the coach card, beside the spell choice.
  { id: 'concept-dialog', mount: tutorial, open: async page => page.locator('#coach-cards [data-concept="health"]'), back: '#coach-cards [data-concept="health"]' },
];
const narrow = page => page.evaluate(() => matchMedia('(max-width: 359px)').matches);

for (const window of WINDOWS) {
  test(`${window.id}${window.from ? ` from ${window.from}` : ''}: the focus goes in, stays in, comes back, and Escape closes it`, async ({ page }) => {
    const { errors } = await (window.mount ?? mount)(page);
    const dialog = page.locator(`#${window.id}`);
    if (window.open) { const opener = await window.open(page); await opener.focus(); await page.keyboard.press('Enter'); }
    await expect(dialog).toHaveJSProperty('open', true);
    await expect.poll(() => focusedIn(page, window.id), 'the focus goes in').toBe(true);
    // Tab once round the window's controls comes back to the first; Shift+Tab from the first goes to the last.
    const ends = () => page.evaluate(id => {
      const all = [...document.getElementById(id).querySelectorAll('button, [href], input, select, textarea, summary, [tabindex]')]
        .filter(one => one.tabIndex >= 0 && !one.matches(':disabled') && one.checkVisibility({ visibilityProperty: true }));
      return { count: all.length, first: all[0] === document.activeElement };
    }, window.id);
    await page.evaluate(id => [...document.getElementById(id).querySelectorAll('button, [href], input, select, textarea, summary, [tabindex]')]
      .find(one => one.tabIndex >= 0 && !one.matches(':disabled') && one.checkVisibility({ visibilityProperty: true })).focus(), window.id);
    const { count } = await ends();
    expect(count).toBeGreaterThan(0);
    const seen = new Set();
    for (let step = 0; step < count; step++) {
      await page.keyboard.press('Tab');
      expect(await focusedIn(page, window.id), `the focus stays in after ${step + 1} presses`).toBe(true);
      seen.add(await page.evaluate(() => document.activeElement.outerHTML.slice(0, 120)));
    }
    expect(seen.size, 'each Tab moves the focus to another control').toBe(count);
    expect((await ends()).first, 'Tab past the last control comes back to the first').toBe(true);
    await page.keyboard.press('Shift+Tab');
    await page.keyboard.press('Tab');
    expect((await ends()).first, 'Shift+Tab from the first goes to the last').toBe(true);
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveJSProperty('open', false);
    await expect(page.locator(typeof window.back === 'function' ? await window.back(page) : window.back), 'the focus comes back').toBeFocused();
    if (window.still) await expect(page.locator(`#${window.still}`)).toHaveJSProperty('open', true);
    expect(errors).toEqual([]);
  });
}

test('a window whose opener went gives the focus to the frame\'s control that reaches it again', async ({ page }) => {
  const { errors } = await mount(page);
  await page.locator('#help-open').click(); await expect(page.locator('#help-dialog')).toHaveJSProperty('open', true);
  await page.locator('#help-dialog').getByRole('button', { name: 'History', exact: true }).click();
  await expect(page.locator('#history-dialog')).toHaveJSProperty('open', true);
  // Help closes under the journal, as a round that moves on closes its tip: the button that opened History is gone.
  await page.evaluate(() => document.getElementById('help-dialog').close());
  await expect(page.locator('#help-dialog')).toBeHidden();
  await page.keyboard.press('Escape');
  await expect(page.locator('#history-dialog')).toHaveJSProperty('open', false);
  await expect(page.locator('#history-open')).toBeFocused();
  expect(errors).toEqual([]);
});

// The windows' floor (UX12), as the frame's (UX11, battle-density.spec.js): no word a window shows under 11 px.
const under11 = (page, id) => page.evaluate(id => {
  const found = new Set(), walker = document.createTreeWalker(document.getElementById(id), NodeFilter.SHOW_TEXT);
  while (walker.nextNode()) {
    const text = walker.currentNode, parent = text.parentElement;
    if (!text.textContent.trim() || !parent.getClientRects().length) continue;
    if (parseFloat(getComputedStyle(parent).fontSize) < 11) found.add(text.textContent.trim());
  }
  return [...found];
}, id);

test('no word a window shows is under 11 px', async ({ page }) => {
  await mount(page);
  const read = {};
  for (const [id, opener] of [['help-dialog', '#help-open'], ['history-dialog', '#history-open'], ['settings-dialog', '#settings'],
    ['inspect-dialog', '#actor-layer [data-creature="4"]']]) {
    await page.locator(opener).click(); await expect(page.locator(`#${id}`)).toHaveJSProperty('open', true);
    read[id] = await under11(page, id);
    await page.keyboard.press('Escape'); await expect(page.locator(`#${id}`)).toBeHidden();
  }
  await page.locator('#planning-open').click(); await page.locator('#mutation-atlas-open').click();
  await page.locator('#atlas-dialog .atlas-node').first().click();
  read['atlas-dialog'] = await under11(page, 'atlas-dialog');
  expect(read).toEqual({ 'help-dialog': [], 'history-dialog': [], 'settings-dialog': [], 'inspect-dialog': [], 'atlas-dialog': [] });
});

// The floor raises what was under 11 px and leaves alone what was above it: a heading of the journal or the recap
// keeps its own size.
test('the floor shrinks nothing that was above 11 px', async ({ page }) => {
  await mount(page);
  const sizes = await page.evaluate(() => [['history-detail', null], ['history-dialog', 'history-creature'], ['recap-dialog', 'recap-section']].map(([host, wrap]) => {
    let parent = document.getElementById(host);
    if (wrap) { const box = document.createElement('div'); box.className = wrap; parent.append(box); parent = box; }
    const probe = document.createElement('h4'); probe.textContent = 'Probe'; parent.append(probe);
    const size = getComputedStyle(probe).fontSize; (wrap ? parent : probe).remove(); return size;
  }));
  expect(sizes).toEqual(['14px', '13px', '12px']);
});
