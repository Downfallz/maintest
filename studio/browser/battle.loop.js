import { test, expect } from '@playwright/test';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { startHost, stopHost } from './host.js';
import { mkdtemp, writeFile, readFile, rm } from 'node:fs/promises';
import { join as pathJoin } from 'node:path';
import { tmpdir } from 'node:os';
import { fitsViewport, fitsBattlefield } from './fit.js';

async function enter(page, join) {
  await page.goto(join); await page.locator('#invitation-take').click();
  await expect(page.locator('#table, #pass').filter({ visible: true }).first()).toBeVisible({ timeout: 20000 });
  const tokens = await page.evaluate(() => JSON.parse(localStorage.getItem('downfall.table.seats')));
  await page.goto(`${new URL(join).origin}/battle`); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#loading')).toBeHidden(); return tokens;
}
const decisionWritten = page => page.waitForResponse(response => response.url().endsWith('/decision') && response.request().method() === 'POST');
async function confirm(page, selector) {
  const response = decisionWritten(page);
  await page.locator(selector).click(); expect((await response).status()).toBe(204);
}
async function answerEvolution(page, view, capture) {
  const offer = view.options.evolution.creatures.find(one => one.availableTiers.length);
  if (!offer) { await page.locator('#planning-pass').click(); return; }
  await page.locator(`#planning-creatures [data-creature="${offer.creature}"]`).click();
  const node = page.locator(`[data-mutation="${offer.availableTiers[0]}"]`).first();
  await node.click();
  if (capture) {
    await expect(page.locator('[data-mutation]')).toHaveCount(offer.availableTiers.length);
    await expect(page.locator('#mutation-tree')).not.toHaveAttribute('open');
    for (const card of await page.locator('.mutation-node[data-status="available"]').all()) {
      await expect(card.locator('.mutation-name')).not.toBeEmpty();
      await expect(card.locator('.mutation-tags')).toBeVisible();
    }
    await expect(page.locator('.mutation-preview')).toBeVisible();
    const later = view.board.roundNumber > 1;
    if (later) await page.locator('#mutation-expand').click();
    await capture(captureSubject(view));
    if (later) await page.locator('#mutation-expand').click();
  }
}
async function answerPlanning(page, view, capture) {
  await page.locator('#planning-open').click();
  if (view.waitingFor === 'Evolution') await answerEvolution(page, view, capture);
  else if (view.waitingFor === 'Speed') {
    const count = await page.locator('[data-tempo-tab]').count();
    for (let index = 0; index < count; index++) await page.locator('[data-speed="Standard"]').click();
    if (capture) {
      await page.locator('[data-tempo-tab]').first().click();
      await page.locator('[data-speed="Quick"]').click();
      await expect(page.locator('[data-tempo-tab]').first()).toContainText('Instinct');
      await page.locator('[data-tempo-tab]').first().click();
      await page.locator('[data-speed="Standard"]').click();
      await expect(page.locator('#planning-confirm')).toHaveText(`Confirm tempos · ${count}/${count}`);
      await capture('team-tempos');
    }
  }
  else await page.locator('#planning-keep').click();
  await confirm(page, '#planning-confirm');
  if (view.waitingFor === 'Speed') await expect(page.locator('#planning-dialog')).toBeHidden({ timeout: 20000 });
}
function castable(view) {
  if (view.waitingFor === 'Action') return view.options.action.spells.map(one => one.spell);
  return view.options.intent.creatures.find(one => one.creature === view.waitingCreature).castableSpells;
}
async function answerCombat(page, view, capture) {
  const kind = view.waitingFor;
  await page.locator('#combat-open').click(); let spell;
  if (capture && kind !== 'Target') await capture('late-spellbook');
  if (kind !== 'Target') { spell = castable(view).find(id => id.includes('heavy_strike')) ?? castable(view)[0]; await page.locator(`[data-spell="${spell}"]`).click(); }
  if (kind !== 'Intent') {
    const legal = (kind === 'Target' ? view.options.target : view.options.action.spells.find(one => one.spell === spell)).legalTargets;
    for (const id of legal.candidates.length < legal.minTargets ? [] : legal.candidates.slice(0, legal.minTargets)) await page.locator(`[data-creature="${id}"]`).click();
  }
  await confirm(page, kind === 'Intent' ? '#combat-declare' : '#combat-confirm');
}
// Auto settles events received under a command dialog; their upkeep remains readable in the
// standalone notice. A round can reach the page in either shape, depending on response timing.
// The pacing suite separately asserts the exact closing-cast/aftermath/upkeep queue ordering.
async function readTurnover(page, round, info) {
  const paced = page.locator('#playback-receipt'), notice = page.locator('#upkeep-dialog');
  await expect(paced.or(notice).filter({ visible: true }).first()).toBeVisible({ timeout: 20000 });
  if (await notice.isVisible()) {
    await expect(page.locator('#upkeep-title')).toHaveText(`Round ${round} · Upkeep`);
    await expect(page.locator('#round-number')).toHaveText(String(round).padStart(2, '0'));
    await expect(page.locator('#upkeep-body')).toContainText('energy');
    await fitsViewport(page, '#upkeep-close');
    await page.screenshot({ path: info.outputPath('paced-upkeep-real-host.jpg'), type: 'jpeg', quality: 85 });
    await page.locator('#upkeep-close').click();
  } else {
    await expect(page.locator('#receipt-title')).toHaveText('Upkeep', { timeout: 20000 });
    await expect(page.locator('#round-number')).toHaveText(String(round).padStart(2, '0'));
    await expect(page.locator('#receipt-sections')).toContainText('energy');
    await expect(page.locator('.creature')).toHaveCount(0);
    await expect(notice).toBeHidden();
    await page.screenshot({ path: info.outputPath('paced-upkeep-real-host.jpg'), type: 'jpeg', quality: 85 });
    await expect(page.locator('#playback-skip')).toBeHidden({ timeout: 20000 });
  }
}
function captureSubject(view) {
  if (view.waitingFor === 'Evolution') return view.board.roundNumber === 1 ? 'mutation-opening' : 'mutation-later';
  if (view.waitingFor === 'Speed') return 'team-tempos';
  return 'late-spellbook';
}
const answer = (page, view, capture) => ['Evolution', 'Speed', 'TieOrder'].includes(view.waitingFor) ? answerPlanning(page, view, capture) : answerCombat(page, view, capture);
// Every write names an asking this page had already acknowledged, and no asking is answered twice.
function expectWritesFenced(writes) {
  for (const write of writes) expect(write.seen).toContain(write.command.asked);
  expect(new Set(writes.map(one => one.command.asked)).size).toBe(writes.length);
}
async function reviewAndReplay(page, latest, info, actions) {
  // The last actions play out under reduced motion; clicking Skip would race a button about to go.
  await expect(page.locator('#playback-skip')).toBeHidden({ timeout: 20000 });
  await page.locator('#history-open').click();
  const recent = [...actions.values()].filter(entry => entry.event.roundId >= latest.board.roundNumber - 1);
  expect(recent.length).toBeGreaterThan(1);
  for (const entry of recent) await expect(page.locator(`#history-list [data-sequence="${entry.sequence}"]`)).toHaveCount(1);
  const last = recent.findLast(entry => entry.event.appliedOutcomes?.some(one => one.kind === 'DamageOutcome' && one.amount > 0)) ?? recent.at(-1), detail = page.locator(`#history-list [data-sequence="${last.sequence}"]`);
  await detail.locator('summary').click(); await expect(detail).toContainText(/Targets:|No targets/);
  await fitsViewport(page, '#history-close');
  await detail.getByRole('button', { name: 'Inspect event' }).click();
  await expect(page.locator('#history-detail')).toContainText('Applied effects');
  await page.locator('#history-before').click(); await expect(page.locator('.history-creature')).toHaveCount(last.event.frame.before.length);
  await page.locator('#history-after').click();
  await fitsViewport(page, '#history-close, #history-latest, #history-before, #history-after');
  await page.screenshot({ path: info.outputPath('history-real-host.jpg'), type: 'jpeg', quality: 85 });
  if (latest.board.roundNumber > 2) {
    const firstLive = Number(await page.locator('#history-list [data-sequence]').first().getAttribute('data-sequence'));
    await page.locator('#history-earlier').click();
    await expect(page.locator('#history-earlier')).not.toHaveText('Loading…');
    await expect(page.locator('#history-load-error')).toBeEmpty();
    await expect.poll(async () => Number(await page.locator('#history-list [data-sequence]').first().getAttribute('data-sequence'))).toBeLessThan(firstLive);
    await page.locator('#history-latest').click();
    await expect(page.locator(`#history-list [data-sequence="${last.sequence}"]`)).toHaveCount(1);
  }
  await page.locator('#history-close').click();
  await page.locator('#round-review').click(); await expect(page.locator('#round-notes')).toContainText('Aftermath'); await page.keyboard.press('Escape');
  await page.locator('#match-open').click(); await expect(page.locator('#match-outcome')).toContainText(latest.outcome.reason === 'RoundCap' ? 'round cap' : 'last team standing');
  let title = 'Draw';
  if (latest.outcome.winner) title = latest.outcome.winner === 'player1' ? 'Victory' : 'Defeat';
  await expect(page.locator('#match-title')).toHaveText(title);
  await Promise.all([latest.board.allies, latest.board.enemies].map(team => expect(page.locator('#match-stats')).toContainText(`${team.filter(one => one.isAlive).length}/${team.length} standing · ${team.reduce((sum, one) => sum + one.health, 0)} HP`)));
  await page.screenshot({ path: info.outputPath('final-result-real-host.jpg'), type: 'jpeg', quality: 85 });
  await page.locator('#match-notes').click();
  await page.getByRole('button', { name: 'I had to look this up', exact: true }).click(); await expect(page.locator('#noted')).toHaveText('Noted: you looked a rule up.');
  await page.locator('#end-comment').fill('AU13 real-host final comment'); await page.locator('#comment-save').click();
  await expect(page.locator('#noted')).toHaveText('Comment saved with the session.'); await page.keyboard.press('Escape');
  await page.locator('#match-replay').click();
  await expect(page.locator('#replay-controls')).toBeVisible(); await page.locator('#replay-round').selectOption('2'); await expect(page.locator('#replay-position')).toContainText('Round 2');
  for (let step = 0; step < 20 && !(await page.locator('#timeline li').count()) && await page.locator('#replay-next').isEnabled(); step++) await page.locator('#replay-next').click();
  await page.locator('#order-open').click(); await expect(page.locator('#order-title')).toHaveText('Round 2 · Turn order');
  const displayed = await page.locator('#timeline [data-order-creature]').evaluateAll(nodes => nodes.map(node => node.dataset.orderCreature));
  expect(await page.locator('#order-list [data-order-creature]').evaluateAll(nodes => nodes.map(node => node.dataset.orderCreature))).toEqual(displayed);
  expect(displayed.length).toBeGreaterThan(0); await page.locator('#order-close').click();
  await fitsBattlefield(page); await fitsViewport(page, '.creature, #replay-next, #replay-previous, #replay-close');
  await page.screenshot({ path: info.outputPath('replay.jpg'), type: 'jpeg', quality: 85 });
  // Every recorded step of the last round is reachable, and an elimination is seen on one of its frames: the
  // final step uses the finished host snapshot, after every cast and cleanup.
  const rounds = await page.locator('#replay-round option').allTextContents(); expect(rounds).toContain(`Round ${latest.board.roundNumber}`);
  await page.locator('#replay-round').selectOption(String(latest.board.roundNumber));
  let defeated = await page.locator('.creature.is-defeated').count();
  for (let step = 0; step < 60 && await page.locator('#replay-next').isEnabled(); step++) {
    await page.locator('#replay-next').click(); defeated += await page.locator('.creature.is-defeated').count();
  }
  await expect(page.locator('#replay-position')).toHaveText(/ (\d+) of \1$/);
  if (latest.outcome.reason === 'Elimination') expect(defeated).toBeGreaterThan(0);
  await expect(page.locator('#replay-note')).toHaveText('Final public battlefield confirmed by the host.');
  await Promise.all([...latest.board.allies, ...latest.board.enemies].map(one => expect(page.locator(`[data-creature="${one.id}"] .creature-stats`)).toContainText(`${one.health}/${one.maxHealth} HP`)));
  await page.locator('#replay-close').click(); await expect(page.locator('#match-dialog')).toBeVisible();
}
// The recorded session endpoint is the same operator-only endpoint used by the table's replay.
async function expectRecordedPerspective(page, context, origin, hostInfo) {
  const adminToken = new URL(hostInfo.admin).searchParams.get('token');
  await page.evaluate(token => localStorage.setItem('downfall.table.operator', token), adminToken);
  const recorded = await context.request.get(`${origin}/api/sessions/${hostInfo.session}/replay`, { headers: { 'X-Seat-Token': adminToken } }); expect(recorded.ok()).toBe(true);
  await page.goto(`${origin}/battle?replay=${hostInfo.session}&seat=player2`); await page.locator('#match-open').click(); await page.locator('#match-replay').click();
  await expect(page.locator('#replay-controls')).toBeVisible(); await expect(page.locator('#perspective')).toContainText('PLAYER 2');
  const board = (await recorded.json()).boards.player2;
  for (const one of board.allies) await expect(page.locator(`[data-creature="${one.id}"]`)).toHaveClass(/ally/);
}

