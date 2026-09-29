import { test, expect } from '@playwright/test';

// What a test expects follows the layout's own widths (table.css: 760 and 1100), not a project's name.
const widthOf = page => page.viewportSize().width;
const isPhone = page => widthOf(page) <= 760;
const isLaptop = page => widthOf(page) >= 1100;

// The document against the viewport the test set: in mobile emulation `innerWidth` grows with the content, so
// comparing with it passes however far the page spills.
async function expectNoSidewaysScroll(page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(widthOf(page));
}

// Host-shaped responses keep layout regressions independent of simulation progress.
const cards = ['Basic Attack', 'Full Plate', 'Guard', 'Heavy Strike', 'Pummel', 'Wait'].map((name, index) => ({
  id: `spell-${index}`, name, cost: index === 5 ? 0 : 2, critical: index === 4 ? '77%' : '0%',
  targeting: 'One enemy', effects: ['Damage 7'],
}));
const view = {
  waitingFor: 'Speed', waitingCreature: 1, waitingAsked: 3, options: { speed: { missing: [1] } }, feed: [],
  guidance: cards.map(card => ({ spell: card.id, cost: card.cost, energyAfterCost: 4 - card.cost, quickCriticalChance: 0, standardCriticalChance: card.name === 'Pummel' ? .77 : 0 })),
  board: { roundNumber: 3, subPhase: 'Speed', allies: [{ id: 1, name: 'Creature 1', health: 6, maxHealth: 30, energy: 4, knownSpells: cards.map(card => card.id) }],
    enemies: [{ id: 2, health: 20, maxHealth: 30 }], intents: [], timeline: [] },
};
// Three families of three upgrades, each with one successor: the shape of the standard catalogue's packages.
const packages = ['North', 'East', 'West'].flatMap((family, root) => [
  { id: `tier:${family}:v1`, name: family, level: 1, prerequisites: [], spells: [cards[root].id], initiativeBonus: 1 },
  ...[1, 2, 3].flatMap(branch => [
    { id: `tier:${family}-${branch}:v1`, name: `${family} Adept ${branch}`, level: 2, prerequisites: [`tier:${family}:v1`], spells: [cards[3].id], initiativeBonus: 1 },
    { id: `tier:${family}-${branch}-master:v1`, name: `${family} Master ${branch}`, level: 3, prerequisites: [`tier:${family}-${branch}:v1`], spells: [cards[4].id], initiativeBonus: 3 },
  ]),
]);
const catalogue = {
  cards, packages, contentHash: 'fixture', rules: { teamSize: 3, energyPerRound: 2, evolutionPicksPerOpportunity: 2, evolutionInterval: 2, firstEvolutionRound: 1, roundCap: 20, criticalMultiplier: 2 },
  round: { subPhases: ['Upkeep', 'Evolution', 'EnergyGain', 'Speed', 'TurnOrderResolution', 'TieOrder', 'IntentSelection', 'Activation', 'Cleanup', 'Finalization'],
    orderings: ['Healing resolves before bleeding.', 'A critical is applied before defense is subtracted.'] },
};

test.beforeEach(async ({ page }) => {
  await page.route('**/api/catalogue', route => route.fulfill({ json: catalogue }));
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: view }));
  await page.goto('/table/?player1=layout-fixture');
  await page.locator('#pass-ready').click();
  await expect(page.locator('.speed-spell')).toHaveCount(6);
});

test('mobile header saves space and the guide stays above the sticky round bar', async ({ page }, info) => {
  await page.evaluate(() => scrollTo(0, 0));
  if (isPhone(page)) {
    expect((await page.locator('.masthead').boundingBox()).height).toBeLessThanOrEqual(50);
    await expect(page.locator('.brand')).toBeHidden();
    await expect(page.locator('#phase')).toBeHidden();
  }
  await page.locator('#shape > summary').click();
  await expect(page.locator('.guide-body')).toBeVisible();
  const aboveDock = await page.evaluate(() => {
    const guide = document.querySelector('.guide-body');
    const g = guide.getBoundingClientRect();
    const d = document.querySelector('#phase-dock').getBoundingClientRect();
    const x = (Math.max(g.left, d.left) + Math.min(g.right, d.right)) / 2;
    const y = (Math.max(g.top, d.top) + Math.min(g.bottom, d.bottom)) / 2;
    return y >= g.top && y <= g.bottom && guide.contains(document.elementFromPoint(x, y));
  });
  expect(aboveDock).toBe(true);
  await expectNoSidewaysScroll(page);
  await page.screenshot({ path: info.outputPath('round-guide.png'), animations: 'disabled' });
});

