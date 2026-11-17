// Which reviewers a change calls for (scripts/review/reviewers.mjs, read by the /reviewers skill), and that every
// reviewer it names is an agent under .claude/agents, read only except for its memory.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { REVIEWERS, readDiff, reviewersFor } from '../scripts/review/reviewers.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

test('an engine change gets the code reviewer alone', () => {
  assert.deepEqual(reviewersFor(['src/DownfallArena.Application/Matches/PlayRound.cs']), ['code-reviewer']);
});

test('a domain change also gets the domain reviewer, and the rules keeper: the domain holds the rules', () => {
  assert.deepEqual(reviewersFor(['src/DownfallArena.Domain/Matches/Match.cs']), [
    'code-reviewer',
    'domain-reviewer',
    'rules-keeper',
  ]);
  assert.deepEqual(reviewersFor(['src/DownfallArena.SharedKernel/Result.cs']), ['code-reviewer', 'domain-reviewer']);
});

test('a route change gets the host reviewer, and the security reviewer when it signs someone in', () => {
  assert.deepEqual(reviewersFor(['src/DownfallArena.Cli/Table/LobbyApi.cs']), ['code-reviewer', 'host-reviewer']);
  assert.deepEqual(reviewersFor(['src/DownfallArena.Cli/Table/AccountSessions.cs']), [
    'code-reviewer',
    'host-reviewer',
    'security-reviewer',
  ]);
});

test('a page change gets the pages reviewer, its look the design reviewer, and its words the copy editor', () => {
  assert.deepEqual(reviewersFor(['table/lobby.js']), ['code-reviewer', 'pages-reviewer']);
  assert.deepEqual(reviewersFor(['table/lobby.html']), [
    'code-reviewer',
    'pages-reviewer',
    'design-reviewer',
    'copy-editor',
  ]);
});

test('a style sheet, an icon or a picture gets the design reviewer', () => {
  for (const path of ['table/identity.css', 'studio/studio.css', 'viewer/viewer.css', 'table/icon.svg']) {
    assert.deepEqual(reviewersFor([path]), ['code-reviewer', 'pages-reviewer', 'design-reviewer'], path);
  }
  assert.deepEqual(reviewersFor(['table/battle-art/brute.webp']), ['code-reviewer', 'pages-reviewer', 'design-reviewer']);
  assert.deepEqual(reviewersFor(['table/manifest.webmanifest']), ['code-reviewer', 'pages-reviewer', 'design-reviewer']);
});

test('the registries of the battle pictures get the design reviewer', () => {
  for (const path of ['table/battle-art.js', 'table/battle-evolution-art.js', 'table/battle-warped-hybrid-art.js']) {
    assert.deepEqual(reviewersFor([path]), ['code-reviewer', 'pages-reviewer', 'design-reviewer'], path);
  }
});

test('the battle renderer gets the design reviewer besides the pages reviewer', () => {
  assert.deepEqual(reviewersFor(['battle/src/spell-effects.ts']), ['code-reviewer', 'pages-reviewer', 'design-reviewer']);
});

test('the brand, the art direction and their tools get the design reviewer', () => {
  assert.deepEqual(reviewersFor(['docs/art/branding.css']), ['code-reviewer', 'design-reviewer']);
  assert.deepEqual(reviewersFor(['docs/art/spell-vfx.md']), ['design-reviewer', 'copy-editor']);
  assert.deepEqual(reviewersFor(['battle/ART.md']), ['design-reviewer', 'copy-editor']);
  assert.deepEqual(reviewersFor(['docs/art/images/branding/lobby-phone.jpg']), ['code-reviewer', 'design-reviewer']);
  assert.deepEqual(reviewersFor(['scripts/sync-branding.mjs']), ['code-reviewer', 'design-reviewer']);
  assert.deepEqual(reviewersFor(['scripts/matte-opaque-art.py']), ['code-reviewer', 'design-reviewer']);
  assert.deepEqual(reviewersFor(['scripts/design-system/build.mjs']), ['code-reviewer', 'design-reviewer']);
});

test('a page script named without its diff, or a capture of a test run, is not a design change', () => {
  assert.deepEqual(reviewersFor(['table/battle-planning.js']), ['code-reviewer', 'pages-reviewer']);
  assert.deepEqual(reviewersFor(['docs/tabletop/images/battle-lab/lab-phone.png']), ['code-reviewer']);
});

test('a page script that adds a raw colour, an inline style or a class calls for the design reviewer', () => {
  const design = ['code-reviewer', 'pages-reviewer', 'design-reviewer'];
  for (const line of [
    "  ctx.fillStyle = '#c9a227';",
    '  const glow = `rgba(255, 200, 80, ${alpha})`;',
    "  badge.style.color = 'var(--gold)';",
    '  return `<span style="opacity: .6">${name}</span>`;',
    "  tag.className = 'tag tag-ally';",
  ]) {
    assert.deepEqual(reviewersFor(['table/table.js'], new Map([['table/table.js', [line]]])), design, line);
  }
});

