import { test, expect } from '@playwright/test';
import { returningBattlePlayer, matchResult } from './battle-help-fixture.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningBattlePlayer);
const creature = (id, health) => ({ id, name: `Creature ${id}`, health, maxHealth: 30, energy: 2, totalDefense: 1,
  currentInitiative: 5, isAlive: health > 0, conditions: [], knownSpells: [], acquiredTiers: [] });
async function mount(page, { over = true, recording = true, outcome = { winner: 'player1', reason: 'Elimination' }, both = false, account = null, permission = 'default', unsupported = false,
  opponent = undefined, friends = [], rematch = { status: 201, json: { seats: [{ seat: 'player1', token: 'next' }] } } } = {}) {
  const state = { status: 200, writes: [], reads: [], errors: [], view: { over, recording, outcome,
    waitingFor: null, feed: [], feedNext: 0, board: { slot: 'Player1', roundNumber: 7, phase: 'Combat', subPhase: 'Activation',
      allies: [creature(1, 17), creature(2, 4), creature(3, 0)], enemies: [creature(4, 0), creature(5, 0), creature(6, 0)], timeline: [] } } };
  page.on('pageerror', error => state.errors.push(error.message));
  await page.addInitScript(({ both, permission, unsupported }) => {
    localStorage.setItem('downfall.table.seats', JSON.stringify(both ? { player1: 'first', player2: 'second' } : { player1: 'first' }));
    window.serviceCalls = []; let subscription = null;
    const registration = { pushManager: { getSubscription: async () => subscription, subscribe: async () => {
      window.serviceCalls.push('subscribe'); return subscription = { options: { applicationServerKey: new Uint8Array([1, 2, 3]) }, toJSON: () => ({ endpoint: 'fixture' }), unsubscribe: async () => { subscription = null; } };
    } }, showNotification: async () => window.serviceCalls.push('page-test'), getNotifications: async () => [{ tag: 'test:page' }] };
    Object.defineProperty(window, 'Notification', { configurable: true, value: { permission, requestPermission: async () => {
      window.serviceCalls.push('permission'); Notification.permission = 'granted'; return 'granted';
    } } });
    if (unsupported) delete window.PushManager;
    else {
      window.PushManager = function () {};
      Object.defineProperty(navigator, 'serviceWorker', { configurable: true, value: { register: async () => registration, ready: Promise.resolve(registration), addEventListener() {} } });
    }
    Object.defineProperty(navigator, 'vibrate', { configurable: true, writable: true, value: () => window.serviceCalls.push('vibrate') });
    const original = fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      window.notifyBattle = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  }, { both, permission, unsupported });
  await page.route('**/api/**', async route => {
    const req = route.request(), url = new URL(req.url()); state.reads.push({ path: url.pathname, query: url.search, token: req.headers()['x-seat-token'] });
    if (req.method() !== 'GET' && !url.pathname.endsWith('/resume')) state.writes.push({ path: url.pathname, method: req.method(), body: req.postDataJSON(), token: req.headers()['x-seat-token'] });
    if (url.pathname === '/api/me') return route.fulfill({ json: { accounts: 'dev', account } });
    if (url.pathname === '/api/friends') return route.fulfill({ json: { friends, incoming: [], outgoing: [] } });
    if (url.pathname === '/api/lobby/tables') return route.fulfill(rematch);
    // The table as the lobby names its seats, when the test names an opponent (`null`: a shared device).
    const named = opponent === undefined ? {} : { seats: [{ seat: 'player1', kind: 'person', name: 'Mark', account: 'acct_markmarkmarkmarkm' }, ...(opponent ? [opponent] : [{ seat: 'player2', kind: 'person', name: 'Mark' }])] };
    if (url.pathname.endsWith('/resume')) return route.fulfill({ json: { tables: [{ table: { id: 'one', format: '3v3', ...named }, seats: [{ seat: 'player1', token: 'first' }, ...(both ? [{ seat: 'player2', token: 'second' }] : [])] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [], packages: [] } });
    if (url.pathname === '/api/lobby/push') return route.fulfill({ json: { publicKey: 'AQID' } });
    if (url.pathname === '/api/lobby/push/test') return route.fulfill({ json: { message: 'Sent.' } });
    if (url.pathname === '/api/notes') return route.fulfill({ status: 204 });
    if (url.pathname === '/api/seat/player1') return route.fulfill({ status: state.status, json: state.view });
    return route.fulfill({ status: 500 });
  });
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  if (!both) { await expect(page.locator('#loading')).toBeHidden(); if (over) await closeResult(page); }
  return state;
}
// A finished match opens its result by itself once (ADR 0145); a test that wants the field closes it first.
async function closeResult(page) {
  await expect(page.locator('#match-dialog')).toBeVisible(); await page.keyboard.press('Escape');
  await expect(page.locator('#match-dialog')).toBeHidden();
}
for (const [winner, reason, title] of [['player1', 'Elimination', 'Victory'], ['player2', 'Concession', 'Defeat'], [null, 'RoundCap', 'Draw']]) {
  test(`final ${title} uses the host reason and public survivors`, async ({ page }, info) => {
    const h = await mount(page, { outcome: { winner, reason } }); await matchResult(page);
    await expect(page.locator('#match-title')).toHaveText(title); await expect(page.locator('#match-eyebrow')).toHaveText('MATCH COMPLETE · ROUND 7');
    // Each team is a card of who stands and the health it kept; its whole line is the card's accessible name.
    await expect(page.locator('#match-stats .match-team[data-side="ally"]')).toHaveAttribute('aria-label', 'Your team: 2/3 standing · 21 HP');
    await expect(page.locator('#match-stats .match-team[data-side="enemy"]')).toHaveAttribute('aria-label', 'Opponent: 0/3 standing · 0 HP');
    await expect(page.locator('#match-stats .match-team[data-side="ally"] .match-team-hp')).toHaveText('21 / 90 HP');
    // History is the field's header's; the card ends on replay and the lobby, by that one name.
    await expect(page.locator('#match-dialog [data-history-open]')).toHaveCount(0);
    await expect(page.locator('#match-lobby')).toHaveText('Lobby'); await expect(page.locator('#match-lobby')).toHaveAttribute('href', '/');
    await expect(page.locator('#match-rematch')).toBeHidden();
    await fitsViewport(page, '#match-replay, #match-lobby, #match-dialog [aria-label="Close match result"]');
    await page.screenshot({ path: info.outputPath('final-result-fixture.jpg'), type: 'jpeg', quality: 85 });
    expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
  });
}
test('empty replay keeps an explicit message on the result and a legacy outcome stays unknown', async ({ page }) => {
  await mount(page, { outcome: null }); await matchResult(page); await expect(page.locator('#match-title')).toHaveText('Match complete');
  await page.locator('#match-replay').click(); await expect(page.locator('#match-replay-error')).toContainText('Nothing was recorded');
  await expect(page.locator('#match-dialog')).toBeVisible(); await expect(page.locator('#replay-controls')).toBeHidden();
});
test('one accepted seat resumes without selection or a shown acknowledgement; switching clears it first', async ({ page }) => {
  // Record the initial HTML and every title change, including the asynchronous seat lookup.
  await page.addInitScript(() => {
    window.accessPanelShown = false;
    new MutationObserver(() => {
      const panel = document.querySelector('#loading');
      if (panel && !panel.hidden) window.accessPanelShown = true;
    }).observe(document, { subtree: true, childList: true, attributes: true, attributeFilter: ['hidden'] });
  });
  const h = await mount(page); await expect(page.locator('#seat-choices')).toBeEmpty();
  expect(await page.evaluate(() => window.accessPanelShown)).toBe(false);
  expect(h.reads.filter(one => one.path.startsWith('/api/seat/')).every(one => one.token === 'first' && !one.query.includes('shown='))).toBe(true);
});
test('two accepted seats stay behind explicit selection and suppress notification controls', async ({ page }) => {
  const h = await mount(page, { both: true }); await expect(page.locator('#seat-choices button')).toHaveCount(2);
  expect(h.reads.some(one => one.path.startsWith('/api/seat/'))).toBe(false);
  await page.locator('#seat-choices button').first().click(); await expect(page.locator('#loading')).toBeHidden(); await closeResult(page); await page.locator('#settings').click();
  await expect(page.locator('#notices-toggle')).toHaveText('No notices with both seats here'); await expect(page.locator('#notices-toggle')).toBeDisabled();
  expect(h.writes).toEqual([]);
});
test('the battle view offers no playtest notes, misplay marks or end comment', async ({ page }) => {
  await mount(page); await page.locator('#settings').click();
  await expect(page.getByRole('button', { name: /Playtest notes|misplay/i })).toHaveCount(0);
  await page.keyboard.press('Escape'); await matchResult(page);
  await expect(page.getByRole('button', { name: /End comment/i })).toHaveCount(0); await expect(page.locator('#notes-dialog')).toHaveCount(0);
});
for (const options of [{ permission: 'denied' }, { unsupported: true }]) test(`notifications explain ${JSON.stringify(options)}`, async ({ page }) => {
  const h = await mount(page, options); await page.locator('#settings').click(); await expect(page.locator('#notices-toggle')).toBeDisabled();
  await expect(page.locator('#notices-toggle')).toHaveText(options.unsupported ? 'This browser cannot notify you' : 'Notifications are blocked');
  expect(await page.evaluate(() => window.serviceCalls)).toEqual([]); expect(h.writes).toEqual([]);
});
test('notification opt-in, diagnostic and selection haptic are explicit user actions', async ({ page }) => {
  const h = await mount(page); await page.locator('#settings').click();
  expect(await page.evaluate(() => window.serviceCalls)).toEqual([]); expect(h.writes).toEqual([]);
  await page.locator('#notices-toggle').click(); await expect(page.locator('#notices-toggle')).toHaveText('Stop notifying me');
  await page.locator('#notices-test').click(); await expect(page.locator('#notices-problem')).toContainText('Two notices');
  expect(h.writes.map(one => one.path)).toEqual(['/api/lobby/push', '/api/lobby/push/test']);
  await page.locator('#haptics').check(); await page.keyboard.press('Escape'); await page.locator('#settings').click();
  expect(await page.evaluate(() => window.serviceCalls)).toContain('vibrate');
  await expect(page.locator('#sound')).not.toBeChecked(); expect(h.errors).toEqual([]);
});
test('revoked access clears the entire result', async ({ page }) => {
  const h = await mount(page); await matchResult(page);
  h.status = 403; await page.evaluate(() => window.notifyBattle()); await expect(page.locator('#match-dialog')).toBeHidden();
  await expect(page.locator('#match-stats')).toBeEmpty();
});
test('final actions fit short landscape and portrait with readable stats', async ({ page }) => {
  await mount(page); await matchResult(page);
  for (const viewport of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(viewport); await fitsViewport(page, '#match-replay, #match-dialog [aria-label="Close match result"]');
  }
});

test('a signed-in player can read a menu panel and return to the same live match in short landscape', async ({ page }, info) => {
  await page.setViewportSize({ width: 740, height: 320 });
  await mount(page, { over: false, account: { id: 'acct_one', username: 'Marc' } });
  const board = page.locator('#battle-zone');
  const before = await board.boundingBox();
  // The signed-in navigation uses the match toolbar's row, preserving the full field height.
  const withoutHeader = await page.locator('.home-header').evaluate(header => {
    header.hidden = true;
    const height = document.getElementById('battle-zone').getBoundingClientRect().height;
    header.hidden = false;
    return height;
  });
  expect(before.height).toBe(withoutHeader);
  // Signed in, Settings opens from the account menu, not the main navigation.
  await page.locator('#account-menu summary').click();
  await page.locator('#account-settings').click();
  await page.getByRole('dialog', { name: 'Settings', exact: true }).getByRole('tab', { name: 'Privacy' }).click();
  await expect(page.getByRole('heading', { name: 'What we keep' })).toBeVisible();
  await page.keyboard.press('Escape');
  expect(await board.boundingBox()).toEqual(before);
  await expect(page.locator('#connection')).toHaveText(/^LIVE/);
  await fitsViewport(page, '#home-menu-toggle, #settings, #order-open');
  await page.screenshot({ path: info.outputPath('signed-in-battle-landscape.jpg'), type: 'jpeg' });
});
test('on a phone held upright the round\'s header is the only one: the menu and the player fold into it, History is a clock', async ({ page }, info) => {
  test.skip(page.viewportSize().width >= 620, 'a phone held upright');
  await mount(page, { over: false, account: { id: 'acct_one', username: 'Marc' } });
  const [site, round] = await Promise.all(['.home-header', '#battle .hud-top'].map(one => page.locator(one).boundingBox()));
  // One row: the site's header stands inside the round's, at its left end.
  expect(site.y).toBeGreaterThanOrEqual(round.y - 1); expect(site.y + site.height).toBeLessThanOrEqual(round.y + round.height + 1);
  await expect(page.locator('.home-header .brand')).toBeHidden();
  await fitsViewport(page, '#home-menu-toggle, .home-header .account-chip, #history-open, #help-open, #settings');
  await expect(page.locator('#history-preview')).toBeHidden();
  await page.screenshot({ path: info.outputPath('one-header.png') });
  await page.locator('#home-menu-toggle').click(); await expect(page.locator('#home-navigation')).toBeInViewport();
  await page.locator('#home-menu-toggle').click();
  await page.locator('.home-header .account-chip').click(); await expect(page.locator('.home-header .account-menu')).toBeInViewport();
});

test('Rematch against the bot opens the same game at once, on a new table', async ({ page }, info) => {
  const h = await mount(page, { opponent: { seat: 'player2', kind: 'bot', name: 'Lookahead' } }); await matchResult(page);
  await expect(page.locator('#match-rematch')).toHaveText('Rematch');
  await fitsViewport(page, '#match-rematch, #match-replay, #match-lobby');
  await page.screenshot({ path: info.outputPath('final-result-rematch.jpg'), type: 'jpeg', quality: 85 });
  await page.locator('#match-rematch').click();
  await page.waitForURL(/\/battle\?player1=next$/);
  expect(h.writes).toEqual([{ path: '/api/lobby/tables', method: 'POST', body: { opponent: 'bot', format: '3v3' }, token: undefined }]);
});
test('a rematch the host refuses stays on the card in its own words', async ({ page }) => {
  const h = await mount(page, { opponent: { seat: 'player2', kind: 'bot', name: 'Lookahead' },
    rematch: { status: 409, json: { message: 'You already have five games against bots in progress.' } } });
  await matchResult(page); await page.locator('#match-rematch').click();
  await expect(page.locator('#match-replay-error')).toHaveText('You already have five games against bots in progress.');
  await expect(page.locator('#match-rematch')).toBeEnabled(); await expect(page.locator('#match-dialog')).toBeVisible(); expect(h.errors).toEqual([]);
});
test('a friend is invited to the rematch at once; anyone else opens the lobby on them', async ({ page }) => {
  const friend = { seat: 'player2', kind: 'person', name: 'Bastien', account: 'acct_bastienbastienba' };
  let h = await mount(page, { opponent: friend, friends: [{ id: friend.account, username: 'Bastien' }] }); await matchResult(page);
  await expect(page.locator('#match-rematch')).toHaveText('Invite to rematch'); await page.locator('#match-rematch').click();
  await page.waitForURL(/\/battle\?player1=next$/);
  expect(h.writes).toEqual([{ path: '/api/lobby/tables', method: 'POST', body: { opponent: 'friend', friend: friend.account, format: '3v3' }, token: undefined }]);
  await page.unrouteAll({ behavior: 'ignoreErrors' });
  h = await mount(page, { opponent: friend }); await matchResult(page);
  await expect(page.locator('#match-rematch')).toHaveText('Invite to rematch'); await page.locator('#match-rematch').click();
  await page.waitForURL(/\/lobby\?rematch=acct_bastienbastienba&format=3v3$/); expect(h.writes).toEqual([]);
});
