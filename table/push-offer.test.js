import { test } from 'node:test';
import assert from 'node:assert/strict';
import { askedOnce, battleMoment, MOST, mountPushOffer, neverAgain, OFFER_KEY, offerDue, offerPlatform, offerWords, readMemory, turnedOnLine, WEEK, writeMemory } from './push-offer.js';

const ANDROID = 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/131.0 Mobile Safari/537.36';
const IPHONE = 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 Version/18.1 Mobile/15E148 Safari/604.1';
const DESKTOP = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0 Safari/537.36';
const pushing = { PushManager: {}, Notification: {}, isSecureContext: true };
const nav = userAgent => ({ userAgent, serviceWorker: {} });
const fresh = { never: false, asked: 0, last: null };
const due = { platform: 'android', keyed: true, subscribed: false, permission: 'default', hotseat: false };
const NOW = Date.UTC(2026, 10, 23, 12);

function store(initial = {}) {
  const kept = { ...initial };
  return { kept, getItem: key => (key in kept ? kept[key] : null), setItem: (key, value) => { kept[key] = String(value); }, removeItem: key => { delete kept[key]; } };
}

test('the platform is read from the browser: Android, an iPhone, any other browser, or nothing to offer', () => {
  assert.equal(offerPlatform(nav(ANDROID), pushing), 'android');
  assert.equal(offerPlatform(nav(DESKTOP), pushing), 'browser');
  assert.equal(offerPlatform(nav(IPHONE), pushing), 'iphone', 'an iPhone that pushes is at the home screen');
  assert.equal(offerPlatform({ userAgent: IPHONE }, { isSecureContext: true }), 'iphone-install');
  assert.equal(offerPlatform({ userAgent: IPHONE, standalone: true }, { isSecureContext: true }), null, 'at the home screen and still no push: no step helps');
  assert.equal(offerPlatform({ userAgent: IPHONE }, { isSecureContext: false }), null);
  assert.equal(offerPlatform({ userAgent: ANDROID }, { isSecureContext: true }), null, 'a browser that cannot push is offered nothing');
  assert.equal(offerPlatform(nav(ANDROID), { ...pushing, isSecureContext: false }), null, 'plain http takes no push');
});

test('the card is due once, to a browser that can be told and is not', () => {
  assert.equal(offerDue(due, fresh, NOW), true);
  assert.equal(offerDue({ ...due, keyed: false }, fresh, NOW), false, 'a host without keys sends nothing');
  assert.equal(offerDue({ ...due, platform: null }, fresh, NOW), false);
  assert.equal(offerDue({ ...due, subscribed: true }, fresh, NOW), false, 'already on');
  assert.equal(offerDue({ ...due, permission: 'denied' }, fresh, NOW), false, 'blocked is a no already said');
  assert.equal(offerDue({ ...due, permission: 'granted' }, fresh, NOW), true, 'allowed but not registered is still off');
  assert.equal(offerDue({ ...due, hotseat: true }, fresh, NOW), false, 'both seats here: nobody is away');
  assert.equal(offerDue({ ...due, platform: 'iphone-install' }, fresh, NOW), true);
});

test('after Not now it asks again a week later at the earliest, three times at most', () => {
  const once = askedOnce(fresh, NOW);
  assert.deepEqual(once, { never: false, asked: 1, last: NOW });
  assert.equal(offerDue(due, once, NOW + WEEK - 1), false);
  assert.equal(offerDue(due, once, NOW + WEEK), true);
  const third = askedOnce(askedOnce(once, NOW + WEEK), NOW + 2 * WEEK);
  assert.equal(third.asked, MOST);
  assert.equal(offerDue(due, third, NOW + 10 * WEEK), false);
});

test('Don\'t ask again ends it at once', () => {
  assert.equal(offerDue(due, neverAgain(fresh), NOW), false);
  assert.equal(offerDue(due, neverAgain(fresh), NOW + 100 * WEEK), false);
});

