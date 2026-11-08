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
], effects: [{ kind: 'Bleed', glyph: '♥', harmful: true }, { kind: 'Regeneration', glyph: '♥', harmful: false },
  { kind: 'DefenseUp', glyph: '◇', harmful: false }, { kind: 'DefenseDown', glyph: '◇', harmful: true }] };
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
  // The stats as tiles with the field's icons.
  await expect(page.locator('#inspect-sheet [data-stat="health"] .inspect-tile-value')).toHaveText('20/25');
  await expect(page.locator('#inspect-sheet [data-stat="defense"] .inspect-tile-value')).toHaveText('3');
  await expect(page.locator('#inspect-sheet [data-stat="initiative"] .inspect-tile-value')).toHaveText('8');
  // Each status and condition a card wearing the icon of what it moves, with its duration and its source.
  const conditions = page.locator('#inspect-effects .inspect-condition');
  await expect(conditions).toHaveText(['Immune to stun', 'Bleed 2 · new · first countdown pending · from Skullbeast · 3 · Strike', 'DefenseBuff 2 · permanent', 'Regeneration 1 · 1 round']);
  expect(await conditions.evaluateAll(nodes => nodes.map(node => node.dataset.cue))).toEqual(['stun', 'health-loss', 'other', 'health-gain']);
  const shared = page.locator('#inspect-mutations [data-inspect-mutation="cap"]');
  await expect(shared.locator('.inspect-chip')).toHaveText(['Stage 4', 'Initiative +0']);
  await expect(shared).toContainText('For every creature of the side');
  // Known spells are the spell choice's cards: cost and crit chips, targeting, effect lines, and their teacher.
  const strike = page.locator('#inspect-spells [data-inspect-spell="strike"]');
  await expect(strike.locator('.spell-card-name')).toHaveText('Strike'); await expect(strike.locator('.spell-card-target')).toHaveText('One enemy');
  await expect(strike.locator('.spell-card-effect[data-caster="true"]')).toHaveText('Gain 1 energy');
  await expect(strike.locator('.spell-chip-crit')).toContainText('20%'); await expect(strike.locator('.spell-chip-crit small')).toHaveText('Precision only · d20 17+');
  await expect(strike.locator('.inspect-spell-source')).toHaveText('Learned from Brute');
  await expect(page.locator('#inspect-spells [data-inspect-spell="focus"] .spell-cost-value')).toHaveText('0');
  await expect(page.locator('#inspect-dialog [data-spell]')).toHaveCount(0);
  await expect(page.locator('#inspect-dialog')).not.toContainText('PRIVATE');
  await fitsViewport(page, '#inspect-close');
  await page.screenshot({ path: info.outputPath('inspection-fixture.jpg'), type: 'jpeg', quality: 85 });
  await page.keyboard.press('Escape'); await expect(page.locator('#actor-layer [data-creature="1"]')).toBeFocused();
  await page.locator('#actor-layer [data-creature="2"]').click();
  await expect(page.locator('#inspect-mutations')).toHaveText('No publicly acquired mutations.');
  await expect(page.locator('#inspect-spells')).toContainText('Strike');
  expect(h.shown).toEqual([]); expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});

