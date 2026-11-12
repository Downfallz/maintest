import { writeFile } from 'node:fs/promises';
import { creatureAppearance } from '../../table/battle-art.js';
import { mountSpellScene, presentSpell } from './battle-spell-scene.js';
import { test, expect } from '@playwright/test';
import { readFileSync, readdirSync } from 'node:fs';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { fitsViewport, fitsBattlefield } from './fit.js';
test.beforeEach(returningBattlePlayer);

// The packages as the host serves them: the authored level and the mutation art the builder publishes.
const mutationArt = JSON.parse(readFileSync(new URL('../../data/presentation/mutation-art.json', import.meta.url), 'utf8'));
const tiersRoot = new URL('../../data/Tiers/', import.meta.url);
const packages = new Map(readdirSync(tiersRoot).filter(path => path.endsWith('.json')).map(path => JSON.parse(readFileSync(new URL(path, tiersRoot), 'utf8')))
  .map(tier => [tier.id, { id: tier.id, name: tier.name, level: tier.level, visual: mutationArt[tier.id] ?? null }]));
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
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [{ id: 'spell:host:v1', name: 'Host spell name' }], packages: [...packages.values()] } });
    if (url.pathname.endsWith('/events')) return route.fulfill({ status: 200, contentType: 'text/event-stream', body: 'event: ready\ndata: 0\n\n' });
    if (/\/api\/seat\/player[12]$/.test(url.pathname)) return route.fulfill({ status: state.status, json: url.pathname.endsWith('player1') ? state.first : state.second });
    return route.fulfill({ status: 500, body: 'Unexpected request' });
  });
  await page.goto(`/table/battle.html${search}`);
  await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  return state;
}
async function choose(page, seat = 1) {
  await page.locator('#seat-choices button').filter({ hasText: `Player ${seat}` }).click();
  await expect(page.locator('#connection')).toHaveText(/^LIVE/); await expect(page.locator('#loading')).toBeHidden();
}
const fits = page => fitsViewport(page, '.creature, #help-open, #settings');
test('a new player reads in Step at 1x with tips, and a change in Match settings lasts for that game alone', async ({ page }) => {
  // A new player: no game settings and no muted tips in this browser (ADR 0140).
  await page.addInitScript(() => { localStorage.removeItem('downfall.arena.game-settings.v1'); localStorage.removeItem('downfall.arena.help.v1'); });
  await mount(page); await choose(page);

  // Tips are on: the first one shows itself and is closed like any other.
  await expect(page.locator('#help-dialog')).toBeVisible(); await expect(page.locator('#help-mute')).not.toBeChecked();
  await page.locator('#help-close').click();
  await page.locator('#settings').click();
  await expect(page.locator('#playback-preference')).toHaveValue('step');
  await expect(page.locator('#playback-speed')).toHaveValue('1');
  await page.selectOption('#playback-preference', 'auto');
  await page.selectOption('#playback-speed', '2');
  await page.getByRole('button', { name: 'Close settings' }).click();

  const kept = () => page.evaluate(() => ({ globals: localStorage.getItem('downfall.arena.game-settings.v1'),
    game: JSON.parse(localStorage.getItem('downfall.arena.game-overrides.v1') ?? '[]').find(one => one.game === 'table-a') }));
  expect(await kept()).toEqual({ globals: null, game: { game: 'table-a', pace: 'auto', speed: 2, help: true, reducedMotion: false } });
  await page.reload(); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true'); await choose(page);
  await expect(page.locator('#playback-preference')).toHaveValue('auto');
  await expect(page.locator('#playback-speed')).toHaveValue('2');
});

test('a game first opened with the player settings keeps them when those settings change later', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('downfall.arena.game-settings.v1', sessionStorage.getItem('next-settings')
    ?? JSON.stringify({ pace: 'auto', speed: 1.5, help: false, reducedMotion: false })));
  await mount(page); await choose(page);
  await expect(page.locator('#playback-preference')).toHaveValue('auto');
  await expect.poll(() => page.evaluate(() => localStorage.getItem('downfall.arena.game-overrides.v1') ?? '')).toContain('"speed":1.5');

  await page.evaluate(() => sessionStorage.setItem('next-settings', JSON.stringify({ pace: 'step', speed: 0.5, help: true, reducedMotion: true })));
  await page.reload(); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true'); await choose(page);

  await expect(page.locator('#playback-preference')).toHaveValue('auto');
  await expect(page.locator('#playback-speed')).toHaveValue('1.5');
  await expect(page.locator('#reduce-motion')).not.toBeChecked();
});

