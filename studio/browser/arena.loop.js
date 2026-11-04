import { test, expect } from '@playwright/test';
import { startHost, stopHost } from './host.js';
import { mkdtemp, writeFile, rm } from 'node:fs/promises';
import { join as pathJoin } from 'node:path';
import { tmpdir } from 'node:os';
import { fitsViewport, fitsBattlefield } from './fit.js';

async function enter(page, join) {
  await page.goto(join); await page.locator('#invitation-take').click();
  await expect(page.locator('#table, #pass').filter({ visible: true }).first()).toBeVisible({ timeout: 20000 });
  const tokens = await page.evaluate(() => JSON.parse(localStorage.getItem('downfall.table.seats')));
  await page.goto(`${new URL(join).origin}/arena`); await expect(page.locator('#arena')).toHaveAttribute('data-ready', 'true');
  await page.locator('#seat-choices button').click(); await expect(page.locator('#loading')).toBeHidden(); return tokens;
}
async function answer(page, view, capture) {
  const kind = view.waitingFor;
  if (['Evolution', 'Speed', 'TieOrder'].includes(kind)) {
    await page.locator('#planning-open').click();
    if (kind === 'Evolution') {
      const offer = view.options.evolution.creatures.find(one => one.availableTiers.length);
      if (offer) {
        await page.locator('#planning-creatures button').filter({ hasText: new RegExp(` · ${offer.creature}$`) }).click();
        const tier = offer.availableTiers[0];
        // Each branch is an actual details element; open ancestors through their visible summaries.
        const node = page.locator(`[data-mutation="${tier}"]`).first();
        const ancestors = node.locator('xpath=ancestor::details');
        for (let index = 0; index < await ancestors.count(); index++) {
          const ancestor = ancestors.nth(index);
          if (await ancestor.getAttribute('open') === null) await ancestor.locator(':scope > summary').click();
        }
        await node.click(); if (capture) await capture('mutation-tree');
      } else await page.locator('#planning-pass').click();
    } else if (kind === 'Speed') await page.locator('[data-speed="Standard"]').click();
    else await page.locator('#planning-keep').click();
    const response = page.waitForResponse(response => response.url().endsWith('/decision') && response.request().method() === 'POST');
    await page.locator('#planning-confirm').click(); expect((await response).status()).toBe(204); return;
  }
  await page.locator('#combat-open').click(); let spell;
  if (kind !== 'Target') {
    const spells = kind === 'Action' ? view.options.action.spells.map(one => one.spell) : view.options.intent.creatures.find(one => one.creature === view.waitingCreature).castableSpells;
    spell = spells.find(id => id.includes('heavy_strike')) ?? spells[0]; await page.locator(`[data-spell="${spell}"]`).click();
  }
  if (kind !== 'Intent') {
    const legal = (kind === 'Target' ? view.options.target : view.options.action.spells.find(one => one.spell === spell)).legalTargets;
    for (const id of legal.candidates.length < legal.minTargets ? [] : legal.candidates.slice(0, legal.minTargets)) await page.locator(`[data-creature="${id}"]`).click();
  }
  const response = page.waitForResponse(response => response.url().endsWith('/decision') && response.request().method() === 'POST');
  await page.locator(kind === 'Intent' ? '#combat-declare' : '#combat-confirm').click(); expect((await response).status()).toBe(204);
}

