import test from 'node:test';
import assert from 'node:assert/strict';
import { codePath, invitationLine, invitedState, lobbyRequest, tableState, turnOf, versus, visibleTables } from './lobby.js';
import { activateSeats, forget, heldSeats, savedSeats, selectTableSeats } from './session.js';

function storage() {
  const values = new Map();
  return { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value) };
}

test('switching tables keeps the earlier seats, including a browser migrated from the old active pair', () => {
  const kept = storage();
  kept.setItem('downfall.table.seats', JSON.stringify({ player1: 'old' }));
  activateSeats([{ seat: 'player1', token: 'new' }], kept);
  assert.deepEqual(heldSeats('', kept), [{ seat: 'player1', token: 'new' }]);
  assert.deepEqual(savedSeats(kept).map(seat => seat.token), ['old', 'new']);
  activateSeats([{ seat: 'player1', token: 'old' }], kept);
  assert.deepEqual(heldSeats('', kept), [{ seat: 'player1', token: 'old' }]);
});

test('a fresh invitation never mixes an older tables other seat into hotseat', () => {
  const first = { seats: [{ seat: 'player1', token: 'first' }, { seat: 'player2', token: 'other' }] };
  const second = { seats: [{ seat: 'player2', token: 'second' }] };
  assert.deepEqual(selectTableSeats([first, second], [{ seat: 'player2', token: 'second' }]), second.seats);
  assert.deepEqual(selectTableSeats([first], [{ seat: 'player1', token: 'first' }]), first.seats);
  assert.deepEqual(selectTableSeats([first], [{ seat: 'player1', token: 'expired' }]), []);
});

test('leaving drops just that capability while the other games remain available', () => {
  const kept = storage();
  activateSeats([{ seat: 'player1', token: 'earlier' }], kept);
  activateSeats([{ seat: 'player1', token: 'current' }, { seat: 'player2', token: 'hotseat' }], kept);
  forget(kept, 'player1');
  assert.deepEqual(savedSeats(kept).map(seat => seat.token), ['earlier', 'hotseat']);
  assert.deepEqual(heldSeats('', kept), [{ seat: 'player2', token: 'hotseat' }]);
});

test('saved seats tolerate corrupt or unavailable browser storage', () => {
  assert.deepEqual(savedSeats({ getItem() { throw new Error('blocked'); } }), []);
  assert.doesNotThrow(() => activateSeats([{ seat: 'player1', token: 'link' }], null));
  assert.deepEqual(savedSeats({ getItem() { return 'null'; } }), []);
});

test('open tables omit my own place and finished games while all ongoing includes occupied tables', () => {
  const tables = [
    { id: 'mine', openSeats: 1 }, { id: 'open', openSeats: 1 },
    { id: 'playing', openSeats: 0 }, { id: 'over', openSeats: 1, over: true },
  ];
  const mine = [{ table: tables[0] }];
  assert.deepEqual(visibleTables(tables, mine).map(table => table.id), ['open']);
  assert.deepEqual(visibleTables(tables, mine, true).map(table => table.id), ['mine', 'open', 'playing']);
  assert.equal(tableState({ waiting: false, openSeats: 1, round: 5 }), 'Player needed');
  assert.equal(tableState({ waiting: false, openSeats: 0, round: 5 }), 'Round 5');
});

test('a typed code supports spaces and lowercase but cannot navigate to arbitrary paths', () => {
  assert.equal(codePath(' k7f2-q9xm '), '/j/K7F2Q9XM');
  for (const code of ['abc', '../admin', 'https://other.example', '<script>']) assert.equal(codePath(code), null);
});

test('a race for a place surfaces the servers refusal instead of opening a broken table', async () => {
  await assert.rejects(lobbyRequest('/api/lobby/tables/a/join', {}, null, async (path, request) => {
    assert.equal(request.headers['Content-Type'], 'application/json');
    return { ok: false, status: 409, text: async () => JSON.stringify({ message: 'Already occupied' }) };
  }), /Already occupied/);
});

test('a card names each side as the host does, and an older host as players', () => {
  const named = versus({ seats: [{ seat: 'player1', kind: 'person', name: 'MarkF' }, { seat: 'player2', kind: 'bot', name: 'search-19' }] });
  assert.deepEqual(named.map(side => `${side.kind}:${side.name}`), ['person:MarkF', 'bot:search-19']);
  assert.deepEqual(versus({ players: ['Player', 'greedy'] }).map(side => side.seat), ['player1', 'player2']);
});

test("a card says whose turn it is at an unfinished table, and when it is this browser's", () => {
  const seats = (first, second) => [{ seat: 'player1', kind: 'person', name: 'MarkF', turn: first }, { seat: 'player2', kind: 'person', name: 'bastien', turn: second }];
  assert.deepEqual(turnOf({ seats: seats(true, false) }, ['player1']), { mine: true, text: 'Your turn' });
  assert.deepEqual(turnOf({ seats: seats(false, true) }, ['player1']), { mine: false, text: "bastien's turn" });
  assert.equal(turnOf({ seats: seats(true, true) }, ['player2']).text, 'Your turn · both players choose');
  assert.equal(turnOf({ seats: seats(true, true) }).text, 'Both players to play');
  assert.equal(turnOf({ seats: [{ seat: 'player1', kind: 'open', name: 'Open seat', turn: true }, { seat: 'player2', kind: 'bot', name: 'Greedy', turn: false }] }).text, 'Waiting for a player to take the open seat');
  for (const table of [{ seats: seats(false, false) }, { over: true, seats: seats(true, false) }, { waiting: true, seats: seats(true, false) }, {}]) assert.equal(turnOf(table, ['player1']), null);
});

test('an inviter\'s card says who it waits for, or who declined and the ways out', () => {
  assert.equal(invitedState(null), null);
  assert.deepEqual(invitedState({ username: 'bastien', declined: false }), { tag: 'Waiting for bastien…', waiting: true, actions: ['cancel'] });
  assert.deepEqual(invitedState({ username: 'bastien', declined: true }), { tag: 'bastien declined', waiting: false, actions: ['reinvite', 'open', 'close'] });
});

test('an invitation card says who asks and to what', () => {
  assert.equal(invitationLine({ from: { username: 'MarkF' }, table: { format: '2v2' } }), 'MarkF invites you to a 2v2 battle');
  assert.equal(invitationLine({ table: { format: '2v2' } }), 'A player invites you to a 2v2 battle');
});
