import { test, expect } from '@playwright/test';
import { returningBattlePlayer } from './battle-help-fixture.js';

// Report or suggest (ADR 0189, feedback plan R2): one entry, a bug report or an idea, sent to POST /api/feedback with
// the seat on screen when there is one. The host adds the table, the round and the position; the page its words,
// its path and its last errors.
const player = { id: 'acct_aaaaaaaaaaaaaaaa', username: 'MarcF' };
const creature = id => ({ id, name: `Creature ${id}`, health: 30, maxHealth: 30, energy: 2, totalDefense: 1,
  currentInitiative: 5, isAlive: true, conditions: [], knownSpells: [], acquiredTiers: [] });

async function answering(state, route) {
  const request = route.request();
  // Asked before the player writes whether they are muted (#717): 204 unless the test mutes them.
  // A test may hold a seat's question (keyed by its token, or 'none') and say what it answers, muted or not (#824).
  if (request.method() === 'GET') {
    const token = request.headers()['x-seat-token'] ?? null;
    state.asked.push(token);
    const key = token ?? 'none';
    if (state.askHolds?.[key]) await state.askHolds[key];
    const muted = state.askMuted?.[key] ?? state.muted;
    await (muted ? route.fulfill({ status: 403, json: { error: 'Feedback.Muted' } }) : route.fulfill({ status: 204, body: '' }));
    state.answered?.push(token);
    return;
  }
  state.sent.push({ body: request.postDataJSON(), token: request.headers()['x-seat-token'] ?? null });
  if (state.hold) await state.hold;
  const answer = state.answers.shift() ?? { status: 201, json: { id: 'fb_1', merged: false } };
  return route.fulfill(answer);
}

async function seat(page, { account = null, muted = false } = {}) {
  return seatWith(page, { muted }, { account });
}

/** A seat in a game, its host answering into `held`: what a test set on it before the page loads holds from the first question. */
async function seatWith(page, held, { account = null } = {}) {
  const state = Object.assign(held, { sent: [], answers: [], errors: [], asked: [] });
  page.on('pageerror', error => state.errors.push(error.message));
  await returningBattlePlayer({ context: page.context() });
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'first' }));
    const original = fetch;
    window.fetch = (url, options) => String(url).endsWith('/events')
      ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
        // A test sends the seat a board when it chooses: the page reads its view again, as on a host's notice.
        window.notifyBattle = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
      } }), { headers: { 'Content-Type': 'text/event-stream' } }))
      : original(url, options);
  });
  const view = { over: false, recording: true, waitingFor: null, feed: [], feedNext: 0,
    board: { slot: 'Player1', roundNumber: 3, phase: 'Planning', subPhase: 'Speed', allies: [creature(1), creature(2), creature(3)], enemies: [creature(4), creature(5), creature(6)], timeline: [] } };
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/feedback') return answering(state, route);
    if (path === '/api/me') return route.fulfill({ json: { accounts: 'dev', account } });
    if (path === '/api/friends') return route.fulfill({ json: { friends: [], incoming: [], outgoing: [] } });
    if (path.endsWith('/resume')) return route.fulfill({ json: { tables: [{ table: { id: 'one', format: '3v3',
      seats: [{ seat: 'player1', kind: 'person', name: 'Mark' }, { seat: 'player2', kind: 'person', name: 'Bastien' }] },
      seats: [{ seat: 'player1', token: 'first' }] }] } });
    if (path === '/api/catalogue') return route.fulfill({ json: { cards: [], packages: [] } });
    if (path === '/api/seat/player1') return route.fulfill({ json: view });
    return route.fulfill({ status: 404, json: {} });
  });
  await page.goto('/table/battle.html');
  await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#loading')).toBeHidden();
  return state;
}

async function lobby(page, { account = player, muted = false } = {}) {
  const state = { sent: [], answers: [], errors: [], asked: [], muted };
  page.on('pageerror', error => state.errors.push(error.message));
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/feedback') return answering(state, route);
    if (path === '/api/me') return route.fulfill({ json: { accounts: 'dev', account, googleClientId: 'test-client' } });
    if (path === '/api/lobby') return route.fulfill({ json: { recording: true, format: '3v3', formats: ['3v3'], ongoing: 0, open: 0 } });
    if (['/api/lobby/adopt', '/api/lobby/mine', '/api/lobby/resume'].includes(path)) return route.fulfill({ json: { tables: [], invitations: [] } });
    if (path === '/api/friends') return route.fulfill({ json: { friends: [], incoming: [], outgoing: [] } });
    return route.fulfill({ status: 404, json: { message: `Unexpected ${path}` } });
  });
  await page.goto('/table/lobby.html');
  await expect(page.locator('body')).toHaveAttribute('data-home', account ? 'member' : 'guest');
  return state;
}

