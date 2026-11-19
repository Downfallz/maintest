// The movement's numbers (docs/art/storyboards/movement.md, BC5): read from the host's confirmed outcome, and
// held inside the windows the queue plays, at every playback rate.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { DRAW_MS, STRIKE_MS, WINDUP_MS, IMPACT_MS, RETURN_MS, RETURN_DELAY_MS, playbackRate } from './battle-timing.js';
import { readFileSync } from 'node:fs';

import { ACTION_END_MS, BONE, BREATH, DRAINED, CAST, DEATH, FLASH_MS, FLIGHT, PUNCH, THROW, TRAUMA, WEIGHTS, blowWeight, breath, flightAt, lastBeat, lossOf,
  meleeBeats, punches, reaction, shakeOf, traumaAfter, traumaOf, wander, weightOf } from './battle-motion.js';

const cue = (changes, extra = {}) => ({ recipients: changes.map(one => one.creature), changes, ...extra });

test('a blow is light to 3 health lost, medium to 6, heavy from 7 or on a defeat', () => {
  assert.deepEqual([0, 3, 4, 6, 7, 11].map(lost => weightOf({ lost })), ['light', 'light', 'medium', 'medium', 'heavy', 'heavy']);
  assert.equal(weightOf({ lost: 1, defeated: true }), 'heavy');
  assert.equal(weightOf(), 'light');
});

test('a creature\'s loss is what the cue\'s changes say: health gained, or nothing said, is no loss', () => {
  const action = cue([{ creature: 'b1', health: -5 }, { creature: 'b2', health: 3 }, { creature: 'b3', defeated: true }, { creature: 4, health: -2 }]);
  assert.deepEqual(lossOf(action, 'b1'), { lost: 5, defeated: false });
  assert.deepEqual(lossOf(action, 'b2'), { lost: 0, defeated: false });
  assert.deepEqual(lossOf(action, 'b3'), { lost: 0, defeated: true });
  // The live page's changes carry the id as the host wrote it; the scene's figures are named by a string.
  assert.deepEqual(lossOf(action, '4'), { lost: 2, defeated: false });
  assert.deepEqual(lossOf(action, 'a1'), { lost: 0, defeated: false });
  assert.deepEqual(lossOf(undefined, 'a1'), { lost: 0, defeated: false });
});

test('the weight of a blow is its heaviest loss, and the caster\'s own cost is not counted', () => {
  assert.equal(blowWeight(cue([{ creature: 'b1', health: -2 }, { creature: 'b2', health: -5 }]), 'a1'), 'medium');
  assert.equal(blowWeight(cue([{ creature: 'a1', health: -9 }, { creature: 'b1', health: -2 }]), 'a1'), 'light');
  assert.equal(blowWeight(cue([]), 'a1'), 'light');
  assert.equal(blowWeight(undefined, 'a1'), 'light');
});

test('a light blow keeps the draw back and the strike the queue was timed on, and a heavier one keeps their sum', () => {
  const light = meleeBeats('light');
  assert.deepEqual([light.draw.lasts, light.strike.at, light.strike.lasts], [DRAW_MS, DRAW_MS, STRIKE_MS]);
  assert.equal(light.home.at - WINDUP_MS, RETURN_DELAY_MS);
  for (const weight of Object.keys(WEIGHTS)) {
    const beats = meleeBeats(weight);
    assert.equal(beats.strike.at + beats.strike.lasts, WINDUP_MS, `${weight}: the strike lands when the windup ends`);
    assert.equal(beats.stretch.at + beats.stretch.lasts, WINDUP_MS, `${weight}: the stretch peaks on the contact`);
    assert.ok(beats.strike.at >= beats.draw.at + beats.draw.lasts, `${weight}: the strike follows the draw back`);
    assert.equal(beats.home.lasts, RETURN_MS);
  }
  assert.ok(meleeBeats('heavy').draw.distance > meleeBeats('medium').draw.distance && meleeBeats('medium').draw.distance > light.draw.distance);
  assert.ok(meleeBeats('heavy').strike.lasts < light.strike.lasts, 'a heavy blow is wound up longer and lands faster');
  assert.deepEqual(meleeBeats('unheard of'), light);
});

test('every beat of the movement ends inside its window', () => {
  // At 1×. The queue divides its windows by the playback rate, so the beats fit at every rate as long as the
  // scene divides each of them by the same rate: the next test holds it to that.
  for (const weight of Object.keys(WEIGHTS)) assert.ok(lastBeat(weight) <= ACTION_END_MS, weight);
  assert.equal(ACTION_END_MS, WINDUP_MS + IMPACT_MS);
  assert.equal(CAST.rise.at + CAST.rise.lasts, WINDUP_MS);
  assert.equal(CAST.load.at + CAST.load.lasts, CAST.rise.at);
  assert.equal(THROW.home.at + THROW.home.lasts, WINDUP_MS, 'the thrower is home when its projectile lands');
  assert.equal(THROW.toss.at, DRAW_MS, 'the thrower lets go when the projectile leaves');
  assert.ok(WINDUP_MS + DEATH.settle.at + DEATH.settle.lasts <= ACTION_END_MS);
  assert.equal(DEATH.fall.at + DEATH.fall.lasts, DEATH.settle.at);
});

