// The promotion's posting checklist (docs/art/promotion-plan.md, P7): scripts/promo/checklist.mjs.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { CHANNELS, readCalendar } from '../scripts/promo/calendar.mjs';
import { CHANNEL_NAMES, approve, checklist, inPostingOrder, parseArguments, publish } from '../scripts/promo/checklist.mjs';

const root = fileURLToPath(new URL('..', import.meta.url));
const script = join(root, 'scripts/promo/checklist.mjs');
const page = 'https://claude.ai/artifact/abc';
const today = '2026-11-24';
const blob = `brute-pummel-round-3/${'a'.repeat(64)}.mp4`;
const media = () => [{ shot: 'brute-pummel-round-3', ratio: '9:16', file: 'film-9x16.mp4', blob }];

const post = ({ hour = '18:00', channels = 'bluesky, x', show = 'cut brute-pummel-answer', status = 'draft', links = [], en = 'Your Brute answers with Pummel.', fr = 'Ta Brute riposte avec Pummel.', title = 'The Brute answers' } = {}) => [
  '---', `hour: ${hour}`, `channels: ${channels}`, `media: ${show}`, `status: ${status}`, ...links, '---', '',
  `# ${title}`, '', '## en', '', en, '', '## fr', '', fr, '',
].join('\n');

