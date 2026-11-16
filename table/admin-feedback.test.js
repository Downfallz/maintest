import test from 'node:test';
import assert from 'node:assert/strict';
import { askedLine, categoryFilters, categoryLabel, changeSaid, creatureLine, feedbackLine, feedbackPath, feedbackRows, mergeChoices, muteLine, planPrompt, positionSides, sheetActions, sheetFacts, sheetWords, statusLine, STATUS_FILTERS } from './admin-feedback.js';

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
  session: '/session/20261116-110000-deadbeef', underWay: true, actions: ['seen', 'reproduced', 'dismiss', 'spam', 'toIdea', 'diagnose', 'mute'],
};

test('the list\'s address leaves out every filter that is every one, and the first page', () => {
  assert.equal(feedbackPath({ status: '' }), '/api/admin/feedback');
  assert.equal(feedbackPath(), '/api/admin/feedback?status=open');
  assert.equal(feedbackPath({ kind: 'idea', status: 'new', category: 'battle', page: 2 }), '/api/admin/feedback?kind=idea&status=new&category=battle&page=2');
});

test('a row says the kind and category in the sheet\'s words, where it came from, and a new bug report stands out', () => {
  const rows = feedbackRows({ feedback: [
    { id: 'a', kind: 'bug', category: 'stuck', line: 'Nothing moves.', status: 'new', at: '2026-11-16T11:00:00Z', table: 't1', round: 4, additions: 1 },
    { id: 'b', kind: 'idea', category: 'lobby', line: 'Invite by name.', status: 'dismissed', reason: 'spam', spam: true, at: '2026-11-15T12:00:00Z', additions: 0 },
  ] }, NOW);

  assert.deepEqual(rows.map(row => [row.kind, row.category, row.place, row.status, row.when, row.more, row.warn]), [
    ['Bug report', 'It is stuck or slow', 'Table t1, round 4', 'New', '1 h ago', '1 added later', true],
    ['Idea', 'The lobby and friends', 'Outside a game', 'Dismissed as spam', '1 d ago', '', false],
  ]);
  assert.equal(feedbackLine({ total: 21, page: 2, pages: 2 }), '21 pieces of feedback · page 2 of 2');
});

