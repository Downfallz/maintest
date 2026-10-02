import { test } from 'node:test';
import assert from 'node:assert/strict';
import { OPERATOR_KEY, adminTransport, defaultOpponent, deletionSaid, filenameOf, openAsked, operatorToken, said, seatable, sessionRows, signInNeeded, tableRows } from './admin.js';

function answering(status, body, seen = []) {
  return async (path, options) => {
    seen.push({ path, options });
    return { status, ok: status >= 200 && status < 300, text: async () => (body === undefined ? '' : JSON.stringify(body)) };
  };
}

test('the transport carries the operator token when it has one and the browser cookies when it does not', async () => {
  const seen = [];
  await adminTransport('op-token', answering(200, { tables: [] }, seen)).list();
  await adminTransport(null, answering(200, { tables: [] }, seen)).list();

  assert.equal(seen[0].options.headers['X-Seat-Token'], 'op-token');
  assert.equal(seen[1].options.headers['X-Seat-Token'], undefined);
});

test('opening a table posts JSON and closing one names it in the path', async () => {
  const seen = [];
  const transport = adminTransport('op-token', answering(201, { id: 'x' }, seen));

  await transport.open({ player1: 'person', player2: 'greedy' });
  await transport.close('20260927-1200-ab12');

  assert.equal(seen[0].options.method, 'POST');
  assert.equal(seen[0].options.headers['Content-Type'], 'application/json');
  assert.deepEqual(JSON.parse(seen[0].options.body), { player1: 'person', player2: 'greedy' });
  assert.equal(seen[1].options.method, 'DELETE');
  assert.equal(seen[1].path, '/api/tables/20260927-1200-ab12');
  assert.equal(seen[1].options.headers['Content-Type'], 'application/json', 'a write without it is refused by the same-origin fence');
  assert.equal(seen[1].options.body, '{}');
});

test('a sign-in is needed only when the host answers 401 with where to sign in', () => {
  assert.equal(signInNeeded({ status: 401, body: { login: '/.auth/login/aad?post_login_redirect_uri=/admin' } }), '/.auth/login/aad?post_login_redirect_uri=/admin');
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
      { id: 'e', over: false, finished: false, waiting: true, seats: [], pilot: '/pilot?token=t', session: '/session/e' },
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
  assert.equal(rows[3].state, 'Playing');
  assert.equal(rows[4].state, 'Waiting for the players');
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

test('the sessions routes list, delete by ids and export one or several as a zip', async () => {
  const seen = [];
  const transport = adminTransport('op-token', answering(200, { sessions: [] }, seen));

  await transport.sessions();
  await transport.deleteSessions(['a', 'b']);

  assert.equal(seen[0].path, '/api/sessions');
  assert.equal(seen[0].options.method, 'GET');
  assert.equal(seen[1].path, '/api/sessions');
  assert.equal(seen[1].options.method, 'DELETE');
  assert.deepEqual(JSON.parse(seen[1].options.body), { ids: ['a', 'b'] });
  assert.equal(seen[1].options.headers['Content-Type'], 'application/json');
});

test('an export hands back the bytes and the file name the host gave, or the refusal', async () => {
  const zipping = async (path, options) => ({
    status: 200, ok: true,
    headers: { get: name => (name === 'Content-Disposition' ? 'attachment; filename="20260927-1200-ab12.zip"' : null) },
    blob: async () => ({ size: 3, path, method: options.method }),
    text: async () => '',
  });
  const one = await adminTransport('op-token', zipping).exportSession('20260927-1200-ab12');
  assert.equal(one.ok, true);
  assert.equal(one.filename, '20260927-1200-ab12.zip');
  assert.equal(one.blob.path, '/api/sessions/20260927-1200-ab12/export');
  assert.equal(one.blob.method, 'GET');

  const several = await adminTransport('op-token', zipping).exportSessions(['a', 'b']);
  assert.equal(several.blob.path, '/api/sessions/export');
  assert.equal(several.blob.method, 'POST');

  const refused = await adminTransport('op-token', answering(404, { message: 'Nothing to export.' })).exportSession('x');
  assert.deepEqual(refused, { status: 404, ok: false, body: { message: 'Nothing to export.' } });
  assert.equal(filenameOf(null), 'sessions.zip');
});

test('the pickers offer the person and then the bots the host puts forward, and open against the first featured one', () => {
  const answer = {
    agents: [
      { value: 'heuristic:learning/weights/search-19.json', label: 'search-19 — the strongest', featured: true },
      { value: 'greedy', label: 'Greedy — the baseline', featured: false },
    ],
  };

  const offered = seatable(answer);

  assert.equal(offered[0].value, 'person');
  assert.deepEqual(offered.slice(1).map(agent => agent.value), ['heuristic:learning/weights/search-19.json', 'greedy']);
  assert.equal(defaultOpponent(answer), 'heuristic:learning/weights/search-19.json');
  assert.deepEqual(seatable(null).map(agent => agent.value), ['person', 'greedy', 'random'], 'before the host answers');
  assert.equal(defaultOpponent(null), 'greedy');
});

test('a recorded session reads as live, finished or not finished, with its players and where it is', () => {
  const rows = sessionRows({
    sessions: [
      { id: 'a', createdAt: '2026-09-27T12:00:00Z', player1: 'human:mk', player2: 'Greedy', matches: 0, steps: 12, live: true, over: false, session: '/session/a', export: '/api/sessions/a/export', location: 'runs/playtest/a' },
      { id: 'b', createdAt: '2026-09-26T12:00:00Z', player1: 'human:mk', player2: 'Greedy', matches: 1, steps: 40, live: false, over: true, session: '/session/b', export: '/api/sessions/b/export' },
      { id: 'c', matches: 0, steps: 0, live: false, over: false, session: '/session/c', export: '/api/sessions/c/export' },
    ],
  });

  assert.equal(rows[0].state, 'live on this host');
  assert.equal(rows[0].players, 'human:mk vs Greedy');
  assert.equal(rows[0].live, true);
  assert.equal(rows[0].location, 'runs/playtest/a');
  assert.equal(rows[1].state, 'finished');
  assert.equal(rows[1].steps, 40);
  assert.equal(rows[2].state, 'not finished');
  assert.equal(rows[2].players, 'no manifest');
  assert.equal(rows[2].when, 'unknown date');
  assert.deepEqual(sessionRows(null), []);
});

test('what the page says after a deletion counts what went and names what was not there', () => {
  assert.deepEqual(deletionSaid({ ok: true, body: { deleted: ['a', 'b'], missing: [] } }), { refused: false, line: '2 sessions deleted.' });
  assert.deepEqual(deletionSaid({ ok: true, body: { deleted: ['a'], missing: ['z'] } }), { refused: false, line: '1 session deleted; not found: z.' });
  assert.deepEqual(deletionSaid({ ok: false, status: 404, body: { message: 'This host records nothing.' } }), { refused: true, line: 'This host records nothing.' });
});

test('the operator token comes from the link and is kept, or from what was kept, or is nothing', () => {
  const stored = new Map();
  const storage = { getItem: key => stored.get(key) ?? null, setItem: (key, value) => stored.set(key, value) };

  assert.equal(operatorToken('?token=abc', storage), 'abc');
  assert.equal(stored.get(OPERATOR_KEY), 'abc');
  assert.equal(operatorToken('', storage), 'abc');
  assert.equal(operatorToken('', null), null);
  assert.equal(operatorToken('?token=def', { getItem: () => { throw new Error('private'); }, setItem: () => { throw new Error('private'); } }), 'def');
});
