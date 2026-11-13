import test from 'node:test';
import assert from 'node:assert/strict';

import { didLine, jobsPath, passSaid, runRows, runsLine, tookText, totalsLine, touchedTables, triggerText } from './admin-jobs.js';

const NOW = Date.parse('2026-11-13T20:00:00Z');
const run = extra => ({ id: '20261113T180000000Z-abcdef', started: '2026-11-13T18:00:00Z', finished: '2026-11-13T18:00:03Z', seconds: 3.2,
  trigger: 'schedule', by: null, outcome: 'completed', error: null, neverBegan: [], invitationsExpired: 0, timedOut: [], abandoned: [], warned: [], ...extra });

test('a page of runs is asked for by its page, the first left out', () => {
  assert.equal(jobsPath(), '/api/admin/jobs/tend');
  assert.equal(jobsPath(1), '/api/admin/jobs/tend');
  assert.equal(jobsPath(3), '/api/admin/jobs/tend?page=3');
});

test('a run says only what it did, or that nothing was due', () => {
  assert.equal(didLine(run()), 'Nothing was due');
  assert.equal(didLine(run({ neverBegan: ['a', 'b'], invitationsExpired: 1, timedOut: ['c'], abandoned: ['d'], warned: ['e', 'f'] })),
    '2 tables closed · 1 invitation let go of · 1 game timed out · 1 left game ended · 2 players warned');
});

test('a run names what started it, and the operator who did', () => {
  assert.equal(triggerText(run()), 'Hourly job');
  assert.equal(triggerText(run({ trigger: 'manual', by: 'mk' })), 'By hand, mk');
  assert.equal(triggerText(run({ trigger: 'manual' })), 'By hand');
});

test('a run says how it ended and how long it took', () => {
  assert.equal(tookText(run()), 'Completed in 3.2 s');
  assert.equal(tookText(run({ outcome: 'failed', seconds: undefined })), 'Failed');
});

test('a failed run shows its error where the others show what they did', () => {
  const [row] = runRows({ runs: [run({ outcome: 'failed', error: 'the store is out' })] }, NOW);

  assert.equal(row.failed, true);
  assert.equal(row.did, 'the store is out');
  assert.equal(row.when, '2 h ago');
  assert.equal(row.started, '2026-11-13 18:00:00 UTC');
});

test('the totals say the runs and what they did, and the failed ones', () => {
  assert.equal(totalsLine({ runs: 0 }, 'In 24 hours'), 'In 24 hours: no run.');
  assert.equal(totalsLine({ runs: 24, failed: 1, neverBegan: 3, invitationsExpired: 2, timedOut: 1, abandoned: 0, warned: 4 }, 'In 24 hours'),
    'In 24 hours: 24 runs, 3 tables that never began closed, 1 game timed out, 0 left by a player, 2 invitations let go of (1 failed).');
});

test('the pager says how many runs are kept and which page', () => {
  assert.equal(runsLine({ total: 45, page: 2, pages: 3 }), '45 runs kept · page 2 of 3');
});

test('every table a run touched is listed with what it did and the way to its session', () => {
  assert.deepEqual(touchedTables(run({ neverBegan: ['t1'], timedOut: ['t 2'], warned: ['t3'] })), [
    { id: 't1', what: 'Closed: it never began', session: '/session/t1' },
    { id: 't 2', what: 'Ended: a player let their time run out', session: '/session/t%202' },
    { id: 't3', what: 'Warned: a quarter of the time left', session: '/session/t3' },
  ]);
});

test('a pass run now says what it did, and a refusal keeps the host sentence', () => {
  assert.deepEqual(passSaid({ ok: true, body: { run: run({ timedOut: ['a'] }) } }), { refused: false, line: 'Pass done: 1 game timed out.' });
  assert.deepEqual(passSaid({ ok: false, status: 409, body: { message: 'A pass is already running.' } }), { refused: true, line: 'A pass is already running.' });
});
