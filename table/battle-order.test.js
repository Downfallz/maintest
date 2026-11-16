import { test } from 'node:test';
import assert from 'node:assert/strict';
import { battleOrder, battleTieGroups, createBattleOrderReveal, orderReason, rollOffDice, tieMarks } from './battle-order.js';
import { livePresentation } from './battle-live.js';

const slot = (creature, owner = 'Player1', speed = 'Quick', initiative = 5) => ({ creature, owner, speed, initiative });
// One Instinct tie across teams settled by d20 rolls (2 and 1 for us, 4 for them), then a Precision pair of our own.
const timeline = [slot(2), slot(4, 'Player2'), slot(1), slot(5, 'Player2', 'Standard', 3), slot(3, 'Player1', 'Standard', 3)];
const rolls = [{ creature: 2, rolls: [11, 18] }, { creature: 4, rolls: [11, 8] }, { creature: 1, rolls: [2] }];
const board = { slot: 'Player1', roundNumber: 2, subPhase: 'Activation', phase: 'Combat', activationCursor: 2, timeline, rollOffs: rolls,
  allies: [1, 2, 3].map(id => ({ id, name: `Ally ${id}`, isAlive: id !== 2, isStunned: id === 3, currentInitiative: 9 })),
  enemies: [4, 5].map(id => ({ id, name: `Enemy ${id}`, isAlive: true })) };
const view = (overrides = {}) => ({ board, ...overrides });
const order = overrides => battleOrder(livePresentation(view(overrides)));
const reveal = (sequence, round = 2) => ({ sequence, event: { kind: 'TimelineBuilt', roundId: round, timeline: { slots: timeline, rollOffs: rolls } } });

test('the order is the host\'s positions as served, never re-ranked', () => {
  assert.deepEqual(order().rows.map(row => row.id), ['2', '4', '1', '5', '3']);
});
test('slots before the cursor are completed, the cursor is now and the rest are upcoming', () => {
  assert.deepEqual(order().rows.map(row => row.state), ['Completed', 'Completed', 'Now', 'Upcoming', 'Upcoming']);
});
test('every slot is upcoming while intents are declared', () => {
  assert.ok(order({ board: { ...board, subPhase: 'IntentSelection' } }).rows.every(row => row.state === 'Upcoming'));
});
for (const subPhase of ['Cleanup', 'Finalization']) test(`every slot is completed in ${subPhase}`, () => {
  assert.ok(order({ board: { ...board, subPhase } }).rows.every(row => row.state === 'Completed'));
});
test('a slot shows the recorded d20 rolls, rerolls included', () => {
  assert.equal(order().rows[0].roll, 'd20 11 → 18');
});
test('a defeated or stunned creature keeps its slot and says so', () => {
  const rows = order().rows;
  assert.equal(rows[0].condition, 'Defeated'); assert.equal(rows[4].condition, 'Stunned');
});
test('a slot without a recorded initiative stays unknown, never the creature\'s current one', () => {
  const rows = order({ board: { ...board, timeline: [{ ...slot(1), initiative: undefined }] } }).rows;
  assert.equal(rows[0].initiative, undefined);
});
test('the summary counts each team\'s tempos', () => {
  assert.match(order().summary[0], /2 Instinct · 1 Precision/);
});
test('tempos not yet revealed leave the order empty', () => {
  const hidden = view({ board: { ...board, timeline: [], speedChoices: [{ creature: 4, speed: 'Quick' }] } });
  assert.deepEqual(battleOrder(livePresentation(hidden)).rows, []);
});

// Two real groups, as the host offers them: two or more creatures each.
const ties = { tieOrder: { ties: [[2, 1], [3, 6]] } };
const tieBoard = { ...board, subPhase: 'TieOrder', allies: [...board.allies, { id: 6, name: 'Ally 6', isAlive: true }],
  timeline: [...timeline.slice(0, 4), slot(3, 'Player1', 'Standard', 3), slot(6, 'Player1', 'Standard', 3)] };
