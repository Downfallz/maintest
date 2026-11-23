import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash, webcrypto } from 'node:crypto';
import { KEPT_FOR_MS, base64Url, discordAuthorizeUrl, discordReturn, keepTicket, pkcePair, startDiscord, takeKept, takeTicket } from './discord.js';

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
  const url = new URL(discordAuthorizeUrl({ clientId: '1311', redirect: 'https://play.example/signin/discord', state: 's', challenge: 'c' }));

  assert.deepEqual(Object.fromEntries(url.searchParams), {
    response_type: 'code', client_id: '1311', scope: 'identify', redirect_uri: 'https://play.example/signin/discord',
    state: 's', code_challenge: 'c', code_challenge_method: 'S256', prompt: 'none',
  });
});

test('the return gives the code and the kept verifier only for the state this tab sent', async () => {
  const storage = tabStore();
  let url;
  await startDiscord({ clientId: '1311', origin: 'https://play.example', next: '/j/abc', storage, cryptoImpl: webcrypto, now: 1000, go: went => { url = new URL(went); } });
  const state = url.searchParams.get('state');
  const kept = takeKept(storage);

  assert.deepEqual(discordReturn(`?code=abc&state=${state}`, kept, 2000), { code: 'abc', verifier: kept.verifier, next: '/j/abc', link: false });
  assert.deepEqual(discordReturn('?code=abc&state=another', kept, 2000), { problem: 'mismatch', next: null });
  assert.deepEqual(discordReturn(`?state=${state}`, kept, 2000), { problem: 'mismatch', next: null });
  assert.equal(takeKept(storage), null, 'what the tab kept is read once');
});

test('a return this tab did not start, or started too long ago, is never posted', () => {
  const kept = { state: 's', verifier: 'v', next: null, at: 0 };

  assert.equal(discordReturn('?code=abc&state=s', null).problem, 'lapsed');
  assert.equal(discordReturn('?code=abc&state=s', kept, KEPT_FOR_MS).problem, 'lapsed');
  assert.equal(discordReturn('?code=abc&state=s', { ...kept, verifier: 7 }, 1).problem, 'lapsed');
  assert.equal(discordReturn('?code=abc&state=s', { ...kept, at: 'yesterday' }, 1).problem, 'lapsed');
  assert.equal(discordReturn('?code=abc&state=s', kept, KEPT_FOR_MS - 1).code, 'abc');
});

test('a player who says no at Discord is cancelled, and goes back where they were going', () => {
  assert.deepEqual(discordReturn('?error=access_denied&state=s', { state: 's', verifier: 'v', next: '/play?x=1', at: 0 }, 1), { problem: 'cancelled', next: '/play?x=1' });
});

test('any other error Discord returns with is a failure the page says, not a cancel', () => {
  for (const error of ['invalid_scope', 'unauthorized_client', 'invalid_request', 'server_error']) {
    assert.equal(discordReturn(`?error=${error}&state=s`, { state: 's', verifier: 'v', next: null, at: 0 }, 1).problem, 'failed', error);
  }
});

test('a tab that keeps nothing cannot leave for Discord and says why', async () => {
  const storage = { setItem() { throw new Error('denied'); } };
  let went = false;

  await assert.rejects(startDiscord({ clientId: '1311', origin: 'https://play.example', storage, cryptoImpl: webcrypto, go: () => { went = true; } }), /keeps nothing/);
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
  await startDiscord({ clientId: '1311', origin: 'https://play.example', purpose: 'link', storage, cryptoImpl: webcrypto, now: 1000, go: went => { url = new URL(went); } });
  const kept = takeKept(storage);

  assert.equal(discordReturn(`?code=abc&state=${url.searchParams.get('state')}`, kept, 2000).link, true);
});
