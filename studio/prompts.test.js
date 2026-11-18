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
