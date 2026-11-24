import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash, webcrypto } from 'node:crypto';
import { KEPT_FOR_MS, authorizeUrl, base64Url, codeReturn, keepTicket, pkcePair, providerOfReturn, startCodeFlow, takeKept, takeTicket } from './codeflow.js';

function tabStore() {
  const kept = new Map();
  return { getItem: key => kept.get(key) ?? null, setItem: (key, value) => kept.set(key, String(value)), removeItem: key => kept.delete(key) };
}

test('a verifier is 43 unreserved characters and its challenge is its SHA-256 in base64url', async () => {
  const { verifier, challenge } = await pkcePair(webcrypto);

  assert.match(verifier, /^[A-Za-z0-9_-]{43}$/);
  assert.equal(challenge, createHash('sha256').update(verifier).digest('base64url'));
  assert.equal(base64Url(new Uint8Array([251, 255, 191])), '-_-_');
});

test('Discord is asked for the code flow, the identify scope alone and an S256 challenge', () => {
  const url = new URL(authorizeUrl('discord', { clientId: '1311', redirect: 'https://play.example/signin/discord', state: 's', challenge: 'c' }));

  assert.deepEqual(Object.fromEntries(url.searchParams), {
    response_type: 'code', client_id: '1311', redirect_uri: 'https://play.example/signin/discord',
    state: 's', code_challenge: 'c', code_challenge_method: 'S256', scope: 'identify', prompt: 'none',
  });
});

test('the return gives the code and the kept verifier only for the state this tab sent', async () => {
  const storage = tabStore();
  let url;
  await startCodeFlow({ provider: 'discord', clientId: '1311', origin: 'https://play.example', next: '/j/abc', storage, cryptoImpl: webcrypto, now: 1000, go: went => { url = new URL(went); } });
  const state = url.searchParams.get('state');
  const kept = takeKept(storage, 'discord');

  assert.deepEqual(codeReturn(`?code=abc&state=${state}`, kept, 2000), { code: 'abc', verifier: kept.verifier, next: '/j/abc', link: false });
  assert.deepEqual(codeReturn('?code=abc&state=another', kept, 2000), { problem: 'mismatch', next: null, link: false });
  assert.deepEqual(codeReturn(`?state=${state}`, kept, 2000), { problem: 'mismatch', next: null, link: false });
  assert.equal(takeKept(storage, 'discord'), null, 'what the tab kept is read once');
});

test('a return this tab did not start, or started too long ago, is never posted', () => {
  const kept = { state: 's', verifier: 'v', next: null, at: 0 };

  assert.equal(codeReturn('?code=abc&state=s', null).problem, 'lapsed');
  assert.equal(codeReturn('?code=abc&state=s', kept, KEPT_FOR_MS).problem, 'lapsed');
  assert.equal(codeReturn('?code=abc&state=s', { ...kept, verifier: 7 }, 1).problem, 'lapsed');
  assert.equal(codeReturn('?code=abc&state=s', { ...kept, at: 'yesterday' }, 1).problem, 'lapsed');
  assert.equal(codeReturn('?code=abc&state=s', kept, KEPT_FOR_MS - 1).code, 'abc');
});

test('a player who says no at Discord is cancelled, and goes back where they were going', () => {
  assert.deepEqual(codeReturn('?error=access_denied&state=s', { state: 's', verifier: 'v', next: '/play?x=1', at: 0 }, 1), { problem: 'cancelled', next: '/play?x=1', link: false });
});

test('any other error Discord returns with is a failure the page says, not a cancel', () => {
  for (const error of ['invalid_scope', 'unauthorized_client', 'invalid_request', 'server_error']) {
    assert.equal(codeReturn(`?error=${error}&state=s`, { state: 's', verifier: 'v', next: null, at: 0 }, 1).problem, 'failed', error);
  }
});

test('a tab that keeps nothing cannot leave for Discord and says why', async () => {
  const storage = { setItem() { throw new Error('denied'); } };
  let went = false;

  await assert.rejects(startCodeFlow({ provider: 'discord', clientId: '1311', origin: 'https://play.example', storage, cryptoImpl: webcrypto, go: () => { went = true; } }), /keeps nothing/);
  assert.equal(went, false);
});

test('a name to choose is handed over once', () => {
  const storage = tabStore();
  keepTicket(storage, 't1');

  assert.equal(takeTicket(storage), 't1');
  assert.equal(takeTicket(storage), null);
  assert.equal(takeTicket(null), null);
});

test('a sign-in started from Settings to add Discord comes back as a link', async () => {
  const storage = tabStore();
  let url;
  await startCodeFlow({ provider: 'discord', clientId: '1311', origin: 'https://play.example', purpose: 'link', storage, cryptoImpl: webcrypto, now: 1000, go: went => { url = new URL(went); } });
  const kept = takeKept(storage, 'discord');

  assert.equal(codeReturn(`?code=abc&state=${url.searchParams.get('state')}`, kept, 2000).link, true);
});

test('Microsoft is asked for personal accounts, the openid scope alone, an S256 challenge and the code in the query', () => {
  const url = new URL(authorizeUrl('microsoft', { clientId: 'ms', redirect: 'https://play.example/signin/microsoft', state: 's', challenge: 'c' }));

  assert.equal(`${url.origin}${url.pathname}`, 'https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize');
  assert.deepEqual(Object.fromEntries(url.searchParams), {
    response_type: 'code', client_id: 'ms', redirect_uri: 'https://play.example/signin/microsoft',
    state: 's', code_challenge: 'c', code_challenge_method: 'S256', scope: 'openid', response_mode: 'query',
  });
});

test('each provider keeps its own sign-in in the tab, Discord under the name it always had', async () => {
  const storage = tabStore();
  await startCodeFlow({ provider: 'microsoft', clientId: 'ms', origin: 'https://play.example', storage, cryptoImpl: webcrypto, go: () => {} });

  assert.equal(takeKept(storage, 'discord'), null);
  assert.equal(typeof takeKept(storage, 'microsoft').verifier, 'string');
  await startCodeFlow({ provider: 'discord', clientId: '1311', origin: 'https://play.example', storage, cryptoImpl: webcrypto, go: () => {} });
  assert.equal(typeof JSON.parse(storage.getItem('downfall.discordSignIn')).state, 'string');
});

test('a tab that keeps nothing names the provider it cannot come back from', async () => {
  const storage = { setItem() { throw new Error('denied'); } };

  await assert.rejects(startCodeFlow({ provider: 'microsoft', clientId: 'ms', origin: 'https://play.example', storage, cryptoImpl: webcrypto, go: () => {} }), /so Microsoft cannot bring you back/);
});

test('a return page names its provider by its path, and nothing else is one', () => {
  assert.equal(providerOfReturn('/signin/discord'), 'discord');
  assert.equal(providerOfReturn('/signin/microsoft'), 'microsoft');
  for (const path of ['/signin/google', '/signin/constructor', '/signin/__proto__', '/signin/microsoft/', '/lobby', '', undefined]) {
    assert.equal(providerOfReturn(path), null, String(path));
  }
});
