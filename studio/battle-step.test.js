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

// #858: on a loaded runner the next view arrived after the first 10 s, counted from the poll's start, while the host
// was still moving. The window restarts at each view that shows the game moved; a seat that stands still for a window fails.
function clock() {
  let now = 0;
  return { now: () => now, sleep: async (ms) => { now += ms; } };
}
test('a step answered after the first window is waited for while the host keeps moving', async () => {
  const time = clock(), asked = view('Evolution', 3);
  // The host's feed moves every 6 s, served as the route does: the new entries once, then none, the cursor rising.
  // The next asking comes at 21 s.
  const read = () => {
    const batch = Math.floor(time.now() / 6000), fresh = time.now() % 6000 < 100;
    return time.now() >= 21000 ? view('Evolution', 4) : view('Evolution', 3, 2, { feed: fresh ? [{ sequence: batch }] : [], feedNext: batch + 1 });
  };
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
  const read = () => view('Evolution', 3, 2, { feed: [{ sequence: time.now() }], feedNext: time.now() + 1 });
  await assert.rejects(waitAnswered(view('Evolution', 3), read, { timeout: 10000, ...time }), /within 30 s, after \d+ moves, though the seat kept moving/);
  assert.ok(time.now() >= 30000 && time.now() < 31000);
});
test("a seat's first view counts as progress", async () => {
  const time = clock();
  await waitAnswered(view('Evolution', 3), () => time.now() < 5000 ? undefined : view(null, null, 2, { over: true }), { timeout: 10000, ...time });
  assert.ok(time.now() >= 5000);
});
test('a seat whose feed only empties and refills from the same cursor is stuck', async () => {
  const time = clock();
  // A delta read, then a full read of the same feed: the entries differ, the host's cursor does not.
  const read = () => view('Evolution', 3, 2, { feed: Math.floor(time.now() / 1000) % 2 ? [{ sequence: 4 }] : [], feedNext: 5 });
  await assert.rejects(waitAnswered(view('Evolution', 3), read, { timeout: 10000, ...time }), /nothing new for 10 s after 0 moves/);
});
