import test from 'node:test';
import assert from 'node:assert/strict';
import { noticeCount, registrationLine, savedLine, settingsSaid } from './admin-settings.js';

const now = Date.parse('2026-11-14T03:00:00Z');

test('registration open and closed each say what a first sign-in meets', () => {
  assert.match(registrationLine(true), /^Open: a first sign-in/);
  assert.match(registrationLine(false), /^Closed: .*Existing players sign in as before\.$/);
});

test('the notice is counted as the host keeps it, one line, and marked over the limit', () => {
  assert.deepEqual(noticeCount('  Maintenance\n at  22:00 ', 200), { line: '20 / 200', over: false });
  assert.deepEqual(noticeCount('a'.repeat(201), 200), { line: '201 / 200', over: true });
  assert.deepEqual(noticeCount(undefined, 200), { line: '0 / 200', over: false });
});

test('the settings say who saved them and when, or that the defaults hold', () => {
  assert.equal(savedLine({ updatedAt: '2026-11-14T01:00:00Z', updatedBy: 'mark' }, now), 'Last saved 2 h ago by mark.');
  assert.equal(savedLine({ registrationOpen: true }, now), 'Never saved: the defaults hold, registration open and no notice.');
});

test('a save says what now holds, and a refusal keeps the host\'s own words', () => {
  assert.deepEqual(settingsSaid({ ok: true, body: { settings: { registrationOpen: false, notice: 'Back at 23:00.' } } }),
    { refused: false, line: 'Saved. Registration is closed, and the notice shows under every page\'s header.' });
  assert.deepEqual(settingsSaid({ ok: true, body: { settings: { registrationOpen: true, notice: null } } }),
    { refused: false, line: 'Saved. Registration is open, and no notice shows.' });
  assert.deepEqual(settingsSaid({ ok: false, status: 400, body: { message: 'A notice is one line of at most 200 characters.' } }),
    { refused: true, line: 'A notice is one line of at most 200 characters.' });
  assert.deepEqual(settingsSaid({ ok: false, status: 503 }), { refused: true, line: 'The host refused it (503).' });
});
