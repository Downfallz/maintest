import { test, expect } from '@playwright/test';
import { fitsBattlefield } from './fit.js';

test.use({ deviceScaleFactor: 3, reducedMotion: 'reduce' });

test('the dense framebuffer keeps the battlefield and touch targets in CSS coordinates after rotation', async ({ page }, info) => {
  test.skip(info.project.name !== 'phone', 'One explicit 3× phone profile covers both orientations.');
  await page.goto('/table/battle-lab.html');
  await expect(page.locator('#lab')).toHaveAttribute('data-ready', 'true');
  for (const size of [{ width: 390, height: 844 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(size);
    const canvas = page.locator('#phaser-mount canvas');
    await expect.poll(() => canvas.evaluate(node => [node.width / node.clientWidth, node.height / node.clientHeight])).toEqual([3, 3]);
    await fitsBattlefield(page);
    await page.locator('#choose').click();
    await page.locator('[data-creature="b2"]').tap();
    await expect(page.locator('[data-creature="b2"]')).toHaveClass(/is-selected/);
    await page.locator('#cancel').click();
  }
});
