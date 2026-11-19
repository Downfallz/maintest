// What CI plays for a diff, and what it says flaked (ADR 0182), run in Node like the pages' own tests (ADR 0024).
// The plan is a list of what each job reads, kept by hand: these tests are where a wrong line in it shows.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { JOURNEY_JOBS, everything, gate, journeyJobs, outputs, plan } from './browser/plan.js';
import { flaked } from './browser/flakes.js';

const here = dirname(fileURLToPath(import.meta.url));
const specs = readdirSync(join(here, 'browser')).filter((file) => file.endsWith('.spec.js'));
const nothing = { journeys: 'none', realEngine: false, release: 'none', image: false, showcase: false, build: 'full' };

test('a change of Markdown alone calls for nothing', () => {
  assert.deepEqual(plan(['docs/adr/0182-x.md', 'table/README.md', 'AGENTS.md', 'infra/README.md'], specs), nothing);
});

test('a change nothing here reads calls for nothing', () => {
  assert.deepEqual(plan(['learning/src/downfall_learning/clone.py', '.claude/rules/host.md', 'scripts/iterate.sh'], specs), nothing);
});

test('a changed spec plays that spec alone', () => {
  assert.deepEqual(plan(['studio/browser/admin.ux.spec.js'], specs), { ...nothing, journeys: 'admin.ux.spec.js' });
});

test('a spec the release profiles play also plays them', () => {
  assert.deepEqual(plan(['studio/browser/battle-live.spec.js'], specs), { ...nothing, journeys: 'battle-live.spec.js', release: 'all' });
});

test('a real-engine journey plays the real engine and no mock journey', () => {
  assert.deepEqual(plan(['studio/browser/table.practice.js'], specs), { ...nothing, realEngine: true });
  assert.deepEqual(plan(['studio/browser/battle.loop.js'], specs), { ...nothing, realEngine: true, release: 'all' });
});

test('a fixture or the mock host plays everything', () => {
  for (const file of ['server.js', 'origin.js', 'dotnet.js', 'battle-help-fixture.js', 'playwright.config.js', 'package-lock.json', 'plan.js']) {
    assert.deepEqual(plan([`studio/browser/${file}`], specs), { ...nothing, journeys: 'all', realEngine: true, release: 'all' }, file);
  }
});

test('the battle view plays the battle specs, the real engine and the release profiles', () => {
  const planned = plan(['battle/src/scene.ts'], specs);
  assert.deepEqual(planned.journeys.split(' '), specs.filter((spec) => spec.startsWith('battle-')));
  assert.deepEqual({ ...planned, journeys: '' }, { ...nothing, journeys: '', realEngine: true, release: 'all', image: true });
});

test('a table page plays every journey but the studio', () => {
  const planned = plan(['table/lobby.js'], specs);
  assert.deepEqual(planned.journeys.split(' '), specs.filter((spec) => spec !== 'studio.spec.js'));
  assert.equal(planned.realEngine && planned.image, true);
  assert.equal(planned.release, 'all');
});

test('a studio page plays the studio and the identity, and builds nothing', () => {
  assert.deepEqual(plan(['studio/catalogue.js'], specs), { ...nothing, journeys: specs.filter((spec) => spec === 'branding.spec.js' || spec === 'studio.spec.js').join(' ') });
});

test('the film strip script plays the two specs that open the bench through it', () => {
  assert.equal(plan(['scripts/battle/filmstrip.mjs'], specs).journeys, 'battle-bench.spec.js battle-fx.spec.js battle-motion.spec.js');
});

test("the pages' own tests play no journey", () => {
  assert.deepEqual(plan(['studio/catalogue.test.js'], specs), nothing);
  assert.deepEqual(plan(['table/presentation.test.js'], specs), { ...nothing, image: true });
  assert.deepEqual(plan(['table/battle-recap.fixture.json'], specs), { ...nothing, image: true });
});

