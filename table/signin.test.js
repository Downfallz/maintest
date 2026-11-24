import test from 'node:test';
import assert from 'node:assert/strict';
import { finishCodeFlow, lobbyPath, notAdded, returnProblem } from './signin.js';

function page() {
  const made = new Map();
  return { getElementById: id => made.get(id) ?? made.set(id, { textContent: '', href: '', hidden: true }).get(id) };
}

function tab(kept, provider = 'discord') {
  const store = new Map(kept ? [[`downfall.${provider}SignIn`, JSON.stringify(kept)]] : []);
  return { getItem: key => store.get(key) ?? null, setItem: (key, value) => store.set(key, value), removeItem: key => store.delete(key), store };
}

function windowAt(search, pathname = '/signin/discord') {
  const win = { replaced: null, address: null };
  win.location = { search, pathname, origin: 'https://play.example', replace: url => { win.replaced = url; } };
  win.history = { replaceState: (state, title, url) => { win.address = url; } };
  return win;
}

const kept = { state: 'st', verifier: 'v'.repeat(43), next: '/j/abc', at: Date.now() };

test('a good return posts the code with its verifier and goes back to the lobby on the way to next', async () => {
  const win = windowAt('?code=abc&state=st');
  const posted = [];

  const outcome = await finishCodeFlow({ win, doc: page(), storage: tab(kept), request: async (path, body) => { posted.push([path, body]); return { needsUsername: false }; } });

  assert.equal(outcome, 'signed-in');
  assert.deepEqual(posted, [['/api/account/discord', { code: 'abc', verifier: kept.verifier, redirect: 'https://play.example/signin/discord' }]]);
  assert.equal(win.replaced, '/lobby?next=%2Fj%2Fabc');
  assert.equal(win.address, '/signin/discord', 'the code is taken off the address');
});

test('a new player\'s ticket is handed to the lobby\'s name window', async () => {
  const storage = tab(kept);

  await finishCodeFlow({ win: windowAt('?code=abc&state=st'), doc: page(), storage, request: async () => ({ needsUsername: true, ticket: 'T' }) });

  assert.equal(storage.store.get('downfall.signInTicket'), 'T');
});

test('a return with another state is never posted and says so', async () => {
  const doc = page();
  let asked = false;

  const outcome = await finishCodeFlow({ win: windowAt('?code=abc&state=forged'), doc, storage: tab(kept), request: async () => { asked = true; return {}; } });

  assert.equal(outcome, 'mismatch');
  assert.equal(asked, false);
  assert.equal(doc.getElementById('signin-state').textContent, returnProblem('mismatch'));
  assert.equal(doc.getElementById('signin-back').hidden, false);
  assert.equal(doc.getElementById('signin-back').href, '/lobby');
});

test('a return with no sign-in started in this tab is never posted', async () => {
  let asked = false;

  const outcome = await finishCodeFlow({ win: windowAt('?code=abc&state=st'), doc: page(), storage: tab(null), request: async () => { asked = true; return {}; } });

  assert.equal(outcome, 'lapsed');
  assert.equal(asked, false);
});

test('a refusal by the host is said on the page with the way back', async () => {
  const doc = page();

  const outcome = await finishCodeFlow({ win: windowAt('?code=abc&state=st'), doc, storage: tab(kept), request: async () => { throw new Error('Your sign-in could not be checked. Sign in again.'); } });

  assert.equal(outcome, 'refused');
  assert.match(doc.getElementById('signin-state').textContent, /could not be checked/);
});

test('a player who said no at Discord goes straight back', async () => {
  const win = windowAt('?error=access_denied&state=st');

  assert.equal(await finishCodeFlow({ win, doc: page(), storage: tab(kept), request: async () => ({}) }), 'cancelled');
  assert.equal(win.replaced, '/lobby?next=%2Fj%2Fabc');
  assert.equal(lobbyPath(null), '/lobby');
});

