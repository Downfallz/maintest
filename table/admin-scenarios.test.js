import { test } from 'node:test';
import assert from 'node:assert/strict';
import { cutAsked, listingToggle, roundChoices, scenarioRows, shareLink } from './admin-scenarios.js';

test('a scenario is listed by its title, where it was cut from, and its link', () => {
  const [row] = scenarioRows({ scenarios: [{ id: 'sc-1', title: 'Bleed after a stun', goal: 'Keep it alive', session: 's 1', round: 4, seat: 'player2', link: '/battle?scenario=sc-1' }] });

  assert.deepEqual(row, { id: 'sc-1', title: 'Bleed after a stun', goal: 'Keep it alive', facts: 'session s 1 · round 4 · played as player2', session: '/session/s%201', link: '/battle?scenario=sc-1', listed: false, shipped: false, tutorial: null, bug: null });
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

test('a listed scenario says so and offers to take it off Practice; an unlisted one offers to list it', () => {
  const [listed, unlisted] = scenarioRows({ scenarios: [{ id: 'sc-1', round: 2, seat: 'player1', link: '/a', listed: true }, { id: 'sc-2', round: 3, seat: 'player1', link: '/b' }] });

  assert.deepEqual(listingToggle(listed), { label: 'take off Practice', state: 'Listed under Practice', body: { listed: false } });
  assert.deepEqual(listingToggle(unlisted), { label: 'list under Practice', state: '', body: { listed: true } });
});

test('a scenario the repository ships names its file, and links no session of this host', () => {
  const [row] = scenarioRows({ scenarios: [{ id: 'sc-repo-greedy', title: 'A greedy mirror', session: 'elsewhere', round: 3, seat: 'player1', source: 'repository', file: 'greedy.json', link: '/battle?scenario=sc-repo-greedy' }] });

  assert.equal(row.shipped, true);
  assert.equal(row.facts, 'scenarios/greedy.json · round 3 · played as player1');
  assert.equal(row.session, null);
});

test('a tutorial of the series carries its place in it, and a scenario that is none carries null', () => {
  const [tutorial, plain] = scenarioRows({ scenarios: [
    { id: 'sc-repo-tutorial-a', round: 2, seat: 'player1', source: 'repository', file: 'tutorials/a.json', link: '/a', tutorial: 1 },
    { id: 'sc-repo-greedy', round: 3, seat: 'player1', source: 'repository', file: 'greedy.json', link: '/b', tutorial: null },
  ] });

  assert.equal(tutorial.tutorial, 1);
  assert.equal(plain.tutorial, null);
});

test('a bug scenario carries the issue it reproduces, and a scenario that is none carries null', () => {
  const [bug, plain] = scenarioRows({ scenarios: [
    { id: 'sc-repo-bug-812-bleed', round: 4, seat: 'player2', source: 'repository', file: 'bugs/812-bleed.json', link: '/a', bug: 812 },
    { id: 'sc-repo-greedy', round: 3, seat: 'player1', source: 'repository', file: 'greedy.json', link: '/b', bug: null },
  ] });

  assert.equal(bug.bug, 812);
  assert.equal(bug.facts, 'scenarios/bugs/812-bleed.json · round 4 · played as player2');
  assert.equal(plain.bug, null);
});
