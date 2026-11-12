import { test, expect } from '@playwright/test';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { fitsViewport } from './fit.js';
import { WINDUP_MS, IMPACT_MS } from '../../table/battle-timing.js';
import { decided } from './decision.js';
test.beforeEach(returningBattlePlayer);
const creature = id => ({ id, name: `Creature ${id}`, health: 20, maxHealth: 20, energy: 4, totalDefense: 2,
  isAlive: true, conditions: [], knownSpells: [], acquiredTiers: [] });
const roster = [1, 2, 3, 4, 5, 6].map(creature);
const slot = (id, speed = 'Quick', initiative = 5) => ({ creature: id, owner: id < 4 ? 'Player1' : 'Player2', speed, initiative });
const timeline = [slot(1), slot(4), slot(2), slot(5, 'Standard', 3), slot(3, 'Standard', 3), slot(6, 'Standard', 1)];
const rollOffs = [{ creature: 1, rolls: [11, 18] }, { creature: 4, rolls: [11, 8] }, { creature: 2, rolls: [2] }];
const board = { slot: 'Player1', roundNumber: 2, phase: 'Combat', subPhase: 'Activation', activationCursor: 1,
  allies: roster.slice(0, 3), enemies: roster.slice(3), timeline, rollOffs };
const reveal = (sequence, kind = 'TimelineBuilt', slots = timeline) => ({ sequence, event: { kind, roundId: 2, timeline: { slots, rollOffs } } });
async function mount(page, overrides = {}) {
  const state = { errors: [], writes: [], shown: [], status: 200,
    view: { board, feed: [reveal(10)], feedNext: 11, options: {}, ...overrides } };
  page.on('pageerror', error => state.errors.push(error.message));
  await page.clock.install();
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' }));
    const original = window.fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      window.notifyOrder = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  });
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url());
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'order', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [], packages: [], rules: { combatDeclaration: 'AtActivation' } } });
    if (url.pathname.endsWith('/decision')) { state.writes.push(decided(request)); return route.fulfill({ status: 204 }); }
    if (url.pathname === '/api/seat/player1') {
      if (url.searchParams.has('shown')) state.shown.push(Number(url.searchParams.get('shown')));
      return route.fulfill({ status: state.status, json: state.view });
    }
    return route.fulfill({ status: 500 });
  });
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 10000));
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  // Freeze before the first connection so a started poll cannot owe a paused zero-delay refresh.
  await expect(page.locator('#loading')).toBeHidden(); await expect(page.locator('#connection')).toHaveText(/^LIVE/);
  const publish = async extra => { state.view = { ...state.view, ...extra }; await page.evaluate(() => window.notifyOrder()); };
  return { state, publish };
}
test('exact order shows bands, initiative, rerolls and creature inspection with no command', async ({ page }, info) => {
  const h = await mount(page); await expect(page.locator('#order-dialog')).toBeHidden();
  await page.locator('#order-open').click();
  await expect(page.locator('#order-list [data-order-creature]')).toHaveText([1, 4, 2, 5, 3, 6].map(id => `Creature ${id}`));
  await expect(page.locator('#order-list .order-band')).toHaveText(['Instinct', 'Precision']);
  await expect(page.locator('#order-list li').first()).toContainText('Initiative 5');
  await expect(page.locator('#order-list li').first()).toContainText('d20 11 → 18');
  await expect(page.locator('#order-list [aria-current="step"]')).toContainText('Creature 4');
  await fitsViewport(page, '#order-close');
  await page.screenshot({ path: info.outputPath('turn-order.jpg'), type: 'jpeg', quality: 85 });
  await page.locator('#order-list [data-order-creature="4"]').click();
  await expect(page.locator('#inspect-name')).toHaveText('Creature 4');
  await page.keyboard.press('Escape'); await expect(page.locator('#order-list [data-order-creature="4"]')).toBeFocused();
  await page.keyboard.press('Escape'); await expect(page.locator('#order-open')).toBeFocused();
  expect(h.state.writes).toEqual([]); expect(h.state.shown).toEqual([]); expect(h.state.errors).toEqual([]);
});
test('unrevealed opponent tempos are absent and same-team ties describe no roll honestly', async ({ page }) => {
  const h = await mount(page, { board: { ...board, subPhase: 'Speed', timeline: [], speedChoices: [{ creature: 4, speed: 'Quick' }] }, feed: [] });
  await page.locator('#order-open').click(); await expect(page.locator('#order-list li')).toHaveCount(0);
  await expect(page.locator('#order-summary')).toHaveText('Tempos stay private until the turn order is revealed.');
  await page.keyboard.press('Escape');
  await h.publish({ board: { ...board, subPhase: 'TieOrder', timeline: [slot(1), slot(2)], rollOffs: [] },
    waitingFor: 'TieOrder', waitingAsked: 20, options: { tieOrder: { ties: [[1, 2]] } }, feed: [reveal(11)] });
  await page.locator('#planning-open').click();
  await expect(page.locator('.tie-group')).toContainText('same-team-only tie needs none');
  await expect(page.locator('.tie-group')).toContainText('Your fixed places: 1, 2');
  await expect(page.locator('#order-dialog')).toBeHidden(); expect(h.state.writes).toEqual([]);
});
test('a tie draft keeps its interleaved places through an order reference, and reading Order consumes the reveal', async ({ page }) => {
  const h = await mount(page, { board: { ...board, subPhase: 'Speed', timeline: [] }, feed: [] });
  await h.publish({ board: { ...board, subPhase: 'TieOrder' }, waitingFor: 'TieOrder', waitingAsked: 20,
    options: { tieOrder: { ties: [[1, 2]] } }, feed: [reveal(11)] });
  await page.locator('#planning-open').click(); await page.locator('[data-tie="2"]').click();
  await expect(page.locator('.tie-group').first()).toContainText('#1 → Creature 2 · #3 → Creature 1');
  await expect(page.locator('.tie-group').first()).toContainText('Opponent places stay fixed: #2 Creature 4');
  await expect(page.locator('.tie-group').first()).toContainText('Recorded d20 rolls allocated these team places');
  await expect(page.locator('#order-dialog')).toBeHidden();
  // Read-only reference above the draft; no additional acknowledgement or decision.
  await page.locator('#planning-order').click();
  await expect(page.locator('#order-explanation')).toContainText('Provisional');
  await page.keyboard.press('Escape'); await expect(page.locator('#planning-order')).toBeFocused();
  await expect(page.locator('.tie-group').first()).toContainText('#1 → Creature 2');
  await page.locator('#planning-confirm').click();
  expect(h.state.writes).toEqual([{ kind: 'TieOrder', order: [2, 1], asked: 20 }]);
  await h.publish({ board: { ...board, subPhase: 'IntentSelection', timeline: [slot(2), slot(4), slot(1), ...timeline.slice(3)] },
    waitingFor: null, waitingAsked: null, options: {}, feed: [reveal(12, 'TiesOrdered', [slot(2), slot(4), slot(1), ...timeline.slice(3)])] });
  // The manual reference already consumed this reveal; it must not interrupt the next question again.
  await expect(page.locator('#order-dialog')).toBeHidden(); await expect(page.locator('#order-continue')).toBeHidden();
  await page.locator('#history-open').click();
  await page.locator('#history-list details').last().locator('summary').click();
  await expect(page.locator('#history-list')).toContainText('d20 11 → 18');
  await page.keyboard.press('Escape');
  // Read by hand, the order lets the seen mark pass it: coming back does not reveal it again.
  await page.reload(); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true'); await expect(page.locator('#loading')).toBeHidden();
  await page.clock.runFor(500); await expect(page.locator('#order-continue')).toBeHidden();
  expect(h.state.shown).toEqual([20]); expect(h.state.errors).toEqual([]);
});
test('a fresh reveal waits until tie ordering ends, opens once and never acknowledges the next question', async ({ page }, info) => {
  const h = await mount(page, { board: { ...board, subPhase: 'Speed', timeline: [] }, feed: [] });
  await h.publish({ board: { ...board, subPhase: 'TurnOrderResolution' }, feed: [reveal(11)] });
  await expect(page.locator('#timeline li')).toHaveCount(6); await expect(page.locator('#order-dialog')).toBeHidden();
  await h.publish({ board: { ...board, subPhase: 'TieOrder' }, waitingFor: 'TieOrder', waitingAsked: 20, options: { tieOrder: { ties: [[1, 2]] } }, feed: [reveal(11)] });
  await expect(page.locator('#planning-open')).toBeVisible(); await expect(page.locator('#order-dialog')).toBeHidden();
  await h.publish({ board: { ...board, subPhase: 'IntentSelection' }, waitingFor: 'Intent', waitingAsked: 21, options: {}, feed: [reveal(12, 'TiesOrdered')] });
  // The order reads on the field, a numbered badge per creature, and in the card (ADR 0144); no dialog covers it.
  await expect(page.locator('#order-continue')).toBeVisible(); await expect(page.locator('#order-dialog')).toBeHidden();
  await expect(page.locator('#reading-detail')).toContainText('Your team: 2 Instinct');
  await expect(page.locator('#actor-layer [data-creature="4"] .effect-chip[data-kind="order"]')).toContainText('2');
  // Each creature that rolled for the tie shows its last throw on a d20 beside it (ADR 0154), gone with the reading:
  // the three rolled in one tie, joined on the field, and each die says where its creature finished in it.
  await expect(page.locator('#battle')).toHaveAttribute('data-dice', '1:18:roll:tie1:1 4:8:roll:tie1:2 2:2:roll:tie1:3');
  await page.clock.runFor(800); await page.screenshot({ path: info.outputPath('roll-off-dice.png') });
  expect(h.state.shown).toEqual([]); await page.locator('#order-continue').click();
  await expect(page.locator('#actor-layer .effect-chip[data-kind="order"]')).toHaveCount(0);
  await expect(page.locator('#battle')).not.toHaveAttribute('data-dice', /./);
  await h.publish({ feed: [reveal(11), reveal(12, 'TiesOrdered')] });
  await expect(page.locator('#order-continue')).toBeHidden(); expect(h.state.errors).toEqual([]);
});
test('an opponent\'s action arriving while the order is read waits for Continue, then plays for its OK', async ({ page }) => {
  // Full frames rendered across a paced action outlast 30 s on a busy runner, as in the pacing spec.
  test.setTimeout(60000);
  const h = await mount(page, { board: { ...board, subPhase: 'Speed', timeline: [] }, feed: [] });
  await h.publish({ board: { ...board, subPhase: 'IntentSelection', activationCursor: -1 }, feed: [reveal(11)] });
  await expect(page.locator('#order-continue')).toBeVisible();
  const action = { sequence: 12, event: { kind: 'CombatActionResolved', roundId: 2, resolution: { action: { actor: 4, spell: 'strike', targets: [1] } },
    appliedOutcomes: [{ kind: 'DamageOutcome', target: 1, amount: 5 }],
    frame: { before: roster, after: roster.map(one => one.id === 1 ? { ...one, health: 15 } : one), timeline, rollOffs } } };

  await h.publish({ board: { ...board, activationCursor: 2 }, feed: [reveal(11), action] });
  await expect.poll(() => h.state.view.feed.length).toBe(2); await page.clock.runFor(1500);

  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', ''); await expect(page.locator('#order-continue')).toBeVisible();
  await page.locator('#order-continue').click();
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '12');
  await page.clock.runFor(WINDUP_MS + IMPACT_MS); await expect(page.locator('#playback-next')).toBeVisible(); await expect(page.locator('#reading-kind')).toContainText("Opponent's action");
  expect(h.state.errors).toEqual([]);
});
test('polls preserve focus and scrolling while progress marks completed, current, stunned and defeated slots', async ({ page }) => {
  const h = await mount(page); await page.locator('#order-open').click();
  await page.locator('#order-list [data-order-creature="6"]').focus();
  const scroll = await page.locator('#order-body').evaluate(node => node.scrollTop);
  await h.publish({ feed: [] }); await expect(page.locator('#order-list [data-order-creature="6"]')).toBeFocused();
  expect(await page.locator('#order-body').evaluate(node => node.scrollTop)).toBe(scroll);
  await h.publish({ board: { ...board, activationCursor: 3, allies: [{ ...roster[0], isAlive: false }, roster[1], { ...roster[2], isStunned: true }] } });
  await expect(page.locator('#order-list [data-state="Completed"]')).toHaveCount(3);
  await expect(page.locator('#order-list')).toContainText('Defeated'); await expect(page.locator('#order-list')).toContainText('Stunned');
  await expect(page.locator('#order-list [data-order-creature="6"]')).toBeFocused();
  await h.publish({ board: { ...board, subPhase: 'Cleanup' } }); await expect(page.locator('#order-list [data-state="Completed"]')).toHaveCount(6);
  expect(h.state.writes).toEqual([]);
});
for (const reason of ['access', 'hidden']) test(`${reason} clears the order and all creature details`, async ({ page }) => {
  const h = await mount(page); await page.locator('#order-open').click();
  if (reason === 'access') { h.state.status = 403; await h.publish({}); }
  else await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
  await expect(page.locator('#order-dialog')).toBeHidden(); await expect(page.locator('#order-list')).toBeEmpty();
  await expect(page.locator('#order-open')).toBeDisabled(); expect(h.state.errors).toEqual([]);
});
test('order controls fit short landscape, small portrait and enlarged text without horizontal page overflow', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'One viewport sweep.');
  await mount(page);
  for (const size of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(size); await page.clock.runFor(150);
    await fitsViewport(page, '#order-open, #help-open'); await page.locator('#order-open').click();
    await fitsViewport(page, '#order-close');
    await page.addStyleTag({ content: '#order-dialog p, #order-dialog button { font-size: 200% !important; }' });
    await fitsViewport(page, '#order-close');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.keyboard.press('Escape');
  }
});
test('the creature acting first waits for its turn order to be read before its spell choice opens', async ({ page }) => {
  // The tempos are drafted and the seat waits; then the order is built and the first question is this seat's.
  const waiting = { ...board, phase: 'Planning', subPhase: 'Speed', timeline: [], rollOffs: [], activationCursor: 0 };
  const h = await mount(page, { board: waiting, feed: [], feedNext: 10, waitingFor: null });
  await page.clock.runFor(100);
  await h.publish({ board: { ...board, activationCursor: 0 }, feed: [reveal(10)], feedNext: 11, waitingFor: 'Action', waitingCreature: 1, waitingAsked: 12,
    options: { action: { creature: 1, spells: [{ spell: 'strike', legalTargets: { candidates: [4, 5, 6], minTargets: 1, maxTargets: 1 } }] } } });
  await expect(page.locator('#order-continue')).toBeVisible(); await expect(page.locator('#combat-dialog')).toBeHidden();
  await page.clock.runFor(2000);
  await expect(page.locator('#order-continue')).toBeVisible(); await expect(page.locator('#combat-dialog')).toBeHidden();
  await page.locator('#order-continue').click();
  await expect(page.locator('#combat-dialog')).toBeVisible(); expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});
