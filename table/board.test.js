import { test } from 'node:test';
import assert from 'node:assert/strict';
import { chipSource, chipText, conditionDock, healthShare, healthText, laneOf, revealedText, standing, statPairs, targetedBy } from './board.js';

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

// The printed dock has four lanes -- `new`, `3`, `2`, `1` -- and the `new` lane is the geometry that makes
// "the first countdown does not count" visible instead of remembered (components.md §3.2).
test('a condition whose first countdown is still ahead of it sits in the new lane', () => {
  assert.equal(laneOf({ remainingRounds: 3, isFresh: true }), 'new');
  assert.equal(laneOf({ remainingRounds: 3, isFresh: false }), 3);
  assert.equal(laneOf({ remainingRounds: 1 }), 1);
  assert.equal(laneOf({ remainingRounds: null, isFresh: false }), null);
  assert.equal(laneOf(undefined), null);
});

test('the dock reads new first, then the numbered lanes longest first, then permanent', () => {
  const dock = conditionDock([
    { effect: { kind: 'Bleed' }, remainingRounds: 1 },
    { effect: { kind: 'DefenseBuff' }, remainingRounds: null },
    { effect: { kind: 'Regeneration' }, remainingRounds: 3, isFresh: true },
    { effect: { kind: 'Stun' }, remainingRounds: 3 },
  ]);

  assert.deepEqual(dock.map(group => group.lane), ['new', 3, 1, null]);
  assert.deepEqual(dock.map(group => group.conditions.map(chipText)), [['Regeneration'], ['Stun'], ['Bleed'], ['DefenseBuff']]);
});

// A fresh condition outlives a counted one of the same number by a round, so sharing a lane would be a round
// out. The lanes keep them apart without the page doing arithmetic on the player's behalf.
test('a fresh condition and a counted one with the same rounds left are never in one lane', () => {
  const dock = conditionDock([
    { effect: { kind: 'Bleed' }, remainingRounds: 2, isFresh: true },
    { effect: { kind: 'Stun' }, remainingRounds: 2, isFresh: false },
  ]);

  assert.deepEqual(dock.map(group => group.lane), ['new', 2]);
});

test('only the lanes that hold something are drawn, because a phone has no room for empty boxes', () => {
  assert.deepEqual(conditionDock([{ effect: { kind: 'Stun' }, remainingRounds: 1 }]).map(group => group.lane), [1]);
  assert.deepEqual(conditionDock([]), []);
  assert.deepEqual(conditionDock(undefined), []);
});

// Two Bleeds of different sizes are two different things to plan around, and which cast put one there is whose
// upkeep it is (ADR 0027). A chip that printed only the kind would be a chip a player cannot use.
test('a chip carries the number its effect holds, whatever that field is called', () => {
  assert.equal(chipText({ effect: { kind: 'Bleed', amountPerRound: 2 } }), 'Bleed 2');
  assert.equal(chipText({ effect: { kind: 'DefenseBuff', amount: 3 } }), 'DefenseBuff 3');
  assert.equal(chipText({ effect: { kind: 'Stun' } }), 'Stun');
  assert.equal(chipText({ effect: {} }), '');
  assert.equal(chipText(undefined), '');
});

// The source is the pair, not the caster: one creature can put two conditions of one kind on one target from
// two different spells, and then the caster alone tells a player nothing that distinguishes them.
test('a chip names the cast that put it there, caster and spell both', () => {
  const cards = new Map([['spell:healing_screech:v1', { name: 'Healing Screech' }]]);

  assert.equal(chipSource({ source: { caster: 5, spell: 'spell:healing_screech:v1' } }, cards), 'from 5 · Healing Screech');
  assert.equal(chipSource({ source: { caster: 5, spell: 'spell:unknown:v1' } }, cards), 'from 5 · spell:unknown:v1');
  assert.equal(chipSource({ source: { caster: 5 } }, cards), 'from 5');
  assert.equal(chipSource({ source: null }, cards), '');
  assert.equal(chipSource(undefined, cards), '');
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

// Reveal-and-target walks the whole timeline before anything resolves, so every cast's markers are on the
// table at once and "who is pointing at me" is what a player reads before choosing their own.
test('a creature row names every caster already pointing at it', () => {
  const board = {
    resolveCursor: 0,
    timeline: [{ creature: 1 }, { creature: 2 }, { creature: 3 }],
    revealedActions: [
      { actor: 1, spell: 's', targets: [4, 5] },
      { actor: 2, spell: 's', targets: [4] },
      { actor: 3, spell: 's', targets: [6] },
    ],
  };

  assert.deepEqual(targetedBy(4, board), [1, 2]);
  assert.deepEqual(targetedBy(5, board), [1]);
  assert.deepEqual(targetedBy(6, board), [3]);
});

test('a creature nothing points at has no markers, and neither does an empty board', () => {
  const board = { resolveCursor: 0, timeline: [{ creature: 1 }], revealedActions: [{ actor: 1, spell: 's', targets: [4] }] };

  assert.deepEqual(targetedBy(9, board), []);
  assert.deepEqual(targetedBy(4, { resolveCursor: 0, timeline: [], revealedActions: [] }), []);
  assert.deepEqual(targetedBy(4, undefined), []);
});

// A cast with nothing left to hit is revealed with no targets: it points at nobody rather than at everybody.
test('a revealed cast with no targets points at nothing', () => {
  const board = { resolveCursor: 0, timeline: [{ creature: 1 }] };

  assert.deepEqual(targetedBy(4, { ...board, revealedActions: [{ actor: 1, spell: 's', targets: [] }] }), []);
  assert.deepEqual(targetedBy(4, { ...board, revealedActions: [{ actor: 1, spell: 's' }] }), []);
});

// At the table a marker goes on when a cast is revealed and comes off when it resolves. `revealedActions` only
// grows -- it is the reveal cursor's prefix -- so through the whole of resolution the two sets differ, and a
// row that read the raw list would name a caster whose cast is already spent.
test('a marker comes off the row when its cast resolves', () => {
  const board = {
    timeline: [{ creature: 1 }, { creature: 2 }, { creature: 3 }],
    revealedActions: [
      { actor: 1, spell: 's', targets: [4] },
      { actor: 2, spell: 's', targets: [4] },
      { actor: 3, spell: 's', targets: [4] },
    ],
  };

  assert.deepEqual(targetedBy(4, { ...board, resolveCursor: 0 }), [1, 2, 3]);
  assert.deepEqual(targetedBy(4, { ...board, resolveCursor: 1 }), [2, 3]);
  assert.deepEqual(targetedBy(4, { ...board, resolveCursor: 2 }), [3]);
  assert.deepEqual(targetedBy(4, { ...board, resolveCursor: 3 }), []);
});

// No sub-phase is tested for, and none needs to be: the resolve cursor is zero while nothing has resolved and
// past the last slot once everything has, so the standing set is right at both ends of the round.
test('every marker is standing before resolution and none after it', () => {
  const board = {
    timeline: [{ creature: 1 }, { creature: 2 }],
    revealedActions: [{ actor: 1, spell: 's', targets: [4] }, { actor: 2, spell: 's', targets: [4] }],
  };

  assert.equal(standing({ ...board, resolveCursor: 0 }).length, 2);
  assert.equal(standing({ ...board, resolveCursor: 2 }).length, 0);
  assert.equal(standing({ ...board }).length, 2);
  assert.deepEqual(standing(undefined), []);
});
