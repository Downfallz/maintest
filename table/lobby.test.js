import test from 'node:test';
import assert from 'node:assert/strict';
import { actionName, challengeRequest, codePath, opponentOrder, reconcileRelations, finishedSummary, opponentAction, opponentRecord, friendChoice, gameKind, invitationLine, invitedState, lobbyRequest, localFinished, ongoingGames, ongoingLimit, rematchRequest, tableState, turnOf, versus, visibleTables, yourTurn, waitingGames, isRanked, createdOn, waitsOnAnother, waitsOnAPerson, latestFinished, seatPushSource } from './lobby.js';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { reachedModules } from '../scripts/module-imports.mjs';
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
  assert.deepEqual(rematchRequest('?rematch=bot'), { opponent: 'bot', friend: null, format: null, level: null });
  assert.deepEqual(rematchRequest('?rematch=bot&level=easy&format=1v1'), { opponent: 'bot', friend: null, format: '1v1', level: 'easy' });
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

test('a game of mine waits on another when the player against it is asked to play, or its seat is still open', () => {
  const theirs = game('d', { against: 'person' });
  theirs.table.seats[1].turn = true;
  assert.equal(waitsOnAnother(theirs), true);
  assert.equal(waitsOnAnother(game('e', { against: 'person', turn: true })), false, 'my turn');
  assert.equal(waitsOnAnother(game('a')), false, 'a bot answers at once: its round in play waits on nobody');
  assert.equal(waitsOnAnother(game('r', { against: 'person' })), false, 'a round being played, nobody asked, waits on no one');
  assert.equal(waitsOnAnother(game('b', { against: 'person', over: true })), false, 'over');
  const open = { table: { id: 'o', waiting: true, openSeats: 1, format: '3v3', seats: [{ seat: 'player1', kind: 'person', name: 'You', turn: false }, { seat: 'player2', kind: 'open' }] }, seats: [{ seat: 'player1' }] };
  assert.equal(waitsOnAnother(open), true, 'a seat not yet taken');
  assert.equal(waitsOnAnother(null), false, 'not one of mine');
});

test('the offer of pop-ups is made on a game against a person that waits on the other player, never a bot\'s or a shared device\'s', () => {
  const theirs = game('d', { against: 'person' });
  theirs.table.seats[1].turn = true;
  assert.equal(waitsOnAPerson(theirs), true);
  assert.equal(waitsOnAPerson(game('e', { against: 'person', turn: true })), false, 'my turn: I am here already');
  const bot = game('a');
  bot.table.waiting = true;
  assert.equal(waitsOnAPerson(bot), false, 'a bot is nobody to wait for');
  const both = { ...theirs, seats: [{ seat: 'player1' }, { seat: 'player2' }] };
  assert.equal(waitsOnAPerson(both), false, 'both seats here: nobody is away');
  assert.equal(waitsOnAPerson(null), false);
});

// A browser that can take a push: a worker, a push manager and the permission's prompt, each answer recorded.
function pushingBrowser({ held = null, permission = 'default', answer = 'granted' } = {}) {
  const calls = [];
  const subscription = endpoint => ({ endpoint, options: { applicationServerKey: new Uint8Array([4, 5, 6]).buffer }, toJSON: () => ({ endpoint }), unsubscribe: async () => { calls.push(['unsubscribe', endpoint]); } });
  let current = held ? subscription(held) : null;
  const registration = { pushManager: { getSubscription: async () => current, subscribe: async () => { calls.push(['subscribe']); current = subscription('made'); return current; } } };
  const nav = { serviceWorker: { register: async path => { calls.push(['register', path]); }, ready: Promise.resolve(registration) } };
  const win = { PushManager: function PushManager() {}, Notification: { permission, requestPermission: async () => { calls.push(['permission']); return answer; } }, isSecureContext: true };
  return { nav, win, calls };
}

// A seat that answers as the host does: its key (BAUG in base64url is the bytes 4, 5, 6), and each subscription it is given.
function seatAnswering({ publicKey = 'BAUG', ok = true, status = ok ? 200 : 500 } = {}) {
  const asked = [];
  const transport = (seat, token) => ({
    pushKey: async () => { asked.push(['key', seat, token]); return { ok: Boolean(publicKey), status: publicKey ? 200 : 404, body: publicKey ? { publicKey } : null }; },
    push: async body => { asked.push(['push', seat, token, body.endpoint]); return { ok, status, body: {} }; },
  });
  return { transport, asked };
}

const waiting = { table: { id: 'w' }, seats: [{ seat: 'player1', token: 'seat-token' }] };

