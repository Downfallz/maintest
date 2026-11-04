// The table's service worker (ADR 0118): it shows the "your turn" the host pushes, and opens the game when
// the notice is tapped. It caches nothing and serves nothing -- a deploy must reach every page on its next
// load, and a worker holding yesterday's page would stop that.

const ICON = '/icon-192.png';

const GAME = '/play';

const LOBBY = '/lobby';

// The page a URL is on, with the lobby's two addresses read as one: `/` without a query is the lobby.
function pageOf(url) {
  const path = new URL(url, 'https://downfall.invalid').pathname;
  return path === '/' ? LOBBY : path;
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
  // A test notice is the one a player asked to see, over the game or not.
  if (!notice.test && windows.some(client => client.focused && client.visibilityState === 'visible' && pageOf(client.url) === page)) return;
  await self.registration.showNotification(notice.title ?? 'Your turn', {
    body: notice.body ?? 'The match is asking you something.',
    tag: notice.tag ?? 'turn',
    icon: ICON,
    badge: ICON,
    renotify: true,
    data: { url: notice.url ?? '/play' },
  });
}

// A tap goes where the notice says: the game at the table it came from, or the lobby for a friend or an
// invitation. The page already open there when there is one, another page of the table sent there otherwise,
// unless it is one this worker does not control, which cannot be steered; a new window when there is none.
async function onTap(url) {
  const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
  const at = client => { const here = new URL(client.url); return `${here.pathname}${here.search}`; };
  const page = pageOf(url);
  const open = windows.find(client => at(client) === url) ?? windows.find(client => pageOf(client.url) === page) ?? windows[0];
  if (open) {
    await open.focus();
    if (at(open) === url) return;
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

self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('push', event => event.waitUntil(onPush(read(event))));
self.addEventListener('notificationclick', event => {
  event.notification.close();
  event.waitUntil(onTap(event.notification.data?.url ?? GAME));
});
self.addEventListener('pushsubscriptionchange', event => event.waitUntil(onSubscriptionChange(event)));