test('speed spells fit in one compact list and only positive critical chances stand out', async ({ page }, info) => {
  await page.locator('#phase-notice-close').click();
  await page.locator('#planning').scrollIntoViewIfNeeded();
  await page.screenshot({ path: info.outputPath('choose-speed.png'), animations: 'disabled' });
  const list = page.locator('.speed-reference').first();
  await list.scrollIntoViewIfNeeded();
  await expect(list.locator('.speed-critical')).toHaveCount(1);
  await expect(list.locator('.speed-critical')).toHaveText('✦ 77% crit');
  await expect(page.locator('#decision-guide')).not.toContainText('Basic Attack');
  await expect(list).not.toContainText('0%');
  expect((await list.boundingBox()).height).toBeLessThan(360);
  expect(await list.evaluate(node => node.scrollWidth <= node.clientWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath('speed-reference.png'), animations: 'disabled' });
  const pummel = list.locator('details').filter({ hasText: 'Pummel' });
  await pummel.locator('summary').focus();
  await page.keyboard.press('Enter');
  await expect(pummel).toHaveAttribute('open', '');
  await expect(pummel.locator('.speed-spell-details')).toContainText('Damage 7');
  await expect(page.locator('#choices')).toContainText('Quick');
  await expect(page.locator('#choices')).toContainText('Standard');
});

test('connection errors remain visible in the compact header', async ({ page }) => {
  await page.route('**/api/seat/player1**', route => route.abort());
  await expect(page.locator('#phase')).toHaveText('Connection lost · retrying…');
  await expect(page.locator('#phase')).toBeVisible();
});

test('the battlefield opens from the round bar over the page and closes back to the same place', async ({ page }, info) => {
  await expect(page.locator('#hand-board')).toHaveCount(0);
  await expect(page.locator('#board-move')).toHaveCount(0);
  const toggle = page.locator('#board-toggle');
  if (isLaptop(page)) {
    await expect(toggle).toBeHidden();
    await expect(page.locator('#board')).toBeInViewport();
    return;
  }
  await page.locator('#phase-notice-close').click();
  await page.locator('.speed-reference').first().scrollIntoViewIfNeeded();
  const scrolled = await page.evaluate(() => scrollY);
  await expect(toggle).toBeInViewport();
  await toggle.click();
  await expect(toggle).toHaveAttribute('aria-pressed', 'true');
  const [dock, board] = await Promise.all([page.locator('#phase-dock').boundingBox(), page.locator('#board').boundingBox()]);
  expect(Math.abs(board.y - (dock.y + dock.height))).toBeLessThanOrEqual(1);
  await expect(page.locator('#allies')).toContainText('Creature 1');
  await expectNoSidewaysScroll(page);
  await page.screenshot({ path: info.outputPath('battlefield-open.png'), animations: 'disabled' });
  await toggle.click();
  await expect(toggle).toHaveAttribute('aria-pressed', 'false');
  expect(await page.evaluate(() => scrollY)).toBe(scrolled);
});

