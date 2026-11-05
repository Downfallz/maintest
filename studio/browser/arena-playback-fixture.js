import { expect } from '@playwright/test';
const creature = (id, health = 20) => ({ id, name: `Creature ${id}`, health, maxHealth: 20, energy: 5, totalDefense: 2,
  currentInitiative: 7, isAlive: health > 0, isStunned: false, isStunImmune: false, knownSpells: ['strike'], conditions: [] });
const before = [1, 2, 3, 4, 5, 6].map(id => creature(id));
const timeline = before.map(one => ({ creature: one.id, owner: one.id < 4 ? 'Player1' : 'Player2', speed: 'Standard' }));
const legalTargets = { candidates: [4, 5, 6], minTargets: 1, maxTargets: 1 };
function entry(sequence, { actor = 1, spell = 'strike', targets = [4], after = before.map(one => creature(one.id, one.id === 4 ? 13 : 20)), fizzled = false, legacy = false, outcomes } = {}) {
  return { sequence, event: { kind: 'CombatActionResolved', roundId: 2,
    resolution: { action: { actor, spell, targets }, isCritical: !fizzled, fizzled, fizzleReason: { message: 'Not enough energy' } },
    appliedOutcomes: outcomes ?? (fizzled ? [] : targets.map(target => ({ kind: 'DamageOutcome', target, amount: 7, critical: true }))),
    ...(legacy ? {} : { frame: { before, after, timeline, rollOffs: [] } }) } };
}
async function mount(page) {
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await page.clock.install();
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' }));
    const original = window.fetch;
    window.fetch = (url, options) => {
      if (String(url).endsWith('/events')) return Promise.resolve(new Response(new ReadableStream({ start(controller) {
        window.notifyArena = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
      } }), { headers: { 'Content-Type': 'text/event-stream' } }));
      return original(url, options);
    };
  });
  const state = { status: 200, requests: [], view: { board: { slot: 'Player1', roundNumber: 2, phase: 'Combat', subPhase: 'Activation',
    activationCursor: 1, allies: before.slice(0, 3), enemies: before.slice(3), timeline }, waitingFor: 'Action', waitingAsked: 5,
    waitingCreature: 2, options: { action: { creature: 2, spells: [{ spell: 'strike', legalTargets }] } }, feed: [], feedNext: 10 } };
  await page.route('**/api/**', route => {
    const request = route.request(), url = new URL(request.url()); state.requests.push({ path: url.pathname, query: url.search, method: request.method() });
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'table-a', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [{ id: 'strike', name: 'Strike', cost: 2 }, { id: 'spell:summon_minions:v1', name: 'Infestation', cost: 2, visual: { family: 'necromantic', motif: 'spirits', delivery: 'cast' } }], effects: [{ kind: 'Bleed', glyph: '♥', harmful: true }] } });
    if (url.pathname === '/api/seat/player1') return route.fulfill({ status: state.status, json: state.view });
    return route.fulfill({ status: 500, body: 'Unexpected request' });
  });
  await page.goto('/table/arena.html'); await expect(page.locator('#arena')).toHaveAttribute('data-ready', 'true');
  // Freeze before connecting: pausing a running poll can strand its owed zero-delay read, so the
  // next SSE notification never reaches the view until a test happens to advance the clock.
  await page.clock.pauseAt(new Date(await page.evaluate(() => Date.now() + 10000)));
  await page.locator('#seat-choices button').click(); await expect(page.locator('#connection')).toHaveText('LIVE');
  const publish = async (entries, extra = {}) => {
    state.view = { ...state.view, ...extra, feed: entries, feedNext: Math.max(state.view.feedNext, ...entries.map(one => one.sequence + 1)) };
    await page.evaluate(() => window.notifyArena());
  };
  return { state, publish, errors };
}

export { mount, entry, before, creature };
