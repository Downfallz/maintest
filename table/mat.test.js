import { test } from 'node:test';
import assert from 'node:assert/strict';
import { drawn, matBands } from './mat.js';

const catalogue = {
  trees: [
    { treeName: 'Base', name: 'Base Creature', depth: 1, spells: ['spell:a:v1', 'spell:b:v1'] },
    { treeName: 'Base', name: 'Brawler', depth: 2, spells: ['spell:c:v1'] },
    { treeName: 'Base', name: 'Empty', depth: 2, spells: [] },
  ],
};

const cards = new Map([
  ['spell:a:v1', { id: 'spell:a:v1', name: 'Alpha' }],
  ['spell:b:v1', { id: 'spell:b:v1', name: 'Beta' }],
]);

const creatures = [
  { id: 1, knownSpells: ['spell:a:v1'] },
  { id: 2, knownSpells: ['spell:a:v1', 'spell:c:v1'] },
];

test('every band of the tree is a row, with a pip per creature', () => {
  const rows = matBands(catalogue, creatures, cards);

  assert.deepEqual(rows.map(row => row.name), ['Base Creature', 'Brawler', 'Empty']);
  assert.deepEqual(rows[0].spells[0].pips, [{ creature: 1, known: true }, { creature: 2, known: true }]);
  assert.deepEqual(rows[1].spells[0].pips, [{ creature: 1, known: false }, { creature: 2, known: true }]);
});

// The mat is read while picking an unlock, so a spell has to be readable as the card names it.
test('a spell is named as its card names it, and as its id when the catalogue has no card', () => {
  const rows = matBands(catalogue, creatures, cards);

  assert.equal(rows[0].spells[0].name, 'Alpha');
  assert.equal(rows[1].spells[0].name, 'spell:c:v1');
});

test('a band with nothing in it is not drawn', () => {
  assert.deepEqual(drawn(matBands(catalogue, creatures, cards)).map(row => row.name), ['Base Creature', 'Brawler']);
});

test('a page with no catalogue draws no mat rather than failing', () => {
  assert.deepEqual(matBands(undefined, creatures, cards), []);
  assert.deepEqual(matBands({}, undefined, undefined), []);
  assert.deepEqual(drawn(undefined), []);
});

// The gate is the only place a player can read why a node is out of reach: the decision sheet offers what is
// unlocked and says nothing about what is not (playtest-app.md §3.1).
test('a band carries the gate each of its spells is behind', () => {
  const catalogue = { trees: [{ treeName: 'Tree', name: 'Node', depth: 2, spells: ['spell:a:v1', 'spell:b:v1'] }] };
  const cards = new Map([
    ['spell:a:v1', { name: 'A', requires: 'B' }],
    ['spell:b:v1', { name: 'B' }],
  ]);

  const bands = matBands(catalogue, [{ id: 1, knownSpells: ['spell:b:v1'] }], cards);

  assert.deepEqual(bands[0].spells.map(spell => [spell.name, spell.requires]), [['A', 'B'], ['B', '']]);
});

test('a spell the catalogue has no card for carries no gate rather than an undefined one', () => {
  const catalogue = { trees: [{ treeName: 'Tree', name: 'Node', depth: 1, spells: ['spell:x:v1'] }] };

  assert.deepEqual(matBands(catalogue, [], new Map())[0].spells.map(spell => spell.requires), ['']);
});
