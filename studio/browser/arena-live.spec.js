import { test, expect } from '@playwright/test';
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
  await expect(page.locator('#connection')).toHaveText('LIVE · READ ONLY'); await expect(page.locator('#loading')).toBeHidden();
}
async function fits(page) {
  const viewport = page.viewportSize();
  expect(await page.evaluate(() => [document.documentElement.scrollWidth, document.documentElement.scrollHeight])).toEqual([viewport.width, viewport.height]);
  for (const node of await page.locator('.creature, #switch-seat, #settings, #play-link').all()) {
    const box = await node.boundingBox();
    expect(box.width).toBeGreaterThanOrEqual(44); expect(box.height).toBeGreaterThanOrEqual(44);
    expect(box.x).toBeGreaterThanOrEqual(-1); expect(box.y).toBeGreaterThanOrEqual(-1);
    expect(box.x + box.width).toBeLessThanOrEqual(viewport.width + 1); expect(box.y + box.height).toBeLessThanOrEqual(viewport.height + 1);
  }
}
test('only the chosen viewpoint loads and the real host fields fit the viewport', async ({ page }, info) => {
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  const state = await mount(page);
  expect(state.requests.some(one => one.path.startsWith('/api/seat/'))).toBe(false);
  await choose(page, 2);
  await expect(page.locator('.creature')).toHaveCount(6);
  await expect(page.locator('[data-creature="4"]')).toHaveClass(/ally/); await expect(page.locator('[data-creature="1"]')).toHaveClass(/enemy/);
  await expect(page.locator('#round-number')).toHaveText('04'); await expect(page.locator('#actor-name')).toHaveText('Creature 2');
  await expect(page.locator('#timeline [aria-current="step"]')).toContainText('Creature 2');
  await page.locator('[data-creature="4"]').click();
  await expect(page.locator('#inspect-spells')).toHaveText('Host spell name');
  await expect(page.locator('#inspect-stats')).toContainText('21/30 HP · 5 energy · 2 defense');
  await page.keyboard.press('Escape'); await fits(page); await page.screenshot({ path: info.outputPath('live-board.png') });
  expect(state.requests.filter(one => one.path.startsWith('/api/seat/')).every(one => one.token === 'second' && one.path.includes('player2'))).toBe(true);
  expect(state.requests.some(one => one.query.includes('shown='))).toBe(false);
  expect(state.requests.filter(one => one.method !== 'GET').map(one => one.path)).toEqual(['/api/lobby/resume']); expect(errors).toEqual([]);
});
test('snapshots update stun, defeat, conditions and completion without acknowledging the question', async ({ page }) => {
  const state = await mount(page); await choose(page);
  state.first.board.allies[0] = creature(1, { health: 8, isStunned: true, conditions: [{ effect: { kind: 'Bleed', amountPerRound: 3 }, remainingRounds: 2, isFresh: true }] });
  state.first.board.enemies[0] = creature(4, { health: 0, isAlive: false }); state.first.board.activationCursor = 2;
  await expect(page.locator('[data-creature="1"] .creature-stats')).toContainText('8/30', { timeout: 10000 });
  await expect(page.locator('[data-creature="1"] .creature-tag')).toHaveText('STUNNED'); await expect(page.locator('[data-creature="4"] .creature-tag')).toHaveText('DEFEATED');
  await page.locator('[data-creature="1"]').click(); await expect(page.locator('#inspect-effects')).toContainText('Bleed 3 · new · first countdown pending'); await page.keyboard.press('Escape');
  state.first.over = true; state.first.outcome = { winner: 'Player1' }; await expect(page.locator('#actor-name')).toHaveText('Match complete', { timeout: 10000 });
  expect(state.requests.some(one => one.query === '?since=83')).toBe(true); expect(state.requests.some(one => one.query.includes('shown='))).toBe(false);
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
  await expect(page.locator('#round-number')).toHaveText('05', { timeout: 10000 }); await expect(page.locator('#connection')).toHaveText('LIVE · READ ONLY');
});
test('an invitation is reviewed at the table and never claimed here', async ({ page }) => {
  const state = await mount(page, { saved: false, invitation: true, search: '?player1=invite' });
  await expect(page.locator('#access-link')).toHaveText('Review invitation'); await expect(page.locator('#access-link')).toHaveAttribute('href', '/play?player1=invite');
  expect(state.requests.map(one => one.path)).toEqual(['/api/lobby/resume', '/api/lobby/invitations']);
});
test('sign-in retains the destination and blocked storage supports linked seats', async ({ page }) => {
  await page.addInitScript(() => Object.defineProperty(window, 'localStorage', { get() { throw new Error('blocked'); } }));
  const state = await mount(page, { saved: false, search: '?player1=first', resumeStatus: 401 });
  await expect(page.locator('#access-link')).toHaveText('Sign in'); await expect(page.locator('#access-link')).toHaveAttribute('href', /next=.*arena/);
  state.resumeStatus = 200; await page.locator('#retry').click(); await choose(page);
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
  await expect(page.locator('[data-creature="1"] .creature-stats')).toContainText('10/30'); await expect(page.locator('#connection')).toHaveText('LIVE · READ ONLY');
});

test('the live dock and all six inspection targets fit short portrait and landscape', async ({ page }) => {
  await mount(page); await choose(page);
  for (const viewport of [{ width: 320, height: 568 }, { width: 844, height: 390 }, { width: 900, height: 700 }]) {
    await page.setViewportSize(viewport);
    await expect.poll(async () => Math.round((await page.locator('#phaser-mount canvas').boundingBox()).width)).toBe(viewport.width);
    await fits(page);
  }
});