for (const declaration of ['at-activation', 'hidden-intent']) test(`${declaration}: a complete 3v3 battle match and its recorded session`, async ({ page, context }, info) => {
  test.setTimeout(240000);
  const directory = await mkdtemp(pathJoin(tmpdir(), 'battle-loop-'));
  const rules = pathJoin(directory, 'rules.json'); await writeFile(rules, JSON.stringify({ roundCap: declaration === 'at-activation' ? 12 : 2, combatDeclaration: declaration }));
  const hostInfo = await startHost(declaration, ['--rules', rules, '--record', pathJoin(directory, 'sessions')]);
  const errors = [], writes = [], shown = [], actions = new Map(); let latest;
  page.on('pageerror', error => errors.push(error.message));
  page.on('request', request => {
    if (!new URL(page.url()).pathname.startsWith('/battle')) return; // Invitation acceptance lives on /play.
    const url = new URL(request.url());
    if (url.searchParams.has('shown')) shown.push(Number(url.searchParams.get('shown')));
    if (url.pathname.endsWith('/decision')) writes.push({ command: request.postDataJSON(), seen: [...shown] });
  });
  page.on('response', async response => {
    if (!/\/api\/seat\/player1(?:\?|$)/.test(response.url()) || response.url().includes('shown=')) return;
    const value = await response.json().catch(() => null);
    if (!value?.board) return;
    latest = value;
    for (const entry of value.feed ?? []) if (entry.event.kind === 'CombatActionResolved') actions.set(entry.sequence, entry);
  });
  try {
    const tokens = await enter(page, hostInfo.join); const origin = new URL(hostInfo.join).origin;
    await expect(page.locator('#upkeep-dialog')).toBeVisible();
    await expect(page.locator('#upkeep-title')).toHaveText('Round 1 · Upkeep');
    await expect(page.locator('#upkeep-body')).toBeHidden();
    await expect(page.locator('#upkeep-intro')).toHaveText('+2 energy each · 6 creatures');
    await expect(page.locator('#upkeep-body .upkeep-rows li')).toHaveCount(6);
    await expect(page.locator('#upkeep-body')).toContainText('+2 energy · 0 → 2');
    expect(shown).toEqual([]); expect(writes).toEqual([]);
    await fitsViewport(page, '#upkeep-close');
    await page.screenshot({ path: info.outputPath('upkeep-real-host.jpg'), type: 'jpeg', quality: 85 });
    await page.locator('#upkeep-close').click();
    let upkeepRound = 1;
    await expect(page.locator('#help-dialog')).toBeVisible();
    await expect(page.locator('#help-title')).toHaveText('What is a mutation?');
    await expect(page.locator('#help-objective')).toContainText('team picks');
    await expect(page.locator('#help-flow li').nth(2)).toContainText(declaration === 'hidden-intent' ? 'declare hidden spells' : 'when your creature acts');
    expect(shown).toEqual([]); expect(writes).toEqual([]);
    await page.screenshot({ path: info.outputPath('guidance-real-host.jpg'), type: 'jpeg', quality: 85 });
    await page.locator('#help-close').click();
    const taught = new Set(['Evolution']);
    // A whole match with sound on: the opt-in is a setting, never a decision (ADR 0134, ADR 0136).
    await page.locator('#settings').click(); await page.locator('#reduce-motion').check();
    await expect(page.locator('#sound')).not.toBeChecked(); await page.locator('#sound').check();
    await expect(page.locator('#sound')).toBeChecked();
    await page.screenshot({ path: info.outputPath('release-settings.jpg'), type: 'jpeg', quality: 85 });
    await page.keyboard.press('Escape'); expect(writes).toEqual([]);
    const captured = new Set(), orderRounds = new Set(); let spellbookRound = 0;
    for (let step = 0; step < 200 && !latest?.over; step++) {
      await expect.poll(() => latest?.over || Boolean(latest?.waitingFor), { timeout: 15000 }).toBe(true);
      const view = structuredClone(latest); if (!view || view.over) break;
      if (view.board.roundNumber > upkeepRound) {
        await readTurnover(page, view.board.roundNumber, info);
        upkeepRound = view.board.roundNumber;
      }
      if (view.board.timeline.length && view.board.subPhase !== 'TieOrder' && !orderRounds.has(view.board.roundNumber)) {
        const beforeShown = [...shown], beforeWrites = writes.length;
        await expect(page.locator('#order-dialog')).toBeVisible({ timeout: 20000 });
        await expect(page.locator('#order-list [data-order-creature]')).toHaveText(view.board.timeline.map(slot => {
          const creatures = [...view.board.allies, ...view.board.enemies], one = creatures.find(one => one.id === slot.creature);
          return creatures.filter(other => other.name === one.name).length > 1 ? `${one.name} · ${one.id}` : one.name;
        }));
        for (const roll of view.board.rollOffs) await expect(page.locator('#order-list')).toContainText(`d20 ${roll.rolls.join(' → ')}`);
        expect(shown).toEqual(beforeShown); expect(writes.length).toBe(beforeWrites);
        if (!orderRounds.size) await page.screenshot({ path: info.outputPath('order-real-host.jpg'), type: 'jpeg', quality: 85 });
        await page.locator('#order-close').click(); orderRounds.add(view.board.roundNumber);
      }
      if (!taught.has(view.waitingFor)) {
        await expect(page.locator('#help-dialog')).toBeVisible({ timeout: 20000 });
        await expect(page.locator('#help-objective')).not.toBeEmpty();
        await page.locator('#help-close').click(); taught.add(view.waitingFor);
      }
      const subject = captureSubject(view);
      const captureNow = subject === 'late-spellbook'
        ? view.board.roundNumber >= 3 && view.board.roundNumber > spellbookRound
        : !captured.has(subject);
      await answer(page, view, captureNow ? async name => {
        await fitsViewport(page, '#planning-confirm, #planning-close, #combat-dialog-close');
        await page.screenshot({ path: info.outputPath(`${name}.jpg`), type: 'jpeg', quality: 85 }); captured.add(name);
        if (name === 'late-spellbook') spellbookRound = view.board.roundNumber;
      } : null);
      await expect.poll(() => latest?.waitingAsked, { timeout: 10000 }).not.toBe(view.waitingAsked);
    }
    expect(latest.over).toBe(true);
    if (declaration === 'hidden-intent') { expect(latest.outcome.reason).toBe('RoundCap'); expect(latest.board.roundNumber).toBe(2); }
    else {
      expect(latest.board.roundNumber).toBeGreaterThan(2);
      expect([...captured]).toEqual(expect.arrayContaining(['mutation-opening', 'team-tempos', 'late-spellbook']));
      expect([...latest.board.allies, ...latest.board.enemies].some(one => !one.isAlive)).toBe(true);
      expect(writes.filter(one => one.command.kind === 'Evolution' && one.command.tier).length).toBeGreaterThan(2);
    }
    expect(taught.has('Speed')).toBe(true);
    expect(taught.has(declaration === 'at-activation' ? 'Action' : 'Intent')).toBe(true);
    if (declaration === 'hidden-intent') expect(taught.has('Target')).toBe(true);
    await reviewAndReplay(page, latest, info, actions);
    const savedNotes = await readFile(pathJoin(directory, 'sessions', hostInfo.session, 'notes.jsonl'), 'utf8');
    expect(savedNotes).toContain('AU13 real-host final comment');
    expectWritesFenced(writes);
    expect(writes.some(one => one.command.kind === 'Evolution' && one.command.tier)).toBe(true);
    expect(writes.filter(one => one.command.kind === 'Speed').length).toBeGreaterThanOrEqual(3);
    await expectRecordedPerspective(page, context, origin, hostInfo);
    expect(tokens.player1).toBeTruthy(); expect(errors).toEqual([]);
  } finally { await page.close(); await stopHost(hostInfo.host); await rm(directory, { recursive: true, force: true }); }
});

