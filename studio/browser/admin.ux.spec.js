import { test, expect } from '@playwright/test';

const widthOf = page => page.viewportSize().width;

// Host-shaped answers, with the two things that pushed the panel past a phone: a bot named by a long weights
// file, and a store whose location is one long path.
const weights = 'heuristic:learning/weights/search-4-lookahead-tuned-against-greedy-and-random.json';
const location = '/home/runner/work/maintest/maintest/runs/playtest/20261031-155351-3ce569f8-a-very-long-store-path';
const tables = { rules: 'Rules 3v3 (a test)', recording: true, tables: [], agents: [{ value: weights, label: `${weights} — searched weights` }] };
const session = (id, player1, player2, over) => ({ id, createdAt: '2026-10-31T15:53:51Z', player1, player2, steps: 90, matches: over ? 1 : 0,
  over, live: false, location: `${location}/${id}`, session: `/session/${id}`, export: `/api/sessions/${id}/export`, replay: over ? `/play?replay=${id}` : null });
const sessions = [
  session('people-finished', 'human:mk', 'human:jo', true),
  session('mixed-finished', 'human:mk', 'Greedy', true),
  session('people-abandoned', 'human:mk', 'human:jo', false),
  ...Array.from({ length: 22 }, (_, index) => session(`bots-${String(index).padStart(2, '0')}`, 'Greedy', 'Random', true)),
];

// A table as the host lists it, playing at round 3 with one seat waiting for its code.
const table = index => ({ id: `table-${String(index).padStart(2, '0')}`, round: 3, format: 'ThreeVersusThree', pilot: `/pilot?token=p${index}`,
  seats: [{ slot: 'player1', seated: 'human:mk', taken: true }, { slot: 'player2', seated: 'nobody', code: `CODE${index}`, join: `/join/CODE${index}` }] });

const overview = { players: null, games: { inProgress: { againstPlayers: 1, againstBots: 2 }, finished: null },
  waiting: { openSeats: 1, invitations: 0, endingSoon: 0 }, jobs: { lastRun: null, turns: null }, registration: null };

test.beforeEach(async ({ page }) => {
  // The shared header asks who is signed in: the operator, on a host where players do not sign in.
  await page.route('**/api/me', route => route.fulfill({ json: { operator: true, admin: '/admin', accounts: 'off', account: null } }));
  await page.route('**/api/tables', route => route.fulfill({ json: tables }));
  await page.route('**/api/sessions', route => route.fulfill({ json: { sessions } }));
  // The Overview (A5) of a host with no accounts and no jobs: its tables' numbers alone.
  await page.route('**/api/admin/overview', route => route.fulfill({ json: overview }));
  await page.goto('/table/admin.html?token=operator');
  await expect(page.locator('#sessions li')).toHaveCount(20);
});

test('the admin panel fits a phone, a long bot name and a long store path included', async ({ page }, info) => {
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(widthOf(page));
  await page.screenshot({ path: info.outputPath('admin.png'), fullPage: true });
});

test('sessions are filtered to finished matches between people and paged twenty at a time', async ({ page }) => {
  await page.locator('#tab-sessions').click();
  await expect(page.locator('#sessions-count')).toHaveText('25 of 25 sessions · page 1 of 2');
  await page.locator('#sessions-next').click();
  await expect(page.locator('#sessions li')).toHaveCount(5);
  await expect(page.locator('#sessions-next')).toBeDisabled();

  await page.selectOption('#sessions-state', 'finished');
  await page.selectOption('#sessions-players', 'people');

  await expect(page.locator('#sessions-count')).toHaveText('1 of 25 sessions · page 1 of 1');
  await expect(page.locator('#sessions li')).toHaveText(/people-finished/);
  await expect(page.locator('#sessions a', { hasText: 'replay' })).toHaveAttribute('href', '/play?replay=people-finished');
});

