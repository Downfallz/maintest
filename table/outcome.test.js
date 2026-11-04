import test from 'node:test';
import assert from 'node:assert/strict';
import { outcomeText } from './outcome.js';

test('a host that serves no outcome only says the match is over', () => {
  assert.equal(outcomeText({ over: true }, 'player1'), 'The match is over.');
});
test('a draw names the reason without a winner', () => {
  assert.equal(outcomeText({ outcome: { winner: null, reason: 'RoundCap' } }, 'player1'), 'The match is a draw · more health at the round cap.');
});
test('a concession is said from the seat reading it', () => {
  assert.equal(outcomeText({ outcome: { winner: 'player2', reason: 'Concession' } }, 'player1'), 'You conceded · Player 2 wins.');
  assert.equal(outcomeText({ outcome: { winner: 'player2', reason: 'Concession' } }, 'player2'), 'Player 1 conceded · you win.');
});
test('a replay is nobody\'s seat and names the winner', () => {
  assert.equal(outcomeText({ replay: true, outcome: { winner: 'player1', reason: 'Elimination' } }, 'player1'), 'Player 1 wins · the last team standing.');
});
test('an unknown reason is shown as the host names it', () => {
  assert.equal(outcomeText({ outcome: { winner: 'player1', reason: 'Timeout' } }, 'player1'), 'You win · Timeout.');
});
