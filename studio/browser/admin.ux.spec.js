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
