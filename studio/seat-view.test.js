import test from 'node:test';
import assert from 'node:assert/strict';
import { readSeatViews } from './browser/seat-view.js';

const view = (feedNext, waitingFor, waitingAsked) => ({ board: {}, feedNext, waitingFor, waitingAsked });

// #787: the responses' bodies are read asynchronously, so an earlier read can finish after a later one.
test('a seat view that finishes reading after a newer one does not take its place', () => {
  const seat = readSeatViews();
  const tie = view(40, 'TieOrder', 8), tempos = view(37, 'Speed', 7);
  seat.receive(tie); seat.receive(tempos);
  assert.equal(seat.latest, tie);
});
test('a later asking on the same journal is the newer view', () => {
  const seat = readSeatViews(), waiting = view(40, null, null), asked = view(40, 'Speed', 7);
  seat.receive(waiting); seat.receive(asked); seat.receive(waiting);
  assert.equal(seat.latest, asked);
});
test('the views arriving in order are each the latest, and a body without a board is not a view', () => {
  const seat = readSeatViews(), seen = [];
  for (const one of [view(10, 'Evolution', 1), view(12, 'Speed', 2), { error: 'gone' }, view(12, 'Speed', 2)]) {
    seat.receive(one); seen.push(seat.latest);
  }
  assert.deepEqual(seen.map(one => one.waitingAsked), [1, 2, 2, 2]);
});
