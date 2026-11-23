import test from 'node:test';
import assert from 'node:assert/strict';
import { finishDiscord, lobbyPath, returnProblem } from './signin.js';

function page() {
  const made = new Map();
  return { getElementById: id => made.get(id) ?? made.set(id, { textContent: '', href: '', hidden: true }).get(id) };
}

function tab(kept) {
  const store = new Map(kept ? [['downfall.discordSignIn', JSON.stringify(kept)]] : []);
  return { getItem: key => store.get(key) ?? null, setItem: (key, value) => store.set(key, value), removeItem: key => store.delete(key), store };
}

function windowAt(search) {
  const win = { replaced: null, address: null };
  win.location = { search, origin: 'https://play.example', replace: url => { win.replaced = url; } };
  win.history = { replaceState: (state, title, url) => { win.address = url; } };
  return win;
}

const kept = { state: 'st', verifier: 'v'.repeat(43), next: '/j/abc', at: Date.now() };

test('a good return posts the code with its verifier and goes back to the lobby on the way to next', async () => {
  const win = windowAt('?code=abc&state=st');
  const posted = [];

  const outcome = await finishDiscord({ win, doc: page(), storage: tab(kept), request: async (path, body) => { posted.push([path, body]); return { needsUsername: false }; } });

  assert.equal(outcome, 'signed-in');
  assert.deepEqual(posted, [['/api/account/discord', { code: 'abc', verifier: kept.verifier, redirect: 'https://play.example/signin/discord' }]]);
  assert.equal(win.replaced, '/lobby?next=%2Fj%2Fabc');
  assert.equal(win.address, '/signin/discord', 'the code is taken off the address');
});

test('a new player\'s ticket is handed to the lobby\'s name window', async () => {
  const storage = tab(kept);

  await finishDiscord({ win: windowAt('?code=abc&state=st'), doc: page(), storage, request: async () => ({ needsUsername: true, ticket: 'T' }) });

  assert.equal(storage.store.get('downfall.signInTicket'), 'T');
});

test('a return with another state is never posted and says so', async () => {
  const doc = page();
  let asked = false;

  const outcome = await finishDiscord({ win: windowAt('?code=abc&state=forged'), doc, storage: tab(kept), request: async () => { asked = true; return {}; } });

  assert.equal(outcome, 'mismatch');
  assert.equal(asked, false);
  assert.equal(doc.getElementById('signin-state').textContent, returnProblem('mismatch'));
  assert.equal(doc.getElementById('signin-back').hidden, false);
  assert.equal(doc.getElementById('signin-back').href, '/lobby');
});

test('a return with no sign-in started in this tab is never posted', async () => {
  let asked = false;

  const outcome = await finishDiscord({ win: windowAt('?code=abc&state=st'), doc: page(), storage: tab(null), request: async () => { asked = true; return {}; } });

  assert.equal(outcome, 'lapsed');
  assert.equal(asked, false);
});

test('a refusal by the host is said on the page with the way back', async () => {
  const doc = page();

  const outcome = await finishDiscord({ win: windowAt('?code=abc&state=st'), doc, storage: tab(kept), request: async () => { throw new Error('Your sign-in could not be checked. Sign in again.'); } });

  assert.equal(outcome, 'refused');
  assert.match(doc.getElementById('signin-state').textContent, /could not be checked/);
});

test('a player who said no at Discord goes straight back', async () => {
  const win = windowAt('?error=access_denied&state=st');

  assert.equal(await finishDiscord({ win, doc: page(), storage: tab(kept), request: async () => ({}) }), 'cancelled');
  assert.equal(win.replaced, '/lobby?next=%2Fj%2Fabc');
  assert.equal(lobbyPath(null), '/lobby');
});

test('an error Discord returns with other than the player saying no is said, with the way back', async () => {
  const doc = page();
  const win = windowAt('?error=invalid_scope&state=st');

  assert.equal(await finishDiscord({ win, doc, storage: tab(kept), request: async () => ({}) }), 'failed');
  assert.equal(win.replaced, null);
  assert.equal(doc.getElementById('signin-state').textContent, returnProblem('failed'));
});
