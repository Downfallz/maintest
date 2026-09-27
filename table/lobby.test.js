import { test } from 'node:test';
import assert from 'node:assert/strict';
import { lobbyTransport, openAsked, said, signInNeeded, tableRows } from './lobby.js';

function answering(status, body, seen = []) {
  return async (path, options) => {
    seen.push({ path, options });
    return { status, ok: status >= 200 && status < 300, text: async () => (body === undefined ? '' : JSON.stringify(body)) };
  };
}

test('the transport carries the operator token when it has one and the browser cookies when it does not', async () => {
  const seen = [];
  await lobbyTransport('op-token', answering(200, { tables: [] }, seen)).list();
  await lobbyTransport(null, answering(200, { tables: [] }, seen)).list();

  assert.equal(seen[0].options.headers['X-Seat-Token'], 'op-token');
  assert.equal(seen[1].options.headers['X-Seat-Token'], undefined);
});

test('opening a table posts JSON and closing one names it in the path', async () => {
  const seen = [];
  const transport = lobbyTransport('op-token', answering(201, { id: 'x' }, seen));

  await transport.open({ player1: 'person', player2: 'greedy' });
  await transport.close('20260927-1200-ab12');

  assert.equal(seen[0].options.method, 'POST');
  assert.equal(seen[0].options.headers['Content-Type'], 'application/json');
  assert.deepEqual(JSON.parse(seen[0].options.body), { player1: 'person', player2: 'greedy' });
  assert.equal(seen[1].options.method, 'DELETE');
  assert.equal(seen[1].path, '/api/tables/20260927-1200-ab12');
});

test('a sign-in is needed only when the host answers 401 with where to sign in', () => {
  assert.equal(signInNeeded({ status: 401, body: { login: '/.auth/login/aad?post_login_redirect_uri=/lobby' } }), '/.auth/login/aad?post_login_redirect_uri=/lobby');
  assert.equal(signInNeeded({ status: 403, body: { message: 'token' } }), null);
  assert.equal(signInNeeded({ status: 401, body: { message: 'no door' } }), null);
  assert.equal(signInNeeded({ status: 200, body: { tables: [] } }), null);
});

test('a table needs a person in at least one seat', () => {
  assert.deepEqual(openAsked({ player1: 'greedy', player2: 'greedy' }), { problem: 'At least one seat is a person: two bots need no table.' });
  assert.deepEqual(openAsked({ player1: '', player2: 'greedy' }), { problem: 'Choose who sits in each seat.' });
});

test('initials and a handover round travel only when given, and a round is a whole number', () => {
  assert.deepEqual(openAsked({ player1: 'person', player2: 'greedy', who: '  ', handover: '' }), { player1: 'person', player2: 'greedy' });
  assert.deepEqual(openAsked({ player1: 'person', player2: 'person', who: ' mk ', handover: '7' }), { player1: 'person', player2: 'person', who: 'mk', handover: 7 });
  assert.deepEqual(openAsked({ player1: 'person', player2: 'greedy', handover: '0' }), { problem: 'A handover round is a whole number, 1 or more.' });
  assert.deepEqual(openAsked({ player1: 'person', player2: 'greedy', handover: 'two' }), { problem: 'A handover round is a whole number, 1 or more.' });
});

test('a table reads as its round while it is played, and as finished once written', () => {
  const rows = tableRows({
    tables: [
      { id: 'a', round: 3, over: false, finished: false, seats: [{ slot: 'player1', seated: 'human:mk', code: 'K7F2Q9XM', join: '/j/K7F2Q9XM', link: '/?player1=t1' }, { slot: 'player2', seated: 'Greedy' }], pilot: '/pilot?token=p', session: '/session/a' },
      { id: 'b', over: true, finished: true, seats: [], pilot: '/pilot?token=q', session: '/session/b', location: 'runs/playtest/b' },
      { id: 'c', over: true, finished: false, seats: [], pilot: '/pilot?token=r', session: '/session/c' },
      { id: 'd', over: false, finished: false, seats: [], pilot: '/pilot?token=s', session: '/session/d' },
    ],
  });

  assert.equal(rows[0].state, 'Round 3');
  assert.equal(rows[0].seats[0].code, 'K7F2Q9XM');
  assert.equal(rows[0].seats[1].code, null);
  assert.equal(rows[0].session, null, 'the session page is offered once the run is written');
  assert.equal(rows[0].closable, true);
  assert.equal(rows[1].state, 'Finished');
  assert.equal(rows[1].session, '/session/b');
  assert.equal(rows[1].closable, false);
  assert.equal(rows[2].state, 'Over, being written');
  assert.equal(rows[3].state, 'Waiting for the first decision');
});

test('an empty or missing list draws nothing', () => {
  assert.deepEqual(tableRows(null), []);
  assert.deepEqual(tableRows({}), []);
});

test('what the page says after an opening names the codes to read out, or the refusal', () => {
  assert.deepEqual(
    said({ ok: true, body: { id: 'a', seats: [{ slot: 'player1', code: 'K7F2Q9XM' }, { slot: 'player2' }] } }),
    { refused: false, line: 'Table a opened: player1 K7F2Q9XM.' },
  );
  assert.deepEqual(said({ ok: false, status: 409, body: { message: 'This host has 16 tables under way.' } }), { refused: true, line: 'This host has 16 tables under way.' });
  assert.deepEqual(said({ ok: false, status: 500, body: null }), { refused: true, line: 'The host refused it (500).' });
});
