import { test } from 'node:test';
import assert from 'node:assert/strict';
import { openPractice, practiceBand, practiceOf } from './battle-practice.js';

const answer = (status, body) => ({ status, ok: status >= 200 && status < 300, json: async () => body });

test('an address names a scenario by its id, and nothing else passes for one', () => {
  assert.equal(practiceOf('?scenario=sc-20261114-041204-d11061d3'), 'sc-20261114-041204-d11061d3');
  assert.equal(practiceOf('?scenario=../admin'), null);
  assert.equal(practiceOf('?player1=abc'), null);
  assert.equal(practiceOf(''), null);
});

test('the band names the practice, where it starts, and that the recorded match is untouched', () => {
  assert.deepEqual(practiceBand({ title: 'Bleed after a stun', round: 4, goal: 'Keep Creature 2 alive' }),
    { title: 'Bleed after a stun', detail: 'From the top of round 4 · the recorded match is unchanged', goal: 'Keep Creature 2 alive' });
  assert.equal(practiceBand(null).title, 'Practice');
});

test('opening resumes the account\'s practice when it has one', async () => {
  const asked = [];
  const seat = { seat: 'player2', token: 't', table: { id: 'practice-sc-1' }, practice: { round: 4 } };
  const opened = await openPractice('sc-1', async (path, options) => { asked.push([path, options?.method ?? 'GET']); return answer(200, seat); });

  assert.deepEqual(opened, seat);
  assert.deepEqual(asked, [['/api/scenarios/sc-1/practice', 'GET']]);
});

test('opening starts one from the cut when the account has none, and restarting always does', async () => {
  const asked = [];
  const request = async (path, options) => { asked.push(options?.method ?? 'GET'); return options?.method ? answer(200, { token: 'new' }) : answer(404, {}); };

  await openPractice('sc-1', request);
  await openPractice('sc-1', request, { fresh: true });

  assert.deepEqual(asked, ['GET', 'POST', 'POST']);
});

test('a refusal carries the host\'s words and status, a signed-out player\'s 401 among them', async () => {
  await assert.rejects(openPractice('sc-1', async () => answer(401, { message: 'Sign in to practise a scenario.' })),
    error => error.status === 401 && error.message === 'Sign in to practise a scenario.');
});
