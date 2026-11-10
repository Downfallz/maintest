import { test, expect } from '@playwright/test';
import { classicTable } from './classic-table-fixture.js';

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
  type: [0, 3, 4].includes(index) ? 'Offensive' : index === 5 ? 'Passive' : 'Defensive',
  targeting: 'One enemy', effects: ['Damage 7'],
  criticalNote: 'Instinct cannot crit. Precision can, and a critical multiplies only direct damage or healing on its targets.',
}));
const view = {
  waitingFor: 'Speed', waitingCreature: 1, waitingAsked: 3, options: { speed: { missing: [1] } }, feed: [],
  guidance: cards.map(card => ({ spell: card.id, cost: card.cost, energyAfterCost: 4 - card.cost, quickCriticalChance: 0, standardCriticalChance: card.name === 'Pummel' ? .77 : 0 })),
  board: { roundNumber: 3, subPhase: 'Speed', allies: [{ id: 1, name: 'Creature 1', health: 6, maxHealth: 30, energy: 4, currentInitiative: 8, knownSpells: cards.map(card => card.id) }],
    enemies: [{ id: 2, health: 20, maxHealth: 30, currentInitiative: 5 }], intents: [], timeline: [] },
};
// Three families of three upgrades, each with one successor: the shape of the standard catalogue's packages.
const packages = ['North', 'East', 'West'].flatMap((family, root) => [
  { id: `tier:${family}:v1`, name: family, level: 1, prerequisites: [], spells: [cards[root].id], initiativeBonus: 1, tags: ['Pressure', 'Defense'] },
  ...[1, 2, 3].flatMap(branch => [
    { id: `tier:${family}-${branch}:v1`, name: `${family} Adept ${branch}`, level: 2, prerequisites: [`tier:${family}:v1`], spells: [cards[3].id], initiativeBonus: 1 },
    { id: `tier:${family}-${branch}-master:v1`, name: `${family} Master ${branch}`, level: 3, prerequisites: [`tier:${family}-${branch}:v1`], spells: [cards[4].id], initiativeBonus: 3,
      tags: ['Sacrifice', 'Burst', 'Sustain'] },
  ]),
]);
const catalogue = {
  cards, packages, contentHash: 'fixture', rules: { teamSize: 3, energyPerRound: 2, evolutionPicksPerOpportunity: 2, evolutionInterval: 2, firstEvolutionRound: 1, roundCap: 20, criticalMultiplier: 2 },
  round: { subPhases: ['Upkeep', 'Evolution', 'EnergyGain', 'Speed', 'TurnOrderResolution', 'TieOrder', 'IntentSelection', 'Activation', 'Cleanup', 'Finalization'],
    orderings: ['Healing resolves before bleeding.', 'A critical is applied before defense is subtracted.'] },
  effects: [{ kind: 'DefenseDebuff', glyph: '◇', harmful: true }, { kind: 'Bleed', glyph: '♥', harmful: true }, { kind: 'Regeneration', glyph: '♥', harmful: false }],
};

test.beforeEach(classicTable);
test.beforeEach(async ({ page }) => {
  await page.route('**/api/catalogue', route => route.fulfill({ json: catalogue }));
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: view }));
  await page.goto('/table/?player1=layout-fixture');
  await expect(page.locator('.speed-spell')).toHaveCount(6);
});

test('the shared presentation keeps a local Action choice through a poll and discards it at the next asking', async ({ page }) => {
  let served = { ...view, waitingFor: 'Action', waitingAsked: 4,
    options: { action: { actor: 1, spells: [0, 3].map(index => ({ actor: 1, spell: cards[index].id,
      legalTargets: { minTargets: 1, maxTargets: 1, candidates: [2] } })) } },
    board: { ...view.board, subPhase: 'Activation' } };
  let reads = 0;
  const decisions = [];
  page.on('request', request => {
    if (request.method() === 'POST' && request.url().endsWith('/decision')) decisions.push(request.postData());
  });
  await page.route('**/api/seat/player1**', route => { reads++; return route.fulfill({ json: served }); });
  await expect(page.locator('#decision')).toHaveAttribute('data-kind', 'Intent');
  await page.locator('#phase-notice-close').click();
  await page.locator('#own-hand .held.offered').first().click();
  await expect(page.locator('#decision')).toHaveAttribute('data-kind', 'Target');
  const selectedAt = reads;
  await expect.poll(() => reads).toBeGreaterThan(selectedAt);
  await expect(page.locator('#decision')).toHaveAttribute('data-kind', 'Target');
  served = { ...served, waitingAsked: 5 };
  await expect(page.locator('#decision')).toHaveAttribute('data-kind', 'Intent');
  await expect(page.locator('#own-hand .held.chosen')).toHaveCount(0);
  expect(decisions).toEqual([]);
});

test('the table draws the timeline in the host\'s order, with its cursor and its rolls, off the shared board model', async ({ page }) => {
  // The expectations are the fixture's own values, not the module's reading of them: the page draws through
  // boardPresentation, so asking the module what to expect would agree with any mistake it made.
  const board = { ...view.board, slot: 'player1', subPhase: 'Activation', activationCursor: 1,
    timeline: [{ creature: 2, owner: 'player2', speed: 'Quick', initiative: 5 },
      { creature: 1, owner: 'player1', speed: 'Standard', initiative: 8 }],
    rollOffs: [{ creature: 1, rolls: [2, 18] }] };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: { ...view, board } }));
  await expect(page.locator('#timeline .slot')).toHaveCount(2);
  await expect(page.locator('#timeline .slot > strong')).toHaveText(['Creature 2', 'Creature 1']);
  await expect(page.locator('#timeline .slot.now > strong')).toHaveText('Creature 1');
  await expect(page.locator('#timeline .slot.enemy > strong')).toHaveText('Creature 2');
  await expect(page.locator('#timeline .roll')).toHaveText(['d20 2 → 18']);
  await expect(page.locator('#allies .health .number')).toHaveText(board.allies.map(creature => `${creature.health}/${creature.maxHealth} HP`));
  await expect(page.locator('#enemies .health .number')).toHaveText(board.enemies.map(creature => `${creature.health}/${creature.maxHealth} HP`));
});

// The motion tokens (UX 01): buttons, creatures and held cards ease their colours over --motion-fast, and a
// person who asks the system for reduced motion gets every token at zero and no transition or animation that
// lasts, whether or not the effect was built on the tokens.
test('interaction transitions use the motion tokens, and reduced motion makes every one immediate', async ({ page }) => {
  const read = () => page.evaluate(() => {
    const root = getComputedStyle(document.documentElement);
    const button = getComputedStyle(document.querySelector('#board-toggle'));
    return { normal: root.getPropertyValue('--motion-normal').trim(), duration: button.transitionDuration, easing: button.transitionTimingFunction };
  });

  const moving = await read();
  expect(moving.normal).toBe('220ms');
  // Border and background over --motion-fast; UX 02 adds the shadow, and the press over --motion-instant.
  expect(moving.duration).toBe('0.12s, 0.12s, 0.12s, 0.08s');
  expect(moving.easing).toContain('cubic-bezier(0.2, 0, 0, 1)');

  await page.emulateMedia({ reducedMotion: 'reduce' });
  const still = await read();
  expect(still.normal).toBe('0ms');
  expect(still.duration.split(', ').map(parseFloat).every(seconds => seconds < 0.001)).toBe(true);
});

