// What a diff calls for (ADR 0182): the browser journeys, the real-engine and release jobs, the table's
// image and the showcase, decided from the paths a change touches. CI's `changes` job asks it on every run, so the `gate` check
// always reports while a change no page reads plays no journey. A path nothing here names calls for nothing.
//
//   git diff --name-only <base> HEAD | node studio/browser/plan.js    # what those paths call for, on main
//   ... | node studio/browser/plan.js --pull-request                    # the same, for a pull request (ADR 0190)
//   node studio/browser/plan.js --all                                  # everything: a dispatch, the nightly run
//   node studio/browser/plan.js --journey-job 'desktop replays'        # that journeys job's Playwright arguments
//   git diff --name-only origin/main... | node studio/browser/plan.js --gate   # the local gate's optional steps
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

// The console commands no page reaches (ADR 0167).
const CONSOLE_ONLY = new Set([
  'src/DownfallArena.Cli/ConsoleAgent.cs',
  'src/DownfallArena.Cli/ConsoleMatchLog.cs',
  'src/DownfallArena.Cli/EvaluationConsole.cs',
]);

// What the real engine is built from besides `src/`.
const BUILD_FILES = new Set(['global.json', 'Directory.Build.props', 'Directory.Packages.props', 'DownfallArena.slnx']);

// The journeys' own plumbing: a change to one of these can break any spec.
const WORKFLOWS = new Set(['.github/workflows/ci.yml', '.github/workflows/studio-browser.yml']);

// What the table's image carries, and what builds, tries and deploys it.
const IMAGE_FILES = new Set([
  'Dockerfile',
  '.dockerignore',
  'viewer/index.html',
  'viewer/viewer.css',
  'learning/seatable.json',
  'docs/tabletop/playtest.rules.json',
  'global.json',
  'Directory.Build.props',
  'Directory.Packages.props',
  '.github/scripts/try-table-image.sh',
  '.github/workflows/image.yml',
  '.github/workflows/rollout.yml',
]);
const IMAGE_FOLDERS = ['infra/', 'src/', 'tools/', 'data/', 'table/', 'battle/', 'learning/weights/', 'models/', 'scenarios/'];

// What `node showcase/build.mjs` reads (ADR 0185), and what deploys it: the pages, the catalogue it renders,
// the branding, the art and the modules that resolve it. Its own tests and its README are not read by the build.
const SHOWCASE_FILES = new Set([
  'docs/art/branding.css',
  'battle/src/cue-art.js',
  'table/battle-evolution.js',
  'table/site-origins.js',
  'table/icon.svg',
  'table/brand-mark.svg',
  '.github/workflows/showcase.yml',
]);
const SHOWCASE_FOLDERS = ['showcase/', 'data/Creatures/', 'data/Spells/', 'data/Tiers/', 'data/presentation/', 'table/battle-art/', 'table/fonts/'];
const builtIntoTheShowcase = (path) =>
  !path.endsWith('.md') &&
  !path.endsWith('.test.js') &&
  (SHOWCASE_FILES.has(path) || SHOWCASE_FOLDERS.some((folder) => path.startsWith(folder)) || /^table\/battle-[a-z-]*art\.js$/.test(path));

// What defines the release job itself: a pull request that changes one plays every profile, not WebKit alone
// (ADR 0190), so the profiles it narrows away on other pull requests are not first played on main.
const RELEASE_DEFINITION = new Set(['.github/workflows/studio-browser.yml', 'studio/browser/battle.release.config.js', 'studio/browser/plan.js']);

// The heaviest desktop specs: a third of desktop's time, in three files. A shard splits by test count, so they
// play in a job of their own and the rest of desktop in two halves without them (M3). A spec that grows that
// heavy joins the pattern.
const HEAVY = String.raw`battle-(playback|pacing|live)\.spec\.js`;
const isHeavy = (spec) => new RegExp(HEAVY).test(spec);

// The mock-host journeys' jobs, side by side in CI, each with the Playwright arguments it plays and the specs it
// has something to play in. Tablet runs only table.ux.spec.js (playwright.config.js), so it shares a desktop half.
export const JOURNEY_JOBS = [
  { name: 'small-phone', arguments: ['--project=small-phone'], plays: () => true },
  { name: 'phone', arguments: ['--project=phone'], plays: () => true },
  { name: 'desktop replays', arguments: ['--project=desktop', `--grep=${HEAVY}`], plays: isHeavy },
  ...[1, 2].map((shard) => ({
    name: `desktop and tablet ${shard}/2`,
    arguments: ['--project=desktop', '--project=tablet', `--grep-invert=${HEAVY}`, `--shard=${shard}/2`],
    plays: (spec) => !isHeavy(spec),
  })),
];

