import test from 'node:test';
import assert from 'node:assert/strict';
import { finishWords, keepWaysNote, mountWays, removeWay, removeWords, takeWaysNote, wayHint, wayName, wayRows } from './ways.js';

// Shaped as the host answers: GET /api/account/identities and /api/me (AccountIdentitiesApiTests, AccountApiTests).
const me = { accounts: 'signin', signIn: [{ provider: 'google', clientId: 'g-client' }, { provider: 'discord', clientId: '1311' }] };

function tab() {
  const store = new Map();
  return { getItem: key => store.get(key) ?? null, setItem: (key, value) => store.set(key, value), removeItem: key => store.delete(key), store };
}

test('the account\'s ways come first, then the ways the host offers that it does not use', () => {
  const rows = wayRows([{ provider: 'google', removable: false }], me);

  assert.deepEqual(rows.map(row => [row.provider, row.held]), [['google', true], ['discord', false]]);
  assert.equal(rows[0].removable, false, 'the host said the only way in cannot be removed');
  assert.equal(rows[1].clientId, '1311');
});

test('a way is removable where the host says so, and only there', () => {
  const rows = wayRows([{ provider: 'google', removable: true }, { provider: 'discord', removable: true }], me);

  assert.deepEqual(rows.map(row => [row.provider, row.removable]), [['google', true], ['discord', true]]);
});

test('a way the host no longer offers is still listed, to remove', () => {
  const rows = wayRows([{ provider: 'google' }, { provider: 'retired' }], me);

  assert.deepEqual(rows.map(row => [row.name, row.held]), [['Google', true], ['Retired', true], ['Discord', false]]);
});

test('a provider this page cannot draw a button for is not offered to add', () => {
  const rows = wayRows([{ provider: 'google' }], { ...me, signIn: [...me.signIn, { provider: 'facebook', clientId: 'f' }] });

  assert.deepEqual(rows.map(row => row.provider), ['google', 'discord']);
});

// Shaped as the host answers a removal that stopped before its binding went (AccountIdentitiesApiTests,
// A_removal_stopped_before_its_binding_went_is_listed_as_pending_so_the_page_can_finish_it, #1262).
const stopped = [{ provider: 'google', removable: false, pending: false }, { provider: 'discord', removable: true, pending: true }];

test('a way whose removal stopped is listed apart, to finish removing, and not offered to add', () => {
  const rows = wayRows(stopped, me);

  assert.deepEqual(rows.map(row => [row.provider, row.held, row.pending]), [['google', true, false], ['discord', true, true]]);
  assert.equal(wayHint(rows[1]), 'Its removal did not finish. You no longer sign in with it.');
});

test('finishing a removal that stopped is a DELETE of that way, and says it is removed', async () => {
  const doc = page();
  const session = tab();
  session.setItem('downfall.recentSignIn', JSON.stringify({ value: 'R', at: Date.now() }));
  const asked = [];
  const answers = [{ identities: stopped }, { identities: [{ provider: 'google', removable: false, pending: false }] }];
  const ways = mountWays(doc, { request: async (path, body, _, method) => { asked.push([path, method]); return answers.shift(); }, session });
  ways.setAccount({ id: 'a1', username: 'MarkF' }, me);
  await ways.shown('account');
  const dialog = doc.getElementById('action-dialog');
  dialog.showModal = () => { dialog.returnValue = 'confirm'; dialog.listeners.close(); };
  const finish = doc.getElementById('ways-list').children[1].children.at(-1);

  await finish.listeners.click();

  assert.equal(finish.textContent, 'Finish removing');
  assert.equal(finish['aria-label'], 'Finish removing Discord');
  assert.deepEqual(asked, [['/api/account/identities', undefined], ['/api/account/identities/discord', 'DELETE']]);
  assert.equal(doc.getElementById('ways-note').textContent, 'Discord removed.');
  assert.equal(doc.getElementById('action-title').textContent, 'Finish removing Discord?');
  assert.equal(doc.getElementById('action-detail').textContent, finishWords('discord').detail);
});

test('finishing a removal asks first in words that say this device stays signed in and pop-ups stop, and no sign-out', () => {
  const words = finishWords('discord');

  assert.equal(words.title, 'Finish removing Discord?');
  assert.equal(words.detail, 'You no longer sign in with Discord. This finishes its removal. You stay signed in here. Pop-ups stop on every device, this one too.');
  assert.equal(words.action, 'Finish removing');
  assert.doesNotMatch(words.detail, /signed out/);
});

test('removing a way in says the other devices are signed out', () => {
  const words = removeWords('discord');

  assert.equal(words.title, 'Remove Discord?');
  assert.match(words.detail, /Every other device is signed out/);
  assert.match(words.detail, /pop-ups stop/);
  assert.equal(wayName('dev'), 'A typed name');
});

test('a line for Settings is handed over once', () => {
  const storage = tab();
  keepWaysNote(storage, 'Discord added.');

  assert.equal(takeWaysNote(storage), 'Discord added.');
  assert.equal(takeWaysNote(storage), null);
});