test('no seat is read before a seat is chosen, and only the chosen one after', async ({ page }) => {
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

test('while the other side plays, the dock says whom it waits for, which creature is choosing and which of its own is next', async ({ page }, info) => {
  const state = await mount(page); Object.assign(state.second, { waitingFor: null, waitingAsked: null });
  await choose(page, 2);
  await expect(page.locator('#battle')).toHaveAttribute('data-awaiting', 'action');
  await expect(page.locator('#playback-pacing')).toHaveAttribute('data-kind', 'waiting'); await expect(page.locator('#playback-pacing')).toBeVisible();
  await expect(page.locator('#reading-kind')).toHaveText("Opponent's turn"); await expect(page.locator('#reading-title')).toHaveText('Waiting for your opponent');
  await expect(page.locator('#reading-detail')).toHaveText('Creature 2 is choosing its action · your Creature 4 plays next');
  await expect(page.locator('#reading-dots')).toBeVisible(); await expect(page.locator('.command-dock .actor-summary')).toBeHidden();
  await page.screenshot({ path: info.outputPath('waiting-on-opponent.png') });
  // Its own question puts the dock back to the command.
  Object.assign(state.second, { waitingFor: 'Action', waitingAsked: 12, board: { ...state.second.board, activationCursor: 3 } });
  await page.evaluate(() => window.dispatchEvent(new Event('online')));
  await page.reload(); await choose(page, 2);
  await expect(page.locator('#battle')).toHaveAttribute('data-awaiting', ''); await expect(page.locator('#playback-pacing')).toBeHidden();
});
test('the header names the opponent with its rank and friendship, and its card adds a friend', async ({ page }, info) => {
  const state = await mount(page); const asked = [];
  let friends = { friends: [], incoming: [], outgoing: [] };
  const seats = [{ seat: 'player1', kind: 'person', name: 'MarkF', account: 'acct_mark' }, { seat: 'player2', kind: 'person', name: 'bastien', account: 'acct_bastien' }];
  await page.route('**/api/lobby/resume', route => route.fulfill({ json: { tables: [{ table: { id: 'table-a', format: '3v3', seats }, seats: [{ seat: 'player1', token: 'first' }, { seat: 'player2', token: 'second' }] }] } }));
  await page.route('**/api/leaderboard', route => route.fulfill({ json: { players: [{ rank: 3, username: 'bastien', rating: 1532, wins: 5, losses: 4, draws: 1, provisional: false }] } }));
  await page.route('**/api/friends', route => route.fulfill({ json: friends }));
  await page.route('**/api/friends/requests', route => { asked.push(route.request().postDataJSON()); friends = { ...friends, outgoing: [{ id: 'acct_bastien' }] }; return route.fulfill({ json: { accepted: false } }); });
  await page.reload(); await choose(page, 1);
  const chip = page.locator('#opponent-open');
  await expect(chip).toBeVisible(); await expect(page.locator('#opponent-name')).toHaveText('bastien');
  await expect(page.locator('#opponent-badge')).toHaveText('#3'); await expect(page.locator('#opponent-heart')).toBeHidden();
  await expect(page.locator('#enemy-label')).toHaveText('OPPONENT · BASTIEN'); await expect(page.locator('#enemy-label')).toBeVisible();
  // On the round's line, never over the field, where it hid a creature in landscape.
  const [chipBox, roundBox, field] = await Promise.all(['#opponent-open', '#battle .round', '#battle-zone'].map(one => page.locator(one).boundingBox()));
  expect(chipBox.y + chipBox.height).toBeLessThanOrEqual(field.y); expect(Math.abs((chipBox.y + chipBox.height / 2) - (roundBox.y + roundBox.height / 2))).toBeLessThan(6);
  // A narrow header folds the chip to its medallion, the rank and the heart still on it; the field's label names them.
  await expect(page.locator('#opponent-name')).toBeVisible({ visible: page.viewportSize().width > 560 });
  expect(await page.locator('#battle .round').evaluate(node => node.scrollWidth <= node.clientWidth + 1)).toBe(true);
  await fitsViewport(page, '#opponent-open, #history-open, #help-open, #settings');
  await page.screenshot({ path: info.outputPath('opponent-header.png') });
  await chip.click(); await expect(page.locator('#opponent-dialog')).toBeVisible();
  await expect(page.locator('#opponent-rank')).toHaveText('#3 · 1532 rating · 5W 4L 1D');
  await page.screenshot({ path: info.outputPath('opponent-card.png') });
  await page.locator('#opponent-friend').click();
  await expect(page.locator('#opponent-note')).toHaveText('Friend request sent'); await expect(page.locator('#opponent-friend')).toBeHidden();
  expect(asked).toEqual([{ username: 'bastien' }]);
  await page.keyboard.press('Escape'); await expect(chip).toBeFocused();
  // A friend wears a heart in the header.
  friends = { friends: [{ id: 'acct_bastien' }], incoming: [], outgoing: [] };
  await chip.click(); await expect(page.locator('#opponent-note')).toHaveText('Your friend');
  await page.keyboard.press('Escape'); await expect(page.locator('#opponent-heart')).toBeVisible();
  expect(state.requests.filter(one => one.path.endsWith('/decision'))).toEqual([]);
});
test('a bot opponent is named as a bot, with no rank and no friend action', async ({ page }) => {
  await mount(page);
  await page.route('**/api/lobby/resume', route => route.fulfill({ json: { tables: [{ table: { id: 'table-a', format: '3v3',
    seats: [{ seat: 'player1', kind: 'person', name: 'MarkF', account: 'acct_mark' }, { seat: 'player2', kind: 'bot', name: 'Lookahead' }] }, seats: [{ seat: 'player1', token: 'first' }] }] } }));
  await page.reload(); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#opponent-name')).toHaveText('Lookahead'); await expect(page.locator('#opponent-badge')).toHaveText('BOT');
  await page.locator('#opponent-open').click();
  await expect(page.locator('#opponent-kind')).toHaveText('OPPONENT · BOT'); await expect(page.locator('#opponent-rank')).toHaveText('A bot · bots are not ranked');
  await expect(page.locator('#opponent-friend')).toBeHidden();
});
test('an opponent who takes the open seat stays named when an older read of the ladder answers last', async ({ page }) => {
  const state = await mount(page); state.first.waiting = true;
  let seats = [{ seat: 'player1', kind: 'person', name: 'MarkF', account: 'acct_mark' }, { seat: 'player2', kind: 'open' }];
  const held = [];
  await page.route('**/api/lobby/resume', route => route.fulfill({ json: { tables: [{ table: { id: 'table-a', format: '3v3', seats }, seats: [{ seat: 'player1', token: 'first' }] }] } }));
  // The read made when the seat is chosen hangs on the ladder until the match has begun.
  await page.route('**/api/leaderboard', route => { if (held.length === 0) held.push(route); else return route.fulfill({ json: { players: [] } }); });
  await page.route('**/api/friends', route => route.fulfill({ json: { friends: [], incoming: [], outgoing: [] } }));
  await page.reload(); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#connection')).toHaveText(/^LIVE/); await expect(page.locator('#loading')).toBeHidden();
  await expect(page.locator('#opponent-name')).toHaveText('Open seat'); await expect.poll(() => held.length).toBe(1);
  seats = [seats[0], { seat: 'player2', kind: 'person', name: 'bastien', account: 'acct_bastien' }]; state.first.waiting = false;
  await expect(page.locator('#opponent-name')).toHaveText('bastien', { timeout: 15000 });
  await held[0].fulfill({ json: { players: [] } });
  await page.evaluate(() => new Promise(resolve => setTimeout(resolve, 200)));
  await expect(page.locator('#opponent-name')).toHaveText('bastien');
  await page.locator('#opponent-open').click(); await expect(page.locator('#opponent-rank')).toHaveText(/Unranked/);
});
test('inspecting a creature reads its stats and the catalogue\'s names for its spells', async ({ page }) => {
  await mount(page); await choose(page, 2);
  await page.locator('[data-creature="4"]').click();
  await expect(page.locator('#inspect-spells .spell-card-name')).toHaveText('Host spell name');
  await expect(page.locator('#inspect-stats')).toContainText('21/30 HP · 5 energy · 2 defense');
});

test('the real host fields fit the viewport without a page error', async ({ page }, info) => {
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await mount(page); await choose(page, 2);
  await fits(page); await page.screenshot({ path: info.outputPath('live-board.png') });
  expect(errors).toEqual([]);
});

test('the page writes nothing but the seat discovery, and says the question was shown once, as its choice opens', async ({ page }) => {
  const state = await mount(page); await choose(page);
  await expect.poll(() => state.requests.filter(one => one.path.startsWith('/api/seat/')).length).toBeGreaterThan(0);
  // The spell choice opens by itself on the player's turn, which is the one acknowledgement (ADR 0149).
  await expect(page.locator('#combat-dialog')).toBeVisible();
  await expect.poll(() => state.requests.filter(one => one.query.includes('shown=')).map(one => one.query)).toEqual(['?since=83&shown=11']);
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
for (const status of [401, 403]) test(`${status} clears private display and offers recovery`, async ({ page }) => {
  const state = await mount(page); await choose(page); await page.locator('[data-creature="1"]').click(); state.status = status;
  await expect(page.locator('.creature')).toHaveCount(0, { timeout: 10000 }); await expect(page.locator('#inspect-dialog')).toBeHidden(); await expect(page.locator('#inspect-name')).toBeEmpty();
  await expect(page.locator('#timeline')).toBeEmpty(); await expect(page.locator('#access-link')).toHaveText(status === 401 ? 'Sign in' : 'Lobby');
});
test('network failures mark the retained board stale and recover with a new snapshot', async ({ page }) => {
  const state = await mount(page); await choose(page); state.status = 503;
  await expect(page.locator('#battle')).toHaveAttribute('data-connection', 'stale', { timeout: 10000 }); await expect(page.locator('.creature')).toHaveCount(6);
  state.status = 200; state.first.board.roundNumber = 5;
  await expect(page.locator('#round-number')).toHaveText('05', { timeout: 10000 }); await expect(page.locator('#connection')).toHaveText(/^LIVE/);
});
test('an invitation is reviewed at the table and never claimed here', async ({ page }) => {
  const state = await mount(page, { saved: false, invitation: true, search: '?player1=invite' });
  await expect(page.locator('#access-link')).toHaveText('Review invitation'); await expect(page.locator('#access-link')).toHaveAttribute('href', '/play?player1=invite');
  // The player's game settings (ADR 0140) may be read as on every page; the invitation is not.
  expect(state.requests.map(one => one.path).filter(path => path !== '/api/account/preferences').sort()).toEqual(['/api/lobby/invitations', '/api/lobby/resume', '/api/me']);
});
test('a refused discovery sends the player to sign in, and back here afterwards', async ({ page }) => {
  await mount(page, { saved: false, search: '?player1=first', resumeStatus: 401 });
  await expect(page.locator('#access-link')).toHaveText('Sign in'); await expect(page.locator('#access-link')).toHaveAttribute('href', /next=.*battle/);
});

test('blocked storage still opens a linked seat, and an interrupted renderer offers the lobby', async ({ page }) => {
  await page.addInitScript(() => Object.defineProperty(window, 'localStorage', { get() { throw new Error('blocked'); } }));
  await mount(page, { saved: false, search: '?player1=first' }); await choose(page);
  await page.locator('canvas').evaluate(canvas => canvas.dispatchEvent(new Event('webglcontextlost', { cancelable: true })));
  await expect(page.locator('#access-link')).toHaveAttribute('href', '/');
});
test('missing art retains usable labelled figures', async ({ page }) => {
  await page.route('**/battle-art/*', route => route.abort()); await mount(page); await choose(page);
  await expect(page.locator('#battle')).toHaveAttribute('data-asset-fallback', 'true'); await page.locator('[data-creature="1"]').click(); await expect(page.locator('#inspect-name')).toHaveText('Creature 1');
});

test('a hidden page clears inspection and reloads a fresh viewpoint on return', async ({ page }) => {
  const state = await mount(page); await choose(page); await page.locator('[data-creature="1"]').click();
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
  await expect(page.locator('.creature')).toHaveCount(0); await expect(page.locator('#inspect-dialog')).toBeHidden(); await expect(page.locator('#inspect-name')).toBeEmpty();
  state.first.board.allies[0].health = 10;
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: false }); document.dispatchEvent(new Event('visibilitychange')); });
  await expect(page.locator('[data-creature="1"] .creature-stats')).toContainText('10/30'); await expect(page.locator('#connection')).toHaveText(/^LIVE/);
});

