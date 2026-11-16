import test from 'node:test';
import assert from 'node:assert/strict';

import { handledSaid, letterRows, moreLine, queueLabel, queueRows } from './admin-turns.js';

const NOW = Date.parse('2026-11-13T20:00:00Z');

test('a queue is named by what it holds', () => {
  assert.equal(queueLabel('agent-turns'), 'Turns owed');
  assert.equal(queueLabel('agent-turns-poison'), 'Given up on, being set aside');
  assert.equal(queueLabel('agent-turns-dlq'), 'Dead letters');
});

test('a queue holding turns given up on is a warning, and an empty one says no age', () => {
  const rows = queueRows({ queues: [
    { name: 'agent-turns', count: 3, oldest: '2026-11-13T19:55:00Z' },
    { name: 'agent-turns-poison', count: 0, oldest: null },
    { name: 'agent-turns-dlq', count: 2, oldest: '2026-11-13T17:00:00Z' },
  ] }, NOW);

  assert.deepEqual(rows.map(row => [row.count, row.oldest, row.warn]), [[3, '5 min ago', false], [0, '', false], [2, '3 h ago', true]]);
});

test('a letter says its table, when it was set aside, how often it was delivered and its error, linked to the session', () => {
  const [row] = letterRows({ letters: [{ id: 'm1', table: 'table 7', delivered: 5, deadLetteredAt: '2026-11-13T19:00:00Z', error: 'The store is out.', fullError: 'The store is out.\n at X', host: 'worker-1' }] }, NOW);

  assert.deepEqual(row, { id: 'm1', table: 'table 7', delivered: '5 deliveries', when: '1 h ago', error: 'The store is out.', fullError: 'The store is out.\n at X', host: 'worker-1', session: '/session/table%207' });
});

test('an item that is not a turn has no table and no session', () => {
  const [row] = letterRows({ letters: [{ id: 'm2', table: null, error: 'Not a dead letter: kept as it was.' }] }, NOW);

  assert.equal(row.table, 'not a turn');
  assert.equal(row.session, null);
});

test('more letters than shown are said, and none when every one is shown', () => {
  const letters = Array.from({ length: 32 }, (_, index) => ({ id: `m${index}` }));
  assert.equal(moreLine({ letters, queues: [{ name: 'agent-turns-dlq', count: 45 }] }), 'Showing the first 32 of 45. Requeue or drop these to see the rest.');
  assert.equal(moreLine({ letters: letters.slice(0, 2), queues: [{ name: 'agent-turns-dlq', count: 2 }] }), '');
});

test('a requeue or a drop says how many letters it handled and how many were gone', () => {
  assert.deepEqual(handledSaid('requeue', { ok: true, body: { handled: ['a'], missing: [] } }), { refused: false, line: '1 letter requeued.' });
  assert.deepEqual(handledSaid('drop', { ok: true, body: { handled: ['a', 'b'], missing: ['c'] } }), { refused: false, line: '2 letters dropped; 1 no longer there.' });
  assert.deepEqual(handledSaid('drop', { ok: false, status: 400, body: { message: 'Say which letters to drop, by their ids.' } }), { refused: true, line: 'Say which letters to drop, by their ids.' });
});
