// The promotion's still (docs/art/promotion-plan.md, P1; ADR 0205): scripts/promo/shoot.mjs, held without a
// browser. That two stills of one shot give the same bytes is the real-engine journey's (studio/browser/table.promo.js).
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { FORMAT, FRAME_RATES, actOn, LONGEST_FILM, RATIOS, SAFE_ZONES, SIZES, answer, encodeFilm, filmFacts, filmNames, frameEnd, framesDigest, framing, hasEncoder, parseArguments, readShot, scenarioId, sha256, sheetFrames, steps, stillName } from '../scripts/promo/shoot.mjs';

const shots = fileURLToPath(new URL('../promo/shots/', import.meta.url));
const script = fileURLToPath(new URL('../scripts/promo/shoot.mjs', import.meta.url));
const film = { seconds: 3, fps: 30, casts: ['spell:pummel:v1'] };
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
  assert.deepEqual(withShot(shot), { name: 'the-shot', title: good.title, scenario: good.scenario, id: 'sc-repo-greedy-1v1-round-3', ratios: Object.keys(RATIOS), size: 1080, film: null });
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

test('the command takes one shot and --still or --film, and refuses the rest', () => {
  assert.deepEqual(parseArguments(['promo/shots/a.json', '--still', '--ratio', '1:1', '--out', 'x']), { shot: 'promo/shots/a.json', still: true, film: false, ratio: '1:1', out: 'x' });
  assert.deepEqual(parseArguments(['promo/shots/a.json', '--film']), { shot: 'promo/shots/a.json', still: false, film: true, ratio: null, out: null });
  assert.throws(() => parseArguments(['promo/shots/a.json']), /Name --still .* or --film/);
  assert.throws(() => parseArguments(['promo/shots/a.json', '--still', '--film']), /not both/);
  assert.throws(() => parseArguments(['--still']), /Usage/);
  assert.throws(() => parseArguments(['a.json', 'b.json', '--still']), /Name one shot/);
  assert.throws(() => parseArguments(['a.json', '--still', '--ratio', '4:3']), /--ratio is one of/);
  assert.throws(() => parseArguments(['a.json', '--still', '--ratio']), /--ratio needs a value/);
  assert.throws(() => parseArguments(['a.json', '--still', '--frames']), /Unknown option --frames/);
});

