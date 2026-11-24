import { test } from 'node:test';
import assert from 'node:assert/strict';
import { deviceSwitch, kindsBody, KINDS, mountDevices, needsFreshSubscription, oldOptOut, forgetOldOptOut } from './devices.js';

const base = { supported: true, keyed: true, permission: 'default', subscribed: false, busy: false };
const tick = () => new Promise(resolve => setImmediate(resolve));
const settled = async () => { for (let turn = 0; turn < 10; turn++) await tick(); };

test('the switch is on when this browser is a device of the account, and says what on and off mean', () => {
  assert.deepEqual(deviceSwitch(base), { hidden: false, disabled: false, checked: false, hint: 'Off: nothing pops up here.' });
  assert.equal(deviceSwitch({ ...base, subscribed: true }).checked, true);
  assert.match(deviceSwitch({ ...base, subscribed: true }).hint, /what you choose below pops up here/);
  assert.equal(deviceSwitch({ ...base, busy: true }).disabled, true);
  assert.equal(deviceSwitch({ ...base, busy: true }).hint, 'Off: nothing pops up here.', 'busy keeps its words');
});

test('the reasons a device cannot show pop-ups are the seat page\'s, said about the account', () => {
  assert.equal(deviceSwitch({ ...base, keyed: false }).hidden, true, 'a host without keys offers nothing');
  const blocked = deviceSwitch({ ...base, permission: 'denied' });
  assert.equal(blocked.disabled, true);
  assert.equal(blocked.checked, false);
  assert.match(blocked.hint, /^Notifications are blocked\. .*to see pop-ups/);
  assert.doesNotMatch(blocked.hint, /your turn/);
  assert.match(deviceSwitch({ ...base, supported: false, ios: true }).hint, /^Notifications need the home screen/);
  assert.match(deviceSwitch({ ...base, supported: false, secure: false }).hint, /^Notifications need https/);
});

test('a hotseat seat page\'s silence does not apply to an account: the switch never says both seats are here', () => {
  assert.equal(deviceSwitch({ ...base, hotseat: true }).disabled, false);
});

test('the kinds are sent as the boxes say, and a kind with no box stays on', () => {
  assert.deepEqual(KINDS, ['turns', 'invitations', 'results']);
  assert.deepEqual(kindsBody({ turns: true, invitations: false, results: false }), { turns: true, invitations: false, results: false });
  assert.deepEqual(kindsBody({ invitations: false }), { turns: true, invitations: false, results: true });
  assert.deepEqual(kindsBody(null), { turns: true, invitations: true, results: true });
});

test('only a 410 from the host means the browser must make a new subscription', () => {
  assert.equal(needsFreshSubscription({ status: 410 }), true);
  assert.equal(needsFreshSubscription({ status: 401 }), false);
  assert.equal(needsFreshSubscription(new Error('offline')), false);
  assert.equal(needsFreshSubscription(undefined), false);
});

