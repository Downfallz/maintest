import { test } from 'node:test';
import assert from 'node:assert/strict';
import { diagnosisFacts, hiddenLines, seatLine } from './admin-diagnosis.js';

const NOW = Date.parse('2026-11-14T06:00:00Z');
const answer = {
  id: 't-1', status: 'open', held: true, begun: true, over: false, round: 5, answerBy: '2026-11-15T06:00:00Z',
  seats: [{ slot: 'player1', name: 'Mark', kind: 'person', playedByBot: false, asked: null },
    { slot: 'player2', name: 'Greedy', kind: 'bot', playedByBot: true, asked: { kind: 'Action', creature: 5 } }],
  journal: { entries: 61, lastWritten: '2026-11-14T04:00:00Z', tail: [{ kind: 'Speed', seat: 'player1' }, { kind: 'Action', seat: 'player1' }] },
  basis: { content: 'f3e8a42ddbd02c113f855a65', rules: '3v3, 2 energy, 20 rounds, AtActivation', engine: 'abc123', refusedUnder: null },
  turns: { queued: true, lastFailure: null, letters: 1, lastError: 'TimeoutException: the bot took too long' },
};

test('a seat says who sits there and what it is asked', () => {
  assert.equal(seatLine(answer.seats[0]), 'player1: Mark (person) · asked nothing');
  assert.equal(seatLine(answer.seats[1]), 'player2: Greedy (bot) · asked Action for creature 5');
  assert.equal(seatLine({ slot: 'player1', name: 'Jo', kind: 'person', playedByBot: true, asked: { kind: 'Speed' } }), 'player1: Jo (person, played by a bot now) · asked Speed');
});

test('the facts say where the table stands, who is asked, its journal, what it is played on and its bot turns', () => {
  const facts = Object.fromEntries(diagnosisFacts(answer, NOW));

  assert.equal(facts.State, 'open · held by this process · begun · round 5');
  assert.equal(facts['Seat 2'], 'player2: Greedy (bot) · asked Action for creature 5');
  assert.equal(facts['Answer by'], '2026-11-15 06:00 UTC');
  assert.equal(facts.Journal, '61 entries · last written 2 h ago');
  assert.equal(facts['Last lines'], 'Speed (player1) → Action (player1)');
  assert.equal(facts['Played on'], '3v3, 2 energy, 20 rounds, AtActivation · content f3e8a42ddbd0 · engine abc123');
  assert.equal(facts['Bot turns'], '1 dead letter · last letter: TimeoutException: the bot took too long');
});

test('a table on a host that takes its turns in process says so, and one refused under an engine says which', () => {
  const facts = Object.fromEntries(diagnosisFacts({ ...answer, basis: { ...answer.basis, refusedUnder: 'old · hash' }, turns: { queued: false, lastFailure: 'boom' } }, NOW));

  assert.equal(facts['Bot turns'], 'turns taken in this process · last failure: boom');
  assert.equal(facts['Refused under'], 'old · hash');
});

test('what each seat holds face down is one line a seat, and an empty one says so', () => {
  const lines = hiddenLines({ seats: [
    { slot: 'player1', speeds: [{ creature: 1, speed: 'Quick' }], intents: [{ creature: 2, spell: 'spell:strike:v1' }], picks: [], held: [{ kind: 'Speed', creature: 3, speed: 'Standard' }] },
    { slot: 'player2', speeds: [], intents: [], picks: [], held: [] },
  ] });

  assert.deepEqual(lines, ['player1: creature 1 Quick · creature 2 intends spell:strike:v1 · holds Speed for creature 3 Standard', 'player2: nothing face down']);
});