test('the live dock and all six inspection targets fit short portrait and landscape', async ({ page }) => {
  // The dock shows once the self-opened spell choice is closed (ADR 0149).
  await mount(page); await choose(page); await page.locator('#combat-dialog-close').click();
  for (const viewport of [{ width: 320, height: 568 }, { width: 844, height: 390 }, { width: 900, height: 700 }]) {
    await page.setViewportSize(viewport);
    await expect.poll(async () => Math.round((await page.locator('#phaser-mount canvas').boundingBox()).width)).toBe(viewport.width);
    await fits(page);
  }
});


test('each creature shows the body its acquired packages give it, mixed builds included', async ({ page }, info) => {
  const state = await mount(page);
  state.first.board.allies[0].acquiredTiers = ['tier:brute:v1'];
  state.first.board.allies[1].acquiredTiers = ['tier:prowler:v1'];
  state.first.board.allies[2].acquiredTiers = ['tier:occultist:v1'];
  state.first.board.enemies[1].acquiredTiers = ['tier:prowler:v1'];
  state.first.board.enemies[2].acquiredTiers = ['tier:occultist:v1', 'tier:brute:v1'];
  await choose(page);
  for (const [id, form] of [[1, 'brute'], [2, 'predator'], [3, 'warped'], [4, 'base-creature'], [5, 'predator'], [6, 'brute']]) {
    await expect(page.locator(`[data-creature="${id}"]`)).toHaveAttribute('data-form', form);
  }
  await expect(page.locator('#battle')).toHaveAttribute('data-asset-fallback', 'false');
  await fits(page);
  await page.screenshot({ path: info.outputPath('art3-six-creatures.png') });
  await page.setViewportSize({ width: 844, height: 390 });
  await fits(page);
  await page.screenshot({ path: info.outputPath('art3-landscape.png') });
  state.first.board.allies[0].acquiredTiers = ['tier:brute:v1', 'tier:prowler:v1', 'tier:plague_doctor:v1'];
  await expect(page.locator('[data-creature="1"]')).toHaveAttribute('data-form', 'predator', { timeout: 10000 });
  await page.locator('[data-creature="1"]').click();
  await expect(page.locator('#inspect-name')).toHaveText('Creature 1');
  await expect(page.locator('#inspect-appearance')).toHaveText(`Appearance: Predator body · ${packages.get('tier:plague_doctor:v1').name} (stage 2)`);
});

