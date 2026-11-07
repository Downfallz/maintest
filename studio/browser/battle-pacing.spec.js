import { test, expect } from '@playwright/test';
import { mount, entry, before } from './battle-playback-fixture.js';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { WINDUP_MS, IMPACT_MS, SETTLE_MS } from '../../table/battle-timing.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningBattlePlayer);
// Full-size software-rendered frames plus paused-clock playback can exceed 30 s on a busy runner.
test.setTimeout(60000);
const cleanup = { sequence: 11, event: { kind: 'ConditionsExpired', roundId: 2, expired: { 1: [{ effect: { kind: 'DefenseBuff', amount: 2 } }] } } };
const energy = { sequence: 12, event: { kind: 'RoundEnergyGranted', roundId: 3, grants: before.map(one => ({
  creature: one.id, gained: 2, before: 5, after: 7, baseRequested: 2, passiveRequested: 0 })) } };
const ongoing = { sequence: 13, event: { kind: 'OngoingEffectsApplied', roundId: 3, bleedTicks: [{ creature: 4, damage: 2 }] } };
const nextRound = h => ({ ...h.state.view.board, roundNumber: 3, phase: 'Planning', subPhase: 'Evolution', timeline: [],
  allies: before.slice(0, 3).map(one => ({ ...one, energy: 7, knownSpells: ['strike', 'future'] })) });
// The opponent's cast: it waits for its OK in either pace, where the player's own moves on by itself (ADR 0141).
const theirs = sequence => entry(sequence, { actor: 4, targets: [1] });
const step = async page => { await page.locator('#settings').click(); await page.selectOption('#playback-preference', 'step'); await page.getByRole('button', { name: 'Close settings' }).click(); };
const fits = page => fitsViewport(page, '#playback-next, #playback-skip, #combat-open, #planning-open, #settings, #playback-result');

// SSE delivery starts the presentation clock. Wait for its sequence before advancing fake time.
async function finishCast(page, sequence = 10) {
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', String(sequence));
  await page.clock.runFor(WINDUP_MS + IMPACT_MS);
}

test('Step reads the closing attack, aftermath and upkeep before showing the next live objective', async ({ page }, info) => {
  const h = await mount(page); await step(page);
  await h.publish([theirs(10), cleanup, energy, ongoing], { board: nextRound(h), waitingFor: 'Evolution', waitingAsked: 6,
    options: { evolution: { remaining: 2, choices: [] } } });
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '10');
  await expect(page.locator('#round-number')).toHaveText('02');
  await expect(page.locator('#round-phase')).toHaveText('COMBAT');
  await finishCast(page);
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '10');
  await expect(page.locator('[data-creature="1"] .creature-stats')).toContainText('5 EN');
  await expect(page.locator('#planning-help')).toBeEmpty();
  await page.locator('[data-creature="1"]').click();
  await expect(page.locator('#inspect-spells')).not.toContainText('future');
  await page.getByRole('button', { name: 'Close inspection' }).click(); await fits(page);
  await page.locator('#playback-next').click();
  // The aftermath reads on the field (ADR 0143): the round's last frame, a plaque on what ended, one line in the card.
  await expect(page.locator('#round-phase')).toHaveText('AFTERMATH');
  await expect(page.locator('#reading-title')).toHaveText('Aftermath'); await expect(page.locator('#reading-detail')).toHaveText('1 condition ended');
  await expect(page.locator('#result-text')).toContainText('DefenseBuff ends');
  await expect(page.locator('#actor-layer .creature')).toHaveCount(6);
  await page.locator('#playback-next').click();
  await expect(page.locator('#round-number')).toHaveText('03'); await expect(page.locator('#round-phase')).toHaveText('UPKEEP');
  await expect(page.locator('#reading-title')).toHaveText('Upkeep'); await expect(page.locator('#reading-detail')).toContainText('+2 energy');
  await expect(page.locator('#result-text')).toContainText('Ongoing damage −2 HP');
  await expect(page.locator('#upkeep-dialog')).toBeHidden();
  // The upkeep reads on its own round's board, creatures included; the dock holds only the reading card.
  await expect(page.locator('#actor-layer [data-creature="1"]')).toBeVisible(); await expect(page.locator('#actor-name')).toBeHidden();
  await expect(page.locator('#reading-kind')).toContainText('Round summary');
  await page.clock.fastForward(5000); await expect(page.locator('#playback-pacing')).toBeVisible(); await fits(page);
  // The card opens the full receipt.
  await page.locator('#reading-open').click(); await expect(page.locator('#result-dialog')).toBeVisible();
  await expect(page.locator('#result-text')).toContainText('Ongoing damage'); await page.keyboard.press('Escape');
  await page.clock.runFor(300);
  await page.screenshot({ path: info.outputPath('paced-upkeep.jpg'), type: 'jpeg', quality: 85 });
  // Nothing of the new asking is acknowledged while the turnover is still being read.
  expect(h.state.requests.filter(one => one.query.includes('shown='))).toEqual([]);
  await page.locator('#playback-next').click();
  await expect(page.locator('#round-phase')).toHaveText('COMMAND'); await expect(page.locator('#upkeep-dialog')).toBeHidden();
  await expect(page.locator('[data-creature="1"] .creature-stats')).toContainText('7 EN');
  // Once the reading ends, the mutation phase opens its tree by itself and acknowledges the asking (ADR 0138).
  await expect(page.locator('#planning-dialog')).toBeVisible();
  await expect.poll(() => h.state.requests.filter(one => one.query.includes('shown=')).map(one => new URLSearchParams(one.query).get('shown'))).toEqual(['6']);
  expect(h.state.requests.filter(one => one.method !== 'GET' && one.path !== '/api/lobby/resume')).toEqual([]); expect(h.errors).toEqual([]);
});

