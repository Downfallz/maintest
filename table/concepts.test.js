// The concept cards (ADR 0187, docs/tabletop/onboarding-plan.md, O3b): every card is complete, names a rulebook
// section that exists, and the set is the one a lesson may name (`ConceptCards.Ids`, which `TutorialLesson` checks).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { CONCEPTS, conceptCard, conceptCards } from './concepts.js';

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
