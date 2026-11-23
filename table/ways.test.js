import test from 'node:test';
import assert from 'node:assert/strict';
import { keepWaysNote, mountWays, removeWords, takeWaysNote, wayName, wayRows } from './ways.js';

// Shaped as the host answers: GET /api/account/identities and /api/me (AccountIdentitiesApiTests, AccountApiTests).
const me = { accounts: 'signin', signIn: [{ provider: 'google', clientId: 'g-client' }, { provider: 'discord', clientId: '1311' }] };

function tab() {
  const store = new Map();
  return { getItem: key => store.get(key) ?? null, setItem: (key, value) => store.set(key, value), removeItem: key => store.delete(key), store };
}

test('the account\'s ways come first, then the ways the host offers that it does not use', () => {
  const rows = wayRows([{ provider: 'google' }], me);

  assert.deepEqual(rows.map(row => [row.provider, row.held]), [['google', true], ['discord', false]]);
  assert.equal(rows[0].removable, false, 'the only way in cannot be removed');
  assert.equal(rows[1].clientId, '1311');
});

test('every way the account uses is removable once it has two', () => {
  const rows = wayRows([{ provider: 'google' }, { provider: 'discord' }], me);

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
  assert.match(words.detail, /signed out on every other device/);
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

test('a signed-in account\'s ways are read from the host and drawn, and a handed line opens Settings on Account', async () => {
  const doc = page();
  const storage = tab();
  keepWaysNote(storage, 'Discord added. You can sign in with it now.');
  const opened = [];
  const asked = [];
  const ways = mountWays(doc, { request: async path => { asked.push(path); return { identities: [{ provider: 'google' }, { provider: 'discord' }] }; }, session: storage, openSettings: tab => opened.push(tab) });

  await ways.setAccount({ id: 'a1', username: 'MarkF' }, me);

  assert.deepEqual(asked, ['/api/account/identities']);
  assert.equal(doc.getElementById('ways').hidden, false);
  assert.equal(doc.getElementById('ways-list').children.length, 2);
  assert.deepEqual(opened, ['account']);
  assert.equal(doc.getElementById('ways-note').textContent, 'Discord added. You can sign in with it now.');
});

test('nobody signed in reads no ways and sees none', async () => {
  const doc = page();
  let asked = false;
  const ways = mountWays(doc, { request: async () => { asked = true; return {}; }, session: tab() });

  await ways.setAccount(null, me);

  assert.equal(asked, false);
  assert.equal(doc.getElementById('ways').hidden, true);
});

test('a host that signs in through no provider shows no ways', async () => {
  const doc = page();
  const ways = mountWays(doc, { request: async () => ({ identities: [{ provider: 'dev' }] }), session: tab() });

  await ways.setAccount({ id: 'a1', username: 'MarkF' }, { accounts: 'dev' });

  assert.equal(doc.getElementById('ways').hidden, true);
});