test('a status dismissed says its reason, and spam says so', () => {
  assert.equal(statusLine({ status: 'dismissed', reason: 'Works as the rules say.' }), 'Dismissed: Works as the rules say.');
  assert.equal(statusLine({ status: 'reproduced' }), 'Reproduced');
  assert.equal(statusLine({ status: 'dismissed', reason: 'spam', spam: false }), 'Dismissed: spam', 'only the host says what is a spam mark');
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
  assert.equal(Object.fromEntries(sheetFacts({ ...report, context: { ...report.context, journalLength: 1 } }, NOW)).Where.endsWith('1 journal entry'), true);
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

test('the sheet shows the buttons the host offers, and none it does not', () => {
  const bug = sheetActions(report);
  assert.deepEqual([bug.seen, bug.reproduced, bug.toIdea, bug.toBug, bug.diagnose, bug.mute, bug.unmute], [true, true, true, false, true, true, false]);

  assert.deepEqual(Object.values(sheetActions({ ...report, actions: ['bogus'] })).filter(Boolean), []);
  assert.deepEqual(Object.values(sheetActions(null)).filter(Boolean), [], 'a sheet still being read offers nothing');
});

test('a mute says how long it lasts and why, and a change says what the host answered', () => {
  assert.equal(muteLine({ daysLeft: 30, why: 'spam', by: 'mark' }), 'This player can send no feedback for 30 more days (three dismissed as spam in 30 days).');
  assert.equal(muteLine({ daysLeft: 1, why: 'operator', by: 'mark' }), 'This player can send no feedback for 1 more day (by mark).');
  assert.deepEqual(changeSaid({ ok: true, body: {} }, 'Marked seen.'), { refused: false, line: 'Marked seen.' });
  assert.deepEqual(changeSaid({ ok: false, status: 409, body: { message: 'This feedback is closed.' } }, 'x'), { refused: true, line: 'This feedback is closed.' });
});

const idea = {
  id: '20261116T100000000Z-aaa111', kind: 'idea', category: 'battle', status: 'seen', reason: null, plan: null, at: '2026-11-14T10:00:00Z',
  text: 'I wanted to see the last round again but could not.', expected: 'A replay button.', additions: [],
  account: 'acct_1', guest: false, senderGone: false, mute: null, context: null, askedBy: 3, since: '2026-11-14T10:00:00Z',
  asks: [
    { id: 'b', at: '2026-11-15T12:00:00Z', text: 'Let me watch a round again.', expected: null, additions: [], account: null, guest: true, senderGone: false },
    { id: 'c', at: '2026-11-16T11:00:00Z', text: 'A replay, please.', expected: 'From the recap.', additions: [{ at: '2026-11-16T11:30:00Z', text: 'Or from the menu.', expected: null }], account: null, guest: false, senderGone: true },
  ],
  mergedInto: null, actions: ['planned', 'shipped', 'dismiss', 'spam', 'declined', 'merge', 'prompt'],
};

test('an idea says how many asked and since when, in its row and on its sheet', () => {
  assert.equal(askedLine(3, '2026-11-14T10:00:00Z'), 'Asked by 3 players since 2026-11-14');
  assert.equal(askedLine(1, '2026-11-14T10:00:00Z'), 'Asked by 1 player since 2026-11-14');
  const [row] = feedbackRows({ feedback: [{ id: 'a', kind: 'idea', category: 'battle', line: 'Replays.', status: 'planned', plan: 'docs/tabletop/replay-plan.md', at: '2026-11-16T11:00:00Z', askedBy: 3, additions: 0 }] }, NOW);
  assert.equal(row.asked, 'asked by 3');
  assert.equal(row.status, 'Planned: docs/tabletop/replay-plan.md');

  const facts = Object.fromEntries(sheetFacts(idea, NOW));
  assert.equal(facts['Asked by'], '3 players since 2026-11-14');
  assert.equal(facts['Merged into'], undefined);
  assert.equal(Object.fromEntries(sheetFacts({ ...idea, mergedInto: { id: 'z', line: 'Replays of a round.', status: 'planned' } }, NOW))['Merged into'], 'Replays of a round.');
});

test('a sheet reads the words of every idea merged into it, each with who asked', () => {
  assert.deepEqual(sheetWords(idea, NOW).map(word => word.label), [
    'What they were trying to do', 'How they would like it', 'Also asked 1 d ago, by a guest', 'Also asked 1 h ago, by an account since deleted',
  ]);
  assert.equal(sheetWords(idea, NOW)[3].text, 'A replay, please.\nFrom the recap.\nOr from the menu.');
});

test('an idea\'s statuses are offered as filters and said with their plan or reason', () => {
  assert.deepEqual(STATUS_FILTERS.map(one => one.value), ['open', 'new', 'seen', 'reproduced', 'planned', 'shipped', 'declined', 'dismissed', '']);
  assert.equal(statusLine({ status: 'declined', reason: 'Not for this game.' }), 'Declined: Not for this game.');
  assert.equal(statusLine({ status: 'shipped', plan: 'docs/tabletop/replay-plan.md' }), 'Shipped');
  const can = sheetActions(idea);
  assert.deepEqual([can.planned, can.shipped, can.declined, can.merge, can.prompt, can.toBug], [true, true, true, true, true, false]);
});

test('the plan prompt is built from the operator\'s words, never the player\'s, with how many asked', () => {
  assert.equal(planPrompt('  ', idea), null);
  assert.equal(planPrompt('Let a player\nwatch the last round again', idea),
    '/new-plan Let a player watch the last round again. Asked by 3 players since 2026-11-14.');
  assert.equal(planPrompt('Replays from the recap?', { ...idea, askedBy: 1 }), '/new-plan Replays from the recap? Asked by 1 player since 2026-11-14.');
});

test('an idea is merged into another open idea, never into itself, each said with how many asked it', () => {
  const choices = mergeChoices([
    { id: 'a', kind: 'idea', line: 'Replays.', askedBy: 2 },
    { id: idea.id, kind: 'idea', line: 'This one.', askedBy: 3 },
    { id: 'r', kind: 'bug', line: 'A bug.' },
  ], idea.id);
  assert.deepEqual(choices, [{ value: 'a', label: 'Replays. (asked by 2)' }]);
});
