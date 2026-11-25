// The prompts of the agents and the skills (.claude/agents/*.md, .claude/skills/*/SKILL.md) name repository
// paths in backticks, and a session follows them as written. A path that moved or never existed sends it
// looking for nothing (issue #633), so every one named must exist. So must a heading it names, as an anchor
// (`docs/reference/commands.md#play`), and a label: a test cannot ask GitHub, so `.github/labels.json` is where
// the labels the workflow uses are declared, and a prompt names only those.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { headingAnchors } from '../scripts/markdown-anchors.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

// Paths a prompt may name before they exist: build output (git-ignored) and a memory an agent writes itself.
const NOT_YET_THERE = [
  'data/dst/', // DataBuilder's output (ADR 0009)
  'table/battle-build/', // the battle bundle, built from battle/
  'artifacts/design-system/', // the design system's folder, built by scripts/design-system/build.mjs
  'artifacts/promo/', // the promotion's stills, films, cuts and packs, written by scripts/promo/
  '.claude/agent-memory/', // an agent's memory folder, made on its first write
];

function prompts() {
  const agents = readdirSync(join(root, '.claude/agents'))
    .filter(name => name.endsWith('.md'))
    .map(name => `.claude/agents/${name}`);
  const skills = readdirSync(join(root, '.claude/skills'))
    .map(name => `.claude/skills/${name}/SKILL.md`)
    .filter(path => existsSync(join(root, path)));
  return [...agents, ...skills];
}