test('ART-3 missing individual sprite preserves inspection and healthy assets', async ({ page }) => {
  const loaded = [];
  page.on('response', response => { if (response.url().includes('/battle-art/') && response.ok()) loaded.push(response.url()); });
  await page.route('**/battle-art/brute.webp', route => route.abort());
  const state = await mount(page);
  state.first.board.allies[0].acquiredTiers = ['tier:brute:v1'];
  await choose(page);
  await expect(page.locator('#battle')).toHaveAttribute('data-asset-fallback', 'true');
  expect(loaded.some(url => url.endsWith('/base-creature.webp'))).toBe(true);
  expect(loaded.some(url => url.endsWith('/predator.webp'))).toBe(true);
  await page.locator('[data-creature="1"]').click();
  await expect(page.locator('#inspect-name')).toHaveText('Creature 1');
});

const MOTIFS = { brute: ['fissures', 'plates', 'crown'], prowler: ['talons', 'fangs', 'glands'], occultist: ['shards', 'souls', 'astral'] };
const mutated = (id, names, enemy = false) => ({ id: String(id), isAlive: true, enemy,
  ...creatureAppearance({ acquiredTiers: names.map(name => `tier:${name}:v1`) }, packages) });
const mutationFrame = creatures => ({ stage: 'decision', target: null, actor: null, targets: [], creatures });
const drawnMutations = page => page.evaluate(() => window.artScene.children.list
  .filter(child => child.name === 'mutation-cue').map(child => ({ ...child.data.getAll(),
    x: child.x, y: child.y, scaleX: child.scaleX, scaleY: child.scaleY, alpha: child.alpha,
    visible: child.visible, commands: child.commandBuffer.length })).sort((a, b) => Number(a.creature) - Number(b.creature)
      || ['body', 'accent', 'capstone'].indexOf(a.slot) - ['body', 'accent', 'capstone'].indexOf(b.slot)));