function page() {
  const made = new Map();
  const node = id => ({
    id, hidden: true, textContent: '', children: [], className: '', listeners: {}, dataset: {},
    classList: { add() {}, remove() {}, toggle() {} },
    removeAttribute() {},
    replaceChildren(...nodes) { this.children = nodes; },
    append(...nodes) { this.children.push(...nodes); },
    setAttribute(name, value) { this[name] = value; },
    addEventListener(type, listener) { this.listeners[type] = listener; },
  });
  return {
    getElementById: id => made.get(id) ?? made.set(id, node(id)).get(id),
    createElement: () => node(null),
  };
}

test('the ways are read when Settings shows Account, once per account, and not on every page', async () => {
  const doc = page();
  const asked = [];
  const ways = mountWays(doc, { request: async path => { asked.push(path); return { identities: [{ provider: 'google', removable: true }, { provider: 'discord', removable: true }] }; }, session: tab() });

  ways.setAccount({ id: 'a1', username: 'MarkF' }, me);
  await ways.shown('game');
  const before = [...asked];
  await ways.shown('account');
  await ways.shown('account');

  assert.deepEqual(before, []);
  assert.deepEqual(asked, ['/api/account/identities']);
  assert.equal(doc.getElementById('ways').hidden, false);
  assert.equal(doc.getElementById('ways-list').children.length, 2);
});

test('a line handed over opens Settings on Account once signed in, and is said there', async () => {
  const doc = page();
  const storage = tab();
  keepWaysNote(storage, 'Discord added. You can sign in with it now.');
  const opened = [];
  const ways = mountWays(doc, { request: async () => ({ identities: [{ provider: 'google', removable: false }] }), session: storage, openSettings: name => opened.push(name) });

  ways.setAccount({ id: 'a1', username: 'MarkF' }, me);
  await ways.shown('account');

  assert.deepEqual(opened, ['account']);
  assert.equal(doc.getElementById('ways-note').textContent, 'Discord added. You can sign in with it now.');
  assert.equal(takeWaysNote(storage), null);
});

test('nobody signed in reads no ways and sees none', async () => {
  const doc = page();
  let asked = false;
  const ways = mountWays(doc, { request: async () => { asked = true; return {}; }, session: tab() });

  ways.setAccount(null, me);
  await ways.shown('account');

  assert.equal(asked, false);
  assert.equal(doc.getElementById('ways').hidden, true);
});

test('a host that signs in through no provider shows no ways', async () => {
  const doc = page();
  const ways = mountWays(doc, { request: async () => ({ identities: [{ provider: 'dev' }] }), session: tab() });

  ways.setAccount({ id: 'a1', username: 'MarkF' }, { accounts: 'dev' });

  assert.equal(doc.getElementById('ways').hidden, true);
});

test('removing a way in is a DELETE that carries JSON, which the host\'s fence asks of every write', async () => {
  const sent = [];
  const fetchImpl = async (path, options) => { sent.push([path, options]); return { ok: true, text: async () => '{"identities":[]}' }; };
  const { accountRequest } = await import('./account.js');

  await removeWay((path, body, _, method) => accountRequest(path, body, fetchImpl, method), 'discord', 'R');

  assert.equal(sent[0][0], '/api/account/identities/discord');
  assert.equal(sent[0][1].method, 'DELETE');
  assert.equal(sent[0][1].headers['Content-Type'], 'application/json');
  assert.deepEqual(JSON.parse(sent[0][1].body), { recent: 'R' });
});

test('a removal with no recent sign-in in the tab asks for a sign in again and asks the host nothing', async () => {
  const doc = page();
  doc.getElementById('ways-again').hidden = true;
  const asked = [];
  const ways = mountWays(doc, { request: async (path, body, _, method) => { asked.push([path, method]); return { identities: [{ provider: 'google', removable: true }, { provider: 'discord', removable: true }] }; }, session: tab() });
  ways.setAccount({ id: 'a1', username: 'MarkF' }, me);
  await ways.shown('account');

  await doc.getElementById('ways-list').children[1].children[1].listeners.click();

  assert.deepEqual(asked, [['/api/account/identities', undefined]]);
  assert.equal(doc.getElementById('ways-again').hidden, false);
  assert.match(doc.getElementById('ways-problem').textContent, /sign in again/);
});

test('Microsoft offered and not yet used is added through its own authorize page, as Discord is', async () => {
  const doc = page();
  const session = tab();
  session.setItem('downfall.recentSignIn', JSON.stringify({ value: 'R', at: Date.now() }));
  const went = [];
  const offering = { accounts: 'signin', signIn: [{ provider: 'google', clientId: 'g-client' }, { provider: 'microsoft', clientId: 'ms-app' }] };
  const ways = mountWays(doc, { request: async () => ({ identities: [{ provider: 'google', removable: false }] }), session, win: { location: { origin: 'https://play.example', assign: url => went.push(url) } } });
  ways.setAccount({ id: 'a1', username: 'MarkF' }, offering);
  await ways.shown('account');

  const add = doc.getElementById('ways-list').children[1].children.at(-1);
  await add.listeners.click();

  assert.equal(add['aria-label'], 'Add Microsoft');
  assert.match(went.at(-1), /^https:\/\/login\.microsoftonline\.com\/consumers\/oauth2\/v2\.0\/authorize\?/);
  assert.equal(JSON.parse(session.getItem('downfall.microsoftSignIn')).purpose, 'link');
});
