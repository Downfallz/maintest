import test from 'node:test';
import assert from 'node:assert/strict';
import { badgeText, bellLabel, inboxItems } from './inbox.js';

const table = (id, seats, extra = {}) => ({ id, format: '3v3', over: false, waiting: false, round: 4, seats, ...extra });
const me = { seat: 'player1', kind: 'person', name: 'MarcF' };

test('a game where this player is asked is their turn, named by who it is against; one where the other is asked is not', () => {
  const mine = [
    { table: table('a', [{ ...me, turn: true }, { seat: 'player2', kind: 'person', name: 'Bastien' }]), seats: [{ seat: 'player1', token: 't1' }] },
    { table: table('b', [me, { seat: 'player2', kind: 'person', name: 'Julie', turn: true }]), seats: [{ seat: 'player1', token: 't2' }] },
    { table: table('c', [{ ...me, turn: true }, { seat: 'player2', kind: 'bot', name: 'Lookahead' }]), seats: [{ seat: 'player1', token: 't3' }] },
  ];
  const items = inboxItems({ mine });
  assert.deepEqual(items.map(item => item.title), ['Your turn against Bastien', 'Your turn against Lookahead (bot)']);
  assert.equal(items[0].detail, '3v3 battle · Round 4');
  assert.deepEqual(items[0].seats, [{ seat: 'player1', token: 't1' }]);
});

test('a finished game, one not begun and a shared device whose turn it is are read as such', () => {
  const both = { table: table('d', [{ ...me, turn: true }, { seat: 'player2', kind: 'person', name: 'MarcF' }]), seats: [{ seat: 'player1', token: 'a' }, { seat: 'player2', token: 'b' }] };
  const over = { table: table('e', [{ ...me, turn: true }, { seat: 'player2', kind: 'person', name: 'Bastien' }], { over: true }), seats: [{ seat: 'player1', token: 'c' }] };
  const waiting = { table: table('f', [{ ...me, turn: true }, { seat: 'player2', kind: 'open' }], { waiting: true }), seats: [{ seat: 'player1', token: 'd' }] };
  assert.deepEqual(inboxItems({ mine: [both, over, waiting] }).map(item => item.title), ['Your turn against a shared device']);
});

test('turns come first, then invitations, friend requests and declined invitations, each with what its action needs', () => {
  const items = inboxItems({
    mine: [
      { table: table('x', [me, { seat: 'player2', kind: 'person', name: 'Alex' }]), seats: [{ seat: 'player1', token: 't' }], invited: { username: 'Alex', declined: true } },
      { table: table('y', [{ ...me, turn: true }, { seat: 'player2', kind: 'person', name: 'Bastien' }]), seats: [{ seat: 'player1', token: 'u' }] },
    ],
    invitations: [{ table: { id: 'z', format: '1v1' }, from: { id: 'acct_b', username: 'Bastien' } }],
    friends: { friends: [], incoming: [{ id: 'acct_c', username: 'Julie' }], outgoing: [] },
  });
  assert.deepEqual(items.map(item => item.kind), ['turn', 'invitation', 'friend', 'declined']);
  assert.equal(items[1].title, 'Bastien invites you'); assert.equal(items[1].table, 'z'); assert.equal(items[1].detail, '1v1 battle');
  assert.equal(items[2].title, 'Julie wants to be your friend'); assert.equal(items[2].account, 'acct_c');
  assert.equal(items[3].title, 'Alex declined your invitation');
});

test('an invitation still waiting for its answer, and nothing at all, give nothing to do', () => {
  const pending = { table: table('w', [me, { seat: 'player2', kind: 'person', name: 'Alex' }], { waiting: true }), seats: [{ seat: 'player1', token: 't' }], invited: { username: 'Alex', declined: false } };
  assert.deepEqual(inboxItems({ mine: [pending] }), []);
  assert.deepEqual(inboxItems(), []);
});

test('the badge counts up to nine, then says 9+, and the bell says how much waits', () => {
  assert.equal(badgeText(0), '');
  assert.equal(badgeText(3), '3');
  assert.equal(badgeText(12), '9+');
  assert.equal(bellLabel(2), 'Notifications · 2 waiting');
  assert.equal(bellLabel(0), 'Notifications · nothing waiting');
});