// The sheet stands inside the viewport, its body scrolling rather than the page, and every control it shows is a
// 44 px target no wider than the screen.
async function sheetFits(page) {
  await expect.poll(() => page.evaluate(({ width, height }) => {
    const wrong = [];
    const sheet = document.getElementById('feedback-dialog').getBoundingClientRect();
    if (sheet.left < 0 || sheet.top < 0 || sheet.right > width || sheet.bottom > height) wrong.push(`sheet ${Math.round(sheet.width)}x${Math.round(sheet.height)}`);
    for (const node of document.querySelectorAll('#feedback-dialog :is(button, .feedback-choice, .feedback-attach)')) {
      const box = node.getBoundingClientRect();
      if (box.width === 0 || box.height === 0) continue;
      if (box.height < 44 || box.width < 44 || box.left < sheet.left || box.right > sheet.right) wrong.push(`${node.id || node.textContent.trim().slice(0, 20)} ${Math.round(box.width)}x${Math.round(box.height)}`);
    }
    return wrong;
  }, page.viewportSize())).toEqual([]);
}

async function openAccountMenu(page) {
  await page.locator('#account-menu summary').click();
}

test('a seat reports a bug from the match settings, with the position and the page’s last error', async ({ page }, info) => {
  const state = await seat(page);
  await page.evaluate(() => setTimeout(() => { throw new Error('The scene lost its board'); }));

  await page.locator('#settings').click();
  await page.locator('#feedback-open-settings').scrollIntoViewIfNeeded();
  await page.screenshot({ path: info.outputPath('feedback-settings.png') });
  await page.getByRole('button', { name: 'Report or suggest…' }).click();
  const sheet = page.locator('#feedback-dialog');
  await expect(sheet).toBeVisible();
  await page.getByRole('button', { name: /Something is wrong/ }).click();
  await page.getByRole('radio', { name: 'The rules did something wrong' }).check();
  await page.getByLabel(/What happened/).fill('Too short');
  await expect(page.locator('#feedback-send')).toBeDisabled();
  await page.getByLabel(/What happened/).fill('The bleed did not tick after the stun ended.');
  await expect(page.locator('#feedback-attach')).toBeChecked();
  await expect(page.locator('#feedback-note')).toContainText('this position');
  await sheetFits(page);
  await page.screenshot({ path: info.outputPath('feedback-bug.png') });
  await page.locator('#feedback-send').click();

  await expect(page.locator('#feedback-sent')).toHaveText('Thank you. Your report reached the game’s operator.');
  expect(state.sent).toHaveLength(1);
  const [{ body, token }] = state.sent;
  expect(token).toBe('first');
  expect(body).toMatchObject({ kind: 'bug', category: 'rules', happened: 'The bleed did not tick after the stun ended.', attach: true, page: '/table/battle.html' });
  expect(body.errors).toEqual([expect.objectContaining({ message: expect.stringContaining('The scene lost its board') })]);
  await page.locator('#feedback-finish').click();
  await expect(sheet).toBeHidden();
  expect(state.errors).toEqual(['The scene lost its board']);
});

test('in a game, the account menu’s entry opens the battle’s own sheet, sent with the seat on screen', async ({ page }) => {
  const state = await seat(page, { account: player });

  await page.locator('#account-menu summary').click();
  await page.locator('#feedback-open-nav').click();
  await page.getByRole('button', { name: /Something is wrong/ }).click();
  await page.getByRole('radio', { name: 'Something looks wrong' }).check();
  await page.getByLabel(/What happened/).fill('The crest overlaps my first creature.');
  await page.locator('#feedback-send').click();

  await expect(page.locator('#feedback-sent')).toBeVisible();
  await expect(page.locator('#feedback-dialog')).toHaveCount(1);
  expect(state.sent).toEqual([expect.objectContaining({ token: 'first', body: expect.objectContaining({ kind: 'bug', category: 'looks', attach: true }) })]);
});

