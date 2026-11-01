// "Your turn" on a phone nobody is looking at (ADR 0118).
//
// The host sends a push to a seat whose page did not say it drew the question within two seconds: a page
// asleep, closed, on another app, or hidden -- a hidden page acknowledges nothing (table.js). The service
// worker (sw.js) shows it, unless the game is visible and focused. Everything here is the pure part: what the
// one button says, and what a key is to the browser. The page does the asking.

export const TAG = 'turn';

const NAMES = { player1: 'Player 1', player2: 'Player 2' };

// Whether this browser can take a push at all: a worker, a push manager, notifications, and a secure
// context, which http on a LAN address is not (the hosted table and localhost are).
export function pushSupported(nav = globalThis.navigator, win = globalThis) {
  return Boolean(nav && 'serviceWorker' in nav && win && 'PushManager' in win && 'Notification' in win && win.isSecureContext !== false);
}

// The host's public key as `applicationServerKey` wants it: the base64url text as bytes.
export function keyBytes(base64url) {
  const padded = `${base64url}${'='.repeat((4 - (base64url.length % 4)) % 4)}`.replace(/-/g, '+').replace(/_/g, '/');
  const raw = atob(padded);
  return Uint8Array.from(raw, character => character.charCodeAt(0));
}

// Whether a subscription this browser already holds was made with the host's current key: a host whose keys
// changed cannot send to one made with the old pair, and the page has to subscribe again.
export function sameKey(subscription, publicKey) {
  const held = subscription?.options?.applicationServerKey;
  if (!held || !publicKey) return false;
  const wanted = keyBytes(publicKey);
  const bytes = new Uint8Array(held);
  return bytes.length === wanted.length && bytes.every((byte, index) => byte === wanted[index]);
}

// What the one button says, from where the page stands. Hidden when there is nothing it could do: a browser
// that cannot push, a host without keys, a practice table. Disabled, with the way out, when the person said no,
// and on an iPhone, which pushes only to a page added to the home screen. Hidden too for a device nobody is away from:
// that is, not two seats of one table on one device, where the other player is whoever holds it (ADR 0120).
export function noticeButton({ supported, keyed, permission, subscribed, busy, practice, ios, hotseat }) {
  if (!keyed || practice || hotseat) return { hidden: true, disabled: true, label: '', hint: '' };
  if (!supported && ios) {
    return { hidden: false, disabled: true, label: 'Notifications need the home screen', hint: 'On iPhone: Share, Add to Home Screen, then open the table from there to be told when it is your turn.' };
  }
  if (!supported) return { hidden: true, disabled: true, label: '', hint: '' };
  if (permission === 'denied') {
    return { hidden: false, disabled: true, label: 'Notifications are blocked', hint: 'Allow them for this site in the browser\'s settings to be told when it is your turn.' };
  }
  if (subscribed) return { hidden: false, disabled: Boolean(busy), label: 'Stop notifying me', hint: 'This phone is told when it is your turn.' };
  return { hidden: false, disabled: Boolean(busy), label: 'Notify me when it\'s my turn', hint: 'Even with the screen off or the browser closed.' };
}

// What the two test notices say together, for the phone to read against what appeared. The page shows one
// itself, through the worker and no push service: whether this browser shows any for this site at all. The
// host sends the other through the push service: whether a push reaches this browser. Both, only the page's,
// or neither is the diagnosis, and the page cannot see which: the person can.
export function testReading({ page, host, hostOk }) {
  const lines = [page ? `The page could not show one itself: ${page}.` : 'The page showed one itself.', host];
  if (page && /dropped/.test(page)) lines.push('Look for it in the system\'s notification history, and at the browser\'s own settings for this site.');
  else if (page) lines.push('Allow notifications for this site in the browser\'s settings.');
  else if (hostOk) lines.push('Two notices, then. Both appeared: notices work. Only the page\'s: the push service is not reaching this browser; check the browser\'s own settings for this site, and a battery saver that puts it to sleep. Neither: the browser is not showing notifications for this site.');
  return lines.join(' ');
}