test('the host plays the real engine and builds the image, without the mock journeys', () => {
  assert.deepEqual(plan(['src/DownfallArena.Cli/Table/TableApi.cs'], specs), { ...nothing, realEngine: true, release: 'all', image: true });
  assert.deepEqual(plan(['src/DownfallArena.Cli/Table/TableApi.cs'], specs, { pullRequest: true }), { ...nothing, realEngine: true, image: true });
});

test('a console command no page reaches builds the image and plays nothing', () => {
  assert.deepEqual(plan(['src/DownfallArena.Cli/ConsoleAgent.cs'], specs), { ...nothing, image: true });
});

test('content plays everything', () => {
  assert.deepEqual(plan(['data/Spells/pummel.json'], specs), everything());
});

test('a tutorial file plays the tutorial journey, which reads its lesson, and the real engine, which plays it to its end', () => {
  assert.deepEqual(plan(['scenarios/tutorials/your-first-combat.json'], specs), { ...nothing, journeys: 'battle-tutorial.spec.js', realEngine: true, image: true });
  assert.deepEqual(plan(['scenarios/greedy-1v1-round-3.json'], specs), { ...nothing, image: true });
});

test('the infrastructure builds the image and plays nothing', () => {
  assert.deepEqual(plan(['infra/main.bicep'], specs), { ...nothing, image: true });
  assert.deepEqual(plan(['Dockerfile'], specs), { ...nothing, image: true });
  assert.deepEqual(plan(['learning/weights/greedy.json'], specs), { ...nothing, image: true });
  assert.deepEqual(plan(['scenarios/greedy-1v1-round-3.json'], specs), { ...nothing, image: true });
});

test('the workflows that play the journeys play everything', () => {
  assert.deepEqual(plan(['.github/workflows/studio-browser.yml'], specs), { ...nothing, journeys: 'all', realEngine: true, release: 'all' });
});

// The showcase is deployed for what its build reads (ADR 0185), and only for that.
test('the showcase is deployed for its pages, the catalogue and the rulebook it renders and the art it copies', () => {
  for (const path of [
    'showcase/page.mjs',
    'showcase/_headers',
    'data/Tiers/warped.json',
    'data/presentation/mutation-art.json',
    'docs/art/branding.css',
    'battle/src/cue-art.js',
    'table/battle-evolution-art.js',
    'table/site-origins.js',
    'table/rules.json',
    'table/rules.fr.json',
    'table/rules.js',
    'table/battle-art/horizon/breach.webp',
    'table/fonts/OFL.txt',
    '.github/workflows/showcase.yml',
  ]) {
    assert.equal(plan([path], specs).showcase, true, path);
  }
});

test('the showcase is not deployed for its readme, a page test, the balance, the book\'s sources or the rest of the table', () => {
  for (const path of ['showcase/README.md', 'showcase/showcase.test.js', 'table/battle-evolution-art.test.js', 'data/balance/knobs.json', 'data/TalentTrees/a.json', 'table/lobby.js', 'table/rules.css', 'table/rules.html', 'rules/03-the-round.md', 'src/DownfallArena.Cli/Table/TableApi.cs']) {
    assert.equal(plan([path], specs).showcase, false, path);
  }
});

test('several paths add up, and blank lines are not paths', () => {
  const planned = plan(['studio/browser/studio.spec.js', '', ' studio/browser/lobby-home.spec.js ', 'infra/main.bicep'], specs);
  assert.deepEqual(planned, { ...nothing, journeys: specs.filter((spec) => spec === 'lobby-home.spec.js' || spec === 'studio.spec.js').join(' '), image: true });
});

// A pull request plays the release profiles' WebKit alone; main and the nightly play the three (ADR 0190).
test("a pull request plays the release profiles' WebKit alone", () => {
  for (const path of ['battle/src/scene.ts', 'table/lobby.js', 'table/battle-scene.js', 'studio/browser/battle-live.spec.js', 'studio/browser/battle.loop.js', '.github/workflows/ci.yml']) {
    assert.equal(plan([path], specs, { pullRequest: true }).release, 'webkit', path);
    assert.equal(plan([path], specs).release, 'all', path);
  }
});

