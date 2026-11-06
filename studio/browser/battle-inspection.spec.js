import { test, expect } from '@playwright/test';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { fitsViewport } from './fit.js';
import { mount as playbackMount, entry, before } from './battle-playback-fixture.js';
test.beforeEach(returningBattlePlayer);
test.setTimeout(60000);
const creature = (id, extra = {}) => ({ id, name: 'Skullbeast', isAlive: true, health: 20, maxHealth: 25, energy: 6,
  totalDefense: 3, currentInitiative: 8, conditions: [], acquiredTiers: ['root'], knownSpells: ['strike', 'focus'], ...extra });
const catalogue = { cards: [
  { id: 'strike', name: 'Strike', cost: 2, type: 'Offense', targeting: 'One enemy', effects: ['Deal 7 damage'], casterEffects: ['Gain 1 energy'], critical: '20%', criticalThreshold: 17, criticalNote: 'Critical: deal 14 damage' },
  { id: 'focus', name: 'Focus', cost: 0, type: 'Utility', targeting: 'Self', effects: ['Gain 2 energy'] },
  { id: 'hidden', name: 'PRIVATE_SPELL' },
], packages: [
  { id: 'root', name: 'Brute', level: 1, initiativeBonus: 2, spells: ['strike'], tags: ['Resilient'] },
  { id: 'cap', name: 'Shared bulwark', level: 4, initiativeBonus: 0, spells: [], passiveLines: ['For every creature of the side: defense +2'] },
  { id: 'pending', name: 'PRIVATE_MUTATION', level: 2, spells: ['hidden'] },
] };
const cast = () => ({ sequence: 4, event: { kind: 'CombatActionResolved', roundId: 2,
  resolution: { action: { actor: 1, spell: 'strike', targets: [3] }, isCritical: false },
  appliedOutcomes: [{ kind: 'DamageOutcome', target: 3, amount: 7 }],
  frame: { before: [creature(1), creature(3)], after: [creature(1), creature(3, { health: 13 })], timeline: [] } } });
async function mount(page, options = {}) {
  const h = { errors: [], shown: [], writes: [], status: 200, view: { board: {
    slot: 'Player1', roundNumber: 3, phase: 'Planning', subPhase: 'Speed', timeline: [],
    allies: [creature(1, { acquiredTiers: ['root', 'cap'], conditions: [
      { effect: { kind: 'Bleed', amountPerRound: 2 }, remainingRounds: 2, isFresh: true, source: { caster: 3, spell: 'strike' } },
      { effect: { kind: 'DefenseBuff', amount: 2 }, remainingRounds: null },
      { effect: { kind: 'Regeneration', amountPerRound: 1 }, remainingRounds: 1 },
    ], isStunImmune: true }), creature(2, { acquiredTiers: [], knownSpells: ['strike'] })],
    enemies: [creature(3, { health: 0, isAlive: false, acquiredTiers: ['unknown'], knownSpells: ['missing'] })],
    evolutionChoices: [{ creature: 2, tier: 'pending' }], intents: [{ actor: 3, spell: 'hidden', targets: [1] }],
  }, waitingFor: null, options: {}, feed: [cast()], feedNext: 5 } };
  page.on('pageerror', error => h.errors.push(error.message)); await page.clock.install();
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' }));
    const original = window.fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      window.notifyBattle = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  });
  if (options.missingArt) await page.route('**/battle-art/**', route => route.abort());
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'inspect', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: options.missingCatalogue ? {} : catalogue });
    if (url.pathname === '/api/seat/player1') {
      if (url.searchParams.has('shown')) h.shown.push(url.searchParams.get('shown'));
      return route.fulfill({ status: h.status, json: h.view });
    }
    if (url.pathname.endsWith('/decision')) h.writes.push(route.request().postDataJSON());
    return route.fulfill({ status: 500 });
  });
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 10000));
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#loading')).toBeHidden();
  h.publish = async () => { await page.evaluate(() => window.notifyBattle()); };
  return h;
}

