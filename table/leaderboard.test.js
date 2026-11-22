import test from 'node:test';
import assert from 'node:assert/strict';
import { recordWords, statusText, placeMove, placeText, ratingBand, recordParts, ratingText, recordText, youSummary } from './leaderboard.js';

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

test('the place is the host\'s rank, with Provisional beside it when the host says so', () => {
  assert.equal(placeText({ rank: 2, provisional: true }), '#2 · Provisional');
  assert.equal(placeText({ rank: 1, provisional: false }), '#1');
});

test('a rank that moved is said from what this browser last showed, and nothing when nothing is remembered', () => {
  const now = { rank: 2, rating: 1338, provisional: true };
  assert.equal(placeMove(undefined, now), null, 'nothing remembered, nothing said');
  assert.equal(placeMove({ standing: null }, now).text, 'New on the ladder.');
  assert.equal(placeMove({ standing: { rank: 2, rating: 1338 } }, now), null, 'the same, no line');
  assert.equal(placeMove({ standing: { rank: 2, rating: 1400 } }, now).text, 'Down 62 since you last looked.');
  assert.equal(placeMove({ standing: { rank: 3, rating: 1300 } }, now).text, 'Up 38 since you last looked. #3 → #2.');
  assert.equal(placeMove({ standing: { rank: 2, rating: 1338 } }, null), null, 'off the ladder says nothing of a move');
});

test('the rating is drawn among the ladder\'s, lowest to highest, the player marked', () => {
  const band = ratingBand([{ rating: 1662, you: false }, { rating: 1338, deviation: 290, you: true }]);
  assert.equal(band.low, 1338);
  assert.equal(band.high, 1662);
  assert.deepEqual(band.ticks.map(tick => [tick.at, tick.you]), [[100, false], [0, true]]);
  assert.equal(ratingBand([{ rating: 1500, you: true }]).ticks[0].at, 50, 'one player stands in the middle');
  assert.equal(ratingBand([]), null);
});

test('a record is drawn as three parts as long as the wins, the losses and the draws', () => {
  assert.deepEqual(recordParts({ wins: 0, losses: 1, draws: 0 }), [{ part: 'won', share: 0 }, { part: 'lost', share: 100 }, { part: 'drawn', share: 0 }]);
  assert.equal(recordParts({ wins: 0, losses: 0, draws: 0 }), null);
});

test('the record names its parts in words, and a rating that moves while the page is open says by how much', () => {
  assert.equal(recordWords({ wins: 0, losses: 1, draws: 0 }), '0 won, 1 lost, 0 drawn');
  assert.equal(statusText({ rating: 1400, rank: 2 }, { rating: 1338, rank: 2 }), 'Your rating is now 1338, down 62.');
  assert.equal(statusText({ rating: 1300, rank: 2 }, { rating: 1338, rank: 2 }), 'Your rating is now 1338, up 38.');
  assert.equal(statusText({ rating: 1338, rank: 3 }, { rating: 1338, rank: 2 }), 'Your place is now #2, from #3.', 'others\' games moved the place alone');
  assert.equal(statusText(null, { rating: 1338, rank: 2 }), 'Your rating is now 1338.');
});

// How the ladder counts is the rulebook's (ADR 0124, rules/): the page links its section and states none of it (#994).
test('the page links the rulebook\'s ladder section instead of restating it', async () => {
  const { readFileSync } = await import('node:fs');
  const page = readFileSync(new URL('./leaderboard.html', import.meta.url), 'utf8');
  const book = JSON.parse(readFileSync(new URL('./rules.json', import.meta.url), 'utf8'));
  const ids = book.parts.flatMap(part => part.sections.map(section => section.id));
  const [, section] = /<a [^>]*href="\/rules#([a-z-]+)"[^>]*>[^<]*ladder/i.exec(page) ?? [];
  assert.ok(section && ids.includes(section), `a link to a section of the book: ${section}`);
  assert.doesNotMatch(page, /surprising the result|do not count/);
  // The one number the book cannot draw stays the host's, beside the link.
  assert.match(page, /Provisional above ±<span id="provisional-above">/);
});
