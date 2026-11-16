import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import {
  BUG_CATEGORIES,
  IDEA_PLACES,
  catchPageErrors,
  createPageErrors,
  feedbackBody,
  feedbackDraft,
  feedbackReady,
  attachedNote,
  mutedBy,
  refusalSentence,
  sentSentence,
} from './feedback-sheet.js';

const bug = (fields = {}) => ({ ...feedbackDraft(), kind: 'bug', category: 'rules', happened: 'The bleed did not tick after the stun.', ...fields });
const idea = (fields = {}) => ({ ...feedbackDraft(), kind: 'idea', where: 'lobby', problem: 'I wanted to find my friend’s game.', ...fields });

test('the sheet asks the host’s own categories and places, in the plan’s order', () => {
  assert.deepEqual(BUG_CATEGORIES.map(one => one.value), ['rules', 'looks', 'stuck', 'other']);
  assert.deepEqual(IDEA_PLACES.map(one => one.value), ['rules', 'battle', 'lobby', 'other']);
});

test('a new draft has chosen nothing and attaches the position', () => {
  const draft = feedbackDraft();
  assert.equal(draft.kind, null);
  assert.equal(draft.attach, true);
  assert.equal(feedbackReady(draft), false);
});

test('a bug report is ready once a category is chosen and what happened holds ten characters', () => {
  assert.equal(feedbackReady(bug()), true);
  assert.equal(feedbackReady(bug({ category: null })), false);
  assert.equal(feedbackReady(bug({ happened: '  too short ' })), false, 'counted without the spaces around it');
  assert.equal(feedbackReady(bug({ happened: 'x'.repeat(2001) })), false);
  assert.equal(feedbackReady(bug({ expected: 'x'.repeat(2001) })), false);
});

test('an idea is ready once a place is chosen and the problem holds ten characters', () => {
  assert.equal(feedbackReady(idea()), true);
  assert.equal(feedbackReady(idea({ where: null })), false);
  assert.equal(feedbackReady(idea({ problem: 'short' })), false);
});

test('a bug report posts its words, the page’s path and the errors, and attaches only in a game', () => {
  const errors = [{ message: 'TypeError: x is undefined', where: 'battle-build/live.js:12' }];
  assert.deepEqual(feedbackBody(bug({ expected: '  It should tick. ' }), { page: '/battle', errors, inGame: true }), {
    kind: 'bug', category: 'rules', happened: 'The bleed did not tick after the stun.', expected: 'It should tick.', attach: true, page: '/battle', errors,
  });
  assert.equal(feedbackBody(bug(), { page: '/lobby', errors: [], inGame: false }).attach, false);
  assert.equal(feedbackBody(bug({ attach: false }), { page: '/battle', errors: [], inGame: true }).attach, false);
  assert.equal('expected' in feedbackBody(bug({ expected: '   ' }), { page: '/battle', errors: [], inGame: true }), false);
});

test('an idea posts its words and the page, never a position nor the page’s errors', () => {
  const body = feedbackBody(idea({ wish: 'A search by name.' }), { page: '/lobby', errors: [{ message: 'boom', where: null }], inGame: true });
  assert.deepEqual(body, { kind: 'idea', where: 'lobby', problem: 'I wanted to find my friend’s game.', wish: 'A search by name.', page: '/lobby' });
});

test('the page keeps its last ten errors, newest last, in memory', () => {
  const errors = createPageErrors();
  for (let i = 1; i <= 12; i++) errors.add(`Error ${i}`, `page.js:${i}`);
  const kept = errors.list();
  assert.equal(kept.length, 10);
  assert.deepEqual(kept[0], { message: 'Error 3', where: 'page.js:3' });
  assert.deepEqual(kept.at(-1), { message: 'Error 12', where: 'page.js:12' });
  kept.pop();
  assert.equal(errors.list().length, 10, 'the list handed out is a copy');
});

test('an error’s message is cut to the host’s line, and a blank one is still an error', () => {
  const errors = createPageErrors();
  errors.add('x'.repeat(900), null);
  errors.add('', undefined);
  assert.equal(errors.list()[0].message.length, 500);
  assert.deepEqual(errors.list()[1], { message: 'Unknown error', where: null });
});

test('an error’s words lose any address’s query, which can carry a seat’s key', () => {
  const errors = createPageErrors();
  errors.add('Failed to load https://table.example/battle?seat=abc123#key=secret, then gave up', null);
  assert.equal(errors.list()[0].message, 'Failed to load https://table.example/battle then gave up');
});

