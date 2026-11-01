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
