import test from 'node:test';
import assert from 'node:assert/strict';
import { initialLabState, labTransition, createLabPlayback } from './arena-lab-state.js';
import { sampleFrame } from './arena-lab-fixture.js';

const move = (state, type, id) => labTransition(state, { type, id });
const targeting = () => move(initialLabState(), 'choose');
const playing = target => move(move(targeting(), 'target', target), 'play');

test('a preview cannot play without an offered target or target a friendly creature', () => {
  const state = targeting();
  assert.equal(move(state, 'play'), state);
  assert.equal(move(state, 'target', 'a2'), state);
  assert.equal(move(state, 'target', 'unknown'), state);
  assert.equal(move(initialLabState(), 'target', 'b1').target, null);
  assert.equal(sampleFrame('unknown'), null);
});

test('choosing and cancelling only changes presentation, not the board', () => {
  const start = initialLabState();
  const selected = move(move(start, 'choose'), 'target', 'b2');
  assert.equal(selected.board, start.board);
  const cancelled = move(selected, 'cancel');
  assert.equal(cancelled.stage, 'decision');
  assert.equal(cancelled.target, null);
  assert.equal(cancelled.board, start.board);
});

test('impact uses the authored after frame and never changes its before state', () => {
  const start = playing('b1');
  const impacted = move(start, 'impact');
  assert.equal(start.board.enemies[0].health, 18);
  assert.equal(impacted.board.enemies[0].health, 14);
  assert.equal(impacted.board.allies[0].energy, 2);
  assert.deepEqual(impacted.changes, [{ creature: 'a1', energy: -2 }, { creature: 'b1', health: -4 }]);
  assert.equal(move(impacted, 'impact'), impacted);
  assert.equal(sampleFrame('b1').before.enemies[0].health, 18);
});

test('skip and normal completion reach the same fixed outcome, including defeat', () => {
  for (const target of ['b1', 'b2', 'b3']) {
    const state = playing(target);
    assert.deepEqual(move(state, 'skip'), move(move(state, 'impact'), 'finish'));
  }
  const final = move(playing('b3'), 'skip');
  assert.equal(final.board.enemies[2].isAlive, false);
  assert.equal(final.changes[1].defeated, true);
  assert.deepEqual(move(final, 'reset'), initialLabState());
});

test('delayed callbacks cannot change a reset or a newer playback', () => {
  const queued = [];
  const cancelled = [];
  const playback = createLabPlayback((callback, delay) => { queued.push({ callback, delay }); return queued.length; }, id => cancelled.push(id));
  const received = [];
  playback.play(action => received.push(action.type));
  playback.cancel();
  queued.forEach(job => job.callback());
  assert.deepEqual(received, []);
  assert.deepEqual(cancelled, [1, 2]);
  playback.play(action => received.push(action.type), { speed: 2 });
  assert.deepEqual(queued.slice(2).map(job => job.delay), [210, 625]);
  queued.forEach(job => job.callback());
  assert.deepEqual(received, ['impact', 'finish']);
});

test('reduced motion applies the final frame synchronously without scheduling animation', () => {
  const playback = createLabPlayback(() => assert.fail('No timer should be scheduled'));
  let state = playing('b2');
  playback.play(action => { state = labTransition(state, action); }, { reducedMotion: true });
  assert.equal(state.stage, 'resolved');
  assert.equal(state.board.enemies[1].health, 5);
});
