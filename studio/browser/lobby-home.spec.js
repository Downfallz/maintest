import { test, expect } from '@playwright/test';
import { fileURLToPath } from 'node:url';

const player = { id: 'acct_aaaaaaaaaaaaaaaa', username: 'MarcF' };
const friend = { id: 'acct_bbbbbbbbbbbbbbbb', username: 'Bastien' };
const challenger = { id: 'acct_cccccccccccccccc', username: 'Alex' };
const battle = {
  id: 'battle-one', format: '3v3', over: false, waiting: false, openSeats: 0, round: 3,
  seats: [{ seat: 'player1', kind: 'person', name: 'MarcF', turn: true }, { seat: 'player2', kind: 'person', name: 'Bastien' }],
};
const held = { table: battle, seats: [{ seat: 'player1', token: 'seat-token' }] };

async function home(page, { account = player, mode = 'dev', delayMine = false, operator = false, meDown = false, notice = null, registrationClosed = false, games = null, finished = { players: [], bots: [] }, opponents = [], tutorials = null, opensTutorial = false, search = '', invitations = [], incoming = [challenger], publicTables = [battle], scenarios = null, newAccount = false, invited = null, holdInvitation = null, lobbyDown = false } = {}) {
  const state = { holdInvitation, account, mode, meDown, offline: lobbyDown, mineReads: 0, friendReads: 0, finishedReads: [], privateReads: [], release: null, invitations, publicTables,
    friends: { friends: [friend], incoming, outgoing: [] } };
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  state.errors = errors;
  await page.addInitScript(() => {
    window.permissionRequests = 0;
    if (window.Notification) Notification.requestPermission = async () => { window.permissionRequests++; return 'denied'; };
    window.google = { accounts: { id: { initialize(options) { window.googleSignIn = options.callback; }, renderButton(el, options) {
      window.googleButton = options;
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
    // A first sign-in names itself in the name window: the host hands a ticket, then takes the name.
    if (path === '/api/account/google' && newAccount) return json({ needsUsername: true, ticket: 'name-ticket' });
    if (path === '/api/account' && route.request().method() === 'POST') { state.named = route.request().postDataJSON(); state.account = { id: player.id, username: state.named.username }; return json({}); }
    // What a link's keys are, asked signed out: the same answer the table's page reads before a seat is taken.
    if (path === '/api/lobby/invitations') {
      state.invitationAsks = [...(state.invitationAsks ?? []), route.request().postDataJSON()];
      if (state.holdInvitation) await state.holdInvitation;
      return json({ invitations: invited ? [{ token: 'inv', kind: 'invitation', seat: 'player2', table: invited }] : [{ token: 'inv', kind: 'spent', seat: null, table: null }] });
    }
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
    if (path === '/api/scenarios' && scenarios) {
      state.scenarioReads = (state.scenarioReads ?? 0) + 1;
      return state.account ? json({ scenarios }) : route.fulfill({ status: 401, json: { message: 'Sign in to practise a scenario.' } });
    }
    // The series as the host says it for this account (O4); a function reads the state a test changes.
    if (path === '/api/tutorials' && tutorials) {
      state.tutorialReads = (state.tutorialReads ?? 0) + 1;
      return state.account ? json(typeof tutorials === 'function' ? tutorials(state) : tutorials) : route.fulfill({ status: 401, json: { message: 'Sign in to practise a scenario.' } });
    }
    if (path === '/api/account/preferences') {
      if (!state.account) return route.fulfill({ status: 401, json: { message: 'Sign in first.' } });
      if (state.holdPreferences) await state.holdPreferences;
      if (route.request().method() === 'PUT') { state.saved = route.request().postDataJSON(); return json({ preferences: state.saved, saved: true }); }
      return json({ preferences: state.saved ?? { pace: 'step', speed: 1, help: true, reducedMotion: false }, saved: Boolean(state.saved) });
    }
    return route.fulfill({ status: 404, json: { message: `Unexpected ${path}` } });
  });
  // A tutorial's own page is the battle view's to test: here it is only where the lobby leads.
  await page.route('**/battle?scenario=*', route => route.fulfill({ contentType: 'text/html', body: '<title>Tutorial</title>' }));
  await page.goto(`/table/lobby.html${search}`);
  if (opensTutorial) return state;
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

test('a guest can sign in above the fold and reach the public game website', async ({ page }, testInfo) => {
  const state = await home(page, { account: null, mode: 'google' });
  await expect(page.getByRole('heading', { name: 'Sign in to play.' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Continue with Google' })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('home-guest.jpg'), fullPage: true, type: 'jpeg', quality: 85, animations: 'disabled' });
  await expect(page.locator('#home-games')).toBeHidden();
  await expect(page.locator('#friends-open')).toBeHidden();
  await expect(page.getByRole('button', { name: 'Refresh', exact: true })).toHaveCount(0);
  await expect(page.locator('body')).not.toContainText('Saved on this browser');
  await expect(page.getByRole('button', { name: 'Continue with Google' })).toBeInViewport();
  await expect(page.getByRole('link', { name: 'About the game' })).toHaveAttribute('href', 'https://downfallinstinct.com/');
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
  await expect(page.locator('#sign-in-panel')).toBeHidden();
  await expect(page.locator('#friends-panel')).toBeHidden();
  await expect(page.locator('#privacy-panel')).toBeHidden();
  await expect(page.locator('#friend-requests-badge')).toHaveText('1');
  await page.screenshot({ path: testInfo.outputPath('home-member.jpg'), fullPage: true, type: 'jpeg', quality: 85, animations: 'disabled' });
  await openPanel(page, /^Friends/);
  await expect(page.getByRole('dialog', { name: 'Friends', exact: true })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('home-friends.jpg'), fullPage: false, type: 'jpeg', quality: 85, animations: 'disabled' });
  await page.getByRole('button', { name: 'Accept Alex' }).click();
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
  // A friend is their initial beside their name, and Invite is quiet: the challenge's filled action is the window's.
  await expect(page.locator('#friend-list .friend-row').first().locator('.account-initial')).toHaveText(friend.username[0]);
  await expect(page.locator('#friend-list').getByRole('button', { name: `Invite ${friend.username}` })).not.toHaveClass(/primary/);
  await expect(page.locator('#friend-ask-submit')).not.toHaveClass(/primary/);
  await page.locator('#friend-list').getByRole('button', { name: `Invite ${friend.username}` }).click();
  await expect(page.locator('#friends-dialog')).toBeHidden();
  await expect(page.locator('#create-dialog')).toBeVisible();
  await expect(page.locator('#opponent')).toHaveValue('friend');
  await expect(page.locator('#friend')).toHaveValue(friend.id);
  // Opened on a friend, the window names them in place of the tabs and the two selects, and Ranked has the focus.
  await expect(page.locator('#create-friend')).toBeVisible();
  await expect(page.locator('#create-friend-name')).toHaveText(friend.username);
  await expect(page.locator('#create-tabs')).toBeHidden();
  await expect(page.locator('#opponent')).toBeHidden();
  await expect(page.locator('#friend-row')).toBeHidden();
  await expect(page.locator('#ranked')).toBeFocused();
  await expect(page.locator('#create-dialog')).toHaveAttribute('aria-describedby', 'create-friend');
  await expect(page.locator('dialog[open]')).toHaveCount(1);
  await expect(page.locator('#create-submit')).toBeEnabled();
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  // New battle opens the window whole again.
  await page.locator('#new-table').click();
  await expect(page.locator('#create-tabs')).toBeVisible();
  await expect(page.locator('#create-friend')).toBeHidden();
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
  await expect(page.locator('#sign-in-panel')).toBeHidden();
  await expect(page.locator('#leaderboard-link')).toBeHidden();
  expect(state.privateReads).toContain('/api/lobby/resume');
  state.offline = true;
  await page.evaluate(() => dispatchEvent(new Event('online')));
  await expect(page.locator('#lobby-status')).toContainText('Reconnecting automatically');
  state.offline = false;
  await page.evaluate(() => dispatchEvent(new Event('online')));
  await expect(page.locator('#lobby-status')).toHaveText('');
  await page.getByRole('button', { name: 'New battle' }).click();
  await expect(page.locator('#create-dialog')).toBeVisible();
  await expect(page.locator('#opponent option[value="friend"]')).toBeHidden();
  expect(state.errors).toEqual([]);
});

test('navigation and dialogs fit narrow, intermediate and short landscape screens', async ({ page }) => {
  // The widest header: a 20-character name of the widest letter, an operator, and every entry of the bar shown,
  // Learn to play included. Its content is 1243 px wide, inside the header's 32 px on each side: the bar needs 1307 px,
  // and the menu folds up to 1306 (#669). In the bar, the header holds its content: nothing spills into the gutter.
  await home(page, { account: { ...player, username: 'WWWWWWWWWWWWWWWWWWWW' }, operator: true });
  for (const [width, height] of [[320, 740], [900, 700], [1101, 800], [1240, 800], [1306, 800], [1307, 800], [1440, 900], [740, 320]]) {
    await page.setViewportSize({ width, height });
    await noOverflow(page);
    await expect(page.locator('#account-menu summary')).toBeInViewport({ ratio: 1 });
    if (width <= 1306) await expect(page.locator('#home-menu-toggle')).toBeVisible();
    else {
      await expect(page.locator('#home-menu-toggle')).toBeHidden();
      await expect(page.locator('#learn-link')).toBeInViewport({ ratio: 1 });
      await expect(page.locator('#nav-admin-link')).toBeInViewport({ ratio: 1 });
      const rows = await page.locator('#home-navigation > :visible').evaluateAll(items => new Set(items.map(item => Math.round(item.getBoundingClientRect().top))).size);
      expect(rows).toBe(1);
      expect(await page.locator('.home-header').evaluate(header => header.scrollWidth <= header.clientWidth)).toBe(true);
    }
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
  // The text face is the system's. In a wide one (DejaVu Sans, CI's Linux runner) the bar is wider than the header's
  // cap: the player's name gives way, and the bar still fits at the first width it shows and on a wide screen.
  await page.addStyleTag({ content: 'body, body * { font-family: "DejaVu Sans", sans-serif !important; }' });
  for (const width of [1307, 1440]) {
    await page.setViewportSize({ width, height: 800 });
    await noOverflow(page);
    expect(await page.locator('.home-header').evaluate(header => header.scrollWidth <= header.clientWidth)).toBe(true);
    await expect(page.locator('#account-menu summary')).toBeInViewport({ ratio: 1 });
    expect((await page.locator('#account-menu summary').boundingBox()).width).toBeGreaterThanOrEqual(44);
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
  const remove = page.getByRole('button', { name: 'Remove Bastien' });
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

test('the account menu holds Settings, Report or suggest and Sign out; the account\'s lasting actions wait at the foot of Settings, Account', async ({ page }, testInfo) => {
  const state = await home(page);
  await page.locator('#account-menu summary').click();
  await expect(page.locator('#account-menu .account-menu button')).toHaveText(['Settings', 'Report or suggest…', 'Sign out']);
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
    // The bell reads what waits on its own on every page: the turn at Bastien's table and Alex's request. Under
    // 360 px the battle view's bar has no room for it: the menu holds it, its count on the menu's button (UX11).
    const folded = file === 'battle' && page.viewportSize().width < 360;
    const badge = page.locator(folded ? '#menu-inbox-badge' : '#inbox-badge');
    await expect(badge).toHaveText('2');
    // Whole on screen, even where the header sits at the screen's top edge (the battle view on a phone).
    await expect(badge).toBeInViewport({ ratio: 1 });
    expect(await badge.evaluate(one => one.getBoundingClientRect().top >= 0)).toBe(true);
    await openNavigation(page);
    const nav = page.getByRole('navigation', { name: 'Main navigation' });
    await expect(nav.locator(':scope > :visible')).toHaveText(['Lobby', /Friends/, 'Leaderboard', 'Your stats', 'Rules', 'Learn to play', 'Join with a code', ...folded ? [/Notifications/] : []]);
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
  // At rest the status says nothing: the record says it (UX10).
  await expect(page.locator('#stats-status')).toHaveText('');
  await expect(page.locator('#glance')).toContainText('2 wins in a row');
  await expect(page.locator('#spell-rows')).toContainText('Strike');
  await expect(page.locator('#mutation-rows')).toContainText('No mutation acquired yet.');
  // The spell cast most stands large; with no mutation acquired, Most acquired is not drawn.
  await expect(page.locator('#leaned-spell')).toContainText('Strike');
  await expect(page.locator('#leaned-spell')).toContainText('Cast 21 times in 3 games');
  await expect(page.locator('#leaned-mutation')).toBeHidden();
  await expect(page.locator('#stats-empty')).toBeHidden();
  // Ranked games are all 3v3 (ADR 0171): no table of creatures and no table of formats.
  await expect(page.locator('#creature-rows, #formats')).toHaveCount(0);
  await openNavigation(page);
  await expect(page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'Your stats' })).toHaveAttribute('aria-current', 'page');
  await page.keyboard.press('Escape');
  await noOverflow(page);
});

test('the mutation a player acquires most stands with its painting and its count', async ({ page }) => {
  await home(page);
  await page.route('**/api/stats', route => route.fulfill({ json: {
    ranked: { games: 1, won: 0, lost: 1, drawn: 0 }, streak: { outcome: 'lost', length: 1 }, bestWinStreak: 0, averageRounds: 2, withTeams: 1,
    // A long name beside the painting: the card wraps rather than pushing the page wider at 320 px.
    mutations: [{ id: 'tier:deathstalker:v1', name: 'Deathstalker', artwork: 'predator-stage3-deathstalker', bought: 2, won: 0 }], spells: [{ id: 'spell:brace:v1', name: 'Brace', portrait: 'brace', casts: 2, games: 1, won: 0 }],
  } }));
  await page.goto('/table/stats.html');
  const card = page.locator('#leaned-mutation');
  await expect(card).toContainText('Deathstalker');
  await expect(card).toContainText('Acquired 2 times');
  await expect(card.locator('img.leaned-picture')).toHaveAttribute('src', /page-art\/mutation-predator-stage3-deathstalker-/);
  expect(await card.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
  await expect(card.locator('img.leaned-picture')).toHaveAttribute('width', /\d+/);
  // The spell cast most, and its row, carry the portrait the battle draws beside its name.
  await expect(page.locator('#leaned-spell img.leaned-portrait')).toHaveAttribute('src', /page-art\/spell-brace-40\./);
  await expect(page.locator('#spell-rows img.row-portrait')).toHaveAttribute('src', /page-art\/spell-brace-40\./);
  await expect(page.locator('#spell-rows td.player')).toHaveText('Brace');
  await expect(page.locator('#glance')).toContainText('2 rounds');
  await noOverflow(page);
});

test('the ladder says where the player stands, and how far they moved since this browser last showed them', async ({ page }) => {
  await home(page);
  const ladder = rating => ({ provisionalAbove: 110, players: [
    { rank: 1, username: 'Rook', rating: 1662, deviation: 290, games: 1, wins: 1, losses: 0, draws: 0, provisional: true, you: false },
    { rank: 2, username: player.username, rating, deviation: 290, games: 1, wins: 0, losses: 1, draws: 0, provisional: true, you: true },
  ], you: { username: player.username, standing: { rank: 2, username: player.username, rating, deviation: 290, games: 1, wins: 0, losses: 1, draws: 0, provisional: true, you: true }, bots: [] } });
  let answer = ladder(1338);
  await page.route('**/api/leaderboard', route => route.fulfill({ json: answer }));
  // This browser last showed the player 1400 at the same place.
  await page.addInitScript(name => localStorage.setItem(`downfall.ladder.seen.v1:${name}`, JSON.stringify({ standing: { rank: 2, rating: 1400, provisional: true } })), player.username);
  await page.goto('/table/leaderboard.html');
  const place = page.locator('#you');
  await expect(place).toContainText('#2 · Provisional');
  await expect(place).toContainText('1338 ±290');
  await expect(place).toContainText('Down 62 since you last looked.');
  await expect(place).toContainText('0 won, 1 lost, 0 drawn in 1 game');
  await expect(place.getByRole('img', { name: /Your rating 1338 ±290, among 2 players from 1338 to 1662/ })).toBeVisible();
  // At rest the status says nothing.
  await expect(page.locator('#ladder-status')).toHaveText('');
  // What it showed is remembered.
  await expect.poll(() => page.evaluate(name => JSON.parse(localStorage.getItem(`downfall.ladder.seen.v1:${name}`)).standing.rating, player.username)).toBe(1338);
  await noOverflow(page);
});

const ranked = (username, rating, rank = 2) => ({ rank, username, rating, deviation: 290, games: 1, wins: 0, losses: 1, draws: 0, provisional: true, you: true });
const ladderOf = (username, rating) => ({ provisionalAbove: 110, players: [
  { rank: 1, username: 'Rook', rating: 1662, deviation: 290, games: 1, wins: 1, losses: 0, draws: 0, provisional: true, you: false }, ranked(username, rating),
], you: { username, standing: ranked(username, rating), bots: [] } });

test('a long player name wraps in Your place, and the place stays inside the card', async ({ page }) => {
  await home(page);
  const long = 'WWWWWWWWWWWWWWWWWWWW';
  await page.route('**/api/leaderboard', route => route.fulfill({ json: ladderOf(long, 1338) }));
  await page.goto('/table/leaderboard.html');
  const card = page.locator('.your-place');
  await expect(page.locator('.you-place')).toHaveText('#2 · Provisional');
  const [badge, box] = await Promise.all([page.locator('.you-place').boundingBox(), card.boundingBox()]);
  expect(badge.x + badge.width).toBeLessThanOrEqual(box.x + box.width);
  await noOverflow(page);
});

test('a live read keeps the keyboard on the action it was on', async ({ page }) => {
  await home(page);
  await page.route('**/api/leaderboard', route => route.fulfill({ json: { provisionalAbove: 110, players: [], you: { username: player.username, standing: null, bots: [] } } }));
  await page.goto('/table/leaderboard.html');
  const action = page.getByRole('link', { name: 'Open a ranked table →' });
  await action.focus();
  // The same answer again, as a stream's word about an unrelated table brings it.
  await page.evaluate(async name => {
    const { drawLadder } = await import('/table/leaderboard.js');
    drawLadder(document, { provisionalAbove: 110, players: [], you: { username: name, standing: null, bots: [] } }, { first: false });
  }, player.username);
  await expect(action).toBeFocused();
});

test('another player signed in while the ladder was open is compared with their own last look, not the first player\'s', async ({ page }) => {
  await home(page);
  await page.addInitScript(() => {
    localStorage.setItem('downfall.ladder.seen.v1:Alice', JSON.stringify({ standing: { rank: 2, rating: 1300, provisional: true } }));
    localStorage.setItem('downfall.ladder.seen.v1:Bob', JSON.stringify({ standing: { rank: 2, rating: 1550, provisional: true } }));
  });
  await page.route('**/api/leaderboard', route => route.fulfill({ json: ladderOf('Alice', 1338) }));
  await page.goto('/table/leaderboard.html');
  await expect(page.locator('#you')).toContainText('Up 38 since you last looked.');
  await page.evaluate(async ladder => {
    const { drawLadder } = await import('/table/leaderboard.js');
    drawLadder(document, ladder, { first: false });
  }, ladderOf('Bob', 1560));
  await expect(page.locator('#you')).toContainText('Bob');
  await expect(page.locator('#you')).toContainText('Up 10 since you last looked.');
  await expect(page.locator('#ladder-status')).toHaveText('');
});

test('the ladder and the statistics still draw when the browser refuses its storage', async ({ page }) => {
  const state = await home(page);
  await page.addInitScript(() => {
    Object.defineProperty(window, 'localStorage', { configurable: true, get() { throw new DOMException('The operation is insecure.', 'SecurityError'); } });
  });
  await page.route('**/api/leaderboard', route => route.fulfill({ json: { provisionalAbove: 110, players: ladderOf('Rook', 1338).players.slice(0, 1), you: null } }));
  await page.goto('/table/leaderboard.html');
  await expect(page.locator('#ladder-rows tr')).toHaveCount(1);
  await page.route('**/api/stats', route => route.fulfill({ json: {
    ranked: { games: 0, won: 0, lost: 0, drawn: 0 }, streak: null, bestWinStreak: 0, averageRounds: null, withTeams: 0, mutations: [], spells: [],
  } }));
  await page.goto('/table/stats.html');
  await expect(page.locator('#stats-empty')).toBeVisible();
  expect(state.errors).toEqual([]);
});

test('the first ranked result that arrives while the statistics are open is announced', async ({ page }) => {
  await home(page);
  const empty = { ranked: { games: 0, won: 0, lost: 0, drawn: 0 }, streak: null, bestWinStreak: 0, averageRounds: null, withTeams: 0, mutations: [], spells: [] };
  await page.route('**/api/stats', route => route.fulfill({ json: empty }));
  await page.goto('/table/stats.html');
  await expect(page.locator('#stats-empty')).toBeVisible();
  await page.evaluate(async () => {
    const { drawStats } = await import('/table/stats.js');
    drawStats(document, { ranked: { games: 1, won: 1, lost: 0, drawn: 0 }, streak: { outcome: 'won', length: 1 }, bestWinStreak: 1, averageRounds: 3, withTeams: 1, mutations: [], spells: [] });
  });
  await expect(page.locator('#stats-status')).toHaveText('Your record is now 1–0–0.');
});

test('a player not on the ladder is told how to get on it, and the action opens a ranked table for another player', async ({ page }) => {
  await home(page);
  await page.route('**/api/leaderboard', route => route.fulfill({ json: { provisionalAbove: 110, players: [], you: { username: player.username, standing: null, bots: [] } } }));
  await page.goto('/table/leaderboard.html');
  await expect(page.locator('#you')).toContainText('Not on the ladder yet.');
  await expect(page.locator('#ladder-empty')).toBeVisible();
  await expect(page.locator('#ladder-table')).toBeHidden();
  await expect(page.locator('#players-caption')).toBeHidden();
  await expect(page.getByRole('link', { name: 'Open a ranked table →' })).toHaveAttribute('href', '/lobby?rematch=person');
  await noOverflow(page);
});

test('an empty page\'s action opens the Create window on a ranked table for another player', async ({ page }) => {
  const state = await home(page, { search: '?rematch=person' });
  await expect(page.locator('#create-dialog')).toBeVisible();
  await expect(page.locator('#create-pvp')).toHaveAttribute('aria-selected', 'true');
  await expect(page.locator('#opponent')).toHaveValue('person');
  await expect(page.locator('#ranked')).toBeChecked();
  await expect(page.locator('#format')).toHaveValue('3v3');
  await expect(page.locator('#create-friend')).toBeHidden();
  expect(state.errors).toEqual([]);
});

test('a player with no ranked game reads one empty state and the action that fills it', async ({ page }) => {
  await home(page);
  await page.route('**/api/stats', route => route.fulfill({ json: {
    ranked: { games: 0, won: 0, lost: 0, drawn: 0 }, streak: null, bestWinStreak: 0, averageRounds: null, withTeams: 0, mutations: [], spells: [],
  } }));
  await page.goto('/table/stats.html');
  await expect(page.locator('#stats-empty')).toBeVisible();
  await expect(page.locator('#stats-body')).toBeHidden();
  await expect(page.getByRole('link', { name: 'Open a ranked table →' })).toHaveAttribute('href', '/lobby?rematch=person');
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
  // The most pressing of them is the featured space's on the first screen (UX9); the band follows it.
  await expect(page.locator('#featured-action')).toHaveAttribute('aria-label', 'Play your turn: round 3 against Bastien');
  await expect(page.locator('#featured-action')).toBeInViewport({ ratio: 1 });
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
  await page.locator('#friends-dialog').getByRole('button', { name: /^Accept / }).click();
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

// The scenarios the operator listed under Practice (scenarios plan, S6), offered to a signed-in player to open as practice.
test('a signed-in player finds the listed scenarios under Practice, each opening its practice', async ({ page }, info) => {
  const state = await home(page, { scenarios: [{ id: 'sc-1', title: 'Bleed after a stun', goal: 'Keep Creature 5 alive', round: 4, seat: 'player2', format: '2v2', link: '/battle?scenario=sc-1' }] });
  const section = page.locator('#practice-scenarios');

  await expect(section).toBeVisible();
  const card = section.locator('.game-card.practice');
  await expect(card).toHaveCount(1);
  await expect(card.locator('h3')).toHaveText('Bleed after a stun');
  await expect(card).toContainText('2v2 · from round 4 · you play player 2');
  await expect(card).toContainText('Keep Creature 5 alive');
  await expect(card.locator('a', { hasText: 'Practise' })).toHaveAttribute('href', '/battle?scenario=sc-1');
  await expect(page.locator('#practice-count')).toHaveText('1');
  await section.scrollIntoViewIfNeeded();
  await page.screenshot({ path: info.outputPath('lobby-practice.png') });
  await noOverflow(page);
  expect(state.errors).toEqual([]);
});

test('with nothing listed there is no Practice section', async ({ page }) => {
  const state = await home(page, { scenarios: [] });
  await expect.poll(() => state.scenarioReads ?? 0).toBeGreaterThan(0);
  await expect(page.locator('#practice-scenarios')).toBeHidden();
});

test('a guest is not asked for the listed scenarios and sees no Practice section', async ({ page }) => {
  const state = await home(page, { account: null, scenarios: [{ id: 'sc-1', round: 1, seat: 'player1', link: '/battle?scenario=sc-1' }] });
  await expect(page.locator('#practice-scenarios')).toBeHidden();
  expect(state.scenarioReads ?? 0).toBe(0);
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

test('a signed-in player can visit the showcase from the account menu and the brand still leads home', async ({ page }) => {
  await home(page);
  await expect(page.getByRole('link', { name: 'Downfall Instinct home' })).toHaveAttribute('href', '/');
  await page.locator('#account-menu summary').click();
  await expect(page.getByRole('link', { name: 'Game website' })).toHaveAttribute('href', 'https://downfallinstinct.com/');
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
  await expect(rows.first()).toContainText('Rival0');
  await expect(rows.first().getByRole('button', { name: 'Take a seat: 3v3 battle, table open-0' })).toBeVisible();
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
  // Under the card's Your turn tag, the line says what follows it (UX9).
  await expect(card.locator('.status-tag.your-turn')).toHaveText('Your turn');
  await expect(card.locator('.game-turn')).toHaveText('1 h left · Round 3');
  const reads = state.mineReads;

  await page.clock.fastForward('03:00');

  await expect(card.locator('.game-turn')).toHaveText(/^5\d min left · Round 3$/);
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

const firstTutorial = { id: 'sc-repo-tutorial-your-first-combat', order: 1, of: 4, title: 'Your first combat', goal: 'Cast a spell with each of your creatures.', round: 2, seat: 'player1', format: '3v3', link: '/battle?scenario=sc-repo-tutorial-your-first-combat' };
const secondTutorial = { id: 'sc-repo-tutorial-who-acts-first', order: 2, of: 4, title: 'Who acts first', goal: 'Choose a tempo.', round: 2, seat: 'player1', format: '3v3', link: '/battle?scenario=sc-repo-tutorial-who-acts-first' };
const series = (first, second = 'new') => ({ opens: first === 'new' ? firstTutorial.link : null, tutorials: [{ ...firstTutorial, state: first }, { ...secondTutorial, state: second }] });

test('a new account\'s first visit opens tutorial 1, and the home page is its own again on the way back', async ({ page }) => {
  const state = await home(page, { tutorials: series('new'), opensTutorial: true });

  await expect(page).toHaveTitle('Tutorial');
  expect(new URL(page.url()).search).toBe('?scenario=sc-repo-tutorial-your-first-combat');
  // Back at the home page in the same visit, before the host has heard of a skip: it does not take the page again.
  await page.goto('/table/lobby.html');
  await expect(page.locator('body')).toHaveAttribute('data-home', 'member');
  await expect(page.locator('#learn-to-play .game-card')).toHaveCount(2);
  expect(new URL(page.url()).pathname).toBe('/table/lobby.html');
  expect(state.errors).toEqual([]);
});

test('an address that asks for a rematch is answered before the first tutorial, which waits for a plain visit', async ({ page }) => {
  const state = await home(page, { tutorials: series('new'), search: `?rematch=${friend.id}` });

  await expect(page.locator('#create-dialog')).toBeVisible();
  await expect(page.locator('#learn-to-play')).toBeVisible();
  expect(new URL(page.url()).pathname).not.toBe('/battle');
  expect(state.errors).toEqual([]);
});

test('a skipped tutorial never opens by itself again: it waits under Learn to play, and in the main menu', async ({ page }, info) => {
  const state = await home(page, { tutorials: series('skipped', 'done') });
  const section = page.locator('#learn-to-play');

  await expect(section).toBeVisible();
  expect(new URL(page.url()).pathname).toBe('/table/lobby.html');
  const cards = section.locator('.game-card');
  await expect(cards.nth(0).locator('h3')).toHaveText('Your first combat');
  // UX8: the state as the host says it, its painting, and the progress in words. Every card's Play is an outline (UX9):
  // the next lesson is filled once, in the featured space, when it is what the player came for.
  await expect(cards.nth(0).locator('.status-tag')).toHaveText('Skipped');
  await expect(cards.nth(0).locator('a')).toHaveText('Play');
  await expect(cards.nth(0).locator('a')).not.toHaveClass(/primary/);
  await expect(cards.nth(0)).toHaveAttribute('data-next', 'true');
  await expect(cards.nth(0).locator('img.tutorial-picture')).toHaveAttribute('alt', '');
  await expect(cards.nth(1).locator('.status-tag')).toHaveText('Done');
  await expect(cards.nth(1).locator('a')).not.toHaveClass(/primary/);
  await expect(page.locator('#tutorial-progress')).toHaveText('1 of 2 done');
  await section.scrollIntoViewIfNeeded();
  await page.screenshot({ path: info.outputPath('lobby-learn.png') });
  await noOverflow(page);
  // The main menu leads there from every page, beside Rules, for a signed-in player (#669); the account menu no longer does.
  await openNavigation(page);
  const learn = page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'Learn to play' });
  await expect(learn).toBeVisible();
  await expect(learn).toHaveAttribute('href', '/#learn-to-play');
  expect((await learn.boundingBox()).height).toBeGreaterThanOrEqual(44);
  await expect(page.locator('#account-menu .account-menu')).not.toContainText('Learn to play');
  expect(state.errors).toEqual([]);
});

test('a finished tutorial is played again from Learn to play', async ({ page }) => {
  await home(page, { tutorials: series('done', 'done') });
  const card = page.locator('#learn-to-play .game-card').first();

  await expect(card.locator('a')).toHaveText('Play again');
  await card.locator('a').click();

  await expect(page).toHaveTitle('Tutorial');
  expect(new URL(page.url()).search).toBe('?scenario=sc-repo-tutorial-your-first-combat');
});

test('the settings show the first tutorial again, whatever the account did with it', async ({ page }) => {
  const state = await home(page, { tutorials: series('skipped') });
  const dialog = await openAccountSettings(page);
  const again = dialog.getByRole('button', { name: 'Show the first tutorial again' });

  await expect(again).toBeVisible();
  const box = await again.boundingBox();
  expect(box.height).toBeGreaterThanOrEqual(44);
  await noOverflow(page);
  await again.click();

  await expect(page).toHaveTitle('Tutorial');
  expect(state.errors).toEqual([]);
});

test('a guest is not asked for the tutorials: no Learn to play, in the page or the menu, and nothing in the settings', async ({ page }) => {
  const state = await home(page, { account: null, tutorials: series('new') });

  await expect(page.locator('#learn-to-play')).toBeHidden();
  await expect(page.locator('#learn-link')).toBeHidden();
  await openNavigation(page);
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: 'Settings' }).click();
  await expect(page.locator('#tutorial-again-row')).toBeHidden();
  expect(state.tutorialReads ?? 0).toBe(0);
});

const rooks = { id: 'rooks-table', format: '3v3', waiting: true, over: false, round: null, openSeats: 1,
  seats: [{ seat: 'player1', kind: 'person', name: 'Rook' }, { seat: 'player2', kind: 'open', name: 'Open seat' }] };
const invitedSearch = `?next=${encodeURIComponent('/?player2=inv')}`;

test('the game\'s door shows its paintings as pictures of the kit, the ground asked for first, and the sign-in within reach at 320 px', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 568 });
  const state = await home(page, { account: null, mode: 'google' });

  await expect(page.locator('#sign-in-eyebrow')).toHaveText('Beta');
  await expect(page.locator('#sign-in-game')).toHaveText('A turn-based duel: your three creatures mutate against another player\'s three.');
  const ground = page.locator('#sign-in-art img.sign-in-ground');
  await expect(ground).toHaveAttribute('fetchpriority', 'high');
  await expect(ground).toHaveAttribute('alt', '');
  for (const image of await page.locator('#sign-in-art img').all()) {
    expect(Number(await image.getAttribute('width'))).toBeGreaterThan(0);
    expect(Number(await image.getAttribute('height'))).toBeGreaterThan(0);
  }
  await expect(page.getByRole('button', { name: 'Continue with Google' })).toBeInViewport();
  // Google draws its own button; asked rectangular, it is the nearest to the identity's square buttons.
  expect(await page.evaluate(() => window.googleButton.shape)).toBe('rectangular');
  await noOverflow(page);
  expect(state.invitationAsks).toBeUndefined();
  expect(state.errors).toEqual([]);
});

test('a friend who follows an invitation signed out is told who invites them, and the name window carries the place to the table', async ({ page }) => {
  const state = await home(page, { account: null, mode: 'google', newAccount: true, invited: rooks, search: invitedSearch });

  await expect(page.getByRole('heading', { name: 'Rook invited you to a 3v3 battle.' })).toBeVisible();
  await expect(page.locator('#sign-in-eyebrow')).toHaveText('Invitation');
  await expect(page.locator('#sign-in-account')).toHaveText('Sign in to take your place. Your games follow you everywhere.');
  expect(state.invitationAsks).toEqual([{ tokens: ['inv'] }]);

  await page.evaluate(() => window.googleSignIn({ credential: 'a-token-google-signed' }));
  const named = page.getByRole('dialog', { name: 'Choose your player name' });
  await expect(named).toBeVisible();
  await expect(page.locator('#name-next')).toHaveText('Next: your place as Player 2 at Rook\'s table.');
  await expect(named.getByRole('button', { name: 'Take this name →' })).toBeVisible();

  // Closed without a name, the focus goes back to the way in.
  await page.keyboard.press('Escape');
  await expect(named).toBeHidden();
  await expect(page.getByRole('button', { name: 'Continue with Google' })).toBeFocused();

  // Named, the page goes back to the invitation it was sent from, the window still saying where.
  await page.route('**/?player2=inv', route => route.fulfill({ contentType: 'text/html', body: '<title>The invitation</title>' }));
  await page.evaluate(() => window.googleSignIn({ credential: 'a-token-google-signed' }));
  await page.locator('#username').fill('Walker');
  await named.getByRole('button', { name: 'Take this name →' }).click();
  await expect(page).toHaveTitle('The invitation');
  expect(state.named).toEqual({ ticket: 'name-ticket', username: 'Walker' });
  expect(state.errors).toEqual([]);
});

test('an invitation is one before the host describes it, and a late answer naming the inviter leaves the sign-in where it was', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 568 });
  let release;
  const held = new Promise(resolve => { release = resolve; });
  const state = await home(page, { account: null, mode: 'google', invited: rooks, search: invitedSearch, holdInvitation: held });

  await expect(page.getByRole('heading', { name: 'You are invited to a battle.' })).toBeVisible();
  const button = await page.getByRole('button', { name: 'Continue with Google' }).boundingBox();
  release();
  await expect(page.getByRole('heading', { name: 'Rook invited you to a 3v3 battle.' })).toBeVisible();

  expect(await page.getByRole('button', { name: 'Continue with Google' }).boundingBox()).toEqual(button);
  expect(state.errors).toEqual([]);
});

test('the panel an invitation opens shows the duel whole on a phone, in place of the ground and the Revenant, the sign-in within reach', async ({ page }) => {
  for (const width of [320, 390]) {
    await page.setViewportSize({ width, height: 568 });
    const state = await home(page, { account: null, mode: 'google', invited: rooks, search: invitedSearch });
    await expect(page.getByRole('heading', { name: 'Rook invited you to a 3v3 battle.' })).toBeVisible();
    const duel = page.locator('#sign-in-art img.sign-in-duel');
    await expect(duel).toHaveAttribute('fetchpriority', 'high');
    await expect(duel).toHaveAttribute('alt', '');
    await expect(page.locator('#sign-in-art img')).toHaveCount(1);
    const [panel, drawn] = await Promise.all([page.locator('#sign-in-panel').boundingBox(), duel.boundingBox()]);
    expect(Math.abs(drawn.width - panel.width), `at ${width}`).toBeLessThanOrEqual(2);
    expect(Math.abs(drawn.height - drawn.width * 941 / 1672), `whole at ${width}`).toBeLessThanOrEqual(2);
    await expect(page.getByRole('button', { name: 'Continue with Google' })).toBeInViewport();
    await noOverflow(page);
    expect(state.errors).toEqual([]);
  }
});

test('on a wide screen the duel sits beside the words and fills the panel to its right edge', async ({ page }) => {
  for (const width of [760, 1000, 1279, 1280, 1600]) {
    await page.setViewportSize({ width, height: 800 });
    await home(page, { account: null, mode: 'google', invited: rooks, search: invitedSearch });
    await expect(page.getByRole('heading', { name: 'Rook invited you to a 3v3 battle.' })).toBeVisible();
    const [panel, drawn, heading] = await Promise.all(['#sign-in-panel', '#sign-in-art img.sign-in-duel', '#sign-in-heading'].map(one => page.locator(one).boundingBox()));
    expect(Math.abs(drawn.x + drawn.width - (panel.x + panel.width)), `right edge at ${width}`).toBeLessThanOrEqual(2);
    if (width >= 1280) {
      expect(drawn.x, `beside the words at ${width}`).toBeGreaterThan(heading.x);
      // The panel's box holds its two borders of 1 px; the picture fills what is inside them.
      expect(Math.abs(drawn.height - panel.height), `the panel's height at ${width}`).toBeLessThanOrEqual(3);
      // Cover cuts the sides: three quarters of the painting's width stay, so both teams show.
      expect((drawn.width / drawn.height) / (1672 / 941), `both teams at ${width}`).toBeGreaterThanOrEqual(0.75);
    } else {
      expect(drawn.height, `above the words at ${width}, never taller than half the screen`).toBeLessThanOrEqual(400 + 1);
      expect(drawn.y + drawn.height, `above the words at ${width}`).toBeLessThanOrEqual(heading.y);
    }
    await expect(page.getByRole('button', { name: 'Continue with Google' }), `at ${width}`).toBeInViewport();
    await noOverflow(page);
  }
});

test('a late answer that the link is the visitor\'s own seat turns the panel plain, and its pictures with it', async ({ page }) => {
  let release;
  const held = new Promise(resolve => { release = resolve; });
  const state = await home(page, { account: null, mode: 'google', search: invitedSearch });
  await page.route('**/api/lobby/invitations', async route => { await held; await route.fulfill({ json: { invitations: [{ token: 'inv', kind: 'seat', seat: 'player2', table: rooks }] } }); });
  await page.reload();
  await expect(page.locator('#sign-in-art img.sign-in-duel')).toHaveCount(1);
  release();
  await expect(page.locator('#sign-in-panel')).toHaveAttribute('data-door', 'plain');
  await expect(page.locator('#sign-in-art img.sign-in-creature')).toHaveCount(1);
  await expect(page.locator('#sign-in-art img.sign-in-duel')).toHaveCount(0);
  expect(state.errors).toEqual([]);
});

test('a link the host answers as the visitor\'s own seat turns the panel plain, with its ground and its Revenant', async ({ page }) => {
  const state = await home(page, { account: null, mode: 'google', invited: null, search: invitedSearch });
  await page.route('**/api/lobby/invitations', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ invitations: [{ token: 'inv', kind: 'seat', seat: 'player2', table: rooks }] }) }));
  await page.reload();
  await expect(page.locator('#sign-in-panel')).toHaveAttribute('data-door', 'plain');
  await expect(page.locator('#sign-in-art img.sign-in-creature')).toHaveCount(1);
  await expect(page.locator('#sign-in-art img.sign-in-duel')).toHaveCount(0);
  expect(state.errors).toEqual([]);
});

test('a spent link is not called an invitation that waits', async ({ page }) => {
  await home(page, { account: null, mode: 'google', invited: null, search: invitedSearch });
  await expect(page.getByRole('heading', { name: 'This invitation can no longer be used.' })).toBeVisible();
  await expect(page.locator('#sign-in-account')).toHaveText('Sign in to play. Your games follow you on every device.');
});

test('the door\'s ground covers the panel at every width', async ({ page }) => {
  for (const width of [320, 390, 760, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    await home(page, { account: null, mode: 'google' });
    const [panel, ground] = await Promise.all(['#sign-in-panel', '#sign-in-art .sign-in-ground'].map(one => page.locator(one).boundingBox()));
    expect(Math.abs(ground.width - panel.width), `at ${width}`).toBeLessThanOrEqual(2);
    // A phone's ground is 300 px under a veil that runs on into the panel's soot; a wider one covers it whole.
    expect(ground.height, `at ${width}`).toBeGreaterThanOrEqual((width > 700 ? panel.height : Math.min(panel.height, 300)) - 2, 'within the panel\'s two borders');
  }
});

test('a new account named on a plain visit is told the first lesson in the window before the lesson opens', async ({ page }) => {
  // What the window said as the page left, kept where the next page of this origin can read it.
  await page.addInitScript(() => addEventListener('pagehide', () => {
    const next = document.getElementById('name-next'), window = document.getElementById('name-dialog');
    if (next) sessionStorage.setItem('left-with', JSON.stringify([next.textContent, window.open]));
  }));
  const state = await home(page, { account: null, mode: 'google', newAccount: true, tutorials: series('new') });

  await page.evaluate(() => window.googleSignIn({ credential: 'a-token-google-signed' }));
  await expect(page.locator('#name-next')).toBeHidden();
  await page.locator('#username').fill('Walker');
  await page.getByRole('button', { name: 'Take this name →' }).click();

  await expect(page).toHaveTitle('Tutorial');
  expect(JSON.parse(await page.evaluate(() => sessionStorage.getItem('left-with')))).toEqual(['Next: Tutorial 1, Your first combat.', true]);
  expect(state.errors).toEqual([]);
});

test('a signed-in player\'s home loads none of the door\'s paintings', async ({ page }) => {
  const asked = [];
  // The door's two paintings: the home's featured space draws its own (UX9).
  page.on('request', request => { if (/\/page-art\/(eclipse-ground|revenant|duel)-/.test(request.url())) asked.push(request.url()); });
  const state = await home(page);

  await expect(page.locator('#sign-in-art img')).toHaveCount(0);
  expect(asked).toEqual([]);
  expect(state.errors).toEqual([]);
});

// The home's featured space (UX9, docs/art/journeys/lobby.md): one game, lesson or new battle, with the home's one
// filled action, following what the host says of the player's games and lessons.
const filledOnTheHome = page => page.locator('#home-games :is(button, a).primary:visible');

test('a new account is featured its first lesson on the ancestral creature, the home\'s one filled action, within reach at 320 px', async ({ page }, info) => {
  await page.setViewportSize({ width: 320, height: 568 });
  const state = await home(page, { games: [], tutorials: series('skipped', 'new') });
  const featured = page.locator('#featured');

  await expect(featured).toHaveAttribute('data-featured', 'lesson');
  await expect(page.locator('#featured-eyebrow')).toHaveText('Tutorial 1 of 4');
  await expect(page.locator('#welcome')).toHaveText('Your first combat');
  await expect(page.locator('#featured-action')).toHaveText('Play →');
  await expect(featured.locator('img.featured-ground')).toHaveAttribute('fetchpriority', 'high');
  await expect(featured.locator('img.featured-creature')).toHaveAttribute('src', /\/page-art\/home-ancestral-/);
  await expect(filledOnTheHome(page)).toHaveCount(1);
  await expect(page.locator('#new-table')).not.toHaveClass(/primary/);
  await expect(page.locator('#featured-action')).toBeInViewport({ ratio: 1 });
  await page.screenshot({ path: info.outputPath('lobby-featured-lesson-320.png') });
  await noOverflow(page);

  await page.locator('#featured-action').click();
  await expect(page).toHaveTitle('Tutorial');
  expect(state.errors).toEqual([]);
});

test('with nothing to resume or learn, the featured action plays a bot, and an empty list teaches and links', async ({ page }) => {
  await home(page, { games: [], tutorials: series('done', 'done') });

  await expect(page.locator('#featured')).toHaveAttribute('data-featured', 'new');
  await expect(page.locator('#featured-action')).toHaveText('Play a bot →');
  await expect(page.locator('#new-table')).not.toHaveClass(/primary/);
  await expect(filledOnTheHome(page)).toHaveCount(1);
  await page.locator('#featured-action').click();
  await expect(page.locator('#create-dialog')).toHaveJSProperty('open', true);
  await expect(page.locator('#create-solo')).toHaveAttribute('aria-selected', 'true');
  await page.locator('#create-cancel').click();
  // An empty list teaches and links.
  await expect(page.locator('#my-tables .empty-state')).toContainText('against a bot');
  await page.locator('#my-tables .empty-state button').click();
  await expect(page.locator('#create-dialog')).toHaveJSProperty('open', true);
});

test('a game waiting on the other player is featured, and when its turn arrives its status and action change together, under the same focus', async ({ page }, info) => {
  const theirs = { table: { ...battle, id: 'waits', round: 7, seats: [{ ...battle.seats[0], turn: false }, { ...battle.seats[1], turn: true }] }, seats: [{ seat: 'player1', token: 'waits-token' }] };
  const state = await home(page, { games: [theirs], tutorials: series('done', 'new') });
  const action = page.locator('#featured-action');

  await expect(page.locator('#featured')).toHaveAttribute('data-featured', 'waiting');
  await expect(page.locator('#welcome')).toHaveText('Round 7 against Bastien');
  await expect(page.locator('#featured-status')).toHaveText("Bastien's turn");
  await expect(action).toHaveText('Open the game →');
  // Round 7 is played on the breach, as the battle lays its plates.
  await expect(page.locator('#featured img.featured-ground')).toHaveAttribute('src', /home-ground-breach-/);
  await action.focus();
  const before = await action.boundingBox();

  // Bastien answers: the host says so, and the home reads its games again.
  theirs.table.seats = [{ ...battle.seats[0], turn: true }, { ...battle.seats[1], turn: false }];
  await page.evaluate(() => window.dispatchEvent(new Event('online')));
  await expect(page.locator('#featured')).toHaveAttribute('data-featured', 'turn');
  await expect(page.locator('#featured-eyebrow')).toHaveText('Your turn');
  await expect(page.locator('#featured-said')).toHaveText('Your turn');
  await expect(action).toHaveText('Play your turn →');
  await expect(action).toBeFocused();
  expect(await action.boundingBox()).toEqual(before);
  await expect(filledOnTheHome(page)).toHaveCount(1);
  await page.screenshot({ path: info.outputPath('lobby-featured-turn.png') });

  // The press is answered at once, and the battle's first frame is handed the game's line.
  await page.route('**/battle?**', route => route.fulfill({ contentType: 'text/html', body: '<title>Battle</title>' }));
  // What the button says once its press is handled, kept across the page change in the tab's storage.
  await page.evaluate(() => window.addEventListener('click', () => sessionStorage.setItem('pressed', document.getElementById('featured-action').textContent)));
  await action.click();
  await page.waitForURL(/\/battle\?player1=waits-token$/);
  expect(await page.evaluate(() => sessionStorage.getItem('pressed'))).toBe('Opening…');
  const cue = JSON.parse(await page.evaluate(() => sessionStorage.getItem('downfall.arena.arrival.v1')));
  expect({ ...cue, at: typeof cue.at }).toEqual({ table: 'waits', line: 'Round 7 against Bastien', tokens: ['waits-token'], at: 'number' });

  // Back from the battle, the page as the back-forward cache keeps it gives the action back.
  await page.goBack();
  await page.evaluate(() => window.dispatchEvent(new PageTransitionEvent('pageshow', { persisted: true })));
  await expect(page.locator('#featured-action')).toHaveText('Play your turn →');
  await expect(page.locator('#featured-action')).not.toHaveAttribute('aria-busy', 'true');
  expect(state.errors).toEqual([]);
});

test('a featured game stays featured while several turns are the player\'s, and a late picture never moves its button', async ({ page }) => {
  let release;
  const held = new Promise(resolve => { release = resolve; });
  // The static server serves the kit's files under /table/: each is answered from the folder, the ground held back.
  await page.route('**/page-art/*', async route => {
    const name = new URL(route.request().url()).pathname.split('/').pop();
    // The breach, a later round's ground, drawn once the lists are read: the eclipse before it is not held, or the
    // page would never finish loading.
    if (name.startsWith('home-ground-breach-')) await held;
    await route.fulfill({ path: fileURLToPath(new URL(`../../table/page-art/${name}`, import.meta.url)), contentType: 'image/webp' });
  });
  const game = (id, round) => ({ table: { ...battle, id, round }, seats: [{ seat: 'player1', token: `${id}-token` }] });
  const games = [game('first', 7), game('second', 8)];
  const state = await home(page, { games, tutorials: series('done', 'new') });
  const action = page.locator('#featured-action');
  await expect(action).toHaveText('Play your turn →');
  const before = await action.boundingBox();

  release();
  await expect(page.locator('#featured img.featured-ground')).toHaveAttribute('src', /home-ground-breach-/);
  await expect.poll(() => page.locator('#featured img.featured-ground').evaluate(image => image.complete && image.naturalWidth > 0)).toBe(true);
  expect(await action.boundingBox()).toEqual(before);
  // The host lists them the other way round: the one on show stays.
  await expect(action).toHaveAttribute('aria-label', 'Play your turn: round 7 against Bastien');
  games.reverse();
  const reads = state.mineReads;
  await page.evaluate(() => window.dispatchEvent(new Event('online')));
  await expect.poll(() => state.mineReads).toBeGreaterThan(reads);
  await expect(page.locator('#your-turn .game-row').first()).toContainText('Round 8');
  await expect(action).toHaveAttribute('aria-label', 'Play your turn: round 7 against Bastien');
  expect(state.errors).toEqual([]);
});

test('creation waits for the host: a first read that failed leaves Play a bot and New battle disabled until a read succeeds', async ({ page }) => {
  const state = await home(page, { games: [], tutorials: series('done', 'done'), lobbyDown: true });

  await expect(page.locator('#lobby-status')).toContainText('Reconnecting automatically');
  await expect(page.locator('#featured')).toHaveAttribute('data-featured', 'new');
  await expect(page.locator('#featured-action')).toBeDisabled();
  await expect(page.locator('#new-table')).toBeDisabled();
  state.offline = false;
  await page.evaluate(() => window.dispatchEvent(new Event('online')));
  await expect(page.locator('#featured-action')).toBeEnabled();
  await expect(page.locator('#new-table')).toBeEnabled();
  await page.locator('#featured-action').click();
  await expect(page.locator('#create-dialog')).toHaveJSProperty('open', true);
});
