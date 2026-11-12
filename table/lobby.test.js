import test from 'node:test';
import assert from 'node:assert/strict';
import { challengeRequest, codePath, opponentOrder, reconcileRelations, finishedSummary, opponentAction, opponentRecord, friendChoice, gameKind, invitationLine, invitedState, lobbyRequest, localFinished, ongoingGames, ongoingLimit, rematchRequest, tableState, turnOf, versus, visibleTables, yourTurn, waitingGames, isRanked, createdOn } from './lobby.js';
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
  const kept = { format: '3v3', rounds: 9, outcome: 'won', opponent: { kind: 'bot', name: 'Lookahead' }, seats: null, replay: '/battle?replay=t1&seat=player1' };

  assert.deepEqual(finishedSummary(kept), { title: '3v3 battle', tag: 'Victory', tone: 'won', casual: false, against: 'Lookahead', bot: true, detail: '9 rounds', action: 'Watch the replay →' });
  assert.equal(finishedSummary({ ...kept, casual: true }).casual, true, 'an unranked game says so');
  assert.equal(finishedSummary({ ...kept, outcome: 'lost', seats: [{ seat: 'player1', token: 't' }] }).action, 'View game →');
  assert.equal(finishedSummary({ ...kept, outcome: 'draw', replay: null }).action, null, 'a result kept before tables were named opens nothing');
  assert.equal(finishedSummary({ ...kept, outcome: 'both' }).tag, 'Shared device');
  assert.equal(finishedSummary({ ...kept, outcome: null, rounds: 1 }).detail, '1 round');
});

test("a recent opponent's record names only what happened, from this player's side", () => {
  // The card shows won–lost, and a third figure only when there was a draw; it is read as the words.
  assert.deepEqual(opponentRecord({ won: 2, lost: 1, drawn: 0 }), { score: '2–1', text: 'Won 2 · Lost 1' });
  assert.deepEqual(opponentRecord({ won: 0, lost: 0, drawn: 1 }), { score: '0–0–1', text: 'Drew 1' });
  assert.deepEqual(opponentRecord({}), { score: '–', text: 'No result kept' });
});

test('a friend is challenged, anyone else is asked to be a friend first, and nothing is offered where friends are not kept', () => {
  assert.deepEqual(opponentAction({ relation: 'friend' }), { label: 'Challenge →', act: 'challenge', primary: true });
  assert.equal(opponentAction({ relation: 'none' }).act, 'ask');
  // Asking one who asked first accepts their request.
  assert.deepEqual(opponentAction({ relation: 'asking' }), { label: 'Accept friend', act: 'ask', primary: false });
  assert.equal(opponentAction({ relation: 'asked' }).act, null);
  assert.equal(opponentAction({ relation: null }), null);
});

test("a challenge opens on the friend in the last game's format and rating", () => {
  assert.deepEqual(challengeRequest({ id: 'acct_abcdefghijklmnop', format: '1v1', casual: true }), { friend: 'acct_abcdefghijklmnop', format: '1v1', ranked: false });
  assert.deepEqual(challengeRequest({ id: 'acct_abcdefghijklmnop', format: '', casual: false }), { friend: 'acct_abcdefghijklmnop', format: null, ranked: true });
});

test('a friends list read again changes how each recent opponent is offered, and leaves alone one the host gave no relation', () => {
  const opponents = [{ id: 'a', relation: 'asked' }, { id: 'b', relation: 'none' }, { id: 'c', relation: 'asking' }, { id: 'd', relation: null }];
  const list = { friends: [{ id: 'a' }, { id: 'c' }], incoming: [], outgoing: [{ id: 'b' }] };
  assert.deepEqual(reconcileRelations(opponents, list).map(one => one.relation), ['friend', 'asked', 'friend', null]);
  assert.equal(reconcileRelations(opponents, { friends: [], incoming: [], outgoing: [] })[0].relation, 'none');
  assert.equal(reconcileRelations(opponents, null), opponents);
  // An opponent whose relation did not change is the same object, so nothing is drawn again.
  assert.equal(reconcileRelations(opponents, { friends: [], incoming: [{ id: 'c' }], outgoing: [{ id: 'a' }] })[0], opponents[0]);
});

