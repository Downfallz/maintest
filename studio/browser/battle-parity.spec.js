import { test, expect } from '@playwright/test';
import { openSpells } from './battle-spells-fixture.js';
import { teamTempos } from './battle-help-fixture.js';
import { fitsViewport } from './fit.js';
import { decided } from './decision.js';

// Cross-feature presentation acceptance. These are declared fixture responses, not a second game engine.
// battle.loop.js separately plays both declaration modes against the actual host.
const creature = id => ({ id, name: `Creature ${id}`, health: 20, maxHealth: 20, energy: 2,
  totalDefense: 0, isAlive: true, conditions: [], acquiredTiers: [], knownSpells: ['strike'] });
const roster = [1, 2, 3, 4, 5, 6].map(creature);
const roots = ['Brute', 'Predator', 'Warped'];
const cards = Array.from({ length: 18 }, (_, index) => ({ id: index ? `spell-${index}` : 'strike',
  name: index ? `Reference spell ${index}` : 'Strike', cost: 2, critical: '20%',
  type: index % 2 ? 'Defensive' : 'Offensive', effects: ['Deal 7 damage to the selected enemy.'], targeting: 'One enemy' }));
const energy = { sequence: 1, event: { kind: 'RoundEnergyGranted', roundId: 1,
  grants: roster.map(one => ({ creature: one.id, gained: 2, before: 0, after: 2, baseRequested: 2, passiveRequested: 0 })) } };
async function mount(page) {
  const state = { errors: [], writes: [], shown: [], view: { waitingFor: 'Evolution', waitingAsked: 11, waitingCreature: 1,
    board: { slot: 'Player1', roundNumber: 1, phase: 'Planning', subPhase: 'Evolution',
      allies: roster.slice(0, 3), enemies: roster.slice(3), timeline: [], speedChoices: [] },
    options: { evolution: { remainingPicks: 1, creatures: [{ creature: 1, availableTiers: roots }] } }, feed: [energy, { sequence: 2, event: { kind: 'OngoingEffectsApplied', roundId: 1 } }], feedNext: 3 } };
  page.on('pageerror', error => state.errors.push(error.message));
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' }));
    const original = window.fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      // A host says it is still there every twenty seconds (ADR 0119): without it the page takes a quiet stream for a
      // dropped one after 45 seconds (ADR 0164) and opens another, and a long test would notify the closed one.
      const alive = setInterval(() => { try { controller.enqueue(new TextEncoder().encode(': keep-alive\n\n')); } catch { clearInterval(alive); } }, 20000);
      window.notifyBattle = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  });
  await page.route('**/api/**', route => {
    const request = route.request(), url = new URL(request.url());
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'parity', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards,
      rules: { combatDeclaration: 'AtActivation', energyPerRound: 2 },
      packages: roots.map((name, i) => ({ id: name, name, level: 1, initiativeBonus: i, spells: ['strike'], tags: [['Guard'], ['Speed'], ['Magic']][i] })) } });
    if (url.pathname === '/api/seat/player1') {
      if (url.searchParams.has('shown')) state.shown.push(Number(url.searchParams.get('shown')));
      return route.fulfill({ json: state.view });
    }
    if (url.pathname.endsWith('/decision')) {
      const command = decided(request); state.writes.push(command);
      if (command.kind === 'Evolution') {
        state.view.waitingFor = 'Speed'; state.view.options = { speed: { missing: [1, 2, 3] } };
        state.view.board.subPhase = 'Speed';
      } else if (command.kind === 'Speed') {
        state.view.board.speedChoices.push({ creature: command.creature, speed: command.speed });
        state.view.waitingCreature = [1, 2, 3].find(id => !state.view.board.speedChoices.some(one => one.creature === id));
        if (!state.view.waitingCreature) state.view.waitingFor = null;
      } else state.view.waitingFor = null;
      state.view.waitingAsked++;
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({ status: 404 });
  });
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#upkeep-dialog')).toBeVisible();
  return state;
}

