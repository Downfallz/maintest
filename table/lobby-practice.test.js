import { test } from 'node:test';
import assert from 'node:assert/strict';
import { practiceCards } from './lobby-practice.js';

test('a listed scenario is a card with its title, where it starts, the seat played, its goal and its link', () => {
  const [card] = practiceCards({ scenarios: [{ id: 'sc-1', title: 'Bleed after a stun', goal: 'Keep it alive', round: 4, seat: 'player2', format: '3v3', link: '/battle?scenario=sc-1' }] });

  assert.deepEqual(card, { id: 'sc-1', title: 'Bleed after a stun', goal: 'Keep it alive', facts: '3v3 · from round 4 · you play player 2', link: '/battle?scenario=sc-1' });
});

test('a scenario with no title is named by its round, and one with no link is left out', () => {
  const cards = practiceCards({ scenarios: [{ id: 'sc-2', round: 3, seat: 'player1', link: '/b' }, { id: 'sc-3', round: 1 }] });

  assert.deepEqual(cards.map(one => [one.id, one.title, one.facts]), [['sc-2', 'Round 3', 'from round 3 · you play player 1']]);
});

test('nothing listed, or no answer, is no card', () => {
  assert.deepEqual(practiceCards({ scenarios: [] }), []);
  assert.deepEqual(practiceCards(undefined), []);
});