// Buttons (UX 02): a pressed toggle says so in gold, but a component's own selected look stays its own -- a
// selected node of the mutation tree keeps its family's colour -- and a pointer that can hover lifts a button.
test('pressed buttons are marked without overriding a component\'s own selected colour', async ({ page }) => {
  await expect(page.locator('#tab-board')).toHaveAttribute('aria-pressed', 'true');
  expect(await page.locator('#tab-board').evaluate(node => getComputedStyle(node).borderBottomColor)).toBe('rgb(231, 193, 131)');

  await page.locator('#hand-talents').click();
  const node = page.locator('#mat .tree-node').first();
  await node.click();
  await expect(node).toHaveAttribute('aria-pressed', 'true');
  // Off the node: a hovered button is gold whatever it is. Polled: the border eases over --motion-fast.
  await page.mouse.move(0, 0);
  await expect.poll(() => node.evaluate(button => {
    const probe = document.createElement('span');
    probe.style.color = getComputedStyle(button).getPropertyValue('--class-color');
    document.body.append(probe);
    const family = getComputedStyle(probe).color;
    probe.remove();
    return getComputedStyle(button).borderLeftColor === family;
  })).toBe(true);
});

test('a pointer that can hover lifts an enabled button', async ({ page }) => {
  test.skip(isPhone(page) || !(await page.evaluate(() => matchMedia('(hover: hover) and (pointer: fine)').matches)));
  const button = page.locator('#tab-mat');
  const before = await button.evaluate(node => getComputedStyle(node).boxShadow);
  await button.hover();
  await expect.poll(() => button.evaluate(node => getComputedStyle(node).boxShadow)).not.toBe(before);
});

// Panels (UX 05) fade in when they open, and only open: the full battlefield on a phone or tablet and the
// Mutation Tree fade without moving, the phase announcement keeps the entrance table.js plays for it, and
// none of it runs for a person who asked for reduced motion.
test('panels fade in as they open, and not at all with reduced motion', async ({ page }) => {
  const animation = selector => page.locator(selector).evaluate(node => getComputedStyle(node).animationName);
  expect(await animation('#phase-notice')).toBe('none');

  if (!isLaptop(page)) {
    await page.locator('#board-toggle').click();
    expect(await animation('#board')).toBe('panel-fade-in');
    await page.locator('#board-toggle').click();
  }
  await page.locator('#hand-talents').click();
  expect(await animation('#talent-window')).toBe('panel-fade-in');

  await page.emulateMedia({ reducedMotion: 'reduce' });
  expect(await animation('#talent-window')).toBe('none');
});

test('the lobby\'s dialog opens with the table\'s entrance', async ({ page }) => {
  await page.route('**/api/**', route => route.fulfill({ json: {} }));
  await page.goto('/table/lobby.html');
  const name = await page.evaluate(() => {
    const dialog = document.getElementById('create-dialog');
    dialog.showModal();
    return getComputedStyle(dialog).animationName;
  });

  expect(name).toBe('panel-enter');
});

test('mobile header saves space and the guide stays above the sticky round bar', async ({ page }, info) => {
  await page.evaluate(() => scrollTo(0, 0));
  if (isPhone(page)) {
    expect((await page.locator('.masthead:not(.home-header)').boundingBox()).height).toBeLessThanOrEqual(50);
    await expect(page.locator('.masthead:not(.home-header) .brand')).toBeHidden();
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

test('the classic match ignores game shortcuts while its player menu owns focus', async ({ page }) => {
  await page.route('**/api/me', route => route.fulfill({ json: { accounts: 'dev', account: { id: 'acct_one', username: 'Marc' } } }));
  const decisions = [];
  page.on('request', request => {
    if (request.method() === 'POST' && request.url().endsWith('/decision')) decisions.push(request.url());
  });
  await page.reload();
  await expect(page.locator('#account-name')).toHaveText('Marc');
  await expect(page.locator('.speed-spell')).toHaveCount(6);
  const menu = page.locator('#home-menu-toggle');
  if (await menu.isVisible()) await menu.click();
  await page.locator('#app-settings-open').click();
  await page.keyboard.press('1');
  await page.keyboard.press('Escape');
  await expect(page.locator('#app-settings-dialog')).toBeHidden();
  await expect(page.locator('.speed-spell')).toHaveCount(6);
  expect(decisions).toEqual([]);
});

test('speed spells fit in one compact list and only positive critical chances stand out', async ({ page }, info) => {
  await page.locator('#phase-notice-close').click();
  await page.locator('#planning').scrollIntoViewIfNeeded();
  await page.screenshot({ path: info.outputPath('choose-speed.png'), animations: 'disabled' });
  const list = page.locator('.speed-reference').first();
  await list.scrollIntoViewIfNeeded();
  await expect(list.locator('.speed-critical')).toHaveCount(1);
  await expect(list.locator('.speed-critical')).toHaveText('✦ 77% crit');
  await expect(list.locator('.spell-group')).toHaveCount(3);
  if (isPhone(page)) {
    const columns = await list.locator('.spell-grid').first().evaluate(node => getComputedStyle(node).gridTemplateColumns.split(' ').length);
    expect(columns).toBe(2);
  }
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
  await expect(page.locator('#choices')).toContainText('Instinct');
  await expect(page.locator('#choices')).toContainText('Precision');
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

test('the Mutation Tree lays its families out without overlap or sideways panning', async ({ page }, info) => {
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
  await expect(page.locator('.talent-inspector')).toContainText('East Master 2 · Stage 3');
  await expect(page.locator('.talent-inspector .talent-class')).toBeInViewport();
});

test('spell rows stay put between the tap that chooses and the tap that declares', async ({ page }, info) => {
  const intent = { ...view, waitingFor: 'Intent', options: { intent: { creatures: [{ creature: 1, castableSpells: cards.map(card => card.id) }] } },
    board: { ...view.board, subPhase: 'IntentSelection' } };
  const decisions = [];
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: intent }));
  await page.route('**/api/seat/player1/decision', route => {
    decisions.push(route.request().postDataJSON());
    return route.fulfill({ status: 204 });
  });
  await expect(page.locator('#own-hand .held.offered')).toHaveCount(cards.length);
  const heights = await page.locator('#own-hand .held.offered').evaluateAll(nodes => nodes.map(node => node.getBoundingClientRect().height));
  if (isPhone(page)) expect(Math.max(...heights)).toBeLessThan(140);
  await expectNoSidewaysScroll(page);
  const pummel = page.locator('#own-hand .held.offered').filter({ hasText: 'Pummel' });
  await pummel.scrollIntoViewIfNeeded();
  const before = await pummel.boundingBox();
  const headerBefore = await page.locator('#decision').boundingBox();
  await pummel.click();
  await expect(pummel).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('#decision-guide')).toBeEmpty();
  await expect(page.locator('#choices .declare-spell')).toHaveText('Declare Pummel');
  // A phone's phase announcement sits exactly on the Declare button; choosing a card closes it, so the button
  // the choice just named is the thing under the finger, not the notice.
  await expect(page.locator('#phase-notice')).toBeHidden();
  const declare = await page.locator('#choices .declare-spell').boundingBox();
  expect(await page.evaluate(([x, y]) => Boolean(document.elementFromPoint(x, y)?.closest('.declare-spell')),
    [declare.x + declare.width / 2, declare.y + declare.height / 2])).toBe(true);
  expect(decisions).toHaveLength(0);
  // The chosen card is where it was, so the second tap lands on it: it declares.
  const after = await pummel.boundingBox();
  expect(Math.abs(after.y - before.y)).toBeLessThan(2);
  if (isPhone(page)) {
    await expect(pummel.locator('.card-critical-note')).toBeHidden();
    await expect(pummel.locator('.card-availability')).toBeHidden();
    expect(Math.abs(after.height - before.height)).toBeLessThan(2);
    expect(Math.abs((await page.locator('#decision').boundingBox()).height - headerBefore.height)).toBeLessThan(2);
  }
  // Marked where it is (UX 03): a gold ring and glow, no lift.
  expect(await pummel.evaluate(node => getComputedStyle(node).boxShadow)).toContain('rgb(231, 193, 131)');
  expect(await pummel.evaluate(node => getComputedStyle(node).transform)).toBe('none');
  await page.screenshot({ path: info.outputPath('choose-spell.png'), animations: 'disabled' });
  await pummel.click();
  await expect.poll(() => decisions.length).toBe(1);
  expect(decisions[0]).toMatchObject({ kind: 'Intent', creature: 1, spell: 'spell-4', asked: 3 });
});

test('phone spell choices use two compact columns with a full detail switch', async ({ page }, info) => {
  test.skip(!isPhone(page));
  const intent = { ...view, waitingFor: 'Intent', options: { intent: { creatures: [{ creature: 1, castableSpells: cards.map(card => card.id) }] } },
    board: { ...view.board, subPhase: 'IntentSelection' } };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: intent }));
  await expect(page.locator('#decision')).toHaveAttribute('data-kind', 'Intent');
  const groups = page.locator('#own-hand .hand-row.active .spell-group');
  await expect(groups).toHaveCount(3);
  const grid = groups.first().locator('.spell-grid');
  expect(await grid.evaluate(node => getComputedStyle(node).gridTemplateColumns.split(' ').length)).toBe(2);
  const first = grid.locator('.held').first();
  expect((await first.boundingBox()).height).toBeLessThan(145);
  await page.locator('#hand-view').click();
  await expect(page.locator('#planning')).toHaveClass(/full-spells/);
  expect(await grid.evaluate(node => getComputedStyle(node).gridTemplateColumns.split(' ').length)).toBe(1);
  await expect(first.locator('.card-body')).toBeVisible();
  await page.locator('#hand-view').click();
  expect(await grid.evaluate(node => getComputedStyle(node).gridTemplateColumns.split(' ').length)).toBe(2);
  await expectNoSidewaysScroll(page);
  await page.screenshot({ path: info.outputPath('compact-spellbook.png'), animations: 'disabled' });
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
  await page.locator('#hand-talents').click();
  await expect(page.locator('#mat')).toBeVisible();
});

