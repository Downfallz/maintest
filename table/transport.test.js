import { test } from 'node:test';
import assert from 'node:assert/strict';
import { httpTransport } from './transport.js';

// The stub is the shape studio/backend.test.js uses: a recorded call and a canned answer, no network.
function stub(answer = { status: 200, body: '{"seat":"player1"}' }) {
  const calls = [];
  const fetchImpl = async (path, options) => {
    calls.push({ path, options });
    return {
      status: answer.status,
      ok: answer.status >= 200 && answer.status < 300,
      text: async () => answer.body,
    };
  };
  return { calls, fetchImpl };
}

test('a transport speaks for one seat and carries its token on every call', async () => {
  const { calls, fetchImpl } = stub();
  const transport = httpTransport('player2', 'abc', fetchImpl);

  await transport.seat();
  await transport.session();

  assert.deepEqual(calls.map(call => call.path), ['/api/seat/player2', '/api/session']);
  assert.ok(calls.every(call => call.options.headers['X-Seat-Token'] === 'abc'));
});

test('a decision is posted as json, which is also what the host requires of a write', async () => {
  const { calls, fetchImpl } = stub({ status: 204, body: '' });
  const transport = httpTransport('player1', 'abc', fetchImpl);

  const answer = await transport.decide({ kind: 'Speed', creature: 1, speed: 'Quick' });

  assert.equal(calls[0].path, '/api/seat/player1/decision');
  assert.equal(calls[0].options.method, 'POST');
  assert.equal(calls[0].options.headers['Content-Type'], 'application/json');
  assert.deepEqual(JSON.parse(calls[0].options.body), { kind: 'Speed', creature: 1, speed: 'Quick' });
  assert.equal(answer.status, 204);
  assert.equal(answer.body, null);
});

test('a refusal keeps the reason the host gave, whether it is json or a line of text', async () => {
  const late = httpTransport('player1', 'abc', stub({ status: 409, body: '{"error":"Seat.NotWaiting"}' }).fetchImpl);
  const plain = httpTransport('player1', 'abc', stub({ status: 403, body: 'This token is player2\'s.' }).fetchImpl);

  assert.equal((await late.decide({})).body.error, 'Seat.NotWaiting');
  assert.equal((await plain.seat()).body.message, "This token is player2's.");
});

test('a transport without a seat or a token refuses to exist', () => {
  assert.throws(() => httpTransport('player3', 'abc'), /neither player1 nor player2/);
  assert.throws(() => httpTransport('player1', ''), /token/);
});
