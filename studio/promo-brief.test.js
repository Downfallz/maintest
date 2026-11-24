// The brief of a promotion's generated part (docs/art/promotion-plan.md, P4; ADR 0205): scripts/promo/brief.mjs.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  BRIEF_FORMAT, EFFECT_WORDS, GENERATED_FORMAT, KINDS, RETURNS, clipArguments, effectWords, fileType, intake, isDay, palette,
  parseArguments, prompt, readBrief, readGenerated, references, shownForms, shownWords, spellsById, writePack,
} from '../scripts/promo/brief.mjs';
import { readShot } from '../scripts/promo/shoot.mjs';

const repository = fileURLToPath(new URL('..', import.meta.url));
const script = join(repository, 'scripts/promo/brief.mjs');
const keyArt = join(repository, 'promo/briefs/brute-pummel-key-art.json');
const png = Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), Buffer.from('a picture')]);
const box = brand => Buffer.concat([Buffer.from([0, 0, 0, 24]), Buffer.from(`ftyp${brand}`), Buffer.alloc(12)]);
const mp4 = box('isom');
const base = { format: BRIEF_FORMAT, title: 'A key art', makes: 'key-art', shot: 'brute-pummel-round-3', ratio: '9:16', frames: [6.5], ask: 'The Brute hammers.' };