test('with accounts off the offer asks through the waiting game\'s seat: ready once its key is read, says its switch is in the game\'s Match settings, and Turn on registers the browser with that seat (#1127, #1160)', async () => {
  const browser = pushingBrowser(), host = seatAnswering();
  let told = 0;
  const source = seatPushSource({ nav: browser.nav, win: browser.win, transport: host.transport, onChange: () => { told++; } });
  assert.equal(source.state().ready, false, 'no game waits: nothing to offer through');

  await source.setSeat(waiting);
  assert.deepEqual(source.state(), { ready: true, keyed: true, subscribed: false, permission: 'default', hotseat: false, where: "in the game's Match settings" });
  assert.ok(told > 0, 'the offer is told the seat was read');
  assert.deepEqual(host.asked, [['key', 'player1', 'seat-token']]);

  const after = await source.turnOn();
  assert.deepEqual(after, { ready: true, keyed: true, subscribed: true, permission: 'granted', hotseat: false, where: "in the game's Match settings" });
  assert.deepEqual(browser.calls, [['register', '/sw.js'], ['permission'], ['subscribe']]);
  assert.deepEqual(host.asked.at(-1), ['push', 'player1', 'seat-token', 'made']);
});

test('with accounts off a subscription the browser already holds is told to the seat, and the offer is not made', async () => {
  const browser = pushingBrowser({ held: 'kept', permission: 'granted' }), host = seatAnswering();
  const source = seatPushSource({ nav: browser.nav, win: browser.win, transport: host.transport });
  await source.setSeat(waiting);
  assert.equal(source.state().subscribed, true);
  assert.deepEqual(host.asked.at(-1), ['push', 'player1', 'seat-token', 'kept']);
});

test('with accounts off a host without keys, or no game waiting any more, offers nothing through the seat', async () => {
  const browser = pushingBrowser(), host = seatAnswering({ publicKey: null });
  const source = seatPushSource({ nav: browser.nav, win: browser.win, transport: host.transport });
  await source.setSeat(waiting);
  assert.equal(source.state().keyed, false);
  await source.setSeat(null);
  assert.equal(source.state().ready, false);
});

test('with accounts off a seat that could not be read is read again with the next list of games', async () => {
  const browser = pushingBrowser(), host = seatAnswering();
  let down = true;
  const transport = (seat, token) => { const real = host.transport(seat, token); return { ...real, pushKey: async () => { if (down) throw new Error('offline'); return real.pushKey(); } }; };
  const source = seatPushSource({ nav: browser.nav, win: browser.win, transport });
  await source.setSeat(waiting);
  assert.equal(source.state().keyed, false);
  down = false;
  await source.setSeat(waiting);
  assert.equal(source.state().keyed, true);
});

test('with accounts off a subscription gone from its push service is let go of, so the next Turn on makes a fresh one', async () => {
  const browser = pushingBrowser({ held: 'dead', permission: 'granted' }), host = seatAnswering({ ok: false, status: 410 });
  const source = seatPushSource({ nav: browser.nav, win: browser.win, transport: host.transport });
  await source.setSeat(waiting);
  assert.equal(source.state().subscribed, false);
  assert.deepEqual(browser.calls.at(-1), ['unsubscribe', 'dead']);
});

test('with accounts off a seat the page cannot speak for is no seat, and nothing is thrown', async () => {
  const browser = pushingBrowser(), host = seatAnswering();
  const source = seatPushSource({ nav: browser.nav, win: browser.win, transport: () => { throw new Error('neither player1 nor player2'); } });
  await source.setSeat({ table: { id: 'x' }, seats: [{ seat: 'player3', token: 't' }] });
  assert.equal(source.state().ready, false);
  assert.deepEqual(host.asked, []);
});

test('with accounts off a key the host could not give (a 503) is asked again with the next list of games', async () => {
  const browser = pushingBrowser(), host = seatAnswering();
  let down = true;
  const transport = (seat, token) => { const real = host.transport(seat, token); return { ...real, pushKey: async () => (down ? { ok: false, status: 503, body: null } : real.pushKey()) }; };
  const source = seatPushSource({ nav: browser.nav, win: browser.win, transport });
  await source.setSeat(waiting);
  assert.equal(source.state().keyed, false);
  down = false;
  await source.setSeat(waiting);
  assert.equal(source.state().keyed, true);
});

test('with accounts off a refusal of the seat leaves pop-ups off, and lets go of the subscription it made', async () => {
  const browser = pushingBrowser(), host = seatAnswering({ ok: false });
  const source = seatPushSource({ nav: browser.nav, win: browser.win, transport: host.transport });
  await source.setSeat(waiting);
  const after = await source.turnOn();
  assert.equal(after.subscribed, false);
  assert.deepEqual(browser.calls.at(-1), ['unsubscribe', 'made']);
});

