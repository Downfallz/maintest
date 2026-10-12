import { test } from 'node:test';
import assert from 'node:assert/strict';
import { actionDecision, askedKey, slotView } from './slot.js';

const strike = { actor: 1, spell: 'spell:strike:v1', legalTargets: { minTargets: 1, maxTargets: 1, candidates: [3, 4] } };
const guard = { actor: 1, spell: 'spell:guard:v1', legalTargets: { minTargets: 1, maxTargets: 1, candidates: [1] } };
const asked = { waitingFor: 'Action', waitingCreature: 1, waitingAsked: 7, options: { kind: 'Action', action: { actor: 1, spells: [strike, guard] } } };

test('an action with no spell chosen yet is drawn as the spell screen, every spell on offer castable', () => {
  const view = slotView(asked);

  assert.equal(view.waitingFor, 'Intent');
  assert.equal(view.askedAs, 'Action');
  assert.deepEqual(view.options.intent.creatures, [{ creature: 1, castableSpells: ['spell:strike:v1', 'spell:guard:v1'], unaffordableSpells: [] }]);
});

test('an action with a spell chosen is drawn as that spell\'s target screen', () => {
  const view = slotView(asked, 'spell:guard:v1');

  assert.equal(view.waitingFor, 'Target');
  assert.equal(view.askedAs, 'Action');
  assert.deepEqual(view.options.target, guard);
});

test('a spell the action does not offer leaves it on the spell screen', () => {
  assert.equal(slotView(asked, 'spell:meteor:v1').waitingFor, 'Intent');
});

test('any other question is drawn as the host asked it', () => {
  const intent = { waitingFor: 'Intent', waitingCreature: 1, options: { intent: { creatures: [] } } };

  assert.equal(slotView(intent, 'spell:strike:v1'), intent);
  assert.equal(slotView(null), null);
});

test('choosing a spell is the same asking, so the choice survives the change of screen', () => {
  assert.equal(askedKey('player1', slotView(asked)), askedKey('player1', slotView(asked, 'spell:strike:v1')));
  assert.notEqual(askedKey('player1', slotView(asked)), askedKey('player1', { ...asked, waitingAsked: 8 }));
  assert.equal(askedKey('player1', { waitingFor: 'Speed', waitingCreature: 2, waitingAsked: 3 }), 'player1/3/Speed/2');
});

test('the action posted names the creature, the spell on the target screen and the targets picked', () => {
  assert.deepEqual(actionDecision(slotView(asked, 'spell:strike:v1'), [4]), { kind: 'Action', creature: 1, spell: 'spell:strike:v1', targets: [4] });
});