test('the catcher keeps a script error with its file and line, and a rejection with its reason', () => {
  const listeners = {};
  const target = { addEventListener: (name, listener) => { listeners[name] = listener; } };
  const errors = createPageErrors();
  catchPageErrors(target, errors);
  listeners.error({ message: 'ReferenceError: board is not defined', filename: 'https://table.example/battle-build/live.js', lineno: 40, colno: 7 });
  listeners.unhandledrejection({ reason: new Error('The host did not answer') });
  listeners.unhandledrejection({ reason: 'plain words' });
  assert.deepEqual(errors.list(), [
    { message: 'ReferenceError: board is not defined', where: '/battle-build/live.js:40' },
    { message: 'The host did not answer', where: null },
    { message: 'plain words', where: null },
  ]);
});

test('nothing of the sheet or its errors is written to the device', () => {
  const source = readFileSync(new URL('./feedback-sheet.js', import.meta.url), 'utf8').replace(/\/\/.*$/gm, '');
  assert.doesNotMatch(source, /localStorage|sessionStorage|indexedDB|document\.cookie|caches\./);
});

test('each refusal reads as a sentence the player can act on', () => {
  assert.equal(refusalSentence(429, { error: 'Feedback.TooSoon', retryAfterSeconds: 75 }), 'You sent feedback a moment ago. Try again in 75 seconds: your words are kept here.');
  assert.equal(refusalSentence(429, { error: 'Feedback.TooSoon', retryAfterSeconds: 1 }), 'You sent feedback a moment ago. Try again in 1 second: your words are kept here.');
  assert.equal(refusalSentence(429, { error: 'Feedback.TooMany' }), 'You can send five a day. Try again tomorrow.');
  assert.equal(refusalSentence(429, { error: 'Feedback.HostFull' }), 'The game is taking no more feedback today. Try again tomorrow.');
  assert.equal(refusalSentence(403, { error: 'Feedback.Muted' }), 'Your feedback is paused for now.');
  assert.equal(refusalSentence(401, { error: 'Feedback.SignInRequired' }), 'Sign in to send feedback, or send it from a game you are playing.');
  assert.equal(refusalSentence(403, { error: 'Feedback.SeatRequired' }), 'Send your feedback from a game you are playing.');
  assert.equal(refusalSentence(403, { error: 'Feedback.NotYourSeat' }), 'This seat belongs to another player.');
  assert.equal(refusalSentence(400, { error: 'Feedback.Invalid', message: 'Write between 10 and 2000 characters.' }), 'Write between 10 and 2000 characters.');
  assert.equal(refusalSentence(503, { error: 'Feedback.Unavailable' }), 'Your feedback could not be kept just now. Try again in a few minutes.');
  assert.equal(refusalSentence(404, null), 'This table keeps no feedback.');
  assert.equal(refusalSentence(0, null), 'The host did not answer. Your words are kept here: try again.');
  assert.equal(refusalSentence(500, null), 'The host did not answer. Your words are kept here: try again.');
});

test('only the mute hides the entry', () => {
  assert.equal(mutedBy(403, { error: 'Feedback.Muted' }), true);
  assert.equal(mutedBy(403, { error: 'Feedback.NotYourSeat' }), false);
  assert.equal(mutedBy(429, { error: 'Feedback.TooMany' }), false);
});

test('a kept piece of feedback thanks the player, and a merged one says where it went', () => {
  assert.equal(sentSentence('bug', { merged: false }), 'Thank you. Your report reached the game’s operator.');
  assert.equal(sentSentence('idea', { merged: false }), 'Thank you. Your idea reached the game’s operator.');
  assert.equal(sentSentence('bug', { merged: true }, { inGame: true }), 'Added to the report you sent from this round.');
  assert.equal(sentSentence('bug', { merged: true }), 'Added to the report you sent a moment ago.', 'outside a game, only the same words sent again merge');
  assert.equal(sentSentence('idea', { merged: true }), 'Added to the idea you sent within the hour.');
});

test('the sheet says what the host adds, so a short report is enough', () => {
  assert.equal(attachedNote('bug', { inGame: true, attach: true }), 'We add the game, the round, this position, the page and its last errors. A few words are enough.');
  assert.equal(attachedNote('bug', { inGame: true, attach: false }), 'We add the game, the round, the page and its last errors. A few words are enough.');
  assert.equal(attachedNote('bug', { inGame: false, attach: true }), 'We add the page and its last errors. A few words are enough.');
  assert.equal(attachedNote('idea', { inGame: true }), 'We add the page, and the game and round you send it from.');
  assert.equal(attachedNote('idea', { inGame: false }), 'We add the page you send it from.');
});
