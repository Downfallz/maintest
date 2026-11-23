// The offer of pop-ups (notifications plan, N4): one card of the page, shared by the lobby and the battle view, made
// at a moment the player has a reason to say yes -- a game against a person that waits on the other player, and the
// end of the first tutorial. It is never the browser's own prompt: a refusal there is final for the site, so the
// browser is asked only after a tap on Turn on. The answers are the browser's, kept beside its other choices,
// because the permission is the browser's too; Settings, Notifications keeps the switch whatever is answered here.
//
// The pure part first: where the browser stands, whether the card is due, and what it says. Then the card.

import { pushSupported } from './push.js';

/** Where this browser keeps its answers to the offer. */
export const OFFER_KEY = 'downfall.push-offer';
/** After Not now, the card waits a week at least before it asks again, and asks three times at most. */
export const WEEK = 7 * 24 * 60 * 60 * 1000;
export const MOST = 3;

/** What a browser that never answered remembers. */
const FRESH = Object.freeze({ never: false, asked: 0, last: null });

/** The answers this browser kept; a browser that keeps nothing, or kept something unreadable, remembers none. */
export function readMemory(storage) {
  try {
    const kept = JSON.parse(storage?.getItem(OFFER_KEY) ?? 'null');
    if (!kept || typeof kept !== 'object') return { ...FRESH };
    return { never: kept.never === true, asked: Number.isInteger(kept.asked) && kept.asked > 0 ? kept.asked : 0, last: Number.isFinite(kept.last) ? kept.last : null };
  } catch {
    return { ...FRESH };
  }
}

export function writeMemory(storage, memory) {
  try {
    storage?.setItem(OFFER_KEY, JSON.stringify(memory));
  } catch {
    // A browser that keeps nothing is asked again on its next visit, a week apart or not: it cannot be told apart.
  }
}

/** Not now, or Turn on answered without pop-ups: one asking more, from now. */
export function askedOnce(memory, now) {
  return { ...memory, asked: memory.asked + 1, last: now };
}

/** Don't ask again: the card is never made again in this browser. */
export function neverAgain(memory) {
  return { ...memory, never: true };
}

/**
 * The platform whose words the card speaks, or null when it has nothing to offer: a browser that cannot take a push
 * and cannot be made to, or plain http on a LAN address. An iPhone takes a push only from a page added to the home
 * screen: outside it, the card gives the steps to add it instead.
 */
export function offerPlatform(nav = globalThis.navigator, win = globalThis) {
  const agent = nav?.userAgent ?? '';
  const iphone = /iPhone|iPad|iPod/.test(agent);
  if (pushSupported(nav, win)) return iphone ? 'iphone' : /Android/.test(agent) ? 'android' : 'browser';
  if (!iphone || win?.isSecureContext === false) return null;
  const standalone = nav?.standalone === true || Boolean(win?.matchMedia?.('(display-mode: standalone)')?.matches);
  // At the home screen and still no push: an iOS too old to push, which no step changes.
  return standalone ? null : 'iphone-install';
}

/**
 * Whether the card is due at this moment: a host that sends pop-ups, a browser with something to offer, not on
 * already, not blocked (blocked is a no the player already said), not a device holding both seats (nobody is away
 * from it, ADR 0120), never told Don't ask again, asked fewer than three times, and a week since the last asking.
 */
export function offerDue({ platform, keyed, subscribed, permission, hotseat }, memory, now) {
  if (!keyed || !platform || subscribed || hotseat || permission === 'denied') return false;
  if (memory.never || memory.asked >= MOST) return false;
  return memory.last === null || now - memory.last >= WEEK;
}

/**
 * Whether the battle view stands at a moment of the offer: a game against a person that waits on the other player
 * (`awaiting`, what the dock says it waits on, and `other`, who sits in the other seat), or the end of the first
 * tutorial. A bot is nobody to wait for, and a tutorial between its steps waits on nobody.
 * @param {{ awaiting?: object | null, lesson?: { done?: boolean, order?: number } | null, other?: { kind?: string } | null }} [moment]
 */
