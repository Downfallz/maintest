import { test, expect } from '@playwright/test';
import { fitsViewport } from './fit.js';
const creature = (id, health = 20) => ({ id, name: `Creature ${id}`, health, maxHealth: 20, energy: 5, totalDefense: 2,
  currentInitiative: 7, isAlive: health > 0, isStunned: false, isStunImmune: false, knownSpells: ['strike'], conditions: [] });
const before = [1, 2, 3, 4, 5, 6].map(id => creature(id));
const timeline = before.map(one => ({ creature: one.id, owner: one.id < 4 ? 'Player1' : 'Player2', speed: 'Standard' }));
const legalTargets = { candidates: [4, 5, 6], minTargets: 1, maxTargets: 1 };
function entry(sequence, { actor = 1, spell = 'strike', targets = [4], after = before.map(one => creature(one.id, one.id === 4 ? 13 : 20)), fizzled = false, legacy = false, outcomes } = {}) {
  return { sequence, event: { kind: 'CombatActionResolved', roundId: 2,
    resolution: { action: { actor, spell, targets }, isCritical: !fizzled, fizzled, fizzleReason: { message: 'Not enough energy' } },
    appliedOutcomes: outcomes ?? (fizzled ? [] : targets.map(target => ({ kind: 'DamageOutcome', target, amount: 7, critical: true }))),
    ...(legacy ? {} : { frame: { before, after, timeline, rollOffs: [] } }) } };
}
async function mount(page) {
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await page.clock.install();
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' }));
    const original = window.fetch;
    window.fetch = (url, options) => {
      if (String(url).endsWith('/events')) return Promise.resolve(new Response(new ReadableStream({ start(controller) {
        window.notifyArena = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
      } }), { headers: { 'Content-Type': 'text/event-stream' } }));
      return original(url, options);
    };
  });
  const state = { status: 200, requests: [], view: { board: { slot: 'Player1', roundNumber: 2, phase: 'Combat', subPhase: 'Activation',
    activationCursor: 1, allies: before.slice(0, 3), enemies: before.slice(3), timeline }, waitingFor: 'Action', waitingAsked: 5,
    waitingCreature: 2, options: { action: { creature: 2, spells: [{ spell: 'strike', legalTargets }] } }, feed: [], feedNext: 10 } };
  await page.route('**/api/**', route => {
    const request = route.request(), url = new URL(request.url()); state.requests.push({ path: url.pathname, query: url.search, method: request.method() });
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'table-a', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [{ id: 'strike', name: 'Strike', cost: 2 }, { id: 'spell:summon_minions:v1', name: 'Infestation', cost: 2 }] } });
    if (url.pathname === '/api/seat/player1') return route.fulfill({ status: state.status, json: state.view });
    return route.fulfill({ status: 500, body: 'Unexpected request' });
  });
  await page.goto('/table/arena.html'); await expect(page.locator('#arena')).toHaveAttribute('data-ready', 'true');
  await page.locator('#seat-choices button').click(); await expect(page.locator('#connection')).toHaveText('LIVE');
  await page.clock.pauseAt(new Date(Date.now() + 1000));
  const publish = async (entries, extra = {}) => {
    state.view = { ...state.view, ...extra, feed: entries, feedNext: Math.max(state.view.feedNext, ...entries.map(one => one.sequence + 1)) };
    await page.evaluate(() => window.notifyArena());
  };
  return { state, publish, errors };
}
const fits = page => fitsViewport(page, '.creature, #playback-skip, #playback-result, #combat-open, #settings');
test('ordered confirmed frames reveal damage at impact and never replay duplicate responses', async ({ page }, info) => {
  const h = await mount(page);
  await h.publish([entry(12), entry(10), entry(10)]);
  await expect(page.locator('#arena')).toHaveAttribute('data-sequence', '10');
  await expect(page.locator('[data-creature="4"] .creature-stats')).toContainText('20/20');
  await fits(page); await page.clock.runFor(440);
  await expect(page.locator('[data-creature="4"] .creature-stats')).toContainText('13/20');
  await expect(page.locator('[data-creature="4"] .creature-effect')).toContainText('Damage 7!');
  await page.screenshot({ path: info.outputPath('confirmed-impact.jpg'), type: 'jpeg', quality: 85 });
  await h.publish([entry(10), entry(12)]); await page.clock.runFor(1100);
  await expect(page.locator('#arena')).toHaveAttribute('data-sequence', '12');
  await page.clock.runFor(1600); await expect(page.locator('#playback-skip')).toBeHidden();
  await h.publish([entry(12)]); await expect(page.locator('#arena')).toHaveAttribute('data-playback', 'decision');
  expect(h.errors).toEqual([]);
});
test('skip and opening combat controls settle to the latest board without waiting for animations', async ({ page }) => {
  const h = await mount(page); await h.publish([entry(10), entry(11)]);
  await expect(page.locator('#playback-skip')).toBeVisible(); await page.locator('#playback-skip').click();
  await expect(page.locator('#arena')).toHaveAttribute('data-playback', 'decision');
  await h.publish([entry(12)]); await expect(page.locator('#playback-skip')).toBeVisible();
  await page.locator('#combat-open').click(); await expect(page.locator('#combat-dialog')).toBeVisible();
  await expect(page.locator('#playback-skip')).toBeHidden(); await page.clock.runFor(2500);
  await expect(page.locator('#combat-dialog')).toBeVisible();
});
test('the final lethal action remains in its round before the next upkeep snapshot', async ({ page }) => {
  const h = await mount(page); const after = before.map(one => creature(one.id, one.id === 4 ? 0 : 20));
  const board = { ...h.state.view.board, roundNumber: 3, phase: 'Planning', subPhase: 'Evolution', allies: after.slice(0, 3), enemies: after.slice(3) };
  await h.publish([entry(10, { after })], { board, waitingFor: 'Evolution' });
  await expect(page.locator('#arena')).toHaveAttribute('data-playback', 'windup');
  await expect(page.locator('#round-number')).toHaveText('02'); await page.clock.runFor(440);
  await expect(page.locator('[data-creature="4"] .creature-tag')).toHaveText('DEFEATED');
  await page.clock.runFor(1200); await expect(page.locator('#round-number')).toHaveText('03'); expect(h.errors).toEqual([]);
});
test('multi-target impacts, conditions and an enemy actor render without lab assumptions', async ({ page }) => {
  const h = await mount(page);
  const after = before.map(one => ({ ...one, health: one.id < 3 ? 13 : 20,
    isStunned: one.id === 2, conditions: one.id === 2 ? [{ effect: { kind: 'Bleed', amountPerRound: 2 }, remainingRounds: 2 }] : [] }));
  await h.publish([entry(10, { actor: 4, targets: [1, 2], after, outcomes: [
    { kind: 'DamageOutcome', target: 1, amount: 7 }, { kind: 'DamageOutcome', target: 2, amount: 7 },
    { kind: 'ConditionOutcome', target: 2, effect: { kind: 'Bleed', amountPerRound: 2 } }] })]);
  await expect(page.locator('#arena')).toHaveAttribute('data-playback', 'windup'); await page.clock.runFor(440);
  await expect(page.locator('[data-creature="1"] .creature-effect')).toContainText('Damage 7');
  await expect(page.locator('[data-creature="2"] .creature-effect')).toContainText('Bleed 2');
  await page.locator('[data-creature="2"]').click(); await expect(page.locator('#inspect-effects')).toContainText('Stunned');
  expect(h.errors).toEqual([]);
});
for (const kind of ['fizzle', 'support', 'legacy']) test(`${kind} keeps the real outcome readable without a fabricated strike`, async ({ page }) => {
  const h = await mount(page);
  await h.publish([entry(10, { fizzled: kind === 'fizzle', legacy: kind === 'legacy', targets: kind === 'support' ? [] : [4],
    outcomes: kind === 'support' ? [{ kind: 'HealOutcome', target: 1, amount: 3 }, { kind: 'EnergyOutcome', target: 1, amount: 2 }] : undefined })]);
  await expect(page.locator('#playback-result')).toBeVisible();
  await page.clock.runFor(440); await page.locator('#playback-result').click();
  await expect(page.locator('#result-text')).toContainText(kind === 'fizzle' ? 'fizzled: Not enough energy' : kind === 'support' ? 'Heal 3' : 'Damage 7');
  if (kind === 'legacy') await expect(page.locator('#playback-skip')).toBeHidden();
  expect(h.errors).toEqual([]);
});
test('the dock\'s instruction is back once the last action has settled', async ({ page }) => {
  const h = await mount(page); await h.publish([entry(10)]);
  await expect(page.locator('#playback-skip')).toBeVisible();
  await page.clock.runFor(1600); await expect(page.locator('#playback-skip')).toBeHidden();
  await expect(page.locator('#playback-result')).toBeVisible();
  await expect(page.locator('#instruction')).toBeVisible();
  await expect(page.locator('#instruction')).toContainText('Choose a spell, then targets');
});
test('reduced motion shows applied results immediately and resize cancels playback safely', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' }); const h = await mount(page); await h.publish([entry(10)]);
  await expect(page.locator('#arena')).toHaveAttribute('data-playback', 'impact');
  await expect(page.locator('[data-creature="4"] .creature-stats')).toContainText('13/20');
  await page.setViewportSize({ width: 844, height: 390 }); await page.clock.runFor(100);
  await expect(page.locator('#playback-skip')).toBeHidden(); await fits(page); expect(h.errors).toEqual([]);
});
for (const reason of ['seat', 'hidden', 'revoked']) test(`${reason} clears private playback and fences its pending callbacks`, async ({ page }) => {
  const h = await mount(page); await h.publish([entry(10)]); await expect(page.locator('#playback-skip')).toBeVisible();
  if (reason === 'seat') await page.locator('#switch-seat').click();
  if (reason === 'hidden') await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
  if (reason === 'revoked') { h.state.status = 403; await page.evaluate(() => window.notifyArena()); }
  await expect(page.locator('#result-text')).toBeEmpty(); await expect(page.locator('.creature')).toHaveCount(0);
  await page.clock.runFor(3000); await expect(page.locator('#result-text')).toBeEmpty();
  await expect(page.locator('#arena')).toHaveAttribute('data-playback', 'decision');
  if (reason === 'hidden') {
    h.state.view.feed = [entry(10), entry(11)]; h.state.view.feedNext = 12;
    await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: false }); document.dispatchEvent(new Event('visibilitychange')); });
    await expect(page.locator('.creature')).toHaveCount(6); await expect(page.locator('#playback-skip')).toBeHidden();
    await expect(page.locator('#result-text')).toBeEmpty();
  }
  expect(h.errors).toEqual([]);
});

