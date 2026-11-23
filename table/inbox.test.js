import test from 'node:test';
import assert from 'node:assert/strict';
import { badgeText, bellLabel, inboxCount, inboxItems, mountInbox, readUpTo } from './inbox.js';

const table = (id, seats, extra = {}) => ({ id, format: '3v3', over: false, waiting: false, round: 4, seats, ...extra });
const me = { seat: 'player1', kind: 'person', name: 'MarcF' };

test('a game against another person where this player is asked is their turn, named by who it is against', () => {
  const mine = [
    { table: table('a', [{ ...me, turn: true }, { seat: 'player2', kind: 'person', name: 'Bastien' }]), seats: [{ seat: 'player1', token: 't1' }] },
    { table: table('b', [me, { seat: 'player2', kind: 'person', name: 'Julie', turn: true }]), seats: [{ seat: 'player1', token: 't2' }] },
  ];
  const items = inboxItems({ mine });
  assert.deepEqual(items.map(item => item.title), ['Your turn against Bastien']);
  assert.equal(items[0].detail, '3v3 battle · Round 4');
  assert.deepEqual(items[0].seats, [{ seat: 'player1', token: 't1' }]);
});

test('a game against a bot, on one shared device, finished or not begun never counts as a turn waiting', () => {
  const bot = { table: table('c', [{ ...me, turn: true }, { seat: 'player2', kind: 'bot', name: 'Lookahead' }]), seats: [{ seat: 'player1', token: 't3' }] };
  const both = { table: table('d', [{ ...me, turn: true }, { seat: 'player2', kind: 'person', name: 'MarcF' }]), seats: [{ seat: 'player1', token: 'a' }, { seat: 'player2', token: 'b' }] };
  const over = { table: table('e', [{ ...me, turn: true }, { seat: 'player2', kind: 'person', name: 'Bastien' }], { over: true }), seats: [{ seat: 'player1', token: 'c' }] };
  const waiting = { table: table('f', [{ ...me, turn: true }, { seat: 'player2', kind: 'open' }], { waiting: true }), seats: [{ seat: 'player1', token: 'd' }] };
  assert.deepEqual(inboxItems({ mine: [bot, both, over, waiting] }), []);
});

test('turns come first, then invitations, friend requests and declined invitations, each with what its action needs', () => {
  const items = inboxItems({
    mine: [
      { table: table('x', [me, { seat: 'player2', kind: 'person', name: 'Alex' }]), seats: [{ seat: 'player1', token: 't' }], invited: { username: 'Alex', declined: true } },
      { table: table('y', [{ ...me, turn: true }, { seat: 'player2', kind: 'person', name: 'Bastien' }]), seats: [{ seat: 'player1', token: 'u' }] },
    ],
    invitations: [{ table: { id: 'z', format: '1v1' }, from: { id: 'acct_b', username: 'Bastien' } }],
    friends: { friends: [], incoming: [{ id: 'acct_c', username: 'Julie' }], outgoing: [] },
  });
  assert.deepEqual(items.map(item => item.kind), ['turn', 'invitation', 'friend', 'declined']);
  assert.equal(items[1].title, 'Bastien invites you'); assert.equal(items[1].table, 'z'); assert.equal(items[1].detail, '1v1 battle');
  assert.equal(items[2].title, 'Julie wants to be your friend'); assert.equal(items[2].account, 'acct_c');
  assert.equal(items[3].title, 'Alex declined your invitation');
});

test('an invitation still waiting for its answer, and nothing at all, give nothing to do', () => {
  const pending = { table: table('w', [me, { seat: 'player2', kind: 'person', name: 'Alex' }], { waiting: true }), seats: [{ seat: 'player1', token: 't' }], invited: { username: 'Alex', declined: false } };
  assert.deepEqual(inboxItems({ mine: [pending] }), []);
  assert.deepEqual(inboxItems(), []);
});

