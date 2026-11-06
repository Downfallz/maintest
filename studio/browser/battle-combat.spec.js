import { test, expect } from '@playwright/test';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningBattlePlayer);
// Match the lab/live suites: software-rendered Phaser pages run one case at a time within this file.
test.describe.configure({ mode: 'default' });
const legal = (candidates, minTargets = 1, maxTargets = 1) => ({ candidates, minTargets, maxTargets });
const creature = id => ({ id, name: `Creature ${id}`, health: 21, maxHealth: 30, energy: 5, totalDefense: 2,
  currentInitiative: 7, isAlive: true, isStunned: false, isStunImmune: false, knownSpells: ['bolt', 'ward', 'locked'], conditions: [] });
async function mount(page, { kind = 'Action', targets = legal([4, 5, 6]) } = {}) {
  const state = { writes: [], shown: [], status: 204, seatStatus: 200, shownStatus: 200, message: '', wait: null, view: {
    waitingFor: kind, waitingAsked: 11, waitingCreature: 1, feedNext: 83, over: false,
    board: { slot: 'Player1', roundNumber: 4, phase: 'Combat', subPhase: kind === 'Intent' ? 'Intent' : 'Activation', activationCursor: 0,
      allies: [1, 2, 3].map(creature), enemies: [4, 5, 6].map(creature), timeline: [{ creature: 1, owner: 'Player1', speed: 'Quick' }] },
    options: { action: { actor: 1, spells: [{ spell: 'bolt', legalTargets: targets }, { spell: 'ward', legalTargets: legal([1, 2, 3]) }] },
      intent: { creatures: [{ creature: 1, castableSpells: ['ward'], unaffordableSpells: ['bolt'] }] }, target: { spell: 'bolt', legalTargets: targets } } } };
  await page.addInitScript(() => localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'first' })));
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url());
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'table-a', format: '3v3' }, seats: [{ seat: 'player1', token: 'first' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: ['bolt', 'ward', 'locked'].map(id => ({ id, name: `Host ${id}`, cost: 0, targeting: 'Host target text', effects: ['Host effect text'] })) } });
    if (url.pathname.endsWith('/events')) return route.fulfill({ contentType: 'text/event-stream', body: '' });
    if (url.pathname.endsWith('/decision')) {
      state.writes.push(request.postDataJSON()); if (state.wait) await state.wait;
      if (state.status === 204) { state.view.waitingFor = null; state.view.waitingAsked = null; state.view.board.enemies[0].health = 12; return route.fulfill({ status: 204 }); }
      // A refusal is the host's: the asking it refused has moved on, and the next read says so.
      state.view.waitingAsked += 1;
      return route.fulfill({ status: state.status, json: { message: state.message } });
    }
    if (url.pathname === '/api/seat/player1') {
      if (url.searchParams.has('shown')) {
        state.shown.push(Number(url.searchParams.get('shown')));
        if (state.shownStatus !== 200) return route.fulfill({ status: state.shownStatus, json: {} });
      }
      return route.fulfill({ status: state.seatStatus, json: state.view });
    }
    return route.fulfill({ status: 500, body: 'Unexpected request' });
  });
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#loading')).toBeHidden(); return state;
}
async function bolt(page) { await page.locator('#combat-open').click(); await page.locator('[data-spell="bolt"]').click(); }

