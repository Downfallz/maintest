import { test } from 'node:test';
import assert from 'node:assert/strict';
import { friendAction, opponentCard, opponentOf } from './battle-opponent.js';

const table = { seats: [{ seat: 'player1', kind: 'person', name: 'MarkF', account: 'acct_mark' }, { seat: 'player2', kind: 'person', name: 'bastien', account: 'acct_bastien' }] };
const ladder = { players: [{ rank: 1, username: 'luce', rating: 1640, wins: 9, losses: 2, draws: 0, provisional: false },
  { rank: 3, username: 'bastien', rating: 1532, wins: 5, losses: 4, draws: 1, provisional: false }] };
const friends = (lists = {}) => ({ friends: [], incoming: [], outgoing: [], ...lists });

test('the opponent is the other seat at the table', () => {
  assert.equal(opponentOf(table, 'player1').name, 'bastien');
  assert.equal(opponentOf(table, 'player2').name, 'MarkF');
  assert.equal(opponentOf({ seats: [] }, 'player1'), null);
});

test('a ranked person shows their place, rating and record, and whether they are a friend', () => {
  const card = opponentCard(opponentOf(table, 'player1'), ladder, friends({ friends: [{ id: 'acct_bastien' }] }));

  assert.deepEqual([card.kind, card.name, card.badge, card.friend], ['person', 'bastien', '#3', 'friend']);
  assert.equal(card.rankText, '#3 · 1532 rating · 5W 4L 1D');
});

test('a person with no rated game is unranked, and a provisional one shows no place', () => {
  assert.equal(opponentCard({ kind: 'person', name: 'newcomer', account: 'acct_new' }, ladder, friends()).rankText, 'Unranked · no rated game yet');
  const provisional = opponentCard(opponentOf(table, 'player1'), { players: [{ ...ladder.players[1], provisional: true }] }, null);
  assert.deepEqual([provisional.badge, provisional.rankText], ['', 'Provisional · 1532 rating · 5W 4L 1D']);
});

test('a bot says it is a bot, and an open seat that nobody has taken yet', () => {
  assert.deepEqual(opponentCard({ seat: 'player2', kind: 'bot', name: 'Lookahead' }, ladder, friends()),
    { kind: 'bot', name: 'Lookahead', badge: 'BOT', rank: null, rankText: 'A bot · bots are not ranked', friend: null });
  assert.equal(opponentCard({ kind: 'open', name: 'Open seat' }).kind, 'waiting');
});

test('friendship reads from the friends list, and nothing is guessed when it could not be read', () => {
  const bastien = opponentOf(table, 'player1');
  assert.equal(opponentCard(bastien, ladder, friends({ outgoing: [{ id: 'acct_bastien' }] })).friend, 'asked');
  assert.equal(opponentCard(bastien, ladder, friends({ incoming: [{ id: 'acct_bastien' }] })).friend, 'asking');
  assert.equal(opponentCard(bastien, ladder, friends()).friend, 'none');
  assert.equal(opponentCard(bastien, null, null).friend, null);
  assert.equal(opponentCard(bastien, null, null).rankText, '');
  assert.equal(opponentCard({ kind: 'person', name: 'Player' }, ladder, friends()).friend, null, 'a seat with no account cannot be befriended');
});

test('the card offers to add a friend, to accept one who asked, and nothing once asked or friends', () => {
  assert.deepEqual(friendAction('none'), { label: 'Add friend', note: '' });
  assert.deepEqual(friendAction('asking'), { label: 'Accept friend request', note: 'Wants to be your friend' });
  assert.equal(friendAction('asked').label, ''); assert.equal(friendAction('friend').note, 'Your friend');
  assert.equal(friendAction(null), null);
});