test('an error Discord returns with other than the player saying no is said, with the way back', async () => {
  const doc = page();
  const win = windowAt('?error=invalid_scope&state=st');

  assert.equal(await finishCodeFlow({ win, doc, storage: tab(kept), request: async () => ({}) }), 'failed');
  assert.equal(win.replaced, null);
  assert.equal(doc.getElementById('signin-state').textContent, returnProblem('failed'));
});

test('a return started from Settings adds Discord to the account and hands Settings its line', async () => {
  const win = windowAt('?code=abc&state=st');
  const storage = tab({ ...kept, next: null, purpose: 'link' });
  storage.setItem('downfall.recentSignIn', JSON.stringify({ value: 'R', at: Date.now() }));
  const posted = [];

  const outcome = await finishCodeFlow({ win, doc: page(), storage, request: async (path, body) => { posted.push([path, body]); return { identities: [{ provider: 'google' }, { provider: 'discord' }] }; } });

  assert.equal(outcome, 'linked');
  assert.deepEqual(posted, [['/api/account/identities/discord', { code: 'abc', verifier: kept.verifier, redirect: 'https://play.example/signin/discord', recent: 'R' }]]);
  assert.equal(win.replaced, '/lobby');
  assert.equal(storage.store.get('downfall.waysNote'), 'Discord added. You can sign in with it now.');
});

test('a Discord another account uses is said on the return page, with the way back', async () => {
  const doc = page();
  const refused = Object.assign(new Error('Another account already uses this way to sign in.'), { code: 'Account.IdentityTaken' });

  const outcome = await finishCodeFlow({ win: windowAt('?code=abc&state=st'), doc, storage: tab({ ...kept, purpose: 'link' }), request: async () => { throw refused; } });

  assert.equal(outcome, 'refused');
  assert.equal(doc.getElementById('signin-state').textContent, refused.message);
  assert.equal(doc.getElementById('signin-back').hidden, false);
});

test('a link whose state does not match says Discord was not added, not to sign in again', async () => {
  const doc = page();
  let asked = false;

  const outcome = await finishCodeFlow({ win: windowAt('?code=abc&state=forged'), doc, storage: tab({ ...kept, purpose: 'link' }), request: async () => { asked = true; return {}; } });

  assert.equal(outcome, 'mismatch');
  assert.equal(asked, false);
  assert.equal(doc.getElementById('signin-heading').textContent, 'Adding Discord');
  assert.equal(doc.getElementById('signin-state').textContent, notAdded('discord'));
});

test('a player who says no at Discord while adding it goes back with Settings told', async () => {
  const win = windowAt('?error=access_denied&state=st');
  const storage = tab({ ...kept, next: null, purpose: 'link' });

  const outcome = await finishCodeFlow({ win, doc: page(), storage, request: async () => ({}) });

  assert.equal(outcome, 'cancelled');
  assert.equal(win.replaced, '/lobby');
  assert.equal(storage.store.get('downfall.waysNote'), 'Discord was not added.');
});

test('Microsoft\'s return posts its code to Microsoft\'s route with Microsoft\'s redirect', async () => {
  const win = windowAt('?code=M.C5_BAY.2.U.x&state=st', '/signin/microsoft');
  const doc = page();
  const posted = [];

  const outcome = await finishCodeFlow({ win, doc, storage: tab(kept, 'microsoft'), request: async (path, body) => { posted.push([path, body]); return { needsUsername: false }; } });

  assert.equal(outcome, 'signed-in');
  assert.deepEqual(posted, [['/api/account/microsoft', { code: 'M.C5_BAY.2.U.x', verifier: kept.verifier, redirect: 'https://play.example/signin/microsoft' }]]);
  assert.equal(win.address, '/signin/microsoft', 'the code is taken off the address');
  assert.equal(doc.getElementById('signin-heading').textContent, 'Signing in with Microsoft');
});