// The specs the release profiles play besides their own (battle.release.config.js).
const RELEASE_FILES = new Set(['battle.loop.js', 'battle.release.config.js', 'battle-release.spec.js', 'battle-live.spec.js']);

const isBattleSpec = (spec) => spec.startsWith('battle-');
const isStudioSpec = (spec) => spec === 'studio.spec.js' || spec === 'branding.spec.js';
const isTableSpec = (spec) => spec !== 'studio.spec.js';
const anySpec = () => true;
const carriedByTheImage = (path) => IMAGE_FILES.has(path) || IMAGE_FOLDERS.some((folder) => path.startsWith(folder));

/** What one file of the suite itself calls for: a spec is its own journey, a fixture can break any of them. */
function suiteFile(file) {
  const release = RELEASE_FILES.has(file);
  if (file.endsWith('.spec.js')) return { journeys: (spec) => spec === file, release };
  if (/^(table|battle)\.[a-z]+\.js$/.test(file)) return { realEngine: true, release };
  return { journeys: anySpec, realEngine: true, release: true };
}

// The first rule a path matches says what it calls for: `journeys` chooses among the specs, and `realEngine`
// and `release` ask for those jobs. `release: 'main'` asks for the release profiles on main alone: the engine and
// the content are judged on a pull request by the real-engine journeys, and the release profiles are for what
// WebKit renders and for the release job itself (M4). A path no rule matches, or a rule that returns nothing,
// plays nothing.
const RULES = [
  // Markdown is read by no page, no spec and no image; the pages' own tests are run by `node --test`.
  { matches: (path) => path.endsWith('.md'), calls: () => ({}) },
  { matches: (path) => WORKFLOWS.has(path), calls: () => ({ journeys: anySpec, realEngine: true, release: true }) },
  { matches: (path) => path.startsWith('studio/browser/'), calls: (path) => suiteFile(path.slice('studio/browser/'.length)) },
  { matches: (path) => path.startsWith('battle/'), calls: () => ({ journeys: isBattleSpec, realEngine: true, release: true }) },
  { matches: (path) => /^(table|studio)\/.*\.test\.js$/.test(path), calls: () => ({}) },
  { matches: (path) => path.startsWith('table/'), calls: () => ({ journeys: isTableSpec, realEngine: true, release: true }) },
  { matches: (path) => path.startsWith('studio/'), calls: () => ({ journeys: isStudioSpec }) },
  { matches: (path) => path === 'viewer/viewer.css', calls: () => ({ journeys: anySpec }) },
  { matches: (path) => path.startsWith('data/'), calls: () => ({ journeys: anySpec, realEngine: true, release: 'main' }) },
  // The tutorial journey plays the lesson of the tutorial files themselves.
  { matches: (path) => path.startsWith('scenarios/tutorials/'), calls: () => ({ journeys: (spec) => spec === 'battle-tutorial.spec.js' }) },
  { matches: (path) => CONSOLE_ONLY.has(path), calls: () => ({}) },
  {
    matches: (path) => path.startsWith('src/') || path.startsWith('tools/') || BUILD_FILES.has(path),
    calls: () => ({ realEngine: true, release: 'main' }),
  },
];

// Markdown the learning tests read: the effect kinds of data/README.md (test_knobs.py), which the light build
// does not run. Every other Markdown is read by Architecture.Tests or the pages' own tests, or by nothing.
const readByTheFullBuild = (path) => path.startsWith('data/');

/**
 * Decide what the changed paths call for. `specs` is every mock-host journey file the suite holds.
 * Returns `journeys` ('none', 'all', or the spec files separated by spaces), `release` ('none', 'webkit' or
 * 'all': a pull request plays the release profiles' WebKit alone, main all three, ADR 0190), `build` ('full', or
 * 'light' for a pull request of Markdown alone, ADR 0192) and three booleans.
 */
