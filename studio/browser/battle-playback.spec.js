import { test, expect } from '@playwright/test';
import { openSpells } from './battle-spells-fixture.js';
import { audioModule, probeAudio } from './battle-audio-probe.js';
import { readFileSync } from 'node:fs';
import { actionCue } from '../../table/battle-playback.js';
import { WINDUP_MS, IMPACT_MS, SETTLE_MS } from '../../table/battle-timing.js';
import { mountSpellScene, spellFrame, presentSpell, renderedEffects, actorPosition } from './battle-spell-scene.js';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningBattlePlayer);
import { mount, entry, before, creature } from './battle-playback-fixture.js';
const fits = page => fitsViewport(page, '.creature, #playback-skip, #playback-result, #combat-open, #settings');
test('ordered confirmed frames reveal damage at impact and never replay duplicate responses', async ({ page }, info) => {
  // Full-size frames rendered across several paced seconds outlast 30 s on a busy desktop runner, as in the pacing spec.
  test.setTimeout(60000);
  const h = await mount(page);
  await h.publish([entry(12), entry(10), entry(10)]);
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '10');
  await expect(page.locator('[data-creature="4"] .creature-stats')).toContainText('20/20');
  await fits(page); await page.clock.runFor(860);
  await expect(page.locator('[data-creature="4"] .creature-stats')).toContainText('13/20');
  await expect(page.locator('[data-creature="4"] .creature-effect')).toContainText('Damage 7!');
  await page.screenshot({ path: info.outputPath('confirmed-impact.jpg'), type: 'jpeg', quality: 85 });
  await h.publish([entry(10), entry(12)]); await page.clock.runFor(2200);
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '12');
  await page.clock.runFor(3200); await expect(page.locator('#playback-skip')).toBeHidden();
  await h.publish([entry(12)]); await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'decision');
  expect(h.errors).toEqual([]);
});
test('the next choice is withdrawn while results are read, and skip settles to the latest board', async ({ page }) => {
  // Three casts of full-size frames on a frozen clock: about 31 s on a quiet desktop, past a minute on a busy two-worker runner.
  test.setTimeout(120000);
  const h = await mount(page); await h.publish([entry(10, { actor: 4, targets: [1] }), entry(11, { actor: 5, targets: [1] })]);
  // An opponent's action offers OK alone, never Skip, and the next choice waits for it.
  await expect(page.locator('#playback-next')).toBeVisible(); await expect(page.locator('#playback-skip')).toBeHidden();
  await expect(page.locator('#combat-dialog')).toBeHidden();
  // Past the incoming impact's timers (windup and impact): a held action has none left to wait for.
  await page.clock.runFor(WINDUP_MS + IMPACT_MS + 200); await expect(page.locator('#combat-dialog')).toBeHidden();
  await page.locator('#playback-next').click(); await page.clock.runFor(WINDUP_MS + IMPACT_MS + 200);
  await page.locator('#playback-next').click();
  await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'decision'); await expect(page.locator('#combat-dialog')).toBeVisible();
  await h.publish([entry(12)]); await expect(page.locator('#battle')).toHaveAttribute('data-reading', 'true'); await expect(page.locator('#combat-dialog')).toBeHidden();
  await page.clock.runFor(3200); await expect(page.locator('#combat-dialog')).toBeVisible();
  await openSpells(page); await expect(page.locator('#combat-dialog')).toBeVisible(); expect(h.errors).toEqual([]);
});
test('an incoming last action holds for its OK however long; the next round and its mutation wait for it', async ({ page }) => {
  // Full-size frames rendered across several paced seconds outlast 30 s on a busy desktop runner, as in the pacing spec.
  test.setTimeout(60000);
  const h = await mount(page); const after = before.map(one => creature(one.id, one.id === 1 ? 0 : 20));
  const board = { ...h.state.view.board, roundNumber: 3, phase: 'Planning', subPhase: 'Evolution', allies: after.slice(0, 3), enemies: after.slice(3) };
  await h.publish([entry(10, { actor: 4, targets: [1], after })], { board, waitingFor: 'Evolution' });
  // Twice what the cast and its settle take: no timer is left to move a held action on.
  await page.clock.runFor(2 * (WINDUP_MS + IMPACT_MS + SETTLE_MS));
  await expect(page.locator('[data-creature="1"] .creature-tag')).toHaveText('DEFEATED'); await expect(page.locator('#round-number')).toHaveText('02');
  await expect(page.locator('#planning-open')).toBeHidden(); await expect(page.locator('#planning-dialog')).toBeHidden();
  await expect(page.locator('#playback-next')).toHaveText('OK'); await expect(page.locator('#playback-next')).toBeFocused();
  await page.keyboard.press('Enter'); await expect(page.locator('#round-number')).toHaveText('03'); expect(h.errors).toEqual([]);
});
test('the final lethal action remains in its round before the next upkeep snapshot', async ({ page }) => {
  const h = await mount(page); const after = before.map(one => creature(one.id, one.id === 4 ? 0 : 20));
  const board = { ...h.state.view.board, roundNumber: 3, phase: 'Planning', subPhase: 'Evolution', allies: after.slice(0, 3), enemies: after.slice(3) };
  await h.publish([entry(10, { after })], { board, waitingFor: 'Evolution' });
  await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'windup');
  await expect(page.locator('#round-number')).toHaveText('02'); await page.clock.runFor(860);
  await expect(page.locator('[data-creature="4"] .creature-tag')).toHaveText('DEFEATED');
  await page.clock.runFor(2400); await expect(page.locator('#round-number')).toHaveText('03'); expect(h.errors).toEqual([]);
});
test('multi-target impacts, conditions and an enemy actor render without lab assumptions', async ({ page }) => {
  const h = await mount(page);
  const after = before.map(one => ({ ...one, health: one.id < 3 ? 13 : 20,
    isStunned: one.id === 2, conditions: one.id === 2 ? [{ effect: { kind: 'Bleed', amountPerRound: 2 }, remainingRounds: 2 }] : [] }));
  await h.publish([entry(10, { actor: 4, targets: [1, 2], after, outcomes: [
    { kind: 'DamageOutcome', target: 1, amount: 7 }, { kind: 'DamageOutcome', target: 2, amount: 7 },
    { kind: 'ConditionOutcome', target: 2, effect: { kind: 'Bleed', amountPerRound: 2 } }] })]);
  await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'windup'); await page.clock.runFor(860);
  await expect(page.locator('[data-creature="1"] .creature-effect')).toContainText('Damage 7');
  await expect(page.locator('[data-creature="2"] .creature-effect')).toContainText('Bleed 2');
  await page.locator('[data-creature="2"]').click(); await expect(page.locator('#inspect-effects')).toContainText('Stunned');
  expect(h.errors).toEqual([]);
});
for (const kind of ['fizzle', 'support', 'legacy']) test(`${kind} keeps the real outcome readable without a fabricated strike`, async ({ page }) => {
  const h = await mount(page);
  await h.publish([entry(10, { fizzled: kind === 'fizzle', legacy: kind === 'legacy', targets: kind === 'support' ? [] : [4],
    outcomes: kind === 'support' ? [{ kind: 'HealOutcome', target: 1, amount: 3 }, { kind: 'EnergyOutcome', target: 1, amount: 2 }] : undefined })]);
  // While it plays, the reading card opens the full result; a legacy cast, which settles at once, the dock's result.
  const opener = page.locator(kind === 'legacy' ? '#playback-result' : '#reading-open');
  // A legacy cast settles at once, and the spell choice opens by itself (ADR 0149): the dock's result is under it.
  if (kind === 'legacy') { await expect(page.locator('#combat-dialog')).toBeVisible(); await page.locator('#combat-dialog-close').click(); }
  await expect(opener).toBeVisible();
  await page.clock.runFor(860); await opener.click();
  await expect(page.locator('#result-text')).toContainText(kind === 'fizzle' ? 'fizzled: Not enough energy' : kind === 'support' ? 'Heal 3' : 'Damage 7');
  if (kind === 'legacy') await expect(page.locator('#playback-skip')).toBeHidden();
  expect(h.errors).toEqual([]);
});
test('the dock\'s instruction is back once the last action has settled', async ({ page }) => {
  const h = await mount(page); await h.publish([entry(10)]);
  await expect(page.locator('#battle')).toHaveAttribute('data-reading', 'true');
  await page.clock.runFor(3200); await expect(page.locator('#playback-skip')).toBeHidden();
  // The spell choice is back by itself (ADR 0149); closed, it leaves the dock's instruction and last result.
  await expect(page.locator('#combat-dialog')).toBeVisible(); await page.locator('#combat-dialog-close').click();
  await expect(page.locator('#playback-result')).toBeVisible();
  await expect(page.locator('#instruction')).toBeVisible();
  await expect(page.locator('#instruction')).toContainText('choose a spell, then targets');
});
test('reduced motion shows applied results immediately and resize cancels playback safely', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' }); const h = await mount(page); await h.publish([entry(10)]);
  await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'impact');
  await expect(page.locator('[data-creature="4"] .creature-stats')).toContainText('13/20');
  await page.setViewportSize({ width: 844, height: 390 }); await page.clock.runFor(100);
  await expect(page.locator('#playback-skip')).toBeHidden(); await fits(page); expect(h.errors).toEqual([]);
});
for (const reason of ['hidden', 'revoked']) test(`${reason} clears private playback and fences its pending callbacks`, async ({ page }) => {
  const h = await mount(page); await h.publish([entry(10)]); await expect(page.locator('#battle')).toHaveAttribute('data-reading', 'true');
  if (reason === 'hidden') await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
  if (reason === 'revoked') { h.state.status = 403; await page.evaluate(() => window.notifyBattle()); }
  await expect(page.locator('#result-text')).toBeEmpty(); await expect(page.locator('.creature')).toHaveCount(0);
  await page.clock.runFor(3000); await expect(page.locator('#result-text')).toBeEmpty();
  await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'decision');
  if (reason === 'hidden') {
    h.state.view.feed = [entry(10), entry(11)]; h.state.view.feedNext = 12;
    await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: false }); document.dispatchEvent(new Event('visibilitychange')); });
    // Back in view, the cast cut short and the one that came while hidden play from the last result shown (ADR 0148).
    await expect(page.locator('.creature')).toHaveCount(6); await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '10');
    await expect(page.locator('#playback-skip')).toHaveText('Skip all (2)');
  }
  expect(h.errors).toEqual([]);
});

