import test from 'node:test';
import assert from 'node:assert/strict';
import { matchResultSummary, outcomeText, rematchLink, rematchPlan } from './outcome.js';

test('a host that serves no outcome only says the match is over', () => {
  assert.equal(outcomeText({ over: true }, 'player1'), 'The match is over.');
});
test('a draw names the reason without a winner', () => {
  assert.equal(outcomeText({ outcome: { winner: null, reason: 'RoundCap' } }, 'player1'), 'The match is a draw · equal health at the round cap.');
});
test('a concession is said from the seat reading it', () => {
  assert.equal(outcomeText({ outcome: { winner: 'player2', reason: 'Concession' } }, 'player1'), 'You conceded · Player 2 wins.');
  assert.equal(outcomeText({ outcome: { winner: 'player2', reason: 'Concession' } }, 'player2'), 'Player 1 conceded · you win.');
  // A timeout (ADR 0169) says whose time ran out, from each side, and a timeout of both is a draw.
  assert.equal(outcomeText({ outcome: { winner: 'player1', reason: 'TimedOut' } }, 'player2'), 'Your time to answer ran out · Player 1 wins.');
  assert.equal(outcomeText({ outcome: { winner: 'player1', reason: 'TimedOut' } }, 'player1'), 'Player 2 let their time to answer run out · you win.');
  assert.equal(outcomeText({ outcome: { winner: null, reason: 'TimedOut' } }, 'player1'), 'The match is a draw · both players let the time to answer run out.');
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
  // An unranked game (ADR 0155) is offered again unranked.
  assert.equal(rematchLink({ ...friend, casual: true }, ['player1']), '/lobby?rematch=acct_bastien&format=2v2&ranked=0');
  assert.equal(rematchPlan({ ...friend, casual: true }, ['player1'], 'friend').request.ranked, false);
  assert.equal(rematchLink(friend, ['player1', 'player2']), '/lobby?rematch=hotseat&format=2v2', 'both seats here: a shared device again');
  assert.equal(rematchLink({ format: '1v1', seats: [{ seat: 'player1', kind: 'person', account: 'acct_mark' }, { seat: 'player2', kind: 'bot', name: 'Greedy' }] }, ['player1']), '/lobby?rematch=bot&format=1v1');
  assert.equal(rematchLink({ seats: [{ seat: 'player1', kind: 'person' }, { seat: 'player2', kind: 'person', name: 'Player' }] }, ['player1']), null, 'an opponent without an account cannot be asked again');
  assert.equal(rematchLink(null, ['player1']), null);
  assert.equal(rematchLink(friend, []), null);
});

test('the final card trusts the host outcome even when the losing team has more health', async () => {
  const { matchResultSummary } = await import('./outcome.js');
  const view = { over: true, outcome: { winner: 'player2', reason: 'Concession' }, board: {
    allies: [{ health: 30, isAlive: true }], enemies: [{ health: 2, isAlive: true }, { health: 0, isAlive: false }] } };
  assert.equal(matchResultSummary(view, 'player1').title, 'Defeat');
  assert.deepEqual(matchResultSummary(view, 'player1').teams.map(one => one.text), ['1/1 standing · 30 HP', '1/2 standing · 2 HP']);
  assert.equal(matchResultSummary({ ...view, outcome: { winner: null, reason: 'RoundCap' } }, 'player1').title, 'Draw');
  assert.equal(matchResultSummary({ ...view, outcome: null }, 'player1').title, 'Match complete');
  assert.equal(matchResultSummary({ ...view, over: false }, 'player1'), null);
  assert.match(matchResultSummary({ over: true }, 'player1').teams[0].text, /unavailable/);
});

test('the end card plays the same game again in one tap against a bot or on a shared device', () => {
  const bot = { format: '1v1', seats: [{ seat: 'player1', kind: 'person', account: 'acct_mark' }, { seat: 'player2', kind: 'bot', name: 'Lookahead' }] };
  assert.deepEqual(rematchPlan(bot, ['player1']), { label: 'Rematch', request: { opponent: 'bot', format: '1v1' } });
  // The same level again (ADR 0177).
  const easy = { format: '3v3', seats: [{ seat: 'player1', kind: 'person' }, { seat: 'player2', kind: 'bot', name: 'Easy', level: 'easy' }] };
  assert.deepEqual(rematchPlan(easy, ['player1']), { label: 'Rematch', request: { opponent: 'bot', format: '3v3', level: 'easy' } });
  assert.equal(rematchLink(easy, ['player1']), '/lobby?rematch=bot&level=easy&format=3v3');
  const shared = { format: '3v3', seats: [{ seat: 'player1', kind: 'person' }, { seat: 'player2', kind: 'person' }] };
  assert.deepEqual(rematchPlan(shared, ['player1', 'player2']), { label: 'Rematch', request: { opponent: 'hotseat', format: '3v3' } });
});
test('against a player, the end card invites a friend at once and opens the lobby on anyone else', () => {
  const table = { format: '2v2', seats: [{ seat: 'player1', kind: 'person', account: 'acct_mark' }, { seat: 'player2', kind: 'person', account: 'acct_bastien' }] };
  assert.deepEqual(rematchPlan(table, ['player1'], 'friend'), { label: 'Invite to rematch', request: { opponent: 'friend', friend: 'acct_bastien', format: '2v2' } });
  for (const friendship of ['none', 'asked', 'asking', null]) {
    assert.deepEqual(rematchPlan(table, ['player1'], friendship), { label: 'Invite to rematch', link: '/lobby?rematch=acct_bastien&format=2v2' }, String(friendship));
  }
});
test('the end card offers no rematch when there is nobody to play again', () => {
  assert.equal(rematchPlan({ seats: [{ seat: 'player1', kind: 'person' }, { seat: 'player2', kind: 'person', name: 'Player' }] }, ['player1'], 'friend'), null);
  assert.equal(rematchPlan(null, ['player1']), null);
});
test('each team of the result carries the numbers its card draws, null where the host served none', () => {
  const creature = (health, isAlive) => ({ health, maxHealth: 30, isAlive });
  const view = { over: true, outcome: { winner: 'player2', reason: 'Elimination' }, board: { roundNumber: 6,
    allies: [creature(0, false), creature(-3, false)], enemies: [creature(12, true), creature(30, true)] } };
  const [ours, theirs] = matchResultSummary(view, 'player1').teams;
  assert.deepEqual({ ...ours }, { label: 'Your team', text: '0/2 standing · -3 HP', standing: 0, total: 2, hp: 0, maxHp: 60 });
  assert.deepEqual({ ...theirs }, { label: 'Opponent', text: '2/2 standing · 42 HP', standing: 2, total: 2, hp: 42, maxHp: 60 });
  const legacy = matchResultSummary({ over: true, board: { allies: [{}], enemies: [] } }, 'player1').teams;
  assert.deepEqual({ ...legacy[0] }, { label: 'Your team', text: 'Survivors unavailable · HP unavailable', standing: null, total: 1, hp: null, maxHp: null });
});
