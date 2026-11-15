// What CI plays for a diff, and what it says flaked (ADR 0182), run in Node like the pages' own tests (ADR 0024).
// The plan is a list of what each job reads, kept by hand: these tests are where a wrong line in it shows.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { everything, outputs, plan } from './browser/plan.js';
import { flaked } from './browser/flakes.js';

const here = dirname(fileURLToPath(import.meta.url));
const specs = readdirSync(join(here, 'browser')).filter((file) => file.endsWith('.spec.js'));
const nothing = { journeys: 'none', realEngine: false, release: false, image: false };

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
  assert.deepEqual(plan(['studio/browser/battle-live.spec.js'], specs), { ...nothing, journeys: 'battle-live.spec.js', release: true });
});

test('a real-engine journey plays the real engine and no mock journey', () => {
  assert.deepEqual(plan(['studio/browser/table.practice.js'], specs), { ...nothing, realEngine: true });
  assert.deepEqual(plan(['studio/browser/battle.loop.js'], specs), { ...nothing, realEngine: true, release: true });
});

test('a fixture or the mock host plays everything', () => {
  for (const file of ['server.js', 'battle-help-fixture.js', 'playwright.config.js', 'package-lock.json', 'plan.js']) {
    assert.deepEqual(plan([`studio/browser/${file}`], specs), { journeys: 'all', realEngine: true, release: true, image: false }, file);
  }
});

test('the battle view plays the battle specs, the real engine and the release profiles', () => {
  const planned = plan(['battle/src/scene.ts'], specs);
  assert.deepEqual(planned.journeys.split(' '), specs.filter((spec) => spec.startsWith('battle-')));
  assert.deepEqual({ ...planned, journeys: '' }, { journeys: '', realEngine: true, release: true, image: true });
});

test('a table page plays every journey but the studio', () => {
  const planned = plan(['table/lobby.js'], specs);
  assert.deepEqual(planned.journeys.split(' '), specs.filter((spec) => spec !== 'studio.spec.js'));
  assert.equal(planned.realEngine && planned.release && planned.image, true);
});

test('a studio page plays the studio and the identity, and builds nothing', () => {
  assert.deepEqual(plan(['studio/catalogue.js'], specs), { ...nothing, journeys: specs.filter((spec) => spec === 'branding.spec.js' || spec === 'studio.spec.js').join(' ') });
});

test("the pages' own tests play no journey", () => {
  assert.deepEqual(plan(['studio/catalogue.test.js'], specs), nothing);
  assert.deepEqual(plan(['table/presentation.test.js'], specs), { ...nothing, image: true });
});

test('the host plays the real engine and builds the image, without the mock journeys', () => {
  assert.deepEqual(plan(['src/DownfallArena.Cli/Table/TableApi.cs'], specs), { journeys: 'none', realEngine: true, release: true, image: true });
});

test('a console command no page reaches builds the image and plays nothing', () => {
  assert.deepEqual(plan(['src/DownfallArena.Cli/ConsoleAgent.cs'], specs), { ...nothing, image: true });
});

test('content plays everything', () => {
  assert.deepEqual(plan(['data/spells/pummel.json'], specs), everything());
});

test('the infrastructure builds the image and plays nothing', () => {
  assert.deepEqual(plan(['infra/main.bicep'], specs), { ...nothing, image: true });
  assert.deepEqual(plan(['Dockerfile'], specs), { ...nothing, image: true });
  assert.deepEqual(plan(['learning/weights/greedy.json'], specs), { ...nothing, image: true });
  assert.deepEqual(plan(['scenarios/greedy-1v1-round-3.json'], specs), { ...nothing, image: true });
});

test('the workflows that play the journeys play everything', () => {
  assert.deepEqual(plan(['.github/workflows/studio-browser.yml'], specs), { journeys: 'all', realEngine: true, release: true, image: false });
});

test('several paths add up, and blank lines are not paths', () => {
  const planned = plan(['studio/browser/studio.spec.js', '', ' studio/browser/lobby-home.spec.js ', 'infra/main.bicep'], specs);
  assert.deepEqual(planned, { ...nothing, journeys: specs.filter((spec) => spec === 'lobby-home.spec.js' || spec === 'studio.spec.js').join(' '), image: true });
});

test('the plan is written as the outputs the workflow reads', () => {
  assert.equal(outputs(everything()), 'journeys=all\nreal-engine=true\nrelease=true\nimage=true');
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

// A job that is not among the gate's needs can fail while the one required check stays green.
test('every job of the CI that runs before the gate is one the gate waits for', () => {
  const workflow = readFileSync(join(here, '..', '.github', 'workflows', 'ci.yml'), 'utf8');
  const jobs = [...workflow.slice(workflow.indexOf('\njobs:')).matchAll(/^ {2}([a-z][a-z-]*):$/gm)].map((match) => match[1]);
  const needs = /^ {2}gate:\n(?:.*\n)*? {4}needs: \[([^\]]*)\]/m.exec(workflow);
  assert.ok(needs, 'the gate job lists what it needs on one line');

  const after = ['gate', 'deploy', 'main-health'];
  assert.deepEqual(needs[1].split(',').map((job) => job.trim()), jobs.filter((job) => !after.includes(job)));
});