test('transient connection loss settles playback and repeated events stay consumed on recovery', async ({ page }) => {
  const h = await mount(page); await h.publish([entry(10)]); await expect(page.locator('#battle')).toHaveAttribute('data-reading', 'true');
  h.state.status = 503; await page.evaluate(() => window.notifyBattle());
  await expect(page.locator('#battle')).toHaveAttribute('data-connection', 'stale');
  await expect(page.locator('#playback-skip')).toBeHidden();
  h.state.status = 200; await h.publish([entry(10)]);
  await expect(page.locator('#battle')).toHaveAttribute('data-connection', 'live');
  await expect(page.locator('#playback-skip')).toBeHidden(); expect(h.errors).toEqual([]);
});
test('confirmed results and commands fit short portrait and landscape', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'The same viewport sweep runs once.');
  const h = await mount(page);
  for (const [index, size] of [{ width: 320, height: 568 }, { width: 844, height: 390 }].entries()) {
    await page.setViewportSize(size); await page.clock.runFor(150);
    // Two results, so Skip all is offered (ADR 0141).
    await h.publish([entry(10 + 2 * index), entry(11 + 2 * index)]); await expect(page.locator('#playback-skip')).toBeVisible();
    await fits(page); await page.locator('#playback-skip').click(); await fits(page);
    // Settled, the dock holds the result, the instruction and the next action together, inside its own bounds.
    await expect.poll(() => page.evaluate(() => {
      const dock = document.querySelector('.command-dock').getBoundingClientRect();
      return [...document.querySelectorAll('#playback-result, #instruction, #combat-open')].filter(node => !node.hidden && node.getClientRects().length).flatMap(node => {
        const box = node.getBoundingClientRect();
        return box.top < dock.top - 1 || box.bottom > dock.bottom + 1 ? [node.id] : [];
      });
    })).toEqual([]);
  }
  expect(h.errors).toEqual([]);
});

