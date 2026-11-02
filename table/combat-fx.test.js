import test from 'node:test';
import assert from 'node:assert/strict';
import { combatChanges, combatSnapshot, createCombatTransitionTracker } from './combat-fx.js';

const board = (health, defense = 0, alive = true) => ({
  allies: [{ id: 'A', health, energy: 2, totalDefense: defense, isAlive: alive }],
  enemies: [{ id: 'B', health: 10, energy: 3, totalDefense: 0, isAlive: true }],
});

test('computes immutable changes for observed combat values', () => {
  const before = combatSnapshot(board(10, 3));
  const changes = combatChanges(before, combatSnapshot(board(6, 1, false)));
  assert.deepEqual(changes, [{ creature: 'A', health: -4, defense: -2, defeated: true }]);
  assert.equal(before.get('A').health, 10);
});

test('first snapshot and repeated event never produce duplicate effects', () => {
  const tracker = createCombatTransitionTracker();
  assert.deepEqual(tracker.observe({ contextKey: 'seat1/round1', confirmedEventId: 1, board: board(10) }), []);
  assert.deepEqual(tracker.observe({ contextKey: 'seat1/round1', confirmedEventId: 1, board: board(7) }), []);
  assert.deepEqual(tracker.observe({ contextKey: 'seat1/round1', confirmedEventId: 2, board: board(7) }), [{ creature: 'A', health: -3 }]);
  assert.deepEqual(tracker.observe({ contextKey: 'seat1/round1', confirmedEventId: 2, board: board(7) }), []);
});

test('local updates without a confirmed event never animate', () => {
  const tracker = createCombatTransitionTracker();
  tracker.observe({ contextKey: 's/r', confirmedEventId: 1, board: board(10) });
  assert.deepEqual(tracker.observe({ contextKey: 's/r', confirmedEventId: null, board: board(3) }), []);
  assert.deepEqual(tracker.observe({ contextKey: 's/r', confirmedEventId: 2, board: board(3) }), [{ creature: 'A', health: -7 }]);
});

test('context changes and resets start a fresh baseline', () => {
  const tracker = createCombatTransitionTracker();
  tracker.observe({ contextKey: 'seat1/live/round1', confirmedEventId: 1, board: board(10) });
  assert.deepEqual(tracker.observe({ contextKey: 'seat1/replay/round1', confirmedEventId: 3, board: board(3) }), []);
  tracker.reset();
  assert.deepEqual(tracker.observe({ contextKey: 'seat1/replay/round1', confirmedEventId: 4, board: board(1) }), []);
});

test('missing or newly spawned actors do not synthesize damage', () => {
  const before = combatSnapshot({ allies: [{ id: 'A', health: 5 }] });
  const after = combatSnapshot({ allies: [{ id: 'B', health: 1 }] });
  assert.deepEqual(combatChanges(before, after), []);
});


test('detects a newly applied stun only once from a confirmed event', () => {
  const tracker = createCombatTransitionTracker();
  const make = stunned => ({
    allies: [{ id: 'A', health: 9, energy: 2, totalDefense: 0, isAlive: true, isStunned: stunned }],
    enemies: [],
  });
  tracker.observe({ contextKey: 'seat1/round2', confirmedEventId: 5, board: make(false) });
  assert.deepEqual(tracker.observe({ contextKey: 'seat1/round2', confirmedEventId: 6, board: make(true) }), [
    { creature: 'A', stunned: true },
  ]);
  assert.deepEqual(tracker.observe({ contextKey: 'seat1/round2', confirmedEventId: 6, board: make(true) }), []);
});
