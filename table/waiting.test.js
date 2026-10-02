import { test } from 'node:test';
import assert from 'node:assert/strict';
import { waitingRoom } from './waiting.js';

test('a seat waiting for the other player is told who, with the code and the link that bring them', () => {
  const room = waitingRoom({ waiting: { seats: [{ slot: 'player2', code: 'K7F2Q9XM', join: '/j/K7F2Q9XM' }] } }, 'https://table.example');

  assert.equal(room.line, 'Waiting for Player 2 to join…');
  assert.deepEqual(room.seats, [{ slot: 'player2', name: 'Player 2', code: 'K7F2Q9XM', link: 'https://table.example/j/K7F2Q9XM' }]);
  assert.match(room.detail, /at the same time/);
});

test('a table waiting for both players names both, and a seat without a code shows none', () => {
  const room = waitingRoom({ waiting: { seats: [{ slot: 'player1' }, { slot: 'player2', code: 'ABCDEFGH', join: '/j/ABCDEFGH' }] } });

  assert.equal(room.line, 'Waiting for Player 1 and Player 2 to join…');
  assert.equal(room.seats[0].code, null);
  assert.equal(room.seats[0].link, null);
  assert.equal(room.seats[1].link, '/j/ABCDEFGH');
});

test('once the match has begun, or against a bot, there is no room', () => {
  assert.equal(waitingRoom({ waiting: null }), null);
  assert.equal(waitingRoom({ waiting: { seats: [] } }), null);
  assert.equal(waitingRoom({}), null);
  assert.equal(waitingRoom(null), null);
});