test('a return to Microsoft\'s page never uses what the tab kept for Discord', async () => {
  let asked = false;

  const outcome = await finishCodeFlow({ win: windowAt('?code=abc&state=st', '/signin/microsoft'), doc: page(), storage: tab(kept, 'discord'), request: async () => { asked = true; return {}; } });

  assert.equal(outcome, 'lapsed');
  assert.equal(asked, false);
});

test('Microsoft added from Settings goes to its identities route and says Microsoft', async () => {
  const storage = tab({ ...kept, next: null, purpose: 'link' }, 'microsoft');
  const posted = [];

  const outcome = await finishCodeFlow({ win: windowAt('?code=abc&state=st', '/signin/microsoft'), doc: page(), storage, request: async (path, body) => { posted.push([path, body]); return {}; } });

  assert.equal(outcome, 'linked');
  assert.equal(posted[0][0], '/api/account/identities/microsoft');
  assert.equal(storage.store.get('downfall.waysNote'), 'Microsoft added. You can sign in with it now.');
  assert.equal(notAdded('microsoft'), 'Microsoft was not added. Open Settings, Account, and try again.');
  assert.equal(returnProblem('failed', 'microsoft'), 'Microsoft could not sign you in. Try again from the lobby.');
});

const busy = (status, retryAfter) => Object.assign(new Error('Too many sign-ins from your network just now. Try again in a minute.'), { status, retryAfter });

test('a short 429 is waited out once, saying the sign-in is being checked, and the same code then signs in', async () => {
  const doc = page();
  const win = windowAt('?code=abc&state=st');
  const posted = [], waited = [];
  const answers = [() => { throw busy(429, 1); }, () => ({ needsUsername: false })];

  const outcome = await finishCodeFlow({ win, doc, storage: tab(kept), request: async (path, body) => { posted.push(body); return answers.shift()(); },
    pause: async ms => { waited.push([ms, doc.getElementById('signin-state').textContent]); } });

  assert.equal(outcome, 'signed-in');
  assert.deepEqual(waited, [[1000, 'Checking your sign-in…']]);
  assert.equal(posted.length, 2);
  assert.deepEqual(posted[1], posted[0], 'the code and verifier kept in memory are posted again');
  assert.equal(win.replaced, '/lobby?next=%2Fj%2Fabc');
});

test('a short 503 is waited out once too, also when adding the provider from Settings', async () => {
  const storage = tab({ ...kept, next: null, purpose: 'link' });
  const answers = [() => { throw busy(503, 2); }, () => ({})];
  const waited = [];

  const outcome = await finishCodeFlow({ win: windowAt('?code=abc&state=st'), doc: page(), storage, request: async () => answers.shift()(), pause: async ms => { waited.push(ms); } });

  assert.equal(outcome, 'linked');
  assert.deepEqual(waited, [2000]);
});

test('a second short 429 says the host\'s message, with the way back, and is not tried a third time', async () => {
  const doc = page();
  let asked = 0;

  const outcome = await finishCodeFlow({ win: windowAt('?code=abc&state=st'), doc, storage: tab(kept), request: async () => { asked += 1; throw busy(429, 1); }, pause: async () => {} });

  assert.equal(outcome, 'refused');
  assert.equal(asked, 2);
  assert.match(doc.getElementById('signin-state').textContent, /Too many sign-ins/);
  assert.equal(doc.getElementById('signin-back').hidden, false);
});

test('a wait longer than five seconds, none said, or another refusal is not tried again', async () => {
  for (const refusal of [busy(429, 6), busy(503, null), busy(401, 1), new Error('The host answered 500. Try again.')]) {
    let asked = 0, waited = false;

    const outcome = await finishCodeFlow({ win: windowAt('?code=abc&state=st'), doc: page(), storage: tab(kept), request: async () => { asked += 1; throw refusal; }, pause: async () => { waited = true; } });

    assert.equal(outcome, 'refused');
    assert.equal(asked, 1, refusal.message);
    assert.equal(waited, false);
  }
});
