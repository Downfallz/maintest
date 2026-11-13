import { test, expect } from '@playwright/test';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningBattlePlayer);

// Reactions (ADR 0173): a fixed set of short messages, sent from the header and shown as a bubble on each side.
const creature = (id, health = 30) => ({ id, name: `Creature ${id}`, health, maxHealth: 30, energy: 2, totalDefense: 1,
  currentInitiative: 5, isAlive: health > 0, conditions: [], knownSpells: [], acquiredTiers: [] });
const reaction = (sequence, seat, name, at = new Date().toISOString()) => ({ sequence, seat, reaction: name, at });

async function mount(page, { available = true, recent = [], both = false, answer = null } = {}) {
  const state = { writes: [], errors: [], view: { over: false, recording: true, waitingFor: null, feed: [], feedNext: 0,
    reactions: { available, cooldownMs: 4000, recent },
    board: { slot: 'Player1', roundNumber: 3, phase: 'Planning', subPhase: 'Speed', allies: [creature(1), creature(2), creature(3)], enemies: [creature(4), creature(5), creature(6)], timeline: [] } } };
  page.on('pageerror', error => state.errors.push(error.message));
  await page.addInitScript(({ both }) => {
    localStorage.setItem('downfall.table.seats', JSON.stringify(both ? { player1: 'first', player2: 'second' } : { player1: 'first' }));
    const original = fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      // A host says it is still there every twenty seconds (ADR 0119); a silent stream is taken for a dropped one (ADR 0164).
      const alive = setInterval(() => { try { controller.enqueue(new TextEncoder().encode(': keep-alive\n\n')); } catch { clearInterval(alive); } }, 20000);
      window.notifyBattle = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  }, { both });
  await page.route('**/api/**', async route => {
    const req = route.request(), url = new URL(req.url());
    if (url.pathname.endsWith('/reaction')) {
      state.writes.push({ path: url.pathname, body: req.postDataJSON(), token: req.headers()['x-seat-token'] });
      return route.fulfill(answer ?? { json: { reaction: { sequence: 99, seat: 'player1', reaction: req.postDataJSON().reaction, at: new Date().toISOString() } } });
    }
    if (url.pathname === '/api/me') return route.fulfill({ json: { accounts: 'dev', account: null } });
    if (url.pathname.endsWith('/resume')) return route.fulfill({ json: { tables: [{ table: { id: 'one', format: '3v3',
      seats: [{ seat: 'player1', kind: 'person', name: 'Mark' }, { seat: 'player2', kind: 'person', name: 'Bastien' }] },
      seats: [{ seat: 'player1', token: 'first' }, ...(both ? [{ seat: 'player2', token: 'second' }] : [])] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [], packages: [] } });
    if (url.pathname === '/api/seat/player1') return route.fulfill({ json: state.view });
    return route.fulfill({ status: 404, json: {} });
  });
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  if (!both) await expect(page.locator('#loading')).toBeHidden();
  const publish = async recent => { state.view = { ...state.view, reactions: { ...state.view.reactions, recent } }; await page.evaluate(() => window.notifyBattle()); };
  return { state, publish };
}

test('a reaction is one tap from the header: the set opens, the choice is sent by name and shows on this side', async ({ page }, info) => {
  const h = await mount(page);

  await page.locator('#reactions-open').click();
  await expect(page.locator('#reactions-dialog')).toBeVisible();
  await expect(page.locator('#reactions-list button')).toHaveCount(8);
  await fitsViewport(page, '#reactions-list button, #reactions-close, .reactions-shown');
  await page.screenshot({ path: info.outputPath('reactions-set.png') });
  await page.getByRole('button', { name: 'Well played!' }).click();

  await expect(page.locator('#reactions-dialog')).toBeHidden();
  await expect(page.locator('#reaction-mine')).toContainText('Well played!');
  await expect(page.locator('#reactions-open')).toBeFocused();
  expect(h.state.writes).toEqual([{ path: '/api/seat/player1/reaction', body: { reaction: 'well-played' }, token: 'first' }]);
  await page.locator('#reactions-open').click();
  await expect(page.locator('#reactions-list button').first()).toBeDisabled();
  await expect(page.locator('#reactions-note')).toContainText('react again in a moment');
  expect(h.state.errors).toEqual([]);
});

test('the opponent\'s reaction shows by their side, named, and goes by itself', async ({ page }, info) => {
  await page.clock.install();
  const h = await mount(page);

  await h.publish([reaction(1, 'player2', 'nice-move')]);

  const bubble = page.locator('#reaction-theirs');
  await expect(bubble).toContainText('Nice move!');
  await expect(bubble).toHaveAttribute('aria-label', 'Bastien: Nice move!');
  await expect(bubble).toHaveAttribute('role', 'status');
  await page.screenshot({ path: info.outputPath('reaction-bubble.png') });
  await page.clock.runFor(5000);
  await expect(bubble).toBeHidden();
});

test('a reaction sent while the player was away is shown once when the game opens, with how long ago', async ({ page }) => {
  await mount(page, { recent: [reaction(4, 'player2', 'good-luck', new Date(Date.now() - 2 * 3600 * 1000).toISOString())] });

  await expect(page.locator('#reaction-theirs')).toContainText('Good luck!');
  await expect(page.locator('#reaction-theirs')).toContainText('2 h ago');
  await page.reload(); await expect(page.locator('#loading')).toBeHidden();
  await expect(page.locator('#reaction-theirs')).toBeHidden();
});

test('hiding the opponent\'s reactions for this game keeps the next ones off the field', async ({ page }) => {
  const h = await mount(page);
  await page.locator('#reactions-open').click();

  await page.locator('#reactions-shown').uncheck();
  await page.locator('#reactions-close').click();
  await h.publish([reaction(1, 'player2', 'oops')]);

  await expect(page.locator('#reaction-theirs')).toBeHidden();
  const kept = await page.evaluate(() => JSON.parse(localStorage.getItem('downfall.arena.game-overrides.v1'))[0]);
  expect(kept).toMatchObject({ game: 'one', reactions: false });
});

test('a reaction refused as too soon is taken back and says to wait', async ({ page }) => {
  await mount(page, { answer: { status: 429, json: { error: 'Reaction.TooSoon', message: 'Wait a moment before reacting again.', retryAfterMs: 2000 } } });

  await page.locator('#reactions-open').click(); await page.getByRole('button', { name: 'Hello!' }).click();

  await expect(page.locator('#reaction-mine')).toBeHidden();
  await page.locator('#reactions-open').click();
  await expect(page.locator('#reactions-note')).toHaveText('Wait a moment before reacting again.');
});

test('a table the host offers no reactions at has no reaction button', async ({ page }) => {
  await mount(page, { available: false, recent: [reaction(1, 'player2', 'hello')] });

  await expect(page.locator('#reactions-open')).toBeHidden();
  await expect(page.locator('#reaction-theirs')).toBeHidden();
});

test('a device holding both seats offers no reactions', async ({ page }) => {
  await mount(page, { both: true });
  await page.locator('#seat-choices button').first().click(); await expect(page.locator('#loading')).toBeHidden();

  await expect(page.locator('#reactions-open')).toBeHidden();
});