test('the answers are kept in the browser, and an unreadable note is no note', () => {
  const storage = store();
  writeMemory(storage, askedOnce(fresh, NOW));
  assert.deepEqual(readMemory(storage), { never: false, asked: 1, last: NOW });
  assert.deepEqual(readMemory(store({ [OFFER_KEY]: 'not json' })), fresh);
  assert.deepEqual(readMemory(store({ [OFFER_KEY]: '{"asked":"two","last":"soon","never":"yes"}' })), fresh);
  assert.deepEqual(readMemory(null), fresh);
  const refusing = { getItem: () => { throw new Error('denied'); }, setItem: () => { throw new Error('denied'); } };
  assert.deepEqual(readMemory(refusing), fresh);
  assert.doesNotThrow(() => writeMemory(refusing, fresh));
});

test('the words follow the platform, and an iPhone outside the home screen gets the steps instead of Turn on', () => {
  assert.equal(offerWords('android').body, 'Turn on pop-ups to be told when it is your turn, even with the screen off.');
  assert.equal(offerWords('android').turnOn, true);
  assert.match(offerWords('browser').body, /tab closed/);
  assert.match(offerWords('iphone').body, /app closed/);
  assert.equal(offerWords('iphone-install').turnOn, false);
  assert.match(offerWords('iphone-install').body, /Share, then Add to Home Screen/);
  assert.equal(offerWords(null), null);
  for (const platform of ['android', 'browser', 'iphone', 'iphone-install']) assert.equal(offerWords(platform).title, 'Away from the game?');
});

test('what the card says after Turn on: done, blocked, or still off, each with where the switch is', () => {
  assert.match(turnedOnLine({ subscribed: true }), /^Done/);
  assert.match(turnedOnLine({ subscribed: false, permission: 'denied' }), /stay off.*Settings, Notifications/);
  assert.match(turnedOnLine({ subscribed: false, permission: 'default' }), /stay off.*Settings, Notifications/);
  assert.match(turnedOnLine({ subscribed: false, permission: 'granted' }, true), /could not be turned on.*Settings, Notifications/);
});

/** A page with a body to add the card to, the elements it adds, and a window that raises events. */
function page({ userAgent = ANDROID, state = { ready: true, keyed: true, subscribed: false, permission: 'default' }, turnOn, storage = store(), now = () => NOW } = {}) {
  const elements = {};
  const listeners = {};
  const element = id => ({ id, hidden: false, disabled: false, textContent: '', focused: 0, focus() { this.focused++; }, setAttribute() {}, contains: node => Object.values(elements).includes(node), addEventListener: (type, listener) => { listeners[`${id}:${type}`] = listener; } });
  const doc = {
    body: { insertAdjacentHTML: (_where, html) => {
      for (const [, id, hidden] of html.matchAll(/id="([\w-]+)"[^>]*?( hidden)?>/g)) { elements[id] = element(id); elements[id].hidden = Boolean(hidden); }
    } },
    getElementById: id => elements[id] ?? null,
  };
  const win = { ...pushing, addEventListener: (type, listener) => { listeners[`window:${type}`] = listener; } };
  const current = { ...state };
  const source = { state: () => current, turnOn: turnOn ?? (async () => { current.subscribed = true; current.permission = 'granted'; return current; }) };
  const offer = mountPushOffer(doc, { source, storage, nav: nav(userAgent), win, now });
  const click = async id => { await listeners[`${id}:click`](); await new Promise(resolve => setImmediate(resolve)); };
  return { offer, elements, listeners, storage, current, click, win, doc };
}

