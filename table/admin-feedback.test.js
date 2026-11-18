import test from 'node:test';
import assert from 'node:assert/strict';
import { ADDRESS_LIMIT, askedLine, askLinks, categoryFilters, choicesNote, categoryLabel, changeSaid, closedIssues, creatureLine, downloadNote, feedbackLine, feedbackPath, feedbackRows, filedSaid, fixedReports, issueAddress, issueLink, issueNumber, issuePath, issuesPath, closedFiled, mergeChoices, muteLine, planPrompt, positionSides, reproduceSaid, sheetActions, sheetFacts, sheetWords, statusLine, STATUS_FILTERS } from './admin-feedback.js';

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
  session: '/session/20261116-110000-deadbeef', underWay: true, reproduceOnceOver: true, actions: ['seen', 'reproduced', 'dismiss', 'spam', 'toIdea', 'diagnose', 'mute'],
};

test('the list\'s address leaves out every filter that is every one, and the first page', () => {
  assert.equal(feedbackPath({ status: '' }), '/api/admin/feedback');
  assert.equal(feedbackPath(), '/api/admin/feedback?status=open');
  assert.equal(feedbackPath({ kind: 'idea', status: 'new', category: 'battle', page: 2 }), '/api/admin/feedback?kind=idea&status=new&category=battle&page=2');
  assert.equal(feedbackPath({ kind: 'bug', filed: true }), '/api/admin/feedback?kind=bug&status=open&filed=true');
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
  assert.deepEqual([bug.reproduce, bug.seen, bug.reproduced, bug.toIdea, bug.toBug, bug.diagnose, bug.mute, bug.unmute], [false, true, true, true, false, true, true, false]);
  assert.equal(sheetActions({ ...report, underWay: false, actions: ['reproduce', 'seen'] }).reproduce, true, 'a game over offers its round to cut');

  assert.equal(sheetActions({ ...report, actions: ['unmerge', 'dismiss'] }).unmerge, true, 'a merged idea offers its unmerge (#725)');
  assert.deepEqual(Object.values(sheetActions({ ...report, actions: ['bogus'] })).filter(Boolean), []);
  assert.deepEqual(Object.values(sheetActions(null)).filter(Boolean), [], 'a sheet still being read offers nothing');
});

test('a mute says how long it lasts and why, and a change says what the host answered', () => {
  assert.equal(muteLine({ daysLeft: 30, why: 'spam', by: 'mark' }), 'This player can send no feedback for 30 more days (three dismissed as spam in 30 days).');
  assert.equal(muteLine({ daysLeft: 1, why: 'operator', by: 'mark' }), 'This player can send no feedback for 1 more day (by mark).');
  assert.deepEqual(changeSaid({ ok: true, body: {} }, 'Marked seen.'), { refused: false, line: 'Marked seen.' });
  assert.deepEqual(changeSaid({ ok: false, status: 409, body: { message: 'This feedback is closed.' } }, 'x'), { refused: true, line: 'This feedback is closed.' });
});

