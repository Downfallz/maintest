import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

// The worker, run against what a browser gives one: `self` with its clients, its registration and the events
// it listens for. A notice is a `push` event whose data reads as JSON.
function worker({ windows = [], opened = [] } = {}) {
  const shown = [];
  const handlers = {};
  const self = {
    addEventListener: (kind, handler) => { handlers[kind] = handler; },
    skipWaiting: () => Promise.resolve(),
    clients: {
      claim: () => Promise.resolve(),
      matchAll: async () => windows,
      openWindow: async url => { opened.push(url); },
    },
    registration: { showNotification: async (title, options) => { shown.push({ title, ...options }); } },
  };
  const context = vm.createContext({ self, URL });
  vm.runInContext(readFileSync(new URL('./sw.js', import.meta.url), 'utf8'), context);
  const fire = async (kind, event) => {
    let waited = Promise.resolve();
    handlers[kind]({ ...event, waitUntil: promise => { waited = promise; } });
    await waited;
  };
  return { handlers, shown, opened, fire, self };
}

const push = notice => ({ data: { json: () => notice } });

test('a push shows the notice the host sent, with a tap going to the game', async () => {
  const w = worker();

  await w.fire('push', push({ title: 'Your turn', body: 'Round 7: choose a tempo.', tag: 'turn:player2', url: '/play' }));

  assert.equal(w.shown.length, 1);
  assert.equal(w.shown[0].title, 'Your turn');
  assert.equal(w.shown[0].body, 'Round 7: choose a tempo.');
  assert.equal(w.shown[0].tag, 'turn:player2');
  assert.equal(w.shown[0].data.url, '/play');
});

test('a push arriving while a window of the table is in front of somebody shows nothing', async () => {
  const w = worker({ windows: [{ url: 'https://table.example/play', focused: true, visibilityState: 'visible' }] });

  await w.fire('push', push({ title: 'Your turn' }));

  assert.equal(w.shown.length, 0);
});

test('a push arriving while the arena is in front of somebody shows nothing: the arena is the game', async () => {
  const w = worker({ windows: [{ url: 'https://table.example/arena?table=a&seat=player1', focused: true, visibilityState: 'visible' }] });

  await w.fire('push', push({ title: 'Your turn', body: 'Round 3: choose a spell.', url: '/play?table=a' }));

  assert.equal(w.shown.length, 0);
});

test('an arena in front of somebody at another table does not stand in for this one: the notice shows', async () => {
  const w = worker({ windows: [{ url: 'https://table.example/arena?table=b&seat=player1', focused: true, visibilityState: 'visible' }] });

  await w.fire('push', push({ title: 'Your turn', url: '/play?table=a' }));

  assert.equal(w.shown.length, 1);
});

test('an arena opened from a seat\'s own link names no table, and keeps the notice quiet while in front', async () => {
  const w = worker({ windows: [{ url: 'https://table.example/arena?player1=tok', focused: true, visibilityState: 'visible' }] });

  await w.fire('push', push({ title: 'Your turn', url: '/play?table=a' }));

  assert.equal(w.shown.length, 0);
});

test('the arena lab in front of somebody is a study, not a seat: the notice shows', async () => {
  const w = worker({ windows: [{ url: 'https://table.example/arena-lab', focused: true, visibilityState: 'visible' }] });

  await w.fire('push', push({ title: 'Your turn', url: '/play?table=a' }));

  assert.equal(w.shown.length, 1);
});

test('a tap with the arena open at the notice\'s table brings it to the front and leaves it in the arena', async () => {
  const went = [];
  const arena = { url: 'https://table.example/arena?table=a&seat=player1', focused: false, focus: async () => { went.push('focus:arena'); }, navigate: async url => { went.push(url); } };
  const w = worker({ windows: [arena] });

  await w.fire('notificationclick', { notification: { data: { url: '/play?table=a' }, close: () => {} } });

  assert.deepEqual(went, ['focus:arena']);
  assert.deepEqual(w.opened, []);
});

