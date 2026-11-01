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
// and on an iPhone, which pushes only to a page added to the home screen.
export function noticeButton({ supported, keyed, permission, subscribed, busy, practice, ios }) {
  if (!keyed || practice) return { hidden: true, disabled: true, label: '', hint: '' };
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
