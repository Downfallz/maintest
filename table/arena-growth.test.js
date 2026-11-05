import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mutationReference, createArenaGrowth } from './arena-growth.js';
const catalogue = { cards: [{ id: 'new', name: 'Devour' }], packages: [
  { id: 'root', name: 'Predator', level: 1, spells: ['old', 'new'] },
  { id: 'other', name: 'Brute', level: 1, spells: [] },
  { id: 'next', name: 'Upgrade', level: 2, prerequisites: ['root'], spells: [] },
  { id: 'cap', name: 'Shared', level: 4, anyOf: ['next'], spells: [], passiveLines: ['All allied creatures gain defense'] },
] };
const view = () => ({ board: { slot: 'Player1', roundNumber: 3, nextEvolutionRound: 7,
  allies: [{ id: 1, name: 'One', knownSpells: ['old'], acquiredTiers: [] }],
  enemies: [{ id: 2, name: 'Two', knownSpells: ['old'], acquiredTiers: [] }], evolutionChoices: [{ creature: 1, tier: 'root' }] },
  waitingFor: 'Evolution', options: { evolution: { creatures: [{ creature: 1, availableTiers: ['other'] }] } }, feed: [], feedNext: 10 });
const reveal = (sequence = 10, choices = [{ creature: 1, tier: 'root' }]) => ({ sequence, round: 3, event: { kind: 'PurchasesRevealed', roundId: 3, choices } });
function revealed(v) { v.board.allies[0].knownSpells.push('new'); v.board.allies[0].acquiredTiers.push('root'); v.feed = [reveal()]; v.feedNext = 11; return v; }
const action = (question = 'a', extras = {}) => ({ opened: true, question, actor: 1, waitingFor: 'Action', known: ['old', 'new'], spells: ['old', 'new'], ...extras });
test('reference uses public ownership and host offers, never pending picks or spell equivalence', () => {
  const v = view(), model = mutationReference(v, catalogue, 1);
  assert.equal(model.packages.find(p => p.id === 'root').status, 'future');
  assert.equal(model.packages.find(p => p.id === 'other').status, 'available');
  assert.equal(model.next, 7); assert.deepEqual(model.families, []);
  const enemy = mutationReference(v, catalogue, '2');
  assert.ok(enemy.packages.every(p => p.status === 'future'));
  assert.equal(JSON.stringify(model).includes('evolutionChoices'), false);
  v.waitingFor = 'Action'; assert.ok(mutationReference(v, catalogue).packages.every(p => p.status === 'future'));
  delete v.board.nextEvolutionRound; assert.equal(mutationReference(v, catalogue).next, null);
});
test('multiclass progress and shared passive wording follow authored acquired packages', () => {
  const v = view(); v.board.allies[0].acquiredTiers = ['root', 'other', 'next', 'cap'];
  const model = mutationReference(v, catalogue, 1);
  assert.deepEqual(model.families, [{ name: 'Predator', stage: 4 }, { name: 'Brute', stage: 1 }]);
  assert.deepEqual(model.packages.find(p => p.id === 'cap').passiveLines, ['All allied creatures gain defense']);
  assert.equal(mutationReference(null, catalogue).available, false);
});
test('first read, reload, replay and hidden-tab return do not announce old reveals', () => {
  const growth = createArenaGrowth(), v = revealed(view());
  growth.receive(v, catalogue); assert.equal(growth.receipt(), null); assert.deepEqual(growth.highlights(action(), true), []);
  growth.reset(); growth.receive(v, catalogue); assert.equal(growth.receipt(), null);
  growth.receive(null); growth.receive(v, catalogue); assert.equal(growth.receipt(), null);
  growth.receive({ ...v, replay: true }, catalogue); assert.equal(growth.receipt(), null);
});
test('private decisions and board diffs alone do not announce or highlight an unlock', () => {
  const growth = createArenaGrowth(), v = view(); growth.receive(v, catalogue);
  v.feed = [{ sequence: 10, event: { kind: 'EvolutionChoiceSubmitted', creature: 1, tier: 'root' } }]; v.feedNext = 11;
  growth.receive(v, catalogue); assert.equal(growth.receipt(), null);
  v.board.allies[0].knownSpells.push('new'); growth.receive(v, catalogue);
  assert.deepEqual(growth.highlights(action(), true), []);
});
test('one public receipt includes both sides, but only genuinely new own spells are highlighted', () => {
  const growth = createArenaGrowth(), v = view(); growth.receive(v, catalogue); revealed(v);
  v.board.enemies[0].knownSpells.push('new'); v.feed[0] = reveal(10, [{ creature: 1, tier: 'root' }, { creature: 2, tier: 'root' }]);
  growth.receive(v, catalogue);
  assert.deepEqual(growth.receipt().rows.map(row => [row.enemy, row.spells]), [[false, ['Devour']], [true, ['Devour']]]);
  assert.deepEqual(growth.highlights(action(), true), ['new']);
  assert.deepEqual(growth.highlights(action('b', { actor: 2 }), true), []);
  growth.dismiss(); growth.receive(v, catalogue); assert.equal(growth.receipt(), null);
});
test('new badges wait for a visible spell choice, survive reopening it, then expire at the next asking', () => {
  const growth = createArenaGrowth(), v = view(); growth.receive(v, catalogue); growth.receive(revealed(v), catalogue);
  assert.deepEqual(growth.highlights(action(), false), []);
  assert.deepEqual(growth.highlights(action('b', { opened: false }), true), []);
  assert.deepEqual(growth.highlights(action('c', { target: {} }), true), []);
  assert.deepEqual(growth.highlights(action('d', { waitingFor: 'Target' }), true), []);
  assert.deepEqual(growth.highlights(action('e'), true), ['new']);
  assert.deepEqual(growth.highlights(action('e', { opened: false }), true), []);
  assert.deepEqual(growth.highlights(action('e'), true), ['new']);
  assert.deepEqual(growth.highlights(action('f'), true), []);
});
test('duplicate and out-of-order reveals do not replay receipts; catch-up retains the latest', () => {
  const growth = createArenaGrowth(), v = view(); growth.receive(v, catalogue); revealed(v);
  v.feed = [reveal(12, [{ creature: 2, tier: 'cap' }]), reveal(10), reveal(10)]; v.feedNext = 13;
  growth.receive(v, catalogue); assert.equal(growth.receipt().sequence, 12);
  growth.dismiss(); v.feed = [reveal(11)]; growth.receive(v, catalogue); assert.equal(growth.receipt(), null);
});
test('passive-only or unknown mutations have no invented spell unlocks and null clears every fact', () => {
  const growth = createArenaGrowth(), v = view(); growth.receive(v, catalogue);
  v.feed = [reveal(10, [{ creature: 1, tier: 'cap' }, { creature: 2, tier: 'unknown' }])];
  growth.receive(v, catalogue); assert.deepEqual(growth.receipt().rows.map(row => row.spells), [[], []]);
  assert.equal(growth.receipt().rows[1].mutation, 'unknown');
  growth.receive(null); assert.equal(growth.receipt(), null); assert.deepEqual(growth.highlights(action(), true), []);
});
