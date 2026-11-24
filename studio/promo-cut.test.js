// The cut of a promotion (docs/art/promotion-plan.md, P3; ADR 0205): scripts/promo/cut.mjs and its cards.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { CARD_LIMITS, CUT_FORMAT, END_NOTE, LANGUAGES, assemble, assembly, brandMark, cardPages, cutName, framesOf, parseArguments, readCut } from '../scripts/promo/cut.mjs';
import { filmFacts, hasEncoder, root } from '../scripts/promo/shoot.mjs';

const script = fileURLToPath(new URL('../scripts/promo/cut.mjs', import.meta.url));
const cuts = join(root, 'promo/cuts');

// A filmed shot as readShot returns it, without a scenario to read.
const filmed = (name, change = {}) => ({ name, title: name, scenario: 'x.json', id: `sc-repo-${name}`, ratios: ['9:16', '16:9', '1:1'], size: 1080, film: { seconds: 12, fps: 30, casts: [] }, ...change });
const words = { en: 'A Brute pummels the other.', fr: 'Une Brute en frappe une autre.' };
const cut = (change = {}) => ({
  format: CUT_FORMAT, title: 'A test cut', ratio: '9:16',
  titleCard: { seconds: 2, en: 'Evolve your creatures.', fr: 'Fais évoluer tes créatures.' },
  shots: [{ shot: 'one', start: 1, seconds: 3, caption: words }],
  endCard: { seconds: 2 },
  ...change,
});