test('every duration the body plays is divided by the playback rate', () => {
  // A tween that forgot the rate would land after the impact at 2× and before it at 0.5×.
  const source = readFileSync(new URL('../battle/src/body-motion.ts', import.meta.url), 'utf8');
  const timed = [...source.matchAll(/\b(duration|delay|hold): ([^,}\n]+)/g)];
  assert.ok(timed.length >= 20, `${timed.length} durations read`);
  for (const [, key, value] of timed.filter(([, , value]) => value !== 'number')) assert.match(value, /\/ rate\b|^ms\b/, `${key}: ${value}`);
  for (const rate of [0.5, 1, 1.5, 2]) assert.equal(playbackRate(rate), rate);
});

test('the flash is the palette\'s bone and a fallen figure drains toward its line colour', () => {
  const palette = readFileSync(new URL('../docs/art/branding.css', import.meta.url), 'utf8');
  const colour = name => Number.parseInt(new RegExp(`--brand-${name}: #([0-9a-f]{6});`).exec(palette)[1], 16);
  assert.equal(BONE, colour('bone'));
  assert.equal(DRAINED, colour('line'));
  assert.notEqual(BONE, colour('gold'));
});

test('a struck figure flashes only when it lost health, and is thrown further the more it lost', () => {
  assert.equal(reaction({ lost: 0 }, true).flash, 0);
  assert.equal(reaction({ lost: 1 }, true).flash, FLASH_MS);
  assert.equal(reaction({ lost: 0, defeated: true }, false).flash, FLASH_MS);
  const [light, medium, heavy] = [2, 5, 8].map(lost => reaction({ lost }, true));
  assert.deepEqual([light.stop, medium.stop, heavy.stop], [60, 90, 120]);
  assert.deepEqual([light.recoil.distance, medium.recoil.distance, heavy.recoil.distance], [12, 18, 26]);
  // The recoil waits for the stop to end.
  for (const one of [light, medium, heavy]) assert.equal(one.recoil.at, one.stop);
  // From a distance the stop is half as long and the push the same.
  const far = reaction({ lost: 8 }, false);
  assert.deepEqual([far.stop, far.tremor.distance, far.recoil.distance], [60, 1, 26]);
  // The tremor's swings fill the stop and end with it, for every weight, a body's blow or not: a tremor that ran
  // on would fight the recoil for the figure's place.
  for (const lost of [0, 2, 5, 8]) for (const melee of [true, false]) {
    const { stop, tremor } = reaction({ lost }, melee);
    assert.ok(Math.abs(tremor.swings * tremor.swing * 2 - stop) < 1e-9, `${lost} lost, melee ${melee}: ${tremor.swings} swings of ${tremor.swing} ms in ${stop} ms`);
    assert.ok(tremor.swings >= 1 && tremor.swing > 0);
  }
});

test('the breath moves the height by 1.5% over 3600 ms, and no two neighbours rise together', () => {
  const heights = Array.from({ length: 360 }, (_, step) => breath(step * 10));
  assert.ok(Math.abs(Math.max(...heights) - Math.min(...heights) - BREATH.amount * 2) < 1e-4);
  assert.ok(Math.abs(BREATH.amount * 2 - .015) < 1e-9);
  assert.ok(Math.abs(breath(0) - breath(BREATH.period)) < 1e-9);
  const peaks = [0, 1, 2, 3, 4, 5].map(place => Array.from({ length: 360 }, (_, step) => breath(step * 10, place)).indexOf(Math.max(...Array.from({ length: 360 }, (_, step) => breath(step * 10, place)))));
  assert.equal(new Set(peaks).size, 6);
});

test('the view trembles by the health lost: a defeat adds more, and a cost, a heal or a blow that took nothing adds none', () => {
  assert.equal(traumaOf(cue([{ creature: 'b1', health: -3 }]), 'a1'), .25);
  assert.equal(traumaOf(cue([{ creature: 'b1', health: -5 }]), 'a1'), .40);
  assert.equal(traumaOf(cue([{ creature: 'b1', health: -8 }]), 'a1'), .55);
  assert.equal(traumaOf(cue([{ creature: 'b1', health: -8, defeated: true }]), 'a1'), TRAUMA.ceiling);
  assert.equal(traumaOf(cue([{ creature: 'b1', health: -3 }, { creature: 'b2', health: -3 }, { creature: 'b3', health: -3 }]), 'a1'), TRAUMA.ceiling);
  // A blood cost alone, a heal, a ward, contact with no loss, a fizzle.
  assert.equal(traumaOf(cue([{ creature: 'a1', health: -4 }]), 'a1'), 0);
  assert.equal(traumaOf(cue([{ creature: 'a2', health: 4 }]), 'a1'), 0);
  assert.equal(traumaOf(cue([{ creature: 'b1', energy: -2 }]), 'a1'), 0);
  assert.equal(traumaOf({ recipients: [], changes: [] }, 'a1'), 0);
  assert.equal(traumaOf(undefined, 'a1'), 0);
});

