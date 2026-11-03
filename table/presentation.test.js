import { test } from 'node:test';
import assert from 'node:assert/strict';
import { activeTurn, boardPresentation, decisionView, presentedView } from './presentation.js';
import { askedKey } from './slot.js';

const strike = { actor: 1, spell: 'strike', legalTargets: { minTargets: 1, maxTargets: 1, candidates: [2] } };
const guard = { actor: 1, spell: 'guard', legalTargets: { minTargets: 1, maxTargets: 1, candidates: [1] } };
const creature = (id, health, energy = 4) => ({ id, health, maxHealth: 30, energy, totalDefense: 2,
  currentInitiative: id === 1 ? 20 : 8, conditions: [], knownSpells: ['strike', 'guard'] });
const timeline = [{ creature: 2, owner: 'player2', speed: 'Quick', initiative: 8 },
  { creature: 1, owner: 'player1', speed: 'Standard', initiative: 20 }];
const board = { slot: 'player1', roundNumber: 4, phase: 'Combat', subPhase: 'Activation', activationCursor: 1,
  allies: [creature(1, 18)], enemies: [creature(2, 0)], timeline, rollOffs: [{ creature: 1, rolls: [3, 17] }],
  intents: [{ actor: 1, spell: 'private-intent' }], speedChoices: [], evolutionChoices: [] };
const latest = { board, waitingFor: 'Action', waitingAsked: 7, waitingCreature: 1,
  options: { action: { actor: 1, spells: [strike, guard] } },
  roundEvents: [10, 13, 18, 25].map(sequence => ({ sequence })) };
const draft = { asked: askedKey('player1', latest), chosen: 'guard' };
const action = { sequence: 18, round: 3, actor: { id: 2 }, action: { actor: 2, spell: 'strike', targets: [1] },
  frame: { before: [creature(1, 25, 2), creature(2, 12)], after: [creature(1, 18, 2), creature(2, 12)],
    timeline, rollOffs: [] } };
const replay = { actions: [action], index: 0, stage: 'after', round: 3 };

function freeze(value) {
  if (value && typeof value === 'object') {
    Object.values(value).forEach(freeze);
    Object.freeze(value);
  }
  return value;
}
freeze(latest);
freeze(replay);

test('the same asking keeps its spell through polls and switching between Action screens', () => {
  const first = decisionView('player1', latest);
  assert.equal(first.waitingFor, 'Intent');
  assert.equal(askedKey('player1', first), draft.asked);
  const selected = decisionView('player1', structuredClone(latest), draft);
  assert.equal(selected.waitingFor, 'Target');
  assert.deepEqual(selected.options.target, guard);
  assert.equal(askedKey('player1', selected), draft.asked);
  assert.equal(decisionView('player1', latest, { ...draft, chosen: null }).waitingFor, 'Intent');
  assert.equal(latest.waitingFor, 'Action');
  assert.equal(latest.options.target, undefined);
});

test('a new asking, creature or seat cannot inherit the previous spell', () => {
  for (const [seat, view] of [
    ['player1', { ...latest, waitingAsked: 8 }],
    ['player1', { ...latest, waitingCreature: 3 }],
    ['player2', latest],
  ]) {
    assert.equal(decisionView(seat, view, draft).waitingFor, 'Intent');
  }
});

test('a spell no longer offered by the host returns to spell selection', () => {
  const changed = { ...latest, options: { action: { actor: 1, spells: [strike] } } };
  assert.equal(decisionView('player1', changed, draft).waitingFor, 'Intent');
});

test('an accepted Action stays answered until a different asking arrives, only for its own seat', () => {
  const pendingPoll = { ...draft, answered: 'player1/7' };
  const answer = decisionView('player1', latest, pendingPoll);
  assert.equal(answer.waitingFor, null);
  assert.equal(answer.waitingCreature, null);
  assert.equal(decisionView('player1', { ...latest, waitingAsked: 8 }, pendingPoll).waitingFor, 'Intent');
  assert.equal(decisionView('player2', latest, pendingPoll).waitingFor, 'Intent');
  assert.equal(latest.waitingFor, 'Action');
});

test('hidden-intent and planning questions keep the host options without synthesizing legality', () => {
  for (const kind of ['Evolution', 'Speed', 'TieOrder', 'Intent', 'Target', null]) {
    const view = { ...latest, waitingFor: kind };
    assert.equal(decisionView('player1', view, draft), view);
  }
  assert.equal(decisionView('player1', null), null);
});

test('without playback, presentation returns the current view unchanged', () => {
  assert.equal(presentedView(latest), latest);
});