/** The repository paths a prompt names in backticks: a top-level entry, with a slash or a file extension. */
function pathsNamed(text, topLevel) {
  const paths = [];
  for (const [, span] of text.matchAll(/`([^`\s]+)`/g)) {
    // A placeholder or a glob (`<agent>/MEMORY.md`, `studio/*.test.js`) names a shape, not a path.
    if (/[<>*{}$?]/.test(span)) continue;
    const path = span.replace(/[.,:;)]+$/, '').replace(/#.*$/, '').replace(/:\d+(-\d+)?$/, '');
    if (!topLevel.has(path.split('/')[0])) continue;
    if (!path.includes('/') && !/\.\w+$/.test(path)) continue;
    paths.push(path);
  }
  return paths;
}

/** The headings a prompt names, as `path.md#anchor` in backticks: { path, anchor }. */
function anchorsNamed(text, topLevel) {
  const anchors = [];
  for (const [, path, anchor] of text.matchAll(/`([^`\s#]+\.md)#([^`\s]+)`/g)) {
    if (topLevel.has(path.split('/')[0])) anchors.push({ path, anchor: anchor.replace(/[.,:;)]+$/, '') });
  }
  return anchors;
}

/**
 * The labels a prompt names: `label:<name>` in a search, the span before the word "label", and the spans shaped
 * like a label between the word "label", "labels" or "labelled" and the end of its sentence.
 */
function labelsNamed(text) {
  const labels = [...text.matchAll(/\blabel:([\w-]+)/g)].map(([, name]) => name);
  const spans = [];
  const prose = text.replace(/`([^`\n]+)`/g, (_, span) => `\u0000${spans.push(span) - 1}\u0000`);
  const named = part => [...part.matchAll(/\u0000(\d+)\u0000/g)].map(([, at]) => spans[at]);
  for (const [, before, after] of prose.matchAll(/(?:(\u0000\d+\u0000)\s+)?\blabel(?:s|led)?\b((?:(?!\.\s|\n\s*\n)[^])*)/g)) {
    labels.push(...named(before ?? ''), ...named(after).filter(span => /^[a-z][a-z0-9-]*$/.test(span)));
  }
  return [...new Set(labels)];
}

test('a span is read as a path only when it starts at the root and is not a placeholder or a glob', () => {
  const topLevel = new Set(['docs', 'studio', 'AGENTS.md']);
  const text = 'Read `docs/a.md:12`, `AGENTS.md`, `docs/<plan>.md`, `studio/*.test.js`, `docs`, `table/x.js`.';
  assert.deepEqual(pathsNamed(text, topLevel), ['docs/a.md', 'AGENTS.md']);
});

test('every repository path an agent or a skill names exists', () => {
  const topLevel = new Set(readdirSync(root).filter(name => name !== '.git'));
  const missing = [];
  for (const prompt of prompts()) {
    const text = readFileSync(join(root, prompt), 'utf8');
    for (const path of pathsNamed(text, topLevel)) {
      if (NOT_YET_THERE.some(prefix => path.startsWith(prefix))) continue;
      if (!existsSync(join(root, path))) missing.push(`${prompt}: ${path}`);
    }
  }
  assert.deepEqual(missing, []);
});

test('a heading is read from an anchor, and its anchor is the one GitHub gives it', () => {
  const topLevel = new Set(['docs']);
  assert.deepEqual(anchorsNamed('See `docs/a.md#the-gate`, `docs/a.md` and `other/b.md#x`.', topLevel), [
    { path: 'docs/a.md', anchor: 'the-gate' },
  ]);
  const markdown = '# Commands\n### ops/read.sh, `report.mjs`\n```\n# not a heading\n```\n#### The panel: tables, agents\n## Commands';
  assert.deepEqual([...headingAnchors(markdown)], ['commands', 'opsreadsh-reportmjs', 'the-panel-tables-agents', 'commands-1']);
});

test('every heading an agent or a skill names exists in the file it names', () => {
  const topLevel = new Set(readdirSync(root).filter(name => name !== '.git'));
  const missing = [];
  for (const prompt of prompts()) {
    for (const { path, anchor } of anchorsNamed(readFileSync(join(root, prompt), 'utf8'), topLevel)) {
      if (!existsSync(join(root, path))) continue; // the test of the paths says so
      if (!headingAnchors(readFileSync(join(root, path), 'utf8')).has(anchor)) missing.push(`${prompt}: ${path}#${anchor}`);
    }
  }
  assert.deepEqual(missing, []);
});

test('a label is read from a search, before the word and after it to the end of the sentence', () => {
  const text = [
    "`gh api search/issues -f q='is:issue label:proposal'`. The `ops` label. One label:",
    '`follow-up` (work left), `flaky` (a retry), not `docs/a.md`. Then `other` and `--label` are not labels.',
    '',
    'Issues labelled `red-main`',
    '',
    '`unrelated` opens another paragraph.',
  ].join('\n');
  assert.deepEqual(labelsNamed(text), ['proposal', 'ops', 'follow-up', 'flaky', 'red-main']);
});

test('every label an agent or a skill names is one the repository declares', () => {
  const declared = Object.keys(JSON.parse(readFileSync(join(root, '.github/labels.json'), 'utf8')));
  const unknown = [];
  const named = new Set();
  for (const prompt of prompts()) {
    for (const label of labelsNamed(readFileSync(join(root, prompt), 'utf8'))) {
      named.add(label);
      if (!declared.includes(label)) unknown.push(`${prompt}: ${label}`);
    }
  }
  assert.deepEqual(unknown, []);
  for (const label of ['follow-up', 'flaky', 'ops', 'proposal']) assert.ok(named.has(label), `${label} is read`);
});

test('every label an issue form puts on its issue is one the repository declares', () => {
  const declared = Object.keys(JSON.parse(readFileSync(join(root, '.github/labels.json'), 'utf8')));
  const forms = readdirSync(join(root, '.github/ISSUE_TEMPLATE')).filter(name => name.endsWith('.yml') && name !== 'config.yml');
  const unknown = [];
  const named = new Set();
  for (const form of forms) {
    const labels = /^labels:\s*\[([^\]]*)\]/m.exec(readFileSync(join(root, '.github/ISSUE_TEMPLATE', form), 'utf8'))?.[1] ?? '';
    for (const label of labels.split(',').map(one => one.trim().replace(/^["']|["']$/g, '')).filter(Boolean)) {
      named.add(label);
      if (!declared.includes(label)) unknown.push(`${form}: ${label}`);
    }
  }
  assert.deepEqual(unknown, []);
  assert.ok(named.has('from-report'), 'the form a report is filed with labels it from-report');
});

test('the issue searches the skills give name issues or pull requests, which GitHub requires', () => {
  const missing = [];
  for (const prompt of prompts()) {
    const text = readFileSync(join(root, prompt), 'utf8');
    for (const [query] of text.matchAll(/search\/issues[^`]*/g)) {
      if (!/is:(issue|pr|pull-request)\b/.test(query)) missing.push(`${prompt}: ${query}`);
    }
  }
  assert.deepEqual(missing, []);
});