// A change to how the release job is defined is played in every profile before it reaches main.
test('a pull request that changes the release job itself plays all three profiles', () => {
  for (const path of ['.github/workflows/studio-browser.yml', 'studio/browser/battle.release.config.js', 'studio/browser/plan.js']) {
    assert.equal(plan([path, 'battle/src/scene.ts'], specs, { pullRequest: true }).release, 'all', path);
  }
});

test('a pull request that calls for no release profile plays none, and the rest of its plan is the same', () => {
  assert.deepEqual(plan(['docs/adr/0182-x.md'], specs, { pullRequest: true }), { ...nothing, build: 'light' });
  const planned = plan(['data/Spells/pummel.json'], specs, { pullRequest: true });
  assert.deepEqual(planned, { ...everything(), release: 'none' });
});

// The engine and the content are judged on a pull request by the real-engine journeys; the release profiles are
// called for by what WebKit renders and by the release job itself, and on main by all of it (M4).
test('a pull request plays no release profile for the engine, the content or what builds them; main plays the three', () => {
  for (const path of ['src/DownfallArena.Cli/Table/TableApi.cs', 'src/DownfallArena.Domain/Match.cs', 'data/Spells/pummel.json', 'data/balance/knobs.json', 'tools/DownfallArena.DataBuilder/Program.cs', 'global.json', 'Directory.Packages.props']) {
    assert.equal(plan([path], specs, { pullRequest: true }).release, 'none', path);
    assert.equal(plan([path], specs).release, 'all', path);
  }
  // With a path that does call for them, the pull request still plays WebKit.
  assert.equal(plan(['src/DownfallArena.Domain/Match.cs', 'table/battle-scene.js'], specs, { pullRequest: true }).release, 'webkit');
});

// A Markdown-only pull request builds, runs Architecture.Tests (the instruction files, the ADRs, the generated
// indexes) and the pages' own tests (the prompts, the rulebook), and nothing else (question 6).
test('a pull request of Markdown alone runs the light build', () => {
  for (const paths of [['docs/adr/0182-x.md'], ['AGENTS.md', 'table/README.md', '.claude/skills/ship/SKILL.md'], ['docs/tabletop/plan.md', 'docs/README.md'], ['learning/README.md', 'infra/README.md']]) {
    assert.equal(plan(paths, specs, { pullRequest: true }).build, 'light', paths.join(' '));
  }
});

test('the full build runs for a path that is not Markdown, for Markdown the learning tests read, on main, and for an empty diff', () => {
  assert.equal(plan(['docs/adr/0182-x.md', 'studio/catalogue.js'], specs, { pullRequest: true }).build, 'full');
  assert.equal(plan(['docs/adr/0182-x.md', 'studio/catalogue.test.js'], specs, { pullRequest: true }).build, 'full');
  // test_knobs.py reads data/README.md's table of effect kinds.
  assert.equal(plan(['data/README.md'], specs, { pullRequest: true }).build, 'full');
  // battle-craft.test.js checks the book's engine names against battle/node_modules, which the light build does not install.
  assert.equal(plan(['docs/art/battle-craft.md'], specs, { pullRequest: true }).build, 'full');
  assert.equal(plan(['docs/art/battle-craft-plan.md'], specs, { pullRequest: true }).build, 'light');
  assert.equal(plan(['docs/adr/0182-x.md'], specs).build, 'full');
  assert.equal(plan([], specs, { pullRequest: true }).build, 'full');
  assert.equal(everything().build, 'full');
});

