// The prompts of the agents and the skills (.claude/agents/*.md, .claude/skills/*/SKILL.md) name repository
// paths in backticks, and a session follows them as written. A path that moved or never existed sends it
// looking for nothing (issue #633), so every one named must exist.

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
