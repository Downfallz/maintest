import { test, expect } from '@playwright/test';
import { spawn } from 'node:child_process';
import { resolve } from 'node:path';
import { createServer } from 'node:net';

const root = resolve('../..');
async function startHost(declaration) {
  const probe = createServer(); await new Promise(done => probe.listen(0, '127.0.0.1', done));
  const port = probe.address().port; await new Promise(done => probe.close(done));
  const host = spawn(resolve(process.env.DOTNET_ROOT ?? '/usr/share/dotnet', 'dotnet'),
    [resolve(root, 'artifacts/bin/DownfallArena.Cli/debug/DownfallArena.Cli.dll'), 'table', '--declaration', declaration,
      '--p2', 'greedy', '--no-record', '--seed', '7', '--port', String(port)], { cwd: root });
  let log = ''; host.stdout.on('data', data => { log += data; }); host.stderr.on('data', data => { log += data; });
  try {
    await expect.poll(() => log, { timeout: 20000, message: 'Table host starts' }).toContain('joins at');
    return { host, join: /joins at (\S+)/.exec(log)[1] };
  } catch (error) { host.kill('SIGINT'); throw error; }
}

for (const declaration of ['at-activation', 'hidden-intent']) test(`${declaration}: arena combat commands resolve through the real engine`, async ({ page, context }, info) => {
  test.setTimeout(90000);
  const { host, join } = await startHost(declaration);
  try {
    await page.goto(join); await page.locator('#invitation-take').click();
    await expect(page.locator('#table, #pass').filter({ visible: true }).first()).toBeVisible({ timeout: 20000 });
    const tokens = await page.evaluate(() => JSON.parse(localStorage.getItem('downfall.table.seats')));
    const origin = new URL(join).origin, headers = { 'X-Seat-Token': tokens.player1 };
    const read = async () => (await context.request.get(`${origin}/api/seat/player1`, { headers })).json();
    async function nextQuestion() {
      let view;
      await expect.poll(async () => { view = await read(); return view.waitingFor ?? (view.over ? 'over' : null); }, { timeout: 15000 }).not.toBeNull();
      return view;
    }
    // The driver answers only planning questions; every combat decision comes from the visible arena.
    const planning = async view => {
      const data = view.waitingFor === 'Evolution' ? { kind: 'Evolution', pass: true }
        : view.waitingFor === 'Speed' ? { kind: 'Speed', creature: view.waitingCreature, speed: 'Standard' }
          : view.waitingFor === 'TieOrder' ? { kind: 'TieOrder', order: view.options.tieOrder.ties.flat() } : null;
      if (!data) return false;
      expect((await context.request.post(`${origin}/api/seat/player1/decision`, { headers, data: { ...data, asked: view.waitingAsked } })).status()).toBe(204);
      return true;
    };
    const sent = [], shown = [], snapshots = [];
    await page.goto(`${origin}/arena`);
    page.on('request', request => {
      const url = new URL(request.url());
      if (url.searchParams.has('shown')) shown.push(Number(url.searchParams.get('shown')));
      if (url.pathname.endsWith('/decision')) sent.push({ command: request.postDataJSON(), seen: [...shown] });
    });
    page.on('response', async response => {
      if (/\/api\/seat\/player1(?:\?|$)/.test(response.url()) && response.ok()) {
        const body = await response.json().catch(() => null); if (body?.board) snapshots.push(body.waitingAsked);
      }
    });
    await expect(page.locator('#arena')).toHaveAttribute('data-ready', 'true'); await page.locator('#seat-choices button').click();
    let casts = 0, intents = 0, captured = false;
    for (let step = 0; step < 40 && casts < 3; step++) {
      const view = await nextQuestion(); expect(view.over).toBe(false);
      if (await planning(view)) continue;
      expect(['Action', 'Intent', 'Target']).toContain(view.waitingFor);
      await expect.poll(() => snapshots.at(-1), { timeout: 10000 }).toBe(view.waitingAsked);
      await page.locator('#combat-open').click();
      let spell = null;
      if (view.waitingFor !== 'Target') {
        const options = view.waitingFor === 'Action' ? view.options.action.spells.map(one => one.spell)
          : view.options.intent.creatures.find(one => one.creature === view.waitingCreature).castableSpells;
        spell = options[0]; expect(spell).toBeTruthy();
        if (!captured) await page.screenshot({ path: info.outputPath(`${declaration}-spells.jpg`), type: 'jpeg', quality: 85 });
        await page.locator(`[data-spell="${spell}"]`).click();
      }
      let targets = [];
      if (view.waitingFor !== 'Intent') {
        const target = view.waitingFor === 'Action' ? view.options.action.spells.find(one => one.spell === spell) : view.options.target;
        targets = target.legalTargets.candidates.slice(0, target.legalTargets.minTargets);
        for (const id of targets) await page.locator(`[data-creature="${id}"]`).click();
        if (!captured) { await page.screenshot({ path: info.outputPath(`${declaration}-targets.jpg`), type: 'jpeg', quality: 85 }); captured = true; }
      }
      const response = page.waitForResponse(response => response.url().endsWith('/decision') && response.request().method() === 'POST');
      await page.locator(view.waitingFor === 'Intent' ? '#combat-declare' : '#combat-confirm').click();
      expect((await response).status()).toBe(204);
      const last = sent.at(-1); expect(last.command.asked).toBe(view.waitingAsked); expect(last.seen).toContain(view.waitingAsked);
      expect(last.command.kind).toBe(view.waitingFor);
      if (view.waitingFor === 'Intent') intents++; else { expect(last.command.targets).toEqual(targets); casts++; }
      await expect.poll(async () => (await read()).waitingAsked).not.toBe(view.waitingAsked);
    }
    expect(casts).toBe(3); expect(intents).toBe(declaration === 'hidden-intent' ? 3 : 0);
    expect(new Set(sent.map(one => one.command.asked)).size).toBe(sent.length);
    const after = await nextQuestion();
    await expect.poll(() => snapshots.at(-1), { timeout: 10000 }).toBe(after.waitingAsked);
    for (const one of [...after.board.allies, ...after.board.enemies]) {
      await expect(page.locator(`[data-creature="${one.id}"] .creature-stats`)).toContainText(`${one.health}/${one.maxHealth} HP`);
    }
  } finally {
    await page.close();
    if (host.exitCode === null) { const closed = new Promise(done => host.once('exit', done)); host.kill('SIGINT'); await closed; }
  }
});