test('the talent atlas lays its families out without overlap or sideways panning', async ({ page }, info) => {
  await page.locator('#phase-notice-close').click();
  await page.locator('#hand-talents').click();
  await expect(page.locator('.tree-node')).toHaveCount(packages.length);
  const boxes = await page.locator('.tree-node').evaluateAll(nodes => nodes.map(node => node.getBoundingClientRect().toJSON()));
  for (const [index, one] of boxes.entries()) {
    for (const other of boxes.slice(index + 1)) {
      const overlap = one.left < other.right - 1 && other.left < one.right - 1 && one.top < other.bottom - 1 && other.top < one.bottom - 1;
      expect(overlap).toBe(false);
    }
  }
  if (!isLaptop(page)) {
    expect(await page.locator('#mat').evaluate(node => node.scrollWidth <= node.clientWidth)).toBe(true);
  }
  await page.screenshot({ path: info.outputPath('talent-atlas.png'), animations: 'disabled' });
  await page.locator('.tree-node').filter({ hasText: 'East Master 2' }).click();
  await expect(page.locator('.talent-inspector')).toContainText('East Master 2 · Tier 3');
  await expect(page.locator('.talent-inspector .talent-class')).toBeInViewport();
});

test('a phone lists the castable spells as compact rows, the chosen one with its declare cue', async ({ page }, info) => {
  const intent = { ...view, waitingFor: 'Intent', options: { intent: { creatures: [{ creature: 1, castableSpells: cards.map(card => card.id) }] } },
    board: { ...view.board, subPhase: 'IntentSelection' } };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: intent }));
  await expect(page.locator('#own-hand .held.offered')).toHaveCount(cards.length);
  const heights = await page.locator('#own-hand .held.offered').evaluateAll(nodes => nodes.map(node => node.getBoundingClientRect().height));
  if (isPhone(page)) expect(Math.max(...heights)).toBeLessThan(140);
  await expectNoSidewaysScroll(page);
  const pummel = page.locator('#own-hand .held.offered').filter({ hasText: 'Pummel' });
  await pummel.click();
  await expect(pummel).toContainText('Tap again to declare');
  await expect(pummel.locator('.card-availability')).toBeVisible();
  await page.screenshot({ path: info.outputPath('choose-spell.png'), animations: 'disabled' });
});

test('on a phone the speed and spell decisions remain visible while only their spellbook scrolls', async ({ page }, info) => {
  test.skip(!isPhone(page));
  for (const kind of ['Speed', 'Intent']) {
    const current = kind === 'Speed' ? view : { ...view, waitingFor: 'Intent',
      options: { intent: { creatures: [{ creature: 1, castableSpells: cards.map(card => card.id) }] } },
      board: { ...view.board, subPhase: 'IntentSelection' } };
    await page.route('**/api/seat/player1**', route => route.fulfill({ json: current }));
    await expect(page.locator('#decision')).toHaveAttribute('data-kind', kind);
    await expect(page.locator(kind === 'Speed' ? '.speed-spell' : '#own-hand .held')).toHaveCount(cards.length);
    const before = await page.locator('#decision').boundingBox();
    const scroller = page.locator('#planning .hand-section');
    expect(await scroller.evaluate(node => getComputedStyle(node).overflowY)).toBe('auto');
    const overflows = await scroller.evaluate(node => node.scrollHeight > node.clientHeight);
    await scroller.evaluate(node => { node.scrollTop = node.scrollHeight; });
    if (overflows) expect(await scroller.evaluate(node => node.scrollTop)).toBeGreaterThan(0);
    const after = await page.locator('#decision').boundingBox();
    expect(Math.abs(after.y - before.y)).toBeLessThan(2);
    await expect(page.locator('#choices')).toBeInViewport();
    await expectNoSidewaysScroll(page);
    await page.screenshot({ path: info.outputPath(`${kind.toLowerCase()}-scroll.png`), animations: 'disabled' });
  }
});

