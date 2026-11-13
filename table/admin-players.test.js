import test from 'node:test';
import assert from 'node:assert/strict';

import { PLAYER_SORTS, actionSaid, ago, deleteConfirmed, gameLine, playerRows, playersLine, playersPath, ratingText } from './admin-players.js';

const NOW = Date.parse('2026-11-13T20:00:00Z');

test('a page of players is asked for by its search, sort and page, each left out at its default', () => {
  assert.equal(playersPath(), '/api/admin/players');
  assert.equal(playersPath({ q: '  mar ', sort: 'games', page: 3 }), '/api/admin/players?q=mar&sort=games&page=3');
  assert.equal(playersPath({ q: '', sort: 'seen', page: 1 }), '/api/admin/players');
  assert.equal(playersPath({ q: 'a&b' }), '/api/admin/players?q=a%26b');
});

test('every sort the page offers is one the host reads', () => {
  assert.deepEqual(PLAYER_SORTS.map(sort => sort.value), ['seen', 'signed-in', 'created', 'name', 'games', 'rating']);
});

test('a moment reads in the largest unit that keeps it short, and one never kept reads never', () => {
  assert.equal(ago(null, NOW), 'never');
  assert.equal(ago('not a date', NOW), 'never');
  assert.equal(ago('2026-11-13T19:59:40Z', NOW), 'just now');
  assert.equal(ago('2026-11-13T19:15:00Z', NOW), '45 min ago');
  assert.equal(ago('2026-11-13T15:00:00Z', NOW), '5 h ago');
  assert.equal(ago('2026-11-10T20:00:00Z', NOW), '3 d ago');
  assert.equal(ago('2026-06-01T20:00:00Z', NOW), '2026-06-01');
});

test('a rating is its number, marked while provisional, and a dash before a ranked game', () => {
  assert.equal(ratingText({ rating: 1612, provisional: false }), '1612');
  assert.equal(ratingText({ rating: 1540, provisional: true }), '1540?');
  assert.equal(ratingText({ rating: null }), '—');
});

test('a player row says when they signed in and were seen, and their games split by kind', () => {
  const [row] = playerRows({
    players: [{ id: 'acct_1', username: 'Mark', createdAt: '2026-11-01T10:00:00Z', lastSignIn: null, lastSeen: '2026-11-13T18:00:00Z',
      games: 12, ranked: 3, againstPlayers: 5, againstBots: 7, wins: 6, rating: null }],
  }, NOW);

  assert.deepEqual(row, {
    id: 'acct_1', username: 'Mark', created: '2026-11-01', signedIn: 'never', seen: '2 h ago',
    games: 12, split: '3 ranked · 5 vs players · 7 vs bots', wins: 6, rating: '—',
  });
});

test('the pager says how many players, how many the search kept, and which page', () => {
  assert.equal(playersLine({ total: 45, matching: 45, page: 2, pages: 3 }), '45 players · page 2 of 3');
  assert.equal(playersLine({ total: 45, matching: 2, page: 1, pages: 1 }), '2 of 45 players · page 1 of 1');
  assert.equal(playersLine({ total: 1, matching: 1, page: 1, pages: 1 }), '1 player · page 1 of 1');
});

test('deleting is confirmed by the account name typed, whatever its case, and by nothing else', () => {
  assert.equal(deleteConfirmed(' mark ', 'Mark'), true);
  assert.equal(deleteConfirmed('Marko', 'Mark'), false);
  assert.equal(deleteConfirmed('', ''), false);
});

test('an action answered says what it did, and a refusal keeps the host sentence', () => {
  assert.deepEqual(actionSaid('rename', { ok: true, body: { account: { username: 'Marko' } } }, 'Mark'), { refused: false, line: 'Renamed to Marko.' });
  assert.match(actionSaid('sign-out', { ok: true, body: {} }, 'Mark').line, /^Mark is signed out everywhere/);
  assert.deepEqual(actionSaid('delete', { ok: true, body: {} }, 'Mark'), { refused: false, line: "Mark's account is deleted." });
  assert.deepEqual(actionSaid('rename', { ok: false, status: 409, body: { message: 'Another player already has that name.' } }, 'Mark'),
    { refused: true, line: 'Another player already has that name.' });
});

test('a finished game reads as its outcome, the opponent, the format and when', () => {
  assert.equal(gameLine({ outcome: 'loss', opponent: 'Greedy', format: '1v1', ranked: false, finishedAt: '2026-11-13T17:00:00Z' }, NOW),
    'Lost against Greedy · 1v1 · unranked · 3 h ago');
  assert.equal(gameLine({ outcome: 'played', opponent: 'themselves', format: '3v3', ranked: false, finishedAt: null }, NOW),
    'Played both seats · 3v3 · unranked · never');
});
