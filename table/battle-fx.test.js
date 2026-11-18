import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { BEATS, stagingBeats } from './battle-staging.js';
import { benchEntries, benchFrame, benchSamples } from './battle-bench.js';
import { engineOrLaboratory, kitOnly, laboratoryOnly } from '../battle/live-guard.mjs';

// The effects kit (ADR 0196) is built by `npm --prefix battle run build`, which CI runs before these tests; a
// checkout without it says so rather than passing.
const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
const built = existsSync(new URL('battle-build/fx.js', import.meta.url));
const skip = !built && 'the battle bundle is not built';
const kitModule = () => import(new URL('battle-build/fx.js', import.meta.url).href);
const samples = benchSamples();

// A scene and an engine nobody may touch: any read of them fails the test.
const untouchable = name => new Proxy({}, { get: (_target, key) => { throw new Error(`${name}.${String(key)} was read`); } });
const play = beat => ({ beat, at: { x: 10, y: 20, radius: 30 }, from: { x: 0, y: 0, radius: 30 }, heading: 0, creature: 'b1', color: 0xffffff, rate: 1,
  figure: untouchable('figure') });

test('the kit plays every beat of the vocabulary but the motif, which the view draws itself', { skip }, async () => {
  const { createKit } = await kitModule();
  const kit = createKit(untouchable('engine'), untouchable('scene'), () => true);
  assert.deepEqual([...kit.beats].sort(), BEATS.filter(beat => beat !== 'motif').sort());
});

test('under reduced motion no primitive makes anything: the scene, the engine and the figure are never touched', { skip }, async () => {
  const { createKit } = await kitModule();
  const kit = createKit(untouchable('engine'), untouchable('scene'), () => true);
  for (const name of kit.beats) {
    for (const delay of [0, 200]) {
      const [beat] = stagingBeats({ staging: [{ beat: name, at: 840 + delay }] });
      assert.equal(beat.delay, delay);
      assert.doesNotThrow(() => kit.play(play(beat)), name);
    }
  }
  assert.deepEqual(kit.playing(), []);
  assert.equal(kit.changes(), 0);
});

test('a beat the kit does not know is not played, and not an error', { skip }, async () => {
  const { createKit } = await kitModule();
  const kit = createKit(untouchable('engine'), untouchable('scene'), () => false);
  assert.doesNotThrow(() => kit.play(play({ beat: 'sparkle', at: 840, delay: 0, window: 'impact', on: 'target' })));
  assert.doesNotThrow(() => kit.play(play({ beat: 'motif', at: 840, delay: 0, window: 'impact', on: 'target' })));
});

test('the kit is a small file of its own: no engine in it, and it in no other bundle', { skip }, () => {
  const kit = read('battle-build/fx.js');
  // The engine alone is 1.3 MB; the kit is handed the one the page holds.
  assert.ok(kit.length < 48 * 1024, `${kit.length} bytes`);
  assert.ok(gzipSync(kit).length < 16 * 1024);
  assert.ok(!kit.includes('battleBench'));
  for (const bundle of ['battle-build/live.js', 'battle-build/game.js']) {
    const other = read(bundle);
    // The kit's own texture is named in the kit alone; the others name the kit by the hash of what was built.
    assert.ok(!other.includes('fx-kit'), bundle);
    assert.match(other, /fx\.js\?v=[0-9a-f]{12}/, bundle);
  }
});

test('the build keeps the kit apart: no engine or laboratory in it, and no kit module in another bundle', () => {
  for (const path of ['node_modules/phaser/dist/phaser.esm.js', '../table/battle-bench.js', 'src/bench.ts', 'spell-names:spell-names']) assert.equal(engineOrLaboratory(path), true, path);
  for (const path of ['src/fx/kit.ts', 'src/fx/burst.ts', '../table/battle-staging.js', '../table/battle-timing.js']) assert.equal(engineOrLaboratory(path), false, path);
  for (const path of ['src/fx/index.ts', 'src/fx/pulse.ts']) assert.equal(kitOnly(path), true, path);
  for (const path of ['src/fx-loader.ts', 'src/spell-effects.ts', 'src/effects.ts']) assert.equal(kitOnly(path), false, path);
  // The spell names the bench reads are the laboratory's: the live page names no spell.
  assert.equal(laboratoryOnly('spell-names:spell-names'), true);
  const build = read('../battle/build.mjs');
  assert.match(build, /inputs\(kit, 'fx'\)\.some\(engineOrLaboratory\)/);
  assert.match(build, /\.some\(kitOnly\)/);
  assert.match(read('../battle/budget.mjs'), /kitBundle: \d+ \* 1024, gzipKit: \d+ \* 1024/);
});

test('the bench has one sample staging a primitive, each one the page can read', () => {
  assert.deepEqual(samples.map(one => one.id), BEATS.filter(beat => beat !== 'motif').map(beat => `kit:${beat}`));
  for (const sample of samples) {
    const beats = stagingBeats(sample);
    assert.ok(beats, sample.id);
    assert.ok(beats.some(beat => beat.beat === sample.id.split(':')[1]), sample.id);
    const { cue } = benchFrame({ entry: sample, actor: 'a1', targets: ['b1'], stage: 'impact' });
    assert.deepEqual(cue.visual.staging, sample.staging);
  }
});

test('the bench names a spell as its file does, and by its id\'s slug when it has no name', () => {
  const art = { 'spell:heavy_strike:v1': { family: 'physical', motif: 'impact', delivery: 'melee' }, 'spell:ice_spear:v1': { family: 'elemental', motif: 'ice', delivery: 'cast',
    staging: [{ beat: 'motif', at: 840 }] } };
  const entries = benchEntries(art, { 'spell:heavy_strike:v1': 'Strike' });
  assert.deepEqual(entries.map(one => one.name), ['Ice Spear', 'Strike']);
  assert.equal(entries[1].staging, undefined);
  assert.deepEqual(entries[0].staging, [{ beat: 'motif', at: 840 }]);
});

test('the bench bundle carries the real names, and the live one none', { skip }, () => {
  // `spell:heavy_strike:v1` is named Strike by its file (`data/Spells`).
  assert.match(read('battle-build/game.js'), /"spell:heavy_strike:v1":"Strike"/);
  assert.ok(!read('battle-build/live.js').includes('spell:heavy_strike:v1'));
});

// The image builds the bundle in a stage that copies its inputs one by one: what `build.mjs` reads must be there.
test('the image\'s battle stage copies the spell files the build reads', () => {
  assert.match(read('../battle/build.mjs'), /spellNames\(folder = '\.\.\/data\/Spells'\)/);
  assert.match(read('../Dockerfile'), /^COPY data\/Spells\/ \/src\/data\/Spells\/$/m);
});
