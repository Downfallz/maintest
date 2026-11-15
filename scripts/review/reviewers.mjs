// Which reviewers a change calls for, decided from the paths it touches, so /reviewers picks them the same way
// every time. Each reviewer is an agent under .claude/agents; studio/reviewers.test.js holds both to that.
//
//   git diff --name-only origin/main... | node scripts/review/reviewers.mjs     # one name a line

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const isMarkdown = (path) => path.endsWith('.md');
const isTest = (path) => /\.(test|spec)\.[a-z]+$/.test(path) || path.startsWith('studio/browser/');
const under = (path, ...folders) => folders.some((folder) => path.startsWith(folder));
const fileName = (path) => path.slice(path.lastIndexOf('/') + 1);

// Who signs in, holds a key or a token, or reaches the operator: the names the host gives those files. A session
// is a sign-in only where the name begins with it (SessionClaim, StoredSessions), not a game's (GameSession).
const SIGNING_IN = /^([A-Za-z]*(Account|Operator|Admin|Push|SignedIn|SignIn|Token|Auth|JoinCode|Keys)|Session|StoredSession)[A-Za-z]*\.cs$/;

// What decides which jobs CI and the local gate run (ADR 0182).
const GATE_FILES = new Set(['studio/browser/plan.js', 'studio/browser/flakes.js', 'scripts/verify.sh']);

// In the order a report lists them: the general read first, then by area.
const RULES = [
  // An agent, a skill or a rule is markdown and behaviour.
  ['code-reviewer', (path) => !isMarkdown(path) || under(path, '.claude/')],
  ['domain-reviewer', (path) => under(path, 'src/DownfallArena.Domain/', 'src/DownfallArena.SharedKernel/')],
  [
    'host-reviewer',
    (path) =>
      under(
        path,
        'src/DownfallArena.Cli/Table/',
        'tests/DownfallArena.Cli.Tests/Table/',
        'src/DownfallArena.Infrastructure/Tables/',
        'src/DownfallArena.Infrastructure/Accounts/',
        'src/DownfallArena.Infrastructure/Work/',
        'src/DownfallArena.Application/Tables/',
        'src/DownfallArena.Application/Accounts/',
      ),
  ],
  [
    'pages-reviewer',
    (path) => under(path, 'table/', 'studio/', 'viewer/', 'battle/src/') && !isMarkdown(path) && !isTest(path),
  ],
  ['copy-editor', (path) => isMarkdown(path) || (under(path, 'table/', 'studio/', 'viewer/') && path.endsWith('.html'))],
  [
    'infra-reviewer',
    (path) =>
      under(path, 'infra/', '.github/', 'scripts/gate/') ||
      GATE_FILES.has(path) ||
      path === 'Dockerfile' ||
      path === '.dockerignore',
  ],
  [
    'security-reviewer',
    (path) =>
      (under(path, 'src/DownfallArena.Cli/', 'src/DownfallArena.Infrastructure/') && SIGNING_IN.test(fileName(path))) ||
      under(
        path,
        'src/DownfallArena.Cli/Hosting/',
        'src/DownfallArena.Infrastructure/Accounts/',
        'src/DownfallArena.Application/Accounts/',
        'infra/',
        '.github/workflows/',
        '.github/scripts/',
      ) ||
      [
        'src/DownfallArena.Cli/Table/TableServer.cs',
        'table/sw.js',
        'table/account.js',
        'table/push.js',
        'table/session.js',
        'Dockerfile',
      ].includes(path),
  ],
  ['stats-reviewer', (path) => under(path, 'learning/', 'docs/learning/', 'models/', 'benchmarks/', 'data/balance/')],
];

export const REVIEWERS = RULES.map(([name]) => name);

/** The reviewers the paths call for, in the order of RULES. The frozen prototypes call for nobody. */
export function reviewersFor(paths) {
  const read = paths.map((path) => path.trim()).filter((path) => path && !path.startsWith('legacy/'));
  return RULES.filter(([, applies]) => read.some(applies)).map(([name]) => name);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const paths = readFileSync(0, 'utf8').split('\n');
  for (const name of reviewersFor(paths)) console.log(name);
}
