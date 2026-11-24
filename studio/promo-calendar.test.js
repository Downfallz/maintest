// The promotion's calendar (docs/art/promotion-plan.md, P5): scripts/promo/calendar.mjs.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { CHANNELS, RESTYLED, montrealToday, readCalendar, readPost } from '../scripts/promo/calendar.mjs';
import { montrealToday as shootToday } from '../scripts/promo/shoot.mjs';
import { spellNamesFr } from '../showcase/field-notes.mjs';

const root = fileURLToPath(new URL('..', import.meta.url));
const calendar = join(root, 'promo/calendar');
const script = join(root, 'scripts/promo/calendar.mjs');
const hash = 'b'.repeat(64);
const today = '2026-11-24';

const post = ({ head = {}, links = [], en = 'Your Brute answers with Pummel.', fr = 'Ta Brute riposte avec Pummel.', title = 'The Brute answers' } = {}) => [
  '---',
  ...Object.entries({ hour: '18:00', channels: 'bluesky, x', media: 'cut brute-pummel-answer', status: 'draft', ...head })
    .filter(([, value]) => value !== undefined)
    .map(([key, value]) => `${key}: ${value}`),
  ...links,
  '---',
  '',
  ...(title === null ? [] : [`# ${title}`, '']),
  ...(en === null ? [] : ['## en', '', en, '']),
  ...(fr === null ? [] : ['## fr', '', fr, '']),
].join('\n');

