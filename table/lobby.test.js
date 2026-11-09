import test from 'node:test';
import assert from 'node:assert/strict';
import { codePath, finishedSummary, friendChoice, gameKind, invitationLine, invitedState, lobbyRequest, localFinished, ongoingGames, ongoingLimit, rematchRequest, tableState, turnOf, versus, visibleTables } from './lobby.js';
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
  assert.deepEqual(invitedState({ free: true, declined: false }), { tag: 'Seat kept for nobody', waiting: false, actions: ['reinvite', 'open', 'close'] }, 'after a cancel the ways out stay');
});

test('an invitation card says who asks and to what', () => {
  assert.equal(invitationLine({ from: { username: 'MarkF' }, table: { format: '2v2' } }), 'MarkF invites you to a 2v2 battle');
  assert.equal(invitationLine({ table: { format: '2v2' } }), 'A player invites you to a 2v2 battle');
});

test('a rematch link names the opponent to choose again, or nothing', () => {
  assert.deepEqual(rematchRequest('?rematch=acct_abcdefghijklmnop&format=2v2'), { opponent: 'friend', friend: 'acct_abcdefghijklmnop', format: '2v2', ranked: true });
  // An unranked game's rematch starts unranked (ADR 0155).
  assert.equal(rematchRequest('?rematch=acct_abcdefghijklmnop&ranked=0').ranked, false);
  assert.deepEqual(rematchRequest('?rematch=bot'), { opponent: 'bot', friend: null, format: null });
  assert.deepEqual(rematchRequest('?rematch=hotseat&format=1v1'), { opponent: 'hotseat', friend: null, format: '1v1' });
  assert.equal(rematchRequest('?rematch=../admin'), null);
  assert.equal(rematchRequest(''), null);
});

test('the create dialog starts on the friend asked for, and on nobody when they are not a friend', () => {
  const friends = [{ id: 'acct_mark', username: 'MarkF' }, { id: 'acct_bastien', username: 'bastien' }];
  assert.deepEqual(friendChoice(friends, 'acct_bastien'), { value: 'acct_bastien', missing: false });
  assert.deepEqual(friendChoice(friends, 'acct_stranger'), { value: '', missing: true }, 'a rematch against a non-friend picks nobody by default');
  assert.deepEqual(friendChoice(friends, null), { value: '', missing: false });
  assert.deepEqual(friendChoice([], 'acct_mark'), { value: '', missing: true });
});

const opponent = kind => kind === 'bot' ? { seat: 'player2', kind: 'bot', name: 'Greedy', turn: false } : { seat: 'player2', kind: 'person', name: 'bastien', turn: false };
const game = (id, { over = false, turn = false, against = 'bot' } = {}) => ({ table: { id, over, format: '3v3',
  seats: [{ seat: 'player1', kind: 'person', name: 'You', turn }, opponent(against)] }, seats: [{ seat: 'player1', token: `${id}-token` }] });

test('a game is against bots when a bot sits at it, and against players otherwise, a shared device included', () => {
  assert.equal(gameKind(game('a').table), 'bots');
  assert.equal(gameKind(game('b', { against: 'person' }).table), 'players');
  assert.equal(gameKind({ seats: [{ kind: 'person' }, { kind: 'person' }] }), 'players');
});

test('games in progress are grouped by kind, each with the ones waiting for you first', () => {
  const mine = [game('a'), game('b', { over: true }), game('c', { turn: true }), game('d', { against: 'person' }), game('e', { against: 'person', turn: true })];

  const ongoing = ongoingGames(mine);

  assert.deepEqual(ongoing.bots.map(entry => entry.table.id), ['c', 'a']);
  assert.deepEqual(ongoing.players.map(entry => entry.table.id), ['e', 'd']);
});

test('a group in progress says how many of the most at once, and whether it is full', () => {
  assert.deepEqual(ongoingLimit(3, 5), { text: '3 / 5', full: false });
  assert.deepEqual(ongoingLimit(5, 5), { text: '5 / 5', full: true });
  assert.deepEqual(ongoingLimit(2, null), { text: '2', full: false }, 'a host without accounts sets no limit');
});

test('without kept results, a kind\'s finished games are the finished tables this browser reaches, six to a page', () => {
  const mine = [...Array.from({ length: 7 }, (_, i) => game(`f${i}`, { over: true })), game('p', { over: true, against: 'person' }), game('live')];

  const first = localFinished(mine, 'bots'), last = localFinished(mine, 'bots', 9);

  assert.deepEqual([first.total, first.pages, first.page, first.items.length], [7, 2, 1, 6]);
  assert.deepEqual([last.page, last.items.map(item => item.table)], [2, ['f6']], 'a page past the end shows the last one');
  assert.deepEqual(localFinished(mine, 'players').items[0].opponent, { kind: 'person', name: 'bastien' });
  assert.deepEqual(localFinished(mine, 'players').items[0].seats, [{ seat: 'player1', token: 'p-token' }]);
});

test('a finished card leads with the result, names who it was against, and opens the game or its replay', () => {
  const kept = { format: '3v3', rounds: 9, outcome: 'won', opponent: { kind: 'bot', name: 'Lookahead' }, seats: null, session: '/session/t1' };

  assert.deepEqual(finishedSummary(kept), { title: '3v3 battle', tag: 'Victory', tone: 'won', casual: false, against: 'Lookahead', bot: true, detail: '9 rounds', action: 'Watch the replay →' });
  assert.equal(finishedSummary({ ...kept, casual: true }).casual, true, 'an unranked game says so');
  assert.equal(finishedSummary({ ...kept, outcome: 'lost', seats: [{ seat: 'player1', token: 't' }] }).action, 'View game →');
  assert.equal(finishedSummary({ ...kept, outcome: 'draw', session: null }).action, null, 'a result kept before tables were named opens nothing');
  assert.equal(finishedSummary({ ...kept, outcome: 'both' }).tag, 'Shared device');
  assert.equal(finishedSummary({ ...kept, outcome: null, rounds: 1 }).detail, '1 round');
});
