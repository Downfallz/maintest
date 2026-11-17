import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { buildRecap, healthChangeText, questionText } from './battle-recap.js';

// A recorded 3v3 match (play --seed 1) as seat player1 reads it: its feed and its board at named moments.
const recorded = JSON.parse(readFileSync(new URL('./battle-recap.fixture.json', import.meta.url), 'utf8'));
const NOW = Date.parse('2026-11-17T12:00:00Z');
const hoursAgo = hours => new Date(NOW - hours * 3600000).toISOString();

function at(moment, seen, extras = {}) {
  const { sequence, board } = recorded.boards[moment];
  const feed = recorded.feed.filter(entry => entry.sequence <= sequence);
  const view = { seat: 'player1', board, waitingFor: 'Speed', answerBy: hoursAgo(-40), lastMoveAt: hoursAgo(5),
    seen: seen === null ? null : { sequence: seen, at: hoursAgo(6) }, over: false, outcome: null,
    reactions: { available: true, recent: [] }, ...extras };
  return buildRecap({ view, feed, seen, now: NOW, catalogue: recorded.catalogue, opponent: 'Mara' });
}
const rounds = recap => recap.rounds.map(one => one.round);
const newCasts = recap => recap.rounds.flatMap(one => one.casts.filter(cast => cast.unseen).map(cast => cast.sequence));

test('one cast unseen is the one marked new, with the round it closed and the round under way told', () => {
  const recap = at('round2Tempo', 35);

  assert.deepEqual(rounds(recap), [1, 2]);
  assert.deepEqual(newCasts(recap), [37]);
  assert.equal(recap.rounds[0].casts.find(cast => cast.sequence === 37).spell, 'Brace');
  assert.equal(recap.rounds[0].casts.filter(cast => !cast.unseen).length, 5);
});

test('a round crossed while away is told in full, every cast in it new, and neither round folded', () => {
  const recap = at('round2Tempo', 14);

  assert.deepEqual(rounds(recap), [1, 2]);
  assert.deepEqual(newCasts(recap), [27, 29, 31, 33, 35, 37]);
  assert.deepEqual(recap.rounds.map(one => [one.state, one.folded]), [['completed', false], ['under way', false]]);
  assert.equal(recap.unseen, 6);
});

test('the count of what is new counts every line marked new: each mutation revealed and each creature defeated', () => {
  const recap = at('round4Tempo', 14);

  const lines = recap.rounds.flatMap(one => [...one.mutations, ...one.casts, ...one.skipped, ...one.defeated]);
  assert.equal(recap.unseen, lines.filter(line => line.unseen).length);
  assert.equal(recap.unseen, 22);
});

test('a round the feed holds nothing of is told as not recorded, never as a quiet round', () => {
  const { sequence, board } = recorded.boards.round4Tempo;
  const feed = recorded.feed.filter(entry => entry.sequence > 114 && entry.sequence <= sequence);

  const recap = buildRecap({ view: { seat: 'player1', board }, feed, seen: 120, now: NOW, catalogue: recorded.catalogue });

  assert.deepEqual(recap.rounds.map(one => [one.round, one.recorded]), [[3, false], [4, true]]);
});

test('three rounds crossed are all told, most recent last, the older ones folded', () => {
  const recap = at('round4Tempo', 14);

  assert.deepEqual(rounds(recap), [1, 2, 3, 4]);
  assert.deepEqual(recap.rounds.map(one => one.folded), [true, true, false, false]);
  assert.ok(recap.rounds.slice(0, 3).every(one => one.unseen));
  assert.equal(recap.rounds[2].mutations.map(one => `${one.creature} · ${one.mutation}`).join(', '),
    'Creature 2 · Predator, Creature 1 · Ethereal, Creature 5 · Predator, Creature 4 · Brute');
  assert.ok(recap.rounds[2].mutations.every(one => one.unseen));
});