test('Reproduce says the round it cut, the rounds the journal offers instead, or what the host refused', () => {
  assert.deepEqual(reproduceSaid({ ok: true, body: { ...report, reproduction: { id: 'sc-1', link: '/battle?scenario=sc-1' } } }),
    { refused: false, line: 'Round 3 is cut, in the reporter\'s seat. Play it from the link.' });
  assert.deepEqual(reproduceSaid({ ok: false, status: 409, body: { error: 'Scenario.RoundUnavailable', message: 'x', rounds: [1, 2, 3] } }),
    { refused: true, line: 'The reported round cannot be cut. The match\'s journal offers rounds 1, 2 and 3: cut one from the Scenarios tab.' });
  assert.equal(reproduceSaid({ ok: false, status: 409, body: { message: 'x', rounds: [2] } }).line,
    'The reported round cannot be cut. The match\'s journal offers round 2: cut one from the Scenarios tab.');
  assert.equal(reproduceSaid({ ok: false, status: 409, body: { message: 'x', rounds: [] } }).line, 'The reported round cannot be cut, and the match\'s journal offers no round.');
  const practice = { ...report, context: { scenario: 'sc-1', seat: 'player2', round: 3 } };
  assert.equal(reproduceSaid({ ok: false, status: 409, body: { message: 'x', rounds: [2] } }, practice).line,
    'The reported round cannot be cut from what this report copied of its practice, which reaches round 2 only.');
  assert.deepEqual(reproduceSaid({ ok: false, status: 409, body: { error: 'Feedback.NoPosition', message: 'This report was not sent from a round.' } }),
    { refused: true, line: 'This report was not sent from a round.' });
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
  assert.equal(askedLine(2, null), 'Asked by 2 players', 'no date the host did not send');
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
  assert.deepEqual(STATUS_FILTERS.map(one => one.value), ['open', 'new', 'seen', 'reproduced', 'filed', 'fixed', 'planned', 'shipped', 'declined', 'dismissed', '']);
  assert.equal(statusLine({ status: 'declined', reason: 'Not for this game.' }), 'Declined: Not for this game.');
  assert.equal(statusLine({ status: 'shipped', plan: 'docs/tabletop/replay-plan.md' }), 'Shipped: docs/tabletop/replay-plan.md');
  assert.equal(statusLine({ status: 'shipped', plan: null }), 'Shipped');
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
  assert.equal(choicesNote(5, true), '');
  assert.equal(choicesNote(7, true), 'Only the first 100 open ideas are listed.');
  assert.equal(choicesNote(1, false), 'The open ideas could not be read. Close the sheet and open it again.');
});

test('each idea merged into a group opens its own sheet, where it can be dismissed and its sender stopped', () => {
  assert.deepEqual(askLinks(idea, NOW), [
    { id: 'b', label: 'Open the idea asked 1 d ago, by a guest' },
    { id: 'c', label: 'Open the idea asked 1 h ago, by an account since deleted' },
  ]);
  assert.deepEqual(askLinks({ ...idea, asks: undefined }, NOW), []);
});

// ---- R5: the redacted scenario and the issue.

const filed = { ...report, repository: 'Downfallz/maintest', issue: null, reproduction: { id: 'sc-1', link: '/battle?scenario=sc-1' }, underWay: false, reproduceOnceOver: false,
  actions: ['reproduce', 'download', 'seen', 'reproduced', 'dismiss', 'spam', 'toIdea', 'file', 'mute'] };

const fields = address => Object.fromEntries(new URL(address).searchParams);

test('File an issue opens GitHub\'s form for a report, pre-filled with what the operator reads before it becomes public', () => {
  const address = issueAddress(filed);

  assert.ok(address.startsWith('https://github.com/Downfallz/maintest/issues/new?'));
  const asked = fields(address);
  assert.equal(asked.template, 'bug_from_report.yml');
  assert.equal(asked.title, 'Report 20261116T113000000Z-abc123: the rules did something wrong, round 3');
  assert.equal(asked.report, '20261116T113000000Z-abc123');
  assert.equal(asked.category, 'The rules did something wrong');
  assert.equal(Object.hasOwn(asked, 'happened'), false, 'the player\'s words stay in the panel: the operator writes the issue');
  assert.equal(Object.hasOwn(asked, 'expected'), false);
  assert.ok(!address.includes('bleed'), 'no word of the player\'s reaches GitHub before the operator writes it');
  assert.equal(asked.where, 'Round 3, Speed, player1');
  assert.equal(asked.played, 'Engine 1.2.3 · content abcdef0123456789 · Rules 3v3');
  assert.equal(asked.errors, 'TypeError: x is undefined (battle.js:12)');
  assert.equal(asked.scenario, 'Drop report-20261116T113000000Z-abc123.zip here, downloaded from the panel.');
  assert.equal(Object.hasOwn(asked, 'table'), false);
  assert.ok(!address.includes('deadbeef'), 'the table\'s id is not in the address');
});

test('the address stays under 8 KB, the page\'s errors cut and marked', () => {
  const long = { ...filed, errors: Array.from({ length: 10 }, (_, at) => ({ message: `TypeError ${at}: ${'é'.repeat(300)}`, where: 'https://play.example/battle.js:12' })) };

  const address = issueAddress(long);

  assert.ok(address.length <= ADDRESS_LIMIT, `${address.length} characters`);
  const asked = fields(address);
  assert.ok(asked.errors.startsWith('TypeError 0: é'));
  assert.ok(asked.errors.endsWith('…'));
  const emoji = issueAddress({ ...filed, errors: Array.from({ length: 10 }, () => ({ message: 'a😀'.repeat(400), where: null })) });
  assert.ok(emoji.length <= ADDRESS_LIMIT, 'an emoji is never cut in half');
  assert.equal(asked.title, 'Report 20261116T113000000Z-abc123: the rules did something wrong, round 3');
});

