// What CI plays for a diff, and what it says flaked (ADR 0182), run in Node like the pages' own tests (ADR 0024).
// The plan is a list of what each job reads, kept by hand: these tests are where a wrong line in it shows.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { everything, gate, outputs, plan } from './browser/plan.js';
import { flaked } from './browser/flakes.js';

const here = dirname(fileURLToPath(import.meta.url));
const specs = readdirSync(join(here, 'browser')).filter((file) => file.endsWith('.spec.js'));
const nothing = { journeys: 'none', realEngine: false, release: 'none', image: false, showcase: false };

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
  for (const file of ['server.js', 'battle-help-fixture.js', 'playwright.config.js', 'package-lock.json', 'plan.js']) {
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

test("the pages' own tests play no journey", () => {
  assert.deepEqual(plan(['studio/catalogue.test.js'], specs), nothing);
  assert.deepEqual(plan(['table/presentation.test.js'], specs), { ...nothing, image: true });
});

test('the host plays the real engine and builds the image, without the mock journeys', () => {
  assert.deepEqual(plan(['src/DownfallArena.Cli/Table/TableApi.cs'], specs), { ...nothing, realEngine: true, release: 'all', image: true });
});

test('a console command no page reaches builds the image and plays nothing', () => {
  assert.deepEqual(plan(['src/DownfallArena.Cli/ConsoleAgent.cs'], specs), { ...nothing, image: true });
});

test('content plays everything', () => {
  assert.deepEqual(plan(['data/Spells/pummel.json'], specs), everything());
});

test('a tutorial file plays the tutorial journey, which reads its lesson', () => {
  assert.deepEqual(plan(['scenarios/tutorials/your-first-combat.json'], specs), { ...nothing, journeys: 'battle-tutorial.spec.js', image: true });
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
test('the showcase is deployed for its pages, the catalogue it renders and the art it copies', () => {
  for (const path of [
    'showcase/page.mjs',
    'showcase/_headers',
    'data/Tiers/warped.json',
    'data/presentation/mutation-art.json',
    'docs/art/branding.css',
    'battle/src/cue-art.js',
    'table/battle-evolution-art.js',
    'table/site-origins.js',
    'table/battle-art/horizon/breach.webp',
    'table/fonts/OFL.txt',
    '.github/workflows/showcase.yml',
  ]) {
    assert.equal(plan([path], specs).showcase, true, path);
  }
});

test('the showcase is not deployed for its readme, a page test, the balance or the rest of the table', () => {
  for (const path of ['showcase/README.md', 'showcase/showcase.test.js', 'table/battle-evolution-art.test.js', 'data/balance/knobs.json', 'data/TalentTrees/a.json', 'table/lobby.js', 'src/DownfallArena.Cli/Table/TableApi.cs']) {
    assert.equal(plan([path], specs).showcase, false, path);
  }
});

test('several paths add up, and blank lines are not paths', () => {
  const planned = plan(['studio/browser/studio.spec.js', '', ' studio/browser/lobby-home.spec.js ', 'infra/main.bicep'], specs);
  assert.deepEqual(planned, { ...nothing, journeys: specs.filter((spec) => spec === 'lobby-home.spec.js' || spec === 'studio.spec.js').join(' '), image: true });
});

// A pull request plays the release profiles' WebKit alone; main and the nightly play the three (ADR 0190).
test("a pull request plays the release profiles' WebKit alone", () => {
  for (const path of ['battle/src/scene.ts', 'table/lobby.js', 'studio/browser/battle-live.spec.js', 'src/DownfallArena.Cli/Table/TableApi.cs', '.github/workflows/ci.yml']) {
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
  assert.deepEqual(plan(['docs/adr/0182-x.md'], specs, { pullRequest: true }), nothing);
  const planned = plan(['data/Spells/pummel.json'], specs, { pullRequest: true });
  assert.deepEqual(planned, { ...everything(), release: 'webkit' });
});

test('the plan is written as the outputs the workflow reads', () => {
  assert.equal(outputs(everything()), 'journeys=all\nreal-engine=true\nrelease=all\nimage=true\nshowcase=true');
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
  assert.deepEqual(gate(['docs/adr/0182-x.md', 'learning/README.md', 'battle/README.md', 'table/lobby.test.js', '.claude/hooks/stop.sh']), none);
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
// third of the time. Those play in a job of their own, the rest in two halves without them.
test('the heaviest desktop specs play in a job of their own, and the rest in two halves, each test once', () => {
  const entries = [...jobBlock(browserWorkflow(), 'journeys').matchAll(/- name: (.*)\n {12}projects: '?([^'\n]*)'?$/gm)]
    .map((match) => ({ name: match[1], projects: match[2] }));
  assert.deepEqual(entries.map((entry) => entry.name), ['small-phone', 'phone', 'desktop replays', 'desktop and tablet 1/2', 'desktop and tablet 2/2']);
  assert.deepEqual(entries.slice(0, 2).map((entry) => entry.projects), ['--project=small-phone', '--project=phone']);

  const [replays, ...halves] = entries.slice(2);
  const heavy = /^--project=desktop --grep=(\S+)$/.exec(replays.projects);
  assert.ok(heavy, 'the replays job plays the desktop project, filtered by its specs');
  halves.forEach((half, index) => {
    // The same pattern inverted, so a test plays in the replays job or in a half, never in both or neither.
    assert.equal(half.projects, `--project=desktop --project=tablet --grep-invert=${heavy[1]} --shard=${index + 1}/2`, half.name);
    assert.ok(half.name.endsWith(`${index + 1}/2`), `${half.name} is named for its shard`);
  });

  // Every spec the pattern names is one that exists, so a renamed spec does not leave the job empty.
  const named = /^battle-\((.*)\)\\\.spec\\\.js$/.exec(heavy[1]);
  assert.ok(named, 'the pattern names battle specs by their file');
  for (const name of named[1].split('|')) assert.ok(specs.includes(`battle-${name}.spec.js`), name);
  for (const spec of specs) assert.equal(new RegExp(heavy[1]).test(spec), named[1].split('|').some((name) => spec === `battle-${name}.spec.js`), spec);
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