test('a creature defeated while away is marked on its team and in the round that defeated it', () => {
  const recap = at('round9Tempo', 263);

  const fallen = recap.teams.opponent.find(one => one.id === '4');
  assert.deepEqual([fallen.defeated, fallen.fellWhileAway, fallen.health], [true, true, 0]);
  assert.deepEqual(recap.rounds.find(one => one.round === 8).defeated, [{ creature: 'Creature 4', side: 'Opponent', unseen: true }]);
  assert.deepEqual(recap.where.sides, { you: { standing: 3, total: 3, health: 59 }, opponent: { standing: 2, total: 3, health: 41 } });
  assert.equal(recap.where.leader, 'you');
});

test('the difference each creature took is measured over the span the sheet tells', () => {
  const recap = at('round9Tempo', 263);

  assert.equal(recap.span, 8);
  assert.deepEqual(recap.teams.you.map(one => [one.id, one.change]), [['1', 0], ['2', -6], ['3', -1]]);
  assert.equal(healthChangeText(-6), '−6 HP');
  assert.equal(healthChangeText(4), '+4 HP');
  assert.equal(healthChangeText(0), '');
});

test('a game that ended while the player was away says how it ended and asks nothing', () => {
  const recap = at('end', 520, { over: true, outcome: { winner: 'player2', reason: 'Elimination' }, waitingFor: null, answerBy: null });

  assert.equal(recap.where.over, true);
  assert.equal(recap.where.result, 'Player 2 wins · the last team standing.');
  assert.deepEqual(rounds(recap), [16, 17]);
  assert.deepEqual(recap.rounds.at(-1).result, { text: 'Player 2 wins · the last team standing.', unseen: true });
  assert.equal(recap.rounds.at(-1).state, 'completed');
  assert.ok(recap.teams.you.every(one => one.defeated));
  assert.equal(recap.asked.question, null);
  assert.equal(recap.where.timeLeft, null);
});

test('the seat\'s own choices still in force are told: its tempos this round and the mutations it bought last', () => {
  const recap = at('round4Combat', 124, { waitingFor: 'TieOrder' });

  assert.deepEqual(recap.chosen.tempos, [
    { creature: 'Creature 1', tempo: 'Precision' }, { creature: 'Creature 2', tempo: 'Precision' }, { creature: 'Creature 3', tempo: 'Instinct' }]);
  assert.deepEqual(recap.chosen.mutations, { round: 3, picks: [{ creature: 'Creature 2', mutation: 'Predator' }, { creature: 'Creature 1', mutation: 'Ethereal' }] });
  assert.deepEqual(recap.chosen.spells, []);
});

test('a spell declared face down is the seat\'s own and is told; the other seat\'s hidden choices never are', () => {
  const { board } = recorded.boards.round4Combat;
  const declared = { ...board, intents: [{ actor: 1, spell: 'spell:healing_screech:v1' }] };
  const theirs = { sequence: 128, round: 4, subPhase: 'Speed', event: { kind: 'SpeedChoiceSubmitted', roundId: 4, slot: 'Player2', choice: { creature: 4, speed: 'Quick' } } };
  const feed = [...recorded.feed.filter(entry => entry.sequence <= 129), theirs];

  const recap = buildRecap({ view: { seat: 'player1', board: declared, seen: null }, feed, seen: 129, now: NOW, catalogue: recorded.catalogue });

  assert.deepEqual(recap.chosen.spells, [{ creature: 'Creature 1', spell: 'Vital Echo' }]);
  assert.ok(recap.chosen.tempos.every(one => one.creature !== 'Creature 4'));
});