// The identity tags are what a pick is scanned by (docs/domain/package-identity-tags.md): every package card
// wears its chips inside the card, three of them included, on a phone as on a desk.
test('package cards wear their identity tags without widening the page', async ({ page }, info) => {
  const evolving = { ...view, waitingFor: 'Evolution', options: { evolution: { remainingPicks: 2,
    creatures: [{ creature: 1, availableTiers: packages.map(item => item.id) }] } },
    board: { ...view.board, subPhase: 'Evolution', evolutionChoices: [] } };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: evolving }));
  await expect(page.locator('#decision')).toHaveAttribute('data-kind', 'Evolution');
  const north = page.locator('#decision .package-card', { hasText: /^North/ }).first();
  await expect(north.locator('.package-tag')).toHaveText(['Pressure', 'Defense']);
  const master = page.locator('#decision .package-card', { hasText: /^North Master 1/ });
  await expect(master.locator('.package-tag')).toHaveText(['Sacrifice', 'Burst', 'Sustain']);
  await expect(page.locator('#decision .package-card', { hasText: /^North Adept 1/ }).locator('.package-tags')).toHaveCount(0);
  const inside = await master.evaluate(card => [...card.querySelectorAll('.package-tag')].every(chip => chip.getBoundingClientRect().right <= card.getBoundingClientRect().right + 0.5));
  expect(inside).toBe(true);
  const clipped = await page.locator('#decision .package-card').evaluateAll(cards => cards
    .filter(card => card.scrollHeight > card.clientHeight + 1).map(card => `${card.textContent} ${card.scrollHeight}/${card.clientHeight}`));
  expect(clipped).toEqual([]);
  await expectNoSidewaysScroll(page);
  await page.screenshot({ path: info.outputPath('package-tags.png'), animations: 'disabled' });
  await page.locator('#hand-talents').click();
  await expect(page.locator('#mat .tree-node', { hasText: 'North Master 1' }).locator('.package-tag')).toHaveText(['Sacrifice', 'Burst', 'Sustain']);
  await expectNoSidewaysScroll(page);
});

test('battlefield names public opponent packages and upkeep popup separates ticks from lasting effects', async ({ page }, info) => {
  const enemy = { ...view.board.enemies[0], acquiredTiers: [packages[0].id],
    conditions: [{ effect: { kind: 'DefenseDebuff', amount: 2 }, remainingRounds: 2 }] };
  const applied = { ...view, board: { ...view.board, roundNumber: 4, enemies: [enemy] },
    feed: [{ sequence: 5, event: { kind: 'OngoingEffectsApplied', roundId: 4,
      regenerationTicks: [{ creature: 1, healed: 2 }], bleedTicks: [{ creature: 2, damage: 1 }] } }] };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: applied }));
  await expect(page.locator('#upkeep')).toBeVisible();
  await expect(page.locator('#phase-notice-changes')).toContainText('+2 ♥');
  await expect(page.locator('#phase-notice-context')).toContainText('1 / 3');
  await expect(page.locator('#phase-notice-skip')).toBeVisible();
  await page.locator('#phase-notice-next').click();
  await expect(page.locator('#phase-notice-changes')).toContainText('−1 ♥');
  await page.locator('#phase-notice-next').click();
  await expect(page.locator('#phase-notice-changes')).toContainText('◇ −2 · 2r');
  await expect(page.locator('#phase-notice-changes .tick')).toHaveAttribute('title', 'DefenseDebuff 2 · 2 rounds left');
  await expect(page.locator('#phase-notice-skip')).toBeHidden();
  await page.locator('#upkeep-label').click();
  await expect(page.locator('#upkeep-conditions')).toContainText('◇ −2');
  if (!isLaptop(page)) await page.locator('#board-toggle').click();
  await expect(page.locator('#enemies .enemy-packages')).toContainText('North');
  if (!isLaptop(page)) await expect(page.locator('#mini-board')).toBeHidden();
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
  // A pick is face down until Evolution ends (ADR 0089): the seat's own choice is listed, nothing is bought.
  served.board.evolutionChoices = [{ creature: 1, tier: packages[0].id }];
  await expect(page.locator('#evolution-budget')).toContainText('1 / 2 team picks remaining');
  served = structuredClone(served);
  served.board.allies[0].acquiredTiers = [packages[0].id];
  served.board.enemies[0].acquiredTiers = [packages[7].id];
  served = { ...served, waitingFor: 'Speed', waitingCreature: 1, waitingAsked: 5,
    options: view.options, board: { ...served.board, subPhase: 'Speed', evolutionChoices: [] } };
  await expect(page.locator('#phase-notice-title')).toHaveText('Round 3 · Mutations acquired');
  await expect(page.locator('#phase-notice-changes')).toContainText('Creature 1 · yours');
  await expect(page.locator('#phase-notice-changes')).toContainText('Creature 2 · opponent');
  await expect(page.locator('#enemies .enemy-packages')).toContainText('East');
  await page.screenshot({ path: info.outputPath('packages-unlocked.png'), animations: 'disabled' });
});

