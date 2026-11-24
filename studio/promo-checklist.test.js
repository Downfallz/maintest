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
const old = `brute-pummel-round-3/${'0'.repeat(64)}.mp4`;
// The record only grows: the shot was filmed twice, and the later film is the one to post.
const media = () => [
  { shot: 'brute-pummel-round-3', ratio: '9:16', file: 'film-9x16.mp4', blob: old },
  { shot: 'brute-pummel-round-3', ratio: '9:16', file: 'film-9x16.mp4', blob },
];
const hash = 'b'.repeat(64);

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
    assert.match(text, /^1\. \*\*18:00, TikTok\.\*\*\n\n   - The cut `brute-pummel-answer` at 9:16, on \[the contact sheet page\]\(https:\/\/claude\.ai\/artifact\/abc\)\. Take the English MP4 for the English words, the French MP4 for the French words\.$/m);
    assert.match(text, /^2\. \*\*18:00, X\.\*\*/m);
    assert.match(text, new RegExp(`^3\\. \\*\\*18:00, Discord\\.\\*\\*\\n\\n   - The film of \`brute-pummel-round-3\` at 9:16, in the Azure portal, signed in as yourself\\. Path: .*\`promo\`, \`${blob}\``, 'm'));
    assert.doesNotMatch(text, new RegExp(old));
    assert.match(text, /   English:\n\n   ```text\n   Your Brute answers with Pummel\.\n   ```/);
    assert.match(text, /   French:\n\n   ```text\n   Ta Brute riposte avec Pummel\.\n   ```/);
    assert.match(text, /   ```text\n   Line one\.\n\n   Line two\.\n   ```/);
    assert.match(text, /```text\n2026-12-01-a tiktok <link>\n2026-12-01-a x <link>\n2026-12-02-b discord <link>\n```/);
  });
});

test('a post that shows several media lists each on its own line, and a post named twice is refused', () => {
  withCalendar({ '2026-12-01-a.md': post({ show: 'cut brute-pummel-answer, still brute-pummel-round-3 9:16' }) }, folder => {
    assert.match(checklist(read(folder), { page, media }), /\n   - The cut .*\n   - The still of `brute-pummel-round-3` at 9:16, on /);
    assert.throws(() => checklist([...read(folder), ...read(folder)], { page, media }), /names a post twice/);
  });
});

