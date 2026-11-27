import { test } from 'node:test';
import assert from 'node:assert/strict';
import { crestInset, mutationReference, mutationSchedule, createBattleGrowth } from './battle-growth.js';
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
  assert.ok(model.packages.every(p => p.status !== 'picked'), 'a pending own pick is never public');
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
  const growth = createBattleGrowth(), v = revealed(view());
  growth.receive(v, catalogue); assert.equal(growth.receipt(), null); assert.deepEqual(growth.highlights(action(), true), []);
  growth.reset(); growth.receive(v, catalogue); assert.equal(growth.receipt(), null);
  growth.receive(null); growth.receive(v, catalogue); assert.equal(growth.receipt(), null);
  growth.receive({ ...v, replay: true }, catalogue); assert.equal(growth.receipt(), null);
});
test('private decisions and board diffs alone do not announce or highlight an unlock', () => {
  const growth = createBattleGrowth(), v = view(); growth.receive(v, catalogue);
  v.feed = [{ sequence: 10, event: { kind: 'EvolutionChoiceSubmitted', creature: 1, tier: 'root' } }]; v.feedNext = 11;
  growth.receive(v, catalogue); assert.equal(growth.receipt(), null);
  v.board.allies[0].knownSpells.push('new'); growth.receive(v, catalogue);
  assert.deepEqual(growth.highlights(action(), true), []);
});
test('one public receipt includes both sides, but only genuinely new own spells are highlighted', () => {
  const growth = createBattleGrowth(), v = view(); growth.receive(v, catalogue); revealed(v);
  v.board.enemies[0].knownSpells.push('new'); v.feed[0] = reveal(10, [{ creature: 1, tier: 'root' }, { creature: 2, tier: 'root' }]);
  growth.receive(v, catalogue);
  assert.deepEqual(growth.receipt().rows.map(row => [row.enemy, row.spells]), [[false, ['Devour']], [true, ['Devour']]]);
  assert.deepEqual(growth.highlights(action(), true), ['new']);
  assert.deepEqual(growth.highlights(action('b', { actor: 2 }), true), []);
  growth.dismiss(); growth.receive(v, catalogue); assert.equal(growth.receipt(), null);
});
test('new badges wait for a visible spell choice, survive reopening it, then expire at the next asking', () => {
  const growth = createBattleGrowth(), v = view(); growth.receive(v, catalogue); growth.receive(revealed(v), catalogue);
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
  const growth = createBattleGrowth(), v = view(); growth.receive(v, catalogue); revealed(v);
  v.feed = [reveal(12, [{ creature: 2, tier: 'cap' }]), reveal(10), reveal(10)]; v.feedNext = 13;
  growth.receive(v, catalogue); assert.equal(growth.receipt().sequence, 12);
  growth.dismiss(); v.feed = [reveal(11)]; growth.receive(v, catalogue); assert.equal(growth.receipt(), null);
});
test('passive-only or unknown mutations have no invented spell unlocks and null clears every fact', () => {
  const growth = createBattleGrowth(), v = view(); growth.receive(v, catalogue);
  v.feed = [reveal(10, [{ creature: 1, tier: 'cap' }, { creature: 2, tier: 'unknown' }])];
  growth.receive(v, catalogue); assert.deepEqual(growth.receipt().rows.map(row => row.spells), [[], []]);
  assert.equal(growth.receipt().rows[1].mutation, 'unknown');
  growth.receive(null); assert.equal(growth.receipt(), null); assert.deepEqual(growth.highlights(action(), true), []);
});

test('the host opportunity is inclusive of this round, never an invented future date or post-match offer', () => {
  const v = view(); v.board.nextEvolutionRound = 3;
  assert.equal(mutationReference(v, catalogue).next, 3); assert.equal(mutationReference(v, catalogue).current, 3);
  v.board.nextEvolutionRound = 2; assert.equal(mutationReference(v, catalogue).next, null);
  v.board.nextEvolutionRound = 7; v.over = true;
  assert.equal(mutationReference(v, catalogue).over, true); assert.equal(mutationReference(v, catalogue).next, null);
});
// The host reads a seat's board before its feed and neither waits for the other, so a reveal can arrive with the
// board from just before the purchase, and a board can show a purchase whose reveal has not been recorded yet.
const race = (known, tiers) => ({ board: { slot: 'Player1', roundNumber: 3, allies: [{ id: 1, name: 'One', knownSpells: known, acquiredTiers: tiers }],
  enemies: [{ id: 2, name: 'Two', knownSpells: ['old'], acquiredTiers: [] }] }, feed: [], feedNext: 20 });