// A push while CI plays cancels the run, and every minute it spent: 22% of a week's minutes went that way
// (docs/engineering/ci-minutes-plan.md, M4). /ship answers a round of review with one push.
test('ship answers a round of review with one push, and does not cancel a run that can finish', () => {
  const ship = readFileSync(join(root, '.claude/skills/ship/SKILL.md'), 'utf8');
  const events = ship.slice(ship.indexOf('7. On each event'), ship.indexOf('\n8. '));
  assert.ok(events.length > 0, 'step 7 is where the events are answered');
  assert.match(events, /\bround\b/);
  assert.match(events, /push once/);
  assert.match(events, /cancels/);
  // The thread, the check and the conflict are fixed in commits; the round's one push is a bullet of its own.
  const bullets = events.split(/\n {3}- /).slice(1);
  for (const kind of ['A review thread', 'A failing check', 'A conflict']) {
    const bullet = bullets.find((text) => text.startsWith(kind));
    assert.ok(bullet, kind);
    assert.doesNotMatch(bullet, /\bpush/i, kind);
  }
  // A run held for is answered even when it ends green and sends nothing.
  assert.match(events, /send_later/);
});

// /promo turns a request into a batch of posts (docs/art/promotion-plan.md, P6). It films with the promotion's
// scripts and checks with their own refusals, the promo-producer writes the words, and nothing leaves for a
// platform: the owner publishes by hand (the plan's question 4).
test('promo films, cuts, briefs and checks with the promotion scripts, and has the producer write the words', () => {
  const promo = readFileSync(join(root, '.claude/skills/promo/SKILL.md'), 'utf8');
  for (const script of ['shoot.mjs', 'cut.mjs', 'brief.mjs', 'calendar.mjs', 'checklist.mjs', 'media.mjs']) {
    assert.match(promo, new RegExp(`node scripts/promo/${script.replace('.', '\\.')}`), script);
  }
  assert.match(promo, /`promo-producer`/);
  assert.ok(existsSync(join(root, '.claude/agents/promo-producer.md')));
  assert.match(promo, /Nothing is posted from here/);
  assert.match(promo, /No post is published, scheduled or sent from a session/);
  assert.match(promo, /`\/ship`/);
  // The workflow writes a shot's film and sheet, not a cut: the cut reaches the owner on the contact sheet page.
  const workflow = readFileSync(join(root, '.github/workflows/promo-film.yml'), 'utf8');
  // Nor a still: the workflow only films. A cut and a still reach the owner on the contact sheet page.
  assert.doesNotMatch(workflow, /cut\.mjs/);
  assert.doesNotMatch(workflow, /--still|still-/);
  assert.match(promo, /Each cut's MP4, one a language, and each still at full size are files of the\s+page too/);
  assert.match(promo, /For each new shot and each ratio a post shows as `film`,/);
  // A scenario tried is written outside scenarios/, where every file is a listed scenario and a test.
  assert.doesNotMatch(promo, /--out scenarios\//);
});

test('the promo producer counts against the limits the scripts hold, and writes only the words', async () => {
  const producer = readFileSync(join(root, '.claude/agents/promo-producer.md'), 'utf8');
  const { CHANNELS } = await import('../scripts/promo/calendar.mjs');
  const { CARD_LIMITS } = await import('../scripts/promo/cut.mjs');
  assert.ok(CHANNELS && CARD_LIMITS, 'the limits it names are exported where it says');
  assert.match(producer, /`CHANNELS` in\s+`scripts\/promo\/calendar\.mjs`/);
  assert.match(producer, /`CARD_LIMITS` in `scripts\/promo\/cut\.mjs`/);
  assert.match(producer, /`showcase\/copy\.mjs` \(the voice/);
  assert.match(producer, /`docs\/domain\/player-\w+\.md` \(the words a player\s+reads/);
  assert.match(producer, /write the words of the files you are given under `promo\/cuts\/` and `promo\/calendar\/`, and nothing\s+else/);
  // Each limit the producer gives is the one the calendar holds for that channel, and the card's at 9:16.
  const limits = [...producer.matchAll(/(\d+) when `([a-z-]+)`(?: or `([a-z-]+)`)? is/g)];
  assert.ok(limits.length >= 3, 'the producer gives the channels\' limits');
  for (const [, limit, ...channels] of limits) {
    for (const channel of channels.filter(Boolean)) assert.equal(CHANNELS[channel]?.limit, Number(limit), channel);
  }
  const card = /at\s+9:16 a title holds (\d+) and a caption (\d+)/.exec(producer);
  assert.ok(card, 'the producer gives the cards\' limits at 9:16');
  assert.deepEqual([Number(card[1]), Number(card[2])], [CARD_LIMITS.title['9:16'], CARD_LIMITS.caption['9:16']]);
  // The producer writes no brief's ask: it is the owner's words (promo/README.md#a-brief).
  assert.match(producer, /`ask` is the owner's own words/);
});