for (const away of ['hidden', 'reload']) test(`a seat ${away === 'hidden' ? 'that hid its tab' : 'that reloaded'} while its order was built reads it before its spell choice`, async ({ page }) => {
  // The tempos are drafted and the seat waits; it leaves, the order is built and its creature acts first.
  const waiting = { ...board, phase: 'Planning', subPhase: 'Speed', timeline: [], rollOffs: [], activationCursor: 0 };
  const h = await mount(page, { board: waiting, feed: [], feedNext: 10, waitingFor: null });
  await page.clock.runFor(100);
  const visibility = hidden => page.evaluate(value => { Object.defineProperty(document, 'hidden', { configurable: true, value }); document.dispatchEvent(new Event('visibilitychange')); }, hidden);
  if (away === 'hidden') await visibility(true);
  h.state.view = { ...h.state.view, board: { ...board, activationCursor: 0 }, feed: [reveal(10)], feedNext: 11, waitingFor: 'Action', waitingCreature: 1, waitingAsked: 12,
    options: { action: { creature: 1, spells: [{ spell: 'strike', legalTargets: { candidates: [4, 5, 6], minTargets: 1, maxTargets: 1 } }] } } };
  if (away === 'hidden') await visibility(false);
  else { await page.reload(); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true'); }
  await expect(page.locator('#order-continue')).toBeVisible(); await expect(page.locator('#combat-dialog')).toBeHidden();
  await page.locator('#order-continue').click();
  await expect(page.locator('#combat-dialog')).toBeVisible(); expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});

test('a strip entry lights its creature in blue and says why it holds its place, and a second tap puts it out', async ({ page }, info) => {
  const h = await mount(page);
  const entry = page.locator('#timeline [data-order-creature="4"]');
  // Each place says its initiative, and a d20 marks those a roll-off placed.
  await expect(entry).toContainText('⚡5'); await expect(entry.locator('.timeline-die')).toHaveText('d20');
  await expect(page.locator('#timeline [data-order-creature="6"] .timeline-die')).toHaveCount(0);

  await entry.click();

  await expect(entry).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('#battle')).toHaveAttribute('data-focus-lit', '4');
  await expect(page.locator('#actor-layer [data-creature="4"]')).toHaveAttribute('data-focus', 'focus');
  const why = page.locator('#order-why');
  await expect(why).toBeVisible();
  await expect(page.locator('#order-why-meta')).toHaveText('Opponent · 2 of 6');
  await expect(page.locator('#order-why-lines li')).toHaveText(['Instinct: acts before every Precision creature.', 'Initiative 5, tied with Creature 1 and Creature 2.',
    'A d20 roll-off broke it: highest first, matches roll again.']);
  await expect(page.locator('.order-why-die')).toHaveText(['18Creature 111 → 18', '8Creature 411 → 8', '2Creature 22']);
  await expect(page.locator('.order-why-die').first()).toHaveAttribute('aria-label', 'Creature 1: d20 11 then 18');
  await expect(page.locator('.order-why-die.is-self')).toContainText('Creature 4');
  await fitsViewport(page, '#order-why-close, #order-why-inspect');
  // The card stands away from the creature it lights, so the creature stays in view.
  await page.clock.runFor(1000);
  const [card, lit] = await Promise.all([why.boundingBox(), page.locator('#actor-layer [data-creature="4"] .caption').boundingBox()]);
  expect(card.y > lit.y + lit.height || card.y + card.height < lit.y || card.x > lit.x + lit.width || card.x + card.width < lit.x).toBe(true);
  await page.screenshot({ path: info.outputPath('order-why.jpg'), type: 'jpeg', quality: 85, animations: 'disabled' });

  await entry.click();

  await expect(entry).toHaveAttribute('aria-pressed', 'false');
  await expect(why).toBeHidden();
  await expect(page.locator('#battle')).not.toHaveAttribute('data-focus-lit', /./);
  await expect(page.locator('#actor-layer [data-creature="4"]')).not.toHaveAttribute('data-focus', /./);
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});