test('the card is added once to the page, hidden until a moment makes it due', () => {
  const { offer, elements } = page();
  assert.equal(elements['push-offer'].hidden, true);
  assert.equal(offer.consider(), true);
  assert.equal(elements['push-offer'].hidden, false);
  assert.equal(elements['push-offer-title'].textContent, 'Away from the game?');
  assert.equal(elements['push-offer-on'].textContent, 'Turn on');
  assert.equal(elements['push-offer-on'].hidden, false);
  assert.equal(elements['push-offer-install'].hidden, true, 'no Install until the browser offers one');
});

test('a switch that has not yet read the browser makes no card', () => {
  const { offer, elements } = page({ state: { ready: false, keyed: true, subscribed: false, permission: 'default' } });
  assert.equal(offer.consider(), false);
  assert.equal(elements['push-offer'].hidden, true);
});

test('Not now closes the card, is remembered, and the page does not ask again', async () => {
  const { offer, elements, storage, click } = page();
  offer.consider();
  await click('push-offer-later');
  assert.equal(elements['push-offer'].hidden, true);
  assert.deepEqual(readMemory(storage), { never: false, asked: 1, last: NOW });
  assert.equal(offer.consider(), false);
});

test('Don\'t ask again is remembered for good', async () => {
  const { offer, storage, click } = page();
  offer.consider();
  await click('push-offer-never');
  assert.equal(readMemory(storage).never, true);
  assert.equal(page({ storage }).offer.consider(), false, 'a later page of the same browser');
});

test('Turn on asks the switch, says Done, and the card offers only Close', async () => {
  let asked = 0;
  const harness = page({ turnOn: async () => { asked++; harness.current.subscribed = true; return harness.current; } });
  harness.offer.consider();
  await harness.click('push-offer-on');
  assert.equal(asked, 1);
  assert.match(harness.elements['push-offer-said'].textContent, /^Done/);
  assert.equal(harness.elements['push-offer-on'].hidden, true);
  assert.equal(harness.elements['push-offer-later'].hidden, true);
  assert.equal(harness.elements['push-offer-close'].hidden, false);
  assert.equal(harness.elements['push-offer-close'].focused, 1, 'the focus goes to what is left to press');
  await harness.click('push-offer-close');
  assert.equal(harness.elements['push-offer'].hidden, true);
  assert.equal(readMemory(harness.storage).never, true, 'on is the last answer');
});

test('Turn on refused in the browser\'s prompt says where the switch is, and counts as an asking', async () => {
  const harness = page({ turnOn: async () => { harness.current.permission = 'denied'; return harness.current; } });
  harness.offer.consider();
  await harness.click('push-offer-on');
  assert.match(harness.elements['push-offer-said'].textContent, /stay off/);
  assert.equal(harness.elements['push-offer-told'].textContent, harness.elements['push-offer-said'].textContent, 'announced');
  assert.deepEqual(readMemory(harness.storage), { never: false, asked: 1, last: NOW });
});

test('a switch that fails is pop-ups still off, not a broken card', async () => {
  const harness = page({ turnOn: async () => { throw new Error('no push service'); } });
  harness.offer.consider();
  await harness.click('push-offer-on');
  assert.match(harness.elements['push-offer-said'].textContent, /could not be turned on/);
  assert.equal(harness.elements['push-offer-close'].hidden, false);
});

test('allowed in the prompt yet not on is the host\'s refusal, not the player\'s', async () => {
  const harness = page({ turnOn: async () => { harness.current.permission = 'granted'; return harness.current; } });
  harness.offer.consider();
  await harness.click('push-offer-on');
  assert.match(harness.elements['push-offer-said'].textContent, /could not be turned on/);
});

test('the card arrives without the focus, is announced, and gives the focus back when it goes', async () => {
  const harness = page();
  const before = { isConnected: true, focused: 0, focus() { this.focused++; } };
  harness.doc.activeElement = before;
  harness.offer.consider();
  assert.match(harness.elements['push-offer-told'].textContent, /Pop-ups offered/);
  harness.doc.activeElement = harness.elements['push-offer-later'];
  await harness.click('push-offer-later');
  assert.equal(before.focused, 1);
});

