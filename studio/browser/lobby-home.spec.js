import { test, expect } from '@playwright/test';

const player = { id: 'acct_aaaaaaaaaaaaaaaa', username: 'MarcF' };
const friend = { id: 'acct_bbbbbbbbbbbbbbbb', username: 'Bastien' };
const challenger = { id: 'acct_cccccccccccccccc', username: 'Alex' };
const battle = {
  id: 'battle-one', format: '3v3', over: false, waiting: false, openSeats: 0, round: 3,
  seats: [{ seat: 'player1', kind: 'person', name: 'MarcF', turn: true }, { seat: 'player2', kind: 'person', name: 'Bastien' }],
};
const held = { table: battle, seats: [{ seat: 'player1', token: 'seat-token' }] };

async function home(page, { account = player, mode = 'dev', delayMine = false, operator = false, meDown = false } = {}) {
  const state = { account, mode, meDown, offline: false, mineReads: 0, friendReads: 0, privateReads: [], release: null,
    friends: { friends: [friend], incoming: [challenger], outgoing: [] } };
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  state.errors = errors;
  await page.addInitScript(() => {
    window.permissionRequests = 0;
    if (window.Notification) Notification.requestPermission = async () => { window.permissionRequests++; return 'denied'; };
    window.google = { accounts: { id: { initialize() {}, renderButton(el) {
      const button = document.createElement('button'); button.textContent = 'Continue with Google'; el.append(button);
    } } } };
  });
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    const json = value => route.fulfill({ json: value });
    if (path === '/api/me' && state.meDown) return route.fulfill({ status: 503, json: { message: 'Restarting' } });
    if (path === '/api/me') return json({ accounts: state.mode, account: state.account, googleClientId: 'test-client', operator });
    if (path === '/api/account/signout') { state.signouts ??= []; state.signouts.push(route.request().postDataJSON()); state.account = null; return json({}); }
    if (path === '/api/account/dev') { state.account = player; return json({}); }
    if (path === '/api/lobby') {
      if (state.offline) return route.fulfill({ status: 503, json: { message: 'Offline' } });
      return json({ recording: true, format: '3v3', formats: ['3v3', '2v2', '1v1'], tables: [battle] });
    }
    if (['/api/lobby/adopt', '/api/lobby/mine', '/api/lobby/resume'].includes(path)) {
      state.privateReads.push(path); state.mineReads++;
      const tables = state.account || state.mode === 'off' ? [held] : [];
      if (delayMine && state.mineReads === 1) await new Promise(resolve => { state.release = resolve; });
      return json({ tables, invitations: [] });
    }
    if (path === `/api/friends/${friend.id}` && route.request().method() === 'DELETE') {
      state.removals = (state.removals ?? 0) + 1;
      state.friends = { ...state.friends, friends: [] }; return json({});
    }
    if (path === '/api/friends') { state.friendReads++; return json(state.friends); }
    if (path === `/api/friends/requests/${challenger.id}/accept`) {
      state.friends = { ...state.friends, friends: [friend, challenger], incoming: [] }; return json({});
    }
    if (path === '/api/lobby/push') return json({ publicKey: null });
    return route.fulfill({ status: 404, json: { message: `Unexpected ${path}` } });
  });
  await page.goto('/table/lobby.html');
  await expect(page.locator('body')).toHaveAttribute('data-home', account ? 'member' : mode === 'off' ? 'local' : 'guest');
  return state;
}

async function openNavigation(page) {
  const toggle = page.getByRole('button', { name: 'Open navigation', exact: true });
  if (await toggle.isVisible()) await toggle.click();
}
async function openPanel(page, name) {
  await openNavigation(page);
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name }).click();
}
async function noOverflow(page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
}

