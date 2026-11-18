import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { ACTION_MS, ANCHORS, BEATS, BLENDS, DIRECTIONS, MAX_BEATS, MAX_PARTICLES, SHAPES, TINTS, stagingBeats } from './battle-staging.js';
import { IMPACT_MS, WINDUP_MS } from './battle-timing.js';

const visual = staging => ({ family: 'physical', motif: 'impact', delivery: 'melee', staging });

test('a visual without a staging plays its motif', () => {
  assert.equal(stagingBeats({ family: 'physical', motif: 'impact', delivery: 'melee' }), null);
  assert.equal(stagingBeats(visual(null)), null);
  assert.equal(stagingBeats(visual([])), null);
  assert.equal(stagingBeats(undefined), null);
});

test('a beat is read with its defaults, its window and its delay inside it', () => {
  const [motif, burst, gather] = stagingBeats(visual([
    { beat: 'motif', at: 840 }, { beat: 'burst', at: 900, count: 8, direction: 'along' }, { beat: 'gather', at: 200, lasts: null },
  ]));
  assert.deepEqual({ beat: motif.beat, window: motif.window, delay: motif.delay, on: motif.on }, { beat: 'motif', window: 'impact', delay: 0, on: 'target' });
  assert.equal(motif.lasts, undefined);
  assert.deepEqual([burst.window, burst.delay, burst.count, burst.direction, burst.spread, burst.shape, burst.tint], ['impact', 60, 8, 'along', 50, 'spark', 'family']);
  // The host writes a parameter left out as null: it is a parameter left out.
  assert.deepEqual([gather.window, gather.delay, gather.on, gather.lasts], ['windup', 200, 'caster', 400]);
});

test('a burst nobody aimed fans all around', () => {
  assert.equal(stagingBeats(visual([{ beat: 'burst', at: 840 }]))[0].spread, 360);
  assert.equal(stagingBeats(visual([{ beat: 'burst', at: 840, spread: 90 }]))[0].spread, 90);
});

test('a beat near the end of its window is shortened to end inside it', () => {
  assert.equal(stagingBeats(visual([{ beat: 'gather', at: 700 }]))[0].lasts, WINDUP_MS - 700);
  assert.equal(stagingBeats(visual([{ beat: 'flow', at: ACTION_MS - 100 }]))[0].lasts, 100);
});

test('a beat the page does not know falls back to the motif, whole', () => {
  assert.equal(stagingBeats(visual([{ beat: 'motif', at: 840 }, { beat: 'sparkle', at: 900 }])), null);
  // A kit that plays fewer beats than the catalogue stages: an older kit beside a newer host.
  const staging = visual([{ beat: 'motif', at: 840 }, { beat: 'pulse', at: 900 }]);
  assert.equal(stagingBeats(staging, ['motif', 'burst']), null);
  assert.equal(stagingBeats(staging, ['motif', 'pulse']).length, 2);
});

test('a beat the page cannot read falls back to the motif', () => {
  const refused = [
    { beat: 'burst', at: -1 }, { beat: 'burst', at: ACTION_MS }, { beat: 'burst', at: 1.5 }, { beat: 'burst' },
    { beat: 'burst', at: 900, shape: 'star' }, { beat: 'burst', at: 900, tint: 'gold' }, { beat: 'burst', at: 900, on: 'sky' },
    { beat: 'burst', at: 900, count: MAX_PARTICLES + 1 }, { beat: 'burst', at: 900, spread: 0 }, { beat: 'glow', at: 900, strength: 2 },
    { beat: 'glow', at: 800, lasts: 100 }, { beat: 'glow', at: 900, lasts: 0 }, { beat: 'motif', at: 100 }, null, 'burst',
  ];
  for (const beat of refused) assert.equal(stagingBeats(visual([beat])), null, JSON.stringify(beat));
  assert.equal(stagingBeats(visual(Array.from({ length: MAX_BEATS + 1 }, () => ({ beat: 'glow', at: 900 })))), null);
  for (const once of ['motif', 'flash', 'shake']) assert.equal(stagingBeats(visual([{ beat: once, at: 900 }, { beat: once, at: 1000 }])), null);
});

test('no beat is tinted in the target marker\'s gold', () => {
  assert.ok(!TINTS.includes('gold'));
});

// The builder refuses what the page would not play: the two hold the same words and the same numbers.
test('the page and the builder hold one vocabulary', () => {
  const source = readFileSync(new URL('../src/DownfallArena.Infrastructure/Resources/SpellArt.cs', import.meta.url), 'utf8');
  const list = name => JSON.parse(source.match(new RegExp(`IReadOnlyList<string> ${name} =\\s*(\\[[^\\]]*\\])`))[1]);
  const number = name => Number(source.match(new RegExp(`const int ${name} = (\\d+);`))[1]);
  assert.deepEqual(list('Beats'), [...BEATS]);
  assert.deepEqual(list('Anchors'), [...ANCHORS]);
  assert.deepEqual(list('Shapes'), [...SHAPES]);
  assert.deepEqual(list('Tints'), [...TINTS]);
  assert.deepEqual(list('Blends'), [...BLENDS]);
  assert.deepEqual(list('Directions'), [...DIRECTIONS]);
  assert.deepEqual(list('Single'), ['motif', 'flash', 'shake']);
  assert.equal(number('WindupMs'), WINDUP_MS);
  assert.equal(number('ActionMs'), WINDUP_MS + IMPACT_MS);
  assert.equal(number('MaxBeats'), MAX_BEATS);
  assert.equal(number('MaxParticles'), MAX_PARTICLES);
});