test('a historical frame never replaces current resources or carries a playable question', () => {
  const decision = decisionView('player1', latest, draft);
  const displayed = presentedView(decision, replay);
  assert.equal(displayed.board.roundNumber, 3);
  assert.equal(displayed.board.allies[0].energy, 2);
  assert.equal(displayed.board.enemies[0].health, 12);
  assert.equal(displayed.waitingFor, null);
  assert.equal(displayed.waitingCreature, null);
  assert.equal(displayed.waitingAsked, null);
  assert.equal(displayed.askedAs, null);
  assert.deepEqual(displayed.options, {});
  assert.deepEqual(displayed.board.intents, []);
  assert.equal(latest.board.roundNumber, 4);
  assert.equal(latest.board.allies[0].energy, 4);
  assert.equal(decision.waitingFor, 'Target');
  assert.equal(presentedView(decision), decision); // Skip returns to the current decision, not the frame.
});

test('before and after use the exact frame and include only public results already displayed', () => {
  const before = presentedView(latest, { ...replay, stage: 'before' });
  const after = presentedView(latest, replay);
  assert.equal(before.board.allies[0].health, 25);
  assert.equal(after.board.allies[0].health, 18);
  assert.deepEqual(before.roundEvents.map(one => one.sequence), [10, 13]);
  assert.deepEqual(after.roundEvents.map(one => one.sequence), [10, 13, 18]);
  assert.deepEqual(after.board.revealedActions, [action.action]);
});

test('a match replay reads its own history, preserving non-contiguous public sequence numbers', () => {
  const history = freeze([{ sequence: 2 }, { sequence: 18 }, { sequence: 21 }]);
  assert.deepEqual(presentedView(latest, replay, history).roundEvents, history.slice(0, 2));
  assert.deepEqual(presentedView(latest, replay, []).roundEvents, []);
});

test('a legacy recording with no frame shows current totals and still offers no decision', () => {
  const legacy = { ...replay, actions: [{ ...action, frame: null }] };
  const displayed = presentedView(latest, legacy);
  assert.equal(displayed.board, latest.board);
  assert.equal(displayed.waitingFor, null);
  assert.deepEqual(displayed.options, {});
});

test('a player-two frame keeps the perspective of that seat', () => {
  const second = { ...latest, board: { ...board, slot: 'player2', allies: board.enemies, enemies: board.allies } };
  const display = boardPresentation(presentedView(second, replay).board);
  assert.deepEqual(display.allies.map(one => one.id), [2]);
  assert.deepEqual(display.enemies.map(one => one.id), [1]);
  assert.deepEqual(display.timeline.map(one => one.side), ['ally', 'enemy']);
});

test('upkeep and cleanup frames do not expose an active combat turn', () => {
  for (const interlude of ['upkeep', 'purchases', 'aftermath']) {
    const historical = { ...replay, actions: [{ ...action, interlude }] };
    const displayed = boardPresentation(presentedView(latest, historical).board);
    assert.equal(displayed.turn, null);
    assert.equal(displayed.cursor, -1);
  }
});

test('board presentation preserves host order, tie rolls and the activation cursor rather than sorting initiative', () => {
  const display = boardPresentation(board);
  assert.deepEqual(display.timeline.map(slot => slot.creature), [2, 1]);
  assert.deepEqual(display.timeline.map(slot => slot.position), [1, 2]);
  assert.deepEqual(display.timeline.map(slot => slot.side), ['enemy', 'ally']);
  assert.deepEqual(display.timeline.map(slot => slot.isNow), [false, true]);
  assert.equal(display.timeline[1].roll, 'd20 3 → 17');
  assert.deepEqual(display.turn, { slot: timeline[1], position: 2, total: 2 });
  assert.equal(display.round, 4);
  assert.equal(display.phase, 'Combat');
  assert.equal(display.subPhase, 'Activation');
});

test('creature resources, defeat and conditions are passed through without reconstruction', () => {
  const conditions = [{ effect: { kind: 'Bleed', amountPerRound: 2 }, remainingRounds: 3, isFresh: true }];
  const ally = freeze({ ...creature(1, 18), isStunned: true, isStunImmune: false, conditions });
  const view = boardPresentation({ ...board, allies: [ally] });
  assert.equal(view.allies[0], ally);
  assert.equal(view.allies[0].conditions, conditions);
  assert.equal(view.enemies[0].health, 0);
  assert.equal(view.enemies.length, 1);
});

test('public board mapping never consults hidden plans or invents enemy spells', () => {
  const source = { ...board, enemies: [{ id: 2, health: 12 }] };
  for (const key of ['intents', 'speedChoices', 'evolutionChoices']) {
    Object.defineProperty(source, key, { get() { throw new Error(`private ${key} read`); } });
  }
  const display = boardPresentation(source);
  assert.equal(display.enemies[0].knownSpells, undefined);
  assert.equal(display.intents, undefined);
});

test('planning, finished cursors and empty boards have no active turn', () => {
  for (const value of [null, {}, { ...board, subPhase: 'IntentSelection' }, { ...board, activationCursor: 2 }]) {
    assert.equal(activeTurn(value), null);
    assert.equal(boardPresentation(value).timeline.some(slot => slot.isNow), false);
  }
  assert.deepEqual(boardPresentation(null).allies, []);
  assert.deepEqual(boardPresentation(null).enemies, []);
});
