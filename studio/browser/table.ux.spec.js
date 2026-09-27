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
const catalogue = {
  cards, packages: [], contentHash: 'fixture', rules: { teamSize: 3, energyPerRound: 2, evolutionPicksPerOpportunity: 2, evolutionInterval: 2, firstEvolutionRound: 1, roundCap: 20, criticalMultiplier: 2 },
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
