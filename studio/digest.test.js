// The morning digest (scripts/digest/digest.mjs, written by the /digest skill): what it says, in what order, and
// that it fits a phone's screen.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  checksStatus,
  currentRuns,
  dayLabel,
  deploymentStatus,
  gather,
  mergedFromLog,
  noteworthy,
  originRepository,
  quiet,
  readGitHub,
  render,
  runStatus,
  shorten,
  waitingFor,
} from '../scripts/digest/digest.mjs';

const NOW = '2026-11-16T11:40:00Z';

test('a quiet day says so in one line and is not worth a push', () => {
  const facts = { now: NOW, runs: { main: { status: 'green' }, deploy: { status: 'green' }, nightly: { status: 'green' } } };
  assert.equal(noteworthy(facts), false);
  const text = render(facts);
  assert.match(text, /^Bilan du 16 nov\./);
  assert.match(text, /Rien de neuf/);
  assert.match(text, /main : vert · déploiement : vert · nightly : vert/);
});

test('the day is read in Montreal time, where the task runs', () => {
  assert.equal(dayLabel('2026-11-16T03:00:00Z'), '15 nov.');
  assert.equal(dayLabel('2026-11-16T11:40:00Z'), '16 nov.');
});

test('a merge is worth a push and lists its number and title', () => {
  const facts = { now: NOW, merged: [{ number: 601, title: 'Add the morning digest (W6)' }] };
  assert.equal(noteworthy(facts), true);
  assert.match(render(facts), /Fusionné \(1\)\n {2}#601 Add the morning digest \(W6\)/);
});

test('a red main names its health issue and links the run', () => {
  const facts = {
    now: NOW,
    runs: { main: { status: 'red', url: 'https://github.com/o/r/actions/runs/1' } },
    health: [{ number: 610, title: 'main is red', label: 'red-main' }],
  };
  assert.equal(noteworthy(facts), true);
  const text = render(facts);
  assert.match(text, /main : rouge \(#610\)/);
  assert.match(text, /https:\/\/github\.com\/o\/r\/actions\/runs\/1/);
});

test('a run nobody read is unknown, not green', () => {
  assert.match(render({ now: NOW }), /main : inconnu · déploiement : inconnu · nightly : inconnu/);
});

test('a pull request waits for the owner when it is green with no open thread, and a draft waits for nobody', () => {
  assert.equal(waitingFor({ checks: 'green', openThreads: 0 }), 'prête, à fusionner');
  assert.equal(waitingFor({ checks: 'red', openThreads: 2 }), 'rouge');
  assert.equal(waitingFor({ checks: 'green', openThreads: 2 }), '2 fils ouverts');
  assert.equal(waitingFor({ checks: 'running' }), 'checks en cours');
  assert.equal(waitingFor({ checks: 'green', openThreads: 0, draft: true }), null);
});

test('a green pull request whose threads were not read is green, not ready', () => {
  assert.equal(waitingFor({ checks: 'green' }), 'verte');
});

test('a waiting pull request is listed with what it waits for', () => {
  const text = render({ now: NOW, pullRequests: [{ number: 602, title: 'Fix the lobby', checks: 'green', openThreads: 0 }] });
  assert.match(text, /PR en attente \(1\)\n {2}#602 prête, à fusionner : Fix the lobby/);
});

test('a session waiting on an answer is worth a push', () => {
  const facts = { now: NOW, sessions: [{ title: 'Scenarios S8', needs: 'which seed file' }] };
  assert.equal(noteworthy(facts), true);
  assert.match(render(facts), /Sessions qui attendent une réponse \(1\)\n {2}Scenarios S8 : which seed file/);
});

test('loose ends are counted by label and the oldest come first', () => {
  const text = render({
    now: NOW,
    issues: [
      { number: 9, title: 'New', label: 'follow-up', createdAt: '2026-11-15' },
      { number: 3, title: 'Old', label: 'flaky', createdAt: '2026-11-01' },
      { number: 4, title: 'Ignored', label: 'bug', createdAt: '2026-10-01' },
    ],
  });
  assert.match(text, /Suivis ouverts : 1 follow-up, 1 flaky\. Les plus anciens :\n {2}#3 Old\n {2}#9 New/);
  assert.doesNotMatch(text, /Ignored/);
});

test('a long list is cut at five and says how many more', () => {
  const merged = Array.from({ length: 8 }, (_, index) => ({ number: index + 1, title: `PR ${index + 1}` }));
  const text = render({ now: NOW, merged });
  assert.match(text, /#5 PR 5\n {2}… et 3 de plus/);
  assert.doesNotMatch(text, /#6 PR 6/);
});

test('a title is one short line', () => {
  assert.equal(shorten('a\nb   c'), 'a b c');
  assert.equal(shorten('x'.repeat(80)).length, 60);
});

test('the merged pull requests are read from the squash subjects, and a commit with no number is left out', () => {
  const log = '2026-11-16T01:00:00Z\tAdd the reviewers (W5) (#600)\n2026-11-16T02:00:00Z\tFix a typo\n';
  assert.deepEqual(mergedFromLog(log), [{ number: 600, title: 'Add the reviewers (W5)' }]);
});

test('the window defaults to the 24 hours before the run, and a field the facts give is not read', async () => {
  let asked = null;
  const facts = await gather(
    { now: NOW, runs: { nightly: { status: 'green' } }, sessions: [{ title: 'S' }] },
    {
      readMerged: (since) => {
        asked = since.toISOString();
        return [];
      },
      readHub: async () => ({ runs: { main: { status: 'red' }, nightly: { status: 'red' } }, unread: ['PR'] }),
    },
  );
  assert.equal(asked, '2026-11-15T11:40:00.000Z');
  assert.equal(facts.since, '2026-11-15T11:40:00.000Z');
  assert.deepEqual(facts.runs, { main: { status: 'red' }, nightly: { status: 'green' } });
  assert.deepEqual(facts.sessions, [{ title: 'S' }]);
  assert.deepEqual(facts.unread, ['PR']);
  const given = await gather({ now: NOW, merged: [] }, { readMerged: () => assert.fail('git read'), readHub: async () => ({}) });
  assert.deepEqual(given.merged, []);
});

test('git that cannot read main is named, not a crash', async () => {
  const facts = await gather(
    { now: NOW },
    {
      readMerged: () => {
        throw new Error('unknown revision origin/main');
      },
      readHub: async () => ({ unread: ['PR'] }),
    },
  );
  assert.deepEqual(facts.unread, ['fusions', 'PR']);
  assert.match(render(facts), /Fusionné \(0\)/);
});

test('what could not be read is named last', () => {
  assert.match(render({ now: NOW, unread: ['PR', 'nightly'] }), /\n\nNon lu : PR, nightly$/);
});

test('a run is running until it completes, and green only on success', () => {
  assert.equal(runStatus({ status: 'pending' }), 'running');
  assert.equal(runStatus({ status: 'completed', conclusion: 'success' }), 'green');
  assert.equal(runStatus({ status: 'completed', conclusion: 'cancelled' }), 'red');
});

test('a deployment is running until its newest status settles, and red on a failure or an error', () => {
  assert.equal(deploymentStatus(undefined), 'running');
  assert.equal(deploymentStatus({ state: 'in_progress' }), 'running');
  assert.equal(deploymentStatus({ state: 'success' }), 'green');
  assert.equal(deploymentStatus({ state: 'failure' }), 'red');
  assert.equal(deploymentStatus({ state: 'error' }), 'red');
});

test('a pull request with no check run has no checks to report', () => {
  assert.equal(checksStatus([]), undefined);
  assert.equal(checksStatus([{ status: 'completed', conclusion: 'success' }, { status: 'completed', conclusion: 'skipped' }]), 'green');
  assert.equal(checksStatus([{ status: 'completed', conclusion: 'failure' }]), 'red');
  assert.equal(checksStatus([{ status: 'completed', conclusion: 'success' }, { status: 'queued', conclusion: null }]), 'running');
});

test('a superseded run of the same workflow and trigger does not colour a pull request', () => {
  const runs = [
    { workflow_id: 1, event: 'pull_request', status: 'completed', conclusion: 'success' },
    { workflow_id: 1, event: 'pull_request', status: 'completed', conclusion: 'failure' },
    { workflow_id: 2, event: 'pull_request', status: 'in_progress', conclusion: null },
  ];
  assert.deepEqual(currentRuns(runs), [runs[0], runs[2]]);
  assert.equal(checksStatus(currentRuns(runs.slice(0, 2))), 'green');
});

test('the repository is the origin on GitHub, and nothing else', () => {
  assert.equal(originRepository('https://github.com/Downfallz/maintest.git\n'), 'Downfallz/maintest');
  assert.equal(originRepository('git@github.com:Downfallz/maintest'), 'Downfallz/maintest');
  assert.throws(() => originRepository('https://example.com/a/b'));
});

const RUNS = '/repos/o/r/actions/workflows/ci.yml/runs?branch=main&event=push&per_page=1';
const DEPLOYMENTS = '/repos/o/r/deployments?environment=azure&per_page=1';
const labelled = (label) => `/repos/o/r/issues?state=open&labels=${label}&per_page=100`;

function answering(answers) {
  return async (path) => {
    if (!(path in answers)) throw new Error(`404 on ${path}`);
    return answers[path];
  };
}

const EVERYTHING = {
  [RUNS]: { workflow_runs: [{ id: 2, status: 'in_progress', html_url: 'u2' }] },
  [DEPLOYMENTS]: [{ id: 41 }],
  '/repos/o/r/deployments/41/statuses?per_page=1': [{ state: 'success', log_url: 'u1' }],
  '/repos/o/r/actions/workflows/nightly.yml/runs?branch=main&per_page=1': { workflow_runs: [] },
  [labelled('red-main')]: [{ number: 7, title: 'main is red', created_at: 'a' }],
  [labelled('red-nightly')]: [],
  [labelled('follow-up')]: [
    { number: 8, title: 'Left', created_at: 'b' },
    { number: 9, title: 'A pull request', created_at: 'c', pull_request: {} },
  ],
  [labelled('flaky')]: [],
  [labelled('ops')]: [],
  '/repos/o/r/pulls?state=open&per_page=100': [{ number: 5, title: 'Fix', draft: false, head: { sha: 'abc' } }],
  '/repos/o/r/commits/abc/check-runs?per_page=100': { check_runs: [{ status: 'completed', conclusion: 'success' }] },
  '/repos/o/r/actions/runs?head_sha=abc&per_page=100': { workflow_runs: [{ status: 'in_progress', conclusion: null }] },
};

test('a deployment that has no status yet is running, and none at all is unknown', async () => {
  const pending = await readGitHub('o/r', answering({ ...EVERYTHING, '/repos/o/r/deployments/41/statuses?per_page=1': [] }));
  assert.deepEqual(pending.runs.deploy, { status: 'running', url: undefined });
  const none = await readGitHub('o/r', answering({ ...EVERYTHING, [DEPLOYMENTS]: [] }));
  assert.equal(none.runs.deploy, undefined);
  assert.deepEqual(none.unread, []);
});

test('GitHub is read field by field: the runs, the newest deployment, the labels, the pull requests', async () => {
  const facts = await readGitHub('o/r', answering(EVERYTHING));
  assert.deepEqual(facts.runs, {
    main: { status: 'running', url: 'u2' },
    deploy: { status: 'green', url: 'u1' },
    nightly: { status: 'never' },
  });
  assert.deepEqual(facts.health, [{ number: 7, title: 'main is red', label: 'red-main', createdAt: 'a' }]);
  assert.deepEqual(facts.issues, [{ number: 8, title: 'Left', label: 'follow-up', createdAt: 'b' }]);
  assert.deepEqual(facts.pullRequests, [{ number: 5, title: 'Fix', draft: false, checks: 'running' }]);
  assert.deepEqual(facts.unread, []);
  assert.match(render({ now: NOW, ...facts }), /nightly : jamais lancé/);
});

test('a field that fails is named instead of guessed, and the deployment is named apart from main', async () => {
  const { [DEPLOYMENTS]: _, '/repos/o/r/pulls?state=open&per_page=100': __, ...rest } = EVERYTHING;
  const facts = await readGitHub('o/r', answering(rest));
  assert.equal(facts.runs.main.status, 'running');
  assert.equal(facts.runs.deploy, undefined);
  assert.equal(facts.pullRequests, undefined);
  assert.deepEqual(facts.unread, ['déploiement', 'PR']);
});

test('a full page is named, for what lies past it was not read', async () => {
  const many = Array.from({ length: 100 }, (_, index) => ({ number: index, title: 't', created_at: 'x' }));
  const facts = await readGitHub('o/r', answering({ ...EVERYTHING, [labelled('ops')]: many }));
  assert.deepEqual(facts.unread, ['ops au-delà de 100']);
});

test('nothing read, or a run not green, is never a quiet day', () => {
  const green = { status: 'green' };
  assert.equal(quiet({ runs: { main: green, deploy: green, nightly: { status: 'never' } } }), true);
  assert.doesNotMatch(render({ now: NOW }), /tout est vert/);
  assert.doesNotMatch(render({ now: NOW, runs: { main: green, deploy: green, nightly: green }, unread: ['PR'] }), /tout est vert/);
  assert.doesNotMatch(render({ now: NOW, runs: { main: { status: 'running' }, deploy: green, nightly: green } }), /tout est vert/);
});

test('a busy morning still fits a phone screen', () => {
  const many = (label) => Array.from({ length: 10 }, (_, index) => ({ number: index, title: 'x'.repeat(90), label, checks: 'red' }));
  const text = render({
    now: NOW,
    merged: many(),
    runs: { main: { status: 'red', url: 'https://u' }, deploy: { status: 'red', url: 'https://u' }, nightly: { status: 'red', url: 'https://u' } },
    pullRequests: many(),
    sessions: many(),
    issues: many('follow-up'),
  });
  assert.ok(text.split('\n').length <= 35, `${text.split('\n').length} lines`);
});
