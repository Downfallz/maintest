// The concept cards (ADR 0187, docs/tabletop/onboarding-plan.md, O3b): every card is complete, names a rulebook
// section that exists, and the set is the one a lesson may name (`ConceptCards.Ids`, which `TutorialLesson` checks).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { CONCEPTS, REFERENCE, conceptCard, conceptCards, conceptsFor } from './concepts.js';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const rulebook = readFileSync(join(root, 'docs/tabletop/rulebook.md'), 'utf8');

// A heading's anchor as GitHub writes it: lowercased, punctuation dropped, spaces as hyphens.
const anchorOf = heading => heading.toLowerCase().replace(/[^a-z0-9 -]/g, '').replace(/ /g, '-');
const headings = new Map(rulebook.split('\n').filter(line => /^#{2,3} /.test(line)).map(line => line.replace(/^#+ /, '')).map(text => [anchorOf(text), text]));

test('the cards are the concepts a lesson may name, in the host\'s order', () => {
  const source = readFileSync(join(root, 'src/DownfallArena.Application/Matches/Scenarios/ConceptCards.cs'), 'utf8');
  const listed = /Ids \{ get; \} =\s*\[([^\]]+)\]/.exec(source)?.[1].match(/"([a-z]+)"/g).map(id => id.slice(1, -1));
  assert.equal(listed?.length, 10);
  assert.deepEqual(CONCEPTS.map(card => card.id), listed);
});

test('every card has a title, one to three lines, an example and a rulebook section', () => {
  for (const card of CONCEPTS) {
    assert.match(card.title, /\S/, card.id);
    assert.ok(card.lines.length >= 1 && card.lines.length <= 3, `${card.id}: ${card.lines.length} lines`);
    for (const line of card.lines) assert.ok(line.trim() && line.length <= 160, `${card.id}: ${line}`);
    assert.ok(card.example.trim() && card.example.length <= 200, `${card.id}: example`);
    assert.match(card.rulebook.section, /^(Part \d|\d\.\d+)$/, card.id);
  }
});

test('every rulebook anchor is a heading of the rulebook, under the section the card names', () => {
  for (const card of CONCEPTS) {
    const heading = headings.get(card.rulebook.anchor);
    assert.ok(heading, `${card.id}: no heading for #${card.rulebook.anchor}`);
    assert.ok(heading.startsWith(card.rulebook.section), `${card.id}: ${heading}`);
    assert.equal(card.rulebook.label, `Rulebook, ${heading.replace(/^Part (\d)\. /, 'Part $1: ')}`);
  }
});

test('the reference values the cards quote match the hosted table\'s rule set and the reference creature', () => {
  const rules = JSON.parse(readFileSync(join(root, 'docs/tabletop/playtest.rules.json'), 'utf8'));
  const creature = JSON.parse(readFileSync(join(root, 'data/Creatures/main.v1.json'), 'utf8'));
  assert.deepEqual({ ...REFERENCE }, {
    health: creature.baseHealth,
    defense: creature.baseDefense,
    initiative: creature.baseInitiative,
    energyPerRound: rules.energyPerRound,
    criticalMultiplier: rules.criticalMultiplier,
  });
});

test('a card reads the reference values it is given, and only the cards that quote a value change', () => {
  const quotedBy = {
    health: ['health'], defense: ['health', 'defense'], initiative: ['initiative', 'tempo', 'mutation'], energyPerRound: ['energy'], criticalMultiplier: ['critical'],
  };
  const moved = { health: 40, defense: 1, initiative: 6, energyPerRound: 3, criticalMultiplier: 1.5 };
  const examples = cards => Object.fromEntries(cards.map(card => [card.id, card.example]));
  const today = examples(conceptsFor(REFERENCE));
  assert.deepEqual(today, examples(CONCEPTS));
  for (const [value, ids] of Object.entries(quotedBy)) {
    const other = examples(conceptsFor({ ...REFERENCE, [value]: moved[value] }));
    assert.deepEqual(Object.keys(today).filter(id => today[id] !== other[id]), ids, value);
  }
  const all = examples(conceptsFor(moved));
  assert.equal(all.health, 'A creature starts at 40 Health. A hit of 7 leaves it at 34.');
  assert.equal(all.energy, 'A creature at 1 Energy gains 3 at Upkeep and has 4: enough for a spell that costs 4.');
  assert.equal(all.defense, 'A hit of 7 on a creature with 3 Defense deals 4. A creature starts at 1 Defense: the same hit on it deals 6.');
  assert.equal(all.critical, 'A spell prints d20: 11+. A roll of 14 is a critical: its 7 damage becomes 10.');
  assert.equal(conceptsFor({ ...REFERENCE, health: 5 })[0].example, 'A creature starts at 5 Health. A hit of 7 leaves it at 0.');
  assert.equal(all.initiative, 'Two Precision creatures at 8 and 6 initiative: the one at 8 acts first.');
  assert.equal(all.tempo, 'An Instinct creature at 6 initiative acts before a Precision creature at 10.');
  assert.equal(all.mutation, 'A creature at 6 base initiative takes a mutation with a bonus of 1: it knows new spells and acts at 7.');
});

test('a card is found by its id, and nothing else passes for one', () => {
  assert.equal(conceptCard('defense').title, 'Defense');
  assert.equal(conceptCard('toString'), null);
  assert.equal(conceptCard(undefined), null);
});

test('a step\'s cards come in the order it names them, once each, the unknown left out', () => {
  assert.deepEqual(conceptCards(['defense', 'nothing', 'health', 'defense']).map(card => card.id), ['defense', 'health']);
  assert.deepEqual(conceptCards(null), []);
});

test('the cards cannot be changed by a page', () => {
  assert.throws(() => { CONCEPTS[0].title = 'x'; }, TypeError);
  assert.throws(() => { CONCEPTS[0].lines.push('x'); }, TypeError);
});
