import { test, expect } from '@playwright/test';

// One Phaser page at a time: under the runner's software rendering a 1440x1000 WebGL canvas is the heaviest
// thing this suite opens, and two of them side by side starved the worker until clicks never returned.
test.describe.configure({ mode: 'default' });

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
// Fake timers, paused: from here on the page's clock moves only when the test runs it.
async function pauseClock(page) {
  await page.clock.install({ time: '2026-11-03T12:00:00Z' });
  await page.clock.pauseAt('2026-11-03T12:00:01Z');
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
  // The page's timers are the test's to run: a cast is then in flight for as long as the test says, and the
  // cancelled cues get their due time to fire, which a stale one would, on a board asked to forget it.
  await pauseClock(page);
  await choose(page);
  await page.locator('#confirm').click();
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'windup');
  await page.locator('#reset').click();
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'decision');
  await page.clock.runFor(2000);
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'decision');
  await expect(page.locator('[data-creature="b1"] .creature-stats')).toContainText('18/24 HP');
  await expect(page.locator('[data-creature="b1"] .creature-result')).toHaveText('');
  await choose(page, 'b2');
  await page.locator('#confirm').click();
  await page.clock.runFor(1250);
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

test('2× playback runs the timed cast at half its cues, with a skip on offer until it settles', async ({ page }) => {
  await page.locator('#settings').click();
  await page.locator('#playback-speed').selectOption('2');
  await page.getByRole('button', { name: 'Close settings' }).click();
  await expect(page.locator('#lab')).toHaveAttribute('data-motion', 'full');
  await pauseClock(page);
  await choose(page);
  await page.locator('#confirm').click();
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'windup');
  await expect(page.locator('#skip')).toBeVisible();
  await expect(page.locator('#skip')).toBeFocused();
  // The impact is due at 210 ms and the settled frame at 625 ms, half of 420 and 1250.
  await page.clock.runFor(209);
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'windup');
  await page.clock.runFor(1);
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'impact');
  await expect(page.locator('[data-creature="b1"] .creature-result')).toHaveText('-4 HP');
  await page.clock.runFor(414);
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'impact');
  await page.clock.runFor(1);
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
  await expect(page.locator('#skip')).toBeHidden();
  await expect(page.locator('#confirm')).toBeFocused();
});

test('the hit targets stand over the figures the scene places', async ({ page }) => {
  // The page serves the layout the scene uses, so the test asks it where the figures are and holds every hit
  // target against that: a target that is not over its figure is a tap that misses.
  const stage = await page.locator('#battle-zone').boundingBox();
  const placed = await page.evaluate(async size => (await import('/table/arena-layout.js')).placements(...size), [stage.width, stage.height]);
  expect(placed).toHaveLength(6);
  for (const [index, node] of (await page.locator('.creature').all()).entries()) {
    const box = await node.boundingBox();
    const { x, y, size } = placed[index];
    expect(Math.abs(box.x + box.width / 2 - (stage.x + x))).toBeLessThanOrEqual(2);
    expect(Math.abs(box.y - (stage.y + Math.max(0, y - size)))).toBeLessThanOrEqual(2);
  }
});
