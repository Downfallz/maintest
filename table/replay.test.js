import { test } from 'node:test';
import assert from 'node:assert/strict';
import { healthChange, playbackBoard } from './replay.js';

test('an action changes health by the difference between its boundary snapshots', () => {
  const action = { frame: { before: [{ id: 1, health: 20 }, { id: 2, health: 12 }] } };
  assert.equal(healthChange(action, { id: 1, health: 17 }), -3);
  assert.equal(healthChange(action, { id: 2, health: 16 }), 4);
  assert.equal(healthChange(action, { id: 2, health: 12 }), 0);
});

test('an action without a snapshot of the creature changes nothing it can show', () => {
  assert.equal(healthChange({ frame: { before: [] } }, { id: 1, health: 17 }), 0);
  assert.equal(healthChange(undefined, { id: 1, health: 17 }), 0);
  assert.equal(healthChange({ frame: { before: [{ id: 1 }] } }, { id: 1, health: 17 }), 0);
  assert.equal(healthChange({ frame: {} }, { id: 1, health: 17 }), 0);
});

test('a replayed board points at the slot its action was played from, not its place among the resolutions', () => {
  const timeline = [{ creature: 4 }, { creature: 5 }, { creature: 2 }];
  const action = { actor: { id: 2 }, frame: { before: [], after: [], timeline, rollOffs: [] } };
  assert.equal(playbackBoard({ actions: [action], index: 0, stage: 'after', round: 1 }, { allies: [] }).activationCursor, 2);
});

test('a replay across rounds shows the action on its own round with only that round revealed', () => {
  const frame = { before: [], after: [], timeline: [], rollOffs: [] };
  const actions = [
    { round: 1, actor: { id: 1 }, action: { actor: 1 }, frame },
    { round: 2, actor: { id: 2 }, action: { actor: 2 }, frame },
    { round: 2, actor: { id: 1 }, action: { actor: 1 }, frame },
  ];

  const board = playbackBoard({ actions, index: 2, stage: 'after', round: 1 }, { allies: [] });

  assert.equal(board.roundNumber, 2);
  assert.deepEqual(board.revealedActions, [{ actor: 2 }, { actor: 1 }]);
  assert.equal(board.activationCursor, 1);
});

test('a round upkeep in a replay is shown before its combat, with nothing turned over', () => {
  const frame = { before: [], after: [], timeline: [], rollOffs: [] };
  const actions = [
    { round: 1, actor: { id: 1 }, action: { actor: 1 }, frame },
    { round: 2, interlude: 'upkeep', ticks: [], frame },
  ];

  const board = playbackBoard({ actions, index: 1, stage: 'after', round: 1 }, { allies: [] });

  assert.equal(board.roundNumber, 2);
  assert.equal(board.subPhase, 'Round start');
  assert.deepEqual(board.revealedActions, []);
});

test('a live cast keeps the round\'s earlier reveals from the latest board, up to its own slot', () => {
  // The live playback hands one action at a time: the slots already played stay turned over (UX12 P7's strip).
  const timeline = [{ creature: 4 }, { creature: 1 }, { creature: 2 }, { creature: 5 }];
  const action = { round: 3, actor: { id: 2 }, action: { actor: 2, spell: 'ward' }, frame: { before: [], after: [], timeline, rollOffs: [] } };
  const latest = { roundNumber: 3, allies: [], revealedActions: [{ actor: 4, spell: 'hit' }, { actor: 1, spell: 'focus' }, { actor: 2, spell: 'ward' }, { actor: 5, spell: 'later' }] };

  const board = playbackBoard({ actions: [action], index: 0, stage: 'before', round: 3 }, latest);

  assert.deepEqual(board.revealedActions, [{ actor: 4, spell: 'hit' }, { actor: 1, spell: 'focus' }, { actor: 2, spell: 'ward' }]);
});

test('a cast of an earlier round takes no reveal from the latest board of another round', () => {
  const timeline = [{ creature: 4 }, { creature: 2 }];
  const action = { round: 2, actor: { id: 2 }, action: { actor: 2 }, frame: { before: [], after: [], timeline, rollOffs: [] } };
  const latest = { roundNumber: 3, allies: [], revealedActions: [{ actor: 4, spell: 'hit' }] };

  assert.deepEqual(playbackBoard({ actions: [action], index: 0, stage: 'after', round: 2 }, latest).revealedActions, [{ actor: 2 }]);
});
