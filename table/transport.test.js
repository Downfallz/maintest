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

// One route for both seats, because the seat a note belongs to is the token's and not the path's.
test('a note is posted to one route, carrying the seat token that wrote it', async () => {
  const { calls, fetchImpl } = stub({ status: 204, body: '' });
  const transport = httpTransport('player2', 'abc', fetchImpl);

  const answer = await transport.note({ kind: 'Lookup', text: '' });

  assert.equal(calls[0].path, '/api/notes');
  assert.equal(calls[0].options.method, 'POST');
  assert.equal(calls[0].options.headers['X-Seat-Token'], 'abc');
  assert.deepEqual(JSON.parse(calls[0].options.body), { kind: 'Lookup', text: '' });
  assert.equal(answer.status, 204);
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

// The seat route's one query parameter, and the only thing it trims: the board and the options come whole on
// every poll because they are a snapshot, and the feed is a log the page already holds part of.
test('a poll asks only for the feed entries it has not seen', async () => {
  const { calls, fetchImpl } = stub();
  const transport = httpTransport('player1', 'abc', fetchImpl);

  await transport.seat(0);
  await transport.seat(42);

  assert.deepEqual(calls.map(call => call.path), ['/api/seat/player1', '/api/seat/player1?since=42']);
});

test('a poll with nothing held asks for the feed from the start', async () => {
  const { calls, fetchImpl } = stub();
  const transport = httpTransport('player1', 'abc', fetchImpl);

  await transport.seat();

  assert.deepEqual(calls.map(call => call.path), ['/api/seat/player1']);
});
