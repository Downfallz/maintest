// The round seen played on the showcase (UX6): scripts/ux/round.mjs, held without a browser.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { CAST, FRAME, PHONE, SCALE, SCREENS, SOURCES, boxOf, castFrame, parseArguments } from '../scripts/ux/round.mjs';
import { JOURNEYS } from '../scripts/ux/journeys.mjs';

const declaration = JSON.parse(readFileSync(new URL('../docs/art/page-pictures.json', import.meta.url), 'utf8'));
const declared = name => declaration.pictures.find(picture => picture.name === name);

test('each move is a screen the match journey walks, cut to one shape inside the phone', () => {
  const match = JOURNEYS.find(journey => journey.id === 'match');
  for (const { name, screen, top } of SCREENS) {
    assert.ok(match.screens.includes(screen), screen);
    assert.equal(declared(name).source, `${SOURCES}/${name}.webp`);
    const [left, upper, right, lower] = boxOf(top);
    assert.deepEqual([left, right - left, lower - upper], [0, FRAME.width * SCALE, FRAME.height * SCALE]);
    assert.ok(lower <= PHONE.height * SCALE);
  }
});

test('a part that would leave the phone is refused', () => {
  assert.throws(() => boxOf(PHONE.height - FRAME.height + 1), /does not fit/);
  assert.throws(() => boxOf(-1), /does not fit/);
});

test('the Cast strip is the declaration\'s frames, in the order the bench films them', () => {
  assert.deepEqual(declared('round-cast').frames, CAST.frames.map((_, index) => `${SOURCES}/${castFrame(index)}.webp`));
  assert.deepEqual([...CAST.frames].sort((a, b) => a - b), CAST.frames);
  // The bench is stepped `interval` ms at a time: a frame between two steps would never be filmed.
  for (const ms of CAST.frames) assert.equal(ms % CAST.interval, 0, `${ms} ms`);
});

test('the script takes a folder to write into, and nothing else', () => {
  assert.deepEqual(parseArguments([]), { out: SOURCES });
  assert.deepEqual(parseArguments(['--out', 'elsewhere']), { out: 'elsewhere' });
  assert.throws(() => parseArguments(['--out']), /Usage/);
  assert.throws(() => parseArguments(['--width', '320']), /Usage/);
});
