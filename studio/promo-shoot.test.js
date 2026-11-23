// The promotion's still (docs/art/promotion-plan.md, P1; ADR 0205): scripts/promo/shoot.mjs, held without a
// browser. That two stills of one shot give the same bytes is the real-engine journey's (studio/browser/table.promo.js).
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { FORMAT, RATIOS, SIZES, framing, parseArguments, readShot, scenarioId, steps, stillName } from '../scripts/promo/shoot.mjs';

const shots = fileURLToPath(new URL('../promo/shots/', import.meta.url));
const script = fileURLToPath(new URL('../scripts/promo/shoot.mjs', import.meta.url));
const good = { format: FORMAT, title: 'Two Brutes at the top of round 3', scenario: 'greedy-1v1-round-3.json', ratios: ['9:16'], size: 1080 };

// A shot written to a folder of its own, read back; the folder is removed after.
function withShot(shot, read = readShot) {
  const folder = mkdtempSync(join(tmpdir(), 'promo-shot-'));
  try {
    const path = join(folder, 'the-shot.json');
    writeFileSync(path, typeof shot === 'string' ? shot : JSON.stringify(shot));
    return read(path);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
}

test('every shot of the repository reads, with its scenario\'s id', () => {
  const files = readdirSync(shots).filter(name => name.endsWith('.json'));
  assert.ok(files.length > 0, 'promo/shots/ holds a shot');
  for (const file of files) {
    const shot = readShot(join(shots, file));
    assert.match(shot.id, /^sc-repo-[a-z0-9-]+$/, file);
  }
});

test('a shot reads with its name, its scenario\'s id, and every ratio at 1080 when it names none', () => {
  const { ratios, size, ...shot } = good;
  assert.deepEqual(withShot(shot), { name: 'the-shot', title: good.title, scenario: good.scenario, id: 'sc-repo-greedy-1v1-round-3', ratios: Object.keys(RATIOS), size: 1080 });
  assert.equal(scenarioId('greedy-1v1-round-3.json'), 'sc-repo-greedy-1v1-round-3');
});

test('a shot naming a missing scenario is refused', () => {
  assert.throws(() => withShot({ ...good, scenario: 'no-such-scenario.json' }), /scenarios\/no-such-scenario\.json does not exist/);
});

test('a shot naming a tutorial, a bug or a path outside scenarios/ is refused', () => {
  for (const scenario of ['tutorials/to-the-end.json', '../package.json', 'greedy-1v1-round-3', '']) {
    assert.throws(() => withShot({ ...good, scenario }), /names a file directly under scenarios\//, scenario);
  }
});

test('a shot naming a file the host leaves out of its scenarios is refused', () => {
  const scenarios = mkdtempSync(join(tmpdir(), 'promo-scenarios-'));
  try {
    const read = path => readShot(path, { scenarios });
    for (const scenario of ['tutorial-a.json', 'bug-812-round-4.json']) {
      writeFileSync(join(scenarios, scenario), '{}');
      assert.throws(() => withShot({ ...good, scenario }, read), /the host leaves out/, scenario);
    }
    const long = `${'a'.repeat(56)}.json`;
    writeFileSync(join(scenarios, long), '{}');
    assert.throws(() => withShot({ ...good, scenario: long }, read), /past 55 characters/);
    const longest = `${'a'.repeat(55)}.json`;
    writeFileSync(join(scenarios, longest), '{}');
    assert.equal(withShot({ ...good, scenario: longest }, read).id, `sc-repo-${'a'.repeat(55)}`);
  } finally {
    rmSync(scenarios, { recursive: true, force: true });
  }
});

test('a shot naming a ratio this script does not frame is refused, and so is one named twice', () => {
  assert.throws(() => withShot({ ...good, ratios: ['4:5'] }), /the ratio "4:5" is not one of 9:16, 16:9, 1:1/);
  assert.throws(() => withShot({ ...good, ratios: [] }), /"ratios" lists one or more/);
  assert.throws(() => withShot({ ...good, ratios: ['1:1', '1:1'] }), /names a ratio twice/);
});

test('a shot with another format, an unknown key, no title, another size or no JSON is refused', () => {
  assert.throws(() => withShot({ ...good, format: 'downfall.promo.shot.v2' }), /"format" is "downfall\.promo\.shot\.v1"/);
  assert.throws(() => withShot({ ...good, pixels: [0, 0] }), /unknown key "pixels"/);
  assert.throws(() => withShot({ ...good, title: ' ' }), /"title" says what the shot shows/);
  assert.throws(() => withShot({ ...good, size: 1920 }), /"size" is 720 or 1080/);
  assert.throws(() => withShot('{ not json'), /not a shot's JSON/);
  assert.throws(() => withShot('[]'), /a shot is a JSON object/);
});

test('each ratio frames the page so its short side is the size asked', () => {
  const pixels = (ratio, size) => {
    const { viewport, density } = framing(ratio, size);
    return [viewport.width * density, viewport.height * density];
  };
  assert.deepEqual(pixels('9:16', 1080), [1080, 1920]);
  assert.deepEqual(pixels('16:9', 1080), [1920, 1080]);
  assert.deepEqual(pixels('1:1', 1080), [1080, 1080]);
  assert.deepEqual(pixels('9:16', 720), [720, 1280]);
  // The vertical ratio is a phone's layout, never wider than one.
  assert.ok(RATIOS['9:16'].width <= 390);
  for (const ratio of Object.keys(RATIOS)) for (const size of SIZES) assert.ok(pixels(ratio, size).every(Number.isInteger), `${ratio} at ${size}`);
});

test('a still is named by its ratio', () => {
  assert.equal(stillName('9:16'), 'still-9x16.png');
  assert.equal(stillName('1:1'), 'still-1x1.png');
});

test('the command takes one shot and --still, and refuses the rest', () => {
  assert.deepEqual(parseArguments(['promo/shots/a.json', '--still', '--ratio', '1:1', '--out', 'x']), { shot: 'promo/shots/a.json', still: true, ratio: '1:1', out: 'x' });
  assert.throws(() => parseArguments(['promo/shots/a.json']), /Only --still/);
  assert.throws(() => parseArguments(['--still']), /Usage/);
  assert.throws(() => parseArguments(['a.json', 'b.json', '--still']), /Name one shot/);
  assert.throws(() => parseArguments(['a.json', '--still', '--ratio', '4:3']), /--ratio is one of/);
  assert.throws(() => parseArguments(['a.json', '--still', '--ratio']), /--ratio needs a value/);
  assert.throws(() => parseArguments(['a.json', '--still', '--film']), /Unknown option --film/);
});

// The command run on a shot written to a folder of its own, with these arguments after it.
function shoot(shot, ...rest) {
  const folder = mkdtempSync(join(tmpdir(), 'promo-shot-'));
  try {
    const path = join(folder, 'the-shot.json');
    writeFileSync(path, JSON.stringify(shot));
    return spawnSync(process.execPath, [script, path, '--still', ...rest], { encoding: 'utf8' });
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
}

test('the command refuses a shot naming a missing scenario before it starts a browser, with exit 1', () => {
  const run = shoot({ ...good, scenario: 'no-such-scenario.json' });
  assert.equal(run.status, 1);
  assert.match(run.stderr, /scenarios\/no-such-scenario\.json does not exist/);
  assert.equal(run.stdout, '');
});

test('the command refuses a --ratio the shot does not name, with exit 1', () => {
  const run = shoot({ ...good, ratios: ['9:16'] }, '--ratio', '1:1');
  assert.equal(run.status, 1);
  assert.match(run.stderr, /--ratio 1:1 is not one of the shot's: 9:16/);
  assert.equal(run.stdout, '');
});

test('steps run one after the other until done, and say whether done was reached', async () => {
  const ran = [];
  assert.equal(await steps(async () => { ran.push(ran.length); }, { times: 5, done: () => ran.length === 3 }), true);
  assert.deepEqual(ran, [0, 1, 2]);
  assert.equal(await steps(async () => { ran.push(ran.length); }, { times: 2, done: () => false }), false);
  assert.deepEqual(ran, [0, 1, 2, 3, 4]);
  // With no done, every step runs: a tick of ten is ten ticks.
  let ticks = 0;
  await steps(() => { ticks += 1; }, { times: 10 });
  assert.equal(ticks, 10);
});