// ART-4: actual Phaser vector rendering, with the same confirmed cue adapter the live queue uses, fed the
// spell art the builder publishes on the cards and the harmful effects the catalogue names.
const drawn = new Map(Object.entries(JSON.parse(readFileSync(new URL('../../data/presentation/spell-art.json', import.meta.url), 'utf8')))
  .map(([id, visual]) => [id, { id, name: id, visual }]));
const visualCue = (spell, outcomes, status = 'Resolved') => actionCue({ actor: { id: 1 },
  action: { spell: `spell:${spell}:v1`, targets: [4, 5, 6] }, status,
  effects: outcomes.map(([id, tone, metadata]) => ({ target: { id }, tone, text: 'Confirmed', ...metadata })) }, drawn, new Set(['Bleed']));

test('each family draws its motif on the confirmed recipients only, and every cast is cleared', async ({ page }, info) => {
  test.setTimeout(90000); // Several rendered casts and captures per viewport, including software GPUs.
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await mountSpellScene(page);
  for (const [spell, motif, outcomes] of [
    ['pummel', 'impact', [[4, 'harm']]], ['tornado', 'wave', [[4, 'harm'], [5, 'harm']]],
    ['throwing_star', 'quill', [[4, 'harm'], [5, 'harm']]], ['noxious_cure', 'spores', [[2, 'recovery'], [3, 'condition']]],
    ['lightning_bolt', 'arc', [[4, 'harm']]], ['meteor', 'fire', [[4, 'harm'], [5, 'harm']]],
    ['ice_spear', 'ice', [[4, 'condition']]], ['bone_ward', 'bones', [[1, 'condition', { kind: 'ConditionOutcome', effect: { kind: 'Bleed' }, onCaster: true }], [2, 'condition']]],
    ['summon_minions', 'spirits', [[1, 'harm'], [4, 'condition'], [5, 'condition']]],
    ['toxic_waves', 'void', [[4, 'harm'], [5, 'harm']]], ['rejuvenate', 'vital', [[2, 'recovery']]],
  ]) {
    const frame = spellFrame(visualCue(spell, outcomes));
    await presentSpell(page, frame, 'decision'); await presentSpell(page, frame, 'windup');
    await page.clock.runFor(400);
    if (frame.cue.visual.delivery !== 'melee') {
      const position = await actorPosition(page);
      expect(position.x).toBe(position.homeX); expect(position.y).toBe(position.homeY);
    }
    await presentSpell(page, frame, 'impact'); await page.clock.runFor(32);
    const effects = await renderedEffects(page);
    expect(effects.map(effect => effect.creature).sort()).toEqual(outcomes.map(([id]) => String(id)).sort());
    expect(effects.filter(effect => effect.creature !== '1').every(effect => effect.motif === motif && effect.alpha > 0)).toBe(true);
    if (frame.cue.effects.some(effect => effect.wound)) expect(effects.find(effect => effect.creature === '1').motif).toBe('wound');
    if (['summon_minions', 'bone_ward', 'throwing_star', 'lightning_bolt', 'noxious_cure', 'toxic_waves'].includes(spell)) {
      await page.screenshot({ path: info.outputPath(`art4-${spell}.png`) });
    }
    await presentSpell(page, frame, 'decision');
    expect(await renderedEffects(page)).toEqual([]);
  }
  expect(errors).toEqual([]);
});