// A folder of its own, removed after.
function inFolder(run) {
  const folder = mkdtempSync(join(tmpdir(), 'promo-brief-'));
  try {
    return run(folder);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
}

// A brief written to a folder of its own, read back.
const withBrief = brief => inFolder(folder => {
  const path = join(folder, 'a-brief.json');
  writeFileSync(path, typeof brief === 'string' ? brief : JSON.stringify(brief));
  return readBrief(path, { shots: join(repository, 'promo/shots') });
});

test('the repository\'s brief reads, at its shot\'s ratio, on two frames of its film', () => {
  const brief = readBrief(keyArt);
  assert.equal(brief.name, 'brute-pummel-key-art');
  assert.equal(brief.shot.name, 'brute-pummel-round-3');
  assert.deepEqual(brief.frames.map(one => [one.frame, one.file]), [[195, 'frame-6.50s.png'], [210, 'frame-7.00s.png']]);
  assert.deepEqual(references(brief), ['frame-6.50s.png', 'frame-7.00s.png']);
});

test('the pack names the real creature and the real spell in the game\'s own words', () => {
  const words = shownWords(readShot(join(repository, 'promo/shots/brute-pummel-round-3.json')));
  assert.deepEqual(words.creatures.map(one => one.name), ['Brute']);
  assert.match(words.creatures[0].look, /enormous front paws/);
  assert.deepEqual(words.spells, [{ id: 'spell:pummel:v1', name: 'Pummel', does: 'deals 2 damage to one enemy', cue: 'Hammer with a huge front paw' }]);
  assert.match(words.anatomy, /bone-mask skull/);
  assert.match(words.ground, /golden eclipse/);
  const text = prompt(readBrief(keyArt));
  for (const words of ['of the Brute family: heavy, low quadruped, enormous front paws', 'The spell Pummel, which deals 2 damage to one enemy',
    'hammer with a huge front paw', 'frame-6.50s.png, the first of 2', '1080 by 1920', 'gold #e7c183', 'stays off the creatures\' bodies', 'No words, letters, numbers or logo']) {
    assert.ok(text.includes(words), `the prompt says "${words}"`);
  }
  assert.doesNotMatch(text, /restyled|--brand-|on accent|panel/, 'a key art is not a restyled clip, and a model reads colours, not the pages\' tokens');
});

test('the palette is the brand\'s tokens, as branding.css declares them', () => {
  const tokens = palette();
  assert.ok(tokens.some(one => one.token === '--brand-slate' && one.colour === '#0b1519'));
  assert.ok(tokens.every(one => one.token.startsWith('--brand-') && /^#[0-9a-f]{6}$/.test(one.colour)));
});

test('a restyled clip keeps its stretch of the film, and its prompt keeps every move', () => {
  const brief = withBrief({ ...base, makes: 'restyled-clip', frames: undefined, clip: { start: 6, seconds: 2 } });
  assert.deepEqual(brief.clip, { start: 6, seconds: 2, first: 180, count: 60, file: 'clip-9x16.mp4' });
  assert.deepEqual(references(brief), ['clip-9x16.mp4']);
  assert.match(prompt(brief), /Keep every move, its timing and the camera frame for frame.*restyled/);
  const args = clipArguments('frames', 30, brief.clip, 'out.mp4');
  assert.deepEqual(args.slice(args.indexOf('-start_number'), args.indexOf('-start_number') + 2), ['-start_number', '180']);
  assert.deepEqual(args.slice(args.indexOf('-frames:v'), args.indexOf('-frames:v') + 2), ['-frames:v', '60']);
  assert.ok(args.includes('libx264') && args.at(-1) === 'out.mp4');
});

test('a brief is refused for a kind a model may not make, a missing shot, another ratio, frames off the film or anything unknown', () => {
  const refused = (change, why) => assert.throws(() => withBrief({ ...base, ...change }), why);
  refused({ makes: 'gameplay' }, /Gameplay from a prompt alone is not one/);
  refused({ shot: 'no-such-shot' }, /promo\/shots\/ does not hold/);
  refused({ ratio: '4:3' }, /one of the shot's/);
  refused({ frames: [12] }, /past the 12 s/);
  refused({ frames: [1.01] }, /on a frame/);
  refused({ frames: [1, 1] }, /a frame twice/);
  refused({ frames: [] }, /one to four times/);
  refused({ shot: 'brute-mirror-round-3' }, /has none: leave them out for its still/);
  refused({ makes: 'restyled-clip' }, /gives its "clip"/);
  refused({ makes: 'restyled-clip', clip: { start: 11, seconds: 2 } }, /whose film lasts 12 s/);
  refused({ clip: { seconds: 1 } }, /for a restyled clip only/);
  refused({ makes: 'transition' }, /two of the shot's film at least/);
  refused({ makes: 'transition', frames: undefined }, /two of the shot's film at least/);
  refused({ ask: ' ' }, /"ask"/);
  refused({ title: '' }, /"title"/);
  refused({ extra: 1 }, /unknown key "extra"/);
  refused({ format: 'x' }, /"format"/);
  assert.throws(() => withBrief('{'), /not a brief's JSON/);
  assert.deepEqual(Object.keys(KINDS), ['key-art', 'background', 'transition', 'styled-still', 'thumbnail', 'restyled-clip']);
});

test('a brief on a shot\'s still hands the model the still, and the pack copies it', () => inFolder(folder => {
  const brief = withBrief({ ...base, shot: 'brute-mirror-round-3', frames: undefined });
  assert.deepEqual(references(brief), ['still-9x16.png']);
  assert.match(prompt(brief), /the attached frame of Downfall Instinct/);
  assert.throws(() => writePack(brief, { from: join(folder, 'shot'), out: join(folder, 'pack') }), /still-9x16\.png is missing: run .* --still --ratio 9:16/);
  mkdirSync(join(folder, 'shot'));
  writeFileSync(join(folder, 'shot/still-9x16.png'), png);
  assert.deepEqual(writePack(brief, { from: join(folder, 'shot'), out: join(folder, 'pack') }).references.map(one => one.file), ['still-9x16.png']);
  assert.deepEqual(readFileSync(join(folder, 'pack/still-9x16.png')), png);
}));

test('a clip\'s pack is refused until its last frame is filmed', () => inFolder(folder => {
  const clip = withBrief({ ...base, makes: 'restyled-clip', frames: undefined, clip: { start: 6, seconds: 2 } });
  assert.throws(() => writePack(clip, { from: join(folder, 'shot'), out: join(folder, 'pack'), encode: () => {} }), /000239\.png is missing: run .* --film --ratio 9:16/);
  assert.match(prompt(clip), /the attached clip of Downfall Instinct/);
}));

test('a background names no creature, and asks for none', () => {
  const text = prompt(withBrief({ ...base, makes: 'background', ask: 'The ground at dusk.' }));
  assert.match(text, /No creature: the background is the ground alone/);
  assert.doesNotMatch(text, /^- Brute|Keep each creature|The spell/m);
  assert.match(text, /golden eclipse/);
});

test('each creature on the field is named once with how many take its form, at its last form', () => {
  const action = (creature, tier) => ({ kind: 'Evolution', creature, tier });
  const match = { rules: { teamSize: 2 } };
  assert.deepEqual(shownForms({ match, before: [action(1, 'tier:brute:v1'), action(1, 'tier:titan:v1'), action(3, 'tier:brute:v1')] }), ['tier:titan:v1', null, 'tier:brute:v1', null]);
  const words = shownWords(readShot(join(repository, 'promo/shots/brute-pummel-round-3.json')));
  assert.deepEqual(words.creatures.map(one => [one.name, one.count]), [['Brute', 2]]);
  assert.match(prompt(readBrief(keyArt)), /Brute \(two on the field\), of the Brute family/);
});

test('every effect the content declares is said in a player\'s words', () => {
  const kinds = new Set([...spellsById(join(repository, 'data/Spells')).values()].flatMap(spell => (spell.effects ?? []).map(effect => effect.kind)));
  for (const kind of kinds) assert.ok(Object.hasOwn(EFFECT_WORDS, kind), `EFFECT_WORDS says what ${kind} does`);
  assert.equal(effectWords({ kind: 'Heal', amount: 4 }), 'heals 4 health');
  assert.equal(effectWords({ kind: 'DefenseBuff', amount: 3, durationRounds: 2 }), 'raises defense by 3 for 2 rounds');
  assert.equal(effectWords({ kind: 'Bleed', amountPerRound: 2, durationRounds: 1 }), 'bleeds 2 health a round for 1 round');
  assert.equal(effectWords({ kind: 'Stun', durationRounds: 1 }), 'stuns for 1 round');
  assert.throws(() => effectWords({ kind: 'Teleport' }), /teach it EFFECT_WORDS/);
});

test('a day is a day of the calendar', () => {
  assert.equal(isDay('2026-11-24'), true);
  for (const day of ['2026-13-01', '2026-02-30', '24/11/2026', 20261124, '']) assert.equal(isDay(day), false, String(day));
});

test('the pack copies the frames as shot, writes the prompt and says what it holds by hash', () => inFolder(folder => {
  const brief = readBrief(keyArt);
  const from = join(folder, 'shot');
  assert.throws(() => writePack(brief, { from, out: join(folder, 'pack') }), /is missing: run node scripts\/promo\/shoot\.mjs promo\/shots\/brute-pummel-round-3\.json --film --ratio 9:16/);
  mkdirSync(join(from, 'frames-9x16'), { recursive: true });
  writeFileSync(join(from, 'frames-9x16/000195.png'), png);
  writeFileSync(join(from, 'frames-9x16/000210.png'), Buffer.concat([png, Buffer.from('2')]));
  const pack = writePack(brief, { from, out: join(folder, 'pack') });
  assert.deepEqual(readFileSync(join(folder, 'pack/frame-6.50s.png')), png);
  assert.equal(readFileSync(join(folder, 'pack/prompt.md'), 'utf8'), prompt(brief));
  assert.deepEqual(pack.references.map(one => one.file), ['frame-6.50s.png', 'frame-7.00s.png']);
  assert.deepEqual(JSON.parse(readFileSync(join(folder, 'pack/pack.json'), 'utf8')), pack);
}));

test('a file\'s type is read from its bytes, not its name', () => {
  assert.equal(fileType(png), 'png');
  assert.equal(fileType(Buffer.from([0xff, 0xd8, 0xff, 0xe0])), 'jpg');
  assert.equal(fileType(Buffer.concat([Buffer.from('RIFF'), Buffer.alloc(4), Buffer.from('WEBP')])), 'webp');
  assert.equal(fileType(mp4), 'mp4');
  assert.equal(fileType(box('heic')), null, 'HEIC shares the ftyp box, and is not an MP4');
  assert.equal(fileType(box('avif')), null);
  assert.equal(fileType(Buffer.from('<svg>')), null);
});

// A pack of the key art written to `folder/pack` from frames made up for it, as the owner was handed it.
function packIn(folder, brief = readBrief(keyArt)) {
  const from = join(folder, 'shot');
  mkdirSync(join(from, 'frames-9x16'), { recursive: true });
  if (brief.clip) writeFileSync(join(from, 'frames-9x16', `${String(brief.clip.first + brief.clip.count - 1).padStart(6, '0')}.png`), png);
  for (const one of brief.frames) writeFileSync(join(from, 'frames-9x16', `${String(one.frame).padStart(6, '0')}.png`), Buffer.concat([png, Buffer.from(String(one.frame))]));
  return writePack(brief, { from, out: join(folder, 'pack'), encode: (_, __, ___, out) => writeFileSync(out, mp4) });
}

// The intake run in a folder of its own: the owner's file, the pack, the record and the store there.
function intakeIn(folder, bytes, options, brief = readBrief(keyArt)) {
  const file = join(folder, 'from-the-owner.bin');
  writeFileSync(file, bytes);
  return intake(brief, file, { until: '2026-11-24', pack: join(folder, 'pack'), record: join(folder, 'generated.json'), store: join(folder, 'store'), ...options });
}

test('an intake without its model, its day, its pack or its prompt is refused, and records nothing', () => inFolder(folder => {
  assert.throws(() => intakeIn(folder, png, { model: 'ChatGPT', date: '2026-11-24' }), /pack\.json is missing/);
  packIn(folder);
  assert.throws(() => intakeIn(folder, png, { date: '2026-11-24' }), /name the model that made it/);
  assert.throws(() => intakeIn(folder, png, { model: ' ', date: '2026-11-24' }), /name the model/);
  assert.throws(() => intakeIn(folder, png, { model: 'ChatGPT' }), /give the day it was made/);
  assert.throws(() => intakeIn(folder, png, { model: 'ChatGPT', date: '2026-02-30' }), /give the day it was made/);
  assert.throws(() => intakeIn(folder, png, { model: 'ChatGPT', date: '2026-13-01' }), /give the day it was made/);
  assert.throws(() => intakeIn(folder, png, { model: 'ChatGPT', date: '2026-11-25' }), /after today/);
  assert.throws(() => intakeIn(folder, png, { model: 'ChatGPT', date: '2026-11-24', promptText: '' }), /give the prompt/);
  assert.throws(() => intakeIn(folder, mp4, { model: 'ChatGPT', date: '2026-11-24' }), /a key-art comes back as png, jpg, webp; this file is mp4/);
  writeFileSync(join(folder, 'pack/prompt.md'), 'another prompt');
  assert.throws(() => intakeIn(folder, png, { model: 'ChatGPT', date: '2026-11-24' }), /prompt\.md changed since the pack was written/);
  assert.equal(existsSync(join(folder, 'generated.json')), false);
}));

test('an intake keeps the file unchanged under its hash, with the pack it answers, once', () => inFolder(folder => {
  const pack = packIn(folder);
  const line = intakeIn(folder, png, { model: 'ChatGPT, GPT-image-1', date: '2026-11-24' });
  assert.equal(line.model, 'ChatGPT, GPT-image-1');
  assert.equal(line.prompt, prompt(readBrief(keyArt)));
  assert.deepEqual(line.references, pack.references);
  assert.deepEqual(line.references.map(one => one.file), ['frame-6.50s.png', 'frame-7.00s.png']);
  assert.equal(line.blob, `generated/brute-pummel-key-art/${line.sha256}.png`);
  assert.deepEqual(readFileSync(join(folder, 'store/brute-pummel-key-art', `${line.sha256}.png`)), png);
  assert.deepEqual(readGenerated(join(folder, 'generated.json'), { until: '2026-11-24' }), [line]);
  assert.throws(() => intakeIn(folder, png, { model: 'ChatGPT', date: '2026-11-24' }), /holds it already/);
  const changed = intakeIn(folder, Buffer.concat([png, Buffer.from('2')]), { model: 'Gemini', date: '2026-11-24', promptText: 'the prompt as the owner changed it' });
  assert.equal(changed.prompt, 'the prompt as the owner changed it');
}));

test('a restyled clip comes back as an MP4, a transition as a picture or a video', () => inFolder(folder => {
  const clip = withBrief({ ...base, makes: 'restyled-clip', frames: undefined, clip: { start: 6, seconds: 2 } });
  const pack = packIn(folder, clip);
  assert.deepEqual(readFileSync(join(folder, 'pack/clip-9x16.mp4')), mp4);
  assert.deepEqual(pack.references.map(one => one.file), ['clip-9x16.mp4']);
  assert.throws(() => intakeIn(folder, png, { model: 'Runway', date: '2026-11-24' }, clip), /a restyled-clip comes back as mp4; this file is png/);
  assert.throws(() => intakeIn(folder, box('qt  '), { model: 'Runway', date: '2026-11-24' }, clip), /this file is none of/);
  assert.equal(intakeIn(folder, mp4, { model: 'Runway', date: '2026-11-24' }, clip).type, 'mp4');
  assert.deepEqual(RETURNS.transition, ['png', 'jpg', 'webp', 'mp4']);
}));

test('the repository\'s record reads, and a line without its provenance is refused', () => inFolder(folder => {
  assert.ok(Array.isArray(readGenerated(join(repository, 'promo/generated.json'))));
  const hash = 'b'.repeat(64);
  const refs = [{ file: 'frame-6.50s.png', sha256: 'c'.repeat(64) }, { file: 'frame-7.00s.png', sha256: 'd'.repeat(64) }];
  const line = { brief: 'brute-pummel-key-art', makes: 'key-art', shot: 'brute-pummel-round-3', ratio: '9:16', references: refs, type: 'png', sha256: hash, bytes: 10, model: 'Gemini', date: '2026-11-24', prompt: 'p', blob: `generated/brute-pummel-key-art/${hash}.png` };
  const read = files => {
    const path = join(folder, 'generated.json');
    writeFileSync(path, JSON.stringify({ format: GENERATED_FORMAT, files }));
    return readGenerated(path, { until: '2026-11-24' });
  };
  assert.deepEqual(read([line]), [line]);
  const refused = (change, why) => assert.throws(() => read([{ ...line, ...change }]), why);
  refused({ model: '' }, /names the model/);
  refused({ date: 'yesterday' }, /the day it was made/);
  refused({ date: '2026-13-01' }, /the day it was made/);
  refused({ date: '2999-01-01' }, /no later than today/);
  refused({ references: ['frame-6.50s.png', 'frame-7.00s.png'] }, /each \{"file", "sha256"\}/);
  refused({ references: [refs[1], refs[0]] }, /each \{"file", "sha256"\}/);
  refused({ type: 'exe', blob: `generated/brute-pummel-key-art/${hash}.exe` }, /png, jpg, webp file/);
  refused({ type: 'mp4', blob: `generated/brute-pummel-key-art/${hash}.mp4` }, /png, jpg, webp file/);
  refused({ shot: 42 }, /the brief's key-art of brute-pummel-round-3 at 9:16/);
  refused({ ratio: null }, /the brief's key-art/);
  refused({ makes: 'restyled-clip' }, /the brief's key-art/);
  refused({ brief: 123 }, /promo\/briefs\/ does not hold/);
  refused({ prompt: '' }, /keeps the prompt/);
  refused({ references: [] }, /references it started from/);
  refused({ brief: 'no-such-brief' }, /promo\/briefs\/ does not hold/);
  refused({ makes: 'gameplay' }, /the brief's key-art/);
  refused({ blob: 'elsewhere.png' }, /its hash under its brief/);
  const { model, ...noModel } = line;
  assert.ok(model);
  assert.throws(() => read([noModel]), /has exactly/);
  assert.throws(() => read([line, line]), /twice/);
}));

test('the command prints the prompt, refuses a bad brief or an intake without provenance, with exit codes', () => {
  const run = args => spawnSync(process.execPath, [script, ...args], { encoding: 'utf8', cwd: repository });
  const check = run([keyArt, '--check']);
  assert.equal(check.status, 0, check.stderr);
  assert.equal(check.stdout, prompt(readBrief(keyArt)));
  const missing = run(['promo/briefs/no-such-brief.json', '--check']);
  assert.equal(missing.status, 1);
  assert.match(missing.stderr, /not a brief's JSON/);
  const bare = run([keyArt, '--intake', keyArt, '--date', '2026-11-24']);
  assert.equal(bare.status, 1);
  assert.match(bare.stderr, /name the model that made it/);
  assert.throws(() => parseArguments([]), /Usage/);
  assert.throws(() => parseArguments([keyArt, '--model', 'x']), /go with --intake/);
  assert.throws(() => parseArguments([keyArt, '--check', '--intake', 'f']), /not both/);
  assert.throws(() => parseArguments([keyArt, '--nope']), /Unknown option/);
  assert.throws(() => parseArguments([keyArt, '--intake', 'f', '--model', '--date', '2026-11-24']), /--model needs a value/);
  assert.throws(() => parseArguments([keyArt, '--check', '--frames', 'x']), /--frames goes with writing a pack/);
  assert.throws(() => parseArguments([keyArt, '--check', '--out', 'x']), /--out goes with/);
});