// A post written under its name in a folder of its own, read back, with the lines of generated files it may name
// (the record's own checks are brief.mjs's, held by promo-brief.test.js); `record` writes a record file instead.
function withPost(text, { name = '2026-12-01-brute-answer.md', generated = [], record } = {}) {
  const folder = mkdtempSync(join(tmpdir(), 'promo-calendar-'));
  try {
    const path = join(folder, name);
    writeFileSync(path, text);
    if (record === undefined) return readPost(path, { today, readRecord: () => generated });
    const file = join(folder, 'generated.json');
    if (record !== null) writeFileSync(file, record);
    return readPost(path, { today, generated: file });
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
}

test('the repository\'s calendar reads, and the command says how many posts it holds', () => {
  assert.ok(readCalendar(calendar, { today }).length > 0);
  const run = spawnSync(process.execPath, [script], { encoding: 'utf8' });
  assert.equal(run.status, 0, run.stderr);
  assert.match(run.stdout, /promo\/calendar: \d+ post\(s\), \d+ draft, \d+ approved, \d+ published\./);
});

test('a draft reads with its date, channels, media and words in each language', () => {
  const read = withPost(post());
  assert.equal(read.date, '2026-12-01');
  assert.deepEqual(read.channels, ['bluesky', 'x']);
  assert.deepEqual(read.media, [{ kind: 'cut', name: 'brute-pummel-answer', ratio: '9:16' }]);
  assert.equal(read.words.en, 'Your Brute answers with Pummel.');
  assert.equal(read.words.fr, 'Ta Brute riposte avec Pummel.');
  assert.equal(read.status, 'draft');
  assert.deepEqual(read.links, {});
});

test('a post names its date in its name, a real day', () => {
  assert.throws(() => withPost(post(), { name: 'brute-answer.md' }), /named by its date/);
  assert.throws(() => withPost(post(), { name: '2026-02-30-brute-answer.md' }), /not a day/);
  assert.throws(() => withPost(post(), { name: '2026-12-01-Brute.md' }), /named by its date/);
});

test('a post without its media, or naming media the repository does not hold, is refused', () => {
  assert.throws(() => withPost(post({ head: { media: undefined } })), /"media"/);
  assert.throws(() => withPost(post({ head: { media: '' } })), /"media"/);
  assert.throws(() => withPost(post({ head: { media: 'cut no-such-cut' } })), /promo\/cuts\/ does not hold/);
  assert.throws(() => withPost(post({ head: { media: 'still no-such-shot 9:16' } })), /promo\/shots\/ does not hold/);
  assert.throws(() => withPost(post({ head: { media: 'still brute-pummel-round-3 4:3' } })), /not taken at 4:3/);
  assert.throws(() => withPost(post({ head: { media: 'film brute-mirror-round-3 9:16' } })), /has no film/);
  assert.throws(() => withPost(post({ head: { media: `generated ${hash}` } })), /promo\/generated\.json does not hold/);
  assert.throws(() => withPost(post({ head: { media: 'picture art.png' } })), /"cut <cut>", "still <shot> <ratio>"/);
  assert.throws(() => withPost(post({ head: { media: 'cut brute-pummel-answer, cut brute-pummel-answer' } })), /twice/);
  assert.throws(() => withPost(post({ head: { media: 'still brute-pummel-round-3' } })), /"still <shot> <ratio>"/);
});

test('a post naming a generated file is refused when the record is not one brief.mjs reads', () => {
  const head = { media: `generated ${hash}` };
  assert.throws(() => withPost(post({ head }), { record: '{' }), /names a generated file, and .*not the record's JSON/s);
  assert.throws(() => withPost(post({ head }), { record: JSON.stringify({ format: 'downfall.promo.generated.v1', files: [{ sha256: hash, ratio: '9:16' }] }) }), /names a generated file/);
  assert.throws(() => withPost(post({ head }), { record: null }), /the record .*generated\.json is missing/);
});

test('a still, a film and a generated file are media too, the shot\'s and the record\'s', () => {
  const generated = [{ brief: 'brute-pummel-key-art', makes: 'key-art', shot: 'brute-pummel-round-3', ratio: '9:16', sha256: hash }];
  const read = withPost(post({ head: { media: `still brute-pummel-round-3 9:16, film brute-pummel-round-3 9:16, generated ${hash}` } }), { generated });
  assert.deepEqual(read.media, [
    { kind: 'still', name: 'brute-pummel-round-3', ratio: '9:16' },
    { kind: 'film', name: 'brute-pummel-round-3', ratio: '9:16' },
    { kind: 'generated', name: hash, ratio: '9:16', makes: 'key-art' },
  ]);
});

test('a post of a restyled clip says it is restyled, in each language', () => {
  const generated = [{ brief: 'brute-pummel-restyled', makes: 'restyled-clip', shot: 'brute-pummel-round-3', ratio: '9:16', sha256: hash }];
  const head = { media: `generated ${hash}` };
  assert.throws(() => withPost(post({ head }), { generated }), /restyled/);
  assert.throws(() => withPost(post({ head, en: `Your Brute answers with Pummel. ${RESTYLED.en}` }), { generated }), new RegExp(RESTYLED.fr));
  const read = withPost(post({ head, en: `Your Brute answers with Pummel. ${RESTYLED.en}`, fr: `Ta Brute riposte avec Pummel. ${RESTYLED.fr}` }), { generated });
  assert.equal(read.media[0].makes, 'restyled-clip');
});

test('a post gives the hour the owner posts it, Montreal\'s, on a 24-hour clock', () => {
  assert.equal(withPost(post()).hour, '18:00');
  assert.equal(withPost(post({ head: { hour: '07:05' } })).hour, '07:05');
  assert.throws(() => withPost(post({ head: { hour: undefined } })), /"hour" is when the owner posts it/);
  for (const hour of ['7:05', '24:00', '18:60', '6 pm', '18h00']) assert.throws(() => withPost(post({ head: { hour } })), /"hour"/, hour);
});

test('a post shows each medium at a ratio each of its channels shows whole', () => {
  assert.deepEqual(Object.fromEntries(Object.entries(CHANNELS).map(([channel, { ratios }]) => [channel, ratios])), {
    'youtube-shorts': ['9:16', '1:1'], tiktok: ['9:16'], 'instagram-reels': ['9:16'],
    reddit: ['9:16', '16:9', '1:1'], x: ['9:16', '16:9', '1:1'], bluesky: ['9:16', '16:9', '1:1'], discord: ['9:16', '16:9', '1:1'],
  });
  assert.equal(withPost(post({ head: { channels: 'tiktok, reddit', media: 'cut brute-pummel-answer' } })).channels.length, 2);
  assert.throws(() => withPost(post({ head: { channels: 'reddit, tiktok', media: 'still brute-mirror-round-3 16:9' } })), /the still brute-mirror-round-3 at 16:9, and tiktok takes 9:16/);
  assert.throws(() => withPost(post({ head: { channels: 'instagram-reels', media: 'still brute-mirror-round-3 1:1' } })), /instagram-reels takes 9:16/);
  assert.equal(withPost(post({ head: { channels: 'youtube-shorts', media: 'still brute-mirror-round-3 1:1' } })).media[0].ratio, '1:1');
  const generated = [{ brief: 'brute-pummel-key-art', makes: 'key-art', shot: 'brute-pummel-round-3', ratio: '16:9', sha256: hash }];
  assert.throws(() => withPost(post({ head: { channels: 'youtube-shorts', media: `generated ${hash}` } }), { generated }), /the generated .* at 16:9, and youtube-shorts takes 9:16 or 1:1/);
});

test('a post names the channels of the launch, each once', () => {
  assert.deepEqual(Object.keys(CHANNELS), ['youtube-shorts', 'tiktok', 'instagram-reels', 'reddit', 'x', 'bluesky', 'discord']);
  assert.throws(() => withPost(post({ head: { channels: undefined } })), /"channels"/);
  assert.throws(() => withPost(post({ head: { channels: 'facebook' } })), /"facebook" is not one of/);
  assert.throws(() => withPost(post({ head: { channels: 'x, x' } })), /twice/);
});

test('a caption over a channel\'s limit is refused, in either language, counted in characters', () => {
  const long = 'é'.repeat(CHANNELS.x.limit + 1);
  assert.throws(() => withPost(post({ en: long })), /en.*281 characters.*x takes 280/s);
  assert.throws(() => withPost(post({ fr: long })), /fr.*x takes 280/s);
  assert.throws(() => withPost(post({ head: { channels: 'youtube-shorts' }, en: 'a'.repeat(101) })), /youtube-shorts takes 100/);
  assert.equal(withPost(post({ head: { channels: 'discord' }, en: 'a'.repeat(400) })).words.en.length, 400);
  assert.equal(withPost(post({ head: { channels: 'bluesky' }, en: '😀'.repeat(CHANNELS.bluesky.limit / 2) })).channels.length, 1);
  assert.throws(() => withPost(post({ head: { channels: 'bluesky' }, en: '😀'.repeat(CHANNELS.bluesky.limit / 2 + 1) })), /bluesky takes 300/);
});

test('X counts its own way: an emoji or a French narrow space twice, an address as 23', () => {
  assert.equal(CHANNELS.x.count('😀'.repeat(140)), 280);
  assert.throws(() => withPost(post({ head: { channels: 'x' }, en: '😀'.repeat(141) })), /x takes 280/);
  assert.equal(CHANNELS.x.count('Prêt\u202f?'), 7);
  assert.equal(CHANNELS.x.count('Play at downfallinstinct.com'), 8 + 23);
  assert.equal(CHANNELS.x.count('https://downfallinstinct.com/play?from=x now'), 23 + 4);
  assert.equal(CHANNELS.x.count('Watch youtu.be/ab'), 6 + 23);
  assert.equal(CHANNELS.x.count('redd.it/ab'), 23);
  assert.equal(CHANNELS.x.count('https://downfallinstinct.com/play.'), 23 + 1);
  assert.throws(() => withPost(post({ head: { channels: 'x' }, en: `${'a'.repeat(256)} https://downfallinstinct.com/play.` })), /281 characters, and x takes 280/);
  assert.equal(CHANNELS.x.count('a.dev'), 23);
  assert.equal(CHANNELS.x.count('me@downfallinstinctverylongdomainname.com'), 41);
});

test('a post without its words in each language, or its title, is refused', () => {
  assert.throws(() => withPost(post({ en: null })), /"## en"/);
  assert.throws(() => withPost(post({ fr: null })), /"## fr"/);
  assert.throws(() => withPost(post({ fr: '   ' })), /"## fr"/);
  assert.throws(() => withPost(post({ title: null })), /"# "/);
  assert.throws(() => withPost(`${post()}\n## de\n\nHallo\n`), /"## de"/);
});

test('a published post gives the link of each of its channels; a post not published gives none', () => {
  const published = { head: { status: 'published' }, links: ['link bluesky: https://bsky.app/profile/a/post/1', 'link x: https://x.com/a/status/1'] };
  const read = withPost(post({ head: { status: 'published' }, links: published.links }), { name: '2026-11-20-brute-answer.md' });
  assert.deepEqual(read.links, { bluesky: 'https://bsky.app/profile/a/post/1', x: 'https://x.com/a/status/1' });
  assert.throws(() => withPost(post({ head: { status: 'published' } }), { name: '2026-11-20-a.md' }), /published.*link of bluesky/s);
  assert.throws(() => withPost(post({ head: { status: 'published' }, links: published.links.slice(0, 1) }), { name: '2026-11-20-a.md' }), /link of x/);
  assert.throws(() => withPost(post({ head: { status: 'published' }, links: [published.links[0], 'link x: https://elsewhere.example/1'] }), { name: '2026-11-20-a.md' }), /on x\.com or twitter\.com, not elsewhere\.example/);
  assert.throws(() => withPost(post({ head: { status: 'published' }, links: [published.links[0], 'link x: https://evil.example#.x.com/a'] }), { name: '2026-11-20-a.md' }), /https:\/\/ address|not evil\.example/);
  assert.throws(() => withPost(post({ head: { status: 'published' }, links: [published.links[0], 'link x: https://evil.example?.x.com/a'] }), { name: '2026-11-20-a.md' }), /https:\/\/ address|not evil\.example/);
  assert.throws(() => withPost(post({ ...published, links: ['link bluesky: http://bsky.app/1', published.links[1]] }), { name: '2026-11-20-a.md' }), /https/);
  assert.throws(() => withPost(post({ ...published, links: [...published.links, 'link discord: https://discord.com/1'] }), { name: '2026-11-20-a.md' }), /discord.*not one of its channels/);
  assert.throws(() => withPost(post({ links: published.links })), /draft.*no link/s);
  assert.throws(() => withPost(post({ head: { status: 'approved' }, links: published.links })), /approved.*no link/s);
  assert.throws(() => withPost(post(published)), /published.*after today/s);
});

test('a status, a key or a line the format does not have is refused', () => {
  assert.throws(() => withPost(post({ head: { status: 'posted' } })), /"status" is draft, approved or published/);
  assert.throws(() => withPost(post({ head: { time: '18:00' } })), /"time" is not a key/);
  assert.throws(() => withPost(post({ links: ['not a key line'] })), /key: value/);
  assert.throws(() => withPost(post({ links: ['channels: x'] })), /twice/);
  assert.throws(() => withPost('# No front matter\n'), /starts with its "---"/);
  assert.equal(withPost(`\uFEFF${post()}`).status, 'draft');
  assert.throws(() => withPost(post({ title: null, en: 'Words.\n\n# Late title' })), /a "# " heading before its words/);
  assert.throws(() => withPost(post({ en: 'Words.\n\n# Second title' })), /one title, before its words/);
  assert.throws(() => withPost(post({ title: 'T\n\n# Second' })), /one title, not 2/);
});

// The spells a post names by the showcase's French name, which the battle does not show.
function showcaseSpells(read) {
  const english = new Set(readdirSync(join(root, 'data/Spells'), { recursive: true }).filter(file => String(file).endsWith('.json'))
    .map(file => JSON.parse(readFileSync(join(root, 'data/Spells', String(file)), 'utf8')).name));
  const showcaseOnly = Object.values(spellNamesFr).filter(name => !english.has(name));
  return Object.values(read.words).flatMap(text => showcaseOnly.filter(name => new RegExp(`(^|[^\\p{L}])${name}([^\\p{L}]|$)`, 'u').test(text)));
}

test('the calendar\'s words name a spell as the battle does, in English and in French', () => {
  assert.deepEqual(showcaseSpells(withPost(post({ fr: 'Ta Brute riposte avec Marteler.' }))), ['Marteler']);
  for (const read of readCalendar(calendar, { today })) assert.deepEqual(showcaseSpells(read), [], read.file);
});

test('the day is Montreal\'s, so a post published in the evening is not dated tomorrow', () => {
  assert.equal(montrealToday(new Date('2026-11-25T01:00:00Z')), '2026-11-24');
  assert.equal(montrealToday(new Date('2026-11-25T06:00:00Z')), '2026-11-25');
  // One day for the promotion's scripts: the brief's intake checks a file's day against the same one.
  assert.equal(montrealToday, shootToday);
  assert.match(readFileSync(join(root, 'scripts/promo/brief.mjs'), 'utf8'), /const today = \(\) => montrealToday\(\);/);
});

test('the command exits 1 on a post it refuses, and on a calendar that is missing or holds no post', () => {
  const folder = mkdtempSync(join(tmpdir(), 'promo-calendar-cli-'));
  try {
    const run = dir => spawnSync(process.execPath, [script, '--calendar', dir], { encoding: 'utf8' });
    assert.equal(run(join(folder, 'missing')).status, 1);
    const empty = join(folder, 'empty');
    mkdirSync(empty);
    const none = run(empty);
    assert.equal(none.status, 1);
    assert.match(none.stderr, /holds no post/);
    const bad = join(folder, 'bad');
    mkdirSync(bad);
    writeFileSync(join(bad, '2026-12-01-a.md'), post({ head: { media: '' } }));
    const refused = run(bad);
    assert.equal(refused.status, 1);
    assert.match(refused.stderr, /"media"/);
    const stray = join(folder, 'stray');
    mkdirSync(stray);
    writeFileSync(join(stray, '2026-12-01-a.md'), post());
    writeFileSync(join(stray, '2026-12-02-b.MD'), post());
    assert.match(run(stray).stderr, /holds posts only/);
    rmSync(join(stray, '2026-12-02-b.MD'));
    assert.match(run(stray).stdout, new RegExp(`${stray.replaceAll(/[.*+?^${}()|[\]\\]/g, '\\$&')}: 1 post\\(s\\)`));
    assert.equal(spawnSync(process.execPath, [script, '--nope'], { encoding: 'utf8' }).status, 1);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});