test('the badge counts up to nine, then says 9+, and the bell says how much waits', () => {
  assert.equal(badgeText(0), '');
  assert.equal(badgeText(3), '3');
  assert.equal(badgeText(12), '9+');
  assert.equal(bellLabel(2), 'Notifications · 2 waiting');
  assert.equal(bellLabel(0), 'Notifications · nothing waiting');
  assert.equal(bellLabel(1, 2), 'Notifications · 1 waiting · 2 new');
  assert.equal(bellLabel(0, 1), 'Notifications · nothing waiting · 1 new');
});

const news = [
  { id: 30, kind: 'match-over', title: 'Victory', body: 'You won against Bastien.', url: '/battle?table=t2', at: '2026-11-17T11:00:00Z', read: false },
  { id: 20, kind: 'friend-accepted', title: 'Julie accepted your request', body: '', url: '/lobby', at: '2026-11-15T12:00:00Z', read: true },
];

test('the news comes after what waits, newest first, each with what it said, when, and where a tap goes', () => {
  const now = Date.parse('2026-11-17T12:00:00Z');
  const items = inboxItems({ friends: { incoming: [{ id: 'acct_c', username: 'Julie' }] }, news }, now);

  assert.deepEqual(items.map(item => item.kind), ['friend', 'news', 'news']);
  assert.deepEqual(items.slice(1).map(item => [item.title, item.detail, item.url, item.unread]), [
    ['Victory', 'You won against Bastien. · 1 h ago', '/battle?table=t2', true],
    ['Julie accepted your request', '2 d ago', '/lobby', false],
  ]);
});

test('a news entry only links inside the site, and one without an id or a title is not drawn', () => {
  const items = inboxItems({ news: [{ id: 1, title: 'Away', url: 'https://elsewhere.example/' }, { id: 3, title: 'Away', url: '//elsewhere.example/' }, { id: 4, title: 'Away', url: '/\\elsewhere.example/' }, { id: 5, title: 'Here', url: '/lobby' }, { title: 'No id' }, { id: 2 }] });
  assert.deepEqual(items.map(item => [item.key, item.url]), [['news:1', null], ['news:3', null], ['news:4', null], ['news:5', '/lobby']]);
});

test('the bell counts what waits and the unread news, never the news already read', () => {
  const mine = [{ table: table('a', [{ ...me, turn: true }, { seat: 'player2', kind: 'person', name: 'Bastien' }]), seats: [{ seat: 'player1', token: 't1' }] }];
  assert.deepEqual(inboxCount(inboxItems({ mine, news })), { waiting: 1, unread: 1 });
  assert.deepEqual(inboxCount(inboxItems({ news: news.map(entry => ({ ...entry, read: true })) })), { waiting: 0, unread: 0 });
});

test('reading the news names the newest entry, and nothing once every entry is read', () => {
  assert.equal(readUpTo(inboxItems({ news })), 30);
  assert.equal(readUpTo(inboxItems({ news: [{ ...news[0], read: true }, news[1]] })), null);
  assert.equal(readUpTo(inboxItems()), null);
});

test('a turn waiting says what happened since the last visit', () => {
  const now = Date.parse('2026-11-17T12:00:00Z');
  const mine = [{ table: table('a', [{ ...me, turn: true }, { seat: 'player2', kind: 'person', name: 'Bastien' }]), seats: [{ seat: 'player1', token: 't1' }],
    recap: { casts: 3, lastMoveAt: '2026-11-17T07:00:00Z' } }];

  const [item] = inboxItems({ mine }, now);

  assert.equal(item.detail, '3v3 battle · Round 4 · 3 casts since your last visit · last move 5 h ago');
});

test('a turn waiting with no cast since the last visit says only when the last move was', () => {
  const now = Date.parse('2026-11-17T12:00:00Z');
  const mine = [{ table: table('a', [{ ...me, turn: true }, { seat: 'player2', kind: 'person', name: 'Bastien' }]), seats: [{ seat: 'player1', token: 't1' }],
    recap: { casts: 0, lastMoveAt: '2026-11-15T12:00:00Z' } }];

  const [item] = inboxItems({ mine }, now);

  assert.equal(item.detail, '3v3 battle · Round 4 · last move 2 d ago');
});

