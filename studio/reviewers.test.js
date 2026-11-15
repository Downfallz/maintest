// Which reviewers a change calls for (scripts/review/reviewers.mjs, read by the /reviewers skill), and that every
// reviewer it names is an agent under .claude/agents, read only except for its memory.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { REVIEWERS, reviewersFor } from '../scripts/review/reviewers.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

test('an engine change gets the code reviewer alone', () => {
  assert.deepEqual(reviewersFor(['src/DownfallArena.Application/Matches/PlayRound.cs']), ['code-reviewer']);
});

test('a domain change also gets the domain reviewer', () => {
  assert.deepEqual(reviewersFor(['src/DownfallArena.Domain/Matches/Match.cs']), ['code-reviewer', 'domain-reviewer']);
});

test('a route change gets the host reviewer, and the security reviewer when it signs someone in', () => {
  assert.deepEqual(reviewersFor(['src/DownfallArena.Cli/Table/LobbyApi.cs']), ['code-reviewer', 'host-reviewer']);
  assert.deepEqual(reviewersFor(['src/DownfallArena.Cli/Table/AccountSessions.cs']), [
    'code-reviewer',
    'host-reviewer',
    'security-reviewer',
  ]);
});

test('a page change gets the pages reviewer, and its words the copy editor', () => {
  assert.deepEqual(reviewersFor(['table/lobby.js']), ['code-reviewer', 'pages-reviewer']);
  assert.deepEqual(reviewersFor(['table/lobby.html']), ['code-reviewer', 'pages-reviewer', 'copy-editor']);
});

test('a page test or a browser spec is not a page', () => {
  assert.deepEqual(reviewersFor(['table/lobby.test.js', 'studio/browser/table.spec.js']), ['code-reviewer']);
});

test('the service worker is read for security as well as for the page', () => {
  assert.deepEqual(reviewersFor(['table/sw.js']), ['code-reviewer', 'pages-reviewer', 'security-reviewer']);
});

test('a workflow or the image gets the infrastructure and security reviewers', () => {
  assert.deepEqual(reviewersFor(['.github/workflows/deploy.yml']), [
    'code-reviewer',
    'infra-reviewer',
    'security-reviewer',
  ]);
  assert.deepEqual(reviewersFor(['infra/main.bicep', 'Dockerfile']), ['code-reviewer', 'infra-reviewer', 'security-reviewer']);
});

test('a learning change gets the statistics reviewer', () => {
  assert.deepEqual(reviewersFor(['learning/src/downfall_learning/paired.py']), ['code-reviewer', 'stats-reviewer']);
  assert.deepEqual(reviewersFor(['docs/learning/journal/2026-11-15-1-a-reading.md']), ['copy-editor', 'stats-reviewer']);
});

test('a docs-only change gets the copy editor and no code reviewer', () => {
  assert.deepEqual(reviewersFor(['docs/tabletop/rulebook.md']), ['copy-editor']);
});

test('the host process and its sign-in plumbing get the security reviewer', () => {
  assert.deepEqual(reviewersFor(['src/DownfallArena.Cli/Table/TableServer.cs']), [
    'code-reviewer',
    'host-reviewer',
    'security-reviewer',
  ]);
  assert.deepEqual(reviewersFor(['src/DownfallArena.Cli/Hosting/HttpHost.cs']), ['code-reviewer', 'security-reviewer']);
});

test('the application side of tables and accounts gets the host reviewer, and accounts the security reviewer', () => {
  assert.deepEqual(reviewersFor(['src/DownfallArena.Application/Tables/OpenTable.cs']), ['code-reviewer', 'host-reviewer']);
  assert.deepEqual(reviewersFor(['src/DownfallArena.Application/Accounts/Suspension.cs']), [
    'code-reviewer',
    'host-reviewer',
    'security-reviewer',
  ]);
});

test('a session that signs nobody in is not a security change', () => {
  assert.deepEqual(reviewersFor(['src/DownfallArena.Cli/GameSession.cs']), ['code-reviewer']);
  assert.deepEqual(reviewersFor(['src/DownfallArena.Cli/Table/SteppedSession.cs']), ['code-reviewer', 'host-reviewer']);
});

test('what decides what CI and the local gate run gets the infrastructure reviewer', () => {
  for (const path of ['studio/browser/plan.js', 'scripts/verify.sh', 'scripts/gate/state.sh']) {
    assert.deepEqual(reviewersFor([path]), ['code-reviewer', 'infra-reviewer'], path);
  }
});

test('an agent, a skill or a rule is behaviour: it gets the code reviewer besides the copy editor', () => {
  for (const path of ['.claude/agents/host-reviewer.md', '.claude/skills/ship/SKILL.md', '.claude/rules/host.md']) {
    assert.deepEqual(reviewersFor([path]), ['code-reviewer', 'copy-editor'], path);
  }
});

test('the frozen prototypes call for nobody', () => {
  assert.deepEqual(reviewersFor(['legacy/arena/index.html']), []);
});

test('no paths, no reviewers', () => {
  assert.deepEqual(reviewersFor([]), []);
});

function frontMatter(name) {
  const text = readFileSync(join(root, '.claude/agents', `${name}.md`), 'utf8');
  const block = /^---\n([\s\S]*?)\n---\n/.exec(text);
  assert.ok(block, `${name}.md opens with a front matter block`);
  return Object.fromEntries(
    block[1].split('\n').map((line) => {
      const colon = line.indexOf(':');
      return [line.slice(0, colon).trim(), line.slice(colon + 1).trim()];
    }),
  );
}

test('every reviewer it names is an agent with that name', () => {
  for (const name of REVIEWERS) {
    assert.ok(existsSync(join(root, '.claude/agents', `${name}.md`)), `.claude/agents/${name}.md`);
    assert.equal(frontMatter(name).name, name);
  }
});

test('no reviewer lists a tool that edits', () => {
  for (const name of REVIEWERS) {
    const tools = frontMatter(name).tools.split(',').map((tool) => tool.trim());
    assert.ok(!tools.includes('Edit') && !tools.includes('Write'), `${name} lists ${tools.join(', ')}`);
  }
});

test('a reviewer that keeps a memory keeps it in the repository, and its prompt holds its writes to that folder', () => {
  for (const name of REVIEWERS) {
    const fields = frontMatter(name);
    if (!fields.memory) continue;
    // memory turns Write and Edit on for the agent; only its prompt keeps them to its own folder.
    assert.equal(fields.memory, 'project', name);
    const body = readFileSync(join(root, '.claude/agents', `${name}.md`), 'utf8');
    assert.ok(body.includes(`\`.claude/agent-memory/${name}/\``), `${name} names its memory folder`);
    assert.ok(body.includes('is the only thing you write'), `${name} holds its writes to it`);
  }
});
