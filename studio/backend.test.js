// The studio's backends, run in Node rather than a browser (ADR 0024). What is worth testing here is the
// shape of what a backend does with a transport it is given: which requests, in which order, with what in
// them. The local host's own behaviour is tested from C# against the real server; these tests are about the
// module.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { backendForThisPage, hostedBackend, localBackend } from './backend.js';

/** A stand-in for `fetch` that records what it was asked and answers from a script. */
function transport(answers) {
  const calls = [];
  return {
    calls,
    fetch: async (url, options = {}) => {
      calls.push({ url, method: options.method ?? 'GET', body: options.body ? JSON.parse(options.body) : undefined });
      const answer = answers.shift() ?? { ok: true, result: {} };
      return {
        ok: answer.status === undefined || answer.status < 400,
        status: answer.status ?? 200,
        statusText: answer.statusText ?? 'OK',
        json: async () => answer.payload ?? answer,
      };
    },
  };
}

test('a change is one request per document, then the removals, then the aliases', async () => {
  const { calls, fetch } = transport([]);

  await localBackend(fetch).change({
    kind: 'spells',
    write: [{ path: 'a.json', document: { id: 'spell:a:v1' }, create: true }],
    remove: ['b.json'],
    aliases: { a: 'spell:a:v1' },
  });

  // The order is the point: an alias must never name a document that is not written, or one just removed.
  assert.deepEqual(calls.map(call => call.url), ['/api/documents', '/api/documents/delete', '/api/aliases']);
  assert.equal(calls[0].body.create, true);
  assert.equal(calls[0].body.kind, 'spells');
  assert.deepEqual(calls[2].body.aliases, { a: 'spell:a:v1' });
});

test('a refusal from the host becomes an error carrying the problems to show', async () => {
  const { fetch } = transport([{ payload: { ok: false, message: 'The content does not build.', problems: ['spell:a:v1 has no effects'] } }]);

  await assert.rejects(
    () => localBackend(fetch).build(),
    error => {
      assert.equal(error.message, 'The content does not build.');
      assert.deepEqual(error.problems, ['spell:a:v1 has no effects']);
      return true;
    });
});

test('a host that answers with something other than JSON still refuses readably', async () => {
  const fetch = async () => ({ ok: false, status: 502, statusText: 'Bad Gateway', json: async () => { throw new Error('not json'); } });

  await assert.rejects(() => localBackend(fetch).read(), /502 Bad Gateway/);
});

test('the published page reads the files the site publishes beside it', async () => {
  const { calls, fetch } = transport([{ payload: { spells: [] } }]);

  await hostedBackend(fetch).read();

  // Relative, so the page does not care what path the site is served under.
  assert.deepEqual(calls.map(call => call.url), ['data/catalogue.json']);
});

test('a published file that is not there says why, not just that it failed', async () => {
  const { fetch } = transport([{ status: 404, statusText: 'Not Found' }]);

  await assert.rejects(() => hostedBackend(fetch).audit(), /data\/audit\.json \(404\)/);
});

test('the published page has played no runs, which is an empty list and not a refusal', async () => {
  assert.deepEqual(await hostedBackend().runs(), []);
});

test('everything the published page cannot do yet refuses where the user tried it', async () => {
  const page = hostedBackend();

  await assert.rejects(() => page.change({ kind: 'spells' }), /Saving needs the engine/);
  await assert.rejects(() => page.build(), /Building needs the engine/);
  await assert.rejects(() => page.play({}), /Playing a match needs the engine/);
});

test('the loopback address is what picks the local host, and nothing else is it', async () => {
  // Where a backend reads the catalogue from is the shortest thing that differs between the two, so it is what
  // makes the choice observable. Asking whether the object looks local would pass on either.
  const readsFrom = async hostname => {
    const previous = globalThis.location;
    globalThis.location = { hostname };
    const { calls, fetch } = transport([{ payload: { ok: true, result: {} } }]);
    try {
      await backendForThisPage(fetch).read();
      return calls[0].url;
    } finally {
      globalThis.location = previous;
    }
  };

  // The local host binds the loopback address and nothing else (ADR 0015), so "not loopback" is "not local".
  assert.equal(await readsFrom('127.0.0.1'), '/api/catalogue');
  assert.equal(localBackend().kind, 'local');
  assert.equal(hostedBackend().kind, 'hosted');
  assert.equal(await readsFrom('localhost'), '/api/catalogue');
  assert.equal(await readsFrom('[::1]'), '/api/catalogue');
  assert.equal(await readsFrom('downfallz.github.io'), 'data/catalogue.json');

  // A hostname that merely starts with the loopback address is not the loopback address.
  assert.equal(await readsFrom('127.0.0.1.evil.example'), 'data/catalogue.json');
});
