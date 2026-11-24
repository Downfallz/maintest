// The promotion's calendar (docs/art/promotion-plan.md, P5): scripts/promo/calendar.mjs.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { CHANNELS, RESTYLED, readCalendar, readPost } from '../scripts/promo/calendar.mjs';
import { spellNamesFr } from '../showcase/field-notes.mjs';

const root = fileURLToPath(new URL('..', import.meta.url));
const calendar = join(root, 'promo/calendar');
const script = join(root, 'scripts/promo/calendar.mjs');
const hash = 'b'.repeat(64);
const today = '2026-11-24';

const post = ({ head = {}, links = [], en = 'Your Brute answers with Pummel.', fr = 'Ta Brute riposte avec Pummel.', title = 'The Brute answers' } = {}) => [
  '---',
  ...Object.entries({ channels: 'bluesky, x', media: 'cut brute-pummel-answer', status: 'draft', ...head })
    .filter(([, value]) => value !== undefined)
    .map(([key, value]) => `${key}: ${value}`),
  ...links,
  '---',
  '',
  ...(title === null ? [] : [`# ${title}`, '']),
  ...(en === null ? [] : ['## en', '', en, '']),
  ...(fr === null ? [] : ['## fr', '', fr, '']),
].join('\n');

// A post written under its name in a folder of its own, read back, with a record of generated files beside it.
function withPost(text, { name = '2026-12-01-brute-answer.md', generated = [] } = {}) {
  const folder = mkdtempSync(join(tmpdir(), 'promo-calendar-'));
  try {
    const path = join(folder, name);
    writeFileSync(path, text);
    const record = join(folder, 'generated.json');
    writeFileSync(record, JSON.stringify({ format: 'downfall.promo.generated.v1', files: generated }));
    return readPost(path, { generated: record, today });
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
  assert.equal(withPost(post({ en: '😀'.repeat(CHANNELS.x.limit) })).channels.length, 2);
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
  assert.throws(() => withPost(post({ ...published, links: ['link bluesky: http://bsky.app/1', published.links[1]] }), { name: '2026-11-20-a.md' }), /https/);
  assert.throws(() => withPost(post({ ...published, links: [...published.links, 'link discord: https://discord.com/1'] }), { name: '2026-11-20-a.md' }), /discord.*not one of its channels/);
  assert.throws(() => withPost(post({ links: published.links })), /draft.*no link/s);
  assert.throws(() => withPost(post({ head: { status: 'approved' }, links: published.links })), /approved.*no link/s);
  assert.throws(() => withPost(post(published)), /published.*after today/s);
});

test('a status, a key or a line the format does not have is refused', () => {
  assert.throws(() => withPost(post({ head: { status: 'posted' } })), /"status" is draft, approved or published/);
  assert.throws(() => withPost(post({ head: { hour: '18:00' } })), /"hour"/);
  assert.throws(() => withPost(post({ links: ['not a key line'] })), /key: value/);
  assert.throws(() => withPost(post({ links: ['channels: x'] })), /twice/);
  assert.throws(() => withPost('# No front matter\n'), /starts with its "---"/);
});

test('the calendar\'s words name a spell as the battle does, in English and in French', () => {
  const english = new Set(readdirSync(join(root, 'data/Spells'), { recursive: true }).filter(file => String(file).endsWith('.json'))
    .map(file => JSON.parse(readFileSync(join(root, 'data/Spells', String(file)), 'utf8')).name));
  const showcaseOnly = Object.values(spellNamesFr).filter(name => !english.has(name));
  for (const read of readCalendar(calendar, { today })) {
    for (const text of Object.values(read.words)) {
      for (const name of showcaseOnly) assert.ok(!new RegExp(`(^|[^\\p{L}])${name}([^\\p{L}]|$)`, 'u').test(text), `${read.file}: "${text}" names the spell ${name}, which the battle does not show`);
    }
  }
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
    assert.equal(spawnSync(process.execPath, [script, '--nope'], { encoding: 'utf8' }).status, 1);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});