test('inspect allies, conditions, acquired passives and complete spell faces without revealing plans', async ({ page }, info) => {
  const h = await mount(page);
  await page.locator('#actor-layer [data-creature="1"]').click();
  await expect(page.locator('#inspect-name')).toHaveText('Skullbeast · 1');
  await expect(page.locator('#inspect-context')).toHaveText('Round 3 · Current state');
  await expect(page.locator('#inspect-stats')).toContainText('3 defense · 8 initiative');
  await expect(page.locator('#inspect-effects')).toContainText('Immune to stun / Bleed 2 · new · first countdown pending · from Skullbeast · 3 · Strike');
  await expect(page.locator('#inspect-effects')).toContainText('permanent'); await expect(page.locator('#inspect-effects')).toContainText(/\b1 round(?!s)/);
  await expect(page.locator('#inspect-mutations')).toContainText('Acquired · Stage 4 · Initiative bonus +0');
  await expect(page.locator('#inspect-mutations')).toContainText('For every creature of the side');
  await page.locator('#inspect-spells [data-inspect-spell="strike"] summary').click();
  await expect(page.locator('#inspect-spells')).toContainText('One enemy');
  await expect(page.locator('#inspect-spells')).toContainText('Caster: Gain 1 energy');
  await expect(page.locator('#inspect-spells')).toContainText('20% crit · Precision only · d20 17+ · catalogue');
  await expect(page.locator('#inspect-spells')).toContainText('Focus · 0 energy');
  await expect(page.locator('#inspect-dialog')).not.toContainText('PRIVATE');
  await fitsViewport(page, '#inspect-close');
  await page.screenshot({ path: info.outputPath('inspection-fixture.jpg'), type: 'jpeg', quality: 85 });
  await page.keyboard.press('Escape'); await expect(page.locator('#actor-layer [data-creature="1"]')).toBeFocused();
  await page.locator('#actor-layer [data-creature="2"]').click();
  await expect(page.locator('#inspect-mutations')).toHaveText('No publicly acquired mutations.');
  await expect(page.locator('#inspect-spells')).toContainText('Strike');
  expect(h.shown).toEqual([]); expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});

test('same-creature polls preserve expanded spell, keyboard focus and reading position', async ({ page }) => {
  const h = await mount(page); await page.locator('#actor-layer [data-creature="1"]').click();
  const summary = page.locator('#inspect-spells [data-inspect-spell="strike"] summary');
  await summary.focus(); await page.keyboard.press('Enter');
  const scroll = await page.locator('#inspect-body').evaluate(node => { node.scrollTop = 170; return node.scrollTop; });
  h.view.board.allies[0].health = 17; await h.publish();
  await expect(page.locator('#inspect-stats')).toContainText('17/25 HP');
  await expect(summary).toBeFocused(); await expect(summary.locator('..')).toHaveAttribute('open');
  expect(await page.locator('#inspect-body').evaluate(node => node.scrollTop)).toBe(scroll);
  // Even adding a known spell leaves the existing focused disclosure intact.
  h.view.board.allies[0].knownSpells.push('new-missing'); await h.publish();
  await expect(page.locator('#inspect-spells')).toContainText('new-missing'); await expect(summary).toBeFocused();
  expect(h.errors).toEqual([]);
});

test('last public cast opens its exact historical event after rollover and returns to the reference', async ({ page }) => {
  const h = await mount(page); await page.locator('#actor-layer [data-creature="1"]').click();
  await expect(page.locator('#inspect-last-action')).toContainText('Round 2 · Strike');
  h.view.board.roundNumber = 9; h.view.feed = []; await h.publish();
  await expect(page.locator('#inspect-context')).toContainText('Round 9');
  await page.locator('#inspect-history').click();
  await expect(page.locator('#history-detail')).toContainText('Round 2 · Combat · event 4');
  await expect(page.locator('#history-detail')).toContainText('13/25 HP');
  await page.locator('#history-before').click(); await expect(page.locator('#history-detail')).toContainText('20/25 HP');
  await page.keyboard.press('Escape'); await expect(page.locator('#inspect-history')).toBeFocused();
  expect(h.shown).toEqual([]); expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});

test('defeated opponents and missing art or catalogue remain inspectable with explicit fallbacks', async ({ page }) => {
  const h = await mount(page, { missingArt: true, missingCatalogue: true });
  await page.locator('#actor-layer [data-creature="3"]').click();
  await expect(page.locator('#inspect-name')).toHaveText('Skullbeast · 3');
  await expect(page.locator('#inspect-side')).toHaveText('OPPONENT');
  await expect(page.locator('#inspect-effects')).toContainText('Defeated');
  await expect(page.locator('#inspect-mutations')).toContainText('unknown');
  await expect(page.locator('#inspect-mutations')).toContainText('Mutation details unavailable');
  await page.locator('#inspect-spells summary').click(); await expect(page.locator('#inspect-spells')).toContainText('Spell details unavailable');
  await expect(page.locator('#inspect-history')).toBeHidden();
  expect(h.errors).toEqual([]);
});

