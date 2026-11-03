import { test, expect } from '@playwright/test';

// This study has no server data dependency. Any API request would break its isolation from a real table.
test.beforeEach(async ({ page }) => {
  await page.goto('/table/arena-lab.html');
});

async function expectNoOverflow(page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(page.viewportSize().width);
}

async function choose(page, target = 'b1') {
  await page.locator('#choose').click();
  await page.locator(`[data-creature="${target}"]`).click();
}

test('both compositions keep six readable targets without horizontal overflow', async ({ page }, testInfo) => {
  await expect(page.locator('.creature')).toHaveCount(6);
  for (const layout of ['arena', 'tableau']) {
    await page.locator(`button[data-layout="${layout}"]`).click();
    await expectNoOverflow(page);
    for (const node of await page.locator('.creature').all()) {
      const box = await node.boundingBox();
      expect(box.width).toBeGreaterThanOrEqual(44);
      expect(box.height).toBeGreaterThanOrEqual(44);
    }
    await page.screenshot({ path: testInfo.outputPath(`${layout}.png`), fullPage: true });
  }
});

test('targeting is explicit, cancellable and preserved when comparing compositions', async ({ page }) => {
  await choose(page, 'b2');
  await expect(page.locator('#confirm')).toBeEnabled();
  await expect(page.locator('[data-creature="b2"]')).toHaveClass(/is-selected/);
  await page.locator('button[data-layout="tableau"]').click();
  await expect(page.locator('[data-creature="b2"]')).toHaveClass(/is-selected/);
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'targeting');
  await page.locator('#cancel').click();
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'decision');
  await expect(page.locator('#choose')).toBeFocused();
  await expect(page.locator('.is-selected')).toHaveCount(0);
});

test('reduced motion reaches each fixed result and reset restores every creature', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  for (const [target, result] of [['b1', '14/24 HP'], ['b2', '5/24 HP'], ['b3', '0/24 HP']]) {
    await choose(page, target);
    await page.locator('#confirm').click();
    await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
    await expect(page.locator(`[data-creature="${target}"] .creature-stats`)).toContainText(result);
    await expect(page.locator('#confirm')).toBeFocused();
    if (target === 'b3') await expect(page.locator('[data-creature="b3"]')).toHaveClass(/is-defeated/);
    expect(await page.locator('.creature svg').first().evaluate(node => getComputedStyle(node).animationName)).toBe('none');
    await page.locator('#reset').click();
    await expect(page.locator('.is-defeated')).toHaveCount(0);
    await expect(page.locator('[data-creature="b1"] .creature-stats')).toContainText('18/24 HP');
  }
});

test('keyboard targeting and skip work without a pointer or a live API', async ({ page }) => {
  const apiRequests = [];
  page.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/')) apiRequests.push(request.url()); });
  await page.reload();
  await page.locator('#choose').focus();
  await page.keyboard.press('Enter');
  await expect(page.locator('[data-creature="b1"]')).toBeFocused();
  await page.keyboard.press('Enter');
  await page.locator('#confirm').focus();
  await page.keyboard.press('Enter');
  await page.keyboard.press('Escape');
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
  await expect(page.locator('[data-creature="b1"] .creature-stats')).toContainText('14/24 HP');
  expect(apiRequests).toEqual([]);
});

test('landscape keeps both teams and commands within the viewport width', async ({ page }, testInfo) => {
  await page.setViewportSize({ width: 844, height: 390 });
  for (const layout of ['arena', 'tableau']) {
    await page.locator(`button[data-layout="${layout}"]`).click();
    await expectNoOverflow(page);
  }
  await page.screenshot({ path: testInfo.outputPath('landscape.png'), fullPage: true });
});