// Find controls with real Tab navigation: no locator.focus(), synthetic clicks, or pointer actions. A control behind the
// focus in the page's order is reached with Shift+Tab, as a person would, rather than by tabbing round the whole page.
async function keyboardActivate(page, selector) {
  const target = page.locator(selector); await expect(target).toBeVisible(); await expect(target).toBeEnabled();
  const behind = await target.evaluate(node => {
    const active = document.activeElement;
    return Boolean(active && active !== document.body && node.compareDocumentPosition(active) & Node.DOCUMENT_POSITION_FOLLOWING);
  });
  const key = behind ? 'Shift+Tab' : 'Tab';
  for (let n = 0; n < 100 && !await target.evaluate(node => node === document.activeElement); n++) await page.keyboard.press(key);
  await expect(target).toBeFocused(); await expect(target).toBeInViewport();
  await page.keyboard.press('Enter');
}
async function combat(page, state) {
  state.view = { ...state.view, waitingFor: 'Action', waitingAsked: 20, waitingCreature: 1, feed: [],
    board: { ...state.view.board, roundNumber: 9, phase: 'Combat', subPhase: 'Activation',
      allies: roster.slice(0, 3).map(one => ({ ...one, knownSpells: cards.map(card => card.id) })) },
    options: { action: { creature: 1, spells: cards.map(card => ({ spell: card.id, legalTargets: { candidates: [4, 5, 6], minTargets: 1, maxTargets: 1 } })) } } };
  await page.evaluate(() => window.notifyBattle());
  await expect(page.locator('#help-dialog')).toBeVisible();
  await keyboardActivate(page, '#help-close');
}

test('keyboard journey reads energy, compares a mutation, edits every tempo and reopens applied damage', async ({ page }, info) => {
  // Every Tab repaints a desktop canvas rendered in software: a CI runner takes over twice the 45s a laptop does.
  test.setTimeout(180000);
  const state = await mount(page);
  await expect(page.locator('#upkeep-body')).toContainText('+2 energy · 0 → 2');
  expect(state.shown).toEqual([]); await keyboardActivate(page, '#upkeep-close');
  await expect(page.locator('#help-title')).toHaveText('What is a mutation?');
  // Closing the tip opens the mutation tree by itself (ADR 0138).
  await keyboardActivate(page, '#help-close'); await expect(page.locator('#planning-dialog')).toBeVisible();
  await keyboardActivate(page, '[data-mutation="Predator"]');
  await expect(page.locator('.mutation-preview .spell-card[data-spell]').first()).toContainText('Strike');
  await expect(page.locator('.mutation-preview .spell-chip-cost').first()).toHaveText('2 energy');
  await keyboardActivate(page, '#mutation-atlas-open'); await expect(page.locator('#atlas-dialog')).toBeVisible();
  // On a phone the first Escape folds the open card back into the tree, the next one closes it.
  await page.keyboard.press('Escape'); if (await page.locator('#atlas-dialog').isVisible()) await page.keyboard.press('Escape');
  await expect(page.locator('#atlas-dialog')).toBeHidden(); await expect(page.locator('#mutation-atlas-open')).toBeFocused();
  await expect(page.locator('#planning-selection')).toHaveText('Creature 1 · Predator');
  expect(state.writes).toEqual([]); await keyboardActivate(page, '#planning-confirm');
  await expect(page.locator('#help-title')).toHaveText('Choose how your team acts');
  // Closing the tip opens the team's tempos by themselves, docked as the mutation tree is (ADR 0150).
  await keyboardActivate(page, '#help-close'); await expect(page.locator('#planning-dialog')).toHaveAttribute('data-kind', 'Speed');
  await expect(page.locator('#planning-dialog')).toBeVisible();
  for (let i = 0; i < 3; i++) await keyboardActivate(page, '[data-speed="Standard"]');
  await keyboardActivate(page, '[data-tempo-tab="3"]');
  await page.keyboard.press('Home'); await expect(page.locator('[data-tempo-tab="1"]')).toBeFocused();
  await keyboardActivate(page, '[data-speed="Quick"]');
  await keyboardActivate(page, '#help-open'); await page.keyboard.press('Escape');
  await expect(page.locator('#help-open')).toBeFocused();
  await expect(page.locator('[data-tempo-tab="1"]')).toContainText('Instinct');
  expect(state.writes.length).toBe(1); await keyboardActivate(page, '#planning-confirm');
  await expect(page.locator('#planning-dialog')).toBeHidden();
  expect(state.writes.slice(1).map(one => one.speed)).toEqual(['Quick', 'Standard', 'Standard']);
  // The spell choice opens by itself on the player's turn (ADR 0149).
  await combat(page, state); await expect(page.locator('#combat-dialog')).toBeVisible();
  await keyboardActivate(page, '[data-spell="strike"]');
  await keyboardActivate(page, '[data-creature="4"]');
  await expect(page.locator('[data-creature="4"]')).toHaveAttribute('aria-pressed', 'true');
  await keyboardActivate(page, '#combat-confirm');
  // The cast is sent as Confirm is pressed; the host's mock records it a moment later.
  await expect.poll(() => state.writes.at(-1)).toMatchObject({ spell: 'strike', targets: [4], asked: 20 });
  const after = roster.map(one => ({ ...one, health: one.id === 4 ? 13 : 20 }));
  state.view.feed = [{ sequence: 10, event: { kind: 'CombatActionResolved', roundId: 9,
    resolution: { action: { actor: 1, spell: 'strike', targets: [4] }, isCritical: false },
    appliedOutcomes: [{ kind: 'DamageOutcome', target: 4, amount: 7 }], frame: { before: roster, after, timeline: [], rollOffs: [] } } }];
  state.view.feedNext = 11; state.view.board.enemies = after.slice(3);
  await page.evaluate(() => window.notifyBattle());
  await expect(page.locator('#playback-skip')).toBeHidden({ timeout: 10000 });
  await keyboardActivate(page, '#history-open');
  // History opens on its summary; the cast is a key away there, and All events holds the full record.
  await expect(page.locator('#history-summary [data-sequence="10"]')).toContainText('Strike');
  await keyboardActivate(page, '#history-mode-events');
  await keyboardActivate(page, '#history-list [data-sequence="10"] summary');
  await expect(page.locator('#history-list [data-sequence="10"]')).toContainText('7');
  await keyboardActivate(page, '#history-list [data-sequence="10"] .history-inspect');
  await expect(page.locator('#history-detail')).toContainText('Applied effects');
  await keyboardActivate(page, '#history-before'); await expect(page.locator('#history-detail')).toContainText('20/20');
  await keyboardActivate(page, '#history-after'); await expect(page.locator('#history-detail')).toContainText('13/20');
  await fitsViewport(page, '#history-close, #history-before, #history-after');
  await page.screenshot({ path: info.outputPath('keyboard-damage-fixture.jpg'), type: 'jpeg', quality: 85 });
  await page.keyboard.press('Escape'); await expect(page.locator('#history-open')).toBeFocused();
  expect(state.writes.every(one => state.shown.includes(one.asked))).toBe(true); expect(state.errors).toEqual([]);
});