test('each branch draws its own motif in the body slot, denser and mirrored on the enemy side at stage three', async ({ page }, info) => {
  test.setTimeout(60000);
  await mountSpellScene(page);
  const families = [
    ['brute', ['berserker', 'ravager'], ['ironbound', 'dreadnought'], ['marauder', 'warmonger']],
    ['prowler', ['assassin', 'deathstalker'], ['parasite', 'soulreaver'], ['plague_doctor', 'blightweaver']],
    ['occultist', ['elementalist', 'harbinger'], ['necromancer', 'lich'], ['shaman', 'spiritcaller']],
  ];
  for (const [root, ...branches] of families) {
    const creatures = branches.map(([second], i) => mutated(i + 1, [root, second]))
      .concat(branches.map(([second, third], i) => mutated(i + 4, [root, second, third], true)));
    await presentSpell(page, mutationFrame(creatures), 'decision', true);
    await page.clock.runFor(40);
    const cues = await drawnMutations(page);
    expect(cues.map(cue => [cue.creature, cue.slot, cue.motif])).toEqual([1, 2, 3, 4, 5, 6].map(id => [String(id), 'body', MOTIFS[root][(id - 1) % 3]]));
    expect(cues.every(cue => cue.visible && cue.commands > 0 && Number.isFinite(cue.x))).toBe(true);
    expect(cues.slice(0, 3).every(cue => cue.stage === 2 && cue.scaleX > 0)).toBe(true);
    expect(cues.slice(3).every(cue => cue.stage === 3 && cue.scaleX < 0)).toBe(true);
    // A larger normalized accent and extra geometry distinguish the evolved form.
    for (let i = 0; i < 3; i++) expect(cues[i + 3].commands).toBeGreaterThan(cues[i].commands);
    await page.screenshot({ path: info.outputPath(`art5-${root}-stages.png`) });
  }
});