test('trauma falls at a steady rate to nothing, and its shake is its square', () => {
  assert.ok(Math.abs(traumaAfter(.6, 120) - .35) < 1e-9);
  assert.equal(traumaAfter(.6, 288), 0);
  assert.equal(traumaAfter(.25, 5000), 0);
  assert.ok(Math.abs(shakeOf(.25) - .0625) < 1e-9);
  for (const ms of [0, 17, 840, 12345]) {
    const { x, y } = wander(ms);
    assert.ok(Math.abs(x) <= 1 && Math.abs(y) <= 1);
    assert.deepEqual(wander(ms), { x, y }, 'the same time gives the same offset');
  }
});

test('the zoom punches on a defeat or a critical hit only', () => {
  assert.equal(punches(cue([{ creature: 'b1', health: -9 }]), 'a1'), false);
  assert.equal(punches(cue([{ creature: 'b1', health: -2, defeated: true }]), 'a1'), true);
  assert.equal(punches(cue([{ creature: 'b1', health: -2 }], { roll: { value: 20, critical: true } }), 'a1'), true);
  assert.equal(punches(cue([{ creature: 'b1', health: -2 }], { roll: { value: 3, critical: false } }), 'a1'), false);
  // A critical heal or ward took nothing from anyone: no punch.
  assert.equal(punches(cue([{ creature: 'a2', health: 6 }], { roll: { value: 20, critical: true } }), 'a1'), false);
  // A caster felled by its own cost is not a killing blow.
  assert.equal(punches(cue([{ creature: 'a1', defeated: true }]), 'a1'), false);
  assert.ok(PUNCH.lasts <= IMPACT_MS);
});

// #909: the body let go at 240 ms and the quill, eased in, was seen to leave it only near 600 ms.
test('a projectile leaves with the throw, at an even pace, and lands as the windup ends', () => {
  assert.equal(FLIGHT.at, THROW.toss.at, 'it leaves when the thrower lets go');
  assert.equal(FLIGHT.at + FLIGHT.lasts, WINDUP_MS, 'it lands on the contact');
  const from = { x: 100, y: 600, radius: 40 }, to = { x: 300, y: 200, radius: 40 };
  const start = flightAt(from, to, 0), end = flightAt(from, to, 1), whole = Math.hypot(end.x - start.x, end.y - start.y);
  assert.deepEqual([end.x, end.y], [to.x, to.y]);
  // It starts on the thrower's back, above its centre and inside its body.
  assert.equal(start.x, from.x);
  assert.ok(start.y < from.y && start.y > from.y - from.radius * 1.4);
  // One tenth of the flight, 60 ms after the throw: already a tenth of the way, where the ease in had moved it a thousandth.
  const early = flightAt(from, to, .1);
  assert.ok(Math.hypot(early.x - start.x, early.y - start.y) >= whole * .1);
  // Never ahead of an even pace by much, and never back: the distance covered only grows.
  let last = 0;
  for (let step = 1; step <= 20; step++) {
    const now = flightAt(from, to, step / 20), covered = Math.hypot(now.x - start.x, now.y - start.y);
    assert.ok(covered > last, `the quill goes on at ${step / 20}`);
    last = covered;
  }
});

test('a projectile flies a low arc, and points where it flies', () => {
  const from = { x: 100, y: 400, radius: 40 }, to = { x: 400, y: 400 - 40 * FLIGHT.back, radius: 40 };
  // Thrown across the field at one height: the middle of the flight is above the straight line by half the lift.
  const start = flightAt(from, to, 0), middle = flightAt(from, to, .5);
  assert.ok(Math.abs(start.y - to.y) < 1e-9);
  assert.ok(Math.abs((to.y - middle.y) - 300 * FLIGHT.lift / 2) < 1e-9);
  // It climbs as it leaves, flies level at the top and comes down onto its target.
  assert.ok(flightAt(from, to, 0).angle < 0);
  assert.ok(Math.abs(middle.angle) < 1e-9);
  assert.ok(flightAt(from, to, 1).angle > 0);
  assert.ok(FLIGHT.lift > 0 && FLIGHT.lift <= .3, 'low: a lob would read as a mortar');
  // A place outside the flight is its nearest end.
  assert.deepEqual(flightAt(from, to, -1), flightAt(from, to, 0));
  assert.deepEqual(flightAt(from, to, 2), flightAt(from, to, 1));
});