test('the result offers the entry on a quiet line of its own', async ({ page }, info) => {
  await seat(page);

  const entry = page.locator('#feedback-open-match');
  // The board arriving late may draw over a result opened by hand: it is opened again until it stays, and read
  // and pressed in the same attempt, so that no read falls between the check and the click (#1141). The first
  // attempt meets a late board after its check, so every run takes that path.
  let late = 'to send';
  await expect(async () => {
    await page.evaluate(() => { const result = document.getElementById('match-dialog'); if (!result.open) result.showModal(); });
    await expect(entry).toBeVisible({ timeout: 1000 });
    if (late === 'to send') {
      late = 'sent';
      await page.evaluate(() => window.notifyBattle());
      await expect(page.locator('#match-dialog')).not.toHaveAttribute('open');
      late = 'met';
    }
    const [box, replay] = await Promise.all([entry.boundingBox(), page.locator('#match-replay').boundingBox()]);
    expect(box && replay, 'the entry and Replay are drawn').toBeTruthy();
    expect(box.height).toBeGreaterThanOrEqual(44);
    expect(box.y).toBeGreaterThan(replay.y);
    await page.screenshot({ path: info.outputPath('feedback-result.png') });
    await entry.click({ timeout: 1000 });
  }).toPass();
  expect(late, 'the late board closed the result once').toBe('met');
  await expect(page.locator('#feedback-dialog')).toBeVisible();
});

test('a refusal keeps the words, and a muted player loses the entry', async ({ page }) => {
  const state = await seat(page);
  state.answers.push({ status: 429, json: { error: 'Feedback.TooSoon', retryAfterSeconds: 42 } }, { status: 403, json: { error: 'Feedback.Muted' } });

  await page.locator('#settings').click();
  await page.locator('#feedback-open-settings').click();
  await page.getByRole('button', { name: /I have an idea/ }).click();
  await page.getByRole('radio', { name: 'The battle' }).check();
  await page.getByLabel(/What were you trying to do/).fill('I wanted to see my last turn again.');
  await page.locator('#feedback-send').click();
  await expect(page.locator('#feedback-refusal')).toHaveText('You sent feedback a moment ago. Try again in 42 seconds: your words are kept here.');
  await expect(page.getByLabel(/What were you trying to do/)).toHaveValue('I wanted to see my last turn again.');
  await page.locator('#feedback-send').click();

  await expect(page.locator('#feedback-refusal')).toHaveText('Your feedback is paused for now.');
  await page.locator('#feedback-close').click();
  await expect(page.locator('#feedback-open-settings')).toBeHidden();
  await expect(page.locator('#settings-dialog .settings-feedback')).toBeHidden();
  // The result's entry is hidden by the same rule, whether or not the result is open.
  expect(await page.locator('#feedback-open-match').evaluate(node => getComputedStyle(node).display)).toBe('none');
  expect(state.sent.map(one => one.body.kind)).toEqual(['idea', 'idea']);
});

test('a player the host says is muted is offered no entry in a game before they write (#717)', async ({ page }) => {
  const state = await seat(page, { muted: true });

  await expect.poll(() => state.asked).toContain('first');
  await page.locator('#settings').click();

  await expect(page.locator('#settings-dialog')).toBeVisible();
  await expect(page.locator('#feedback-open-settings')).toBeHidden();
  await expect(page.locator('#settings-dialog .settings-feedback')).toBeHidden();
  expect(await page.locator('#feedback-open-match').evaluate(node => getComputedStyle(node).display)).toBe('none');
  expect(state.sent).toEqual([]);
});

/** Holds the questions the page asks for these tokens ('none' for the one asked without a seat) until released. */
function holdAsking(state, keys) {
  const releases = {};
  state.askHolds = Object.fromEntries(keys.map(key => [key, new Promise(resolve => { releases[key] = resolve; })]));
  state.answered = [];
  return releases;
}

/** Lets the page read the answer it was just given: the question's handler runs once its response has arrived. */
async function settled(page) {
  await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => setTimeout(resolve, 100))));
}

test('an answer to an earlier question, about a seat no longer on screen, is not read over the latest one (#824)', async ({ page }) => {
  // The page asks once mounted, without a seat, then again for the seat it shows; the first answer, muted, comes last.
  const state = { muted: false };
  const releases = holdAsking(state, ['none']);
  state.askMuted = { none: true, first: false };
  await seatWith(page, state);
  await expect.poll(() => state.answered).toEqual(['first']);
  await settled(page);

  expect(state.asked).toContain(null);
  releases.none();
  // Each question asked without a seat is answered muted, and only after the seat's own question.
  await expect.poll(() => state.answered.length).toBe(state.asked.length);
  expect(state.answered[0]).toBe('first');
  await settled(page);

  await expect(page.locator('html')).not.toHaveAttribute('data-feedback-muted', 'true');
  await page.locator('#settings').click();
  await expect(page.locator('#feedback-open-settings')).toBeVisible();
});

