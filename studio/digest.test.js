// The morning digest (scripts/digest/digest.mjs, written by the /digest skill): what it says, in what order, and
// that it fits a phone's screen.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  checksStatus,
  dayLabel,
  deployStatus,
  gather,
  mergedFromLog,
  noteworthy,
  originRepository,
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
  assert.match(text, /main : vert · déploiement : vert · nuit : vert/);
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
  assert.match(render({ now: NOW }), /main : inconnu · déploiement : inconnu · nuit : inconnu/);
});

test('a pull request waits for the owner when it is green with no open thread, and a draft waits for nobody', () => {
  assert.equal(waitingFor({ checks: 'green', openThreads: 0 }), 'prête, à fusionner');
  assert.equal(waitingFor({ checks: 'red', openThreads: 2 }), 'rouge');
  assert.equal(waitingFor({ checks: 'green', openThreads: 2 }), '2 fil(s) ouvert(s)');
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

test('what could not be read is named last', () => {
  assert.match(render({ now: NOW, unread: ['PR', 'nuit'] }), /\n\nNon lu : PR, nuit$/);
});

test('a run is running until it completes, and green only on success', () => {
  assert.equal(runStatus({ status: 'pending' }), 'running');
  assert.equal(runStatus({ status: 'completed', conclusion: 'success' }), 'green');
  assert.equal(runStatus({ status: 'completed', conclusion: 'cancelled' }), 'red');
});

test('a skipped deployment says nothing, and a called one reads its jobs', () => {
  assert.equal(deployStatus([{ name: 'gate', conclusion: 'success' }, { name: 'deploy', conclusion: 'skipped' }]), null);
  assert.equal(deployStatus([{ name: 'deploy / roll out', status: 'completed', conclusion: 'success' }]), 'green');
  assert.equal(deployStatus([{ name: 'deploy / roll out', status: 'completed', conclusion: 'failure' }]), 'red');
  assert.equal(deployStatus([{ name: 'deploy / roll out', status: 'in_progress', conclusion: null }]), 'running');
});

test('a pull request with no check run has no checks to report', () => {
  assert.equal(checksStatus([]), undefined);
  assert.equal(checksStatus([{ status: 'completed', conclusion: 'success' }, { status: 'completed', conclusion: 'skipped' }]), 'green');
  assert.equal(checksStatus([{ status: 'completed', conclusion: 'failure' }]), 'red');
});

test('the repository is the origin on GitHub, and nothing else', () => {
  assert.equal(originRepository('https://github.com/Downfallz/maintest.git\n'), 'Downfallz/maintest');
  assert.equal(originRepository('git@github.com:Downfallz/maintest'), 'Downfallz/maintest');
  assert.throws(() => originRepository('https://example.com/a/b'));
});

test('GitHub is read field by field, and a field that fails is named instead of guessed', async () => {
  const answers = {
    '/repos/o/r/actions/workflows/ci.yml/runs?branch=main&event=push&per_page=5': {
      workflow_runs: [
        { id: 2, status: 'in_progress', html_url: 'u2' },
        { id: 1, status: 'completed', conclusion: 'success', html_url: 'u1' },
      ],
    },
    '/repos/o/r/actions/runs/2/jobs?filter=latest&per_page=100': { jobs: [{ name: 'deploy', conclusion: 'skipped' }] },
    '/repos/o/r/actions/runs/1/jobs?filter=latest&per_page=100': {
      jobs: [{ name: 'deploy / roll out', status: 'completed', conclusion: 'success' }],
    },
    '/repos/o/r/issues?state=open&per_page=100': [
      { number: 7, title: 'main is red', labels: [{ name: 'red-main' }], created_at: 'a' },
      { number: 8, title: 'Left', labels: [{ name: 'follow-up' }], created_at: 'b' },
      { number: 9, title: 'A pull request', labels: [{ name: 'follow-up' }], pull_request: {} },
    ],
  };
  const facts = await readGitHub('o/r', async (path) => {
    if (!(path in answers)) throw new Error(`404 on ${path}`);
    return answers[path];
  });
  assert.deepEqual(facts.runs, { main: { status: 'running', url: 'u2' }, deploy: { status: 'green', url: 'u1' } });
  assert.deepEqual(facts.health, [{ number: 7, title: 'main is red', label: 'red-main', createdAt: 'a' }]);
  assert.deepEqual(facts.issues, [{ number: 8, title: 'Left', label: 'follow-up', createdAt: 'b' }]);
  assert.deepEqual(facts.unread, ['nuit', 'PR']);
  assert.equal(facts.pullRequests, undefined);
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