test('package budget stays above a separately scrolling choice list on a phone', async ({ page }, info) => {
  test.skip(!isPhone(page));
  const evolving = { ...view, waitingFor: 'Evolution', options: { evolution: { remainingPicks: 2,
    creatures: [{ creature: 1, availableTiers: packages.map(item => item.id) }] } },
    board: { ...view.board, subPhase: 'Evolution', evolutionChoices: [] } };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: evolving }));
  await expect(page.locator('#decision')).toHaveAttribute('data-kind', 'Evolution');
  const budget = page.locator('#evolution-budget');
  const before = await budget.boundingBox();
  const scroller = page.locator('#decision .choice-cards');
  expect(await scroller.evaluate(node => node.scrollHeight > node.clientHeight && getComputedStyle(node).overflowY === 'auto')).toBe(true);
  await scroller.evaluate(node => { node.scrollTop = node.scrollHeight; });
  expect(await scroller.evaluate(node => node.scrollTop)).toBeGreaterThan(0);
  const heights = await scroller.locator('.package-card').evaluateAll(nodes => nodes.map(node => node.getBoundingClientRect().height));
  expect(Math.min(...heights)).toBeGreaterThan(48);
  const after = await budget.boundingBox();
  expect(Math.abs(after.y - before.y)).toBeLessThan(2);
  await expect(budget).toBeInViewport();
  await expectNoSidewaysScroll(page);
  await page.screenshot({ path: info.outputPath('packages-scroll.png'), animations: 'disabled' });
});

test('battlefield names public opponent packages and upkeep popup separates ticks from lasting effects', async ({ page }, info) => {
  const enemy = { ...view.board.enemies[0], acquiredTiers: [packages[0].id],
    conditions: [{ effect: { kind: 'DefenseModifier', amount: -2 }, remainingRounds: 2 }] };
  const applied = { ...view, board: { ...view.board, roundNumber: 4, enemies: [enemy] },
    feed: [{ sequence: 5, event: { kind: 'OngoingEffectsApplied', roundId: 4,
      regenerationTicks: [{ creature: 1, healed: 2 }], bleedTicks: [{ creature: 2, damage: 1 }] } }] };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: applied }));
  await expect(page.locator('#upkeep')).toBeVisible();
  await expect(page.locator('#phase-notice-changes')).toContainText('still active');
  await expect(page.locator('#phase-notice-changes')).toContainText('+2 HP');
  await page.locator('#upkeep-label').click();
  await expect(page.locator('#upkeep-conditions')).toContainText('DefenseModifier -2');
  if (!isLaptop(page)) await page.locator('#board-toggle').click();
  await expect(page.locator('#enemies .enemy-packages')).toContainText('North');
  await expectNoSidewaysScroll(page);
  await page.screenshot({ path: info.outputPath('opponent-packages-upkeep.png'), animations: 'disabled' });
});

test('the phase popup recaps both teams packages after the unlock opportunity', async ({ page }, info) => {
  test.skip(!isPhone(page));
  let served = { ...view, waitingFor: 'Evolution', options: { evolution: { remainingPicks: 2,
    creatures: [{ creature: 1, availableTiers: [packages[0].id] }] } },
    board: { ...view.board, subPhase: 'Evolution', evolutionChoices: [] } };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: served }));
  await expect(page.locator('#decision')).toHaveAttribute('data-kind', 'Evolution');
  served = structuredClone(served);
  served.waitingAsked = 4;
  served.options.evolution.remainingPicks = 1;
  served.board.allies[0].acquiredTiers = [packages[0].id];
  served.board.evolutionChoices = [{ creature: 1, tier: packages[0].id }];
  await expect(page.locator('#evolution-budget')).toContainText('1 / 2 team picks remaining');
  served = structuredClone(served);
  served.board.enemies[0].acquiredTiers = [packages[7].id];
  await expect(page.locator('#enemies .enemy-packages')).toContainText('East');
  served = { ...served, waitingFor: 'Speed', waitingCreature: 1, waitingAsked: 5,
    options: view.options, board: { ...served.board, subPhase: 'Speed' } };
  await expect(page.locator('#phase-notice-title')).toHaveText('Round 3 · Packages unlocked');
  await expect(page.locator('#phase-notice-changes')).toContainText('Creature 1 · yours');
  await expect(page.locator('#phase-notice-changes')).toContainText('Creature 2 · opponent');
  await page.screenshot({ path: info.outputPath('packages-unlocked.png'), animations: 'disabled' });
});