// A cut written to a folder of its own, read against shots named in `known`.
function withCut(value, known = { one: filmed('one') }) {
  const folder = mkdtempSync(join(tmpdir(), 'promo-cut-'));
  try {
    const shots = join(folder, 'shots');
    mkdirSync(shots);
    for (const name of Object.keys(known)) writeFileSync(join(shots, `${name}.json`), '{}');
    const path = join(folder, 'cut.json');
    writeFileSync(path, typeof value === 'string' ? value : JSON.stringify(value));
    return readCut(path, { shots, readOne: file => known[file.split(/[\\/]/).pop().replace('.json', '')] });
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
}

test('every cut of the repository reads, and its shots are the repository\'s filmed shots', () => {
  const files = readdirSync(cuts).filter(file => file.endsWith('.json'));
  assert.ok(files.length > 0, 'promo/cuts/ holds a cut');
  for (const file of files) {
    const read = readCut(join(cuts, file));
    assert.ok(read.shots.every(stretch => stretch.shot.film), file);
    const run = spawnSync(process.execPath, [script, join(cuts, file), '--check'], { encoding: 'utf8' });
    assert.equal(run.status, 0, run.stderr);
    assert.match(run.stdout, /stretch\(es\), [\d.]+ s at /);
  }
});

test('a cut reads with its shots, its words in each language and its length', () => {
  const read = withCut(cut());
  assert.equal(read.seconds, 7);
  assert.equal(read.size, 1080);
  assert.equal(read.fps, 30);
  assert.deepEqual(read.shots[0].caption, words);
  assert.equal(read.shots[0].shot.name, 'one');
  assert.equal(read.endCard.seconds, 2);
});

test('a cut naming a missing shot, a shot not filmed or not at its ratio is refused', () => {
  assert.throws(() => withCut(cut({ shots: [{ shot: 'no-such-shot', seconds: 2, caption: words }] })), /promo\/shots\/ does not hold/);
  assert.throws(() => withCut(cut(), { one: filmed('one', { film: null }) }), /a shot with no "film"/);
  assert.throws(() => withCut(cut(), { one: filmed('one', { ratios: ['16:9'] }) }), /not filmed at 9:16/);
});

test('a cut keeping seconds past its film, or off its frames, is refused', () => {
  assert.throws(() => withCut(cut({ shots: [{ shot: 'one', start: 10, seconds: 3, caption: words }] })), /whose film lasts 12 s/);
  assert.throws(() => withCut(cut({ shots: [{ shot: 'one', start: 0.01, seconds: 3, caption: words }] })), /on a frame/);
  assert.throws(() => withCut(cut({ shots: [{ shot: 'one', seconds: 0, caption: words }] })), /over 0/);
});

test('a caption or a title over its card, missing a language or on two lines, is refused', () => {
  const long = 'x'.repeat(CARD_LIMITS.caption['9:16'] + 1);
  assert.throws(() => withCut(cut({ shots: [{ shot: 'one', seconds: 2, caption: { ...words, fr: long } }] })), /shots\[0\]\.caption\.fr runs over its card/);
  assert.ok(withCut(cut({ ratio: '16:9', shots: [{ shot: 'one', seconds: 2, caption: { ...words, fr: long } }] })), 'the wider card holds it');
  assert.throws(() => withCut(cut({ titleCard: { en: 'x'.repeat(CARD_LIMITS.title['9:16'] + 1), fr: 'y' } })), /titleCard\.en runs over its card/);
  assert.throws(() => withCut(cut({ shots: [{ shot: 'one', seconds: 2, caption: { en: 'only English' } }] })), /caption\.fr is missing/);
  assert.throws(() => withCut(cut({ shots: [{ shot: 'one', seconds: 2, caption: { ...words, en: 'two\nlines' } }] })), /is one line/);
});

test('a cut with words on its end card, an unknown key, a card too long or a length past a feed\'s clip is refused', () => {
  assert.throws(() => withCut(cut({ endCard: { seconds: 2, en: 'Buy now' } })), /its words are the brand's/);
  assert.throws(() => withCut(cut({ music: 'x' })), /unknown key "music"/);
  assert.throws(() => withCut(cut({ titleCard: { ...cut().titleCard, seconds: 9 } })), /titleCard\.seconds is from 1 to 5/);
  const long = Array.from({ length: 6 }, () => ({ shot: 'one', seconds: 10, caption: words }));
  assert.throws(() => withCut(cut({ shots: long })), /past the 60 s/);
  assert.throws(() => withCut(cut({ format: 'x' })), /"format"/);
  assert.throws(() => withCut('{'), /not a cut's JSON/);
});

test('a cut of shots filmed at two sizes or two rates is refused', () => {
  const two = cut({ shots: [{ shot: 'one', seconds: 2, caption: words }, { shot: 'two', seconds: 2, caption: words }] });
  assert.throws(() => withCut(two, { one: filmed('one'), two: filmed('two', { size: 720 }) }), /at one size/);
  assert.throws(() => withCut(two, { one: filmed('one'), two: filmed('two', { film: { seconds: 12, fps: 60, casts: [] } }) }), /at one rate/);
});

test('the cards draw with the brand\'s tokens, not raw colours, as the pages do', () => {
  const css = readFileSync(join(root, 'scripts/promo/cards.css'), 'utf8');
  const declared = new Set([...readFileSync(join(root, 'docs/art/branding.css'), 'utf8').matchAll(/(--[\w-]+):/g)].map(match => match[1]));
  assert.doesNotMatch(css.replace(/\/\*[\s\S]*?\*\//g, ''), /#[0-9a-f]{3,8}\b|\b(?:rgba?|hsla?)\(|\b(?:white|black)\b/i, 'cards.css names a raw colour');
  for (const [, token] of css.matchAll(/var\((--[\w-]+)\)/g)) assert.ok(declared.has(token), `${token} is a token of branding.css`);
  const read = withCut(cut());
  for (const language of LANGUAGES) {
    const pages = cardPages(read, language);
    for (const html of [pages.title, ...pages.captions, pages.end]) {
      const own = html.replace(/<style>[\s\S]*<\/style>/, '');
      assert.doesNotMatch(own, /#[0-9a-f]{3,8}\b|\b(?:rgba?|hsla?)\(/i, 'a card\'s page names a raw colour');
      assert.match(html, new RegExp(`<html lang="${language}">`));
    }
  }
  assert.match(brandMark(), /fill="currentColor"/);
});

test('the end card says the site and the showcase\'s own line in each language', () => {
  assert.deepEqual(END_NOTE, { en: 'Beta · Free to play', fr: 'Bêta · Gratuit' });
  const pages = cardPages(withCut(cut()), 'fr');
  assert.match(pages.end, /downfallinstinct\.com/);
  assert.match(pages.end, /Bêta · Gratuit/);
  assert.match(pages.captions[0], /Une Brute en frappe une autre\./);
  assert.match(cardPages(withCut(cut({ titleCard: { en: 'Fire & <ice>', fr: 'x' } })), 'en').title, /Fire &amp; &lt;ice&gt;/);
});

test('a caption sits inside the margins a vertical feed covers', () => {
  const [caption] = cardPages(withCut(cut()), 'en').captions;
  assert.match(caption, /bottom:25%/);
  assert.match(caption, /right:13\.88/);
});

test('the assembly holds the cards for their seconds and keeps each stretch from its frame', () => {
  const read = withCut(cut());
  const args = assembly(read, { frames: ['/f/one'], cards: { title: 't.png', captions: ['c.png'], end: 'e.png' }, out: 'o.mp4' });
  assert.deepEqual(args.slice(args.indexOf('t.png') - 7, args.indexOf('t.png') + 1), ['-loop', '1', '-framerate', '30', '-t', '2', '-i', 't.png']);
  assert.ok(args.includes('-start_number') && args[args.indexOf('-start_number') + 1] === '30', 'the stretch starts at its first second\'s frame');
  const graph = args[args.indexOf('-filter_complex') + 1];
  assert.match(graph, /trim=end_frame=90/);
  assert.match(graph, /concat=n=3:v=1:a=0/);
  assert.ok(args.includes('-an') && args.at(-1) === 'o.mp4');
});

test('a stretch whose frames are not filmed yet is refused with the command that films them', () => {
  const read = withCut(cut());
  const folder = mkdtempSync(join(tmpdir(), 'promo-frames-'));
  try {
    assert.throws(() => framesOf(read, folder), /holds 0 frames, the cut needs 120: run node scripts\/promo\/shoot\.mjs promo\/shots\/one\.json --film --ratio 9:16/);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});

test('the arguments name one cut, an option it knows and a language it says', () => {
  assert.deepEqual(parseArguments(['c.json', '--lang', 'fr', '--check']), { cut: 'c.json', check: true, lang: 'fr', out: null, frames: null });
  assert.throws(() => parseArguments([]), /Usage/);
  assert.throws(() => parseArguments(['c.json', '--lang', 'de']), /--lang is one of en, fr/);
  assert.throws(() => parseArguments(['c.json', '--fast']), /Unknown option --fast/);
  assert.throws(() => parseArguments(['a.json', 'b.json']), /Name one cut/);
  assert.equal(cutName('fr', '9:16'), 'cut-fr-9x16.mp4');
});

test('the command refuses a cut it cannot read, with exit 1 and the reason', () => {
  const folder = mkdtempSync(join(tmpdir(), 'promo-cut-'));
  try {
    const path = join(folder, 'broken.json');
    writeFileSync(path, JSON.stringify(cut({ shots: [{ shot: 'no-such-shot', seconds: 2, caption: words }] })));
    const run = spawnSync(process.execPath, [script, path, '--check'], { encoding: 'utf8' });
    assert.equal(run.status, 1);
    assert.match(run.stderr, /promo\/shots\/ does not hold/);
    assert.equal(spawnSync(process.execPath, [script], { encoding: 'utf8' }).status, 1);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});

test('cards and frames assemble into one MP4 of the cut\'s length, size and frame count', { skip: !hasEncoder() && 'ffmpeg and ffprobe are not on the PATH' }, () => {
  const read = { ...withCut(cut({ titleCard: { seconds: 1, en: 'a', fr: 'b' }, endCard: { seconds: 1 } }), { one: filmed('one', { size: 720 }) }), size: 720 };
  const folder = mkdtempSync(join(tmpdir(), 'promo-assembly-'));
  const ffmpeg = args => assert.equal(spawnSync('ffmpeg', ['-y', '-loglevel', 'error', ...args]).status, 0);
  try {
    const frames = join(folder, 'frames');
    mkdirSync(frames);
    // Four seconds of frames, drawn by ffmpeg; cards of the size, the caption on a clear ground.
    ffmpeg(['-f', 'lavfi', '-i', 'testsrc2=size=720x1280:rate=30', '-frames:v', '120', '-start_number', '0', join(frames, '%06d.png')]);
    ffmpeg(['-f', 'lavfi', '-i', 'color=c=gray:size=720x1280', '-frames:v', '1', join(folder, 't.png')]);
    ffmpeg(['-f', 'lavfi', '-i', 'color=c=gray@0.0:size=720x1280,format=rgba', '-frames:v', '1', join(folder, 'c.png')]);
    const files = { frames: [frames], cards: { title: join(folder, 't.png'), captions: [join(folder, 'c.png')], end: join(folder, 't.png') } };
    assemble(read, { ...files, out: join(folder, 'a.mp4') });
    assemble(read, { ...files, out: join(folder, 'b.mp4') });
    const facts = filmFacts(join(folder, 'a.mp4'));
    assert.deepEqual({ width: facts.width, height: facts.height, frames: facts.frames, codec: facts.codec, pixels: facts.pixels }, { width: 720, height: 1280, frames: 150, codec: 'h264', pixels: 'yuv420p' });
    assert.ok(Math.abs(facts.seconds - 5) < 0.1, `${facts.seconds} s`);
    assert.ok(readFileSync(join(folder, 'a.mp4')).equals(readFileSync(join(folder, 'b.mp4'))), 'one cut gives one file');
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});