test('Auto continues held results; an incoming one offers OK, not Skip, and returns the live command without acknowledging it', async ({ page }) => {
  const h = await mount(page); await step(page); await h.publish([entry(10), entry(11, { actor: 4 })]);
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '10');
  await finishCast(page); await page.locator('#settings').click(); await page.selectOption('#playback-preference', 'auto'); await page.getByRole('button', { name: 'Close settings' }).click(); await page.clock.runFor(SETTLE_MS);
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '11');
  await expect(page.locator('#playback-skip')).toBeHidden(); await finishCast(page, 11);
  await page.locator('#playback-next').click(); await expect(page.locator('#playback-next')).toBeHidden();
  await expect(page.locator('#combat-open')).toBeEnabled();
  await h.publish([entry(10), entry(11, { actor: 4 })]); await expect(page.locator('#playback-skip')).toBeHidden();
  expect(h.state.requests.filter(one => one.query.includes('shown='))).toEqual([]);
  expect(h.state.requests.filter(one => one.method !== 'GET' && one.path !== '/api/lobby/resume')).toEqual([]); expect(h.errors).toEqual([]);
});

test('the next choice waits for the reading, and skipping it passes unread receipts without a second upkeep modal', async ({ page }) => {
  const h = await mount(page); await step(page); await h.publish([entry(10), energy, ongoing], { board: nextRound(h) });
  await expect(page.locator('#battle')).toHaveAttribute('data-reading', 'true'); await expect(page.locator('#combat-open')).toBeHidden();
  await page.locator('#playback-skip').click(); await page.locator('#combat-open').click();
  await expect(page.locator('#combat-dialog')).toBeVisible();
  await expect(page.locator('#combat-context')).toContainText('7 energy');
  await page.getByRole('button', { name: 'Close spell choices' }).click(); await h.publish([]);
  await expect(page.locator('#connection')).toHaveText('LIVE'); await page.clock.runFor(500);
  await expect(page.locator('#upkeep-dialog')).toBeHidden(); expect(h.errors).toEqual([]);
});

const rotate = page => { const { width, height } = page.viewportSize(); return page.setViewportSize({ width: height, height: width }); };
test('rotation and reduced motion invalidate Step callbacks and keep the current board', async ({ page }) => {
  const h = await mount(page); await step(page); await h.publish([entry(10), cleanup, energy, ongoing]);
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '10');
  await finishCast(page); await rotate(page);
  await expect(page.locator('#playback-skip')).toBeHidden(); await page.clock.fastForward(5000); await fits(page);
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '');
  await expect(page.locator('#round-number')).toHaveText('02'); await expect(page.locator('#round-phase')).toHaveText('COMBAT');
  await h.publish([entry(14)]); await expect(page.locator('#battle')).toHaveAttribute('data-reading', 'true');
  await page.locator('#settings').click(); await page.locator('#reduce-motion').check();
  await page.getByRole('button', { name: 'Close settings' }).click();
  await expect(page.locator('#battle')).toHaveAttribute('data-reading', 'false'); expect(h.errors).toEqual([]);
});

test('seat revocation clears a held receipt and cannot resurrect it', async ({ page }) => {
  const h = await mount(page); await step(page); await h.publish([energy, ongoing]);
  await expect(page.locator('#playback-pacing')).toBeVisible(); h.state.status = 403;
  await page.evaluate(() => window.notifyBattle()); await expect(page.locator('#playback-pacing')).toBeHidden();
  await expect(page.locator('#reading-title')).toBeEmpty(); await page.clock.fastForward(8000);
  await expect(page.locator('#playback-pacing')).toBeHidden(); expect(h.errors).toEqual([]);
});

test('the last result after lethal upkeep is read even when there is no next command', async ({ page }) => {
  const h = await mount(page); await step(page);
  await h.publish([energy, ongoing, { sequence: 14, event: { kind: 'MatchEnded', roundId: 3, outcome: { winner: 'Player1', reason: 'Elimination' } } }],
    { board: nextRound(h), waitingFor: null, over: true, outcome: { winner: 'player1', reason: 'Elimination' } });
  await expect(page.locator('#result-text')).toContainText('Ongoing damage');
  // The match's end is one moment: once the last result is read, the result opens by itself, with no step of its own.
  await expect(page.locator('#playback-next')).toHaveText('OK'); await page.locator('#playback-next').click();
  await expect(page.locator('#match-dialog')).toBeVisible(); await expect(page.locator('#match-title')).toHaveText('Victory');
  await expect(page.locator('#match-dialog .eyebrow')).toHaveText('MATCH COMPLETE');
  await page.keyboard.press('Escape'); await expect(page.locator('#match-open')).toBeVisible();
  await page.clock.runFor(500); await expect(page.locator('#match-dialog')).toBeHidden(); expect(h.errors).toEqual([]);
});

