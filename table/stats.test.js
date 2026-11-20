import test from 'node:test';
import assert from 'node:assert/strict';
import { leanedOn, percent, roundsText, streakText, tallyText, teamsNote } from './stats.js';

test('a tally reads won, lost and drawn, and a share is a whole percent or a dash', () => {
  assert.equal(tallyText({ won: 7, lost: 2, drawn: 1 }), '7–2–1');
  assert.equal(percent(2, 3), '67%');
  assert.equal(percent(0, 0), '–');
});

test('the streak says how many of the same outcome in a row, and nothing before a first game', () => {
  assert.equal(streakText(null), 'No game yet');
  assert.equal(streakText({ outcome: 'won', length: 3 }), '3 wins in a row');
  assert.equal(streakText({ outcome: 'lost', length: 1 }), '1 loss');
  assert.equal(streakText({ outcome: 'drawn', length: 2 }), '2 draws in a row');
});

test('what was played says which ranked games it counts when older ones kept no teams', () => {
  assert.equal(teamsNote({ ranked: { games: 0 }, withTeams: 0 }), '');
  assert.equal(teamsNote({ ranked: { games: 4 }, withTeams: 4 }), 'From your 4 games');
  assert.equal(teamsNote({ ranked: { games: 1 }, withTeams: 1 }), 'From your 1 game');
  assert.match(teamsNote({ ranked: { games: 9 }, withTeams: 2 }), /^From 2 of your 9 games: older games/);
});

test('an average length reads one round, or rounds', () => {
  assert.equal(roundsText(null), '–');
  assert.equal(roundsText(1), '1 round');
  assert.equal(roundsText(2.5), '2.5 rounds');
});

test('what a player leans on is the host\'s first row of each: the page chooses nothing', () => {
  const brute = { id: 'tier:brute:v1', name: 'Brute', artwork: 'brute-base', bought: 2, won: 0 };
  const brace = { id: 'spell:brace:v1', name: 'Brace', casts: 2, games: 1, won: 0 };
  const leaned = leanedOn({ mutations: [brute, { id: 'x', bought: 1 }], spells: [brace] });
  assert.equal(leaned.mutation, brute);
  assert.equal(leaned.picture, 'mutation-brute-base');
  assert.equal(leaned.spell, brace);
  assert.equal(leanedOn({ mutations: [{ ...brute, artwork: null }], spells: [] }).picture, null, 'a capstone draws no painting');
  assert.deepEqual(leanedOn({ mutations: [], spells: [] }), { mutation: null, picture: null, spell: null });
});
