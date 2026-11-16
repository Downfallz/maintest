import test from 'node:test';
import assert from 'node:assert/strict';
import { categoryFilters, categoryLabel, changeSaid, creatureLine, feedbackLine, feedbackPath, feedbackRows, muteLine, positionSides, sheetActions, sheetFacts, sheetWords, statusLine } from './admin-feedback.js';

const NOW = Date.parse('2026-11-16T12:00:00Z');

const report = {
  id: '20261116T113000000Z-abc123', kind: 'bug', category: 'rules', status: 'new', reason: null, at: '2026-11-16T11:30:00Z',
  text: 'The bleed did not tick after the stun.', expected: 'One damage in the upkeep.',
  additions: [{ at: '2026-11-16T11:40:00Z', text: 'It happened again.', expected: null }],
  page: '/battle', browser: 'Firefox/140', errors: [{ message: 'TypeError: x is undefined', where: 'battle.js:12' }],
  account: null, guest: true, senderGone: false, mute: null,
  context: { table: '20261116-110000-deadbeef', seat: 'player1', round: 3, subPhase: 'Speed', journalLength: 42, content: 'abcdef0123456789', rules: 'Rules 3v3', engine: '1.2.3', position: {
    allies: [{ name: 'Ravager', health: 7, maxHealth: 10, energy: 2, isStunned: true, conditions: [{ effect: { kind: 'Bleed' } }] }],
    enemies: [{ name: 'Warden', health: 0, maxHealth: 12, energy: 0, isStunned: false, conditions: [] }],
  } },
  session: '/session/20261116-110000-deadbeef', underWay: true,
};

test('the list\'s address leaves out every filter that is every one, and the first page', () => {
  assert.equal(feedbackPath({ status: '' }), '/api/admin/feedback');
  assert.equal(feedbackPath(), '/api/admin/feedback?status=open');
  assert.equal(feedbackPath({ kind: 'idea', status: 'new', category: 'battle', page: 2 }), '/api/admin/feedback?kind=idea&status=new&category=battle&page=2');
});

test('a row says the kind and category in the sheet\'s words, where it came from, and a new bug report stands out', () => {
  const rows = feedbackRows({ feedback: [
    { id: 'a', kind: 'bug', category: 'stuck', line: 'Nothing moves.', status: 'new', at: '2026-11-16T11:00:00Z', table: 't1', round: 4, additions: 1 },
    { id: 'b', kind: 'idea', category: 'lobby', line: 'Invite by name.', status: 'dismissed', reason: 'spam', at: '2026-11-15T12:00:00Z', additions: 0 },
  ] }, NOW);

  assert.deepEqual(rows.map(row => [row.kind, row.category, row.place, row.status, row.when, row.more, row.warn]), [
    ['Bug report', 'It is stuck or slow', 'Table t1, round 4', 'New', '1 h ago', '+1 more from the same player', true],
    ['Idea', 'The lobby and friends', 'Outside a game', 'Dismissed as spam', '1 d ago', '', false],
  ]);
  assert.equal(feedbackLine({ total: 21, page: 2, pages: 2 }), '21 pieces of feedback · page 2 of 2');
});

test('a status dismissed says its reason, and spam says so', () => {
  assert.equal(statusLine({ status: 'dismissed', reason: 'Works as the rules say.' }), 'Dismissed: Works as the rules say.');
  assert.equal(statusLine({ status: 'reproduced' }), 'Reproduced');
  assert.equal(categoryLabel('idea', 'rules'), 'The rules');
  assert.equal(categoryLabel('bug', 'rules'), 'The rules did something wrong');
  assert.deepEqual(categoryFilters().map(one => one.value), ['', 'rules', 'looks', 'stuck', 'other', 'battle', 'lobby']);
});

test('the sheet lists the player\'s words, what was added later, and what the page and the host attached', () => {
  assert.deepEqual(sheetWords(report, NOW).map(word => word.label), ['What happened', 'What they expected', 'Added 20 min ago']);
  assert.deepEqual(sheetWords({ ...report, kind: 'idea', additions: [] }, NOW).map(word => word.label), ['What they were trying to do', 'How they would like it']);

  const facts = Object.fromEntries(sheetFacts(report, NOW));
  assert.equal(facts.From, 'A guest, by their seat');
  assert.equal(facts.Where, 'Table 20261116-110000-deadbeef, round 3 · player1 · Tempo · 42 journal entries');
  assert.equal(facts['Played on'], 'Rules 3v3 · content abcdef012345 · engine 1.2.3');
  assert.equal(facts['Page error'], 'TypeError: x is undefined (battle.js:12)');
  assert.equal(facts.Muted, undefined);
});

test('the position is drawn as the reporter\'s side then the other, a creature as one line', () => {
  const sides = positionSides(report.context.position, 'player1');

  assert.deepEqual(sides, [
    { label: 'The reporter\'s side (player1)', creatures: ['Ravager · 7/10 health · 2 energy · stunned, Bleed'] },
    { label: 'The other side', creatures: ['Warden · down · 0 energy'] },
  ]);
  assert.deepEqual(positionSides(null, 'player1'), []);
  assert.equal(creatureLine({ name: 'Imp', health: { value: 3 }, maxHealth: { value: 5 }, energy: 1, conditions: [] }), 'Imp · 3/5 health · 1 energy');
});

test('the sheet offers what applies: reproduced and this is an idea for a bug, this is a bug for an idea, Diagnose… while its table is under way', () => {
  const bug = sheetActions(report);
  assert.deepEqual([bug.seen, bug.reproduced, bug.toIdea, bug.toBug, bug.diagnose, bug.mute, bug.unmute], [true, true, true, false, true, true, false]);

  const idea = sheetActions({ ...report, kind: 'idea', status: 'seen', underWay: false });
  assert.deepEqual([idea.seen, idea.reproduced, idea.toIdea, idea.toBug, idea.diagnose], [false, false, false, true, false]);

  const dismissed = sheetActions({ ...report, status: 'dismissed', mute: { until: '2026-12-16T12:00:00Z', why: 'spam' } });
  assert.deepEqual([dismissed.dismiss, dismissed.spam, dismissed.toIdea, dismissed.seen, dismissed.mute, dismissed.unmute], [false, false, false, true, false, true]);

  const gone = sheetActions({ ...report, senderGone: true });
  assert.deepEqual([gone.mute, gone.unmute], [false, false]);
});

test('a mute says how long it lasts and why, and a change says what the host answered', () => {
  assert.equal(muteLine({ until: '2026-12-16T12:00:00Z', why: 'spam', by: 'mark' }, NOW), 'No feedback from this player for 30 more days (three marked as spam in 30 days).');
  assert.equal(muteLine({ until: '2026-11-17T00:00:00Z', why: 'operator', by: 'mark' }, NOW), 'No feedback from this player for 1 more day (by mark).');
  assert.deepEqual(changeSaid({ ok: true, body: {} }, 'Marked seen.'), { refused: false, line: 'Marked seen.' });
  assert.deepEqual(changeSaid({ ok: false, status: 409, body: { message: 'This feedback is closed.' } }, 'x'), { refused: true, line: 'This feedback is closed.' });
});