for (const size of [{ width: 320, height: 568 }, { width: 844, height: 390 }, { width: 720, height: 500 }]) {
  test(`references and primary choices remain reachable at ${size.width}×${size.height}`, async ({ page }) => {
    test.setTimeout(90000);
    await page.setViewportSize(size); const state = await mount(page);
    await fitsViewport(page, '#upkeep-close'); await page.locator('#upkeep-close').click();
    await fitsViewport(page, '#help-close'); await page.locator('#help-close').click();
    await expect(page.locator('#planning-dialog')).toBeVisible(); await page.locator('[data-mutation="Warped"]').click();
    await fitsViewport(page, '#planning-confirm, #planning-close');
    await expect(page.locator('[data-mutation="Warped"]')).toBeInViewport();
    await page.locator('#planning-confirm').click(); await page.locator('#help-close').click();
    await teamTempos(page);
    for (let i = 0; i < 3; i++) await page.locator('[data-speed="Standard"]').click();
    await fitsViewport(page, '#planning-confirm, #planning-close'); await page.locator('#planning-confirm').click();
    await expect(page.locator('#planning-dialog')).toBeHidden();
    await combat(page, state); await openSpells(page);
    const last = page.locator('[data-spell="spell-17"]'); await last.scrollIntoViewIfNeeded(); await expect(last).toBeInViewport();
    await fitsViewport(page, '#combat-dialog-close'); await page.keyboard.press('Escape');
    await page.locator('#settings').click(); await page.locator('#concede-open').scrollIntoViewIfNeeded();
    await expect(page.locator('#concede-open')).toBeInViewport(); await page.keyboard.press('Escape');
    await page.locator('[data-creature="4"]').click();
    const known = page.locator('#inspect-spells .inspect-spell').last(); await known.scrollIntoViewIfNeeded(); await expect(known).toBeInViewport();
    await fitsViewport(page, '#inspect-close'); await page.keyboard.press('Escape');
    expect(state.errors).toEqual([]);
  });
}
