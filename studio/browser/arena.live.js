import { test, expect } from '@playwright/test';
import { spawn } from 'node:child_process';
import { resolve } from 'node:path';
import { createServer } from 'node:net';

// The live arena reads the same authenticated projection as a real table host.

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

test('the real host supplies the arena and external decisions update it without arena writes', async ({ page, context }, info) => {
  await page.goto(join);
  await page.locator('#invitation-take').click();
  await expect(page.locator('#table, #pass').filter({ visible: true }).first()).toBeVisible({ timeout: 20000 });
  const tokens = await page.evaluate(() => JSON.parse(localStorage.getItem('downfall.table.seats')));
  const origin = new URL(join).origin;
  const headers = { 'X-Seat-Token': tokens.player1 };
  const before = await (await context.request.get(`${origin}/api/seat/player1`, { headers })).json();
  const arena = await context.newPage();
  const requests = [];
  arena.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/')) requests.push({ method: request.method(), url: request.url() }); });
  await arena.goto(`${origin}/arena`);
  await expect(arena.locator('#arena')).toHaveAttribute('data-ready', 'true');
  await arena.locator('#seat-choices button').filter({ hasText: 'Player 1' }).click();
  await expect(arena.locator('#loading')).toBeHidden();
  for (const one of before.board.allies) {
    await expect(arena.locator(`[data-creature="${one.id}"]`)).toHaveClass(/ally/);
    await expect(arena.locator(`[data-creature="${one.id}"] .creature-stats`)).toContainText(`${one.health}/${one.maxHealth} HP`);
  }
  await arena.screenshot({ path: info.outputPath('real-live-board.jpg'), type: 'jpeg', quality: 85 });
  // The driver acts through another client. The arena itself owns no command or shown acknowledgement.
  for (let attempt = 0; attempt < 4; attempt++) {
    const current = await (await context.request.get(`${origin}/api/seat/player1`, { headers })).json();
    if (current.waitingFor !== 'Evolution') break;
    const result = await context.request.post(`${origin}/api/seat/player1/decision`, { headers, data: { kind: 'Evolution', pass: true, asked: current.waitingAsked } });
    expect(result.status()).toBe(204);
  }
  await expect(arena.locator('#actor-meta')).toHaveText('Tempo', { timeout: 15000 });
  expect(requests.filter(one => one.method !== 'GET').map(one => new URL(one.url).pathname)).toEqual(['/api/lobby/resume']);
  expect(requests.some(one => new URL(one.url).searchParams.has('shown'))).toBe(false);
  await arena.close();
});
