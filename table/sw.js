// The table's service worker (ADR 0118): it shows the "your turn" the host pushes, and opens the game when
// the notice is tapped. It serves no page of the table from a cache while the network answers -- a deploy must reach
// every page on its next load, and a worker holding yesterday's page would stop that. What it keeps is the offline
// page (ADR 0166), shown in place of the browser's own error when a page cannot be loaded at all, with what draws its
// plate, and the rulebook (ADR 0191), given only when the network cannot answer.

const OFFLINE = '/offline.html';
const CACHE = 'downfall-offline-v2';

// The rulebook's page and everything it loads besides the menu, which it draws without: kept at install and refreshed
// each time the network answers, so the rules a player last read can be read again with no network. Only a path whose
// answer is the same for everyone may join: what is kept stays after a sign-out.
const RULEBOOK = '/rules';
const RULEBOOK_OWN = new Set([RULEBOOK, '/rules.js', '/rules.css', '/rules.json', '/rules.fr.json']);
// What it shares with every page: given only when the network cannot answer at all, never in place of a server error,
// so that no other page is drawn with an older deploy's style.
// The motion kit's module is one of them (ADR 0197): without it the account's less motion would not reach the book.
const RULEBOOK_SHARED = ['/table.css', '/lobby.css', '/identity.css', '/motion.js', '/brand-mark.svg', '/fonts/marcellus-latin-400.woff2'];
// What the offline page draws its plate with (ADR 0200): its script, the picture kit and the sizes it derived, and the
// plate's own files, which page-art.js names. Given only when the network cannot answer at all. The kit is refreshed
// each time a page loads it; offline.js only when the worker installs again, since no page loads it online.
const OFFLINE_SCRIPTS = ['/offline.js', '/picture.js', '/picture-form.js', '/picture-focus.js', '/page-art.js', '/battle-page-art.js'];
const PAGE_ART = '/page-art.js';
const PLATE = 'parasite-offline';
const KEPT_FILES = new Set([...RULEBOOK_OWN, ...RULEBOOK_SHARED, ...OFFLINE_SCRIPTS]);

// The plate's files, as page-art.js names them (scripts/ux/pictures.mjs writes one entry a picture, its sizes listed
// under it): a worker cannot import a module, so it reads the text. A test reads the real file with it.
function plateFiles(pageArt) {
  const entry = pageArt.match(new RegExp(`'${PLATE}': \\{[\\s\\S]*?sizes: \\[([^\\]]*)\\]`));
  return entry ? [...entry[1].matchAll(/file: '([^']+)'/g)].map(found => `/page-art/${found[1]}`) : [];
}
const isPlateFile = path => path.startsWith(`/page-art/${PLATE}-`);

const ICON = '/icon-192.png';
const BADGE = '/notification-badge.png';

const GAME = '/play';

// An already-open tab may still use the previous route until its next navigation.
const battlePath = path => path === '/battle' || path === '/arena';

const LOBBY = '/lobby';

// The page a URL is on, with the lobby's two addresses read as one (`/` without a query is the lobby) and the
// battle read as the game: a seat played there is the same seat, asked the same thing (ADR 0126), so a battle
// in front of somebody is the "your turn" as much as the table page is. The battle lab is a study, not a seat.
function pageOf(url) {
  const path = new URL(url, 'https://downfall.invalid').pathname;
  if (path === '/') return LOBBY;
  return battlePath(path) ? GAME : path;
}

const isBattle = client => battlePath(new URL(client.url).pathname);

// Whether a battle page is at the table a "your turn" came from. The battle names its table in its address
// (`?table=`) when it was chosen from the lobby's list; opened from a seat's own link it names the seat's token
// instead, which the worker cannot read a table off, and the one table a device usually holds is taken to be
// it. Only two tables both named, and named differently, is another table.
function sameTable(client, url) {
  const asked = new URL(url, 'https://downfall.invalid').searchParams.get('table');
  const held = new URL(client.url).searchParams.get('table');
  return !asked || !held || asked === held;
}