// A journeys job the plan leaves empty used to start, set up, and play nothing (M3's note): the plan names the
// jobs that have something to play.
test('the journey jobs are the ones the planned specs play in', () => {
  const names = JOURNEY_JOBS.map((job) => job.name);
  const replays = 'desktop replays';
  assert.deepEqual(journeyJobs('all'), names);
  assert.deepEqual(journeyJobs('none'), []);
  assert.deepEqual(journeyJobs('admin.ux.spec.js studio.spec.js'), names.filter((name) => name !== replays));
  assert.deepEqual(journeyJobs('table.ux.spec.js'), names.filter((name) => name !== replays));
  assert.deepEqual(journeyJobs('battle-live.spec.js'), ['small-phone', 'phone', replays]);
  assert.deepEqual(journeyJobs('battle-live.spec.js battle-tutorial.spec.js'), names);
});

test('the plan is written as the outputs the workflow reads', () => {
  assert.equal(
    outputs(everything()),
    `journeys=all\njourney-jobs=${JSON.stringify(JOURNEY_JOBS.map((job) => job.name))}\nreal-engine=true\nrelease=all\nimage=true\nshowcase=true\nbuild=full`,
  );
  assert.match(outputs(nothing), /^journey-jobs=\[\]$/m);
});

test('a test that passed only on its retry is reported, and no other', () => {
  const report = {
    suites: [{
      title: 'table.ux.spec.js',
      file: 'table.ux.spec.js',
      specs: [
        { title: 'opens', file: 'table.ux.spec.js', tests: [{ projectName: 'phone', status: 'expected' }, { projectName: 'desktop', status: 'flaky' }] },
      ],
      suites: [{
        title: 'lobby',
        specs: [
          { title: 'lists', file: 'table.ux.spec.js', tests: [{ projectName: 'phone', status: 'flaky' }, { projectName: 'tablet', status: 'unexpected' }] },
          { title: 'skips', file: 'table.ux.spec.js', tests: [{ projectName: 'phone', status: 'skipped' }] },
        ],
      }],
    }],
  };

  assert.deepEqual(flaked(report), ['table.ux.spec.js › lobby › lists [phone]', 'table.ux.spec.js › opens [desktop]']);
  assert.deepEqual(flaked({}), []);
});

// The local gate (scripts/verify.sh) always builds and tests the engine and the pages; the rest is the diff's.
test('the local gate adds nothing for markdown, a page test or a hook alone', () => {
  const none = { battle: false, learning: false, content: false };
  assert.deepEqual(gate([]), none);
  assert.deepEqual(gate(['docs/adr/0182-x.md', 'learning/README.md', 'battle/README.md', 'table/lobby.test.js', 'table/battle-recap.fixture.json', '.claude/hooks/stop.sh']), none);
});

test('the local gate rebuilds the battle bundle for a change to the battle view', () => {
  assert.deepEqual(gate(['battle/src/scene.ts']), { battle: true, learning: false, content: false });
});

// battle/src imports the table's own modules (battle/src/target-preview.ts reads table/battle-targets.js).
test('the local gate rebuilds the battle bundle for a table module the bundle may import', () => {
  for (const path of ['table/battle-targets.js', 'table/account.js']) {
    assert.deepEqual(gate([path]), { battle: true, learning: false, content: false }, path);
  }
});

test('the local gate runs the learning project for what its lint and tests read', () => {
  for (const path of ['learning/src/downfall_learning/clone.py', 'scripts/sweep-weight.py', 'scripts/iterate.sh', 'viewer/samples/evaluation.json', 'studio/value.fixture.json', 'data/README.md', '.github/workflows/studio-browser.yml']) {
    assert.deepEqual(gate([path]), { battle: false, learning: true, content: false }, path);
  }
});

test('the local gate builds the content for the content and for what the builder is built from', () => {
  assert.deepEqual(gate(['data/spells/pummel.json']), { battle: false, learning: true, content: true });
  for (const path of ['tools/DownfallArena.DataBuilder/Program.cs', 'src/DownfallArena.Domain/Match.cs', 'Directory.Packages.props']) {
    assert.deepEqual(gate([path]), { battle: false, learning: false, content: true }, path);
  }
});

