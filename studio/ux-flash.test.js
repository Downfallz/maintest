import { test } from 'node:test';
import assert from 'node:assert/strict';
import { cellChanges, flashesASecond, luminance } from '../scripts/ux/flash.mjs';

// The flash guard of UX13 (scripts/ux/flash.mjs), without a browser: WCAG 2.3.1's thresholds on frames of cells.
const dark = Array(100).fill(0.02), bright = Array(100).fill(0.6);
const part = share => Array.from({ length: 100 }, (_, cell) => (cell < share * 100 ? 0.6 : 0.02));

test('luminance follows WCAG: black is 0, white is 1, and green weighs most', () => {
  assert.equal(luminance(0, 0, 0), 0);
  assert.equal(Number(luminance(255, 255, 255).toFixed(4)), 1);
  assert.ok(luminance(0, 255, 0) > luminance(255, 0, 0));
});

test('a window that opens bright over a dark page is one change and no flash', () => {
  const frames = [dark, dark, part(0.5), bright, bright, bright];
  assert.deepEqual(cellChanges(frames.map(frame => frame[0])), [{ at: 2, direction: 'up' }]);
  assert.deepEqual(cellChanges(frames.map(frame => frame[99])), [{ at: 3, direction: 'up' }]);
  assert.equal(flashesASecond(frames, 40), 0);
});

test('a light that comes and goes four times in a second is four flashes', () => {
  const frames = [dark, bright, dark, bright, dark, bright, dark, bright, dark];
  assert.equal(flashesASecond(frames, 100), 4);
});

test('three flashes in a second pass, and the same spread over two seconds count apart', () => {
  assert.equal(flashesASecond([dark, bright, dark, bright, dark, bright, dark], 100), 3);
  assert.equal(flashesASecond([dark, bright, dark, bright, dark, bright, dark], 400), 2);
});

test('a change over less than a quarter of the screen, a small one, or one between two light shades does not count', () => {
  assert.equal(flashesASecond([dark, part(0.2), dark, part(0.2), dark, part(0.2), dark, part(0.2), dark], 50), 0);
  const faint = Array(100).fill(0.07);
  assert.equal(flashesASecond([dark, faint, dark, faint, dark, faint, dark, faint, dark], 50), 0);
  const light = Array(100).fill(0.85), lighter = Array(100).fill(1);
  assert.equal(flashesASecond([light, lighter, light, lighter, light, lighter, light, lighter, light], 50), 0);
});

test('a light that ramps up and down in steps each under 10% still flashes, read against the extreme', () => {
  // 0.02, 0.06, 0.10, 0.14, 0.10, 0.06, 0.02, ...: each step is 0.04, each swing 0.12, a flash every 240 ms, so five
  // flashes end within one second (at 240, 480, 720, 960 and 1200 ms). Read frame to frame, it would be none.
  const ramp = [0.02, 0.06, 0.1, 0.14, 0.1, 0.06];
  const frames = Array.from({ length: 31 }, (_, index) => Array(100).fill(ramp[index % ramp.length]));
  assert.equal(flashesASecond(frames, 40), 5);
  assert.equal(flashesASecond(frames, 40, { step: 0.13 }), 0);
});

test('a slow ramp the same way is one change, and a change spread over frames from cell to cell still counts together', () => {
  const slow = Array.from({ length: 30 }, (_, index) => Array(100).fill(0.02 + index * 0.02));
  assert.deepEqual(cellChanges(slow.map(frame => frame[0])), [{ at: 5, direction: 'up' }]);
  assert.equal(flashesASecond(slow, 40), 0);
  // Half the cells flash one frame behind the other half: no frame has a quarter of them changing at once.
  const flicker = index => (Math.floor(index / 2) % 2 ? 0.6 : 0.02);
  const staggered = Array.from({ length: 13 }, (_, index) => Array.from({ length: 100 }, (__, cell) => flicker(cell < 50 ? index : index + 1)));
  assert.equal(flashesASecond(staggered, 40), 3);
});

test('the guard refuses a film it cannot read', () => {
  assert.throws(() => flashesASecond([dark], 40), /two frames/);
  assert.throws(() => flashesASecond([dark, Array(5).fill(0)], 40), /same cells/);
  assert.throws(() => flashesASecond([dark, dark], 0), /time between frames/);
});