test('below a laptop the battlefield is in the round bar at a glance, not at the bottom of the page', async ({ page }) => {
  if (isLaptop(page)) {
    await expect(page.locator('#mini-board')).toBeHidden();
    await expect(page.locator('#board')).toBeVisible();
    return;
  }
  await expect(page.locator('#board')).toBeHidden();
  await expect(page.locator('#mini-board .mini-creature')).toHaveCount(2);
  await expect(page.locator('#mini-enemies .mini-creature')).toHaveAttribute('aria-label', /^Creature 2, opponent, 20\/30 health/);
  const mini = await page.locator('#mini-board').boundingBox();
  expect(mini.x + mini.width).toBeLessThanOrEqual(widthOf(page));
  await expectNoSidewaysScroll(page);
  await page.locator('#phase-notice-close').click();
  await page.evaluate(() => scrollTo(0, document.documentElement.scrollHeight));
  await expect(page.locator('#mini-board')).toBeInViewport();
});

test('a spell aimed at an ally is aimed from the round bar, and the battlefield opens at the allies', async ({ page }, info) => {
  const creature = (id, health) => ({ id, health, maxHealth: 30, energy: 2 });
  const target = { ...view, waitingFor: 'Target', options: { target: { actor: 1, spell: cards[2].id, legalTargets: { candidates: [1, 5], minTargets: 1, maxTargets: 1 } } },
    board: { ...view.board, subPhase: 'Activation', allies: [{ ...view.board.allies[0], name: undefined }, creature(5, 30), creature(6, 30)],
      enemies: [creature(2, 20), creature(3, 30), creature(4, 30)] } };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: target }));
  await expect(page.locator('#decision')).toHaveAttribute('data-kind', 'Target');
  await page.locator('#phase-notice-close').click();
  if (isLaptop(page)) {
    await page.locator('[data-focus="target-5"]').click();
    await expect(page.locator('#choices')).toContainText('Cast on 1 of 1');
    return;
  }
  await expect(page.locator('#mini-enemies .mini-creature.legal')).toHaveCount(0);
  await page.locator('#mini-allies [aria-label^="Creature 5,"]').click();
  // Picked, the chip says what its second tap does, and so does the sheet.
  await expect(page.locator('#mini-allies [aria-label^="Creature 5,"]')).toHaveAttribute('aria-label', /selected target, tap again to cast$/);
  await expect(page.locator('#mini-allies .mini-creature.picked')).toHaveCount(1);
  await expect(page.locator('#choices')).toContainText('Tap a selected target again, or Cast, to confirm.');
  await expect(page.locator('#choices')).toContainText('Cast on 1 of 1');
  await page.screenshot({ path: info.outputPath('ally-target.png'), animations: 'disabled' });
  await page.locator('#board-toggle').click();
  await expect(page.locator('#allies .creature.legal').first()).toBeInViewport();
  expect(await page.locator('#board').evaluate(node => node.scrollTop)).toBeGreaterThan(0);
  await page.screenshot({ path: info.outputPath('ally-target-battlefield.png'), animations: 'disabled' });
});

// What resolved since the seat's last move, with the longest lines an action produces, in every question.
const resolved = (sequence, actor, targets, outcomes) => ({ sequence, round: 3, subPhase: 'Activation', event: { kind: 'CombatActionResolved', roundId: 3,
  resolution: { action: { actor, spell: cards[3].id, targets } },
  appliedOutcomes: outcomes.map(target => ({ kind: 'ConditionOutcome', target, effect: { kind: 'DefenseModifier', amount: -3, duration: { rounds: 2 } } })) } });

