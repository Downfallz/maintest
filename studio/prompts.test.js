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

/** The anchors GitHub gives a markdown file's headings: lower case, punctuation dropped, a hyphen for a space. */
function headingAnchors(markdown) {
  const anchors = new Set();
  let fenced = false;
  for (const line of markdown.split('\n')) {
    if (line.startsWith('```')) fenced = !fenced;
    const heading = fenced ? null : /^#{1,6}\s+(.+?)\s*$/.exec(line);
    if (!heading) continue;
    const slug = heading[1].toLowerCase().replace(/[^\p{L}\p{N}\s_-]/gu, '').replace(/\s/g, '-');
    let anchor = slug;
    for (let n = 1; anchors.has(anchor); n++) anchor = `${slug}-${n}`;
    anchors.add(anchor);
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