test('Turn on says it is on its way while the browser is asked', async () => {
  let release;
  const harness = page({ turnOn: () => new Promise(resolve => { release = () => { harness.current.subscribed = true; resolve(harness.current); }; }) });
  harness.offer.consider();
  const pending = harness.listeners['push-offer-on:click']();
  assert.equal(harness.elements['push-offer-on'].textContent, 'Turning on…');
  assert.equal(harness.elements['push-offer-later'].disabled, true);
  release(); await pending;
  assert.match(harness.elements['push-offer-said'].textContent, /^Done/);
});

test('an iPhone outside the home screen is given the steps, with no Turn on', () => {
  const elements = {};
  const doc = { body: { insertAdjacentHTML: (_where, html) => { for (const [, id] of html.matchAll(/id="([\w-]+)"/g)) elements[id] = { hidden: true, textContent: '', addEventListener() {} }; } }, getElementById: id => elements[id] };
  const offer = mountPushOffer(doc, { source: { state: () => ({ ready: true, keyed: true, subscribed: false, permission: 'default' }) }, storage: store(), nav: { userAgent: IPHONE }, win: { isSecureContext: true }, now: () => NOW });
  assert.equal(offer.consider(), true);
  assert.equal(elements['push-offer-on'].hidden, true);
  assert.match(elements['push-offer-body'].textContent, /Add to Home Screen/);
  assert.equal(elements['push-offer-later'].hidden, false);
  assert.equal(elements['push-offer-never'].hidden, false);
});

test('on Android the browser\'s install prompt becomes Install on the card, used once', async () => {
  const harness = page();
  let prompted = 0, prevented = 0;
  harness.listeners['window:beforeinstallprompt']({ preventDefault: () => { prevented++; }, prompt: async () => { prompted++; } });
  assert.equal(prevented, 1);
  harness.offer.consider();
  assert.equal(harness.elements['push-offer-install'].hidden, false);
  await harness.click('push-offer-install');
  assert.equal(prompted, 1);
  assert.equal(harness.elements['push-offer-install'].hidden, true);
  assert.equal(harness.elements['push-offer'].hidden, false, 'Turn on is still asked');
});

test('another platform keeps the browser\'s own install prompt', () => {
  const harness = page({ userAgent: DESKTOP });
  let prevented = 0;
  harness.listeners['window:beforeinstallprompt']({ preventDefault: () => { prevented++; }, prompt: async () => {} });
  assert.equal(prevented, 0);
  harness.offer.consider();
  assert.equal(harness.elements['push-offer-install'].hidden, true);
});

test('a card for a browser turned on elsewhere goes', () => {
  const harness = page();
  harness.offer.consider();
  harness.current.subscribed = true;
  harness.offer.changed();
  assert.equal(harness.elements['push-offer'].hidden, true);
});

test('the battle view\'s moments: the other player\'s move against a person, and the end of the first tutorial', () => {
  const awaiting = { kind: 'choice' };
  assert.equal(battleMoment({ awaiting, other: { kind: 'person' } }), true);
  assert.equal(battleMoment({ awaiting, other: { kind: 'open' } }), true, 'a waiting room for a person to come');
  assert.equal(battleMoment({ awaiting, other: { kind: 'bot' } }), false, 'a bot is nobody to wait for');
  assert.equal(battleMoment({ awaiting: null, other: { kind: 'person' } }), false, 'my turn');
  assert.equal(battleMoment({ awaiting, other: null }), false, 'nobody known in the other seat');
  assert.equal(battleMoment({ lesson: { order: 1, done: true } }), true);
  assert.equal(battleMoment({ lesson: { order: 1, done: false }, awaiting, other: { kind: 'person' } }), false, 'a lesson under way');
  assert.equal(battleMoment({ lesson: { order: 2, done: true } }), false, 'only the first tutorial\'s end');
  assert.equal(battleMoment(), false);
});