for (const kind of ['Speed', 'Evolution', 'Target']) {
  test(`long resolved actions are read one at a time and wrap inside the sheet during ${kind}`, async ({ page }, info) => {
    const creature = (id, health) => ({ id, health, maxHealth: 30, energy: 2 });
    const board = { ...view.board, subPhase: kind === 'Target' ? 'Activation' : kind, allies: [view.board.allies[0], creature(5, 30), creature(6, 30)], enemies: [creature(2, 20), creature(3, 30), creature(4, 30)],
      evolutionChoices: [] };
    const options = { Speed: view.options, Evolution: { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: [packages[0].id] }] } },
      Target: { target: { actor: 1, spell: cards[2].id, legalTargets: { candidates: [2, 3], minTargets: 1, maxTargets: 1 } } } }[kind];
    const feed = [resolved(20, 5, [2, 3, 4], [2, 3, 4]), resolved(21, 2, [1, 5, 6], [1, 5, 6])];
    await page.route('**/api/seat/player1**', route => route.fulfill({ json: { ...view, waitingFor: kind, options, board, feed, feedNext: 22 } }));
    await expect(page.locator('#decision')).toHaveAttribute('data-kind', kind);
    await page.locator('#phase-notice-close').click();
    // The opponent's action is read first, one OK each, and only then is the question offered.
    await expect(page.locator('#combat-step')).toBeVisible();
    await expect(page.locator('#combat-line')).toContainText('Heavy Strike');
    await expect(page.locator('#choices')).toBeEmpty();
    await expectNoSidewaysScroll(page);
    await page.locator('#decision').scrollIntoViewIfNeeded();
    await page.screenshot({ path: info.outputPath(`opponent-action-${kind}.png`), animations: 'disabled' });
    await page.locator('#combat-step-controls button').first().click();
    await expect(page.locator('#combat-step')).toBeHidden();
    await expect(page.locator('#live-action')).toHaveCount(0);
    await expect(page.locator('#choices')).not.toBeEmpty();
    await expectNoSidewaysScroll(page);
    await page.locator('#decision').scrollIntoViewIfNeeded();
    await page.screenshot({ path: info.outputPath(`resolved-${kind}.png`), animations: 'disabled' });
  });
}

test('revealed speeds open the turn order from the round bar, and the spell being chosen says when it acts', async ({ page }, info) => {
  const creature = (id, health) => ({ id, health, maxHealth: 30, energy: 2 });
  const timeline = [[2, 'Player2', 'Quick', 9], [1, 'Player1', 'Quick', 8], [5, 'Player1', 'Standard', 7], [3, 'Player2', 'Standard', 6], [6, 'Player1', 'Standard', 5], [4, 'Player2', 'Standard', 3]]
    .map(([id, owner, speed, initiative]) => ({ creature: id, owner, speed, initiative }));
  const intent = { ...view, waitingFor: 'Intent', options: { intent: { creatures: [{ creature: 1, castableSpells: cards.map(card => card.id) }] } },
    board: { ...view.board, slot: 'Player1', subPhase: 'IntentSelection', allies: [view.board.allies[0], creature(5, 30), creature(6, 30)], enemies: [creature(2, 20), creature(3, 30), creature(4, 30)],
      timeline, rollOffs: [{ creature: 3, rolls: [14] }] } };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: intent }));
  await expect(page.locator('#order')).toBeVisible();
  // A laptop already shows the turn order beside the battlefield: the panel waits to be asked for there.
  if (isLaptop(page)) {
    await expect(page.locator('#order-panel')).toBeHidden();
    await page.locator('#order-label').click();
  }
  await expect(page.locator('#order-panel')).toBeVisible();
  await expect(page.locator('#order-theirs')).toHaveText('Opponent: Creature 2 Quick · Creature 3 Standard · Creature 4 Standard');
  await expect(page.locator('#order-list li')).toHaveCount(6);
  await expect(page.locator('#decision-turn')).toHaveText('Acts 2 of 6 · Quick');
  const panel = await page.locator('#order-panel').boundingBox();
  expect(panel.x).toBeGreaterThanOrEqual(0);
  expect(panel.x + panel.width).toBeLessThanOrEqual(widthOf(page));
  await expectNoSidewaysScroll(page);
  await page.screenshot({ path: info.outputPath('turn-order.png'), animations: 'disabled' });
  await page.locator('#order-label').click();
  await expect(page.locator('#order-panel')).toBeHidden();
  await page.locator('#announcements-label').click();
  await page.locator('#announcement-list .announcement-mute button').click();
  await expect(page.locator('#announcements-label')).toContainText('muted');
});