// A document with only what the bell and its copy in the site menu read (#1002): elements by id, created nodes and
// their listeners. Each starts shown and written, so a test sees what the bell hides or empties; `request` answers
// the lists the test hands it.
function inboxDocument() {
  const element = (id = '') => {
    const node = { id, hidden: false, textContent: 'stale', attributes: {}, children: [], listeners: {}, dataset: {} };
    node.setAttribute = (name, value) => { node.attributes[name] = String(value); };
    node.addEventListener = (type, listener) => { (node.listeners[type] ??= []).push(listener); };
    node.append = (...items) => { node.children.push(...items); };
    node.replaceChildren = (...items) => { node.children = items; };
    return node;
  };
  const ids = ['inbox-open', 'inbox-badge', 'inbox-problem', 'inbox-list', 'inbox-dialog', 'inbox-open-menu', 'inbox-menu-count', 'menu-inbox-badge', 'menu-inbox-said'];
  const byId = Object.fromEntries(ids.map(id => [id, element(id)]));
  return { byId, hidden: false, getElementById: id => byId[id] ?? null, createElement: () => element() };
}

function mountedInbox(account, mine = []) {
  const doc = inboxDocument();
  const request = async path => {
    if (path === '/api/lobby/mine') return { tables: mine, invitations: [] };
    if (path === '/api/friends') return { friends: [], incoming: [], outgoing: [] };
    if (path === '/api/account/notices') return { notices: [] };
    throw new Error(`Unexpected ${path}`);
  };
  const win = { setTimeout: () => 0, clearTimeout: () => {}, navigator: {}, location: { assign() {} } };
  const inbox = mountInbox(doc, { request, win, changes: () => () => {}, reopen: () => {} });
  inbox.setAccount(account);
  return { doc, inbox };
}
const turnAgainst = (id, name) => ({ table: table(id, [{ ...me, turn: true }, { seat: 'player2', kind: 'person', name }]), seats: [{ seat: 'player1', token: id }] });
const settled = () => new Promise(resolve => setImmediate(resolve));

test('a player who signs out gets no bell entry in the site menu, its counts hidden and its description emptied', async () => {
  const { doc, inbox } = mountedInbox({ id: 'acct_a', username: 'MarcF' }, [turnAgainst('a', 'Bastien'), turnAgainst('b', 'Julie')]);
  await settled();
  assert.equal(doc.byId['inbox-open-menu'].hidden, false);
  assert.equal(doc.byId['menu-inbox-said'].textContent, 'Notifications · 2 waiting');
  inbox.setAccount(null);
  await settled();
  for (const id of ['inbox-open-menu', 'inbox-open', 'inbox-menu-count', 'menu-inbox-badge']) assert.equal(doc.byId[id].hidden, true, id);
  assert.equal(doc.byId['menu-inbox-said'].textContent, '');
});

test('a player with nothing waiting sees the menu\'s entry, both its counts hidden and no description', async () => {
  const { doc } = mountedInbox({ id: 'acct_a', username: 'MarcF' });
  await settled();
  assert.equal(doc.byId['inbox-open-menu'].hidden, false);
  for (const id of ['inbox-menu-count', 'menu-inbox-badge']) {
    assert.equal(doc.byId[id].hidden, true);
    assert.equal(doc.byId[id].textContent, '');
  }
  assert.equal(doc.byId['menu-inbox-said'].textContent, '');
});

test('two turns waiting show 2 on the menu\'s entry and its button, and the button is described by the bell\'s words', async () => {
  const { doc } = mountedInbox({ id: 'acct_a', username: 'MarcF' }, [turnAgainst('a', 'Bastien'), turnAgainst('b', 'Julie')]);
  await settled();
  assert.equal(doc.byId['inbox-open-menu'].hidden, false);
  for (const id of ['inbox-menu-count', 'menu-inbox-badge']) {
    assert.equal(doc.byId[id].hidden, false);
    assert.equal(doc.byId[id].textContent, '2');
  }
  assert.equal(doc.byId['menu-inbox-said'].textContent, 'Notifications · 2 waiting');
});
