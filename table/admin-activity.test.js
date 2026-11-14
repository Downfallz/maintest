import test from 'node:test';
import assert from 'node:assert/strict';
import { ACTION_LABELS, activityLine, activityPath, activityRows } from './admin-activity.js';

const NOW = Date.parse('2026-11-14T03:00:00Z');

test('a page of the log is asked for by its number, the first left out', () => {
  assert.equal(activityPath(), '/api/admin/activity');
  assert.equal(activityPath(1), '/api/admin/activity');
  assert.equal(activityPath(3), '/api/admin/activity?page=3');
});

test('an action reads as what was done, by whom and when, with the way to the table\'s session', () => {
  const [voided, suspended, deleted] = activityRows({ actions: [
    { id: 'a', at: '2026-11-14T02:00:00Z', by: 'mark', action: 'table.void', target: 't-1', detail: 'stuck: The match stopped answering.' },
    { id: 'b', at: '2026-11-14T02:30:00Z', by: 'mark', action: 'player.suspend', target: 'acct_0000000000000001', detail: 'Bastien: Spam.' },
    { id: 'c', at: '2026-11-14T02:45:00Z', by: 'ops', action: 'sessions.delete', target: 't-2', detail: null },
  ] }, NOW);

  assert.deepEqual(voided, { id: 'a', when: '1 h ago', at: '2026-11-14 02:00:00 UTC', by: 'mark', what: 'Voided a match', target: 't-1',
    detail: 'stuck: The match stopped answering.', session: '/session/t-1' });
  assert.equal(suspended.what, 'Suspended an account');
  assert.equal(suspended.session, null);
  assert.equal(deleted.session, null, 'a deleted session has no page left to open');
});

test('every action the host notes has a label, and one this page does not know reads as its name', () => {
  assert.equal(Object.keys(ACTION_LABELS).length, 17);
  assert.equal(activityRows({ actions: [{ id: 'x', at: null, by: null, action: 'host.restart' }] }, NOW)[0].what, 'host.restart');
});

test('the pager says how many actions are kept, the limit, and which page', () => {
  assert.equal(activityLine({ total: 45, page: 2, pages: 3, kept: 1000 }), '45 actions kept (the last 1000) · page 2 of 3');
  assert.equal(activityLine({ total: 1, page: 1, pages: 1 }), '1 action kept (the last 1000) · page 1 of 1');
});