/** The panel's elements as the page draws them, and a browser that holds one subscription. */
function harness({ notices = { turns: true, invitations: true, results: true }, held = true, storage = null, testRefused = null } = {}) {
  const calls = [];
  const listeners = {};
  const element = id => ({ id, hidden: false, checked: false, disabled: false, textContent: '', addEventListener: (type, listener) => { listeners[`${id}:${type}`] = listener; } });
  const boxes = Object.fromEntries(KINDS.map(kind => [kind, element(`device-kind-${kind}`)]));
  const elements = Object.fromEntries(['device-panel', 'device-unavailable', 'device-bell', 'device-toggle', 'device-hint', 'device-test', 'device-problem', 'device-result'].map(id => [id, element(id)]));
  elements['device-kinds'] = { ...element('device-kinds'), querySelector: selector => boxes[/data-kind="(\w+)"/.exec(selector)[1]] };
  elements['device-kinds'].addEventListener = (type, listener) => { listeners[`device-kinds:${type}`] = listener; };
  let subscription = held ? { endpoint: 'https://push.example/laptop', options: { applicationServerKey: new Uint8Array([1, 2, 3]) }, toJSON: () => ({ endpoint: 'https://push.example/laptop' }), unsubscribe: async () => { calls.push('unsubscribe'); subscription = null; } } : null;
  const registration = { pushManager: { getSubscription: async () => subscription } };
  const nav = { serviceWorker: { register: async () => registration, ready: Promise.resolve(registration) } };
  const win = { PushManager: {}, Notification: { permission: 'granted' } };
  const request = async (path, body, _fetch, method = body === undefined ? 'GET' : 'POST') => {
    calls.push(`${method} ${path}${body === undefined ? '' : ` ${JSON.stringify(body)}`}`);
    if (path === '/api/lobby/push') return { publicKey: 'AQID' };
    if (path === '/api/account/preferences') return notices ? { notices } : {};
    if (path === '/api/account/preferences/notices') return { notices: body };
    if (path === '/api/account/push') return { notices: method === 'PUT' };
    if (path === '/api/account/push/test' && testRefused) throw testRefused;
    return {};
  };
  const doc = { getElementById: id => elements[id] ?? null };
  const devices = mountDevices(doc, { request, nav, win, storage });
  return { devices, calls, elements, boxes, listeners };
}

test('a signed-in player reads the line on the bell, the switch on, and the kinds the account chose', async () => {
  const h = harness({ notices: { turns: true, invitations: false, results: true } });

  h.devices.setAccount({ id: 'acct_mark' });
  await settled();

  assert.equal(h.elements['device-bell'].hidden, false);
  assert.equal(h.elements['device-panel'].hidden, false);
  assert.equal(h.elements['device-toggle'].checked, true);
  assert.equal(h.elements['device-kinds'].hidden, false);
  assert.deepEqual(KINDS.map(kind => h.boxes[kind].checked), [true, false, true]);
  assert.ok(h.calls.includes('PUT /api/account/push {"endpoint":"https://push.example/laptop"}'));
});

test('turning the switch off forgets the device and lets go of the browser\'s subscription, which every page shares', async () => {
  const h = harness();
  h.devices.setAccount({ id: 'acct_mark' });
  await settled();

  h.listeners['device-toggle:change']();
  await settled();

  assert.deepEqual(h.calls.slice(-2), ['DELETE /api/account/push {"endpoint":"https://push.example/laptop"}', 'unsubscribe']);
  assert.equal(h.elements['device-toggle'].checked, false);
  assert.equal(h.elements['device-kinds'].hidden, false, 'the kinds are the account\'s, whatever this device does');
});

test('a box changed saves the three kinds for the account at once', async () => {
  const h = harness();
  h.devices.setAccount({ id: 'acct_mark' });
  await settled();
  h.boxes.results.checked = false;

  h.listeners['device-kinds:change']();
  await settled();

  assert.equal(h.calls.at(-1), 'PUT /api/account/preferences/notices {"turns":true,"invitations":true,"results":false}');
  assert.equal(h.boxes.results.checked, false);
});

test('a host that keeps no settings shows the switch and no kinds', async () => {
  const h = harness({ notices: null });

  h.devices.setAccount({ id: 'acct_mark' });
  await settled();

  assert.equal(h.elements['device-panel'].hidden, false);
  assert.equal(h.elements['device-kinds'].hidden, true);
});

function kept(values = {}) {
  const map = new Map(Object.entries(values));
  return { getItem: key => map.get(key) ?? null, setItem: (key, value) => map.set(key, value), removeItem: key => map.delete(key), map };
}

test('a browser turned off under the old switch is read for its account only, and forgotten on request', () => {
  const storage = kept({ 'downfall.table.device-opt-out': 'acct_mark' });
  assert.equal(oldOptOut(storage, 'acct_bastien'), false, 'another account on the same browser');
  assert.equal(oldOptOut(storage, 'acct_mark'), true);
  forgetOldOptOut(storage);
  assert.equal(oldOptOut(storage, 'acct_mark'), false);
  assert.equal(oldOptOut(null, 'acct_mark'), false, 'a browser that keeps nothing');
  assert.equal(oldOptOut({ getItem() { throw new Error('blocked'); } }, 'acct_mark'), false);
  assert.doesNotThrow(() => forgetOldOptOut({ removeItem() { throw new Error('blocked'); } }));
});

