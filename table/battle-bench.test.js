import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { benchCreatures, benchDefaultTargets, benchDuration, benchEntries, benchFrame, benchStage, filmInterval,
  seededRandom, FILM_FRAMES, FILM_TICK_MS } from './battle-bench.js';
import { IMPACT_MS, WINDUP_MS } from './battle-timing.js';
import { parseArguments } from '../scripts/battle/filmstrip.mjs';
import { laboratoryOnly } from '../battle/live-guard.mjs';

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
const art = JSON.parse(read('../data/presentation/spell-art.json'));
const entries = benchEntries(art);
const entry = id => entries.find(one => one.id === id);

test('the bench lists every entry of the spell art, each with its look and a name to read', () => {
  assert.equal(entries.length, Object.keys(art).length);
  assert.equal(entries.length, 44);
  assert.deepEqual(entries.map(one => one.id).sort(), Object.keys(art).sort());
  for (const one of entries) {
    assert.deepEqual([one.family, one.motif, one.delivery], [art[one.id].family, art[one.id].motif, art[one.id].delivery]);
    assert.match(one.name, /^[A-Z][a-z]+( [A-Z][a-z]+)*$/);
  }
  assert.deepEqual(entries.map(one => one.name), entries.map(one => one.name).sort((a, b) => a.localeCompare(b, 'en')));
});

test('the bench plays on the six creatures of the fixture, three a side', () => {
  const creatures = benchCreatures();
  assert.equal(creatures.length, 6);
  assert.equal(creatures.filter(one => one.enemy).length, 3);
  assert.ok(creatures.every(one => one.isAlive && one.form && one.name));
  assert.deepEqual(benchDefaultTargets('a1'), ['b1']);
  assert.deepEqual(benchDefaultTargets('b2'), ['a1']);
  assert.deepEqual(benchDefaultTargets('nobody'), []);
});

test('a play is the windup then the impact, shortened by the speed', () => {
  assert.equal(benchDuration(), WINDUP_MS + IMPACT_MS);
  assert.equal(benchDuration(2), (WINDUP_MS + IMPACT_MS) / 2);
  assert.equal(benchStage(0), 'windup');
  assert.equal(benchStage(WINDUP_MS - 1), 'windup');
  assert.equal(benchStage(WINDUP_MS), 'impact');
  assert.equal(benchStage(WINDUP_MS + IMPACT_MS - 1), 'impact');
  assert.equal(benchStage(WINDUP_MS + IMPACT_MS), 'resolved');
  assert.equal(benchStage(WINDUP_MS / 2, 2), 'impact');
  // A speed the playback does not know plays at 1×, as everywhere else.
  assert.equal(benchStage(WINDUP_MS - 1, 7), 'windup');
});

test('a frame carries the chosen look to the chosen creatures while it plays, and nothing at rest', () => {
  const pummel = entry('spell:pummel:v1');
  const rest = benchFrame({ entry: pummel, actor: 'a1', targets: ['b2'], stage: 'decision' });
  assert.equal(rest.cue, undefined);
  assert.equal(rest.actor, 'a1');
  assert.equal(rest.target, 'b2');
  assert.equal(benchFrame({ entry: pummel, actor: 'a1', targets: ['b2'], stage: 'resolved' }).cue, undefined);
  for (const stage of ['windup', 'impact']) {
    const { cue } = benchFrame({ entry: pummel, actor: 'a1', targets: ['b2', 'b3'], stage });
    assert.deepEqual(cue.visual, art['spell:pummel:v1']);
    assert.deepEqual(cue.recipients, ['b2', 'b3']);
    assert.deepEqual(cue.contact, ['b2', 'b3']);
    assert.deepEqual(cue.struck, ['b2', 'b3']);
    assert.equal(cue.kind, 'strike');
  }
});

test('where a creature stands decides whether it is struck: the bench holds no rule', () => {
  const ward = entry('spell:bone_ward:v1');
  const { cue, target } = benchFrame({ entry: ward, actor: 'a1', targets: ['a1', 'a2', 'ghost'], stage: 'impact' });
  assert.deepEqual(cue.recipients, ['a1', 'a2']);
  assert.deepEqual(cue.contact, ['a2']);
  assert.deepEqual(cue.struck, []);
  assert.equal(cue.kind, 'pulse');
  assert.equal(target, 'a2');
  // An opposing caster strikes your side.
  assert.deepEqual(benchFrame({ entry: ward, actor: 'b1', targets: ['a2', 'b3'], stage: 'impact' }).cue.struck, ['a2']);
});

