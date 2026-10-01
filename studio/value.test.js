import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { castValue, roundValue, isAttack, standing } from './value.js';

const weights = JSON.parse(readFileSync(new URL('../learning/weights/greedy.json', import.meta.url), 'utf8'));
// Frozen copies, not data/Spells: a tuning pass that moves one of these spells must not fail a test about the
// formula (tune 18 did). learning/tests/test_knobs.py reads the same file, so both readings stay pinned together.
const fixture = JSON.parse(readFileSync(new URL('./value.fixture.json', import.meta.url), 'utf8'));
const spells = Object.fromEntries(fixture.spells.map(document => [document.id, document]));
const round = value => Math.round(value * 100) / 100;

test('the value a round matches what check-knobs prints for the same documents', () => {
  // From `cast_value(document, load_weights()) / _rounds_a_cast(cost)` in learning/src/downfall_learning/knobs.py,
  // on the documents in value.fixture.json. Changing the formula on one side fails that side's test.
  const expected = {
    'spell:protective_slam:v1': [13.53, 9.02], // crit, and an initiative debuff over two rounds
    'spell:full_plate:v1': [5.85, 5.85], // permanent reads as three rounds
    'spell:noxious_cure:v1': [8.39, 8.39], // a debuff aimed at allies is a price
    'spell:summon_minions:v1': [12.4, 8.27], // three targets, and damage on the caster subtracted once
    'spell:meteor:v1': [10.5, 7], // a bleed on every target it reaches
    'spell:pummel:v1': [3.53, 3.53], // one energy costs a whole round, like two
    'spell:restorative_burst:v1': [5.4, 3.6],
  };
  for (const [id, [cast, perRound]] of Object.entries(expected)) {
    assert.equal(round(castValue(spells[id], weights)), cast, id);
    assert.equal(round(roundValue(spells[id], weights)), perRound, id);
  }
});

test('a malformed document reads as nothing rather than throwing', () => {
  assert.equal(castValue({ effects: 3, casterEffects: [null, { kind: 'Unknown', amount: 9 }] }, weights), 0);
  assert.equal(roundValue({}, weights), 0);
  assert.equal(isAttack({ effects: 'Damage' }), false);
});

test('a spell is placed among the same tier on the same side of the attack line', () => {
  const spell = (id, damage, heal = 0) => ({ id, document: { energyCost: 2, effects: [
    ...(damage ? [{ kind: 'Damage', amount: damage }] : []), ...(heal ? [{ kind: 'Heal', amount: heal }] : [])] } });
  const all = [spell('a', 4), spell('b', 6), spell('c', 8), spell('heal', 0, 20), spell('deeper', 30)];
  const levels = new Map([['a', 2], ['b', 2], ['c', 2], ['heal', 2], ['deeper', 3]]);
  const middle = standing(all[1], all, levels, weights);
  assert.deepEqual([middle.low, middle.high, middle.median, middle.place, middle.peers], [4, 8, 6, 'within', 3]);
  assert.equal(standing(all[0], all, levels, weights).place, 'lowest');
  assert.equal(standing(all[3], all, levels, weights).place, 'alone');
  assert.equal(standing(spell('untaught', 5), all, levels, weights).level, null);
});
