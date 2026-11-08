import { test, expect } from '@playwright/test';
import { readFileSync, readdirSync } from 'node:fs';
import { returningBattlePlayer } from './battle-help-fixture.js';
test.beforeEach(returningBattlePlayer);
test.setTimeout(60000);

// The packages as the catalogue serves them: their level and prerequisites, and the art authored beside the
// content, which names each package's illustration and each capstone's effect (ADR 0151).
const art = JSON.parse(readFileSync(new URL('../../data/presentation/mutation-art.json', import.meta.url), 'utf8'));
const tiersRoot = new URL('../../data/Tiers/', import.meta.url);
const packages = readdirSync(tiersRoot).filter(name => name.endsWith('.json')).map(name => JSON.parse(readFileSync(new URL(name, tiersRoot), 'utf8')))
  .map(tier => ({ id: tier.id, name: tier.name, level: tier.level, prerequisites: tier.prerequisites ?? [], anyOf: tier.anyOf ?? [], spells: [], visual: art[tier.id] }));
const T = name => `tier:${name}:v1`;
const creature = (id, bought = []) => ({ id, name: `Creature ${id}`, health: 20, maxHealth: 20, energy: 3, totalDefense: 1, currentInitiative: 5,
  isAlive: true, isStunned: false, isStunImmune: false, knownSpells: [], conditions: [], acquiredTiers: bought.map(T).sort() });
// Each purchase revealed in its own Evolution sub-phase, in the order given: the chronology the page reads.
const reveals = purchases => purchases.map(([id, name], index) => ({ sequence: index + 1, round: 1,
  event: { kind: 'PurchasesRevealed', roundId: 1, choices: [{ creature: id, tier: T(name) }] } }));

async function mount(page, { purchases = [], missing = [] } = {}) {
  const bought = id => purchases.filter(([one]) => one === id).map(([, name]) => name);
  const h = { errors: [], requests: [], view: null };
  h.board = () => ({ slot: 'Player1', roundNumber: 4, phase: 'Planning', subPhase: 'Speed', timeline: [],
    allies: [1, 2, 3].map(id => creature(id, bought(id))), enemies: [4, 5, 6].map(id => creature(id, bought(id))) });
  h.view = { board: h.board(), waitingFor: null, waitingAsked: null, options: {}, feed: reveals(purchases), feedNext: purchases.length + 1, over: false };
  page.on('pageerror', error => h.errors.push(error.message));
  page.on('request', request => { const url = new URL(request.url()); if (url.pathname.includes('/battle-art/evolutions/')) h.requests.push(url.pathname.split('/').pop()); });
  await page.clock.install();
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' }));
    const original = window.fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      window.notifyBattle = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  });
  for (const file of missing) await page.route(`**/battle-art/evolutions/${file}.webp`, route => route.fulfill({ status: 404, body: '' }));
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'evolution', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [], packages } });
    if (url.pathname === '/api/seat/player1') return route.fulfill({ json: h.view });
    return route.fulfill({ status: 500 });
  });
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#loading')).toBeHidden();
  h.publish = async () => { await page.evaluate(() => window.notifyBattle()); };
  return h;
}
const figure = (page, id) => page.locator(`#actor-layer [data-creature="${id}"]`);

test('each creature shows the illustration its purchases resolve to, and only those illustrations are fetched', async ({ page }, info) => {
  const h = await mount(page, { purchases: [[1, 'brute'], [1, 'berserker'], [4, 'occultist'], [4, 'necromancer'], [4, 'lich'], [4, 'prowler'], [4, 'assassin'], [4, 'deathstalker']] });
  await expect(figure(page, 1)).toHaveAttribute('data-artwork', 'brute-stage2-frenzied');
  // The first Stage 3 bought stays the identity: Revenant, not the Deathstalker bought after it.
  await expect(figure(page, 4)).toHaveAttribute('data-artwork', 'warped-stage3-revenant');
  await expect(figure(page, 2)).toHaveAttribute('data-artwork', 'base-creature');
  await page.clock.runFor(100);
  await expect(figure(page, 1)).toHaveAttribute('data-artwork-shown', 'brute-stage2-frenzied');
  await expect(figure(page, 4)).toHaveAttribute('data-artwork-shown', 'warped-stage3-revenant');
  await page.clock.runFor(500); await page.screenshot({ path: info.outputPath('evolution-field.png') });
  expect([...new Set(h.requests)].sort()).toEqual(['base-creature.webp', 'brute-stage2-frenzied.webp', 'warped-stage3-revenant.webp']);
  await expect(page.locator('#battle')).toHaveAttribute('data-asset-fallback', 'false'); expect(h.errors).toEqual([]);
});

