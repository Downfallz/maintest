import { writeFile } from 'node:fs/promises';
import { test, expect } from '@playwright/test';
import { fitsViewport, fitsBattlefield } from './fit.js';
test.describe.configure({ mode: 'default' });
const creature = (id, extras = {}) => ({ id, name: `Creature ${id}`, health: 21, maxHealth: 30, energy: 5, totalDefense: 2,
  currentInitiative: 7, isAlive: true, isStunned: false, isStunImmune: false, knownSpells: ['spell:host:v1'], conditions: [], ...extras });
function view(seat = 'player1', size = 3) {
  const own = Array.from({ length: size }, (_, index) => creature(index + 1));
  const foes = Array.from({ length: size }, (_, index) => creature(index + 4));
  return { seat, board: { slot: seat === 'player1' ? 'Player1' : 'Player2', roundNumber: 4, phase: 'Combat', subPhase: 'Activation', activationCursor: 1,
    allies: seat === 'player1' ? own : foes, enemies: seat === 'player1' ? foes : own,
    timeline: [...own.map(one => ({ creature: one.id, owner: 'Player1', speed: 'Quick' })), ...foes.map(one => ({ creature: one.id, owner: 'Player2', speed: 'Standard' }))] },
    waitingFor: 'Action', waitingAsked: 11, feedNext: 83, feed: [{ sequence: 72 }], over: false };
}
async function mount(page, { size = 3, saved = true, search = '', resumeStatus = 200, invitation = false } = {}) {
  const state = { first: view('player1', size), second: view('player2', size), status: 200, requests: [], resumeStatus };
  if (saved) await page.addInitScript(() => localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'first', player2: 'second' })));
  await page.route('**/api/**', route => {
    const request = route.request(); const url = new URL(request.url());
    state.requests.push({ path: url.pathname, query: url.search, method: request.method(), token: request.headers()['x-seat-token'] });
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ status: state.resumeStatus, json: { tables: invitation ? [] : [{ table: { id: 'table-a', format: '3v3' }, seats: [{ seat: 'player1', token: 'first' }, { seat: 'player2', token: 'second' }] }] } });
    if (url.pathname === '/api/lobby/invitations') return route.fulfill({ json: { invitations: invitation ? [{ kind: 'invitation' }] : [] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [{ id: 'spell:host:v1', name: 'Host spell name' }] } });
    if (url.pathname.endsWith('/events')) return route.fulfill({ status: 200, contentType: 'text/event-stream', body: '' });
    if (/\/api\/seat\/player[12]$/.test(url.pathname)) return route.fulfill({ status: state.status, json: url.pathname.endsWith('player1') ? state.first : state.second });
    return route.fulfill({ status: 500, body: 'Unexpected request' });
  });
  await page.goto(`/table/arena.html${search}`);
  await expect(page.locator('#arena')).toHaveAttribute('data-ready', 'true');
  return state;
}
async function choose(page, seat = 1) {
  await page.locator('#seat-choices button').filter({ hasText: `Player ${seat}` }).click();
  await expect(page.locator('#connection')).toHaveText('LIVE'); await expect(page.locator('#loading')).toBeHidden();
}
const fits = page => fitsViewport(page, '.creature, #switch-seat, #settings, #play-link');
test('no seat is read before a viewpoint is chosen, and only the chosen one after', async ({ page }) => {
  const state = await mount(page);
  expect(state.requests.some(one => one.path.startsWith('/api/seat/'))).toBe(false);
  await choose(page, 2);
  const reads = state.requests.filter(one => one.path.startsWith('/api/seat/'));
  expect(reads.length).toBeGreaterThan(0);
  expect(reads.every(one => one.token === 'second' && one.path.includes('player2'))).toBe(true);
});

test('the board is drawn from the chosen seat: its side, its round, its cursor', async ({ page }) => {
  await mount(page); await choose(page, 2);
  await expect(page.locator('.creature')).toHaveCount(6);
  await expect(page.locator('[data-creature="4"]')).toHaveClass(/ally/); await expect(page.locator('[data-creature="1"]')).toHaveClass(/enemy/);
  await expect(page.locator('#round-number')).toHaveText('04'); await expect(page.locator('#actor-name')).toHaveText('Creature 2');
  await expect(page.locator('#timeline [aria-current="step"]')).toContainText('Creature 2');
});

