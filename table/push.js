// "Your turn" on a phone nobody is looking at (ADR 0118).
//
// Two things notify, and they do not overlap. The host sends a push to a seat whose page did not say it drew
// the question within two seconds: a page asleep, closed, or on another app. The service worker (sw.js) shows
// it, unless a window of this page is focused. And a page that is still running but hidden -- another tab,
// the screen off with the browser in front -- hears nothing from the host, since its polls keep acknowledging,
// so it shows the notice itself. Everything here is the pure part: what to show, what the one button says,
// and what a key is to the browser. The page does the asking.

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
// that cannot push, a host without keys, a practice table. Disabled, with the way out, when the person said no.
export function noticeButton({ supported, keyed, permission, subscribed, busy, practice }) {
  if (!supported || !keyed || practice) return { hidden: true, disabled: true, label: '', hint: '' };
  if (permission === 'denied') {
    return { hidden: false, disabled: true, label: 'Notifications are blocked', hint: 'Allow them for this site in the browser\'s settings to be told when it is your turn.' };
  }
  if (subscribed) return { hidden: false, disabled: Boolean(busy), label: 'Stop notifying me', hint: 'This phone is told when it is your turn.' };
  return { hidden: false, disabled: Boolean(busy), label: 'Notify me when it\'s my turn', hint: 'Even with the screen off or the browser closed.' };
}

// The notice a hidden page shows itself: the same words the host sends (PushNotices.Payload).
export function turnNotice(view, seat) {
  const what = {
    Evolution: 'pick your packages', Speed: 'choose a speed', TieOrder: 'order the tie',
    Intent: 'declare a spell', Target: 'choose your targets', Action: 'choose a spell',
  }[view?.waitingFor] ?? 'decide';
  const round = view?.board?.roundNumber;
  return {
    title: 'Your turn',
    body: Number.isInteger(round) ? `Round ${round}: ${what}.` : `${NAMES[seat] ?? seat}: ${what}.`,
    tag: `${TAG}:${seat}`,
  };
}

// Whether a page should show the notice itself: a new asking, while it is hidden, with leave to notify and the
// notices on. A visible page is the notice, a page that was already told of this asking says nothing twice,
// and a player who tapped "Stop notifying me" hears nothing from the page either, whatever the browser allows.
export function shouldNotifyInPage({ asked, previous, hidden, permission, enabled }) {
  return asked !== null && asked !== undefined && asked !== previous && Boolean(hidden) && permission === 'granted' && Boolean(enabled);
}

// Where a tap on a notice goes: the game, at the table the notice came from. A browser may hold several
// tables (ADR 0115), and the question is at this one.
export function noticeUrl(table) {
  return table ? `/play?table=${encodeURIComponent(table)}` : '/play';
}
