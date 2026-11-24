import test from 'node:test';
import assert from 'node:assert/strict';
import { keepWaysNote, mountWays, removeWay, removeWords, takeWaysNote, wayName, wayRows } from './ways.js';

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
  const rows = wayRows([{ provider: 'google' }], { ...me, signIn: [...me.signIn, { provider: 'microsoft', clientId: 'm' }] });

  assert.deepEqual(rows.map(row => row.provider), ['google', 'discord']);
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
    id, hidden: true, textContent: '', children: [], className: '', listeners: {},
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