const tieGroups = (tapped = []) => battleTieGroups(view({ board: tieBoard, options: ties }), tapped);
test('a tie group keeps the places the dice gave it, around the opponent\'s', () => {
  assert.deepEqual(tieGroups()[0].places, [1, 3]);
});
test('the chosen occupants fill their group\'s places in order', () => {
  assert.deepEqual(tieGroups([1, 2])[0].assignments, [{ id: 1, position: 1 }, { id: 2, position: 3 }]);
});
test('an opponent place inside the tie is named as fixed', () => {
  assert.deepEqual(tieGroups()[0].fixed.map(row => row.position), [2]);
});
test('a group settled by d20 rolls says so, and a same-team group does not', () => {
  assert.equal(tieGroups()[0].rolled, true); assert.equal(tieGroups()[1].rolled, false);
});
test('a group without positions in the snapshot leaves them unknown', () => {
  const groups = battleTieGroups(view({ board: { ...tieBoard, timeline: [] }, options: ties }));
  assert.deepEqual(groups[0].assignments, [{ id: 2, position: null }, { id: 1, position: null }]);
});
test('an opponent with one place in the tie has a known occupant', () => {
  assert.equal(tieGroups()[0].opponentOrderOpen, false);
});
test('an opponent with two places in the tie may still reorder them', () => {
  const both = [slot(1), slot(4, 'Player2'), slot(5, 'Player2'), slot(2)];
  const groups = battleTieGroups(view({ board: { ...tieBoard, timeline: both }, options: { tieOrder: { ties: [[1, 2]] } } }));
  assert.equal(groups[0].opponentOrderOpen, true); assert.deepEqual(groups[0].fixed.map(row => row.position), [2, 3]);
});

const pendingAfter = (...views) => { const controller = createBattleOrderReveal(); views.forEach(one => controller.receive(one)); return controller.model().pending; };
test('a reveal seen after the first read is pending once the order is in play', () => {
  assert.equal(pendingAfter(view({ feed: [] }), view({ feed: [reveal(1)] })), true);
});
test('a reveal in the first read is a baseline, not a fresh reveal', () => {
  assert.equal(pendingAfter(view({ feed: [reveal(1)] })), false);
});
test('a reveal waits out the order being resolved and the tie question', () => {
  assert.equal(pendingAfter(view({ feed: [] }), view({ board: { ...board, subPhase: 'TurnOrderResolution' }, feed: [reveal(1)] })), false);
  assert.equal(pendingAfter(view({ feed: [] }), view({ board: { ...board, subPhase: 'TieOrder' }, feed: [reveal(1)] })), false);
});
test('a reveal is shown once: presenting it clears it and a duplicate poll does not bring it back', () => {
  const controller = createBattleOrderReveal(); controller.receive(view({ feed: [] })); controller.receive(view({ feed: [reveal(1)] }));
  controller.presented(); controller.receive(view({ feed: [reveal(1)] })); assert.equal(controller.model().pending, false);
});
for (const [name, extra] of [['an ended match', { over: true }], ['a waiting room', { waiting: { seats: [] } }], ['a replay', { replay: true }]]) {
  test(`a pending reveal is dropped by ${name}`, () => {
    assert.equal(pendingAfter(view({ feed: [] }), view({ feed: [reveal(1)] }), view({ ...extra, feed: [reveal(1)] })), false);
  });
}
test('a pending reveal is dropped when the round moves on', () => {
  assert.equal(pendingAfter(view({ feed: [] }), view({ feed: [reveal(1)] }), view({ board: { ...board, roundNumber: 3 }, feed: [] })), false);
});
test('a reveal of another round is not this round\'s reveal', () => {
  assert.equal(pendingAfter(view({ feed: [] }), view({ feed: [reveal(1, 1)] })), false);
});
test('a seat change is a fresh baseline', () => {
  const controller = createBattleOrderReveal(); controller.receive(view({ feed: [] }));
  controller.receive(null); controller.receive(view({ feed: [reveal(1)] })); assert.equal(controller.model().pending, false);
});
test('a hidden tie submission is not a reveal', () => {
  assert.equal(pendingAfter(view({ feed: [] }), view({ feed: [{ sequence: 2, event: { kind: 'TieOrderSubmitted' } }] })), false);
});
test('a seat that comes back after its order was built is shown it, from the last sequence it was shown', () => {
  const controller = createBattleOrderReveal();
  controller.receive(view({ feed: [reveal(9)] }), { since: 8 });
  assert.equal(controller.model().pending, true); assert.equal(controller.model().unread, 9);
  const seen = createBattleOrderReveal(); seen.receive(view({ feed: [reveal(9)] }), { since: 9 });
  assert.equal(seen.model().pending, false);
  const fresh = createBattleOrderReveal(); fresh.receive(view({ feed: [reveal(9)] }));
  assert.equal(fresh.model().pending, false);
});
test('an order being read stays unread until Continue', () => {
  const controller = createBattleOrderReveal();
  controller.receive(view({ feed: [] })); controller.receive(view({ feed: [reveal(5)] }));
  controller.presented();
  assert.equal(controller.model().pending, false); assert.equal(controller.model().unread, 5);
  controller.read(); assert.equal(controller.model().unread, null);
});

