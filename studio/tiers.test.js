import { test } from 'node:test';
import assert from 'node:assert/strict';
import { startersOverlapping, tierNamed, tierWarnings, tiersBehind, tiersTeaching } from './tiers.js';

// Packages as the catalogue lists them: an id, a name and the authored document under it.
const tier = (id, level, prerequisites = [], spells = ['spell:guard:v1']) =>
  ({ id, name: id, path: `Tiers/${id}.json`, document: { id, level, prerequisites, spells, initiativeBonus: 1 } });

const OPENER = tier('tier:brute:v1', 1);
const ADVANCED = tier('tier:marauder:v1', 2, ['tier:brute:v1'], ['spell:slam:v1']);
const CATALOGUE = [OPENER, ADVANCED];

test('a package that teaches nothing is refused, because a pick would buy nothing', () => {
  assert.deepEqual(tierWarnings({ id: 'tier:empty:v1', level: 1, spells: [] }, CATALOGUE), [
    'A package has to teach at least one spell: a pick that buys nothing is refused.',
  ]);
  assert.deepEqual(tierWarnings({ id: 'tier:blank:v1', level: 1, spells: ['', null] }, CATALOGUE), [
    'A package has to teach at least one spell: a pick that buys nothing is refused.',
  ]);
  assert.deepEqual(tierWarnings(OPENER.document, CATALOGUE), []);
});

// The rule that makes a level mean what it is priced and paced at: a level-3 package reached through one
// level-1 prerequisite is bought with two picks rather than three.
test('a package above level one has to follow the level below it', () => {
  const jumped = { id: 'tier:jumped:v1', level: 3, prerequisites: ['tier:brute:v1'], spells: ['spell:slam:v1'] };

  assert.deepEqual(tierWarnings(jumped, CATALOGUE), [
    'A package at level 3 needs a prerequisite at level 2: a family is climbed one level at a time.',
  ]);
  assert.deepEqual(tierWarnings(ADVANCED.document, CATALOGUE), []);
});

test('a prerequisite level with or below what it opens cannot be reached first', () => {
  const sideways = { id: 'tier:sideways:v1', level: 2, prerequisites: ['tier:marauder:v1'], spells: ['spell:slam:v1'] };

  assert.deepEqual(tierWarnings(sideways, CATALOGUE), [
    'A package at level 2 needs a prerequisite at level 1: a family is climbed one level at a time.',
    'A prerequisite has to sit above what it opens, so its level has to be lower than this one.',
  ]);
});

test('a package cannot require itself', () => {
  const loop = { id: 'tier:brute:v1', level: 1, prerequisites: ['tier:brute:v1'], spells: ['spell:guard:v1'] };

  assert.ok(tierWarnings(loop, CATALOGUE).includes('A package cannot require itself.'));
});

// A prerequisite half-typed is not a level, and the level rule is answered only once every one of them
// resolves -- the same order GameResources uses, so one mistake reads as one problem rather than two.
test('a prerequisite the catalogue does not carry is left to the builder rather than warned about twice', () => {
  const typing = { id: 'tier:new:v1', level: 2, prerequisites: ['tier:brute:v1', 'tier:typ'], spells: ['spell:slam:v1'] };

  assert.deepEqual(tierWarnings(typing, CATALOGUE), []);
});

test('a draft with nothing in it warns about the spells rather than throwing', () => {
  assert.deepEqual(tierWarnings({}, []), [
    'A package has to teach at least one spell: a pick that buys nothing is refused.',
  ]);
  assert.deepEqual(tierWarnings(undefined, undefined), [
    'A package has to teach at least one spell: a pick that buys nothing is refused.',
  ]);
});

test('a package is found by the reference that names it, and an unknown one is nothing', () => {
  assert.equal(tierNamed('tier:brute:v1', CATALOGUE), OPENER);
  assert.equal(tierNamed('tier:nobody:v1', CATALOGUE), null);
  assert.equal(tierNamed('tier:brute:v1', undefined), null);
});

// GameSchemaBuilder.Canonical resolves a package's id and its prerequisites through the alias map, exactly as
// it resolves a spell. Cutting a new version of an opener is what writes one, and a prerequisite pointing at
// the alias is how a package behind it follows rather than staying pinned to the version it was authored on.
test('a prerequisite naming an alias finds the package it points at', () => {
  const v2 = tier('tier:brute:v2', 1);
  const behind = tier('tier:heir:v1', 2, ['tier:brute'], ['spell:slam:v1']);
  const catalogue = [v2, behind];
  const resolve = reference => (reference === 'tier:brute' ? 'tier:brute:v2' : reference);

  assert.equal(tierNamed('tier:brute', catalogue, resolve), v2);
  assert.deepEqual(tierWarnings(behind.document, catalogue, resolve), []);
  assert.deepEqual(tiersBehind('tier:brute:v2', catalogue, resolve), [behind]);
});

// The same reference read two ways is still the same package, so a self-requirement written through the alias
// is one. Reading the two strings literally would let it through.
test('a package that requires the alias pointing at itself still requires itself', () => {
  const v2 = { id: 'tier:brute:v2', level: 1, prerequisites: ['tier:brute'], spells: ['spell:guard:v1'] };
  const resolve = reference => (reference === 'tier:brute' ? 'tier:brute:v2' : reference);

  assert.ok(tierWarnings(v2, [tier('tier:brute:v2', 1)], resolve).includes('A package cannot require itself.'));
});

// The link that decides whether a spell is reachable at all: a pick buys a package, so a spell no package
// teaches is one nobody acquires.
test('the packages teaching a spell are found through the alias the reference uses', () => {
  const named = tier('tier:aliased:v1', 1, [], ['spell:guard']);
  const resolve = reference => (reference === 'spell:guard' ? 'spell:guard:v1' : reference);

  assert.deepEqual(tiersTeaching('spell:guard:v1', [...CATALOGUE, named], resolve), [OPENER, named]);
  assert.deepEqual(tiersTeaching('spell:slam:v1', CATALOGUE), [ADVANCED]);
  assert.deepEqual(tiersTeaching('spell:nobody:v1', CATALOGUE), []);
});

test('the packages bought behind one are what renaming or disabling it would strand', () => {
  assert.deepEqual(tiersBehind('tier:brute:v1', CATALOGUE), [ADVANCED]);
  assert.deepEqual(tiersBehind('tier:marauder:v1', CATALOGUE), []);
});

// A package may not sell what every creature already starts with, and the builder's refusal names no
// creature. The sheet names them, so the author knows which starting kit is in the way.
test('a creature that already starts with a spell the package teaches is named', () => {
  const knight = { id: 'creature:knight:v1', name: 'Knight', path: 'Creatures/knight.json', document: { startingSpellIds: ['spell:guard'] } };
  const archer = { id: 'creature:archer:v1', name: 'Archer', path: 'Creatures/archer.json', document: { startingSpellIds: ['spell:shoot:v1'] } };
  const resolve = reference => (reference === 'spell:guard' ? 'spell:guard:v1' : reference);

  assert.deepEqual(startersOverlapping(OPENER.document, [knight, archer], resolve), [knight]);
  assert.deepEqual(startersOverlapping(ADVANCED.document, [knight, archer], resolve), []);
  assert.deepEqual(startersOverlapping(undefined, undefined), []);
});