test('inspecting a creature reads its stats and the catalogue\'s names for its spells', async ({ page }) => {
  await mount(page); await choose(page, 2);
  await page.locator('[data-creature="4"]').click();
  await expect(page.locator('#inspect-spells')).toHaveText('Host spell name');
  await expect(page.locator('#inspect-stats')).toContainText('21/30 HP · 5 energy · 2 defense');
});

test('the real host fields fit the viewport without a page error', async ({ page }, info) => {
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await mount(page); await choose(page, 2);
  await fits(page); await page.screenshot({ path: info.outputPath('live-board.png') });
  expect(errors).toEqual([]);
});

test('the preview writes nothing but the seat discovery, and never says a question was shown', async ({ page }) => {
  const state = await mount(page); await choose(page);
  await expect.poll(() => state.requests.filter(one => one.path.startsWith('/api/seat/')).length).toBeGreaterThan(0);
  expect(state.requests.some(one => one.query.includes('shown='))).toBe(false);
  expect(state.requests.filter(one => one.method !== 'GET').map(one => one.path)).toEqual(['/api/lobby/resume']);
});

test('a notification on the stream refreshes the board at once, ahead of the poll', async ({ page }) => {
  const state = await mount(page);
  // The stream stays open and silent until the test lets it speak: one `changed`, then it closes.
  let speak; const spoken = new Promise(resolve => { speak = resolve; });
  await page.route('**/events', async route => { await spoken; await route.fulfill({ status: 200, contentType: 'text/event-stream', body: 'event: changed\ndata: 2\n\n' }); });
  await choose(page);
  state.first.board.roundNumber = 9;
  speak();
  await expect(page.locator('#round-number')).toHaveText('09', { timeout: 2500 });
});

test('snapshots update stun, defeat and conditions', async ({ page }) => {
  const state = await mount(page); await choose(page);
  state.first.board.allies[0] = creature(1, { health: 8, isStunned: true, conditions: [{ effect: { kind: 'Bleed', amountPerRound: 3 }, remainingRounds: 2, isFresh: true }] });
  state.first.board.enemies[0] = creature(4, { health: 0, isAlive: false }); state.first.board.activationCursor = 2;
  await expect(page.locator('[data-creature="1"] .creature-stats')).toContainText('8/30', { timeout: 10000 });
  await expect(page.locator('[data-creature="1"] .creature-tag')).toHaveText('STUNNED'); await expect(page.locator('[data-creature="4"] .creature-tag')).toHaveText('DEFEATED');
  await page.locator('[data-creature="1"]').click(); await expect(page.locator('#inspect-effects')).toContainText('Bleed 3 · new · first countdown pending');
});