test('a film not yet filmed on main says how to film it; a cut, a still or a generated file needs the contact sheet page', () => {
  withCalendar({ '2026-12-01-a.md': post({ show: 'film brute-pummel-round-3 16:9' }) }, folder => {
    assert.match(checklist(read(folder), { media }), /The film of `brute-pummel-round-3` at 16:9, not filmed on `main` yet\. To film it: GitHub, \*Actions\*, \*Film a promotion shot\*, \*Run workflow\*, branch `main`, shot `promo\/shots\/brute-pummel-round-3\.json`, ratio `16:9`/);
  });
  withCalendar({ '2026-12-01-a.md': post() }, folder => {
    assert.throws(() => checklist(read(folder), { media }), /The cut `brute-pummel-answer`.*--page/);
  });
  withCalendar({ '2026-12-01-a.md': post({ show: `generated ${hash}` }) }, folder => {
    const generated = readCalendar(folder, { today, readRecord: () => [{ brief: 'b', makes: 'key-art', shot: 'brute-pummel-round-3', ratio: '9:16', sha256: hash }] });
    assert.throws(() => checklist(generated, { media }), /The generated file `bbbbbbbbbbbb`.*--page/);
    assert.match(checklist(generated, { page, media }), /The generated file `bbbbbbbbbbbb` at 9:16, on \[the contact sheet page\]/);
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

test('the checklist is refused for a post at a ratio, or with words, a channel does not take', () => {
  const run = folder => spawnSync(process.execPath, [script, '--calendar', folder, '--page', page], { encoding: 'utf8' });
  const cases = [
    [post({ channels: 'tiktok', show: 'still brute-mirror-round-3 16:9' }), /tiktok takes 9:16/],
    [post({ channels: 'youtube-shorts', en: 'a'.repeat(101) }), /youtube-shorts takes 100/],
  ];
  for (const [text, why] of cases) {
    withCalendar({ '2026-12-01-a.md': text }, folder => {
      const refused = run(folder);
      assert.equal(refused.status, 1);
      assert.match(refused.stderr, why);
    });
  }
});

test('approving several posts writes none when one is refused, or named twice', () => {
  withCalendar({ '2026-12-01-a.md': post(), '2026-12-02-b.md': post({ status: 'approved' }) }, folder => {
    const options = { calendar: folder, today };
    assert.throws(() => approve(['2026-12-01-a', '2026-12-02-b'], options), /it is approved, and only a draft/);
    assert.match(readFileSync(join(folder, '2026-12-01-a.md'), 'utf8'), /^status: draft$/m);
    assert.throws(() => approve(['2026-12-01-a', '2026-12-01-a.md'], options), /names a post twice/);
    assert.deepEqual(approve([join(folder, '2026-12-01-a.md')], options).map(read => read.status), ['approved']);
  });
});

test('a post is a file of the calendar, never a path elsewhere', () => {
  withCalendar({ '2026-12-01-a.md': post() }, calendar => withCalendar({ '2026-12-01-a.md': post() }, elsewhere => {
    assert.throws(() => approve(join(elsewhere, '2026-12-01-a.md'), { calendar, today }), /not a path elsewhere/);
    assert.throws(() => approve('elsewhere/2026-12-01-a', { calendar, today }), /not a path elsewhere/);
    assert.match(readFileSync(join(elsewhere, '2026-12-01-a.md'), 'utf8'), /^status: draft$/m);
    assert.match(readFileSync(join(calendar, '2026-12-01-a.md'), 'utf8'), /^status: draft$/m);
  }));
});

test('a merged draft becomes approved, and only a draft does', () => {
  withCalendar({ '2026-12-01-a.md': post() }, folder => {
    assert.equal(approve('2026-12-01-a', { calendar: folder, today, readRecord: () => [] })[0].status, 'approved');
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
    assert.throws(() => publish('2026-12-01-b', { bluesky: 'https://bsky.app/p/1', x: 'https://x.com/a/status/1' }, options), /it is draft: a post is approved \(--approve/);
  });
  withCalendar({ '2026-12-01-b.md': post({ status: 'approved' }) }, folder => {
    assert.throws(() => publish('2026-12-01-b', { bluesky: 'https://bsky.app/p/1', x: 'https://x.com/a/status/1' }, { calendar: folder, today }), /after today/);
  });
});

test('marking a post keeps its line endings and its byte order mark', () => {
  withCalendar({ '2026-11-20-a.md': '﻿' + post({ status: 'approved' }).replaceAll('\n', '\r\n') }, folder => {
    publish('2026-11-20-a', { bluesky: 'https://bsky.app/p/1', x: 'https://x.com/a/status/1' }, { calendar: folder, today });
    const text = readFileSync(join(folder, '2026-11-20-a.md'), 'utf8');
    assert.ok(text.startsWith('﻿---\r\n'));
    assert.match(text, /status: published\r\nlink bluesky: https:\/\/bsky\.app\/p\/1\r\nlink x: https:\/\/x\.com\/a\/status\/1\r\n---\r\n/);
    assert.doesNotMatch(text, /[^\r]\n/);
  });
});

test('the arguments name one mode, and refuse what it does not take', () => {
  assert.deepEqual(parseArguments(['--page', page, 'a']), { mode: 'checklist', posts: ['a'], links: {}, page });
  assert.deepEqual(parseArguments(['--publish', 'a', '--link', 'x', 'https://x.com/1']).links, { x: 'https://x.com/1' });
  for (const argv of [['--approve'], ['--publish', 'a'], ['--publish', 'a', 'b', '--link', 'x', 'u'], ['--link', 'x', 'u'], ['--approve', 'a', '--page', page],
    ['--approve', 'a', '--publish', 'b'], ['--page'], ['--nope'], ['--publish', 'a', '--link', 'x', 'u', '--link', 'x', 'v'], ['--publish', 'a', '--link', 'x'],
    ['--calendar', '--approve', 'a'], ['--page', page, '--page', 'https://b'], ['--calendar', 'a', '--calendar', 'b', '--approve', 'c']]) {
    assert.throws(() => parseArguments(argv), /usage|takes a value|twice|goes with/, argv.join(' '));
  }
});

test('the command prints the checklist, marks posts, and exits 1 on what it refuses', () => {
  const run = argv => spawnSync(process.execPath, [script, ...argv], { encoding: 'utf8' });
  const repository = run(['--page', page]);
  assert.equal(repository.status, 0, repository.stderr);
  assert.match(repository.stdout, /^## Posting checklist/);
  withCalendar({ '2026-11-20-a.md': post(), '2026-12-01-b.md': post() }, folder => {
    assert.match(run(['--calendar', folder, '--page', page, '2026-12-01-b']).stdout, /2026-12-01-b x <link>/);
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