const quills = () => spellFrame(visualCue('throwing_star', [[4, 'harm'], [5, 'harm']]));
async function mounted(page) {
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await mountSpellScene(page); return errors;
}
test('quills fly to each confirmed recipient and are gone once the action settles', async ({ page }) => {
  const errors = await mounted(page); const frame = quills();
  await presentSpell(page, frame, 'decision'); await presentSpell(page, frame, 'windup');
  expect((await renderedEffects(page)).map(effect => effect.creature)).toEqual(['4', '5']);
  await presentSpell(page, frame, 'decision'); await page.clock.runFor(1400);
  expect(await renderedEffects(page)).toEqual([]); expect(errors).toEqual([]);
});
test('a resize during the windup cancels the quills in flight', async ({ page }) => {
  const errors = await mounted(page); const frame = quills();
  await presentSpell(page, frame, 'decision'); await presentSpell(page, frame, 'windup');
  await page.setViewportSize({ width: 844, height: 390 }); await page.clock.runFor(1400);
  expect(await renderedEffects(page)).toEqual([]); expect(errors).toEqual([]);
});
test('reduced motion draws no vector at impact', async ({ page }) => {
  const errors = await mounted(page); const frame = quills();
  await presentSpell(page, frame, 'decision'); await presentSpell(page, frame, 'impact', true);
  expect(await renderedEffects(page)).toEqual([]); expect(errors).toEqual([]);
});
test('a fizzle launches nothing and leaves only a broken cast mark at the caster', async ({ page }) => {
  const errors = await mounted(page); const fizzle = spellFrame(visualCue('throwing_star', [[4, 'harm']], 'Fizzled'));
  await presentSpell(page, fizzle, 'decision'); await presentSpell(page, fizzle, 'windup'); expect(await renderedEffects(page)).toEqual([]);
  await presentSpell(page, fizzle, 'impact');
  expect((await renderedEffects(page)).map(effect => [effect.creature, effect.motif])).toEqual([['1', 'fizzle']]);
  await page.clock.runFor(1800); expect(await renderedEffects(page)).toEqual([]); expect(errors).toEqual([]);
});
test('a caster that is its own beneficiary shows both the blood cost and the ward', async ({ page }) => {
  const errors = await mounted(page);
  const selfWard = spellFrame(visualCue('bone_ward', [[1, 'condition', { kind: 'ConditionOutcome', effect: { kind: 'Bleed' }, onCaster: true }],
    [1, 'condition', { kind: 'ConditionOutcome', effect: { kind: 'DefenseBuff' }, onCaster: false }]]));
  await presentSpell(page, selfWard, 'decision'); await presentSpell(page, selfWard, 'impact');
  expect((await renderedEffects(page)).map(effect => effect.motif).sort()).toEqual(['bones', 'wound']); expect(errors).toEqual([]);
});
test('figures created by a mutation stand on their rings when reduced motion enters impact directly', async ({ page }) => {
  const errors = await mounted(page); const frame = quills();
  await presentSpell(page, frame, 'decision');
  const changed = { ...frame, creatures: frame.creatures.map(one => ({ ...one, form: 'base-creature' })) };
  await presentSpell(page, changed, 'impact', true);
  expect(await page.evaluate(() => [...window.artScene.figures.values()].every(figure =>
    figure.image.x === figure.home.x && figure.image.y === figure.home.y))).toBe(true);
  expect(errors).toEqual([]);
});