test('a send refused as muted is not undone by a question still on its way (#824)', async ({ page }) => {
  const state = { muted: false };
  const releases = holdAsking(state, ['first']);
  await seatWith(page, state);
  await expect.poll(() => state.asked).toContain('first');
  state.answers.push({ status: 403, json: { error: 'Feedback.Muted' } });

  await page.locator('#settings').click();
  await page.locator('#feedback-open-settings').click();
  await page.getByRole('button', { name: /I have an idea/ }).click();
  await page.getByRole('radio', { name: 'The battle' }).check();
  await page.getByLabel(/What were you trying to do/).fill('I wanted to see my last turn again.');
  await page.locator('#feedback-send').click();
  await expect(page.locator('#feedback-refusal')).toHaveText('Your feedback is paused for now.');

  releases.first();
  await expect.poll(() => state.answered).toContain('first');
  await settled(page);

  await expect(page.locator('html')).toHaveAttribute('data-feedback-muted', 'true');
  await page.locator('#feedback-close').click();
  await expect(page.locator('#feedback-open-settings')).toBeHidden();
});

test('a signed-in player the host says is muted finds no entry in the lobby’s account menu (#717)', async ({ page }) => {
  const state = await lobby(page, { muted: true });

  await expect.poll(() => state.asked.length).toBeGreaterThan(0);
  await page.locator('#account-menu summary').click();

  await expect(page.locator('#account-menu [data-feedback-open]')).toBeHidden();
  expect(state.asked).toEqual([null]);
});

test('a sheet closed while it sends is empty when it opens again, once the host kept it', async ({ page }) => {
  const state = await seat(page);
  let release;
  state.hold = new Promise(resolve => { release = resolve; });
  await page.locator('#settings').click();
  await page.locator('#feedback-open-settings').click();
  await page.getByRole('button', { name: /Something is wrong/ }).click();
  await page.getByRole('radio', { name: 'It is stuck or slow' }).check();
  await page.getByLabel(/What happened/).fill('The board froze after my tempo.');
  await page.locator('#feedback-send').click();
  await expect.poll(() => state.sent.length).toBe(1);
  // While it is sent, the draft cannot change under the words already on their way.
  await expect(page.getByLabel(/What happened/)).toBeDisabled();
  await expect(page.locator('#feedback-back')).toBeDisabled();

  await page.locator('#feedback-close').click();
  release();
  await expect.poll(() => page.evaluate(() => document.getElementById('feedback-happened').value)).toBe('');
  await page.locator('#feedback-open-settings').click();

  await expect(page.locator('#feedback-kinds')).toBeVisible();
  await expect(page.locator('#feedback-sent')).toBeHidden();
  await page.getByRole('button', { name: /Something is wrong/ }).click();
  await expect(page.getByLabel(/What happened/)).toHaveValue('');
});

test('a signed-in player sends an idea from the lobby’s account menu, without a seat or a position', async ({ page }, info) => {
  const state = await lobby(page);

  await openAccountMenu(page);
  await page.getByRole('button', { name: 'Report or suggest…' }).click();
  await expect(page.locator('#account-menu')).not.toHaveAttribute('open');
  await page.getByRole('button', { name: /I have an idea/ }).click();
  await page.getByRole('radio', { name: 'The lobby and friends' }).check();
  await page.getByLabel(/What were you trying to do/).fill('I wanted to find a friend’s game by name.');
  await page.getByLabel(/How you would like it/).fill('A search box above Your games.');
  await expect(page.locator('#feedback-attach-row')).toBeHidden();
  await expect(page.locator('#feedback-note')).toHaveText('We add the page you send it from.');
  await sheetFits(page);
  await page.screenshot({ path: info.outputPath('feedback-idea.png') });
  await page.locator('#feedback-send').click();

  await expect(page.locator('#feedback-sent')).toHaveText('Thank you. Your idea reached the game’s operator.');
  expect(state.sent).toEqual([{ token: null, body: { kind: 'idea', where: 'lobby', problem: 'I wanted to find a friend’s game by name.', wish: 'A search box above Your games.', page: '/table/lobby.html' } }]);
  await page.locator('#feedback-finish').click();
  await expect(page.locator('#account-menu summary')).toBeFocused();
  expect(state.errors).toEqual([]);
});

test('a guest in the lobby is not offered the entry: it has no seat to send from', async ({ page }) => {
  const state = await lobby(page, { account: null });

  await expect(page.locator('#feedback-open-nav')).toBeHidden();
  expect(state.errors).toEqual([]);
});

test('Settings, Privacy says what feedback keeps', async ({ page }) => {
  await lobby(page);

  await page.locator('#account-menu summary').click();
  await page.locator('#account-settings').click();
  await page.locator('#app-settings-tab-privacy').click();

  await expect(page.locator('#privacy-panel')).toContainText('What you send with Report or suggest');
});
