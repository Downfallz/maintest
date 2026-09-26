import { test, expect } from '@playwright/test';
import { spawn } from 'node:child_process';
import { resolve } from 'node:path';
import { createServer } from 'node:net';

let host, url, token;
const root = resolve('../..');

test.beforeAll(async () => {
  const probe = createServer();
  await new Promise(done => probe.listen(0, '127.0.0.1', done));
  const port = probe.address().port;
  await new Promise(done => probe.close(done));
  host = spawn('dotnet', [resolve(root, 'artifacts/bin/DownfallArena.Cli/debug/DownfallArena.Cli.dll'), 'table', '--practice', '--port', String(port)], { cwd: root });
  let log = '';
  host.stdout.on('data', data => { log += data.toString(); });
  host.stderr.on('data', data => { log += data.toString(); });
  await expect.poll(() => log, { timeout: 20000, message: 'Practice host starts' }).toContain('Practice table:');
  const link = /Practice table: (\S+)/.exec(log)[1];
  token = new URL(link).searchParams.get('practice');
  url = new URL('/', link).href;
  await expect.poll(async () => {
    try { return (await fetch(url)).status; } catch { return 0; }
  }).toBe(200);
});

test.afterAll(async () => {
  if (host && host.exitCode === null) {
    const closed = new Promise(done => host.once('exit', done));
    host.kill('SIGINT');
    await closed;
  }
});

async function state() {
  const response = await fetch(`${url}api/practice`, { headers: { 'X-Seat-Token': token } });
  return response.json();
}

async function open(page, scenario) {
  await page.goto(`${url}?practice=${token}`);
  await page.locator('#practice-scenario').selectOption(scenario);
  await page.locator('#practice-start').click();
  await expect(page.locator('#practice-status')).toContainText('not recorded');
  await expect(page.locator('#table')).toBeVisible();
  await expect(page.locator('#playback')).toBeHidden();
}

test('target previews stay readable on a phone and restart clears the selection', async ({ page }, info) => {
  await open(page, 'targeting');
  await expect(page.locator('#decision-guide')).toContainText('If cast on the current board');
  await expect(page.locator('#decision-guide')).toContainText('Hidden choices and future rolls are unknown');
  await page.locator('[data-focus="target-4"]').click();
  await expect(page.locator('#decision-guide')).toContainText('Creature 4');
  await expect(page.locator('#decision-guide')).not.toContainText('Creature 5');
  await expect(page.locator('#choices')).toContainText('Cast on 1 of 1');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.locator('#decision').scrollIntoViewIfNeeded();
  await page.screenshot({ path: info.outputPath('target-preview.png'), animations: 'disabled' });
  const old = (await state()).seatToken;
  await page.locator('#practice-start').click();
  await expect.poll(async () => (await state()).seatToken).not.toBe(old);
  await expect(page.locator('#choices')).toContainText('Cast on 0 of 1');
  await expect(page.locator('#decision-guide')).toContainText('Creature 5');
});

test('multiclass starts at the new opportunity with existing packages visible', async ({ page }) => {
  await open(page, 'multiclass');
  await expect(page.locator('#phase')).toContainText('Round 3');
  await expect(page.locator('#evolution-budget')).toContainText('2 / 2');
  await expect(page.locator('#choices')).toContainText('Occultist');
  await expect(page.locator('#choices')).toContainText('Berserker');
  await page.locator('#tab-mat').click();
  await expect(page.locator('#mat')).toBeVisible();
});

for (const scenario of ['stun', 'resolution']) {
  test(`${scenario} reaches a real resolution and its before/after replay`, async ({ page }, info) => {
    await open(page, scenario);
    const expected = scenario === 'stun' ? 'Tranquilizer Dart' : 'Toxic Waves';
    await expect(page.locator('#asking')).toContainText(expected);
    await expect(page.locator('#decision-guide')).toContainText(scenario === 'stun' ? 'Stun' : 'Bleed');
    const seatToken = (await state()).seatToken;
    // Finish the few remaining choices through the real API, then inspect its actual replay in the page.
    for (let step = 0; step < 8; step++) {
      const response = await fetch(`${url}api/seat/player1`, { headers: { 'X-Seat-Token': seatToken } });
      const view = await response.json();
      if (view.board.roundNumber > 5) break;
      if (view.waitingFor !== 'Target') continue;
      const targets = view.options.target.legalTargets.candidates.slice(0, view.options.target.legalTargets.maxTargets);
      const posted = await fetch(`${url}api/seat/player1/decision`, {
        method: 'POST', headers: { 'X-Seat-Token': seatToken, 'Content-Type': 'application/json' },
        body: JSON.stringify({ kind: 'Target', targets, asked: view.waitingAsked }),
      });
      expect(posted.status).toBe(204);
    }
    await expect(page.locator('#playback')).toBeVisible();
    await expect(page.locator('#playback-board-note')).toContainText('Before action');
    await page.locator('[data-focus="playback-next"]').click();
    await expect(page.locator('#playback-board-note')).toContainText('After action');
    await page.screenshot({ path: info.outputPath(`${scenario}-resolution.png`), animations: 'disabled' });
  });
}