test('Step inspection uses recorded knowledge and never offers the current mutation tree', async ({ page }) => {
  const h = await playbackMount(page); await page.locator('#settings').click(); await page.selectOption('#playback-preference', 'step'); await page.getByRole('button', { name: 'Close settings' }).click();
  await h.publish([entry(10)], { board: { ...h.state.view.board, roundNumber: 3,
    allies: before.slice(0, 3).map(one => ({ ...one, acquiredTiers: ['FUTURE'], knownSpells: ['FUTURE'], energy: 99 })) } });
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '10'); await page.clock.runFor(3400);
  await page.locator('#actor-layer [data-creature="1"]').click();
  await expect(page.locator('#inspect-context')).toHaveText('Round 2 · After recorded action · event 10');
  await expect(page.locator('#inspect-stats')).toContainText('5 energy'); await expect(page.locator('#inspect-dialog')).not.toContainText('FUTURE');
  await expect(page.locator('#inspect-mutations')).toHaveText('Mutation ownership not recorded.');
  await expect(page.locator('#inspect-growth')).toBeHidden();
  await expect(page.locator('#inspect-action-heading')).toHaveText('Latest loaded action at this point');
  expect(h.errors).toEqual([]);
});

test('revocation erases every reference and pending history link', async ({ page }) => {
  const h = await mount(page); await page.locator('#actor-layer [data-creature="1"]').click(); await page.locator('#inspect-history').click();
  h.status = 403; await h.publish(); await expect(page.locator('#inspect-dialog')).toBeHidden();
  await expect(page.locator('#history-dialog')).toBeHidden();
  for (const id of ['inspect-mutations', 'inspect-spells', 'inspect-stats', 'inspect-last-action']) await expect(page.locator(`#${id}`)).toBeEmpty();
  await expect(page.locator('#inspect-history')).toBeHidden(); expect(h.errors).toEqual([]);
});

test('short portrait, landscape and enlarged text retain internal scrolling and reachable close', async ({ page }) => {
  const h = await mount(page); await page.locator('#actor-layer [data-creature="1"]').click();
  for (const viewport of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(viewport);
    await page.addStyleTag({ content: '#inspect-dialog p, #inspect-dialog summary { font-size: 24px !important; }' });
    await fitsViewport(page, '#inspect-close');
    expect(await page.locator('#inspect-body').evaluate(node => node.clientHeight)).toBeGreaterThan(40);
    await page.locator('#inspect-spells [data-inspect-spell="focus"] summary').click();
    await expect(page.locator('#inspect-spells [data-inspect-spell="focus"]')).toHaveAttribute('open');
    await page.locator('#inspect-spells [data-inspect-spell="focus"] summary').click();
  }
  expect(h.errors).toEqual([]);
});

test('field labels keep defense, initiative and conditions legible beside the status tag', async ({ page }, info) => {
  const h = await mount(page); h.view.board.allies[0].isStunned = true; await h.publish();
  const actor = page.locator('#actor-layer [data-creature="1"]');
  await expect(actor.locator('.creature-tag')).toHaveText('STUNNED');
  await expect(actor).toHaveAttribute('aria-label', /3 defense, 8 initiative.*Immune to stun/);
  if (page.viewportSize().width >= 620) {
    await expect(actor.locator('.creature-defense')).toBeVisible();
    await expect(actor.locator('.creature-defense')).toHaveText('3 DEF · 8 INIT');
    const bounds = await actor.evaluate(node => ({ tag: node.querySelector('.creature-tag').getBoundingClientRect().bottom, caption: node.querySelector('.caption').getBoundingClientRect().top }));
    expect(bounds.tag).toBeLessThanOrEqual(bounds.caption);
    await page.clock.runFor(50);
    await page.screenshot({ path: info.outputPath('field-cues-fixture.jpg'), type: 'jpeg', quality: 85 });
  }
  expect(h.errors).toEqual([]);
});
