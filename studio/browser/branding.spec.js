import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

for (const colorScheme of ['light', 'dark']) {
  test(`the brand survives ${colorScheme} OS preference on every static shell`, async ({ page }) => {
    await page.emulateMedia({ colorScheme });
    // This check isolates delivered HTML/CSS; behaviour is exercised by the existing journeys.
    await page.route('**/*.js', route => route.fulfill({ body: '', contentType: 'text/javascript' }));
    for (const path of ['/', '/table/lobby.html', '/table/index.html', '/table/admin.html', '/table/pilot.html', '/table/leaderboard.html', '/table/arena.html']) {
      await page.goto(path);
      await expect(page.locator('html')).toHaveCSS('color-scheme', 'dark');
      const palette = await page.evaluate(() => {
        const style = getComputedStyle(document.documentElement);
        return ['--paper', '--panel', '--gold', '--accent'].map(name => style.getPropertyValue(name).trim());
      });
      expect(palette).toEqual(['#0b1519', '#132228', '#e7c183', '#e7c183']);
      const sample = await page.evaluate(() => {
        const button = document.createElement('button');
        button.style.cssText = 'background:var(--accent);color:var(--accent-ink)';
        document.body.append(button);
        const style = getComputedStyle(button);
        return [style.backgroundColor, style.color];
      });
      expect(sample).toEqual(['rgb(231, 193, 131)', 'rgb(11, 21, 25)']);
    }
  });
}

test('a standalone viewer carries its palette and readable canvas labels without a stylesheet request', async ({ page }) => {
  const root = resolve('../..');
  const html = readFileSync(`${root}/viewer/index.html`, 'utf8').replace('<link rel="stylesheet" href="viewer.css">',
    `<style>${readFileSync(`${root}/viewer/viewer.css`, 'utf8')}</style>`);
  await page.route('https://cdn.jsdelivr.net/**', route => route.fulfill({ body: '', contentType: 'text/javascript' }));
  await page.setContent(html);
  await expect(page.locator('body')).toHaveCSS('background-color', 'rgb(11, 21, 25)');
  expect(await page.evaluate(() => [PLAYER1_COLOUR, PLAYER2_COLOUR, ACCENT, WARN])).toEqual(['#8bc6ed', '#e3a29a', '#8bc6ed', '#e7c183']);
  const chartColours = await page.evaluate(() => {
    window.Chart = class { static defaults = {}; };
    chart(document.createElement('canvas'), { type: 'bar' });
    return Chart.defaults;
  });
  expect(chartColours).toEqual({ color: '#adb8bd', borderColor: '#52636b' });
});