test('opening combat shows host card text and only then acknowledges the question', async ({ page }) => {
  const state = await mount(page); expect(state.shown).toEqual([]); await page.locator('#combat-open').click();
  await expect(page.locator('[data-spell="bolt"]')).toContainText('Host effect text'); await expect(page.locator('[data-spell="bolt"]')).toContainText('0 energy');
  await expect(page.locator('[data-spell="locked"]')).toBeDisabled(); await expect.poll(() => state.shown).toEqual([11]); expect(state.writes).toEqual([]);
});
test('target taps select and deselect; only explicit confirmation posts one complete action', async ({ page }) => {
  const state = await mount(page); await bolt(page); await expect(page.locator('[data-creature="1"]')).toBeDisabled();
  const target = page.locator('[data-creature="4"]'); await target.click(); await expect(target).toHaveAttribute('aria-pressed', 'true');
  await target.click(); await expect(page.locator('#combat-confirm')).toBeDisabled(); expect(state.writes).toEqual([]);
  await target.click(); await page.locator('#combat-confirm').click();
  await expect.poll(() => state.writes).toEqual([{ kind: 'Action', creature: 1, spell: 'bolt', targets: [4], asked: 11 }]);
  await expect(page.locator('[data-creature="4"] .creature-stats')).toContainText('12/30'); await expect(page.locator('#combat-open')).toBeHidden();
});
test('changing spell cancels the previous target choice without a write', async ({ page }) => {
  const state = await mount(page); await bolt(page); await page.locator('[data-creature="4"]').click(); await page.locator('#combat-back').click();
  await page.locator('[data-spell="ward"]').click(); await expect(page.locator('[data-creature="4"]')).toBeDisabled();
  await expect(page.locator('#combat-confirm')).toBeDisabled(); await page.locator('[data-creature="1"]').click();
  await page.locator('#combat-cancel').click(); expect(state.writes).toEqual([]); await expect(page.locator('#combat-open')).toBeVisible();
});
test('multiple targets remain separately selected and require the host count', async ({ page }) => {
  const state = await mount(page, { targets: legal([2, 4, 5], 2, 2) }); await bolt(page);
  await page.locator('[data-creature="2"]').click(); await expect(page.locator('#combat-confirm')).toBeDisabled();
  await page.locator('[data-creature="5"]').click(); await expect(page.locator('.creature[aria-pressed="true"]')).toHaveCount(2);
  await page.locator('#combat-confirm').click(); await expect.poll(() => state.writes[0]?.targets).toEqual([2, 5]);
});
test('a targetless spell still requires explicit confirmation', async ({ page }) => {
  const state = await mount(page, { targets: legal([], 0, 0) }); await bolt(page); expect(state.writes).toEqual([]);
  await expect(page.locator('#combat-confirm')).toBeEnabled(); await page.locator('#combat-confirm').click(); await expect.poll(() => state.writes[0]?.targets).toEqual([]);
});
test('hidden intent warns of unaffordable plans and declares without binding targets', async ({ page }) => {
  const state = await mount(page, { kind: 'Intent' }); await bolt(page);
  await expect(page.locator('[data-spell="bolt"]')).toContainText('may fizzle'); await expect(page.locator('#combat-dialog')).toBeVisible();
  await page.locator('#combat-declare').click(); await expect.poll(() => state.writes).toEqual([{ kind: 'Intent', creature: 1, spell: 'bolt', asked: 11 }]);
});
test('a revealed intent asks only for targets', async ({ page }) => {
  const state = await mount(page, { kind: 'Target' }); await page.locator('#combat-open').click(); await expect(page.locator('#combat-back')).toBeHidden();
  await page.locator('[data-creature="4"]').click(); await page.locator('#combat-confirm').click(); await expect.poll(() => state.writes).toEqual([{ kind: 'Target', targets: [4], asked: 11 }]);
});
test('a pending command disables confirmation and target changes', async ({ page }) => {
  const state = await mount(page); let release; state.wait = new Promise(resolve => { release = resolve; });
  await bolt(page); await page.locator('[data-creature="4"]').click(); await page.locator('#combat-confirm').click();
  await expect(page.locator('#combat-confirm')).toBeDisabled(); await expect(page.locator('[data-creature="5"]')).toBeDisabled();
  await expect.poll(() => state.writes.length).toBe(1); release(); await expect(page.locator('#combat-targets')).toBeHidden();
});
test('host rejection stays readable after the draft closes and the next asking arrives', async ({ page }) => {
  const state = await mount(page); state.status = 409; state.message = 'This question has already ended.';
  await bolt(page); await page.locator('[data-creature="4"]').click(); await page.locator('#combat-confirm').click();
  await expect(page.locator('#combat-error')).toHaveText(state.message); await expect(page.locator('#combat-targets')).toBeHidden(); expect(state.writes).toHaveLength(1);
  // The refresh the write triggered has delivered the new asking by now: the message is still there to read.
  await expect(page.locator('#combat-open')).toBeVisible({ timeout: 10000 });
  await expect(page.locator('#combat-error')).toHaveText(state.message);
  await page.locator('#combat-open').click();
  await expect(page.locator('#combat-error')).toBeHidden();
});
test('keyboard users can leave the spell dialog and confirm a field target', async ({ page }) => {
  const state = await mount(page); await page.locator('#combat-open').focus(); await page.keyboard.press('Enter');
  await page.locator('[data-spell="bolt"]').focus(); await page.keyboard.press('Enter');
  await expect(page.locator('[data-creature="4"]')).toBeFocused(); await page.keyboard.press('Enter');
  await page.locator('#combat-confirm').focus(); await page.keyboard.press('Enter'); await expect.poll(() => state.writes.length).toBe(1);
});
// The test sets its own viewports: one project runs it, three would run it three times.
test('combat controls and all field targets fit short portrait and landscape', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'The viewports are the test\'s own');
  await mount(page); await bolt(page);
  for (const viewport of [{ width: 320, height: 568 }, { width: 844, height: 390 }, { width: 1440, height: 1000 }]) {
    await page.setViewportSize(viewport);
    await fitsViewport(page, '.creature, #combat-confirm, #combat-back, #combat-cancel, #switch-seat, #settings');
  }
});

