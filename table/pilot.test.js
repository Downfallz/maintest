import { test } from 'node:test';
import assert from 'node:assert/strict';
import { SEATABLE, earliestRound, newestFirst, pilotTransport, said, seatRows, swapAsked, whereItIs } from './pilot.js';

const view = {
  round: 3,
  subPhase: 'IntentSelection',
  over: false,
  seats: [
    { slot: 'player1', seated: 'human:mk', hasPerson: true, waitingFor: 'Intent', waitingCreature: 2, pending: null },
    { slot: 'player2', seated: 'Greedy', hasPerson: false, waitingFor: null, waitingCreature: null, pending: { to: 'Random', round: 5 } },
  ],
};

test('a seat reads as who is playing it, what it is being asked, and what it will become', () => {
  const [one, two] = seatRows(view);

  assert.equal(one.seated, 'human:mk');
  assert.equal(one.asked, 'Intent · creature 2');
  assert.equal(one.pending, null);
  assert.equal(two.seated, 'Greedy');
  assert.equal(two.asked, null);
  assert.equal(two.pending, 'Random from round 5');
});

// The point of showing it at all: a seat holds one pending swap and a second replaces it, so an operator who
// cannot see the pending one overwrites it without knowing.
test('the pending swap is shown, because asking for another one replaces it', () => {
  assert.equal(seatRows(view)[1].pending, 'Random from round 5');
  assert.equal(seatRows({ seats: [{ slot: 'player1', pending: null }] })[0].pending, null);
});

test('a seat nobody joined cannot be handed back to a person', () => {
  const [one, two] = seatRows(view);

  assert.equal(one.canTakeThePerson, true);
  assert.equal(two.canTakeThePerson, false);
  assert.match(swapAsked('player2', 'person', 9, view).problem, /Nobody ever joined player2/);
  assert.equal(swapAsked('player1', 'person', 9, view).problem, undefined);
});

// A seat changes hands at the top of a round, so the round being played is already too late. The host checks
// this again under its own lock; here it is so a typo reads as a sentence rather than as a 409.
test('a swap must name a round the match has not reached', () => {
  assert.equal(earliestRound(view), 4);
  assert.match(swapAsked('player1', 'greedy', 3, view).problem, /Round 3 is played or being played/);
  assert.match(swapAsked('player1', 'greedy', 1, view).problem, /Name 4 or later/);
  assert.deepEqual(swapAsked('player1', 'greedy', 4, view), { slot: 'player1', agent: 'greedy', round: 4 });
});

test('a round that is not a whole number at least one is refused before it is sent', () => {
  assert.match(swapAsked('player1', 'greedy', 0, view).problem, /whole number, 1 or more/);
  assert.match(swapAsked('player1', 'greedy', 'soon', view).problem, /whole number, 1 or more/);
  assert.match(swapAsked('', 'greedy', 4, view).problem, /Choose a seat/);
  assert.match(swapAsked('player1', '', 4, view).problem, /Choose who takes it/);
});

test('a match with no round yet takes a swap from the first', () => {
  assert.equal(earliestRound({ round: null, seats: [] }), 1);
  assert.equal(earliestRound(null), 1);
});

test('the line about the match says the round, or that it is over', () => {
  assert.equal(whereItIs(view), 'Round 3 · IntentSelection');
  assert.equal(whereItIs({ over: true }), 'The match is over.');
  assert.equal(whereItIs(null), 'Connecting…');
});

// A refusal is the host saying something true about the match, not a failure of the page, so it keeps the
// host's own words.
test('what the page says after a swap carries the host answer either way', () => {
  const taken = said({ ok: true, status: 200, body: { slot: 'player1', from: 'human:mk', to: 'Greedy', round: 6 } });
  assert.equal(taken.refused, false);
  assert.equal(taken.line, 'player1: human:mk → Greedy, from round 6.');

  const refused = said({ ok: false, status: 409, body: { error: 'Table.SwapMidRound', message: 'Round 3 is being played; …' } });
  assert.equal(refused.refused, true);
  assert.equal(refused.line, 'Round 3 is being played; …');
  assert.match(said({ ok: false, status: 500, body: null }).line, /refused it \(500\)/);
});

test('the pilot speaks for the pilot: its token, its two routes, and nothing of a seat', async () => {
  const asked = [];
  const transport = pilotTransport('token-of-the-pilot', async (path, options) => {
    asked.push({ path, method: options.method, token: options.headers['X-Seat-Token'], body: options.body });
    return { status: 200, ok: true, text: async () => '{}' };
  });

  await transport.view();
  await transport.swap('player2', 'greedy', 7);

  assert.deepEqual(asked.map(call => `${call.method} ${call.path}`), ['GET /api/pilot', 'POST /api/pilot/seats/player2']);
  assert.ok(asked.every(call => call.token === 'token-of-the-pilot'));
  assert.deepEqual(JSON.parse(asked[1].body), { agent: 'greedy', round: 7 });
});

test('a pilot page without a token refuses to be built rather than polling a 403 for ever', () => {
  assert.throws(() => pilotTransport(''), /token the host printed/);
});

test('what a pilot may seat names the person first and the exploits last', () => {
  assert.equal(SEATABLE[0].value, 'person');
  assert.ok(SEATABLE.some(seat => seat.value === 'greedy'));
  assert.ok(SEATABLE.some(seat => seat.value.includes('search-4')));
});

// The page polls on a timer and asks for its own view the moment a swap lands, so two are in flight at once
// and the host answers them concurrently. Drawing them in the order they come back puts the seat back as it
// was: the pending swap blinks out of the page whose whole job is to show it, and the operator replaces a
// swap they cannot see.
test('an answer older than the last one drawn is dropped rather than drawn over it', () => {
  const answers = newestFirst();

  const slow = answers.take();
  const quick = answers.take();

  assert.equal(answers.keep(quick), true);
  assert.equal(answers.keep(slow), false);
});

test('answers that come back in the order they were asked are all drawn', () => {
  const answers = newestFirst();

  assert.equal(answers.keep(answers.take()), true);
  assert.equal(answers.keep(answers.take()), true);
  assert.equal(answers.keep(answers.take()), true);
});

// A poll whose answer was already drawn cannot draw itself again: an error path that fell through to the same
// ticket would redraw a view the page has moved past.
test('the same answer is drawn once', () => {
  const answers = newestFirst();

  const only = answers.take();

  assert.equal(answers.keep(only), true);
  assert.equal(answers.keep(only), false);
});
