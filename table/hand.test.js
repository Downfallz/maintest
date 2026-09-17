import { test } from 'node:test';
import assert from 'node:assert/strict';
import { backText, declaredBy, handRows } from './hand.js';

const allies = [
  { id: 1, knownSpells: ['spell:basic_attack:v1', 'spell:heavy_strike:v1'] },
  { id: 2, knownSpells: ['spell:basic_attack:v1'] },
];

// The hand is every spell a creature knows, and the options say which of them can be cast now. A card that is
// known but unaffordable stays on the screen: "why is this one out" is a question the board answers by showing
// it, and a rule the page must never answer itself.
test('the hand holds every known spell, with the castable ones marked', () => {
  const intent = { creatures: [{ creature: 1, castableSpells: ['spell:basic_attack:v1'] }] };

  const rows = handRows(allies, intent, []);

  assert.deepEqual(rows.map(row => row.creature), [1, 2]);
  assert.deepEqual(rows[0].spells, [
    { spell: 'spell:basic_attack:v1', castable: true },
    { spell: 'spell:heavy_strike:v1', castable: false },
  ]);
});

// Outside intent selection there is no Intent section, and then nothing is castable -- which is the truth
// about that moment rather than a fallback the page invented.
test('with no intent options in the payload nothing in the hand is castable', () => {
  const rows = handRows(allies, undefined, []);

  assert.deepEqual(rows[0].spells.map(held => held.castable), [false, false]);
  assert.deepEqual(rows[1].spells.map(held => held.castable), [false]);
});

test('a creature with no known spells has an empty row rather than no row', () => {
  assert.deepEqual(handRows([{ id: 3 }], undefined, []), [{ creature: 3, declared: false, spells: [] }]);
  assert.deepEqual(handRows(undefined, undefined, undefined), []);
});

test('a creature that has declared says so on its row', () => {
  const rows = handRows(allies, undefined, [{ actor: 2, spell: 'spell:basic_attack:v1' }]);

  assert.deepEqual(rows.map(row => row.declared), [false, true]);
});

test('the creatures that have declared are the actors of the seat own intents', () => {
  assert.deepEqual([...declaredBy([{ actor: 1 }, { actor: 3 }])], [1, 3]);
  assert.deepEqual([...declaredBy(undefined)], []);
});

// This seat's own back is its own to read; the catalogue names the card, its id stands in when it cannot.
test('a back this seat may read names the creature and its card', () => {
  const cards = new Map([['spell:pummel:v1', { name: 'Pummel' }]]);

  assert.equal(backText({ actor: 1, spell: 'spell:pummel:v1' }, cards), '1: Pummel');
  assert.equal(backText({ actor: 2, spell: 'spell:unknown:v1' }, cards), '2: spell:unknown:v1');
  assert.equal(backText(undefined, cards), ': ');
});
