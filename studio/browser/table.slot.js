import { test, expect } from '@playwright/test';
import { spawn } from 'node:child_process';
import { resolve } from 'node:path';
import { createServer } from 'node:net';

// A table that chooses each spell at its slot (ADR 0110), played by a person against Greedy through the real
// engine: nothing is declared, and when a creature's turn comes its player taps a card, picks that spell's
// targets and confirms, and the one action resolves. The spell can be changed until the targets are confirmed.

const root = resolve('../..');
let host, join;

test.beforeAll(async () => {
  const probe = createServer();
  await new Promise(done => probe.listen(0, '127.0.0.1', done));
  const port = probe.address().port;
  await new Promise(done => probe.close(done));
  const dotnet = resolve(process.env.DOTNET_ROOT ?? '/usr/share/dotnet', 'dotnet');
  const cli = resolve(root, 'artifacts/bin/DownfallArena.Cli/debug/DownfallArena.Cli.dll');
  host = spawn(dotnet, [cli, 'table', '--declaration', 'at-activation', '--p2', 'greedy', '--no-record', '--seed', '7', '--port', String(port)], { cwd: root });
  let log = '';
  host.stdout.on('data', data => { log += data.toString(); });
  host.stderr.on('data', data => { log += data.toString(); });
  await expect.poll(() => log, { timeout: 20000, message: 'Table host starts' }).toContain('joins at');
  join = /joins at (\S+)/.exec(log)[1];
});

test.afterAll(async () => {
  if (host?.exitCode === null) {
    const closed = new Promise(done => host.once('exit', done));
    host.kill('SIGINT');
    await closed;
  }
});

/** A visible control matching the selector, or null: what the page offers right now. */
async function offered(page, selector, options) {
  const found = page.locator(selector, options).filter({ visible: true }).first();
  return (await found.count()) > 0 && await found.isEnabled() ? found : null;
}

test('a person plays their creatures\' actions at the slot, a card then its targets', async ({ page }, info) => {
  test.setTimeout(90000);
  const posted = [];
  const answered = [];
  page.on('request', request => {
    if (request.method() === 'POST' && request.url().endsWith('/decision')) posted.push(JSON.parse(request.postData()));
  });
  page.on('response', response => {
    if (response.request().method() === 'POST' && response.url().endsWith('/decision')) answered.push(response.status());
  });
  await page.goto(join);
  await expect(page.locator('#table, #pass').filter({ visible: true }).first()).toBeVisible({ timeout: 20000 });

  let changedSpell = false;
  for (let step = 0; step < 400 && posted.filter(one => one.kind === 'Action').length < 3; step++) {
    const control = await offered(page, '#pass-ready') ?? await offered(page, '[data-focus="step-skip"]') ?? await offered(page, '[data-focus="step-next"]');
    if (control) { await control.click(); continue; }

    if (/your turn: choose a spell/.test(await page.locator('#asking').textContent() ?? '')) {
      const cards = page.locator('#own-hand .offered').filter({ visible: true });
      const several = await cards.count() > 1;
      await cards.first().click();
      if (!changedSpell && several) {
        // Going back is free: nothing was sent, and another card leads to that spell's own targets.
        const first = await page.locator('#asking').textContent();
        await page.locator('[data-focus="slot-back"]').click();
        await expect(page.locator('#asking')).toContainText('your turn: choose a spell');
        await cards.nth(1).click();
        await expect(page.locator('#asking')).not.toHaveText(first ?? '');
        await page.screenshot({ path: info.outputPath('slot-targets.png'), animations: 'disabled' });
        changedSpell = true;
      }
      await page.locator('[data-focus^="target-"], [data-focus^="mini-target-"]').filter({ visible: true }).first().click();
      await page.locator('#choices button', { hasText: /^Cast on/ }).click();
      continue;
    }

    const answer = await offered(page, '#choices button', { hasText: /^Pass/ })
      ?? await offered(page, '#choices button', { hasText: /^Standard$/ })
      ?? await offered(page, '#choices button', { hasText: /^Keep the order/ });
    if (answer) { await answer.click(); continue; }
    await page.waitForTimeout(200);
  }

  const actions = posted.filter(one => one.kind === 'Action');
  expect(actions.length).toBeGreaterThanOrEqual(3);
  expect(changedSpell).toBe(true);
  expect(answered.filter(status => status !== 204)).toEqual([]);
  expect(posted.filter(one => one.kind === 'Intent' || one.kind === 'Target')).toEqual([]);
  for (const action of actions) {
    expect(action.spell).toBeTruthy();
    expect(action.targets.length).toBeGreaterThan(0);
  }
});
