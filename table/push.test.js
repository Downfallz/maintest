import { test } from 'node:test';
import assert from 'node:assert/strict';
import { keyBytes, noticeButton, offerShown, pushSupported, sameKey, testReading } from './push.js';

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
  assert.equal(noticeButton({ ...base, supported: false }).label, 'This browser cannot notify you', 'a chat app\'s browser is told, not left without a button');
  assert.equal(noticeButton({ ...base, supported: false }).disabled, true);
  assert.match(noticeButton({ ...base, supported: false }).hint, /Chrome, Firefox or Safari/);
  assert.equal(noticeButton({ ...base, supported: false, secure: false }).label, 'Notifications need https');
  assert.equal(noticeButton({ ...base, supported: false, secure: false }).offered, false, 'plain http is the operator\'s affair, not offered to a player');
  assert.equal(noticeButton({ ...base, supported: false, ios: true }).label, 'Notifications need the home screen', 'an iPhone is told what to do');
  assert.equal(noticeButton({ ...base, supported: false, ios: true, keyed: false }).hidden, true, 'not without keys');
  assert.equal(noticeButton({ ...base, keyed: false }).hidden, true, 'a host without keys');
  assert.equal(noticeButton({ ...base, practice: true }).hidden, true);
  assert.equal(noticeButton({ ...base, hotseat: true }).label, 'No notices with both seats here', 'two seats on one device: the panel says why');
  assert.equal(noticeButton({ ...base, hotseat: true }).disabled, true);
  assert.equal(noticeButton({ ...base, hotseat: true }).offered, false, 'two seats on one device: nobody is away, nothing is offered');
  assert.equal(noticeButton({}).hidden, true);
});

test('the offer is made once, to a page that could be told and is not yet', () => {
  const offerable = noticeButton({ supported: true, keyed: true, permission: 'default' });

  assert.equal(offerShown(offerable, { permission: 'default' }), true);
  assert.equal(offerShown(offerable, { permission: 'default' }, true), false, 'declined once, not asked again');
  assert.equal(offerShown(offerable, { permission: 'denied' }), false, 'a person who blocked them said no already');
  assert.equal(offerShown(offerable, { subscribed: true }), false);
  assert.equal(offerShown(noticeButton({ supported: false, keyed: true, ios: true }), {}), true, 'an iPhone is told the way once');
  assert.equal(offerShown(noticeButton({ supported: true, keyed: true, hotseat: true }), {}), false);
  assert.equal(offerShown(noticeButton({ supported: true, keyed: false }), {}), false, 'a host without keys offers nothing');
});

test('the test reads the two notices together: the page\'s own, the host\'s, and what each outcome means', () => {
  const both = testReading({ page: null, host: 'Sent.', hostOk: true });
  assert.match(both, /^The page showed one itself\. Sent\. Two notices, then\./);
  assert.match(both, /Only the page's: the push service is not reaching this browser/);
  assert.match(both, /Neither: the browser is not showing notifications/);

  const refused = testReading({ page: null, host: 'The push service refused it: 403.', hostOk: false });
  assert.equal(refused, 'The page showed one itself. The push service refused it: 403.');

  const blocked = testReading({ page: 'permission denied', host: 'Sent.', hostOk: true });
  assert.equal(blocked, 'The page could not show one itself: permission denied. Sent. Allow notifications for this site in the browser\'s settings.');

  const dropped = testReading({ page: 'the browser took it and then listed no such notice: it dropped it', host: 'Sent.', hostOk: true });
  assert.match(dropped, /it dropped it\. Sent\. Look for it in the system's notification history/);
});
