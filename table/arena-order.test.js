import { test } from 'node:test';
import assert from 'node:assert/strict';
import { arenaOrder, arenaTieGroups, createArenaOrderReveal } from './arena-order.js';
import { livePresentation } from './arena-live.js';
import { historyEntry } from './arena-history.js';

const slot = (creature, owner = 'Player1', speed = 'Quick', initiative = 5) => ({ creature, owner, speed, initiative });
const timeline = [slot(2), slot(4, 'Player2'), slot(1), slot(5, 'Player2', 'Standard', 3), slot(3, 'Player1', 'Standard', 3)];
const rolls = [{ creature: 2, rolls: [11, 18] }, { creature: 4, rolls: [11, 8] }, { creature: 1, rolls: [2] }];
const board = { slot: 'Player1', roundNumber: 2, subPhase: 'Activation', phase: 'Combat', activationCursor: 2, timeline, rollOffs: rolls,
  allies: [1, 2, 3].map(id => ({ id, name: `Ally ${id}`, isAlive: id !== 2, isStunned: id === 3 })),
  enemies: [4, 5].map(id => ({ id, name: `Enemy ${id}`, isAlive: true })) };
const view = (overrides = {}) => ({ board, ...overrides });
const reveal = (sequence, round = 2) => ({ sequence, event: { kind: 'TimelineBuilt', roundId: round, timeline: { slots: timeline, rollOffs: rolls } } });

test('order follows exact host positions and recorded rerolls without ranking or dropping defeated/stunned creatures', () => {
  const model = arenaOrder(livePresentation(view()));
  assert.deepEqual(model.rows.map(row => row.id), ['2', '4', '1', '5', '3']);
  assert.deepEqual(model.rows.map(row => row.state), ['Completed', 'Completed', 'Now', 'Upcoming', 'Upcoming']);
  assert.equal(model.rows[0].roll, 'd20 11 → 18'); assert.equal(model.rows[0].condition, 'Defeated');
  assert.equal(model.rows[4].condition, 'Stunned'); assert.equal(model.rows[1].team, 'Opponent');
  assert.equal(model.rows[0].initiative, 5); assert.match(model.summary[0], /2 Instinct · 1 Precision/);
});
test('declaration is upcoming, cleanup completed and missing initiative never borrowed from creature stats', () => {
  for (const [subPhase, state] of [['IntentSelection', 'Upcoming'], ['Cleanup', 'Completed'], ['Finalization', 'Completed']]) {
    const model = arenaOrder(livePresentation(view({ board: { ...board, subPhase } })));
    assert.ok(model.rows.every(row => row.state === state));
  }
  assert.equal(arenaOrder(livePresentation(view({ board: { ...board, timeline: [slot(1, 'Player1', 'Quick', undefined)].map(one => ({ ...one, initiative: undefined })) } }))).rows[0].initiative, undefined);
});
test('unrevealed tempos and hidden tie submissions never populate order or reveal summary', () => {
  const privateView = view({ board: { ...board, timeline: [], speedChoices: [{ creature: 4, speed: 'Quick' }] },
    feed: [{ sequence: 1, event: { kind: 'SpeedChoiceSubmitted' } }] });
  assert.deepEqual(arenaOrder(livePresentation(privateView)).rows, []);
  const controller = createArenaOrderReveal(); controller.receive(privateView);
  controller.receive({ ...privateView, feed: [{ sequence: 2, event: { kind: 'TieOrderSubmitted' } }] });
  assert.equal(controller.model().pending, false);
});
test('multiple tie groups map chosen occupants only to their offered positions with opponent places fixed', () => {
  const current = view({ options: { tieOrder: { ties: [[2, 1], [3]] } } });
  const groups = arenaTieGroups(current, [1, 3]);
  assert.deepEqual(groups[0].places, [1, 3]);
  assert.deepEqual(groups[0].assignments, [{ id: 1, position: 1 }, { id: 2, position: 3 }]);
  assert.deepEqual(groups[0].fixed.map(row => row.position), [2]);
  assert.deepEqual(groups[1].places, [5]); assert.equal(groups[1].rolled, false);
  assert.deepEqual(arenaTieGroups({ ...current, board: { ...board, timeline: [] } })[0].assignments,
    [{ id: 2, position: null }, { id: 1, position: null }]);
});
test('reveal is once per observed round, never over an unresolved tie or replayed on duplicates', () => {
  const controller = createArenaOrderReveal(); controller.receive(view({ feed: [] }));
  // The driver may expose the built timeline before it has asked the tie question.
  controller.receive(view({ board: { ...board, subPhase: 'TurnOrderResolution' }, feed: [reveal(1)] }));
  assert.equal(controller.model().pending, false);
  controller.receive(view({ board: { ...board, subPhase: 'TieOrder' }, waitingFor: 'TieOrder', feed: [reveal(1)] }));
  assert.equal(controller.model().pending, false);
  controller.receive(view({ feed: [reveal(1)] })); assert.equal(controller.model().pending, true);
  controller.presented(); controller.receive(view({ feed: [reveal(1)] })); assert.equal(controller.model().pending, false);
});
test('first read, reconnect, ended match, waiting room and newer round do not invent a fresh reveal', () => {
  for (const extra of [{}, { replay: true }, { waiting: { seats: [] } }, { over: true }]) {
    const controller = createArenaOrderReveal(); controller.receive(view({ ...extra, feed: [reveal(3)] }));
    assert.equal(controller.model().pending, false);
  }
  const controller = createArenaOrderReveal(); controller.receive(view({ feed: [] }));
  controller.receive(view({ feed: [reveal(1)] })); controller.receive(view({ board: { ...board, roundNumber: 3 }, feed: [] }));
  assert.equal(controller.model().pending, false);
  controller.receive(null); controller.receive(view({ feed: [reveal(1)] })); assert.equal(controller.model().pending, false);
});
test('history retains initial and final public order with event initiatives and rolls rather than current values', () => {
  for (const kind of ['TimelineBuilt', 'TiesOrdered']) {
    const entry = reveal(4); entry.event.kind = kind;
    const row = historyEntry(entry, { ...board, rollOffs: [{ creature: 2, rolls: [20] }] });
    assert.match(row.details[0].text, /Initiative 5 · d20 11 → 18/);
    assert.doesNotMatch(row.details[0].text, /d20 20/);
    const legacy = historyEntry({ ...entry, event: { ...entry.event, timeline: { slots: [slot(2)] } } }, board);
    assert.match(legacy.details[0].text, /No d20 roll recorded/);
  }
});
