// What the header shows a guest (ADR 0207, docs/art/journeys/doors.md, UX15): what the host lets it reach, and
// nothing it would refuse.
import test from 'node:test';
import assert from 'node:assert/strict';
import { headerShows, memberOf } from './navigation.js';

const walker = { id: 'acct_walker', username: 'Walker', guest: false };
const guest = { id: 'acct_guest', username: 'Guest-7KQ2', guest: true };

test('a guest is nobody\'s member: the lists, the bell, devices and ways to sign in read nothing for it', () => {
  assert.equal(memberOf(guest), null);
  assert.equal(memberOf(walker), walker);
  assert.equal(memberOf(null), null);
});

test('a member\'s header shows what a member reaches', () => {
  const shows = headerShows(walker, 'signin');

  assert.deepEqual(shows, { home: 'member', guest: false, join: true, friends: true, menuSettings: false, notificationsTab: true, accountTab: true, leaderboard: true, stats: true, learn: true, feedback: true });
});

test('a guest\'s header shows the tutorials and its settings, and nothing the host refuses a guest', () => {
  const shows = headerShows(guest, 'signin');

  assert.deepEqual(shows, { home: 'member', guest: true, join: false, friends: false, menuSettings: false, notificationsTab: false, accountTab: false, leaderboard: false, stats: false, learn: true, feedback: false });
});

test('a visitor signed out keeps the header it had', () => {
  assert.deepEqual(headerShows(null, 'signin'), { home: 'visitor', guest: false, join: false, friends: false, menuSettings: true, notificationsTab: false, accountTab: false, leaderboard: true, stats: false, learn: false, feedback: false });
  assert.equal(headerShows(null, 'off').home, 'local');
  assert.equal(headerShows(null, 'off').join, true);
});
