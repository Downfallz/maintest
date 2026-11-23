import test from 'node:test';
import assert from 'node:assert/strict';

import { countRows, countedLine, kindLabel, noticeQueueLabel, noticeQueueRows } from './admin-notices.js';

const NOW = Date.parse('2026-11-23T20:00:00Z');

test('a notices queue is named by what it holds', () => {
  assert.equal(noticeQueueLabel('notices'), 'Waiting to be sent');
  assert.equal(noticeQueueLabel('notices-poison'), 'Given up on, being set aside');
  assert.equal(noticeQueueLabel('notices-dlq'), 'Given up on, kept a week');
});

test('a queue holding notices given up on is a warning, and an empty one says no age', () => {
  const rows = noticeQueueRows({ queues: [
    { name: 'notices', count: 2, oldest: '2026-11-23T19:58:00Z' },
    { name: 'notices-poison', count: 0, oldest: null },
    { name: 'notices-dlq', count: 1, oldest: '2026-11-23T18:00:00Z' },
  ] }, NOW);

  assert.deepEqual(rows.map(row => [row.count, row.oldest, row.warn]), [[2, '2 min ago', false], [0, '', false], [1, '2 h ago', true]]);
});

test('a kind is said in the words of What pops up, and one this page does not know keeps its name', () => {
  assert.deepEqual(['turn', 'invitation', 'friend', 'match', 'tournament'].map(kindLabel), ['Your turn', 'Invitations', 'Friends', 'Results', 'tournament']);
});

test('the counts are a row a kind with today beside the week, and a row of totals last', () => {
  const rows = countRows({
    day: [{ kind: 'turn', sent: 3, refused: 1, gone: 0 }],
    week: [{ kind: 'turn', sent: 10, refused: 2, gone: 1 }, { kind: 'match', sent: 4, refused: 0, gone: 1 }],
  });

  assert.deepEqual(rows.map(row => [row.label, row.day, row.week]), [
    ['Results', { sent: 0, refused: 0, gone: 0 }, { sent: 4, refused: 0, gone: 1 }],
    ['Your turn', { sent: 3, refused: 1, gone: 0 }, { sent: 10, refused: 2, gone: 1 }],
    ['All', { sent: 3, refused: 1, gone: 0 }, { sent: 14, refused: 2, gone: 2 }],
  ]);
  assert.equal(rows.at(-1).total, true);
});

test('no count is no row, not even the totals, and a count without its kind or with no number is skipped or zero', () => {
  assert.deepEqual(countRows({ day: [], week: [] }), []);
  assert.deepEqual(countRows({ day: [null, { sent: 2 }], week: [{ kind: 'turn', sent: 'x', refused: -1 }] }).map(row => [row.label, row.week]),
    [['Your turn', { sent: 0, refused: 0, gone: 0 }], ['All', { sent: 0, refused: 0, gone: 0 }]]);
});

test('the line above the counts says what they are, that nothing was sent, or that the host counts nothing', () => {
  assert.match(countedLine({ counted: true, days: 7, week: [{ kind: 'turn', sent: 1 }] }), /^Today \(UTC, since midnight\) and the last 7 days\./);
  assert.equal(countedLine({ counted: true, days: 7, week: [] }), 'Nothing was sent in the last 7 days.');
  assert.match(countedLine({ counted: false }), /could not open its counts/);
});