test('friends come first among recent opponents, each group in the order the host listed it', () => {
  const opponents = [{ id: 'a', relation: 'none' }, { id: 'b', relation: 'friend' }, { id: 'c', relation: 'asking' }, { id: 'd', relation: 'friend' }, { id: 'e', relation: null }];
  assert.deepEqual(opponentOrder(opponents).map(one => one.id), ['b', 'd', 'a', 'c', 'e']);
  assert.deepEqual(opponentOrder([]), []);
});

// A game of this browser's: its table, who sits, who is asked, and when it must answer by (ADR 0169).
const held = (id, { mine = true, bot = false, over = false, answerBy = null } = {}) => ({
  table: { id, format: '3v3', over, answerBy, seats: [
    { seat: 'player1', kind: 'person', name: 'Mark', turn: mine, account: 'acct_mark' },
    { seat: 'player2', kind: bot ? 'bot' : 'person', name: bot ? 'Greedy' : 'Bastien', turn: !mine, ...(bot ? {} : { account: 'acct_bastien' }) } ] },
  seats: [{ seat: 'player1', token: `${id}-token` }],
});

test('your turn gathers the games against players waiting for this player, not the ones against a bot, which always are', () => {
  const games = [held('a'), held('b', { mine: false }), held('c', { bot: true }), held('d', { over: true })];

  assert.deepEqual(yourTurn(games).map(entry => entry.table.id), ['a']);
  assert.deepEqual(waitingGames(games).map(entry => entry.table.id), ['a', 'c'], 'the groups still put a bot game waiting for its player first');
});

test('your turn puts first the game whose time to answer runs out soonest, then the ones with none, as the host listed them', () => {
  const games = [held('no-clock'), held('later', { answerBy: '2026-11-13T10:00:00Z' }), held('also-no-clock'), held('sooner', { answerBy: '2026-11-12T10:00:00Z' })];

  assert.deepEqual(yourTurn(games).map(entry => entry.table.id), ['sooner', 'later', 'no-clock', 'also-no-clock']);
});

test('nothing waiting for this player is an empty turn', () => {
  assert.deepEqual(yourTurn([held('a', { mine: false })]), []);
  assert.deepEqual(yourTurn([]), []);
});

test('in each group, the games waiting for this player come first, the most pressing of them first', () => {
  const games = [held('theirs', { mine: false }), held('later', { answerBy: '2026-11-13T10:00:00Z' }), held('sooner', { answerBy: '2026-11-12T10:00:00Z' })];

  assert.deepEqual(ongoingGames(games).players.map(entry => entry.table.id), ['sooner', 'later', 'theirs']);
});

test('a game counts on the ladder when it is between players, in 3v3, and not opened unranked', () => {
  assert.equal(isRanked(held('a').table), true);
  assert.equal(isRanked({ ...held('b').table, casual: true }), false, 'opened unranked, or a shared device');
  assert.equal(isRanked({ ...held('c').table, format: '1v1' }), false, 'only 3v3 is ranked (ADR 0171)');
  assert.equal(isRanked(held('d', { bot: true }).table), false, 'a bot never is');
});

test('a game whose seats no account holds is not ranked: a host where nobody signs in keeps no ladder', () => {
  const table = held('a').table;
  assert.equal(isRanked({ ...table, seats: table.seats.map(seat => ({ ...seat, account: undefined })) }), false);
  assert.equal(isRanked({ ...table, seats: [table.seats[0], { ...table.seats[1], account: table.seats[0].account }] }), false, 'one account at both seats');
});

test('a seat still open or kept for a friend leaves a game ranked: an account takes it', () => {
  const table = held('a').table;
  assert.equal(isRanked({ ...table, seats: [table.seats[0], { seat: 'player2', kind: 'open', name: 'Open seat' }] }), true);
  assert.equal(isRanked({ ...table, seats: [table.seats[0], { seat: 'player2', kind: 'invited', name: 'Bastien' }] }), true);
});

test('a card says the day its game was created, and nothing when the host did not say', () => {
  assert.equal(createdOn('2026-11-12T15:30:00Z', 'en-GB'), '12 Nov 2026');
  assert.equal(createdOn(null), null);
  assert.equal(createdOn('not a date'), null);
});
