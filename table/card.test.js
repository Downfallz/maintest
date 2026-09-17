import { test } from 'node:test';
import assert from 'node:assert/strict';
import { cardCost, cardHead, cardLines, cardTitle, cardsById, criticalLine } from './card.js';

// A card as the host serves one. Every value here is arbitrary on purpose: the test holds that each one comes
// out of the renderer, which is what "the page carries no content" means on this side of the wire.
const card = {
  id: 'spell:probe:v1',
  name: 'Probe',
  cost: 2,
  initiative: 3,
  creatureClass: 'Scoundrel',
  tier: 'Assassin',
  targeting: 'Up to 2 enemies',
  effects: ['Damage 7', 'Bleed 4 a round, 2 rounds'],
  casterEffects: ['Caster: Heal 2'],
  critical: '35%',
  criticalThreshold: 14,
  requires: 'Guard, Strike',
};

test('every line of a card comes from the card', () => {
  assert.deepEqual(cardLines(card), [
    'Up to 2 enemies',
    'Damage 7',
    'Bleed 4 a round, 2 rounds',
    'Caster: Heal 2',
    'Crit 35% · d20 14+',
    'Unlock: +3 initiative',
    'Requires: Guard, Strike',
  ]);
  assert.equal(cardTitle(card), 'Probe');
  assert.equal(cardCost(card), '2');
  assert.equal(cardHead(card), 'Scoundrel · Assassin');
});

// The one that makes the sweep over the shipped files mean something: with nothing to draw from, the renderer
// draws nothing. A default here would be a rule the page had invented and the engine had never agreed to.
test('a card with no fields renders no lines rather than invented ones', () => {
  assert.deepEqual(cardLines({}), []);
  assert.equal(cardTitle({}), '');
  assert.equal(cardCost({}), '');
  assert.equal(cardHead({}), '');
});

test('a missing card is nothing at all, not a blank card', () => {
  assert.deepEqual(cardLines(undefined), []);
  assert.equal(cardTitle(null), '');
  assert.equal(criticalLine(undefined), '');
});

test('a cost of zero is printed, because free is a rule the page would be inventing', () => {
  assert.equal(cardCost({ cost: 0 }), '0');
});

// Until the catalogue is snapped to the d20 grid, a chance is a percentage and there is no face to roll. The
// page shows whichever it is given, with no idea which case it is in.
test('the die line appears only when the host computed a threshold', () => {
  assert.equal(criticalLine({ critical: '35%', criticalThreshold: 14 }), 'Crit 35% · d20 14+');
  assert.equal(criticalLine({ critical: '33%' }), 'Crit 33%');
  assert.equal(criticalLine({ critical: '0%' }), 'Crit 0%');
});

test('a spell id finds its card, and a spell the catalogue does not carry finds none', () => {
  const cards = cardsById({ cards: [card] });

  assert.equal(cards.get('spell:probe:v1').name, 'Probe');
  assert.equal(cards.get('spell:absent:v1'), undefined);
  assert.equal(cardsById(undefined).size, 0);
  assert.equal(cardsById({}).size, 0);
});