test('the why card opens the creature\'s sheet, and Escape or its close puts the light out', async ({ page }) => {
  const h = await mount(page);
  await page.locator('#timeline [data-order-creature="5"]').click();
  await expect(page.locator('#order-why-lines li').nth(1)).toHaveText('Initiative 3, tied with Creature 3.');

  await page.locator('#order-why-inspect').click();
  await expect(page.locator('#inspect-name')).toHaveText('Creature 5');
  await page.keyboard.press('Escape');
  await expect(page.locator('#order-why')).toBeVisible('closing the sheet keeps the light');
  await page.keyboard.press('Escape');
  await expect(page.locator('#order-why')).toBeHidden();

  await page.locator('#timeline [data-order-creature="6"]').click();
  await expect(page.locator('#order-why-lines li').nth(1)).toHaveText('Initiative 1: after Creature 3 (3).');
  await page.locator('#order-why-close').click();
  await expect(page.locator('#order-why')).toBeHidden(); await expect(page.locator('#timeline [data-order-creature="6"]')).toBeFocused();
  expect(h.state.writes).toEqual([]); expect(h.state.errors).toEqual([]);
});

test('ordering ties lights the tied creatures on the field with their places, and a tap on the field places one', async ({ page }, info) => {
  const h = await mount(page, { board: { ...board, subPhase: 'Speed', timeline: [] }, feed: [] });
  await h.publish({ board: { ...board, subPhase: 'TieOrder' }, waitingFor: 'TieOrder', waitingAsked: 20,
    options: { tieOrder: { ties: [[1, 2]] } }, feed: [reveal(11)] });
  await page.locator('#planning-open').click();
  // The docked sheet shortens the field: let it lay both rows out above the sheet.
  await page.clock.runFor(1000);
  const field = id => page.locator(`#actor-layer [data-creature="${id}"]`);
  await expect(field(1)).toHaveAttribute('data-focus', 'pending'); await expect(field(1).locator('.focus-badge')).toHaveText('?');
  await expect(field(2)).toHaveAttribute('data-focus', 'pending');
  await expect(field(4)).toHaveAttribute('data-focus', 'opponent'); await expect(field(4).locator('.focus-badge')).toHaveText('2');
  await expect(field(1).locator('.creature-tag')).toHaveText('TAP TO PLACE');
  await expect(page.locator('#battle')).toHaveAttribute('data-focus-lit', '1 2');
  await page.screenshot({ path: info.outputPath('ties-on-field.jpg'), type: 'jpeg', quality: 85 });

  await field(2).click();

  await expect(field(2)).toHaveAttribute('data-focus', 'placed'); await expect(field(2).locator('.focus-badge')).toHaveText('1');
  await expect(field(1).locator('.focus-badge')).toHaveText('3', 'the last one left takes the last place');
  await expect(page.locator('.tie-group').first()).toContainText('#1 → Creature 2 · #3 → Creature 1');
  await page.locator('#planning-confirm').click();
  expect(h.state.writes).toEqual([{ kind: 'TieOrder', order: [2, 1], asked: 20 }]);
  expect(h.state.errors).toEqual([]);
});