test('a changed asking clears the open draft before it can be confirmed', async ({ page }) => {
  const state = await mount(page); await bolt(page); await page.locator('[data-creature="4"]').click();
  state.view.waitingAsked++;
  await expect(page.locator('#combat-targets')).toBeHidden({ timeout: 10000 }); expect(state.writes).toEqual([]);
  await bolt(page); await expect(page.locator('#combat-confirm')).toBeDisabled();
});
test('access revocation clears the open spell dialog and its private text', async ({ page }) => {
  const state = await mount(page); await page.locator('#combat-open').click(); state.seatStatus = 403;
  await expect(page.locator('#combat-dialog')).toBeHidden({ timeout: 10000 });
  await expect(page.locator('#combat-heading')).toBeEmpty(); await expect(page.locator('#combat-spells')).toBeEmpty();
  await expect(page.locator('#combat-open')).toBeHidden(); expect(state.writes).toEqual([]);
});
test('hiding a page cancels targeting and resuming does not acknowledge unseen controls', async ({ page }) => {
  const state = await mount(page); await bolt(page); await page.locator('[data-creature="4"]').click();
  await expect.poll(() => state.shown).toEqual([11]);
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
  await expect(page.locator('#combat-targets')).toBeHidden(); await expect(page.locator('#combat-title')).toBeEmpty();
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: false }); document.dispatchEvent(new Event('visibilitychange')); });
  await expect(page.locator('#combat-open')).toBeVisible(); expect(state.shown).toEqual([11]); expect(state.writes).toEqual([]);
});

test('failed acknowledgement disables choices and asks for an explicit reopen', async ({ page }) => {
  const state = await mount(page); state.shownStatus = 503; await page.locator('#combat-open').click();
  await expect(page.locator('#combat-dialog-error')).toContainText('Close and reopen');
  await expect(page.locator('[data-spell="bolt"]')).toBeDisabled(); expect(state.writes).toEqual([]);
  state.shownStatus = 200; await page.locator('#combat-dialog-close').click(); await bolt(page);
  await page.locator('[data-creature="4"]').click(); await page.locator('#combat-confirm').click();
  await expect.poll(() => state.writes.length).toBe(1); expect(state.shown).toEqual([11, 11]);
});

test('too few candidates expose an explicit empty cast without partial target buttons', async ({ page }) => {
  const state = await mount(page, { targets: legal([4], 2, 2) }); await bolt(page);
  await expect(page.locator('[data-creature="4"]')).toBeDisabled(); await expect(page.locator('#combat-selection')).toContainText('Too few legal targets');
  await expect(page.locator('#combat-confirm')).toHaveText('Cast without targets'); await page.locator('#combat-confirm').click();
  await expect.poll(() => state.writes[0]?.targets).toEqual([]);
});

test('opening and rejected-action controls stay inside the dock on short screens', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'The viewports are the test\'s own');
  for (const viewport of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(viewport); const state = await mount(page); state.status = 409; state.message = 'This action could not be accepted. Please review the latest question before trying again.';
    async function contained() {
      await expect.poll(() => page.evaluate(() => {
        const dock = document.querySelector('.command-dock').getBoundingClientRect();
        return [...document.querySelectorAll('#combat-open, #play-link')].filter(node => !node.hidden).flatMap(node => {
          const box = node.getBoundingClientRect();
          return box.top < dock.top - 1 || box.bottom > dock.bottom + 1 ? [node.id] : [];
        });
      })).toEqual([]);
    }
    await contained(); await bolt(page); await page.locator('[data-creature="4"]').click(); await page.locator('#combat-confirm').click();
    await expect(page.locator('#combat-error')).toHaveText(state.message); await contained();
  }
});