const mixedPair = () => [
  mutated(1, ['brute', 'ironbound', 'dreadnought', 'titan', 'prowler', 'plague_doctor']),
  mutated(2, ['occultist', 'necromancer', 'lich', 'archmage'], true),
];
const strikeFrame = creatures => ({ ...mutationFrame(creatures), actor: '1', target: '2',
  cue: { kind: 'strike', struck: ['2'], effects: [], recipients: ['2'], contact: ['2'], changes: [],
    visual: { family: 'physical', motif: 'impact', delivery: 'melee' } } });
async function mountedPair(page) {
  await mountSpellScene(page); const frame = strikeFrame(mixedPair());
  await presentSpell(page, frame, 'decision'); return frame;
}
test('a mixed build fills its three slots and a capstone stays on the creature that owns it', async ({ page }) => {
  await mountedPair(page);
  const cues = await drawnMutations(page);
  expect(cues.map(cue => [cue.creature, cue.slot, cue.motif])).toEqual([['1', 'body', 'plates'], ['1', 'accent', 'glands'], ['1', 'capstone', 'reinforcement'], ['2', 'body', 'souls'], ['2', 'capstone', 'circuit']]);
});
test('the layers move with their figure during a lunge', async ({ page }) => {
  const frame = await mountedPair(page); const initial = await drawnMutations(page);
  await presentSpell(page, frame, 'windup'); await page.clock.runFor(110);
  const moved = await drawnMutations(page);
  expect(Math.abs(moved[0].x - initial[0].x) + Math.abs(moved[0].y - initial[0].y)).toBeGreaterThan(1);
});
test('a resize keeps every layer on its figure', async ({ page }) => {
  await mountedPair(page);
  await page.setViewportSize({ width: 844, height: 390 }); await page.clock.runFor(100);
  expect(await drawnMutations(page)).toHaveLength(5);
});
test('a defeated creature fades its layers with its image', async ({ page }) => {
  const frame = await mountedPair(page);
  await presentSpell(page, { ...frame, creatures: frame.creatures.map(one => ({ ...one, isAlive: false })) }, 'decision', true);
  expect((await drawnMutations(page)).every(cue => Math.abs(cue.alpha - .16) < .001)).toBe(true);
});
test('a frame whose creatures have no later packages destroys the layers of the frame before', async ({ page }) => {
  await mountedPair(page);
  await presentSpell(page, mutationFrame([mutated(1, ['brute'])]), 'decision', true);
  expect(await drawnMutations(page)).toEqual([]);
  await presentSpell(page, mutationFrame([]), 'decision', true);
  expect(await drawnMutations(page)).toEqual([]);
});
test('a creature drawn with the base because its sprite is missing hides the accents shaped for its body', async ({ page }) => {
  // Fail the asset request before the scene starts. Removing a texture while Phaser is drawing it
  // can crash the renderer and does not model a missing download.
  await page.route('**/battle-art/brute.webp', route => route.abort());
  await mountedPair(page);
  const cues = await drawnMutations(page);
  const missing = cues.filter(cue => cue.creature === '1');
  const healthy = cues.filter(cue => cue.creature === '2');
  expect(missing).toHaveLength(3);
  expect(healthy).toHaveLength(2);
  expect(missing.every(cue => !cue.visible)).toBe(true);
  expect(healthy.every(cue => cue.visible)).toBe(true);
});

