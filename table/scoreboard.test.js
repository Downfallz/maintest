import test from 'node:test';
import assert from 'node:assert/strict';
import { matchScoreboard } from './scoreboard.js';

const creature = (id, name, isAlive = true) => ({ id, name, isAlive, acquiredTiers: [] });
const board = (allies, enemies) => ({ allies, enemies });
const cast = (sequence, actor, { outcomes = [], applied = [], before = [], after = [], fizzled = false, critical = false } = {}) => ({
  sequence, event: { kind: 'CombatActionResolved', resolution: { action: { actor, spell: 'spell:strike:v1', targets: [] }, fizzled, isCritical: critical, outcomes },
    appliedOutcomes: applied, frame: { before, after } } });
const damage = (target, amount, extra = {}) => ({ kind: 'DamageOutcome', target, amount, critical: false, onCaster: false, ...extra });
const heal = (target, amount, extra = {}) => ({ kind: 'HealOutcome', target, amount, onCaster: false, ...extra });
const upkeep = (sequence, { bleeds = [], regens = [] }) => ({ sequence, event: { kind: 'OngoingEffectsApplied', bleedTicks: bleeds, regenerationTicks: regens, energyRegenerationTicks: [] } });
const share = (caster, amount) => ({ source: { caster, spell: 'spell:x:v1' }, amount });
const row = (report, id) => report.sides.flatMap(side => side.creatures).find(one => one.id === id);

test('damage and healing count what landed and what the bleeds and regenerations the creature left took and gave', () => {
  const final = board([creature(1, 'Wolf'), creature(2, 'Bear')], [creature(3, 'Ghoul')]);
  const feed = [
    cast(1, 1, { outcomes: [damage(3, 4, { blocked: 2 })], applied: [damage(3, 4)] }),
    cast(2, 2, { applied: [heal(1, 3), damage(2, 2, { onCaster: true })] }),
    upkeep(3, { bleeds: [{ creature: 3, damage: 2, shares: [share(1, 2)] }], regens: [{ creature: 1, healed: 1, shares: [share(2, 1)] }] }),
  ];

  const report = matchScoreboard(feed, final);

  assert.deepEqual([row(report, 1).dealt, row(report, 1).bleed], [6, 2]);
  assert.deepEqual([row(report, 2).healed, row(report, 2).regen, row(report, 2).dealt], [4, 1, 0], 'a recoil on the caster is not damage dealt');
  assert.equal(row(report, 2).taken, 2, 'but it is damage taken');
  assert.deepEqual([row(report, 3).taken, row(report, 3).blocked], [6, 2]);
  assert.deepEqual(report.sides.map(side => side.totals.dealt), [6, 0]);
});

test('a bleed a spell put on its own caster is damage taken, never damage dealt', () => {
  const report = matchScoreboard([upkeep(1, { bleeds: [{ creature: 1, damage: 4, shares: [share(1, 4)] }] })], board([creature(1, 'Specter')], [creature(3, 'Ghoul')]));

  assert.deepEqual([row(report, 1).dealt, row(report, 1).bleed, row(report, 1).taken], [0, 0, 4]);
});

test('a hit the defense took whole counts as blocked though nothing landed', () => {
  const report = matchScoreboard([cast(1, 3, { outcomes: [damage(1, 0, { blocked: 3 })] })], board([creature(1, 'Wolf')], [creature(3, 'Ghoul')]));

  assert.equal(row(report, 1).blocked, 3);
  assert.equal(row(report, 1).taken, 0);
});

test('a kill goes to the cast that left its target dead, and an upkeep death to the largest bleed on it', () => {
  const final = board([creature(1, 'Wolf'), creature(2, 'Bear', false)], [creature(3, 'Ghoul', false), creature(4, 'Wraith', false)]);
  const feed = [
    cast(1, 1, { applied: [damage(3, 9)], before: [creature(3, 'Ghoul')], after: [creature(3, 'Ghoul', false)] }),
    upkeep(2, { bleeds: [{ creature: 4, damage: 3, shares: [share(1, 1), share(2, 2)] }] }),
    cast(3, 4, { applied: [damage(2, 9)], before: [creature(4, 'Wraith')], after: [creature(2, 'Bear', false)] }),
  ];

  const report = matchScoreboard(feed, final);

  assert.equal(row(report, 1).kills, 1);
  assert.equal(row(report, 2).kills, 1, 'the Wraith died at the upkeep, where the Bear\'s bleed was the larger share');
  assert.equal(row(report, 4).kills, 1);
  assert.deepEqual(report.sides.map(side => side.creatures.map(one => one.alive)), [[true, false], [false, false]]);
});

test('a creature that falls to its own side is nobody\'s kill', () => {
  const final = board([creature(1, 'Wolf'), creature(2, 'Bear', false)], [creature(3, 'Ghoul')]);

  const report = matchScoreboard([cast(1, 1, { applied: [damage(2, 9)], after: [creature(2, 'Bear', false)] })], final);

  assert.equal(row(report, 1).kills, 0);
});

test('casts and criticals count what resolved, and a fizzle counts nothing', () => {
  const report = matchScoreboard([cast(1, 1, { critical: true }), cast(2, 1), cast(3, 1, { fizzled: true })], board([creature(1, 'Wolf')], [creature(3, 'Ghoul')]));

  assert.deepEqual([row(report, 1).casts, row(report, 1).crits], [2, 1]);
});

test('the creature of the match is the one that dealt the most, and there is none before anything resolved', () => {
  const final = board([creature(1, 'Wolf'), creature(2, 'Bear')], [creature(3, 'Ghoul')]);
  const report = matchScoreboard([cast(1, 2, { applied: [damage(3, 5)] }), cast(2, 1, { applied: [damage(3, 2)] })], final);

  assert.equal(report.mvp, 2);
  assert.equal(report.top.dealt, 5);
  assert.equal(matchScoreboard([], final).mvp, null);
  assert.equal(matchScoreboard(null, final), null);
});

test('a creature is named as the board names it, never by the host\'s raw name', () => {
  const report = matchScoreboard([], board([creature(1, 'Main'), creature(2, 'Main')], [creature(4, 'Main')]));

  assert.deepEqual(report.sides.flatMap(side => side.creatures.map(one => one.name)), ['Creature 1', 'Creature 2', 'Creature 4']);
});

test('each creature is drawn as it ended the match: the illustration its mutations gave it, the base one without', () => {
  const packages = new Map([['tier:brute:v1', { id: 'tier:brute:v1', level: 1, prerequisites: [], anyOf: [], visual: { family: 'brute', artwork: 'brute-base' } }]]);
  const mutated = { ...creature(1, 'Wolf'), acquiredTiers: ['tier:brute:v1'] };

  const report = matchScoreboard([], board([mutated], [creature(3, 'Ghoul')]), packages);

  assert.match(row(report, 1).artwork, /brute/);
  assert.equal(row(report, 3).artwork, 'evolutions/base-creature.webp');
});