test('roll-off dice say which tie each broke and where its creature finished in it', () => {
  const slot = (creature, speed, initiative) => ({ creature, speed, initiative, owner: creature < 4 ? 'Player1' : 'Player2' });
  const board = { timeline: [slot(5, 'Instinct', 8), slot(1, 'Instinct', 8), slot(2, 'Instinct', 7), slot(4, 'Instinct', 7), slot(3, 'Precision', 5)],
    rollOffs: [{ creature: 1, rolls: [2] }, { creature: 5, rolls: [13] }, { creature: 2, rolls: [9, 9, 17] }, { creature: 4, rolls: [9, 9, 4] }] };
  assert.deepEqual(rollOffDice(board).map(die => [die.id, die.value, die.group, die.place, die.rivals]),
    [['5', 13, 1, 1, 1], ['1', 2, 1, 2, 1], ['2', 17, 2, 1, 1], ['4', 4, 2, 2, 1]]);
  assert.deepEqual(rollOffDice({ timeline: [slot(1, 'Instinct', 8)], rollOffs: [{ creature: 9, rolls: [3] }] }), []);
});

test('a creature alone in its tempo and initiative is placed by its band and its neighbours\' initiative', () => {
  const ranked = { ...board, timeline: [slot(1, 'Player1', 'Quick', 9), slot(4, 'Player2', 'Quick', 7), slot(2, 'Player1', 'Quick', 5), slot(5, 'Player2', 'Standard', 8)], rollOffs: [] };
  const reason = orderReason(livePresentation(view({ board: ranked })), '4');

  assert.equal(reason.position, 2);
  assert.equal(reason.tie, 'none');
  assert.deepEqual(reason.lines, ['Instinct: acts before every Precision creature.', 'Initiative 7: after Ally 1 (9), before Ally 2 (5).']);
});

test('a tie across the teams is explained by the roll-off, with every rolled die and its rerolls', () => {
  const reason = orderReason(livePresentation(view()), '4');

  assert.equal(reason.tie, 'rolled');
  assert.deepEqual(reason.dice.map(die => [die.id, die.rolls, die.self]), [['2', [11, 18], false], ['4', [11, 8], true], ['1', [2], false]]);
  assert.equal(reason.lines[1], 'Initiative 5, tied with Ally 2 and Ally 1.');
});

test('a creature that shared a rolled tie with its own team says its owner ordered those places', () => {
  assert.match(orderReason(livePresentation(view()), '2').lines.at(-1), /^Within its team's places, you chose the order\.$/);
});

test('a tie within one team rolls nothing: its owner chose the order', () => {
  const own = { ...board, timeline: [slot(1, 'Player1', 'Quick', 6), slot(3, 'Player1', 'Quick', 6), slot(4, 'Player2', 'Quick', 2)], rollOffs: [] };

  const reason = orderReason(livePresentation(view({ board: own })), '3');

  assert.equal(reason.tie, 'own');
  assert.deepEqual(reason.dice, []);
  assert.equal(reason.lines.at(-1), 'Same team: you chose the order, no dice.');
});

test('a creature with no place in the order has no reason', () => {
  assert.equal(orderReason(livePresentation(view()), '99'), null);
});

test('while ties are ordered, the field marks each tied creature with its place, a ? while it is still to place, and the opponent\'s fixed places', () => {
  const marks = tieMarks(view({ board: tieBoard, options: ties }), [1]);

  assert.deepEqual(Object.fromEntries(marks), {
    1: { badge: '1', state: 'placed' }, 2: { badge: '3', state: 'placed' }, 4: { badge: '2', state: 'opponent' },
    3: { badge: '?', state: 'pending' }, 6: { badge: '?', state: 'pending' }, 5: { badge: '4', state: 'opponent' },
  });
});
