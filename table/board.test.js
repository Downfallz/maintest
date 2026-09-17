import { test } from 'node:test';
import assert from 'node:assert/strict';
import { chipText, conditionDock, expiresIn, healthShare, healthText, revealedText, statPairs } from './board.js';

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

// A fresh condition skips its first countdown, so it outlives one with the same `remainingRounds` by a round.
// A dock that read the raw field would say the two go at the same time, which is the one thing it must not say.
test('a condition that has not counted down yet is docked a round later than one that has', () => {
  assert.equal(expiresIn({ remainingRounds: 2, isFresh: true }), 3);
  assert.equal(expiresIn({ remainingRounds: 2, isFresh: false }), 2);
  assert.equal(expiresIn({ remainingRounds: 2 }), 2);
  assert.equal(expiresIn({ remainingRounds: null, isFresh: true }), null);
  assert.equal(expiresIn(undefined), null);
});

test('a fresh condition and a stale one with the same rounds left are docked apart', () => {
  const dock = conditionDock([
    { effect: { kind: 'Bleed' }, remainingRounds: 2, isFresh: true },
    { effect: { kind: 'Stun' }, remainingRounds: 2, isFresh: false },
  ]);

  assert.deepEqual(dock.map(group => group.rounds), [2, 3]);
  assert.deepEqual(dock.map(group => group.conditions.map(chipText)), [['Stun'], ['Bleed']]);
});

test('conditions that do expire together are docked together whatever their freshness says', () => {
  const dock = conditionDock([
    { effect: { kind: 'Bleed' }, remainingRounds: 2, isFresh: true },
    { effect: { kind: 'Stun' }, remainingRounds: 3, isFresh: false },
  ]);

  assert.deepEqual(dock.map(group => group.rounds), [3]);
  assert.deepEqual(dock[0].conditions.map(chipText), ['Bleed', 'Stun']);
});

// The reveal is what the next player picks targets against, so it has to be on the screen and not only in the
// payload. The spell is named from the catalogue when the page has it, and by its id when it does not.
test('a revealed action says who is acting, with what, and on whom', () => {
  const cards = new Map([['spell:pummel:v1', { name: 'Pummel' }]]);

  assert.equal(revealedText({ actor: 1, spell: 'spell:pummel:v1', targets: [3] }, cards), '1: Pummel → 3');
  assert.equal(revealedText({ actor: 2, spell: 'spell:pummel:v1', targets: [3, 4] }, cards), '2: Pummel → 3, 4');
});

test('a revealed action the page has no card for still names its spell', () => {
  assert.equal(revealedText({ actor: 1, spell: 'spell:unknown:v1', targets: [2] }, new Map()), '1: spell:unknown:v1 → 2');
});

// A spell with nothing left to hit is revealed with no targets and fizzles: the arrow would point at nothing.
test('a revealed action with no targets is printed without an arrow', () => {
  assert.equal(revealedText({ actor: 1, spell: 'Pummel', targets: [] }, new Map()), '1: Pummel');
  assert.equal(revealedText(undefined, undefined), ': ');
});