test('an illustration that cannot load leaves the audited sprite of its form, and the battle carries on', async ({ page }) => {
  const h = await mount(page, { purchases: [[1, 'brute'], [1, 'ironbound']], missing: ['brute-stage2-ironhide'] });
  await expect(figure(page, 1)).toHaveAttribute('data-artwork', 'brute-stage2-ironhide');
  await page.clock.runFor(100);
  await expect(figure(page, 1)).toHaveAttribute('data-artwork-shown', 'form:brute');
  await expect(page.locator('#battle')).toHaveAttribute('data-asset-fallback', 'false');
  await figure(page, 1).click(); await expect(page.locator('#inspect-dialog')).toBeVisible();
  await expect(page.locator('#inspect-portrait')).toHaveCSS('background-image', /battle-art\/brute\.webp/); expect(h.errors).toEqual([]);
});

test('a purchase revealed on the field changes the illustration in place', async ({ page }) => {
  const h = await mount(page, { purchases: [[1, 'prowler']] });
  await page.clock.runFor(100); await expect(figure(page, 1)).toHaveAttribute('data-artwork-shown', 'predator-base');
  h.view = { ...h.view, board: { ...h.board(), allies: [creature(1, ['prowler', 'parasite']), creature(2), creature(3)] },
    feed: [...h.view.feed, ...reveals([[1, 'prowler'], [1, 'parasite']]).slice(1).map(entry => ({ ...entry, sequence: 2 }))], feedNext: 3 };
  await h.publish(); await page.clock.runFor(100);
  await expect(figure(page, 1)).toHaveAttribute('data-artwork', 'predator-stage2-open-maw');
  await expect(figure(page, 1)).toHaveAttribute('data-artwork-shown', 'predator-stage2-open-maw'); expect(h.errors).toEqual([]);
});

test('a capstone lends its effect and never changes the illustration; reduced motion stills it', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  const h = await mount(page, { purchases: [[1, 'brute'], [1, 'berserker'], [1, 'ravager'], [1, 'titan']] });
  await expect(figure(page, 1)).toHaveAttribute('data-artwork', 'brute-stage3-ravager');
  await expect(figure(page, 1)).toHaveAttribute('data-capstones', 'capstone-titan');
  await page.clock.runFor(100); await expect(figure(page, 1)).toHaveAttribute('data-artwork-shown', 'brute-stage3-ravager');
  // Still: the field around the figure is the same picture a second later.
  const box = await figure(page, 1).boundingBox();
  const clip = { x: Math.max(0, box.x - 20), y: Math.max(0, box.y - 120), width: box.width + 40, height: box.height + 140 };
  const first = await page.screenshot({ clip }); await page.clock.runFor(1000); const later = await page.screenshot({ clip });
  expect(Buffer.compare(first, later)).toBe(0); expect(h.errors).toEqual([]);
});

test('capstone effects move when motion is allowed', async ({ page }) => {
  const h = await mount(page, { purchases: [[1, 'occultist'], [1, 'elementalist'], [1, 'harbinger'], [1, 'archmage']] });
  await expect(figure(page, 1)).toHaveAttribute('data-capstones', 'capstone-archmage');
  await page.clock.runFor(100); await expect(figure(page, 1)).toHaveAttribute('data-artwork-shown', 'warped-stage3-cataclysm');
  const box = await figure(page, 1).boundingBox();
  const clip = { x: Math.max(0, box.x - 20), y: Math.max(0, box.y - 120), width: box.width + 40, height: box.height + 140 };
  const first = await page.screenshot({ clip }); await page.clock.runFor(700); const later = await page.screenshot({ clip });
  expect(Buffer.compare(first, later)).not.toBe(0); expect(h.errors).toEqual([]);
});