export function battleMoment({ awaiting = null, lesson = null, other = null } = {}) {
  if (lesson) return lesson.done === true && lesson.order === 1;
  return Boolean(awaiting && other && other.kind !== 'bot');
}

const TITLE = 'Away from the game?';
const WORDS = Object.freeze({
  android: { body: 'Turn on pop-ups to be told when it is your turn, even with the screen off.', turnOn: true },
  browser: { body: 'Turn on pop-ups to be told when it is your turn, even with the tab closed.', turnOn: true },
  iphone: { body: 'Turn on pop-ups to be told when it is your turn, even with the app closed.', turnOn: true },
  'iphone-install': { body: 'On iPhone, pop-ups need the game on your Home Screen: tap Share, then Add to Home Screen, and open it from there.', turnOn: false },
});

/** What the card says on this platform, and whether it offers Turn on (an iPhone outside the home screen cannot). */
export function offerWords(platform) {
  const words = WORDS[platform];
  return words ? { eyebrow: 'POP-UPS', title: TITLE, ...words } : null;
}

/**
 * What the card says once Turn on was answered: on; refused, or left unanswered, in the browser's prompt; or the
 * switch could not be turned on (`failed`: the host or the push service refused). Each says where the switch is.
 */
export function turnedOnLine({ subscribed }, failed = false) {
  if (subscribed) return 'Done: pop-ups are on for this device.';
  if (failed) return 'Pop-ups could not be turned on. You can try again in the menu, under Settings, Notifications.';
  return 'Pop-ups stay off. You can turn them on in the menu, under Settings, Notifications.';
}

const MARKUP = `<section id="push-offer" class="push-offer motion-arrive" aria-labelledby="push-offer-title" hidden>
  <p id="push-offer-eyebrow" class="eyebrow"></p>
  <h2 id="push-offer-title"></h2>
  <p id="push-offer-body"></p>
  <p id="push-offer-said" class="push-offer-said" hidden></p>
  <p id="push-offer-told" class="unseen" role="status" aria-live="polite"></p>
  <div class="push-offer-actions">
    <button id="push-offer-on" type="button" class="primary"></button>
    <button id="push-offer-install" type="button" class="secondary" hidden>Install the app</button>
    <button id="push-offer-later" type="button" class="secondary">Not now</button>
    <button id="push-offer-never" type="button" class="secondary">Don't ask again</button>
    <button id="push-offer-close" type="button" class="secondary" aria-label="Close the pop-ups offer" hidden>Close</button>
  </div>
</section>`;

/**
 * The card, added once to the page's body. `source` is the page's own switch: `state()` says where it stands
 * (`keyed`, `subscribed`, `permission`, `hotseat`, and `ready` once it has read the browser), and `turnOn()` asks
 * the browser and registers the device, the same as the switch does. The page calls `consider()` at its moments; the
 * card shows only when it is due, once a page.
 * @param {Document} doc
 * @param {{ source: { state: () => any, turnOn: () => Promise<any> }, storage?: Storage | null, nav?: any, win?: any, now?: () => number }} options
 */
