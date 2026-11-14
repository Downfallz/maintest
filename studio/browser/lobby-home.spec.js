import { test, expect } from '@playwright/test';

const player = { id: 'acct_aaaaaaaaaaaaaaaa', username: 'MarcF' };
const friend = { id: 'acct_bbbbbbbbbbbbbbbb', username: 'Bastien' };
const challenger = { id: 'acct_cccccccccccccccc', username: 'Alex' };
const battle = {
  id: 'battle-one', format: '3v3', over: false, waiting: false, openSeats: 0, round: 3,
  seats: [{ seat: 'player1', kind: 'person', name: 'MarcF', turn: true }, { seat: 'player2', kind: 'person', name: 'Bastien' }],
};
const held = { table: battle, seats: [{ seat: 'player1', token: 'seat-token' }] };

async function home(page, { account = player, mode = 'dev', delayMine = false, operator = false, meDown = false, notice = null, registrationClosed = false, games = null, finished = { players: [], bots: [] }, opponents = [], invitations = [], incoming = [challenger], publicTables = [battle] } = {}) {
  const state = { account, mode, meDown, offline: false, mineReads: 0, friendReads: 0, finishedReads: [], privateReads: [], release: null, invitations, publicTables,
    friends: { friends: [friend], incoming, outgoing: [] } };
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  state.errors = errors;
  await page.addInitScript(() => {
    window.permissionRequests = 0;
    if (window.Notification) Notification.requestPermission = async () => { window.permissionRequests++; return 'denied'; };
    window.google = { accounts: { id: { initialize(options) { window.googleSignIn = options.callback; }, renderButton(el) {
      const button = document.createElement('button'); button.textContent = 'Continue with Google'; el.append(button);
    } } } };
  });
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    const json = value => route.fulfill({ json: value });
    if (path === '/api/me' && state.meDown) return route.fulfill({ status: 503, json: { message: 'Restarting' } });
    if (path === '/api/me') return json({ accounts: state.mode, account: state.account, googleClientId: 'test-client', operator, notice });
    // A first sign-in while the operator has registration closed (ADR 0177) is refused rather than asked for a name.
    if (path === '/api/account/google' && registrationClosed) return route.fulfill({ status: 409, json: { error: 'Account.RegistrationClosed', message: 'New accounts are closed for now. Try again later.' } });
    // A table asked for is noted and refused: the dialog's request is what the tests read.
    if (path === '/api/lobby/tables' && route.request().method() === 'POST') { state.created = route.request().postDataJSON(); return route.fulfill({ status: 409, json: { message: 'All tables are occupied.' } }); }
    if (path === '/api/account/signout') { state.signouts ??= []; state.signouts.push(route.request().postDataJSON()); state.account = null; return json({}); }
    if (path === '/api/account/dev') { state.account = player; return json({}); }
    if (path === '/api/lobby') {
      if (state.offline) return route.fulfill({ status: 503, json: { message: 'Offline' } });
      return json({ recording: true, format: '3v3', formats: ['3v3', '2v2', '1v1'], ongoing: state.publicTables.length, open: state.publicTables.filter(one => one.openSeats > 0).length });
    }
    // Find an opponent, a page of eight at a time, as the host serves it.
    const listed = /^\/api\/lobby\/page\/(open|all)\/(\d+)$/.exec(path);
    if (listed) {
      if (state.offline) return route.fulfill({ status: 503, json: { message: 'Offline' } });
      state.tablePages = [...(state.tablePages ?? []), `${listed[1]}/${listed[2]}`];
      const all = listed[1] === 'open' ? state.publicTables.filter(one => one.openSeats > 0) : state.publicTables;
      const pages = Math.max(1, Math.ceil(all.length / 8)), at = Math.min(Number(listed[2]), pages);
      return json({ total: all.length, pages, page: at, items: all.slice((at - 1) * 8, at * 8) });
    }
    if (['/api/lobby/adopt', '/api/lobby/mine', '/api/lobby/resume'].includes(path)) {
      state.privateReads.push(path); state.mineReads++;
      const tables = state.account || state.mode === 'off' ? games ?? [held] : [];
      if (delayMine && state.mineReads === 1) await new Promise(resolve => { state.release = resolve; });
      return json({ tables, invitations: state.account ? state.invitations : [], ongoingPerKind: state.account ? 5 : undefined });
    }
    const pageOf = /^\/api\/lobby\/finished\/(bots|players)\/(\d+)$/.exec(path);
    if (pageOf) {
      state.finishedReads.push(`${pageOf[1]}/${pageOf[2]}`);
      if (state.failFinished > 0) { state.failFinished -= 1; return route.fulfill({ status: 503, json: { message: 'The results are out for a moment.' } }); }
      const all = finished[pageOf[1]], pages = Math.max(1, Math.ceil(all.length / 6)), at = Math.min(Number(pageOf[2]), pages);
      return json({ total: all.length, pages, page: at, items: all.slice((at - 1) * 6, at * 6) });
    }
    if (path === `/api/friends/${friend.id}` && route.request().method() === 'DELETE') {
      state.removals = (state.removals ?? 0) + 1;
      state.friends = { ...state.friends, friends: [] }; return json({});
    }
    // Each opponent's relation is the friends list's, as the host reads it.
    if (path === '/api/lobby/opponents') {
      state.opponentReads = (state.opponentReads ?? 0) + 1;
      const relation = id => [['friends', 'friend'], ['outgoing', 'asked'], ['incoming', 'asking']].find(([key]) => state.friends[key].some(link => link.id === id))?.[1] ?? 'none';
      return json({ opponents: opponents.map(one => ({ ...one, relation: relation(one.id) })) });
    }
    if (path === '/api/friends/requests' && route.request().method() === 'POST') {
      state.asked = route.request().postDataJSON();
      const other = opponents.find(one => one.name === state.asked.username);
      state.friends = { ...state.friends, outgoing: [...state.friends.outgoing, { id: other.id, username: other.name }] };
      return json({ accepted: false });
    }
    if (path === '/api/friends') { state.friendReads++; return json(state.friends); }
    const answer = /^\/api\/lobby\/tables\/([^/]+)\/(accept|decline)$/.exec(path);
    if (answer) {
      state.answered = [...(state.answered ?? []), `${answer[2]}:${answer[1]}`];
      state.invitations = state.invitations.filter(one => one.table.id !== answer[1]);
      return json(answer[2] === 'accept' ? { seats: [{ seat: 'player2', token: 'invited-token' }] } : {});
    }
    if (path === `/api/friends/requests/${challenger.id}` && route.request().method() === 'DELETE') {
      state.friends = { ...state.friends, incoming: [] }; return json({});
    }
    if (path === `/api/friends/requests/${challenger.id}/accept`) {
      state.friends = { ...state.friends, friends: [friend, challenger], incoming: [] }; return json({});
    }
    if (path === '/api/lobby/push') return json({ publicKey: null });
    if (path === '/api/account/preferences') {
      if (!state.account) return route.fulfill({ status: 401, json: { message: 'Sign in first.' } });
      if (state.holdPreferences) await state.holdPreferences;
      if (route.request().method() === 'PUT') { state.saved = route.request().postDataJSON(); return json({ preferences: state.saved, saved: true }); }
      return json({ preferences: state.saved ?? { pace: 'step', speed: 1, help: true, reducedMotion: false }, saved: Boolean(state.saved) });
    }
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
// Settings is one window of tabs, opened from the menu or from the player's name (the account chip).
async function openAccountSettings(page, tab = 'Game') {
  await page.locator('#account-menu summary').click();
  await page.locator('#account-menu').getByRole('button', { name: 'Settings' }).click();
  const dialog = page.getByRole('dialog', { name: 'Settings' });
  await dialog.getByRole('tab', { name: tab }).click();
  return dialog;
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
  await expect(page.getByRole('button', { name: 'Refresh', exact: true })).toHaveCount(0);
  await expect(page.locator('body')).not.toContainText('Saved on this browser');
  await page.getByRole('link', { name: 'Play Downfall Instinct' }).click();
  await expect(page.locator('#sign-in-panel')).toBeInViewport();
  await page.evaluate(() => scrollTo(0, 0));
  await openNavigation(page);
  await expect(page.locator('#join-open')).toBeHidden();
  await expect(page.getByRole('link', { name: 'Leaderboard', exact: true })).toHaveAttribute('href', '/leaderboard');
  // A visitor's Settings holds the game and privacy tabs; notifications and the account are for a player.
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: 'Settings', exact: true }).click();
  const settings = page.getByRole('dialog', { name: 'Settings', exact: true });
  await expect(settings.getByRole('tab')).toHaveText(['Game', 'Privacy']);
  await settings.getByRole('tab', { name: 'Privacy' }).click();
  await expect(page.getByRole('heading', { name: 'What we keep' })).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.locator('#app-settings-dialog')).toBeHidden();
  await noOverflow(page);
  expect(state.privateReads).toEqual([]);
  expect(state.friendReads).toBe(0);
  expect(state.errors).toEqual([]);
});