// A job that is not among the gate's needs can fail while the one required check stays green.
test('every job of the CI that runs before the gate is one the gate waits for', () => {
  const workflow = readFileSync(join(here, '..', '.github', 'workflows', 'ci.yml'), 'utf8');
  const jobs = [...workflow.slice(workflow.indexOf('\njobs:')).matchAll(/^ {2}([a-z][a-z-]*):$/gm)].map((match) => match[1]);
  const needs = /^ {2}gate:\n(?:.*\n)*? {4}needs: \[([^\]]*)\]/m.exec(workflow);
  assert.ok(needs, 'the gate job lists what it needs on one line');

  const after = ['gate', 'deploy', 'showcase', 'main-health'];
  assert.deepEqual(needs[1].split(',').map((job) => job.trim()), jobs.filter((job) => !after.includes(job)));
});

// The release job is one job per profile: the matrix names the profiles of battle.release.config.js, and a pull
// request's list is the profiles that run on WebKit.
test('the release matrix plays every release profile, and a pull request the WebKit ones alone', () => {
  const config = readFileSync(join(here, 'browser', 'battle.release.config.js'), 'utf8');
  const projects = [...config.matchAll(/\{ name: '([a-z-]+)', use: \{([^\n]*)\} \}/g)].map((match) => ({ name: match[1], webkit: /browserName: 'webkit'/.test(match[2]) }));
  assert.ok(projects.length >= 3, 'the release profiles are read from their configuration');

  const workflow = readFileSync(join(here, '..', '.github', 'workflows', 'studio-browser.yml'), 'utf8');
  const matrix = /profile: \$\{\{ fromJSON\(inputs\.release == 'webkit' && '(\[[^\]]*\])' \|\| '(\[[^\]]*\])'\) \}\}/.exec(workflow);
  assert.ok(matrix, 'the release job chooses its profiles from the release input on one line');

  assert.deepEqual(JSON.parse(matrix[1]), projects.filter((project) => project.webkit).map((project) => project.name));
  assert.deepEqual(JSON.parse(matrix[2]), projects.map((project) => project.name));
});

test('CI asks for the narrower plan on a pull request alone', () => {
  const workflow = readFileSync(join(here, '..', '.github', 'workflows', 'ci.yml'), 'utf8');
  assert.match(workflow, /if \[\[ "\$EVENT" == pull_request \]\]; then narrow=\(--pull-request\); fi/);
  assert.match(workflow, /node studio\/browser\/plan\.js "\$\{narrow\[@\]\}" < "\$RUNNER_TEMP\/changed\.txt"/);
});

// The wait evened out (docs/engineering/ci-minutes-plan.md, M3).
const browserWorkflow = () => readFileSync(join(here, '..', '.github', 'workflows', 'studio-browser.yml'), 'utf8');
const ciWorkflow = () => readFileSync(join(here, '..', '.github', 'workflows', 'ci.yml'), 'utf8');

// A job's block: from its key under `jobs:` to the next job's key.
function jobBlock(workflow, job) {
  const start = workflow.search(new RegExp(`^ {2}${job}:$`, 'm'));
  assert.ok(start >= 0, `the job ${job} is in the workflow`);
  const next = workflow.slice(start + 1).search(/^ {2}[a-z][a-z0-9_-]*:$/m);
  return next < 0 ? workflow.slice(start) : workflow.slice(start, start + 1 + next);
}