test('a finished match reads as complete, polled from the host\'s own cursor', async ({ page }) => {
  const state = await mount(page); await choose(page);
  state.first.over = true; state.first.outcome = { winner: 'Player1' };
  await expect(page.locator('#actor-name')).toHaveText('Match complete', { timeout: 10000 });
  expect(state.requests.some(one => one.query === '?since=83')).toBe(true);
});
for (const size of [1, 2]) test(`${size}v${size} keeps opponents across the field after rotation`, async ({ page }) => {
  await mount(page, { size }); await choose(page); await expect(page.locator('.creature')).toHaveCount(size * 2); await fits(page);
  await page.setViewportSize({ width: 844, height: 390 });
  await expect.poll(async () => (await page.locator('.creature.ally').first().boundingBox()).x).toBeLessThan(422); await fits(page);
});
test('switching viewpoint clears inspection before fetching another seat', async ({ page }) => {
  const state = await mount(page); await choose(page); await page.locator('[data-creature="1"]').click(); await page.keyboard.press('Escape');
  await page.locator('#switch-seat').click(); await expect(page.locator('.creature')).toHaveCount(0); await expect(page.locator('#inspect-name')).toBeEmpty();
  expect(state.requests.some(one => one.path.includes('player2'))).toBe(false); await choose(page, 2); await expect(page.locator('[data-creature="4"]')).toHaveClass(/ally/);
});
for (const status of [401, 403]) test(`${status} clears private display and offers recovery`, async ({ page }) => {
  const state = await mount(page); await choose(page); await page.locator('[data-creature="1"]').click(); state.status = status;
  await expect(page.locator('.creature')).toHaveCount(0, { timeout: 10000 }); await expect(page.locator('#inspect-dialog')).toBeHidden(); await expect(page.locator('#inspect-name')).toBeEmpty();
  await expect(page.locator('#timeline')).toBeEmpty(); await expect(page.locator('#play-link')).toBeHidden(); await expect(page.locator('#access-link')).toHaveText(status === 401 ? 'Sign in' : 'Open tables');
});
test('network failures mark the retained board stale and recover with a new snapshot', async ({ page }) => {
  const state = await mount(page); await choose(page); state.status = 503;
  await expect(page.locator('#arena')).toHaveAttribute('data-connection', 'stale', { timeout: 10000 }); await expect(page.locator('.creature')).toHaveCount(6);
  state.status = 200; state.first.board.roundNumber = 5;
  await expect(page.locator('#round-number')).toHaveText('05', { timeout: 10000 }); await expect(page.locator('#connection')).toHaveText('LIVE');
});
test('an invitation is reviewed at the table and never claimed here', async ({ page }) => {
  const state = await mount(page, { saved: false, invitation: true, search: '?player1=invite' });
  await expect(page.locator('#access-link')).toHaveText('Review invitation'); await expect(page.locator('#access-link')).toHaveAttribute('href', '/play?player1=invite');
  expect(state.requests.map(one => one.path)).toEqual(['/api/lobby/resume', '/api/lobby/invitations']);
});
test('a refused discovery sends the player to sign in, and back here afterwards', async ({ page }) => {
  await mount(page, { saved: false, search: '?player1=first', resumeStatus: 401 });
  await expect(page.locator('#access-link')).toHaveText('Sign in'); await expect(page.locator('#access-link')).toHaveAttribute('href', /next=.*arena/);
});

test('blocked storage still opens a linked seat, and the way back to the table keeps its token', async ({ page }) => {
  await page.addInitScript(() => Object.defineProperty(window, 'localStorage', { get() { throw new Error('blocked'); } }));
  await mount(page, { saved: false, search: '?player1=first' }); await choose(page);
  await expect(page.locator('#play-link')).toHaveAttribute('href', '/play?table=table-a&seat=player1&player1=first');
});
test('missing art retains usable labelled figures', async ({ page }) => {
  await page.route('**/arena-art/*', route => route.abort()); await mount(page); await choose(page);
  await expect(page.locator('#arena')).toHaveAttribute('data-asset-fallback', 'true'); await page.locator('[data-creature="1"]').click(); await expect(page.locator('#inspect-name')).toHaveText('Creature 1');
});

test('a hidden page clears inspection and reloads a fresh viewpoint on return', async ({ page }) => {
  const state = await mount(page); await choose(page); await page.locator('[data-creature="1"]').click();
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
  await expect(page.locator('.creature')).toHaveCount(0); await expect(page.locator('#inspect-dialog')).toBeHidden(); await expect(page.locator('#inspect-name')).toBeEmpty();
  state.first.board.allies[0].health = 10;
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: false }); document.dispatchEvent(new Event('visibilitychange')); });
  await expect(page.locator('[data-creature="1"] .creature-stats')).toContainText('10/30'); await expect(page.locator('#connection')).toHaveText('LIVE');
});

test('the live dock and all six inspection targets fit short portrait and landscape', async ({ page }) => {
  await mount(page); await choose(page);
  for (const viewport of [{ width: 320, height: 568 }, { width: 844, height: 390 }, { width: 900, height: 700 }]) {
    await page.setViewportSize(viewport);
    await expect.poll(async () => Math.round((await page.locator('#phaser-mount canvas').boundingBox()).width)).toBe(viewport.width);
    await fits(page);
  }
});

