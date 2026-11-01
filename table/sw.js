// The table's service worker (ADR 0118): it shows the "your turn" the host pushes, and opens the game when
// the notice is tapped. It caches nothing and serves nothing -- a deploy must reach every page on its next
// load, and a worker holding yesterday's page would stop that.

const ICON = '/icon-192.png';

const GAME = '/play';

// A notice is shown unless the game is in front of somebody: a page focused on the question is the notice,
// and the same words twice would be noise. In front of somebody means visible as well as focused: a phone
// whose screen went off keeps its page focused for a while, and nobody is looking at that one. The lobby is
// not the game: a player parked on it is told.
async function onPush(notice) {
  const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
  // A test notice is the one a player asked to see, over the game or not.
  if (!notice.test && windows.some(client => client.focused && client.visibilityState === 'visible' && new URL(client.url).pathname === GAME)) return;
  await self.registration.showNotification(notice.title ?? 'Your turn', {
    body: notice.body ?? 'The match is asking you something.',
    tag: notice.tag ?? 'turn',
    icon: ICON,
    badge: ICON,
    renotify: true,
    data: { url: notice.url ?? '/play' },
  });
}

// A tap goes to the game at the table the notice came from: the page already open on it when there is one, a
// new one otherwise. A page on the lobby, or on another table, is sent there, unless it is one this worker
// does not control, which cannot be steered.
async function onTap(url) {
  const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
  const at = client => { const here = new URL(client.url); return `${here.pathname}${here.search}`; };
  const open = windows.find(client => at(client) === url) ?? windows.find(client => new URL(client.url).pathname === GAME) ?? windows[0];
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