test('mixed builds keep their targets inside the viewport and say their appearance in package names', async ({ page }, info) => {
  const state = await mount(page);
  const builds = [
    ['brute', 'ironbound', 'dreadnought', 'titan'],
    ['prowler', 'plague_doctor', 'blightweaver', 'apex'],
    ['occultist', 'necromancer', 'lich', 'archmage'],
    ['brute', 'berserker', 'ravager', 'occultist', 'necromancer'],
    ['prowler', 'parasite', 'soulreaver', 'occultist', 'shaman'],
    ['occultist', 'elementalist', 'harbinger', 'brute', 'marauder'],
  ];
  [...state.first.board.allies, ...state.first.board.enemies].forEach((one, i) => { one.acquiredTiers = builds[i].map(name => `tier:${name}:v1`); });
  await choose(page);
  await fits(page);
  await page.screenshot({ path: info.outputPath('art5-six-creatures.png') });
  await page.locator('[data-creature="3"]').click();
  await expect(page.locator('#inspect-appearance')).toHaveText(`Appearance: Warped body · ${packages.get('tier:lich:v1').name} (stage 3) · ${packages.get('tier:archmage:v1').name} (stage 4)`);
  await page.getByRole('button', { name: 'Close inspection' }).click();
  await page.setViewportSize({ width: 844, height: 390 });
  await fits(page);
  await page.screenshot({ path: info.outputPath('art5-landscape.png') });
});

test('a failed renderer offers to reload the scene, and the lobby, the classic table being archived', async ({ page }) => {
  await mount(page); await choose(page, 2);
  await page.locator('canvas').evaluate(canvas => canvas.dispatchEvent(new Event('webglcontextlost', { cancelable: true })));
  await expect(page.locator('#load-title')).toHaveText('The renderer was interrupted');
  await expect(page.locator('#access-link')).toHaveAttribute('href', '/');
  await expect(page.locator('#access-link')).toHaveText('Lobby'); await expect(page.locator('#retry')).toHaveText('Reload scene');
  await fitsViewport(page, '#access-link, #retry');
});

test('keyboard inspection and settings return focus and fit a short viewport', async ({ page }, info) => {
  await mount(page); await choose(page);
  await page.setViewportSize({ width: 720, height: 500 }); // CSS viewport of a 1440×1000 desktop at 200% zoom.
  await fitsViewport(page, '.creature, #help-open, #settings'); await fitsBattlefield(page);
  const target = page.locator('.creature').first(); await target.focus(); await page.keyboard.press('Enter');
  await expect(page.locator('#inspect-dialog')).toBeVisible(); await page.keyboard.press('Escape'); await expect(target).toBeFocused();
  await page.locator('#settings').focus(); await page.keyboard.press('Enter');
  await page.locator('#sound').focus(); await page.keyboard.press('Space');
  await expect(page.locator('#sound')).toBeChecked(); await page.keyboard.press('Escape'); await expect(page.locator('#settings')).toBeFocused();
  await page.screenshot({ path: info.outputPath('battle-short-viewport.jpg'), type: 'jpeg', quality: 85 });
});

test('cold scene resources and sampled frame intervals are recorded for release review', async ({ page }, info) => {
  const state = await mount(page); state.first.board.roundNumber = 1; await choose(page);
  const metrics = await page.evaluate(async () => {
    const intervals = []; let previous = performance.now();
    await new Promise(resolve => {
      const sample = now => { intervals.push(now - previous); previous = now; if (intervals.length < 90) requestAnimationFrame(sample); else resolve(); };
      requestAnimationFrame(sample);
    });
    intervals.shift(); intervals.sort((a, b) => a - b);
    const resources = performance.getEntriesByType('resource').filter(entry => /battle-art|battle-build/.test(entry.name));
    return { userAgent: navigator.userAgent, viewport: [innerWidth, innerHeight], dpr: devicePixelRatio,
      p50FrameMs: intervals[Math.floor(intervals.length * .5)], p95FrameMs: intervals[Math.floor(intervals.length * .95)],
      resources: resources.map(entry => ({ file: new URL(entry.name).pathname, decodedBytes: entry.decodedBodySize, transferBytes: entry.transferSize, durationMs: entry.duration })) };
  });
  // The scene loads the background and the four creature bodies; the retired three-form sheet is never fetched.
  const art = metrics.resources.filter(entry => entry.file.endsWith('.webp')).map(entry => entry.file.split('/').pop()).sort();
  expect(art).toEqual(['base-creature.webp', 'brute.webp', 'eclipse-battle.webp', 'predator.webp', 'warped.webp']);
  expect(metrics.resources.reduce((sum, entry) => sum + entry.decodedBytes, 0)).toBeLessThan(4 * 1024 * 1024);
  await writeFile(info.outputPath('release-metrics.json'), JSON.stringify(metrics, null, 2));
  await info.attach('release-metrics', { body: JSON.stringify(metrics, null, 2), contentType: 'application/json' });
});