test('a page script that adds no colour and no markup is still not a design change', () => {
  const added = new Map([['table/mat.js', ["  const seat = document.querySelector('#seat');", '  return round + 1;']]]);
  assert.deepEqual(reviewersFor(['table/mat.js'], added), ['code-reviewer', 'pages-reviewer']);
});

test('a page test that adds a colour is not a design change', () => {
  const added = new Map([['table/mat.test.js', ["  assert.equal(color, '#c9a227');"]]]);
  assert.deepEqual(reviewersFor(['table/mat.test.js'], added), ['code-reviewer']);
});

test('a unified diff gives its paths and the lines it adds to each, not the ones it removes', () => {
  const diff = [
    'diff --git a/table/mat.js b/table/mat.js',
    'index 1111111..2222222 100644',
    '--- a/table/mat.js',
    '+++ b/table/mat.js',
    '@@ -1,2 +1,2 @@',
    " const seat = '#seat';",
    "-ctx.fillStyle = '#c9a227';",
    "+ctx.fillStyle = gold;",
    'diff --git a/table/lobby.js b/table/lobby.js',
    'deleted file mode 100644',
    '',
  ].join('\n');
  const { paths, added } = readDiff(diff);
  assert.deepEqual(paths, ['table/mat.js', 'table/lobby.js']);
  assert.deepEqual(added.get('table/mat.js'), ['ctx.fillStyle = gold;']);
  assert.deepEqual(reviewersFor(paths, added), ['code-reviewer', 'pages-reviewer']);
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

test('what the rules are made of gets the rules keeper', () => {
  assert.deepEqual(reviewersFor(['docs/domain/game-rules.md']), ['copy-editor', 'rules-keeper']);
  assert.deepEqual(reviewersFor(['docs/domain/glossary.md']), ['copy-editor', 'rules-keeper']);
  assert.deepEqual(reviewersFor(['docs/domain/player-terminology.md']), ['copy-editor', 'rules-keeper']);
  assert.deepEqual(reviewersFor(['docs/adr/0114-a-shared-package-is-held-by-the-whole-side.md']), ['copy-editor', 'rules-keeper']);
  for (const path of [
    'data/Spells/base/heavy_strike.v1.json',
    'data/Tiers/titan.v1.json',
    'data/Creatures/main.v1.json',
    'data/TalentTrees/talent_tree.v1.json',
    'docs/tabletop/playtest.rules.json',
  ]) {
    assert.deepEqual(reviewersFor([path]), ['code-reviewer', 'rules-keeper'], path);
  }
  assert.deepEqual(reviewersFor(['data/balance/knobs.json']), ['code-reviewer', 'stats-reviewer', 'rules-keeper']);
});

test('the book itself, and the rule text of the pages and the tutorials, get the rules keeper', () => {
  assert.deepEqual(reviewersFor(['rules/03-the-round.md']), ['copy-editor', 'rules-keeper']);
  assert.deepEqual(reviewersFor(['rules/moved.json']), ['code-reviewer', 'rules-keeper']);
  for (const path of ['table/terms.js', 'table/battle-guidance.js']) {
    assert.deepEqual(reviewersFor([path]), ['code-reviewer', 'pages-reviewer', 'rules-keeper'], path);
  }
  assert.deepEqual(reviewersFor(['battle/src/tempo.ts']), ['code-reviewer', 'pages-reviewer', 'design-reviewer', 'rules-keeper']);
  assert.deepEqual(reviewersFor(['showcase/guide.mjs']), ['code-reviewer', 'rules-keeper']);
  assert.deepEqual(reviewersFor(['scenarios/tutorials/your-first-combat.json']), ['code-reviewer', 'rules-keeper']);
});

test('an index of the ADRs, the template, the print draft or another page script is not a rules change', () => {
  assert.deepEqual(reviewersFor(['docs/adr/README.md']), ['copy-editor']);
  assert.deepEqual(reviewersFor(['docs/adr/0000-template.md']), ['copy-editor']);
  assert.deepEqual(reviewersFor(['docs/tabletop/rulebook.md']), ['copy-editor']);
  assert.deepEqual(reviewersFor(['table/lobby.js']), ['code-reviewer', 'pages-reviewer']);
  assert.deepEqual(reviewersFor(['scenarios/greedy-1v1-round-3.json']), ['code-reviewer']);
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

test('every reviewer reads the range /reviewers gives it, not only the checked-out branch', () => {
  for (const name of REVIEWERS) {
    const body = readFileSync(join(root, '.claude/agents', `${name}.md`), 'utf8');
    assert.ok(body.includes('the range you are given'), name);
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
    // One file per branch: two pull requests in flight never append to the same file, so the memory never conflicts.
    assert.ok(body.includes('`lessons/<branch>.md`'), `${name} writes a lesson in its branch's own file`);
  }
});
