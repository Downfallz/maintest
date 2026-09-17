import { test } from 'node:test';
import assert from 'node:assert/strict';
import { chipText, conditionDock, healthShare, healthText, statPairs } from './board.js';

const creature = { id: 1, health: 14, maxHealth: 20, energy: 2, totalDefense: 3, currentInitiative: 7 };

test('a board reads its health as a number, not as a track', () => {
  assert.equal(healthText(creature), '14/20');
  assert.equal(healthShare(creature), 0.7);
});

test('a creature at zero has an empty bar rather than none, because dead is a state to see', () => {
  assert.equal(healthText({ health: 0, maxHealth: 20 }), '0/20');
  assert.equal(healthShare({ health: 0, maxHealth: 20 }), 0);
});

test('a bar cannot overflow or go negative whatever the payload says', () => {
  assert.equal(healthShare({ health: 30, maxHealth: 20 }), 1);
  assert.equal(healthShare({ health: -5, maxHealth: 20 }), 0);
  assert.equal(healthShare({ health: 5, maxHealth: 0 }), 0);
  assert.equal(healthShare(undefined), 0);
});

test('the stats a board carries are the printed ones, in the printed order', () => {
  assert.deepEqual(statPairs(creature), [['energy', 2], ['defense', 3], ['initiative', 7]]);
});

// The question a player asks of a dock is "what goes away at the end of this round", so that is what it is
// grouped by. Permanent is its own group and it is last: it answers the opposite question.
test('the dock groups conditions by what is left of them, soonest first', () => {
  const dock = conditionDock([
    { effect: { kind: 'Bleed' }, remainingRounds: 2 },
    { effect: { kind: 'Stun' }, remainingRounds: 1 },
    { effect: { kind: 'DefenseBuff' }, remainingRounds: null },
    { effect: { kind: 'Regeneration' }, remainingRounds: 2 },
  ]);

  assert.deepEqual(dock.map(group => group.rounds), [1, 2, null]);
  assert.deepEqual(dock.map(group => group.conditions.length), [1, 2, 1]);
});

test('a permanent condition is grouped even when it is the only one', () => {
  const dock = conditionDock([{ effect: { kind: 'DefenseBuff' }, remainingRounds: null }]);

  assert.deepEqual(dock.map(group => group.rounds), [null]);
});

test('a creature with nothing on it has an empty dock', () => {
  assert.deepEqual(conditionDock([]), []);
  assert.deepEqual(conditionDock(undefined), []);
});

// The chip says the kind the host named; the page has never heard of any of them.
test('a chip prints the kind it was given and nothing it made up', () => {
  assert.equal(chipText({ effect: { kind: 'Bleed' } }), 'Bleed');
  assert.equal(chipText({ effect: {} }), '');
  assert.equal(chipText(undefined), '');
});
