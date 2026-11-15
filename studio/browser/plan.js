// What a diff calls for (ADR 0182): the browser journeys, the real-engine and release jobs, and the table's
// image, decided from the paths a change touches. CI's `changes` job asks it on every run, so the `gate` check
// always reports while a change no page reads plays no journey. A path nothing here names calls for nothing.
//
//   git diff --name-only <base> HEAD | node studio/browser/plan.js    # what those paths call for
//   node studio/browser/plan.js --all                                  # everything: a dispatch, the nightly run
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
const IMAGE_FOLDERS = ['infra/', 'src/', 'tools/', 'data/', 'table/', 'battle/', 'learning/weights/', 'models/'];

// The specs the release profiles play besides their own (battle.release.config.js).
const RELEASE_FILES = new Set(['battle.loop.js', 'battle.release.config.js', 'battle-release.spec.js', 'battle-live.spec.js']);

const isBattleSpec = (spec) => spec.startsWith('battle-');
const isStudioSpec = (spec) => spec === 'studio.spec.js' || spec === 'branding.spec.js';
const isTableSpec = (spec) => spec !== 'studio.spec.js';

/**
 * Decide what the changed paths call for. `specs` is every mock-host journey file the suite holds.
 * Returns `journeys` ('none', 'all', or the spec files separated by spaces) and three booleans.
 */
export function plan(paths, specs) {
  const journeys = new Set();
  const calls = { realEngine: false, release: false, image: false };
  const journeysOf = (matches) => specs.filter(matches).forEach((spec) => journeys.add(spec));
  const engine = () => {
    calls.realEngine = true;
    calls.release = true;
  };
  const everything = () => {
    journeysOf(() => true);
    engine();
  };

  for (const path of paths.map((line) => line.trim()).filter(Boolean)) {
    // Markdown is read by no page, no spec and no image.
    if (path.endsWith('.md')) continue;

    if (IMAGE_FILES.has(path) || IMAGE_FOLDERS.some((folder) => path.startsWith(folder))) calls.image = true;

    if (WORKFLOWS.has(path)) {
      everything();
    } else if (path.startsWith('studio/browser/')) {
      const file = path.slice('studio/browser/'.length);
      if (RELEASE_FILES.has(file)) calls.release = true;
      if (file.endsWith('.spec.js')) journeysOf((spec) => spec === file);
      else if (/^(table|battle)\.[a-z]+\.js$/.test(file)) calls.realEngine = true;
      else everything();
    } else if (path.startsWith('battle/')) {
      journeysOf(isBattleSpec);
      engine();
    } else if (path.startsWith('table/')) {
      // The pages' own tests are run by `node --test`; no journey reads them.
      if (path.endsWith('.test.js')) continue;
      journeysOf(isTableSpec);
      engine();
    } else if (path.startsWith('studio/')) {
      if (path.endsWith('.test.js')) continue;
      journeysOf(isStudioSpec);
    } else if (path === 'viewer/viewer.css') {
      journeysOf(() => true);
    } else if (path.startsWith('data/')) {
      everything();
    } else if (path.startsWith('src/')) {
      if (!CONSOLE_ONLY.has(path)) engine();
    } else if (path.startsWith('tools/') || BUILD_FILES.has(path)) {
      engine();
    }
  }

  let chosen = 'none';
  if (journeys.size > 0) chosen = journeys.size === specs.length ? 'all' : specs.filter((spec) => journeys.has(spec)).join(' ');
  return { journeys: chosen, ...calls };
}

/** Everything: what a run that has no diff to read plays. */
export function everything() {
  return { journeys: 'all', realEngine: true, release: true, image: true };
}

/** The plan as the `key=value` lines a workflow appends to its outputs. */
export function outputs(planned) {
  return [
    `journeys=${planned.journeys}`,
    `real-engine=${planned.realEngine}`,
    `release=${planned.release}`,
    `image=${planned.image}`,
  ].join('\n');
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const here = dirname(fileURLToPath(import.meta.url));
  const specs = readdirSync(here).filter((file) => file.endsWith('.spec.js')).sort();
  const planned = process.argv.includes('--all') ? everything() : plan(readFileSync(0, 'utf8').split('\n'), specs);
  console.log(outputs(planned));
}