// A notice is shown unless the page it opens is in front of somebody: a game page focused on the question is
// the "your turn", a lobby in front of somebody is the invitation, and the same words twice would be noise. In
// front of somebody means visible as well as focused: a phone whose screen went off keeps its page focused for
// a while, and nobody is looking at that one. The lobby is not the game: a player parked on it is told of a
// turn, and a player at the game is told of a friend.
async function onPush(notice) {
  const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
  const page = pageOf(notice.url ?? GAME);
  // Every page is told something arrived, so a lobby in front of somebody reads its lists again now rather
  // than at its next refresh; the notice itself is then shown or not.
  for (const client of windows) client.postMessage?.({ kind: 'notice', url: notice.url ?? GAME });
  // A test notice is the one a player asked to see, over the game or not. A game in front of somebody at another
  // table is not this one's: a browser may hold several (ADR 0115), and a turn there stays a turn to be told of.
  const url = notice.url ?? GAME;
  const inFront = client => client.focused && client.visibilityState === 'visible' && pageOf(client.url) === page
    && (page !== GAME || sameTable(client, url));
  if (!notice.test && windows.some(inFront)) return;
  await self.registration.showNotification(notice.title ?? 'Your turn', {
    body: notice.body ?? 'The match is asking you something.',
    tag: notice.tag ?? 'turn',
    icon: ICON,
    badge: BADGE,
    renotify: true,
    data: { url: notice.url ?? '/play' },
  });
}

// A tap goes where the notice says: the game at the table it came from, or the lobby for a friend or an
// invitation. The page already open there when there is one, another page of the table sent there otherwise,
// unless it is one this worker does not control, which cannot be steered; a new window when there is none.
// An battle page is the view of the game its player chose: for a "your turn" at its table it is brought to the
// front and left in the battle, never sent to the table page, and for anything else it is left alone.
async function onTap(url) {
  const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
  const at = client => { const here = new URL(client.url); return `${here.pathname}${here.search}`; };
  const page = pageOf(url);
  const played = page === GAME ? windows.find(client => isBattle(client) && sameTable(client, url)) : undefined;
  const open = windows.find(client => at(client) === url)
    ?? windows.find(client => !isBattle(client) && pageOf(client.url) === page)
    ?? played
    ?? windows.find(client => !isBattle(client));
  if (open) {
    await open.focus();
    if (at(open) === url || isBattle(open)) return;
    try {
      if (open.navigate) {
        await open.navigate(url);
        return;
      }
    } catch {
      // Not this worker's to steer: a new window is.
    }
  }
  await self.clients.openWindow(url);
}

// The browser rotated the subscription, or let it go: made again with the same key, and every page told, so
// one that is open sends the new one to the host. With none open, the host hears of it at the next page load.
async function onSubscriptionChange(event) {
  const key = event.oldSubscription?.options?.applicationServerKey;
  if (!key) return;
  await self.registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key });
  const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
  for (const client of windows) client.postMessage({ kind: 'subscription-changed' });
}

function read(event) {
  try {
    return event.data?.json() ?? {};
  } catch {
    return {};
  }
}

// The path a request is kept under when it is one the worker keeps, or null: same site, read, and named in the list
// or one of the offline plate's files.
function keptPath(request) {
  if (request.method && request.method !== 'GET') return null;
  const url = new URL(request.url);
  return url.origin === self.location.origin && (KEPT_FILES.has(url.pathname) || isPlateFile(url.pathname)) ? url.pathname : null;
}

// A successful answer is kept as the copy of its path; anything else is the network's to show, and never kept. A
// redirected one is not a success here: a browser refuses it as the answer to a navigation.
const keepable = response => Boolean(response?.ok) && !response.redirected;

// A host that is down behind Cloudflare is an answer, a 52x, not a network error: for the rulebook it counts as no
// answer at all, and the copy is given when there is one.
const unanswered = response => (response?.status ?? 0) >= 500;

