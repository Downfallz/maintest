import test from 'node:test';
import assert from 'node:assert/strict';
import { stepAnswered, waitAnswered } from './browser/battle-step.js';

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

// #858: a loaded runner served the next view after the 10 s window of a poll set at the call, while the host still
// moved. The window restarts at each view that shows the game moved; a seat that stands still for a window fails.
function clock() {
  let now = 0;
  return { now: () => now, sleep: async (ms) => { now += ms; } };
}
test('a step answered after the first window is waited for while the host keeps moving', async () => {
  const time = clock(), asked = view('Evolution', 3);
  // The board moves every 6 s (a feed entry), and the next asking comes at 21 s.
  const read = () => time.now() >= 21000 ? view('Evolution', 4) : view('Evolution', 3, 2, { feed: [{ sequence: Math.floor(time.now() / 6000) }] });
  await waitAnswered(asked, read, { timeout: 10000, ...time });
  assert.ok(time.now() >= 21000 && time.now() < 22000);
});
test('a step the host stops moving on fails after one window, saying what the seat showed', async () => {
  const time = clock();
  await assert.rejects(waitAnswered(view('Evolution', 3), () => view('Evolution', 3), { timeout: 10000, ...time }),
    /not answered: the seat showed nothing new for 10 s.*"waitingFor":"Evolution","waitingAsked":3/s);
  assert.ok(time.now() >= 10000 && time.now() < 11000);
});
test('a host that keeps moving without answering the step still fails at three windows', async () => {
  const time = clock();
  const read = () => view('Evolution', 3, 2, { feed: [{ sequence: time.now() }] });
  await assert.rejects(waitAnswered(view('Evolution', 3), read, { timeout: 10000, ...time }), /in 30 s in all/);
  assert.ok(time.now() >= 30000 && time.now() < 31000);
});
test('a seat with no view yet moves once its first view arrives', async () => {
  const time = clock();
  await waitAnswered(view('Evolution', 3), () => time.now() < 5000 ? undefined : view(null, null, 2, { over: true }), { timeout: 10000, ...time });
  assert.ok(time.now() >= 5000);
});
