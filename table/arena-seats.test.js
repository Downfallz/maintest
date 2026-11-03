import test from 'node:test';
import assert from 'node:assert/strict';
import { arenaSeats, seatPath } from './arena-seats.js';
import { nextPath, signInPath } from './account.js';
const choices = [{ table: { id: 'one' }, seats: [{ seat: 'player1', token: 'first' }, { seat: 'player2', token: 'second' }] }, { table: { id: 'two' }, seats: [{ seat: 'player1', token: 'third' }] }];
const storage = { getItem: key => key === 'downfall.table.seats' ? JSON.stringify({ player1: 'first', player2: 'second' }) : '[]', setItem: () => assert.fail('Discovery must not write storage') };
function host(calls, invitations = []) { return async (path, options) => {
  calls.push({ path, body: JSON.parse(options.body) });
  return { ok: true, json: async () => path.endsWith('/resume') ? { tables: choices } : { invitations } };
}; }

test('discovery keeps separate tables and both held viewpoints without reading either board', async () => {
  const calls = []; const result = await arenaSeats('', storage, host(calls));
  assert.equal(result.choices.length, 3); assert.deepEqual(calls, [{ path: '/api/lobby/resume', body: { tokens: ['first', 'second'] } }]);
});
test('an explicit table never silently falls back to another saved table', async () => {
  assert.equal((await arenaSeats('?table=missing', storage, host([]))).choices.length, 0);
  assert.deepEqual((await arenaSeats('?table=two', storage, host([]))).choices.map(one => one.token), ['third']);
});
test('a linked invitation is offered for review and is never claimed', async () => {
  const calls = []; const result = await arenaSeats('?player1=invite', null, host(calls, [{ kind: 'invitation', token: 'invite' }]));
  assert.equal(result.choices.length, 0); assert.equal(result.invitations.length, 1);
  assert.deepEqual(calls.map(one => one.path), ['/api/lobby/resume', '/api/lobby/invitations']);
});
test('an account refusal retains its status and never fetches a private board', async () => {
  await assert.rejects(arenaSeats('', storage, async () => ({ ok: false, status: 401 })), error => error.status === 401);
});
test('storage-disabled links keep their exact seat while saved seats need no token in the address', () => {
  const choice = { ...choices[0].seats[0], table: choices[0].table };
  assert.equal(seatPath('/play', choice, storage), '/play?table=one&seat=player1');
  assert.equal(seatPath('/play', choice, null), '/play?table=one&seat=player1&player1=first');
});
test('sign-in returns to the arena but never an arena-like external or unexpected path', () => {
  assert.equal(nextPath(new URL(signInPath('/arena?table=one'), 'https://host').search), '/arena?table=one');
  for (const path of ['/arena-lab', '/arenaevil', '/arena/elsewhere', '//evil/arena', '/\\evil/arena']) assert.equal(nextPath(`?next=${encodeURIComponent(path)}`), null);
});
