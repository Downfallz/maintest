import test from 'node:test';
import assert from 'node:assert/strict';
import { accountRequest, accountState, drawWays, nameProblem, nextPath, noticeText, signInPath, signInWays, signOutBody } from './account.js';

test('a name the host could never take is said at once, without asking it', () => {
  for (const name of ['', 'ab', '7kodiak', '_mark', 'Mark F', 'Médéric', 'a23456789012345678901']) assert.notEqual(nameProblem(name), null, name);
  for (const name of ['MarkF', ' kodiak-7 ', 'Lune_Noire', 'abc']) assert.equal(nameProblem(name), null, name);
});

test('a host that says nothing about accounts has them off', () => {
  assert.deepEqual(accountState({ operator: false }), { mode: 'off', account: null, clientId: null, ways: [] });
  assert.deepEqual(accountState(null), { mode: 'off', account: null, clientId: null, ways: [] });
  const signedIn = accountState({ accounts: 'signin', signIn: [{ provider: 'google', clientId: 'id' }], account: { id: 'acct_x', username: 'MarkF' } });
  assert.equal(signedIn.account.username, 'MarkF');
  assert.equal(signedIn.clientId, 'id');
});

test('the ways in are the providers the host lists, in its order, that this page can draw', () => {
  assert.deepEqual(signInWays({ signIn: [{ provider: 'microsoft', clientId: 'ms' }, { provider: 'discord', clientId: '1311' }, { provider: 'google', clientId: 'id' }] }),
    [{ provider: 'discord', clientId: '1311' }, { provider: 'google', clientId: 'id' }]);
  assert.deepEqual(signInWays({ signIn: [null, { provider: 7 }, 'google', { provider: 'google', clientId: 9 }] }), [{ provider: 'google', clientId: null }]);
  assert.deepEqual(signInWays({ accounts: 'dev' }), []);
  assert.deepEqual(signInWays({ signIn: 'google' }), []);
});

test('a host of the release before, which lists no ways, still draws Google\'s button from its client id', () => {
  const doc = anyPage();

  assert.deepEqual(drawWays(doc, accountState({ accounts: 'google', googleClientId: 'id', account: null })), ['google']);
  assert.equal(doc.getElementById('google-button').hidden, false);
  assert.deepEqual(signInWays({ accounts: 'dev', googleClientId: 'id' }), []);
});

test('the page draws the button of each way /api/me lists and hides the others', () => {
  const doc = anyPage();
  const me = { accounts: 'signin', signIn: [{ provider: 'google', clientId: 'id' }], account: null };

  assert.deepEqual(drawWays(doc, accountState(me)), ['google']);
  assert.equal(doc.getElementById('google-button').hidden, false);
  assert.equal(doc.getElementById('sign-in-ways').hidden, false);

  assert.deepEqual(drawWays(doc, accountState({ ...me, signIn: [] })), []);
  assert.equal(doc.getElementById('google-button').hidden, true);
  assert.equal(doc.getElementById('sign-in-ways').hidden, true);

  assert.deepEqual(drawWays(doc, accountState({ accounts: 'dev', signIn: [{ provider: 'google', clientId: 'id' }] })), [], 'a developer\'s host draws the typed name, not a provider');
  assert.equal(doc.getElementById('google-button').hidden, true);
});

test('Discord\'s button is drawn beside Google\'s, in the order the host lists them', () => {
  const doc = anyPage();
  const appended = [];
  doc.getElementById('sign-in-ways').append = (...buttons) => appended.push(...buttons);

  const shown = drawWays(doc, accountState({ accounts: 'signin', signIn: [{ provider: 'discord', clientId: '1311' }, { provider: 'google', clientId: 'id' }], account: null }));

  assert.deepEqual(shown, ['discord', 'google']);
  assert.equal(doc.getElementById('discord-button').hidden, false);
  assert.deepEqual(appended, [doc.getElementById('discord-button'), doc.getElementById('google-button')]);
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
  assert.equal(nextPath('?next=%2Farena%3Ftable%3Done%23history'), '/battle?table=one#history');
  assert.equal(nextPath('?next=%2Farena-other'), null);
  assert.equal(nextPath('?next=%2Fj%2FK7F2Q9XM'), '/j/K7F2Q9XM');
  for (const away of ['https://evil.example', '//evil.example/play', '/admin', 'javascript:alert(1)', '']) assert.equal(nextPath(`?next=${encodeURIComponent(away)}`), null, away);
  assert.equal(nextPath(''), null);
});

