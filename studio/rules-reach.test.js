// A lesson in `.claude/rules/` helps only when it loads where the mistake is made: a rule file loads when a session
// touches a path its front matter names. Each lesson below was missed because the file that held it did not load
// (#979, #991, #1017), so this test holds where each one reaches, by a file in each place the mistake was made.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const RULES = '.claude/rules';

/** A rule file's front matter paths, as globs, and its body. */
export function ruleFile(markdown) {
  const front = /^---\n([\s\S]*?)\n---\n/.exec(markdown);
  const globs = front ? [...front[1].matchAll(/^\s+-\s+"([^"]+)"\s*$/gm)].map(([, glob]) => glob) : [];
  return { globs, body: front ? markdown.slice(front[0].length) : markdown };
}

/** Whether a repository path matches a glob of a rule's front matter: `**` crosses folders, `*` does not. */
export function matches(glob, path) {
  let pattern = '';
  for (let i = 0; i < glob.length; i++) {
    if (glob.startsWith('**/', i)) { pattern += '(?:.*/)?'; i += 2; }
    else if (glob.startsWith('**', i)) { pattern += '.*'; i += 1; }
    else if (glob[i] === '*') pattern += '[^/]*';
    else pattern += glob[i].replace(/[.+?^${}()|[\]\\]/g, '\\$&');
  }
  return new RegExp(`^${pattern}$`).test(path);
}

const rules = readdirSync(join(root, RULES)).filter(name => name.endsWith('.md'))
  .map(name => ({ name, ...ruleFile(readFileSync(join(root, RULES, name), 'utf8')) }));

/** The rule files that hold a lesson (its words, a line break read as a space) and load on a path. */
const reaching = (words, path) => rules
  .filter(rule => words.test(rule.body.replace(/\n\s*/g, ' ')) && rule.globs.some(glob => matches(glob, path)))
  .map(rule => rule.name);

// A lesson: the words that say it, and a file in each place a session made the mistake from.
const LESSONS = [
  {
    issue: '#1017, a field added to a route\'s answer is tested through the route',
    words: /field added to a route's answer gets one test through the route/,
    paths: [
      'src/DownfallArena.Cli/Table/TableApi.cs',
      'src/DownfallArena.Application/Catalogue/CatalogueProjection.cs',
      'src/DownfallArena.Infrastructure/Content/SpellVisual.cs',
      'tests/DownfallArena.Application.Tests/Catalogue/CatalogueProjectionTests.cs',
    ],
  },
  {
    issue: '#991, a service that sets a field in a route\'s answer is required',
    words: /service that sets a field in a route's answer is a required constructor parameter/,
    paths: ['src/DownfallArena.Cli/Table/StatsApi.cs', 'tests/DownfallArena.Cli.Tests/Table/StatsApiTests.cs'],
  },
  {
    issue: '#979, a changed visible string is grepped in the real-engine journeys and the walk',
    words: /old text in every suite: `studio\/browser\/`.*?`scripts\/ux\/`/,
    paths: ['table/lobby-tutorials.js', 'table/battle.html', 'studio/browser/battle.tutorials.js'],
  },
  {
    issue: '#979, a window that fades is read by `open`, not `isVisible`, in every suite',
    words: /`isVisible`/,
    paths: ['table/battle.css', 'table/battle-tutorial.js', 'studio/browser/battle.tutorials.js'],
  },
];

test('a glob of a rule reaches the paths under it, and only those', () => {
  assert.ok(matches('src/DownfallArena.Application/**', 'src/DownfallArena.Application/Catalogue/A.cs'));
  assert.ok(!matches('src/DownfallArena.Application/**', 'src/DownfallArena.Applications/A.cs'));
  assert.ok(matches('**/AGENTS.md', 'AGENTS.md'));
  assert.ok(matches('**/AGENTS.md', 'table/AGENTS.md'));
  assert.ok(!matches('table/*.js', 'table/sub/a.js'));
  assert.ok(matches('Dockerfile', 'Dockerfile'));
  assert.deepEqual(ruleFile('---\npaths:\n  - "tests/**"\n---\n\n# T\n'), { globs: ['tests/**'], body: '\n# T\n' });
});

for (const lesson of LESSONS) {
  test(`the lesson loads wherever its mistake is made: ${lesson.issue}`, () => {
    const missed = lesson.paths.filter(path => reaching(lesson.words, path).length === 0);
    assert.deepEqual(missed, [], `no rule under ${RULES} says it and loads on these paths`);
  });
}
