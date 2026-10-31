// The table's service worker (ADR 0117): it shows the "your turn" the host pushes, and opens the game when
// the notice is tapped. It caches nothing and serves nothing -- a deploy must reach every page on its next
// load, and a worker holding yesterday's page would stop that.

const ICON = '/icon-192.png';

// A notice is shown unless a window of this table is in front of somebody: a page focused on the question
// is the notice, and the same words twice would be noise.
async function onPush(notice) {
  const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
  if (windows.some(client => client.focused)) return;
  await self.registration.showNotification(notice.title ?? 'Your turn', {
    body: notice.body ?? 'The match is asking you something.',
    tag: notice.tag ?? 'turn',
    icon: ICON,
    badge: ICON,
    renotify: true,
    data: { url: notice.url ?? '/play' },
  });
}

// A tap goes to the game: the page already open on it when there is one, a new one otherwise.
async function onTap(url) {
  const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
  const open = windows.find(client => new URL(client.url).pathname === url) ?? windows[0];
  if (open) {
    await open.focus();
    if (new URL(open.url).pathname !== url && open.navigate) await open.navigate(url);
    return;
  }
  await self.clients.openWindow(url);
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
  event.waitUntil(onTap(event.notification.data?.url ?? '/play'));
});
