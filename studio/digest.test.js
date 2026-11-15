// The morning digest (scripts/digest/digest.mjs, written by the /digest skill): what it says, in what order, and
// that it fits a phone's screen.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { dayLabel, mergedFromLog, noteworthy, render, shorten, waitingFor, withDefaults } from '../scripts/digest/digest.mjs';

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
  assert.equal(waitingFor({ checks: 'green', draft: true }), null);
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

test('the window defaults to the 24 hours before the run, and git is read only when merged is left out', () => {
  let asked = null;
  const facts = withDefaults({ now: NOW }, (since) => {
    asked = since.toISOString();
    return [];
  });
  assert.equal(asked, '2026-11-15T11:40:00.000Z');
  assert.equal(facts.since, '2026-11-15T11:40:00.000Z');
  assert.deepEqual(withDefaults({ now: NOW, merged: [] }, () => assert.fail('git read')).merged, []);
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