test('live playback shows the necromantic recipients apart from the caster blood cost', async ({ page }, info) => {
  const h = await mount(page);
  const shapes = ['tier:occultist:v1', 'tier:brute:v1', 'tier:prowler:v1', 'tier:brute:v1', 'tier:prowler:v1', 'tier:occultist:v1'];
  const start = before.map((one, index) => ({ ...one, acquiredTiers: [shapes[index]] }));
  const after = start.map(one => ({ ...one, health: one.id === 1 ? 18 : 20, energy: one.id === 1 ? 2 : 5,
    conditions: [4, 5].includes(one.id) ? [{ effect: { kind: 'Bleed', amountPerRound: 2 }, remainingRounds: 2 }] : [] }));
  const action = entry(10, { spell: 'spell:summon_minions:v1', targets: [4, 5], after, outcomes: [
    { kind: 'DamageOutcome', target: 1, amount: 2, onCaster: true },
    { kind: 'ConditionOutcome', target: 4, effect: { kind: 'Bleed', amountPerRound: 2 } },
    { kind: 'ConditionOutcome', target: 5, effect: { kind: 'Bleed', amountPerRound: 2 } },
  ] });
  action.event.frame.before = start;
  action.event.resolution.isCritical = false;
  await h.publish([action], { board: { ...h.state.view.board, allies: after.slice(0, 3), enemies: after.slice(3) } });
  await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'windup');
  await page.clock.runFor(900);
  await expect(page.locator('[data-creature="1"] .creature-effect')).toContainText('Damage 2');
  await expect(page.locator('[data-creature="4"] .creature-effect')).toContainText('Bleed 2');
  await expect(page.locator('[data-creature="5"] .creature-effect')).toContainText('Bleed 2');
  await expect(page.locator('[data-creature="6"] .creature-effect')).toBeEmpty();
  await fits(page); await page.screenshot({ path: info.outputPath('art4-live-necromancy.png') });
  // The player's own cast plays out and moves on by itself (ADR 0141).
  await page.clock.runFor(WINDUP_MS + IMPACT_MS + SETTLE_MS);
  await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'decision');
  expect(h.errors).toEqual([]);
});