test('a browser turned off under the old switch is forgotten by the host, lets go of its subscription, then of the note', async () => {
  const storage = kept({ 'downfall.table.device-opt-out': 'acct_mark' });
  const h = harness({ storage });

  h.devices.setAccount({ id: 'acct_mark' });
  await settled();

  assert.deepEqual(h.calls.filter(call => call.includes('/api/account/push') || call === 'unsubscribe'), ['DELETE /api/account/push {"endpoint":"https://push.example/laptop"}', 'unsubscribe']);
  assert.equal(storage.map.size, 0);
  assert.equal(h.elements['device-toggle'].checked, false);
});

test('a box ticked while a save is on its way is kept as ticked and saved next', async () => {
  const h = harness();
  h.devices.setAccount({ id: 'acct_mark' });
  await settled();

  h.boxes.results.checked = false;
  h.listeners['device-kinds:change']();
  h.boxes.turns.checked = false;
  h.listeners['device-kinds:change']();
  await settled();

  assert.deepEqual(h.calls.filter(call => call.startsWith('PUT /api/account/preferences/notices')), [
    'PUT /api/account/preferences/notices {"turns":true,"invitations":true,"results":false}',
    'PUT /api/account/preferences/notices {"turns":false,"invitations":true,"results":false}',
  ]);
  assert.deepEqual(KINDS.map(kind => h.boxes[kind].checked), [false, true, false]);
});

test('the offer reads the switch only once it has read this browser, and Turn on is the switch turned on', async () => {
  const { devices, calls } = harness({ held: false });
  assert.equal(devices.offerState().ready, false, 'nobody signed in: nothing read');
  devices.setAccount({ id: 'acc-1' });
  await settled();
  assert.deepEqual(devices.offerState(), { ready: true, keyed: true, subscribed: false, permission: 'granted', hotseat: false });
  // The harness's browser has no push manager to subscribe with: the switch tries, and the state says off.
  const after = await devices.turnOn();
  assert.equal(after.subscribed, false);
  assert.equal(after.ready, true);
  assert.equal(calls.filter(call => call.startsWith('PUT /api/account/push')).length, 0);
});

test('a browser already on is not turned on again by the offer', async () => {
  const { devices, calls } = harness();
  devices.setAccount({ id: 'acc-1' });
  await settled();
  assert.equal(devices.offerState().subscribed, true);
  const before = calls.length;
  await devices.turnOn();
  assert.equal(calls.length, before);
});

test('a browser that cannot push is read once the host\'s key is, so the offer can give an iPhone the steps', async () => {
  const { devices } = harness();
  const unable = mountDevices({ getElementById: () => null }, { request: async path => (path === '/api/lobby/push' ? { publicKey: 'AQID' } : {}), nav: { userAgent: 'iPhone' }, win: {} });
  unable.setAccount({ id: 'acc-1' });
  await settled();
  assert.deepEqual(unable.offerState(), { ready: true, keyed: true, subscribed: false, permission: 'default', hotseat: false });
  assert.equal(devices.offerState().ready, false, 'nobody signed in there');
});

test('a test on a push service the host does not send to turns the switch off and lets go of the browser\'s subscription (#1209)', async () => {
  const refused = Object.assign(new Error('This browser\'s push service is not one this host sends to.'), { status: 400, code: 'Account.UnknownPushService' });
  const h = harness({ testRefused: refused });
  h.devices.setAccount({ id: 'acct_mark' });
  await settled();

  h.listeners['device-test:click']();
  await settled();

  assert.equal(h.calls.at(-1), 'unsubscribe');
  assert.equal(h.elements['device-toggle'].checked, false);
  assert.match(h.elements['device-result'].textContent, /not one this host sends to/);
});
