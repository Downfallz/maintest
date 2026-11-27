import test from 'node:test';
import assert from 'node:assert/strict';
import { mutationChoices, pickPlan } from './battle-mutations.js';
const catalogue = { packages: [
  { id: 'root', name: 'Brute', level: 1, spells: ['strike'], initiativeBonus: 0, tags: ['Guard', 'Pressure'] },
  { id: 'branch', name: 'Ironhide', level: 2, prerequisites: ['root'], spells: ['ward'], initiativeBonus: 2 },
  { id: 'third', name: 'Colossus', level: 3, prerequisites: ['branch'], spells: ['ward'], initiativeBonus: 1 },
  { id: 'cap', name: 'Capstone', level: 4, anyOf: ['third'], spells: [], initiativeBonus: 0, passiveLines: ['Defense +2', 'For every creature of the team'] },
  { id: 'other', name: 'Seer', level: 1, spells: [], initiativeBonus: 1 },
], cards: [{ id: 'strike', name: 'Strike', cost: 0, targeting: 'One enemy', effects: ['Deal 3 damage'], critical: '0%' }] };
const view = { board: { allies: [{ id: 1, name: 'One', isAlive: true, acquiredTiers: ['root'], knownSpells: ['ward'] },
  { id: 2, name: 'Two', isAlive: false }, { id: 3, name: 'Three', isAlive: true }], evolutionChoices: [{ creature: 3, tier: 'root' }] },
  options: { evolution: { remainingPicks: 1, creatures: [{ creature: 1, availableTiers: ['branch', 'cap', 'missing'] }] } } };
test('only host-offered mutations become cards; current spells cannot imply ownership', () => {
  const choices = mutationChoices(view, catalogue);
  assert.deepEqual(choices.faces.map(one => one.id), ['branch', 'cap', 'missing']);
  assert.equal(choices.packages.find(one => one.id === 'root').status, 'known');
  assert.equal(choices.packages.find(one => one.id === 'third').status, 'future');
  assert.equal(choices.selected, 1); assert.equal(choices.faces[0].spells[0].missing, true);
});
test('the budget is the one the host sends, already capped, with defeated and picked contexts', () => {
  const choices = mutationChoices(view, catalogue);
  assert.equal(choices.remaining, 1); assert.equal(mutationChoices({ ...view, options: {} }, catalogue).remaining, null);
  assert.equal(choices.roster[1].context, 'Defeated'); assert.match(choices.roster[2].context, /Picked/);
  const other = mutationChoices(view, catalogue, 3);
  assert.equal(other.selected, 3); assert.equal(other.faces.length, 0);
  assert.equal(other.packages.find(one => one.id === 'root').status, 'picked');
});
test('capstones inherit family colour and preserve authored shared passive text without invented spells or tags', () => {
  const choices = mutationChoices(view, catalogue), [branch, cap] = choices.faces;
  assert.equal(cap.family, 'Brute'); assert.equal(cap.colour, branch.colour);
  const seer = mutationChoices({ ...view, options: { evolution: { remainingPicks: 1, creatures: [{ creature: 1, availableTiers: ['other'] }] } } }, catalogue).faces[0];
  assert.equal(seer.family, 'Seer'); assert.notEqual(seer.colour, cap.colour);
  assert.deepEqual(cap.tags, []); assert.deepEqual(cap.spells, []);
  assert.deepEqual(cap.passives, ['Defense +2', 'For every creature of the team']);
  assert.match(cap.requirement, /Requires one of: Colossus/); assert.equal(cap.initiative, 0);
});
test('missing catalogue details remain playable by id without invented stats', () => {
  const choices = mutationChoices(view, null);
  assert.deepEqual(choices.faces.map(one => one.name), ['branch', 'cap', 'missing']);
  assert.equal(choices.faces[0].initiative, null); assert.equal(choices.faces[0].stage, null); assert.deepEqual(choices.faces[0].tags, []);
});
test('opening cards retain authored tags, a zero cost and exact spell details', () => {
  const choices = mutationChoices({ ...view, options: { evolution: { remainingPicks: 1, creatures: [{ creature: 1, availableTiers: ['root'] }] } } }, catalogue);
  const face = choices.faces[0]; assert.deepEqual(face.tags, ['Guard', 'Pressure']);
  assert.equal(face.spells[0].cost, 0); assert.equal(face.spells[0].preview, 'Deal 3 damage');
  assert.deepEqual(face.spells[0].lines, ['One enemy', 'Deal 3 damage']);
});
test('the selected recipient survives changed offers and a new roster defaults to an offered creature', () => {
  // The offer moves to creature 3: the recipient shown stays creature 1, with nothing offered, rather than switching silently.
  const moved = { ...view, board: { ...view.board, evolutionChoices: [] }, options: { evolution: { remainingPicks: 1, creatures: [{ creature: 3, availableTiers: ['branch'] }] } } };
  assert.equal(mutationChoices(moved, catalogue, 1).selected, 1); assert.deepEqual(mutationChoices(moved, catalogue, 1).faces, []);
  assert.equal(mutationChoices(moved, catalogue).selected, 3);
  assert.equal(mutationChoices(view, catalogue, 2).selected, 2);
  assert.equal(mutationChoices(view, catalogue, 99).selected, 1);
  const empty = mutationChoices({ board: { allies: [] }, options: { evolution: { remainingPicks: 0 } } }, catalogue);
  assert.equal(empty.selected, null); assert.equal(empty.remaining, 0); assert.deepEqual(empty.faces, []);
  assert.equal(mutationChoices(null, null).remaining, null);
});

