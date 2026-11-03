import test from 'node:test';
import assert from 'node:assert/strict';
import { ratingText, recordText, youSummary } from './leaderboard.js';

test('a record reads wins, losses and draws, and a rating carries how sure it is', () => {
  assert.equal(recordText({ wins: 7, losses: 2, draws: 1 }), '7–2–1');
  assert.equal(ratingText({ rating: 1642, deviation: 58 }), '1642 ±58');
});

test('the signed-in panel says where a player stands, or how to get a place', () => {
  assert.equal(youSummary(null).kind, 'signed-out');
  assert.match(youSummary({ username: 'MarkF', standing: null, bots: [] }).text, /no ranked game yet/);
  const ranked = youSummary({ username: 'MarkF', standing: { rank: 2, provisional: false, wins: 3, losses: 1, draws: 0, games: 4 } });
  assert.equal(ranked.text, 'MarkF · #2 · 3–1–0 in 4 games');
  assert.match(youSummary({ username: 'MarkF', standing: { rank: 5, provisional: true, wins: 1, losses: 0, draws: 0, games: 1 } }).text, /provisional · 1–0–0 in 1 game$/);
});