test('the portrait says it zooms, shows the illustration over the card and gives the card back', async ({ page }, info) => {
  const h = await mount(page);
  await page.locator('#actor-layer [data-creature="1"]').click(); await expect(page.locator('#inspect-dialog')).toBeVisible();
  const portrait = page.locator('#inspect-portrait');
  await expect(portrait.locator('.inspect-zoom-hint')).toBeVisible(); await expect(portrait).toHaveAttribute('aria-label', 'Show Skullbeast · 1 large');
  await portrait.click();
  await expect(page.locator('#inspect-zoom')).toBeVisible(); await expect(page.locator('#inspect-zoom-close')).toBeFocused();
  expect(await page.locator('#inspect-zoom-close').evaluate(node => node.style.backgroundImage))
    .toBe(await portrait.evaluate(node => node.style.backgroundImage));
  await page.screenshot({ path: info.outputPath('inspect-zoom.png') });
  // Escape gives the card back first; a second Escape closes the card.
  await page.keyboard.press('Escape'); await expect(page.locator('#inspect-zoom')).toBeHidden();
  await expect(page.locator('#inspect-dialog')).toBeVisible(); await expect(portrait).toBeFocused();
  await portrait.click(); await page.locator('#inspect-zoom-close').click(); await expect(page.locator('#inspect-zoom')).toBeHidden();
  await page.keyboard.press('Escape'); await expect(page.locator('#inspect-dialog')).toBeHidden();
  expect(h.writes ?? []).toEqual([]); expect(h.errors).toEqual([]);
});
test('same-creature polls keep the spell cards, keyboard focus and reading position', async ({ page }) => {
  const h = await mount(page); await page.locator('#actor-layer [data-creature="1"]').click();
  const body = page.locator('#inspect-body'), strike = page.locator('#inspect-spells [data-inspect-spell="strike"]');
  await body.focus(); await strike.evaluate(node => { node.dataset.kept = 'yes'; });
  const scroll = await body.evaluate(node => { node.scrollTop = 170; return node.scrollTop; });
  h.view.board.allies[0].health = 17; await h.publish();
  await expect(page.locator('#inspect-stats')).toContainText('17/25 HP');
  await expect(page.locator('#inspect-sheet [data-stat="health"] .inspect-tile-value')).toHaveText('17/25');
  await expect(body).toBeFocused(); await expect(strike).toHaveAttribute('data-kept', 'yes');
  expect(await body.evaluate(node => node.scrollTop)).toBe(scroll);
  // A newly known spell is dealt as a card; the reader keeps their place.
  h.view.board.allies[0].knownSpells.push('new-missing'); await h.publish();
  await expect(page.locator('#inspect-spells [data-inspect-spell="new-missing"]')).toContainText('Spell details unavailable'); await expect(body).toBeFocused();
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
  await expect(page.locator('#inspect-spells .inspect-spell')).toHaveCount(1); await expect(page.locator('#inspect-spells')).toContainText('Spell details unavailable');
  await expect(page.locator('#inspect-portrait')).toHaveAttribute('data-defeated', 'true');
  await expect(page.locator('#inspect-history')).toBeHidden();
  expect(h.errors).toEqual([]);
});