for (const declaration of ['at-activation', 'hidden-intent']) test(`${declaration}: a complete 3v3 arena match and its recorded session`, async ({ page, context }, info) => {
  test.setTimeout(240000);
  const directory = await mkdtemp(pathJoin(tmpdir(), 'arena-loop-'));
  const rules = pathJoin(directory, 'rules.json'); await writeFile(rules, JSON.stringify({ roundCap: declaration === 'at-activation' ? 12 : 2, combatDeclaration: declaration }));
  const hostInfo = await startHost(declaration, ['--rules', rules, '--record', pathJoin(directory, 'sessions')]);
  const errors = [], writes = [], shown = []; let latest;
  page.on('pageerror', error => errors.push(error.message));
  page.on('request', request => { const url = new URL(request.url()); if (url.searchParams.has('shown')) shown.push(Number(url.searchParams.get('shown'))); if (url.pathname.endsWith('/decision')) writes.push({ command: request.postDataJSON(), seen: [...shown] }); });
  page.on('response', async response => { if (/\/api\/seat\/player1(?:\?|$)/.test(response.url()) && !response.url().includes('shown=')) { const value = await response.json().catch(() => null); if (value?.board) latest = value; } });
  try {
    const tokens = await enter(page, hostInfo.join); const origin = new URL(hostInfo.join).origin;
    await page.locator('#settings').click(); await page.locator('#reduce-motion').check(); await page.keyboard.press('Escape');
    let captured = false;
    for (let step = 0; step < 200; step++) {
      await expect.poll(() => latest?.over || Boolean(latest?.waitingFor), { timeout: 15000 }).toBe(true);
      if (latest.over) break;
      const view = structuredClone(latest), asked = view.waitingAsked;
      await answer(page, view, !captured && view.waitingFor === 'Evolution' ? async name => {
        await fitsViewport(page, '#planning-confirm, #planning-close'); await page.screenshot({ path: info.outputPath(`${name}.jpg`), type: 'jpeg', quality: 85 }); captured = true;
      } : null);
      await expect.poll(() => latest?.waitingAsked, { timeout: 10000 }).not.toBe(asked);
    }
    expect(latest.over).toBe(true);
    if (declaration === 'hidden-intent') { expect(latest.outcome.reason).toBe('RoundCap'); expect(latest.board.roundNumber).toBe(2); }
    else {
      expect(latest.board.roundNumber).toBeGreaterThan(2);
      expect([...latest.board.allies, ...latest.board.enemies].some(one => !one.isAlive)).toBe(true);
      expect(writes.filter(one => one.command.kind === 'Evolution' && one.command.tier).length).toBeGreaterThan(2);
    }
    if (await page.locator('#playback-skip').isVisible()) await page.locator('#playback-skip').click();
    await page.locator('#round-review').click(); await expect(page.locator('#round-notes')).toContainText('Aftermath'); await page.keyboard.press('Escape');
    await page.locator('#match-open').click(); await expect(page.locator('#match-outcome')).toContainText(latest.outcome.reason === 'RoundCap' ? 'round cap' : 'last team standing'); await page.locator('#match-replay').click();
    await expect(page.locator('#replay-controls')).toBeVisible(); await page.locator('#replay-round').selectOption('2'); await expect(page.locator('#replay-position')).toContainText('Round 2');
    await fitsBattlefield(page); await fitsViewport(page, '.creature, #replay-next, #replay-previous, #replay-close');
    await page.screenshot({ path: info.outputPath('replay.jpg'), type: 'jpeg', quality: 85 });
    for (const write of writes) expect(write.seen).toContain(write.command.asked);
    expect(new Set(writes.map(one => one.command.asked)).size).toBe(writes.length);
    expect(writes.some(one => one.command.kind === 'Evolution' && one.command.tier)).toBe(true);
    expect(writes.filter(one => one.command.kind === 'Speed').length).toBeGreaterThanOrEqual(3);
    // The recorded session endpoint is the same operator-only endpoint used by the table's replay.
    const adminToken = new URL(hostInfo.admin).searchParams.get('token');
    await page.evaluate(token => localStorage.setItem('downfall.table.operator', token), adminToken);
    const recorded = await context.request.get(`${origin}/api/sessions/${hostInfo.session}/replay`, { headers: { 'X-Seat-Token': adminToken } }); expect(recorded.ok()).toBe(true);
    await page.goto(`${origin}/arena?replay=${hostInfo.session}&seat=player2`); await page.locator('#match-open').click(); await page.locator('#match-replay').click();
    await expect(page.locator('#replay-controls')).toBeVisible(); await expect(page.locator('#perspective')).toContainText('PLAYER 2');
    const board = (await recorded.json()).boards.player2;
    for (const one of board.allies) await expect(page.locator(`[data-creature="${one.id}"]`)).toHaveClass(/ally/);
    expect(tokens.player1).toBeTruthy(); expect(errors).toEqual([]);
  } finally { await page.close(); await stopHost(hostInfo.host); await rm(directory, { recursive: true, force: true }); }
});

test('two arena seats plan simultaneously without revealing the other team choices', async ({ page, browser }, info) => {
  test.setTimeout(120000);
  const hostInfo = await startHost('at-activation', [], { twoPeople: true });
  const otherContext = await browser.newContext({ viewport: page.viewportSize() }); const other = await otherContext.newPage();
  const errors = []; for (const surface of [page, other]) surface.on('pageerror', error => errors.push(error.message));
  try {
    const firstTokens = await enter(page, hostInfo.joins[0]), secondTokens = await enter(other, hostInfo.joins[1]);
    const origin = new URL(hostInfo.join).origin;
    const read = async (surface, seat, token) => (await surface.request.get(`${origin}/api/seat/${seat}`, { headers: { 'X-Seat-Token': token } })).json();
    let first, second;
    await expect.poll(async () => { first = await read(page, 'player1', firstTokens.player1); second = await read(other, 'player2', secondTokens.player2); return [first.waitingFor, second.waitingFor]; }).toEqual(['Evolution', 'Evolution']);
    await answer(page, first);
    const concealed = await read(other, 'player2', secondTokens.player2);
    expect(concealed.waitingAsked).toBe(second.waitingAsked); expect(concealed.board.evolutionChoices).toEqual([]);
    // Each seat finishes its own picks; the opponent's untouched asking never becomes acknowledged by this page.
    for (const [surface, seat, token] of [[page, 'player1', firstTokens.player1], [other, 'player2', secondTokens.player2]]) {
      for (let pick = 0; pick < 3; pick++) {
        const view = await read(surface, seat, token); if (view.waitingFor !== 'Evolution') break;
        await surface.locator('#planning-open').click(); await surface.locator('#planning-pass').click();
        const response = surface.waitForResponse(response => response.url().endsWith('/decision') && response.request().method() === 'POST');
        await surface.locator('#planning-confirm').click(); expect((await response).status()).toBe(204);
        await expect.poll(async () => (await read(surface, seat, token)).waitingAsked).not.toBe(view.waitingAsked);
      }
    }
    await expect.poll(async () => { first = await read(page, 'player1', firstTokens.player1); second = await read(other, 'player2', secondTokens.player2); return [first.waitingFor, second.waitingFor]; }, { timeout: 15000 }).toEqual(['Speed', 'Speed']);
    await answer(page, first); expect((await read(other, 'player2', secondTokens.player2)).board.speedChoices).toEqual([]);
    await page.locator('#settings').click(); await page.locator('#concede-open').click(); await page.locator('#concede-confirm').click();
    await other.locator('#match-open').click(); await expect(other.locator('#match-outcome')).toHaveText('You win · concession.');
    await other.screenshot({ path: info.outputPath('concession-result.jpg'), type: 'jpeg', quality: 85 }); expect(errors).toEqual([]);
  } finally { await otherContext.close(); await page.close(); await stopHost(hostInfo.host); }
});