test('the pinned randomness repeats for one seed and stays in [0, 1)', () => {
  const one = seededRandom(7), same = seededRandom(7), other = seededRandom(8);
  const drawn = Array.from({ length: 50 }, one);
  assert.deepEqual(drawn, Array.from({ length: 50 }, same));
  assert.notDeepEqual(drawn, Array.from({ length: 50 }, other));
  assert.ok(drawn.every(value => value >= 0 && value < 1));
  assert.ok(new Set(drawn).size > 45);
});

test('twelve frames spread over one whole play, on whole ticks', () => {
  for (const speed of [0.5, 1, 2]) {
    const interval = filmInterval(speed);
    assert.equal(interval % FILM_TICK_MS, 0);
    assert.ok(interval * (FILM_FRAMES - 1) >= benchDuration(speed));
    assert.ok((interval - FILM_TICK_MS) * (FILM_FRAMES - 1) < benchDuration(speed));
  }
});

// The bundle is built by `npm --prefix battle run build`, which CI runs before these tests; a checkout without
// it says so rather than passing.
test('the live bundle holds no bench code, and the laboratory bundle holds it', { skip: !existsSync(new URL('battle-build/live.js', import.meta.url)) && 'the battle bundle is not built' }, () => {
  assert.ok(read('battle-build/game.js').includes('battleBench'));
  const live = read('battle-build/live.js');
  assert.ok(!live.includes('battleBench'));
  assert.ok(!live.includes('spell:adrenaline_tonic:v1'));
});

test('the build refuses the laboratory and the bench in the live bundle, and nothing the live view imports', () => {
  for (const path of ['../table/battle-bench.js', 'src/bench.ts', '../data/presentation/spell-art.json',
    '../table/battle-lab-state.js', '../table/battle-lab-fixture.js']) assert.equal(laboratoryOnly(path), true, path);
  for (const path of ['src/live.ts', 'src/scene.ts', 'src/spell-effects.ts', '../table/battle-timing.js',
    'src/workbench.ts', '../table/battle-art.js']) assert.equal(laboratoryOnly(path), false, path);
  assert.match(read('../battle/build.mjs'), /live\.some\(laboratoryOnly\)/);
});

test('the film strip script reads its options, and refuses what it cannot film', () => {
  const read = parseArguments(['spell:pummel:v1', '--actor', 'a2', '--targets', 'b1,b3', '--speed', '2', '--out', 'sheet.png']);
  assert.deepEqual(read.spells, ['spell:pummel:v1']);
  assert.deepEqual(read.film, { actor: 'a2', targets: ['b1', 'b3'], speed: 2 });
  assert.equal(read.out, 'sheet.png');
  assert.equal(parseArguments(['--all']).all, true);
  assert.throws(() => parseArguments([]), /Name one or more effects/);
  assert.throws(() => parseArguments(['--all', 'spell:pummel:v1']), /Name one or more effects/);
  assert.throws(() => parseArguments(['spell:pummel:v1', '--speed', 'fast']), /positive number/);
  assert.throws(() => parseArguments(['spell:pummel:v1', '--out']), /needs a value/);
  assert.throws(() => parseArguments(['spell:pummel:v1', '--loud']), /Unknown option/);
  // A speed the page does not offer would film at 1× under another speed's title.
  assert.throws(() => parseArguments(['spell:pummel:v1', '--speed', '1.5']), /0\.5, 1 or 2/);
  assert.throws(() => parseArguments(['spell:pummel:v1', '--interval', '250']), /multiple of 20/);
  assert.throws(() => parseArguments(['--all', '--out', 'sheet.png']), /folder/);
  assert.throws(() => parseArguments(['spell:pummel:v1', 'spell:frostbite:v1', '--out', 'sheet.png']), /folder/);
});

test('the film strip script run with nothing to film says so and exits 1', () => {
  const run = spawnSync(process.execPath, [fileURLToPath(new URL('../scripts/battle/filmstrip.mjs', import.meta.url))], { encoding: 'utf8' });
  assert.equal(run.status, 1);
  assert.match(run.stderr, /Name one or more effects/);
  assert.equal(run.stdout, '');
});

// With or without the browser suite installed, a film that cannot start ends: it does not leave its server
// listening and the caller waiting.
test('the film strip script exits 1 when its browser does not start', () => {
  const run = spawnSync(process.execPath, [fileURLToPath(new URL('../scripts/battle/filmstrip.mjs', import.meta.url)), 'spell:pummel:v1'],
    { encoding: 'utf8', timeout: 20000, env: { ...process.env, STUDIO_CHROMIUM: '/definitely-not-a-browser' } });
  assert.equal(run.signal, null);
  assert.equal(run.status, 1);
  assert.notEqual(run.stderr, '');
});
