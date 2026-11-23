import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';
import { reachedModules } from '../scripts/module-imports.mjs';

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

for (const path of ['/battle', '/arena']) {
  test(`a focused match at ${path} suppresses its own turn notice`, async () => {
    const w = worker({ windows: [{ url: `https://table.example${path}?table=a&seat=player1`, focused: true, visibilityState: 'visible' }] });
    await w.fire('push', push({ title: 'Your turn', body: 'Round 3: choose a spell.', url: '/play?table=a' }));
    assert.equal(w.shown.length, 0);
  });
}

test('a battle in front of somebody at another table does not stand in for this one: the notice shows', async () => {
  const w = worker({ windows: [{ url: 'https://table.example/battle?table=b&seat=player1', focused: true, visibilityState: 'visible' }] });

  await w.fire('push', push({ title: 'Your turn', url: '/play?table=a' }));

  assert.equal(w.shown.length, 1);
});

test('a battle opened from a seat\'s own link names no table, and keeps the notice quiet while in front', async () => {
  const w = worker({ windows: [{ url: 'https://table.example/battle?player1=tok', focused: true, visibilityState: 'visible' }] });

  await w.fire('push', push({ title: 'Your turn', url: '/play?table=a' }));

  assert.equal(w.shown.length, 0);
});

test('the battle lab in front of somebody is a study, not a seat: the notice shows', async () => {
  const w = worker({ windows: [{ url: 'https://table.example/battle-lab', focused: true, visibilityState: 'visible' }] });

  await w.fire('push', push({ title: 'Your turn', url: '/play?table=a' }));

  assert.equal(w.shown.length, 1);
});

test('a tap with the battle open at the notice\'s table brings it to the front and leaves it in the battle', async () => {
  const went = [];
  const battle = { url: 'https://table.example/battle?table=a&seat=player1', focused: false, focus: async () => { went.push('focus:battle'); }, navigate: async url => { went.push(url); } };
  const w = worker({ windows: [battle] });

  await w.fire('notificationclick', { notification: { data: { url: '/play?table=a' }, close: () => {} } });

  assert.deepEqual(went, ['focus:battle']);
  assert.deepEqual(w.opened, []);
});

test('a battle opened from a seat\'s own link names no table, and stands for the one the notice came from', async () => {
  const went = [];
  const battle = { url: 'https://table.example/battle?player1=tok', focused: false, focus: async () => { went.push('focus:battle'); }, navigate: async url => { went.push(url); } };
  const w = worker({ windows: [battle] });

  await w.fire('notificationclick', { notification: { data: { url: '/play?table=a' }, close: () => {} } });

  assert.deepEqual(went, ['focus:battle']);
  assert.deepEqual(w.opened, []);
});

test('a tap with the battle open at another table leaves it alone and opens the game in a new window', async () => {
  const went = [];
  const battle = { url: 'https://table.example/battle?table=b&seat=player1', focused: false, focus: async () => { went.push('focus:battle'); }, navigate: async url => { went.push(url); } };
  const w = worker({ windows: [battle] });

  await w.fire('notificationclick', { notification: { data: { url: '/play?table=a' }, close: () => {} } });

  assert.deepEqual(went, []);
  assert.deepEqual(w.opened, ['/play?table=a']);
});

