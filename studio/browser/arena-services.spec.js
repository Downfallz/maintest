import { test, expect } from '@playwright/test';
import { returningArenaPlayer } from './arena-help-fixture.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningArenaPlayer);
const creature = (id, health) => ({ id, name: `Creature ${id}`, health, maxHealth: 30, energy: 2, totalDefense: 1,
  currentInitiative: 5, isAlive: health > 0, conditions: [], knownSpells: [], acquiredTiers: [] });
async function mount(page, { over = true, recording = true, outcome = { winner: 'player1', reason: 'Elimination' }, both = false, account = null, permission = 'default', unsupported = false } = {}) {
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
      window.notifyArena = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  }, { both, permission, unsupported });
  await page.route('**/api/**', async route => {
    const req = route.request(), url = new URL(req.url()); state.reads.push({ path: url.pathname, query: url.search, token: req.headers()['x-seat-token'] });
    if (req.method() !== 'GET' && !url.pathname.endsWith('/resume')) state.writes.push({ path: url.pathname, method: req.method(), body: req.postDataJSON(), token: req.headers()['x-seat-token'] });
    if (url.pathname === '/api/me') return route.fulfill({ json: { accounts: 'dev', account } });
    if (url.pathname === '/api/friends') return route.fulfill({ json: { friends: [], incoming: [], outgoing: [] } });
    if (url.pathname.endsWith('/resume')) return route.fulfill({ json: { tables: [{ table: { id: 'one', format: '3v3' }, seats: [{ seat: 'player1', token: 'first' }, ...(both ? [{ seat: 'player2', token: 'second' }] : [])] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [], packages: [] } });
    if (url.pathname === '/api/lobby/push') return route.fulfill({ json: { publicKey: 'AQID' } });
    if (url.pathname === '/api/lobby/push/test') return route.fulfill({ json: { message: 'Sent.' } });
    if (url.pathname === '/api/notes') return route.fulfill({ status: 204 });
    if (url.pathname === '/api/seat/player1') return route.fulfill({ status: state.status, json: state.view });
    return route.fulfill({ status: 500 });
  });
  await page.goto('/table/arena.html'); await expect(page.locator('#arena')).toHaveAttribute('data-ready', 'true');
  if (!both) await expect(page.locator('#loading')).toBeHidden();
  return state;
}
for (const [winner, reason, title] of [['player1', 'Elimination', 'Victory'], ['player2', 'Concession', 'Defeat'], [null, 'RoundCap', 'Draw']]) {
  test(`final ${title} uses the host reason and public survivors`, async ({ page }, info) => {
    const h = await mount(page, { outcome: { winner, reason } }); await page.locator('#match-open').click();
    await expect(page.locator('#match-title')).toHaveText(title); await expect(page.locator('#match-stats')).toContainText('2/3 standing · 21 HP');
    await expect(page.locator('#match-stats')).toContainText('0/3 standing · 0 HP');
    await fitsViewport(page, '#match-replay, #match-dialog [aria-label="Close match result"]');
    await page.screenshot({ path: info.outputPath('final-result-fixture.jpg'), type: 'jpeg', quality: 85 });
    await page.locator('#match-dialog [data-history-open]').click(); await expect(page.locator('#history-dialog')).toBeVisible();
    expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
  });
}
test('empty replay keeps an explicit message on the result and a legacy outcome stays unknown', async ({ page }) => {
  await mount(page, { outcome: null }); await page.locator('#match-open').click(); await expect(page.locator('#match-title')).toHaveText('Match complete');
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
  await page.locator('#switch-seat').click(); await expect(page.locator('.creature')).toHaveCount(0); await expect(page.locator('#seat-choices button')).toHaveCount(1);
  await expect(page.locator('#load-title')).toHaveText('Choose your viewpoint');
  await expect(page.locator('#noted')).toBeEmpty(); await expect(page.locator('#match-stats')).toBeEmpty();
});
test('two accepted seats stay behind explicit selection and suppress notification controls', async ({ page }) => {
  const h = await mount(page, { both: true }); await expect(page.locator('#seat-choices button')).toHaveCount(2);
  expect(h.reads.some(one => one.path.startsWith('/api/seat/'))).toBe(false);
  await page.locator('#seat-choices button').first().click(); await expect(page.locator('#loading')).toBeHidden(); await page.locator('#settings').click();
  await expect(page.locator('#notices-toggle')).toHaveText('No notices with both seats here'); await expect(page.locator('#notices-toggle')).toBeDisabled();
  expect(h.writes).toEqual([]);
});
test('recorded notes and end comment use the seat note route without decisions', async ({ page }) => {
  const h = await mount(page, { over: false }); await page.locator('#settings').click(); await page.locator('#playtest-open').click();
  await expect(page.locator('#comment-form')).toBeHidden(); await page.getByRole('button', { name: 'I had to look this up', exact: true }).click();
  await expect(page.locator('#noted')).toHaveText('Noted: you looked a rule up.');
  await page.getByRole('button', { name: 'That was a misplay', exact: true }).click(); await expect(page.locator('#noted')).toHaveText('Noted: a misplay.');
  h.view.over = true; await page.evaluate(() => window.notifyArena()); await expect(page.locator('#comment-form')).toBeVisible();
  await page.locator('#end-comment').fill('The final round was close.'); await page.locator('#comment-save').click();
  await expect(page.locator('#noted')).toHaveText('Comment saved with the session.'); await expect(page.locator('#end-comment')).toBeEmpty();
  expect(h.writes.map(one => [one.path, one.token, one.body.kind])).toEqual([['/api/notes', 'first', 'Lookup'], ['/api/notes', 'first', 'Misplay'], ['/api/notes', 'first', 'Comment']]);
  expect(h.reads.some(one => one.query.includes('shown='))).toBe(false); expect(h.errors).toEqual([]);
});
test('unrecorded sessions hide every note entry point', async ({ page }) => {
  await mount(page, { recording: false }); await page.locator('#settings').click(); await expect(page.locator('#playtest-open')).toBeHidden();
  await page.keyboard.press('Escape'); await page.locator('#match-open').click(); await expect(page.locator('#match-notes')).toBeHidden();
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
test('an end comment being written survives leaving the page and coming back to the same seat', async ({ page }) => {
  const h = await mount(page); await page.locator('#match-open').click(); await page.locator('#match-notes').click();
  await page.locator('#end-comment').fill('Half a thought about round seven');
  const visibility = hidden => page.evaluate(value => { Object.defineProperty(document, 'hidden', { configurable: true, value }); document.dispatchEvent(new Event('visibilitychange')); }, hidden);
  await visibility(true); await visibility(false);
  await expect(page.locator('#comment-form')).toBeVisible(); await expect(page.locator('#notes-dialog')).toBeVisible();
  await expect(page.locator('#end-comment')).toHaveValue('Half a thought about round seven');
  expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
test('revoked access clears an unsaved note and the entire result', async ({ page }) => {
  const h = await mount(page); await page.locator('#match-open').click(); await page.locator('#match-notes').click(); await page.locator('#end-comment').fill('Private draft');
  h.status = 403; await page.evaluate(() => window.notifyArena()); await expect(page.locator('#notes-dialog')).toBeHidden(); await expect(page.locator('#match-dialog')).toBeHidden();
  await expect(page.locator('#end-comment')).toBeEmpty(); await expect(page.locator('#match-stats')).toBeEmpty(); await expect(page.locator('#noted')).toBeEmpty();
});
test('final actions fit short landscape and portrait with readable stats', async ({ page }) => {
  await mount(page); await page.locator('#match-open').click();
  for (const viewport of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(viewport); await fitsViewport(page, '#match-replay, #match-dialog [aria-label="Close match result"], #match-notes');
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
  await page.getByRole('button', { name: 'Open navigation' }).click();
  await page.getByRole('button', { name: 'Your data', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'What we keep' })).toBeVisible();
  await page.keyboard.press('Escape');
  expect(await board.boundingBox()).toEqual(before);
  await expect(page.locator('#connection')).toHaveText('LIVE');
  await fitsViewport(page, '#home-menu-toggle, #settings, #order-open');
  await page.screenshot({ path: info.outputPath('signed-in-arena-landscape.jpg'), type: 'jpeg' });
});
