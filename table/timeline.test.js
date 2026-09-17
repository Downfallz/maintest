import { test } from 'node:test';
import assert from 'node:assert/strict';
import { bands, side, withCursor } from './timeline.js';

const timeline = [
  { owner: 'Player1', creature: 2, speed: 'Quick', initiative: 6 },
  { owner: 'Player2', creature: 4, speed: 'Quick', initiative: 5 },
  { owner: 'Player1', creature: 1, speed: 'Standard', initiative: 9 },
  { owner: 'Player2', creature: 3, speed: 'Standard', initiative: 5 },
];

// The one that matters: the engine's order is the order played, ties and all. A strip that sorted would be
// showing an order nobody plays.
test('the strip keeps the order the engine built, and never sorts it', () => {
  const strip = bands(timeline);

  assert.deepEqual(strip.map(band => band.speed), ['Quick', 'Standard']);
  assert.deepEqual(strip[0].slots.map(slot => slot.creature), [2, 4]);
  assert.deepEqual(strip[1].slots.map(slot => slot.creature), [1, 3]);
});

test('the bands appear in the order the timeline first names them', () => {
  const standardFirst = bands([
    { creature: 1, speed: 'Standard' },
    { creature: 2, speed: 'Quick' },
  ]);

  assert.deepEqual(standardFirst.map(band => band.speed), ['Standard', 'Quick']);
});

test('a round with no timeline yet is an empty strip', () => {
  assert.deepEqual(bands([]), []);
  assert.deepEqual(bands(undefined), []);
});

// The cursor is an index over the whole strip, not within a band: the engine counts it that way.
test('the slot the round is on is marked, and only that one', () => {
  const marked = withCursor(timeline, 2);

  assert.deepEqual(marked.map(slot => slot.isNow), [false, false, true, false]);
});

test('a cursor past the end marks nothing', () => {
  assert.equal(withCursor(timeline, 9).filter(slot => slot.isNow).length, 0);
  assert.equal(withCursor(undefined, 0).length, 0);
});

test('a slot is ours or theirs, from the seat reading it', () => {
  assert.equal(side(timeline[0], 'Player1'), 'ally');
  assert.equal(side(timeline[1], 'Player1'), 'enemy');
  assert.equal(side(undefined, 'Player1'), 'enemy');
});