// ADR 0087: the Match tool opens under the dock on every layout, where a finger can reach it; two taps concede.
test('Concede opens in view under the dock, asks twice, and the end screen says who won', async ({ page }) => {
  await page.locator('#phase-notice-close').click();
  await page.locator('#match-tools-label').click();
  await expect(page.locator('#concede')).toBeInViewport();
  const panel = await page.locator('#match-panel').boundingBox();
  const dock = await page.locator('#phase-dock').boundingBox();
  expect(panel.y).toBeGreaterThanOrEqual(dock.y);
  expect(panel.x + panel.width).toBeLessThanOrEqual(widthOf(page));
  await page.locator('#concede').click();
  await expect(page.locator('#concede-yes')).toBeInViewport();
  let conceded = 0;
  // The seat reports the end only once the concession has been posted: a poll landing between here and the
  // tap would otherwise end the match under the confirmation. The later route wins, so the concession's is
  // registered after the seat's, which matches its path too.
  const ended = { ...view, over: true, outcome: { winner: 'player2', reason: 'Concession' } };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: conceded > 0 ? ended : view }));
  await page.route('**/api/seat/player1/concede', route => { conceded += 1; return route.fulfill({ json: ended.outcome }); });
  await page.locator('#concede-yes').click();
  await expect.poll(() => conceded).toBe(1);
  await expect(page.locator('#asking')).toHaveText('You conceded · Player 2 wins.');
  await expect(page.locator('#match-tools')).toBeHidden();
});

test('a finished match replays every round from its result, and the round can be jumped to', async ({ page }, info) => {
  const ally = view.board.allies[0]; const enemy = view.board.enemies[0];
  const cast = (sequence, round, health) => ({ sequence, round, subPhase: 'Activation', event: { kind: 'CombatActionResolved', roundId: round,
    resolution: { action: { actor: 1, spell: cards[3].id, targets: [2] } }, appliedOutcomes: [{ kind: 'DamageOutcome', target: 2, amount: 7 }],
    frame: { before: [ally, { ...enemy, health: health + 7 }], after: [ally, { ...enemy, health }], timeline: [{ creature: 1, speed: 'Standard' }, { creature: 2, speed: 'Standard' }], rollOffs: [] } } });
  const ended = round => ({ sequence: 100 + round, round: round + 1, event: { kind: 'RoundEnded', roundId: round } });
  const energy = { sequence: 14, round: 2, event: { kind: 'RoundEnergyGranted', roundId: 2, grants: [{ creature: 1, baseRequested: 2, passiveRequested: 1, gained: 3, before: 4, after: 7 }, { creature: 2, baseRequested: 2, passiveRequested: 0, gained: 2, before: 1, after: 3 }] } };
  const upkeep = { sequence: 15, round: 2, event: { kind: 'OngoingEffectsApplied', roundId: 2, bleedTicks: [{ creature: 2, damage: 2 }] } };
  const bought = { sequence: 16, round: 2, event: { kind: 'PurchasesRevealed', roundId: 2, choices: [{ creature: 1, tier: packages[0].id }, { creature: 2, tier: packages[1].id }] } };
  const feed = [cast(10, 1, 27), ended(1), energy, upkeep, bought, cast(20, 2, 20), ended(2), cast(30, 3, 13)];
  const over = { ...view, over: true, waitingFor: null, waitingCreature: null, options: {}, outcome: { winner: 'player1', reason: 'RoundCap' }, feed, feedNext: 104 };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: over }));
  await expect(page.locator('#asking')).toHaveText(/You win/);

  await page.getByRole('button', { name: 'Replay the match action by action' }).click();
  await expect(page.locator('#playback')).toBeVisible();
  await expect(page.locator('#phase-current')).toHaveText('Match replay');
  await expect(page.locator('#playback-count')).toContainText('Round 1 · action 1 of 6');
  await page.locator('[data-focus="playback-round"]').selectOption({ label: 'Round 2' });
  // The round opens on its recorded receipt, named as such, then the ongoing effects (ADR 0132).
  await expect(page.locator('#playback-title')).toHaveText('Round 2 · round energy');
  await expect(page.locator('#playback-action')).toContainText('+3 energy · 4 → 7 (base 2, passive 1)');
  await expect(page.locator('#playback-action')).toContainText('+2 energy · 1 → 3 (base 2, passive 0)');
  await page.locator('[data-focus="playback-next"]').click();
  await expect(page.locator('#playback-title')).toHaveText('Round 2 · upkeep');
  await expect(page.locator('#playback-action')).toContainText('Ongoing damage −2 HP');
  await page.locator('[data-focus="playback-next"]').click();
  await expect(page.locator('#playback-title')).toHaveText('Round 2 · mutations acquired');
  await expect(page.locator('#playback-action')).toContainText(packages[1].name);
  await expectNoSidewaysScroll(page);
  await page.locator('#playback').scrollIntoViewIfNeeded();
  await page.screenshot({ path: info.outputPath('match-replay-packages.png'), animations: 'disabled' });
  await page.locator('[data-focus="playback-round"]').selectOption({ label: 'Round 3' });
  await expect(page.locator('#playback-count')).toContainText('Round 3 · action 6 of 6');
  await expectNoSidewaysScroll(page);
  await page.screenshot({ path: info.outputPath('match-replay.png'), animations: 'disabled' });
  await page.locator('[data-focus="playback-skip"]').click();
  await expect(page.locator('#playback')).toBeHidden();
  await expect(page.locator('#asking')).toHaveText(/You win/);
});

// UX 15: walking a replay forward onto a cast's result plays that cast's effects once, from its recorded frames:
// one floating number on each place the struck creature is drawn, never one more from the live tracker.
test('a replayed cast floats its damage once when the replay reaches its result', async ({ page }) => {
  await page.addInitScript(() => {
    globalThis.floats = [];
    new MutationObserver(records => {
      for (const record of records) {
        for (const node of record.addedNodes) {
          if (node.classList?.contains('combat-float')) globalThis.floats.push({ text: node.textContent, id: node.parentElement.dataset.creatureId });
        }
      }
    }).observe(document, { childList: true, subtree: true });
  });
  await page.reload();
  const ally = view.board.allies[0]; const enemy = view.board.enemies[0];
  const frame = { before: [ally, { ...enemy, health: 27 }], after: [ally, { ...enemy, health: 20 }], timeline: [{ creature: 1, speed: 'Standard' }, { creature: 2, speed: 'Standard' }], rollOffs: [] };
  const feed = [{ sequence: 10, round: 1, subPhase: 'Activation', event: { kind: 'CombatActionResolved', roundId: 1,
    resolution: { action: { actor: 1, spell: cards[3].id, targets: [2] } }, appliedOutcomes: [{ kind: 'DamageOutcome', target: 2, amount: 7 }], frame } }];
  const over = { ...view, over: true, waitingFor: null, waitingCreature: null, options: {}, outcome: { winner: 'player1', reason: 'RoundCap' }, feed, feedNext: 11 };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: over }));
  await page.getByRole('button', { name: 'Replay the match action by action' }).click();
  await expect(page.locator('#playback')).toBeVisible();
  expect(await page.evaluate(() => globalThis.floats)).toEqual([]);

  await page.locator('[data-focus="playback-next"]').click();
  await expect.poll(() => page.evaluate(() => globalThis.floats.length), { timeout: 5000 }).toBeGreaterThan(0);
  await page.waitForTimeout(500);

  const floats = await page.evaluate(() => globalThis.floats);
  const drawn = await page.locator('[data-creature-id="2"]').count();
  expect(floats.every(float => float.text === '-7' && float.id === '2')).toBe(true);
  expect(floats.length).toBe(drawn);
});