test('the seat\'s own submissions are found whatever case its slot is written in, and none without a slot', () => {
  const { board } = recorded.boards.round2Tempo;
  const feed = recorded.feed.filter(entry => entry.sequence <= 51);
  const read = slot => buildRecap({ view: { seat: 'player1', board: { ...board, slot } }, feed, seen: 51, now: NOW, catalogue: recorded.catalogue }).chosen.mutations;

  assert.deepEqual(read('player1'), { round: 1, picks: [{ creature: 'Creature 2', mutation: 'Brute' }, { creature: 'Creature 1', mutation: 'Warped' }] });
  assert.equal(read(undefined), null);
});

test('a spell or mutation the catalogue does not name is said as unknown, never by its content id', () => {
  const { board } = recorded.boards.round4Combat;
  const declared = { ...board, intents: [{ actor: 1, spell: 'spell:not_in_catalogue:v1' }] };

  const recap = buildRecap({ view: { seat: 'player1', board: declared }, feed: recorded.feed.filter(entry => entry.sequence <= 129), seen: 129, now: NOW, catalogue: { cards: [], packages: [] } });

  assert.deepEqual(recap.chosen.spells, [{ creature: 'Creature 1', spell: 'Unknown spell' }]);
  assert.ok(recap.chosen.mutations.picks.every(one => one.mutation === 'Unknown mutation'));
});

test('with nothing unseen the last completed round and the round under way are still told, nothing marked new', () => {
  const recap = at('round4Combat', 129);

  assert.deepEqual(rounds(recap), [3, 4]);
  assert.equal(recap.unseen, 0);
  assert.ok(recap.rounds.every(one => !one.unseen && one.casts.every(cast => !cast.unseen)));
  assert.equal(recap.rounds[0].casts.length, 6);
});

test('a seat with no last visit is told the recent rounds with nothing marked new', () => {
  const recap = at('round4Tempo', null);

  assert.deepEqual(rounds(recap), [3, 4]);
  assert.equal(recap.unseen, 0);
});

test('the first round has no completed round to tell, only the round under way', () => {
  const recap = at('round1Tempo', 14);

  assert.deepEqual(rounds(recap), [1]);
  assert.equal(recap.rounds[0].state, 'under way');
  assert.equal(recap.where.round, 1);
  assert.equal(recap.where.step, 'Command');
});

test('where the game is: against whom, the last move so many hours ago and the time left to answer', () => {
  const recap = at('round4Tempo', 14);

  assert.equal(recap.where.opponent, 'Mara');
  assert.deepEqual(recap.where.lastMove, { at: hoursAgo(5), ago: '5 h ago' });
  assert.equal(recap.where.timeLeft, '1 d 16 h');
});

test('what the seat is asked now is said in the push notice\'s words', () => {
  assert.equal(at('round4Tempo', 14).asked.question, 'Choose a tempo');
  assert.equal(questionText('Action'), 'Choose a spell');
  assert.equal(questionText('Evolution'), 'Choose your mutations');
  assert.equal(questionText(null), null);
});

test('the opponent\'s reactions are told, those sent since the last visit marked new, the seat\'s own left out', () => {
  const recent = [
    { sequence: 1, seat: 'player2', reaction: 'good-luck', at: hoursAgo(9) },
    { sequence: 2, seat: 'player1', reaction: 'thanks', at: hoursAgo(8) },
    { sequence: 3, seat: 'player2', reaction: 'nice-move', at: hoursAgo(2) },
    { sequence: 4, seat: 'player2', reaction: 'not-a-reaction', at: hoursAgo(1) }];

  const recap = at('round4Tempo', 14, { reactions: { available: true, recent } });

  assert.deepEqual(recap.asked.reactions, [
    { glyph: '🍀', label: 'Good luck!', ago: '9 h ago', unseen: false },
    { glyph: '🎯', label: 'Nice move!', ago: '2 h ago', unseen: true }]);
});

test('the recap carries the journal\'s public words only, never a raw event', () => {
  const recap = at('round4Combat', 14);

  assert.doesNotMatch(JSON.stringify(recap), /SpeedChoiceSubmitted|CombatActionResolved|spell:|tier:/);
});