test('an arena opened from a seat\'s own link names no table, and stands for the one the notice came from', async () => {
  const went = [];
  const arena = { url: 'https://table.example/arena?player1=tok', focused: false, focus: async () => { went.push('focus:arena'); }, navigate: async url => { went.push(url); } };
  const w = worker({ windows: [arena] });

  await w.fire('notificationclick', { notification: { data: { url: '/play?table=a' }, close: () => {} } });

  assert.deepEqual(went, ['focus:arena']);
  assert.deepEqual(w.opened, []);
});

test('a tap with the arena open at another table leaves it alone and opens the game in a new window', async () => {
  const went = [];
  const arena = { url: 'https://table.example/arena?table=b&seat=player1', focused: false, focus: async () => { went.push('focus:arena'); }, navigate: async url => { went.push(url); } };
  const w = worker({ windows: [arena] });

  await w.fire('notificationclick', { notification: { data: { url: '/play?table=a' }, close: () => {} } });

  assert.deepEqual(went, []);
  assert.deepEqual(w.opened, ['/play?table=a']);
});

test('a tap on a lobby notice never steers the arena: the lobby opens in a new window', async () => {
  const went = [];
  const arena = { url: 'https://table.example/arena?table=a&seat=player1', focused: true, visibilityState: 'visible', focus: async () => { went.push('focus:arena'); }, navigate: async url => { went.push(url); } };
  const w = worker({ windows: [arena] });

  await w.fire('push', push({ title: 'A friend', url: '/lobby#friends' }));
  assert.equal(w.shown.length, 1, 'the arena is the game, not the lobby');

  await w.fire('notificationclick', { notification: { data: { url: '/lobby#friends' }, close: () => {} } });
  assert.deepEqual(went, []);
  assert.deepEqual(w.opened, ['/lobby#friends']);
});

test('a test notice shows over the game in front of somebody: it is the one notice that was asked for', async () => {
  const w = worker({ windows: [{ url: 'https://table.example/play', focused: true, visibilityState: 'visible' }] });

  await w.fire('push', push({ title: 'Notifications work', test: true, tag: 'test:player1' }));

  assert.equal(w.shown.length, 1);
  assert.equal(w.shown[0].tag, 'test:player1');
});

test('a page still focused on a phone whose screen went off is not in front of anybody: the notice shows', async () => {
  const w = worker({ windows: [{ url: 'https://table.example/play', focused: true, visibilityState: 'hidden' }] });

  await w.fire('push', push({ title: 'Your turn' }));

  assert.equal(w.shown.length, 1);
});

test('a push arriving while the page is open but not in front shows the notice', async () => {
  const w = worker({ windows: [{ url: 'https://table.example/play', focused: false }] });

  await w.fire('push', push({ title: 'Your turn' }));

  assert.equal(w.shown.length, 1);
});

test('a push with no readable data still says something rather than nothing', async () => {
  const w = worker();

  await w.fire('push', { data: { json: () => { throw new SyntaxError('not json'); } } });

  assert.equal(w.shown[0].title, 'Your turn');
  assert.equal(w.shown[0].tag, 'turn');
});

test('a tap focuses the page already on the game, or opens one', async () => {
  const focused = [];
  const page = { url: 'https://table.example/play', focused: false, focus: async () => { focused.push('play'); } };
  const w = worker({ windows: [page] });
  const closed = [];
  const notification = { data: { url: '/play' }, close: () => closed.push(true) };

  await w.fire('notificationclick', { notification });
  assert.deepEqual(focused, ['play']);
  assert.deepEqual(closed, [true]);
  assert.deepEqual(w.opened, []);

  const none = worker();
  await none.fire('notificationclick', { notification });
  assert.deepEqual(none.opened, ['/play']);
});

test('a tap with a page open elsewhere on the table brings it to the game', async () => {
  const went = [];
  const lobby = { url: 'https://table.example/', focused: false, focus: async () => {}, navigate: async url => { went.push(url); } };
  const w = worker({ windows: [lobby] });

  await w.fire('notificationclick', { notification: { data: { url: '/play' }, close: () => {} } });

  assert.deepEqual(went, ['/play']);
});

