import test from 'node:test';
import assert from 'node:assert/strict';
import { outcomeText, rematchLink } from './outcome.js';

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

test('a rematch goes to the lobby with the same opponent and format chosen, when there is one to choose', () => {
  const friend = { format: '2v2', seats: [{ seat: 'player1', kind: 'person', name: 'MarkF', account: 'acct_mark' }, { seat: 'player2', kind: 'person', name: 'bastien', account: 'acct_bastien' }] };
  assert.equal(rematchLink(friend, ['player1']), '/lobby?rematch=acct_bastien&format=2v2');
  assert.equal(rematchLink(friend, ['player2']), '/lobby?rematch=acct_mark&format=2v2');
  assert.equal(rematchLink(friend, ['player1', 'player2']), '/lobby?rematch=hotseat&format=2v2', 'both seats here: a shared device again');
  assert.equal(rematchLink({ format: '1v1', seats: [{ seat: 'player1', kind: 'person', account: 'acct_mark' }, { seat: 'player2', kind: 'bot', name: 'Greedy' }] }, ['player1']), '/lobby?rematch=bot&format=1v1');
  assert.equal(rematchLink({ seats: [{ seat: 'player1', kind: 'person' }, { seat: 'player2', kind: 'person', name: 'Player' }] }, ['player1']), null, 'an opponent without an account cannot be asked again');
  assert.equal(rematchLink(null, ['player1']), null);
  assert.equal(rematchLink(friend, []), null);
});