test('a report sent outside a game is filed with no round and no scenario to drop', () => {
  const asked = fields(issueAddress({ ...filed, context: null, errors: [], expected: null, additions: [], reproduction: null }));

  assert.equal(asked.title, 'Report 20261116T113000000Z-abc123: the rules did something wrong');
  assert.equal(asked.where, 'Outside a game');
  assert.equal(Object.hasOwn(asked, 'errors'), false);
  assert.equal(Object.hasOwn(asked, 'scenario'), false);
  assert.equal(issueAddress({ ...filed, repository: null }), null);
});

test('the issue\'s number is read as typed, with its hash or as its address', () => {
  assert.equal(issueNumber('731', 'Downfallz/maintest'), 731);
  assert.equal(issueNumber(' #731 ', 'Downfallz/maintest'), 731);
  assert.equal(issueNumber('https://github.com/Downfallz/maintest/issues/731', 'Downfallz/maintest'), 731);
  assert.equal(issueNumber('https://github.com/downfallz/maintest/issues/731#issuecomment-1', 'Downfallz/maintest'), 731);
  for (const typed of ['', 'seven', '0', '-3', '7.5', 'https://github.com/Downfallz/maintest/pull/731', 'https://github.com/someone/else/issues/731']) {
    assert.equal(issueNumber(typed, 'Downfallz/maintest'), null, typed);
  }
});

test('a filed report links its issue, so a mistyped number shows before it marks the wrong report fixed', () => {
  assert.equal(issueLink({ ...filed, issue: 731 }), 'https://github.com/Downfallz/maintest/issues/731');
  assert.equal(issueLink(filed), null);
  assert.equal(issueLink({ ...filed, issue: 731, repository: null }), null);
});

test('a report filed reads filed #N and fixed #N, and both are filters', () => {
  assert.equal(statusLine({ status: 'filed', issue: 731 }), 'Filed #731');
  assert.equal(statusLine({ status: 'fixed', issue: 731 }), 'Fixed #731');
  assert.equal(statusLine({ status: 'reproduced', issue: 731 }), 'Reproduced · filed #731');
  assert.equal(filedSaid({ ok: true, body: { issue: 731 } }).line, 'Filed as #731. The report reads fixed once the issue is closed.');
  assert.equal(filedSaid({ ok: false, status: 400, body: { message: 'Filing names the issue\'s number, as GitHub gave it.' } }).refused, true);
});

test('the tab reads the closed issues filed from reports, updated since the oldest filed report was sent, a page at a time', () => {
  assert.equal(issuesPath('Downfallz/maintest', '2026-11-01T08:00:00Z'),
    'https://api.github.com/repos/Downfallz/maintest/issues?state=closed&labels=from-report&sort=updated&direction=desc&per_page=100&page=1&since=2026-11-01T08%3A00%3A00.000Z');
  assert.equal(issuesPath('Downfallz/maintest', null, 3), 'https://api.github.com/repos/Downfallz/maintest/issues?state=closed&labels=from-report&sort=updated&direction=desc&per_page=100&page=3');
  assert.equal(issuesPath(null), null);
});