// Desktop and tablet were two halves of 13 and 21 minutes: a shard splits by test count, and three specs hold a
// third of the time. Those play in a job of their own, the rest in two halves without them. The plan holds the
// jobs (M4), so that it can leave out a job with nothing to play.
test('the heaviest desktop specs play in a job of their own, and the rest in two halves, each test once', () => {
  assert.deepEqual(JOURNEY_JOBS.map((job) => job.name), ['small-phone', 'phone', 'desktop replays', 'desktop and tablet 1/2', 'desktop and tablet 2/2']);
  assert.deepEqual(JOURNEY_JOBS.slice(0, 2).map((job) => job.arguments), [['--project=small-phone'], ['--project=phone']]);

  const [replays, ...halves] = JOURNEY_JOBS.slice(2);
  assert.equal(replays.arguments.length, 2);
  assert.equal(replays.arguments[0], '--project=desktop', 'the replays job plays the desktop project');
  const heavy = /^--grep=(\S+)$/.exec(replays.arguments[1]);
  assert.ok(heavy, 'the replays job is filtered by its specs');
  halves.forEach((half, index) => {
    // The same pattern inverted, so a test plays in the replays job or in a half, never in both or neither.
    assert.deepEqual(half.arguments, ['--project=desktop', '--project=tablet', `--grep-invert=${heavy[1]}`, `--shard=${index + 1}/2`], half.name);
    assert.ok(half.name.endsWith(`${index + 1}/2`), `${half.name} is named for its shard`);
  });

  // Every spec the pattern names is one that exists, so a renamed spec does not leave the job empty.
  const named = /^battle-\((.*)\)\\\.spec\\\.js$/.exec(heavy[1]);
  assert.ok(named, 'the pattern names battle specs by their file');
  for (const name of named[1].split('|')) assert.ok(specs.includes(`battle-${name}.spec.js`), name);
  for (const spec of specs) {
    const isHeavy = named[1].split('|').some((name) => spec === `battle-${name}.spec.js`);
    assert.equal(new RegExp(heavy[1]).test(spec), isHeavy, spec);
    // The jobs the plan chooses for a spec are the ones whose arguments play it.
    const jobs = journeyJobs(spec);
    assert.equal(jobs.includes(replays.name), isHeavy, spec);
    for (const half of halves) assert.equal(jobs.includes(half.name), !isHeavy, spec);
  }
});

test("the journeys matrix is the plan's journey jobs, each playing the arguments the plan gives it", () => {
  const workflow = browserWorkflow();
  const block = jobBlock(workflow, 'journeys');
  const matrix = /^ {8}name: \$\{\{ fromJSON\(inputs\.journey-jobs == 'all' && '(\[[^\]]*\])' \|\| inputs\.journey-jobs\) \}\}$/m.exec(block);
  assert.ok(matrix, 'the journeys matrix names its jobs from the journey-jobs input on one line');
  assert.deepEqual(JSON.parse(matrix[1]), JOURNEY_JOBS.map((job) => job.name));
  assert.match(block, /^ {4}if: inputs\.journeys != 'none' && inputs\.journey-jobs != '\[\]'$/m);
  assert.match(block, /mapfile -t projects < <\(node plan\.js --journey-job "\$NAME"\)/);
  assert.match(block, /test \$SPECS "\$\{projects\[@\]\}" --retries=1 --pass-with-no-tests/);
  // Declared for a call and for a run by hand, each with `all` by default, which the nightly matrix plays.
  assert.equal([...workflow.matchAll(/^ {6}journey-jobs:\n(?: {8}.*\n)*? {8}default: all$/gm)].length, 2);
  assert.match(ciWorkflow(), /^ {6}journey-jobs: \$\{\{ needs\.changes\.outputs\.journey-jobs \}\}$/m);
  assert.match(ciWorkflow(), /^ {6}journey-jobs: \$\{\{ steps\.plan\.outputs\.journey-jobs \}\}$/m);
});

test('the plan prints the arguments of one journey job, and refuses a job it does not hold', () => {
  const script = join(here, 'browser', 'plan.js');
  for (const job of JOURNEY_JOBS) {
    const printed = spawnSync(process.execPath, [script, '--journey-job', job.name], { encoding: 'utf8' });
    assert.equal(printed.status, 0, job.name);
    assert.deepEqual(printed.stdout.trimEnd().split('\n'), job.arguments, job.name);
  }
  const unknown = spawnSync(process.execPath, [script, '--journey-job', 'tablet'], { encoding: 'utf8' });
  assert.notEqual(unknown.status, 0);
  assert.equal(unknown.stdout, '');
});