test('a session opened from the admin panel goes straight to its replay, and its result names the winner', async ({ page }) => {
  const ally = view.board.allies[0]; const enemy = view.board.enemies[0];
  const frame = { before: [ally, { ...enemy, health: 27 }], after: [ally, enemy], timeline: [{ creature: 1, speed: 'Standard' }], rollOffs: [] };
  const feed = [{ sequence: 10, round: 1, subPhase: 'Activation', event: { kind: 'CombatActionResolved', roundId: 1,
    resolution: { action: { actor: 1, spell: cards[3].id, targets: [2] } }, appliedOutcomes: [{ kind: 'DamageOutcome', target: 2, amount: 7 }], frame } }];
  let asked = 0;
  await page.route('**/api/sessions/finished-one/replay', route => {
    asked += 1;
    return route.fulfill({ json: { id: 'finished-one', outcome: { winner: 'player2', reason: 'Elimination' }, catalogue,
      boards: { player1: view.board, player2: { ...view.board, allies: view.board.enemies, enemies: view.board.allies } }, feed } });
  });

  await page.goto('/table/?replay=finished-one');

  await expect(page.locator('#phase-current')).toHaveText('Match replay');
  await expect(page.locator('#playback-action')).toContainText('Heavy Strike');
  await page.locator('[data-focus="playback-skip"]').click();
  await expect(page.locator('#asking')).toHaveText('Player 2 wins · the last team standing.');
  await expect(page.locator('#notes')).toBeHidden();
  expect(asked).toBe(1);
  await expectNoSidewaysScroll(page);
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
  await expect(page.locator('#mini-enemies .mini-initiative')).toHaveText('↟5');
  await expect(page.locator('#mini-allies .mini-initiative')).toHaveText('↟8');
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

for (const candidates of [[2, 3, 4], [1, 5, 6], [1, 2, 5]]) {
  test(`target rows make room for a thumb on phones: ${candidates.join(', ')}`, async ({ page }, info) => {
    const creature = id => ({ id, health: 30, maxHealth: 30, energy: 2, currentInitiative: 6, totalDefense: 2 });
    const target = { ...view, waitingFor: 'Target', waitingAsked: 4,
      guidance: [{ spell: cards[3].id, cost: 2, energyAfterCost: 0, chosenSpeed: 'Quick',
        targets: candidates.map(target => ({ target, plain: [{ kind: 'DamageOutcome', amount: 3 }], critical: [{ kind: 'DamageOutcome', amount: 3 }] })) }],
      options: { target: { actor: 1, spell: cards[3].id, legalTargets: { candidates, minTargets: 1, maxTargets: 1 } } },
      board: { ...view.board, subPhase: 'Activation', allies: [1, 5, 6].map(creature), enemies: [2, 3, 4].map(creature) } };
    let submissions = 0;
    await page.route('**/api/seat/player1**', route => {
      if (route.request().method() === 'POST') submissions++;
      return route.fulfill({ json: target });
    });
    await page.route('**/api/seat/player1/decision', route => {
      submissions++;
      return route.fulfill({ json: {} });
    });
    await expect(page.locator('#decision')).toHaveAttribute('data-kind', 'Target');
    await page.locator('#phase-notice-close').click();
    if (isLaptop(page)) {
      await expect(page.locator('#mini-board')).toBeHidden();
      await expect(page.locator('#board .creature.legal')).toHaveCount(candidates.length);
      // UX 04: a legal target carries a gold outline besides its dashed border.
      expect(await page.locator('#board .creature.legal').first().evaluate(node => getComputedStyle(node).outlineStyle)).toBe('solid');
      return;
    }
    const chips = page.locator('#mini-board .mini-creature.legal');
    await expect(chips).toHaveCount(candidates.length);
    for (const chip of await chips.all()) {
      const box = await chip.boundingBox();
      expect(box.height).toBeGreaterThanOrEqual(isPhone(page) ? 72 : 39);
      if (isPhone(page)) expect(box.width).toBeGreaterThanOrEqual(72);
      expect(await chip.evaluate(node => node.scrollWidth <= node.clientWidth)).toBe(true);
    }
    if (isPhone(page)) {
      for (const row of await page.locator('#mini-board .mini-row').all()) {
        const boxes = await row.locator('.mini-creature').evaluateAll(nodes => nodes.map(node => {
          const { left, right } = node.getBoundingClientRect(); return { left, right };
        }));
        expect(boxes[1].left - boxes[0].right).toBeGreaterThanOrEqual(8);
      }
    }
    // A tap near the bottom corner must select the creature, without casting or opening the full board.
    const chip = chips.first(); const box = await chip.boundingBox();
    const breathing = node => getComputedStyle(node).animationName;
    // UX 04: an eligible chip breathes until it is picked; a picked one stays still.
    expect(await chip.evaluate(breathing)).toBe('target-available-glow');
    await chip.tap({ position: { x: box.width - 10, y: box.height - 10 } });
    await expect(chip).toHaveClass(/picked/);
    expect(await chip.evaluate(breathing)).toBe('none');
    // And nothing breathes for a person who asked for reduced motion.
    await page.emulateMedia({ reducedMotion: 'reduce' });
    expect(await chips.nth(1).evaluate(breathing)).toBe('none');
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await expect(page.locator('#choices')).toContainText('Cast on 1 of 1');
    await expect(page.locator('#board')).toBeHidden();
    expect(submissions).toBe(0);
    await expectNoSidewaysScroll(page);
    await page.evaluate(() => scrollTo(0, 0));
    await page.screenshot({ path: info.outputPath('thumb-targets.png'), animations: 'disabled' });
    await page.route('**/api/seat/player1**', route => route.fulfill({ json: view }));
    await expect(page.locator('#decision')).toHaveAttribute('data-kind', 'Speed');
    await expect(page.locator('#mini-board')).not.toHaveClass(/targeting/);
    expect((await page.locator('#mini-board .mini-creature').first().boundingBox()).height).toBeLessThan(72);
  });
}

// What resolved since the seat's last move, with the longest lines an action produces, in every question.
const resolved = (sequence, actor, targets, outcomes) => ({ sequence, round: 3, subPhase: 'Activation', event: { kind: 'CombatActionResolved', roundId: 3,
  resolution: { action: { actor, spell: cards[3].id, targets } },
  appliedOutcomes: outcomes.map(target => ({ kind: 'ConditionOutcome', target, effect: { kind: 'DefenseModifier', amount: -3, duration: { rounds: 2 } } })) } });

// UX 07: a health bar eases from the health a confirmed action found to the health it left, once. A redraw,
// a repeated poll of the same event and the first board the page sees animate nothing.
test('a confirmed hit eases its health bar once, and a redraw does not replay it', async ({ page }) => {
  await page.addInitScript(() => {
    globalThis.barMoves = [];
    const animate = Element.prototype.animate;
    Element.prototype.animate = function (frames, options) {
      if (this.matches('.bar, .mini-bar')) globalThis.barMoves.push(frames.map(frame => frame.width));
      return animate.call(this, frames, options);
    };
  });
  await page.reload();
  await expect(page.locator('.speed-spell')).toHaveCount(6);
  expect(await page.evaluate(() => globalThis.barMoves)).toEqual([]);

  const hit = { ...view, feed: [resolved(5, 2, [1], [])], feedNext: 6,
    board: { ...view.board, allies: [{ ...view.board.allies[0], health: 3 }] } };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: hit }));
  await expect.poll(() => page.evaluate(() => globalThis.barMoves.length), { timeout: 10000 }).toBeGreaterThan(0);
  const moves = await page.evaluate(() => globalThis.barMoves);
  expect(moves.every(([from, to]) => from === '20%' && to === '10%')).toBe(true);

  await page.locator('#hand-view').evaluate(button => button.click());
  await page.waitForTimeout(1500);
  expect(await page.evaluate(() => globalThis.barMoves.length)).toBe(moves.length);
});

