import { test } from 'node:test';
import assert from 'node:assert/strict';
import { deviceButton, needsFreshSubscription } from './devices.js';

const base = { supported: true, keyed: true, permission: 'default', subscribed: false, busy: false };

test('the button speaks for the account, not for a seat', () => {
  assert.equal(deviceButton(base).label, 'Notify me on this device');
  assert.match(deviceButton(base).hint, /Friend requests, invitations/);
  assert.equal(deviceButton({ ...base, subscribed: true }).label, 'Stop notifying this device');
  assert.match(deviceButton({ ...base, subscribed: true }).hint, /your friends, your invitations and your games/);
  assert.equal(deviceButton({ ...base, busy: true }).disabled, true);
  assert.equal(deviceButton({ ...base, busy: true }).label, 'Notify me on this device', 'busy keeps the account\'s words');
});

test('the reasons a device cannot be told are the seat page\'s, said about the account', () => {
  assert.equal(deviceButton({ ...base, keyed: false }).hidden, true, 'a host without keys offers nothing');
  assert.equal(deviceButton({ ...base, permission: 'denied' }).disabled, true);
  assert.match(deviceButton({ ...base, permission: 'denied' }).hint, /about your friends and your games/);
  assert.doesNotMatch(deviceButton({ ...base, permission: 'denied' }).hint, /your turn/);
  assert.equal(deviceButton({ ...base, supported: false, ios: true }).label, 'Notifications need the home screen');
  assert.equal(deviceButton({ ...base, supported: false, secure: false }).label, 'Notifications need https');
});

test('a hotseat seat page\'s silence does not apply to an account: the panel never says both seats are here', () => {
  assert.equal(deviceButton({ ...base, hotseat: true }).label, 'Notify me on this device');
});

test('only a 410 from the host means the browser must make a new subscription', () => {
  assert.equal(needsFreshSubscription({ status: 410 }), true);
  assert.equal(needsFreshSubscription({ status: 401 }), false);
  assert.equal(needsFreshSubscription(new Error('offline')), false);
  assert.equal(needsFreshSubscription(undefined), false);
});