// A Markdown-only pull request runs the light build: the build, Architecture.Tests and the pages' own tests (M4).
test("build-and-test runs the light build when the plan says so: the build, Architecture.Tests and the pages' tests", () => {
  const workflow = ciWorkflow();
  assert.match(workflow, /^ {6}build: \$\{\{ steps\.plan\.outputs\.build \}\}$/m);
  const block = jobBlock(workflow, 'build-and-test');
  assert.match(block, /^ {4}needs: changes$/m);
  assert.match(block, /^ {6}LIGHT_BUILD: \$\{\{ needs\.changes\.outputs\.build == 'light' \}\}$/m);

  const steps = [...block.matchAll(/^ {6}- name: (.*)\n((?: {8}.*\n)*)/gm)].map((match) => ({ name: match[1], body: match[2] }));
  const skipped = (step) => /^ {8}if: .*env\.LIGHT_BUILD != 'true'/m.test(step.body);
  const lightOnly = (step) => /^ {8}if: .*env\.LIGHT_BUILD == 'true'/m.test(step.body);
  assert.deepEqual(steps.filter((step) => !skipped(step)).map((step) => step.name), [
    'Checkout',
    'Set up .NET (version from global.json)',
    'Restore packages and tools',
    'Test the static pages',
    'Build (warnings are errors)',
    'Test the instruction files and the layering',
    'Upload test results and coverage',
    "Show the storage emulator's log",
    'The learning project passed',
  ]);
  assert.deepEqual(steps.filter(lightOnly).map((step) => step.name), ['Test the instruction files and the layering']);
  const architecture = steps.find(lightOnly);
  assert.match(architecture.body, /dotnet test --no-build --configuration Release --project tests\/DownfallArena\.Architecture\.Tests$/m);
  // Every Sonar step is off in the light build: an analysis without the tests' coverage would read as none.
  for (const step of steps.filter((step) => step.name.includes('SonarCloud'))) assert.ok(skipped(step), step.name);
});

