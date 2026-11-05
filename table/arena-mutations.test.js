import test from 'node:test';
import assert from 'node:assert/strict';
import { mutationChoices } from './arena-mutations.js';
const catalogue = { packages: [
  { id: 'root', name: 'Brute', level: 1, spells: ['strike'], initiativeBonus: 0, tags: ['Guard', 'Pressure'] },
  { id: 'branch', name: 'Ironhide', level: 2, prerequisites: ['root'], spells: ['ward'], initiativeBonus: 2 },
  { id: 'third', name: 'Colossus', level: 3, prerequisites: ['branch'], spells: ['ward'], initiativeBonus: 1 },
  { id: 'cap', name: 'Capstone', level: 4, anyOf: ['third'], spells: [], initiativeBonus: 0, passiveLines: ['Defense +2', 'For every creature of the side'] },
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
  assert.deepEqual(cap.passives, ['Defense +2', 'For every creature of the side']);
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
