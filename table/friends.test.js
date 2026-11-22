import { test } from 'node:test';
import assert from 'node:assert/strict';
import { arrivals, askedText, askFilled, friendSections, initialOf } from './friends.js';

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
  assert.match(none.friends.empty, /Recent opponents/);
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

test('a player is their initial, as the header shows the player\'s own', () => {
  assert.equal(initialOf('rook'), 'R');
  assert.equal(initialOf(''), '?');
});

test('Ask is the window\'s filled action only while it holds no friend and no request', () => {
  assert.equal(askFilled({ friends: [], incoming: [], outgoing: [] }), true);
  assert.equal(askFilled({ friends: [], incoming: [], outgoing: [mark] }), true, 'a request sent is not a friend yet');
  assert.equal(askFilled({ friends: [mark], incoming: [], outgoing: [] }), false);
  assert.equal(askFilled({ friends: [], incoming: [bastien], outgoing: [] }), false, 'Accept answers a request');
});

test('only a row the window had not drawn arrives, never on the first draw', () => {
  assert.deepEqual([...arrivals(null, ['friends:a', 'friends:b'])], [], 'the first draw arrives all at once, without a beat');
  assert.deepEqual([...arrivals(new Set(['friends:a']), ['friends:a', 'friends:b'])], ['friends:b']);
  assert.deepEqual([...arrivals(new Set(['outgoing:b']), ['friends:b'])], ['friends:b'], 'a request answered arrives among the friends');
  assert.deepEqual([...arrivals(new Set(['friends:a', 'friends:b']), ['friends:a'])], []);
});

test('the Friends window wears its head on the player\'s pages, each declared for it, and not on the admin page or the classic table', async () => {
  const { HEADER_SCENE_PAGES } = await import('./navigation.js');
  const { PAGE_ART } = await import('./page-art.js');
  assert.deepEqual(Object.keys(HEADER_SCENE_PAGES).sort(), ['battle', 'leaderboard', 'lobby', 'rules', 'stats']);
  for (const page of Object.values(HEADER_SCENE_PAGES)) assert.equal(PAGE_ART['predator-and-deathstalker'].pages[page], 'lazy', page);
});
