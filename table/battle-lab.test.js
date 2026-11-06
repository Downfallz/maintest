import test from 'node:test';
import assert from 'node:assert/strict';
import { initialLabState, isLabPlaying, labTransition, createLabPlayback } from './battle-lab-state.js';
import { battleFixture, sampleAction, sampleFrame } from './battle-lab-fixture.js';

const move = (state, type, id) => labTransition(state, { type, id });
const targeting = () => move(initialLabState(), 'choose');
const playing = target => move(move(targeting(), 'target', target), 'play');

test('the fixture offers an ally acting against the enemies, all of them on the board and in the order', () => {
  const board = battleFixture();
  const ids = [...board.allies, ...board.enemies].map(one => one.id);
  assert.ok(board.allies.some(one => one.id === sampleAction.actor));
  for (const target of sampleAction.targets) assert.ok(board.enemies.some(one => one.id === target));
  assert.deepEqual(board.timeline.map(slot => slot.creature).sort(), [...ids].sort());
  assert.equal(board.timeline[0].creature, sampleAction.actor);
});

test('a sample frame changes the target and the actor and nothing else', () => {
  for (const target of sampleAction.targets) {
    const { before, after } = sampleFrame(target);
    assert.deepEqual(before, battleFixture());
    const untouched = one => one.id !== target && one.id !== sampleAction.actor;
    assert.deepEqual([...after.allies, ...after.enemies].filter(untouched), [...before.allies, ...before.enemies].filter(untouched));
    assert.ok(after.enemies.find(one => one.id === target).health < before.enemies.find(one => one.id === target).health);
    assert.ok(after.allies.find(one => one.id === sampleAction.actor).energy < before.allies[0].energy);
  }
});

test('the state carries the one action it offers, and starts by inspecting its actor', () => {
  const state = initialLabState();
  assert.equal(state.action, sampleAction);
  assert.equal(state.inspected, sampleAction.actor);
  assert.equal(state.stage, 'decision');
  assert.equal(isLabPlaying(state), false);
});

test('a preview cannot play without an offered target or target a friendly creature', () => {
  const state = targeting();
  assert.equal(move(state, 'play'), state);
  assert.equal(move(state, 'target', 'a2'), state);
  assert.equal(move(state, 'target', 'unknown'), state);
  assert.equal(move(initialLabState(), 'target', 'b1').target, null);
  assert.equal(sampleFrame('unknown'), null);
});

test('inspecting a creature on the board changes the inspection alone; an unknown one changes nothing', () => {
  const state = initialLabState();
  const inspected = move(state, 'inspect', 'b3');
  assert.equal(inspected.inspected, 'b3');
  assert.deepEqual({ ...inspected, inspected: state.inspected }, state);
  assert.equal(move(state, 'inspect', 'unknown'), state);
});

test('selecting a target also inspects it', () => {
  assert.equal(move(targeting(), 'target', 'b2').inspected, 'b2');
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

test('each move is refused outside its stage, so a late click changes nothing', () => {
  const refused = [
    ['choose', targeting()], ['choose', playing('b1')],
    ['cancel', initialLabState()], ['cancel', playing('b1')],
    ['target', playing('b1'), 'b2'],
    ['play', initialLabState()], ['play', playing('b1')],
    ['impact', initialLabState()], ['impact', targeting()],
    ['finish', initialLabState()], ['finish', playing('b1')],
    ['skip', initialLabState()], ['skip', targeting()],
    ['unknown', initialLabState()],
  ];
  for (const [type, state, id] of refused) assert.equal(move(state, type, id), state, `${type} at ${state.stage}`);
});

test('impact uses the authored after frame and never changes its before state', () => {
  const start = playing('b1');
  const impacted = move(start, 'impact');
  assert.equal(isLabPlaying(start), true);
  assert.equal(isLabPlaying(impacted), true);
  assert.equal(start.board.enemies[0].health, 18);
  assert.equal(impacted.board.enemies[0].health, 14);
  assert.equal(impacted.board.allies[0].energy, 2);
  assert.deepEqual(impacted.changes, [{ creature: 'a1', energy: -2 }, { creature: 'b1', health: -4 }]);
  assert.equal(move(impacted, 'impact'), impacted);
  assert.equal(sampleFrame('b1').before.enemies[0].health, 18);
});

test('skip and normal completion reach the same fixed outcome, including defeat', () => {
  for (const target of sampleAction.targets) {
    const state = playing(target);
    assert.deepEqual(move(state, 'skip'), move(move(state, 'impact'), 'finish'));
    assert.deepEqual(move(move(state, 'impact'), 'skip'), move(move(state, 'impact'), 'finish'));
  }
  const final = move(playing('b3'), 'skip');
  assert.equal(isLabPlaying(final), false);
  assert.equal(final.board.enemies[2].isAlive, false);
  assert.equal(final.changes[1].defeated, true);
  assert.deepEqual(move(final, 'reset'), initialLabState());
});

// A fake clock: every scheduled callback is kept with its delay, and an unscheduled id is remembered.
function clock() {
  const queued = [];
  const cancelled = [];
  const playback = createLabPlayback((callback, delay) => { queued.push({ callback, delay }); return queued.length; }, id => cancelled.push(id));
  return { queued, cancelled, playback, fire: () => queued.forEach(job => job.callback()) };
}

test('a playback schedules the impact then the settled frame, halved at 2× and at 1× for any other speed', () => {
  const { queued, playback } = clock();
  playback.play(() => {});
  playback.play(() => {}, { speed: 2 });
  playback.play(() => {}, { speed: 3 });
  assert.deepEqual(queued.map(job => job.delay), [420, 1250, 210, 625, 420, 1250]);
});

test('delayed callbacks cannot change a reset or a newer playback', () => {
  const { queued, cancelled, playback, fire } = clock();
  const received = [];
  playback.play(action => received.push(action.type));
  playback.cancel();
  fire();
  assert.deepEqual(received, []);
  assert.deepEqual(cancelled, [1, 2]);
  playback.play(action => received.push(action.type), { speed: 2 });
  assert.deepEqual(queued.slice(2).map(job => job.delay), [210, 625]);
  fire();
  assert.deepEqual(received, ['impact', 'finish']);
});

test('a second playback supersedes the first: its timers are unscheduled and its late callbacks ignored', () => {
  const { cancelled, playback, fire } = clock();
  const received = [];
  playback.play(() => received.push('first'));
  playback.play(() => received.push('second'));
  assert.deepEqual(cancelled, [1, 2]);
  fire();
  assert.deepEqual(received, ['second', 'second']);
});

test('the default scheduler is the clock of the moment a cue is posted, not of the playback\'s creation', () => {
  const playback = createLabPlayback();
  const real = { setTimeout: globalThis.setTimeout, clearTimeout: globalThis.clearTimeout };
  const queued = [];
  const cleared = [];
  globalThis.setTimeout = (callback, delay) => queued.push(delay);
  globalThis.clearTimeout = id => cleared.push(id);
  try {
    playback.play(() => {});
    playback.cancel();
  } finally {
    Object.assign(globalThis, real);
  }
  assert.deepEqual(queued, [420, 1250]);
  assert.deepEqual(cleared, [1, 2]);
});

test('reduced motion applies the final frame synchronously without scheduling animation', () => {
  const playback = createLabPlayback(() => assert.fail('No timer should be scheduled'));
  let state = playing('b2');
  playback.play(action => { state = labTransition(state, action); }, { reducedMotion: true });
  assert.equal(state.stage, 'resolved');
  assert.equal(state.board.enemies[1].health, 5);
});