test('two battle seats plan simultaneously without revealing the other team choices', async ({ page, context, browser }, info) => {
  test.setTimeout(120000);
  await returningBattlePlayer({ context });
  const hostInfo = await startHost('at-activation', [], { twoPeople: true });
  const otherContext = await browser.newContext({ viewport: page.viewportSize() }); const other = await otherContext.newPage();
  await returningBattlePlayer({ context: otherContext });
  const errors = []; for (const surface of [page, other]) surface.on('pageerror', error => errors.push(error.message));
  try {
    const firstTokens = await enter(page, hostInfo.joins[0]), secondTokens = await enter(other, hostInfo.joins[1]);
    const origin = new URL(hostInfo.join).origin;
    const read = async (surface, seat, token) => (await surface.request.get(`${origin}/api/seat/${seat}`, { headers: { 'X-Seat-Token': token } })).json();
    let first, second;
    await expect.poll(async () => { first = await read(page, 'player1', firstTokens.player1); second = await read(other, 'player2', secondTokens.player2); return [first.waitingFor, second.waitingFor]; }).toEqual(['Evolution', 'Evolution']);
    for (const surface of [page, other]) {
      await expect(surface.locator('#upkeep-dialog')).toBeVisible();
      await surface.locator('#upkeep-close').click();
    }
    await answer(page, first);
    // Nothing of a private pick reaches the other seat: not its board, its offer, its feed or its question.
    const concealed = await read(other, 'player2', secondTokens.player2);
    expect(concealed.waitingAsked).toBe(second.waitingAsked); expect(concealed.options).toEqual(second.options);
    expect(concealed.board).toEqual(second.board); expect(concealed.feed).toEqual(second.feed);
    // Each seat finishes its own picks; the opponent's untouched asking never becomes acknowledged by this page.
    for (const [surface, seat, token] of [[page, 'player1', firstTokens.player1], [other, 'player2', secondTokens.player2]]) {
      for (let pick = 0; pick < 3; pick++) {
        const view = await read(surface, seat, token); if (view.waitingFor !== 'Evolution') break;
        await surface.locator('#planning-open').click(); await surface.locator('#planning-pass').click(); await confirm(surface, '#planning-confirm');
        await expect.poll(async () => (await read(surface, seat, token)).waitingAsked).not.toBe(view.waitingAsked);
      }
    }
    await expect.poll(async () => { first = await read(page, 'player1', firstTokens.player1); second = await read(other, 'player2', secondTokens.player2); return [first.waitingFor, second.waitingFor]; }, { timeout: 15000 }).toEqual(['Speed', 'Speed']);
    const tempos = [];
    page.on('request', request => { if (request.url().endsWith('/decision') && request.postDataJSON()?.kind === 'Speed') tempos.push(request.postDataJSON()); });
    // Interrupt after the first accepted creature, then reload. The host's own private receipts survive;
    // neither the draft nor the rest of the old confirmed batch resumes by itself.
    await page.route('**/decision', route => route.request().postDataJSON()?.kind === 'Speed' && tempos.length === 2 ? route.abort('failed') : route.continue());
    await page.locator('#planning-open').click();
    for (let index = 0; index < 3; index++) await page.locator('[data-speed="Standard"]').click();
    expect(tempos).toEqual([]);
    await page.screenshot({ path: info.outputPath('au5-real-team-tempos.jpg'), type: 'jpeg', quality: 85 });
    await confirm(page, '#planning-confirm');
    await expect(page.locator('#planning-dialog-error')).toContainText('may have arrived');
    const partial = await read(page, 'player1', firstTokens.player1); expect(partial.board.speedChoices).toHaveLength(1);
    const privateSurface = value => ({ board: value.board, options: value.options, feed: value.feed,
      waitingAsked: value.waitingAsked, waitingFor: value.waitingFor, waitingCreature: value.waitingCreature });
    expect(privateSurface(await read(other, 'player2', secondTokens.player2))).toEqual(privateSurface(second));
    await page.unroute('**/decision');
    await page.reload(); await expect(page.locator('#loading')).toBeHidden(); await page.locator('#planning-open').click();
    await expect(page.locator('[data-tempo-tab]')).toHaveCount(2); await expect(page.locator('#planning-confirm')).toBeDisabled();
    expect(tempos).toHaveLength(2);
    await page.locator('[data-speed="Standard"]').click(); await page.locator('[data-speed="Quick"]').click();
    await confirm(page, '#planning-confirm'); await expect(page.locator('#planning-dialog')).toBeHidden();
    expect(tempos).toHaveLength(4); expect(tempos.filter(one => one.creature === first.waitingCreature)).toHaveLength(1);
    expect(privateSurface(await read(other, 'player2', secondTokens.player2))).toEqual(privateSurface(second));
    await page.locator('#settings').click(); await page.locator('#concede-open').click(); await page.locator('#concede-confirm').click();
    await other.locator('#match-open').click(); await expect(other.locator('#match-outcome')).toHaveText('Player 1 conceded · you win.');
    await other.screenshot({ path: info.outputPath('concession-result.jpg'), type: 'jpeg', quality: 85 }); expect(errors).toEqual([]);
  } finally { await otherContext.close(); await page.close(); await stopHost(hostInfo.host); }
});
