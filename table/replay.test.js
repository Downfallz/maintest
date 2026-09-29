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