test('the headquarters open on the Overview under the shared header, and each section is part of the address', async ({ page }, info) => {
  await expect(page.locator('header[data-navigation="admin"] #nav-admin-link')).toHaveAttribute('aria-current', 'page');
  await expect(page.locator('#tab-overview')).toHaveAttribute('aria-current', 'page');
  await expect(page.locator('#panel-overview')).toBeVisible(); await expect(page.locator('#panel-tables')).toBeHidden();
  await expect(page.locator('#tab-sessions-count')).toHaveText('25');

  await page.locator('#tab-tables').click();
  await expect(page).toHaveURL(/#tables$/);
  await expect(page.locator('#panel-tables')).toBeVisible(); await expect(page.locator('#panel-overview')).toBeHidden();
  await page.locator('#tab-sessions').click();
  await expect(page).toHaveURL(/#sessions$/);
  await expect(page.locator('#panel-sessions')).toBeVisible(); await expect(page.locator('#panel-tables')).toBeHidden();
  await expect(page.locator('#tab-sessions')).toHaveAttribute('aria-current', 'page');
  await page.screenshot({ path: info.outputPath('admin-sessions.png'), fullPage: true });

  // Back is the section before; a reload keeps the one in the address.
  await page.goBack();
  await expect(page.locator('#panel-tables')).toBeVisible();
  await page.goto('/table/admin.html#sessions');
  await expect(page.locator('#panel-sessions')).toBeVisible();
  // The console's link carries the token: it leaves the address, and the section it names stays.
  await page.goto('/table/admin.html?token=operator#sessions');
  await expect(page.locator('#panel-sessions')).toBeVisible();
  await expect(page).toHaveURL(/\/table\/admin\.html#sessions$/);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(widthOf(page));
});

// The Overview (A5): a card a source, each linking to its tab, and the ones that need the operator marked.
test('the Overview shows a card for each number the host has, links each to its tab, and marks what needs the operator', async ({ page }, info) => {
  await page.route('**/api/admin/overview', route => route.fulfill({ json: {
    players: { total: 42, seenThisWeek: 17, newThisWeek: 3, suspended: 1 },
    games: { inProgress: { againstPlayers: 4, againstBots: 6 }, finished: { unavailable: true } },
    waiting: { openSeats: 2, invitations: 1, endingSoon: 3 },
    jobs: { lastRun: { id: 'r', started: new Date(Date.now() - 3600000).toISOString(), outcome: 'failed' }, turns: { waiting: 2, failed: 1 } },
    registration: { open: false } } }));
  await page.reload();

  const cards = page.locator('#overview-cards .overview-card');
  await expect(cards).toHaveCount(7);
  await expect(page.locator('[data-card="players"] .overview-number')).toHaveText('42');
  await expect(page.locator('[data-card="players"] .overview-detail')).toHaveText('17 seen this week · 3 new · 1 suspended');
  await expect(page.locator('[data-card="finished"] .overview-number')).toHaveText('—');
  await expect(page.locator('#overview-cards .overview-card.warning')).toHaveCount(3);
  await expect(page.locator('#tab-overview-count')).toHaveText('!');
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(widthOf(page));
  await page.screenshot({ path: info.outputPath('admin-overview.png'), fullPage: true });

  await page.locator('[data-card="in-progress"]').click();
  await expect(page).toHaveURL(/#tables$/);
  await expect(page.locator('#panel-tables')).toBeVisible();
});

test('the tables under way are paged twenty at a time, the page kept across the host\'s polls', async ({ page }) => {
  await page.route('**/api/tables', route => route.fulfill({ json: { ...tables, tables: Array.from({ length: 25 }, (_, index) => table(index)) } }));
  await page.reload();
  await page.locator('#tab-tables').click();
  await expect(page.locator('#tables .table-card')).toHaveCount(20);
  await expect(page.locator('#tables-count')).toHaveText('25 tables · page 1 of 2');
  await expect(page.locator('#tables-previous')).toBeDisabled();
  await expect(page.locator('#tab-tables-count')).toHaveText('25');

  await page.locator('#tables-next').click();
  await expect(page.locator('#tables .table-card')).toHaveCount(5);
  await expect(page.locator('#tables-next')).toBeDisabled();
  // The list is read again every few seconds; the page the operator turned to stays.
  await page.waitForTimeout(3000);
  await expect(page.locator('#tables-count')).toHaveText('25 tables · page 2 of 2');
});

// The Players tab (A2, ADR 0174) on a host that signs players in: the host pages, sorts and searches; the page sends
// what was asked and draws what comes back.
const player = (index, extra = {}) => ({ id: `acct_${String(index).padStart(16, '0')}`, username: `player${String(index).padStart(2, '0')}`,
  createdAt: '2026-11-01T10:00:00Z', lastSignIn: '2026-11-12T10:00:00Z', lastSeen: '2026-11-13T10:00:00Z',
  games: index, ranked: 1, againstPlayers: 1, againstBots: index - 1, wins: 1, rating: index === 1 ? 1612 : null, provisional: false, ...extra });
const everyone = [player(1, { username: 'a-very-long-player-name' }), ...Array.from({ length: 24 }, (_, index) => player(index + 2))];

async function withPlayers(page) {
  const asked = [];
  const kept = new Map(everyone.map(one => [one.id, { ...one }]));
  await page.route(/\/api\/admin\/players(\?.*)?$/, route => {
    const url = new URL(route.request().url());
    asked.push(url.search);
    const q = (url.searchParams.get('q') ?? '').toLowerCase();
    const number = Number(url.searchParams.get('page') ?? 1);
    const matching = [...kept.values()].filter(one => one.username.toLowerCase().includes(q));
    const pages = Math.max(1, Math.ceil(matching.length / 20));
    const shown = Math.min(number, pages);
    return route.fulfill({ json: { players: matching.slice((shown - 1) * 20, shown * 20), total: kept.size, matching: matching.length, page: shown, pages, perPage: 20, sort: url.searchParams.get('sort') ?? 'seen', q } });
  });
  await page.route(/\/api\/admin\/players\/acct_[^/?]+(\/[a-z-]+)?$/, async route => {
    const [, id, action] = /players\/(acct_[^/?]+)(?:\/([a-z-]+))?$/.exec(new URL(route.request().url()).pathname);
    const method = route.request().method();
    // A suspension (ADR 0178) is kept on the player as the host describes it, and a restore lifts it.
    if (action === 'suspension' && method === 'POST') {
      kept.get(id).suspension = { reason: route.request().postDataJSON().reason, by: 'operator', at: new Date().toISOString() };
      return route.fulfill({ json: { suspension: kept.get(id).suspension } });
    }
    if (action === 'suspension' && method === 'DELETE') {
      delete kept.get(id).suspension;
      return route.fulfill({ json: { restored: true } });
    }
    if (method === 'DELETE') {
      kept.delete(id);
      return route.fulfill({ json: { deleted: true } });
    }
    if (action === 'rename') {
      const username = route.request().postDataJSON().username;
      if ([...kept.values()].some(one => one.username.toLowerCase() === username.toLowerCase() && one.id !== id)) {
        return route.fulfill({ status: 409, json: { error: 'Account.UsernameTaken', message: 'Another player already has that name. Choose another.' } });
      }
      kept.get(id).username = username;
      return route.fulfill({ json: { account: { id, username } } });
    }
    if (action === 'sign-out') return route.fulfill({ json: { signedOut: true } });
    return route.fulfill({ json: { player: kept.get(id), devices: 2, friends: 3, requests: 1,
      recent: [{ finishedAt: '2026-11-13T09:00:00Z', format: '1v1', rounds: 7, ranked: false, opponent: 'Greedy', outcome: 'loss', table: 't1' }] } });
  });
  await page.reload();
  return asked;
}

test('a host without accounts shows no Players tab', async ({ page }) => {
  await expect(page.locator('#tab-players')).toBeHidden();
  await page.goto('/table/admin.html#players');
  await expect(page.locator('#players-off')).toBeVisible();
});

test('the players are listed twenty a page, searched and sorted by the host', async ({ page }, info) => {
  const asked = await withPlayers(page);
  await page.locator('#tab-players').click();

  await expect(page.locator('#players tr')).toHaveCount(20);
  await expect(page.locator('#tab-players-count')).toHaveText('25');
  await expect(page.locator('#players-count')).toHaveText('25 players · page 1 of 2');
  await expect(page.locator('#players tr').first().locator('.player-rating')).toHaveText('1612');
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(widthOf(page));
  await page.screenshot({ path: info.outputPath('admin-players.png'), fullPage: true });

  await page.locator('#players-next').click();
  await expect(page.locator('#players tr')).toHaveCount(5);
  await page.selectOption('#players-sort', 'games');
  await expect.poll(() => asked.at(-1)).toBe('?sort=games');
  await page.locator('#players-search').fill('player1');
  await expect(page.locator('#players-count')).toHaveText('10 of 25 players · page 1 of 1');
  expect(asked.at(-1)).toBe('?q=player1&sort=games');
});

test('a player\'s sheet renames them, refuses a name taken, and deletes only once the name is typed', async ({ page }, info) => {
  await withPlayers(page);
  await page.locator('#tab-players').click();
  await page.locator('#players .player-open', { hasText: 'player02' }).click();

  const sheet = page.locator('#player-sheet');
  await expect(sheet).toBeVisible();
  await expect(sheet.locator('#player-sheet-name')).toHaveText('player02');
  await expect(sheet.locator('#player-recent li')).toHaveText(/Lost against Greedy · 1v1 · unranked/);
  await expect(sheet.locator('#player-facts')).toContainText('3 · 1 pending');
  const box = await sheet.boundingBox();
  expect(box.x + box.width).toBeLessThanOrEqual(widthOf(page) + 1);
  await page.screenshot({ path: info.outputPath('admin-player-sheet.png') });

  await sheet.locator('#player-rename-name').fill('player03');
  await sheet.locator('#player-rename').click();
  await expect(sheet.locator('#player-said')).toHaveText('Another player already has that name. Choose another.');
  await sheet.locator('#player-rename-name').fill('Renamed');
  await sheet.locator('#player-rename').click();
  await expect(sheet.locator('#player-said')).toHaveText('Renamed to Renamed.');
  await expect(sheet.locator('#player-sheet-name')).toHaveText('Renamed');

  await sheet.locator('#player-signout').click();
  await sheet.locator('#player-signout-yes').click();
  await expect(sheet.locator('#player-said')).toHaveText(/^Renamed is signed out everywhere/);

  await expect(sheet.locator('#player-delete')).toBeDisabled();
  await sheet.locator('#player-delete-name').fill('someone else');
  await expect(sheet.locator('#player-delete')).toBeDisabled();
  await sheet.locator('#player-delete-name').fill('renamed');
  await sheet.locator('#player-delete').click();
  await expect(sheet).toBeHidden();
  await expect(page.locator('#players-said')).toHaveText("Renamed's account is deleted.");
  await expect(page.locator('#tab-players-count')).toHaveText('24');
});

test('a player is suspended with a reason they read, marked in the list, and restored', async ({ page }, info) => {
  await withPlayers(page);
  await page.locator('#tab-players').click();
  await page.locator('#players .player-open', { hasText: 'player02' }).click();
  const sheet = page.locator('#player-sheet');

  await expect(sheet.locator('#player-suspend')).toBeDisabled();
  await sheet.locator('#player-suspend-reason').fill('too short');
  await expect(sheet.locator('#player-suspend')).toBeDisabled();
  await sheet.locator('#player-suspend-reason').fill('Spamming invitations to strangers.');
  await sheet.locator('#player-suspend').click();
  await expect(sheet.locator('#player-suspend-line')).toContainText('Spamming invitations to strangers.');
  await sheet.locator('#player-suspend-yes').click();

  await expect(sheet.locator('#player-said')).toHaveText('player02 is suspended. Signing in tells them why.');
  await expect(sheet.locator('#player-suspended-line')).toHaveText(/^Suspended just now by operator: Spamming invitations to strangers\. Its seats are offered to other players in 24 h, unless it is restored first\.$/);
  await expect(sheet.locator('#player-suspend-form')).toBeHidden();
  await expect(page.locator('#players tr', { hasText: 'player02' }).locator('.status-tag.suspended')).toHaveText('Suspended');
  const box = await sheet.boundingBox();
  expect(box.x + box.width).toBeLessThanOrEqual(widthOf(page) + 1);
  await page.screenshot({ path: info.outputPath('admin-player-suspended.png') });

  await sheet.locator('#player-restore').click();
  await expect(sheet.locator('#player-said')).toHaveText('player02 is restored and can sign in again.');
  await expect(sheet.locator('#player-suspended')).toBeHidden();
  await expect(sheet.locator('#player-suspend-form')).toBeVisible();
  await expect(page.locator('#players tr', { hasText: 'player02' }).locator('.status-tag.suspended')).toHaveCount(0);
});

// The Activity tab (A6): what operators did, twenty a page, newest first, a table's action linking to its session.
test('a host that records nothing shows no Activity tab', async ({ page }) => {
  await expect(page.locator('#tab-activity')).toBeHidden();
  await page.goto('/table/admin.html#activity');
  await expect(page.locator('#activity-off')).toBeVisible();
});

test('the operators\' actions are listed twenty a page, newest first, a table\'s linking to its session', async ({ page }, info) => {
  const kinds = ['table.void', 'player.suspend', 'settings.save', 'table.open', 'turns.requeue'];
  const actions = Array.from({ length: 23 }, (_, index) => ({ id: `a${index}`, at: new Date(Date.now() - index * 60000).toISOString(), by: index % 2 ? 'ops' : 'mark',
    action: kinds[index % kinds.length], target: index % kinds.length === 2 || index % kinds.length === 4 ? null : `target-${index}`,
    detail: index === 0 ? 'stuck: The match stopped answering after a host restart, and nobody could reach their seat again.' : null }));
  await page.route(/\/api\/admin\/activity(\?.*)?$/, route => {
    const number = Number(new URL(route.request().url()).searchParams.get('page') ?? 1);
    return route.fulfill({ json: { actions: actions.slice((number - 1) * 20, number * 20), total: actions.length, page: number, pages: 2, perPage: 20, kept: 1000 } });
  });
  await page.reload();
  await page.locator('#tab-activity').click();

  await expect(page.locator('#activity li')).toHaveCount(20);
  await expect(page.locator('#activity li').first()).toContainText('Voided a match · target-0');
  await expect(page.locator('#activity li').first().locator('a')).toHaveAttribute('href', '/session/target-0');
  await expect(page.locator('#activity li').nth(1)).toContainText('Suspended an account · target-1');
  await expect(page.locator('#activity li').nth(1).locator('a')).toHaveCount(0);
  await expect(page.locator('#activity-count')).toHaveText('23 actions kept (the last 1000) · page 1 of 2');
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(widthOf(page));
  await page.screenshot({ path: info.outputPath('admin-activity.png'), fullPage: true });

  await page.locator('#activity-next').click();
  await expect(page.locator('#activity li')).toHaveCount(3);
});

// The Feedback tab (feedback plan, R3): a bug report's sheet with its position, its round cut as a scenario (R4), moved
// to an idea, dismissed as spam, its sender stopped; each answer the host's.
const feedbackReport = {
  id: '20261116T113000000Z-abc123', kind: 'bug', category: 'rules', status: 'new', reason: null, at: new Date(Date.now() - 30 * 60000).toISOString(),
  text: 'The bleed did not tick after the stun, and the creature that should have bled at the top of the round kept every point of its health.',
  expected: 'One damage in the upkeep.', additions: [], page: '/battle', browser: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15',
  errors: [{ message: 'TypeError: undefined is not an object (evaluating \'board.timeline.length\')', where: 'battle-build/battle.js:12345' }],
  account: null, guest: true, senderGone: false, mute: null,
  context: { table: '20261116-110000-a-long-table-id', seat: 'player1', round: 3, subPhase: 'Upkeep', journalLength: 42, content: 'f3e8a42ddbd02c11', rules: 'Rules 3v3', engine: 'abc', position: {
    allies: [{ name: 'Ravager', health: 7, maxHealth: 10, energy: 2, isStunned: true, conditions: [{ effect: { kind: 'Bleed' } }] }],
    enemies: [{ name: 'Warden', health: 12, maxHealth: 12, energy: 1, isStunned: false, conditions: [] }] } },
  session: '/session/20261116-110000-a-long-table-id', underWay: true, reproduceOnceOver: true, actions: ['seen', 'reproduced', 'dismiss', 'spam', 'toIdea', 'diagnose', 'mute'],
};
const feedbackRow = one => ({ id: one.id, kind: one.kind, category: one.category, line: one.text.slice(0, 120), status: one.status, reason: one.reason, spam: one.reason === 'spam', at: one.at,
  table: one.context?.table ?? null, round: one.context?.round ?? null, additions: 0 });

test('a host that keeps no feedback shows no Feedback tab', async ({ page }) => {
  await expect(page.locator('#tab-feedback')).toBeHidden();
  await page.goto('/table/admin.html#feedback');
  await expect(page.locator('#admin-feedback-off')).toBeVisible();
});

test('a bug report is read beside its position, its round cut, turned into an idea, dismissed as spam, and its sender stopped', async ({ page }, info) => {
  let sheet = { ...feedbackReport };
  const idea = { ...feedbackReport, id: '20261116T100000000Z-def456', kind: 'idea', category: 'lobby', text: 'I wanted to invite a friend by name.', context: null, session: null, underWay: false, actions: [] };
  // What the host offers, as FeedbackAdminApi decides it, for the sheet as it now stands.
  const offered = one => [one.kind === 'bug' && !one.underWay && 'reproduce', one.status !== 'seen' && 'seen', one.kind === 'bug' && one.status !== 'reproduced' && 'reproduced',
    ...(one.status === 'dismissed' ? [] : ['dismiss', 'spam', one.kind === 'bug' ? 'toIdea' : 'toBug']), one.underWay && 'diagnose', one.mute ? 'unmute' : 'mute'].filter(Boolean);
  const asked = [];
  await page.route(/\/api\/admin\/feedback(\?.*)?$/, route => {
    asked.push(`GET ${new URL(route.request().url()).search}`);
    return route.fulfill({ json: { feedback: [feedbackRow(sheet), feedbackRow(idea)], total: 2, page: 1, pages: 1, perPage: 20 } });
  });
  await page.route(/\/api\/admin\/feedback\/[^?]+$/, async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace('/api/admin/feedback/', '');
    const body = request.method() === 'GET' || request.method() === 'DELETE' ? null : request.postDataJSON();
    asked.push(`${request.method()} ${path.replace(feedbackReport.id, 'id')}${body && Object.keys(body).length ? ` ${JSON.stringify(body)}` : ''}`);
    if (path.endsWith('/reproduce')) sheet = { ...sheet, reproduction: { id: 'sc-20261116-113500-0a1b2c3d', link: '/battle?scenario=sc-20261116-113500-0a1b2c3d' } };
    if (path.endsWith('/kind')) sheet = { ...sheet, kind: body.kind, status: 'seen' };
    if (path.endsWith('/status')) sheet = { ...sheet, status: body.status, reason: body.reason ?? null };
    if (path.endsWith('/mute')) sheet = { ...sheet, mute: request.method() === 'POST' ? { at: new Date().toISOString(), until: new Date(Date.now() + 30 * 86400000).toISOString(), by: 'operator', why: 'operator', daysLeft: 30 } : null };
    sheet = { ...sheet, spam: sheet.reason === 'spam', actions: offered(sheet) };
    return route.fulfill({ json: sheet });
  });
  await page.route('**/api/admin/overview', route => route.fulfill({ json: { ...overview, feedback: { new: 2, newBugs: 1, lastDay: 2, full: false } } }));
  page.on('dialog', dialog => dialog.accept());
  await page.reload();

  await expect(page.locator('[data-card="feedback"]')).toHaveClass(/warning/);
  await expect(page.locator('[data-card="feedback"]')).toContainText('1 bug report new · 2 sent in 24 hours');
  await page.locator('#tab-feedback').click();
  await expect(page.locator('#admin-feedback-list li')).toHaveCount(2);
  await expect(page.locator('#admin-feedback-list li').first()).toHaveClass(/warning/);
  await expect(page.locator('#admin-feedback-list li').first()).toContainText('Bug report · The rules did something wrong');
  await expect(page.locator('#admin-feedback-list li').nth(1)).toContainText('Idea · The lobby and friends');
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(widthOf(page));
  await page.screenshot({ path: info.outputPath('admin-feedback.png'), fullPage: true });

  await page.locator('#admin-feedback-list li').first().locator('button').click();
  await expect(page.locator('#admin-feedback-sheet-kind')).toHaveText('Bug report · New');
  await expect(page.locator('#admin-feedback-words')).toContainText('One damage in the upkeep.');
  await expect(page.locator('#admin-feedback-facts')).toContainText('Table 20261116-110000-a-long-table-id, round 3 · player1 · Upkeep · 42 journal entries');
  await expect(page.locator('#admin-feedback-position li').first()).toHaveText('Ravager · 7/10 health · 2 energy · stunned, Bleed');
  await expect(page.locator('#admin-feedback-session')).toHaveAttribute('href', '/session/20261116-110000-a-long-table-id');
  await expect(page.locator('#admin-feedback-diagnose')).toBeVisible();
  await expect(page.locator('#admin-feedback-to-bug')).toBeHidden();
  expect(await page.evaluate(() => { const body = document.querySelector('#admin-feedback-sheet .home-dialog-body'); return body.scrollWidth <= body.clientWidth; })).toBe(true);
  await page.screenshot({ path: info.outputPath('admin-feedback-sheet.png') });

  // A table still played is cut once it is over (#736): the sheet says so instead of offering Reproduce.
  await expect(page.locator('#admin-feedback-reproduce')).toBeHidden();
  await expect(page.locator('#admin-feedback-download-note')).toHaveText('The round can be reproduced once the game it was sent from is over: its scenario would hold what the other seat keeps face down.');
  await page.locator('#admin-feedback-close').click();
  sheet = { ...sheet, underWay: false, reproduceOnceOver: false };
  await page.locator('#admin-feedback-list li').first().locator('button').click();
  await expect(page.locator('#admin-feedback-diagnose')).toBeHidden();
  await expect(page.locator('#admin-feedback-download-note')).toHaveText('Reproduce the round first: its scenario is what the issue carries.');

  await expect(page.locator('#admin-feedback-reproduction')).toBeHidden();
  await page.locator('#admin-feedback-reproduce').click();
  await expect(page.locator('#admin-feedback-sheet-said')).toHaveText('Round 3 is cut, in the reporter\'s seat. Play it from the link.');
  await expect(page.locator('#admin-feedback-reproduction')).toHaveAttribute('href', '/battle?scenario=sc-20261116-113500-0a1b2c3d');

  await page.locator('#admin-feedback-to-idea').click();
  await expect(page.locator('#admin-feedback-sheet-said')).toHaveText('Now an idea. Its words and position are kept.');
  await expect(page.locator('#admin-feedback-sheet-said')).toBeFocused();
  await expect(page.locator('#admin-feedback-sheet-kind')).toHaveText('Idea · Seen');
  await expect(page.locator('#admin-feedback-to-bug')).toBeVisible();
  await expect(page.locator('#admin-feedback-reproduced')).toBeHidden();
  await expect(page.locator('#admin-feedback-reproduce')).toBeHidden();

  await page.locator('#admin-feedback-dismiss').click();
  await expect(page.locator('#admin-feedback-sheet-said')).toHaveClass(/refused/);
  await page.locator('#admin-feedback-spam').click();
  await expect(page.locator('#admin-feedback-sheet-kind')).toHaveText('Idea · Dismissed as spam');
  await expect(page.locator('#admin-feedback-spam')).toBeHidden();

  await page.locator('#admin-feedback-mute').click();
  await expect(page.locator('#admin-feedback-facts')).toContainText('This player can send no feedback for 30 more days (by operator).');
  await page.locator('#admin-feedback-unmute').click();
  await expect(page.locator('#admin-feedback-mute')).toBeVisible();

  await page.locator('#admin-feedback-close').click();
  await page.selectOption('#admin-feedback-kind', 'bug');
  await expect.poll(() => asked.at(-1)).toBe('GET ?kind=bug&status=open');
  expect(asked.filter(line => !line.startsWith('GET ?'))).toEqual(['GET id', 'GET id', 'POST id/reproduce', 'PUT id/kind {"kind":"idea"}', 'PUT id/status {"status":"dismissed","reason":"spam"}', 'POST id/mute', 'DELETE id/mute']);
});

// Filing (feedback plan, R5): a reproduced report from a game that is over is downloaded redacted, filed on GitHub's
// new-issue form pre-filled without the player's words or the table's id, marked filed with the issue's address, and
// marked fixed once GitHub says that issue was closed as done.
test('a reproduced report is downloaded, filed as an issue and marked fixed once GitHub closes it', async ({ page }, info) => {
  const repository = 'Downfallz/maintest';
  const scenario = 'sc-20261116-113500-0a1b2c3d';
  let sheet = { ...feedbackReport, underWay: false, repository, issue: null, reproduction: { id: scenario, link: `/battle?scenario=${scenario}` } };
  // What the host offers, as FeedbackAdminApi decides it: the download once reproduced and over, filing while open.
  const offered = one => [one.status !== 'seen' && 'seen', one.status !== 'reproduced' && 'reproduced',
    ...(['fixed', 'dismissed'].includes(one.status) ? [] : ['dismiss', 'spam', 'toIdea', 'file']), 'reproduce', 'download', 'mute'].filter(Boolean);
  sheet = { ...sheet, actions: offered(sheet) };
  const asked = [];
  await page.route(/\/api\/admin\/feedback(\?.*)?$/, route => {
    const search = new URL(route.request().url()).search;
    asked.push(`GET ${search}`);
    return route.fulfill({ json: { feedback: [{ ...feedbackRow(sheet), issue: sheet.issue }], total: 1, page: 1, pages: 1, perPage: 20, repository } });
  });
  await page.route(/\/api\/admin\/feedback\/[^?]+$/, async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace('/api/admin/feedback/', '').replace(feedbackReport.id, 'id');
    const body = request.method() === 'GET' ? null : request.postDataJSON();
    asked.push(`${request.method()} ${path}${body && Object.keys(body).length ? ` ${JSON.stringify(body)}` : ''}`);
    if (path === 'id/scenario.zip') {
      return route.fulfill({ status: 200, contentType: 'application/zip', headers: { 'Content-Disposition': `attachment; filename="report-${feedbackReport.id}.zip"` }, body: Buffer.from('PK\x05\x06' + '\0'.repeat(18), 'binary') });
    }
    if (path === 'id/status') sheet = { ...sheet, status: body.status, issue: body.issue ?? sheet.issue };
    sheet = { ...sheet, actions: offered(sheet) };
    return route.fulfill({ json: sheet });
  });
  const github = [];
  await page.route('https://api.github.com/**', route => {
    github.push(new URL(route.request().url()).pathname);
    return route.fulfill({ headers: { 'Access-Control-Allow-Origin': '*' }, json: [{ number: 731, state: 'closed', state_reason: 'completed' }, { number: 732, state: 'closed', state_reason: 'not_planned' }] });
  });
  await page.reload();

  await page.locator('#tab-feedback').click();
  await page.locator('#admin-feedback-list li').first().locator('button').click();
  await expect(page.locator('#admin-feedback-sheet-kind')).toHaveText('Bug report · New');
  await expect(page.locator('#admin-feedback-filing')).toBeVisible();
  await expect(page.locator('#admin-feedback-download-note')).toBeHidden();
  const issue = await page.locator('#admin-feedback-issue').getAttribute('href');
  expect(issue.startsWith(`https://github.com/${repository}/issues/new?template=bug_from_report.yml&`)).toBe(true);
  expect(decodeURIComponent(issue)).toContain(`Drop report-${feedbackReport.id}.zip here`);
  expect(decodeURIComponent(issue)).not.toContain(feedbackReport.context.table);
  expect(decodeURIComponent(issue)).not.toContain('bleed');
  expect(await page.evaluate(() => { const body = document.querySelector('#admin-feedback-sheet .home-dialog-body'); return body.scrollWidth <= body.clientWidth; })).toBe(true);
  await page.locator('#admin-feedback-filing').scrollIntoViewIfNeeded();
  await page.screenshot({ path: info.outputPath('admin-feedback-filing.png') });

  const saving = page.waitForEvent('download');
  await page.locator('#admin-feedback-download').click();
  expect((await saving).suggestedFilename()).toBe(`report-${feedbackReport.id}.zip`);
  await expect(page.locator('#admin-feedback-sheet-said')).toHaveText(`Saved report-${feedbackReport.id}.zip. Attach it to the issue: GitHub takes a zip, not a .json.`);

  await page.fill('#admin-feedback-issue-number', 'https://github.com/someone/else/issues/731');
  await page.locator('#admin-feedback-filed').click();
  await expect(page.locator('#admin-feedback-sheet-said')).toHaveClass(/refused/);
  await page.fill('#admin-feedback-issue-number', `https://github.com/${repository}/issues/731`);
  await page.locator('#admin-feedback-filed').click();
  await expect(page.locator('#admin-feedback-sheet-said')).toHaveText('Filed as #731. The report reads fixed once the issue is closed.');
  await expect(page.locator('#admin-feedback-sheet-kind')).toHaveText('Bug report · Filed #731');
  await expect(page.locator('#admin-feedback-issue-link')).toHaveAttribute('href', `https://github.com/${repository}/issues/731`);
  expect(await page.evaluate(() => { const body = document.querySelector('#admin-feedback-sheet .home-dialog-body'); return body.scrollWidth <= body.clientWidth; })).toBe(true);
  for (const control of ['#admin-feedback-download', '#admin-feedback-issue-number', '#admin-feedback-filed']) {
    expect((await page.locator(control).boundingBox()).height, control).toBeGreaterThanOrEqual(44);
  }
  await page.screenshot({ path: info.outputPath('admin-feedback-filed.png') });

  // The tab, opened again, asks GitHub whether the filed issues are closed, and marks fixed the one closed as done.
  await page.locator('#admin-feedback-close').click();
  await page.evaluate(() => { location.hash = '#overview'; });
  await page.locator('#tab-feedback').click();
  await expect.poll(() => sheet.status).toBe('fixed');
  expect(github).toEqual([`/repos/${repository}/issues`]);
  await expect(page.locator('#admin-feedback-list li').first()).toContainText('Fixed');
  expect(asked.filter(line => !line.startsWith('GET ?') && line !== 'GET id')).toEqual(['POST id/scenario.zip', 'PUT id/status {"status":"filed","issue":731}', 'PUT id/status {"status":"fixed"}']);
  expect(asked).toContain('GET ?kind=bug&status=open&filed=true');
});

// Ideas grouped (feedback plan, I1): the ideas listed by how many asked, one merged into another, the group read on
// one sheet, planned with its plan's path, and copied as a plan prompt in the operator's own words.
test('an idea is merged into another, read with everyone who asked it, planned, and copied as a plan prompt', async ({ page, context }, info) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']).catch(() => {});
  const ago = minutes => new Date(Date.now() - minutes * 60000).toISOString();
  const base = { kind: 'idea', category: 'battle', reason: null, plan: null, expected: null, additions: [], page: '/battle', browser: 'Firefox/140',
    errors: [], account: null, guest: true, senderGone: false, mute: null, context: null, session: null, underWay: false, asks: [], mergedInto: null };
  const ideas = {
    replay: { ...base, id: '20261116T090000000Z-aaa111', status: 'seen', at: ago(600), text: 'I wanted to see the last round again but could not find a way to.' },
    watch: { ...base, id: '20261116T100000000Z-bbb222', status: 'new', at: ago(120), text: 'Let me watch a round again from the recap, it went too fast to follow.' },
    names: { ...base, id: '20261116T110000000Z-ccc333', status: 'new', at: ago(60), text: 'I wanted to invite a friend by name, not by a code.', category: 'lobby' },
  };
  const groupOf = id => Object.values(ideas).filter(one => one.mergedInto?.id === id);
  // The sheet as FeedbackAdminApi answers it: how many asked, the ideas merged into it, and the actions that apply.
  const sheetOf = one => {
    if (one.mergedInto) return { ...one, askedBy: null, since: null, actions: ['unmerge', 'dismiss', 'spam', 'mute'] };
    const asks = groupOf(one.id);
    const closed = ['shipped', 'declined', 'dismissed'].includes(one.status);
    return { ...one, askedBy: 1 + asks.length, since: one.at, asks: asks.map(ask => ({ id: ask.id, at: ask.at, text: ask.text, expected: null, additions: [], account: null, guest: true, senderGone: false })),
      actions: [one.status !== 'seen' && 'seen', 'planned', one.status !== 'shipped' && 'shipped', ...(closed ? [] : ['dismiss', 'spam', 'declined', asks.length ? null : 'toBug', 'merge']), 'prompt', 'mute'].filter(Boolean) };
  };
  const asked = [];
  await page.route(/\/api\/admin\/feedback(\?.*)?$/, route => {
    const listed = Object.values(ideas).filter(one => !one.mergedInto).map(sheetOf)
      .sort((a, b) => b.askedBy - a.askedBy || Date.parse(b.at) - Date.parse(a.at))
      .map(one => ({ ...feedbackRow(one), plan: one.plan, askedBy: one.askedBy }));
    return route.fulfill({ json: { feedback: listed, total: listed.length, page: 1, pages: 1, perPage: 20 } });
  });
  await page.route(/\/api\/admin\/feedback\/[^?]+$/, route => {
    const request = route.request();
    const [id, change] = new URL(request.url()).pathname.replace('/api/admin/feedback/', '').split('/');
    const one = Object.values(ideas).find(idea => idea.id === id);
    const body = ['GET', 'DELETE'].includes(request.method()) ? null : request.postDataJSON();
    if (change) asked.push(`${request.method()} ${change} ${JSON.stringify(body)}`);
    if (change === 'merge' && request.method() === 'DELETE') Object.assign(one, { mergedInto: null });
    else if (change === 'merge') Object.assign(one, { mergedInto: { id: body.into, line: ideas.replay.text, status: 'seen' }, status: 'seen' });
    if (change === 'status') Object.assign(one, { status: body.status, plan: body.plan ?? null });
    return route.fulfill({ json: sheetOf(one) });
  });
  page.on('dialog', dialog => dialog.accept());
  await page.reload();

  await page.locator('#tab-feedback').click();
  await page.selectOption('#admin-feedback-kind', 'idea');
  await expect(page.locator('#admin-feedback-list li')).toHaveCount(3);
  await page.locator('#admin-feedback-list li', { hasText: 'Let me watch' }).locator('button').click();
  await expect(page.locator('#admin-feedback-merge-pick')).toBeHidden();
  await page.locator('#admin-feedback-merge').click();
  await expect(page.locator('#admin-feedback-merge-into option')).toHaveCount(3);
  await page.selectOption('#admin-feedback-merge-into', ideas.replay.id);
  await page.locator('#admin-feedback-merge-confirm').click();
  await expect(page.locator('#admin-feedback-sheet-said')).toHaveText('Merged. It is read inside the idea it joined.');
  await expect(page.locator('#admin-feedback-facts')).toContainText('Merged into');
  await expect(page.locator('#admin-feedback-merge')).toBeHidden();
  await expect(page.locator('#admin-feedback-spam')).toBeVisible();
  await expect(page.locator('#admin-feedback-unmerge')).toBeVisible();

  await page.locator('#admin-feedback-joined').click();
  await expect(page.locator('#admin-feedback-facts')).toContainText('2 players since');
  await expect(page.locator('#admin-feedback-words')).toContainText('Let me watch a round again from the recap');
  await expect(page.locator('#admin-feedback-to-bug')).toBeHidden();
  await page.locator('#admin-feedback-asks button').click();
  await expect(page.locator('#admin-feedback-spam')).toBeVisible();
  await page.locator('#admin-feedback-joined').click();
  await page.locator('#admin-feedback-close').click();
  await expect(page.locator('#admin-feedback-list li')).toHaveCount(2);
  await expect(page.locator('#admin-feedback-list li').first()).toContainText('asked by 2');

  await page.locator('#admin-feedback-list li').first().locator('button').click();
  await page.fill('#admin-feedback-plan', 'docs/tabletop/replay-plan.md');
  await page.locator('#admin-feedback-planned').click();
  await expect(page.locator('#admin-feedback-sheet-kind')).toHaveText('Idea · Planned: docs/tabletop/replay-plan.md');
  await page.locator('#admin-feedback-prompt').click();
  await expect(page.locator('#admin-feedback-sheet-said')).toHaveClass(/refused/);
  await page.fill('#admin-feedback-prompt-text', 'Let a player watch the last round again');
  await page.locator('#admin-feedback-prompt').click();
  await expect(page.locator('#admin-feedback-prompt-said')).toHaveText(/^\/new-plan Let a player watch the last round again\. Asked by 2 players since \d{4}-\d{2}-\d{2}\.$/);
  expect(await page.evaluate(() => { const body = document.querySelector('#admin-feedback-sheet .home-dialog-body'); return body.scrollWidth <= body.clientWidth; })).toBe(true);
  await page.screenshot({ path: info.outputPath('admin-feedback-idea.png') });

  // A merge made by mistake is undone from the merged idea's sheet (#725): it is listed again as an idea of its own.
  await page.locator('#admin-feedback-asks button').click();
  await page.locator('#admin-feedback-unmerge').click();
  await expect(page.locator('#admin-feedback-sheet-said')).toHaveText('Unmerged. It is listed again as an idea of its own.');
  await expect(page.locator('#admin-feedback-unmerge')).toBeHidden();
  await expect(page.locator('#admin-feedback-merge')).toBeVisible();
  await page.locator('#admin-feedback-close').click();
  await expect(page.locator('#admin-feedback-list li')).toHaveCount(3);

  expect(asked).toEqual([`POST merge {"into":"${ideas.replay.id}"}`, 'PUT status {"status":"planned","plan":"docs/tabletop/replay-plan.md"}', 'DELETE merge null']);
});

// The Jobs tab (A3): the tending runs, the totals over them, a pass run now and a run's sheet.
const tendRun = (hoursAgo, extra = {}) => {
  const started = new Date(Date.parse('2026-11-13T18:00:00Z') - hoursAgo * 3600000);
  const stamp = started.toISOString().replace(/[-:.]/g, '').replace('T', 'T').slice(0, 18) + 'Z';
  return { id: `${stamp}-${String(hoursAgo).padStart(6, '0')}`, started: started.toISOString(), finished: started.toISOString(), seconds: 2.4,
    trigger: 'schedule', by: null, outcome: 'completed', error: null, neverBegan: [], invitationsExpired: 0, timedOut: [], abandoned: [], warned: [], ...extra };
};

async function withJobs(page) {
  const runs = [tendRun(0, { neverBegan: ['20261113-a-table-that-never-began-with-a-long-id'], timedOut: ['late-table'] }),
    tendRun(1, { outcome: 'failed', error: 'The storage account did not answer.' }),
    ...Array.from({ length: 23 }, (_, index) => tendRun(index + 2))];
  const totals = { runs: 25, failed: 1, neverBegan: 1, invitationsExpired: 0, timedOut: 1, abandoned: 0, warned: 0 };
  await page.route(/\/api\/admin\/jobs\/tend(\?.*)?$/, route => {
    if (route.request().method() === 'POST') {
      const made = tendRun(-1, { trigger: 'manual', by: 'operator', warned: ['warned-table'] });
      runs.unshift(made);
      return route.fulfill({ json: { run: made } });
    }
    const number = Number(new URL(route.request().url()).searchParams.get('page') ?? 1);
    const pages = Math.ceil(runs.length / 20);
    return route.fulfill({ json: { runs: runs.slice((number - 1) * 20, number * 20), total: runs.length, page: number, pages, perPage: 20, day: totals, week: totals } });
  });
  await page.route(/\/api\/admin\/jobs\/tend\/[^/?]+$/, route => {
    const id = decodeURIComponent(new URL(route.request().url()).pathname.split('/').pop());
    return route.fulfill({ json: { run: runs.find(one => one.id === id) } });
  });
  await page.reload();
}

test('a host that records nothing shows no Jobs tab', async ({ page }) => {
  await expect(page.locator('#tab-jobs')).toBeHidden();
  await page.goto('/table/admin.html#jobs');
  await expect(page.locator('#jobs-off')).toBeVisible();
});

test('the tending runs are listed with their totals, and a failed one stands out', async ({ page }, info) => {
  await withJobs(page);
  await page.locator('#tab-jobs').click();

  await expect(page.locator('#jobs-runs li')).toHaveCount(20);
  await expect(page.locator('#jobs-count')).toHaveText('25 runs kept · page 1 of 2');
  await expect(page.locator('#jobs-day')).toHaveText(/^In 24 hours: 25 runs, 1 table that never began closed, 1 game timed out/);
  await expect(page.locator('#jobs-day')).toHaveClass(/warning/);
  await expect(page.locator('#tab-jobs-count')).toHaveText('!');
  await expect(page.locator('#jobs-runs li.failed')).toHaveText(/The storage account did not answer\./);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(widthOf(page));
  await page.screenshot({ path: info.outputPath('admin-jobs.png'), fullPage: true });

  await page.locator('#jobs-next').click();
  await expect(page.locator('#jobs-runs li')).toHaveCount(5);
});

test('a pass run now is confirmed, then listed first, and a run\'s sheet links its tables to their sessions', async ({ page }, info) => {
  await withJobs(page);
  await page.locator('#tab-jobs').click();

  await page.locator('#jobs-pass').click();
  await expect(page.locator('#jobs-pass-confirm')).toBeVisible();
  await page.locator('#jobs-pass-yes').click();
  await expect(page.locator('#jobs-said')).toHaveText('Pass done: 1 player warned.');
  await expect(page.locator('#jobs-runs li').first()).toContainText('By hand, operator');

  await page.locator('#jobs-runs li').nth(1).locator('button').click();
  const sheet = page.locator('#run-sheet');
  await expect(sheet).toBeVisible();
  await expect(sheet.locator('#run-tables li')).toHaveCount(2);
  await expect(sheet.locator('#run-tables a').first()).toHaveAttribute('href', '/session/20261113-a-table-that-never-began-with-a-long-id');
  const box = await sheet.boundingBox();
  expect(box.x + box.width).toBeLessThanOrEqual(widthOf(page) + 1);
  await page.screenshot({ path: info.outputPath('admin-run-sheet.png') });
  await sheet.locator('#run-sheet-close').click();
  await expect(sheet).toBeHidden();
});

// The bots' turns in the Jobs tab (A3b): the three queues, the dead letters, requeue, drop and a letter's sheet.
async function withTurns(page) {
  const letter = (index, extra = {}) => ({ id: `letter-${index}`, table: `table-${index}`, queue: 'agent-turns', delivered: 5,
    error: 'The journal could not be read.', fullError: 'The journal could not be read.\n   at Journal.ReadAsync()', queuedAt: '2026-11-13T17:00:00Z',
    poisonedAt: '2026-11-13T17:01:00Z', deadLetteredAt: '2026-11-13T17:02:00Z', host: 'worker-1', body: `{"run":"table-${index}"}`, ...extra });
  let letters = [letter(1, { table: 'a-table-with-a-very-long-session-id-20261113-172233-abcdef' }), letter(2), letter(3)];
  const asked = [];
  await page.route(/\/api\/admin\/jobs\/turns(\/[a-z]+)?$/, route => {
    const action = new URL(route.request().url()).pathname.split('/').pop();
    if (route.request().method() === 'POST') {
      const body = route.request().postDataJSON();
      asked.push({ action, body });
      const ids = body.all ? letters.map(one => one.id) : body.ids;
      letters = letters.filter(one => !ids.includes(one.id));
      return route.fulfill({ json: { handled: ids, missing: [] } });
    }
    return route.fulfill({ json: { queues: [{ name: 'agent-turns', count: 1, oldest: '2026-11-13T19:00:00Z' }, { name: 'agent-turns-poison', count: 0, oldest: null },
      { name: 'agent-turns-dlq', count: letters.length, oldest: letters.length ? '2026-11-13T17:00:00Z' : null }], letters, shownAtMost: 32 } });
  });
  await page.reload();
  return asked;
}

test('the bots\' turn queues show in the Jobs tab, and their dead letters are requeued and dropped', async ({ page }, info) => {
  const asked = await withTurns(page);
  await page.locator('#tab-jobs').click();

  await expect(page.locator('#jobs-off')).toBeHidden();
  await expect(page.locator('#turns-queues li')).toHaveCount(3);
  await expect(page.locator('#turns-queues li.warning')).toHaveText(/3\s*Dead letters/);
  await expect(page.locator('#tab-jobs-count')).toHaveText('!');
  await expect(page.locator('#turns-letters li')).toHaveCount(3);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(widthOf(page));
  await page.locator('#turns-on').screenshot({ path: info.outputPath('admin-turns.png') });

  await page.locator('#turns-letters input').first().check();
  await page.locator('#turns-requeue').click();
  await expect(page.locator('#turns-said')).toHaveText('1 letter requeued.');
  expect(asked.at(-1)).toEqual({ action: 'requeue', body: { ids: ['letter-1'] } });
  await expect(page.locator('#turns-letters li')).toHaveCount(2);

  await page.locator('#turns-letters input').first().check();
  await page.locator('#turns-drop').click();
  await expect(page.locator('#turns-confirm')).toBeVisible();
  await page.locator('#turns-drop-yes').click();
  await expect(page.locator('#turns-said')).toHaveText('1 letter dropped.');
  expect(asked.at(-1)).toEqual({ action: 'drop', body: { ids: ['letter-2'] } });

  await page.locator('#turns-letters .player-open').first().click();
  await expect(page.locator('#letter-sheet')).toBeVisible();
  await expect(page.locator('#letter-error')).toContainText('at Journal.ReadAsync()');
  await expect(page.locator('#letter-session')).toHaveAttribute('href', '/session/table-3');
  await page.locator('#letter-requeue').click();
  await expect(page.locator('#letter-sheet')).toBeHidden();
  await expect(page.locator('#turns-empty')).toBeVisible();
  await expect(page.locator('#tab-jobs-count')).toHaveText('');
});

// The notices in the Jobs tab (notifications plan, N6): the three queues and the worker's counts, a kind each.
test('the notices\' queues and counts show in the Jobs tab, and a notice given up on marks the tab', async ({ page }, info) => {
  await page.route(/\/api\/admin\/jobs\/notices$/, route => route.fulfill({ json: {
    queues: [{ name: 'notices', count: 0, oldest: null }, { name: 'notices-poison', count: 0, oldest: null }, { name: 'notices-dlq', count: 1, oldest: '2026-11-13T17:00:00Z' }],
    counted: true, today: '2026-11-13', days: 7,
    day: [{ kind: 'turn', sent: 12, refused: 1, gone: 0 }],
    week: [{ kind: 'turn', sent: 80, refused: 6, gone: 2 }, { kind: 'invitation', sent: 9, refused: 0, gone: 1 }, { kind: 'match', sent: 30, refused: 0, gone: 0 }],
  } }));
  await page.reload();
  await page.locator('#tab-jobs').click();

  await expect(page.locator('#notices-on')).toBeVisible();
  await expect(page.locator('#notices-queues li.warning')).toHaveText(/1\s*Given up on, kept a week/);
  await expect(page.locator('#tab-jobs-count')).toHaveText('!');
  await expect(page.locator('#notices-counts tr')).toHaveCount(4);
  await expect(page.locator('#notices-counts tr').last()).toHaveText(/All\s*12\s*1\s*0\s*119\s*6\s*3/);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(widthOf(page));
  await page.locator('#notices-on').screenshot({ path: info.outputPath('admin-notices.png') });
});

// Voiding a match (A3c, ADR 0176): from a session row, only with a category and an explanation, and the session then
// says it is void.
test('a session is voided only with a reason, and then says it is void', async ({ page }, info) => {
  let voided = null;
  const listed = () => ({ sessions: [{ ...session('people-finished', 'human:mk', 'human:jo', true), voidable: !voided, voided: voided?.category ?? null }] });
  await page.route('**/api/sessions', route => route.fulfill({ json: listed() }));
  await page.route(/\/api\/tables\/people-finished\/void$/, route => {
    voided = route.request().postDataJSON();
    return route.fulfill({ json: { voided: true, withdrawn: true, category: voided.category } });
  });
  await page.reload();
  await page.locator('#tab-sessions').click();

  await page.locator('#sessions .danger-link').click();
  const sheet = page.locator('#void-sheet');
  await expect(sheet).toBeVisible();
  await expect(sheet.locator('#void-sheet-name')).toHaveText('people-finished');
  await expect(sheet.locator('#void-warning')).toContainText('Its result is withdrawn');
  await expect(sheet.locator('#void-submit')).toBeDisabled();
  await sheet.locator('#void-category').selectOption('abuse');
  await sheet.locator('#void-explanation').fill('short');
  await expect(sheet.locator('#void-submit')).toBeDisabled();
  await sheet.locator('#void-explanation').fill('A player won through a bug in the order of the slots.');
  await expect(sheet.locator('#void-submit')).toBeEnabled();
  const box = await sheet.boundingBox();
  expect(box.x + box.width).toBeLessThanOrEqual(widthOf(page) + 1);
  await page.screenshot({ path: info.outputPath('admin-void-sheet.png') });

  await sheet.locator('#void-submit').click();
  await expect(sheet).toBeHidden();
  expect(voided).toEqual({ category: 'abuse', explanation: 'A player won through a bug in the order of the slots.' });
  await expect(page.locator('#sessions-said')).toHaveText('people-finished voided; its result is withdrawn.');
  await expect(page.locator('#sessions li').first()).toContainText('void: a player abused the game');
  await expect(page.locator('#sessions .danger-link')).toHaveCount(0);
});

test('a host that signs nobody in shows no Settings tab', async ({ page }) => {
  await expect(page.locator('#tab-settings')).toBeHidden();
  await page.goto('/table/admin.html#settings');
  await expect(page.locator('#settings-off')).toBeVisible();
  await expect(page.locator('#host-notice')).toBeHidden();
});

// The host's settings (ADR 0177) as a host that signs players in keeps them, and /api/me carrying their notice.
async function withSettings(page) {
  const state = { settings: { registrationOpen: true, notice: null, updatedBy: null, updatedAt: null }, puts: [] };
  await page.route('**/api/me', route => route.fulfill({ json: { operator: true, admin: '/admin', accounts: 'signin', signIn: [{ provider: 'google', clientId: 'id' }], account: null, notice: state.settings.notice } }));
  await page.route('**/api/admin/settings', route => {
    if (route.request().method() === 'PUT') {
      const asked = route.request().postDataJSON();
      state.puts.push(asked);
      state.settings = { registrationOpen: asked.registrationOpen, notice: asked.notice.trim() || null, updatedBy: 'operator', updatedAt: new Date().toISOString() };
    }
    return route.fulfill({ json: { settings: state.settings, longestNotice: 200 } });
  });
  await page.reload();
  return state;
}

test('the Settings tab closes registration and posts a notice, which every page then shows under its header', async ({ page }, info) => {
  const state = await withSettings(page);
  await page.locator('#tab-settings').click();

  await expect(page.locator('#settings-open')).toBeChecked();
  await expect(page.locator('#settings-saved')).toHaveText('Never saved: the defaults hold, registration open and no notice.');
  await page.locator('#settings-closed').check();
  await expect(page.locator('#settings-registration-line')).toHaveText(/^Closed: /);
  await page.locator('#settings-notice').fill('a'.repeat(201).slice(0, 200));
  await expect(page.locator('#settings-notice-count')).toHaveText('200 / 200');
  await page.locator('#settings-notice').fill('Maintenance tonight at 22:00 UTC.');
  await page.locator('#settings-save').click();

  await expect(page.locator('#settings-said')).toHaveText('Saved. Registration is closed, and the notice shows under every page\'s header.');
  expect(state.puts).toEqual([{ registrationOpen: false, notice: 'Maintenance tonight at 22:00 UTC.' }]);
  await expect(page.locator('#tab-settings-count')).toHaveText('!');
  await expect(page.locator('#settings-saved')).toHaveText('Last saved just now by operator.');
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(widthOf(page));
  await page.screenshot({ path: info.outputPath('admin-settings.png'), fullPage: true });

  await page.reload();
  await expect(page.locator('#host-notice')).toHaveText('Maintenance tonight at 22:00 UTC.');
  await expect(page.locator('#host-notice')).toBeVisible();
  await expect(page.locator('#settings-closed')).toBeChecked();
});

// The panel's own sheets look like the header's dialogs; before this was fixed the header stopped on them while
// mounting, so it never read who was signed in.
test('the shared header reads who is signed in on the admin page and links the operator to it', async ({ page }) => {
  // Unhidden by the header once /api/me says the operator; on a phone it waits in the closed menu.
  await expect.poll(() => page.locator('#nav-admin-link').evaluate(link => link.hidden)).toBe(false);
  await expect(page.locator('#nav-admin-link')).toHaveAttribute('aria-current', 'page');
});

// Match scenarios (ADR 0179): a recorded session cut at the top of a round from its row, then listed with its link.
test('a session is cut at a round from its row, and the scenario is listed with the link a player opens', async ({ page }, info) => {
  const kept = [];
  const posted = [];
  await page.route('**/api/admin/scenarios**', async route => {
    const req = route.request(), url = new URL(req.url());
    if (url.pathname.endsWith('/rounds')) return route.fulfill({ json: { session: url.searchParams.get('session'), rounds: [1, 2, 3, 4], stoppedBy: null } });
    if (req.method() === 'POST') {
      const asked = req.postDataJSON(); posted.push(asked);
      const made = { id: 'sc-1', title: asked.title, goal: asked.goal, session: asked.session, round: asked.round, seat: asked.seat, content: 'abc', link: '/battle?scenario=sc-1' };
      kept.unshift(made);
      return route.fulfill({ status: 201, json: { id: made.id, link: made.link } });
    }
    return route.fulfill({ json: { scenarios: kept } });
  });
  // A table still played, by its record, whichever process holds it, is cut once it is over (#736).
  await page.route('**/api/sessions', route => route.fulfill({ json: { sessions: [{ ...session('people-playing', 'human:mk', 'human:jo', false), underWay: true }, ...sessions] } }));
  await page.reload();
  await expect(page.locator('#tab-scenarios')).toBeVisible();
  await page.locator('#tab-sessions').click();
  await expect(page.locator('#sessions li', { hasText: 'people-playing' })).toBeVisible();
  await expect(page.locator('#sessions li', { hasText: 'people-playing' }).locator('.link-button', { hasText: 'practice from a round' })).toHaveCount(0);

  await page.locator('#sessions li', { hasText: 'people-finished' }).locator('.link-button', { hasText: 'practice from a round' }).click();
  await expect(page.locator('#scenario-round option')).toHaveCount(4);
  await page.selectOption('#scenario-round', '3');
  await page.selectOption('#scenario-seat', 'player2');
  await page.fill('#scenario-title', 'Bleed after a stun');
  await page.locator('#scenario-submit').click();

  await expect(page.locator('#scenario-made code')).toHaveText(/\/battle\?scenario=sc-1$/);
  expect(posted).toEqual([{ session: 'people-finished', round: 3, seat: 'player2', title: 'Bleed after a stun' }]);
  await page.screenshot({ path: info.outputPath('scenario-sheet.png') });
  await page.locator('#scenario-cancel').click();
  await page.locator('#tab-scenarios').click();
  await expect(page.locator('#scenarios li')).toHaveCount(1);
  await expect(page.locator('#scenarios li')).toContainText('Bleed after a stun');
  await expect(page.locator('#scenarios li a', { hasText: 'play' })).toHaveAttribute('href', '/battle?scenario=sc-1');
  await expect(page.locator('#tab-scenarios-count')).toHaveText('1');
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(widthOf(page));
});

// Listed under Practice (scenarios plan, S6): the operator lists a scenario for signed-in players, or takes it off.
test('a scenario is listed under Practice from its row, and taken off again', async ({ page }, info) => {
  const kept = [{ id: 'sc-1', title: 'Bleed after a stun', session: 'people-finished', round: 3, seat: 'player2', content: 'abc', link: '/battle?scenario=sc-1', listed: false }];
  const put = [];
  await page.route('**/api/admin/scenarios**', async route => {
    const req = route.request(), url = new URL(req.url());
    if (req.method() === 'PUT' && url.pathname === '/api/admin/scenarios/sc-1/listing') {
      put.push(req.postDataJSON()); kept[0].listed = req.postDataJSON().listed;
      return route.fulfill({ json: { id: 'sc-1', listed: kept[0].listed } });
    }
    return route.fulfill({ json: { scenarios: kept } });
  });
  await page.reload();
  await page.locator('#tab-scenarios').click();
  const row = page.locator('#scenarios li', { hasText: 'Bleed after a stun' });

  await row.locator('.link-button', { hasText: 'list under Practice' }).click();
  await expect(row.locator('.status-tag.listed')).toHaveText('Listed under Practice');
  await expect(page.locator('#scenarios-said')).toContainText('is listed under Practice');
  await page.screenshot({ path: info.outputPath('scenario-listed.png') });
  await row.locator('.link-button', { hasText: 'take off Practice' }).click();
  await expect(row.locator('.status-tag.listed')).toHaveCount(0);

  expect(put).toEqual([{ listed: true }, { listed: false }]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(widthOf(page));
});

// The repository's scenarios (scenarios plan, S7): listed with their file, played from the row, never removed or listed here.
test('a scenario the repository ships is played from its row, and is neither listed nor removed there', async ({ page }, info) => {
  await page.route('**/api/admin/scenarios**', route => route.fulfill({ json: { scenarios: [
    { id: 'sc-repo-greedy-1v1-round-3', title: 'A greedy 1v1 mirror, round 3', goal: 'A test scenario.', session: 'elsewhere', round: 3, seat: 'player1', content: 'abc', listed: false, source: 'repository', file: 'greedy-1v1-round-3.json', link: '/battle?scenario=sc-repo-greedy-1v1-round-3' },
    { id: 'sc-1', title: 'Bleed after a stun', session: 'people-finished', round: 3, seat: 'player2', content: 'abc', listed: false, source: 'cut', file: null, link: '/battle?scenario=sc-1' },
  ] } }));
  await page.reload();
  await page.locator('#tab-scenarios').click();
  const shipped = page.locator('#scenarios li[data-source="repository"]');
  await expect(shipped).toContainText('Repository');
  await expect(shipped).toContainText('scenarios/greedy-1v1-round-3.json');
  await expect(shipped.locator('a', { hasText: 'play' })).toHaveAttribute('href', '/battle?scenario=sc-repo-greedy-1v1-round-3');
  await expect(shipped.locator('button', { hasText: 'remove' })).toHaveCount(0);
  await expect(shipped.locator('button[data-listing]')).toHaveCount(0);
  await expect(page.locator('#scenarios li', { hasText: 'Bleed after a stun' }).locator('button', { hasText: 'remove' })).toHaveCount(1);
  await page.screenshot({ path: info.outputPath('scenario-repository.png') });
});

test('a host that keeps no scenarios shows no Scenarios tab', async ({ page }) => {
  await page.route('**/api/admin/scenarios**', route => route.fulfill({ status: 404, body: 'This host keeps no scenarios.' }));
  await page.reload();
  await expect(page.locator('#tab-sessions')).toBeVisible();
  await expect(page.locator('#tab-scenarios')).toBeHidden();
});

// The diagnosis of a table in progress (scenarios plan, S5b): its facts from its card, a replay check, what its seats
// hold on request, its bot turn owed again, and the Void sheet one button away.
test('a table in progress is diagnosed from its card, and the Void sheet is one button away', async ({ page }, info) => {
  const asked = [];
  await page.route('**/api/tables', route => route.fulfill({ json: { ...tables, tables: [table(1)] } }));
  await page.route('**/api/tables/table-01/diagnosis**', async route => {
    const url = new URL(route.request().url()); asked.push(`${route.request().method()} ${url.pathname.replace('/api/tables/table-01/diagnosis', '') || '/'}`);
    if (url.pathname.endsWith('/replay')) return route.fulfill({ json: { replays: false, message: 'Line 41 of the journal does not answer the match being rebuilt.' } });
    if (url.pathname.endsWith('/hidden')) return route.fulfill({ json: { seats: [{ slot: 'player1', speeds: [{ creature: 1, speed: 'Quick' }], intents: [], picks: [], held: [] }, { slot: 'player2', speeds: [], intents: [], picks: [], held: [] }] } });
    if (url.pathname.endsWith('/turn')) return route.fulfill({ json: { message: 'The bots\' turn is owed again.' } });
    return route.fulfill({ json: { id: 'table-01', status: 'open', held: true, begun: true, over: false, round: 3,
      seats: [{ slot: 'player1', name: 'human:mk', kind: 'person', playedByBot: false, asked: null }, { slot: 'player2', name: 'Greedy', kind: 'bot', playedByBot: true, asked: { kind: 'Action', creature: 5 } }],
      journal: { entries: 41, lastWritten: '2026-11-14T04:00:00Z', tail: [{ kind: 'Speed', seat: 'player1' }] }, basis: { content: 'f3e8a42ddbd02c11', rules: '3v3', engine: 'abc' }, turns: { queued: true, letters: 1, lastError: 'Timeout' } } });
  });
  page.on('dialog', dialog => dialog.accept());
  await page.reload();
  await page.locator('#tab-tables').click();

  await page.locator('#tables button', { hasText: 'Diagnose…' }).first().click();
  await expect(page.locator('#diagnosis-sheet-name')).toHaveText('table-01');
  await expect(page.locator('#diagnosis-facts')).toContainText('player2: Greedy (bot) · asked Action for creature 5');
  await expect(page.locator('#diagnosis-facts')).toContainText('1 dead letter · last letter: Timeout');
  await page.locator('#diagnosis-replay').click();
  await expect(page.locator('#diagnosis-said')).toHaveText('Line 41 of the journal does not answer the match being rebuilt.');
  await expect(page.locator('#diagnosis-said')).toHaveClass(/refused/);
  await page.locator('#diagnosis-reveal').click();
  await expect(page.locator('#diagnosis-hidden li').first()).toHaveText('player1: creature 1 Quick');
  await page.locator('#diagnosis-turn').click();
  await expect(page.locator('#diagnosis-said')).toHaveText('The bots\' turn is owed again.');
  await page.screenshot({ path: info.outputPath('diagnosis-sheet.png') });
  await page.locator('#diagnosis-void').click();
  await expect(page.locator('#void-sheet-name')).toHaveText('table-01');
  expect(asked).toEqual(['GET /', 'POST /replay', 'POST /hidden', 'POST /turn', 'GET /']);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(widthOf(page));
});
