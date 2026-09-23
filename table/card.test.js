import { test } from 'node:test';
import assert from 'node:assert/strict';
import { cardCost, cardHead, cardLines, cardTitle, cardsById, criticalLine, loadCards, loadCatalogue } from './card.js';

// A card as the host serves one. Every value here is arbitrary on purpose: the test holds that each one comes
// out of the renderer, which is what "the page carries no content" means on this side of the wire.
const card = {
  id: 'spell:probe:v1',
  name: 'Probe',
  cost: 2,
  creatureClass: 'Scoundrel',
  targeting: 'Up to 2 enemies',
  effects: ['Damage 7', 'Bleed 4 a round, 2 rounds'],
  casterEffects: ['Caster: Heal 2'],
  critical: '35%',
  criticalThreshold: 14,
};

test('every line of a card comes from the card', () => {
  assert.deepEqual(cardLines(card), [
    'Up to 2 enemies',
    'Damage 7',
    'Bleed 4 a round, 2 rounds',
    'Caster: Heal 2',
    'Crit 35% · d20 14+',
  ]);
  assert.equal(cardTitle(card), 'Probe');
  assert.equal(cardCost(card), '2');
  assert.equal(cardHead(card), 'Scoundrel');
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

// A spell is acquired by buying the package that teaches it, so what it takes and what it buys are printed on
// that package's card and only there (ADR 0056). A face that still carried a talent gate, a tree depth or a
// per-spell unlock bonus would print three rules the engine stopped applying -- and a table plays its cards.
test('a card prints no gate, no tree depth and no unlock bonus, whatever it is handed', () => {
  assert.deepEqual(cardLines({ initiative: 0, requires: 'Guard, Strike' }), []);
  assert.deepEqual(cardLines({ initiative: 3, requires: 'Guard' }), []);
  assert.equal(cardHead({ creatureClass: 'Scoundrel', tier: 3 }), 'Scoundrel');
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

// A seat whose transport refuses everything: the token this browser kept from an earlier table.
const refusing = { seat: 'player1', transport: { catalogue: async () => ({ ok: false, status: 403, body: null }) } };

const serving = cards => ({ seat: 'player2', transport: { catalogue: async () => ({ ok: true, status: 200, body: { cards } }) } });

// The expensive failure this guards: asking only the first seat, finding a stale token there, and printing
// raw spell ids for the rest of the session while the seat beside it would have answered.
test('the catalogue is loaded through whichever seat the table accepts', async () => {
  const cards = await loadCards([refusing, serving([card])]);

  assert.equal(cards.get('spell:probe:v1').name, 'Probe');
});

test('a page whose every seat is refused draws no cards rather than failing', async () => {
  assert.equal((await loadCards([refusing])).size, 0);
  assert.equal((await loadCards([])).size, 0);
  assert.equal((await loadCards(undefined)).size, 0);
});

test('the catalogue itself is what the mat and the rule line are drawn from', async () => {
  const catalogue = await loadCatalogue([refusing, serving([card])]);

  assert.deepEqual(catalogue.cards, [card]);
  assert.equal(await loadCatalogue([refusing]), null);
  assert.equal(await loadCatalogue([]), null);
});

test('visual stat groups preserve zero and non-d20 chances without inventing absent stats', async () => {
  const { cardStats, cardDetails } = await import('./card.js');
  assert.deepEqual(cardStats({}), []);
  const stats = cardStats({ initiative: 0, critical: '33%' });
  assert.equal(stats.length, 1);
  assert.equal(stats[0].value, '33%');
  assert.equal(stats[0].hint, 'Standard only');
  assert.equal(cardStats({ critical: '0%' })[0].value, '0%');
  assert.equal(cardDetails({ requires: 'retired spell gate' }).length, 0);
  assert.deepEqual(cardDetails({}), []);
  assert.equal(cardDetails(card).find(row => row.role === 'target').text, card.targeting);
  assert.deepEqual(cardDetails(card).filter(row => row.role === 'effect').map(row => row.text), card.effects);
});