// ART-6: one playback rate for the queue and the renderer, and an opt-in sound that stops with the action.
async function atSpeed(page, h, speed) {
  await page.locator('#settings').click(); await page.selectOption('#playback-speed', speed);
  await page.getByRole('button', { name: 'Close settings' }).click();
}
test('at 2x the confirmed result lands at half the windup', async ({ page }, info) => {
  const h = await mount(page); await atSpeed(page, h, '2');
  await h.publish([entry(10)]); await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'windup');
  await page.clock.runFor(400); await expect(page.locator('[data-creature="4"] .creature-stats')).toContainText('20/20');
  await page.clock.runFor(35); await expect(page.locator('[data-creature="4"] .creature-stats')).toContainText('13/20');
  await page.screenshot({ path: info.outputPath('art6-speed.png') }); expect(h.errors).toEqual([]);
});
test('the scene is handed the speed the queue plays at', async ({ page }) => {
  const h = await mount(page);
  await expect(page.locator('#battle')).toHaveAttribute('data-rate', '1');
  await atSpeed(page, h, '1.5'); await expect(page.locator('#battle')).toHaveAttribute('data-rate', '1.5');
  expect(h.errors).toEqual([]);
});
test('changing the speed mid-action settles it and a repeated poll does not replay it', async ({ page }) => {
  const h = await mount(page); await atSpeed(page, h, '2');
  await h.publish([entry(10)]); await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'windup');
  await atSpeed(page, h, '1.5'); await expect(page.locator('#playback-skip')).toBeHidden();
  await h.publish([entry(10)]); await expect(page.locator('#playback-skip')).toBeHidden(); expect(h.errors).toEqual([]);
});
test('a reduced-motion result stays readable for 650 ms even at 2x', async ({ page }) => {
  const h = await mount(page); await atSpeed(page, h, '2');
  await page.locator('#settings').click(); await page.locator('#reduce-motion').check();
  await page.getByRole('button', { name: 'Close settings' }).click();
  await h.publish([entry(11)]); await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'impact');
  await page.clock.runFor(500); await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'impact');
  await page.clock.runFor(180); await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'resolved');
  expect(h.errors).toEqual([]);
});
test('the scene lunges at the speed it is given', async ({ page }) => {
  await mountSpellScene(page);
  const frame = spellFrame({ kind: 'strike', struck: ['4'], effects: [{ creature: '4', tone: 'harm', text: 'Damage 7', wound: false }],
    visual: { family: 'physical', motif: 'impact', delivery: 'melee' }, recipients: ['4'], contact: ['4'], changes: [] });
  const travelled = async rate => {
    await presentSpell(page, frame, 'decision', false, rate); await presentSpell(page, frame, 'windup', false, rate);
    // 430 ms is past the whole 2x lunge (120 ms draw, 300 ms strike) and early in the 1x strike (240 ms, then 600 ms).
    await page.clock.runFor(430);
    const position = await actorPosition(page); return Math.abs(position.x - position.homeX);
  };
  const atOne = await travelled(1), atTwo = await travelled(2);
  expect(atTwo).toBeGreaterThan(atOne * 3);
});

