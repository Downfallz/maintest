import { test } from 'node:test';
import assert from 'node:assert/strict';
import { cutAsked, roundChoices, scenarioRows, shareLink } from './admin-scenarios.js';

test('a scenario is listed by its title, where it was cut from, and its link', () => {
  const [row] = scenarioRows({ scenarios: [{ id: 'sc-1', title: 'Bleed after a stun', goal: 'Keep it alive', session: 's 1', round: 4, seat: 'player2', link: '/battle?scenario=sc-1' }] });

  assert.deepEqual(row, { id: 'sc-1', title: 'Bleed after a stun', goal: 'Keep it alive', facts: 'session s 1 · round 4 · played as player2', session: '/session/s%201', link: '/battle?scenario=sc-1' });
  assert.equal(scenarioRows({ scenarios: [{ id: 'sc-2', round: 3, seat: 'player1', link: '/x' }] })[0].title, 'Round 3');
});

test('the rounds a session offers are the chooser\'s options, and a journal that stopped says where', () => {
  assert.deepEqual(roundChoices({ rounds: [1, 2, 3] }), { rounds: [1, 2, 3], note: '' });
  assert.equal(roundChoices({ rounds: [1, 2], stoppedBy: 'Line 40 diverged.' }).note, 'The journal stopped rebuilding after round 2: Line 40 diverged.');
  assert.equal(roundChoices({ rounds: [] }).note, 'This session offers no round to start from.');
});

test('a cut sends its session, round and seat, and a title and goal only when given', () => {
  assert.deepEqual(cutAsked({ session: 's', round: '4', seat: 'player2', title: '  ', goal: '' }), { body: { session: 's', round: 4, seat: 'player2' } });
  assert.deepEqual(cutAsked({ session: 's', round: '2', title: ' A title ', goal: 'Try it' }).body, { session: 's', round: 2, seat: 'player1', title: 'A title', goal: 'Try it' });
});

test('a cut without a round, with another seat, or with words too long is not sent', () => {
  assert.equal(cutAsked({ session: 's', round: '' }).problem, 'Choose a round to start from.');
  assert.equal(cutAsked({ session: 's', round: '2', seat: 'player3' }).problem, 'Choose the seat the player plays.');
  assert.match(cutAsked({ session: 's', round: '2', title: 'x'.repeat(81) }).problem, /80 characters/);
  assert.match(cutAsked({ session: 's', round: '2', goal: 'x'.repeat(301) }).problem, /300 characters/);
});

test('the link to copy is absolute, on this host', () => {
  assert.equal(shareLink('/battle?scenario=sc-1', 'https://play.example.com'), 'https://play.example.com/battle?scenario=sc-1');
});
