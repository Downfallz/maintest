import test from 'node:test';
import assert from 'node:assert/strict';
import { hostLinks } from './browser/host-log.js';
const session = '  Session sample into a temporary directory\n';
const first = '  player1: a person — joins at http://localhost:1234/j/first\n';
const second = '  player2: a person — joins at http://localhost:1234/j/second\n';
const admin = '  Admin: http://localhost:1234/admin?token=example\n';
test('the first invitation cannot finish startup before the admin announcement', () => {
  assert.equal(hostLinks(session + first), null);
  assert.equal(hostLinks(session + first + '  Admin: http://local'), null);
  assert.equal(hostLinks(session + first + admin.trimEnd()), null);
});
test('every stdout split gives the same complete invitations and recorded session', () => {
  const log = session + first + second + admin;
  for (let cut = 0; cut < log.length; cut++) assert.equal(hostLinks(log.slice(0, cut)), null);
  assert.deepEqual(hostLinks(log), { join: 'http://localhost:1234/j/first',
    joins: ['http://localhost:1234/j/first', 'http://localhost:1234/j/second'],
    admin: 'http://localhost:1234/admin?token=example', session: 'sample' });
});
test('CRLF announcements and a non-recording session use the same readiness rule', () => {
  assert.equal(hostLinks(('  Session sample\n' + first + admin).replaceAll('\n', '\r\n')).session, 'sample');
});
test('missing session or invitations cannot be mistaken for a complete host', () => {
  assert.equal(hostLinks(first + admin), null); assert.equal(hostLinks(session + admin), null);
});
