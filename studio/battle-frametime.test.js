// The battle's acceptance (docs/art/battle-craft-plan.md, BC10): what the frame time script computes from its
// samples, what it asks of its flags, and the record it fills (docs/art/battle-acceptance.md), held to the
// spell art so a spell staged later cannot go unmeasured.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { FRAME_MS, middle, parseArguments, recordTable, summarize } from '../scripts/battle/frametime.mjs';
import { castTargets, spellNames } from '../scripts/battle/cast.mjs';
import { stagingBeats } from '../table/battle-staging.js';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const read = path => readFileSync(join(root, path), 'utf8');
const art = JSON.parse(read('data/presentation/spell-art.json'));
const RECORD = 'docs/art/battle-acceptance.md';

test('a summary tells the median, the 95th centile, the worst and how many samples are over a frame', () => {
  const samples = [1, 2, 3, 4, 5, 6, 7, 8, 9, 40];
  assert.deepEqual(summarize(samples), { count: 10, median: 5.5, p95: 40, worst: 40, over: 1 });
  assert.deepEqual(summarize([3, 1, 2]), { count: 3, median: 2, p95: 3, worst: 3, over: 0 });
  // A sample that fills the frame exactly is not over it.
  assert.equal(summarize([FRAME_MS]).over, 0);
  assert.equal(summarize([10, 20], 15).over, 1);
});

test('a summary of no sample is refused: a film that measured nothing is a fault, not a zero', () => {
  assert.throws(() => summarize([]), /no sample/);
});

test('several runs are read by the middle one of each number, so one loaded moment does not make the record', () => {
  const runs = [{ median: 2, worst: 90, over: 3 }, { median: 3, worst: 10, over: 0 }, { median: 2.5, worst: 12, over: 1 }];
  assert.deepEqual(middle(runs), { median: 2.5, worst: 12, over: 1 });
});

test('the flags: effects by name or all of them, the throttle, the runs and where the measures go', () => {
  assert.deepEqual(parseArguments(['spell:pummel:v1']), { spells: ['spell:pummel:v1'], all: false, throttle: 4, runs: 3, speed: 1, out: null, base: null, markdown: false });
  const asked = parseArguments(['--all', '--throttle', '6', '--runs', '5', '--speed', '2', '--out', 'frames.json', '--markdown']);
  assert.deepEqual(asked, { spells: [], all: true, throttle: 6, runs: 5, speed: 2, out: 'frames.json', base: null, markdown: true });
});

test('the flags refuse what cannot be measured', () => {
  assert.throws(() => parseArguments([]), /Name one or more effects/);
  assert.throws(() => parseArguments(['--all', 'spell:pummel:v1']), /Name one or more effects/);
  assert.throws(() => parseArguments(['--all', '--throttle', '0.5']), /--throttle/);
  assert.throws(() => parseArguments(['--all', '--runs', '0']), /--runs/);
  assert.throws(() => parseArguments(['--all', '--runs', '2.5']), /--runs/);
  assert.throws(() => parseArguments(['--all', '--speed', '3']), /--speed/);
  assert.throws(() => parseArguments(['--all', '--out']), /needs a value/);
  assert.throws(() => parseArguments(['--all', '--fast']), /Unknown option/);
});

test('the record\'s table has one row an effect, the numbers to one decimal', () => {
  const table = recordTable([{ name: 'Pummel', targets: ['b1'], beats: 4, pulse: false,
    step: { median: 2.04, p95: 5.16, worst: 28.7, over: 1 }, frame: { median: 16.7, worst: 33.3, over: 2 } }]);
  const lines = table.trim().split('\n');
  assert.equal(lines.length, 3);
  assert.match(lines[0], /^\| Effect \| Figures \| Beats \|/);
  assert.equal(lines[2], '| Pummel | 1 | 4 | 2.0 | 5.2 | 28.7 | 1 | 16.7 | 33.3 | 2 |');
  // A staging with a halo on the outline says so by its name: its cost is the renderer's.
  assert.match(recordTable([{ name: 'Tornado', targets: ['b1', 'b2'], beats: 9, pulse: true,
    step: { median: 1, p95: 1, worst: 1, over: 0 }, frame: { median: 1, worst: 1, over: 0 } }]), /\| Tornado ◌ \| 2 \| 9 \|/);
});

test('a spell is cast on whom its content lets it reach: itself, an ally, or as many enemies as it may', () => {
  const targets = castTargets(root);
  for (const id of Object.keys(art)) {
    const on = targets(id);
    assert.ok(on.length >= 1 && on.length <= 3, `${id} is cast on ${on}`);
    assert.ok(on.every(one => /^[ab][123]$/.test(one)), `${id} is cast on ${on}`);
  }
  // Tornado reaches several enemies; Strike one.
  assert.ok(targets('spell:tornado:v1').length > 1);
  assert.deepEqual(targets('spell:heavy_strike:v1'), ['b1']);
});

test('every entry of the spell art is staged, and its staging is one the page reads', () => {
  for (const [id, visual] of Object.entries(art)) assert.ok(stagingBeats(visual), `${id} has no staging the page reads`);
});

test('the record measures every entry of the spell art, and no spell that is gone', () => {
  const record = read(RECORD);
  const names = spellNames(root);
  const section = record.split(/^## /m).find(part => part.startsWith('Frame time'));
  assert.ok(section, 'the record has no "Frame time" section');
  const rows = section.split('\n').filter(line => /^\| [^|]+ \| \d+ \| \d+ \|/.test(line)).map(line => line.split('|')[1].replace('◌', '').trim());
  const spells = Object.keys(art).map(id => names[id]).sort();
  assert.deepEqual(rows.filter(name => spells.includes(name)).sort(), spells);
  const samples = rows.filter(name => !spells.includes(name));
  // What is measured beside the spells: the bench's samples that give the baselines.
  for (const name of samples) assert.match(name, /^Kit: /, `${name} is no spell of the art`);
});

test('the record is listed in the docs index', () => {
  assert.ok(read('docs/README.md').includes('art/battle-acceptance.md'));
});
