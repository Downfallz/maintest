import test from 'node:test';
import assert from 'node:assert/strict';
import { seatsOf, versusLine } from './players.js';

const mark = { seat: 'player1', kind: 'person', name: 'MarkF' };

test('the masthead says who this page plays and against whom', () => {
  assert.equal(versusLine([mark, { seat: 'player2', kind: 'bot', name: 'Lookahead (lookahead-55)' }], ['player1']), 'You: MarkF · against Lookahead (lookahead-55) (bot)');
  assert.equal(versusLine([mark, { seat: 'player2', kind: 'person', name: 'bastien' }], ['player2']), 'You: bastien · against MarkF');
  assert.equal(versusLine([mark, { seat: 'player2', kind: 'open', name: 'Open seat' }], ['player1']), 'You: MarkF · against an open seat');
});

test('a shared device, a player without an account and an older host are each said plainly', () => {
  assert.equal(versusLine([mark, { seat: 'player2', kind: 'person', name: 'MarkF' }], ['player1', 'player2']), 'MarkF · both seats on this device');
  assert.equal(versusLine([{ seat: 'player1', kind: 'person', name: 'Player' }, { seat: 'player2', kind: 'person', name: 'Player' }], ['player1']), 'You · against another player');
  assert.equal(versusLine(undefined, ['player1']), null);
  assert.equal(versusLine([mark], []), null);
});

test('the seats are read from the table the page holds a token of', () => {
  const answer = { tables: [
    { table: { id: 'a', seats: [mark] }, seats: [{ seat: 'player1', token: 'other' }] },
    { table: { id: 'b', seats: [{ seat: 'player1', kind: 'person', name: 'bastien' }] }, seats: [{ seat: 'player1', token: 'mine' }] },
  ] };
  assert.equal(seatsOf(answer, ['mine'])[0].name, 'bastien');
  assert.equal(seatsOf(answer, ['none']), null);
  assert.equal(seatsOf(null, ['mine']), null);
});