test('deleting an account is a DELETE with the typed name, and its refusal is said', async () => {
  const refused = accountRequest('/api/account', { username: 'Mark' }, async (path, request) => {
    assert.equal(request.method, 'DELETE');
    assert.equal(JSON.parse(request.body).username, 'Mark');
    return { ok: false, status: 400, text: async () => JSON.stringify({ error: 'Account.ConfirmDelete', message: 'Type your player name exactly to delete your account.' }) };
  }, 'DELETE');
  await assert.rejects(refused, error => error.code === 'Account.ConfirmDelete');
});

test('the operator\'s notice shows on every page but the game itself, and an empty one is none', () => {
  assert.equal(noticeText({ notice: '  Maintenance at 22:00 UTC. ' }, 'lobby'), 'Maintenance at 22:00 UTC.');
  assert.equal(noticeText({ notice: 'Maintenance at 22:00 UTC.' }, 'admin'), 'Maintenance at 22:00 UTC.');
  assert.equal(noticeText({ notice: 'Maintenance at 22:00 UTC.' }, 'battle'), null);
  assert.equal(noticeText({ notice: 'Maintenance at 22:00 UTC.' }, 'classic'), null);
  assert.equal(noticeText({ notice: '   ' }, 'lobby'), null);
  assert.equal(noticeText({}, 'lobby'), null, 'a host before ADR 0177 says no notice');
});

test('a sign out is asked with its own words, the action filled and the way back named Stay signed in', async () => {
  const { signOutWords } = await import('./account.js');
  assert.deepEqual(signOutWords(false), { title: 'Sign out on this device?', detail: 'You can sign in again to resume your games.', action: 'Sign out', keep: 'Stay signed in' });
  assert.deepEqual(signOutWords(true), { title: 'Sign out everywhere?', detail: 'You will need to sign in again on every device.', action: 'Sign out everywhere', keep: 'Stay signed in' });
});

test('a sign out\'s confirmation wears the eye still open as its head, on each page that declares it, the eye in view', async () => {
  const { SIGN_OUT_HEAD } = await import('./account.js');
  const { HEADER_SCENE_PAGES } = await import('./navigation.js');
  const { PAGE_ART } = await import('./page-art.js');
  assert.deepEqual({ name: SIGN_OUT_HEAD.name, form: SIGN_OUT_HEAD.form, side: SIGN_OUT_HEAD.side, focus: SIGN_OUT_HEAD.focus },
    { name: 'eye-still-open', form: 'head', side: 'start', focus: '70% 45%' });
  for (const page of Object.values(HEADER_SCENE_PAGES)) assert.equal(PAGE_ART['eye-still-open'].pages[page], 'lazy', page);
});

test('the tab remembers a sign out once: the next read says goodbye, the one after does not', async () => {
  const { markSignedOut, takeSignedOut } = await import('./account.js');
  const store = new Map();
  const storage = { getItem: key => store.get(key) ?? null, setItem: (key, value) => store.set(key, value), removeItem: key => store.delete(key) };
  assert.equal(takeSignedOut(storage), false, 'a tab that signed nobody out');
  markSignedOut(storage);
  assert.equal(takeSignedOut(storage), true);
  assert.equal(takeSignedOut(storage), false, 'a reload shows the plain door');
  const refusing = { getItem() { throw new Error('blocked'); }, setItem() { throw new Error('blocked'); }, removeItem() {} };
  markSignedOut(refusing);
  assert.equal(takeSignedOut(refusing), false, 'a browser without storage says no goodbye');
  assert.equal(takeSignedOut(null), false);
});