export function plan(paths, specs, { pullRequest = false } = {}) {
  const changed = paths.map((line) => line.trim()).filter(Boolean);
  const called = changed.map((path) => RULES.find((rule) => rule.matches(path))?.calls(path) ?? {});
  const journeys = specs.filter((spec) => called.some((call) => call.journeys?.(spec)));

  let chosen = journeys.join(' ') || 'none';
  if (journeys.length === specs.length && specs.length > 0) chosen = 'all';
  return {
    journeys: chosen,
    realEngine: called.some((call) => call.realEngine === true),
    release: releaseProfiles(
      called.some((call) => call.release === true || (!pullRequest && call.release === 'main')),
      pullRequest && !changed.some((path) => RELEASE_DEFINITION.has(path)),
    ),
    image: changed.some((path) => !path.endsWith('.md') && carriedByTheImage(path)),
    showcase: changed.some(builtIntoTheShowcase),
    build: pullRequest && changed.length > 0 && changed.every((path) => path.endsWith('.md') && !readByTheFullBuild(path)) ? 'light' : 'full',
  };
}

/** The journeys jobs that have something to play: `journeys` is 'all', 'none', or spec files separated by spaces. */
export function journeyJobs(journeys) {
  if (journeys === 'all') return JOURNEY_JOBS.map((job) => job.name);
  const planned = journeys.split(' ').filter((spec) => spec && spec !== 'none');
  return JOURNEY_JOBS.filter((job) => planned.some(job.plays)).map((job) => job.name);
}

/** Which release profiles a run plays when its diff calls for them: narrowed, the one engine no other job plays. */
function releaseProfiles(called, narrowed) {
  if (!called) return 'none';
  return narrowed ? 'webkit' : 'all';
}

/** Everything: what a run that has no diff to read plays. */
export function everything() {
  return { journeys: 'all', realEngine: true, release: 'all', image: true, showcase: true, build: 'full' };
}

// What the local gate (scripts/verify.sh) adds to the steps it always runs, each with the paths it reads: the
// battle bundle is rebuilt from `battle/` and the table's modules it imports (`table/battle-*.js`, `account.js`); the learning project lints and runs `scripts/` (test_iterate_script.py)
// and reads the samples, the studio's value fixture, the content, its README included (test_knobs.py), and the
// release job's name in the browser workflow (test_workflow_minutes.py); the
// content builder is built from the engine. Markdown is read by the learning tests only.
const isCode = (path) => !path.endsWith('.md');
const GATE_STEPS = {
  battle: (path) =>
    isCode(path) &&
    (path.startsWith('battle/') || (path.startsWith('table/') && !path.startsWith('table/battle-build/') && !path.endsWith('.test.js'))),
  learning: (path) =>
    (isCode(path) && (path.startsWith('learning/') || path.startsWith('scripts/') || path.startsWith('viewer/samples/'))) ||
    path === 'studio/value.fixture.json' ||
    path === '.github/workflows/studio-browser.yml' ||
    path.startsWith('data/'),
  content: (path) =>
    isCode(path) && (path.startsWith('data/') || path.startsWith('tools/') || path.startsWith('src/') || BUILD_FILES.has(path)),
};

/** The local gate's optional steps the changed paths call for. */
export function gate(paths) {
  const changed = paths.map((line) => line.trim()).filter(Boolean);
  return Object.fromEntries(Object.entries(GATE_STEPS).map(([step, reads]) => [step, changed.some(reads)]));
}

/** The plan as the `key=value` lines a workflow appends to its outputs. */
export function outputs(planned) {
  return [
    `journeys=${planned.journeys}`,
    `journey-jobs=${JSON.stringify(journeyJobs(planned.journeys))}`,
    `real-engine=${planned.realEngine}`,
    `release=${planned.release}`,
    `image=${planned.image}`,
    `showcase=${planned.showcase}`,
    `build=${planned.build}`,
  ].join('\n');
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const here = dirname(fileURLToPath(import.meta.url));
  const specs = readdirSync(here).filter((file) => file.endsWith('.spec.js'));
  const job = process.argv.indexOf('--journey-job');
  if (job >= 0) {
    // One argument a line, for `mapfile`: the grep pattern is never split by the shell or matched as a glob.
    const found = JOURNEY_JOBS.find((candidate) => candidate.name === process.argv[job + 1]);
    if (!found) {
      console.error(`No journeys job is named ${JSON.stringify(process.argv[job + 1])}.`);
      process.exit(2);
    }
    console.log(found.arguments.join('\n'));
  } else if (process.argv.includes('--gate')) {
    const steps = gate(readFileSync(0, 'utf8').split('\n'));
    console.log(Object.entries(steps).map(([step, called]) => `${step}=${called}`).join('\n'));
  } else {
    const pullRequest = process.argv.includes('--pull-request');
    const planned = process.argv.includes('--all') ? everything() : plan(readFileSync(0, 'utf8').split('\n'), specs, { pullRequest });
    console.log(outputs(planned));
  }
}
