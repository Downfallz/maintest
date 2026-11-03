import { test, expect } from '@playwright/test';

async function fits(page) {
  const viewport = page.viewportSize();
  const dimensions = await page.evaluate(() => ({ width: document.documentElement.scrollWidth, height: document.documentElement.scrollHeight }));
  expect(dimensions.width).toBeLessThanOrEqual(viewport.width);
  expect(dimensions.height).toBeLessThanOrEqual(viewport.height);
  for (const node of await page.locator('.creature, #choose, #confirm, #cancel, #skip, #reset, #settings').all()) {
    if (!await node.isVisible()) continue;
    const box = await node.boundingBox();
    expect(box.width).toBeGreaterThanOrEqual(44);
    expect(box.height).toBeGreaterThanOrEqual(44);
    expect(box.x).toBeGreaterThanOrEqual(-1);
    expect(box.y).toBeGreaterThanOrEqual(-1);
    expect(box.x + box.width).toBeLessThanOrEqual(viewport.width + 1);
    expect(box.y + box.height).toBeLessThanOrEqual(viewport.height + 1);
  }
}
async function choose(page, target = 'b1') {
  await page.locator('#choose').click();
  await page.locator(`[data-creature="${target}"]`).click();
}

test.beforeEach(async ({ page }) => {
  await page.goto('/table/arena-lab.html');
  await expect(page.locator('#lab')).toHaveAttribute('data-ready', 'true');
});

test('Phaser loads local artwork and fits the complete game into the viewport', async ({ page }, testInfo) => {
  await expect(page.locator('#lab')).toHaveAttribute('data-renderer', 'phaser');
  await expect(page.locator('#lab')).toHaveAttribute('data-asset-fallback', 'false');
  await expect(page.locator('#phaser-mount canvas')).toHaveCount(1);
  await expect(page.locator('.creature')).toHaveCount(6);
  // The HUD is read off the fixture, not spelled in the page: the round, its phase in the player's words, the
  // actor and the one action.
  await expect(page.locator('#round-number')).toHaveText('05');
  await expect(page.locator('#round-phase')).toHaveText('COMBAT');
  await expect(page.locator('#actor-name')).toHaveText('Brute');
  await expect(page.locator('#actor-meta')).toHaveText('Instinct · 4 energy');
  await expect(page.locator('#choose')).toContainText('Strike');
  await expect(page.locator('#confirm')).toHaveText('Choose Strike');
  await expect(page.locator('[data-creature="a1"] .creature-tag')).toHaveText('ACTING');
  await expect(page.locator('[data-creature="b1"]')).toHaveClass(/enemy/);
  await fits(page);
  await page.screenshot({ path: testInfo.outputPath('phaser-arena.png') });
});

test('targeting, cancellation and inspection work without leaving the game screen', async ({ page }, testInfo) => {
  await choose(page, 'b2');
  await expect(page.locator('#confirm')).toBeEnabled();
  await expect(page.locator('#confirm')).toHaveText('Cast Strike');
  await expect(page.locator('[data-creature="b2"]')).toHaveClass(/is-selected/);
  await expect(page.locator('[data-creature="b2"] .creature-tag')).toHaveText('SELECTED');
  await expect(page.locator('[data-creature="b1"] .creature-tag')).toHaveText('TARGET');
  await expect(page.locator('[data-creature="a2"] .creature-tag')).toHaveText('');
  await fits(page);
  await page.screenshot({ path: testInfo.outputPath('phaser-targeting.png') });
  await page.locator('#inspect').click();
  await expect(page.locator('#inspect-dialog')).toBeVisible();
  await expect(page.locator('#inspect-name')).toHaveText('Predator');
  await page.keyboard.press('Escape');
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'targeting');
  await page.locator('#cancel').click();
  await expect(page.locator('#choose')).toBeFocused();
  await expect(page.locator('.is-selected')).toHaveCount(0);
});

test('reduced motion reaches all fixed outcomes including defeat, then resets cleanly', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  for (const [target, result] of [['b1', '14/24 HP'], ['b2', '5/24 HP'], ['b3', '0/24 HP']]) {
    await choose(page, target);
    await page.locator('#confirm').click();
    await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
    await expect(page.locator(`[data-creature="${target}"] .creature-stats`)).toContainText(result);
    await expect(page.locator('#confirm')).toBeFocused();
    if (target === 'b3') {
      await expect(page.locator('[data-creature="b3"]')).toHaveClass(/is-defeated/);
      await expect(page.locator('[data-creature="b3"] .creature-tag')).toHaveText('DEFEATED');
    }
    await expect(page.locator('#confirm')).toHaveText('Play again');
    await page.locator('#reset').click();
    await expect(page.locator('.is-defeated')).toHaveCount(0);
    await expect(page.locator('[data-creature="b1"] .creature-stats')).toContainText('18/24 HP');
  }
});