// Each browser job downloaded its engine, nothing cached: the install took 18 to 60 seconds a job. The system
// packages are still installed every time (`--with-deps`), since a runner's image does not keep them.
test("every job that installs a browser restores Playwright's browsers first, keyed by its version and the engine", () => {
  const workflow = browserWorkflow();
  const jobs = [...workflow.slice(workflow.indexOf('\njobs:')).matchAll(/^ {2}([a-z][a-z0-9_-]*):$/gm)].map((match) => match[1])
    .filter((job) => jobBlock(workflow, job).includes('playwright/cli.js install'));
  assert.deepEqual(jobs, ['journeys', 'real-engine', 'battle-release']);
  for (const job of jobs) {
    const block = jobBlock(workflow, job);
    const restore = block.search(/uses: actions\/cache\/restore@[0-9a-f]{40} # v\d/);
    const install = block.indexOf('playwright/cli.js install --with-deps "$ENGINE"');
    const save = block.search(/uses: actions\/cache\/save@[0-9a-f]{40} # v\d/);
    assert.ok(restore >= 0, `${job} restores the browsers`);
    assert.ok(install > restore, `${job} installs after the restore, so a cached engine is not downloaded again`);
    // Saved before any build or spec runs code, so the cache holds what Playwright downloaded and nothing else.
    assert.ok(save > install && save < block.indexOf('npm --prefix battle'), `${job} saves straight after the install`);
    assert.match(block, /if: steps\.browsers\.outputs\.cache-hit != 'true'/, job);
    assert.equal([...block.matchAll(/path: ~\/\.cache\/ms-playwright/g)].length, 2, job);
    assert.equal([...block.matchAll(/key: \$\{\{ steps\.playwright\.outputs\.key \}\}/g)].length, 2, job);
    assert.match(block, /require\('\.\/node_modules\/playwright\/package\.json'\)\.version/, job);
    assert.match(block, /key=playwright-\$ImageOS-\$version-\$ENGINE/, job);
    // One ENGINE a job, so the key and the install cannot name different engines.
    assert.equal([...block.matchAll(/^ +ENGINE: /gm)].length, 1, `${job} sets ENGINE once`);
    assert.match(block, /^ {4}env:\n {6}ENGINE: /m, `${job} sets ENGINE for the whole job`);
  }
});

// The learning project's own job paid a minute to run ruff, then build-and-test ran pytest again for Sonar.
test('the learning project is linted and tested once, in build-and-test, and its failure fails the job', () => {
  const workflow = ciWorkflow();
  assert.doesNotMatch(workflow, /^ {2}learning:$/m);
  const block = jobBlock(workflow, 'build-and-test');
  const check = block.indexOf('ruff" check . ../scripts');
  const format = block.indexOf('ruff" format --check . ../scripts');
  const pytest = block.indexOf('bin/pytest"');
  assert.ok(check >= 0 && format > check && pytest > format, 'ruff check, ruff format, then pytest');
  assert.equal([...block.matchAll(/bin\/pytest"/g)].length, 1, 'pytest runs once');
  // First after the checkout, so no failing .NET step skips it.
  const steps = [...block.matchAll(/^ {6}- name: (.*)$/gm)].map((match) => match[1]);
  assert.deepEqual(steps.slice(0, 2), ['Checkout', 'Lint and test the learning project']);
  assert.match(block, /- name: Lint and test the learning project\n {8}id: learning\n {8}continue-on-error: true\n/);
  // Each command runs whatever the one before it said: a lint error hides neither the tests nor the coverage.
  for (const command of ['ruff" check . ../scripts', 'ruff" format --check . ../scripts', '-q']) {
    assert.match(block, new RegExp(`${command.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')} \\|\\| status=1$`, 'm'), command);
  }
  assert.match(block, /^ {10}exit "\$status"$/m);
  // It goes on when it fails, so the .NET steps still run; the job's last step is what fails it.
  const last = block.lastIndexOf('      - name: ');
  assert.match(block.slice(last), /^ {6}- name: .*\n {8}if: \$\{\{ !cancelled\(\) && steps\.learning\.outcome == 'failure' \}\}\n {8}run: \|\n(?: {10}.*\n)*? {10}exit 1\n/);
});

// A rename is a deletion and an addition: git's rename detection would print the new name alone, and a code file
// renamed to Markdown would read as a Markdown-only diff and get the light build (M4's security review).
test('the changed paths of a pull request name both sides of a rename', () => {
  const repository = mkdtempSync(join(tmpdir(), 'changed-paths-'));
  try {
    const git = (...args) => execFileSync('git', ['-C', repository, '-c', 'user.name=t', '-c', 'user.email=t@example.invalid', ...args], { encoding: 'utf8' });
    git('init', '-q');
    mkdirSync(join(repository, 'src'));
    writeFileSync(join(repository, 'src', 'Table.cs'), 'class Table {}\n'.repeat(20));
    git('add', '.');
    git('commit', '-q', '-m', 'base');
    git('mv', 'src/Table.cs', 'src/Table.md');
    git('commit', '-q', '-m', 'rename');

    const script = join(here, '..', '.github', 'scripts', 'changed-paths.sh');
    const printed = spawnSync('bash', [script], { cwd: repository, encoding: 'utf8', env: { ...process.env, EVENT: 'pull_request' } });
    assert.equal(printed.status, 0, printed.stderr);
    const paths = printed.stdout.split('\n').filter(Boolean).sort();
    assert.deepEqual(paths, ['src/Table.cs', 'src/Table.md']);
    assert.equal(plan(paths, specs, { pullRequest: true }).build, 'full');
  } finally {
    rmSync(repository, { recursive: true, force: true });
  }
});

test('every diff the changed paths are read from turns rename detection off', () => {
  const script = readFileSync(join(here, '..', '.github', 'scripts', 'changed-paths.sh'), 'utf8');
  const diffs = [...script.matchAll(/git diff [^\n]*/g)].map((match) => match[0]);
  assert.equal(diffs.length, 2);
  for (const diff of diffs) assert.match(diff, /--no-renames/, diff);
});