// UX 08-11: a confirmed hit flashes the creature it struck and floats the health it cost, on the row and the
// chip alike -- inside the chip, which clips -- with the stun it also applied, and a stun the board shows wears
// its own colour on the condition chip.
test('a confirmed hit floats its damage on the creature it struck, and a stun chip wears its colour', async ({ page }) => {
  await page.addInitScript(() => {
    globalThis.floats = [];
    new MutationObserver(records => {
      for (const record of records) {
        for (const node of record.addedNodes) {
          if (node.classList?.contains('combat-float')) {
            const chip = node.parentElement;
            globalThis.floats.push({ text: node.textContent, struck: chip.classList.contains('fx-damage'),
              effects: getComputedStyle(chip).animationName,
              // A chip clips what overflows it: its number has to start inside it.
              inside: !chip.classList.contains('mini-creature') || (node.offsetTop >= 0 && node.offsetTop + node.offsetHeight <= chip.clientHeight) });
          }
        }
      }
    }).observe(document, { childList: true, subtree: true });
  });
  await page.reload();
  await expect(page.locator('.speed-spell')).toHaveCount(6);

  const hit = { ...view, feed: [resolved(5, 2, [1], [])], feedNext: 6,
    board: { ...view.board, allies: [{ ...view.board.allies[0], health: 3, isStunned: true,
      conditions: [{ effect: { kind: 'Stun', duration: { rounds: 1 } }, remainingRounds: 1 }] }] } };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: hit }));
  await expect.poll(() => page.evaluate(() => globalThis.floats.length), { timeout: 10000 }).toBeGreaterThan(0);

  const floats = await page.evaluate(() => globalThis.floats);
  expect(floats.every(float => float.text === '-3' && float.struck && float.inside)).toBe(true);
  // The hit stunned too: both play, neither replaces the other.
  expect(floats.every(float => float.effects.includes('combat-hit') && float.effects.includes('combat-stun'))).toBe(true);
  await expect(page.locator('.chip.condition-stun').first()).toBeAttached();
});

// UX 13: an opponent's resolved cast arrives as a step, and that is when it is revealed: its caster and its
// target light up once, and a target the cast hurt keeps its hit flash beside the cue.
test('an opponent\'s confirmed cast lights its caster and its target, beside the hit it dealt', async ({ page }) => {
  await page.addInitScript(() => {
    globalThis.reveals = [];
    new MutationObserver(records => {
      for (const record of records) {
        for (const node of record.addedNodes) {
          if (node.classList?.contains('spell-reveal-caster') || node.classList?.contains('spell-reveal-target')) {
            globalThis.reveals.push({ id: node.dataset.creatureId, caster: node.classList.contains('spell-reveal-caster'),
              effects: getComputedStyle(node).animationName });
          }
        }
      }
    }).observe(document, { childList: true, subtree: true });
  });
  await page.reload();
  await expect(page.locator('.speed-spell')).toHaveCount(6);
  expect(await page.evaluate(() => globalThis.reveals)).toEqual([]);

  const hit = { ...view, feed: [resolved(5, 2, [1], [])], feedNext: 6,
    board: { ...view.board, allies: [{ ...view.board.allies[0], health: 3 }] } };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: hit }));
  await expect.poll(() => page.evaluate(() => globalThis.reveals.length), { timeout: 10000 }).toBeGreaterThan(0);

  const reveals = await page.evaluate(() => globalThis.reveals);
  expect(reveals.filter(one => one.caster).every(one => one.id === '2')).toBe(true);
  const targets = reveals.filter(one => !one.caster);
  expect(targets.length).toBeGreaterThan(0);
  expect(targets.every(one => one.id === '1' && one.effects.includes('spell-target-reveal') && one.effects.includes('combat-hit'))).toBe(true);
});

// UX 14: the round label lights up when the round turns over, once, and not on the board the page first draws.
test('a new round lights the round label once, and the first board does not', async ({ page }) => {
  await page.addInitScript(() => {
    globalThis.cues = [];
    const animate = Element.prototype.animate;
    Element.prototype.animate = function (frames, options) {
      if (this.id === 'phase-round' || this.id === 'phase-current') globalThis.cues.push(this.id);
      return animate.call(this, frames, options);
    };
  });
  await page.reload();
  await expect(page.locator('.speed-spell')).toHaveCount(6);
  expect(await page.evaluate(() => globalThis.cues)).toEqual([]);

  await page.route('**/api/seat/player1**', route => route.fulfill({ json: { ...view, board: { ...view.board, roundNumber: 4 } } }));
  await expect.poll(() => page.evaluate(() => globalThis.cues), { timeout: 10000 }).toEqual(['phase-round']);
  await page.waitForTimeout(1500);
  expect(await page.evaluate(() => globalThis.cues)).toEqual(['phase-round']);
});

// UX 20: on a phone the battlefield is a step of the history: Back closes it without leaving the table, Forward
// opens it again, and closing it from its own button leaves no entry behind.
test('Back closes the battlefield and Forward reopens it, without leaving the table', async ({ page }) => {
  test.skip(isLaptop(page));
  const open = () => page.evaluate(() => document.body.classList.contains('board-open'));
  const onBoardEntry = () => page.evaluate(() => history.state?.downfallBoard === true);
  await page.locator('#phase-notice-close').click();

  await page.locator('#board-toggle').click();
  expect(await open()).toBe(true);
  expect(await onBoardEntry()).toBe(true);
  await page.goBack();
  await expect.poll(open).toBe(false);
  await expect(page.locator('#table')).toBeVisible();
  await page.goForward();
  await expect.poll(open).toBe(true);
  await page.locator('#board-toggle').click();
  await expect.poll(open).toBe(false);
  await expect.poll(onBoardEntry).toBe(false);
});

// UX 20: the round bar says when the table cannot reach its host, and clears once a poll answers again.
test('the round bar says the table is offline, and clears once it reaches the host', async ({ page, context }) => {
  const status = page.locator('#connection-state');
  await context.setOffline(true);
  await expect(status).toHaveText(/Offline/);
  await context.setOffline(false);
  await expect(status).toBeHidden({ timeout: 10000 });
});

// UX 16-17: a spell a creature has just learned glows where it is first seen -- the tempo question's list, the
// round's first after Evolution -- once; the first board the page draws glows nothing.
test('a spell just learned glows in the tempo list, and the first board does not', async ({ page }) => {
  await page.addInitScript(() => {
    globalThis.unlocked = [];
    new MutationObserver(records => {
      for (const record of records) {
        // The hand is built whole before it is inserted: the card is a descendant of the node added, never one.
        for (const added of record.addedNodes) {
          const found = [added, ...(added.querySelectorAll?.('.spell-just-unlocked') ?? [])].filter(node => node.classList?.contains('spell-just-unlocked'));
          for (const node of found) globalThis.unlocked.push({ text: node.textContent, effects: getComputedStyle(node).animationName });
        }
      }
    }).observe(document, { childList: true, subtree: true });
  });
  const ally = view.board.allies[0];
  const earlier = { ...view, board: { ...view.board, allies: [{ ...ally, knownSpells: ally.knownSpells.slice(1) }] } };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: earlier }));
  await page.reload();
  await expect(page.locator('.speed-spell')).toHaveCount(5);
  expect(await page.evaluate(() => globalThis.unlocked)).toEqual([]);

  await page.route('**/api/seat/player1**', route => route.fulfill({ json: { ...view, waitingAsked: 4 } }));
  await expect.poll(() => page.evaluate(() => globalThis.unlocked.length), { timeout: 10000 }).toBeGreaterThan(0);
  const unlocked = await page.evaluate(() => globalThis.unlocked);
  expect(unlocked.every(one => one.effects === 'learned-spell-glow')).toBe(true);
  expect(unlocked.every(one => one.text.includes(cards[0].name))).toBe(true);
});

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
  await expect(page.locator('#order-theirs')).toHaveText('Opponent: Creature 2 Instinct · Creature 3 Precision · Creature 4 Precision');
  await expect(page.locator('#order-list li')).toHaveCount(6);
  await expect(page.locator('#decision-turn')).toHaveText('Acts 2 of 6 · Instinct');
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

