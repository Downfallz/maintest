import test from 'node:test';
import assert from 'node:assert/strict';
import { accountRequest, accountState, nameProblem, nextPath, signInPath } from './account.js';

test('a name the host could never take is said at once, without asking it', () => {
  for (const name of ['', 'ab', '7kodiak', '_mark', 'Mark F', 'Médéric', 'a23456789012345678901']) assert.notEqual(nameProblem(name), null, name);
  for (const name of ['MarkF', ' kodiak-7 ', 'Lune_Noire', 'abc']) assert.equal(nameProblem(name), null, name);
});

test('a host that says nothing about accounts has them off', () => {
  assert.deepEqual(accountState({ operator: false }), { mode: 'off', account: null, clientId: null });
  assert.deepEqual(accountState(null), { mode: 'off', account: null, clientId: null });
  const signedIn = accountState({ accounts: 'google', googleClientId: 'id', account: { id: 'acct_x', username: 'MarkF' } });
  assert.equal(signedIn.account.username, 'MarkF');
  assert.equal(signedIn.clientId, 'id');
});

test('a refusal keeps the hosts code so the page can tell a lapsed sign-in from a taken name', async () => {
  const refused = accountRequest('/api/account', { ticket: 't', username: 'MarkF' }, async (path, request) => {
    assert.equal(request.method, 'POST');
    assert.equal(request.headers['Content-Type'], 'application/json');
    assert.equal(request.credentials, 'same-origin');
    return { ok: false, status: 409, text: async () => JSON.stringify({ error: 'Account.UsernameTaken', message: 'Another player already has that name.' }) };
  });
  await assert.rejects(refused, error => error.code === 'Account.UsernameTaken' && /already has/.test(error.message));
});

test('after signing in a player goes back only to a seat or an invitation of this host', () => {
  assert.equal(signInPath('/play?invite=abc'), '/lobby?next=%2Fplay%3Finvite%3Dabc');
  assert.equal(nextPath('?next=%2Fplay%3Finvite%3Dabc'), '/play?invite=abc');
  assert.equal(nextPath('?next=%2Fj%2FK7F2Q9XM'), '/j/K7F2Q9XM');
  for (const away of ['https://evil.example', '//evil.example/play', '/admin', 'javascript:alert(1)', '']) assert.equal(nextPath(`?next=${encodeURIComponent(away)}`), null, away);
  assert.equal(nextPath(''), null);
});