// The copy kept of a path, marked as one (ADR 0191, #698): the rulebook's page reads the mark and tells the player the
// book may be older than the one the table now plays. Undefined when nothing is kept.
async function copyOf(path) {
  const copy = await caches.match(path);
  if (!copy) return undefined;
  const headers = new Headers(copy.headers);
  headers.set('X-Rulebook-Copy', 'kept');
  return new Response(copy.body, { status: copy.status, statusText: copy.statusText, headers });
}

async function keep(path, response) {
  if (!keepable(response)) return;
  try {
    const cache = await caches.open(CACHE);
    // The sizes are read before the copy is put: a response's body is read once.
    const plate = path === PAGE_ART ? plateFiles(await response.clone().text()) : null;
    await cache.put(path, response);
    if (plate) await keepPlate(cache, plate);
  } catch {
    // A full or refused cache keeps the copy it had: the page itself was answered.
  }
}

// The plate's files a fresh page-art.js names, kept when they are not yet, and the files of an older derivation let
// go, so the copy of page-art.js and the plate it names stay one.
async function keepPlate(cache, files) {
  if (files.length === 0) return;
  const named = new Set(files);
  for (const request of await cache.keys()) {
    const path = new URL(request.url ?? request, self.location.origin).pathname;
    if (isPlateFile(path) && !named.has(path)) await cache.delete(request);
  }
  await Promise.allSettled(files.map(async path => { if (!(await cache.match(path))) { const response = await fetch(path); if (keepable(response)) await cache.put(path, response); } }));
}

// A navigation goes to the network as it always did -- through the preload the browser started while the worker woke,
// when there is one -- and only a navigation that could not be answered at all is shown a kept page: the rulebook's
// copy for the rulebook, the offline page for anything else.
async function navigate(event) {
  const path = keptPath(event.request);
  try {
    const response = (await event.preloadResponse) ?? await fetch(event.request);
    if (path && keepable(response)) event.waitUntil(keep(path, response.clone()));
    if (path && unanswered(response)) return (await copyOf(path)) ?? response;
    return response;
  } catch {
    return (path && await copyOf(path)) ?? (await caches.match(OFFLINE)) ?? Response.error();
  }
}

// One of the files kept: the network's answer, its copy refreshed, or the copy when the network cannot answer.
async function keptFile(event, path) {
  try {
    const response = await fetch(event.request);
    if (keepable(response)) event.waitUntil(keep(path, response.clone()));
    if (unanswered(response) && RULEBOOK_OWN.has(path)) return (await copyOf(path)) ?? response;
    return response;
  } catch {
    return (await copyOf(path)) ?? Response.error();
  }
}

// The offline page must be kept, or the worker does not install; the rulebook and the offline plate are kept as well
// as they can be, so a host that cannot serve one of their files still has a worker that shows its notices. The plate's
// files are kept with page-art.js, which names them.
async function install() {
  const cache = await caches.open(CACHE);
  await cache.add(OFFLINE);
  await Promise.allSettled([...KEPT_FILES].map(async path => keep(path, await fetch(path))));
  await self.skipWaiting();
}

// One cache name: an older name is dropped, so nothing else is ever kept.
async function activate() {
  for (const name of await caches.keys()) if (name !== CACHE) await caches.delete(name);
  await self.registration.navigationPreload?.enable();
  await self.clients.claim();
}

self.addEventListener('install', event => event.waitUntil(install()));
self.addEventListener('activate', event => event.waitUntil(activate()));
self.addEventListener('fetch', event => {
  if (event.request.mode === 'navigate') {
    event.respondWith(navigate(event));
    return;
  }
  const path = keptPath(event.request);
  if (path) event.respondWith(keptFile(event, path));
});
self.addEventListener('push', event => event.waitUntil(onPush(read(event))));
self.addEventListener('notificationclick', event => {
  event.notification.close();
  event.waitUntil(onTap(event.notification.data?.url ?? GAME));
});
self.addEventListener('pushsubscriptionchange', event => event.waitUntil(onSubscriptionChange(event)));