async function soundOn(page) {
  await page.locator('#settings').click(); await page.locator('#sound').check();
  await expect(page.locator('#sound')).toBeChecked();
  await page.getByRole('button', { name: 'Close settings' }).click();
}
const probe = page => page.evaluate(() => window.audioProbe);
async function sounding(page, h, sequence) {
  // A second result behind it, so Skip all is offered while the first one sounds.
  await h.publish([entry(sequence), entry(sequence + 1)]); await expect(page.locator('#battle')).toHaveAttribute('data-playback', 'windup');
  await page.clock.runFor(860); await expect.poll(() => probe(page).then(one => one.starts)).toBe(1);
}
test('no audio context exists before the player turns sound on', async ({ page }) => {
  await probeAudio(page); const h = await mount(page);
  await h.publish([entry(10)]); await page.clock.runFor(3200);
  expect(await probe(page)).toEqual({ contexts: 0, starts: 0, stops: 0 }); expect(h.errors).toEqual([]);
});
test('one confirmed impact sounds once, however often it is polled', async ({ page }) => {
  await probeAudio(page); const h = await mount(page); await soundOn(page);
  await sounding(page, h, 12);
  await h.publish([entry(12)]); await page.clock.runFor(3000);
  expect((await probe(page)).starts).toBe(1); expect(h.errors).toEqual([]);
});
test('skipping during the impact cuts its tone', async ({ page }) => {
  await probeAudio(page); const h = await mount(page); await soundOn(page);
  await sounding(page, h, 12);
  await page.locator('#playback-skip').click();
  expect((await probe(page)).stops).toBeGreaterThan(0); expect(h.errors).toEqual([]);
});
test('a hidden page cuts the tone that was playing', async ({ page }) => {
  await probeAudio(page); const h = await mount(page); await soundOn(page);
  await sounding(page, h, 12);
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
  expect((await probe(page)).stops).toBeGreaterThan(0); expect(h.errors).toEqual([]);
});
test('a seat revoked while sound is being turned on leaves it off', async ({ page }) => {
  await probeAudio(page, { holdResume: true }); const h = await mount(page);
  await page.locator('#settings').click(); await page.locator('#sound').check();
  await page.getByRole('button', { name: 'Close settings' }).click();
  h.state.status = 403; await page.evaluate(() => window.notifyBattle());
  await expect(page.locator('.creature')).toHaveCount(0);
  await page.evaluate(() => window.releaseAudio());
  await expect(page.locator('#sound')).not.toBeChecked(); expect(h.errors).toEqual([]);
});
test('turning sound off keeps the next impact silent', async ({ page }) => {
  await probeAudio(page); const h = await mount(page); await soundOn(page);
  await page.locator('#settings').click(); await page.locator('#sound').uncheck();
  await page.getByRole('button', { name: 'Close settings' }).click();
  await h.publish([entry(13)]); await page.clock.runFor(3200);
  expect((await probe(page)).starts).toBe(0); expect(h.errors).toEqual([]);
});

// The owner itself, against a context the test controls.
async function owner(page) { await page.goto('/table/battle.html'); await audioModule(page); }
// var, not let: a direct eval's let and const stay inside the eval, and the test needs them after it.
const fakeContext = `
  var resume = () => {}; var counts = { starts: 0, stops: 0, releases: 0, resumes: 0 };
  var envelope = { setValueAtTime() {}, exponentialRampToValueAtTime() {} };
  var context = { state: 'running', currentTime: 0, destination: {},
    resume: () => { counts.resumes++; return new Promise(done => { resume = done; }); },
    createOscillator: () => ({ frequency: envelope, connect() {}, disconnect() { counts.releases++; }, start() { counts.starts++; }, stop() { counts.stops++; }, onended: null }),
    createGain: () => ({ gain: envelope, connect() {}, disconnect() { counts.releases++; } }) };`;
test('muting while a resume is pending keeps sound off when it lands', async ({ page }) => {
  await owner(page);
  expect(await page.evaluate(async source => { eval(source); const audio = new window.TestBattleAudio(() => context);
    const pending = audio.enable(true); audio.stop(); resume(); return await pending; }, fakeContext)).toBe(false);
});
test('an animation cancelled while a resume is pending does not turn sound off', async ({ page }) => {
  await owner(page);
  expect(await page.evaluate(async source => { eval(source); const audio = new window.TestBattleAudio(() => context);
    const pending = audio.enable(true); audio.silence(); resume(); return await pending; }, fakeContext)).toBe(true);
});
test('a new tone releases the one before it, so one voice at most is alive', async ({ page }) => {
  await owner(page);
  expect(await page.evaluate(async source => { eval(source); const audio = new window.TestBattleAudio(() => context);
    context.resume = async () => {}; await audio.enable(true);
    audio.play({ family: 'necromantic', motif: 'spirits', delivery: 'cast' }); audio.play(); audio.stop(); return counts; }, fakeContext))
    .toMatchObject({ starts: 2, stops: 4, releases: 4 });
});
test('a context the platform suspended is asked back on the next impact instead of staying silent', async ({ page }) => {
  await owner(page);
  expect(await page.evaluate(async source => { eval(source); const audio = new window.TestBattleAudio(() => context);
    context.resume = async () => { counts.resumes++; }; await audio.enable(true);
    context.state = 'suspended'; audio.play(); return counts; }, fakeContext)).toMatchObject({ starts: 0, resumes: 2 });
});
test('unavailable audio leaves sound off and throws nothing', async ({ page }) => {
  await owner(page);
  expect(await page.evaluate(() => new window.TestBattleAudio(() => { throw new Error('unavailable'); }).enable(true))).toBe(false);
});

