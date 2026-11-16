import test from 'node:test';
import assert from 'node:assert/strict';
import { seatsOf, timeLeft, turnOf, versusLine } from './players.js';

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

test('the time left to answer reads in days and hours, hours, or minutes, and is nothing once it has passed', () => {
  const now = Date.parse('2026-11-12T00:00:00Z');
  const at = hours => new Date(now + hours * 3600000).toISOString();

  assert.deepEqual([at(53), at(48), at(5.5), at(0.25), at(-1), undefined].map(answerBy => timeLeft(answerBy, now)), ['2 d 5 h', '2 d', '5 h', '15 min', null, null]);
});

test('whose turn it is says how long they have left to answer in a game between two players', () => {
  const now = Date.parse('2026-11-12T00:00:00Z');
  const table = mine => ({ answerBy: '2026-11-13T05:00:00Z', seats: [{ seat: 'player1', kind: 'person', name: 'Mark', turn: mine }, { seat: 'player2', kind: 'person', name: 'Bastien', turn: !mine }] });

  assert.equal(turnOf(table(true), ['player1'], now).text, 'Your turn · 1 d 5 h left');
  assert.equal(turnOf(table(false), ['player1'], now).text, "Bastien's turn · 1 d 5 h left");
  assert.equal(turnOf({ ...table(true), answerBy: null }, ['player1'], now).text, 'Your turn');
});