// The command run on a shot written to a folder of its own, with these arguments after it.
function shoot(shot, ...rest) {
  const folder = mkdtempSync(join(tmpdir(), 'promo-shot-'));
  try {
    const path = join(folder, 'the-shot.json');
    writeFileSync(path, JSON.stringify(shot));
    return spawnSync(process.execPath, [script, path, ...(rest.includes('--film') ? [] : ['--still']), ...rest], { encoding: 'utf8' });
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

test('a shot\'s film reads with its length, its rate and the spells cast, 30 fps and none by default', () => {
  assert.deepEqual(withShot({ ...good, film }).film, film);
  assert.deepEqual(withShot({ ...good, film: { seconds: 5 } }).film, { seconds: 5, fps: 30, casts: [] });
  assert.deepEqual(FRAME_RATES, [30, 60]);
});

test('a film too long, too short, at another rate, with an unknown key or casting no spell of the content is refused', () => {
  assert.throws(() => withShot({ ...good, film: { ...film, seconds: 0 } }), /"film\.seconds" is a whole number from 1 to 60/);
  assert.throws(() => withShot({ ...good, film: { ...film, seconds: LONGEST_FILM + 1 } }), /"film\.seconds"/);
  assert.throws(() => withShot({ ...good, film: { ...film, seconds: 2.5 } }), /"film\.seconds"/);
  assert.throws(() => withShot({ ...good, film: { ...film, fps: 24 } }), /"film\.fps" is 30 or 60/);
  assert.throws(() => withShot({ ...good, film: { ...film, music: 'on' } }), /unknown key "music" in "film"/);
  assert.throws(() => withShot({ ...good, film: { ...film, casts: ['spell:no_such_spell:v1'] } }), /names "spell:no_such_spell:v1", which no spell of data\/Spells\/ declares/);
  assert.throws(() => withShot({ ...good, film: { ...film, casts: 'spell:pummel:v1' } }), /"film\.casts" lists spell ids/);
  assert.throws(() => withShot({ ...good, film: [] }), /"film" is an object/);
});

test('the filmed seat buys nothing, keeps a Standard tempo and orders ties as rolled', () => {
  assert.deepEqual(answer({ waitingAsked: 6, options: { kind: 'Evolution', evolution: { creatures: [{ creature: 1, availableTiers: ['tier:berserker:v1'] }] } } }), { asked: 6, kind: 'Evolution', pass: true });
  assert.deepEqual(answer({ waitingAsked: 7, waitingCreature: null, options: { kind: 'Speed', speed: { missing: [2, 3] } } }), { asked: 7, kind: 'Speed', creature: 2, speed: 'Standard' });
  assert.deepEqual(answer({ waitingAsked: 7, waitingCreature: 3, options: { kind: 'Speed', speed: { missing: [2, 3] } } }).creature, 3);
  assert.deepEqual(answer({ waitingAsked: 8, options: { kind: 'TieOrder', tieOrder: { ties: [[2, 1], [3]] } } }), { asked: 8, kind: 'TieOrder', order: [2, 1, 3] });
  assert.equal(answer({ waitingAsked: null, options: { kind: 'Waiting' } }), null);
  assert.equal(answer(null), null);
});

test('the filmed seat casts the shot\'s spell when it can, else the first it can, on the first targets offered', () => {
  const legal = (candidates, castable = true, minTargets = 1, maxTargets = 1) => ({ minTargets, maxTargets, candidates, isCastable: castable });
  const action = { waitingAsked: 9, options: { kind: 'Action', action: { actor: 1, spells: [
    { actor: 1, spell: 'spell:strike:v1', legalTargets: legal([2]) },
    { actor: 1, spell: 'spell:pummel:v1', legalTargets: legal([2, 3]) },
    { actor: 1, spell: 'spell:fury:v1', legalTargets: legal([2], false) },
  ] } } };
  assert.deepEqual(answer(action, ['spell:pummel:v1']), { asked: 9, kind: 'Action', creature: 1, spell: 'spell:pummel:v1', targets: [2] });
  assert.equal(answer(action, ['spell:fury:v1']).spell, 'spell:strike:v1', 'a spell it cannot cast is not cast');
  assert.equal(answer(action).spell, 'spell:strike:v1');
  const spread = { ...action, options: { kind: 'Action', action: { actor: 1, spells: [{ actor: 1, spell: 'spell:sweep:v1', legalTargets: legal([2, 3, 4], true, 2, 3) }] } } };
  assert.deepEqual(answer(spread).targets, [2, 3]);
  const none = { ...action, options: { kind: 'Action', action: { actor: 1, spells: [{ actor: 1, spell: 'spell:fury:v1', legalTargets: legal([2], false) }] } } };
  assert.throws(() => answer(none), /can cast nothing/);
  assert.deepEqual(answer({ waitingAsked: 4, waitingCreature: 2, options: { kind: 'Intent', intent: { creatures: [{ creature: 1, castableSpells: ['spell:strike:v1'] }, { creature: 2, castableSpells: ['spell:guard:v1', 'spell:pummel:v1'] }] } } }, ['spell:pummel:v1']),
    { asked: 4, kind: 'Intent', creature: 2, spell: 'spell:pummel:v1' });
  assert.deepEqual(answer({ waitingAsked: 5, options: { kind: 'Target', target: { actor: 1, spell: 'spell:pummel:v1', legalTargets: legal([3, 2]) } } }), { asked: 5, kind: 'Target', targets: [3] });
});

test('frames end on whole milliseconds that add up to the film\'s length', () => {
  assert.deepEqual([0, 1, 2].map(index => frameEnd(index, 30)), [33, 67, 100]);
  assert.equal(frameEnd(89, 30), 3000);
  assert.equal(frameEnd(179, 60), 3000);
});

test('a sheet shows twelve frames evenly spaced, the first and the last among them, or all of a shorter film', () => {
  assert.deepEqual(sheetFrames(90), [0, 8, 16, 24, 32, 40, 49, 57, 65, 73, 81, 89]);
  assert.deepEqual(sheetFrames(5), [0, 1, 2, 3, 4]);
  assert.deepEqual(filmNames('9:16'), { frames: 'frames-9x16', film: 'film-9x16.mp4', sheet: 'sheet-9x16.png' });
});

test('a feed\'s margins are declared for every ratio, inside the picture', () => {
  assert.deepEqual(Object.keys(SAFE_ZONES).sort(), Object.keys(RATIOS).sort());
  for (const [ratio, zone] of Object.entries(SAFE_ZONES)) {
    for (const side of ['top', 'bottom', 'left', 'right']) assert.ok(zone[side] >= 0 && zone[side] < 0.5, `${ratio} ${side}`);
    assert.ok(zone.top + zone.bottom < 1 && zone.left + zone.right < 1, ratio);
  }
  assert.ok(SAFE_ZONES['9:16'].bottom > 0, 'the vertical feeds cover the foot of a clip');
});

test('a frames\' digest follows the frames and their order', () => {
  const [a, b] = [sha256(Buffer.from('a')), sha256(Buffer.from('b'))];
  assert.equal(framesDigest([a, b]), framesDigest([a, b]));
  assert.notEqual(framesDigest([a, b]), framesDigest([b, a]));
});

test('the command refuses --film for a shot without one, with exit 1', () => {
  const run = shoot(good, '--film');
  assert.equal(run.status, 1);
  assert.match(run.stderr, /the shot has no "film" to shoot/);
  assert.equal(run.stdout, '');
});

test('frames encode into an MP4 of the length, the size and the frame count asked', { skip: !hasEncoder() && 'ffmpeg and ffprobe are not on the PATH' }, () => {
  const folder = mkdtempSync(join(tmpdir(), 'promo-film-'));
  try {
    // Ninety frames of 64 by 112, drawn by ffmpeg itself: what a three-second film at 30 fps is.
    const drawn = spawnSync('ffmpeg', ['-loglevel', 'error', '-f', 'lavfi', '-i', 'testsrc=size=64x112:rate=30:duration=3', join(folder, '%06d.png')], { encoding: 'utf8' });
    assert.equal(drawn.status, 0, drawn.stderr);
    const out = join(folder, 'film.mp4');
    encodeFilm(folder, 30, out);
    const facts = filmFacts(out);
    assert.deepEqual({ ...facts, seconds: Math.round(facts.seconds * 10) / 10 }, { width: 64, height: 112, frames: 90, codec: 'h264', pixels: 'yuv420p', seconds: 3 });
    // Nothing of the moment or of the encoder's build is written in it: not x264's note of its version and options.
    assert.ok(!readFileSync(out).includes('x264 - core'), 'the encoder\'s note is cut');
    // The same frames give the same file.
    const again = join(folder, 'again.mp4');
    encodeFilm(folder, 30, again);
    assert.equal(sha256(readFileSync(out)), sha256(readFileSync(again)));
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});

test('the filmed seat closes a window first, then presses what a reading waits on, then answers once nothing is read', () => {
  const asked = { waitingFor: 'Action', waitingAsked: 8, over: false };
  assert.equal(actOn({ window: 'help-dialog', button: '#playback-next', reading: true }, asked), 'window help-dialog');
  assert.equal(actOn({ window: null, button: '#playback-next', reading: true }, asked), '#playback-next');
  assert.equal(actOn({ window: null, button: null, reading: true }, asked), null, 'a reading is let finish');
  assert.equal(actOn({ window: null, button: null, reading: false }, asked), 'asked 8');
  assert.equal(actOn({ window: null, button: null, reading: false }, { ...asked, over: true }), null);
  assert.equal(actOn({ window: null, button: null, reading: false }, { waitingFor: null }), null);
});
