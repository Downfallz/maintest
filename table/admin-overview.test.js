import test from 'node:test';
import assert from 'node:assert/strict';
import { overviewCards } from './admin-overview.js';

const NOW = Date.parse('2026-11-14T03:00:00Z');

const full = {
  players: { total: 42, seenThisWeek: 17, newThisWeek: 3, suspended: 1 },
  games: { inProgress: { againstPlayers: 4, againstBots: 6 }, finished: { lastDay: 5, lastWeek: 31, voidedThisWeek: 1 } },
  waiting: { openSeats: 2, invitations: 1, endingSoon: 3 },
  jobs: { lastRun: { id: 'r', started: '2026-11-14T02:00:00Z', outcome: 'completed' }, turns: { waiting: 2, failed: 0 } },
  registration: { open: true },
};

test('every source the host has is a card, in reading order, each linking to the tab that explains it', () => {
  const cards = overviewCards(full, NOW);

  assert.deepEqual(cards.map(card => [card.id, card.tab, card.number]), [
    ['players', 'players', '42'], ['in-progress', 'tables', '10'], ['finished', 'sessions', '5'], ['waiting', 'tables', '2'],
    ['tending', 'jobs', '1 h ago'], ['turns', 'jobs', '2'], ['registration', 'settings', 'Open'],
  ]);
  assert.equal(cards[0].detail, '17 seen this week · 3 new · 1 suspended');
  assert.equal(cards[1].detail, '4 between players · 6 against bots');
  assert.equal(cards[2].detail, '31 in 7 days · 1 voided');
  assert.equal(cards[3].detail, '1 invitation unanswered · 3 games out of time within a day');
  assert.ok(cards.every(card => !card.warn));
});

test('a failed run, turns given up on and registration closed are drawn as needing the operator', () => {
  const cards = overviewCards({ ...full, jobs: { lastRun: { started: '2026-11-14T02:00:00Z', outcome: 'failed' }, turns: { waiting: 0, failed: 2 } }, registration: { open: false } }, NOW);

  assert.deepEqual(cards.filter(card => card.warn).map(card => card.id), ['tending', 'turns', 'registration']);
  assert.equal(cards.find(card => card.id === 'turns').detail, '2 turns given up on');
  assert.equal(cards.find(card => card.id === 'registration').number, 'Closed');
});

test('a source this host lacks has no card, one that did not answer keeps its card with a dash, and no run yet says so', () => {
  const cards = overviewCards({ players: null, games: { inProgress: { againstPlayers: 0, againstBots: 1 }, finished: { unavailable: true } },
    waiting: { unavailable: true }, jobs: { lastRun: { id: null, started: null, outcome: 'none' }, turns: null }, registration: null }, NOW);

  assert.deepEqual(cards.map(card => [card.id, card.number]), [['in-progress', '1'], ['finished', '—'], ['waiting', '—'], ['tending', '—']]);
  assert.equal(cards.find(card => card.id === 'tending').detail, 'No run kept yet.');
  assert.equal(cards.find(card => card.id === 'finished').detail, 'The host could not read this just now.');
});
