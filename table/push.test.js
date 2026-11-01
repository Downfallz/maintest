import { test } from 'node:test';
import assert from 'node:assert/strict';
import { keyBytes, noticeButton, pushSupported, sameKey } from './push.js';

test('a browser pushes when it has a worker, a push manager, notifications and a secure context', () => {
  const nav = { serviceWorker: {} };
  const win = { PushManager: class {}, Notification: class {}, isSecureContext: true };

  assert.equal(pushSupported(nav, win), true);
  assert.equal(pushSupported({}, win), false, 'no worker');
  assert.equal(pushSupported(nav, { Notification: class {}, isSecureContext: true }), false, 'no push manager');
  assert.equal(pushSupported(nav, { ...win, isSecureContext: false }), false, 'http on the LAN');
  assert.equal(pushSupported(undefined, undefined), false);
});

test('the host key is read as bytes the way applicationServerKey wants it, without padding', () => {
  // 0x04 then 0xff: base64url of two bytes needs no padding characters, and the browser never sends them.
  assert.deepEqual([...keyBytes('BP8')], [4, 255]);
  assert.deepEqual([...keyBytes('-_8')], [251, 255]);
  assert.equal(keyBytes('BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A').length, 65);
});

test('a subscription made with the host key is kept, one made with another is not', () => {
  const made = { options: { applicationServerKey: Uint8Array.from([4, 255]).buffer } };

  assert.equal(sameKey(made, 'BP8'), true);
  assert.equal(sameKey(made, 'BP4'), false);
  assert.equal(sameKey({ options: {} }, 'BP8'), false);
  assert.equal(sameKey(null, 'BP8'), false);
});

test('the button is offered only where a notice could reach, and says what tapping it does', () => {
  const base = { supported: true, keyed: true, permission: 'default', subscribed: false, busy: false, practice: false };

  assert.equal(noticeButton(base).label, 'Notify me when it\'s my turn');
  assert.equal(noticeButton({ ...base, subscribed: true }).label, 'Stop notifying me');
  assert.equal(noticeButton({ ...base, busy: true }).disabled, true);
  assert.equal(noticeButton({ ...base, permission: 'denied' }).disabled, true);
  assert.match(noticeButton({ ...base, permission: 'denied' }).hint, /browser's settings/);
  assert.equal(noticeButton({ ...base, supported: false }).hidden, true);
  assert.equal(noticeButton({ ...base, supported: false, ios: true }).label, 'Notifications need the home screen', 'an iPhone is told what to do');
  assert.equal(noticeButton({ ...base, supported: false, ios: true, keyed: false }).hidden, true, 'not without keys');
  assert.equal(noticeButton({ ...base, keyed: false }).hidden, true, 'a host without keys');
  assert.equal(noticeButton({ ...base, practice: true }).hidden, true);
  assert.equal(noticeButton({}).hidden, true);
});
