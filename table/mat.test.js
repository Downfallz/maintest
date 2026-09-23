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

test('package progress uses acquired tiers and legal offers, independently of known spells', async () => {
  const { talentClasses } = await import('./mat.js');
  const catalogue = { packages: [
    { id: 'first', name: 'North', level: 1, spells: ['a', 'b'], prerequisites: [], initiativeBonus: 3 },
    { id: 'second', name: 'South', level: 2, spells: ['c'], prerequisites: ['first'], initiativeBonus: 1 },
  ] };
  const creature = { id: 1, knownSpells: ['a', 'b'], acquiredTiers: [] };
  let groups = talentClasses(catalogue, new Map(), creature, { creatures: [{ creature: 1, availableTiers: ['first'] }] });
  assert.equal(groups[0].status, 'available');
  assert.ok(groups[0].tiers[0].spells.every(spell => spell.status === 'known'));
  assert.equal(groups[1].status, 'future');
  creature.acquiredTiers = ['first'];
  groups = talentClasses(catalogue, new Map(), creature, { creatures: [{ creature: 2, availableTiers: ['second'] }] });
  assert.equal(groups[0].status, 'known');
  assert.equal(groups[1].status, 'future');
  assert.equal(talentClasses(catalogue, null, creature, null)[1].status, 'future');
  assert.deepEqual(talentClasses(null, null, null, null), []);
});

test('package hierarchy uses actual prerequisite ids and remains safe for orphaned or invalid edges', async () => {
  const { packageForest } = await import('./mat.js');
  const packages = [
    { id: 'leaf', level: 3, prerequisites: ['branch'] },
    { id: 'root', level: 1, prerequisites: [] },
    { id: 'branch', level: 2, prerequisites: ['root'] },
    { id: 'orphan', level: 2, prerequisites: ['missing'] },
    { id: 'invalid', level: 1, prerequisites: ['invalid'] },
  ];
  const roots = packageForest({ packages });
  assert.deepEqual(roots.map(pack => pack.id), ['root', 'orphan', 'invalid']);
  assert.equal(roots[0].children[0].children[0].id, 'leaf');
  assert.deepEqual(packageForest(null), []);
});

test('class accents depend on the class name and stay stable across card locations', async () => {
  const { classColour } = await import('./mat.js');
  assert.equal(classColour('North'), classColour('North'));
  assert.notEqual(classColour('North'), classColour('South'));
  assert.match(classColour('North'), /^hsl\(\d+ 48% 68%\)$/);
});

test('hierarchy follows explicit parent codes even when nodes arrive out of order and names repeat across trees', async () => {
  const { talentForest } = await import('./mat.js');
  const catalogue = { trees: [
    { tree: 'a', code: 'leaf', parentCode: 'branch', depth: 3, spells: [] },
    { tree: 'b', code: 'root', parentCode: null, depth: 1, spells: [] },
    { tree: 'a', code: 'root', parentCode: null, depth: 1, spells: [] },
    { tree: 'a', code: 'branch', parentCode: 'root', depth: 2, spells: [] },
  ] };
  const roots = talentForest(catalogue);
  assert.equal(roots.length, 2);
  assert.equal(roots[0].children.length, 0);
  assert.equal(roots[1].children[0].children[0].code, 'leaf');
  assert.equal(roots[1].children[0].children[0].depth, 3);
});

test('invalid ancestry remains disconnected instead of creating cycles or invented edges', async () => {
  const { talentForest } = await import('./mat.js');
  const roots = talentForest({ trees: [
    { tree: 'a', code: 'one', parentCode: 'two', depth: 1 },
    { tree: 'a', code: 'two', parentCode: 'one', depth: 1 },
    { tree: 'a', code: 'orphan', parentCode: 'missing', depth: 3 },
  ] });
  assert.equal(roots.length, 3); assert.ok(roots.every(one => one.children.length === 0));
});

test('each authored family has a coherent range shared by its cards and descendant nodes', async () => {
  const { talentPalette, classColour } = await import('./mat.js');
  const catalogue = { trees: [
    { tree: 'a', code: 'root', parentCode: null, depth: 1 },
    ...['cool', 'leaf', 'ember'].flatMap((family, index) => [
      { tree: 'a', code: family, parentCode: 'root', depth: 2, spells: [family] },
      { tree: 'a', code: `${family}-child`, parentCode: family, depth: 3, spells: [`child-${index}`] },
    ]),
  ] };
  const cards = new Map(['cool', 'leaf', 'ember', 'child-0', 'child-1', 'child-2'].map(id => [id, { creatureClass: id }]));
  const palette = talentPalette(catalogue, cards);
  assert.equal(classColour('cool', palette), '#89b8ee');
  assert.equal(classColour('leaf', palette), '#b6cb78');
  assert.equal(classColour('ember', palette), '#e5a36f');
  assert.equal(classColour('child-0', palette), palette.get('a/cool-child'));
  assert.equal(classColour('child-2', palette), '#e68573');
});