test('Step inspection uses recorded knowledge and never offers the current mutation tree', async ({ page }) => {
  const h = await playbackMount(page); await page.locator('#settings').click(); await page.selectOption('#playback-preference', 'step'); await page.getByRole('button', { name: 'Close settings' }).click();
  // The opponent's action, held for its OK: the player's own would move on by itself (ADR 0141).
  await h.publish([entry(10, { actor: 4, targets: [1] })], { board: { ...h.state.view.board, roundNumber: 3,
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
  for (const id of ['inspect-mutations', 'inspect-spells', 'inspect-stats', 'inspect-sheet', 'inspect-effects', 'inspect-last-action']) await expect(page.locator(`#${id}`)).toBeEmpty();
  await expect(page.locator('#inspect-history')).toBeHidden(); expect(h.errors).toEqual([]);
});

test('a hidden tab clears the inspection, and the same creature reopened shows its stat tiles again', async ({ page }) => {
  await mount(page); await page.locator('#actor-layer [data-creature="1"]').click();
  await expect(page.locator('#inspect-sheet .inspect-tile')).toHaveCount(4);
  const visibility = hidden => page.evaluate(value => { Object.defineProperty(document, 'hidden', { configurable: true, value }); document.dispatchEvent(new Event('visibilitychange')); }, hidden);
  await visibility(true); await expect(page.locator('#inspect-dialog')).toBeHidden(); await expect(page.locator('#inspect-sheet')).toBeEmpty();
  await visibility(false); await expect(page.locator('#loading')).toBeHidden();
  await page.locator('#actor-layer [data-creature="1"]').click();
  await expect(page.locator('#inspect-sheet .inspect-tile')).toHaveCount(4);
  await expect(page.locator('#inspect-sheet [data-stat="health"] .inspect-tile-value')).toHaveText('20/25');
});

test('short portrait, landscape and enlarged text retain internal scrolling and reachable close', async ({ page }) => {
  const h = await mount(page); await page.locator('#actor-layer [data-creature="1"]').click();
  for (const viewport of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(viewport);
    await page.addStyleTag({ content: '#inspect-dialog p, #inspect-dialog li, #inspect-dialog strong { font-size: 24px !important; }' });
    await fitsViewport(page, '#inspect-close');
    expect(await page.locator('#inspect-body').evaluate(node => node.clientHeight)).toBeGreaterThan(40);
    // The last card is reached by scrolling the body, never by losing the close button.
    const last = page.locator('#inspect-spells [data-inspect-spell="focus"]');
    await last.scrollIntoViewIfNeeded(); await expect(last).toBeInViewport();
    await fitsViewport(page, '#inspect-close');
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
    await expect(actor.locator('.creature-defense')).toHaveText('3');
    const bounds = await actor.evaluate(node => ({ tag: node.querySelector('.creature-tag').getBoundingClientRect().bottom, caption: node.querySelector('.caption').getBoundingClientRect().top }));
    expect(bounds.tag).toBeLessThanOrEqual(bounds.caption);
    await page.clock.runFor(50);
    await page.screenshot({ path: info.outputPath('field-cues-fixture.jpg'), type: 'jpeg', quality: 85 });
  }
  expect(h.errors).toEqual([]);
});

test('initiative stands under every name on the field, on phones too, below the status tag', async ({ page }) => {
  const h = await mount(page); h.view.board.allies[0].isStunned = true; await h.publish();
  await expect(page.locator('#actor-layer [data-creature="1"] .creature-tag')).toHaveText('STUNNED');
  const clear = await page.locator('#actor-layer [data-creature="1"]').evaluate(node => node.querySelector('.creature-tag').getBoundingClientRect().bottom <= node.querySelector('.caption').getBoundingClientRect().top);
  expect(clear).toBe(true);
  for (const id of ['1', '3']) {
    const node = page.locator(`#actor-layer [data-creature="${id}"]`);
    await expect(node.locator('.creature-init')).toBeVisible();
    // The initiative leads the badge row, and the row is what stands centred under the name.
    const [name, init] = await node.evaluate(one => [one.querySelector('.creature-name'), one.querySelector('.creature-badges')].map(part => part.getBoundingClientRect().toJSON()));
    // Right under the whole name, centred on it, and the name itself never cut to make room.
    expect(init.top).toBeGreaterThanOrEqual(name.bottom - 1); expect(init.top - name.bottom).toBeLessThan(8);
    expect(Math.abs((init.left + init.right) / 2 - (name.left + name.right) / 2)).toBeLessThan(2);
    expect(await node.locator('.creature-name').evaluate(one => one.scrollWidth <= one.clientWidth)).toBe(true);
  }
  await expect(page.locator('#actor-layer [data-creature="1"] .creature-init')).toHaveText('8 INIT');
  expect(h.errors).toEqual([]);
});

test('defense shows with a shield and each kind of condition with its icon, on phones too; zero defense shows nothing', async ({ page }) => {
  const h = await mount(page);
  h.view.board.allies[0] = { ...h.view.board.allies[0], isStunned: true, conditions: [
    { effect: { kind: 'Bleed', amountPerRound: 2 }, remainingRounds: 2 }, { effect: { kind: 'Bleed', amountPerRound: 1 }, remainingRounds: 1 },
    { effect: { kind: 'Regeneration', amountPerRound: 1 }, remainingRounds: 2 },
    { effect: { kind: 'DefenseUp', amount: 2 }, remainingRounds: 1 }, { effect: { kind: 'DefenseDown', amount: 1 }, remainingRounds: 1 }] };
  h.view.board.allies[1] = { ...h.view.board.allies[1], totalDefense: 0 };
  await h.publish();
  const first = page.locator('#actor-layer [data-creature="1"]'), second = page.locator('#actor-layer [data-creature="2"]');
  await expect(first.locator('.creature-defense')).toBeVisible(); await expect(first.locator('.creature-defense')).toHaveText('3');
  await expect(first.locator('.creature-defense')).toHaveAttribute('title', '3 defense');
  await expect(second.locator('.creature-defense')).toBeHidden();
  const cues = first.locator('.creature-conditions .cue');
  await expect(cues).toHaveCount(5); await expect(cues.first()).toBeVisible();
  expect(await cues.evaluateAll(nodes => nodes.map(node => node.dataset.cue))).toEqual(['health-loss', 'health-gain', 'defense-gain', 'defense-loss', 'stun']);
  await expect(cues.first()).toHaveAttribute('title', 'Bleed 2 · Bleed 1');
  await expect(second.locator('.creature-conditions')).toBeHidden();
  // The status tag still clears the caption, now taller by its badge rows.
  const clear = await first.evaluate(node => node.querySelector('.creature-tag').getBoundingClientRect().bottom <= node.querySelector('.caption').getBoundingClientRect().top + 1);
  expect(clear).toBe(true); expect(h.errors).toEqual([]);
});

test('a new round is announced at the top of the field, then fades; opening the page is not a new round', async ({ page }) => {
  const h = await mount(page); await expect(page.locator('#round-toast')).toBeHidden();
  h.view.board.roundNumber = 4; await h.publish();
  await expect(page.locator('#round-toast')).toBeVisible(); await expect(page.locator('#round-toast')).toContainText('Round 4');
  await page.clock.runFor(2700); await expect(page.locator('#round-toast')).toBeHidden(); expect(h.errors).toEqual([]);
});