test('past the window of closed issues, each filed issue left is asked by its number, and GitHub refusing keeps what was read (#751)', async () => {
  assert.equal(issuePath('Downfallz/maintest', 731), 'https://api.github.com/repos/Downfallz/maintest/issues/731');
  assert.equal(issuePath('Downfallz/maintest', 'x'), null);
  const page = (from) => Array.from({ length: 100 }, (_, index) => ({ number: from + index, state: 'closed', state_reason: 'completed' }));
  const rows = [{ id: 'a', issue: 1000, at: '2026-11-01T08:00:00Z' }, { id: 'z', issue: 9000, at: '2026-11-01T09:00:00Z' }, { id: 'b', issue: 9001, at: '2026-11-02T08:00:00Z' }, { id: 'c', issue: 9002, at: '2026-11-03T08:00:00Z' }, { id: 'd', issue: 9001, at: '2026-11-04T08:00:00Z' }];
  const asked = [];
  const github = (limit) => async (address) => {
    asked.push(address.replace('https://api.github.com/repos/Downfallz/maintest/issues', ''));
    if (asked.length > limit) return { ok: false, status: 403, json: async () => ({}) };
    const number = Number(address.match(/issues\/(\d+)$/)?.[1]);
    if (number === 9000) return { ok: false, status: 410, json: async () => ({}) };
    if (number) return { ok: true, json: async () => ({ number, state: number === 9001 ? 'closed' : 'open', state_reason: number === 9001 ? 'completed' : null }) };
    return { ok: true, json: async () => page(Number(address.match(/&page=(\d+)/)[1]) * 1000) };
  };

  const read = await closedFiled(github(Infinity), 'Downfallz/maintest', rows);

  assert.equal(read.complete, true);
  assert.deepEqual([read.closed.has(9001), read.closed.has(9002), read.closed.has(1000)], [true, false, true]);
  assert.deepEqual(asked.filter(path => !path.startsWith('?')), ['/9000', '/9001', '/9002'], 'only the issues the window did not hold, each once, past one GitHub no longer has');

  asked.length = 0;
  const cut = await closedFiled(github(7), 'Downfallz/maintest', rows);

  assert.deepEqual([cut.complete, cut.closed.has(9001), cut.closed.has(1000)], [false, true, true]);
  asked.length = 0;
  await assert.rejects(closedFiled(github(0), 'Downfallz/maintest', rows), 'a window GitHub refuses is not read at all');
});

test('an issue closed as completed fixes the reports filed as it; one closed as not planned, a pull request or one still open does not', () => {
  const closed = closedIssues([
    { number: 731, state: 'closed', state_reason: 'completed' },
    { number: 732, state: 'closed', state_reason: 'not_planned' },
    { number: 733, state: 'closed', state_reason: 'completed', pull_request: {} },
    { number: 734, state: 'open', state_reason: null },
    { number: 735, state: 'closed', state_reason: null },
    { number: 736, state: 'closed', state_reason: 'duplicate' },
  ]);

  assert.deepEqual([...closed].sort(), [731, 735]);
  assert.deepEqual(fixedReports([{ id: 'a', status: 'filed', issue: 731 }, { id: 'b', status: 'filed', issue: 732 }, { id: 'c', status: 'fixed', issue: 735 },
    { id: 'd', status: 'new' }, { id: 'e', status: 'reproduced', issue: 735 }, { id: 'f', status: 'dismissed', issue: 731 }], closed), ['a', 'e']);
  assert.deepEqual([...closedIssues({ message: 'API rate limit exceeded' })], []);
});

test('the download is offered once the game is over, and the sheet says why while it is not', () => {
  assert.deepEqual([sheetActions(filed).download, sheetActions(filed).file], [true, true]);
  assert.equal(downloadNote(filed), '');
  assert.equal(downloadNote({ ...filed, underWay: true, actions: ['file'] }), 'The scenario can be downloaded once the game it was sent from is over: nothing a seat holds face down reaches a public issue.');
  assert.equal(downloadNote({ ...filed, unread: true, actions: ['file'] }), 'The record of the game this report was sent from is missing or cannot be read (its session may have been deleted): nothing shows the game is over, so the scenario is not offered.');
  assert.equal(downloadNote({ ...filed, reproduction: null, actions: ['reproduce', 'file'] }), 'Reproduce the round first: its scenario is what the issue carries.');
  assert.equal(downloadNote({ ...filed, kind: 'idea', reproduction: null, actions: [] }), '');
  assert.equal(downloadNote({ ...filed, reproduction: null, underWay: true, reproduceOnceOver: true, actions: ['file', 'diagnose'] }),
    'The round can be reproduced once the game it was sent from is over: its scenario would hold what the other seat keeps face down.');
  assert.equal(downloadNote({ ...filed, reproduction: null, underWay: true, reproduceOnceOver: false, actions: ['file', 'diagnose'] }), '', 'a report sent outside a round is never cut');
});
