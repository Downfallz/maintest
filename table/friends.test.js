import { test } from 'node:test';
import assert from 'node:assert/strict';
import { askedText, friendSections, REFRESH_EVERY } from './friends.js';

const mark = { id: 'acct_mark', username: 'MarkF' };
const bastien = { id: 'acct_bastien', username: 'bastien' };

test('the panel shows the requests first, then the friends, then who has not answered', () => {
  const sections = friendSections({ friends: [mark], incoming: [bastien], outgoing: [] });

  assert.equal(sections.incoming.shown, true);
  assert.equal(sections.incoming.title, 'One player asked to be your friend');
  assert.deepEqual(sections.friends.items, [mark]);
  assert.equal(sections.outgoing.shown, false, 'nothing waiting, nothing said');
});

test('an empty list says what to do, and several requests are counted', () => {
  const none = friendSections({ friends: [], incoming: [], outgoing: [] });
  assert.equal(none.incoming.shown, false);
  assert.match(none.friends.empty, /name on their cards/);
  assert.equal(friendSections(null).friends.items.length, 0, 'before the host answered');

  const two = friendSections({ friends: [], incoming: [mark, bastien], outgoing: [mark] });
  assert.equal(two.incoming.title, '2 players asked to be your friends');
  assert.equal(two.outgoing.shown, true);
});

test('asking says whether it was a request or an acceptance', () => {
  assert.equal(askedText({ accepted: false, other: bastien }), 'Asked. bastien will see your request.');
  assert.equal(askedText({ accepted: true, other: bastien }), 'bastien had already asked you: you are friends now.');
  assert.equal(askedText(undefined), '');
});

test('the list is read again once a half-minute at most', () => {
  assert.equal(REFRESH_EVERY, 30000);
});