test('a tap on a lobby notice never steers the battle: the lobby opens in a new window', async () => {
  const went = [];
  const battle = { url: 'https://table.example/battle?table=a&seat=player1', focused: true, visibilityState: 'visible', focus: async () => { went.push('focus:battle'); }, navigate: async url => { went.push(url); } };
  const w = worker({ windows: [battle] });

  await w.fire('push', push({ title: 'A friend', url: '/lobby#friends' }));
  assert.equal(w.shown.length, 1, 'the battle is the game, not the lobby');

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

// What a fetch event is, for the offline page (ADR 0166) and the rulebook (ADR 0191): its request's mode and
// address, a preload the browser may have started, what the worker answered with, and what it kept doing after.
function fetched(mode, { preloadResponse, url = 'https://table.example/battle', method = 'GET' } = {}) {
  let answered = null;
  const kept = [];
  const event = { request: { mode, url, method }, preloadResponse, respondWith: promise => { answered = promise; }, waitUntil: promise => { kept.push(promise); } };
  return { event, answered: () => answered, settled: () => Promise.all(kept) };
}

// An answer as fetch gives one: whether it is ok, and a copy to keep while the page reads the original.
const answer = (body, status = 200, { redirected = false } = {}) => ({ body, status, redirected, ok: status >= 200 && status < 300, clone() { return { ...this, copy: true }; } });

// What an answer says: its text when it is a real one, the fake's body otherwise.
const bodyOf = async answered => { const given = await answered; return given instanceof Response ? given.text() : given; };

// The Cache Storage a worker sees: one store per name, each a map from a path to the answer kept for it.
function offlineWorker({ network, cached = 'offline page', kept = {} }) {
  const deleted = [];
  // A kept copy is an answer, as the Cache Storage gives one, read afresh each time it is matched.
  const store = new Map(Object.entries({ '/offline.html': cached, ...Object.fromEntries(Object.entries(kept).map(([path, body]) => [path, typeof body === 'string' ? new Response(body) : body])) }));
  const match = async path => { const found = store.get(path); return found instanceof Response ? found.clone() : found; };
  const caches = {
    match,
    open: async () => ({
      add: async path => { store.set(path, `installed ${path}`); },
      put: async (path, response) => { store.set(path, response); },
      match,
      keys: async () => [...store.keys()].map(path => ({ url: `https://table.example${path}` })),
      delete: async request => store.delete(new URL(request.url).pathname),
    }),
    keys: async () => ['downfall-offline-v0', 'downfall-offline-v1', 'downfall-offline-v2'],
    delete: async name => { deleted.push(name); return true; },
  };
  const handlers = {};
  const self = { addEventListener: (kind, handler) => { handlers[kind] = handler; }, skipWaiting: async () => {},
    location: new URL('https://table.example/sw.js'),
    clients: { claim: async () => {} }, registration: { navigationPreload: { enable: async () => {} } } };
  const context = vm.createContext({ self, URL, caches, fetch: network, Headers, Response });
  vm.runInContext(readFileSync(new URL('./sw.js', import.meta.url), 'utf8'), context);
  return { handlers, deleted, store, context };
}

async function install(w) {
  let waited;
  w.handlers.install({ waitUntil: promise => { waited = promise; } });
  await waited;
}

test('a page that loads is the network\'s answer, untouched', async () => {
  const w = offlineWorker({ network: async () => 'the page' });
  const f = fetched('navigate');

  w.handlers.fetch(f.event);

  assert.equal(await f.answered(), 'the page');
});

test('a page the browser preloaded while the worker woke is that preload', async () => {
  const w = offlineWorker({ network: async () => assert.fail('asked twice') });
  const f = fetched('navigate', { preloadResponse: Promise.resolve('preloaded') });

  w.handlers.fetch(f.event);

  assert.equal(await f.answered(), 'preloaded');
});

test('a page that cannot be loaded at all is the offline page', async () => {
  const w = offlineWorker({ network: async () => { throw new TypeError('offline'); } });
  const f = fetched('navigate');

  w.handlers.fetch(f.event);

  assert.equal(await f.answered(), 'offline page');
});

test('nothing but a navigation and the rulebook is touched: scripts, the API and the art go to the network as before', () => {
  const w = offlineWorker({ network: async () => assert.fail('not the worker\'s') });
  for (const mode of ['cors', 'no-cors', 'same-origin']) {
    for (const url of ['https://table.example/battle.js', 'https://table.example/api/lobby', 'https://table.example/battle-art/brute.webp']) {
      const f = fetched(mode, { url });
      w.handlers.fetch(f.event);
      assert.equal(f.answered(), null, `${mode} ${url} is not answered by the worker`);
    }
  }
});

test('installing keeps the offline page and the rulebook: the page, its script, its style and the book in both languages', async () => {
  const w = offlineWorker({ network: async path => answer(`installed ${path}`) });

  await install(w);

  for (const path of ['/offline.html', '/rules', '/rules.js', '/rules.css', '/rules.json', '/rules.fr.json', '/identity.css', '/motion.js', '/brand-mark.svg']) {
    assert.ok(w.store.has(path), `${path} is kept`);
  }
});

// page-art.js as scripts/ux/pictures.mjs writes it, holding the offline plate's entry among others.
const pageArtNaming = (...files) => [
  "export const PAGE_ART = {",
  "  'parasite-under-slate': {",
  "    from: 'a', frames: 1, drawn: [323], pages: {\"showcase/mutations\":\"lazy\"},",
  "    sizes: [\n      { file: 'parasite-under-slate-323.aaaaaaaa.webp', width: 323, height: 182, bytes: 1 },\n    ],",
  "  },",
  "  'parasite-offline': {",
  "    from: 'b', frames: 1, drawn: [358], pages: {\"table/offline\":\"largest\"},",
  `    sizes: [\n${files.map(file => `      { file: '${file}', width: 358, height: 201, bytes: 1 },`).join('\n')}\n    ],`,
  "  },",
  "};",
].join('\n');

test('the offline plate\'s files are the ones page-art.js names for it, and no other picture\'s', () => {
  const w = offlineWorker({ network: async () => 'x' });
  const real = readFileSync(new URL('./page-art.js', import.meta.url), 'utf8');
  const files = [...w.context.pictureFiles(real)].filter(file => file.startsWith('/page-art/parasite-offline-'));
  assert.ok(files.length > 0, 'the real page-art.js declares the plate');
  for (const file of files) {
    assert.match(file, /^\/page-art\/parasite-offline-\d+\.[0-9a-f]{8}\.webp$/);
    assert.ok(real.includes(`file: '${file.slice('/page-art/'.length)}'`));
  }
  assert.deepEqual([...w.context.pictureFiles(pageArtNaming('parasite-offline-358.11111111.webp'))], ['/page-art/parasite-offline-358.11111111.webp']);
});

test('installing keeps the offline page\'s script, the picture kit and the plate page-art.js names', async () => {
  const pageArt = pageArtNaming('parasite-offline-358.11111111.webp', 'parasite-offline-716.22222222.webp');
  const w = offlineWorker({ network: async path => ({ ...answer(`installed ${path}`), text: async () => (path === '/page-art.js' ? pageArt : '') }) });

  await install(w);

  for (const path of ['/offline.js', '/picture.js', '/picture-form.js', '/picture-focus.js', '/page-art.js', '/battle-page-art.js', '/page-art/parasite-offline-358.11111111.webp', '/page-art/parasite-offline-716.22222222.webp']) {
    assert.ok(w.store.has(path), `${path} is kept`);
  }
  assert.ok(!w.store.has('/page-art/parasite-under-slate-323.aaaaaaaa.webp'), 'only the plate is kept, not every picture');
});

// The modules a script reaches by its static imports, its own path first, each from the root as the host serves it.
function importsOf(path, seen = new Set()) {
  if (seen.has(path)) return seen;
  seen.add(path);
  const text = readFileSync(new URL(`.${path}`, import.meta.url), 'utf8');
  for (const [, from] of text.matchAll(/^\s*(?:import|export)\s(?:[^'";]*?\sfrom\s)?['"](\.\/[^'"]+)['"]/gm)) importsOf(`/${from.slice(2)}`, seen);
  return seen;
}

test('the offline page\'s scripts are every module offline.js reaches by its imports (#1136)', () => {
  const w = offlineWorker({ network: async () => 'x' });
  const kept = vm.runInContext('OFFLINE_SCRIPTS', w.context);
  const reached = [...importsOf('/offline.js')];
  assert.ok(reached.includes('/picture-form.js') && reached.includes('/battle-page-art.js'), `the walk follows the kit: ${reached}`);
  assert.deepEqual(reached.filter(path => !kept.includes(path)), [], 'a module offline.js imports is kept for the offline page');
});

test('installing keeps the rulebook\'s head painting at the sizes a phone asks for, and no other of its files (#1060)', async () => {
  const real = readFileSync(new URL('./page-art.js', import.meta.url), 'utf8');
  const w = offlineWorker({ network: async path => ({ ...answer(`installed ${path}`), text: async () => (path === '/page-art.js' ? real : '') }) });

  await install(w);

  const head = [...w.store.keys()].filter(path => path.startsWith('/page-art/latch-under-the-arch-'));
  assert.deepEqual(head.map(path => Number(path.match(/-(\d+)\./)[1])).sort((a, b) => a - b), [354, 708]);
  for (const path of head) assert.ok(real.includes(`file: '${path.slice('/page-art/'.length)}'`));
});

test('the rulebook\'s head painting the network cannot load is its kept copy (#1060)', async () => {
  const w = offlineWorker({ network: async () => { throw new TypeError('offline'); }, kept: { '/page-art/latch-under-the-arch-354.11111111.webp': 'the head' } });
  const f = fetched('no-cors', { url: 'https://table.example/page-art/latch-under-the-arch-354.11111111.webp' });

  w.handlers.fetch(f.event);

  assert.equal(await bodyOf(f.answered()), 'the head');
});

test('a painting the worker keeps is not kept again when a page loads it: page-art.js names what is kept', async () => {
  const w = offlineWorker({ network: async path => answer(`fresh ${path}`) });
  const f = fetched('no-cors', { url: 'https://table.example/page-art/latch-under-the-arch-1280.11111111.webp' });

  w.handlers.fetch(f.event);
  await f.answered();
  await f.settled();

  assert.ok(!w.store.has('/page-art/latch-under-the-arch-1280.11111111.webp'));
});

test('a fresh page-art.js refreshes the kept offline.js with it, so the offline page never meets a newer kit with an older script (#1061)', async () => {
  const w = offlineWorker({
    network: async path => ({ ...answer(`fresh ${path}`), text: async () => pageArtNaming('parasite-offline-358.11111111.webp') }),
    kept: { '/offline.js': 'old offline.js', '/page-art.js': 'old' },
  });
  const f = fetched('cors', { url: 'https://table.example/page-art.js' });

  w.handlers.fetch(f.event);
  await f.answered();
  await f.settled();

  assert.equal(w.store.get('/offline.js').body, 'fresh /offline.js');
});

test('a page-art.js the same as the kept one asks nothing more of the network: offline.js is already its deploy\'s', async () => {
  const pageArt = pageArtNaming('parasite-offline-358.11111111.webp');
  const asked = [];
  const w = offlineWorker({
    network: async path => { asked.push(path); return { ...answer(`fresh ${path}`), text: async () => pageArt }; },
    kept: { '/offline.js': 'kept offline.js', '/page-art.js': pageArt, '/page-art/parasite-offline-358.11111111.webp': 'the plate' },
  });
  const f = fetched('cors', { url: 'https://table.example/page-art.js' });

  w.handlers.fetch(f.event);
  await f.answered();
  await f.settled();

  assert.deepEqual(asked.map(request => request.url ?? request), ['https://table.example/page-art.js']);
  assert.equal(await w.store.get('/offline.js').text(), 'kept offline.js');
});

test('installing asks for offline.js once', async () => {
  const asked = [];
  const w = offlineWorker({ network: async path => { asked.push(path); return { ...answer(`installed ${path}`), text: async () => pageArtNaming('parasite-offline-358.11111111.webp') }; }, kept: { '/page-art.js': 'old' } });

  await install(w);

  assert.equal(asked.filter(path => path === '/offline.js').length, 1);
});

test('a fresh page-art.js keeps the plate it names and lets go of the older derivation\'s', async () => {
  const pageArt = pageArtNaming('parasite-offline-358.33333333.webp');
  const w = offlineWorker({
    network: async path => ({ ...answer(`fresh ${path}`), text: async () => pageArt }),
    kept: { '/page-art.js': 'old', '/page-art/parasite-offline-358.11111111.webp': 'old plate' },
  });
  const f = fetched('cors', { url: 'https://table.example/page-art.js' });

  w.handlers.fetch(f.event);
  await f.answered();
  await f.settled();

  assert.ok(w.store.has('/page-art/parasite-offline-358.33333333.webp'));
  assert.ok(!w.store.has('/page-art/parasite-offline-358.11111111.webp'));
});

test('the plate the network cannot load is its kept copy; another picture is the network\'s alone', async () => {
  const w = offlineWorker({ network: async () => { throw new TypeError('offline'); }, kept: { '/page-art/parasite-offline-358.11111111.webp': 'the plate' } });
  const plate = fetched('no-cors', { url: 'https://table.example/page-art/parasite-offline-358.11111111.webp' });
  w.handlers.fetch(plate.event);
  assert.equal(await bodyOf(plate.answered()), 'the plate');

  const other = fetched('no-cors', { url: 'https://table.example/page-art/last-one-standing-354.11111111.webp' });
  w.handlers.fetch(other.event);
  assert.equal(other.answered(), null);
});

test('a rulebook file the host cannot serve while the worker installs leaves the worker installed', async () => {
  const w = offlineWorker({ network: async path => { if (path === '/rules.json') throw new TypeError('refused'); return answer(path); } });

  await install(w);

  assert.ok(w.store.has('/rules'));
  assert.ok(!w.store.has('/rules.json'));
});

test('the rulebook that loads is the network\'s answer, and its kept copy is refreshed with it', async () => {
  const fresh = answer('today\'s rulebook');
  const w = offlineWorker({ network: async () => fresh, kept: { '/rules': 'yesterday\'s rulebook' } });
  const f = fetched('navigate', { url: 'https://table.example/rules' });

  w.handlers.fetch(f.event);

  assert.equal(await f.answered(), fresh);
  await f.settled();
  assert.equal(w.store.get('/rules').body, 'today\'s rulebook');
});

test('the rulebook the network cannot load is the kept copy, at any of its deep links, not the offline page', async () => {
  const w = offlineWorker({ network: async () => { throw new TypeError('offline'); }, kept: { '/rules': 'the kept rulebook' } });

  for (const url of ['https://table.example/rules', 'https://table.example/rules?from=battle']) {
    const f = fetched('navigate', { url });
    w.handlers.fetch(f.event);
    assert.equal(await bodyOf(f.answered()), 'the kept rulebook', url);
  }
});

test('the rulebook asked in a language is still the kept copy offline', async () => {
  const w = offlineWorker({ network: async () => { throw new TypeError('offline'); }, kept: { '/rules': 'the kept rulebook' } });
  const f = fetched('navigate', { url: 'https://table.example/rules?lang=fr' });

  w.handlers.fetch(f.event);

  assert.equal(await bodyOf(f.answered()), 'the kept rulebook');
});

// A kept copy as the Cache Storage gives one: a real answer, so the worker can say it is a copy.
const keptAnswer = body => new Response(body, { headers: { 'Content-Type': 'application/json' } });

test('the book given from its kept copy says so in a header, so the page can tell the player it may be older', async () => {
  for (const network of [async () => { throw new TypeError('offline'); }, async () => answer('origin unreachable', 522)]) {
    const w = offlineWorker({ network, kept: { '/rules.json': keptAnswer('{"kept":true}') } });
    const f = fetched('cors', { url: 'https://table.example/rules.json' });

    w.handlers.fetch(f.event);

    const given = await f.answered();
    assert.equal(given.headers.get('X-Rulebook-Copy'), 'kept');
    assert.equal(given.headers.get('Content-Type'), 'application/json');
    assert.equal(await given.text(), '{"kept":true}');
  }
});

test('the book the network answers carries no mark of a copy', async () => {
  const fresh = new Response('{"fresh":true}');
  const w = offlineWorker({ network: async () => fresh, kept: { '/rules.json': keptAnswer('{}') } });
  const f = fetched('cors', { url: 'https://table.example/rules.json' });

  w.handlers.fetch(f.event);

  assert.equal((await f.answered()).headers.get('X-Rulebook-Copy'), null);
});

test('the rulebook never kept and not reachable is the offline page', async () => {
  const w = offlineWorker({ network: async () => { throw new TypeError('offline'); } });
  const f = fetched('navigate', { url: 'https://table.example/rules' });

  w.handlers.fetch(f.event);

  assert.equal(await f.answered(), 'offline page');
});

test('the book and the page\'s files go to the network first, their copies refreshed, and are the copies offline', async () => {
  for (const path of ['/rules.json', '/rules.js', '/rules.css', '/identity.css']) {
    const online = offlineWorker({ network: async () => answer(`fresh ${path}`), kept: { [path]: `old ${path}` } });
    const asked = fetched('cors', { url: `https://table.example${path}` });
    online.handlers.fetch(asked.event);
    assert.equal((await asked.answered()).body, `fresh ${path}`);
    await asked.settled();
    assert.equal(online.store.get(path).body, `fresh ${path}`);

    const offline = offlineWorker({ network: async () => { throw new TypeError('offline'); }, kept: { [path]: `old ${path}` } });
    const missed = fetched('cors', { url: `https://table.example${path}` });
    offline.handlers.fetch(missed.event);
    assert.equal(await bodyOf(missed.answered()), `old ${path}`);
  }
});

test('a redirected answer is passed on and never kept: a browser refuses it as the answer to a navigation', async () => {
  const moved = answer('the rulebook, elsewhere', 200, { redirected: true });
  const w = offlineWorker({ network: async () => moved, kept: { '/rules': 'the kept rulebook' } });
  const f = fetched('navigate', { url: 'https://table.example/rules' });

  w.handlers.fetch(f.event);

  assert.equal(await f.answered(), moved);
  await f.settled();
  assert.equal(await w.store.get('/rules').text(), 'the kept rulebook');
});

test('the rulebook whose host answers with a server error, as Cloudflare does for a host that is down, is the kept copy', async () => {
  for (const [mode, path] of [['navigate', '/rules'], ['cors', '/rules.json']]) {
    const w = offlineWorker({ network: async () => answer('origin unreachable', 522), kept: { [path]: `the kept ${path}` } });
    const f = fetched(mode, { url: `https://table.example${path}` });

    w.handlers.fetch(f.event);

    assert.equal(await bodyOf(f.answered()), `the kept ${path}`, path);
  }
});

test('a server error for a style every page shares is the host\'s answer: no page is drawn with an older deploy\'s style', async () => {
  for (const path of ['/table.css', '/lobby.css', '/identity.css', '/brand-mark.svg']) {
    const down = answer('origin unreachable', 502);
    const w = offlineWorker({ network: async () => down, kept: { [path]: `the kept ${path}` } });
    const f = fetched('cors', { url: `https://table.example${path}` });

    w.handlers.fetch(f.event);

    assert.equal(await f.answered(), down, path);
  }
});

test('a server error for the rulebook with no copy kept is the host\'s answer, as it was', async () => {
  const down = answer('origin unreachable', 522);
  const w = offlineWorker({ network: async () => down });
  const f = fetched('navigate', { url: 'https://table.example/rules' });

  w.handlers.fetch(f.event);

  assert.equal(await f.answered(), down);
});

test('an answer that is not a success is passed on and never kept', async () => {
  const failed = answer('gone', 404);
  const w = offlineWorker({ network: async () => failed, kept: { '/rules.json': 'the kept book' } });
  const f = fetched('cors', { url: 'https://table.example/rules.json' });

  w.handlers.fetch(f.event);

  assert.equal(await f.answered(), failed);
  await f.settled();
  assert.equal(await w.store.get('/rules.json').text(), 'the kept book');
});

test('a rulebook\'s path on another site, or asked with another method, is not the worker\'s', () => {
  const w = offlineWorker({ network: async () => assert.fail('not the worker\'s') });
  for (const f of [fetched('cors', { url: 'https://elsewhere.example/rules.json' }), fetched('cors', { url: 'https://table.example/rules.json', method: 'POST' })]) {
    w.handlers.fetch(f.event);
    assert.equal(f.answered(), null);
  }
});

test('activating keeps only the worker\'s own cache', async () => {
  const w = offlineWorker({ network: async () => 'x' });
  let waited;

  w.handlers.activate({ waitUntil: promise => { waited = promise; } });
  await waited;

  assert.deepEqual(w.deleted, ['downfall-offline-v0', 'downfall-offline-v1']);
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

// #1136: OFFLINE_SCRIPTS is written by hand; a module offline.js reaches by import and the list leaves out would
// leave the offline page without its plate, the network gone.
test('installing keeps every module the offline page\'s script reaches by its imports', async () => {
  const w = offlineWorker({ network: async path => ({ ...answer(`installed ${path}`), text: async () => '' }) });

  await install(w);

  const reached = reachedModules(fileURLToPath(new URL('.', import.meta.url)), ['offline.js']);
  assert.ok(reached.has('/picture-focus.js'), 'the walk follows picture.js down to picture-focus.js');
  for (const path of reached) assert.ok(w.store.has(path), `${path} is kept`);
});