test('rotation passes over a queued upkeep without hiding it: the upkeep notice still opens', async ({ page }) => {
  const h = await mount(page); await step(page); await h.publish([theirs(10), energy, ongoing], { board: nextRound(h) });
  await finishCast(page); await expect(page.locator('#playback-next')).toBeEnabled(); await rotate(page);
  await expect(page.locator('#playback-skip')).toBeHidden();
  await expect(page.locator('#upkeep-dialog')).toBeVisible(); await expect(page.locator('#upkeep-title')).toHaveText('Round 3 · Upkeep');
  expect(h.state.requests.filter(one => one.method !== 'GET' && one.path !== '/api/lobby/resume')).toEqual([]); expect(h.errors).toEqual([]);
});

test('a resize that keeps the orientation keeps the held result', async ({ page }) => {
  const h = await mount(page); await step(page); await h.publish([theirs(10), entry(11, { actor: 4 })]);
  await finishCast(page); const { width, height } = page.viewportSize();
  await page.setViewportSize({ width: width - 20, height: height - 60 });
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '10'); await expect(page.locator('#playback-next')).toBeEnabled();
  expect(h.errors).toEqual([]);
});

test('an inspection opened on a held action survives the next arrival', async ({ page }) => {
  const h = await mount(page); await step(page); await h.publish([theirs(10)]);
  await finishCast(page); await page.locator('[data-creature="1"]').click();
  await expect(page.locator('#inspect-dialog')).toBeVisible();
  await h.publish([theirs(10), entry(11, { actor: 4 })]); await expect(page.locator('#playback-next')).toHaveText('Next');
  await page.getByRole('button', { name: 'Close inspection' }).click();
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '10'); await expect(page.locator('#playback-next')).toBeEnabled();
  await expect(page.locator('#playback-next')).toHaveText('Next'); expect(h.errors).toEqual([]);
});

test('a receipt arriving under an open inspection leaves it open and takes the focus back on close', async ({ page }) => {
  const h = await mount(page); await h.publish([entry(10), cleanup]);
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '10'); await page.locator('[data-creature="1"]').click();
  await expect(page.locator('#inspect-dialog')).toBeVisible(); await finishCast(page); await page.clock.runFor(SETTLE_MS);
  await expect(page.locator('#reading-title')).toHaveText('Aftermath'); await expect(page.locator('#inspect-dialog')).toBeVisible();
  await expect(page.locator('#inspect-name')).toHaveText(/\S/);
  // The aftermath reads on the round's last frame, so the creature inspected is still there to take the focus.
  await page.keyboard.press('Escape'); await expect(page.locator('#actor-layer [data-creature="1"]')).toBeFocused(); expect(h.errors).toEqual([]);
});

test('Next keeps the keyboard focus while the following result plays', async ({ page }) => {
  const h = await mount(page); await step(page); await h.publish([theirs(10), entry(11, { actor: 4 })]);
  await finishCast(page); await page.locator('#playback-next').click();
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '11');
  await expect(page.locator('#playback-next')).toBeFocused(); await expect(page.locator('#playback-next')).toBeDisabled();
  await finishCast(page, 11); await expect(page.locator('#playback-next')).toBeEnabled();
  await page.keyboard.press('Enter'); await expect(page.locator('#playback-next')).toBeHidden();
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', ''); expect(h.errors).toEqual([]);
});

test('in Auto the own cast moves on by itself and an incoming one waits for its OK, an inspection over it included', async ({ page }) => {
  // Two casts and five paced seconds of full-size desktop frames on a frozen clock: about 35 s on a quiet machine,
  // past a minute on a busy two-worker runner.
  test.setTimeout(120000);
  const h = await mount(page); await h.publish([entry(10), entry(11, { actor: 4, targets: [1] }), { ...cleanup, sequence: 12 }]);
  await expect(page.locator('#playback-next')).toBeHidden(); await finishCast(page); await page.clock.runFor(SETTLE_MS);
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '11');
  await page.locator('[data-creature="2"]').click(); await expect(page.locator('#inspect-dialog')).toBeVisible();
  await finishCast(page, 11); await page.clock.runFor(5000);
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '11'); await expect(page.locator('#reading-kind')).toContainText("Opponent's action");
  await page.keyboard.press('Escape'); await expect(page.locator('#combat-open')).toBeHidden();
  await expect(page.locator('#playback-next')).toHaveText('Next'); await page.locator('#playback-next').click();
  await expect(page.locator('#reading-title')).toHaveText('Aftermath'); expect(h.errors).toEqual([]);
});