test('keyboard casting and skip need no live API', async ({ page }) => {
  const apiRequests = [];
  page.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/')) apiRequests.push(request.url()); });
  await page.reload();
  await expect(page.locator('#lab')).toHaveAttribute('data-ready', 'true');
  await page.keyboard.press('1');
  await expect(page.locator('[data-creature="b1"]')).toBeFocused();
  await page.keyboard.press('Enter');
  await page.locator('#confirm').focus();
  await page.keyboard.press('Enter');
  await page.keyboard.press('Escape');
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
  await expect(page.locator('[data-creature="b1"] .creature-stats')).toContainText('14/24 HP');
  expect(apiRequests).toEqual([]);
});

test('reset during playback cancels pending results and a second cast resolves normally', async ({ page }) => {
  await choose(page);
  await page.locator('#confirm').click();
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'windup');
  await page.locator('#reset').click();
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'decision');
  // The cancelled cast's cues were due at 420 ms and 1250 ms: outlive both before looking again, because a
  // stale cue that still fired would show the impact on a board that was asked to forget it.
  await page.waitForTimeout(1500);
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'decision');
  await expect(page.locator('[data-creature="b1"] .creature-stats')).toContainText('18/24 HP');
  await expect(page.locator('[data-creature="b1"] .creature-result')).toHaveText('');
  await choose(page, 'b2');
  await page.locator('#confirm').click();
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
  await expect(page.locator('[data-creature="b1"] .creature-stats')).toContainText('18/24 HP');
  await expect(page.locator('[data-creature="b2"] .creature-stats')).toContainText('5/24 HP');
});

test('landscape, short portrait and resizing retain visible commands without scrolling', async ({ page }, testInfo) => {
  for (const viewport of [{ width: 844, height: 390 }, { width: 320, height: 568 }, { width: 720, height: 500 }]) {
    await page.setViewportSize(viewport);
    await expect.poll(() => page.locator('#phaser-mount canvas').evaluate(node => Math.round(node.getBoundingClientRect().width))).toBe(viewport.width);
    await fits(page);
    if (viewport.width === 844) await page.screenshot({ path: testInfo.outputPath('phaser-landscape.png') });
  }
});

test('manual reduced motion, chosen in a contained settings dialog, settles a cast at once', async ({ page }) => {
  await page.locator('#settings').click();
  await page.locator('#reduce-motion').check();
  await expect(page.locator('#lab')).toHaveAttribute('data-motion', 'reduced');
  await page.getByRole('button', { name: 'Close settings' }).click();
  await expect(page.locator('#settings')).toBeFocused();
  await choose(page);
  await page.locator('#confirm').click();
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
  await expect(page.locator('#skip')).toBeHidden();
});

test('2× playback still runs the timed cast, with a skip on offer, before it settles', async ({ page }) => {
  await page.locator('#settings').click();
  await page.locator('#playback-speed').selectOption('2');
  await page.getByRole('button', { name: 'Close settings' }).click();
  await expect(page.locator('#lab')).toHaveAttribute('data-motion', 'full');
  await choose(page);
  // At 2× the whole cast lasts 625 ms, too short for a poll from outside to be sure of seeing it in flight:
  // the page records its own stage changes, with the clock and whether a skip was on offer at each.
  await page.evaluate(() => {
    const lab = document.getElementById('lab');
    globalThis.__stages = [];
    new MutationObserver(() => globalThis.__stages.push({ stage: lab.dataset.stage, at: performance.now(), skip: !document.getElementById('skip').hidden }))
      .observe(lab, { attributes: true, attributeFilter: ['data-stage'] });
  });
  await page.locator('#confirm').click();
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
  await expect(page.locator('#confirm')).toBeFocused();
  await expect(page.locator('[data-creature="b1"] .creature-result')).toHaveText('-4 HP');
  const stages = await page.evaluate(() => globalThis.__stages);
  expect(stages.map(one => one.stage)).toEqual(['windup', 'impact', 'resolved']);
  expect(stages.map(one => one.skip)).toEqual([true, true, false]);
  expect(stages[2].at - stages[0].at).toBeGreaterThanOrEqual(600);
});

test('the hit targets stand over the figures the scene places', async ({ page }) => {
  // The page serves the layout the scene uses, so the test asks it where the figures are and holds every hit
  // target against that: a target that is not over its figure is a tap that misses.
  const stage = await page.locator('#battle-zone').boundingBox();
  const placed = await page.evaluate(async size => (await import('/table/arena-lab-layout.js')).placements(...size), [stage.width, stage.height]);
  expect(placed).toHaveLength(6);
  for (const [index, node] of (await page.locator('.creature').all()).entries()) {
    const box = await node.boundingBox();
    const { x, y, size } = placed[index];
    expect(Math.abs(box.x + box.width / 2 - (stage.x + x))).toBeLessThanOrEqual(2);
    expect(Math.abs(box.y - (stage.y + Math.max(0, y - size)))).toBeLessThanOrEqual(2);
  }
});
