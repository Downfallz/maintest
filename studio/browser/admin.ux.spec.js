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

test.beforeEach(async ({ page }) => {
  // The shared header asks who is signed in: the operator, on a host where players do not sign in.
  await page.route('**/api/me', route => route.fulfill({ json: { operator: true, admin: '/admin', accounts: 'off', account: null } }));
  await page.route('**/api/tables', route => route.fulfill({ json: tables }));
  await page.route('**/api/sessions', route => route.fulfill({ json: { sessions } }));
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

test('the headquarters open on Tables under the shared header, and each section is part of the address', async ({ page }, info) => {
  await expect(page.locator('header[data-navigation="admin"] #nav-admin-link')).toHaveAttribute('aria-current', 'page');
  await expect(page.locator('#tab-tables')).toHaveAttribute('aria-current', 'page');
  await expect(page.locator('#panel-tables')).toBeVisible(); await expect(page.locator('#panel-sessions')).toBeHidden();
  await expect(page.locator('#tab-sessions-count')).toHaveText('25');

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

test('the tables under way are paged twenty at a time, the page kept across the host\'s polls', async ({ page }) => {
  await page.route('**/api/tables', route => route.fulfill({ json: { ...tables, tables: Array.from({ length: 25 }, (_, index) => table(index)) } }));
  await page.reload();
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