export function mountPushOffer(doc, { source, storage = null, nav = globalThis.navigator, win = globalThis, now = () => Date.now() } = {}) {
  doc.body.insertAdjacentHTML('beforeend', MARKUP);
  const el = id => doc.getElementById(id);
  const platform = offerPlatform(nav, win);
  // The browser's own install prompt, kept for the card's Install on Android: raised once, used once.
  let install = null;
  let answered = false;
  let busy = false;

  win?.addEventListener?.('beforeinstallprompt', event => {
    if (platform !== 'android') return;
    event.preventDefault();
    install = event;
    if (!el('push-offer').hidden) draw();
  });
  win?.addEventListener?.('appinstalled', () => { install = null; if (!el('push-offer').hidden) draw(); });

  function draw() {
    const words = offerWords(platform);
    el('push-offer-eyebrow').textContent = words.eyebrow;
    el('push-offer-title').textContent = words.title;
    el('push-offer-body').textContent = words.body;
    const said = Boolean(el('push-offer-said').textContent);
    el('push-offer-said').hidden = !said;
    el('push-offer-on').textContent = busy ? 'Turning on…' : 'Turn on';
    el('push-offer').setAttribute?.('aria-busy', String(busy));
    el('push-offer-on').hidden = said || !words.turnOn;
    el('push-offer-install').hidden = said || !install;
    el('push-offer-later').hidden = said;
    el('push-offer-never').hidden = said;
    el('push-offer-close').hidden = !said;
    for (const id of ['push-offer-on', 'push-offer-install', 'push-offer-later', 'push-offer-never']) el(id).disabled = busy;
  }

  // The card never takes the focus when it arrives; when it goes, a focus inside it goes back where it was before.
  let before = null;
  function close() {
    answered = true;
    const inside = el('push-offer').contains?.(doc.activeElement);
    el('push-offer').hidden = true;
    if (inside && before?.isConnected) before.focus?.();
  }

  function answer(memory) {
    writeMemory(storage, memory);
    close();
  }

  async function turnOn() {
    if (busy) return;
    busy = true;
    draw();
    let after = source.state(), failed = false;
    try {
      after = (await source.turnOn()) ?? source.state();
      // Allowed in the prompt, yet not on: the host or the push service refused it.
      failed = !after.subscribed && after.permission === 'granted';
    } catch {
      after = { ...source.state(), subscribed: false };
      failed = true;
    }
    busy = false;
    // A tap on Turn on is an asking, whatever the browser answered: a prompt closed without an answer waits a week too.
    writeMemory(storage, after.subscribed ? neverAgain(readMemory(storage)) : askedOnce(readMemory(storage), now()));
    answered = true;
    el('push-offer-said').textContent = turnedOnLine(after, failed);
    el('push-offer-told').textContent = el('push-offer-said').textContent;
    draw();
    el('push-offer-close').focus?.();
  }

  el('push-offer-on').addEventListener('click', () => void turnOn());
  el('push-offer-later').addEventListener('click', () => answer(askedOnce(readMemory(storage), now())));
  el('push-offer-never').addEventListener('click', () => answer(neverAgain(readMemory(storage))));
  el('push-offer-close').addEventListener('click', close);
  el('push-offer-install').addEventListener('click', async () => {
    const asked = install;
    if (!asked) return;
    install = null;
    draw();
    try { await asked.prompt(); } catch { /* A prompt the browser will not show again: Install goes, Turn on stays. */ }
  });

  return {
    /** A moment the player has a reason to say yes: the card shows if it is due, and never twice a page. */
    consider() {
      if (answered || !el('push-offer').hidden) return false;
      const state = source.state();
      if (!state?.ready || !offerDue({ ...state, platform }, readMemory(storage), now())) return false;
      el('push-offer-said').textContent = '';
      before = doc.activeElement ?? null;
      draw();
      el('push-offer').hidden = false;
      // Said once the card is shown: words written into a hidden region are not announced.
      const tell = () => { el('push-offer-told').textContent = `${TITLE} Pop-ups offered.`; };
      if (win?.requestAnimationFrame) win.requestAnimationFrame(tell); else tell();
      return true;
    },
    /**
     * The moment passed unanswered (the player's own turn came): the card goes, nothing is kept, and this page does
     * not ask again. A card that answered Turn on stays until its Close.
     */
    withdraw() {
      if (el('push-offer').hidden || busy || el('push-offer-said').textContent) return;
      close();
    },
    /** The switch changed elsewhere (Settings, the match settings): a card for a browser now on goes. */
    changed() {
      if (!el('push-offer').hidden && !busy && !el('push-offer-said').textContent && source.state()?.subscribed) el('push-offer').hidden = true;
    },
  };
}