const wardFrame = () => spellFrame({ kind: 'pulse', struck: [], effects: [{ creature: '2', tone: 'condition', text: 'Confirmed ward', wound: false }],
  visual: { family: 'necromantic', motif: 'bones', delivery: 'cast' }, recipients: ['2'], contact: ['2'], changes: [] });
const effectState = page => page.evaluate(() => window.artScene.children.list.filter(one => one.name === 'spell-vfx').map(one => ({ x: one.x, y: one.y, scale: one.scaleX })));
test('a ward rises and grows into place', async ({ page }, info) => {
  await mountSpellScene(page); const frame = wardFrame();
  await presentSpell(page, frame, 'decision'); await presentSpell(page, frame, 'impact');
  const initial = await effectState(page); await page.clock.runFor(120); const settled = await effectState(page);
  expect(settled[0].y).toBeLessThan(initial[0].y); expect(settled[0].scale).toBeGreaterThan(initial[0].scale);
  await page.screenshot({ path: info.outputPath('art6-ward.png') });
});
test('an arc stays pinned to its endpoints while it lands', async ({ page }) => {
  await mountSpellScene(page);
  const frame = spellFrame({ kind: 'strike', struck: ['4'], effects: [{ creature: '4', tone: 'harm', text: 'Damage 5', wound: false }],
    visual: { family: 'elemental', motif: 'arc', delivery: 'cast' }, recipients: ['4'], contact: ['4'], changes: [] });
  await presentSpell(page, frame, 'decision'); await presentSpell(page, frame, 'impact');
  const initial = await effectState(page); await page.clock.runFor(50); const landing = await effectState(page);
  expect(initial[0].scale).toBe(1); expect(landing[0].scale).toBe(1);
  expect(landing[0].x).toBe(initial[0].x); expect(landing[0].y).toBe(initial[0].y);
});
test('returning to a decision clears the effects', async ({ page }) => {
  await mountSpellScene(page); const frame = wardFrame();
  await presentSpell(page, frame, 'decision'); await presentSpell(page, frame, 'impact');
  await presentSpell(page, frame, 'decision'); expect(await renderedEffects(page)).toEqual([]);
});
test('reduced motion shows the result with no effect drawn', async ({ page }) => {
  await mountSpellScene(page); const frame = wardFrame();
  await presentSpell(page, frame, 'decision'); await presentSpell(page, frame, 'impact', true);
  expect(await renderedEffects(page)).toEqual([]);
});
test('a page opened again plays the opponent\'s actions it missed before the player\'s own turn', async ({ page }) => {
  test.setTimeout(60000);
  const h = await mount(page);
  await expect.poll(() => page.evaluate(() => localStorage.getItem('downfall.battle.seen.v1'))).toContain('"sequence":9');
  // The page is closed while the opponent plays its slot; it is now this seat's turn.
  h.state.view = { ...h.state.view, feed: [entry(10, { actor: 4, targets: [1] })], feedNext: 11, waitingAsked: 6 };
  await page.reload(); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '10');
  await expect(page.locator('#reading-kind')).toContainText("Opponent's action"); await expect(page.locator('#combat-dialog')).toBeHidden();
  await page.clock.runFor(4000); await expect(page.locator('#playback-next')).toHaveText('OK');
  await page.locator('#playback-next').click();
  await expect(page.locator('#battle')).toHaveAttribute('data-sequence', ''); await expect(page.locator('#combat-dialog')).toBeVisible();
  await expect.poll(() => page.evaluate(() => localStorage.getItem('downfall.battle.seen.v1'))).toContain('"sequence":10');
  // Opened again with nothing new, the board is as it is.
  await page.reload(); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#combat-dialog')).toBeVisible(); await expect(page.locator('#battle')).toHaveAttribute('data-sequence', '');
  expect(h.errors).toEqual([]);
});