test("the next creature's question opens its spellbook at the top, not where the last one was scrolled", async ({ page }) => {
  test.skip(!isPhone(page), 'the spellbook scrolls on its own only on a phone');
  const creature = id => ({ id, health: 30, maxHealth: 30, energy: 4, knownSpells: cards.map(card => card.id) });
  const board = { ...view.board, allies: [view.board.allies[0], creature(5)] };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: { ...view, board } }));
  const hand = page.locator('#hand-section');
  await expect(page.locator('.speed-reference')).toHaveCount(2);
  await page.locator('#hand-section .hand-row').nth(1).locator('> summary').click();
  await hand.evaluate(node => { node.scrollTop = node.scrollHeight; });
  expect(await hand.evaluate(node => node.scrollTop)).toBeGreaterThan(0);
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: { ...view, board, waitingCreature: 5, waitingAsked: 4 } }));
  await expect(page.locator('#asking')).toContainText('Creature 5');
  expect(await hand.evaluate(node => node.scrollTop)).toBe(0);
});

// Tempos are chosen per creature and can be changed from its tab until Confirm, which sends them all; the host
// asks one creature at a time, and each is sent when its creature is asked.
test('tempos are chosen per creature, can go back, and Confirm sends them as the host asks', async ({ page }) => {
  const posted = [];
  const asked = (creature, waitingAsked, speedChoices = []) => ({ ...view, waitingCreature: creature, waitingAsked, options: { speed: { missing: [1, 3] } },
    board: { ...view.board, speedChoices, allies: [view.board.allies[0], { ...view.board.allies[0], id: 3, name: 'Creature 3' }] } });
  let served = asked(1, 4);
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: served }));
  await page.route('**/api/seat/player1/decision', route => {
    posted.push(route.request().postDataJSON());
    // The host moves on to the next creature once it holds the first answer, and never before: a poll that
    // found creature 3 asked while creature 1 was still being confirmed would read creature 1 as answered
    // already and send one tempo instead of two.
    served = asked(3, 5, [{ creature: 1, speed: 'Standard' }]);
    return route.fulfill({ status: 204 });
  });
  await page.reload();
  const confirm = page.locator('[data-focus="tempo-confirm"]');
  await expect(confirm).toBeDisabled();

  await page.locator('[data-focus="speed-Quick"]').click();
  await expect(page.locator('#asking')).toHaveText('Creature 3 · choose tempo');
  await page.locator('[data-focus="speed-Quick"]').click();
  await page.locator('[data-focus="tempo-creature-1"]').click();
  await expect(page.locator('[data-focus="speed-Quick"]')).toHaveAttribute('aria-pressed', 'true');
  await page.locator('[data-focus="speed-Standard"]').click();
  await expect(page.locator('[data-focus="tempo-creature-1"]')).toHaveText('Creature 1 · Precision');
  expect(posted).toEqual([]);
  await expectNoSidewaysScroll(page);

  await confirm.click();
  await expect.poll(() => posted.map(one => [one.creature, one.speed]), { timeout: 10000 }).toEqual([[1, 'Standard'], [3, 'Quick']]);
});

// Sound and haptics are a setting: a small toggle in the masthead beside the Round guide, not a panel in the match.
test('sound and haptics sit in the masthead beside the Round guide, one popover at a time', async ({ page }) => {
  const toggle = page.locator('#feedback-preferences > summary');
  await expect(page.locator('.masthead #feedback-preferences')).toBeVisible();
  expect((await toggle.boundingBox()).height).toBeLessThanOrEqual(48);
  await toggle.click();
  await expect(page.locator('#feedback-sound')).toBeVisible();
  await page.locator('#feedback-sound').check();
  await expect(page.locator('#feedback-preferences')).toHaveClass(/on/);
  await page.locator('#shape > summary').click();
  await expect(page.locator('#feedback-sound')).toBeHidden();
  await expectNoSidewaysScroll(page);
});

// A round that ends turns over in steps: once the action that ended it is read, its Cleanup says what ran out,
// and the next Upkeep moves the bars by what its ticks dealt -- the float a hit gets, on the creature it bled.
test('the round turns over in steps after its last action, and the upkeep floats its bleed', async ({ page }) => {
  await page.addInitScript(() => {
    globalThis.floats = [];
    new MutationObserver(records => {
      for (const record of records) {
        for (const node of record.addedNodes) {
          if (node.classList?.contains('combat-float')) {
            globalThis.floats.push({ text: node.textContent, id: node.parentElement.dataset.creatureId,
              struck: node.parentElement.classList.contains('fx-damage') });
          }
        }
      }
    }).observe(document, { childList: true, subtree: true });
  });
  // The seat has read its own action of round 3, so what follows it is read as steps.
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: { ...view, feed: [resolved(5, 1, [2], [])], feedNext: 6 } }));
  await page.reload();
  await expect(page.locator('.speed-spell')).toHaveCount(6);

  const turnedOver = { ...view, feedNext: 11, feed: [resolved(5, 1, [2], []), resolved(7, 2, [1], []),
    { sequence: 8, round: 4, subPhase: 'Upkeep', event: { kind: 'ConditionsExpired', roundId: 3, expired: { 1: [{ effect: { kind: 'Stun' }, remainingRounds: 0 }] } } },
    { sequence: 9, round: 4, subPhase: 'Upkeep', event: { kind: 'RoundEnded', roundId: 3 } },
    { sequence: 10, round: 4, subPhase: 'Upkeep', event: { kind: 'OngoingEffectsApplied', roundId: 4, bleedTicks: [{ creature: 2, damage: 2 }] } }],
    board: { ...view.board, roundNumber: 4, enemies: [{ ...view.board.enemies[0], health: 18 }] } };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: turnedOver }));

  const step = page.locator('#combat-step-count');
  await expect(step).toContainText('Round 3 · opponent action', { timeout: 10000 });
  await page.locator('[data-focus="step-next"]').click();
  await expect(step).toContainText('Round 3 · cleanup');
  await expect(page.locator('#combat-step-action')).toContainText('Stun ends');
  expect(await page.evaluate(() => globalThis.floats)).toEqual([]);
  await page.locator('[data-focus="step-next"]').click();
  await expect(step).toContainText('Round 4 · upkeep');
  await expect.poll(() => page.evaluate(() => globalThis.floats.length)).toBeGreaterThan(0);
  const floats = await page.evaluate(() => globalThis.floats);
  expect(floats.every(float => float.text === '-2' && float.id === '2' && float.struck)).toBe(true);
  await page.locator('[data-focus="step-next"]').click();
  await expect(page.locator('#combat-step')).toBeHidden();
});

