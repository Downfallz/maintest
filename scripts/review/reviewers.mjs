// Which reviewers a change calls for, decided from the paths it touches, so /reviewers picks them the same way
// every time. Each reviewer is an agent under .claude/agents; studio/reviewers.test.js holds both to that.
// Given the whole diff rather than only its names, it also reads the lines a page script adds (issue #606).
//
//   git diff origin/main... | node scripts/review/reviewers.mjs                 # one name a line
//   git diff --name-only origin/main... | node scripts/review/reviewers.mjs     # the paths alone
//
// Adding a reviewer: its agent under .claude/agents (read only, "the range you are given" in its prompt), a rule
// below and its tests in studio/reviewers.test.js; narrow the sibling reviewer whose scope it takes over; the W5
// "Built" note in docs/engineering/ai-workflow-plan.md; the lists in CLAUDE.md and docs/reference/commands.md.

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const isMarkdown = (path) => path.endsWith('.md');
const isTest = (path) => /\.(test|spec)\.[a-z]+$/.test(path) || path.startsWith('studio/browser/');
const under = (path, ...folders) => folders.some((folder) => path.startsWith(folder));
const fileName = (path) => path.slice(path.lastIndexOf('/') + 1);

// Who signs in, holds a key or a token, or reaches the operator: the names the host gives those files. A session
// is a sign-in only where the name begins with it (SessionClaim, StoredSessions), not a game's (GameSession).
const SIGNING_IN = /^([A-Za-z]*(Account|Operator|Admin|Push|SignedIn|SignIn|Token|Auth|JoinCode|Keys)|Session|StoredSession)[A-Za-z]*\.cs$/;

// What a page looks like: its sheets, its markup, its pictures, its icon and fonts, the app's manifest.
const LOOK = /\.(css|html|svg|png|webp|jpe?g|gif|avif|woff2|webmanifest)$/;

// The registries of the battle's pictures: each one's canvas, silhouette bounds and ground anchor.
const ART_REGISTRY = /^table\/battle-[a-z-]*art\.js$/;

// A page's own script, not its tests: where a raw colour or markup built by hand can hide.
const PAGE_SCRIPT = /^(table|studio|viewer)\/[^/]+\.m?js$/;

// What an added line of a page script says about the look: a raw colour (a hexadecimal in a string, rgb(), hsl()),
// an inline style, or a class it sets, where a token or a shared part of identity.css may already do.
const LOOKS_LIKE_STYLE = [
  /['"`\s(:]#(?:[0-9a-f]{3,4}|[0-9a-f]{6}|[0-9a-f]{8})\b/i,
  /\b(?:rgba?|hsla?)\(/,
  /\bstyle\s*=|\.style\b|\bcssText\b/,
  /\bclassName\b/,
];

const addsStyle = (path, added) =>
  PAGE_SCRIPT.test(path) &&
  !isTest(path) &&
  (added.get(path) ?? []).some((line) => LOOKS_LIKE_STYLE.some((look) => look.test(line)));

// What makes the brand and the art: the palette's synchronizer and the matte of the art.
const BRAND_TOOLS = new Set(['scripts/sync-branding.mjs', 'scripts/matte-opaque-art.py', 'scripts/design-system/build.mjs']);

// What the rules are made of (docs/tabletop/rulebook-plan.md, R5): the domain that plays them, their specification
// and words, the decisions, the content and the hosted rule set the book draws from, the book, and the rule text the
// pages and the tutorials show. The ADRs' generated index and their template decide nothing.
const RULES_SOURCES = [
  'src/DownfallArena.Domain/',
  'data/Spells/',
  'data/Tiers/',
  'data/Creatures/',
  'data/TalentTrees/',
  'data/balance/',
  'rules/',
  'scenarios/tutorials/',
];
const RULES_FILES = new Set([
  'docs/domain/game-rules.md',
  'docs/domain/glossary.md',
  'docs/domain/player-terminology.md',
  'docs/tabletop/playtest.rules.json',
  'table/terms.js',
  'table/battle-guidance.js',
]);
const AN_ADR = /^docs\/adr\/(?!0000-)\d{4}-[^/]+\.md$/;

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
  [
    'design-reviewer',
    (path, added) =>
      addsStyle(path, added) ||
      (under(path, 'table/', 'studio/', 'viewer/') && LOOK.test(path) && !isTest(path)) ||
      ART_REGISTRY.test(path) ||
      under(path, 'battle/src/', 'docs/art/') ||
      path === 'battle/ART.md' ||
      BRAND_TOOLS.has(path),
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
  ['rules-keeper', (path) => under(path, ...RULES_SOURCES) || RULES_FILES.has(path) || AN_ADR.test(path)],
];

export const REVIEWERS = RULES.map(([name]) => name);

/**
 * The reviewers the paths call for, in the order of RULES. `added` maps a path to the lines the change adds to it,
 * when the diff is known. The frozen prototypes call for nobody.
 */
export function reviewersFor(paths, added = new Map()) {
  const read = paths.map((path) => path.trim()).filter((path) => path && !path.startsWith('legacy/'));
  return RULES.filter(([, applies]) => read.some((path) => applies(path, added))).map(([name]) => name);
}

/** The paths of a unified diff (`git diff`), and the lines it adds to each. A deleted file is still a path. */
export function readDiff(text) {
  const paths = [];
  const added = new Map();
  let current = null;
  for (const line of text.split('\n')) {
    const header = /^diff --git a\/(.+) b\/(.+)$/.exec(line);
    if (header) {
      current = header[2];
      paths.push(current);
      added.set(current, []);
    } else if (current && line.startsWith('+') && !line.startsWith('+++ ')) {
      added.get(current).push(line.slice(1));
    }
  }
  return { paths, added };
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const input = readFileSync(0, 'utf8');
  const { paths, added } = input.startsWith('diff --git ')
    ? readDiff(input)
    : { paths: input.split('\n'), added: new Map() };
  for (const name of reviewersFor(paths, added)) console.log(name);
}
