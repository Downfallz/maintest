// The concept cards (ADR 0187, onboarding-plan.md O3b), read from the official rulebook (ADR 0188,
// rulebook-plan.md R4): every card the book carries, with its words as the book draws them, the section it sits
// in as its link, and nothing a page writes itself. The host holds the same set to `ConceptCards.Ids`
// (`RulebookConceptCardsTests`).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { NO_CONCEPTS, RULES_BOOK, conceptsOf, loadConcepts } from './concepts.js';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const book = JSON.parse(readFileSync(join(root, 'table/rules.json'), 'utf8'));
const sections = book.parts.flatMap(part => part.sections);
const concepts = conceptsOf(book);

test('the cards are the ones the rulebook carries, in its order, and the host may name them all', () => {
  assert.deepEqual(concepts.all.map(card => card.id), book.cards.map(card => card.id));
  const source = readFileSync(join(root, 'src/DownfallArena.Application/Matches/Scenarios/ConceptCards.cs'), 'utf8');
  const listed = /Ids \{ get; \} =\s*\[([^\]]+)\]/.exec(source)?.[1].match(/"([a-z]+)"/g).map(id => id.slice(1, -1));
  assert.deepEqual(concepts.all.map(card => card.id).sort(), [...listed].sort());
});

test('a card says what its section in the book says: its title, its lines and its example', () => {
  for (const card of concepts.all) {
    const section = sections.find(one => one.card?.id === card.id);
    assert.deepEqual({ title: card.title, lines: card.lines, example: card.example },
      { title: section.card.title, lines: section.card.lines, example: section.card.example }, card.id);
  }
  assert.equal(concepts.card('health').example, sections.find(one => one.id === 'winning').card.example);
});

test('a card links to its section of the rulebook, by the section\'s id, and names it as the book numbers it', () => {
  const tempo = sections.find(section => section.id === 'tempo');
  assert.deepEqual(concepts.card('tempo').rules, { section: 'tempo', href: '/rules#tempo', label: `Rules, ${tempo.number} ${tempo.title}` });
  // A card whose id is not its section's: the link is the section, which the page lands on and the build checks.
  const order = sections.find(section => section.card?.id === 'initiative');
  assert.equal(concepts.card('initiative').rules.href, `/rules#${order.id}`);
  for (const card of concepts.all) assert.match(card.rules.href, /^\/rules#[a-z0-9-]+$/, card.id);
});

test('the book a page reads moves the cards: no word or number of a card is written in the page', () => {
  const moved = structuredClone(book);
  const winning = moved.parts.flatMap(part => part.sections).find(section => section.id === 'winning');
  winning.card = { ...winning.card, title: 'Life', lines: ['One line.'], example: 'A creature starts at 40 Health.' };
  winning.number = '9.9';
  const card = conceptsOf(moved).card('health');
  assert.deepEqual({ title: card.title, lines: card.lines, example: card.example, label: card.rules.label },
    { title: 'Life', lines: ['One line.'], example: 'A creature starts at 40 Health.', label: 'Rules, 9.9 Winning' });
  const source = readFileSync(join(root, 'table/concepts.js'), 'utf8');
  for (const one of concepts.all) for (const line of [...one.lines, one.example]) assert.ok(!source.includes(line), `${one.id}: ${line}`);
});

test('a book that cannot be read gives no card, and a malformed card is left out', () => {
  for (const nothing of [null, undefined, 'x', {}, { parts: 'x' }, { parts: [{ sections: null }] }]) assert.deepEqual(conceptsOf(nothing).all, []);
  const odd = { parts: [{ sections: [
    { id: 'a', number: '1.1', title: 'A', card: { id: 'health', title: '', lines: ['x'], example: 'y' } },
    { id: 'b', number: '1.2', title: 'B', card: { id: 'energy', title: 'Energy', lines: 'x', example: 'y' } },
    { id: 'c', number: '1.3', title: 'C', card: { id: 'Tempo!', title: 'Tempo', lines: ['x'], example: 'y' } },
    { id: 'd', number: '1.4', title: 'D', card: { id: 'defense', title: 'Defense', lines: ['x', 3], example: 'y' } },
    { id: 'e', number: '1.5', title: 'E', card: { id: 'critical', title: 'Critical', lines: ['x'], example: 'y' } },
    { id: 'f', number: '1.6', title: 'F', card: { id: 'critical', title: 'Again', lines: ['x'], example: 'y' } },
    { id: 'g', number: '1.7', title: 'G', card: null },
  ] }] };
  assert.deepEqual(conceptsOf(odd).all.map(card => [card.id, card.title]), [['critical', 'Critical']]);
  assert.deepEqual(NO_CONCEPTS.all, []);
  assert.equal(NO_CONCEPTS.card('health'), null);
});

test('a card is found by its id, and nothing else passes for one', () => {
  assert.equal(concepts.card('defense').title, 'Defense');
  assert.equal(concepts.card('toString'), null);
  assert.equal(concepts.card(undefined), null);
});

test('a step\'s cards come in the order it names them, once each, the unknown left out', () => {
  assert.deepEqual(concepts.cards(['defense', 'nothing', 'health', 'defense']).map(card => card.id), ['defense', 'health']);
  assert.deepEqual(concepts.cards(null), []);
  assert.deepEqual(NO_CONCEPTS.cards(['health']), []);
});

test('the cards cannot be changed by a page', () => {
  assert.throws(() => { concepts.all[0].title = 'x'; }, TypeError);
  assert.throws(() => { concepts.all[0].lines.push('x'); }, TypeError);
  assert.throws(() => { concepts.all[0].rules.href = 'x'; }, TypeError);
  assert.throws(() => { concepts.all.push(concepts.all[0]); }, TypeError);
});

test('the page loads the book it is served beside, and goes on with no card when it cannot', async () => {
  assert.equal(RULES_BOOK, 'rules.json');
  const loaded = await loadConcepts(async () => book);
  assert.deepEqual(loaded.all.map(card => card.id), concepts.all.map(card => card.id));
  assert.equal(await loadConcepts(async () => { throw new Error('offline'); }), NO_CONCEPTS);
  assert.equal(await loadConcepts(async () => null), NO_CONCEPTS);
});