// Three living creatures, two team picks, nothing picked yet: the first Mutation phase of a 3v3.
const fresh = { board: { allies: [1, 2, 3].map(id => ({ id, name: `C${id}`, isAlive: true })), evolutionChoices: [] },
  options: { evolution: { remainingPicks: 2, creatures: [1, 2, 3].map(creature => ({ creature, availableTiers: ['root'] })) } } };
test('fewer picks than creatures that could mutate says the player chooses which ones mutate now', () => {
  const plan = pickPlan(fresh);
  assert.deepEqual([plan.spent, plan.left, plan.open], [0, 2, 3]);
  assert.equal(plan.rule, '3 creatures can mutate: choose which 2 mutate now.');
});
test('the pick plan links what comes in later Mutation phases to the rulebook whenever a pick is left (#1022)', () => {
  assert.equal(pickPlan(fresh).rules, '/rules#evolution');
  assert.doesNotMatch(pickPlan(fresh).rule, /later/i);
  assert.equal(pickPlan(view).rules, '/rules#evolution', 'a pick left with no sentence still reaches the rulebook');
  assert.equal(pickPlan(fresh, { pass: true }).rules, null);
});
test('a sent pick counts its creature as picked and the budget as one less before the host restates it', () => {
  const plan = pickPlan(fresh, { creature: 1, tier: 'root' }, 2);
  assert.deepEqual([plan.spent, plan.left, plan.open], [1, 1, 2]);
  assert.equal(plan.full, 'A mutation round comes every 2 rounds. Pick sent · 1 pick left.');
  assert.equal(plan.short, 'Pick sent · 1 pick left');
  assert.equal(plan.rule, '2 creatures can mutate: choose which one mutates now.');
  const last = pickPlan(view, { creature: 2, tier: 'root' }, 2);
  assert.deepEqual([last.left, last.full, last.short], [0, 'Pick sent.', 'Pick sent']);
  assert.equal(pickPlan(fresh, { pass: true }, 2).left, 0);
  assert.equal(pickPlan(fresh, { pass: true }, 2).full, 'Pick sent.');
});
test('recorded picks, defeated creatures and creatures without offers are not open for a pick', () => {
  const plan = pickPlan(view);
  assert.deepEqual([plan.spent, plan.left, plan.open], [1, 1, 1]);
  assert.equal(plan.rule, '');
});
test('as many picks as open creatures gives no rule, and no budget gives no plan', () => {
  const two = { ...fresh, board: { ...fresh.board, allies: fresh.board.allies.slice(0, 2) } };
  assert.equal(pickPlan(two).rule, '', 'the first line says each pick goes to a different creature');
  assert.equal(pickPlan({ ...fresh, options: {} }), null);
  assert.equal(pickPlan({ ...fresh, options: { evolution: { remainingPicks: 0, creatures: [] } } }).rule, '');
});
// F3 of docs/tabletop/first-playtest-plan.md: the pick's first line says how often a mutation round comes and the
// team's picks, in a full form and a short one (docs/art/journeys/windows.md, `mutation-pick`). The gap is the rule
// set's and the count the offer's: a team of one reads 1 pick, never the rule set's 2.
test('the pick opens on the schedule and the team\'s picks, each for a different creature', () => {
  const plan = pickPlan(fresh, null, 2);
  assert.equal(plan.full, 'A mutation round comes every 2 rounds. Your team has 2 picks, each for a different creature.');
  assert.equal(plan.short, 'Mutation round every 2 rounds · 2 picks left');
  const one = { ...fresh, options: { evolution: { remainingPicks: 1, creatures: fresh.options.evolution.creatures } } };
  assert.equal(pickPlan(one, null, 2).full, 'A mutation round comes every 2 rounds. Your team has 1 pick.');
  assert.equal(pickPlan(one, null, 2).short, 'Mutation round every 2 rounds · 1 pick left');
});
test('the next asking after a pick says what is left, and a rule set of every round says so', () => {
  const plan = pickPlan(view, null, 2);
  assert.equal(plan.spent, 1);
  assert.equal(plan.full, 'A mutation round comes every 2 rounds. 1 pick left.');
  assert.equal(plan.short, 'Mutation round every 2 rounds · 1 pick left');
  assert.equal(pickPlan(fresh, null, 1).full, 'A mutation round comes every round. Your team has 2 picks, each for a different creature.');
  assert.equal(pickPlan(fresh, null, 1).short, 'Mutation round every round · 2 picks left');
});
test('with no gap served the schedule is left out and the count stands alone', () => {
  for (const gap of [undefined, null, 0, -2, 1.5, '2']) {
    const plan = pickPlan(fresh, null, gap);
    assert.equal(plan.full, 'Your team has 2 picks, each for a different creature.', String(gap));
    assert.equal(plan.short, '2 picks left', String(gap));
  }
  assert.equal(pickPlan(fresh, { creature: 1, tier: 'root' }).full, 'Pick sent · 1 pick left.');
  const none = pickPlan({ ...fresh, options: { evolution: { remainingPicks: 0, creatures: [] } } }, null, 2);
  assert.equal(none.full, 'A mutation round comes every 2 rounds. 0 picks left.');
  assert.equal(none.short, 'Mutation round every 2 rounds · 0 picks left');
});
