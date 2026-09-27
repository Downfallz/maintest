import { test, expect } from '@playwright/test';

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
  round: { subPhases: ['Upkeep', 'Evolution', 'EnergyGain', 'Speed', 'TurnOrderResolution', 'TieOrder', 'IntentSelection', 'RevealAndTarget', 'ActionResolution', 'Cleanup', 'Finalization'],
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
  if (info.project.name !== 'desktop') {
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
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
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
  if (info.project.name === 'desktop') {
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
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
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
  if (info.project.name !== 'desktop') {
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
  if (info.project.name !== 'desktop') expect(Math.max(...heights)).toBeLessThan(140);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  const pummel = page.locator('#own-hand .held.offered').filter({ hasText: 'Pummel' });
  await pummel.click();
  await expect(pummel).toContainText('Tap again to declare');
  await expect(pummel.locator('.card-availability')).toBeVisible();
  await page.screenshot({ path: info.outputPath('choose-spell.png'), animations: 'disabled' });
});
