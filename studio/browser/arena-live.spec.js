import { creatureAppearance } from '../../table/arena-art.js';
import { mountSpellScene, presentSpell } from './arena-spell-scene.js';
import { test, expect } from '@playwright/test';
import { readFileSync, readdirSync } from 'node:fs';
import { returningArenaPlayer } from './arena-help-fixture.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningArenaPlayer);

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
  await expect(page.locator('#arena')).toHaveAttribute('data-asset-fallback', 'false');
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
  page.on('response', response => { if (response.url().includes('/arena-art/') && response.ok()) loaded.push(response.url()); });
  await page.route('**/arena-art/brute.webp', route => route.abort());
  const state = await mount(page);
  state.first.board.allies[0].acquiredTiers = ['tier:brute:v1'];
  await choose(page);
  await expect(page.locator('#arena')).toHaveAttribute('data-asset-fallback', 'true');
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
  const frame = await mountedPair(page);
  await page.evaluate(() => window.artScene.textures.remove('creature-brute'));
  await presentSpell(page, { ...frame, creatures: frame.creatures.map(one => ({ ...one, id: `${one.id}0` })) }, 'decision', true);
  const fallback = await drawnMutations(page);
  expect(fallback.filter(cue => cue.creature === '10').every(cue => !cue.visible)).toBe(true);
  expect(fallback.filter(cue => cue.creature === '20').every(cue => cue.visible)).toBe(true);
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