test('a guest sees an introduction and sign-in, with secondary details in the menu', async ({ page }, testInfo) => {
  const state = await home(page, { account: null, mode: 'google' });
  await expect(page.getByRole('heading', { name: 'Build your creatures. Outplay your rival.' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Continue with Google' })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('home-guest.jpg'), fullPage: true, type: 'jpeg', quality: 85, animations: 'disabled' });
  await expect(page.locator('#home-games')).toBeHidden();
  await expect(page.locator('#friends-open')).toBeHidden();
  await expect(page.locator('#notifications-open')).toBeHidden();
  await expect(page.getByRole('button', { name: 'Refresh', exact: true })).toHaveCount(0);
  await expect(page.locator('body')).not.toContainText('Saved on this browser');
  await page.getByRole('link', { name: 'Enter the arena' }).click();
  await expect(page.locator('#sign-in-panel')).toBeInViewport();
  await page.evaluate(() => scrollTo(0, 0));
  await openNavigation(page);
  await expect(page.locator('#join-open')).toBeHidden();
  await expect(page.getByRole('link', { name: 'Leaderboard', exact: true })).toHaveAttribute('href', '/leaderboard');
  await page.getByRole('button', { name: 'Your data', exact: true }).click();
  await expect(page.getByRole('dialog', { name: 'Your data', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'What we keep' })).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.locator('#data-dialog')).toBeHidden();
  await noOverflow(page);
  expect(state.privateReads).toEqual([]);
  expect(state.friendReads).toBe(0);
  expect(state.errors).toEqual([]);
});

test('a player opens friends and notices without crowding the dashboard or asking permission', async ({ page }, testInfo) => {
  const state = await home(page);
  await expect(page.locator('#my-tables .game-card')).toHaveCount(1);
  await expect(page.locator('#welcome-intro')).toBeHidden();
  await expect(page.locator('#sign-in-panel')).toBeHidden();
  await expect(page.locator('#friends-panel')).toBeHidden();
  await expect(page.locator('#privacy-panel')).toBeHidden();
  await expect(page.locator('#friend-requests-badge')).toHaveText('1');
  await page.screenshot({ path: testInfo.outputPath('home-member.jpg'), fullPage: true, type: 'jpeg', quality: 85, animations: 'disabled' });
  await openPanel(page, /^Friends/);
  await expect(page.getByRole('dialog', { name: 'Friends', exact: true })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('home-friends.jpg'), fullPage: false, type: 'jpeg', quality: 85, animations: 'disabled' });
  await page.getByRole('button', { name: 'Accept', exact: true }).click();
  await expect(page.locator('#friend-incoming')).toBeHidden();
  await expect(page.locator('#friend-list')).toContainText('Alex');
  await expect(page.locator('#friend-requests-badge')).toBeHidden();
  await page.getByRole('button', { name: 'Close friends' }).click();
  const narrow = await page.locator('#home-menu-toggle').isVisible();
  await expect(page.locator(narrow ? '#home-menu-toggle' : '#friends-open')).toBeFocused();
  await openPanel(page, 'Notifications');
  await expect(page.getByRole('dialog', { name: 'Notifications', exact: true })).toBeVisible();
  await expect(page.locator('#device-unavailable')).toBeVisible();
  expect(await page.evaluate(() => window.permissionRequests)).toBe(0);
  await page.keyboard.press('Escape');
  await expect(page.locator('#notifications-dialog')).toBeHidden();
  await noOverflow(page);
  expect(state.errors).toEqual([]);
});

test('inviting a friend leaves the friends window and selects that friend in the create dialog', async ({ page }) => {
  const state = await home(page);
  await expect(page.locator('#my-tables .game-card')).toHaveCount(1);
  await openPanel(page, /^Friends/);
  await page.locator('#friend-list').getByRole('button', { name: 'Invite', exact: true }).click();
  await expect(page.locator('#friends-dialog')).toBeHidden();
  await expect(page.locator('#create-dialog')).toBeVisible();
  await expect(page.locator('#opponent')).toHaveValue('friend');
  await expect(page.locator('#friend')).toHaveValue(friend.id);
  await expect(page.locator('dialog[open]')).toHaveCount(1);
  await expect(page.locator('#create-submit')).toBeEnabled();
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  expect(state.errors).toEqual([]);
});

test('joining validates the code inside its window and follows a valid invitation', async ({ page }) => {
  await home(page);
  await openPanel(page, 'Join with a code');
  await page.getByLabel('Invitation code').fill('ABC');
  await page.getByRole('button', { name: 'Join →', exact: true }).click();
  await expect(page.locator('#join-problem')).toHaveText('Enter the eight-character code from your invitation.');
  await expect(page.getByLabel('Invitation code')).toBeFocused();
  await page.route('**/j/K7F2Q9XM', route => route.fulfill({ contentType: 'text/html', body: '<h1>Invitation</h1>' }));
  await page.getByLabel('Invitation code').fill('k7f2-q9xm');
  await page.getByRole('button', { name: 'Join →', exact: true }).click();
  await expect(page).toHaveURL(/\/j\/K7F2Q9XM$/);
});

test('sign-out clears private content even when an earlier game request finishes later', async ({ page }) => {
  const state = await home(page, { delayMine: true });
  await expect.poll(() => Boolean(state.release)).toBe(true);
  await page.locator('#account-menu summary').click();
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
  await expect(page.locator('#action-dialog')).toContainText('Sign out on this device?');
  await page.locator('#action-confirm').click();
  await expect(page.locator('body')).toHaveAttribute('data-home', 'guest');
  expect(state.signouts).toEqual([{ everywhere: false }]);
  state.release();
  await expect(page.locator('#my-tables .game-card')).toHaveCount(0);
  await expect(page.locator('#friend-list')).toBeEmpty();
  await expect(page.locator('#home-games')).toBeHidden();
  await page.getByLabel('Developer sign-in: any player name').fill('MarcF');
  await page.getByRole('button', { name: 'Sign in →' }).click();
  await expect(page.locator('body')).toHaveAttribute('data-home', 'member');
  await expect(page.locator('#my-tables .game-card')).toHaveCount(1);
  await expect(page.locator('#friend-list')).toContainText('Bastien');
  expect(state.errors).toEqual([]);
});

test('local hosts still resume games and reconnect automatically without a refresh button', async ({ page }) => {
  const state = await home(page, { account: null, mode: 'off' });
  await expect(page.locator('#my-tables .game-card')).toHaveCount(1);
  await expect(page.locator('#welcome-intro')).toBeHidden();
  await expect(page.locator('#leaderboard-link')).toBeHidden();
  expect(state.privateReads).toContain('/api/lobby/resume');
  state.offline = true;
  await page.evaluate(() => dispatchEvent(new Event('online')));
  await expect(page.locator('#lobby-status')).toContainText('Reconnecting automatically');
  state.offline = false;
  await page.evaluate(() => dispatchEvent(new Event('online')));
  await expect(page.locator('#lobby-status')).toHaveText('1 table ongoing · updates automatically');
  await page.getByRole('button', { name: 'New battle' }).click();
  await expect(page.locator('#create-dialog')).toBeVisible();
  await expect(page.locator('#opponent option[value="friend"]')).toBeHidden();
  expect(state.errors).toEqual([]);
});

test('navigation and dialogs fit narrow, intermediate and short landscape screens', async ({ page }) => {
  await home(page, { account: { ...player, username: 'VeryLongPlayerName123' }, operator: true });
  for (const [width, height] of [[320, 740], [900, 700], [1101, 800], [1280, 800], [740, 320]]) {
    await page.setViewportSize({ width, height });
    await noOverflow(page);
    await openPanel(page, /^Friends/);
    const dialog = page.locator('#friends-dialog');
    await expect(dialog).toBeVisible();
    const bounds = await dialog.boundingBox();
    expect(bounds.x).toBeGreaterThanOrEqual(0);
    expect(bounds.y).toBeGreaterThanOrEqual(0);
    expect(bounds.x + bounds.width).toBeLessThanOrEqual(width);
    expect(bounds.y + bounds.height).toBeLessThanOrEqual(height);
    await expect(page.getByRole('button', { name: 'Close friends' })).toBeInViewport();
    await page.keyboard.press('Escape');
  }
  await page.setViewportSize({ width: 390, height: 844 });
  await openNavigation(page);
  await page.keyboard.press('Escape');
  await expect(page.locator('#home-menu-toggle')).toBeFocused();
  await expect(page.locator('#home-navigation')).toBeHidden();
  await openNavigation(page);
  await page.locator('.home-header').click({ position: { x: 1, y: 1 } });
  await expect(page.locator('#home-navigation')).toBeHidden();
});

test('removing a friend requires confirming the named player, and hides an empty count', async ({ page }) => {
  const state = await home(page);
  await openPanel(page, /^Friends/);
  const remove = page.getByRole('button', { name: 'Remove', exact: true });
  await remove.click();
  await expect(page.locator('#action-title')).toHaveText('Remove Bastien?');
  await page.keyboard.press('Escape');
  await expect(remove).toBeVisible();
  expect(state.removals).toBeUndefined();
  await remove.click();
  await page.locator('#action-confirm').click();
  await expect(page.locator('#friend-list')).toHaveText(/No friends yet/);
  await expect(page.locator('#friend-count')).toBeHidden();
  expect(state.removals).toBe(1);
});

test('account deletion can be cancelled and signing out everywhere requires its own confirmation', async ({ page }) => {
  const state = await home(page);
  await page.locator('#account-menu summary').click();
  await page.locator('#delete-account').click();
  await expect(page.getByLabel('Type your player name to confirm')).toBeFocused();
  await page.locator('#delete-cancel').click();
  await page.locator('#account-menu summary').click();
  await page.locator('#sign-out-everywhere').click();
  await expect(page.locator('#action-detail')).toContainText('every device');
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  expect(state.signouts).toBeUndefined();
  if (!await page.locator('#sign-out-everywhere').isVisible()) await page.locator('#account-menu summary').click();
  await page.locator('#sign-out-everywhere').click();
  await page.locator('#action-confirm').click();
  await expect(page.locator('body')).toHaveAttribute('data-home', 'guest');
  expect(state.signouts).toEqual([{ everywhere: true }]);
});

for (const file of ['leaderboard', 'arena', 'index']) {
  test(`the signed-in ${file} page opens the same menu and friends panel in place`, async ({ page }) => {
    await home(page);
    await page.goto(`/table/${file}.html`);
    await expect(page.locator('#account-name')).toHaveText(player.username);
    await openNavigation(page);
    const nav = page.getByRole('navigation', { name: 'Main navigation' });
    await expect(nav.locator(':scope > :visible')).toHaveText(['Play', /Friends/, 'Leaderboard', 'Notifications', 'Your data', 'Join with a code']);
    const sharedStyle = await nav.evaluate(node => {
      const props = ['fontFamily', 'fontSize', 'fontWeight', 'lineHeight', 'padding', 'minHeight'];
      const styleOf = selector => props.map(prop => getComputedStyle(node.querySelector(selector))[prop]);
      return { link: styleOf('#leaderboard-link'), button: styleOf('#notifications-open') };
    });
    expect(sharedStyle.link).toEqual(sharedStyle.button);
    if (file === 'leaderboard') await expect(nav.getByRole('link', { name: 'Leaderboard' })).toHaveAttribute('aria-current', 'page');
    await nav.getByRole('button', { name: /^Friends/ }).click();
    await expect(page.locator('#friends-dialog')).toBeVisible();
    await expect(page.locator('#friend-list')).toContainText(friend.username);
    await expect(page).toHaveURL(new RegExp(`/${file}\\.html$`));
    await page.keyboard.press('Escape');
    await noOverflow(page);
  });
}

test('an account host that cannot be reached says so instead of offering a sign-in with no way in', async ({ page }) => {
  const state = await home(page, { account: null, mode: 'google', meDown: true });
  await expect(page.locator('#sign-in-problem')).toHaveText('Could not reach the host to check your account. Reload the page to try again.');
  await expect(page.locator('#home-games')).toBeHidden();
  expect(state.errors).toEqual([]);
});