test('transient connection loss settles playback and repeated events stay consumed on recovery', async ({ page }) => {
  const h = await mount(page); await h.publish([entry(10)]); await expect(page.locator('#playback-skip')).toBeVisible();
  h.state.status = 503; await page.evaluate(() => window.notifyArena());
  await expect(page.locator('#arena')).toHaveAttribute('data-connection', 'stale');
  await expect(page.locator('#playback-skip')).toBeHidden();
  h.state.status = 200; await h.publish([entry(10)]);
  await expect(page.locator('#arena')).toHaveAttribute('data-connection', 'live');
  await expect(page.locator('#playback-skip')).toBeHidden(); expect(h.errors).toEqual([]);
});
test('confirmed results and commands fit short portrait and landscape', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'The same viewport sweep runs once.');
  const h = await mount(page);
  for (const [index, size] of [{ width: 320, height: 568 }, { width: 844, height: 390 }].entries()) {
    await page.setViewportSize(size); await page.clock.runFor(150);
    await h.publish([entry(10 + index)]); await expect(page.locator('#playback-skip')).toBeVisible();
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

// ART-4: actual Phaser vector rendering, with the same confirmed cue adapter the live queue uses.
import { actionCue } from '../../table/arena-playback.js';
import { mountSpellScene, spellFrame, presentSpell, renderedEffects, actorPosition } from './arena-spell-scene.js';
const visualCue = (spell, outcomes, status = 'Resolved') => actionCue({ actor: { id: 1 },
  action: { spell: `spell:${spell}:v1`, targets: [4, 5, 6] }, status,
  effects: outcomes.map(([id, tone, metadata]) => ({ target: { id }, tone, text: 'Confirmed', ...metadata })) });

test('ART-4 renders distinct signatures only on confirmed recipients and clears every cast', async ({ page }, info) => {
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

test('ART-4 suppresses fizzle hits and reduced motion, and cancels projectiles on resize or skip', async ({ page }) => {
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await mountSpellScene(page);
  const frame = spellFrame(visualCue('throwing_star', [[4, 'harm'], [5, 'harm']]));
  await presentSpell(page, frame, 'decision'); await presentSpell(page, frame, 'windup');
  expect((await renderedEffects(page)).map(effect => effect.creature)).toEqual(['4', '5']);
  await presentSpell(page, frame, 'decision'); await page.clock.runFor(700);
  expect(await renderedEffects(page)).toEqual([]);
  await presentSpell(page, frame, 'windup'); await page.setViewportSize({ width: 844, height: 390 });
  await page.clock.runFor(700); expect(await renderedEffects(page)).toEqual([]);
  await presentSpell(page, frame, 'impact', true); expect(await renderedEffects(page)).toEqual([]);
  await presentSpell(page, frame, 'decision');
  const fizzle = spellFrame(visualCue('throwing_star', [[4, 'harm']], 'Fizzled'));
  await presentSpell(page, fizzle, 'windup'); expect(await renderedEffects(page)).toEqual([]);
  await presentSpell(page, fizzle, 'impact');
  expect((await renderedEffects(page)).map(effect => [effect.creature, effect.motif])).toEqual([['1', 'fizzle']]);
  await page.clock.runFor(900); expect(await renderedEffects(page)).toEqual([]);
  // A caster may also be the beneficiary: blood cost must not hide its own ward.
  const selfWard = spellFrame(visualCue('bone_ward', [[1, 'condition', { kind: 'ConditionOutcome', effect: { kind: 'Bleed' }, onCaster: true }],
    [1, 'condition', { kind: 'ConditionOutcome', effect: { kind: 'DefenseBuff' }, onCaster: false }]]));
  await presentSpell(page, selfWard, 'decision'); await presentSpell(page, selfWard, 'impact');
  expect((await renderedEffects(page)).map(effect => effect.motif).sort()).toEqual(['bones', 'wound']);
  const changed = { ...frame, creatures: frame.creatures.map(one => ({ ...one, form: 'base-creature' })) };
  await presentSpell(page, changed, 'impact', true);
  expect(await page.evaluate(() => [...window.artScene.figures.values()].every(figure =>
    figure.image.x === figure.home.x && figure.image.y === figure.home.y))).toBe(true);
  expect(errors).toEqual([]);
});


test('ART-4 live playback separates necromantic recipients from the caster blood cost', async ({ page }, info) => {
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
  await expect(page.locator('#arena')).toHaveAttribute('data-playback', 'windup');
  await page.clock.runFor(460);
  await expect(page.locator('[data-creature="1"] .creature-effect')).toContainText('Damage 2');
  await expect(page.locator('[data-creature="4"] .creature-effect')).toContainText('Bleed 2');
  await expect(page.locator('[data-creature="5"] .creature-effect')).toContainText('Bleed 2');
  await expect(page.locator('[data-creature="6"] .creature-effect')).toBeEmpty();
  await fits(page); await page.screenshot({ path: info.outputPath('art4-live-necromancy.png') });
  await page.locator('#playback-skip').click();
  await expect(page.locator('#arena')).toHaveAttribute('data-playback', 'decision');
  expect(h.errors).toEqual([]);
});
