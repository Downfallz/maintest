import { test, expect } from '@playwright/test';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { startHost, stopHost } from './host.js';

test.beforeEach(returningBattlePlayer);

// Let the real engine play up to the handover. The accepted seat opens the production battle directly.
for (const [round, art] of [[2, []], [4, ['fallen-ledge']], [6, ['breach']], [12, ['shattered-sky', 'broken-floor']]]) {
  test('the live battlefield at handover round ' + round, async ({ page }, info) => {
    const { host, join } = await startHost('at-activation', ['--handover', String(round), '--p2', 'random']);
    const loaded = [];
    page.on('response', response => {
      if (response.ok() && response.url().includes('/battle-art/horizon/')) loaded.push(new URL(response.url()).pathname);
    });
    try {
      await page.goto(join);
      await page.locator('#invitation-take').click();
      await page.waitForURL(/\/battle(?:\?|$)/);
      await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
      await expect(page.locator('#loading')).toBeHidden();
      await expect(page.locator('#round-number')).toHaveText(String(round).padStart(2, '0'));
      await expect(page.locator('.creature').first()).toBeVisible();
      await Promise.all(art.map(file => expect.poll(() => loaded).toContain('/battle-art/horizon/' + file + '.webp')));
      // The handover is an even-round tempo choice; close its sheet to see the entire playing surface. A page that
      // loads while the host still plays to the handover sees the round's upkeep arrive, and its receipt is read
      // before the tempos open by themselves (#880): the journey reads it as a player would.
      await expect(async () => {
        if (await page.locator('#upkeep-dialog').isVisible()) await page.locator('#upkeep-close').click({ timeout: 2000 });
        await expect(page.locator('#planning-dialog')).toBeVisible({ timeout: 2000 });
      }).toPass({ timeout: 30000 });
      await page.locator('#planning-close').click();
      await expect(page.locator('#planning-dialog')).toBeHidden();
      await page.screenshot({ path: info.outputPath('live-round-' + round + '.jpg'), type: 'jpeg', quality: 86 });
    } finally { await stopHost(host); }
  });
}
