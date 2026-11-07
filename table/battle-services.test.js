import test from 'node:test';
import assert from 'node:assert/strict';
import { createBattleServices } from './battle-services.js';
const tick = () => new Promise(resolve => setImmediate(resolve));
function harness({ permission = 'default', hotseat = false, held = false, supported = true } = {}) {
  const calls = [];
  let subscription = held ? sub() : null;
  function sub() { return { options: { applicationServerKey: new Uint8Array([1, 2, 3]) }, toJSON: () => ({ endpoint: 'local-test' }), unsubscribe: async () => { calls.push('unsubscribe'); subscription = null; } }; }
  const registration = { pushManager: { getSubscription: async () => subscription, subscribe: async () => { calls.push('subscribe'); return subscription = sub(); } },
    showNotification: async () => { calls.push('page-test'); }, getNotifications: async () => [{ tag: 'test:page' }] };
  const scope = { navigator: supported ? { serviceWorker: { register: async () => registration, ready: Promise.resolve(registration) } } : {},
    PushManager: {}, Notification: { permission, requestPermission: async () => { calls.push('permission'); scope.Notification.permission = permission === 'denied' ? 'denied' : 'granted'; return scope.Notification.permission; } } };
  const transport = { pushKey: async () => ({ ok: true, body: { publicKey: 'AQID' } }),
    push: async () => { calls.push('bind'); return { ok: true }; }, forgetPush: async () => { calls.push('forget'); return { ok: true }; },
    pushTest: async () => { calls.push('host-test'); return { ok: true, body: { message: 'Sent.' } }; },
    note: async body => { calls.push(body); return { ok: true }; } };
  const control = createBattleServices({ onChange() {}, scope });
  control.reset(transport, hotseat ? [transport, transport] : [transport]);
  control.receive({ recording: true, over: false }); control.setOnline(true);
  return { control, calls, scope, transport, registration };
}
test('opening a seat offers notices without asking permission or making a subscription', async () => {
  const h = harness(); await tick(); assert.deepEqual(h.calls, []); assert.equal(h.control.model().notice.disabled, false);
  await h.control.toggle(); assert.deepEqual(h.calls, ['permission', 'subscribe', 'bind']);
  assert.equal(h.control.model().canTest, true); await h.control.test();
  assert.deepEqual(h.calls.slice(-2), ['page-test', 'host-test']); assert.match(h.control.model().message, /Two notices/);
  await h.control.toggle(); assert.deepEqual(h.calls.slice(-2), ['unsubscribe', 'forget']);
});
test('denied and unsupported browsers explain why without subscribing or testing', async () => {
  for (const options of [{ permission: 'denied' }, { supported: false }]) {
    const h = harness(options); await tick(); assert.equal(h.control.model().notice.disabled, true);
    assert.match(h.control.model().notice.label, /blocked|cannot notify/); await h.control.toggle(); await h.control.test(); assert.deepEqual(h.calls, []);
  }
});
test('both held seats rebind an existing endpoint for server suppression and offer no test', async () => {
  const h = harness({ hotseat: true, held: true }); await tick(); assert.deepEqual(h.calls, ['bind', 'bind']);
  assert.match(h.control.model().notice.label, /both seats/); assert.equal(h.control.model().canTest, false);
  await h.control.toggle(); await h.control.test(); assert.deepEqual(h.calls, ['bind', 'bind']);
});
test('late permission does not subscribe or bind after a seat switch', async () => {
  const h = harness(); await tick(); let done;
  h.scope.Notification.requestPermission = () => new Promise(resolve => { done = resolve; });
  const pending = h.control.toggle(); h.control.reset(); done('granted'); await pending;
  assert.deepEqual(h.calls, []); assert.equal(h.control.model().notice.hidden, true);
});
test('a late worker does not bind a subscription after access loss', async () => {
  const h = harness({ held: true }); let done;
  h.scope.navigator.serviceWorker.ready = new Promise(resolve => { done = resolve; });
  await tick(); h.control.reset(); done(h.registration); await tick(); assert.deepEqual(h.calls, []);
});
test('the model names the seat it belongs to and says when its view has not arrived yet', () => {
  const control = createBattleServices({ onChange() {}, scope: {} });
  control.reset({}, [], 'one:player1');
  assert.equal(control.model().owner, 'one:player1'); assert.equal(control.model().loaded, false);
  control.receive({ recording: true, over: true }); assert.equal(control.model().loaded, true);
  control.reset(); assert.equal(control.model().owner, null);
});