const raceAction = { opened: true, question: 'q', actor: 1, waitingFor: 'Action', known: ['old', 'new'], spells: ['old', 'new'] };
test('a reveal read with the board from before its purchase still announces and marks the new spell', () => {
  const growth = createBattleGrowth(); growth.receive(race(['old'], []), catalogue);
  growth.receive({ ...race(['old'], []), feed: [reveal(20)], feedNext: 21 }, catalogue);
  growth.receive({ ...race(['old', 'new'], ['root']), feedNext: 21 }, catalogue);
  assert.deepEqual(growth.receipt().rows[0].spells, ['Devour']); assert.deepEqual(growth.highlights(raceAction, true), ['new']);
});
test('a board showing a purchase before its reveal is recorded does not swallow the new spell', () => {
  const growth = createBattleGrowth(); growth.receive(race(['old'], []), catalogue);
  growth.receive({ ...race(['old', 'new'], ['root']), feedNext: 20 }, catalogue);
  growth.receive({ ...race(['old', 'new'], ['root']), feed: [reveal(20)], feedNext: 21 }, catalogue);
  assert.deepEqual(growth.receipt().rows[0].spells, ['Devour']); assert.deepEqual(growth.highlights(raceAction, true), ['new']);
});
test('a reveal row carries what its plaque and card show: family colour, stage, initiative, passives and spell costs', () => {
  const content = { cards: [{ id: 'new', name: 'Devour', cost: 2, targeting: 'One enemy' }], packages: [
    { id: 'root', name: 'Predator', level: 1, spells: ['old', 'new'], initiativeBonus: 1 },
    { id: 'next', name: 'Stalker', level: 2, prerequisites: ['root'], spells: [], initiativeBonus: 2, passiveLines: ['Bleeds last longer'] }] };
  const growth = createBattleGrowth(), v = view(); growth.receive(v, content); revealed(v);
  v.board.enemies[0].acquiredTiers = ['root', 'next'];
  v.feed[0] = reveal(10, [{ creature: 1, tier: 'root' }, { creature: 2, tier: 'next' }]);
  growth.receive(v, content);
  const [own, rival] = growth.receipt().rows;
  assert.deepEqual([own.stage, own.family, own.initiative, own.passives], [1, 'Predator', 1, []]);
  assert.deepEqual(own.details, [{ name: 'Devour', cost: 2, targeting: 'One enemy' }]);
  assert.deepEqual([rival.mutation, rival.stage, rival.family, rival.passives], ['Stalker', 2, 'Predator', ['Bleeds last longer']]);
  assert.equal(own.colour, rival.colour, 'one family, one colour');
  assert.match(own.colour, /^(#|hsl)/);
  assert.deepEqual(growth.receipt().counts, { ours: 1, theirs: 1 });
});
test('a reveal clears by itself once this seat has committed its tempos, or the round has moved on', () => {
  const at = (v, subPhase, waitingFor, speedChoices = []) => ({ ...v, waitingFor, board: { ...v.board, subPhase, speedChoices } });
  const growth = createBattleGrowth(), v = view(); growth.receive(v, catalogue); revealed(v);
  growth.receive(at(v, 'Speed', 'Speed'), catalogue); assert.ok(growth.receipt(), 'still choosing tempos');
  growth.receive(at(v, 'Speed', null), catalogue); assert.ok(growth.receipt(), 'a gap between tempo askings is not a commit');
  growth.receive(at(v, 'Speed', null, [{ creature: 1, speed: 'Standard' }]), catalogue);
  assert.equal(growth.receipt(), null, 'every own tempo given, the other seat still choosing');
  const later = createBattleGrowth(), w = view(); later.receive(w, catalogue); revealed(w);
  later.receive(at(w, 'Speed', 'Speed'), catalogue); later.receive(at(w, 'Activation', 'Action'), catalogue);
  assert.equal(later.receipt(), null, 'combat began');
  const evolving = createBattleGrowth(), x = view(); evolving.receive(x, catalogue); revealed(x);
  evolving.receive(at(x, 'Evolution', 'Evolution'), catalogue); assert.ok(evolving.receipt(), 'a reveal read during Evolution waits for the tempos');
});

// #1091: the reveal's receipt arrives rising (`.motion-arrive`, 8 px low): the crests start under where it settles,
// not under the frame's place of a receipt still on its way.
test('the crests start under where the receipt settles, its arrival\'s rise taken out', () => {
  const receipt = { bottom: 120, height: 60 }, stage = { top: 40 };
  assert.equal(crestInset(receipt, stage, 'none'), 84);
  assert.equal(crestInset(receipt, stage, '0px 8px'), 76);
  assert.equal(crestInset(receipt, stage, '0px 3.5px'), 80.5);
  // A translate along x alone moves nothing down.
  assert.equal(crestInset(receipt, stage, '4px'), 84);
});

test('no crest inset without a receipt in view or a stage to stand on', () => {
  assert.equal(crestInset({ bottom: 0, height: 0 }, { top: 40 }, 'none'), 0);
  assert.equal(crestInset({ bottom: 120, height: 60 }, undefined, 'none'), 0);
  // A receipt above the stage leaves it whole.
  assert.equal(crestInset({ bottom: 20, height: 20 }, { top: 40 }, 'none'), 0);
});

// F3 of docs/tabletop/first-playtest-plan.md: when the mutation rounds come is the host's to say. The board names the
// next one (this round included) and the one after it; the rule set names the gap and the picks.
const scheduled = (roundNumber, nextEvolutionRound, followingEvolutionRound, extras = {}) => ({ board: { roundNumber, nextEvolutionRound, followingEvolutionRound, allies: [], enemies: [] }, ...extras });
const rules = { rules: { evolutionInterval: 2, evolutionPicksPerOpportunity: 2 } };
test('the schedule says a mutation round, the round before one, and the round the next one comes', () => {
  assert.deepEqual(mutationSchedule(scheduled(3, 3, 5), rules), { round: 3, now: true, soon: false, next: 3, following: 5, interval: 2, picks: 2 });
  assert.deepEqual(mutationSchedule(scheduled(4, 5, 7), rules), { round: 4, now: false, soon: true, next: 5, following: 7, interval: 2, picks: 2 });
  const far = mutationSchedule(scheduled(2, 6, 10), rules);
  assert.equal(far.now, false); assert.equal(far.soon, false); assert.equal(far.next, 6);
});
test('the schedule names nothing the host did not serve, and nothing once the match is over or not begun', () => {
  const bare = mutationSchedule(scheduled(11, 11, null), {});
  assert.deepEqual(bare, { round: 11, now: true, soon: false, next: 11, following: null, interval: null, picks: null });
  const last = mutationSchedule(scheduled(12, null, null), rules);
  assert.equal(last.now, false); assert.equal(last.soon, false); assert.equal(last.next, null);
  for (const quiet of [null, {}, scheduled(3, 3, 5, { over: true }), scheduled(3, 3, 5, { waiting: { seats: [] } }), scheduled(undefined, 3, 5)]) {
    assert.deepEqual(mutationSchedule(quiet, rules), { round: null, now: false, soon: false, next: null, following: null, interval: 2, picks: 2 });
  }
  // A board older than its field (a deploy in between) never reads as a schedule: a past round is not "next".
  assert.equal(mutationSchedule(scheduled(4, 3, 5), rules).next, null);
  assert.equal(mutationSchedule(scheduled(4, 5, 5), rules).following, null);
  assert.equal(mutationSchedule(scheduled(4, 5, 7), { rules: { evolutionInterval: 0, evolutionPicksPerOpportunity: 1.5 } }).interval, null);
});
