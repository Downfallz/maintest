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

// What the one button says, from where the page stands, and whether it is offered outside the match panel
// (`offered`: the waiting room, and the one-time offer every seat page makes on entering -- the second player
// to arrive never sees a waiting room). Hidden only when there is nothing to say: a host without keys, a
// practice table. Everywhere else the match panel says something, because a notice that silently is not there
// reads as one that is broken: disabled, with the way out, when the person said no, on an iPhone, which pushes
// only to a page added to the home screen, and in a browser that cannot take a push at all, an app's built-in
// browser that a chat opened the invitation in most often. A device holding both seats is told nothing, since
// nobody is away from it (ADR 0120): the panel says so, and nothing is offered.
export function noticeButton({ supported, keyed, permission, subscribed, busy, practice, ios, hotseat, secure }) {
  if (!keyed || practice) return { hidden: true, disabled: true, offered: false, label: '', hint: '' };
  if (hotseat) {
    return { hidden: false, disabled: true, offered: false, label: 'No notices with both seats here', hint: 'This device holds both seats, so whoever holds it is the other player: nobody is away to be told.' };
  }
  if (!supported && ios) {
    return { hidden: false, disabled: true, offered: true, label: 'Notifications need the home screen', hint: 'On iPhone: Share, Add to Home Screen, then open the table from there to be told when it is your turn.' };
  }
  if (!supported && secure === false) {
    return { hidden: false, disabled: true, offered: false, label: 'Notifications need https', hint: 'This table is served over plain http, where no browser takes a push: the hosted table, or this computer at localhost, can notify.' };
  }
  if (!supported) {
    return { hidden: false, disabled: true, offered: true, label: 'This browser cannot notify you', hint: 'Open the table in Chrome, Firefox or Safari to be told when it is your turn: the browser built into a chat or social app cannot show notifications.' };
  }
  if (permission === 'denied') {
    return { hidden: false, disabled: true, offered: true, label: 'Notifications are blocked', hint: 'Allow them for this site in the browser\'s settings to be told when it is your turn.' };
  }
  if (subscribed) return { hidden: false, disabled: Boolean(busy), offered: true, label: 'Stop notifying me', hint: 'This phone is told when it is your turn.' };
  return { hidden: false, disabled: Boolean(busy), offered: true, label: 'Notify me when it\'s my turn', hint: 'Even with the screen off or the browser closed.' };
}

// Whether the one-time offer is made: once a browser, to a seat page that could be told and is not yet, and
// never again once it was declined or accepted. A person who blocked notifications said no already.
export function offerShown(shown, { subscribed, permission } = {}, declined = false) {
  return Boolean(shown?.offered && !subscribed && permission !== 'denied' && !declined);
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