test('the latest finished game is the one that ended last among the first game of each group\'s first page', () => {
  const ended = (table, finishedAt) => ({ table, finishedAt });
  assert.equal(latestFinished([ended('p1', '2026-11-05T20:00:00Z'), ended('b1', '2026-11-07T20:00:00Z')]), 'b1');
  assert.equal(latestFinished([ended('p1', '2026-11-09T20:00:00Z'), ended('b1', '2026-11-07T20:00:00Z')]), 'p1');
  assert.equal(latestFinished([null, ended('b1', '2026-11-07T20:00:00Z')]), 'b1', 'a group with no game, or not read yet');
  assert.equal(latestFinished([ended('p1', null), ended('b1', null)]), null, 'without dates no game is the last');
  assert.equal(latestFinished([ended(null, '2026-11-09T20:00:00Z'), ended('b1', '2026-11-07T20:00:00Z')]), 'b1', 'a result kept without its table is never chosen');
  assert.equal(latestFinished([]), null);
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

  assert.deepEqual(finishedSummary(kept), { title: '3v3 battle', tag: 'Victory', tone: 'won', casual: false, ranked: null, voided: null, against: 'Lookahead', bot: true, detail: '9 rounds', action: 'Watch the replay →' });
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

test('a game counts on the ladder when the host says it does, and only then', () => {
  assert.equal(isRanked({ ...held('a').table, ranked: true }), true);
  assert.equal(isRanked({ ...held('b').table, ranked: false }), false);
  assert.equal(isRanked(held('c').table), false, 'a host that did not say is not taken to mean ranked');
  assert.equal(isRanked(null), false);
});

test('a public card is ranked without the seats\' account ids, which the lobby gives only to a player seated there (#971)', () => {
  const table = held('a').table;
  const unnamed = { ...table, seats: table.seats.map(({ account, ...seat }) => seat) };
  assert.equal(isRanked({ ...unnamed, ranked: true }), true);
  assert.equal(isRanked({ ...table, ranked: false }), false, 'distinct accounts do not overrule the host');
});

test('a card says the day its game was created, and nothing when the host did not say', () => {
  assert.equal(createdOn('2026-11-12T15:30:00Z', 'en-GB'), '12 Nov 2026');
  assert.equal(createdOn(null), null);
  assert.equal(createdOn('not a date'), null);
});

test('a finished game says whether it counted on the ladder as the host kept it, and nothing when the host did not say', () => {
  const kept = { format: '3v3', outcome: 'won', opponent: { kind: 'person', name: 'bastien' }, rounds: 9 };
  assert.equal(finishedSummary({ ...kept, ranked: true }).ranked, true);
  assert.equal(finishedSummary({ ...kept, ranked: false }).ranked, false);
  assert.equal(finishedSummary(kept).ranked, null);
});

test('a game the operator voided says Voided and why instead of its result', () => {
  const summary = finishedSummary({ format: '3v3', outcome: 'won', ranked: false, voided: { category: 'abuse', explanation: 'A player won through a bug.' }, opponent: { kind: 'person', name: 'Bastien' } });

  assert.equal(summary.tag, 'Voided');
  assert.equal(summary.tone, 'voided');
  assert.equal(summary.voided, 'A player won through a bug.');
});

test('a table\'s action is named by its visible words first, then the battle, whom it is against and the table (UX9)', () => {
  const table = { id: 'tb-1', format: '3v3', seats: [{ seat: 'player1', kind: 'person', name: 'Walker' }, { seat: 'player2', kind: 'bot', name: 'Hard' }] };

  assert.equal(actionName('Resume game →', table, ['player1']), 'Resume game: 3v3 battle against Hard (bot), table tb-1');
  assert.equal(actionName('Take a seat →', { ...table, seats: [table.seats[0], { seat: 'player2', kind: 'open', name: null }] }), 'Take a seat: 3v3 battle, table tb-1');
});

test('an empty ladder or statistics page opens a ranked table for another player', () => {
  assert.deepEqual(rematchRequest('?rematch=person'), { opponent: 'person', friend: null, format: null, ranked: true });
});

// The home's text waits for every module lobby.js reaches: the page asks for all of them at once, not one level of
// imports after the other, which cost a new player's home its paint budget on the slow phone (#1207). motion.js
// is loaded by its own script.
test('the lobby page preloads every module lobby.js reaches, and nothing else', () => {
  const folder = fileURLToPath(new URL('.', import.meta.url));
  const page = readFileSync(new URL('./lobby.html', import.meta.url), 'utf8');
  // Every <link> whose rel names modulepreload, whatever the order of its attributes; one with no href is refused.
  const links = [...page.matchAll(/<link\b[^>]*>/g)].map(found => found[0]).filter(tag => /\brel="[^"]*\bmodulepreload\b/.test(tag));
  const preloaded = links.map(tag => {
    const href = /\bhref="([^"]+)"/.exec(tag);
    assert.ok(href, `a preload names no module: ${tag}`);
    return `/${href[1]}`;
  });
  assert.equal(new Set(preloaded).size, preloaded.length, 'a module is preloaded twice');
  assert.equal(preloaded[0], '/lobby.js');
  const reached = [...reachedModules(folder, ['lobby.js'])].filter(path => path !== '/motion.js');
  assert.deepEqual([...preloaded].sort(), reached.sort());
});
