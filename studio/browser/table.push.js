import { test, expect } from '@playwright/test';
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve, join } from 'node:path';
import { createServer } from 'node:net';

// A host with push keys offers "your turn" on the phone (ADR 0118): the page registers the worker, asks the
// host for its key, and shows the one button. The browser here has no push service to subscribe with, so the
// tap is as far as this goes; what it proves is the wiring on the real host, which the unit tests cannot.

const root = resolve('../..');
let host, seatLink, keysDirectory;

function run(args) {
  const dotnet = resolve(process.env.DOTNET_ROOT ?? '/usr/share/dotnet', 'dotnet');
  const cli = resolve(root, 'artifacts/bin/DownfallArena.Cli/debug/DownfallArena.Cli.dll');
  return spawn(dotnet, [cli, ...args], { cwd: root });
}

test.beforeAll(async () => {
  const probe = createServer();
  await new Promise(done => probe.listen(0, '127.0.0.1', done));
  const port = probe.address().port;
  await new Promise(done => probe.close(done));

  keysDirectory = mkdtempSync(join(tmpdir(), 'push-keys-'));
  const keys = await new Promise((done, fail) => {
    let out = '';
    const printing = run(['push-keys']);
    printing.stdout.on('data', data => { out += data.toString(); });
    printing.on('exit', code => (code === 0 ? done(out) : fail(new Error(`push-keys exited ${code}`))));
  });
  const keysFile = join(keysDirectory, 'push-keys.json');
  writeFileSync(keysFile, keys);

  host = run(['table', '--p2', 'greedy', '--no-record', '--seed', '7', '--port', String(port), '--push-keys', keysFile]);
  let log = '';
  host.stdout.on('data', data => { log += data.toString(); });
  host.stderr.on('data', data => { log += data.toString(); });
  await expect.poll(() => log, { timeout: 20000, message: 'Table host starts' }).toContain('joins at');
  expect(log).toContain('Notices: on');
  seatLink = /joins at (\S+)/.exec(log)[1];
});

test.afterAll(async () => {
  if (host?.exitCode === null) {
    const closed = new Promise(done => host.once('exit', done));
    host.kill('SIGINT');
    await closed;
  }
  if (keysDirectory) rmSync(keysDirectory, { recursive: true, force: true });
});

test('a seat page on a host with keys registers the worker and offers the notices button', async ({ page, context }) => {
  await context.grantPermissions(['notifications']);
  await page.goto(seatLink);
  await page.locator('#invitation-take').click();
  await expect(page.locator('#table, #pass').filter({ visible: true }).first()).toBeVisible({ timeout: 20000 });

  // Offered, as "notify me" where the browser lets a page ask, as "blocked" where it does not: the headless
  // browser on the CI runner answers every notification permission with a refusal, and the page says so.
  const button = page.locator('#notices-match');
  await expect(button).toHaveText(/Notify me when it's my turn|Notifications are blocked/, { timeout: 10000 });
  // Offered once on entering, too: a player who arrives second never waits in the room that offered it before.
  // Blocked is a "no" already said, and is not offered again.
  const offer = page.locator('#notices-offer');
  if (await button.isEnabled()) {
    await expect(offer).toBeVisible();
    await expect(page.locator('#notices-offer-yes')).toHaveText("Notify me when it's my turn");
    await page.locator('#notices-offer-no').click();
    await expect(offer).toBeHidden();
    await page.reload();
    await expect(button).toHaveText("Notify me when it's my turn", { timeout: 10000 });
    await expect(offer).toBeHidden();
  } else {
    await expect(offer).toBeHidden();
  }
  await expect.poll(() => page.evaluate(async () => Boolean(await navigator.serviceWorker.getRegistration('/'))), { timeout: 10000 }).toBe(true);
  const key = await page.evaluate(async () => (await (await fetch('/api/lobby/push')).json()).publicKey);
  expect(key).toMatch(/^[A-Za-z0-9_-]{87}$/);

  // Tapping asks the browser for a subscription. Headless Chromium has no push service behind it, so this ends
  // in a refusal the page takes quietly: the button is offered again, and nothing is left half done. Only
  // where the tap is offered at all.
  await page.locator('#match-tools > summary').click();
  if (await button.isEnabled()) {
    await button.click();
    await expect(button).toBeEnabled({ timeout: 10000 });
    await expect(button).toHaveText(/Notify me when it's my turn|Stop notifying me/);
  }
});