// Any element the account asks for, by id: enough to mount it without a browser.
function anyPage() {
  const made = new Map();
  const element = () => ({ hidden: false, textContent: '', value: '', open: false, disabled: false, className: '', listeners: {},
    classList: { toggle() {}, add() {}, remove() {} }, setAttribute() {}, focus() {}, showModal() { this.open = true; }, close() { this.open = false; },
    addEventListener(kind, heard) { this.listeners[kind] = heard; }, querySelector: () => element() });
  return { getElementById: id => made.get(id) ?? made.set(id, element()).get(id) };
}

test('a refresh of the account does not wait for the page to know whether it leaves', async () => {
  let answer;
  const tutorials = new Promise(resolve => { answer = resolve; });
  const { mountAccount } = await import('./account.js');
  const mounted = mountAccount(anyPage(), { readMe: async () => ({ accounts: 'dev', account: { username: 'ada' } }), onChange: () => tutorials, google: async () => ({}), session: null });
  const drawn = await Promise.race([mounted.then(() => 'drawn'), new Promise(resolve => setTimeout(() => resolve('waiting'), 50))]);
  assert.equal(drawn, 'drawn');
  answer(true);
  assert.equal(await (await mounted).leaving, true);
});

test('a page that fails to say whether it leaves is told to whoever waits for it, and the page is still drawn', async () => {
  const { mountAccount } = await import('./account.js');
  const state = await mountAccount(anyPage(), { readMe: async () => ({ accounts: 'dev', account: { username: 'ada' } }), onChange: async () => { throw new Error('lessons unread'); }, google: async () => ({}), session: null });
  assert.equal(state.account.username, 'ada');
  await assert.rejects(state.leaving, /lessons unread/);
});

test('a sign-out on this browser names the push endpoint it holds, and everywhere names none', async () => {
  const nav = endpoint => ({ serviceWorker: { getRegistration: async () => ({ pushManager: { getSubscription: async () => (endpoint ? { endpoint } : null) } }) } });
  assert.deepEqual(await signOutBody(false, nav('https://push.example/laptop')), { everywhere: false, endpoint: 'https://push.example/laptop' });
  assert.deepEqual(await signOutBody(false, nav(null)), { everywhere: false });
  assert.deepEqual(await signOutBody(false, {}), { everywhere: false }, 'a browser without a worker');
  assert.deepEqual(await signOutBody(false, { serviceWorker: { getRegistration: async () => { throw new Error('blocked'); } } }), { everywhere: false });
  assert.deepEqual(await signOutBody(true, nav('https://push.example/laptop')), { everywhere: true });
});

function tabStore(entries = {}) {
  const kept = new Map(Object.entries(entries));
  return { getItem: key => kept.get(key) ?? null, setItem: (key, value) => kept.set(key, String(value)), removeItem: key => kept.delete(key), kept };
}

test('Discord\'s button keeps the tab\'s state and verifier, then leaves for Discord with the next page kept', async () => {
  const { mountAccount } = await import('./account.js');
  const doc = anyPage();
  const session = tabStore();
  const went = [];
  const win = { location: { origin: 'https://play.example', search: '?next=%2Fj%2Fabc', assign: url => went.push(url) } };
  await mountAccount(doc, { readMe: async () => ({ accounts: 'signin', signIn: [{ provider: 'discord', clientId: '1311' }], account: null }), google: async () => ({}), session, win });

  await doc.getElementById('discord-button').listeners.click();

  const url = new URL(went.at(-1));
  const kept = JSON.parse(session.kept.get('downfall.discordSignIn'));
  assert.equal(`${url.origin}${url.pathname}`, 'https://discord.com/oauth2/authorize');
  assert.equal(url.searchParams.get('client_id'), '1311');
  assert.equal(url.searchParams.get('redirect_uri'), 'https://play.example/signin/discord');
  assert.equal(url.searchParams.get('state'), kept.state);
  assert.equal(kept.next, '/j/abc');
  assert.notEqual(url.searchParams.get('code_challenge'), kept.verifier, 'the verifier never leaves the tab');
});