test('the horizon falls once on 1 to 2 and settles on resume, replay and reduced motion', async ({ page }) => {
  await mountSpellScene(page);
  const present = (round, { reduced = false, replay = false } = {}) => page.evaluate(({ round, reduced, replay }) => {
    window.artScene.present({ stage: 'decision', actor: null, target: null, targets: [], creatures: [], round }, reduced, 1, replay);
  }, { round, reduced, replay });
  const span = () => page.evaluate(() => {
    const piece = window.artScene.children.getByName('horizon-span');
    return { visible: piece.visible, angle: piece.angle };
  });
  await present(2);
  expect((await span()).visible).toBe(false);
  await present(1);
  expect(await span()).toEqual({ visible: true, angle: 0 });
  await present(2);
  await page.clock.runFor(400);
  const midway = await span();
  expect(midway.visible).toBe(true);
  expect(midway.angle).toBeGreaterThan(0);
  await present(2);
  expect((await span()).angle).toBe(midway.angle);
  await page.clock.runFor(800);
  expect((await span()).visible).toBe(false);
  await present(1);
  await present(2, { replay: true });
  expect((await span()).visible).toBe(false);
  await present(1);
  await present(2, { reduced: true });
  expect((await span()).visible).toBe(false);
});

test('a returning seat sees the round twelve ruins with the combat HUD on a landscape phone', async ({ page }, info) => {
  test.skip(info.project.name !== 'phone');
  await page.setViewportSize({ width: 844, height: 390 });
  const state = await mount(page);
  state.second.board.roundNumber = 12;
  state.second.waitingFor = null;
  state.second.waitingAsked = null;
  await choose(page, 2);
  await expect(page.locator('#round-number')).toHaveText('12');
  await expect(page.locator('.creature')).toHaveCount(6);
  await page.screenshot({ path: info.outputPath('horizon-live-landscape.png') });
});

test('horizon damage follows the displayed round in both directions and keeps spreading past twelve', async ({ page }) => {
  await mountSpellScene(page);
  const visible = () => page.evaluate(() => window.artScene.children.list
    .filter(piece => piece.name.startsWith('horizon-') && piece.visible)
    .map(piece => piece.name.slice(8)).sort());
  const present = round => page.evaluate(round => window.artScene.present(
    { stage: 'decision', target: null, actor: null, targets: [], creatures: [], round }, true, 1, true), round);
  const stages = [
    [1, ['pier', 'span']], [2, ['pier']], [4, ['fallen-ledge', 'pier']], [6, ['breach', 'fallen-ledge', 'pier']],
    [8, ['breach', 'fallen-ledge']], [10, ['breach', 'fallen-ledge', 'shattered-sky']], [12, ['breach', 'broken-floor', 'fallen-ledge', 'shattered-sky']],
    [14, ['breach', 'broken-floor', 'fallen-ledge', 'faults', 'shattered-sky']],
  ];
  for (const [round, pieces] of stages) {
    await present(round);
    await expect.poll(async () => { await page.clock.runFor(80); return visible(); }).toEqual(pieces);
    await present(round + 1);
    if (round !== 1) expect(await visible()).toEqual(pieces);
  }
  const commands = () => page.evaluate(() => window.artScene.children.getByName('horizon-faults').commandBuffer.length);
  const first = await commands();
  await present(16);
  expect(await commands()).toBeGreaterThan(first);
  await present(6);
  expect(await visible()).toEqual(['breach', 'fallen-ledge', 'pier']);
  await present(null);
  expect(await visible()).toEqual([]);
});