// A calendar of its own, with these posts by name, handed to the body and removed after.
function withCalendar(posts, body) {
  const folder = mkdtempSync(join(tmpdir(), 'promo-checklist-'));
  try {
    for (const [file, text] of Object.entries(posts)) writeFileSync(join(folder, file), text);
    return body(folder);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
}

const read = folder => readCalendar(folder, { today, readRecord: () => [] });

test('every channel of the launch has the name the owner finds it by', () => {
  assert.deepEqual(Object.keys(CHANNEL_NAMES), Object.keys(CHANNELS));
});

test('the posts come in the order they are posted: by day, then by hour', () => {
  withCalendar({
    '2026-12-02-a.md': post({ hour: '09:00', title: 'Third' }),
    '2026-12-01-b.md': post({ hour: '20:00', title: 'Second' }),
    '2026-12-01-c.md': post({ hour: '08:30', title: 'First' }),
  }, folder => {
    assert.deepEqual(inPostingOrder(read(folder)).map(item => item.title), ['First', 'Second', 'Third']);
    const text = checklist(read(folder), { page, media });
    assert.ok(text.indexOf('First') < text.indexOf('Second') && text.indexOf('Second') < text.indexOf('Third'));
  });
});

test('each channel is a numbered step across the batch, with its hour, its file and the words in a block a language', () => {
  withCalendar({
    '2026-12-01-a.md': post({ channels: 'tiktok, x' }),
    '2026-12-02-b.md': post({ channels: 'discord', show: 'film brute-pummel-round-3 9:16', en: 'Line one.\n\nLine two.' }),
  }, folder => {
    const text = checklist(read(folder), { page, media });
    assert.match(text, /### 1 December 2026, 18:00: The Brute answers\n\n`2026-12-01-a`, draft/);
    assert.match(text, /^1\. \*\*18:00, TikTok\.\*\* The cut `brute-pummel-answer` at 9:16: the English MP4 with the English words, the French MP4 with the French words, on \[the contact sheet page\]\(https:\/\/claude\.ai\/artifact\/abc\)\./m);
    assert.match(text, /^2\. \*\*18:00, X\.\*\*/m);
    assert.match(text, new RegExp(`^3\\. \\*\\*18:00, Discord\\.\\*\\* The film of \`brute-pummel-round-3\` at 9:16, in the Azure portal: .*\`promo\`, \`${blob}\``, 'm'));
    assert.match(text, /   English:\n\n   ```text\n   Your Brute answers with Pummel\.\n   ```/);
    assert.match(text, /   French:\n\n   ```text\n   Ta Brute riposte avec Pummel\.\n   ```/);
    assert.match(text, /   ```text\n   Line one\.\n\n   Line two\.\n   ```/);
    assert.match(text, /```text\n2026-12-01-a tiktok https:\/\/\.\.\.\n2026-12-01-a x https:\/\/\.\.\.\n2026-12-02-b discord https:\/\/\.\.\.\n```/);
  });
});

test('a film not yet filmed on main says how to film it; a cut or a still needs the contact sheet page', () => {
  withCalendar({ '2026-12-01-a.md': post({ show: 'film brute-pummel-round-3 16:9' }) }, folder => {
    assert.match(checklist(read(folder), { media }), /The film of `brute-pummel-round-3` at 16:9, not filmed on `main` yet: GitHub, \*Actions\*, \*Film a promotion shot\*, \*Run workflow\*, the branch `main`, `promo\/shots\/brute-pummel-round-3\.json`, the ratio `16:9`/);
  });
  withCalendar({ '2026-12-01-a.md': post({ show: 'still brute-mirror-round-3 1:1' }) }, folder => {
    assert.throws(() => checklist(read(folder), { media }), /contact sheet page: give its link with --page/);
    assert.throws(() => checklist(read(folder), { page: 'http://example.com', media }), /https:\/\//);
    assert.match(checklist(read(folder), { page, media }), /The still of `brute-mirror-round-3` at 1:1, on \[the contact sheet page\]/);
  });
});

test('a published post is left out, and a checklist with nothing to post is refused', () => {
  const links = ['link bluesky: https://bsky.app/profile/a/post/1', 'link x: https://x.com/a/status/1'];
  withCalendar({ '2026-11-20-a.md': post({ status: 'published', links }), '2026-12-01-b.md': post({ title: 'Waiting' }) }, folder => {
    const text = checklist(read(folder), { page, media });
    assert.doesNotMatch(text, /2026-11-20-a/);
    assert.match(text, /Waiting/);
    assert.throws(() => checklist(read(folder).slice(0, 1), { page, media }), /no post to publish/);
  });
});

test('the checklist reads posts as the calendar does: a ratio or words a channel does not take are refused', () => {
  withCalendar({ '2026-12-01-a.md': post({ channels: 'tiktok', show: 'still brute-mirror-round-3 16:9' }) }, folder => {
    assert.throws(() => read(folder), /tiktok takes 9:16/);
  });
  withCalendar({ '2026-12-01-a.md': post({ channels: 'youtube-shorts', en: 'a'.repeat(101) }) }, folder => {
    assert.throws(() => read(folder), /youtube-shorts takes 100/);
  });
});

test('a merged draft becomes approved, and only a draft does', () => {
  withCalendar({ '2026-12-01-a.md': post() }, folder => {
    assert.equal(approve('2026-12-01-a', { calendar: folder, today, readRecord: () => [] }).status, 'approved');
    assert.match(readFileSync(join(folder, '2026-12-01-a.md'), 'utf8'), /^status: approved$/m);
    assert.throws(() => approve('2026-12-01-a.md', { calendar: folder, today }), /it is approved, and only a draft/);
    assert.throws(() => approve('2026-12-09-none', { calendar: folder, today }), /holds no such post/);
  });
});

test('a posted post becomes published with each channel\'s link, or stays as it was', () => {
  withCalendar({ '2026-11-20-a.md': post({ status: 'approved' }), '2026-12-01-b.md': post() }, folder => {
    const path = join(folder, '2026-11-20-a.md');
    const before = readFileSync(path, 'utf8');
    const options = { calendar: folder, today };
    assert.throws(() => publish('2026-11-20-a', { x: 'https://x.com/a/status/1' }, options), /link of bluesky/);
    assert.throws(() => publish('2026-11-20-a', { x: 'https://x.com/a/status/1', bluesky: 'https://elsewhere.example/1' }, options), /on bsky\.app/);
    assert.throws(() => publish('2026-11-20-a', { facebook: 'https://facebook.com/1' }, options), /not a channel of the launch/);
    assert.throws(() => publish('2026-11-20-a', { x: 'https://x.com/a/status/1', bluesky: 'https://bsky.app/p/1', discord: 'https://discord.com/1' }, options), /not one of its channels/);
    assert.equal(readFileSync(path, 'utf8'), before);
    const done = publish(path, { bluesky: 'https://bsky.app/profile/a/post/1', x: 'https://x.com/a/status/1' }, options);
    assert.equal(done.status, 'published');
    assert.deepEqual(done.links, { bluesky: 'https://bsky.app/profile/a/post/1', x: 'https://x.com/a/status/1' });
    assert.match(readFileSync(path, 'utf8'), /status: published\nlink bluesky: https:\/\/bsky\.app\/profile\/a\/post\/1\nlink x: https:\/\/x\.com\/a\/status\/1\n---/);
    assert.throws(() => publish(path, { x: 'https://x.com/a/status/2' }, options), /published already/);
    assert.throws(() => publish('2026-12-01-b', { bluesky: 'https://bsky.app/p/1', x: 'https://x.com/a/status/1' }, options), /after today/);
  });
});

test('the arguments name one mode, and refuse what it does not take', () => {
  assert.deepEqual(parseArguments(['--page', page, 'a']), { mode: 'checklist', posts: ['a'], links: {}, page });
  assert.deepEqual(parseArguments(['--publish', 'a', '--link', 'x', 'https://x.com/1']).links, { x: 'https://x.com/1' });
  for (const argv of [['--approve'], ['--publish', 'a'], ['--publish', 'a', 'b', '--link', 'x', 'u'], ['--link', 'x', 'u'], ['--approve', 'a', '--page', page],
    ['--approve', 'a', '--publish', 'b'], ['--page'], ['--nope'], ['--publish', 'a', '--link', 'x', 'u', '--link', 'x', 'v'], ['--publish', 'a', '--link', 'x']]) {
    assert.throws(() => parseArguments(argv), /usage|takes a value|twice|goes with/, argv.join(' '));
  }
});

test('the command prints the checklist, marks posts, and exits 1 on what it refuses', () => {
  const run = argv => spawnSync(process.execPath, [script, ...argv], { encoding: 'utf8' });
  const repository = run(['--page', page]);
  assert.equal(repository.status, 0, repository.stderr);
  assert.match(repository.stdout, /^## Posting checklist/);
  withCalendar({ '2026-11-20-a.md': post(), '2026-12-01-b.md': post() }, folder => {
    assert.match(run(['--calendar', folder, '--page', page, '2026-12-01-b']).stdout, /2026-12-01-b x https/);
    assert.equal(run(['--calendar', folder]).status, 1);
    assert.match(run(['--calendar', folder]).stderr, /--page/);
    assert.match(run(['--calendar', folder, '--approve', '2026-11-20-a']).stdout, /2026-11-20-a\.md: approved\./);
    const refused = run(['--calendar', folder, '--publish', '2026-11-20-a', '--link', 'x', 'https://x.com/a/status/1']);
    assert.equal(refused.status, 1);
    assert.match(refused.stderr, /link of bluesky/);
    const published = run(['--calendar', folder, '--publish', '2026-11-20-a', '--link', 'x', 'https://x.com/a/status/1', '--link', 'bluesky', 'https://bsky.app/p/1']);
    assert.equal(published.status, 0, published.stderr);
    assert.match(published.stdout, /published\./);
    assert.equal(run(['--nope']).status, 1);
  });
});
