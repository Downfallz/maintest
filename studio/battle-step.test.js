import test from 'node:test';
import assert from 'node:assert/strict';
import { stepAnswered } from './browser/battle-step.js';

const view = (waitingFor, waitingAsked, roundNumber = 2, extras = {}) => ({ board: { roundNumber }, waitingFor, waitingAsked, ...extras });

// #787: the team's tempos are one asking per creature, sent one after another from one sheet. The next creature's
// asking is not the step's end: the next step would read it, while the page has gone on to the tie order.
test('a team tempo step is not answered while the round still asks a tempo', () => {
  assert.equal(stepAnswered(view('Speed', 7), view('Speed', 8)), false);
  assert.equal(stepAnswered(view('Speed', 7), view(null, null)), false);
});
test('a team tempo step is answered once the round has left its tempos', () => {
  assert.equal(stepAnswered(view('Speed', 7), view('TieOrder', 10)), true);
  assert.equal(stepAnswered(view('Speed', 7), view('Intent', 11)), true);
  assert.equal(stepAnswered(view('Speed', 7), view('Speed', 12, 3)), true);
  assert.equal(stepAnswered(view('Speed', 7), view(null, null, 2, { over: true })), true);
});
test('any other step is answered once the seat is asked something else', () => {
  assert.equal(stepAnswered(view('Evolution', 3), view('Evolution', 3)), false);
  assert.equal(stepAnswered(view('Evolution', 3), view('Evolution', 4)), true);
  assert.equal(stepAnswered(view('Action', 9), view(null, null)), true);
});