test('a notice for the lobby is not shown over a lobby in front of somebody, and is shown over the game', async () => {
  const notice = { title: 'A friend', body: 'bastien wants to be your friend', url: '/lobby#friends', tag: 'friend:acct' };
  const atLobby = worker({ windows: [{ url: 'https://table.example/', focused: true, visibilityState: 'visible' }] });
  await atLobby.fire('push', push(notice));
  assert.equal(atLobby.shown.length, 0, 'the lobby at `/` is the lobby');

  const atGame = worker({ windows: [{ url: 'https://table.example/play', focused: true, visibilityState: 'visible' }] });
  await atGame.fire('push', push(notice));
  assert.equal(atGame.shown.length, 1);
  assert.equal(atGame.shown[0].data.url, '/lobby#friends');
});

test('a tap on a lobby notice goes to the lobby page already open, not to a game page', async () => {
  const went = [];
  const game = { url: 'https://table.example/play', focused: false, focus: async () => { went.push('focus:play'); }, navigate: async url => { went.push(url); } };
  const lobby = { url: 'https://table.example/', focused: false, focus: async () => { went.push('focus:lobby'); }, navigate: async url => { went.push(url); } };
  const w = worker({ windows: [game, lobby] });

  await w.fire('notificationclick', { notification: { data: { url: '/lobby#friends' }, close: () => {} } });

  assert.deepEqual(went, ['focus:lobby', '/lobby#friends']);
  assert.deepEqual(w.opened, []);
});

test('every page is told a notice arrived, shown or not', async () => {
  const told = [];
  const lobby = { url: 'https://table.example/', focused: true, visibilityState: 'visible', postMessage: message => told.push(message) };
  const w = worker({ windows: [lobby] });

  await w.fire('push', push({ title: 'A friend', url: '/lobby#friends' }));

  assert.deepEqual(JSON.parse(JSON.stringify(told)), [{ kind: 'notice', url: '/lobby#friends' }]);
  assert.equal(w.shown.length, 0, 'the lobby in front of somebody is the notice');
});

test('the worker caches nothing: it listens for no fetch', () => {
  assert.equal(worker().handlers.fetch, undefined);
});

test('a focused window on the lobby does not stand in for the notice: only the game does', async () => {
  const w = worker({ windows: [{ url: 'https://table.example/', focused: true }] });

  await w.fire('push', push({ title: 'Your turn' }));

  assert.equal(w.shown.length, 1);
});

test('a tap on a lobby page this worker cannot steer opens the game in a new window', async () => {
  const lobby = { url: 'https://table.example/', focused: false, focus: async () => {}, navigate: async () => { throw new TypeError('not controlled'); } };
  const w = worker({ windows: [lobby] });

  await w.fire('notificationclick', { notification: { data: { url: '/play' }, close: () => {} } });

  assert.deepEqual(w.opened, ['/play']);
});

test('a subscription the browser rotated is made again with the same key, and every page is told', async () => {
  const told = [];
  const made = [];
  const page = { url: 'https://table.example/play', focused: false, postMessage: message => told.push(message) };
  const w = worker({ windows: [page] });
  w.self.registration.pushManager = { subscribe: async options => { made.push(options); } };

  await w.fire('pushsubscriptionchange', { oldSubscription: { options: { applicationServerKey: 'key' } } });

  // Read across the worker's realm, whose objects are not this one's.
  assert.deepEqual(JSON.parse(JSON.stringify(made)), [{ userVisibleOnly: true, applicationServerKey: 'key' }]);
  assert.deepEqual(JSON.parse(JSON.stringify(told)), [{ kind: 'subscription-changed' }]);
});

test('a tap sends the page already on the game to the table the notice came from', async () => {
  const went = [];
  const game = { url: 'https://table.example/play', focused: false, focus: async () => {}, navigate: async url => { went.push(url); } };
  const w = worker({ windows: [game] });

  await w.fire('notificationclick', { notification: { data: { url: '/play?table=a' }, close: () => {} } });

  assert.deepEqual(went, ['/play?table=a']);
  assert.deepEqual(w.opened, []);
});