test('sound requires a gesture, mutes on hide and never submits a game command', async ({ page }) => {
  await page.addInitScript(() => {
    globalThis.audioCalls = [];
    globalThis.AudioContext = class {
      state = 'running';
      constructor() { globalThis.audioCalls.push('created'); }
      async resume() { globalThis.audioCalls.push('resumed'); }
    };
  });
  const state = await mount(page); await choose(page);
  expect(await page.evaluate(() => globalThis.audioCalls)).toEqual([]);
  await page.locator('#settings').click(); await expect(page.locator('#sound')).not.toBeChecked();
  await page.locator('#sound').check(); await expect(page.locator('#sound-status')).toContainText('Sound on');
  expect(await page.evaluate(() => globalThis.audioCalls)).toEqual(['created', 'resumed']);
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
  await expect(page.locator('#sound')).not.toBeChecked();
  expect(state.requests.filter(one => one.method !== 'GET').map(one => one.path)).toEqual(['/api/lobby/resume']);
});

test('a failed renderer retains a usable link to the same seat at the table', async ({ page }) => {
  await mount(page); await choose(page, 2);
  const fallback = await page.locator('#play-link').getAttribute('href');
  await page.locator('canvas').evaluate(canvas => canvas.dispatchEvent(new Event('webglcontextlost', { cancelable: true })));
  await expect(page.locator('#load-title')).toHaveText('The renderer was interrupted');
  await expect(page.locator('#access-link')).toHaveAttribute('href', fallback);
  await expect(page.locator('#access-link')).toHaveText('Continue at table');
  await fitsViewport(page, '#access-link, #retry');
});

test('keyboard inspection and settings return focus and fit a short viewport', async ({ page }, info) => {
  await mount(page); await choose(page);
  await page.setViewportSize({ width: 720, height: 500 }); // CSS viewport of a 1440×1000 desktop at 200% zoom.
  await fitsViewport(page, '.creature, #settings, #play-link'); await fitsBattlefield(page);
  const target = page.locator('.creature').first(); await target.focus(); await page.keyboard.press('Enter');
  await expect(page.locator('#inspect-dialog')).toBeVisible(); await page.keyboard.press('Escape'); await expect(target).toBeFocused();
  await page.locator('#settings').focus(); await page.keyboard.press('Enter');
  await page.locator('#sound').focus(); await page.keyboard.press('Space');
  await expect(page.locator('#sound')).toBeChecked(); await page.keyboard.press('Escape'); await expect(page.locator('#settings')).toBeFocused();
  await page.screenshot({ path: info.outputPath('arena-short-viewport.jpg'), type: 'jpeg', quality: 85 });
});

test('cold scene resources and sampled frame intervals are recorded for release review', async ({ page }, info) => {
  await mount(page); await choose(page);
  const metrics = await page.evaluate(async () => {
    const intervals = []; let previous = performance.now();
    await new Promise(resolve => {
      const sample = now => { intervals.push(now - previous); previous = now; if (intervals.length < 90) requestAnimationFrame(sample); else resolve(); };
      requestAnimationFrame(sample);
    });
    intervals.shift(); intervals.sort((a, b) => a - b);
    const resources = performance.getEntriesByType('resource').filter(entry => /arena-art|arena-build/.test(entry.name));
    return { userAgent: navigator.userAgent, viewport: [innerWidth, innerHeight], dpr: devicePixelRatio,
      p50FrameMs: intervals[Math.floor(intervals.length * .5)], p95FrameMs: intervals[Math.floor(intervals.length * .95)],
      resources: resources.map(entry => ({ file: new URL(entry.name).pathname, decodedBytes: entry.decodedBodySize, transferBytes: entry.transferSize, durationMs: entry.duration })) };
  });
  expect(metrics.resources.filter(entry => entry.file.endsWith('.webp'))).toHaveLength(2);
  expect(metrics.resources.reduce((sum, entry) => sum + entry.decodedBytes, 0)).toBeLessThan(4 * 1024 * 1024);
  await writeFile(info.outputPath('release-metrics.json'), JSON.stringify(metrics, null, 2));
  await info.attach('release-metrics', { body: JSON.stringify(metrics, null, 2), contentType: 'application/json' });
});