test('a signed-in player keeps game settings with the account, starting from step, 1x and tips on', async ({ page }) => {
  const state = await home(page);
  await openNavigation(page);
  // Signed in, Settings is in the account menu only: the main menu does not repeat it.
  await expect(page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: 'Settings', exact: true })).toBeHidden();
  await page.keyboard.press('Escape');

  const dialog = await openAccountSettings(page);
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('tab')).toHaveText(['Game', 'Notifications', 'Privacy', 'Account']);
  await expect(dialog.getByRole('tab', { name: 'Game' })).toHaveAttribute('aria-selected', 'true');
  await expect(dialog.getByLabel('Read confirmed results')).toHaveValue('step');
  await expect(dialog.getByLabel('Playback speed')).toHaveValue('1');
  await expect(dialog.getByLabel('Phase tips')).toBeChecked();
  await expect(dialog.getByLabel('Less motion')).not.toBeChecked();
  await expect(dialog.getByLabel('Opponent reactions')).toBeChecked();
  await expect(page.locator('#game-settings-where')).toContainText('Kept with your account');
  await dialog.getByLabel('Read confirmed results').selectOption('auto');
  await expect.poll(() => state.saved).toEqual({ pace: 'auto', speed: 1, help: true, reducedMotion: false, reactions: true });
  await dialog.getByLabel('Playback speed').selectOption('1.5');
  await expect.poll(() => state.saved?.speed).toBe(1.5);
  await noOverflow(page);
  await page.keyboard.press('Escape');
  await expect(page.locator('#account-menu summary')).toBeFocused();
  expect(state.errors).toEqual([]);
});

test('game settings cannot be changed until the account has said what they are', async ({ page }) => {
  const state = await home(page);
  let release; state.holdPreferences = new Promise(resolve => { release = resolve; });
  state.saved = { pace: 'auto', speed: 2, help: true, reducedMotion: false };

  await openAccountSettings(page);

  await expect(page.getByLabel('Playback speed')).toBeDisabled();
  release();
  await expect(page.getByLabel('Playback speed')).toBeEnabled();
  await expect(page.getByLabel('Playback speed')).toHaveValue('2');
  await page.getByLabel('Playback speed').selectOption('0.5');
  await expect.poll(() => state.saved.speed).toBe(0.5);
  expect(state.errors).toEqual([]);
});