test('a name to choose handed over by Discord\'s return opens the name window once', async () => {
  const { mountAccount } = await import('./account.js');
  const doc = anyPage();
  const session = tabStore({ 'downfall.signInTicket': 'ticket-1' });
  const me = async () => ({ accounts: 'signin', signIn: [{ provider: 'discord', clientId: '1311' }], account: null });

  await mountAccount(doc, { readMe: me, google: async () => ({}), session, win: { location: { origin: 'https://play.example', search: '' } } });

  assert.equal(doc.getElementById('name-dialog').open, true);
  assert.equal(session.kept.has('downfall.signInTicket'), false);
});

test('a page brought back from the cache after leaving for Discord gives its button back', async () => {
  const { mountAccount } = await import('./account.js');
  const doc = anyPage();
  const heard = {};
  const win = { location: { origin: 'https://play.example', search: '', assign() {} }, addEventListener: (kind, listener) => { heard[kind] = listener; } };
  await mountAccount(doc, { readMe: async () => ({ accounts: 'signin', signIn: [{ provider: 'discord', clientId: '1311' }], account: null }), google: async () => ({}), session: tabStore(), win });
  await doc.getElementById('discord-button').listeners.click();
  assert.equal(doc.getElementById('discord-button').disabled, true);

  heard.pageshow({ persisted: true });

  assert.equal(doc.getElementById('discord-button').disabled, false);
});

test('a name to choose is kept while the host cannot be read, for the reload', async () => {
  const { mountAccount } = await import('./account.js');
  const session = tabStore({ 'downfall.signInTicket': 'ticket-1' });

  await mountAccount(anyPage(), { readMe: async () => { throw new Error('down'); }, google: async () => ({}), session, win: { location: { origin: 'https://play.example', search: '' } } });

  assert.equal(session.kept.get('downfall.signInTicket'), 'ticket-1');
});

test('a sign-in\'s answer leaves its proof of a recent sign-in in the tab, for ten minutes', async () => {
  const { keepRecentSignIn, recentSignIn, RECENT_FOR_MS } = await import('./account.js');
  const store = new Map();
  const storage = { getItem: key => store.get(key) ?? null, setItem: (key, value) => store.set(key, value) };

  keepRecentSignIn(storage, 'r1.proof', 1000);

  assert.equal(recentSignIn(storage, 1000 + RECENT_FOR_MS - 1), 'r1.proof');
  assert.equal(recentSignIn(storage, 1000 + RECENT_FOR_MS), null);
});

test('Google is initialized once, and its answer goes to Settings while it waits, the sign-in otherwise', async () => {
  const { answerGoogle, prepareGoogle } = await import('./account.js');
  let initialized = 0, callback = null;
  const id = { initialize(options) { initialized += 1; callback = options.callback; } };
  const got = [];
  answerGoogle('signin', response => got.push(['signin', response]));

  prepareGoogle(id, 'g'); prepareGoogle(id, 'g');
  answerGoogle('link', response => got.push(['link', response]));
  callback('a');
  answerGoogle('link', null);
  callback('b');

  assert.equal(initialized, 1);
  assert.deepEqual(got, [['link', 'a'], ['signin', 'b']]);
  answerGoogle('signin', null);
});

test('a sign out forgets the tab\'s proof of a recent sign-in', async () => {
  const { forgetRecentSignIn, keepRecentSignIn, recentSignIn } = await import('./account.js');
  const store = new Map();
  const storage = { getItem: key => store.get(key) ?? null, setItem: (key, value) => store.set(key, value), removeItem: key => store.delete(key) };
  keepRecentSignIn(storage, 'r1.proof');

  forgetRecentSignIn(storage);

  assert.equal(recentSignIn(storage), null);
});