// A phone held sideways: the battlefield and the desk side by side, both in view without scrolling, each
// scrolling on its own, and the board always there rather than behind a button.
for (const [width, height] of [[844, 390], [740, 360]]) {
  test(`held sideways at ${width}×${height}, the battlefield and the desk sit side by side`, async ({ page }) => {
    await page.setViewportSize({ width, height });
    await expect(page.locator('.speed-spell')).toHaveCount(6);
    const board = await page.locator('#board').boundingBox();
    const desk = await page.locator('#decision').boundingBox();
    const dock = await page.locator('#phase-dock').boundingBox();
    expect(board.x).toBeLessThan(2);
    expect(desk.x).toBeGreaterThanOrEqual(board.x + board.width);
    expect(dock.x + dock.width).toBeLessThanOrEqual(desk.x);
    expect(board.y).toBeGreaterThanOrEqual(dock.y + dock.height - 1);
    expect(board.y + board.height).toBeLessThanOrEqual(height + 1);
    await expect(page.locator('#mini-board')).toBeVisible();
    await expect(page.locator('#board-toggle')).toBeHidden();
    await expect(page.locator('[data-focus="speed-Quick"]')).toBeInViewport();
    await expectNoSidewaysScroll(page);

    // The board scrolls on its own: the desk stays where it is.
    const before = await page.locator('#decision').boundingBox();
    await page.locator('#board').evaluate(node => { node.scrollTop = node.scrollHeight; });
    expect((await page.locator('#decision').boundingBox()).y).toBe(before.y);
  });
}

// The landscape regression was vertical: the capped decision let its cards paint over the spellbook.
// Exercise the real evolution DOM, including enough choices to outgrow a short screen, and rotate back.
for (const [width, height] of [[740, 360], [844, 390], [960, 432]]) {
  test(`landscape mutations stay inside their panel at ${width}×${height}`, async ({ page }, info) => {
    await page.setViewportSize({ width, height });
    const creature = id => ({ id, name: `Creature ${id}`, health: 30, maxHealth: 30, energy: 2, currentInitiative: 5, knownSpells: cards.map(card => card.id) });
    const evolving = { ...view, waitingFor: 'Evolution', options: { evolution: { remainingPicks: 2,
      creatures: [1, 2, 3].map(creature => ({ creature, availableTiers: packages.map(item => item.id) })) } },
      board: { ...view.board, subPhase: 'Evolution', allies: [1, 2, 3].map(creature), enemies: [4, 5, 6].map(creature), evolutionChoices: [] } };
    await page.route('**/api/seat/player1**', route => route.fulfill({ json: evolving }));
    await expect(page.locator('#decision')).toHaveAttribute('data-kind', 'Evolution');
    await page.locator('#phase-notice-close').click();
    await page.evaluate(() => window.scrollTo(0, 0));
    const decision = await page.locator('#decision').boundingBox();
    const hand = await page.locator('#hand-section').boundingBox();
    const lastCard = page.locator('#decision .package-card').last();
    const last = await lastCard.boundingBox();
    expect(last.y + last.height).toBeLessThanOrEqual(decision.y + decision.height + 1);
    expect(hand.y).toBeGreaterThanOrEqual(decision.y + decision.height);
    await expectNoSidewaysScroll(page);
    await page.screenshot({ path: info.outputPath('landscape-evolution.png'), animations: 'disabled' });
    const board = await page.locator('#board').boundingBox();
    await lastCard.scrollIntoViewIfNeeded();
    await expect(lastCard).toBeInViewport();
    expect((await page.locator('#board').boundingBox()).y).toBe(board.y);
    await expect(page.locator('#phase-dock')).toBeInViewport();
    // Returning to portrait restores the bounded list and the battlefield toggle.
    await page.setViewportSize({ width: 390, height: 844 });
    await page.evaluate(() => window.scrollTo(0, 0));
    await expect(page.locator('#board-toggle')).toBeVisible();
    expect(await page.locator('#decision .choice-cards').evaluate(node => getComputedStyle(node).overflowY)).toBe('auto');
    await expectNoSidewaysScroll(page);
  });
}

for (const width of [740, 844]) {
  test(`landscape spell selection and targeting remain usable at ${width}px`, async ({ page }, info) => {
    await page.setViewportSize({ width, height: 390 });
    const creature = id => ({ id, health: 30, maxHealth: 30, energy: 4, currentInitiative: 6, knownSpells: cards.map(card => card.id) });
    const board = { ...view.board, subPhase: 'Activation', allies: [1, 5, 6].map(creature), enemies: [2, 3, 4].map(creature) };
    let served = { ...view, board, waitingFor: 'Intent', options: { intent: { creatures: [{ creature: 1, castableSpells: cards.map(card => card.id) }] } } };
    await page.route('**/api/seat/player1**', route => route.fulfill({ json: served }));
    await expect(page.locator('#decision')).toHaveAttribute('data-kind', 'Intent');
    await page.locator('#phase-notice-close').click();
    await page.locator('#own-hand .held.offered').first().click();
    await expect(page.locator('#own-hand .held.chosen')).toHaveCount(1);
    await page.evaluate(() => window.scrollTo(0, 0));
    await expectNoSidewaysScroll(page);
    await page.screenshot({ path: info.outputPath('landscape-spells.png'), animations: 'disabled' });

    served = { ...served, waitingAsked: 4, waitingFor: 'Target', options: { target: { actor: 1, spell: cards[3].id,
      legalTargets: { candidates: [1, 2, 5], minTargets: 1, maxTargets: 1 } } } };
    await expect(page.locator('#decision')).toHaveAttribute('data-kind', 'Target');
    const target = page.locator('#board .creature.legal').first();
    await target.click();
    await expect(target).toHaveClass(/picked/);
    await expect(page.locator('#choices')).toContainText('Cast on 1 of 1');
    const panel = await page.locator('#decision').boundingBox();
    const hand = await page.locator('#hand-section').boundingBox();
    expect(hand.y).toBeGreaterThanOrEqual(panel.y + panel.height);
    await expectNoSidewaysScroll(page);
    await page.screenshot({ path: info.outputPath('landscape-target.png'), animations: 'disabled' });
  });
}

test('classic replay separates recorded ordinary energy from ongoing ticks without a nominal allowance', async ({ page }) => {
  const feed = [
    { sequence: 1, round: 1, event: { kind: 'RoundEnergyGranted', roundId: 1, grants: [
      { creature: 1, baseRequested: 4, passiveRequested: 1, gained: 3, before: 7, after: 10 },
      { creature: 2, baseRequested: 0, passiveRequested: 0, gained: 0, before: 2, after: 2 },
    ] } },
    { sequence: 2, round: 1, event: { kind: 'OngoingEffectsApplied', roundId: 1, bleedTicks: [{ creature: 2, damage: 2 }] } },
    { sequence: 3, round: 2, event: { kind: 'OngoingEffectsApplied', roundId: 2 } },
  ];
  const over = { ...view, over: true, waitingFor: null, waitingCreature: null, options: {}, outcome: { winner: 'player1', reason: 'RoundCap' }, feed, feedNext: 4 };
  await page.route('**/api/seat/player1**', route => route.fulfill({ json: over }));
  await expect(page.locator('#asking')).toHaveText(/You win/);
  await page.getByRole('button', { name: 'Replay the match action by action' }).click();
  const rows = page.locator('#playback-action .recap-action');
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0)).toContainText('+3 energy · 7 → 10 (base 4, passive 1)');
  await expect(rows.nth(1)).toContainText('+0 energy · 2 → 2 (base 0, passive 0)');
  await page.locator('[data-focus="playback-next"]').click();
  await expect(rows).toHaveCount(1); await expect(rows).toContainText('Ongoing damage −2 HP');
  await expect(page.locator('#playback-action')).not.toContainText('energy');
  await page.locator('[data-focus="playback-next"]').click();
  await expect(rows).toHaveCount(1); await expect(rows).toContainText('No ongoing effect ticked.');
  await expect(page.locator('#playback-action')).not.toContainText('energy');
  await expectNoSidewaysScroll(page);
});
