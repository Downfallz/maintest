import { test } from 'node:test';
import assert from 'node:assert/strict';
import { bands, cursorOf, rollText, side, withCursor } from './timeline.js';

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

// The two cursors do not advance together: the resolve cursor stays at zero for the whole of RevealAndTarget
// while the reveal cursor walks the strip, so reading the wrong one lights the wrong creature for a sub-phase.
test('the strip points at the slot being revealed while targets are being picked', () => {
  assert.equal(cursorOf({ subPhase: 'RevealAndTarget', revealCursor: 2, resolveCursor: 0 }), 2);
});

test('the strip points at the slot being resolved while actions resolve', () => {
  assert.equal(cursorOf({ subPhase: 'ActionResolution', revealCursor: 4, resolveCursor: 1 }), 1);
});

// The timeline is built before intents are even declared, so for several sub-phases it exists and the round is
// on none of its slots. A lit slot there would be pointing at a creature nobody is playing.
test('the strip points at nothing outside the sub-phases that spend a slot', () => {
  assert.equal(cursorOf({ subPhase: 'IntentSelection', revealCursor: 0, resolveCursor: 0 }), -1);
  assert.equal(cursorOf({ subPhase: 'Evolution', revealCursor: 0, resolveCursor: 0 }), -1);
  assert.equal(cursorOf({ subPhase: 'RevealAndTarget' }), -1);
  assert.equal(cursorOf(undefined), -1);
});

test('a strip pointing at nothing lights no slot', () => {
  const slots = withCursor([{ creature: 1 }, { creature: 2 }], cursorOf({ subPhase: 'Cleanup' }));

  assert.deepEqual(slots.map(slot => slot.isNow), [false, false]);
});

// ADR 0063: the dice are public, and a slot shows the rolls behind its place -- every roll, since a creature the
// other side matched rolled again -- and nothing when it did not roll.
test('a slot reads the rolls behind its place, and nothing when it did not roll', () => {
  const rollOffs = [{ creature: 2, rolls: [17] }, { creature: 4, rolls: [11, 4] }];

  assert.equal(rollText(rollOffs, 2), 'd20 17');
  assert.equal(rollText(rollOffs, 4), 'd20 11 → 4');
  assert.equal(rollText(rollOffs, 1), '');
  assert.equal(rollText(undefined, 1), '');
});
