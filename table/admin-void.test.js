import test from 'node:test';
import assert from 'node:assert/strict';

import { VOID_CATEGORIES, voidAsked, voidLabel, voidSaid, voidWarning } from './admin-void.js';

test('the categories are the ones the host keeps', () => {
  assert.deepEqual(VOID_CATEGORIES.map(one => one.value), ['stuck', 'engine', 'abuse', 'mistake', 'other']);
  assert.equal(voidLabel('abuse'), 'A player abused the game');
  assert.equal(voidLabel('unknown'), 'Voided');
});

test('a void asks for a category and an explanation of ten to five hundred characters', () => {
  assert.deepEqual(voidAsked('', 'The match stopped answering.'), { problem: 'Choose why the match is voided.' });
  assert.match(voidAsked('stuck', ' too short ').problem, /at least 10/);
  assert.match(voidAsked('stuck', 'x'.repeat(501)).problem, /500/);
  assert.deepEqual(voidAsked('stuck', '  The match stopped answering.  '), { body: { category: 'stuck', explanation: 'The match stopped answering.' } });
});

test('what a void does is said before it is done, for a finished match and one in progress', () => {
  assert.match(voidWarning({ over: true }), /result is withdrawn/);
  assert.match(voidWarning({ over: false }), /ends now, with no result/);
  assert.match(voidWarning({ over: false }), /A void is final\.$/);
});

test('a void answered says whether a result was withdrawn, and a refusal keeps the host sentence', () => {
  assert.deepEqual(voidSaid({ ok: true, body: { withdrawn: true } }, 't1'), { refused: false, line: 't1 voided; its result is withdrawn.' });
  assert.deepEqual(voidSaid({ ok: true, body: { withdrawn: false } }, 't1'), { refused: false, line: 't1 voided.' });
  assert.deepEqual(voidSaid({ ok: false, status: 409, body: { message: 'A void is final.' } }, 't1'), { refused: true, line: 'A void is final.' });
});