test('a guest keeps game settings in this browser and asks the host nothing', async ({ page }) => {
  const state = await home(page, { account: null, mode: 'google' });

  await openPanel(page, 'Settings');
  await page.getByLabel('Phase tips').uncheck();

  await expect(page.locator('#game-settings-where')).toContainText('Kept in this browser');
  await expect.poll(() => page.evaluate(() => JSON.parse(localStorage.getItem('downfall.arena.game-settings.v1'))?.help)).toBe(false);
  expect(state.saved).toBeUndefined();
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
  // Notifications are a tab of Settings; opening it asks the browser nothing.
  await openAccountSettings(page, 'Notifications');
  await expect(page.locator('#device-unavailable')).toBeVisible();
  expect(await page.evaluate(() => window.permissionRequests)).toBe(0);
  await page.screenshot({ path: testInfo.outputPath('home-settings-notifications.jpg'), fullPage: false, type: 'jpeg', quality: 85, animations: 'disabled' });
  await page.keyboard.press('Escape');
  await expect(page.locator('#app-settings-dialog')).toBeHidden();
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
  for (const [width, height] of [[320, 740], [900, 700], [1101, 800], [1240, 800], [1241, 800], [1280, 800], [740, 320]]) {
    await page.setViewportSize({ width, height });
    await noOverflow(page);
    await expect(page.locator('#account-menu summary')).toBeInViewport({ ratio: 1 });
    if (width <= 1240) await expect(page.locator('#home-menu-toggle')).toBeVisible();
    else await expect(page.locator('#home-menu-toggle')).toBeHidden();
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

test('the account menu holds Settings and Sign out; the account\'s lasting actions wait at the foot of Settings, Account', async ({ page }, testInfo) => {
  const state = await home(page);
  await page.locator('#account-menu summary').click();
  await expect(page.locator('#account-menu .account-menu button')).toHaveText(['Settings', 'Sign out']);
  await expect(page.locator('#account-menu-name')).toHaveText(player.username);
  await page.keyboard.press('Escape');
  await openAccountSettings(page, 'Account');
  await expect(page.locator('#app-settings-name')).toHaveText(player.username);
  await page.screenshot({ path: testInfo.outputPath('home-settings-account.jpg'), fullPage: false, type: 'jpeg', quality: 85, animations: 'disabled' });
  await page.locator('#delete-account').click();
  await expect(page.getByLabel('Type your player name to confirm')).toBeFocused();
  await page.locator('#delete-cancel').click();
  await page.locator('#sign-out-everywhere').click();
  await expect(page.locator('#action-detail')).toContainText('every device');
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  expect(state.signouts).toBeUndefined();
  await page.locator('#sign-out-everywhere').click();
  await page.locator('#action-confirm').click();
  await expect(page.locator('body')).toHaveAttribute('data-home', 'guest');
  expect(state.signouts).toEqual([{ everywhere: true }]);
});

for (const file of ['leaderboard', 'battle', 'index']) {
  test(`the signed-in ${file} page opens the same menu and friends panel in place`, async ({ page }) => {
    await home(page);
    await page.goto(`/table/${file}.html`);
    await expect(page.locator('#account-name')).toHaveText(player.username);
    // The bell reads what waits on its own on every page: the turn at Bastien's table and Alex's request.
    await expect(page.locator('#inbox-badge')).toHaveText('2');
    // Whole on screen, even where the header sits at the screen's top edge (the battle view on a phone).
    await expect(page.locator('#inbox-badge')).toBeInViewport({ ratio: 1 });
    expect(await page.locator('#inbox-badge').evaluate(badge => badge.getBoundingClientRect().top >= 0)).toBe(true);
    await openNavigation(page);
    const nav = page.getByRole('navigation', { name: 'Main navigation' });
    await expect(nav.locator(':scope > :visible')).toHaveText(['Lobby', /Friends/, 'Leaderboard', 'Your stats', 'Join with a code']);
    const sharedStyle = await nav.evaluate(node => {
      const props = ['fontFamily', 'fontSize', 'fontWeight', 'lineHeight', 'padding', 'minHeight'];
      const styleOf = selector => props.map(prop => getComputedStyle(node.querySelector(selector))[prop]);
      return { link: styleOf('#leaderboard-link'), button: styleOf('#app-settings-open') };
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

test('the signed-in player reads the statistics of their ranked games on a page of their own', async ({ page }) => {
  await home(page);
  await page.route('**/api/stats', route => route.fulfill({ json: {
    ranked: { games: 3, won: 2, lost: 1, drawn: 0 }, streak: { outcome: 'won', length: 2 }, bestWinStreak: 2,
    averageRounds: 7.5, withTeams: 3, mutations: [], spells: [{ id: 'spell:strike:v1', name: 'Strike', casts: 21, games: 3, won: 2 }],
  } }));
  await page.goto('/table/stats.html');
  await expect(page.locator('#record-cards .stats-card')).toHaveCount(1);
  await expect(page.locator('#record-cards .stats-card')).toContainText('Ranked 3v3');
  await expect(page.locator('#record-cards .stats-card')).toContainText('2–1–0');
  await expect(page.locator('#stats-status')).toHaveText('3 ranked games · updates as your games end');
  await expect(page.locator('#glance')).toContainText('2 wins in a row');
  await expect(page.locator('#spell-rows')).toContainText('Strike');
  await expect(page.locator('#mutation-rows')).toContainText('No mutation bought yet.');
  // Ranked games are all 3v3 (ADR 0171): no table of creatures and no table of formats.
  await expect(page.locator('#creature-rows, #formats')).toHaveCount(0);
  await openNavigation(page);
  await expect(page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'Your stats' })).toHaveAttribute('aria-current', 'page');
  await page.keyboard.press('Escape');
  await noOverflow(page);
});

test('an account host that cannot be reached says so instead of offering a sign-in with no way in', async ({ page }) => {
  const state = await home(page, { account: null, mode: 'google', meDown: true });
  await expect(page.locator('#sign-in-problem')).toHaveText('Could not reach the host to check your account. Reload the page to try again.');
  await expect(page.locator('#home-games')).toBeHidden();
  expect(state.errors).toEqual([]);
});

test('every game against a player waiting for the player is gathered under Your turn, the most pressing first, and Play next opens it', async ({ page }, info) => {
  const bot = { seat: 'player2', kind: 'bot', name: 'Lookahead', turn: false };
  const game = (id, { turn = true, against = 'bot', answerBy = null, casual = false } = {}) => ({ table: { ...battle, id, over: false, answerBy, casual, createdAt: '2026-11-10T18:00:00Z',
    seats: [{ ...battle.seats[0], turn, account: 'acct_marc' }, against === 'bot' ? bot : { ...battle.seats[1], turn: !turn, account: 'acct_bastien' }] },
    seats: [{ seat: 'player1', token: `${id}-token` }] });
  const soon = new Date(Date.now() + 2 * 3600000).toISOString(), later = new Date(Date.now() + 20 * 3600000).toISOString();
  const games = [game('bot-move'), game('their-move', { turn: false, against: 'person' }), game('friend-later', { against: 'person', answerBy: later }),
    game('friend-soon', { against: 'person', answerBy: soon, casual: true })];
  const state = await home(page, { games });
  const band = page.locator('.your-turn-band');

  // A game against a bot is always waiting for its player: it stays in its group, not here.
  await expect(band.getByRole('heading', { name: 'Your turn · 2' })).toBeVisible();
  await expect(band.locator('.game-row')).toHaveCount(2);
  await expect(band.locator('.game-row').first()).toContainText('Unranked');
  await expect(band.locator('.game-row').first()).toContainText('left');
  await expect(band.locator('.game-row').last()).toContainText('Ranked');
  await expect(band).not.toContainText('Lookahead');
  // Every card of one's own says when its game was created and whether it counts on the ladder.
  const bots = page.locator('.mine-group.bots .game-card');
  await expect(bots.locator('.game-meta')).toContainText('Created');
  await expect(bots.locator('.game-meta')).toContainText('2026');
  await expect(bots.locator('.game-meta .game-ranked')).toHaveText('Unranked');
  await expect(page.locator('.mine-group.players .game-meta .game-ranked.ranked')).toHaveCount(2);
  // Still in their own groups too: the band is the way through them, not their only place.
  await expect(page.locator('.mine-group .game-card')).toHaveCount(4);
  await expect(page.locator('#my-games-section')).toBeInViewport({ ratio: 0.1 });
  await page.locator('#my-games-section').screenshot({ path: info.outputPath('lobby-your-turn.png') });

  // The battle view itself is not served here: where the tap goes is what is checked.
  await page.route('**/battle?**', route => route.fulfill({ contentType: 'text/html', body: '<title>Battle</title>' }));
  await page.locator('#play-next').click();
  await page.waitForURL(/\/battle\?player1=friend-soon-token$/);
  expect(state.errors).toEqual([]);
});

test('nothing waiting for the player shows no Your turn at all', async ({ page }) => {
  const games = [{ table: { ...battle, id: 'theirs', seats: [{ ...battle.seats[0], turn: false }, { ...battle.seats[1], turn: true }] }, seats: [{ seat: 'player1', token: 'theirs-token' }] }];
  const state = await home(page, { games });

  await expect(page.locator('.mine-group .game-card')).toHaveCount(1);
  await expect(page.locator('#your-turn')).toBeHidden();
  expect(state.errors).toEqual([]);
});

test('your games are grouped against players and against bots, at most five of each in progress, finished ones read a page at a time', async ({ page }, info) => {
  const bot = { seat: 'player2', kind: 'bot', name: 'Lookahead', turn: false };
  const game = (id, { turn = false, against = 'bot' } = {}) => ({ table: { ...battle, id, over: false,
    seats: [{ ...battle.seats[0], turn }, against === 'bot' ? bot : { ...battle.seats[1], turn: false }] }, seats: [{ seat: 'player1', token: `${id}-token` }] });
  const games = [game('waiting-on-them', { against: 'person' }), ...Array.from({ length: 4 }, (_, index) => game(`bot-${index}`)), game('your-move', { turn: true })];
  const result = (index, outcome, opponent) => ({ match: `m${index}`, table: `t${index}`, finishedAt: `2026-11-0${1 + (index % 7)}T20:00:00Z`, format: '3v3', rounds: 9,
    outcome, you: ['player1'], opponent, seats: null, replay: `/battle?replay=t${index}&seat=player1`,
    createdAt: '2026-10-30T18:00:00Z', ranked: opponent.kind === 'person' });
  const finished = { bots: Array.from({ length: 8 }, (_, index) => result(index, 'won', { kind: 'bot', name: 'Lookahead' })),
    players: [result(20, 'lost', { kind: 'person', name: 'bastien' })] };
  const state = await home(page, { games, finished });
  const players = page.locator('.mine-group.players'), bots = page.locator('.mine-group.bots');

  await expect(page.locator('#mine-ongoing')).toHaveAttribute('aria-pressed', 'true');
  await expect(players.locator('.game-card')).toHaveCount(1); await expect(bots.locator('.game-card')).toHaveCount(5);
  await expect(bots.locator('.game-card').first()).toContainText('Your turn');
  await expect(players.locator('.mine-group-head .lobby-count')).toHaveText('1 / 5');
  await expect(bots.locator('.mine-group-head .lobby-count')).toHaveText('5 / 5'); await expect(bots.locator('.mine-limit')).toBeVisible();
  await expect(page.locator('#ongoing-count')).toHaveText('· 6'); await expect(page.locator('#finished-count')).toHaveText('· 9');
  expect(state.finishedReads.sort()).toEqual(['bots/1', 'players/1']);
  await page.locator('#my-games-section').screenshot({ path: info.outputPath('lobby-ongoing.png') });

  await page.locator('#mine-finished').click();
  // Finished games are rows: one line each, with the result, who, when and the way back to the game.
  await expect(bots.locator('.game-row')).toHaveCount(6); await expect(bots.locator('.game-row').first()).toContainText('Victory');
  await expect(bots.locator('.game-row').first()).toContainText('vs Lookahead');
  await expect(bots.locator('.game-row a').first()).toHaveAttribute('href', '/battle?replay=t0&seat=player1');
  await expect(players.locator('.game-row')).toContainText('Defeat'); await expect(players.locator('.lobby-pages')).toHaveCount(0);
  // Each says when it was created and when it finished, and whether it counted on the ladder, as the host kept it.
  await expect(players.locator('.game-row-detail')).toContainText('Created'); await expect(players.locator('.game-row-detail')).toContainText('Finished');
  await expect(players.locator('.casual-tag.ranked')).toHaveText('Ranked');
  await expect(bots.locator('.game-row').first().locator('.casual-tag')).toHaveText('Unranked');
  await expect(bots.locator('.lobby-pages span')).toHaveText('Page 1 of 2');
  await page.locator('#my-games-section').screenshot({ path: info.outputPath('lobby-finished.png') });
  await bots.getByRole('button', { name: 'Next →' }).click();
  await expect(bots.locator('.game-row')).toHaveCount(2); await expect(bots.locator('.lobby-pages span')).toHaveText('Page 2 of 2');
  await expect(bots.getByRole('button', { name: 'Next →' })).toBeDisabled();
  expect(state.finishedReads.filter(read => read.startsWith('players'))).toEqual(['players/1']);
  await noOverflow(page);

  // At five games against bots, a sixth is turned down before it is asked for.
  await page.locator('#new-table').click(); await page.getByRole('tab', { name: /^Solo/ }).click();
  await expect(page.locator('#create-help')).toContainText('the most at once'); await expect(page.locator('#create-submit')).toBeDisabled();
  await page.getByRole('tab', { name: /^PvP/ }).click(); await expect(page.locator('#create-submit')).toBeEnabled();
  // A game against another player can be opened unranked (ADR 0155); a bot's never counts, so it has no switch.
  await expect(page.getByLabel(/^Ranked/)).toBeChecked();
  await page.getByRole('tab', { name: /^Solo/ }).click(); await expect(page.locator('#ranked-row')).toBeHidden();
  await expect(page.getByLabel('Bot difficulty')).toHaveValue('hard');
  // Nor a time to answer (ADR 0169): a bot always answers. Between players it is three days unless another is chosen.
  await expect(page.locator('#turn-limit-row')).toBeHidden();
  await page.getByRole('tab', { name: /^PvP/ }).click(); await page.locator('#opponent').selectOption('person');
  await expect(page.getByLabel(/^Time to answer/)).toHaveValue('72'); await page.getByLabel(/^Time to answer/).selectOption('168');
  await page.getByLabel(/^Ranked/).uncheck();
  await page.locator('#create-submit').click();
  await expect.poll(() => state.created).toEqual({ opponent: 'person', format: '3v3', ranked: false, turnLimit: 168 });
  expect(state.errors).toEqual([]);
});

test('a finished page that failed is read again on the next poll, and the three polls after a game ends read again', async ({ page }) => {
  const bot = { seat: 'player2', kind: 'bot', name: 'Lookahead', turn: false };
  const games = [{ table: { ...battle, id: 'live', seats: [battle.seats[0], bot] }, seats: [{ seat: 'player1', token: 'live-token' }] }];
  const won = { match: 'm1', table: 'done', finishedAt: '2026-11-07T20:00:00Z', format: '3v3', rounds: 4, outcome: 'won', you: ['player1'],
    opponent: { kind: 'bot', name: 'Lookahead' }, seats: null, replay: '/battle?replay=done&seat=player1' };
  const finished = { bots: [], players: [] };
  const state = await home(page, { games, finished });
  state.failFinished = 0;
  const poll = async () => { const before = state.mineReads; await page.evaluate(() => window.dispatchEvent(new Event('online'))); await expect.poll(() => state.mineReads).toBeGreaterThan(before); };
  await expect.poll(() => state.finishedReads.length).toBe(2);

  // The game ends, and its result is not written yet when the lobby first hears of it, nor can the host read it once.
  games[0].table = { ...games[0].table, over: true }; state.failFinished = 1;
  await poll(); await expect.poll(() => state.finishedReads.length).toBe(4);
  finished.bots.push(won);
  await poll(); await expect.poll(() => state.finishedReads.length).toBe(6);
  await page.locator('#mine-finished').click();
  await expect(page.locator('.mine-group.bots .game-row')).toHaveCount(1);
  await poll(); await expect.poll(() => state.finishedReads.length).toBe(8);
  await poll(); await poll();
  expect(state.finishedReads.length).toBe(8);
  expect(state.errors).toEqual([]);
});


test('recent opponents are listed with the record between you: a friend is challenged again in the same format, anyone else is asked to be a friend first', async ({ page }, info) => {
  const last = '2026-11-08T20:00:00Z';
  // Bastien is a friend and Alex asked to be one (the mock's friends list); Julie is neither. The host lists them
  // met most recently first: Julie, Bastien, Alex.
  const state = await home(page, { opponents: [
    { id: 'acct_dddddddddddddddd', name: 'Julie', lastPlayedAt: '2026-11-09T20:00:00Z', games: 1, won: 0, lost: 1, drawn: 0, format: '3v3', casual: false },
    { id: friend.id, name: friend.username, lastPlayedAt: last, games: 3, won: 2, lost: 1, drawn: 0, format: '1v1', casual: true },
    { id: challenger.id, name: challenger.username, lastPlayedAt: '2026-11-07T20:00:00Z', games: 1, won: 1, lost: 0, drawn: 0, format: '3v3', casual: false },
  ] });
  const section = page.locator('#opponents'), cards = section.locator('.game-card.opponent');
  const names = () => cards.locator('h3').allTextContents();
  // A friend, the one a tap challenges, comes first; the others keep the order they were met in.
  await expect.poll(names).toEqual(['Bastien', 'Julie', 'Alex']);
  await expect(section).toBeVisible();
  await expect(cards).toHaveCount(3);
  await expect(cards.nth(0).locator('.record')).toHaveText('2–1');
  await expect(cards.nth(0).locator('.record')).toHaveAccessibleName('Won 2 · Lost 1');
  await expect(cards.nth(0)).toContainText('1v1 · unranked');
  // One row that scrolls sideways: every card on the same line, the section one card high, the page never wider.
  const tops = await cards.evaluateAll(nodes => nodes.map(node => Math.round(node.getBoundingClientRect().top)));
  expect(new Set(tops).size).toBe(1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  if (page.viewportSize().width < 600) {
    const strip = page.locator('#opponent-cards');
    expect(await strip.evaluate(node => node.scrollWidth > node.clientWidth)).toBe(true);
    await expect(cards.nth(1)).not.toBeInViewport({ ratio: 1 });
  }
  await expect(cards.nth(2).getByRole('button', { name: 'Accept friend' })).toBeEnabled();
  await page.screenshot({ path: info.outputPath('recent-opponents.png'), fullPage: true });
  // A stranger is asked to be a friend, by name: no invitation reaches them.
  await cards.nth(1).getByRole('button', { name: 'Add friend' }).click();
  await expect(cards.nth(1).getByRole('button', { name: 'Friend request sent' })).toBeDisabled();
  expect(state.asked).toEqual({ username: 'Julie' });
  // Accepted from the Friends panel instead, Alex's card follows: Alex can be challenged now.
  await openPanel(page, /^Friends/);
  await page.locator('#friends-dialog').getByRole('button', { name: 'Accept', exact: true }).click();
  await page.getByRole('button', { name: 'Close friends' }).click();
  // A friend now, Alex moves forward with the friends: after Bastien, met more recently.
  await expect.poll(names).toEqual(['Bastien', 'Alex', 'Julie']);
  await expect(cards.nth(1).getByRole('button', { name: 'Challenge →' })).toBeEnabled();
  await expect(cards.nth(2).getByRole('button', { name: 'Friend request sent' })).toBeDisabled();
  // A friend is challenged from the create dialog, on them, in the last game's format and rating.
  await cards.nth(0).getByRole('button', { name: 'Challenge →' }).click();
  await expect(page.locator('#create-dialog')).toBeVisible();
  await expect(page.locator('#opponent')).toHaveValue('friend');
  await expect(page.locator('#friend')).toHaveValue(friend.id);
  await expect(page.locator('#format')).toHaveValue('1v1');
  await expect(page.locator('#ranked')).not.toBeChecked();
  expect(state.created).toBeUndefined();
  expect(state.errors).toEqual([]);
});

test('a player who has met nobody yet sees no recent opponents section', async ({ page }) => {
  const state = await home(page);
  await expect(page.locator('#my-tables .game-card')).toHaveCount(1);
  await expect.poll(() => state.opponentReads ?? 0).toBeGreaterThan(0);
  await expect(page.locator('#opponents')).toBeHidden();
});

test('the bell counts what waits for the player and acts on each: play a turn, answer an invitation, answer a friend request', async ({ page }, info) => {
  const state = await home(page, { invitations: [{ table: { id: 'invited', format: '1v1' }, from: { id: friend.id, username: friend.username }, sentAt: '2026-11-09T20:00:00Z' }] });
  // The game page itself is not this test's: a stand-in answers where the bell sends the player.
  await page.route('**/battle?**', route => route.fulfill({ contentType: 'text/html', body: '<!doctype html><title>Game</title>' }));
  const bell = page.locator('#inbox-open'), badge = page.locator('#inbox-badge');
  await expect(bell).toBeInViewport();
  await expect(badge).toHaveText('3');
  await expect(bell).toHaveAccessibleName('Notifications · 3 waiting');
  await bell.click();
  const dialog = page.locator('#inbox-dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.locator('.inbox-group h3')).toHaveText(['Your turn · 1', 'Invitations · 1', 'Friend requests · 1']);
  await expect(dialog.locator('.inbox-item.turn')).toContainText('Your turn against Bastien');
  await expect(dialog.locator('.inbox-item.turn')).toContainText('3v3 battle · Round 3');
  await page.screenshot({ path: info.outputPath('inbox.png') });
  // A friend request accepted here is gone from the bell and from the Friends panel's count.
  await dialog.locator('.inbox-item.friend').getByRole('button', { name: 'Accept', exact: true }).click();
  await expect(dialog.locator('.inbox-item.friend')).toHaveCount(0);
  await expect(page.locator('#friend-requests-badge')).toBeHidden();
  await expect(badge).toHaveText('2');
  // An invitation declined here is answered with the lobby's own route.
  await dialog.locator('.inbox-item.invitation').getByRole('button', { name: 'Decline' }).click();
  await expect(dialog.locator('.inbox-item.invitation')).toHaveCount(0);
  expect(state.answered).toEqual(['decline:invited']);
  await expect(badge).toHaveText('1');
  // The turn opens the game, in the battle view (ADR 0157), with the seat the player holds there.
  await dialog.locator('.inbox-item.turn').getByRole('button', { name: 'Play →' }).click();
  await page.waitForURL(/\/battle\?player1=seat-token$/);
  expect(state.errors).toEqual([]);
});

test('accepting an invitation from the bell takes the seat and opens the game', async ({ page }) => {
  const state = await home(page, { incoming: [], games: [], invitations: [{ table: { id: 'invited', format: '1v1' }, from: { id: friend.id, username: friend.username } }] });
  // The game page itself is not this test's: a stand-in answers where the bell sends the player.
  await page.route('**/battle?**', route => route.fulfill({ contentType: 'text/html', body: '<!doctype html><title>Game</title>' }));
  await expect(page.locator('#inbox-badge')).toHaveText('1');
  await page.locator('#inbox-open').click();
  await page.locator('#inbox-dialog .inbox-item.invitation').getByRole('button', { name: 'Accept & play →' }).click();
  await page.waitForURL(/\/battle\?player2=invited-token$/);
  expect(state.answered).toEqual(['accept:invited']);
});

test('with nothing waiting the bell shows no count and says so; a guest has no bell', async ({ page }) => {
  await home(page, { incoming: [], games: [] });
  await expect(page.locator('#inbox-open')).toHaveAccessibleName('Notifications · nothing waiting');
  await expect(page.locator('#inbox-badge')).toBeHidden();
  await page.locator('#inbox-open').click();
  await expect(page.locator('#inbox-dialog .inbox-empty')).toBeVisible();
  await page.keyboard.press('Escape');
  await page.locator('#account-menu summary').click();
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
  await page.locator('#action-confirm').click();
  await expect(page.locator('body')).toHaveAttribute('data-home', 'guest');
  await expect(page.locator('#inbox-open')).toBeHidden();
});

test('the guest page shows three Stage 3 creatures in depth, none hiding another, and how a creature evolves', async ({ page }) => {
  await home(page, { account: null, mode: 'google' });
  const beasts = page.locator('.intro-art .intro-beast');
  await expect(beasts).toHaveCount(3);
  for (const img of await page.locator('.intro-art img').all()) await expect(img).toHaveAttribute('src', /evolutions\/.*-stage3-/);
  const boxes = await beasts.evaluateAll(nodes => nodes.map(node => node.getBoundingClientRect().toJSON()));
  // Back, middle, front: each lower and larger than the one behind it, and no two sharing much of their room.
  const [back, middle, front] = boxes;
  expect(back.bottom).toBeLessThan(middle.bottom); expect(middle.bottom).toBeLessThan(front.bottom);
  expect(back.width).toBeLessThan(middle.width); expect(middle.width).toBeLessThan(front.width);
  const shared = (a, b) => Math.max(0, Math.min(a.right, b.right) - Math.max(a.left, b.left)) * Math.max(0, Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top));
  for (const [a, b] of [[back, middle], [middle, front], [back, front]]) expect(shared(a, b) / Math.min(a.width * a.height, b.width * b.height)).toBeLessThan(0.3);
  await expect(page.locator('.evolution-strip li')).toHaveText([/Stage 1\s*Warped/, /Stage 2\s*Stormborn/, /Stage 3\s*Cataclysm/]);
  await expect(page.locator('.intro-features li')).toHaveCount(4);
  await noOverflow(page);
});

test('find an opponent pages the tables eight at a time, one row each, open seats first and every battle on request', async ({ page }, info) => {
  const open = Array.from({ length: 10 }, (_, index) => ({ id: `open-${index}`, format: '3v3', over: false, waiting: true, openSeats: 1, round: null,
    seats: [{ seat: 'player1', kind: 'person', name: `Rival${index}`, turn: false }, { seat: 'player2', kind: 'open', name: 'Open seat' }] }));
  const busy = { id: 'busy', format: '1v1', over: false, waiting: false, openSeats: 0, round: 5,
    seats: [{ seat: 'player1', kind: 'person', name: 'Julie', turn: true }, { seat: 'player2', kind: 'bot', name: 'Lookahead' }] };
  const state = await home(page, { publicTables: [...open, busy] });
  const list = page.locator('#open-tables'), rows = list.locator('.game-row');
  await expect(rows).toHaveCount(8);
  await expect(page.locator('#tables-count')).toHaveText('10');
  await expect(page.locator('#lobby-status')).toHaveText('11 tables ongoing · updates automatically');
  await expect(rows.first()).toContainText('Rival0');
  await expect(rows.first().getByRole('button', { name: 'Join 3v3 table open-0' })).toBeVisible();
  // One row each: the list grows by short lines, never by cards.
  for (const box of await rows.evaluateAll(nodes => nodes.map(node => node.getBoundingClientRect().height))) expect(box).toBeLessThan(page.viewportSize().width < 760 ? 120 : 80);
  await noOverflow(page);
  await page.locator('#my-games-section ~ section').last().screenshot({ path: info.outputPath('find-an-opponent.png') });
  await list.getByRole('button', { name: 'Next →' }).click();
  await expect(rows).toHaveCount(2);
  await expect(list.locator('.lobby-pages span')).toHaveText('Page 2 of 2');
  // Every battle underway, from its first page: the one nobody can join has no action.
  await page.locator('#filter-all').click();
  await expect(page.locator('#tables-count')).toHaveText('11');
  await expect(list.locator('.lobby-pages span')).toHaveText('Page 1 of 2');
  await list.getByRole('button', { name: 'Next →' }).click();
  const last = rows.filter({ hasText: 'Julie' });
  await expect(last).toContainText('Round 5'); await expect(last.getByRole('button')).toHaveCount(0);
  expect(state.tablePages).toEqual(expect.arrayContaining(['open/1', 'open/2', 'all/1', 'all/2']));
  expect(state.errors).toEqual([]);
});

test('a game between two players counts its time to answer down on the card, with nothing read again', async ({ page }) => {
  const now = new Date('2026-11-12T12:00:00Z');
  await page.clock.install({ time: now });
  const answerBy = new Date(now.getTime() + 62.5 * 60000).toISOString();
  const state = await home(page, { games: [{ ...held, table: { ...battle, answerBy } }] });
  const card = page.locator('.mine-group.players .game-card');
  await expect(card).toContainText('Your turn · 1 h left');
  const reads = state.mineReads;

  await page.clock.fastForward('03:00');

  await expect(card).toContainText(/Your turn · 5\d min left/);
  expect(state.mineReads).toBe(reads);
  expect(state.errors).toEqual([]);
});

test('the create dialog plays Solo against a bot in any format, and PvP ranked in 3v3 or unranked in any format', async ({ page }, info) => {
  const state = await home(page);
  await page.locator('#new-table').click();
  const solo = page.getByRole('tab', { name: /^Solo/ }), pvp = page.getByRole('tab', { name: /^PvP/ }), format = page.getByLabel(/^Creatures per side/);
  await expect(pvp).toHaveAttribute('aria-selected', 'true');
  // Ranked is 3v3 (ADR 0171): the format is fixed and says why.
  await expect(format).toHaveValue('3v3'); await expect(format).toBeDisabled(); await expect(page.locator('#format-note')).toBeVisible();
  const ranked = await page.getByLabel(/^Time to answer/).locator('option').allTextContents();
  await page.getByLabel(/^Ranked/).uncheck();
  await expect(format).toBeEnabled(); await expect(page.locator('#format-note')).toBeHidden();
  expect(await page.getByLabel(/^Time to answer/).locator('option').allTextContents()).toEqual(ranked);
  await format.selectOption('1v1');
  await page.getByLabel(/^Ranked/).check(); await expect(format).toHaveValue('3v3');
  await page.locator('#create-dialog').screenshot({ path: info.outputPath('create-pvp.png') });

  await solo.click();
  await expect(page.locator('#create-solo-panel')).toBeVisible(); await expect(page.locator('#create-pvp-panel')).toBeHidden();
  await expect(format).toBeEnabled(); await format.selectOption('2v2');
  // A bot is chosen by how strong it is, never by its name (ADR 0180): Hard unless the player picks another.
  const level = page.getByLabel('Bot difficulty');
  await expect(level).toHaveValue('hard');
  expect(await level.locator('option').allTextContents()).toEqual(['Easy · makes mistakes', 'Medium · plays it straight', 'Hard · the strongest']);
  await level.selectOption('easy');
  await page.locator('#create-dialog').screenshot({ path: info.outputPath('create-solo.png') });
  await page.locator('#create-submit').click();
  await expect.poll(() => state.created).toEqual({ opponent: 'bot', format: '2v2', level: 'easy' });
  // PvP comes first, Solo beside it; the arrow keys move between the two tabs.
  await expect(page.getByRole('tab')).toHaveText([/^PvP/, /^Solo/]);
  await solo.focus(); await page.keyboard.press('ArrowLeft'); await expect(pvp).toBeFocused(); await expect(pvp).toHaveAttribute('aria-selected', 'true');
  await noOverflow(page);
  expect(state.errors).toEqual([]);
});

test('a first sign-in while registration is closed is told so, and the operator\'s notice shows under the header', async ({ page }, testInfo) => {
  await home(page, { account: null, mode: 'google', registrationClosed: true, notice: 'New accounts open again on Monday.' });
  await expect(page.locator('#host-notice')).toHaveText('New accounts open again on Monday.');
  await expect(page.getByRole('button', { name: 'Continue with Google' })).toBeVisible();

  await page.evaluate(() => window.googleSignIn({ credential: 'a-token-google-signed' }));

  await expect(page.locator('#sign-in-problem')).toHaveText('New accounts are closed for now. Try again later.');
  await expect(page.locator('#name-dialog')).not.toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('home-registration-closed.jpg'), fullPage: true, type: 'jpeg', quality: 85, animations: 'disabled' });
});
