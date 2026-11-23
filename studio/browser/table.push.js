import { returningBattlePlayer } from './battle-help-fixture.js';
import { test, expect } from '@playwright/test';
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve, join } from 'node:path';
import { createServer } from 'node:net';
import { classicTable, archivedTable } from './classic-table-fixture.js';
import { dotnetBinary } from './dotnet.js';
test.beforeEach(classicTable);

// A host with push keys offers "your turn" on the phone (ADR 0118): the page registers the worker, asks the
// host for its key, and shows the one button. The browser here has no push service to subscribe with, so the
// tap is as far as this goes; what it proves is the wiring on the real host, which the unit tests cannot.

const root = resolve('../..');
let host, seatLink, keysDirectory, keysFile, port;

function run(args) {
  const dotnet = dotnetBinary();
  const cli = resolve(root, 'artifacts/bin/DownfallArena.Cli/debug/DownfallArena.Cli.dll');
  return spawn(dotnet, [cli, ...args], { cwd: root });
}

test.beforeAll(async () => {
  const probe = createServer();
  await new Promise(done => probe.listen(0, '127.0.0.1', done));
  port = probe.address().port;
  await new Promise(done => probe.close(done));

  keysDirectory = mkdtempSync(join(tmpdir(), 'push-keys-'));
  const keys = await new Promise((done, fail) => {
    let out = '';
    const printing = run(['push-keys']);
    printing.stdout.on('data', data => { out += data.toString(); });
    printing.on('exit', code => (code === 0 ? done(out) : fail(new Error(`push-keys exited ${code}`))));
  });
  keysFile = join(keysDirectory, 'push-keys.json');
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
  // The offer of pop-ups is the lobby's and the battle view's (notifications plan, N4): the classic page has none.
  await expect(page.locator('#notices-offer')).toHaveCount(0);
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
  // The battle uses the same host key and worker; entering neither subscribes nor sends a diagnostic.
  await returningBattlePlayer({ context });
  const battleWrites = [];
  page.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/lobby/push') && request.method() !== 'GET') battleWrites.push(request.method()); });
  await page.goto(new URL('/battle', seatLink).href);
  await expect(page.locator('#loading')).toBeHidden();
  await expect(page.locator('#upkeep-dialog')).toBeVisible(); await page.locator('#upkeep-close').click();
  await page.locator('#settings').click();
  await expect(page.locator('#notices-toggle')).toHaveText(/Notify me when it's my turn|Notifications are blocked|Stop notifying me/);
  await expect(page.locator('#notices-problem')).toBeEmpty();
  // An already opted-in endpoint may be rebound; subscriptions/test requests are never automatic.
  expect(battleWrites.every(method => method === 'PUT')).toBe(true);

});

// The offer in the lobby (N4): a signed-in player whose game against a person waits on the other player is offered
// pop-ups by the page's own card, never the browser's prompt first; Not now waits a week, Don't ask again ends it.
test('the lobby offers pop-ups when a game against a person waits, and keeps the answer', async ({ page, context }) => {
  const probe = createServer();
  await new Promise(done => probe.listen(0, '127.0.0.1', done));
  const lobbyPort = probe.address().port;
  await new Promise(done => probe.close(done));
  const lobbyHost = run(['table', '--p2', 'greedy', '--record', mkdtempSync(join(keysDirectory, 'sessions-')), '--seed', '7', '--accounts', 'dev', '--port', String(lobbyPort), '--push-keys', keysFile]);
  let log = '';
  lobbyHost.stdout.on('data', data => { log += data.toString(); });
  lobbyHost.stderr.on('data', data => { log += data.toString(); });
  try {
    await expect.poll(() => log, { timeout: 20000, message: 'Table host starts' }).toContain('joins at');
    const origin = `http://127.0.0.1:${lobbyPort}`;
    // Headless Chromium answers every notification permission with a refusal, which the card rightly takes as a no:
    // the page is shown a browser never asked, whose prompt is closed without an answer.
    await context.addInitScript(() => {
      Object.defineProperty(Notification, 'permission', { configurable: true, get: () => 'default' });
      Notification.requestPermission = async () => 'default';
    });
    const headers = { Origin: origin };
    expect((await page.request.post(`${origin}/api/account/dev`, { data: { username: 'Walker' }, headers })).ok()).toBe(true);
    // The first tutorial would open by itself for a new account: skipped, as a returning player has.
    expect((await page.request.post(`${origin}/api/tutorials/sc-repo-tutorial-your-first-combat/skip`, { data: {}, headers })).ok()).toBe(true);
    expect((await page.request.post(`${origin}/api/lobby/tables`, { data: { opponent: 'person' }, headers })).ok()).toBe(true);

    await page.goto(`${origin}/`);
    const offer = page.locator('#push-offer');
    await expect(offer).toBeVisible({ timeout: 20000 });
    await expect(page.locator('#push-offer-title')).toHaveText('Away from the game?');
    await expect(page.locator('#push-offer-on')).toHaveText('Turn on');
    // A region, not a window: the focus stays where the player is.
    expect(await page.evaluate(() => document.getElementById('push-offer').contains(document.activeElement))).toBe(false);
    await page.locator('#push-offer-later').click();
    await expect(offer).toBeHidden();
    expect(JSON.parse(await page.evaluate(() => localStorage.getItem('downfall.push-offer'))).asked).toBe(1);
    await page.reload();
    await expect(page.locator('#my-tables')).not.toContainText('Finding');
    await expect(offer).toBeHidden();

    // A week later it asks again; Turn on asks the browser, which has no push service here: it says so, and where the switch is.
    await page.evaluate(() => localStorage.setItem('downfall.push-offer', JSON.stringify({ never: false, asked: 1, last: Date.now() - 8 * 24 * 3600 * 1000 })));
    await page.reload();
    await expect(offer).toBeVisible({ timeout: 20000 });
    await page.locator('#push-offer-on').click();
    await expect(page.locator('#push-offer-said')).toHaveText('Pop-ups stay off. You can turn them on in the menu, under Settings, Notifications.', { timeout: 10000 });
    await expect(page.locator('#push-offer-close')).toBeFocused();
    await page.locator('#push-offer-close').click();
    await expect(offer).toBeHidden();

    await page.evaluate(() => localStorage.removeItem('downfall.push-offer'));
    await page.reload();
    await expect(offer).toBeVisible({ timeout: 20000 });
    await page.locator('#push-offer-never').click();
    await page.reload();
    await expect(page.locator('#my-tables')).not.toContainText('Finding');
    await expect(offer).toBeHidden();
    expect(JSON.parse(await page.evaluate(() => localStorage.getItem('downfall.push-offer'))).never).toBe(true);
  } finally {
    if (lobbyHost.exitCode === null) {
      const closed = new Promise(done => lobbyHost.once('exit', done));
      lobbyHost.kill('SIGINT');
      await closed;
    }
  }
});

// A host with accounts off (a local or LAN host, #1127): nobody is signed in, so the lobby asks through the seat of
// the game that waits, as the battle view does, and the card is made all the same.
test('a keyed host with accounts off offers pop-ups in the lobby, through the waiting game\'s seat', async ({ page, context }) => {
  const probe = createServer();
  await new Promise(done => probe.listen(0, '127.0.0.1', done));
  const lobbyPort = probe.address().port;
  await new Promise(done => probe.close(done));
  const lobbyHost = run(['table', '--p2', 'greedy', '--record', mkdtempSync(join(keysDirectory, 'sessions-')), '--seed', '7', '--accounts', 'off', '--port', String(lobbyPort), '--push-keys', keysFile]);
  let log = '';
  lobbyHost.stdout.on('data', data => { log += data.toString(); });
  lobbyHost.stderr.on('data', data => { log += data.toString(); });
  try {
    await expect.poll(() => log, { timeout: 20000, message: 'Table host starts' }).toContain('joins at');
    const origin = `http://127.0.0.1:${lobbyPort}`;
    await context.addInitScript(() => {
      Object.defineProperty(Notification, 'permission', { configurable: true, get: () => 'default' });
      Notification.requestPermission = async () => 'default';
    });
    const keyReads = [];
    page.on('request', request => { if (new URL(request.url()).pathname === '/api/lobby/push') keyReads.push(request.headers()['x-seat-token'] ?? null); });
    // A table opened for another player from this browser, which keeps its seat; the battle page is not needed here.
    // This suite keeps the classic presentation (classicTable): the seat opens on /play.
    await page.route(/\/(battle|play)\?player1=/, route => route.fulfill({ contentType: 'text/html', body: '<title>Seat</title>' }));
    await page.goto(`${origin}/`);
    await page.locator('#new-table').click();
    await page.locator('#create-submit').click();
    await page.waitForURL(/\/(battle|play)\?player1=/);
    await page.goto(`${origin}/`);

    const offer = page.locator('#push-offer');
    await expect(offer).toBeVisible({ timeout: 20000 });
    await expect(page.locator('#push-offer-title')).toHaveText('Away from the game?');
    expect(keyReads.some(token => token)).toBe(true);
    await page.locator('#push-offer-on').click();
    await expect(page.locator('#push-offer-said')).toHaveText('Pop-ups stay off. You can turn them on in the menu, under Settings, Notifications.', { timeout: 10000 });
  } finally {
    if (lobbyHost.exitCode === null) {
      const closed = new Promise(done => lobbyHost.once('exit', done));
      lobbyHost.kill('SIGINT');
      await closed;
    }
  }
});
